using Zyon.Core.Sim;

namespace Zyon.Client;

/// <summary>
/// Linha (em tiles) da base do fundo em parallax: a média da superfície das colunas visíveis mais
/// <see cref="BaseOffsetRows"/> (a base fica um pouco abaixo do chão, escondida atrás dele). Uma linha fixa no mundo
/// não serve: a superfície varia 10% da altura (120 tiles num mundo pequeno) e o fundo tem só ~22 tiles, então ele
/// ficaria quase sempre enterrado ou flutuando. A superfície de cada coluna é lida uma vez, no início do jogo; o que o
/// jogador cava depois não importa. A base vai até o alvo suavemente (exponencial, meia-vida
/// <see cref="HalfLifeSeconds"/>), sem passar dele; a primeira chamada vai direto.
/// </summary>
public sealed class BackdropAnchor
{
    /// <summary>Linhas abaixo da superfície média onde fica a base do fundo.</summary>
    public const float BaseOffsetRows = 6f;

    /// <summary>Tempo para a base percorrer metade da distância até o alvo.</summary>
    public const float HalfLifeSeconds = 0.5f;

    // prefix[i] = soma das linhas da superfície das colunas 0..i-1: média de qualquer intervalo em O(1).
    private readonly long[] prefix;
    private float current;
    private bool started;

    public BackdropAnchor(IReadOnlyList<int> surfaceRows)
    {
        if (surfaceRows.Count == 0) throw new ArgumentException("mundo sem colunas", nameof(surfaceRows));
        prefix = new long[surfaceRows.Count + 1];
        for (int i = 0; i < surfaceRows.Count; i++) prefix[i + 1] = prefix[i] + surfaceRows[i];
    }

    /// <summary>Primeira linha com bloco sólido de cada coluna (de cima para baixo); a altura se não houver nenhum.</summary>
    public static int[] SurfaceRows(IWorldView world)
    {
        var rows = new int[world.Width];
        for (int x = 0; x < world.Width; x++)
        {
            int y = 0;
            while (y < world.Height && !world.Registry.Block(world.GetBlock(x, y)).Solid) y++;
            rows[x] = y;
        }
        return rows;
    }

    /// <summary>Linha da base atual (tiles) para as colunas visíveis [first, last], recortadas ao mundo.</summary>
    public float Update(int firstColumn, int lastColumn, float seconds)
    {
        int columns = prefix.Length - 1;
        int first = Math.Clamp(firstColumn, 0, columns - 1);
        int last = Math.Clamp(lastColumn, first, columns - 1);
        float target = (prefix[last + 1] - prefix[first]) / (float)(last - first + 1) + BaseOffsetRows;
        if (!started)
        {
            started = true;
            current = target;
        }
        else if (seconds > 0f)
        {
            float remaining = MathF.Pow(0.5f, seconds / HalfLifeSeconds);
            current = target + (current - target) * remaining;
        }
        return current;
    }
}
