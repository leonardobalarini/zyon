using Godot;
using Zyon.Client;

namespace Zyon.Game;

/// <summary>
/// Autoload: mantém as escalas inteiras da tela (<see cref="ScreenScale"/>) quando a janela muda de tamanho, alterna a
/// tela cheia com F11 e o zoom do mundo com Ctrl+roda ou +/− em qualquer cena, lembrando as escolhas em
/// user://settings.json. O modo canvas_items desenha o texto na resolução real (nítido); a câmera aplica
/// <see cref="WorldZoom"/> para o mundo ter a própria escala, também inteira.
/// </summary>
public partial class ScreenScaler : Node
{
    private static readonly Vector2I MinWindowSize = new(640, 360);

    /// <summary>
    /// Zoom da câmera do mundo sobre a tela interna. Em headless (smoke test, profile-walk) fica o de 1080p (0,5): a tela
    /// interna padrão do projeto é 960×540, então o mundo visível é o de uma tela 1080p, 120×67 tiles.
    /// </summary>
    public static float WorldZoom { get; private set; } = ScreenScale.WorldZoom(1080);

    private string settingsPath = "";
    private DisplaySettings settings = new();
    private Vector2I applied;

    public override void _Ready()
    {
        // Em headless não há janela de verdade: fica a tela interna padrão do projeto (960×540, modo canvas_items com
        // escala inteira, ver project.godot) e nada de preferências.
        if (DisplayServer.GetName() == "headless") return;
        var window = GetWindow();
        window.MinSize = MinWindowSize;
        settingsPath = ProjectSettings.GlobalizePath("user://" + DisplaySettings.FileName);
        settings = DisplaySettings.Load(settingsPath, out var error);
        if (error is not null) GD.PushWarning($"Preferências de tela ignoradas: {error}");
        if (settings.Fullscreen) window.Mode = Window.ModeEnum.Fullscreen;
        window.SizeChanged += () => Apply(force: false);
        Apply(force: true);
    }

    public override void _Input(InputEvent @event)
    {
        if (settingsPath == "") return;
        switch (@event)
        {
            case InputEventKey { Pressed: true, Echo: false, Keycode: Key.F11 }:
                var window = GetWindow();
                bool fullscreen = window.Mode is not (Window.ModeEnum.Fullscreen or Window.ModeEnum.ExclusiveFullscreen);
                window.Mode = fullscreen ? Window.ModeEnum.Fullscreen : Window.ModeEnum.Maximized;
                settings = settings with { Fullscreen = fullscreen };
                break;
            case InputEventKey { Pressed: true, Keycode: Key.Equal or Key.KpAdd }:
                Zoom(+1);
                break;
            case InputEventKey { Pressed: true, Keycode: Key.Minus or Key.KpSubtract }:
                Zoom(-1);
                break;
            // Ctrl+roda: a roda sozinha troca o slot da hotbar.
            case InputEventMouseButton { Pressed: true, CtrlPressed: true, ButtonIndex: MouseButton.WheelUp }:
                Zoom(+1);
                break;
            case InputEventMouseButton { Pressed: true, CtrlPressed: true, ButtonIndex: MouseButton.WheelDown }:
                Zoom(-1);
                break;
            default:
                return;
        }
        Save();
        GetViewport().SetInputAsHandled();
    }

    private void Zoom(int delta)
    {
        // Guarda só os passos que tiveram efeito: insistir no limite não acumula passos "invisíveis".
        settings = settings with { ZoomSteps = ScreenScale.ClampZoomSteps(GetWindow().Size.Y, settings.ZoomSteps + delta) };
        Apply(force: true);
    }

    private void Save()
    {
        try
        {
            settings.Save(settingsPath);
        }
        catch (Exception e) when (e is System.IO.IOException or UnauthorizedAccessException)
        {
            GD.PushWarning($"Não foi possível gravar as preferências de tela: {e.Message}");
        }
    }

    private void Apply(bool force)
    {
        var size = GetWindow().Size;
        if ((!force && size == applied) || size.X <= 0 || size.Y <= 0) return;
        applied = size;
        int scale = ScreenScale.For(size.Y);
        var (w, h) = ScreenScale.BaseSize(size.X, size.Y, scale);
        GetWindow().ContentScaleSize = new Vector2I(w, h);
        WorldZoom = ScreenScale.WorldZoom(size.Y, settings.ZoomSteps);
        GD.Print($"Tela: janela {size.X}×{size.Y}, escala {scale}, interna {w}×{h}, " +
                 $"mundo {ScreenScale.ForWorld(size.Y, settings.ZoomSteps)} px por pixel da arte (zoom {settings.ZoomSteps:+0;-0;0})");
    }
}
