namespace Zyon.Client;

/// <summary>Dispara uma vez a cada intervalo de tempo de jogo.</summary>
public sealed class AutosaveTimer(double intervalSeconds)
{
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
