using System.Globalization;
using Zyon.Core.WorldGen;

namespace Zyon.Client;

/// <summary>Argumentos do jogo (depois de "--" na linha de comando do Godot).</summary>
public sealed record LaunchOptions(int Width, int Height, int? Seed, bool SmokeTest, int SmokeFrames)
{
    public static readonly (int Width, int Height) DevSize = (1050, 300);

    public static LaunchOptions Parse(IReadOnlyList<string> args)
    {
        var (width, height) = DevSize;
        int? seed = null;
        bool smoke = false;
        int frames = 600;

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
                case "--frames":
                    frames = int.TryParse(Next(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int f) && f > 0
                        ? f : throw new ArgumentException("--frames precisa de um inteiro > 0");
                    break;
                default:
                    throw new ArgumentException($"argumento desconhecido '{args[i]}'");
            }
        }

        return new LaunchOptions(width, height, seed, smoke, frames);
    }
}
