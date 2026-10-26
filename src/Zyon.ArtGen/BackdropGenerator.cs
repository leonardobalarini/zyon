using Zyon.Client;

namespace Zyon.ArtGen;

/// <summary>
/// Fundos em parallax (480×180, repetíveis na horizontal): silhuetas em 2 tons (forma e realce) sobre transparente.
/// Tudo é função de x módulo a largura — relevo por pontos de controle periódicos e objetos desenhados com volta —,
/// então a coluna 0 continua a última. A camada distante (near = false) e a próxima de cada estilo:
/// floresta: prédios em ruínas atrás de morros / árvores mortas; neve: montanhas com neve / morros com pinheiros;
/// deserto: dunas altas / dunas baixas com carcaças de veículos.
/// </summary>
public static class BackdropGenerator
{
    public const int Width = ArtManifest.BackgroundWidth;
    public const int Height = ArtManifest.BackgroundHeight;

    public static readonly string[] Styles = ["forest", "snow", "desert"];

    public static Canvas Draw(string style, bool near, Rgba[] colors, int seed)
    {
        int s = Array.IndexOf(Styles, style);
        if (s < 0) throw new ArgumentException($"estilo '{style}' desconhecido", nameof(style));
        var img = new Canvas(Width, Height);
        int salt = seed * 1000 + s * 10 + (near ? 1 : 0);
        Rgba shape = colors[0], light = colors[1];
        switch (style, near)
        {
            case ("forest", false):
                Buildings(img, shape, light, salt, count: 7, minHeight: 40, maxHeight: 90);
                Ridge(img, shape, light, salt, step: 60, baseY: 150, amplitude: 25, cap: 1);
                break;
            case ("forest", true):
                DeadTrees(img, shape, light, Ridge(img, shape, light, salt, step: 80, baseY: 165, amplitude: 12, cap: 1), salt, count: 8);
                break;
            case ("snow", false):
                Ridge(img, shape, light, salt, step: 60, baseY: 150, amplitude: 90, cap: 8);
                break;
            case ("snow", true):
                Pines(img, shape, light, Ridge(img, shape, light, salt, step: 80, baseY: 160, amplitude: 30, cap: 2), salt, count: 10);
                break;
            case ("desert", false):
                Ridge(img, shape, light, salt, step: 120, baseY: 150, amplitude: 50, cap: 2);
                break;
            case ("desert", true):
                Wrecks(img, shape, light, Ridge(img, shape, light, salt, step: 96, baseY: 165, amplitude: 20, cap: 1), salt, count: 3);
                break;
            default:
                throw new ArgumentException($"estilo '{style}' sem camada {(near ? "próxima" : "distante")}", nameof(style));
        }
        return img;
    }

    /// <summary>Altura do relevo em x (0..amplitude): pontos de controle a cada step px, periódicos na largura, ligados por smoothstep inteiro.</summary>
    public static int RidgeHeight(int x, int step, int amplitude, int salt)
    {
        if (step < 1 || Width % step != 0)
            throw new ArgumentException($"step {step} deve ser positivo e dividir a largura {Width} (senão a emenda não é periódica)", nameof(step));
        int n = Width / step;
        x = Mod(x, Width);
        int i = x / step, t = x % step;
        int a = ArtHash.Range(i % n, 0, salt, amplitude + 1);
        int b = ArtHash.Range((i + 1) % n, 0, salt, amplitude + 1);
        long smooth = (long)t * t * (3 * step - 2 * t);
        return a + (int)((b - a) * smooth / ((long)step * step * step));
    }

    /// <summary>Escreve o pixel com x dado a volta na largura (y fora da imagem é ignorado).</summary>
    public static void PutWrapped(Canvas img, int x, int y, Rgba c) => img.Set(Mod(x, Width), y, c);

    private static int Mod(int x, int m) => ((x % m) + m) % m;

    /// <summary>Relevo cheio até a base da imagem; as cap primeiras linhas de todo o relevo levam o realce. Devolve o topo de cada coluna.</summary>
    private static int[] Ridge(Canvas img, Rgba shape, Rgba light, int salt, int step, int baseY, int amplitude, int cap)
    {
        var top = new int[Width];
        for (int x = 0; x < Width; x++)
        {
            int h = RidgeHeight(x, step, amplitude, salt);
            top[x] = baseY - h;
            for (int y = top[x]; y < Height; y++)
                img.Set(x, y, y < top[x] + cap ? light : shape);
        }
        return top;
    }

    /// <summary>Prédios em ruínas até a base: topo quebrado em trechos de 3 colunas, aresta esquerda clara e janelas vazadas.</summary>
    private static void Buildings(Canvas img, Rgba shape, Rgba light, int salt, int count, int minHeight, int maxHeight)
    {
        for (int k = 0; k < count; k++)
        {
            int x0 = ArtHash.Range(k, 1, salt, Width);
            int w = 14 + ArtHash.Range(k, 2, salt, 20);
            int top = Height - (minHeight + ArtHash.Range(k, 3, salt, maxHeight - minHeight + 1));
            for (int dx = 0; dx < w; dx++)
            {
                int chunk = k * 64 + dx / 3;
                int columnTop = top + (ArtHash.Range(chunk, 4, salt, 100) < 35 ? ArtHash.Range(chunk, 5, salt, 8) : 0);
                for (int y = columnTop; y < Height; y++)
                {
                    int lx = dx % 6, ly = (y - top) % 8;
                    bool window = dx > 1 && dx < w - 2 && lx is 2 or 3 && ly is >= 3 and <= 5
                                  && ArtHash.Chance(k * 64 + dx / 6, (y - top) / 8, salt + 6, 40);
                    if (window) continue;
                    PutWrapped(img, x0 + dx, y, dx == 0 ? light : shape);
                }
            }
        }
    }

    /// <summary>Árvores mortas sobre o relevo: tronco de 2 px com realce e três galhos inclinados, alternando o lado.</summary>
    private static void DeadTrees(Canvas img, Rgba shape, Rgba light, int[] top, int salt, int count)
    {
        for (int k = 0; k < count; k++)
        {
            int x0 = ArtHash.Range(k, 1, salt, Width);
            int h = 22 + ArtHash.Range(k, 2, salt, 24);
            int ground = top[x0] + 2;
            for (int y = ground - h; y <= ground; y++)
            {
                PutWrapped(img, x0, y, y % 3 == 0 ? light : shape);
                PutWrapped(img, x0 + 1, y, shape);
            }
            for (int b = 0; b < 3; b++)
            {
                int by = ground - h + 4 + b * h / 4;
                int dir = (k + b) % 2 == 0 ? 1 : -1;
                int length = 6 + ArtHash.Range(k, 10 + b, salt, 7);
                for (int i = 0; i < length; i++)
                    PutWrapped(img, x0 + (dir > 0 ? 2 : -1) + dir * i, by - i / 2, shape);
            }
        }
    }

    /// <summary>Pinheiros sobre o relevo: triângulo serrilhado com faixas de neve do lado esquerdo e um toco embaixo.</summary>
    private static void Pines(Canvas img, Rgba shape, Rgba light, int[] top, int salt, int count)
    {
        for (int k = 0; k < count; k++)
        {
            int x0 = ArtHash.Range(k, 1, salt, Width);
            int h = 18 + ArtHash.Range(k, 2, salt, 18);
            int ground = top[x0] + 2;
            for (int r = 0; r < h; r++)
            {
                int y = ground - h + r;
                int half = r * 6 / h - (r % 4 == 3 ? 1 : 0);
                for (int dx = -half; dx <= half; dx++)
                    PutWrapped(img, x0 + dx, y, r % 5 == 1 && dx < 0 ? light : shape);
            }
            for (int y = ground; y <= ground + 2; y++) PutWrapped(img, x0, y, shape);
        }
    }

    /// <summary>Carcaças de veículos meio enterradas: carroceria, cabine com janela vazada, rodas e realce no teto.</summary>
    private static void Wrecks(Canvas img, Rgba shape, Rgba light, int[] top, int salt, int count)
    {
        for (int k = 0; k < count; k++)
        {
            int x0 = ArtHash.Range(k, 1, salt, Width);
            int lowest = top[x0];
            for (int dx = 1; dx < 34; dx++) lowest = Math.Max(lowest, top[(x0 + dx) % Width]);
            int ground = lowest + 1;
            for (int dx = 0; dx < 34; dx++)
            for (int y = ground - 9; y < ground; y++)
                PutWrapped(img, x0 + dx, y, y == ground - 9 && (dx < 8 || dx > 21) ? light : shape);
            for (int dx = 8; dx <= 21; dx++)
            for (int y = ground - 15; y <= ground - 10; y++)
            {
                bool window = dx >= 11 && dx <= 18 && y >= ground - 14 && y <= ground - 12;
                if (!window) PutWrapped(img, x0 + dx, y, y == ground - 15 ? light : shape);
            }
            foreach (int wx in new[] { 6, 27 })
                for (int dy = -3; dy <= 3; dy++)
                for (int dx = -3; dx <= 3; dx++)
                    if (dx * dx + dy * dy <= 9) PutWrapped(img, x0 + wx + dx, ground + dy, shape);
        }
    }
}
