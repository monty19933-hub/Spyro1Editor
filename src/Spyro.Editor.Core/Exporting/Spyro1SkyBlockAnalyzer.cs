using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Exporting;

public sealed record Spyro1SkyPartLayout(
    int Index,
    int Pointer,
    int PartOffset,
    int ByteLength,
    short GlobalX,
    short GlobalY,
    short GlobalZ,
    int VertexCount,
    int ColorCount,
    int PolygonCount,
    int VertexTableOffset,
    int ColorTableOffset,
    int PolygonTableOffset);

public sealed record Spyro1SkyBlockLayout(
    int Index,
    int BlockOffset,
    int ByteLength,
    int EndOffsetExclusive,
    string BackgroundColor,
    int BackgroundColorOffset,
    int PartCount,
    int VertexCount,
    int ColorCount,
    int PolygonCount,
    int PaletteWordCount,
    int TrailingBytes,
    string Sha256,
    string GeometrySha256,
    IReadOnlyList<Spyro1SkyPartLayout> Parts);

public sealed record Spyro1SkyBlockReference(
    string LevelKey,
    string LevelName,
    int WadEntry,
    int BlockIndex,
    int BlockOffset,
    int ByteLength,
    bool IsPrimary);

public sealed record Spyro1LevelSkyBlockLayout(
    string Key,
    string DisplayName,
    int LevelId,
    int WadEntry,
    long ModelSubfileWadOffset,
    int ModelSubfileSize,
    IReadOnlyList<int> JumpChainOffsets,
    int FirstSkyOffset,
    int SkyBlockCount,
    int TotalPaletteWordCount,
    IReadOnlyList<Spyro1SkyBlockLayout> SkyBlocks,
    IReadOnlyList<string> CapacityCompatiblePrimaryDonorKeys,
    IReadOnlyList<string> GeometryMatchingPrimaryLevelKeys,
    IReadOnlyList<Spyro1SkyBlockReference> LinkedPrimarySkyCopies);

public sealed record Spyro1SkyVerifiedPaletteScore(
    string LevelKey,
    int VerifiedWordCount,
    int ParsedPaletteWordCount,
    int VerifiedParsedWordCount,
    int MissingVerifiedWordCount,
    int ParsedUnverifiedWordCount,
    double Recall,
    double Precision);

public sealed record Spyro1SkyBlockReport(
    DateTimeOffset GeneratedAt,
    string SourceImageFileName,
    int WadLba,
    int LevelCount,
    int ModelSubfileCoverage,
    int LevelsWithSkyBlocks,
    int TotalSkyBlocks,
    IReadOnlyList<Spyro1LevelSkyBlockLayout> Levels,
    Spyro1SkyVerifiedPaletteScore StoneHillVerification,
    IReadOnlyList<string> SafetyNotes);

public sealed record Spyro1SkyBlockWriteResult(string JsonPath, string MarkdownPath);

public static class Spyro1SkyBlockAnalyzer
{
    private const int ModelSubfileIndex = 1;
    private const int MaximumJumpCount = 32;

    public static Spyro1SkyBlockReport Analyze(string imagePath, string wadAnalysisPath, LevelCatalog catalog)
    {
        if (!File.Exists(imagePath))
            throw new FileNotFoundException("Missing source disc image.", imagePath);
        if (!File.Exists(wadAnalysisPath))
            throw new FileNotFoundException("Missing WAD analysis.", wadAnalysisPath);

        using FileStream analysisStream = File.OpenRead(wadAnalysisPath);
        using JsonDocument analysis = JsonDocument.Parse(analysisStream);
        int wadLba = JsonValue.GetInt32(analysis.RootElement.GetProperty("wad"), "lba", 37);
        Dictionary<int, WadModelSubfile> modelSubfiles = LoadModelSubfiles(analysis.RootElement);
        DiscLayout layout = DiscImage.DetectLayout(imagePath);
        using FileStream image = File.Open(imagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        List<MutableLevelLayout> mutableLevels = new();
        foreach (LevelDefinition level in catalog.Levels)
        {
            if (!modelSubfiles.TryGetValue(level.SourceWadEntry, out WadModelSubfile? modelSubfile))
                continue;
            if (modelSubfile.Size > int.MaxValue)
                throw new InvalidOperationException($"{level.DisplayName} model subfile is too large to inspect.");
            byte[] bytes = DiscImage.ReadFileBytes(image, layout, wadLba, modelSubfile.WadOffset, checked((int)modelSubfile.Size));
            (IReadOnlyList<int> jumps, int firstSkyOffset, IReadOnlyList<ParsedSkyBlock> blocks) = ParseSkyBlocks(bytes);
            Spyro1SkyBlockLayout[] blockLayouts = blocks
                .Select((block, index) => ToLayout(bytes, block, index))
                .ToArray();
            mutableLevels.Add(new MutableLevelLayout(level, modelSubfile, jumps, firstSkyOffset, blockLayouts));
        }

        List<Spyro1LevelSkyBlockLayout> levels = new();
        foreach (MutableLevelLayout mutable in mutableLevels)
        {
            Spyro1SkyBlockLayout? primary = mutable.Blocks.FirstOrDefault();
            string[] capacityDonors = primary == null
                ? []
                : mutableLevels
                    .Where(candidate => candidate.Level.Key != mutable.Level.Key && candidate.Blocks.FirstOrDefault()?.ByteLength <= primary.ByteLength)
                    .Select(candidate => candidate.Level.Key)
                    .OrderBy(key => key, StringComparer.Ordinal)
                    .ToArray();
            string[] geometryMatches = primary == null
                ? []
                : mutableLevels
                    .Where(candidate => candidate.Level.Key != mutable.Level.Key &&
                        candidate.Blocks.FirstOrDefault()?.GeometrySha256 == primary.GeometrySha256)
                    .Select(candidate => candidate.Level.Key)
                    .OrderBy(key => key, StringComparer.Ordinal)
                    .ToArray();
            Spyro1SkyBlockReference[] linkedCopies = primary == null
                ? []
                : mutableLevels
                    .SelectMany(candidate => candidate.Blocks.Select(block => new { Candidate = candidate, Block = block }))
                    .Where(item => item.Block.Sha256 == primary.Sha256 &&
                        (item.Candidate.Level.Key == mutable.Level.Key || item.Candidate.Blocks.Count > 1))
                    .Select(item => new Spyro1SkyBlockReference(
                        LevelKey: item.Candidate.Level.Key,
                        LevelName: item.Candidate.Level.DisplayName,
                        WadEntry: item.Candidate.Level.SourceWadEntry,
                        BlockIndex: item.Block.Index,
                        BlockOffset: item.Block.BlockOffset,
                        ByteLength: item.Block.ByteLength,
                        IsPrimary: item.Block.Index == 0))
                    .OrderBy(reference => reference.WadEntry)
                    .ThenBy(reference => reference.BlockIndex)
                    .ToArray();
            levels.Add(new Spyro1LevelSkyBlockLayout(
                Key: mutable.Level.Key,
                DisplayName: mutable.Level.DisplayName,
                LevelId: mutable.Level.LevelId,
                WadEntry: mutable.Level.SourceWadEntry,
                ModelSubfileWadOffset: mutable.ModelSubfile.WadOffset,
                ModelSubfileSize: checked((int)mutable.ModelSubfile.Size),
                JumpChainOffsets: mutable.JumpChainOffsets,
                FirstSkyOffset: mutable.FirstSkyOffset,
                SkyBlockCount: mutable.Blocks.Count,
                TotalPaletteWordCount: mutable.Blocks.Sum(block => block.PaletteWordCount),
                SkyBlocks: mutable.Blocks,
                CapacityCompatiblePrimaryDonorKeys: capacityDonors,
                GeometryMatchingPrimaryLevelKeys: geometryMatches,
                LinkedPrimarySkyCopies: linkedCopies));
        }

        Spyro1SkyVerifiedPaletteScore stoneHillScore = BuildStoneHillVerification(levels);
        return new Spyro1SkyBlockReport(
            GeneratedAt: DateTimeOffset.UtcNow,
            SourceImageFileName: Path.GetFileName(imagePath),
            WadLba: wadLba,
            LevelCount: catalog.Levels.Count,
            ModelSubfileCoverage: levels.Count,
            LevelsWithSkyBlocks: levels.Count(level => level.SkyBlockCount > 0),
            TotalSkyBlocks: levels.Sum(level => level.SkyBlockCount),
            Levels: levels,
            StoneHillVerification: stoneHillScore,
            SafetyNotes:
            [
                "Sky blocks are parsed from the model-subfile jump chain and validated through their part pointer tables and explicit vertex/color/polygon counts.",
                "Palette edits may target only the background RGB word and parsed sky color tables while preserving each fourth command byte.",
                "A donor no larger than the target is capacity-compatible for an in-place research swap; this alone does not prove occlusion or runtime behavior.",
                "Geometry hashes ignore background RGB and per-vertex RGB while preserving structure and command bytes.",
                "This analyzer is read-only and does not patch BIN/CUE files."
            ]);
    }

    private static (IReadOnlyList<int> Jumps, int FirstSkyOffset, IReadOnlyList<ParsedSkyBlock> Blocks) ParseSkyBlocks(byte[] bytes)
    {
        List<int> jumpOffsets = [0];
        int cursor = 0;
        ParsedSkyBlock? first = null;
        for (int guard = 0; guard < MaximumJumpCount; guard++)
        {
            if (TryParseSkyBlock(bytes, cursor, out ParsedSkyBlock? candidate))
            {
                first = candidate;
                break;
            }

            uint jump = ReadUInt32(bytes, cursor);
            if (jump == 0 || jump > int.MaxValue || cursor + (long)jump > bytes.Length - 16)
                break;
            int next = cursor + checked((int)jump);
            if (next <= cursor || (next & 3) != 0)
                break;
            cursor = next;
            jumpOffsets.Add(cursor);
        }

        if (first == null)
            return (jumpOffsets, -1, Array.Empty<ParsedSkyBlock>());

        List<ParsedSkyBlock> blocks = [first];
        int scan = Align4(first.EndOffsetExclusive);
        while (scan <= bytes.Length - 24)
        {
            if (TryParseSkyBlock(bytes, scan, out ParsedSkyBlock? candidate))
            {
                blocks.Add(candidate!);
                scan = Align4(candidate!.EndOffsetExclusive);
                continue;
            }
            scan += 4;
        }
        return (jumpOffsets, first.BlockOffset, blocks);
    }

    private static bool TryParseSkyBlock(byte[] bytes, int blockOffset, out ParsedSkyBlock? block)
    {
        block = null;
        if (blockOffset < 0 || blockOffset + 24 > bytes.Length || (blockOffset & 3) != 0)
            return false;
        uint rawSize = ReadUInt32(bytes, blockOffset);
        if (rawSize < 32 || rawSize > int.MaxValue || (rawSize & 3) != 0)
            return false;
        int byteLength = checked((int)rawSize);
        int blockEnd = blockOffset + byteLength;
        if (blockEnd < blockOffset || blockEnd > bytes.Length)
            return false;

        int payloadStart = blockOffset + 4;
        uint rawPartCount = ReadUInt32(bytes, payloadStart + 4);
        if (rawPartCount == 0 || rawPartCount > 1024)
            return false;
        int partCount = checked((int)rawPartCount);
        int expectedFirstPointer = 8 + (partCount * 4);
        if (payloadStart + expectedFirstPointer > blockEnd || ReadUInt32(bytes, payloadStart + 8) != expectedFirstPointer)
            return false;

        List<ParsedSkyPart> parts = new();
        int previousPointer = -1;
        for (int index = 0; index < partCount; index++)
        {
            uint rawPointer = ReadUInt32(bytes, payloadStart + 8 + (index * 4));
            if (rawPointer > int.MaxValue)
                return false;
            int pointer = checked((int)rawPointer);
            if (pointer < expectedFirstPointer || pointer <= previousPointer || payloadStart + pointer + 24 > blockEnd)
                return false;
            int partOffset = payloadStart + pointer;
            int vertexCount = ReadUInt16(bytes, partOffset + 12);
            int polygonCount = ReadUInt16(bytes, partOffset + 16);
            int colorCount = ReadUInt16(bytes, partOffset + 18);
            if (vertexCount <= 0 || colorCount <= 0 || polygonCount <= 0 ||
                ReadUInt16(bytes, partOffset + 20) != 0xFFFF || ReadUInt16(bytes, partOffset + 22) != 0xFFFF)
                return false;
            long partLength = 24L + (vertexCount * 4L) + (colorCount * 4L) + (polygonCount * 8L);
            if (partLength > int.MaxValue || partOffset + partLength > blockEnd)
                return false;
            if (index + 1 < partCount)
            {
                uint nextPointer = ReadUInt32(bytes, payloadStart + 8 + ((index + 1) * 4));
                if (payloadStart + nextPointer < partOffset + partLength)
                    return false;
            }
            parts.Add(new ParsedSkyPart(
                Index: index,
                Pointer: pointer,
                PartOffset: partOffset,
                ByteLength: checked((int)partLength),
                GlobalX: unchecked((short)ReadUInt16(bytes, partOffset + 14)),
                GlobalY: unchecked((short)ReadUInt16(bytes, partOffset + 8)),
                GlobalZ: unchecked((short)ReadUInt16(bytes, partOffset + 10)),
                VertexCount: vertexCount,
                ColorCount: colorCount,
                PolygonCount: polygonCount));
            previousPointer = pointer;
        }

        int usedEnd = parts.Max(part => part.PartOffset + part.ByteLength);
        block = new ParsedSkyBlock(
            BlockOffset: blockOffset,
            ByteLength: byteLength,
            EndOffsetExclusive: blockEnd,
            BackgroundColorOffset: payloadStart,
            PartCount: partCount,
            UsedEndOffsetExclusive: usedEnd,
            Parts: parts);
        return true;
    }

    public static bool IsValidStandaloneSkyBlock(byte[] bytes)
    {
        return TryParseSkyBlock(bytes, 0, out ParsedSkyBlock? block) && block?.ByteLength == bytes.Length;
    }

    public static byte[] NormalizeStandaloneSkyFile(byte[] input)
    {
        if (input.Length < 24)
            throw new InvalidDataException("The .sky file is too small to contain a Spyro 1 sky block.");
        if (IsValidStandaloneSkyBlock(input))
            return input.ToArray();

        byte[] wrapped = new byte[input.Length + 4];
        BitConverter.GetBytes(wrapped.Length).CopyTo(wrapped, 0);
        input.CopyTo(wrapped, 4);
        if (IsValidStandaloneSkyBlock(wrapped))
            return wrapped;

        throw new InvalidDataException("The file is not a compatible Spyro 1 .sky block or payload.");
    }

    private static Spyro1SkyBlockLayout ToLayout(byte[] modelBytes, ParsedSkyBlock block, int index)
    {
        byte[] exact = modelBytes.AsSpan(block.BlockOffset, block.ByteLength).ToArray();
        byte[] geometry = exact.ToArray();
        ZeroRgb(geometry, block.BackgroundColorOffset - block.BlockOffset);
        List<Spyro1SkyPartLayout> parts = new();
        foreach (ParsedSkyPart part in block.Parts)
        {
            int vertexTableOffset = part.PartOffset + 24;
            int colorTableOffset = vertexTableOffset + (part.VertexCount * 4);
            int polygonTableOffset = colorTableOffset + (part.ColorCount * 4);
            for (int color = 0; color < part.ColorCount; color++)
                ZeroRgb(geometry, colorTableOffset - block.BlockOffset + (color * 4));
            parts.Add(new Spyro1SkyPartLayout(
                Index: part.Index,
                Pointer: part.Pointer,
                PartOffset: part.PartOffset,
                ByteLength: part.ByteLength,
                GlobalX: part.GlobalX,
                GlobalY: part.GlobalY,
                GlobalZ: part.GlobalZ,
                VertexCount: part.VertexCount,
                ColorCount: part.ColorCount,
                PolygonCount: part.PolygonCount,
                VertexTableOffset: vertexTableOffset,
                ColorTableOffset: colorTableOffset,
                PolygonTableOffset: polygonTableOffset));
        }

        return new Spyro1SkyBlockLayout(
            Index: index,
            BlockOffset: block.BlockOffset,
            ByteLength: block.ByteLength,
            EndOffsetExclusive: block.EndOffsetExclusive,
            BackgroundColor: ToColor(modelBytes, block.BackgroundColorOffset),
            BackgroundColorOffset: block.BackgroundColorOffset,
            PartCount: block.PartCount,
            VertexCount: parts.Sum(part => part.VertexCount),
            ColorCount: parts.Sum(part => part.ColorCount),
            PolygonCount: parts.Sum(part => part.PolygonCount),
            PaletteWordCount: 1 + parts.Sum(part => part.ColorCount),
            TrailingBytes: block.EndOffsetExclusive - block.UsedEndOffsetExclusive,
            Sha256: Convert.ToHexString(SHA256.HashData(exact)),
            GeometrySha256: Convert.ToHexString(SHA256.HashData(geometry)),
            Parts: parts);
    }

    private static Spyro1SkyVerifiedPaletteScore BuildStoneHillVerification(IReadOnlyList<Spyro1LevelSkyBlockLayout> levels)
    {
        HashSet<int> verified = BuildVerifiedStoneHillLoadedWordOffsets();
        Spyro1LevelSkyBlockLayout? stoneHill = levels.FirstOrDefault(level => level.Key == "stonehill");
        HashSet<int> parsed = stoneHill == null
            ? []
            : stoneHill.SkyBlocks
                .SelectMany(block => new[] { block.BackgroundColorOffset }.Concat(
                    block.Parts.SelectMany(part => Enumerable.Range(0, part.ColorCount)
                        .Select(color => part.ColorTableOffset + (color * 4)))))
                .ToHashSet();
        int overlap = verified.Count(parsed.Contains);
        return new Spyro1SkyVerifiedPaletteScore(
            LevelKey: "stonehill",
            VerifiedWordCount: verified.Count,
            ParsedPaletteWordCount: parsed.Count,
            VerifiedParsedWordCount: overlap,
            MissingVerifiedWordCount: verified.Count - overlap,
            ParsedUnverifiedWordCount: parsed.Count(offset => !verified.Contains(offset)),
            Recall: verified.Count == 0 ? 0 : Math.Round((double)overlap / verified.Count, 6),
            Precision: parsed.Count == 0 ? 0 : Math.Round((double)overlap / parsed.Count, 6));
    }

    private static HashSet<int> BuildVerifiedStoneHillLoadedWordOffsets()
    {
        HashSet<int> result = new();
        foreach (SkyboxTargetRecord record in SkyboxTargets.ForPreset("StoneHillNightKeeper"))
        {
            foreach (SkyboxTargetPair pair in record.Pairs().Where(pair => pair.Entry == 12 && pair.Subfile == ModelSubfileIndex))
            {
                for (int word = 0; word < record.WordCount; word++)
                    result.Add(checked((int)(pair.Start + (word * 4L))));
            }
        }
        return result;
    }

    private static Dictionary<int, WadModelSubfile> LoadModelSubfiles(JsonElement root)
    {
        Dictionary<int, WadModelSubfile> result = new();
        foreach (JsonElement entry in root.GetProperty("entries").EnumerateArray())
        {
            int entryIndex = JsonValue.GetInt32(entry, "index", -1);
            long entryOffset = JsonValue.GetInt64(entry, "offset", -1);
            if (entryIndex < 0 || entryOffset < 0 ||
                !entry.TryGetProperty("level", out JsonElement level) || level.ValueKind != JsonValueKind.Object ||
                !level.TryGetProperty("subfiles", out JsonElement subfiles) || subfiles.ValueKind != JsonValueKind.Array)
                continue;
            foreach (JsonElement subfile in subfiles.EnumerateArray())
            {
                if (JsonValue.GetInt32(subfile, "index", -1) != ModelSubfileIndex)
                    continue;
                long relativeOffset = JsonValue.GetInt64(subfile, "offset", -1);
                long size = JsonValue.GetInt64(subfile, "size", -1);
                if (relativeOffset >= 0 && size > 0)
                    result[entryIndex] = new WadModelSubfile(entryOffset + relativeOffset, size);
            }
        }
        return result;
    }

    private static void ZeroRgb(byte[] bytes, int offset)
    {
        if (offset < 0 || offset + 4 > bytes.Length)
            return;
        bytes[offset] = 0;
        bytes[offset + 1] = 0;
        bytes[offset + 2] = 0;
    }

    private static string ToColor(byte[] bytes, int offset) =>
        $"#{bytes[offset]:X2}{bytes[offset + 1]:X2}{bytes[offset + 2]:X2}/0x{bytes[offset + 3]:X2}";
    private static int Align4(int value) => (value + 3) & ~3;
    private static ushort ReadUInt16(byte[] bytes, int offset) =>
        offset >= 0 && offset + 2 <= bytes.Length ? BitConverter.ToUInt16(bytes, offset) : (ushort)0;
    private static uint ReadUInt32(byte[] bytes, int offset) =>
        offset >= 0 && offset + 4 <= bytes.Length ? BitConverter.ToUInt32(bytes, offset) : 0;

    private sealed record WadModelSubfile(long WadOffset, long Size);
    private sealed record ParsedSkyPart(
        int Index,
        int Pointer,
        int PartOffset,
        int ByteLength,
        short GlobalX,
        short GlobalY,
        short GlobalZ,
        int VertexCount,
        int ColorCount,
        int PolygonCount);
    private sealed record ParsedSkyBlock(
        int BlockOffset,
        int ByteLength,
        int EndOffsetExclusive,
        int BackgroundColorOffset,
        int PartCount,
        int UsedEndOffsetExclusive,
        IReadOnlyList<ParsedSkyPart> Parts);
    private sealed record MutableLevelLayout(
        LevelDefinition Level,
        WadModelSubfile ModelSubfile,
        IReadOnlyList<int> JumpChainOffsets,
        int FirstSkyOffset,
        IReadOnlyList<Spyro1SkyBlockLayout> Blocks);
}

public static class Spyro1SkyBlockReportWriter
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

    public static async Task<Spyro1SkyBlockWriteResult> WriteAsync(
        Spyro1SkyBlockReport report,
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
        return new Spyro1SkyBlockWriteResult(jsonPath, markdownPath);
    }

    private static string BuildMarkdown(Spyro1SkyBlockReport report)
    {
        StringBuilder text = new();
        text.AppendLine("# Spyro 1 Native Sky-Block Layout");
        text.AppendLine();
        text.AppendLine($"Coverage: {report.ModelSubfileCoverage}/{report.LevelCount} model subfiles; {report.LevelsWithSkyBlocks}/{report.LevelCount} levels with parsed skies; {report.TotalSkyBlocks} sky blocks");
        text.AppendLine($"Stone Hill verification: {report.StoneHillVerification.VerifiedParsedWordCount}/{report.StoneHillVerification.VerifiedWordCount} verified targets parsed; palette precision {report.StoneHillVerification.Precision:P1}");
        text.AppendLine();
        foreach (string note in report.SafetyNotes)
            text.AppendLine($"- {note}");

        text.AppendLine();
        text.AppendLine("## Level Skies");
        text.AppendLine();
        text.AppendLine("| Level | WAD | First sky | Blocks | Primary bytes | Parts | Vertices | Colors | Polygons | Capacity donors | Linked copies |");
        text.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | --- |");
        foreach (Spyro1LevelSkyBlockLayout level in report.Levels)
        {
            Spyro1SkyBlockLayout? primary = level.SkyBlocks.FirstOrDefault();
            string linked = string.Join(", ", level.LinkedPrimarySkyCopies.Select(copy => $"{copy.LevelKey}#{copy.BlockIndex}"));
            text.AppendLine($"| {Escape(level.DisplayName)} | {level.WadEntry} | {Hex(level.FirstSkyOffset)} | {level.SkyBlockCount} | {primary?.ByteLength ?? 0:N0} | {primary?.PartCount ?? 0} | {primary?.VertexCount ?? 0:N0} | {primary?.ColorCount ?? 0:N0} | {primary?.PolygonCount ?? 0:N0} | {level.CapacityCompatiblePrimaryDonorKeys.Count} | {Escape(linked)} |");
        }

        text.AppendLine();
        text.AppendLine("## Block Details");
        text.AppendLine();
        text.AppendLine("| Level | Block | Offset | Bytes | Background | Parts | Palette words | Trailing | SHA-256 | Geometry SHA-256 |");
        text.AppendLine("| --- | ---: | ---: | ---: | --- | ---: | ---: | ---: | --- | --- |");
        foreach (Spyro1LevelSkyBlockLayout level in report.Levels)
        {
            foreach (Spyro1SkyBlockLayout block in level.SkyBlocks)
                text.AppendLine($"| {Escape(level.DisplayName)} | {block.Index} | 0x{block.BlockOffset:X} | {block.ByteLength:N0} | {block.BackgroundColor} | {block.PartCount} | {block.PaletteWordCount:N0} | {block.TrailingBytes:N0} | `{block.Sha256[..12]}` | `{block.GeometrySha256[..12]}` |");
        }
        return text.ToString();
    }

    private static string Hex(int value) => value < 0 ? "missing" : $"0x{value:X}";
    private static string Escape(string value) => value.Replace("|", "\\|", StringComparison.Ordinal);
}
