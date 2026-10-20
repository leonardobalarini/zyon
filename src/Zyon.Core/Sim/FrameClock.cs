namespace Zyon.Core.Sim;

/// <summary>
/// Converte o tempo de cada quadro em ticks fixos (acumulador) sem os quadros de 0 e 2 ticks que o ruído do
/// relógio causaria num monitor de mesma frequência que a simulação.
/// <list type="bullet">
/// <item>Delta a até <see cref="SnapToleranceSeconds"/> de um múltiplo inteiro (≥ 1) do tick é arredondado para
/// esse múltiplo ("delta snapping"); o resíduo é descartado. Num monitor de 59,94 Hz a simulação roda 0,1% mais
/// devagar em vez de rodar 2 ticks num quadro a cada ~17 s; o desvio de longo prazo é limitado pela tolerância.</item>
/// <item>A fase do acumulador começa no meio do tick e, nos quadros arredondados, é puxada de volta ao meio em no
/// máximo <see cref="MaxPhaseCorrectionSeconds"/> por quadro: o ruído que escapa da tolerância precisa passar de
/// meio tick para mudar a contagem.</item>
/// <item>Deltas fora da tolerância (monitor de 144 Hz, um quadro realmente longo) entram inteiros.</item>
/// </list>
/// No máximo <see cref="MaxTicksPerAdvance"/> ticks por quadro; o excedente é descartado (câmera lenta).
/// </summary>
public sealed class FrameClock
{
    /// <summary>Tolerância padrão do arredondamento: 0,5 ms (o ruído típico entre quadros com VSync).</summary>
    public const double DefaultSnapToleranceSeconds = 0.0005;

    /// <summary>Correção máxima da fase por quadro arredondado (imperceptível: 0,05 ms de tempo desenhado).</summary>
    public const double MaxPhaseCorrectionSeconds = 0.00005;

    private readonly double center;
    private double accumulator;

    public FrameClock(double tickSeconds, int maxTicksPerAdvance, double snapToleranceSeconds = DefaultSnapToleranceSeconds)
    {
        if (!(tickSeconds > 0) || !double.IsFinite(tickSeconds))
            throw new ArgumentOutOfRangeException(nameof(tickSeconds), tickSeconds, "precisa ser > 0 e finito");
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxTicksPerAdvance);
        if (!(snapToleranceSeconds >= 0) || snapToleranceSeconds >= tickSeconds / 2)
            throw new ArgumentOutOfRangeException(nameof(snapToleranceSeconds), snapToleranceSeconds, "precisa estar em [0, meio tick)");
        TickSeconds = tickSeconds;
        MaxTicksPerAdvance = maxTicksPerAdvance;
        SnapToleranceSeconds = snapToleranceSeconds;
        center = tickSeconds / 2;
        accumulator = center;
    }

    public double TickSeconds { get; }
    public int MaxTicksPerAdvance { get; }
    public double SnapToleranceSeconds { get; }

    /// <summary>Fração do tick já acumulada, em [0, 1): o alpha de interpolação.</summary>
    public float Alpha => Math.Clamp((float)(accumulator / TickSeconds), 0f, 0.99999994f);

    /// <summary>Se o último delta foi arredondado para um múltiplo do tick.</summary>
    public bool LastSnapped { get; private set; }

    /// <summary>Avança o relógio em <paramref name="deltaSeconds"/> e devolve quantos ticks rodar agora.</summary>
    public int Advance(double deltaSeconds)
    {
        if (!(deltaSeconds > 0) || !double.IsFinite(deltaSeconds))
            return 0;

        double multiple = Math.Round(deltaSeconds / TickSeconds);
        LastSnapped = multiple >= 1 && Math.Abs(deltaSeconds - multiple * TickSeconds) <= SnapToleranceSeconds;
        accumulator += LastSnapped ? multiple * TickSeconds : deltaSeconds;

        int ticks = 0;
        while (accumulator >= TickSeconds && ticks < MaxTicksPerAdvance)
        {
            accumulator -= TickSeconds;
            ticks++;
        }
        if (accumulator >= TickSeconds)
            accumulator = center;   // atraso grande demais: descarta o excedente
        else if (LastSnapped)
            accumulator += Math.Clamp(center - accumulator, -MaxPhaseCorrectionSeconds, MaxPhaseCorrectionSeconds);
        return ticks;
    }
}
