using System.Buffers;
using System.Buffers.Binary;
using System.IO.Compression;
using System.Numerics;
using System.Text;
using Zyon.Core.Data;
using Zyon.Core.Tiles;

namespace Zyon.Core.Persistence;

/// <summary>
/// Formato .zworld. Sem compressão: magic "ZYONWLD", versão do formato (int32), tamanho do payload descomprimido
/// (int64) e o hash FNV-1a 64 (uint64) de versão + tamanho + payload. Em seguida o payload comprimido com Brotli: versão do gerador, seed,
/// impressão digital da config, largura, altura, spawn, tick, paleta de blocos e de paredes (só os ids presentes),
/// chunks em RLE, itens caídos e posições dos jogadores.
/// A leitura só lança DataException (arquivo corrompido/inválido) ou SaveIncompatibleException (íntegro, mas não
/// utilizável por esta versão do jogo); nunca carrega conteúdo que não bata com o hash.
/// Versão mais nova: o layout dela é desconhecido, então o hash não pode ser verificado. Só uma versão plausível
/// (até FormatVersion + MaxPlausibleVersionJump) é tratada como "feito por uma versão mais nova"
/// (SaveIncompatibleException); um número absurdo é bit trocado no cabeçalho, ou seja, corrupção (DataException).
/// </summary>
public static class WorldFile
{
    public const int FormatVersion = 1;
    public const string Extension = ".zworld";
    public const int MaxWidth = 16384;
    public const int MaxHeight = 8192;
    public const long MaxPayloadBytes = 512L * 1024 * 1024;
    /// <summary>Quantas versões de formato à frente ainda contam como "save de um jogo mais novo" e não como corrupção.</summary>
    public const int MaxPlausibleVersionJump = 100;
    private const int MaxItems = 1_000_000;
    private const int MaxPlayers = 10_000;

    private static ReadOnlySpan<byte> Magic => "ZYONWLD"u8;

    public static void Write(Stream stream, WorldSave save, Registry registry)
    {
        using var payload = new MemoryStream();
        using (var w = new BinaryWriter(payload, Encoding.UTF8, leaveOpen: true))
            WritePayload(w, save, registry);
        var bytes = payload.GetBuffer().AsSpan(0, (int)payload.Length);

        stream.Write(Magic);
        using (var header = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            header.Write(FormatVersion);
            header.Write((long)bytes.Length);
            header.Write(ContentHash(FormatVersion, bytes.Length, bytes));
        }
        using (var brotli = new BrotliStream(stream, CompressionLevel.Fastest, leaveOpen: true))
            brotli.Write(bytes);

        foreach (var chunk in save.Tiles.Chunks)
            chunk.Dirty = false;
    }

    private static void WritePayload(BinaryWriter w, WorldSave save, Registry registry)
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

    public static WorldSave Read(Stream stream, Registry registry)
    {
        try
        {
            return ReadCore(stream, registry);
        }
        catch (EndOfStreamException ex)
        {
            throw Corrupt("fim inesperado do arquivo", ex);
        }
        catch (Exception ex) when (IsMalformedInput(ex))
        {
            throw Corrupt(ex.Message, ex);
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
        if (version > FormatVersion + MaxPlausibleVersionJump)
            throw Corrupt($"versão de formato {version} implausível");
        if (version > FormatVersion)
            throw Incompatible($"feito por uma versão mais nova do jogo (formato {version}, suportado até {FormatVersion})");
        if (version < 1)
            throw Fail($"versão de formato {version} não suportada");
        // Migrações futuras: ao criar o formato 2, mantenha a leitura do 1 e converta para o 2 aqui.

        long payloadLength = header.ReadInt64();
        ulong expectedHash = header.ReadUInt64();
        if (payloadLength <= 0 || payloadLength > MaxPayloadBytes)
            throw Corrupt($"tamanho do conteúdo inválido ({payloadLength} bytes)");

        var payload = Decompress(stream, (int)payloadLength);
        if (ContentHash(version, payloadLength, payload) != expectedHash)
            throw Corrupt("o conteúdo não confere com o hash gravado");

        using var r = new BinaryReader(new MemoryStream(payload, writable: false), Encoding.UTF8);
        var save = ReadPayload(r, registry);
        if (r.BaseStream.Position != r.BaseStream.Length)
            throw Corrupt("dados extras no fim do conteúdo");
        return save;
    }

    /// <summary>
    /// Descomprime todo o resto do stream. Exige exatamente <paramref name="length"/> bytes descomprimidos e nenhum byte
    /// depois do fim do Brotli. A memória cresce com os dados reais, não com o tamanho declarado no cabeçalho.
    /// </summary>
    private static byte[] Decompress(Stream stream, int length)
    {
        using var compressed = new MemoryStream();
        var copyBuffer = new byte[81920];
        int n;
        while ((n = stream.Read(copyBuffer)) > 0)
        {
            if (compressed.Length + n > MaxPayloadBytes)
                throw Corrupt("conteúdo comprimido grande demais");
            compressed.Write(copyBuffer, 0, n);
        }

        ReadOnlySpan<byte> source = compressed.GetBuffer().AsSpan(0, (int)compressed.Length);
        using var output = new MemoryStream(Math.Min(length, 1 << 20));
        var buffer = new byte[64 * 1024];
        using var decoder = new BrotliDecoder();
        while (true)
        {
            var status = decoder.Decompress(source, buffer, out int consumed, out int written);
            source = source[consumed..];
            if (output.Length + written > length)
                throw Corrupt("conteúdo descomprimido maior que o declarado");
            output.Write(buffer, 0, written);

            if (status == OperationStatus.Done)
            {
                if (!source.IsEmpty) throw Corrupt("dados extras após o conteúdo comprimido");
                break;
            }
            if (status == OperationStatus.NeedMoreData)
                throw new EndOfStreamException("o conteúdo comprimido terminou antes do fim");
            if (status == OperationStatus.InvalidData || (consumed == 0 && written == 0))
                throw Corrupt("dados comprimidos inválidos");
        }

        if (output.Length != length)
            throw Corrupt("conteúdo descomprimido menor que o declarado");
        return output.ToArray();
    }

    private static WorldSave ReadPayload(BinaryReader r, Registry registry)
    {
        int generatorVersion = r.ReadInt32();
        int seed = r.ReadInt32();
        ulong fingerprint = r.ReadUInt64();
        int width = r.ReadInt32();
        int height = r.ReadInt32();
        if (width <= 0 || height <= 0 || width > MaxWidth || height > MaxHeight)
            throw Corrupt($"dimensões inválidas {width}×{height} (máximo {MaxWidth}×{MaxHeight})");
        var spawn = new Vector2(r.ReadSingle(), r.ReadSingle());
        if (!float.IsFinite(spawn.X) || !float.IsFinite(spawn.Y))
            throw Corrupt("ponto de spawn inválido");
        long tick = r.ReadInt64();
        if (tick < 0) throw Corrupt("tick negativo");

        var blockRemap = ReadPalette(r, name =>
            registry.TryGetBlockId(name, out var id) ? id : throw Incompatible($"bloco '{name}' do save não existe mais nos dados do jogo"));
        var wallRemap = ReadPalette(r, name =>
            registry.TryGetWallId(name, out var id) ? id : throw Incompatible($"parede '{name}' do save não existe mais nos dados do jogo"));

        // Cada chunk tem ao menos uma sequência RLE (4 bytes) por camada, e ainda vêm as contagens de itens e jogadores.
        // Rejeita antes de alocar o TileWorld, que é proporcional às dimensões declaradas.
        long chunkCount = (long)((width + Chunk.Size - 1) / Chunk.Size) * ((height + Chunk.Size - 1) / Chunk.Size);
        long minimumRemaining = chunkCount * 2 * 4 + 4 + 4;
        if (r.BaseStream.Length - r.BaseStream.Position < minimumRemaining)
            throw Corrupt($"conteúdo pequeno demais para um mundo {width}×{height}");

        var tiles = new TileWorld(width, height);
        foreach (var chunk in tiles.Chunks)
        {
            ReadRle(r, chunk.Blocks, blockRemap, "bloco");
            ReadRle(r, chunk.Walls, wallRemap, "parede");
            chunk.Dirty = false;
        }

        int itemCount = r.ReadInt32();
        if (itemCount < 0 || itemCount > MaxItems) throw Corrupt($"contagem de itens inválida ({itemCount})");
        var items = new List<SavedDroppedItem>();
        for (int i = 0; i < itemCount; i++)
        {
            var item = new SavedDroppedItem(r.ReadString(), r.ReadInt32(), new Vector2(r.ReadSingle(), r.ReadSingle()), r.ReadInt32(), r.ReadInt32());
            if (!registry.TryGetItemId(item.Item, out var itemId))
                throw Incompatible($"item caído '{item.Item}' do save não existe mais nos dados do jogo");
            int max = registry.Item(itemId).MaxStack;
            if (item.Count < 1 || item.Count > max)
                throw Corrupt($"item caído '{item.Item}' com quantidade {item.Count} fora de 1..{max}");
            if (item.AgeTicks < 0 || item.PickupDelayTicks < 0)
                throw Corrupt($"item caído '{item.Item}' com contador de ticks negativo");
            if (!float.IsFinite(item.Position.X) || !float.IsFinite(item.Position.Y))
                throw Corrupt($"item caído '{item.Item}' com posição inválida");
            items.Add(item);
        }

        int playerCount = r.ReadInt32();
        if (playerCount < 0 || playerCount > MaxPlayers) throw Corrupt($"contagem de jogadores inválida ({playerCount})");
        var positions = new Dictionary<string, Vector2>();
        for (int i = 0; i < playerCount; i++)
        {
            string name = r.ReadString();
            var position = new Vector2(r.ReadSingle(), r.ReadSingle());
            if (!float.IsFinite(position.X) || !float.IsFinite(position.Y))
                throw Corrupt($"posição do jogador '{name}' inválida");
            if (!positions.TryAdd(name, position))
                throw Corrupt($"jogador '{name}' repetido");
        }

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
        var seen = new bool[ushort.MaxValue + 1];
        for (int i = 0; i < count; i++)
        {
            ushort saved = r.ReadUInt16();
            string name = r.ReadString();
            if (saved == 0) throw Corrupt("id 0 na paleta");
            if (seen[saved]) throw Corrupt($"id {saved} repetido na paleta");
            seen[saved] = true;
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

    /// <summary>FNV-1a 64 de versão (int32 LE) + tamanho do payload (int64 LE) + payload descomprimido.</summary>
    private static ulong ContentHash(int version, long payloadLength, ReadOnlySpan<byte> payload)
    {
        Span<byte> header = stackalloc byte[12];
        BinaryPrimitives.WriteInt32LittleEndian(header, version);
        BinaryPrimitives.WriteInt64LittleEndian(header[4..], payloadLength);
        return Fnv1a64(payload, Fnv1a64(header, 14695981039346656037UL));
    }

    private static ulong Fnv1a64(ReadOnlySpan<byte> data, ulong hash)
    {
        foreach (byte b in data)
        {
            hash ^= b;
            hash *= 1099511628211UL;
        }
        return hash;
    }

    private static DataException Fail(string message) => new($"{Extension}: {message}");

    private static SaveIncompatibleException Incompatible(string message) => new($"{Extension}: {message}");

    private static DataException Corrupt(string message) => Fail($"arquivo corrompido — {message}");

    private static DataException Corrupt(string message, Exception inner) => new($"{Extension}: arquivo corrompido — {message}", inner);

    /// <summary>
    /// Exceções que entrada malformada produz (BinaryReader, decodificação UTF-8, TileWorld, E/S no meio da leitura).
    /// Qualquer outra (ex.: NullReferenceException) é bug e sobe inalterada.
    /// </summary>
    private static bool IsMalformedInput(Exception ex) =>
        ex is InvalidDataException or InvalidOperationException or FormatException or OverflowException
            or ArgumentException or IOException;
}
