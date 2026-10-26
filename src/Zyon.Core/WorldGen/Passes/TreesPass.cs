namespace Zyon.Core.WorldGen.Passes;

/// <summary>
/// Árvores e decoração sobre a superfície, depois do bunker. Da esquerda para a direita, em cada coluna cuja
/// superfície é o bloco de cima do bioma: com a chance do bioma e a treeSpacing colunas da última árvore, planta a
/// árvore do bioma se todos os tiles dela estiverem livres (ar, dentro do mundo, fora da área do bunker); senão,
/// com a chance de decoração, põe o decorativo sobre o chão. A área do bunker (com o teto e o patamar, mais
/// BunkerClearance colunas de folga) fica sem nada.
/// </summary>
internal sealed class TreesPass : IWorldGenPass
{
    public const int BunkerClearance = 2;

    private readonly List<(int Dx, int Dy, ushort Block)> shape = [];

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
                if (Fits(world, x, y - 1, skipFrom, skipTo))
                {
                    foreach (var (dx, dy, block) in shape) world.SetBlock(x + dx, y - 1 + dy, block);
                    lastTree = x;
                    continue;
                }
            }
            if (rng.NextDouble() < biome.DecorationChance && world.GetBlock(x, y - 1) == 0)
                world.SetBlock(x, y - 1, biome.Decoration);
        }
    }

    private bool Fits(Zyon.Core.Tiles.TileWorld world, int baseX, int baseY, int skipFrom, int skipTo)
    {
        foreach (var (dx, dy, _) in shape)
        {
            int tx = baseX + dx, ty = baseY + dy;
            if (!world.InBounds(tx, ty) || world.GetBlock(tx, ty) != 0 || (tx >= skipFrom && tx <= skipTo)) return false;
        }
        return true;
    }
}
