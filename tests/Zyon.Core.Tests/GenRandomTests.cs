using Zyon.Core.WorldGen;

namespace Zyon.Core.Tests;

public class GenRandomTests
{
    [Fact]
    public void SameSeed_SameSequence()
    {
        var a = new GenRandom(7);
        var b = new GenRandom(7);
        for (int i = 0; i < 1000; i++) Assert.Equal(a.Next(), b.Next());
    }

    [Fact]
    public void DifferentSeeds_Differ()
    {
        var a = new GenRandom(1);
        var b = new GenRandom(2);
        var sa = Enumerable.Range(0, 10).Select(_ => a.Next()).ToArray();
        var sb = Enumerable.Range(0, 10).Select(_ => b.Next()).ToArray();
        Assert.NotEqual(sa, sb);
    }

    [Fact]
    public void NextMax_StaysInRangeAndCoversAllValues()
    {
        var r = new GenRandom(3);
        var seen = new bool[10];
        for (int i = 0; i < 100_000; i++)
        {
            int v = r.Next(10);
            Assert.InRange(v, 0, 9);
            seen[v] = true;
        }
        Assert.All(seen, Assert.True);
    }

    [Fact]
    public void NextMinMax_EqualReturnsMin() => Assert.Equal(5, new GenRandom(1).Next(5, 5));

    [Fact]
    public void NextMinMax_MinGreaterThanMax_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new GenRandom(1).Next(6, 5));

    [Fact]
    public void NextMinMax_HandlesNegativeRanges()
    {
        var r = new GenRandom(4);
        for (int i = 0; i < 10_000; i++) Assert.InRange(r.Next(-5, 5), -5, 4);
    }

    [Fact]
    public void NextDouble_InUnitInterval()
    {
        var r = new GenRandom(5);
        for (int i = 0; i < 10_000; i++) Assert.InRange(r.NextDouble(), 0.0, 0.9999999999999999);
    }

    [Fact]
    public void Golden_FirstFiveValuesAreStable()
    {
        var r = new GenRandom(42);
        var values = Enumerable.Range(0, 5).Select(_ => r.Next()).ToArray();
        Assert.Equal(new[] { 343404953, 598291371, 739143935, 81669165, 1864505597 }, values);
    }
}
