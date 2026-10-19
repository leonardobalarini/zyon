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
    private int frame;
    private WorldRenderer worldRenderer = null!;
    private GameCamera camera = null!;
    private int playerId;
    private EntityRenderer entityRenderer = null!;
    private PlayerController controller = null!;
    private WorldOverlay overlay = null!;
    private Hotbar hotbar = null!;

    public override void _Ready()
    {
        options = LaunchOptions.Parse(OS.GetCmdlineUserArgs());
        try
        {
            Start();
        }
        catch (System.Exception ex) when (options.SmokeTest)
        {
            // Sem isto uma falha na montagem deixaria o jogo aberto sem nunca chamar Quit.
            GD.PrintErr($"SMOKE FAIL: {ex}");
            session?.Dispose();
            session = null;
            GetTree().Quit(1);
        }
    }

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
        session = new LocalSession(sim, player);
        RenderingServer.SetDefaultClearColor(visuals.Sky.ToGodot());
        GD.Print($"Zyon: mundo {options.Width}×{options.Height}, seed {seed}");

        worldRenderer = new WorldRenderer();
        AddChild(worldRenderer);
        worldRenderer.Init(session.World, visuals);
        entityRenderer = new EntityRenderer();
        AddChild(entityRenderer);
        entityRenderer.Init(session.World, visuals);
        overlay = new WorldOverlay();
        AddChild(overlay);
        overlay.Init(session.World, playerId);
        hotbar = new Hotbar();
        AddChild(hotbar);
        hotbar.Init(session.World, visuals, playerId);
        controller = new PlayerController();
        AddChild(controller);
        controller.Init(session);
        camera = new GameCamera();
        AddChild(camera);
        camera.Init(options.Width, options.Height);
        if (session.World.TryGetEntityView(playerId, out var start))
        {
            camera.Follow(RenderMath.ToPixels(start.Position).ToGodot());
            camera.ResetSmoothing();
        }
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
            }
            if (session.World.TryGetEntityView(playerId, out var me))
                camera.Follow(RenderMath.ToPixels(RenderMath.Interpolate(me.PreviousPosition, me.Position, session.InterpolationAlpha)).ToGodot());
            entityRenderer.Refresh(session.InterpolationAlpha);
            overlay.Refresh(GetGlobalMousePosition());
            hotbar.Refresh();
            worldRenderer.UpdateVisible(camera.VisiblePixels());
            if (options.SmokeTest && ++frame >= options.SmokeFrames)
            {
                GD.Print($"SMOKE OK: {frame} frames, tick {session.World.Tick}, chunks {worldRenderer.ActiveChunkCount}");
                GetTree().Quit(0);
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
