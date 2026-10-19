using System.Globalization;
using System.Text.Json;
using Zyon.Core.Data;

namespace Zyon.Client;

public readonly record struct Rgb(byte R, byte G, byte B)
{
    public static bool TryParse(string? hex, out Rgb color)
    {
        color = default;
        if (hex is null || hex.Length != 7 || hex[0] != '#') return false;
        if (!uint.TryParse(hex.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint v)) return false;
        color = new Rgb((byte)(v >> 16), (byte)(v >> 8), (byte)v);
        return true;
    }
}

/// <summary>Cores provisórias por id (até existir arte). Lidas de data/client/visuals.json.</summary>
public sealed class ClientVisuals
{
    public const string FileName = "visuals.json";
    public static readonly Rgb FallbackColor = new(200, 0, 200);

    private readonly Rgb?[] itemColors;

    private ClientVisuals(Rgb?[] blocks, Rgb?[] walls, Rgb?[] items, Rgb player, Rgb sky)
    {
        BlockColors = blocks;
        WallColors = walls;
        itemColors = items;
        Player = player;
        Sky = sky;
    }

    public IReadOnlyList<Rgb?> BlockColors { get; }
    public IReadOnlyList<Rgb?> WallColors { get; }
    public Rgb Player { get; }
    public Rgb Sky { get; }

    public Rgb ItemColor(ushort itemId) => itemColors[itemId] ?? FallbackColor;

    public static ClientVisuals LoadFromDirectory(string dataDir, Registry registry)
    {
        var path = Path.Combine(dataDir, "client", FileName);
        if (!File.Exists(path)) throw new DataException($"{FileName}: arquivo não encontrado em '{path}'");
        return Load(File.ReadAllText(path), registry);
    }

    public static ClientVisuals Load(string json, Registry registry)
    {
        Dto dto;
        try
        {
            dto = JsonSerializer.Deserialize<Dto>(json, RegistryOptions) ?? throw Fail("conteúdo vazio");
        }
        catch (JsonException ex)
        {
            throw Fail($"JSON inválido — {ex.Message}");
        }

        var blocks = new Rgb?[registry.Blocks.Count];
        foreach (var (name, hex) in dto.Blocks ?? new Dictionary<string, string>())
            blocks[registry.TryGetBlockId(name, out var id) ? id : throw Fail($"bloco '{name}' não existe")] = Parse(hex, $"blocks.{name}");
        for (int id = 1; id < blocks.Length; id++)
            if (blocks[id] is null) throw Fail($"falta a cor do bloco '{registry.Block((ushort)id).Name}'");

        var walls = new Rgb?[registry.Walls.Count];
        foreach (var (name, hex) in dto.Walls ?? new Dictionary<string, string>())
            walls[registry.TryGetWallId(name, out var id) ? id : throw Fail($"parede '{name}' não existe")] = Parse(hex, $"walls.{name}");
        for (int id = 1; id < walls.Length; id++)
            if (walls[id] is null) throw Fail($"falta a cor da parede '{registry.Wall((ushort)id).Name}'");

        var items = new Rgb?[registry.Items.Count];
        foreach (var (name, hex) in dto.Items ?? new Dictionary<string, string>())
            items[registry.TryGetItemId(name, out var id) ? id : throw Fail($"item '{name}' não existe")] = Parse(hex, $"items.{name}");
        for (int id = 1; id < items.Length; id++)
        {
            var def = registry.Item((ushort)id);
            if (items[id] is null && def.PlacesBlockId != 0)
                items[id] = blocks[def.PlacesBlockId];
        }

        return new ClientVisuals(blocks, walls, items, Parse(dto.Player, "player"), Parse(dto.Sky, "sky"));
    }

    private static Rgb Parse(string? hex, string field) =>
        Rgb.TryParse(hex, out var c) ? c : throw Fail($"'{field}': cor inválida '{hex}' (use #rrggbb)");

    private static DataException Fail(string message) => new($"{FileName}: {message}");

    private static readonly JsonSerializerOptions RegistryOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,
    };

    private sealed class Dto
    {
        public Dictionary<string, string>? Blocks { get; set; }
        public Dictionary<string, string>? Walls { get; set; }
        public Dictionary<string, string>? Items { get; set; }
        public string? Player { get; set; }
        public string? Sky { get; set; }
    }
}
