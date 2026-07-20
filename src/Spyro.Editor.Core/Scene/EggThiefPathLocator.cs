using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Scene;

/// <summary>
/// Locates the native PathData owned by retail egg-thief class 0x0021.
/// The owner-specific wrapper is deliberately kept here while the decoded
/// NativeMobyPath/NativePathNode model remains reusable by other actor classes.
/// </summary>
public static class EggThiefPathLocator
{
    public const ushort EggThiefNativeClass = 0x0021;
    public const int RecordStride = 0x58;
    public const int PathHeaderBytes = 0x08;
    public const int PathNodeBytes = 0x10;
    public const int EggThiefPathOffsetInProperties = 0x5C;
    private const int EntrySubfileDirectoryBytes = 0x40;
    private const int MaximumPathNodes = 64;

    public static IReadOnlyList<NativeMobyPath> Locate(string sourceImagePath, LevelDefinition level)
    {
        if (!File.Exists(sourceImagePath))
            throw new FileNotFoundException("Missing source disc image.", sourceImagePath);
        if (!level.HasSourceTable)
            throw new InvalidOperationException($"{level.DisplayName} does not have a mapped native Moby table.");

        DiscLayout layout = DiscImage.DetectLayout(sourceImagePath);
        using FileStream stream = File.OpenRead(sourceImagePath);
        DiscFileRecord wad = DiscImage.FindRootFileRecord(stream, layout, IsWadFileName);
        return Locate(stream, layout, wad, level);
    }

    public static IReadOnlyList<NativeMobyPath> Locate(
        string sourceImagePath,
        IEnumerable<LevelDefinition> levels)
    {
        if (!File.Exists(sourceImagePath))
            throw new FileNotFoundException("Missing source disc image.", sourceImagePath);

        DiscLayout layout = DiscImage.DetectLayout(sourceImagePath);
        using FileStream stream = File.OpenRead(sourceImagePath);
        DiscFileRecord wad = DiscImage.FindRootFileRecord(stream, layout, IsWadFileName);
        List<NativeMobyPath> paths = [];
        foreach (LevelDefinition level in levels)
            paths.AddRange(Locate(stream, layout, wad, level));
        return paths;
    }

    private static IReadOnlyList<NativeMobyPath> Locate(
        FileStream stream,
        DiscLayout layout,
        DiscFileRecord wad,
        LevelDefinition level)
    {
        long tableWadOffset = ParseOffset(level.SourceTableWadOffset);
        long tableRelativeOffset = ParseOffset(level.SourceTableRelativeOffset);
        long tableByteLength = checked((long)level.SourceRecordCount * RecordStride);

        byte[] entryPair = DiscImage.ReadFileBytes(stream, layout, wad.Lba, level.SourceWadEntry * 8L, 8);
        long entryWadOffset = BinaryPrimitives.ReadUInt32LittleEndian(entryPair.AsSpan(0, 4));
        int entryByteLength = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(entryPair.AsSpan(4, 4)));
        if (entryWadOffset <= 0 || entryByteLength < EntrySubfileDirectoryBytes)
            throw new InvalidOperationException($"{level.DisplayName} has an invalid WAD entry {level.SourceWadEntry}.");
        if (entryWadOffset + tableRelativeOffset != tableWadOffset)
            throw new InvalidOperationException($"{level.DisplayName}'s mapped Moby table is not relative to its source WAD entry.");
        if (tableRelativeOffset < 0 || tableRelativeOffset + tableByteLength > entryByteLength)
            throw new InvalidOperationException($"{level.DisplayName}'s mapped Moby table is outside its source WAD entry.");

        (long sceneRelativeOffset, int sceneByteLength) = LocateSceneSubfile(
            stream,
            layout,
            wad.Lba,
            entryWadOffset,
            tableRelativeOffset,
            tableByteLength,
            level.DisplayName);
        long sceneWadOffset = checked(entryWadOffset + sceneRelativeOffset);

        List<NativeMobyPath> paths = [];
        for (int trueIndex = 0; trueIndex < level.SourceRecordCount; trueIndex++)
        {
            long recordWadOffset = checked(tableWadOffset + ((long)trueIndex * RecordStride));
            byte[] record = DiscImage.ReadFileBytes(stream, layout, wad.Lba, recordWadOffset, RecordStride);
            if (!IsEggThief(level.Key, record))
                continue;

            ushort nativeClass = BinaryPrimitives.ReadUInt16LittleEndian(record.AsSpan(0x36, 2));
            uint propertiesPointer = BinaryPrimitives.ReadUInt32LittleEndian(record.AsSpan(0, 4));
            ValidateSceneRange(propertiesPointer, 4, sceneByteLength, level.DisplayName, trueIndex, "properties pointer");
            long propertiesWadOffset = checked(sceneWadOffset + propertiesPointer);
            uint pathPointer = BinaryPrimitives.ReadUInt32LittleEndian(
                DiscImage.ReadFileBytes(stream, layout, wad.Lba, propertiesWadOffset, 4));
            if (pathPointer != propertiesPointer + EggThiefPathOffsetInProperties)
            {
                throw new InvalidOperationException(
                    $"{level.DisplayName} T{trueIndex}'s class-0x0021 path pointer 0x{pathPointer:X} " +
                    $"does not match the verified m_Props+0x{EggThiefPathOffsetInProperties:X} layout.");
            }

            ValidateSceneRange(pathPointer, PathHeaderBytes, sceneByteLength, level.DisplayName, trueIndex, "path header");
            long pathWadOffset = checked(sceneWadOffset + pathPointer);
            byte[] header = DiscImage.ReadFileBytes(stream, layout, wad.Lba, pathWadOffset, PathHeaderBytes);
            int nodeCount = header[0];
            int currentNode = header[1];
            short reversed = BinaryPrimitives.ReadInt16LittleEndian(header.AsSpan(6, 2));
            if (nodeCount is < 1 or > MaximumPathNodes)
                throw new InvalidOperationException($"{level.DisplayName} T{trueIndex} has invalid path node count {nodeCount}.");
            if (currentNode >= nodeCount)
                throw new InvalidOperationException($"{level.DisplayName} T{trueIndex} has invalid current path node {currentNode}/{nodeCount}.");
            if (reversed is not -1 and not 1)
                throw new InvalidOperationException($"{level.DisplayName} T{trueIndex} has unexpected path direction value {reversed}.");

            int nodesByteLength = checked(nodeCount * PathNodeBytes);
            ValidateSceneRange(
                pathPointer + PathHeaderBytes,
                nodesByteLength,
                sceneByteLength,
                level.DisplayName,
                trueIndex,
                "path nodes");
            byte[] nodesBytes = DiscImage.ReadFileBytes(
                stream,
                layout,
                wad.Lba,
                pathWadOffset + PathHeaderBytes,
                nodesByteLength);
            List<NativePathNode> nodes = new(nodeCount);
            for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
            {
                ReadOnlySpan<byte> node = nodesBytes.AsSpan(nodeIndex * PathNodeBytes, PathNodeBytes);
                int rawX = BinaryPrimitives.ReadInt32LittleEndian(node[..4]);
                int rawY = BinaryPrimitives.ReadInt32LittleEndian(node.Slice(4, 4));
                int rawZ = BinaryPrimitives.ReadInt32LittleEndian(node.Slice(8, 4));
                if (!IsPlausibleCoordinate(rawX) || !IsPlausibleCoordinate(rawY) || !IsPlausibleCoordinate(rawZ))
                    throw new InvalidOperationException($"{level.DisplayName} T{trueIndex} path node {nodeIndex} has implausible coordinates.");
                nodes.Add(new NativePathNode(
                    nodeIndex,
                    rawX,
                    rawY,
                    rawZ,
                    BinaryPrimitives.ReadInt32LittleEndian(node.Slice(12, 4)),
                    pathWadOffset + PathHeaderBytes + ((long)nodeIndex * PathNodeBytes)));
            }

            byte[] pathBytes = new byte[PathHeaderBytes + nodesBytes.Length];
            header.CopyTo(pathBytes, 0);
            nodesBytes.CopyTo(pathBytes, PathHeaderBytes);
            paths.Add(new NativeMobyPath(
                LevelCatalog.NormalizeKey(level.Key),
                level.DisplayName,
                trueIndex,
                nativeClass,
                recordWadOffset,
                wad.Lba,
                sceneWadOffset,
                sceneByteLength,
                propertiesPointer,
                propertiesWadOffset,
                pathPointer,
                pathWadOffset,
                header,
                Convert.ToHexString(SHA256.HashData(pathBytes)).ToLowerInvariant(),
                nodes));
        }

        return paths;
    }

    private static (long RelativeOffset, int ByteLength) LocateSceneSubfile(
        FileStream stream,
        DiscLayout layout,
        int wadLba,
        long entryWadOffset,
        long tableRelativeOffset,
        long tableByteLength,
        string levelName)
    {
        byte[] directory = DiscImage.ReadFileBytes(
            stream,
            layout,
            wadLba,
            entryWadOffset,
            EntrySubfileDirectoryBytes);
        List<(long Offset, int Length)> matches = [];
        for (int offset = 0; offset < directory.Length; offset += 8)
        {
            long subfileOffset = BinaryPrimitives.ReadUInt32LittleEndian(directory.AsSpan(offset, 4));
            int subfileLength = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(directory.AsSpan(offset + 4, 4)));
            if (subfileOffset <= tableRelativeOffset &&
                tableRelativeOffset + tableByteLength <= subfileOffset + subfileLength)
            {
                matches.Add((subfileOffset, subfileLength));
            }
        }

        if (matches.Count != 1)
            throw new InvalidOperationException($"{levelName} did not resolve exactly one scene subfile for its Moby table.");
        return matches[0];
    }

    private static bool IsEggThief(string levelKey, byte[] record)
    {
        ushort nativeClass = BinaryPrimitives.ReadUInt16LittleEndian(record.AsSpan(0x36, 2));
        int updateDistance = record[0x52];
        bool expectedUpdateDistance = updateDistance == 0x30 ||
            (string.Equals(LevelCatalog.NormalizeKey(levelKey), "alpineridge", StringComparison.Ordinal) && updateDistance == 0x20);
        return nativeClass == EggThiefNativeClass &&
            expectedUpdateDistance &&
            record[0x53] == 0x22 &&
            record[0x50] is 0x20 or 0x40;
    }

    private static void ValidateSceneRange(
        uint relativeOffset,
        int byteLength,
        int sceneByteLength,
        string levelName,
        int trueIndex,
        string label)
    {
        if (relativeOffset >= sceneByteLength || (long)relativeOffset + byteLength > sceneByteLength)
            throw new InvalidOperationException($"{levelName} T{trueIndex} {label} is outside the native scene subfile.");
    }

    private static bool IsPlausibleCoordinate(int value) => value is > -1_000_000 and < 1_000_000;

    private static bool IsWadFileName(string name) =>
        string.Equals(name, "WAD.WAD", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, "WAD", StringComparison.OrdinalIgnoreCase);

    private static long ParseOffset(string value)
    {
        string text = value.Trim();
        return text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? long.Parse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture)
            : long.Parse(text, CultureInfo.InvariantCulture);
    }
}
