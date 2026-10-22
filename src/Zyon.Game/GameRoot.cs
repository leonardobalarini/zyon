using System;
using System.Diagnostics;
using System.Globalization;
using Godot;
using Zyon.Client;
using Zyon.Core.Sim;
using Zyon.Game.Hud;
using Zyon.Game.Menus;
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
    private LightOverlay lightOverlay = null!;
    private WorldOverlay overlay = null!;
    private Hotbar hotbar = null!;
    private InventoryPanel inventory = null!;
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
    private GameSaver saver = null!;
    private AutosaveTimer autosave = null!;
    private PauseMenu pause = null!;
    private Toast toast = null!;
    private AcceptDialog saveFailedDialog = null!;
    private Exception? lastReportedSaveError;
    // Saída pedida (menu ou fechar): o save roda só depois de um quadro desenhado com "Salvando...".
    private bool exiting;
    private bool exitToMenu;
    private ulong exitRequestFrame;
    private bool exitSavePending;
    // "Sair sem salvar": _ExitTree não espera a fila de gravação de novo.
    private bool leftWithoutSaving;
    // Autosave enfileirado: "Jogo salvo." só quando CompletedSaves chegar a este valor (gravação concluída).
    private int? autosaveToastAt;

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
        catch (System.Exception ex)
        {
            // Jogo normal: nada de cena vazia que não fecha — volta ao menu e mostra o erro lá.
            GD.PushError($"Falha ao montar o jogo: {ex}");
            GetTree().AutoAcceptQuit = true;
            GameFlow.MenuMessage = $"Não foi possível abrir o jogo: {ErrorMessages.Describe(ex)}";
            GameFlow.GoTo(this, Scenes.MainMenu);
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
            // Luz depois do mundo e das entidades (multiplica os dois) e antes do WorldOverlay: o destaque do tile
            // mirado e as rachaduras não escurecem. HUD e menus são CanvasLayers e ficam fora do multiply.
            lightOverlay = new LightOverlay();
            AddChild(lightOverlay);
            lightOverlay.Init(newSession.World, GameFlow.Lighting, playerId);
            overlay = new WorldOverlay();
            AddChild(overlay);
            overlay.Init(newSession.World, playerId);
            hotbar = new Hotbar();
            AddChild(hotbar);
            hotbar.Init(newSession.World, visuals, playerId);
            inventory = new InventoryPanel();
            AddChild(inventory);
            inventory.Init(newSession, visuals, hotbar);
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
            saver = new GameSaver(GameFlow.Registry, game);
            autosave = new AutosaveTimer(options.AutosaveSeconds);
            toast = new Toast();
            AddChild(toast);
            toast.Init();
            pause = new PauseMenu();
            AddChild(pause);
            pause.Init(() => controller.Enabled = true, () => RequestExit(toMenu: true), () => RequestExit(toMenu: false));
            saveFailedDialog = BuildSaveFailedDialog();
            GetTree().AutoAcceptQuit = false;
            if (game.Warnings.Count > 0) toast.Show(string.Join("\n", game.Warnings), 10);
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
            case Key.E:
                if (exiting || pause.IsOpen) break;
                inventory.Toggle();
                break;
            case Key.Escape:
                if (exiting) break;
                if (inventory.IsOpen)
                    inventory.Close();
                else if (pause.IsOpen)
                {
                    pause.Close();
                    controller.Enabled = true;
                }
                else
                {
                    controller.Enabled = false;
                    pause.Open();
                }
                break;
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
            // Um quadro inteiro (com "Salvando..." na tela) depois do pedido de saída: salva entre Updates.
            if (exitSavePending && Engine.GetProcessFrames() >= exitRequestFrame + 2)
            {
                exitSavePending = false;
                FinishExit();
                if (session is null) return;
            }
            long tickBefore = session.World.Tick;
            if (framesProcessed > 0)
                cameraLagMax = Mathf.Max(cameraLagMax, camera.GetScreenCenterPosition().DistanceTo(lastCameraTarget));
            controller.Poll(GetGlobalMousePosition());
            session.Update(delta);
            long updateEnd = Stopwatch.GetTimestamp();
            if (autosave.Update(delta) && !exiting)
            {
                int before = saver.CompletedSaves;
                string? problem = SaveNow(waitSeconds: 0);
                if (problem is null) autosaveToastAt = before + 1;
                else toast.Show($"Não foi possível salvar: {problem}", 8);
            }
            if (autosaveToastAt is { } target && saver.CompletedSaves >= target)
            {
                autosaveToastAt = null;
                if (!exiting) toast.Show("Jogo salvo.", 2);
            }
            if (saver.LastError is { } saveError && saveError != lastReportedSaveError)
            {
                lastReportedSaveError = saveError;
                autosaveToastAt = null;
                // Com o aviso de falha aberto o erro já está na tela.
                if (!saveFailedDialog.Visible)
                    toast.Show($"Não foi possível salvar: {ErrorMessages.Describe(saveError)}", 8);
            }
            foreach (var gameEvent in session.DrainEvents())
            {
                worldRenderer.OnEvent(gameEvent);
                lightOverlay.OnEvent(gameEvent);
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
            inventory.Refresh();
            worldRenderer.UpdateVisible(camera.VisiblePixels());
            lightOverlay.UpdateLight(camera.VisiblePixels());
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
                        VerifySmokeSave();
                        PrintSmokeStats();
                        GD.Print($"SMOKE OK: tick {session.World.Tick}, chunks {worldRenderer.ActiveChunkCount}, save ok");
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
                camera.ModeDescription, DisplayServer.WindowGetVsyncMode().ToString(),
                lightOverlay.RecomputesLastUpdate, lightOverlay.LastComputeMs, lightOverlay.SkyFactor));
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

    public override void _Notification(int what)
    {
        if (what != NotificationWMCloseRequest) return;
        // Fechar de novo com o aviso de falha aberto: sai sem salvar.
        if (saveFailedDialog is not null && saveFailedDialog.Visible) GetTree().Quit();
        else RequestExit(toMenu: false);
    }

    /// <summary>Mostra "Salvando..." e agenda o save para depois de um quadro desenhado (ver _Process).</summary>
    private void RequestExit(bool toMenu)
    {
        if (exiting || session is null) return;
        exiting = true;
        exitToMenu = toMenu;
        controller.Enabled = false;
        pause.Close();
        inventory.Close();
        ScheduleExitSave();
    }

    private void ScheduleExitSave()
    {
        toast.Show("Salvando...", 3600);
        exitRequestFrame = Engine.GetProcessFrames();
        exitSavePending = true;
    }

    private void FinishExit()
    {
        string? problem = SaveNow(waitSeconds: 10);
        toast.Clear();
        if (problem is not null)
        {
            saveFailedDialog.DialogText = $"O jogo pode não ter sido salvo por completo.\n{problem}";
            saveFailedDialog.PopupCentered(new Vector2I(560, 0));
            return;
        }
        Leave();
    }

    private void Leave()
    {
        if (exitToMenu)
        {
            session?.Dispose();
            session = null;
            GameFlow.GoTo(this, Scenes.MainMenu);
        }
        else
        {
            GetTree().Quit();
        }
    }

    /// <summary>
    /// Captura e enfileira mundo + personagem; com waitSeconds &gt; 0 espera a gravação em disco.
    /// Devolve null se deu certo, ou a explicação para o jogador.
    /// </summary>
    private string? SaveNow(double waitSeconds)
    {
        try
        {
            saver.Save(game.Simulation);
        }
        catch (Exception ex)
        {
            // Erro na captura/serialização (bug ou dados inválidos): registra inteiro e avisa, sem derrubar o jogo.
            GD.PushError($"Falha ao salvar: {ex}");
            return ErrorMessages.Describe(ex);
        }
        if (waitSeconds <= 0) return null;
        if (!saver.Flush(TimeSpan.FromSeconds(waitSeconds))) return "A gravação em disco não terminou a tempo.";
        return saver.LastError is { } error ? ErrorMessages.Describe(error) : null;
    }

    private AcceptDialog BuildSaveFailedDialog()
    {
        var dialog = new AcceptDialog
        {
            Title = "Falha ao salvar",
            OkButtonText = "Tentar de novo",
            Exclusive = true,
            DialogAutowrap = true,
            // Cabe na tela de 640×360; o texto quebra em linhas (caminhos longos inclusive).
            Size = new Vector2I(560, 160),
            MaxSize = new Vector2I(600, 340),
            Theme = new Theme { DefaultFontSize = 10 },
        };
        dialog.AddButton("Sair sem salvar", right: true, action: "leave");
        dialog.AddCancelButton("Voltar ao jogo");
        dialog.Confirmed += ScheduleExitSave;
        dialog.CustomAction += action =>
        {
            if (action != "leave") return;
            dialog.Hide();
            leftWithoutSaving = true;
            Leave();
        };
        dialog.Canceled += () =>
        {
            exiting = false;
            controller.Enabled = true;
        };
        AddChild(dialog);
        return dialog;
    }

    /// <summary>Smoke test: salva, espera o disco e confere que o mundo relido é igual ao mundo em jogo.</summary>
    private void VerifySmokeSave()
    {
        saver.Save(game.Simulation);
        if (!saver.Flush(TimeSpan.FromSeconds(10)) || saver.LastError is not null)
            throw new InvalidOperationException($"save falhou: {saver.LastError?.Message ?? "tempo esgotado"}");
        var reloaded = Zyon.Core.Persistence.SaveFiles.ReadWithBackup(game.WorldPath,
            s => Zyon.Core.Persistence.WorldFile.Read(s, GameFlow.Registry)).Value;
        if (Zyon.Core.Tiles.TileWorldHash.Compute(reloaded.Tiles) != Zyon.Core.Tiles.TileWorldHash.Compute(game.Simulation.Tiles))
            throw new InvalidOperationException("o mundo salvo difere do mundo em jogo");
    }

    public override void _ExitTree()
    {
        // O saver espera a fila de gravação antes de a simulação ser descartada (as gravações só usam bytes já
        // serializados). Se o jogador escolheu sair sem salvar, não espera de novo.
        if (leftWithoutSaving) saver?.Flush(TimeSpan.Zero);
        else saver?.Dispose();
        session?.Dispose();
    }
}
