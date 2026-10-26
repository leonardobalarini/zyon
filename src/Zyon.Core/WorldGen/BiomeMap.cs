using Zyon.Core.Biomes;

namespace Zyon.Core.WorldGen;

/// <summary>Faixa posicionada: colunas [Left, Right] da divisa na superfície, antes do ruído.</summary>
public sealed record PlacedBand(Biome Biome, int Left, int Right);

/// <summary>
/// Bioma de cada tile durante a geração (não vai para o save). Fora das faixas vale o padrão. Cada divisa de faixa
/// é deslocada por ruído (um valor por linha, ±EdgeNoise colunas) e, a d linhas abaixo da superfície da coluna,
/// anda d / Taper colunas para dentro da faixa — o bioma afina com a profundidade. Somente leitura depois do passe
/// "biomes": <see cref="At"/> pode ser chamado de dentro de Parallel.For.
/// </summary>
public sealed class BiomeMap
{
    private readonly int height;
    private readonly List<PlacedBand> bands = [];
    private readonly List<(int[] Left, int[] Right)> offsets = [];

    public BiomeMap(BiomeSet set, int worldHeight)
    {
        Set = set;
        height = worldHeight;
    }

    public BiomeSet Set { get; }

    /// <summary>A da esquerda primeiro.</summary>
    public IReadOnlyList<PlacedBand> Bands => bands;

    /// <summary>Acrescenta uma faixa; o ruído das duas divisas é lido uma vez por linha (amostras em x = edge × 1000).</summary>
    internal void AddBand(Biome biome, int left, int right, FastNoiseLite edgeNoise)
    {
        int edge = 2 * bands.Count;
        var l = new int[height];
        var r = new int[height];
        for (int y = 0; y < height; y++)
        {
            l[y] = (int)MathF.Round(edgeNoise.GetNoise(edge * 1000f, y) * Set.EdgeNoise);
            r[y] = (int)MathF.Round(edgeNoise.GetNoise((edge + 1) * 1000f, y) * Set.EdgeNoise);
        }
        bands.Add(new PlacedBand(biome, left, right));
        offsets.Add((l, r));
    }

    /// <summary>Bioma do tile (x, y) numa coluna cuja superfície está em surfaceY.</summary>
    public Biome At(int x, int y, int surfaceY)
    {
        int row = Math.Clamp(y, 0, height - 1);
        int inward = Math.Max(0, y - surfaceY) / Set.Taper;
        for (int i = 0; i < bands.Count; i++)
        {
            var band = bands[i];
            var (l, r) = offsets[i];
            if (x >= band.Left + l[row] + inward && x <= band.Right + r[row] - inward)
                return band.Biome;
        }
        return Set.Default;
    }
}
