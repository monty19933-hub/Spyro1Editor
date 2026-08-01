using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Exporting;

public sealed record NativeTerrainTexturePrivateMultiLevelBatchRequest(
    string SourceImagePath,
    string SourceCuePath,
    string OutputPrefix,
    IReadOnlyList<NativeTerrainTexturePrivateRecordBatchPlan> LevelPlans,
    NativeTerrainTextureStructuralGrowthPolicy StructuralGrowthPolicy =
        NativeTerrainTextureStructuralGrowthPolicy.NormalChecked);

public sealed record NativeTerrainTexturePrivateAggregatePatch(
    long SourceWadOffset,
    long RelocatedWadOffset,
    int SourceWadEntry,
    int ByteLength,
    byte[] Before,
    byte[] After,
    string BeforeSha256,
    string AfterSha256,
    string Kind,
    string RuntimeKey,
    string Description);

public sealed record NativeTerrainTexturePrivateAggregateLevelDataWrite(
    string TargetLevelKey,
    int TargetWadEntry,
    long SourceWadOffset,
    long RelocatedWadOffset,
    int SourceByteLength,
    int OutputByteLength,
    byte[] Before,
    byte[] After,
    string BeforeSha256,
    string AfterSha256);

public sealed record NativeTerrainTexturePrivateMultiLevelBatchPlan(
    DateTimeOffset GeneratedAt,
    string SourceImagePath,
    string SourceImageSha256,
    string SourceCuePath,
    string OutputPrefix,
    string WadAnalysisPath,
    IReadOnlyList<NativeTerrainTexturePrivateRecordBatchPlan> LevelPlans,
    IReadOnlyList<NativeTerrainTexturePrivateStructuralPatch> CombinedPatches,
    IReadOnlyList<NativeTerrainTexturePrivateAggregatePatch> RelocatedPatches,
    IReadOnlyList<NativeTerrainTexturePrivateAggregateLevelDataWrite> ExpandedLevelDataWrites,
    int SectorRelocationDestinationCount,
    int WadGrowthBytes,
    int OriginalExecutableLba,
    int RelocatedExecutableLba,
    bool SourceBindingsVerified,
    bool DestinationWadEntriesUnique,
    bool FixedTailWritersOnly,
    bool AggregateWriterSelected,
    bool TextureOnlyWritersVerified,
    bool PatchPreimagesVerified,
    bool CombinedPatchesDisjoint,
    bool RelocatedPatchesDisjoint,
    bool RequiresDuckStationRuntimeProof,
    IReadOnlyList<string> Notes)
{
    public NativeTerrainTextureStructuralGrowthPolicy StructuralGrowthPolicy { get; init; } =
        NativeTerrainTextureStructuralGrowthPolicy.NormalChecked;

    public bool StaticResearchOnly =>
        NativeTerrainTextureRecordAppendBuilder.IsStaticResearchOnly(
            StructuralGrowthPolicy);
}

public sealed record NativeTerrainTexturePrivateMultiLevelBatchExportResult(
    NativeTerrainTexturePrivateMultiLevelBatchPlan Plan,
    string OutputImagePath,
    string OutputCuePath,
    string OutputImageSha256,
    bool SourceImagePreserved,
    bool ExactReadbackVerified,
    bool EveryRuntimeTargetReadbackVerified,
    bool EveryGlobalLogicalReadbackVerified,
    bool AtomicRenameCompleted);

/// <summary>
/// One atomic writer for two or more private-texture destinations.
/// Each per-level compiler plan remains bound to the same immutable retail BIN;
/// all preimages are checked before the first byte is written, then the disjoint
/// WAD-entry patches are installed into one temporary image and every level is
/// logically re-read before the final rename.
///
/// Fixed-tail and sector-growth destinations share one NativeSkyWadRelocator
/// transaction. A larger runtime-proven aggregate may contain ordinary
/// NormalChecked children and exact runtime-proven children at or below its
/// own checked growth ceiling; each destination is still validated against its
/// own policy. The explicit
/// static-research policy remains isolated and requires every child to use it.
/// Every equal-length patch is retained in immutable source coordinates and
/// remapped only after the aggregate entry-growth map is complete. No unrelated
/// image writer is accepted into this texture-only transaction.
/// </summary>
public static class NativeTerrainTexturePrivateMultiLevelBatchCompiler
{
    private const int WadLba = 37;
    private const int NestedArchiveHeaderBytes = 2048;
    private const int LevelDataSubfileIndex = 1;

    public static NativeTerrainTexturePrivateMultiLevelBatchPlan BuildPlan(
        NativeTerrainTexturePrivateMultiLevelBatchRequest request)
    {
        if (!TryBuild(request, out NativeTerrainTexturePrivateMultiLevelBatchPlan? plan, out string failure) ||
            plan == null)
        {
            throw new InvalidOperationException(failure);
        }
        return plan;
    }

    public static bool TryBuild(
        NativeTerrainTexturePrivateMultiLevelBatchRequest request,
        out NativeTerrainTexturePrivateMultiLevelBatchPlan? plan,
        out string failureReason)
    {
        plan = null;
        failureReason = "";
        try
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(request.LevelPlans);
            ArgumentException.ThrowIfNullOrWhiteSpace(request.SourceImagePath);
            ArgumentException.ThrowIfNullOrWhiteSpace(request.SourceCuePath);
            ArgumentException.ThrowIfNullOrWhiteSpace(request.OutputPrefix);
            _ = NativeTerrainTextureRecordAppendBuilder.GetMaximumSectorGrowthBytes(
                request.StructuralGrowthPolicy);
            if (!File.Exists(request.SourceImagePath))
                throw new FileNotFoundException("Missing source BIN for the multi-level private texture batch.", request.SourceImagePath);
            if (!File.Exists(request.SourceCuePath))
                throw new FileNotFoundException("Missing source CUE for the multi-level private texture batch.", request.SourceCuePath);
            if (request.LevelPlans.Count < 2)
                throw new InvalidOperationException("A multi-level private texture batch requires at least two destination-level plans.");

            string sourceImagePath = Path.GetFullPath(request.SourceImagePath);
            string sourceCuePath = Path.GetFullPath(request.SourceCuePath);
            string sourceSha256 = Sha256File(sourceImagePath);
            HashSet<string> levelKeys = new(StringComparer.OrdinalIgnoreCase);
            HashSet<int> wadEntries = [];
            List<NativeTerrainTexturePrivateStructuralPatch> combined = [];
            List<NativeTerrainTextureSectorPrivateRecordPlan> sectorPlans = [];
            foreach (NativeTerrainTexturePrivateRecordBatchPlan levelPlan in request.LevelPlans)
            {
                ArgumentNullException.ThrowIfNull(levelPlan);
                if (!IsLevelPolicyCompatible(
                        request.StructuralGrowthPolicy,
                        levelPlan.StructuralGrowthPolicy))
                {
                    throw new InvalidDataException(
                        $"Destination {levelPlan.TargetLevelKey} uses structural-growth policy {levelPlan.StructuralGrowthPolicy}, which is incompatible with aggregate policy {request.StructuralGrowthPolicy}. Static research cannot mix with normal release, and a NormalChecked aggregate cannot absorb an extended child.");
                }
                if (!levelKeys.Add(LevelCatalog.NormalizeKey(levelPlan.TargetLevelKey)))
                    throw new InvalidDataException($"Destination level '{levelPlan.TargetLevelKey}' appears more than once in the multi-level batch.");
                if (!wadEntries.Add(levelPlan.TargetWadEntry))
                    throw new InvalidDataException($"Multiple destination plans claim WAD entry {levelPlan.TargetWadEntry}.");
                if (!PathsEqual(levelPlan.SourceImagePath, sourceImagePath) ||
                    !PathsEqual(levelPlan.SourceCuePath, sourceCuePath) ||
                    !HashEquals(levelPlan.SourceImageSha256, sourceSha256))
                {
                    throw new InvalidDataException(
                        $"Destination {levelPlan.TargetLevelKey} is bound to a different source BIN/CUE or source SHA-256.");
                }
                if (!levelPlan.SourcePreimagesVerified ||
                    !levelPlan.ContractsUnique ||
                    !levelPlan.AppendedIdsContiguous ||
                    !levelPlan.CustomImageTexturePatchesExcluded ||
                    !levelPlan.ExclusiveImageWriterSelected)
                {
                    throw new InvalidDataException(
                        $"Destination {levelPlan.TargetLevelKey} is missing a required private-record compiler proof.");
                }

                if (levelPlan.WriterKind == NativeTerrainTexturePrivateImageWriterKind.FixedTail)
                {
                    NativeTerrainTextureFixedTailPrivateRecordPlan fixedPlan = levelPlan.FixedTailPlan
                        ?? throw new InvalidDataException(
                            $"Destination {levelPlan.TargetLevelKey} selected fixed-tail output without a fixed-tail plan.");
                    if (levelPlan.SectorRelocationPlan != null ||
                        fixedPlan.TargetWadEntry != levelPlan.TargetWadEntry ||
                        !fixedPlan.SourceBindingVerified ||
                        !fixedPlan.GlobalPackingProofComplete ||
                        !fixedPlan.OriginalRowsInstalledExactly ||
                        !fixedPlan.SyntheticRowsInstalledExactly ||
                        !fixedPlan.OrdinaryPatchesIncluded ||
                        !fixedPlan.PatchPreimagesVerified ||
                        !fixedPlan.CombinedPatchesDisjoint ||
                        !fixedPlan.FixedSubfileBoundaryPreserved ||
                        fixedPlan.CombinedPatches.Count == 0)
                    {
                        throw new InvalidDataException(
                            $"Destination {levelPlan.TargetLevelKey}'s fixed-tail plan is incomplete or disagrees with its WAD-entry identity.");
                    }
                    ValidateFixedTextureOnlyPlan(fixedPlan);
                    combined.AddRange(fixedPlan.CombinedPatches);
                    continue;
                }

                if (levelPlan.WriterKind != NativeTerrainTexturePrivateImageWriterKind.SectorRelocation)
                {
                    throw new InvalidOperationException(
                        $"Destination {levelPlan.TargetLevelKey} selected an unrelated or unsupported image writer. " +
                        $"The aggregate private-texture transaction accepts only fixed-tail and aligned texture-record writers authorized by {request.StructuralGrowthPolicy}.");
                }

                NativeTerrainTextureSectorPrivateRecordPlan sectorPlan = levelPlan.SectorRelocationPlan
                    ?? throw new InvalidDataException(
                        $"Destination {levelPlan.TargetLevelKey} selected sector relocation without a sector plan.");
                if (levelPlan.FixedTailPlan != null ||
                    sectorPlan.TargetWadEntry != levelPlan.TargetWadEntry ||
                    !sectorPlan.SourceBindingVerified ||
                    !sectorPlan.GlobalPackingProofComplete ||
                    !sectorPlan.OriginalRowsInstalledExactly ||
                    !sectorPlan.SyntheticRowsInstalledExactly ||
                    !sectorPlan.OrdinaryPatchesIncludedAndRelocated ||
                    !sectorPlan.TexturePagePatchesIncludedAndRelocated ||
                    !sectorPlan.StructuralSectorGrowthVerified ||
                    !IsSupportedSectorGrowth(
                        sectorPlan.Structural.SectorGrowthBytes,
                        levelPlan.StructuralGrowthPolicy) ||
                    sectorPlan.Structural.Append.Patch.OutputByteLength -
                        sectorPlan.Structural.Append.Patch.SourceByteLength !=
                            sectorPlan.Structural.SectorGrowthBytes ||
                    !sectorPlan.Structural.AppendSourceBindingVerified ||
                    !sectorPlan.Structural.ExistingLevelDataPatchesConsumed ||
                    !sectorPlan.Structural.ExternalPatchPreimagesVerified ||
                    !sectorPlan.Structural.RelocationOffsetsVerified ||
                    sectorPlan.StructuralRequest.ExternalPatches.Count !=
                        sectorPlan.Structural.RelocatedExternalPatches.Count)
                {
                    throw new InvalidDataException(
                        $"Destination {levelPlan.TargetLevelKey}'s sector plan is incomplete, exceeds the aligned growth authorized by its {levelPlan.StructuralGrowthPolicy} policy, or contains an unproven writer.");
                }
                if (!PathsEqual(sectorPlan.StructuralRequest.SourceImagePath, sourceImagePath) ||
                    !PathsEqual(sectorPlan.StructuralRequest.SourceCuePath, sourceCuePath) ||
                    string.IsNullOrWhiteSpace(sectorPlan.StructuralRequest.WadAnalysisPath) ||
                    !File.Exists(sectorPlan.StructuralRequest.WadAnalysisPath))
                {
                    throw new InvalidDataException(
                        $"Destination {levelPlan.TargetLevelKey}'s structural-growth request is not bound to this source BIN/CUE and WAD analysis.");
                }

                ValidateSectorTextureOnlyPlan(sectorPlan);
                combined.AddRange(sectorPlan.StructuralRequest.ExternalPatches.Select(ToStructuralPatch));
                sectorPlans.Add(sectorPlan);
            }

            NativeTerrainTexturePrivateStructuralPatch[] ordered = combined
                .OrderBy(patch => patch.WadOffset)
                .ThenBy(patch => patch.ByteLength)
                .ToArray();
            ValidateCombinedPatches(ordered, requireAtLeastOne: sectorPlans.Count == 0);
            VerifyPatchPreimages(sourceImagePath, ordered, expectedAfter: false);
            AggregateStructuralState aggregate = BuildAggregateStructuralState(
                sourceImagePath,
                request.LevelPlans,
                ordered,
                sectorPlans);
            VerifyLevelDataPreimages(
                sourceImagePath,
                aggregate.ExpandedLevelDataWrites,
                expectedAfter: false);
            if (!HashEquals(Sha256File(sourceImagePath), sourceSha256))
                throw new InvalidDataException("The source BIN changed while multi-level private texture preimages were checked.");

            plan = new NativeTerrainTexturePrivateMultiLevelBatchPlan(
                DateTimeOffset.UtcNow,
                sourceImagePath,
                sourceSha256,
                sourceCuePath,
                Path.GetFullPath(request.OutputPrefix),
                aggregate.WadAnalysisPath,
                request.LevelPlans.OrderBy(item => item.TargetWadEntry).ToArray(),
                ordered,
                aggregate.RelocatedPatches,
                aggregate.ExpandedLevelDataWrites,
                sectorPlans.Count,
                aggregate.Relocation?.WadGrowthBytes ?? 0,
                aggregate.Relocation?.OriginalExecutableLba ?? -1,
                aggregate.Relocation?.RelocatedExecutableLba ?? -1,
                true,
                true,
                sectorPlans.Count == 0,
                true,
                true,
                true,
                true,
                true,
                true,
                [
                    $"Compiled {request.LevelPlans.Count} destination WAD entries, including {sectorPlans.Count} checked sector-growth destination(s), into one disjoint texture-only transaction.",
                    NativeTerrainTextureRecordAppendBuilder.IsStaticResearchOnly(
                        request.StructuralGrowthPolicy)
                        ? $"Aggregate structural growth uses {request.StructuralGrowthPolicy}; every extended plan and output remains static-research-only."
                        : $"Aggregate structural growth policy {request.StructuralGrowthPolicy} authorizes checked aligned growth through +0x{NativeTerrainTextureRecordAppendBuilder.GetMaximumSectorGrowthBytes(request.StructuralGrowthPolicy):X} per destination.",
                    "Every per-level plan is bound to the same immutable source BIN/CUE and every before byte is checked before writing.",
                    sectorPlans.Count == 0
                        ? "No WAD growth is required; relocated patch offsets equal their immutable source offsets."
                        : $"NativeSkyWadRelocator owns one aggregate +0x{aggregate.Relocation!.WadGrowthBytes:X} WAD/ISO transaction and every patch is remapped from immutable source coordinates.",
                    "The final temporary and renamed BIN are logically re-read for every destination level.",
                    "Object, custom-image, sky, chest, and other unrelated writer plans are not accepted; they remain fail-closed until their source patches can join this aggregate rebase map.",
                    "DuckStation gameplay proof is still required for the combined artifact."
                ])
            {
                StructuralGrowthPolicy = request.StructuralGrowthPolicy
            };
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidDataException or InvalidOperationException or IOException or OverflowException)
        {
            failureReason = ex.Message;
            return false;
        }
    }

    private static void ValidateFixedTextureOnlyPlan(
        NativeTerrainTextureFixedTailPrivateRecordPlan plan)
    {
        if (plan.CombinedPatches.Count != plan.GlobalPacking.TexturePagePatches.Count + 1)
        {
            throw new InvalidDataException(
                $"Destination {plan.TargetLevelKey}'s fixed-tail transaction contains a writer outside its global texture pages and complete level-data payload.");
        }
        NativeTerrainTexturePrivateStructuralPatch[] levelDataMatches = plan.CombinedPatches
            .Where(patch =>
                patch.WadOffset == plan.Append.Patch.WadOffset &&
                patch.ByteLength == plan.Append.Patch.SourceByteLength &&
                patch.Before.SequenceEqual(plan.Append.Patch.Before) &&
                patch.After.SequenceEqual(plan.Append.Patch.After))
            .ToArray();
        if (levelDataMatches.Length != 1 || plan.GlobalPacking.TexturePagePatches.Any(page =>
                plan.CombinedPatches.Count(patch => StructuralPatchMatchesPage(patch, page)) != 1))
        {
            throw new InvalidDataException(
                $"Destination {plan.TargetLevelKey}'s fixed-tail patch set is not exactly its checked texture-page writes plus one complete level-data write.");
        }
    }

    private static void ValidateSectorTextureOnlyPlan(
        NativeTerrainTextureSectorPrivateRecordPlan plan)
    {
        IReadOnlyList<NativeTerrainTextureRecordRelocationExternalPatch> external =
            plan.StructuralRequest.ExternalPatches;
        if (external.Count != plan.GlobalPacking.TexturePagePatches.Count ||
            plan.GlobalPacking.TexturePagePatches.Any(page =>
                external.Count(patch => ExternalPatchMatchesPage(patch, page)) != 1))
        {
            throw new InvalidDataException(
                $"Destination {plan.TargetLevelKey}'s +0x{plan.Structural.SectorGrowthBytes:X} transaction contains an external writer outside its checked global texture pages.");
        }

        NativeTerrainTextureRecordExistingPatch[] expectedExisting = plan.GeneratedOriginalRowPatches
            .Concat(plan.OrdinaryLevelDataPatches)
            .OrderBy(patch => patch.WadOffset)
            .ThenBy(patch => patch.Before.Length)
            .ToArray();
        NativeTerrainTextureRecordExistingPatch[] structuralExisting = plan.StructuralRequest.ExistingLevelDataPatches
            .OrderBy(patch => patch.WadOffset)
            .ThenBy(patch => patch.Before.Length)
            .ToArray();
        if (expectedExisting.Length != structuralExisting.Length ||
            !expectedExisting.Zip(structuralExisting).All(pair => ExistingPatchesEqual(pair.First, pair.Second)))
        {
            throw new InvalidDataException(
                $"Destination {plan.TargetLevelKey}'s +0x{plan.Structural.SectorGrowthBytes:X} transaction contains a level-data writer that was not consumed by the checked append builder.");
        }
    }

    private static bool StructuralPatchMatchesPage(
        NativeTerrainTexturePrivateStructuralPatch patch,
        NativeTerrainTextureRelocationPatch page) =>
        patch.WadOffset == page.WadOffset &&
        patch.ByteLength == page.ByteLength &&
        patch.Before.SequenceEqual(page.Before) &&
        patch.After.SequenceEqual(page.After) &&
        string.Equals(patch.Kind, page.Kind, StringComparison.Ordinal) &&
        string.Equals(patch.Description, page.Description, StringComparison.Ordinal);

    private static bool ExternalPatchMatchesPage(
        NativeTerrainTextureRecordRelocationExternalPatch patch,
        NativeTerrainTextureRelocationPatch page) =>
        patch.SourceWadOffset == page.WadOffset &&
        patch.Before.Length == page.ByteLength &&
        patch.Before.SequenceEqual(page.Before) &&
        patch.After.SequenceEqual(page.After) &&
        string.Equals(patch.Kind, page.Kind, StringComparison.Ordinal) &&
        string.Equals(patch.Description, page.Description, StringComparison.Ordinal);

    private static bool ExistingPatchesEqual(
        NativeTerrainTextureRecordExistingPatch left,
        NativeTerrainTextureRecordExistingPatch right) =>
        left.WadOffset == right.WadOffset &&
        left.Before.SequenceEqual(right.Before) &&
        left.After.SequenceEqual(right.After) &&
        string.Equals(left.Kind, right.Kind, StringComparison.Ordinal) &&
        string.Equals(left.RuntimeKey, right.RuntimeKey, StringComparison.Ordinal);

    private static AggregateStructuralState BuildAggregateStructuralState(
        string sourceImagePath,
        IReadOnlyList<NativeTerrainTexturePrivateRecordBatchPlan> levelPlans,
        IReadOnlyList<NativeTerrainTexturePrivateStructuralPatch> sourcePatches,
        IReadOnlyList<NativeTerrainTextureSectorPrivateRecordPlan> sectorPlans)
    {
        if (levelPlans.Count < 2)
            throw new InvalidOperationException("An aggregate private-texture transaction requires at least two destination plans.");

        if (sectorPlans.Count == 0)
        {
            NativeTerrainTexturePrivateAggregatePatch[] unchangedOffsets = sourcePatches
                .Select(patch => new NativeTerrainTexturePrivateAggregatePatch(
                    patch.WadOffset,
                    patch.WadOffset,
                    -1,
                    patch.ByteLength,
                    patch.Before.ToArray(),
                    patch.After.ToArray(),
                    patch.BeforeSha256,
                    patch.AfterSha256,
                    patch.Kind,
                    patch.RuntimeKey,
                    patch.Description))
                .ToArray();
            ValidateRelocatedWrites(unchangedOffsets, []);
            return new AggregateStructuralState(
                "",
                null,
                [],
                unchangedOffsets,
                []);
        }

        string wadAnalysisPath = Path.GetFullPath(sectorPlans[0].StructuralRequest.WadAnalysisPath);
        if (!File.Exists(wadAnalysisPath) || sectorPlans.Any(item =>
                !PathsEqual(item.StructuralRequest.WadAnalysisPath, wadAnalysisPath)))
        {
            throw new InvalidDataException(
                "Every sector-growth destination in one aggregate transaction must use the same source-bound WAD analysis.");
        }

        ArchiveEntryMap[] entries = LoadWadEntries(wadAnalysisPath);
        List<EntryInsertion> insertions = [];
        List<NativeSkyRelocationPayload> payloads = [];
        for (int index = 0; index < sectorPlans.Count; index++)
        {
            NativeTerrainTextureSectorPrivateRecordPlan sector = sectorPlans[index];
            NativeTerrainTextureRecordAppendPlan append = sector.Structural.Append;
            ArchiveEntryMap entry = entries.SingleOrDefault(item => item.Index == sector.TargetWadEntry)
                ?? throw new InvalidDataException(
                    $"WAD analysis is missing destination entry {sector.TargetWadEntry} for {sector.TargetLevelKey}.");
            if (append.TargetWadEntry != entry.Index ||
                append.TargetEntryWadOffset != entry.Offset ||
                append.LevelDataWadOffset < entry.Offset + NestedArchiveHeaderBytes ||
                append.LevelDataWadOffset + append.Patch.SourceByteLength > entry.EndExclusive ||
                append.Patch.WadOffset != append.LevelDataWadOffset ||
                append.Patch.SourceByteLength != append.LevelDataByteLength ||
                append.Patch.OutputByteLength - append.Patch.SourceByteLength != sector.Structural.SectorGrowthBytes)
            {
                throw new InvalidDataException(
                    $"Destination {sector.TargetLevelKey}'s append layout no longer matches its immutable WAD-entry boundaries.");
            }

            long insertionRelative = checked(
                append.LevelDataWadOffset - entry.Offset + append.Patch.SourceByteLength);
            insertions.Add(new EntryInsertion(
                entry.Index,
                insertionRelative,
                sector.Structural.SectorGrowthBytes,
                sector.TargetLevelKey));
            payloads.Add(new NativeSkyRelocationPayload(
                PatchIndex: index,
                WadLba: WadLba,
                OriginalWadOffset: append.LevelDataWadOffset + append.Patch.SourceByteLength,
                StorageWadEntry: entry.Index,
                ModelBlockOffset: append.Patch.SourceByteLength,
                OriginalLength: 0,
                Bytes: new byte[sector.Structural.SectorGrowthBytes],
                SubfileIndex: LevelDataSubfileIndex,
                RequireLengthPrefix: false));
        }

        NativeSkyWadRelocationPlan relocation = NativeSkyWadRelocator.BuildPlan(
            sourceImagePath,
            wadAnalysisPath,
            payloads);
        int expectedGrowth = checked(sectorPlans.Sum(item => item.Structural.SectorGrowthBytes));
        if (!relocation.Required ||
            relocation.WadLba != WadLba ||
            relocation.WadGrowthBytes != expectedGrowth ||
            relocation.RelocatedPatches.Count != payloads.Count ||
            sectorPlans.Any(item =>
                relocation.EntryGrowthBytes.GetValueOrDefault(item.TargetWadEntry) !=
                    item.Structural.SectorGrowthBytes))
        {
            throw new InvalidDataException(
                "NativeSkyWadRelocator did not reproduce the complete aggregate destination-growth map.");
        }

        NativeTerrainTexturePrivateAggregateLevelDataWrite[] expandedWrites = sectorPlans
            .Select(sector =>
            {
                NativeTerrainTextureRecordAppendPlan append = sector.Structural.Append;
                ArchiveEntryMap entry = entries.Single(item => item.Index == sector.TargetWadEntry);
                long relocatedEntry = relocation.RelocatedEntryOffsets[entry.Index];
                long relocatedLevelData = checked(
                    relocatedEntry + append.LevelDataWadOffset - entry.Offset);
                return new NativeTerrainTexturePrivateAggregateLevelDataWrite(
                    LevelCatalog.NormalizeKey(sector.TargetLevelKey),
                    entry.Index,
                    append.LevelDataWadOffset,
                    relocatedLevelData,
                    append.Patch.SourceByteLength,
                    append.Patch.OutputByteLength,
                    append.Patch.Before.ToArray(),
                    append.Patch.After.ToArray(),
                    append.Patch.BeforeSha256,
                    append.Patch.AfterSha256);
            })
            .OrderBy(item => item.RelocatedWadOffset)
            .ToArray();

        foreach (NativeTerrainTexturePrivateStructuralPatch patch in sourcePatches)
        {
            foreach (NativeTerrainTexturePrivateAggregateLevelDataWrite write in expandedWrites)
            {
                if (RangesOverlap(
                        patch.WadOffset,
                        patch.WadOffset + patch.ByteLength,
                        write.SourceWadOffset,
                        write.SourceWadOffset + write.SourceByteLength))
                {
                    throw new InvalidDataException(
                        $"Source patch {patch.Kind}/{patch.RuntimeKey} overlaps {write.TargetLevelKey}'s complete level-data transaction. " +
                        "That writer must be folded into ExistingLevelDataPatches before aggregate relocation.");
                }
            }
        }

        NativeTerrainTexturePrivateAggregatePatch[] relocatedPatches = sourcePatches
            .Select(patch => MapPatch(patch, entries, insertions, relocation))
            .OrderBy(item => item.RelocatedWadOffset)
            .ThenBy(item => item.ByteLength)
            .ToArray();
        ValidateRelocatedWrites(relocatedPatches, expandedWrites);
        return new AggregateStructuralState(
            wadAnalysisPath,
            relocation,
            payloads,
            relocatedPatches,
            expandedWrites);
    }

    private static NativeTerrainTexturePrivateAggregatePatch MapPatch(
        NativeTerrainTexturePrivateStructuralPatch patch,
        IReadOnlyList<ArchiveEntryMap> entries,
        IReadOnlyList<EntryInsertion> insertions,
        NativeSkyWadRelocationPlan relocation)
    {
        ArchiveEntryMap entry = entries.SingleOrDefault(candidate =>
                patch.WadOffset >= candidate.Offset &&
                patch.WadOffset + patch.ByteLength <= candidate.EndExclusive)
            ?? throw new InvalidDataException(
                $"Source patch {patch.Kind}/{patch.RuntimeKey} at WAD 0x{patch.WadOffset:X} is outside one analyzed WAD entry.");
        long relativeStart = patch.WadOffset - entry.Offset;
        long relativeEnd = checked(relativeStart + patch.ByteLength);
        if (relativeStart < NestedArchiveHeaderBytes)
        {
            throw new InvalidDataException(
                $"Source patch {patch.Kind}/{patch.RuntimeKey} enters WAD entry {entry.Index}'s structural header.");
        }

        long internalShift = 0;
        foreach (EntryInsertion insertion in insertions
                     .Where(item => item.WadEntry == entry.Index)
                     .OrderBy(item => item.EntryRelativeOffset))
        {
            if (relativeEnd <= insertion.EntryRelativeOffset)
                continue;
            if (relativeStart >= insertion.EntryRelativeOffset)
            {
                internalShift = checked(internalShift + insertion.GrowthBytes);
                continue;
            }
            throw new InvalidDataException(
                $"Source patch {patch.Kind}/{patch.RuntimeKey} crosses {insertion.TargetLevelKey}'s +0x{insertion.GrowthBytes:X} insertion point.");
        }

        long relocatedWadOffset = checked(
            relocation.RelocatedEntryOffsets[entry.Index] + relativeStart + internalShift);
        return new NativeTerrainTexturePrivateAggregatePatch(
            patch.WadOffset,
            relocatedWadOffset,
            entry.Index,
            patch.ByteLength,
            patch.Before.ToArray(),
            patch.After.ToArray(),
            patch.BeforeSha256,
            patch.AfterSha256,
            patch.Kind,
            patch.RuntimeKey,
            patch.Description);
    }

    private static void ValidateRelocatedWrites(
        IReadOnlyList<NativeTerrainTexturePrivateAggregatePatch> patches,
        IReadOnlyList<NativeTerrainTexturePrivateAggregateLevelDataWrite> expandedWrites)
    {
        for (int index = 0; index < patches.Count; index++)
        {
            NativeTerrainTexturePrivateAggregatePatch patch = patches[index];
            if (patch.RelocatedWadOffset < 0 ||
                patch.ByteLength <= 0 ||
                patch.Before.Length != patch.ByteLength ||
                patch.After.Length != patch.ByteLength ||
                !HashEquals(Sha256(patch.Before), patch.BeforeSha256) ||
                !HashEquals(Sha256(patch.After), patch.AfterSha256))
            {
                throw new InvalidDataException(
                    $"Relocated patch {patch.Kind}/{patch.RuntimeKey} is empty, unequal-length, or hash-inconsistent.");
            }
            if (index > 0)
            {
                NativeTerrainTexturePrivateAggregatePatch previous = patches[index - 1];
                if (RangesOverlap(
                        previous.RelocatedWadOffset,
                        previous.RelocatedWadOffset + previous.ByteLength,
                        patch.RelocatedWadOffset,
                        patch.RelocatedWadOffset + patch.ByteLength))
                {
                    throw new InvalidDataException(
                        $"Relocated patches {previous.Kind}/{previous.RuntimeKey} and {patch.Kind}/{patch.RuntimeKey} overlap.");
                }
            }
        }

        for (int index = 0; index < expandedWrites.Count; index++)
        {
            NativeTerrainTexturePrivateAggregateLevelDataWrite write = expandedWrites[index];
            if (write.RelocatedWadOffset < 0 ||
                write.SourceByteLength <= 0 ||
                write.OutputByteLength <= write.SourceByteLength ||
                write.Before.Length != write.SourceByteLength ||
                write.After.Length != write.OutputByteLength ||
                !HashEquals(Sha256(write.Before), write.BeforeSha256) ||
                !HashEquals(Sha256(write.After), write.AfterSha256))
            {
                throw new InvalidDataException(
                    $"Expanded level-data write for {write.TargetLevelKey} is empty, non-growing, or hash-inconsistent.");
            }
            if (index > 0)
            {
                NativeTerrainTexturePrivateAggregateLevelDataWrite previous = expandedWrites[index - 1];
                if (RangesOverlap(
                        previous.RelocatedWadOffset,
                        previous.RelocatedWadOffset + previous.OutputByteLength,
                        write.RelocatedWadOffset,
                        write.RelocatedWadOffset + write.OutputByteLength))
                {
                    throw new InvalidDataException(
                        $"Expanded level-data writes for {previous.TargetLevelKey} and {write.TargetLevelKey} overlap.");
                }
            }
            foreach (NativeTerrainTexturePrivateAggregatePatch patch in patches)
            {
                if (RangesOverlap(
                        patch.RelocatedWadOffset,
                        patch.RelocatedWadOffset + patch.ByteLength,
                        write.RelocatedWadOffset,
                        write.RelocatedWadOffset + write.OutputByteLength))
                {
                    throw new InvalidDataException(
                        $"Relocated patch {patch.Kind}/{patch.RuntimeKey} overlaps {write.TargetLevelKey}'s expanded level-data write.");
                }
            }
        }
    }

    private static void ValidateAggregateMatchesPlan(
        NativeTerrainTexturePrivateMultiLevelBatchPlan plan,
        AggregateStructuralState aggregate)
    {
        int sectorCount = plan.LevelPlans.Count(item =>
            item.WriterKind == NativeTerrainTexturePrivateImageWriterKind.SectorRelocation);
        if (plan.SectorRelocationDestinationCount != sectorCount ||
            plan.FixedTailWritersOnly != (sectorCount == 0) ||
            plan.WadGrowthBytes != (aggregate.Relocation?.WadGrowthBytes ?? 0) ||
            plan.OriginalExecutableLba != (aggregate.Relocation?.OriginalExecutableLba ?? -1) ||
            plan.RelocatedExecutableLba != (aggregate.Relocation?.RelocatedExecutableLba ?? -1) ||
            !OptionalPathsEqual(plan.WadAnalysisPath, aggregate.WadAnalysisPath) ||
            !AggregatePatchesEqual(plan.RelocatedPatches, aggregate.RelocatedPatches) ||
            !LevelDataWritesEqual(plan.ExpandedLevelDataWrites, aggregate.ExpandedLevelDataWrites))
        {
            throw new InvalidDataException(
                "The checked aggregate private-texture plan no longer matches its rebuilt relocation transaction.");
        }
    }

    private static bool AggregatePatchesEqual(
        IReadOnlyList<NativeTerrainTexturePrivateAggregatePatch> left,
        IReadOnlyList<NativeTerrainTexturePrivateAggregatePatch> right) =>
        left.Count == right.Count && left.Zip(right).All(pair =>
            pair.First.SourceWadOffset == pair.Second.SourceWadOffset &&
            pair.First.RelocatedWadOffset == pair.Second.RelocatedWadOffset &&
            pair.First.SourceWadEntry == pair.Second.SourceWadEntry &&
            pair.First.ByteLength == pair.Second.ByteLength &&
            pair.First.Before.SequenceEqual(pair.Second.Before) &&
            pair.First.After.SequenceEqual(pair.Second.After) &&
            HashEquals(pair.First.BeforeSha256, pair.Second.BeforeSha256) &&
            HashEquals(pair.First.AfterSha256, pair.Second.AfterSha256) &&
            string.Equals(pair.First.Kind, pair.Second.Kind, StringComparison.Ordinal) &&
            string.Equals(pair.First.RuntimeKey, pair.Second.RuntimeKey, StringComparison.Ordinal));

    private static bool LevelDataWritesEqual(
        IReadOnlyList<NativeTerrainTexturePrivateAggregateLevelDataWrite> left,
        IReadOnlyList<NativeTerrainTexturePrivateAggregateLevelDataWrite> right) =>
        left.Count == right.Count && left.Zip(right).All(pair =>
            string.Equals(pair.First.TargetLevelKey, pair.Second.TargetLevelKey, StringComparison.OrdinalIgnoreCase) &&
            pair.First.TargetWadEntry == pair.Second.TargetWadEntry &&
            pair.First.SourceWadOffset == pair.Second.SourceWadOffset &&
            pair.First.RelocatedWadOffset == pair.Second.RelocatedWadOffset &&
            pair.First.SourceByteLength == pair.Second.SourceByteLength &&
            pair.First.OutputByteLength == pair.Second.OutputByteLength &&
            pair.First.Before.SequenceEqual(pair.Second.Before) &&
            pair.First.After.SequenceEqual(pair.Second.After) &&
            HashEquals(pair.First.BeforeSha256, pair.Second.BeforeSha256) &&
            HashEquals(pair.First.AfterSha256, pair.Second.AfterSha256));

    public static async Task<NativeTerrainTexturePrivateMultiLevelBatchExportResult> ExportAsync(
        NativeTerrainTexturePrivateMultiLevelBatchPlan plan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (!plan.SourceBindingsVerified ||
            !plan.DestinationWadEntriesUnique ||
            !plan.AggregateWriterSelected ||
            !plan.TextureOnlyWritersVerified ||
            !plan.PatchPreimagesVerified ||
            !plan.CombinedPatchesDisjoint ||
            !plan.RelocatedPatchesDisjoint ||
            plan.LevelPlans.Count < 2)
        {
            throw new InvalidOperationException("The multi-level private texture batch is missing a required source, destination, writer, or patch proof.");
        }
        if (!File.Exists(plan.SourceImagePath) ||
            !HashEquals(Sha256File(plan.SourceImagePath), plan.SourceImageSha256))
        {
            throw new InvalidDataException("The source BIN no longer matches the checked multi-level private texture batch.");
        }

        string outputImagePath = plan.OutputPrefix + ".bin";
        string outputCuePath = plan.OutputPrefix + ".cue";
        if (PathsEqual(outputImagePath, plan.SourceImagePath))
            throw new InvalidOperationException("The multi-level private texture output BIN must not overwrite its bound source BIN.");
        Directory.CreateDirectory(Path.GetDirectoryName(outputImagePath) ?? ".");
        string temporaryImagePath = outputImagePath + $".{Guid.NewGuid():N}.tmp";
        string temporaryCuePath = outputCuePath + $".{Guid.NewGuid():N}.tmp";
        DeleteStale(outputImagePath);
        DeleteStale(outputCuePath);
        DeleteStale(temporaryImagePath);
        DeleteStale(temporaryCuePath);
        try
        {
            NativeTerrainTextureSectorPrivateRecordPlan[] sectorPlans = plan.LevelPlans
                .Where(item => item.WriterKind == NativeTerrainTexturePrivateImageWriterKind.SectorRelocation)
                .Select(item => item.SectorRelocationPlan
                    ?? throw new InvalidDataException($"Destination {item.TargetLevelKey} lost its sector plan."))
                .ToArray();
            AggregateStructuralState aggregate = BuildAggregateStructuralState(
                plan.SourceImagePath,
                plan.LevelPlans,
                plan.CombinedPatches,
                sectorPlans);
            ValidateAggregateMatchesPlan(plan, aggregate);
            VerifyPatchPreimages(plan.SourceImagePath, plan.CombinedPatches, expectedAfter: false);
            VerifyLevelDataPreimages(
                plan.SourceImagePath,
                aggregate.ExpandedLevelDataWrites,
                expectedAfter: false);

            cancellationToken.ThrowIfCancellationRequested();
            if (aggregate.Relocation != null)
            {
                NativeSkyWadRelocator.WriteExpandedImage(
                    plan.SourceImagePath,
                    temporaryImagePath,
                    aggregate.WadAnalysisPath,
                    aggregate.Relocation,
                    aggregate.Payloads);
            }
            else
            {
                File.Copy(plan.SourceImagePath, temporaryImagePath, true);
            }

            VerifyPreparedLevelData(temporaryImagePath, aggregate.ExpandedLevelDataWrites);
            VerifyRelocatedPatchReadback(
                temporaryImagePath,
                aggregate.RelocatedPatches,
                expectedAfter: false);
            DiscLayout layout = DiscImage.DetectLayout(temporaryImagePath);
            await using (FileStream output = File.Open(temporaryImagePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
            {
                foreach (NativeTerrainTexturePrivateAggregateLevelDataWrite write in
                         aggregate.ExpandedLevelDataWrites)
                {
                    DiscImage.WriteFileBytes(
                        output,
                        layout,
                        WadLba,
                        write.RelocatedWadOffset,
                        write.After);
                }
                foreach (NativeTerrainTexturePrivateAggregatePatch patch in aggregate.RelocatedPatches)
                {
                    DiscImage.WriteFileBytes(
                        output,
                        layout,
                        WadLba,
                        patch.RelocatedWadOffset,
                        patch.After);
                }
                output.Flush(flushToDisk: true);
            }
            VerifyRelocatedPatchReadback(
                temporaryImagePath,
                aggregate.RelocatedPatches,
                expectedAfter: true);
            VerifyLevelDataPreimages(
                temporaryImagePath,
                aggregate.ExpandedLevelDataWrites,
                expectedAfter: true);
            VerifyEveryLevel(temporaryImagePath, plan);

            string cue = DiscImage.BuildCueText(plan.SourceCuePath, Path.GetFileName(outputImagePath));
            await File.WriteAllTextAsync(temporaryCuePath, cue, Encoding.ASCII, cancellationToken);
            File.Move(temporaryImagePath, outputImagePath, overwrite: true);
            File.Move(temporaryCuePath, outputCuePath, overwrite: true);

            VerifyRelocatedPatchReadback(
                outputImagePath,
                aggregate.RelocatedPatches,
                expectedAfter: true);
            VerifyLevelDataPreimages(
                outputImagePath,
                aggregate.ExpandedLevelDataWrites,
                expectedAfter: true);
            VerifyEveryLevel(outputImagePath, plan);
            if (!HashEquals(Sha256File(plan.SourceImagePath), plan.SourceImageSha256))
                throw new InvalidDataException("Multi-level private texture export modified its source BIN.");
            return new NativeTerrainTexturePrivateMultiLevelBatchExportResult(
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

    private static void VerifyEveryLevel(
        string candidateImagePath,
        NativeTerrainTexturePrivateMultiLevelBatchPlan plan)
    {
        foreach (NativeTerrainTexturePrivateRecordBatchPlan levelPlan in plan.LevelPlans)
        {
            NativeTerrainTextureRecordAppendPlan append;
            NativeTerrainTextureGlobalRepackSyntheticPlan globalPacking;
            string targetLevelName;
            if (levelPlan.WriterKind == NativeTerrainTexturePrivateImageWriterKind.FixedTail)
            {
                NativeTerrainTextureFixedTailPrivateRecordPlan fixedPlan = levelPlan.FixedTailPlan
                    ?? throw new InvalidDataException($"Destination {levelPlan.TargetLevelKey} lost its fixed-tail plan.");
                append = fixedPlan.Append;
                globalPacking = fixedPlan.GlobalPacking;
                targetLevelName = fixedPlan.TargetLevelName;
            }
            else if (levelPlan.WriterKind == NativeTerrainTexturePrivateImageWriterKind.SectorRelocation)
            {
                NativeTerrainTextureSectorPrivateRecordPlan sectorPlan = levelPlan.SectorRelocationPlan
                    ?? throw new InvalidDataException($"Destination {levelPlan.TargetLevelKey} lost its sector plan.");
                append = sectorPlan.Structural.Append;
                globalPacking = sectorPlan.GlobalPacking;
                targetLevelName = sectorPlan.TargetLevelName;
            }
            else
            {
                throw new InvalidDataException(
                    $"Destination {levelPlan.TargetLevelKey} selected an unrelated writer during final readback.");
            }

            LevelDefinition level = new()
            {
                Key = levelPlan.TargetLevelKey,
                DisplayName = targetLevelName,
                SourceWadEntry = levelPlan.TargetWadEntry
            };
            NativeTerrainTextureRuntimeControlAudit runtime =
                NativeTerrainTextureRuntimeControlScanner.Inspect(candidateImagePath, level);
            if (!runtime.Complete ||
                runtime.TextureCount != append.OutputTextureCount ||
                append.ResolvedRecords.Any(record =>
                    !runtime.IsRuntimePersistentTarget(record.AssignedTextureId)))
            {
                throw new InvalidDataException(
                    $"Destination {levelPlan.TargetLevelKey} failed multi-level runtime-control readback.");
            }

            NativeTerrainTextureGlobalRepackPackedRecord[] expectedRows = globalPacking.OriginalMovableRecords
                .Concat(globalPacking.SyntheticRecords)
                .OrderBy(row => row.TargetTextureId)
                .ToArray();
            if (!NativeTerrainTextureGlobalRepackerResearch.TryVerifyCandidateLogicalReadback(
                    candidateImagePath,
                    level,
                    expectedRows,
                    plan.SourceImagePath,
                    out string logicalFailure))
            {
                throw new InvalidDataException(
                    $"Destination {levelPlan.TargetLevelKey} failed multi-level global logical readback: {logicalFailure}");
            }
        }
    }

    private static void ValidateCombinedPatches(
        IReadOnlyList<NativeTerrainTexturePrivateStructuralPatch> patches,
        bool requireAtLeastOne)
    {
        if (requireAtLeastOne && patches.Count == 0)
            throw new InvalidDataException("The multi-level private texture batch contains no structural patches.");
        for (int index = 0; index < patches.Count; index++)
        {
            NativeTerrainTexturePrivateStructuralPatch patch = patches[index];
            if (patch.WadOffset < 0 ||
                patch.ByteLength <= 0 ||
                patch.Before == null ||
                patch.After == null ||
                patch.Before.Length != patch.ByteLength ||
                patch.After.Length != patch.ByteLength ||
                string.IsNullOrWhiteSpace(patch.BeforeSha256) ||
                string.IsNullOrWhiteSpace(patch.AfterSha256) ||
                !HashEquals(Sha256(patch.Before), patch.BeforeSha256) ||
                !HashEquals(Sha256(patch.After), patch.AfterSha256))
            {
                throw new InvalidDataException(
                    $"Structural patch {patch.Kind}/{patch.RuntimeKey} is empty, unequal-length, or missing its source/readback hash.");
            }
            if (index > 0)
            {
                NativeTerrainTexturePrivateStructuralPatch previous = patches[index - 1];
                if (patch.WadOffset < previous.WadOffset + previous.ByteLength)
                {
                    throw new InvalidDataException(
                        $"Multi-level structural patches {previous.Kind}/{previous.RuntimeKey} and {patch.Kind}/{patch.RuntimeKey} overlap.");
                }
            }
        }
    }

    private static void VerifyPatchPreimages(
        string imagePath,
        IReadOnlyList<NativeTerrainTexturePrivateStructuralPatch> patches,
        bool expectedAfter)
    {
        DiscLayout layout = DiscImage.DetectLayout(imagePath);
        using FileStream image = File.OpenRead(imagePath);
        foreach (NativeTerrainTexturePrivateStructuralPatch patch in patches)
        {
            byte[] expected = expectedAfter ? patch.After : patch.Before;
            string expectedSha256 = expectedAfter ? patch.AfterSha256 : patch.BeforeSha256;
            byte[] actual = DiscImage.ReadFileBytes(image, layout, WadLba, patch.WadOffset, patch.ByteLength);
            if (!actual.SequenceEqual(expected) || !HashEquals(Sha256(actual), expectedSha256))
            {
                string phase = expectedAfter ? "after" : "before";
                throw new InvalidDataException(
                    $"Multi-level structural patch {patch.Kind}/{patch.RuntimeKey} failed its {phase}-byte readback at WAD 0x{patch.WadOffset:X}.");
            }
        }
    }

    private static void VerifyPreparedLevelData(
        string imagePath,
        IReadOnlyList<NativeTerrainTexturePrivateAggregateLevelDataWrite> writes)
    {
        if (writes.Count == 0)
            return;
        DiscLayout layout = DiscImage.DetectLayout(imagePath);
        using FileStream image = File.OpenRead(imagePath);
        foreach (NativeTerrainTexturePrivateAggregateLevelDataWrite write in writes)
        {
            byte[] prepared = DiscImage.ReadFileBytes(
                image,
                layout,
                WadLba,
                write.RelocatedWadOffset,
                write.OutputByteLength);
            if (!prepared.AsSpan(0, write.SourceByteLength).SequenceEqual(write.Before) ||
                prepared.AsSpan(write.SourceByteLength).IndexOfAnyExcept((byte)0) >= 0)
            {
                throw new InvalidDataException(
                    $"Aggregate relocation did not prepare {write.TargetLevelKey}'s complete source level data followed by zero growth capacity.");
            }
        }
    }

    private static void VerifyLevelDataPreimages(
        string imagePath,
        IReadOnlyList<NativeTerrainTexturePrivateAggregateLevelDataWrite> writes,
        bool expectedAfter)
    {
        if (writes.Count == 0)
            return;
        DiscLayout layout = DiscImage.DetectLayout(imagePath);
        using FileStream image = File.OpenRead(imagePath);
        foreach (NativeTerrainTexturePrivateAggregateLevelDataWrite write in writes)
        {
            long wadOffset = expectedAfter ? write.RelocatedWadOffset : write.SourceWadOffset;
            int byteLength = expectedAfter ? write.OutputByteLength : write.SourceByteLength;
            byte[] expected = expectedAfter ? write.After : write.Before;
            string expectedSha256 = expectedAfter ? write.AfterSha256 : write.BeforeSha256;
            byte[] actual = DiscImage.ReadFileBytes(image, layout, WadLba, wadOffset, byteLength);
            if (!actual.SequenceEqual(expected) || !HashEquals(Sha256(actual), expectedSha256))
            {
                string phase = expectedAfter ? "after" : "before";
                throw new InvalidDataException(
                    $"Expanded level-data write for {write.TargetLevelKey} failed its {phase}-byte readback at WAD 0x{wadOffset:X}.");
            }
        }
    }

    private static void VerifyRelocatedPatchReadback(
        string imagePath,
        IReadOnlyList<NativeTerrainTexturePrivateAggregatePatch> patches,
        bool expectedAfter)
    {
        if (patches.Count == 0)
            return;
        DiscLayout layout = DiscImage.DetectLayout(imagePath);
        using FileStream image = File.OpenRead(imagePath);
        foreach (NativeTerrainTexturePrivateAggregatePatch patch in patches)
        {
            byte[] expected = expectedAfter ? patch.After : patch.Before;
            string expectedSha256 = expectedAfter ? patch.AfterSha256 : patch.BeforeSha256;
            byte[] actual = DiscImage.ReadFileBytes(
                image,
                layout,
                WadLba,
                patch.RelocatedWadOffset,
                patch.ByteLength);
            if (!actual.SequenceEqual(expected) || !HashEquals(Sha256(actual), expectedSha256))
            {
                string phase = expectedAfter ? "after" : "before";
                throw new InvalidDataException(
                    $"Relocated patch {patch.Kind}/{patch.RuntimeKey} failed its {phase}-byte readback at WAD 0x{patch.RelocatedWadOffset:X}.");
            }
        }
    }

    private static NativeTerrainTexturePrivateStructuralPatch ToStructuralPatch(
        NativeTerrainTextureRecordRelocationExternalPatch patch)
    {
        if (patch.Before == null || patch.After == null ||
            patch.Before.Length == 0 || patch.Before.Length != patch.After.Length)
        {
            throw new InvalidDataException(
                $"External texture patch {patch.Kind}/{patch.RuntimeKey} must have equal non-empty before/after bytes.");
        }
        byte[] before = patch.Before.ToArray();
        byte[] after = patch.After.ToArray();
        return new NativeTerrainTexturePrivateStructuralPatch(
            patch.SourceWadOffset,
            before.Length,
            before,
            after,
            Sha256(before),
            Sha256(after),
            patch.Kind,
            patch.RuntimeKey,
            patch.Description);
    }

    private static ArchiveEntryMap[] LoadWadEntries(string wadAnalysisPath)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(wadAnalysisPath));
        ArchiveEntryMap[] entries = document.RootElement.GetProperty("entries")
            .EnumerateArray()
            .Select(element => new ArchiveEntryMap(
                JsonValue.GetInt32(element, "index", -1),
                JsonValue.GetInt64(element, "offset", -1),
                JsonValue.GetInt32(element, "size", -1)))
            .Where(item => item.Index >= 0 && item.Offset >= NestedArchiveHeaderBytes && item.ByteLength > 0)
            .OrderBy(item => item.Offset)
            .ToArray();
        if (entries.Length == 0)
            throw new InvalidDataException("The source-bound WAD analysis contains no usable entries.");
        return entries;
    }

    private static bool OptionalPathsEqual(string left, string right)
    {
        bool leftEmpty = string.IsNullOrWhiteSpace(left);
        bool rightEmpty = string.IsNullOrWhiteSpace(right);
        return leftEmpty || rightEmpty ? leftEmpty && rightEmpty : PathsEqual(left, right);
    }

    private static bool RangesOverlap(long leftStart, long leftEnd, long rightStart, long rightEnd) =>
        leftStart < rightEnd && rightStart < leftEnd;

    private static bool IsSupportedSectorGrowth(
        int growthBytes,
        NativeTerrainTextureStructuralGrowthPolicy policy) =>
        NativeTerrainTextureRecordAppendBuilder.IsSectorGrowthAuthorized(
            growthBytes,
            policy);

    private static bool IsLevelPolicyCompatible(
        NativeTerrainTextureStructuralGrowthPolicy aggregatePolicy,
        NativeTerrainTextureStructuralGrowthPolicy levelPolicy) =>
        aggregatePolicy switch
        {
            NativeTerrainTextureStructuralGrowthPolicy.NormalChecked =>
                levelPolicy ==
                NativeTerrainTextureStructuralGrowthPolicy.NormalChecked,
            NativeTerrainTextureStructuralGrowthPolicy.RuntimeProvenPlus2000 =>
                levelPolicy is
                    NativeTerrainTextureStructuralGrowthPolicy.NormalChecked or
                    NativeTerrainTextureStructuralGrowthPolicy
                        .RuntimeProvenPlus2000,
            NativeTerrainTextureStructuralGrowthPolicy.RuntimeProvenExtended =>
                levelPolicy is
                    NativeTerrainTextureStructuralGrowthPolicy.NormalChecked or
                    NativeTerrainTextureStructuralGrowthPolicy
                        .RuntimeProvenPlus2000 or
                    NativeTerrainTextureStructuralGrowthPolicy
                        .RuntimeProvenExtended,
            NativeTerrainTextureStructuralGrowthPolicy.FiftyRowStaticResearch =>
                levelPolicy ==
                NativeTerrainTextureStructuralGrowthPolicy
                    .FiftyRowStaticResearch,
            _ => false
        };

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

    private static bool PathsEqual(string left, string right) =>
        string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);

    private static void DeleteStale(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    private sealed record AggregateStructuralState(
        string WadAnalysisPath,
        NativeSkyWadRelocationPlan? Relocation,
        IReadOnlyList<NativeSkyRelocationPayload> Payloads,
        IReadOnlyList<NativeTerrainTexturePrivateAggregatePatch> RelocatedPatches,
        IReadOnlyList<NativeTerrainTexturePrivateAggregateLevelDataWrite> ExpandedLevelDataWrites);

    private sealed record ArchiveEntryMap(int Index, long Offset, int ByteLength)
    {
        public long EndExclusive => checked(Offset + ByteLength);
    }

    private sealed record EntryInsertion(
        int WadEntry,
        long EntryRelativeOffset,
        int GrowthBytes,
        string TargetLevelKey);
}
