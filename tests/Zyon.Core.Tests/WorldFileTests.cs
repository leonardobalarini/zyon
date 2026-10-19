using System.IO.Compression;
using System.Numerics;
using System.Text;
using System.Text.Json.Nodes;
using Xunit.Abstractions;
using Zyon.Core.Data;
using Zyon.Core.Persistence;
using Zyon.Core.Tiles;
using Zyon.Core.WorldGen;

namespace Zyon.Core.Tests;

public class WorldFileTests(ITestOutputHelper output)
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
                new SavedDroppedItem("dirt", 5, new Vector2(10.5f, 20.25f), 30, 45),
                new SavedDroppedItem("ore_b", 1, new Vector2(400.125f, 200f), 0, 0),
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

        var ex = Assert.Throws<SaveIncompatibleException>(() => Read(Write(save, r), withoutOreB));
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

        var ex = Assert.Throws<SaveIncompatibleException>(() => Read(bytes, r));
        Assert.Contains(".zworld", ex.Message);
        Assert.Contains("versão mais nova", ex.Message);
    }

    [Fact]
    public void Truncated_ThrowsDataException()
    {
        var r = TestData.Registry();
        var bytes = Write(Sample(r), r);

        var ex = Assert.Throws<DataException>(() => Read(bytes[..(bytes.Length / 2)], r));
        Assert.Contains("corrompido", ex.Message);
        Assert.NotNull(ex.InnerException);
    }

    [Fact]
    public void FlatWorld_CompressesWell()
    {
        var r = TestData.Registry();
        var flat = new WorldSave(Info, TestWorlds.Flat(r, 512, 256, 100), 0, [], new Dictionary<string, Vector2>());

        Assert.True(Write(flat, r).Length < 4096, "RLE + Brotli deveria deixar um mundo plano minúsculo");
    }

    // ---- Resistência a corrupção ----

    /// <summary>magic (7) + versão (4) + tamanho do payload (8) + hash do payload (8).</summary>
    private const int HeaderSize = 27;

    private static ulong Fnv1a64(ReadOnlySpan<byte> data)
    {
        ulong hash = 14695981039346656037UL;
        foreach (byte b in data)
        {
            hash ^= b;
            hash *= 1099511628211UL;
        }
        return hash;
    }

    /// <summary>Monta um .zworld com um payload arbitrário (hash correto), como faria um arquivo hostil.</summary>
    private static byte[] Pack(byte[] payload)
    {
        byte[] hashed = [.. BitConverter.GetBytes(WorldFile.FormatVersion), .. BitConverter.GetBytes((long)payload.Length), .. payload];
        using var ms = new MemoryStream();
        ms.Write("ZYONWLD"u8);
        using (var w = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
        {
            w.Write(WorldFile.FormatVersion);
            w.Write((long)payload.Length);
            w.Write(Fnv1a64(hashed));
        }
        using (var brotli = new BrotliStream(ms, CompressionLevel.Fastest, leaveOpen: true))
            brotli.Write(payload);
        return ms.ToArray();
    }

    private static byte[] Unpack(byte[] file)
    {
        using var brotli = new BrotliStream(new MemoryStream(file, HeaderSize, file.Length - HeaderSize), CompressionMode.Decompress);
        using var ms = new MemoryStream();
        brotli.CopyTo(ms);
        return ms.ToArray();
    }

    private static WorldSave Small(Registry r) =>
        new(Info, TestWorlds.Flat(r, 128, 64, 40), 77,
            [
                new SavedDroppedItem("dirt", 3, new Vector2(10.5f, 39f), 12, 0),
                new SavedDroppedItem("stone", 1, new Vector2(100f, 38.5f), 0, 30),
            ],
            new Dictionary<string, Vector2> { ["Leo"] = new Vector2(64.5f, 38f) });

    private static WorldSave EmptySmall(Registry r) =>
        new(Info, TestWorlds.Flat(r, 64, 64, 40), 0, [], new Dictionary<string, Vector2>());

    [Fact]
    public void Fuzz_OnlyDataExceptionOrIdenticalLoad()
    {
        var r = TestData.Registry();
        var save = Small(r);
        byte[] original = Write(save, r);
        var tileHash = TileWorldHash.Compute(save.Tiles);
        var rng = new Random(1234);
        int rejected = 0, incompatible = 0, identical = 0;

        for (int i = 0; i < 3000; i++)
        {
            byte[] bytes;
            switch (rng.Next(3))
            {
                case 0:
                    bytes = (byte[])original.Clone();
                    int flips = rng.Next(1, 4);
                    for (int f = 0; f < flips; f++)
                        bytes[rng.Next(bytes.Length)] ^= (byte)rng.Next(1, 256);
                    break;
                case 1:
                    bytes = original[..rng.Next(original.Length)];
                    break;
                default:
                    int at = rng.Next(original.Length + 1);
                    var extra = new byte[rng.Next(1, 9)];
                    rng.NextBytes(extra);
                    bytes = [.. original[..at], .. extra, .. original[at..]];
                    break;
            }

            try
            {
                var loaded = Read(bytes, r);
                Assert.Equal(save.Info, loaded.Info);
                Assert.Equal(save.Tick, loaded.Tick);
                Assert.Equal(tileHash, TileWorldHash.Compute(loaded.Tiles));
                Assert.Equal(save.DroppedItems, loaded.DroppedItems);
                Assert.Equal(save.PlayerPositions.OrderBy(p => p.Key), loaded.PlayerPositions.OrderBy(p => p.Key));
                identical++;
            }
            catch (DataException)
            {
                rejected++;
            }
            catch (SaveIncompatibleException)
            {
                int version = bytes.Length >= 11 ? BitConverter.ToInt32(bytes, 7) : 0;
                Assert.True(version > WorldFile.FormatVersion && version <= WorldFile.FormatVersion + WorldFile.MaxPlausibleVersionJump,
                    $"mutação {i}: SaveIncompatibleException sem a versão virar uma versão mais nova plausível ({version})");
                incompatible++;
            }
        }

        output.WriteLine($"rejeitadas: {rejected}, incompatíveis: {incompatible}, idênticas: {identical}");
    }

    [Fact]
    public void HostileHeader_HugeDimensions_FailsFast()
    {
        using var payload = new MemoryStream();
        using (var w = new BinaryWriter(payload, Encoding.UTF8, leaveOpen: true))
        {
            w.Write(1);            // versão do gerador
            w.Write(3);            // seed
            w.Write(0UL);          // impressão digital
            w.Write(30000);        // largura
            w.Write(30000);        // altura
            w.Write(1f);
            w.Write(1f);
            w.Write(0L);           // tick
        }
        byte[] bytes = Pack(payload.ToArray());
        var r = TestData.Registry();

        long before = GC.GetAllocatedBytesForCurrentThread();
        var ex = Assert.Throws<DataException>(() => Read(bytes, r));
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Contains("dimensões", ex.Message);
        Assert.True(allocated < 16 * 1024 * 1024, $"alocou {allocated} bytes para rejeitar o cabeçalho");
    }

    [Theory]
    [InlineData(WorldFile.MaxWidth + 1, 64)]
    [InlineData(64, WorldFile.MaxHeight + 1)]
    public void DimensionsAboveLimit_Throw(int width, int height)
    {
        var r = TestData.Registry();
        var payload = Unpack(Write(EmptySmall(r), r));
        BitConverter.GetBytes(width).CopyTo(payload, 16);
        BitConverter.GetBytes(height).CopyTo(payload, 20);

        var ex = Assert.Throws<DataException>(() => Read(Pack(payload), r));
        Assert.Contains("dimensões", ex.Message);
    }

    [Fact]
    public void HostilePayloadLength_Throws()
    {
        var r = TestData.Registry();
        var bytes = Write(EmptySmall(r), r);
        BitConverter.GetBytes(long.MaxValue).CopyTo(bytes, 11);

        Assert.Throws<DataException>(() => Read(bytes, r));
    }

    [Fact]
    public void TrailingGarbage_Throws()
    {
        var r = TestData.Registry();
        var bytes = Write(EmptySmall(r), r);

        var appended = Assert.Throws<DataException>(() => Read([.. bytes, 1, 2, 3], r));
        Assert.Contains("corrompido", appended.Message);

        var insidePayload = Assert.Throws<DataException>(() => Read(Pack([.. Unpack(bytes), 0, 0, 0, 0]), r));
        Assert.Contains("dados extras", insidePayload.Message);
    }

    [Fact]
    public void ChecksumMismatch_Throws()
    {
        var r = TestData.Registry();
        var payload = Unpack(Write(Small(r), r));
        var genuine = Pack(payload);
        payload[^5] ^= 0x01; // altera a posição do jogador...
        var tampered = Pack(payload);
        genuine.AsSpan(19, 8).CopyTo(tampered.AsSpan(19, 8)); // ...mas mantém o hash do conteúdo original

        var ex = Assert.Throws<DataException>(() => Read(tampered, r));
        Assert.Contains("corrompido", ex.Message);
    }

    [Fact]
    public void DuplicatePlayerName_Throws()
    {
        var r = TestData.Registry();
        var payload = Unpack(Write(EmptySmall(r), r));
        using var ms = new MemoryStream();
        ms.Write(payload.AsSpan(0, payload.Length - 4)); // remove a contagem de jogadores (0)
        using (var w = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
        {
            w.Write(2);
            w.Write("Leo");
            w.Write(1f);
            w.Write(1f);
            w.Write("Leo");
            w.Write(2f);
            w.Write(2f);
        }

        var ex = Assert.Throws<DataException>(() => Read(Pack(ms.ToArray()), r));
        Assert.Contains("Leo", ex.Message);
    }

    [Fact]
    public void DuplicatePaletteId_Throws()
    {
        var r = TestData.Registry();
        var payload = Unpack(Write(EmptySmall(r), r));
        const int paletteStart = 40;
        ushort id;
        string name;
        int afterEntry;
        using (var reader = new BinaryReader(new MemoryStream(payload, paletteStart, payload.Length - paletteStart)))
        {
            Assert.Equal(1, reader.ReadInt32());
            id = reader.ReadUInt16();
            name = reader.ReadString();
            afterEntry = paletteStart + (int)reader.BaseStream.Position;
        }
        using var ms = new MemoryStream();
        ms.Write(payload.AsSpan(0, paletteStart));
        using (var w = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
        {
            w.Write(2);
            w.Write(id);
            w.Write(name);
            w.Write(id);
            w.Write("stone");
        }
        ms.Write(payload.AsSpan(afterEntry));

        var ex = Assert.Throws<DataException>(() => Read(Pack(ms.ToArray()), r));
        Assert.Contains("paleta", ex.Message);
    }

    [Fact]
    public void NonFiniteSpawn_Throws()
    {
        var r = TestData.Registry();
        var save = new WorldSave(Info with { SpawnFeet = new Vector2(float.NaN, 1f) }, TestWorlds.Flat(r, 64, 64, 40), 0, [],
            new Dictionary<string, Vector2>());

        var ex = Assert.Throws<DataException>(() => Read(Write(save, r), r));
        Assert.Contains("spawn", ex.Message);
    }

    [Fact]
    public void NonFinitePlayerPosition_Throws()
    {
        var r = TestData.Registry();
        var save = new WorldSave(Info, TestWorlds.Flat(r, 64, 64, 40), 0, [],
            new Dictionary<string, Vector2> { ["Leo"] = new Vector2(1f, float.PositiveInfinity) });

        var ex = Assert.Throws<DataException>(() => Read(Write(save, r), r));
        Assert.Contains("Leo", ex.Message);
    }
    [Fact]
    public void ImplausibleVersion_IsCorruption()
    {
        var r = TestData.Registry();
        var bytes = Write(EmptySmall(r), r);
        bytes[10] ^= 0x40; // bit trocado no byte mais alto da versão

        var ex = Assert.Throws<DataException>(() => Read(bytes, r));
        Assert.Contains("corrompido", ex.Message);
    }

    [Fact]
    public void HeaderFields_AreCoveredByHash()
    {
        var r = TestData.Registry();
        var payload = Unpack(Write(EmptySmall(r), r));
        var bytes = Pack(payload);
        // Hash calculado só sobre o payload (sem versão e tamanho) não deve mais ser aceito.
        BitConverter.GetBytes(Fnv1a64(payload)).CopyTo(bytes, 19);

        var ex = Assert.Throws<DataException>(() => Read(bytes, r));
        Assert.Contains("hash", ex.Message);
    }

    [Fact]
    public void HostileHeader_MaxDimensionsWithTinyPayload_FailsFast()
    {
        using var payload = new MemoryStream();
        using (var w = new BinaryWriter(payload, Encoding.UTF8, leaveOpen: true))
        {
            w.Write(1);
            w.Write(3);
            w.Write(0UL);
            w.Write(WorldFile.MaxWidth);
            w.Write(WorldFile.MaxHeight);
            w.Write(1f);
            w.Write(1f);
            w.Write(0L);
            w.Write(0); // paleta de blocos vazia
            w.Write(0); // paleta de paredes vazia
            w.Write(0); // nenhum dado de chunk
        }
        byte[] bytes = Pack(payload.ToArray());
        var r = TestData.Registry();

        long before = GC.GetAllocatedBytesForCurrentThread();
        var ex = Assert.Throws<DataException>(() => Read(bytes, r));
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Contains("corrompido", ex.Message);
        Assert.True(allocated < 16 * 1024 * 1024, $"alocou {allocated} bytes para rejeitar o cabeçalho");
    }

    private static WorldSave WithItem(Registry r, SavedDroppedItem item) =>
        new(Info, TestWorlds.Flat(r, 64, 64, 40), 0, [item], new Dictionary<string, Vector2>());

    [Fact]
    public void RemovedDroppedItem_ThrowsIncompatible()
    {
        var r = TestData.Registry();
        var withoutOreB = TestData.RegistryWithEdits(
            b => TestData.RemoveById(b, "ore_b"),
            i => TestData.RemoveById(i, "ore_b"));
        var bytes = Write(WithItem(r, new SavedDroppedItem("ore_b", 1, new Vector2(3f, 3f), 0, 0)), r);

        var ex = Assert.Throws<SaveIncompatibleException>(() => Read(bytes, withoutOreB));
        Assert.Contains(".zworld", ex.Message);
        Assert.Contains("ore_b", ex.Message);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(1000, 0, 0)]
    [InlineData(1, -1, 0)]
    [InlineData(1, 0, -5)]
    public void InvalidDroppedItem_IsCorruption(int count, int age, int pickupDelay)
    {
        var r = TestData.Registry();
        var bytes = Write(WithItem(r, new SavedDroppedItem("dirt", count, new Vector2(3f, 3f), age, pickupDelay)), r);

        var ex = Assert.Throws<DataException>(() => Read(bytes, r));
        Assert.Contains("dirt", ex.Message);
    }
}
