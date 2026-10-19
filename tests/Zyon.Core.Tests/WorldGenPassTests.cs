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
    public void Ores_NeverReplaceDirtOrAir()
    {
        var before = Run(new TerrainPass(), new LayersPass(), new CavesPass());
        var after = Run(new TerrainPass(), new LayersPass(), new CavesPass(), new OrePass());
        var m = after.Materials;

        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
        {
            ushort old = before.World.GetBlock(x, y);
            if (old == 0 || old == m.Dirt)
                Assert.Equal(old, after.World.GetBlock(x, y));
        }
    }

    [Fact]
    public void Walls_FillBelowSurface_IncludingCaves_AndNothingAbove()
    {
        var ctx = Run(new TerrainPass(), new LayersPass(), new CavesPass(), new WallsPass());
        var m = ctx.Materials;

        for (int x = 0; x < Width; x++)
        for (int y = 0; y < Height; y++)
        {
            ushort wall = ctx.World.GetWall(x, y);
            if (y <= ctx.Surface[x])
                Assert.Equal(0, wall);
            else
                Assert.Equal(y < ctx.DirtBottom[x] ? m.DirtWall : m.StoneWall, wall);
        }
    }

    [Fact]
    public void Ores_StayInsideANarrowBand()
    {
        var root = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(TestData.DataDir, "worldgen.json")))!;
        root["ores"] = System.Text.Json.Nodes.JsonNode.Parse(
            """[{"block":"ore_a","minDepth":0.50,"maxDepth":0.52,"veinsPerMillionTiles":500,"veinSizeMin":10,"veinSizeMax":14}]""");
        var config = WorldGenConfigLoader.Load(root.ToJsonString());

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

    private static void AssertValidSpawn(WorldGenContext ctx)
    {
        Assert.True(ctx.SpawnFound);
        int feetY = (int)ctx.SpawnFeet.Y;
        Assert.Equal(ctx.SpawnFeet.Y, feetY);   // pés exatamente no topo de um tile
        int flat = ctx.Config.SpawnFlatWidth;
        int x0 = (int)MathF.Floor(ctx.SpawnFeet.X - flat / 2f);
        for (int x = x0; x < x0 + flat; x++)
        {
            Assert.NotEqual(0, ctx.World.GetBlock(x, feetY));
            for (int y = feetY - SpawnPass.ClearanceRows; y < feetY; y++)
                Assert.Equal(0, ctx.World.GetBlock(x, y));
        }
    }

    [Fact]
    public void Spawn_OnNaturalTerrain_IsFlatSolidAndClear()
    {
        var ctx = Run(new TerrainPass(), new LayersPass(), new CavesPass(), new OrePass(), new WallsPass(), new SpawnPass());

        AssertValidSpawn(ctx);
        Assert.InRange(ctx.SpawnFeet.X, Width / 2f - ctx.Config.SpawnSearchRadius - 4, Width / 2f + ctx.Config.SpawnSearchRadius + 4);
    }

    [Fact]
    public void Spawn_WithoutFlatGround_FlattensAtCenter()
    {
        var ctx = Context();
        ushort dirt = ctx.Materials.Dirt;
        for (int x = 0; x < Width; x++)
        {
            ctx.Surface[x] = 100 + (x % 2);   // zigue-zague: nunca 2 colunas iguais seguidas
            for (int y = ctx.Surface[x]; y < Height; y++)
                ctx.World.SetBlock(x, y, dirt);
        }

        WorldGenerator.RunPass(ctx, new SpawnPass());

        AssertValidSpawn(ctx);
        Assert.Equal(Width / 2 + ctx.Config.SpawnFlatWidth / 2f, ctx.SpawnFeet.X);
    }

    [Theory]
    [InlineData(512)]   // coluna central par: é a alta (y=100); vizinhas em y=110
    [InlineData(514)]   // Width/2 ímpar: a coluna central é a baixa (y=110)
    public void Spawn_FallbackWithTallZigzag_LeavesNoOverhang(int width)
    {
        var ctx = Context(width, Height, Seed);
        ushort dirt = ctx.Materials.Dirt;
        for (int x = 0; x < width; x++)
        {
            ctx.Surface[x] = 100 + (x % 2) * 10;   // degraus de 10 linhas, maiores que ClearanceRows
            for (int y = ctx.Surface[x]; y < Height; y++)
            {
                ctx.World.SetBlock(x, y, dirt);
                ctx.World.SetWall(x, y, ctx.Materials.DirtWall);   // como o WallsPass: parede sob o terreno
            }
        }

        WorldGenerator.RunPass(ctx, new SpawnPass());

        AssertValidSpawn(ctx);
        int feetY = (int)ctx.SpawnFeet.Y;
        int flat = ctx.Config.SpawnFlatWidth;
        int x0 = (int)MathF.Floor(ctx.SpawnFeet.X - flat / 2f);
        for (int x = x0; x < x0 + flat; x++)
        {
            int firstSolid = 0;
            while (ctx.World.GetBlock(x, firstSolid) == 0) firstSolid++;
            Assert.Equal(feetY, firstSolid);
            Assert.Equal(firstSolid, ctx.Surface[x]);

            for (int y = 0; y <= feetY; y++)
                Assert.Equal(0, ctx.World.GetWall(x, y));          // sem parede no ar livre
            for (int y = feetY + 1; y < Height; y++)
                Assert.NotEqual(0, ctx.World.GetWall(x, y));       // sólido preenchido tem parede
        }
    }
}
