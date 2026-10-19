namespace Zyon.Client.Tests;

public class TileAtlasLayoutTests
{
    [Fact]
    public void Dimensions_HoldEveryMaskAndVariationPerMaterial()
    {
        var layout = new TileAtlasLayout(materialCount: 6, variations: 3);

        Assert.Equal(16 * 3 * 16, layout.WidthPx);
        Assert.Equal(6 * 16, layout.HeightPx);
    }

    [Fact]
    public void CellOrigin_RowIsMaterial_ColumnIsMaskThenVariation()
    {
        var layout = new TileAtlasLayout(6, 3);

        Assert.Equal((0, 0), layout.CellOrigin(0, 0, 0));
        Assert.Equal(((5 * 3 + 2) * 16, 4 * 16), layout.CellOrigin(4, 5, 2));
    }

    [Fact]
    public void Uv_IsInsideTheCell_WithASmallInset()
    {
        var layout = new TileAtlasLayout(6, 3);
        var (x, y) = layout.CellOrigin(2, 9, 1);

        var uv = layout.Uv(2, 9, 1);

        Assert.True(uv.U0 > x / (float)layout.WidthPx);
        Assert.True(uv.U1 < (x + 16) / (float)layout.WidthPx);
        Assert.True(uv.V0 > y / (float)layout.HeightPx);
        Assert.True(uv.V1 < (y + 16) / (float)layout.HeightPx);
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(6, 0)]
    public void NonPositiveArguments_Throw(int materials, int variations)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TileAtlasLayout(materials, variations));
    }
}
