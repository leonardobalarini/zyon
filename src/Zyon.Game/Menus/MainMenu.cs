using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;
using Zyon.Client;
using Zyon.Core.Data;
using Zyon.Core.Persistence;

namespace Zyon.Game.Menus;

/// <summary>
/// Escolher/criar personagem e escolher/criar mundo. Com --dev, --world, --player, --smoke-test ou --profile-walk,
/// pula direto para o jogo.
/// </summary>
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
    // Falso se os dados/opções não carregaram: os botões não fazem nada (só a mensagem de erro é mostrada).
    private bool ready;

    public override void _Ready()
    {
        // O jogo desliga o fechamento automático para salvar antes de sair; fora dele a janela fecha normalmente.
        GetTree().AutoAcceptQuit = true;
        try
        {
            GameFlow.EnsureInitialized();
            if (GameFlow.ShouldSkipMenu)
            {
                GameFlow.PendingRequest = GameFlow.BuildSkipMenuRequest();
                GameFlow.GoTo(this, Scenes.Loading);
                return;
            }
        }
        catch (Exception ex) when (ex is DataException or ArgumentException or SaveException or System.IO.IOException)
        {
            GD.PrintErr($"Falha ao iniciar: {ex.Message}");
            if (GameFlow.Automated)
            {
                GD.PrintErr($"SMOKE FAIL: {ex.Message}");
                GetTree().Quit(1);
                return;
            }
            BuildUi();
            status.Text = ex is ArgumentException ? $"Argumentos inválidos: {ex.Message}" : ErrorMessages.Describe(ex);
            return;
        }

        BuildUi();
        ready = true;
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
        try
        {
            players.Clear();
            foreach (var name in GameFlow.Library.Players()) players.AddItem(name);
            if (players.ItemCount > 0) players.Select(0);
            worlds.Clear();
            foreach (var name in GameFlow.Library.Worlds()) worlds.AddItem(name);
            if (worlds.ItemCount > 0) worlds.Select(0);
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            status.Text = $"Não foi possível ler a pasta de saves '{GameFlow.Library.Root}': {ex.Message}";
        }
    }

    private void OnCreatePlayer()
    {
        if (!ready) return;
        try
        {
            var created = GameFlow.Library.CreatePlayer(playerName.Text, GameFlow.Registry);
            playerName.Text = "";
            Refresh();
            for (int i = 0; i < players.ItemCount; i++)
                if (players.GetItemText(i) == created.Name) players.Select(i);
            status.Text = $"Personagem '{created.Name}' criado.";
        }
        catch (Exception ex) when (ex is DataException or SaveException or System.IO.IOException or UnauthorizedAccessException)
        {
            status.Text = ErrorMessages.Describe(ex);
        }
    }

    private void OnCreateWorld()
    {
        if (!ready || !TrySelectedPlayer(out var player, out var warnings)) return;
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
        if (!ready) return;
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
        GameFlow.CurrentLibrary = GameFlow.Library;
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
