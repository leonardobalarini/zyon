using System;
using Godot;
using Zyon.Core.Items;
using Zyon.Game.Rendering;

namespace Zyon.Game.Hud;

/// <summary>Um slot do inventário: arrasta a pilha para outro slot (MoveInventoryItem) ou para fora (DropItem).</summary>
public partial class InventorySlot : Panel
{
    public const int SlotSize = 20;
    public const int IconSize = 16;

    private TextureRect icon = null!;
    private Label count = null!;
    private Action<int, int> move = null!;
    private Action<int> dragFailed = null!;
    private bool dragging;
    private ItemStack shown = new(ushort.MaxValue, -1);

    public int Slot { get; private set; }

    /// <param name="onDragFailed">Arrasto deste slot terminou sem cair em outro slot do painel (o painel decide).</param>
    public void Init(int slot, Action<int, int> onMove, Action<int> onDragFailed)
    {
        Slot = slot;
        move = onMove;
        dragFailed = onDragFailed;
        CustomMinimumSize = new Vector2(SlotSize, SlotSize);
        icon = CreateIcon();
        count = new Label { Position = new Vector2(1, SlotSize - 12), MouseFilter = MouseFilterEnum.Ignore };
        count.AddThemeFontSizeOverride("font_size", 8);
        AddChild(icon);
        AddChild(count);
    }

    /// <summary>
    /// Ícone 16×16 centrado no slot de 20 px, em escala 1:1 (reduzir a arte em nearest apagaria linhas de pixels).
    /// Não captura o mouse.
    /// </summary>
    public static TextureRect CreateIcon() => new()
    {
        Position = new Vector2((SlotSize - IconSize) / 2, (SlotSize - IconSize) / 2),
        Size = new Vector2(IconSize, IconSize),
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        TextureFilter = TextureFilterEnum.Nearest,
        MouseFilter = MouseFilterEnum.Ignore,
    };

    public void Show(ItemStack stack, ItemIcons icons)
    {
        if (stack == shown) return;
        shown = stack;
        icon.Texture = stack.IsEmpty ? null : icons.Get(stack.ItemId);
        count.Text = stack.Count > 1 ? stack.Count.ToString() : "";
    }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (shown.IsEmpty) return default;
        dragging = true;
        var preview = CreateIcon();
        preview.Position = Vector2.Zero;
        preview.Texture = icon.Texture;
        SetDragPreview(preview);
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
        if (!GetViewport().GuiIsDragSuccessful()) dragFailed(Slot);
    }
}
