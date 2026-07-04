using System.Text.Json;

namespace Spyro.Editor.Core.Exporting;

public sealed record WadAnalysisBuildResult(string OutputPath, int EntryCount);

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
}
