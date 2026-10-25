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
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false,
        };
        // Faixa no topo com a largura da tela (que acompanha a janela).
        label.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        label.OffsetLeft = 8;
        label.OffsetRight = -8;
        label.OffsetTop = 6;
        label.OffsetBottom = 46;
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
