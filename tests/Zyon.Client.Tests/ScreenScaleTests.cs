namespace Zyon.Client.Tests;

public class ScreenScaleTests
{
    [Theory]
    [InlineData(360, 1)]
    [InlineData(768, 1)]
    [InlineData(900, 1)]
    [InlineData(1009, 2)]     // 1080p maximizada (barra de título e de tarefas)
    [InlineData(1080, 2)]
    [InlineData(1369, 2)]     // 1440p maximizada
    [InlineData(1440, 2)]
    [InlineData(1600, 3)]
    [InlineData(2090, 4)]     // 4K maximizada
    [InlineData(2160, 4)]
    [InlineData(0, 1)]
    public void For_FitsTargetHeightWithToleranceForMaximizedWindows(int windowHeight, int expected) =>
        Assert.Equal(expected, ScreenScale.For(windowHeight));

    [Theory]
    [InlineData(1920, 1080, 2, 960, 540)]
    [InlineData(1920, 1009, 2, 960, 504)]
    [InlineData(2560, 1440, 2, 1280, 720)]
    [InlineData(3840, 2160, 4, 960, 540)]
    public void BaseSize_DividesWindowByScale(int w, int h, int scale, int expectedW, int expectedH) =>
        Assert.Equal((expectedW, expectedH), ScreenScale.BaseSize(w, h, scale));

    [Theory]
    [InlineData(1920, 1009)]
    [InlineData(2560, 1440)]
    [InlineData(1366, 768)]
    [InlineData(3440, 1440)]
    public void BaseSize_KeepsEngineIntegerScaleEqualToChosenScale(int w, int h)
    {
        int scale = ScreenScale.For(h);
        var (bw, bh) = ScreenScale.BaseSize(w, h, scale);

        // O modo inteiro do motor usa floor(min(janela / base)): tem de dar exatamente a escala escolhida.
        Assert.Equal(scale, (int)Math.Floor(Math.Min(w / (double)bw, h / (double)bh)));
    }

    [Fact]
    public void VisibleTiles_At1080p_AreAbout60By34()
    {
        var (bw, bh) = ScreenScale.BaseSize(1920, 1080, ScreenScale.For(1080));

        Assert.Equal(60, bw / RenderMath.PixelsPerTile);
        Assert.Equal(33, bh / RenderMath.PixelsPerTile);
    }
}
