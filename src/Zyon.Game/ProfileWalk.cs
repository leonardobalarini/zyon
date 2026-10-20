using System.Collections.Generic;
using System.Globalization;
using Godot;
using Zyon.Client;
using Zyon.Core.Sim;

namespace Zyon.Game;

/// <summary>
/// Modo --profile-walk: anda sem parar (pula e minera o que bloquear, vira nas bordas do mundo) para cruzar muitas
/// fronteiras de chunk, guardando as medidas de cada quadro para <see cref="FrameProfileReport"/>.
/// </summary>
public sealed class ProfileWalk(IGameSession session, PlayerController controller, int totalFrames)
{
    private const int StuckFrames = 20;

    private readonly List<FrameSample> samples = new(totalFrames);
    private readonly List<float> alphas = new(totalFrames);
    private int frame;
    private float direction = 1f;
    private float lastX = float.NaN;
    private int sinceProgress;

    public bool Finished => frame >= totalFrames;
    public IReadOnlyList<FrameSample> Samples => samples;

    public void Record(FrameSample sample, float interpolationAlpha)
    {
        // Os primeiros quadros são a montagem inicial: não interessam para os soluços durante o jogo.
        if (frame < 60) return;
        samples.Add(sample);
        alphas.Add(interpolationAlpha);
    }

    public void Step()
    {
        frame++;
        if (!session.World.TryGetEntityView(session.LocalPlayerId, out var me)) return;
        float x = me.Position.X;
        if (x > session.World.Width - 24) direction = -1f;
        else if (x < 24) direction = 1f;

        if (float.IsNaN(lastX) || System.MathF.Abs(x - lastX) > 0.05f)
        {
            lastX = x;
            sinceProgress = 0;
        }
        else sinceProgress++;

        bool stuck = sinceProgress > StuckFrames;
        bool jump = stuck && frame % 30 < 12;
        int tx = (int)System.MathF.Floor(x + direction * (me.Size.X / 2 + 0.5f));
        int feet = (int)System.MathF.Floor(me.Position.Y + me.Size.Y / 2 - 0.01f);
        int ty = feet;
        for (int dy = 0; dy < 3; dy++)
            if (session.World.GetBlock(tx, feet - dy) != 0) { ty = feet - dy; break; }
        controller.ScriptedInput = new PlayerInput(direction, jump, stuck, tx, ty);
    }

    public void PrintReport(string csvPath)
    {
        using (var file = Godot.FileAccess.Open(csvPath, Godot.FileAccess.ModeFlags.Write))
        {
            file.StoreLine("delta_ms;medido_ms;ticks;alpha;process_ms;sessao_ms;render_ms;remontagens;chunks_novos;gc");
            for (int i = 0; i < samples.Count; i++)
            {
                var s = samples[i];
                file.StoreLine(string.Format(CultureInfo.InvariantCulture,
                    "{0:0.000000};{1:0.000};{2};{3:0.00000};{4:0.000};{5:0.000};{6:0.000};{7};{8};{9}",
                    s.DeltaSeconds * 1000, s.MeasuredSeconds * 1000, s.Ticks, alphas[i], s.ProcessMs, s.UpdateMs, s.RenderMs,
                    s.Rebuilds, s.NewChunks, s.GcCollections));
            }
        }
        foreach (string line in FrameProfileReport.Describe(samples))
            GD.Print("PROFILE " + line);
        GD.Print("PROFILE CSV: " + ProjectSettings.GlobalizePath(csvPath));
    }
}
