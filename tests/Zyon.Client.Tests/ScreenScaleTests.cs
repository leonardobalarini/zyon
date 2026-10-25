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

    [Theory]
    [InlineData(768, 1, 1f)]
    [InlineData(1009, 1, 0.5f)]
    [InlineData(1080, 1, 0.5f)]
    [InlineData(1369, 1, 0.5f)]
    [InlineData(1600, 1, 1f / 3)]
    [InlineData(2160, 2, 0.5f)]
    public void World_IsIntegerAndZoomsOutFromInterface(int windowHeight, int expectedWorld, float expectedZoom)
    {
        Assert.Equal(expectedWorld, ScreenScale.ForWorld(windowHeight));
        Assert.Equal(expectedZoom, ScreenScale.WorldZoom(windowHeight), 5);
    }

    [Theory]
    [InlineData(1080, 1, 2)]      // um passo mais perto: 60×34 tiles
    [InlineData(1080, 2, 3)]      // 40×22, o limite (interface + 1)
    [InlineData(1080, 9, 3)]
    [InlineData(1080, -5, 1)]
    [InlineData(2160, -1, 1)]
    [InlineData(2160, 3, 5)]
    public void ZoomSteps_MoveWorldScaleWithinLimits(int windowHeight, int steps, int expectedWorld) =>
        Assert.Equal(expectedWorld, ScreenScale.ForWorld(windowHeight, steps));

    [Theory]
    [InlineData(1080, 9, 2)]
    [InlineData(1080, -3, 0)]
    [InlineData(2160, -3, -1)]
    public void ClampZoomSteps_DropsStepsThatHaveNoEffect(int windowHeight, int steps, int expected) =>
        Assert.Equal(expected, ScreenScale.ClampZoomSteps(windowHeight, steps));

    [Fact]
    public void VisibleTiles_At1080p_AreLikeTerraria()
    {
        var (bw, bh) = ScreenScale.BaseSize(1920, 1080, ScreenScale.For(1080));
        float zoom = ScreenScale.WorldZoom(1080);

        Assert.Equal(120, (int)(bw / zoom) / RenderMath.PixelsPerTile);
        Assert.Equal(67, (int)(bh / zoom) / RenderMath.PixelsPerTile);
    }
}
