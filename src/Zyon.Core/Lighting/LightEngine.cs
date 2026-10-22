using Zyon.Core.Sim;

namespace Zyon.Core.Lighting;

/// <summary>
/// Calcula a luz de um retângulo do mundo: semeia céu (colunas abertas — ar sem parede — desde o topo do mundo),
/// blocos emissores e emissores extras; depois propaga com varreduras nas 4 direções até estabilizar.
/// Cada passo para um vizinho perde a atenuação do tile de destino.
/// </summary>
public sealed class LightEngine(LightingConfig config)
{
    private const int MaxRounds = 64;
    private float[] attenuation = [];

    public int LastRounds { get; private set; }

    public void Compute(IWorldView view, TileRect region, float skyFactor, IReadOnlyList<LightEmitter> extra, LightMap output)
    {
        region = region.ClampTo(view.Width, view.Height);
        output.Reset(region);
        if (region.IsEmpty) return;

        int w = region.Width, h = region.Height, size = w * h;
        if (attenuation.Length < size) attenuation = new float[size];
        float[] light = output.Levels;
        var registry = view.Registry;

        // Céu: desce por cada coluna enquanto for ar sem parede de fundo.
        for (int x = region.MinX; x <= region.MaxX; x++)
        {
            for (int y = 0; y <= region.MaxY; y++)
            {
                if (view.GetBlock(x, y) != 0 || view.GetWall(x, y) != 0) break;
                if (y >= region.MinY) light[(y - region.MinY) * w + (x - region.MinX)] = skyFactor;
            }
        }

        // Atenuação de cada tile e emissão dos blocos.
        for (int y = region.MinY; y <= region.MaxY; y++)
        for (int x = region.MinX; x <= region.MaxX; x++)
        {
            int i = (y - region.MinY) * w + (x - region.MinX);
            var block = registry.Block(view.GetBlock(x, y));
            attenuation[i] = block.Solid ? config.SolidAttenuation : config.AirAttenuation;
            if (block.Light > light[i]) light[i] = block.Light;
        }

        foreach (var e in extra)
        {
            if (!region.Contains(e.X, e.Y)) continue;
            int i = (e.Y - region.MinY) * w + (e.X - region.MinX);
            if (e.Level > light[i]) light[i] = e.Level;
        }

        int rounds = 0;
        bool changed;
        do
        {
            changed = false;
            rounds++;
            for (int y = 0; y < h; y++)
            {
                int row = y * w;
                for (int x = 1; x < w; x++) changed |= Relax(light, row + x, row + x - 1);
                for (int x = w - 2; x >= 0; x--) changed |= Relax(light, row + x, row + x + 1);
            }
            for (int x = 0; x < w; x++)
            {
                for (int y = 1; y < h; y++) changed |= Relax(light, y * w + x, (y - 1) * w + x);
                for (int y = h - 2; y >= 0; y--) changed |= Relax(light, y * w + x, (y + 1) * w + x);
            }
        } while (changed && rounds < MaxRounds);
        LastRounds = rounds;
    }

    private bool Relax(float[] light, int target, int from)
    {
        float candidate = light[from] - attenuation[target];
        if (candidate <= light[target]) return false;
        light[target] = candidate;
        return true;
    }
}
