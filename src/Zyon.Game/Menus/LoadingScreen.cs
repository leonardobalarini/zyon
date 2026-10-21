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
        GetTree().AutoAcceptQuit = true;
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
        var library = GameFlow.CurrentLibrary;
        work = Task.Run(() => GameLoader.Run(library, GameFlow.Registry, GameFlow.WorldGenConfig,
            request, progress, token, new GodotLog()), token);
    }

    public override void _Process(double delta)
    {
        if (work is null || failed) return;
        if (progress.Latest is { } p && !cancel.IsCancellationRequested)
        {
            bar.Value = p.Fraction;
            // Depois do último passe ainda há gravação do mundo novo e montagem da simulação.
            label.Text = p.Fraction >= 0.999f
                ? "Preparando o mundo..."
                : $"Gerando: {(PassNames.TryGetValue(p.Pass, out var name) ? name : p.Pass)}";
        }
        if (!work.IsCompleted) return;

        if (work.IsCompletedSuccessfully)
        {
            if (cancel.IsCancellationRequested)
            {
                // Cancelado tarde demais para interromper: o jogo ficou pronto, mas o jogador pediu para voltar.
                work.Result.Simulation.Dispose();
                work = null;
                GameFlow.GoTo(this, Scenes.MainMenu);
                return;
            }
            GameFlow.PendingGame = work.Result;
            GameFlow.GoTo(this, Scenes.Game);
            work = null;
            return;
        }

        Exception error = work.Exception?.GetBaseException() ?? new OperationCanceledException();
        work = null;
        if (error is OperationCanceledException)
        {
            GameFlow.GoTo(this, Scenes.MainMenu);
            return;
        }
        if (GameFlow.Automated)
        {
            GD.PrintErr($"SMOKE FAIL: {error}");
            GetTree().Quit(1);
            return;
        }
        GD.PrintErr(error.ToString());
        failed = true;
        label.Text = ErrorMessages.Describe(error);
        button.Text = "Voltar ao menu";
        button.Disabled = false;
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

    // Sem Dispose do CancellationTokenSource: a geração em segundo plano ainda pode estar lendo o token
    // (ex.: janela fechada no meio); cancelar basta para ela parar.
    public override void _ExitTree() => cancel.Cancel();
}
