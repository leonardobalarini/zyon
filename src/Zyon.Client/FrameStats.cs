namespace Zyon.Client;

/// <summary>
/// Medidas de um quadro: intervalo desde o anterior (o delta do motor), tempo total do processamento do
/// quadro, tempo dentro da sessão (simulação), tempo em eventos + renderizadores, chunks montados ou remontados
/// e coletas de lixo ocorridas durante o quadro. <see cref="Ticks"/> = ticks de simulação rodados no quadro;
/// <see cref="MeasuredSeconds"/> = intervalo medido pelo relógio entre o início deste quadro e o do anterior
/// (0 se desconhecido) — difere do delta quando o motor suaviza o delta; <see cref="NewChunks"/> = quantos dos
/// <see cref="Rebuilds"/> foram chunks que entraram na faixa visível; <see cref="LightRecomputes"/> e
/// <see cref="LightMs"/> = recálculos da luz no quadro e o tempo do último (0 se não houve).
/// </summary>
public readonly record struct FrameSample(
    double DeltaSeconds, double ProcessMs, double UpdateMs, double RenderMs, int Rebuilds, int GcCollections,
    int Ticks = 1, double MeasuredSeconds = 0, int NewChunks = 0, int LightRecomputes = 0, double LightMs = 0)
{
    /// <summary>Quadro com 2 ou mais ticks: a simulação avançou o dobro (ou mais) de uma vez.</summary>
    public bool IsHitch => Ticks >= 2;
}

/// <summary>Resumo de um conjunto de quadros. <see cref="Frames"/> = 0 enquanto nada foi publicado.</summary>
public readonly record struct FrameSummary(
    int Frames, double Fps,
    double FrameAvgMs, double FrameMaxMs,
    double ProcessAvgMs, double ProcessMaxMs,
    double UpdateAvgMs, double UpdateMaxMs,
    double RenderAvgMs, double RenderMaxMs,
    int Rebuilds, int MaxRebuildsPerFrame, int GcCollections,
    int ZeroTickFrames = 0, int MultiTickFrames = 0, double MeasuredMaxMs = 0,
    int LightRecomputes = 0, double LightMaxMs = 0);

/// <summary>Último quadro com 2+ ticks: o número do quadro (desde o início) e suas medidas.</summary>
public readonly record struct FrameHitch(long Frame, FrameSample Sample);

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

    /// <summary>Quadros adicionados desde o início.</summary>
    public long FrameCount { get; private set; }

    /// <summary>Último quadro com 2+ ticks (null se nenhum).</summary>
    public FrameHitch? LastHitch { get; private set; }

    /// <summary>Adiciona o quadro; devolve true se ele rodou 2+ ticks (vira <see cref="LastHitch"/>).</summary>
    public bool Add(FrameSample sample)
    {
        FrameCount++;
        window.Add(sample);
        overall.Add(sample);
        // Tolerância: somas de deltas como 10 × 0,1 ficam um fio abaixo de 1,0 em ponto flutuante.
        if (window.Seconds >= windowSeconds - 1e-9)
        {
            Window = window.Summary();
            window = default;
        }
        if (!sample.IsHitch) return false;
        LastHitch = new FrameHitch(FrameCount, sample);
        return true;
    }

    private struct Accumulator
    {
        public int Frames;
        public double Seconds;
        private double frameMax, measuredMax, lightMax, processSum, processMax, updateSum, updateMax, renderSum, renderMax;
        private int rebuilds, rebuildsMax, gc, zeroTicks, multiTicks, lightRecomputes;

        public void Add(FrameSample s)
        {
            Frames++;
            if (s.DeltaSeconds > 0)
            {
                Seconds += s.DeltaSeconds;
                frameMax = Math.Max(frameMax, s.DeltaSeconds * 1000);
            }
            measuredMax = Math.Max(measuredMax, s.MeasuredSeconds * 1000);
            processSum += s.ProcessMs;
            processMax = Math.Max(processMax, s.ProcessMs);
            updateSum += s.UpdateMs;
            updateMax = Math.Max(updateMax, s.UpdateMs);
            renderSum += s.RenderMs;
            renderMax = Math.Max(renderMax, s.RenderMs);
            rebuilds += s.Rebuilds;
            rebuildsMax = Math.Max(rebuildsMax, s.Rebuilds);
            gc += s.GcCollections;
            if (s.Ticks == 0) zeroTicks++;
            if (s.IsHitch) multiTicks++;
            lightRecomputes += s.LightRecomputes;
            if (s.LightRecomputes > 0) lightMax = Math.Max(lightMax, s.LightMs);
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
                rebuilds, rebuildsMax, gc,
                zeroTicks, multiTicks, measuredMax,
                lightRecomputes, lightMax);
        }
    }
}
