using System.Globalization;
using System.Numerics;
using System.Text;
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
        return new PlayerSave(ValidateName(name), 0, ToSlots(inventory, registry));
    }

    public const int MaxNameLength = SaveNames.MaxLength;

    /// <summary>
    /// Valida o nome do personagem (usado como nome de arquivo) e devolve-o normalizado (NFC) e sem espaços nas pontas.
    /// Use sempre o valor devolvido.
    /// </summary>
    public static string ValidateName(string name) =>
        SaveNames.Validate(name, "nome do personagem", PlayerFile.Extension);

    /// <summary>
    /// Valida slot selecionado (na hotbar), slots (válidos e únicos), itens (existentes) e quantidades (1..MaxStack)
    /// contra o Registry. Item que sumiu dos dados lança SaveIncompatibleException; o resto, DataException.
    /// </summary>
    public void Validate(Registry registry)
    {
        if ((uint)SelectedSlot >= Inventory.HotbarSize)
            throw Fail($"'selectedSlot' {SelectedSlot} fora da hotbar (0..{Inventory.HotbarSize - 1})");
        var seen = new HashSet<int>();
        for (int i = 0; i < Slots.Count; i++)
        {
            var ctx = $"inventory[{i}]";
            var entry = Slots[i] ?? throw Fail($"{ctx}: entrada nula");
            if (!Inventory.IsValidSlot(entry.Slot)) throw Fail($"{ctx}: slot {entry.Slot} inválido");
            if (!seen.Add(entry.Slot)) throw Fail($"{ctx}: slot {entry.Slot} repetido");
            if (!registry.TryGetItemId(entry.Item ?? "", out var itemId))
                throw new SaveIncompatibleException($"{PlayerFile.Extension}: {ctx}: item '{entry.Item}' não existe mais nos dados do jogo");
            int max = registry.Item(itemId).MaxStack;
            if (entry.Count < 1 || entry.Count > max)
                throw Fail($"{ctx}: quantidade {entry.Count} fora de 1..{max}");
        }
    }

    private static DataException Fail(string message) => new($"{PlayerFile.Extension}: {message}");

    public static PlayerSave Capture(Simulation sim, int playerId, string name)
    {
        var inventory = sim.GetInventory(playerId)
                        ?? throw new ArgumentException($"Jogador {playerId} não existe", nameof(playerId));
        return new PlayerSave(ValidateName(name), sim.GetSelectedSlot(playerId), ToSlots(inventory, sim.Registry));
    }

    /// <summary>Cria o jogador na simulação com este inventário (sem itens iniciais). Os nomes já foram validados na leitura.</summary>
    public int SpawnInto(Simulation sim, Vector2 position)
    {
        Validate(sim.Registry);
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
