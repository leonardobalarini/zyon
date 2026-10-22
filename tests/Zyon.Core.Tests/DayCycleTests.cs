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
}
