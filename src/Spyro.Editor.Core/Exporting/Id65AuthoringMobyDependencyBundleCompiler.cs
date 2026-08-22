using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Spyro.Editor.Core.Exporting;

internal enum Id65MobyRowAllocationPolicy
{
    ReuseResidentExact,
    ReplaceExact,
    AppendContiguous
}

internal enum Id65MobyCoordinatePolicy
{
    PreserveDestination,
    AuthorExact,
    TranslateExact
}

internal enum Id65MobyAssetAllocationPolicy
{
    ReuseResidentExact,
    InstallDeduplicated
}

internal enum Id65MobyExternalDependencyPolicy
{
    PreserveExact,
    RequiresFutureCompositeCompiler
}

internal enum Id65MobyExternalDependencyKind
{
    TargetOverlay,
    GlobalExecutable,
    TexturePayload,
    SoundAndParticles,
    NativeLinks,
    RewardsTotalsAndPersistence,
    AssociatedDataRange
}

internal readonly record struct Id65MobyFieldSpan(int Offset, int ByteLength, string Role);

internal sealed record Id65MobyRowTemplate(
    string Id,
    string SourceLevelKey,
    int SourceTrueIndex,
    ushort ActorId,
    string ExactHex,
    string Sha256,
    string PropertiesAssetId,
    IReadOnlyList<Id65MobyFieldSpan> ScenePointerFields,
    IReadOnlyList<Id65MobyFieldSpan> MutablePlacementFields);

internal sealed record Id65MobyPropertiesAsset(
    string Id,
    string ExactHex,
    string Sha256,
    int Alignment,
    IReadOnlyList<int> InternalScenePointerFieldOffsets);

internal sealed record Id65MobyActorPackageAsset(
    string Id,
    ushort ActorId,
    int SourceRootIndex,
    string ExactHex,
    string Sha256,
    int Alignment,
    IReadOnlyList<int> EntryRelativePointerFieldOffsets);

internal sealed record Id65MobyActorRootRequirement(
    string Id,
    ushort ActorId,
    string PackageAssetId,
    Id65MobyAssetAllocationPolicy AllocationPolicy,
    int? ResidentRootIndex);

internal sealed record Id65MobySceneFixupPolicy(
    string Id,
    bool RegisterRowPropertiesFields,
    bool RegisterInternalPropertiesFields,
    bool PreserveExistingOrder,
    bool RejectDuplicateFields);

internal sealed record Id65MobyExternalDependencyIntent(
    string Id,
    Id65MobyExternalDependencyKind Kind,
    Id65LogicalAddress Address,
    int ByteLength,
    string PreimageSha256,
    Id65MobyExternalDependencyPolicy Policy);

internal sealed class Id65MobyDependencyBundleDescriptor
{
    internal Id65MobyDependencyBundleDescriptor(
        string id,
        int schemaVersion,
        string foundationContractSha256,
        IEnumerable<Id65MobyRowTemplate> rows,
        IEnumerable<Id65MobyPropertiesAsset> properties,
        IEnumerable<Id65MobyActorPackageAsset> actorPackages,
        IEnumerable<Id65MobyActorRootRequirement> actorRoots,
        IEnumerable<Id65MobySceneFixupPolicy> fixups,
        IEnumerable<Id65MobyExternalDependencyIntent> externalDependencies,
        UnusedLevel65MobyBehaviorClosureManifest behaviorClosure,
        IEnumerable<UnusedLevel65MobyDependencyRequirement> runtimeDependencies,
        string canonicalJson,
        string canonicalSha256)
    {
        Id = id;
        SchemaVersion = schemaVersion;
        FoundationContractSha256 = foundationContractSha256;
        Rows = Array.AsReadOnly(rows.ToArray());
        Properties = Array.AsReadOnly(properties.ToArray());
        ActorPackages = Array.AsReadOnly(actorPackages.ToArray());
        ActorRoots = Array.AsReadOnly(actorRoots.ToArray());
        Fixups = Array.AsReadOnly(fixups.ToArray());
        ExternalDependencies = Array.AsReadOnly(externalDependencies.ToArray());
        BehaviorClosure = behaviorClosure;
        RuntimeDependencies = Array.AsReadOnly(runtimeDependencies.ToArray());
        CanonicalJson = canonicalJson;
        CanonicalSha256 = canonicalSha256;
    }

    public string Id { get; }
    public int SchemaVersion { get; }
    public string FoundationContractSha256 { get; }
    public IReadOnlyList<Id65MobyRowTemplate> Rows { get; }
    public IReadOnlyList<Id65MobyPropertiesAsset> Properties { get; }
    public IReadOnlyList<Id65MobyActorPackageAsset> ActorPackages { get; }
    public IReadOnlyList<Id65MobyActorRootRequirement> ActorRoots { get; }
    public IReadOnlyList<Id65MobySceneFixupPolicy> Fixups { get; }
    public IReadOnlyList<Id65MobyExternalDependencyIntent> ExternalDependencies { get; }
    public UnusedLevel65MobyBehaviorClosureManifest BehaviorClosure { get; }
    public IReadOnlyList<UnusedLevel65MobyDependencyRequirement> RuntimeDependencies { get; }
    public string CanonicalJson { get; }
    public string CanonicalSha256 { get; }
}

internal sealed record Id65MobyPlacementIntent(
    string Id,
    string AtomicGroupId,
    string BundleId,
    string RowTemplateId,
    Id65MobyRowAllocationPolicy RowPolicy,
    int TargetTrueIndex,
    Id65MobyCoordinatePolicy CoordinatePolicy,
    Id65AuthoringPoint? CoordinatesOrDelta,
    byte? YawByte,
    string? SupportHandle);

internal sealed record Id65MobyBundleCompilerLimits(
    int MaximumObjectCount = 223,
    int MaximumActorRootCount = 64,
    int ActorSubfileEnd = 0x1D0000,
    int SceneSubfileEnd = 0x8800,
    int MinimumPropertiesSceneOffset = 0x8138);

internal sealed record Id65MobyBundleCompileRequest(
    Id65AuthoringManifest Manifest,
    UnusedLevel65RemoteBlankCollisionWindingRepairV2StaticPlan V2Template,
    IReadOnlyList<Id65MobyDependencyBundleDescriptor> Bundles,
    IReadOnlyList<Id65MobyPlacementIntent> Placements,
    Id65MobyBundleCompilerLimits? Limits = null);

internal sealed record Id65MobyOwnedDataRange(
    string StableId,
    string OwnerId,
    int DataRelativeOffset,
    int ByteLength,
    string BeforeHex,
    string AfterHex,
    string BeforeSha256,
    string AfterSha256);

internal sealed record Id65MobyAssetRelocation(
    string StableId,
    string Kind,
    Id65LogicalAddress Source,
    Id65LogicalAddress Output,
    int ByteLength,
    string ContentSha256,
    bool Deduplicated);

internal sealed record Id65MobyBundleCapacityReadback(
    int ObjectCountBefore,
    int ObjectCountAfter,
    int ActorRootCountBefore,
    int ActorRootCountAfter,
    int ActorTailBytesRemaining,
    int FixupCountBefore,
    int FixupCountAfter,
    int SceneTailBytesRemaining);

internal sealed record Id65MobyPlacementReadback(
    string PlacementId,
    int TargetTrueIndex,
    Id65MobyRowAllocationPolicy RowPolicy,
    int RawX,
    int RawY,
    int RawZ,
    ushort ActorId,
    int PropertiesSceneOffset,
    string RowSha256);

internal sealed class Id65CompiledMobyDependencyBundles
{
    private readonly byte[] _sourceData;
    private readonly byte[] _outputData;
    private readonly ReadOnlyCollection<Id65MobyOwnedDataRange> _ownedRanges;
    private readonly ReadOnlyCollection<Id65MobyAssetRelocation> _assetRelocations;
    private readonly ReadOnlyCollection<Id65MobyPlacementReadback> _placements;

    internal Id65CompiledMobyDependencyBundles(
        string scenarioId,
        Id65AuthoringManifest manifest,
        string bundleDescriptorSha256,
        byte[] sourceData,
        byte[] outputData,
        string sourceDataSha256,
        string v2OutputDataSha256,
        string outputDataSha256,
        int changedDataByteCount,
        int diffRangeCount,
        string diffManifestSha256,
        int mobyChangedByteCountOverV2,
        IEnumerable<Id65MobyOwnedDataRange> ownedRanges,
        string ownedRangeMapSha256,
        IEnumerable<Id65MobyAssetRelocation> assetRelocations,
        string assetRelocationMapSha256,
        IEnumerable<Id65MobyPlacementReadback> placements,
        Id65MobyBundleCapacityReadback capacity,
        string combinedTransactionSha256,
        string deterministicPlanSha256)
    {
        ScenarioId = scenarioId;
        Manifest = manifest;
        ManifestSha256 = manifest.CanonicalSha256;
        BundleDescriptorSha256 = bundleDescriptorSha256;
        _sourceData = sourceData.ToArray();
        _outputData = outputData.ToArray();
        SourceDataSha256 = sourceDataSha256;
        V2OutputDataSha256 = v2OutputDataSha256;
        OutputDataSha256 = outputDataSha256;
        ChangedDataByteCount = changedDataByteCount;
        DiffRangeCount = diffRangeCount;
        DiffManifestSha256 = diffManifestSha256;
        MobyChangedByteCountOverV2 = mobyChangedByteCountOverV2;
        _ownedRanges = Array.AsReadOnly(ownedRanges.ToArray());
        OwnedRangeMapSha256 = ownedRangeMapSha256;
        _assetRelocations = Array.AsReadOnly(assetRelocations.ToArray());
        AssetRelocationMapSha256 = assetRelocationMapSha256;
        _placements = Array.AsReadOnly(placements.ToArray());
        Capacity = capacity;
        CombinedTransactionSha256 = combinedTransactionSha256;
        DeterministicPlanSha256 = deterministicPlanSha256;
    }

    public string ProfileId => Id65AuthoringMobyDependencyBundleCompiler.ProfileId;
    public string ScenarioId { get; }
    public Id65AuthoringManifest Manifest { get; }
    public string ManifestSha256 { get; }
    public string BundleDescriptorSha256 { get; }
    public string SourceDataSha256 { get; }
    public string V2OutputDataSha256 { get; }
    public string OutputDataSha256 { get; }
    public int ChangedDataByteCount { get; }
    public int DiffRangeCount { get; }
    public string DiffManifestSha256 { get; }
    public int MobyChangedByteCountOverV2 { get; }
    public IReadOnlyList<Id65MobyOwnedDataRange> OwnedRanges => _ownedRanges;
    public string OwnedRangeMapSha256 { get; }
    public IReadOnlyList<Id65MobyAssetRelocation> AssetRelocations => _assetRelocations;
    public string AssetRelocationMapSha256 { get; }
    public IReadOnlyList<Id65MobyPlacementReadback> Placements => _placements;
    public Id65MobyBundleCapacityReadback Capacity { get; }
    public string CombinedTransactionSha256 { get; }
    public string DeterministicPlanSha256 { get; }

    public bool BuildsFromLockedSourceInOneTransaction => true;
    public bool AcceptsPriorSliceAfterimage => false;
    public bool CrossSliceCompositionSupported => false;
    public bool DescriptorCanFeedFutureCompositeCompiler => true;
    public bool WritesFileSystem => false;
    public bool WritesDiscImage => false;
    public bool WritesCue => false;
    public bool RetiredPublisherCalled => false;
    public bool AppIntegrated => false;
    public bool NormalCreateBinEnabled => false;
    public bool ReleaseIntegrated => false;
    public bool RuntimeCandidateAuthorized => false;
    public bool PromotionAuthorized => false;
    public bool Publishable => false;
    public bool V2StructuralPatchesComposedAtomically => true;
    public bool ExactInverseVerified => true;
    public bool DeterministicReadbackVerified => true;
    public bool RootPackageAndPropertiesDeduplicated => true;
    public bool ReplacedRowPrivateDataPreserved => true;
    public bool GlobalExecutableExcluded => true;

    internal byte[] CopySourceData() => _sourceData.ToArray();
    internal byte[] CopyOutputData() => _outputData.ToArray();
}

internal static class Id65AuthoringMobyDependencyBundleCompiler
{
    public const string ProfileId =
        "id65-authoring-moby-dependency-bundle-v2-backed-static-in-memory-v1";

    public const string ArtisansGrassBundleId = "bundle.artisans-grass.actor-01f5";
    public const string ArtisansGrassRowTemplateId = "row.artisans-grass.t121";
    public const string ArtisansGrassPropertiesAssetId = "props.artisans-grass.shared-v1";
    public const string ArtisansGrassPackageAssetId = "package.artisans-grass.actor-01f5";
    public const string AppendScenarioId = "scenario.artisans-grass.t107";
    public const string ZeroEggScenarioId = "scenario.zero-egg.t88";
    public const string CombinedScenarioId = "scenario.artisans-grass.t107-plus-zero-egg.t88";
    public const string AppendManifestProfileId = "id65-authoring-manifest-v2-plus-artisans-grass-t107-v1";
    public const string ZeroEggManifestProfileId = "id65-authoring-manifest-v2-plus-zero-egg-t88-v1";
    public const string CombinedManifestProfileId = "id65-authoring-manifest-v2-plus-grass-t107-zero-egg-t88-v1";

    public const string T107PlacementId = "placement.artisans-grass.t107";
    public const string T88PlacementId = "placement.zero-egg.t88";
    public const string AtomicPlacementGroupId = "atomic.id65-passive-grass-authoring";

    public const string ExpectedArtisansGrassBundleSha256 =
        "22e43e5a192b4486cf0d30f6d42bcbc92249a19b8998e02dc13e98b3c672ca91";
    public const string ExpectedAppendManifestSha256 =
        "e06735a29e031735ad1d3abaec46e6961f461ddb1ffd83e4bc4535f370270940";
    public const string ExpectedZeroEggManifestSha256 =
        "69a5f4b8b15a36feff16003917800b34460ee36273f82f0b7613b43cc9b21343";
    public const string ExpectedCombinedManifestSha256 =
        "12c40e52dc0542c3e515df5c75a5d39b376253ba8c43ba647e98d3bf4da38310";
    public const string ExpectedAppendOutputDataSha256 =
        "fcec571bba9178c2e48bc161493d7b7bd160b70882e22b44fbad6a7ab8f05c8d";
    public const string ExpectedZeroEggOutputDataSha256 =
        "8955ffdeab54d04e4a58e76ee68bd27fb4363d930087411e298bc0966104c5b2";
    public const string ExpectedCombinedOutputDataSha256 =
        "ecece3083bf720b9e5ee2e4aa18051cffa688cb327b2c8eb337007cb7fcfa875";
    public const int ExpectedAppendChangedDataByteCount = 484_629;
    public const int ExpectedAppendDiffRangeCount = 55_450;
    public const int ExpectedZeroEggChangedDataByteCount = 484_612;
    public const int ExpectedZeroEggDiffRangeCount = 55_441;
    public const int ExpectedCombinedChangedDataByteCount = 484_638;
    public const int ExpectedCombinedDiffRangeCount = 55_456;
    public const int ExpectedCombinedMobyChangedBytesOverV2 = 302;
    public const string ExpectedAppendDiffManifestSha256 =
        "0467a0ac778b484f441094e9f9d5325cc077399ce58c61b4ff257becbb945947";
    public const string ExpectedZeroEggDiffManifestSha256 =
        "3cdeb506b71b02ac434716831c4221ae0694e8ee6788acc4bec143bb564f5f68";
    public const string ExpectedCombinedDiffManifestSha256 =
        "85d876ef66dc16af8a46a83463abf94ceb53ebe84af45fcb53ad770162b49b07";
    public const string ExpectedAppendOwnedRangeMapSha256 =
        "3e06b5fde25166cb7488caed0ad7ccb611ca0a4754c45bdaf154bba96cc2a64e";
    public const string ExpectedZeroEggOwnedRangeMapSha256 =
        "fc66561e959169f28df26b59fe24f42433d3a807898a53282186dec5c6243242";
    public const string ExpectedCombinedOwnedRangeMapSha256 =
        "a6b54f8dc15fae3450af3c8e4ee8871bdab51da0a42f345ecbdcd399e1abd14f";
    public const string ExpectedAppendTransactionSha256 =
        "a552e705b67d33cd25654d2965c9446588f6edc21221e681219e919f91ef65fa";
    public const string ExpectedZeroEggTransactionSha256 =
        "4bf379c7b6ed26ae9e83644ea5c37473e0397591b4e90422728e25008b0e711e";
    public const string ExpectedCombinedTransactionSha256 =
        "f52effdef57ccf6fd1cb83a1709691ef24743578403bfd86b14ef9d9b402ad46";
    public const string ExpectedAppendRelocationMapSha256 =
        "ceefb6f18d939a10c2cc95ba82ba65c8782002a9fc371e55e9efc108aab8eff2";
    public const string ExpectedZeroEggRelocationMapSha256 =
        "74a5bd4390b4a8e5e47a3ec38f58cacb46a82e9cdf7989fc242ca80aec60e662";
    public const string ExpectedCombinedRelocationMapSha256 =
        "89d23fb4e8109584e827a2356911ca8d6e73af8f59cda033c2966eee50791d93";
    public const string ExpectedAppendDeterministicPlanSha256 =
        "4104df081b35e1c1d5984ae05601af885b4bab22c23b0fd6bc1a65268713784e";
    public const string ExpectedZeroEggDeterministicPlanSha256 =
        "efd639f72bde7b188a58c8741e406f53f8dae913687d231e01743fdfe5596410";
    public const string ExpectedCombinedDeterministicPlanSha256 =
        "74bc981eafa5fb5ebd17db8c88a99fbca14d0fb50bde0bc517d4ce2f36151a07";

    private const int DataByteLength = 0x2E2000;
    private const int RecordByteLength = 0x58;
    private const int SceneOffset = 0x1D0000;
    private const int SceneByteLength = 0x8800;
    private const int ObjectCountOffset = SceneOffset + 0x16C;
    private const int ObjectTableOffset = SceneOffset + 0x170;
    private const int T88RowOffset = ObjectTableOffset + (88 * RecordByteLength);
    private const int T107RowOffset = ObjectTableOffset + (107 * RecordByteLength);
    private const int FixupCountOffset = SceneOffset + 0x7F28;
    private const int FixupListOffset = FixupCountOffset + 4;
    private const int FixupAppendOffset = SceneOffset + 0x8130;
    private const int PropertiesOffset = SceneOffset + 0x8138;
    private const int Root37Offset = 0xE4;
    private const int ActorId37Offset = 0x19A;
    private const int PackageOffset = 0x1CFA44;
    private const int PackageByteLength = 0x174;
    private const int PrivateThiefBlockOffset = SceneOffset + 0x5AFC;
    private const int PrivateThiefBlockByteLength = 0x134;
    private const int ObjectCountBefore = 107;
    private const int FixupCountBefore = 0x81;
    private const int ExistingActorRootCount = 37;
    private const int ActorSubfileEnd = 0x1D0000;
    private const ushort GrassActorId = 0x01F5;
    private const uint GrassPackagePointer = 0x1CFA44;
    private const uint GrassPropertiesPointer = 0x8138;
    private const string GrassRowSha256 =
        "de8450049c0bea92fba8fe4e7f9f9cb749a514d1318dbd83dc9b6b1b18a0b190";
    private const string GrassPropertiesSha256 =
        "9eeeff662fd5b77dbc35de8ed01e0d1fd149cee49126625b69f65553c4b7c20b";
    private const string GrassPackageSha256 =
        "90ca71a190c4567817d728753f25df667d56515e1721d24e19e6da9dc07edcc3";
    private const string SourceT88RowSha256 =
        "dccfa0a9489f9d76c93958a1a43e64ea948262b51634d03ddc311cdf3ba1a09a";
    private const string OutputT88RowSha256 =
        "4228cf9bf97a8309f26b9f1303174e116916e2f9633388f2346a654e97cdb56f";
    private const string OutputT107RowSha256 =
        "d42e49248278759d2c2020bba4d4c4db18817bf120d73829a2bbba7dddcef9fb";
    private const string PrivateThiefBlockSha256 =
        "6213aceb752cb139ac669b392a3faf7441888306b363fb4cf3e2c63f45715136";
    private const int T107RawX = 124_384;
    private const int T107RawY = 102_304;
    private const int T107RawZ = 8_192;
    private const int T88RawX = 96_850;
    private const int T88RawY = 141_732;
    private const int T88RawZ = 12_248;
    private const byte GrassYaw = 0x04;

    public static Id65MobyDependencyBundleDescriptor CreateArtisansGrassBundle(
        UnusedLevel65MobyDependencyBundleContract contract,
        ReadOnlySpan<byte> exactActorPackage)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ValidateFoundationContract(contract, exactActorPackage);
        Id65MobyRowTemplate row = new(
            ArtisansGrassRowTemplateId,
            "artisans",
            121,
            GrassActorId,
            contract.SourceRecord.Hex,
            contract.SourceRecord.Sha256,
            ArtisansGrassPropertiesAssetId,
            Array.AsReadOnly(new[] { new Id65MobyFieldSpan(0, 4, "scene-relative-properties") }),
            Array.AsReadOnly(new[]
            {
                new Id65MobyFieldSpan(0x0C, 12, "raw-xyz"),
                new Id65MobyFieldSpan(0x47, 1, "yaw")
            }));
        Id65MobyPropertiesAsset properties = new(
            ArtisansGrassPropertiesAssetId,
            contract.Properties.PropertiesHex,
            contract.Properties.PropertiesSha256,
            4,
            Array.Empty<int>());
        Id65MobyActorPackageAsset package = new(
            ArtisansGrassPackageAssetId,
            GrassActorId,
            22,
            Convert.ToHexString(exactActorPackage),
            Hash(exactActorPackage),
            4,
            Array.Empty<int>());
        Id65MobyActorRootRequirement root = new(
            "root.artisans-grass.actor-01f5",
            GrassActorId,
            package.Id,
            Id65MobyAssetAllocationPolicy.InstallDeduplicated,
            null);
        Id65MobySceneFixupPolicy fixup = new(
            "fixup.artisans-grass.row-properties",
            RegisterRowPropertiesFields: true,
            RegisterInternalPropertiesFields: false,
            PreserveExistingOrder: true,
            RejectDuplicateFields: true);
        Id65MobyExternalDependencyIntent[] external =
        [
            new(
                "target-overlay.default-no-update-dispatch",
                Id65MobyExternalDependencyKind.TargetOverlay,
                new Id65LogicalAddress("wad-entry", 79, 0),
                0xF800,
                contract.BehaviorClosure.TargetDispatch.OverlaySha256,
                Id65MobyExternalDependencyPolicy.PreserveExact),
            new(
                "global-executable.no-01f5-handler",
                Id65MobyExternalDependencyKind.GlobalExecutable,
                new Id65LogicalAddress("disc-lba", 55_382, 0),
                0x66000,
                contract.BehaviorClosure.GlobalExecutable.Sha256,
                Id65MobyExternalDependencyPolicy.PreserveExact)
        ];
        string canonicalJson = BuildBundleCanonicalJson(
            ArtisansGrassBundleId,
            schemaVersion: 1,
            contract.DeterministicContractSha256,
            [row],
            [properties],
            [package],
            [root],
            [fixup],
            external,
            contract.BehaviorClosure,
            contract.Dependencies);
        string canonicalSha256 = Hash(Encoding.UTF8.GetBytes(canonicalJson));
        RequireHash(canonicalSha256, ExpectedArtisansGrassBundleSha256, "Artisans Grass descriptor");
        return new(
            ArtisansGrassBundleId,
            1,
            contract.DeterministicContractSha256,
            [row],
            [properties],
            [package],
            [root],
            [fixup],
            external,
            contract.BehaviorClosure,
            contract.Dependencies,
            canonicalJson,
            canonicalSha256);
    }

    public static Id65MobyPlacementIntent CreateT107Placement() => new(
        T107PlacementId,
        AtomicPlacementGroupId,
        ArtisansGrassBundleId,
        ArtisansGrassRowTemplateId,
        Id65MobyRowAllocationPolicy.AppendContiguous,
        107,
        Id65MobyCoordinatePolicy.AuthorExact,
        new Id65AuthoringPoint(T107RawX, T107RawY, T107RawZ),
        GrassYaw,
        "support.native-apron.t1353");

    public static Id65MobyPlacementIntent CreateZeroEggT88Placement() => new(
        T88PlacementId,
        AtomicPlacementGroupId,
        ArtisansGrassBundleId,
        ArtisansGrassRowTemplateId,
        Id65MobyRowAllocationPolicy.ReplaceExact,
        88,
        Id65MobyCoordinatePolicy.PreserveDestination,
        null,
        null,
        null);

    public static Id65CompiledMobyDependencyBundles Compile(Id65MobyBundleCompileRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Manifest);
        ArgumentNullException.ThrowIfNull(request.V2Template);
        ArgumentNullException.ThrowIfNull(request.Bundles);
        ArgumentNullException.ThrowIfNull(request.Placements);
        Id65MobyBundleCompilerLimits limits = request.Limits ?? new();

        Id65AuthoringManifest minimal = Id65AuthoringModelCompiler.CreateMinimalV2EquivalentManifest();
        _ = Id65AuthoringModelCompiler.Compile(minimal, request.V2Template);
        ValidateManifestEnvelope(request.Manifest, minimal);

        foreach (Id65MobyDependencyBundleDescriptor candidate in request.Bundles)
            ValidateExternalDependencies(candidate);
        Id65MobyDependencyBundleDescriptor bundle = ValidateBundles(request.Bundles);
        Id65MobyPlacementIntent[] placements = ValidatePlacements(
            request.Manifest,
            bundle,
            request.Placements,
            out string scenarioId);
        ValidateLimits(limits, placements);

        byte[] source = request.V2Template.SourceData.ToArray();
        byte[] v2Output = request.V2Template.OutputData.ToArray();
        if (source.Length != DataByteLength || v2Output.Length != DataByteLength)
            throw new InvalidDataException("The locked ID65 row-80 data length changed.");
        RequireHash(Hash(source), UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.ExpectedSourceDataSha256, "locked row-80 data");
        RequireHash(Hash(v2Output), UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.ExpectedOutputDataSha256, "winding-v2 row-80 data");

        List<Id65MobyOwnedDataRange> owned = BuildOwnedRanges(source, bundle, placements);
        ValidateOwnedRangesAgainstV2(source, v2Output, owned, request.V2Template.StructuralPatches);
        TransactionPatch[] combinedPatches =
        [
            .. request.V2Template.StructuralPatches.Select(patch => new TransactionPatch(
                $"v2:{patch.Kind}",
                patch.DataRelativeOffset,
                patch.Before.ToArray(),
                patch.After.ToArray())),
            .. owned.Select(range => new TransactionPatch(
                $"moby:{range.StableId}",
                range.DataRelativeOffset,
                Convert.FromHexString(range.BeforeHex),
                Convert.FromHexString(range.AfterHex)))
        ];
        ValidateTransactionPatches(source, combinedPatches);
        byte[] output = ApplyPatches(source, combinedPatches, reverse: false);
        byte[] inverse = ApplyPatches(output, combinedPatches, reverse: true);
        if (!inverse.SequenceEqual(source))
            throw new InvalidDataException("The combined v2/Moby transaction did not invert to the locked source.");
        VerifyOnlyOwnedDifferencesFromV2(v2Output, output, owned);

        Id65MobyPlacementReadback[] placementReadback = BuildPlacementReadback(output, placements);
        VerifySemanticReadback(source, output, placements, placementReadback);
        Id65MobyAssetRelocation[] relocations = BuildRelocations(bundle, placements);
        string ownedMapHash = HashOwnedRanges(owned);
        string relocationMapHash = HashRelocations(relocations);
        DiffProof diff = BuildDiffProof(source, output);
        int mobyChangedOverV2 = CountChangedBytes(v2Output, output);
        Id65MobyBundleCapacityReadback capacity = new(
            ObjectCountBefore,
            placements.Any(item => item.RowPolicy == Id65MobyRowAllocationPolicy.AppendContiguous) ? 108 : 107,
            ExistingActorRootCount,
            ExistingActorRootCount + 1,
            ActorSubfileEnd - (PackageOffset + PackageByteLength),
            FixupCountBefore,
            FixupCountBefore + placements.Count(item => item.RowPolicy == Id65MobyRowAllocationPolicy.AppendContiguous),
            SceneOffset + SceneByteLength - (PropertiesOffset + 8));
        string transactionHash = HashTransaction(combinedPatches);
        string deterministicHash = Hash(Encoding.UTF8.GetBytes(string.Join('\n',
        [
            ProfileId,
            scenarioId,
            request.Manifest.CanonicalSha256,
            bundle.CanonicalSha256,
            Hash(source),
            Hash(v2Output),
            Hash(output),
            diff.ManifestSha256,
            ownedMapHash,
            relocationMapHash,
            transactionHash,
            mobyChangedOverV2.ToString(CultureInfo.InvariantCulture)
        ])));

        VerifyFrozenScenarioPins(
            scenarioId,
            request.Manifest.CanonicalSha256,
            output,
            diff,
            mobyChangedOverV2,
            ownedMapHash,
            transactionHash,
            relocationMapHash,
            deterministicHash);
        Id65CompiledMobyDependencyBundles compiled = new(
            scenarioId,
            request.Manifest,
            bundle.CanonicalSha256,
            source,
            output,
            Hash(source),
            Hash(v2Output),
            Hash(output),
            diff.ChangedByteCount,
            diff.RangeCount,
            diff.ManifestSha256,
            mobyChangedOverV2,
            owned,
            ownedMapHash,
            relocations,
            relocationMapHash,
            placementReadback,
            capacity,
            transactionHash,
            deterministicHash);
        byte[] applied = ApplyTransactional(compiled, source, reverse: false);
        byte[] reversed = ApplyTransactional(compiled, applied, reverse: true);
        if (!applied.SequenceEqual(output) || !reversed.SequenceEqual(source))
            throw new InvalidDataException("Compiled Moby readback failed its exact transaction round trip.");
        return compiled;
    }

    internal static byte[] ApplyTransactional(
        Id65CompiledMobyDependencyBundles compiled,
        byte[] input,
        bool reverse)
    {
        ArgumentNullException.ThrowIfNull(compiled);
        ArgumentNullException.ThrowIfNull(input);
        byte[] expected = reverse ? compiled.CopyOutputData() : compiled.CopySourceData();
        if (!input.SequenceEqual(expected))
            throw new InvalidDataException(
                $"The combined ID65 Moby transaction {(reverse ? "afterimage" : "locked-source preimage")} changed.");
        return reverse ? compiled.CopySourceData() : compiled.CopyOutputData();
    }

    private static Id65MobyDependencyBundleDescriptor ValidateBundles(
        IReadOnlyList<Id65MobyDependencyBundleDescriptor> bundles)
    {
        if (bundles.Count != 1)
            throw new InvalidDataException("The bounded compiler accepts exactly one deduplicated Grass bundle descriptor.");
        Id65MobyDependencyBundleDescriptor bundle = bundles[0];
        if (bundle.Id != ArtisansGrassBundleId || bundle.SchemaVersion != 1 ||
            bundle.FoundationContractSha256 != UnusedLevel65MobyDependencyBundleFoundation.ExpectedContractSha256 ||
            bundle.Rows.Count != 1 || bundle.Properties.Count != 1 ||
            bundle.ActorPackages.Count != 1 || bundle.ActorRoots.Count != 1 || bundle.Fixups.Count != 1 ||
            bundle.Rows[0].Id != ArtisansGrassRowTemplateId || bundle.Rows[0].SourceLevelKey != "artisans" ||
            bundle.Rows[0].SourceTrueIndex != 121 || bundle.Rows[0].Sha256 != GrassRowSha256 ||
            bundle.Rows[0].ActorId != GrassActorId || bundle.Rows[0].PropertiesAssetId != ArtisansGrassPropertiesAssetId ||
            !bundle.Rows[0].ScenePointerFields.SequenceEqual(
                new[] { new Id65MobyFieldSpan(0, 4, "scene-relative-properties") }) ||
            !bundle.Rows[0].MutablePlacementFields.SequenceEqual(
                new[]
                {
                    new Id65MobyFieldSpan(0x0C, 12, "raw-xyz"),
                    new Id65MobyFieldSpan(0x47, 1, "yaw")
                }) ||
            bundle.Properties[0].Id != ArtisansGrassPropertiesAssetId ||
            bundle.Properties[0].Sha256 != GrassPropertiesSha256 || bundle.Properties[0].Alignment != 4 ||
            bundle.Properties[0].ExactHex != "040000008A000000" ||
            bundle.Properties[0].InternalScenePointerFieldOffsets.Count != 0 ||
            bundle.ActorPackages[0].Id != ArtisansGrassPackageAssetId ||
            bundle.ActorPackages[0].Sha256 != GrassPackageSha256 ||
            bundle.ActorPackages[0].ActorId != GrassActorId || bundle.ActorPackages[0].SourceRootIndex != 22 ||
            bundle.ActorPackages[0].Alignment != 4 || bundle.ActorPackages[0].EntryRelativePointerFieldOffsets.Count != 0 ||
            bundle.ActorRoots[0].Id != "root.artisans-grass.actor-01f5" ||
            bundle.ActorRoots[0].ActorId != GrassActorId ||
            bundle.ActorRoots[0].PackageAssetId != ArtisansGrassPackageAssetId ||
            bundle.ActorRoots[0].AllocationPolicy != Id65MobyAssetAllocationPolicy.InstallDeduplicated ||
            bundle.ActorRoots[0].ResidentRootIndex.HasValue ||
            bundle.Fixups[0] != new Id65MobySceneFixupPolicy(
                "fixup.artisans-grass.row-properties",
                RegisterRowPropertiesFields: true,
                RegisterInternalPropertiesFields: false,
                PreserveExistingOrder: true,
                RejectDuplicateFields: true) ||
            Hash(Convert.FromHexString(bundle.Rows[0].ExactHex)) != GrassRowSha256 ||
            Hash(Convert.FromHexString(bundle.Properties[0].ExactHex)) != GrassPropertiesSha256 ||
            Hash(Convert.FromHexString(bundle.ActorPackages[0].ExactHex)) != GrassPackageSha256 ||
            !bundle.BehaviorClosure.StaticRuntimeDependencyClosureComplete ||
            bundle.BehaviorClosure.PropertiesContainPointers ||
            bundle.BehaviorClosure.PropertiesControllerSemanticsRequired ||
            bundle.BehaviorClosure.SoundDependencyRequired ||
            bundle.BehaviorClosure.ParticleOrEffectDependencyRequired ||
            bundle.BehaviorClosure.DynamicSpawnDependencyRequired ||
            bundle.BehaviorClosure.DynamicNativeLinkDependencyRequired)
        {
            throw new InvalidDataException("The exact passive Artisans Grass dependency descriptor changed.");
        }
        Id65MobyExternalDependencyIntent[] expectedExternal =
        [
            new(
                "target-overlay.default-no-update-dispatch",
                Id65MobyExternalDependencyKind.TargetOverlay,
                new Id65LogicalAddress("wad-entry", 79, 0),
                0xF800,
                "9bc923cc8d27703537b81b01f51fde351e58aabee87d38a8afc630f1031878a5",
                Id65MobyExternalDependencyPolicy.PreserveExact),
            new(
                "global-executable.no-01f5-handler",
                Id65MobyExternalDependencyKind.GlobalExecutable,
                new Id65LogicalAddress("disc-lba", 55_382, 0),
                0x66000,
                "fa5fc7981188b78fa7d7b78facca64c1f79dadb107515e9146ad178ade39d442",
                Id65MobyExternalDependencyPolicy.PreserveExact)
        ];
        string[] expectedDependencyIds =
        [
            "source-row",
            "scene-properties",
            "scene-pointer-fixups",
            "actor-model-package",
            "normal-and-far-lod-animation",
            "texture-pixels-and-cluts",
            "destination-object-row",
            "destination-actor-root",
            "destination-scene-storage",
            "reward-totals-persistence",
            "overlay-dispatch-controller",
            "controller-property-semantics",
            "sound-particle-spawn-closure",
            "native-dynamic-link-closure",
            "runtime-acceptance"
        ];
        if (!bundle.ExternalDependencies.SequenceEqual(expectedExternal) ||
            !bundle.RuntimeDependencies.Select(item => item.Id).SequenceEqual(expectedDependencyIds) ||
            bundle.RuntimeDependencies.Count(item =>
                item.EvidenceState == UnusedLevel65MobyDependencyEvidenceState.UnresolvedHardBlocker) != 1 ||
            bundle.RuntimeDependencies.Single(item =>
                item.EvidenceState == UnusedLevel65MobyDependencyEvidenceState.UnresolvedHardBlocker).Id != "runtime-acceptance")
        {
            throw new InvalidDataException("The exact external/runtime dependency evidence changed.");
        }
        string recomputedJson = BuildBundleCanonicalJson(
            bundle.Id,
            bundle.SchemaVersion,
            bundle.FoundationContractSha256,
            bundle.Rows,
            bundle.Properties,
            bundle.ActorPackages,
            bundle.ActorRoots,
            bundle.Fixups,
            bundle.ExternalDependencies,
            bundle.BehaviorClosure,
            bundle.RuntimeDependencies);
        string recomputedSha256 = Hash(Encoding.UTF8.GetBytes(recomputedJson));
        if (bundle.CanonicalJson != recomputedJson || bundle.CanonicalSha256 != recomputedSha256 ||
            bundle.CanonicalSha256 != ExpectedArtisansGrassBundleSha256)
        {
            throw new InvalidDataException("The full-field Artisans Grass descriptor canonical identity changed.");
        }
        return bundle;
    }

    private static void ValidateExternalDependencies(Id65MobyDependencyBundleDescriptor bundle)
    {
        Id65MobyExternalDependencyIntent? unresolved = bundle.ExternalDependencies.FirstOrDefault(item =>
            item.Policy == Id65MobyExternalDependencyPolicy.RequiresFutureCompositeCompiler);
        if (unresolved is not null)
        {
            string owner = unresolved.Kind == Id65MobyExternalDependencyKind.GlobalExecutable
                ? "SCUS/global executable"
                : unresolved.Kind.ToString();
            throw new InvalidDataException(
                $"Bundle `{bundle.Id}` requires {owner} ownership and must wait for the future composite compiler.");
        }
        if (bundle.ExternalDependencies.Any(item => item.Policy != Id65MobyExternalDependencyPolicy.PreserveExact))
            throw new InvalidDataException("An external Moby dependency is not preserve-exact.");
    }

    private static Id65MobyPlacementIntent[] ValidatePlacements(
        Id65AuthoringManifest manifest,
        Id65MobyDependencyBundleDescriptor bundle,
        IReadOnlyList<Id65MobyPlacementIntent> supplied,
        out string scenarioId)
    {
        if (supplied.Count is < 1 or > 2)
            throw new InvalidDataException("The bounded compiler accepts T107, T88, or their exact two-placement composition.");
        Id65MobyPlacementIntent[] placements = supplied.OrderBy(item => item.TargetTrueIndex).ToArray();
        if (placements.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() != placements.Length ||
            placements.Select(item => item.TargetTrueIndex).Distinct().Count() != placements.Length ||
            placements.Any(item => item.BundleId != bundle.Id || item.RowTemplateId != ArtisansGrassRowTemplateId))
        {
            throw new InvalidDataException("Moby placement identities, targets, or bundle references conflict.");
        }
        bool hasT88 = placements.Any(item => item.TargetTrueIndex == 88);
        bool hasT107 = placements.Any(item => item.TargetTrueIndex == 107);
        if (placements.Any(item => item.TargetTrueIndex is not (88 or 107)) || (!hasT88 && !hasT107))
            throw new InvalidDataException("Only exact T88 replacement and contiguous T107 append are supported.");
        Id65MobyPlacementIntent? t88 = placements.SingleOrDefault(item => item.TargetTrueIndex == 88);
        if (t88 is not null &&
            (t88.Id != T88PlacementId || t88.AtomicGroupId != AtomicPlacementGroupId ||
             t88.RowPolicy != Id65MobyRowAllocationPolicy.ReplaceExact ||
             t88.CoordinatePolicy != Id65MobyCoordinatePolicy.PreserveDestination ||
             t88.CoordinatesOrDelta.HasValue || t88.YawByte.HasValue || t88.SupportHandle is not null))
        {
            throw new InvalidDataException("ZeroEgg T88 must preserve destination XYZ and use only the donor Grass yaw.");
        }
        Id65MobyPlacementIntent? t107 = placements.SingleOrDefault(item => item.TargetTrueIndex == 107);
        if (t107 is not null &&
            (t107.Id != T107PlacementId || t107.AtomicGroupId != AtomicPlacementGroupId ||
             t107.RowPolicy != Id65MobyRowAllocationPolicy.AppendContiguous ||
             t107.CoordinatePolicy != Id65MobyCoordinatePolicy.AuthorExact ||
             t107.CoordinatesOrDelta != new Id65AuthoringPoint(T107RawX, T107RawY, T107RawZ) ||
             t107.YawByte != GrassYaw || t107.SupportHandle != "support.native-apron.t1353"))
        {
            throw new InvalidDataException("Grass T107 lost its exact runtime-proven native-apron placement.");
        }
        if (placements.Any(item => item.AtomicGroupId != AtomicPlacementGroupId))
            throw new InvalidDataException("All placements must share the exact atomic Moby authoring group.");
        scenarioId = (hasT88, hasT107) switch
        {
            (false, true) => AppendScenarioId,
            (true, false) => ZeroEggScenarioId,
            (true, true) => CombinedScenarioId,
            _ => throw new InvalidDataException("Unsupported Moby scenario.")
        };
        string expectedProfile = scenarioId switch
        {
            AppendScenarioId => AppendManifestProfileId,
            ZeroEggScenarioId => ZeroEggManifestProfileId,
            _ => CombinedManifestProfileId
        };
        if (manifest.ProfileId != expectedProfile)
            throw new InvalidDataException("The scenario-specific Moby manifest profile changed.");
        Id65MobyIntent[] expectedAuthored = placements.Select(item => item.TargetTrueIndex == 88
            ? new Id65MobyIntent(
                "moby.zero-egg.grass.t88",
                Id65AuthoringIntentDisposition.AuthorExact,
                88,
                T88RawX,
                T88RawY,
                T88RawZ,
                bundle.Id)
            : new Id65MobyIntent(
                "moby.artisans-grass.t107",
                Id65AuthoringIntentDisposition.AuthorExact,
                107,
                T107RawX,
                T107RawY,
                T107RawZ,
                bundle.Id)).ToArray();
        Id65MobyIntent[] actualAuthored = manifest.Mobys
            .Where(item => item.Id != "moby.player-anchor.t92")
            .OrderBy(item => item.TrueIndex)
            .ToArray();
        if (!actualAuthored.SequenceEqual(expectedAuthored.OrderBy(item => item.TrueIndex)))
            throw new InvalidDataException("Manifest Moby intents do not exactly match their placement transaction.");
        return placements;
    }

    private static void ValidateManifestEnvelope(Id65AuthoringManifest manifest, Id65AuthoringManifest minimal)
    {
        if (manifest.LockedBase != minimal.LockedBase ||
            !manifest.Sectors.SequenceEqual(minimal.Sectors) ||
            !manifest.Vertices.SequenceEqual(minimal.Vertices) ||
            !manifest.RenderFaces.SequenceEqual(minimal.RenderFaces) ||
            !manifest.Collision.SequenceEqual(minimal.Collision) ||
            !manifest.Occlusion.SequenceEqual(minimal.Occlusion) ||
            !manifest.Textures.SequenceEqual(minimal.Textures) ||
            manifest.Spawn != minimal.Spawn || manifest.Music != minimal.Music ||
            manifest.Totals != minimal.Totals || manifest.Exit != minimal.Exit || manifest.Save != minimal.Save ||
            !manifest.Mobys.Contains(minimal.Mobys.Single()) ||
            manifest.Mobys.Count is < 2 or > 3)
        {
            throw new InvalidDataException("The winding-v2 locked manifest envelope changed outside Moby ownership.");
        }
    }

    private static void ValidateLimits(Id65MobyBundleCompilerLimits limits, Id65MobyPlacementIntent[] placements)
    {
        int objectCount = placements.Any(item => item.TargetTrueIndex == 107) ? 108 : 107;
        if (limits.MaximumObjectCount < objectCount)
            throw new InvalidDataException("Moby object-row capacity is insufficient.");
        if (limits.MaximumActorRootCount < 38)
            throw new InvalidDataException("Moby actor-root capacity is insufficient.");
        if (limits.ActorSubfileEnd < PackageOffset + PackageByteLength)
            throw new InvalidDataException("Moby actor-tail capacity is insufficient.");
        if (limits.SceneSubfileEnd < PropertiesOffset - SceneOffset + 8)
            throw new InvalidDataException("Moby scene-tail capacity is insufficient.");
        if (limits.MinimumPropertiesSceneOffset > GrassPropertiesPointer)
            throw new InvalidDataException("Moby properties allocation violates the scene-tail floor.");
    }

    private static List<Id65MobyOwnedDataRange> BuildOwnedRanges(
        byte[] source,
        Id65MobyDependencyBundleDescriptor bundle,
        Id65MobyPlacementIntent[] placements)
    {
        List<Id65MobyOwnedDataRange> ranges = [];
        byte[] package = Convert.FromHexString(bundle.ActorPackages.Single().ExactHex);
        byte[] properties = Convert.FromHexString(bundle.Properties.Single().ExactHex);
        byte[] rowTemplate = Convert.FromHexString(bundle.Rows.Single().ExactHex);
        AddRange(ranges, source, "asset.root37", bundle.Id, Root37Offset, UInt32(GrassPackagePointer));
        AddRange(ranges, source, "asset.actor-id37", bundle.Id, ActorId37Offset, UInt16(GrassActorId));
        AddRange(ranges, source, "asset.package.01f5", bundle.Id, PackageOffset, package);

        foreach (Id65MobyPlacementIntent placement in placements.OrderBy(item => item.TargetTrueIndex))
        {
            int rowOffset = placement.TargetTrueIndex == 88 ? T88RowOffset : T107RowOffset;
            byte[] row = rowTemplate.ToArray();
            BinaryPrimitives.WriteUInt32LittleEndian(row, GrassPropertiesPointer);
            if (placement.CoordinatePolicy == Id65MobyCoordinatePolicy.PreserveDestination)
                source.AsSpan(rowOffset + 0x0C, 12).CopyTo(row.AsSpan(0x0C, 12));
            else
            {
                Id65AuthoringPoint point = placement.CoordinatesOrDelta!.Value;
                BinaryPrimitives.WriteInt32LittleEndian(row.AsSpan(0x0C, 4), point.X);
                BinaryPrimitives.WriteInt32LittleEndian(row.AsSpan(0x10, 4), point.Y);
                BinaryPrimitives.WriteInt32LittleEndian(row.AsSpan(0x14, 4), point.Z);
            }
            if (placement.YawByte.HasValue)
                row[0x47] = placement.YawByte.Value;
            AddRange(ranges, source, $"placement.t{placement.TargetTrueIndex}", placement.Id, rowOffset, row);
        }

        bool appended = placements.Any(item => item.TargetTrueIndex == 107);
        if (appended)
        {
            AddRange(ranges, source, "scene.object-count", "allocation.rows", ObjectCountOffset, UInt32(108));
            AddRange(ranges, source, "scene.fixup-count", "allocation.fixups", FixupCountOffset, UInt32(0x82));
            AddRange(ranges, source, "scene.fixup.t107-properties", "allocation.fixups", FixupAppendOffset, UInt32(0x2638));
        }
        AddRange(ranges, source, "asset.properties.grass", bundle.Id, PropertiesOffset, properties);
        return ranges.OrderBy(item => item.DataRelativeOffset).ToList();
    }

    private static void AddRange(
        List<Id65MobyOwnedDataRange> ranges,
        byte[] source,
        string stableId,
        string ownerId,
        int offset,
        byte[] after)
    {
        byte[] before = source.AsSpan(offset, after.Length).ToArray();
        if (before.SequenceEqual(after))
            throw new InvalidDataException($"Moby range `{stableId}` became redundant.");
        ranges.Add(new(
            stableId,
            ownerId,
            offset,
            after.Length,
            Convert.ToHexString(before),
            Convert.ToHexString(after),
            Hash(before),
            Hash(after)));
    }

    private static void ValidateOwnedRangesAgainstV2(
        byte[] source,
        byte[] v2Output,
        IReadOnlyList<Id65MobyOwnedDataRange> owned,
        IReadOnlyList<UnusedLevel65RemoteBlankStructuralPatch> v2Patches)
    {
        for (int index = 0; index < owned.Count; index++)
        {
            Id65MobyOwnedDataRange range = owned[index];
            if (!source.AsSpan(range.DataRelativeOffset, range.ByteLength)
                    .SequenceEqual(v2Output.AsSpan(range.DataRelativeOffset, range.ByteLength)))
                throw new InvalidDataException($"Moby ownership range `{range.StableId}` overlaps winding-v2 output bytes.");
            if (index > 0 && RangesOverlap(
                    owned[index - 1].DataRelativeOffset,
                    owned[index - 1].ByteLength,
                    range.DataRelativeOffset,
                    range.ByteLength))
                throw new InvalidDataException("Moby owned data ranges overlap.");
            if (v2Patches.Any(patch => RangesOverlap(
                    patch.DataRelativeOffset,
                    patch.Before.Length,
                    range.DataRelativeOffset,
                    range.ByteLength)))
                throw new InvalidDataException($"Moby range `{range.StableId}` overlaps a v2 structural patch.");
        }
    }

    private static void ValidateTransactionPatches(byte[] source, TransactionPatch[] patches)
    {
        TransactionPatch[] ordered = patches.OrderBy(item => item.Offset).ToArray();
        for (int index = 0; index < ordered.Length; index++)
        {
            TransactionPatch patch = ordered[index];
            if (patch.Before.Length != patch.After.Length || patch.Before.Length == 0 ||
                !source.AsSpan(patch.Offset, patch.Before.Length).SequenceEqual(patch.Before))
                throw new InvalidDataException($"Combined transaction patch `{patch.Id}` lost its locked-source preimage.");
            if (index > 0 && RangesOverlap(
                    ordered[index - 1].Offset,
                    ordered[index - 1].Before.Length,
                    patch.Offset,
                    patch.Before.Length))
                throw new InvalidDataException("Combined v2/Moby transaction patches overlap.");
        }
    }

    private static byte[] ApplyPatches(byte[] input, TransactionPatch[] patches, bool reverse)
    {
        byte[] output = input.ToArray();
        IEnumerable<TransactionPatch> ordered = reverse
            ? patches.OrderByDescending(item => item.Offset)
            : patches.OrderBy(item => item.Offset);
        foreach (TransactionPatch patch in ordered)
        {
            byte[] expected = reverse ? patch.After : patch.Before;
            byte[] replacement = reverse ? patch.Before : patch.After;
            if (!output.AsSpan(patch.Offset, expected.Length).SequenceEqual(expected))
                throw new InvalidDataException($"Combined patch `{patch.Id}` lost its exact {(reverse ? "afterimage" : "preimage")}.");
            replacement.CopyTo(output, patch.Offset);
        }
        return output;
    }

    private static void VerifyOnlyOwnedDifferencesFromV2(
        byte[] v2Output,
        byte[] output,
        IReadOnlyList<Id65MobyOwnedDataRange> owned)
    {
        bool[] allowed = new bool[output.Length];
        foreach (Id65MobyOwnedDataRange range in owned)
            Array.Fill(allowed, true, range.DataRelativeOffset, range.ByteLength);
        for (int index = 0; index < output.Length; index++)
        {
            if (v2Output[index] != output[index] && !allowed[index])
                throw new InvalidDataException($"Combined output escaped Moby ownership at data+0x{index:X}.");
        }
    }

    private static Id65MobyPlacementReadback[] BuildPlacementReadback(
        byte[] output,
        Id65MobyPlacementIntent[] placements) =>
        placements.OrderBy(item => item.TargetTrueIndex).Select(placement =>
        {
            int offset = placement.TargetTrueIndex == 88 ? T88RowOffset : T107RowOffset;
            ReadOnlySpan<byte> row = output.AsSpan(offset, RecordByteLength);
            return new Id65MobyPlacementReadback(
                placement.Id,
                placement.TargetTrueIndex,
                placement.RowPolicy,
                BinaryPrimitives.ReadInt32LittleEndian(row.Slice(0x0C, 4)),
                BinaryPrimitives.ReadInt32LittleEndian(row.Slice(0x10, 4)),
                BinaryPrimitives.ReadInt32LittleEndian(row.Slice(0x14, 4)),
                BinaryPrimitives.ReadUInt16LittleEndian(row.Slice(0x36, 2)),
                BinaryPrimitives.ReadInt32LittleEndian(row.Slice(0, 4)),
                Hash(row));
        }).ToArray();

    private static void VerifySemanticReadback(
        byte[] source,
        byte[] output,
        Id65MobyPlacementIntent[] placements,
        Id65MobyPlacementReadback[] readback)
    {
        bool hasT88 = placements.Any(item => item.TargetTrueIndex == 88);
        bool hasT107 = placements.Any(item => item.TargetTrueIndex == 107);
        if (BinaryPrimitives.ReadUInt32LittleEndian(output.AsSpan(Root37Offset, 4)) != GrassPackagePointer ||
            BinaryPrimitives.ReadUInt16LittleEndian(output.AsSpan(ActorId37Offset, 2)) != GrassActorId ||
            Hash(output.AsSpan(PackageOffset, PackageByteLength)) != GrassPackageSha256 ||
            Hash(output.AsSpan(PropertiesOffset, 8)) != GrassPropertiesSha256 ||
            BinaryPrimitives.ReadInt32LittleEndian(output.AsSpan(ObjectCountOffset, 4)) != (hasT107 ? 108 : 107) ||
            BinaryPrimitives.ReadInt32LittleEndian(output.AsSpan(FixupCountOffset, 4)) != (hasT107 ? 0x82 : 0x81))
        {
            throw new InvalidDataException("Actor root/package/properties/count/fixup readback changed.");
        }
        if (hasT88)
        {
            RequireHash(Hash(source.AsSpan(T88RowOffset, RecordByteLength)), SourceT88RowSha256, "source T88 thief row");
            RequireHash(readback.Single(item => item.TargetTrueIndex == 88).RowSha256, OutputT88RowSha256, "output T88 Grass row");
            RequireHash(Hash(source.AsSpan(PrivateThiefBlockOffset, PrivateThiefBlockByteLength)), PrivateThiefBlockSha256, "T88 private block");
            if (!source.AsSpan(PrivateThiefBlockOffset, PrivateThiefBlockByteLength)
                    .SequenceEqual(output.AsSpan(PrivateThiefBlockOffset, PrivateThiefBlockByteLength)))
                throw new InvalidDataException("ZeroEgg did not preserve the orphaned thief private block/path.");
        }
        if (hasT107)
            RequireHash(readback.Single(item => item.TargetTrueIndex == 107).RowSha256, OutputT107RowSha256, "output T107 Grass row");
        if (readback.Any(item => item.ActorId != GrassActorId || item.PropertiesSceneOffset != 0x8138))
            throw new InvalidDataException("A compiled Grass row lost actor or shared-properties identity.");

        int fixupCount = BinaryPrimitives.ReadInt32LittleEndian(output.AsSpan(FixupCountOffset, 4));
        int t88Occurrences = 0;
        int t107Occurrences = 0;
        int privateOccurrences = 0;
        for (int index = 0; index < fixupCount; index++)
        {
            int field = BinaryPrimitives.ReadInt32LittleEndian(output.AsSpan(FixupListOffset + (index * 4), 4));
            if (field == 0x1FB0) t88Occurrences++;
            if (field == 0x2638) t107Occurrences++;
            if (field == 0x5AFC) privateOccurrences++;
        }
        if (t88Occurrences != 1 || privateOccurrences != 1 || t107Occurrences != (hasT107 ? 1 : 0))
            throw new InvalidDataException("Scene fixup dedupe/readback changed; only appended T107 may add one fixup.");
    }

    private static Id65MobyAssetRelocation[] BuildRelocations(
        Id65MobyDependencyBundleDescriptor bundle,
        Id65MobyPlacementIntent[] placements)
    {
        bool shared = placements.Length > 1;
        List<Id65MobyAssetRelocation> result =
        [
            new(
                "asset.root.actor-01f5",
                "actor-root",
                new Id65LogicalAddress("actor-root.artisans", 22, GrassActorId),
                new Id65LogicalAddress("actor-root.id65", 37, GrassActorId),
                6,
                Hash(Encoding.ASCII.GetBytes("01f5:1cfa44")),
                shared),
            new(
                bundle.ActorPackages.Single().Id,
                "actor-package",
                new Id65LogicalAddress("data.artisans", 0x1C1520, 0),
                new Id65LogicalAddress("data.id65", PackageOffset, 0),
                PackageByteLength,
                GrassPackageSha256,
                shared),
            new(
                bundle.Properties.Single().Id,
                "scene-properties",
                new Id65LogicalAddress("scene.artisans", 0x11ACC, 0),
                new Id65LogicalAddress("scene.id65", 0x8138, 0),
                8,
                GrassPropertiesSha256,
                shared)
        ];
        result.AddRange(placements.OrderBy(item => item.TargetTrueIndex).Select(item => new Id65MobyAssetRelocation(
            $"row.t{item.TargetTrueIndex}",
            "moby-row",
            new Id65LogicalAddress("object-row.artisans", 121, 0),
            new Id65LogicalAddress("object-row.id65", item.TargetTrueIndex, 0),
            RecordByteLength,
            item.TargetTrueIndex == 88 ? OutputT88RowSha256 : OutputT107RowSha256,
            Deduplicated: false)));
        return result.ToArray();
    }

    private static void VerifyFrozenScenarioPins(
        string scenarioId,
        string manifestSha256,
        byte[] output,
        DiffProof diff,
        int mobyChangedOverV2,
        string ownedMapSha256,
        string transactionSha256,
        string relocationMapSha256,
        string deterministicPlanSha256)
    {
        string expected = scenarioId switch
        {
            AppendScenarioId => ExpectedAppendOutputDataSha256,
            ZeroEggScenarioId => ExpectedZeroEggOutputDataSha256,
            _ => ExpectedCombinedOutputDataSha256
        };
        RequireHash(Hash(output), expected, $"{scenarioId} output data");
        (int expectedChanged, int expectedRanges, int expectedMobyChanged, string expectedDiff,
            string expectedOwned, string expectedTransaction, string expectedManifest,
            string expectedRelocation, string expectedPlan) = scenarioId switch
        {
            AppendScenarioId => (
                ExpectedAppendChangedDataByteCount,
                ExpectedAppendDiffRangeCount,
                293,
                ExpectedAppendDiffManifestSha256,
                ExpectedAppendOwnedRangeMapSha256,
                ExpectedAppendTransactionSha256,
                ExpectedAppendManifestSha256,
                ExpectedAppendRelocationMapSha256,
                ExpectedAppendDeterministicPlanSha256),
            ZeroEggScenarioId => (
                ExpectedZeroEggChangedDataByteCount,
                ExpectedZeroEggDiffRangeCount,
                276,
                ExpectedZeroEggDiffManifestSha256,
                ExpectedZeroEggOwnedRangeMapSha256,
                ExpectedZeroEggTransactionSha256,
                ExpectedZeroEggManifestSha256,
                ExpectedZeroEggRelocationMapSha256,
                ExpectedZeroEggDeterministicPlanSha256),
            _ => (
                ExpectedCombinedChangedDataByteCount,
                ExpectedCombinedDiffRangeCount,
                ExpectedCombinedMobyChangedBytesOverV2,
                ExpectedCombinedDiffManifestSha256,
                ExpectedCombinedOwnedRangeMapSha256,
                ExpectedCombinedTransactionSha256,
                ExpectedCombinedManifestSha256,
                ExpectedCombinedRelocationMapSha256,
                ExpectedCombinedDeterministicPlanSha256)
        };
        if (diff.ChangedByteCount != expectedChanged || diff.RangeCount != expectedRanges ||
            mobyChangedOverV2 != expectedMobyChanged || diff.ManifestSha256 != expectedDiff ||
            ownedMapSha256 != expectedOwned || transactionSha256 != expectedTransaction ||
            manifestSha256 != expectedManifest || relocationMapSha256 != expectedRelocation ||
            deterministicPlanSha256 != expectedPlan)
        {
            throw new InvalidDataException("The frozen v2/Grass/ZeroEgg scenario diff pins changed.");
        }
    }

    private static void ValidateFoundationContract(
        UnusedLevel65MobyDependencyBundleContract contract,
        ReadOnlySpan<byte> exactActorPackage)
    {
        if (contract.ProfileId != UnusedLevel65MobyDependencyBundleFoundation.ProfileId ||
            contract.SchemaVersion != UnusedLevel65MobyDependencyBundleFoundation.SchemaVersion ||
            contract.DeterministicContractSha256 != UnusedLevel65MobyDependencyBundleFoundation.ExpectedContractSha256 ||
            contract.SourceRecord.TrueIndex != 121 || contract.SourceRecord.ActorId != GrassActorId ||
            contract.SourceRecord.Sha256 != GrassRowSha256 ||
            contract.Properties.PropertiesSha256 != GrassPropertiesSha256 ||
            contract.ActorPackage.ActorPackageSha256 != GrassPackageSha256 ||
            contract.ActorPackage.ActorPackageByteLength != PackageByteLength ||
            contract.ActorPackage.TexturePixelsRequired || contract.ActorPackage.ClutsRequired ||
            contract.ActorPackage.PackageRebaseRequired ||
            contract.Destination.FirstAppendTrueIndex != 107 ||
            contract.Destination.NewActorRootIndex != 37 ||
            contract.Destination.NewActorRootEntryRelativeOffset != PackageOffset ||
            contract.Destination.PropertiesAllocationSceneOffset != 0x8138 ||
            contract.Destination.AppendedPointerFieldSceneOffset != 0x2638 ||
            !contract.StructurallyCompleteAllocation || !contract.StaticRuntimeDependencyClosureComplete ||
            contract.RunnableBundleSupport || !contract.StaticInspectionOnly || contract.ProducesPatches ||
            contract.WritesBin || contract.WritesCue || contract.AppIntegrated ||
            contract.NormalCreateBinEnabled || contract.ReleasePublicationAuthorized ||
            exactActorPackage.Length != PackageByteLength || Hash(exactActorPackage) != GrassPackageSha256)
        {
            throw new InvalidDataException("The frozen Artisans Grass foundation contract or exact package changed.");
        }
    }

    private static string BuildBundleCanonicalJson(
        string id,
        int schemaVersion,
        string foundationContractSha256,
        IReadOnlyList<Id65MobyRowTemplate> rows,
        IReadOnlyList<Id65MobyPropertiesAsset> properties,
        IReadOnlyList<Id65MobyActorPackageAsset> actorPackages,
        IReadOnlyList<Id65MobyActorRootRequirement> actorRoots,
        IReadOnlyList<Id65MobySceneFixupPolicy> fixups,
        IReadOnlyList<Id65MobyExternalDependencyIntent> externalDependencies,
        UnusedLevel65MobyBehaviorClosureManifest behaviorClosure,
        IReadOnlyList<UnusedLevel65MobyDependencyRequirement> runtimeDependencies) =>
        JsonSerializer.Serialize(
            new
            {
                id,
                schemaVersion,
                foundationContractSha256,
                rows,
                properties,
                actorPackages,
                actorRoots,
                fixups,
                externalDependencies,
                behaviorClosure,
                runtimeDependencies
            },
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            });

    private static DiffProof BuildDiffProof(byte[] before, byte[] after)
    {
        int changed = 0;
        int ranges = 0;
        StringBuilder manifest = new();
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
            changed += length;
            ranges++;
            manifest.Append(start.ToString("X8", CultureInfo.InvariantCulture));
            manifest.Append(':');
            manifest.Append(length.ToString("X8", CultureInfo.InvariantCulture));
            manifest.Append(':');
            manifest.Append(Hash(before.AsSpan(start, length)));
            manifest.Append(':');
            manifest.Append(Hash(after.AsSpan(start, length)));
            manifest.Append('\n');
        }
        return new(changed, ranges, Hash(Encoding.UTF8.GetBytes(manifest.ToString())));
    }

    private static string HashOwnedRanges(IEnumerable<Id65MobyOwnedDataRange> ranges) =>
        Hash(Encoding.UTF8.GetBytes(string.Join('\n', ranges.Select(item =>
            $"{item.StableId}|{item.OwnerId}|{item.DataRelativeOffset:X8}|{item.ByteLength:X8}|{item.BeforeSha256}|{item.AfterSha256}"))));

    private static string HashRelocations(IEnumerable<Id65MobyAssetRelocation> relocations) =>
        Hash(Encoding.UTF8.GetBytes(string.Join('\n', relocations.Select(item =>
            $"{item.StableId}|{item.Kind}|{item.Source}|{item.Output}|{item.ByteLength}|{item.ContentSha256}|{item.Deduplicated}"))));

    private static string HashTransaction(IEnumerable<TransactionPatch> patches) =>
        Hash(Encoding.UTF8.GetBytes(string.Join('\n', patches.OrderBy(item => item.Offset).Select(item =>
            $"{item.Id}|{item.Offset:X8}|{item.Before.Length:X8}|{Hash(item.Before)}|{Hash(item.After)}"))));

    private static int CountChangedBytes(ReadOnlySpan<byte> before, ReadOnlySpan<byte> after)
    {
        if (before.Length != after.Length)
            throw new InvalidDataException("Changed-byte inputs differ in length.");
        int changed = 0;
        for (int index = 0; index < before.Length; index++)
            if (before[index] != after[index]) changed++;
        return changed;
    }

    private static bool RangesOverlap(int leftOffset, int leftLength, int rightOffset, int rightLength) =>
        leftOffset < rightOffset + rightLength && rightOffset < leftOffset + leftLength;

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

    private static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static void RequireHash(string actual, string expected, string label)
    {
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
            throw new InvalidDataException($"{label} SHA-256 changed: expected {expected}, got {actual}.");
    }

    private sealed record TransactionPatch(string Id, int Offset, byte[] Before, byte[] After);
    private sealed record DiffProof(int ChangedByteCount, int RangeCount, string ManifestSha256);
}
