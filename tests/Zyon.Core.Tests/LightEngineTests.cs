using Zyon.Core.Data;
using Zyon.Core.Lighting;
using Zyon.Core.Sim;
using Zyon.Core.Tiles;

namespace Zyon.Core.Tests;

public class LightEngineTests
{
    private static readonly Registry R = TestData.Registry();
    private static readonly LightingConfig Config = TestData.LightingConfig();
    private static ushort Dirt => R.BlockId("dirt");
    private static ushort Torch => R.BlockId("torch");
    private static ushort Wall => R.WallId("dirt_wall");

    /// <summary>64×64: céu nas linhas 0..19, terra sólida com parede de fundo da linha 20 para baixo.</summary>
    private static TileWorld Ground()
    {
        var w = new TileWorld(64, 64);
        for (int y = 20; y < 64; y++)
        for (int x = 0; x < 64; x++)
        {
            w.SetBlock(x, y, Dirt);
            w.SetWall(x, y, Wall);
        }
        return w;
    }

    private static LightMap Compute(TileWorld world, float sky = 1f, params LightEmitter[] extra)
    {
        using var view = new Simulation(R, world);
        var map = new LightMap();
        new LightEngine(Config).Compute(view, new TileRect(0, 0, world.Width - 1, world.Height - 1), sky, extra, map);
        return map;
    }

    [Fact]
    public void OpenSky_IsFullyLit_AndFollowsSkyFactor()
    {
        Assert.Equal(1f, Compute(Ground()).Get(10, 5), 5);
        Assert.Equal(0.15f, Compute(Ground(), sky: 0.15f).Get(10, 5), 5);
    }

    [Fact]
    public void ClosedCave_IsDark_BackgroundWallsBlockTheSky()
    {
        var w = Ground();
        for (int y = 40; y < 45; y++)
        for (int x = 20; x < 30; x++)
            w.SetBlock(x, y, 0);   // caverna com paredes de fundo

        Assert.Equal(0f, Compute(w).Get(25, 42), 5);
    }

    [Fact]
    public void ShaftToTheSurface_LetsTheSkyIn()
    {
        var w = Ground();
        for (int y = 20; y < 40; y++)
        {
            w.SetBlock(32, y, 0);
            w.SetWall(32, y, 0);   // poço sem parede: o céu desce
        }

        Assert.Equal(1f, Compute(w).Get(32, 39), 5);
    }

    [Fact]
    public void Torch_FallsOffByAirAttenuationPerTile()
    {
        var w = new TileWorld(64, 64);   // tudo ar, sem céu (paredes em tudo)
        for (int y = 0; y < 64; y++)
        for (int x = 0; x < 64; x++)
            w.SetWall(x, y, Wall);
        w.SetBlock(30, 30, Torch);

        var map = Compute(w);

        Assert.Equal(1f, map.Get(30, 30), 5);
        Assert.Equal(1f - 0.06f, map.Get(31, 30), 4);
        Assert.Equal(1f - 0.06f * 5, map.Get(30, 35), 4);
        Assert.Equal(1f - 0.06f * 4, map.Get(32, 32), 4);   // distância Manhattan 4
        Assert.Equal(0f, map.Get(30, 30 + 17), 4);           // além do alcance
    }

    [Fact]
    public void SolidBlock_CutsTheLightSharply()
    {
        var w = new TileWorld(64, 64);
        for (int y = 0; y < 64; y++)
        for (int x = 0; x < 64; x++)
            w.SetWall(x, y, Wall);
        w.SetBlock(30, 30, Torch);
        for (int y = 0; y < 64; y++)
            w.SetBlock(32, y, Dirt);   // muro sólido em x = 32

        var map = Compute(w);

        Assert.Equal(1f - 0.06f - 0.25f, map.Get(32, 30), 4);
        Assert.True(map.Get(34, 30) < map.Get(28, 30) - 0.2f, "o muro deveria fazer sombra");
    }

    [Fact]
    public void GlowingOre_EmitsItsLevel()
    {
        var w = Ground();
        w.SetBlock(40, 50, R.BlockId("ore_b"));

        Assert.Equal(0.35f, Compute(w).Get(40, 50), 5);
    }

    [Fact]
    public void ExtraEmitter_LikeThePlayerHalo_IsApplied()
    {
        var w = Ground();
        for (int y = 40; y < 45; y++)
        for (int x = 20; x < 30; x++)
            w.SetBlock(x, y, 0);

        var map = Compute(w, 1f, new LightEmitter(25, 42, 0.3f));

        Assert.Equal(0.3f, map.Get(25, 42), 5);
        Assert.Equal(0.3f - 0.06f, map.Get(26, 42), 4);
    }

    [Fact]
    public void RegionWithMargin_MatchesWholeWorld_InTheVisiblePart()
    {
        var w = Ground();
        w.SetBlock(5, 30, 0);
        for (int x = 3; x < 60; x += 7) { w.SetBlock(x, 35, 0); w.SetBlock(x, 34, Torch); }
        using var view = new Simulation(R, w);
        var engine = new LightEngine(Config);
        var whole = new LightMap();
        var part = new LightMap();
        var visible = new TileRect(25, 25, 40, 40);

        engine.Compute(view, new TileRect(0, 0, 63, 63), 1f, [], whole);
        engine.Compute(view, visible.Expand(Config.RegionMargin).ClampTo(view.Width, view.Height), 1f, [], part);

        for (int y = visible.MinY; y <= visible.MaxY; y++)
        for (int x = visible.MinX; x <= visible.MaxX; x++)
            Assert.Equal(whole.Get(x, y), part.Get(x, y), 4);
    }

    [Fact]
    public void LightMap_OutsideRegion_IsZero()
    {
        var map = Compute(Ground());

        Assert.Equal(0f, map.Get(-1, 5));
        Assert.Equal(0f, map.Get(64, 5));
    }

    [Fact]
    public void Staircase_AgainstSweepOrder_NeedsMoreThanOneRound()
    {
        var w = new TileWorld(64, 64);
        for (int y = 0; y < 64; y++)
        for (int x = 0; x < 64; x++)
        {
            w.SetBlock(x, y, Dirt);
            w.SetWall(x, y, Wall);
        }
        int cx = 40, cy = 10;
        w.SetBlock(cx, cy, Torch);
        // Caminho de ar: desce 2, esquerda 2, repetido (12 passos). Esquerda só propaga na varredura de linhas da rodada seguinte.
        for (int i = 0; i < 12; i++)
        {
            if ((i / 2) % 2 == 0) cy++; else cx--;
            w.SetBlock(cx, cy, 0);
        }

        using var view = new Simulation(R, w);
        var engine = new LightEngine(Config);
        var map = new LightMap();
        engine.Compute(view, new TileRect(0, 0, 63, 63), 1f, [], map);

        Assert.Equal(1f - 12 * 0.06f, map.Get(cx, cy), 4);
        Assert.True(engine.LastRounds > 1, $"rodadas: {engine.LastRounds}");
    }

    [Fact]
    public void SkyBelowACeiling_IsNotSeeded_WhenRegionStartsBelowIt()
    {
        var w = new TileWorld(64, 64);
        for (int y = 0; y < 64; y++)
        for (int x = 0; x < 64; x++)
            if (y < 25 || x != 32) w.SetBlock(x, y, Dirt);   // teto sólido; coluna de ar em x = 32 sem paredes

        using var view = new Simulation(R, w);
        var engine = new LightEngine(Config);
        var map = new LightMap();
        engine.Compute(view, new TileRect(0, 25, 63, 40), 1f, [], map);

        Assert.Equal(0f, map.Get(32, 30), 5);
    }

    [Fact]
    public void ReusedMap_DoesNotKeepOldLight()
    {
        var lit = Ground();
        lit.SetBlock(30, 30, Torch);
        using var litView = new Simulation(R, lit);
        var engine = new LightEngine(Config);
        var map = new LightMap();
        engine.Compute(litView, new TileRect(0, 20, 63, 63), 1f, [], map);
        Assert.Equal(1f, map.Get(30, 30), 5);

        using var darkView = new Simulation(R, Ground());
        engine.Compute(darkView, new TileRect(25, 25, 35, 35), 1f, [], map);

        for (int y = 25; y <= 35; y++)
        for (int x = 25; x <= 35; x++)
            Assert.Equal(0f, map.Get(x, y));
    }

    [Fact]
    public void RegionPartlyOutsideTheWorld_IsClamped()
    {
        var w = Ground();
        w.SetBlock(5, 30, 0);
        w.SetBlock(8, 29, Torch);
        using var view = new Simulation(R, w);
        var engine = new LightEngine(Config);
        var whole = new LightMap();
        var part = new LightMap();

        engine.Compute(view, new TileRect(0, 0, 63, 63), 1f, [], whole);
        engine.Compute(view, new TileRect(-10, -10, 20, 20), 1f, [], part);

        Assert.Equal(new TileRect(0, 0, 20, 20), part.Region);
        for (int y = 0; y <= 20; y++)
        for (int x = 0; x <= 20; x++)
            Assert.Equal(whole.Get(x, y), part.Get(x, y), 4);
    }
}
