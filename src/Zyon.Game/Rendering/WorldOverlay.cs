using Godot;
using Zyon.Client;
using Zyon.Core.Sim;

namespace Zyon.Game.Rendering;

/// <summary>Desenha no mundo o contorno do tile mirado (se alcançável) e as rachaduras da mineração.</summary>
public partial class WorldOverlay : Node2D
{
    private static readonly Color Highlight = new(1f, 1f, 1f, 0.8f);
    private static readonly Color Crack = new(0f, 0f, 0f, 1f);
    private const float Tile = RenderMath.PixelsPerTile;

    private IWorldView view = null!;
    private int playerId;
    private (int X, int Y)? aimed;
    private (int X, int Y, float Progress)? mining;

    public void Init(IWorldView worldView, int localPlayerId)
    {
        view = worldView;
        playerId = localPlayerId;
    }

    public void OnEvent(IGameEvent gameEvent)
    {
        if (gameEvent is MiningProgressChanged m && m.PlayerId == playerId)
            mining = m.Progress > 0f ? (m.X, m.Y, m.Progress) : null;
    }

    public void Refresh(Vector2 mouseWorldPixels)
    {
        var tile = RenderMath.TileAtPixel(mouseWorldPixels.ToNumerics());
        aimed = InReach(tile.X, tile.Y) ? tile : null;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (aimed is { } a)
            DrawRect(new Rect2(a.X * Tile, a.Y * Tile, Tile, Tile), Highlight, filled: false, width: 1f);
        if (mining is { } m)
            DrawRect(new Rect2(m.X * Tile, m.Y * Tile, Tile, Tile), new Color(Crack, 0.6f * m.Progress));
    }

    /// <summary>Mesma regra do Core (distância do centro do jogador ao centro do tile), só para o visual.</summary>
    private bool InReach(int x, int y)
    {
        if (!view.TryGetEntityView(playerId, out var me)) return false;
        var tileCenter = new System.Numerics.Vector2(x + 0.5f, y + 0.5f);
        return System.Numerics.Vector2.Distance(me.Position, tileCenter) <= view.Registry.Physics.ReachTiles;
    }
}
