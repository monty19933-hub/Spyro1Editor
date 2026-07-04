using System.Globalization;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Cache;

public sealed record SourceMobyCacheResult(string OutputPath, int MobyCount, int TreasureValue);

public static class SourceMobyCacheBuilder
{
    private const int RecordStride = 0x58;

    public static async Task<SourceMobyCacheResult> BuildAsync(
        string sourceImagePath,
        LevelDefinition level,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        if (!level.HasSourceTable)
            throw new InvalidOperationException($"{level.DisplayName} does not have a mapped source moby table.");
        if (!File.Exists(sourceImagePath))
            throw new FileNotFoundException("Missing source disc image.", sourceImagePath);

        long tableWadOffset = ParseNumber(level.SourceTableWadOffset);
        DiscLayout layout = DiscImage.DetectLayout(sourceImagePath);
        int wadLba;
        List<object> mobys = new(level.SourceRecordCount);
        int treasureValue = 0;
        using FileStream stream = File.OpenRead(sourceImagePath);
        DiscFileRecord wadFile = DiscImage.FindRootFileRecord(stream, layout, IsWadFileName);
        wadLba = wadFile.Lba;
        for (int trueIndex = 0; trueIndex < level.SourceRecordCount; trueIndex++)
        {
            long recordWadOffset = tableWadOffset + ((long)trueIndex * RecordStride);
            byte[] record = DiscImage.ReadFileBytes(stream, layout, wadLba, recordWadOffset, RecordStride);
            int rawX = BitConverter.ToInt32(record, 0x0C);
            int rawY = BitConverter.ToInt32(record, 0x10);
            int rawZ = BitConverter.ToInt32(record, 0x14);
            int type = record[0x50];
            int sourceByte36 = record[0x36];
            int sourceByte4F = record[0x4F];
            int flag4A = record[0x52];
            int flag4B = record[0x53];
            treasureValue += TreasureValue(type, sourceByte36, flag4A, flag4B);

            mobys.Add(new
            {
                index = trueIndex,
                trueIndex,
                legacyIndex = MobyLoader.GetLegacyAliasIndex(trueIndex),
                x = Math.Round(rawX / 16.0, 4),
                y = Math.Round(rawY / 16.0, 4),
                z = Math.Round(rawZ / 16.0, 4),
                rawX,
                rawY,
                rawZ,
                typeHex = $"0x{type:X2}",
                stateHex = $"0x{record[0x51]:X2}",
                runtimeAddress = 0,
                sourceRuntimeAddress = $"source-wad:0x{recordWadOffset:X}",
                specialDataPointer = $"0x{BitConverter.ToUInt32(record, 0x08):X8}",
                sourceByte36Hex = $"0x{sourceByte36:X2}",
                sourceByte37Hex = $"0x{record[0x37]:X2}",
                sourceByte4FHex = $"0x{sourceByte4F:X2}",
                flag4AHex = $"0x{flag4A:X2}",
                flag4BHex = $"0x{flag4B:X2}",
                sourceRecordWadOffset = $"0x{recordWadOffset:X}",
                sourceRecordImageOffset = $"0x{DiscImage.ConvertFileOffsetToImageOffset(layout, wadLba, recordWadOffset):X}",
                patchStatus = "source-table-patchable",
                patchLead = $"WAD entry {level.SourceWadEntry}, true record {trueIndex}, XYZ +0x0C/+0x10/+0x14"
            });
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        var root = new
        {
            generatedBy = "Spyro.Editor.Core",
            generatedAt = DateTime.Now.ToString("o"),
            purpose = "Portable native editor moby cache decoded directly from the level source table.",
            levelKey = level.Key,
            displayName = level.DisplayName,
            levelId = level.LevelId,
            source = "source-wad-moby-table",
            sourceImage = sourceImagePath,
            wadLba,
            sourceTableWadOffset = $"0x{tableWadOffset:X}",
            sourceRecordCount = level.SourceRecordCount,
            recordStride = "0x58",
            mobyCount = mobys.Count,
            treasureValue,
            mobys
        };

        JsonSerializerOptions options = new() { WriteIndented = true };
        await using FileStream outStream = File.Create(outputPath);
        await JsonSerializer.SerializeAsync(outStream, root, options, cancellationToken);
        return new SourceMobyCacheResult(outputPath, mobys.Count, treasureValue);
    }

    private static bool IsWadFileName(string name)
    {
        return string.Equals(name, "WAD.WAD", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "WAD", StringComparison.OrdinalIgnoreCase);
    }

    private static int TreasureValue(int type, int sourceByte36, int flag4A, int flag4B)
    {
        if (type == 0x18 && GemValue.TryFromIdByte(sourceByte36, out GemValue visibleGem))
            return visibleGem.Value;
        if (type == 0x00 && flag4A == 0xFF && GemValue.TryFromIdByte(flag4B, out GemValue containedGem))
            return containedGem.Value;
        if (type == 0x20 && GemValue.TryFromIdByte(flag4B, out GemValue rewardGem))
            return rewardGem.Value;

        return 0;
    }

    private static long ParseNumber(string text)
    {
        text = text.Trim();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return long.Parse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return long.Parse(text, CultureInfo.InvariantCulture);
    }
}
