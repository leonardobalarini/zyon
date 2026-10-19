using System.Text.Json;
using Zyon.Core.Data;
using Zyon.Core.Items;

namespace Zyon.Core.Persistence;

/// <summary>Formato .zplayer: JSON legível com itens referenciados pelo nome.</summary>
public static class PlayerFile
{
    public const int FormatVersion = 1;
    public const string Extension = ".zplayer";

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static void Write(Stream stream, PlayerSave save)
    {
        var dto = new PlayerDto
        {
            FormatVersion = FormatVersion,
            Name = save.Name,
            SelectedSlot = save.SelectedSlot,
            Inventory = save.Slots.Select(s => (SlotDto?)new SlotDto { Slot = s.Slot, Item = s.Item, Count = s.Count }).ToList(),
        };
        JsonSerializer.Serialize(stream, dto, WriteOptions);
    }

    /// <summary>Lê e valida contra o Registry atual (itens inexistentes, slots e quantidades inválidos falham).</summary>
    public static PlayerSave Read(Stream stream, Registry registry)
    {
        PlayerDto dto;
        try
        {
            dto = JsonSerializer.Deserialize<PlayerDto>(stream, RegistryLoader.Options) ?? throw Fail("conteúdo vazio");
        }
        catch (JsonException ex)
        {
            throw Fail($"JSON inválido — {ex.Message}");
        }

        int version = dto.FormatVersion ?? throw Fail("campo 'formatVersion' ausente");
        if (version > FormatVersion)
            throw Fail($"feito por uma versão mais nova do jogo (formato {version}, suportado até {FormatVersion})");
        if (version < 1)
            throw Fail($"versão de formato {version} não suportada");
        // Migrações futuras entram aqui, em sequência: v1 → v2 → ... até FormatVersion.

        string name = PlayerSave.ValidateName(dto.Name ?? throw Fail("campo 'name' ausente"));
        int selected = dto.SelectedSlot ?? throw Fail("campo 'selectedSlot' ausente");
        if ((uint)selected >= Inventory.HotbarSize)
            throw Fail($"'selectedSlot' {selected} fora da hotbar (0..{Inventory.HotbarSize - 1})");

        var entries = dto.Inventory ?? throw Fail("campo 'inventory' ausente");
        var slots = new List<SavedSlot>();
        for (int i = 0; i < entries.Count; i++)
        {
            var ctx = $"inventory[{i}]";
            var entry = entries[i] ?? throw Fail($"{ctx}: entrada nula");
            slots.Add(new SavedSlot(
                entry.Slot ?? throw Fail($"{ctx}: campo 'slot' ausente"),
                entry.Item ?? throw Fail($"{ctx}: campo 'item' ausente"),
                entry.Count ?? throw Fail($"{ctx}: campo 'count' ausente")));
        }

        var save = new PlayerSave(name, selected, slots);
        save.Validate(registry);
        return save;
    }

    private static DataException Fail(string message) => new($"{Extension}: {message}");

    private sealed class PlayerDto
    {
        public int? FormatVersion { get; set; }
        public string? Name { get; set; }
        public int? SelectedSlot { get; set; }
        public List<SlotDto?>? Inventory { get; set; }
    }

    private sealed class SlotDto
    {
        public int? Slot { get; set; }
        public string? Item { get; set; }
        public int? Count { get; set; }
    }
}
