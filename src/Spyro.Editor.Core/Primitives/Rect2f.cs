namespace Spyro.Editor.Core.Primitives;

public readonly record struct Rect2f(float Left, float Top, float Right, float Bottom)
{
    public static Rect2f Empty { get; } = new(0, 0, 0, 0);

    public float Width => Math.Max(0, Right - Left);
    public float Height => Math.Max(0, Bottom - Top);
    public bool IsEmpty => Width <= 0 || Height <= 0;

    public static Rect2f FromBounds(float minX, float minY, float maxX, float maxY)
    {
        return new Rect2f(minX, minY, maxX, maxY);
    }
}
