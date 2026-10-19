namespace Zyon.Client.Tests;

public class FrameStatsTests
{
    private static FrameSample Frame(double deltaSeconds, double processMs = 1, double updateMs = 0.5,
        double renderMs = 0.25, int rebuilds = 0, int gc = 0) =>
        new(deltaSeconds, processMs, updateMs, renderMs, rebuilds, gc);

    [Fact]
    public void Window_IsEmptyUntilWindowElapses()
    {
        var stats = new FrameStats(windowSeconds: 1.0);

        for (int i = 0; i < 59; i++) stats.Add(Frame(1.0 / 60));

        Assert.Equal(0, stats.Window.Frames);
    }

    [Fact]
    public void Window_PublishesFpsAverageAndMaxOverTheLastSecond()
    {
        var stats = new FrameStats(windowSeconds: 1.0);

        for (int i = 0; i < 9; i++) stats.Add(Frame(0.1, processMs: 2, updateMs: 1, renderMs: 0.5));
        stats.Add(Frame(0.1, processMs: 12, updateMs: 7, renderMs: 4, rebuilds: 3, gc: 1));

        var w = stats.Window;
        Assert.Equal(10, w.Frames);
        Assert.Equal(10.0, w.Fps, 6);
        Assert.Equal(100.0, w.FrameAvgMs, 6);
        Assert.Equal(100.0, w.FrameMaxMs, 6);
        Assert.Equal(3.0, w.ProcessAvgMs, 6);
        Assert.Equal(12.0, w.ProcessMaxMs, 6);
        Assert.Equal(1.6, w.UpdateAvgMs, 6);
        Assert.Equal(7.0, w.UpdateMaxMs, 6);
        Assert.Equal(0.85, w.RenderAvgMs, 6);
        Assert.Equal(4.0, w.RenderMaxMs, 6);
        Assert.Equal(3, w.Rebuilds);
        Assert.Equal(3, w.MaxRebuildsPerFrame);
        Assert.Equal(1, w.GcCollections);
    }

    [Fact]
    public void Window_ResetsAfterPublishing()
    {
        var stats = new FrameStats(windowSeconds: 0.5);

        stats.Add(Frame(0.5, processMs: 30));
        stats.Add(Frame(0.25, processMs: 1));
        stats.Add(Frame(0.25, processMs: 3));

        Assert.Equal(2, stats.Window.Frames);
        Assert.Equal(4.0, stats.Window.Fps, 6);
        Assert.Equal(3.0, stats.Window.ProcessMaxMs, 6);
        Assert.Equal(250.0, stats.Window.FrameMaxMs, 6);
    }

    [Fact]
    public void Overall_AccumulatesEveryFrame()
    {
        var stats = new FrameStats(windowSeconds: 1.0);

        stats.Add(Frame(0.01, processMs: 1, rebuilds: 2, gc: 1));
        stats.Add(Frame(0.03, processMs: 5, rebuilds: 4));

        var o = stats.Overall;
        Assert.Equal(2, o.Frames);
        Assert.Equal(50.0, o.Fps, 6);
        Assert.Equal(20.0, o.FrameAvgMs, 6);
        Assert.Equal(30.0, o.FrameMaxMs, 6);
        Assert.Equal(3.0, o.ProcessAvgMs, 6);
        Assert.Equal(5.0, o.ProcessMaxMs, 6);
        Assert.Equal(6, o.Rebuilds);
        Assert.Equal(4, o.MaxRebuildsPerFrame);
        Assert.Equal(1, o.GcCollections);
    }

    [Fact]
    public void ZeroOrNegativeDelta_CountsTheFrameButNotTheTime()
    {
        var stats = new FrameStats(windowSeconds: 1.0);

        stats.Add(Frame(0, processMs: 2));
        stats.Add(Frame(-1, processMs: 4));

        Assert.Equal(2, stats.Overall.Frames);
        Assert.Equal(0.0, stats.Overall.Fps);
        Assert.Equal(0.0, stats.Overall.FrameMaxMs);
        Assert.Equal(3.0, stats.Overall.ProcessAvgMs, 6);
    }

    [Fact]
    public void Constructor_RejectsNonPositiveWindow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FrameStats(0));
    }
}
