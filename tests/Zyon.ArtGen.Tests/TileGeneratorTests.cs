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
        Assert.Equal(R.BlockId("torch") - 1, rows["torch"]);
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

    [Theory]
    [InlineData("bunker_lamp")]
    [InlineData("crate")]
    [InlineData("pipe")]
    [InlineData("vent")]
    [InlineData("panel")]
    public void Prop_IsTheSameInEveryCell_AndHasTransparentPixels(string name)
    {
        var img = TileGenerator.BuildBlocks(Source, R, out var rows);
        int y = rows[name] * T;
        int cells = img.Width / T;
        bool anyTransparent = false, anyOpaque = false;
        for (int py = 0; py < T; py++)
        for (int px = 0; px < T; px++)
        {
            var first = img.Get(px, y + py);
            if (first.A == 0) anyTransparent = true; else anyOpaque = true;
            for (int cell = 1; cell < cells; cell++)
                Assert.Equal(first, img.Get(cell * T + px, y + py));
        }
        Assert.True(anyTransparent, $"{name}: sem fundo transparente");
        Assert.True(anyOpaque, $"{name}: vazio");
    }

    [Fact]
    public void MetalPlate_HasJointsAndRivets()
    {
        var img = TileGenerator.BuildBlocks(Source, R, out var rows);
        var pal = ArtSource.Colors(Source.Blocks["metal_plate"].Palette!);
        // Máscara 15 (todos os vizinhos conectados): sem contorno; variação 0.
        int x = 15 * Source.Variations * T, y = rows["metal_plate"] * T;
        Assert.Equal(pal[0], img.Get(x + 7, y + 3));    // junta vertical (fiada de cima, sem desencontro)
        Assert.Equal(pal[0], img.Get(x + 3, y + 7));    // junta horizontal
        Assert.Equal(pal[2], img.Get(x + 1, y + 1));    // rebite
    }

    [Fact]
    public void HazardPlate_HasBothStripeColors()
    {
        var img = TileGenerator.BuildBlocks(Source, R, out var rows);
        var m = Source.Blocks["hazard_plate"];
        var yellow = ArtSource.Colors(m.Palette!);
        var black = ArtSource.Colors(m.Accent!);
        int x = 15 * Source.Variations * T, y = rows["hazard_plate"] * T;
        var pixels = Enumerable.Range(0, T * T).Select(i => img.Get(x + i % T, y + i / T)).ToList();
        Assert.Contains(pixels, p => yellow.Contains(p));
        Assert.Contains(pixels, p => black.Contains(p));
    }

    [Fact]
    public void MetalWall_UsesPlatesJoints()
    {
        var img = TileGenerator.BuildWalls(Source, R, out var rows);
        var pal = ArtSource.Colors(Source.Walls["metal_wall"].Palette!);
        int x = 15 * Source.Variations * T, y = rows["metal_wall"] * T;
        Assert.Equal(pal[0], img.Get(x + 7, y + 3));
        Assert.Equal(pal[0], img.Get(x + 3, y + 7));
        Assert.Equal(pal[2], img.Get(x + 1, y + 1));
    }

    [Theory]
    [InlineData("crate", """{"recipe":"clumps","palette":["#000000","#000000","#000000"]}""", "'torch' ou 'prop'")]
    [InlineData("crate", """{"recipe":"prop","shape":"sofa","palette":["#000000","#000000","#000000"]}""", "forma 'sofa'")]
    [InlineData("bunker_lamp", """{"recipe":"prop","shape":"lamp","palette":["#000000","#000000","#000000"]}""", "accent")]
    [InlineData("hazard_plate", """{"recipe":"stripes","palette":["#000000","#000000","#000000"]}""", "accent")]
    public void InvalidNewRecipes_Throw(string block, string entry, string expected)
    {
        var root = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(RepoPaths.Root, "art", "art.json")))!;
        root["blocks"]![block] = System.Text.Json.Nodes.JsonNode.Parse(entry);
        var ex = Assert.Throws<DataException>(() => ArtSource.Load(root.ToJsonString(), R));
        Assert.Contains(expected, ex.Message);
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

    private static ArtSource FreshSource() => ArtSource.LoadFromFile(Path.Combine(RepoPaths.Root, "art", "art.json"), R);

    private static readonly string[] Gray = ["#101010", "#808080", "#c0c0c0"];

    private static (int X, int Y) CellOf(ArtSource src, int row, int mask, int variation) =>
        ((mask * src.Variations + variation) * T, row * T);

    /// <summary>Em cada coluna da célula, as linhas escuras ficam a 4 px uma da outra (faixas horizontais).</summary>
    private static void AssertStrata(Canvas img, int x, int y)
    {
        var dark = Rgba.Parse(Gray[0]);
        for (int px = 0; px < T; px++)
        {
            var rows = Enumerable.Range(0, T).Where(py => img.Get(x + px, y + py) == dark).ToList();
            Assert.Equal(4, rows.Count);
            for (int i = 1; i < rows.Count; i++) Assert.Equal(4, rows[i] - rows[i - 1]);
        }
    }

    [Fact]
    public void Strata_HasHorizontalDarkLinesEvery4Pixels()
    {
        var src = FreshSource();
        src.Blocks["stone"] = new MaterialSource { Recipe = "strata", Palette = Gray };
        var img = TileGenerator.BuildBlocks(src, R, out var rows);
        var (x, y) = CellOf(src, rows["stone"], mask: 15, variation: 1);

        AssertStrata(img, x, y);
    }

    [Fact]
    public void StrataWall_HasTheSameLines()
    {
        var src = FreshSource();
        src.Walls["stone_wall"] = new MaterialSource { Recipe = "strata", Palette = Gray };
        var img = TileGenerator.BuildWalls(src, R, out var rows);
        var (x, y) = CellOf(src, rows["stone_wall"], mask: 15, variation: 0);

        AssertStrata(img, x, y);
    }

    [Fact]
    public void Planks_HaveDarkJointEvery4Rows_AndASeamInEachPlank()
    {
        var src = FreshSource();
        src.Blocks["stone"] = new MaterialSource { Recipe = "planks", Palette = Gray };
        var img = TileGenerator.BuildBlocks(src, R, out var rows);
        var (x, y) = CellOf(src, rows["stone"], mask: 15, variation: 2);
        var dark = Rgba.Parse(Gray[0]);

        foreach (int py in new[] { 3, 7, 11, 15 })
            for (int px = 0; px < T; px++)
                Assert.Equal(dark, img.Get(x + px, y + py));
        foreach (int py in new[] { 1, 5, 9, 13 })
            Assert.Contains(Enumerable.Range(0, T), px => img.Get(x + px, y + py) == dark);
    }

    [Fact]
    public void ClumpsWithAccent_HaveGlints()
    {
        var src = FreshSource();
        src.Blocks["stone"] = new MaterialSource { Recipe = "clumps", Palette = Gray, Accent = ["#0000ff", "#00ff00", "#ff0000"] };
        var img = TileGenerator.BuildBlocks(src, R, out var rows);
        var (x, y) = CellOf(src, rows["stone"], mask: 15, variation: 0);
        var brightest = Rgba.Parse("#ff0000");

        Assert.Contains(Enumerable.Range(0, T * T), i => img.Get(x + i % T, y + i / T) == brightest);
    }

    [Fact]
    public void PlanksWall_IsRejected()
    {
        var root = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(RepoPaths.Root, "art", "art.json")))!;
        root["walls"]!["stone_wall"]!["recipe"] = "planks";

        var ex = Assert.Throws<DataException>(() => ArtSource.Load(root.ToJsonString(), R));

        Assert.Contains("planks", ex.Message);
    }

    [Theory]
    [InlineData("trunk", true)]
    [InlineData("cactus", true)]
    [InlineData("branch", false)]
    [InlineData("leaves", false)]
    [InlineData("grass", false)]
    [InlineData("shrub", false)]
    [InlineData("bush", false)]
    public void TreeAndDecorationShapes_HaveOpaqueAndTransparentPixels(string shape, bool crossesVertically)
    {
        var img = new Canvas(T, T);
        var pal = Gray.Select(Rgba.Parse).ToArray();

        PropGenerator.Draw(img, 0, 0, shape, pal, pal);

        int opaque = Enumerable.Range(0, T * T).Count(i => img.Get(i % T, i / T).A != 0);
        Assert.InRange(opaque, 8, T * T - 1);
        if (crossesVertically)   // tronco e cacto atravessam o tile: o de cima continua o de baixo
            for (int px = 0; px < T; px++)
                Assert.Equal(img.Get(px, 0).A != 0, img.Get(px, T - 1).A != 0);
    }
}
