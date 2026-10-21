namespace Zyon.Client;

/// <summary>Dispara uma vez a cada intervalo de tempo de jogo.</summary>
public sealed class AutosaveTimer(double intervalSeconds)
{
    private readonly double intervalSeconds = double.IsFinite(intervalSeconds) && intervalSeconds > 0
        ? intervalSeconds
        : throw new ArgumentOutOfRangeException(nameof(intervalSeconds), "o intervalo deve ser finito e > 0");
    private double elapsed;

    public bool Update(double deltaSeconds)
    {
        if (!(deltaSeconds > 0)) return false;
        elapsed += deltaSeconds;
        if (elapsed < intervalSeconds) return false;
        elapsed = 0;
        return true;
    }

    public void Reset() => elapsed = 0;
}
