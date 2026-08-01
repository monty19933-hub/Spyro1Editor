using System.Buffers.Binary;
using System.Text;
using System.Text.Json;

namespace Spyro.Editor.Core.Exporting;

internal sealed record NativeSkyRelocationPayload(
    int PatchIndex,
    int WadLba,
    long OriginalWadOffset,
    int StorageWadEntry,
    int ModelBlockOffset,
    int OriginalLength,
    byte[] Bytes,
    int SubfileIndex = 1,
    bool RequireLengthPrefix = true);

internal sealed record NativeSkyRelocatedPatch(
    int PatchIndex,
    int ModelBlockOffset,
    long WadOffset,
    long ImageOffset);

internal sealed record NativeSkyWadRelocationPlan(
    bool Required,
    int WadLba,
    int OriginalWadSize,
    int ExpandedWadSize,
    int WadGrowthBytes,
    int OriginalExecutableLba,
    int RelocatedExecutableLba,
    int ExecutableSize,
    int NextFileLba,
    int AvailableGrowthBytes,
    IReadOnlyDictionary<int, int> EntryGrowthBytes,
    IReadOnlyDictionary<int, long> RelocatedEntryOffsets,
    IReadOnlyList<NativeSkyRelocatedPatch> RelocatedPatches);

internal static class NativeSkyWadRelocator
{
    private const int SectorBytes = 2048;
    public static NativeSkyWadRelocationPlan BuildPlan(
        string imagePath,
        string wadAnalysisPath,
        IReadOnlyList<NativeSkyRelocationPayload> payloads)
    {
        WadLayout wad = LoadWadLayout(wadAnalysisPath);
        DiscLayout discLayout = DiscImage.DetectLayout(imagePath);
        using FileStream image = File.Open(imagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        IReadOnlyList<IsoRootRecord> rootFiles = ReadRootFiles(image, discLayout);
        IsoRootRecord wadFile = rootFiles.FirstOrDefault(file =>
                string.Equals(file.Name, "WAD.WAD", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(file.Name, "WAD", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("WAD.WAD was not found in the disc root.");
        IsoRootRecord executable = rootFiles.FirstOrDefault(file => IsExecutableName(file.Name))
            ?? throw new InvalidOperationException("The Spyro executable was not found in the disc root.");
        IsoRootRecord nextFile = rootFiles
            .Where(file => file.Lba > executable.Lba)
            .OrderBy(file => file.Lba)
            .FirstOrDefault()
            ?? throw new InvalidOperationException("No file extent follows the Spyro executable.");

        if (wadFile.Lba != wad.WadLba || wadFile.Size != wad.WadSize)
            throw new InvalidDataException("The live WAD extent does not match the selected WAD analysis.");
        ValidateLiveWadEntryTable(image, discLayout, wad);
        if (wad.WadSize % SectorBytes != 0)
            throw new InvalidDataException("WAD.WAD is not sector aligned.");
        if (executable.Lba != wad.WadLba + (wad.WadSize / SectorBytes))
            throw new InvalidDataException("The executable does not immediately follow WAD.WAD in this disc layout.");

        Dictionary<int, int> entryGrowth = new();
        Dictionary<int, int> modelOffsets = new();
        foreach (IGrouping<int, NativeSkyRelocationPayload> group in payloads.GroupBy(payload => payload.StorageWadEntry))
        {
            WadEntry entry = wad.EntriesByIndex.TryGetValue(group.Key, out WadEntry? found)
                ? found
                : throw new InvalidDataException($"WAD entry {group.Key} is missing from the analysis.");
            byte[] header = DiscImage.ReadFileBytes(image, discLayout, wad.WadLba, entry.Offset, SectorBytes);
            IReadOnlyList<NestedSubfile> subfiles = ParseNestedSubfiles(header, entry.Size);
            int[] requestedSubfiles = group.Select(payload => payload.SubfileIndex).Distinct().ToArray();
            if (requestedSubfiles.Length != 1)
                throw new InvalidOperationException($"WAD entry {group.Key} relocation payloads must target one nested subfile at a time.");
            int requestedSubfile = requestedSubfiles[0];
            NestedSubfile model = subfiles.SingleOrDefault(subfile => subfile.Index == requestedSubfile)
                ?? throw new InvalidDataException($"WAD entry {group.Key} has no nested subfile {requestedSubfile}.");
            modelOffsets[group.Key] = model.Offset;

            int modelDelta = 0;
            int previousEnd = -1;
            foreach (NativeSkyRelocationPayload payload in group.OrderBy(payload => payload.ModelBlockOffset))
            {
                if (payload.Bytes.Length < payload.OriginalLength)
                    throw new InvalidOperationException("Relocation payloads must preserve or increase each sky block length.");
                if (payload.ModelBlockOffset < 0 || payload.ModelBlockOffset + (long)payload.OriginalLength > model.Size)
                    throw new InvalidDataException($"Relocation patch {payload.PatchIndex} is outside WAD entry {group.Key} subfile {requestedSubfile}.");
                if (payload.ModelBlockOffset < previousEnd)
                    throw new InvalidOperationException($"Sky patches overlap in WAD entry {group.Key}.");
                previousEnd = payload.ModelBlockOffset + payload.OriginalLength;
                modelDelta = checked(modelDelta + payload.Bytes.Length - payload.OriginalLength);
            }
            entryGrowth[group.Key] = AlignSector(modelDelta);
        }

        int totalGrowth = checked(entryGrowth.Values.Sum());
        Dictionary<int, long> relocatedEntryOffsets = new();
        long originalCursor = SectorBytes;
        long relocatedCursor = SectorBytes;
        foreach (WadEntry entry in wad.Entries.OrderBy(entry => entry.Offset))
        {
            if (entry.Offset != originalCursor)
                throw new InvalidDataException($"WAD entry {entry.Index} is not packed directly after the previous entry.");
            relocatedEntryOffsets[entry.Index] = relocatedCursor;
            originalCursor = checked(originalCursor + entry.Size);
            relocatedCursor = checked(relocatedCursor + entry.Size + entryGrowth.GetValueOrDefault(entry.Index));
        }
        int expandedWadSize = checked((int)relocatedCursor);
        if (expandedWadSize != wad.WadSize + totalGrowth)
            throw new InvalidDataException("Expanded WAD size accounting did not balance.");

        int relocatedExecutableLba = checked(wad.WadLba + (expandedWadSize / SectorBytes));
        int executableSectors = DivideRoundUp(executable.Size, SectorBytes);
        int availableGrowth = checked((nextFile.Lba - executable.Lba - executableSectors) * SectorBytes);
        if (totalGrowth > availableGrowth || relocatedExecutableLba + executableSectors > nextFile.Lba)
        {
            throw new InvalidOperationException(
                $"Expanded skies need {totalGrowth:N0} bytes, but this disc layout has {availableGrowth:N0} safe bytes before {nextFile.Name}.");
        }

        List<NativeSkyRelocatedPatch> relocatedPatches = new();
        foreach (IGrouping<int, NativeSkyRelocationPayload> group in payloads.GroupBy(payload => payload.StorageWadEntry))
        {
            int cumulativeDelta = 0;
            foreach (NativeSkyRelocationPayload payload in group.OrderBy(payload => payload.ModelBlockOffset))
            {
                int relocatedBlockOffset = checked(payload.ModelBlockOffset + cumulativeDelta);
                long wadOffset = checked(relocatedEntryOffsets[group.Key] + modelOffsets[group.Key] + relocatedBlockOffset);
                relocatedPatches.Add(new NativeSkyRelocatedPatch(
                    payload.PatchIndex,
                    relocatedBlockOffset,
                    wadOffset,
                    DiscImage.ConvertFileOffsetToImageOffset(discLayout, wad.WadLba, wadOffset)));
                cumulativeDelta = checked(cumulativeDelta + payload.Bytes.Length - payload.OriginalLength);
            }
        }

        return new NativeSkyWadRelocationPlan(
            Required: totalGrowth > 0,
            WadLba: wad.WadLba,
            OriginalWadSize: wad.WadSize,
            ExpandedWadSize: expandedWadSize,
            WadGrowthBytes: totalGrowth,
            OriginalExecutableLba: executable.Lba,
            RelocatedExecutableLba: relocatedExecutableLba,
            ExecutableSize: executable.Size,
            NextFileLba: nextFile.Lba,
            AvailableGrowthBytes: availableGrowth,
            EntryGrowthBytes: entryGrowth,
            RelocatedEntryOffsets: relocatedEntryOffsets,
            RelocatedPatches: relocatedPatches);
    }

    public static void WriteExpandedImage(
        string sourceImagePath,
        string outputImagePath,
        string wadAnalysisPath,
        NativeSkyWadRelocationPlan plan,
        IReadOnlyList<NativeSkyRelocationPayload> payloads)
    {
        if (!plan.Required)
            throw new InvalidOperationException("The sky patch plan does not require WAD relocation.");

        WadLayout wad = LoadWadLayout(wadAnalysisPath);
        DiscLayout discLayout = DiscImage.DetectLayout(sourceImagePath);
        using FileStream source = File.Open(sourceImagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        IReadOnlyList<IsoRootRecord> rootFiles = ReadRootFiles(source, discLayout);
        IsoRootRecord wadFile = rootFiles.First(file =>
            string.Equals(file.Name, "WAD.WAD", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(file.Name, "WAD", StringComparison.OrdinalIgnoreCase));
        IsoRootRecord executable = rootFiles.First(file => IsExecutableName(file.Name));
        if (wadFile.Lba != wad.WadLba || wadFile.Size != wad.WadSize)
            throw new InvalidDataException("The live WAD extent no longer matches the selected WAD analysis.");
        ValidateLiveWadEntryTable(source, discLayout, wad);
        File.Copy(sourceImagePath, outputImagePath, true);
        using FileStream output = File.Open(outputImagePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
        byte[] executableBytes = DiscImage.ReadFileBytes(source, discLayout, executable.Lba, 0, executable.Size);
        byte[] wadHeader = DiscImage.ReadFileBytes(source, discLayout, wad.WadLba, 0, SectorBytes);
        Dictionary<int, NativeSkyRelocationPayload[]> payloadsByEntry = payloads
            .GroupBy(payload => payload.StorageWadEntry)
            .ToDictionary(group => group.Key, group => group.OrderBy(payload => payload.ModelBlockOffset).ToArray());

        foreach (WadEntry entry in wad.Entries.OrderBy(entry => entry.Offset))
        {
            byte[] entryBytes = DiscImage.ReadFileBytes(source, discLayout, wad.WadLba, entry.Offset, entry.Size);
            byte[] relocated = payloadsByEntry.TryGetValue(entry.Index, out NativeSkyRelocationPayload[]? entryPayloads)
                ? ExpandNestedLevelEntry(entryBytes, entryPayloads, plan.EntryGrowthBytes.GetValueOrDefault(entry.Index))
                : entryBytes;
            long relocatedOffset = plan.RelocatedEntryOffsets[entry.Index];
            WriteUInt32(wadHeader, entry.Index * 8, checked((uint)relocatedOffset));
            WriteUInt32(wadHeader, (entry.Index * 8) + 4, checked((uint)relocated.Length));
            DiscImage.WriteFileBytes(output, discLayout, wad.WadLba, relocatedOffset, relocated);
        }
        DiscImage.WriteFileBytes(output, discLayout, wad.WadLba, 0, wadHeader);
        DiscImage.WriteFileBytes(output, discLayout, plan.RelocatedExecutableLba, 0, executableBytes);

        byte[] rootDirectory = DiscImage.ReadFileBytes(output, discLayout, discLayout.RootExtent, 0, discLayout.RootLength);
        PatchRootRecord(rootDirectory, wadFile.Name, wadFile.Lba, plan.ExpandedWadSize);
        PatchRootRecord(rootDirectory, executable.Name, plan.RelocatedExecutableLba, executable.Size);
        DiscImage.WriteFileBytes(output, discLayout, discLayout.RootExtent, 0, rootDirectory);
        output.Flush();

        IReadOnlyList<IsoRootRecord> relocatedRootFiles = ReadRootFiles(output, discLayout);
        IsoRootRecord relocatedWad = relocatedRootFiles.Single(file => string.Equals(file.Name, wadFile.Name, StringComparison.OrdinalIgnoreCase));
        IsoRootRecord relocatedExecutable = relocatedRootFiles.Single(file => string.Equals(file.Name, executable.Name, StringComparison.OrdinalIgnoreCase));
        if (relocatedWad.Lba != plan.WadLba || relocatedWad.Size != plan.ExpandedWadSize ||
            relocatedExecutable.Lba != plan.RelocatedExecutableLba || relocatedExecutable.Size != executable.Size ||
            !relocatedRootFiles.Any(file => file.Lba == plan.NextFileLba))
        {
            throw new InvalidDataException("Expanded ISO directory readback does not match the relocation plan.");
        }
        byte[] relocatedExecutableBytes = DiscImage.ReadFileBytes(output, discLayout, relocatedExecutable.Lba, 0, relocatedExecutable.Size);
        if (!relocatedExecutableBytes.AsSpan().SequenceEqual(executableBytes))
            throw new InvalidDataException("The relocated executable does not match its original bytes.");
    }

    internal static byte[] ExpandNestedLevelEntry(
        byte[] entry,
        IReadOnlyList<NativeSkyRelocationPayload> payloads,
        int entryGrowth)
    {
        IReadOnlyList<NestedSubfile> subfiles = ParseNestedSubfiles(entry.AsSpan(0, Math.Min(entry.Length, SectorBytes)).ToArray(), entry.Length);
        int[] requestedSubfiles = payloads.Select(payload => payload.SubfileIndex).Distinct().ToArray();
        if (requestedSubfiles.Length != 1)
            throw new InvalidOperationException("A nested-entry relocation batch must target exactly one subfile.");
        NestedSubfile model = subfiles.Single(subfile => subfile.Index == requestedSubfiles[0]);
        byte[] modelBytes = entry.AsSpan(model.Offset, model.Size).ToArray();
        int exactDelta = payloads.Sum(payload => payload.Bytes.Length - payload.OriginalLength);
        if (entryGrowth != AlignSector(exactDelta))
            throw new InvalidDataException("Nested entry growth does not match its sky payloads.");

        byte[] expandedContent = new byte[checked(model.Size + exactDelta)];
        int sourceCursor = 0;
        int outputCursor = 0;
        foreach (NativeSkyRelocationPayload payload in payloads)
        {
            int unchanged = payload.ModelBlockOffset - sourceCursor;
            modelBytes.AsSpan(sourceCursor, unchanged).CopyTo(expandedContent.AsSpan(outputCursor));
            sourceCursor += unchanged;
            outputCursor += unchanged;
            if (payload.RequireLengthPrefix)
            {
                if (sourceCursor + 4 > modelBytes.Length ||
                    BinaryPrimitives.ReadInt32LittleEndian(modelBytes.AsSpan(sourceCursor, 4)) != payload.OriginalLength)
                {
                    throw new InvalidDataException($"Relocation patch {payload.PatchIndex} no longer matches its source block length.");
                }
            }
            payload.Bytes.CopyTo(expandedContent, outputCursor);
            sourceCursor += payload.OriginalLength;
            outputCursor += payload.Bytes.Length;
        }
        modelBytes.AsSpan(sourceCursor).CopyTo(expandedContent.AsSpan(outputCursor));

        int originalModelEnd = checked(model.Offset + model.Size);
        byte[] expandedEntry = new byte[checked(entry.Length + entryGrowth)];
        entry.AsSpan(0, model.Offset).CopyTo(expandedEntry);
        expandedContent.CopyTo(expandedEntry, model.Offset);
        entry.AsSpan(originalModelEnd).CopyTo(expandedEntry.AsSpan(originalModelEnd + entryGrowth));

        foreach (NestedSubfile subfile in subfiles)
        {
            if (subfile.Index == model.Index)
                WriteUInt32(expandedEntry, (subfile.Index * 8) + 4, checked((uint)(subfile.Size + entryGrowth)));
            else if (subfile.Index > model.Index)
                WriteUInt32(expandedEntry, subfile.Index * 8, checked((uint)(subfile.Offset + entryGrowth)));
        }

        int tableEnd = subfiles.Count * 8;
        int headerEnd = subfiles[0].Offset;
        for (int offset = tableEnd; offset <= headerEnd - 4; offset += 4)
        {
            uint value = BinaryPrimitives.ReadUInt32LittleEndian(entry.AsSpan(offset, 4));
            if (value < model.Offset || value >= entry.Length)
                continue;
            int relocated = RelocateNestedPointer(checked((int)value), model, payloads, exactDelta, entryGrowth);
            WriteUInt32(expandedEntry, offset, checked((uint)relocated));
        }
        return expandedEntry;
    }

    private static int RelocateNestedPointer(
        int value,
        NestedSubfile model,
        IReadOnlyList<NativeSkyRelocationPayload> payloads,
        int exactDelta,
        int entryGrowth)
    {
        int originalModelEnd = model.Offset + model.Size;
        if (value >= originalModelEnd)
            return checked(value + entryGrowth);

        int relative = value - model.Offset;
        int shift = 0;
        foreach (NativeSkyRelocationPayload payload in payloads)
        {
            if (relative < payload.ModelBlockOffset)
                break;
            if (relative < payload.ModelBlockOffset + payload.OriginalLength)
                throw new InvalidDataException($"Nested header pointer 0x{value:X} enters replaced sky block {payload.PatchIndex}.");
            shift = checked(shift + payload.Bytes.Length - payload.OriginalLength);
        }
        if (shift > exactDelta)
            throw new InvalidDataException("Nested model pointer relocation exceeded the model growth.");
        return checked(value + shift);
    }

    private static IReadOnlyList<NestedSubfile> ParseNestedSubfiles(byte[] header, int entrySize)
    {
        List<NestedSubfile> subfiles = new();
        for (int index = 0; index < header.Length / 8; index++)
        {
            int offset = checked((int)ReadUInt32(header, index * 8));
            int size = checked((int)ReadUInt32(header, (index * 8) + 4));
            if (offset <= 0 || size <= 0 || offset + (long)size > entrySize)
                break;
            if (index == 0 && offset != SectorBytes)
                throw new InvalidDataException("Nested level archive does not begin at one sector.");
            if (subfiles.Count > 0 && offset != subfiles[^1].Offset + subfiles[^1].Size)
                throw new InvalidDataException($"Nested subfile {index} is not packed directly after subfile {index - 1}.");
            subfiles.Add(new NestedSubfile(index, offset, size));
        }
        if (subfiles.Count == 0)
            throw new InvalidDataException("Nested level archive contains no packed subfiles.");
        return subfiles;
    }

    private static WadLayout LoadWadLayout(string path)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement root = document.RootElement;
        int wadLba = JsonValue.GetInt32(root.GetProperty("wad"), "lba", 37);
        int wadSize = JsonValue.GetInt32(root.GetProperty("wad"), "size", 0);
        WadEntry[] entries = root.GetProperty("entries")
            .EnumerateArray()
            .Select(element => new WadEntry(
                JsonValue.GetInt32(element, "index", -1),
                JsonValue.GetInt64(element, "offset", -1),
                JsonValue.GetInt32(element, "size", -1)))
            .Where(entry => entry.Index >= 0 && entry.Offset >= SectorBytes && entry.Size > 0)
            .OrderBy(entry => entry.Offset)
            .ToArray();
        if (entries.Length == 0 || entries[^1].Offset + entries[^1].Size != wadSize)
            throw new InvalidDataException("WAD analysis does not cover the complete archive.");
        return new WadLayout(wadLba, wadSize, entries, entries.ToDictionary(entry => entry.Index));
    }

    /// <summary>
    /// Binds every analyzed WAD entry boundary to the selected source archive.
    /// Matching only the outer ISO extent is insufficient: a stale analysis can
    /// have the same total WAD size while repartitioning later entries, and the
    /// whole-WAD relocation writer would otherwise copy those wrong slices.
    /// </summary>
    private static void ValidateLiveWadEntryTable(
        FileStream image,
        DiscLayout layout,
        WadLayout analysis)
    {
        byte[] header = DiscImage.ReadFileBytes(
            image,
            layout,
            analysis.WadLba,
            0,
            SectorBytes);
        int firstDataOffset = checked((int)ReadUInt32(header, 0));
        if (firstDataOffset <= 0 || firstDataOffset > header.Length || firstDataOffset % 8 != 0)
        {
            throw new InvalidDataException(
                "The live WAD archive header has an invalid first-entry boundary.");
        }

        Dictionary<int, WadEntry> liveEntries = [];
        for (int tableOffset = 0; tableOffset < firstDataOffset; tableOffset += 8)
        {
            int index = tableOffset / 8;
            long offset = ReadUInt32(header, tableOffset);
            int size = checked((int)ReadUInt32(header, tableOffset + 4));
            if (offset == 0 && size == 0)
                continue;
            if (offset < SectorBytes || size <= 0 || offset + size > analysis.WadSize)
            {
                throw new InvalidDataException(
                    $"The live WAD archive header contains an invalid entry {index} boundary 0x{offset:X}+0x{size:X}.");
            }
            liveEntries[index] = new WadEntry(index, offset, size);
        }

        if (liveEntries.Count != analysis.Entries.Count)
        {
            throw new InvalidDataException(
                $"The selected WAD analysis has {analysis.Entries.Count} entries, but the live WAD archive header has {liveEntries.Count}.");
        }
        foreach (WadEntry expected in analysis.Entries)
        {
            if (!liveEntries.TryGetValue(expected.Index, out WadEntry? actual) ||
                actual.Offset != expected.Offset ||
                actual.Size != expected.Size)
            {
                string live = actual == null
                    ? "missing"
                    : $"0x{actual.Offset:X}+0x{actual.Size:X}";
                throw new InvalidDataException(
                    $"WAD analysis entry {expected.Index} boundary 0x{expected.Offset:X}+0x{expected.Size:X} does not match the live WAD archive header ({live}).");
            }
        }
    }

    private static IReadOnlyList<IsoRootRecord> ReadRootFiles(FileStream image, DiscLayout layout)
    {
        byte[] directory = DiscImage.ReadFileBytes(image, layout, layout.RootExtent, 0, layout.RootLength);
        List<IsoRootRecord> result = new();
        for (int offset = 0; offset < directory.Length;)
        {
            int recordLength = directory[offset];
            if (recordLength == 0)
            {
                offset = ((offset / SectorBytes) + 1) * SectorBytes;
                continue;
            }
            if (recordLength < 34 || offset + recordLength > directory.Length)
                break;
            int nameLength = directory[offset + 32];
            string name = Encoding.ASCII.GetString(directory, offset + 33, nameLength)
                .Replace(";1", "", StringComparison.OrdinalIgnoreCase);
            if (name is not "\0" and not "\u0001")
            {
                result.Add(new IsoRootRecord(
                    name,
                    checked((int)ReadUInt32(directory, offset + 2)),
                    checked((int)ReadUInt32(directory, offset + 10)),
                    offset));
            }
            offset += recordLength;
        }
        return result;
    }

    private static void PatchRootRecord(byte[] directory, string name, int lba, int size)
    {
        IsoRootRecord record = FindRootRecord(directory, name);
        WriteBothEndianUInt32(directory, record.RecordOffset + 2, checked((uint)lba));
        WriteBothEndianUInt32(directory, record.RecordOffset + 10, checked((uint)size));
    }

    private static IsoRootRecord FindRootRecord(byte[] directory, string expectedName)
    {
        for (int offset = 0; offset < directory.Length;)
        {
            int recordLength = directory[offset];
            if (recordLength == 0)
            {
                offset = ((offset / SectorBytes) + 1) * SectorBytes;
                continue;
            }
            if (recordLength < 34 || offset + recordLength > directory.Length)
                break;
            int nameLength = directory[offset + 32];
            string name = Encoding.ASCII.GetString(directory, offset + 33, nameLength)
                .Replace(";1", "", StringComparison.OrdinalIgnoreCase);
            if (string.Equals(name, expectedName, StringComparison.OrdinalIgnoreCase))
            {
                return new IsoRootRecord(
                    name,
                    checked((int)ReadUInt32(directory, offset + 2)),
                    checked((int)ReadUInt32(directory, offset + 10)),
                    offset);
            }
            offset += recordLength;
        }
        throw new InvalidDataException($"ISO root record '{expectedName}' was not found.");
    }

    private static bool IsExecutableName(string name) =>
        name.StartsWith("SCUS_", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("SLUS_", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("SLES_", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("SCES_", StringComparison.OrdinalIgnoreCase);

    private static int AlignSector(int value) => value <= 0 ? 0 : checked(DivideRoundUp(value, SectorBytes) * SectorBytes);
    private static int DivideRoundUp(int value, int divisor) => checked((value + divisor - 1) / divisor);
    private static uint ReadUInt32(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
    private static void WriteUInt32(byte[] bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset, 4), value);

    private static void WriteBothEndianUInt32(byte[] bytes, int offset, uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset, 4), value);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(offset + 4, 4), value);
    }

    private sealed record WadLayout(
        int WadLba,
        int WadSize,
        IReadOnlyList<WadEntry> Entries,
        IReadOnlyDictionary<int, WadEntry> EntriesByIndex);
    private sealed record WadEntry(int Index, long Offset, int Size);
    private sealed record NestedSubfile(int Index, int Offset, int Size);
    private sealed record IsoRootRecord(string Name, int Lba, int Size, int RecordOffset);
}
