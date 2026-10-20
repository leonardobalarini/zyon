using System.Numerics;
using Zyon.Core.Data;
using Zyon.Core.Persistence;
using Zyon.Core.Sim;

namespace Zyon.Client;

/// <summary>
/// Salva mundo e personagem. Save() captura e serializa na thread chamadora — a do jogo, entre Updates —
/// e enfileira a gravação em disco numa fila única em segundo plano (nunca duas gravações ao mesmo tempo).
/// Erros de disco ficam em LastError; não derrubam o jogo.
/// </summary>
public sealed class GameSaver(Registry registry, LoadedGame game) : IDisposable
{
    private readonly object gate = new();
    private IReadOnlyDictionary<string, Vector2> knownPositions = game.KnownPositions;
    private Task pending = Task.CompletedTask;
    private int completedSaves;
    private Exception? lastError;

    public int CompletedSaves => Volatile.Read(ref completedSaves);
    public Exception? LastError => Volatile.Read(ref lastError);

    public void Save(Simulation sim)
    {
        var world = WorldSave.Capture(sim, game.Info,
            new Dictionary<string, int> { [game.PlayerName] = game.PlayerId }, knownPositions);
        knownPositions = world.PlayerPositions;
        byte[] worldBytes = Serialize(s => WorldFile.Write(s, world, registry));
        byte[] playerBytes = Serialize(s => PlayerFile.Write(s, PlayerSave.Capture(sim, game.PlayerId, game.PlayerName)));

        lock (gate)
            pending = pending.ContinueWith(_ => Write(worldBytes, playerBytes), TaskScheduler.Default);
    }

    /// <summary>Espera as gravações pendentes. Devolve false se o tempo acabar.</summary>
    public bool Flush(TimeSpan timeout)
    {
        Task current;
        lock (gate) current = pending;
        return current.Wait(timeout);
    }

    public void Dispose() => Flush(TimeSpan.FromSeconds(10));

    private void Write(byte[] worldBytes, byte[] playerBytes)
    {
        try
        {
            SaveFiles.WriteAtomic(game.WorldPath, s => s.Write(worldBytes));
            SaveFiles.WriteAtomic(game.PlayerPath, s => s.Write(playerBytes));
            Interlocked.Increment(ref completedSaves);
            Volatile.Write(ref lastError, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Volatile.Write(ref lastError, ex);
        }
    }

    private static byte[] Serialize(Action<Stream> write)
    {
        using var buffer = new MemoryStream();
        write(buffer);
        return buffer.ToArray();
    }
}
