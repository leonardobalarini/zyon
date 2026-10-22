using Zyon.Core.Sim;

namespace Zyon.Core.Lighting;

/// <summary>
/// Hora do dia derivada do Tick (que já vai no save do mundo). Amanhecer centrado em 0,25 e anoitecer em 0,75,
/// cada um com transição suave (smoothstep) de TwilightFraction do dia.
/// </summary>
public sealed class DayCycle(LightingConfig config)
{
    private const double Dawn = 0.25;
    private const double Dusk = 0.75;

    public long TicksPerDay { get; } = (long)Math.Round(config.DayLengthSeconds * Simulation.TicksPerSecond);

    /// <summary>Hora em [0, 1): 0 = meia-noite, 0,5 = meio-dia.</summary>
    public double TimeOfDay(long tick)
    {
        long r = ((tick % TicksPerDay) + TicksPerDay) % TicksPerDay;
        return (r / (double)TicksPerDay + config.StartTimeOfDay) % 1.0;
    }

    /// <summary>Intensidade da luz do céu: NightSkyLevel à noite, 1 de dia.</summary>
    public float SkyFactor(long tick)
    {
        double t = TimeOfDay(tick);
        double half = config.TwilightFraction / 2;
        double day = Ramp(t, Dawn - half, Dawn + half) - Ramp(t, Dusk - half, Dusk + half);
        float night = config.NightSkyLevel;
        return (float)(night + (1 - night) * day);
    }

    private static double Ramp(double t, double from, double to)
    {
        if (t <= from) return 0;
        if (t >= to) return 1;
        double x = (t - from) / (to - from);
        return x * x * (3 - 2 * x);
    }
}
