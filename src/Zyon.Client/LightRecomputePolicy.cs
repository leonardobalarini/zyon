using Zyon.Core.Lighting;

namespace Zyon.Client;

/// <summary>
/// Decide se a luz precisa ser recalculada neste frame: na primeira vez, se um tile mudou dentro da região calculada,
/// se o jogador mudou de tile (o halo anda), se o fator do céu variou mais de 0,01 ou se a área visível chegou
/// perto demais da borda da região (dentro da margem). O chamador passa a Computed a região sem ClampTo do mundo.
/// </summary>
public sealed class LightRecomputePolicy(int margin)
{
    public const float SkyThreshold = 0.01f;

    private bool computedOnce;
    private bool dirty;
    private TileRect region;
    private (int X, int Y) playerTile;
    private float sky;

    public void MarkTileChanged(int x, int y)
    {
        if (computedOnce && region.Contains(x, y)) dirty = true;
    }

    public bool ShouldRecompute(TileRect visible, (int X, int Y) currentPlayerTile, float currentSky) =>
        !computedOnce
        || dirty
        || currentPlayerTile != playerTile
        || MathF.Abs(currentSky - sky) > SkyThreshold
        || !region.Expand(-margin).Contains(visible);

    public void Computed(TileRect computedRegion, (int X, int Y) currentPlayerTile, float currentSky)
    {
        computedOnce = true;
        dirty = false;
        region = computedRegion;
        playerTile = currentPlayerTile;
        sky = currentSky;
    }
}
