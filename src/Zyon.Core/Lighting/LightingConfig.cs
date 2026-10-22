namespace Zyon.Core.Lighting;

/// <summary>Parâmetros da luz (data/lighting.json). Níveis em [0, 1]; frações do dia em [0, 1).</summary>
public sealed class LightingConfig
{
    /// <summary>Luz perdida ao entrar num tile de ar ou bloco não sólido.</summary>
    public float AirAttenuation { get; init; } = 0.06f;

    /// <summary>Luz perdida ao entrar num bloco sólido.</summary>
    public float SolidAttenuation { get; init; } = 0.25f;

    public double DayLengthSeconds { get; init; } = 1440;
    public double StartTimeOfDay { get; init; } = 0.30;
    public float NightSkyLevel { get; init; } = 0.15f;

    /// <summary>Duração do amanhecer e do anoitecer, como fração do dia.</summary>
    public double TwilightFraction { get; init; } = 1.0 / 24;

    public float PlayerHaloLevel { get; init; } = 0.3f;

    /// <summary>Tiles calculados além da área visível em cada lado (maior que o alcance de uma luz 1,0).</summary>
    public int RegionMargin { get; init; } = 18;
}
