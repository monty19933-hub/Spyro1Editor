using System.Buffers.Binary;
using Spyro.Editor.Core.Editing;

namespace Spyro.Editor.Core.Sky;

/// <summary>A native sky triangle in world axes (X east, Y north, Z up) with its corner colours.</summary>
public readonly record struct NativeSkyTriangle(SkyVector A, SkyVector B, SkyVector C, Rgba32 ColorA, Rgba32 ColorB, Rgba32 ColorC, int Part);

/// <summary>Reads a native sky block back into triangles and draws it as a panorama: what the game shows around the
/// player (see <see cref="NativeSkyBuilder"/> for the layout).</summary>
public static class NativeSkyPreview
{
    public static IReadOnlyList<NativeSkyTriangle> Triangles(byte[] block)
    {
        if (!Exporting.Spyro1SkyBlockAnalyzer.IsValidStandaloneSkyBlock(block))
            throw new InvalidDataException("This is not a native sky block.");
        int parts = BinaryPrimitives.ReadInt32LittleEndian(block.AsSpan(8));
        List<NativeSkyTriangle> result = [];
        for (int p = 0; p < parts; p++)
        {
            int at = 4 + BinaryPrimitives.ReadInt32LittleEndian(block.AsSpan(12 + 4 * p));
            short S(int o) => BinaryPrimitives.ReadInt16LittleEndian(block.AsSpan(at + o));
            ushort U(int o) => BinaryPrimitives.ReadUInt16LittleEndian(block.AsSpan(at + o));
            int ox = S(8), oy = S(10), oz = S(14), vertices = U(12), triangles = U(16), colors = U(18);
            int vt = at + 24, ct = vt + 4 * vertices, pt = ct + 4 * colors;
            SkyVector Corner(int offset)
            {
                uint w = BinaryPrimitives.ReadUInt32LittleEndian(block.AsSpan(vt + offset));
                int vx = ox - (int)((w >> 10) & 0x7FF), vy = oy - (int)(w & 0x3FF), vz = oz + (int)(w >> 21);
                return new(vz, -vx, -vy);
            }
            Rgba32 Color(int offset) => new(block[ct + offset], block[ct + offset + 1], block[ct + offset + 2], 255);
            for (int t = 0; t < triangles; t++)
            {
                uint v = BinaryPrimitives.ReadUInt32LittleEndian(block.AsSpan(pt + 8 * t));
                uint c = BinaryPrimitives.ReadUInt32LittleEndian(block.AsSpan(pt + 8 * t + 4));
                int v0 = (int)(v & 0x3FC), v1 = (int)((v >> 10) & 0x3FC), v2 = (int)(v >> 20);
                int c0 = (int)(c & 0x3FC), c1 = (int)((c >> 10) & 0x3FC), c2 = (int)(c >> 20);
                if (v2 >= 4 * vertices || v0 >= 4 * vertices || v1 >= 4 * vertices || c2 >= 4 * colors || c0 >= 4 * colors || c1 >= 4 * colors)
                    throw new InvalidDataException("A sky triangle names a corner or colour its part does not have.");
                result.Add(new(Corner(v0), Corner(v1), Corner(v2), Color(c0), Color(c1), Color(c2), p));
            }
        }
        return result;
    }

    /// <summary>The sky as a panorama picture <paramref name="width"/> pixels wide, drawn in the game's order (later
    /// triangles over earlier ones), with its background colour where no triangle reaches.</summary>
    public static SkyImage Render(byte[] block, int width = 1024)
    {
        var canvas = new SkyCanvas(width, width / 2);
        foreach (var t in Triangles(block))
            canvas.Draw(t.A, t.B, t.C, (wa, wb, wc) => (
                (float)(t.ColorA.R * wa + t.ColorB.R * wb + t.ColorC.R * wc),
                (float)(t.ColorA.G * wa + t.ColorB.G * wb + t.ColorC.G * wc),
                (float)(t.ColorA.B * wa + t.ColorB.B * wb + t.ColorC.B * wc)), inOrder: true);
        var pixels = new Rgba32[canvas.Width * canvas.Height];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = canvas.Covered(i)
                ? new(SkyPanorama.Byte(canvas.Color[i * 3]), SkyPanorama.Byte(canvas.Color[i * 3 + 1]), SkyPanorama.Byte(canvas.Color[i * 3 + 2]), 255)
                : new(block[4], block[5], block[6], 255);
        return new SkyImage(canvas.Width, canvas.Height, pixels);
    }
}
