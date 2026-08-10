using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Immutable capture of the three exact locked-source regions consumed or
/// preserved by the first ID65 composite compiler. It contains no path,
/// stream, slice plan, compiled slice, or publisher state.
/// </summary>
internal sealed class Id65AuthoringCompositeLockedSource
{
    private readonly byte[] _row80;
    private readonly byte[] _targetOverlay;
    private readonly byte[] _globalExecutable;

    internal Id65AuthoringCompositeLockedSource(
        byte[] row80,
        byte[] targetOverlay,
        byte[] globalExecutable)
    {
        _row80 = row80.ToArray();
        _targetOverlay = targetOverlay.ToArray();
        _globalExecutable = globalExecutable.ToArray();
        Row80Sha256 = Id65AuthoringCompositeCompiler.Hash(_row80);
        TargetOverlaySha256 = Id65AuthoringCompositeCompiler.Hash(_targetOverlay);
        GlobalExecutableSha256 = Id65AuthoringCompositeCompiler.Hash(_globalExecutable);
    }

    public string Row80Sha256 { get; }
    public string TargetOverlaySha256 { get; }
    public string GlobalExecutableSha256 { get; }
    public int Row80ByteLength => _row80.Length;
    public int TargetOverlayByteLength => _targetOverlay.Length;
    public int GlobalExecutableByteLength => _globalExecutable.Length;

    internal byte[] CopyRow80() => _row80.ToArray();
    internal byte[] CopyTargetOverlay() => _targetOverlay.ToArray();
    internal byte[] CopyGlobalExecutable() => _globalExecutable.ToArray();
}

internal sealed class Id65AuthoringCompositeManifest
{
    private readonly ReadOnlyCollection<Id65MobyPlacementIntent> _placements;

    internal Id65AuthoringCompositeManifest(
        Id65TerrainTriangleIntent? optionalTerrainTriangle,
        string textureWitnessSha256,
        string mobyBundleSha256,
        IEnumerable<Id65MobyPlacementIntent> placements)
    {
        OptionalTerrainTriangle = optionalTerrainTriangle;
        TextureWitnessSha256 = textureWitnessSha256;
        MobyBundleSha256 = mobyBundleSha256;
        _placements = Array.AsReadOnly(placements
            .OrderBy(item => item.TargetTrueIndex)
            .ToArray());
        CanonicalJson = JsonSerializer.Serialize(
            new
            {
                profileId = Id65AuthoringCompositeCompiler.ProfileId,
                mandatoryNegativeWindingV2 = true,
                optionalTerrainTriangle,
                privateTextureId = Id65V2NativeTextureCompositionCompiler.PrivateTextureId,
                textureWitnessSha256,
                mobyBundleSha256,
                placements = _placements
            },
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            });
        CanonicalSha256 = Id65AuthoringCompositeCompiler.Hash(Encoding.UTF8.GetBytes(CanonicalJson));
    }

    public string ProfileId => Id65AuthoringCompositeCompiler.ProfileId;
    public bool MandatoryNegativeWindingV2 => true;
    public Id65TerrainTriangleIntent? OptionalTerrainTriangle { get; }
    public string TextureWitnessSha256 { get; }
    public string MobyBundleSha256 { get; }
    public IReadOnlyList<Id65MobyPlacementIntent> Placements => _placements;
    public string CanonicalJson { get; }
    public string CanonicalSha256 { get; }
}

internal sealed record Id65AuthoringCompositeCompilerLimits(
    int ModelByteCapacity = 0x94800,
    int MaximumOutputUsedModelBytes = 0x94800,
    int MaximumNativeTextureId = 127,
    int MaximumObjectCount = 223,
    int MaximumActorRootCount = 64,
    int ActorSubfileEnd = 0x1D0000,
    int SceneSubfileEnd = 0x8800,
    int CollisionTreeCapacityBytes = 0x6A60,
    int CollisionBlocksCapacityBytes = 0x17984);

internal sealed record Id65AuthoringCompositeCompileRequest(
    Id65AuthoringCompositeLockedSource Source,
    Id65AuthoringCompositeManifest Manifest,
    Id65V2NativeTextureWitness TextureWitness,
    IReadOnlyList<Id65MobyDependencyBundleDescriptor> MobyBundles,
    IReadOnlyList<Id65MobyPlacementIntent> MobyPlacements,
    Id65AuthoringCompositeCompilerLimits? Limits = null);

internal sealed class Id65AuthoringCompositeOwnedRange
{
    private readonly byte[] _before;
    private readonly byte[] _after;

    internal Id65AuthoringCompositeOwnedRange(
        string stableId,
        string ownerId,
        int dataRelativeOffset,
        byte[] before,
        byte[] after)
    {
        StableId = stableId;
        OwnerId = ownerId;
        DataRelativeOffset = dataRelativeOffset;
        _before = before.ToArray();
        _after = after.ToArray();
        BeforeSha256 = Id65AuthoringCompositeCompiler.Hash(_before);
        AfterSha256 = Id65AuthoringCompositeCompiler.Hash(_after);
    }

    public string StableId { get; }
    public string OwnerId { get; }
    public int DataRelativeOffset { get; }
    public int ByteLength => _before.Length;
    public string BeforeSha256 { get; }
    public string AfterSha256 { get; }
    internal byte[] CopyBefore() => _before.ToArray();
    internal byte[] CopyAfter() => _after.ToArray();
    internal Id65AuthoringCompositeOwnedRange DeepCopy() =>
        new(StableId, OwnerId, DataRelativeOffset, _before, _after);
}

internal sealed record Id65AuthoringCompositeDiffRange(
    int DataRelativeOffset,
    int ByteLength,
    string OwnerStableId,
    string OwnerId,
    string BeforeSha256,
    string AfterSha256);

internal sealed record Id65AuthoringCompositeTerrainReadback(
    bool MandatoryNegativeWindingV2,
    bool OptionalTrianglePresent,
    int SectorIndex,
    int LowDetailVertexCount,
    int LowDetailFaceCount,
    int HighDetailVertexCount,
    int HighDetailFaceCount,
    int BaseCollisionTriangleIndex,
    string BaseCollisionTriangleHex,
    long BaseCollisionNormalZ,
    string EnvironmentSha256,
    string SectorSha256,
    string CollisionSha256,
    string CollisionTreeSha256,
    string CollisionBlocksSha256,
    int CollisionBlocksUsedBytes,
    int OcclusionGroupIndex,
    bool OcclusionContainsSector,
    Id65TerrainTriangleReadback? OptionalTriangle);

internal sealed record Id65AuthoringCompositeTextureReadback(
    string WitnessSha256,
    int SourceTextureCount,
    int OutputTextureCount,
    int PrivateTextureId,
    int DonorWadEntry,
    int DonorTextureId,
    string PrivateLowRowSha256,
    string PrivateHighRowSha256,
    int OutputTextureComponentBytes,
    string OutputTextureComponentSha256,
    string OutputTexturePagesSha256,
    string RenderFaceId,
    int SectorIndex,
    int LowDetailFaceIndex,
    int HighDetailFaceIndex,
    int OutputLowFaceModelOffset,
    int OutputHighFaceModelOffset,
    int OutputHighTextureWordModelOffset,
    string OutputLowFaceHex,
    string OutputHighFaceHex,
    int TemplateTextureId,
    int OutputTextureId,
    bool AddedTerrainFacePreservedT25);

internal sealed record Id65AuthoringCompositeCapacityReadback(
    int ModelByteCapacity,
    int SourceUsedModelBytes,
    int OutputUsedModelBytes,
    int OutputZeroTailBytes,
    int TextureCountBefore,
    int TextureCountAfter,
    int HighestTextureId,
    int SectorCount,
    int LowDetailVertexCount,
    int LowDetailFaceCount,
    int HighDetailVertexCount,
    int HighDetailFaceCount,
    int CollisionTriangleCount,
    int CollisionBlocksUsedBytes,
    int CollisionBlocksCapacityBytes,
    int ObjectCountBefore,
    int ObjectCountAfter,
    int ActorRootCountBefore,
    int ActorRootCountAfter,
    int ActorTailBytesRemaining,
    int FixupCountBefore,
    int FixupCountAfter,
    int SceneTailBytesRemaining);

/// <summary>
/// Immutable exact-afterimage rollback transaction. This type is produced
/// only from a compiled composite and is intentionally absent from every
/// authoring compile request.
/// </summary>
internal sealed class Id65AuthoringCompositeRollbackPlan
{
    private readonly byte[] _expectedAfterimage;
    private readonly byte[] _lockedSource;
    private readonly ReadOnlyCollection<Id65AuthoringCompositeOwnedRange> _inverseRanges;

    internal Id65AuthoringCompositeRollbackPlan(
        Id65CompiledAuthoringComposite compiled,
        IEnumerable<Id65AuthoringCompositeOwnedRange> inverseRanges)
    {
        ProfileId = compiled.ProfileId;
        ManifestSha256 = compiled.ManifestSha256;
        DeterministicPlanSha256 = compiled.DeterministicPlanSha256;
        ExpectedAfterimageSha256 = compiled.OutputRow80Sha256;
        LockedSourceSha256 = compiled.SourceRow80Sha256;
        _expectedAfterimage = compiled.CopyOutputRow80();
        _lockedSource = compiled.CopySourceRow80();
        _inverseRanges = Array.AsReadOnly(inverseRanges.Select(item => item.DeepCopy()).ToArray());
    }

    public string ProfileId { get; }
    public string ManifestSha256 { get; }
    public string DeterministicPlanSha256 { get; }
    public string ExpectedAfterimageSha256 { get; }
    public string LockedSourceSha256 { get; }
    public IReadOnlyList<Id65AuthoringCompositeOwnedRange> InverseRanges => _inverseRanges;
    public bool RollbackOnly => true;
    public bool AcceptedAsCompileInput => false;
    public bool WritesFileSystem => false;
    public bool WritesDiscImage => false;
    public bool WritesCue => false;
    public bool RetiredPublisherCalled => false;
    public bool AppIntegrated => false;
    public bool CreateBinEnabled => false;
    public bool NormalCreateBinEnabled => false;
    public bool RuntimeCandidateAuthorized => false;
    public bool ReleaseIntegrated => false;
    public bool PromotionAuthorized => false;
    public bool Publishable => false;

    internal byte[] CopyExpectedAfterimage() => _expectedAfterimage.ToArray();
    internal byte[] CopyLockedSource() => _lockedSource.ToArray();
}

internal sealed class Id65CompiledAuthoringComposite
{
    private readonly byte[] _sourceRow80;
    private readonly byte[] _outputRow80;
    private readonly byte[] _sourceModel;
    private readonly byte[] _outputModel;
    private readonly ReadOnlyCollection<Id65AuthoringCompositeOwnedRange> _ownedRanges;
    private readonly ReadOnlyCollection<Id65AuthoringCompositeDiffRange> _diffRanges;
    private readonly ReadOnlyCollection<Id65ModelComponentRelocation> _relocations;
    private readonly ReadOnlyCollection<Id65StableHandleRebase> _rebases;
    private readonly ReadOnlyCollection<Id65MobyAssetRelocation> _mobyAssetRelocations;
    private readonly ReadOnlyCollection<Id65MobyPlacementReadback> _mobyPlacements;

    internal Id65CompiledAuthoringComposite(
        Id65AuthoringCompositeManifest manifest,
        byte[] sourceRow80,
        byte[] outputRow80,
        byte[] sourceModel,
        byte[] outputModel,
        IEnumerable<Id65AuthoringCompositeOwnedRange> ownedRanges,
        IEnumerable<Id65AuthoringCompositeDiffRange> diffRanges,
        IEnumerable<Id65ModelComponentRelocation> relocations,
        IEnumerable<Id65StableHandleRebase> rebases,
        IEnumerable<Id65MobyAssetRelocation> mobyAssetRelocations,
        IEnumerable<Id65MobyPlacementReadback> mobyPlacements,
        Id65AuthoringCompositeTerrainReadback terrain,
        Id65AuthoringCompositeTextureReadback texture,
        Id65AuthoringCompositeCapacityReadback capacity,
        int changedByteCount,
        int diffRangeCount,
        string diffManifestSha256,
        string ownedRangeMapSha256,
        string relocationMapSha256,
        string rebaseMapSha256,
        string mobyAssetRelocationMapSha256,
        string transactionSha256,
        string deterministicPlanSha256,
        string targetOverlaySha256,
        string globalExecutableSha256)
    {
        Manifest = manifest;
        ManifestSha256 = manifest.CanonicalSha256;
        _sourceRow80 = sourceRow80.ToArray();
        _outputRow80 = outputRow80.ToArray();
        _sourceModel = sourceModel.ToArray();
        _outputModel = outputModel.ToArray();
        SourceRow80Sha256 = Id65AuthoringCompositeCompiler.Hash(_sourceRow80);
        OutputRow80Sha256 = Id65AuthoringCompositeCompiler.Hash(_outputRow80);
        SourceModelSha256 = Id65AuthoringCompositeCompiler.Hash(_sourceModel);
        OutputModelSha256 = Id65AuthoringCompositeCompiler.Hash(_outputModel);
        _ownedRanges = Array.AsReadOnly(ownedRanges.ToArray());
        _diffRanges = Array.AsReadOnly(diffRanges.ToArray());
        _relocations = Array.AsReadOnly(relocations.ToArray());
        _rebases = Array.AsReadOnly(rebases.ToArray());
        _mobyAssetRelocations = Array.AsReadOnly(mobyAssetRelocations.ToArray());
        _mobyPlacements = Array.AsReadOnly(mobyPlacements.ToArray());
        Terrain = terrain;
        Texture = texture;
        Capacity = capacity;
        ChangedByteCount = changedByteCount;
        DiffRangeCount = diffRangeCount;
        DiffManifestSha256 = diffManifestSha256;
        OwnedRangeMapSha256 = ownedRangeMapSha256;
        RelocationMapSha256 = relocationMapSha256;
        RebaseMapSha256 = rebaseMapSha256;
        MobyAssetRelocationMapSha256 = mobyAssetRelocationMapSha256;
        TransactionSha256 = transactionSha256;
        DeterministicPlanSha256 = deterministicPlanSha256;
        TargetOverlaySha256 = targetOverlaySha256;
        GlobalExecutableSha256 = globalExecutableSha256;
    }

    public string ProfileId => Id65AuthoringCompositeCompiler.ProfileId;
    public Id65AuthoringCompositeManifest Manifest { get; }
    public string ManifestSha256 { get; }
    public string SourceRow80Sha256 { get; }
    public string OutputRow80Sha256 { get; }
    public string SourceModelSha256 { get; }
    public string OutputModelSha256 { get; }
    public string TargetOverlaySha256 { get; }
    public string GlobalExecutableSha256 { get; }
    public IReadOnlyList<Id65AuthoringCompositeOwnedRange> OwnedRanges => _ownedRanges;
    public IReadOnlyList<Id65AuthoringCompositeDiffRange> DiffRanges => _diffRanges;
    public IReadOnlyList<Id65ModelComponentRelocation> Relocations => _relocations;
    public IReadOnlyList<Id65StableHandleRebase> HandleRebases => _rebases;
    public IReadOnlyList<Id65MobyAssetRelocation> MobyAssetRelocations => _mobyAssetRelocations;
    public IReadOnlyList<Id65MobyPlacementReadback> MobyPlacements => _mobyPlacements;
    public Id65AuthoringCompositeTerrainReadback Terrain { get; }
    public Id65AuthoringCompositeTextureReadback Texture { get; }
    public Id65AuthoringCompositeCapacityReadback Capacity { get; }
    public int ChangedByteCount { get; }
    public int DiffRangeCount { get; }
    public string DiffManifestSha256 { get; }
    public string OwnedRangeMapSha256 { get; }
    public string RelocationMapSha256 { get; }
    public string RebaseMapSha256 { get; }
    public string MobyAssetRelocationMapSha256 { get; }
    public string TransactionSha256 { get; }
    public string DeterministicPlanSha256 { get; }

    public bool BuildsDirectlyFromLockedSource => true;
    public bool SinglePassCompositeRebuildOwned => true;
    public bool AcceptsCompiledSliceAfterimages => false;
    public bool AcceptsSlicePlans => false;
    public bool CrossSliceCompositionSupported => false;
    public bool AppliesOneNonOverlappingLedger => true;
    public bool OwnsV2CollisionWinding => true;
    public bool OwnsOptionalTerrainTriangle => true;
    public bool OwnsPrivateT66Texture => true;
    public bool OwnsMobyDependencyBundles => true;
    public bool DirectRelocationsAndRebases => true;
    public bool ExactInverseVerified => true;
    public bool DeterministicReadbackRequired => true;
    public bool ProtectedRetailEntriesPreserved => true;
    public bool TargetOverlayPreservedExact => true;
    public bool GlobalExecutablePreservedExact => true;
    public bool ExecutableMutationExcluded => true;
    public bool InverseIsRollbackOnly => true;
    public bool InverseIsAuthoringInput => false;
    public bool WritesFileSystem => false;
    public bool WritesDiscImage => false;
    public bool WritesCue => false;
    public bool RetiredPublisherCalled => false;
    public bool AppIntegrated => false;
    public bool CreateBinEnabled => false;
    public bool NormalCreateBinEnabled => false;
    public bool RuntimeCandidateAuthorized => false;
    public bool ReleaseIntegrated => false;
    public bool PromotionAuthorized => false;
    public bool Publishable => false;

    internal byte[] CopySourceRow80() => _sourceRow80.ToArray();
    internal byte[] CopyOutputRow80() => _outputRow80.ToArray();
    internal byte[] CopySourceModel() => _sourceModel.ToArray();
    internal byte[] CopyOutputModel() => _outputModel.ToArray();
}

/// <summary>
/// First bounded single-pass ID65 authoring compiler. Every output byte is
/// rebuilt from the exact locked display-name row-80 source and declarative
/// witness/intent data. Slice plans and compiled slice afterimages are absent
/// from the API and are never called internally.
/// </summary>
internal static class Id65AuthoringCompositeCompiler
{
    public const string ProfileId = "id65-authoring-single-pass-composite-v1";
    public const string ExpectedLockedRow80Sha256 =
        "7ddbf6d9a6ee6c0f64c8564a89e374ca0ca234ef608ec812aae68e8176de1dc0";
    public const string ExpectedLockedModelSha256 =
        "1aa6950fe78e71ef4506d823fd33806c13a32838cdb00aadc4e35daffcf17f47";
    public const string ExpectedTargetOverlaySha256 =
        "9bc923cc8d27703537b81b01f51fde351e58aabee87d38a8afc630f1031878a5";
    public const string ExpectedGlobalExecutableSha256 =
        "fa5fc7981188b78fa7d7b78facca64c1f79dadb107515e9146ad178ade39d442";
    public const string ExpectedCombinedModelSha256 =
        "3973e0fcc9358050153c8dba2eed96b168dd959d182d42d9a73869384b0a5f75";

    // Frozen after the dedicated smoke independently derives the complete
    // locked-source -> terrain + T66 + T88 + T107 row-80 witness.
    public const string ExpectedCombinedManifestSha256 =
        "f3e08613e272df4a5bac8db4b459dcd5053b53cdfd5fd1689dfcc4a707e57c6c";
    public const string ExpectedCombinedOutputRow80Sha256 =
        "ae7128e958fd86736de3b4eb3100e0a59f20839fd77aacb8b266a93ef84a8fdd";
    public const int ExpectedCombinedChangedByteCount = 921_121;
    public const int ExpectedCombinedDiffRangeCount = 55_105;
    public const string ExpectedCombinedDiffManifestSha256 =
        "c32f0b9f030118316883cc5b9836d2e46dae2018dbd53bc0669f85510ad1163b";
    public const string ExpectedCombinedOwnedRangeMapSha256 =
        "12c7971011baa6e759e6fa86fb66fc7ca237accc9338c01e9e65c6735c71f3ea";
    public const string ExpectedCombinedRelocationMapSha256 =
        "395a8414babfce68497972f1200b5a15cfac0747aa0f6ee6738b4f8a25a7da21";
    public const string ExpectedCombinedRebaseMapSha256 =
        "c8bbf5da21b4687495b06fab7b8b9d2e0e86a13d0f4efcf11ac487058f5cdc6b";
    public const string ExpectedCombinedMobyAssetRelocationMapSha256 =
        "4f661cbfbfbbcb1bcbab9752dcd6bdfa1093aa1557915c47d3f0be913d75e076";
    public const string ExpectedCombinedTransactionSha256 =
        "74eca954c216436c7409292a9d4d4e0b796534bf5e47f567e0df8333f43a466a";
    public const string ExpectedCombinedDeterministicPlanSha256 =
        "3f7cc40e89d5786bf249386db9af4728f0e0efaaf5cbb39266ef3279d85a588e";

    private const int Row80ByteLength = 0x2E2000;
    private const int TexturePagesOffset = 0x800;
    private const int TexturePagesLength = 0xDE000;
    private const int ModelOffset = 0xDE800;
    private const int ModelLength = 0x94800;
    private const int LandingOffset = 0x1D0000;
    private const int T92XyOffset = 0x1D211C;
    private const int RecordLength = 0x58;
    private const int SceneOffset = 0x1D0000;
    private const int ObjectCountOffset = SceneOffset + 0x16C;
    private const int ObjectTableOffset = SceneOffset + 0x170;
    private const int T88Offset = ObjectTableOffset + (88 * RecordLength);
    private const int T107Offset = ObjectTableOffset + (107 * RecordLength);
    private const int FixupCountOffset = SceneOffset + 0x7F28;
    private const int FixupListOffset = FixupCountOffset + 4;
    private const int FixupAppendOffset = SceneOffset + 0x8130;
    private const int PropertiesOffset = SceneOffset + 0x8138;
    private const int PrivateThiefBlockOffset = SceneOffset + 0x5AFC;
    private const int PrivateThiefBlockLength = 0x134;
    private const int Root37Offset = 0xE4;
    private const int ActorId37Offset = 0x19A;
    private const int PackageOffset = 0x1CFA44;
    private const int PackageLength = 0x174;

    public static Id65AuthoringCompositeLockedSource CaptureLockedSource(
        ReadOnlySpan<byte> row80,
        ReadOnlySpan<byte> targetOverlay,
        ReadOnlySpan<byte> globalExecutable)
    {
        if (row80.Length != Row80ByteLength || targetOverlay.Length != 0xF800 ||
            globalExecutable.Length != 0x66000)
            throw new InvalidDataException("The locked composite source region lengths changed.");
        RequireHash(Hash(row80), ExpectedLockedRow80Sha256, "locked row-80 source");
        RequireHash(Hash(targetOverlay), ExpectedTargetOverlaySha256, "locked target overlay");
        RequireHash(Hash(globalExecutable), ExpectedGlobalExecutableSha256, "locked global executable");
        return new(row80.ToArray(), targetOverlay.ToArray(), globalExecutable.ToArray());
    }

    public static Id65AuthoringCompositeManifest CreateManifest(
        Id65TerrainTriangleIntent? optionalTerrainTriangle,
        Id65V2NativeTextureWitness textureWitness,
        Id65MobyDependencyBundleDescriptor mobyBundle,
        IEnumerable<Id65MobyPlacementIntent> placements)
    {
        ArgumentNullException.ThrowIfNull(textureWitness);
        ArgumentNullException.ThrowIfNull(mobyBundle);
        ArgumentNullException.ThrowIfNull(placements);
        return new(optionalTerrainTriangle, textureWitness.CanonicalSha256,
            mobyBundle.CanonicalSha256, placements);
    }

    public static Id65CompiledAuthoringComposite Compile(Id65AuthoringCompositeCompileRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Source);
        ArgumentNullException.ThrowIfNull(request.Manifest);
        ArgumentNullException.ThrowIfNull(request.TextureWitness);
        ArgumentNullException.ThrowIfNull(request.MobyBundles);
        ArgumentNullException.ThrowIfNull(request.MobyPlacements);
        Id65AuthoringCompositeCompilerLimits limits = request.Limits ?? new();

        byte[] source = ValidateAndCopySource(request.Source);
        Id65TerrainTriangleIntent? triangle = ValidateTerrain(request.Manifest.OptionalTerrainTriangle);
        IReadOnlyList<Id65V2NativeTexturePackedRow> textureRows = ValidateTextureWitness(request.TextureWitness);
        IReadOnlyList<Id65V2NativeTexturePagePatch> texturePatches = request.TextureWitness.CopyPagePatches();
        Id65MobyDependencyBundleDescriptor bundle = ValidateMobyBundle(request.MobyBundles, request.Source);
        Id65MobyPlacementIntent[] placements = ValidatePlacements(request.MobyPlacements, bundle);
        ValidateManifest(request.Manifest, triangle, request.TextureWitness, bundle, placements);
        ValidateLimits(limits, triangle is not null, placements);

        byte[] sourceModel = source.AsSpan(ModelOffset, ModelLength).ToArray();
        RequireHash(Hash(sourceModel), ExpectedLockedModelSha256, "locked source model");
        byte[] finalModel = BuildFinalModelFromLockedSource(
            sourceModel, textureRows, triangle is not null);
        string expectedModel = triangle is null
            ? Id65V2NativeTextureCompositionCompiler.ExpectedOutputModelSha256
            : ExpectedCombinedModelSha256;
        RequireHash(Hash(finalModel), expectedModel, "single-pass final model");

        List<Id65AuthoringCompositeOwnedRange> ledger = BuildLedger(
            source, finalModel, texturePatches, bundle, placements);
        ValidateLedger(source, ledger);
        byte[] output = ApplyLedger(source, ledger, reverse: false);
        byte[] inverse = ApplyLedger(output, ledger, reverse: true);
        if (!inverse.SequenceEqual(source))
            throw new InvalidDataException("The composite ledger is not exactly invertible to locked source.");

        VerifyFinalReadback(source, output, finalModel, triangle is not null, placements);
        DiffProof diff = BuildDiffProof(source, output, ledger);
        Id65ModelComponentRelocation[] relocations = BuildRelocations(sourceModel, finalModel, triangle is not null);
        ValidateRelocations(relocations, sourceModel, finalModel, triangle is not null);
        Id65StableHandleRebase[] rebases = BuildRebases(triangle is not null, placements);
        ValidateRebases(rebases, triangle is not null, placements);
        Id65MobyAssetRelocation[] mobyAssetRelocations =
            BuildMobyAssetRelocations(bundle, placements);
        ValidateMobyAssetRelocations(mobyAssetRelocations, bundle, placements);
        Id65MobyPlacementReadback[] mobyPlacementReadback =
            BuildMobyPlacementReadback(output, placements);
        Id65AuthoringCompositeTerrainReadback terrainReadback =
            BuildTerrainReadback(finalModel, triangle is not null);
        Id65AuthoringCompositeTextureReadback textureReadback =
            BuildTextureReadback(request.TextureWitness, finalModel, triangle is not null);
        string ownedHash = HashOwnedRanges(ledger);
        string relocationHash = HashRelocations(relocations);
        string rebaseHash = HashRebases(rebases);
        string mobyAssetRelocationHash = HashMobyAssetRelocations(mobyAssetRelocations);
        string transactionHash = HashTransaction(ledger);
        Id65AuthoringCompositeCapacityReadback capacity = BuildCapacity(triangle is not null, placements);
        string deterministicHash = Hash(Encoding.UTF8.GetBytes(string.Join('\n',
        [
            ProfileId,
            request.Manifest.CanonicalSha256,
            request.TextureWitness.CanonicalSha256,
            bundle.CanonicalSha256,
            Hash(source),
            Hash(output),
            Hash(sourceModel),
            Hash(finalModel),
            diff.ManifestSha256,
            ownedHash,
            relocationHash,
            rebaseHash,
            mobyAssetRelocationHash,
            transactionHash
        ])));

        Id65CompiledAuthoringComposite compiled = new(
            request.Manifest,
            source,
            output,
            sourceModel,
            finalModel,
            ledger,
            diff.Ranges,
            relocations,
            rebases,
            mobyAssetRelocations,
            mobyPlacementReadback,
            terrainReadback,
            textureReadback,
            capacity,
            diff.ChangedByteCount,
            diff.RangeCount,
            diff.ManifestSha256,
            ownedHash,
            relocationHash,
            rebaseHash,
            mobyAssetRelocationHash,
            transactionHash,
            deterministicHash,
            request.Source.TargetOverlaySha256,
            request.Source.GlobalExecutableSha256);

        byte[] applied = ApplyTransactional(compiled, compiled.CopySourceRow80(), reverse: false);
        byte[] rolledBack = ApplyTransactional(compiled, applied, reverse: true);
        Id65AuthoringCompositeRollbackPlan rollbackPlan = CreateRollbackPlan(compiled);
        byte[] plannedRollback = ApplyRollback(rollbackPlan, applied);
        if (!applied.SequenceEqual(output) || !rolledBack.SequenceEqual(source) ||
            !plannedRollback.SequenceEqual(source) || !rollbackPlan.RollbackOnly ||
            rollbackPlan.AcceptedAsCompileInput ||
            !InvertRelocations(InvertRelocations(relocations)).SequenceEqual(relocations) ||
            !InvertHandleRebases(InvertHandleRebases(rebases)).SequenceEqual(rebases))
            throw new InvalidDataException("Composite transaction, relocation, or rebase inverse failed.");

        if (triangle is not null && placements.Length == 2)
            VerifyFrozenCombinedPins(compiled);
        return compiled;
    }

    public static byte[] ApplyTransactional(
        Id65CompiledAuthoringComposite compiled,
        byte[] input,
        bool reverse)
    {
        ArgumentNullException.ThrowIfNull(compiled);
        ArgumentNullException.ThrowIfNull(input);
        byte[] expected = reverse ? compiled.CopyOutputRow80() : compiled.CopySourceRow80();
        if (!input.SequenceEqual(expected))
            throw new InvalidDataException(
                $"The composite {(reverse ? "rollback afterimage" : "locked-source preimage")} changed.");
        return reverse ? compiled.CopySourceRow80() : compiled.CopyOutputRow80();
    }

    public static Id65AuthoringCompositeRollbackPlan CreateRollbackPlan(
        Id65CompiledAuthoringComposite compiled)
    {
        ArgumentNullException.ThrowIfNull(compiled);
        Id65AuthoringCompositeOwnedRange[] inverse = compiled.OwnedRanges
            .Reverse()
            .Select(item => new Id65AuthoringCompositeOwnedRange(
                item.StableId,
                item.OwnerId,
                item.DataRelativeOffset,
                item.CopyAfter(),
                item.CopyBefore()))
            .ToArray();
        Id65AuthoringCompositeRollbackPlan plan = new(compiled, inverse);
        byte[] rolledBack = ApplyRollback(plan, plan.CopyExpectedAfterimage());
        if (!rolledBack.SequenceEqual(compiled.CopySourceRow80()))
            throw new InvalidDataException("The rollback-only plan did not restore locked source exactly.");
        return plan;
    }

    public static byte[] ApplyRollback(
        Id65AuthoringCompositeRollbackPlan plan,
        byte[] exactAfterimage)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(exactAfterimage);
        byte[] expected = plan.CopyExpectedAfterimage();
        if (!exactAfterimage.SequenceEqual(expected) || Hash(exactAfterimage) != plan.ExpectedAfterimageSha256)
            throw new InvalidDataException("The rollback plan exact composite afterimage changed.");
        byte[] output = ApplyLedger(exactAfterimage, plan.InverseRanges, reverse: false);
        if (!output.SequenceEqual(plan.CopyLockedSource()) || Hash(output) != plan.LockedSourceSha256)
            throw new InvalidDataException("The rollback plan did not produce the exact locked source.");
        return output;
    }

    internal static IReadOnlyList<Id65ModelComponentRelocation> InvertRelocations(
        IEnumerable<Id65ModelComponentRelocation> relocations) =>
        relocations.Select(item => new Id65ModelComponentRelocation(
            item.StableId,
            item.OutputRelativeOffset,
            item.OutputByteLength,
            item.SourceRelativeOffset,
            item.SourceByteLength,
            item.OutputSha256,
            item.SourceSha256,
            item.ContentsPreserved)).ToArray();

    internal static IReadOnlyList<Id65StableHandleRebase> InvertHandleRebases(
        IEnumerable<Id65StableHandleRebase> rebases) =>
        rebases.Select(item => item with { Source = item.Output, Output = item.Source }).ToArray();

    private static byte[] ValidateAndCopySource(Id65AuthoringCompositeLockedSource source)
    {
        if (source.Row80ByteLength != Row80ByteLength || source.TargetOverlayByteLength != 0xF800 ||
            source.GlobalExecutableByteLength != 0x66000 ||
            source.Row80Sha256 != ExpectedLockedRow80Sha256 ||
            source.TargetOverlaySha256 != ExpectedTargetOverlaySha256 ||
            source.GlobalExecutableSha256 != ExpectedGlobalExecutableSha256)
            throw new InvalidDataException("The immutable locked composite source identity changed.");
        byte[] row80 = source.CopyRow80();
        RequireHash(Hash(row80), ExpectedLockedRow80Sha256, "copied locked row-80 source");
        RequireHash(Hash(source.CopyTargetOverlay()), ExpectedTargetOverlaySha256, "copied target overlay");
        RequireHash(Hash(source.CopyGlobalExecutable()), ExpectedGlobalExecutableSha256, "copied global executable");
        return row80;
    }

    private static Id65TerrainTriangleIntent? ValidateTerrain(Id65TerrainTriangleIntent? triangle)
    {
        if (triangle is null)
            return null;
        Id65TerrainTriangleIntent expected =
            Id65AuthoringTerrainMutationCompiler.CreateConcreteTwoTileWitness();
        if (triangle != expected)
            throw new InvalidDataException("Only the exact optional adjacent terrain triangle is supported.");
        return triangle;
    }

    private static IReadOnlyList<Id65V2NativeTexturePackedRow> ValidateTextureWitness(
        Id65V2NativeTextureWitness witness)
    {
        if (witness.ProfileId != Id65V2NativeTextureCompositionCompiler.WitnessProfileId ||
            !HashEquals(witness.CanonicalSha256, Id65V2NativeTextureCompositionCompiler.ExpectedWitnessSha256) ||
            witness.DonorManifestId != UnusedLevel65NativeTextureAuthoringContract.DonorManifestId ||
            !HashEquals(witness.DonorCompleteRecordSha256,
                UnusedLevel65NativeTextureAuthoringContract.ExpectedDonorCompleteRecordSha256) ||
            !HashEquals(witness.SourceTexturePagesSha256,
                Id65V2NativeTextureCompositionCompiler.ExpectedSourceTexturePagesSha256) ||
            !HashEquals(witness.OutputTexturePagesSha256,
                Id65V2NativeTextureCompositionCompiler.ExpectedOutputTexturePagesSha256) ||
            !HashEquals(witness.SourceTextureComponentSha256,
                Id65V2NativeTextureCompositionCompiler.ExpectedSourceTextureComponentSha256) ||
            !HashEquals(witness.OutputTextureComponentSha256,
                Id65V2NativeTextureCompositionCompiler.ExpectedOutputTextureComponentSha256) ||
            witness.SourceTextureCount != 66 || witness.OutputTextureCount != 67 ||
            witness.PrivateTextureId != 66 || witness.DonorWadEntry != 70 ||
            witness.DonorTextureId != 12 || witness.MaterialTemplateTextureId != 25 ||
            witness.ContainsFoundationModelBytes || witness.ContainsFoundationFaceBinding ||
            witness.ContainsAppendAfterimage || witness.ContainsPublisherState ||
            !witness.LaterSinglePassCompositeInputAvailable || witness.CrossSliceCompositionAuthorized ||
            witness.AfterimageStackingAuthorized)
            throw new InvalidDataException("The immutable T66 witness identity or safety boundary changed.");

        IReadOnlyList<Id65V2NativeTexturePackedRow> rows = witness.CopyPackedRows();
        IReadOnlyList<Id65V2NativeTexturePagePatch> patches = witness.CopyPagePatches();
        if (rows.Select(item => item.TextureId).Distinct().Count() != rows.Count ||
            rows.Count(item => item.Synthetic) != 1 ||
            rows.Any(item => item.TextureId is < 0 or > 66 || item.CopyLow().Length != 16 ||
                item.CopyHigh().Length != 168 || !HashEquals(Hash(item.CopyLow()), item.LowSha256) ||
                !HashEquals(Hash(item.CopyHigh()), item.HighSha256)) ||
            rows.Where(item => !item.Synthetic).Any(item => item.DonorWadEntry != 80 ||
                item.DonorTextureId != item.TextureId))
            throw new InvalidDataException("A T66 packed row lost exact byte/provenance identity.");
        Id65V2NativeTexturePackedRow synthetic = rows.Single(item => item.Synthetic);
        if (synthetic.TextureId != 66 || synthetic.DonorWadEntry != 70 || synthetic.DonorTextureId != 12 ||
            !HashEquals(synthetic.LowSha256,
                Id65V2NativeTextureCompositionCompiler.ExpectedPrivateLowRowSha256) ||
            !HashEquals(synthetic.HighSha256,
                Id65V2NativeTextureCompositionCompiler.ExpectedPrivateHighRowSha256))
            throw new InvalidDataException("The private T66 synthetic row changed.");
        if (patches.Count != UnusedLevel65NativeTextureAuthoringContract.ExpectedTexturePagePatchCount)
            throw new InvalidDataException("The T66 page-patch count changed.");
        for (int index = 0; index < patches.Count; index++)
        {
            Id65V2NativeTexturePagePatch patch = patches[index];
            if (patch.ByteLength <= 0 || patch.RelativeOffset < 0 ||
                patch.RelativeOffset + (long)patch.ByteLength > TexturePagesLength ||
                patch.Owner != "terrain-texture-global-repack-data" ||
                !HashEquals(Hash(patch.CopyBefore()), patch.BeforeSha256) ||
                !HashEquals(Hash(patch.CopyAfter()), patch.AfterSha256) ||
                index > 0 && patches[index - 1].RelativeOffset + patches[index - 1].ByteLength >
                    patch.RelativeOffset)
                throw new InvalidDataException("A T66 page patch lost exact nonoverlapping ownership.");
        }
        string canonical = ComputeTextureWitnessCanonical(witness, rows, patches);
        if (!HashEquals(canonical, witness.CanonicalSha256) ||
            !HashEquals(canonical, Id65V2NativeTextureCompositionCompiler.ExpectedWitnessSha256))
            throw new InvalidDataException("The T66 witness canonical identity does not match its contents.");
        return rows;
    }

    private static string ComputeTextureWitnessCanonical(
        Id65V2NativeTextureWitness witness,
        IReadOnlyList<Id65V2NativeTexturePackedRow> rows,
        IReadOnlyList<Id65V2NativeTexturePagePatch> patches)
    {
        StringBuilder text = new();
        text.AppendLine(witness.ProfileId).AppendLine(witness.DonorManifestId)
            .AppendLine(witness.DonorCompleteRecordSha256)
            .AppendLine(witness.SourceTexturePagesSha256).AppendLine(witness.OutputTexturePagesSha256)
            .AppendLine(witness.SourceTextureComponentSha256).AppendLine(witness.OutputTextureComponentSha256);
        foreach (Id65V2NativeTexturePackedRow row in rows.OrderBy(item => item.TextureId))
            text.Append(row.TextureId).Append('|').Append(row.DonorWadEntry).Append('|')
                .Append(row.DonorTextureId).Append('|').Append(row.Synthetic ? '1' : '0').Append('|')
                .Append(row.LowSha256).Append('|').Append(row.HighSha256).Append('\n');
        foreach (Id65V2NativeTexturePagePatch patch in patches)
            text.Append(patch.RelativeOffset.ToString("X8", CultureInfo.InvariantCulture)).Append('|')
                .Append(patch.ByteLength).Append('|').Append(patch.BeforeSha256).Append('|')
                .Append(patch.AfterSha256).Append('|').Append(patch.Owner).Append('\n');
        return Hash(Encoding.UTF8.GetBytes(text.ToString()));
    }

    private static Id65MobyDependencyBundleDescriptor ValidateMobyBundle(
        IReadOnlyList<Id65MobyDependencyBundleDescriptor> bundles,
        Id65AuthoringCompositeLockedSource source)
    {
        foreach (Id65MobyDependencyBundleDescriptor candidate in bundles)
        {
            Id65MobyExternalDependencyIntent? future = candidate.ExternalDependencies.FirstOrDefault(item =>
                item.Policy == Id65MobyExternalDependencyPolicy.RequiresFutureCompositeCompiler);
            if (future is not null)
            {
                string owner = future.Kind == Id65MobyExternalDependencyKind.GlobalExecutable
                    ? "SCUS/global executable"
                    : future.Kind.ToString();
                throw new InvalidDataException(
                    $"Bundle `{candidate.Id}` requires unsupported {owner} mutation in this first composite compiler.");
            }
        }
        if (bundles.Count != 1)
            throw new InvalidDataException("The first composite accepts one exact deduplicated Grass bundle.");
        Id65MobyDependencyBundleDescriptor bundle = bundles[0];
        string canonicalJson = BuildMobyCanonicalJson(bundle);
        string canonicalSha256 = Hash(Encoding.UTF8.GetBytes(canonicalJson));
        if (bundle.Id != Id65AuthoringMobyDependencyBundleCompiler.ArtisansGrassBundleId ||
            bundle.SchemaVersion != 1 ||
            bundle.FoundationContractSha256 != UnusedLevel65MobyDependencyBundleFoundation.ExpectedContractSha256 ||
            bundle.CanonicalJson != canonicalJson || bundle.CanonicalSha256 != canonicalSha256 ||
            canonicalSha256 != Id65AuthoringMobyDependencyBundleCompiler.ExpectedArtisansGrassBundleSha256)
            throw new InvalidDataException("The full-field frozen Moby bundle canonical identity changed.");

        Id65MobyExternalDependencyIntent[] expected =
        [
            new(
                "target-overlay.default-no-update-dispatch",
                Id65MobyExternalDependencyKind.TargetOverlay,
                new Id65LogicalAddress("wad-entry", 79, 0),
                0xF800,
                ExpectedTargetOverlaySha256,
                Id65MobyExternalDependencyPolicy.PreserveExact),
            new(
                "global-executable.no-01f5-handler",
                Id65MobyExternalDependencyKind.GlobalExecutable,
                new Id65LogicalAddress("disc-lba", 55_382, 0),
                0x66000,
                ExpectedGlobalExecutableSha256,
                Id65MobyExternalDependencyPolicy.PreserveExact)
        ];
        if (!bundle.ExternalDependencies.SequenceEqual(expected) ||
            bundle.ExternalDependencies.Any(item => item.Policy != Id65MobyExternalDependencyPolicy.PreserveExact) ||
            source.TargetOverlaySha256 != expected[0].PreimageSha256 ||
            source.GlobalExecutableSha256 != expected[1].PreimageSha256 ||
            bundle.Rows.Count != 1 || bundle.Properties.Count != 1 || bundle.ActorPackages.Count != 1 ||
            bundle.ActorRoots.Count != 1 || bundle.Fixups.Count != 1)
            throw new InvalidDataException("The exact PreserveExact overlay/SCUS or Moby asset envelope changed.");
        return bundle;
    }

    private static string BuildMobyCanonicalJson(Id65MobyDependencyBundleDescriptor bundle) =>
        JsonSerializer.Serialize(
            new
            {
                id = bundle.Id,
                schemaVersion = bundle.SchemaVersion,
                foundationContractSha256 = bundle.FoundationContractSha256,
                rows = bundle.Rows,
                properties = bundle.Properties,
                actorPackages = bundle.ActorPackages,
                actorRoots = bundle.ActorRoots,
                fixups = bundle.Fixups,
                externalDependencies = bundle.ExternalDependencies,
                behaviorClosure = bundle.BehaviorClosure,
                runtimeDependencies = bundle.RuntimeDependencies
            },
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            });

    private static Id65MobyPlacementIntent[] ValidatePlacements(
        IReadOnlyList<Id65MobyPlacementIntent> supplied,
        Id65MobyDependencyBundleDescriptor bundle)
    {
        if (supplied.Count is < 1 or > 2)
            throw new InvalidDataException("The first composite accepts exact T88, T107, or both.");
        Id65MobyPlacementIntent[] placements = supplied.OrderBy(item => item.TargetTrueIndex).ToArray();
        if (placements.Select(item => item.TargetTrueIndex).Distinct().Count() != placements.Length ||
            placements.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != placements.Length ||
            placements.Any(item => item.BundleId != bundle.Id ||
                item.RowTemplateId != Id65AuthoringMobyDependencyBundleCompiler.ArtisansGrassRowTemplateId ||
                item.AtomicGroupId != Id65AuthoringMobyDependencyBundleCompiler.AtomicPlacementGroupId))
            throw new InvalidDataException("Composite Moby placement identities or shared atomic group changed.");
        foreach (Id65MobyPlacementIntent placement in placements)
        {
            Id65MobyPlacementIntent expected = placement.TargetTrueIndex switch
            {
                88 => Id65AuthoringMobyDependencyBundleCompiler.CreateZeroEggT88Placement(),
                107 => Id65AuthoringMobyDependencyBundleCompiler.CreateT107Placement(),
                _ => throw new InvalidDataException("Only exact T88/T107 placement targets are supported.")
            };
            if (placement != expected)
                throw new InvalidDataException($"Composite placement T{placement.TargetTrueIndex} changed.");
        }
        return placements;
    }

    private static void ValidateManifest(
        Id65AuthoringCompositeManifest manifest,
        Id65TerrainTriangleIntent? triangle,
        Id65V2NativeTextureWitness texture,
        Id65MobyDependencyBundleDescriptor bundle,
        Id65MobyPlacementIntent[] placements)
    {
        Id65AuthoringCompositeManifest expected = new(
            triangle,
            texture.CanonicalSha256,
            bundle.CanonicalSha256,
            placements);
        if (manifest.ProfileId != ProfileId || !manifest.MandatoryNegativeWindingV2 ||
            manifest.CanonicalJson != expected.CanonicalJson ||
            manifest.CanonicalSha256 != expected.CanonicalSha256 ||
            manifest.TextureWitnessSha256 != texture.CanonicalSha256 ||
            manifest.MobyBundleSha256 != bundle.CanonicalSha256 ||
            !manifest.Placements.SequenceEqual(placements))
            throw new InvalidDataException("The composite manifest does not match its exact declarative inputs.");
    }

    private static void ValidateLimits(
        Id65AuthoringCompositeCompilerLimits limits,
        bool terrain,
        Id65MobyPlacementIntent[] placements)
    {
        int usedModel = terrain ? 0x94664 : 0x94634;
        int objects = placements.Any(item => item.TargetTrueIndex == 107) ? 108 : 107;
        if (limits.ModelByteCapacity < ModelLength || limits.MaximumOutputUsedModelBytes < usedModel)
            throw new InvalidDataException("Composite model/tail capacity is insufficient.");
        if (limits.MaximumNativeTextureId < 66 || limits.MaximumNativeTextureId > 127)
            throw new InvalidDataException("Composite seven-bit texture capacity is insufficient or invalid.");
        if (limits.MaximumObjectCount < objects)
            throw new InvalidDataException("Composite object-row capacity is insufficient.");
        if (limits.MaximumActorRootCount < 38)
            throw new InvalidDataException("Composite actor-root capacity is insufficient.");
        if (limits.ActorSubfileEnd < PackageOffset + PackageLength)
            throw new InvalidDataException("Composite actor-tail capacity is insufficient.");
        if (limits.SceneSubfileEnd < 0x8140)
            throw new InvalidDataException("Composite scene-tail capacity is insufficient.");
        if (limits.CollisionTreeCapacityBytes < 0x6A60 || limits.CollisionBlocksCapacityBytes < 0x17984)
            throw new InvalidDataException("Composite collision tree/block capacity is insufficient.");
    }

    private static byte[] BuildRemoteEnvironment(byte[] source)
    {
        if (source.Length != 0x284A4 || ReadInt32(source, 0) != source.Length ||
            ReadInt32(source, 4) != 216)
            throw new InvalidDataException("The locked 216-sector environment changed.");
        int sourceTableEnd = 8 + (216 * 4);
        int outputTableEnd = 8 + (217 * 4);
        int payloadLength = source.Length - sourceTableEnd;
        byte[] sector = BuildRemoteSector();
        byte[] output = new byte[0x28518];
        WriteInt32(output, 0, output.Length);
        WriteInt32(output, 4, 217);
        for (int index = 0; index < 216; index++)
            WriteInt32(output, 8 + (index * 4), ReadInt32(source, 8 + (index * 4)) + 4);
        WriteInt32(output, 8 + (216 * 4), source.Length);
        source.AsSpan(sourceTableEnd, payloadLength).CopyTo(output.AsSpan(outputTableEnd));
        int sectorOffset = outputTableEnd + payloadLength;
        sector.CopyTo(output, sectorOffset);
        if (sectorOffset != source.Length + 4 || sectorOffset + sector.Length != output.Length)
            throw new InvalidDataException("The remote sector append boundary changed.");
        return output;
    }

    private static byte[] BuildRemoteSector()
    {
        byte[][] parts =
        [
            Convert.FromHexString("80018001C000000200010001000000020303010003030109FFFFFFFF"),
            Convert.FromHexString("004000020040001E00C00310"),
            Convert.FromHexString("2D39300030552C0037622C00"),
            Convert.FromHexString("0082100000821000"),
            Convert.FromHexString("004000020040001E00C00310"),
            Convert.FromHexString("99756B00775D6600896A6A00775D6600806268007A5F6800"),
            Convert.FromHexString("000001020101000219024001080A1000")
        ];
        byte[] sector = new byte[0x70];
        int cursor = 0;
        foreach (byte[] part in parts)
        {
            part.CopyTo(sector, cursor);
            cursor += part.Length;
        }
        if (cursor != sector.Length)
            throw new InvalidDataException("The exact remote HP/LP sector length changed.");
        RequireHash(Hash(sector),
            UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.ExpectedNewSectorSha256,
            "remote sector 216");
        return sector;
    }

    private static byte[] BuildRemoteOcclusion(byte[] source)
    {
        if (source.Length != 0x974 || ReadInt32(source, 0) != source.Length ||
            ReadInt32(source, 4) != 0x71C || ReadInt32(source, 8) != 16)
            throw new InvalidDataException("The locked occlusion directory changed.");
        IReadOnlyList<int>[] sourceGroups = ParseOcclusionGroups(source, 216, out int[] pointers);
        if (pointers[0] != 0x48 || sourceGroups[0].Count != 124 || sourceGroups[0][8] != 213 ||
            source[0xC8] != 0xFF || source[0x71E] != 0xFF || source[0x71F] != 0xFF)
            throw new InvalidDataException("The locked occlusion insertion substrate changed.");
        byte[] output = source.ToArray();
        int environmentEnd = 4 + 0x71C;
        Array.Copy(source, 0xC8, output, 0xC9, environmentEnd - 2 - 0xC8);
        output[0xC8] = 216;
        for (int group = 1; group < pointers.Length; group++)
            WriteInt32(output, 12 + (group * 4), pointers[group] + 1);
        output[environmentEnd - 1] = source[environmentEnd - 1];
        IReadOnlyList<int>[] readback = ParseOcclusionGroups(output, 217, out int[] outputPointers);
        if (!readback[0].SequenceEqual(sourceGroups[0].Concat([216])) ||
            !sourceGroups.Skip(1).Zip(readback.Skip(1)).All(pair => pair.First.SequenceEqual(pair.Second)) ||
            outputPointers[0] != pointers[0] ||
            !outputPointers.Skip(1).SequenceEqual(pointers.Skip(1).Select(value => value + 1)))
            throw new InvalidDataException("The direct occlusion insertion failed readback.");
        RequireHash(Hash(output), "34c97013761d4ec0833745c3be420a0c416b1a7d6912070af5ce76ce00135ccd",
            "remote occlusion");
        return output;
    }

    private static IReadOnlyList<int>[] ParseOcclusionGroups(
        byte[] component,
        int sectorCount,
        out int[] pointers)
    {
        int environmentLength = ReadInt32(component, 4);
        int environmentEnd = 4 + environmentLength;
        int groupCount = ReadInt32(component, 8);
        if (environmentLength < 8 || environmentEnd > component.Length || groupCount != 16 ||
            12 + (groupCount * 4) > environmentEnd)
            throw new InvalidDataException("The occlusion group directory is invalid.");
        pointers = new int[groupCount];
        IReadOnlyList<int>[] groups = new IReadOnlyList<int>[groupCount];
        for (int group = 0; group < groupCount; group++)
        {
            int relative = ReadInt32(component, 12 + (group * 4));
            pointers[group] = relative;
            int offset = 4 + relative;
            if (offset < 12 + (groupCount * 4) || offset >= environmentEnd)
                throw new InvalidDataException("An occlusion group points outside its directory.");
            List<int> sectors = [];
            while (offset < environmentEnd && component[offset] != 0xFF)
            {
                if (component[offset] >= sectorCount)
                    throw new InvalidDataException("An occlusion group references an invalid sector.");
                sectors.Add(component[offset++]);
            }
            if (offset >= environmentEnd)
                throw new InvalidDataException("An occlusion group lacks a terminator.");
            groups[group] = sectors;
        }
        return groups;
    }

    private static byte[] BuildRemoteCollision(byte[] source)
    {
        CompositeCollisionLayout layout = ParseCollisionLayout(
            source, 19_808, 0x58480, 0x5D1E0, expectedLength: 0x5FAE8);
        byte[] sourceTriangle = source.AsSpan(layout.TriangleOffset + (13_995 * 12), 12).ToArray();
        if (Convert.ToHexString(sourceTriangle) != "A7A2140044631900E0010000" ||
            source[layout.AssignmentsOffset + 13_995] != 0xFF)
            throw new InvalidDataException("The exact locked T13995 collision substrate changed.");
        byte[] tree = Slice(source, layout.TreeOffset, layout.TreeCapacityBytes);
        byte[] blocks = Slice(source, layout.BlocksOffset, layout.BlocksCapacityBytes);
        RequireHash(Hash(tree), UnusedLevel65RemoteBlankIsolationConstruction.ExpectedSourceTreeSha256,
            "locked collision tree");
        RequireHash(Hash(blocks), UnusedLevel65RemoteBlankIsolationConstruction.ExpectedSourceBlocksSha256,
            "locked collision blocks");
        CompositeCollisionIndex native = DecodeCollisionIndex(
            tree, blocks, blocks.Length, 19_808, requireZeroTail: false);
        Id65AuthoringCollisionCell sourceCellKey = new(34, 35, 1);
        Id65AuthoringCollisionCell targetCellKey = new(1, 1, 2);
        int[] expectedSourceSequence =
        [
            14_001, 14_000, 13_999, 13_998, 13_995, 11_276, 11_234, 11_197,
            11_196, 11_191, 11_190, 11_184, 3_130, 3_129, 3_128, 3_066,
            3_065, 3_064, 3_060, 3_058, 3_057, 3_056, 3_055
        ];
        if (native.Cells.Count != 4_252 || native.GroupStartCount != 4_253 ||
            !native.Cells.TryGetValue(sourceCellKey, out CompositeCollisionBinding? sourceCell) ||
            !sourceCell.OrderedTriangleIndexes.SequenceEqual(expectedSourceSequence))
            throw new InvalidDataException("The native collision-cell substrate changed.");
        int targetPointer = FindTreeLeafPointer(tree, targetCellKey);
        if (targetPointer != 0x1254 || ReadUInt16(tree, targetPointer) != 0xFFFF ||
            native.Cells.ContainsKey(targetCellKey))
            throw new InvalidDataException("The remote collision target cell is no longer vacant.");

        Dictionary<Id65AuthoringCollisionCell, int[]> desired = native.Cells
            .ToDictionary(pair => pair.Key, pair => pair.Value.OrderedTriangleIndexes.ToArray());
        desired[sourceCellKey] = desired[sourceCellKey].Where(index => index != 13_995).ToArray();
        desired.Add(targetCellKey, [13_995]);
        (byte[] outputTree, byte[] outputBlocks, int usedBytes) =
            RepackCollision(native, desired, 19_808, targetCellKey, targetPointer);
        if (usedBytes != 0x17960)
            throw new InvalidDataException("The direct v2 collision-block used length changed.");
        RequireHash(Hash(outputTree),
            UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.ExpectedOutputTreeSha256,
            "remote collision tree");
        RequireHash(Hash(outputBlocks),
            UnusedLevel65RemoteBlankIsolationConstruction.ExpectedOutputBlocksSha256,
            "remote collision blocks");

        byte[] output = source.ToArray();
        outputTree.CopyTo(output, layout.TreeOffset);
        outputBlocks.CopyTo(output, layout.BlocksOffset);
        Convert.FromHexString("100138381001007000020000")
            .CopyTo(output, layout.TriangleOffset + (13_995 * 12));
        output[layout.AssignmentsOffset + 13_995] = 0;
        return output;
    }

    private static CompositeCollisionLayout ParseCollisionLayout(
        byte[] collision,
        int triangleCount,
        int assignmentsRelative,
        int flagsRelative,
        int expectedLength)
    {
        if (collision.Length != expectedLength || ReadInt32(collision, 0) != collision.Length ||
            ReadInt32(collision, 4) != triangleCount || ReadInt32(collision, 8) != 0x2904 ||
            ReadInt32(collision, 12) != 0x1C || ReadInt32(collision, 16) != 0x6A7C ||
            ReadInt32(collision, 20) != 0x1E400 ||
            ReadInt32(collision, 24) != assignmentsRelative ||
            ReadInt32(collision, 28) != flagsRelative)
            throw new InvalidDataException("The collision component header changed.");
        int tree = 4 + 0x1C;
        int blocks = 4 + 0x6A7C;
        int triangles = 4 + 0x1E400;
        int assignments = 4 + assignmentsRelative;
        int flags = 4 + flagsRelative;
        if (blocks - tree != 0x6A60 || triangles - blocks != 0x17984 ||
            triangles + (triangleCount * 12) != assignments || assignments + triangleCount > flags ||
            flags + 0x2904 != collision.Length)
            throw new InvalidDataException("The collision component partitions changed.");
        return new(tree, blocks, triangles, assignments, flags, 0x6A60, 0x17984);
    }

    private static CompositeCollisionIndex DecodeCollisionIndex(
        byte[] tree,
        byte[] blocks,
        int usedBlockBytes,
        int triangleCount,
        bool requireZeroTail)
    {
        if (usedBlockBytes <= 0 || (usedBlockBytes & 1) != 0 || usedBlockBytes > blocks.Length ||
            requireZeroTail && blocks.AsSpan(usedBlockBytes).IndexOfAnyExcept((byte)0) >= 0)
            throw new InvalidDataException("The collision block used length or zero tail is invalid.");
        int usedWords = usedBlockBytes / 2;
        List<int> starts = [];
        for (int word = 0; word < usedWords; word++)
            if ((ReadUInt16(blocks, word * 2) & 0x8000) != 0)
                starts.Add(word);
        if (starts.Count < 2 || starts[^1] != usedWords - 1 ||
            ReadUInt16(blocks, starts[^1] * 2) != 0x8000)
            throw new InvalidDataException("The collision blocks lack their terminal sentinel.");
        Dictionary<int, int[]> groups = [];
        for (int group = 0; group + 1 < starts.Count; group++)
        {
            int start = starts[group];
            int end = starts[group + 1];
            int[] sequence = new int[end - start];
            for (int index = 0; index < sequence.Length; index++)
            {
                ushort word = ReadUInt16(blocks, (start + index) * 2);
                if ((index == 0) != ((word & 0x8000) != 0))
                    throw new InvalidDataException("A collision group marker is invalid.");
                int triangle = word & 0x7FFF;
                if (triangle >= triangleCount)
                    throw new InvalidDataException("A collision group references an invalid triangle.");
                sequence[index] = triangle;
            }
            if (!IsStrictlyDescending(sequence))
                throw new InvalidDataException("A collision cell sequence is not strictly descending.");
            groups.Add(start, sequence);
        }

        int TreeWord(int offset)
        {
            if (offset < 0 || (offset & 1) != 0 || offset + 2 > tree.Length)
                throw new InvalidDataException("A collision tree pointer is outside capacity.");
            return ReadUInt16(tree, offset);
        }
        int SegmentLength(int offset)
        {
            int length = TreeWord(offset);
            if (length > 256 || offset + ((length + 1L) * 2L) > tree.Length)
                throw new InvalidDataException("A collision tree segment is invalid.");
            return length;
        }

        Dictionary<Id65AuthoringCollisionCell, CompositeCollisionBinding> cells = [];
        int zLength = SegmentLength(0);
        for (int z = 0; z < zLength; z++)
        {
            int yOffset = TreeWord((z + 1) * 2);
            if (yOffset == 0xFFFF)
                continue;
            int yLength = SegmentLength(yOffset);
            for (int y = 0; y < yLength; y++)
            {
                int xOffset = TreeWord(yOffset + ((y + 1) * 2));
                if (xOffset == 0xFFFF)
                    continue;
                int xLength = SegmentLength(xOffset);
                for (int x = 0; x < xLength; x++)
                {
                    int pointerOffset = xOffset + ((x + 1) * 2);
                    int groupOffset = TreeWord(pointerOffset);
                    if (groupOffset == 0xFFFF)
                        continue;
                    if (!groups.TryGetValue(groupOffset, out int[]? sequence))
                        throw new InvalidDataException("A collision leaf points outside a real block group.");
                    Id65AuthoringCollisionCell cell = new(x, y, z);
                    cells.Add(cell, new(cell, pointerOffset, groupOffset, sequence.ToArray()));
                }
            }
        }
        if (!cells.Values.Select(item => item.SourceBlockWordOffset).Distinct().Order()
                .SequenceEqual(groups.Keys.Order()))
            throw new InvalidDataException("The collision blocks contain an unreferenced group.");
        return new(tree.ToArray(), blocks.ToArray(), cells, starts.Count, usedBlockBytes);
    }

    private static int FindTreeLeafPointer(byte[] tree, Id65AuthoringCollisionCell cell)
    {
        int zLength = ReadUInt16(tree, 0);
        if (cell.Z < 0 || cell.Z >= zLength)
            throw new InvalidDataException("The target Z cell is outside the collision tree.");
        int yOffset = ReadUInt16(tree, (cell.Z + 1) * 2);
        if (yOffset == 0xFFFF || cell.Y < 0 || cell.Y >= ReadUInt16(tree, yOffset))
            throw new InvalidDataException("The target Y cell is outside an active collision segment.");
        int xOffset = ReadUInt16(tree, yOffset + ((cell.Y + 1) * 2));
        if (xOffset == 0xFFFF || cell.X < 0 || cell.X >= ReadUInt16(tree, xOffset))
            throw new InvalidDataException("The target X cell is outside an active collision segment.");
        return xOffset + ((cell.X + 1) * 2);
    }

    private static (byte[] Tree, byte[] Blocks, int UsedBytes) RepackCollision(
        CompositeCollisionIndex native,
        IReadOnlyDictionary<Id65AuthoringCollisionCell, int[]> desired,
        int triangleCount,
        Id65AuthoringCollisionCell? addedCell = null,
        int? addedTreePointer = null)
    {
        int expectedCount = native.Cells.Count + (addedCell.HasValue ? 1 : 0);
        if (desired.Count != expectedCount || addedCell.HasValue != addedTreePointer.HasValue)
            throw new InvalidDataException("The collision repack ownership envelope changed.");
        Dictionary<string, int> emitted = new(StringComparer.Ordinal);
        Dictionary<Id65AuthoringCollisionCell, int> offsets = [];
        List<ushort> words = [];
        void Emit(Id65AuthoringCollisionCell cell, int[] sequence)
        {
            if (sequence.Length == 0 || sequence.Any(index => index < 0 || index >= triangleCount) ||
                !IsStrictlyDescending(sequence))
                throw new InvalidDataException("A desired collision-cell sequence is invalid.");
            string key = string.Join(',', sequence);
            if (!emitted.TryGetValue(key, out int offset))
            {
                offset = words.Count;
                for (int index = 0; index < sequence.Length; index++)
                {
                    ushort word = checked((ushort)sequence[index]);
                    if (index == 0)
                        word |= 0x8000;
                    words.Add(word);
                }
                emitted.Add(key, offset);
            }
            offsets.Add(cell, offset);
        }
        foreach (CompositeCollisionBinding binding in native.Cells.Values
                     .OrderBy(item => item.SourceBlockWordOffset)
                     .ThenBy(item => item.Cell.Z).ThenBy(item => item.Cell.Y).ThenBy(item => item.Cell.X))
        {
            if (!desired.TryGetValue(binding.Cell, out int[]? sequence))
                throw new InvalidDataException("A native collision cell lost desired ownership.");
            Emit(binding.Cell, sequence);
        }
        if (addedCell.HasValue)
            Emit(addedCell.Value, desired[addedCell.Value]);
        words.Add(0x8000);
        int usedBytes = checked(words.Count * 2);
        if (usedBytes > native.BlockBytes.Length)
            throw new InvalidDataException("The collision repack exceeds fixed block capacity.");
        byte[] tree = native.TreeBytes.ToArray();
        foreach (CompositeCollisionBinding binding in native.Cells.Values)
            WriteUInt16(tree, binding.TreePointerByteOffset, checked((ushort)offsets[binding.Cell]));
        if (addedCell.HasValue)
            WriteUInt16(tree, addedTreePointer!.Value, checked((ushort)offsets[addedCell.Value]));
        byte[] blocks = new byte[native.BlockBytes.Length];
        for (int index = 0; index < words.Count; index++)
            WriteUInt16(blocks, index * 2, words[index]);
        CompositeCollisionIndex readback = DecodeCollisionIndex(
            tree, blocks, usedBytes, triangleCount, requireZeroTail: true);
        if (readback.Cells.Count != desired.Count ||
            readback.Cells.Any(pair => !desired[pair.Key].SequenceEqual(pair.Value.OrderedTriangleIndexes)))
            throw new InvalidDataException("The collision repack failed exact semantic readback.");
        return (tree, blocks, usedBytes);
    }

    private static byte[] BuildFinalModelFromLockedSource(
        byte[] sourceModel,
        IReadOnlyList<Id65V2NativeTexturePackedRow> textureRows,
        bool terrain)
    {
        byte[] texture = BuildFinalTextureComponent(sourceModel, textureRows);
        byte[] environment = BuildRemoteEnvironment(Slice(sourceModel, 0x02F78, 0x284A4));
        if (terrain)
            environment = ExpandRemoteEnvironmentForOptionalTriangle(environment);
        byte[] occlusion = BuildRemoteOcclusion(Slice(sourceModel, 0x2B41C, 0x974));
        byte[] collision = BuildRemoteCollision(Slice(sourceModel, 0x2BDC0, 0x5FAE8));
        const int collisionTriangleRelativeOffset = 0x47408;
        byte[] positiveTriangle = Convert.FromHexString("100138381001007000020000");
        byte[] negativeTriangle = Convert.FromHexString("10011C701001380000020000");
        if (!collision.AsSpan(collisionTriangleRelativeOffset, 12).SequenceEqual(positiveTriangle))
            throw new InvalidDataException("The direct collision-winding component preimage changed.");
        negativeTriangle.CopyTo(collision, collisionTriangleRelativeOffset);
        RequireHash(Hash(collision),
            UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.ExpectedOutputCollisionComponentSha256,
            "direct v2 collision component");
        if (terrain)
            collision = ExpandRemoteCollisionForOptionalTriangle(collision);

        byte[] final = new byte[ModelLength];
        int cursor = 0;
        void Append(byte[] bytes)
        {
            bytes.CopyTo(final, cursor);
            cursor += bytes.Length;
        }
        Append(texture);
        Append(environment);
        Append(occlusion);
        Append(Slice(sourceModel, 0x2BD90, 0x30));
        Append(collision);
        Append(Slice(sourceModel, 0x8B8A8, 0x84E4));
        Append(Slice(sourceModel, 0x93D8C, 0x4));
        Append(Slice(sourceModel, 0x93D90, 0x80));
        Append(Slice(sourceModel, 0x93E10, 0x6F8));
        int used = terrain ? 0x94664 : 0x94634;
        if (cursor != used || final.AsSpan(cursor).IndexOfAnyExcept((byte)0) >= 0)
            throw new InvalidDataException("The direct final model used/tail boundary changed.");

        int textureWordOffset = terrain ? 0x2B550 : 0x2B540;
        uint textureWord = BinaryPrimitives.ReadUInt32LittleEndian(final.AsSpan(textureWordOffset, 4));
        if ((textureWord & 0x7F) != 25)
            throw new InvalidDataException("The direct final HP face is not bound to source T25.");
        BinaryPrimitives.WriteUInt32LittleEndian(
            final.AsSpan(textureWordOffset, 4),
            (textureWord & 0xFFFFFF80u) | 66u);
        if (terrain &&
            (BinaryPrimitives.ReadUInt32LittleEndian(final.AsSpan(0x2B560, 4)) & 0x7F) != 25)
            throw new InvalidDataException("The added optional HP face lost its independent T25 binding.");

        byte[] finalEnvironment = Slice(final, 0x3030, terrain ? 0x28538 : 0x28518);
        if (terrain)
        {
            RequireHash(Hash(finalEnvironment),
                "c41f70eacc4c507401743dfe7d8fe20de2443b62de78682a16211306b3e4c617",
                "combined final environment");
            int sectorOffset = 4 + ReadInt32(finalEnvironment, 8 + (216 * 4));
            RequireHash(Hash(finalEnvironment.AsSpan(sectorOffset, 0x90)),
                "8537b07f283bdbfebf3816048269e0a93454456749e710a94f7f5ead000c4111",
                "combined final sector 216");
        }
        return final;
    }

    private static byte[] BuildFinalTextureComponent(
        byte[] sourceModel,
        IReadOnlyList<Id65V2NativeTexturePackedRow> packedRows)
    {
        ReadOnlySpan<byte> source = sourceModel.AsSpan(0, 0x2F78);
        if (ReadInt32(source, 0) != 0x2F78 || ReadInt32(source, 4) != 66)
            throw new InvalidDataException("The locked native texture table length/count changed.");
        Dictionary<int, Id65V2NativeTexturePackedRow> packed =
            packedRows.ToDictionary(item => item.TextureId);
        byte[] output = new byte[0x3030];
        WriteInt32(output, 0, output.Length);
        WriteInt32(output, 4, 67);
        int sourceHighStart = 8 + (66 * 16);
        int outputHighStart = 8 + (67 * 16);
        for (int textureId = 0; textureId < 66; textureId++)
        {
            byte[] low = packed.TryGetValue(textureId, out Id65V2NativeTexturePackedRow? row)
                ? row.CopyLow()
                : source.Slice(8 + (textureId * 16), 16).ToArray();
            byte[] high = row is not null
                ? row.CopyHigh()
                : source.Slice(sourceHighStart + (textureId * 168), 168).ToArray();
            low.CopyTo(output, 8 + (textureId * 16));
            high.CopyTo(output, outputHighStart + (textureId * 168));
        }
        Id65V2NativeTexturePackedRow synthetic = packedRows.Single(item => item.Synthetic);
        synthetic.CopyLow().CopyTo(output, 8 + (66 * 16));
        synthetic.CopyHigh().CopyTo(output, outputHighStart + (66 * 168));
        RequireHash(Hash(output),
            Id65V2NativeTextureCompositionCompiler.ExpectedOutputTextureComponentSha256,
            "direct final texture component");
        return output;
    }

    private static byte[] ExpandRemoteEnvironmentForOptionalTriangle(byte[] v2Environment)
    {
        if (v2Environment.Length != 0x28518 || ReadInt32(v2Environment, 0) != v2Environment.Length ||
            ReadInt32(v2Environment, 4) != 217)
            throw new InvalidDataException("The direct v2 environment header changed.");
        int sectorOffset = 4 + ReadInt32(v2Environment, 8 + (216 * 4));
        if (sectorOffset + 0x70 != v2Environment.Length)
            throw new InvalidDataException("Sector 216 is no longer the final v2 sector.");
        byte[] source = v2Environment.AsSpan(sectorOffset, 0x70).ToArray();
        byte[] header = source.AsSpan(0, 28).ToArray();
        header[16] = 4;
        header[18] = 2;
        header[20] = 4;
        header[22] = 2;
        byte[] d = Convert.FromHexString(Id65AuthoringTerrainMutationCompiler.ExpectedAddedVertexWordHex);
        byte[] lowFace = Convert.FromHexString(Id65AuthoringTerrainMutationCompiler.ExpectedLowDetailFaceHex);
        byte[] highFace = Convert.FromHexString(Id65AuthoringTerrainMutationCompiler.ExpectedHighDetailFaceHex);
        byte[] sector = new byte[0x90];
        int cursor = 0;
        void Append(ReadOnlySpan<byte> bytes)
        {
            bytes.CopyTo(sector.AsSpan(cursor));
            cursor += bytes.Length;
        }
        Append(header);
        Append(source.AsSpan(28, 12));
        Append(d);
        Append(source.AsSpan(40, 12));
        Append(source.AsSpan(52, 8));
        Append(lowFace);
        Append(source.AsSpan(60, 12));
        Append(d);
        Append(source.AsSpan(72, 24));
        Append(source.AsSpan(96, 16));
        Append(highFace);
        if (cursor != sector.Length)
            throw new InvalidDataException("The optional terrain sector length changed.");
        RequireHash(Hash(sector), Id65AuthoringTerrainMutationCompiler.ExpectedTwoTileSectorSha256,
            "optional terrain sector before T66 binding");
        byte[] output = new byte[0x28538];
        v2Environment.AsSpan(0, sectorOffset).CopyTo(output);
        sector.CopyTo(output, sectorOffset);
        WriteInt32(output, 0, output.Length);
        RequireHash(Hash(output), Id65AuthoringTerrainMutationCompiler.ExpectedTwoTileEnvironmentSha256,
            "optional terrain environment before T66 binding");
        return output;
    }

    private static byte[] ExpandRemoteCollisionForOptionalTriangle(byte[] v2Collision)
    {
        CompositeCollisionLayout layout = ParseCollisionLayout(
            v2Collision, 19_808, 0x58480, 0x5D1E0, 0x5FAE8);
        byte[] tree = Slice(v2Collision, layout.TreeOffset, layout.TreeCapacityBytes);
        byte[] blocks = Slice(v2Collision, layout.BlocksOffset, layout.BlocksCapacityBytes);
        CompositeCollisionIndex native = DecodeCollisionIndex(
            tree, blocks, 0x17960, 19_808, requireZeroTail: true);
        Id65AuthoringCollisionCell target = new(1, 1, 2);
        if (!native.Cells.TryGetValue(target, out CompositeCollisionBinding? targetBinding) ||
            !targetBinding.OrderedTriangleIndexes.SequenceEqual([13_995]))
            throw new InvalidDataException("The v2 target cell no longer owns exactly T13995.");
        Dictionary<Id65AuthoringCollisionCell, int[]> desired = native.Cells
            .ToDictionary(pair => pair.Key, pair => pair.Value.OrderedTriangleIndexes.ToArray());
        desired[target] = [19_808, 13_995];
        (byte[] outputTree, byte[] outputBlocks, int usedBytes) =
            RepackCollision(native, desired, 19_809);
        if (usedBytes != 0x17962)
            throw new InvalidDataException("The optional terrain collision blocks used length changed.");
        RequireHash(Hash(outputTree), Id65AuthoringTerrainMutationCompiler.ExpectedTwoTileTreeSha256,
            "optional terrain collision tree");
        RequireHash(Hash(outputBlocks), Id65AuthoringTerrainMutationCompiler.ExpectedTwoTileBlocksSha256,
            "optional terrain collision blocks");

        byte[] result = new byte[0x5FAF8];
        WriteInt32(result, 0, result.Length);
        WriteInt32(result, 4, 19_809);
        WriteInt32(result, 8, 0x2904);
        WriteInt32(result, 12, 0x1C);
        WriteInt32(result, 16, 0x6A7C);
        WriteInt32(result, 20, 0x1E400);
        WriteInt32(result, 24, 0x5848C);
        WriteInt32(result, 28, 0x5D1F0);
        int treeOffset = 4 + 0x1C;
        int blocksOffset = 4 + 0x6A7C;
        int trianglesOffset = 4 + 0x1E400;
        int assignmentsOffset = 4 + 0x5848C;
        int flagsOffset = 4 + 0x5D1F0;
        outputTree.CopyTo(result, treeOffset);
        outputBlocks.CopyTo(result, blocksOffset);
        v2Collision.AsSpan(layout.TriangleOffset, 19_808 * 12).CopyTo(result.AsSpan(trianglesOffset));
        Convert.FromHexString(Id65AuthoringTerrainMutationCompiler.ExpectedCollisionTriangleHex)
            .CopyTo(result, trianglesOffset + (19_808 * 12));
        v2Collision.AsSpan(layout.AssignmentsOffset, 19_808).CopyTo(result.AsSpan(assignmentsOffset));
        result[assignmentsOffset + 19_808] = 0;
        v2Collision.AsSpan(layout.FlagsOffset, 0x2904).CopyTo(result.AsSpan(flagsOffset));
        RequireHash(Hash(result), Id65AuthoringTerrainMutationCompiler.ExpectedTwoTileCollisionSha256,
            "optional terrain collision component");
        return result;
    }

    private static List<Id65AuthoringCompositeOwnedRange> BuildLedger(
        byte[] source,
        byte[] finalModel,
        IReadOnlyList<Id65V2NativeTexturePagePatch> texturePatches,
        Id65MobyDependencyBundleDescriptor bundle,
        Id65MobyPlacementIntent[] placements)
    {
        RequireHash(Hash(source.AsSpan(TexturePagesOffset, TexturePagesLength)),
            Id65V2NativeTextureCompositionCompiler.ExpectedSourceTexturePagesSha256,
            "locked texture pages");
        List<Id65AuthoringCompositeOwnedRange> ranges = [];
        for (int index = 0; index < texturePatches.Count; index++)
        {
            Id65V2NativeTexturePagePatch patch = texturePatches[index];
            AddRange(
                ranges,
                source,
                $"texture.page.{index:D4}.{patch.RelativeOffset:X8}",
                patch.Owner,
                TexturePagesOffset + patch.RelativeOffset,
                patch.CopyAfter(),
                patch.CopyBefore());
        }
        AddRange(ranges, source, "model.final", "composite.v2-terrain-t66",
            ModelOffset, finalModel);
        AddRange(ranges, source, "spawn.landing-xy", "composite.v2-spawn",
            LandingOffset, Convert.FromHexString("091800000A180000"));
        byte[] playerXy = new byte[8];
        WriteInt32(playerXy, 0, 6_153);
        WriteInt32(playerXy, 4, 6_154);
        AddRange(ranges, source, "spawn.t92-player-anchor-xy", "composite.v2-spawn",
            T92XyOffset, playerXy);

        byte[] package = Convert.FromHexString(bundle.ActorPackages.Single().ExactHex);
        byte[] properties = Convert.FromHexString(bundle.Properties.Single().ExactHex);
        byte[] rowTemplate = Convert.FromHexString(bundle.Rows.Single().ExactHex);
        AddRange(ranges, source, "asset.root37", bundle.Id, Root37Offset, UInt32(0x1CFA44));
        AddRange(ranges, source, "asset.actor-id37", bundle.Id, ActorId37Offset, UInt16(0x01F5));
        AddRange(ranges, source, "asset.package.01f5", bundle.Id, PackageOffset, package);
        foreach (Id65MobyPlacementIntent placement in placements)
        {
            int offset = placement.TargetTrueIndex == 88 ? T88Offset : T107Offset;
            byte[] row = rowTemplate.ToArray();
            WriteInt32(row, 0, 0x8138);
            if (placement.TargetTrueIndex == 88)
                source.AsSpan(offset + 0x0C, 12).CopyTo(row.AsSpan(0x0C, 12));
            else
            {
                Id65AuthoringPoint point = placement.CoordinatesOrDelta!.Value;
                WriteInt32(row, 0x0C, point.X);
                WriteInt32(row, 0x10, point.Y);
                WriteInt32(row, 0x14, point.Z);
                row[0x47] = placement.YawByte!.Value;
            }
            AddRange(ranges, source, $"placement.t{placement.TargetTrueIndex}", placement.Id, offset, row);
        }
        bool append = placements.Any(item => item.TargetTrueIndex == 107);
        if (append)
        {
            AddRange(ranges, source, "scene.object-count", "allocation.rows",
                ObjectCountOffset, UInt32(108));
            AddRange(ranges, source, "scene.fixup-count", "allocation.fixups",
                FixupCountOffset, UInt32(0x82));
            AddRange(ranges, source, "scene.fixup.t107-properties", "allocation.fixups",
                FixupAppendOffset, UInt32(0x2638));
        }
        AddRange(ranges, source, "asset.properties.grass", bundle.Id, PropertiesOffset, properties);

        Id65AuthoringCompositeOwnedRange[] ordered = ranges
            .OrderBy(item => item.DataRelativeOffset)
            .ThenBy(item => item.StableId, StringComparer.Ordinal)
            .ToArray();
        byte[] pageReadback = source.AsSpan(TexturePagesOffset, TexturePagesLength).ToArray();
        foreach (Id65AuthoringCompositeOwnedRange range in ordered.Where(item =>
                     item.DataRelativeOffset >= TexturePagesOffset &&
                     item.DataRelativeOffset + item.ByteLength <= TexturePagesOffset + TexturePagesLength))
            range.CopyAfter().CopyTo(pageReadback, range.DataRelativeOffset - TexturePagesOffset);
        RequireHash(Hash(pageReadback),
            Id65V2NativeTextureCompositionCompiler.ExpectedOutputTexturePagesSha256,
            "direct final texture pages");
        return ordered.ToList();
    }

    private static void AddRange(
        List<Id65AuthoringCompositeOwnedRange> ranges,
        byte[] source,
        string stableId,
        string owner,
        int offset,
        byte[] after,
        byte[]? declaredBefore = null)
    {
        if (offset < 0 || after.Length <= 0 || offset + (long)after.Length > source.Length)
            throw new InvalidDataException($"Composite range `{stableId}` is out of bounds.");
        byte[] before = source.AsSpan(offset, after.Length).ToArray();
        if (declaredBefore is not null && !before.SequenceEqual(declaredBefore))
            throw new InvalidDataException($"Composite range `{stableId}` lost its declared locked preimage.");
        if (before.SequenceEqual(after))
            throw new InvalidDataException($"Composite range `{stableId}` became redundant.");
        ranges.Add(new(stableId, owner, offset, before, after));
    }

    private static void ValidateLedger(
        byte[] source,
        IReadOnlyList<Id65AuthoringCompositeOwnedRange> ledger)
    {
        if (ledger.Count < 12 || ledger.Select(item => item.StableId).Distinct(StringComparer.Ordinal).Count() !=
            ledger.Count)
            throw new InvalidDataException("The composite ownership ledger is empty or has duplicate IDs.");
        for (int index = 0; index < ledger.Count; index++)
        {
            Id65AuthoringCompositeOwnedRange range = ledger[index];
            byte[] before = range.CopyBefore();
            byte[] after = range.CopyAfter();
            if (before.Length != range.ByteLength || after.Length != range.ByteLength ||
                Hash(before) != range.BeforeSha256 || Hash(after) != range.AfterSha256 ||
                !source.AsSpan(range.DataRelativeOffset, range.ByteLength).SequenceEqual(before))
                throw new InvalidDataException($"Composite range `{range.StableId}` lost byte/hash identity.");
            if (index > 0 && RangesOverlap(
                    ledger[index - 1].DataRelativeOffset,
                    ledger[index - 1].ByteLength,
                    range.DataRelativeOffset,
                    range.ByteLength))
                throw new InvalidDataException("Composite owned ranges overlap.");
        }
        if (ledger.Count(item => item.StableId.StartsWith("texture.page.", StringComparison.Ordinal)) != 7_949 ||
            ledger.Count(item => item.StableId == "model.final") != 1 ||
            ledger.Count(item => item.OwnerId == "composite.v2-spawn") != 2)
            throw new InvalidDataException("The texture/model/spawn ownership partition changed.");
    }

    private static byte[] ApplyLedger(
        byte[] input,
        IReadOnlyList<Id65AuthoringCompositeOwnedRange> ledger,
        bool reverse)
    {
        byte[] output = input.ToArray();
        IEnumerable<Id65AuthoringCompositeOwnedRange> ordered = reverse
            ? ledger.Reverse()
            : ledger;
        foreach (Id65AuthoringCompositeOwnedRange range in ordered)
        {
            byte[] expected = reverse ? range.CopyAfter() : range.CopyBefore();
            byte[] replacement = reverse ? range.CopyBefore() : range.CopyAfter();
            if (!output.AsSpan(range.DataRelativeOffset, range.ByteLength).SequenceEqual(expected))
                throw new InvalidDataException(
                    $"Composite range `{range.StableId}` lost exact {(reverse ? "afterimage" : "preimage")}.");
            replacement.CopyTo(output, range.DataRelativeOffset);
        }
        return output;
    }

    private static void VerifyFinalReadback(
        byte[] source,
        byte[] output,
        byte[] finalModel,
        bool terrain,
        Id65MobyPlacementIntent[] placements)
    {
        if (!output.AsSpan(ModelOffset, ModelLength).SequenceEqual(finalModel))
            throw new InvalidDataException("The final model escaped its one whole-model ledger range.");
        RequireHash(Hash(output.AsSpan(TexturePagesOffset, TexturePagesLength)),
            Id65V2NativeTextureCompositionCompiler.ExpectedOutputTexturePagesSha256,
            "final texture pages");
        RequireHash(Hash(finalModel), terrain
            ? ExpectedCombinedModelSha256
            : Id65V2NativeTextureCompositionCompiler.ExpectedOutputModelSha256,
            "final model readback");
        if (ReadInt32(finalModel, 0) != 0x3030 || ReadInt32(finalModel, 4) != 67)
            throw new InvalidDataException("The final private T66 texture table changed.");
        int textureWordOffset = terrain ? 0x2B550 : 0x2B540;
        if ((ReadUInt32(finalModel, textureWordOffset) & 0x7F) != 66)
            throw new InvalidDataException("The final authored HP face did not bind private T66.");
        if (terrain && (ReadUInt32(finalModel, 0x2B560) & 0x7F) != 25)
            throw new InvalidDataException("The added optional HP face did not preserve T25.");

        bool t88 = placements.Any(item => item.TargetTrueIndex == 88);
        bool t107 = placements.Any(item => item.TargetTrueIndex == 107);
        if (ReadUInt32(output, Root37Offset) != 0x1CFA44 ||
            ReadUInt16(output, ActorId37Offset) != 0x01F5 ||
            ReadInt32(output, ObjectCountOffset) != (t107 ? 108 : 107) ||
            ReadInt32(output, FixupCountOffset) != (t107 ? 0x82 : 0x81) ||
            Hash(output.AsSpan(PackageOffset, PackageLength)) !=
                "90ca71a190c4567817d728753f25df667d56515e1721d24e19e6da9dc07edcc3" ||
            Hash(output.AsSpan(PropertiesOffset, 8)) !=
                "9eeeff662fd5b77dbc35de8ed01e0d1fd149cee49126625b69f65553c4b7c20b")
            throw new InvalidDataException("The final Moby root/package/count/fixup/properties readback changed.");
        if (t88 && Hash(output.AsSpan(T88Offset, RecordLength)) !=
            "4228cf9bf97a8309f26b9f1303174e116916e2f9633388f2346a654e97cdb56f")
            throw new InvalidDataException("The final ZeroEgg T88 row changed.");
        if (t107 && Hash(output.AsSpan(T107Offset, RecordLength)) !=
            "d42e49248278759d2c2020bba4d4c4db18817bf120d73829a2bbba7dddcef9fb")
            throw new InvalidDataException("The final Grass T107 row changed.");
        RequireHash(Hash(source.AsSpan(PrivateThiefBlockOffset, PrivateThiefBlockLength)),
            "6213aceb752cb139ac669b392a3faf7441888306b363fb4cf3e2c63f45715136",
            "locked retired-thief private block");
        if (!source.AsSpan(PrivateThiefBlockOffset, PrivateThiefBlockLength)
                .SequenceEqual(output.AsSpan(PrivateThiefBlockOffset, PrivateThiefBlockLength)))
            throw new InvalidDataException("The retired thief private block/path changed.");
        int fixupCount = ReadInt32(output, FixupCountOffset);
        int t88Fixups = 0;
        int t107Fixups = 0;
        int privateFixups = 0;
        for (int index = 0; index < fixupCount; index++)
        {
            int field = ReadInt32(output, FixupListOffset + (index * 4));
            if (field == 0x1FB0) t88Fixups++;
            if (field == 0x2638) t107Fixups++;
            if (field == 0x5AFC) privateFixups++;
        }
        if (t88Fixups != 1 || privateFixups != 1 || t107Fixups != (t107 ? 1 : 0))
            throw new InvalidDataException(
                "The final T88/private/T107 scene-fixup occurrence readback changed.");
        if (!output.AsSpan(LandingOffset, 8).SequenceEqual(Convert.FromHexString("091800000A180000")) ||
            ReadInt32(output, T92XyOffset) != 6_153 || ReadInt32(output, T92XyOffset + 4) != 6_154)
            throw new InvalidDataException("The direct v2 landing/player-anchor XY readback changed.");

        int collisionOffset = terrain ? 0x2BF0C : 0x2BEEC;
        int collisionLength = terrain ? 0x5FAF8 : 0x5FAE8;
        string collisionHash = terrain
            ? Id65AuthoringTerrainMutationCompiler.ExpectedTwoTileCollisionSha256
            : UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.ExpectedOutputCollisionComponentSha256;
        RequireHash(Hash(finalModel.AsSpan(collisionOffset, collisionLength)), collisionHash,
            "final collision component");
        int triangleIndex = terrain ? 19_808 : 13_995;
        int triangleOffset = collisionOffset + 4 + 0x1E400 + (triangleIndex * 12);
        string expectedTriangle = terrain
            ? Id65AuthoringTerrainMutationCompiler.ExpectedCollisionTriangleHex
            : "10011C701001380000020000";
        if (Convert.ToHexString(finalModel.AsSpan(triangleOffset, 12)) != expectedTriangle)
            throw new InvalidDataException("The final collision triangle lost exact negative winding.");

        if (terrain && t88 && t107)
        {
            RequireHash(Hash(output.AsSpan(0, 0x200)),
                "6157dc52dbf53f744378aa5577209751dc75eb397401504ba23a28f1d5dabd71",
                "combined header");
            RequireHash(Hash(output.AsSpan(0x173000, 0x5D000)),
                "e5e0f898a2d9487d56da9b28c1fb53706df219380ce5831f79334b06ebd1118f",
                "combined actor subfile");
            RequireHash(Hash(output.AsSpan(0x1D0000, 0x8800)),
                "170dcef6eba922addeddae47449150f905e94fe5de9d8b3af2834d165b13a0bf",
                "combined scene");
            RequireHash(Hash(output.AsSpan(0x1D0170, 108 * 0x58)),
                "bf4c7d4f3f2f2f89c20da6a7d63c9033f4f109fad28a58c74212b95b83783104",
                "combined object table");
            RequireHash(Hash(output), ExpectedCombinedOutputRow80Sha256, "combined row-80 witness");
        }
    }

    private static Id65ModelComponentRelocation[] BuildRelocations(
        byte[] source,
        byte[] output,
        bool terrain)
    {
        (string Name, int Source, int SourceLength, int Output, int OutputLength)[] layout = terrain
            ?
            [
                ("texture", 0x00000, 0x2F78, 0x00000, 0x3030),
                ("environment", 0x02F78, 0x284A4, 0x03030, 0x28538),
                ("occlusion", 0x2B41C, 0x974, 0x2B568, 0x974),
                ("special-surface", 0x2BD90, 0x30, 0x2BEDC, 0x30),
                ("collision", 0x2BDC0, 0x5FAE8, 0x2BF0C, 0x5FAF8),
                ("cyclorama", 0x8B8A8, 0x84E4, 0x8BA04, 0x84E4),
                ("portal-table", 0x93D8C, 0x4, 0x93EE8, 0x4),
                ("particles", 0x93D90, 0x80, 0x93EEC, 0x80),
                ("sound", 0x93E10, 0x6F8, 0x93F6C, 0x6F8)
            ]
            :
            [
                ("texture", 0x00000, 0x2F78, 0x00000, 0x3030),
                ("environment", 0x02F78, 0x284A4, 0x03030, 0x28518),
                ("occlusion", 0x2B41C, 0x974, 0x2B548, 0x974),
                ("special-surface", 0x2BD90, 0x30, 0x2BEBC, 0x30),
                ("collision", 0x2BDC0, 0x5FAE8, 0x2BEEC, 0x5FAE8),
                ("cyclorama", 0x8B8A8, 0x84E4, 0x8B9D4, 0x84E4),
                ("portal-table", 0x93D8C, 0x4, 0x93EB8, 0x4),
                ("particles", 0x93D90, 0x80, 0x93EBC, 0x80),
                ("sound", 0x93E10, 0x6F8, 0x93F3C, 0x6F8)
            ];
        return layout.Select(item =>
        {
            byte[] before = Slice(source, item.Source, item.SourceLength);
            byte[] after = Slice(output, item.Output, item.OutputLength);
            return new Id65ModelComponentRelocation(
                $"component.{item.Name}",
                item.Source,
                item.SourceLength,
                item.Output,
                item.OutputLength,
                Hash(before),
                Hash(after),
                before.SequenceEqual(after));
        }).ToArray();
    }

    private static void ValidateRelocations(
        IReadOnlyList<Id65ModelComponentRelocation> relocations,
        byte[] source,
        byte[] output,
        bool terrain)
    {
        string[] expectedIds =
        [
            "component.texture",
            "component.environment",
            "component.occlusion",
            "component.special-surface",
            "component.collision",
            "component.cyclorama",
            "component.portal-table",
            "component.particles",
            "component.sound"
        ];
        if (relocations.Count != expectedIds.Length ||
            relocations.Select(item => item.StableId).Distinct(StringComparer.Ordinal).Count() !=
                relocations.Count ||
            !relocations.Select(item => item.StableId).SequenceEqual(expectedIds))
            throw new InvalidDataException("The direct component relocation identity set changed.");

        int sourceCursor = 0;
        int outputCursor = 0;
        foreach (Id65ModelComponentRelocation item in relocations)
        {
            if (item.SourceRelativeOffset != sourceCursor || item.OutputRelativeOffset != outputCursor ||
                item.SourceByteLength <= 0 || item.OutputByteLength <= 0 ||
                item.SourceRelativeOffset + (long)item.SourceByteLength > source.Length ||
                item.OutputRelativeOffset + (long)item.OutputByteLength > output.Length ||
                Hash(source.AsSpan(item.SourceRelativeOffset, item.SourceByteLength)) != item.SourceSha256 ||
                Hash(output.AsSpan(item.OutputRelativeOffset, item.OutputByteLength)) != item.OutputSha256 ||
                source.AsSpan(item.SourceRelativeOffset, item.SourceByteLength)
                    .SequenceEqual(output.AsSpan(item.OutputRelativeOffset, item.OutputByteLength)) !=
                    item.ContentsPreserved)
                throw new InvalidDataException($"Direct component relocation `{item.StableId}` changed.");
            sourceCursor += item.SourceByteLength;
            outputCursor += item.OutputByteLength;
        }
        string[] preserved =
        [
            "component.special-surface",
            "component.cyclorama",
            "component.portal-table",
            "component.particles",
            "component.sound"
        ];
        if (sourceCursor != 0x94508 || outputCursor != (terrain ? 0x94664 : 0x94634) ||
            !relocations.Where(item => item.ContentsPreserved).Select(item => item.StableId)
                .SequenceEqual(preserved) ||
            source.AsSpan(sourceCursor).IndexOfAnyExcept((byte)0) >= 0 ||
            output.AsSpan(outputCursor).IndexOfAnyExcept((byte)0) >= 0)
            throw new InvalidDataException(
                "The direct component map lost exact used-span coverage or protected preservation.");
    }

    private static Id65StableHandleRebase[] BuildRebases(
        bool terrain,
        Id65MobyPlacementIntent[] placements)
    {
        List<Id65StableHandleRebase> values =
        [
            new("sector.remote-pad.216", "scene-sector", null, new("scene-sector", 216, 0)),
            new("terrain.remote-pad.face.0:lp", "low-detail-face", null, new("scene-sector", 216, 0)),
            new("terrain.remote-pad.face.0:hp", "high-detail-face", null, new("scene-sector", 216, 0)),
            new("collision.remote-pad.t13995", "collision-triangle",
                new("collision-triangle", 13_995, 0), new("collision-triangle", 13_995, 0)),
            new("occlusion.remote-pad.group0", "occlusion-membership",
                null, new("occlusion-group", 0, 216)),
            new("texture.locked.record25", "texture-record",
                new("texture-record", 25, 0), new("texture-record", 25, 0)),
            new("spawn.remote-pad", "landing-record",
                new("landing-record", 0, 0), new("landing-record", 0, 0)),
            new("moby.player-anchor.t92", "moby-row",
                new("moby-row", 92, 0), new("moby-row", 92, 0)),
            new("music.slot35", "music-slot",
                new("music-slot", 35, 0), new("music-slot", 35, 0)),
            new("totals.slot65", "totals-slot",
                new("level-slot", 65, 0), new("level-slot", 65, 0)),
            new("exit.slot65", "exit-slot",
                new("level-slot", 65, 0), new("level-slot", 65, 0)),
            new("save.slot65", "save-slot",
                new("level-slot", 65, 0), new("level-slot", 65, 0)),
            new("vertex.remote-pad.a:lp", "low-detail-vertex", null, new("scene-sector", 216, 0)),
            new("vertex.remote-pad.a:hp", "high-detail-vertex", null, new("scene-sector", 216, 0)),
            new("vertex.remote-pad.b:lp", "low-detail-vertex", null, new("scene-sector", 216, 1)),
            new("vertex.remote-pad.b:hp", "high-detail-vertex", null, new("scene-sector", 216, 1)),
            new("vertex.remote-pad.c:lp", "low-detail-vertex", null, new("scene-sector", 216, 2)),
            new("vertex.remote-pad.c:hp", "high-detail-vertex", null, new("scene-sector", 216, 2)),
            new(Id65V2NativeTextureCompositionCompiler.PrivateTextureIntentId, "texture-record",
                null, new("texture-record", 66, 0)),
            new("asset.root.actor-01f5", "actor-root",
                new("actor-root.artisans", 22, 0x01F5), new("actor-root.id65", 37, 0x01F5)),
            new(Id65AuthoringMobyDependencyBundleCompiler.ArtisansGrassPackageAssetId, "actor-package",
                new("data.artisans", 0x1C1520, 0), new("data.id65", PackageOffset, 0)),
            new(Id65AuthoringMobyDependencyBundleCompiler.ArtisansGrassPropertiesAssetId, "scene-properties",
                new("scene.artisans", 0x11ACC, 0), new("scene.id65", 0x8138, 0))
        ];
        if (terrain)
        {
            values.Add(new(Id65AuthoringTerrainMutationCompiler.TriangleId, "terrain-tile",
                null, new("terrain-tile", 216, 1)));
            values.Add(new($"{Id65AuthoringTerrainMutationCompiler.AddedVertexHandle}:lp", "low-detail-vertex",
                null, new("scene-sector", 216, 3)));
            values.Add(new($"{Id65AuthoringTerrainMutationCompiler.AddedVertexHandle}:hp", "high-detail-vertex",
                null, new("scene-sector", 216, 3)));
            values.Add(new($"{Id65AuthoringTerrainMutationCompiler.TriangleId}:lp", "low-detail-face",
                null, new("scene-sector", 216, 1)));
            values.Add(new($"{Id65AuthoringTerrainMutationCompiler.TriangleId}:hp", "high-detail-face",
                null, new("scene-sector", 216, 1)));
            values.Add(new($"{Id65AuthoringTerrainMutationCompiler.TriangleId}:collision", "collision-triangle",
                null, new("collision-triangle", 19_808, 0)));
        }
        foreach (Id65MobyPlacementIntent placement in placements)
            values.Add(new(placement.Id, "moby-row",
                null, new("object-row.id65", placement.TargetTrueIndex, 0)));
        if (placements.Any(item => item.TargetTrueIndex == 88))
            values.Add(new("moby.retired-thief.t88", "retired-moby-row",
                new("object-row.id65", 88, 0), null));
        return values.OrderBy(item => item.StableId, StringComparer.Ordinal).ToArray();
    }

    private static void ValidateRebases(
        IReadOnlyList<Id65StableHandleRebase> rebases,
        bool terrain,
        Id65MobyPlacementIntent[] placements)
    {
        int expectedCount = 22 + (terrain ? 6 : 0) + placements.Length +
            (placements.Any(item => item.TargetTrueIndex == 88) ? 1 : 0);
        if (rebases.Count != expectedCount ||
            rebases.Select(item => item.StableId).Distinct(StringComparer.Ordinal).Count() != rebases.Count ||
            rebases.Count(item => item.Output is null) !=
                (placements.Any(item => item.TargetTrueIndex == 88) ? 1 : 0))
            throw new InvalidDataException("The direct stable-handle map is incomplete or conflicting.");

        RequireRebase(rebases, "sector.remote-pad.216", "scene-sector", null,
            new("scene-sector", 216, 0));
        RequireRebase(rebases, "terrain.remote-pad.face.0:lp", "low-detail-face", null,
            new("scene-sector", 216, 0));
        RequireRebase(rebases, "terrain.remote-pad.face.0:hp", "high-detail-face", null,
            new("scene-sector", 216, 0));
        RequireRebase(rebases, "collision.remote-pad.t13995", "collision-triangle",
            new("collision-triangle", 13_995, 0), new("collision-triangle", 13_995, 0));
        RequireRebase(rebases, "occlusion.remote-pad.group0", "occlusion-membership", null,
            new("occlusion-group", 0, 216));
        RequireRebase(rebases, "texture.locked.record25", "texture-record",
            new("texture-record", 25, 0), new("texture-record", 25, 0));
        RequireRebase(rebases, "spawn.remote-pad", "landing-record",
            new("landing-record", 0, 0), new("landing-record", 0, 0));
        RequireRebase(rebases, "moby.player-anchor.t92", "moby-row",
            new("moby-row", 92, 0), new("moby-row", 92, 0));
        RequireRebase(rebases, "music.slot35", "music-slot",
            new("music-slot", 35, 0), new("music-slot", 35, 0));
        RequireRebase(rebases, "totals.slot65", "totals-slot",
            new("level-slot", 65, 0), new("level-slot", 65, 0));
        RequireRebase(rebases, "exit.slot65", "exit-slot",
            new("level-slot", 65, 0), new("level-slot", 65, 0));
        RequireRebase(rebases, "save.slot65", "save-slot",
            new("level-slot", 65, 0), new("level-slot", 65, 0));
        foreach ((string id, int ordinal) in new[]
                 {
                     ("vertex.remote-pad.a", 0),
                     ("vertex.remote-pad.b", 1),
                     ("vertex.remote-pad.c", 2)
                 })
        {
            RequireRebase(rebases, $"{id}:lp", "low-detail-vertex", null,
                new("scene-sector", 216, ordinal));
            RequireRebase(rebases, $"{id}:hp", "high-detail-vertex", null,
                new("scene-sector", 216, ordinal));
        }
        RequireRebase(rebases, Id65V2NativeTextureCompositionCompiler.PrivateTextureIntentId,
            "texture-record", null, new("texture-record", 66, 0));
        RequireRebase(rebases, "asset.root.actor-01f5", "actor-root",
            new("actor-root.artisans", 22, 0x01F5), new("actor-root.id65", 37, 0x01F5));
        RequireRebase(rebases, Id65AuthoringMobyDependencyBundleCompiler.ArtisansGrassPackageAssetId,
            "actor-package", new("data.artisans", 0x1C1520, 0), new("data.id65", PackageOffset, 0));
        RequireRebase(rebases, Id65AuthoringMobyDependencyBundleCompiler.ArtisansGrassPropertiesAssetId,
            "scene-properties", new("scene.artisans", 0x11ACC, 0), new("scene.id65", 0x8138, 0));

        if (terrain)
        {
            RequireRebase(rebases, Id65AuthoringTerrainMutationCompiler.TriangleId, "terrain-tile", null,
                new("terrain-tile", 216, 1));
            RequireRebase(rebases, $"{Id65AuthoringTerrainMutationCompiler.AddedVertexHandle}:lp",
                "low-detail-vertex", null, new("scene-sector", 216, 3));
            RequireRebase(rebases, $"{Id65AuthoringTerrainMutationCompiler.AddedVertexHandle}:hp",
                "high-detail-vertex", null, new("scene-sector", 216, 3));
            RequireRebase(rebases, $"{Id65AuthoringTerrainMutationCompiler.TriangleId}:lp",
                "low-detail-face", null, new("scene-sector", 216, 1));
            RequireRebase(rebases, $"{Id65AuthoringTerrainMutationCompiler.TriangleId}:hp",
                "high-detail-face", null, new("scene-sector", 216, 1));
            RequireRebase(rebases, $"{Id65AuthoringTerrainMutationCompiler.TriangleId}:collision",
                "collision-triangle", null, new("collision-triangle", 19_808, 0));
        }
        foreach (Id65MobyPlacementIntent placement in placements)
            RequireRebase(rebases, placement.Id, "moby-row", null,
                new("object-row.id65", placement.TargetTrueIndex, 0));
        if (placements.Any(item => item.TargetTrueIndex == 88))
            RequireRebase(rebases, "moby.retired-thief.t88", "retired-moby-row",
                new("object-row.id65", 88, 0), null);

        string[] forbiddenCollapsedAliases =
        [
            "vertex.remote-pad.a", "vertex.remote-pad.b", "vertex.remote-pad.c",
            "terrain.remote-pad.face.0", "spawn.remote-pad.landing"
        ];
        if (rebases.Any(item => forbiddenCollapsedAliases.Contains(item.StableId, StringComparer.Ordinal)))
            throw new InvalidDataException("A collapsed or renamed stable handle escaped into the direct map.");
    }

    private static void RequireRebase(
        IReadOnlyList<Id65StableHandleRebase> rebases,
        string stableId,
        string kind,
        Id65LogicalAddress? source,
        Id65LogicalAddress? output)
    {
        Id65StableHandleRebase item = rebases.Single(value => value.StableId == stableId);
        if (item.Kind != kind || item.Source != source || item.Output != output)
            throw new InvalidDataException($"Stable handle `{stableId}` lost exact direct identity.");
    }

    private static Id65MobyAssetRelocation[] BuildMobyAssetRelocations(
        Id65MobyDependencyBundleDescriptor bundle,
        Id65MobyPlacementIntent[] placements)
    {
        bool shared = placements.Length > 1;
        List<Id65MobyAssetRelocation> result =
        [
            new(
                "asset.root.actor-01f5",
                "actor-root",
                new("actor-root.artisans", 22, 0x01F5),
                new("actor-root.id65", 37, 0x01F5),
                6,
                Hash(Encoding.ASCII.GetBytes("01f5:1cfa44")),
                shared),
            new(
                bundle.ActorPackages.Single().Id,
                "actor-package",
                new("data.artisans", 0x1C1520, 0),
                new("data.id65", PackageOffset, 0),
                PackageLength,
                "90ca71a190c4567817d728753f25df667d56515e1721d24e19e6da9dc07edcc3",
                shared),
            new(
                bundle.Properties.Single().Id,
                "scene-properties",
                new("scene.artisans", 0x11ACC, 0),
                new("scene.id65", 0x8138, 0),
                8,
                "9eeeff662fd5b77dbc35de8ed01e0d1fd149cee49126625b69f65553c4b7c20b",
                shared)
        ];
        foreach (Id65MobyPlacementIntent placement in placements.OrderBy(item => item.TargetTrueIndex))
        {
            string rowSha = placement.TargetTrueIndex == 88
                ? "4228cf9bf97a8309f26b9f1303174e116916e2f9633388f2346a654e97cdb56f"
                : "d42e49248278759d2c2020bba4d4c4db18817bf120d73829a2bbba7dddcef9fb";
            result.Add(new(
                $"{Id65AuthoringMobyDependencyBundleCompiler.ArtisansGrassRowTemplateId}.to-t{placement.TargetTrueIndex}",
                "moby-row-template",
                new("object-row.artisans", 121, 0),
                new("object-row.id65", placement.TargetTrueIndex, 0),
                RecordLength,
                rowSha,
                false));
        }
        return result.ToArray();
    }

    private static void ValidateMobyAssetRelocations(
        IReadOnlyList<Id65MobyAssetRelocation> relocations,
        Id65MobyDependencyBundleDescriptor bundle,
        Id65MobyPlacementIntent[] placements)
    {
        if (relocations.Count != 3 + placements.Length ||
            relocations.Select(item => item.StableId).Distinct(StringComparer.Ordinal).Count() !=
                relocations.Count)
            throw new InvalidDataException("The direct Moby asset-relocation set changed.");
        bool shared = placements.Length > 1;
        Id65MobyAssetRelocation root = relocations.Single(item => item.StableId == "asset.root.actor-01f5");
        Id65MobyAssetRelocation package = relocations.Single(item =>
            item.StableId == Id65AuthoringMobyDependencyBundleCompiler.ArtisansGrassPackageAssetId);
        Id65MobyAssetRelocation properties = relocations.Single(item =>
            item.StableId == Id65AuthoringMobyDependencyBundleCompiler.ArtisansGrassPropertiesAssetId);
        if (root.Kind != "actor-root" || root.Source != new Id65LogicalAddress("actor-root.artisans", 22, 0x01F5) ||
            root.Output != new Id65LogicalAddress("actor-root.id65", 37, 0x01F5) || root.ByteLength != 6 ||
            root.ContentSha256 != Hash(Encoding.ASCII.GetBytes("01f5:1cfa44")) || root.Deduplicated != shared ||
            package.Kind != "actor-package" || package.Source != new Id65LogicalAddress("data.artisans", 0x1C1520, 0) ||
            package.Output != new Id65LogicalAddress("data.id65", PackageOffset, 0) ||
            package.ByteLength != PackageLength || package.ContentSha256 !=
                "90ca71a190c4567817d728753f25df667d56515e1721d24e19e6da9dc07edcc3" ||
            package.Deduplicated != shared ||
            properties.Kind != "scene-properties" ||
            properties.Source != new Id65LogicalAddress("scene.artisans", 0x11ACC, 0) ||
            properties.Output != new Id65LogicalAddress("scene.id65", 0x8138, 0) ||
            properties.ByteLength != 8 || properties.ContentSha256 !=
                "9eeeff662fd5b77dbc35de8ed01e0d1fd149cee49126625b69f65553c4b7c20b" ||
            properties.Deduplicated != shared || bundle.CanonicalSha256 !=
                Id65AuthoringMobyDependencyBundleCompiler.ExpectedArtisansGrassBundleSha256)
            throw new InvalidDataException("A direct shared Moby asset lost exact Artisans provenance.");
        foreach (Id65MobyPlacementIntent placement in placements)
        {
            Id65MobyAssetRelocation row = relocations.Single(item => item.StableId ==
                $"{Id65AuthoringMobyDependencyBundleCompiler.ArtisansGrassRowTemplateId}.to-t{placement.TargetTrueIndex}");
            string expectedHash = placement.TargetTrueIndex == 88
                ? "4228cf9bf97a8309f26b9f1303174e116916e2f9633388f2346a654e97cdb56f"
                : "d42e49248278759d2c2020bba4d4c4db18817bf120d73829a2bbba7dddcef9fb";
            if (row.Kind != "moby-row-template" ||
                row.Source != new Id65LogicalAddress("object-row.artisans", 121, 0) ||
                row.Output != new Id65LogicalAddress("object-row.id65", placement.TargetTrueIndex, 0) ||
                row.ByteLength != RecordLength || row.ContentSha256 != expectedHash || row.Deduplicated)
                throw new InvalidDataException("A direct Moby row-template relocation changed.");
        }
    }

    private static Id65MobyPlacementReadback[] BuildMobyPlacementReadback(
        byte[] output,
        Id65MobyPlacementIntent[] placements)
    {
        Id65MobyPlacementReadback[] result = placements.OrderBy(item => item.TargetTrueIndex).Select(placement =>
        {
            int offset = placement.TargetTrueIndex == 88 ? T88Offset : T107Offset;
            ReadOnlySpan<byte> row = output.AsSpan(offset, RecordLength);
            return new Id65MobyPlacementReadback(
                placement.Id,
                placement.TargetTrueIndex,
                placement.RowPolicy,
                ReadInt32(row, 0x0C),
                ReadInt32(row, 0x10),
                ReadInt32(row, 0x14),
                ReadUInt16(row, 0x36),
                ReadInt32(row, 0),
                Hash(row));
        }).ToArray();
        foreach (Id65MobyPlacementReadback item in result)
        {
            (int X, int Y, int Z, Id65MobyRowAllocationPolicy Policy, string Sha256) expected =
                item.TargetTrueIndex switch
                {
                    88 => (96_850, 141_732, 12_248, Id65MobyRowAllocationPolicy.ReplaceExact,
                        "4228cf9bf97a8309f26b9f1303174e116916e2f9633388f2346a654e97cdb56f"),
                    107 => (124_384, 102_304, 8_192, Id65MobyRowAllocationPolicy.AppendContiguous,
                        "d42e49248278759d2c2020bba4d4c4db18817bf120d73829a2bbba7dddcef9fb"),
                    _ => throw new InvalidDataException("An unsupported Moby placement escaped readback.")
                };
            if (item.RawX != expected.X || item.RawY != expected.Y || item.RawZ != expected.Z ||
                item.RowPolicy != expected.Policy || item.ActorId != 0x01F5 ||
                item.PropertiesSceneOffset != 0x8138 || item.RowSha256 != expected.Sha256)
                throw new InvalidDataException(
                    $"The direct semantic placement T{item.TargetTrueIndex} changed.");
        }
        return result;
    }

    private static Id65AuthoringCompositeTerrainReadback BuildTerrainReadback(
        byte[] finalModel,
        bool terrain)
    {
        int environmentOffset = 0x3030;
        int environmentLength = terrain ? 0x28538 : 0x28518;
        ReadOnlySpan<byte> environment = finalModel.AsSpan(environmentOffset, environmentLength);
        int sectorRelativeOffset = 4 + ReadInt32(environment, 8 + (216 * 4));
        int sectorLength = terrain ? 0x90 : 0x70;
        int collisionOffset = terrain ? 0x2BF0C : 0x2BEEC;
        int collisionLength = terrain ? 0x5FAF8 : 0x5FAE8;
        ReadOnlySpan<byte> collision = finalModel.AsSpan(collisionOffset, collisionLength);
        int baseTriangleOffset = 4 + 0x1E400 + (13_995 * 12);
        string baseTriangle = Convert.ToHexString(collision.Slice(baseTriangleOffset, 12));
        ReadOnlySpan<byte> tree = collision.Slice(4 + 0x1C, 0x6A60);
        ReadOnlySpan<byte> blocks = collision.Slice(4 + 0x6A7C, 0x17984);
        byte[] occlusion = Slice(finalModel, terrain ? 0x2B568 : 0x2B548, 0x974);
        IReadOnlyList<int>[] groups = ParseOcclusionGroups(occlusion, 217, out _);
        Id65TerrainTriangleReadback? optional = terrain
            ? new(
                Id65AuthoringTerrainMutationCompiler.TriangleId,
                Id65AuthoringTerrainMutationCompiler.AddedVertexHandle,
                false,
                true,
                216,
                3,
                3,
                1,
                1,
                19_808,
                Id65AuthoringTerrainMutationCompiler.ExpectedAddedVertexWordHex,
                Id65AuthoringTerrainMutationCompiler.ExpectedLowDetailFaceHex,
                Id65AuthoringTerrainMutationCompiler.ExpectedHighDetailFaceHex,
                Id65AuthoringTerrainMutationCompiler.ExpectedCollisionTriangleHex,
                25_088,
                -25_088,
                new(1, 1, 2),
                Array.AsReadOnly(new[] { 19_808, 13_995 }),
                0)
            : null;
        Id65AuthoringCompositeTerrainReadback result = new(
            true,
            terrain,
            216,
            terrain ? 4 : 3,
            terrain ? 2 : 1,
            terrain ? 4 : 3,
            terrain ? 2 : 1,
            13_995,
            baseTriangle,
            -50_176,
            Hash(environment),
            Hash(environment.Slice(sectorRelativeOffset, sectorLength)),
            Hash(collision),
            Hash(tree),
            Hash(blocks),
            terrain ? 0x17962 : 0x17960,
            0,
            groups[0].Contains(216),
            optional);
        if (result.BaseCollisionTriangleHex != "10011C701001380000020000" ||
            !result.OcclusionContainsSector ||
            result.CollisionSha256 != (terrain
                ? Id65AuthoringTerrainMutationCompiler.ExpectedTwoTileCollisionSha256
                : UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.ExpectedOutputCollisionComponentSha256) ||
            result.CollisionTreeSha256 != Id65AuthoringTerrainMutationCompiler.ExpectedTwoTileTreeSha256 ||
            result.CollisionBlocksSha256 != (terrain
                ? Id65AuthoringTerrainMutationCompiler.ExpectedTwoTileBlocksSha256
                : UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.ExpectedOutputBlocksSha256))
            throw new InvalidDataException("The direct terrain semantic readback changed.");
        return result;
    }

    private static Id65AuthoringCompositeTextureReadback BuildTextureReadback(
        Id65V2NativeTextureWitness witness,
        byte[] finalModel,
        bool terrain)
    {
        int lowFaceOffset = terrain ? 0x2B510 : 0x2B50C;
        int highFaceOffset = terrain ? 0x2B548 : 0x2B538;
        int textureWordOffset = terrain ? 0x2B550 : 0x2B540;
        Id65AuthoringCompositeTextureReadback result = new(
            witness.CanonicalSha256,
            66,
            67,
            66,
            70,
            12,
            Hash(finalModel.AsSpan(0x428, 16)),
            Hash(finalModel.AsSpan(0x2F88, 168)),
            0x3030,
            Hash(finalModel.AsSpan(0, 0x3030)),
            witness.OutputTexturePagesSha256,
            "terrain.remote-pad.face.0",
            216,
            0,
            0,
            lowFaceOffset,
            highFaceOffset,
            textureWordOffset,
            Convert.ToHexString(finalModel.AsSpan(lowFaceOffset, 8)),
            Convert.ToHexString(finalModel.AsSpan(highFaceOffset, 16)),
            25,
            (int)(ReadUInt32(finalModel, textureWordOffset) & 0x7F),
            !terrain || (ReadUInt32(finalModel, 0x2B560) & 0x7F) == 25);
        if (result.PrivateLowRowSha256 != Id65V2NativeTextureCompositionCompiler.ExpectedPrivateLowRowSha256 ||
            result.PrivateHighRowSha256 != Id65V2NativeTextureCompositionCompiler.ExpectedPrivateHighRowSha256 ||
            result.OutputTextureComponentSha256 !=
                Id65V2NativeTextureCompositionCompiler.ExpectedOutputTextureComponentSha256 ||
            result.OutputTexturePagesSha256 !=
                Id65V2NativeTextureCompositionCompiler.ExpectedOutputTexturePagesSha256 ||
            result.OutputLowFaceHex != "0082100000821000" ||
            result.OutputHighFaceHex != "000001020101000242024001080A1000" ||
            result.OutputTextureId != 66 || !result.AddedTerrainFacePreservedT25)
            throw new InvalidDataException("The direct T66 semantic readback changed.");
        return result;
    }

    private static Id65AuthoringCompositeCapacityReadback BuildCapacity(
        bool terrain,
        Id65MobyPlacementIntent[] placements)
    {
        bool append = placements.Any(item => item.TargetTrueIndex == 107);
        return new(
            ModelLength,
            0x94508,
            terrain ? 0x94664 : 0x94634,
            terrain ? 0x19C : 0x1CC,
            66,
            67,
            66,
            217,
            terrain ? 4 : 3,
            terrain ? 2 : 1,
            terrain ? 4 : 3,
            terrain ? 2 : 1,
            terrain ? 19_809 : 19_808,
            terrain ? 0x17962 : 0x17960,
            0x17984,
            107,
            append ? 108 : 107,
            37,
            38,
            0x448,
            0x81,
            append ? 0x82 : 0x81,
            0x6C0);
    }

    private static DiffProof BuildDiffProof(
        byte[] before,
        byte[] after,
        IReadOnlyList<Id65AuthoringCompositeOwnedRange> ownedRanges)
    {
        if (before.Length != after.Length)
            throw new InvalidDataException("Composite diff inputs differ in length.");
        int changed = 0;
        List<Id65AuthoringCompositeDiffRange> ranges = [];
        StringBuilder manifest = new();
        int index = 0;
        int ownerCursor = 0;
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
            changed += length;
            while (ownerCursor < ownedRanges.Count &&
                   ownedRanges[ownerCursor].DataRelativeOffset +
                       (long)ownedRanges[ownerCursor].ByteLength <= start)
                ownerCursor++;
            if (ownerCursor >= ownedRanges.Count)
                throw new InvalidDataException(
                    $"Composite diff run data+0x{start:X}/0x{length:X} has no owner.");
            Id65AuthoringCompositeOwnedRange owner = ownedRanges[ownerCursor];
            long runEnd = start + (long)length;
            long ownerEnd = owner.DataRelativeOffset + (long)owner.ByteLength;
            bool nextOverlaps = ownerCursor + 1 < ownedRanges.Count &&
                ownedRanges[ownerCursor + 1].DataRelativeOffset < runEnd;
            if (start < owner.DataRelativeOffset || runEnd > ownerEnd || nextOverlaps)
                throw new InvalidDataException(
                    $"Composite diff run data+0x{start:X}/0x{length:X} is not contained by exactly one owner.");
            string beforeHash = Hash(before.AsSpan(start, length));
            string afterHash = Hash(after.AsSpan(start, length));
            ranges.Add(new(start, length, owner.StableId, owner.OwnerId, beforeHash, afterHash));
            manifest.Append(start.ToString("X8", CultureInfo.InvariantCulture)).Append(':')
                .Append(length.ToString("X8", CultureInfo.InvariantCulture)).Append(':')
                .Append(owner.StableId).Append(':').Append(owner.OwnerId).Append(':')
                .Append(beforeHash).Append(':').Append(afterHash).Append('\n');
        }
        int independentlyCovered = 0;
        foreach (Id65AuthoringCompositeOwnedRange owner in ownedRanges)
        {
            int start = owner.DataRelativeOffset;
            int end = start + owner.ByteLength;
            for (int offset = start; offset < end; offset++)
                if (before[offset] != after[offset]) independentlyCovered++;
        }
        if (independentlyCovered != changed || ranges.Sum(item => item.ByteLength) != changed)
            throw new InvalidDataException("Composite changed-byte ownership is incomplete or duplicated.");
        return new(
            changed,
            ranges.Count,
            Hash(Encoding.UTF8.GetBytes(manifest.ToString())),
            Array.AsReadOnly(ranges.ToArray()));
    }

    internal static void ValidateDiffOwnershipForSmoke(
        byte[] before,
        byte[] after,
        IReadOnlyList<Id65AuthoringCompositeOwnedRange> ownedRanges)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        ArgumentNullException.ThrowIfNull(ownedRanges);
        _ = BuildDiffProof(before, after, ownedRanges);
    }

    private static string HashOwnedRanges(IEnumerable<Id65AuthoringCompositeOwnedRange> ranges) =>
        Hash(Encoding.UTF8.GetBytes(string.Join('\n', ranges.Select(item =>
            $"{item.StableId}|{item.OwnerId}|{item.DataRelativeOffset:X8}|{item.ByteLength:X8}|{item.BeforeSha256}|{item.AfterSha256}"))));

    private static string HashRelocations(IEnumerable<Id65ModelComponentRelocation> relocations) =>
        Hash(Encoding.UTF8.GetBytes(string.Join('\n', relocations.Select(item =>
            $"{item.StableId}|{item.SourceRelativeOffset:X8}|{item.SourceByteLength:X8}|" +
            $"{item.OutputRelativeOffset:X8}|{item.OutputByteLength:X8}|{item.SourceSha256}|" +
            $"{item.OutputSha256}|{item.ContentsPreserved}"))));

    private static string HashRebases(IEnumerable<Id65StableHandleRebase> rebases) =>
        Hash(Encoding.UTF8.GetBytes(string.Join('\n', rebases.Select(item =>
            $"{item.StableId}|{item.Kind}|{Address(item.Source)}|{Address(item.Output)}"))));

    private static string HashMobyAssetRelocations(IEnumerable<Id65MobyAssetRelocation> relocations) =>
        Hash(Encoding.UTF8.GetBytes(string.Join('\n', relocations.Select(item =>
            $"{item.StableId}|{item.Kind}|{item.Source}|{item.Output}|{item.ByteLength}|" +
            $"{item.ContentSha256}|{item.Deduplicated}"))));

    private static string HashTransaction(IEnumerable<Id65AuthoringCompositeOwnedRange> ranges) =>
        Hash(Encoding.UTF8.GetBytes(string.Join('\n', ranges.Select(item =>
            $"{item.StableId}|{item.DataRelativeOffset:X8}|{item.ByteLength:X8}|" +
            $"{item.BeforeSha256}|{item.AfterSha256}"))));

    private static string Address(Id65LogicalAddress? address) => address.HasValue
        ? $"{address.Value.Space}:{address.Value.Primary}:{address.Value.Secondary}"
        : "null";

    private static void VerifyFrozenCombinedPins(Id65CompiledAuthoringComposite compiled)
    {
        if (compiled.ManifestSha256 != ExpectedCombinedManifestSha256 ||
            compiled.OutputRow80Sha256 != ExpectedCombinedOutputRow80Sha256 ||
            compiled.OutputModelSha256 != ExpectedCombinedModelSha256 ||
            compiled.ChangedByteCount != ExpectedCombinedChangedByteCount ||
            compiled.DiffRangeCount != ExpectedCombinedDiffRangeCount)
            throw new InvalidDataException("The concrete combined output witness changed.");
        RequireHash(compiled.DiffManifestSha256, ExpectedCombinedDiffManifestSha256,
            "combined diff manifest");
        RequireHash(compiled.OwnedRangeMapSha256, ExpectedCombinedOwnedRangeMapSha256,
            "combined owned-range map");
        RequireHash(compiled.RelocationMapSha256, ExpectedCombinedRelocationMapSha256,
            "combined relocation map");
        RequireHash(compiled.RebaseMapSha256, ExpectedCombinedRebaseMapSha256,
            "combined rebase map");
        RequireHash(compiled.MobyAssetRelocationMapSha256,
            ExpectedCombinedMobyAssetRelocationMapSha256, "combined Moby asset-relocation map");
        RequireHash(compiled.TransactionSha256, ExpectedCombinedTransactionSha256,
            "combined transaction");
        RequireHash(compiled.DeterministicPlanSha256, ExpectedCombinedDeterministicPlanSha256,
            "combined deterministic plan");
    }

    private static bool RangesOverlap(int leftOffset, int leftLength, int rightOffset, int rightLength) =>
        leftOffset < rightOffset + rightLength && rightOffset < leftOffset + leftLength;

    private static bool IsStrictlyDescending(IReadOnlyList<int> values)
    {
        for (int index = 1; index < values.Count; index++)
            if (values[index - 1] <= values[index])
                return false;
        return true;
    }

    private static byte[] Slice(byte[] source, int offset, int length)
    {
        if (offset < 0 || length < 0 || offset + (long)length > source.Length)
            throw new InvalidDataException("A direct source component slice is out of bounds.");
        return source.AsSpan(offset, length).ToArray();
    }

    private static byte[] UInt32(uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return bytes;
    }

    private static byte[] UInt16(ushort value)
    {
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return bytes;
    }

    private static int ReadInt32(ReadOnlySpan<byte> bytes, int offset)
    {
        if (offset < 0 || offset + 4 > bytes.Length)
            throw new InvalidDataException("A 32-bit read is out of range.");
        return BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset, 4));
    }

    private static uint ReadUInt32(ReadOnlySpan<byte> bytes, int offset)
    {
        if (offset < 0 || offset + 4 > bytes.Length)
            throw new InvalidDataException("A 32-bit unsigned read is out of range.");
        return BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset, 4));
    }

    private static ushort ReadUInt16(ReadOnlySpan<byte> bytes, int offset)
    {
        if (offset < 0 || offset + 2 > bytes.Length)
            throw new InvalidDataException("A 16-bit read is out of range.");
        return BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(offset, 2));
    }

    private static void WriteInt32(Span<byte> bytes, int offset, int value)
    {
        if (offset < 0 || offset + 4 > bytes.Length)
            throw new InvalidDataException("A 32-bit write is out of range.");
        BinaryPrimitives.WriteInt32LittleEndian(bytes.Slice(offset, 4), value);
    }

    private static void WriteUInt16(Span<byte> bytes, int offset, ushort value)
    {
        if (offset < 0 || offset + 2 > bytes.Length)
            throw new InvalidDataException("A 16-bit write is out of range.");
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.Slice(offset, 2), value);
    }

    internal static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static bool HashEquals(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static void RequireHash(string actual, string expected, string label)
    {
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
            throw new InvalidDataException($"{label} SHA-256 changed: expected {expected}, got {actual}.");
    }

    private sealed record CompositeCollisionLayout(
        int TreeOffset,
        int BlocksOffset,
        int TriangleOffset,
        int AssignmentsOffset,
        int FlagsOffset,
        int TreeCapacityBytes,
        int BlocksCapacityBytes);

    private sealed record CompositeCollisionBinding(
        Id65AuthoringCollisionCell Cell,
        int TreePointerByteOffset,
        int SourceBlockWordOffset,
        int[] OrderedTriangleIndexes);

    private sealed record CompositeCollisionIndex(
        byte[] TreeBytes,
        byte[] BlockBytes,
        IReadOnlyDictionary<Id65AuthoringCollisionCell, CompositeCollisionBinding> Cells,
        int GroupStartCount,
        int UsedBlockBytes);

    private sealed record DiffProof(
        int ChangedByteCount,
        int RangeCount,
        string ManifestSha256,
        IReadOnlyList<Id65AuthoringCompositeDiffRange> Ranges);
}
