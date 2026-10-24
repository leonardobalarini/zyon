using Zyon.Core.Data;

namespace Zyon.Client.Tests;

public class ArtCatalogTests
{
    private static ArtManifest Complete()
    {
        var r = TestData.Registry();
        var m = new ArtManifest();
        for (int i = 1; i < r.Blocks.Count; i++) m.Tiles[r.Blocks[i].Name] = i - 1;
        for (int i = 1; i < r.Walls.Count; i++) m.Walls[r.Walls[i].Name] = i - 1;
        m.MergeOrder.AddRange(["dirt", "stone", "deep_stone", "ore_a", "ore_b"]);
        for (int i = 1; i < r.Items.Count; i++)
            m.Items[r.Items[i].Name] = new ItemArt { Cell = i - 1, GripX = 8, GripY = 14, PointDeg = -90 };
        m.Player.Hand.AddRange(Enumerable.Repeat(new[] { 20, 28 }, m.Player.ArmFrames));
        m.Player.ArmAngleDeg.AddRange(Enumerable.Repeat(90f, m.Player.ArmFrames));
        return m;
    }

    [Fact]
    public void Resolve_MapsNamesToIds()
    {
        var r = TestData.Registry();

        var c = ArtCatalog.Resolve(Complete(), r);

        Assert.Equal(0, c.TileRowByBlockId[r.BlockId("dirt")]);
        Assert.Equal(-1, c.TileRowByBlockId[0]);
        Assert.Equal(1, c.MergeRankByBlockId[r.BlockId("stone")]);
        Assert.Equal(-1, c.MergeRankByBlockId[r.BlockId("torch")]);
        Assert.Equal(r.Items.Count - 2, c.ItemByItemId[r.ItemId("torch")]!.Cell);
    }

    [Fact]
    public void MissingBlockArt_Throws()
    {
        var m = Complete();
        m.Tiles.Remove("torch");

        var ex = Assert.Throws<DataException>(() => ArtCatalog.Resolve(m, TestData.Registry()));

        Assert.Contains("falta a arte do bloco 'torch'", ex.Message);
    }

    [Theory]
    [InlineData("wall")]
    [InlineData("item")]
    public void MissingWallOrItemArt_Throws(string what)
    {
        var m = Complete();
        if (what == "wall") m.Walls.Remove("stone_wall"); else m.Items.Remove("basic_pickaxe");

        var ex = Assert.Throws<DataException>(() => ArtCatalog.Resolve(m, TestData.Registry()));

        Assert.Contains(what == "wall" ? "parede 'stone_wall'" : "item 'basic_pickaxe'", ex.Message);
    }

    [Fact]
    public void UnknownName_OrNonSolidInMergeOrder_Throws()
    {
        var m = Complete();
        m.MergeOrder.Add("torch");
        Assert.Contains("mergeOrder", Assert.Throws<DataException>(() => ArtCatalog.Resolve(m, TestData.Registry())).Message);

        m = Complete();
        m.Tiles["unobtainium"] = 9;
        Assert.Contains("unobtainium", Assert.Throws<DataException>(() => ArtCatalog.Resolve(m, TestData.Registry())).Message);
    }

    [Fact]
    public void WrongPlayerFrameData_Throws()
    {
        var m = Complete();
        m.Player.Hand.RemoveAt(0);

        Assert.Contains("hand", Assert.Throws<DataException>(() => ArtCatalog.Resolve(m, TestData.Registry())).Message);
    }

    [Theory]
    [InlineData("variations")]
    [InlineData("tileSize")]
    [InlineData("tiles.dirt")]
    [InlineData("walls.dirt_wall")]
    [InlineData("items.dirt.cell")]
    public void InvalidNumbers_ThrowNamingTheField(string field)
    {
        var m = Complete();
        switch (field)
        {
            case "variations": m.Variations = 0; break;
            case "tileSize": m.TileSize = 32; break;
            case "tiles.dirt": m.Tiles["dirt"] = -2; break;
            case "walls.dirt_wall": m.Walls["dirt_wall"] = -1; break;
            case "items.dirt.cell": m.Items["dirt"].Cell = -1; break;
        }

        Assert.Contains(field, Assert.Throws<DataException>(() => ArtCatalog.Resolve(m, TestData.Registry())).Message);
    }

    [Theory]
    [InlineData("\"tiles\"", "tiles")]
    [InlineData("\"walls\"", "walls")]
    [InlineData("\"mergeOrder\"", "mergeOrder")]
    [InlineData("\"items\"", "items")]
    [InlineData("\"player\"", "player")]
    [InlineData("\"hand\"", "player.hand")]
    [InlineData("\"layers\"", "player.layers")]
    [InlineData("\"armAngleDeg\"", "player.armAngleDeg")]
    public void NullCollections_ThrowDataException(string property, string field)
    {
        string json = NullProperty(Complete().ToJson(), property);

        var ex = Assert.Throws<DataException>(() => ArtCatalog.Resolve(ArtManifest.FromJson(json), TestData.Registry()));

        Assert.Contains(field, ex.Message);
    }

    [Fact]
    public void NullEntries_ThrowDataException()
    {
        var m = Complete();
        m.Player.Hand[3] = null!;
        Assert.Contains("player.hand", Assert.Throws<DataException>(() => ArtCatalog.Resolve(m, TestData.Registry())).Message);

        m = Complete();
        m.Items["dirt"] = null!;
        Assert.Contains("items.dirt", Assert.Throws<DataException>(() => ArtCatalog.Resolve(m, TestData.Registry())).Message);

        m = Complete();
        m.MergeOrder.Add(null!);
        Assert.Contains("mergeOrder[5]", Assert.Throws<DataException>(() => ArtCatalog.Resolve(m, TestData.Registry())).Message);
    }

    [Fact]
    public void ExpectedSize_FollowsTheManifest()
    {
        var r = TestData.Registry();
        var c = ArtCatalog.Resolve(Complete(), r);
        int v = c.Manifest.Variations;

        Assert.Equal(ArtImageSize.Sheet(16 * v * 16, true, (r.Blocks.Count - 1) * 16), c.ExpectedSize(ArtManifest.TilesFile));
        Assert.Equal(ArtImageSize.Sheet(16 * v * 16, true, (r.Walls.Count - 1) * 16), c.ExpectedSize(ArtManifest.WallsFile));
        Assert.Equal(ArtImageSize.Sheet(4 * v * 16, true, 5 * 16), c.ExpectedSize(ArtManifest.MergeFile));
        Assert.Equal(ArtImageSize.Sheet((r.Items.Count - 1) * 16, false, 16), c.ExpectedSize(ArtManifest.ItemsFile));
        Assert.Equal(ArtImageSize.Exact(10 * 32, 48), c.ExpectedSize(ArtManifest.PlayerFile("hair")));
        Assert.Equal(ArtImageSize.Exact(15 * 32, 48), c.ExpectedSize(ArtManifest.PlayerFile(ArtManifest.ArmLayer)));
    }

    [Fact]
    public void ExpectedSize_EmptyMergeOrderStillNeedsOneRow()
    {
        var m = Complete();
        m.MergeOrder.Clear();

        Assert.Equal(16, ArtCatalog.Resolve(m, TestData.Registry()).ExpectedSize(ArtManifest.MergeFile).Height);
    }

    [Fact]
    public void CheckSize_AcceptsExtraRowsOnSheets_AndRejectsWrongSizesNamingFileAndSize()
    {
        var c = ArtCatalog.Resolve(Complete(), TestData.Registry());
        var tiles = c.ExpectedSize(ArtManifest.TilesFile);

        c.CheckSize(ArtManifest.TilesFile, tiles.Width, tiles.Height);
        c.CheckSize(ArtManifest.TilesFile, tiles.Width, tiles.Height + 16);
        c.CheckSize(ArtManifest.ItemsFile, c.ExpectedSize(ArtManifest.ItemsFile).Width + 32, 16);

        var narrow = Assert.Throws<DataException>(() => c.CheckSize(ArtManifest.TilesFile, tiles.Width - 16, tiles.Height));
        Assert.Contains("tiles.png", narrow.Message);
        Assert.Contains($"largura {tiles.Width}", narrow.Message);
        Assert.Throws<DataException>(() => c.CheckSize(ArtManifest.TilesFile, tiles.Width, tiles.Height - 16));
        Assert.Throws<DataException>(() => c.CheckSize(ArtManifest.TilesFile, tiles.Width, tiles.Height + 8));
        var arm = Assert.Throws<DataException>(() => c.CheckSize(ArtManifest.PlayerFile(ArtManifest.ArmLayer), 320, 48));
        Assert.Contains("arm.png", arm.Message);
        Assert.Contains("largura 480, altura 48", arm.Message);
    }

    [Fact]
    public void RealArt_PngSizesMatchTheManifest()
    {
        var c = ArtCatalog.LoadFromDirectory(TestData.DataDir, TestData.Registry());
        var files = new List<string> { ArtManifest.TilesFile, ArtManifest.WallsFile, ArtManifest.MergeFile, ArtManifest.ItemsFile,
            ArtManifest.PlayerFile(ArtManifest.ArmLayer) };
        files.AddRange(c.Manifest.Player.Layers.Select(ArtManifest.PlayerFile));

        foreach (var f in files)
        {
            var (w, h) = PngSize(c.PathOf(f));
            c.CheckSize(f, w, h);
        }
    }

    private static (int Width, int Height) PngSize(string path)
    {
        var b = File.ReadAllBytes(path);   // IHDR: largura e altura big-endian nos bytes 16–23
        return ((b[16] << 24) | (b[17] << 16) | (b[18] << 8) | b[19], (b[20] << 24) | (b[21] << 16) | (b[22] << 8) | b[23]);
    }

    private static string NullProperty(string json, string property)
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse(json)!.AsObject();
        string name = property.Trim('"');
        var owner = node.ContainsKey(name) ? node : node["player"]!.AsObject();
        owner[name] = null;
        return node.ToJsonString();
    }

    [Fact]
    public void Json_RoundTrips()
    {
        var m = Complete();

        var back = ArtManifest.FromJson(m.ToJson());

        Assert.Equal(m.ToJson(), back.ToJson());
        Assert.Contains("\"mergeOrder\"", m.ToJson());
    }

    [Fact]
    public void LoadFromDirectory_MissingPng_ThrowsWithPath()
    {
        string dir = Path.Combine(Path.GetTempPath(), "zyon-art-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "client", "art"));
        File.WriteAllText(Path.Combine(dir, "client", "art", ArtManifest.FileName), Complete().ToJson());
        try
        {
            var ex = Assert.Throws<DataException>(() => ArtCatalog.LoadFromDirectory(dir, TestData.Registry()));
            Assert.Contains("tiles.png", ex.Message);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void RealArt_LoadsAndCoversAllContent()
    {
        var c = ArtCatalog.LoadFromDirectory(TestData.DataDir, TestData.Registry());

        Assert.Equal(TestData.Registry().Blocks.Count, c.TileRowByBlockId.Count);
        Assert.True(File.Exists(c.PathOf(ArtManifest.TilesFile)));
    }
}
