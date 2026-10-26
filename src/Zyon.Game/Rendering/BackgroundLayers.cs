using System.Collections.Generic;
using Godot;
using Zyon.Client;

namespace Zyon.Game.Rendering;

/// <summary>
/// Fundo distante em parallax: as camadas de todos os biomas (distantes primeiro, depois as próximas), cada uma um
/// Parallax2D que rola na horizontal a uma fração da câmera e se repete; na vertical acompanha o mundo, com a base
/// na linha surfaceMax × altura (abaixo dela só o céu). A opacidade de cada bioma vem do BiomeAmbience. Fica antes
/// dos chunks na árvore (atrás deles) e antes da LightOverlay (a noite escurece o fundo).
/// </summary>
public partial class BackgroundLayers : Node2D
{
    /// <summary>Fração da rolagem da câmera de cada camada (distante, próxima).</summary>
    private static readonly float[] ScrollFactors = [0.1f, 0.3f];

    /// <summary>Pixels do mundo por pixel do fundo.</summary>
    private const int PixelScale = 2;

    /// <summary>
    /// Cópias além da original, metade de cada lado. O Parallax2D mantém a original a menos de uma repetição da borda
    /// da tela, então o lado mais curto cobre (RepeatTimes / 2) × largura: com 480 px × 2 = 960 px do mundo, 16 cobre
    /// 7680 px — a tela mais larga (8K ou ultrawide) no zoom mais afastado (1 pixel do mundo por pixel real).
    /// </summary>
    private const int RepeatTimes = 16;

    private readonly List<(string Biome, Parallax2D Node)> layers = new();

    public void Init(ClientVisuals visuals, IReadOnlyDictionary<string, Texture2D> textures, float baseY)
    {
        for (int i = 0; i < ClientVisuals.LayerCount; i++)
            foreach (var (biome, visual) in visuals.Biomes)
            {
                var texture = textures[visual.Layers[i]];
                var parallax = new Parallax2D
                {
                    ScrollScale = new Vector2(ScrollFactors[i], 1f),
                    RepeatSize = new Vector2(texture.GetWidth() * PixelScale, 0f),
                    RepeatTimes = RepeatTimes,
                    Visible = false,
                };
                parallax.AddChild(new Sprite2D
                {
                    Texture = texture,
                    Centered = false,
                    Scale = new Vector2(PixelScale, PixelScale),
                    Position = new Vector2(0f, baseY - texture.GetHeight() * PixelScale),
                    TextureFilter = TextureFilterEnum.Nearest,
                });
                AddChild(parallax);
                layers.Add((biome, parallax));
            }
    }

    public void Refresh(BiomeAmbience ambience)
    {
        foreach (var (biome, node) in layers)
        {
            float weight = ambience.Weight(biome);
            node.Visible = weight > 0f;
            node.Modulate = new Color(1f, 1f, 1f, weight);
        }
    }
}
