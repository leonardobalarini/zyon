using System.Numerics;
using Zyon.Core.Sim;
using Zyon.Core.Tiles;

namespace Zyon.Client;

/// <summary>Buffers reutilizáveis de uma malha de chunk (posições em pixels relativas à origem do chunk).</summary>
public sealed class ChunkMeshData
{
    public List<Vector2> Vertices { get; } = new();
    public List<Vector2> Uvs { get; } = new();
    public List<int> Indices { get; } = new();
    public int QuadCount => Vertices.Count / 4;

    public void Clear()
    {
        Vertices.Clear();
        Uvs.Clear();
        Indices.Clear();
    }
}

public static class ChunkMeshBuilder
{
    /// <summary>Um quad por tile não vazio da camada. Paredes escondidas atrás de blocos são puladas.</summary>
    public static void Build(IWorldView view, int chunkX, int chunkY, TileLayer layer, TileAtlasLayout atlas, ChunkMeshData output)
    {
        output.Clear();
        int x0 = chunkX * Chunk.Size, y0 = chunkY * Chunk.Size;
        const float s = RenderMath.PixelsPerTile;

        for (int ly = 0; ly < Chunk.Size; ly++)
        for (int lx = 0; lx < Chunk.Size; lx++)
        {
            int x = x0 + lx, y = y0 + ly;
            if (x >= view.Width || y >= view.Height) continue;
            ushort id = layer == TileLayer.Blocks ? view.GetBlock(x, y) : view.GetWall(x, y);
            if (id == 0) continue;
            if (layer == TileLayer.Walls && view.GetBlock(x, y) != 0) continue;

            int mask = AutoTile.Mask(view, x, y, layer);
            var uv = atlas.Uv(id, mask, AutoTile.Variation(x, y, atlas.Variations));
            float px = lx * s, py = ly * s;
            int b = output.Vertices.Count;

            output.Vertices.Add(new Vector2(px, py));
            output.Vertices.Add(new Vector2(px + s, py));
            output.Vertices.Add(new Vector2(px + s, py + s));
            output.Vertices.Add(new Vector2(px, py + s));
            output.Uvs.Add(new Vector2(uv.U0, uv.V0));
            output.Uvs.Add(new Vector2(uv.U1, uv.V0));
            output.Uvs.Add(new Vector2(uv.U1, uv.V1));
            output.Uvs.Add(new Vector2(uv.U0, uv.V1));
            output.Indices.Add(b);
            output.Indices.Add(b + 1);
            output.Indices.Add(b + 2);
            output.Indices.Add(b);
            output.Indices.Add(b + 2);
            output.Indices.Add(b + 3);
        }
    }
}
