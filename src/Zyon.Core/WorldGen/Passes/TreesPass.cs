using Zyon.Core.Data;
using Zyon.Core.Tiles;

namespace Zyon.Core.WorldGen.Passes;

/// <summary>
/// Árvores e decoração sobre a superfície, depois do bunker. Da esquerda para a direita, em cada coluna cuja
/// superfície é o bloco de cima do bioma: com a chance do bioma e a treeSpacing colunas da última árvore, planta a
/// árvore do bioma se todos os tiles dela estiverem livres (ar, dentro do mundo, fora da área do bunker) e nenhum
/// vizinho de 4 deles (fora da própria árvore) for bloco de árvore — árvores nunca se encostam, senão a derrubada
/// (flood fill) levaria a vizinha; se não couber,
/// com a chance de decoração, põe o decorativo sobre o chão. A área do bunker (com o teto e o patamar, mais
/// BunkerClearance colunas de folga) fica sem nada.
/// </summary>
internal sealed class TreesPass : IWorldGenPass
{
    public const int BunkerClearance = 2;

    public string Name => "trees";

    public void Apply(WorldGenContext ctx)
    {
        var world = ctx.World;
        var biomes = ctx.Biomes ?? throw new InvalidOperationException("biomas não carregados: use WorldGenConfigLoader.LoadFromDirectory");
        var rng = ctx.Random;
        int skipFrom = int.MaxValue, skipTo = int.MinValue;
        if (ctx.Bunker is { } bunker)
        {
            skipFrom = bunker.X - BunkerPass.PlateauWidth - BunkerClearance;
            skipTo = bunker.X + bunker.Structure.Width - 1 + BunkerPass.PlateauWidth + BunkerClearance;
        }

        var shape = new List<(int Dx, int Dy, ushort Block)>();
        int lastTree = int.MinValue / 2;
        for (int x = 0; x < world.Width; x++)
        {
            if (x >= skipFrom && x <= skipTo) continue;
            int y = ctx.Surface[x];
            if (y == 0) continue;
            var biome = biomes.At(x, y, y);
            if (world.GetBlock(x, y) != biome.SurfaceBlock) continue;

            if (x - lastTree >= biome.TreeSpacing && rng.NextDouble() < biome.TreeChance)
            {
                TreeShapes.Build(biome.Tree, rng, shape);
                if (Fits(world, ctx.Registry, shape, x, y - 1, skipFrom, skipTo))
                {
                    foreach (var (dx, dy, block) in shape) world.SetBlock(x + dx, y - 1 + dy, block);
                    ctx.PlantedTreeRoots.Add(x);
                    lastTree = x;
                    continue;
                }
            }
            if (rng.NextDouble() < biome.DecorationChance && world.GetBlock(x, y - 1) == 0)
                world.SetBlock(x, y - 1, biome.Decoration);
        }
    }

    private static bool Fits(TileWorld world, Registry registry, List<(int Dx, int Dy, ushort Block)> shape,
        int baseX, int baseY, int skipFrom, int skipTo)
    {
        var own = new HashSet<(int, int)>();
        foreach (var (dx, dy, _) in shape)
        {
            int tx = baseX + dx, ty = baseY + dy;
            if (!world.InBounds(tx, ty) || world.GetBlock(tx, ty) != 0 || (tx >= skipFrom && tx <= skipTo)) return false;
            own.Add((tx, ty));
        }
        foreach (var (tx, ty) in own)
        foreach (var (nx, ny) in new[] { (tx + 1, ty), (tx - 1, ty), (tx, ty + 1), (tx, ty - 1) })
            if (!own.Contains((nx, ny)) && world.InBounds(nx, ny) && registry.Block(world.GetBlock(nx, ny)).Tree) return false;
        return true;
    }
}
