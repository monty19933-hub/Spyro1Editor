using System.Text.Json;

namespace Spyro.Editor.Core.Exporting;

public sealed record WadAnalysisBuildResult(string OutputPath, int EntryCount);

public sealed record WadAnalysisCompatibilityResult(
    bool Compatible,
    int EntryCount,
    string Failure);

public sealed record WadAnalysisEnsureResult(
    string OutputPath,
    int EntryCount,
    bool ReusedCompatibleCache);

public static class WadAnalysisBuilder
{
    private const int DefaultWadLba = 37;
    private const int DefaultWadSize = 110260224;

    public static async Task<WadAnalysisBuildResult> BuildAsync(
        string imagePath,
        string outputPath,
        int wadLba = DefaultWadLba,
        int wadSize = DefaultWadSize,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(imagePath))
            throw new FileNotFoundException("Missing source disc image.", imagePath);

        await using FileStream image = File.OpenRead(imagePath);
        DiscLayout layout = DiscImage.DetectLayout(imagePath);
        DiscFileRecord? wadFile = TryFindWadFile(image, layout);
        int actualWadLba = wadFile?.Lba ?? wadLba;
        int actualWadSize = wadFile?.Size ?? wadSize;
        byte[] header = DiscImage.ReadFileBytes(image, layout, actualWadLba, 0, 65536);
        List<WadArchiveEntry> entries = ParseArchiveHeader(header, actualWadSize);
        List<object> summaries = new();

        foreach (WadArchiveEntry entry in entries)
        {
            byte[] head = DiscImage.ReadFileBytes(image, layout, actualWadLba, entry.Offset, checked((int)Math.Min(entry.Size, 64)));
            string kind = GuessEntryKind(entry.Index, entry.Size, head);
            object? level = null;
            if (kind == "level-candidate" || kind == "nested-archive")
                level = ParseLevelCandidate(image, layout, actualWadLba, entry);

            summaries.Add(new
            {
                index = entry.Index,
                offset = entry.Offset,
                size = entry.Size,
                kind,
                firstWords = FirstWords(head),
                level
            });
        }

        var root = new
        {
            wad = new
            {
                lba = actualWadLba,
                size = actualWadSize,
                entryCount = entries.Count
            },
            entries = summaries
        };

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        await using FileStream output = File.Create(outputPath);
        await JsonSerializer.SerializeAsync(output, root, new JsonSerializerOptions { WriteIndented = true }, cancellationToken);
        return new WadAnalysisBuildResult(outputPath, entries.Count);
    }

    /// <summary>
    /// Cheaply binds a cached analysis to its source disc without rescanning
    /// every WAD payload. The ISO WAD extent and every outer archive-header
    /// entry must match exactly; matching only the total archive size is not
    /// enough because a stale cache can repartition later entries.
    /// </summary>
    public static WadAnalysisCompatibilityResult CheckSourceCompatibility(
        string imagePath,
        string analysisPath,
        int wadLba = DefaultWadLba,
        int wadSize = DefaultWadSize)
    {
        if (!File.Exists(imagePath))
            return Incompatible($"Source disc image does not exist: {imagePath}");
        if (!File.Exists(analysisPath))
            return Incompatible($"WAD analysis does not exist: {analysisPath}");

        try
        {
            using FileStream analysisStream = File.OpenRead(analysisPath);
            using JsonDocument document = JsonDocument.Parse(analysisStream);
            CachedWadAnalysis cached = ParseCachedAnalysis(document.RootElement);

            DiscLayout layout = DiscImage.DetectLayout(imagePath);
            using FileStream image = File.Open(imagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            DiscFileRecord? wadFile = TryFindWadFile(image, layout);
            int liveWadLba = wadFile?.Lba ?? wadLba;
            int liveWadSize = wadFile?.Size ?? wadSize;
            if (cached.WadLba != liveWadLba || cached.WadSize != liveWadSize)
            {
                return Incompatible(
                    $"Cached WAD extent LBA {cached.WadLba}, size {cached.WadSize:N0} does not match live LBA {liveWadLba}, size {liveWadSize:N0}.");
            }

            int headerReadLength = checked((int)Math.Min(65536L, liveWadSize));
            byte[] header = DiscImage.ReadFileBytes(image, layout, liveWadLba, 0, headerReadLength);
            IReadOnlyList<WadArchiveEntry> liveEntries = ParseArchiveHeaderStrict(header, liveWadSize);
            if (cached.Entries.Count != liveEntries.Count)
            {
                return Incompatible(
                    $"Cached WAD analysis has {cached.Entries.Count} entries, but the live archive header has {liveEntries.Count}.");
            }

            Dictionary<int, WadArchiveEntry> liveByIndex = liveEntries.ToDictionary(entry => entry.Index);
            foreach (WadArchiveEntry expected in cached.Entries)
            {
                if (!liveByIndex.TryGetValue(expected.Index, out WadArchiveEntry? actual) ||
                    actual.Offset != expected.Offset ||
                    actual.Size != expected.Size)
                {
                    string live = actual == null
                        ? "missing"
                        : $"0x{actual.Offset:X}+0x{actual.Size:X}";
                    return Incompatible(
                        $"Cached WAD entry {expected.Index} boundary 0x{expected.Offset:X}+0x{expected.Size:X} does not match the live archive header ({live}).");
                }
            }

            return new WadAnalysisCompatibilityResult(true, cached.Entries.Count, "");
        }
        catch (Exception ex) when (ex is
            IOException or
            UnauthorizedAccessException or
            JsonException or
            InvalidDataException or
            InvalidOperationException or
            OverflowException)
        {
            return Incompatible(ex.Message);
        }
    }

    /// <summary>
    /// Reuses a source-compatible cache byte-for-byte. Missing, malformed, or
    /// source-incompatible analyses are rebuilt beside the destination and
    /// installed with one same-directory rename so a failed rebuild cannot
    /// truncate the previous cache.
    /// </summary>
    public static async Task<WadAnalysisEnsureResult> EnsureCompatibleAsync(
        string imagePath,
        string outputPath,
        int wadLba = DefaultWadLba,
        int wadSize = DefaultWadSize,
        CancellationToken cancellationToken = default)
    {
        WadAnalysisCompatibilityResult existing = CheckSourceCompatibility(
            imagePath,
            outputPath,
            wadLba,
            wadSize);
        if (existing.Compatible)
        {
            return new WadAnalysisEnsureResult(
                outputPath,
                existing.EntryCount,
                ReusedCompatibleCache: true);
        }

        string fullOutputPath = Path.GetFullPath(outputPath);
        string outputDirectory = Path.GetDirectoryName(fullOutputPath) ?? ".";
        Directory.CreateDirectory(outputDirectory);
        string temporaryPath = Path.Combine(
            outputDirectory,
            $".{Path.GetFileName(fullOutputPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            WadAnalysisBuildResult built = await BuildAsync(
                imagePath,
                temporaryPath,
                wadLba,
                wadSize,
                cancellationToken);
            WadAnalysisCompatibilityResult generated = CheckSourceCompatibility(
                imagePath,
                temporaryPath,
                wadLba,
                wadSize);
            if (!generated.Compatible || generated.EntryCount != built.EntryCount)
            {
                throw new InvalidDataException(
                    $"Generated WAD analysis did not bind back to its source archive: {generated.Failure}");
            }

            File.Move(temporaryPath, fullOutputPath, overwrite: true);
            return new WadAnalysisEnsureResult(
                fullOutputPath,
                generated.EntryCount,
                ReusedCompatibleCache: false);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private static DiscFileRecord? TryFindWadFile(FileStream image, DiscLayout layout)
    {
        try
        {
            return DiscImage.FindRootFileRecord(image, layout, name =>
                string.Equals(name, "WAD.WAD", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "WAD", StringComparison.OrdinalIgnoreCase));
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static object ParseLevelCandidate(FileStream image, DiscLayout layout, int wadLba, WadArchiveEntry entry)
    {
        byte[] head = DiscImage.ReadFileBytes(image, layout, wadLba, entry.Offset, checked((int)Math.Min(entry.Size, 4096)));
        List<WadArchiveEntry> subEntries = ParseArchiveHeader(head, entry.Size);
        return new
        {
            subfileCount = subEntries.Count,
            subfiles = subEntries.Select(sub => new
            {
                index = sub.Index,
                offset = sub.Offset,
                size = sub.Size
            }).ToArray()
        };
    }

    private static List<WadArchiveEntry> ParseArchiveHeader(byte[] bytes, long archiveSize)
    {
        List<WadArchiveEntry> entries = new();
        long firstDataOffset = ReadUInt32(bytes, 0);
        if (firstDataOffset <= 0 || firstDataOffset > bytes.Length)
            firstDataOffset = bytes.Length;

        int headerLimit = checked((int)Math.Min(bytes.Length, firstDataOffset));
        for (int offset = 0; offset <= headerLimit - 8; offset += 8)
        {
            long fileOffset = ReadUInt32(bytes, offset);
            long fileSize = ReadUInt32(bytes, offset + 4);
            if (fileOffset == 0 && fileSize == 0)
                continue;
            if (fileOffset < 0 || fileSize <= 0 || fileOffset + fileSize > archiveSize)
                continue;

            entries.Add(new WadArchiveEntry(offset / 8, fileOffset, fileSize));
        }

        return entries;
    }

    private static IReadOnlyList<WadArchiveEntry> ParseArchiveHeaderStrict(byte[] bytes, long archiveSize)
    {
        long firstDataOffset = ReadUInt32(bytes, 0);
        if (firstDataOffset <= 0 || firstDataOffset > bytes.Length || firstDataOffset % 8 != 0)
            throw new InvalidDataException("The live WAD archive header has an invalid first-entry boundary.");

        List<WadArchiveEntry> entries = new();
        HashSet<int> indexes = [];
        int headerLimit = checked((int)firstDataOffset);
        for (int offset = 0; offset <= headerLimit - 8; offset += 8)
        {
            long fileOffset = ReadUInt32(bytes, offset);
            long fileSize = ReadUInt32(bytes, offset + 4);
            if (fileOffset == 0 && fileSize == 0)
                continue;
            int index = offset / 8;
            if (fileOffset < firstDataOffset || fileSize <= 0 || fileOffset + fileSize > archiveSize)
            {
                throw new InvalidDataException(
                    $"The live WAD archive header contains an invalid entry {index} boundary 0x{fileOffset:X}+0x{fileSize:X}.");
            }
            if (!indexes.Add(index))
                throw new InvalidDataException($"The live WAD archive header contains duplicate entry {index}.");
            entries.Add(new WadArchiveEntry(index, fileOffset, fileSize));
        }
        return entries;
    }

    private static CachedWadAnalysis ParseCachedAnalysis(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("wad", out JsonElement wad) ||
            wad.ValueKind != JsonValueKind.Object ||
            !wad.TryGetProperty("lba", out JsonElement lbaElement) ||
            !lbaElement.TryGetInt32(out int wadLba) || wadLba < 0 ||
            !wad.TryGetProperty("size", out JsonElement sizeElement) ||
            !sizeElement.TryGetInt32(out int wadSize) || wadSize <= 0 ||
            !root.TryGetProperty("entries", out JsonElement entriesElement) ||
            entriesElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("Cached WAD analysis is missing a valid WAD extent or entries array.");
        }

        List<WadArchiveEntry> entries = new();
        HashSet<int> indexes = [];
        foreach (JsonElement element in entriesElement.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object ||
                !element.TryGetProperty("index", out JsonElement indexElement) ||
                !indexElement.TryGetInt32(out int index) || index < 0 ||
                !element.TryGetProperty("offset", out JsonElement offsetElement) ||
                !offsetElement.TryGetInt64(out long offset) || offset < 0 ||
                !element.TryGetProperty("size", out JsonElement entrySizeElement) ||
                !entrySizeElement.TryGetInt64(out long size) || size <= 0 ||
                size > wadSize || offset > wadSize - size)
            {
                throw new InvalidDataException("Cached WAD analysis contains a malformed archive-entry boundary.");
            }
            if (!indexes.Add(index))
                throw new InvalidDataException($"Cached WAD analysis contains duplicate entry {index}.");
            entries.Add(new WadArchiveEntry(index, offset, size));
        }
        if (entries.Count == 0)
            throw new InvalidDataException("Cached WAD analysis contains no archive entries.");
        if (wad.TryGetProperty("entryCount", out JsonElement countElement) &&
            (!countElement.TryGetInt32(out int declaredCount) || declaredCount != entries.Count))
        {
            throw new InvalidDataException("Cached WAD analysis entryCount does not match its entries array.");
        }
        return new CachedWadAnalysis(wadLba, wadSize, entries);
    }

    private static WadAnalysisCompatibilityResult Incompatible(string failure) =>
        new(false, 0, string.IsNullOrWhiteSpace(failure) ? "WAD analysis is not source-compatible." : failure);

    private static string GuessEntryKind(int index, long size, byte[] head)
    {
        uint first = ReadUInt32(head, 0);
        uint second = ReadUInt32(head, 4);
        if (first == 0x800 && second > first && second < size)
            return "nested-archive";
        if (index >= 11 && index <= 79 && index % 2 == 1)
            return "level-candidate";
        if (index >= 4 && index <= 7)
            return "cutscene-candidate";
        if (index >= 83 && index <= 102)
            return "starring-candidate";
        if (size == 524288)
            return "vram-sized";
        return CountZeroRun(head) > 128 ? "zero-padded/unknown" : "unknown";
    }

    private static string[] FirstWords(byte[] bytes)
    {
        return Enumerable.Range(0, 4)
            .Select(index => index * 4 + 4 <= bytes.Length ? $"0x{ReadUInt32(bytes, index * 4):X8}" : "")
            .ToArray();
    }

    private static int CountZeroRun(byte[] bytes)
    {
        int count = 0;
        foreach (byte value in bytes)
        {
            if (value != 0)
                break;
            count++;
        }
        return count;
    }

    private static uint ReadUInt32(byte[] bytes, int offset)
    {
        return offset >= 0 && offset + 4 <= bytes.Length
            ? BitConverter.ToUInt32(bytes, offset)
            : 0;
    }

    private sealed record WadArchiveEntry(int Index, long Offset, long Size);
    private sealed record CachedWadAnalysis(int WadLba, int WadSize, IReadOnlyList<WadArchiveEntry> Entries);
}
