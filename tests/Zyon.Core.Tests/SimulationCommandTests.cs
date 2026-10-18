using Zyon.Core.Entities;
using Zyon.Core.Sim;

namespace Zyon.Core.Tests;

public class SimulationCommandTests
{
    [Fact]
    public void SpawnPlayer_GivesStarterItems_AndEmitsSpawn()
    {
        using var h = new SimHarness();

        var events = h.Sim.DrainEvents();

        Assert.Equal(h.Registry.ItemId("basic_pickaxe"), h.Inventory[0].ItemId);
        var spawned = Assert.Single(events.OfType<EntitySpawned>());
        Assert.Equal(h.PlayerId, spawned.NetworkId);
        Assert.Equal(EntityKind.Player, spawned.Kind);
    }

    [Fact]
    public void Step_AdvancesTick()
    {
        using var h = new SimHarness();

        h.Idle(3);

        Assert.Equal(3, h.Sim.Tick);
    }

    [Fact]
    public void SelectHotbarSlot_Valid_ChangesSelection()
    {
        using var h = new SimHarness();
        h.Sim.DrainEvents();

        h.Send(new SelectHotbarSlot(h.PlayerId, 3));

        Assert.Equal(3, h.Sim.GetSelectedSlot(h.PlayerId));
        Assert.Contains(new HotbarSelectionChanged(h.PlayerId, 3), h.Sim.DrainEvents());
    }

    [Fact]
    public void SelectHotbarSlot_OutOfRange_IsDiscarded()
    {
        using var h = new SimHarness();

        h.Send(new SelectHotbarSlot(h.PlayerId, 10));

        Assert.Equal(0, h.Sim.GetSelectedSlot(h.PlayerId));
        Assert.Equal(1, h.Sim.InvalidCommandCount);
    }

    [Fact]
    public void Command_ForUnknownPlayer_IsDiscarded()
    {
        using var h = new SimHarness();

        h.Send(new SelectHotbarSlot(999, 1));

        Assert.Equal(1, h.Sim.InvalidCommandCount);
    }

    [Fact]
    public void MoveInventoryItem_MovesAndEmitsChange()
    {
        using var h = new SimHarness();
        h.Sim.DrainEvents();

        h.Send(new MoveInventoryItem(h.PlayerId, 0, 5));

        Assert.True(h.Inventory[0].IsEmpty);
        Assert.Equal(h.Registry.ItemId("basic_pickaxe"), h.Inventory[5].ItemId);
        var changed = Assert.Single(h.Sim.DrainEvents().OfType<InventoryChanged>());
        Assert.Equal(new[] { 0, 5 }, changed.Slots);
    }

    [Fact]
    public void MoveInventoryItem_Invalid_IsDiscarded()
    {
        using var h = new SimHarness();

        h.Send(new MoveInventoryItem(h.PlayerId, 7, 8));

        Assert.Equal(1, h.Sim.InvalidCommandCount);
    }

    [Fact]
    public void DropItem_EmptiesSlot_AndSpawnsDroppedItem()
    {
        using var h = new SimHarness();
        h.Sim.DrainEvents();

        h.Send(new DropItem(h.PlayerId, 0));

        Assert.True(h.Inventory[0].IsEmpty);
        var events = h.Sim.DrainEvents();
        var spawned = Assert.Single(events.OfType<EntitySpawned>());
        Assert.Equal(EntityKind.DroppedItem, spawned.Kind);
        Assert.True(h.Sim.TryGetEntityView(spawned.NetworkId, out var view));
        Assert.Equal(h.Registry.ItemId("basic_pickaxe"), view.Item.ItemId);
    }

    [Fact]
    public void DropItem_EmptySlot_IsDiscarded()
    {
        using var h = new SimHarness();

        h.Send(new DropItem(h.PlayerId, 9));

        Assert.Equal(1, h.Sim.InvalidCommandCount);
    }

    [Fact]
    public void WorldView_ExposesTilesAndEntities()
    {
        using var h = new SimHarness();
        IWorldView view = h.Sim;
        var buffer = new List<EntityView>();

        Assert.Equal(64, view.Width);
        Assert.Equal(h.Registry.BlockId("dirt"), view.GetBlock(0, 40));
        Assert.Equal(1, view.CopyEntities(buffer));
        Assert.Equal(EntityKind.Player, buffer[0].Kind);
    }
}
