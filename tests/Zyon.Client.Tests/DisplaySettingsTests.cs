namespace Zyon.Client.Tests;

public sealed class DisplaySettingsTests : IDisposable
{
    private readonly string dir = Path.Combine(Path.GetTempPath(), "zyon-settings-" + Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(dir, DisplaySettings.FileName);

    public void Dispose()
    {
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
    }

    [Fact]
    public void MissingFile_GivesDefaultsWithoutError()
    {
        var s = DisplaySettings.Load(FilePath, out var error);

        Assert.False(s.Fullscreen);
        Assert.Null(error);
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        new DisplaySettings(Fullscreen: true, ZoomSteps: -1).Save(FilePath);

        var s = DisplaySettings.Load(FilePath, out var error);

        Assert.True(s.Fullscreen);
        Assert.Equal(-1, s.ZoomSteps);
        Assert.Null(error);
    }

    [Fact]
    public void CorruptFile_GivesDefaultsAndReportsError()
    {
        Directory.CreateDirectory(dir);
        File.WriteAllText(FilePath, "{ nope");

        var s = DisplaySettings.Load(FilePath, out var error);

        Assert.False(s.Fullscreen);
        Assert.Contains(DisplaySettings.FileName, error);
    }
}
