namespace Spyro.Editor.Core.Sky;

/// <summary>A direction or point in the game's axes (X east, Y north, Z up; heading 0 looks along +X).</summary>
public readonly record struct SkyVector(double X, double Y, double Z)
{
    public static SkyVector operator +(SkyVector a, SkyVector b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static SkyVector operator -(SkyVector a, SkyVector b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static SkyVector operator *(SkyVector a, double s) => new(a.X * s, a.Y * s, a.Z * s);
    public double Dot(SkyVector b) => X * b.X + Y * b.Y + Z * b.Z;
    public SkyVector Cross(SkyVector b) => new(Y * b.Z - Z * b.Y, Z * b.X - X * b.Z, X * b.Y - Y * b.X);
    public double Length => Math.Sqrt(Dot(this));
    public SkyVector Normalized() { double l = Length; return l > 0 ? this * (1 / l) : this; }
}

/// <summary>How a 360° panorama (equirectangular, twice as wide as tall) wraps around the player: the middle column
/// faces heading 0 (+X), moving right in the picture turns clockwise seen from above (as panorama viewers do), the top
/// row is straight up. <c>turn</c> (degrees, anticlockwise from above) turns the whole sky.</summary>
public static class SkyDirections
{
    public static SkyVector FromUv(double u, double v, double turnDegrees = 0)
    {
        double azimuth = turnDegrees * Math.PI / 180 - 2 * Math.PI * (u - 0.5), elevation = Math.PI / 2 - Math.PI * v;
        return new(Math.Cos(elevation) * Math.Cos(azimuth), Math.Cos(elevation) * Math.Sin(azimuth), Math.Sin(elevation));
    }

    public static (double U, double V) ToUv(SkyVector direction, double turnDegrees = 0)
    {
        double azimuth = Math.Atan2(direction.Y, direction.X), elevation = Math.Atan2(direction.Z, Math.Sqrt(direction.X * direction.X + direction.Y * direction.Y));
        double u = 0.5 - (azimuth - turnDegrees * Math.PI / 180) / (2 * Math.PI);
        return (u - Math.Floor(u), 0.5 - elevation / Math.PI);
    }
}

/// <summary>A panorama being painted by triangles seen from the centre (the player): each pixel takes the nearest
/// triangle along its direction. Used to turn a sky model into a panorama and to preview a native sky.</summary>
internal sealed class SkyCanvas
{
    public SkyCanvas(int width, int height)
    {
        Width = width; Height = height;
        Color = new float[width * height * 3];
        Depth = new double[width * height];
        Array.Fill(Depth, double.PositiveInfinity);
        _columnCos = new double[width]; _columnSin = new double[width]; _rowCos = new double[height]; _rowSin = new double[height];
        for (int x = 0; x < width; x++)
        {
            SkyVector d = SkyDirections.FromUv((x + 0.5) / width, 0.5);
            _columnCos[x] = d.X; _columnSin[x] = d.Y;
        }
        for (int y = 0; y < height; y++)
        {
            double elevation = Math.PI / 2 - Math.PI * (y + 0.5) / height;
            _rowCos[y] = Math.Cos(elevation); _rowSin[y] = Math.Sin(elevation);
        }
    }

    public int Width { get; }
    public int Height { get; }
    public float[] Color { get; }
    public double[] Depth { get; }
    private readonly double[] _columnCos, _columnSin, _rowCos, _rowSin;

    public bool Covered(int index) => !double.IsPositiveInfinity(Depth[index]);

    /// <summary>Paints the triangle a, b, c (points relative to the centre); <paramref name="shade"/> gets the weights of
    /// a, b and c where a pixel's direction meets the triangle. The nearest triangle wins, or with
    /// <paramref name="inOrder"/> the last one drawn (as the game draws a sky).</summary>
    public void Draw(SkyVector a, SkyVector b, SkyVector c, Func<double, double, double, (float R, float G, float B)> shade, bool inOrder = false)
    {
        double volume = a.Dot(b.Cross(c));
        if (Math.Abs(volume) < 1e-12) return; // edge-on from the centre, or through it
        SkyVector nab = a.Cross(b), nbc = b.Cross(c), nca = c.Cross(a);
        foreach ((int x0, int x1, int y0, int y1) in Bounds(a, b, c))
            for (int y = y0; y <= y1; y++)
                for (int xx = x0; xx <= x1; xx++)
                {
                    int x = ((xx % Width) + Width) % Width, i = y * Width + x;
                    SkyVector d = new(_rowCos[y] * _columnCos[x], _rowCos[y] * _columnSin[x], _rowSin[y]);
                    // Weights of the point where the ray meets the triangle's plane (positive inside the triangle).
                    double wa = nbc.Dot(d) / volume, wb = nca.Dot(d) / volume, wc = nab.Dot(d) / volume;
                    double sum = wa + wb + wc;
                    if (wa < -1e-9 || wb < -1e-9 || wc < -1e-9 || sum <= 1e-12) continue;
                    wa /= sum; wb /= sum; wc /= sum;
                    double depth = (a * wa + b * wb + c * wc).Length;
                    if (!inOrder && depth >= Depth[i]) continue;
                    Depth[i] = depth;
                    var (r, g, bl) = shade(wa, wb, wc);
                    Color[i * 3] = r; Color[i * 3 + 1] = g; Color[i * 3 + 2] = bl;
                }
    }

    /// <summary>Pixel ranges that hold the triangle: its corners and edges (great-circle arcs) sampled, the shortest arc
    /// of columns that covers them, every column when it holds a pole.</summary>
    private IEnumerable<(int X0, int X1, int Y0, int Y1)> Bounds(SkyVector a, SkyVector b, SkyVector c)
    {
        List<(double U, double V)> points = [];
        foreach ((SkyVector p, SkyVector q) in new[] { (a, b), (b, c), (c, a) })
            for (int k = 0; k < 16; k++)
                points.Add(SkyDirections.ToUv(p + (q - p) * (k / 16.0)));
        double vMin = points.Min(p => p.V), vMax = points.Max(p => p.V);
        bool north = Contains(a, b, c, new(0, 0, 1)), south = Contains(a, b, c, new(0, 0, -1));
        if (north) vMin = 0;
        if (south) vMax = 1;
        int y0 = Math.Clamp((int)Math.Floor(vMin * Height) - 1, 0, Height - 1), y1 = Math.Clamp((int)Math.Ceiling(vMax * Height) + 1, 0, Height - 1);
        if (north || south) { yield return (0, Width - 1, y0, y1); yield break; }
        var us = points.Select(p => p.U).Order().ToList();
        double gap = 1 - us[^1] + us[0], start = us[0], end = us[^1];
        for (int k = 1; k < us.Count; k++)
            if (us[k] - us[k - 1] > gap) { gap = us[k] - us[k - 1]; start = us[k]; end = us[k - 1] + 1; }
        if (end - start > 0.5) { yield return (0, Width - 1, y0, y1); yield break; }
        yield return ((int)Math.Floor(start * Width) - 1, (int)Math.Ceiling(end * Width) + 1, y0, y1);
    }

    private static bool Contains(SkyVector a, SkyVector b, SkyVector c, SkyVector d)
    {
        double volume = a.Dot(b.Cross(c));
        double wa = b.Cross(c).Dot(d) / volume, wb = c.Cross(a).Dot(d) / volume, wc = a.Cross(b).Dot(d) / volume;
        return wa >= 0 && wb >= 0 && wc >= 0 && wa + wb + wc > 0;
    }

    /// <summary>Gives every pixel no triangle reached the colour of the nearest painted ones (a dome without a floor
    /// continues its lowest colours down), working on a coarse grid so large gaps fill quickly.</summary>
    public void FillGaps()
    {
        if (Enumerable.Range(0, Width * Height).All(Covered)) return;
        if (!Enumerable.Range(0, Width * Height).Any(Covered)) throw new InvalidDataException("The model covers no direction seen from its centre.");
        int cw = Math.Max(8, Width / 8), ch = Math.Max(4, Height / 8);
        var sum = new double[cw * ch * 3]; var count = new int[cw * ch];
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                int i = y * Width + x; if (!Covered(i)) continue;
                int j = (y * ch / Height) * cw + x * cw / Width;
                for (int k = 0; k < 3; k++) sum[j * 3 + k] += Color[i * 3 + k];
                count[j]++;
            }
        var coarse = new float[cw * ch * 3]; var known = new bool[cw * ch];
        for (int j = 0; j < cw * ch; j++)
            if (count[j] > 0) { known[j] = true; for (int k = 0; k < 3; k++) coarse[j * 3 + k] = (float)(sum[j * 3 + k] / count[j]); }
        while (known.Any(k => !k))
        {
            var next = (bool[])known.Clone();
            for (int y = 0; y < ch; y++)
                for (int x = 0; x < cw; x++)
                {
                    int j = y * cw + x; if (known[j]) continue;
                    double r = 0, g = 0, b = 0; int n = 0;
                    foreach ((int dx, int dy) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1) })
                    {
                        int ny = y + dy; if (ny < 0 || ny >= ch) continue;
                        int nj = ny * cw + ((x + dx + cw) % cw);
                        if (!known[nj]) continue;
                        r += coarse[nj * 3]; g += coarse[nj * 3 + 1]; b += coarse[nj * 3 + 2]; n++;
                    }
                    if (n == 0) continue;
                    coarse[j * 3] = (float)(r / n); coarse[j * 3 + 1] = (float)(g / n); coarse[j * 3 + 2] = (float)(b / n); next[j] = true;
                }
            known = next;
        }
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                int i = y * Width + x; if (Covered(i)) continue;
                int j = (y * ch / Height) * cw + x * cw / Width;
                for (int k = 0; k < 3; k++) Color[i * 3 + k] = coarse[j * 3 + k];
            }
    }
}
