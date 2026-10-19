using System.Numerics;
using System.Text.Json.Nodes;
using Zyon.Core.Data;
using Zyon.Core.Persistence;
using Zyon.Core.Tiles;
using Zyon.Core.WorldGen;

namespace Zyon.Core.Tests;

public class WorldFileTests
{
    private static readonly WorldInfo Info = new(3, 1, 0xABCDEFUL, new Vector2(257f, 75f));

    private static byte[] Write(WorldSave save, Registry registry)
    {
        using var ms = new MemoryStream();
        WorldFile.Write(ms, save, registry);
        return ms.ToArray();
    }

    private static WorldSave Read(byte[] bytes, Registry registry) => WorldFile.Read(new MemoryStream(bytes), registry);

    private static WorldSave Sample(Registry r)
    {
        var tiles = new WorldGenerator(r, TestData.WorldGenConfig()).Generate(512, 256, 3).Tiles;
        return new WorldSave(Info, tiles, 1234,
            [
                new SavedDroppedItem("dirt", 5, new Vector2(10.5f, 20.25f), 30),
                new SavedDroppedItem("ore_b", 1, new Vector2(400.125f, 200f), 0),
            ],
            new Dictionary<string, Vector2> { ["Leo"] = new Vector2(100.5f, 60.625f), ["Ana"] = new Vector2(1f, 2f) });
    }

    [Fact]
    public void RoundTrip_PreservesEverything()
    {
        var r = TestData.Registry();
        var save = Sample(r);

        var loaded = Read(Write(save, r), r);

        Assert.Equal(save.Info, loaded.Info);
        Assert.Equal(save.Tick, loaded.Tick);
        Assert.Equal(TileWorldHash.Compute(save.Tiles), TileWorldHash.Compute(loaded.Tiles));
        Assert.Equal(save.DroppedItems, loaded.DroppedItems);
        Assert.Equal(save.PlayerPositions.OrderBy(p => p.Key), loaded.PlayerPositions.OrderBy(p => p.Key));
    }

    [Fact]
    public void Write_ClearsDirty_AndLoadedWorldIsClean()
    {
        var r = TestData.Registry();
        var save = Sample(r);
        save.Tiles.SetBlock(5, 5, r.BlockId("dirt"));

        var bytes = Write(save, r);

        Assert.All(save.Tiles.Chunks, c => Assert.False(c.Dirty));
        Assert.All(Read(bytes, r).Tiles.Chunks, c => Assert.False(c.Dirty));
    }

    [Fact]
    public void FileStartsWithMagicAndVersion()
    {
        var r = TestData.Registry();

        var bytes = Write(Sample(r), r);

        Assert.Equal("ZYONWLD"u8.ToArray(), bytes[..7]);
        Assert.Equal(WorldFile.FormatVersion, BitConverter.ToInt32(bytes, 7));
    }

    [Fact]
    public void Palette_RemapsIdsWhenDataOrderChanges()
    {
        var original = TestData.Registry();
        var reordered = TestData.RegistryWithEdits(editBlocks: blocks =>
        {
            var copy = blocks.Select(n => n!.DeepClone()).Reverse().ToList();
            blocks.Clear();
            foreach (var n in copy) blocks.Add(n);
        });
        var save = Sample(original);

        var loaded = Read(Write(save, original), reordered);

        for (int y = 0; y < save.Tiles.Height; y += 3)
        for (int x = 0; x < save.Tiles.Width; x += 3)
            Assert.Equal(
                original.Block(save.Tiles.GetBlock(x, y)).Name,
                reordered.Block(loaded.Tiles.GetBlock(x, y)).Name);
    }

    [Fact]
    public void RemovedUnusedBlock_StillLoads()
    {
        var r = TestData.Registry();
        var flat = new WorldSave(Info, TestWorlds.Flat(r, 64, 64, 40), 0, [], new Dictionary<string, Vector2>());
        var withoutOreB = TestData.RegistryWithEdits(
            b => TestData.RemoveById(b, "ore_b"),
            i => TestData.RemoveById(i, "ore_b"));

        var loaded = Read(Write(flat, r), withoutOreB);

        Assert.Equal(withoutOreB.BlockId("dirt"), loaded.Tiles.GetBlock(0, 40));
    }

    [Fact]
    public void RemovedUsedBlock_Throws()
    {
        var r = TestData.Registry();
        var tiles = TestWorlds.Flat(r, 64, 64, 40);
        tiles.SetBlock(3, 50, r.BlockId("ore_b"));
        var save = new WorldSave(Info, tiles, 0, [], new Dictionary<string, Vector2>());
        var withoutOreB = TestData.RegistryWithEdits(
            b => TestData.RemoveById(b, "ore_b"),
            i => TestData.RemoveById(i, "ore_b"));

        var ex = Assert.Throws<DataException>(() => Read(Write(save, r), withoutOreB));
        Assert.Contains(".zworld", ex.Message);
        Assert.Contains("ore_b", ex.Message);
    }

    [Fact]
    public void BadMagic_Throws()
    {
        var r = TestData.Registry();
        var bytes = Write(Sample(r), r);
        bytes[0] = (byte)'X';

        var ex = Assert.Throws<DataException>(() => Read(bytes, r));
        Assert.Contains("não é um arquivo de mundo", ex.Message);
    }

    [Fact]
    public void FutureVersion_Throws()
    {
        var r = TestData.Registry();
        var bytes = Write(Sample(r), r);
        BitConverter.GetBytes(99).CopyTo(bytes, 7);

        var ex = Assert.Throws<DataException>(() => Read(bytes, r));
        Assert.Contains("versão mais nova", ex.Message);
    }

    [Fact]
    public void Truncated_ThrowsDataException()
    {
        var r = TestData.Registry();
        var bytes = Write(Sample(r), r);

        var ex = Assert.Throws<DataException>(() => Read(bytes[..(bytes.Length / 2)], r));
        Assert.Contains("corrompido", ex.Message);
    }

    [Fact]
    public void FlatWorld_CompressesWell()
    {
        var r = TestData.Registry();
        var flat = new WorldSave(Info, TestWorlds.Flat(r, 512, 256, 100), 0, [], new Dictionary<string, Vector2>());

        Assert.True(Write(flat, r).Length < 4096, "RLE + Brotli deveria deixar um mundo plano minúsculo");
    }
}
