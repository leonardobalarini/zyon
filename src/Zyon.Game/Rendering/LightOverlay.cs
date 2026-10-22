using System;
using System.Collections.Generic;
using System.Diagnostics;
using Godot;
using Zyon.Client;
using Zyon.Core.Lighting;
using Zyon.Core.Sim;

namespace Zyon.Game.Rendering;

/// <summary>
/// Calcula a luz da área visível (+ margem) e a desenha como uma textura de 1 px por tile, esticada ×16 com filtro
/// bilinear (degradê suave) e blend multiply sobre o mundo e as entidades. HUD e menus ficam fora (CanvasLayers);
/// o WorldOverlay (destaque e rachaduras) vem depois na árvore e não é escurecido. Fora do mundo nada é
/// multiplicado (a região é recortada ao mundo).
/// </summary>
public partial class LightOverlay : Node2D
{
    // Folga além da margem: a câmera anda alguns tiles antes de a região precisar ser recalculada.
    private const int Slack = 8;

    private readonly LightMap map = new();
    private readonly List<LightEmitter> extra = new(1);
    private readonly Sprite2D sprite = new();
    private IWorldView view = null!;
    private LightingConfig config = null!;
    private LightEngine engine = null!;
    private DayCycle day = null!;
    private LightRecomputePolicy policy = null!;
    private int playerId;
    private byte[] buffer = [];
    private Image? image;
    private ImageTexture? texture;

    public int RecomputesLastUpdate { get; private set; }
    public double LastComputeMs { get; private set; }
    public float SkyFactor { get; private set; }

    public void Init(IWorldView worldView, LightingConfig lightingConfig, int localPlayerId)
    {
        view = worldView;
        config = lightingConfig;
        playerId = localPlayerId;
        engine = new LightEngine(config);
        day = new DayCycle(config);
        policy = new LightRecomputePolicy(config.RegionMargin);
        // Sem centralizar: o texel (0,0) cobre os pixels 0..16 do tile (MinX, MinY), com o centro no centro do tile.
        sprite.Centered = false;
        sprite.TextureFilter = TextureFilterEnum.Linear;
        sprite.Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Mul };
        sprite.Scale = new Vector2(RenderMath.PixelsPerTile, RenderMath.PixelsPerTile);
        sprite.Visible = false;
        AddChild(sprite);
    }

    public void OnEvent(IGameEvent gameEvent)
    {
        if (gameEvent is TileChanged t) policy.MarkTileChanged(t.X, t.Y);
    }

    /// <summary>Nível de luz calculado de um tile (para o smoke test e depuração); 0 fora da região calculada.</summary>
    public float LevelAt(int x, int y) => map.Get(x, y);

    public void UpdateLight(Rect2 visiblePixels)
    {
        RecomputesLastUpdate = 0;
        const float t = RenderMath.PixelsPerTile;
        var visible = new TileRect(
            Mathf.FloorToInt(visiblePixels.Position.X / t), Mathf.FloorToInt(visiblePixels.Position.Y / t),
            Mathf.FloorToInt(visiblePixels.End.X / t), Mathf.FloorToInt(visiblePixels.End.Y / t));
        (int X, int Y) playerTile = (0, 0);
        if (view.TryGetEntityView(playerId, out var me))
            playerTile = ((int)MathF.Floor(me.Position.X), (int)MathF.Floor(me.Position.Y));
        SkyFactor = day.SkyFactor(view.Tick);
        if (!policy.ShouldRecompute(visible, playerTile, SkyFactor)) return;

        // A política recebe a região sem recorte: perto da borda do mundo ela continua "contendo" a área visível.
        var unclamped = visible.Expand(config.RegionMargin + Slack);
        extra.Clear();
        extra.Add(new LightEmitter(playerTile.X, playerTile.Y, config.PlayerHaloLevel));
        long start = Stopwatch.GetTimestamp();
        engine.Compute(view, unclamped, SkyFactor, extra, map);
        policy.Computed(unclamped, playerTile, SkyFactor);
        Upload();
        LastComputeMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        RecomputesLastUpdate = 1;
    }

    /// <summary>Envia o mapa (já recortado ao mundo) para a textura, reaproveitando buffer, imagem e textura.</summary>
    private void Upload()
    {
        if (map.Region.IsEmpty)
        {
            sprite.Visible = false;
            return;
        }
        int w = map.Width, h = map.Height;
        if (buffer.Length != w * h) buffer = new byte[w * h];
        LightTexture.ToL8(map, buffer);
        if (image is null || texture is null || image.GetWidth() != w || image.GetHeight() != h)
        {
            image = Image.CreateFromData(w, h, false, Image.Format.L8, buffer);
            texture = ImageTexture.CreateFromImage(image);
            sprite.Texture = texture;
        }
        else
        {
            image.SetData(w, h, false, Image.Format.L8, buffer);
            texture.Update(image);
        }
        sprite.Position = new Vector2(map.Region.MinX * RenderMath.PixelsPerTile, map.Region.MinY * RenderMath.PixelsPerTile);
        sprite.Visible = true;
    }
}
