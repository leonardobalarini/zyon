namespace Zyon.ArtGen;

/// <summary>Decorativos de 1 tile (blocos não sólidos, inclusive partes de árvore): o mesmo desenho em todas as células, fundo transparente.</summary>
public static class PropGenerator
{
    public static readonly string[] Shapes = ["lamp", "crate", "pipe", "vent", "panel", "trunk", "branch", "leaves", "cactus", "grass", "shrub", "bush"];

    /// <summary>Formas que usam a cor de destaque (luz da luminária, tela e luzinha do painel, neve no arbusto).</summary>
    public static readonly string[] ShapesWithAccent = ["lamp", "panel", "shrub"];

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
            case "trunk":   // atravessa o tile na vertical (o tronco de cima continua o de baixo), com dois nós
                Rect(5, 0, 10, 15, pal[1]);
                Rect(5, 0, 5, 15, pal[0]);
                Rect(10, 0, 10, 15, pal[0]);
                Rect(7, 0, 7, 15, pal[2]);
                Rect(8, 5, 9, 6, pal[0]);
                Rect(6, 11, 7, 12, pal[0]);
                break;
            case "branch":  // atravessa o tile na horizontal, com um graveto para cima
                Rect(0, 8, 15, 10, pal[1]);
                Rect(0, 8, 15, 8, pal[2]);
                Rect(0, 10, 15, 10, pal[0]);
                for (int i = 0; i < 5; i++) img.Set(ox + 9 + i, oy + 7 - i, pal[1]);
                break;
            case "leaves":  // agulhas de pinheiro em fiadas de 4 px com recortes: folhas vizinhas formam a copa
                for (int y = 0; y < 16; y++)
                for (int x = 0; x < 16; x++)
                {
                    int band = y % 4;
                    if (band == 0 && (x + y / 4) % 4 == 0) continue;
                    img.Set(ox + x, oy + y, band == 3 ? pal[0] : (x + y) % 5 == 0 ? pal[2] : pal[1]);
                }
                break;
            case "cactus":  // atravessa o tile na vertical: gomos com sulco escuro e espinhos claros nas bordas
                Rect(4, 0, 11, 15, pal[1]);
                Rect(4, 0, 4, 15, pal[0]);
                Rect(11, 0, 11, 15, pal[0]);
                Rect(7, 0, 7, 15, pal[0]);
                Rect(5, 0, 5, 15, pal[2]);
                Rect(9, 0, 9, 15, pal[2]);
                for (int y = 2; y < 13; y += 4)
                {
                    img.Set(ox + 3, oy + y, pal[2]);
                    img.Set(ox + 12, oy + y + 2, pal[2]);
                }
                break;
            case "grass":   // capim seco: seis lâminas finas saindo do chão, as mais altas tortas
                for (int i = 0; i < 6; i++)
                {
                    int x = 1 + i * 2 + i / 3, h = 4 + i * 5 % 7;
                    for (int y = 0; y < h; y++)
                    {
                        int bend = y > h / 2 ? (i % 2 == 0 ? 1 : -1) : 0;
                        img.Set(ox + x + bend, oy + 15 - y, y == h - 1 ? pal[2] : i % 2 == 0 ? pal[1] : pal[0]);
                    }
                }
                break;
            case "shrub":   // arbusto congelado: copa baixa e oval sobre um tronquinho, com neve em cima
                Rect(7, 12, 8, 15, pal[0]);
                for (int y = 6; y <= 12; y++)
                for (int x = 3; x <= 12; x++)
                {
                    int dx = 2 * x - 15, dy = 2 * y - 18;
                    if (dx * dx + dy * dy * 2 > 100) continue;
                    img.Set(ox + x, oy + y, (x + y) % 3 == 0 ? pal[0] : pal[1]);
                }
                Rect(5, 6, 10, 6, accent![2]);
                Rect(4, 7, 11, 7, accent[1]);
                break;
            case "bush":    // arbusto seco: cinco galhos finos em leque, sem folhas
                for (int i = 0; i < 5; i++)
                {
                    int dir = i - 2;
                    for (int s = 0; s < 8; s++)
                        img.Set(ox + 7 + dir * s / 3, oy + 15 - s, s >= 6 ? pal[2] : s % 2 == 0 ? pal[1] : pal[0]);
                }
                break;
            default:
                throw new ArgumentException($"forma '{shape}' desconhecida", nameof(shape));
        }
    }
}
