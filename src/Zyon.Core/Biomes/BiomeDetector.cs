using Zyon.Core.Data;
using Zyon.Core.Sim;

namespace Zyon.Core.Biomes;

/// <summary>
/// Bioma em volta de um ponto, como no Terraria: conta os marcadores de cada bioma num retângulo DetectWidth ×
/// DetectHeight centrado nele; o que tiver mais vence se chegar a DetectThreshold, senão vale o padrão (empate: o
/// primeiro do arquivo). Nada vai para o save. O jogo consulta a cada <see cref="IntervalTicks"/>.
/// </summary>
public sealed class BiomeDetector
{
    public const int IntervalTicks = 30;

    private readonly BiomeSet set;
    private readonly int[] biomeOfBlock;
    private readonly int[] counts;

    public BiomeDetector(BiomeSet set, Registry registry)
    {
        this.set = set;
        biomeOfBlock = Enumerable.Repeat(-1, registry.Blocks.Count).ToArray();
        foreach (var biome in set.All)
            foreach (var marker in biome.Markers)
                biomeOfBlock[marker] = biome.Index;
        counts = new int[set.All.Count];
    }

    public Biome Detect(IWorldView view, int centerX, int centerY)
    {
        Array.Clear(counts);
        int x0 = Math.Max(0, centerX - set.DetectWidth / 2), x1 = Math.Min(view.Width, centerX - set.DetectWidth / 2 + set.DetectWidth);
        int y0 = Math.Max(0, centerY - set.DetectHeight / 2), y1 = Math.Min(view.Height, centerY - set.DetectHeight / 2 + set.DetectHeight);
        for (int y = y0; y < y1; y++)
        for (int x = x0; x < x1; x++)
        {
            int b = biomeOfBlock[view.GetBlock(x, y)];
            if (b >= 0) counts[b]++;
        }

        int best = -1;
        for (int i = 0; i < counts.Length; i++)
            if (counts[i] >= set.DetectThreshold && (best < 0 || counts[i] > counts[best]))
                best = i;
        return best < 0 ? set.Default : set.All[best];
    }
}
