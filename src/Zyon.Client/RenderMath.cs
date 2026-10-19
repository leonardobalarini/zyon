using System.Numerics;

namespace Zyon.Client;

/// <summary>Conversões entre unidades de tile (Core) e pixels (apresentação).</summary>
public static class RenderMath
{
    public const int PixelsPerTile = 16;

    public static Vector2 ToPixels(Vector2 tiles) => tiles * PixelsPerTile;

    public static (int X, int Y) TileAtPixel(Vector2 pixels) =>
        ((int)MathF.Floor(pixels.X / PixelsPerTile), (int)MathF.Floor(pixels.Y / PixelsPerTile));

    /// <summary>Posição para desenhar entre o tick anterior e o atual (alpha em [0, 1]).</summary>
    public static Vector2 Interpolate(Vector2 previous, Vector2 current, float alpha) =>
        Vector2.Lerp(previous, current, alpha);
}
