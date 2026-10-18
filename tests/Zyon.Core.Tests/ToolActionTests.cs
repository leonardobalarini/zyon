using Zyon.Core.Entities;
using Zyon.Core.Sim;

namespace Zyon.Core.Tests;

public class ToolActionTests
{
    private static PlayerInput Use(int x, int y) => new(0f, false, true, x, y);

    /// <summary>Ticks necessários para quebrar terra com a picareta inicial, a partir dos dados.</summary>
    private static int TicksToMineDirt(SimHarness h)
    {
        var r = h.Registry;
        var pick = r.Item(r.ItemId("basic_pickaxe"));
        float hardness = r.Block(r.BlockId("dirt")).Hardness;
        float perSecond = pick.ToolPower * pick.ToolSpeed * r.Physics.MiningRate / hardness;
        return (int)MathF.Ceiling(Simulation.TicksPerSecond / perSecond);
    }

    [Fact]
    public void MiningDirt_BreaksAfterEnoughTicks_AndDropsItem()
    {
        using var h = new SimHarness();
        int needed = TicksToMineDirt(h);
        h.Idle(5);
        h.Sim.DrainEvents();

        h.Run(needed - 5, Use(21, 40));
        Assert.NotEqual(0, h.Tiles.GetBlock(21, 40));

        h.Run(10, Use(21, 40));
        Assert.Equal(0, h.Tiles.GetBlock(21, 40));

        var events = h.Sim.DrainEvents();
        Assert.Contains(new TileChanged(21, 40, 0, 0), events);
        Assert.Contains(events.OfType<EntitySpawned>(), e => e.Kind == EntityKind.DroppedItem);
        Assert.Contains(events.OfType<MiningProgressChanged>(), e => e.Progress > 0f);
    }

    [Fact]
    public void ChangingTarget_ResetsProgress()
    {
        using var h = new SimHarness();
        int needed = TicksToMineDirt(h);

        h.Run(needed - 5, Use(21, 40));
        h.Run(needed - 5, Use(22, 40));

        Assert.NotEqual(0, h.Tiles.GetBlock(21, 40));
        Assert.NotEqual(0, h.Tiles.GetBlock(22, 40));
    }

    [Fact]
    public void ReleasingButton_ResetsProgress()
    {
        using var h = new SimHarness();
        int needed = TicksToMineDirt(h);

        h.Run(needed - 5, Use(21, 40));
        h.Idle(1);
        h.Run(needed - 5, Use(21, 40));

        Assert.NotEqual(0, h.Tiles.GetBlock(21, 40));
    }

    [Fact]
    public void OutOfReach_DoesNotMine()
    {
        using var h = new SimHarness();

        h.Run(200, Use(30, 40));

        Assert.NotEqual(0, h.Tiles.GetBlock(30, 40));
    }

    [Fact]
    public void PlacingBlock_ConsumesItem()
    {
        using var h = new SimHarness();
        h.Give("dirt", 5);
        h.Send(new SelectHotbarSlot(h.PlayerId, 1));
        h.Sim.DrainEvents();

        h.Run(1, Use(22, 39));

        Assert.Equal(h.Registry.BlockId("dirt"), h.Tiles.GetBlock(22, 39));
        Assert.Equal(4, h.Inventory[1].Count);
        var events = h.Sim.DrainEvents();
        Assert.Contains(new TileChanged(22, 39, h.Registry.BlockId("dirt"), 0), events);
        Assert.Contains(events.OfType<InventoryChanged>(), e => e.Slots.Contains(1));
    }

    [Fact]
    public void Placing_RequiresAnchor()
    {
        using var h = new SimHarness();
        h.Give("dirt", 5);
        h.Send(new SelectHotbarSlot(h.PlayerId, 1));

        h.Run(5, Use(22, 36));

        Assert.Equal(0, h.Tiles.GetBlock(22, 36));
    }

    [Fact]
    public void Placing_WallCountsAsAnchor()
    {
        using var h = new SimHarness();
        h.Give("dirt", 5);
        h.Send(new SelectHotbarSlot(h.PlayerId, 1));
        h.Tiles.SetWall(22, 36, h.Registry.WallId("dirt_wall"));

        h.Run(1, Use(22, 36));

        Assert.Equal(h.Registry.BlockId("dirt"), h.Tiles.GetBlock(22, 36));
    }

    [Fact]
    public void Placing_InsidePlayer_IsBlocked()
    {
        using var h = new SimHarness();
        h.Give("dirt", 5);
        h.Send(new SelectHotbarSlot(h.PlayerId, 1));

        h.Run(5, Use(20, 39));

        Assert.Equal(0, h.Tiles.GetBlock(20, 39));
    }

    [Fact]
    public void Placing_HasCooldown()
    {
        using var h = new SimHarness();
        h.Give("dirt", 5);
        h.Send(new SelectHotbarSlot(h.PlayerId, 1));

        h.Run(1, Use(22, 39));
        h.Run(1, Use(23, 39));
        Assert.Equal(0, h.Tiles.GetBlock(23, 39));

        h.Run(h.Registry.Physics.PlaceCooldownTicks, Use(23, 39));
        Assert.NotEqual(0, h.Tiles.GetBlock(23, 39));
    }

    [Fact]
    public void EmptySlot_DoesNothing()
    {
        using var h = new SimHarness();
        h.Send(new SelectHotbarSlot(h.PlayerId, 4));

        h.Run(100, Use(21, 40));

        Assert.NotEqual(0, h.Tiles.GetBlock(21, 40));
    }
}
