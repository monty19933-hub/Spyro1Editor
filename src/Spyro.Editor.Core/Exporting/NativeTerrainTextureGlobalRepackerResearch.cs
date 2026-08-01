using System.Buffers.Binary;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Exporting;

public sealed record NativeTerrainTextureGlobalRepackResearchRequest(
    string SourceImagePath,
    LevelDefinition TargetLevel,
    IReadOnlyList<NativeTerrainTextureRelocationImport> DonorOverrides,
    string DonorSourceImagePath = "",
    NativeTerrainTexturePrivateRecordStagingSourceProof? StagingSourceProof = null);

/// <summary>
/// One not-yet-installed native texture row. Assigned ids must form the exact
/// contiguous range immediately after the retail table. The material template
/// supplies only the destination row's native non-coordinate bits; indexed
/// pixels, palettes, and descriptor coordinates come from the donor.
/// </summary>
public sealed record NativeTerrainTextureGlobalRepackSyntheticRecord(
    int AssignedTextureId,
    int DonorWadEntry,
    int DonorTextureId,
    int MaterialTemplateTextureId);

/// <summary>
/// Production-neutral packing request. Unlike the candidate-image research
/// overload, this form reads the retail BIN once and models appended rows only
/// in memory, so a structural composer can later install all returned rows and
/// ordinary level-data edits atomically.
/// </summary>
public sealed record NativeTerrainTextureGlobalRepackSyntheticRequest(
    string SourceImagePath,
    LevelDefinition TargetLevel,
    IReadOnlyList<NativeTerrainTextureRelocationImport> ExistingRecordOverrides,
    IReadOnlyList<NativeTerrainTextureGlobalRepackSyntheticRecord> SyntheticRecords,
    string DonorSourceImagePath = "",
    NativeTerrainTexturePrivateRecordStagingSourceProof? StagingSourceProof = null);

public sealed record NativeTerrainTextureGlobalRepackPackedRecord(
    int TargetTextureId,
    int DonorWadEntry,
    int DonorTextureId,
    byte[] LowDetailRow,
    byte[] HighDetailRow,
    string LowDetailRowSha256,
    string HighDetailRowSha256);

public sealed record NativeTerrainTextureGlobalRepackSyntheticPlan(
    int SourceTextureCount,
    int OutputTextureCount,
    IReadOnlyList<NativeTerrainTextureGlobalRepackPackedRecord> OriginalMovableRecords,
    IReadOnlyList<NativeTerrainTextureGlobalRepackPackedRecord> SyntheticRecords,
    IReadOnlyList<NativeTerrainTextureRelocationPatch> TexturePagePatches,
    IReadOnlyList<NativeTerrainTextureRelocationPatch> OriginalDescriptorPatches,
    NativeTerrainTextureGlobalRepackResearchPlan PackingProof,
    bool SourceDescriptorOffsetsRemainUnmodified,
    bool SyntheticRowsModeledOnlyInMemory,
    bool RequiresStructuralComposer,
    bool RequiresDuckStationRuntimeProof,
    IReadOnlyList<string> Notes);

public sealed record NativeTerrainTextureGlobalRepackResearchOutput(
    string OutputImagePath,
    string OutputCuePath,
    string OutputImageSha256,
    bool PatchPreimagesVerified,
    bool ExactPatchReadbackVerified,
    bool SourceImageUnchanged,
    bool RequiresDuckStationRuntimeProof);

public sealed record NativeTerrainTextureGlobalRepackResearchPlan(
    int TargetWadEntry,
    int TextureCount,
    int AddressableTexturePageByteCount,
    string TexturePagesSha256,
    IReadOnlyList<int> MovableTextureIds,
    IReadOnlyList<int> FixedRuntimeControlledTextureIds,
    int ProtectedByteCount,
    int AnchoredTerrainByteCount,
    int ReleasedTerrainByteCount,
    int AllocatedPixelByteCount,
    int AllocatedPaletteByteCount,
    int AllocatedByteCount,
    int FreeByteCount,
    int PixelStorageGroupCount,
    int PaletteStorageGroupCount,
    int PixelAliasPairCount,
    int PaletteAliasPairCount,
    int RewrittenDescriptorCount,
    int FixedDescriptorCount,
    int ChangedTexturePageByteCount,
    int ChangedDescriptorByteCount,
    string PackingStrategy,
    bool ExactIndexedPixelReadbackVerified,
    bool ExactPaletteReadbackVerified,
    bool PixelAliasRelationshipsPreserved,
    bool PaletteAliasRelationshipsPreserved,
    bool LowDetailAliasPreserved,
    bool ProtectedStoragePreserved,
    bool FixedDescriptorRowsPreserved,
    bool TargetMaterialBitsPreserved,
    bool RequiresDuckStationRuntimeProof,
    IReadOnlyList<NativeTerrainTextureGlobalRepackPackedRecord> PackedOverrideRecords,
    IReadOnlyList<NativeTerrainTextureRelocationPatch> Patches,
    IReadOnlyList<string> Notes)
{
    /// <summary>Final complete rows for every movable target record.</summary>
    public IReadOnlyList<NativeTerrainTextureGlobalRepackPackedRecord> PackedMovableRecords { get; init; } = [];

    public IReadOnlyList<NativeTerrainTextureRelocationPatch> TexturePagePatches =>
        Patches.Where(item => item.Kind == "terrain-texture-global-repack-data").ToArray();

    public IReadOnlyList<NativeTerrainTextureRelocationPatch> DescriptorPatches =>
        Patches.Where(item => item.Kind == "terrain-texture-global-repack-descriptor").ToArray();
}

/// <summary>
/// Research-only deterministic global terrain texture-page repacker. It releases
/// the collectively-exclusive storage of every runtime-persistent terrain record,
/// keeps external consumers and runtime-controlled records fixed, and packs the
/// complete logical terrain union back into the retail 0x80000-byte VRAM upload.
/// This is static proof only and is intentionally not wired to Create BIN.
/// </summary>
public static class NativeTerrainTextureGlobalRepackerResearch
{
    private const int WadLba = 37;
    private const int TexturePagesSubfileIndex = 0;
    private const int ModelSubfileIndex = 1;
    private const int RowBytes = 1024;
    private const int FullVramTextureByteX = 1024;
    private const int VramRows = 512;
    private const int AddressableBytes = RowBytes * VramRows;
    private const int LqRecordBytes = 16;
    private const int HqRecordBytes = 168;
    private const int LqTileSide = 32;
    private const int LqPixelWidthBytes = 16;
    private const int LqPaletteWidthBytes = 32;
    private const int LqPaletteRows = 16;
    private const int HqPaletteBytes = 512;
    private const int DescriptorsPerRecord = 23;
    private const string ArtisansRetailTexturePagesSha256 =
        "01a034ad32eba1b100e685e63d902dabd04e06cb3629fad73c6b83dc50f779e5";
    private const string ArtisansStagingPackingStrategy =
        "palette-first-forward-first-fit";
    private const string ArtisansGnastyEyePreferredPackingStrategy =
        "lq-first-palette-components-best-contact";

    private static readonly int[][] Matrices =
    [
        [ 1,  0,  0,  1],
        [ 0,  1,  1,  0],
        [-1,  0,  0, -1],
        [ 0, -1,  1,  0],
        [ 0,  1,  1,  0],
        [-1,  0,  0,  1],
        [ 0, -1, -1,  0],
        [ 1,  0,  0, -1]
    ];

    public static bool TryBuild(
        NativeTerrainTextureGlobalRepackResearchRequest request,
        out NativeTerrainTextureGlobalRepackResearchPlan? plan,
        out string failureReason) =>
        TryBuildCore(request, [], out plan, out failureReason);

    /// <summary>
    /// Packs retail records plus synthetic appended records without first
    /// materializing a full candidate BIN. The returned page patches apply to
    /// the retail preimage. Complete final rows are returned separately because
    /// a structural table-growth composer owns their final level-data offsets.
    /// </summary>
    public static bool TryBuild(
        NativeTerrainTextureGlobalRepackSyntheticRequest request,
        out NativeTerrainTextureGlobalRepackSyntheticPlan? plan,
        out string failureReason)
    {
        plan = null;
        failureReason = "";
        try
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(request.TargetLevel);
            ArgumentNullException.ThrowIfNull(request.ExistingRecordOverrides);
            ArgumentNullException.ThrowIfNull(request.SyntheticRecords);
            ArgumentException.ThrowIfNullOrWhiteSpace(request.SourceImagePath);
            if (request.SyntheticRecords.Count == 0)
            {
                failureReason = "At least one synthetic appended texture record is required.";
                return false;
            }

            NativeTerrainTextureRuntimeControlAudit runtime =
                request.StagingSourceProof == null
                    ? NativeTerrainTextureRuntimeControlScanner.Inspect(
                        request.SourceImagePath,
                        request.TargetLevel)
                    : NativeTerrainTextureSourceAnalysisSessionCache.InspectRuntime(
                        request.SourceImagePath,
                        request.TargetLevel,
                        request.StagingSourceProof.SourceBinding,
                        out _);
            if (!runtime.Complete)
            {
                failureReason = runtime.SafetyBlockers.FirstOrDefault()
                    ?? "Runtime texture-control closure is incomplete.";
                return false;
            }
            int sourceCount = runtime.TextureCount;
            NativeTerrainTextureGlobalRepackSyntheticRecord[] synthetic = request.SyntheticRecords
                .OrderBy(item => item.AssignedTextureId)
                .ToArray();
            for (int index = 0; index < synthetic.Length; index++)
            {
                int expectedId = checked(sourceCount + index);
                NativeTerrainTextureGlobalRepackSyntheticRecord item = synthetic[index];
                if (item.AssignedTextureId != expectedId)
                {
                    failureReason = $"Synthetic texture ids must be contiguous at the retail tail; expected T{expectedId} but found T{item.AssignedTextureId}.";
                    return false;
                }
                if (item.AssignedTextureId > 0x7F)
                {
                    failureReason = $"Synthetic texture T{item.AssignedTextureId} exceeds the native seven-bit terrain-face field.";
                    return false;
                }
                if (item.MaterialTemplateTextureId < 0 || item.MaterialTemplateTextureId >= sourceCount)
                {
                    failureReason = $"Synthetic texture T{item.AssignedTextureId} has invalid native material template T{item.MaterialTemplateTextureId}.";
                    return false;
                }
            }

            NativeTerrainTextureRelocationImport[] syntheticOverrides = synthetic
                .Select(item => new NativeTerrainTextureRelocationImport(
                    item.AssignedTextureId,
                    item.DonorWadEntry,
                    item.DonorTextureId,
                    "both",
                    PreserveTargetDescriptorMaterial: true))
                .ToArray();
            if (request.ExistingRecordOverrides.Select(item => item.TargetTextureId)
                .Intersect(syntheticOverrides.Select(item => item.TargetTextureId)).Any())
            {
                failureReason = "An existing-record donor override collides with a synthetic assigned texture id.";
                return false;
            }

            NativeTerrainTextureGlobalRepackResearchRequest coreRequest = new(
                request.SourceImagePath,
                request.TargetLevel,
                request.ExistingRecordOverrides.Concat(syntheticOverrides).ToArray(),
                request.DonorSourceImagePath,
                request.StagingSourceProof);
            if (!TryBuildCore(coreRequest, synthetic, out NativeTerrainTextureGlobalRepackResearchPlan? proof, out failureReason) ||
                proof == null)
            {
                return false;
            }

            IReadOnlyList<NativeTerrainTextureRelocationPatch> pagePatches = proof.TexturePagePatches;
            NativeTerrainTextureGlobalRepackPackedRecord[] originalRows = proof.PackedMovableRecords
                .Where(item => item.TargetTextureId < sourceCount)
                .OrderBy(item => item.TargetTextureId)
                .ToArray();
            NativeTerrainTextureGlobalRepackPackedRecord[] syntheticRows = proof.PackedMovableRecords
                .Where(item => item.TargetTextureId >= sourceCount)
                .OrderBy(item => item.TargetTextureId)
                .ToArray();
            if (syntheticRows.Length != synthetic.Length ||
                !syntheticRows.Select(item => item.TargetTextureId)
                    .SequenceEqual(synthetic.Select(item => item.AssignedTextureId)))
            {
                failureReason = "The in-memory pack did not return one exact final row for every synthetic record.";
                return false;
            }

            DiscLayout sourceLayout = DiscImage.DetectLayout(request.SourceImagePath);
            using FileStream sourceImage = File.OpenRead(request.SourceImagePath);
            TextureAsset retailTarget = LoadAsset(
                request.SourceImagePath,
                sourceImage,
                sourceLayout,
                request.TargetLevel.SourceWadEntry,
                initializeRuntimeState: false);
            List<NativeTerrainTextureRelocationPatch> originalDescriptorPatches = [];
            int retailHighStart = checked(8 + (sourceCount * LqRecordBytes));
            foreach (NativeTerrainTextureGlobalRepackPackedRecord row in originalRows)
            {
                int lowOffset = checked(8 + (row.TargetTextureId * LqRecordBytes));
                int highOffset = checked(retailHighStart + (row.TargetTextureId * HqRecordBytes));
                originalDescriptorPatches.AddRange(BuildDiffPatches(
                    retailTarget.Model.AsSpan(lowOffset, LqRecordBytes).ToArray(),
                    row.LowDetailRow,
                    retailTarget.ModelWadOffset + lowOffset,
                    "terrain-texture-global-repack-descriptor",
                    $"Install globally packed LQ row for original T{row.TargetTextureId}."));
                originalDescriptorPatches.AddRange(BuildDiffPatches(
                    retailTarget.Model.AsSpan(highOffset, HqRecordBytes).ToArray(),
                    row.HighDetailRow,
                    retailTarget.ModelWadOffset + highOffset,
                    "terrain-texture-global-repack-descriptor",
                    $"Install globally packed HQ row for original T{row.TargetTextureId}."));
            }
            NativeTerrainTextureRelocationPatch[] sourceApplicablePatches = pagePatches
                .Concat(originalDescriptorPatches)
                .OrderBy(item => item.WadOffset)
                .ToArray();
            // Synthetic descriptor offsets after an in-memory append are
            // deliberately suppressed. Original descriptor patches above are
            // rebuilt against exact retail offsets/preimages.
            NativeTerrainTextureGlobalRepackResearchPlan sourceSafeProof = proof with
            {
                Patches = sourceApplicablePatches,
                Notes = proof.Notes.Concat([
                    "Synthetic mode exposes original descriptor rewrites at retail offsets and suppresses only synthetic in-memory descriptor offsets."
                ]).ToArray()
            };

            plan = new NativeTerrainTextureGlobalRepackSyntheticPlan(
                sourceCount,
                proof.TextureCount,
                originalRows,
                syntheticRows,
                pagePatches,
                originalDescriptorPatches.OrderBy(item => item.WadOffset).ToArray(),
                sourceSafeProof,
                true,
                true,
                true,
                true,
                [
                    "Texture-page patches are source-bound directly to the retail BIN preimage.",
                    "Original descriptor patches use exact retail offsets/preimages; complete original and synthetic LQ/HQ rows are also returned for structural composition.",
                    "A structural composer must install these rows and rebase other level-data edits atomically before DuckStation testing."
                ]);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidDataException or InvalidOperationException or IOException or OverflowException)
        {
            failureReason = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Reopens a materialized research candidate and proves that each supplied
    /// final row is present exactly and still decodes to the donor's indexed
    /// pixels and palettes. This is a static final-BIN readback, not runtime
    /// evidence.
    /// </summary>
    public static bool TryVerifyCandidateLogicalReadback(
        string candidateImagePath,
        LevelDefinition targetLevel,
        IReadOnlyList<NativeTerrainTextureGlobalRepackPackedRecord> expectedRecords,
        out string failureReason) =>
        TryVerifyCandidateLogicalReadback(
            candidateImagePath,
            targetLevel,
            expectedRecords,
            candidateImagePath,
            out failureReason);

    public static bool TryVerifyCandidateLogicalReadback(
        string candidateImagePath,
        LevelDefinition targetLevel,
        IReadOnlyList<NativeTerrainTextureGlobalRepackPackedRecord> expectedRecords,
        string donorSourceImagePath,
        out string failureReason)
    {
        failureReason = "";
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(candidateImagePath);
            ArgumentException.ThrowIfNullOrWhiteSpace(donorSourceImagePath);
            ArgumentNullException.ThrowIfNull(targetLevel);
            ArgumentNullException.ThrowIfNull(expectedRecords);
            if (!File.Exists(candidateImagePath))
                throw new FileNotFoundException("Missing materialized repack candidate.", candidateImagePath);
            if (!File.Exists(donorSourceImagePath))
                throw new FileNotFoundException("Missing immutable donor readback source.", donorSourceImagePath);
            DiscLayout layout = DiscImage.DetectLayout(candidateImagePath);
            using FileStream image = File.OpenRead(candidateImagePath);
            TextureAsset target = LoadAsset(
                candidateImagePath,
                image,
                layout,
                targetLevel.SourceWadEntry,
                initializeRuntimeState: false);
            DiscLayout donorLayout = DiscImage.DetectLayout(donorSourceImagePath);
            using FileStream donorImage = File.OpenRead(donorSourceImagePath);
            Dictionary<int, TextureAsset> donors = [];
            foreach (int wadEntry in expectedRecords.Select(item => item.DonorWadEntry).Distinct())
            {
                donors[wadEntry] = LoadAsset(
                    donorSourceImagePath,
                    donorImage,
                    donorLayout,
                    wadEntry,
                    initializeRuntimeState: true);
            }
            foreach (NativeTerrainTextureGlobalRepackPackedRecord expected in expectedRecords)
            {
                if (expected.TargetTextureId < 0 || expected.TargetTextureId >= target.Index.TextureCount)
                {
                    failureReason = $"Final candidate is missing expected target T{expected.TargetTextureId}.";
                    return false;
                }
                if (!donors.TryGetValue(expected.DonorWadEntry, out TextureAsset? donor) ||
                    expected.DonorTextureId < 0 || expected.DonorTextureId >= donor.Index.TextureCount)
                {
                    failureReason = $"Final candidate cannot decode donor WAD {expected.DonorWadEntry} T{expected.DonorTextureId}.";
                    return false;
                }
                NativeTerrainTextureRelocationImport import = new(
                    expected.TargetTextureId,
                    expected.DonorWadEntry,
                    expected.DonorTextureId,
                    "both");
                NativeTerrainTextureGlobalRepackPackedRecord actual =
                    ExtractPackedRecord(target.Model, target.Index.TextureCount, import);
                if (!actual.LowDetailRow.SequenceEqual(expected.LowDetailRow) ||
                    !actual.HighDetailRow.SequenceEqual(expected.HighDetailRow))
                {
                    failureReason = $"Final candidate T{expected.TargetTextureId} does not contain the planned exact LQ/HQ rows.";
                    return false;
                }

                TextureRecord source = donor.Index.Records[expected.DonorTextureId];
                TextureRecord destination = target.Index.Records[expected.TargetTextureId];
                if (!VerifyDescriptor(donor.TexturePages, source.Low[0], target.TexturePages, destination.Low[0]) ||
                    !VerifyDescriptor(donor.TexturePages, source.Low[0], target.TexturePages, destination.Low[1]) ||
                    !VerifyDescriptor(donor.TexturePages, source.Low[0], target.TexturePages, destination.Leading))
                {
                    failureReason = $"Final candidate T{expected.TargetTextureId} failed LQ donor logical readback.";
                    return false;
                }
                for (int index = 0; index < source.Normal.Count; index++)
                {
                    if (!VerifyDescriptor(donor.TexturePages, source.Normal[index], target.TexturePages, destination.Normal[index]))
                    {
                        failureReason = $"Final candidate T{expected.TargetTextureId} failed normal[{index}] donor logical readback.";
                        return false;
                    }
                }
                for (int index = 0; index < source.Close.Count; index++)
                {
                    if (!VerifyDescriptor(donor.TexturePages, source.Close[index], target.TexturePages, destination.Close[index]))
                    {
                        failureReason = $"Final candidate T{expected.TargetTextureId} failed close[{index}] donor logical readback.";
                        return false;
                    }
                }
            }
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidDataException or InvalidOperationException or IOException or OverflowException)
        {
            failureReason = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Materializes a source-bound research plan for DuckStation testing. This
    /// writer is intentionally separate from normal Create BIN integration.
    /// </summary>
    public static async Task<NativeTerrainTextureGlobalRepackResearchOutput> WriteCandidateAsync(
        string sourceImagePath,
        string sourceCuePath,
        NativeTerrainTextureGlobalRepackResearchPlan plan,
        string outputPrefix,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceImagePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceCuePath);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPrefix);
        if (!File.Exists(sourceImagePath))
            throw new FileNotFoundException("Missing repack candidate source BIN.", sourceImagePath);
        if (!File.Exists(sourceCuePath))
            throw new FileNotFoundException("Missing repack candidate source CUE.", sourceCuePath);

        string sourceSha = Sha256File(sourceImagePath);
        string outputImagePath = Path.GetFullPath(outputPrefix + ".bin");
        string outputCuePath = Path.GetFullPath(outputPrefix + ".cue");
        Directory.CreateDirectory(Path.GetDirectoryName(outputImagePath) ?? ".");
        File.Delete(outputImagePath);
        File.Delete(outputCuePath);
        File.Copy(sourceImagePath, outputImagePath, overwrite: true);

        NativeTerrainTextureRelocationPatch[] patches = plan.Patches
            .OrderBy(item => item.WadOffset)
            .ToArray();
        for (int index = 1; index < patches.Length; index++)
        {
            long priorEnd = checked(patches[index - 1].WadOffset + patches[index - 1].ByteLength);
            if (patches[index].WadOffset < priorEnd)
                throw new InvalidDataException("Global repack patches overlap and cannot be materialized deterministically.");
        }

        DiscLayout layout = DiscImage.DetectLayout(outputImagePath);
        await using (FileStream output = File.Open(outputImagePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
        {
            foreach (NativeTerrainTextureRelocationPatch patch in patches)
            {
                byte[] before = DiscImage.ReadFileBytes(output, layout, WadLba, patch.WadOffset, patch.ByteLength);
                if (!before.SequenceEqual(patch.Before))
                    throw new InvalidDataException($"Global repack patch preimage mismatch at WAD +0x{patch.WadOffset:X}.");
                DiscImage.WriteFileBytes(output, layout, WadLba, patch.WadOffset, patch.After);
            }
            output.Flush(flushToDisk: true);
            foreach (NativeTerrainTextureRelocationPatch patch in patches)
            {
                byte[] after = DiscImage.ReadFileBytes(output, layout, WadLba, patch.WadOffset, patch.ByteLength);
                if (!after.SequenceEqual(patch.After))
                    throw new InvalidDataException($"Global repack patch readback mismatch at WAD +0x{patch.WadOffset:X}.");
            }
        }

        string cueText = DiscImage.BuildCueText(sourceCuePath, Path.GetFileName(outputImagePath));
        await File.WriteAllTextAsync(outputCuePath, cueText, System.Text.Encoding.ASCII, cancellationToken);
        bool sourceUnchanged = string.Equals(sourceSha, Sha256File(sourceImagePath), StringComparison.OrdinalIgnoreCase);
        if (!sourceUnchanged)
            throw new InvalidDataException("The global repack writer changed its source BIN.");
        return new NativeTerrainTextureGlobalRepackResearchOutput(
            outputImagePath,
            outputCuePath,
            Sha256File(outputImagePath),
            true,
            true,
            true,
            true);
    }

    private static bool TryBuildCore(
        NativeTerrainTextureGlobalRepackResearchRequest request,
        IReadOnlyList<NativeTerrainTextureGlobalRepackSyntheticRecord> syntheticRecords,
        out NativeTerrainTextureGlobalRepackResearchPlan? plan,
        out string failureReason)
    {
        plan = null;
        failureReason = "";
        try
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(request.TargetLevel);
            ArgumentNullException.ThrowIfNull(request.DonorOverrides);
            ArgumentException.ThrowIfNullOrWhiteSpace(request.SourceImagePath);
            if (!File.Exists(request.SourceImagePath))
                throw new FileNotFoundException("Missing source image.", request.SourceImagePath);
            string donorSourceImagePath = string.IsNullOrWhiteSpace(request.DonorSourceImagePath)
                ? request.SourceImagePath
                : Path.GetFullPath(request.DonorSourceImagePath);
            if (!File.Exists(donorSourceImagePath))
                throw new FileNotFoundException("Missing immutable donor source image.", donorSourceImagePath);

            NativeTerrainTextureRuntimeControlAudit runtime =
                request.StagingSourceProof == null
                    ? NativeTerrainTextureRuntimeControlScanner.Inspect(
                        request.SourceImagePath,
                        request.TargetLevel)
                    : NativeTerrainTextureSourceAnalysisSessionCache.InspectRuntime(
                        request.SourceImagePath,
                        request.TargetLevel,
                        request.StagingSourceProof.SourceBinding,
                        out _);
            if (!runtime.Complete)
            {
                failureReason = runtime.SafetyBlockers.FirstOrDefault()
                    ?? "Runtime texture-control closure is incomplete.";
                return false;
            }

            DiscLayout layout = DiscImage.DetectLayout(request.SourceImagePath);
            using FileStream image = File.OpenRead(request.SourceImagePath);
            DiscLayout donorLayout = DiscImage.DetectLayout(donorSourceImagePath);
            using FileStream donorImage = File.OpenRead(donorSourceImagePath);
            TextureAsset retailTarget = LoadAsset(
                request.SourceImagePath,
                image,
                layout,
                request.TargetLevel.SourceWadEntry,
                initializeRuntimeState: false);
            if (runtime.TextureCount != retailTarget.Index.TextureCount)
            {
                failureReason = $"Runtime audit has {runtime.TextureCount} records but the target table has {retailTarget.Index.TextureCount}.";
                return false;
            }
            TextureAsset target = syntheticRecords.Count == 0
                ? retailTarget
                : ExpandWithSyntheticRecords(retailTarget, syntheticRecords);

            Dictionary<int, NativeTerrainTextureRelocationImport> overrides = request.DonorOverrides
                .ToDictionary(item => item.TargetTextureId);
            if (overrides.Count != request.DonorOverrides.Count)
            {
                failureReason = "Donor overrides must have unique target texture ids.";
                return false;
            }
            int[] fixedIds = runtime.ControlledTextureIds.Order().ToArray();
            int[] movableIds = Enumerable.Range(0, target.Index.TextureCount)
                .Except(fixedIds)
                .ToArray();
            if (movableIds.Length == 0)
            {
                failureReason = "The target has no runtime-persistent terrain records to repack.";
                return false;
            }
            foreach ((int targetId, NativeTerrainTextureRelocationImport import) in overrides)
            {
                if (!movableIds.Contains(targetId))
                {
                    failureReason = $"Override target T{targetId} is outside the runtime-persistent terrain set.";
                    return false;
                }
                if (!IsCompleteTier(import.DescriptorTier))
                {
                    failureReason = $"Override target T{targetId} must use the complete native descriptor tier.";
                    return false;
                }
            }

            int[] ownershipTargetIds = Enumerable
                .Range(0, retailTarget.Index.TextureCount)
                .Except(fixedIds)
                .ToArray();
            NativeTexturePageRelocationOwnershipProofResult? ownership;
            bool ownershipBuilt = request.StagingSourceProof == null
                ? NativeTexturePageOwnershipScanner.TryBuildRelocationOwnershipProof(
                    request.SourceImagePath,
                    request.TargetLevel,
                    ownershipTargetIds,
                    out ownership,
                    out failureReason)
                : NativeTerrainTextureSourceAnalysisSessionCache
                    .TryBuildRelocationOwnershipProof(
                        request.SourceImagePath,
                        request.TargetLevel,
                        ownershipTargetIds,
                        request.StagingSourceProof.SourceBinding,
                        out ownership,
                        out failureReason,
                        out _);
            if (!ownershipBuilt || ownership == null)
            {
                return false;
            }
            if (!TryValidateOwnershipTexturePagesFingerprint(
                    target.TexturePagesSha256,
                    ownership.Proof.TexturePagesSha256,
                    out failureReason))
            {
                return false;
            }

            bool[] externalProtectedBytes = BuildProtectedMask(target.TexturePages.Length, ownership.Proof);
            bool[] retainedBytes = externalProtectedBytes.ToArray();
            Dictionary<int, TextureAsset> donors = [];
            foreach (int wadEntry in overrides.Values.Select(item => item.DonorWadEntry).Distinct())
            {
                donors[wadEntry] = LoadAsset(
                    donorSourceImagePath,
                    donorImage,
                    donorLayout,
                    wadEntry,
                    initializeRuntimeState: true);
            }

            List<Work> work = [];
            foreach (int targetId in movableIds)
            {
                bool hasOverride = overrides.TryGetValue(targetId, out NativeTerrainTextureRelocationImport? supplied);
                NativeTerrainTextureRelocationImport import = hasOverride
                    ? supplied!
                    : new NativeTerrainTextureRelocationImport(
                        targetId,
                        target.WadEntry,
                        targetId,
                        "both",
                        PreserveTargetDescriptorMaterial: true);
                TextureAsset? donor = hasOverride
                    ? donors.GetValueOrDefault(import.DonorWadEntry)
                    : target;
                if (donor == null ||
                    import.DonorTextureId < 0 || import.DonorTextureId >= donor.Index.TextureCount)
                {
                    failureReason = $"Override donor WAD {import.DonorWadEntry} T{import.DonorTextureId} is outside its decoded table.";
                    return false;
                }

                TextureRecord source = donor.Index.Records[import.DonorTextureId];
                TextureRecord destination = target.Index.Records[targetId];
                if (!ValidateRecord(source, donor.WadEntry, import.DonorTextureId, out failureReason) ||
                    !ValidateRecord(destination, target.WadEntry, targetId, out failureReason))
                {
                    return false;
                }

                work.Add(CreateWork(import, donor, source.Low[0], [destination.Low[0], destination.Low[1], destination.Leading], Tier.LqAlias));
                for (int index = 0; index < source.Normal.Count; index++)
                    work.Add(CreateWork(import, donor, source.Normal[index], [destination.Normal[index]], Tier.Normal));
                for (int index = 0; index < source.Close.Count; index++)
                    work.Add(CreateWork(import, donor, source.Close[index], [destination.Close[index]], Tier.Close));
            }

            if (!BuildAliasGroups(work, out int pixelAliasPairs, out int paletteAliasPairs, out failureReason))
                return false;

            List<StorageItem> storage = BuildStorageItems(
                work,
                target,
                retainedBytes,
                out int anchoredTerrainBytes,
                out IReadOnlyList<StorageItem> packingStorage);
            bool exactRetailArtisansSource =
                target.WadEntry == 10 &&
                string.Equals(
                    target.TexturePagesSha256,
                    ArtisansRetailTexturePagesSha256,
                    StringComparison.OrdinalIgnoreCase);
            bool exactArtisansGnastyEyeContract =
                exactRetailArtisansSource &&
                overrides.Count > 0 &&
                overrides.Values.All(item =>
                    item.DonorWadEntry == 70 &&
                    item.DonorTextureId == 17);
            // A staging proof returns only binding/writer/count and can never
            // be exported.  This exact retail fingerprint may therefore try
            // the deterministic first-fit variant first; the same variant
            // remains in the final planner's fallback list, so staging cannot
            // accept a layout that a fresh final BuildPlan cannot reproduce.
            // Final Artisans/Gnasty-eye planning instead starts at its existing
            // legacy winner, skipping only strategies already proven to fail
            // and retaining byte-identical patch output.
            string? preferredFirstStrategy =
                request.StagingSourceProof != null && exactRetailArtisansSource
                    ? ArtisansStagingPackingStrategy
                    : exactArtisansGnastyEyeContract
                        ? ArtisansGnastyEyePreferredPackingStrategy
                        : null;
            if (!TryPack(
                    packingStorage,
                    retainedBytes,
                    preferredFirstStrategy,
                    out string strategy,
                    out bool[] reserved,
                    out failureReason))
            {
                failureReason = $"Alias scan: {pixelAliasPairs:N0} pixel pairs, {paletteAliasPairs:N0} palette pairs, " +
                    $"{work.Select(item => (item.SourceAsset.StorageKey, item.Source.Format, item.Source.PaletteStart)).Distinct().Count():N0} exact source palette starts. " + failureReason;
                return false;
            }

            AssignStoragePlacements(storage);
            AssignChildPlacements(work);
            byte[] afterPages = target.TexturePages.ToArray();
            int addressableLength = Math.Min(AddressableBytes, afterPages.Length);
            for (int offset = 0; offset < addressableLength; offset++)
            {
                if (!retainedBytes[offset])
                    afterPages[offset] = 0;
            }

            foreach (StorageItem item in storage)
                CopyStorage(item, afterPages);

            byte[] afterModel = target.Model.ToArray();
            foreach (Work item in work)
            {
                byte[] raw = BuildDescriptor(item);
                foreach (Descriptor destination in item.TargetDescriptors)
                    raw.CopyTo(afterModel, destination.ModelOffset);
            }

            if (!VerifyProtectedBytes(target.TexturePages, afterPages, externalProtectedBytes) ||
                !VerifyProtectedBytes(target.TexturePages, afterPages, retainedBytes))
            {
                failureReason = "The global repack modified fixed external/runtime-controlled or anchored native texture-page storage.";
                return false;
            }
            if (!VerifyFixedDescriptors(target, afterModel, movableIds.ToHashSet()))
            {
                failureReason = "The global repack modified a runtime-controlled descriptor row.";
                return false;
            }
            if (!VerifyLogicalReadback(work, afterPages, afterModel, target.Index.TextureCount, out failureReason))
                return false;
            if (!VerifyAliasRelationships(
                    work,
                    out bool pixelAliasesPreserved,
                    out bool paletteAliasesPreserved,
                    out string aliasFailure))
            {
                failureReason = "A source pixel or palette alias relationship changed after global packing. " + aliasFailure;
                return false;
            }
            if (!VerifyTargetMaterialBits(work, afterModel, target.Index.TextureCount))
            {
                failureReason = "A target terrain descriptor's non-coordinate material bits changed.";
                return false;
            }

            int allocatedPixels = storage.Where(item =>
                    item.Role == StorageRole.Pixel && !item.PlacementOwner.Anchored)
                .Sum(item => item.ByteCount);
            int allocatedPalettes = storage.Where(item =>
                    item.Role == StorageRole.Palette && !item.PlacementOwner.Anchored)
                .Sum(item => item.ByteCount);
            int protectedCount = externalProtectedBytes.Take(addressableLength).Count(value => value);
            int allocatedCount = packingStorage.Where(item => !item.Anchored).Sum(item => item.ByteCount);
            int free = addressableLength - reserved.Take(addressableLength).Count(value => value);
            IReadOnlyList<NativeTerrainTextureRelocationPatch> patches = BuildDiffPatches(
                    target.TexturePages,
                    afterPages,
                    target.TexturePagesWadOffset,
                    "terrain-texture-global-repack-data",
                    "Write the deterministic alias-preserving global terrain texture-page layout.")
                .Concat(BuildDiffPatches(
                    target.Model,
                    afterModel,
                    target.ModelWadOffset,
                    "terrain-texture-global-repack-descriptor",
                    "Point every runtime-persistent terrain record at the global packed layout."))
                .OrderBy(item => item.WadOffset)
                .ToArray();
            IReadOnlyList<NativeTerrainTextureGlobalRepackPackedRecord> packedOverrides = overrides.Values
                .OrderBy(item => item.TargetTextureId)
                .Select(item => ExtractPackedRecord(afterModel, target.Index.TextureCount, item))
                .ToArray();
            IReadOnlyList<NativeTerrainTextureGlobalRepackPackedRecord> packedMovable = work
                .GroupBy(item => item.Import.TargetTextureId)
                .OrderBy(group => group.Key)
                .Select(group => ExtractPackedRecord(afterModel, target.Index.TextureCount, group.First().Import))
                .ToArray();

            plan = new NativeTerrainTextureGlobalRepackResearchPlan(
                target.WadEntry,
                target.Index.TextureCount,
                addressableLength,
                target.TexturePagesSha256,
                movableIds,
                fixedIds,
                protectedCount,
                anchoredTerrainBytes,
                ownership.ReleasedTargetByteCount,
                allocatedPixels,
                allocatedPalettes,
                allocatedCount,
                free,
                storage.Count(item => item.Role == StorageRole.Pixel),
                storage.Count(item => item.Role == StorageRole.Palette),
                pixelAliasPairs,
                paletteAliasPairs,
                work.Sum(item => item.TargetDescriptors.Count),
                checked(fixedIds.Length * DescriptorsPerRecord),
                patches.Where(item => item.Kind == "terrain-texture-global-repack-data").Sum(item => item.ByteLength),
                patches.Where(item => item.Kind == "terrain-texture-global-repack-descriptor").Sum(item => item.ByteLength),
                strategy,
                true,
                true,
                pixelAliasesPreserved,
                paletteAliasesPreserved,
                work.Where(item => item.Tier == Tier.LqAlias).All(item => item.TargetDescriptors.Count == 3),
                true,
                true,
                true,
                true,
                packedOverrides,
                patches,
                [
                    "Every runtime-persistent terrain record is packed as one collective union; runtime-controlled records and all decoded/unclassified external bytes remain fixed.",
                    "Pixel containment aliases and exact/partial cross-format palette aliases are retained by shared storage groups; physical cross-role and cross-format alias components retain one exact linear source-to-target translation.",
                    "This plan proves descriptor encodability and exact static readback only. DuckStation runtime proof is still required before editor/export integration."
                ])
            {
                PackedMovableRecords = packedMovable
            };
            failureReason = "";
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidDataException or InvalidOperationException or IOException or OverflowException)
        {
            failureReason = ex.Message;
            return false;
        }
    }

    internal static bool TryValidateOwnershipTexturePagesFingerprint(
        string currentTexturePagesSha256,
        string ownershipTexturePagesSha256,
        out string failureReason)
    {
        failureReason = "";
        if (!IsSha256(currentTexturePagesSha256) ||
            !IsSha256(ownershipTexturePagesSha256))
        {
            failureReason =
                "The current target or relocation-ownership proof is missing an exact full texture-pages subfile SHA-256 fingerprint.";
            return false;
        }
        if (!string.Equals(
                currentTexturePagesSha256,
                ownershipTexturePagesSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            failureReason =
                "The target texture-pages subfile changed after the relocation ownership proof was captured; discard the cached staging proof and run the safety check again.";
            return false;
        }
        return true;
    }

    private static Work CreateWork(
        NativeTerrainTextureRelocationImport import,
        TextureAsset sourceAsset,
        Descriptor source,
        IReadOnlyList<Descriptor> targets,
        Tier tier)
    {
        if (!CanRead(source, sourceAsset.TexturePages.Length))
            throw new InvalidDataException($"Donor WAD {sourceAsset.WadEntry} T{import.DonorTextureId} {tier} descriptor is not fully readable.");
        return new Work(import, sourceAsset, source, targets, tier, GetPixelBounds(source));
    }

    private static bool BuildAliasGroups(
        IReadOnlyList<Work> work,
        out int pixelAliasPairs,
        out int paletteAliasPairs,
        out string failureReason)
    {
        pixelAliasPairs = 0;
        paletteAliasPairs = 0;
        failureReason = "";
        List<Work> pixelOwners = [];
        foreach (Work item in work.OrderByDescending(item => item.SourcePixelBounds.ByteCount)
                     .ThenBy(item => item.Import.TargetTextureId)
                     .ThenBy(item => item.Tier)
                     .ThenBy(item => item.Source.Index))
        {
            foreach (Work other in pixelOwners.Where(owner =>
                         owner.SourceAsset.StorageKey == item.SourceAsset.StorageKey &&
                         owner.Source.Format == item.Source.Format))
            {
                if (other.SourcePixelBounds.Intersects(item.SourcePixelBounds) &&
                    !other.SourcePixelBounds.Contains(item.SourcePixelBounds))
                {
                    failureReason = "The source contains a partially-overlapping terrain pixel alias that the rectangular global packer cannot preserve.";
                    return false;
                }
            }

            Work? pixelOwner = pixelOwners.FirstOrDefault(owner =>
                owner.SourceAsset.StorageKey == item.SourceAsset.StorageKey &&
                owner.Source.Format == item.Source.Format &&
                owner.SourcePixelBounds.Contains(item.SourcePixelBounds));
            item.PixelOwner = pixelOwner ?? item;
            if (pixelOwner == null)
                pixelOwners.Add(item);
            else
                pixelAliasPairs++;

        }

        int[] parents = Enumerable.Range(0, work.Count).ToArray();
        for (int first = 0; first < work.Count; first++)
        {
            for (int second = first + 1; second < work.Count; second++)
            {
                Work left = work[first];
                Work right = work[second];
                if (left.SourceAsset.StorageKey != right.SourceAsset.StorageKey ||
                    !PalettesIntersect(left.Source, right.Source))
                    continue;
                Union(parents, first, second);
                paletteAliasPairs++;
            }
        }
        foreach (IGrouping<int, int> component in Enumerable.Range(0, work.Count)
                     .GroupBy(index => Find(parents, index)))
        {
            Work[] members = component.Select(index => work[index]).ToArray();
            int[] offsets = members.SelectMany(item => EnumeratePaletteOffsets(item.Source)).Distinct().Order().ToArray();
            int sourceOriginOffset = members.Min(item => checked((int)item.Source.PaletteStart));
            Cell[] normalizedCells = offsets.Select(offset =>
            {
                int delta = checked(offset - sourceOriginOffset);
                return new Cell(delta % RowBytes, delta / RowBytes);
            }).ToArray();
            PaletteGroup group = new(
                members[0].SourceAsset,
                members.Any(item => item.Source.Format == Format.Lq4),
                members.Any(item => item.Source.Format == Format.Hq8),
                members,
                offsets,
                sourceOriginOffset,
                normalizedCells.Max(cell => cell.X) + 1,
                normalizedCells.Max(cell => cell.Y) + 1);
            foreach (Work member in members)
                member.PaletteGroup = group;
        }

        return true;
    }

    private static List<StorageItem> BuildStorageItems(
        IReadOnlyList<Work> work,
        TextureAsset targetAsset,
        bool[] protectedBytes,
        out int anchoredTerrainBytes,
        out IReadOnlyList<StorageItem> packingItems)
    {
        List<StorageItem> result = [];
        anchoredTerrainBytes = 0;
        foreach (Work owner in work.Where(item => ReferenceEquals(item.PixelOwner, item)))
        {
            Bounds bounds = owner.SourcePixelBounds;
            StorageKind kind = owner.Tier == Tier.LqAlias
                ? StorageKind.LqPixel
                : bounds.Width == 32 ? StorageKind.Pixel32 : StorageKind.Pixel16;
            int sourceOriginOffset = checked((bounds.Y * RowBytes) + bounds.X);
            int[] sourceOffsets = Enumerable.Range(0, bounds.Height)
                .SelectMany(row => Enumerable.Range(0, bounds.Width)
                    .Select(column => checked(sourceOriginOffset + (row * RowBytes) + column)))
                .ToArray();
            StorageItem item = new(
                owner,
                StorageRole.Pixel,
                kind,
                bounds.Width,
                bounds.Height,
                sourceOriginOffset,
                sourceOffsets);
            if (ReferenceEquals(owner.SourceAsset, targetAsset))
            {
                item.PreferredX = bounds.X;
                item.PreferredY = bounds.Y;
            }
            owner.PixelStorage = item;
            result.Add(item);
        }
        foreach (Work item in work.Where(item => !ReferenceEquals(item.PixelOwner, item)))
            item.PixelStorage = item.PixelOwner.PixelStorage;

        foreach (PaletteGroup group in work.Select(item => item.PaletteGroup)
                     .Distinct(ReferenceComparer<PaletteGroup>.Instance))
        {
            StorageKind kind = group.HasLq && group.HasHq
                ? StorageKind.CombinedPalette
                : group.HasLq ? StorageKind.LqPalette : StorageKind.HqPalette;
            StorageItem item = new(
                group.Members[0],
                StorageRole.Palette,
                kind,
                group.Width,
                group.Height,
                group.SourceOriginOffset,
                group.SourceOffsets,
                group.SourceOffsets.Select(offset =>
                {
                    int delta = checked(offset - group.SourceOriginOffset);
                    return new Cell(delta % RowBytes, delta / RowBytes);
                }).ToArray(),
                xModulo: group.SourceOriginX & 31,
                yModulo: group.HasLq ? group.SourceOriginY & 3 : 0);
            if (ReferenceEquals(group.SourceAsset, targetAsset))
            {
                item.PreferredX = group.SourceOriginX;
                item.PreferredY = group.SourceOriginY;
            }
            group.Storage = item;
            result.Add(item);
        }
        foreach (Work item in work)
            item.PaletteStorage = item.PaletteGroup.Storage;
        packingItems = BuildStoragePackingItems(result, targetAsset);
        foreach (StorageItem item in packingItems.Where(item =>
                     ReferenceEquals(item.Owner.SourceAsset, targetAsset) &&
                     item.SourceOffsets.Any(offset => protectedBytes[offset])))
        {
            // A native target component that already shares even one byte with
            // fixed/external storage cannot be relocated without duplicating
            // that overlap. Keep the complete translation-connected union at
            // its retail origin and retain all of its bytes atomically.
            item.Anchored = true;
            item.X = item.SourceOriginOffset % RowBytes;
            item.Y = item.SourceOriginOffset / RowBytes;
            foreach (int offset in item.SourceOffsets)
            {
                if (!protectedBytes[offset])
                {
                    protectedBytes[offset] = true;
                    anchoredTerrainBytes++;
                }
            }
        }
        return result;
    }

    private static IReadOnlyList<StorageItem> BuildStoragePackingItems(
        IReadOnlyList<StorageItem> sourceItems,
        TextureAsset targetAsset)
    {
        int[] parents = Enumerable.Range(0, sourceItems.Count).ToArray();
        Dictionary<(string StorageKey, int Offset), int> byteOwners = [];
        for (int index = 0; index < sourceItems.Count; index++)
        {
            StorageItem item = sourceItems[index];
            foreach (int offset in item.SourceOffsets)
            {
                (string StorageKey, int Offset) key = (item.Owner.SourceAsset.StorageKey, offset);
                if (byteOwners.TryGetValue(key, out int prior))
                    Union(parents, prior, index);
                else
                    byteOwners[key] = index;
            }
        }

        List<StorageItem> result = [];
        foreach (IGrouping<int, int> component in Enumerable.Range(0, sourceItems.Count)
                     .GroupBy(index => Find(parents, index)))
        {
            StorageItem[] children = component.Select(index => sourceItems[index]).ToArray();
            if (children.Length == 1)
            {
                result.Add(children[0]);
                continue;
            }

            int[] offsets = children.SelectMany(item => item.SourceOffsets).Distinct().Order().ToArray();
            int sourceOriginOffset = offsets[0];
            Cell[] cells = offsets.Select(offset =>
            {
                int delta = checked(offset - sourceOriginOffset);
                return new Cell(delta % RowBytes, delta / RowBytes);
            }).ToArray();
            bool containsPalette = children.Any(item => item.Role == StorageRole.Palette);
            bool containsLqPalette = children.Any(item =>
                item.Role == StorageRole.Palette && item.Owner.PaletteGroup.HasLq);
            StorageItem compound = new(
                children[0].Owner,
                StorageRole.Compound,
                StorageKind.SharedAlias,
                cells.Max(cell => cell.X) + 1,
                cells.Max(cell => cell.Y) + 1,
                sourceOriginOffset,
                offsets,
                cells,
                xModulo: containsPalette ? sourceOriginOffset % RowBytes & 31 : 0,
                // A compound can shift across the packed-row seam, changing
                // the target row of an LQ CLUT. Search every row and let the
                // exact child validation enforce its four-row encoding.
                yModulo: 0,
                children: children);
            if (children.All(item => ReferenceEquals(item.Owner.SourceAsset, targetAsset)))
            {
                compound.PreferredX = sourceOriginOffset % RowBytes;
                compound.PreferredY = sourceOriginOffset / RowBytes;
            }
            foreach (StorageItem child in children)
                child.PlacementOwner = compound;
            result.Add(compound);
        }
        return result;
    }

    private static bool TryPack(
        IReadOnlyList<StorageItem> items,
        bool[] protectedBytes,
        string? preferredFirstStrategy,
        out string strategy,
        out bool[] reserved,
        out string failureReason)
    {
        int retainedByteCount = protectedBytes.Take(AddressableBytes).Count(value => value);
        int movableByteCount = items.Where(item => !item.Anchored).Sum(item => item.ByteCount);
        int requiredByteCount = checked(retainedByteCount + movableByteCount);
        int tightHeadroom = Math.Min(AddressableBytes, protectedBytes.Length) - requiredByteCount;
        if (tightHeadroom < 0)
        {
            strategy = "";
            reserved = protectedBytes.ToArray();
            failureReason = $"Exact terrain storage lower bound exceeds the 0x{AddressableBytes:X} page: " +
                $"{requiredByteCount:N0} bytes required, {Math.Min(AddressableBytes, protectedBytes.Length):N0} available " +
                $"({-tightHeadroom:N0} bytes over capacity). Use shared-record replacement instead of a private append.";
            return false;
        }
        string earlyRepairFailure = "";
        bool triedEarlyRepair = tightHeadroom <= 14_500;
        if (triedEarlyRepair && TryPackNativeSelectiveRepair(
                items,
                protectedBytes,
                out strategy,
                out reserved,
                out earlyRepairFailure))
        {
            failureReason = "";
            return true;
        }
        if (triedEarlyRepair)
        {
            strategy = "";
            reserved = protectedBytes.ToArray();
            failureReason = earlyRepairFailure;
            return false;
        }

        (string Name, StorageKind[] Order, bool Reverse, bool DonorFirst, bool NativeFirst, bool HonorPreferred, bool FirstFit)[] variants =
        [
            ("native-layout-then-donor-holes", [StorageKind.SharedAlias, StorageKind.CombinedPalette, StorageKind.HqPalette, StorageKind.LqPalette, StorageKind.LqPixel, StorageKind.Pixel32, StorageKind.Pixel16], false, false, true, true, false),
            ("native-layout-donor-pixels-first", [StorageKind.SharedAlias, StorageKind.Pixel32, StorageKind.LqPixel, StorageKind.Pixel16, StorageKind.CombinedPalette, StorageKind.HqPalette, StorageKind.LqPalette], false, false, true, true, false),
            ("native-layout-donor-pixels-first-reverse", [StorageKind.SharedAlias, StorageKind.Pixel32, StorageKind.LqPixel, StorageKind.Pixel16, StorageKind.CombinedPalette, StorageKind.HqPalette, StorageKind.LqPalette], true, false, true, true, false),
            ("donor-first-native-layout", [StorageKind.SharedAlias, StorageKind.CombinedPalette, StorageKind.HqPalette, StorageKind.LqPalette, StorageKind.LqPixel, StorageKind.Pixel32, StorageKind.Pixel16], false, true, false, true, false),
            ("palette-components-first-best-contact", [StorageKind.SharedAlias, StorageKind.CombinedPalette, StorageKind.HqPalette, StorageKind.LqPalette, StorageKind.LqPixel, StorageKind.Pixel32, StorageKind.Pixel16], false, false, false, false, false),
            ("lq-first-palette-components-best-contact", [StorageKind.SharedAlias, StorageKind.LqPixel, StorageKind.CombinedPalette, StorageKind.HqPalette, StorageKind.LqPalette, StorageKind.Pixel32, StorageKind.Pixel16], false, false, false, false, false),
            ("palette-components-pixels-best-contact", [StorageKind.SharedAlias, StorageKind.CombinedPalette, StorageKind.HqPalette, StorageKind.LqPixel, StorageKind.Pixel32, StorageKind.LqPalette, StorageKind.Pixel16], false, false, false, false, false),
            ("palette-components-first-reverse-contact", [StorageKind.SharedAlias, StorageKind.CombinedPalette, StorageKind.HqPalette, StorageKind.LqPalette, StorageKind.LqPixel, StorageKind.Pixel32, StorageKind.Pixel16], true, false, false, false, false),
            // Several dense retail levels retain enough total free bytes but the
            // palette-first strategies split those bytes into strips narrower
            // than one 32x32 indexed-pixel block. Reserve the least-flexible
            // pixel rectangles first, then let sparse/aligned palettes fill the
            // remaining cavities. Both scan directions are deterministic.
            ("pixel32-first-large-pixels-contact", [StorageKind.SharedAlias, StorageKind.Pixel32, StorageKind.LqPixel, StorageKind.Pixel16, StorageKind.CombinedPalette, StorageKind.HqPalette, StorageKind.LqPalette], false, false, false, false, false),
            ("pixel32-first-palettes-contact", [StorageKind.SharedAlias, StorageKind.Pixel32, StorageKind.CombinedPalette, StorageKind.HqPalette, StorageKind.LqPalette, StorageKind.LqPixel, StorageKind.Pixel16], false, false, false, false, false),
            ("pixel32-first-large-pixels-reverse-contact", [StorageKind.SharedAlias, StorageKind.Pixel32, StorageKind.LqPixel, StorageKind.Pixel16, StorageKind.CombinedPalette, StorageKind.HqPalette, StorageKind.LqPalette], true, false, false, false, false),
            ("all-pixels-first-reverse-contact", [StorageKind.SharedAlias, StorageKind.Pixel32, StorageKind.Pixel16, StorageKind.LqPixel, StorageKind.CombinedPalette, StorageKind.HqPalette, StorageKind.LqPalette], true, false, false, false, false),
            ("palette-first-forward-first-fit", [StorageKind.SharedAlias, StorageKind.CombinedPalette, StorageKind.HqPalette, StorageKind.LqPalette, StorageKind.Pixel32, StorageKind.LqPixel, StorageKind.Pixel16], false, false, false, false, true),
            ("palette-first-reverse-first-fit", [StorageKind.SharedAlias, StorageKind.CombinedPalette, StorageKind.HqPalette, StorageKind.LqPalette, StorageKind.Pixel32, StorageKind.LqPixel, StorageKind.Pixel16], true, false, false, false, true),
            ("pixels-first-forward-first-fit", [StorageKind.SharedAlias, StorageKind.Pixel32, StorageKind.LqPixel, StorageKind.Pixel16, StorageKind.CombinedPalette, StorageKind.HqPalette, StorageKind.LqPalette], false, false, false, false, true),
            ("pixels-first-reverse-first-fit", [StorageKind.SharedAlias, StorageKind.Pixel32, StorageKind.LqPixel, StorageKind.Pixel16, StorageKind.CombinedPalette, StorageKind.HqPalette, StorageKind.LqPalette], true, false, false, false, true)
        ];

        List<string> failures = [];
        bool[] nativePreferredPixels = new bool[protectedBytes.Length];
        bool[] nativePreferredPalettes = new bool[protectedBytes.Length];
        foreach (StorageItem item in items.Where(item => item.HasPreferred))
            MarkStorage(
                item.Role == StorageRole.Palette ? nativePreferredPalettes : nativePreferredPixels,
                item,
                item.PreferredX,
                item.PreferredY);
        IEnumerable<(string Name, StorageKind[] Order, bool Reverse, bool DonorFirst, bool NativeFirst, bool HonorPreferred, bool FirstFit)>
            orderedVariants = string.IsNullOrWhiteSpace(preferredFirstStrategy)
                ? variants
                : variants
                    .Where(variant => string.Equals(
                        variant.Name,
                        preferredFirstStrategy,
                        StringComparison.Ordinal))
                    .Concat(variants.Where(variant => !string.Equals(
                        variant.Name,
                        preferredFirstStrategy,
                        StringComparison.Ordinal)));
        foreach ((string name, StorageKind[] order, bool reverse, bool donorFirst, bool nativeFirst, bool honorPreferred, bool firstFit) in orderedVariants)
        {
            bool[] trial = protectedBytes.ToArray();
            foreach (StorageItem item in items.Where(item => !item.Anchored))
            {
                item.X = -1;
                item.Y = -1;
            }
            bool failed = false;
            IEnumerable<StorageItem> packingOrder = order
                .SelectMany(kind => items.Where(item => !item.Anchored && item.Kind == kind)
                    .OrderByDescending(item => item.ByteCount)
                    .ThenBy(item => item.Owner.Import.TargetTextureId)
                    .ThenBy(item => item.Owner.Source.Index));
            if (donorFirst)
            {
                Dictionary<StorageKind, int> ranks = order
                    .Select((kind, index) => (kind, index))
                    .ToDictionary(item => item.kind, item => item.index);
                packingOrder = items.Where(item => !item.Anchored)
                    .OrderBy(item => item.HasPreferred ? 1 : 0)
                    .ThenBy(item => ranks[item.Kind])
                    .ThenByDescending(item => item.ByteCount)
                    .ThenBy(item => item.Owner.Import.TargetTextureId)
                    .ThenBy(item => item.Owner.Source.Index);
            }
            else if (nativeFirst)
            {
                Dictionary<StorageKind, int> ranks = order
                    .Select((kind, index) => (kind, index))
                    .ToDictionary(item => item.kind, item => item.index);
                packingOrder = items.Where(item => !item.Anchored)
                    .OrderBy(item => item.HasPreferred ? 0 : 1)
                    .ThenBy(item => ranks[item.Kind])
                    .ThenByDescending(item => item.ByteCount)
                    .ThenBy(item => item.Owner.Import.TargetTextureId)
                    .ThenBy(item => item.Owner.Source.Index);
            }
            foreach (StorageItem item in packingOrder)
            {
                if (!TryPlace(item, trial, reverse, honorPreferred, firstFit, nativePreferredPixels, nativePreferredPalettes))
                {
                    int placedBytes = items.Where(candidate => candidate.Anchored || candidate.X >= 0)
                        .Sum(candidate => candidate.ByteCount);
                    int openBytes = Math.Min(AddressableBytes, trial.Length) - trial.Take(AddressableBytes).Count(value => value);
                    failures.Add($"{name}: blocked at {item.Kind} {item.Width}x{item.Height} " +
                        $"for target T{item.Owner.Import.TargetTextureId} from W{item.Owner.SourceAsset.WadEntry} " +
                        $"({(item.HasPreferred ? "native-preferred" : "donor")}) after {placedBytes:N0} storage bytes; " +
                        $"{openBytes:N0} bytes remained fragmented");
                    failed = true;
                    break;
                }
            }
            if (!failed)
            {
                strategy = name;
                reserved = trial;
                failureReason = "";
                return true;
            }
        }

        string repairFailure = "";
        if (!triedEarlyRepair && TryPackNativeSelectiveRepair(
                items,
                protectedBytes,
                out string repairStrategy,
                out bool[] repairReserved,
                out repairFailure))
        {
            strategy = repairStrategy;
            reserved = repairReserved;
            failureReason = "";
            return true;
        }
        failures.Add(triedEarlyRepair ? earlyRepairFailure : repairFailure);

        strategy = "";
        reserved = protectedBytes.ToArray();
        string inventory = string.Join(", ", items
            .GroupBy(item => item.Kind)
            .OrderBy(group => group.Key)
            .Select(group => $"{group.Key}={group.Count()}/{group.Sum(item => item.ByteCount):N0}B anchored {group.Count(item => item.Anchored)}/{group.Where(item => item.Anchored).Sum(item => item.ByteCount):N0}B"));
        failureReason = $"Inventory: {inventory}. " + string.Join(" | ", failures);
        return false;
    }

    private static bool TryPackNativeSelectiveRepair(
        IReadOnlyList<StorageItem> items,
        bool[] protectedBytes,
        out string strategy,
        out bool[] reserved,
        out string failureReason)
    {
        string lastFailure = "no repair attempt ran";
        if (TryPackDonorFirstConflictClosure(
                items,
                protectedBytes,
                out reserved,
                out int closureMovedRoots,
                out int closureMovedBytes,
                out string closureMode,
                out lastFailure))
        {
            strategy = $"native-conflict-csp-{closureMode}-" +
                $"moved-{closureMovedRoots}-roots-{closureMovedBytes}-bytes";
            failureReason = "";
            return true;
        }

        if (TryPackNativeWindowRepair(
                items,
                protectedBytes,
                out reserved,
                out int windowMovedRoots,
                out int windowMovedBytes,
                out string windowShape,
                out lastFailure))
        {
            strategy = $"native-window-repair-{windowShape}-" +
                $"moved-{windowMovedRoots}-roots-{windowMovedBytes}-bytes";
            failureReason = "";
            return true;
        }

        strategy = "";
        reserved = protectedBytes.ToArray();
        failureReason = "native-selective-repair: bounded CSP neighborhood search exhausted; " + lastFailure;
        return false;
    }

    private static bool TryPackDonorFirstConflictClosure(
        IReadOnlyList<StorageItem> items,
        bool[] protectedBytes,
        out bool[] reserved,
        out int movedRootCount,
        out int movedByteCount,
        out string mode,
        out string failureReason)
    {
        long deadline = Stopwatch.GetTimestamp() + (15 * Stopwatch.Frequency);
        for (int orderMode = 0; orderMode < 2; orderMode++)
        {
            bool[] partial = protectedBytes.ToArray();
            bool[] preferredPixels = new bool[protectedBytes.Length];
            bool[] preferredPalettes = new bool[protectedBytes.Length];
            foreach (StorageItem preferred in items.Where(item => item.HasPreferred))
                MarkStorage(preferred.Role == StorageRole.Palette ? preferredPalettes : preferredPixels,
                    preferred, preferred.PreferredX, preferred.PreferredY);
            foreach (StorageItem item in items.Where(item => !item.Anchored))
            {
                item.X = -1;
                item.Y = -1;
            }
            StorageItem[] donors = items.Where(item => !item.Anchored && !item.HasPreferred)
                .OrderBy(item => orderMode == 0
                    ? RepairOrderRankPixelsFirst(item.Kind)
                    : RepairOrderRankPalettesFirst(item.Kind))
                .ThenByDescending(item => item.ByteCount)
                .ThenBy(item => item.Owner.Import.TargetTextureId)
                .ThenBy(item => item.Owner.Source.Index)
                .ToArray();
            StorageItem[] natives = items.Where(item => !item.Anchored && item.HasPreferred)
                .OrderByDescending(item => item.ByteCount)
                .ThenBy(item => item.Owner.Import.TargetTextureId)
                .ThenBy(item => item.Kind)
                .ThenBy(item => item.Owner.Source.Index)
                .ToArray();
            StorageItem? blocked = null;
            foreach (StorageItem item in donors.Concat(natives))
            {
                if (TryPlace(item, partial, false, true, false, preferredPixels, preferredPalettes))
                    continue;
                blocked = item;
                break;
            }
            if (blocked == null)
            {
                reserved = partial;
                movedRootCount = natives.Count(item =>
                    item.X != item.PreferredX || item.Y != item.PreferredY);
                movedByteCount = natives.Where(item =>
                        item.X != item.PreferredX || item.Y != item.PreferredY)
                    .Sum(item => item.ByteCount);
                mode = $"direct-o{orderMode}";
                failureReason = "";
                return true;
            }

            HashSet<StorageItem> evicted = natives.Where(item => item.X >= 0 &&
                    (item.X != item.PreferredX || item.Y != item.PreferredY))
                .ToHashSet(ReferenceComparer<StorageItem>.Instance);
            if (blocked.HasPreferred)
                evicted.Add(blocked);
            if (evicted.Count == 0 || evicted.Count > 96)
                continue;
            bool[] baseline = protectedBytes.ToArray();
            bool baselineValid = true;
            foreach (StorageItem native in natives.Where(item => !evicted.Contains(item)))
            {
                native.X = native.PreferredX;
                native.Y = native.PreferredY;
                if (!IsStorageFree(baseline, native, native.X, native.Y) ||
                    !IsStoragePlacementEncodable(native, native.X, native.Y))
                {
                    baselineValid = false;
                    break;
                }
                MarkStorage(baseline, native, native.X, native.Y);
            }
            if (!baselineValid)
                continue;
            StorageItem[] subset = evicted.Concat(donors)
                .Distinct(ReferenceComparer<StorageItem>.Instance)
                .ToArray();
            if (Stopwatch.GetTimestamp() >= deadline ||
                !TryPackRepairSubset(subset, baseline, true, deadline, out bool[] repaired))
            {
                continue;
            }
            reserved = repaired;
            movedRootCount = evicted.Count;
            movedByteCount = evicted.Sum(item => item.ByteCount);
            mode = $"o{orderMode}";
            failureReason = "";
            return true;
        }

        reserved = protectedBytes.ToArray();
        movedRootCount = 0;
        movedByteCount = 0;
        mode = "none";
        failureReason = "donor-first conflict-closure CSP exhausted its 15-second bound.";
        return false;
    }

    private static bool TryPackNativeWindowRepair(
        IReadOnlyList<StorageItem> items,
        bool[] protectedBytes,
        out bool[] reserved,
        out int movedRootCount,
        out int movedByteCount,
        out string windowShape,
        out string failureReason)
    {
        int[] nativeIndices = Enumerable.Range(0, items.Count)
            .Where(index => !items[index].Anchored && items[index].HasPreferred)
            .ToArray();
        StorageItem[] donors = items.Where(item => !item.Anchored && !item.HasPreferred).ToArray();
        int[] nativeOwner = Enumerable.Repeat(-1, protectedBytes.Length).ToArray();
        foreach (int index in nativeIndices)
        {
            StorageItem item = items[index];
            int origin = checked((item.PreferredY * RowBytes) + item.PreferredX);
            foreach (Cell cell in item.Cells)
                nativeOwner[checked(origin + (cell.Y * RowBytes) + cell.X)] = index;
        }

        List<RepairWindow> windows = [];
        HashSet<string> signatures = [];
        long searchDeadline = Stopwatch.GetTimestamp() + (15 * Stopwatch.Frequency);
        foreach ((int width, int height) in new[]
                 {
                     (512, 64), (256, 128), (128, 256), (512, 128), (1024, 64)
                 })
        {
            for (int y = 0; y + height <= VramRows; y += 32)
            {
                for (int x = 0; x + width <= RowBytes; x += 32)
                {
                    HashSet<int> conflicts = [];
                    int protectedCount = 0;
                    for (int row = 0; row < height; row++)
                    {
                        int start = ((y + row) * RowBytes) + x;
                        for (int offset = start; offset < start + width; offset++)
                        {
                            if (protectedBytes[offset])
                                protectedCount++;
                            if (nativeOwner[offset] >= 0)
                                conflicts.Add(nativeOwner[offset]);
                        }
                    }
                    if (conflicts.Count == 0 || conflicts.Count > 64)
                        continue;
                    int[] ordered = conflicts.Order().ToArray();
                    string signature = string.Join(',', ordered);
                    if (!signatures.Add(signature))
                        continue;
                    int bytes = ordered.Sum(index => items[index].ByteCount);
                    windows.Add(new RepairWindow(x, y, width, height, ordered, protectedCount, bytes));
                }
            }
        }

        int windowAttempt = 0;
        foreach (RepairWindow window in windows
                     .OrderBy(item => item.ProtectedBytes)
                     .ThenBy(item => item.ConflictIndices.Count)
                     .ThenBy(item => item.MovedBytes)
                     .ThenBy(item => item.Y)
                     .ThenBy(item => item.X)
                     .Take(96))
        {
            if (Stopwatch.GetTimestamp() >= searchDeadline)
                break;
            windowAttempt++;
            HashSet<int> evicted = window.ConflictIndices.ToHashSet();
            bool[] baseline = protectedBytes.ToArray();
            bool baselineValid = true;
            foreach (int index in nativeIndices.Where(index => !evicted.Contains(index)))
            {
                StorageItem item = items[index];
                item.X = item.PreferredX;
                item.Y = item.PreferredY;
                if (!IsStorageFree(baseline, item, item.X, item.Y) ||
                    !IsStoragePlacementEncodable(item, item.X, item.Y))
                {
                    baselineValid = false;
                    break;
                }
                MarkStorage(baseline, item, item.X, item.Y);
            }
            if (!baselineValid)
                continue;

            StorageItem[] subset = window.ConflictIndices.Select(index => items[index])
                .Concat(donors)
                .Distinct(ReferenceComparer<StorageItem>.Instance)
                .ToArray();
            if (!TryPackRepairSubset(
                    subset,
                    baseline,
                    windowAttempt <= 16,
                    searchDeadline,
                    out bool[] trial))
                continue;

            reserved = trial;
            movedRootCount = window.ConflictIndices.Count;
            movedByteCount = window.MovedBytes;
            windowShape = $"{window.Width}x{window.Height}-at-{window.X}-{window.Y}";
            failureReason = "";
            return true;
        }

        reserved = protectedBytes.ToArray();
        movedRootCount = 0;
        movedByteCount = 0;
        windowShape = "none";
        failureReason = $"window repair exhausted {Math.Min(96, windows.Count)} deterministic conflict neighborhoods.";
        return false;
    }

    private static bool TryPackRepairSubset(
        IReadOnlyList<StorageItem> subset,
        bool[] baseline,
        bool runMrv,
        long searchDeadline,
        out bool[] reserved)
    {
        (StorageKind[] Order, bool Reverse, bool FirstFit)[] variants =
        [
            ([StorageKind.SharedAlias, StorageKind.Pixel32, StorageKind.LqPixel, StorageKind.Pixel16, StorageKind.CombinedPalette, StorageKind.HqPalette, StorageKind.LqPalette], false, true),
            ([StorageKind.SharedAlias, StorageKind.CombinedPalette, StorageKind.HqPalette, StorageKind.LqPalette, StorageKind.Pixel32, StorageKind.LqPixel, StorageKind.Pixel16], false, true),
            ([StorageKind.SharedAlias, StorageKind.Pixel32, StorageKind.LqPixel, StorageKind.Pixel16, StorageKind.CombinedPalette, StorageKind.HqPalette, StorageKind.LqPalette], true, true),
            ([StorageKind.SharedAlias, StorageKind.CombinedPalette, StorageKind.HqPalette, StorageKind.LqPalette, StorageKind.Pixel32, StorageKind.LqPixel, StorageKind.Pixel16], true, true),
            ([StorageKind.SharedAlias, StorageKind.Pixel32, StorageKind.LqPixel, StorageKind.Pixel16, StorageKind.CombinedPalette, StorageKind.HqPalette, StorageKind.LqPalette], false, false),
            ([StorageKind.SharedAlias, StorageKind.CombinedPalette, StorageKind.HqPalette, StorageKind.LqPalette, StorageKind.Pixel32, StorageKind.LqPixel, StorageKind.Pixel16], false, false)
        ];
        bool[] noPreferredPixels = new bool[baseline.Length];
        bool[] noPreferredPalettes = new bool[baseline.Length];
        foreach ((StorageKind[] order, bool reverse, bool firstFit) in variants)
        {
            bool[] trial = baseline.ToArray();
            foreach (StorageItem item in subset)
            {
                item.X = -1;
                item.Y = -1;
            }
            bool failed = false;
            foreach (StorageItem item in order.SelectMany(kind => subset
                         .Where(candidate => candidate.Kind == kind)
                         .OrderByDescending(candidate => candidate.ByteCount)
                         .ThenBy(candidate => candidate.Owner.Import.TargetTextureId)
                         .ThenBy(candidate => candidate.Owner.Source.Index)))
            {
                if (TryPlace(item, trial, reverse, false, firstFit, noPreferredPixels, noPreferredPalettes))
                    continue;
                failed = true;
                break;
            }
            if (!failed)
            {
                reserved = trial;
                return true;
            }
        }

        if (!runMrv)
        {
            reserved = baseline.ToArray();
            return false;
        }
        foreach ((int candidateLimit, int nodeBudget) in new[] { (64, 500_000) })
        {
            if (TryPackRepairSubsetMrv(
                    subset,
                    baseline,
                    candidateLimit,
                    nodeBudget,
                    searchDeadline,
                    out reserved))
            {
                return true;
            }
        }
        reserved = baseline.ToArray();
        return false;
    }

    private static bool TryPackRepairSubsetMrv(
        IReadOnlyList<StorageItem> subset,
        bool[] baseline,
        int candidateLimit,
        int nodeBudget,
        long searchDeadline,
        out bool[] reserved)
    {
        StorageItem[] ordered = subset
            .OrderByDescending(item => item.ByteCount)
            .ThenBy(item => item.Kind)
            .ThenBy(item => item.Owner.Import.TargetTextureId)
            .ThenBy(item => item.Owner.Source.Index)
            .ToArray();
        RepairCspOption[][] candidates = ordered.Select(item =>
                BuildRepairSubsetCandidates(item, ordered, baseline, Math.Min(64, candidateLimit)))
            .ToArray();
        if (candidates.Any(list => list.Length == 0))
        {
            reserved = baseline.ToArray();
            return false;
        }

        int itemCount = ordered.Length;
        ulong[][][] incompatibility = new ulong[itemCount][][];
        for (int itemIndex = 0; itemIndex < itemCount; itemIndex++)
        {
            incompatibility[itemIndex] = new ulong[candidates[itemIndex].Length][];
            for (int candidateIndex = 0; candidateIndex < candidates[itemIndex].Length; candidateIndex++)
                incompatibility[itemIndex][candidateIndex] = new ulong[itemCount];
        }
        for (int first = 0; first < itemCount; first++)
        {
            for (int second = first + 1; second < itemCount; second++)
            {
                for (int left = 0; left < candidates[first].Length; left++)
                {
                    for (int right = 0; right < candidates[second].Length; right++)
                    {
                        if (!RepairOptionsConflict(candidates[first][left], candidates[second][right]))
                            continue;
                        incompatibility[first][left][second] |= 1UL << right;
                        incompatibility[second][right][first] |= 1UL << left;
                    }
                }
            }
        }

        bool[] trial = baseline.ToArray();
        bool[] placed = new bool[ordered.Length];
        ulong[] domains = candidates.Select(list => list.Length == 64
                ? ulong.MaxValue
                : (1UL << list.Length) - 1UL)
            .ToArray();
        string[] geometryKeys = ordered.Select(RepairGeometryKey).ToArray();
        HashSet<(int PlacedCount, ulong StateHash)> memo = [];
        int nodes = 0;
        int remainingBytes = ordered.Sum(item => item.ByteCount);
        int initialFreeBytes = Math.Min(AddressableBytes, trial.Length) -
            trial.Take(AddressableBytes).Count(value => value);
        if (initialFreeBytes < remainingBytes)
        {
            reserved = baseline.ToArray();
            return false;
        }

        bool Search(int placedCount, int bytesLeft)
        {
            if (placedCount == ordered.Length)
                return true;
            if (++nodes > nodeBudget || Stopwatch.GetTimestamp() >= searchDeadline)
                return false;
            ulong stateHash = RepairDomainStateHash(domains, placed);
            if (!memo.Add((placedCount, stateHash)))
                return false;

            int selected = -1;
            int selectedCount = int.MaxValue;
            HashSet<string> seenGeometry = [];
            for (int index = 0; index < ordered.Length; index++)
            {
                if (placed[index] || !seenGeometry.Add(geometryKeys[index]))
                    continue;
                int availableCount = BitOperations.PopCount(domains[index]);
                if (availableCount == 0)
                    return false;
                if (availableCount >= selectedCount)
                    continue;
                selected = index;
                selectedCount = availableCount;
                if (availableCount == 1)
                    break;
            }

            StorageItem item = ordered[selected];
            ulong candidateBits = domains[selected];
            while (candidateBits != 0)
            {
                int candidateIndex = BitOperations.TrailingZeroCount(candidateBits);
                candidateBits &= candidateBits - 1;
                RepairCspOption option = candidates[selected][candidateIndex];
                int origin = option.Origin;
                int x = origin % RowBytes;
                int y = origin / RowBytes;
                item.X = x;
                item.Y = y;
                MarkStorage(trial, item, x, y);
                placed[selected] = true;
                List<(int Index, ulong Domain)> changed = [];
                bool forwardValid = true;
                for (int other = 0; other < ordered.Length; other++)
                {
                    if (other == selected || placed[other])
                        continue;
                    ulong prior = domains[other];
                    ulong next = prior & ~incompatibility[selected][candidateIndex][other];
                    if (geometryKeys[other] == geometryKeys[selected])
                    {
                        ulong canonical = 0;
                        for (int optionIndex = 0; optionIndex < candidates[other].Length; optionIndex++)
                        {
                            if (candidates[other][optionIndex].Origin > origin)
                                canonical |= 1UL << optionIndex;
                        }
                        next &= canonical;
                    }
                    if (next != prior)
                    {
                        changed.Add((other, prior));
                        domains[other] = next;
                    }
                    if (next == 0)
                    {
                        forwardValid = false;
                        break;
                    }
                }
                ulong selectedPrior = domains[selected];
                domains[selected] = 1UL << candidateIndex;
                if (forwardValid && Search(placedCount + 1, bytesLeft - item.ByteCount))
                    return true;
                domains[selected] = selectedPrior;
                for (int change = changed.Count - 1; change >= 0; change--)
                    domains[changed[change].Index] = changed[change].Domain;
                placed[selected] = false;
                UnmarkStorage(trial, item, x, y);
                item.X = -1;
                item.Y = -1;
            }
            return false;
        }

        if (Search(0, remainingBytes))
        {
            reserved = trial;
            return true;
        }
        reserved = baseline.ToArray();
        return false;
    }

    private static RepairCspOption[] BuildRepairSubsetCandidates(
        StorageItem item,
        IReadOnlyList<StorageItem> subset,
        bool[] baseline,
        int candidateLimit)
    {
        HashSet<int> origins = [];
        void Add(int x, int y)
        {
            if (x >= 0 && x < RowBytes && y >= 0 && y < VramRows)
                origins.Add(checked((y * RowBytes) + x));
        }
        Add(item.SourceOriginOffset % RowBytes, item.SourceOriginOffset / RowBytes);
        if (item.HasPreferred)
            Add(item.PreferredX, item.PreferredY);
        foreach (StorageItem other in subset)
        {
            Add(other.SourceOriginOffset % RowBytes, other.SourceOriginOffset / RowBytes);
            if (other.HasPreferred)
                Add(other.PreferredX, other.PreferredY);
        }

        bool containsPalette = item.Role == StorageRole.Palette ||
            item.Children.Any(child => child.Role == StorageRole.Palette);
        int xStep = containsPalette ? 32 : item.Kind == StorageKind.Pixel32 ? 16 : 8;
        int yStep = item.Kind switch
        {
            StorageKind.HqPalette => 1,
            StorageKind.LqPalette => 4,
            _ => 16
        };
        int firstX = containsPalette ? item.XModulo : 0;
        int firstY = item.Kind == StorageKind.LqPalette ? item.YModulo : 0;
        for (int y = firstY; y < VramRows; y += yStep)
        {
            for (int x = firstX; x < RowBytes; x += xStep)
                Add(x, y);
        }

        int[] valid = origins
            .Where(origin =>
            {
                int x = origin % RowBytes;
                int y = origin / RowBytes;
                return IsStoragePlacementEncodable(item, x, y) &&
                    IsStorageFree(baseline, item, x, y);
            })
            .ToArray();
        int[] contactRanked = valid
            .OrderByDescending(origin => ContactScore(baseline, item, origin % RowBytes, origin / RowBytes))
            .ThenBy(origin => origin)
            .ToArray();
        HashSet<int> selected = contactRanked.Take(Math.Min(32, candidateLimit)).ToHashSet();
        int[] spatial = valid.Order().ToArray();
        int remaining = candidateLimit - selected.Count;
        for (int index = 0; index < remaining && spatial.Length > 0; index++)
        {
            int ordinal = remaining == 1
                ? spatial.Length / 2
                : (int)Math.Round(index * (spatial.Length - 1d) / (remaining - 1d));
            selected.Add(spatial[ordinal]);
        }
        foreach (int origin in contactRanked)
        {
            if (selected.Count >= candidateLimit)
                break;
            selected.Add(origin);
        }
        return selected
            .OrderBy(origin => Array.IndexOf(contactRanked, origin))
            .Select(origin =>
            {
                int[] offsets = item.Cells.Select(cell =>
                        checked(origin + (cell.Y * RowBytes) + cell.X))
                    .ToArray();
                int x = origin % RowBytes;
                int y = origin / RowBytes;
                Bounds? rectangle = item.Cells.Count == item.Width * item.Height && x + item.Width <= RowBytes
                    ? new Bounds(x, y, item.Width, item.Height)
                    : null;
                return new RepairCspOption(origin, offsets, rectangle);
            })
            .ToArray();
    }

    private static bool RepairOptionsConflict(RepairCspOption left, RepairCspOption right)
    {
        if (left.Rectangle != null && right.Rectangle != null)
            return left.Rectangle.Intersects(right.Rectangle);
        int first = 0;
        int second = 0;
        while (first < left.Offsets.Length && second < right.Offsets.Length)
        {
            int leftOffset = left.Offsets[first];
            int rightOffset = right.Offsets[second];
            if (leftOffset == rightOffset)
                return true;
            if (leftOffset < rightOffset)
                first++;
            else
                second++;
        }
        return false;
    }

    private static string RepairGeometryKey(StorageItem item)
    {
        int cellHash = 17;
        foreach (Cell cell in item.Cells)
            cellHash = unchecked((cellHash * 31) + (cell.Y * RowBytes) + cell.X);
        return $"{item.Role}:{item.Kind}:{item.Width}:{item.Height}:{item.XModulo}:{item.YModulo}:{cellHash}";
    }

    private static ulong RepairDomainStateHash(IReadOnlyList<ulong> domains, IReadOnlyList<bool> placed)
    {
        ulong hash = 1469598103934665603UL;
        for (int index = 0; index < domains.Count; index++)
        {
            hash ^= domains[index] + (placed[index] ? 0x9E3779B97F4A7C15UL : 0UL);
            hash *= 1099511628211UL;
        }
        return hash;
    }

    private static void UnmarkStorage(bool[] reserved, StorageItem item, int x, int y)
    {
        int origin = checked((y * RowBytes) + x);
        foreach (Cell cell in item.Cells)
            reserved[checked(origin + (cell.Y * RowBytes) + cell.X)] = false;
    }

    private static bool TryPackNativeSelectiveRepairAttempt(
        IReadOnlyList<StorageItem> items,
        bool[] protectedBytes,
        int orderMode,
        int scoreMode,
        int movedRootLimit,
        out bool[] reserved,
        out int movedRootCount,
        out int movedByteCount,
        out string failureReason)
    {
        bool[] trial = protectedBytes.ToArray();
        int[] occupancy = Enumerable.Repeat(-1, protectedBytes.Length).ToArray();
        for (int offset = 0; offset < Math.Min(AddressableBytes, protectedBytes.Length); offset++)
        {
            if (protectedBytes[offset])
                occupancy[offset] = -2;
        }
        foreach (StorageItem item in items.Where(item => !item.Anchored))
        {
            item.X = -1;
            item.Y = -1;
        }

        int[] nativeIndices = Enumerable.Range(0, items.Count)
            .Where(index => !items[index].Anchored && items[index].HasPreferred)
            .OrderBy(index => items[index].Owner.Import.TargetTextureId)
            .ThenBy(index => items[index].Kind)
            .ThenBy(index => items[index].Owner.Source.Index)
            .ToArray();
        foreach (int index in nativeIndices)
        {
            StorageItem item = items[index];
            if (!IsStorageFree(trial, item, item.PreferredX, item.PreferredY) ||
                !IsStoragePlacementEncodable(item, item.PreferredX, item.PreferredY))
            {
                reserved = protectedBytes.ToArray();
                movedRootCount = 0;
                movedByteCount = 0;
                failureReason = $"native baseline could not retain T{item.Owner.Import.TargetTextureId} {item.Kind}.";
                return false;
            }
            item.X = item.PreferredX;
            item.Y = item.PreferredY;
            MarkStorage(trial, item, item.X, item.Y);
            MarkOccupancy(occupancy, item, item.X, item.Y, index);
        }

        StorageItem[] donors = items.Where(item => !item.Anchored && !item.HasPreferred)
            .OrderBy(item => orderMode == 0 ? RepairOrderRankPixelsFirst(item.Kind) : RepairOrderRankPalettesFirst(item.Kind))
            .ThenByDescending(item => item.ByteCount)
            .ThenBy(item => item.Owner.Import.TargetTextureId)
            .ThenBy(item => item.Owner.Source.Index)
            .ToArray();
        Queue<StorageItem> pending = new(donors);
        HashSet<int> movedNative = [];
        int guard = 0;
        while (pending.Count > 0)
        {
            if (++guard > items.Count + donors.Length)
            {
                reserved = protectedBytes.ToArray();
                movedRootCount = movedNative.Count;
                movedByteCount = movedNative.Sum(index => items[index].ByteCount);
                failureReason = "selective repair queue exceeded its finite root bound.";
                return false;
            }

            StorageItem incoming = pending.Dequeue();
            if (!TryFindRepairCandidate(
                    incoming,
                    items,
                    occupancy,
                    scoreMode,
                    out RepairCandidate? candidate) || candidate == null)
            {
                reserved = protectedBytes.ToArray();
                movedRootCount = movedNative.Count;
                movedByteCount = movedNative.Sum(index => items[index].ByteCount);
                failureReason = $"no descriptor-valid repair placement for {incoming.Kind}.";
                return false;
            }

            foreach (int conflictIndex in candidate.ConflictIndices)
            {
                if (!movedNative.Add(conflictIndex))
                    continue;
                StorageItem conflict = items[conflictIndex];
                ClearOccupancy(trial, occupancy, conflict, conflict.X, conflict.Y, conflictIndex);
                conflict.X = -1;
                conflict.Y = -1;
                pending.Enqueue(conflict);
            }
            movedByteCount = movedNative.Sum(index => items[index].ByteCount);
            if (movedNative.Count > movedRootLimit || movedByteCount > 131_072)
            {
                reserved = protectedBytes.ToArray();
                movedRootCount = movedNative.Count;
                failureReason = $"repair exceeded {movedRootLimit} moved roots ({movedByteCount:N0} bytes).";
                return false;
            }

            incoming.X = candidate.X;
            incoming.Y = candidate.Y;
            MarkStorage(trial, incoming, incoming.X, incoming.Y);
            MarkOccupancy(occupancy, incoming, incoming.X, incoming.Y, -3);
        }

        reserved = trial;
        movedRootCount = movedNative.Count;
        movedByteCount = movedNative.Sum(index => items[index].ByteCount);
        failureReason = "";
        return true;
    }

    private static bool TryFindRepairCandidate(
        StorageItem item,
        IReadOnlyList<StorageItem> allItems,
        int[] occupancy,
        int scoreMode,
        out RepairCandidate? best)
    {
        best = null;
        HashSet<int> origins = [];
        void AddOrigin(int x, int y)
        {
            if (x >= 0 && x < RowBytes && y >= 0 && y < VramRows)
                origins.Add(checked((y * RowBytes) + x));
        }

        AddOrigin(item.SourceOriginOffset % RowBytes, item.SourceOriginOffset / RowBytes);
        foreach (StorageItem native in allItems.Where(candidate => candidate.HasPreferred))
            AddOrigin(native.PreferredX, native.PreferredY);

        bool containsPalette = item.Role == StorageRole.Palette ||
            item.Children.Any(child => child.Role == StorageRole.Palette);
        int xStep = containsPalette ? 32 : item.Kind == StorageKind.Pixel32 ? 16 : 8;
        int yStep = item.Kind switch
        {
            StorageKind.HqPalette => 1,
            StorageKind.LqPalette => 4,
            _ => 16
        };
        int firstX = containsPalette ? item.XModulo : 0;
        int firstY = item.Kind == StorageKind.LqPalette ? item.YModulo : 0;
        for (int y = firstY; y < VramRows; y += yStep)
        {
            for (int x = firstX; x < RowBytes; x += xStep)
                AddOrigin(x, y);
        }

        foreach (int origin in origins.Order())
        {
            int x = origin % RowBytes;
            int y = origin / RowBytes;
            if (!IsStoragePlacementEncodable(item, x, y))
                continue;
            HashSet<int> conflicts = [];
            bool blocked = false;
            foreach (Cell cell in item.Cells)
            {
                int offset = checked(origin + (cell.Y * RowBytes) + cell.X);
                if (offset < 0 || offset >= Math.Min(AddressableBytes, occupancy.Length))
                {
                    blocked = true;
                    break;
                }
                int owner = occupancy[offset];
                if (owner is -2 or -3)
                {
                    blocked = true;
                    break;
                }
                if (owner >= 0)
                    conflicts.Add(owner);
            }
            if (blocked)
                continue;
            int conflictBytes = conflicts.Sum(index => allItems[index].ByteCount);
            int rigidity = conflicts.Sum(index => RepairRigidity(allItems[index].Kind));
            RepairCandidate candidate = new(x, y, conflicts.Order().ToArray(), conflictBytes, rigidity);
            if (best == null || IsBetterRepairCandidate(candidate, best, scoreMode))
                best = candidate;
        }
        return best != null;
    }

    private static bool IsBetterRepairCandidate(RepairCandidate candidate, RepairCandidate best, int scoreMode)
    {
        int comparison = scoreMode == 0
            ? candidate.ConflictBytes.CompareTo(best.ConflictBytes)
            : candidate.ConflictIndices.Count.CompareTo(best.ConflictIndices.Count);
        if (comparison != 0)
            return comparison < 0;
        comparison = candidate.Rigidity.CompareTo(best.Rigidity);
        if (comparison != 0)
            return comparison < 0;
        comparison = candidate.ConflictIndices.Count.CompareTo(best.ConflictIndices.Count);
        if (comparison != 0)
            return comparison < 0;
        comparison = candidate.ConflictBytes.CompareTo(best.ConflictBytes);
        if (comparison != 0)
            return comparison < 0;
        return candidate.Y != best.Y ? candidate.Y < best.Y : candidate.X < best.X;
    }

    private static int RepairOrderRankPixelsFirst(StorageKind kind) => kind switch
    {
        StorageKind.SharedAlias => 0,
        StorageKind.Pixel32 => 1,
        StorageKind.LqPixel => 2,
        StorageKind.Pixel16 => 3,
        StorageKind.HqPalette => 4,
        StorageKind.CombinedPalette => 5,
        _ => 6
    };

    private static int RepairOrderRankPalettesFirst(StorageKind kind) => kind switch
    {
        StorageKind.SharedAlias => 0,
        StorageKind.HqPalette => 1,
        StorageKind.CombinedPalette => 2,
        StorageKind.LqPalette => 3,
        StorageKind.Pixel32 => 4,
        StorageKind.LqPixel => 5,
        _ => 6
    };

    private static int RepairRigidity(StorageKind kind) => kind switch
    {
        StorageKind.SharedAlias => 4096,
        StorageKind.Pixel32 => 2048,
        StorageKind.HqPalette => 1024,
        StorageKind.CombinedPalette => 768,
        StorageKind.LqPixel => 256,
        StorageKind.Pixel16 => 128,
        _ => 64
    };

    private static void MarkOccupancy(int[] occupancy, StorageItem item, int x, int y, int owner)
    {
        int origin = checked((y * RowBytes) + x);
        foreach (Cell cell in item.Cells)
            occupancy[checked(origin + (cell.Y * RowBytes) + cell.X)] = owner;
    }

    private static void ClearOccupancy(
        bool[] reserved,
        int[] occupancy,
        StorageItem item,
        int x,
        int y,
        int owner)
    {
        int origin = checked((y * RowBytes) + x);
        foreach (Cell cell in item.Cells)
        {
            int offset = checked(origin + (cell.Y * RowBytes) + cell.X);
            if (occupancy[offset] != owner)
                continue;
            occupancy[offset] = -1;
            reserved[offset] = false;
        }
    }

    private static bool TryPlace(
        StorageItem item,
        bool[] reserved,
        bool reverse,
        bool honorPreferred,
        bool firstFit,
        bool[] nativePreferredPixels,
        bool[] nativePreferredPalettes)
    {
        if (honorPreferred && item.HasPreferred &&
            IsStorageFree(reserved, item, item.PreferredX, item.PreferredY) &&
            IsStoragePlacementEncodable(item, item.PreferredX, item.PreferredY))
        {
            item.X = item.PreferredX;
            item.Y = item.PreferredY;
            MarkStorage(reserved, item, item.X, item.Y);
            return true;
        }
        bool containsPalette = item.Role == StorageRole.Palette ||
            item.Children.Any(child => child.Role == StorageRole.Palette);
        int xStep = containsPalette ? 32 : 1;
        int yStep = item.Role == StorageRole.Palette && item.Kind == StorageKind.LqPalette ? 4 : 1;
        int bestX = -1;
        int bestY = -1;
        int bestContact = int.MinValue;
        bool solid = item.Role == StorageRole.Pixel && item.Cells.Count == item.Width * item.Height;
        int[]? integral = solid ? BuildIntegral(reserved) : null;
        int firstY = item.YModulo;
        int firstX = item.XModulo;
        int lastY = item.Role == StorageRole.Pixel ? VramRows - item.Height : VramRows - 1;
        int lastX = item.Role == StorageRole.Pixel ? RowBytes - item.Width : RowBytes - 1;
        // Keep the original forward/reverse candidate order and strict
        // contact-score tie behavior without allocating LINQ iterators in the
        // innermost placement scan.
        int yCount = firstY > lastY ? 0 : ((lastY - firstY) / yStep) + 1;
        int xCount = firstX > lastX ? 0 : ((lastX - firstX) / xStep) + 1;
        for (int yIndex = 0; yIndex < yCount; yIndex++)
        {
            int orderedYIndex = reverse ? yCount - 1 - yIndex : yIndex;
            int y = firstY + (orderedYIndex * yStep);
            if (item.Kind is StorageKind.LqPixel or StorageKind.Pixel16 or StorageKind.Pixel32)
            {
                if (y < 256 && y + item.Height > 256)
                    continue;
            }
            for (int xIndex = 0; xIndex < xCount; xIndex++)
            {
                int orderedXIndex = reverse ? xCount - 1 - xIndex : xIndex;
                int x = firstX + (orderedXIndex * xStep);
                if (item.Kind == StorageKind.LqPixel && (x / 128) != ((x + item.Width - 1) / 128))
                    continue;
                if (solid
                        ? RectangleCount(integral!, x, y, item.Width, item.Height) != 0
                        : !IsStorageFree(reserved, item, x, y))
                    continue;
                if (!IsStoragePlacementEncodable(item, x, y))
                    continue;
                if (firstFit)
                {
                    item.X = x;
                    item.Y = y;
                    MarkStorage(reserved, item, x, y);
                    return true;
                }
                int contact = solid
                    ? RectangleContactScore(integral!, x, y, item.Width, item.Height)
                    : ContactScore(reserved, item, x, y);
                if (honorPreferred && !item.HasPreferred)
                {
                    contact -= CountStorageOverlap(nativePreferredPalettes, item, x, y) * 4096;
                    contact -= CountStorageOverlap(nativePreferredPixels, item, x, y) * 8;
                }
                if (contact <= bestContact)
                    continue;
                bestContact = contact;
                bestX = x;
                bestY = y;
            }
        }

        if (bestX < 0)
            return false;
        item.X = bestX;
        item.Y = bestY;
        MarkStorage(reserved, item, bestX, bestY);
        return true;
    }

    private static int CountStorageOverlap(bool[] occupied, StorageItem item, int x, int y)
    {
        int count = 0;
        int targetOrigin = checked((y * RowBytes) + x);
        foreach (Cell cell in item.Cells)
        {
            int offset = checked(targetOrigin + (cell.Y * RowBytes) + cell.X);
            if (offset >= 0 && offset < occupied.Length && occupied[offset])
                count++;
        }
        return count;
    }

    private static int[] BuildIntegral(bool[] reserved)
    {
        int stride = RowBytes + 1;
        int[] result = new int[(VramRows + 1) * stride];
        for (int y = 0; y < VramRows; y++)
        {
            int running = 0;
            for (int x = 0; x < RowBytes; x++)
            {
                int sourceOffset = (y * RowBytes) + x;
                running += sourceOffset < reserved.Length && reserved[sourceOffset] ? 1 : 0;
                result[((y + 1) * stride) + x + 1] = result[(y * stride) + x + 1] + running;
            }
        }
        return result;
    }

    private static int RectangleCount(int[] integral, int x, int y, int width, int height)
    {
        int stride = RowBytes + 1;
        int right = x + width;
        int bottom = y + height;
        return integral[(bottom * stride) + right] - integral[(y * stride) + right] -
            integral[(bottom * stride) + x] + integral[(y * stride) + x];
    }

    private static int RectangleContactScore(int[] integral, int x, int y, int width, int height)
    {
        int score = 0;
        score += y == 0 ? width : RectangleCount(integral, x, y - 1, width, 1);
        score += y + height == VramRows ? width : RectangleCount(integral, x, y + height, width, 1);
        score += x == 0 ? height : RectangleCount(integral, x - 1, y, 1, height);
        score += x + width == RowBytes ? height : RectangleCount(integral, x + width, y, 1, height);
        return score;
    }

    private static int ContactScore(bool[] reserved, StorageItem item, int x, int y)
    {
        int score = 0;
        int targetOrigin = checked((y * RowBytes) + x);
        foreach (Cell cell in item.Cells)
        {
            int offset = checked(targetOrigin + (cell.Y * RowBytes) + cell.X);
            int cx = offset % RowBytes;
            int cy = offset / RowBytes;
            if (cx == 0 || reserved[offset - 1])
                score++;
            if (cx + 1 == RowBytes || reserved[offset + 1])
                score++;
            if (cy == 0 || reserved[offset - RowBytes])
                score++;
            if (cy + 1 == VramRows || reserved[offset + RowBytes])
                score++;
        }
        return score;
    }

    private static void AssignStoragePlacements(IEnumerable<StorageItem> storage)
    {
        foreach (StorageItem item in storage)
        {
            StorageItem placementOwner = item.PlacementOwner;
            if (ReferenceEquals(item, placementOwner))
                continue;
            int ownerTargetOrigin = checked((placementOwner.Y * RowBytes) + placementOwner.X);
            int targetOrigin = checked(
                ownerTargetOrigin + item.SourceOriginOffset - placementOwner.SourceOriginOffset);
            item.X = targetOrigin % RowBytes;
            item.Y = targetOrigin / RowBytes;
        }
    }

    private static void AssignChildPlacements(IEnumerable<Work> work)
    {
        foreach (Work item in work)
        {
            Work pixelOwner = item.PixelOwner;
            item.TargetPixelX = pixelOwner.PixelStorage.X + item.SourcePixelBounds.X - pixelOwner.SourcePixelBounds.X;
            item.TargetPixelY = pixelOwner.PixelStorage.Y + item.SourcePixelBounds.Y - pixelOwner.SourcePixelBounds.Y;
            int storageOrigin = checked((item.PaletteGroup.Storage.Y * RowBytes) + item.PaletteGroup.Storage.X);
            item.TargetPaletteOffset = checked(
                storageOrigin + (int)item.Source.PaletteStart - item.PaletteGroup.SourceOriginOffset);
        }
    }

    private static void CopyStorage(StorageItem item, byte[] afterPages)
    {
        Work owner = item.Owner;
        if (item.Role == StorageRole.Pixel)
        {
            Bounds source = owner.SourcePixelBounds;
            for (int row = 0; row < source.Height; row++)
            {
                Array.Copy(
                    owner.SourceAsset.TexturePages,
                    ((source.Y + row) * RowBytes) + source.X,
                    afterPages,
                    ((item.Y + row) * RowBytes) + item.X,
                    source.Width);
            }
            return;
        }

        if (item.Anchored)
            return;

        PaletteGroup group = owner.PaletteGroup;
        foreach (int sourceOffset in group.SourceOffsets)
        {
            int delta = checked(sourceOffset - group.SourceOriginOffset);
            int dx = delta % RowBytes;
            int dy = delta / RowBytes;
            afterPages[((item.Y + dy) * RowBytes) + item.X + dx] = owner.SourceAsset.TexturePages[sourceOffset];
        }
    }

    private static byte[] BuildDescriptor(Work item)
    {
        if (item.Tier == Tier.LqAlias)
            return BuildLqDescriptor(item);
        int deltaX = item.TargetPixelX - item.SourcePixelBounds.X;
        int deltaY = item.TargetPixelY - item.SourcePixelBounds.Y;
        byte[] raw = item.Source.Raw.ToArray();
        int sourceRegion = item.Source.Raw[6];
        int fullX0 = checked(GetTextureX(sourceRegion, item.Source.Raw[0]) + deltaX);
        int fullX1 = checked(GetTextureX(sourceRegion, item.Source.Raw[4]) + deltaX);
        int fullY0 = checked(GetTextureY(sourceRegion, item.Source.Raw[1]) + deltaY);
        int fullY1 = checked(GetTextureY(sourceRegion, item.Source.Raw[5]) + deltaY);
        int minimumX = Math.Min(fullX0, fullX1) - FullVramTextureByteX;
        int minimumY = Math.Min(fullY0, fullY1);
        int xPage = (FullVramTextureByteX + minimumX) / 128;
        int yPage = minimumY >= 256 ? 1 : 0;
        int localX0 = fullX0 - (xPage * 128);
        int localX1 = fullX1 - (xPage * 128);
        int localY0 = fullY0 - (yPage * 256);
        int localY1 = fullY1 - (yPage * 256);
        if (xPage is < 0 or > 15 || localX0 is < 0 or > 255 || localX1 is < 0 or > 255 ||
            localY0 is < 0 or > 255 || localY1 is < 0 or > 255)
        {
            throw new InvalidOperationException("Packed HQ descriptor is not encodable after placement.");
        }
        raw[0] = (byte)localX0;
        raw[1] = (byte)localY0;
        raw[4] = (byte)localX1;
        raw[5] = (byte)localY1;
        // Retail HQ CLUTs can begin late enough in a packed row that their
        // linear 512-byte palette crosses into the following row.  The raw
        // native CLUT word is already the authoritative encoding when an
        // anchored palette remains at its source offset; avoid rejecting that
        // unchanged descriptor through the stricter new-placement encoder.
        if (item.TargetPaletteOffset != item.Source.PaletteStart)
            BinaryPrimitives.WriteUInt16LittleEndian(raw.AsSpan(2, 2), EncodeClut(item.TargetPaletteOffset));
        byte[] material = item.TargetDescriptors[0].Raw;
        raw[6] = (byte)((material[6] & 0xE0) | xPage | (yPage << 4));
        raw[7] = (byte)((material[7] & 0x8F) | (item.Source.Raw[7] & 0x70));
        return raw;
    }

    private static byte[] BuildLqDescriptor(Work item)
    {
        int fullTexelX = item.TargetPixelX * 2;
        int xPage = fullTexelX / 256;
        int yPage = item.TargetPixelY >= 256 ? 1 : 0;
        int localX = fullTexelX - (xPage * 256);
        int localY = item.TargetPixelY - (yPage * 256);
        if (xPage is < 0 or > 7 || localX < 0 || localX + 31 > 255 || localY < 0 || localY + 32 > 256)
            throw new InvalidOperationException("Packed LQ descriptor is not encodable after placement.");

        int paletteY = item.TargetPaletteOffset / RowBytes;
        int paletteX = (FullVramTextureByteX + (item.TargetPaletteOffset % RowBytes)) / LqPaletteWidthBytes;
        if ((paletteY & 3) != 0 || paletteY + LqPaletteRows > VramRows || paletteX > 255)
            throw new InvalidOperationException("Packed LQ distance palette is not encodable after placement.");
        byte[] raw = item.Source.Raw.ToArray();
        raw[0] = (byte)localX;
        raw[1] = (byte)localY;
        raw[2] = (byte)paletteX;
        raw[3] = (byte)(paletteY / 4);
        raw[4] = (byte)(localX + 31);
        raw[5] = (byte)localY;
        byte[] material = item.TargetDescriptors[0].Raw;
        raw[6] = (byte)((material[6] & 0xE8) | xPage | (yPage << 4));
        raw[7] = material[7];
        return raw;
    }

    private static ushort EncodeClut(int offset)
    {
        int y = offset / RowBytes;
        int xByte = offset % RowBytes;
        if ((xByte & 31) != 0 || xByte + HqPaletteBytes > RowBytes || y is < 0 or >= VramRows)
            throw new InvalidOperationException("Packed HQ palette is not CLUT-encodable.");
        int clutX = 512 + (xByte / 2);
        return checked((ushort)((y << 6) | ((clutX / 16) & 0x3F)));
    }

    private static bool VerifyLogicalReadback(
        IReadOnlyList<Work> work,
        byte[] afterPages,
        byte[] afterModel,
        int textureCount,
        out string failureReason)
    {
        TextureIndex after = Decode(afterModel);
        if (after.TextureCount != textureCount)
        {
            failureReason = "The repacked descriptor table no longer has the source texture count.";
            return false;
        }
        foreach (Work item in work)
        {
            TextureRecord record = after.Records[item.Import.TargetTextureId];
            IReadOnlyList<Descriptor> targets = item.Tier switch
            {
                Tier.LqAlias => [record.Low[0], record.Low[1], record.Leading],
                Tier.Normal => [record.Normal[item.Source.Index]],
                _ => [record.Close[item.Source.Index]]
            };
            foreach (Descriptor target in targets)
            {
                if (!VerifyDescriptor(item.SourceAsset.TexturePages, item.Source, afterPages, target))
                {
                    failureReason = $"Exact logical readback failed for target T{item.Import.TargetTextureId} {item.Tier}[{item.Source.Index}].";
                    return false;
                }
            }
            if (item.Tier == Tier.LqAlias &&
                (!targets[0].Raw.SequenceEqual(targets[1].Raw) || !targets[0].Raw.SequenceEqual(targets[2].Raw)))
            {
                failureReason = $"Target T{item.Import.TargetTextureId} lost its byte-identical LQ/leading alias.";
                return false;
            }
        }
        failureReason = "";
        return true;
    }

    private static bool VerifyDescriptor(byte[] sourcePages, Descriptor source, byte[] targetPages, Descriptor target)
    {
        if (source.Format != target.Format || source.Side != target.Side)
            return false;
        int[] sourcePalette = EnumeratePaletteOffsets(source).ToArray();
        int[] targetPalette = EnumeratePaletteOffsets(target).ToArray();
        if (sourcePalette.Length != targetPalette.Length)
            return false;
        for (int index = 0; index < sourcePalette.Length; index++)
        {
            if (sourcePages[sourcePalette[index]] != targetPages[targetPalette[index]])
                return false;
        }
        for (int y = 0; y < source.Side; y++)
        {
            for (int x = 0; x < source.Side; x++)
            {
                if (ReadIndex(sourcePages, source, x, y) != ReadIndex(targetPages, target, x, y))
                    return false;
            }
        }
        return true;
    }

    private static bool VerifyAliasRelationships(
        IReadOnlyList<Work> work,
        out bool pixelPreserved,
        out bool palettePreserved,
        out string failureReason)
    {
        pixelPreserved = true;
        palettePreserved = true;
        failureReason = "";
        Dictionary<(string StorageKey, int SourceOffset), int> paletteForward = [];
        Dictionary<int, (string StorageKey, int SourceOffset)> paletteReverse = [];
        foreach (Work item in work)
        {
            int[] sourcePalette = EnumeratePaletteOffsets(item.Source).ToArray();
            int[] targetPalette = EnumeratePaletteOffsets(item.Source.Format, item.TargetPaletteOffset).ToArray();
            if (sourcePalette.Length != targetPalette.Length)
            {
                palettePreserved = false;
                failureReason = $"Palette length changed for T{item.Import.TargetTextureId} {item.Tier}[{item.Source.Index}].";
                continue;
            }
            for (int ordinal = 0; ordinal < sourcePalette.Length; ordinal++)
            {
                (string StorageKey, int SourceOffset) sourceKey = (item.SourceAsset.StorageKey, sourcePalette[ordinal]);
                int targetOffset = targetPalette[ordinal];
                if (paletteForward.TryGetValue(sourceKey, out int priorTarget) && priorTarget != targetOffset)
                {
                    palettePreserved = false;
                    failureReason = $"One source palette byte {sourceKey} maps to both {priorTarget} and {targetOffset}.";
                }
                else
                    paletteForward[sourceKey] = targetOffset;
                if (paletteReverse.TryGetValue(targetOffset, out (string StorageKey, int SourceOffset) priorSource) &&
                    priorSource != sourceKey)
                {
                    palettePreserved = false;
                    failureReason = $"Target palette byte {targetOffset} merges source bytes {priorSource} and {sourceKey}.";
                }
                else
                {
                    paletteReverse[targetOffset] = sourceKey;
                }
            }
        }
        for (int first = 0; first < work.Count; first++)
        {
            for (int second = first + 1; second < work.Count; second++)
            {
                Work left = work[first];
                Work right = work[second];
                if (left.SourceAsset.StorageKey != right.SourceAsset.StorageKey)
                    continue;
                bool sourcePixelAlias = left.SourcePixelBounds.Intersects(right.SourcePixelBounds);
                Bounds leftTarget = new(left.TargetPixelX, left.TargetPixelY, left.SourcePixelBounds.Width, left.SourcePixelBounds.Height);
                Bounds rightTarget = new(right.TargetPixelX, right.TargetPixelY, right.SourcePixelBounds.Width, right.SourcePixelBounds.Height);
                bool targetPixelAlias = leftTarget.Intersects(rightTarget);
                if (sourcePixelAlias != targetPixelAlias)
                {
                    pixelPreserved = false;
                    failureReason = $"Pixel alias changed between T{left.Import.TargetTextureId} {left.Tier}[{left.Source.Index}] and " +
                        $"T{right.Import.TargetTextureId} {right.Tier}[{right.Source.Index}].";
                }
            }
        }
        return pixelPreserved && palettePreserved;
    }

    private static bool VerifyTargetMaterialBits(IReadOnlyList<Work> work, byte[] afterModel, int textureCount)
    {
        TextureIndex after = Decode(afterModel);
        foreach (Work item in work)
        {
            TextureRecord record = after.Records[item.Import.TargetTextureId];
            IReadOnlyList<Descriptor> targets = item.Tier switch
            {
                Tier.LqAlias => [record.Low[0], record.Low[1], record.Leading],
                Tier.Normal => [record.Normal[item.Source.Index]],
                _ => [record.Close[item.Source.Index]]
            };
            for (int index = 0; index < targets.Count; index++)
            {
                byte[] before = item.TargetDescriptors[index].Raw;
                byte[] current = targets[index].Raw;
                byte regionMask = item.Source.Format == Format.Lq4 ? (byte)0xE8 : (byte)0xE0;
                byte alphaMask = item.Source.Format == Format.Lq4 ? (byte)0xFF : (byte)0x8F;
                if ((before[6] & regionMask) != (current[6] & regionMask) ||
                    (before[7] & alphaMask) != (current[7] & alphaMask))
                    return false;
            }
        }
        return after.TextureCount == textureCount;
    }

    private static bool VerifyFixedDescriptors(TextureAsset target, byte[] afterModel, IReadOnlySet<int> movableIds)
    {
        TextureIndex after = Decode(afterModel);
        foreach (TextureRecord before in target.Index.Records.Where(record => !movableIds.Contains(record.TextureId)))
        {
            TextureRecord current = after.Records[before.TextureId];
            if (!EnumerateDescriptors(before).SelectMany(item => item.Raw)
                    .SequenceEqual(EnumerateDescriptors(current).SelectMany(item => item.Raw)))
                return false;
        }
        return true;
    }

    private static IReadOnlyList<Descriptor> EnumerateDescriptors(TextureRecord record) =>
        record.Low.Concat([record.Leading]).Concat(record.Normal).Concat(record.Close).ToArray();

    private static bool VerifyProtectedBytes(byte[] before, byte[] after, bool[] protectedBytes)
    {
        for (int index = 0; index < Math.Min(before.Length, protectedBytes.Length); index++)
        {
            if (protectedBytes[index] && before[index] != after[index])
                return false;
        }
        return true;
    }

    private static bool[] BuildProtectedMask(int length, NativeTexturePageExternalOwnershipProof proof)
    {
        bool[] result = new bool[length];
        foreach (NativeTexturePageOwnedRange range in proof.OwnedRanges)
        {
            if (range.Offset < 0 || range.Length <= 0 || range.Offset + range.Length > length)
                throw new InvalidDataException($"Ownership range '{range.Owner}' is outside the target page subfile.");
            for (long offset = range.Offset; offset < range.Offset + range.Length; offset++)
                result[(int)offset] = true;
        }
        for (int offset = Math.Min(AddressableBytes, length); offset < length; offset++)
            result[offset] = true;
        return result;
    }

    private static bool ValidateRecord(TextureRecord record, int wadEntry, int textureId, out string failureReason)
    {
        if (record.Low.Count != 2 || record.Normal.Count != 4 || record.Close.Count != 16 ||
            !record.Low[0].Raw.SequenceEqual(record.Low[1].Raw) ||
            !record.Low[0].Raw.SequenceEqual(record.Leading.Raw) ||
            record.Low.Any(item => item.Format != Format.Lq4 || item.Side != 32) ||
            record.Leading.Format != Format.Lq4 || record.Leading.Side != 32 ||
            record.Normal.Any(item => item.Format != Format.Hq8 || item.Side != 32) ||
            record.Close.Any(item => item.Format != Format.Hq8 || item.Side is not (16 or 32)))
        {
            failureReason = $"WAD {wadEntry} T{textureId} is outside the proven complete native terrain texture grammar.";
            return false;
        }
        foreach (Descriptor descriptor in EnumerateDescriptors(record))
        {
            if (!IsSourcePaletteEncodable(descriptor))
            {
                int x = checked((int)(descriptor.PaletteStart % RowBytes));
                int y = checked((int)(descriptor.PaletteStart / RowBytes));
                failureReason = $"WAD {wadEntry} T{textureId} has a non-encodable {descriptor.Format} palette start ({x},{y}).";
                return false;
            }
        }
        failureReason = "";
        return true;
    }

    private static bool IsSourcePaletteEncodable(Descriptor descriptor)
    {
        if (descriptor.PaletteStart < 0 || descriptor.PaletteStart >= AddressableBytes)
            return false;
        int x = checked((int)(descriptor.PaletteStart % RowBytes));
        int y = checked((int)(descriptor.PaletteStart / RowBytes));
        return descriptor.Format == Format.Hq8
            // Retail HQ CLUT starts may be anywhere on the aligned right-half
            // row (for example Magic Crafters x=672 and Alpine Ridge x=992).
            // The native lookup and the rest of this editor treat the 256-word
            // palette as one linear 512-byte span, so a source span may cross a
            // packed-row boundary. New targets remain packed at x<=512 by
            // EncodeClut/TryPlace, while source readback preserves the retail
            // byte sequence exactly.
            ? (x & 31) == 0 && x <= 992 && y is >= 0 and < VramRows &&
              descriptor.PaletteStart + HqPaletteBytes <= AddressableBytes
            : (x & 31) == 0 && x <= 992 && (y & 3) == 0 && y is >= 0 and <= 496;
    }

    private static bool IsCompleteTier(string value) =>
        string.Equals(value, "both", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "all", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "complete", StringComparison.OrdinalIgnoreCase);

    private static bool CanRead(Descriptor descriptor, int pageLength)
    {
        foreach (int offset in EnumeratePaletteOffsets(descriptor))
        {
            if (offset < 0 || offset >= pageLength)
                return false;
        }
        for (int y = 0; y < descriptor.Side; y++)
        {
            for (int x = 0; x < descriptor.Side; x++)
            {
                if (!TrySampleAddress(descriptor, x, y, pageLength, out _, out _))
                    return false;
            }
        }
        return true;
    }

    private static Bounds GetPixelBounds(Descriptor descriptor)
    {
        int minX = int.MaxValue;
        int minY = int.MaxValue;
        int maxX = int.MinValue;
        int maxY = int.MinValue;
        for (int y = 0; y < descriptor.Side; y++)
        {
            for (int x = 0; x < descriptor.Side; x++)
            {
                if (!TrySampleAddress(descriptor, x, y, AddressableBytes, out int offset, out _))
                    throw new InvalidDataException("Descriptor pixel is outside the addressable terrain page.");
                int px = offset % RowBytes;
                int py = offset / RowBytes;
                minX = Math.Min(minX, px);
                maxX = Math.Max(maxX, px);
                minY = Math.Min(minY, py);
                maxY = Math.Max(maxY, py);
            }
        }
        return new Bounds(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    private static bool TrySampleAddress(Descriptor descriptor, int x, int y, int length, out int offset, out int nibble)
    {
        if (descriptor.Format == Format.Lq4)
        {
            int sx = descriptor.PackedX + (x / 2);
            int sy = descriptor.Y + y;
            offset = (sy * RowBytes) + sx;
            nibble = x & 1;
            return sx >= 0 && sx < RowBytes && sy >= 0 && sy < VramRows && offset >= 0 && offset < length;
        }
        int[] matrix = Matrices[Math.Clamp(descriptor.Orientation, 0, 7)];
        int edge = descriptor.Side - 1;
        int startY = descriptor.Y + ((matrix[2] < 0 || matrix[3] < 0) ? edge : 0);
        int startX = descriptor.PackedX + ((matrix[0] < 0 || matrix[1] < 0) ? edge : 0);
        int sxHq = startX + (x * matrix[0]) + (y * matrix[1]);
        int syHq = startY + (x * matrix[2]) + (y * matrix[3]);
        offset = (syHq * RowBytes) + sxHq;
        nibble = 0;
        return sxHq >= 0 && sxHq < RowBytes && syHq >= 0 && syHq < VramRows && offset >= 0 && offset < length;
    }

    private static byte ReadIndex(byte[] pages, Descriptor descriptor, int x, int y)
    {
        if (!TrySampleAddress(descriptor, x, y, pages.Length, out int offset, out int nibble))
            throw new InvalidDataException("Descriptor sample is outside its page subfile.");
        byte value = pages[offset];
        return descriptor.Format == Format.Lq4 ? (byte)((value >> (nibble * 4)) & 0x0F) : value;
    }

    private static IEnumerable<int> EnumeratePaletteOffsets(Descriptor descriptor)
        => EnumeratePaletteOffsets(descriptor.Format, descriptor.PaletteStart);

    private static IEnumerable<int> EnumeratePaletteOffsets(Format format, long paletteStart)
    {
        if (format == Format.Lq4)
        {
            for (int row = 0; row < LqPaletteRows; row++)
            {
                int start = checked((int)paletteStart + (row * RowBytes));
                for (int index = 0; index < LqPaletteWidthBytes; index++)
                    yield return start + index;
            }
            yield break;
        }
        for (int index = 0; index < HqPaletteBytes; index++)
            yield return checked((int)paletteStart + index);
    }

    private static bool IsRectangleFree(bool[] reserved, int x, int y, int width, int height)
    {
        for (int row = 0; row < height; row++)
        {
            int offset = ((y + row) * RowBytes) + x;
            if (offset < 0 || offset + width > Math.Min(AddressableBytes, reserved.Length))
                return false;
            for (int index = offset; index < offset + width; index++)
            {
                if (reserved[index])
                    return false;
            }
        }
        return true;
    }

    private static bool IsStorageFree(bool[] reserved, StorageItem item, int x, int y)
    {
        if (x < 0 || y < 0 || x >= RowBytes || y >= VramRows ||
            (item.Role == StorageRole.Pixel && (x + item.Width > RowBytes || y + item.Height > VramRows)))
            return false;
        int targetOrigin = checked((y * RowBytes) + x);
        foreach (Cell cell in item.Cells)
        {
            int offset = checked(targetOrigin + (cell.Y * RowBytes) + cell.X);
            if (offset < 0 || offset >= Math.Min(AddressableBytes, reserved.Length) || reserved[offset])
                return false;
        }
        return true;
    }

    private static bool IsStoragePlacementEncodable(StorageItem item, int x, int y)
    {
        if (item.Role == StorageRole.Pixel)
            return IsPixelPlacementEncodable(item, x, y);
        if (item.Role == StorageRole.Palette)
            return IsPalettePlacementEncodable(item, x, y);

        int targetOrigin = checked((y * RowBytes) + x);
        foreach (StorageItem child in item.Children)
        {
            int childTargetOrigin = checked(
                targetOrigin + child.SourceOriginOffset - item.SourceOriginOffset);
            if (childTargetOrigin < 0 || childTargetOrigin >= AddressableBytes)
                return false;
            int childX = childTargetOrigin % RowBytes;
            int childY = childTargetOrigin / RowBytes;
            if (child.Role == StorageRole.Pixel
                    ? !IsPixelPlacementEncodable(child, childX, childY)
                    : !IsPalettePlacementEncodable(child, childX, childY))
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsPixelPlacementEncodable(StorageItem item, int x, int y)
    {
        if (x < 0 || y < 0 || x + item.Width > RowBytes || y + item.Height > VramRows)
            return false;
        if (y < 256 && y + item.Height > 256)
            return false;
        return item.Kind != StorageKind.LqPixel ||
            (x / 128) == ((x + item.Width - 1) / 128);
    }

    private static bool IsPalettePlacementEncodable(StorageItem item, int x, int y)
    {
        PaletteGroup group = item.Owner.PaletteGroup;
        int targetOrigin = checked((y * RowBytes) + x);
        foreach (Work member in group.Members)
        {
            int targetStart = checked(
                targetOrigin + (int)member.Source.PaletteStart - group.SourceOriginOffset);
            int targetX = targetStart % RowBytes;
            int targetY = targetStart / RowBytes;
            if ((targetX & 31) != 0)
                return false;
            if (member.Source.Format == Format.Hq8)
            {
                // New HQ CLUTs must be entirely contained by the encoded packed
                // row even when their retail source was a legal linear span
                // crossing the 1024-byte storage-row boundary.
                if (targetX + HqPaletteBytes > RowBytes || targetY is < 0 or >= VramRows ||
                    targetStart + HqPaletteBytes > AddressableBytes)
                {
                    return false;
                }
            }
            else if (targetX + LqPaletteWidthBytes > RowBytes ||
                     (targetY & 3) != 0 || targetY is < 0 or > VramRows - LqPaletteRows)
            {
                return false;
            }
        }
        return true;
    }

    private static void MarkStorage(bool[] reserved, StorageItem item, int x, int y)
    {
        int targetOrigin = checked((y * RowBytes) + x);
        foreach (Cell cell in item.Cells)
            reserved[checked(targetOrigin + (cell.Y * RowBytes) + cell.X)] = true;
    }

    private static bool PalettesIntersect(Descriptor left, Descriptor right)
        => PaletteRegionsIntersect(left.Format, left.PaletteStart, right.Format, right.PaletteStart);

    private static bool PaletteRegionsIntersect(Format leftFormat, long leftStart, Format rightFormat, long rightStart)
    {
        if (leftFormat == Format.Hq8 && rightFormat == Format.Hq8)
        {
            long leftEnd = leftStart + HqPaletteBytes;
            long rightEnd = rightStart + HqPaletteBytes;
            return leftStart < rightEnd && rightStart < leftEnd;
        }
        if (leftFormat != rightFormat)
        {
            long hqStart = leftFormat == Format.Hq8 ? leftStart : rightStart;
            long lqBase = leftFormat == Format.Lq4 ? leftStart : rightStart;
            long hqEnd = hqStart + HqPaletteBytes;
            for (int row = 0; row < LqPaletteRows; row++)
            {
                long lqStart = lqBase + (row * RowBytes);
                long lqEnd = lqStart + LqPaletteWidthBytes;
                if (hqStart < lqEnd && lqStart < hqEnd)
                    return true;
            }
            return false;
        }
        int leftX = checked((int)leftStart % RowBytes);
        int leftY = checked((int)leftStart / RowBytes);
        int rightX = checked((int)rightStart % RowBytes);
        int rightY = checked((int)rightStart / RowBytes);
        return leftX < rightX + LqPaletteWidthBytes && rightX < leftX + LqPaletteWidthBytes &&
            leftY < rightY + LqPaletteRows && rightY < leftY + LqPaletteRows;
    }

    private static int Find(int[] parents, int value)
    {
        while (parents[value] != value)
        {
            parents[value] = parents[parents[value]];
            value = parents[value];
        }
        return value;
    }

    private static void Union(int[] parents, int first, int second)
    {
        int left = Find(parents, first);
        int right = Find(parents, second);
        if (left != right)
            parents[right] = left;
    }

    private static void MarkRectangle(bool[] reserved, int x, int y, int width, int height)
    {
        for (int row = 0; row < height; row++)
        {
            int offset = ((y + row) * RowBytes) + x;
            for (int index = offset; index < offset + width; index++)
                reserved[index] = true;
        }
    }

    private static IReadOnlyList<NativeTerrainTextureRelocationPatch> BuildDiffPatches(
        byte[] before,
        byte[] after,
        long baseWadOffset,
        string kind,
        string description)
    {
        List<NativeTerrainTextureRelocationPatch> result = [];
        int cursor = 0;
        while (cursor < before.Length)
        {
            if (before[cursor] == after[cursor])
            {
                cursor++;
                continue;
            }
            int start = cursor;
            while (cursor < before.Length && before[cursor] != after[cursor])
                cursor++;
            int length = cursor - start;
            result.Add(new NativeTerrainTextureRelocationPatch(
                baseWadOffset + start,
                length,
                before.AsSpan(start, length).ToArray(),
                after.AsSpan(start, length).ToArray(),
                kind,
                description));
        }
        return result;
    }

    private static NativeTerrainTextureGlobalRepackPackedRecord ExtractPackedRecord(
        byte[] model,
        int textureCount,
        NativeTerrainTextureRelocationImport import)
    {
        int lowOffset = checked(8 + (import.TargetTextureId * LqRecordBytes));
        int highOffset = checked(8 + (textureCount * LqRecordBytes) + (import.TargetTextureId * HqRecordBytes));
        byte[] low = model.AsSpan(lowOffset, LqRecordBytes).ToArray();
        byte[] high = model.AsSpan(highOffset, HqRecordBytes).ToArray();
        return new NativeTerrainTextureGlobalRepackPackedRecord(
            import.TargetTextureId,
            import.DonorWadEntry,
            import.DonorTextureId,
            low,
            high,
            Convert.ToHexString(SHA256.HashData(low)),
            Convert.ToHexString(SHA256.HashData(high)));
    }

    private static TextureAsset ExpandWithSyntheticRecords(
        TextureAsset retail,
        IReadOnlyList<NativeTerrainTextureGlobalRepackSyntheticRecord> syntheticRecords)
    {
        int sourceCount = retail.Index.TextureCount;
        int outputCount = checked(sourceCount + syntheticRecords.Count);
        if (outputCount > 128)
            throw new InvalidDataException($"Expanded terrain texture count {outputCount} exceeds the native 128-record limit.");

        int sourceTextureLength = checked(8 + (sourceCount * (LqRecordBytes + HqRecordBytes)));
        int outputTextureLength = checked(8 + (outputCount * (LqRecordBytes + HqRecordBytes)));
        int sourceHighStart = checked(8 + (sourceCount * LqRecordBytes));
        int outputHighStart = checked(8 + (outputCount * LqRecordBytes));
        int growth = checked(syntheticRecords.Count * (LqRecordBytes + HqRecordBytes));
        byte[] expanded = new byte[checked(retail.Model.Length + growth)];
        BinaryPrimitives.WriteInt32LittleEndian(expanded.AsSpan(0, 4), outputTextureLength);
        BinaryPrimitives.WriteInt32LittleEndian(expanded.AsSpan(4, 4), outputCount);

        retail.Model.AsSpan(8, sourceCount * LqRecordBytes).CopyTo(expanded.AsSpan(8));
        retail.Model.AsSpan(sourceHighStart, sourceCount * HqRecordBytes)
            .CopyTo(expanded.AsSpan(outputHighStart));
        for (int index = 0; index < syntheticRecords.Count; index++)
        {
            NativeTerrainTextureGlobalRepackSyntheticRecord item = syntheticRecords[index];
            int templateLow = checked(8 + (item.MaterialTemplateTextureId * LqRecordBytes));
            int templateHigh = checked(sourceHighStart + (item.MaterialTemplateTextureId * HqRecordBytes));
            int outputLow = checked(8 + ((sourceCount + index) * LqRecordBytes));
            int outputHigh = checked(outputHighStart + ((sourceCount + index) * HqRecordBytes));
            retail.Model.AsSpan(templateLow, LqRecordBytes).CopyTo(expanded.AsSpan(outputLow));
            retail.Model.AsSpan(templateHigh, HqRecordBytes).CopyTo(expanded.AsSpan(outputHigh));
        }
        retail.Model.AsSpan(sourceTextureLength)
            .CopyTo(expanded.AsSpan(outputTextureLength));

        return retail with
        {
            Model = expanded,
            Index = Decode(expanded)
        };
    }

    private static TextureAsset LoadAsset(
        string sourceImagePath,
        FileStream image,
        DiscLayout layout,
        int wadEntry,
        bool initializeRuntimeState)
    {
        Subfile pagesInfo = GetSubfile(image, layout, wadEntry, TexturePagesSubfileIndex);
        Subfile modelInfo = GetSubfile(image, layout, wadEntry, ModelSubfileIndex);
        byte[] pages = DiscImage.ReadFileBytes(image, layout, WadLba, pagesInfo.WadOffset, checked((int)pagesInfo.Length));
        byte[] model = DiscImage.ReadFileBytes(image, layout, WadLba, modelInfo.WadOffset, checked((int)modelInfo.Length));
        TextureIndex index;
        if (initializeRuntimeState)
        {
            NativeTerrainTextureRuntimeControlAudit runtime = NativeTerrainTextureRuntimeControlScanner.Inspect(sourceImagePath, wadEntry);
            NativeTerrainTextureInitialStateResult initialized =
                NativeTerrainTextureRuntimeControlScanner.InitializeTextureRecords(runtime, model);
            if (!runtime.Complete || !initialized.Complete)
                throw new InvalidDataException(runtime.SafetyBlockers.FirstOrDefault() ?? initialized.SafetyBlockers.FirstOrDefault() ?? "Texture runtime initialization failed.");
            index = Decode(initialized.InitializedTextureData);
        }
        else
        {
            index = Decode(model);
        }
        return new TextureAsset(
            $"{Path.GetFullPath(sourceImagePath)}|W{wadEntry}",
            wadEntry,
            pagesInfo.WadOffset,
            modelInfo.WadOffset,
            pages,
            model,
            Convert.ToHexString(SHA256.HashData(pages)),
            index);
    }

    private static Subfile GetSubfile(FileStream image, DiscLayout layout, int wadEntry, int subfileIndex)
    {
        byte[] wadHeader = DiscImage.ReadFileBytes(image, layout, WadLba, 0, 4096);
        ArchiveEntry wad = ParseHeader(wadHeader, 200_000_000).Single(item => item.Index == wadEntry);
        byte[] assetHeader = DiscImage.ReadFileBytes(image, layout, WadLba, wad.Offset, 4096);
        ArchiveEntry subfile = ParseHeader(assetHeader, wad.Length).Single(item => item.Index == subfileIndex);
        return new Subfile(wad.Offset + subfile.Offset, subfile.Length);
    }

    private static IReadOnlyList<ArchiveEntry> ParseHeader(byte[] bytes, long archiveLength)
    {
        List<ArchiveEntry> result = [];
        long first = ReadUInt32(bytes, 0);
        if (first <= 0 || first > bytes.Length)
            first = bytes.Length;
        for (int offset = 0; offset <= Math.Min(bytes.Length, first) - 8; offset += 8)
        {
            long start = ReadUInt32(bytes, offset);
            long length = ReadUInt32(bytes, offset + 4);
            if (start > 0 && length > 0 && start + length <= archiveLength)
                result.Add(new ArchiveEntry(offset / 8, start, length));
        }
        return result;
    }

    private static TextureIndex Decode(byte[] model)
    {
        if (model.Length < 8)
            throw new InvalidDataException("Texture model is shorter than its native header.");
        int length = checked((int)ReadUInt32(model, 0));
        int count = checked((int)ReadUInt32(model, 4));
        int expected = checked(8 + (count * (LqRecordBytes + HqRecordBytes)));
        if (count is < 1 or > 128 || length != expected || length > model.Length)
            throw new InvalidDataException($"Invalid native terrain texture table {length}/{count}.");
        int highStart = checked(8 + (count * LqRecordBytes));
        List<TextureRecord> records = [];
        for (int textureId = 0; textureId < count; textureId++)
        {
            int lowOffset = checked(8 + (textureId * LqRecordBytes));
            int highOffset = checked(highStart + (textureId * HqRecordBytes));
            Descriptor[] low = [DecodeLq(model, lowOffset, 0), DecodeLq(model, lowOffset + 8, 1)];
            Descriptor leading = DecodeLq(model, highOffset, 0);
            Descriptor[] normal = Enumerable.Range(0, 4).Select(index => DecodeHq(model, highOffset + 8 + (index * 8), index)).ToArray();
            Descriptor[] close = Enumerable.Range(0, 16).Select(index => DecodeHq(model, highOffset + 40 + (index * 8), index)).ToArray();
            records.Add(new TextureRecord(textureId, low, leading, normal, close));
        }
        return new TextureIndex(count, records);
    }

    private static Descriptor DecodeLq(byte[] model, int offset, int index)
    {
        byte[] raw = model.AsSpan(offset, 8).ToArray();
        int region = raw[6];
        return new Descriptor(
            index,
            offset,
            raw,
            Format.Lq4,
            LqTileSide,
            checked((raw[3] * 4L * RowBytes) + (raw[2] * (long)LqPaletteWidthBytes) - FullVramTextureByteX),
            0,
            ((((region * 256) % 2048) + raw[0]) / 2),
            GetTextureY(region, raw[1]));
    }

    private static Descriptor DecodeHq(byte[] model, int offset, int index)
    {
        byte[] raw = model.AsSpan(offset, 8).ToArray();
        int region = raw[6];
        int side = Math.Max(Math.Abs(raw[4] - raw[0]), Math.Abs(raw[5] - raw[1])) + 1;
        return new Descriptor(
            index,
            offset,
            raw,
            Format.Hq8,
            side,
            DecodeClut(BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(2, 2))),
            (raw[7] >> 4) & 7,
            GetTextureX(region, raw[0]) - FullVramTextureByteX,
            GetTextureY(region, raw[1]));
    }

    private static int GetTextureX(int region, int value) => ((region * 128) % 2048) + value;
    private static int GetTextureY(int region, int value) => ((region & 0x10) != 0 ? 256 : 0) + value;
    private static long DecodeClut(int code)
    {
        int x = (code & 0x3F) * 16;
        int y = (code >> 6) & 0x1FF;
        return checked((y * (long)RowBytes) + ((x - 512) * 2L));
    }
    private static uint ReadUInt32(byte[] bytes, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
    private static string Sha256File(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } &&
        value.All(character =>
            character is >= '0' and <= '9' or
                >= 'A' and <= 'F' or
                >= 'a' and <= 'f');

    private sealed record ArchiveEntry(int Index, long Offset, long Length);
    private sealed record Subfile(long WadOffset, long Length);
    private sealed record TextureAsset(
        string StorageKey,
        int WadEntry,
        long TexturePagesWadOffset,
        long ModelWadOffset,
        byte[] TexturePages,
        byte[] Model,
        string TexturePagesSha256,
        TextureIndex Index);
    private sealed record TextureIndex(int TextureCount, IReadOnlyList<TextureRecord> Records);
    private sealed record TextureRecord(
        int TextureId,
        IReadOnlyList<Descriptor> Low,
        Descriptor Leading,
        IReadOnlyList<Descriptor> Normal,
        IReadOnlyList<Descriptor> Close);
    private sealed record Descriptor(
        int Index,
        int ModelOffset,
        byte[] Raw,
        Format Format,
        int Side,
        long PaletteStart,
        int Orientation,
        int PackedX,
        int Y);
    private sealed record Bounds(int X, int Y, int Width, int Height)
    {
        public int ByteCount => checked(Width * Height);
        public bool Contains(Bounds other) => other.X >= X && other.Y >= Y &&
            other.X + other.Width <= X + Width && other.Y + other.Height <= Y + Height;
        public bool Intersects(Bounds other) => X < other.X + other.Width && other.X < X + Width &&
            Y < other.Y + other.Height && other.Y < Y + Height;
    }
    private sealed class Work
    {
        public Work(NativeTerrainTextureRelocationImport import, TextureAsset sourceAsset, Descriptor source,
            IReadOnlyList<Descriptor> targets, Tier tier, Bounds sourcePixelBounds)
        {
            Import = import;
            SourceAsset = sourceAsset;
            Source = source;
            TargetDescriptors = targets;
            Tier = tier;
            SourcePixelBounds = sourcePixelBounds;
        }
        public NativeTerrainTextureRelocationImport Import { get; }
        public TextureAsset SourceAsset { get; }
        public Descriptor Source { get; }
        public IReadOnlyList<Descriptor> TargetDescriptors { get; }
        public Tier Tier { get; }
        public Bounds SourcePixelBounds { get; }
        public int PaletteByteCount => Tier == Tier.LqAlias ? LqPaletteWidthBytes * LqPaletteRows : HqPaletteBytes;
        public Work PixelOwner { get; set; } = null!;
        public PaletteGroup PaletteGroup { get; set; } = null!;
        public StorageItem PixelStorage { get; set; } = null!;
        public StorageItem PaletteStorage { get; set; } = null!;
        public int TargetPixelX { get; set; }
        public int TargetPixelY { get; set; }
        public int TargetPaletteOffset { get; set; }
    }
    private sealed class StorageItem
    {
        public StorageItem(
            Work owner,
            StorageRole role,
            StorageKind kind,
            int width,
            int height,
            int sourceOriginOffset,
            IReadOnlyList<int> sourceOffsets,
            IReadOnlyList<Cell>? cells = null,
            int xModulo = 0,
            int yModulo = 0,
            IReadOnlyList<StorageItem>? children = null)
        {
            Owner = owner;
            Role = role;
            Kind = kind;
            Width = width;
            Height = height;
            SourceOriginOffset = sourceOriginOffset;
            SourceOffsets = sourceOffsets;
            Cells = cells ?? Enumerable.Range(0, height)
                .SelectMany(y => Enumerable.Range(0, width).Select(x => new Cell(x, y)))
                .ToArray();
            XModulo = xModulo;
            YModulo = yModulo;
            Children = children ?? [];
            PlacementOwner = this;
        }
        public Work Owner { get; }
        public StorageRole Role { get; }
        public StorageKind Kind { get; }
        public int Width { get; }
        public int Height { get; }
        public int SourceOriginOffset { get; }
        public IReadOnlyList<int> SourceOffsets { get; }
        public IReadOnlyList<Cell> Cells { get; }
        public int XModulo { get; }
        public int YModulo { get; }
        public IReadOnlyList<StorageItem> Children { get; }
        public StorageItem PlacementOwner { get; set; }
        public int ByteCount => Cells.Count;
        public int X { get; set; } = -1;
        public int Y { get; set; } = -1;
        public int PreferredX { get; set; } = -1;
        public int PreferredY { get; set; } = -1;
        public bool HasPreferred => PreferredX >= 0 && PreferredY >= 0;
        public bool Anchored { get; set; }
    }
    private sealed record PaletteGroup(
        TextureAsset SourceAsset,
        bool HasLq,
        bool HasHq,
        IReadOnlyList<Work> Members,
        IReadOnlyList<int> SourceOffsets,
        int SourceOriginOffset,
        int Width,
        int Height)
    {
        public int SourceOriginX => SourceOriginOffset % RowBytes;
        public int SourceOriginY => SourceOriginOffset / RowBytes;
        public StorageItem Storage { get; set; } = null!;
    }
    private sealed record Cell(int X, int Y);
    private sealed record RepairCandidate(
        int X,
        int Y,
        IReadOnlyList<int> ConflictIndices,
        int ConflictBytes,
        int Rigidity);
    private sealed record RepairWindow(
        int X,
        int Y,
        int Width,
        int Height,
        IReadOnlyList<int> ConflictIndices,
        int ProtectedBytes,
        int MovedBytes);
    private sealed record RepairCspOption(
        int Origin,
        int[] Offsets,
        Bounds? Rectangle);
    private sealed class ReferenceComparer<T> : IEqualityComparer<T> where T : class
    {
        public static ReferenceComparer<T> Instance { get; } = new();
        public bool Equals(T? left, T? right) => ReferenceEquals(left, right);
        public int GetHashCode(T value) => RuntimeHelpers.GetHashCode(value);
    }
    private enum Format { Lq4, Hq8 }
    private enum Tier { LqAlias, Normal, Close }
    private enum StorageRole { Pixel, Palette, Compound }
    private enum StorageKind { SharedAlias, CombinedPalette, HqPalette, LqPalette, LqPixel, Pixel32, Pixel16 }
}
