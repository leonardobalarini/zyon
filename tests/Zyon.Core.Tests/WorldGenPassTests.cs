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
}
