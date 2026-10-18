namespace Zyon.Core.Items;

public readonly record struct ItemStack(ushort ItemId, int Count)
{
    public static ItemStack Empty => default;

    public bool IsEmpty => ItemId == 0 || Count <= 0;
}
