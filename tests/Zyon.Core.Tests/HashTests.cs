using Zyon.Core.Tiles;
using Zyon.Core.WorldGen;

namespace Zyon.Core.Tests;

public class HashTests
{
    [Theory]
    [InlineData(0, "terrain", 135734238)]
    [InlineData(42, "caves.cheese", 861942530)]
    [InlineData(-1, "", -485093455)]
    public void StableHash_MatchesReferenceFnv1a(int seed, string name, int expected)
    {
        Assert.Equal(expected, StableHash.Seed(seed, name));
    }

    [Fact]
    public void StableHash_DependsOnSeedAndName()
    {
        Assert.NotEqual(StableHash.Seed(1, "terrain"), StableHash.Seed(2, "terrain"));
        Assert.NotEqual(StableHash.Seed(1, "terrain"), StableHash.Seed(1, "caves"));
    }

    [Fact]
    public void TileWorldHash_EqualWorlds_EqualHash()
    {
        var a = new TileWorld(100, 40);
        var b = new TileWorld(100, 40);
        a.SetBlock(5, 5, 3);
        b.SetBlock(5, 5, 3);

        Assert.Equal(TileWorldHash.Compute(a), TileWorldHash.Compute(b));
    }

    [Fact]
    public void TileWorldHash_ChangesWithBlocksWallsAndSize()
    {
        var baseWorld = new TileWorld(100, 40);
        ulong h = TileWorldHash.Compute(baseWorld);

        var block = new TileWorld(100, 40);
        block.SetBlock(99, 39, 1);
        var wall = new TileWorld(100, 40);
        wall.SetWall(0, 0, 1);
        var size = new TileWorld(100, 41);

        Assert.NotEqual(h, TileWorldHash.Compute(block));
        Assert.NotEqual(h, TileWorldHash.Compute(wall));
        Assert.NotEqual(h, TileWorldHash.Compute(size));
    }
}
