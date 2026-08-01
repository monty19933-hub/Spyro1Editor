using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Exporting;

public sealed record NativeTerrainTextureRecordAppendResearchRequest(
    string SourceImagePath,
    string SourceCuePath,
    LevelDefinition TargetLevel,
    LevelDefinition DonorLevel,
    int DonorTextureId);

public sealed record NativeTerrainTextureRecordAppendComponentProof(
    string Name,
    int SourceOffset,
    int OutputOffset,
    int ByteLength,
    string Sha256);

public sealed record NativeTerrainTextureRecordAppendResearchPlan(
    string SourceImagePath,
    string SourceImageSha256,
    int TargetWadEntry,
    int DonorWadEntry,
    int DonorTextureId,
    int AppendedTextureId,
    long LevelDataWadOffset,
    int LevelDataByteLength,
    int SourceTextureCount,
    int OutputTextureCount,
    int SourceTextureComponentByteLength,
    int OutputTextureComponentByteLength,
    int GrowthByteCount,
    int SourceUsedByteLength,
    int OutputUsedByteLength,
    int SourceZeroTailByteCount,
    int OutputZeroTailByteCount,
    string SourceLevelDataSha256,
    string OutputLevelDataSha256,
    string PreservedSuffixSha256,
    string DonorLowDetailRowSha256,
    string DonorHighDetailRowSha256,
    bool SourceRuntimeControlsComplete,
    bool DonorRuntimeControlsComplete,
    bool AppendedTargetRuntimePersistent,
    bool ExistingLowDetailRowsPreserved,
    bool ExistingHighDetailRowsPreserved,
    bool DonorRowsCopiedExactly,
    bool SuffixPreservedExactly,
    bool OutputZeroTailVerified,
    bool OutputComponentChainReparsed,
    bool RequiresDuckStationRuntimeProof,
    IReadOnlyList<NativeTerrainTextureRecordAppendComponentProof> Components,
    byte[] SourceLevelData,
    byte[] OutputLevelData);

public sealed record NativeTerrainTextureRecordAppendResearchOutput(
    string OutputImagePath,
    string OutputCuePath,
    string OutputImageSha256,
    bool ExactReadbackVerified);

/// <summary>
/// Research-only proof for adding one native terrain texture record inside a
/// level's fixed-size level-data subfile. This type is intentionally not wired
/// to normal project persistence, Build Safety, or Create BIN. Static proof is
/// not runtime evidence; a candidate still requires a DuckStation test before
/// this mechanism can be promoted into the editor.
/// </summary>
public static class NativeTerrainTextureRecordAppendResearch
{
    public const int LowDetailRecordBytes = 16;
    public const int HighDetailRecordBytes = 168;
    public const int RecordGrowthBytes = LowDetailRecordBytes + HighDetailRecordBytes;

    private const int WadLba = 37;
    private const int LevelDataSubfileIndex = 1;
    private const int MaximumTextureId = 0x7F;
    private const int MaximumPortalCount = 64;
    private const int MaximumPortalPointCount = 4096;

    public static bool TryBuild(
        NativeTerrainTextureRecordAppendResearchRequest request,
        out NativeTerrainTextureRecordAppendResearchPlan? plan,
        out string failureReason)
    {
        plan = null;
        failureReason = "";
        try
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(request.TargetLevel);
            ArgumentNullException.ThrowIfNull(request.DonorLevel);
            ArgumentException.ThrowIfNullOrWhiteSpace(request.SourceImagePath);
            if (!File.Exists(request.SourceImagePath))
                throw new FileNotFoundException("Missing retail source image.", request.SourceImagePath);
            if (request.TargetLevel.SourceWadEntry < 0 || request.DonorLevel.SourceWadEntry < 0)
                throw new InvalidOperationException("Both target and donor levels need mapped native WAD entries.");

            DiscLayout discLayout = DiscImage.DetectLayout(request.SourceImagePath);
            using FileStream image = File.OpenRead(request.SourceImagePath);
            AssetSubfile target = LoadSubfile(
                image,
                discLayout,
                request.TargetLevel.SourceWadEntry,
                LevelDataSubfileIndex);
            AssetSubfile donor = LoadSubfile(
                image,
                discLayout,
                request.DonorLevel.SourceWadEntry,
                LevelDataSubfileIndex);
            byte[] sourceLevelData = DiscImage.ReadFileBytes(
                image,
                discLayout,
                WadLba,
                target.AbsoluteWadOffset,
                checked((int)target.ByteLength));
            byte[] donorLevelData = DiscImage.ReadFileBytes(
                image,
                discLayout,
                WadLba,
                donor.AbsoluteWadOffset,
                checked((int)donor.ByteLength));

            if (!TryParseLayout(sourceLevelData, out LevelDataLayout? sourceLayout, out failureReason) || sourceLayout == null)
                return false;
            if (!TryParseLayout(donorLevelData, out LevelDataLayout? donorLayout, out failureReason) || donorLayout == null)
            {
                failureReason = $"Donor level-data layout is not proven: {failureReason}";
                return false;
            }
            if (sourceLayout.TextureCount > MaximumTextureId)
            {
                failureReason = $"Appending texture id {sourceLayout.TextureCount} would exceed the native seven-bit terrain face field.";
                return false;
            }
            if (request.DonorTextureId < 0 || request.DonorTextureId >= donorLayout.TextureCount)
            {
                failureReason = $"Donor texture {request.DonorTextureId} is outside {request.DonorLevel.DisplayName}'s 0..{donorLayout.TextureCount - 1} table.";
                return false;
            }
            if (sourceLayout.ZeroTailByteCount < RecordGrowthBytes)
            {
                failureReason = $"The fixed level-data subfile has only {sourceLayout.ZeroTailByteCount} verified zero tail bytes; one native record needs {RecordGrowthBytes}.";
                return false;
            }

            NativeTerrainTextureRuntimeControlAudit targetRuntime =
                NativeTerrainTextureRuntimeControlScanner.Inspect(request.SourceImagePath, request.TargetLevel);
            NativeTerrainTextureRuntimeControlAudit donorRuntime =
                NativeTerrainTextureRuntimeControlScanner.Inspect(request.SourceImagePath, request.DonorLevel);
            if (!targetRuntime.Complete)
            {
                failureReason = targetRuntime.SafetyBlockers.FirstOrDefault()
                    ?? "Target runtime texture-control inspection is incomplete.";
                return false;
            }
            if (!donorRuntime.Complete)
            {
                failureReason = donorRuntime.SafetyBlockers.FirstOrDefault()
                    ?? "Donor runtime texture-control inspection is incomplete.";
                return false;
            }

            NativeTerrainTextureInitialStateResult initializedDonor =
                NativeTerrainTextureRuntimeControlScanner.InitializeTextureRecords(donorRuntime, donorLevelData);
            if (!initializedDonor.Complete)
            {
                failureReason = initializedDonor.SafetyBlockers.FirstOrDefault()
                    ?? "Donor texture table could not be initialized to its native load state.";
                return false;
            }

            int donorLowOffset = checked(8 + (request.DonorTextureId * LowDetailRecordBytes));
            int donorHighOffset = checked(
                8 + (donorLayout.TextureCount * LowDetailRecordBytes) +
                (request.DonorTextureId * HighDetailRecordBytes));
            byte[] donorLow = initializedDonor.InitializedTextureData
                .AsSpan(donorLowOffset, LowDetailRecordBytes)
                .ToArray();
            byte[] donorHigh = initializedDonor.InitializedTextureData
                .AsSpan(donorHighOffset, HighDetailRecordBytes)
                .ToArray();

            byte[] output = new byte[sourceLevelData.Length];
            int sourceLowTableStart = 8;
            int sourceHighTableStart = checked(sourceLowTableStart + (sourceLayout.TextureCount * LowDetailRecordBytes));
            int sourceTextureEnd = sourceLayout.TextureComponentByteLength;
            int outputHighTableStart = checked(sourceHighTableStart + LowDetailRecordBytes);
            int outputTextureEnd = checked(sourceTextureEnd + RecordGrowthBytes);

            BinaryPrimitives.WriteInt32LittleEndian(output.AsSpan(0, 4), outputTextureEnd);
            BinaryPrimitives.WriteInt32LittleEndian(output.AsSpan(4, 4), sourceLayout.TextureCount + 1);
            sourceLevelData.AsSpan(sourceLowTableStart, sourceHighTableStart - sourceLowTableStart)
                .CopyTo(output.AsSpan(sourceLowTableStart));
            donorLow.CopyTo(output, sourceHighTableStart);
            sourceLevelData.AsSpan(sourceHighTableStart, sourceTextureEnd - sourceHighTableStart)
                .CopyTo(output.AsSpan(outputHighTableStart));
            donorHigh.CopyTo(output, outputTextureEnd - HighDetailRecordBytes);
            sourceLevelData.AsSpan(sourceTextureEnd, sourceLayout.UsedByteLength - sourceTextureEnd)
                .CopyTo(output.AsSpan(outputTextureEnd));

            if (!TryParseLayout(output, out LevelDataLayout? outputLayout, out failureReason) || outputLayout == null)
            {
                failureReason = $"Expanded level-data component chain did not reparse: {failureReason}";
                return false;
            }
            if (outputLayout.TextureCount != sourceLayout.TextureCount + 1 ||
                outputLayout.TextureComponentByteLength != outputTextureEnd ||
                outputLayout.UsedByteLength != sourceLayout.UsedByteLength + RecordGrowthBytes)
            {
                failureReason = "Expanded texture count, component size, or used level-data length did not advance by one exact native record.";
                return false;
            }

            bool existingLowPreserved = sourceLevelData
                .AsSpan(sourceLowTableStart, sourceHighTableStart - sourceLowTableStart)
                .SequenceEqual(output.AsSpan(sourceLowTableStart, sourceHighTableStart - sourceLowTableStart));
            bool existingHighPreserved = sourceLevelData
                .AsSpan(sourceHighTableStart, sourceTextureEnd - sourceHighTableStart)
                .SequenceEqual(output.AsSpan(outputHighTableStart, sourceTextureEnd - sourceHighTableStart));
            bool donorRowsCopied = output.AsSpan(sourceHighTableStart, LowDetailRecordBytes).SequenceEqual(donorLow) &&
                output.AsSpan(outputTextureEnd - HighDetailRecordBytes, HighDetailRecordBytes).SequenceEqual(donorHigh);
            ReadOnlySpan<byte> sourceSuffix = sourceLevelData.AsSpan(
                sourceTextureEnd,
                sourceLayout.UsedByteLength - sourceTextureEnd);
            bool suffixPreserved = sourceSuffix.SequenceEqual(output.AsSpan(outputTextureEnd, sourceSuffix.Length));
            bool zeroTailVerified = output.AsSpan(outputLayout.UsedByteLength).IndexOfAnyExcept((byte)0) < 0;
            if (!existingLowPreserved || !existingHighPreserved || !donorRowsCopied || !suffixPreserved || !zeroTailVerified)
            {
                failureReason = "One or more table preservation, donor-row, suffix, or zero-tail invariants failed.";
                return false;
            }

            if (sourceLayout.Components.Count != outputLayout.Components.Count)
            {
                failureReason = "The expanded level-data suffix no longer has the same component count.";
                return false;
            }
            List<NativeTerrainTextureRecordAppendComponentProof> componentProofs = [];
            for (int index = 0; index < sourceLayout.Components.Count; index++)
            {
                LevelDataComponent before = sourceLayout.Components[index];
                LevelDataComponent after = outputLayout.Components[index];
                if (!string.Equals(before.Name, after.Name, StringComparison.Ordinal) ||
                    before.ByteLength != after.ByteLength ||
                    after.Offset != before.Offset + RecordGrowthBytes ||
                    !sourceLevelData.AsSpan(before.Offset, before.ByteLength)
                        .SequenceEqual(output.AsSpan(after.Offset, after.ByteLength)))
                {
                    failureReason = $"Suffix component '{before.Name}' was not preserved byte-for-byte at old offset +{RecordGrowthBytes}.";
                    return false;
                }
                componentProofs.Add(new NativeTerrainTextureRecordAppendComponentProof(
                    before.Name,
                    before.Offset,
                    after.Offset,
                    before.ByteLength,
                    Sha256(sourceLevelData.AsSpan(before.Offset, before.ByteLength))));
            }

            int appendedTextureId = sourceLayout.TextureCount;
            bool appendedRuntimePersistent = targetRuntime.Complete &&
                appendedTextureId >= targetRuntime.TextureCount &&
                !targetRuntime.Controls.Any(control => control.TextureId == appendedTextureId);
            if (!appendedRuntimePersistent)
            {
                failureReason = "The appended id is unexpectedly targeted by a native animation or scrolling control.";
                return false;
            }

            plan = new NativeTerrainTextureRecordAppendResearchPlan(
                request.SourceImagePath,
                Sha256File(request.SourceImagePath),
                request.TargetLevel.SourceWadEntry,
                request.DonorLevel.SourceWadEntry,
                request.DonorTextureId,
                appendedTextureId,
                target.AbsoluteWadOffset,
                sourceLevelData.Length,
                sourceLayout.TextureCount,
                outputLayout.TextureCount,
                sourceLayout.TextureComponentByteLength,
                outputLayout.TextureComponentByteLength,
                RecordGrowthBytes,
                sourceLayout.UsedByteLength,
                outputLayout.UsedByteLength,
                sourceLayout.ZeroTailByteCount,
                outputLayout.ZeroTailByteCount,
                Sha256(sourceLevelData),
                Sha256(output),
                Sha256(sourceSuffix),
                Sha256(donorLow),
                Sha256(donorHigh),
                targetRuntime.Complete,
                donorRuntime.Complete,
                appendedRuntimePersistent,
                existingLowPreserved,
                existingHighPreserved,
                donorRowsCopied,
                suffixPreserved,
                zeroTailVerified,
                true,
                true,
                componentProofs,
                sourceLevelData,
                output);
            failureReason = "";
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidDataException or InvalidOperationException or IOException or OverflowException)
        {
            failureReason = ex.Message;
            return false;
        }
    }

    public static async Task<NativeTerrainTextureRecordAppendResearchOutput> WriteCandidateAsync(
        NativeTerrainTextureRecordAppendResearchRequest request,
        NativeTerrainTextureRecordAppendResearchPlan plan,
        string outputPrefix,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPrefix);
        if (!string.Equals(Sha256File(request.SourceImagePath), plan.SourceImageSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The source BIN no longer matches the append plan's SHA-256 preimage.");

        string outputImagePath = Path.GetFullPath(outputPrefix + ".bin");
        string outputCuePath = Path.GetFullPath(outputPrefix + ".cue");
        Directory.CreateDirectory(Path.GetDirectoryName(outputImagePath) ?? ".");
        File.Delete(outputImagePath);
        File.Delete(outputCuePath);
        File.Copy(request.SourceImagePath, outputImagePath, true);

        DiscLayout layout = DiscImage.DetectLayout(outputImagePath);
        await using (FileStream output = File.Open(outputImagePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
        {
            byte[] before = DiscImage.ReadFileBytes(
                output,
                layout,
                WadLba,
                plan.LevelDataWadOffset,
                plan.LevelDataByteLength);
            if (!before.SequenceEqual(plan.SourceLevelData))
                throw new InvalidDataException("The candidate copy does not match the planned level-data preimage.");
            DiscImage.WriteFileBytes(output, layout, WadLba, plan.LevelDataWadOffset, plan.OutputLevelData);
            output.Flush(flushToDisk: true);
            byte[] after = DiscImage.ReadFileBytes(
                output,
                layout,
                WadLba,
                plan.LevelDataWadOffset,
                plan.LevelDataByteLength);
            if (!after.SequenceEqual(plan.OutputLevelData))
                throw new InvalidDataException("Expanded level-data final BIN readback failed.");
        }

        string cue = DiscImage.BuildCueText(request.SourceCuePath, Path.GetFileName(outputImagePath));
        await File.WriteAllTextAsync(outputCuePath, cue, Encoding.ASCII, cancellationToken);
        return new NativeTerrainTextureRecordAppendResearchOutput(
            outputImagePath,
            outputCuePath,
            Sha256File(outputImagePath),
            true);
    }

    private static bool TryParseLayout(byte[] data, out LevelDataLayout? layout, out string failureReason)
    {
        layout = null;
        failureReason = "";
        if (data.Length < 8)
        {
            failureReason = "Level-data subfile is shorter than the texture header.";
            return false;
        }

        int textureLength = ReadInt32(data, 0);
        int textureCount = ReadInt32(data, 4);
        long expectedTextureLength = 8L + (textureCount * (long)RecordGrowthBytes);
        if (textureCount <= 0 || textureCount > 128 || expectedTextureLength > int.MaxValue ||
            textureLength != expectedTextureLength || textureLength > data.Length)
        {
            failureReason = $"Terrain texture component has invalid size/count {textureLength}/{textureCount}.";
            return false;
        }

        int cursor = textureLength;
        List<LevelDataComponent> components = [];
        foreach (string name in new[] { "environment", "occlusion", "special surface", "collision", "cyclorama" })
        {
            if (!TryAdvanceComponent(data, ref cursor, name, components, out failureReason))
                return false;
        }

        if (cursor + 4 > data.Length)
        {
            failureReason = "Portal count is outside the level-data subfile.";
            return false;
        }
        int portalCount = ReadInt32(data, cursor);
        if (portalCount is < 0 or > MaximumPortalCount)
        {
            failureReason = $"Portal count {portalCount} is outside the proven range.";
            return false;
        }
        cursor += 4;
        for (int portal = 0; portal < portalCount; portal++)
        {
            if (cursor + 8 > data.Length)
            {
                failureReason = $"Portal {portal} header is outside level data.";
                return false;
            }
            int pointCount = ReadInt32(data, cursor + 4);
            if (pointCount is < 1 or > MaximumPortalPointCount)
            {
                failureReason = $"Portal {portal} point count {pointCount} is outside the proven range.";
                return false;
            }
            long skyboxStartLong = cursor + 0x40L + ((pointCount - 1L) * 12L);
            if (skyboxStartLong < 0 || skyboxStartLong > int.MaxValue || skyboxStartLong >= data.Length)
            {
                failureReason = $"Portal {portal} skybox component offset is invalid.";
                return false;
            }
            int portalStructureStart = cursor;
            cursor = (int)skyboxStartLong;
            if (!TryAdvanceComponent(data, ref cursor, $"portal {portal} structure and cyclorama", components, out failureReason, portalStructureStart))
                return false;
        }

        foreach (string name in new[] { "particle textures", "sound table" })
        {
            if (!TryAdvanceComponent(data, ref cursor, name, components, out failureReason))
                return false;
        }
        if (data.AsSpan(cursor).IndexOfAnyExcept((byte)0) >= 0)
        {
            failureReason = "Bytes after the final native sound component are not an all-zero tail.";
            return false;
        }

        layout = new LevelDataLayout(
            textureLength,
            textureCount,
            cursor,
            data.Length - cursor,
            components);
        return true;
    }

    private static bool TryAdvanceComponent(
        byte[] data,
        ref int cursor,
        string name,
        List<LevelDataComponent> components,
        out string failureReason,
        int? spanStart = null)
    {
        failureReason = "";
        int componentStart = cursor;
        if (componentStart < 0 || componentStart + 4 > data.Length)
        {
            failureReason = $"{name} component header is outside level data.";
            return false;
        }
        int length = ReadInt32(data, componentStart);
        if (length < 4 || (long)componentStart + length > data.Length)
        {
            failureReason = $"{name} component length 0x{length:X} is invalid at +0x{componentStart:X}.";
            return false;
        }
        cursor = componentStart + length;
        int start = spanStart ?? componentStart;
        components.Add(new LevelDataComponent(name, start, cursor - start));
        return true;
    }

    private static AssetSubfile LoadSubfile(FileStream image, DiscLayout layout, int wadEntry, int subfileIndex)
    {
        byte[] wadHeader = DiscImage.ReadFileBytes(image, layout, WadLba, 0, 4096);
        ArchiveEntry asset = ParseArchive(wadHeader, 200_000_000)
            .FirstOrDefault(entry => entry.Index == wadEntry)
            ?? throw new InvalidDataException($"Missing WAD entry {wadEntry}.");
        byte[] assetHeader = DiscImage.ReadFileBytes(image, layout, WadLba, asset.Offset, 4096);
        ArchiveEntry subfile = ParseArchive(assetHeader, asset.ByteLength)
            .FirstOrDefault(entry => entry.Index == subfileIndex)
            ?? throw new InvalidDataException($"Missing subfile {subfileIndex} in WAD entry {wadEntry}.");
        return new AssetSubfile(asset.Offset + subfile.Offset, subfile.ByteLength);
    }

    private static IReadOnlyList<ArchiveEntry> ParseArchive(byte[] header, long archiveLength)
    {
        List<ArchiveEntry> entries = [];
        long firstDataOffset = ReadUInt32(header, 0);
        if (firstDataOffset <= 0 || firstDataOffset > header.Length)
            firstDataOffset = header.Length;
        for (int offset = 0; offset <= Math.Min(header.Length, firstDataOffset) - 8; offset += 8)
        {
            long entryOffset = ReadUInt32(header, offset);
            long entryLength = ReadUInt32(header, offset + 4);
            if (entryOffset <= 0 || entryLength <= 0 || entryOffset + entryLength > archiveLength)
                continue;
            entries.Add(new ArchiveEntry(offset / 8, entryOffset, entryLength));
        }
        return entries;
    }

    private static int ReadInt32(byte[] data, int offset) =>
        BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset, 4));

    private static uint ReadUInt32(byte[] data, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset, 4));

    private static string Sha256(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes));

    private static string Sha256File(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private sealed record ArchiveEntry(int Index, long Offset, long ByteLength);
    private sealed record AssetSubfile(long AbsoluteWadOffset, long ByteLength);
    private sealed record LevelDataComponent(string Name, int Offset, int ByteLength);
    private sealed record LevelDataLayout(
        int TextureComponentByteLength,
        int TextureCount,
        int UsedByteLength,
        int ZeroTailByteCount,
        IReadOnlyList<LevelDataComponent> Components);
}
