using Godot;
using Zyon.Client;
using Zyon.Core.Data;
using Zyon.Core.Persistence;
using Zyon.Core.Sim;
using Zyon.Core.WorldGen;

namespace Zyon.Game;

/// <summary>Raiz da cena de jogo: cria a sessão e conecta renderização, câmera, controles e HUD.</summary>
public partial class GameRoot : Node2D
{
    private IGameSession? session;
    private LaunchOptions options = null!;
    private int frame;

    public override void _Ready()
    {
        options = LaunchOptions.Parse(OS.GetCmdlineUserArgs());
        try
        {
            Start();
        }
        catch (System.Exception ex) when (options.SmokeTest)
        {
            GD.PrintErr($"SMOKE FAIL: {ex}");
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
        session = new LocalSession(sim, player);
        RenderingServer.SetDefaultClearColor(visuals.Sky.ToGodot());
        GD.Print($"Zyon: mundo {options.Width}×{options.Height}, seed {seed}");
    }

    public override void _Process(double delta)
    {
        if (session is null) return;
        try
        {
            session.Update(delta);
            session.DrainEvents();
            if (options.SmokeTest && ++frame >= options.SmokeFrames)
            {
                GD.Print($"SMOKE OK: {frame} frames, tick {session.World.Tick}");
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
