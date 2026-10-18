using Zyon.Core.Sim;

namespace Zyon.Core.Tests;

public class PlayerMovementTests
{
    private static readonly PlayerInput Right = new(1f, false, false, 0, 0);
    private static readonly PlayerInput JumpHeld = new(0f, true, false, 0, 0);

    [Fact]
    public void Idle_PlayerStaysOnGround()
    {
        using var h = new SimHarness();

        h.Idle(60);

        Assert.Equal(h.StandingY, h.PlayerPosition.Y, 3);
        Assert.Equal(20f, h.PlayerPosition.X, 3);
    }

    [Fact]
    public void FloatingPlayer_FallsToGround()
    {
        using var h = new SimHarness();
        h.Sim.SetInput(h.PlayerId, default);
        // Remove o chão sob o jogador por 5 linhas e deixa um piso mais baixo.
        for (int y = 40; y < 45; y++)
            for (int x = 15; x < 26; x++)
                h.Tiles.SetBlock(x, y, 0);

        h.Idle(120);

        Assert.Equal(45f - h.Registry.Physics.PlayerHeight / 2, h.PlayerPosition.Y, 3);
    }

    [Fact]
    public void WalkRight_AcceleratesAndMoves()
    {
        using var h = new SimHarness();

        h.Run(60, Right);

        Assert.True(h.PlayerPosition.X - 20f > 7f, $"andou só {h.PlayerPosition.X - 20f}");
    }

    [Fact]
    public void ReleasingMove_Decelerates()
    {
        using var h = new SimHarness();
        h.Run(30, Right);

        h.Idle(30);
        float x1 = h.PlayerPosition.X;
        h.Idle(10);

        Assert.Equal(x1, h.PlayerPosition.X, 3);
    }

    [Fact]
    public void Jump_RisesThenLands()
    {
        using var h = new SimHarness();
        h.Idle(5);
        float minY = float.MaxValue;

        for (int i = 0; i < 40; i++)
        {
            h.Run(1, JumpHeld);
            minY = MathF.Min(minY, h.PlayerPosition.Y);
        }
        h.Idle(120);

        Assert.True(h.StandingY - minY > 3.5f, $"pulo de só {h.StandingY - minY}");
        Assert.Equal(h.StandingY, h.PlayerPosition.Y, 3);
    }

    [Fact]
    public void ShortTap_JumpsLowerThanHeldJump()
    {
        float Peak(int holdTicks)
        {
            using var h = new SimHarness();
            h.Idle(5);
            float minY = float.MaxValue;
            for (int i = 0; i < 60; i++)
            {
                h.Run(1, i < holdTicks ? JumpHeld : default);
                minY = MathF.Min(minY, h.PlayerPosition.Y);
            }
            return h.StandingY - minY;
        }

        Assert.True(Peak(3) < Peak(40) * 0.6f);
    }

    [Fact]
    public void CannotJumpInMidAir()
    {
        using var h = new SimHarness();
        h.Idle(5);
        h.Run(30, JumpHeld);   // sobe e começa a cair
        h.Idle(1);              // solta
        float before = h.PlayerPosition.Y;

        h.Run(3, JumpHeld);     // tenta pular no ar

        Assert.True(h.PlayerPosition.Y >= before, "pulou no ar");
    }

    [Fact]
    public void Walking_StepsUpOneTileLedge()
    {
        using var h = new SimHarness();
        h.Tiles.SetBlock(23, 39, h.Registry.BlockId("dirt"));

        h.Run(60, Right);

        Assert.True(h.PlayerPosition.X > 25f, $"parou em {h.PlayerPosition.X}");
    }

    [Fact]
    public void Walking_IsBlockedByTwoTileWall()
    {
        using var h = new SimHarness();
        h.Tiles.SetBlock(23, 39, h.Registry.BlockId("dirt"));
        h.Tiles.SetBlock(23, 38, h.Registry.BlockId("dirt"));

        h.Run(60, Right);

        Assert.Equal(23f - h.Registry.Physics.PlayerWidth / 2, h.PlayerPosition.X, 3);
    }

    // Remove o chão à direita do jogador, anda até cair e devolve o mínimo de Y após apertar pulo
    // depois de `idleTicks` ticks extras no ar.
    private static float PeakAfterLeavingLedge(int idleTicks, out float standingY)
    {
        using var h = new SimHarness();
        for (int y = 40; y < 64; y++)
            for (int x = 22; x <= 40; x++)
                h.Tiles.SetBlock(x, y, 0);
        standingY = h.StandingY;

        int guard = 0;
        while (h.PlayerPosition.Y < h.StandingY + 1e-4f)
        {
            h.Run(1, Right);
            Assert.True(++guard < 200, "jogador não caiu da borda");
        }
        // Neste ponto o 1º tick no ar já passou.
        h.Idle(idleTicks);
        float minY = float.MaxValue;
        for (int i = 0; i < 30; i++)
        {
            h.Run(1, JumpHeld);
            minY = MathF.Min(minY, h.PlayerPosition.Y);
        }
        return minY;
    }

    [Fact]
    public void Coyote_AllowsJumpShortlyAfterLeavingLedge()
    {
        // idle 5 => o aperto cai no último tick da janela de 6 ticks (a janela antiga era 1 tick menor).
        float minY = PeakAfterLeavingLedge(5, out float standingY);

        Assert.True(standingY - minY > 2f, $"não pulou na janela de coyote: {standingY - minY}");
    }

    [Fact]
    public void Coyote_DoesNotAllowJumpLongAfterLeavingLedge()
    {
        float minY = PeakAfterLeavingLedge(15, out float standingY);

        Assert.True(minY >= standingY - 0.01f, $"pulou tarde demais: {standingY - minY}");
    }

    [Fact]
    public void JumpHeldFromSpawn_Jumps()
    {
        using var h = new SimHarness();
        float minY = float.MaxValue;

        for (int i = 0; i < 30; i++)
        {
            h.Run(1, JumpHeld);
            minY = MathF.Min(minY, h.PlayerPosition.Y);
        }

        Assert.True(h.StandingY - minY > 3f, $"pulo de só {h.StandingY - minY}");
    }

    [Fact]
    public void NonFiniteMoveX_IsIgnored()
    {
        using var h = new SimHarness();

        h.Run(10, new PlayerInput(float.NaN, false, false, 0, 0));

        Assert.True(float.IsFinite(h.PlayerPosition.X) && float.IsFinite(h.PlayerPosition.Y));
        Assert.Equal(20f, h.PlayerPosition.X, 3);
    }

    [Fact]
    public void HeadBump_StopsRiseAndFallsBack()
    {
        using var h = new SimHarness();
        for (int x = 18; x <= 22; x++)
            h.Tiles.SetBlock(x, 35, h.Registry.BlockId("dirt"));
        h.Idle(5);
        float minY = float.MaxValue;

        for (int i = 0; i < 40; i++)
        {
            h.Run(1, JumpHeld);
            minY = MathF.Min(minY, h.PlayerPosition.Y);
        }
        h.Idle(120);

        Assert.True(minY >= 36f + h.Registry.Physics.PlayerHeight / 2 - 0.01f, $"atravessou o teto: {minY}");
        Assert.Equal(h.StandingY, h.PlayerPosition.Y, 3);
    }
}
