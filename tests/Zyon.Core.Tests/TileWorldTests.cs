using Zyon.Core.Tiles;

namespace Zyon.Core.Tests;

public class TileWorldTests
{
    [Fact]
    public void NewWorld_IsAllAir()
    {
        var w = new TileWorld(64, 64);

        Assert.Equal(0, w.GetBlock(10, 10));
        Assert.Equal(0, w.GetWall(10, 10));
    }

    [Fact]
    public void SetAndGet_RoundTrip_AcrossChunkBoundary()
    {
        var w = new TileWorld(100, 50);

        w.SetBlock(31, 5, 1);
        w.SetBlock(32, 5, 2);
        w.SetWall(32, 33, 3);

        Assert.Equal(1, w.GetBlock(31, 5));
        Assert.Equal(2, w.GetBlock(32, 5));
        Assert.Equal(3, w.GetWall(32, 33));
        Assert.Equal(0, w.GetWall(31, 5));
    }

    [Fact]
    public void ChunkCount_RoundsUpPartialChunks()
    {
        var w = new TileWorld(100, 40);

        Assert.Equal(4, w.ChunksX);
        Assert.Equal(2, w.ChunksY);
        Assert.Equal(8, w.Chunks.Count());
    }

    [Fact]
    public void SetBlock_MarksOwningChunkDirty()
    {
        var w = new TileWorld(64, 64);

        w.SetBlock(40, 3, 1);

        Assert.True(w.GetChunk(1, 0).Dirty);
        Assert.False(w.GetChunk(0, 0).Dirty);
    }

    [Fact]
    public void OutOfBounds_GetReturnsAir_SetThrows()
    {
        var w = new TileWorld(64, 64);

        Assert.False(w.InBounds(-1, 0));
        Assert.False(w.InBounds(64, 0));
        Assert.Equal(0, w.GetBlock(-1, 0));
        Assert.Equal(0, w.GetBlock(0, 64));
        Assert.Throws<ArgumentOutOfRangeException>(() => w.SetBlock(64, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => w.SetWall(0, -1, 1));
    }

    [Fact]
    public void InvalidSize_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TileWorld(0, 10));
    }

    [Fact]
    public void FlatHelper_FillsFromGroundDown()
    {
        var r = TestData.Registry();
        var w = TestWorlds.Flat(r, 64, 64, 40);

        Assert.Equal(0, w.GetBlock(5, 39));
        Assert.Equal(r.BlockId("dirt"), w.GetBlock(5, 40));
        Assert.Equal(r.BlockId("dirt"), w.GetBlock(63, 63));
    }

    [Fact]
    public void GetChunk_OutOfRange_Throws()
    {
        var w = new TileWorld(100, 40);

        Assert.Throws<ArgumentOutOfRangeException>(() => w.GetChunk(w.ChunksX, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => w.GetChunk(0, w.ChunksY));
        Assert.Throws<ArgumentOutOfRangeException>(() => w.GetChunk(-1, 0));
    }

    [Fact]
    public void LastTileOfPartialChunk_RoundTrips()
    {
        var w = new TileWorld(100, 40);

        w.SetBlock(99, 39, 5);
        w.SetWall(99, 39, 6);

        Assert.Equal(5, w.GetBlock(99, 39));
        Assert.Equal(6, w.GetWall(99, 39));
        Assert.True(w.GetChunk(3, 1).Dirty);
    }
}
