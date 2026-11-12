using System.Buffers.Binary;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.Core.Sky;

/// <summary>Paints a native sky below the horizon in one colour (a level's ground colour).
/// <para>Why: the game cuts the tiles nearest the camera into sixteen pieces and the next ones into four (USA face pass
/// 0x80026D00..0x80026D30: a corner nearer than 0x140 depth, or the four depths adding up to less than 0x2000), and where
/// a sixteen-piece tile meets a four-piece one the extra corners of the finer tile do not land exactly on the coarser
/// tile's edge on screen. The hairline gaps that leaves (measured in the emulator: a generated level of 128-unit tiles,
/// 23-28 a frame; 256-unit tiles, 5; Artisans' own ground, 3) show whatever is behind the ground. Below the horizon that
/// is the sky block's background colour (bytes 4-6, the colour the screen is cleared to: the game's skies reach only a
/// little below the horizon), e.g. Artisans' light green (80,184,64), Dry Canyon's orange. Painted the ground's colour,
/// a gap no longer shows the sky.</para>
/// <para>The background colour is set, and a sky triangle colour every corner of which lies below
/// <paramref name="fromDegrees"/> is repainted, faded in over the next few degrees down so the horizon keeps its own
/// colours where far terrain does not hide it (colours are indexed per corner, <see cref="NativeSkyPreview"/> has the
/// layout). Nothing else in the block changes.</para></summary>
public static class NativeSkyBelow
{
    /// <summary>Elevation (degrees) from which the sky starts to take the ground's colour, and where it is all ground.</summary>
    public const double FromDegrees = -2, FullDegrees = -8;

    /// <summary>The sky block with every colour below the horizon moved to <paramref name="ground"/>; how many colour
    /// entries changed and how many were left because corners above and below the horizon share them.</summary>
    public static (byte[] Block, int Painted, int Shared) Paint(byte[] block, ColorRgba ground, double fromDegrees = FromDegrees, double fullDegrees = FullDegrees)
    {
        if (!Exporting.Spyro1SkyBlockAnalyzer.IsValidStandaloneSkyBlock(block))
            throw new InvalidDataException("This is not a native sky block.");
        byte[] result = (byte[])block.Clone();
        int parts = BinaryPrimitives.ReadInt32LittleEndian(block.AsSpan(8));
        int painted = 0, shared = 0;
        for (int p = 0; p < parts; p++)
        {
            int at = 4 + BinaryPrimitives.ReadInt32LittleEndian(block.AsSpan(12 + 4 * p));
            short S(int o) => BinaryPrimitives.ReadInt16LittleEndian(block.AsSpan(at + o));
            ushort U(int o) => BinaryPrimitives.ReadUInt16LittleEndian(block.AsSpan(at + o));
            int ox = S(8), oy = S(10), oz = S(14), vertices = U(12), triangles = U(16), colors = U(18);
            int vt = at + 24, ct = vt + 4 * vertices, pt = ct + 4 * colors;
            if (pt + 8 * triangles > block.Length) throw new InvalidDataException("A sky part runs past its block.");
            double Elevation(int index)
            {
                uint w = BinaryPrimitives.ReadUInt32LittleEndian(block.AsSpan(vt + 4 * index));
                int vx = ox - (int)((w >> 10) & 0x7FF), vy = oy - (int)(w & 0x3FF), vz = oz + (int)(w >> 21);
                // World axes: X = vz, Y = -vx, Z = -vy (see NativeSkyPreview).
                return Math.Atan2(-vy, Math.Sqrt((double)vz * vz + (double)vx * vx)) * 180 / Math.PI;
            }
            // Per colour entry: the highest corner that uses it.
            var highest = new double[colors];
            Array.Fill(highest, double.NegativeInfinity);
            for (int t = 0; t < triangles; t++)
            {
                uint v = BinaryPrimitives.ReadUInt32LittleEndian(block.AsSpan(pt + 8 * t));
                uint c = BinaryPrimitives.ReadUInt32LittleEndian(block.AsSpan(pt + 8 * t + 4));
                int[] vs = [(int)(v & 0x3FC) / 4, (int)((v >> 10) & 0x3FC) / 4, (int)(v >> 20) / 4];
                int[] cs = [(int)(c & 0x3FC) / 4, (int)((c >> 10) & 0x3FC) / 4, (int)(c >> 20) / 4];
                for (int k = 0; k < 3; k++)
                {
                    if (vs[k] >= vertices || cs[k] >= colors) throw new InvalidDataException("A sky triangle names a corner or colour its part does not have.");
                    highest[cs[k]] = Math.Max(highest[cs[k]], Elevation(vs[k]));
                }
            }
            for (int i = 0; i < colors; i++)
            {
                if (double.IsNegativeInfinity(highest[i]) || highest[i] >= fromDegrees)
                {
                    if (!double.IsNegativeInfinity(highest[i]) && highest[i] >= fromDegrees && LowestUse(i) < fromDegrees) shared++;
                    continue;
                }
                double share = Math.Clamp((fromDegrees - highest[i]) / (fromDegrees - fullDegrees), 0, 1);
                int o = ct + 4 * i;
                result[o] = Mix(block[o], ground.R, share); result[o + 1] = Mix(block[o + 1], ground.G, share); result[o + 2] = Mix(block[o + 2], ground.B, share);
                painted++;
            }
            double LowestUse(int colour)
            {
                double low = double.PositiveInfinity;
                for (int t = 0; t < triangles; t++)
                {
                    uint v = BinaryPrimitives.ReadUInt32LittleEndian(block.AsSpan(pt + 8 * t));
                    uint c = BinaryPrimitives.ReadUInt32LittleEndian(block.AsSpan(pt + 8 * t + 4));
                    int[] vs = [(int)(v & 0x3FC) / 4, (int)((v >> 10) & 0x3FC) / 4, (int)(v >> 20) / 4];
                    int[] cs = [(int)(c & 0x3FC) / 4, (int)((c >> 10) & 0x3FC) / 4, (int)(c >> 20) / 4];
                    for (int k = 0; k < 3; k++) if (cs[k] == colour) low = Math.Min(low, Elevation(vs[k]));
                }
                return low;
            }
        }
        // The background: what the screen shows where no sky triangle reaches, which is most of the sky below the horizon.
        result[4] = ground.R; result[5] = ground.G; result[6] = ground.B;
        return (result, painted, shared);
    }

    private static byte Mix(byte from, byte to, double share) => (byte)Math.Round(from + (to - from) * share);
}
