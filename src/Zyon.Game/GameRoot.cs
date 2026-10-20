using System.Diagnostics;
using System.Globalization;
using Godot;
using Zyon.Client;
using Zyon.Core.Sim;
using Zyon.Game.Hud;
using Zyon.Game.Rendering;

namespace Zyon.Game;

/// <summary>Raiz da cena de jogo: cria a sessão e conecta renderização, câmera, controles e HUD.</summary>
public partial class GameRoot : Node2D
{
    private IGameSession? session;
    private LaunchOptions options = null!;
    private LoadedGame game = null!;
    private WorldRenderer worldRenderer = null!;
    private GameCamera camera = null!;
    private int playerId;
    private EntityRenderer entityRenderer = null!;
    private PlayerController controller = null!;
    private WorldOverlay overlay = null!;
    private Hotbar hotbar = null!;
    private SmokeTest? smoke;
    private ProfileWalk? profile;
    // Início do quadro anterior (Stopwatch): o intervalo medido mostra se o delta do motor difere do relógio.
    private long lastFrameStart;
    private DebugOverlay debugOverlay = null!;
    private readonly FrameStats frameStats = new();
    // Só a partir do 60º quadro: no smoke test separa a montagem inicial do regime normal.
    private readonly FrameStats steadyStats = new();
    private int framesProcessed;
    private int lastGcCount;
    // Distância (pixels do mundo) entre o centro da tela e o jogador no início do quadro: atraso visual da câmera.
    private float cameraLagMax;
    private Vector2 lastCameraTarget;

    public override void _Ready()
    {
        var pending = GameFlow.PendingGame;
        GameFlow.PendingGame = null;
        if (pending is null)
        {
            // Cena aberta diretamente (sem passar pelo carregamento): o menu inicializa tudo e mostra erros.
            GameFlow.GoTo(this, Scenes.MainMenu);
            return;
        }
        game = pending;
        options = GameFlow.Options;

        try
        {
            Start();
        }
        catch (System.Exception ex) when (options.SmokeTest || options.ProfileWalk)
        {
            // Sem isto uma falha na montagem deixaria o jogo aberto sem nunca chamar Quit.
            GD.PrintErr($"SMOKE FAIL: {ex}");
            GetTree().Quit(1);
        }
    }

    /// <summary>Monta tudo; a sessão só é publicada no campo no fim, então _Process nunca a vê pela metade.</summary>
    private void Start()
    {
        var visuals = GameFlow.Visuals;
        var sim = game.Simulation;
        playerId = game.PlayerId;
        var newSession = new LocalSession(sim, playerId);
        try
        {
            RenderingServer.SetDefaultClearColor(visuals.Sky.ToGodot());
            GD.Print($"Zyon: mundo '{System.IO.Path.GetFileNameWithoutExtension(game.WorldPath)}' {sim.Width}×{sim.Height}, seed {game.Info.Seed}, personagem '{game.PlayerName}'");

            worldRenderer = new WorldRenderer();
            AddChild(worldRenderer);
            worldRenderer.Init(newSession.World, visuals);
            entityRenderer = new EntityRenderer();
            AddChild(entityRenderer);
            entityRenderer.Init(newSession.World, visuals);
            overlay = new WorldOverlay();
            AddChild(overlay);
            overlay.Init(newSession.World, playerId);
            hotbar = new Hotbar();
            AddChild(hotbar);
            hotbar.Init(newSession.World, visuals, playerId);
            debugOverlay = new DebugOverlay();
            AddChild(debugOverlay);
            controller = new PlayerController();
            AddChild(controller);
            controller.Init(newSession);
            camera = new GameCamera();
            AddChild(camera);
            camera.Init(sim.Width, sim.Height, options.CameraSmoothing);
            if (newSession.World.TryGetEntityView(playerId, out var start))
            {
                camera.Follow(RenderMath.ToPixels(start.Position).ToGodot());
                camera.ResetSmoothing();
            }
            if (options.SmokeTest) smoke = new SmokeTest(newSession, controller, options.SmokeFrames);
            if (options.ProfileWalk)
            {
                profile = new ProfileWalk(newSession, controller, options.SmokeFrames);
                GD.Print("PROFILE ambiente: " + TimingEnvironment());
            }
        }
        catch
        {
            newSession.Dispose();
            throw;
        }
        lastGcCount = System.GC.CollectionCount(0);
        session = newSession;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (session is null || @event is not InputEventKey { Pressed: true, Echo: false } key) return;
        switch (key.PhysicalKeycode)
        {
            case Key.F3:
                debugOverlay.Toggle();
                break;
            case Key.F4:
                camera.ToggleSmoothing();
                GD.Print($"Câmera: {camera.ModeDescription}");
                break;
            case Key.F5:
                var vsync = DisplayServer.WindowGetVsyncMode() == DisplayServer.VSyncMode.Disabled
                    ? DisplayServer.VSyncMode.Enabled : DisplayServer.VSyncMode.Disabled;
                DisplayServer.WindowSetVsyncMode(vsync);
                GD.Print($"VSync: {DisplayServer.WindowGetVsyncMode()}");
                break;
            default:
                return;
        }
        GetViewport().SetInputAsHandled();
    }

    public override void _Process(double delta)
    {
        if (session is null) return;
        try
        {
            long frameStart = Stopwatch.GetTimestamp();
            double measured = lastFrameStart == 0 ? 0 : Stopwatch.GetElapsedTime(lastFrameStart, frameStart).TotalSeconds;
            lastFrameStart = frameStart;
            long tickBefore = session.World.Tick;
            if (framesProcessed > 0)
                cameraLagMax = Mathf.Max(cameraLagMax, camera.GetScreenCenterPosition().DistanceTo(lastCameraTarget));
            controller.Poll(GetGlobalMousePosition());
            session.Update(delta);
            long updateEnd = Stopwatch.GetTimestamp();
            foreach (var gameEvent in session.DrainEvents())
            {
                worldRenderer.OnEvent(gameEvent);
                overlay.OnEvent(gameEvent);
                smoke?.OnEvent(gameEvent);
            }
            // A câmera segue a mesma posição interpolada que o jogador desenhado: com a posição do tick atual
            // (até 1 tick à frente) o mundo rolaria em degraus de 60 Hz e o jogador tremeria em relação à tela.
            if (session.World.TryGetEntityView(playerId, out var me))
            {
                lastCameraTarget = RenderMath.ToPixels(RenderMath.Interpolate(me.PreviousPosition, me.Position, session.InterpolationAlpha)).ToGodot();
                camera.Follow(lastCameraTarget);
            }
            entityRenderer.Refresh(session.InterpolationAlpha);
            overlay.Refresh(GetGlobalMousePosition());
            hotbar.Refresh();
            worldRenderer.UpdateVisible(camera.VisiblePixels());
            long frameEnd = Stopwatch.GetTimestamp();
            RecordFrame(delta, measured, frameStart, updateEnd, frameEnd, (int)(session.World.Tick - tickBefore));
            if (profile is not null)
            {
                profile.Step();
                if (profile.Finished)
                {
                    profile.PrintReport("user://profile-walk.csv");
                    GetTree().Quit(0);
                }
            }
            if (smoke is not null)
            {
                smoke.Step();
                if (smoke.Finished)
                {
                    if (smoke.Failure is null)
                    {
                        PrintSmokeStats();
                        GD.Print($"SMOKE OK: tick {session.World.Tick}, chunks {worldRenderer.ActiveChunkCount}");
                        GetTree().Quit(0);
                    }
                    else
                    {
                        GD.PrintErr($"SMOKE FAIL: {smoke.Failure}");
                        GetTree().Quit(1);
                    }
                }
            }
        }
        catch (System.Exception ex) when (options.SmokeTest || options.ProfileWalk)
        {
            GD.PrintErr($"SMOKE FAIL: {ex}");
            GetTree().Quit(1);
        }
    }

    private void RecordFrame(double delta, double measured, long frameStart, long updateEnd, long frameEnd, int ticks)
    {
        // Contagem da geração 0 inclui as coletas das gerações 1 e 2; mede desde o quadro anterior.
        int gcCount = System.GC.CollectionCount(0);
        var sample = new FrameSample(delta,
            Stopwatch.GetElapsedTime(frameStart, frameEnd).TotalMilliseconds,
            Stopwatch.GetElapsedTime(frameStart, updateEnd).TotalMilliseconds,
            Stopwatch.GetElapsedTime(updateEnd, frameEnd).TotalMilliseconds,
            worldRenderer.RebuildsLastUpdate, gcCount - lastGcCount,
            ticks, measured, worldRenderer.NewChunksLastUpdate);
        lastGcCount = gcCount;
        bool hitch = frameStats.Add(sample);
        if (++framesProcessed > 60) steadyStats.Add(sample);
        profile?.Record(sample, session!.InterpolationAlpha);
        // Só com a sobreposição visível, para não poluir o console.
        if (hitch && debugOverlay.Visible)
            GD.Print(DebugOverlay.HitchText(frameStats.LastHitch!.Value, frameStats.FrameCount));
        if (debugOverlay.ShouldRefresh(delta))
            debugOverlay.Refresh(frameStats, new DebugInfo(worldRenderer.ActiveChunkCount,
                worldRenderer.RebuildsLastUpdate, session!.InterpolationAlpha, ticks,
                camera.ModeDescription, DisplayServer.WindowGetVsyncMode().ToString()));
    }

    /// <summary>Configuração de tempo do motor que afeta o delta entregue a _Process.</summary>
    private static string TimingEnvironment() =>
        $"vsync {DisplayServer.WindowGetVsyncMode()}, delta_smoothing {OS.DeltaSmoothing}, " +
        $"max_fps {Engine.MaxFps}, physics_ticks {Engine.PhysicsTicksPerSecond}, " +
        $"physics_jitter_fix {Engine.PhysicsJitterFix.ToString(CultureInfo.InvariantCulture)}, " +
        $"refresh {DisplayServer.ScreenGetRefreshRate().ToString(CultureInfo.InvariantCulture)}";

    private void PrintSmokeStats()
    {
        static string Line(FrameSummary s) => string.Format(CultureInfo.InvariantCulture,
            "{0} quadros, delta med {1:0.00} máx {2:0.00} ms, _Process med {3:0.000} máx {4:0.000} ms, " +
            "sessão med {5:0.000} máx {6:0.000} ms, render med {7:0.000} máx {8:0.000} ms, " +
            "remontagens {9} (máx {10}/quadro), GC {11}, quadros com 0 ticks {12}, com 2+ ticks {13}",
            s.Frames, s.FrameAvgMs, s.FrameMaxMs, s.ProcessAvgMs, s.ProcessMaxMs, s.UpdateAvgMs, s.UpdateMaxMs,
            s.RenderAvgMs, s.RenderMaxMs, s.Rebuilds, s.MaxRebuildsPerFrame, s.GcCollections,
            s.ZeroTickFrames, s.MultiTickFrames);
        GD.Print($"SMOKE STATS total: {Line(frameStats.Overall)}");
        GD.Print($"SMOKE STATS após 60 quadros: {Line(steadyStats.Overall)}");
        GD.Print(string.Format(CultureInfo.InvariantCulture,
            "SMOKE STATS câmera: {0}, atraso máx {1:0.0} px do mundo ({2:0.00} tiles)",
            camera.ModeDescription, cameraLagMax, cameraLagMax / RenderMath.PixelsPerTile));
        GD.Print("SMOKE STATS apresentação: swapchain_image_count " +
            ProjectSettings.GetSetting("rendering/rendering_device/vsync/swapchain_image_count") +
            ", frame_queue_size " + ProjectSettings.GetSetting("rendering/rendering_device/vsync/frame_queue_size") +
            ", vsync_mode " + ProjectSettings.GetSetting("display/window/vsync/vsync_mode"));
    }

    public override void _ExitTree() => session?.Dispose();
}
