namespace Zyon.Core.WorldGen;

/// <summary>
/// Seno e cosseno usando apenas +, -, *, / e comparações (exatos pelo IEEE 754 em qualquer plataforma).
/// MathF.Sin/Cos delegam à biblioteca C da plataforma e podem divergir em 1 ulp, o que quebraria
/// "mesma seed ⇒ mesmo mundo" entre Windows/Linux/macOS. Erro máximo ≈ 2e-4 — suficiente para geração.
/// </summary>
internal static class DeterministicMath
{
    private const float Pi = 3.14159265358979f;
    private const float TwoPi = 2f * Pi;
    private const float HalfPi = Pi / 2f;

    public static float Sin(float x)
    {
        // Reduz para [-π, π]. Floor é exato (não é transcendental).
        x -= TwoPi * MathF.Floor(x / TwoPi + 0.5f);
        // Simetria para [-π/2, π/2]
        if (x > HalfPi) x = Pi - x;
        else if (x < -HalfPi) x = -Pi - x;

        float x2 = x * x;
        // Taylor ímpar até x^7
        return x * (1f - x2 / 6f * (1f - x2 / 20f * (1f - x2 / 42f)));
    }

    public static float Cos(float x) => Sin(x + HalfPi);
}
