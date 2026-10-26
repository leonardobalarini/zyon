namespace Zyon.Core.WorldGen.Passes;

/// <summary>
/// Põe as duas faixas de bioma (neve e deserto, nos dados) em lados opostos do centro — o Random do passe sorteia o
/// lado —, cada uma a uma distância da montanha (ou do centro) e com largura sorteadas entre os limites dos dados,
/// em frações da largura do mundo. A montanha e o bunker ficam sempre no bioma padrão.
/// </summary>
internal sealed class BiomesPass : IWorldGenPass
{
    /// <summary>Colunas livres entre a faixa e a montanha e entre a faixa e a borda do mundo.</summary>
    public const int EdgeMargin = 10;

    /// <summary>Largura mínima de uma faixa; com menos espaço ela encosta na borda do mundo.</summary>
    public const int MinBandWidth = 20;

    public const float EdgeNoiseFrequency = 0.04f;

    public string Name => "biomes";

    public void Apply(WorldGenContext ctx)
    {
        var map = ctx.Biomes ?? throw new InvalidOperationException("biomas não carregados: use WorldGenConfigLoader.LoadFromDirectory");
        int width = ctx.World.Width, center = width / 2;
        int innerLeft = center, innerRight = center;
        if (ctx.Mountain is { } m)
        {
            innerLeft = Math.Min(center, Math.Min(m.FootX, m.GentleFootX));
            innerRight = Math.Max(center, Math.Max(m.FootX, m.GentleFootX));
        }

        var rng = ctx.Random;
        var bands = map.Set.Bands;
        int leftIndex = rng.Next(2);
        var noise = ctx.CreateNoise("biomes.edge", EdgeNoiseFrequency, 1);
        for (int side = 0; side < 2; side++)
        {
            var biome = bands[side == 0 ? leftIndex : 1 - leftIndex];
            var band = biome.Band!;
            int gap = (int)MathF.Round(width * Lerp(band.GapMin, band.GapMax, (float)rng.NextDouble()));
            int bandWidth = (int)MathF.Round(width * Lerp(band.WidthMin, band.WidthMax, (float)rng.NextDouble()));
            var (left, right) = side == 0
                ? PlaceBand(leftSide: true, innerLeft, gap, bandWidth, width)
                : PlaceBand(leftSide: false, innerRight, gap, bandWidth, width);
            map.AddBand(biome, left, right, noise);
        }
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    /// <summary>
    /// Colunas [left, right] de uma faixa: começa a gap colunas da borda interna (montanha ou centro) e tem bandWidth
    /// colunas; encolhe para ficar a EdgeMargin da montanha e da borda; se sobrar menos de MinBandWidth, fica com
    /// MinBandWidth colunas encostada na borda do mundo.
    /// </summary>
    internal static (int Left, int Right) PlaceBand(bool leftSide, int inner, int gap, int bandWidth, int worldWidth)
    {
        if (leftSide)
        {
            int right = Math.Min(inner - gap, inner - EdgeMargin);
            int left = Math.Max(right - bandWidth + 1, EdgeMargin);
            return right - left + 1 >= MinBandWidth ? (left, right) : (0, MinBandWidth - 1);
        }
        else
        {
            int left = Math.Max(inner + gap, inner + EdgeMargin);
            int right = Math.Min(left + bandWidth - 1, worldWidth - 1 - EdgeMargin);
            return right - left + 1 >= MinBandWidth ? (left, right) : (worldWidth - MinBandWidth, worldWidth - 1);
        }
    }
}
