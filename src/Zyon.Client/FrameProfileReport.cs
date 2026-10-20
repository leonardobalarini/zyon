using System.Globalization;

namespace Zyon.Client;

/// <summary>
/// Resumo de uma gravação quadro a quadro (modo --profile-walk): distribuição de ticks por quadro, quadros cujo
/// delta difere do intervalo medido e picos de processamento separados por causa provável (chunks novos, GC).
/// </summary>
public static class FrameProfileReport
{
    public static IReadOnlyList<string> Describe(IReadOnlyList<FrameSample> frames, double spikeMs = 8.0)
    {
        var lines = new List<string>();
        if (frames.Count == 0)
        {
            lines.Add("nenhum quadro");
            return lines;
        }

        var ticks = new int[4];
        int deltaMismatch = 0;
        foreach (var f in frames)
        {
            ticks[Math.Clamp(f.Ticks, 0, 3)]++;
            if (f.MeasuredSeconds > 0 && Math.Abs(f.DeltaSeconds - f.MeasuredSeconds) > 0.002) deltaMismatch++;
        }
        lines.Add(F("{0} quadros | ticks por quadro: 0 → {1}, 1 → {2}, 2 → {3}, 3+ → {4} | delta ≠ intervalo medido (>2 ms) em {5}",
            frames.Count, ticks[0], ticks[1], ticks[2], ticks[3], deltaMismatch));

        lines.Add(Percentiles("_Process todos", frames.Select(f => f.ProcessMs)));
        var withNew = frames.Where(f => f.NewChunks > 0).ToList();
        lines.Add(Percentiles("_Process com chunk novo", withNew.Select(f => f.ProcessMs)));
        lines.Add(Percentiles("_Process sem chunk novo", frames.Where(f => f.NewChunks == 0).Select(f => f.ProcessMs)));
        int newChunks = withNew.Sum(f => f.NewChunks);
        if (newChunks > 0)
            lines.Add(F("chunks novos: {0} em {1} quadros (máx {2}/quadro), render med {3:0.000} ms por chunk novo",
                newChunks, withNew.Count, withNew.Max(f => f.NewChunks), withNew.Sum(f => f.RenderMs) / newChunks));

        var spikes = frames.Where(f => f.ProcessMs > spikeMs).ToList();
        lines.Add(F("picos > {0:0.#} ms: {1} (com chunk novo {2}, com GC {3}, sem nenhum dos dois {4})",
            spikeMs, spikes.Count, spikes.Count(f => f.NewChunks > 0), spikes.Count(f => f.GcCollections > 0),
            spikes.Count(f => f.NewChunks == 0 && f.GcCollections == 0)));
        var hitches = frames.Where(f => f.IsHitch).ToList();
        lines.Add(F("quadros com 2+ ticks: {0} (delta > 25 ms {1}, intervalo medido > 25 ms {2}, com pico > {3:0.#} ms {4})",
            hitches.Count, hitches.Count(f => f.DeltaSeconds > 0.025), hitches.Count(f => f.MeasuredSeconds > 0.025),
            spikeMs, hitches.Count(f => f.ProcessMs > spikeMs)));
        return lines;
    }

    private static string Percentiles(string label, IEnumerable<double> values)
    {
        var sorted = values.Order().ToArray();
        if (sorted.Length == 0) return label + ": nenhum";
        double P(double p) => sorted[Math.Min(sorted.Length - 1, (int)(p * sorted.Length))];
        return F("{0}: n {1}, p50 {2:0.000} p99 {3:0.000} máx {4:0.000} ms", label, sorted.Length, P(0.5), P(0.99), sorted[^1]);
    }

    private static string F(string format, params object[] args) => string.Format(CultureInfo.InvariantCulture, format, args);
}
