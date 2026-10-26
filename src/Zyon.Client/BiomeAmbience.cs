namespace Zyon.Client;

/// <summary>
/// Céu e peso (opacidade) dos fundos de cada bioma conforme o bioma atual. Ao trocar, o céu vai linearmente da cor
/// daquele instante até a do bioma novo em <see cref="TransitionSeconds"/>, o peso do novo sobe e o dos outros desce
/// no mesmo ritmo — trocar no meio de uma transição parte do que está na tela.
/// </summary>
public sealed class BiomeAmbience
{
    public const float TransitionSeconds = 2f;

    private readonly ClientVisuals visuals;
    private readonly Dictionary<string, float> weights = [];
    private readonly string[] ids;
    private Rgb from;
    private float progress = 1f;

    public BiomeAmbience(ClientVisuals visuals, string initialBiome)
    {
        this.visuals = visuals;
        ids = visuals.Biomes.Keys.ToArray();
        foreach (var id in ids) weights[id] = 0f;
        Require(initialBiome);
        Current = initialBiome;
        weights[initialBiome] = 1f;
        Sky = from = visuals.For(initialBiome).Sky;
    }

    public string Current { get; private set; }
    public Rgb Sky { get; private set; }

    public float Weight(string biomeId)
    {
        Require(biomeId);
        return weights[biomeId];
    }

    public void SetBiome(string biomeId)
    {
        Require(biomeId);
        if (biomeId == Current) return;
        Current = biomeId;
        from = Sky;
        progress = 0f;
    }

    public void Advance(float seconds)
    {
        float step = seconds / TransitionSeconds;
        progress = Math.Min(1f, progress + step);
        foreach (var id in ids)
            weights[id] = id == Current ? Math.Min(1f, weights[id] + step) : Math.Max(0f, weights[id] - step);
        var to = visuals.For(Current).Sky;
        Sky = new Rgb(Lerp(from.R, to.R, progress), Lerp(from.G, to.G, progress), Lerp(from.B, to.B, progress));
    }

    private void Require(string biomeId)
    {
        if (!weights.ContainsKey(biomeId)) throw new ArgumentException($"bioma '{biomeId}' sem visual", nameof(biomeId));
    }

    private static byte Lerp(byte a, byte b, float t) => (byte)Math.Clamp((int)MathF.Round(a + (b - a) * t), 0, 255);
}
