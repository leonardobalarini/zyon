using Godot;
using Zyon.Client;

namespace Zyon.Game.Rendering;

/// <summary>AtlasTexture por item (célula 16×16 de items.png), criadas uma vez.</summary>
public sealed class ItemIcons
{
    private readonly AtlasTexture?[] icons;

    public ItemIcons(ArtCatalog art, Texture2D items)
    {
        const int s = TileAtlasLayout.TileSize;
        icons = new AtlasTexture?[art.ItemByItemId.Count];
        for (int id = 1; id < icons.Length; id++)
            icons[id] = new AtlasTexture { Atlas = items, Region = new Rect2(art.ItemByItemId[id]!.Cell * s, 0, s, s) };
    }

    /// <summary>Ícone do item, ou null para "nenhum" e ids desconhecidos.</summary>
    public Texture2D? Get(ushort itemId) => itemId < icons.Length ? icons[itemId] : null;
}
