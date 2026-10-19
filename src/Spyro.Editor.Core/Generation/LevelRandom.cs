namespace Spyro.Editor.Core.Generation;

/// <summary>The generator's own random numbers (SplitMix64): the same seed gives the same level on every machine and
/// .NET version, which <see cref="Random"/> does not promise.</summary>
public sealed class LevelRandom
{
    private ulong _state;
    public LevelRandom(long seed) { _state = (ulong)seed ^ 0x9E3779B97F4A7C15UL; Next64(); }

    public ulong Next64()
    {
        ulong z = _state += 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>0 ≤ value &lt; <paramref name="max"/>.</summary>
    public int Next(int max) => max <= 1 ? 0 : (int)(Next64() % (ulong)max);
    /// <summary><paramref name="min"/> ≤ value &lt; <paramref name="max"/>.</summary>
    public int Next(int min, int max) => min + Next(max - min);
    public double NextDouble() => (Next64() >> 11) * (1.0 / (1UL << 53));
    public bool Chance(double p) => NextDouble() < p;
    public T Pick<T>(IReadOnlyList<T> items) => items[Next(items.Count)];
    /// <summary>One of <paramref name="items"/>, each as likely as its weight.</summary>
    public T Pick<T>(IReadOnlyList<(T Item, double Weight)> items)
    {
        double total = items.Sum(i => i.Weight), at = NextDouble() * total;
        foreach (var (item, weight) in items) { if (at < weight) return item; at -= weight; }
        return items[^1].Item;
    }
    public void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--) { int j = Next(i + 1); (list[i], list[j]) = (list[j], list[i]); }
    }
    /// <summary>A stable identity for a face or document (the editor's GUID "N" form).</summary>
    public string Id()
    {
        Span<byte> b = stackalloc byte[16];
        BitConverter.TryWriteBytes(b, Next64()); BitConverter.TryWriteBytes(b[8..], Next64());
        b[7] = (byte)(b[7] & 0x0F | 0x40); b[8] = (byte)(b[8] & 0x3F | 0x80);
        return new Guid(b).ToString("N");
    }
    /// <summary>An independent stream for one part of the level, so changing one part does not reshuffle the others.</summary>
    public LevelRandom Fork(string part) => new((long)(Next64() ^ (ulong)StableHash(part)));
    private static long StableHash(string s) { ulong h = 1469598103934665603UL; foreach (char c in s) { h ^= c; h *= 1099511628211UL; } return (long)h; }
}
