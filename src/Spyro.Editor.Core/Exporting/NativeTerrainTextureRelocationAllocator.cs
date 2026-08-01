using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Exporting;

[Flags]
public enum NativeTexturePageConsumerScope
{
    None = 0,
    Particles = 1 << 0,
    ResidentActorsAndScenery = 1 << 1,
    PlayerHudAndGlobal = 1 << 2,
    OtherRuntimeConsumers = 1 << 3,
    RequiredExternalConsumers = Particles | ResidentActorsAndScenery | PlayerHudAndGlobal | OtherRuntimeConsumers
}

public sealed record NativeTexturePageOwnedRange(long Offset, int Length, string Owner);

/// <summary>
/// A source-bound declaration of every non-terrain consumer of a level asset's shared
/// texture-pages subfile.  The allocator independently protects every terrain LQ/HQ
/// descriptor, so this proof only supplies the other runtime consumers.
/// </summary>
public sealed record NativeTexturePageExternalOwnershipProof(
    int TargetWadEntry,
    int TexturePagesLength,
    string TexturePagesSha256,
    NativeTexturePageConsumerScope CoveredScopes,
    IReadOnlyList<NativeTexturePageOwnedRange> OwnedRanges);

public sealed record NativeTerrainTextureRelocationImport(
    int TargetTextureId,
    int DonorWadEntry,
    int DonorTextureId,
    string DescriptorTier = "both",
    bool PreserveTargetDescriptorMaterial = false);

public sealed record NativeTerrainTextureRelocationPatch(
    long WadOffset,
    int ByteLength,
    byte[] Before,
    byte[] After,
    string Kind,
    string Description);

public sealed record NativeTerrainTextureRelocationAudit(
    int TargetWadEntry,
    int TargetTextureCount,
    int TexturePagesLength,
    string TexturePagesSha256,
    int TerrainOwnedByteCount,
    int TerrainFreeByteCount,
    int FullyMappedTerrainDescriptorCount,
    int PartiallyMappedTerrainDescriptorCount,
    int FullyExternalTerrainDescriptorCount,
    int RequestedLowDetailDescriptorCount,
    int RequestedLeadingDescriptorCount,
    int RequestedNormalDescriptorCount,
    int RequestedCloseDescriptorCount,
    int RequestedNormal32DescriptorCount,
    int RequestedClose16DescriptorCount,
    int RequestedClose32DescriptorCount,
    int RequestedRotatedHqDescriptorCount,
    int RequiredAllocationByteCount,
    bool ProvisionalTerrainOnlyFit,
    bool OwnershipProofComplete,
    bool RuntimeAnimationControlAuditRequired,
    IReadOnlyList<string> SafetyBlockers,
    IReadOnlyList<string> Notes);

public sealed record NativeTerrainTextureRelocationPlan(
    int TargetWadEntry,
    int TargetTextureCount,
    int TexturePagesLength,
    string TexturePagesSha256,
    int TerrainOwnedByteCount,
    int ExternalOwnedByteCount,
    int AllocatedPixelByteCount,
    int AllocatedPaletteByteCount,
    int RewrittenDescriptorCount,
    int RewrittenLowDetailDescriptorCount,
    int RewrittenLeadingDescriptorCount,
    int RewrittenNormalDescriptorCount,
    int RewrittenCloseDescriptorCount,
    int RelocatedNormal32DescriptorCount,
    int RelocatedClose16DescriptorCount,
    int RelocatedClose32DescriptorCount,
    int RelocatedRotatedHqDescriptorCount,
    int VerifiedProtectedDescriptorCount,
    bool ExactDonorIndexedPixelsVerified,
    bool ExactDonorPalettesVerified,
    bool LowDetailAliasPreserved,
    bool RuntimeAnimationControlAuditRequired,
    bool LogicalReadbackVerified,
    bool ProtectedStorageVerified,
    bool TargetDescriptorMaterialPolicyVerified,
    IReadOnlyList<NativeTerrainTextureRelocationImport> Imports,
    IReadOnlyList<NativeTerrainTextureRelocationPatch> Patches,
    IReadOnlyList<string> Notes);

public sealed record NativeTerrainTexturePromotionAttemptSet(
    IReadOnlyList<int[]> Attempts,
    int CandidateCount,
    int TotalPairCount,
    int PairAttemptCount,
    bool PairAttemptsTruncated);

/// <summary>
/// Relocates native terrain texture descriptors to new byte-private storage.  This is
/// deliberately proof-gated: a real-disc plan is not emitted until every non-terrain
/// consumer of the shared texture-pages subfile is represented by an ownership proof.
/// </summary>
public static class NativeTerrainTextureRelocationAllocator
{
    public const int MaxPromotionPairAttempts = 512;
    private const int WadLba = 37;
    private const int TexturePagesSubfileIndex = 0;
    private const int ModelSubfileIndex = 1;
    // Retail uploads the first 0x80000 bytes of subfile 0 into the right half
    // of PSX VRAM (x words 512..1023).  Texture descriptor X is expressed as
    // an 8-bpp byte coordinate, so the packed file coordinate is fullX-1024.
    private const int PackedVramRowBytes = 1024;
    private const int FullVramTextureByteX = 1024;
    private const int TexturePageMaxRows = 512;
    private const int AddressableTexturePageBytes = PackedVramRowBytes * TexturePageMaxRows;
    private const int LowDetailRecordBytes = 16;
    private const int HighDetailRecordBytes = 168;
    private const int LowDetailDescriptorCount = 2;
    private const int LeadingDescriptorCount = 1;
    private const int NormalDescriptorCount = 4;
    private const int CloseDescriptorCount = 16;
    private const int CompleteDescriptorCount =
        LowDetailDescriptorCount + LeadingDescriptorCount + NormalDescriptorCount + CloseDescriptorCount;
    private const int IndexedTileSize = 32;
    private const int HqPaletteByteCount = 256 * 2;
    private const int LqPixelRowByteCount = IndexedTileSize / 2;
    private const int LqPixelByteCount = LqPixelRowByteCount * IndexedTileSize;
    private const int LqPaletteRowByteCount = 16 * 2;
    private const int LqPaletteRowCount = 16;
    private const int LqPaletteByteCount = LqPaletteRowByteCount * LqPaletteRowCount;

    public static NativeTerrainTexturePromotionAttemptSet BuildBoundedPromotionAttempts(
        IReadOnlyList<int> candidateTextureIds)
    {
        ArgumentNullException.ThrowIfNull(candidateTextureIds);
        int[] candidates = candidateTextureIds
            .Distinct()
            .Order()
            .ToArray();
        int totalPairCount = checked((candidates.Length * (candidates.Length - 1)) / 2);
        List<int[]> attempts = [[]];
        attempts.AddRange(candidates.Select(textureId => new[] { textureId }));

        int pairAttemptCount = 0;
        for (int first = 0;
             first < candidates.Length && pairAttemptCount < MaxPromotionPairAttempts;
             first++)
        {
            for (int second = first + 1;
                 second < candidates.Length && pairAttemptCount < MaxPromotionPairAttempts;
                 second++)
            {
                attempts.Add([candidates[first], candidates[second]]);
                pairAttemptCount++;
            }
        }

        // The complete set remains a final deterministic escape hatch even
        // when the quadratic pair search is capped. Two candidates already
        // produce the same set as their sole pair, so do not duplicate it.
        if (candidates.Length > 2)
            attempts.Add(candidates);

        return new NativeTerrainTexturePromotionAttemptSet(
            attempts,
            candidates.Length,
            totalPairCount,
            pairAttemptCount,
            pairAttemptCount < totalPairCount);
    }

    private static readonly int[][] TextureDescriptorMatrices =
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

    public static NativeTerrainTextureRelocationAudit Audit(
        string sourceImagePath,
        LevelDefinition targetLevel,
        IReadOnlyList<NativeTerrainTextureRelocationImport> imports,
        NativeTexturePageExternalOwnershipProof? ownershipProof = null)
    {
        ArgumentNullException.ThrowIfNull(targetLevel);
        return Audit(sourceImagePath, targetLevel.SourceWadEntry, imports, ownershipProof);
    }

    public static NativeTerrainTextureRelocationAudit Audit(
        string sourceImagePath,
        int targetWadEntry,
        IReadOnlyList<NativeTerrainTextureRelocationImport> imports,
        NativeTexturePageExternalOwnershipProof? ownershipProof = null)
    {
        ValidateCommonArguments(sourceImagePath, targetWadEntry, imports);
        DiscLayout layout = DiscImage.DetectLayout(sourceImagePath);
        using FileStream imageStream = File.OpenRead(sourceImagePath);
        TextureAsset target = LoadTextureAsset(imageStream, layout, targetWadEntry);
        Dictionary<int, TextureAsset> donors = LoadDonors(sourceImagePath, imageStream, layout, imports);
        Dictionary<int, TierSelection> targetSelections = BuildTargetSelections(target, imports);
        bool[] terrainOwned = BuildTerrainOwnership(target, targetSelections, out TerrainOccupancyStatistics statistics);
        int requiredBytes = CalculateRequiredAllocationBytes(donors, imports);
        DescriptorShapeStatistics requestedShapes = CalculateRequestedShapeStatistics(donors, imports);
        List<string> blockers = ValidateOwnershipProof(target, ownershipProof, includeRanges: false, out _);

        bool provisionalFit = TryBuildAssetPlan(
            target,
            donors,
            imports,
            targetSelections,
            terrainOwned,
            Array.Empty<NativeTexturePageOwnedRange>(),
            out _,
            out string provisionalFailure);

        List<string> notes =
        [
            "The provisional fit relocates the complete native record: two TexLq descriptors, the byte-identical leading HQ/sprite descriptor, four normal HQ descriptors, and sixteen close HQ descriptors.",
            "TexLq is preserved as 4-bpp 32x32 indexed data plus all sixteen 16-color distance-palette rows. Normal and close HQ descriptors are preserved as packed 8-bpp square indexed data with 512-byte CLUTs; each descriptor supplies its proven 16- or 32-pixel side.",
            "The two TexLq rows and the leading HQ/sprite row retain their retail physical alias by sharing one relocated LQ pixel/palette allocation.",
            "Close HQ tile sides are derived from each descriptor edge: proven 16x16 and 32x32 forms are accepted, including all eight orientation matrices; any other form is blocked.",
            "The texture-pages subfile is shared. No patch plan is emitted until every required external-consumer scope is proven.",
            "Integration must separately reject target texture IDs controlled by the level's texture-animation or scrolling tables, because runtime code can overwrite the complete 184-byte descriptor record."
        ];
        if (!provisionalFit && !string.IsNullOrWhiteSpace(provisionalFailure))
            notes.Add($"Provisional allocation failed: {provisionalFailure}");

        return new NativeTerrainTextureRelocationAudit(
            TargetWadEntry: targetWadEntry,
            TargetTextureCount: target.Index.TextureCount,
            TexturePagesLength: target.TexturePages.Length,
            TexturePagesSha256: target.TexturePagesSha256,
            TerrainOwnedByteCount: terrainOwned.Count(value => value),
            TerrainFreeByteCount: terrainOwned.Take(Math.Min(terrainOwned.Length, AddressableTexturePageBytes)).Count(value => !value),
            FullyMappedTerrainDescriptorCount: statistics.FullyMappedDescriptorCount,
            PartiallyMappedTerrainDescriptorCount: statistics.PartiallyMappedDescriptorCount,
            FullyExternalTerrainDescriptorCount: statistics.FullyExternalDescriptorCount,
            RequestedLowDetailDescriptorCount: checked(imports.Count * LowDetailDescriptorCount),
            RequestedLeadingDescriptorCount: checked(imports.Count * LeadingDescriptorCount),
            RequestedNormalDescriptorCount: checked(imports.Count * NormalDescriptorCount),
            RequestedCloseDescriptorCount: checked(imports.Count * CloseDescriptorCount),
            RequestedNormal32DescriptorCount: requestedShapes.Normal32DescriptorCount,
            RequestedClose16DescriptorCount: requestedShapes.Close16DescriptorCount,
            RequestedClose32DescriptorCount: requestedShapes.Close32DescriptorCount,
            RequestedRotatedHqDescriptorCount: requestedShapes.RotatedHqDescriptorCount,
            RequiredAllocationByteCount: requiredBytes,
            ProvisionalTerrainOnlyFit: provisionalFit,
            OwnershipProofComplete: blockers.Count == 0,
            RuntimeAnimationControlAuditRequired: true,
            SafetyBlockers: blockers,
            Notes: notes);
    }

    public static bool TryBuild(
        string sourceImagePath,
        LevelDefinition targetLevel,
        IReadOnlyList<NativeTerrainTextureRelocationImport> imports,
        NativeTexturePageExternalOwnershipProof ownershipProof,
        out NativeTerrainTextureRelocationPlan? plan,
        out string failureReason)
    {
        ArgumentNullException.ThrowIfNull(targetLevel);
        return TryBuild(sourceImagePath, targetLevel.SourceWadEntry, imports, ownershipProof, out plan, out failureReason);
    }

    public static bool TryBuild(
        string sourceImagePath,
        int targetWadEntry,
        IReadOnlyList<NativeTerrainTextureRelocationImport> imports,
        NativeTexturePageExternalOwnershipProof ownershipProof,
        out NativeTerrainTextureRelocationPlan? plan,
        out string failureReason)
    {
        plan = null;
        try
        {
            ValidateCommonArguments(sourceImagePath, targetWadEntry, imports);
            ArgumentNullException.ThrowIfNull(ownershipProof);

            DiscLayout layout = DiscImage.DetectLayout(sourceImagePath);
            using FileStream imageStream = File.OpenRead(sourceImagePath);
            TextureAsset target = LoadTextureAsset(imageStream, layout, targetWadEntry);
            List<string> blockers = ValidateOwnershipProof(target, ownershipProof, includeRanges: true, out bool[] externalOwned);
            if (blockers.Count > 0)
            {
                failureReason = string.Join(" ", blockers);
                return false;
            }

            Dictionary<int, TextureAsset> donors = LoadDonors(sourceImagePath, imageStream, layout, imports);
            Dictionary<int, TierSelection> targetSelections = BuildTargetSelections(target, imports);
            bool[] terrainOwned = BuildTerrainOwnership(target, targetSelections, out TerrainOccupancyStatistics statistics);
            NativeTexturePageOwnedRange[] externalRanges = ownershipProof.OwnedRanges?.ToArray() ?? Array.Empty<NativeTexturePageOwnedRange>();
            if (!TryBuildAssetPlan(
                    target,
                    donors,
                    imports,
                    targetSelections,
                    terrainOwned,
                    externalRanges,
                    out AssetRelocationPlan? assetPlan,
                    out failureReason))
            {
                return false;
            }

            AssetRelocationPlan built = assetPlan!;
            List<NativeTerrainTextureRelocationPatch> patches = [];
            patches.AddRange(BuildDiffPatches(
                target.TexturePages,
                built.AfterTexturePages,
                target.TexturePagesWadOffset,
                "terrain-texture-relocated-data",
                "Write byte-private relocated native palette and indexed pixel data."));
            patches.AddRange(BuildDiffPatches(
                target.Model,
                built.AfterModel,
                target.ModelWadOffset,
                "terrain-texture-relocated-descriptor",
                "Point the reserved target texture descriptor at byte-private identity-layout storage."));

            plan = new NativeTerrainTextureRelocationPlan(
                TargetWadEntry: targetWadEntry,
                TargetTextureCount: target.Index.TextureCount,
                TexturePagesLength: target.TexturePages.Length,
                TexturePagesSha256: target.TexturePagesSha256,
                TerrainOwnedByteCount: terrainOwned.Count(value => value),
                ExternalOwnedByteCount: externalOwned.Count(value => value),
                AllocatedPixelByteCount: built.AllocatedPixelByteCount,
                AllocatedPaletteByteCount: built.AllocatedPaletteByteCount,
                RewrittenDescriptorCount: built.RewrittenDescriptorCount,
                RewrittenLowDetailDescriptorCount: built.RewrittenLowDetailDescriptorCount,
                RewrittenLeadingDescriptorCount: built.RewrittenLeadingDescriptorCount,
                RewrittenNormalDescriptorCount: built.RewrittenNormalDescriptorCount,
                RewrittenCloseDescriptorCount: built.RewrittenCloseDescriptorCount,
                RelocatedNormal32DescriptorCount: built.RelocatedNormal32DescriptorCount,
                RelocatedClose16DescriptorCount: built.RelocatedClose16DescriptorCount,
                RelocatedClose32DescriptorCount: built.RelocatedClose32DescriptorCount,
                RelocatedRotatedHqDescriptorCount: built.RelocatedRotatedHqDescriptorCount,
                VerifiedProtectedDescriptorCount: built.VerifiedProtectedDescriptorCount,
                ExactDonorIndexedPixelsVerified: built.ExactDonorIndexedPixelsVerified,
                ExactDonorPalettesVerified: built.ExactDonorPalettesVerified,
                LowDetailAliasPreserved: built.LowDetailAliasPreserved,
                RuntimeAnimationControlAuditRequired: true,
                LogicalReadbackVerified: built.LogicalReadbackVerified,
                ProtectedStorageVerified: built.ProtectedStorageVerified,
                TargetDescriptorMaterialPolicyVerified: built.TargetDescriptorMaterialPolicyVerified,
                Imports: imports.ToArray(),
                Patches: patches.OrderBy(patch => patch.WadOffset).ToArray(),
                Notes:
                [
                    $"Protected {statistics.FullyMappedDescriptorCount} fully mapped, {statistics.PartiallyMappedDescriptorCount} partially mapped, and {statistics.FullyExternalDescriptorCount} fully external terrain descriptors.",
                    "Each requested normal/close HQ tile has an independent 8-bpp pixel rectangle and 512-byte CLUT; logical indexed output is exact even when native HQ aliases are expanded.",
                    "Both TexLq descriptors and the leading HQ/sprite descriptor were rewritten identically to one relocated 4-bpp tile and its complete sixteen-row distance palette.",
                    imports.Any(importItem => importItem.PreserveTargetDescriptorMaterial)
                        ? "Art-only imports preserve every target descriptor's region material/ABR bits and non-orientation alpha/material controls while relocating donor logical pixels and palettes."
                        : "Native surface-transfer imports retain donor descriptor material/ABR semantics.",
                    "Before exporter integration, reject target IDs controlled by runtime texture-animation or scrolling tables; those loops can overwrite this static descriptor transplant."
                ]);
            failureReason = "";
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidDataException or InvalidOperationException or IOException or OverflowException)
        {
            failureReason = ex.Message;
            return false;
        }
    }

    private static void ValidateCommonArguments(
        string sourceImagePath,
        int targetWadEntry,
        IReadOnlyList<NativeTerrainTextureRelocationImport> imports)
    {
        if (!File.Exists(sourceImagePath))
            throw new FileNotFoundException("Missing source disc image.", sourceImagePath);
        if (targetWadEntry < 0)
            throw new ArgumentOutOfRangeException(nameof(targetWadEntry));
        if (imports == null || imports.Count == 0)
            throw new ArgumentException("At least one native terrain texture relocation import is required.", nameof(imports));
        int duplicateTarget = imports.GroupBy(importItem => importItem.TargetTextureId).FirstOrDefault(group => group.Count() > 1)?.Key ?? -1;
        if (duplicateTarget >= 0)
            throw new ArgumentException($"Target texture {duplicateTarget} is requested more than once; batch relocation must have one donor per target record.", nameof(imports));
    }

    private static int CalculateRequiredAllocationBytes(
        IReadOnlyDictionary<int, TextureAsset> donors,
        IReadOnlyList<NativeTerrainTextureRelocationImport> imports)
    {
        int total = 0;
        List<(int WadEntry, PixelBounds Bounds)> hqPixelAllocations = [];
        HashSet<(int WadEntry, long PaletteByteStart)> hqPaletteAllocations = [];
        HashSet<(int WadEntry, long PaletteByteStart, PixelBounds Bounds)> lqAllocations = [];
        foreach (NativeTerrainTextureRelocationImport import in imports)
        {
            _ = ParseTierSelection(import.DescriptorTier);
            if (!donors.TryGetValue(import.DonorWadEntry, out TextureAsset? donor) ||
                import.DonorTextureId < 0 || import.DonorTextureId >= donor.Index.TextureCount)
            {
                throw new InvalidOperationException(
                    $"Donor texture {import.DonorTextureId} is outside WAD entry {import.DonorWadEntry}'s decoded texture table.");
            }

            TextureRecord record = donor.Index.Records[import.DonorTextureId];
            ValidateCompleteRecordShape(record, import.DonorWadEntry, import.DonorTextureId, "Donor");
            TextureDescriptor lq = record.LowDetailDescriptors[0];
            PixelBounds lqBounds = GetPhysicalPixelBounds(lq, donor.TexturePages.Length);
            if (lqAllocations.Add((import.DonorWadEntry, lq.PaletteByteStart, lqBounds)))
                total = checked(total + LqPixelByteCount + LqPaletteByteCount);

            foreach (TextureDescriptor descriptor in record.NormalDescriptors
                         .Concat(record.CloseDescriptors)
                         .OrderByDescending(candidate => candidate.PixelStorageByteCount))
            {
                PixelBounds bounds = GetPhysicalPixelBounds(descriptor, donor.TexturePages.Length);
                if (!hqPixelAllocations.Any(existing =>
                        existing.WadEntry == import.DonorWadEntry && existing.Bounds.Contains(bounds)))
                {
                    hqPixelAllocations.Add((import.DonorWadEntry, bounds));
                    total = checked(total + bounds.ByteCount);
                }
                if (hqPaletteAllocations.Add((import.DonorWadEntry, descriptor.PaletteByteStart)))
                    total = checked(total + HqPaletteByteCount);
            }
        }

        return total;
    }

    private static DescriptorShapeStatistics CalculateRequestedShapeStatistics(
        IReadOnlyDictionary<int, TextureAsset> donors,
        IReadOnlyList<NativeTerrainTextureRelocationImport> imports)
    {
        int normal32 = 0;
        int close16 = 0;
        int close32 = 0;
        int rotated = 0;
        foreach (NativeTerrainTextureRelocationImport import in imports)
        {
            TextureRecord record = donors[import.DonorWadEntry].Index.Records[import.DonorTextureId];
            normal32 = checked(normal32 + record.NormalDescriptors.Count(descriptor => descriptor.TileSize == 32));
            close16 = checked(close16 + record.CloseDescriptors.Count(descriptor => descriptor.TileSize == 16));
            close32 = checked(close32 + record.CloseDescriptors.Count(descriptor => descriptor.TileSize == 32));
            rotated = checked(rotated + record.NormalDescriptors
                .Concat(record.CloseDescriptors)
                .Count(descriptor => descriptor.Orientation != 0));
        }
        return new DescriptorShapeStatistics(normal32, close16, close32, rotated);
    }

    private static Dictionary<int, TextureAsset> LoadDonors(
        string sourceImagePath,
        FileStream imageStream,
        DiscLayout layout,
        IReadOnlyList<NativeTerrainTextureRelocationImport> imports)
    {
        Dictionary<int, TextureAsset> result = new();
        foreach (int wadEntry in imports.Select(importItem => importItem.DonorWadEntry).Distinct())
        {
            TextureAsset raw = LoadTextureAsset(imageStream, layout, wadEntry);
            NativeTerrainTextureRuntimeControlAudit runtimeAudit =
                NativeTerrainTextureRuntimeControlScanner.Inspect(sourceImagePath, wadEntry);
            NativeTerrainTextureInitialStateResult initialState =
                NativeTerrainTextureRuntimeControlScanner.InitializeTextureRecords(runtimeAudit, raw.Model);
            if (!runtimeAudit.Complete || !initialState.Complete)
            {
                throw new InvalidDataException(
                    $"Donor WAD entry {wadEntry} could not be initialized to its native load-state texture table: " +
                    (initialState.SafetyBlockers.FirstOrDefault() ??
                     runtimeAudit.SafetyBlockers.FirstOrDefault() ??
                     "runtime texture-control initialization is incomplete"));
            }
            TextureRecordIndex initializedIndex = DecodeTextureRecords(initialState.InitializedTextureData);
            result[wadEntry] = raw with { Index = initializedIndex };
        }
        return result;
    }

    private static Dictionary<int, TierSelection> BuildTargetSelections(
        TextureAsset target,
        IReadOnlyList<NativeTerrainTextureRelocationImport> imports)
    {
        Dictionary<int, TierSelection> result = new();
        foreach (NativeTerrainTextureRelocationImport import in imports)
        {
            if (import.TargetTextureId < 0 || import.TargetTextureId >= target.Index.TextureCount)
                throw new InvalidOperationException($"Target texture {import.TargetTextureId} is outside the decoded {target.Index.TextureCount}-record table in WAD entry {target.WadEntry}.");
            if (import.DonorWadEntry < 0 || import.DonorTextureId < 0)
                throw new InvalidOperationException("Every relocation import needs a valid donor WAD entry and donor texture id.");
            TierSelection selection = ParseTierSelection(import.DescriptorTier);
            ValidateCompleteRecordShape(
                target.Index.Records[import.TargetTextureId],
                target.WadEntry,
                import.TargetTextureId,
                "Target");
            result[import.TargetTextureId] = selection;
        }

        return result;
    }

    private static TierSelection ParseTierSelection(string descriptorTier)
    {
        if (string.Equals(descriptorTier, "both", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(descriptorTier, "all", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(descriptorTier, "complete", StringComparison.OrdinalIgnoreCase))
        {
            return new TierSelection(LowDetail: true, Leading: true, Normal: true, Close: true);
        }

        if (string.Equals(descriptorTier, "hqData", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(descriptorTier, "hqDataClose", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Partial descriptor tier '{descriptorTier}' is runtime-incomplete. Native relocation requires 'both' so TexLq, leading HQ/sprite, normal HQ, and close HQ descriptors move atomically.");
        }

        throw new InvalidOperationException(
            $"Unsupported descriptor tier '{descriptorTier}'. Expected 'both' for the complete native texture record.");
    }

    private static List<string> ValidateOwnershipProof(
        TextureAsset target,
        NativeTexturePageExternalOwnershipProof? proof,
        bool includeRanges,
        out bool[] externalOwned)
    {
        externalOwned = new bool[target.TexturePages.Length];
        List<string> blockers = [];
        NativeTexturePageConsumerScope required = NativeTexturePageConsumerScope.RequiredExternalConsumers;
        NativeTexturePageConsumerScope covered = proof?.CoveredScopes ?? NativeTexturePageConsumerScope.None;
        foreach (NativeTexturePageConsumerScope scope in new[]
                 {
                     NativeTexturePageConsumerScope.Particles,
                     NativeTexturePageConsumerScope.ResidentActorsAndScenery,
                     NativeTexturePageConsumerScope.PlayerHudAndGlobal,
                     NativeTexturePageConsumerScope.OtherRuntimeConsumers
                 })
        {
            if ((covered & scope) == 0)
                blockers.Add($"Missing all-consumer ownership proof for {ScopeLabel(scope)}.");
        }

        if (proof == null)
            return blockers;
        if (proof.TargetWadEntry != target.WadEntry)
            blockers.Add($"Ownership proof targets WAD entry {proof.TargetWadEntry}, not target WAD entry {target.WadEntry}.");
        if (proof.TexturePagesLength != target.TexturePages.Length)
            blockers.Add($"Ownership proof texture-pages length {proof.TexturePagesLength} does not match {target.TexturePages.Length} bytes.");
        if (!string.Equals(proof.TexturePagesSha256, target.TexturePagesSha256, StringComparison.OrdinalIgnoreCase))
            blockers.Add("Ownership proof texture-pages SHA256 does not match the selected source disc.");
        if ((covered & required) != required)
            return blockers;
        if (!includeRanges)
            return blockers;

        foreach (NativeTexturePageOwnedRange range in proof.OwnedRanges ?? Array.Empty<NativeTexturePageOwnedRange>())
        {
            if (range.Offset < 0 || range.Length <= 0 || range.Offset + range.Length > externalOwned.Length)
            {
                blockers.Add($"External ownership range '{range.Owner}' at 0x{range.Offset:X}+0x{range.Length:X} is outside the texture-pages subfile.");
                continue;
            }

            for (long offset = range.Offset; offset < range.Offset + range.Length; offset++)
                externalOwned[checked((int)offset)] = true;
        }

        return blockers;
    }

    private static string ScopeLabel(NativeTexturePageConsumerScope scope) => scope switch
    {
        NativeTexturePageConsumerScope.Particles => "particle descriptors",
        NativeTexturePageConsumerScope.ResidentActorsAndScenery => "resident actor/scenery packages",
        NativeTexturePageConsumerScope.PlayerHudAndGlobal => "player, HUD, and global textures",
        NativeTexturePageConsumerScope.OtherRuntimeConsumers => "other runtime texture-page consumers",
        _ => scope.ToString()
    };

    private static string TierLabel(RelocationTier tier) => tier switch
    {
        RelocationTier.LowDetailAlias => "TexLq/leading alias",
        RelocationTier.NormalHq => "normal HQ",
        RelocationTier.CloseHq => "close HQ",
        _ => tier.ToString()
    };

    private static void ValidateCompleteRecordShape(
        TextureRecord record,
        int wadEntry,
        int textureId,
        string role)
    {
        if (record.LowDetailDescriptors.Count != LowDetailDescriptorCount ||
            record.NormalDescriptors.Count != NormalDescriptorCount ||
            record.CloseDescriptors.Count != CloseDescriptorCount)
        {
            throw new InvalidDataException(
                $"{role} WAD entry {wadEntry} texture {textureId} is not the native 2 TexLq + 1 leading + 4 normal HQ + 16 close HQ layout.");
        }
        if (!HasRetailLowDetailAlias(record))
        {
            throw new InvalidDataException(
                $"{role} WAD entry {wadEntry} texture {textureId} does not preserve the retail byte-identical TexLq0/TexLq1/leading alias.");
        }
        if (record.LowDetailDescriptors.Any(descriptor => !IsProvenLowDetailDescriptor(descriptor)) ||
            record.HighDetailLeadingDescriptor.Format != TextureDescriptorFormat.LowDetail4Bpp ||
            !IsProvenLowDetailDescriptor(record.HighDetailLeadingDescriptor))
        {
            throw new InvalidDataException(
                $"{role} WAD entry {wadEntry} texture {textureId} has an unsupported TexLq/leading shape.");
        }
        if (record.NormalDescriptors.Any(descriptor =>
                descriptor.Format != TextureDescriptorFormat.HighDetail8Bpp || descriptor.TileSize != IndexedTileSize))
        {
            throw new InvalidDataException(
                $"{role} WAD entry {wadEntry} texture {textureId} has a normal HQ descriptor that is not the proven 8-bpp 32x32 form.");
        }
        if (record.CloseDescriptors.Any(descriptor =>
                descriptor.Format != TextureDescriptorFormat.HighDetail8Bpp ||
                descriptor.TileSize is not (16 or IndexedTileSize)))
        {
            throw new InvalidDataException(
                $"{role} WAD entry {wadEntry} texture {textureId} has a close HQ descriptor outside the proven 8-bpp 16x16/32x32 forms.");
        }
    }

    private static bool HasRetailLowDetailAlias(TextureRecord record) =>
        record.LowDetailDescriptors.Count == LowDetailDescriptorCount &&
        record.LowDetailDescriptors[0].Raw.SequenceEqual(record.LowDetailDescriptors[1].Raw) &&
        record.LowDetailDescriptors[0].Raw.SequenceEqual(record.HighDetailLeadingDescriptor.Raw);

    private static bool IsProvenLowDetailDescriptor(TextureDescriptor descriptor) =>
        descriptor.Format == TextureDescriptorFormat.LowDetail4Bpp &&
        descriptor.TileSize == IndexedTileSize &&
        descriptor.Raw.Length == 8 &&
        (descriptor.Raw[0] & 1) == 0 &&
        Math.Abs(descriptor.Raw[4] - descriptor.Raw[0]) == IndexedTileSize - 1 &&
        descriptor.Raw[5] == descriptor.Raw[1] &&
        descriptor.Raw[7] == 0x80;

    private static bool[] BuildTerrainOwnership(
        TextureAsset target,
        IReadOnlyDictionary<int, TierSelection> targetSelections,
        out TerrainOccupancyStatistics statistics)
    {
        bool[] occupied = new bool[target.TexturePages.Length];
        int fullyMapped = 0;
        int partiallyMapped = 0;
        int fullyExternal = 0;
        foreach (TextureRecord record in target.Index.Records)
        {
            bool hasSelection = targetSelections.TryGetValue(record.TextureId, out TierSelection? selection);
            bool omitLowDetail = hasSelection && selection!.LowDetail;
            bool omitLeading = hasSelection && selection!.Leading;
            bool omitNormal = hasSelection && selection!.Normal;
            bool omitClose = hasSelection && selection!.Close;
            if (!omitLowDetail)
            {
                foreach (TextureDescriptor descriptor in record.LowDetailDescriptors)
                    MarkDescriptorOwnership(occupied, descriptor, ref fullyMapped, ref partiallyMapped, ref fullyExternal);
            }
            if (!omitLeading)
                MarkDescriptorOwnership(occupied, record.HighDetailLeadingDescriptor, ref fullyMapped, ref partiallyMapped, ref fullyExternal);
            if (!omitNormal)
            {
                foreach (TextureDescriptor descriptor in record.NormalDescriptors)
                    MarkDescriptorOwnership(occupied, descriptor, ref fullyMapped, ref partiallyMapped, ref fullyExternal);
            }

            if (!omitClose)
            {
                foreach (TextureDescriptor descriptor in record.CloseDescriptors)
                    MarkDescriptorOwnership(occupied, descriptor, ref fullyMapped, ref partiallyMapped, ref fullyExternal);
            }
        }

        statistics = new TerrainOccupancyStatistics(fullyMapped, partiallyMapped, fullyExternal);
        return occupied;
    }

    private static void MarkDescriptorOwnership(
        bool[] occupied,
        TextureDescriptor descriptor,
        ref int fullyMapped,
        ref int partiallyMapped,
        ref int fullyExternal)
    {
        int mappedPaletteBytes = 0;
        foreach (long offset in EnumeratePaletteByteOffsets(descriptor))
        {
            if (offset < 0 || offset >= occupied.Length)
                continue;
            occupied[checked((int)offset)] = true;
            mappedPaletteBytes++;
        }

        int mappedPixelSamples = 0;
        for (int y = 0; y < descriptor.TileSize; y++)
        {
            for (int x = 0; x < descriptor.TileSize; x++)
            {
                if (!TryGetTextureSampleAddress(descriptor, x, y, occupied.Length, out long offset, out _))
                    continue;
                occupied[checked((int)offset)] = true;
                if (descriptor.Format == TextureDescriptorFormat.LowDetail4Bpp)
                {
                    // Two logical 4-bpp samples share each packed byte. Count the
                    // byte once so mapped/expected units remain physical bytes.
                    if ((x & 1) == 0)
                        mappedPixelSamples++;
                }
                else
                {
                    mappedPixelSamples++;
                }
            }
        }

        int mappedUnits = mappedPaletteBytes + mappedPixelSamples;
        int expectedUnits = descriptor.Format == TextureDescriptorFormat.LowDetail4Bpp
            ? LqPaletteByteCount + LqPixelByteCount
            : HqPaletteByteCount + descriptor.PixelStorageByteCount;
        if (mappedUnits == expectedUnits)
            fullyMapped++;
        else if (mappedUnits == 0)
            fullyExternal++;
        else
            partiallyMapped++;
    }

    private static bool TryBuildAssetPlan(
        TextureAsset target,
        IReadOnlyDictionary<int, TextureAsset> donors,
        IReadOnlyList<NativeTerrainTextureRelocationImport> imports,
        IReadOnlyDictionary<int, TierSelection> targetSelections,
        bool[] terrainOwned,
        IReadOnlyList<NativeTexturePageOwnedRange> externalRanges,
        out AssetRelocationPlan? plan,
        out string failureReason)
    {
        plan = null;
        bool[] protectedBytes = terrainOwned.ToArray();
        foreach (NativeTexturePageOwnedRange range in externalRanges)
        {
            if (range.Offset < 0 || range.Length <= 0 || range.Offset + range.Length > protectedBytes.Length)
            {
                failureReason = $"External ownership range '{range.Owner}' is outside the texture-pages subfile.";
                return false;
            }
            for (long offset = range.Offset; offset < range.Offset + range.Length; offset++)
                protectedBytes[checked((int)offset)] = true;
        }

        bool[] reserved = protectedBytes.ToArray();
        byte[] afterPages = target.TexturePages.ToArray();
        byte[] afterModel = target.Model.ToArray();
        List<RelocationWork> work = [];
        foreach (NativeTerrainTextureRelocationImport import in imports)
        {
            if (!donors.TryGetValue(import.DonorWadEntry, out TextureAsset? donor))
            {
                failureReason = $"Donor WAD entry {import.DonorWadEntry} was not loaded.";
                return false;
            }
            if (import.DonorTextureId < 0 || import.DonorTextureId >= donor.Index.TextureCount)
            {
                failureReason = $"Donor texture {import.DonorTextureId} is outside WAD entry {import.DonorWadEntry}'s {donor.Index.TextureCount}-record table.";
                return false;
            }

            TextureRecord donorRecord = donor.Index.Records[import.DonorTextureId];
            TextureRecord targetRecord = target.Index.Records[import.TargetTextureId];
            TierSelection selection = targetSelections[import.TargetTextureId];
            if (!HasRetailLowDetailAlias(donorRecord))
            {
                failureReason =
                    $"Donor WAD entry {import.DonorWadEntry} texture {import.DonorTextureId} does not preserve the retail TexLq0/TexLq1/leading-descriptor alias; runtime-complete relocation is blocked.";
                return false;
            }
            if (!HasRetailLowDetailAlias(targetRecord))
            {
                failureReason =
                    $"Target WAD entry {target.WadEntry} texture {import.TargetTextureId} does not preserve the retail TexLq0/TexLq1/leading-descriptor alias; runtime-complete relocation is blocked.";
                return false;
            }

            if (selection.LowDetail && selection.Leading)
            {
                work.Add(new RelocationWork(
                    import,
                    donor,
                    donorRecord.LowDetailDescriptors[0],
                    [
                        targetRecord.LowDetailDescriptors[0],
                        targetRecord.LowDetailDescriptors[1],
                        targetRecord.HighDetailLeadingDescriptor
                    ],
                    RelocationTier.LowDetailAlias));
            }
            if (selection.Normal)
            {
                for (int index = 0; index < donorRecord.NormalDescriptors.Count; index++)
                {
                    work.Add(new RelocationWork(
                        import,
                        donor,
                        donorRecord.NormalDescriptors[index],
                        [targetRecord.NormalDescriptors[index]],
                        RelocationTier.NormalHq));
                }
            }
            if (selection.Close)
            {
                for (int index = 0; index < donorRecord.CloseDescriptors.Count; index++)
                {
                    work.Add(new RelocationWork(
                        import,
                        donor,
                        donorRecord.CloseDescriptors[index],
                        [targetRecord.CloseDescriptors[index]],
                        RelocationTier.CloseHq));
                }
            }
        }

        foreach (RelocationWork item in work)
        {
            if (!CanReadDescriptor(item.DonorDescriptor, item.Donor.TexturePages.Length))
            {
                failureReason =
                    $"Donor WAD entry {item.Import.DonorWadEntry} texture {item.Import.DonorTextureId} " +
                    $"{TierLabel(item.Tier)} descriptor {item.DonorDescriptor.Index} is not fully readable.";
                return false;
            }
            item.DonorPixelBounds = GetPhysicalPixelBounds(
                item.DonorDescriptor,
                item.Donor.TexturePages.Length);
        }
        AssignSharedStorageOwners(work);
        int rewrittenDescriptorCount = work.Sum(item => item.TargetDescriptors.Count);
        if (rewrittenDescriptorCount != checked(imports.Count * CompleteDescriptorCount))
        {
            failureReason =
                $"Runtime-complete relocation requires {CompleteDescriptorCount} descriptors per texture record, but the plan accounted for {rewrittenDescriptorCount}.";
            return false;
        }

        // Pixel rectangles have two-dimensional alignment and page-boundary
        // constraints, while HQ palettes only need one aligned span in a row.
        // Reserve the geometrically constrained rectangles first so palette
        // rows cannot fragment the only descriptor-encodable pixel holes.
        foreach (RelocationWork item in work
                     .Where(candidate => ReferenceEquals(candidate.PixelStorageOwner, candidate))
                     .OrderByDescending(candidate => candidate.PixelStorageByteCount))
        {
            int height = item.DonorPixelBounds.Height;
            int width = item.DonorPixelBounds.Width;
            int xAlignment = item.Tier == RelocationTier.LowDetailAlias ? 128 : item.TileSize;
            int yAlignment = item.Tier == RelocationTier.LowDetailAlias ? IndexedTileSize : item.TileSize;
            if (!TryAllocatePixelRectangle(
                    reserved,
                    width,
                    height,
                    xAlignment,
                    yAlignment,
                    out int pixelX,
                    out int pixelY))
            {
                failureReason =
                    $"No byte-private, descriptor-encodable {width}-byte x {height}-row {TierLabel(item.Tier)} pixel rectangle remains in target WAD entry {target.WadEntry}.";
                return false;
            }
            item.TargetPixelX = pixelX;
            item.TargetPixelY = pixelY;
        }

        foreach (RelocationWork item in work.Where(candidate =>
                     !ReferenceEquals(candidate.PixelStorageOwner, candidate)))
        {
            RelocationWork owner = item.PixelStorageOwner;
            item.TargetPixelX = checked(owner.TargetPixelX + item.DonorPixelBounds.X - owner.DonorPixelBounds.X);
            item.TargetPixelY = checked(owner.TargetPixelY + item.DonorPixelBounds.Y - owner.DonorPixelBounds.Y);
        }

        foreach (RelocationWork item in work
                     .Where(candidate => ReferenceEquals(candidate.PaletteStorageOwner, candidate))
                     .OrderByDescending(candidate => candidate.PaletteStorageByteCount))
        {
            bool allocated = item.Tier == RelocationTier.LowDetailAlias
                ? TryAllocateLqPalette(reserved, out int paletteOffset)
                : TryAllocateHqPalette(reserved, out paletteOffset);
            if (!allocated)
            {
                failureReason = item.Tier == RelocationTier.LowDetailAlias
                    ? $"No byte-private, TexLq-encodable sixteen-row 16-color distance palette remains in target WAD entry {target.WadEntry}."
                    : $"No byte-private, CLUT-encodable {HqPaletteByteCount}-byte HQ palette slot remains in target WAD entry {target.WadEntry}.";
                return false;
            }
            item.TargetPaletteOffset = paletteOffset;
        }

        foreach (RelocationWork item in work.Where(candidate =>
                     !ReferenceEquals(candidate.PaletteStorageOwner, candidate)))
        {
            item.TargetPaletteOffset = item.PaletteStorageOwner.TargetPaletteOffset;
        }

        foreach (RelocationWork item in work.Where(candidate =>
                     ReferenceEquals(candidate.PaletteStorageOwner, candidate)))
        {
            CopyPaletteData(item, afterPages);
        }
        foreach (RelocationWork item in work.Where(candidate =>
                     ReferenceEquals(candidate.PixelStorageOwner, candidate)))
        {
            CopyPixelData(item, afterPages);
        }
        foreach (RelocationWork item in work)
        {
            byte[] relocatedDescriptor = BuildRelocatedDescriptor(item);
            foreach (TextureDescriptor targetDescriptor in item.TargetDescriptors)
                relocatedDescriptor.CopyTo(afterModel, targetDescriptor.ModelOffset);
        }

        if (!VerifyProtectedStorage(target.TexturePages, afterPages, protectedBytes))
        {
            failureReason = "Relocation modified a byte owned by a protected terrain or external texture-page consumer.";
            return false;
        }
        if (!VerifyModelDiffScope(target, afterModel, work))
        {
            failureReason = "Relocation modified model bytes outside the requested target descriptor rows.";
            return false;
        }
        if (!VerifyLogicalReadback(target, donors, imports, afterPages, afterModel, out int verifiedProtectedDescriptors, out string verificationFailure))
        {
            failureReason = verificationFailure;
            return false;
        }

        plan = new AssetRelocationPlan(
            afterPages,
            afterModel,
            work.Where(item => ReferenceEquals(item.PixelStorageOwner, item))
                .Sum(item => item.PixelStorageByteCount),
            work.Where(item => ReferenceEquals(item.PaletteStorageOwner, item))
                .Sum(item => item.PaletteStorageByteCount),
            rewrittenDescriptorCount,
            RewrittenLowDetailDescriptorCount: work
                .Where(item => item.Tier == RelocationTier.LowDetailAlias)
                .Sum(_ => LowDetailDescriptorCount),
            RewrittenLeadingDescriptorCount: work.Count(item => item.Tier == RelocationTier.LowDetailAlias),
            RewrittenNormalDescriptorCount: work.Count(item => item.Tier == RelocationTier.NormalHq),
            RewrittenCloseDescriptorCount: work.Count(item => item.Tier == RelocationTier.CloseHq),
            RelocatedNormal32DescriptorCount: work.Count(item =>
                item.Tier == RelocationTier.NormalHq && item.TileSize == 32),
            RelocatedClose16DescriptorCount: work.Count(item =>
                item.Tier == RelocationTier.CloseHq && item.TileSize == 16),
            RelocatedClose32DescriptorCount: work.Count(item =>
                item.Tier == RelocationTier.CloseHq && item.TileSize == 32),
            RelocatedRotatedHqDescriptorCount: work.Count(item =>
                item.Tier != RelocationTier.LowDetailAlias && item.DonorDescriptor.Orientation != 0),
            verifiedProtectedDescriptors,
            ExactDonorIndexedPixelsVerified: true,
            ExactDonorPalettesVerified: true,
            LowDetailAliasPreserved: work
                .Where(item => item.Tier == RelocationTier.LowDetailAlias)
                .All(item => item.TargetDescriptors.Count == 3),
            LogicalReadbackVerified: true,
            ProtectedStorageVerified: true,
            TargetDescriptorMaterialPolicyVerified: true);
        failureReason = "";
        return true;
    }

    private static bool TryAllocateHqPalette(bool[] reserved, out int offset)
    {
        int maxX = PackedVramRowBytes - HqPaletteByteCount;
        int addressableLength = Math.Min(reserved.Length, AddressableTexturePageBytes);
        int maxY = Math.Min(TexturePageMaxRows - 1, (addressableLength - HqPaletteByteCount) / PackedVramRowBytes);
        for (int y = 0; y <= maxY; y++)
        {
            for (int x = 0; x <= maxX; x += 32)
            {
                int candidate = checked((y * PackedVramRowBytes) + x);
                if (candidate + HqPaletteByteCount > addressableLength || !IsRangeFree(reserved, candidate, HqPaletteByteCount))
                    continue;
                MarkRange(reserved, candidate, HqPaletteByteCount);
                offset = candidate;
                return true;
            }
        }

        offset = -1;
        return false;
    }

    private static bool TryAllocateLqPalette(bool[] reserved, out int offset)
    {
        int rows = Math.Min(TexturePageMaxRows, Math.Min(reserved.Length, AddressableTexturePageBytes) / PackedVramRowBytes);
        for (int y = 0; y + LqPaletteRowCount <= rows; y += 4)
        {
            for (int x = 0; x + LqPaletteRowByteCount <= PackedVramRowBytes; x += 32)
            {
                if (!IsRectangleFree(reserved, x, y, LqPaletteRowByteCount, LqPaletteRowCount))
                    continue;
                MarkRectangle(reserved, x, y, LqPaletteRowByteCount, LqPaletteRowCount);
                offset = checked((y * PackedVramRowBytes) + x);
                return true;
            }
        }

        offset = -1;
        return false;
    }

    private static bool TryAllocatePixelRectangle(
        bool[] reserved,
        int width,
        int height,
        int xAlignment,
        int yAlignment,
        out int pixelX,
        out int pixelY)
    {
        int rows = Math.Min(TexturePageMaxRows, Math.Min(reserved.Length, AddressableTexturePageBytes) / PackedVramRowBytes);
        int startY = ((rows - height) / yAlignment) * yAlignment;
        for (int y = startY; y >= 0; y -= yAlignment)
        {
            if ((y < 256 && y + height > 256) || y + height > rows)
                continue;
            int startX = ((PackedVramRowBytes - width) / xAlignment) * xAlignment;
            for (int x = startX; x >= 0; x -= xAlignment)
            {
                if (!IsRectangleFree(reserved, x, y, width, height))
                    continue;
                MarkRectangle(reserved, x, y, width, height);
                pixelX = x;
                pixelY = y;
                return true;
            }
        }

        pixelX = -1;
        pixelY = -1;
        return false;
    }

    private static bool IsRangeFree(bool[] reserved, int offset, int length)
    {
        for (int index = offset; index < offset + length; index++)
        {
            if (reserved[index])
                return false;
        }
        return true;
    }

    private static void MarkRange(bool[] reserved, int offset, int length)
    {
        for (int index = offset; index < offset + length; index++)
            reserved[index] = true;
    }

    private static bool IsRectangleFree(bool[] reserved, int x, int y, int width, int height)
    {
        for (int row = 0; row < height; row++)
        {
            int offset = checked(((y + row) * PackedVramRowBytes) + x);
            if (offset < 0 || offset + width > reserved.Length || !IsRangeFree(reserved, offset, width))
                return false;
        }
        return true;
    }

    private static void MarkRectangle(bool[] reserved, int x, int y, int width, int height)
    {
        for (int row = 0; row < height; row++)
            MarkRange(reserved, checked(((y + row) * PackedVramRowBytes) + x), width);
    }

    private static void AssignSharedStorageOwners(IReadOnlyList<RelocationWork> work)
    {
        List<RelocationWork> pixelOwners = [];
        List<RelocationWork> paletteOwners = [];
        foreach (RelocationWork item in work.OrderByDescending(candidate => candidate.PixelStorageByteCount))
        {
            RelocationWork? pixelOwner = pixelOwners.FirstOrDefault(owner =>
                owner.Donor.WadEntry == item.Donor.WadEntry &&
                owner.DonorPixelBounds.Contains(item.DonorPixelBounds) &&
                owner.DonorDescriptor.Format == item.DonorDescriptor.Format);
            item.PixelStorageOwner = pixelOwner ?? item;
            if (pixelOwner == null)
                pixelOwners.Add(item);

            RelocationWork? paletteOwner = paletteOwners.FirstOrDefault(owner =>
                owner.Donor.WadEntry == item.Donor.WadEntry &&
                owner.DonorDescriptor.Format == item.DonorDescriptor.Format &&
                owner.DonorDescriptor.PaletteByteStart == item.DonorDescriptor.PaletteByteStart);
            item.PaletteStorageOwner = paletteOwner ?? item;
            if (paletteOwner == null)
                paletteOwners.Add(item);
        }
    }

    private static void CopyPaletteData(RelocationWork item, byte[] targetPages)
    {
        if (item.Tier == RelocationTier.LowDetailAlias)
        {
            for (int row = 0; row < LqPaletteRowCount; row++)
            {
                Array.Copy(
                    item.Donor.TexturePages,
                    checked((int)item.DonorDescriptor.PaletteByteStart + (row * PackedVramRowBytes)),
                    targetPages,
                    checked(item.TargetPaletteOffset + (row * PackedVramRowBytes)),
                    LqPaletteRowByteCount);
            }
        }
        else
        {
            Array.Copy(
                item.Donor.TexturePages,
                checked((int)item.DonorDescriptor.PaletteByteStart),
                targetPages,
                item.TargetPaletteOffset,
                HqPaletteByteCount);
        }
    }

    private static void CopyPixelData(RelocationWork item, byte[] targetPages)
    {
        if (item.Tier != RelocationTier.LowDetailAlias)
        {
            for (int row = 0; row < item.DonorPixelBounds.Height; row++)
            {
                int donorOffset = checked(
                    ((item.DonorPixelBounds.Y + row) * PackedVramRowBytes) + item.DonorPixelBounds.X);
                int targetOffset = checked(
                    ((item.TargetPixelY + row) * PackedVramRowBytes) + item.TargetPixelX);
                Array.Copy(
                    item.Donor.TexturePages,
                    donorOffset,
                    targetPages,
                    targetOffset,
                    item.DonorPixelBounds.Width);
            }
            return;
        }

        for (int y = 0; y < item.TileSize; y++)
        {
            for (int x = 0; x < item.TileSize; x++)
            {
                if (!TryGetTextureSampleAddress(
                        item.DonorDescriptor,
                        x,
                        y,
                        item.Donor.TexturePages.Length,
                        out long donorOffset,
                        out int donorNibble))
                {
                    throw new InvalidOperationException("A donor descriptor became unreadable after validation.");
                }

                int donorIndex = (item.Donor.TexturePages[checked((int)donorOffset)] >> (donorNibble * 4)) & 0x0F;
                int targetOffset = checked(
                    ((item.TargetPixelY + y) * PackedVramRowBytes) + item.TargetPixelX + (x / 2));
                int targetNibble = x & 1;
                targetPages[targetOffset] = targetNibble == 0
                    ? (byte)((targetPages[targetOffset] & 0xF0) | donorIndex)
                    : (byte)((targetPages[targetOffset] & 0x0F) | (donorIndex << 4));
            }
        }
    }

    private static byte[] BuildRelocatedDescriptor(RelocationWork item)
    {
        if (item.Tier == RelocationTier.LowDetailAlias)
            return BuildLowDetailIdentityDescriptor(item);

        return BuildTranslatedHighDetailDescriptor(item);
    }

    private static byte[] BuildTranslatedHighDetailDescriptor(RelocationWork item)
    {
        int deltaX = item.TargetPixelX - item.DonorPixelBounds.X;
        int deltaY = item.TargetPixelY - item.DonorPixelBounds.Y;
        byte[] result = item.DonorDescriptor.Raw.ToArray();
        int donorRegion = item.DonorDescriptor.Raw[6];
        int targetFullX0 = checked(GetTextureX(donorRegion, item.DonorDescriptor.Raw[0]) + deltaX);
        int targetFullX1 = checked(GetTextureX(donorRegion, item.DonorDescriptor.Raw[4]) + deltaX);
        int targetFullY0 = checked(GetTextureY(donorRegion, item.DonorDescriptor.Raw[1]) + deltaY);
        int targetFullY1 = checked(GetTextureY(donorRegion, item.DonorDescriptor.Raw[5]) + deltaY);
        int targetPackedMinimumX = Math.Min(targetFullX0, targetFullX1) - FullVramTextureByteX;
        int targetMinimumY = Math.Min(targetFullY0, targetFullY1);
        int xPage = (FullVramTextureByteX + targetPackedMinimumX) / 128;
        int yPage = targetMinimumY >= 256 ? 1 : 0;
        int xBase = xPage * 128;
        int yBase = yPage * 256;
        int localX0 = targetFullX0 - xBase;
        int localX1 = targetFullX1 - xBase;
        int localY0 = targetFullY0 - yBase;
        int localY1 = targetFullY1 - yBase;
        if (xPage < 0 || xPage > 15 ||
            localX0 < 0 || localX0 > byte.MaxValue || localX1 < 0 || localX1 > byte.MaxValue ||
            localY0 < 0 || localY0 > byte.MaxValue || localY1 < 0 || localY1 > byte.MaxValue)
        {
            throw new InvalidOperationException("Allocated HQ pixel rectangle cannot preserve the donor descriptor transform in native coordinate fields.");
        }

        int paletteY = item.TargetPaletteOffset / PackedVramRowBytes;
        int paletteXByte = item.TargetPaletteOffset % PackedVramRowBytes;
        if ((paletteXByte & 31) != 0 || paletteY < 0 || paletteY > 511)
            throw new InvalidOperationException("Allocated palette slot cannot be encoded in the native CLUT field.");
        int clutX = 512 + (paletteXByte / 2);
        int clutCode = (paletteY << 6) | ((clutX / 16) & 0x3F);

        result[0] = checked((byte)localX0);
        result[1] = checked((byte)localY0);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(2, 2), checked((ushort)clutCode));
        result[4] = checked((byte)localX1);
        result[5] = checked((byte)localY1);
        byte[] targetMaterialDescriptor = item.TargetDescriptors[0].Raw;
        byte regionMaterial = item.Import.PreserveTargetDescriptorMaterial
            ? (byte)(targetMaterialDescriptor[6] & 0xE0)
            : (byte)(result[6] & 0xE0);
        result[6] = (byte)(regionMaterial | xPage | (yPage << 4));
        if (item.Import.PreserveTargetDescriptorMaterial)
        {
            result[7] = (byte)((targetMaterialDescriptor[7] & 0x8F) | (item.DonorDescriptor.Raw[7] & 0x70));
        }
        return result;
    }

    private static byte[] BuildIdentityDescriptor(RelocationWork item)
    {
        if (item.Tier == RelocationTier.LowDetailAlias)
            return BuildLowDetailIdentityDescriptor(item);

        int fullPixelX = FullVramTextureByteX + item.TargetPixelX;
        int xPage = fullPixelX / 128;
        int xBase = xPage * 128;
        int yPage = item.TargetPixelY >= 256 ? 1 : 0;
        int yBase = yPage * 256;
        int localX = fullPixelX - xBase;
        int localY = item.TargetPixelY - yBase;
        int logicalByteWidth = item.TileSize;
        if (xPage < 0 || xPage > 15 || localX < 0 || localX + logicalByteWidth - 1 > 255 ||
            localY < 0 || localY + item.TileSize > 256)
            throw new InvalidOperationException("Allocated pixel rectangle cannot be encoded in the native descriptor fields.");

        int paletteY = item.TargetPaletteOffset / PackedVramRowBytes;
        int paletteXByte = item.TargetPaletteOffset % PackedVramRowBytes;
        if ((paletteXByte & 31) != 0 || paletteY < 0 || paletteY > 511)
            throw new InvalidOperationException("Allocated palette slot cannot be encoded in the native CLUT field.");
        int clutX = 512 + (paletteXByte / 2);
        int clutCode = (paletteY << 6) | ((clutX / 16) & 0x3F);

        byte[] targetMaterialDescriptor = item.TargetDescriptors[0].Raw;
        byte[] result = item.DonorDescriptor.Raw.ToArray();
        result[0] = checked((byte)localX);
        result[1] = checked((byte)localY);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(2, 2), checked((ushort)clutCode));
        result[4] = checked((byte)(localX + logicalByteWidth - 1));
        result[5] = checked((byte)localY);
        byte regionMaterial = item.Import.PreserveTargetDescriptorMaterial
            ? (byte)(targetMaterialDescriptor[6] & 0xE0)
            : (byte)(result[6] & 0xE0);
        byte alphaMaterial = item.Import.PreserveTargetDescriptorMaterial
            ? (byte)(targetMaterialDescriptor[7] & 0x8F)
            : (byte)(result[7] & 0x8F);
        result[6] = (byte)(regionMaterial | xPage | (yPage << 4));
        // Relocated storage uses identity orientation. Preserve the target's
        // non-orientation alpha/material controls only in explicit art-only mode.
        result[7] = alphaMaterial;
        return result;
    }

    private static byte[] BuildLowDetailIdentityDescriptor(RelocationWork item)
    {
        int fullPixelTexelX = checked(item.TargetPixelX * 2);
        int xPage = fullPixelTexelX / 256;
        int xBase = xPage * 256;
        int yPage = item.TargetPixelY >= 256 ? 1 : 0;
        int yBase = yPage * 256;
        int localX = fullPixelTexelX - xBase;
        int localY = item.TargetPixelY - yBase;
        if (xPage < 0 || xPage > 7 || localX < 0 || localX + IndexedTileSize - 1 > byte.MaxValue ||
            localY < 0 || localY + IndexedTileSize > 256)
        {
            throw new InvalidOperationException("Allocated TexLq pixel rectangle cannot be encoded in the native descriptor fields.");
        }

        int paletteY = item.TargetPaletteOffset / PackedVramRowBytes;
        int paletteXByte = item.TargetPaletteOffset % PackedVramRowBytes;
        int fullPaletteXByte = FullVramTextureByteX + paletteXByte;
        if ((fullPaletteXByte % LqPaletteRowByteCount) != 0 || (paletteY % 4) != 0 ||
            paletteY + LqPaletteRowCount > TexturePageMaxRows)
        {
            throw new InvalidOperationException("Allocated TexLq distance palette cannot be encoded in palettex/palettey.");
        }

        int paletteX = fullPaletteXByte / LqPaletteRowByteCount;
        int paletteYCode = paletteY / 4;
        if (paletteX > byte.MaxValue || paletteYCode > byte.MaxValue)
            throw new InvalidOperationException("Allocated TexLq distance palette exceeds its byte-sized coordinate fields.");

        byte[] targetMaterialDescriptor = item.TargetDescriptors[0].Raw;
        byte[] result = item.DonorDescriptor.Raw.ToArray();
        result[0] = checked((byte)localX);
        result[1] = checked((byte)localY);
        result[2] = checked((byte)paletteX);
        result[3] = checked((byte)paletteYCode);
        result[4] = checked((byte)(localX + IndexedTileSize - 1));
        result[5] = checked((byte)localY);
        byte regionMaterial = item.Import.PreserveTargetDescriptorMaterial
            ? (byte)(targetMaterialDescriptor[6] & 0xE8)
            : (byte)(result[6] & 0xE8);
        result[6] = (byte)(regionMaterial | xPage | (yPage << 4));
        // TexLq has no orientation transform; alphaEtc is donor-owned for full
        // property transfer and target-owned for explicit art-only mode.
        if (item.Import.PreserveTargetDescriptorMaterial)
            result[7] = targetMaterialDescriptor[7];
        return result;
    }

    private static bool VerifyProtectedStorage(byte[] before, byte[] after, bool[] protectedBytes)
    {
        for (int index = 0; index < before.Length; index++)
        {
            if (protectedBytes[index] && before[index] != after[index])
                return false;
        }
        return true;
    }

    private static bool VerifyModelDiffScope(TextureAsset target, byte[] afterModel, IReadOnlyList<RelocationWork> work)
    {
        bool[] writable = new bool[target.Model.Length];
        foreach (RelocationWork item in work)
        {
            foreach (TextureDescriptor targetDescriptor in item.TargetDescriptors)
            {
                for (int index = targetDescriptor.ModelOffset; index < targetDescriptor.ModelOffset + 8; index++)
                    writable[index] = true;
            }
        }

        for (int index = 0; index < target.Model.Length; index++)
        {
            if (!writable[index] && target.Model[index] != afterModel[index])
                return false;
        }
        return true;
    }

    private static bool VerifyLogicalReadback(
        TextureAsset target,
        IReadOnlyDictionary<int, TextureAsset> donors,
        IReadOnlyList<NativeTerrainTextureRelocationImport> imports,
        byte[] afterPages,
        byte[] afterModel,
        out int verifiedProtectedDescriptors,
        out string failureReason)
    {
        TextureRecordIndex afterIndex = DecodeTextureRecords(afterModel);
        if (!VerifyTargetDescriptorMaterialPolicy(target, afterIndex, imports, out failureReason))
        {
            verifiedProtectedDescriptors = 0;
            return false;
        }
        foreach (NativeTerrainTextureRelocationImport import in imports)
        {
            TextureAsset donor = donors[import.DonorWadEntry];
            TextureRecord donorRecord = donor.Index.Records[import.DonorTextureId];
            TextureRecord targetRecord = afterIndex.Records[import.TargetTextureId];
            TierSelection selection = ParseTierSelection(import.DescriptorTier);
            if (selection.LowDetail && !VerifyDescriptorTier(
                    donor.TexturePages,
                    donorRecord.LowDetailDescriptors,
                    afterPages,
                    targetRecord.LowDetailDescriptors))
            {
                verifiedProtectedDescriptors = 0;
                failureReason = $"TexLq logical indexed/palette readback failed for target texture {import.TargetTextureId}.";
                return false;
            }
            if (selection.Leading && !VerifyDescriptorTier(
                    donor.TexturePages,
                    [donorRecord.HighDetailLeadingDescriptor],
                    afterPages,
                    [targetRecord.HighDetailLeadingDescriptor]))
            {
                verifiedProtectedDescriptors = 0;
                failureReason = $"Leading HQ/sprite alias logical indexed/palette readback failed for target texture {import.TargetTextureId}.";
                return false;
            }
            if (!HasRetailLowDetailAlias(targetRecord))
            {
                verifiedProtectedDescriptors = 0;
                failureReason = $"Relocated target texture {import.TargetTextureId} did not preserve the byte-identical TexLq0/TexLq1/leading alias.";
                return false;
            }
            if (selection.Normal && !VerifyDescriptorTier(donor.TexturePages, donorRecord.NormalDescriptors, afterPages, targetRecord.NormalDescriptors))
            {
                verifiedProtectedDescriptors = 0;
                failureReason = $"Normal-tier logical readback failed for target texture {import.TargetTextureId}.";
                return false;
            }
            if (selection.Close && !VerifyDescriptorTier(donor.TexturePages, donorRecord.CloseDescriptors, afterPages, targetRecord.CloseDescriptors))
            {
                verifiedProtectedDescriptors = 0;
                failureReason = $"Close-tier logical readback failed for target texture {import.TargetTextureId}.";
                return false;
            }
        }

        HashSet<int> rewritten = imports.Select(import => import.TargetTextureId).ToHashSet();
        verifiedProtectedDescriptors = 0;
        foreach (TextureRecord beforeRecord in target.Index.Records)
        {
            TextureRecord afterRecord = afterIndex.Records[beforeRecord.TextureId];
            if (rewritten.Contains(beforeRecord.TextureId))
                continue;

            if (!VerifyDescriptorTier(
                    target.TexturePages,
                    beforeRecord.LowDetailDescriptors,
                    afterPages,
                    afterRecord.LowDetailDescriptors,
                    requireReadable: false))
            {
                failureReason = $"A protected TexLq descriptor changed for texture {beforeRecord.TextureId}.";
                return false;
            }
            verifiedProtectedDescriptors += beforeRecord.LowDetailDescriptors.Count;
            if (!VerifyDescriptorTier(
                    target.TexturePages,
                    [beforeRecord.HighDetailLeadingDescriptor],
                    afterPages,
                    [afterRecord.HighDetailLeadingDescriptor],
                    requireReadable: false))
            {
                failureReason = $"A protected leading HQ/sprite descriptor changed for texture {beforeRecord.TextureId}.";
                return false;
            }
            verifiedProtectedDescriptors++;
            if (!VerifyDescriptorTier(target.TexturePages, beforeRecord.NormalDescriptors, afterPages, afterRecord.NormalDescriptors, requireReadable: false))
            {
                failureReason = $"A protected normal descriptor changed for texture {beforeRecord.TextureId}.";
                return false;
            }
            verifiedProtectedDescriptors += beforeRecord.NormalDescriptors.Count;
            if (!VerifyDescriptorTier(target.TexturePages, beforeRecord.CloseDescriptors, afterPages, afterRecord.CloseDescriptors, requireReadable: false))
            {
                failureReason = $"A protected close descriptor changed for texture {beforeRecord.TextureId}.";
                return false;
            }
            verifiedProtectedDescriptors += beforeRecord.CloseDescriptors.Count;
        }

        failureReason = "";
        return true;
    }

    private static bool VerifyTargetDescriptorMaterialPolicy(
        TextureAsset target,
        TextureRecordIndex afterIndex,
        IReadOnlyList<NativeTerrainTextureRelocationImport> imports,
        out string failureReason)
    {
        foreach (NativeTerrainTextureRelocationImport import in imports.Where(importItem =>
                     importItem.PreserveTargetDescriptorMaterial))
        {
            TextureDescriptor[] beforeDescriptors = EnumerateCompleteDescriptors(
                target.Index.Records[import.TargetTextureId]);
            TextureDescriptor[] afterDescriptors = EnumerateCompleteDescriptors(
                afterIndex.Records[import.TargetTextureId]);
            if (beforeDescriptors.Length != CompleteDescriptorCount ||
                afterDescriptors.Length != CompleteDescriptorCount)
            {
                failureReason = $"Art-only target texture {import.TargetTextureId} does not expose the complete descriptor set for material-policy readback.";
                return false;
            }

            for (int index = 0; index < beforeDescriptors.Length; index++)
            {
                TextureDescriptor before = beforeDescriptors[index];
                TextureDescriptor after = afterDescriptors[index];
                byte regionMask = before.Format == TextureDescriptorFormat.LowDetail4Bpp
                    ? (byte)0xE8
                    : (byte)0xE0;
                byte alphaMask = before.Format == TextureDescriptorFormat.LowDetail4Bpp
                    ? byte.MaxValue
                    : (byte)0x8F;
                if (before.Format != after.Format ||
                    (before.Raw[6] & regionMask) != (after.Raw[6] & regionMask) ||
                    (before.Raw[7] & alphaMask) != (after.Raw[7] & alphaMask))
                {
                    failureReason =
                        $"Art-only target texture {import.TargetTextureId} descriptor {index} changed target ABR/material controls " +
                        $"({before.Raw[6]:X2}/{before.Raw[7]:X2} -> {after.Raw[6]:X2}/{after.Raw[7]:X2}).";
                    return false;
                }
            }
        }

        failureReason = "";
        return true;
    }

    private static TextureDescriptor[] EnumerateCompleteDescriptors(TextureRecord record) =>
        record.LowDetailDescriptors
            .Concat([record.HighDetailLeadingDescriptor])
            .Concat(record.NormalDescriptors)
            .Concat(record.CloseDescriptors)
            .ToArray();

    private static bool VerifyDescriptorTier(
        byte[] donorPages,
        IReadOnlyList<TextureDescriptor> donorDescriptors,
        byte[] targetPages,
        IReadOnlyList<TextureDescriptor> targetDescriptors,
        bool requireReadable = true)
    {
        if (donorDescriptors.Count != targetDescriptors.Count)
            return false;
        for (int index = 0; index < donorDescriptors.Count; index++)
        {
            TextureDescriptor donor = donorDescriptors[index];
            TextureDescriptor target = targetDescriptors[index];
            if (donor.Format != target.Format || donor.TileSize != target.TileSize)
                return false;
            bool donorReadable = CanReadDescriptor(donor, donorPages.Length);
            bool targetReadable = CanReadDescriptor(target, targetPages.Length);
            if (!donorReadable || !targetReadable)
            {
                if (requireReadable)
                    return false;
                if (!donor.Raw.SequenceEqual(target.Raw))
                    return false;
                continue;
            }
            if (!VerifyPaletteReadback(donorPages, donor, targetPages, target))
                return false;
            for (int y = 0; y < donor.TileSize; y++)
            {
                for (int x = 0; x < donor.TileSize; x++)
                {
                    byte donorValue = ReadLogicalIndex(donorPages, donor, x, y);
                    byte targetValue = ReadLogicalIndex(targetPages, target, x, y);
                    if (donorValue != targetValue)
                        return false;
                }
            }
        }
        return true;
    }

    private static bool VerifyPaletteReadback(
        byte[] donorPages,
        TextureDescriptor donor,
        byte[] targetPages,
        TextureDescriptor target)
    {
        long[] donorOffsets = EnumeratePaletteByteOffsets(donor).ToArray();
        long[] targetOffsets = EnumeratePaletteByteOffsets(target).ToArray();
        if (donorOffsets.Length != targetOffsets.Length)
            return false;
        for (int index = 0; index < donorOffsets.Length; index++)
        {
            long donorOffset = donorOffsets[index];
            long targetOffset = targetOffsets[index];
            if (donorOffset < 0 || donorOffset >= donorPages.Length ||
                targetOffset < 0 || targetOffset >= targetPages.Length ||
                donorPages[checked((int)donorOffset)] != targetPages[checked((int)targetOffset)])
            {
                return false;
            }
        }
        return true;
    }

    private static byte ReadLogicalIndex(byte[] pages, TextureDescriptor descriptor, int x, int y)
    {
        if (!TryGetTextureSampleAddress(descriptor, x, y, pages.Length, out long offset, out int nibble))
            throw new InvalidOperationException("Descriptor sample is outside the texture-pages subfile.");
        byte packed = pages[checked((int)offset)];
        return descriptor.Format == TextureDescriptorFormat.LowDetail4Bpp
            ? (byte)((packed >> (nibble * 4)) & 0x0F)
            : packed;
    }

    private static bool CanReadDescriptor(TextureDescriptor descriptor, int texturePagesLength)
    {
        foreach (long paletteOffset in EnumeratePaletteByteOffsets(descriptor))
        {
            if (paletteOffset < 0 || paletteOffset >= texturePagesLength)
                return false;
        }
        for (int y = 0; y < descriptor.TileSize; y++)
        {
            for (int x = 0; x < descriptor.TileSize; x++)
            {
                if (!TryGetTextureSampleAddress(descriptor, x, y, texturePagesLength, out _, out _))
                    return false;
            }
        }
        return true;
    }

    private static PixelBounds GetPhysicalPixelBounds(
        TextureDescriptor descriptor,
        int texturePagesLength)
    {
        int minimumX = int.MaxValue;
        int minimumY = int.MaxValue;
        int maximumX = int.MinValue;
        int maximumY = int.MinValue;
        for (int y = 0; y < descriptor.TileSize; y++)
        {
            for (int x = 0; x < descriptor.TileSize; x++)
            {
                if (!TryGetTextureSampleAddress(
                        descriptor,
                        x,
                        y,
                        texturePagesLength,
                        out long offset,
                        out _))
                {
                    throw new InvalidDataException(
                        $"Descriptor {descriptor.Tier}[{descriptor.Index}] has a pixel outside the texture-pages subfile.");
                }
                int packedX = checked((int)(offset % PackedVramRowBytes));
                int sampleY = checked((int)(offset / PackedVramRowBytes));
                minimumX = Math.Min(minimumX, packedX);
                maximumX = Math.Max(maximumX, packedX);
                minimumY = Math.Min(minimumY, sampleY);
                maximumY = Math.Max(maximumY, sampleY);
            }
        }

        PixelBounds bounds = new(
            minimumX,
            minimumY,
            checked(maximumX - minimumX + 1),
            checked(maximumY - minimumY + 1));
        int expectedBytes = descriptor.Format == TextureDescriptorFormat.LowDetail4Bpp
            ? LqPixelByteCount
            : descriptor.PixelStorageByteCount;
        if (bounds.ByteCount != expectedBytes)
        {
            throw new InvalidDataException(
                $"Descriptor {descriptor.Tier}[{descriptor.Index}] maps a {bounds.Width}x{bounds.Height} physical rectangle, not its expected {expectedBytes:N0}-byte storage footprint.");
        }
        return bounds;
    }

    private static bool TryGetTextureSampleAddress(
        TextureDescriptor descriptor,
        int x,
        int y,
        int texturePagesLength,
        out long relativeOffset,
        out int nibble)
    {
        if (x < 0 || x >= descriptor.TileSize || y < 0 || y >= descriptor.TileSize)
        {
            relativeOffset = -1;
            nibble = 0;
            return false;
        }

        if (descriptor.Format == TextureDescriptorFormat.LowDetail4Bpp)
        {
            int packedX = descriptor.PackedPixelXMin + (x / 2);
            int sampleY = descriptor.VramYMin + y;
            nibble = x & 1;
            relativeOffset = (sampleY * (long)PackedVramRowBytes) + packedX;
            return packedX >= 0 && packedX < PackedVramRowBytes &&
                sampleY >= 0 && sampleY < TexturePageMaxRows &&
                relativeOffset >= 0 && relativeOffset < texturePagesLength;
        }

        int[] matrix = TextureDescriptorMatrices[Math.Clamp(descriptor.Orientation, 0, TextureDescriptorMatrices.Length - 1)];
        int xx = matrix[0];
        int xy = matrix[1];
        int yx = matrix[2];
        int yy = matrix[3];
        int edge = descriptor.TileSize - 1;
        int startY = descriptor.VramYMin + ((yx < 0 || yy < 0) ? edge : 0);
        int sy = startY + (x * yx) + (y * yy);
        int startX = descriptor.PackedPixelXMin + ((xx < 0 || xy < 0) ? edge : 0);
        int sxPacked = startX + (x * xx) + (y * xy);
        nibble = 0;

        relativeOffset = (sy * (long)PackedVramRowBytes) + sxPacked;
        return sxPacked >= 0 && sxPacked < PackedVramRowBytes && sy >= 0 && sy < TexturePageMaxRows &&
            relativeOffset >= 0 && relativeOffset < texturePagesLength;
    }

    private static IEnumerable<long> EnumeratePaletteByteOffsets(TextureDescriptor descriptor)
    {
        if (descriptor.Format == TextureDescriptorFormat.LowDetail4Bpp)
        {
            for (int row = 0; row < LqPaletteRowCount; row++)
            {
                long rowStart = descriptor.PaletteByteStart + (row * PackedVramRowBytes);
                for (int index = 0; index < LqPaletteRowByteCount; index++)
                    yield return rowStart + index;
            }
            yield break;
        }

        for (int index = 0; index < HqPaletteByteCount; index++)
            yield return descriptor.PaletteByteStart + index;
    }

    private static IReadOnlyList<NativeTerrainTextureRelocationPatch> BuildDiffPatches(
        byte[] before,
        byte[] after,
        long baseWadOffset,
        string kind,
        string description)
    {
        List<NativeTerrainTextureRelocationPatch> patches = [];
        int index = 0;
        while (index < before.Length)
        {
            if (before[index] == after[index])
            {
                index++;
                continue;
            }
            int start = index;
            while (index < before.Length && before[index] != after[index])
                index++;
            int length = index - start;
            patches.Add(new NativeTerrainTextureRelocationPatch(
                WadOffset: baseWadOffset + start,
                ByteLength: length,
                Before: before.AsSpan(start, length).ToArray(),
                After: after.AsSpan(start, length).ToArray(),
                Kind: kind,
                Description: description));
        }
        return patches;
    }

    private static TextureAsset LoadTextureAsset(FileStream imageStream, DiscLayout layout, int wadEntry)
    {
        AssetSubfileInfo pagesInfo = GetAssetSubfileInfo(imageStream, layout, wadEntry, TexturePagesSubfileIndex);
        AssetSubfileInfo modelInfo = GetAssetSubfileInfo(imageStream, layout, wadEntry, ModelSubfileIndex);
        byte[] pages = ReadWadBytes(imageStream, layout, pagesInfo.AbsoluteWadOffset, checked((int)pagesInfo.SubfileSize));
        byte[] model = ReadWadBytes(imageStream, layout, modelInfo.AbsoluteWadOffset, checked((int)modelInfo.SubfileSize));
        TextureRecordIndex index = DecodeTextureRecords(model);
        foreach (TextureRecord record in index.Records)
            ValidateCompleteRecordShape(record, wadEntry, record.TextureId, "Decoded");
        return new TextureAsset(
            wadEntry,
            pagesInfo.AbsoluteWadOffset,
            modelInfo.AbsoluteWadOffset,
            pages,
            model,
            Convert.ToHexString(SHA256.HashData(pages)),
            index);
    }

    private static AssetSubfileInfo GetAssetSubfileInfo(FileStream imageStream, DiscLayout layout, int assetWadIndex, int subfileIndex)
    {
        byte[] wadHeader = ReadWadBytes(imageStream, layout, 0, 4096);
        ArchiveEntry assetEntry = ParseArchiveHeader(wadHeader, 200_000_000)
            .FirstOrDefault(entry => entry.Index == assetWadIndex)
            ?? throw new InvalidOperationException($"Could not find WAD entry {assetWadIndex}.");
        byte[] assetHeader = ReadWadBytes(imageStream, layout, assetEntry.Offset, 4096);
        ArchiveEntry subfile = ParseArchiveHeader(assetHeader, assetEntry.Size)
            .FirstOrDefault(entry => entry.Index == subfileIndex)
            ?? throw new InvalidOperationException($"Could not find asset subfile {subfileIndex} in WAD entry {assetWadIndex}.");
        return new AssetSubfileInfo(assetWadIndex, subfileIndex, subfile.Size, assetEntry.Offset + subfile.Offset);
    }

    private static IReadOnlyList<ArchiveEntry> ParseArchiveHeader(byte[] bytes, long archiveSize)
    {
        List<ArchiveEntry> entries = [];
        long firstDataOffset = ReadUInt32(bytes, 0);
        if (firstDataOffset <= 0 || firstDataOffset > bytes.Length)
            firstDataOffset = bytes.Length;
        for (int offset = 0; offset <= Math.Min(bytes.Length, firstDataOffset) - 8; offset += 8)
        {
            long fileOffset = ReadUInt32(bytes, offset);
            long fileSize = ReadUInt32(bytes, offset + 4);
            if (fileOffset == 0 && fileSize == 0)
                continue;
            if (fileOffset < 0 || fileSize <= 0 || fileOffset + fileSize > archiveSize)
                continue;
            entries.Add(new ArchiveEntry(offset / 8, fileOffset, fileSize));
        }
        return entries;
    }

    private static byte[] ReadWadBytes(FileStream stream, DiscLayout layout, long wadOffset, int length) =>
        DiscImage.ReadFileBytes(stream, layout, WadLba, wadOffset, length);

    private static TextureRecordIndex DecodeTextureRecords(byte[] model)
    {
        if (model.Length < 8)
            throw new InvalidDataException("Model subfile is too short for a native terrain texture table.");
        int textureListSize = checked((int)ReadUInt32(model, 0));
        int textureCount = checked((int)ReadUInt32(model, 4));
        if (textureCount <= 0)
            throw new InvalidDataException("Native terrain texture table has no records.");
        int expected = checked(8 + (textureCount * (LowDetailRecordBytes + HighDetailRecordBytes)));
        if (textureListSize != expected || expected > model.Length)
            throw new InvalidDataException($"Native texture table length 0x{textureListSize:X} does not match {textureCount} 16-byte LQ plus 168-byte HQ records.");
        int highTableOffset = checked(8 + (textureCount * LowDetailRecordBytes));
        List<TextureRecord> records = new(textureCount);
        for (int textureId = 0; textureId < textureCount; textureId++)
        {
            int lowOffset = checked(8 + (textureId * LowDetailRecordBytes));
            int highOffset = checked(highTableOffset + (textureId * HighDetailRecordBytes));
            TextureDescriptor[] low =
            [
                DecodeLowDetailDescriptor(model, lowOffset, 0, "lq"),
                DecodeLowDetailDescriptor(model, lowOffset + 8, 1, "lq")
            ];
            // Retail duplicates TexLq0 into the first eight bytes of each 168-byte
            // HQ row. It is a 4-bpp LQ/sprite alias, not another TexHq tile.
            TextureDescriptor leading = DecodeLowDetailDescriptor(model, highOffset, 0, "hqLeading");
            TextureDescriptor[] normal = Enumerable.Range(0, 4)
                .Select(index => DecodeHighDetailDescriptor(model, highOffset + 8 + (index * 8), index, "hqData"))
                .ToArray();
            TextureDescriptor[] close = Enumerable.Range(0, 16)
                .Select(index => DecodeHighDetailDescriptor(model, highOffset + 40 + (index * 8), index, "hqDataClose"))
                .ToArray();
            records.Add(new TextureRecord(textureId, low, leading, normal, close));
        }
        return new TextureRecordIndex(textureCount, records);
    }

    private static TextureDescriptor DecodeLowDetailDescriptor(byte[] model, int offset, int index, string tier)
    {
        byte[] raw = model.AsSpan(offset, 8).ToArray();
        int region = raw[6];
        int paletteX = raw[2];
        int paletteY = raw[3];
        return new TextureDescriptor(
            Index: index,
            Tier: tier,
            ModelOffset: offset,
            Raw: raw,
            Format: TextureDescriptorFormat.LowDetail4Bpp,
            TileSize: IndexedTileSize,
            PaletteByteStart: checked((paletteY * 4L * PackedVramRowBytes) +
                (paletteX * (long)LqPaletteRowByteCount) - FullVramTextureByteX),
            Orientation: 0,
            PackedPixelXMin: (((region * 256) % 2048) + raw[0]) / 2,
            VramYMin: GetTextureY(region, raw[1]));
    }

    private static TextureDescriptor DecodeHighDetailDescriptor(byte[] model, int offset, int index, string tier)
    {
        byte[] raw = model.AsSpan(offset, 8).ToArray();
        int region = raw[6];
        int edgeSpan = Math.Max(Math.Abs(raw[4] - raw[0]), Math.Abs(raw[5] - raw[1]));
        int tileSize = checked(edgeSpan + 1);
        return new TextureDescriptor(
            Index: index,
            Tier: tier,
            ModelOffset: offset,
            Raw: raw,
            Format: TextureDescriptorFormat.HighDetail8Bpp,
            TileSize: tileSize,
            PaletteByteStart: DecodePackedClutByteStart(BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(2, 2))),
            Orientation: (raw[7] >> 4) & 7,
            PackedPixelXMin: GetTextureX(region, raw[0]) - FullVramTextureByteX,
            VramYMin: GetTextureY(region, raw[1]));
    }

    private static int GetTextureX(int region, int value) => ((region * 128) % 2048) + value;

    private static int GetTextureY(int region, int value) => ((region & 0x10) != 0 ? 256 : 0) + value;

    private static int DecodePackedClutByteStart(int code)
    {
        int clutX = (code & 0x3F) * 16;
        int clutY = (code >> 6) & 0x1FF;
        return checked((clutY * PackedVramRowBytes) + ((clutX - 512) * 2));
    }

    private static uint ReadUInt32(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));

    private sealed record ArchiveEntry(int Index, long Offset, long Size);

    private sealed record AssetSubfileInfo(int AssetWadIndex, int SubfileIndex, long SubfileSize, long AbsoluteWadOffset);

    private sealed record TextureAsset(
        int WadEntry,
        long TexturePagesWadOffset,
        long ModelWadOffset,
        byte[] TexturePages,
        byte[] Model,
        string TexturePagesSha256,
        TextureRecordIndex Index);

    private sealed record TextureRecordIndex(int TextureCount, IReadOnlyList<TextureRecord> Records);

    private sealed record TextureRecord(
        int TextureId,
        IReadOnlyList<TextureDescriptor> LowDetailDescriptors,
        TextureDescriptor HighDetailLeadingDescriptor,
        IReadOnlyList<TextureDescriptor> NormalDescriptors,
        IReadOnlyList<TextureDescriptor> CloseDescriptors);

    private sealed record TextureDescriptor(
        int Index,
        string Tier,
        int ModelOffset,
        byte[] Raw,
        TextureDescriptorFormat Format,
        int TileSize,
        long PaletteByteStart,
        int Orientation,
        int PackedPixelXMin,
        int VramYMin)
    {
        public int PixelStorageByteCount => Format == TextureDescriptorFormat.LowDetail4Bpp
            ? LqPixelByteCount
            : checked(TileSize * TileSize);
    }

    private enum TextureDescriptorFormat
    {
        LowDetail4Bpp,
        HighDetail8Bpp
    }

    private enum RelocationTier
    {
        LowDetailAlias,
        NormalHq,
        CloseHq
    }

    private sealed record TierSelection(bool LowDetail, bool Leading, bool Normal, bool Close);

    private sealed record TerrainOccupancyStatistics(
        int FullyMappedDescriptorCount,
        int PartiallyMappedDescriptorCount,
        int FullyExternalDescriptorCount);

    private sealed record DescriptorShapeStatistics(
        int Normal32DescriptorCount,
        int Close16DescriptorCount,
        int Close32DescriptorCount,
        int RotatedHqDescriptorCount);

    private sealed record PixelBounds(int X, int Y, int Width, int Height)
    {
        public int ByteCount => checked(Width * Height);

        public bool Contains(PixelBounds other) =>
            other.X >= X && other.Y >= Y &&
            other.X + other.Width <= X + Width &&
            other.Y + other.Height <= Y + Height;
    }

    private sealed class RelocationWork
    {
        public RelocationWork(
            NativeTerrainTextureRelocationImport import,
            TextureAsset donor,
            TextureDescriptor donorDescriptor,
            IReadOnlyList<TextureDescriptor> targetDescriptors,
            RelocationTier tier)
        {
            Import = import;
            Donor = donor;
            DonorDescriptor = donorDescriptor;
            TargetDescriptors = targetDescriptors;
            Tier = tier;
        }

        public NativeTerrainTextureRelocationImport Import { get; }
        public TextureAsset Donor { get; }
        public TextureDescriptor DonorDescriptor { get; }
        public IReadOnlyList<TextureDescriptor> TargetDescriptors { get; }
        public RelocationTier Tier { get; }
        public PixelBounds DonorPixelBounds { get; set; } = new(0, 0, 0, 0);
        public RelocationWork PixelStorageOwner { get; set; } = null!;
        public RelocationWork PaletteStorageOwner { get; set; } = null!;
        public int PaletteStorageByteCount => Tier == RelocationTier.LowDetailAlias
            ? LqPaletteByteCount
            : HqPaletteByteCount;
        public int TileSize => DonorDescriptor.TileSize;
        public int PixelStorageByteCount => DonorDescriptor.PixelStorageByteCount;
        public int TargetPaletteOffset { get; set; } = -1;
        public int TargetPixelX { get; set; } = -1;
        public int TargetPixelY { get; set; } = -1;
    }

    private sealed record AssetRelocationPlan(
        byte[] AfterTexturePages,
        byte[] AfterModel,
        int AllocatedPixelByteCount,
        int AllocatedPaletteByteCount,
        int RewrittenDescriptorCount,
        int RewrittenLowDetailDescriptorCount,
        int RewrittenLeadingDescriptorCount,
        int RewrittenNormalDescriptorCount,
        int RewrittenCloseDescriptorCount,
        int RelocatedNormal32DescriptorCount,
        int RelocatedClose16DescriptorCount,
        int RelocatedClose32DescriptorCount,
        int RelocatedRotatedHqDescriptorCount,
        int VerifiedProtectedDescriptorCount,
        bool ExactDonorIndexedPixelsVerified,
        bool ExactDonorPalettesVerified,
        bool LowDetailAliasPreserved,
        bool LogicalReadbackVerified,
        bool ProtectedStorageVerified,
        bool TargetDescriptorMaterialPolicyVerified);
}
