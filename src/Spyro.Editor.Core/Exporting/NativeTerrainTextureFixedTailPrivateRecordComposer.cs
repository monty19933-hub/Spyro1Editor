using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Saved-edit identity and allocator input for one new native texture record.
/// Assigned ids must be the contiguous retail-tail ids selected by the caller.
/// </summary>
public sealed record NativeTerrainTexturePrivateSyntheticRecord(
    string StableEditId,
    int AssignedTextureId,
    string DonorLevelKey,
    int DonorWadEntry,
    int DonorTextureId,
    int MaterialTemplateTextureId);

/// <param name="OrdinaryLevelDataPatches">
/// Equal-length patches bound to the retail level-data preimage. Callers must
/// pass face/collision/material patches here before table growth; they must not
/// apply them later at their obsolete retail offsets. The composer consumes
/// and rebases them inside its one complete level-data transaction.
/// </param>
public sealed record NativeTerrainTextureFixedTailPrivateRecordRequest(
    string SourceImagePath,
    LevelDefinition TargetLevel,
    NativeTerrainTextureRecordAppendSourceBinding SourceBinding,
    IReadOnlyList<NativeTerrainTextureRelocationImport> ExistingRecordOverrides,
    IReadOnlyList<NativeTerrainTexturePrivateSyntheticRecord> SyntheticRecords,
    IReadOnlyList<NativeTerrainTextureRecordExistingPatch> OrdinaryLevelDataPatches,
    NativeTerrainTexturePrivateRecordStagingSourceProof? StagingSourceProof = null);

public sealed record NativeTerrainTexturePrivateOriginalRowProof(
    int TargetTextureId,
    int DonorWadEntry,
    int DonorTextureId,
    long SourceLowDetailWadOffset,
    long OutputLowDetailWadOffset,
    long SourceHighDetailWadOffset,
    long OutputHighDetailWadOffset,
    string LowDetailRowSha256,
    string HighDetailRowSha256,
    bool LowDetailRowChanged,
    bool HighDetailRowChanged,
    bool InstalledExactly);

/// <summary>
/// Equal-length, source-bound patch ready for conversion to TerrainPatch by a
/// higher-level exporter. One patch owns the complete fixed level-data subfile;
/// every other patch is a disjoint texture-page write from the global packer.
/// </summary>
public sealed record NativeTerrainTexturePrivateStructuralPatch(
    long WadOffset,
    int ByteLength,
    byte[] Before,
    byte[] After,
    string BeforeSha256,
    string AfterSha256,
    string Kind,
    string RuntimeKey,
    string Description);

public sealed record NativeTerrainTextureFixedTailPrivateRecordPlan(
    DateTimeOffset GeneratedAt,
    string SourceImagePath,
    string SourceImageSha256,
    string TargetLevelKey,
    string TargetLevelName,
    int TargetWadEntry,
    NativeTerrainTextureGlobalRepackSyntheticPlan GlobalPacking,
    NativeTerrainTextureRecordAppendPlan Append,
    IReadOnlyList<NativeTerrainTextureRecordExistingPatch> GeneratedOriginalRowPatches,
    IReadOnlyList<NativeTerrainTextureRecordExistingPatch> OrdinaryLevelDataPatches,
    IReadOnlyList<NativeTerrainTextureRecordRebasedPatchProof> OrdinaryPatchRebaseProofs,
    IReadOnlyList<NativeTerrainTexturePrivateOriginalRowProof> OriginalRowProofs,
    IReadOnlyList<NativeTerrainTexturePrivateStructuralPatch> CombinedPatches,
    int TexturePagePatchCount,
    int TexturePagePatchedByteCount,
    bool SourceBindingVerified,
    bool GlobalPackingProofComplete,
    bool OriginalRowsInstalledExactly,
    bool SyntheticRowsInstalledExactly,
    bool OrdinaryPatchesIncluded,
    bool PatchPreimagesVerified,
    bool CombinedPatchesDisjoint,
    bool FixedSubfileBoundaryPreserved,
    bool RequiresDuckStationRuntimeProof,
    IReadOnlyList<string> Notes);

public sealed record NativeTerrainTextureFixedTailPrivateRecordExportResult(
    NativeTerrainTextureFixedTailPrivateRecordPlan Plan,
    string OutputImagePath,
    string OutputCuePath,
    string OutputImageSha256,
    bool SourceImagePreserved,
    bool ExactPatchReadbackVerified,
    bool RuntimeTargetReadbackVerified,
    bool GlobalLogicalReadbackVerified,
    bool AtomicRenameCompleted);

/// <summary>
/// Joins the production-neutral global page packer and fixed-tail record append
/// builder. The global packer's complete final rows for existing movable
/// records become source-bound level-data patches; synthetic rows are appended;
/// ordinary level-data edits are rebased in the same complete-subfile image.
/// This type is intentionally separate from TerrainPatchExporter so integration
/// can remain gated on DuckStation evidence.
/// </summary>
public static class NativeTerrainTextureFixedTailPrivateRecordComposer
{
    private const int WadLba = 37;
    private const int TexturePagesSubfileIndex = 0;

    public static NativeTerrainTextureFixedTailPrivateRecordPlan BuildPlan(
        NativeTerrainTextureFixedTailPrivateRecordRequest request)
    {
        if (!TryBuild(request, out NativeTerrainTextureFixedTailPrivateRecordPlan? plan, out string failureReason) ||
            plan == null)
        {
            throw new InvalidOperationException(failureReason);
        }
        return plan;
    }

    public static bool TryBuild(
        NativeTerrainTextureFixedTailPrivateRecordRequest request,
        out NativeTerrainTextureFixedTailPrivateRecordPlan? plan,
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
            if (!File.Exists(request.SourceImagePath))
                throw new FileNotFoundException("Missing retail source image.", request.SourceImagePath);
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

            Dictionary<int, NativeTerrainTexturePrivateSyntheticRecord> identities = request.SyntheticRecords
                .ToDictionary(item => item.AssignedTextureId);
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

            NativeTerrainTextureRecordAppendRequest appendRequest = new(
                request.SourceImagePath,
                request.TargetLevel,
                request.SourceBinding,
                appendRecords,
                Array.Empty<NativeTerrainTextureRecordExistingPatch>(),
                request.StagingSourceProof);
            if (!NativeTerrainTextureRecordAppendBuilder.TryBuild(
                    appendRequest,
                    out NativeTerrainTextureRecordAppendPlan? provisional,
                    out failureReason) ||
                provisional == null)
            {
                failureReason = $"The private-record destination is not fixed-tail appendable: {failureReason}";
                return false;
            }
            if (!provisional.Patch.IsFixedLength)
            {
                failureReason = "A non-fixed append plan must use the sector-relocation composer.";
                return false;
            }
            if (global.SourceTextureCount != provisional.SourceTextureCount ||
                global.OutputTextureCount != provisional.OutputTextureCount)
            {
                failureReason = "The global packer and append builder disagree on source/output texture counts.";
                return false;
            }

            (long texturePagesWadOffset, int texturePagesByteLength) = ReadNestedSubfile(
                request.SourceImagePath,
                provisional.TargetEntryWadOffset,
                provisional.TargetEntryByteLength,
                TexturePagesSubfileIndex);
            if (!TryValidatePagePatches(
                    request.SourceImagePath,
                    texturePagesWadOffset,
                    texturePagesByteLength,
                    global,
                    provisional,
                    out failureReason))
            {
                return false;
            }

            List<NativeTerrainTextureRecordExistingPatch> generated = [];
            List<RowSource> rowSources = [];
            HashSet<int> originalIds = [];
            int sourceHighStart = checked(8 + (provisional.SourceTextureCount * NativeTerrainTextureRecordAppendBuilder.LowDetailRecordBytes));
            foreach (NativeTerrainTextureGlobalRepackPackedRecord row in global.OriginalMovableRecords.OrderBy(item => item.TargetTextureId))
            {
                if (row.TargetTextureId < 0 || row.TargetTextureId >= provisional.SourceTextureCount ||
                    !originalIds.Add(row.TargetTextureId))
                {
                    failureReason = $"Global packing returned invalid or duplicate original movable row T{row.TargetTextureId}.";
                    return false;
                }
                int lowRelative = checked(8 + (row.TargetTextureId * NativeTerrainTextureRecordAppendBuilder.LowDetailRecordBytes));
                int highRelative = checked(sourceHighStart + (row.TargetTextureId * NativeTerrainTextureRecordAppendBuilder.HighDetailRecordBytes));
                byte[] lowBefore = provisional.Patch.Before
                    .AsSpan(lowRelative, NativeTerrainTextureRecordAppendBuilder.LowDetailRecordBytes)
                    .ToArray();
                byte[] highBefore = provisional.Patch.Before
                    .AsSpan(highRelative, NativeTerrainTextureRecordAppendBuilder.HighDetailRecordBytes)
                    .ToArray();
                long lowWadOffset = provisional.LevelDataWadOffset + lowRelative;
                long highWadOffset = provisional.LevelDataWadOffset + highRelative;
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

            appendRequest = appendRequest with { ExistingLevelDataPatches = allExisting };
            if (!NativeTerrainTextureRecordAppendBuilder.TryBuild(
                    appendRequest,
                    out NativeTerrainTextureRecordAppendPlan? append,
                    out failureReason) ||
                append == null)
            {
                failureReason = $"Atomic row/ordinary-patch append composition failed: {failureReason}";
                return false;
            }
            if (!append.Patch.IsFixedLength ||
                !append.FixedSubfileBoundaryPreserved ||
                !append.ArchiveHeadersRequireNoFixup ||
                !append.ExistingPatchesRebasedExactly)
            {
                failureReason = "The fixed-tail append lost its subfile-boundary, archive-header, or patch-rebase proof.";
                return false;
            }

            List<NativeTerrainTextureRecordRebasedPatchProof> ordinaryRebaseProofs = [];
            foreach (NativeTerrainTextureRecordExistingPatch ordinary in request.OrdinaryLevelDataPatches)
            {
                string beforeSha = Sha256(ordinary.Before);
                string afterSha = Sha256(ordinary.After);
                NativeTerrainTextureRecordRebasedPatchProof[] matches = append.RebasedPatches
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
                        $"Ordinary retail-preimage patch {ordinary.Kind}/{ordinary.RuntimeKey} at WAD 0x{ordinary.WadOffset:X} did not produce exactly one source-bound rebase proof.";
                    return false;
                }
                ordinaryRebaseProofs.Add(matches[0]);
            }

            int outputHighStart = checked(8 + (append.OutputTextureCount * NativeTerrainTextureRecordAppendBuilder.LowDetailRecordBytes));
            NativeTerrainTexturePrivateOriginalRowProof[] originalProofs = rowSources
                .Select(source =>
                {
                    int lowRelative = checked(8 + (source.Row.TargetTextureId * NativeTerrainTextureRecordAppendBuilder.LowDetailRecordBytes));
                    int highRelative = checked(outputHighStart + (source.Row.TargetTextureId * NativeTerrainTextureRecordAppendBuilder.HighDetailRecordBytes));
                    bool exact = append.Patch.After
                        .AsSpan(lowRelative, NativeTerrainTextureRecordAppendBuilder.LowDetailRecordBytes)
                        .SequenceEqual(source.Row.LowDetailRow) &&
                        append.Patch.After
                            .AsSpan(highRelative, NativeTerrainTextureRecordAppendBuilder.HighDetailRecordBytes)
                            .SequenceEqual(source.Row.HighDetailRow);
                    return new NativeTerrainTexturePrivateOriginalRowProof(
                        source.Row.TargetTextureId,
                        source.Row.DonorWadEntry,
                        source.Row.DonorTextureId,
                        source.SourceLowWadOffset,
                        append.LevelDataWadOffset + lowRelative,
                        source.SourceHighWadOffset,
                        append.LevelDataWadOffset + highRelative,
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
                int lowRelative = checked(8 + (row.TargetTextureId * NativeTerrainTextureRecordAppendBuilder.LowDetailRecordBytes));
                int highRelative = checked(outputHighStart + (row.TargetTextureId * NativeTerrainTextureRecordAppendBuilder.HighDetailRecordBytes));
                return append.Patch.After
                           .AsSpan(lowRelative, NativeTerrainTextureRecordAppendBuilder.LowDetailRecordBytes)
                           .SequenceEqual(row.LowDetailRow) &&
                       append.Patch.After
                           .AsSpan(highRelative, NativeTerrainTextureRecordAppendBuilder.HighDetailRecordBytes)
                           .SequenceEqual(row.HighDetailRow);
            });
            if (!originalRowsInstalled || !syntheticRowsInstalled)
            {
                failureReason = "One or more global-pack existing/synthetic rows were not installed exactly in the composed level-data image.";
                return false;
            }

            List<NativeTerrainTexturePrivateStructuralPatch> combined = global.TexturePagePatches
                .Select((patch, index) => ToStructuralPatch(
                    patch.WadOffset,
                    patch.Before,
                    patch.After,
                    patch.Kind,
                    $"global-page-{index}",
                    patch.Description))
                .ToList();
            combined.Add(ToStructuralPatch(
                append.Patch.WadOffset,
                append.Patch.Before,
                append.Patch.After,
                "native-terrain-private-record-level-data",
                "private-record-level-data",
                "Atomically install globally packed existing rows, append private rows, rebase the complete level-data suffix, and include ordinary level-data edits."));
            NativeTerrainTexturePrivateStructuralPatch[] orderedCombined = combined
                .OrderBy(item => item.WadOffset)
                .ToArray();
            if (!TryValidateDisjointStructuralPatches(orderedCombined, out failureReason))
                return false;

            plan = new NativeTerrainTextureFixedTailPrivateRecordPlan(
                DateTimeOffset.UtcNow,
                Path.GetFullPath(request.SourceImagePath),
                request.SourceBinding.SourceImageSha256,
                request.TargetLevel.Key,
                request.TargetLevel.DisplayName,
                request.TargetLevel.SourceWadEntry,
                global,
                append,
                generated,
                request.OrdinaryLevelDataPatches.ToArray(),
                ordinaryRebaseProofs,
                originalProofs,
                orderedCombined,
                global.TexturePagePatches.Count,
                global.TexturePagePatches.Sum(item => item.ByteLength),
                true,
                true,
                true,
                true,
                append.RebasedPatches.Count == allExisting.Length,
                true,
                true,
                true,
                true,
                [
                    $"Global packing installed {originalProofs.Length} complete existing movable row(s) and {global.SyntheticRecords.Count} synthetic row(s).",
                    $"The combined fixed-length plan contains {global.TexturePagePatches.Count} source-bound page patch(es) plus one complete level-data patch; no ordinary level-data patch is emitted separately.",
                    $"All {ordinaryRebaseProofs.Count} ordinary level-data patch(es) were consumed from retail preimages and rebased before the complete level-data image was emitted.",
                    "This plan is structurally and logically checked but remains outside normal Create BIN until DuckStation runtime evidence is recorded."
                ]);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidDataException or InvalidOperationException or IOException or OverflowException)
        {
            failureReason = ex.Message;
            return false;
        }
    }

    public static async Task<NativeTerrainTextureFixedTailPrivateRecordExportResult> ExportAsync(
        NativeTerrainTextureFixedTailPrivateRecordPlan plan,
        string sourceCuePath,
        string outputPrefix,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPrefix);
        if (!File.Exists(plan.SourceImagePath))
            throw new FileNotFoundException("The plan's bound source BIN is unavailable.", plan.SourceImagePath);
        if (!HashEquals(Sha256File(plan.SourceImagePath), plan.SourceImageSha256))
            throw new InvalidDataException("The source BIN no longer matches the private-record plan's SHA-256 binding.");

        string outputImagePath = Path.GetFullPath(outputPrefix + ".bin");
        string outputCuePath = Path.GetFullPath(outputPrefix + ".cue");
        if (string.Equals(outputImagePath, Path.GetFullPath(plan.SourceImagePath), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The fixed-tail private-record output must not overwrite its bound source BIN.");
        Directory.CreateDirectory(Path.GetDirectoryName(outputImagePath) ?? ".");
        string temporaryImagePath = outputImagePath + $".{Guid.NewGuid():N}.tmp";
        string temporaryCuePath = outputCuePath + $".{Guid.NewGuid():N}.tmp";
        DeleteStale(outputImagePath);
        DeleteStale(outputCuePath);
        DeleteStale(temporaryImagePath);
        DeleteStale(temporaryCuePath);

        try
        {
            File.Copy(plan.SourceImagePath, temporaryImagePath, true);
            DiscLayout layout = DiscImage.DetectLayout(temporaryImagePath);
            await using (FileStream output = File.Open(temporaryImagePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
            {
                foreach (NativeTerrainTexturePrivateStructuralPatch patch in plan.CombinedPatches)
                {
                    byte[] before = DiscImage.ReadFileBytes(output, layout, WadLba, patch.WadOffset, patch.ByteLength);
                    if (!before.SequenceEqual(patch.Before) || !HashEquals(Sha256(before), patch.BeforeSha256))
                        throw new InvalidDataException($"Combined patch {patch.Kind}/{patch.RuntimeKey} no longer matches its source-bound before bytes.");
                    DiscImage.WriteFileBytes(output, layout, WadLba, patch.WadOffset, patch.After);
                }
                output.Flush(flushToDisk: true);
                VerifyPatchReadback(output, layout, plan.CombinedPatches);
                VerifyOrdinaryPatchReadback(output, layout, plan);
            }

            NativeTerrainTextureRuntimeControlAudit runtime =
                NativeTerrainTextureRuntimeControlScanner.Inspect(temporaryImagePath, new LevelDefinition
                {
                    Key = plan.TargetLevelKey,
                    DisplayName = plan.TargetLevelName,
                    SourceWadEntry = plan.TargetWadEntry
                });
            if (!runtime.Complete || runtime.TextureCount != plan.Append.OutputTextureCount ||
                plan.Append.ResolvedRecords.Any(item => !runtime.IsRuntimePersistentTarget(item.AssignedTextureId)))
            {
                throw new InvalidDataException("The fixed-tail private records failed runtime-control readback.");
            }
            NativeTerrainTextureGlobalRepackPackedRecord[] expectedRows = plan.GlobalPacking.OriginalMovableRecords
                .Concat(plan.GlobalPacking.SyntheticRecords)
                .OrderBy(item => item.TargetTextureId)
                .ToArray();
            if (!NativeTerrainTextureGlobalRepackerResearch.TryVerifyCandidateLogicalReadback(
                    temporaryImagePath,
                    new LevelDefinition
                    {
                        Key = plan.TargetLevelKey,
                        DisplayName = plan.TargetLevelName,
                        SourceWadEntry = plan.TargetWadEntry
                    },
                    expectedRows,
                    plan.SourceImagePath,
                    out string logicalFailure))
            {
                throw new InvalidDataException($"Global logical final-BIN readback failed: {logicalFailure}");
            }

            string cue = DiscImage.BuildCueText(sourceCuePath, Path.GetFileName(outputImagePath));
            await File.WriteAllTextAsync(temporaryCuePath, cue, Encoding.ASCII, cancellationToken);
            File.Move(temporaryImagePath, outputImagePath, overwrite: true);
            File.Move(temporaryCuePath, outputCuePath, overwrite: true);

            DiscLayout finalLayout = DiscImage.DetectLayout(outputImagePath);
            using (FileStream final = File.OpenRead(outputImagePath))
            {
                VerifyPatchReadback(final, finalLayout, plan.CombinedPatches);
                VerifyOrdinaryPatchReadback(final, finalLayout, plan);
            }
            if (!NativeTerrainTextureGlobalRepackerResearch.TryVerifyCandidateLogicalReadback(
                    outputImagePath,
                    new LevelDefinition
                    {
                        Key = plan.TargetLevelKey,
                        DisplayName = plan.TargetLevelName,
                        SourceWadEntry = plan.TargetWadEntry
                    },
                    expectedRows,
                    plan.SourceImagePath,
                    out string finalLogicalFailure))
            {
                throw new InvalidDataException($"Renamed final BIN failed global logical readback: {finalLogicalFailure}");
            }
            bool sourcePreserved = HashEquals(Sha256File(plan.SourceImagePath), plan.SourceImageSha256);
            if (!sourcePreserved)
                throw new InvalidDataException("Fixed-tail private-record export modified its source BIN.");
            return new NativeTerrainTextureFixedTailPrivateRecordExportResult(
                plan,
                outputImagePath,
                outputCuePath,
                Sha256File(outputImagePath),
                true,
                true,
                true,
                true,
                true);
        }
        catch
        {
            DeleteStale(outputImagePath);
            DeleteStale(outputCuePath);
            throw;
        }
        finally
        {
            DeleteStale(temporaryImagePath);
            DeleteStale(temporaryCuePath);
        }
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

    private static bool TryValidatePagePatches(
        string sourceImagePath,
        long pageWadOffset,
        int pageByteLength,
        NativeTerrainTextureGlobalRepackSyntheticPlan global,
        NativeTerrainTextureRecordAppendPlan append,
        out string failureReason)
    {
        failureReason = "";
        DiscLayout layout = DiscImage.DetectLayout(sourceImagePath);
        using FileStream source = File.OpenRead(sourceImagePath);
        byte[] pages = DiscImage.ReadFileBytes(source, layout, WadLba, pageWadOffset, pageByteLength);
        if (!HashEquals(Sha256(pages), global.PackingProof.TexturePagesSha256))
        {
            failureReason = "The source texture-pages subfile no longer matches the global packing SHA-256 preimage.";
            return false;
        }
        List<(long Start, long End)> ranges = [];
        foreach (NativeTerrainTextureRelocationPatch patch in global.TexturePagePatches.OrderBy(item => item.WadOffset))
        {
            if (patch.ByteLength <= 0 || patch.Before == null || patch.After == null ||
                patch.ByteLength != patch.Before.Length || patch.ByteLength != patch.After.Length ||
                patch.WadOffset < pageWadOffset || patch.WadOffset + patch.ByteLength > pageWadOffset + pageByteLength)
            {
                failureReason = $"Global page patch at WAD 0x{patch.WadOffset:X} is empty, unequal-length, or outside the target texture-pages subfile.";
                return false;
            }
            if (RangesOverlap(patch.WadOffset, patch.WadOffset + patch.ByteLength,
                    append.LevelDataWadOffset, append.LevelDataWadOffset + append.LevelDataByteLength))
            {
                failureReason = $"Global page patch at WAD 0x{patch.WadOffset:X} overlaps destination level data.";
                return false;
            }
            byte[] actual = DiscImage.ReadFileBytes(source, layout, WadLba, patch.WadOffset, patch.ByteLength);
            if (!actual.SequenceEqual(patch.Before))
            {
                failureReason = $"Global page patch at WAD 0x{patch.WadOffset:X} no longer matches source bytes.";
                return false;
            }
            ranges.Add((patch.WadOffset, patch.WadOffset + patch.ByteLength));
        }
        for (int index = 1; index < ranges.Count; index++)
        {
            if (ranges[index].Start < ranges[index - 1].End)
            {
                failureReason = "Global page patches overlap one another.";
                return false;
            }
        }
        return true;
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
            if (RangesOverlap(previous.WadOffset, previous.WadOffset + previous.Before.Length,
                    current.WadOffset, current.WadOffset + current.Before.Length))
            {
                failureReason =
                    $"Existing patches {previous.Kind}/{previous.RuntimeKey} and {current.Kind}/{current.RuntimeKey} overlap; global row ownership and ordinary edits must be disjoint.";
                return false;
            }
        }
        return true;
    }

    private static bool TryValidateDisjointStructuralPatches(
        IReadOnlyList<NativeTerrainTexturePrivateStructuralPatch> patches,
        out string failureReason)
    {
        failureReason = "";
        for (int index = 1; index < patches.Count; index++)
        {
            NativeTerrainTexturePrivateStructuralPatch previous = patches[index - 1];
            NativeTerrainTexturePrivateStructuralPatch current = patches[index];
            if (RangesOverlap(previous.WadOffset, previous.WadOffset + previous.ByteLength,
                    current.WadOffset, current.WadOffset + current.ByteLength))
            {
                failureReason = $"Combined structural patches {previous.Kind}/{previous.RuntimeKey} and {current.Kind}/{current.RuntimeKey} overlap.";
                return false;
            }
        }
        return true;
    }

    private static NativeTerrainTexturePrivateStructuralPatch ToStructuralPatch(
        long wadOffset,
        byte[] before,
        byte[] after,
        string kind,
        string runtimeKey,
        string description)
    {
        if (before == null || after == null || before.Length == 0 || before.Length != after.Length)
            throw new InvalidDataException($"Structural patch {kind}/{runtimeKey} must have equal non-empty before/after bytes.");
        byte[] clonedBefore = before.ToArray();
        byte[] clonedAfter = after.ToArray();
        return new NativeTerrainTexturePrivateStructuralPatch(
            wadOffset,
            clonedBefore.Length,
            clonedBefore,
            clonedAfter,
            Sha256(clonedBefore),
            Sha256(clonedAfter),
            kind,
            runtimeKey,
            description);
    }

    private static (long WadOffset, int ByteLength) ReadNestedSubfile(
        string sourceImagePath,
        long targetEntryWadOffset,
        int targetEntryByteLength,
        int subfileIndex)
    {
        DiscLayout layout = DiscImage.DetectLayout(sourceImagePath);
        using FileStream source = File.OpenRead(sourceImagePath);
        byte[] slot = DiscImage.ReadFileBytes(
            source,
            layout,
            WadLba,
            targetEntryWadOffset + (subfileIndex * 8L),
            8);
        int relativeOffset = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(slot.AsSpan(0, 4)));
        int length = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(slot.AsSpan(4, 4)));
        if (relativeOffset <= 0 || length <= 0 || (long)relativeOffset + length > targetEntryByteLength)
            throw new InvalidDataException($"Target WAD entry has no valid nested subfile {subfileIndex}.");
        return (targetEntryWadOffset + relativeOffset, length);
    }

    private static void VerifyPatchReadback(
        FileStream image,
        DiscLayout layout,
        IReadOnlyList<NativeTerrainTexturePrivateStructuralPatch> patches)
    {
        foreach (NativeTerrainTexturePrivateStructuralPatch patch in patches)
        {
            byte[] actual = DiscImage.ReadFileBytes(image, layout, WadLba, patch.WadOffset, patch.ByteLength);
            if (!actual.SequenceEqual(patch.After) || !HashEquals(Sha256(actual), patch.AfterSha256))
                throw new InvalidDataException($"Final readback failed for {patch.Kind}/{patch.RuntimeKey} at WAD 0x{patch.WadOffset:X}.");
        }
    }

    private static void VerifyOrdinaryPatchReadback(
        FileStream image,
        DiscLayout layout,
        NativeTerrainTextureFixedTailPrivateRecordPlan plan)
    {
        if (plan.OrdinaryLevelDataPatches.Count != plan.OrdinaryPatchRebaseProofs.Count)
            throw new InvalidDataException("Ordinary patch/rebase-proof counts disagree in the composed plan.");
        for (int index = 0; index < plan.OrdinaryLevelDataPatches.Count; index++)
        {
            NativeTerrainTextureRecordExistingPatch ordinary = plan.OrdinaryLevelDataPatches[index];
            NativeTerrainTextureRecordRebasedPatchProof proof = plan.OrdinaryPatchRebaseProofs[index];
            byte[] actual = DiscImage.ReadFileBytes(image, layout, WadLba, proof.OutputWadOffset, proof.ByteLength);
            if (!actual.SequenceEqual(ordinary.After) || !HashEquals(Sha256(actual), proof.AfterSha256))
            {
                throw new InvalidDataException(
                    $"Final BIN ordinary patch {ordinary.Kind}/{ordinary.RuntimeKey} was not installed at rebased WAD 0x{proof.OutputWadOffset:X}; applying it later at retail WAD 0x{proof.SourceWadOffset:X} is forbidden.");
            }
        }
    }

    private static bool RangesOverlap(long firstStart, long firstEnd, long secondStart, long secondEnd) =>
        firstStart < secondEnd && secondStart < firstEnd;

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

    private static void DeleteStale(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private sealed record RowSource(
        NativeTerrainTextureGlobalRepackPackedRecord Row,
        long SourceLowWadOffset,
        long SourceHighWadOffset,
        byte[] LowBefore,
        byte[] HighBefore);
}
