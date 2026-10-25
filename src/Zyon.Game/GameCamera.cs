using Godot;
using Zyon.Client;

namespace Zyon.Game;

/// <summary>
/// Segue o jogador e fica presa às bordas do mundo. Por padrão segue sem suavização: com a suavização do
/// Camera2D (velocidade 10) a imagem ficava ~100 ms e até ~1 tile atrás do jogador, sentido como latência.
/// O alvo é arredondado ao pixel, como o snap 2D do projeto faz com os sprites.
/// </summary>
public partial class GameCamera : Camera2D
{
    /// <summary>Velocidade usada ao ligar a suavização com F4 quando ela foi iniciada desligada (o valor antigo).</summary>
    public const float ComparisonSmoothingSpeed = 10f;

    public void Init(int worldWidthTiles, int worldHeightTiles, float smoothingSpeed)
    {
        LimitLeft = 0;
        LimitTop = 0;
        LimitRight = worldWidthTiles * RenderMath.PixelsPerTile;
        LimitBottom = worldHeightTiles * RenderMath.PixelsPerTile;
        PositionSmoothingSpeed = smoothingSpeed > 0 ? smoothingSpeed : ComparisonSmoothingSpeed;
        PositionSmoothingEnabled = smoothingSpeed > 0;
    }

    public override void _Ready() => MakeCurrent();

    /// <summary>
    /// Posiciona a câmera. Sem suavização a rolagem é aplicada na hora, então o mouse no mundo e a faixa visível
    /// lidos depois disto já valem para este quadro. Com suavização não: ForceUpdateScroll avançaria a suavização
    /// uma segunda vez no quadro.
    /// </summary>
    public void Follow(Vector2 targetPixels)
    {
        // Zoom do jogador e da janela (ScreenScaler); lido a cada quadro, então muda na hora.
        Zoom = new Vector2(ScreenScaler.WorldZoom, ScreenScaler.WorldZoom);
        Position = RenderMath.SnapToPixel(targetPixels.ToNumerics()).ToGodot();
        if (!PositionSmoothingEnabled) ForceUpdateScroll();
    }

    /// <summary>Liga/desliga a suavização (F4) para comparar; ResetSmoothing evita que ela parta de uma posição velha.</summary>
    public void ToggleSmoothing()
    {
        PositionSmoothingEnabled = !PositionSmoothingEnabled;
        ResetSmoothing();
    }

    public string ModeDescription => PositionSmoothingEnabled ? $"suave {PositionSmoothingSpeed:0.#}" : "direta";

    /// <summary>Retângulo do mundo (em pixels) que está na tela.</summary>
    public Rect2 VisiblePixels()
    {
        var size = GetViewportRect().Size / Zoom;
        return new Rect2(GetScreenCenterPosition() - size / 2, size);
    }
}
