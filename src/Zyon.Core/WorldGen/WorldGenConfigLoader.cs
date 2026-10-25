using System.Text.Json;
using Zyon.Core.Data;
using Zyon.Core.WorldGen.Structures;

namespace Zyon.Core.WorldGen;

public static class WorldGenConfigLoader
{
    public const string FileName = "worldgen.json";

    public static WorldGenConfig LoadFromDirectory(string dataDir)
    {
        var path = Path.Combine(dataDir, FileName);
        if (!File.Exists(path))
            throw new DataException($"{FileName}: arquivo não encontrado em '{dataDir}'");
        var config = Load(File.ReadAllText(path));
        config.Bunker = StructureLoader.LoadFromDirectory(dataDir, config.BunkerStructure);
        return config;
    }

    public static WorldGenConfig Load(string json)
    {
        WorldGenConfig config;
        try
        {
            config = JsonSerializer.Deserialize<WorldGenConfig>(json, RegistryLoader.Options)
                     ?? throw new DataException($"{FileName}: conteúdo vazio");
        }
        catch (JsonException ex)
        {
            throw new DataException($"{FileName}: JSON inválido — {ex.Message}");
        }
        Validate(config);
        return config;
    }

    private static void Validate(WorldGenConfig c)
    {
        static void Require(bool condition, string message)
        {
            if (!condition) throw new DataException($"{FileName}: {message}");
        }

        Require(c.Materials is not null, "campo 'materials' não pode ser nulo");
        Require(c.Ores is not null, "campo 'ores' não pode ser nulo");
        var m = c.Materials!;
        Require(!string.IsNullOrWhiteSpace(m.Dirt), "campo 'materials.dirt' ausente");
        Require(!string.IsNullOrWhiteSpace(m.Stone), "campo 'materials.stone' ausente");
        Require(!string.IsNullOrWhiteSpace(m.DeepStone), "campo 'materials.deepStone' ausente");
        Require(!string.IsNullOrWhiteSpace(m.DirtWall), "campo 'materials.dirtWall' ausente");
        Require(!string.IsNullOrWhiteSpace(m.StoneWall), "campo 'materials.stoneWall' ausente");
        Require(c.SurfaceMin > 0 && c.SurfaceMin < c.SurfaceMax && c.SurfaceMax < 1,
            "'surfaceMin' e 'surfaceMax' devem satisfazer 0 < surfaceMin < surfaceMax < 1");
        Require(c.DeepLayerStart > c.SurfaceMax && c.DeepLayerStart < 1,
            "'deepLayerStart' deve estar entre 'surfaceMax' e 1");
        Require(c.TerrainFrequency > 0 && c.LayerNoiseFrequency > 0 && c.CaveFrequency > 0,
            "frequências de ruído devem ser > 0");
        Require(c.HillinessFrequency > 0, "'hillinessFrequency' deve ser > 0");
        Require(c.HillinessMin >= 0 && c.HillinessMin <= 1, "'hillinessMin' deve estar entre 0 e 1");
        Require(c.TerrainOctaves >= 1, "'terrainOctaves' deve ser >= 1");
        Require(c.DirtDepthVariation >= 0, "'dirtDepthVariation' deve ser >= 0");
        Require(c.DirtDepth - c.DirtDepthVariation >= 1, "'dirtDepth' deve exceder 'dirtDepthVariation'");
        Require(c.CaveThreshold > -1 && c.CaveThreshold < 1, "'caveThreshold' deve estar entre -1 e 1 (exclusivo)");
        Require(c.DeepLayerVariation >= 0, "'deepLayerVariation' deve ser >= 0");
        Require(c.CaveMinDepth >= 1, "'caveMinDepth' deve ser >= 1");
        Require(c.BottomSolidRows >= 1, "'bottomSolidRows' deve ser >= 1");
        Require(c.WormsPerThousandColumns >= 0, "'wormsPerThousandColumns' deve ser >= 0");
        Require(c.WormLengthMin >= 1 && c.WormLengthMin <= c.WormLengthMax,
            "'wormLengthMin' e 'wormLengthMax' devem satisfazer 1 <= min <= max");
        Require(c.ShallowWormFraction >= 0 && c.ShallowWormFraction <= 1, "'shallowWormFraction' deve estar entre 0 e 1");
        Require(c.ShallowWormDepth >= 1, "'shallowWormDepth' deve ser >= 1");
        Require(c.WormRadiusMin > 0 && c.WormRadiusMin <= c.WormRadiusMax,
            "'wormRadiusMin' e 'wormRadiusMax' devem satisfazer 0 < min <= max");

        Require(c.MountainSteepWidth >= 4, "'mountainSteepWidth' deve ser >= 4");
        Require(c.MountainGentleWidth >= 4, "'mountainGentleWidth' deve ser >= 4");
        Require(c.MountainHeight > 0, "'mountainHeight' deve ser > 0");
        Require(c.MountainNoiseFrequency > 0, "'mountainNoiseFrequency' deve ser > 0");
        Require(c.MountainNoiseAmplitude >= 0, "'mountainNoiseAmplitude' deve ser >= 0");
        Require(c.BunkerRoofRows >= 1, "'bunkerRoofRows' deve ser >= 1");
        Require(!string.IsNullOrEmpty(c.BunkerStructure) && c.BunkerStructure.All(ch => ch is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_'),
            "'bunkerStructure' deve ter só letras minúsculas, dígitos e '_'");

        for (int i = 0; i < c.Ores!.Count; i++)
        {
            var o = c.Ores[i];
            var ctx = $"ores[{i}]";
            Require(o is not null, $"{ctx}: entrada nula");
            Require(!string.IsNullOrWhiteSpace(o!.Block), $"{ctx}: campo 'block' ausente");
            Require(o.MinDepth >= 0 && o.MinDepth < o.MaxDepth && o.MaxDepth <= 1,
                $"{ctx}: 'minDepth' e 'maxDepth' devem satisfazer 0 <= min < max <= 1");
            Require(o.VeinsPerMillionTiles >= 0, $"{ctx}: 'veinsPerMillionTiles' deve ser >= 0");
            Require(o.VeinSizeMin >= 1 && o.VeinSizeMin <= o.VeinSizeMax && o.VeinSizeMax <= 10000,
                $"{ctx}: 'veinSizeMin' e 'veinSizeMax' devem satisfazer 1 <= min <= max <= 10000");
        }
    }
}
