using System.Collections.Concurrent;
using Zyon.Core.Data;
using Zyon.Core.Tiles;
using Zyon.Core.WorldGen;
using static Zyon.Core.Tests.WorldGenTestKit;

namespace Zyon.Core.Tests;

public class BunkerPassTests
{
    /// <summary>Colunas além do portão, no sentido de fora, onde o caminho precisa chegar ao céu aberto.</summary>
    private const int WalkOutColumns = 10;

    // Os testes só leem o mundo gerado: cada mundo é gerado uma vez e compartilhado (o 4200×1200 é caro).
    private static readonly ConcurrentDictionary<(int, int, int), Lazy<WorldGenContext>> Cache = new();

    private static WorldGenContext RunAll(int width = Width, int height = Height, int seed = Seed, WorldGenConfig? config = null)
    {
        var ctx = Context(width, height, seed, config);
        foreach (var pass in WorldGenerator.DefaultPasses())
            WorldGenerator.RunPass(ctx, pass);
        return ctx;
    }

    private static WorldGenContext Generated(int width, int height, int seed) =>
        Cache.GetOrAdd((width, height, seed), k => new Lazy<WorldGenContext>(() => RunAll(k.Item1, k.Item2, k.Item3))).Value;

    private static WorldGenConfig MirroredConfig()
    {
        var cfg = WorldGenConfigLoader.LoadFromDirectory(TestData.DataDir);
        var json = File.ReadAllText(Path.Combine(TestData.DataDir, "worldgen.json")).Replace("\"mountainOffset\": 40", "\"mountainOffset\": -40");
        var mirrored = WorldGenConfigLoader.Load(json);
        mirrored.Bunker = cfg.Bunker;   // Load(json) não carrega o molde nem os biomas
        mirrored.Biomes = cfg.Biomes;
        return mirrored;
    }

    public static TheoryData<int, int, int> Worlds => new()
    {
        { 512, 256, 1 }, { 512, 256, 2 }, { 512, 256, 3 },
        { 1050, 300, 1 }, { 1050, 300, 42 },
        { 128, 128, 5 }, { 64, 64, 9 },
        { 4200, 1200, 1 }, { 4200, 1200, 2 },   // mundo pequeno real (WorldSize.Small)
    };

    [Theory]
    [MemberData(nameof(Worlds))]
    public void Bunker_IsStampedTileByTile(int width, int height, int seed)
    {
        var ctx = Generated(width, height, seed);
        var (x0, y0, s) = ctx.Bunker!;

        for (int y = 0; y < s.Height; y++)
        for (int x = 0; x < s.Width; x++)
        {
            var t = s[x, y];
            if (t.Keep) continue;
            Assert.Equal(t.Block, ctx.World.GetBlock(x0 + x, y0 + y));
            Assert.Equal(t.Wall, ctx.World.GetWall(x0 + x, y0 + y));
        }
    }

    [Theory]
    [MemberData(nameof(Worlds))]
    public void Spawn_IsOnTheMarkedTile_InsideTheBunker(int width, int height, int seed)
    {
        var ctx = Generated(width, height, seed);
        var (x0, y0, s) = ctx.Bunker!;

        Assert.Equal(new System.Numerics.Vector2(x0 + s.Spawn.X + 0.5f, y0 + s.Spawn.Y + 1), ctx.SpawnFeet);
        Assert.Equal(ctx.Registry.WallId("metal_wall"), ctx.World.GetWall(x0 + s.Spawn.X, y0 + s.Spawn.Y));

        int sx = (int)ctx.SpawnFeet.X, floor = (int)ctx.SpawnFeet.Y;
        Assert.True(ctx.Registry.Block(ctx.World.GetBlock(sx, floor)).Solid, $"chão sob os pés ({sx}, {floor}) não é sólido");
        for (int x = sx - 1; x <= sx + 1; x++)
        for (int y = floor - 3; y < floor; y++)
            Assert.False(ctx.Registry.Block(ctx.World.GetBlock(x, y)).Solid, $"tile sólido em ({x}, {y}) no espaço do jogador");
    }

    [Theory]
    [MemberData(nameof(Worlds))]
    public void Roof_HasSolidRowsAboveEveryColumn(int width, int height, int seed)
    {
        var ctx = Generated(width, height, seed);
        var (x0, y0, s) = ctx.Bunker!;
        int roof = ctx.Config.BunkerRoofRows;

        for (int x = 0; x < s.Width; x++)
        {
            int top = s.TopRow(x);
            for (int y = Math.Max(0, y0 + top - roof); y < y0 + top; y++)
                Assert.True(ctx.World.GetBlock(x0 + x, y) != 0, $"buraco no teto em ({x0 + x}, {y})");
        }
    }

    [Theory]
    [MemberData(nameof(Worlds))]
    public void Player_CanWalkFromSpawnToOpenSky(int width, int height, int seed)
    {
        AssertWalksOut(Generated(width, height, seed));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Surface_MatchesFirstBlockOverTheBunkerAndPlateau(bool mirrored)
    {
        var ctx = mirrored ? RunAll(config: MirroredConfig()) : RunAll();
        var (x0, _, s) = ctx.Bunker!;

        for (int x = x0 - 3; x < x0 + s.Width + 3; x++)
        {
            int first = 0;
            while (ctx.World.GetBlock(x, first) == 0) first++;
            Assert.Equal(first, ctx.Surface[x]);
        }
    }

    [Fact]
    public void MirroredMountain_PutsTheGateOnTheRight_AndStillWalksOut()
    {
        var ctx = RunAll(config: MirroredConfig());
        var s = ctx.Bunker!.Structure;

        Assert.Equal(s.Width - 1, s.Anchor.X);
        AssertWalksOut(ctx);
    }

    [Fact]
    public void WithoutTemplate_Throws()
    {
        var cfg = WorldGenConfigLoader.Load(File.ReadAllText(Path.Combine(TestData.DataDir, "worldgen.json")));
        cfg.Biomes = TestData.WorldGenConfig().Biomes;   // só o molde fica de fora

        var ex = Assert.Throws<InvalidOperationException>(() => RunAll(config: cfg));
        Assert.Contains("molde do bunker", ex.Message);
    }

    /// <summary>Do spawn, anda até céu aberto a pelo menos WalkOutColumns colunas além do portão, do lado de fora.</summary>
    private static void AssertWalksOut(WorldGenContext ctx)
    {
        var (x0, _, s) = ctx.Bunker!;
        int gateX = x0 + s.Anchor.X;
        bool outLeft = s.Anchor.X < s.Width / 2;
        int goalX = outLeft ? gateX - WalkOutColumns : gateX + WalkOutColumns;
        Assert.InRange(goalX, 0, ctx.World.Width - 1);
        int sx = (int)ctx.SpawnFeet.X, feet = (int)ctx.SpawnFeet.Y - 1;

        Assert.True(Walkable.ReachesOpenSky(ctx.World, ctx.Registry, sx, feet, x => outLeft ? x <= goalX : x >= goalX),
            $"sem caminho do spawn ({sx}, {feet}) até o céu aberto além da coluna {goalX}");
    }
}

/// <summary>
/// Caminho andável aproximado: o jogador como coluna de 1 tile × 3 de altura; anda para o lado no mesmo nível,
/// sobe até MaxStep tiles (pulo; o real alcança uns 4) ou cai qualquer altura. É uma aproximação de 1 coluna de
/// largura (o jogador real tem 1,25): vale porque o molde do bunker não tem vãos estreitos.
/// </summary>
internal static class Walkable
{
    public const int MaxStep = 2;

    public static bool ReachesOpenSky(TileWorld w, Registry r, int startX, int startFeet, Func<int, bool> goal)
    {
        bool Solid(int x, int y) => !w.InBounds(x, y) || r.Block(w.GetBlock(x, y)).Solid;
        bool Free(int x, int y) => !Solid(x, y) && !Solid(x, y - 1) && !Solid(x, y - 2);
        int Land(int x, int y)
        {
            while (y + 1 < w.Height && !Solid(x, y + 1)) y++;
            return y;
        }
        bool OpenSky(int x, int y)
        {
            if (w.GetWall(x, y) != 0) return false;
            for (int yy = 0; yy < y; yy++)
                if (w.GetBlock(x, yy) != 0) return false;
            return true;
        }

        var seen = new HashSet<(int, int)>();
        var queue = new Queue<(int X, int Y)>();
        queue.Enqueue((startX, Land(startX, startFeet)));
        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            if (!seen.Add((x, y))) continue;
            if (goal(x) && OpenSky(x, y)) return true;
            foreach (int dx in new[] { -1, 1 })
            {
                int nx = x + dx;
                if (Free(nx, y))
                {
                    queue.Enqueue((nx, Land(nx, y)));
                    continue;
                }
                for (int h = 1; h <= MaxStep; h++)   // sobe h tiles: espaço livre acima da posição atual e no destino
                {
                    if (!Free(x, y - h)) break;
                    if (Free(nx, y - h))
                    {
                        queue.Enqueue((nx, Land(nx, y - h)));
                        break;
                    }
                }
            }
        }
        return false;
    }
}

public class BunkerLightTests
{
    [Fact]
    public void SpawnTile_HasSameLightByDayAndNight_AndIsLitByLamps()
    {
        var registry = TestData.Registry();
        var ctx = WorldGenTestKit.Context(512, 256, 1234);
        foreach (var pass in WorldGenerator.DefaultPasses())
            WorldGenerator.RunPass(ctx, pass);
        using var view = new Zyon.Core.Sim.Simulation(registry, ctx.World);
        var config = TestData.LightingConfig();
        int sx = (int)ctx.SpawnFeet.X, sy = (int)ctx.SpawnFeet.Y - 1;   // tile dos pés
        var region = new Zyon.Core.Lighting.TileRect(sx - 30, sy - 20, sx + 30, sy + 20)
            .Expand(config.RegionMargin).ClampTo(view.Width, view.Height);
        var engine = new Zyon.Core.Lighting.LightEngine(config);
        var day = new Zyon.Core.Lighting.LightMap();
        var night = new Zyon.Core.Lighting.LightMap();
        engine.Compute(view, region, 1f, [], day);
        engine.Compute(view, region, 0f, [], night);

        Assert.Equal(night.Get(sx, sy), day.Get(sx, sy), 5);
        Assert.True(night.Get(sx, sy) > 0f, "as luminárias deveriam iluminar o spawn");
    }
}
