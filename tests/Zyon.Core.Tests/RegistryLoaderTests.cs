using Zyon.Core.Data;

namespace Zyon.Core.Tests;

public class RegistryLoaderTests
{
    private const string Blocks = """[{"id":"dirt","hardness":1,"solid":true,"drops":"dirt"}]""";
    private const string Walls = """[{"id":"dirt_wall"}]""";
    private const string Items = """
        [
          {"id":"dirt","name":"Terra","maxStack":999,"kind":"Block","placesBlock":"dirt"},
          {"id":"pick","name":"Picareta","maxStack":1,"kind":"Tool","power":1,"speed":1.5}
        ]
        """;
    private const string Physics = "{}";
    private const string Player = """{"starterItems":[{"item":"pick","count":1}]}""";

    private static Registry Load(string? blocks = null, string? walls = null, string? items = null,
        string? physics = null, string? player = null) =>
        RegistryLoader.Load(blocks ?? Blocks, walls ?? Walls, items ?? Items, physics ?? Physics, player ?? Player);

    [Fact]
    public void Load_AssignsIdsFromOne_AndReservesZero()
    {
        var r = Load();

        Assert.Equal(1, r.BlockId("dirt"));
        Assert.Equal("air", r.Block(0).Name);
        Assert.False(r.Block(0).Solid);
        Assert.Equal("none", r.Wall(0).Name);
        Assert.Equal(1, r.WallId("dirt_wall"));
        Assert.Equal(1, r.ItemId("dirt"));
        Assert.Equal(2, r.ItemId("pick"));
        Assert.Equal(ItemKind.None, r.Item(0).Kind);
    }

    [Fact]
    public void Load_ResolvesCrossReferences()
    {
        var r = Load();

        Assert.Equal(r.ItemId("dirt"), r.Block(r.BlockId("dirt")).DropItemId);
        var dirtItem = r.Item(r.ItemId("dirt"));
        Assert.Equal(ItemKind.Block, dirtItem.Kind);
        Assert.Equal(r.BlockId("dirt"), dirtItem.PlacesBlockId);
        Assert.Equal("Terra", dirtItem.DisplayName);
        var pick = r.Item(r.ItemId("pick"));
        Assert.Equal(ItemKind.Tool, pick.Kind);
        Assert.Equal(1.5f, pick.ToolSpeed);
    }

    [Fact]
    public void Load_ResolvesStarterItems()
    {
        var r = Load();

        var starter = Assert.Single(r.StarterItems);
        Assert.Equal(r.ItemId("pick"), starter.ItemId);
        Assert.Equal(1, starter.Count);
    }

    [Fact]
    public void Load_EmptyPhysics_UsesDefaults()
    {
        var r = Load();

        Assert.Equal(60f, r.Physics.Gravity);
        Assert.Equal(5f, r.Physics.ReachTiles);
    }

    [Fact]
    public void Load_PhysicsOverride_IsApplied()
    {
        var r = Load(physics: """{"gravity": 10}""");

        Assert.Equal(10f, r.Physics.Gravity);
    }

    [Fact]
    public void Load_DuplicateId_Throws()
    {
        var ex = Assert.Throws<DataException>(() => Load(blocks:
            """[{"id":"dirt","hardness":1,"solid":true},{"id":"dirt","hardness":1,"solid":true}]"""));

        Assert.Contains("blocks.json", ex.Message);
        Assert.Contains("duplicado", ex.Message);
    }

    [Fact]
    public void Load_UnknownReference_Throws()
    {
        var ex = Assert.Throws<DataException>(() => Load(items:
            """[{"id":"x","name":"X","maxStack":1,"kind":"Block","placesBlock":"nope"}]""",
            player: """{"starterItems":[]}""",
            blocks: """[{"id":"dirt","hardness":1,"solid":true}]"""));

        Assert.Contains("items.json", ex.Message);
        Assert.Contains("placesBlock", ex.Message);
    }

    [Fact]
    public void Load_MissingField_Throws()
    {
        var ex = Assert.Throws<DataException>(() => Load(blocks: """[{"id":"dirt","solid":true}]"""));

        Assert.Contains("hardness", ex.Message);
    }

    [Fact]
    public void Load_ReservedName_Throws()
    {
        var ex = Assert.Throws<DataException>(() => Load(blocks: """[{"id":"air","hardness":1,"solid":true}]"""));

        Assert.Contains("reservado", ex.Message);
    }

    [Fact]
    public void Load_InvalidKind_Throws()
    {
        var ex = Assert.Throws<DataException>(() => Load(items:
            """[{"id":"pick","name":"P","maxStack":1,"kind":"Weapon"}]""",
            blocks: """[{"id":"dirt","hardness":1,"solid":true}]"""));

        Assert.Contains("kind", ex.Message);
    }

    [Fact]
    public void Load_UnknownPhysicsField_Throws()
    {
        var ex = Assert.Throws<DataException>(() => Load(physics: """{"gravvity": 10}"""));

        Assert.Contains("physics.json", ex.Message);
    }

    [Fact]
    public void Load_InvalidJson_Throws()
    {
        var ex = Assert.Throws<DataException>(() => Load(walls: "[{"));

        Assert.Contains("walls.json", ex.Message);
    }

    [Fact]
    public void LoadFromDirectory_MissingFile_Throws()
    {
        var dir = Path.Combine(Path.GetTempPath(), "zyon-empty-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        try
        {
            var ex = Assert.Throws<DataException>(() => RegistryLoader.LoadFromDirectory(dir));
            Assert.Contains("blocks.json", ex.Message);
        }
        finally
        {
            Directory.Delete(dir);
        }
    }

    [Fact]
    public void BlockId_Unknown_Throws()
    {
        var r = Load();

        Assert.Throws<KeyNotFoundException>(() => r.BlockId("nope"));
        Assert.False(r.TryGetBlockId("nope", out _));
    }

    private static string Fail(string? blocks = null, string? walls = null, string? items = null,
        string? physics = null, string? player = null) =>
        Assert.Throws<DataException>(() => Load(blocks, walls, items, physics, player)).Message;

    private const string DirtBlock = """[{"id":"dirt","hardness":1,"solid":true}]""";

    [Fact]
    public void Load_NullElementInBlocks_Throws()
    {
        var msg = Fail(blocks: "[null]");
        Assert.Contains("blocks.json", msg);
        Assert.Contains("nula", msg);
    }

    [Fact]
    public void Load_NullElementInWalls_Throws()
    {
        var msg = Fail(walls: """[{"id":"a"}, null]""");
        Assert.Contains("walls.json", msg);
        Assert.Contains("nula", msg);
    }

    [Fact]
    public void Load_NullElementInItems_Throws()
    {
        var msg = Fail(items: "[null]", blocks: DirtBlock);
        Assert.Contains("items.json", msg);
        Assert.Contains("nula", msg);
    }

    [Fact]
    public void Load_NullStarterEntry_Throws()
    {
        var msg = Fail(player: """{"starterItems":[null]}""");
        Assert.Contains("player.json", msg);
        Assert.Contains("nula", msg);
    }

    [Fact]
    public void Load_UnknownDrops_Throws()
    {
        var msg = Fail(blocks: """[{"id":"dirt","hardness":1,"solid":true,"drops":"nope"}]""");
        Assert.Contains("blocks.json", msg);
        Assert.Contains("drops", msg);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void Load_StarterCountOutOfRange_Throws(int count)
    {
        var msg = Fail(player: $$"""{"starterItems":[{"item":"pick","count":{{count}}}]}""");
        Assert.Contains("player.json", msg);
        Assert.Contains("count", msg);
    }

    [Fact]
    public void Load_UnknownStarterItem_Throws()
    {
        var msg = Fail(player: """{"starterItems":[{"item":"nope","count":1}]}""");
        Assert.Contains("player.json", msg);
        Assert.Contains("item", msg);
    }

    [Fact]
    public void Load_MaxStackZero_Throws()
    {
        var msg = Fail(items: """[{"id":"dirt","name":"T","maxStack":0,"kind":"Block","placesBlock":"dirt"}]""",
            player: """{"starterItems":[]}""", blocks: DirtBlock);
        Assert.Contains("items.json", msg);
        Assert.Contains("maxStack", msg);
    }

    [Fact]
    public void Load_NegativeHardness_Throws()
    {
        var msg = Fail(blocks: """[{"id":"dirt","hardness":-1,"solid":true}]""");
        Assert.Contains("blocks.json", msg);
        Assert.Contains("hardness", msg);
    }

    [Fact]
    public void Load_ToolPowerZero_Throws()
    {
        var msg = Fail(items: """[{"id":"pick","name":"P","maxStack":1,"kind":"Tool","power":0,"speed":1}]""",
            player: """{"starterItems":[]}""", blocks: DirtBlock);
        Assert.Contains("items.json", msg);
        Assert.Contains("power", msg);
    }

    [Fact]
    public void Load_MissingKind_Throws()
    {
        var msg = Fail(items: """[{"id":"pick","name":"P","maxStack":1}]""",
            player: """{"starterItems":[]}""", blocks: DirtBlock);
        Assert.Contains("items.json", msg);
        Assert.Contains("kind", msg);
    }

    [Fact]
    public void Load_DuplicateItemId_Throws()
    {
        var msg = Fail(items: """
            [{"id":"pick","name":"P","maxStack":1,"kind":"Tool","power":1,"speed":1},
             {"id":"pick","name":"P","maxStack":1,"kind":"Tool","power":1,"speed":1}]
            """, player: """{"starterItems":[]}""", blocks: DirtBlock);
        Assert.Contains("items.json", msg);
        Assert.Contains("duplicado", msg);
    }

    [Fact]
    public void Load_ReservedWallName_Throws()
    {
        var msg = Fail(walls: """[{"id":"none"}]""");
        Assert.Contains("walls.json", msg);
        Assert.Contains("reservado", msg);
    }

    [Fact]
    public void Load_JsonNullContent_Throws()
    {
        var msg = Fail(blocks: "null");
        Assert.Contains("blocks.json", msg);
        Assert.Contains("conteúdo vazio", msg);
    }

    [Fact]
    public void Load_MisspelledBlockField_Throws()
    {
        var msg = Fail(blocks: """[{"id":"dirt","hardnes":1,"solid":true}]""");
        Assert.Contains("blocks.json", msg);
    }
}
