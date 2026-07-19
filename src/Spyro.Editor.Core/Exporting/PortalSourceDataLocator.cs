using System.Buffers.Binary;
using System.Globalization;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Exporting;

public sealed record PortalSourcePoint(int X, int Y, int Z, long WadOffset);

public sealed record PortalPathSourceNode(
    int Index,
    int X,
    int Y,
    int Z,
    long WadOffset);

public sealed record PortalPathSourceRecord(
    long PropsWadOffset,
    long PathWadOffset,
    int NodeCount,
    int CurrentNode,
    int Reversed,
    IReadOnlyList<PortalPathSourceNode> Nodes);

public sealed record PortalSourceRecord(
    int Index,
    int DestinationLevelId,
    int PathMobyIndex,
    int SourcePathMobyTrueIndex,
    int WorldSectorIndex,
    int PointCount,
    int NormalX,
    int NormalY,
    int NormalZ,
    int CenterX,
    int CenterY,
    int CenterZ,
    long WadOffset,
    IReadOnlyList<PortalSourcePoint> Points,
    PortalPathSourceRecord Path);

public sealed record PortalSpecialSurfaceRecord(
    int Index,
    int Type,
    int DestinationLevelId,
    int PortalIndex,
    long WadOffset,
    int RelativeOffset,
    byte[] RawBytes)
{
    // The native record is shared by every special-surface kind.  These two
    // fields are portal destinations only when Type == 6; for damaging floors,
    // supercharge, electric floors, and the remaining native kinds they are
    // generic behavior parameters.
    public int Param1 => DestinationLevelId;
    public int Param2 => PortalIndex;
    public int ByteLength => RawBytes.Length;
    public string RawHex => Convert.ToHexString(RawBytes);
}

public sealed record NativeCollisionSurfaceTriangle(
    int TriangleIndex,
    int FlagByte,
    int SurfaceIndex,
    int SurfaceType,
    int Param1,
    int Param2,
    long TriangleWadOffset,
    long FlagWadOffset,
    PortalSourcePoint P1,
    PortalSourcePoint P2,
    PortalSourcePoint P3)
{
    public bool HasSpecialSurface => SurfaceIndex is >= 0 and < 63 && SurfaceType >= 0;
}

public sealed record NativeTerrainSurfaceSourceData(
    string LevelKey,
    string LevelName,
    long LevelDataWadOffset,
    int LevelDataByteLength,
    long SpecialSurfaceComponentWadOffset,
    long CollisionComponentWadOffset,
    PortalCollisionSourceLayout Collision,
    NativeTerrainSurfaceFlagCapacity FlagPromotionCapacity,
    IReadOnlyList<PortalSpecialSurfaceRecord> SpecialSurfaces,
    IReadOnlyList<NativeCollisionSurfaceTriangle> CollisionSurfaceTriangles);

public sealed record PortalTriggerTriangle(
    int TriangleIndex,
    int SurfaceIndex,
    int DestinationLevelId,
    int PortalIndex,
    int FlagByte,
    long WadOffset,
    PortalSourcePoint P1,
    PortalSourcePoint P2,
    PortalSourcePoint P3);

public sealed record PortalCollisionSourceLayout(
    int TriangleCount,
    int FlagCount,
    long BlockTreeWadOffset,
    int BlockTreeByteCapacity,
    long BlocksWadOffset,
    int BlocksByteCapacity,
    long TriangleTableWadOffset,
    long FlagsWadOffset);

public sealed record PortalSourceLevelData(
    string LevelKey,
    string LevelName,
    long LevelDataWadOffset,
    long EntryDataWadOffset,
    int EntryDataByteLength,
    long SpecialSurfaceComponentWadOffset,
    long CollisionComponentWadOffset,
    long PortalTableWadOffset,
    int SourceMobyIndexBias,
    PortalCollisionSourceLayout Collision,
    IReadOnlyList<PortalSourceRecord> Portals,
    IReadOnlyList<PortalSpecialSurfaceRecord> SpecialSurfaces,
    IReadOnlyList<PortalTriggerTriangle> TriggerTriangles,
    IReadOnlyList<NativeCollisionSurfaceTriangle> CollisionSurfaceTriangles);

public static class PortalSourceDataLocator
{
    private const int WadLba = 37;
    private const int LevelEntryHeaderLength = 0x20;

    public static PortalSourceLevelData Locate(string sourceImagePath, LevelDefinition level)
    {
        if (!File.Exists(sourceImagePath))
            throw new FileNotFoundException("Missing source disc image.", sourceImagePath);

        DiscLayout layout = DiscImage.DetectLayout(sourceImagePath);
        using FileStream stream = File.OpenRead(sourceImagePath);
        return Locate(stream, layout, level);
    }

    /// <summary>
    /// Reads only the native special-surface and collision components. Unlike
    /// the portal locator, this path does not require a resolved source/runtime
    /// moby-table bias, so it is valid for every playable level and flight.
    /// </summary>
    public static NativeTerrainSurfaceSourceData LocateTerrainSurfaces(string sourceImagePath, LevelDefinition level)
    {
        if (!File.Exists(sourceImagePath))
            throw new FileNotFoundException("Missing source disc image.", sourceImagePath);

        DiscLayout layout = DiscImage.DetectLayout(sourceImagePath);
        using FileStream stream = File.OpenRead(sourceImagePath);
        return LocateTerrainSurfaces(stream, layout, level);
    }

    internal static NativeTerrainSurfaceSourceData LocateTerrainSurfaces(
        FileStream stream,
        DiscLayout layout,
        LevelDefinition level)
    {
        if (level.SourceWadEntry < 0)
            throw new InvalidOperationException($"{level.DisplayName} does not have a mapped source WAD entry.");

        byte[] entryHeader = DiscImage.ReadFileBytes(stream, layout, WadLba, level.SourceWadEntry * 8L, 8);
        long entryWadOffset = ReadUInt32(entryHeader, 0);
        int entryByteLength = checked((int)ReadUInt32(entryHeader, 4));
        if (entryWadOffset <= 0 || entryByteLength < LevelEntryHeaderLength)
            throw new InvalidOperationException($"{level.DisplayName} has an invalid source WAD entry header.");

        byte[] levelHeader = DiscImage.ReadFileBytes(
            stream,
            layout,
            WadLba,
            entryWadOffset,
            LevelEntryHeaderLength);
        int levelDataRelativeOffset = checked((int)ReadUInt32(levelHeader, 0x08));
        int levelDataByteLength = checked((int)ReadUInt32(levelHeader, 0x0C));
        if (levelDataRelativeOffset < LevelEntryHeaderLength ||
            levelDataByteLength <= 0 ||
            (long)levelDataRelativeOffset + levelDataByteLength > entryByteLength)
        {
            throw new InvalidOperationException($"{level.DisplayName} does not contain one valid native level-data block.");
        }

        long levelDataWadOffset = entryWadOffset + levelDataRelativeOffset;
        byte[] levelData = DiscImage.ReadFileBytes(
            stream,
            layout,
            WadLba,
            levelDataWadOffset,
            levelDataByteLength);

        return ParseTerrainSurfaces(
            levelData,
            levelDataWadOffset,
            level.Key,
            level.DisplayName);
    }

    /// <summary>
    /// Parses one already-extracted native level-data block. This is the same
    /// structural path used by the disc-backed locator and allows atomic layout
    /// builders to verify their in-memory result before any BIN is written.
    /// </summary>
    public static NativeTerrainSurfaceSourceData ParseTerrainSurfaces(
        byte[] levelData,
        long levelDataWadOffset,
        string levelKey,
        string levelName)
    {
        ArgumentNullException.ThrowIfNull(levelData);
        levelKey ??= "";
        levelName = string.IsNullOrWhiteSpace(levelName) ? levelKey : levelName;

        int offset = 0;
        offset = AdvanceComponent(levelData, offset, levelName, "texture");
        offset = AdvanceComponent(levelData, offset, levelName, "environment");
        offset = AdvanceComponent(levelData, offset, levelName, "occlusion");

        int specialSurfaceComponentOffset = offset;
        int collisionComponentOffset = AdvanceComponent(levelData, offset, levelName, "special surface");
        IReadOnlyList<PortalSpecialSurfaceRecord> specialSurfaces = ReadSpecialSurfaces(
            levelData,
            specialSurfaceComponentOffset,
            levelDataWadOffset,
            levelName);
        (
            PortalCollisionSourceLayout collision,
            _,
            IReadOnlyList<NativeCollisionSurfaceTriangle> collisionSurfaceTriangles
        ) = ReadTriggerTriangles(
            levelData,
            collisionComponentOffset,
            levelDataWadOffset,
            levelName,
            specialSurfaces);
        if (!NativeTerrainSurfaceFlagPromoter.TryInspectCapacity(
                levelData,
                levelDataWadOffset,
                levelDataWadOffset + collisionComponentOffset,
                out NativeTerrainSurfaceFlagCapacity? flagPromotionCapacity,
                out string capacityReason) ||
            flagPromotionCapacity == null)
        {
            throw new InvalidOperationException(
                $"{levelName}'s bounded native surface-flag capacity could not be verified: {capacityReason}");
        }

        return new NativeTerrainSurfaceSourceData(
            levelKey,
            levelName,
            levelDataWadOffset,
            levelData.Length,
            levelDataWadOffset + specialSurfaceComponentOffset,
            levelDataWadOffset + collisionComponentOffset,
            collision,
            flagPromotionCapacity,
            specialSurfaces,
            collisionSurfaceTriangles);
    }

    internal static PortalSourceLevelData Locate(FileStream stream, DiscLayout layout, LevelDefinition level)
    {
        if (level.SourceWadEntry < 0)
            throw new InvalidOperationException($"{level.DisplayName} does not have a mapped source WAD entry.");

        byte[] entryHeader = DiscImage.ReadFileBytes(stream, layout, WadLba, level.SourceWadEntry * 8L, 8);
        long entryWadOffset = ReadUInt32(entryHeader, 0);
        int entryByteLength = checked((int)ReadUInt32(entryHeader, 4));
        if (entryWadOffset <= 0 || entryByteLength < LevelEntryHeaderLength)
            throw new InvalidOperationException($"{level.DisplayName} has an invalid source WAD entry header.");

        byte[] levelHeader = DiscImage.ReadFileBytes(
            stream,
            layout,
            WadLba,
            entryWadOffset,
            LevelEntryHeaderLength);
        int levelDataRelativeOffset = checked((int)ReadUInt32(levelHeader, 0x08));
        int levelDataByteLength = checked((int)ReadUInt32(levelHeader, 0x0C));
        if (levelDataRelativeOffset < LevelEntryHeaderLength ||
            levelDataByteLength <= 0 ||
            (long)levelDataRelativeOffset + levelDataByteLength > entryByteLength)
        {
            throw new InvalidOperationException($"{level.DisplayName} does not contain one valid native level-data block.");
        }

        int entryDataRelativeOffset = checked((int)ReadUInt32(levelHeader, 0x18));
        int entryDataByteLength = checked((int)ReadUInt32(levelHeader, 0x1C));
        if (entryDataRelativeOffset < LevelEntryHeaderLength ||
            entryDataByteLength <= 0 ||
            (long)entryDataRelativeOffset + entryDataByteLength > entryByteLength)
        {
            throw new InvalidOperationException($"{level.DisplayName} does not contain one valid native entry-scene block.");
        }

        long levelDataWadOffset = entryWadOffset + levelDataRelativeOffset;
        long entryDataWadOffset = entryWadOffset + entryDataRelativeOffset;
        byte[] levelData = DiscImage.ReadFileBytes(
            stream,
            layout,
            WadLba,
            levelDataWadOffset,
            levelDataByteLength);
        int sourceMobyIndexBias = LocateSourceMobyIndexBias(stream, layout, level);

        int offset = 0;
        offset = AdvanceComponent(levelData, offset, level.DisplayName, "texture");
        offset = AdvanceComponent(levelData, offset, level.DisplayName, "environment");
        offset = AdvanceComponent(levelData, offset, level.DisplayName, "occlusion");

        int specialSurfaceComponentOffset = offset;
        int afterSpecialSurfaces = AdvanceComponent(levelData, offset, level.DisplayName, "special surface");
        IReadOnlyList<PortalSpecialSurfaceRecord> specialSurfaces = ReadSpecialSurfaces(
            levelData,
            specialSurfaceComponentOffset,
            levelDataWadOffset,
            level.DisplayName);

        int collisionComponentOffset = afterSpecialSurfaces;
        int afterCollision = AdvanceComponent(levelData, collisionComponentOffset, level.DisplayName, "collision");
        int cycloramaComponentOffset = afterCollision;
        int portalTableOffset = AdvanceComponent(levelData, cycloramaComponentOffset, level.DisplayName, "cyclorama");
        IReadOnlyList<PortalSourceRecord> portals = ReadPortals(
            levelData,
            portalTableOffset,
            levelDataWadOffset,
            level.DisplayName,
            sourceMobyIndexBias,
            stream,
            layout,
            level,
            entryDataWadOffset,
            entryDataByteLength);
        (
            PortalCollisionSourceLayout collision,
            IReadOnlyList<PortalTriggerTriangle> triggerTriangles,
            IReadOnlyList<NativeCollisionSurfaceTriangle> collisionSurfaceTriangles
        ) = ReadTriggerTriangles(
            levelData,
            collisionComponentOffset,
            levelDataWadOffset,
            level.DisplayName,
            specialSurfaces);

        return new PortalSourceLevelData(
            level.Key,
            level.DisplayName,
            levelDataWadOffset,
            entryDataWadOffset,
            entryDataByteLength,
            levelDataWadOffset + specialSurfaceComponentOffset,
            levelDataWadOffset + collisionComponentOffset,
            levelDataWadOffset + portalTableOffset,
            sourceMobyIndexBias,
            collision,
            portals,
            specialSurfaces,
            triggerTriangles,
            collisionSurfaceTriangles);
    }

    private static IReadOnlyList<PortalSpecialSurfaceRecord> ReadSpecialSurfaces(
        byte[] levelData,
        int componentOffset,
        long levelDataWadOffset,
        string levelName)
    {
        int componentLength = ReadComponentLength(levelData, componentOffset, levelName, "special surface");
        int componentBodyOffset = componentOffset + 4;
        int count = ReadInt32(levelData, componentBodyOffset, levelName, "special surface count");
        if (count is < 0 or > 63 || 8L + (count * 4L) > componentLength)
            throw new InvalidOperationException($"{levelName} has an invalid special-surface count {count}.");

        int[] relativeOffsets = new int[count];
        for (int index = 0; index < count; index++)
        {
            relativeOffsets[index] = ReadInt32(
                levelData,
                componentBodyOffset + 4 + (index * 4),
                levelName,
                $"special surface {index} pointer");
        }

        List<PortalSpecialSurfaceRecord> records = new(count);
        for (int index = 0; index < count; index++)
        {
            int relativeOffset = relativeOffsets[index];
            int recordOffset = checked(componentBodyOffset + relativeOffset);
            int recordLimit = relativeOffsets
                .Where(candidate => candidate > relativeOffset)
                .Select(candidate => checked(componentBodyOffset + candidate))
                .DefaultIfEmpty(componentOffset + componentLength)
                .Min();
            if (recordOffset < componentBodyOffset || recordOffset + 4 > componentOffset + componentLength || recordLimit <= recordOffset)
                throw new InvalidOperationException(
                    $"{levelName} special surface {index} pointer 0x{relativeOffset:X} resolves to 0x{recordOffset:X}, outside component 0x{componentOffset:X}-0x{componentOffset + componentLength:X}.");

            int param1 = recordOffset + 8 <= recordLimit
                ? ReadInt32(levelData, recordOffset + 4, levelName, $"special surface {index} parameter 1")
                : 0;
            int param2 = recordOffset + 12 <= recordLimit
                ? ReadInt32(levelData, recordOffset + 8, levelName, $"special surface {index} parameter 2")
                : 0;
            byte[] rawBytes = levelData
                .AsSpan(recordOffset, recordLimit - recordOffset)
                .ToArray();

            records.Add(new PortalSpecialSurfaceRecord(
                index,
                levelData[recordOffset],
                param1,
                param2,
                levelDataWadOffset + recordOffset,
                relativeOffset,
                rawBytes));
        }

        return records;
    }

    private static IReadOnlyList<PortalSourceRecord> ReadPortals(
        byte[] levelData,
        int portalTableOffset,
        long levelDataWadOffset,
        string levelName,
        int sourceMobyIndexBias,
        FileStream stream,
        DiscLayout layout,
        LevelDefinition level,
        long entryDataWadOffset,
        int entryDataByteLength)
    {
        int portalCount = ReadInt32(levelData, portalTableOffset, levelName, "portal count");
        if (portalCount is < 0 or > 6)
            throw new InvalidOperationException($"{levelName} has an invalid portal count {portalCount}.");

        List<PortalSourceRecord> portals = new(portalCount);
        int offset = portalTableOffset + 4;
        for (int index = 0; index < portalCount; index++)
        {
            int portalOffset = offset;
            int pointCount = ReadInt32(levelData, portalOffset + 4, levelName, $"portal {index} point count");
            if (pointCount is < 1 or > 64)
                throw new InvalidOperationException($"{levelName} portal {index} has an invalid point count {pointCount}.");

            int storedPointCount = pointCount - 1;
            int skyboxStubOffset = checked(portalOffset + 0x2C + (storedPointCount * 12));
            int skyboxComponentOffset = checked(skyboxStubOffset + 20);
            if (skyboxComponentOffset + 4 > levelData.Length)
                throw new InvalidOperationException($"{levelName} portal {index} is truncated.");

            List<PortalSourcePoint> points = new(storedPointCount);
            for (int pointIndex = 0; pointIndex < storedPointCount; pointIndex++)
            {
                int pointOffset = portalOffset + 0x2C + (pointIndex * 12);
                points.Add(ReadPoint(
                    levelData,
                    pointOffset,
                    levelDataWadOffset,
                    levelName,
                    $"portal {index} point {pointIndex}"));
            }

            int pathMobyIndex = ReadInt32(levelData, portalOffset + 0x18, levelName, $"portal {index} path moby");
            int sourcePathMobyTrueIndex = checked(pathMobyIndex + sourceMobyIndexBias);
            PortalPathSourceRecord path = ReadPortalPath(
                stream,
                layout,
                level,
                sourcePathMobyTrueIndex,
                entryDataWadOffset,
                entryDataByteLength);

            portals.Add(new PortalSourceRecord(
                index,
                ReadInt32(levelData, portalOffset + 0x1C, levelName, $"portal {index} destination"),
                pathMobyIndex,
                sourcePathMobyTrueIndex,
                ReadInt32(levelData, portalOffset + 0x14, levelName, $"portal {index} world sector"),
                pointCount,
                ReadInt32(levelData, portalOffset + 0x08, levelName, $"portal {index} normal X"),
                ReadInt32(levelData, portalOffset + 0x0C, levelName, $"portal {index} normal Y"),
                ReadInt32(levelData, portalOffset + 0x10, levelName, $"portal {index} normal Z"),
                ReadInt32(levelData, portalOffset + 0x20, levelName, $"portal {index} center X"),
                ReadInt32(levelData, portalOffset + 0x24, levelName, $"portal {index} center Y"),
                ReadInt32(levelData, portalOffset + 0x28, levelName, $"portal {index} center Z"),
                levelDataWadOffset + portalOffset,
                points,
                path));

            offset = AdvanceComponent(levelData, skyboxComponentOffset, levelName, $"portal {index} skybox");
        }

        return portals;
    }

    private static PortalPathSourceRecord ReadPortalPath(
        FileStream stream,
        DiscLayout layout,
        LevelDefinition level,
        int sourcePathMobyTrueIndex,
        long entryDataWadOffset,
        int entryDataByteLength)
    {
        if (!level.HasSourceTable || sourcePathMobyTrueIndex < 0 || sourcePathMobyTrueIndex >= level.SourceRecordCount)
        {
            throw new InvalidOperationException(
                $"{level.DisplayName} portal path T{sourcePathMobyTrueIndex} is outside its mapped source moby table.");
        }

        long tableWadOffset = ParseOffset(level.SourceTableWadOffset);
        long tableEndWadOffset = checked(tableWadOffset + ((long)level.SourceRecordCount * 0x58));
        long entryDataEndWadOffset = checked(entryDataWadOffset + entryDataByteLength);
        if (tableWadOffset < entryDataWadOffset || tableEndWadOffset > entryDataEndWadOffset)
        {
            throw new InvalidOperationException(
                $"{level.DisplayName}'s source moby table is outside its native entry-scene block.");
        }

        long recordWadOffset = checked(tableWadOffset + ((long)sourcePathMobyTrueIndex * 0x58));
        byte[] record = DiscImage.ReadFileBytes(stream, layout, WadLba, recordWadOffset, 0x58);
        int actorClass = BinaryPrimitives.ReadUInt16LittleEndian(record.AsSpan(0x36, 2));
        if (actorClass != 398)
        {
            throw new InvalidOperationException(
                $"{level.DisplayName} portal path T{sourcePathMobyTrueIndex} is actor class {actorClass}, expected native portal-path class 398.");
        }

        uint propsRelativeOffset = BinaryPrimitives.ReadUInt32LittleEndian(record.AsSpan(0, 4));
        long propsWadOffset = ValidateEntryDataOffset(
            level.DisplayName,
            sourcePathMobyTrueIndex,
            "properties",
            propsRelativeOffset,
            0x1C,
            entryDataWadOffset,
            entryDataByteLength);
        byte[] props = DiscImage.ReadFileBytes(stream, layout, WadLba, propsWadOffset, 0x1C);
        uint pathRelativeOffset = BinaryPrimitives.ReadUInt32LittleEndian(props.AsSpan(0, 4));
        long pathWadOffset = ValidateEntryDataOffset(
            level.DisplayName,
            sourcePathMobyTrueIndex,
            "path",
            pathRelativeOffset,
            8,
            entryDataWadOffset,
            entryDataByteLength);
        byte[] pathHeader = DiscImage.ReadFileBytes(stream, layout, WadLba, pathWadOffset, 8);
        int nodeCount = pathHeader[0];
        int currentNode = pathHeader[1];
        int reversed = BinaryPrimitives.ReadInt16LittleEndian(pathHeader.AsSpan(6, 2));
        if (nodeCount is < 2 or > 64 || currentNode >= nodeCount)
        {
            throw new InvalidOperationException(
                $"{level.DisplayName} portal path T{sourcePathMobyTrueIndex} has invalid node state {currentNode}/{nodeCount}.");
        }

        int pathByteLength = checked(8 + (nodeCount * 16));
        ValidateEntryDataOffset(
            level.DisplayName,
            sourcePathMobyTrueIndex,
            "path nodes",
            pathRelativeOffset,
            pathByteLength,
            entryDataWadOffset,
            entryDataByteLength);
        byte[] pathBytes = DiscImage.ReadFileBytes(stream, layout, WadLba, pathWadOffset, pathByteLength);
        List<PortalPathSourceNode> nodes = new(nodeCount);
        for (int nodeIndex = 0; nodeIndex < nodeCount; nodeIndex++)
        {
            int nodeOffset = 8 + (nodeIndex * 16);
            nodes.Add(new PortalPathSourceNode(
                nodeIndex,
                BinaryPrimitives.ReadInt32LittleEndian(pathBytes.AsSpan(nodeOffset, 4)),
                BinaryPrimitives.ReadInt32LittleEndian(pathBytes.AsSpan(nodeOffset + 4, 4)),
                BinaryPrimitives.ReadInt32LittleEndian(pathBytes.AsSpan(nodeOffset + 8, 4)),
                pathWadOffset + nodeOffset));
        }

        return new PortalPathSourceRecord(
            propsWadOffset,
            pathWadOffset,
            nodeCount,
            currentNode,
            reversed,
            nodes);
    }

    private static long ValidateEntryDataOffset(
        string levelName,
        int sourcePathMobyTrueIndex,
        string field,
        uint relativeOffset,
        int byteLength,
        long entryDataWadOffset,
        int entryDataByteLength)
    {
        if (relativeOffset == 0 || byteLength <= 0 ||
            (long)relativeOffset + byteLength > entryDataByteLength)
        {
            throw new InvalidOperationException(
                $"{levelName} portal path T{sourcePathMobyTrueIndex} has an invalid {field} offset 0x{relativeOffset:X}.");
        }

        return checked(entryDataWadOffset + relativeOffset);
    }

    private static int LocateSourceMobyIndexBias(FileStream stream, DiscLayout layout, LevelDefinition level)
    {
        if (!level.HasSourceTable || level.SourceRecordCount <= 0)
            return 0;

        long tableWadOffset = ParseOffset(level.SourceTableWadOffset);
        int directCount = BinaryPrimitives.ReadInt32LittleEndian(
            DiscImage.ReadFileBytes(stream, layout, WadLba, tableWadOffset - 4, 4));
        if (directCount == level.SourceRecordCount)
            return 0;

        int oneRowLaterCount = BinaryPrimitives.ReadInt32LittleEndian(
            DiscImage.ReadFileBytes(stream, layout, WadLba, tableWadOffset + 0x58 - 4, 4));
        if (oneRowLaterCount == level.SourceRecordCount - 1)
            return 1;

        throw new InvalidOperationException(
            $"{level.DisplayName}'s source/runtime moby index bias is unresolved ({directCount}, {oneRowLaterCount}).");
    }

    private static (
        PortalCollisionSourceLayout Layout,
        IReadOnlyList<PortalTriggerTriangle> PortalTriangles,
        IReadOnlyList<NativeCollisionSurfaceTriangle> SurfaceTriangles
    ) ReadTriggerTriangles(
        byte[] levelData,
        int collisionComponentOffset,
        long levelDataWadOffset,
        string levelName,
        IReadOnlyList<PortalSpecialSurfaceRecord> specialSurfaces)
    {
        int componentLength = ReadComponentLength(levelData, collisionComponentOffset, levelName, "collision");
        int bodyOffset = collisionComponentOffset + 4;
        int triangleCount = ReadInt32(levelData, bodyOffset, levelName, "collision triangle count");
        int flagCount = ReadInt32(levelData, bodyOffset + 4, levelName, "collision flag count");
        if (triangleCount < 0 || triangleCount > 100_000 || flagCount < 0 || flagCount > triangleCount)
            throw new InvalidOperationException($"{levelName} has invalid collision counts {triangleCount}/{flagCount}.");

        int triangleRelativeOffset = ReadInt32(levelData, bodyOffset + 0x10, levelName, "collision triangle pointer");
        int flagsRelativeOffset = ReadInt32(levelData, bodyOffset + 0x18, levelName, "collision flags pointer");
        int blockTreeRelativeOffset = ReadInt32(levelData, bodyOffset + 0x08, levelName, "collision block-tree pointer");
        int blocksRelativeOffset = ReadInt32(levelData, bodyOffset + 0x0C, levelName, "collision blocks pointer");
        int blockTreeOffset = checked(bodyOffset + blockTreeRelativeOffset);
        int blocksOffset = checked(bodyOffset + blocksRelativeOffset);
        int triangleOffset = checked(bodyOffset + triangleRelativeOffset);
        int flagsOffset = checked(bodyOffset + flagsRelativeOffset);
        if (blockTreeOffset < bodyOffset || blocksOffset <= blockTreeOffset || triangleOffset <= blocksOffset)
            throw new InvalidOperationException($"{levelName}'s collision index pointers are not in native table order.");
        if (triangleOffset < bodyOffset || triangleOffset + (triangleCount * 12L) > collisionComponentOffset + componentLength)
            throw new InvalidOperationException($"{levelName}'s collision triangle table points outside its component.");
        if (flagsOffset < bodyOffset || flagsOffset + flagCount > collisionComponentOffset + componentLength)
            throw new InvalidOperationException($"{levelName}'s collision flag table points outside its component.");

        Dictionary<int, PortalSpecialSurfaceRecord> portalSurfaces = specialSurfaces
            .Where(surface => surface.Type == 6)
            .ToDictionary(surface => surface.Index);
        Dictionary<int, PortalSpecialSurfaceRecord> surfacesByIndex = specialSurfaces
            .ToDictionary(surface => surface.Index);
        List<PortalTriggerTriangle> triangles = new();
        List<NativeCollisionSurfaceTriangle> surfaceTriangles = new(triangleCount);
        for (int index = 0; index < triangleCount; index++)
        {
            int flagByte = index < flagCount ? levelData[flagsOffset + index] : 0xFF;
            int surfaceIndex = flagByte & 0x3F;
            int recordOffset = triangleOffset + (index * 12);
            uint xWord = ReadUInt32(levelData, recordOffset, levelName, $"collision triangle {index} X");
            uint yWord = ReadUInt32(levelData, recordOffset + 4, levelName, $"collision triangle {index} Y");
            uint zWord = ReadUInt32(levelData, recordOffset + 8, levelName, $"collision triangle {index} Z");
            int p1X = (int)(xWord & 0x3FFF);
            int p1Y = (int)(yWord & 0x3FFF);
            int p1Z = (int)(zWord & 0x3FFF);
            PortalSourcePoint p1 = new(p1X, p1Y, p1Z, levelDataWadOffset + recordOffset);
            PortalSourcePoint p2 = new(
                p1X + SignedBits((int)((xWord >> 14) & 0x1FF), 9),
                p1Y + SignedBits((int)((yWord >> 14) & 0x1FF), 9),
                p1Z + (int)((zWord >> 16) & 0xFF),
                levelDataWadOffset + recordOffset);
            PortalSourcePoint p3 = new(
                p1X + SignedBits((int)((xWord >> 23) & 0x1FF), 9),
                p1Y + SignedBits((int)((yWord >> 23) & 0x1FF), 9),
                p1Z + (int)((zWord >> 24) & 0xFF),
                levelDataWadOffset + recordOffset);

            surfacesByIndex.TryGetValue(surfaceIndex, out PortalSpecialSurfaceRecord? nativeSurface);
            surfaceTriangles.Add(new NativeCollisionSurfaceTriangle(
                index,
                flagByte,
                surfaceIndex,
                nativeSurface?.Type ?? -1,
                nativeSurface?.Param1 ?? 0,
                nativeSurface?.Param2 ?? 0,
                levelDataWadOffset + recordOffset,
                index < flagCount ? levelDataWadOffset + flagsOffset + index : -1,
                p1,
                p2,
                p3));

            if (!portalSurfaces.TryGetValue(surfaceIndex, out PortalSpecialSurfaceRecord? surface))
                continue;

            triangles.Add(new PortalTriggerTriangle(
                index,
                surfaceIndex,
                surface.DestinationLevelId,
                surface.PortalIndex,
                flagByte,
                levelDataWadOffset + recordOffset,
                p1,
                p2,
                p3));
        }

        PortalCollisionSourceLayout layout = new(
            triangleCount,
            flagCount,
            levelDataWadOffset + blockTreeOffset,
            blocksOffset - blockTreeOffset,
            levelDataWadOffset + blocksOffset,
            triangleOffset - blocksOffset,
            levelDataWadOffset + triangleOffset,
            levelDataWadOffset + flagsOffset);
        return (layout, triangles, surfaceTriangles);
    }

    private static PortalSourcePoint ReadPoint(
        byte[] bytes,
        int offset,
        long levelDataWadOffset,
        string levelName,
        string field)
    {
        return new PortalSourcePoint(
            ReadInt32(bytes, offset, levelName, $"{field} X"),
            ReadInt32(bytes, offset + 4, levelName, $"{field} Y"),
            ReadInt32(bytes, offset + 8, levelName, $"{field} Z"),
            levelDataWadOffset + offset);
    }

    private static int AdvanceComponent(byte[] bytes, int offset, string levelName, string componentName)
    {
        int length = ReadComponentLength(bytes, offset, levelName, componentName);
        return checked(offset + length);
    }

    private static int ReadComponentLength(byte[] bytes, int offset, string levelName, string componentName)
    {
        int length = ReadInt32(bytes, offset, levelName, $"{componentName} component length");
        if (length < 4 || (long)offset + length > bytes.Length)
            throw new InvalidOperationException($"{levelName}'s {componentName} component at 0x{offset:X} has invalid length 0x{length:X}.");
        return length;
    }

    private static int ReadInt32(byte[] bytes, int offset, string levelName, string field)
    {
        if (offset < 0 || offset + 4 > bytes.Length)
            throw new InvalidOperationException($"{levelName}'s {field} is outside the native level-data block.");
        return BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset, 4));
    }

    private static uint ReadUInt32(byte[] bytes, int offset, string levelName, string field)
    {
        if (offset < 0 || offset + 4 > bytes.Length)
            throw new InvalidOperationException($"{levelName}'s {field} is outside the native level-data block.");
        return BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
    }

    private static uint ReadUInt32(byte[] bytes, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));

    private static int SignedBits(int value, int bits)
    {
        int sign = 1 << (bits - 1);
        int range = 1 << bits;
        return (value & sign) != 0 ? value - range : value;
    }

    private static long ParseOffset(string value)
    {
        string text = value.Trim();
        return text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? long.Parse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture)
            : long.Parse(text, CultureInfo.InvariantCulture);
    }
}
