using Zyon.Client;
using Zyon.Core.Data;

namespace Zyon.ArtGen.Tests;

public class TileGeneratorTests
{
    private static readonly Registry R = RegistryLoader.LoadFromDirectory(Path.Combine(RepoPaths.Root, "data"));
    private static readonly ArtSource Source = ArtSource.LoadFromFile(Path.Combine(RepoPaths.Root, "art", "art.json"), R);
    private const int T = 16;

    private static (int X, int Y) Cell(int row, int mask, int variation) =>
        ((mask * Source.Variations + variation) * T, row * T);

    [Fact]
    public void Blocks_HaveOneRowPerBlock_AndAllCells()
    {
        var img = TileGenerator.BuildBlocks(Source, R, out var rows);

        Assert.Equal(AutoTile.MaskCount * Source.Variations * T, img.Width);
        Assert.Equal((R.Blocks.Count - 1) * T, img.Height);
        Assert.Equal(0, rows["dirt"]);
        Assert.Equal(R.Blocks.Count - 2, rows["torch"]);
    }

    [Fact]
    public void IsolatedBlock_HasOutline_AndCutCorners()
    {
        var img = TileGenerator.BuildBlocks(Source, R, out var rows);
        var (x, y) = Cell(rows["stone"], mask: 0, variation: 0);
        var outline = Rgba.Parse(Source.Blocks["stone"].Palette![0]).Scale(TileGenerator.OutlineFactor);

        Assert.Equal(0, img.Get(x, y).A);                 // canto arredondado
        Assert.Equal(0, img.Get(x + 15, y + 15).A);
        Assert.Equal(outline, img.Get(x + 8, y));         // contorno no topo
        Assert.Equal(outline, img.Get(x, y + 8));         // contorno à esquerda
    }

    [Fact]
    public void FullyConnectedBlock_HasNoOutline_AndIsOpaque()
    {
        var img = TileGenerator.BuildBlocks(Source, R, out var rows);
        var (x, y) = Cell(rows["stone"], mask: 15, variation: 1);
        var outline = Rgba.Parse(Source.Blocks["stone"].Palette![0]).Scale(TileGenerator.OutlineFactor);

        for (int py = 0; py < T; py++)
        for (int px = 0; px < T; px++)
        {
            Assert.Equal(255, img.Get(x + px, y + py).A);
            Assert.NotEqual(outline, img.Get(x + px, y + py));
        }
    }

    [Fact]
    public void DirtWithExposedTop_HasAshCap()
    {
        var img = TileGenerator.BuildBlocks(Source, R, out var rows);
        var (x, y) = Cell(rows["dirt"], mask: AutoTile.East | AutoTile.South | AutoTile.West, variation: 0);
        var cap = Source.Blocks["dirt"].Cap!.Select(Rgba.Parse).ToArray();

        Assert.Contains(img.Get(x + 8, y + 1), cap.Concat(cap.Select(c => c.Scale(TileGenerator.HighlightFactor))));
    }

    [Fact]
    public void Torch_IsTheSameInEveryCell_AndTransparentAround()
    {
        var img = TileGenerator.BuildBlocks(Source, R, out var rows);
        var (x0, y0) = Cell(rows["torch"], 0, 0);
        var (x1, y1) = Cell(rows["torch"], 15, 2);

        Assert.Equal(0, img.Get(x0, y0).A);
        Assert.Equal(255, img.Get(x0 + 7, y0 + 10).A);
        for (int py = 0; py < T; py++)
        for (int px = 0; px < T; px++)
            Assert.Equal(img.Get(x0 + px, y0 + py), img.Get(x1 + px, y1 + py));
    }

    [Fact]
    public void Ore_HasFleckColors()
    {
        var img = TileGenerator.BuildBlocks(Source, R, out var rows);
        var (x, y) = Cell(rows["ore_b"], 15, 0);
        var fleck = Source.Blocks["ore_b"].Fleck!.Select(Rgba.Parse).ToHashSet();

        int count = 0;
        for (int py = 0; py < T; py++)
        for (int px = 0; px < T; px++)
            if (fleck.Contains(img.Get(x + px, y + py))) count++;
        Assert.InRange(count, 6, 60);
    }

    [Fact]
    public void Merge_IsTransparentExceptA2To4PixelBandOnItsSide()
    {
        var img = TileGenerator.BuildMerge(Source, R);
        int row = Source.MergeOrder.IndexOf("dirt");
        for (int side = 0; side < 4; side++)
        for (int v = 0; v < Source.Variations; v++)
        {
            int x = (side * Source.Variations + v) * T, y = row * T;
            for (int along = 0; along < T; along++)
            {
                int depth = 0;
                for (int d = 0; d < T; d++)
                {
                    var (px, py) = side switch { 0 => (along, d), 1 => (T - 1 - d, along), 2 => (along, T - 1 - d), _ => (d, along) };
                    if (img.Get(x + px, y + py).A != 0) depth = d + 1;
                }
                Assert.InRange(depth, 2, 4);
            }
        }
    }

    [Fact]
    public void Walls_HaveOneRowPerWall_Opaque()
    {
        var img = TileGenerator.BuildWalls(Source, R, out var rows);

        Assert.Equal((R.Walls.Count - 1) * T, img.Height);
        Assert.Equal(1, rows["stone_wall"]);
        var (x, y) = Cell(rows["dirt_wall"], 0, 0);
        Assert.Equal(255, img.Get(x, y).A);
    }

    [Fact]
    public void SameInput_SamePixels()
    {
        var a = TileGenerator.BuildBlocks(Source, R, out _);
        var b = TileGenerator.BuildBlocks(Source, R, out _);

        Assert.Equal(a.Pixels, b.Pixels);
    }

    [Theory]
    [InlineData("""{"seed":1,"variations":3,"blocks":{},"walls":{},"mergeOrder":[],"player":{"skin":["#000000","#000000","#000000"],"hair":["#000000","#000000","#000000"],"outfit":["#000000","#000000","#000000"],"detail":"#000000","boots":["#000000","#000000"],"outline":"#000000"},"items":{}}""", "falta a arte do bloco 'dirt'")]
    [InlineData("""{"seed":1,"variations":0}""", "variations")]
    public void InvalidSource_Throws(string json, string expected)
    {
        var ex = Assert.Throws<DataException>(() => ArtSource.Load(json, R));

        Assert.Contains("art.json", ex.Message);
        Assert.Contains(expected, ex.Message);
    }
}
