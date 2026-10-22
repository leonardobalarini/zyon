using System.Text.Json;
using Zyon.Core.Data;

namespace Zyon.Core.Lighting;

public static class LightingConfigLoader
{
    public const string FileName = "lighting.json";

    public static LightingConfig LoadFromDirectory(string dataDir)
    {
        var path = Path.Combine(dataDir, FileName);
        if (!File.Exists(path)) throw new DataException($"{FileName}: arquivo não encontrado em '{dataDir}'");
        return Load(File.ReadAllText(path));
    }

    public static LightingConfig Load(string json)
    {
        LightingConfig c;
        try
        {
            c = JsonSerializer.Deserialize<LightingConfig>(json, RegistryLoader.Options)
                ?? throw new DataException($"{FileName}: conteúdo vazio");
        }
        catch (JsonException ex)
        {
            throw new DataException($"{FileName}: JSON inválido — {ex.Message}", ex);
        }

        static void Require(bool ok, string message)
        {
            if (!ok) throw new DataException($"{FileName}: {message}");
        }

        Require(c.AirAttenuation > 0 && c.AirAttenuation <= 1, "'airAttenuation' deve estar em (0, 1]");
        Require(c.SolidAttenuation > 0 && c.SolidAttenuation <= 1, "'solidAttenuation' deve estar em (0, 1]");
        Require(c.DayLengthSeconds >= 1 && c.DayLengthSeconds <= 1e7, "'dayLengthSeconds' deve estar em [1, 10000000]");
        Require(c.StartTimeOfDay >= 0 && c.StartTimeOfDay < 1, "'startTimeOfDay' deve estar em [0, 1)");
        Require(c.NightSkyLevel >= 0 && c.NightSkyLevel <= 1, "'nightSkyLevel' deve estar em [0, 1]");
        // O limite de 0,25 evita que as rampas do amanhecer e do anoitecer se sobreponham.
        Require(c.TwilightFraction > 0 && c.TwilightFraction <= 0.25, "'twilightFraction' deve estar em (0, 0,25]");
        Require(c.PlayerHaloLevel >= 0 && c.PlayerHaloLevel <= 1, "'playerHaloLevel' deve estar em [0, 1]");
        Require(c.RegionMargin >= 0 && c.RegionMargin <= 256, "'regionMargin' deve estar entre 0 e 256");
        return c;
    }
}
