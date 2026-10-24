using Zyon.Core.Entities;
using Zyon.Core.Items;
using Zyon.Core.Sim;

namespace Zyon.Core.Tests;

public class EntityViewTests
{
    [Fact]
    public void Player_ExposesAnimationState()
    {
        using var h = new SimHarness();
        ushort pickaxe = h.Registry.ItemId("basic_pickaxe");
        h.Sim.TryGetEntityView(h.PlayerId, out var standing);
        Assert.Equal(pickaxe, standing.HeldItemId);     // slot 0 selecionado com a picareta
        Assert.False(standing.UsingItem);

        // Anda para a direita segurando o botão de uso mirando 3 tiles à frente.
        var p = standing.Position;
        h.Run(30, new PlayerInput(1f, false, true, (int)p.X + 3, (int)p.Y));
        h.Sim.TryGetEntityView(h.PlayerId, out var moving);

        Assert.True(moving.Velocity.X > 0);
        Assert.True(moving.OnGround);
        Assert.Equal(1, moving.Facing);
        Assert.True(moving.UsingItem);
        Assert.Equal((int)p.X + 3, moving.AimX);
        Assert.Equal((int)p.Y, moving.AimY);
    }

    [Fact]
    public void WalkingLeft_FacesLeft()
    {
        using var h = new SimHarness();

        h.Run(30, new PlayerInput(-1f, false, false, 0, 0));
        h.Sim.TryGetEntityView(h.PlayerId, out var moving);

        Assert.True(moving.Velocity.X < 0);
        Assert.Equal(-1, moving.Facing);
    }

    [Fact]
    public void EmptySelectedSlot_HoldsNothing()
    {
        using var h = new SimHarness();
        int empty = Inventory.HotbarSize - 1;
        Assert.True(h.Sim.GetInventory(h.PlayerId)![empty].IsEmpty);

        h.Send(new SelectHotbarSlot(h.PlayerId, empty));
        h.Sim.TryGetEntityView(h.PlayerId, out var view);

        Assert.Equal((ushort)0, view.HeldItemId);
    }

    [Fact]
    public void DroppedItem_HasNoPlayerState()
    {
        using var h = new SimHarness();
        h.Send(new DropItem(h.PlayerId, 0));
        var items = new List<EntityView>();
        h.Sim.CopyEntities(items);
        var item = Assert.Single(items, e => e.Kind == EntityKind.DroppedItem);

        Assert.Equal((ushort)0, item.HeldItemId);
        Assert.False(item.UsingItem);
    }
}
