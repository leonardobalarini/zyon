using System.Text.Json;
using System.Text.Json.Nodes;
using Zyon.Core.Data;

namespace Zyon.Core.WorldGen.Structures;

/// <summary>
/// Lê moldes de <c>data/structures/NOME.json</c>: <c>legend</c> (caractere → "keep" ou {block, wall, spawn, anchor})
/// e <c>rows</c> (linhas de mesma largura). Exatamente um tile com <c>spawn</c> e um com <c>anchor</c>.
/// </summary>
public static class StructureLoader
{
    public const string DirectoryName = "structures";

    public static string FilePath(string name) => $"{DirectoryName}/{name}.json";

    public static StructureTemplate LoadFromDirectory(string dataDir, string name)
    {
        var path = Path.Combine(dataDir, DirectoryName, name + ".json");
        if (!File.Exists(path))
            throw new DataException($"{FilePath(name)}: arquivo não encontrado em '{dataDir}'");
        return Load(name, File.ReadAllText(path));
    }

    public static StructureTemplate Load(string name, string json)
    {
        DataException Fail(string message) => new($"{FilePath(name)}: {message}");

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(json);
        }
        catch (JsonException ex)
        {
            throw Fail($"JSON inválido — {ex.Message}");
        }
        if (root is not JsonObject obj) throw Fail("o conteúdo deve ser um objeto");
        if (obj["legend"] is not JsonObject legendNode) throw Fail("campo 'legend' ausente");
        if (obj["rows"] is not JsonArray rowsNode || rowsNode.Count == 0) throw Fail("campo 'rows' ausente ou vazio");

        var legend = new Dictionary<char, (StructureCell Cell, bool Spawn, bool Anchor)>();
        foreach (var (key, value) in legendNode)
        {
            if (key.Length != 1) throw Fail($"legenda: a chave '{key}' deve ter um caractere");
            char ch = key[0];
            if (value is JsonValue text && text.TryGetValue<string>(out var s))
            {
                if (s != "keep") throw Fail($"legenda '{key}': use \"keep\" ou um objeto");
                legend[ch] = (new StructureCell(ch, null, null, true), false, false);
                continue;
            }
            if (value is not JsonObject entry) throw Fail($"legenda '{key}': use \"keep\" ou um objeto");
            foreach (var (field, _) in entry)
                if (field is not ("block" or "wall" or "spawn" or "anchor"))
                    throw Fail($"legenda '{key}': campo '{field}' desconhecido");
            legend[ch] = (new StructureCell(ch, Name(entry, "block", key, Fail), Name(entry, "wall", key, Fail), false),
                Flag(entry, "spawn", key, Fail), Flag(entry, "anchor", key, Fail));
        }

        var rows = new List<string>();
        for (int r = 0; r < rowsNode.Count; r++)
        {
            if (rowsNode[r] is not JsonValue v || !v.TryGetValue<string>(out var row))
                throw Fail($"linha {r + 1}: deve ser texto");
            rows.Add(row);
        }
        int width = rows[0].Length;
        if (width == 0) throw Fail("linha 1: vazia");

        var cells = new StructureCell[width * rows.Count];
        (int X, int Y)? spawn = null, anchor = null;
        for (int y = 0; y < rows.Count; y++)
        {
            if (rows[y].Length != width) throw Fail($"linha {y + 1}: largura {rows[y].Length} diferente da primeira ({width})");
            for (int x = 0; x < width; x++)
            {
                char ch = rows[y][x];
                if (!legend.TryGetValue(ch, out var e)) throw Fail($"linha {y + 1}, coluna {x + 1}: caractere '{ch}' fora da legenda");
                cells[y * width + x] = e.Cell;
                if (e.Spawn)
                {
                    if (spawn is not null) throw Fail($"linha {y + 1}, coluna {x + 1}: segundo tile marcado com 'spawn'");
                    spawn = (x, y);
                }
                if (e.Anchor)
                {
                    if (anchor is not null) throw Fail($"linha {y + 1}, coluna {x + 1}: segundo tile marcado com 'anchor'");
                    anchor = (x, y);
                }
            }
        }
        if (spawn is null) throw Fail("nenhum tile marcado com 'spawn'");
        if (anchor is null) throw Fail("nenhum tile marcado com 'anchor'");
        return new StructureTemplate(name, json, width, rows.Count, cells, anchor.Value, spawn.Value);
    }

    private static string? Name(JsonObject entry, string field, string key, Func<string, DataException> fail)
    {
        if (!entry.TryGetPropertyValue(field, out var node) || node is null) return null;
        if (node is JsonValue v && v.TryGetValue<string>(out var s) && !string.IsNullOrWhiteSpace(s)) return s;
        throw fail($"legenda '{key}': '{field}' deve ser um nome");
    }

    private static bool Flag(JsonObject entry, string field, string key, Func<string, DataException> fail)
    {
        if (!entry.TryGetPropertyValue(field, out var node) || node is null) return false;
        if (node is JsonValue v && v.TryGetValue<bool>(out var b)) return b;
        throw fail($"legenda '{key}': '{field}' deve ser true ou false");
    }
}
