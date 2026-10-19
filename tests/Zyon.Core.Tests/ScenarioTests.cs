using Zyon.Core.Sim;

namespace Zyon.Core.Tests;

public class ScenarioTests
{
    private static PlayerInput Use(int x, int y) => new(0f, false, true, x, y);

    [Fact]
    public void MiningThreeBlocks_EndsWithThreeItemsInInventory()
    {
        using var h = new SimHarness();
        h.Idle(5);

        h.Run(40, Use(21, 40));
        h.Run(40, Use(22, 40));
        h.Run(40, Use(23, 40));
        h.Run(60, new PlayerInput(1f, false, false, 0, 0));
        h.Idle(60);

        Assert.Equal(0, h.Tiles.GetBlock(21, 40));
        Assert.Equal(0, h.Tiles.GetBlock(22, 40));
        Assert.Equal(0, h.Tiles.GetBlock(23, 40));
        int dirt = Enumerable.Range(0, h.Inventory.SlotCount)
            .Select(i => h.Inventory[i])
            .Where(s => s.ItemId == h.Registry.ItemId("dirt"))
            .Sum(s => s.Count);
        Assert.Equal(3, dirt);
    }

    [Fact]
    public void PlaceThenMineBack_KeepsItemCount()
    {
        using var h = new SimHarness();
        h.Give("dirt", 1);
        h.Send(new SelectHotbarSlot(h.PlayerId, 1));
        h.Run(1, Use(22, 39));
        Assert.True(h.Inventory[1].IsEmpty);

        h.Send(new SelectHotbarSlot(h.PlayerId, 0));
        h.Run(40, Use(22, 39));
        h.Idle(60);

        Assert.Equal(0, h.Tiles.GetBlock(22, 39));
        Assert.Equal(1, h.Inventory[1].Count);
    }
}
