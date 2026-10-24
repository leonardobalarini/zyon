using Zyon.Client;
using Zyon.Core.WorldGen;

namespace Zyon.ArtGen;

/// <summary>Molde do jogador em camadas. Ver a tabela de geometria do plano 6, Task 5.</summary>
public static class PlayerGenerator
{
    private const int W = 32, H = 48;
    private const int ShoulderX = 16, ShoulderY = 16, ArmLength = 11;

    public static PlayerArt Build(ArtSource src, out Dictionary<string, Canvas> layers, out PlayerArt meta)
    {
        meta = new PlayerArt();
        var p = src.Player;
        var skin = ArtSource.Colors(p.Skin);
        var hair = ArtSource.Colors(p.Hair);
        var outfit = ArtSource.Colors(p.Outfit);
        var boots = ArtSource.Colors(p.Boots);
        var detail = Rgba.Parse(p.Detail);
        var outline = Rgba.Parse(p.Outline);

        var body = new Canvas(meta.BodyFrames * W, H);
        var clothes = new Canvas(meta.BodyFrames * W, H);
        var hairs = new Canvas(meta.BodyFrames * W, H);
        var arm = new Canvas(meta.ArmFrames * W, H);

        for (int f = 0; f < meta.BodyFrames; f++)
        {
            var (front, back, armA) = Pose(meta, f);
            int ox = f * W;
            // braço de trás: manga na roupa (atrás do tronco), mão no corpo
            Limb(clothes, ox, ShoulderX - 2, ShoulderY, -armA, 0, 7, 3, outfit[0]);
            Limb(body, ox, ShoulderX - 2, ShoulderY, -armA, 8, ArmLength, 3, skin[0]);
            // cabeça e pescoço
            Rect(body, ox, 11, 3, 20, 12, skin[1]);
            Rect(body, ox, 11, 3, 11, 12, skin[0]);
            body.Set(ox + 18, 7, outline);
            Rect(body, ox, 14, 13, 17, 13, skin[0]);
            // pernas e botas
            Limb(clothes, ox, 13, 30, back, 0, 13, 4, outfit[0]);
            Limb(clothes, ox, 13, 30, back, 14, 16, 4, boots[0]);
            Limb(clothes, ox, 18, 30, front, 0, 13, 4, outfit[1]);
            Limb(clothes, ox, 18, 30, front, 14, 16, 4, boots[1]);
            // tronco
            Rect(clothes, ox, 11, 14, 20, 29, outfit[1]);
            Rect(clothes, ox, 19, 14, 20, 29, outfit[0]);
            Rect(clothes, ox, 11, 14, 12, 15, outfit[2]);
            Rect(clothes, ox, 15, 15, 15, 28, detail);
            Rect(clothes, ox, 11, 27, 20, 27, outfit[0]);
            // cabelo
            Rect(hairs, ox, 10, 2, 21, 5, hair[1]);
            Rect(hairs, ox, 10, 2, 21, 2, hair[2]);
            Rect(hairs, ox, 10, 6, 12, 10, hair[0]);

            body.Outline(ox, 0, W, H, outline);
            clothes.Outline(ox, 0, W, H, outline);
            hairs.Outline(ox, 0, W, H, outline);
        }

        for (int f = 0; f < meta.ArmFrames; f++)
        {
            float a = f >= meta.SwingFirst ? SwingAngles[f - meta.SwingFirst] : Pose(meta, f).Arm;
            int ox = f * W;
            Limb(arm, ox, ShoulderX, ShoulderY, a, 0, 7, 3, outfit[2]);
            Limb(arm, ox, ShoulderX, ShoulderY, a, 8, ArmLength, 3, skin[1]);
            arm.Outline(ox, 0, W, H, outline);
            var (dx, dy) = Dir(a);
            meta.Hand.Add([ShoulderX + Round(dx * ArmLength), ShoulderY + Round(dy * ArmLength)]);
            meta.ArmAngleDeg.Add(90f - a);
        }

        layers = new Dictionary<string, Canvas>
        {
            ["body"] = body,
            ["outfit"] = clothes,
            ["hair"] = hairs,
            [ArtManifest.ArmLayer] = arm,
        };
        return meta;
    }

    private static readonly float[] SwingAngles = [200f, 157f, 115f, 72f, 30f];

    /// <summary>(perna da frente, perna de trás, braço da frente) em graus para um quadro do corpo.</summary>
    private static (float Front, float Back, float Arm) Pose(PlayerArt meta, int frame)
    {
        if (frame == meta.Air) return (25f, -15f, 30f);
        if (frame >= meta.WalkFirst && frame < meta.WalkFirst + meta.WalkCount)
        {
            float phi = (frame - meta.WalkFirst) / (float)meta.WalkCount * 6.2831853f;
            float s = DeterministicMath.Sin(phi);
            return (30f * s, -30f * s, -25f * s);
        }
        return (0f, 0f, 5f);
    }

    private static (float X, float Y) Dir(float degrees)
    {
        float r = degrees * 0.017453292f;
        return (DeterministicMath.Sin(r), DeterministicMath.Cos(r));
    }

    private static int Round(float v) => (int)MathF.Round(v, MidpointRounding.AwayFromZero);

    /// <summary>Segmento de membro: do ponto (x0, y0), na direção do ângulo, de t = from até to, com a largura dada.</summary>
    private static void Limb(Canvas c, int ox, int x0, int y0, float degrees, int from, int to, int width, Rgba color)
    {
        var (dx, dy) = Dir(degrees);
        for (int t = from; t <= to; t++)
        {
            int cx = x0 + Round(dx * t), cy = y0 + Round(dy * t);
            for (int k = 0; k < width; k++)
                c.Set(ox + cx - width / 2 + k, cy, color);
        }
    }

    private static void Rect(Canvas c, int ox, int x0, int y0, int x1, int y1, Rgba color)
    {
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
            c.Set(ox + x, y, color);
    }
}
