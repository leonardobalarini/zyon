using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace Zyon.ArtGen;

/// <summary>PNG RGBA 8 bits. Grava sem filtro; lê os 5 filtros do formato.</summary>
public static class Png
{
    private static readonly byte[] Signature = [137, 80, 78, 71, 13, 10, 26, 10];
    private static readonly uint[] CrcTable = BuildCrcTable();

    public static byte[] Encode(Canvas image)
    {
        using var output = new MemoryStream();
        output.Write(Signature);

        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, image.Width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), image.Height);
        header[8] = 8;   // bits por canal
        header[9] = 6;   // RGBA
        WriteChunk(output, "IHDR", header);

        int stride = image.Width * 4;
        var raw = new byte[image.Height * (stride + 1)];
        for (int y = 0; y < image.Height; y++)
            image.Pixels.AsSpan(y * stride, stride).CopyTo(raw.AsSpan(y * (stride + 1) + 1));   // filtro 0
        using (var compressed = new MemoryStream())
        {
            using (var z = new ZLibStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
                z.Write(raw);
            WriteChunk(output, "IDAT", compressed.ToArray());
        }
        WriteChunk(output, "IEND", []);
        return output.ToArray();
    }

    public static Canvas Decode(byte[] png)
    {
        if (png.Length < 8 || !png.AsSpan(0, 8).SequenceEqual(Signature))
            throw new InvalidDataException("não é um PNG");
        int pos = 8, width = 0, height = 0;
        using var idat = new MemoryStream();
        while (pos + 8 <= png.Length)
        {
            int length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(pos));
            string type = Encoding.ASCII.GetString(png, pos + 4, 4);
            var data = png.AsSpan(pos + 8, length);
            if (type == "IHDR")
            {
                width = BinaryPrimitives.ReadInt32BigEndian(data);
                height = BinaryPrimitives.ReadInt32BigEndian(data[4..]);
                if (data[8] != 8 || data[9] != 6 || data[12] != 0)
                    throw new InvalidDataException("só PNG RGBA 8 bits sem entrelaçamento");
            }
            else if (type == "IDAT") idat.Write(data);
            else if (type == "IEND") break;
            pos += 12 + length;
        }
        if (width <= 0 || height <= 0) throw new InvalidDataException("PNG sem IHDR");

        idat.Position = 0;
        using var z = new ZLibStream(idat, CompressionMode.Decompress);
        int stride = width * 4;
        var raw = new byte[height * (stride + 1)];
        z.ReadExactly(raw);

        var image = new Canvas(width, height);
        var prev = new byte[stride];
        var line = new byte[stride];
        for (int y = 0; y < height; y++)
        {
            byte filter = raw[y * (stride + 1)];
            raw.AsSpan(y * (stride + 1) + 1, stride).CopyTo(line);
            for (int i = 0; i < stride; i++)
            {
                int a = i >= 4 ? line[i - 4] : 0, b = prev[i], c = i >= 4 ? prev[i - 4] : 0;
                line[i] = filter switch
                {
                    0 => line[i],
                    1 => (byte)(line[i] + a),
                    2 => (byte)(line[i] + b),
                    3 => (byte)(line[i] + (a + b) / 2),
                    4 => (byte)(line[i] + Paeth(a, b, c)),
                    _ => throw new InvalidDataException($"filtro PNG desconhecido {filter}"),
                };
            }
            line.CopyTo(image.Pixels, y * stride);
            (prev, line) = (line, prev);
        }
        return image;
    }

    private static int Paeth(int a, int b, int c)
    {
        int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }

    private static void WriteChunk(Stream output, string type, ReadOnlySpan<byte> data)
    {
        Span<byte> buf = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(buf, data.Length);
        output.Write(buf);
        var typeBytes = Encoding.ASCII.GetBytes(type);
        output.Write(typeBytes);
        output.Write(data);
        uint crc = Crc(Crc(0xFFFFFFFFu, typeBytes), data) ^ 0xFFFFFFFFu;
        BinaryPrimitives.WriteUInt32BigEndian(buf, crc);
        output.Write(buf);
    }

    private static uint Crc(uint crc, ReadOnlySpan<byte> data)
    {
        foreach (byte b in data) crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc;
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            table[n] = c;
        }
        return table;
    }
}
