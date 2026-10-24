using Godot;
using Zyon.Core.Items;
using Zyon.Core.Sim;

namespace Zyon.Game;

/// <summary>
/// Roteiro do smoke test: parado, anda para a direita, pula, minera o chão à frente, coloca uma tocha no buraco
/// (ou num tile de ar vizinho, se o jogador caiu nele) e confere que o jogador continua no mundo, que algum tile
/// mudou e que a tocha foi colocada. A luz da tocha é conferida pelo GameRoot.
/// </summary>
public sealed class SmokeTest(IGameSession session, PlayerController controller, int totalFrames)
{
    private static readonly PlayerInput Idle = new(0f, false, false, 0, 0);
    // Teto para segurar o botão com a tocha: frames rodam mais rápido que ticks e há o tempo de recarga.
    private const int MaxTorchFrames = 120;

    private int frame;
    private int tilesChanged;
    private (int X, int Y)? target;
    private (int X, int Y)? torch;
    private int torchFrames;

    public bool Finished { get; private set; }
    public string? Failure { get; private set; }

    /// <summary>Onde a tocha foi colocada (válido quando o roteiro termina sem falha).</summary>
    public (int X, int Y) TorchTile => torch ?? (0, 0);

    /// <summary>Tile minerado pelo roteiro.</summary>
    public (int X, int Y) MinedTile => target ?? (0, 0);

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
        bool settled = me.Position == me.PreviousPosition;
        if (frame < third / 2)
            controller.ScriptedInput = Idle;
        else if (frame < third)
            controller.ScriptedInput = new PlayerInput(1f, frame % 40 < 10, false, 0, 0);
        else if (target is null && (frame < third + 30 || !settled))
            controller.ScriptedInput = Idle;   // para e pousa antes de mirar (frames rodam mais rápido que ticks)
        else if (target is null || session.World.GetBlock(target.Value.X, target.Value.Y) != 0)
        {
            target ??= GroundAhead(me.Position.X, me.Position.Y + me.Size.Y / 2);
            controller.ScriptedInput = new PlayerInput(0f, false, true, target.Value.X, target.Value.Y);
        }
        else
            PlaceTorch(id, me.Position, me.Size, settled);

        if (++frame < totalFrames) return;
        if (tilesChanged == 0)
        {
            Fail(target is { } t ? $"nenhum tile mudou (alvo {t})" : "o jogador não parou para mirar");
            return;
        }
        if (torch is not { } placed || session.World.GetBlock(placed.X, placed.Y) != session.World.Registry.BlockId("torch"))
        {
            Fail(torch is { } p ? $"a tocha não foi colocada em {p}" : $"o roteiro não chegou à tocha (alvo {target})");
            return;
        }
        Finished = true;
    }

    /// <summary>Tile minerado virou ar: espera pousar, escolhe o slot da tocha e segura o botão mirando o tile.</summary>
    private void PlaceTorch(int id, System.Numerics.Vector2 position, System.Numerics.Vector2 size, bool settled)
    {
        ushort torchBlock = session.World.Registry.BlockId("torch");
        if ((torch is { } t && session.World.GetBlock(t.X, t.Y) == torchBlock) || torchFrames >= MaxTorchFrames)
        {
            controller.ScriptedInput = Idle;
            return;
        }
        if (torch is null)
        {
            // O jogador pode ter caído no buraco: espera parar antes de escolher onde pôr a tocha.
            if (!settled)
            {
                controller.ScriptedInput = Idle;
                return;
            }
            int slot = TorchSlot();
            if (slot < 0)
            {
                Fail("nenhuma tocha na hotbar");
                return;
            }
            session.Send(new SelectHotbarSlot(id, slot));
            torch = TorchSpot(position, size);
        }
        torchFrames++;
        controller.ScriptedInput = new PlayerInput(0f, false, true, torch!.Value.X, torch.Value.Y);
    }

    private int TorchSlot()
    {
        var inventory = session.World.GetInventory(session.LocalPlayerId);
        if (inventory is null) return -1;
        ushort torchItem = session.World.Registry.ItemId("torch");
        for (int slot = 0; slot < Inventory.HotbarSize; slot++)
            if (inventory[slot].ItemId == torchItem && !inventory[slot].IsEmpty)
                return slot;
        return -1;
    }

    /// <summary>
    /// O tile minerado, ou o primeiro vizinho dele que aceite um bloco: ar, ancorado (parede ou bloco ao lado) e
    /// fora do corpo do jogador (a regra é validada no Core; aqui só se escolhe um candidato provável).
    /// </summary>
    private (int X, int Y) TorchSpot(System.Numerics.Vector2 position, System.Numerics.Vector2 size)
    {
        var (mx, my) = target!.Value;
        (int, int)[] offsets = [(0, 0), (-1, 0), (1, 0), (0, -1), (-1, -1), (1, -1), (-2, 0), (2, 0), (0, 1)];
        foreach (var (dx, dy) in offsets)
        {
            int x = mx + dx, y = my + dy;
            if (session.World.GetBlock(x, y) != 0 || !Anchored(x, y) || Overlaps(position, size, x, y)) continue;
            return (x, y);
        }
        return (mx, my);
    }

    private bool Anchored(int x, int y) =>
        session.World.GetWall(x, y) != 0
        || session.World.GetBlock(x - 1, y) != 0 || session.World.GetBlock(x + 1, y) != 0
        || session.World.GetBlock(x, y - 1) != 0 || session.World.GetBlock(x, y + 1) != 0;

    private static bool Overlaps(System.Numerics.Vector2 center, System.Numerics.Vector2 size, int x, int y) =>
        System.MathF.Abs(center.X - (x + 0.5f)) < (size.X + 1f) / 2 &&
        System.MathF.Abs(center.Y - (y + 0.5f)) < (size.Y + 1f) / 2;

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
