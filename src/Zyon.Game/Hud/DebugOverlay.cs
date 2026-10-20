using System.Globalization;
using Godot;
using Zyon.Client;

namespace Zyon.Game.Hud;

/// <summary>
/// Sobreposição de depuração (F3): FPS e tempos de quadro da última janela de 1 s, custo da simulação e dos
/// renderizadores, chunks, coletas de lixo, o alpha de interpolação, quantos quadros rodaram 0 ou 2+ ticks no
/// último segundo e o último "soluço" (quadro com 2+ ticks) com suas medidas. Não captura o mouse.
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

    /// <summary>Linha de um soluço (quadro com 2+ ticks), usada na sobreposição e no console.</summary>
    public static string HitchText(FrameHitch hitch, long currentFrame)
    {
        var s = hitch.Sample;
        return string.Format(CultureInfo.InvariantCulture,
            "soluço: quadro #{0} (há {1} quadros): {2} ticks, delta {3:0.00} ms (medido {4:0.00} ms), " +
            "_Process {5:0.00} ms, remontagens {6} (novos {7}), GC {8}",
            hitch.Frame, currentFrame - hitch.Frame, s.Ticks, s.DeltaSeconds * 1000, s.MeasuredSeconds * 1000,
            s.ProcessMs, s.Rebuilds, s.NewChunks, s.GcCollections);
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
            "1 s: quadros com 0 ticks {18}, com 2+ ticks {19} | delta máx {2:0.00} ms, medido máx {20:0.00} ms\n" +
            "{21}\n" +
            "câmera: {16} (F4) | vsync: {17} (F5)",
            w.Fps, w.FrameAvgMs, w.FrameMaxMs,
            w.ProcessAvgMs, w.ProcessMaxMs,
            w.UpdateAvgMs, w.UpdateMaxMs, w.RenderAvgMs, w.RenderMaxMs,
            info.ActiveChunks, info.RebuildsThisFrame, w.Rebuilds, w.MaxRebuildsPerFrame,
            w.GcCollections, info.InterpolationAlpha, info.TicksThisFrame,
            info.CameraMode, info.VsyncMode,
            w.ZeroTickFrames, w.MultiTickFrames, w.MeasuredMaxMs,
            stats.LastHitch is { } h ? HitchText(h, stats.FrameCount) : "soluço: nenhum quadro com 2+ ticks");
    }
}

/// <summary>Estado instantâneo mostrado pela sobreposição de depuração.</summary>
public readonly record struct DebugInfo(
    int ActiveChunks, int RebuildsThisFrame, float InterpolationAlpha, long TicksThisFrame,
    string CameraMode, string VsyncMode);
