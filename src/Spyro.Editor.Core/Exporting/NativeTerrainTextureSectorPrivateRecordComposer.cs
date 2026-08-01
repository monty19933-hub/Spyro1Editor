using System.Security.Cryptography;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Atomic private-record request for destinations whose native level-data tail
/// cannot hold another 184-byte texture row. Ordinary terrain patches are
/// always expressed against the retail level-data preimage; the append builder
/// rebases them before the existing sector relocator moves the composed WAD
/// entry.
/// </summary>
public sealed record NativeTerrainTextureSectorPrivateRecordRequest(
    string SourceImagePath,
    string SourceCuePath,
    string OutputPrefix,
    string WadAnalysisPath,
    LevelDefinition TargetLevel,
    NativeTerrainTextureRecordAppendSourceBinding SourceBinding,
    IReadOnlyList<NativeTerrainTextureRelocationImport> ExistingRecordOverrides,
    IReadOnlyList<NativeTerrainTexturePrivateSyntheticRecord> SyntheticRecords,
    IReadOnlyList<NativeTerrainTextureRecordExistingPatch> OrdinaryLevelDataPatches,
    NativeTerrainTexturePrivateRecordStagingSourceProof? StagingSourceProof = null,
    NativeTerrainTextureStructuralGrowthPolicy StructuralGrowthPolicy =
        NativeTerrainTextureStructuralGrowthPolicy.NormalChecked);

public sealed record NativeTerrainTextureSectorPrivateRecordPlan(
    DateTimeOffset GeneratedAt,
    string SourceImagePath,
    string SourceImageSha256,
    string TargetLevelKey,
    string TargetLevelName,
    int TargetWadEntry,
    NativeTerrainTextureGlobalRepackSyntheticPlan GlobalPacking,
    NativeTerrainTextureRecordSectorRelocationRequest StructuralRequest,
    NativeTerrainTextureRecordSectorRelocationPlan Structural,
    IReadOnlyList<NativeTerrainTextureRecordExistingPatch> GeneratedOriginalRowPatches,
    IReadOnlyList<NativeTerrainTextureRecordExistingPatch> OrdinaryLevelDataPatches,
    IReadOnlyList<NativeTerrainTextureRecordRebasedPatchProof> OrdinaryRelocatedPatchProofs,
    IReadOnlyList<NativeTerrainTexturePrivateOriginalRowProof> OriginalRowProofs,
    int TexturePagePatchCount,
    int TexturePagePatchedByteCount,
    bool SourceBindingVerified,
    bool GlobalPackingProofComplete,
    bool OriginalRowsInstalledExactly,
    bool SyntheticRowsInstalledExactly,
    bool OrdinaryPatchesIncludedAndRelocated,
    bool TexturePagePatchesIncludedAndRelocated,
    bool StructuralSectorGrowthVerified,
    bool RequiresDuckStationRuntimeProof,
    IReadOnlyList<string> Notes)
{
    public NativeTerrainTextureStructuralGrowthPolicy StructuralGrowthPolicy =>
        StructuralRequest.StructuralGrowthPolicy;

    public bool StaticResearchOnly =>
        NativeTerrainTextureRecordAppendBuilder.IsStaticResearchOnly(
            StructuralGrowthPolicy);
}

public sealed record NativeTerrainTextureSectorPrivateRecordExportResult(
    NativeTerrainTextureSectorPrivateRecordPlan Plan,
    string OutputImagePath,
    string OutputCuePath,
    string OutputImageSha256,
    bool SourceImagePreserved,
    bool ExactStructuralReadbackVerified,
    bool OrdinaryRelocatedPatchReadbackVerified,
    bool RuntimeTargetReadbackVerified,
    bool GlobalLogicalReadbackVerified,
    bool AtomicRenameCompleted);

/// <summary>
/// Joins the global alias-preserving page repacker to normal checked
/// +0x800/+0x1000 relocation or explicit static-research aligned growth through
/// +0x2800. This composer deliberately owns no WAD/ISO movement: all archive
/// growth and executable relocation remains inside
/// <see cref="NativeTerrainTextureRecordSectorRelocationComposer"/>.
/// </summary>
public static class NativeTerrainTextureSectorPrivateRecordComposer
{
    private const int WadLba = 37;

    public static NativeTerrainTextureSectorPrivateRecordPlan BuildPlan(
        NativeTerrainTextureSectorPrivateRecordRequest request)
    {
        if (!TryBuild(request, out NativeTerrainTextureSectorPrivateRecordPlan? plan, out string failureReason) ||
            plan == null)
        {
            throw new InvalidOperationException(failureReason);
        }
        return plan;
    }

    public static bool TryBuild(
        NativeTerrainTextureSectorPrivateRecordRequest request,
        out NativeTerrainTextureSectorPrivateRecordPlan? plan,
        out string failureReason)
    {
        plan = null;
        failureReason = "";
        try
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(request.TargetLevel);
            ArgumentNullException.ThrowIfNull(request.SourceBinding);
            ArgumentNullException.ThrowIfNull(request.ExistingRecordOverrides);
            ArgumentNullException.ThrowIfNull(request.SyntheticRecords);
            ArgumentNullException.ThrowIfNull(request.OrdinaryLevelDataPatches);
            ArgumentException.ThrowIfNullOrWhiteSpace(request.SourceImagePath);
            ArgumentException.ThrowIfNullOrWhiteSpace(request.SourceCuePath);
            ArgumentException.ThrowIfNullOrWhiteSpace(request.OutputPrefix);
            ArgumentException.ThrowIfNullOrWhiteSpace(request.WadAnalysisPath);
            if (!File.Exists(request.SourceImagePath))
                throw new FileNotFoundException("Missing retail source image.", request.SourceImagePath);
            if (!File.Exists(request.SourceCuePath))
                throw new FileNotFoundException("Missing retail source CUE.", request.SourceCuePath);
            if (!File.Exists(request.WadAnalysisPath))
                throw new FileNotFoundException("Missing source-bound WAD analysis.", request.WadAnalysisPath);
            if (request.SyntheticRecords.Count == 0)
                throw new InvalidOperationException("At least one private synthetic texture record is required.");

            HashSet<string> stableIds = new(StringComparer.OrdinalIgnoreCase);
            foreach (NativeTerrainTexturePrivateSyntheticRecord item in request.SyntheticRecords)
            {
                if (string.IsNullOrWhiteSpace(item.StableEditId) || !stableIds.Add(item.StableEditId.Trim()))
                    throw new InvalidDataException("Private synthetic records require unique non-empty stable edit identities.");
                if (string.IsNullOrWhiteSpace(item.DonorLevelKey) || item.DonorWadEntry < 0 || item.DonorTextureId < 0)
                    throw new InvalidDataException($"Private synthetic record '{item.StableEditId}' is missing donor provenance.");
            }

            NativeTerrainTextureGlobalRepackSyntheticRequest globalRequest = new(
                request.SourceImagePath,
                request.TargetLevel,
                request.ExistingRecordOverrides,
                request.SyntheticRecords
                    .Select(item => new NativeTerrainTextureGlobalRepackSyntheticRecord(
                        item.AssignedTextureId,
                        item.DonorWadEntry,
                        item.DonorTextureId,
                        item.MaterialTemplateTextureId))
                    .ToArray(),
                request.SourceImagePath,
                request.StagingSourceProof);
            if (!NativeTerrainTextureGlobalRepackerResearch.TryBuild(
                    globalRequest,
                    out NativeTerrainTextureGlobalRepackSyntheticPlan? global,
                    out failureReason) ||
                global == null)
            {
                return false;
            }
            if (!HasCompleteGlobalProof(global))
            {
                failureReason = "The global page packing plan omitted an exact-pixel, palette, alias, protected-storage, fixed-row, or material-bit proof.";
                return false;
            }

            Dictionary<int, NativeTerrainTexturePrivateSyntheticRecord> identities =
                request.SyntheticRecords.ToDictionary(item => item.AssignedTextureId);
            NativeTerrainTexturePackedAppendRecord[] appendRecords = global.SyntheticRecords
                .OrderBy(row => row.TargetTextureId)
                .Select(row =>
                {
                    if (!identities.TryGetValue(row.TargetTextureId, out NativeTerrainTexturePrivateSyntheticRecord? identity) ||
                        row.DonorWadEntry != identity.DonorWadEntry ||
                        row.DonorTextureId != identity.DonorTextureId)
                    {
                        throw new InvalidDataException($"Global synthetic row T{row.TargetTextureId} does not match its saved edit identity/provenance.");
                    }
                    return new NativeTerrainTexturePackedAppendRecord(
                        identity.StableEditId,
                        identity.DonorLevelKey,
                        identity.DonorWadEntry,
                        identity.DonorTextureId,
                        identity.MaterialTemplateTextureId,
                        row.LowDetailRow.ToArray(),
                        row.HighDetailRow.ToArray(),
                        row.LowDetailRowSha256,
                        row.HighDetailRowSha256);
                })
                .ToArray();

            NativeTerrainTextureRecordSectorRelocationRequest provisionalRequest = new(
                request.SourceImagePath,
                request.SourceCuePath,
                request.OutputPrefix,
                request.WadAnalysisPath,
                request.TargetLevel,
                request.SourceBinding,
                appendRecords,
                Array.Empty<NativeTerrainTextureRecordExistingPatch>(),
                Array.Empty<NativeTerrainTextureRecordRelocationExternalPatch>(),
                request.StagingSourceProof,
                request.StructuralGrowthPolicy);
            NativeTerrainTextureRecordSectorRelocationPlan provisional =
                NativeTerrainTextureRecordSectorRelocationComposer.BuildPlan(provisionalRequest);
            if (global.SourceTextureCount != provisional.Append.SourceTextureCount ||
                global.OutputTextureCount != provisional.Append.OutputTextureCount)
            {
                failureReason = "The global repacker and expanded append builder disagree on source/output texture counts.";
                return false;
            }

            List<NativeTerrainTextureRecordExistingPatch> generated = [];
            List<RowSource> rowSources = [];
            HashSet<int> originalIds = [];
            int sourceHighStart = checked(8 +
                (provisional.Append.SourceTextureCount * NativeTerrainTextureRecordAppendBuilder.LowDetailRecordBytes));
            foreach (NativeTerrainTextureGlobalRepackPackedRecord row in
                     global.OriginalMovableRecords.OrderBy(item => item.TargetTextureId))
            {
                if (row.TargetTextureId < 0 || row.TargetTextureId >= provisional.Append.SourceTextureCount ||
                    !originalIds.Add(row.TargetTextureId))
                {
                    failureReason = $"Global packing returned invalid or duplicate original movable row T{row.TargetTextureId}.";
                    return false;
                }
                int lowRelative = checked(8 +
                    (row.TargetTextureId * NativeTerrainTextureRecordAppendBuilder.LowDetailRecordBytes));
                int highRelative = checked(sourceHighStart +
                    (row.TargetTextureId * NativeTerrainTextureRecordAppendBuilder.HighDetailRecordBytes));
                byte[] lowBefore = provisional.Append.Patch.Before
                    .AsSpan(lowRelative, NativeTerrainTextureRecordAppendBuilder.LowDetailRecordBytes)
                    .ToArray();
                byte[] highBefore = provisional.Append.Patch.Before
                    .AsSpan(highRelative, NativeTerrainTextureRecordAppendBuilder.HighDetailRecordBytes)
                    .ToArray();
                long lowWadOffset = provisional.Append.LevelDataWadOffset + lowRelative;
                long highWadOffset = provisional.Append.LevelDataWadOffset + highRelative;
                generated.Add(new NativeTerrainTextureRecordExistingPatch(
                    lowWadOffset,
                    lowBefore,
                    row.LowDetailRow.ToArray(),
                    "global-repack-existing-row-lq",
                    $"global-repack:T{row.TargetTextureId}:lq"));
                generated.Add(new NativeTerrainTextureRecordExistingPatch(
                    highWadOffset,
                    highBefore,
                    row.HighDetailRow.ToArray(),
                    "global-repack-existing-row-hq",
                    $"global-repack:T{row.TargetTextureId}:hq"));
                rowSources.Add(new RowSource(row, lowWadOffset, highWadOffset, lowBefore, highBefore));
            }

            NativeTerrainTextureRecordExistingPatch[] allExisting = generated
                .Concat(request.OrdinaryLevelDataPatches)
                .OrderBy(item => item.WadOffset)
                .ThenBy(item => item.Before?.Length ?? 0)
                .ToArray();
            if (!TryValidateDisjointExistingPatches(allExisting, out failureReason))
                return false;

            NativeTerrainTextureRecordRelocationExternalPatch[] pagePatches = global.TexturePagePatches
                .OrderBy(item => item.WadOffset)
                .Select((patch, index) => new NativeTerrainTextureRecordRelocationExternalPatch(
                    patch.WadOffset,
                    patch.Before.ToArray(),
                    patch.After.ToArray(),
                    patch.Kind,
                    $"global-page-{index:D5}",
                    patch.Description))
                .ToArray();
            NativeTerrainTextureRecordSectorRelocationRequest structuralRequest = provisionalRequest with
            {
                ExistingLevelDataPatches = allExisting,
                ExternalPatches = pagePatches
            };
            NativeTerrainTextureRecordSectorRelocationPlan structural =
                NativeTerrainTextureRecordSectorRelocationComposer.BuildPlan(structuralRequest);
            if (!structural.Append.ExistingPatchesRebasedExactly ||
                structural.Append.RebasedPatches.Count != allExisting.Length)
            {
                failureReason = "The expanded append did not consume and rebase every original-row and ordinary level-data patch.";
                return false;
            }

            List<NativeTerrainTextureRecordRebasedPatchProof> ordinaryProofs = [];
            foreach (NativeTerrainTextureRecordExistingPatch ordinary in request.OrdinaryLevelDataPatches)
            {
                string beforeSha = Sha256(ordinary.Before);
                string afterSha = Sha256(ordinary.After);
                NativeTerrainTextureRecordRebasedPatchProof[] matches = structural.RelocatedLevelDataPatchProofs
                    .Where(item =>
                        item.SourceWadOffset == ordinary.WadOffset &&
                        item.ByteLength == ordinary.Before.Length &&
                        item.Kind.Equals(ordinary.Kind, StringComparison.Ordinal) &&
                        item.RuntimeKey.Equals(ordinary.RuntimeKey, StringComparison.Ordinal) &&
                        HashEquals(item.BeforeSha256, beforeSha) &&
                        HashEquals(item.AfterSha256, afterSha))
                    .ToArray();
                if (matches.Length != 1)
                {
                    failureReason =
                        $"Ordinary retail-preimage patch {ordinary.Kind}/{ordinary.RuntimeKey} at WAD 0x{ordinary.WadOffset:X} did not produce exactly one relocated rebase proof.";
                    return false;
                }
                ordinaryProofs.Add(matches[0]);
            }

            int outputHighStart = checked(8 +
                (structural.Append.OutputTextureCount * NativeTerrainTextureRecordAppendBuilder.LowDetailRecordBytes));
            NativeTerrainTexturePrivateOriginalRowProof[] originalProofs = rowSources
                .Select(source =>
                {
                    int lowRelative = checked(8 +
                        (source.Row.TargetTextureId * NativeTerrainTextureRecordAppendBuilder.LowDetailRecordBytes));
                    int highRelative = checked(outputHighStart +
                        (source.Row.TargetTextureId * NativeTerrainTextureRecordAppendBuilder.HighDetailRecordBytes));
                    bool exact = structural.Append.Patch.After
                        .AsSpan(lowRelative, NativeTerrainTextureRecordAppendBuilder.LowDetailRecordBytes)
                        .SequenceEqual(source.Row.LowDetailRow) &&
                        structural.Append.Patch.After
                            .AsSpan(highRelative, NativeTerrainTextureRecordAppendBuilder.HighDetailRecordBytes)
                            .SequenceEqual(source.Row.HighDetailRow);
                    return new NativeTerrainTexturePrivateOriginalRowProof(
                        source.Row.TargetTextureId,
                        source.Row.DonorWadEntry,
                        source.Row.DonorTextureId,
                        source.SourceLowWadOffset,
                        structural.RelocatedLevelDataWadOffset + lowRelative,
                        source.SourceHighWadOffset,
                        structural.RelocatedLevelDataWadOffset + highRelative,
                        source.Row.LowDetailRowSha256,
                        source.Row.HighDetailRowSha256,
                        !source.LowBefore.AsSpan().SequenceEqual(source.Row.LowDetailRow),
                        !source.HighBefore.AsSpan().SequenceEqual(source.Row.HighDetailRow),
                        exact);
                })
                .ToArray();
            bool originalRowsInstalled = originalProofs.All(item => item.InstalledExactly);
            bool syntheticRowsInstalled = global.SyntheticRecords.All(row =>
            {
                int lowRelative = checked(8 +
                    (row.TargetTextureId * NativeTerrainTextureRecordAppendBuilder.LowDetailRecordBytes));
                int highRelative = checked(outputHighStart +
                    (row.TargetTextureId * NativeTerrainTextureRecordAppendBuilder.HighDetailRecordBytes));
                return structural.Append.Patch.After
                           .AsSpan(lowRelative, NativeTerrainTextureRecordAppendBuilder.LowDetailRecordBytes)
                           .SequenceEqual(row.LowDetailRow) &&
                       structural.Append.Patch.After
                           .AsSpan(highRelative, NativeTerrainTextureRecordAppendBuilder.HighDetailRecordBytes)
                           .SequenceEqual(row.HighDetailRow);
            });
            if (!originalRowsInstalled || !syntheticRowsInstalled)
            {
                failureReason = "One or more global-pack existing/synthetic rows were not installed exactly in the expanded level-data image.";
                return false;
            }
            if (structural.RelocatedExternalPatches.Count != pagePatches.Length ||
                !structural.ExternalPatchPreimagesVerified ||
                !structural.RelocationOffsetsVerified)
            {
                failureReason = "One or more global texture-page patches were not source-bound and relocated by the structural composer.";
                return false;
            }

            int sectorGrowth = structural.SectorGrowthBytes;
            bool exactSectorGrowth =
                NativeTerrainTextureRecordAppendBuilder.IsSectorGrowthAuthorized(
                    sectorGrowth,
                    request.StructuralGrowthPolicy) &&
                structural.ExpandedWadSize == structural.OriginalWadSize + sectorGrowth &&
                structural.RelocatedExecutableLba ==
                    structural.OriginalExecutableLba + (sectorGrowth / 0x800);
            if (!exactSectorGrowth)
            {
                failureReason =
                    $"The private-record structural path did not retain the audited aligned WAD/executable relocation authorized by {request.StructuralGrowthPolicy}.";
                return false;
            }

            plan = new NativeTerrainTextureSectorPrivateRecordPlan(
                DateTimeOffset.UtcNow,
                Path.GetFullPath(request.SourceImagePath),
                request.SourceBinding.SourceImageSha256,
                request.TargetLevel.Key,
                request.TargetLevel.DisplayName,
                request.TargetLevel.SourceWadEntry,
                global,
                structuralRequest,
                structural,
                generated,
                request.OrdinaryLevelDataPatches.ToArray(),
                ordinaryProofs,
                originalProofs,
                global.TexturePagePatches.Count,
                global.TexturePagePatches.Sum(item => item.ByteLength),
                true,
                true,
                originalRowsInstalled,
                syntheticRowsInstalled,
                ordinaryProofs.Count == request.OrdinaryLevelDataPatches.Count,
                structural.RelocatedExternalPatches.Count == pagePatches.Length,
                exactSectorGrowth,
                true,
                [
                    $"Global packing installed {originalProofs.Length} complete existing movable row(s) and {global.SyntheticRecords.Count} synthetic row(s).",
                    $"All {ordinaryProofs.Count} ordinary retail-preimage patch(es) were rebased inside the complete expanded level-data payload before relocation.",
                    $"The sector composer relocated {pagePatches.Length} source-bound global page patch(es) and grew the target WAD entry by exactly 0x{sectorGrowth:X} bytes.",
                    NativeTerrainTextureRecordAppendBuilder.IsStaticResearchOnly(
                        request.StructuralGrowthPolicy)
                        ? "This extended growth plan is explicitly static-research-only and cannot authorize normal Create BIN."
                        : $"Structural growth policy {request.StructuralGrowthPolicy} authorizes checked aligned growth through +0x{NativeTerrainTextureRecordAppendBuilder.GetMaximumSectorGrowthBytes(request.StructuralGrowthPolicy):X}.",
                    "This plan has static and final-BIN proof only; DuckStation gameplay evidence remains required before normal Create BIN promotion."
                ]);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidDataException or InvalidOperationException or IOException or OverflowException)
        {
            failureReason = ex.Message;
            return false;
        }
    }

    public static async Task<NativeTerrainTextureSectorPrivateRecordExportResult> ExportAsync(
        NativeTerrainTextureSectorPrivateRecordRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return await ExportAsync(BuildPlan(request), cancellationToken);
    }

    public static async Task<NativeTerrainTextureSectorPrivateRecordExportResult> ExportAsync(
        NativeTerrainTextureSectorPrivateRecordPlan plan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        NativeTerrainTextureRecordSectorRelocationExportResult structural =
            await NativeTerrainTextureRecordSectorRelocationComposer.ExportAsync(
                plan.StructuralRequest,
                cancellationToken);
        if (!HashEquals(structural.Plan.SourceImageSha256, plan.SourceImageSha256) ||
            structural.Plan.Append.OutputTextureCount != plan.Structural.Append.OutputTextureCount ||
            structural.Plan.RelocatedLevelDataWadOffset != plan.Structural.RelocatedLevelDataWadOffset ||
            !HashEquals(structural.Plan.Append.Patch.AfterSha256, plan.Structural.Append.Patch.AfterSha256) ||
            !RelocatedExternalPatchesEqual(
                structural.Plan.RelocatedExternalPatches,
                plan.Structural.RelocatedExternalPatches))
        {
            throw new InvalidDataException("The exported structural relocation no longer matches the checked private-record plan.");
        }

        VerifyOrdinaryFinalReadback(structural.OutputImagePath, plan);
        NativeTerrainTextureGlobalRepackPackedRecord[] expectedRows = plan.GlobalPacking.OriginalMovableRecords
            .Concat(plan.GlobalPacking.SyntheticRecords)
            .OrderBy(item => item.TargetTextureId)
            .ToArray();
        if (!NativeTerrainTextureGlobalRepackerResearch.TryVerifyCandidateLogicalReadback(
                structural.OutputImagePath,
                plan.StructuralRequest.TargetLevel,
                expectedRows,
                plan.SourceImagePath,
                out string logicalFailure))
        {
            throw new InvalidDataException($"Relocated final BIN failed global logical readback: {logicalFailure}");
        }
        bool sourcePreserved = HashEquals(Sha256File(plan.SourceImagePath), plan.SourceImageSha256);
        if (!sourcePreserved)
            throw new InvalidDataException("Sector private-record export modified its retail source BIN.");

        return new NativeTerrainTextureSectorPrivateRecordExportResult(
            plan,
            structural.OutputImagePath,
            structural.OutputCuePath,
            structural.OutputImageSha256,
            true,
            structural.RelocatedLevelDataReadbackVerified &&
                structural.RelocatedExternalPatchReadbackVerified,
            true,
            structural.RuntimeTargetReadbackVerified,
            true,
            structural.AtomicRenameCompleted);
    }

    private static bool HasCompleteGlobalProof(NativeTerrainTextureGlobalRepackSyntheticPlan plan)
    {
        NativeTerrainTextureGlobalRepackResearchPlan proof = plan.PackingProof;
        return plan.SourceDescriptorOffsetsRemainUnmodified &&
               plan.SyntheticRowsModeledOnlyInMemory &&
               plan.RequiresStructuralComposer &&
               proof.ExactIndexedPixelReadbackVerified &&
               proof.ExactPaletteReadbackVerified &&
               proof.PixelAliasRelationshipsPreserved &&
               proof.PaletteAliasRelationshipsPreserved &&
               proof.LowDetailAliasPreserved &&
               proof.ProtectedStoragePreserved &&
               proof.FixedDescriptorRowsPreserved &&
               proof.TargetMaterialBitsPreserved;
    }

    private static bool TryValidateDisjointExistingPatches(
        IReadOnlyList<NativeTerrainTextureRecordExistingPatch> patches,
        out string failureReason)
    {
        failureReason = "";
        for (int index = 0; index < patches.Count; index++)
        {
            NativeTerrainTextureRecordExistingPatch current = patches[index];
            if (current.Before == null || current.After == null || current.Before.Length == 0 ||
                current.Before.Length != current.After.Length)
            {
                failureReason = $"Existing patch {current.Kind}/{current.RuntimeKey} must have equal non-empty before/after bytes.";
                return false;
            }
            if (index == 0)
                continue;
            NativeTerrainTextureRecordExistingPatch previous = patches[index - 1];
            if (RangesOverlap(
                    previous.WadOffset,
                    previous.WadOffset + previous.Before.Length,
                    current.WadOffset,
                    current.WadOffset + current.Before.Length))
            {
                failureReason =
                    $"Existing patches {previous.Kind}/{previous.RuntimeKey} and {current.Kind}/{current.RuntimeKey} overlap; global row ownership and ordinary edits must be disjoint.";
                return false;
            }
        }
        return true;
    }

    private static void VerifyOrdinaryFinalReadback(
        string outputImagePath,
        NativeTerrainTextureSectorPrivateRecordPlan plan)
    {
        if (plan.OrdinaryLevelDataPatches.Count != plan.OrdinaryRelocatedPatchProofs.Count)
            throw new InvalidDataException("Ordinary patch/relocated-proof counts disagree in the sector private-record plan.");
        DiscLayout layout = DiscImage.DetectLayout(outputImagePath);
        using FileStream output = File.OpenRead(outputImagePath);
        for (int index = 0; index < plan.OrdinaryLevelDataPatches.Count; index++)
        {
            NativeTerrainTextureRecordExistingPatch ordinary = plan.OrdinaryLevelDataPatches[index];
            NativeTerrainTextureRecordRebasedPatchProof proof = plan.OrdinaryRelocatedPatchProofs[index];
            byte[] actual = DiscImage.ReadFileBytes(
                output,
                layout,
                WadLba,
                proof.OutputWadOffset,
                proof.ByteLength);
            if (!actual.SequenceEqual(ordinary.After) || !HashEquals(Sha256(actual), proof.AfterSha256))
            {
                throw new InvalidDataException(
                    $"Final BIN ordinary patch {ordinary.Kind}/{ordinary.RuntimeKey} was not installed at relocated WAD 0x{proof.OutputWadOffset:X}; writing it later at retail WAD 0x{proof.SourceWadOffset:X} is forbidden.");
            }
        }
    }

    private static bool RangesOverlap(long firstStart, long firstEnd, long secondStart, long secondEnd) =>
        firstStart < secondEnd && secondStart < firstEnd;

    private static bool RelocatedExternalPatchesEqual(
        IReadOnlyList<NativeTerrainTextureRecordRelocatedExternalPatch> left,
        IReadOnlyList<NativeTerrainTextureRecordRelocatedExternalPatch> right) =>
        left.Count == right.Count &&
        left.Zip(right).All(pair =>
            pair.First.SourceWadOffset == pair.Second.SourceWadOffset &&
            pair.First.RelocatedWadOffset == pair.Second.RelocatedWadOffset &&
            pair.First.ByteLength == pair.Second.ByteLength &&
            HashEquals(pair.First.BeforeSha256, pair.Second.BeforeSha256) &&
            HashEquals(pair.First.AfterSha256, pair.Second.AfterSha256));

    private static string Sha256(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes));

    private static string Sha256File(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static bool HashEquals(string left, string right) =>
        !string.IsNullOrWhiteSpace(left) &&
        !string.IsNullOrWhiteSpace(right) &&
        string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);

    private sealed record RowSource(
        NativeTerrainTextureGlobalRepackPackedRecord Row,
        long SourceLowWadOffset,
        long SourceHighWadOffset,
        byte[] LowBefore,
        byte[] HighBefore);
}
