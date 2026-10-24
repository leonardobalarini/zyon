namespace Zyon.ArtGen;

/// <summary>Imagem RGBA 8 bits, linha por linha. Começa transparente.</summary>
public sealed class Canvas
{
    public Canvas(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        Width = width;
        Height = height;
        Pixels = new byte[width * height * 4];
    }

    public int Width { get; }
    public int Height { get; }
    public byte[] Pixels { get; }

    public bool Contains(int x, int y) => (uint)x < (uint)Width && (uint)y < (uint)Height;

    public Rgba Get(int x, int y)
    {
        int i = (y * Width + x) * 4;
        return new Rgba(Pixels[i], Pixels[i + 1], Pixels[i + 2], Pixels[i + 3]);
    }

    /// <summary>Escreve o pixel; fora da imagem é ignorado.</summary>
    public void Set(int x, int y, Rgba c)
    {
        if (!Contains(x, y)) return;
        int i = (y * Width + x) * 4;
        Pixels[i] = c.R;
        Pixels[i + 1] = c.G;
        Pixels[i + 2] = c.B;
        Pixels[i + 3] = c.A;
    }

    /// <summary>Copia um retângulo de outra imagem (inclusive pixels transparentes).</summary>
    public void Blit(Canvas source, int sx, int sy, int width, int height, int dx, int dy)
    {
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
            Set(dx + x, dy + y, source.Get(sx + x, sy + y));
    }

    /// <summary>Contorno: todo pixel transparente vizinho (4 lados) de um opaco, dentro do retângulo, vira a cor dada.</summary>
    public void Outline(int x0, int y0, int width, int height, Rgba color)
    {
        var marks = new List<(int, int)>();
        for (int y = y0; y < y0 + height; y++)
        for (int x = x0; x < x0 + width; x++)
        {
            if (Get(x, y).A != 0) continue;
            bool near = (x > x0 && Get(x - 1, y).A != 0) || (x < x0 + width - 1 && Get(x + 1, y).A != 0)
                     || (y > y0 && Get(x, y - 1).A != 0) || (y < y0 + height - 1 && Get(x, y + 1).A != 0);
            if (near) marks.Add((x, y));
        }
        foreach (var (x, y) in marks) Set(x, y, color);
    }
}
