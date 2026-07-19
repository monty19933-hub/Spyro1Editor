using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Analysis;

public enum NativeTextureOwnershipProofState
{
    ProvenEnumerated,
    Unproven
}

public sealed record NativeTextureOwnershipScopeResult(
    string Scope,
    NativeTextureOwnershipProofState ProofState,
    int DescriptorCount,
    int MappedDescriptorCount,
    int PartiallyMappedDescriptorCount,
    int ExternalDescriptorCount,
    int ModelCount,
    int AnimatedModelCount,
    int SimpleModelCount,
    int FaceTableCount,
    int UnresolvedFaceTableCount,
    int AmbiguousFaceTableCount,
    int TexturedFaceCount,
    int OwnedByteCount,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<string> Notes);

public sealed record NativeTerrainDescriptorOwnershipSummary(
    int TextureCount,
    int LowDetailDescriptorCount,
    int LeadingDescriptorCount,
    int NormalDescriptorCount,
    int CloseDescriptorCount,
    int CloseSide16Count,
    int CloseSide32Count,
    int CloseOtherSideCount,
    int RotatedHqDescriptorCount,
    int MinimumCloseSide,
    int MaximumCloseSide,
    IReadOnlyDictionary<int, int> CloseSideHistogram);

public sealed record NativeTextureUnknownRangeSample(int Offset, int Length);

public sealed record NativeTexturePageOwnershipReport(
    string LevelKey,
    string LevelName,
    int LevelId,
    int WadEntry,
    long AssetWadOffset,
    int AssetByteLength,
    long TexturePagesWadOffset,
    int TexturePagesSubfileByteLength,
    string TexturePagesSubfileSha256,
    int AddressableTexturePageByteLength,
    string AddressableTexturePagesSha256,
    NativeTerrainDescriptorOwnershipSummary Terrain,
    IReadOnlyList<NativeTextureOwnershipScopeResult> Scopes,
    int KnownOwnedByteCount,
    int UnknownNonZeroByteCount,
    int UnknownNonZeroRegionCount,
    string UnknownNonZeroRangesSha256,
    IReadOnlyList<NativeTextureUnknownRangeSample> UnknownNonZeroRangeSamples,
    int UnknownUnprovenZeroByteCount,
    int ProtectedByteCount,
    int ProvablyPrivateByteCount,
    IReadOnlyList<NativeTexturePageOwnedRange> ProtectedRanges,
    bool AllConsumerClosureComplete,
    bool ProvablyPrivatePixelAndClutSpace,
    IReadOnlyList<string> SafetyBlockers,
    IReadOnlyList<string> Notes);

public sealed record NativeTerrainTextureRecordStorageRequirement(
    string LevelKey,
    int WadEntry,
    int TextureId,
    int LowDetailDescriptorCount,
    int HqDescriptorCount,
    int HqPixelByteCount,
    int HqClutByteCount,
    int CompleteRecordPixelByteCount,
    int CompleteRecordClutByteCount,
    int CompleteRecordIndependentByteCount,
    IReadOnlyDictionary<int, int> HqSideHistogram,
    bool FormatComplete,
    IReadOnlyList<string> Blockers);

public sealed record NativeTexturePrivateSpaceAssessment(
    string Request,
    int RequiredPixelByteCount,
    int RequiredClutByteCount,
    int RequiredTotalByteCount,
    bool RequiresNewStorage,
    bool Safe,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<string> Notes);

public sealed record NativeTexturePageTargetStorageIsolationReport(
    string LevelKey,
    string LevelName,
    int WadEntry,
    int TargetTextureId,
    int TextureCount,
    int TargetOwnedByteCount,
    int TargetPixelAndClutByteCount,
    int OtherTerrainOverlapByteCount,
    int ParticleOverlapByteCount,
    int ResidentActorOverlapByteCount,
    int PlayerOverlapByteCount,
    int HudGlobalOverlapByteCount,
    int OtherDecodedOverlapByteCount,
    int AnyDecodedConsumerOverlapByteCount,
    int TargetExclusiveByteCount,
    string TexturePagesSubfileSha256,
    int TerrainDescriptorTableByteLength,
    string TerrainDescriptorTableSha256,
    IReadOnlyList<NativeTexturePageOwnedRange> TargetOwnedRanges,
    IReadOnlyList<NativeTexturePageOwnedRange> TargetExclusiveRanges,
    bool DecodedConsumerExclusive,
    bool AllConsumerClosureComplete,
    IReadOnlyList<string> SafetyBlockers,
    IReadOnlyList<string> Notes);

public sealed record NativeTexturePageRelocationOwnershipProofResult(
    NativeTexturePageOwnershipReport LevelOwnership,
    IReadOnlyList<NativeTexturePageTargetStorageIsolationReport> TargetIsolations,
    NativeTexturePageExternalOwnershipProof Proof,
    int ReleasedTargetByteCount,
    int ProtectedRangeCount,
    IReadOnlyList<string> Notes);

/// <summary>
/// Enumerates source-bound users of the first 0x80000 bytes of a retail level's
/// VRAM/SPU subfile. The retail loader uploads only those bytes to the right half
/// of PSX VRAM. All undecoded bytes remain protected; a zero byte is never treated
/// as free merely because terrain does not reference it.
/// </summary>
public static class NativeTexturePageOwnershipScanner
{
    public const int AddressableTexturePageBytes = 0x80000;

    private const int WadLba = 37;
    private const int PackedRowBytes = 0x400;
    private const int FullRightHalfByteX = 0x400;
    private const int VramRows = 512;
    private const int LevelHeaderBytes = 0x1D0;
    private const int WadHeaderPeteEntryOffset = 0x40;
    private const int PeteModelTableBytes = 4 + (64 * 8);
    private const int PeteModelDataOffset = 0x800;
    private const int TerrainLowRecordBytes = 16;
    private const int TerrainHighRecordBytes = 168;

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

    public static NativeTexturePageOwnershipReport Scan(string sourceImagePath, LevelDefinition level)
    {
        ArgumentNullException.ThrowIfNull(level);
        if (!File.Exists(sourceImagePath))
            throw new FileNotFoundException("Missing source disc image.", sourceImagePath);
        if (level.SourceWadEntry < 0)
            throw new InvalidOperationException($"{level.DisplayName} has no mapped source WAD entry.");

        DiscLayout layout = DiscImage.DetectLayout(sourceImagePath);
        using FileStream stream = File.OpenRead(sourceImagePath);
        LevelAsset asset = LoadLevelAsset(stream, layout, level);
        ushort[] ownership = new ushort[AddressableTexturePageBytes];

        ScopeState terrainLq = new("terrain-lq");
        ScopeState terrainLeading = new("terrain-hq-leading");
        ScopeState terrainNormal = new("terrain-hq-normal");
        ScopeState terrainClose = new("terrain-hq-close");
        ScopeState particles = new("particle-tables");
        ScopeState residents = new("resident-actors-and-scenery");
        ScopeState player = new("spyro-player");
        ScopeState hudGlobal = new("hud-and-level-global");
        ScopeState other = new("other-runtime-consumers");

        TerrainDecodeResult terrain = DecodeTerrain(
            asset.LevelData,
            ownership,
            terrainLq,
            terrainLeading,
            terrainNormal,
            terrainClose);
        DecodeParticles(asset.LevelData, ownership, particles);
        DecodeResidentModels(asset, ownership, residents);
        DecodeSharedPeteModels(asset, stream, layout, ownership, player, hudGlobal);
        DecodeSceneTiledefs(asset.Scene, ownership, player, hudGlobal);
        DecodeDragonCutsceneModels(asset, stream, layout, ownership, other);

        // PETE.WAD is loaded separately, but its model faces still address the
        // right-half VRAM image supplied by the active level. Decode those shared
        // roots against every level upload rather than assuming the universal boot
        // image remains resident after LoadLevel.
        player.Note("Source trace: initialization loads the universal right-half VRAM image before PETE.WAD, but each level loader subsequently overwrites the complete right half. Shared Spyro face descriptors are therefore mapped against this level's uploaded bytes.");
        hudGlobal.Note("Shared PETE.WAD HUD/gem/text models and the 15 level-local LevelSceneHeader Tiledefs are mapped against this level's uploaded right-half bytes.");
        CloseOtherRuntimeConsumerScope(other);

        ScopeState[] scopeStates =
        [
            terrainLq,
            terrainLeading,
            terrainNormal,
            terrainClose,
            particles,
            residents,
            player,
            hudGlobal,
            other
        ];

        int knownOwned = ownership.Count(value => value != 0);
        UnknownSummary unknown = BuildUnknownSummary(asset.TexturePages, ownership);
        bool closureComplete = scopeStates.All(scope => scope.IsComplete);
        List<string> blockers = scopeStates
            .SelectMany(scope => scope.Blockers.Select(blocker => $"{scope.Name}: {blocker}"))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (!closureComplete)
        {
            blockers.Add(
                "Unclaimed zero-filled bytes are UnknownUnprovenZero and remain protected; terrain-only nonuse is not evidence of private storage.");
        }
        if (!closureComplete && unknown.NonZeroByteCount > 0)
        {
            blockers.Add(
                $"{unknown.NonZeroByteCount:N0} nonzero bytes in {unknown.RegionCount:N0} unclassified regions remain protected as UnknownNonZero.");
        }

        NativeTerrainDescriptorOwnershipSummary terrainSummary = new(
            TextureCount: terrain.TextureCount,
            LowDetailDescriptorCount: terrainLq.DescriptorCount,
            LeadingDescriptorCount: terrainLeading.DescriptorCount,
            NormalDescriptorCount: terrainNormal.DescriptorCount,
            CloseDescriptorCount: terrainClose.DescriptorCount,
            CloseSide16Count: terrain.CloseSides.TryGetValue(16, out int side16) ? side16 : 0,
            CloseSide32Count: terrain.CloseSides.TryGetValue(32, out int side32) ? side32 : 0,
            CloseOtherSideCount: terrain.CloseSides.Where(pair => pair.Key != 16 && pair.Key != 32).Sum(pair => pair.Value),
            RotatedHqDescriptorCount: terrain.RotatedHqDescriptorCount,
            MinimumCloseSide: terrain.CloseSides.Count == 0 ? 0 : terrain.CloseSides.Keys.Min(),
            MaximumCloseSide: terrain.CloseSides.Count == 0 ? 0 : terrain.CloseSides.Keys.Max(),
            CloseSideHistogram: new SortedDictionary<int, int>(terrain.CloseSides));

        IReadOnlyList<NativeTextureOwnershipScopeResult> scopeResults = scopeStates
            .Select(scope => scope.ToResult(CountOwnedBytes(ownership, scope.Bit)))
            .ToArray();

        int protectedByteCount = closureComplete
            ? knownOwned + unknown.NonZeroByteCount
            : AddressableTexturePageBytes;
        // Keep the enumerated/nonzero ranges visible even while closure is
        // incomplete. Unlisted zero bytes still remain protected by the
        // report's blocker and ProtectedByteCount; they are not an allocation
        // proof until every consumer scope closes.
        IReadOnlyList<NativeTexturePageOwnedRange> protectedRanges =
            BuildProtectedRanges(asset.TexturePages, ownership);
        return new NativeTexturePageOwnershipReport(
            LevelKey: level.Key,
            LevelName: level.DisplayName,
            LevelId: level.LevelId,
            WadEntry: level.SourceWadEntry,
            AssetWadOffset: asset.AssetWadOffset,
            AssetByteLength: asset.AssetByteLength,
            TexturePagesWadOffset: asset.AssetWadOffset + asset.VramOffset,
            TexturePagesSubfileByteLength: asset.VramSize,
            TexturePagesSubfileSha256: asset.TexturePagesSubfileSha256,
            AddressableTexturePageByteLength: asset.TexturePages.Length,
            AddressableTexturePagesSha256: Convert.ToHexString(SHA256.HashData(asset.TexturePages)),
            Terrain: terrainSummary,
            Scopes: scopeResults,
            KnownOwnedByteCount: knownOwned,
            UnknownNonZeroByteCount: unknown.NonZeroByteCount,
            UnknownNonZeroRegionCount: unknown.RegionCount,
            UnknownNonZeroRangesSha256: unknown.RangesSha256,
            UnknownNonZeroRangeSamples: unknown.Samples,
            UnknownUnprovenZeroByteCount: AddressableTexturePageBytes - knownOwned - unknown.NonZeroByteCount,
            ProtectedByteCount: protectedByteCount,
            ProvablyPrivateByteCount: closureComplete ? AddressableTexturePageBytes - knownOwned - unknown.NonZeroByteCount : 0,
            ProtectedRanges: protectedRanges,
            AllConsumerClosureComplete: closureComplete,
            ProvablyPrivatePixelAndClutSpace: closureComplete && knownOwned + unknown.NonZeroByteCount < AddressableTexturePageBytes,
            SafetyBlockers: blockers,
            Notes:
            [
                "Retail mapping: packed offset = y * 0x400 + (full VRAM byte X - 0x400), covering VRAM word X 512..1023 and Y 0..511.",
                "Only the first 0x80000 bytes of m_VramSramOffset are texture-addressable; later bytes are SPU payload and are excluded.",
                "HQ side length is derived from the raw descriptor extents and projected through its orientation matrix. Close descriptors remain 8-bpp and are commonly 16x16.",
                "Every decoded HQ descriptor protects its complete 512-byte CLUT. LQ protects its 32x32 4-bpp pixels and all sixteen 16-color palette rows.",
                "Every nonzero byte not claimed by a decoded consumer remains protected as UnknownNonZero. After all consumer scopes close, only the remaining source-zero union can be considered by the pair-specific allocator.",
                $"Protected {unknown.NonZeroByteCount:N0} unclassified nonzero byte(s) in {unknown.RegionCount:N0} compressed source regions."
            ]);
    }

    public static bool TryBuildExternalOwnershipProof(
        string sourceImagePath,
        LevelDefinition level,
        out NativeTexturePageOwnershipReport report,
        out NativeTexturePageExternalOwnershipProof? proof,
        out string failureReason)
    {
        report = Scan(sourceImagePath, level);
        proof = null;
        if (!report.AllConsumerClosureComplete)
        {
            failureReason = report.SafetyBlockers.FirstOrDefault()
                ?? "The all-consumer texture-page ownership closure is incomplete.";
            return false;
        }
        if (!report.ProvablyPrivatePixelAndClutSpace)
        {
            failureReason = "The complete ownership scan found no private pixel-and-CLUT storage for relocation.";
            return false;
        }
        if (report.AddressableTexturePageByteLength != report.TexturePagesSubfileByteLength &&
            report.TexturePagesSubfileByteLength < report.AddressableTexturePageByteLength)
        {
            failureReason = "The level texture-pages subfile is shorter than the source-bound addressable VRAM prefix.";
            return false;
        }

        proof = new NativeTexturePageExternalOwnershipProof(
            TargetWadEntry: report.WadEntry,
            TexturePagesLength: report.TexturePagesSubfileByteLength,
            TexturePagesSha256: report.TexturePagesSubfileSha256,
            CoveredScopes: NativeTexturePageConsumerScope.RequiredExternalConsumers,
            OwnedRanges: report.ProtectedRanges);
        failureReason = "";
        return true;
    }

    public static NativeTexturePageTargetStorageIsolationReport InspectTargetStorageIsolation(
        string sourceImagePath,
        LevelDefinition level,
        int targetTextureId)
    {
        ArgumentNullException.ThrowIfNull(level);
        if (!File.Exists(sourceImagePath))
            throw new FileNotFoundException("Missing source disc image.", sourceImagePath);

        DiscLayout layout = DiscImage.DetectLayout(sourceImagePath);
        using FileStream stream = File.OpenRead(sourceImagePath);
        LevelAsset asset = LoadLevelAsset(stream, layout, level);
        if (!TryReadTerrainHeader(asset.LevelData, out int textureCount, out int highTableOffset, out string failure))
            throw new InvalidDataException(failure);
        if (targetTextureId < 0 || targetTextureId >= textureCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetTextureId),
                $"Texture {targetTextureId} is outside {level.DisplayName}'s decoded 0..{textureCount - 1} record table.");
        }

        ushort[] targetOwnership = new ushort[AddressableTexturePageBytes];
        ScopeState targetLq = new("terrain-lq");
        ScopeState targetLeading = new("terrain-hq-leading");
        ScopeState targetNormal = new("terrain-hq-normal");
        ScopeState targetClose = new("terrain-hq-close");
        SortedDictionary<int, int> targetCloseSides = [];
        int targetRotated = 0;
        MarkTerrainTextureRecord(
            asset.LevelData,
            targetTextureId,
            highTableOffset,
            targetOwnership,
            targetLq,
            targetLeading,
            targetNormal,
            targetClose,
            targetCloseSides,
            ref targetRotated);

        ushort[] otherOwnership = new ushort[AddressableTexturePageBytes];
        ScopeState otherTerrainLq = new("terrain-lq");
        ScopeState otherTerrainLeading = new("terrain-hq-leading");
        ScopeState otherTerrainNormal = new("terrain-hq-normal");
        ScopeState otherTerrainClose = new("terrain-hq-close");
        SortedDictionary<int, int> otherCloseSides = [];
        int otherRotated = 0;
        for (int textureId = 0; textureId < textureCount; textureId++)
        {
            if (textureId == targetTextureId)
                continue;
            MarkTerrainTextureRecord(
                asset.LevelData,
                textureId,
                highTableOffset,
                otherOwnership,
                otherTerrainLq,
                otherTerrainLeading,
                otherTerrainNormal,
                otherTerrainClose,
                otherCloseSides,
                ref otherRotated);
        }

        ScopeState particles = new("particle-tables");
        ScopeState residents = new("resident-actors-and-scenery");
        ScopeState player = new("spyro-player");
        ScopeState hudGlobal = new("hud-and-level-global");
        ScopeState other = new("other-runtime-consumers");
        DecodeParticles(asset.LevelData, otherOwnership, particles);
        DecodeResidentModels(asset, otherOwnership, residents);
        DecodeSharedPeteModels(asset, stream, layout, otherOwnership, player, hudGlobal);
        DecodeSceneTiledefs(asset.Scene, otherOwnership, player, hudGlobal);
        DecodeDragonCutsceneModels(asset, stream, layout, otherOwnership, other);
        CloseOtherRuntimeConsumerScope(other);

        OwnershipBit terrainMask = OwnershipBit.TerrainLq |
            OwnershipBit.TerrainLeading |
            OwnershipBit.TerrainNormal |
            OwnershipBit.TerrainClose;
        int targetOwned = targetOwnership.Count(value => value != 0);
        int otherTerrainOverlap = CountOverlap(targetOwnership, otherOwnership, terrainMask);
        int particleOverlap = CountOverlap(targetOwnership, otherOwnership, OwnershipBit.Particles);
        int residentOverlap = CountOverlap(targetOwnership, otherOwnership, OwnershipBit.Residents);
        int playerOverlap = CountOverlap(targetOwnership, otherOwnership, OwnershipBit.Player);
        int hudOverlap = CountOverlap(targetOwnership, otherOwnership, OwnershipBit.HudGlobal);
        int otherOverlap = CountOverlap(targetOwnership, otherOwnership, OwnershipBit.Other);
        int anyOverlap = Enumerable.Range(0, AddressableTexturePageBytes)
            .Count(offset => targetOwnership[offset] != 0 && otherOwnership[offset] != 0);
        int exclusive = targetOwned - anyOverlap;

        ScopeState[] targetScopes = [targetLq, targetLeading, targetNormal, targetClose];
        ScopeState[] otherTerrainScopes =
            [otherTerrainLq, otherTerrainLeading, otherTerrainNormal, otherTerrainClose];
        ScopeState[] externalScopes = [particles, residents, player, hudGlobal, other];
        List<string> blockers = targetScopes
            .Concat(otherTerrainScopes)
            .Concat(externalScopes)
            .SelectMany(scope => scope.Blockers.Select(blocker => $"{scope.Name}: {blocker}"))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (anyOverlap > 0)
        {
            blockers.Add(
                $"Target texture {targetTextureId} overlaps {anyOverlap:N0} byte(s) used by another decoded terrain or external consumer.");
        }
        bool closureComplete = targetScopes
            .Concat(otherTerrainScopes)
            .Concat(externalScopes)
            .All(scope => scope.IsComplete);
        if (!closureComplete)
        {
            blockers.Add(
                "Target exclusivity is proven only against decoded consumers; the remaining runtime-consumer scope must close before production writeback.");
        }

        return new NativeTexturePageTargetStorageIsolationReport(
            LevelKey: level.Key,
            LevelName: level.DisplayName,
            WadEntry: level.SourceWadEntry,
            TargetTextureId: targetTextureId,
            TextureCount: textureCount,
            TargetOwnedByteCount: targetOwned,
            TargetPixelAndClutByteCount: targetOwned,
            OtherTerrainOverlapByteCount: otherTerrainOverlap,
            ParticleOverlapByteCount: particleOverlap,
            ResidentActorOverlapByteCount: residentOverlap,
            PlayerOverlapByteCount: playerOverlap,
            HudGlobalOverlapByteCount: hudOverlap,
            OtherDecodedOverlapByteCount: otherOverlap,
            AnyDecodedConsumerOverlapByteCount: anyOverlap,
            TargetExclusiveByteCount: exclusive,
            TexturePagesSubfileSha256: asset.TexturePagesSubfileSha256,
            TerrainDescriptorTableByteLength: checked((int)ReadUInt32(asset.LevelData, 0)),
            TerrainDescriptorTableSha256: Convert.ToHexString(SHA256.HashData(
                asset.LevelData.AsSpan(0, checked((int)ReadUInt32(asset.LevelData, 0))))),
            TargetOwnedRanges: BuildMaskRanges(targetOwnership, value => value != 0, $"terrain-texture-{targetTextureId}"),
            TargetExclusiveRanges: BuildExclusiveRanges(targetOwnership, otherOwnership, $"terrain-texture-{targetTextureId}-decoded-exclusive"),
            DecodedConsumerExclusive: targetOwned > 0 && anyOverlap == 0,
            AllConsumerClosureComplete: closureComplete,
            SafetyBlockers: blockers,
            Notes:
            [
                "The target footprint uses allocator-correct grammar: two 4-bpp TexLq rows plus their byte-identical leading alias, four normal 8-bpp HQ rows, and sixteen close 8-bpp HQ rows.",
                "Byte counts are physical unions, so the three LQ/leading aliases and any descriptor pixel/CLUT aliases are counted once.",
                $"Target record includes {targetCloseSides.GetValueOrDefault(16)} close 16x16 and {targetCloseSides.GetValueOrDefault(32)} close 32x32 descriptor(s), with {targetRotated} rotated HQ descriptor(s)."
            ]);
    }

    public static bool TryBuildRelocationOwnershipProof(
        string sourceImagePath,
        LevelDefinition level,
        IReadOnlyList<int> targetTextureIds,
        out NativeTexturePageRelocationOwnershipProofResult? result,
        out string failureReason)
    {
        result = null;
        ArgumentNullException.ThrowIfNull(level);
        if (targetTextureIds == null || targetTextureIds.Count == 0)
        {
            failureReason = "At least one target terrain texture id is required.";
            return false;
        }
        int[] targets = targetTextureIds.Distinct().Order().ToArray();
        if (targets.Length != targetTextureIds.Count)
        {
            failureReason = "Relocation ownership proof target texture ids must be unique.";
            return false;
        }

        NativeTexturePageOwnershipReport ownership = Scan(sourceImagePath, level);
        if (!ownership.AllConsumerClosureComplete)
        {
            failureReason = ownership.SafetyBlockers.FirstOrDefault()
                ?? "The level's all-consumer ownership closure is incomplete.";
            return false;
        }

        List<NativeTexturePageTargetStorageIsolationReport> isolations = [];
        foreach (int targetTextureId in targets)
        {
            NativeTexturePageTargetStorageIsolationReport isolation =
                InspectTargetStorageIsolation(sourceImagePath, level, targetTextureId);
            if (!isolation.AllConsumerClosureComplete || isolation.TargetOwnedByteCount <= 0)
            {
                failureReason = isolation.SafetyBlockers.FirstOrDefault()
                    ?? $"Target texture {targetTextureId} does not have a closed decoded footprint.";
                return false;
            }
            if (!string.Equals(
                    isolation.TexturePagesSubfileSha256,
                    ownership.TexturePagesSubfileSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                failureReason = $"Target texture {targetTextureId}'s isolation hash does not match the level ownership scan.";
                return false;
            }
            isolations.Add(isolation);
        }

        NativeTexturePageOwnedRange[] releasable = isolations
            .SelectMany(isolation => isolation.TargetExclusiveRanges)
            .OrderBy(range => range.Offset)
            .ToArray();
        IReadOnlyList<NativeTexturePageOwnedRange> protectedRanges = SubtractRanges(
            ownership.ProtectedRanges,
            releasable,
            "decoded-consumer-or-unclassified-nonzero-excluding-target-records");
        NativeTexturePageExternalOwnershipProof proof = new(
            TargetWadEntry: ownership.WadEntry,
            TexturePagesLength: ownership.TexturePagesSubfileByteLength,
            TexturePagesSha256: ownership.TexturePagesSubfileSha256,
            CoveredScopes: NativeTexturePageConsumerScope.RequiredExternalConsumers,
            OwnedRanges: protectedRanges);
        int releasedBytes = ownership.ProtectedByteCount - protectedRanges.Sum(range => range.Length);
        result = new NativeTexturePageRelocationOwnershipProofResult(
            LevelOwnership: ownership,
            TargetIsolations: isolations,
            Proof: proof,
            ReleasedTargetByteCount: releasedBytes,
            ProtectedRangeCount: protectedRanges.Count,
            Notes:
            [
                "All decoded consumers and every unclassified source-nonzero byte remain protected.",
                "Only byte ranges proven exclusive to the requested target terrain records are released. Bytes shared with another decoded terrain or external consumer remain protected while the allocator independently omits only the requested target descriptors.",
                "Source-zero space is available only because every runtime-consumer scope is closed and the proof is bound to the complete texture-page subfile hash."
            ]);
        failureReason = "";
        return true;
    }

    public static NativeTerrainTextureRecordStorageRequirement InspectTerrainRecordStorage(
        string sourceImagePath,
        LevelDefinition level,
        int textureId)
    {
        ArgumentNullException.ThrowIfNull(level);
        if (!File.Exists(sourceImagePath))
            throw new FileNotFoundException("Missing source disc image.", sourceImagePath);
        DiscLayout layout = DiscImage.DetectLayout(sourceImagePath);
        using FileStream stream = File.OpenRead(sourceImagePath);
        LevelAsset asset = LoadLevelAsset(stream, layout, level);
        List<string> blockers = [];
        if (!TryReadTerrainHeader(asset.LevelData, out int textureCount, out int highTableOffset, out string failure))
        {
            blockers.Add(failure);
            return new NativeTerrainTextureRecordStorageRequirement(
                level.Key, level.SourceWadEntry, textureId, 0, 0, 0, 0, 0, 0, 0,
                new SortedDictionary<int, int>(), false, blockers);
        }
        if (textureId < 0 || textureId >= textureCount)
        {
            blockers.Add($"Texture {textureId} is outside the decoded 0..{textureCount - 1} range.");
            return new NativeTerrainTextureRecordStorageRequirement(
                level.Key, level.SourceWadEntry, textureId, 0, 0, 0, 0, 0, 0, 0,
                new SortedDictionary<int, int>(), false, blockers);
        }

        int hqPixelBytes = 0;
        SortedDictionary<int, int> sideHistogram = [];
        int highOffset = checked(highTableOffset + (textureId * TerrainHighRecordBytes));
        foreach (int descriptorOffset in EnumerateHqDescriptorOffsets(highOffset))
        {
            ReadOnlySpan<byte> raw = asset.LevelData.AsSpan(descriptorOffset, 8);
            int side = DeriveHqSide(raw);
            if (side is < 1 or > 256)
            {
                blockers.Add($"HQ descriptor at level-data offset 0x{descriptorOffset:X} has invalid side {side}.");
                continue;
            }
            hqPixelBytes = checked(hqPixelBytes + (side * side));
            sideHistogram[side] = sideHistogram.TryGetValue(side, out int count) ? count + 1 : 1;
        }

        const int lqDescriptorCount = 2;
        const int hqDescriptorCount = 21;
        const int lqPixelBytes = lqDescriptorCount * 32 * 32 / 2;
        const int lqClutBytes = lqDescriptorCount * 16 * 32;
        int hqClutBytes = hqDescriptorCount * 512;
        int completePixels = checked(lqPixelBytes + hqPixelBytes);
        int completeCluts = checked(lqClutBytes + hqClutBytes);
        return new NativeTerrainTextureRecordStorageRequirement(
            LevelKey: level.Key,
            WadEntry: level.SourceWadEntry,
            TextureId: textureId,
            LowDetailDescriptorCount: lqDescriptorCount,
            HqDescriptorCount: hqDescriptorCount,
            HqPixelByteCount: hqPixelBytes,
            HqClutByteCount: hqClutBytes,
            CompleteRecordPixelByteCount: completePixels,
            CompleteRecordClutByteCount: completeCluts,
            CompleteRecordIndependentByteCount: checked(completePixels + completeCluts),
            HqSideHistogram: sideHistogram,
            FormatComplete: blockers.Count == 0,
            Blockers: blockers);
    }

    public static NativeTexturePrivateSpaceAssessment AssessPrivateSpace(
        NativeTexturePageOwnershipReport report,
        string request,
        int requiredPixelByteCount,
        int requiredClutByteCount)
    {
        ArgumentNullException.ThrowIfNull(report);
        if (requiredPixelByteCount < 0)
            throw new ArgumentOutOfRangeException(nameof(requiredPixelByteCount));
        if (requiredClutByteCount < 0)
            throw new ArgumentOutOfRangeException(nameof(requiredClutByteCount));

        List<string> blockers = [];
        if (!report.AllConsumerClosureComplete)
            blockers.Add("The all-consumer ownership proof is incomplete.");
        if (!report.ProvablyPrivatePixelAndClutSpace)
            blockers.Add("No byte range is proven private for both indexed pixels and CLUT data.");
        int requiredTotal = checked(requiredPixelByteCount + requiredClutByteCount);
        if (report.ProvablyPrivateByteCount < requiredTotal)
        {
            blockers.Add(
                $"The source-bound private-byte union has {report.ProvablyPrivateByteCount:N0} bytes, less than the requested {requiredTotal:N0} bytes.");
        }
        if (requiredTotal > 0)
        {
            blockers.Add(
                "A byte count is not a descriptor-encodable allocation plan; the relocation allocator must still prove aligned pixel rectangles, CLUT rows, alias safety, and logical readback.");
        }
        blockers.AddRange(report.SafetyBlockers);
        return new NativeTexturePrivateSpaceAssessment(
            Request: request,
            RequiredPixelByteCount: requiredPixelByteCount,
            RequiredClutByteCount: requiredClutByteCount,
            RequiredTotalByteCount: requiredTotal,
            RequiresNewStorage: requiredTotal > 0,
            Safe: blockers.Count == 0,
            Blockers: blockers.Distinct(StringComparer.Ordinal).ToArray(),
            Notes:
            [
                "This assessment does not convert terrain-only holes or zero-filled bytes into allocatable storage.",
                "Pixel and CLUT capacity must both be proven under complete runtime-consumer closure.",
                "Use NativeTerrainTextureRelocationAllocator with the source-bound proof for an actual encodable fit decision."
            ]);
    }

    public static NativeTexturePrivateSpaceAssessment AssessNativeTextureReuse(
        NativeTexturePageOwnershipReport report,
        string request)
    {
        ArgumentNullException.ThrowIfNull(report);
        return new NativeTexturePrivateSpaceAssessment(
            Request: request,
            RequiredPixelByteCount: 0,
            RequiredClutByteCount: 0,
            RequiredTotalByteCount: 0,
            RequiresNewStorage: false,
            Safe: true,
            Blockers: Array.Empty<string>(),
            Notes:
            [
                "Reusing an already resident native texture ID requires no private pixel or CLUT allocation.",
                "This is storage-safety only; face selection, material behavior, and runtime rendering still need their own validation.",
                "A same-level reuse result is not evidence that cross-level texture storage is available."
            ]);
    }

    private static IReadOnlyList<NativeTexturePageOwnedRange> BuildProtectedRanges(
        byte[] texturePages,
        ushort[] ownership)
    {
        List<NativeTexturePageOwnedRange> ranges = [];
        int start = -1;
        for (int offset = 0; offset < AddressableTexturePageBytes; offset++)
        {
            bool isProtected = ownership[offset] != 0 || texturePages[offset] != 0;
            if (isProtected && start < 0)
            {
                start = offset;
            }
            else if (!isProtected && start >= 0)
            {
                ranges.Add(new NativeTexturePageOwnedRange(
                    start,
                    offset - start,
                    "decoded-consumer-or-unclassified-nonzero"));
                start = -1;
            }
        }
        if (start >= 0)
        {
            ranges.Add(new NativeTexturePageOwnedRange(
                start,
                AddressableTexturePageBytes - start,
                "decoded-consumer-or-unclassified-nonzero"));
        }
        return ranges;
    }

    private static int CountOverlap(
        ushort[] targetOwnership,
        ushort[] otherOwnership,
        OwnershipBit mask)
    {
        ushort bits = (ushort)mask;
        int count = 0;
        for (int offset = 0; offset < AddressableTexturePageBytes; offset++)
        {
            if (targetOwnership[offset] != 0 && (otherOwnership[offset] & bits) != 0)
                count++;
        }
        return count;
    }

    private static IReadOnlyList<NativeTexturePageOwnedRange> BuildMaskRanges(
        ushort[] ownership,
        Func<ushort, bool> predicate,
        string owner)
    {
        List<NativeTexturePageOwnedRange> ranges = [];
        int start = -1;
        for (int offset = 0; offset < AddressableTexturePageBytes; offset++)
        {
            bool included = predicate(ownership[offset]);
            if (included && start < 0)
            {
                start = offset;
            }
            else if (!included && start >= 0)
            {
                ranges.Add(new NativeTexturePageOwnedRange(start, offset - start, owner));
                start = -1;
            }
        }
        if (start >= 0)
            ranges.Add(new NativeTexturePageOwnedRange(start, AddressableTexturePageBytes - start, owner));
        return ranges;
    }

    private static IReadOnlyList<NativeTexturePageOwnedRange> BuildExclusiveRanges(
        ushort[] targetOwnership,
        ushort[] otherOwnership,
        string owner)
    {
        ushort[] exclusive = new ushort[AddressableTexturePageBytes];
        for (int offset = 0; offset < exclusive.Length; offset++)
            exclusive[offset] = targetOwnership[offset] != 0 && otherOwnership[offset] == 0 ? (ushort)1 : (ushort)0;
        return BuildMaskRanges(exclusive, value => value != 0, owner);
    }

    private static IReadOnlyList<NativeTexturePageOwnedRange> SubtractRanges(
        IReadOnlyList<NativeTexturePageOwnedRange> protectedRanges,
        IReadOnlyList<NativeTexturePageOwnedRange> releasedRanges,
        string owner)
    {
        List<NativeTexturePageOwnedRange> result = [];
        NativeTexturePageOwnedRange[] released = releasedRanges
            .Where(range => range.Length > 0)
            .OrderBy(range => range.Offset)
            .ToArray();
        foreach (NativeTexturePageOwnedRange source in protectedRanges.OrderBy(range => range.Offset))
        {
            long cursor = source.Offset;
            long end = source.Offset + source.Length;
            foreach (NativeTexturePageOwnedRange release in released)
            {
                long releaseStart = release.Offset;
                long releaseEnd = release.Offset + release.Length;
                if (releaseEnd <= cursor)
                    continue;
                if (releaseStart >= end)
                    break;
                if (releaseStart > cursor)
                {
                    result.Add(new NativeTexturePageOwnedRange(
                        cursor,
                        checked((int)(Math.Min(releaseStart, end) - cursor)),
                        owner));
                }
                cursor = Math.Max(cursor, releaseEnd);
                if (cursor >= end)
                    break;
            }
            if (cursor < end)
            {
                result.Add(new NativeTexturePageOwnedRange(
                    cursor,
                    checked((int)(end - cursor)),
                    owner));
            }
        }
        return result;
    }

    private static LevelAsset LoadLevelAsset(FileStream stream, DiscLayout layout, LevelDefinition level)
    {
        byte[] entryPair = DiscImage.ReadFileBytes(stream, layout, WadLba, level.SourceWadEntry * 8L, 8);
        long assetOffset = ReadUInt32(entryPair, 0);
        int assetLength = checked((int)ReadUInt32(entryPair, 4));
        if (assetOffset <= 0 || assetLength < LevelHeaderBytes)
            throw new InvalidDataException($"{level.DisplayName} WAD entry {level.SourceWadEntry} has an invalid asset header.");

        byte[] header = DiscImage.ReadFileBytes(stream, layout, WadLba, assetOffset, LevelHeaderBytes);
        int vramOffset = checked((int)ReadUInt32(header, 0x00));
        int vramSize = checked((int)ReadUInt32(header, 0x04));
        int dataOffset = checked((int)ReadUInt32(header, 0x08));
        int dataSize = checked((int)ReadUInt32(header, 0x0C));
        int modelOffset = checked((int)ReadUInt32(header, 0x10));
        int modelSize = checked((int)ReadUInt32(header, 0x14));
        int sceneOffset = checked((int)ReadUInt32(header, 0x18));
        int sceneSize = checked((int)ReadUInt32(header, 0x1C));
        ValidateSubfile("VRAM/SPU", vramOffset, vramSize, assetLength);
        ValidateSubfile("level data", dataOffset, dataSize, assetLength);
        ValidateSubfile("resident model", modelOffset, modelSize, assetLength);
        ValidateSubfile("scene", sceneOffset, sceneSize, assetLength);
        if (vramSize < AddressableTexturePageBytes)
            throw new InvalidDataException($"{level.DisplayName} VRAM/SPU subfile is only 0x{vramSize:X}; retail needs the first 0x80000 texture bytes.");

        int[] modelOffsets = new int[64];
        ushort[] modelIndices = new ushort[64];
        int[] dragonOffsets = new int[6];
        int[] dragonLengths = new int[6];
        for (int index = 0; index < dragonOffsets.Length; index++)
        {
            dragonOffsets[index] = checked((int)ReadUInt32(header, 0x20 + (index * 8)));
            dragonLengths[index] = checked((int)ReadUInt32(header, 0x24 + (index * 8)));
        }
        for (int index = 0; index < 64; index++)
        {
            modelOffsets[index] = checked((int)ReadUInt32(header, 0x50 + (index * 4)));
            modelIndices[index] = ReadUInt16(header, 0x150 + (index * 2));
        }

        byte[] texturePagesSubfile = DiscImage.ReadFileBytes(
            stream,
            layout,
            WadLba,
            assetOffset + vramOffset,
            vramSize);
        return new LevelAsset(
            assetOffset,
            assetLength,
            vramOffset,
            vramSize,
            dataOffset,
            modelOffset,
            sceneOffset,
            texturePagesSubfile.AsSpan(0, AddressableTexturePageBytes).ToArray(),
            Convert.ToHexString(SHA256.HashData(texturePagesSubfile)),
            DiscImage.ReadFileBytes(stream, layout, WadLba, assetOffset + dataOffset, dataSize),
            DiscImage.ReadFileBytes(stream, layout, WadLba, assetOffset + modelOffset, modelSize),
            DiscImage.ReadFileBytes(stream, layout, WadLba, assetOffset + sceneOffset, sceneSize),
            modelOffsets,
            modelIndices,
            dragonOffsets,
            dragonLengths);
    }

    private static void DecodeDragonCutsceneModels(
        LevelAsset asset,
        FileStream stream,
        DiscLayout layout,
        ushort[] ownership,
        ScopeState scope)
    {
        const int dragonHeaderBytes = 0x24;
        const int dragonModelPairOffset = 0x0C;
        const int spyroModelPairOffset = 0x14;
        int decodedBlobCount = 0;
        int decodedRootCount = 0;
        int unresolvedBefore = scope.UnresolvedFaceTableCount;
        for (int slot = 0; slot < asset.DragonOffsets.Length; slot++)
        {
            int blobOffset = asset.DragonOffsets[slot];
            int blobLength = asset.DragonLengths[slot];
            if (blobOffset == 0 && blobLength == 0)
                continue;
            if (blobOffset < LevelHeaderBytes ||
                blobLength < dragonHeaderBytes ||
                (long)blobOffset + blobLength > asset.AssetByteLength)
            {
                scope.Block(
                    $"Dragon cutscene slot {slot} has invalid level-entry range 0x{blobOffset:X}+0x{blobLength:X}.");
                continue;
            }

            byte[] blob = DiscImage.ReadFileBytes(
                stream,
                layout,
                WadLba,
                asset.AssetWadOffset + blobOffset,
                blobLength);
            if (ReadUInt32(blob, 0) != 0)
            {
                scope.Block($"Dragon cutscene slot {slot} has a nonzero WadDragonHeader sentinel.");
                continue;
            }

            decodedBlobCount++;
            foreach ((int pairOffset, ushort syntheticActorId, string label) in new[]
                     {
                         (dragonModelPairOffset, (ushort)510, "dragon"),
                         (spyroModelPairOffset, (ushort)511, "cutscene Spyro")
                     })
            {
                int modelOffset = checked((int)ReadUInt32(blob, pairOffset));
                int modelLength = checked((int)ReadUInt32(blob, pairOffset + 4));
                if (modelOffset < dragonHeaderBytes ||
                    modelLength < 16 ||
                    (long)modelOffset + modelLength > blob.Length)
                {
                    scope.Block(
                        $"Dragon cutscene slot {slot} {label} model has invalid blob-relative range 0x{modelOffset:X}+0x{modelLength:X}.");
                    continue;
                }

                scope.ModelCount++;
                ParseResidentModel(blob, modelOffset, syntheticActorId, ownership, scope);
                decodedRootCount++;
            }
        }

        if (scope.UnresolvedFaceTableCount > unresolvedBefore)
        {
            scope.Block(
                $"{scope.UnresolvedFaceTableCount - unresolvedBefore} dragon-cutscene model face table(s) did not end exactly under the retail renderer command grammar.");
        }
        scope.Note(
            $"Decoded {decodedRootCount} dragon/cutscene-Spyro model root(s) from {decodedBlobCount} nonempty LevelHeader m_Dragons blob(s). Retail loads these models without a separate VRAM upload, so their faces consume the active level texture page.");
    }

    private static void CloseOtherRuntimeConsumerScope(ScopeState scope)
    {
        scope.Note(
            "Source trace closure: the main and portal cyclorama renderers emit untextured RGB polygons; their sector records contain positions and vertex colors but no tpage/CLUT descriptor.");
        scope.Note(
            "All level-specific overlay sources were audited for the shared texture globals, generic textured emitters, and LoadImage/StoreImage/MoveImage calls. No overlay owns source-bound level texture bytes.");
        scope.Note(
            "Flight and pause/results paths temporarily capture the framebuffer into the right half of VRAM and restore/reload it as a transient render target. They do not read a source-bound pixel/CLUT range from the level upload.");
        scope.Note(
            "The remaining generic textured consumers resolve through the decoded terrain records, resident/PETE/dragon model face tables, particle records, or LevelSceneHeader Tiledefs. Texture animation and scrolling mutate those terrain records and are enforced separately by NativeTerrainTextureRuntimeControlScanner.");
    }

    private static TerrainDecodeResult DecodeTerrain(
        byte[] levelData,
        ushort[] ownership,
        ScopeState lq,
        ScopeState leading,
        ScopeState normal,
        ScopeState close)
    {
        if (!TryReadTerrainHeader(levelData, out int textureCount, out int highTableOffset, out string failure))
        {
            lq.Block(failure);
            leading.Block(failure);
            normal.Block(failure);
            close.Block(failure);
            return new TerrainDecodeResult(0, 0, []);
        }

        SortedDictionary<int, int> closeSides = [];
        int rotatedHq = 0;
        for (int textureId = 0; textureId < textureCount; textureId++)
        {
            MarkTerrainTextureRecord(
                levelData,
                textureId,
                highTableOffset,
                ownership,
                lq,
                leading,
                normal,
                close,
                closeSides,
                ref rotatedHq);
        }

        lq.Note("Decoded both 32x32 4-bpp TexLq descriptors for every terrain record, including all sixteen palette rows.");
        leading.Note("Decoded the leading row as the byte-identical 4-bpp TexLq/sprite alias, including all sixteen distance-palette rows.");
        normal.Note("Decoded four normal HQ descriptors per terrain record using raw extent-derived square footprints.");
        close.Note("Decoded sixteen close HQ descriptors per terrain record. Retail close footprints are commonly 16x16, not universally 32x32.");
        return new TerrainDecodeResult(textureCount, rotatedHq, closeSides);
    }

    private static void MarkTerrainTextureRecord(
        byte[] levelData,
        int textureId,
        int highTableOffset,
        ushort[] ownership,
        ScopeState lq,
        ScopeState leading,
        ScopeState normal,
        ScopeState close,
        SortedDictionary<int, int> closeSides,
        ref int rotatedHq)
    {
        int lowOffset = checked(8 + (textureId * TerrainLowRecordBytes));
        MarkLqDescriptor(levelData.AsSpan(lowOffset, 8), ownership, lq, $"terrain {textureId} LQ 0");
        MarkLqDescriptor(levelData.AsSpan(lowOffset + 8, 8), ownership, lq, $"terrain {textureId} LQ 1");

        int highOffset = checked(highTableOffset + (textureId * TerrainHighRecordBytes));
        // The first row of the 168-byte high-detail record is not an HQ
        // 8-bpp descriptor. Retail keeps it byte-identical to both TexLq
        // rows and the renderer treats the three rows as one 4-bpp
        // low-detail/sprite alias. Decode it with the LQ grammar so its
        // packed pixels and all sixteen distance-palette rows are not
        // misclassified as a 512-byte HQ CLUT.
        MarkLqDescriptor(
            levelData.AsSpan(highOffset, 8),
            ownership,
            leading,
            $"terrain {textureId} leading LQ/sprite alias");

        for (int descriptor = 0; descriptor < 4; descriptor++)
        {
            int offset = highOffset + 8 + (descriptor * 8);
            MarkHqDescriptor(levelData.AsSpan(offset, 8), ownership, normal, $"terrain {textureId} normal {descriptor}", out int side);
            if (((levelData[offset + 7] >> 4) & 7) != 0)
                rotatedHq++;
            if (side <= 0)
                normal.Block($"Texture {textureId}'s normal HQ descriptor {descriptor} has an invalid footprint.");
        }

        for (int descriptor = 0; descriptor < 16; descriptor++)
        {
            int offset = highOffset + 40 + (descriptor * 8);
            MarkHqDescriptor(levelData.AsSpan(offset, 8), ownership, close, $"terrain {textureId} close {descriptor}", out int side);
            if (((levelData[offset + 7] >> 4) & 7) != 0)
                rotatedHq++;
            closeSides[side] = closeSides.TryGetValue(side, out int count) ? count + 1 : 1;
            if (side <= 0)
                close.Block($"Texture {textureId}'s close HQ descriptor {descriptor} has an invalid footprint.");
        }
    }

    private static bool TryReadTerrainHeader(byte[] levelData, out int textureCount, out int highTableOffset, out string failure)
    {
        textureCount = 0;
        highTableOffset = 0;
        if (levelData.Length < 8)
        {
            failure = "Level data is too short for the terrain texture component.";
            return false;
        }
        int componentLength;
        try
        {
            componentLength = checked((int)ReadUInt32(levelData, 0));
            textureCount = checked((int)ReadUInt32(levelData, 4));
            int expected = checked(8 + textureCount * (TerrainLowRecordBytes + TerrainHighRecordBytes));
            if (textureCount <= 0 || componentLength != expected || componentLength > levelData.Length)
            {
                failure = $"Terrain component length 0x{componentLength:X} does not match {textureCount} complete LQ/HQ records (expected 0x{expected:X}).";
                return false;
            }
            highTableOffset = checked(8 + textureCount * TerrainLowRecordBytes);
        }
        catch (OverflowException)
        {
            failure = "Terrain texture count overflows the component bounds.";
            return false;
        }
        failure = "";
        return true;
    }

    private static IEnumerable<int> EnumerateHqDescriptorOffsets(int highOffset)
    {
        yield return highOffset;
        for (int index = 0; index < 4; index++)
            yield return highOffset + 8 + (index * 8);
        for (int index = 0; index < 16; index++)
            yield return highOffset + 40 + (index * 8);
    }

    private static void MarkHqDescriptor(
        ReadOnlySpan<byte> raw,
        ushort[] ownership,
        ScopeState scope,
        string owner,
        out int side)
    {
        scope.DescriptorCount++;
        side = DeriveHqSide(raw);
        if (side is < 1 or > 256)
        {
            scope.Block($"{owner} has invalid HQ side length {side}.");
            scope.ExternalDescriptorCount++;
            return;
        }

        int region = raw[6];
        int orientation = (raw[7] >> 4) & 7;
        int[] matrix = TextureDescriptorMatrices[orientation];
        int startX = ((region * 128) % 2048) + raw[0];
        int startY = ((region & 0x10) != 0 ? 256 : 0) + raw[1];
        if (matrix[0] < 0 || matrix[1] < 0)
            startX += side - 1;
        if (matrix[2] < 0 || matrix[3] < 0)
            startY += side - 1;

        DescriptorMapCounter counter = new();
        for (int y = 0; y < side; y++)
        {
            for (int x = 0; x < side; x++)
            {
                int fullX = startX + (x * matrix[0]) + (y * matrix[1]);
                int fullY = startY + (x * matrix[2]) + (y * matrix[3]);
                counter.Add(MarkFullByte(ownership, fullX, fullY, scope.Bit));
            }
        }

        ushort clut = BinaryPrimitives.ReadUInt16LittleEndian(raw.Slice(2, 2));
        int clutXWord = (clut & 0x3F) * 16;
        int clutY = (clut >> 6) & 0x1FF;
        for (int index = 0; index < 512; index++)
            counter.Add(MarkFullByte(ownership, (clutXWord * 2) + index, clutY, scope.Bit));
        scope.Classify(counter);
    }

    private static int DeriveHqSide(ReadOnlySpan<byte> raw) =>
        Math.Max(Math.Abs(raw[4] - raw[0]), Math.Abs(raw[5] - raw[1])) + 1;

    private static void MarkLqDescriptor(
        ReadOnlySpan<byte> raw,
        ushort[] ownership,
        ScopeState scope,
        string owner)
    {
        scope.DescriptorCount++;
        const int side = 32;
        int region = raw[6];
        int orientation = (raw[7] >> 4) & 7;
        int[] matrix = TextureDescriptorMatrices[orientation];
        int startTexelX = 2048 + ((region * 256) % 2048) + raw[0];
        int startY = ((region & 0x10) != 0 ? 256 : 0) + raw[1];
        if (matrix[0] < 0 || matrix[1] < 0)
            startTexelX += side - 1;
        if (matrix[2] < 0 || matrix[3] < 0)
            startY += side - 1;

        DescriptorMapCounter counter = new();
        for (int y = 0; y < side; y++)
        {
            for (int x = 0; x < side; x++)
            {
                int fullTexelX = startTexelX + (x * matrix[0]) + (y * matrix[1]);
                int fullY = startY + (x * matrix[2]) + (y * matrix[3]);
                counter.Add(MarkFullByte(ownership, fullTexelX / 2, fullY, scope.Bit));
            }
        }

        int paletteFullByteX = raw[2] * 32;
        int paletteY = raw[3] * 4;
        // Spyro 1 retains sixteen adjacent 16-color LQ palettes for distance
        // fading. Protect every row, not merely the first visible row.
        for (int row = 0; row < 16; row++)
        {
            for (int index = 0; index < 32; index++)
                counter.Add(MarkFullByte(ownership, paletteFullByteX + index, paletteY + row, scope.Bit));
        }
        if (counter.Mapped == 0)
            scope.Note($"{owner} maps outside the level-loaded right-half VRAM page and consumes no byte in subfile 0.");
        scope.Classify(counter);
    }

    private static void DecodeParticles(byte[] levelData, ushort[] ownership, ScopeState scope)
    {
        if (!TryLocateParticleComponent(levelData, out int componentOffset, out int componentLength, out string failure))
        {
            scope.Block(failure);
            return;
        }
        int end = componentOffset + componentLength;
        if (componentLength < 8)
        {
            scope.Block("Particle component is too short for its count.");
            return;
        }
        int count = checked((int)ReadUInt32(levelData, componentOffset + 4));
        if (count is < 0 or > 4096)
        {
            scope.Block($"Particle descriptor count {count} is invalid.");
            return;
        }
        int cursor = componentOffset + 8;
        for (int index = 0; index < count; index++)
        {
            if (cursor + 4 > end)
            {
                scope.Block($"Particle record {index} header exceeds the component.");
                return;
            }
            ushort type = ReadUInt16(levelData, cursor);
            ushort payloadLength = ReadUInt16(levelData, cursor + 2);
            if (payloadLength < 8 || cursor + 4 + payloadLength > end)
            {
                scope.Block($"Particle record {index} has invalid payload length {payloadLength}.");
                return;
            }
            MarkTileDescriptor(levelData.AsSpan(cursor + 4, 8), ownership, scope, $"particle type {type}");
            cursor += 4 + payloadLength;
        }
        scope.Note($"Validated the component chain and enumerated {count} particle texture records (type, payload length, 8-byte ParticleTexture payload). Renderer type values are retained as data and do not alter the validated record grammar.");
    }

    private static bool TryLocateParticleComponent(byte[] levelData, out int offset, out int length, out string failure)
    {
        offset = 0;
        length = 0;
        int cursor = 0;
        string[] components = ["terrain texture", "environment", "occlusion", "special surface", "collision", "cyclorama"];
        foreach (string component in components)
        {
            if (!TryAdvanceComponent(levelData, ref cursor, component, out failure))
                return false;
        }
        if (cursor + 4 > levelData.Length)
        {
            failure = "Portal count is outside level data while locating particle textures.";
            return false;
        }
        int portalCount = checked((int)ReadUInt32(levelData, cursor));
        if (portalCount is < 0 or > 64)
        {
            failure = $"Portal count {portalCount} is invalid while locating particle textures.";
            return false;
        }
        cursor += 4;
        for (int portal = 0; portal < portalCount; portal++)
        {
            if (cursor + 8 > levelData.Length)
            {
                failure = $"Portal {portal} header is outside level data.";
                return false;
            }
            int pointCount = checked((int)ReadUInt32(levelData, cursor + 4));
            if (pointCount is < 1 or > 4096)
            {
                failure = $"Portal {portal} point count {pointCount} is invalid.";
                return false;
            }
            long skyComponent = cursor + 0x40L + ((pointCount - 1L) * 12L);
            if (skyComponent < 0 || skyComponent > int.MaxValue)
            {
                failure = $"Portal {portal} sky component offset overflows.";
                return false;
            }
            cursor = (int)skyComponent;
            if (!TryAdvanceComponent(levelData, ref cursor, $"portal {portal} cyclorama", out failure))
                return false;
        }
        if (cursor + 4 > levelData.Length)
        {
            failure = "Particle component header is outside level data.";
            return false;
        }
        int componentLength = checked((int)ReadUInt32(levelData, cursor));
        if (componentLength < 4 || (long)cursor + componentLength > levelData.Length)
        {
            failure = $"Particle component length 0x{componentLength:X} is invalid.";
            return false;
        }
        offset = cursor;
        length = componentLength;
        failure = "";
        return true;
    }

    private static bool TryAdvanceComponent(byte[] data, ref int cursor, string name, out string failure)
    {
        if (cursor < 0 || cursor + 4 > data.Length)
        {
            failure = $"{name} component header is outside level data.";
            return false;
        }
        int length = checked((int)ReadUInt32(data, cursor));
        if (length < 4 || (long)cursor + length > data.Length)
        {
            failure = $"{name} component length 0x{length:X} is invalid at 0x{cursor:X}.";
            return false;
        }
        cursor += length;
        failure = "";
        return true;
    }

    private static void DecodeResidentModels(LevelAsset asset, ushort[] ownership, ScopeState scope)
    {
        HashSet<int> parsedRoots = [];
        for (int slot = 1; slot < asset.ModelOffsets.Length; slot++)
        {
            int absoluteOffset = asset.ModelOffsets[slot];
            if (absoluteOffset <= 0)
                break;
            int localOffset = absoluteOffset - asset.ModelDataOffset;
            scope.ModelCount++;
            if (localOffset < 0 || localOffset + 16 > asset.Models.Length)
            {
                scope.Block($"Resident model slot {slot} (actor {asset.ModelIndices[slot]}) points outside m_ModelDataOffset/m_ModelDataSize.");
                continue;
            }
            if (!parsedRoots.Add(localOffset))
                continue;
            ParseResidentModel(asset.Models, localOffset, asset.ModelIndices[slot], ownership, scope);
        }
        if (scope.UnresolvedFaceTableCount > 0)
        {
            scope.Block(
                $"{scope.UnresolvedFaceTableCount} animated normal/LP face tables did not end exactly under the retail renderer command grammar; every remaining page byte stays unknown/protected.");
        }
        scope.Note("Enumerated every positive LevelHeader model offset from slot 1 onward, every animated-model root, and every unique normal/LP face-table pointer reachable from those animations.");
        scope.Note("Animated face tables use the retail renderer's per-command grammar: nonnegative control words are 8-byte untextured or 20-byte textured triangles; negative controls are 12-byte untextured or 24-byte textured quads, except bit 0x4 selects a 20-byte textured special command. Textured descriptors are the final three words and the table byte count must end exactly.");
        scope.Note("Simple-model source closure: header byte 1 is the face count, the +0x0C root-relative pointer addresses exactly count * 8 bytes, and the simple renderer emits only untextured primitives. Simple faces therefore consume no indexed pixel or CLUT bytes.");
    }

    private static void DecodeSharedPeteModels(
        LevelAsset levelAsset,
        FileStream stream,
        DiscLayout layout,
        ushort[] ownership,
        ScopeState player,
        ScopeState hudGlobal)
    {
        SharedPeteModelAsset asset;
        try
        {
            asset = LoadSharedPeteModelAsset(stream, layout);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or OverflowException)
        {
            string blocker = $"Shared PETE.WAD model enumeration failed: {ex.Message}";
            player.Block(blocker);
            hudGlobal.Block(blocker);
            return;
        }

        HashSet<int> playerRoots = [];
        HashSet<int> hudRoots = [];
        bool foundSpyro = false;
        foreach (SharedPeteModelRoot root in asset.Roots)
        {
            ScopeState scope = root.ActorId == 0 ? player : hudGlobal;
            HashSet<int> parsedRoots = root.ActorId == 0 ? playerRoots : hudRoots;
            scope.ModelCount++;
            foundSpyro |= root.ActorId == 0;
            if (root.LocalOffset < 0 || root.LocalOffset + 16 > asset.Models.Length)
            {
                scope.Block($"Shared PETE.WAD actor {root.ActorId} points outside the runtime-copied model block.");
                continue;
            }
            if (!parsedRoots.Add(root.LocalOffset))
                continue;
            ParseResidentModel(asset.Models, root.LocalOffset, root.ActorId, ownership, scope);
        }

        DecodeLevelSpyroAnimationFaces(levelAsset, asset, ownership, player);

        if (!foundSpyro)
            player.Block("Shared PETE.WAD did not expose the required actor-0 Spyro model root.");
        if (hudGlobal.ModelCount == 0)
            hudGlobal.Block("Shared PETE.WAD exposed no HUD/gem/text model roots.");
        if (player.UnresolvedFaceTableCount > 0)
        {
            player.Block(
                $"{player.UnresolvedFaceTableCount} shared Spyro face tables did not end exactly under the retail renderer command grammar.");
        }
        if (hudGlobal.UnresolvedFaceTableCount > 0)
        {
            hudGlobal.Block(
                $"{hudGlobal.UnresolvedFaceTableCount} shared HUD/gem/text face tables did not end exactly under the retail renderer command grammar.");
        }

        int requiredPeteExtent = checked(PeteModelDataOffset + asset.RuntimeCopyLength);
        string lengthNote = requiredPeteExtent > asset.DeclaredWadLength
            ? $" Retail copies 0x{asset.RuntimeCopyLength:X} model-data bytes from PETE+0x800 (requiring PETE extent 0x{requiredPeteExtent:X}) although the WAD entry is 0x{asset.DeclaredWadLength:X}; the scanner mirrors that source-observed copy extent."
            : "";
        player.Note(
            $"Enumerated the PETE.WAD actor-0 Spyro root and every unique normal/LP face table reachable from its {player.AnimatedModelCount} animated model(s).{lengthNote}");
        hudGlobal.Note(
            $"Enumerated {hudGlobal.ModelCount} PETE.WAD HUD/gem/key/number/letter roots and every unique normal/LP face table reachable from them.{lengthNote}");
    }

    private static SharedPeteModelAsset LoadSharedPeteModelAsset(FileStream stream, DiscLayout layout)
    {
        byte[] peteEntry = DiscImage.ReadFileBytes(
            stream,
            layout,
            WadLba,
            WadHeaderPeteEntryOffset,
            8);
        long peteWadOffset = ReadUInt32(peteEntry, 0);
        int declaredWadLength = checked((int)ReadUInt32(peteEntry, 4));
        if (peteWadOffset <= 0 || declaredWadLength < PeteModelDataOffset)
            throw new InvalidDataException("WAD.WAD has an invalid PETE.WAD offset/length entry.");

        byte[] peteHeader = DiscImage.ReadFileBytes(
            stream,
            layout,
            WadLba,
            peteWadOffset,
            PeteModelTableBytes);
        int runtimeCopyLength = checked((int)ReadUInt32(peteHeader, 0));
        if (runtimeCopyLength <= 0 || runtimeCopyLength > 0x800000)
            throw new InvalidDataException($"PETE.WAD has invalid runtime model-data size 0x{runtimeCopyLength:X}.");

        List<SharedPeteModelRoot> roots = [];
        HashSet<ushort> actorIds = [];
        for (int index = 0; index < 64; index++)
        {
            int row = 4 + (index * 8);
            int modelOffset = checked((int)ReadUInt32(peteHeader, row));
            if (modelOffset == 0)
                break;
            int actorIdValue = checked((int)ReadUInt32(peteHeader, row + 4));
            if (actorIdValue is < 0 or > ushort.MaxValue)
                throw new InvalidDataException($"PETE.WAD model row {index} has invalid actor id {actorIdValue}.");
            if (modelOffset < PeteModelDataOffset || modelOffset - PeteModelDataOffset >= runtimeCopyLength)
            {
                throw new InvalidDataException(
                    $"PETE.WAD model row {index} points to 0x{modelOffset:X}, outside its runtime-copied model block.");
            }

            ushort actorId = (ushort)actorIdValue;
            if (!actorIds.Add(actorId))
                throw new InvalidDataException($"PETE.WAD repeats shared actor id {actorId}.");
            roots.Add(new SharedPeteModelRoot(actorId, modelOffset - PeteModelDataOffset));
        }
        if (roots.Count == 0)
            throw new InvalidDataException("PETE.WAD has no shared model roots.");

        byte[] models = DiscImage.ReadFileBytes(
            stream,
            layout,
            WadLba,
            peteWadOffset + PeteModelDataOffset,
            runtimeCopyLength);
        SharedPeteModelRoot spyroRoot = roots.FirstOrDefault(root => root.ActorId == 0)
            ?? throw new InvalidDataException("PETE.WAD has no actor-0 Spyro root.");
        if (spyroRoot.LocalOffset + 0x38 > models.Length)
            throw new InvalidDataException("PETE.WAD actor-0 Spyro root is truncated.");
        int spyroDataRelative = checked((int)ReadUInt32(models, spyroRoot.LocalOffset + 0x34));
        int spyroModelDataOffset = checked(spyroRoot.LocalOffset + spyroDataRelative);
        if (spyroModelDataOffset < 0 || spyroModelDataOffset >= models.Length)
            throw new InvalidDataException("PETE.WAD actor-0 Spyro model-data pointer is outside the runtime-copied block.");
        return new SharedPeteModelAsset(
            models,
            roots,
            declaredWadLength,
            runtimeCopyLength,
            spyroModelDataOffset);
    }

    private static void DecodeLevelSpyroAnimationFaces(
        LevelAsset levelAsset,
        SharedPeteModelAsset peteAsset,
        ushort[] ownership,
        ScopeState player)
    {
        int absoluteOffset = levelAsset.ModelOffsets[0];
        int cursor = absoluteOffset - levelAsset.ModelDataOffset;
        if (absoluteOffset <= 0 || cursor < 0 || cursor + 4 > levelAsset.Models.Length)
        {
            player.Block("LevelHeader model slot 0 does not point to the level-specific Spyro animation stream.");
            return;
        }

        HashSet<int> faceTables = [];
        HashSet<int> animationIndexes = [];
        int animationEntryCount = 0;
        bool terminated = false;
        while (cursor + 4 <= levelAsset.Models.Length)
        {
            int animationIndex = ReadInt32(levelAsset.Models, cursor);
            if (animationIndex < 0)
            {
                terminated = true;
                break;
            }
            if (cursor + 8 > levelAsset.Models.Length)
            {
                player.Block("A level-specific Spyro animation row is truncated before its byte length.");
                return;
            }
            int animationLength = ReadInt32(levelAsset.Models, cursor + 4);
            int animationStart = cursor + 8;
            if (animationIndex > 1024 ||
                !animationIndexes.Add(animationIndex) ||
                animationLength < 0x24 ||
                animationStart + (long)animationLength > levelAsset.Models.Length)
            {
                player.Block(
                    $"Level-specific Spyro animation {animationIndex} has an invalid or duplicate 0x{animationLength:X}-byte record.");
                return;
            }

            int faceRelative = checked((int)ReadUInt32(levelAsset.Models, animationStart + 0x14));
            int faceStart = checked(peteAsset.SpyroModelDataOffset + faceRelative);
            if (faceTables.Add(faceStart))
                ParseFaceTable(peteAsset.Models, faceStart, 0, $"level animation {animationIndex}", ownership, player);

            animationEntryCount++;
            cursor = checked(animationStart + animationLength);
        }

        if (!terminated)
        {
            player.Block("The level-specific Spyro animation stream has no negative terminator.");
            return;
        }
        if (animationEntryCount == 0)
        {
            player.Block("The level-specific Spyro animation stream contains no animation records.");
            return;
        }

        player.Note(
            $"Enumerated {animationEntryCount} level-specific Spyro animation rows from LevelHeader model slot 0 and {faceTables.Count} unique face tables relative to PETE.WAD actor-0 model data.");
    }

    private static void ParseResidentModel(byte[] models, int modelStart, ushort actorId, ushort[] ownership, ScopeState scope)
    {
        int animationCount = ReadInt32(models, modelStart);
        if (animationCount < 0)
        {
            scope.SimpleModelCount++;
            ParseSimpleModel(models, modelStart, actorId, scope);
            return;
        }
        scope.AnimatedModelCount++;
        if (animationCount > 1024 || modelStart + 0x38L + (animationCount * 4L) > models.Length)
        {
            scope.Block($"Resident actor {actorId} has invalid animation count {animationCount}.");
            return;
        }
        int dataRelative = checked((int)ReadUInt32(models, modelStart + 0x34));
        int modelData = modelStart + dataRelative;
        if (modelData < 0 || modelData >= models.Length)
        {
            scope.Block($"Resident actor {actorId} has an invalid model-data relative pointer 0x{dataRelative:X}.");
            return;
        }
        HashSet<int> faceTables = [];
        for (int animation = 0; animation < animationCount; animation++)
        {
            int animationRelative = ReadInt32(models, modelStart + 0x38 + (animation * 4));
            if (animationRelative == -1)
                continue;
            int animationStart = modelStart + animationRelative;
            if (animationStart < 0 || animationStart + 0x24 > models.Length)
            {
                scope.Block($"Resident actor {actorId} animation {animation} points outside the model subfile.");
                continue;
            }
            int faceRelative = checked((int)ReadUInt32(models, animationStart + 0x14));
            int faceStart = modelData + faceRelative;
            if (faceTables.Add(faceStart))
                ParseFaceTable(models, faceStart, actorId, "normal", ownership, scope);
            int lowFaceRelative = checked((int)ReadUInt32(models, animationStart + 0x1C));
            if (lowFaceRelative != 0)
            {
                int lowFaceStart = modelData + lowFaceRelative;
                if (faceTables.Add(lowFaceStart))
                    ParseFaceTable(models, lowFaceStart, actorId, "low-poly", ownership, scope);
            }
        }
    }

    private static void ParseSimpleModel(byte[] models, int modelStart, ushort actorId, ScopeState scope)
    {
        if (modelStart < 0 || modelStart + 16 > models.Length)
        {
            scope.Block($"Resident simple actor {actorId} header points outside the model subfile.");
            return;
        }

        int faceCount = models[modelStart + 1];
        int faceRelative = checked((int)ReadUInt32(models, modelStart + 0x0C));
        int faceStart;
        int faceEnd;
        try
        {
            faceStart = checked(modelStart + faceRelative);
            faceEnd = checked(faceStart + (faceCount * 8));
        }
        catch (OverflowException)
        {
            scope.Block($"Resident simple actor {actorId} face range overflows its model subfile.");
            return;
        }

        if (faceStart < 0 || faceEnd < faceStart || faceEnd > models.Length)
        {
            scope.Block(
                $"Resident simple actor {actorId} has {faceCount} fixed 8-byte faces at invalid root-relative pointer 0x{faceRelative:X}.");
        }
    }

    private static void ParseFaceTable(
        byte[] models,
        int faceStart,
        ushort actorId,
        string tier,
        ushort[] ownership,
        ScopeState scope)
    {
        scope.FaceTableCount++;
        if (faceStart < 0 || faceStart + 4 > models.Length)
        {
            scope.RecordFaceFailure($"Resident actor {actorId} {tier} face table points outside the model subfile.");
            return;
        }
        int bodyLength = checked((int)ReadUInt32(models, faceStart));
        if ((bodyLength & 3) != 0 || bodyLength < 0 || bodyLength > 0x20000 || faceStart + 4L + bodyLength > models.Length)
        {
            scope.RecordFaceFailure($"Resident actor {actorId} {tier} face table at 0x{faceStart:X} has invalid byte length 0x{bodyLength:X}.");
            return;
        }
        int cursor = faceStart + 4;
        int end = cursor + bodyLength;
        if (!TryDecodeRendererFaceRecords(models, cursor, end, out IReadOnlyList<FaceRecord> records))
        {
            scope.RecordFaceFailure(
                $"Resident actor {actorId} {tier} face table at 0x{faceStart:X} does not end exactly under the retail renderer command grammar.");
            return;
        }

        foreach (FaceRecord record in records.Where(record => record.TextureDescriptorOffset.HasValue))
        {
            int descriptorOffset = record.TextureDescriptorOffset!.Value;
            uint word2 = ReadUInt32(models, descriptorOffset);
            uint word3 = ReadUInt32(models, descriptorOffset + 4);
            uint word4 = ReadUInt32(models, descriptorOffset + 8);
            byte[] descriptor = new byte[8];
            descriptor[0] = (byte)word2;
            descriptor[1] = (byte)(word2 >> 8);
            BinaryPrimitives.WriteUInt16LittleEndian(descriptor.AsSpan(2, 2), (ushort)(word2 >> 16));
            descriptor[4] = (byte)word3;
            descriptor[5] = (byte)(word3 >> 8);
            BinaryPrimitives.WriteUInt16LittleEndian(descriptor.AsSpan(6, 2), (ushort)(word3 >> 16));
            MarkFaceTexture(descriptor, word4, ownership, scope, $"resident actor {actorId} face 0x{record.Offset:X}");
            scope.TexturedFaceCount++;
        }
    }

    private static bool TryDecodeRendererFaceRecords(
        byte[] models,
        int start,
        int end,
        out IReadOnlyList<FaceRecord> records)
    {
        List<FaceRecord> result = [];
        int cursor = start;
        while (cursor < end)
        {
            int remaining = end - cursor;
            if (remaining < 8)
            {
                records = Array.Empty<FaceRecord>();
                return false;
            }

            uint control = ReadUInt32(models, cursor);
            int recordBytes;
            int? textureDescriptorOffset = null;
            if ((control & 0x80000000u) == 0)
            {
                // Retail renderer triangle command: bit 0x2 appends the
                // final three texture words to the 8-byte base command.
                bool texturedTriangle = (control & 0x2u) != 0;
                recordBytes = texturedTriangle ? 20 : 8;
                if (texturedTriangle)
                    textureDescriptorOffset = cursor + 8;
            }
            else if ((control & 0x4u) != 0)
            {
                // Retail renderer special/billboard command. It is always a
                // 20-byte textured command with its descriptor at +8.
                recordBytes = 20;
                textureDescriptorOffset = cursor + 8;
            }
            else
            {
                // Retail renderer quad command: bit 0x2 appends the final
                // three texture words to the 12-byte base command.
                bool texturedQuad = (control & 0x2u) != 0;
                recordBytes = texturedQuad ? 24 : 12;
                if (texturedQuad)
                    textureDescriptorOffset = cursor + 12;
            }
            if (recordBytes > remaining)
            {
                records = Array.Empty<FaceRecord>();
                return false;
            }
            result.Add(new FaceRecord(cursor, textureDescriptorOffset));
            cursor += recordBytes;
        }
        records = result;
        return cursor == end;
    }

    private static void MarkFaceTexture(
        ReadOnlySpan<byte> descriptor,
        uint word4,
        ushort[] ownership,
        ScopeState scope,
        string owner)
    {
        int[] u = [descriptor[0], descriptor[4], (byte)word4, (byte)(word4 >> 16)];
        int[] v = [descriptor[1], descriptor[5], (byte)(word4 >> 8), (byte)(word4 >> 24)];
        MarkPsxTextureRectangle(
            u.Min(),
            v.Min(),
            u.Max(),
            v.Max(),
            BinaryPrimitives.ReadUInt16LittleEndian(descriptor.Slice(2, 2)),
            BinaryPrimitives.ReadUInt16LittleEndian(descriptor.Slice(6, 2)),
            ownership,
            scope,
            owner);
    }

    private static void DecodeSceneTiledefs(
        byte[] scene,
        ushort[] ownership,
        ScopeState player,
        ScopeState hudGlobal)
    {
        const int tileOffset = 0x10;
        const int tileCount = 15;
        if (scene.Length < tileOffset + tileCount * 8)
        {
            player.Block("Level scene is too short for the complete 15-Tiledef LevelSceneHeader.");
            hudGlobal.Block("Level scene is too short for the complete 15-Tiledef LevelSceneHeader.");
            return;
        }
        string[] labels =
        [
            "flame", "shadow", "unused", "orb", "egg-0", "egg-1", "egg-2", "egg-3", "egg-4", "egg-5", "egg-6", "egg-7", "egg-8", "superflame", "specular-metal"
        ];
        for (int index = 0; index < tileCount; index++)
        {
            ScopeState scope = index is 0 or 13 ? player : hudGlobal;
            MarkTileDescriptor(scene.AsSpan(tileOffset + index * 8, 8), ownership, scope, $"scene Tiledef {labels[index]}");
        }
        player.Note("Protected level-local flame and superflame Tiledefs from LevelSceneHeader.");
        hudGlobal.Note("Protected shadow, unused, orb/egg[10], and specular-metal Tiledefs from LevelSceneHeader.");
    }

    private static void MarkTileDescriptor(
        ReadOnlySpan<byte> descriptor,
        ushort[] ownership,
        ScopeState scope,
        string owner)
    {
        MarkPsxTextureRectangle(
            Math.Min(descriptor[0], descriptor[4]),
            Math.Min(descriptor[1], descriptor[5]),
            Math.Max(descriptor[0], descriptor[4]),
            Math.Max(descriptor[1], descriptor[5]),
            BinaryPrimitives.ReadUInt16LittleEndian(descriptor.Slice(2, 2)),
            BinaryPrimitives.ReadUInt16LittleEndian(descriptor.Slice(6, 2)),
            ownership,
            scope,
            owner);
    }

    private static void MarkPsxTextureRectangle(
        int minU,
        int minV,
        int maxU,
        int maxV,
        ushort clut,
        ushort tpage,
        ushort[] ownership,
        ScopeState scope,
        string owner)
    {
        scope.DescriptorCount++;
        if (minU < 0 || minV < 0 || maxU > 255 || maxV > 255 || minU > maxU || minV > maxV)
        {
            scope.Block($"{owner} has invalid UV bounds ({minU},{minV})..({maxU},{maxV}).");
            scope.ExternalDescriptorCount++;
            return;
        }

        int depth = (tpage >> 7) & 3;
        int pageWordX = (tpage & 0x0F) * 64;
        int pageY = (tpage & 0x10) != 0 ? 256 : 0;
        DescriptorMapCounter counter = new();
        for (int v = minV; v <= maxV; v++)
        {
            for (int u = minU; u <= maxU; u++)
            {
                int fullByteX = depth switch
                {
                    0 => (pageWordX * 2) + (u / 2),
                    1 => (pageWordX * 2) + u,
                    _ => (pageWordX * 2) + (u * 2)
                };
                counter.Add(MarkFullByte(ownership, fullByteX, pageY + v, scope.Bit));
                if (depth >= 2)
                    counter.Add(MarkFullByte(ownership, fullByteX + 1, pageY + v, scope.Bit));
            }
        }

        if (depth < 2)
        {
            int clutBytes = depth == 0 ? 32 : 512;
            int clutXWord = (clut & 0x3F) * 16;
            int clutY = (clut >> 6) & 0x1FF;
            for (int index = 0; index < clutBytes; index++)
                counter.Add(MarkFullByte(ownership, (clutXWord * 2) + index, clutY, scope.Bit));
        }
        scope.Classify(counter);
    }

    private static bool MarkFullByte(ushort[] ownership, int fullByteX, int y, OwnershipBit bit)
    {
        int packedX = fullByteX - FullRightHalfByteX;
        if (packedX < 0 || packedX >= PackedRowBytes || y < 0 || y >= VramRows)
            return false;
        ownership[(y * PackedRowBytes) + packedX] |= (ushort)bit;
        return true;
    }

    private static int CountOwnedBytes(ushort[] ownership, OwnershipBit bit) =>
        bit == OwnershipBit.None ? 0 : ownership.Count(value => (value & (ushort)bit) != 0);

    private static UnknownSummary BuildUnknownSummary(byte[] texturePages, ushort[] ownership)
    {
        int nonZeroBytes = 0;
        List<(int Offset, int Length)> ranges = [];
        int cursor = 0;
        while (cursor < texturePages.Length)
        {
            if (ownership[cursor] != 0 || texturePages[cursor] == 0)
            {
                cursor++;
                continue;
            }
            int start = cursor;
            while (cursor < texturePages.Length && ownership[cursor] == 0 && texturePages[cursor] != 0)
                cursor++;
            int length = cursor - start;
            nonZeroBytes += length;
            ranges.Add((start, length));
        }
        byte[] digestBytes = new byte[ranges.Count * 8];
        for (int index = 0; index < ranges.Count; index++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(digestBytes.AsSpan(index * 8, 4), ranges[index].Offset);
            BinaryPrimitives.WriteInt32LittleEndian(digestBytes.AsSpan(index * 8 + 4, 4), ranges[index].Length);
        }
        return new UnknownSummary(
            nonZeroBytes,
            ranges.Count,
            Convert.ToHexString(SHA256.HashData(digestBytes)),
            ranges.Take(32).Select(range => new NativeTextureUnknownRangeSample(range.Offset, range.Length)).ToArray());
    }

    private static void ValidateSubfile(string name, int offset, int length, int assetLength)
    {
        if (offset < LevelHeaderBytes || length <= 0 || (long)offset + length > assetLength)
            throw new InvalidDataException($"Level asset {name} range 0x{offset:X}+0x{length:X} is invalid for 0x{assetLength:X} bytes.");
    }

    private static uint ReadUInt32(byte[] bytes, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));

    private static int ReadInt32(byte[] bytes, int offset) =>
        BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset, 4));

    private static ushort ReadUInt16(byte[] bytes, int offset) =>
        BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2));

    [Flags]
    private enum OwnershipBit : ushort
    {
        None = 0,
        TerrainLq = 1 << 0,
        TerrainLeading = 1 << 1,
        TerrainNormal = 1 << 2,
        TerrainClose = 1 << 3,
        Particles = 1 << 4,
        Residents = 1 << 5,
        Player = 1 << 6,
        HudGlobal = 1 << 7,
        Other = 1 << 8
    }

    private sealed class ScopeState
    {
        private static readonly IReadOnlyDictionary<string, OwnershipBit> Bits = new Dictionary<string, OwnershipBit>(StringComparer.Ordinal)
        {
            ["terrain-lq"] = OwnershipBit.TerrainLq,
            ["terrain-hq-leading"] = OwnershipBit.TerrainLeading,
            ["terrain-hq-normal"] = OwnershipBit.TerrainNormal,
            ["terrain-hq-close"] = OwnershipBit.TerrainClose,
            ["particle-tables"] = OwnershipBit.Particles,
            ["resident-actors-and-scenery"] = OwnershipBit.Residents,
            ["spyro-player"] = OwnershipBit.Player,
            ["hud-and-level-global"] = OwnershipBit.HudGlobal,
            ["other-runtime-consumers"] = OwnershipBit.Other
        };

        public ScopeState(string name)
        {
            Name = name;
            Bit = Bits[name];
        }

        public string Name { get; }
        public OwnershipBit Bit { get; }
        public int DescriptorCount { get; set; }
        public int MappedDescriptorCount { get; private set; }
        public int PartiallyMappedDescriptorCount { get; private set; }
        public int ExternalDescriptorCount { get; set; }
        public int ModelCount { get; set; }
        public int AnimatedModelCount { get; set; }
        public int SimpleModelCount { get; set; }
        public int FaceTableCount { get; set; }
        public int UnresolvedFaceTableCount { get; private set; }
        public int AmbiguousFaceTableCount { get; private set; }
        public int TexturedFaceCount { get; set; }
        public List<string> Blockers { get; } = [];
        public List<string> Notes { get; } = [];
        public bool IsComplete => Blockers.Count == 0;

        public void Classify(DescriptorMapCounter counter)
        {
            if (counter.Mapped == 0)
                ExternalDescriptorCount++;
            else if (counter.External == 0)
                MappedDescriptorCount++;
            else
                PartiallyMappedDescriptorCount++;
        }

        public void Block(string blocker)
        {
            if (!Blockers.Contains(blocker, StringComparer.Ordinal))
                Blockers.Add(blocker);
        }

        public void Note(string note)
        {
            if (!Notes.Contains(note, StringComparer.Ordinal))
                Notes.Add(note);
        }

        public void RecordFaceFailure(string detail)
        {
            UnresolvedFaceTableCount++;
            if (Notes.Count(note => note.StartsWith("Face-table sample:", StringComparison.Ordinal)) < 5)
                Notes.Add($"Face-table sample: {detail}");
        }

        public NativeTextureOwnershipScopeResult ToResult(int ownedByteCount) => new(
            Scope: Name,
            ProofState: IsComplete ? NativeTextureOwnershipProofState.ProvenEnumerated : NativeTextureOwnershipProofState.Unproven,
            DescriptorCount: DescriptorCount,
            MappedDescriptorCount: MappedDescriptorCount,
            PartiallyMappedDescriptorCount: PartiallyMappedDescriptorCount,
            ExternalDescriptorCount: ExternalDescriptorCount,
            ModelCount: ModelCount,
            AnimatedModelCount: AnimatedModelCount,
            SimpleModelCount: SimpleModelCount,
            FaceTableCount: FaceTableCount,
            UnresolvedFaceTableCount: UnresolvedFaceTableCount,
            AmbiguousFaceTableCount: AmbiguousFaceTableCount,
            TexturedFaceCount: TexturedFaceCount,
            OwnedByteCount: ownedByteCount,
            Blockers: Blockers.ToArray(),
            Notes: Notes.ToArray());
    }

    private sealed class DescriptorMapCounter
    {
        public int Mapped { get; private set; }
        public int External { get; private set; }

        public void Add(bool mapped)
        {
            if (mapped)
                Mapped++;
            else
                External++;
        }
    }

    private sealed record LevelAsset(
        long AssetWadOffset,
        int AssetByteLength,
        int VramOffset,
        int VramSize,
        int LevelDataOffset,
        int ModelDataOffset,
        int SceneOffset,
        byte[] TexturePages,
        string TexturePagesSubfileSha256,
        byte[] LevelData,
        byte[] Models,
        byte[] Scene,
        int[] ModelOffsets,
        ushort[] ModelIndices,
        int[] DragonOffsets,
        int[] DragonLengths);

    private sealed record SharedPeteModelAsset(
        byte[] Models,
        IReadOnlyList<SharedPeteModelRoot> Roots,
        int DeclaredWadLength,
        int RuntimeCopyLength,
        int SpyroModelDataOffset);

    private sealed record SharedPeteModelRoot(ushort ActorId, int LocalOffset);

    private sealed record TerrainDecodeResult(
        int TextureCount,
        int RotatedHqDescriptorCount,
        SortedDictionary<int, int> CloseSides);

    private sealed record UnknownSummary(
        int NonZeroByteCount,
        int RegionCount,
        string RangesSha256,
        IReadOnlyList<NativeTextureUnknownRangeSample> Samples);

    private sealed record FaceRecord(int Offset, int? TextureDescriptorOffset);
}
