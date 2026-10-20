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

    /// <summary>
    /// Arredonda para o pixel inteiro mais próximo (meio para cima), como o snap de transformações 2D do motor:
    /// câmera e sprites arredondados da mesma forma andam juntos, sem tremer um pixel um contra o outro.
    /// </summary>
    public static Vector2 SnapToPixel(Vector2 pixels) =>
        new(MathF.Round(pixels.X, MidpointRounding.AwayFromZero), MathF.Round(pixels.Y, MidpointRounding.AwayFromZero));
}
