using System.Buffers.Binary;

namespace Spyro.Editor.Core.Universal;

/// <summary>A dragon rescue is filmed from world positions. The statue (class 0xFA) carries the scene's first camera in
/// its properties: position (game units) at +0x00, then roll, pitch and heading (4096ths of a turn) at +0x0C..+0x14;
/// Spyro stands +0x2C game units from the statue in heading +0x28 (+0x30 is another heading of the scene). The rescue stream ends with the camera's track, one such
/// 24-byte record per frame, starting with that same camera (header word 7 = where the track starts, word 8 = its
/// length). A statue placed somewhere else takes both along: positions move with the statue and turn about it by the
/// change in its heading, and headings turn with it. Unmoved statues keep their bytes.</summary>
public static class NativeDragonRescueCamera
{
    public const int StatueClass = 0xFA;
    private const int Length = 0x34, Reach = 0x4000, Record = 24;
    private static readonly int[] Headings = [0x14, 0x28, 0x30];

    private sealed record Turn(int Ox, int Oy, int Oz, int X, int Y, int Z, int Delta)
    {
        public bool None => (X, Y, Z) == (Ox, Oy, Oz) && Delta == 0;
        public void Camera(Span<byte> at)
        {
            int cx = I(at, 0) - Ox, cy = I(at, 4) - Oy, cz = I(at, 8) - Oz;
            if (Math.Abs(cx) > Reach || Math.Abs(cy) > Reach || Math.Abs(cz) > Reach || I(at, 0x14) is < 0 or > 0x1000)
                throw new InvalidDataException("A dragon rescue camera is not where the game keeps it.");
            double a = Delta * Math.PI / 128, cos = Math.Cos(a), sin = Math.Sin(a);
            W(at, 0, X + (int)Math.Round(cx * cos - cy * sin));
            W(at, 4, Y + (int)Math.Round(cx * sin + cy * cos));
            W(at, 8, Z + cz);
            Heading(at, 0x14);
        }
        public void Heading(Span<byte> at, int offset) => W(at, offset, (I(at, offset) + Delta * 16) & 0xFFF);
    }

    private static Turn For(ReadOnlySpan<byte> sourceRow, int x, int y, int z, int yaw) =>
        new(I(sourceRow, 0x0C), I(sourceRow, 0x10), I(sourceRow, 0x14), x, y, z, (yaw - sourceRow[0x46]) & 0xFF);

    /// <param name="sourceRow">The statue's original row (position at +0x0C, heading at +0x46).</param>
    public static byte[] Move(byte[] properties, ReadOnlySpan<byte> sourceRow, int x, int y, int z, int yaw)
    {
        var turn = For(sourceRow, x, y, z, yaw);
        if (turn.None) return properties;
        if (properties.Length < Length || Headings.Any(h => I(properties, h) is < 0 or >= 0x1000))
            throw new InvalidDataException("A dragon statue's rescue camera is missing.");
        byte[] moved = properties.ToArray();
        turn.Camera(moved);
        foreach (int h in Headings.Skip(1)) turn.Heading(moved, h);
        return moved;
    }

    /// <summary>The camera track of a rescue stream.</summary>
    public static (int Offset, int Length) Track(ReadOnlySpan<byte> stream)
    {
        if (stream.Length < 36 || I(stream, 4) != 36) throw new InvalidDataException("A dragon rescue stream header changed.");
        int at = I(stream, 28), length = I(stream, 32);
        if (at < 36 || length < Record || length % Record != 0 || at + length > stream.Length)
            throw new InvalidDataException("A dragon rescue stream has no camera track.");
        return (at, length);
    }

    /// <summary>True when the stream films this statue (its track starts at the statue's camera).</summary>
    public static bool Films(ReadOnlySpan<byte> stream, ReadOnlySpan<byte> statueProperties)
    {
        var (at, _) = Track(stream);
        return statueProperties.Length >= Length && stream.Slice(at, Record).SequenceEqual(statueProperties[..Record]);
    }

    public static byte[] MoveStream(byte[] stream, ReadOnlySpan<byte> sourceRow, int x, int y, int z, int yaw)
    {
        var turn = For(sourceRow, x, y, z, yaw);
        if (turn.None) return stream;
        var (at, length) = Track(stream);
        byte[] moved = stream.ToArray();
        for (int r = at; r < at + length; r += Record) turn.Camera(moved.AsSpan(r, Record));
        return moved;
    }

    /// <summary>The scene's two model packages in a rescue stream (header words 3-6: the dragon's, then Spyro's).</summary>
    public static (int Offset, int Length)[] Models(ReadOnlySpan<byte> stream)
    {
        Track(stream);
        var models = new[] { (I(stream, 12), I(stream, 16)), (I(stream, 20), I(stream, 24)) };
        int length = stream.Length;
        if (models.Any(m => m.Item1 < 36 || m.Item2 <= 0 || m.Item1 + m.Item2 > length))
            throw new InvalidDataException("A dragon rescue stream's models are not where the game reads them.");
        return models;
    }

    /// <summary>The model packages of every rescue stream in a level (subfiles 4 on), as copies with their level-data
    /// offsets: they are textured from the level's own VRAM, so a level whose VRAM changed imports their regions.</summary>
    public static IReadOnlyList<(int Offset, byte[] Package)> Scenes(UniversalLevelData level)
    {
        var sf = level.Subfiles();
        List<(int, byte[])> scenes = [];
        for (int n = 4; n < sf.Count; n++)
            foreach (var (at, length) in Models(level.Bytes.AsSpan(sf[n].Offset, sf[n].Length)))
                scenes.Add((sf[n].Offset + at, level.Bytes.AsSpan(sf[n].Offset + at, length).ToArray()));
        return scenes;
    }

    /// <summary>True when two streams differ at most in their camera tracks and their models' texture descriptors.</summary>
    public static bool SameButCamera(ReadOnlySpan<byte> original, ReadOnlySpan<byte> written)
    {
        if (original.Length != written.Length) return false;
        var (at, length) = Track(original);
        if (Track(written) != (at, length)) return false;
        byte[] a = original.ToArray(), b = written.ToArray();
        a.AsSpan(at, length).Clear(); b.AsSpan(at, length).Clear();
        foreach (var (offset, size) in Models(original))
            foreach (int f in UniversalAssetImport.TextureDescriptorOffsets(original.Slice(offset, size)))
            { a.AsSpan(offset + f, 12).Clear(); b.AsSpan(offset + f, 12).Clear(); }
        return a.AsSpan().SequenceEqual(b);
    }

    private static int I(ReadOnlySpan<byte> b, int at) => BinaryPrimitives.ReadInt32LittleEndian(b[at..]);
    private static void W(Span<byte> b, int at, int v) => BinaryPrimitives.WriteInt32LittleEndian(b[at..], v);
}
