using System.Buffers.Binary;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Reads the two native high-detail terrain color banks directly from the
/// untouched source disc. Cached overlays intentionally keep only an averaged
/// near color, so a runtime-safe texture swap must recover and bind both the
/// near and fade colors from source bytes.
/// </summary>
public static class NativeTerrainTextureVisualInspector
{
    private const int WadLba = 37;
    private const int SectorHeaderBytes = 28;
    private const int HpFaceBytes = 16;

    public static bool TryInspectSourceImage(
        string sourceImagePath,
        string sourceLevelKey,
        TerrainPolygon face,
        out TerrainTextureVisualEdit visual,
        out string error)
    {
        visual = null!;
        error = "";
        if (string.IsNullOrWhiteSpace(sourceImagePath) || !File.Exists(sourceImagePath))
        {
            error = "The selected source BIN is missing.";
            return false;
        }
        if (!IsSourceMappedHpFace(face, out error))
            return false;

        try
        {
            DiscLayout layout = DiscImage.DetectLayout(sourceImagePath);
            using FileStream stream = File.OpenRead(sourceImagePath);
            return TryInspect(
                (offset, length) => DiscImage.ReadFileBytes(stream, layout, WadLba, offset, length),
                sourceLevelKey,
                face.RuntimeKey,
                face.SectorOffset,
                face.FaceOffset,
                out visual,
                out error);
        }
        catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or IOException or OverflowException)
        {
            error = $"Could not read native tint data for {face.RuntimeKey}: {ex.Message}";
            return false;
        }
    }

    public static bool TryInspectLogicalWad(
        byte[] logicalWad,
        string sourceLevelKey,
        string sourceRuntimeKey,
        int sourceSectorOffset,
        int sourceFaceOffset,
        out TerrainTextureVisualEdit visual,
        out string error)
    {
        ArgumentNullException.ThrowIfNull(logicalWad);
        return TryInspect(
            (offset, length) => ReadLogicalWadBytes(logicalWad, offset, length),
            sourceLevelKey,
            sourceRuntimeKey,
            sourceSectorOffset,
            sourceFaceOffset,
            out visual,
            out error);
    }

    public static bool TryGetWritableColorSlotCapacity(
        string sourceImagePath,
        TerrainPolygon targetFace,
        out int capacity,
        out string error)
    {
        capacity = 0;
        error = "";
        if (string.IsNullOrWhiteSpace(sourceImagePath) || !File.Exists(sourceImagePath))
        {
            error = "The selected source BIN is missing.";
            return false;
        }
        if (!IsSourceMappedHpFace(targetFace, out error))
            return false;

        try
        {
            DiscLayout layout = DiscImage.DetectLayout(sourceImagePath);
            using FileStream stream = File.OpenRead(sourceImagePath);
            if (!TryFindWritableColorSlots(
                    (offset, length) => DiscImage.ReadFileBytes(stream, layout, WadLba, offset, length),
                    targetFace.SectorOffset,
                    targetFace.FaceOffset,
                    requiredCount: 0,
                    out int[] slots,
                    out error))
            {
                return false;
            }

            capacity = slots.Length;
            return true;
        }
        catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or IOException or OverflowException)
        {
            error = $"Could not inspect private tint capacity for {targetFace.RuntimeKey}: {ex.Message}";
            return false;
        }
    }

    public static bool TryFindWritableColorSlotsInLogicalWad(
        byte[] logicalWad,
        int targetSectorOffset,
        int targetFaceOffset,
        int requiredCount,
        out int[] slots,
        out string error)
    {
        ArgumentNullException.ThrowIfNull(logicalWad);
        return TryFindWritableColorSlots(
            (offset, length) => ReadLogicalWadBytes(logicalWad, offset, length),
            targetSectorOffset,
            targetFaceOffset,
            requiredCount,
            out slots,
            out error);
    }

    private static bool TryInspect(
        Func<long, int, byte[]> readWadBytes,
        string sourceLevelKey,
        string sourceRuntimeKey,
        int sourceSectorOffset,
        int sourceFaceOffset,
        out TerrainTextureVisualEdit visual,
        out string error)
    {
        visual = null!;
        if (!TryReadSector(readWadBytes, sourceSectorOffset, out byte[] sector, out SectorLayout layout, out error))
            return false;
        if (!TryGetHpFaceRelativeOffset(layout, sourceSectorOffset, sourceFaceOffset, out int faceRelativeOffset, out error))
            return false;

        uint word3 = BinaryPrimitives.ReadUInt32LittleEndian(sector.AsSpan(faceRelativeOffset + 8, 4));
        int textureId = (int)(word3 & 0x7Fu);
        TerrainTextureVisualCorner[] corners = new TerrainTextureVisualCorner[4];
        for (int corner = 0; corner < corners.Length; corner++)
        {
            int colorIndex = sector[faceRelativeOffset + 4 + corner];
            if (colorIndex < 0 || colorIndex >= layout.NumHpColors)
            {
                error = $"Native face {sourceRuntimeKey} uses out-of-range high-detail color slot {colorIndex}.";
                return false;
            }

            NativeTerrainHpColorOffsets colorOffsets =
                NativeTerrainHpColorLayout.GetColorOffsets(
                    layout.HpColorStart,
                    layout.NumHpColors,
                    colorIndex);
            corners[corner] = new TerrainTextureVisualCorner(
                ReadColor(sector, colorOffsets.Table2Offset),
                ReadColor(sector, colorOffsets.Table1Offset));
        }

        visual = new TerrainTextureVisualEdit(
            textureId,
            sourceLevelKey ?? "",
            sourceRuntimeKey ?? "",
            sourceSectorOffset,
            sourceFaceOffset,
            corners[0],
            corners[1],
            corners[2],
            corners[3],
            $"native texture {textureId} visual from {sourceRuntimeKey}");
        error = "";
        return true;
    }

    private static bool TryFindWritableColorSlots(
        Func<long, int, byte[]> readWadBytes,
        int targetSectorOffset,
        int targetFaceOffset,
        int requiredCount,
        out int[] slots,
        out string error)
    {
        slots = [];
        if (requiredCount < 0)
        {
            error = "The required native tint-slot count cannot be negative.";
            return false;
        }
        if (!TryReadSector(readWadBytes, targetSectorOffset, out byte[] sector, out SectorLayout layout, out error))
            return false;
        if (!TryGetHpFaceRelativeOffset(layout, targetSectorOffset, targetFaceOffset, out int targetRelativeOffset, out error))
            return false;

        bool[] usedByOtherFace = new bool[layout.NumHpColors];
        for (int faceIndex = 0; faceIndex < layout.NumHpFaces; faceIndex++)
        {
            int faceOffset = layout.HpFaceStart + (faceIndex * HpFaceBytes);
            if (faceOffset == targetRelativeOffset)
                continue;

            for (int corner = 0; corner < 4; corner++)
            {
                int colorIndex = sector[faceOffset + 4 + corner];
                if (colorIndex < 0 || colorIndex >= usedByOtherFace.Length)
                {
                    error = $"The target sector contains out-of-range high-detail color slot {colorIndex}.";
                    return false;
                }
                usedByOtherFace[colorIndex] = true;
            }
        }

        List<int> available = new();
        for (int corner = 0; corner < 4; corner++)
        {
            int colorIndex = sector[targetRelativeOffset + 4 + corner];
            if (colorIndex >= 0 && colorIndex < usedByOtherFace.Length && !usedByOtherFace[colorIndex] && !available.Contains(colorIndex))
                available.Add(colorIndex);
        }
        for (int colorIndex = 0; colorIndex < usedByOtherFace.Length; colorIndex++)
        {
            if (!usedByOtherFace[colorIndex] && !available.Contains(colorIndex))
                available.Add(colorIndex);
        }

        if (requiredCount > available.Count)
        {
            error = $"The target face needs {requiredCount} private native tint slot(s), but its sector has {available.Count}.";
            return false;
        }

        slots = requiredCount == 0 ? available.ToArray() : available.Take(requiredCount).ToArray();
        error = "";
        return true;
    }

    private static bool TryReadSector(
        Func<long, int, byte[]> readWadBytes,
        int sectorOffset,
        out byte[] sector,
        out SectorLayout layout,
        out string error)
    {
        sector = [];
        layout = default;
        if (sectorOffset < 0)
        {
            error = "The native terrain sector offset is missing.";
            return false;
        }

        byte[] header = readWadBytes(sectorOffset, SectorHeaderBytes);
        int numLpVertices = header[16];
        int numLpColors = header[17];
        int numLpFaces = header[18];
        int numHpVertices = header[20];
        int numHpColors = header[21];
        int numHpFaces = header[22];
        int sizeWords = 7 + numLpVertices + numLpColors + (numLpFaces * 2) + numHpVertices + (numHpColors * 2) + (numHpFaces * 4);
        int sizeBytes = checked(sizeWords * 4);
        if (sizeBytes < SectorHeaderBytes || sizeBytes > 0x40000 || numHpColors <= 0 || numHpFaces <= 0)
        {
            error = $"The native terrain sector at 0x{sectorOffset:X} has an invalid high-detail layout.";
            return false;
        }

        int hpColorStart = SectorHeaderBytes + ((numLpVertices + numLpColors + (numLpFaces * 2) + numHpVertices) * 4);
        int hpFaceStart = hpColorStart + (numHpColors * 8);
        if (hpFaceStart < SectorHeaderBytes || hpFaceStart + (numHpFaces * HpFaceBytes) > sizeBytes)
        {
            error = $"The native terrain sector at 0x{sectorOffset:X} has out-of-range color or face tables.";
            return false;
        }

        sector = readWadBytes(sectorOffset, sizeBytes);
        layout = new SectorLayout(sizeBytes, numHpColors, numHpFaces, hpColorStart, hpFaceStart);
        error = "";
        return true;
    }

    private static bool TryGetHpFaceRelativeOffset(
        SectorLayout layout,
        int sectorOffset,
        int faceOffset,
        out int relativeOffset,
        out string error)
    {
        relativeOffset = faceOffset - sectorOffset;
        int hpFaceEnd = layout.HpFaceStart + (layout.NumHpFaces * HpFaceBytes);
        if (relativeOffset < layout.HpFaceStart || relativeOffset + HpFaceBytes > hpFaceEnd ||
            ((relativeOffset - layout.HpFaceStart) % HpFaceBytes) != 0)
        {
            error = $"Face offset 0x{faceOffset:X} is not one high-detail face in sector 0x{sectorOffset:X}.";
            return false;
        }

        error = "";
        return true;
    }

    private static bool IsSourceMappedHpFace(TerrainPolygon face, out string error)
    {
        if (!string.Equals(face.Detail, "hp", StringComparison.OrdinalIgnoreCase))
        {
            error = $"Face {face.RuntimeKey} is not high-detail terrain.";
            return false;
        }
        if (face.SectorOffset < 0 || face.FaceOffset < 0)
        {
            error = $"Face {face.RuntimeKey} is missing source-bound sector/face offsets.";
            return false;
        }

        error = "";
        return true;
    }

    private static byte[] ReadLogicalWadBytes(byte[] logicalWad, long offset, int length)
    {
        if (offset < 0 || length < 0 || offset + length > logicalWad.LongLength)
            throw new EndOfStreamException($"Logical WAD read 0x{offset:X}+0x{length:X} is outside the source data.");
        return logicalWad.AsSpan(checked((int)offset), length).ToArray();
    }

    private static ColorRgba ReadColor(byte[] bytes, int offset) =>
        ColorRgba.FromRgb(bytes[offset], bytes[offset + 1], bytes[offset + 2]);

    private readonly record struct SectorLayout(
        int SizeBytes,
        int NumHpColors,
        int NumHpFaces,
        int HpColorStart,
        int HpFaceStart);
}
