using System.Numerics;
using Zyon.Core.Data;
using Zyon.Core.Persistence;
using Zyon.Core.Sim;

namespace Zyon.Client;

/// <summary>
/// Salva mundo e personagem. Save() captura e serializa na thread chamadora — a do jogo, entre Updates —
/// e enfileira a gravação em disco numa fila única em segundo plano (nunca duas gravações ao mesmo tempo).
/// Erros de disco ficam em LastError; não derrubam o jogo.
/// Mundo e personagem são gravados em sequência, não como par atômico: se um falhar, LastError fica definido e o
/// próximo save bem-sucedido repara. Quem chama deve mostrar LastError ao jogador e tentar de novo ao sair.
/// Falhas que não são de disco (bugs) também vão para LastError e ficam em <see cref="UnexpectedError"/>.
/// </summary>
public sealed class GameSaver(Registry registry, LoadedGame game) : IDisposable
{
    private readonly object gate = new();
    private IReadOnlyDictionary<string, Vector2> knownPositions = game.KnownPositions;
    private Task pending = Task.CompletedTask;
    private int completedSaves;
    private Exception? lastError;
    private Exception? unexpectedError;
    private bool disposed;

    /// <summary>Ponto de injeção para testes; por padrão grava atomicamente.</summary>
    internal Action<string, Action<Stream>> WriteFile { get; set; } = SaveFiles.WriteAtomic;

    /// <summary>Primeira falha que não é de disco (provável bug); nunca é limpa.</summary>
    public Exception? UnexpectedError => Volatile.Read(ref unexpectedError);

    public int CompletedSaves => Volatile.Read(ref completedSaves);
    public Exception? LastError => Volatile.Read(ref lastError);

    public void Save(Simulation sim)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed), this);
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
        try
        {
            Task current;
            lock (gate) current = pending;
            return current.Wait(timeout);
        }
        catch (Exception)
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, true)) return;
        Flush(TimeSpan.FromSeconds(10));
    }

    private void Write(byte[] worldBytes, byte[] playerBytes)
    {
        try
        {
            WriteFile(game.WorldPath, s => s.Write(worldBytes));
            WriteFile(game.PlayerPath, s => s.Write(playerBytes));
            Interlocked.Increment(ref completedSaves);
            Volatile.Write(ref lastError, null);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Volatile.Write(ref lastError, ex);
            if (ex is not (IOException or UnauthorizedAccessException))
                Interlocked.CompareExchange(ref unexpectedError, ex, null);
        }
    }

    private static byte[] Serialize(Action<Stream> write)
    {
        using var buffer = new MemoryStream();
        write(buffer);
        return buffer.ToArray();
    }
}
