using System.Buffers.Binary;

namespace Spyro.Editor.Core.Exporting;

internal readonly record struct NativeTerrainTextureRepresentativeColor(
    double Red,
    double Green,
    double Blue)
{
    public bool IsValid =>
        double.IsFinite(Red) &&
        double.IsFinite(Green) &&
        double.IsFinite(Blue) &&
        Red is >= 0 and <= 255 &&
        Green is >= 0 and <= 255 &&
        Blue is >= 0 and <= 255;
}

internal sealed record NativeTerrainTextureLowDetailReplacement(
    int TargetTextureId,
    NativeTerrainTextureRepresentativeColor TargetAppearance,
    NativeTerrainTextureRepresentativeColor DonorAppearance);

internal sealed record NativeTerrainTextureLowDetailCompanionPatch(
    byte[] Before,
    byte[] After,
    int LowDetailColorOffset,
    int LowDetailColorCount,
    int AffectedHighDetailFaceCount,
    int AffectedLowDetailCornerCount,
    int ChangedLowDetailColorCount,
    IReadOnlyList<int> TargetTextureIds)
{
    public bool HasChanges => ChangedLowDetailColorCount > 0 && !Before.AsSpan().SequenceEqual(After);
}

/// <summary>
/// The retail scene uses textured HP faces nearby and a separate, untextured
/// Gouraud-shaded LP mesh at distance. A texture-record transplant therefore
/// needs a source-bound LP color companion or the old level appearance returns
/// as soon as the HP sector drops out.
/// </summary>
internal static class NativeTerrainTextureLowDetailCompanion
{
    private const int SectorHeaderBytes = 28;
    private const double RatioRegularizer = 24.0;
    private const double MinimumRatio = 0.25;
    private const double MaximumRatio = 4.0;

    public static NativeTerrainTextureLowDetailCompanionPatch Build(
        byte[] sectorBytes,
        IReadOnlyDictionary<int, NativeTerrainTextureLowDetailReplacement> replacements)
    {
        ArgumentNullException.ThrowIfNull(sectorBytes);
        ArgumentNullException.ThrowIfNull(replacements);
        if (sectorBytes.Length < SectorHeaderBytes)
            throw new InvalidDataException("Native terrain sector header is truncated.");

        int numLpVertices = sectorBytes[16];
        int numLpColors = sectorBytes[17];
        int numLpFaces = sectorBytes[18];
        int numHpVertices = sectorBytes[20];
        int numHpColors = sectorBytes[21];
        int numHpFaces = sectorBytes[22];
        int expectedBytes = checked(
            (7 +
             numLpVertices +
             numLpColors +
             (numLpFaces * 2) +
             numHpVertices +
             (numHpColors * 2) +
             (numHpFaces * 4)) * 4);
        if (expectedBytes != sectorBytes.Length)
        {
            throw new InvalidDataException(
                $"Native terrain sector is {sectorBytes.Length:N0} bytes, but its count header describes {expectedBytes:N0} bytes.");
        }

        int lpVertexStart = SectorHeaderBytes;
        int lpColorStart = checked(lpVertexStart + (numLpVertices * 4));
        int lpFaceStart = checked(lpColorStart + (numLpColors * 4));
        int hpVertexStart = checked(lpFaceStart + (numLpFaces * 8));
        int hpColorStart = checked(hpVertexStart + (numHpVertices * 4));
        int hpFaceStart = checked(hpColorStart + (numHpColors * 8));

        byte[] before = numLpColors == 0
            ? []
            : sectorBytes.AsSpan(lpColorStart, numLpColors * 4).ToArray();
        byte[] after = before.ToArray();
        if (replacements.Count == 0 ||
            numLpVertices == 0 ||
            numLpColors == 0 ||
            numLpFaces == 0 ||
            numHpVertices == 0 ||
            numHpFaces == 0)
        {
            return new NativeTerrainTextureLowDetailCompanionPatch(
                before,
                after,
                lpColorStart,
                numLpColors,
                0,
                0,
                0,
                []);
        }

        for (int color = 0; color < numLpColors; color++)
        {
            byte command = before[(color * 4) + 3];
            if (command is not (0x00 or 0x30))
            {
                throw new InvalidDataException(
                    $"LP color {color} has command byte 0x{command:X2}; expected the native RGB0/RGB30 layout.");
            }
        }

        NativePoint[] lpVertices = ReadVertices(sectorBytes, lpVertexStart, numLpVertices);
        NativePoint[] hpVertices = ReadVertices(sectorBytes, hpVertexStart, numHpVertices);
        List<HighDetailFace> hpFaces = ReadHighDetailFaces(
            sectorBytes,
            hpFaceStart,
            numHpFaces,
            hpVertices);
        if (hpFaces.Count == 0)
        {
            throw new InvalidDataException(
                "Native terrain sector has no valid HP faces for its LP appearance companion.");
        }

        HighDetailFace[] affectedHpFaces = hpFaces
            .Where(face => replacements.ContainsKey(face.TextureId))
            .ToArray();
        if (affectedHpFaces.Length == 0)
        {
            return new NativeTerrainTextureLowDetailCompanionPatch(
                before,
                after,
                lpColorStart,
                numLpColors,
                0,
                0,
                0,
                []);
        }

        ColorAccumulator[] accumulators = Enumerable.Range(0, numLpColors)
            .Select(_ => new ColorAccumulator())
            .ToArray();
        int affectedLpCorners = 0;
        for (int faceIndex = 0; faceIndex < numLpFaces; faceIndex++)
        {
            int faceOffset = checked(lpFaceStart + (faceIndex * 8));
            uint vertexWord = ReadUInt32(sectorBytes, faceOffset);
            uint colorWord = ReadUInt32(sectorBytes, faceOffset + 4);
            int[] vertexIndexes = ReadPackedSixBitSlots(vertexWord);
            int[] colorIndexes = ReadPackedSixBitSlots(colorWord);
            for (int corner = 0; corner < 4; corner++)
            {
                int vertexIndex = vertexIndexes[corner];
                int colorIndex = colorIndexes[corner];
                if (vertexIndex < 0 || vertexIndex >= lpVertices.Length ||
                    colorIndex < 0 || colorIndex >= numLpColors)
                {
                    throw new InvalidDataException(
                        $"LP face {faceIndex} corner {corner} references vertex {vertexIndex} / color {colorIndex} outside this sector.");
                }

                HighDetailFace nearest = FindNearestHighDetailFace(lpVertices[vertexIndex], hpFaces);
                NativeRgb original = ReadRgb(before, colorIndex * 4);
                if (replacements.TryGetValue(
                        nearest.TextureId,
                        out NativeTerrainTextureLowDetailReplacement? replacement))
                {
                    NativeRgb recolored = Recolor(
                        original,
                        replacement.TargetAppearance,
                        replacement.DonorAppearance);
                    accumulators[colorIndex].Add(recolored, affected: true);
                    affectedLpCorners++;
                }
                else
                {
                    accumulators[colorIndex].Add(original, affected: false);
                }
            }
        }

        // Very small HP details can sit between every coarse LP vertex. Ensure
        // each affected HP region still contributes to the nearest LP color,
        // instead of leaving an unmistakable island of the old level color.
        foreach (HighDetailFace affectedFace in affectedHpFaces)
        {
            (int ColorIndex, NativePoint Point) nearestLp = FindNearestLowDetailColor(
                sectorBytes,
                lpFaceStart,
                numLpFaces,
                lpVertices,
                numLpColors,
                affectedFace.Center);
            NativeRgb original = ReadRgb(before, nearestLp.ColorIndex * 4);
            NativeTerrainTextureLowDetailReplacement replacement = replacements[affectedFace.TextureId];
            NativeRgb recolored = Recolor(
                original,
                replacement.TargetAppearance,
                replacement.DonorAppearance);
            accumulators[nearestLp.ColorIndex].Add(recolored, affected: true);
        }

        int changedColorCount = 0;
        for (int colorIndex = 0; colorIndex < accumulators.Length; colorIndex++)
        {
            ColorAccumulator accumulator = accumulators[colorIndex];
            if (accumulator.AffectedCount == 0)
                continue;

            NativeRgb recolored = accumulator.Average();
            int offset = colorIndex * 4;
            if (after[offset] == recolored.Red &&
                after[offset + 1] == recolored.Green &&
                after[offset + 2] == recolored.Blue)
            {
                continue;
            }

            after[offset] = recolored.Red;
            after[offset + 1] = recolored.Green;
            after[offset + 2] = recolored.Blue;
            changedColorCount++;
        }

        return new NativeTerrainTextureLowDetailCompanionPatch(
            before,
            after,
            lpColorStart,
            numLpColors,
            affectedHpFaces.Length,
            affectedLpCorners,
            changedColorCount,
            affectedHpFaces
                .Select(face => face.TextureId)
                .Distinct()
                .Order()
                .ToArray());
    }

    private static NativePoint[] ReadVertices(byte[] sectorBytes, int start, int count)
    {
        NativePoint[] result = new NativePoint[count];
        uint xyPosition = ReadUInt32(sectorBytes, 8);
        uint zPosition = ReadUInt32(sectorBytes, 12);
        ushort centerRadiusAndFlags = ReadUInt16(sectorBytes, 4);
        int sectorX = (int)((xyPosition >> 16) & 0xFFFF);
        int sectorY = (int)(xyPosition & 0xFFFF);
        int sectorZ = (int)((zPosition >> 14) & 0xFFFF) >> 2;
        bool flat = ((centerRadiusAndFlags >> 12) & 1) == 1;
        for (int index = 0; index < count; index++)
        {
            uint word = ReadUInt32(sectorBytes, start + (index * 4));
            int x = sectorX + (int)(((word >> 19) & 0x1FFC) >> 2);
            int y = sectorY + (int)(((word >> 8) & 0x1FFC) >> 2);
            int z = sectorZ + (int)(((word << 3) & 0x1FFC) >> 3);
            if (flat)
                z >>= 3;
            result[index] = new NativePoint(x, y, z);
        }
        return result;
    }

    private static List<HighDetailFace> ReadHighDetailFaces(
        byte[] sectorBytes,
        int start,
        int count,
        IReadOnlyList<NativePoint> hpVertices)
    {
        List<HighDetailFace> result = new(count);
        for (int faceIndex = 0; faceIndex < count; faceIndex++)
        {
            int offset = checked(start + (faceIndex * 16));
            int[] indexes =
            [
                sectorBytes[offset],
                sectorBytes[offset + 1],
                sectorBytes[offset + 2],
                sectorBytes[offset + 3]
            ];
            NativePoint[] points = indexes
                .Where(index => index >= 0 && index < hpVertices.Count)
                .Distinct()
                .Select(index => hpVertices[index])
                .ToArray();
            if (points.Length < 3)
                continue;

            int textureId = (int)(ReadUInt32(sectorBytes, offset + 8) & 0x7F);
            result.Add(HighDetailFace.FromPoints(faceIndex, textureId, points));
        }
        return result;
    }

    private static HighDetailFace FindNearestHighDetailFace(
        NativePoint point,
        IReadOnlyList<HighDetailFace> faces)
    {
        HighDetailFace nearest = faces[0];
        double nearestScore = ScorePointToFace(point, nearest);
        for (int index = 1; index < faces.Count; index++)
        {
            HighDetailFace candidate = faces[index];
            double score = ScorePointToFace(point, candidate);
            if (score < nearestScore ||
                (score == nearestScore && candidate.FaceIndex < nearest.FaceIndex))
            {
                nearest = candidate;
                nearestScore = score;
            }
        }
        return nearest;
    }

    private static (int ColorIndex, NativePoint Point) FindNearestLowDetailColor(
        byte[] sectorBytes,
        int lpFaceStart,
        int numLpFaces,
        IReadOnlyList<NativePoint> lpVertices,
        int numLpColors,
        NativePoint target)
    {
        int bestColor = -1;
        NativePoint bestPoint = default;
        double bestDistance = double.MaxValue;
        for (int faceIndex = 0; faceIndex < numLpFaces; faceIndex++)
        {
            int faceOffset = checked(lpFaceStart + (faceIndex * 8));
            int[] vertexIndexes = ReadPackedSixBitSlots(ReadUInt32(sectorBytes, faceOffset));
            int[] colorIndexes = ReadPackedSixBitSlots(ReadUInt32(sectorBytes, faceOffset + 4));
            for (int corner = 0; corner < 4; corner++)
            {
                int vertexIndex = vertexIndexes[corner];
                int colorIndex = colorIndexes[corner];
                if (vertexIndex < 0 || vertexIndex >= lpVertices.Count ||
                    colorIndex < 0 || colorIndex >= numLpColors)
                {
                    continue;
                }

                NativePoint point = lpVertices[vertexIndex];
                double distance = DistanceSquared(point, target);
                if (distance < bestDistance ||
                    (distance == bestDistance && colorIndex < bestColor))
                {
                    bestDistance = distance;
                    bestColor = colorIndex;
                    bestPoint = point;
                }
            }
        }

        if (bestColor < 0)
            throw new InvalidDataException("Native LP faces do not reference any valid color/vertex pair.");
        return (bestColor, bestPoint);
    }

    private static double ScorePointToFace(NativePoint point, HighDetailFace face)
    {
        double dx = point.X < face.MinX
            ? face.MinX - point.X
            : point.X > face.MaxX
                ? point.X - face.MaxX
                : 0;
        double dy = point.Y < face.MinY
            ? face.MinY - point.Y
            : point.Y > face.MaxY
                ? point.Y - face.MaxY
                : 0;
        double dz = point.Z < face.MinZ
            ? face.MinZ - point.Z
            : point.Z > face.MaxZ
                ? point.Z - face.MaxZ
                : 0;
        double centerTieBreak = DistanceSquared(point, face.Center) * 0.000001;
        return (dx * dx) + (dy * dy) + (dz * dz) + centerTieBreak;
    }

    private static NativeRgb Recolor(
        NativeRgb original,
        NativeTerrainTextureRepresentativeColor target,
        NativeTerrainTextureRepresentativeColor donor)
    {
        if (!target.IsValid || !donor.IsValid)
            throw new InvalidDataException("Native texture representative color is not finite RGB.");

        return new NativeRgb(
            TransformChannel(original.Red, target.Red, donor.Red),
            TransformChannel(original.Green, target.Green, donor.Green),
            TransformChannel(original.Blue, target.Blue, donor.Blue));
    }

    private static byte TransformChannel(byte value, double target, double donor)
    {
        double ratio = Math.Clamp(
            (donor + RatioRegularizer) / (target + RatioRegularizer),
            MinimumRatio,
            MaximumRatio);
        return (byte)Math.Clamp((int)Math.Round(value * ratio), 0, 255);
    }

    private static NativeRgb ReadRgb(byte[] bytes, int offset) =>
        new(bytes[offset], bytes[offset + 1], bytes[offset + 2]);

    private static int[] ReadPackedSixBitSlots(uint word) =>
        [(int)((word >> 26) & 0x3F), (int)((word >> 20) & 0x3F), (int)((word >> 14) & 0x3F), (int)((word >> 8) & 0x3F)];

    private static double DistanceSquared(NativePoint a, NativePoint b)
    {
        double dx = a.X - b.X;
        double dy = a.Y - b.Y;
        double dz = a.Z - b.Z;
        return (dx * dx) + (dy * dy) + (dz * dz);
    }

    private static ushort ReadUInt16(byte[] bytes, int offset) =>
        BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2));

    private static uint ReadUInt32(byte[] bytes, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));

    private readonly record struct NativePoint(int X, int Y, int Z);

    private readonly record struct NativeRgb(byte Red, byte Green, byte Blue);

    private sealed record HighDetailFace(
        int FaceIndex,
        int TextureId,
        int MinX,
        int MaxX,
        int MinY,
        int MaxY,
        int MinZ,
        int MaxZ,
        NativePoint Center)
    {
        public static HighDetailFace FromPoints(
            int faceIndex,
            int textureId,
            IReadOnlyList<NativePoint> points) =>
            new(
                faceIndex,
                textureId,
                points.Min(point => point.X),
                points.Max(point => point.X),
                points.Min(point => point.Y),
                points.Max(point => point.Y),
                points.Min(point => point.Z),
                points.Max(point => point.Z),
                new NativePoint(
                    (int)Math.Round(points.Average(point => point.X)),
                    (int)Math.Round(points.Average(point => point.Y)),
                    (int)Math.Round(points.Average(point => point.Z))));
    }

    private sealed class ColorAccumulator
    {
        private long _red;
        private long _green;
        private long _blue;

        public int Count { get; private set; }
        public int AffectedCount { get; private set; }

        public void Add(NativeRgb color, bool affected)
        {
            _red += color.Red;
            _green += color.Green;
            _blue += color.Blue;
            Count++;
            if (affected)
                AffectedCount++;
        }

        public NativeRgb Average()
        {
            if (Count <= 0)
                return default;
            return new NativeRgb(
                (byte)Math.Clamp((int)Math.Round((double)_red / Count), 0, 255),
                (byte)Math.Clamp((int)Math.Round((double)_green / Count), 0, 255),
                (byte)Math.Clamp((int)Math.Round((double)_blue / Count), 0, 255));
        }
    }
}
