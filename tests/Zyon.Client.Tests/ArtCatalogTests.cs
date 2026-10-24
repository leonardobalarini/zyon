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
