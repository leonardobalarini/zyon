namespace Zyon.ArtGen;

/// <summary>Decorativos de 1 tile (blocos não sólidos): o mesmo desenho em todas as células, fundo transparente.</summary>
public static class PropGenerator
{
    public static readonly string[] Shapes = ["lamp", "crate", "pipe", "vent", "panel"];

    /// <summary>Formas que usam a cor de destaque (luz da luminária, tela e luzinha do painel).</summary>
    public static readonly string[] ShapesWithAccent = ["lamp", "panel"];

    public static void Draw(Canvas img, int ox, int oy, string shape, Rgba[] pal, Rgba[]? accent)
    {
        void Rect(int x0, int y0, int x1, int y1, Rgba c)
        {
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                img.Set(ox + x, oy + y, c);
        }

        switch (shape)
        {
            case "lamp":   // presa ao teto: suporte, corpo e vidro aceso embaixo
                Rect(4, 0, 11, 1, pal[0]);
                Rect(5, 2, 10, 3, pal[1]);
                Rect(5, 2, 10, 2, pal[2]);
                Rect(4, 4, 11, 4, pal[0]);
                Rect(5, 4, 10, 4, accent![1]);
                Rect(6, 5, 9, 5, accent[2]);
                break;
            case "crate":  // apoiado no chão: moldura, ripa do meio e travessa diagonal
                Rect(2, 4, 13, 15, pal[0]);
                Rect(3, 5, 12, 14, pal[1]);
                Rect(3, 9, 12, 10, pal[0]);
                for (int i = 0; i < 10; i++) img.Set(ox + 3 + i, oy + 14 - i, pal[2]);
                break;
            case "pipe":   // atravessa o tile na horizontal, com uma flange
                Rect(0, 6, 15, 6, pal[2]);
                Rect(0, 7, 15, 8, pal[1]);
                Rect(0, 9, 15, 9, pal[0]);
                Rect(6, 5, 7, 10, pal[0]);
                Rect(6, 5, 6, 10, pal[2]);
                break;
            case "vent":   // grade com aletas
                Rect(2, 3, 13, 12, pal[0]);
                for (int y = 4; y <= 11; y++) Rect(3, y, 12, y, y % 2 == 0 ? pal[2] : pal[0]);
                break;
            case "panel":  // caixa com tela, botões e luzinha
                Rect(3, 2, 12, 13, pal[0]);
                Rect(4, 3, 11, 12, pal[1]);
                Rect(5, 4, 10, 7, accent![0]);
                Rect(6, 5, 7, 5, accent[1]);
                Rect(5, 10, 6, 10, pal[2]);
                img.Set(ox + 9, oy + 10, accent[1]);
                img.Set(ox + 10, oy + 10, accent[2]);
                break;
            default:
                throw new ArgumentException($"forma '{shape}' desconhecida", nameof(shape));
        }
    }
}
