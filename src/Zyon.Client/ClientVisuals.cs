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
        if (!uint.TryParse(hex.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out uint v)) return false;
        color = new Rgb((byte)(v >> 16), (byte)(v >> 8), (byte)v);
        return true;
    }
}

/// <summary>Cores do cliente que não são arte: o céu. Lidas de data/client/visuals.json.</summary>
public sealed class ClientVisuals
{
    public const string FileName = "visuals.json";

    private ClientVisuals(Rgb sky) => Sky = sky;

    public Rgb Sky { get; }

    public static ClientVisuals LoadFromDirectory(string dataDir)
    {
        var path = Path.Combine(dataDir, "client", FileName);
        if (!File.Exists(path)) throw new DataException($"{FileName}: arquivo não encontrado em '{path}'");
        return Load(File.ReadAllText(path));
    }

    public static ClientVisuals Load(string json)
    {
        Dto dto;
        try
        {
            dto = JsonSerializer.Deserialize<Dto>(json, JsonOptions) ?? throw Fail("conteúdo vazio");
        }
        catch (JsonException ex)
        {
            throw Fail($"JSON inválido — {ex.Message}");
        }

        return new ClientVisuals(Parse(dto.Sky, "sky"));
    }

    private static Rgb Parse(string? hex, string field) =>
        hex is null ? throw Fail($"campo '{field}' ausente") :
        Rgb.TryParse(hex, out var c) ? c : throw Fail($"'{field}': cor inválida '{hex}' (use #rrggbb)");

    private static DataException Fail(string message) => new($"{FileName}: {message}");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private sealed class Dto
    {
        public string? Sky { get; set; }
    }
}
