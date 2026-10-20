using System.Globalization;
using Zyon.Core.WorldGen;

namespace Zyon.Client;

/// <summary>
/// Argumentos do jogo (depois de "--" na linha de comando do executável do jogo). <see cref="CameraSmoothing"/>
/// é a velocidade da suavização da câmera (0 = segue o jogador sem atraso, o padrão). <see cref="ProfileWalk"/>
/// anda pelo mundo por <see cref="SmokeFrames"/> quadros gravando cada quadro e imprime um relatório (diagnóstico
/// de soluços); não pode ser combinado com o smoke test.
/// </summary>
public sealed record LaunchOptions(int Width, int Height, int? Seed, bool SmokeTest, int SmokeFrames, float CameraSmoothing = 0f,
    bool ProfileWalk = false, bool Dev = false, string? World = null, string? Player = null, string? SavesDir = null,
    double AutosaveSeconds = 300)
{
    /// <summary>Pula o menu e entra direto no jogo (desenvolvimento, smoke test e profile-walk).</summary>
    public bool SkipMenu => SmokeTest || ProfileWalk || Dev || World is not null || Player is not null;

    public static readonly (int Width, int Height) DevSize = (1050, 300);

    public static LaunchOptions Parse(IReadOnlyList<string> args)
    {
        var (width, height) = DevSize;
        int? seed = null;
        bool smoke = false;
        int frames = 600;
        float smoothing = 0f;
        bool profile = false;
        bool dev = false;
        string? world = null, player = null, savesDir = null;
        double autosave = 300;

        for (int i = 0; i < args.Count; i++)
        {
            string Next() => i + 1 < args.Count ? args[++i] : throw new ArgumentException($"'{args[i]}' precisa de um valor");
            switch (args[i])
            {
                case "--size":
                    (width, height) = Next() switch
                    {
                        "dev" => DevSize,
                        "small" => WorldSizes.Dimensions(WorldSize.Small),
                        "medium" => WorldSizes.Dimensions(WorldSize.Medium),
                        "large" => WorldSizes.Dimensions(WorldSize.Large),
                        var other => throw new ArgumentException($"tamanho desconhecido '{other}' (dev, small, medium, large)"),
                    };
                    break;
                case "--seed":
                    seed = int.TryParse(Next(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int s)
                        ? s : throw new ArgumentException("--seed precisa de um inteiro");
                    break;
                case "--smoke-test":
                    smoke = true;
                    break;
                case "--profile-walk":
                    profile = true;
                    break;
                case "--frames":
                    frames = int.TryParse(Next(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int f) && f > 0
                        ? f : throw new ArgumentException("--frames precisa de um inteiro > 0");
                    break;
                case "--camera-smoothing":
                    smoothing = float.TryParse(Next(), NumberStyles.Float, CultureInfo.InvariantCulture, out float c) && float.IsFinite(c) && c >= 0
                        ? c : throw new ArgumentException("--camera-smoothing precisa de um número >= 0 (0 = sem suavização)");
                    break;
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
                default:
                    throw new ArgumentException($"argumento desconhecido '{args[i]}'");
            }
        }

        if (smoke && profile)
            throw new ArgumentException("--smoke-test e --profile-walk não podem ser usados juntos");
        if (world is not null && (dev || smoke || profile))
            throw new ArgumentException("--world não combina com modos descartáveis (--dev, --smoke-test, --profile-walk)");
        return new LaunchOptions(width, height, seed, smoke, frames, smoothing, profile, dev, world, player, savesDir, autosave);
    }
}
