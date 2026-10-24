using Zyon.Core.Data;

namespace Zyon.Core.Tests;

public class LightingDataTests
{
    private const string Walls = """[{"id":"dirt_wall"}]""";
    private const string Items = """[{"id":"dirt","name":"Terra","maxStack":999,"kind":"Block","placesBlock":"dirt"}]""";
    private const string Player = """{"starterItems":[]}""";

    [Fact]
    public void Light_IsOptional_AndDefaultsToZero()
    {
        var r = RegistryLoader.Load("""[{"id":"dirt","hardness":1,"solid":true,"drops":"dirt"}]""", Walls, Items, "{}", Player);

        Assert.Equal(0f, r.Block(r.BlockId("dirt")).Light);
        Assert.Equal(0f, r.Block(0).Light);
    }

    [Fact]
    public void Light_IsLoaded()
    {
        var r = RegistryLoader.Load("""[{"id":"dirt","hardness":1,"solid":true,"drops":"dirt","light":0.4}]""", Walls, Items, "{}", Player);

        Assert.Equal(0.4f, r.Block(r.BlockId("dirt")).Light);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.5)]
    public void Light_OutsideZeroToOne_Throws(double light)
    {
        string blocks = $$"""[{"id":"dirt","hardness":1,"solid":true,"drops":"dirt","light":{{light.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}]""";

        var ex = Assert.Throws<DataException>(() => RegistryLoader.Load(blocks, Walls, Items, "{}", Player));

        Assert.Contains("blocks.json", ex.Message);
        Assert.Contains("light", ex.Message);
    }

    [Fact]
    public void RealData_HasTorch_GlowingOre_AndStarterTorches()
    {
        var r = TestData.Registry();

        var torch = r.Block(r.BlockId("torch"));
        Assert.False(torch.Solid);
        Assert.Equal(0f, torch.Hardness);
        Assert.Equal(1f, torch.Light);
        Assert.Equal(r.ItemId("torch"), torch.DropItemId);
        Assert.Equal(0.35f, r.Block(r.BlockId("ore_b")).Light);
        Assert.Contains(r.StarterItems, s => s.ItemId == r.ItemId("torch") && s.Count == 30);
    }

    [Fact]
    public void Torch_IsTheLastBlock_SoExistingIdsDoNotChange()
    {
        var r = TestData.Registry();

        Assert.Equal(r.Blocks.Count - 1, r.BlockId("torch"));
    }
}
