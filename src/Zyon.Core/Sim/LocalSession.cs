namespace Zyon.Core.Sim;

/// <summary>
/// Roda a Simulation no mesmo processo, em ticks fixos contados por um <see cref="FrameClock"/> (arredonda deltas
/// próximos de múltiplos do tick para que um monitor de 60 Hz rode sempre 1 tick por quadro).
/// Assume a posse da Simulation (Dispose a descarta). Quadros mais lentos que 1/12 s rodam em câmera lenta,
/// pois MaxTicksPerUpdate limita os ticks por chamada e o excedente é descartado.
/// </summary>
public sealed class LocalSession(Simulation simulation, int localPlayerId,
    double snapToleranceSeconds = FrameClock.DefaultSnapToleranceSeconds) : IGameSession
{
    public const int MaxTicksPerUpdate = 5;

    private readonly FrameClock clock = new(1.0 / Simulation.TicksPerSecond, MaxTicksPerUpdate, snapToleranceSeconds);

    public int LocalPlayerId { get; } = localPlayerId;
    public IWorldView World => simulation;
    public float InterpolationAlpha => clock.Alpha;

    public void SetInput(PlayerInput input) => simulation.SetInput(LocalPlayerId, input);

    public void Send(ICommand command) => simulation.Enqueue(command);

    public void Update(double deltaSeconds)
    {
        int ticks = clock.Advance(deltaSeconds);
        for (int i = 0; i < ticks; i++)
            simulation.Step();
    }

    public IReadOnlyList<IGameEvent> DrainEvents() => simulation.DrainEvents();

    public void Dispose() => simulation.Dispose();
}
