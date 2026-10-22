namespace Zyon.Core.Lighting;

/// <summary>Retângulo inclusivo de tiles.</summary>
public readonly record struct TileRect(int MinX, int MinY, int MaxX, int MaxY)
{
    public int Width => MaxX - MinX + 1;
    public int Height => MaxY - MinY + 1;
    public bool IsEmpty => MaxX < MinX || MaxY < MinY;

    public bool Contains(int x, int y) => x >= MinX && x <= MaxX && y >= MinY && y <= MaxY;

    public bool Contains(TileRect other) =>
        other.MinX >= MinX && other.MaxX <= MaxX && other.MinY >= MinY && other.MaxY <= MaxY;

    public TileRect Expand(int by) => new(MinX - by, MinY - by, MaxX + by, MaxY + by);

    public TileRect ClampTo(int worldWidth, int worldHeight) =>
        new(Math.Max(0, MinX), Math.Max(0, MinY), Math.Min(worldWidth - 1, MaxX), Math.Min(worldHeight - 1, MaxY));
}
