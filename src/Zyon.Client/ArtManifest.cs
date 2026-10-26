using System.Text.Json;
using System.Text.Json.Serialization;
using Zyon.Core.Data;

namespace Zyon.Client;

/// <summary>Ponto de encaixe (pixels no ícone 16×16) e direção para onde o ícone aponta (ângulo de tela, graus).</summary>
public sealed class ItemArt
{
    public int Cell { get; set; }
    public int GripX { get; set; }
    public int GripY { get; set; }
    public float PointDeg { get; set; }
}

/// <summary>Quadros do jogador (tiras de 32×48; pés em FeetX, FeetY). Ver "Formatos" no plano 6.</summary>
public sealed class PlayerArt
{
    public int FrameWidth { get; set; } = 32;
    public int FrameHeight { get; set; } = 48;
    public int FeetX { get; set; } = 16;
    public int FeetY { get; set; } = 47;
    public List<string> Layers { get; set; } = ["body", "outfit", "hair"];
    public int BodyFrames { get; set; } = 10;
    public int ArmFrames { get; set; } = 15;
    public int Idle { get; set; } = 0;
    public int WalkFirst { get; set; } = 1;
    public int WalkCount { get; set; } = 8;
    public int Air { get; set; } = 9;
    public int SwingFirst { get; set; } = 10;
    public int SwingCount { get; set; } = 5;

    /// <summary>Ponto da mão em cada quadro do braço (x, y no quadro).</summary>
    public List<int[]> Hand { get; set; } = [];

    /// <summary>Direção do braço (ombro → mão) em cada quadro, em ângulo de tela.</summary>
    public List<float> ArmAngleDeg { get; set; } = [];
}

/// <summary>data/client/art/art.json — escrito pelo Zyon.ArtGen, lido pelo jogo.</summary>
public sealed class ArtManifest
{
    public const string FileName = "art.json";
    public const string TilesFile = "tiles.png";
    public const string WallsFile = "walls.png";
    public const string MergeFile = "merge.png";
    public const string ItemsFile = "items.png";
    public const string ArmLayer = "arm";
    public const int BackgroundWidth = 480;
    public const int BackgroundHeight = 180;

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public int TileSize { get; set; } = 16;
    public int Variations { get; set; } = 3;
    public Dictionary<string, int> Tiles { get; set; } = [];
    public Dictionary<string, int> Walls { get; set; } = [];
    public List<string> MergeOrder { get; set; } = [];
    public Dictionary<string, ItemArt> Items { get; set; } = [];
    public PlayerArt Player { get; set; } = new();

    /// <summary>Camadas de fundo (nome sem extensão), em backgrounds/&lt;nome&gt;.png, 480×180, repetíveis na horizontal.</summary>
    public List<string> Backgrounds { get; set; } = [];

    public static string BackgroundFile(string name) => $"backgrounds/{name}.png";

    public static string PlayerFile(string layer) => $"player/{layer}.png";

    public string ToJson() => JsonSerializer.Serialize(this, Options).ReplaceLineEndings("\n") + "\n";

    public static ArtManifest FromJson(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<ArtManifest>(json, Options) ?? throw new DataException($"{FileName}: conteúdo vazio");
        }
        catch (JsonException ex)
        {
            throw new DataException($"{FileName}: JSON inválido — {ex.Message}", ex);
        }
    }
}
