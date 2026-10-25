using Godot;
using Zyon.Client;

namespace Zyon.Game;

/// <summary>
/// Autoload: mantém a escala da tela inteira (<see cref="ScreenScale"/>) quando a janela muda de tamanho e alterna a tela
/// cheia com F11 em qualquer cena, lembrando a escolha em user://settings.json. O modo canvas_items desenha o texto na
/// resolução real (nítido); a escala inteira mantém os pixels da arte iguais.
/// </summary>
public partial class ScreenScaler : Node
{
    private static readonly Vector2I MinWindowSize = new(640, 360);

    private string settingsPath = "";
    private DisplaySettings settings = new();
    private Vector2I applied;

    public override void _Ready()
    {
        // Em headless (smoke test, profile-walk) não há janela de verdade: fica a tela interna padrão do projeto
        // (960×540, modo canvas_items com escala inteira, ver project.godot) e nada de preferências.
        if (DisplayServer.GetName() == "headless") return;
        var window = GetWindow();
        window.MinSize = MinWindowSize;
        settingsPath = ProjectSettings.GlobalizePath("user://" + DisplaySettings.FileName);
        settings = DisplaySettings.Load(settingsPath, out var error);
        if (error is not null) GD.PushWarning($"Preferências de tela ignoradas: {error}");
        if (settings.Fullscreen) window.Mode = Window.ModeEnum.Fullscreen;
        window.SizeChanged += Apply;
        Apply();
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false, Keycode: Key.F11 } || settingsPath == "") return;
        var window = GetWindow();
        bool fullscreen = window.Mode is not (Window.ModeEnum.Fullscreen or Window.ModeEnum.ExclusiveFullscreen);
        window.Mode = fullscreen ? Window.ModeEnum.Fullscreen : Window.ModeEnum.Maximized;
        settings = settings with { Fullscreen = fullscreen };
        try
        {
            settings.Save(settingsPath);
        }
        catch (Exception e) when (e is System.IO.IOException or UnauthorizedAccessException)
        {
            GD.PushWarning($"Não foi possível gravar as preferências de tela: {e.Message}");
        }
        GetViewport().SetInputAsHandled();
    }

    private void Apply()
    {
        var size = GetWindow().Size;
        if (size == applied || size.X <= 0 || size.Y <= 0) return;
        applied = size;
        int scale = ScreenScale.For(size.Y);
        var (w, h) = ScreenScale.BaseSize(size.X, size.Y, scale);
        GetWindow().ContentScaleSize = new Vector2I(w, h);
        GD.Print($"Tela: janela {size.X}×{size.Y}, escala {scale}, interna {w}×{h}");
    }
}
