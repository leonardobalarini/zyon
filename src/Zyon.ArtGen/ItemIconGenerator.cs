using Zyon.Client;
using Zyon.Core.Data;

namespace Zyon.ArtGen;

/// <summary>Ícones 16×16 em uma linha (célula = id − 1). Itens de bloco copiam o bloco isolado (máscara 0, variação 0).</summary>
public static class ItemIconGenerator
{
    private const int T = 16;

    public static Canvas Build(ArtSource src, Registry registry, Canvas tiles, out Dictionary<string, ItemArt> arts)
    {
        arts = [];
        var img = new Canvas((registry.Items.Count - 1) * T, T);
        var outline = Rgba.Parse(src.Player.Outline);
        for (int id = 1; id < registry.Items.Count; id++)
        {
            var def = registry.Item((ushort)id);
            int cell = id - 1, ox = cell * T;
            if (src.Items.TryGetValue(def.Name, out var item) && item.Recipe == "pickaxe")
            {
                DrawPickaxe(img, ox, ArtSource.Colors(item.Handle!), ArtSource.Colors(item.Head!));
                img.Outline(ox, 0, T, T, outline);
                arts[def.Name] = new ItemArt { Cell = cell, GripX = 3, GripY = 12, PointDeg = -45f };
            }
            else
            {
                int row = def.PlacesBlockId - 1;   // linha do bloco no tiles.png
                img.Blit(tiles, 0, row * T, T, T, ox, 0);
                arts[def.Name] = new ItemArt { Cell = cell, GripX = 8, GripY = 14, PointDeg = -90f };
            }
        }
        return img;
    }

    /// <summary>
    /// Cabo na diagonal (canto inferior esquerdo → centro) e cabeça em arco no alto à direita: um anel em volta da
    /// pega (3, 12), a até ~39° da diagonal, mais grosso no meio e afinando nas pontas, que se curvam para o cabo.
    /// </summary>
    private static void DrawPickaxe(Canvas img, int ox, Rgba[] handle, Rgba[] head)
    {
        for (int py = 0; py < T; py++)
        for (int px = 0; px < T; px++)
        {
            int dx = px - 3, dy = py - 12;
            int along = dx - dy, across = Math.Abs(dx + dy), d2 = dx * dx + dy * dy;
            if (along <= 0 || across * 100 > along * 80) continue;
            int inner = across * 100 > along * 45 ? 121 : 100;
            if (d2 < inner || d2 > 156) continue;
            img.Set(ox + px, py, d2 >= 140 ? head[2] : d2 >= 115 ? head[1] : head[0]);
        }
        for (int x = 2; x <= 10; x++)
        {
            img.Set(ox + x, 15 - x, handle[1]);
            img.Set(ox + x + 1, 15 - x, handle[0]);
        }
        img.Set(ox + 2, 13, handle[2]);
    }
}
