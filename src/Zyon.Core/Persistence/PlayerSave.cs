using System.Numerics;
using Zyon.Core.Data;
using Zyon.Core.Items;
using Zyon.Core.Sim;

namespace Zyon.Core.Persistence;

/// <summary>Um slot ocupado do inventário salvo, com o item referenciado pelo nome.</summary>
public sealed record SavedSlot(int Slot, string Item, int Count);

/// <summary>Personagem independente de mundo: nome, slot selecionado e inventário.</summary>
public sealed record PlayerSave(string Name, int SelectedSlot, IReadOnlyList<SavedSlot> Slots)
{
    /// <summary>Personagem novo com os itens iniciais do Registry.</summary>
    public static PlayerSave New(string name, Registry registry)
    {
        var inventory = new Inventory(registry);
        foreach (var stack in registry.StarterItems)
            inventory.Add(stack.ItemId, stack.Count);
        return new PlayerSave(name, 0, ToSlots(inventory, registry));
    }

    public static PlayerSave Capture(Simulation sim, int playerId, string name)
    {
        var inventory = sim.GetInventory(playerId)
                        ?? throw new ArgumentException($"Jogador {playerId} não existe", nameof(playerId));
        return new PlayerSave(name, sim.GetSelectedSlot(playerId), ToSlots(inventory, sim.Registry));
    }

    /// <summary>Cria o jogador na simulação com este inventário (sem itens iniciais). Os nomes já foram validados na leitura.</summary>
    public int SpawnInto(Simulation sim, Vector2 position)
    {
        var inventory = new Inventory(sim.Registry);
        foreach (var slot in Slots)
            inventory.Set(slot.Slot, new ItemStack(sim.Registry.ItemId(slot.Item), slot.Count));
        return sim.SpawnPlayer(position, inventory, SelectedSlot);
    }

    private static List<SavedSlot> ToSlots(IReadOnlyInventory inventory, Registry registry)
    {
        var slots = new List<SavedSlot>();
        for (int i = 0; i < inventory.SlotCount; i++)
        {
            var stack = inventory[i];
            if (!stack.IsEmpty)
                slots.Add(new SavedSlot(i, registry.Item(stack.ItemId).Name, stack.Count));
        }
        return slots;
    }
}
