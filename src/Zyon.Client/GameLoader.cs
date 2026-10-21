using System.Numerics;
using Zyon.Core.Data;
using Zyon.Core.Logging;
using Zyon.Core.Persistence;
using Zyon.Core.Sim;
using Zyon.Core.WorldGen;

namespace Zyon.Client;

/// <summary>Um jogo pronto para começar: simulação com o personagem dentro e o que é preciso para salvar depois.</summary>
public sealed record LoadedGame(
    Simulation Simulation,
    int PlayerId,
    string PlayerName,
    WorldInfo Info,
    IReadOnlyDictionary<string, Vector2> KnownPositions,
    string WorldPath,
    string PlayerPath,
    IReadOnlyList<string> Warnings);

/// <summary>Cria (gera + grava) ou carrega um mundo e coloca o personagem nele. Pode rodar fora da thread do jogo.</summary>
public static class GameLoader
{
    public static LoadedGame Run(SaveLibrary library, Registry registry, WorldGenConfig config, LaunchRequest request,
        IProgress<WorldGenProgress>? progress, CancellationToken cancellationToken, ILog? log = null)
    {
        string worldPath = library.WorldPath(request.WorldName);
        string playerPath = library.PlayerPath(request.Player.Name);
        var warnings = new List<string>(request.Warnings);
        WorldSave world;

        if (request.CreateNew)
        {
            if (library.Worlds().Contains(Path.GetFileNameWithoutExtension(worldPath), StringComparer.OrdinalIgnoreCase))
                throw new DataException($"{WorldFile.Extension}: o mundo '{request.WorldName}' já existe");
            var generated = new WorldGenerator(registry, config)
                .Generate(request.Width, request.Height, request.Seed, progress, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            world = WorldSave.FromGenerated(generated);
            SaveFiles.WriteAtomic(worldPath, s => WorldFile.Write(s, world, registry));
        }
        else
        {
            var loaded = SaveFiles.ReadWithBackup(worldPath, s => WorldFile.Read(s, registry));
            if (loaded.FromBackup)
                warnings.Add(ErrorMessages.BackupWarning("mundo", request.WorldName, loaded.QuarantinedPath));
            world = loaded.Value;
        }

        var sim = world.CreateSimulation(registry, log);
        try
        {
            int playerId = request.Player.SpawnInto(sim, world.PlayerPosition(request.Player.Name, registry.Physics));
            return new LoadedGame(sim, playerId, request.Player.Name, world.Info, world.PlayerPositions,
                worldPath, playerPath, warnings);
        }
        catch
        {
            sim.Dispose();
            throw;
        }
    }
}
