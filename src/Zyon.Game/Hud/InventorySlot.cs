using System;
using Godot;
using Zyon.Client;
using Zyon.Core.Items;

namespace Zyon.Game.Hud;

/// <summary>Um slot do inventário: arrasta a pilha para outro slot (MoveInventoryItem) ou para fora (DropItem).</summary>
public partial class InventorySlot : Panel
{
    public const int SlotSize = 20;

    private ColorRect icon = null!;
    private Label count = null!;
    private Action<int, int> move = null!;
    private Action<int> dropOutside = null!;
    private Func<bool> mouseInsidePanel = null!;
    private bool dragging;
    private ItemStack shown = new(ushort.MaxValue, -1);

    public int Slot { get; private set; }

    public void Init(int slot, Action<int, int> onMove, Action<int> onDropOutside, Func<bool> isMouseInsidePanel)
    {
        Slot = slot;
        move = onMove;
        dropOutside = onDropOutside;
        mouseInsidePanel = isMouseInsidePanel;
        CustomMinimumSize = new Vector2(SlotSize, SlotSize);
        icon = new ColorRect { Position = new Vector2(5, 5), Size = new Vector2(SlotSize - 10, SlotSize - 10), MouseFilter = MouseFilterEnum.Ignore };
        count = new Label { Position = new Vector2(1, SlotSize - 12), MouseFilter = MouseFilterEnum.Ignore };
        count.AddThemeFontSizeOverride("font_size", 8);
        AddChild(icon);
        AddChild(count);
    }

    public void Show(ItemStack stack, ClientVisuals visuals)
    {
        if (stack == shown) return;
        shown = stack;
        icon.Visible = !stack.IsEmpty;
        if (!stack.IsEmpty) icon.Color = visuals.ItemColor(stack.ItemId).ToGodot();
        count.Text = stack.Count > 1 ? stack.Count.ToString() : "";
    }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (shown.IsEmpty) return default;
        dragging = true;
        SetDragPreview(new ColorRect { Color = icon.Color, Size = new Vector2(12, 12), MouseFilter = MouseFilterEnum.Ignore });
        return Slot;
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data) => data.VariantType == Variant.Type.Int;

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        int from = data.AsInt32();
        if (from != Slot) move(from, Slot);
    }

    public override void _Notification(int what)
    {
        if (what != NotificationDragEnd || !dragging) return;
        dragging = false;
        // Soltou fora de qualquer slot e fora do painel: joga a pilha no chão.
        if (!GetViewport().GuiIsDragSuccessful() && !mouseInsidePanel())
            dropOutside(Slot);
    }
}
