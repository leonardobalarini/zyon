using Zyon.Core.Lighting;

namespace Zyon.Core.Tests;

public class DayCycleTests
{
    private static readonly DayCycle Cycle = new(TestData.LightingConfig());

    [Fact]
    public void TicksPerDay_FollowsDayLength()
    {
        Assert.Equal(1440L * 60, Cycle.TicksPerDay);
    }

    [Fact]
    public void NewWorld_StartsInFullDaylight()
    {
        Assert.Equal(0.30, Cycle.TimeOfDay(0), 6);
        Assert.Equal(1f, Cycle.SkyFactor(0), 5);
    }

    [Fact]
    public void TimeOfDay_Wraps()
    {
        Assert.Equal(Cycle.TimeOfDay(123), Cycle.TimeOfDay(123 + Cycle.TicksPerDay), 9);
    }

    [Fact]
    public void Midnight_IsNightLevel_Noon_IsFull()
    {
        long ticksPerDay = Cycle.TicksPerDay;
        long midnight = (long)((1.0 - 0.30) * ticksPerDay);        // hora 0
        long noon = (long)((0.50 - 0.30) * ticksPerDay);            // hora 0,5

        Assert.Equal(0.15f, Cycle.SkyFactor(midnight), 4);
        Assert.Equal(1f, Cycle.SkyFactor(noon), 4);
    }

    [Fact]
    public void Transitions_AreContinuous_NoJumps()
    {
        float previous = Cycle.SkyFactor(0);
        for (long t = 1; t <= Cycle.TicksPerDay; t++)
        {
            float current = Cycle.SkyFactor(t);
            Assert.True(MathF.Abs(current - previous) < 0.002f, $"salto de {previous} para {current} no tick {t}");
            previous = current;
        }
    }

    private static long TickAt(double hour) =>
        (long)Math.Round((hour - 0.30 + 1) % 1 * Cycle.TicksPerDay);

    private static double Half => TestData.LightingConfig().TwilightFraction / 2;

    [Theory]
    [InlineData(0.25)]
    [InlineData(0.75)]
    public void Dawn_And_Dusk_AreAtTheMidpoint(double hour)
    {
        Assert.Equal((1f + 0.15f) / 2, Cycle.SkyFactor(TickAt(hour)), 3);
    }

    [Fact]
    public void RampWidth_MatchesTwilightFraction()
    {
        Assert.Equal(1f, Cycle.SkyFactor(TickAt(0.25 + Half)), 3);
        Assert.Equal(1f, Cycle.SkyFactor(TickAt(0.75 - Half)), 3);
        Assert.Equal(0.15f, Cycle.SkyFactor(TickAt(0.25 - Half - 0.002)), 3);
        Assert.Equal(0.15f, Cycle.SkyFactor(TickAt(0.75 + Half + 0.002)), 3);
    }

    [Fact]
    public void Dawn_NeverDecreases_Dusk_NeverIncreases()
    {
        float prev = Cycle.SkyFactor(TickAt(0.2));
        for (long t = TickAt(0.2) + 1; t <= TickAt(0.3); t++)
        {
            float cur = Cycle.SkyFactor(t);
            Assert.True(cur >= prev - 1e-6f, $"amanhecer decresceu no tick {t}");
            prev = cur;
        }

        prev = Cycle.SkyFactor(TickAt(0.7));
        for (long t = TickAt(0.7) + 1; t <= TickAt(0.8); t++)
        {
            float cur = Cycle.SkyFactor(t);
            Assert.True(cur <= prev + 1e-6f, $"anoitecer cresceu no tick {t}");
            prev = cur;
        }
    }

    [Fact]
    public void TimeOfDay_StaysInRange_ForNegativeAndHugeTicks()
    {
        long d = Cycle.TicksPerDay;
        foreach (long tick in new[] { -1L, -d, 10L * 365 * d + 5 })
        {
            double t = Cycle.TimeOfDay(tick);
            Assert.InRange(t, 0.0, 1.0 - 1e-12);
        }
    }
}
