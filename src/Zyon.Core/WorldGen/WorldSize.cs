namespace Zyon.Core.WorldGen;

public enum WorldSize { Small, Medium, Large }

public static class WorldSizes
{
    public static (int Width, int Height) Dimensions(WorldSize size) => size switch
    {
        WorldSize.Small => (4200, 1200),
        WorldSize.Medium => (6400, 1800),
        WorldSize.Large => (8400, 2400),
        _ => throw new ArgumentOutOfRangeException(nameof(size)),
    };
}
