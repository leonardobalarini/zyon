using Zyon.Core.Biomes;
using Zyon.Core.Sim;
using Zyon.Core.Tiles;

namespace Zyon.Core.Tests;

public class BiomeDetectorTests
{
    private static readonly Data.Registry R = TestData.Registry();
    private static readonly BiomeSet Set = BiomeSet.Resolve(TestData.WorldGenConfig().Biomes!, R);

    /// <summary>Preenche count tiles com o bloco, linha a linha a partir de (x0, y0), numa largura de 20.</summary>
    private static void Fill(TileWorld world, string block, int count, int x0 = 40, int y0 = 40)
    {
        ushort id = R.BlockId(block);
        for (int i = 0; i < count; i++) world.SetBlock(x0 + i % 20, y0 + i / 20, id);
    }

    private static string Detect(TileWorld world, int x = 50, int y = 50)
    {
        using var sim = new Simulation(R, world);
        return new BiomeDetector(Set, R).Detect(sim, x, y).Id;
    }

    [Theory]
    [InlineData("snow", "snow")]
    [InlineData("ice", "snow")]
    [InlineData("sand", "desert")]
    [InlineData("sandstone", "desert")]
    [InlineData("cactus", "desert")]
    public void EnoughMarkers_DetectTheirBiome(string block, string biome)
    {
        var world = new TileWorld(128, 128);
        Fill(world, block, Set.DetectThreshold);

        Assert.Equal(biome, Detect(world));
    }

    [Fact]
    public void BelowTheThreshold_IsTheDefault()
    {
        var world = new TileWorld(128, 128);
        Fill(world, "snow", Set.DetectThreshold - 1);
        Fill(world, "dirt", 400, y0: 60);

        Assert.Equal("forest", Detect(world));
    }

    [Fact]
    public void MostMarkersWin()
    {
        var world = new TileWorld(128, 128);
        Fill(world, "sand", 250);
        Fill(world, "snow", 300, y0: 55);

        Assert.Equal("snow", Detect(world));
    }

    [Fact]
    public void Tie_FirstBiomeInTheFileWins()
    {
        var world = new TileWorld(128, 128);
        Fill(world, "snow", Set.DetectThreshold);
        Fill(world, "sand", Set.DetectThreshold, y0: 60);

        Assert.Equal("snow", Detect(world));
    }

    [Fact]
    public void NearTheBottomRightEdge_DoesNotThrow()
    {
        var world = new TileWorld(64, 64);
        Fill(world, "sand", 300, x0: 44, y0: 44);

        Assert.Equal("desert", Detect(world, 63, 63));
    }

    [Fact]
    public void MarkersOutsideTheArea_DoNotCount()
    {
        var world = new TileWorld(256, 128);
        Fill(world, "snow", 400, x0: 150);

        Assert.Equal("forest", Detect(world, 50, 50));
    }

    [Fact]
    public void NearTheWorldEdge_DoesNotThrow()
    {
        var world = new TileWorld(64, 64);
        Fill(world, "sand", 300, x0: 0, y0: 0);

        Assert.Equal("desert", Detect(world, 0, 0));
    }
}
