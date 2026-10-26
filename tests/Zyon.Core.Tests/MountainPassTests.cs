using Zyon.Core.WorldGen;
using Zyon.Core.WorldGen.Passes;
using static Zyon.Core.Tests.WorldGenTestKit;

namespace Zyon.Core.Tests;

public class MountainPassTests
{
    private static WorldGenContext RunMountain(int width = Width, int height = Height, WorldGenConfig? config = null)
    {
        var ctx = Context(width, height, Seed, config);
        WorldGenerator.RunPass(ctx, new TerrainPass());
        WorldGenerator.RunPass(ctx, new MountainPass());
        return ctx;
    }

    private static WorldGenContext TerrainOnly(int width = Width, int height = Height)
    {
        var ctx = Context(width, height, Seed);
        WorldGenerator.RunPass(ctx, new TerrainPass());
        return ctx;
    }

    [Fact]
    public void Mountain_PeakRightOfCenter_FacingLeft_TallerThanTheFoot()
    {
        var ctx = RunMountain();
        var m = ctx.Mountain!;

        Assert.True(m.FacesLeft);
        Assert.Equal(Width / 2 + 40, m.PeakX);
        Assert.Equal(m.PeakX - 30, m.FootX);
        Assert.True(ctx.Surface[m.FootX] - ctx.Surface[m.PeakX] >= 20,
            $"pico {ctx.Surface[m.PeakX]} pouco acima do pé {ctx.Surface[m.FootX]}");
    }

    [Fact]
    public void Mountain_KeepsTerrainOutsideItsRange_AndFillsBelowTheSurface()
    {
        var terrainOnly = Context();
        WorldGenerator.RunPass(terrainOnly, new TerrainPass());
        var ctx = RunMountain();
        var m = ctx.Mountain!;

        for (int x = 0; x < Width; x++)
        {
            if (x < m.FootX || x > m.PeakX + 80)
                Assert.Equal(terrainOnly.Surface[x], ctx.Surface[x]);
            Assert.Equal(0, ctx.World.GetBlock(x, ctx.Surface[x] - 1));
            for (int y = ctx.Surface[x]; y < Height; y++)
                Assert.NotEqual(0, ctx.World.GetBlock(x, y));
        }
        Assert.Equal(terrainOnly.Surface[m.FootX], ctx.Surface[m.FootX]);   // o pé fica no nível do terreno
    }

    [Fact]
    public void Mountain_SteepSideRisesFasterThanTheGentleSide()
    {
        var ctx = RunMountain();
        var m = ctx.Mountain!;
        int steepRise = ctx.Surface[m.FootX] - ctx.Surface[m.FootX + 10];
        int gentleRise = ctx.Surface[m.PeakX + 80] - ctx.Surface[m.PeakX + 70];

        Assert.True(steepRise > gentleRise, $"paredão {steepRise} vs suave {gentleRise}");
    }

    [Fact]
    public void Mountain_NegativeOffset_FacesRight()
    {
        var cfg = WorldGenConfigLoader.Load(File.ReadAllText(Path.Combine(TestData.DataDir, "worldgen.json"))
            .Replace("\"mountainOffset\": 40", "\"mountainOffset\": -40"));
        var ctx = RunMountain(config: cfg);
        var m = ctx.Mountain!;

        Assert.False(m.FacesLeft);
        Assert.Equal(Width / 2 - 40, m.PeakX);
        Assert.Equal(m.PeakX + 30, m.FootX);
        Assert.Equal(TerrainOnly().Surface[m.FootX], ctx.Surface[m.FootX]);   // o pé fica no nível do terreno
    }

    [Theory]
    [InlineData(64, 64)]
    [InlineData(128, 128)]
    public void Mountain_SmallWorld_ScalesDownAndStaysInBounds(int width, int height)
    {
        var ctx = RunMountain(width, height);
        var m = ctx.Mountain!;
        int minRow = (int)(ctx.Config.SurfaceMin * height / 2);

        Assert.InRange(m.PeakX, 0, width - 1);
        Assert.InRange(m.FootX, 0, width - 1);
        Assert.True(ctx.Surface[m.PeakX] < ctx.Surface[m.FootX],
            $"pico {ctx.Surface[m.PeakX]} não fica acima do pé {ctx.Surface[m.FootX]}");
        Assert.Equal(TerrainOnly(width, height).Surface[m.FootX], ctx.Surface[m.FootX]);   // o pé fica no nível do terreno
        for (int x = 0; x < width; x++)
            Assert.True(ctx.Surface[x] >= minRow, $"coluna {x}: superfície {ctx.Surface[x]} acima de {minRow}");
    }

    [Theory]
    [InlineData(40, 1)]
    [InlineData(-40, -1)]
    public void Mountain_GentleFoot_IsTheEndOfTheGentleSlope(int offset, int dir)
    {
        var cfg = WorldGenConfigLoader.Load(File.ReadAllText(Path.Combine(TestData.DataDir, "worldgen.json"))
            .Replace("\"mountainOffset\": 40", $"\"mountainOffset\": {offset}"));
        const int gentle = 80;   // mundo de teste (512): k = 1, largura suave sem encolher
        var terrain = TerrainOnly();
        var ctx = RunMountain(config: cfg);
        var m = ctx.Mountain!;

        Assert.Equal(gentle, cfg.MountainGentleWidth);
        Assert.Equal(m.PeakX + dir * gentle, m.GentleFootX);
        for (int i = 0; i <= gentle / 2; i++)
        {
            int x = m.PeakX + dir * i;
            Assert.True(ctx.Surface[x] < terrain.Surface[x], $"coluna {x} (i={i}) não foi elevada");
        }
        for (int i = 0; i <= gentle; i++)
            Assert.True(ctx.Surface[m.PeakX + dir * i] <= terrain.Surface[m.PeakX + dir * i]);
        for (int i = 1; i <= 20; i++)
        {
            int x = m.GentleFootX + dir * i;
            Assert.Equal(terrain.Surface[x], ctx.Surface[x]);   // além do pé suave: terreno intacto
        }
    }

    [Fact]
    public void Mountain_IsDeterministic()
    {
        Assert.Equal(RunMountain().Surface, RunMountain().Surface);
    }
}
