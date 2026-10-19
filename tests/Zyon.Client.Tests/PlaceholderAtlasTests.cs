namespace Zyon.Client.Tests;

public class PlaceholderAtlasTests
{
    private static readonly Rgb Brown = new(100, 80, 60);

    private static Rgb Pixel(byte[] rgba, TileAtlasLayout layout, int x, int y)
    {
        int i = (y * layout.WidthPx + x) * 4;
        Assert.Equal(255, rgba[i + 3]);
        return new Rgb(rgba[i], rgba[i + 1], rgba[i + 2]);
    }

    [Fact]
    public void Buffer_HasRgbaForTheWholeAtlas()
    {
        var layout = new TileAtlasLayout(2, 3);

        var rgba = PlaceholderAtlas.BuildRgba(layout, [null, Brown]);

        Assert.Equal(layout.WidthPx * layout.HeightPx * 4, rgba.Length);
    }

    [Fact]
    public void CellCenter_IsCloseToTheBaseColor()
    {
        var layout = new TileAtlasLayout(2, 3);
        var rgba = PlaceholderAtlas.BuildRgba(layout, [null, Brown]);
        var (x, y) = layout.CellOrigin(1, AutoTile.MaskCount - 1, 0);

        var p = Pixel(rgba, layout, x + 8, y + 8);

        Assert.InRange(p.R, Brown.R - 12, Brown.R + 12);
        Assert.InRange(p.G, Brown.G - 12, Brown.G + 12);
    }

    [Fact]
    public void ExposedSide_IsDarker_ConnectedSideIsNot()
    {
        var layout = new TileAtlasLayout(2, 3);
        var rgba = PlaceholderAtlas.BuildRgba(layout, [null, Brown]);
        var (x, y) = layout.CellOrigin(1, AutoTile.South, 0);   // só o sul conectado

        var top = Pixel(rgba, layout, x + 8, y);         // norte exposto
        var bottom = Pixel(rgba, layout, x + 8, y + 15); // sul conectado

        Assert.True(top.R < Brown.R - 20, $"topo {top}");
        Assert.True(bottom.R > Brown.R - 15, $"base {bottom}");
    }

    [Fact]
    public void MaterialWithoutColor_IsTransparent()
    {
        var layout = new TileAtlasLayout(2, 3);

        var rgba = PlaceholderAtlas.BuildRgba(layout, [null, Brown]);

        Assert.Equal(0, rgba[3]);
    }
}
