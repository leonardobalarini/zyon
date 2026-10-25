using Zyon.Client;
using Zyon.Core.Data;

namespace Zyon.ArtGen;

/// <summary>
/// Atlas de blocos e paredes (16 máscaras × variações por linha) e de bordas de mescla (4 lados × variações).
/// Estilo contornado: manchas 2×2 em 3 tons; contorno, brilho e cantos arredondados só contra o vazio.
/// </summary>
public static class TileGenerator
{
    public const int T = 16;
    public const float OutlineFactor = 0.55f;
    public const float HighlightFactor = 1.18f;
    public const float ShadeFactor = 0.85f;

    public static Canvas BuildBlocks(ArtSource src, Registry registry, out Dictionary<string, int> rows)
    {
        rows = [];
        var img = new Canvas(AutoTile.MaskCount * src.Variations * T, (registry.Blocks.Count - 1) * T);
        for (int id = 1; id < registry.Blocks.Count; id++)
        {
            string name = registry.Block((ushort)id).Name;
            int row = id - 1;
            rows[name] = row;
            for (int mask = 0; mask < AutoTile.MaskCount; mask++)
            for (int v = 0; v < src.Variations; v++)
                DrawBlock(img, (mask * src.Variations + v) * T, row * T, src, name, mask, v, salt: id * 101);
        }
        return img;
    }

    public static Canvas BuildWalls(ArtSource src, Registry registry, out Dictionary<string, int> rows)
    {
        rows = [];
        var img = new Canvas(AutoTile.MaskCount * src.Variations * T, (registry.Walls.Count - 1) * T);
        for (int id = 1; id < registry.Walls.Count; id++)
        {
            string name = registry.Wall((ushort)id).Name;
            int row = id - 1;
            rows[name] = row;
            var pal = ArtSource.Colors(src.Walls[name].Palette!);
            for (int mask = 0; mask < AutoTile.MaskCount; mask++)
            for (int v = 0; v < src.Variations; v++)
                DrawWall(img, (mask * src.Variations + v) * T, row * T, pal, src.Walls[name].Recipe == "plates", mask, v, src.Seed, salt: 5000 + id * 101);
        }
        return img;
    }

    /// <summary>Lado: 0 N, 1 L, 2 S, 3 O — lado do tile duro em que a borda do material mole aparece.</summary>
    public static Canvas BuildMerge(ArtSource src, Registry registry)
    {
        var img = new Canvas(4 * src.Variations * T, Math.Max(1, src.MergeOrder.Count) * T);
        for (int row = 0; row < src.MergeOrder.Count; row++)
        {
            string name = src.MergeOrder[row];
            var pal = src.BlockPalette(name);
            int salt = 9000 + registry.BlockId(name) * 101;
            for (int side = 0; side < 4; side++)
            for (int v = 0; v < src.Variations; v++)
                DrawTongue(img, (side * src.Variations + v) * T, row * T, pal, side, v, src.Seed, salt);
        }
        return img;
    }

    /// <summary>Tom 0..2 da mancha 2×2 que contém (px, py).</summary>
    public static int Tone(int px, int py, int v, int seed, int salt)
    {
        uint h = ArtHash.Get(v * 8 + px / 2, py / 2, seed * 31 + salt) % 100u;
        return h < 30 ? 0 : h >= 72 ? 2 : 1;
    }

    private static void DrawBlock(Canvas img, int ox, int oy, ArtSource src, string name, int mask, int v, int salt)
    {
        var m = src.Blocks[name];
        if (m.Recipe == "torch")
        {
            DrawTorch(img, ox, oy, ArtSource.Colors(m.Palette!), ArtSource.Colors(m.Flame!));
            return;
        }

        if (m.Recipe == "prop")
        {
            PropGenerator.Draw(img, ox, oy, m.Shape!, ArtSource.Colors(m.Palette!), m.Accent is null ? null : ArtSource.Colors(m.Accent));
            return;
        }

        var pal = src.BlockPalette(name);
        var cap = m.Cap is null ? null : ArtSource.Colors(m.Cap);
        var fleck = m.Recipe == "ore" ? ArtSource.Colors(m.Fleck!) : null;
        var accent = m.Accent is null ? null : ArtSource.Colors(m.Accent);
        bool panels = m.Recipe == "panels" || (m.Recipe == "ore" && src.Blocks[m.Base!].Recipe == "panels");
        bool n = (mask & AutoTile.North) == 0, e = (mask & AutoTile.East) == 0;
        bool s = (mask & AutoTile.South) == 0, w = (mask & AutoTile.West) == 0;

        for (int py = 0; py < T; py++)
        for (int px = 0; px < T; px++)
        {
            if (CornerCut(px, py, n, e, s, w)) continue;
            var c = m.Recipe switch
            {
                "plates" => Plate(px, py, v, src.Seed, salt, pal),
                "stripes" => Stripe(px, py, v, src.Seed, salt, pal, accent!),
                _ => pal[Tone(px, py, v, src.Seed, salt)],
            };
            if (panels) c = Panel(px, py, v, src.Seed, salt, pal) ?? c;
            if (fleck is not null) c = Fleck(px, py, v, src.Seed, salt, fleck) ?? c;
            bool capped = cap is not null && n && py < 2 + ArtHash.Range(v * T + px, 0, src.Seed + salt + 5, 2);
            if (capped) c = cap![Tone(px, py, v, src.Seed, salt + 7)];

            if ((n && py == 0) || (s && py == T - 1) || (w && px == 0) || (e && px == T - 1))
                c = (cap is not null && n && py == 0 ? cap[0] : pal[0]).Scale(OutlineFactor);
            else if (n && py == 1) c = c.Scale(HighlightFactor);
            else if ((w && px == 1) || (e && px == T - 2) || (s && py == T - 2)) c = c.Scale(ShadeFactor);
            img.Set(ox + px, oy + py, c);
        }
    }

    /// <summary>Chapas 8×8 em fiada desencontrada: junta escura, bisel claro, rebites em grade 2×2 dentro de cada chapa e manchas raras.</summary>
    private static Rgba Plate(int px, int py, int v, int seed, int salt, Rgba[] pal)
    {
        int row = py / 8, ly = py % 8;
        int shift = (row + v) % 2 * 4;   // a variação troca o desencontro das fiadas
        int lx = (px + shift) % 8;
        if (ly == 7 || lx == 7) return pal[0];
        if ((lx == 1 || lx == 5) && (ly == 1 || ly == 5)) return pal[2];
        if (ly == 0 || lx == 0) return pal[2];
        return ArtHash.Chance(v * T + px, py, seed + salt + 30, 6) ? pal[0] : pal[1];
    }

    /// <summary>Listras diagonais de 4 px (paleta e destaque) com lascas no tom escuro.</summary>
    private static Rgba Stripe(int px, int py, int v, int seed, int salt, Rgba[] pal, Rgba[] accent)
    {
        var tones = (px + py + v * 2) / 4 % 2 == 0 ? pal : accent;
        return ArtHash.Chance(v * T + px, py, seed + salt + 31, 7) ? tones[0] : tones[1];
    }

    private static bool CornerCut(int px, int py, bool n, bool e, bool s, bool w) =>
        (n && w && px + py < 2) || (n && e && (T - 1 - px) + py < 2) ||
        (s && w && px + (T - 1 - py) < 2) || (s && e && (T - 1 - px) + (T - 1 - py) < 2);

    /// <summary>Linha de painel com bisel e uma emenda vertical (pista das ruínas high-tech).</summary>
    private static Rgba? Panel(int px, int py, int v, int seed, int salt, Rgba[] pal)
    {
        int line = 4 + ArtHash.Range(v, 0, seed + salt + 11, 8);
        int seam = 3 + ArtHash.Range(v, 1, seed + salt + 12, 10);
        if (px >= 2 && px <= T - 3 && py == line) return pal[0];
        if (px >= 2 && px <= T - 3 && py == line + 1) return pal[2];
        if (px == seam && py < line && py > 1) return pal[0];
        return null;
    }

    /// <summary>Três pintas em losango na cor do minério.</summary>
    private static Rgba? Fleck(int px, int py, int v, int seed, int salt, Rgba[] fleck)
    {
        for (int k = 0; k < 3; k++)
        {
            int cx = 3 + ArtHash.Range(v, k, seed + salt + 20, 10);
            int cy = 3 + ArtHash.Range(v, k, seed + salt + 21, 10);
            int d = Math.Abs(px - cx) + Math.Abs(py - cy);
            if (d == 0) return fleck[2];
            if (d == 1) return fleck[1];
            if (d == 2 && ArtHash.Chance(px, py, seed + salt + 22 + k, 50)) return fleck[0];
        }
        return null;
    }

    private static void DrawTorch(Canvas img, int ox, int oy, Rgba[] handle, Rgba[] flame)
    {
        for (int py = 6; py < T; py++)
        {
            img.Set(ox + 7, oy + py, py == T - 1 ? handle[0] : handle[1]);
            img.Set(ox + 8, oy + py, handle[0]);
        }
        for (int py = 2; py <= 5; py++)
        for (int px = 6; px <= 9; px++)
        {
            bool outer = px == 6 || px == 9 || py == 5;
            bool core = (px == 7 || px == 8) && py <= 3;
            img.Set(ox + px, oy + py, core ? flame[2] : outer ? flame[0] : flame[1]);
        }
        img.Set(ox + 7, oy + 1, flame[1]);
        img.Set(ox + 8, oy + 1, flame[0]);
    }

    private static void DrawWall(Canvas img, int ox, int oy, Rgba[] pal, bool plates, int mask, int v, int seed, int salt)
    {
        bool n = (mask & AutoTile.North) == 0, e = (mask & AutoTile.East) == 0;
        bool s = (mask & AutoTile.South) == 0, w = (mask & AutoTile.West) == 0;
        for (int py = 0; py < T; py++)
        for (int px = 0; px < T; px++)
        {
            var c = plates ? Plate(px, py, v, seed, salt, pal) : pal[Tone(px, py, v, seed, salt)];
            if ((n && py == 0) || (s && py == T - 1) || (w && px == 0) || (e && px == T - 1)) c = pal[0].Scale(0.8f);
            img.Set(ox + px, oy + py, c);
        }
    }

    /// <summary>Faixa irregular de 2–4 px (em colunas de 2 px) ao longo de um lado; o último pixel é a sombra.</summary>
    private static void DrawTongue(Canvas img, int ox, int oy, Rgba[] pal, int side, int v, int seed, int salt)
    {
        for (int along = 0; along < T; along++)
        {
            int depth = 2 + ArtHash.Range(v * T + along / 2, side, seed + salt + 40, 3);
            for (int d = 0; d < depth; d++)
            {
                var (px, py) = side switch
                {
                    0 => (along, d),
                    1 => (T - 1 - d, along),
                    2 => (along, T - 1 - d),
                    _ => (d, along),
                };
                var c = d == depth - 1 ? pal[0].Scale(0.8f) : pal[Tone(px, py, v, seed, salt)];
                img.Set(ox + px, oy + py, c);
            }
        }
    }
}
