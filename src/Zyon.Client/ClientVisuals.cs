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

/// <summary>Céu e camadas de fundo (da mais distante à mais próxima) de um bioma.</summary>
public sealed record BiomeVisual(Rgb Sky, IReadOnlyList<string> Layers);

/// <summary>
/// Cores e fundos de cada bioma, lidos de data/client/biomes.json (não confundir com data/biomes.json, da
/// simulação). Precisa de exatamente uma entrada para cada bioma dos dados; as camadas precisam existir na arte.
/// </summary>
public sealed class ClientVisuals
{
    public const string FileName = "biomes.json";
    public const string DisplayName = "client/biomes.json";

    /// <summary>Camadas por bioma: distante e próxima.</summary>
    public const int LayerCount = 2;

    private readonly Dictionary<string, BiomeVisual> biomes;

    private ClientVisuals(Dictionary<string, BiomeVisual> biomes) => this.biomes = biomes;

    public IReadOnlyDictionary<string, BiomeVisual> Biomes => biomes;

    public BiomeVisual For(string biomeId) => biomes[biomeId];

    public static ClientVisuals LoadFromDirectory(string dataDir, IEnumerable<string> biomeIds)
    {
        var path = Path.Combine(dataDir, "client", FileName);
        if (!File.Exists(path)) throw new DataException($"{DisplayName}: arquivo não encontrado em '{path}'");
        return Load(File.ReadAllText(path), biomeIds);
    }

    public static ClientVisuals Load(string json, IEnumerable<string> biomeIds)
    {
        Dictionary<string, Dto?> dtos;
        try
        {
            dtos = JsonSerializer.Deserialize<Dictionary<string, Dto?>>(json, JsonOptions) ?? throw Fail("conteúdo vazio");
        }
        catch (JsonException ex)
        {
            throw Fail($"JSON inválido — {ex.Message}");
        }

        var ids = biomeIds.ToList();
        foreach (var id in ids)
            if (!dtos.ContainsKey(id)) throw Fail($"falta o bioma '{id}' (de data/biomes.json)");
        var result = new Dictionary<string, BiomeVisual>();
        foreach (var (id, dto) in dtos)
        {
            if (!ids.Contains(id)) throw Fail($"bioma '{id}' não existe em data/biomes.json");
            if (dto is null) throw Fail($"{id}: entrada nula");
            var layers = dto.Layers;
            if (layers is null || layers.Count != LayerCount || layers.Any(string.IsNullOrEmpty))
                throw Fail($"{id}: 'layers' precisa de {LayerCount} nomes (distante, próxima)");
            result[id] = new BiomeVisual(Parse(dto.Sky, $"{id}.sky"), layers!);
        }
        return new ClientVisuals(result);
    }

    /// <summary>Lança DataException se alguma camada de fundo não estiver no manifesto da arte.</summary>
    public void RequireArt(ArtManifest manifest)
    {
        foreach (var (id, visual) in biomes)
            foreach (var layer in visual.Layers)
                if (!manifest.Backgrounds.Contains(layer))
                    throw Fail($"{id}: camada '{layer}' sem arte: o gerador cria '<chave>_far' e '<chave>_near' de cada chave em art/art.json → backgrounds; rode tools/gen-art.sh");
    }

    private static Rgb Parse(string? hex, string field) =>
        hex is null ? throw Fail($"campo '{field}' ausente") :
        Rgb.TryParse(hex, out var c) ? c : throw Fail($"'{field}': cor inválida '{hex}' (use #rrggbb)");

    private static DataException Fail(string message) => new($"{DisplayName}: {message}");

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
        public List<string>? Layers { get; set; }
    }
}
