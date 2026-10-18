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
}
