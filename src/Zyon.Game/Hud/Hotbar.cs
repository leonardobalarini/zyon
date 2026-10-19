using Godot;
using Zyon.Client;
using Zyon.Core.Items;
using Zyon.Core.Sim;

namespace Zyon.Game.Hud;

/// <summary>Hotbar provisória: 10 slots com a cor do item, quantidade e o slot selecionado destacado.</summary>
public partial class Hotbar : CanvasLayer
{
    private const int SlotSize = 20;
    private const int Gap = 2;
    private static readonly Color SlotBack = new(0f, 0f, 0f, 0.55f);
    private static readonly Color Selected = new(1f, 0.85f, 0.3f);

    private readonly ColorRect[] backs = new ColorRect[Inventory.HotbarSize];
    private readonly ColorRect[] icons = new ColorRect[Inventory.HotbarSize];
    private readonly Label[] counts = new Label[Inventory.HotbarSize];
    // Último estado mostrado em cada slot: só mexe nos nós quando muda (sem string nova por frame).
    private readonly (ItemStack Stack, bool Selected)?[] shown = new (ItemStack, bool)?[Inventory.HotbarSize];
    private IWorldView view = null!;
    private ClientVisuals visuals = null!;
    private int playerId;

    public void Init(IWorldView worldView, ClientVisuals clientVisuals, int localPlayerId)
    {
        view = worldView;
        visuals = clientVisuals;
        playerId = localPlayerId;
        const int total = Inventory.HotbarSize * (SlotSize + Gap) - Gap;
        for (int i = 0; i < Inventory.HotbarSize; i++)
        {
            var x = (640 - total) / 2 + i * (SlotSize + Gap);
            // Ignore: a hotbar não captura o mouse (clicar sobre ela não é "clique na interface").
            backs[i] = new ColorRect { Position = new Vector2(x, 360 - SlotSize - 4), Size = new Vector2(SlotSize, SlotSize), Color = SlotBack, MouseFilter = Control.MouseFilterEnum.Ignore };
            icons[i] = new ColorRect { Position = new Vector2(5, 5), Size = new Vector2(SlotSize - 10, SlotSize - 10), MouseFilter = Control.MouseFilterEnum.Ignore };
            counts[i] = new Label { Position = new Vector2(1, SlotSize - 12), MouseFilter = Control.MouseFilterEnum.Ignore };
            counts[i].AddThemeFontSizeOverride("font_size", 8);
            backs[i].AddChild(icons[i]);
            backs[i].AddChild(counts[i]);
            AddChild(backs[i]);
        }
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
            icons[i].Visible = !stack.IsEmpty;
            if (!stack.IsEmpty) icons[i].Color = visuals.ItemColor(stack.ItemId).ToGodot();
            counts[i].Text = stack.Count > 1 ? stack.Count.ToString() : "";
            backs[i].Color = isSelected ? Selected : SlotBack;
        }
    }
}
