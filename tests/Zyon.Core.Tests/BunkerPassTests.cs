using Zyon.Core.Data;
using Zyon.Core.Tiles;
using Zyon.Core.WorldGen;
using static Zyon.Core.Tests.WorldGenTestKit;

namespace Zyon.Core.Tests;

public class BunkerPassTests
{
    private static WorldGenContext RunAll(int width = Width, int height = Height, int seed = Seed, WorldGenConfig? config = null)
    {
        var ctx = Context(width, height, seed, config);
        foreach (var pass in WorldGenerator.DefaultPasses())
            WorldGenerator.RunPass(ctx, pass);
        return ctx;
    }

    public static TheoryData<int, int, int> Worlds => new()
    {
        { 512, 256, 1 }, { 512, 256, 2 }, { 512, 256, 3 },
        { 1050, 300, 1 }, { 1050, 300, 42 },
        { 128, 128, 5 }, { 64, 64, 9 },
    };

    [Theory]
    [MemberData(nameof(Worlds))]
    public void Bunker_IsStampedTileByTile(int width, int height, int seed)
    {
        var ctx = RunAll(width, height, seed);
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
        var ctx = RunAll(width, height, seed);
        var (x0, y0, s) = ctx.Bunker!;

        Assert.Equal(new System.Numerics.Vector2(x0 + s.Spawn.X + 0.5f, y0 + s.Spawn.Y + 1), ctx.SpawnFeet);
        Assert.Equal(ctx.Registry.WallId("metal_wall"), ctx.World.GetWall(x0 + s.Spawn.X, y0 + s.Spawn.Y));
    }

    [Theory]
    [MemberData(nameof(Worlds))]
    public void Roof_HasSolidRowsAboveEveryColumn(int width, int height, int seed)
    {
        var ctx = RunAll(width, height, seed);
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
        var ctx = RunAll(width, height, seed);
        var (x0, _, s) = ctx.Bunker!;
        int sx = (int)ctx.SpawnFeet.X, feet = (int)ctx.SpawnFeet.Y - 1;

        Assert.True(Walkable.ReachesOpenSky(ctx.World, ctx.Registry, sx, feet, x => x < x0 || x >= x0 + s.Width),
            $"sem caminho do spawn ({sx}, {feet}) até o céu aberto");
    }

    [Fact]
    public void Surface_MatchesFirstBlockOverTheBunkerAndPlateau()
    {
        var ctx = RunAll();
        var (x0, _, s) = ctx.Bunker!;

        for (int x = x0 - 3; x < x0 + s.Width; x++)
        {
            int first = 0;
            while (ctx.World.GetBlock(x, first) == 0) first++;
            Assert.Equal(first, ctx.Surface[x]);
        }
    }

    [Fact]
    public void MirroredMountain_PutsTheGateOnTheRight_AndStillWalksOut()
    {
        var cfg = WorldGenConfigLoader.LoadFromDirectory(TestData.DataDir);
        var json = File.ReadAllText(Path.Combine(TestData.DataDir, "worldgen.json")).Replace("\"mountainOffset\": 40", "\"mountainOffset\": -40");
        var mirrored = WorldGenConfigLoader.Load(json);
        mirrored.Bunker = cfg.Bunker;   // Load(json) não carrega o molde
        var ctx = RunAll(config: mirrored);
        var (x0, _, s) = ctx.Bunker!;

        Assert.Equal(s.Width - 1, s.Anchor.X);
        int sx = (int)ctx.SpawnFeet.X, feet = (int)ctx.SpawnFeet.Y - 1;
        Assert.True(Walkable.ReachesOpenSky(ctx.World, ctx.Registry, sx, feet, x => x < x0 || x >= x0 + s.Width));
    }

    [Fact]
    public void WithoutTemplate_Throws()
    {
        var cfg = WorldGenConfigLoader.Load(File.ReadAllText(Path.Combine(TestData.DataDir, "worldgen.json")));

        var ex = Assert.Throws<InvalidOperationException>(() => RunAll(config: cfg));
        Assert.Contains("molde do bunker", ex.Message);
    }
}

/// <summary>
/// Caminho andável aproximado: o jogador como coluna de 1 tile × 3 de altura; anda para o lado no mesmo nível,
/// sobe 1 tile (pulo) ou cai qualquer altura.
/// </summary>
internal static class Walkable
{
    public static bool ReachesOpenSky(TileWorld w, Registry r, int startX, int startFeet, Func<int, bool> outside)
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
            if (outside(x) && OpenSky(x, y)) return true;
            foreach (int dx in new[] { -1, 1 })
            {
                int nx = x + dx;
                if (Free(nx, y)) queue.Enqueue((nx, Land(nx, y)));
                else if (Free(x, y - 1) && Free(nx, y - 1)) queue.Enqueue((nx, Land(nx, y - 1)));   // sobe 1 tile
            }
        }
        return false;
    }
}
