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

    [Fact]
    public void Parses_MenuSkippingOptions()
    {
        var o = LaunchOptions.Parse(["--world", "Teste", "--player", "Ana", "--saves-dir", "C:/tmp/s", "--autosave-seconds", "2.5"]);

        Assert.False(o.Dev);
        Assert.Equal("Teste", o.World);
        Assert.Equal("Ana", o.Player);
        Assert.Equal("C:/tmp/s", o.SavesDir);
        Assert.Equal(2.5, o.AutosaveSeconds);
        Assert.True(o.SkipMenu);
    }

    [Fact]
    public void NoArguments_ShowsMenu_WithFiveMinuteAutosave()
    {
        var o = LaunchOptions.Parse([]);

        Assert.False(o.SkipMenu);
        Assert.Equal(300.0, o.AutosaveSeconds);
    }

    [Fact]
    public void SmokeTest_SkipsMenu()
    {
        Assert.True(LaunchOptions.Parse(["--smoke-test"]).SkipMenu);
    }

    [Fact]
    public void ProfileWalk_SkipsMenu()
    {
        Assert.True(LaunchOptions.Parse(["--profile-walk"]).SkipMenu);
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

    [Theory]
    [InlineData("--dev")]
    [InlineData("--smoke-test")]
    [InlineData("--profile-walk")]
    public void ThrowawayModes_RejectNamedWorld(string mode)
    {
        var ex = Assert.Throws<ArgumentException>(() => LaunchOptions.Parse([mode, "--world", "Casa"]));
        Assert.Contains("--world não combina com modos descartáveis", ex.Message);
        Assert.Throws<ArgumentException>(() => LaunchOptions.Parse(["--world", "Casa", mode]));
    }
}
