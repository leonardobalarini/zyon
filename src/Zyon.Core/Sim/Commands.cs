namespace Zyon.Core.Sim;

public interface ICommand
{
    int PlayerId { get; }
}

public sealed record SelectHotbarSlot(int PlayerId, int Slot) : ICommand;

public sealed record MoveInventoryItem(int PlayerId, int From, int To) : ICommand;

public sealed record DropItem(int PlayerId, int Slot) : ICommand;
