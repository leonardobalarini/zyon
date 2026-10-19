namespace Zyon.Core.Tiles;

public sealed class TileWorld
{
    private readonly Chunk[] chunks;

    public TileWorld(int width, int height)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        Width = width;
        Height = height;
        ChunksX = (width + Chunk.Size - 1) / Chunk.Size;
        ChunksY = (height + Chunk.Size - 1) / Chunk.Size;
        chunks = new Chunk[ChunksX * ChunksY];
        for (int cy = 0; cy < ChunksY; cy++)
            for (int cx = 0; cx < ChunksX; cx++)
                chunks[cy * ChunksX + cx] = new Chunk(cx, cy);
    }

    public int Width { get; }
    public int Height { get; }
    public int ChunksX { get; }
    public int ChunksY { get; }
    public IEnumerable<Chunk> Chunks => chunks;

    public bool InBounds(int x, int y) => (uint)x < (uint)Width && (uint)y < (uint)Height;

    public Chunk GetChunk(int chunkX, int chunkY)
    {
        if ((uint)chunkX >= (uint)ChunksX || (uint)chunkY >= (uint)ChunksY)
            throw new ArgumentOutOfRangeException(
                (uint)chunkX >= (uint)ChunksX ? nameof(chunkX) : nameof(chunkY),
                $"Chunk ({chunkX}, {chunkY}) fora da grade {ChunksX}×{ChunksY}");
        return chunks[chunkY * ChunksX + chunkX];
    }

    public ushort GetBlock(int x, int y) =>
        InBounds(x, y) ? ChunkAt(x, y).Blocks[LocalIndex(x, y)] : (ushort)0;

    public ushort GetWall(int x, int y) =>
        InBounds(x, y) ? ChunkAt(x, y).Walls[LocalIndex(x, y)] : (ushort)0;

    public void SetBlock(int x, int y, ushort id)
    {
        EnsureInBounds(x, y);
        var chunk = ChunkAt(x, y);
        chunk.Blocks[LocalIndex(x, y)] = id;
        chunk.Dirty = true;
    }

    public void SetWall(int x, int y, ushort id)
    {
        EnsureInBounds(x, y);
        var chunk = ChunkAt(x, y);
        chunk.Walls[LocalIndex(x, y)] = id;
        chunk.Dirty = true;
    }

    private Chunk ChunkAt(int x, int y) => chunks[(y >> Chunk.Shift) * ChunksX + (x >> Chunk.Shift)];

    private static int LocalIndex(int x, int y) => Chunk.Index(x & Chunk.Mask, y & Chunk.Mask);

    private bool InBoundsX(int x) => (uint)x < (uint)Width;

    private void EnsureInBounds(int x, int y)
    {
        if (!InBounds(x, y))
            throw new ArgumentOutOfRangeException(InBoundsX(x) ? "y" : "x", $"Tile ({x}, {y}) fora do mundo {Width}×{Height}");
    }
}
