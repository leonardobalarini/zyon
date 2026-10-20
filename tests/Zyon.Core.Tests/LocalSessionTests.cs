using Zyon.Core.Sim;

namespace Zyon.Core.Tests;

public class LocalSessionTests
{
    private static (LocalSession Session, SimHarness Harness) Create()
    {
        var h = new SimHarness();
        return (new LocalSession(h.Sim, h.PlayerId), h);
    }

    [Fact]
    public void Update_RunsWholeTicks_AndKeepsRemainderAsAlpha()
    {
        var (session, _) = Create();
        using var _s = session;
        Assert.Equal(0.5f, session.InterpolationAlpha, 3);   // a fase começa no meio do tick (FrameClock)

        session.Update(3.25 / Simulation.TicksPerSecond);

        Assert.Equal(3, session.World.Tick);
        Assert.Equal(0.75f, session.InterpolationAlpha, 3);
    }

    [Fact]
    public void Update_AccumulatesSmallDeltas()
    {
        var (session, _) = Create();
        using var _s = session;

        session.Update(0.6 / Simulation.TicksPerSecond);
        session.Update(0.6 / Simulation.TicksPerSecond);

        Assert.Equal(1, session.World.Tick);
    }

    [Fact]
    public void Update_CapsTicksPerCall_AndDropsBacklog()
    {
        var (session, _) = Create();
        using var _s = session;

        session.Update(1.0);
        Assert.Equal(LocalSession.MaxTicksPerUpdate, session.World.Tick);

        session.Update(0.0);
        Assert.Equal(LocalSession.MaxTicksPerUpdate, session.World.Tick);
    }

    [Fact]
    public void SendAndSetInput_ReachTheLocalPlayer()
    {
        var (session, h) = Create();
        using var _s = session;
        session.DrainEvents();

        session.Send(new SelectHotbarSlot(session.LocalPlayerId, 2));
        session.SetInput(new PlayerInput(1f, false, false, 0, 0));
        for (int i = 0; i < 6; i++)
            session.Update(5.0 / Simulation.TicksPerSecond + 1e-9);
        Assert.Equal(30, session.World.Tick);

        Assert.Equal(2, session.World.GetSelectedSlot(session.LocalPlayerId));
        Assert.True(h.PlayerPosition.X > 20.5f);
        Assert.Contains(new HotbarSelectionChanged(session.LocalPlayerId, 2), session.DrainEvents());
    }

    [Fact]
    public void Update_NaNOrNegativeDelta_IsIgnored()
    {
        var (session, _) = Create();
        using var _s = session;

        session.Update(double.NaN);
        session.Update(-1.0);
        session.Update(2.0 / Simulation.TicksPerSecond);

        Assert.Equal(2, session.World.Tick);
        Assert.True(float.IsFinite(session.InterpolationAlpha));
        Assert.InRange(session.InterpolationAlpha, 0f, 0.99999994f);
    }

    /// <summary>Roda os deltas e devolve quantos quadros rodaram 0, 1, 2 e 3+ ticks.</summary>
    private static int[] TicksPerFrame(LocalSession session, IEnumerable<double> deltas)
    {
        var counts = new int[4];
        foreach (double delta in deltas)
        {
            long before = session.World.Tick;
            session.Update(delta);
            counts[Math.Min(3, (int)(session.World.Tick - before))]++;
        }
        return counts;
    }

    /// <summary>Deltas de um monitor de <paramref name="hz"/> Hz com ruído uniforme de ±<paramref name="jitterMs"/> ms.</summary>
    private static IEnumerable<double> Display(double hz, double jitterMs, int frames, int seed = 1)
    {
        var random = new Random(seed);
        for (int i = 0; i < frames; i++)
            yield return 1.0 / hz + (random.NextDouble() * 2 - 1) * jitterMs / 1000;
    }

    [Theory]
    [InlineData(60.0)]
    [InlineData(59.94)]
    public void Update_JitteryVsyncNear60Hz_RunsExactlyOneTickEveryFrame(double hz)
    {
        var (session, _) = Create();
        using var _s = session;

        var counts = TicksPerFrame(session, Display(hz, jitterMs: 0.3, frames: 10_000));

        Assert.Equal([0, 10_000, 0, 0], counts);
    }
}
