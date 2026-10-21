namespace Zyon.Client.Tests;

public class AutosaveTimerTests
{
    [Fact]
    public void FiresOncePerInterval_AndResets()
    {
        var t = new AutosaveTimer(1.0);

        Assert.False(t.Update(0.6));
        Assert.True(t.Update(0.5));
        Assert.False(t.Update(0.5));
        Assert.True(t.Update(0.6));
    }

    [Fact]
    public void IgnoresInvalidDeltas_AndResetRestarts()
    {
        var t = new AutosaveTimer(1.0);

        Assert.False(t.Update(double.NaN));
        Assert.False(t.Update(-5));
        t.Update(0.9);
        t.Reset();
        Assert.False(t.Update(0.5));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Constructor_RejectsInvalidIntervals(double interval)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AutosaveTimer(interval));
    }
}
