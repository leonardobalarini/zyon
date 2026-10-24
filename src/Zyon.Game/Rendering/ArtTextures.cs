using System.Collections.Generic;
using Godot;
using Zyon.Client;
using Zyon.Core.Data;

namespace Zyon.Game.Rendering;

/// <summary>Texturas da arte (PNGs de data/client/art), carregadas uma vez e conferidas contra o manifesto.</summary>
public sealed class ArtTextures
{
    public required Texture2D Tiles { get; init; }
    public required Texture2D Walls { get; init; }
    public required Texture2D Merge { get; init; }
    public required Texture2D Items { get; init; }

    /// <summary>Na ordem de <see cref="PlayerArt.Layers"/>.</summary>
    public required IReadOnlyList<Texture2D> PlayerLayers { get; init; }

    public required Texture2D PlayerArm { get; init; }

    /// <summary>Lança DataException se um PNG faltar, não puder ser lido ou tiver tamanho diferente do manifesto.</summary>
    public static ArtTextures Load(ArtCatalog art)
    {
        var layers = new List<Texture2D>();
        foreach (var layer in art.Manifest.Player.Layers) layers.Add(Png(art, ArtManifest.PlayerFile(layer)));
        return new ArtTextures
        {
            Tiles = Png(art, ArtManifest.TilesFile),
            Walls = Png(art, ArtManifest.WallsFile),
            Merge = Png(art, ArtManifest.MergeFile),
            Items = Png(art, ArtManifest.ItemsFile),
            PlayerLayers = layers,
            PlayerArm = Png(art, ArtManifest.PlayerFile(ArtManifest.ArmLayer)),
        };
    }

    private static ImageTexture Png(ArtCatalog art, string file)
    {
        string path = art.PathOf(file);
        byte[] bytes;
        try
        {
            bytes = System.IO.File.ReadAllBytes(path);
        }
        catch (System.Exception ex) when (ex is System.IO.IOException or System.UnauthorizedAccessException)
        {
            throw new DataException($"arte: não consegui ler '{path}' ({ex.Message})", ex);
        }
        var image = new Image();
        var error = image.LoadPngFromBuffer(bytes);
        if (error != Error.Ok) throw new DataException($"arte: não consegui ler '{path}' ({error})");
        art.CheckSize(file, image.GetWidth(), image.GetHeight());
        return ImageTexture.CreateFromImage(image);
    }
}
