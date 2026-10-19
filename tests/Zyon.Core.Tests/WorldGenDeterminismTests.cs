using Zyon.Core.Tiles;
using Zyon.Core.WorldGen;

namespace Zyon.Core.Tests;

public class WorldGenDeterminismTests
{
    private static WorldGenerator Generator() => new(TestData.Registry(), TestData.WorldGenConfig());

    [Fact]
    public void DefaultPasses_AreInSpecOrder()
    {
        Assert.Equal(
            new[] { "terrain", "layers", "caves", "ores", "walls", "spawn" },
            Generator().Passes.Select(p => p.Name));
    }

    [Fact]
    public void SameSeed_ProducesIdenticalWorld()
    {
        var a = Generator().Generate(512, 256, 1234);
        var b = Generator().Generate(512, 256, 1234);

        Assert.Equal(TileWorldHash.Compute(a.Tiles), TileWorldHash.Compute(b.Tiles));
        Assert.Equal(a.SpawnFeet, b.SpawnFeet);
    }

    [Fact]
    public void DifferentSeeds_ProduceDifferentWorlds()
    {
        var a = Generator().Generate(512, 256, 1);
        var b = Generator().Generate(512, 256, 2);

        Assert.NotEqual(TileWorldHash.Compute(a.Tiles), TileWorldHash.Compute(b.Tiles));
    }
}
