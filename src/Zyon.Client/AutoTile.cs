using Zyon.Core.Sim;

namespace Zyon.Client;

public enum TileLayer { Walls, Blocks }

/// <summary>Máscara de 4 vizinhos (N, L, S, O) e variação estável por posição.</summary>
public static class AutoTile
{
    public const int North = 1;
    public const int East = 2;
    public const int South = 4;
    public const int West = 8;
    public const int MaskCount = 16;

    public static int Mask(IWorldView view, int x, int y, TileLayer layer)
    {
        int mask = 0;
        if (Connects(view, x, y - 1, layer)) mask |= North;
        if (Connects(view, x + 1, y, layer)) mask |= East;
        if (Connects(view, x, y + 1, layer)) mask |= South;
        if (Connects(view, x - 1, y, layer)) mask |= West;
        return mask;
    }

    /// <summary>Índice em [0, count) que só depende da posição — quebra a repetição sem piscar.</summary>
    public static int Variation(int x, int y, int count)
    {
        uint h = unchecked((uint)x * 73856093u ^ (uint)y * 19349663u);
        h ^= h >> 13;
        h = unchecked(h * 0x5bd1e995u);
        h ^= h >> 15;
        return (int)(h % (uint)count);
    }

    /// <summary>Fora do mundo conta como conectado; blocos conectam com qualquer bloco sólido.</summary>
    private static bool Connects(IWorldView view, int x, int y, TileLayer layer)
    {
        if ((uint)x >= (uint)view.Width || (uint)y >= (uint)view.Height) return true;
        if (layer == TileLayer.Walls) return view.GetWall(x, y) != 0;
        ushort block = view.GetBlock(x, y);
        return block != 0 && view.Registry.Block(block).Solid;
    }
}
