using System.Numerics;
using Zyon.Core.Data;
using Zyon.Core.Items;
using Zyon.Core.Persistence;
using Zyon.Core.WorldGen;

namespace Zyon.Core.Tests;

public class WorldSaveTests
{
    private static readonly WorldInfo Info = new(Seed: 9, GeneratorVersion: 1, ConfigFingerprint: 42UL, SpawnFeet: new Vector2(20f, 40f));

    [Fact]
    public void Capture_RecordsTilesTickItemsAndPlayers()
    {
        using var h = new SimHarness();
        h.Sim.SpawnDroppedItem(new ItemStack(h.Registry.ItemId("stone"), 4), new Vector2(50.5f, 39.625f), Vector2.Zero, 0, ageTicks: 30);
        h.Idle(10);

        var save = WorldSave.Capture(h.Sim, Info, new Dictionary<string, int> { ["Leo"] = h.PlayerId });

        Assert.Same(h.Tiles, save.Tiles);
        Assert.Equal(10, save.Tick);
        var item = Assert.Single(save.DroppedItems);
        Assert.Equal("stone", item.Item);
        Assert.Equal(4, item.Count);
        Assert.Equal(40, item.AgeTicks);
        Assert.Equal(h.PlayerPosition, save.PlayerPositions["Leo"]);
    }

    [Fact]
    public void CreateSimulation_RestoresTickAndItems()
    {
        var r = TestData.Registry();
        var save = new WorldSave(Info, TestWorlds.Flat(r, 64, 64, 40), 777,
            [new SavedDroppedItem("dirt", 3, new Vector2(30.5f, 39.625f), 12, 33)],
            new Dictionary<string, Vector2>());

        using var sim = save.CreateSimulation(r);

        Assert.Equal(777, sim.Tick);
        var restored = Assert.Single(sim.SnapshotDroppedItems());
        Assert.Equal(new ItemStack(r.ItemId("dirt"), 3), restored.Stack);
        Assert.Equal(new Vector2(30.5f, 39.625f), restored.Position);
        Assert.Equal(12, restored.AgeTicks);
        Assert.Equal(33, restored.PickupDelayTicks);
    }

    [Fact]
    public void CreateSimulation_UnknownItem_Throws()
    {
        var r = TestData.Registry();
        var save = new WorldSave(Info, TestWorlds.Flat(r, 64, 64, 40), 0,
            [new SavedDroppedItem("mithril", 1, new Vector2(30.5f, 39.625f), 0, 0)],
            new Dictionary<string, Vector2>());

        var ex = Assert.Throws<SaveIncompatibleException>(() => save.CreateSimulation(r));
        Assert.Contains("mithril", ex.Message);
    }

    [Fact]
    public void PlayerPosition_UsesSavedPosition_OrFallsBackToSpawn()
    {
        var physics = TestData.Registry().Physics;
        var save = new WorldSave(Info, new Zyon.Core.Tiles.TileWorld(64, 64), 0, [],
            new Dictionary<string, Vector2> { ["Leo"] = new Vector2(5f, 6f) });

        Assert.Equal(new Vector2(5f, 6f), save.PlayerPosition("Leo", physics));
        Assert.Equal(new Vector2(20f, 40f - physics.PlayerHeight / 2), save.PlayerPosition("Ana", physics));
    }

    [Fact]
    public void FromGenerated_StartsAtTickZeroWithWorldInfo()
    {
        var gen = new WorldGenerator(TestData.Registry(), TestData.WorldGenConfig()).Generate(128, 128, 5);

        var save = WorldSave.FromGenerated(gen);

        Assert.Same(gen.Tiles, save.Tiles);
        Assert.Equal(0, save.Tick);
        Assert.Equal(new WorldInfo(5, gen.GeneratorVersion, gen.ConfigFingerprint, gen.SpawnFeet), save.Info);
        Assert.Empty(save.DroppedItems);
    }

    [Fact]
    public void Capture_KeepsPreviousPositionsOfAbsentPlayers()
    {
        using var h = new SimHarness();

        var save = WorldSave.Capture(h.Sim, Info, new Dictionary<string, int> { ["Leo"] = h.PlayerId },
            new Dictionary<string, Vector2> { ["Ana"] = new Vector2(5f, 6f) });

        Assert.Equal(new Vector2(5f, 6f), save.PlayerPositions["Ana"]);
        Assert.Equal(h.PlayerPosition, save.PlayerPositions["Leo"]);
    }

    [Fact]
    public void CreateSimulation_InvalidCount_Throws()
    {
        var r = TestData.Registry();
        var save = new WorldSave(Info, TestWorlds.Flat(r, 64, 64, 40), 0,
            [new SavedDroppedItem("dirt", 0, new Vector2(30.5f, 39.625f), 0, 0)],
            new Dictionary<string, Vector2>());

        var ex = Assert.Throws<DataException>(() => save.CreateSimulation(r));
        Assert.Contains(".zworld", ex.Message);
        Assert.Contains("dirt", ex.Message);
    }

    [Fact]
    public void ThrownItem_KeepsPickupDelayAcrossSaveAndLoad()
    {
        using var h = new SimHarness();
        h.Send(new Zyon.Core.Sim.DropItem(h.PlayerId, 0));
        h.Idle(1);
        var names = new Dictionary<string, int> { ["Leo"] = h.PlayerId };
        var world = WorldSave.Capture(h.Sim, Info, names);
        var player = PlayerSave.Capture(h.Sim, h.PlayerId, "Leo");

        using var sim2 = world.CreateSimulation(h.Registry);
        int id2 = player.SpawnInto(sim2, world.PlayerPosition("Leo", h.Registry.Physics));
        for (int i = 0; i < 3; i++) sim2.Step();

        Assert.True(sim2.GetInventory(id2)![0].IsEmpty);
        Assert.Single(sim2.SnapshotDroppedItems());
    }
}
