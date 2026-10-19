using Godot;

namespace Zyon.Game;

/// <summary>Segue o jogador com suavização e fica presa às bordas do mundo.</summary>
public partial class GameCamera : Camera2D
{
    public void Init(int worldWidthTiles, int worldHeightTiles)
    {
        LimitLeft = 0;
        LimitTop = 0;
        LimitRight = worldWidthTiles * Zyon.Client.RenderMath.PixelsPerTile;
        LimitBottom = worldHeightTiles * Zyon.Client.RenderMath.PixelsPerTile;
        PositionSmoothingEnabled = true;
        PositionSmoothingSpeed = 10f;
    }

    public override void _Ready() => MakeCurrent();

    public void Follow(Vector2 targetPixels) => Position = targetPixels;

    /// <summary>Retângulo do mundo (em pixels) que está na tela.</summary>
    public Rect2 VisiblePixels()
    {
        var size = GetViewportRect().Size / Zoom;
        return new Rect2(GetScreenCenterPosition() - size / 2, size);
    }
}
