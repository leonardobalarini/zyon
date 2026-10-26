using Zyon.Core.Biomes;

namespace Zyon.Core.WorldGen.Passes;

/// <summary>
/// Tiles de uma árvore a partir da base: (dx, dy, bloco), dy negativo para cima, (0, 0) = tile logo acima do chão.
/// Pode repetir um tile (dois galhos iguais); quem planta confere o espaço livre antes.
/// </summary>
internal static class TreeShapes
{
    public static void Build(BiomeTree tree, GenRandom rng, List<(int Dx, int Dy, ushort Block)> tiles)
    {
        tiles.Clear();
        int h = rng.Next(tree.HeightMin, tree.HeightMax + 1);
        for (int i = 0; i < h; i++) tiles.Add((0, -i, tree.Trunk));
        switch (tree.Shape)
        {
            case TreeShape.Dead: DeadBranches(tree, h, rng, tiles); break;
            case TreeShape.Pine: PineCrown(tree, h, tiles); break;
            default: CactusArms(tree, h, rng, tiles); break;
        }
    }

    /// <summary>2 a 4 galhos de 1–2 tiles, alternando o lado, na metade de cima do tronco.</summary>
    private static void DeadBranches(BiomeTree tree, int h, GenRandom rng, List<(int, int, ushort)> tiles)
    {
        int count = rng.Next(2, 5);
        int side = rng.Next(2) == 0 ? -1 : 1;
        int lowest = h / 2;
        for (int i = 0; i < count; i++, side = -side)
        {
            int row = lowest + rng.Next(h - lowest);
            int length = rng.Next(1, 3);
            for (int k = 1; k <= length; k++) tiles.Add((side * k, -row, tree.Crown));
        }
    }

    /// <summary>Copa triangular nos 2/3 de cima do tronco: 5 tiles de largura embaixo até 1 na ponta, um tile acima do tronco.</summary>
    private static void PineCrown(BiomeTree tree, int h, List<(int, int, ushort)> tiles)
    {
        int start = h / 3, top = h, span = top - start;
        for (int row = start; row <= top; row++)
        {
            int half = (2 * (top - row) + span / 2) / span;   // 2 embaixo → 0 na ponta
            for (int dx = -half; dx <= half; dx++)
                if (dx != 0 || row == top) tiles.Add((dx, -row, tree.Crown));
        }
    }

    /// <summary>Até 2 braços em lados diferentes: 1 tile para o lado e 1–2 para cima, acima do chão e abaixo do topo.</summary>
    private static void CactusArms(BiomeTree tree, int h, GenRandom rng, List<(int, int, ushort)> tiles)
    {
        int arms = rng.Next(3);
        int side = rng.Next(2) == 0 ? -1 : 1;
        for (int i = 0; i < arms; i++, side = -side)
        {
            int row = 1 + rng.Next(h - 2);
            int up = rng.Next(1, 3);
            for (int k = 0; k <= up; k++) tiles.Add((side, -row - k, tree.Crown));
        }
    }
}
