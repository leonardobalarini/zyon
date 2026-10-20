using Zyon.Core.Data;
using Zyon.Core.Persistence;

namespace Zyon.Client;

/// <summary>
/// Monta o pedido de jogo quando o menu é pulado. --dev e --smoke-test recriam sempre o mundo (descartável);
/// --world NOME carrega o mundo se ele existir e cria se não existir.
/// </summary>
public static class DevLaunch
{
    public const string DefaultPlayer = "Dev";
    public const string DefaultWorld = "dev";

    public static LaunchRequest Build(SaveLibrary library, Registry registry, LaunchOptions options)
    {
        string playerName = options.Player ?? DefaultPlayer;
        var warnings = new List<string>();
        PlayerSave player;
        if (library.Players().Contains(PlayerSave.ValidateName(playerName), StringComparer.OrdinalIgnoreCase))
        {
            var loaded = library.LoadPlayer(playerName, registry);
            if (loaded.FromBackup) warnings.Add(ErrorMessages.BackupWarning("personagem", playerName, loaded.QuarantinedPath));
            player = loaded.Value;
        }
        else
        {
            player = library.CreatePlayer(playerName, registry);
        }

        string worldName = options.World ?? DefaultWorld;
        bool throwaway = options.Dev || options.SmokeTest;
        if (throwaway)
            foreach (string existing in library.Worlds().Where(w => string.Equals(w, worldName, StringComparison.OrdinalIgnoreCase)).ToList())
                SaveFiles.Delete(library.WorldPath(existing));
        if (throwaway && library.WorldExists(worldName))
            SaveFiles.Delete(library.WorldPath(worldName));
        bool create = !library.WorldExistsIgnoringCase(worldName);
        return new LaunchRequest(player, warnings, worldName, create, options.Width, options.Height,
            options.Seed ?? Environment.TickCount);
    }
}
