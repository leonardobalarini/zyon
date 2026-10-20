using Zyon.Core.Data;

namespace Zyon.Client.Tests;

public class ClientVisualsTests
{
    [Fact]
    public void RealFile_LoadsColorsByIdForEveryBlockWallAndItem()
    {
        var r = TestData.Registry();

        var v = ClientVisuals.LoadFromDirectory(TestData.DataDir, r);

        Assert.Equal(r.Blocks.Count, v.BlockColors.Count);
        Assert.Null(v.BlockColors[0]);
        Assert.NotNull(v.BlockColors[r.BlockId("dirt")]);
        Assert.NotNull(v.WallColors[r.WallId("stone_wall")]);
        Assert.NotEqual(ClientVisuals.FallbackColor, v.ItemColor(r.ItemId("basic_pickaxe")));
    }

    [Fact]
    public void ItemWithoutOwnColor_UsesThePlacedBlockColor()
    {
        var r = TestData.Registry();

        var v = ClientVisuals.Load("""{"blocks": {"dirt":"#102030","stone":"#000000","deep_stone":"#000000","ore_a":"#000000","ore_b":"#000000"}, "walls": {"dirt_wall":"#000000","stone_wall":"#000000"}, "items": {}, "player": "#ffffff", "sky": "#000000"}""", r);

        Assert.Equal(new Rgb(0x10, 0x20, 0x30), v.ItemColor(r.ItemId("dirt")));
        Assert.Equal(ClientVisuals.FallbackColor, v.ItemColor(r.ItemId("basic_pickaxe")));
    }

    [Theory]
    [InlineData("""{"blocks": {}, "walls": {}, "items": {}, "player": "#ffffff", "sky": "#000000"}""", "dirt")]
    [InlineData("""{"blocks": {"dirt":"#zz0000"}, "walls": {}, "items": {}, "player": "#ffffff", "sky": "#000000"}""", "#zz0000")]
    [InlineData("""{"blocks": {"mithril":"#000000"}, "walls": {}, "items": {}, "player": "#ffffff", "sky": "#000000"}""", "mithril")]
    public void InvalidFile_Throws(string json, string expectedInMessage)
    {
        var ex = Assert.Throws<DataException>(() => ClientVisuals.Load(json, TestData.Registry()));

        Assert.Contains("visuals.json", ex.Message);
        Assert.Contains(expectedInMessage, ex.Message);
    }

    private const string AllBlocks = "\"dirt\":\"#000000\",\"stone\":\"#000000\",\"deep_stone\":\"#000000\",\"ore_a\":\"#000000\",\"ore_b\":\"#000000\"";
    private const string AllWalls = "\"dirt_wall\":\"#000000\",\"stone_wall\":\"#000000\"";

    [Fact]
    public void ColorWithWhitespace_Throws()
    {
        var json = "{\"blocks\": {\"dirt\":\"# 12345\"}, \"walls\": {}, \"items\": {}, \"player\": \"#ffffff\", \"sky\": \"#000000\"}";

        Assert.Throws<DataException>(() => ClientVisuals.Load(json, TestData.Registry()));
    }

    [Fact]
    public void CommentsAndTrailingCommas_AreAccepted()
    {
        var json = "{\n// comentário\n\"blocks\": {" + AllBlocks + ",}, \"walls\": {" + AllWalls + "}, \"items\": {}, \"player\": \"#ffffff\", \"sky\": \"#000000\",\n}";

        var v = ClientVisuals.Load(json, TestData.Registry());

        Assert.Equal(new Rgb(255, 255, 255), v.Player);
    }

    [Fact]
    public void MissingPlayer_SaysFieldIsAbsent()
    {
        var json = "{\"blocks\": {" + AllBlocks + "}, \"walls\": {" + AllWalls + "}, \"items\": {}, \"sky\": \"#000000\"}";

        var ex = Assert.Throws<DataException>(() => ClientVisuals.Load(json, TestData.Registry()));

        Assert.Contains("player", ex.Message);
        Assert.Contains("ausente", ex.Message);
    }
}
