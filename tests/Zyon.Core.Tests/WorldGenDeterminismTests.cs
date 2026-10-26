using Zyon.Core.Tiles;
using Zyon.Core.WorldGen;

namespace Zyon.Core.Tests;

public class WorldGenDeterminismTests
{
    private static WorldGenerator Generator() => new(TestData.Registry(), TestData.WorldGenConfig());

    [Fact]
    public void DefaultPasses_AreInSpecOrder()
    {
        Assert.Equal(
            new[] { "terrain", "mountain", "biomes", "layers", "caves", "ores", "walls", "bunker", "trees" },
            Generator().Passes.Select(p => p.Name));
    }

    [Fact]
    public void SameSeed_ProducesIdenticalWorld()
    {
        var a = Generator().Generate(512, 256, 1234);
        var b = Generator().Generate(512, 256, 1234);

        Assert.Equal(TileWorldHash.Compute(a.Tiles), TileWorldHash.Compute(b.Tiles));
        Assert.Equal(a.SpawnFeet, b.SpawnFeet);
    }

    [Fact]
    public void DifferentSeeds_ProduceDifferentWorlds()
    {
        var a = Generator().Generate(512, 256, 1);
        var b = Generator().Generate(512, 256, 2);

        Assert.NotEqual(TileWorldHash.Compute(a.Tiles), TileWorldHash.Compute(b.Tiles));
    }

    // Valores de referência para detectar mudanças silenciosas na geração (ex.: o algoritmo do
    // GenRandom, mudanças na FastNoiseLite ou no runtime do .NET).
    // Se este teste falhar após uma atualização do .NET, da FastNoiseLite ou de uma mudança
    // intencional na geração: incremente WorldGenerator.Version (quando for intencional) e
    // atualize as constantes abaixo com os novos valores.
    private const ulong GoldenHash = 0xF8ECD1FD3F974C95UL;
    private static readonly System.Numerics.Vector2 GoldenSpawnFeet = new(281.5f, 76f);

    [Fact]
    public void GoldenHash_512x256_Seed1234()
    {
        var world = Generator().Generate(512, 256, 1234);
        var hash = TileWorldHash.Compute(world.Tiles);

        Assert.Equal(GoldenHash, hash);
        Assert.Equal(GoldenSpawnFeet, world.SpawnFeet);
    }
}
