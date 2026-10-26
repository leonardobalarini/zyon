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

    /// <summary>Raízes: tronco do bioma da coluna logo acima da superfície.</summary>
    private static List<int> Roots(WorldGenContext ctx)
    {
        var roots = new List<int>();
        for (int x = 0; x < ctx.World.Width; x++)
        {
            int s = ctx.Surface[x];
            if (s > 0 && ctx.World.GetBlock(x, s - 1) == ctx.Biomes!.At(x, s, s).Tree.Trunk) roots.Add(x);
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

    [Fact]
    public void EveryTreeBlock_IsConnectedToARoot()
    {
        var ctx = Generate(1);
        var world = ctx.World;
        var seen = new HashSet<(int, int)>();
        var queue = new Queue<(int X, int Y)>();
        foreach (int x in Roots(ctx))
        {
            queue.Enqueue((x, ctx.Surface[x] - 1));
            seen.Add((x, ctx.Surface[x] - 1));
        }
        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            foreach (var (nx, ny) in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
                if (ctx.Registry.Block(world.GetBlock(nx, ny)).Tree && seen.Add((nx, ny)))
                    queue.Enqueue((nx, ny));
        }

        for (int y = 0; y < world.Height; y++)
        for (int x = 0; x < world.Width; x++)
            if (ctx.Registry.Block(world.GetBlock(x, y)).Tree)
                Assert.Contains((x, y), seen);
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

    [Fact]
    public void Pine_CrownNarrowsFrom5To1_AboveTheTrunk()
    {
        var tree = TreeOf(TreeShape.Pine);
        var tiles = new List<(int Dx, int Dy, ushort Block)>();

        TreeShapes.Build(tree, new GenRandom(4), tiles);

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
    [InlineData(4)]
    public void Dead_Has2To4BranchesInTheUpperHalf_AndCactusUpTo2Arms(int seed)
    {
        var dead = TreeOf(TreeShape.Dead);
        var tiles = new List<(int Dx, int Dy, ushort Block)>();
        TreeShapes.Build(dead, new GenRandom(seed), tiles);
        int h = tiles.Count(t => t.Block == dead.Trunk);
        var branchRoots = tiles.Where(t => t.Block == dead.Crown && Math.Abs(t.Dx) == 1).ToList();
        Assert.InRange(branchRoots.Count, 1, 4);   // dois galhos no mesmo lado e linha se sobrepõem
        Assert.All(branchRoots, t => Assert.True(-t.Dy >= h / 2 && -t.Dy < h));
        Assert.Contains(branchRoots, t => t.Dx < 0);
        Assert.Contains(branchRoots, t => t.Dx > 0);

        var cactus = TreeOf(TreeShape.Cactus);
        TreeShapes.Build(cactus, new GenRandom(seed), tiles);
        var sides = tiles.Where(t => t.Dx != 0).Select(t => Math.Sign(t.Dx)).Distinct().ToList();
        Assert.True(sides.Count <= 2);
        Assert.All(tiles, t => Assert.InRange(Math.Abs(t.Dx), 0, 1));
    }
}
