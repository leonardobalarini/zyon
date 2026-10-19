namespace Zyon.Core.Tiles;

/// <summary>Hash FNV-1a 64 bits do conteúdo de um TileWorld (dimensões, blocos e paredes). Usado para verificar determinismo.</summary>
public static class TileWorldHash
{
    private const ulong OffsetBasis = 14695981039346656037UL;
    private const ulong Prime = 1099511628211UL;

    public static ulong Compute(TileWorld world)
    {
        ulong h = OffsetBasis;
        h = Mix32(h, (uint)world.Width);
        h = Mix32(h, (uint)world.Height);
        foreach (var chunk in world.Chunks)
        {
            foreach (ushort b in chunk.Blocks) h = Mix16(h, b);
            foreach (ushort w in chunk.Walls) h = Mix16(h, w);
        }
        return h;
    }

    private static ulong Mix16(ulong h, ushort v)
    {
        h = (h ^ (byte)v) * Prime;
        return (h ^ (byte)(v >> 8)) * Prime;
    }

    private static ulong Mix32(ulong h, uint v)
    {
        for (int i = 0; i < 4; i++)
            h = (h ^ (byte)(v >> (8 * i))) * Prime;
        return h;
    }
}
