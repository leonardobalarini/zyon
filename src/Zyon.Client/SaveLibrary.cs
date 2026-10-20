using Zyon.Core.Data;
using Zyon.Core.Persistence;

namespace Zyon.Client;

/// <summary>Pastas e arquivos de save: &lt;raiz&gt;/worlds/*.zworld e &lt;raiz&gt;/players/*.zplayer.</summary>
public sealed class SaveLibrary(string root)
{
    public string Root { get; } = root;
    public string WorldsDir => Path.Combine(Root, "worlds");
    public string PlayersDir => Path.Combine(Root, "players");

    public string WorldPath(string name) =>
        Path.Combine(WorldsDir, SaveNames.Validate(name, "nome do mundo", WorldFile.Extension) + WorldFile.Extension);

    public string PlayerPath(string name) =>
        Path.Combine(PlayersDir, PlayerSave.ValidateName(name) + PlayerFile.Extension);

    public IReadOnlyList<string> Worlds() => SaveFiles.List(WorldsDir, WorldFile.Extension);

    public IReadOnlyList<string> Players() => SaveFiles.List(PlayersDir, PlayerFile.Extension);

    public bool WorldExists(string name) => SaveFiles.Exists(WorldPath(name));

    /// <summary>Cria e grava um personagem novo. Nome já usado (ignorando maiúsculas) lança DataException.</summary>
    public PlayerSave CreatePlayer(string name, Registry registry)
    {
        var player = PlayerSave.New(name, registry);
        string path = PlayerPath(player.Name);
        if (SaveFiles.Exists(path) || Players().Contains(player.Name, StringComparer.OrdinalIgnoreCase))
            throw new DataException($"{PlayerFile.Extension}: o personagem '{player.Name}' já existe");
        SaveFiles.WriteAtomic(path, s => PlayerFile.Write(s, player));
        return player;
    }

    public LoadResult<PlayerSave> LoadPlayer(string name, Registry registry) =>
        SaveFiles.ReadWithBackup(PlayerPath(name), s => PlayerFile.Read(s, registry));
}
