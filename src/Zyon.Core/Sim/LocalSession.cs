namespace Zyon.Core.Sim;

/// <summary>
/// Roda a Simulation no mesmo processo, com acumulador de tempo para tick fixo.
/// Assume a posse da Simulation (Dispose a descarta). Quadros mais lentos que 1/12 s rodam em câmera lenta,
/// pois MaxTicksPerUpdate limita os ticks por chamada e o excedente é descartado.
/// </summary>
public sealed class LocalSession(Simulation simulation, int localPlayerId) : IGameSession
{
    public const int MaxTicksPerUpdate = 5;
    private const double TickSeconds = 1.0 / Simulation.TicksPerSecond;

    private double accumulator;

    public int LocalPlayerId { get; } = localPlayerId;
    public IWorldView World => simulation;
    public float InterpolationAlpha { get; private set; }

    public void SetInput(PlayerInput input) => simulation.SetInput(LocalPlayerId, input);

    public void Send(ICommand command) => simulation.Enqueue(command);

    public void Update(double deltaSeconds)
    {
        if (!(deltaSeconds > 0))
            return;

        accumulator += deltaSeconds;
        int ticks = 0;
        while (accumulator >= TickSeconds && ticks < MaxTicksPerUpdate)
        {
            simulation.Step();
            accumulator -= TickSeconds;
            ticks++;
        }
        if (accumulator >= TickSeconds)
            accumulator = 0;
        InterpolationAlpha = Math.Clamp((float)(accumulator / TickSeconds), 0f, 0.99999994f);
    }

    public IReadOnlyList<IGameEvent> DrainEvents() => simulation.DrainEvents();

    public void Dispose() => simulation.Dispose();
}
