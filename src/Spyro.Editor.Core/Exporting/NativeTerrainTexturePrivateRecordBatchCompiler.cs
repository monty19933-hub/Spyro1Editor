using System.Security.Cryptography;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Exporting;

public enum NativeTerrainTexturePrivateImageWriterKind
{
    FixedTail,
    SectorRelocation
}

/// <summary>
/// One source-bound compilation request for every appended-private texture in
/// a destination level. Ordinary terrain edits must already be represented as
/// equal-length patches against this same retail level-data preimage. Custom
/// image texture writes are intentionally excluded until they use the same
/// global page allocator.
/// </summary>
public sealed record NativeTerrainTexturePrivateRecordBatchRequest(
    string SourceImagePath,
    string SourceCuePath,
    string OutputPrefix,
    string WadAnalysisPath,
    LevelDefinition TargetLevel,
    IReadOnlyList<NativeTerrainTextureRelocationEdit> SavedEdits,
    IReadOnlyList<NativeTerrainTextureRecordExistingPatch> OrdinaryLevelDataPatches,
    bool HasCustomImageTexturePatches = false,
    NativeTerrainTextureStructuralGrowthPolicy StructuralGrowthPolicy =
        NativeTerrainTextureStructuralGrowthPolicy.NormalChecked);

public sealed record NativeTerrainTexturePrivateRecordBatchPlan(
    DateTimeOffset GeneratedAt,
    string SourceImagePath,
    string SourceImageSha256,
    string SourceCuePath,
    string OutputPrefix,
    string TargetLevelKey,
    int TargetWadEntry,
    NativeTerrainTextureRecordAppendSourceBinding SourceBinding,
    IReadOnlyList<NativeTerrainTextureRelocationEdit> ExistingRecordEdits,
    IReadOnlyList<NativeTerrainTextureRelocationEdit> AppendedPrivateEdits,
    IReadOnlyList<NativeTerrainTextureRelocationImport> ExistingRecordOverrides,
    IReadOnlyList<NativeTerrainTexturePrivateSyntheticRecord> SyntheticRecords,
    IReadOnlyList<NativeTerrainTextureRecordExistingPatch> OrdinaryLevelDataPatches,
    NativeTerrainTexturePrivateImageWriterKind WriterKind,
    NativeTerrainTextureFixedTailPrivateRecordPlan? FixedTailPlan,
    NativeTerrainTextureSectorPrivateRecordPlan? SectorRelocationPlan,
    bool SourcePreimagesVerified,
    bool ContractsUnique,
    bool AppendedIdsContiguous,
    bool CustomImageTexturePatchesExcluded,
    bool ExclusiveImageWriterSelected,
    bool RequiresDuckStationRuntimeProof,
    IReadOnlyList<string> Notes)
{
    public NativeTerrainTextureStructuralGrowthPolicy StructuralGrowthPolicy { get; init; } =
        NativeTerrainTextureStructuralGrowthPolicy.NormalChecked;

    public bool StaticResearchOnly =>
        NativeTerrainTextureRecordAppendBuilder.IsStaticResearchOnly(
            StructuralGrowthPolicy);
}

public sealed record NativeTerrainTexturePrivateRecordBatchExportResult(
    NativeTerrainTexturePrivateRecordBatchPlan Plan,
    string OutputImagePath,
    string OutputCuePath,
    string OutputImageSha256,
    bool SourceImagePreserved,
    bool ExactReadbackVerified,
    bool RuntimeTargetReadbackVerified,
    bool GlobalLogicalReadbackVerified,
    bool AtomicRenameCompleted);

public sealed record NativeTerrainTexturePrivateRecordStagingPreflightResult(
    NativeTerrainTextureRecordAppendSourceBinding SourceBinding,
    NativeTerrainTexturePrivateImageWriterKind WriterKind,
    int AppendedPrivateEditCount);

/// <summary>
/// Converts a v3 saved-edit manifest into exactly one structural image writer.
/// It never stacks the legacy in-place relocation exporter after a table append:
/// fixed-tail destinations use one complete level-data patch and tight levels
/// use either normal checked growth or an explicitly static-research extended
/// relocation composer. Any stale source
/// binding, duplicate donor contract, custom-image page write, or
/// non-contiguous private id stops the batch before an output BIN is created.
/// </summary>
public static class NativeTerrainTexturePrivateRecordBatchCompiler
{
    private const string FixedTailCapacityFailurePrefix =
        "The private-record destination is not fixed-tail appendable:";

    /// <summary>
    /// Converts the ordinary portion of a normal terrain preflight into the
    /// retail-preimage patches consumed by the structural composer. The
    /// preflight must be built without the native relocation manifest; seeing
    /// legacy relocation/page patches here means two allocators would own the
    /// same transaction and is therefore a hard failure.
    /// </summary>
    public static IReadOnlyList<NativeTerrainTextureRecordExistingPatch>
        ConvertOrdinaryTerrainPlan(TerrainPatchPlan terrainPlan)
    {
        ArgumentNullException.ThrowIfNull(terrainPlan);
        if (terrainPlan.NativeTextureRelocationBytePatchCount != 0 ||
            terrainPlan.Patches.Any(patch =>
                (patch.Kind ?? "").StartsWith("native-terrain-texture-", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                "The terrain preflight already contains legacy native-texture relocation patches. Rebuild it without the relocation manifest so the private-record batch is the sole allocator/writer.");
        }
        if (terrainPlan.CustomTextureBytePatchCount != 0 ||
            terrainPlan.Patches.Any(patch =>
                (patch.Kind ?? "").StartsWith("custom-texture-", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                "The terrain preflight contains custom/imported-image page patches, which cannot yet share the private-record global allocator.");
        }
        string? atomicSkip = terrainPlan.SkippedEdits.FirstOrDefault(item =>
            (item ?? "").StartsWith("[atomic terrain swap blocked]", StringComparison.Ordinal));
        if (atomicSkip != null)
            throw new InvalidOperationException($"The ordinary terrain preflight is not atomic: {atomicSkip}");

        List<NativeTerrainTextureRecordExistingPatch> converted = [];
        foreach (TerrainPatch patch in terrainPlan.Patches)
        {
            if (!TryParseWadOffset(patch.WadRelativeOffset, out long wadOffset))
            {
                throw new InvalidDataException(
                    $"Terrain patch {patch.Kind}/{patch.RuntimeKey} has an invalid WAD offset '{patch.WadRelativeOffset}'.");
            }
            byte[] before = ParseHex(patch.BeforeHexPreview);
            byte[] after = ParseHex(patch.AfterHexPreview);
            if (before.Length == 0 || before.Length != after.Length || before.Length != patch.ByteLength)
            {
                throw new InvalidDataException(
                    $"Terrain patch {patch.Kind}/{patch.RuntimeKey} does not contain one complete equal-length before/after preimage.");
            }
            converted.Add(new NativeTerrainTextureRecordExistingPatch(
                wadOffset,
                before,
                after,
                patch.Kind,
                patch.RuntimeKey));
        }
        return converted
            .OrderBy(patch => patch.WadOffset)
            .ThenBy(patch => patch.Before.Length)
            .ToArray();
    }

    public static NativeTerrainTexturePrivateRecordBatchPlan BuildPlan(
        NativeTerrainTexturePrivateRecordBatchRequest request)
    {
        if (!TryBuild(
                request,
                out NativeTerrainTexturePrivateRecordBatchPlan? plan,
                out string failureReason) ||
            plan == null)
        {
            throw new InvalidOperationException(failureReason);
        }
        return plan;
    }

    public static bool TryBuild(
        NativeTerrainTexturePrivateRecordBatchRequest request,
        out NativeTerrainTexturePrivateRecordBatchPlan? plan,
        out string failureReason) =>
        TryBuildCore(
            request,
            stagingSourceProof: null,
            out plan,
            out failureReason);

    /// <summary>
    /// Runs the complete structural/global planner for editor staging while
    /// reusing one exact, source-stable full-BIN binding. Only a compact result
    /// is returned, so this path cannot be handed to the output writer. Normal
    /// BuildPlan/Create BIN continue through <see cref="TryBuild"/> and hash the
    /// source again before returning an exportable plan.
    /// </summary>
    public static bool TryBuildStagingPreflight(
        NativeTerrainTexturePrivateRecordBatchRequest request,
        NativeTerrainTexturePrivateRecordStagingSourceProof stagingSourceProof,
        out NativeTerrainTexturePrivateRecordStagingPreflightResult? result,
        out string failureReason)
    {
        ArgumentNullException.ThrowIfNull(stagingSourceProof);
        result = null;
        if (!TryBuildCore(
                request,
                stagingSourceProof,
                out NativeTerrainTexturePrivateRecordBatchPlan? plan,
                out failureReason) ||
            plan == null)
        {
            return false;
        }
        if (!stagingSourceProof.TryValidate(
                request.SourceImagePath,
                request.TargetLevel,
                plan.SourceBinding,
                out failureReason))
        {
            return false;
        }
        result = new NativeTerrainTexturePrivateRecordStagingPreflightResult(
            plan.SourceBinding,
            plan.WriterKind,
            plan.AppendedPrivateEdits.Count);
        return true;
    }

    private static bool TryBuildCore(
        NativeTerrainTexturePrivateRecordBatchRequest request,
        NativeTerrainTexturePrivateRecordStagingSourceProof? stagingSourceProof,
        out NativeTerrainTexturePrivateRecordBatchPlan? plan,
        out string failureReason)
    {
        plan = null;
        failureReason = "";
        try
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(request.TargetLevel);
            ArgumentNullException.ThrowIfNull(request.SavedEdits);
            ArgumentNullException.ThrowIfNull(request.OrdinaryLevelDataPatches);
            ArgumentException.ThrowIfNullOrWhiteSpace(request.SourceImagePath);
            ArgumentException.ThrowIfNullOrWhiteSpace(request.SourceCuePath);
            ArgumentException.ThrowIfNullOrWhiteSpace(request.OutputPrefix);
            _ = NativeTerrainTextureRecordAppendBuilder.GetMaximumSectorGrowthBytes(
                request.StructuralGrowthPolicy);
            if (!File.Exists(request.SourceImagePath))
                throw new FileNotFoundException("Missing source BIN for private terrain textures.", request.SourceImagePath);
            if (!File.Exists(request.SourceCuePath))
                throw new FileNotFoundException("Missing source CUE for private terrain textures.", request.SourceCuePath);
            if (request.TargetLevel.SourceWadEntry < 0)
                throw new InvalidOperationException("The destination level has no mapped native WAD entry.");
            if (request.HasCustomImageTexturePatches || request.OrdinaryLevelDataPatches.Any(patch =>
                    (patch.Kind ?? "").StartsWith("custom-texture-", StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException(
                    "Appended-private native textures cannot share a build with custom/imported-image texture-page patches until those writes use the same global allocator.");
            }

            foreach (NativeTerrainTextureRelocationEdit edit in request.SavedEdits)
                ValidateCanonicalSavedEdit(edit);

            NativeTerrainTextureRelocationEdit[] existing = request.SavedEdits
                .Where(edit => !edit.UsesAppendedPrivateRecord)
                .OrderBy(edit => edit.TargetTextureId)
                .ToArray();
            NativeTerrainTextureRelocationEdit[] appended = request.SavedEdits
                .Where(edit => edit.UsesAppendedPrivateRecord)
                .OrderBy(edit => edit.TargetTextureId)
                .ToArray();
            if (appended.Length == 0)
                throw new InvalidOperationException("No appended-private native terrain texture record is staged for this level.");
            if (!NativeTerrainTextureRecordAppendBuilder.IsRequestedRecordCountAuthorized(
                    appended.Length,
                    request.StructuralGrowthPolicy))
            {
                throw new InvalidOperationException(
                    $"Structural growth policy {request.StructuralGrowthPolicy} permits at most " +
                    $"{NativeTerrainTextureRecordAppendBuilder.GetMaximumRequestedRecordCount(request.StructuralGrowthPolicy)} " +
                    $"appended native texture record(s); this request contains {appended.Length}.");
            }
            if (request.SavedEdits.GroupBy(edit => edit.TargetTextureId).Any(group => group.Count() != 1))
                throw new InvalidDataException("The saved texture manifest contains duplicate target texture ids.");

            NativeTerrainTextureRecordAppendSourceBinding binding;
            string sourceSha256;
            if (stagingSourceProof == null)
            {
                binding =
                    NativeTerrainTextureRecordAppendBuilder.InspectSourceBinding(
                        request.SourceImagePath,
                        request.TargetLevel);
                sourceSha256 = Sha256File(request.SourceImagePath);
            }
            else
            {
                binding = stagingSourceProof.SourceBinding;
                if (!stagingSourceProof.TryValidate(
                        request.SourceImagePath,
                        request.TargetLevel,
                        binding,
                        out string stagingFailure))
                {
                    throw new InvalidDataException(stagingFailure);
                }
                sourceSha256 = binding.SourceImageSha256;
            }
            if (!HashEquals(sourceSha256, binding.SourceImageSha256))
                throw new InvalidDataException("The inspected source BIN hash and append binding disagree.");

            foreach (NativeTerrainTextureRelocationEdit edit in existing)
            {
                if (edit.TargetTextureId >= binding.ExpectedSourceTextureCount)
                {
                    throw new InvalidDataException(
                        $"Existing-record edit T{edit.TargetTextureId} is outside the bound retail table T0..T{binding.ExpectedSourceTextureCount - 1}.");
                }
            }

            for (int index = 0; index < appended.Length; index++)
            {
                NativeTerrainTextureRelocationEdit edit = appended[index];
                int expectedTextureId = binding.ExpectedSourceTextureCount + index;
                if (edit.TargetTextureId != expectedTextureId ||
                    !string.Equals(
                        edit.PrivateRecordEditId,
                        NativeTerrainTextureRelocationEditStore.BuildAppendedPrivateRecordId(expectedTextureId),
                        StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        $"Appended-private ids are not contiguous: row {index} must be T{expectedTextureId}, not T{edit.TargetTextureId}. Apply the compaction remap to the terrain faces and manifest before building.");
                }
                if (edit.TargetWadEntry != binding.TargetWadEntry ||
                    edit.SourceTextureRecordCount != binding.ExpectedSourceTextureCount ||
                    !HashEquals(edit.SourceImageSha256, binding.SourceImageSha256) ||
                    !HashEquals(edit.SourceTextureComponentSha256, binding.ExpectedTextureComponentSha256) ||
                    !HashEquals(edit.SourceLevelDataSha256, binding.ExpectedLevelDataSha256))
                {
                    throw new InvalidDataException(
                        $"Appended-private edit T{edit.TargetTextureId} has a stale disc, WAD-entry, texture-table, or level-data preimage.");
                }
                if (!edit.PreservesTargetNativeSurface ||
                    edit.MaterialTemplateTextureId < 0 ||
                    edit.MaterialTemplateTextureId >= binding.ExpectedSourceTextureCount)
                {
                    throw new InvalidDataException(
                        $"Appended-private edit T{edit.TargetTextureId} must preserve a valid retail target material template.");
                }
            }

            var duplicateContract = appended
                .GroupBy(edit => new
                {
                    edit.DonorWadEntry,
                    edit.DonorTextureId,
                    edit.MaterialTemplateTextureId,
                    edit.ApplyMode
                })
                .FirstOrDefault(group => group.Count() > 1);
            if (duplicateContract != null)
            {
                throw new InvalidDataException(
                    $"Private texture records {string.Join(", ", duplicateContract.Select(edit => $"T{edit.TargetTextureId}"))} have the same donor/material contract. Reuse one record and compact all face ids before building.");
            }

            NativeTerrainTextureRelocationImport[] overrides = existing
                .Select(edit => new NativeTerrainTextureRelocationImport(
                    edit.TargetTextureId,
                    edit.DonorWadEntry,
                    edit.DonorTextureId,
                    NativeTerrainTextureRelocationEditStore.CompleteDescriptorTier,
                    edit.PreservesTargetNativeSurface))
                .ToArray();
            NativeTerrainTexturePrivateSyntheticRecord[] synthetic = appended
                .Select(edit => new NativeTerrainTexturePrivateSyntheticRecord(
                    edit.PrivateRecordEditId,
                    edit.TargetTextureId,
                    LevelCatalog.NormalizeKey(edit.DonorLevelKey),
                    edit.DonorWadEntry,
                    edit.DonorTextureId,
                    edit.MaterialTemplateTextureId))
                .ToArray();

            NativeTerrainTextureFixedTailPrivateRecordRequest fixedRequest = new(
                request.SourceImagePath,
                request.TargetLevel,
                binding,
                overrides,
                synthetic,
                request.OrdinaryLevelDataPatches,
                stagingSourceProof);
            if (NativeTerrainTextureFixedTailPrivateRecordComposer.TryBuild(
                    fixedRequest,
                    out NativeTerrainTextureFixedTailPrivateRecordPlan? fixedPlan,
                    out string fixedFailure) &&
                fixedPlan != null)
            {
                plan = CreatePlan(
                    request,
                    sourceSha256,
                    binding,
                    existing,
                    appended,
                    overrides,
                    synthetic,
                    NativeTerrainTexturePrivateImageWriterKind.FixedTail,
                    fixedPlan,
                    null);
                return true;
            }

            if (!fixedFailure.StartsWith(FixedTailCapacityFailurePrefix, StringComparison.Ordinal))
            {
                failureReason = fixedFailure;
                return false;
            }
            if (string.IsNullOrWhiteSpace(request.WadAnalysisPath) || !File.Exists(request.WadAnalysisPath))
            {
                failureReason =
                    $"{fixedFailure} This destination requires the exclusive aligned structural writer authorized by {request.StructuralGrowthPolicy}, but its source-bound WAD analysis is unavailable.";
                return false;
            }

            NativeTerrainTextureSectorPrivateRecordRequest sectorRequest = new(
                request.SourceImagePath,
                request.SourceCuePath,
                request.OutputPrefix,
                request.WadAnalysisPath,
                request.TargetLevel,
                binding,
                overrides,
                synthetic,
                request.OrdinaryLevelDataPatches,
                stagingSourceProof,
                request.StructuralGrowthPolicy);
            if (!NativeTerrainTextureSectorPrivateRecordComposer.TryBuild(
                    sectorRequest,
                    out NativeTerrainTextureSectorPrivateRecordPlan? sectorPlan,
                    out string sectorFailure) ||
                sectorPlan == null)
            {
                failureReason = sectorFailure;
                return false;
            }

            plan = CreatePlan(
                request,
                sourceSha256,
                binding,
                existing,
                appended,
                overrides,
                synthetic,
                NativeTerrainTexturePrivateImageWriterKind.SectorRelocation,
                null,
                sectorPlan);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidDataException or InvalidOperationException or IOException or OverflowException)
        {
            failureReason = ex.Message;
            return false;
        }
    }

    public static async Task<NativeTerrainTexturePrivateRecordBatchExportResult> ExportAsync(
        NativeTerrainTexturePrivateRecordBatchPlan plan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (!plan.SourcePreimagesVerified || !plan.ContractsUnique || !plan.AppendedIdsContiguous ||
            !plan.CustomImageTexturePatchesExcluded || !plan.ExclusiveImageWriterSelected)
        {
            throw new InvalidOperationException("The private texture batch is missing a required source, identity, allocation, or exclusive-writer proof.");
        }
        if (!File.Exists(plan.SourceImagePath) ||
            !HashEquals(Sha256File(plan.SourceImagePath), plan.SourceImageSha256))
        {
            throw new InvalidDataException("The source BIN no longer matches the checked private texture batch.");
        }

        if (plan.WriterKind == NativeTerrainTexturePrivateImageWriterKind.FixedTail)
        {
            NativeTerrainTextureFixedTailPrivateRecordPlan fixedPlan = plan.FixedTailPlan
                ?? throw new InvalidDataException("The fixed-tail batch has no fixed-tail composer plan.");
            if (plan.SectorRelocationPlan != null)
                throw new InvalidDataException("The fixed-tail batch unexpectedly contains a second structural writer.");
            NativeTerrainTextureFixedTailPrivateRecordExportResult result =
                await NativeTerrainTextureFixedTailPrivateRecordComposer.ExportAsync(
                    fixedPlan,
                    plan.SourceCuePath,
                    plan.OutputPrefix,
                    cancellationToken);
            return new NativeTerrainTexturePrivateRecordBatchExportResult(
                plan,
                result.OutputImagePath,
                result.OutputCuePath,
                result.OutputImageSha256,
                result.SourceImagePreserved,
                result.ExactPatchReadbackVerified,
                result.RuntimeTargetReadbackVerified,
                result.GlobalLogicalReadbackVerified,
                result.AtomicRenameCompleted);
        }

        NativeTerrainTextureSectorPrivateRecordPlan sectorPlan = plan.SectorRelocationPlan
            ?? throw new InvalidDataException("The sector-relocation batch has no sector composer plan.");
        if (plan.FixedTailPlan != null)
            throw new InvalidDataException("The sector-relocation batch unexpectedly contains a second structural writer.");
        NativeTerrainTextureSectorPrivateRecordExportResult sectorResult =
            await NativeTerrainTextureSectorPrivateRecordComposer.ExportAsync(
                sectorPlan,
                cancellationToken);
        return new NativeTerrainTexturePrivateRecordBatchExportResult(
            plan,
            sectorResult.OutputImagePath,
            sectorResult.OutputCuePath,
            sectorResult.OutputImageSha256,
            sectorResult.SourceImagePreserved,
            sectorResult.ExactStructuralReadbackVerified &&
                sectorResult.OrdinaryRelocatedPatchReadbackVerified,
            sectorResult.RuntimeTargetReadbackVerified,
            sectorResult.GlobalLogicalReadbackVerified,
            sectorResult.AtomicRenameCompleted);
    }

    private static NativeTerrainTexturePrivateRecordBatchPlan CreatePlan(
        NativeTerrainTexturePrivateRecordBatchRequest request,
        string sourceSha256,
        NativeTerrainTextureRecordAppendSourceBinding binding,
        IReadOnlyList<NativeTerrainTextureRelocationEdit> existing,
        IReadOnlyList<NativeTerrainTextureRelocationEdit> appended,
        IReadOnlyList<NativeTerrainTextureRelocationImport> overrides,
        IReadOnlyList<NativeTerrainTexturePrivateSyntheticRecord> synthetic,
        NativeTerrainTexturePrivateImageWriterKind writerKind,
        NativeTerrainTextureFixedTailPrivateRecordPlan? fixedPlan,
        NativeTerrainTextureSectorPrivateRecordPlan? sectorPlan)
    {
        bool exclusive = (fixedPlan != null) ^ (sectorPlan != null);
        if (!exclusive ||
            (writerKind == NativeTerrainTexturePrivateImageWriterKind.FixedTail) != (fixedPlan != null))
        {
            throw new InvalidDataException("Private texture batch writer selection is not exclusive or does not match its composer plan.");
        }
        return new NativeTerrainTexturePrivateRecordBatchPlan(
            DateTimeOffset.UtcNow,
            Path.GetFullPath(request.SourceImagePath),
            sourceSha256,
            Path.GetFullPath(request.SourceCuePath),
            Path.GetFullPath(request.OutputPrefix),
            LevelCatalog.NormalizeKey(request.TargetLevel.Key),
            request.TargetLevel.SourceWadEntry,
            binding,
            existing.ToArray(),
            appended.ToArray(),
            overrides.ToArray(),
            synthetic.ToArray(),
            request.OrdinaryLevelDataPatches.ToArray(),
            writerKind,
            fixedPlan,
            sectorPlan,
            true,
            true,
            true,
            true,
            true,
            true,
            [
                $"Compiled {appended.Count} appended-private record(s) from {appended.Select(edit => LevelCatalog.NormalizeKey(edit.DonorLevelKey)).Distinct(StringComparer.OrdinalIgnoreCase).Count()} donor level(s).",
                $"Selected exactly one structural writer: {writerKind}.",
                NativeTerrainTextureRecordAppendBuilder.IsStaticResearchOnly(
                    request.StructuralGrowthPolicy)
                    ? $"Structural growth policy {request.StructuralGrowthPolicy} is static-research-only and cannot authorize normal Create BIN."
                    : $"Structural growth policy {request.StructuralGrowthPolicy} authorizes checked aligned growth through +0x{NativeTerrainTextureRecordAppendBuilder.GetMaximumSectorGrowthBytes(request.StructuralGrowthPolicy):X}.",
                "Ordinary level-data edits are consumed by the structural composer; applying the legacy terrain writer afterward at retail offsets is forbidden.",
                "Custom/imported-image page writes remain blocked until they share the global alias-preserving allocator.",
                "Static and final-BIN proof does not promote this path to normal Create BIN; DuckStation gameplay evidence is still required."
            ])
        {
            StructuralGrowthPolicy = request.StructuralGrowthPolicy
        };
    }

    private static string Sha256File(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static bool HashEquals(string left, string right) =>
        string.Equals((left ?? "").Trim(), (right ?? "").Trim(), StringComparison.OrdinalIgnoreCase);

    private static void ValidateCanonicalSavedEdit(NativeTerrainTextureRelocationEdit edit)
    {
        ArgumentNullException.ThrowIfNull(edit);
        string normalizedDonorKey = LevelCatalog.NormalizeKey(edit.DonorLevelKey);
        if (edit.TargetTextureId is < 0 or > 127 ||
            edit.DonorWadEntry < 0 ||
            edit.DonorTextureId < 0 ||
            string.IsNullOrWhiteSpace(normalizedDonorKey) ||
            !string.Equals(edit.DonorLevelKey, normalizedDonorKey, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(edit.DonorLevelName) ||
            !string.Equals(edit.DonorLevelName, edit.DonorLevelName.Trim(), StringComparison.Ordinal) ||
            !Enum.IsDefined(edit.ApplyMode) ||
            !Enum.IsDefined(edit.TargetRecordKind))
        {
            throw new InvalidDataException(
                $"Saved texture T{edit.TargetTextureId} has a noncanonical target, donor, apply-mode, or record-kind contract.");
        }
        if (!string.Equals(
                edit.DescriptorTier,
                NativeTerrainTextureRelocationEditStore.CompleteDescriptorTier,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Saved texture T{edit.TargetTextureId} has noncanonical descriptor tier '{edit.DescriptorTier}'; the private-record compiler requires exact tier 'both'.");
        }
        if (string.IsNullOrWhiteSpace(edit.DonorRuntimeKey))
        {
            throw new InvalidDataException(
                $"Saved texture T{edit.TargetTextureId} has no donor runtime/provenance key.");
        }
        if (edit.PreservesTargetNativeSurface)
        {
            string expectedProvenance = NativeTerrainTextureRelocationEditStore.BuildTextureRecordProvenanceKey(
                normalizedDonorKey,
                edit.DonorTextureId);
            if (!string.Equals(edit.DonorRuntimeKey, expectedProvenance, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Saved art-only texture T{edit.TargetTextureId} has noncanonical donor provenance '{edit.DonorRuntimeKey}', expected '{expectedProvenance}'.");
            }
        }
        if (!edit.UsesAppendedPrivateRecord &&
            (edit.TargetWadEntry != -1 ||
             !string.IsNullOrEmpty(edit.SourceImageSha256) ||
             edit.SourceTextureRecordCount != -1 ||
             !string.IsNullOrEmpty(edit.SourceTextureComponentSha256) ||
             !string.IsNullOrEmpty(edit.SourceLevelDataSha256) ||
             edit.MaterialTemplateTextureId != -1 ||
             !string.IsNullOrEmpty(edit.PrivateRecordEditId)))
        {
            throw new InvalidDataException(
                $"Existing-native texture T{edit.TargetTextureId} contains appended-private binding fields and would be reinterpreted by the batch compiler.");
        }
    }

    private static bool TryParseWadOffset(string value, out long result)
    {
        string trimmed = (value ?? "").Trim();
        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return long.TryParse(trimmed[2..], System.Globalization.NumberStyles.HexNumber, null, out result);
        return long.TryParse(trimmed, out result);
    }

    private static byte[] ParseHex(string value)
    {
        string normalized = new((value ?? "").Where(Uri.IsHexDigit).ToArray());
        if (normalized.Length == 0 || normalized.Length % 2 != 0)
            return [];
        try
        {
            return Convert.FromHexString(normalized);
        }
        catch (FormatException)
        {
            return [];
        }
    }
}
