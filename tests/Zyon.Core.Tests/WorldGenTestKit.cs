using Zyon.Core.Tiles;
using Zyon.Core.WorldGen;

namespace Zyon.Core.Tests;

internal static class WorldGenTestKit
{
    public const int Width = 512;
    public const int Height = 256;
    public const int Seed = 7;

    public static WorldGenContext Context(int width = Width, int height = Height, int seed = Seed)
    {
        var registry = TestData.Registry();
        var config = TestData.WorldGenConfig();
        return new WorldGenContext(new TileWorld(width, height), registry, config,
            WorldGenMaterials.Resolve(config, registry), seed, _ => { });
    }

    /// <summary>Roda os passes em sequência num mundo de teste, do mesmo jeito que o gerador.</summary>
    public static WorldGenContext Run(params IWorldGenPass[] passes)
    {
        var ctx = Context();
        foreach (var pass in passes)
            WorldGenerator.RunPass(ctx, pass);
        return ctx;
    }
}
