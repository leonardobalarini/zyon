using System.Numerics;
using Zyon.Core.Data;
using Zyon.Core.Items;
using Zyon.Core.Sim;
using Zyon.Core.Tiles;

namespace Zyon.Core.Tests;

/// <summary>Mundo plano 64×64 com chão em y=40 e um jogador em pé em x=20.</summary>
internal sealed class SimHarness : IDisposable
{
    public SimHarness(int width = 64, int height = 64, int groundY = 40, float playerX = 20f, Registry? registry = null)
    {
        Registry = registry ?? TestData.Registry();
        GroundY = groundY;
        Tiles = TestWorlds.Flat(Registry, width, height, groundY);
        Sim = new Simulation(Registry, Tiles);
        PlayerId = Sim.SpawnPlayer(new Vector2(playerX, groundY - Registry.Physics.PlayerHeight / 2));
    }

    public Registry Registry { get; }
    public TileWorld Tiles { get; }
    public Simulation Sim { get; }
    public int PlayerId { get; }
    public int GroundY { get; }

    public float StandingY => GroundY - Registry.Physics.PlayerHeight / 2;

    public Vector2 PlayerPosition =>
        Sim.TryGetEntityView(PlayerId, out var v) ? v.Position : throw new InvalidOperationException("jogador sumiu");

    public Inventory Inventory => (Inventory)Sim.GetInventory(PlayerId)!;

    public void Run(int ticks, PlayerInput input)
    {
        Sim.SetInput(PlayerId, input);
        for (int i = 0; i < ticks; i++)
            Sim.Step();
    }

    public void Idle(int ticks) => Run(ticks, default);

    public void Send(ICommand command)
    {
        Sim.Enqueue(command);
        Sim.Step();
    }

    public void Give(string item, int count) => Inventory.Add(Registry.ItemId(item), count);

    public void Dispose() => Sim.Dispose();
}
