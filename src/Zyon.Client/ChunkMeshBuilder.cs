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
    private static readonly (int Dx, int Dy)[] SideOffsets = [(0, -1), (1, 0), (0, 1), (-1, 0)];   // N, L, S, O

    /// <summary>Um quad por tile não vazio da camada (linha do atlas = rows[id]). Paredes atrás de blocos sólidos são puladas.</summary>
    public static void Build(IWorldView view, int chunkX, int chunkY, TileLayer layer, TileAtlasLayout atlas, IReadOnlyList<int> rows, ChunkMeshData output)
    {
        output.Clear();
        int x0 = chunkX * Chunk.Size, y0 = chunkY * Chunk.Size;
        for (int ly = 0; ly < Chunk.Size; ly++)
        for (int lx = 0; lx < Chunk.Size; lx++)
        {
            int x = x0 + lx, y = y0 + ly;
            if (x >= view.Width || y >= view.Height) continue;
            ushort id = layer == TileLayer.Blocks ? view.GetBlock(x, y) : view.GetWall(x, y);
            if (id == 0) continue;
            if (layer == TileLayer.Walls && view.Registry.Block(view.GetBlock(x, y)).Solid) continue;
            var uv = atlas.Uv(rows[id], AutoTile.Mask(view, x, y, layer), AutoTile.Variation(x, y, atlas.Variations));
            AddQuad(output, lx, ly, uv);
        }
    }

    /// <summary>
    /// Bordas de mescla: para cada bloco sólido com rank R e cada vizinho ortogonal sólido com rank menor (mais mole),
    /// um quad com a borda do vizinho naquele lado, por cima do tile.
    /// </summary>
    public static void BuildMerge(IWorldView view, int chunkX, int chunkY, MergeAtlasLayout atlas, IReadOnlyList<int> rankById, ChunkMeshData output)
    {
        output.Clear();
        int x0 = chunkX * Chunk.Size, y0 = chunkY * Chunk.Size;
        for (int ly = 0; ly < Chunk.Size; ly++)
        for (int lx = 0; lx < Chunk.Size; lx++)
        {
            int x = x0 + lx, y = y0 + ly;
            if (x >= view.Width || y >= view.Height) continue;
            int rank = rankById[view.GetBlock(x, y)];
            if (rank <= 0) continue;   // não mescla, ou é o mais mole de todos
            for (int side = 0; side < MergeAtlasLayout.Sides; side++)
            {
                int nx = x + SideOffsets[side].Dx, ny = y + SideOffsets[side].Dy;
                if ((uint)nx >= (uint)view.Width || (uint)ny >= (uint)view.Height) continue;
                int neighbour = rankById[view.GetBlock(nx, ny)];
                if (neighbour < 0 || neighbour >= rank) continue;
                AddQuad(output, lx, ly, atlas.Uv(neighbour, side, AutoTile.Variation(x, y, atlas.Variations)));
            }
        }
    }

    private static void AddQuad(ChunkMeshData output, int lx, int ly, (float U0, float V0, float U1, float V1) uv)
    {
        const float s = RenderMath.PixelsPerTile;
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
