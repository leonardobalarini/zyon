namespace Zyon.Client;

/// <summary>
/// Medidas de um quadro: intervalo desde o anterior (o delta do motor), tempo total do processamento do
/// quadro, tempo dentro da sessão (simulação), tempo em eventos + renderizadores, chunks remontados e
/// coletas de lixo ocorridas durante o quadro.
/// </summary>
public readonly record struct FrameSample(
    double DeltaSeconds, double ProcessMs, double UpdateMs, double RenderMs, int Rebuilds, int GcCollections);

/// <summary>Resumo de um conjunto de quadros. <see cref="Frames"/> = 0 enquanto nada foi publicado.</summary>
public readonly record struct FrameSummary(
    int Frames, double Fps,
    double FrameAvgMs, double FrameMaxMs,
    double ProcessAvgMs, double ProcessMaxMs,
    double UpdateAvgMs, double UpdateMaxMs,
    double RenderAvgMs, double RenderMaxMs,
    int Rebuilds, int MaxRebuildsPerFrame, int GcCollections);

/// <summary>
/// Estatísticas de quadros para a sobreposição de depuração: <see cref="Window"/> resume a última janela
/// completa (1 s por padrão; é trocada de uma vez quando a janela fecha) e <see cref="Overall"/> tudo desde o início.
/// </summary>
public sealed class FrameStats
{
    private readonly double windowSeconds;
    private Accumulator window;
    private Accumulator overall;

    public FrameStats(double windowSeconds = 1.0)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(windowSeconds);
        this.windowSeconds = windowSeconds;
    }

    public FrameSummary Window { get; private set; }
    public FrameSummary Overall => overall.Summary();

    public void Add(FrameSample sample)
    {
        window.Add(sample);
        overall.Add(sample);
        // Tolerância: somas de deltas como 10 × 0,1 ficam um fio abaixo de 1,0 em ponto flutuante.
        if (window.Seconds >= windowSeconds - 1e-9)
        {
            Window = window.Summary();
            window = default;
        }
    }

    private struct Accumulator
    {
        public int Frames;
        public double Seconds;
        private double frameMax, processSum, processMax, updateSum, updateMax, renderSum, renderMax;
        private int rebuilds, rebuildsMax, gc;

        public void Add(FrameSample s)
        {
            Frames++;
            if (s.DeltaSeconds > 0)
            {
                Seconds += s.DeltaSeconds;
                frameMax = Math.Max(frameMax, s.DeltaSeconds * 1000);
            }
            processSum += s.ProcessMs;
            processMax = Math.Max(processMax, s.ProcessMs);
            updateSum += s.UpdateMs;
            updateMax = Math.Max(updateMax, s.UpdateMs);
            renderSum += s.RenderMs;
            renderMax = Math.Max(renderMax, s.RenderMs);
            rebuilds += s.Rebuilds;
            rebuildsMax = Math.Max(rebuildsMax, s.Rebuilds);
            gc += s.GcCollections;
        }

        public readonly FrameSummary Summary()
        {
            if (Frames == 0) return default;
            return new FrameSummary(
                Frames, Seconds > 0 ? Frames / Seconds : 0,
                Seconds * 1000 / Frames, frameMax,
                processSum / Frames, processMax,
                updateSum / Frames, updateMax,
                renderSum / Frames, renderMax,
                rebuilds, rebuildsMax, gc);
        }
    }
}
