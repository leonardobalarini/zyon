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
        Assert.Equal(0f, o.CameraSmoothing);
    }

    [Fact]
    public void Parses_CameraSmoothing()
    {
        Assert.Equal(12.5f, LaunchOptions.Parse(["--camera-smoothing", "12.5"]).CameraSmoothing);
        Assert.Equal(0f, LaunchOptions.Parse(["--camera-smoothing", "0"]).CameraSmoothing);
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

    [Fact]
    public void Parses_ProfileWalk_AndRejectsItWithSmokeTest()
    {
        var o = LaunchOptions.Parse(["--profile-walk", "--frames", "3600"]);

        Assert.True(o.ProfileWalk);
        Assert.False(o.SmokeTest);
        Assert.Equal(3600, o.SmokeFrames);
        Assert.False(LaunchOptions.Parse([]).ProfileWalk);
        Assert.Throws<ArgumentException>(() => LaunchOptions.Parse(["--profile-walk", "--smoke-test"]));
    }

    [Theory]
    [InlineData("--size", "huge")]
    [InlineData("--seed", "abc")]
    [InlineData("--frames", "0")]
    [InlineData("--bogus", "1")]
    [InlineData("--camera-smoothing", "-1")]
    [InlineData("--camera-smoothing", "NaN")]
    [InlineData("--camera-smoothing", "fast")]
    public void InvalidArguments_Throw(string flag, string value)
    {
        Assert.Throws<ArgumentException>(() => LaunchOptions.Parse([flag, value]));
    }
}
