using Zyon.Core.Tiles;

namespace Zyon.Client;

/// <summary>Faixa inclusiva de chunks.</summary>
public readonly record struct ChunkRange(int MinX, int MinY, int MaxX, int MaxY)
{
    public bool Contains(int cx, int cy) => cx >= MinX && cx <= MaxX && cy >= MinY && cy <= MaxY;
}

public static class ChunkVisibility
{
    public const int ChunkPixels = Chunk.Size * RenderMath.PixelsPerTile;

    /// <summary>Chunks que intersectam o retângulo (em pixels), mais uma margem, limitados ao mundo.</summary>
    public static ChunkRange Range(float left, float top, float width, float height, int margin, int chunksX, int chunksY)
    {
        int minX = (int)MathF.Floor(left / ChunkPixels) - margin;
        int minY = (int)MathF.Floor(top / ChunkPixels) - margin;
        int maxX = (int)MathF.Floor((left + width) / ChunkPixels) + margin;
        int maxY = (int)MathF.Floor((top + height) / ChunkPixels) + margin;
        return new ChunkRange(
            Math.Clamp(minX, 0, chunksX - 1),
            Math.Clamp(minY, 0, chunksY - 1),
            Math.Clamp(maxX, 0, chunksX - 1),
            Math.Clamp(maxY, 0, chunksY - 1));
    }
}
