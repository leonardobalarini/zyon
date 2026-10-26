using System.Text.Json;
using System.Text.Json.Serialization;
using Zyon.Core.Items;

namespace Zyon.Core.Data;

public static class RegistryLoader
{
    public const string BlocksFile = "blocks.json";
    public const string WallsFile = "walls.json";
    public const string ItemsFile = "items.json";
    public const string PhysicsFile = "physics.json";
    public const string PlayerFile = "player.json";

    internal static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static Registry LoadFromDirectory(string dataDir)
    {
        string Read(string file)
        {
            var path = Path.Combine(dataDir, file);
            if (!File.Exists(path))
                throw new DataException($"{file}: arquivo não encontrado em '{dataDir}'");
            return File.ReadAllText(path);
        }

        return Load(Read(BlocksFile), Read(WallsFile), Read(ItemsFile), Read(PhysicsFile), Read(PlayerFile));
    }

    public static Registry Load(string blocksJson, string wallsJson, string itemsJson,
        string physicsJson, string playerJson)
    {
        var blockDtos = Parse<List<BlockDto>>(blocksJson, BlocksFile);
        var wallDtos = Parse<List<WallDto>>(wallsJson, WallsFile);
        var itemDtos = Parse<List<ItemDto>>(itemsJson, ItemsFile);
        var physics = Parse<PhysicsConfig>(physicsJson, PhysicsFile);
        var player = Parse<PlayerDto>(playerJson, PlayerFile);

        RequireNoNulls(blockDtos, BlocksFile);
        RequireNoNulls(wallDtos, WallsFile);
        RequireNoNulls(itemDtos, ItemsFile);

        var blockIds = AssignIds(blockDtos.Select(b => b.Id).ToList(), BlocksFile, "air");
        var wallIds = AssignIds(wallDtos.Select(w => w.Id).ToList(), WallsFile, "none");
        var itemIds = AssignIds(itemDtos.Select(i => i.Id).ToList(), ItemsFile, "none");

        var blocks = new BlockDef[blockDtos.Count + 1];
        blocks[0] = new BlockDef(0, "air", 0f, false, 0);
        for (int i = 0; i < blockDtos.Count; i++)
        {
            var d = blockDtos[i];
            var ctx = $"{BlocksFile}[{i}] ('{d.Id}')";
            float hardness = d.Hardness ?? throw Missing(ctx, "hardness");
            if (hardness < 0) throw new DataException($"{ctx}: campo 'hardness' não pode ser negativo");
            bool solid = d.Solid ?? throw Missing(ctx, "solid");
            ushort drop = d.Drops is null ? (ushort)0 : Resolve(itemIds, d.Drops, ctx, "drops");
            float light = d.Light ?? 0f;
            if (light < 0f || light > 1f || !float.IsFinite(light))
                throw new DataException($"{ctx}: campo 'light' deve estar entre 0 e 1");
            blocks[i + 1] = new BlockDef((ushort)(i + 1), d.Id!, hardness, solid, drop, light, d.Tree ?? false);
        }

        var walls = new WallDef[wallDtos.Count + 1];
        walls[0] = new WallDef(0, "none");
        for (int i = 0; i < wallDtos.Count; i++)
            walls[i + 1] = new WallDef((ushort)(i + 1), wallDtos[i].Id!);

        var items = new ItemDef[itemDtos.Count + 1];
        items[0] = new ItemDef(0, "none", "", 0, ItemKind.None, 0, 0f, 0f);
        for (int i = 0; i < itemDtos.Count; i++)
        {
            var d = itemDtos[i];
            var ctx = $"{ItemsFile}[{i}] ('{d.Id}')";
            string display = d.Name ?? throw Missing(ctx, "name");
            int maxStack = d.MaxStack ?? throw Missing(ctx, "maxStack");
            if (maxStack < 1) throw new DataException($"{ctx}: campo 'maxStack' deve ser >= 1");
            var kind = d.Kind switch
            {
                "Block" => ItemKind.Block,
                "Tool" => ItemKind.Tool,
                null => throw Missing(ctx, "kind"),
                _ => throw new DataException($"{ctx}: campo 'kind' inválido '{d.Kind}' (use Block ou Tool)"),
            };
            ushort places = 0;
            float power = 0f, speed = 0f;
            if (kind == ItemKind.Block)
            {
                places = Resolve(blockIds, d.PlacesBlock ?? throw Missing(ctx, "placesBlock"), ctx, "placesBlock");
            }
            else
            {
                power = d.Power ?? throw Missing(ctx, "power");
                speed = d.Speed ?? throw Missing(ctx, "speed");
                if (power <= 0) throw new DataException($"{ctx}: campo 'power' deve ser > 0");
                if (speed <= 0) throw new DataException($"{ctx}: campo 'speed' deve ser > 0");
            }
            items[i + 1] = new ItemDef((ushort)(i + 1), d.Id!, display, maxStack, kind, places, power, speed);
        }

        var starter = new List<ItemStack>();
        var starterDtos = player.StarterItems ?? [];
        for (int i = 0; i < starterDtos.Count; i++)
        {
            var ctx = $"{PlayerFile} starterItems[{i}]";
            var s = starterDtos[i] ?? throw new DataException($"{ctx}: entrada nula");
            ushort id = Resolve(itemIds, s.Item ?? throw Missing(ctx, "item"), ctx, "item");
            int count = s.Count ?? throw Missing(ctx, "count");
            if (count < 1 || count > items[id].MaxStack)
                throw new DataException($"{ctx}: campo 'count' deve estar entre 1 e {items[id].MaxStack}");
            starter.Add(new ItemStack(id, count));
        }

        return new Registry(blocks, walls, items, physics, starter);
    }

    private static T Parse<T>(string json, string file) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, Options)
                   ?? throw new DataException($"{file}: conteúdo vazio");
        }
        catch (JsonException ex)
        {
            throw new DataException($"{file}: JSON inválido — {ex.Message}");
        }
    }

    private static void RequireNoNulls<T>(List<T> list, string file) where T : class
    {
        for (int i = 0; i < list.Count; i++)
            if (list[i] is null)
                throw new DataException($"{file}[{i}]: entrada nula");
    }

    private static Dictionary<string, ushort> AssignIds(List<string?> names, string file, string reserved)
    {
        if (names.Count >= ushort.MaxValue)
            throw new DataException($"{file}: excesso de entradas ({names.Count})");
        var ids = new Dictionary<string, ushort>();
        for (int i = 0; i < names.Count; i++)
        {
            var name = names[i];
            if (string.IsNullOrWhiteSpace(name))
                throw new DataException($"{file}[{i}]: campo 'id' ausente");
            if (name == reserved)
                throw new DataException($"{file}[{i}]: id '{name}' é reservado");
            if (!ids.TryAdd(name, (ushort)(i + 1)))
                throw new DataException($"{file}[{i}]: id duplicado '{name}'");
        }
        return ids;
    }

    private static ushort Resolve(Dictionary<string, ushort> ids, string name, string ctx, string field) =>
        ids.TryGetValue(name, out var id)
            ? id
            : throw new DataException($"{ctx}: campo '{field}' referencia id inexistente '{name}'");

    private static DataException Missing(string ctx, string field) =>
        new($"{ctx}: campo obrigatório '{field}' ausente");

    private sealed class BlockDto
    {
        public string? Id { get; set; }
        public float? Hardness { get; set; }
        public bool? Solid { get; set; }
        public string? Drops { get; set; }
        public float? Light { get; set; }
        public bool? Tree { get; set; }
    }

    private sealed class WallDto
    {
        public string? Id { get; set; }
    }

    private sealed class ItemDto
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public int? MaxStack { get; set; }
        public string? Kind { get; set; }
        public string? PlacesBlock { get; set; }
        public float? Power { get; set; }
        public float? Speed { get; set; }
    }

    private sealed class PlayerDto
    {
        public List<StarterItemDto>? StarterItems { get; set; }
    }

    private sealed class StarterItemDto
    {
        public string? Item { get; set; }
        public int? Count { get; set; }
    }
}
