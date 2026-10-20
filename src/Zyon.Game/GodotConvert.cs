using Zyon.Client;

namespace Zyon.Game;

/// <summary>Conversões entre tipos do Client/.NET e do Godot.</summary>
public static class GodotConvert
{
    public static Godot.Vector2 ToGodot(this System.Numerics.Vector2 v) => new(v.X, v.Y);

    public static System.Numerics.Vector2 ToNumerics(this Godot.Vector2 v) => new(v.X, v.Y);

    public static Godot.Color ToGodot(this Rgb c, float alpha = 1f) => new(c.R / 255f, c.G / 255f, c.B / 255f, alpha);
}
