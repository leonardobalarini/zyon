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
        return new PlayerSave(ValidateName(name), 0, ToSlots(inventory, registry));
    }

    public const int MaxNameLength = 24;

    private static readonly string[] ReservedNames =
        ["CON", "PRN", "AUX", "NUL", .. Enumerable.Range(1, 9).Select(i => $"COM{i}"), .. Enumerable.Range(1, 9).Select(i => $"LPT{i}")];

    /// <summary>Valida o nome do personagem (usado como nome de arquivo) e devolve-o sem espaços nas pontas.</summary>
    public static string ValidateName(string name)
    {
        string trimmed = (name ?? "").Trim();
        if (trimmed.Length == 0) throw Fail("nome do personagem ('name') vazio");
        if (trimmed.Length > MaxNameLength) throw Fail($"nome do personagem ('name') com mais de {MaxNameLength} caracteres");
        if (trimmed.Any(char.IsControl)) throw Fail("nome do personagem ('name') contém caracteres de controle");
        if (trimmed.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || trimmed.IndexOfAny("/\\:*?\"<>|".ToCharArray()) >= 0)
            throw Fail("nome do personagem ('name') contém caracteres inválidos");
        if (trimmed.EndsWith('.')) throw Fail("nome do personagem ('name') não pode terminar com ponto");
        string stem = trimmed.Split('.')[0].TrimEnd();
        if (ReservedNames.Contains(stem, StringComparer.OrdinalIgnoreCase))
            throw Fail($"nome do personagem ('name') '{trimmed}' é reservado pelo sistema");
        return trimmed;
    }

    /// <summary>Valida slots (válidos e únicos), itens (existentes) e quantidades (1..MaxStack) contra o Registry.</summary>
    public void Validate(Registry registry)
    {
        var seen = new HashSet<int>();
        for (int i = 0; i < Slots.Count; i++)
        {
            var ctx = $"inventory[{i}]";
            var entry = Slots[i] ?? throw Fail($"{ctx}: entrada nula");
            if (!Inventory.IsValidSlot(entry.Slot)) throw Fail($"{ctx}: slot {entry.Slot} inválido");
            if (!seen.Add(entry.Slot)) throw Fail($"{ctx}: slot {entry.Slot} repetido");
            if (!registry.TryGetItemId(entry.Item ?? "", out var itemId))
                throw Fail($"{ctx}: item '{entry.Item}' não existe mais nos dados do jogo");
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
        return new PlayerSave(name, sim.GetSelectedSlot(playerId), ToSlots(inventory, sim.Registry));
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
