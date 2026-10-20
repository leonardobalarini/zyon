using Godot;
using Zyon.Client;
using Zyon.Core.Items;
using Zyon.Core.Sim;

namespace Zyon.Game.Hud;

/// <summary>Inventário completo (5 linhas × 10; a primeira linha é a hotbar). Abre e fecha com E; Esc também fecha.</summary>
public partial class InventoryPanel : CanvasLayer
{
    private readonly InventorySlot[] slots = new InventorySlot[Inventory.Slots];
    private PanelContainer panel = null!;
    private IGameSession session = null!;
    private ClientVisuals visuals = null!;

    public bool IsOpen => panel.Visible;

    public void Init(IGameSession gameSession, ClientVisuals clientVisuals)
    {
        session = gameSession;
        visuals = clientVisuals;
        Layer = 5;
        panel = new PanelContainer { Position = new Vector2(8, 8), Visible = false };
        var grid = new GridContainer { Columns = Inventory.HotbarSize };
        grid.AddThemeConstantOverride("h_separation", 2);
        grid.AddThemeConstantOverride("v_separation", 2);
        panel.AddChild(grid);
        for (int i = 0; i < Inventory.Slots; i++)
        {
            var slot = new InventorySlot();
            slot.Init(i, Move, Drop, IsMouseInside);
            slots[i] = slot;
            grid.AddChild(slot);
        }
        AddChild(panel);
    }

    public void Toggle() => panel.Visible = !panel.Visible;

    public void Close() => panel.Visible = false;

    public void Refresh()
    {
        if (!panel.Visible) return;
        var inventory = session.World.GetInventory(session.LocalPlayerId);
        if (inventory is null) return;
        for (int i = 0; i < slots.Length; i++)
            slots[i].Show(inventory[i], visuals);
    }

    private void Move(int from, int to) => session.Send(new MoveInventoryItem(session.LocalPlayerId, from, to));

    private void Drop(int slot) => session.Send(new DropItem(session.LocalPlayerId, slot));

    private bool IsMouseInside() => panel.GetGlobalRect().HasPoint(panel.GetViewport().GetMousePosition());
}
