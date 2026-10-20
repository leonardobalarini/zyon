using Zyon.Core.Sim;

namespace Zyon.Core.Tests;

public class FrameClockTests
{
    private const double Tick = 1.0 / 60;

    private static FrameClock Clock(double tolerance = FrameClock.DefaultSnapToleranceSeconds) =>
        new(Tick, maxTicksPerAdvance: 5, tolerance);

    /// <summary>Quantos quadros rodaram 0, 1, 2 e 3+ ticks.</summary>
    private static int[] Count(FrameClock clock, IEnumerable<double> deltas)
    {
        var counts = new int[4];
        foreach (double d in deltas) counts[Math.Min(3, clock.Advance(d))]++;
        return counts;
    }

    private static IEnumerable<double> Display(double hz, double jitterMs, int frames, int seed = 1)
    {
        var random = new Random(seed);
        for (int i = 0; i < frames; i++)
            yield return 1.0 / hz + (random.NextDouble() * 2 - 1) * jitterMs / 1000;
    }

    [Fact]
    public void Starts_AtHalfTick()
    {
        Assert.Equal(0.5f, Clock().Alpha, 6);
    }

    [Theory]
    [InlineData(60.0)]
    [InlineData(59.94)]
    [InlineData(60.06)]
    public void JitteryDisplayNear60Hz_AlwaysOneTick(double hz)
    {
        var clock = Clock();

        Assert.Equal([0, 10_000, 0, 0], Count(clock, Display(hz, 0.3, 10_000)));
        Assert.InRange(clock.Alpha, 0.49f, 0.51f);
    }

    [Fact]
    public void WithoutSnapping_59_94Hz_DriftsIntoTwoTickFrames()
    {
        // Sem arredondar, o resíduo de 0,017 ms por quadro acumula até sobrar um tick: o mesmo soluço periódico
        // que o arredondamento elimina (o teste acima, com a tolerância padrão).
        var counts = Count(Clock(tolerance: 0), Display(59.94, 0, 10_000));

        Assert.True(counts[2] > 0, $"esperava quadros com 2 ticks, contagem {string.Join('/', counts)}");
    }

    [Fact]
    public void Display144Hz_RunsZeroOrOneTick_NeverTwo_AndKeepsRealTime()
    {
        var clock = Clock();
        var deltas = Display(144, 0.3, 10_000).ToList();

        var counts = Count(clock, deltas);

        Assert.Equal(0, counts[2] + counts[3]);
        int ticks = counts[1];
        Assert.InRange(ticks, (int)(deltas.Sum() / Tick) - 1, (int)(deltas.Sum() / Tick) + 1);
    }

    [Theory]
    [InlineData(0.040, 2, 3)]
    [InlineData(2.0 / 60 + 0.0002, 2, 2)]   // VSync perdido: dois períodos de 60 Hz
    [InlineData(0.050, 3, 3)]
    public void GenuineHitch_RunsTheTicksItTook(double hitch, int min, int max)
    {
        var clock = Clock();
        Count(clock, Display(60, 0.3, 600));

        Assert.InRange(clock.Advance(hitch), min, max);
    }

    [Fact]
    public void AfterUnsnappedFrame_PhaseReturnsToCenter()
    {
        var clock = Clock();
        clock.Advance(0.020);   // 3,33 ms além de um tick: fora da tolerância
        Assert.InRange(clock.Alpha, 0.69f, 0.71f);

        // 3,33 ms / 0,05 ms por quadro = 67 quadros.
        Assert.Equal([0, 70, 0, 0], Count(clock, Display(60, 0.3, 70)));
        Assert.Equal(0.5f, clock.Alpha, 4);
    }

    [Fact]
    public void SnappedResidual_IsDiscarded()
    {
        // 16,9 ms está a 0,23 ms de um tick: roda 1 tick por quadro (simulação 1,4% mais lenta que o relógio).
        var clock = Clock();

        Assert.Equal([0, 1000, 0, 0], Count(clock, Enumerable.Repeat(0.0169, 1000)));
    }

    [Fact]
    public void LongStall_CapsTicks_AndRecentersPhase()
    {
        var clock = Clock();

        Assert.Equal(5, clock.Advance(1.01));
        Assert.Equal(0.5f, clock.Alpha, 6);
        Assert.False(clock.LastSnapped);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidDelta_IsIgnored(double delta)
    {
        var clock = Clock();

        Assert.Equal(0, clock.Advance(delta));
        Assert.Equal(0.5f, clock.Alpha, 6);
    }

    [Fact]
    public void Constructor_ValidatesArguments()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FrameClock(0, 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FrameClock(double.NaN, 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FrameClock(Tick, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FrameClock(Tick, 5, -0.001));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FrameClock(Tick, 5, Tick / 2));
    }
}
