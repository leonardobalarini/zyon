using System.Numerics;

namespace Zyon.Client.Tests;

public class RenderMathTests
{
    [Fact]
    public void ToPixels_ScalesBySixteen()
    {
        Assert.Equal(new Vector2(160f, 40f), RenderMath.ToPixels(new Vector2(10f, 2.5f)));
    }

    [Theory]
    [InlineData(0f, 0f, 0, 0)]
    [InlineData(15.9f, 31.9f, 0, 1)]
    [InlineData(16f, 32f, 1, 2)]
    [InlineData(-0.1f, -16f, -1, -1)]
    public void TileAtPixel_FloorsToTiles(float px, float py, int tx, int ty)
    {
        Assert.Equal((tx, ty), RenderMath.TileAtPixel(new Vector2(px, py)));
    }

    [Fact]
    public void Interpolate_BlendsPreviousAndCurrent()
    {
        var prev = new Vector2(0f, 10f);
        var cur = new Vector2(4f, 6f);

        Assert.Equal(prev, RenderMath.Interpolate(prev, cur, 0f));
        Assert.Equal(new Vector2(1f, 9f), RenderMath.Interpolate(prev, cur, 0.25f));
        Assert.Equal(cur, RenderMath.Interpolate(prev, cur, 1f));
    }
}
