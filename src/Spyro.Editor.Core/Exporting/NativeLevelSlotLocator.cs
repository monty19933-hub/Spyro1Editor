using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Exporting;

public static class NativeLevelSlotLocator
{
    private const int MaximumArchiveHeaderBytes = 1 << 20;
    private const int NestedDescriptorLimitOffset = 0x50;
    private const int ExpectedStoneHillNestedDescriptorCount = 8;
    private static readonly int[] StoneHillReturnHomeTrueIndexes = [177, 179];

    public static async Task<NativeLevelReplacementSourceBinding> InspectAsync(
        string sourceImagePath,
        NativeLevelSlotContract slot,
        LevelCatalog catalog,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceImagePath);
        ArgumentNullException.ThrowIfNull(slot);
        ArgumentNullException.ThrowIfNull(catalog);
        NativeLevelSlotContract expected = NativeLevelReplacementSupportCatalog.RequireSupported(
            catalog,
            slot.TargetLevelKey);
        if (slot != expected)
            throw new InvalidDataException("The requested native level slot is not the checked Stone Hill contract.");

        string sourcePath = Path.GetFullPath(sourceImagePath);
        FileInfo sourceInfo = new(sourcePath);
        if (!sourceInfo.Exists)
            throw new FileNotFoundException("The source disc image does not exist.", sourcePath);

        string sourceSha256 = await HashFileAsync(sourcePath, cancellationToken);
        DiscLayout layout = DiscImage.DetectLayout(sourcePath);
        await using FileStream image = new(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        DiscFileRecord wad = DiscImage.FindRootFileRecord(image, layout, IsWadName);
        DiscFileRecord executable = DiscImage.FindRootFileRecord(image, layout, IsExecutableName);

        byte[] wadPrefix = DiscImage.ReadFileBytes(image, layout, wad.Lba, 0, 8);
        int wadHeaderByteLength = checked((int)ReadUInt32(wadPrefix, 0));
        if (wadHeaderByteLength < 8 || wadHeaderByteLength > MaximumArchiveHeaderBytes ||
            wadHeaderByteLength > wad.Size)
            throw new InvalidDataException($"The WAD archive header length 0x{wadHeaderByteLength:X} is invalid.");
        byte[] wadHeader = DiscImage.ReadFileBytes(image, layout, wad.Lba, 0, wadHeaderByteLength);

        NativeWadEntryPreimage metadata = ReadEntryPreimage(
            image, layout, wad, wadHeader, slot.MetadataWadDirectoryIndex, cancellationToken);
        NativeWadEntryPreimage metadataAdjacent = ReadEntryPreimage(
            image, layout, wad, wadHeader, slot.MetadataAdjacentWadDirectoryIndex, cancellationToken);
        NativeWadEntryPreimage loadedPredecessor = ReadEntryPreimage(
            image, layout, wad, wadHeader, slot.LoadedDataPredecessorWadDirectoryIndex, cancellationToken);
        NativeWadEntryPreimage dataEntry = ReadEntryPreimage(
            image, layout, wad, wadHeader, slot.LevelDataWadDirectoryIndex, cancellationToken);

        ValidateMetadataIdentity(image, layout, wad, wadHeader, metadata, slot.LevelId);
        if (loadedPredecessor.FirstWord != 12)
        {
            throw new InvalidDataException(
                "Stone Hill's loaded data entry no longer has the checked level-12 predecessor package at WAD entry 11.");
        }

        int nestedHeaderByteLength = checked((int)dataEntry.FirstWord);
        if (nestedHeaderByteLength < 16 || nestedHeaderByteLength > MaximumArchiveHeaderBytes ||
            nestedHeaderByteLength > dataEntry.ByteLength)
            throw new InvalidDataException("Stone Hill's loaded WAD entry has an invalid nested header boundary.");
        byte[] nestedHeader = DiscImage.ReadFileBytes(
            image,
            layout,
            wad.Lba,
            dataEntry.WadOffset,
            nestedHeaderByteLength);
        IReadOnlyList<NestedDescriptor> descriptors = ParsePackedNestedDescriptors(
            nestedHeader,
            dataEntry.ByteLength);
        if (descriptors.Count != ExpectedStoneHillNestedDescriptorCount ||
            !descriptors.Select(descriptor => descriptor.Index)
                .SequenceEqual(Enumerable.Range(0, ExpectedStoneHillNestedDescriptorCount)))
        {
            throw new InvalidDataException(
                $"Stone Hill's packed nested directory changed from the checked eight contiguous descriptors; found {descriptors.Count}.");
        }

        int descriptorTableByteLength = checked(descriptors.Count * 8);
        string descriptorTableSha256 = HashBytes(nestedHeader.AsSpan(0, descriptorTableByteLength));
        NativeNestedSubfilePreimage levelDataSubfile = ReadSubfilePreimage(
            image,
            layout,
            wad,
            dataEntry,
            descriptors,
            slot.LevelDataSubfileIndex,
            cancellationToken);
        NativeNestedSubfilePreimage mobyTableSubfile = ReadSubfilePreimage(
            image,
            layout,
            wad,
            dataEntry,
            descriptors,
            slot.SourceMobyTableSubfileIndex,
            cancellationToken);

        if (dataEntry.WadOffset + slot.SourceMobyTableRelativeOffset != slot.SourceMobyTableWadOffset)
            throw new InvalidDataException("Stone Hill's absolute and entry-relative source Moby table offsets disagree.");
        int mobyTableByteLength = checked(slot.SourceMobyRecordCount * slot.SourceMobyRecordStride);
        long mobySubfileStart = dataEntry.WadOffset + mobyTableSubfile.RelativeOffset;
        long mobySubfileEnd = mobySubfileStart + mobyTableSubfile.ByteLength;
        if (slot.SourceMobyTableWadOffset < mobySubfileStart ||
            slot.SourceMobyTableWadOffset + mobyTableByteLength > mobySubfileEnd)
            throw new InvalidDataException("Stone Hill's 195-row Moby table no longer lies inside packed nested subfile 3.");

        LevelDefinition artisans = catalog.FindByKey("artisans")
            ?? throw new InvalidDataException("The Artisans catalog row is missing.");
        if (!NativeLevelReplacementSupportCatalog.TryParseOffset(artisans.SourceTableWadOffset, out long artisansTableOffset))
            throw new InvalidDataException("The Artisans source Moby table offset is invalid.");
        int[] portalIndexes = NativeLevelReplacementSupportCatalog.RequireStoneHillPortal().TrueIndexes.ToArray();
        IReadOnlyList<NativeMobyRowPreimage> portalRows = ReadMobyRows(
            image,
            layout,
            wad,
            artisans,
            artisansTableOffset,
            portalIndexes,
            cancellationToken);
        LevelDefinition stoneHill = catalog.FindByKey(slot.TargetLevelKey)
            ?? throw new InvalidDataException("The Stone Hill catalog row is missing.");
        IReadOnlyList<NativeMobyRowPreimage> returnHomeRows = ReadMobyRows(
            image,
            layout,
            wad,
            stoneHill,
            slot.SourceMobyTableWadOffset,
            StoneHillReturnHomeTrueIndexes,
            cancellationToken);

        return new NativeLevelReplacementSourceBinding(
            SourceImageBytes: sourceInfo.Length,
            SourceImageSha256: sourceSha256,
            DiscSectorSize: layout.SectorSize,
            DiscUserOffset: layout.UserOffset,
            Executable: new NativeDiscFilePreimage(
                executable.Name,
                executable.Lba,
                executable.Size,
                HashDiscRegion(image, layout, executable.Lba, 0, executable.Size, cancellationToken)),
            Wad: new NativeDiscFilePreimage(
                wad.Name,
                wad.Lba,
                wad.Size,
                HashDiscRegion(image, layout, wad.Lba, 0, wad.Size, cancellationToken)),
            WadArchiveHeaderByteLength: wadHeaderByteLength,
            WadArchiveHeaderSha256: HashBytes(wadHeader),
            LevelMetadataEntry: metadata,
            MetadataAdjacentEntry: metadataAdjacent,
            LoadedDataPredecessorEntry: loadedPredecessor,
            LevelDataEntry: dataEntry,
            NestedHeaderByteLength: nestedHeaderByteLength,
            NestedHeaderSha256: HashBytes(nestedHeader),
            NestedDescriptorTableByteLength: descriptorTableByteLength,
            NestedDescriptorTableSha256: descriptorTableSha256,
            LevelDataSubfile: levelDataSubfile,
            SourceMobyTableSubfile: mobyTableSubfile,
            SourceMobyTableByteLength: mobyTableByteLength,
            SourceMobyTableSha256: HashDiscRegion(
                image,
                layout,
                wad.Lba,
                slot.SourceMobyTableWadOffset,
                mobyTableByteLength,
                cancellationToken),
            ArtisansPortalControlRows: portalRows,
            StoneHillReturnHomeRows: returnHomeRows);
    }

    private static NativeWadEntryPreimage ReadEntryPreimage(
        FileStream image,
        DiscLayout layout,
        DiscFileRecord wad,
        byte[] wadHeader,
        int directoryIndex,
        CancellationToken cancellationToken)
    {
        int slotOffset = checked(directoryIndex * 8);
        if (slotOffset < 0 || slotOffset + 8 > wadHeader.Length)
            throw new InvalidDataException($"WAD entry {directoryIndex} is outside the archive header.");
        long offset = ReadUInt32(wadHeader, slotOffset);
        int byteLength = checked((int)ReadUInt32(wadHeader, slotOffset + 4));
        if (offset <= 0 || byteLength < 8 || offset + byteLength > wad.Size)
            throw new InvalidDataException($"WAD entry {directoryIndex} has an invalid boundary.");
        byte[] head = DiscImage.ReadFileBytes(image, layout, wad.Lba, offset, 8);
        return new NativeWadEntryPreimage(
            directoryIndex,
            offset,
            byteLength,
            ReadUInt32(head, 0),
            ReadUInt32(head, 4),
            HashDiscRegion(image, layout, wad.Lba, offset, byteLength, cancellationToken));
    }

    private static void ValidateMetadataIdentity(
        FileStream image,
        DiscLayout layout,
        DiscFileRecord wad,
        byte[] wadHeader,
        NativeWadEntryPreimage expectedMetadata,
        int levelId)
    {
        List<int> matches = [];
        for (int index = 0; index * 8 + 8 <= wadHeader.Length; index++)
        {
            int row = index * 8;
            long offset = ReadUInt32(wadHeader, row);
            int length = checked((int)ReadUInt32(wadHeader, row + 4));
            if (offset <= 0 || length < 8 || offset + length > wad.Size)
                continue;
            byte[] head = DiscImage.ReadFileBytes(image, layout, wad.Lba, offset, 8);
            uint first = ReadUInt32(head, 0);
            uint second = ReadUInt32(head, 4);
            if (first == levelId && second is >= 0x80000000 and < 0x80200000)
                matches.Add(index);
        }

        if (matches.Count != 1 || matches[0] != expectedMetadata.DirectoryIndex ||
            expectedMetadata.FirstWord != levelId ||
            expectedMetadata.SecondWord is < 0x80000000 or >= 0x80200000)
        {
            throw new InvalidDataException(
                $"Stone Hill level ID {levelId} no longer resolves uniquely to metadata WAD entry {expectedMetadata.DirectoryIndex}.");
        }
    }

    private static IReadOnlyList<NestedDescriptor> ParsePackedNestedDescriptors(
        byte[] header,
        int archiveByteLength)
    {
        List<NestedDescriptor> result = [];
        int previousEnd = -1;
        for (int row = 0; row + 8 <= header.Length && row < NestedDescriptorLimitOffset; row += 8)
        {
            int relativeOffset = checked((int)ReadUInt32(header, row));
            int byteLength = checked((int)ReadUInt32(header, row + 4));
            if (relativeOffset == 0 && byteLength == 0)
                break;
            if (relativeOffset < header.Length || byteLength <= 0 ||
                (long)relativeOffset + byteLength > archiveByteLength)
                throw new InvalidDataException($"Nested WAD descriptor {row / 8} has an invalid boundary.");
            if (result.Count == 0 && relativeOffset != header.Length)
                throw new InvalidDataException("The first nested WAD subfile does not begin after the complete header.");
            if (previousEnd >= 0 && relativeOffset != previousEnd)
                throw new InvalidDataException("Stone Hill's packed nested WAD subfiles are no longer contiguous.");
            result.Add(new NestedDescriptor(row / 8, relativeOffset, byteLength));
            previousEnd = checked(relativeOffset + byteLength);
        }

        if (result.Count == 0 || previousEnd != archiveByteLength)
            throw new InvalidDataException("Stone Hill's packed nested WAD directory does not cover the complete data entry.");
        return result;
    }

    private static NativeNestedSubfilePreimage ReadSubfilePreimage(
        FileStream image,
        DiscLayout layout,
        DiscFileRecord wad,
        NativeWadEntryPreimage entry,
        IReadOnlyList<NestedDescriptor> descriptors,
        int subfileIndex,
        CancellationToken cancellationToken)
    {
        NestedDescriptor descriptor = descriptors.SingleOrDefault(candidate => candidate.Index == subfileIndex)
            ?? throw new InvalidDataException($"Stone Hill's data entry has no packed nested subfile {subfileIndex}.");
        return new NativeNestedSubfilePreimage(
            descriptor.Index,
            descriptor.RelativeOffset,
            descriptor.ByteLength,
            HashDiscRegion(
                image,
                layout,
                wad.Lba,
                entry.WadOffset + descriptor.RelativeOffset,
                descriptor.ByteLength,
                cancellationToken));
    }

    private static IReadOnlyList<NativeMobyRowPreimage> ReadMobyRows(
        FileStream image,
        DiscLayout layout,
        DiscFileRecord wad,
        LevelDefinition owner,
        long tableWadOffset,
        IReadOnlyList<int> trueIndexes,
        CancellationToken cancellationToken)
    {
        if (trueIndexes.Distinct().Count() != trueIndexes.Count ||
            trueIndexes.Any(index => index < 0 || index >= owner.SourceRecordCount))
            throw new InvalidDataException($"{owner.DisplayName}'s protected Moby-row indexes are invalid.");
        return trueIndexes
            .Select(index =>
            {
                long offset = checked(tableWadOffset + ((long)index * MobyLoader.RuntimeRecordStride));
                return new NativeMobyRowPreimage(
                    LevelCatalog.NormalizeKey(owner.Key),
                    index,
                    offset,
                    HashDiscRegion(
                        image,
                        layout,
                        wad.Lba,
                        offset,
                        MobyLoader.RuntimeRecordStride,
                        cancellationToken));
            })
            .ToArray();
    }

    private static string HashDiscRegion(
        FileStream image,
        DiscLayout layout,
        int fileLba,
        long fileOffset,
        int byteLength,
        CancellationToken cancellationToken)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        const int chunkBytes = 1 << 20;
        long cursor = 0;
        while (cursor < byteLength)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int length = (int)Math.Min(chunkBytes, byteLength - cursor);
            hash.AppendData(DiscImage.ReadFileBytes(image, layout, fileLba, fileOffset + cursor, length));
            cursor += length;
        }
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static async Task<string> HashFileAsync(string path, CancellationToken cancellationToken)
    {
        await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken)).ToLowerInvariant();
    }

    private static string HashBytes(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static uint ReadUInt32(ReadOnlySpan<byte> bytes, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset, 4));

    private static bool IsWadName(string name) =>
        string.Equals(name, "WAD.WAD", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, "WAD", StringComparison.OrdinalIgnoreCase);

    private static bool IsExecutableName(string name) =>
        name.StartsWith("SCUS_", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("SCES_", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("SLES_", StringComparison.OrdinalIgnoreCase);

    private sealed record NestedDescriptor(int Index, int RelativeOffset, int ByteLength);
}
