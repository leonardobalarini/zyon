using System.Globalization;
using Godot;
using Zyon.Client;

namespace Zyon.Game.Hud;

/// <summary>
/// Sobreposição de depuração (F3): FPS e tempos de quadro da última janela de 1 s, custo da simulação e dos
/// renderizadores, chunks, coletas de lixo e o alpha de interpolação. Não captura o mouse.
/// </summary>
public partial class DebugOverlay : CanvasLayer
{
    private const double TextRefreshSeconds = 0.1;

    private readonly Label label = new()
    {
        Position = new Vector2(4, 4),
        MouseFilter = Control.MouseFilterEnum.Ignore,
    };
    private double sinceText = TextRefreshSeconds;

    public override void _Ready()
    {
        Layer = 100;
        Visible = false;
        label.AddThemeFontSizeOverride("font_size", 8);
        label.AddThemeColorOverride("font_color", Colors.White);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        label.AddThemeConstantOverride("outline_size", 2);
        AddChild(label);
    }

    public void Toggle() => Visible = !Visible;

    /// <summary>
    /// Se o texto deve ser refeito neste quadro: só visível e no máximo 10×/s, para a própria sobreposição não
    /// alocar strings a cada quadro (o que apareceria como coletas de lixo nas medidas).
    /// </summary>
    public bool ShouldRefresh(double delta)
    {
        if (!Visible) return false;
        sinceText += delta;
        if (sinceText < TextRefreshSeconds) return false;
        sinceText = 0;
        return true;
    }

    public void Refresh(FrameStats stats, in DebugInfo info)
    {

        var w = stats.Window;
        label.Text = string.Format(CultureInfo.InvariantCulture,
            "FPS {0:0} | quadro med {1:0.00} ms máx {2:0.00} ms\n" +
            "_Process med {3:0.00} máx {4:0.00} ms\n" +
            "sessão med {5:0.00} máx {6:0.00} ms | render med {7:0.00} máx {8:0.00} ms\n" +
            "chunks {9} | remontados: quadro {10}, 1 s {11} (máx {12}/quadro)\n" +
            "GC 1 s {13} | alpha {14:0.00} | ticks no quadro {15}\n" +
            "câmera: {16} (F4) | vsync: {17} (F5)",
            w.Fps, w.FrameAvgMs, w.FrameMaxMs,
            w.ProcessAvgMs, w.ProcessMaxMs,
            w.UpdateAvgMs, w.UpdateMaxMs, w.RenderAvgMs, w.RenderMaxMs,
            info.ActiveChunks, info.RebuildsThisFrame, w.Rebuilds, w.MaxRebuildsPerFrame,
            w.GcCollections, info.InterpolationAlpha, info.TicksThisFrame,
            info.CameraMode, info.VsyncMode);
    }
}

/// <summary>Estado instantâneo mostrado pela sobreposição de depuração.</summary>
public readonly record struct DebugInfo(
    int ActiveChunks, int RebuildsThisFrame, float InterpolationAlpha, long TicksThisFrame,
    string CameraMode, string VsyncMode);
