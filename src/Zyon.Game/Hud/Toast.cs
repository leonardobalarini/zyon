using Godot;

namespace Zyon.Game.Hud;

/// <summary>Mensagem curta no topo da tela que some sozinha (acima do menu de pausa).</summary>
public partial class Toast : CanvasLayer
{
    private Label label = null!;
    private double remaining;

    public void Init()
    {
        Layer = 50;
        label = new Label
        {
            Position = new Vector2(8, 6),
            Size = new Vector2(624, 40),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false,
        };
        label.AddThemeFontSizeOverride("font_size", 9);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        label.AddThemeConstantOverride("outline_size", 3);
        AddChild(label);
    }

    public void Show(string text, double seconds = 4)
    {
        label.Text = text;
        label.Visible = true;
        remaining = seconds;
    }

    public void Clear()
    {
        label.Visible = false;
        remaining = 0;
    }

    public override void _Process(double delta)
    {
        if (remaining <= 0) return;
        remaining -= delta;
        if (remaining <= 0) label.Visible = false;
    }
}
