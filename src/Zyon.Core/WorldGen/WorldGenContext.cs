using System.Numerics;
using Zyon.Core.Data;
using Zyon.Core.Tiles;
using Zyon.Core.WorldGen.Structures;

namespace Zyon.Core.WorldGen;

/// <summary>Montanha do passe "mountain": pico, pé da face íngreme (portão), se o portão fica à esquerda do pico e o pé do lado suave.</summary>
public sealed record MountainInfo(int PeakX, int FootX, bool FacesLeft, int GentleFootX);

/// <summary>Onde o passe "bunker" carimbou o molde (canto superior esquerdo) e qual molde (já espelhado se preciso).</summary>
public sealed record BunkerPlacement(int X, int Y, Structure Structure);

/// <summary>
/// Estado compartilhado entre os passes. Dentro de Parallel.For use só ruído e escreva só na própria coluna:
/// GenRandom e Report não são thread-safe nem determinísticos fora de código sequencial.
/// </summary>
public sealed class WorldGenContext
{
    private readonly Action<float> report;

    internal WorldGenContext(TileWorld world, Registry registry, WorldGenConfig config,
        WorldGenMaterials materials, int seed, Action<float> report)
    {
        World = world;
        Registry = registry;
        Config = config;
        Materials = materials;
        Seed = seed;
        this.report = report;
        Surface = new int[world.Width];
        DirtBottom = new int[world.Width];
        DeepStart = new int[world.Width];
        Biomes = materials.Biomes is { } set ? new BiomeMap(set, world.Height) : null;
    }

    public TileWorld World { get; }
    public Registry Registry { get; }
    public WorldGenConfig Config { get; }
    public WorldGenMaterials Materials { get; }
    public int Seed { get; }

    /// <summary>Random exclusivo do passe atual, derivado de seed + nome do passe.</summary>
    public GenRandom Random { get; internal set; } = new(0);

    /// <summary>Linha do primeiro tile sólido de cada coluna (definida pelo passe de terreno).</summary>
    public int[] Surface { get; }

    /// <summary>Primeira linha abaixo da camada de solo de cada coluna (definida pelo passe de camadas).</summary>
    public int[] DirtBottom { get; }

    /// <summary>Primeira linha da rocha profunda de cada coluna (definida pelo passe de camadas).</summary>
    public int[] DeepStart { get; }

    /// <summary>Bioma de cada tile; só o padrão até o passe "biomes" rodar. Null se a configuração não tem biomas (<c>Load(json)</c>).</summary>
    public BiomeMap? Biomes { get; }

    /// <summary>Colunas das raízes plantadas pelo passe "trees", em ordem crescente (só diagnóstico, para os testes).</summary>
    internal List<int> PlantedTreeRoots { get; } = [];

    /// <summary>Definido pelo passe "mountain".</summary>
    public MountainInfo? Mountain { get; internal set; }

    /// <summary>Definido pelo passe "bunker".</summary>
    public BunkerPlacement? Bunker { get; internal set; }

    /// <summary>Ponto sob os pés do jogador no spawn: X no centro, Y na linha de cima do chão.</summary>
    public Vector2 SpawnFeet { get; private set; }

    public bool SpawnFound { get; private set; }

    public void SetSpawn(Vector2 feet)
    {
        SpawnFeet = feet;
        SpawnFound = true;
    }

    /// <summary>Progresso do passe atual em [0, 1]. Somente em código sequencial.</summary>
    public void Report(float fraction) => report(Math.Clamp(fraction, 0f, 1f));

    /// <summary>Ruído OpenSimplex2 semeado por seed + nome (fBm quando octaves > 1). GetNoise é puro e pode ser usado em paralelo.</summary>
    public FastNoiseLite CreateNoise(string name, float frequency, int octaves)
    {
        var noise = new FastNoiseLite(StableHash.Seed(Seed, name));
        noise.SetNoiseType(FastNoiseLite.NoiseType.OpenSimplex2);
        noise.SetFrequency(frequency);
        if (octaves > 1)
        {
            noise.SetFractalType(FastNoiseLite.FractalType.FBm);
            noise.SetFractalOctaves(octaves);
        }
        return noise;
    }
}
