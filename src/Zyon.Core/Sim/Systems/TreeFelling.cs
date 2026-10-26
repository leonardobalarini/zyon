using Zyon.Core.Data;
using Zyon.Core.Tiles;

namespace Zyon.Core.Sim.Systems;

/// <summary>
/// Derrubada de árvore, disparada só ao quebrar um tronco ("trunk"): os blocos com "tree" ligados (4 vizinhos, só passando
/// por blocos com "tree") ao tile quebrado que estejam na linha dele ou acima. O que está abaixo (o toco) fica; não há
/// regra de sustentação. O tile cortado conta no limite: no máximo MaxBlocks blocos por derrubada, ele incluso.
/// </summary>
internal static class TreeFelling
{
    /// <summary>Limite de blocos por derrubada, contando o tile cortado.</summary>
    public const int MaxBlocks = 256;

    private static readonly (int Dx, int Dy)[] Neighbors = [(1, 0), (-1, 0), (0, -1), (0, 1)];

    /// <summary>Preenche result com os tiles a derrubar a partir de (x, y), que já foi quebrado (não entra no resultado, mas conta no limite: devolve no máximo MaxBlocks - 1).</summary>
    public static void Collect(TileWorld tiles, Registry registry, int x, int y, List<(int X, int Y)> result)
    {
        result.Clear();
        var seen = new HashSet<(int, int)> { (x, y) };
        var queue = new Queue<(int X, int Y)>();
        queue.Enqueue((x, y));
        while (queue.Count > 0)
        {
            var (cx, cy) = queue.Dequeue();
            foreach (var (dx, dy) in Neighbors)
            {
                int nx = cx + dx, ny = cy + dy;
                if (ny > y || !seen.Add((nx, ny)) || !registry.Block(tiles.GetBlock(nx, ny)).Tree) continue;
                result.Add((nx, ny));
                if (result.Count == MaxBlocks - 1) return;
                queue.Enqueue((nx, ny));
            }
        }
    }
}
