using Godot;
using Zyon.Core.Items;
using Zyon.Core.Sim;
using Zyon.Game.Rendering;

namespace Zyon.Game.Hud;

/// <summary>Hotbar: 10 slots com o ícone do item, quantidade e o slot selecionado destacado.</summary>
public partial class Hotbar : CanvasLayer
{
    private const int SlotSize = 20;
    private const int Gap = 2;
    private static readonly Color SlotBack = new(0f, 0f, 0f, 0.55f);
    private static readonly Color Selected = new(1f, 0.85f, 0.3f);

    private readonly ColorRect[] backs = new ColorRect[Inventory.HotbarSize];
    private readonly TextureRect[] icons = new TextureRect[Inventory.HotbarSize];
    private readonly Label[] counts = new Label[Inventory.HotbarSize];
    // Último estado mostrado em cada slot: só mexe nos nós quando muda (sem string nova por frame).
    private readonly (ItemStack Stack, bool Selected)?[] shown = new (ItemStack, bool)?[Inventory.HotbarSize];
    private IWorldView view = null!;
    private ItemIcons itemIcons = null!;
    private int playerId;

    public void Init(IWorldView worldView, ItemIcons itemIconSource, int localPlayerId)
    {
        view = worldView;
        itemIcons = itemIconSource;
        playerId = localPlayerId;
        const int total = Inventory.HotbarSize * (SlotSize + Gap) - Gap;
        // Presa ao centro da borda de baixo: a tela interna muda de tamanho com a janela.
        var bar = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        bar.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
        bar.OffsetLeft = -total / 2;
        bar.OffsetRight = total - total / 2;
        bar.OffsetTop = -SlotSize - 4;
        bar.OffsetBottom = -4;
        AddChild(bar);
        for (int i = 0; i < Inventory.HotbarSize; i++)
        {
            var x = i * (SlotSize + Gap);
            // Ignore: a hotbar não captura o mouse (clicar sobre ela não é "clique na interface").
            backs[i] = new ColorRect { Position = new Vector2(x, 0), Size = new Vector2(SlotSize, SlotSize), Color = SlotBack, MouseFilter = Control.MouseFilterEnum.Ignore };
            icons[i] = InventorySlot.CreateIcon();
            counts[i] = new Label { Position = new Vector2(1, SlotSize - 12), MouseFilter = Control.MouseFilterEnum.Ignore };
            counts[i].AddThemeFontSizeOverride("font_size", 8);
            backs[i].AddChild(icons[i]);
            backs[i].AddChild(counts[i]);
            bar.AddChild(backs[i]);
        }
    }

    /// <summary>Slot da hotbar sob o ponto (coordenadas da tela/viewport), ou null.</summary>
    public int? SlotAtScreen(Vector2 position)
    {
        for (int i = 0; i < backs.Length; i++)
            if (backs[i].GetGlobalRect().HasPoint(position)) return i;
        return null;
    }

    public void Refresh()
    {
        var inventory = view.GetInventory(playerId);
        if (inventory is null) return;
        int selected = view.GetSelectedSlot(playerId);
        for (int i = 0; i < Inventory.HotbarSize; i++)
        {
            var stack = inventory[i];
            bool isSelected = i == selected;
            if (shown[i] is { } last && last.Stack == stack && last.Selected == isSelected) continue;
            shown[i] = (stack, isSelected);
            icons[i].Texture = stack.IsEmpty ? null : itemIcons.Get(stack.ItemId);
            counts[i].Text = stack.Count > 1 ? stack.Count.ToString() : "";
            backs[i].Color = isSelected ? Selected : SlotBack;
        }
    }
}
