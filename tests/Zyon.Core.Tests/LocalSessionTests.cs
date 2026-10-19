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

        session.Update(3.5 / Simulation.TicksPerSecond);

        Assert.Equal(3, session.World.Tick);
        Assert.Equal(0.5f, session.InterpolationAlpha, 3);
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
            session.Update(5.0 / Simulation.TicksPerSecond);
        Assert.Equal(30, session.World.Tick);

        Assert.Equal(2, session.World.GetSelectedSlot(session.LocalPlayerId));
        Assert.True(h.PlayerPosition.X > 20.5f);
        Assert.Contains(new HotbarSelectionChanged(session.LocalPlayerId, 2), session.DrainEvents());
    }
}
