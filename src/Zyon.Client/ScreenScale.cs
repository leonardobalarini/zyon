namespace Zyon.Client;

/// <summary>
/// Escalas inteiras da tela. A interface usa <see cref="For"/>: cada pixel dela vira S×S pixels reais, com S = quantas
/// vezes <see cref="TargetHeight"/> cabe na altura da janela (no mínimo 1). O mundo usa <see cref="ForWorld"/>, com alvo
/// <see cref="WorldTargetHeight"/> (como o Terraria: em 1080p um pixel da arte = um pixel da tela, 120×67 tiles; em
/// 1440p 160×90); a câmera aplica a diferença com <see cref="WorldZoom"/>; o jogador aproxima ou afasta em passos inteiros. Como as duas escalas são inteiras, os
/// pixels da arte continuam iguais. <see cref="Tolerance"/> deixa uma janela maximizada (sem a barra de título e de
/// tarefas, ~1010 px em 1080p) ficar com a escala da tela cheia. A largura acompanha a janela.
/// </summary>
public static class ScreenScale
{
    public const int TargetHeight = 540;
    public const int WorldTargetHeight = 1080;

    /// <summary>Fração do alvo que pode faltar à janela para ainda ganhar a escala de cima.</summary>
    public const double Tolerance = 0.15;

    /// <summary>Escala da interface (e da tela interna).</summary>
    public static int For(int windowHeight) => Fit(windowHeight, TargetHeight);

    /// <summary>Pixels reais por pixel da arte no mundo no zoom padrão; nunca maior que a escala da interface.</summary>
    public static int ForWorld(int windowHeight) => Math.Min(Fit(windowHeight, WorldTargetHeight), For(windowHeight));

    /// <summary>
    /// Escala do mundo com o zoom do jogador (<paramref name="zoomSteps"/> &gt; 0 aproxima, &lt; 0 afasta), presa entre 1
    /// e a escala da interface + 1.
    /// </summary>
    public static int ForWorld(int windowHeight, int zoomSteps) =>
        Math.Clamp(ForWorld(windowHeight) + zoomSteps, 1, For(windowHeight) + 1);

    /// <summary>Passos de zoom ainda válidos para esta janela (o que <see cref="ForWorld(int, int)"/> realmente usa).</summary>
    public static int ClampZoomSteps(int windowHeight, int zoomSteps) => ForWorld(windowHeight, zoomSteps) - ForWorld(windowHeight);

    /// <summary>Zoom da câmera sobre a tela interna: escala do mundo ÷ escala da interface (&lt; 1 = mais mundo).</summary>
    public static float WorldZoom(int windowHeight, int zoomSteps = 0) => ForWorld(windowHeight, zoomSteps) / (float)For(windowHeight);

    /// <summary>
    /// Tamanho da tela interna para a escala <paramref name="scale"/>. Truncado: com o modo de escala inteira do motor,
    /// janela ÷ tamanho fica entre S e S+1 e o motor usa exatamente S (a sobra de até S−1 px vai para a borda).
    /// </summary>
    public static (int Width, int Height) BaseSize(int windowWidth, int windowHeight, int scale) =>
        (Math.Max(1, windowWidth / scale), Math.Max(1, windowHeight / scale));

    private static int Fit(int windowHeight, int target) =>
        Math.Max(1, (int)Math.Floor(windowHeight / (double)target + Tolerance));
}
