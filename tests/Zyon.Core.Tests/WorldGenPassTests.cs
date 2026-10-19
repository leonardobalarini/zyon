using System.Numerics;
using Zyon.Core.WorldGen;
using Zyon.Core.WorldGen.Passes;
using static Zyon.Core.Tests.WorldGenTestKit;

namespace Zyon.Core.Tests;

public class WorldGenPassTests
{
    [Fact]
    public void Terrain_SurfaceWithinBand_AirAbove_SolidBelow()
    {
        var ctx = Run(new TerrainPass());
        var cfg = ctx.Config;
        int min = (int)MathF.Round(cfg.SurfaceMin * Height);
        int max = (int)MathF.Round(cfg.SurfaceMax * Height);

        for (int x = 0; x < Width; x++)
        {
            int s = ctx.Surface[x];
            Assert.InRange(s, min, max);
            Assert.Equal(0, ctx.World.GetBlock(x, s - 1));
            Assert.NotEqual(0, ctx.World.GetBlock(x, s));
            Assert.NotEqual(0, ctx.World.GetBlock(x, Height - 1));
        }
    }

    [Fact]
    public void Terrain_IsNotFlat()
    {
        var ctx = Run(new TerrainPass());

        Assert.True(ctx.Surface.Distinct().Count() > 3);
    }

    [Fact]
    public void Layers_DirtOnTop_StoneBelow_DeepStoneAtBottom()
    {
        var ctx = Run(new TerrainPass(), new LayersPass());
        var m = ctx.Materials;

        for (int x = 0; x < Width; x++)
        {
            int s = ctx.Surface[x];
            int d = ctx.DirtBottom[x];
            Assert.True(d > s, $"coluna {x}: DirtBottom {d} <= superfície {s}");
            Assert.Equal(m.Dirt, ctx.World.GetBlock(x, s));
            Assert.Equal(m.Dirt, ctx.World.GetBlock(x, d - 1));
            Assert.Equal(m.Stone, ctx.World.GetBlock(x, d));
            Assert.Equal(m.DeepStone, ctx.World.GetBlock(x, Height - 1));
        }
    }

    [Fact]
    public void Layers_BoundaryIsIrregular()
    {
        var ctx = Run(new TerrainPass(), new LayersPass());

        var depths = Enumerable.Range(0, Width).Select(x => ctx.DirtBottom[x] - ctx.Surface[x]);
        Assert.True(depths.Distinct().Count() > 2);
    }

    private static WorldGenContext WideTerrain()
    {
        var ctx = Context(4200, 1200, 7);
        WorldGenerator.RunPass(ctx, new TerrainPass());
        return ctx;
    }

    [Fact]
    public void Terrain_HasPlainsAndHills()
    {
        var ctx = WideTerrain();
        var ranges = new List<int>();
        for (int start = 0; start + 200 <= 4200; start += 100)
        {
            var w = ctx.Surface.Skip(start).Take(200).ToArray();
            ranges.Add(w.Max() - w.Min());
        }
        int lo = ranges.Min(), hi = ranges.Max();
        Console.WriteLine($"window ranges: min={lo} max={hi} all=[{string.Join(",", ranges)}]");

        Assert.True(lo <= 8, $"sem planície: menor faixa {lo}");
        Assert.True(hi >= 25 && hi >= 3 * lo, $"sem colinas: maior faixa {hi}, menor {lo}");
    }

    [Fact]
    public void Terrain_HasNoCliffsTallerThanAJump()
    {
        var ctx = WideTerrain();

        for (int x = 0; x + 1 < 4200; x++)
            Assert.True(Math.Abs(ctx.Surface[x + 1] - ctx.Surface[x]) <= 3, $"degrau na coluna {x}");
    }
}
