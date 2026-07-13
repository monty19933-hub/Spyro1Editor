using System.Buffers.Binary;
using System.Globalization;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Exporting;

public sealed record DragonRescueCameraData(
    int DragonTrueIndex,
    int PedestalTrueIndex,
    int NativeDragonIndex,
    int NativePedestalIndex,
    long SceneLinkWadOffset,
    long CameraDataWadOffset,
    int CameraRawX,
    int CameraRawY,
    int CameraRawZ,
    int CameraAngleX,
    int CameraAngleY,
    int CameraAngleZ,
    int DragonNameIndex,
    int CutsceneIndex,
    long CutsceneCameraTrackWadOffset,
    int CutsceneCameraTrackByteLength,
    int CutsceneCameraFrameCount);

public static class DragonRescueCameraLocator
{
    private const int WadLba = 37;
    private const int RecordStride = 0x58;
    private const int SceneLinkLength = 0x28;
    private const int CameraDataLength = 0x44;
    private const long MaximumCameraDistanceSquared = 5_000_000_000L;

    public static IReadOnlyDictionary<int, DragonRescueCameraData> Locate(string sourceImagePath, LevelDefinition level)
    {
        if (!File.Exists(sourceImagePath))
            throw new FileNotFoundException("Missing source disc image.", sourceImagePath);

        DiscLayout layout = DiscImage.DetectLayout(sourceImagePath);
        using FileStream stream = File.OpenRead(sourceImagePath);
        return Locate(stream, layout, level);
    }

    internal static IReadOnlyDictionary<int, DragonRescueCameraData> Locate(
        FileStream stream,
        DiscLayout layout,
        LevelDefinition level)
    {
        if (!level.HasSourceTable)
            return new Dictionary<int, DragonRescueCameraData>();

        long tableWadOffset = ParseOffset(level.SourceTableWadOffset);
        List<byte[]> rows = new(level.SourceRecordCount);
        for (int trueIndex = 0; trueIndex < level.SourceRecordCount; trueIndex++)
        {
            rows.Add(DiscImage.ReadFileBytes(
                stream,
                layout,
                WadLba,
                tableWadOffset + ((long)trueIndex * RecordStride),
                RecordStride));
        }

        HashSet<int> dragonTrueIndexes = rows
            .Select((row, trueIndex) => (row, trueIndex))
            .Where(item => IsDragonActor(item.row))
            .Select(item => item.trueIndex)
            .ToHashSet();
        if (dragonTrueIndexes.Count == 0)
            return new Dictionary<int, DragonRescueCameraData>();

        HashSet<int> pedestalTrueIndexes = rows
            .Select((row, trueIndex) => (row, trueIndex))
            .Where(item => IsDragonPedestal(item.row))
            .Select(item => item.trueIndex)
            .ToHashSet();

        byte[] entryHeader = DiscImage.ReadFileBytes(stream, layout, WadLba, level.SourceWadEntry * 8L, 8);
        long entryWadOffset = BinaryPrimitives.ReadUInt32LittleEndian(entryHeader.AsSpan(0, 4));
        int entryLength = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(entryHeader.AsSpan(4, 4)));
        byte[] entry = DiscImage.ReadFileBytes(stream, layout, WadLba, entryWadOffset, entryLength);
        int catalogPrefixRows = GetCatalogPrefixRows(stream, layout, level, tableWadOffset);

        List<SceneLinkCandidate> sceneLinks = FindSceneLinks(entry, entryWadOffset, rows, dragonTrueIndexes, pedestalTrueIndexes, catalogPrefixRows);
        Dictionary<int, SceneLinkCandidate> sceneLinkByDragon = RequireOnePerDragon(level, dragonTrueIndexes, sceneLinks);
        List<CameraCandidate> cameras = FindCameraData(entry, entryWadOffset, rows, sceneLinks);
        Dictionary<int, CameraCandidate> cameraByDragon = RequireOneCameraPerDragon(level, dragonTrueIndexes, cameras);

        Dictionary<int, DragonRescueCameraData> result = new();
        foreach (int dragonTrueIndex in dragonTrueIndexes.Order())
        {
            SceneLinkCandidate link = sceneLinkByDragon[dragonTrueIndex];
            CameraCandidate camera = cameraByDragon[dragonTrueIndex];
            CutsceneCameraTrack cutsceneTrack = LocateCutsceneCameraTrack(
                stream,
                layout,
                level,
                entry,
                entryWadOffset,
                camera);
            result.Add(dragonTrueIndex, new DragonRescueCameraData(
                DragonTrueIndex: dragonTrueIndex,
                PedestalTrueIndex: link.PedestalTrueIndex,
                NativeDragonIndex: link.NativeDragonIndex,
                NativePedestalIndex: link.NativePedestalIndex,
                SceneLinkWadOffset: link.WadOffset,
                CameraDataWadOffset: camera.WadOffset,
                CameraRawX: camera.RawX,
                CameraRawY: camera.RawY,
                CameraRawZ: camera.RawZ,
                CameraAngleX: camera.AngleX,
                CameraAngleY: camera.AngleY,
                CameraAngleZ: camera.AngleZ,
                DragonNameIndex: camera.DragonNameIndex,
                CutsceneIndex: camera.CutsceneIndex,
                CutsceneCameraTrackWadOffset: cutsceneTrack.WadOffset,
                CutsceneCameraTrackByteLength: cutsceneTrack.ByteLength,
                CutsceneCameraFrameCount: cutsceneTrack.FrameCount));
        }

        return result;
    }

    private static List<SceneLinkCandidate> FindSceneLinks(
        byte[] entry,
        long entryWadOffset,
        IReadOnlyList<byte[]> rows,
        IReadOnlySet<int> dragonTrueIndexes,
        IReadOnlySet<int> pedestalTrueIndexes,
        int catalogPrefixRows)
    {
        List<SceneLinkCandidate> candidates = [];
        for (int offset = 0; offset <= entry.Length - SceneLinkLength; offset += 4)
        {
            int nativeDragonIndex = ReadInt32(entry, offset);
            int nativePedestalIndex = ReadInt32(entry, offset + 4);
            if (!HasZeroSceneLinkTail(entry, offset))
                continue;

            int dragonTrueIndex = nativeDragonIndex + catalogPrefixRows;
            int pedestalTrueIndex = nativePedestalIndex + catalogPrefixRows;
            if (!dragonTrueIndexes.Contains(dragonTrueIndex) ||
                !pedestalTrueIndexes.Contains(pedestalTrueIndex) ||
                !IsDragonActor(rows[dragonTrueIndex]) ||
                !IsDragonPedestal(rows[pedestalTrueIndex]))
            {
                continue;
            }

            candidates.Add(new SceneLinkCandidate(
                dragonTrueIndex,
                pedestalTrueIndex,
                nativeDragonIndex,
                nativePedestalIndex,
                entryWadOffset + offset));
        }

        return candidates
            .DistinctBy(candidate => (candidate.DragonTrueIndex, candidate.PedestalTrueIndex, candidate.WadOffset))
            .ToList();
    }

    private static int GetCatalogPrefixRows(
        FileStream stream,
        DiscLayout layout,
        LevelDefinition level,
        long tableWadOffset)
    {
        int directCount = BinaryPrimitives.ReadInt32LittleEndian(
            DiscImage.ReadFileBytes(stream, layout, WadLba, tableWadOffset - 4, 4));
        if (directCount == level.SourceRecordCount)
            return 0;

        int oneRowLaterCount = BinaryPrimitives.ReadInt32LittleEndian(
            DiscImage.ReadFileBytes(stream, layout, WadLba, tableWadOffset + RecordStride - 4, 4));
        if (oneRowLaterCount == level.SourceRecordCount - 1)
            return 1;

        throw new InvalidOperationException(
            $"{level.DisplayName}'s dragon table prefix could not be resolved from source counts {directCount} and {oneRowLaterCount}.");
    }

    private static List<CameraCandidate> FindCameraData(
        byte[] entry,
        long entryWadOffset,
        IReadOnlyList<byte[]> rows,
        IReadOnlyList<SceneLinkCandidate> sceneLinks)
    {
        Dictionary<int, SceneLinkCandidate> linksByNativePedestal = sceneLinks
            .GroupBy(link => link.NativePedestalIndex)
            .Where(group => group.Select(link => link.DragonTrueIndex).Distinct().Count() == 1)
            .ToDictionary(group => group.Key, group => group.First());
        List<CameraCandidate> candidates = [];

        for (int offset = 0; offset <= entry.Length - CameraDataLength; offset += 4)
        {
            int nativePedestalIndex = ReadInt32(entry, offset + 0x20);
            if (!linksByNativePedestal.TryGetValue(nativePedestalIndex, out SceneLinkCandidate? link))
                continue;

            int angleX = ReadInt32(entry, offset + 0x0C);
            int angleY = ReadInt32(entry, offset + 0x10);
            int angleZ = ReadInt32(entry, offset + 0x14);
            int cutsceneIndex = ReadInt32(entry, offset + 0x18);
            int dragonNameIndex = ReadInt32(entry, offset + 0x38);
            if (!IsCameraAngle(angleX) || !IsCameraAngle(angleY) || !IsCameraAngle(angleZ) ||
                cutsceneIndex is < 0 or >= 32 ||
                dragonNameIndex is < 0 or >= 80 ||
                ReadInt32(entry, offset + 0x3C) != 0x5622)
            {
                continue;
            }

            int rawX = ReadInt32(entry, offset);
            int rawY = ReadInt32(entry, offset + 4);
            int rawZ = ReadInt32(entry, offset + 8);
            if (!IsPlausibleCoordinate(rawX) || !IsPlausibleCoordinate(rawY) || !IsPlausibleCoordinate(rawZ))
                continue;

            byte[] dragonRow = rows[link.DragonTrueIndex];
            long dx = (long)rawX - ReadInt32(dragonRow, 0x0C);
            long dy = (long)rawY - ReadInt32(dragonRow, 0x10);
            long dz = (long)rawZ - ReadInt32(dragonRow, 0x14);
            if ((dx * dx) + (dy * dy) + (dz * dz) > MaximumCameraDistanceSquared)
                continue;

            candidates.Add(new CameraCandidate(
                link.DragonTrueIndex,
                link.PedestalTrueIndex,
                entryWadOffset + offset,
                rawX,
                rawY,
                rawZ,
                angleX,
                angleY,
                angleZ,
                dragonNameIndex,
                cutsceneIndex));
        }

        return candidates
            .DistinctBy(candidate => (candidate.DragonTrueIndex, candidate.WadOffset))
            .ToList();
    }

    private static CutsceneCameraTrack LocateCutsceneCameraTrack(
        FileStream stream,
        DiscLayout layout,
        LevelDefinition level,
        byte[] levelEntry,
        long levelEntryWadOffset,
        CameraCandidate camera)
    {
        int nestedHeaderOffset = checked(0x20 + (camera.CutsceneIndex * 8));
        if (nestedHeaderOffset < 0 || nestedHeaderOffset + 8 > levelEntry.Length)
        {
            throw new InvalidOperationException(
                $"{level.DisplayName} dragon T{camera.DragonTrueIndex} cutscene index {camera.CutsceneIndex} is outside the level archive header.");
        }

        int cutsceneRelativeOffset = checked((int)ReadUInt32(levelEntry, nestedHeaderOffset));
        int cutsceneByteLength = checked((int)ReadUInt32(levelEntry, nestedHeaderOffset + 4));
        if (cutsceneRelativeOffset <= 0 ||
            cutsceneByteLength < 0x24 ||
            (long)cutsceneRelativeOffset + cutsceneByteLength > levelEntry.Length)
        {
            throw new InvalidOperationException(
                $"{level.DisplayName} dragon T{camera.DragonTrueIndex} cutscene {camera.CutsceneIndex} has an invalid nested archive range.");
        }

        long cutsceneWadOffset = levelEntryWadOffset + cutsceneRelativeOffset;
        byte[] cutsceneHeader = DiscImage.ReadFileBytes(stream, layout, WadLba, cutsceneWadOffset, 0x24);
        int cutsceneHeaderLength = checked((int)ReadUInt32(cutsceneHeader, 0x04));
        int cameraTrackRelativeOffset = checked((int)ReadUInt32(cutsceneHeader, 0x1C));
        int cameraTrackByteLength = checked((int)ReadUInt32(cutsceneHeader, 0x20));
        if (cutsceneHeaderLength != 0x24 ||
            cameraTrackRelativeOffset < cutsceneHeaderLength ||
            cameraTrackByteLength <= 0 ||
            cameraTrackByteLength % 0x18 != 0 ||
            (long)cameraTrackRelativeOffset + cameraTrackByteLength > cutsceneByteLength)
        {
            throw new InvalidOperationException(
                $"{level.DisplayName} dragon T{camera.DragonTrueIndex} cutscene {camera.CutsceneIndex} does not contain one valid 24-byte camera-keyframe track.");
        }

        return new CutsceneCameraTrack(
            cutsceneWadOffset + cameraTrackRelativeOffset,
            cameraTrackByteLength,
            cameraTrackByteLength / 0x18);
    }

    private static Dictionary<int, SceneLinkCandidate> RequireOnePerDragon(
        LevelDefinition level,
        IReadOnlySet<int> dragonTrueIndexes,
        IReadOnlyList<SceneLinkCandidate> candidates)
    {
        Dictionary<int, SceneLinkCandidate> result = new();
        foreach (int dragonTrueIndex in dragonTrueIndexes.Order())
        {
            List<SceneLinkCandidate> matches = candidates
                .Where(candidate => candidate.DragonTrueIndex == dragonTrueIndex)
                .ToList();
            if (matches.Count != 1)
            {
                throw new InvalidOperationException(
                    $"{level.DisplayName} dragon T{dragonTrueIndex} has {matches.Count} packed dragon/pedestal scene-link records; expected exactly one.");
            }

            result.Add(dragonTrueIndex, matches[0]);
        }

        return result;
    }

    private static Dictionary<int, CameraCandidate> RequireOneCameraPerDragon(
        LevelDefinition level,
        IReadOnlySet<int> dragonTrueIndexes,
        IReadOnlyList<CameraCandidate> candidates)
    {
        Dictionary<int, CameraCandidate> result = new();
        foreach (int dragonTrueIndex in dragonTrueIndexes.Order())
        {
            List<CameraCandidate> matches = candidates
                .Where(candidate => candidate.DragonTrueIndex == dragonTrueIndex)
                .ToList();
            if (matches.Count != 1)
            {
                throw new InvalidOperationException(
                    $"{level.DisplayName} dragon T{dragonTrueIndex} has {matches.Count} packed rescue-camera records; expected exactly one.");
            }

            result.Add(dragonTrueIndex, matches[0]);
        }

        return result;
    }

    private static bool HasZeroSceneLinkTail(byte[] bytes, int offset)
    {
        for (int fieldOffset = 8; fieldOffset < SceneLinkLength; fieldOffset += 4)
        {
            if (ReadInt32(bytes, offset + fieldOffset) != 0)
                return false;
        }

        return true;
    }

    private static bool IsDragonActor(byte[] row)
    {
        return row[0x50] is 0x20 or 0x3C &&
            row[0x36] == 0xFA &&
            row[0x37] == 0x00 &&
            row[0x4F] == 0x00 &&
            row[0x52] == 0x10 &&
            row[0x53] == 0xFF;
    }

    private static bool IsDragonPedestal(byte[] row)
    {
        return row[0x50] == 0x20 &&
            row[0x36] is 0x4B or 0x4C or 0x4D &&
            row[0x37] == 0x01 &&
            row[0x4F] == 0x00 &&
            row[0x52] == 0x10 &&
            row[0x53] == 0xFF;
    }

    private static bool IsCameraAngle(int value) => value is >= 0 and <= 0x1000;

    private static bool IsPlausibleCoordinate(int value) => Math.Abs((long)value) <= 2_000_000;

    private static int ReadInt32(byte[] bytes, int offset)
    {
        return BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset, 4));
    }

    private static uint ReadUInt32(byte[] bytes, int offset)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
    }

    private static long ParseOffset(string value)
    {
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return long.Parse(value[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return long.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
    }

    private sealed record SceneLinkCandidate(
        int DragonTrueIndex,
        int PedestalTrueIndex,
        int NativeDragonIndex,
        int NativePedestalIndex,
        long WadOffset);

    private sealed record CameraCandidate(
        int DragonTrueIndex,
        int PedestalTrueIndex,
        long WadOffset,
        int RawX,
        int RawY,
        int RawZ,
        int AngleX,
        int AngleY,
        int AngleZ,
        int DragonNameIndex,
        int CutsceneIndex);

    private sealed record CutsceneCameraTrack(long WadOffset, int ByteLength, int FrameCount);
}
