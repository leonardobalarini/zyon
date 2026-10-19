using Godot;
using Zyon.Core.Sim;

namespace Zyon.Game;

/// <summary>
/// Roteiro do smoke test: parado, anda para a direita, pula, minera o chão à frente e confere que o
/// jogador continua no mundo, que algum tile mudou e que a renderização tem chunks ativos.
/// </summary>
public sealed class SmokeTest(IGameSession session, PlayerController controller, int totalFrames)
{
    private static readonly PlayerInput Idle = new(0f, false, false, 0, 0);

    private int frame;
    private int tilesChanged;
    private (int X, int Y)? target;

    public bool Finished { get; private set; }
    public string? Failure { get; private set; }

    public void OnEvent(IGameEvent gameEvent)
    {
        if (gameEvent is TileChanged) tilesChanged++;
    }

    public void Step()
    {
        int id = session.LocalPlayerId;
        if (!session.World.TryGetEntityView(id, out var me))
        {
            Fail("o jogador sumiu");
            return;
        }
        if (!float.IsFinite(me.Position.X) || !float.IsFinite(me.Position.Y))
        {
            Fail($"posição inválida {me.Position}");
            return;
        }

        int third = totalFrames / 3;
        if (frame < third / 2)
            controller.ScriptedInput = Idle;
        else if (frame < third)
            controller.ScriptedInput = new PlayerInput(1f, frame % 40 < 10, false, 0, 0);
        else if (target is null && (frame < third + 30 || me.Position != me.PreviousPosition))
            controller.ScriptedInput = Idle;   // para e pousa antes de mirar (frames rodam mais rápido que ticks)
        else
        {
            target ??= GroundAhead(me.Position.X, me.Position.Y + me.Size.Y / 2);
            controller.ScriptedInput = new PlayerInput(0f, false, true, target.Value.X, target.Value.Y);
        }

        if (++frame < totalFrames) return;
        if (tilesChanged == 0)
        {
            Fail(target is { } t ? $"nenhum tile mudou (alvo {t})" : "o jogador não parou para mirar");
            return;
        }
        Finished = true;
    }

    /// <summary>
    /// Primeiro bloco sólido na linha dos pés: à frente, embaixo ou atrás. Onde o jogador para depende do
    /// tempo real de cada frame; se à frente houver um buraco, mirar nele não mudaria tile algum.
    /// </summary>
    private (int X, int Y) GroundAhead(float x, float feetY)
    {
        int tx = (int)System.MathF.Floor(x);
        int ty = (int)System.MathF.Floor(feetY);
        foreach (int candidate in new[] { tx + 1, tx, tx - 1 })
            if (session.World.GetBlock(candidate, ty) != 0)
                return (candidate, ty);
        return (tx + 1, ty);
    }

    private void Fail(string reason)
    {
        Failure = reason;
        Finished = true;
    }
}
