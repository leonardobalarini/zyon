using Zyon.Core.WorldGen.Passes;
using System.Numerics;
using Zyon.Core.Data;
using Zyon.Core.Sim;
using Zyon.Core.Tiles;

namespace Zyon.Core.WorldGen;

public sealed record GeneratedWorld(TileWorld Tiles, int Seed, int GeneratorVersion, ulong ConfigFingerprint, Vector2 SpawnFeet)
{
    /// <summary>Centro do AABB do jogador em pé no ponto de spawn.</summary>
    public Vector2 PlayerSpawnCenter(PhysicsConfig physics) => SpawnFeet - new Vector2(0f, physics.PlayerHeight / 2);
}

/// <summary>Roda os passes em ordem. Mesma seed + mesma Version + mesmos dados ⇒ mesmo mundo.</summary>
public sealed class WorldGenerator
{
    /// <summary>Incremente sempre que a saída da geração mudar para a mesma seed.</summary>
    public const int Version = 2;
    public const int MinSize = 64;

    private readonly Registry registry;
    private readonly WorldGenConfig config;
    private readonly WorldGenMaterials materials;
    private readonly ulong fingerprint;

    /// <summary>Gerador com os passes padrão (terreno, montanha, biomas, camadas, cavernas, minérios, paredes, bunker).</summary>
    public WorldGenerator(Registry registry, WorldGenConfig config)
        : this(registry, config, DefaultPasses())
    {
    }

    public static IReadOnlyList<IWorldGenPass> DefaultPasses() =>
    [
        new TerrainPass(),
        new MountainPass(),
        new BiomesPass(),
        new LayersPass(),
        new CavesPass(),
        new OrePass(),
        new WallsPass(),
        new BunkerPass(),
    ];

    public WorldGenerator(Registry registry, WorldGenConfig config, IReadOnlyList<IWorldGenPass> passes)
    {
        this.registry = registry;
        this.config = config;
        fingerprint = WorldGenConfigFingerprint.Compute(config);
        Passes = passes.ToArray();
        var seen = new HashSet<string>();
        foreach (var pass in Passes)
        {
            if (!seen.Add(pass.Name))
                throw new ArgumentException($"Nome de passe duplicado: '{pass.Name}'", nameof(passes));
        }
        materials = WorldGenMaterials.Resolve(config, registry);
    }

    public IReadOnlyList<IWorldGenPass> Passes { get; }

    public GeneratedWorld Generate(int width, int height, int seed,
        IProgress<WorldGenProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (width < MinSize) throw new ArgumentOutOfRangeException(nameof(width), $"Largura mínima: {MinSize}");
        if (height < MinSize) throw new ArgumentOutOfRangeException(nameof(height), $"Altura mínima: {MinSize}");
        if (config.BottomSolidRows + config.CaveMinDepth >= height / 2)
            throw new ArgumentOutOfRangeException(nameof(height),
                $"Altura {height} é pequena demais para bottomSolidRows={config.BottomSolidRows} + caveMinDepth={config.CaveMinDepth} (devem somar menos que altura / 2)");

        var world = new TileWorld(width, height);
        int current = 0;
        int count = Passes.Count;
        var ctx = new WorldGenContext(world, registry, config, materials, seed,
            f => progress?.Report(new WorldGenProgress(Passes[current].Name, current, count, (current + f) / count)));

        for (current = 0; current < count; current++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ctx.Report(0f);
            RunPass(ctx, Passes[current]);
            ctx.Report(1f);
        }

        if (!ctx.SpawnFound)
            throw new InvalidOperationException("Nenhum passe definiu o ponto de spawn.");

        foreach (var chunk in world.Chunks)
            chunk.Dirty = false;
        return new GeneratedWorld(world, seed, Version, fingerprint, ctx.SpawnFeet);
    }

    /// <summary>Prepara o Random do passe (seed + nome) e o executa.</summary>
    internal static void RunPass(WorldGenContext ctx, IWorldGenPass pass)
    {
        ctx.Random = new GenRandom(StableHash.Seed(ctx.Seed, pass.Name));
        pass.Apply(ctx);
    }
}
