using System.IO.Compression;
using System.Numerics;
using System.Text;
using Zyon.Core.Data;
using Zyon.Core.Tiles;

namespace Zyon.Core.Persistence;

/// <summary>
/// Formato .zworld. Sem compressão: magic "ZYONWLD" + versão do formato (int32).
/// Comprimido com Brotli: versão do gerador, seed, impressão digital da config, largura, altura, spawn, tick,
/// paleta de blocos e de paredes (só os ids presentes), chunks em RLE, itens caídos e posições dos jogadores.
/// </summary>
public static class WorldFile
{
    public const int FormatVersion = 1;
    public const string Extension = ".zworld";
    private const int MaxDimension = 100_000;

    private static ReadOnlySpan<byte> Magic => "ZYONWLD"u8;

    public static void Write(Stream stream, WorldSave save, Registry registry)
    {
        stream.Write(Magic);
        using (var header = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
            header.Write(FormatVersion);

        using (var brotli = new BrotliStream(stream, CompressionLevel.Fastest, leaveOpen: true))
        using (var w = new BinaryWriter(brotli, Encoding.UTF8, leaveOpen: true))
        {
            var info = save.Info;
            var tiles = save.Tiles;
            w.Write(info.GeneratorVersion);
            w.Write(info.Seed);
            w.Write(info.ConfigFingerprint);
            w.Write(tiles.Width);
            w.Write(tiles.Height);
            w.Write(info.SpawnFeet.X);
            w.Write(info.SpawnFeet.Y);
            w.Write(save.Tick);

            WritePalette(w, UsedIds(tiles, c => c.Blocks), id => registry.Block(id).Name);
            WritePalette(w, UsedIds(tiles, c => c.Walls), id => registry.Wall(id).Name);
            foreach (var chunk in tiles.Chunks)
            {
                WriteRle(w, chunk.Blocks);
                WriteRle(w, chunk.Walls);
            }

            w.Write(save.DroppedItems.Count);
            foreach (var item in save.DroppedItems)
            {
                w.Write(item.Item);
                w.Write(item.Count);
                w.Write(item.Position.X);
                w.Write(item.Position.Y);
                w.Write(item.AgeTicks);
                w.Write(item.PickupDelayTicks);
            }

            w.Write(save.PlayerPositions.Count);
            foreach (var (name, position) in save.PlayerPositions.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                w.Write(name);
                w.Write(position.X);
                w.Write(position.Y);
            }
        }

        foreach (var chunk in save.Tiles.Chunks)
            chunk.Dirty = false;
    }

    public static WorldSave Read(Stream stream, Registry registry)
    {
        try
        {
            return ReadCore(stream, registry);
        }
        catch (EndOfStreamException)
        {
            throw Corrupt("fim inesperado do arquivo");
        }
        catch (InvalidDataException ex)
        {
            throw Corrupt(ex.Message);
        }
    }

    private static WorldSave ReadCore(Stream stream, Registry registry)
    {
        Span<byte> magic = stackalloc byte[7];
        stream.ReadExactly(magic);
        if (!magic.SequenceEqual(Magic))
            throw Fail("não é um arquivo de mundo do Zyon");

        using var header = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        int version = header.ReadInt32();
        if (version > FormatVersion)
            throw Fail($"feito por uma versão mais nova do jogo (formato {version}, suportado até {FormatVersion})");
        if (version < 1)
            throw Fail($"versão de formato {version} não suportada");
        // Migrações futuras: ao criar o formato 2, mantenha a leitura do 1 e converta para o 2 aqui.

        using var brotli = new BrotliStream(stream, CompressionMode.Decompress, leaveOpen: true);
        using var r = new BinaryReader(brotli, Encoding.UTF8, leaveOpen: true);

        int generatorVersion = r.ReadInt32();
        int seed = r.ReadInt32();
        ulong fingerprint = r.ReadUInt64();
        int width = r.ReadInt32();
        int height = r.ReadInt32();
        if (width <= 0 || height <= 0 || width > MaxDimension || height > MaxDimension)
            throw Corrupt($"dimensões inválidas {width}×{height}");
        var spawn = new Vector2(r.ReadSingle(), r.ReadSingle());
        long tick = r.ReadInt64();
        if (tick < 0) throw Corrupt("tick negativo");

        var blockRemap = ReadPalette(r, name =>
            registry.TryGetBlockId(name, out var id) ? id : throw Fail($"bloco '{name}' do save não existe mais nos dados do jogo"));
        var wallRemap = ReadPalette(r, name =>
            registry.TryGetWallId(name, out var id) ? id : throw Fail($"parede '{name}' do save não existe mais nos dados do jogo"));

        var tiles = new TileWorld(width, height);
        foreach (var chunk in tiles.Chunks)
        {
            ReadRle(r, chunk.Blocks, blockRemap, "bloco");
            ReadRle(r, chunk.Walls, wallRemap, "parede");
            chunk.Dirty = false;
        }

        int itemCount = r.ReadInt32();
        if (itemCount < 0) throw Corrupt("contagem de itens negativa");
        var items = new List<SavedDroppedItem>(itemCount);
        for (int i = 0; i < itemCount; i++)
            items.Add(new SavedDroppedItem(r.ReadString(), r.ReadInt32(), new Vector2(r.ReadSingle(), r.ReadSingle()), r.ReadInt32(), r.ReadInt32()));

        int playerCount = r.ReadInt32();
        if (playerCount < 0) throw Corrupt("contagem de jogadores negativa");
        var positions = new Dictionary<string, Vector2>(playerCount);
        for (int i = 0; i < playerCount; i++)
            positions[r.ReadString()] = new Vector2(r.ReadSingle(), r.ReadSingle());

        return new WorldSave(new WorldInfo(seed, generatorVersion, fingerprint, spawn), tiles, tick, items, positions);
    }

    private static List<ushort> UsedIds(TileWorld tiles, Func<Chunk, ushort[]> layer)
    {
        var used = new bool[ushort.MaxValue + 1];
        foreach (var chunk in tiles.Chunks)
            foreach (ushort id in layer(chunk))
                used[id] = true;
        var ids = new List<ushort>();
        for (int id = 1; id < used.Length; id++)
            if (used[id]) ids.Add((ushort)id);
        return ids;
    }

    private static void WritePalette(BinaryWriter w, List<ushort> ids, Func<ushort, string> name)
    {
        w.Write(ids.Count);
        foreach (ushort id in ids)
        {
            w.Write(id);
            w.Write(name(id));
        }
    }

    /// <summary>Tabela id-do-save → id-atual (0 = ausente da paleta).</summary>
    private static ushort[] ReadPalette(BinaryReader r, Func<string, ushort> resolve)
    {
        int count = r.ReadInt32();
        if (count < 0 || count > ushort.MaxValue) throw Corrupt("paleta inválida");
        var remap = new ushort[ushort.MaxValue + 1];
        for (int i = 0; i < count; i++)
        {
            ushort saved = r.ReadUInt16();
            string name = r.ReadString();
            if (saved == 0) throw Corrupt("id 0 na paleta");
            remap[saved] = resolve(name);
        }
        return remap;
    }

    private static void WriteRle(BinaryWriter w, ushort[] data)
    {
        int i = 0;
        while (i < data.Length)
        {
            ushort value = data[i];
            int run = 1;
            while (i + run < data.Length && data[i + run] == value) run++;
            w.Write(value);
            w.Write((ushort)run);
            i += run;
        }
    }

    private static void ReadRle(BinaryReader r, ushort[] target, ushort[] remap, string kind)
    {
        int i = 0;
        while (i < target.Length)
        {
            ushort saved = r.ReadUInt16();
            int run = r.ReadUInt16();
            if (run == 0 || i + run > target.Length) throw Corrupt("sequência RLE inválida");
            ushort id = 0;
            if (saved != 0)
            {
                id = remap[saved];
                if (id == 0) throw Corrupt($"{kind} {saved} ausente da paleta");
            }
            Array.Fill(target, id, i, run);
            i += run;
        }
    }

    private static DataException Fail(string message) => new($"{Extension}: {message}");

    private static DataException Corrupt(string message) => Fail($"arquivo corrompido — {message}");
}
