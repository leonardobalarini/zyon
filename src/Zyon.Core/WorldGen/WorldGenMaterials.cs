using Zyon.Core.Biomes;
using Zyon.Core.Data;
using Zyon.Core.WorldGen.Structures;

namespace Zyon.Core.WorldGen;

/// <summary>Ids numéricos dos materiais da geração, resolvidos pelo nome no Registry.</summary>
public sealed record WorldGenMaterials(
    ushort Dirt,
    ushort Stone,
    ushort DeepStone,
    ushort DirtWall,
    ushort StoneWall,
    IReadOnlyList<(ushort Block, OreConfig Config)> Ores)
{
    /// <summary>Molde do bunker resolvido no Registry; null quando a configuração veio de <c>Load(json)</c>.</summary>
    public Structure? Bunker { get; init; }

    /// <summary>Biomas resolvidos no Registry; null quando a configuração veio de <c>Load(json)</c>.</summary>
    public BiomeSet? Biomes { get; init; }

    public static WorldGenMaterials Resolve(WorldGenConfig config, Registry registry)
    {
        ushort Block(string name, string field) =>
            registry.TryGetBlockId(name, out var id)
                ? id
                : throw new DataException($"{WorldGenConfigLoader.FileName}: '{field}' referencia bloco inexistente '{name}'");

        ushort Wall(string name, string field) =>
            registry.TryGetWallId(name, out var id)
                ? id
                : throw new DataException($"{WorldGenConfigLoader.FileName}: '{field}' referencia parede inexistente '{name}'");

        var m = config.Materials;
        var ores = config.Ores
            .Select((ore, i) => (Block(ore.Block, $"ores[{i}].block"), ore))
            .ToList();

        return new WorldGenMaterials(
            Block(m.Dirt, "materials.dirt"),
            Block(m.Stone, "materials.stone"),
            Block(m.DeepStone, "materials.deepStone"),
            Wall(m.DirtWall, "materials.dirtWall"),
            Wall(m.StoneWall, "materials.stoneWall"),
            ores)
        {
            Bunker = config.Bunker?.Resolve(registry),
            Biomes = config.Biomes is null ? null : BiomeSet.Resolve(config.Biomes, registry),
        };
    }
}
