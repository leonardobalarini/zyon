using Zyon.Core.Data;

namespace Zyon.Core.Items;

public interface IReadOnlyInventory
{
    int SlotCount { get; }
    ItemStack this[int slot] { get; }
}

/// <summary>50 slots: 0–9 hotbar, 10–49 mochila.</summary>
public sealed class Inventory(Registry registry) : IReadOnlyInventory
{
    public const int Slots = 50;
    public const int HotbarSize = 10;

    private readonly ItemStack[] slots = new ItemStack[Slots];

    public int SlotCount => Slots;

    public ItemStack this[int slot] => slots[slot];

    public static bool IsValidSlot(int slot) => (uint)slot < Slots;

    public void Set(int slot, ItemStack stack) => slots[slot] = stack.IsEmpty ? ItemStack.Empty : stack;

    public bool CanAccept(ushort itemId)
    {
        int max = registry.Item(itemId).MaxStack;
        foreach (var s in slots)
            if (s.IsEmpty || (s.ItemId == itemId && s.Count < max))
                return true;
        return false;
    }

    /// <summary>Adiciona completando pilhas existentes e depois slots vazios (hotbar primeiro). Retorna a sobra.</summary>
    public int Add(ushort itemId, int count, ICollection<int>? changed = null)
    {
        if (itemId == 0 || count <= 0) return Math.Max(count, 0);
        int max = registry.Item(itemId).MaxStack;
        int remaining = count;

        for (int i = 0; i < Slots && remaining > 0; i++)
        {
            var s = slots[i];
            if (s.IsEmpty || s.ItemId != itemId || s.Count >= max) continue;
            int n = Math.Min(max - s.Count, remaining);
            slots[i] = s with { Count = s.Count + n };
            remaining -= n;
            changed?.Add(i);
        }

        for (int i = 0; i < Slots && remaining > 0; i++)
        {
            if (!slots[i].IsEmpty) continue;
            int n = Math.Min(max, remaining);
            slots[i] = new ItemStack(itemId, n);
            remaining -= n;
            changed?.Add(i);
        }

        return remaining;
    }

    /// <summary>Mesmo item: funde até maxStack (excedente fica na origem). Caso contrário: troca.</summary>
    public bool Move(int from, int to, ICollection<int>? changed = null)
    {
        if (!IsValidSlot(from) || !IsValidSlot(to) || from == to) return false;
        var a = slots[from];
        if (a.IsEmpty) return false;
        var b = slots[to];

        if (!b.IsEmpty && b.ItemId == a.ItemId)
        {
            int max = registry.Item(a.ItemId).MaxStack;
            int n = Math.Min(max - b.Count, a.Count);
            if (n <= 0) return false;
            slots[to] = b with { Count = b.Count + n };
            slots[from] = a.Count - n == 0 ? ItemStack.Empty : a with { Count = a.Count - n };
        }
        else
        {
            slots[from] = b;
            slots[to] = a;
        }

        changed?.Add(from);
        changed?.Add(to);
        return true;
    }

    public ItemStack TakeAll(int slot)
    {
        var s = slots[slot];
        slots[slot] = ItemStack.Empty;
        return s;
    }

    public bool ConsumeOne(int slot)
    {
        var s = slots[slot];
        if (s.IsEmpty) return false;
        slots[slot] = s.Count == 1 ? ItemStack.Empty : s with { Count = s.Count - 1 };
        return true;
    }
}
