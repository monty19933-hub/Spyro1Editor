using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Music;

namespace Spyro.Editor.Core.Exporting;

public static class LevelMusicPatchExporter
{
    private const int TrackCount = 48;
    private const int MappedLevelCount = 36;
    private const int LateAlternateLevelCount = 35;
    private const int LateAlternateCountPerLevel = 3;
    private const int MappingAndLateAlternateByteCount =
        (TrackCount + LateAlternateLevelCount * LateAlternateCountPerLevel) * sizeof(int);

    public static async Task<LevelMusicBatchPatchResult> ExportBatchAsync(
        LevelMusicBatchPatchRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(request.SourceImagePath))
            throw new FileNotFoundException("Missing source disc image.", request.SourceImagePath);
        if (request.Edits.Count == 0)
            throw new InvalidOperationException("Choose at least one saved level-music edit.");

        string outputPrefix = string.IsNullOrWhiteSpace(request.OutputPrefix)
            ? Path.Combine(Path.GetDirectoryName(request.SourceImagePath) ?? "", "Spyro the Dragon (USA)-level-music")
            : request.OutputPrefix;
        string outputImagePath = $"{outputPrefix}.bin";
        string outputCuePath = $"{outputPrefix}.cue";
        string outputPlanPath = $"{outputPrefix}.level-music-batch-plan.json";
        LevelMusicBatchPatchPlan plan = await Task.Run(
            () => BuildBatchPlan(
                request.SourceImagePath,
                outputImagePath,
                outputCuePath,
                request.Edits),
            cancellationToken);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPlanPath) ?? ".");
        await File.WriteAllTextAsync(
            outputPlanPath,
            JsonSerializer.Serialize(plan, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);

        bool wroteImage = request.WriteImage && plan.Patches.Count > 0;
        if (wroteImage)
            await WriteImageAsync(
                request.SourceImagePath,
                request.SourceCuePath,
                outputImagePath,
                outputCuePath,
                plan,
                request.ConsumeDisposableSourceImage,
                cancellationToken);

        return new LevelMusicBatchPatchResult(outputImagePath, outputCuePath, outputPlanPath, plan, wroteImage);
    }

    public static LevelMusicBatchPatchPlan BuildBatchPlan(
        string sourceImagePath,
        string outputImagePath,
        string outputCuePath,
        IReadOnlyList<LevelMusicReplacement> edits)
    {
        DiscLayout discLayout = DiscImage.DetectLayout(sourceImagePath);
        using FileStream source = File.OpenRead(sourceImagePath);
        DiscFileRecord executable = DiscImage.FindRootFileRecord(source, discLayout, IsExecutableName);
        byte[] exeBytes = DiscImage.ReadFileBytes(source, discLayout, executable.Lba, 0, executable.Size);
        int[] peteXaLbas = Enumerable.Range(0, 6)
            .Select(index => DiscImage.FindRootFileRecord(
                source,
                discLayout,
                name => name.Equals($"PETEXA{index}.STR", StringComparison.OrdinalIgnoreCase)).Lba)
            .ToArray();
        MusicTableLayout tableLayout = LocateTables(exeBytes, peteXaLbas);

        List<LevelMusicReplacement> normalizedEdits = edits
            .GroupBy(edit => LevelCatalog.NormalizeKey(edit.Level.Key), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last())
            .OrderBy(edit => MusicTrackCatalog.GetLevelIndex(edit.Level))
            .ToList();
        List<LevelMusicPatch> patches = [];
        foreach (LevelMusicReplacement edit in normalizedEdits)
        {
            MusicTrackEntry track = MusicTrackCatalog.Find(edit.TrackId)
                ?? throw new InvalidOperationException($"Music track {edit.TrackId} does not exist.");
            if (!track.IsSelectable)
                throw new InvalidOperationException($"{track.DisplayName} is reserved for future custom-audio support.");

            int levelIndex = MusicTrackCatalog.GetLevelIndex(edit.Level);
            if (levelIndex < 0 || levelIndex >= LateAlternateLevelCount)
                throw new InvalidOperationException($"{edit.Level.DisplayName} does not have an editable level-music slot.");

            AddPatchIfChanged(
                patches,
                discLayout,
                executable.Lba,
                exeBytes,
                edit.Level,
                track,
                "level-track",
                tableLayout.MappingTableOffset + levelIndex * sizeof(int),
                edit.TrackId,
                "Changes the level's initial XA music track.");

            for (int alternate = 0; alternate < LateAlternateCountPerLevel; alternate++)
            {
                AddPatchIfChanged(
                    patches,
                    discLayout,
                    executable.Lba,
                    exeBytes,
                    edit.Level,
                    track,
                    "late-alternate-source-index",
                    tableLayout.LateAlternateTableOffset +
                        (levelIndex * LateAlternateCountPerLevel + alternate) * sizeof(int),
                    levelIndex,
                    "Keeps the selected track active when the late-music timer chooses an alternate slot.");
            }
        }

        return new LevelMusicBatchPatchPlan(
            GeneratedAt: DateTimeOffset.UtcNow,
            SourceImagePath: sourceImagePath,
            OutputImagePath: outputImagePath,
            OutputCuePath: outputCuePath,
            ExeName: executable.Name,
            ExeLba: executable.Lba,
            SectorSize: discLayout.SectorSize,
            UserOffset: discLayout.UserOffset,
            MappingTableFileOffset: tableLayout.MappingTableOffset,
            LateAlternateTableFileOffset: tableLayout.LateAlternateTableOffset,
            PeteXaStartTableFileOffset: tableLayout.PeteXaStartTableOffset,
            EditedLevelCount: normalizedEdits.Count,
            PatchCount: patches.Count,
            Levels: normalizedEdits.Select(edit => edit.Level.DisplayName).ToArray(),
            Patches: patches,
            Notes:
            [
                "The table locator is anchored to the six PETEXA stream LBAs from this disc's ISO directory.",
                "Each selected level keeps its chosen XA track when the native 8-12 minute alternate-music timer fires.",
                "No music audio sectors are copied into the plan; built-in selection patches executable table values only."
            ]);
    }

    private static MusicTableLayout LocateTables(byte[] exeBytes, IReadOnlyList<int> peteXaLbas)
    {
        byte[] lbaSignature = new byte[peteXaLbas.Count * sizeof(int)];
        for (int i = 0; i < peteXaLbas.Count; i++)
            BinaryPrimitives.WriteInt32LittleEndian(lbaSignature.AsSpan(i * sizeof(int), sizeof(int)), peteXaLbas[i]);

        List<MusicTableLayout> candidates = [];
        int lbaOffset = DiscImage.IndexOfBytes(exeBytes, lbaSignature, 0);
        while (lbaOffset >= 0)
        {
            int mappingOffset = lbaOffset - MappingAndLateAlternateByteCount;
            int lateAlternateOffset = mappingOffset + TrackCount * sizeof(int);
            if (ValidateTables(exeBytes, mappingOffset, lateAlternateOffset, lbaOffset))
                candidates.Add(new MusicTableLayout(mappingOffset, lateAlternateOffset, lbaOffset));

            lbaOffset = DiscImage.IndexOfBytes(exeBytes, lbaSignature, lbaOffset + 1);
        }

        MusicTableLayout[] unique = candidates.Distinct().ToArray();
        if (unique.Length != 1)
            throw new InvalidOperationException($"Expected one validated level-music table next to the PETEXA LBA table, found {unique.Length}.");

        return unique[0];
    }

    private static bool ValidateTables(byte[] exeBytes, int mappingOffset, int lateAlternateOffset, int lbaOffset)
    {
        if (mappingOffset < 0 ||
            lateAlternateOffset != mappingOffset + TrackCount * sizeof(int) ||
            lbaOffset != lateAlternateOffset + LateAlternateLevelCount * LateAlternateCountPerLevel * sizeof(int) ||
            lbaOffset > exeBytes.Length)
        {
            return false;
        }

        for (int index = 0; index < MappedLevelCount; index++)
        {
            int trackId = ReadInt32(exeBytes, mappingOffset + index * sizeof(int));
            if (trackId < 0 || trackId >= TrackCount)
                return false;
        }
        for (int index = MappedLevelCount; index < TrackCount; index++)
        {
            if (ReadInt32(exeBytes, mappingOffset + index * sizeof(int)) != index)
                return false;
        }
        for (int index = 0; index < LateAlternateLevelCount * LateAlternateCountPerLevel; index++)
        {
            int sourceLevelIndex = ReadInt32(exeBytes, lateAlternateOffset + index * sizeof(int));
            if (sourceLevelIndex < 0 || sourceLevelIndex >= TrackCount)
                return false;
        }

        return true;
    }

    private static void AddPatchIfChanged(
        ICollection<LevelMusicPatch> patches,
        DiscLayout discLayout,
        int exeLba,
        byte[] exeBytes,
        LevelDefinition level,
        MusicTrackEntry track,
        string kind,
        int fileOffset,
        int afterValue,
        string note)
    {
        int beforeValue = ReadInt32(exeBytes, fileOffset);
        if (beforeValue == afterValue)
            return;

        byte[] beforeBytes = exeBytes.AsSpan(fileOffset, sizeof(int)).ToArray();
        byte[] afterBytes = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(afterBytes, afterValue);
        patches.Add(new LevelMusicPatch(
            LevelKey: level.Key,
            LevelName: level.DisplayName,
            SelectedTrackId: track.TrackId,
            SelectedTrackName: track.DisplayName,
            Kind: kind,
            ExeFileOffset: fileOffset,
            ImageOffset: DiscImage.ConvertFileOffsetToImageOffset(discLayout, exeLba, fileOffset),
            ByteLength: sizeof(int),
            BeforeValue: beforeValue,
            AfterValue: afterValue,
            BeforeHexPreview: ToHex(beforeBytes),
            AfterHexPreview: ToHex(afterBytes),
            Note: note));
    }

    private static async Task WriteImageAsync(
        string sourceImagePath,
        string sourceCuePath,
        string outputImagePath,
        string outputCuePath,
        LevelMusicBatchPatchPlan plan,
        bool consumeDisposableSourceImage,
        CancellationToken cancellationToken)
    {
        await DiscImageWorkingCopy.StageAsync(
            sourceImagePath,
            outputImagePath,
            consumeDisposableSourceImage,
            cancellationToken);
        await using (FileStream stream = File.Open(outputImagePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
        {
            DiscLayout layout = new(plan.SectorSize, plan.UserOffset, 0, 0);
            foreach (LevelMusicPatch patch in plan.Patches)
                DiscImage.WriteFileBytes(stream, layout, plan.ExeLba, patch.ExeFileOffset, HexToBytes(patch.AfterHexPreview));
        }

        string cueText = DiscImage.BuildCueText(sourceCuePath, Path.GetFileName(outputImagePath));
        await File.WriteAllTextAsync(outputCuePath, cueText, Encoding.ASCII, cancellationToken);
    }

    private static bool IsExecutableName(string name)
    {
        string upper = name.ToUpperInvariant();
        return upper.StartsWith("SCUS", StringComparison.Ordinal)
            || upper.StartsWith("SCES", StringComparison.Ordinal)
            || upper.StartsWith("SCPS", StringComparison.Ordinal)
            || upper.StartsWith("SLUS", StringComparison.Ordinal)
            || upper.StartsWith("SLES", StringComparison.Ordinal)
            || upper.StartsWith("SLPS", StringComparison.Ordinal);
    }

    private static int ReadInt32(byte[] bytes, int offset) =>
        BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset, sizeof(int)));

    private static string ToHex(byte[] bytes) => string.Join(" ", bytes.Select(value => $"{value:X2}"));

    private static byte[] HexToBytes(string text) => text
        .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(part => Convert.ToByte(part, 16))
        .ToArray();

    private sealed record MusicTableLayout(
        int MappingTableOffset,
        int LateAlternateTableOffset,
        int PeteXaStartTableOffset);
}

public sealed record LevelMusicReplacement(LevelDefinition Level, int TrackId);

public sealed record LevelMusicBatchPatchRequest(
    string SourceImagePath,
    string SourceCuePath,
    string OutputPrefix,
    IReadOnlyList<LevelMusicReplacement> Edits,
    bool WriteImage,
    bool ConsumeDisposableSourceImage = false);

public sealed record LevelMusicBatchPatchResult(
    string OutputImagePath,
    string OutputCuePath,
    string OutputPlanPath,
    LevelMusicBatchPatchPlan Plan,
    bool WroteImage);

public sealed record LevelMusicBatchPatchPlan(
    DateTimeOffset GeneratedAt,
    string SourceImagePath,
    string OutputImagePath,
    string OutputCuePath,
    string ExeName,
    int ExeLba,
    int SectorSize,
    int UserOffset,
    int MappingTableFileOffset,
    int LateAlternateTableFileOffset,
    int PeteXaStartTableFileOffset,
    int EditedLevelCount,
    int PatchCount,
    IReadOnlyList<string> Levels,
    IReadOnlyList<LevelMusicPatch> Patches,
    IReadOnlyList<string> Notes);

public sealed record LevelMusicPatch(
    string LevelKey,
    string LevelName,
    int SelectedTrackId,
    string SelectedTrackName,
    string Kind,
    int ExeFileOffset,
    long ImageOffset,
    int ByteLength,
    int BeforeValue,
    int AfterValue,
    string BeforeHexPreview,
    string AfterHexPreview,
    string Note);
