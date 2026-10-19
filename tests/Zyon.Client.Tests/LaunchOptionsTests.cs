namespace Zyon.Client.Tests;

public class LaunchOptionsTests
{
    [Fact]
    public void Defaults_AreDevWorldNoSmokeTest()
    {
        var o = LaunchOptions.Parse([]);

        Assert.Equal((1050, 300), (o.Width, o.Height));
        Assert.Null(o.Seed);
        Assert.False(o.SmokeTest);
    }

    [Fact]
    public void Parses_SizeSeedAndSmokeTest()
    {
        var o = LaunchOptions.Parse(["--size", "small", "--seed", "42", "--smoke-test", "--frames", "120"]);

        Assert.Equal((4200, 1200), (o.Width, o.Height));
        Assert.Equal(42, o.Seed);
        Assert.True(o.SmokeTest);
        Assert.Equal(120, o.SmokeFrames);
    }

    [Theory]
    [InlineData("--size", "huge")]
    [InlineData("--seed", "abc")]
    [InlineData("--frames", "0")]
    [InlineData("--bogus", "1")]
    public void InvalidArguments_Throw(string flag, string value)
    {
        Assert.Throws<ArgumentException>(() => LaunchOptions.Parse([flag, value]));
    }
}
