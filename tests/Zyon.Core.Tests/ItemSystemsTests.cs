using System.Numerics;
using Zyon.Core.Items;
using Zyon.Core.Sim;

namespace Zyon.Core.Tests;

public class ItemSystemsTests
{
    private static ItemStack Dirt(SimHarness h, int count = 1) => new(h.Registry.ItemId("dirt"), count);

    private static float RestingY(SimHarness h) => h.GroundY - h.Registry.Physics.ItemSize / 2;

    [Fact]
    public void DroppedItem_FallsAndRestsOnGround()
    {
        using var h = new SimHarness();
        int id = h.Sim.SpawnDroppedItem(Dirt(h), new Vector2(45f, 30f), Vector2.Zero, 0);

        h.Idle(120);

        Assert.True(h.Sim.TryGetEntityView(id, out var view));
        Assert.Equal(RestingY(h), view.Position.Y, 3);
    }

    [Fact]
    public void ItemNearPlayer_IsPickedUp()
    {
        using var h = new SimHarness();
        int id = h.Sim.SpawnDroppedItem(Dirt(h), new Vector2(22f, RestingY(h)), Vector2.Zero, 0);
        h.Sim.DrainEvents();

        h.Idle(60);

        Assert.False(h.Sim.TryGetEntityView(id, out _));
        Assert.Equal(Dirt(h), h.Inventory[1]);
        var events = h.Sim.DrainEvents();
        Assert.Contains(new EntityDespawned(id), events);
        Assert.Contains(events.OfType<InventoryChanged>(), e => e.Slots.Contains(1));
    }

    [Fact]
    public void ItemFarFromPlayer_IsNotPickedUp()
    {
        using var h = new SimHarness();
        int id = h.Sim.SpawnDroppedItem(Dirt(h), new Vector2(40f, RestingY(h)), Vector2.Zero, 0);

        h.Idle(60);

        Assert.True(h.Sim.TryGetEntityView(id, out _));
    }

    [Fact]
    public void PickupDelay_IsRespected()
    {
        using var h = new SimHarness();
        int id = h.Sim.SpawnDroppedItem(Dirt(h), new Vector2(20f, RestingY(h)), Vector2.Zero, 90);

        h.Idle(30);
        Assert.True(h.Sim.TryGetEntityView(id, out _));

        h.Idle(90);
        Assert.False(h.Sim.TryGetEntityView(id, out _));
    }

    [Fact]
    public void FullInventory_LeavesItemOnGround()
    {
        using var h = new SimHarness();
        for (int i = 1; i < Inventory.Slots; i++)
            h.Inventory.Set(i, new ItemStack(h.Registry.ItemId("basic_pickaxe"), 1));
        int id = h.Sim.SpawnDroppedItem(Dirt(h), new Vector2(21f, RestingY(h)), Vector2.Zero, 0);

        h.Idle(60);

        Assert.True(h.Sim.TryGetEntityView(id, out _));
    }

    [Fact]
    public void NearbyEqualItems_Merge()
    {
        using var h = new SimHarness();
        int a = h.Sim.SpawnDroppedItem(Dirt(h, 2), new Vector2(45f, RestingY(h)), Vector2.Zero, 0);
        int b = h.Sim.SpawnDroppedItem(Dirt(h, 3), new Vector2(45.5f, RestingY(h)), Vector2.Zero, 0);

        h.Idle(h.Registry.Physics.ItemMergeIntervalTicks + 1);

        // Qualquer um dos dois pode absorver o outro, dependendo da ordem de iteração do ECS.
        bool aAlive = h.Sim.TryGetEntityView(a, out var viewA);
        bool bAlive = h.Sim.TryGetEntityView(b, out var viewB);
        Assert.True(aAlive ^ bAlive, "deveria sobrar exatamente uma pilha");
        Assert.Equal(5, (aAlive ? viewA : viewB).Item.Count);
    }

    [Fact]
    public void DistantItems_DoNotMerge()
    {
        using var h = new SimHarness();
        int a = h.Sim.SpawnDroppedItem(Dirt(h), new Vector2(45f, RestingY(h)), Vector2.Zero, 0);
        int b = h.Sim.SpawnDroppedItem(Dirt(h), new Vector2(48f, RestingY(h)), Vector2.Zero, 0);

        h.Idle(h.Registry.Physics.ItemMergeIntervalTicks + 1);

        Assert.True(h.Sim.TryGetEntityView(a, out _));
        Assert.True(h.Sim.TryGetEntityView(b, out _));
    }

    [Fact]
    public void OldItems_Despawn()
    {
        var registry = TestData.RegistryWithPhysics("""{"itemDespawnSeconds": 1}""");
        using var h = new SimHarness(registry: registry);
        int id = h.Sim.SpawnDroppedItem(Dirt(h), new Vector2(45f, RestingY(h)), Vector2.Zero, 0);

        h.Idle(Simulation.TicksPerSecond + 1);

        Assert.False(h.Sim.TryGetEntityView(id, out _));
    }

    [Fact]
    public void DroppedItem_IsRecollectedAfterDelay()
    {
        using var h = new SimHarness();
        h.Send(new DropItem(h.PlayerId, 0));
        Assert.True(h.Inventory[0].IsEmpty);

        // O impulso joga o item ~3 tiles à frente, fora do raio do ímã: o jogador anda até ele.
        h.Idle(h.Registry.Physics.DropPickupDelayTicks);
        h.Run(30, new PlayerInput(1f, false, false, 0, 0));
        h.Idle(60);

        Assert.Equal(h.Registry.ItemId("basic_pickaxe"), h.Inventory[0].ItemId);
    }

    [Fact]
    public void ItemInsidePlacedBlock_IsPushedOut()
    {
        using var h = new SimHarness();
        int id = h.Sim.SpawnDroppedItem(Dirt(h), new Vector2(45.5f, RestingY(h)), Vector2.Zero, 0);
        h.Idle(5);

        h.Tiles.SetBlock(45, (int)MathF.Floor(RestingY(h)), h.Registry.BlockId("dirt"));
        h.Idle(5);

        Assert.True(h.Sim.TryGetEntityView(id, out var view));
        Assert.False(h.Sim.Collider.BoxHasSolid(view.Position, view.Size));
        h.Idle(60);
        Assert.True(h.Sim.TryGetEntityView(id, out view));
        Assert.Equal((int)MathF.Floor(RestingY(h)) - h.Registry.Physics.ItemSize / 2, view.Position.Y, 3);
    }

    [Fact]
    public void ItemInsidePlacedBlock_NearPlayer_IsCollected()
    {
        using var h = new SimHarness();
        int id = h.Sim.SpawnDroppedItem(Dirt(h), new Vector2(22.5f, RestingY(h)), Vector2.Zero, 0);

        h.Tiles.SetBlock(22, (int)MathF.Floor(RestingY(h)), h.Registry.BlockId("dirt"));
        h.Idle(60);

        Assert.False(h.Sim.TryGetEntityView(id, out _));
        Assert.Equal(Dirt(h), h.Inventory[1]);
    }

    [Fact]
    public void MagnetBlockedByWall_ItemSettles()
    {
        using var h = new SimHarness();
        var stone = h.Registry.BlockId("stone");
        for (int y = 0; y < h.GroundY; y++) h.Tiles.SetBlock(21, y, stone);
        int id = h.Sim.SpawnDroppedItem(Dirt(h), new Vector2(22.6f, RestingY(h)), Vector2.Zero, 0);

        h.Idle(300);
        float min = float.MaxValue, max = float.MinValue;
        for (int i = 0; i < 60; i++)
        {
            h.Idle(1);
            Assert.True(h.Sim.TryGetEntityView(id, out var v));
            min = MathF.Min(min, v.Position.Y);
            max = MathF.Max(max, v.Position.Y);
        }
        Assert.True(max - min < 0.05f, $"oscilou {max - min}");
    }

    [Fact]
    public void PartialPickup_LeavesRemainderOnGround()
    {
        using var h = new SimHarness();
        var pick = new ItemStack(h.Registry.ItemId("basic_pickaxe"), 1);
        for (int i = 0; i < Inventory.Slots - 1; i++) h.Inventory.Set(i, pick);
        h.Inventory.Set(Inventory.Slots - 1, Dirt(h, 997));
        int id = h.Sim.SpawnDroppedItem(Dirt(h, 5), new Vector2(21f, RestingY(h)), Vector2.Zero, 0);

        h.Idle(60);

        Assert.Equal(999, h.Inventory[Inventory.Slots - 1].Count);
        Assert.True(h.Sim.TryGetEntityView(id, out var view));
        Assert.Equal(3, view.Item.Count);
    }

    [Fact]
    public void Merge_RespectsMaxStack()
    {
        using var h = new SimHarness();
        int a = h.Sim.SpawnDroppedItem(Dirt(h, 990), new Vector2(45f, RestingY(h)), Vector2.Zero, 0);
        int b = h.Sim.SpawnDroppedItem(Dirt(h, 20), new Vector2(45.5f, RestingY(h)), Vector2.Zero, 0);

        h.Idle(h.Registry.Physics.ItemMergeIntervalTicks + 1);

        Assert.True(h.Sim.TryGetEntityView(a, out var va));
        Assert.True(h.Sim.TryGetEntityView(b, out var vb));
        Assert.Equal(new[] { 11, 999 }, new[] { va.Item.Count, vb.Item.Count }.OrderBy(x => x).ToArray());
    }

    [Fact]
    public void MagnetPullsItemUpOneTileLedge()
    {
        using var h = new SimHarness(playerX: 10f);
        var dirt = h.Registry.BlockId("dirt");
        for (int x = 12; x <= 21; x++) h.Tiles.SetBlock(x, h.GroundY - 1, dirt);
        for (int i = 0; i < 300 && h.PlayerPosition.X < 20.5f; i++)
            h.Run(1, new PlayerInput(1f, false, false, 0, 0));
        h.Idle(30);
        Assert.Equal(h.GroundY - 1 - h.Registry.Physics.PlayerHeight / 2, h.PlayerPosition.Y, 2);

        int id = h.Sim.SpawnDroppedItem(Dirt(h), new Vector2(h.PlayerPosition.X + 2.2f, RestingY(h)), Vector2.Zero, 0);
        h.Idle(60);

        Assert.False(h.Sim.TryGetEntityView(id, out _));
        Assert.Equal(Dirt(h), h.Inventory[1]);
    }
}
