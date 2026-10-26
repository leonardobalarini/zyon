using Zyon.Core.Biomes;
using Zyon.Core.WorldGen;
using Zyon.Core.WorldGen.Passes;
using static Zyon.Core.Tests.WorldGenTestKit;

namespace Zyon.Core.Tests;

public class TreesPassTests
{
    private static WorldGenContext Generate(int seed)
    {
        var ctx = Context(1050, 300, seed);
        foreach (var pass in WorldGenerator.DefaultPasses())
            WorldGenerator.RunPass(ctx, pass);
        return ctx;
    }

    private static BiomeTree TreeOf(TreeShape shape) =>
        BiomeSet.Resolve(TestData.WorldGenConfig().Biomes!, TestData.Registry()).All.First(b => b.Tree.Shape == shape).Tree;

    /// <summary>
    /// Raízes plantadas pelo passe (diagnóstico interno do contexto: deduzir pelo mundo é frágil, o braço do cacto usa o
    /// mesmo bloco do tronco). Confere de forma independente que em cada uma há tronco do bioma sobre o bloco de superfície.
    /// </summary>
    private static List<int> Roots(WorldGenContext ctx)
    {
        var roots = ctx.PlantedTreeRoots.ToList();
        foreach (int x in roots)
        {
            int s = ctx.Surface[x];
            var biome = ctx.Biomes!.At(x, s, s);
            Assert.Equal(biome.Tree.Trunk, ctx.World.GetBlock(x, s - 1));
            Assert.Equal(biome.SurfaceBlock, ctx.World.GetBlock(x, s));
        }
        return roots;
    }

    private static (int From, int To) Excluded(WorldGenContext ctx)
    {
        var b = ctx.Bunker!;
        return (b.X - BunkerPass.PlateauWidth - TreesPass.BunkerClearance,
                b.X + b.Structure.Width - 1 + BunkerPass.PlateauWidth + TreesPass.BunkerClearance);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void EveryBiomeGetsTrees_OnItsSurfaceBlock_WithTheDataHeights(int seed)
    {
        var ctx = Generate(seed);
        var roots = Roots(ctx);

        foreach (var biome in ctx.Biomes!.Set.All)
            Assert.Contains(roots, x => ctx.Biomes.At(x, ctx.Surface[x], ctx.Surface[x]) == biome);
        foreach (int x in roots)
        {
            int s = ctx.Surface[x];
            var biome = ctx.Biomes.At(x, s, s);
            Assert.Equal(biome.SurfaceBlock, ctx.World.GetBlock(x, s));
            int trunk = 0;
            while (ctx.World.GetBlock(x, s - 1 - trunk) == biome.Tree.Trunk) trunk++;
            if (biome.Tree.Shape != TreeShape.Cactus)   // no cacto o braço usa o mesmo bloco e pode emendar no topo
                Assert.InRange(trunk, biome.Tree.HeightMin, biome.Tree.HeightMax);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Trees_RespectSpacing_AndStayAwayFromTheBunker(int seed)
    {
        var ctx = Generate(seed);
        var roots = Roots(ctx);
        var (from, to) = Excluded(ctx);

        for (int i = 1; i < roots.Count; i++)
        {
            int x = roots[i];
            var biome = ctx.Biomes!.At(x, ctx.Surface[x], ctx.Surface[x]);
            Assert.True(x - roots[i - 1] >= biome.TreeSpacing, $"árvores em {roots[i - 1]} e {x}");
        }
        for (int x = Math.Max(0, from); x <= Math.Min(ctx.World.Width - 1, to); x++)
        for (int y = 0; y < ctx.World.Height; y++)
        {
            var def = ctx.Registry.Block(ctx.World.GetBlock(x, y));
            Assert.False(def.Tree, $"árvore em ({x}, {y}) perto do bunker");
            Assert.DoesNotContain(def.Id, ctx.Biomes!.Set.All.Select(b => b.Decoration));
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void EveryTreeComponent_HasExactlyOneRoot(int seed)
    {
        var ctx = Generate(seed);
        var world = ctx.World;
        var roots = Roots(ctx).Select(x => (x, ctx.Surface[x] - 1)).ToHashSet();
        var seen = new HashSet<(int, int)>();
        int components = 0;

        for (int y = 0; y < world.Height; y++)
        for (int x = 0; x < world.Width; x++)
        {
            if (!ctx.Registry.Block(world.GetBlock(x, y)).Tree || !seen.Add((x, y))) continue;
            components++;
            int rootsInside = 0;
            var queue = new Queue<(int X, int Y)>();
            queue.Enqueue((x, y));
            while (queue.Count > 0)
            {
                var (cx, cy) = queue.Dequeue();
                if (roots.Contains((cx, cy))) rootsInside++;
                foreach (var (nx, ny) in new[] { (cx + 1, cy), (cx - 1, cy), (cx, cy + 1), (cx, cy - 1) })
                    if (world.InBounds(nx, ny) && ctx.Registry.Block(world.GetBlock(nx, ny)).Tree && seen.Add((nx, ny)))
                        queue.Enqueue((nx, ny));
            }
            Assert.True(rootsInside == 1, $"componente de árvore perto de ({x}, {y}) com {rootsInside} raízes");
        }
        Assert.Equal(roots.Count, components);
    }

    [Fact]
    public void Decorations_SitOnTheSurfaceBlockOfTheirBiome()
    {
        var ctx = Generate(2);
        int found = 0;
        for (int x = 0; x < ctx.World.Width; x++)
        {
            int s = ctx.Surface[x];
            var biome = ctx.Biomes!.At(x, s, s);
            if (s == 0 || ctx.World.GetBlock(x, s - 1) != biome.Decoration) continue;
            found++;
            Assert.Equal(biome.SurfaceBlock, ctx.World.GetBlock(x, s));
        }
        Assert.True(found > 10, $"só {found} decorativos");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Pine_CrownNarrowsFrom5To1_AboveTheTrunk(int seed)
    {
        var tree = TreeOf(TreeShape.Pine);
        var tiles = new List<(int Dx, int Dy, ushort Block)>();

        TreeShapes.Build(tree, new GenRandom(seed), tiles);

        int h = tiles.Count(t => t.Block == tree.Trunk);
        var crownRows = tiles.Where(t => t.Block == tree.Crown).GroupBy(t => t.Dy).OrderByDescending(g => g.Key).ToList();
        int Width(IGrouping<int, (int Dx, int Dy, ushort Block)> row) => row.Max(t => t.Dx) - row.Min(t => t.Dx) + 1;
        Assert.Equal(5, Width(crownRows.First()));
        Assert.Equal(1, Width(crownRows.Last()));
        Assert.Equal(-h, crownRows.Last().Key);   // ponta um tile acima do tronco
        for (int i = 1; i < crownRows.Count; i++) Assert.True(Width(crownRows[i]) <= Width(crownRows[i - 1]));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void BiomeEdges_TaperWithDepth_ShallowBlockThenDefaultStone(int seed)
    {
        // Faixa [Left, Right] com a divisa deslocada por ±EdgeNoise (6) e, a d linhas da superfície, d / Taper (4)
        // colunas para dentro. Coluna Right − 12: de dirtBottom (d ≈ 7–17) até 10 linhas abaixo (d < 28 → até 6 para
        // dentro) a divisa fica em ≥ Right − 6 − 6 = Right − 12, ainda dentro da faixa (rocha rasa do bioma). Com d ≥ 80
        // (20 para dentro) a divisa fica em ≤ Right + 6 − 20 = Right − 14 < Right − 12, já fora: rocha rasa do padrão.
        // Simetria na borda esquerda com Left + 12. Cavernas e minérios abrem tiles: conta em janelas de linhas e em colunas vizinhas (para dentro da faixa, ainda mais longe da divisa).
        var ctx = Generate(seed);
        var map = ctx.Biomes!;
        var band = map.Bands.First(b => b.Biome.ShallowBlock != map.Set.Default.ShallowBlock);
        string inner = ctx.Registry.Block(band.Biome.ShallowBlock).Name, stone = ctx.Registry.Block(map.Set.Default.ShallowBlock).Name;
        Assert.Equal(6, map.Set.EdgeNoise);
        Assert.Equal(4, map.Set.Taper);

        foreach (int x in new[] { band.Right - 12, band.Left + 12 })
        {
            int s = ctx.Surface[x];
            int Count(int from, int to, ushort block) =>
                Enumerable.Range(from, to - from).Count(y => ctx.World.GetBlock(x, y) == block);

            // cavernas grandes abrem trechos inteiros: soma as colunas x … x + 4 para dentro da faixa
            int toward = x == band.Right - 12 ? -1 : 1, shallow = 0;
            for (int k = 0; k < 5; k++)
                for (int y = ctx.DirtBottom[x + toward * k]; y < ctx.DirtBottom[x + toward * k] + 10; y++)
                    if (ctx.World.GetBlock(x + toward * k, y) == band.Biome.ShallowBlock) shallow++;
            Assert.True(shallow >= 1, $"sem {inner} logo abaixo da camada de cima perto de x={x}");

            int deep = s + 80;
            Assert.True(deep + 6 < ctx.DeepStart[x]);
            Assert.Equal(0, Count(deep, deep + 6, band.Biome.ShallowBlock));
            Assert.True(Count(deep, deep + 6, map.Set.Default.ShallowBlock) >= 1, $"sem {stone} fundo em x={x}");
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Dead_Has2To4BranchesInTheUpperHalf_AndCactusUpTo2ArmsOnDistinctSides(int seed)
    {
        var dead = TreeOf(TreeShape.Dead);
        var tiles = new List<(int Dx, int Dy, ushort Block)>();
        TreeShapes.Build(dead, new GenRandom(seed), tiles);
        int h = tiles.Count(t => t.Block == dead.Trunk);
        var branchRoots = tiles.Where(t => t.Block == dead.Crown && Math.Abs(t.Dx) == 1).ToList();
        Assert.InRange(branchRoots.Count, 2, 4);
        Assert.All(branchRoots, t => Assert.True(-t.Dy >= h / 2 && -t.Dy < h));
        Assert.Contains(branchRoots, t => t.Dx < 0);
        Assert.Contains(branchRoots, t => t.Dx > 0);

        var cactus = TreeOf(TreeShape.Cactus);
        TreeShapes.Build(cactus, new GenRandom(seed), tiles);
        var shape = tiles.Select(t => (t.Dx, t.Dy)).ToHashSet();
        var arms = tiles.Where(t => t.Dx != 0 && !shape.Contains((t.Dx, t.Dy + 1))).Select(t => Math.Sign(t.Dx)).ToList();   // base do braço
        Assert.True(arms.Count <= 2);
        if (arms.Count == 2) Assert.NotEqual(arms[0], arms[1]);
        Assert.All(tiles, t => Assert.InRange(Math.Abs(t.Dx), 0, 1));
    }
}
