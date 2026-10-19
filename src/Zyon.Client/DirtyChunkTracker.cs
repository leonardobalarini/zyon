using Zyon.Core.Tiles;

namespace Zyon.Client;

/// <summary>
/// Chunks cuja malha precisa ser refeita. Um tile na borda de um chunk também suja o vizinho,
/// porque o autotiling do vizinho depende dele.
/// </summary>
public sealed class DirtyChunkTracker(int chunksX, int chunksY)
{
    private readonly HashSet<(int, int)> pending = new();
    private readonly Queue<(int, int)> order = new();

    public int Count => pending.Count;

    public void MarkTile(int x, int y)
    {
        int cx = x >> Chunk.Shift, cy = y >> Chunk.Shift;
        int lx = x & Chunk.Mask, ly = y & Chunk.Mask;
        Mark(cx, cy);
        if (lx == 0) Mark(cx - 1, cy);
        if (lx == Chunk.Mask) Mark(cx + 1, cy);
        if (ly == 0) Mark(cx, cy - 1);
        if (ly == Chunk.Mask) Mark(cx, cy + 1);
    }

    public bool TryTake(out (int Cx, int Cy) chunk)
    {
        if (order.TryDequeue(out chunk))
        {
            pending.Remove(chunk);
            return true;
        }
        return false;
    }

    private void Mark(int cx, int cy)
    {
        if ((uint)cx >= (uint)chunksX || (uint)cy >= (uint)chunksY) return;
        if (pending.Add((cx, cy)))
            order.Enqueue((cx, cy));
    }
}
