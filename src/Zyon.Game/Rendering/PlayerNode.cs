using System.Collections.Generic;
using Godot;
using Zyon.Client;

namespace Zyon.Game.Rendering;

/// <summary>
/// Um jogador: camadas (corpo, roupa, cabelo), braço da frente e o item na mão, por cima de tudo. A origem do nó fica nos pés;
/// virar para a esquerda é Scale.X = −1 no nó "pivot", que espelha tudo junto. FeetY é a linha de pixels dos pés
/// (a última do quadro): ela fica logo acima da origem, apoiada no chão, e não 1 px dentro dele.
/// </summary>
public partial class PlayerNode : Node2D
{
    private readonly Node2D pivot = new();
    private readonly List<Sprite2D> layers = new();
    private readonly Sprite2D item = new();
    private readonly Sprite2D arm = new();
    private PlayerArt art = null!;
    private ArtCatalog catalog = null!;
    private PlayerPose? lastPose;

    /// <summary>Estado do <see cref="PlayerAnimator"/> deste jogador (passado por ref a cada quadro).</summary>
    public PlayerAnimState State;

    public void Init(ArtCatalog artCatalog, ArtTextures textures)
    {
        catalog = artCatalog;
        art = artCatalog.Manifest.Player;
        AddChild(pivot);
        foreach (var texture in textures.PlayerLayers)
        {
            var s = new Sprite2D { Texture = texture };
            ConfigureFrames(s, art.BodyFrames);
            layers.Add(s);
            pivot.AddChild(s);
        }
        arm.Texture = textures.PlayerArm;
        ConfigureFrames(arm, art.ArmFrames);
        pivot.AddChild(arm);
        item.Texture = textures.Items;
        item.RegionEnabled = true;
        item.Centered = false;
        item.TextureFilter = TextureFilterEnum.Nearest;
        item.Visible = false;
        pivot.AddChild(item);
    }

    public void Apply(PlayerPose pose)
    {
        if (lastPose == pose) return;
        lastPose = pose;
        pivot.Scale = new Vector2(pose.FacingLeft ? -1 : 1, 1);
        foreach (var s in layers) s.Frame = pose.BodyFrame;
        arm.Frame = pose.ArmFrame;
        item.Visible = pose.ItemId != 0;
        if (pose.ItemId == 0) return;
        const int size = TileAtlasLayout.TileSize;
        var a = catalog.ItemByItemId[pose.ItemId]!;
        item.RegionRect = new Rect2(a.Cell * size, 0, size, size);
        // Offset desloca o ícone para que o ponto de encaixe fique na origem do sprite, que vai para a mão;
        // a rotação gira em torno dessa origem.
        item.Offset = new Vector2(-a.GripX, -a.GripY);
        item.Position = new Vector2(pose.HandX - art.FeetX, pose.HandY - OriginY);
        item.RotationDegrees = pose.ItemRotationDeg;
    }

    /// <summary>Y do quadro que vai para a origem do nó (borda de baixo da linha dos pés).</summary>
    private int OriginY => art.FeetY + 1;

    private void ConfigureFrames(Sprite2D s, int frames)
    {
        s.Hframes = frames;
        s.Centered = false;
        s.Offset = new Vector2(-art.FeetX, -OriginY);
        s.TextureFilter = TextureFilterEnum.Nearest;
    }
}
