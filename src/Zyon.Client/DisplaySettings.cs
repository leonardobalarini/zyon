using System.Text.Json;

namespace Zyon.Client;

/// <summary>
/// Preferências de tela que sobrevivem entre sessões (hoje só a tela cheia, F11). É só conforto: arquivo ausente ou
/// ilegível volta ao padrão em vez de impedir o jogo de abrir.
/// </summary>
public sealed record DisplaySettings(bool Fullscreen = false)
{
    public const string FileName = "settings.json";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

    /// <summary>Lê o arquivo; devolve o padrão e o motivo em <paramref name="error"/> se não der.</summary>
    public static DisplaySettings Load(string path, out string? error)
    {
        error = null;
        if (!File.Exists(path)) return new DisplaySettings();
        try
        {
            return JsonSerializer.Deserialize<DisplaySettings>(File.ReadAllText(path), JsonOptions) ?? new DisplaySettings();
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            error = $"{FileName}: {e.Message}";
            return new DisplaySettings();
        }
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
    }
}
