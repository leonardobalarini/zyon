namespace Zyon.Client;

/// <summary>
/// Escala inteira da tela: cada pixel da arte vira S×S pixels reais, com S = quantas vezes <see cref="TargetHeight"/>
/// cabe na altura da janela (no mínimo 1). A sobra vira mais mundo visível: 1080p mostra 60×34 tiles, 1440p 80×45.
/// <see cref="Tolerance"/> deixa uma janela maximizada (sem a barra de título e de tarefas, ~1010 px em 1080p) ficar com
/// a escala da tela cheia em vez de cair para a de baixo. A largura acompanha a janela (monitores largos veem mais).
/// </summary>
public static class ScreenScale
{
    public const int TargetHeight = 540;

    /// <summary>Fração de <see cref="TargetHeight"/> que pode faltar à janela para ainda ganhar a escala de cima.</summary>
    public const double Tolerance = 0.15;

    public static int For(int windowHeight) =>
        Math.Max(1, (int)Math.Floor(windowHeight / (double)TargetHeight + Tolerance));

    /// <summary>
    /// Tamanho da tela interna para a escala <paramref name="scale"/>. Truncado: com o modo de escala inteira do motor,
    /// janela ÷ tamanho fica entre S e S+1 e o motor usa exatamente S (a sobra de até S−1 px vai para a borda).
    /// </summary>
    public static (int Width, int Height) BaseSize(int windowWidth, int windowHeight, int scale) =>
        (Math.Max(1, windowWidth / scale), Math.Max(1, windowHeight / scale));
}
