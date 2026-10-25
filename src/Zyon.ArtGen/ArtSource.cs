using System.Text.Json;
using System.Text.Json.Serialization;
using Zyon.Core.Data;

namespace Zyon.ArtGen;

public sealed class MaterialSource
{
    public string Recipe { get; set; } = "";
    public string[]? Palette { get; set; }
    public string[]? Cap { get; set; }
    public string? Base { get; set; }
    public string[]? Fleck { get; set; }
    public string[]? Flame { get; set; }
    public string? Shape { get; set; }
    public string[]? Accent { get; set; }
}

public sealed class PlayerSource
{
    public string[] Skin { get; set; } = [];
    public string[] Hair { get; set; } = [];
    public string[] Outfit { get; set; } = [];
    public string Detail { get; set; } = "";
    public string[] Boots { get; set; } = [];
    public string Outline { get; set; } = "";
}

public sealed class ItemSource
{
    public string Recipe { get; set; } = "";
    public string[]? Handle { get; set; }
    public string[]? Head { get; set; }
}

/// <summary>art/art.json — paletas e receitas da arte gerada. Validado contra o Registry.</summary>
public sealed class ArtSource
{
    public const string FileName = "art.json";

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    private static readonly string[] SolidRecipes = ["clumps", "panels", "ore", "plates", "stripes"];
    private static readonly string[] WallRecipes = ["clumps", "plates"];

    public int Seed { get; set; }
    public int Variations { get; set; } = 3;
    public Dictionary<string, MaterialSource> Blocks { get; set; } = [];
    public Dictionary<string, MaterialSource> Walls { get; set; } = [];
    public List<string> MergeOrder { get; set; } = [];
    public PlayerSource Player { get; set; } = new();
    public Dictionary<string, ItemSource> Items { get; set; } = [];

    public static ArtSource LoadFromFile(string path, Registry registry)
    {
        if (!File.Exists(path)) throw new DataException($"{FileName}: arquivo não encontrado em '{path}'");
        return Load(File.ReadAllText(path), registry);
    }

    public static ArtSource Load(string json, Registry registry)
    {
        ArtSource s;
        try
        {
            s = JsonSerializer.Deserialize<ArtSource>(json, Options) ?? throw Fail("conteúdo vazio");
        }
        catch (JsonException ex)
        {
            throw Fail($"JSON inválido — {ex.Message}");
        }
        s.Validate(registry);
        return s;
    }

    /// <summary>Paleta de 3 tons de um bloco (para minério, a do material base).</summary>
    public Rgba[] BlockPalette(string name)
    {
        var m = Blocks[name];
        return Colors(m.Recipe == "ore" ? Blocks[m.Base!].Palette! : m.Palette!);
    }

    public static Rgba[] Colors(string[] hex) => hex.Select(Rgba.Parse).ToArray();

    private static DataException Fail(string message) => new($"{FileName}: {message}");

    private void Validate(Registry registry)
    {
        if (Variations < 1 || Variations > 8) throw Fail("'variations' deve estar entre 1 e 8");

        for (int id = 1; id < registry.Blocks.Count; id++)
        {
            var def = registry.Block((ushort)id);
            if (!Blocks.TryGetValue(def.Name, out var m)) throw Fail($"falta a arte do bloco '{def.Name}'");
            string ctx = $"blocks.{def.Name}";
            if (def.Solid && !SolidRecipes.Contains(m.Recipe)) throw Fail($"{ctx}: receita '{m.Recipe}' inválida para bloco sólido (use {string.Join(", ", SolidRecipes)})");
            if (!def.Solid && m.Recipe is not ("torch" or "prop")) throw Fail($"{ctx}: bloco não sólido precisa da receita 'torch' ou 'prop'");
            if (m.Recipe == "prop")
            {
                if (m.Shape is null || !PropGenerator.Shapes.Contains(m.Shape))
                    throw Fail($"{ctx}: forma '{m.Shape}' desconhecida (use {string.Join(", ", PropGenerator.Shapes)})");
                if (PropGenerator.ShapesWithAccent.Contains(m.Shape)) CheckColors(m.Accent, 3, $"{ctx}.accent");
            }
            if (m.Recipe == "stripes") CheckColors(m.Accent, 3, $"{ctx}.accent");
            if (m.Recipe == "ore")
            {
                if (m.Base is null || !Blocks.TryGetValue(m.Base, out var b) || b.Recipe is not ("clumps" or "panels"))
                    throw Fail($"{ctx}: 'base' deve ser um bloco com receita clumps ou panels");
                CheckColors(m.Fleck, 3, $"{ctx}.fleck");
            }
            else CheckColors(m.Palette, 3, $"{ctx}.palette");
            if (m.Cap is not null) CheckColors(m.Cap, 3, $"{ctx}.cap");
            if (m.Recipe == "torch") CheckColors(m.Flame, 3, $"{ctx}.flame");
        }
        foreach (var name in Blocks.Keys)
            if (!registry.TryGetBlockId(name, out _)) throw Fail($"blocks: bloco '{name}' não existe");

        for (int id = 1; id < registry.Walls.Count; id++)
        {
            var def = registry.Wall((ushort)id);
            if (!Walls.TryGetValue(def.Name, out var m)) throw Fail($"falta a arte da parede '{def.Name}'");
            if (!WallRecipes.Contains(m.Recipe)) throw Fail($"walls.{def.Name}: paredes usam a receita 'clumps' ou 'plates'");
            CheckColors(m.Palette, 3, $"walls.{def.Name}.palette");
        }
        foreach (var name in Walls.Keys)
            if (!registry.TryGetWallId(name, out _)) throw Fail($"walls: parede '{name}' não existe");

        var seen = new HashSet<string>();
        foreach (var name in MergeOrder)
        {
            if (!registry.TryGetBlockId(name, out var id) || !registry.Block(id).Solid) throw Fail($"mergeOrder: '{name}' não é um bloco sólido");
            if (!seen.Add(name)) throw Fail($"mergeOrder: '{name}' repetido");
        }

        CheckColors(Player.Skin, 3, "player.skin");
        CheckColors(Player.Hair, 3, "player.hair");
        CheckColors(Player.Outfit, 3, "player.outfit");
        CheckColors(Player.Boots, 2, "player.boots");
        CheckColors([Player.Detail, Player.Outline], 2, "player.detail/outline");

        for (int id = 1; id < registry.Items.Count; id++)
        {
            var def = registry.Item((ushort)id);
            if (Items.TryGetValue(def.Name, out var item))
            {
                if (item.Recipe != "pickaxe") throw Fail($"items.{def.Name}: receita '{item.Recipe}' desconhecida (use pickaxe; itens de bloco não precisam de entrada)");
                CheckColors(item.Handle, 3, $"items.{def.Name}.handle");
                CheckColors(item.Head, 3, $"items.{def.Name}.head");
            }
            else if (def.PlacesBlockId == 0) throw Fail($"falta a arte do item '{def.Name}'");
        }
        foreach (var name in Items.Keys)
            if (!registry.TryGetItemId(name, out _)) throw Fail($"items: item '{name}' não existe");
    }

    private static void CheckColors(string[]? colors, int count, string ctx)
    {
        if (colors is null || colors.Length != count) throw Fail($"'{ctx}' precisa de {count} cores");
        foreach (var c in colors)
        {
            try { Rgba.Parse(c); }
            catch (FormatException ex) { throw Fail($"'{ctx}': {ex.Message}"); }
        }
    }
}
