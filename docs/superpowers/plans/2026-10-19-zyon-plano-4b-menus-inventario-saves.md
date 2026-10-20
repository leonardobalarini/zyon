# Zyon — Plano 4b: Menus, inventário e saves no jogo — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fechar a fatia vertical: menu inicial (criar/escolher personagem e mundo), tela de carregamento com progresso e cancelamento, autosave a cada 5 min e ao sair, menu de pausa, avisos de backup e inventário completo com arrastar/soltar — cobrindo os critérios de sucesso do spec (seção 12).

**Architecture:** Todo o fluxo testável fica em `Zyon.Client` (.NET puro): `SaveLibrary` (pastas e nomes de arquivo), `DevLaunch` (atalho de desenvolvimento/smoke), `GameLoader` (criar/carregar mundo e personagem → simulação pronta), `GameSaver` (captura na thread do jogo, grava em segundo plano numa fila única), `AutosaveTimer`, `LatestProgress` e `ErrorMessages`. O Godot só monta telas, chama esses serviços e troca de cena; um `GameFlow` estático leva o pedido do menu até a tela de carregamento e o jogo carregado até a cena de jogo.

**Tech Stack:** Godot 4.7.2 .NET, .NET 10, xUnit 2.9.

**Spec:** seções 7 (interface), 8 (save/autosave) e 12 (critérios de sucesso). Pendências incorporadas: `SaveFiles.Exists/Delete/List` com arquivos laterais, validador de nome compartilhado por mundos e personagens, fila única de gravação, avisos de `FromBackup`, `SaveException` como contrato de erro.

**Pré-requisito:** a branch do plano 4a (incluindo a correção de latência em andamento) mesclada na `main`.

---

## Convenções (valem para todas as tarefas)

- Tudo do `CLAUDE.md` continua valendo. Lógica testável em `Zyon.Client`; `Zyon.Game` fino; nós Godot com construtor vazio + `Init(...)` quando criados por código.
- **Pastas de save:** `<raiz>/worlds/<nome>.zworld` e `<raiz>/players/<nome>.zplayer`; raiz padrão `user://saves` (globalizada), sobrescrita por `--saves-dir`.
- **Thread:** a captura do save acontece na thread do jogo entre `Update`s; só os bytes vão para a fila em segundo plano. UI do Godot só é tocada no `_Process` (nunca em callbacks de outras threads).
- **Verificação:** `dotnet test` + `dotnet build src/Zyon.Game/Zyon.Game.csproj` + `tools/smoke-test.sh 600`.
- **Shell:** Bash (Git Bash) a partir de `D:\Projetos\Zyon`. **Commits:** mensagem + segundo `-m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"`; nunca `--amend`.
- **Branch:** `feat/plano-4b-menus-inventario-saves` (criada pelo controlador a partir da `main`).

## Mapa de arquivos

```
src/Zyon.Core/Persistence/
  SaveNames.cs              # validador de nome de arquivo (mundos e personagens)
  PlayerSave.cs             # (mod) ValidateName delega para SaveNames
  SaveFiles.cs              # (mod) Exists, Delete, List
src/Zyon.Client/
  LaunchOptions.cs          # (mod) --dev, --world, --player, --saves-dir, --autosave-seconds
  SaveLibrary.cs  LaunchRequest.cs  DevLaunch.cs
  GameLoader.cs  ErrorMessages.cs  LatestProgress.cs
  GameSaver.cs  AutosaveTimer.cs
src/Zyon.Game/
  GameFlow.cs  Scenes.cs
  scenes/MainMenu.tscn  scenes/Loading.tscn  (Game.tscn já existe)
  Menus/MainMenu.cs  Menus/LoadingScreen.cs  Menus/PauseMenu.cs
  Hud/Toast.cs  Hud/InventoryPanel.cs  Hud/InventorySlot.cs
  GameRoot.cs  PlayerController.cs  SmokeTest.cs  project.godot   # (mod)
tools/smoke-test.sh  tools/play.sh  tools/play.ps1                # (mod)
tests/Zyon.Core.Tests/SaveNamesTests.cs  SaveFilesListingTests.cs
tests/Zyon.Client.Tests/SaveLibraryTests.cs  DevLaunchTests.cs  GameLoaderTests.cs
  ErrorMessagesTests.cs  GameSaverTests.cs  AutosaveTimerTests.cs  LaunchOptionsTests.cs (mod)
```

---

### Task 1: Nomes de arquivo e utilitários de save (Core)

**Files:**
- Create: `src/Zyon.Core/Persistence/SaveNames.cs`
- Modify: `src/Zyon.Core/Persistence/PlayerSave.cs`, `src/Zyon.Core/Persistence/SaveFiles.cs`
- Test: `tests/Zyon.Core.Tests/SaveNamesTests.cs`, `tests/Zyon.Core.Tests/SaveFilesListingTests.cs`

- [ ] **Step 1: Testes que falham**

`tests/Zyon.Core.Tests/SaveNamesTests.cs`:

```csharp
using Zyon.Core.Data;
using Zyon.Core.Persistence;

namespace Zyon.Core.Tests;

public class SaveNamesTests
{
    [Theory]
    [InlineData(" Terra Nova ", "Terra Nova")]
    [InlineData("Mundo-1", "Mundo-1")]
    public void Valid_ReturnsTrimmedNfc(string input, string expected)
    {
        Assert.Equal(expected, SaveNames.Validate(input, "nome do mundo", ".zworld"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("a/b")]
    [InlineData("CON")]
    [InlineData("mundo.")]
    [InlineData("Mundo\u200B")]
    public void Invalid_ThrowsWithSubjectAndPrefix(string input)
    {
        var ex = Assert.Throws<DataException>(() => SaveNames.Validate(input, "nome do mundo", ".zworld"));

        Assert.StartsWith(".zworld:", ex.Message);
        Assert.Contains("nome do mundo", ex.Message);
    }

    [Fact]
    public void PlayerValidateName_StillUsesPlayerMessages()
    {
        var ex = Assert.Throws<DataException>(() => PlayerSave.ValidateName("CON"));

        Assert.StartsWith(".zplayer:", ex.Message);
        Assert.Contains("nome do personagem", ex.Message);
    }
}
```

`tests/Zyon.Core.Tests/SaveFilesListingTests.cs`:

```csharp
using Zyon.Core.Persistence;

namespace Zyon.Core.Tests;

public sealed class SaveFilesListingTests : IDisposable
{
    private readonly string dir = Path.Combine(Path.GetTempPath(), "zyon-list-" + Guid.NewGuid());

    public SaveFilesListingTests() => Directory.CreateDirectory(dir);

    public void Dispose() => Directory.Delete(dir, recursive: true);

    private void Touch(string file) => File.WriteAllText(Path.Combine(dir, file), "x");

    [Fact]
    public void List_ReturnsBaseNames_IncludingRecoverableOnes_SortedIgnoringCase()
    {
        Touch("beta.zworld");
        Touch("beta.zworld.bak");
        Touch("Alfa.zworld");
        Touch("gama.zworld.tmp");        // só .tmp: recuperável
        Touch("delta.zworld.bak");       // só .bak: recuperável
        Touch("eco.zworld.corrupt");     // só quarentena: não lista
        Touch("leo.zplayer");            // outra extensão

        Assert.Equal(new[] { "Alfa", "beta", "delta", "gama" }, SaveFiles.List(dir, ".zworld"));
    }

    [Fact]
    public void List_MissingDirectory_IsEmpty()
    {
        Assert.Empty(SaveFiles.List(Path.Combine(dir, "nao-existe"), ".zworld"));
    }

    [Fact]
    public void Exists_TrueForMainTempOrBackup()
    {
        string path = Path.Combine(dir, "m.zworld");
        Assert.False(SaveFiles.Exists(path));
        Touch("m.zworld.bak");
        Assert.True(SaveFiles.Exists(path));
    }

    [Fact]
    public void Delete_RemovesMainAndAllSidecars()
    {
        foreach (var f in new[] { "m.zworld", "m.zworld.bak", "m.zworld.tmp", "m.zworld.corrupt", "m.zworld.tmp.orphan" })
            Touch(f);
        Touch("outro.zworld");

        SaveFiles.Delete(Path.Combine(dir, "m.zworld"));

        Assert.Equal(new[] { "outro.zworld" }, Directory.GetFiles(dir).Select(Path.GetFileName));
    }
}
```

Run: `dotnet test --filter "FullyQualifiedName~SaveNamesTests|FullyQualifiedName~SaveFilesListingTests"` → FAIL na compilação.

- [ ] **Step 2: `SaveNames`**

Criar `src/Zyon.Core/Persistence/SaveNames.cs` movendo para lá a lógica que hoje está em `PlayerSave.ValidateName` (normalização NFC, trim, comprimento máximo 24, controle, formatação invisível, caracteres inválidos de arquivo, ponto final, nomes reservados incluindo `COM¹²³`/`LPT¹²³`/`CONIN$`/`CONOUT$`), parametrizada pelo assunto e pelo prefixo:

```csharp
namespace Zyon.Core.Persistence;

/// <summary>Validação de nomes que viram nome de arquivo (personagens e mundos).</summary>
public static class SaveNames
{
    public const int MaxLength = 24;

    /// <summary>
    /// Devolve o nome normalizado (NFC, sem espaços nas pontas) ou lança DataException
    /// "<prefix>: <subject> ('name') ...". Use sempre o valor devolvido.
    /// </summary>
    public static string Validate(string? name, string subject, string prefix)
    {
        // corpo movido de PlayerSave.ValidateName, trocando "nome do personagem" por {subject}
        // e o prefixo fixo ".zplayer" por {prefix}.
    }
}
```

(O comentário acima descreve exatamente o que copiar: o corpo atual de `ValidateName`, com as mesmas regras e a mesma ordem de checagens; as constantes `DeviceSuffixes`/`ReservedNames` vão junto, como `private static readonly`.)

Em `PlayerSave.cs`: `MaxNameLength` passa a ser `SaveNames.MaxLength`, as constantes de nomes reservados saem, e

```csharp
    public static string ValidateName(string name) =>
        SaveNames.Validate(name, "nome do personagem", PlayerFile.Extension);
```

- [ ] **Step 3: `SaveFiles.Exists/Delete/List`**

Em `src/Zyon.Core/Persistence/SaveFiles.cs`, adicionar:

```csharp
    /// <summary>Existe algo carregável para este save (principal, .tmp de recuperação ou .bak)?</summary>
    public static bool Exists(string path) =>
        File.Exists(path) || File.Exists(path + TempSuffix) || File.Exists(path + BackupSuffix);

    /// <summary>Apaga o save e todos os arquivos laterais (.tmp, .bak, .corrupt, .tmp.orphan).</summary>
    public static void Delete(string path)
    {
        foreach (string suffix in new[] { "", TempSuffix, BackupSuffix, CorruptSuffix, TempSuffix + OrphanSuffix })
        {
            string file = path + suffix;
            if (File.Exists(file)) File.Delete(file);
        }
    }

    /// <summary>
    /// Nomes (sem extensão) dos saves carregáveis da pasta — principal, .tmp ou .bak — em ordem alfabética
    /// sem diferenciar maiúsculas. Arquivos só em quarentena (.corrupt) ou órfãos não aparecem.
    /// </summary>
    public static IReadOnlyList<string> List(string directory, string extension)
    {
        if (!Directory.Exists(directory)) return [];
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string suffix in new[] { "", TempSuffix, BackupSuffix })
        {
            string ending = extension + suffix;
            foreach (string file in Directory.EnumerateFiles(directory, "*" + ending))
            {
                string fileName = Path.GetFileName(file);
                if (fileName.EndsWith(ending, StringComparison.OrdinalIgnoreCase))
                    names.Add(fileName[..^ending.Length]);
            }
        }
        return names.Order(StringComparer.OrdinalIgnoreCase).ToList();
    }
```

(`Directory.EnumerateFiles` com padrão `*.zworld` no Windows também casa `*.zworld.bak`? Não — mas casa nomes de extensão longa por compatibilidade 8.3 em alguns casos; por isso o `EndsWith` confirma.)

- [ ] **Step 4: Rodar e confirmar**

Run: `dotnet test` → tudo verde (os testes antigos de `ValidateName` continuam passando).

- [ ] **Step 5: Commit**

```bash
git add src/Zyon.Core/Persistence tests/Zyon.Core.Tests/SaveNamesTests.cs tests/Zyon.Core.Tests/SaveFilesListingTests.cs
git commit -m "feat(persistence): validador de nomes compartilhado e listagem/remoção de saves" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Biblioteca de saves e atalho de desenvolvimento (Client)

**Files:**
- Modify: `src/Zyon.Client/LaunchOptions.cs`, `tests/Zyon.Client.Tests/LaunchOptionsTests.cs`
- Create: `src/Zyon.Client/SaveLibrary.cs`, `src/Zyon.Client/LaunchRequest.cs`, `src/Zyon.Client/DevLaunch.cs`
- Test: `tests/Zyon.Client.Tests/SaveLibraryTests.cs`, `tests/Zyon.Client.Tests/DevLaunchTests.cs`

- [ ] **Step 1: Testes que falham**

Adicionar a `LaunchOptionsTests`:

```csharp
    [Fact]
    public void Parses_MenuSkippingOptions()
    {
        var o = LaunchOptions.Parse(["--dev", "--world", "Teste", "--player", "Ana", "--saves-dir", "C:/tmp/s", "--autosave-seconds", "2.5"]);

        Assert.True(o.Dev);
        Assert.Equal("Teste", o.World);
        Assert.Equal("Ana", o.Player);
        Assert.Equal("C:/tmp/s", o.SavesDir);
        Assert.Equal(2.5, o.AutosaveSeconds);
        Assert.True(o.SkipMenu);
    }

    [Fact]
    public void NoArguments_ShowsMenu_WithFiveMinuteAutosave()
    {
        var o = LaunchOptions.Parse([]);

        Assert.False(o.SkipMenu);
        Assert.Equal(300.0, o.AutosaveSeconds);
    }

    [Fact]
    public void SmokeTest_SkipsMenu()
    {
        Assert.True(LaunchOptions.Parse(["--smoke-test"]).SkipMenu);
    }
```

`tests/Zyon.Client.Tests/SaveLibraryTests.cs`:

```csharp
using Zyon.Core.Data;
using Zyon.Core.Persistence;

namespace Zyon.Client.Tests;

public sealed class SaveLibraryTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "zyon-lib-" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void Paths_UseValidatedNames_InTheirFolders()
    {
        var lib = new SaveLibrary(root);

        Assert.Equal(Path.Combine(root, "worlds", "Terra.zworld"), lib.WorldPath(" Terra "));
        Assert.Equal(Path.Combine(root, "players", "Ana.zplayer"), lib.PlayerPath("Ana"));
        Assert.Throws<DataException>(() => lib.WorldPath("a/b"));
    }

    [Fact]
    public void CreatePlayer_WritesFile_AndListsIt_AndRejectsDuplicates()
    {
        var lib = new SaveLibrary(root);
        var r = TestData.Registry();

        var ana = lib.CreatePlayer("Ana", r);

        Assert.Equal("Ana", ana.Name);
        Assert.Equal(new[] { "Ana" }, lib.Players());
        Assert.Equal(ana.Slots, lib.LoadPlayer("Ana", r).Value.Slots);
        var ex = Assert.Throws<DataException>(() => lib.CreatePlayer("ana", r));
        Assert.Contains("já existe", ex.Message);
    }

    [Fact]
    public void Lists_AreEmpty_BeforeAnySave()
    {
        var lib = new SaveLibrary(root);

        Assert.Empty(lib.Worlds());
        Assert.Empty(lib.Players());
        Assert.False(lib.WorldExists("x"));
    }
}
```

`tests/Zyon.Client.Tests/DevLaunchTests.cs`:

```csharp
using Zyon.Core.Persistence;

namespace Zyon.Client.Tests;

public sealed class DevLaunchTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "zyon-dev-" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void Dev_CreatesPlayer_AndAlwaysRecreatesTheDevWorld()
    {
        var lib = new SaveLibrary(root);
        var r = TestData.Registry();
        Directory.CreateDirectory(lib.WorldsDir);
        File.WriteAllText(lib.WorldPath("dev"), "velho");

        var req = DevLaunch.Build(lib, r, LaunchOptions.Parse(["--dev", "--seed", "7"]));

        Assert.Equal("Dev", req.Player.Name);
        Assert.Equal("dev", req.WorldName);
        Assert.True(req.CreateNew);
        Assert.Equal(7, req.Seed);
        Assert.False(SaveFiles.Exists(lib.WorldPath("dev")));
        Assert.Contains("Dev", lib.Players());
    }

    [Fact]
    public void NamedWorld_LoadsWhenItExists_CreatesWhenNot()
    {
        var lib = new SaveLibrary(root);
        var r = TestData.Registry();

        var first = DevLaunch.Build(lib, r, LaunchOptions.Parse(["--world", "Casa", "--player", "Ana"]));
        Directory.CreateDirectory(lib.WorldsDir);
        File.WriteAllText(lib.WorldPath("Casa"), "existe");
        var second = DevLaunch.Build(lib, r, LaunchOptions.Parse(["--world", "Casa", "--player", "Ana"]));

        Assert.True(first.CreateNew);
        Assert.False(second.CreateNew);
        Assert.Equal("Ana", second.Player.Name);
    }
}
```

Run: `dotnet test tests/Zyon.Client.Tests` → FAIL na compilação.

- [ ] **Step 2: Implementar**

`src/Zyon.Client/LaunchOptions.cs` — o record ganha parâmetros com padrão e a propriedade `SkipMenu`, e o `switch` ganha os casos novos:

```csharp
public sealed record LaunchOptions(
    int Width, int Height, int? Seed, bool SmokeTest, int SmokeFrames,
    bool Dev = false, string? World = null, string? Player = null, string? SavesDir = null, double AutosaveSeconds = 300)
{
    /// <summary>Pula o menu e entra direto no jogo (desenvolvimento e smoke test).</summary>
    public bool SkipMenu => SmokeTest || Dev || World is not null || Player is not null;
```

No `Parse`, declarar `bool dev = false; string? world = null, player = null, savesDir = null; double autosave = 300;`, adicionar ao `switch`:

```csharp
                case "--dev":
                    dev = true;
                    break;
                case "--world":
                    world = Next();
                    break;
                case "--player":
                    player = Next();
                    break;
                case "--saves-dir":
                    savesDir = Next();
                    break;
                case "--autosave-seconds":
                    autosave = double.TryParse(Next(), NumberStyles.Float, CultureInfo.InvariantCulture, out double a) && a > 0
                        ? a : throw new ArgumentException("--autosave-seconds precisa de um número > 0");
                    break;
```

e retornar `new LaunchOptions(width, height, seed, smoke, frames, dev, world, player, savesDir, autosave)`.

`src/Zyon.Client/LaunchRequest.cs`:

```csharp
using Zyon.Core.Persistence;

namespace Zyon.Client;

/// <summary>O que a tela de carregamento deve fazer: criar um mundo novo ou carregar um existente, com este personagem.</summary>
public sealed record LaunchRequest(
    PlayerSave Player,
    IReadOnlyList<string> Warnings,
    string WorldName,
    bool CreateNew,
    int Width,
    int Height,
    int Seed);
```

`src/Zyon.Client/SaveLibrary.cs`:

```csharp
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
```

`src/Zyon.Client/DevLaunch.cs`:

```csharp
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
        if (throwaway && library.WorldExists(worldName))
            SaveFiles.Delete(library.WorldPath(worldName));
        bool create = !library.WorldExists(worldName);
        return new LaunchRequest(player, warnings, worldName, create, options.Width, options.Height,
            options.Seed ?? Environment.TickCount);
    }
}
```

(`ErrorMessages.BackupWarning` é criado na Task 3; crie já o arquivo com esse método para compilar — o restante de `ErrorMessages` vem na Task 3:)

```csharp
namespace Zyon.Client;

/// <summary>Textos mostrados ao jogador para erros e avisos de save.</summary>
public static class ErrorMessages
{
    public static string BackupWarning(string what, string name, string? quarantinedPath) =>
        $"O {what} '{name}' estava danificado; foi carregado o último backup."
        + (quarantinedPath is null ? "" : $" O arquivo danificado foi guardado em '{quarantinedPath}'.");
}
```

- [ ] **Step 3: Rodar e confirmar**

Run: `dotnet test` → tudo verde.

- [ ] **Step 4: Commit**

```bash
git add src/Zyon.Client tests/Zyon.Client.Tests
git commit -m "feat(client): biblioteca de saves, pedido de jogo e atalho de desenvolvimento" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Carregar e criar jogos, mensagens de erro e progresso (Client)

**Files:**
- Create: `src/Zyon.Client/GameLoader.cs`, `src/Zyon.Client/LatestProgress.cs`
- Modify: `src/Zyon.Client/ErrorMessages.cs`
- Test: `tests/Zyon.Client.Tests/GameLoaderTests.cs`, `tests/Zyon.Client.Tests/ErrorMessagesTests.cs`

- [ ] **Step 1: Testes que falham**

`tests/Zyon.Client.Tests/GameLoaderTests.cs`:

```csharp
using Zyon.Core.Persistence;
using Zyon.Core.Tiles;
using Zyon.Core.WorldGen;

namespace Zyon.Client.Tests;

public sealed class GameLoaderTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "zyon-loader-" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    private static WorldGenConfig Config() => WorldGenConfigLoader.LoadFromDirectory(TestData.DataDir);

    [Fact]
    public void CreateWorld_GeneratesWritesAndStartsTheGame()
    {
        var lib = new SaveLibrary(root);
        var r = TestData.Registry();
        var player = lib.CreatePlayer("Ana", r);
        var progress = new LatestProgress<Zyon.Core.Sim.WorldGenProgress>();
        var req = new LaunchRequest(player, [], "Terra", true, 256, 128, 5);

        var game = GameLoader.Run(lib, r, Config(), req, progress, CancellationToken.None);
        using var _ = game.Simulation;

        Assert.True(File.Exists(lib.WorldPath("Terra")));
        Assert.Equal(1f, progress.Latest!.Fraction);
        Assert.True(game.Simulation.TryGetEntityView(game.PlayerId, out var me));
        Assert.Equal(game.Info.SpawnFeet.Y - r.Physics.PlayerHeight / 2, me.Position.Y);
        Assert.Equal("Ana", game.PlayerName);
        Assert.Empty(game.Warnings);
    }

    [Fact]
    public void LoadWorld_RestoresTheSavedWorld()
    {
        var lib = new SaveLibrary(root);
        var r = TestData.Registry();
        var player = lib.CreatePlayer("Ana", r);
        var created = GameLoader.Run(lib, r, Config(), new LaunchRequest(player, [], "Terra", true, 256, 128, 5), null, CancellationToken.None);
        ulong hash = TileWorldHash.Compute(created.Simulation.Tiles);
        created.Simulation.Dispose();

        var loaded = GameLoader.Run(lib, r, Config(), new LaunchRequest(player, ["aviso do personagem"], "Terra", false, 0, 0, 0), null, CancellationToken.None);
        using var _ = loaded.Simulation;

        Assert.Equal(hash, TileWorldHash.Compute(loaded.Simulation.Tiles));
        Assert.Equal(new[] { "aviso do personagem" }, loaded.Warnings);
    }

    [Fact]
    public void CreateWorld_WithExistingName_Throws()
    {
        var lib = new SaveLibrary(root);
        var r = TestData.Registry();
        var player = lib.CreatePlayer("Ana", r);
        GameLoader.Run(lib, r, Config(), new LaunchRequest(player, [], "Terra", true, 128, 128, 5), null, CancellationToken.None).Simulation.Dispose();

        var ex = Assert.Throws<Zyon.Core.Data.DataException>(() =>
            GameLoader.Run(lib, r, Config(), new LaunchRequest(player, [], "terra", true, 128, 128, 5), null, CancellationToken.None));
        Assert.Contains("já existe", ex.Message);
    }

    [Fact]
    public void CreateWorld_Cancelled_ThrowsAndWritesNothing()
    {
        var lib = new SaveLibrary(root);
        var r = TestData.Registry();
        var player = lib.CreatePlayer("Ana", r);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() =>
            GameLoader.Run(lib, r, Config(), new LaunchRequest(player, [], "Terra", true, 128, 128, 5), null, cts.Token));
        Assert.False(SaveFiles.Exists(lib.WorldPath("Terra")));
    }

    [Fact]
    public void LoadWorld_FromBackup_AddsWarning()
    {
        var lib = new SaveLibrary(root);
        var r = TestData.Registry();
        var player = lib.CreatePlayer("Ana", r);
        GameLoader.Run(lib, r, Config(), new LaunchRequest(player, [], "Terra", true, 128, 128, 5), null, CancellationToken.None).Simulation.Dispose();
        string path = lib.WorldPath("Terra");
        File.Copy(path, path + SaveFiles.BackupSuffix);
        File.WriteAllText(path, "lixo");

        var game = GameLoader.Run(lib, r, Config(), new LaunchRequest(player, [], "Terra", false, 0, 0, 0), null, CancellationToken.None);
        using var _ = game.Simulation;

        var warning = Assert.Single(game.Warnings);
        Assert.Contains("backup", warning);
    }
}
```

`tests/Zyon.Client.Tests/ErrorMessagesTests.cs`:

```csharp
using Zyon.Core.Data;
using Zyon.Core.Persistence;

namespace Zyon.Client.Tests;

public class ErrorMessagesTests
{
    [Fact]
    public void Incompatible_ExplainsNewerVersionOrRemovedContent()
    {
        var text = ErrorMessages.Describe(new SaveIncompatibleException(".zworld: bloco 'x' não existe mais"));

        Assert.Contains("versão mais nova", text);
        Assert.Contains("bloco 'x'", text);
    }

    [Theory]
    [InlineData(SaveLoadFailure.Inaccessible, "em uso")]
    [InlineData(SaveLoadFailure.Corrupt, "corrompido")]
    [InlineData(SaveLoadFailure.Missing, "não foi encontrado")]
    public void LoadFailure_DependsOnKind(SaveLoadFailure kind, string expected)
    {
        var ex = new SaveLoadException("C:/x.zworld", kind, new IOException("detalhe"), null);

        Assert.Contains(expected, ErrorMessages.Describe(ex));
    }

    [Fact]
    public void DataAndCancel_AndUnexpected()
    {
        Assert.Contains("nome inválido", ErrorMessages.Describe(new DataException(".zworld: nome inválido")));
        Assert.Equal("Cancelado.", ErrorMessages.Describe(new OperationCanceledException()));
        Assert.Contains("inesperado", ErrorMessages.Describe(new InvalidOperationException("bug")));
    }

    [Fact]
    public void Describe_UnwrapsAggregateExceptions()
    {
        var inner = new SaveIncompatibleException(".zworld: formato 9");

        Assert.Contains("formato 9", ErrorMessages.Describe(new AggregateException(inner)));
    }
}
```

(Se o construtor de `SaveLoadException` tiver outra ordem de parâmetros, ajuste o teste para a assinatura real — ela foi definida no plano 3 como `(filePath, kind, primaryError, backupError)`.)

Run: `dotnet test tests/Zyon.Client.Tests` → FAIL na compilação.

- [ ] **Step 2: Implementar**

`src/Zyon.Client/LatestProgress.cs`:

```csharp
namespace Zyon.Client;

/// <summary>
/// IProgress que só guarda o último valor (seguro entre threads). A UI lê Latest no seu próprio frame,
/// em vez de receber callbacks de outra thread.
/// </summary>
public sealed class LatestProgress<T> : IProgress<T> where T : class
{
    private T? latest;

    public T? Latest => Volatile.Read(ref latest);

    public void Report(T value) => Volatile.Write(ref latest, value);
}
```

`src/Zyon.Client/ErrorMessages.cs` — acrescentar `Describe` ao arquivo da Task 2:

```csharp
using Zyon.Core.Data;
using Zyon.Core.Persistence;

namespace Zyon.Client;

/// <summary>Textos mostrados ao jogador para erros e avisos de save.</summary>
public static class ErrorMessages
{
    public static string BackupWarning(string what, string name, string? quarantinedPath) =>
        $"O {what} '{name}' estava danificado; foi carregado o último backup."
        + (quarantinedPath is null ? "" : $" O arquivo danificado foi guardado em '{quarantinedPath}'.");

    public static string Describe(Exception error)
    {
        if (error is AggregateException { InnerExceptions.Count: 1 } aggregate)
            error = aggregate.InnerExceptions[0];
        return error switch
        {
            SaveIncompatibleException e =>
                $"Este save é de uma versão mais nova do jogo ou usa conteúdo que foi removido. ({e.Message})",
            SaveLoadException { Kind: SaveLoadFailure.Inaccessible } =>
                "O arquivo está em uso ou sem permissão. Feche outros programas que possam estar com ele aberto e tente de novo.",
            SaveLoadException { Kind: SaveLoadFailure.Missing } =>
                "O save não foi encontrado.",
            SaveLoadException e =>
                $"O save está corrompido e não havia backup utilizável. ({e.Message})",
            DataException e => e.Message,
            OperationCanceledException => "Cancelado.",
            _ => $"Erro inesperado: {error.GetType().Name}: {error.Message}",
        };
    }
}
```

`src/Zyon.Client/GameLoader.cs`:

```csharp
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
```

- [ ] **Step 3: Rodar e confirmar**

Run: `dotnet test` → tudo verde.

- [ ] **Step 4: Commit**

```bash
git add src/Zyon.Client tests/Zyon.Client.Tests
git commit -m "feat(client): criar/carregar jogos, mensagens de erro e progresso entre threads" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Salvar em segundo plano e autosave (Client)

**Files:**
- Create: `src/Zyon.Client/GameSaver.cs`, `src/Zyon.Client/AutosaveTimer.cs`
- Test: `tests/Zyon.Client.Tests/GameSaverTests.cs`, `tests/Zyon.Client.Tests/AutosaveTimerTests.cs`

- [ ] **Step 1: Testes que falham**

`tests/Zyon.Client.Tests/AutosaveTimerTests.cs`:

```csharp
namespace Zyon.Client.Tests;

public class AutosaveTimerTests
{
    [Fact]
    public void FiresOncePerInterval_AndResets()
    {
        var t = new AutosaveTimer(1.0);

        Assert.False(t.Update(0.6));
        Assert.True(t.Update(0.5));
        Assert.False(t.Update(0.5));
        Assert.True(t.Update(0.6));
    }

    [Fact]
    public void IgnoresInvalidDeltas_AndResetRestarts()
    {
        var t = new AutosaveTimer(1.0);

        Assert.False(t.Update(double.NaN));
        Assert.False(t.Update(-5));
        t.Update(0.9);
        t.Reset();
        Assert.False(t.Update(0.5));
    }
}
```

`tests/Zyon.Client.Tests/GameSaverTests.cs`:

```csharp
using System.Numerics;
using Zyon.Core.Persistence;
using Zyon.Core.Sim;
using Zyon.Core.Tiles;
using Zyon.Core.WorldGen;

namespace Zyon.Client.Tests;

public sealed class GameSaverTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "zyon-saver-" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    private (SaveLibrary Lib, LoadedGame Game) NewGame()
    {
        var lib = new SaveLibrary(root);
        var r = TestData.Registry();
        var player = lib.CreatePlayer("Ana", r);
        var game = GameLoader.Run(lib, r, WorldGenConfigLoader.LoadFromDirectory(TestData.DataDir),
            new LaunchRequest(player, [], "Terra", true, 256, 128, 9), null, CancellationToken.None);
        return (lib, game);
    }

    [Fact]
    public void Save_WritesWorldAndPlayer_ThatLoadBackIdentical()
    {
        var (lib, game) = NewGame();
        var r = game.Simulation.Registry;
        using var saver = new GameSaver(r, game);
        using var sim = game.Simulation;
        var (x, y) = ((int)game.Info.SpawnFeet.X + 1, (int)game.Info.SpawnFeet.Y);
        sim.SetInput(game.PlayerId, new PlayerInput(0f, false, true, x, y));
        for (int i = 0; i < 90; i++) sim.Step();

        saver.Save(sim);
        Assert.True(saver.Flush(TimeSpan.FromSeconds(10)));

        Assert.Null(saver.LastError);
        Assert.Equal(1, saver.CompletedSaves);
        var world = SaveFiles.ReadWithBackup(game.WorldPath, s => WorldFile.Read(s, r)).Value;
        Assert.Equal(TileWorldHash.Compute(sim.Tiles), TileWorldHash.Compute(world.Tiles));
        Assert.Equal(sim.Tick, world.Tick);
        Assert.True(world.PlayerPositions.ContainsKey("Ana"));
        var player = lib.LoadPlayer("Ana", r).Value;
        Assert.Equal(PlayerSave.Capture(sim, game.PlayerId, "Ana").Slots, player.Slots);
    }

    [Fact]
    public void ConsecutiveSaves_AreWrittenInOrder_AndKeepABackup()
    {
        var (_, game) = NewGame();
        using var saver = new GameSaver(game.Simulation.Registry, game);
        using var sim = game.Simulation;

        saver.Save(sim);
        for (int i = 0; i < 10; i++) sim.Step();
        saver.Save(sim);
        Assert.True(saver.Flush(TimeSpan.FromSeconds(10)));

        var latest = SaveFiles.ReadWithBackup(game.WorldPath, s => WorldFile.Read(s, sim.Registry)).Value;
        Assert.Equal(10, latest.Tick);
        Assert.True(File.Exists(game.WorldPath + SaveFiles.BackupSuffix));
        Assert.Equal(2, saver.CompletedSaves);
    }

    [Fact]
    public void KeepsPositionsOfCharactersNotInThisSession()
    {
        var (_, game) = NewGame();
        var known = new Dictionary<string, Vector2> { ["Bia"] = new Vector2(3f, 4f) };
        game = game with { KnownPositions = known };
        using var saver = new GameSaver(game.Simulation.Registry, game);
        using var sim = game.Simulation;

        saver.Save(sim);
        saver.Flush(TimeSpan.FromSeconds(10));

        var world = SaveFiles.ReadWithBackup(game.WorldPath, s => WorldFile.Read(s, sim.Registry)).Value;
        Assert.Equal(new Vector2(3f, 4f), world.PlayerPositions["Bia"]);
        Assert.True(world.PlayerPositions.ContainsKey("Ana"));
    }

    [Fact]
    public void WriteFailure_IsReported_NotThrown()
    {
        var (_, game) = NewGame();
        using var saver = new GameSaver(game.Simulation.Registry, game);
        using var sim = game.Simulation;
        File.SetAttributes(game.WorldPath, FileAttributes.ReadOnly);
        try
        {
            saver.Save(sim);
            saver.Flush(TimeSpan.FromSeconds(10));

            Assert.NotNull(saver.LastError);
            Assert.Equal(0, saver.CompletedSaves);
        }
        finally
        {
            File.SetAttributes(game.WorldPath, FileAttributes.Normal);
        }
    }
}
```

Run: `dotnet test tests/Zyon.Client.Tests` → FAIL na compilação.

- [ ] **Step 2: Implementar**

`src/Zyon.Client/AutosaveTimer.cs`:

```csharp
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
```

`src/Zyon.Client/GameSaver.cs`:

```csharp
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
```

- [ ] **Step 3: Rodar e confirmar**

Run: `dotnet test` → tudo verde.

- [ ] **Step 4: Commit**

```bash
git add src/Zyon.Client tests/Zyon.Client.Tests
git commit -m "feat(client): salvamento em fila única em segundo plano e autosave" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Menu inicial e tela de carregamento (Godot)

**Files:**
- Create: `src/Zyon.Game/GameFlow.cs`, `src/Zyon.Game/Scenes.cs`, `src/Zyon.Game/Menus/MainMenu.cs`, `src/Zyon.Game/Menus/LoadingScreen.cs`, `src/Zyon.Game/scenes/MainMenu.tscn`, `src/Zyon.Game/scenes/Loading.tscn`
- Modify: `src/Zyon.Game/project.godot` (cena principal), `src/Zyon.Game/GameRoot.cs`, `tools/smoke-test.sh`, `tools/play.sh`, `tools/play.ps1`

- [ ] **Step 1: Estado compartilhado entre cenas**

`src/Zyon.Game/Scenes.cs`:

```csharp
namespace Zyon.Game;

public static class Scenes
{
    public const string MainMenu = "res://scenes/MainMenu.tscn";
    public const string Loading = "res://scenes/Loading.tscn";
    public const string Game = "res://scenes/Game.tscn";
}
```

`src/Zyon.Game/GameFlow.cs`:

```csharp
using Godot;
using Zyon.Client;
using Zyon.Core.Data;
using Zyon.Core.WorldGen;

namespace Zyon.Game;

/// <summary>Dados carregados uma vez e o que passa de uma cena para a próxima (menu → carregamento → jogo).</summary>
public static class GameFlow
{
    private static bool initialized;

    public static string DataDir { get; private set; } = "";
    public static Registry Registry { get; private set; } = null!;
    public static WorldGenConfig WorldGenConfig { get; private set; } = null!;
    public static ClientVisuals Visuals { get; private set; } = null!;
    public static LaunchOptions Options { get; private set; } = null!;
    public static SaveLibrary Library { get; private set; } = null!;

    /// <summary>Pedido do menu para a tela de carregamento.</summary>
    public static LaunchRequest? PendingRequest { get; set; }

    /// <summary>Jogo pronto, da tela de carregamento para a cena de jogo.</summary>
    public static LoadedGame? PendingGame { get; set; }

    /// <summary>Carrega dados e opções na primeira chamada. Lança DataException/ArgumentException se algo estiver errado.</summary>
    public static void EnsureInitialized()
    {
        if (initialized) return;
        Options = LaunchOptions.Parse(OS.GetCmdlineUserArgs());
        DataDir = DataPaths.Resolve();
        Registry = RegistryLoader.LoadFromDirectory(DataDir);
        WorldGenConfig = WorldGenConfigLoader.LoadFromDirectory(DataDir);
        Visuals = ClientVisuals.LoadFromDirectory(DataDir, Registry);
        Library = new SaveLibrary(Options.SavesDir ?? ProjectSettings.GlobalizePath("user://saves"));
        initialized = true;
    }

    /// <summary>Troca de cena no fim do frame (seguro de chamar em _Ready/_Process).</summary>
    public static void GoTo(Node from, string scene) =>
        Callable.From(() => from.GetTree().ChangeSceneToFile(scene)).CallDeferred();
}
```

- [ ] **Step 2: Menu inicial**

`src/Zyon.Game/scenes/MainMenu.tscn`:

```ini
[gd_scene load_steps=2 format=3]

[ext_resource type="Script" path="res://Menus/MainMenu.cs" id="1"]

[node name="MainMenu" type="Control"]
layout_mode = 3
anchors_preset = 15
anchor_right = 1.0
anchor_bottom = 1.0
script = ExtResource("1")
```

`src/Zyon.Game/scenes/Loading.tscn`:

```ini
[gd_scene load_steps=2 format=3]

[ext_resource type="Script" path="res://Menus/LoadingScreen.cs" id="1"]

[node name="Loading" type="Control"]
layout_mode = 3
anchors_preset = 15
anchor_right = 1.0
anchor_bottom = 1.0
script = ExtResource("1")
```

Em `project.godot`, trocar `run/main_scene="res://scenes/Game.tscn"` por `run/main_scene="res://scenes/MainMenu.tscn"`.

`src/Zyon.Game/Menus/MainMenu.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;
using Zyon.Client;
using Zyon.Core.Data;
using Zyon.Core.Persistence;
using Zyon.Core.WorldGen;

namespace Zyon.Game.Menus;

/// <summary>Escolher/criar personagem e escolher/criar mundo. Com --dev, --world, --player ou --smoke-test, pula direto para o jogo.</summary>
public partial class MainMenu : Control
{
    private static readonly (string Label, int Width, int Height)[] Sizes =
    [
        ("Pequeno (4200×1200)", 4200, 1200),
        ("Médio (6400×1800)", 6400, 1800),
        ("Grande (8400×2400)", 8400, 2400),
        ("Teste (1050×300)", 1050, 300),
    ];

    private ItemList players = null!;
    private ItemList worlds = null!;
    private LineEdit playerName = null!;
    private LineEdit worldName = null!;
    private LineEdit seed = null!;
    private OptionButton size = null!;
    private Label status = null!;

    public override void _Ready()
    {
        try
        {
            GameFlow.EnsureInitialized();
            if (GameFlow.Options.SkipMenu)
            {
                GameFlow.PendingRequest = DevLaunch.Build(GameFlow.Library, GameFlow.Registry, GameFlow.Options);
                GameFlow.GoTo(this, Scenes.Loading);
                return;
            }
        }
        catch (Exception ex) when (ex is DataException or ArgumentException or SaveException or System.IO.IOException)
        {
            GD.PrintErr($"Falha ao iniciar: {ex.Message}");
            if (OS.GetCmdlineUserArgs().AsSpan().Contains("--smoke-test"))
            {
                GD.PrintErr($"SMOKE FAIL: {ex.Message}");
                GetTree().Quit(1);
                return;
            }
            BuildUi();
            status.Text = ErrorMessages.Describe(ex);
            return;
        }

        BuildUi();
        Refresh();
    }

    private void BuildUi()
    {
        Theme = new Theme { DefaultFontSize = 10 };
        var margin = new MarginContainer();
        margin.SetAnchorsPreset(LayoutPreset.FullRect);
        foreach (var side in new[] { "left", "right", "top", "bottom" })
            margin.AddThemeConstantOverride($"margin_{side}", 12);
        AddChild(margin);

        var column = new VBoxContainer();
        margin.AddChild(column);
        column.AddChild(new Label { Text = "ZYON", HorizontalAlignment = HorizontalAlignment.Center });

        var row = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 16);
        column.AddChild(row);

        var left = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddChild(left);
        left.AddChild(new Label { Text = "Personagens" });
        players = new ItemList { SizeFlagsVertical = SizeFlags.ExpandFill };
        left.AddChild(players);
        playerName = new LineEdit { PlaceholderText = "Nome do novo personagem", MaxLength = SaveNames.MaxLength };
        left.AddChild(playerName);
        var createPlayer = new Button { Text = "Criar personagem" };
        createPlayer.Pressed += OnCreatePlayer;
        left.AddChild(createPlayer);

        var right = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddChild(right);
        right.AddChild(new Label { Text = "Mundos" });
        worlds = new ItemList { SizeFlagsVertical = SizeFlags.ExpandFill };
        right.AddChild(worlds);
        var play = new Button { Text = "Jogar mundo selecionado" };
        play.Pressed += OnPlaySelected;
        right.AddChild(play);
        worldName = new LineEdit { PlaceholderText = "Nome do novo mundo", MaxLength = SaveNames.MaxLength };
        right.AddChild(worldName);
        size = new OptionButton();
        foreach (var s in Sizes) size.AddItem(s.Label);
        right.AddChild(size);
        seed = new LineEdit { PlaceholderText = "Seed (opcional)" };
        right.AddChild(seed);
        var create = new Button { Text = "Criar mundo e jogar" };
        create.Pressed += OnCreateWorld;
        right.AddChild(create);

        status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        column.AddChild(status);
    }

    private void Refresh()
    {
        players.Clear();
        foreach (var name in GameFlow.Library.Players()) players.AddItem(name);
        if (players.ItemCount > 0) players.Select(0);
        worlds.Clear();
        foreach (var name in GameFlow.Library.Worlds()) worlds.AddItem(name);
        if (worlds.ItemCount > 0) worlds.Select(0);
    }

    private void OnCreatePlayer()
    {
        try
        {
            var created = GameFlow.Library.CreatePlayer(playerName.Text, GameFlow.Registry);
            playerName.Text = "";
            Refresh();
            for (int i = 0; i < players.ItemCount; i++)
                if (players.GetItemText(i) == created.Name) players.Select(i);
            status.Text = $"Personagem '{created.Name}' criado.";
        }
        catch (Exception ex) when (ex is DataException or SaveException or System.IO.IOException)
        {
            status.Text = ErrorMessages.Describe(ex);
        }
    }

    private void OnCreateWorld()
    {
        if (!TrySelectedPlayer(out var player, out var warnings)) return;
        var (_, width, height) = Sizes[Math.Max(0, size.Selected)];
        int seedValue = int.TryParse(seed.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int s)
            ? s
            : (seed.Text.Length > 0 ? StableSeed(seed.Text) : System.Environment.TickCount);
        try
        {
            GameFlow.Library.WorldPath(worldName.Text);   // valida o nome antes de gerar
        }
        catch (DataException ex)
        {
            status.Text = ex.Message;
            return;
        }
        Start(new LaunchRequest(player, warnings, worldName.Text, true, width, height, seedValue));
    }

    private void OnPlaySelected()
    {
        var selected = worlds.GetSelectedItems();
        if (selected.Length == 0)
        {
            status.Text = "Escolha um mundo da lista (ou crie um novo).";
            return;
        }
        if (!TrySelectedPlayer(out var player, out var warnings)) return;
        Start(new LaunchRequest(player, warnings, worlds.GetItemText(selected[0]), false, 0, 0, 0));
    }

    private bool TrySelectedPlayer(out PlayerSave player, out IReadOnlyList<string> warnings)
    {
        player = null!;
        warnings = [];
        var selected = players.GetSelectedItems();
        if (selected.Length == 0)
        {
            status.Text = "Escolha um personagem (ou crie um novo).";
            return false;
        }
        string name = players.GetItemText(selected[0]);
        try
        {
            var loaded = GameFlow.Library.LoadPlayer(name, GameFlow.Registry);
            player = loaded.Value;
            warnings = loaded.FromBackup ? [ErrorMessages.BackupWarning("personagem", name, loaded.QuarantinedPath)] : [];
            return true;
        }
        catch (Exception ex) when (ex is SaveException or DataException)
        {
            status.Text = ErrorMessages.Describe(ex);
            return false;
        }
    }

    private void Start(LaunchRequest request)
    {
        GameFlow.PendingRequest = request;
        GameFlow.GoTo(this, Scenes.Loading);
    }

    /// <summary>Seed estável para texto ("terra" gera sempre o mesmo mundo): FNV-1a 32 dos bytes UTF-8.</summary>
    private static int StableSeed(string text)
    {
        uint h = 2166136261;
        foreach (byte b in System.Text.Encoding.UTF8.GetBytes(text))
            h = (h ^ b) * 16777619;
        return unchecked((int)h);
    }
}
```


- [ ] **Step 3: Tela de carregamento**

`src/Zyon.Game/Menus/LoadingScreen.cs`:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using Zyon.Client;
using Zyon.Core.Sim;

namespace Zyon.Game.Menus;

/// <summary>Gera ou carrega o mundo em segundo plano, mostra o progresso e permite cancelar.</summary>
public partial class LoadingScreen : Control
{
    private static readonly System.Collections.Generic.Dictionary<string, string> PassNames = new()
    {
        ["terrain"] = "terreno",
        ["layers"] = "camadas",
        ["caves"] = "cavernas",
        ["ores"] = "minérios",
        ["walls"] = "paredes",
        ["spawn"] = "ponto de partida",
    };

    private readonly CancellationTokenSource cancel = new();
    private readonly LatestProgress<WorldGenProgress> progress = new();
    private Task<LoadedGame>? work;
    private ProgressBar bar = null!;
    private Label label = null!;
    private Button button = null!;
    private bool failed;

    public override void _Ready()
    {
        Theme = new Theme { DefaultFontSize = 10 };
        var center = new CenterContainer();
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(center);
        var box = new VBoxContainer { CustomMinimumSize = new Vector2(320, 0) };
        center.AddChild(box);
        label = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, HorizontalAlignment = HorizontalAlignment.Center };
        box.AddChild(label);
        bar = new ProgressBar { MinValue = 0, MaxValue = 1, Step = 0.001 };
        box.AddChild(bar);
        button = new Button { Text = "Cancelar" };
        button.Pressed += OnButton;
        box.AddChild(button);

        var request = GameFlow.PendingRequest;
        GameFlow.PendingRequest = null;
        if (request is null)
        {
            GameFlow.GoTo(this, Scenes.MainMenu);
            return;
        }
        label.Text = request.CreateNew ? $"Criando o mundo '{request.WorldName}'..." : $"Carregando '{request.WorldName}'...";
        var token = cancel.Token;
        work = Task.Run(() => GameLoader.Run(GameFlow.Library, GameFlow.Registry, GameFlow.WorldGenConfig,
            request, progress, token, new GodotLog()), token);
    }

    public override void _Process(double delta)
    {
        if (work is null || failed) return;
        if (progress.Latest is { } p)
        {
            bar.Value = p.Fraction;
            label.Text = $"Gerando: {(PassNames.TryGetValue(p.Pass, out var name) ? name : p.Pass)}";
        }
        if (!work.IsCompleted) return;

        if (work.IsCompletedSuccessfully)
        {
            GameFlow.PendingGame = work.Result;
            GameFlow.GoTo(this, Scenes.Game);
            work = null;
            return;
        }

        Exception error = work.Exception?.GetBaseException() ?? new OperationCanceledException();
        if (error is OperationCanceledException)
        {
            GameFlow.GoTo(this, Scenes.MainMenu);
            work = null;
            return;
        }
        if (GameFlow.Options.SmokeTest)
        {
            GD.PrintErr($"SMOKE FAIL: {error}");
            GetTree().Quit(1);
            return;
        }
        GD.PrintErr(error.ToString());
        failed = true;
        label.Text = ErrorMessages.Describe(error);
        button.Text = "Voltar ao menu";
    }

    private void OnButton()
    {
        if (failed)
        {
            GameFlow.GoTo(this, Scenes.MainMenu);
            return;
        }
        cancel.Cancel();
        button.Disabled = true;
        label.Text = "Cancelando...";
    }

    public override void _ExitTree() => cancel.Dispose();
}
```

- [ ] **Step 4: `GameRoot` usa o jogo carregado**

Em `src/Zyon.Game/GameRoot.cs`, no início de `_Ready`: chamar `GameFlow.EnsureInitialized()`, pegar `var game = GameFlow.PendingGame; GameFlow.PendingGame = null;` e, se for `null`, `GameFlow.GoTo(this, Scenes.MainMenu); return;`. Trocar a criação local de mundo/simulação/jogador (geração com `LaunchOptions`, `WorldSave.FromGenerated`, `PlayerSave.New("Dev")`) por `var sim = game.Simulation; int player = game.PlayerId;`, usando `GameFlow.Registry`/`GameFlow.Visuals`/`GameFlow.Options` no lugar das variáveis locais, e o tamanho do mundo para a câmera vindo de `sim.Width`/`sim.Height`. Guardar `game` num campo `private LoadedGame game = null!;` (usado na Task 6). Toda a montagem de nós e a lógica de `_Process` continuam iguais.

- [ ] **Step 5: Scripts**

`tools/smoke-test.sh`: antes de rodar o Godot, `SAVES="$(mktemp -d)"` e passar `-- --smoke-test --frames "$FRAMES" --size dev --seed 1 --saves-dir "$SAVES"`; ao final, `rm -rf "$SAVES"`.

`tools/play.sh` e `tools/play.ps1`: sem argumentos, **abrir o menu** (não passar mais `--size dev` por padrão); com argumentos, repassá-los. Atualizar o comentário do topo: `tools/play.sh --dev` entra direto num mundo de teste descartável.

- [ ] **Step 6: Verificar**

Run: `dotnet build src/Zyon.Game/Zyon.Game.csproj` → 0/0; `tools/smoke-test.sh 600` → `SMOKE OK` (o fluxo agora passa por menu pulado → carregamento → jogo); `dotnet test` → verde.

- [ ] **Step 7: Commit**

```bash
git add src/Zyon.Game tools
git commit -m "feat(game): menu inicial e tela de carregamento com progresso" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: Autosave, menu de pausa, salvar ao sair e avisos (Godot)

**Files:**
- Create: `src/Zyon.Game/Menus/PauseMenu.cs`, `src/Zyon.Game/Hud/Toast.cs`
- Modify: `src/Zyon.Game/GameRoot.cs`, `src/Zyon.Game/PlayerController.cs`, `src/Zyon.Game/SmokeTest.cs`

- [ ] **Step 1: Aviso rápido na tela**

`src/Zyon.Game/Hud/Toast.cs`:

```csharp
using Godot;

namespace Zyon.Game.Hud;

/// <summary>Mensagem curta no topo da tela que some sozinha.</summary>
public partial class Toast : CanvasLayer
{
    private Label label = null!;
    private double remaining;

    public void Init()
    {
        label = new Label
        {
            Position = new Vector2(8, 6),
            Size = new Vector2(624, 40),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        label.AddThemeFontSizeOverride("font_size", 9);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        label.AddThemeConstantOverride("outline_size", 3);
        AddChild(label);
    }

    public void Show(string text, double seconds = 4)
    {
        label.Text = text;
        label.Visible = true;
        remaining = seconds;
    }

    public override void _Process(double delta)
    {
        if (remaining <= 0) return;
        remaining -= delta;
        if (remaining <= 0) label.Visible = false;
    }
}
```

- [ ] **Step 2: Menu de pausa**

`src/Zyon.Game/Menus/PauseMenu.cs`:

```csharp
using System;
using Godot;

namespace Zyon.Game.Menus;

/// <summary>Esc: continuar, salvar e voltar ao menu, ou salvar e sair. O jogo continua simulando por trás (não há pausa real no modo local da fatia).</summary>
public partial class PauseMenu : CanvasLayer
{
    private Control panel = null!;

    public bool IsOpen => panel.Visible;

    public void Init(Action resume, Action saveAndMenu, Action saveAndQuit)
    {
        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.5f) };
        dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        dim.AddChild(center);
        var box = new VBoxContainer();
        center.AddChild(box);
        box.AddChild(new Label { Text = "Pausa", HorizontalAlignment = HorizontalAlignment.Center });
        box.AddChild(MakeButton("Continuar", () => { Close(); resume(); }));
        box.AddChild(MakeButton("Salvar e voltar ao menu", saveAndMenu));
        box.AddChild(MakeButton("Salvar e sair", saveAndQuit));
        panel = dim;
        panel.Theme = new Theme { DefaultFontSize = 10 };
        panel.Visible = false;
        AddChild(panel);
    }

    public void Open() => panel.Visible = true;

    public void Close() => panel.Visible = false;

    private static Button MakeButton(string text, Action onPressed)
    {
        var button = new Button { Text = text };
        button.Pressed += onPressed;
        return button;
    }
}
```

- [ ] **Step 3: `PlayerController` pode ser desligado**

Em `PlayerController.cs`, adicionar `public bool Enabled { get; set; } = true;`. Em `Poll`, logo no início (antes do `ScriptedInput`): `if (!Enabled) { session.SetInput(new PlayerInput(0f, false, false, 0, 0)); return; }`. Em `_UnhandledInput`, retornar cedo se `!Enabled`.

- [ ] **Step 4: `GameRoot` — salvar, pausar e sair**

Em `GameRoot.cs`:

- Campos: `private GameSaver saver = null!; private AutosaveTimer autosave = null!; private PauseMenu pause = null!; private Toast toast = null!; private Exception? lastReportedSaveError; private bool exiting;`.
- No fim da montagem em `_Ready` (antes de atribuir `session`): criar `saver = new GameSaver(GameFlow.Registry, game);`, `autosave = new AutosaveTimer(GameFlow.Options.AutosaveSeconds);`, `toast` (`new Toast()`, `AddChild`, `Init()`), `pause` (`new PauseMenu()`, `AddChild`, `Init(() => controller.Enabled = true, () => SaveAndExit(toMenu: true), () => SaveAndExit(toMenu: false))`), `GetTree().AutoAcceptQuit = false;`, e mostrar avisos: `if (game.Warnings.Count > 0) toast.Show(string.Join("\n", game.Warnings), 10);`.
- Em `_Process`, depois de `session.Update(delta)`:

```csharp
            if (autosave.Update(delta))
            {
                saver.Save(game.Simulation);
                toast.Show("Jogo salvo.", 2);
            }
            if (saver.LastError is { } saveError && saveError != lastReportedSaveError)
            {
                lastReportedSaveError = saveError;
                toast.Show($"Não foi possível salvar: {saveError.Message}", 8);
            }
```

- Teclas: em `_UnhandledInput(InputEvent e)` do `GameRoot`: `Esc` (`Key.Escape`) alterna o menu de pausa (abrir → `controller.Enabled = false; pause.Open()`; fechar → `pause.Close(); controller.Enabled = true`) e marca o evento como tratado (`GetViewport().SetInputAsHandled()`).
- Fechar a janela:

```csharp
    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest) SaveAndExit(toMenu: false);
    }

    private void SaveAndExit(bool toMenu)
    {
        if (exiting || session is null) return;
        exiting = true;
        saver.Save(game.Simulation);
        bool done = saver.Flush(TimeSpan.FromSeconds(10));
        if (!done || saver.LastError is not null)
            GD.PushError($"Falha ao salvar ao sair: {saver.LastError?.Message ?? "tempo esgotado"}");
        if (toMenu)
        {
            session.Dispose();
            session = null;
            GameFlow.GoTo(this, Scenes.MainMenu);
        }
        else
        {
            GetTree().Quit();
        }
    }
```

- Em `_ExitTree`: `saver?.Dispose(); session?.Dispose();` (o `Dispose` do saver espera a fila).

- [ ] **Step 5: Smoke test verifica o save**

Em `SmokeTest.cs`, quando o roteiro terminar com sucesso, o `GameRoot` (no bloco que hoje imprime `SMOKE OK`) faz, antes de imprimir:

```csharp
                        saver.Save(game.Simulation);
                        if (!saver.Flush(TimeSpan.FromSeconds(10)) || saver.LastError is not null)
                            throw new InvalidOperationException($"save falhou: {saver.LastError?.Message ?? "tempo esgotado"}");
                        var reloaded = Zyon.Core.Persistence.SaveFiles.ReadWithBackup(game.WorldPath,
                            s => Zyon.Core.Persistence.WorldFile.Read(s, GameFlow.Registry)).Value;
                        if (Zyon.Core.Tiles.TileWorldHash.Compute(reloaded.Tiles) != Zyon.Core.Tiles.TileWorldHash.Compute(game.Simulation.Tiles))
                            throw new InvalidOperationException("o mundo salvo difere do mundo em jogo");
```

e inclui `save ok` na linha `SMOKE OK`. (A exceção cai no `catch` de smoke existente → `SMOKE FAIL`.)

- [ ] **Step 6: Verificar**

Run: `dotnet build src/Zyon.Game/Zyon.Game.csproj` → 0/0; `tools/smoke-test.sh 600` → `SMOKE OK ... save ok`; `dotnet test` → verde.

- [ ] **Step 7: Commit**

```bash
git add src/Zyon.Game
git commit -m "feat(game): autosave, menu de pausa, salvar ao sair e avisos" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: Inventário com arrastar e soltar (Godot)

**Files:**
- Create: `src/Zyon.Game/Hud/InventorySlot.cs`, `src/Zyon.Game/Hud/InventoryPanel.cs`
- Modify: `src/Zyon.Game/GameRoot.cs`

- [ ] **Step 1: Slot arrastável**

`src/Zyon.Game/Hud/InventorySlot.cs`:

```csharp
using System;
using Godot;
using Zyon.Client;
using Zyon.Core.Items;

namespace Zyon.Game.Hud;

/// <summary>Um slot do inventário: arrasta a pilha para outro slot (MoveInventoryItem) ou para fora (DropItem).</summary>
public partial class InventorySlot : Panel
{
    public const int Size = 20;

    private ColorRect icon = null!;
    private Label count = null!;
    private Action<int, int> move = null!;
    private Action<int> dropOutside = null!;
    private Func<bool> mouseInsidePanel = null!;
    private bool dragging;
    private ItemStack shown = new(ushort.MaxValue, -1);

    public int Slot { get; private set; }

    public void Init(int slot, Action<int, int> onMove, Action<int> onDropOutside, Func<bool> isMouseInsidePanel)
    {
        Slot = slot;
        move = onMove;
        dropOutside = onDropOutside;
        mouseInsidePanel = isMouseInsidePanel;
        CustomMinimumSize = new Vector2(Size, Size);
        icon = new ColorRect { Position = new Vector2(5, 5), Size = new Vector2(Size - 10, Size - 10), MouseFilter = MouseFilterEnum.Ignore };
        count = new Label { Position = new Vector2(1, Size - 12), MouseFilter = MouseFilterEnum.Ignore };
        count.AddThemeFontSizeOverride("font_size", 8);
        AddChild(icon);
        AddChild(count);
    }

    public void Show(ItemStack stack, ClientVisuals visuals)
    {
        if (stack == shown) return;
        shown = stack;
        icon.Visible = !stack.IsEmpty;
        if (!stack.IsEmpty) icon.Color = visuals.ItemColor(stack.ItemId).ToGodot();
        count.Text = stack.Count > 1 ? stack.Count.ToString() : "";
    }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (shown.IsEmpty) return default;
        dragging = true;
        SetDragPreview(new ColorRect { Color = icon.Color, Size = new Vector2(12, 12), MouseFilter = MouseFilterEnum.Ignore });
        return Slot;
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data) => data.VariantType == Variant.Type.Int;

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        int from = data.AsInt32();
        if (from != Slot) move(from, Slot);
    }

    public override void _Notification(int what)
    {
        if (what != NotificationDragEnd || !dragging) return;
        dragging = false;
        if (!GetViewport().GuiIsDragSuccessful() && !mouseInsidePanel())
            dropOutside(Slot);
    }
}
```

- [ ] **Step 2: Painel**

`src/Zyon.Game/Hud/InventoryPanel.cs`:

```csharp
using Godot;
using Zyon.Client;
using Zyon.Core.Items;
using Zyon.Core.Sim;

namespace Zyon.Game.Hud;

/// <summary>Inventário completo (5 linhas × 10; a primeira linha é a hotbar). Abre e fecha com E; Esc também fecha.</summary>
public partial class InventoryPanel : CanvasLayer
{
    private readonly InventorySlot[] slots = new InventorySlot[Inventory.Slots];
    private PanelContainer panel = null!;
    private IGameSession session = null!;
    private ClientVisuals visuals = null!;

    public bool IsOpen => panel.Visible;

    public void Init(IGameSession gameSession, ClientVisuals clientVisuals)
    {
        session = gameSession;
        visuals = clientVisuals;
        panel = new PanelContainer { Position = new Vector2(8, 8), Visible = false };
        var grid = new GridContainer { Columns = Inventory.HotbarSize };
        grid.AddThemeConstantOverride("h_separation", 2);
        grid.AddThemeConstantOverride("v_separation", 2);
        panel.AddChild(grid);
        for (int i = 0; i < Inventory.Slots; i++)
        {
            var slot = new InventorySlot();
            slot.Init(i, Move, Drop, IsMouseInside);
            slots[i] = slot;
            grid.AddChild(slot);
        }
        AddChild(panel);
    }

    public void Toggle() => panel.Visible = !panel.Visible;

    public void Close() => panel.Visible = false;

    public void Refresh()
    {
        if (!panel.Visible) return;
        var inventory = session.World.GetInventory(session.LocalPlayerId);
        if (inventory is null) return;
        for (int i = 0; i < slots.Length; i++)
            slots[i].Show(inventory[i], visuals);
    }

    private void Move(int from, int to) => session.Send(new MoveInventoryItem(session.LocalPlayerId, from, to));

    private void Drop(int slot) => session.Send(new DropItem(session.LocalPlayerId, slot));

    private bool IsMouseInside() => panel.GetGlobalRect().HasPoint(panel.GetViewport().GetMousePosition());
}
```

- [ ] **Step 3: Ligar no `GameRoot`**

Campo `private InventoryPanel inventory = null!;`; criar na montagem (`new InventoryPanel()`, `AddChild`, `Init(session, GameFlow.Visuals)`); em `_Process` chamar `inventory.Refresh();` junto do `hotbar.Refresh()`; em `_UnhandledInput`, `Key.E` alterna (`inventory.Toggle()`) e `Esc` fecha o inventário primeiro se ele estiver aberto (só abre a pausa se o inventário já estiver fechado). Com o inventário aberto o controle continua ativo (o jogador pode andar), e o clique sobre o painel não minera porque o `PlayerController` já ignora o botão esquerdo sobre a GUI.

- [ ] **Step 4: Verificar**

Run: `dotnet build src/Zyon.Game/Zyon.Game.csproj` → 0/0; `tools/smoke-test.sh 600` → OK; `dotnet test` → verde.

- [ ] **Step 5: Commit**

```bash
git add src/Zyon.Game
git commit -m "feat(game): inventário completo com arrastar e soltar" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 8: Documentação e verificação final

- [ ] **Step 1: `CLAUDE.md`**

Na seção "Comandos", substituir a linha "Jogar" por:

```markdown
- Jogar: `tools/play.sh` ou `.\tools\play.ps1` (abre o menu). `--dev` entra direto num mundo de teste **descartável**; `--world NOME --player NOME` entra direto num mundo salvo (cria se não existir); `--saves-dir PASTA` muda onde ficam os saves (padrão `user://saves`); `--autosave-seconds N` (padrão 300).
- Controles: A/D andar, Espaço pular, botão esquerdo usar item, 1–0/roda hotbar, Q soltar item, E inventário (arrastar entre slots; soltar fora do painel joga o item no chão), Esc pausa.
```

Na seção "Estrutura", no item de `Zyon.Client`, acrescentar: "fluxo de jogo (`SaveLibrary`, `GameLoader`, `GameSaver`, `AutosaveTimer`, `DevLaunch`)". No item de `Zyon.Game`: "cenas `MainMenu` → `Loading` → `Game`, ligadas por `GameFlow`".

- [ ] **Step 2: Verificação**

Run: `dotnet build -c Release` → 0 avisos/erros; `dotnet test` e `dotnet test -c Release` → verdes; `tools/smoke-test.sh 600` → `SMOKE OK ... save ok`; `git status --short` → só o CLAUDE.md.

- [ ] **Step 3: Commit**

```bash
git add CLAUDE.md
git commit -m "docs: fluxo de menus, saves e controles" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 4: Teste manual (pelo usuário)** — critérios do spec (seção 12):
  1. Criar personagem e mundo (pequeno) pelo menu, ver o progresso da geração e entrar no jogo.
  2. Andar, pular, subir degraus; minerar (rachaduras), coletar itens; colocar blocos.
  3. Abrir o inventário (E), arrastar itens entre slots e soltar um item fora do painel.
  4. Esc → "Salvar e voltar ao menu"; escolher o mesmo mundo e personagem: tudo como estava (blocos minerados, inventário, posição).
  5. Fechar a janela no X: o jogo salva antes de sair.
