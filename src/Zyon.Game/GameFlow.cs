using Godot;
using Zyon.Client;
using Zyon.Core.Data;
using Zyon.Core.WorldGen;

namespace Zyon.Game;

/// <summary>Dados carregados uma vez e o que passa de uma cena para a próxima (menu → carregamento → jogo).</summary>
public static class GameFlow
{
    private static bool initialized;
    private static bool skipUsed;
    private static SaveLibrary devLibrary = null!;

    public static string DataDir { get; private set; } = "";
    public static Registry Registry { get; private set; } = null!;
    public static WorldGenConfig WorldGenConfig { get; private set; } = null!;
    public static ClientVisuals Visuals { get; private set; } = null!;
    public static LaunchOptions Options { get; private set; } = null!;
    /// <summary>Saves do jogador (pasta raiz): o que o menu lista e onde ele cria.</summary>
    public static SaveLibrary Library { get; private set; } = null!;

    /// <summary>Biblioteca do pedido em andamento (a do menu ou a descartável do atalho de desenvolvimento).</summary>
    public static SaveLibrary CurrentLibrary { get; set; } = null!;

    /// <summary>Atalho de desenvolvimento (--dev, --world...) ainda não usado: só vale na primeira abertura do menu.</summary>
    public static bool ShouldSkipMenu => Options.SkipMenu && !skipUsed;

    /// <summary>Pedido do menu para a tela de carregamento.</summary>
    public static LaunchRequest? PendingRequest { get; set; }

    /// <summary>Jogo pronto, da tela de carregamento para a cena de jogo.</summary>
    public static LoadedGame? PendingGame { get; set; }

    /// <summary>Smoke test ou profile-walk: qualquer falha deve encerrar o processo com erro em vez de mostrar a interface.</summary>
    public static bool Automated =>
        initialized ? Options.SmokeTest || Options.ProfileWalk : AutomatedRequested(OS.GetCmdlineUserArgs());

    /// <summary>Carrega dados e opções na primeira chamada. Lança DataException/ArgumentException/IOException se algo estiver errado.</summary>
    public static void EnsureInitialized()
    {
        if (initialized) return;
        Options = LaunchOptions.Parse(OS.GetCmdlineUserArgs());
        DataDir = DataPaths.Resolve();
        Registry = RegistryLoader.LoadFromDirectory(DataDir);
        WorldGenConfig = WorldGenConfigLoader.LoadFromDirectory(DataDir);
        Visuals = ClientVisuals.LoadFromDirectory(DataDir, Registry);
        string root = Options.SavesDir ?? ProjectSettings.GlobalizePath("user://saves");
        // Mundos descartáveis (--dev, smoke test, profile-walk) ficam na subpasta "dev": nunca tocam nos saves
        // do jogador. O menu e --world/--player sem --dev usam a raiz.
        Library = new SaveLibrary(root);
        devLibrary = new SaveLibrary(System.IO.Path.Combine(root, "dev"));
        CurrentLibrary = Library;
        initialized = true;
    }

    /// <summary>
    /// Pedido de jogo quando o menu é pulado. O profile-walk usa o mundo descartável, como o smoke test: o relatório
    /// só é comparável entre execuções se --size/--seed valerem sempre (um mundo "dev" salvo os ignoraria).
    /// </summary>
    public static LaunchRequest BuildSkipMenuRequest()
    {
        skipUsed = true;
        bool throwaway = Options.Dev || Options.SmokeTest || Options.ProfileWalk;
        CurrentLibrary = throwaway ? devLibrary : Library;
        return DevLaunch.Build(CurrentLibrary, Registry, Options.ProfileWalk ? Options with { Dev = true } : Options);
    }

    /// <summary>Troca de cena no fim do frame (seguro de chamar em _Ready/_Process).</summary>
    public static void GoTo(Node from, string scene) =>
        Callable.From(() => from.GetTree().ChangeSceneToFile(scene)).CallDeferred();

    private static bool AutomatedRequested(string[] args) =>
        System.Array.IndexOf(args, "--smoke-test") >= 0 || System.Array.IndexOf(args, "--profile-walk") >= 0;
}
