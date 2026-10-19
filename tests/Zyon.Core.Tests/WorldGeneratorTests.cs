using System.Numerics;
using Zyon.Core.Sim;
using Zyon.Core.Tiles;
using Zyon.Core.WorldGen;

namespace Zyon.Core.Tests;

public class WorldGeneratorTests
{
    /// <summary>Escreve 200 blocos em posições sorteadas pelo Random do passe.</summary>
    private sealed class ScatterPass(string name, ushort block) : IWorldGenPass
    {
        public string Name => name;

        public void Apply(WorldGenContext ctx)
        {
            for (int i = 0; i < 200; i++)
                ctx.World.SetBlock(ctx.Random.Next(ctx.World.Width), ctx.Random.Next(ctx.World.Height), block);
        }
    }

    /// <summary>Consome aleatoriedade sem escrever nada.</summary>
    private sealed class ConsumeRandomPass : IWorldGenPass
    {
        public string Name => "consume";

        public void Apply(WorldGenContext ctx)
        {
            for (int i = 0; i < 1000; i++) ctx.Random.Next();
        }
    }

    private sealed class FixedSpawnPass : IWorldGenPass
    {
        public string Name => "spawn";

        public void Apply(WorldGenContext ctx)
        {
            ctx.Report(0.5f);
            ctx.SetSpawn(new Vector2(10.5f, 20f));
        }
    }

    private static WorldGenerator Generator(params IWorldGenPass[] passes) =>
        new(TestData.Registry(), TestData.WorldGenConfig(), passes);

    [Fact]
    public void Generate_RunsPassesAndReturnsSpawn()
    {
        var gen = Generator(new ScatterPass("a", 1), new FixedSpawnPass());

        var world = gen.Generate(128, 64, seed: 3);

        Assert.Equal(128, world.Tiles.Width);
        Assert.Equal(3, world.Seed);
        Assert.Equal(WorldGenerator.Version, world.GeneratorVersion);
        Assert.Equal(new Vector2(10.5f, 20f), world.SpawnFeet);
        Assert.Contains(world.Tiles.Chunks, c => c.Blocks.Contains((ushort)1));
    }

    [Fact]
    public void SameSeed_SameWorld_DifferentSeed_DifferentWorld()
    {
        var gen = Generator(new ScatterPass("a", 1), new FixedSpawnPass());

        ulong a = TileWorldHash.Compute(gen.Generate(128, 64, 5).Tiles);
        ulong b = TileWorldHash.Compute(gen.Generate(128, 64, 5).Tiles);
        ulong c = TileWorldHash.Compute(gen.Generate(128, 64, 6).Tiles);

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }

    [Fact]
    public void InsertingAPass_DoesNotChangeTheOthersRandomness()
    {
        var original = Generator(new ScatterPass("a", 1), new ScatterPass("b", 2), new FixedSpawnPass());
        var withExtra = Generator(new ScatterPass("a", 1), new ConsumeRandomPass(), new ScatterPass("b", 2), new FixedSpawnPass());

        Assert.Equal(
            TileWorldHash.Compute(original.Generate(128, 64, 9).Tiles),
            TileWorldHash.Compute(withExtra.Generate(128, 64, 9).Tiles));
    }

    [Fact]
    public void Progress_IsReportedPerPass_MonotonicAndEndsAtOne()
    {
        var gen = Generator(new ScatterPass("a", 1), new FixedSpawnPass());
        var progress = new ListProgress<WorldGenProgress>();

        gen.Generate(128, 64, 1, progress);

        Assert.Equal("a", progress.Items[0].Pass);
        Assert.Equal(0f, progress.Items[0].Fraction);
        Assert.Contains(progress.Items, p => p.Pass == "spawn" && p.Fraction == 0.75f);
        Assert.Equal(1f, progress.Items[^1].Fraction);
        Assert.All(progress.Items, p => Assert.Equal(2, p.PassCount));
        for (int i = 1; i < progress.Items.Count; i++)
            Assert.True(progress.Items[i].Fraction >= progress.Items[i - 1].Fraction);
    }

    [Fact]
    public void Generate_ClearsDirtyFlags()
    {
        var gen = Generator(new ScatterPass("a", 1), new FixedSpawnPass());

        var world = gen.Generate(128, 64, 1);

        Assert.All(world.Tiles.Chunks, c => Assert.False(c.Dirty));
    }

    [Fact]
    public void Generate_Cancelled_Throws()
    {
        var gen = Generator(new ScatterPass("a", 1), new FixedSpawnPass());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() => gen.Generate(128, 64, 1, cancellationToken: cts.Token));
    }

    [Fact]
    public void Generate_WithoutSpawnPass_Throws()
    {
        var gen = Generator(new ScatterPass("a", 1));

        Assert.Throws<InvalidOperationException>(() => gen.Generate(128, 64, 1));
    }

    [Fact]
    public void Generate_TooSmall_Throws()
    {
        var gen = Generator(new FixedSpawnPass());

        Assert.Throws<ArgumentOutOfRangeException>(() => gen.Generate(WorldGenerator.MinSize - 1, 128, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => gen.Generate(128, WorldGenerator.MinSize - 1, 1));
    }

    [Theory]
    [InlineData(WorldSize.Small, 4200, 1200)]
    [InlineData(WorldSize.Medium, 6400, 1800)]
    [InlineData(WorldSize.Large, 8400, 2400)]
    public void WorldSizes_MatchSpec(WorldSize size, int width, int height)
    {
        Assert.Equal((width, height), WorldSizes.Dimensions(size));
    }

    [Fact]
    public void PlayerSpawnCenter_IsHalfAPlayerAboveTheFeet()
    {
        var gen = Generator(new FixedSpawnPass());
        var world = gen.Generate(128, 64, 1);
        var physics = TestData.Registry().Physics;

        var center = world.PlayerSpawnCenter(physics);

        Assert.Equal(new Vector2(10.5f, 20f - physics.PlayerHeight / 2), center);
    }
}
