using System;
using Godot;

namespace Zyon.Game.Menus;

/// <summary>Esc: continuar, salvar e voltar ao menu, ou salvar e sair. O jogo continua simulando por trás (não há pausa real no modo local da fatia).</summary>
public partial class PauseMenu : CanvasLayer
{
    private Control panel = null!;

    public bool IsOpen => panel.Visible;

    public void Init(Action resume, Action saveAndMenu, Action saveAndQuit)
    {
        Layer = 10;
        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.5f) };
        dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        dim.AddChild(center);
        var box = new VBoxContainer();
        center.AddChild(box);
        box.AddChild(new Label { Text = "Pausa", HorizontalAlignment = HorizontalAlignment.Center });
        box.AddChild(MakeButton("Continuar", () => { Close(); resume(); }));
        box.AddChild(MakeButton("Salvar e voltar ao menu", saveAndMenu));
        box.AddChild(MakeButton("Salvar e sair", saveAndQuit));
        panel = dim;
        panel.Theme = new Theme { DefaultFontSize = 10 };
        panel.Visible = false;
        AddChild(panel);
    }

    public void Open() => panel.Visible = true;

    public void Close() => panel.Visible = false;

    private static Button MakeButton(string text, Action onPressed)
    {
        var button = new Button { Text = text };
        button.Pressed += onPressed;
        return button;
    }
}
