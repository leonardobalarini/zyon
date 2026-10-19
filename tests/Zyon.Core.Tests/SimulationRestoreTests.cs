using System.Numerics;
using Zyon.Core.Items;
using Zyon.Core.Sim;

namespace Zyon.Core.Tests;

public class SimulationRestoreTests
{
    [Fact]
    public void SpawnPlayer_WithInventory_UsesItExactly()
    {
        var r = TestData.Registry();
        using var sim = new Simulation(r, TestWorlds.Flat(r, 64, 64, 40));
        var inv = new Inventory(r);
        inv.Set(7, new ItemStack(r.ItemId("dirt"), 37));

        int id = sim.SpawnPlayer(new Vector2(20f, 40f - r.Physics.PlayerHeight / 2), inv, selectedSlot: 3);

        var loaded = sim.GetInventory(id)!;
        Assert.True(loaded[0].IsEmpty);   // sem itens iniciais
        Assert.Equal(new ItemStack(r.ItemId("dirt"), 37), loaded[7]);
        Assert.Equal(3, sim.GetSelectedSlot(id));
    }

    [Fact]
    public void SpawnPlayer_WithInventory_InvalidSelectedSlot_Throws()
    {
        var r = TestData.Registry();
        using var sim = new Simulation(r, TestWorlds.Flat(r, 64, 64, 40));

        Assert.Throws<ArgumentOutOfRangeException>(() => sim.SpawnPlayer(new Vector2(20f, 30f), new Inventory(r), 10));
    }

    [Fact]
    public void RestoreTick_SetsClock_AndStepContinuesFromIt()
    {
        var r = TestData.Registry();
        using var sim = new Simulation(r, TestWorlds.Flat(r, 64, 64, 40));

        sim.RestoreTick(5000);
        sim.Step();

        Assert.Equal(5001, sim.Tick);
        Assert.Throws<ArgumentOutOfRangeException>(() => sim.RestoreTick(-1));
    }

    [Fact]
    public void DroppedItem_RestoredWithAge_DespawnsAccordingly()
    {
        var r = TestData.RegistryWithPhysics("""{"itemDespawnSeconds": 1}""");
        using var sim = new Simulation(r, TestWorlds.Flat(r, 64, 64, 40));
        var dirt = new ItemStack(r.ItemId("dirt"), 1);
        int old = sim.SpawnDroppedItem(dirt, new Vector2(10f, 39.625f), Vector2.Zero, 0, ageTicks: 50);
        int fresh = sim.SpawnDroppedItem(dirt, new Vector2(50f, 39.625f), Vector2.Zero, 0);

        for (int i = 0; i < 11; i++) sim.Step();

        Assert.False(sim.TryGetEntityView(old, out _));
        Assert.True(sim.TryGetEntityView(fresh, out _));
    }

    [Fact]
    public void SnapshotDroppedItems_ReturnsStateOrderedBySpawn()
    {
        var r = TestData.Registry();
        using var sim = new Simulation(r, TestWorlds.Flat(r, 64, 64, 40));
        sim.SpawnDroppedItem(new ItemStack(r.ItemId("stone"), 3), new Vector2(30f, 39.625f), Vector2.Zero, 0, ageTicks: 7);
        sim.SpawnDroppedItem(new ItemStack(r.ItemId("dirt"), 5), new Vector2(10f, 39.625f), Vector2.Zero, 0, ageTicks: 9);

        var snapshot = sim.SnapshotDroppedItems();

        Assert.Equal(2, snapshot.Count);
        Assert.Equal((new ItemStack(r.ItemId("stone"), 3), new Vector2(30f, 39.625f), 7), snapshot[0]);
        Assert.Equal((new ItemStack(r.ItemId("dirt"), 5), new Vector2(10f, 39.625f), 9), snapshot[1]);
    }
}
