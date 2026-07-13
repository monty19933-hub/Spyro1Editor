using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Exporting;

public sealed record SkyboxCandidateSubfileLayout(
    int Index,
    long RelativeOffset,
    long WadOffset,
    long Size,
    long Capacity,
    long SlackBytes,
    string Sha256);

public sealed record SkyboxArchiveLayout(
    int WadEntry,
    long WadOffset,
    long Size,
    int SubfileCount,
    SkyboxCandidateSubfileLayout? CandidateSubfile3);

public sealed record SkyboxNativeLevelLayout(
    string Key,
    string DisplayName,
    int LevelId,
    int LoadedSourceWadEntry,
    SkyboxArchiveLayout LoadedArchive,
    int? MetadataWadEntry,
    int MetadataMatchCount,
    int? MetadataAdjacentArchiveWadEntry,
    SkyboxArchiveLayout? MetadataAdjacentArchive,
    int? LoadedArchivePrecedingMetadataLevelId,
    string LoadedArchivePrecedingMetadataLevelKey,
    string ArchiveRelationship,
    string SafetyClassification);

public sealed record SkyboxCandidateCompatibilityGroup(
    string Scope,
    long Size,
    IReadOnlyList<string> LevelKeys,
    IReadOnlyList<string> LevelNames,
    bool ByteIdentical,
    string Classification);

public sealed record SkyboxNativeLayoutReport(
    DateTimeOffset GeneratedAt,
    string SourceImageFileName,
    int WadLba,
    int WadSize,
    int WadEntryCount,
    int LevelCount,
    int LoadedArchiveCoverage,
    int LoadedCandidateSubfile3Coverage,
    int MetadataCoverage,
    int MetadataAdjacentArchiveCoverage,
    IReadOnlyList<SkyboxNativeLevelLayout> Levels,
    IReadOnlyList<SkyboxCandidateCompatibilityGroup> CompatibilityGroups,
    IReadOnlyList<string> SafetyNotes);

public sealed record SkyboxNativeLayoutWriteResult(string JsonPath, string MarkdownPath);

public static class SkyboxNativeLayoutAnalyzer
{
    private const int CandidateSubfileIndex = 3;

    public static SkyboxNativeLayoutReport Analyze(string imagePath, LevelCatalog catalog)
    {
        if (!File.Exists(imagePath))
            throw new FileNotFoundException("Missing source disc image.", imagePath);
        if (catalog.Levels.Count == 0)
            throw new InvalidOperationException("The level catalog is empty.");

        DiscLayout layout = DiscImage.DetectLayout(imagePath);
        using FileStream image = File.Open(imagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        DiscFileRecord wadFile = DiscImage.FindRootFileRecord(image, layout, name =>
            string.Equals(name, "WAD.WAD", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "WAD", StringComparison.OrdinalIgnoreCase));

        IReadOnlyList<ArchiveEntry> wadEntries = ReadArchiveEntries(image, layout, wadFile.Lba, 0, wadFile.Size);
        Dictionary<int, ArchiveEntry> wadByIndex = wadEntries.ToDictionary(entry => entry.Index);
        Dictionary<int, TopLevelEntryInfo> topLevel = wadEntries.ToDictionary(
            entry => entry.Index,
            entry => ReadTopLevelEntryInfo(image, layout, wadFile.Lba, entry));
        Dictionary<int, LevelDefinition> levelsById = catalog.Levels
            .GroupBy(level => level.LevelId)
            .Where(group => group.Key >= 0 && group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single());

        List<SkyboxNativeLevelLayout> levels = new();
        foreach (LevelDefinition level in catalog.Levels)
        {
            if (!wadByIndex.TryGetValue(level.SourceWadEntry, out ArchiveEntry? loadedEntry))
                throw new InvalidOperationException($"{level.DisplayName} references missing WAD entry {level.SourceWadEntry}.");
            if (!topLevel[level.SourceWadEntry].IsNestedArchive)
                throw new InvalidOperationException($"{level.DisplayName} source WAD entry {level.SourceWadEntry} is not a nested archive.");

            SkyboxArchiveLayout loadedArchive = ReadArchiveLayout(image, layout, wadFile.Lba, loadedEntry);
            TopLevelEntryInfo[] metadataMatches = topLevel.Values
                .Where(entry => entry.IsMetadataCandidate && entry.FirstWord == (uint)level.LevelId)
                .OrderBy(entry => entry.Entry.Index)
                .ToArray();
            TopLevelEntryInfo? metadata = metadataMatches.Length == 1 ? metadataMatches[0] : null;
            TopLevelEntryInfo? metadataAdjacent = metadata != null && topLevel.TryGetValue(metadata.Entry.Index + 1, out TopLevelEntryInfo? adjacent) && adjacent.IsNestedArchive
                ? adjacent
                : null;
            SkyboxArchiveLayout? metadataAdjacentArchive = metadataAdjacent == null
                ? null
                : ReadArchiveLayout(image, layout, wadFile.Lba, metadataAdjacent.Entry);

            int? precedingMetadataLevelId = null;
            string precedingMetadataLevelKey = "";
            if (topLevel.TryGetValue(level.SourceWadEntry - 1, out TopLevelEntryInfo? preceding) && preceding.IsMetadataCandidate)
            {
                precedingMetadataLevelId = checked((int)preceding.FirstWord);
                if (levelsById.TryGetValue(precedingMetadataLevelId.Value, out LevelDefinition? precedingLevel))
                    precedingMetadataLevelKey = precedingLevel.Key;
            }

            string relationship = metadataAdjacentArchive == null
                ? "no-metadata-adjacent-archive"
                : metadataAdjacentArchive.WadEntry == loadedArchive.WadEntry
                    ? "metadata-adjacent-equals-loaded-source"
                    : "metadata-adjacent-differs-from-loaded-source";

            levels.Add(new SkyboxNativeLevelLayout(
                Key: level.Key,
                DisplayName: level.DisplayName,
                LevelId: level.LevelId,
                LoadedSourceWadEntry: level.SourceWadEntry,
                LoadedArchive: loadedArchive,
                MetadataWadEntry: metadata?.Entry.Index,
                MetadataMatchCount: metadataMatches.Length,
                MetadataAdjacentArchiveWadEntry: metadataAdjacentArchive?.WadEntry,
                MetadataAdjacentArchive: metadataAdjacentArchive,
                LoadedArchivePrecedingMetadataLevelId: precedingMetadataLevelId,
                LoadedArchivePrecedingMetadataLevelKey: precedingMetadataLevelKey,
                ArchiveRelationship: relationship,
                SafetyClassification: "read-only-layout-evidence"));
        }

        IReadOnlyList<SkyboxCandidateCompatibilityGroup> compatibilityGroups = BuildCompatibilityGroups(levels);
        return new SkyboxNativeLayoutReport(
            GeneratedAt: DateTimeOffset.UtcNow,
            SourceImageFileName: Path.GetFileName(imagePath),
            WadLba: wadFile.Lba,
            WadSize: wadFile.Size,
            WadEntryCount: wadEntries.Count,
            LevelCount: levels.Count,
            LoadedArchiveCoverage: levels.Count(level => level.LoadedArchive.SubfileCount > 0),
            LoadedCandidateSubfile3Coverage: levels.Count(level => level.LoadedArchive.CandidateSubfile3 != null),
            MetadataCoverage: levels.Count(level => level.MetadataWadEntry.HasValue),
            MetadataAdjacentArchiveCoverage: levels.Count(level => level.MetadataAdjacentArchive != null),
            Levels: levels,
            CompatibilityGroups: compatibilityGroups,
            SafetyNotes:
            [
                "Loaded-source WAD ownership comes from the editor's proven level catalog and is validated against the selected disc.",
                "Metadata-adjacent archives are reported separately because they do not consistently equal the loaded-source archive.",
                "Subfile 3 is retained only as a legacy sky/texture research candidate; this report does not claim that it is sky-only.",
                "Same-size candidates are archive-size-compatible only. They are not runtime-safe swap approvals.",
                "The analyzer is read-only and does not create or patch BIN/CUE files."
            ]);
    }

    private static IReadOnlyList<SkyboxCandidateCompatibilityGroup> BuildCompatibilityGroups(IReadOnlyList<SkyboxNativeLevelLayout> levels)
    {
        List<(string Scope, string Key, string Name, SkyboxCandidateSubfileLayout Subfile)> rows = new();
        foreach (SkyboxNativeLevelLayout level in levels)
        {
            if (level.LoadedArchive.CandidateSubfile3 is { } loaded)
                rows.Add(("loaded-source", level.Key, level.DisplayName, loaded));
            if (level.MetadataAdjacentArchive?.CandidateSubfile3 is { } adjacent)
                rows.Add(("metadata-adjacent", level.Key, level.DisplayName, adjacent));
        }

        return rows
            .GroupBy(row => new { row.Scope, row.Subfile.Size })
            .Where(group => group.Count() > 1)
            .OrderBy(group => group.Key.Scope, StringComparer.Ordinal)
            .ThenBy(group => group.Key.Size)
            .Select(group => new SkyboxCandidateCompatibilityGroup(
                Scope: group.Key.Scope,
                Size: group.Key.Size,
                LevelKeys: group.Select(row => row.Key).OrderBy(key => key, StringComparer.Ordinal).ToArray(),
                LevelNames: group.OrderBy(row => row.Name, StringComparer.Ordinal).Select(row => row.Name).ToArray(),
                ByteIdentical: group.Select(row => row.Subfile.Sha256).Distinct(StringComparer.Ordinal).Count() == 1,
                Classification: "archive-size-compatible-only"))
            .ToArray();
    }

    private static TopLevelEntryInfo ReadTopLevelEntryInfo(FileStream image, DiscLayout layout, int wadLba, ArchiveEntry entry)
    {
        int length = checked((int)Math.Min(entry.Size, 16));
        byte[] head = DiscImage.ReadFileBytes(image, layout, wadLba, entry.Offset, length);
        uint first = ReadUInt32(head, 0);
        uint second = ReadUInt32(head, 4);
        bool nested = first == 0x800 && second > first && second < entry.Size;
        bool metadata = first is >= 10 and <= 99 && second is >= 0x80000000 and < 0x80200000;
        return new TopLevelEntryInfo(entry, first, second, nested, metadata);
    }

    private static SkyboxArchiveLayout ReadArchiveLayout(FileStream image, DiscLayout layout, int wadLba, ArchiveEntry archive)
    {
        IReadOnlyList<ArchiveEntry> subfiles = ReadArchiveEntries(image, layout, wadLba, archive.Offset, archive.Size);
        ArchiveEntry? candidate = subfiles.FirstOrDefault(entry => entry.Index == CandidateSubfileIndex);
        SkyboxCandidateSubfileLayout? candidateLayout = null;
        if (candidate != null)
        {
            long capacity = CapacityFor(candidate, subfiles, archive.Size);
            if (candidate.Size > int.MaxValue)
                throw new InvalidOperationException($"WAD entry {archive.Index} subfile {candidate.Index} is too large to hash.");
            byte[] bytes = DiscImage.ReadFileBytes(
                image,
                layout,
                wadLba,
                archive.Offset + candidate.Offset,
                checked((int)candidate.Size));
            candidateLayout = new SkyboxCandidateSubfileLayout(
                Index: candidate.Index,
                RelativeOffset: candidate.Offset,
                WadOffset: archive.Offset + candidate.Offset,
                Size: candidate.Size,
                Capacity: capacity,
                SlackBytes: capacity - candidate.Size,
                Sha256: Convert.ToHexString(SHA256.HashData(bytes)));
        }

        return new SkyboxArchiveLayout(
            WadEntry: archive.Index,
            WadOffset: archive.Offset,
            Size: archive.Size,
            SubfileCount: subfiles.Count,
            CandidateSubfile3: candidateLayout);
    }

    private static long CapacityFor(ArchiveEntry entry, IReadOnlyList<ArchiveEntry> entries, long archiveSize)
    {
        long nextOffset = entries
            .Where(candidate => candidate.Offset > entry.Offset)
            .Select(candidate => candidate.Offset)
            .DefaultIfEmpty(archiveSize)
            .Min();
        return nextOffset - entry.Offset;
    }

    private static IReadOnlyList<ArchiveEntry> ReadArchiveEntries(
        FileStream image,
        DiscLayout layout,
        int wadLba,
        long archiveOffset,
        long archiveSize)
    {
        byte[] first = DiscImage.ReadFileBytes(image, layout, wadLba, archiveOffset, checked((int)Math.Min(archiveSize, 8)));
        long firstDataOffset = ReadUInt32(first, 0);
        if (firstDataOffset <= 0 || firstDataOffset > archiveSize || firstDataOffset > int.MaxValue)
            return Array.Empty<ArchiveEntry>();

        byte[] header = DiscImage.ReadFileBytes(image, layout, wadLba, archiveOffset, checked((int)firstDataOffset));
        List<ArchiveEntry> entries = new();
        for (int offset = 0; offset <= header.Length - 8; offset += 8)
        {
            long fileOffset = ReadUInt32(header, offset);
            long fileSize = ReadUInt32(header, offset + 4);
            if (fileOffset == 0 && fileSize == 0)
                continue;
            if (fileOffset < firstDataOffset || fileSize <= 0 || fileOffset + fileSize > archiveSize)
                continue;
            entries.Add(new ArchiveEntry(offset / 8, fileOffset, fileSize));
        }

        return entries;
    }

    private static uint ReadUInt32(byte[] bytes, int offset)
    {
        return offset >= 0 && offset + 4 <= bytes.Length ? BitConverter.ToUInt32(bytes, offset) : 0;
    }

    private sealed record ArchiveEntry(int Index, long Offset, long Size);
    private sealed record TopLevelEntryInfo(ArchiveEntry Entry, uint FirstWord, uint SecondWord, bool IsNestedArchive, bool IsMetadataCandidate);
}

public static class SkyboxNativeLayoutReportWriter
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

    public static async Task<SkyboxNativeLayoutWriteResult> WriteAsync(
        SkyboxNativeLayoutReport report,
        string jsonPath,
        string markdownPath,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(jsonPath) ?? ".");
        Directory.CreateDirectory(Path.GetDirectoryName(markdownPath) ?? ".");
        await using (FileStream output = File.Create(jsonPath))
        {
            await JsonSerializer.SerializeAsync(output, report, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }, cancellationToken);
        }

        await File.WriteAllTextAsync(markdownPath, BuildMarkdown(report), Utf8NoBom, cancellationToken);
        return new SkyboxNativeLayoutWriteResult(jsonPath, markdownPath);
    }

    private static string BuildMarkdown(SkyboxNativeLayoutReport report)
    {
        StringBuilder text = new();
        text.AppendLine("# Spyro Skybox Native Layout Research");
        text.AppendLine();
        text.AppendLine($"Generated: {report.GeneratedAt:O}");
        text.AppendLine($"Disc file: `{report.SourceImageFileName}`");
        text.AppendLine($"WAD: LBA {report.WadLba}, {report.WadSize:N0} bytes, {report.WadEntryCount} entries");
        text.AppendLine($"Coverage: {report.LoadedArchiveCoverage}/{report.LevelCount} loaded archives, {report.LoadedCandidateSubfile3Coverage}/{report.LevelCount} loaded candidate subfile 3 rows, {report.MetadataCoverage}/{report.LevelCount} metadata IDs");
        text.AppendLine();
        text.AppendLine("## Safety Boundary");
        text.AppendLine();
        foreach (string note in report.SafetyNotes)
            text.AppendLine($"- {note}");

        text.AppendLine();
        text.AppendLine("## Level Layout");
        text.AppendLine();
        text.AppendLine("| Level | ID | Loaded WAD | Loaded subfile 3 | Metadata WAD | Metadata-adjacent WAD | Relationship |");
        text.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: | --- |");
        foreach (SkyboxNativeLevelLayout level in report.Levels)
        {
            string loadedSubfile = level.LoadedArchive.CandidateSubfile3 == null
                ? "missing"
                : $"{level.LoadedArchive.CandidateSubfile3.Size:N0} bytes";
            text.AppendLine($"| {Escape(level.DisplayName)} | {level.LevelId} | {level.LoadedSourceWadEntry} | {loadedSubfile} | {Format(level.MetadataWadEntry)} | {Format(level.MetadataAdjacentArchiveWadEntry)} | {level.ArchiveRelationship} |");
        }

        text.AppendLine();
        text.AppendLine("## Same-Size Research Groups");
        text.AppendLine();
        if (report.CompatibilityGroups.Count == 0)
        {
            text.AppendLine("No repeated candidate subfile sizes were found.");
        }
        else
        {
            text.AppendLine("| Scope | Size | Levels | Byte-identical | Classification |");
            text.AppendLine("| --- | ---: | --- | --- | --- |");
            foreach (SkyboxCandidateCompatibilityGroup group in report.CompatibilityGroups)
                text.AppendLine($"| {group.Scope} | {group.Size:N0} | {Escape(string.Join(", ", group.LevelNames))} | {(group.ByteIdentical ? "yes" : "no")} | {group.Classification} |");
        }

        return text.ToString();
    }

    private static string Format(int? value) => value?.ToString() ?? "missing";
    private static string Escape(string value) => value.Replace("|", "\\|", StringComparison.Ordinal);
}
