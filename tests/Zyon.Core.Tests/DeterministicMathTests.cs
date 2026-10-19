using Zyon.Core.WorldGen;

namespace Zyon.Core.Tests;

public class DeterministicMathTests
{
    [Fact]
    public void SinAndCos_MatchSystemMath_WithinTolerance()
    {
        const int n = 10_000;
        for (int i = 0; i < n; i++)
        {
            double a = -20 + 40.0 * i / (n - 1);
            Assert.InRange(Math.Abs(DeterministicMath.Sin((float)a) - Math.Sin(a)), 0, 2e-3);
            Assert.InRange(Math.Abs(DeterministicMath.Cos((float)a) - Math.Cos(a)), 0, 2e-3);
        }
    }

    [Fact]
    public void Sin_OfZero_IsExactlyZero() => Assert.Equal(0f, DeterministicMath.Sin(0f));
}
