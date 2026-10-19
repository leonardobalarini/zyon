using Zyon.Core.Data;
using Zyon.Core.Items;

namespace Zyon.Core.Tests;

public class InventoryTests
{
    private readonly Registry r = TestData.Registry();
    private ushort Dirt => r.ItemId("dirt");
    private ushort Stone => r.ItemId("stone");
    private ushort Pick => r.ItemId("basic_pickaxe");

    [Fact]
    public void Add_ToEmpty_FillsHotbarFirst()
    {
        var inv = new Inventory(r);
        var changed = new List<int>();

        int left = inv.Add(Dirt, 5, changed);

        Assert.Equal(0, left);
        Assert.Equal(new ItemStack(Dirt, 5), inv[0]);
        Assert.Equal(new[] { 0 }, changed);
    }

    [Fact]
    public void Add_CompletesExistingStackBeforeEmptySlot()
    {
        var inv = new Inventory(r);
        inv.Set(0, new ItemStack(Stone, 1));
        inv.Set(3, new ItemStack(Dirt, 10));

        inv.Add(Dirt, 5);

        Assert.Equal(new ItemStack(Dirt, 15), inv[3]);
        Assert.True(inv[1].IsEmpty);
    }

    [Fact]
    public void Add_OverflowsAcrossStacks()
    {
        var inv = new Inventory(r);
        inv.Set(0, new ItemStack(Dirt, 998));

        int left = inv.Add(Dirt, 5);

        Assert.Equal(0, left);
        Assert.Equal(999, inv[0].Count);
        Assert.Equal(new ItemStack(Dirt, 4), inv[1]);
    }

    [Fact]
    public void Add_WhenFull_ReturnsLeftover()
    {
        var inv = new Inventory(r);
        for (int i = 0; i < Inventory.Slots; i++)
            inv.Set(i, new ItemStack(Pick, 1));

        Assert.False(inv.CanAccept(Dirt));
        Assert.Equal(7, inv.Add(Dirt, 7));
    }

    [Fact]
    public void Add_RespectsMaxStackOfOne()
    {
        var inv = new Inventory(r);

        inv.Add(Pick, 2);

        Assert.Equal(new ItemStack(Pick, 1), inv[0]);
        Assert.Equal(new ItemStack(Pick, 1), inv[1]);
    }

    [Fact]
    public void Move_ToEmpty_MovesStack()
    {
        var inv = new Inventory(r);
        inv.Set(0, new ItemStack(Dirt, 3));
        var changed = new List<int>();

        Assert.True(inv.Move(0, 12, changed));

        Assert.True(inv[0].IsEmpty);
        Assert.Equal(new ItemStack(Dirt, 3), inv[12]);
        Assert.Equal(new[] { 0, 12 }, changed);
    }

    [Fact]
    public void Move_DifferentItems_Swaps()
    {
        var inv = new Inventory(r);
        inv.Set(0, new ItemStack(Dirt, 3));
        inv.Set(1, new ItemStack(Stone, 4));

        Assert.True(inv.Move(0, 1));

        Assert.Equal(new ItemStack(Stone, 4), inv[0]);
        Assert.Equal(new ItemStack(Dirt, 3), inv[1]);
    }

    [Fact]
    public void Move_SameItem_MergesAndKeepsExcessInSource()
    {
        var inv = new Inventory(r);
        inv.Set(0, new ItemStack(Dirt, 10));
        inv.Set(1, new ItemStack(Dirt, 995));

        Assert.True(inv.Move(0, 1));

        Assert.Equal(new ItemStack(Dirt, 6), inv[0]);
        Assert.Equal(new ItemStack(Dirt, 999), inv[1]);
    }

    [Fact]
    public void Move_Invalid_ReturnsFalse()
    {
        var inv = new Inventory(r);
        inv.Set(0, new ItemStack(Dirt, 1));
        inv.Set(1, new ItemStack(Dirt, 999));

        Assert.False(inv.Move(0, 0));
        Assert.False(inv.Move(0, 50));
        Assert.False(inv.Move(-1, 2));
        Assert.False(inv.Move(5, 6));     // origem vazia
        Assert.False(inv.Move(0, 1));     // destino cheio do mesmo item
    }

    [Fact]
    public void TakeAll_And_ConsumeOne()
    {
        var inv = new Inventory(r);
        inv.Set(2, new ItemStack(Dirt, 2));

        Assert.True(inv.ConsumeOne(2));
        Assert.Equal(1, inv[2].Count);
        Assert.True(inv.ConsumeOne(2));
        Assert.True(inv[2].IsEmpty);
        Assert.False(inv.ConsumeOne(2));

        inv.Set(4, new ItemStack(Stone, 9));
        Assert.Equal(new ItemStack(Stone, 9), inv.TakeAll(4));
        Assert.True(inv[4].IsEmpty);
    }
}
