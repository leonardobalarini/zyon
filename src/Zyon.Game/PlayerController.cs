using Godot;
using Zyon.Client;
using Zyon.Core.Sim;

namespace Zyon.Game;

/// <summary>
/// Lê teclado e mouse e fala com a sessão: A/D andam, Espaço pula, botão esquerdo usa o item,
/// 1–0 e a roda escolhem o slot, Q solta o item selecionado. ScriptedInput substitui o teclado (smoke test).
/// </summary>
public partial class PlayerController : Node
{
    private IGameSession session = null!;

    public PlayerInput? ScriptedInput { get; set; }

    /// <summary>Falso com o menu de pausa aberto: o jogador fica parado e as teclas de jogo são ignoradas.</summary>
    public bool Enabled { get; set; } = true;

    public void Init(IGameSession gameSession) => session = gameSession;

    public void Poll(Vector2 mouseWorldPixels)
    {
        if (!Enabled)
        {
            session.SetInput(new PlayerInput(0f, false, false, 0, 0));
            return;
        }
        if (ScriptedInput is { } scripted)
        {
            session.SetInput(scripted);
            return;
        }
        float move = (Input.IsPhysicalKeyPressed(Key.D) ? 1f : 0f) - (Input.IsPhysicalKeyPressed(Key.A) ? 1f : 0f);
        bool jump = Input.IsPhysicalKeyPressed(Key.Space);
        // Clique sobre um controle da interface não minera nem coloca no mundo.
        bool primary = Input.IsMouseButtonPressed(MouseButton.Left) && GetViewport().GuiGetHoveredControl() is null;
        var (tx, ty) = RenderMath.TileAtPixel(mouseWorldPixels.ToNumerics());
        session.SetInput(new PlayerInput(move, jump, primary, tx, ty));
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Enabled) return;
        int id = session.LocalPlayerId;
        int selected = session.World.GetSelectedSlot(id);
        switch (@event)
        {
            case InputEventKey { Pressed: true, Echo: false } key:
                int slot = key.PhysicalKeycode switch
                {
                    Key.Key1 => 0, Key.Key2 => 1, Key.Key3 => 2, Key.Key4 => 3, Key.Key5 => 4,
                    Key.Key6 => 5, Key.Key7 => 6, Key.Key8 => 7, Key.Key9 => 8, Key.Key0 => 9,
                    _ => -1,
                };
                if (slot >= 0) session.Send(new SelectHotbarSlot(id, slot));
                if (key.PhysicalKeycode == Key.Q) session.Send(new DropItem(id, selected));
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp }:
                session.Send(new SelectHotbarSlot(id, (selected + 9) % 10));
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelDown }:
                session.Send(new SelectHotbarSlot(id, (selected + 1) % 10));
                break;
        }
    }
}
