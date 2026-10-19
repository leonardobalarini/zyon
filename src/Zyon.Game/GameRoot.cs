using Godot;
using Zyon.Client;
using Zyon.Core.Data;
using Zyon.Core.Persistence;
using Zyon.Core.Sim;
using Zyon.Core.WorldGen;
using Zyon.Game.Hud;
using Zyon.Game.Rendering;

namespace Zyon.Game;

/// <summary>Raiz da cena de jogo: cria a sessão e conecta renderização, câmera, controles e HUD.</summary>
public partial class GameRoot : Node2D
{
    private IGameSession? session;
    private LaunchOptions options = null!;
    private WorldRenderer worldRenderer = null!;
    private GameCamera camera = null!;
    private int playerId;
    private EntityRenderer entityRenderer = null!;
    private PlayerController controller = null!;
    private WorldOverlay overlay = null!;
    private Hotbar hotbar = null!;
    private SmokeTest? smoke;

    public override void _Ready()
    {
        string[] args = OS.GetCmdlineUserArgs();
        bool smokeRequested = System.Array.IndexOf(args, "--smoke-test") >= 0;
        try
        {
            options = LaunchOptions.Parse(args);
        }
        catch (System.ArgumentException ex)
        {
            GD.PrintErr(smokeRequested ? $"SMOKE FAIL: argumentos inválidos: {ex.Message}" : $"Argumentos inválidos: {ex.Message}");
            GetTree().Quit(smokeRequested ? 1 : 2);
            return;
        }

        try
        {
            Start();
        }
        catch (System.Exception ex) when (options.SmokeTest)
        {
            // Sem isto uma falha na montagem deixaria o jogo aberto sem nunca chamar Quit.
            GD.PrintErr($"SMOKE FAIL: {ex}");
            GetTree().Quit(1);
        }
    }

    /// <summary>Monta tudo; a sessão só é publicada no campo no fim, então _Process nunca a vê pela metade.</summary>
    private void Start()
    {
        string dataDir = DataPaths.Resolve();
        var registry = RegistryLoader.LoadFromDirectory(dataDir);
        var visuals = ClientVisuals.LoadFromDirectory(dataDir, registry);
        int seed = options.Seed ?? System.Environment.TickCount;
        var generated = new WorldGenerator(registry, WorldGenConfigLoader.LoadFromDirectory(dataDir))
            .Generate(options.Width, options.Height, seed);
        var world = WorldSave.FromGenerated(generated);
        var sim = world.CreateSimulation(registry, new GodotLog());
        int player = PlayerSave.New("Dev", registry).SpawnInto(sim, world.PlayerPosition("Dev", registry.Physics));
        playerId = player;
        var newSession = new LocalSession(sim, player);
        try
        {
            RenderingServer.SetDefaultClearColor(visuals.Sky.ToGodot());
            GD.Print($"Zyon: mundo {options.Width}×{options.Height}, seed {seed}");

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
            controller = new PlayerController();
            AddChild(controller);
            controller.Init(newSession);
            camera = new GameCamera();
            AddChild(camera);
            camera.Init(options.Width, options.Height);
            if (newSession.World.TryGetEntityView(playerId, out var start))
            {
                camera.Follow(RenderMath.ToPixels(start.Position).ToGodot());
                camera.ResetSmoothing();
            }
            if (options.SmokeTest) smoke = new SmokeTest(newSession, controller, options.SmokeFrames);
        }
        catch
        {
            newSession.Dispose();
            throw;
        }
        session = newSession;
    }

    public override void _Process(double delta)
    {
        if (session is null) return;
        try
        {
            controller.Poll(GetGlobalMousePosition());
            session.Update(delta);
            foreach (var gameEvent in session.DrainEvents())
            {
                worldRenderer.OnEvent(gameEvent);
                overlay.OnEvent(gameEvent);
                smoke?.OnEvent(gameEvent);
            }
            if (session.World.TryGetEntityView(playerId, out var me))
                camera.Follow(RenderMath.ToPixels(RenderMath.Interpolate(me.PreviousPosition, me.Position, session.InterpolationAlpha)).ToGodot());
            entityRenderer.Refresh(session.InterpolationAlpha);
            overlay.Refresh(GetGlobalMousePosition());
            hotbar.Refresh();
            worldRenderer.UpdateVisible(camera.VisiblePixels());
            if (smoke is not null)
            {
                smoke.Step();
                if (smoke.Finished)
                {
                    if (smoke.Failure is null)
                    {
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
        catch (System.Exception ex) when (options.SmokeTest)
        {
            GD.PrintErr($"SMOKE FAIL: {ex}");
            GetTree().Quit(1);
        }
    }

    public override void _ExitTree() => session?.Dispose();
}
