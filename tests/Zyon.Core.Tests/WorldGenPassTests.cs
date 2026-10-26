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
    public void Layers_FollowTheBiomeOfEachTile_AndDeepStoneBelow()
    {
        var ctx = Run(new TerrainPass(), new MountainPass(), new BiomesPass(), new LayersPass());
        var m = ctx.Materials;
        var map = ctx.Biomes!;

        for (int x = 0; x < Width; x++)
        {
            int s = ctx.Surface[x], d = ctx.DirtBottom[x], deep = ctx.DeepStart[x];
            Assert.True(d > s, $"coluna {x}: DirtBottom {d} <= superfície {s}");
            Assert.True(deep > d, $"coluna {x}: DeepStart {deep} <= DirtBottom {d}");
            for (int y = s; y < Height; y++)
            {
                var b = map.At(x, y, s);
                ushort expected = y >= deep ? m.DeepStone : y < d ? b.SurfaceBlock : b.ShallowBlock;
                Assert.Equal(expected, ctx.World.GetBlock(x, y));
            }
        }
    }

    [Fact]
    public void Layers_HaveTheBlocksOfAllThreeBiomes()
    {
        var ctx = Run(new TerrainPass(), new MountainPass(), new BiomesPass(), new LayersPass());
        var r = ctx.Registry;
        var present = new HashSet<ushort>();
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
            present.Add(ctx.World.GetBlock(x, y));

        foreach (var name in new[] { "dirt", "stone", "snow", "ice", "sand", "sandstone", "deep_stone" })
            Assert.Contains(r.BlockId(name), present);
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

    [Fact]
    public void Caves_CarveAirUnderground()
    {
        var ctx = Run(new TerrainPass(), new LayersPass(), new CavesPass());
        var cfg = ctx.Config;
        int floor = Height - cfg.BottomSolidRows;

        int carved = 0;
        for (int x = 0; x < Width; x++)
            for (int y = ctx.Surface[x] + 1; y < floor; y++)
                if (ctx.World.GetBlock(x, y) == 0) carved++;

        Assert.True(carved > 200, $"só {carved} tiles escavados");
    }

    [Fact]
    public void Caves_KeepSurfaceTileAndBottomRowsSolid()
    {
        var ctx = Run(new TerrainPass(), new LayersPass(), new CavesPass());
        int floor = Height - ctx.Config.BottomSolidRows;

        for (int x = 0; x < Width; x++)
        {
            Assert.NotEqual(0, ctx.World.GetBlock(x, ctx.Surface[x]));
            for (int y = floor; y < Height; y++)
                Assert.NotEqual(0, ctx.World.GetBlock(x, y));
        }
    }

    private static WorldGenContext WideCaves()
    {
        var ctx = Context(4200, 1200, 7);
        WorldGenerator.RunPass(ctx, new TerrainPass());
        WorldGenerator.RunPass(ctx, new LayersPass());
        WorldGenerator.RunPass(ctx, new CavesPass());
        return ctx;
    }

    [Fact]
    public void Caves_SomeTunnelsStartNearTheSurface()
    {
        var ctx = WideCaves();
        int limit = ctx.Config.CaveMinDepth + ctx.Config.ShallowWormDepth;

        int shallow = 0;
        for (int x = 0; x < 4200; x++)
            for (int y = ctx.Surface[x] + 1; y <= ctx.Surface[x] + limit; y++)
                if (ctx.World.GetBlock(x, y) == 0) shallow++;
        Console.WriteLine($"shallow carved={shallow}");

        Assert.True(shallow > 500, $"só {shallow} tiles escavados perto da superfície");
    }

    [Fact]
    public void Caves_UndergroundCarvedFractionIsReasonable()
    {
        var ctx = WideCaves();
        int floor = 1200 - ctx.Config.BottomSolidRows;

        long carved = 0, total = 0;
        for (int x = 0; x < 4200; x++)
            for (int y = ctx.Surface[x] + 1; y < floor; y++)
            {
                total++;
                if (ctx.World.GetBlock(x, y) == 0) carved++;
            }
        double fraction = (double)carved / total;
        Console.WriteLine($"carved fraction={fraction:P2}");

        Assert.InRange(fraction, 0.08, 0.20);
    }

    [Fact]
    public void Ores_AppearOnlyWithinTheirDepthBand()
    {
        var ctx = Run(new TerrainPass(), new LayersPass(), new CavesPass(), new OrePass());

        foreach (var (block, ore) in ctx.Materials.Ores)
        {
            int yMin = (int)(ore.MinDepth * Height);
            int yMax = Math.Max(yMin + 1, (int)(ore.MaxDepth * Height));
            int count = 0;
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                if (ctx.World.GetBlock(x, y) != block) continue;
                count++;
                Assert.InRange(y, yMin, yMax - 1);
            }
            Assert.True(count > 0, $"minério {ore.Block} não apareceu");
        }
    }

    [Fact]
    public void Ores_NeverReplaceTheSurfaceLayerOrAir()
    {
        var before = Run(new TerrainPass(), new MountainPass(), new BiomesPass(), new LayersPass(), new CavesPass());
        var after = Run(new TerrainPass(), new MountainPass(), new BiomesPass(), new LayersPass(), new CavesPass(), new OrePass());
        var surfaceBlocks = after.Biomes!.Set.All.Select(b => b.SurfaceBlock).ToHashSet();

        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
        {
            ushort old = before.World.GetBlock(x, y);
            if (old == 0 || surfaceBlocks.Contains(old))
                Assert.Equal(old, after.World.GetBlock(x, y));
        }
    }

    [Fact]
    public void Ores_AlsoReplaceIceAndSandstone()
    {
        var root = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(TestData.DataDir, "worldgen.json")))!;
        root["ores"] = System.Text.Json.Nodes.JsonNode.Parse(
            """[{"block":"ore_a","minDepth":0.3,"maxDepth":0.65,"veinsPerMillionTiles":3000,"veinSizeMin":10,"veinSizeMax":14}]""");
        var config = WorldGenConfigLoader.Load(root.ToJsonString());
        var loaded = TestData.WorldGenConfig();
        config.Biomes = loaded.Biomes;
        IWorldGenPass[] layers = [new TerrainPass(), new MountainPass(), new BiomesPass(), new LayersPass()];

        var before = RunWith(config, layers);
        var after = RunWith(config, [.. layers, new OrePass()]);
        var r = after.Registry;
        ushort ore = r.BlockId("ore_a");
        var replaced = new HashSet<ushort>();
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
            if (after.World.GetBlock(x, y) == ore && before.World.GetBlock(x, y) != ore)
                replaced.Add(before.World.GetBlock(x, y));

        Assert.Contains(r.BlockId("ice"), replaced);
        Assert.Contains(r.BlockId("sandstone"), replaced);
    }

    [Fact]
    public void Walls_FollowTheBiome_BelowTheSurface_AndNothingAbove()
    {
        var ctx = Run(new TerrainPass(), new MountainPass(), new BiomesPass(), new LayersPass(), new CavesPass(), new WallsPass());
        var m = ctx.Materials;
        var map = ctx.Biomes!;

        for (int x = 0; x < Width; x++)
        for (int y = 0; y < Height; y++)
        {
            ushort wall = ctx.World.GetWall(x, y);
            int s = ctx.Surface[x];
            if (y <= s)
            {
                Assert.Equal(0, wall);
                continue;
            }
            var b = map.At(x, y, s);
            ushort expected = y >= ctx.DeepStart[x] ? m.StoneWall : y < ctx.DirtBottom[x] ? b.SurfaceWall : b.ShallowWall;
            Assert.Equal(expected, wall);
        }
    }

    [Fact]
    public void Ores_StayInsideANarrowBand()
    {
        var root = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(TestData.DataDir, "worldgen.json")))!;
        root["ores"] = System.Text.Json.Nodes.JsonNode.Parse(
            """[{"block":"ore_a","minDepth":0.50,"maxDepth":0.52,"veinsPerMillionTiles":500,"veinSizeMin":10,"veinSizeMax":14}]""");
        var config = WorldGenConfigLoader.Load(root.ToJsonString());
        config.Biomes = TestData.WorldGenConfig().Biomes;

        var ctx = RunWith(config, new TerrainPass(), new LayersPass(), new CavesPass(), new OrePass());
        var (block, ore) = ctx.Materials.Ores[0];
        int yMin = (int)(ore.MinDepth * Height);
        int yMax = Math.Max(yMin + 1, (int)(ore.MaxDepth * Height));

        int count = 0;
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
        {
            if (ctx.World.GetBlock(x, y) != block) continue;
            count++;
            Assert.InRange(y, yMin, yMax - 1);
        }
        Assert.True(count > 0);
    }
}
