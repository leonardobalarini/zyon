namespace Zyon.Client.Tests;

public class DirtyChunkTrackerTests
{
    private static List<(int, int)> Drain(DirtyChunkTracker t)
    {
        var all = new List<(int, int)>();
        while (t.TryTake(out var c)) all.Add(c);
        return all;
    }

    [Fact]
    public void InteriorTile_MarksOnlyItsChunk()
    {
        var t = new DirtyChunkTracker(chunksX: 4, chunksY: 4);

        t.MarkTile(40, 40);

        Assert.Equal(new[] { (1, 1) }, Drain(t));
    }

    [Fact]
    public void EdgeAndCornerTiles_MarkNeighbours()
    {
        var t = new DirtyChunkTracker(4, 4);

        t.MarkTile(32, 50);   // borda esquerda do chunk (1,1)
        Assert.Equal(new[] { (1, 1), (0, 1) }, Drain(t));

        t.MarkTile(63, 63);   // canto inferior direito do chunk (1,1)
        Assert.Equal(new[] { (1, 1), (2, 1), (1, 2) }, Drain(t));
    }

    [Fact]
    public void WorldBorder_SkipsMissingNeighbours()
    {
        var t = new DirtyChunkTracker(2, 2);

        t.MarkTile(0, 0);

        Assert.Equal(new[] { (0, 0) }, Drain(t));
    }

    [Fact]
    public void RepeatedMarks_AreDeduplicatedUntilTaken()
    {
        var t = new DirtyChunkTracker(4, 4);

        t.MarkTile(40, 40);
        t.MarkTile(41, 41);
        Assert.Equal(1, t.Count);
        Drain(t);
        t.MarkTile(40, 40);
        Assert.Equal(1, t.Count);
    }
}
