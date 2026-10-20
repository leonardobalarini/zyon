namespace Zyon.Client.Tests;

public class ChunkVisibilityTests
{
    // Um chunk tem 32 tiles × 16 px = 512 px.
    [Fact]
    public void Range_CoversRectPlusMargin()
    {
        var r = ChunkVisibility.Range(600f, 100f, 640f, 360f, margin: 1, chunksX: 100, chunksY: 100);

        Assert.Equal(new ChunkRange(0, 0, 3, 1), r);
    }

    [Fact]
    public void Range_IsClampedToWorld()
    {
        var r = ChunkVisibility.Range(-1000f, -1000f, 640f, 360f, margin: 2, chunksX: 4, chunksY: 3);

        Assert.Equal(new ChunkRange(0, 0, 1, 0), r);
    }

    [Fact]
    public void Contains_IsInclusive()
    {
        var r = new ChunkRange(1, 2, 3, 4);

        Assert.True(r.Contains(1, 2));
        Assert.True(r.Contains(3, 4));
        Assert.False(r.Contains(4, 4));
        Assert.False(r.Contains(1, 1));
    }
}
