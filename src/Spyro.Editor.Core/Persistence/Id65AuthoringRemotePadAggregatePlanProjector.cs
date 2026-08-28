using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Workspace;

namespace Spyro.Editor.Core.Persistence;

internal sealed record Id65AuthoringRemotePadAggregatePlanRequest(
    Id65AuthoringEditorDocumentStoreResult StoredDocument,
    UnusedLevel65BlankLevelLabManifest LabManifest,
    Id65AuthoringEditorNativeInputProvider.InputSet NativeInputs);

internal sealed record Id65AuthoringRemotePadAggregatePlanFrozenPin(
    string PositionId,
    string EnvelopeCanonicalSha256,
    string StaticAuthoringProjectionSha256,
    string OutputRow80Sha256,
    string OwnedRangeMapSha256,
    string InverseRangeMapSha256,
    string DeterministicAggregatePlanSha256);

internal sealed record Id65AuthoringRemotePadAggregatePlanStateFrozenPin(
    bool StructuralTerrainPresent,
    bool GrassT107Present,
    string PositionId,
    string EnvelopeCanonicalSha256,
    string StaticAuthoringProjectionSha256,
    string OutputRow80Sha256,
    string OwnedRangeMapSha256,
    string InverseRangeMapSha256,
    string ProtectedRangeMapSha256,
    string AtomicGroupMapSha256,
    string DeterministicAggregatePlanSha256);

/// <summary>
/// One complete, immutable schema-3 WAD80 transaction. It owns only row-80
/// before/after leaves and destination evidence; it cannot write a candidate,
/// enter the App, enable Create BIN, or claim runtime acceptance.
/// </summary>
internal sealed class Id65AuthoringRemotePadAggregatePlan
{
    private readonly ReadOnlyCollection<Id65AuthoringEditorAggregateOwnedRange>
        _ownedRanges;
    private readonly ReadOnlyCollection<Id65AuthoringEditorAggregateOwnedRange>
        _inverseRanges;
    private readonly ReadOnlyCollection<Id65AuthoringEditorAggregateProtectedRange>
        _protectedRanges;
    private readonly ReadOnlyCollection<Id65AuthoringEditorAggregateAtomicGroup>
        _atomicGroups;

    internal Id65AuthoringRemotePadAggregatePlan(
        Id65AuthoringEditorAggregateDestinationBinding destination,
        Id65AuthoringEditorDocument document,
        string envelopeCanonicalSha256,
        string staticAuthoringProjectionSha256,
        string nativeInputSetSha256,
        string sourceRow80Sha256,
        string outputRow80Sha256,
        string outputModelSha256,
        string targetOverlaySha256,
        string globalExecutableSha256,
        string baselineOutputRow80Sha256,
        string baselineDeterministicPlanSha256,
        string compilerOwnedRangeMapSha256,
        string compilerTransactionSha256,
        string compilerDeterministicPlanSha256,
        Id65AuthoringRemotePadCompositePlacementReadback placement,
        IEnumerable<Id65AuthoringEditorAggregateOwnedRange> ownedRanges,
        IEnumerable<Id65AuthoringEditorAggregateOwnedRange> inverseRanges,
        IEnumerable<Id65AuthoringEditorAggregateProtectedRange> protectedRanges,
        IEnumerable<Id65AuthoringEditorAggregateAtomicGroup> atomicGroups,
        int guardedByteCount,
        int protectedByteCount,
        int changedByteCount,
        string ownedRangeMapSha256,
        string inverseRangeMapSha256,
        string protectedRangeMapSha256,
        string protectedComplementSha256,
        string atomicGroupMapSha256,
        string deterministicAggregatePlanSha256)
    {
        Destination = destination;
        Document = Id65AuthoringEditorDocument.Parse(document.SerializeUtf8());
        DocumentCanonicalSha256 = Document.CanonicalSha256;
        StructuralTerrainPresent = Document.Terrain.Count == 1;
        GrassT107Present = Document.MobyPlacements.Any(item =>
            item.TargetTrueIndex == Id65RemotePadGrassT107Catalog.TargetTrueIndex);
        EnvelopeCanonicalSha256 = envelopeCanonicalSha256;
        StaticAuthoringProjectionSha256 = staticAuthoringProjectionSha256;
        NativeInputSetSha256 = nativeInputSetSha256;
        SourceRow80Sha256 = sourceRow80Sha256;
        OutputRow80Sha256 = outputRow80Sha256;
        OutputModelSha256 = outputModelSha256;
        TargetOverlaySha256 = targetOverlaySha256;
        GlobalExecutableSha256 = globalExecutableSha256;
        BaselineOutputRow80Sha256 = baselineOutputRow80Sha256;
        BaselineDeterministicPlanSha256 = baselineDeterministicPlanSha256;
        CompilerOwnedRangeMapSha256 = compilerOwnedRangeMapSha256;
        CompilerTransactionSha256 = compilerTransactionSha256;
        CompilerDeterministicPlanSha256 = compilerDeterministicPlanSha256;
        Placement = placement;
        _ownedRanges = Array.AsReadOnly(
            ownedRanges.Select(item => item.DeepCopy()).ToArray());
        _inverseRanges = Array.AsReadOnly(
            inverseRanges.Select(item => item.DeepCopy()).ToArray());
        _protectedRanges = Array.AsReadOnly(protectedRanges.ToArray());
        _atomicGroups = Array.AsReadOnly(atomicGroups.ToArray());
        GuardedByteCount = guardedByteCount;
        ProtectedByteCount = protectedByteCount;
        ChangedByteCount = changedByteCount;
        OwnedRangeMapSha256 = ownedRangeMapSha256;
        InverseRangeMapSha256 = inverseRangeMapSha256;
        ProtectedRangeMapSha256 = protectedRangeMapSha256;
        ProtectedComplementSha256 = protectedComplementSha256;
        AtomicGroupMapSha256 = atomicGroupMapSha256;
        DeterministicAggregatePlanSha256 = deterministicAggregatePlanSha256;
    }

    public string ProfileId => Id65AuthoringRemotePadAggregatePlanProjector.ProfileId;
    public int SchemaVersion => Id65AuthoringRemotePadAggregatePlanProjector.SchemaVersion;
    public int LevelId => 65;
    public int DataWadEntry => 80;
    public Id65AuthoringEditorAggregateDestinationBinding Destination { get; }
    public Id65AuthoringEditorDocument Document { get; }
    public string DocumentCanonicalSha256 { get; }
    public bool StructuralTerrainPresent { get; }
    public bool GrassT107Present { get; }
    public string EnvelopeCanonicalSha256 { get; }
    public string StaticAuthoringProjectionSha256 { get; }
    public string NativeInputSetSha256 { get; }
    public string CatalogCanonicalSha256 => Id65RemotePadGrassT107Catalog.CanonicalSha256;
    public string SourceRow80Sha256 { get; }
    public string OutputRow80Sha256 { get; }
    public string OutputModelSha256 { get; }
    public string TargetOverlaySha256 { get; }
    public string GlobalExecutableSha256 { get; }
    public string BaselineOutputRow80Sha256 { get; }
    public string BaselineDeterministicPlanSha256 { get; }
    public string CompilerOwnedRangeMapSha256 { get; }
    public string CompilerTransactionSha256 { get; }
    public string CompilerDeterministicPlanSha256 { get; }
    public Id65AuthoringRemotePadCompositePlacementReadback Placement { get; }
    public IReadOnlyList<Id65AuthoringEditorAggregateOwnedRange> OwnedRanges =>
        _ownedRanges;
    public IReadOnlyList<Id65AuthoringEditorAggregateOwnedRange> InverseRanges =>
        _inverseRanges;
    public IReadOnlyList<Id65AuthoringEditorAggregateProtectedRange> ProtectedRanges =>
        _protectedRanges;
    public IReadOnlyList<Id65AuthoringEditorAggregateAtomicGroup> AtomicGroups =>
        _atomicGroups;
    public int OwnedRangeCount => _ownedRanges.Count;
    public int InverseRangeCount => _inverseRanges.Count;
    public int ProtectedRangeCount => _protectedRanges.Count;
    public int GuardedByteCount { get; }
    public int ProtectedByteCount { get; }
    public int ChangedByteCount { get; }
    public string OwnedRangeMapSha256 { get; }
    public string InverseRangeMapSha256 { get; }
    public string ProtectedRangeMapSha256 { get; }
    public string ProtectedComplementSha256 { get; }
    public string AtomicGroupMapSha256 { get; }
    public string DeterministicAggregatePlanSha256 { get; }

    public bool FullExactSchema3DocumentRequired => true;
    public bool FrozenStoreEnvelopeIdentityMatched => true;
    public bool FrozenStaticAuthoringProjectionIdentityMatched => true;
    public bool StoreEnvelopeIdentityIndependentlyVerified => false;
    public bool StaticAuthoringProjectionRecomputedFromStoreBytes => false;
    public bool NativeInputGenerationReverified => true;
    public bool ExactPhysicalDestinationBindingPinned => true;
    public bool PhysicalDestinationReadbackVerified => true;
    public bool T66StructuralTerrainBindingVerified => true;
    public bool RepairedT13995SupportBindingVerified => true;
    public bool RemotePadCatalogBindingVerified => true;
    public bool GrassT107StaticBindingVerified => true;
    public bool GrassT88ExcludedVerified => true;
    public bool SpawnAndT92ExactBindingVerified => true;
    public bool ProtectedComplementVerified => true;
    public bool ExactInverseVerified => true;
    public bool OneAtomicTransactionRequired => true;
    public bool ContainsOwnedAfterimageBytes => true;
    public bool ContainsCompleteCompositeAfterimage => false;
    public bool ContainsDiscImageBytes => false;
    public bool ContainsCueBytes => false;
    public bool PartialRangeApplicationAuthorized => false;
    public bool CrossSliceCompositionSupported => false;
    public bool AfterimageStackingAuthorized => false;
    public bool GenericExporterCompatible => false;
    public bool RetailWriterCompatible => false;
    public bool RetailLevelMutationAuthorized => false;
    public bool PersistenceEnabled => false;
    public bool WriterAuthorized => false;
    public bool WritesFileSystem => false;
    public bool WritesDiscImage => false;
    public bool WritesBin => false;
    public bool WritesCue => false;
    public bool AppIntegrated => false;
    public bool UiIntegrated => false;
    public bool CreateBinEnabled => false;
    public bool NormalCreateBinEnabled => false;
    public bool RuntimeCandidateAuthorized => false;
    public bool RuntimeProven => false;
    public bool SaveAuthorized => false;
    public bool SavePersistenceProven => false;
    public bool TreasureTotal200Proven => false;
    public bool MusicLongPlayProven => false;
    public bool ExitDestination65Proven => false;
    public bool ReleaseIntegrated => false;
    public bool ReleaseAuthorized => false;
    public bool PromotionAuthorized => false;
    public bool Publishable => false;
    public bool CurrentOnDiskFreshnessVerified => false;
    public bool RequiresTrustedExclusiveWorkspaceDirectory => true;
    public bool HostileFilesystemRaceSafe => false;
    public bool CommonWorkspaceLeaseOwned => false;
}

/// <summary>
/// Rebinds one genuine schema-3 store readback to fresh provider-owned native
/// inputs and E1 compilation. The result remains an offline plan only.
/// </summary>
internal static class Id65AuthoringRemotePadAggregatePlanProjector
{
    public const string ProfileId =
        "id65-authoring-remote-pad-aggregate-plan-schema3-in-memory-v1";
    public const int SchemaVersion = 1;
    public const int ExpectedRow80ByteLength = 0x2E2000;
    public const int ExpectedOwnedRangeCount = 7_960;
    public const int ExpectedOwnedRangeCountWithoutT107 = 7_956;
    public const int ExpectedTexturePageRangeCount = 7_949;
    public const int ExpectedGrassRangeCount = 8;
    public const int ExpectedGrassRangeCountWithoutT107 = 4;
    public const int ExpectedGuardedByteCount = 1_010_952;
    public const int ExpectedGuardedByteCountWithoutT107 = 1_010_852;
    public const int ExpectedProtectedByteCount = 2_011_896;
    public const int ExpectedProtectedByteCountWithoutT107 = 2_011_996;
    public const int ExpectedProtectedRangeCount = 7_961;
    public const int ExpectedProtectedRangeCountWithoutT107 = 7_957;
    public const string ExpectedProtectedRangeMapSha256 =
        "bbbf1311df5797455b47cc3b408ba07884a826f76f242d2fdf7db54629d1a609";
    public const string ExpectedProtectedComplementSha256 =
        "2211814c105583eecf0d1e3f259ce8dd867a2c4a3988ac77c2c1541adf4fc5a4";
    public const string ExpectedAtomicGroupMapSha256 =
        "effb5602b6930ca1bb5ec56a48f14d7d0be2b438a2150943c9afdc1a970a672e";

    private static readonly Id65AuthoringRemotePadAggregatePlanFrozenPin[]
        FrozenPinArray =
    [
        new(
            Id65RemotePadGrassT107Catalog.NorthWestPositionId,
            "f24766c877de0b5473382cdc85d38f285a113feb15572c8d246b2bfd1f67884f",
            "5ed5d1ca51b2484927c4695b5ac0978d20d5192ce648d60c32878720e1b2e904",
            "0701cb4de1ceca00143976ef6a7dfbc1da4d528a020cfad616169a3a06a60e60",
            "fa0288492a0922713affad28a4444fcd22f3b4a9fa5dc0b77ed1b5df544ad8f8",
            "37ee6ecc2aa0a688c9803579e3b256e92ebc7173ea135c27eca494dabfac7acf",
            "e154925ff199a7987a685cec58a388e4c3c2a50d0324c17c02e31877d311745d"),
        new(
            Id65RemotePadGrassT107Catalog.NorthPositionId,
            "1b07d76e08d1dd85eb3f9d2b5f13e345d89c2ce11d8927328258d40e6596b7f3",
            "34ba078b18d34cab5f17456cb7771c38084ffd0cc6740bc30e318c0ed2690da4",
            "097517a1d19c6f8bd947401fcaa159062caed8e552f757777c58aaac8cdaf550",
            "161ee316ca5bbf8a98e3a45b2cb9682bb780a7423d09e50e0abe49b4a658a923",
            "a53cd2286ea6bef1bbc5332d3f54e50bfd0f454821bad572478898d6cc7c5aff",
            "34214efbe01c5a4beaf66ac4d2e30e2e58d13cf3858d082bea21d1d83ca61b9a"),
        new(
            Id65RemotePadGrassT107Catalog.NorthEastPositionId,
            "5abb6da1a77456304d35b04986af1d3959753b8777f603a7eaea7473674b878c",
            "0c951d6f4ba8704d6bb5978296ff51518d08caba34bb8209e739e04a39276df7",
            "48e6069d5f764a851dca4f65e3d978130c52c2dcb27f2879eaeadd87597d62b2",
            "442a03d735e12c8678733db60a2dcb4c03793bdebdc2902dd4b5ea2d7ed2d1b3",
            "79b3185632ed07ce0edef6b2e21245a3272b7e9fe3e5f05c91a71402f2dddb41",
            "90a095b995dcaf151643940f85b0af96838103ba1dcff281dcc25fd13337ff8c"),
        new(
            Id65RemotePadGrassT107Catalog.WestPositionId,
            "f93c96af6959806640fa000b6ceaeabc6c8a243de540601fd2949a6aba1bf7ff",
            "4afe6f94085ec4d0171f6d155bffde350d5aa9b13643810a744f68e067c4ce45",
            "2c09d59715026b062f1c2d570a28fc42d787e940870bb8622cfa3353417eb161",
            "47d72458f40289d6ab9d0237a3df200f2cb3796f16141e4e30c02932db1cc886",
            "52ed678f9d943717a7f9587268e5642a812b218963bb3341551c9d32c39348aa",
            "69fac939404f039b81e43ff5c88c7aa4bd38eadb15823f69e50f37598668d4ee"),
        new(
            Id65RemotePadGrassT107Catalog.CenterPositionId,
            "0076070dfa92ecb87fda3e0b87c6075eb0e140b3a3c8820c76e4f27a20af02a9",
            "998bff9ffee6f0490ba56ea5e8b5219e31803075ef64782d2474b79e098d8fdc",
            "5deb3c24686fba84f4d25c30eec5f1bc6159df2e2165b98b31f7c32279c10f80",
            "86eac4fc4c67188fc00a4b6ab27adbabbc7aea7f41c5fac0fc43f0a731df583b",
            "3615408f4981d5a17348e2c8c9fe380dc956a00ae2d8df2d419728387c28bd70",
            "22c918d011d62f58c5badc343e488b8f0e765cafe6eda86c796ac2c6588c05ed"),
        new(
            Id65RemotePadGrassT107Catalog.EastPositionId,
            "945fa8c03f0ad372a8309c967d22ad31c9f06a311df8efc527bfa2c39f5637cc",
            "9ca09363815044cbb5e1ec7bd05a8dc6fc8b73e8872cc91687647102a843b057",
            "00f5869e8d1398f4a0264eef9b52332a0a90ff66254995639eba09f2d00063f2",
            "86c1e4688be0d50e7c96867c36f6da261323a1de42da30fcfefa31f24d8e90c9",
            "f83e5903d9042f581630ab86c06cd4e268f433964cee52ee32c17283b08f41a4",
            "a11611a7b56ea567736e937d70825bdd65f119c11a41115dfde31d19349d3c27"),
        new(
            Id65RemotePadGrassT107Catalog.SouthWestPositionId,
            "74d87245cbf69e967b189554e01d730ff2f1ec574d3cf676894911b4b846f42b",
            "44445af3f2ff7e3ec62d39d51e35cad6d0b77bd3f78d4f884ea33a646899c372",
            "4041dd88c2e31a4bc425d1a929b2e763959963530197ca46893b5a2f69adedba",
            "cd610b1a22e5e7ed9f2959ca378cb02ea8d1d77a63dca701216188ce58615b6a",
            "49dd0ee90f6c1b70868d2753a976ba5a1fc2be2c9249b6bdd7ed3d7cec44fbf2",
            "bdc847e0428b3f79fd8a0fbf6271517af07b4cd4be48237e17a986a22fe6f935"),
        new(
            Id65RemotePadGrassT107Catalog.SouthPositionId,
            "b13424ca8833220049cfad7ec688be230fb25e33bd1fb095e0e58d92e8836cf3",
            "0f09f74ced231ec25c04bfd0e4138555ca035c801ce4f935fd33f7ec6d770ccd",
            "95c1b2f746ce5c212978dd0a2af1e13cac71ef1026c01765d215384c7dd20776",
            "adda39638e362c8c4952cf5e3e53c7b2099703fb7df4375ef7aeb081848e4bea",
            "9a54d9795c05d6ee03824156b69bb6571a11946598aae4c64f7868cee1d9bc15",
            "c132f69886330a772bb02b15f34b0a5892f13d1c1852c0007bd361034c7f0aad"),
        new(
            Id65RemotePadGrassT107Catalog.SouthEastPositionId,
            "e3c6db494af7626e920681eb6dfcb0d30e720f9b214a92615778e3d1c5b30ee5",
            "aa8abc140fad07d20a4868b3611af169464ba5808115fca38fe1da3fc727a964",
            "a6d4f09522dfafc046c2f61ad031034ff7eef4d32a79a8c1bad1a8e13f22fcd3",
            "c241d310cc4aab1de15023eebfcf4b1a28222c5a68b8012d62d6546d494f1a32",
            "0321c92383aeed22d4eb60e4074b1c6c6d56bc18ae86b37761c5a2d04c3484fc",
            "374cf6147daac4c87ad4cae08c658780e1561801c1d3829858faa227158e0c68")
    ];

    private static readonly ReadOnlyDictionary<
        string,
        Id65AuthoringRemotePadAggregatePlanFrozenPin> FrozenPins =
        new(FrozenPinArray.ToDictionary(item => item.PositionId, StringComparer.Ordinal));

    private static readonly Id65AuthoringRemotePadAggregatePlanStateFrozenPin[]
        AdditionalStateFrozenPinArray =
    [
        new(
            false,
            false,
            "remote-pad-grid.x368-y336",
            "08439efd80bd03332f2857a04ece4245b064efbed6fc8bc34d61298f985e2d6b",
            "3bb70b8fd4987a3d794b9e74778bbb19c7a65e9edfc74cd5dba91aedac371d82",
            "5d670998972bde939cf4b0cac9ab1812072a3a5f9d477ed13346bb55862a3fd0",
            "be206d64b792b40abbedae3d20a9d4aa472958ce88caf71175c86d2065cc596a",
            "b020ed78c633d32bca2e7f657e382344ee76d0bc17edfae76478c1eb69cded37",
            "843310a2affb9b14bddd207b4f954f16dd48153af90b1585eaedaa6b377660e2",
            "eb4d8677a47b4d0ebff5caeb5e2b4ab77a1a2bac18c96f25508a5adaf83afdf9",
            "34a89a7e5dd111ca7e724119fbf9266a50e579f2f306240cfc07601d7f3e7f5f"),
        new(
            false,
            false,
            "remote-pad-grid.x384-y336",
            "d44bcfde15eb73ce0c9579e51d039569742cc2e6bc418588c1215f5334890fe8",
            "e2afc211671d8b939398df293d66c10c4783b943a1ad3163abf85b1f70ca492b",
            "5d670998972bde939cf4b0cac9ab1812072a3a5f9d477ed13346bb55862a3fd0",
            "be206d64b792b40abbedae3d20a9d4aa472958ce88caf71175c86d2065cc596a",
            "b020ed78c633d32bca2e7f657e382344ee76d0bc17edfae76478c1eb69cded37",
            "843310a2affb9b14bddd207b4f954f16dd48153af90b1585eaedaa6b377660e2",
            "eb4d8677a47b4d0ebff5caeb5e2b4ab77a1a2bac18c96f25508a5adaf83afdf9",
            "788c3a3b7a5e070557155ae1e44bca2899eabbc4e24d94eb5a681cdfa185f6cd"),
        new(
            false,
            false,
            "remote-pad-grid.x400-y336",
            "91ba6b2c2fe5f451fbabf26858765d34238d6b0ff39312604353abae1398e99d",
            "0831338e16b96d0064a32662ef3c3a84646ac6fc076b3270c41eb301aa6d749e",
            "5d670998972bde939cf4b0cac9ab1812072a3a5f9d477ed13346bb55862a3fd0",
            "be206d64b792b40abbedae3d20a9d4aa472958ce88caf71175c86d2065cc596a",
            "b020ed78c633d32bca2e7f657e382344ee76d0bc17edfae76478c1eb69cded37",
            "843310a2affb9b14bddd207b4f954f16dd48153af90b1585eaedaa6b377660e2",
            "eb4d8677a47b4d0ebff5caeb5e2b4ab77a1a2bac18c96f25508a5adaf83afdf9",
            "2bfc7481e224ecbedcf593efd6288ad2d34f1f12a1e51c42e3113964a232f5b5"),
        new(
            false,
            false,
            "remote-pad-grid.x368-y352",
            "64f73461f0f33c36dbbdea4bfda73a85e079ea4971962db13278c2b88f96e0f4",
            "42e4541a9244ef26f5bdcdbf0a6a5bd26ed3b459cf288e59b7633254db21be20",
            "5d670998972bde939cf4b0cac9ab1812072a3a5f9d477ed13346bb55862a3fd0",
            "be206d64b792b40abbedae3d20a9d4aa472958ce88caf71175c86d2065cc596a",
            "b020ed78c633d32bca2e7f657e382344ee76d0bc17edfae76478c1eb69cded37",
            "843310a2affb9b14bddd207b4f954f16dd48153af90b1585eaedaa6b377660e2",
            "eb4d8677a47b4d0ebff5caeb5e2b4ab77a1a2bac18c96f25508a5adaf83afdf9",
            "21e2c3ed27410bbdd392d850611b71422d9cfaf6fcd43eb11f78d81bb9da67ff"),
        new(
            false,
            false,
            "remote-pad-grid.x384-y352",
            "457cf58a78e326c1e9cb82052817ee1fa0e0736f304bc813650f4608fb2587f6",
            "429899c2365938cd50c33709f19a36dd7aa7fd536195999ac16df155842c7b68",
            "5d670998972bde939cf4b0cac9ab1812072a3a5f9d477ed13346bb55862a3fd0",
            "be206d64b792b40abbedae3d20a9d4aa472958ce88caf71175c86d2065cc596a",
            "b020ed78c633d32bca2e7f657e382344ee76d0bc17edfae76478c1eb69cded37",
            "843310a2affb9b14bddd207b4f954f16dd48153af90b1585eaedaa6b377660e2",
            "eb4d8677a47b4d0ebff5caeb5e2b4ab77a1a2bac18c96f25508a5adaf83afdf9",
            "6d44a3abd65c4a4905f623bbc600cce136d26863f71383386de9a6ce4ebc41b1"),
        new(
            false,
            false,
            "remote-pad-grid.x400-y352",
            "87d59b58c12f6930a6eba8fce422ddcbe7853ea1ca82dc8872377d3a8d740daf",
            "461687a88d57bbd848b9c4755347e4d5aa42cbe2544b4a6cefc7b1d4b796e611",
            "5d670998972bde939cf4b0cac9ab1812072a3a5f9d477ed13346bb55862a3fd0",
            "be206d64b792b40abbedae3d20a9d4aa472958ce88caf71175c86d2065cc596a",
            "b020ed78c633d32bca2e7f657e382344ee76d0bc17edfae76478c1eb69cded37",
            "843310a2affb9b14bddd207b4f954f16dd48153af90b1585eaedaa6b377660e2",
            "eb4d8677a47b4d0ebff5caeb5e2b4ab77a1a2bac18c96f25508a5adaf83afdf9",
            "36949dba2f3b9d0e12111a785e5b118e917f97137b6e4913fc023837526e6560"),
        new(
            false,
            false,
            "remote-pad-grid.x368-y368",
            "5624b9622d8ab3a5d2e97e3fa351c7188798f5b4ae4c2692e4cfd9120e3f641d",
            "b39b05a6fb9fb800dd61f5c357db0e135049c2f115d7539135e3c5670f67f17d",
            "5d670998972bde939cf4b0cac9ab1812072a3a5f9d477ed13346bb55862a3fd0",
            "be206d64b792b40abbedae3d20a9d4aa472958ce88caf71175c86d2065cc596a",
            "b020ed78c633d32bca2e7f657e382344ee76d0bc17edfae76478c1eb69cded37",
            "843310a2affb9b14bddd207b4f954f16dd48153af90b1585eaedaa6b377660e2",
            "eb4d8677a47b4d0ebff5caeb5e2b4ab77a1a2bac18c96f25508a5adaf83afdf9",
            "57ef1973d081d1e6eae6e119e16757455ed726901ca043b1e97dabccfee68979"),
        new(
            false,
            false,
            "remote-pad-grid.x384-y368",
            "a67df09892b990b3c1e719b6adbc70966ed18f16ce382230a9de8289376d1a2b",
            "6dde4ad39cca6bfdda7bf66903d026840ce4386a613d960a36f937731915fd0d",
            "5d670998972bde939cf4b0cac9ab1812072a3a5f9d477ed13346bb55862a3fd0",
            "be206d64b792b40abbedae3d20a9d4aa472958ce88caf71175c86d2065cc596a",
            "b020ed78c633d32bca2e7f657e382344ee76d0bc17edfae76478c1eb69cded37",
            "843310a2affb9b14bddd207b4f954f16dd48153af90b1585eaedaa6b377660e2",
            "eb4d8677a47b4d0ebff5caeb5e2b4ab77a1a2bac18c96f25508a5adaf83afdf9",
            "105cf5474f88e401602863452dc4ffac74b66d2d7d07a0463b2daff3f9458ad6"),
        new(
            false,
            false,
            "remote-pad-grid.x400-y368",
            "4998685b84c5edb5d0bd93039d5b20490edcfed57d8821aa34d22a256805c12f",
            "a6da3d7d17505d192593721af7db7a1cc6afc6fc45d9d76a30bd403850fd38f6",
            "5d670998972bde939cf4b0cac9ab1812072a3a5f9d477ed13346bb55862a3fd0",
            "be206d64b792b40abbedae3d20a9d4aa472958ce88caf71175c86d2065cc596a",
            "b020ed78c633d32bca2e7f657e382344ee76d0bc17edfae76478c1eb69cded37",
            "843310a2affb9b14bddd207b4f954f16dd48153af90b1585eaedaa6b377660e2",
            "eb4d8677a47b4d0ebff5caeb5e2b4ab77a1a2bac18c96f25508a5adaf83afdf9",
            "4af74758696e482cff5f1d84c4e77200e4465055487018be939325cb59aa5db2"),
        new(
            false,
            true,
            "remote-pad-grid.x368-y336",
            "6bab371b0b6f615f494a0a1e29e38f9bffc320651f9868f199d2ff90ec1def94",
            "02c0a2a0aedb56695b65172ecc43669b7d11db8f1fa6b78a49b3337b328ea5f1",
            "43819fc49ac133633b3676f1621161e400e7739f5efa816780b13fa09497a47c",
            "fd122d5dce236ce61a608c88a263f09cdb7f7eea1e1a56b46001f6b6ec7cf608",
            "9493f11f44e2233fdbcc13e1055594f8e7fb7cf89211c250876d92464cd9078d",
            "bbbf1311df5797455b47cc3b408ba07884a826f76f242d2fdf7db54629d1a609",
            "effb5602b6930ca1bb5ec56a48f14d7d0be2b438a2150943c9afdc1a970a672e",
            "325c503a21143d7c7b6860b3a7c0f49ade189954a1f858a0c32c6538b2e9e87a"),
        new(
            false,
            true,
            "remote-pad-grid.x384-y336",
            "9f32dc018eb1194553222d5207e31ea470f635933db138d170b7a05ad3400488",
            "6d196e95292a3240866cfd5b382c84cd87b4c696fe4377089b5595c51de568f7",
            "603cb4855f97f69d8f7d03c9afb13472f35f4fa009db4acfa9b0cee41245151c",
            "3d62b9f52e786a8dcd366f250b730cc06b38411b9250621a84c4121073658fc1",
            "77ccf0cc8f54ba8632ae8e776a9b840cb40746c0693e837ebcda20e299abfc27",
            "bbbf1311df5797455b47cc3b408ba07884a826f76f242d2fdf7db54629d1a609",
            "effb5602b6930ca1bb5ec56a48f14d7d0be2b438a2150943c9afdc1a970a672e",
            "e20e5b942c63f780a19254941970bcd5616605b3cf4b4018f4e46540d1a7c5ac"),
        new(
            false,
            true,
            "remote-pad-grid.x400-y336",
            "beee04a944572a70a2e4b375a341700ae16f9226017b69c907a0e5c514bdbb93",
            "8052e85b6cb0eaa71f36cfb35184068aa128c5f0ec46738fcef9bca3d7baee2e",
            "94ad4b3acc6570d8f9233746ec179250807a3d17a189a4059db41d322299f195",
            "37cd78e3744b59633055ca6b7b4f2deebbcd82e74191f598ce65c6edd531e78f",
            "3ee09d903123bc41bcd2d41418d4e81ed0eafc0818b86700aacf78bddf22d5d3",
            "bbbf1311df5797455b47cc3b408ba07884a826f76f242d2fdf7db54629d1a609",
            "effb5602b6930ca1bb5ec56a48f14d7d0be2b438a2150943c9afdc1a970a672e",
            "bc2a4974d0356b314642aaec092cdd6c0165e07dc8c80d99a3db3f083f96213c"),
        new(
            false,
            true,
            "remote-pad-grid.x368-y352",
            "2982d7a7d309aef1220d221740cd52522ef0c3181a81651a22b79f133157c2fb",
            "82bde56c3645dcda165bc6cee8d9bd69c1b48345aa07d3d7e63c9f084b95c35d",
            "7ad0236af974e7542f35e039eae9e567aca971dc39d224c9d63ae3d8eebc962b",
            "70ce3a23937a4c6790972ebf68764ac35d21d86670fa41f110507291e1280001",
            "a63c4e15e43a5e339f70ac3bbc5767a9fda7fad9eedcbeac4153f79f50229271",
            "bbbf1311df5797455b47cc3b408ba07884a826f76f242d2fdf7db54629d1a609",
            "effb5602b6930ca1bb5ec56a48f14d7d0be2b438a2150943c9afdc1a970a672e",
            "a6439bdb5888003e53a114b46330b4a8ff4120a6cbd3130d67367dfe7a4bc160"),
        new(
            false,
            true,
            "remote-pad-grid.x384-y352",
            "20cd1126a70c443a1231fd78ea26b1f3fb91abd06507d79ff1da6fe33a09a179",
            "1d59eaa48eb399ccfedcd187a9d6d6cee2533702e23bd0b48dd43dcd7f84436e",
            "e9c377d1a594b0bc712b231ad00ad5a0259ac604d6b7b928c448253110549068",
            "a8e5268e390b6fb3e2ed1a52452feb811b012aa1eac63d964b24a94a7994560e",
            "f84ea68c3a77c912fcbfff5dd41dafa42479a54940464a91e2678c604a07d4fb",
            "bbbf1311df5797455b47cc3b408ba07884a826f76f242d2fdf7db54629d1a609",
            "effb5602b6930ca1bb5ec56a48f14d7d0be2b438a2150943c9afdc1a970a672e",
            "90869d00e4cd06c684cec1dd1da7a061f9e7920f75f225e08dd93803672833ba"),
        new(
            false,
            true,
            "remote-pad-grid.x400-y352",
            "677ad94bb29cd86c954b6df41460b3c1d8acc91ffb18c6dbbb8e6690c28421e3",
            "c7f3b4d9510fe50fa6e7e2efc46b68b24b81b300e5bb157929d57b74a6426c1b",
            "a94ab9f3c825e57c4110f74937622d5210cb94d3219aad572aab42a7dce5a528",
            "e5af98f1d52200323ef5ccb23bccc5ee28a2d1f320935e98de4102c7c2eb3514",
            "33d3bd1b2f58397735afa58018737ed4a48f9240222643cfd6f908986fcdcc7e",
            "bbbf1311df5797455b47cc3b408ba07884a826f76f242d2fdf7db54629d1a609",
            "effb5602b6930ca1bb5ec56a48f14d7d0be2b438a2150943c9afdc1a970a672e",
            "25db12a6ff4207e1ca7ba502d367c9204fad65a29de833353b9ebe98bee955f4"),
        new(
            false,
            true,
            "remote-pad-grid.x368-y368",
            "530a27751385b572a1e913918589befa8d8ea1033f7e98632d00a9ed19610c18",
            "8a02dd51ef3398736a592de5c71a3aca8d7d7015f4b1893bdcf358eab70f77d8",
            "45b95366fff8e8f0a78613d120843cd4eff5cb79c9b98beebc458af2d3527ba7",
            "c8731aae408de21fe9b6607103abd0f5e148d34626f1439459f97453b9b734db",
            "af173edece9f46af1ff13423d02259b2f6c3c34ac9fecd22fd16afbe18a11f65",
            "bbbf1311df5797455b47cc3b408ba07884a826f76f242d2fdf7db54629d1a609",
            "effb5602b6930ca1bb5ec56a48f14d7d0be2b438a2150943c9afdc1a970a672e",
            "0ca46faa8a6bbdd9ffc9720d9bc9ec3ce74f143f880c29ed526ab4ac2189068d"),
        new(
            false,
            true,
            "remote-pad-grid.x384-y368",
            "4678605902eca79db327ad35137a055f2d8bb1b01ffbb239d56e1f0709b1c516",
            "4c433ff4319daff96e695035d5901a6b985c1c03b78cabe74212f2274daf0f57",
            "157f857cb66077c4c79d72117180a7818a453f053a1e65be4cbb5a6d2555badc",
            "6f2cbfe5ec51f5f8e66720d249c274ab3f33ca4120c86952188530c685d4aa5e",
            "4b0f703a2ba4d4ba77cffb2e0066f03cfb5d5740fa3b1bf64132991b0e66949f",
            "bbbf1311df5797455b47cc3b408ba07884a826f76f242d2fdf7db54629d1a609",
            "effb5602b6930ca1bb5ec56a48f14d7d0be2b438a2150943c9afdc1a970a672e",
            "01d9d3bdb791fdae1674b38bba4663d3d00276e53d347cbbe6265e3820091f47"),
        new(
            false,
            true,
            "remote-pad-grid.x400-y368",
            "d020674a25040051dbfab19e8c4e273ed986e1b812627ff33689872e62f57143",
            "d5591c1a94eae18f2ba3bc78fac77093aba166a57d2bc0708f9ca4c0604942c2",
            "35abd2fd140a46218d3ca473638a48cc0490b2463582b2bacd055a2d7913ae85",
            "cb0759b69b708dba3c437c52007cea3e4058c056f21f4564567f0310597e04ce",
            "527f68a59bf1efa36d35541f3645cc7aec28e5b103c22ac06749941090988759",
            "bbbf1311df5797455b47cc3b408ba07884a826f76f242d2fdf7db54629d1a609",
            "effb5602b6930ca1bb5ec56a48f14d7d0be2b438a2150943c9afdc1a970a672e",
            "816276448129c121a9568660a298bdaa390c04c447b5ac9a200f055cd621362f"),
        new(
            true,
            false,
            "remote-pad-grid.x368-y336",
            "4f4b092c376c8c3237438bdb3fcdf918f64fb51fe047dc42e7af26ac95d10e54",
            "0067f36f9ac2aa07702b960d94f8a97623e4d25a7cd0f227b47a1fc8179d68ab",
            "51b677eb4313dccd2f6718ccfa6ea43797ee6110e23b073b362e979cab3eb380",
            "b57816578698433ca07b337b6245da9429ca69fc251c05c896c595043b2aaf30",
            "79bea74469b3703d38f5463f88b0e8ac84b83c1116db310639b815ec5f740810",
            "843310a2affb9b14bddd207b4f954f16dd48153af90b1585eaedaa6b377660e2",
            "eb4d8677a47b4d0ebff5caeb5e2b4ab77a1a2bac18c96f25508a5adaf83afdf9",
            "ffce33eed0335d12fddc2ea29b52da42f5331bbbb33a9c8265096b2f35e8e866"),
        new(
            true,
            false,
            "remote-pad-grid.x384-y336",
            "abe754c6bd8dec891047df3237669a3a62069abb8bd61f088f307bd9a97ee0b8",
            "6a64a4725147b01e8e2581f18251f052fc8c4e0a4562b45b5ea0f165e8fb5466",
            "51b677eb4313dccd2f6718ccfa6ea43797ee6110e23b073b362e979cab3eb380",
            "b57816578698433ca07b337b6245da9429ca69fc251c05c896c595043b2aaf30",
            "79bea74469b3703d38f5463f88b0e8ac84b83c1116db310639b815ec5f740810",
            "843310a2affb9b14bddd207b4f954f16dd48153af90b1585eaedaa6b377660e2",
            "eb4d8677a47b4d0ebff5caeb5e2b4ab77a1a2bac18c96f25508a5adaf83afdf9",
            "c5ada16a0e674f7f3cb556e9865f03738229a3f528aa819fb653258a52d7a5b5"),
        new(
            true,
            false,
            "remote-pad-grid.x400-y336",
            "2394f23db7ee3ba80a50461faadc958a3a67e5f353b2e480a37e3e2106876718",
            "4d3eaacc3bf13079fdd995167f598b34bb11d0f9482a3b3a22a0ca65658006a0",
            "51b677eb4313dccd2f6718ccfa6ea43797ee6110e23b073b362e979cab3eb380",
            "b57816578698433ca07b337b6245da9429ca69fc251c05c896c595043b2aaf30",
            "79bea74469b3703d38f5463f88b0e8ac84b83c1116db310639b815ec5f740810",
            "843310a2affb9b14bddd207b4f954f16dd48153af90b1585eaedaa6b377660e2",
            "eb4d8677a47b4d0ebff5caeb5e2b4ab77a1a2bac18c96f25508a5adaf83afdf9",
            "6b87f2899d42f3be8f8fdec647faa2943e40996e0903a056b4786fbb04002485"),
        new(
            true,
            false,
            "remote-pad-grid.x368-y352",
            "b326700b2a1fff8a28d6750babdb4b845f4c348f5e6408933c3e2571b91971c3",
            "d6f41ad8f4d23b3a84431aa2e70eef053bea037c55b47e737f08e57a64b13475",
            "51b677eb4313dccd2f6718ccfa6ea43797ee6110e23b073b362e979cab3eb380",
            "b57816578698433ca07b337b6245da9429ca69fc251c05c896c595043b2aaf30",
            "79bea74469b3703d38f5463f88b0e8ac84b83c1116db310639b815ec5f740810",
            "843310a2affb9b14bddd207b4f954f16dd48153af90b1585eaedaa6b377660e2",
            "eb4d8677a47b4d0ebff5caeb5e2b4ab77a1a2bac18c96f25508a5adaf83afdf9",
            "234c5ebe555497e9f5e185898ec3d8830348c971c5ea82c50c15dc8ca64625ae"),
        new(
            true,
            false,
            "remote-pad-grid.x384-y352",
            "27eea182402def3d181b3ab8b78984e636865e5f1aeae28f9c651fede4638043",
            "d6e65ae21198c8b518ae8880f159d35725b90ed963a3f6885001adbc23a5aaba",
            "51b677eb4313dccd2f6718ccfa6ea43797ee6110e23b073b362e979cab3eb380",
            "b57816578698433ca07b337b6245da9429ca69fc251c05c896c595043b2aaf30",
            "79bea74469b3703d38f5463f88b0e8ac84b83c1116db310639b815ec5f740810",
            "843310a2affb9b14bddd207b4f954f16dd48153af90b1585eaedaa6b377660e2",
            "eb4d8677a47b4d0ebff5caeb5e2b4ab77a1a2bac18c96f25508a5adaf83afdf9",
            "60503847b1b9fdcac0154d9ad3fde4c0a40e23c58811f0af6b3d8853318b6599"),
        new(
            true,
            false,
            "remote-pad-grid.x400-y352",
            "a0abcd343ddece82fb66bc1bd74c2c351fd49ec2cdc7e7b9d06940452450b56d",
            "3df0190b25657a81abc0a7c00f6057f9bf1fcaa163c63a2162a0608f2da41cff",
            "51b677eb4313dccd2f6718ccfa6ea43797ee6110e23b073b362e979cab3eb380",
            "b57816578698433ca07b337b6245da9429ca69fc251c05c896c595043b2aaf30",
            "79bea74469b3703d38f5463f88b0e8ac84b83c1116db310639b815ec5f740810",
            "843310a2affb9b14bddd207b4f954f16dd48153af90b1585eaedaa6b377660e2",
            "eb4d8677a47b4d0ebff5caeb5e2b4ab77a1a2bac18c96f25508a5adaf83afdf9",
            "8becd4c246601c96434cbeea51a82dbdf9db0626dd67fabb7d8e2b574b576b9b"),
        new(
            true,
            false,
            "remote-pad-grid.x368-y368",
            "0907e425cce46d8e98eadacb1ef2f8d853bdc68d4b92e8246f69c31b0f160727",
            "4893434eb4a298ac53101856ce7376d71c8a50ecbc5f46ba871272a89d2d8aea",
            "51b677eb4313dccd2f6718ccfa6ea43797ee6110e23b073b362e979cab3eb380",
            "b57816578698433ca07b337b6245da9429ca69fc251c05c896c595043b2aaf30",
            "79bea74469b3703d38f5463f88b0e8ac84b83c1116db310639b815ec5f740810",
            "843310a2affb9b14bddd207b4f954f16dd48153af90b1585eaedaa6b377660e2",
            "eb4d8677a47b4d0ebff5caeb5e2b4ab77a1a2bac18c96f25508a5adaf83afdf9",
            "af401eb6f244c62e364838334ff3d048ca4bfef287acb36b94e9b5f52e0dcc1a"),
        new(
            true,
            false,
            "remote-pad-grid.x384-y368",
            "bd65324307a6ff54066fc78241b3a8ab10608f736623bd4a1c14fbaf72220d2d",
            "824746daea2b1dfd36b06bebe54d3f398b7bb8c698855c664e8f67515903d678",
            "51b677eb4313dccd2f6718ccfa6ea43797ee6110e23b073b362e979cab3eb380",
            "b57816578698433ca07b337b6245da9429ca69fc251c05c896c595043b2aaf30",
            "79bea74469b3703d38f5463f88b0e8ac84b83c1116db310639b815ec5f740810",
            "843310a2affb9b14bddd207b4f954f16dd48153af90b1585eaedaa6b377660e2",
            "eb4d8677a47b4d0ebff5caeb5e2b4ab77a1a2bac18c96f25508a5adaf83afdf9",
            "662d86192853ef1341250ff6e4958988236f8c7bdf2dc62c9ac3d5531e13491e"),
        new(
            true,
            false,
            "remote-pad-grid.x400-y368",
            "d6a0971024e9940dd202a3be37cee7958a4cf3153bd4050ffce1fa3192a77a9e",
            "1a0551d525c8d83c643ccc9f64908eb4f6d79b654f6bc07e787745e4ff6afbab",
            "51b677eb4313dccd2f6718ccfa6ea43797ee6110e23b073b362e979cab3eb380",
            "b57816578698433ca07b337b6245da9429ca69fc251c05c896c595043b2aaf30",
            "79bea74469b3703d38f5463f88b0e8ac84b83c1116db310639b815ec5f740810",
            "843310a2affb9b14bddd207b4f954f16dd48153af90b1585eaedaa6b377660e2",
            "eb4d8677a47b4d0ebff5caeb5e2b4ab77a1a2bac18c96f25508a5adaf83afdf9",
            "f03668d7e64ff39034817d40f73ecd96558933c7578e5c1ea8acb90d5b74ee3a")
    ];

    private static readonly ReadOnlyDictionary<
        string,
        Id65AuthoringRemotePadAggregatePlanStateFrozenPin>
        AdditionalStateFrozenPins =
        new(AdditionalStateFrozenPinArray.ToDictionary(
            item => BuildStateKey(
                item.StructuralTerrainPresent,
                item.GrassT107Present,
                item.PositionId),
            StringComparer.Ordinal));

    private const int TargetOverlayWadEntry = 79;
    private const long TargetOverlayWadOffset = 0x6927000;
    private const int TargetOverlayByteLength = 0xF800;
    private const long TargetDataWadOffset = 0x6936800;
    private const int RetailTownSquareOverlayWadEntry = 15;
    private const long RetailTownSquareOverlayWadOffset = 0x118E800;
    private const int RetailTownSquareDataWadEntry = 16;
    private const long RetailTownSquareDataWadOffset = 0x119E000;
    private const int TextureDonorWadEntry = 70;
    private const int GrassDonorWadEntry = 10;

    static Id65AuthoringRemotePadAggregatePlanProjector()
    {
        (bool Terrain, bool Grass)[] additionalStates =
        [
            (false, false),
            (false, true),
            (true, false)
        ];
        string[] expectedAdditionalKeys = additionalStates
            .SelectMany(state => Id65RemotePadGrassT107Catalog.Positions.Select(
                position => BuildStateKey(
                    state.Terrain,
                    state.Grass,
                    position.PositionId)))
            .ToArray();
        if (FrozenPinArray.Length != 9 || FrozenPins.Count != 9 ||
            !FrozenPinArray.Select(item => item.PositionId).SequenceEqual(
                Id65RemotePadGrassT107Catalog.Positions.Select(item => item.PositionId)) ||
            FrozenPinArray.Any(item => new[]
            {
                item.EnvelopeCanonicalSha256,
                item.StaticAuthoringProjectionSha256,
                item.OutputRow80Sha256,
                item.OwnedRangeMapSha256,
                item.InverseRangeMapSha256,
                item.DeterministicAggregatePlanSha256
            }.Any(value => !IsCanonicalSha256(value))) ||
            FrozenPinArray.Select(item => item.EnvelopeCanonicalSha256)
                .Distinct(StringComparer.Ordinal).Count() != 9 ||
            FrozenPinArray.Select(item => item.StaticAuthoringProjectionSha256)
                .Distinct(StringComparer.Ordinal).Count() != 9 ||
            FrozenPinArray.Select(item => item.OutputRow80Sha256)
                .Distinct(StringComparer.Ordinal).Count() != 9 ||
            FrozenPinArray.Select(item => item.DeterministicAggregatePlanSha256)
                .Distinct(StringComparer.Ordinal).Count() != 9 ||
            AdditionalStateFrozenPinArray.Length != 27 ||
            AdditionalStateFrozenPins.Count != 27 ||
            !AdditionalStateFrozenPinArray.Select(item => BuildStateKey(
                    item.StructuralTerrainPresent,
                    item.GrassT107Present,
                    item.PositionId))
                .SequenceEqual(expectedAdditionalKeys) ||
            AdditionalStateFrozenPinArray.Any(item =>
                item.StructuralTerrainPresent && item.GrassT107Present ||
                new[]
                {
                    item.EnvelopeCanonicalSha256,
                    item.StaticAuthoringProjectionSha256,
                    item.OutputRow80Sha256,
                    item.OwnedRangeMapSha256,
                    item.InverseRangeMapSha256,
                    item.ProtectedRangeMapSha256,
                    item.AtomicGroupMapSha256,
                    item.DeterministicAggregatePlanSha256
                }.Any(value => !IsCanonicalSha256(value))) ||
            AdditionalStateFrozenPinArray.Select(item =>
                    item.EnvelopeCanonicalSha256)
                .Distinct(StringComparer.Ordinal).Count() != 27 ||
            AdditionalStateFrozenPinArray.Select(item =>
                    item.StaticAuthoringProjectionSha256)
                .Distinct(StringComparer.Ordinal).Count() != 27 ||
            AdditionalStateFrozenPinArray.Select(item =>
                    item.OutputRow80Sha256)
                .Distinct(StringComparer.Ordinal).Count() != 11 ||
            AdditionalStateFrozenPinArray.Select(item =>
                    item.DeterministicAggregatePlanSha256)
                .Distinct(StringComparer.Ordinal).Count() != 27)
        {
            throw new InvalidDataException(
                "The frozen 36-state schema-3 aggregate-plan contract changed.");
        }
    }

    public static Id65AuthoringRemotePadAggregatePlanFrozenPin GetRequiredFrozenPin(
        string positionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(positionId);
        if (!FrozenPins.TryGetValue(
                positionId,
                out Id65AuthoringRemotePadAggregatePlanFrozenPin? pin))
        {
            throw new KeyNotFoundException(
                "Unknown frozen schema-3 aggregate position " + positionId + ".");
        }
        return pin;
    }

    public static Id65AuthoringRemotePadAggregatePlanStateFrozenPin
        GetRequiredStateFrozenPin(
            bool structuralTerrainPresent,
            bool grassT107Present,
            string positionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(positionId);
        _ = Id65RemotePadGrassT107Catalog.GetRequiredPosition(positionId);
        Id65AuthoringRemotePadAggregatePlanStateFrozenPin? pin =
            TryGetStateFrozenPin(
                structuralTerrainPresent,
                grassT107Present,
                positionId);
        if (pin is null)
        {
            throw new KeyNotFoundException(
                "Unknown frozen schema-3 aggregate state terrain=" +
                structuralTerrainPresent + ", Grass T107=" +
                grassT107Present + ", position=" + positionId + ".");
        }
        return pin;
    }

    private static Id65AuthoringRemotePadAggregatePlanStateFrozenPin?
        TryGetStateFrozenPin(
            bool structuralTerrainPresent,
            bool grassT107Present,
            string positionId)
    {
        if (structuralTerrainPresent && grassT107Present)
        {
            Id65AuthoringRemotePadAggregatePlanFrozenPin current =
                GetRequiredFrozenPin(positionId);
            return new(
                true,
                true,
                positionId,
                current.EnvelopeCanonicalSha256,
                current.StaticAuthoringProjectionSha256,
                current.OutputRow80Sha256,
                current.OwnedRangeMapSha256,
                current.InverseRangeMapSha256,
                ExpectedProtectedRangeMapSha256,
                ExpectedAtomicGroupMapSha256,
                current.DeterministicAggregatePlanSha256);
        }

        AdditionalStateFrozenPins.TryGetValue(
            BuildStateKey(
                structuralTerrainPresent,
                grassT107Present,
                positionId),
            out Id65AuthoringRemotePadAggregatePlanStateFrozenPin? pin);
        return pin;
    }

    private static string BuildStateKey(
        bool structuralTerrainPresent,
        bool grassT107Present,
        string positionId) =>
        (structuralTerrainPresent ? "terrain-on" : "terrain-off") + "|" +
        (grassT107Present ? "grass-on" : "grass-off") + "|" + positionId;

    public static Id65AuthoringRemotePadAggregatePlan Project(
        Id65AuthoringRemotePadAggregatePlanRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.StoredDocument);
        ArgumentNullException.ThrowIfNull(request.LabManifest);
        ArgumentNullException.ThrowIfNull(request.NativeInputs);
        Id65AuthoringEditorNativeInputProvider.ValidateForConsumption(
            request.NativeInputs);

        ValidateExactLabManifest(request.LabManifest);
        Id65AuthoringEditorDocument document =
            ValidateStoredDocument(request.StoredDocument);
        ValidateInputIdentity(request, document);

        Id65CompiledAuthoringRemotePadComposite compiled =
            Id65AuthoringRemotePadCompositeCompiler.Compile(new(
                document,
                request.NativeInputs.LockedSource,
                request.NativeInputs.PrivateTextureWitness,
                request.NativeInputs.GrassBundle));
        ValidateCompiled(document, compiled);

        byte[] source = compiled.CopySourceRow80();
        byte[] output = compiled.CopyOutputRow80();
        Id65AuthoringEditorAggregateOwnedRange[] owned = compiled.OwnedRanges
            .Select(ToAggregateRange)
            .ToArray();
        TransactionPartition partition =
            ValidateAndBuildPartition(
                source,
                output,
                owned,
                compiled.GrassT107Present);
        Id65AuthoringEditorAggregateOwnedRange[] inverse = owned
            .Reverse()
            .Select(item => item.Invert())
            .ToArray();
        ValidateInverse(source, output, inverse, compiled.GrassT107Present);
        Id65AuthoringEditorAggregateAtomicGroup[] atomicGroups =
            BuildAtomicGroups(owned, compiled.GrassT107Present);
        Id65AuthoringEditorAggregateDestinationBinding destination =
            BuildDestinationBinding(request.LabManifest, request.NativeInputs.BoundSource);

        string ownedHash = HashOwnedRanges(owned);
        string inverseHash = HashOwnedRanges(inverse);
        string protectedHash = HashProtectedRanges(partition.ProtectedRanges);
        string atomicHash = HashAtomicGroups(atomicGroups);
        string aggregateHash = Hash(Encoding.UTF8.GetBytes(string.Join('\n',
        [
            ProfileId,
            SchemaVersion.ToString(CultureInfo.InvariantCulture),
            request.StoredDocument.EnvelopeCanonicalSha256,
            request.StoredDocument.StaticAuthoringProjectionSha256,
            request.NativeInputs.CanonicalInputSetSha256,
            document.CanonicalSha256,
            Id65RemotePadGrassT107Catalog.CanonicalSha256,
            compiled.Placement.PositionId,
            compiled.Placement.RowSha256,
            compiled.SourceRow80Sha256,
            compiled.OutputRow80Sha256,
            compiled.OutputModelSha256,
            compiled.TargetOverlaySha256,
            compiled.GlobalExecutableSha256,
            compiled.BaselineOutputRow80Sha256,
            compiled.BaselineDeterministicPlanSha256,
            compiled.ChangedByteCount.ToString(CultureInfo.InvariantCulture),
            compiled.OwnedRangeMapSha256,
            compiled.TransactionSha256,
            compiled.ProtectedComplementSha256,
            compiled.DeterministicPlanSha256,
            ownedHash,
            inverseHash,
            protectedHash,
            partition.ProtectedComplementSha256,
            atomicHash,
            destination.TargetDataWadEntry.ToString(CultureInfo.InvariantCulture),
            destination.TargetDataWadOffset.ToString("X", CultureInfo.InvariantCulture),
            destination.TargetDataDirectoryRowSha256,
            destination.SourceImageSha256,
            destination.RejectedRetailDataWadEntry.ToString(CultureInfo.InvariantCulture),
            destination.RejectedRetailDataWadOffset.ToString("X", CultureInfo.InvariantCulture),
            destination.RejectedRetailDataDirectoryRowSha256
        ])));

        Id65AuthoringRemotePadAggregatePlan plan = new(
            destination,
            document,
            request.StoredDocument.EnvelopeCanonicalSha256,
            request.StoredDocument.StaticAuthoringProjectionSha256,
            request.NativeInputs.CanonicalInputSetSha256,
            compiled.SourceRow80Sha256,
            compiled.OutputRow80Sha256,
            compiled.OutputModelSha256,
            compiled.TargetOverlaySha256,
            compiled.GlobalExecutableSha256,
            compiled.BaselineOutputRow80Sha256,
            compiled.BaselineDeterministicPlanSha256,
            compiled.OwnedRangeMapSha256,
            compiled.TransactionSha256,
            compiled.DeterministicPlanSha256,
            compiled.Placement,
            owned,
            inverse,
            partition.ProtectedRanges,
            atomicGroups,
            partition.GuardedByteCount,
            partition.ProtectedByteCount,
            compiled.ChangedByteCount,
            ownedHash,
            inverseHash,
            protectedHash,
            partition.ProtectedComplementSha256,
            atomicHash,
            aggregateHash);
        ValidatePlan(plan);
        return plan;
    }

    internal static byte[] ApplyExact(
        Id65AuthoringRemotePadAggregatePlan plan,
        byte[] exactRow80,
        bool reverse)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(exactRow80);
        string expectedInputHash =
            reverse ? plan.OutputRow80Sha256 : plan.SourceRow80Sha256;
        string expectedOutputHash =
            reverse ? plan.SourceRow80Sha256 : plan.OutputRow80Sha256;
        if (exactRow80.Length != ExpectedRow80ByteLength ||
            Hash(exactRow80) != expectedInputHash)
        {
            throw new InvalidDataException(
                "The schema-3 aggregate transaction whole-row preimage changed.");
        }
        IReadOnlyList<Id65AuthoringEditorAggregateOwnedRange> ranges = reverse
            ? plan.InverseRanges
            : plan.OwnedRanges;
        byte[] output = ApplyAllRanges(exactRow80, ranges);
        if (Hash(output) != expectedOutputHash)
        {
            throw new InvalidDataException(
                "The schema-3 aggregate transaction whole-row result changed.");
        }
        return output;
    }

    internal static void ValidatePartitionForSmoke(
        byte[] source,
        byte[] output,
        IEnumerable<Id65AuthoringEditorAggregateOwnedRange> ranges)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(ranges);
        Id65AuthoringEditorAggregateOwnedRange[] owned = ranges
            .Select(item => item.DeepCopy())
            .ToArray();
        TransactionPartition partition =
            ValidateAndBuildPartition(
                source,
                output,
                owned,
                HasGrassT107Range(owned));
        ValidateInverse(
            source,
            output,
            owned.Reverse().Select(item => item.Invert()).ToArray(),
            HasGrassT107Range(owned));
        if (partition.GuardedByteCount + partition.ProtectedByteCount !=
            ExpectedRow80ByteLength)
        {
            throw new InvalidDataException(
                "The schema-3 aggregate partition does not cover row 80.");
        }
    }

    private static Id65AuthoringEditorDocument ValidateStoredDocument(
        Id65AuthoringEditorDocumentStoreResult stored)
    {
        Id65AuthoringEditorDocument reparsed =
            Id65AuthoringEditorDocument.Parse(stored.Document.SerializeUtf8());
        if (!reparsed.UsesRemotePadT107Grid ||
            reparsed.SchemaVersion !=
                Id65AuthoringEditorDocument.RemotePadGridSchemaVersion ||
            reparsed.ProfileId !=
                Id65AuthoringEditorDocument.RemotePadGridDocumentProfileId ||
            reparsed.T107RemotePadPositionId is not string positionId ||
            reparsed.MandatoryBaseFaceTextureId !=
                Id65AuthoringEditorDocument.PrivateMandatoryBaseFaceTextureId ||
            reparsed.Terrain.Count > 1 ||
            reparsed.MobyPlacements.Count > 1 ||
            reparsed.MobyPlacements.Any(item =>
                item.Id != Id65RemotePadGrassT107Catalog.PlacementId))
        {
            throw new NotSupportedException(
                "The schema-3 aggregate plan supports only the exact T66 " +
                "remote-pad family with optional terrain, optional T107, and T88 off.");
        }
        bool structuralTerrainPresent = reparsed.Terrain.Count == 1;
        bool grassT107Present = reparsed.MobyPlacements.Count == 1;
        Id65AuthoringEditorDocument exact =
            Id65AuthoringEditorDocument.CreateRemotePadGridExact(
                positionId,
                includeOptionalTerrain: structuralTerrainPresent,
                placementIds: grassT107Present
                    ? [Id65RemotePadGrassT107Catalog.PlacementId]
                    : [],
                mandatoryBaseFaceTextureId:
                    Id65AuthoringEditorDocument.PrivateMandatoryBaseFaceTextureId);
        Id65AuthoringRemotePadAggregatePlanStateFrozenPin frozen =
            GetRequiredStateFrozenPin(
                structuralTerrainPresent,
                grassT107Present,
                positionId);
        if (!reparsed.SerializeUtf8().SequenceEqual(exact.SerializeUtf8()) ||
            stored.DocumentCanonicalSha256 != reparsed.CanonicalSha256 ||
            stored.EnvelopeSchemaVersion !=
                Id65AuthoringEditorDocumentStore.RemotePadGridEnvelopeSchemaVersion ||
            !IsCanonicalSha256(stored.EnvelopeCanonicalSha256) ||
            stored.ProjectionContractId !=
                Id65AuthoringEditorDocumentStore.RemotePadGridProjectionContractId ||
            stored.CompositeProfileId.Length != 0 ||
            stored.CompositeManifestSha256.Length != 0 ||
            stored.CompositeOutputRow80Sha256.Length != 0 ||
            stored.CompositeDeterministicPlanSha256.Length != 0 ||
            stored.StaticAuthoringProfileId !=
                Id65AuthoringEditorDocumentStore.RemotePadGridStaticAuthoringProfileId ||
            stored.StaticAuthoringCatalogSha256 !=
                Id65RemotePadGrassT107Catalog.CanonicalSha256 ||
            stored.StaticAuthoringSceneGraphSha256 !=
                (structuralTerrainPresent
                    ? Id65FreshLevelSceneProjector
                        .ExpectedStructuralGraphCanonicalSha256
                    : Id65FreshLevelSceneProjector.ExpectedCanonicalSha256) ||
            !IsCanonicalSha256(stored.StaticAuthoringProjectionSha256) ||
            stored.EnvelopeCanonicalSha256 != frozen.EnvelopeCanonicalSha256 ||
            stored.StaticAuthoringProjectionSha256 !=
                frozen.StaticAuthoringProjectionSha256 ||
            !stored.HasStaticAuthoringProjection ||
            stored.HasCompiledCompositeProjection ||
            stored.ContainsCompositeAfterimage ||
            stored.ContainsDiscImageBytes || stored.ContainsCueBytes ||
            stored.WritesDiscImage || stored.WritesCue || stored.AppIntegrated ||
            stored.CreateBinEnabled || stored.NormalCreateBinEnabled ||
            stored.RuntimeCandidateAuthorized || stored.ReleaseIntegrated ||
            stored.PromotionAuthorized || stored.Publishable ||
            !stored.RequiresTrustedExclusiveWorkspaceDirectory ||
            stored.HostileFilesystemRaceSafe || stored.CommonWorkspaceLeaseOwned)
        {
            throw new InvalidDataException(
                "The schema-3 aggregate plan requires a genuine exact static store readback.");
        }
        return reparsed;
    }

    private static void ValidateInputIdentity(
        Id65AuthoringRemotePadAggregatePlanRequest request,
        Id65AuthoringEditorDocument document)
    {
        Id65AuthoringEditorAggregateBoundSource bound =
            request.NativeInputs.BoundSource;
        Id65AuthoringCompositeLockedSource source =
            request.NativeInputs.LockedSource;
        if (bound.SnapshotProfileId != Id65RewardCensusContract.ProfileId ||
            bound.SourceImageSha256 != request.LabManifest.LockedBaseImageSha256 ||
            bound.TargetDataWadEntry != 80 ||
            bound.TargetDataWadOffset != TargetDataWadOffset ||
            bound.RejectedRetailDataWadEntry != 16 ||
            bound.RejectedRetailDataWadOffset != RetailTownSquareDataWadOffset ||
            !bound.PhysicalDirectorySnapshotVerified ||
            !bound.RetailCloneComparedAndExcluded ||
            bound.CurrentOnDiskFreshnessVerified ||
            request.NativeInputs.CanonicalInputSetSha256 !=
                Id65AuthoringEditorNativeInputProvider.ExpectedCanonicalInputSetSha256 ||
            source.Row80ByteLength != ExpectedRow80ByteLength ||
            source.TargetOverlayByteLength != TargetOverlayByteLength ||
            source.GlobalExecutableByteLength != 0x66000 ||
            source.Row80Sha256 !=
                Id65AuthoringCompositeCompiler.ExpectedLockedRow80Sha256 ||
            source.TargetOverlaySha256 !=
                Id65AuthoringCompositeCompiler.ExpectedTargetOverlaySha256 ||
            source.GlobalExecutableSha256 !=
                Id65AuthoringCompositeCompiler.ExpectedGlobalExecutableSha256 ||
            request.NativeInputs.PrivateTextureWitnessSha256 !=
                Id65V2NativeTextureCompositionCompiler.ExpectedWitnessSha256 ||
            request.NativeInputs.GrassBundleSha256 !=
                Id65RemotePadGrassT107Catalog.BundleCanonicalSha256 ||
            document.PrivateTextureWitnessSha256 !=
                request.NativeInputs.PrivateTextureWitnessSha256 ||
            document.MobyBundleSha256 != request.NativeInputs.GrassBundleSha256)
        {
            throw new InvalidDataException(
                "The schema-3 aggregate source, T66 witness, or Grass bundle changed.");
        }
    }

    private static void ValidateCompiled(
        Id65AuthoringEditorDocument document,
        Id65CompiledAuthoringRemotePadComposite compiled)
    {
        bool structuralTerrainPresent = document.Terrain.Count == 1;
        bool grassT107Present = document.MobyPlacements.Count == 1;
        Id65AuthoringRemotePadCompositeStateFrozenPin frozen =
            Id65AuthoringRemotePadCompositeCompiler.GetRequiredStateFrozenPin(
                structuralTerrainPresent,
                grassT107Present,
                compiled.Placement.PositionId);
        int expectedOwnedRangeCount = grassT107Present
            ? ExpectedOwnedRangeCount
            : ExpectedOwnedRangeCountWithoutT107;
        if (!compiled.Document.SerializeUtf8().SequenceEqual(document.SerializeUtf8()) ||
            compiled.ProfileId != Id65AuthoringRemotePadCompositeCompiler.ProfileId ||
            compiled.SchemaVersion != Id65AuthoringRemotePadCompositeCompiler.SchemaVersion ||
            compiled.StructuralTerrainPresent != structuralTerrainPresent ||
            compiled.GrassT107Present != grassT107Present ||
            compiled.Placement.Present != grassT107Present ||
            compiled.SourceRow80Sha256 !=
                Id65AuthoringCompositeCompiler.ExpectedLockedRow80Sha256 ||
            compiled.OutputModelSha256 != frozen.OutputModelSha256 ||
            compiled.BaselineOutputRow80Sha256 !=
                frozen.BaselineOutputRow80Sha256 ||
            compiled.BaselineDeterministicPlanSha256 !=
                frozen.BaselineDeterministicPlanSha256 ||
            compiled.ChangedByteCount != frozen.ChangedByteCount ||
            compiled.ProtectedComplementSha256 !=
                frozen.ProtectedComplementSha256 ||
            compiled.OwnedRanges.Count != expectedOwnedRangeCount ||
            compiled.InverseRanges.Count != expectedOwnedRangeCount ||
            !compiled.BuildsFromLockedSourceThroughFrozenLegacyBaseline ||
            !compiled.PlacementDerivedFromCanonicalDocumentAndCatalog ||
            !compiled.AppliesOneNonOverlappingLedger ||
            !compiled.ExactInverseVerified ||
            compiled.AcceptsCallerCompiledAfterimages || compiled.AcceptsAggregatePlans ||
            compiled.AcceptsMaterializedCandidates || compiled.WritesFileSystem ||
            compiled.WritesDiscImage || compiled.WritesCue || compiled.AppIntegrated ||
            compiled.UiIntegrated || compiled.CreateBinEnabled ||
            compiled.NormalCreateBinEnabled || compiled.RuntimeCandidateAuthorized ||
            compiled.RuntimeProven || compiled.ReleaseIntegrated ||
            compiled.PromotionAuthorized || compiled.Publishable)
        {
            throw new InvalidDataException(
                "The schema-3 aggregate compiler result or authority boundary changed.");
        }
    }

    private static Id65AuthoringEditorAggregateOwnedRange ToAggregateRange(
        Id65AuthoringCompositeOwnedRange range)
    {
        Id65AuthoringEditorAggregateRangeDomain domain = range.StableId switch
        {
            _ when range.StableId.StartsWith("texture.page.", StringComparison.Ordinal) =>
                Id65AuthoringEditorAggregateRangeDomain.NativeTexturePages,
            "model.final" => Id65AuthoringEditorAggregateRangeDomain.CompositeModel,
            _ when range.OwnerId == "composite.v3-spawn" =>
                Id65AuthoringEditorAggregateRangeDomain.Spawn,
            _ => Id65AuthoringEditorAggregateRangeDomain.Grass
        };
        return new(
            range.StableId,
            range.OwnerId,
            domain,
            range.DataRelativeOffset,
            range.CopyBefore(),
            range.CopyAfter());
    }

    private static TransactionPartition ValidateAndBuildPartition(
        byte[] source,
        byte[] output,
        IReadOnlyList<Id65AuthoringEditorAggregateOwnedRange> owned,
        bool grassT107Present)
    {
        int expectedOwnedRangeCount = grassT107Present
            ? ExpectedOwnedRangeCount
            : ExpectedOwnedRangeCountWithoutT107;
        int expectedGuardedByteCount = grassT107Present
            ? ExpectedGuardedByteCount
            : ExpectedGuardedByteCountWithoutT107;
        int expectedProtectedByteCount = grassT107Present
            ? ExpectedProtectedByteCount
            : ExpectedProtectedByteCountWithoutT107;
        int expectedProtectedRangeCount = grassT107Present
            ? ExpectedProtectedRangeCount
            : ExpectedProtectedRangeCountWithoutT107;
        if (source.Length != ExpectedRow80ByteLength ||
            output.Length != ExpectedRow80ByteLength ||
            owned.Count != expectedOwnedRangeCount ||
            HasGrassT107Range(owned) != grassT107Present ||
            owned.Select(item => item.StableId)
                .Distinct(StringComparer.Ordinal).Count() != owned.Count)
        {
            throw new InvalidDataException(
                "The schema-3 aggregate transaction dimensions changed.");
        }

        List<Id65AuthoringEditorAggregateProtectedRange> protectedRanges = [];
        int cursor = 0;
        int guarded = 0;
        for (int index = 0; index < owned.Count; index++)
        {
            Id65AuthoringEditorAggregateOwnedRange range = owned[index];
            byte[] before = range.CopyBefore();
            byte[] after = range.CopyAfter();
            if (range.TargetWadEntry != 80 ||
                range.WadOffset != TargetDataWadOffset + range.DataRelativeOffset ||
                range.DataRelativeOffset < cursor || range.ByteLength <= 0 ||
                range.DataRelativeOffset + (long)range.ByteLength > source.Length ||
                before.Length != range.ByteLength || after.Length != range.ByteLength ||
                Hash(before) != range.BeforeSha256 || Hash(after) != range.AfterSha256 ||
                !source.AsSpan(range.DataRelativeOffset, range.ByteLength)
                    .SequenceEqual(before) ||
                !output.AsSpan(range.DataRelativeOffset, range.ByteLength)
                    .SequenceEqual(after) ||
                before.SequenceEqual(after))
            {
                throw new InvalidDataException(
                    "Schema-3 aggregate range " + range.StableId +
                    " lost destination, order, hash, preimage, or afterimage identity.");
            }
            if (cursor < range.DataRelativeOffset)
            {
                int length = range.DataRelativeOffset - cursor;
                if (!source.AsSpan(cursor, length).SequenceEqual(
                        output.AsSpan(cursor, length)))
                {
                    throw new InvalidDataException(
                        "The schema-3 aggregate changed a protected gap.");
                }
                protectedRanges.Add(new(cursor, length, Hash(source.AsSpan(cursor, length))));
            }
            cursor = checked(range.DataRelativeOffset + range.ByteLength);
            guarded = checked(guarded + range.ByteLength);
        }
        if (cursor < source.Length)
        {
            int length = source.Length - cursor;
            if (!source.AsSpan(cursor, length).SequenceEqual(output.AsSpan(cursor, length)))
            {
                throw new InvalidDataException(
                    "The schema-3 aggregate changed its protected tail.");
            }
            protectedRanges.Add(new(cursor, length, Hash(source.AsSpan(cursor, length))));
        }
        byte[] rebuilt = ApplyAllRanges(source, owned);
        if (!rebuilt.SequenceEqual(output))
        {
            throw new InvalidDataException(
                "The schema-3 aggregate owned ranges do not reproduce the compiler output.");
        }
        int protectedBytes = source.Length - guarded;
        if (guarded != expectedGuardedByteCount ||
            protectedBytes != expectedProtectedByteCount ||
            protectedRanges.Count != expectedProtectedRangeCount)
        {
            throw new InvalidDataException(
                "The schema-3 aggregate guarded/protected partition changed.");
        }
        return new(
            protectedRanges.AsReadOnly(),
            guarded,
            protectedBytes,
            HashProtectedBytes(source, owned));
    }

    private static void ValidateInverse(
        byte[] source,
        byte[] output,
        IReadOnlyList<Id65AuthoringEditorAggregateOwnedRange> inverse,
        bool grassT107Present)
    {
        int expectedOwnedRangeCount = grassT107Present
            ? ExpectedOwnedRangeCount
            : ExpectedOwnedRangeCountWithoutT107;
        if (inverse.Count != expectedOwnedRangeCount ||
            !ApplyAllRanges(output, inverse).SequenceEqual(source))
        {
            throw new InvalidDataException(
                "The schema-3 aggregate inverse transaction changed.");
        }
    }

    private static byte[] ApplyAllRanges(
        byte[] input,
        IReadOnlyList<Id65AuthoringEditorAggregateOwnedRange> ranges)
    {
        byte[] output = input.ToArray();
        foreach (Id65AuthoringEditorAggregateOwnedRange range in ranges)
        {
            byte[] before = range.CopyBefore();
            if (!output.AsSpan(range.DataRelativeOffset, range.ByteLength)
                .SequenceEqual(before))
            {
                throw new InvalidDataException(
                    "Schema-3 aggregate range " + range.StableId +
                    " lost its exact transactional preimage.");
            }
            range.CopyAfter().CopyTo(output, range.DataRelativeOffset);
        }
        return output;
    }

    private static Id65AuthoringEditorAggregateAtomicGroup[] BuildAtomicGroups(
        IReadOnlyList<Id65AuthoringEditorAggregateOwnedRange> owned,
        bool grassT107Present)
    {
        Id65AuthoringEditorAggregateAtomicGroup Create(
            string id,
            Func<Id65AuthoringEditorAggregateOwnedRange, bool> predicate)
        {
            string[] stableIds = owned.Where(predicate)
                .Select(item => item.StableId)
                .ToArray();
            return new(
                id,
                stableIds.Length,
                Hash(Encoding.UTF8.GetBytes(string.Join('\n', stableIds))),
                AllRangesRequired: true,
                IndependentlyApplicable: false);
        }
        Id65AuthoringEditorAggregateAtomicGroup[] groups =
        [
            Create("v3-terrain-private-t66", item => item.Domain is
                Id65AuthoringEditorAggregateRangeDomain.NativeTexturePages or
                Id65AuthoringEditorAggregateRangeDomain.CompositeModel),
            Create("landing-t92", item =>
                item.Domain == Id65AuthoringEditorAggregateRangeDomain.Spawn),
            Create("artisans-grass-remote-pad-t107", item =>
                item.Domain == Id65AuthoringEditorAggregateRangeDomain.Grass)
        ];
        if (!groups.Select(item => item.OwnedRangeCount)
                .SequenceEqual([
                    7_950,
                    2,
                    grassT107Present
                        ? ExpectedGrassRangeCount
                        : ExpectedGrassRangeCountWithoutT107
                ]) ||
            groups.Any(item => !item.AllRangesRequired || item.IndependentlyApplicable))
        {
            throw new InvalidDataException(
                "The schema-3 aggregate atomic-group partition changed.");
        }
        return groups;
    }

    private static bool HasGrassT107Range(
        IReadOnlyList<Id65AuthoringEditorAggregateOwnedRange> owned) =>
        owned.Any(item => item.StableId == "placement.remote-pad.t107");

    private static Id65AuthoringEditorAggregateDestinationBinding BuildDestinationBinding(
        UnusedLevel65BlankLevelLabManifest manifest,
        Id65AuthoringEditorAggregateBoundSource bound)
    {
        Id65AuthoringEditorAggregateDestinationBinding result = new(
            bound.SnapshotProfileId,
            bound.SourceImageSha256,
            manifest.LockedBaseProfileId,
            manifest.LockedBaseImageSha256,
            "WAD.WAD",
            TargetOverlayWadEntry,
            TargetOverlayWadOffset,
            TargetOverlayByteLength,
            manifest.DataWadEntry,
            TargetDataWadOffset,
            ExpectedRow80ByteLength,
            bound.TargetDataDirectoryRowSha256,
            manifest.ObjectTableWadOffset,
            manifest.ObjectTableRelativeOffset,
            RetailTownSquareOverlayWadEntry,
            RetailTownSquareOverlayWadOffset,
            RetailTownSquareDataWadEntry,
            RetailTownSquareDataWadOffset,
            bound.RejectedRetailDataDirectoryRowSha256,
            TextureDonorWadEntry,
            GrassDonorWadEntry,
            PhysicalSnapshotVerified: true,
            RetailTownSquareDestinationRejected: true,
            DonorLocationsReadOnlyProvenance: true,
            PhysicalDestinationRevalidationRequired: true);
        if (result.SourceSnapshotProfileId != Id65RewardCensusContract.ProfileId ||
            result.SourceImageSha256 != manifest.LockedBaseImageSha256 ||
            result.TargetDataWadEntry != 80 ||
            result.TargetDataWadOffset != TargetDataWadOffset ||
            result.TargetDataByteLength != ExpectedRow80ByteLength ||
            result.TargetDataDirectoryRowSha256 !=
                "3c6ae9564c3f6fc794bbae9670ee0dff7bd9a7bdc0f96c3c206c752551d8c09a" ||
            result.TargetObjectTableWadOffset -
                result.TargetObjectTableRelativeOffset != TargetDataWadOffset ||
            result.RejectedRetailDataWadEntry != 16 ||
            result.RejectedRetailDataWadOffset != RetailTownSquareDataWadOffset ||
            result.RejectedRetailDataDirectoryRowSha256 !=
                "ba8329f16f9caed1bed39c6b794968ff95160adbfd2480c5d4bbb6fc7545f77b" ||
            result.TargetDataWadOffset == result.RejectedRetailDataWadOffset ||
            !result.PhysicalSnapshotVerified ||
            !result.RetailTownSquareDestinationRejected ||
            !result.DonorLocationsReadOnlyProvenance ||
            !result.PhysicalDestinationRevalidationRequired)
        {
            throw new InvalidDataException(
                "The schema-3 aggregate physical destination binding changed.");
        }
        return result;
    }

    private static void ValidateExactLabManifest(
        UnusedLevel65BlankLevelLabManifest manifest)
    {
        if (manifest.ManifestSchemaVersion !=
                UnusedLevel65BlankLevelLabProfileRegistry.ManifestSchemaVersion ||
            manifest.ProfileId != UnusedLevel65BlankLevelLabProfileRegistry.ProfileId ||
            manifest.ProfileVersion !=
                UnusedLevel65BlankLevelLabProfileRegistry.ProfileVersion ||
            manifest.WorkspaceKey != UnusedLevel65BlankLevelLabProfileRegistry.Key ||
            manifest.WorkspaceLayoutVersion !=
                UnusedLevel65BlankLevelLabProfileRegistry.WorkspaceLayoutVersion ||
            manifest.WorkspaceLayoutRelativeRoot !=
                UnusedLevel65BlankLevelLabProfileRegistry.WorkspaceLayoutRelativeRoot ||
            manifest.DisplayName !=
                UnusedLevel65BlankLevelLabProfileRegistry.Profile.DisplayName ||
            manifest.LevelId != 65 || manifest.DataWadEntry != 80 ||
            manifest.ObjectTableWadOffset !=
                UnusedLevel65BlankLevelLabProfileRegistry.ObjectTableWadOffset ||
            manifest.ObjectTableRelativeOffset !=
                UnusedLevel65BlankLevelLabProfileRegistry.ObjectTableRelativeOffset ||
            manifest.ObjectRecordCount !=
                UnusedLevel65BlankLevelLabProfileRegistry.ObjectRecordCount ||
            manifest.LockedBaseImageRelativePath !=
                "locked-base/unusedlevel65blank-locked-base-v1.bin" ||
            manifest.LockedBaseCueRelativePath !=
                "locked-base/unusedlevel65blank-locked-base-v1.cue" ||
            manifest.AuthoredEditsRelativePath != "authored-edits" ||
            manifest.CleanSourceImageSha256 !=
                UnusedLevel65BlankLevelLabProfileRegistry.CleanUsaImageSha256 ||
            manifest.PhysicalCloneProfileId !=
                UnusedLevel65BlankLevelLabProfileRegistry.Profile.PhysicalCloneProfileId ||
            manifest.PhysicalCloneImageSha256 !=
                UnusedLevel65BlankLevelLabProfileRegistry.Profile.PhysicalCloneImageSha256 ||
            manifest.PhysicalCloneEvidence !=
                UnusedLevel65BlankLevelLabProfileRegistry.PhysicalCloneEvidence ||
            manifest.LockedBaseProfileId !=
                UnusedLevel65BlankLevelLabProfileRegistry.Profile.LockedBaseProfileId ||
            manifest.LockedBaseImageSha256 !=
                UnusedLevel65BlankLevelLabProfileRegistry.Profile.LockedBaseImageSha256 ||
            manifest.LockedBaseEvidence !=
                UnusedLevel65BlankLevelLabProfileRegistry.LockedBaseEvidence ||
            manifest.Capabilities !=
                UnusedLevel65BlankLevelLabProfileRegistry.Capabilities ||
            !manifest.ExternalGameDataRequired || manifest.GameDataEmbedded ||
            manifest.PromotionAuthorized || manifest.NormalCreateBinEnabled)
        {
            throw new InvalidDataException(
                "The schema-3 aggregate lab manifest is not the exact WAD80 workspace.");
        }
    }

    private static void ValidatePlan(Id65AuthoringRemotePadAggregatePlan plan)
    {
        bool structuralTerrainPresent = plan.Document.Terrain.Count == 1;
        bool grassT107Present = plan.Document.MobyPlacements.Count == 1;
        Id65AuthoringRemotePadCompositeStateFrozenPin compilerFrozen =
            Id65AuthoringRemotePadCompositeCompiler.GetRequiredStateFrozenPin(
                structuralTerrainPresent,
                grassT107Present,
                plan.Placement.PositionId);
        Id65AuthoringRemotePadAggregatePlanStateFrozenPin frozen =
            GetRequiredStateFrozenPin(
                structuralTerrainPresent,
                grassT107Present,
                plan.Placement.PositionId);
        int expectedOwnedRangeCount = grassT107Present
            ? ExpectedOwnedRangeCount
            : ExpectedOwnedRangeCountWithoutT107;
        int expectedGrassRangeCount = grassT107Present
            ? ExpectedGrassRangeCount
            : ExpectedGrassRangeCountWithoutT107;
        int expectedGuardedByteCount = grassT107Present
            ? ExpectedGuardedByteCount
            : ExpectedGuardedByteCountWithoutT107;
        int expectedProtectedByteCount = grassT107Present
            ? ExpectedProtectedByteCount
            : ExpectedProtectedByteCountWithoutT107;
        int expectedProtectedRangeCount = grassT107Present
            ? ExpectedProtectedRangeCount
            : ExpectedProtectedRangeCountWithoutT107;
        if (plan.ProfileId != ProfileId || plan.SchemaVersion != SchemaVersion ||
            plan.LevelId != 65 || plan.DataWadEntry != 80 ||
            plan.StructuralTerrainPresent != structuralTerrainPresent ||
            plan.GrassT107Present != grassT107Present ||
            plan.Placement.Present != grassT107Present ||
            plan.CatalogCanonicalSha256 !=
                Id65RemotePadGrassT107Catalog.ExpectedCanonicalSha256 ||
            plan.SourceRow80Sha256 !=
                Id65AuthoringCompositeCompiler.ExpectedLockedRow80Sha256 ||
            plan.OutputModelSha256 != compilerFrozen.OutputModelSha256 ||
            plan.BaselineOutputRow80Sha256 !=
                compilerFrozen.BaselineOutputRow80Sha256 ||
            plan.BaselineDeterministicPlanSha256 !=
                compilerFrozen.BaselineDeterministicPlanSha256 ||
            plan.NativeInputSetSha256 !=
                Id65AuthoringEditorNativeInputProvider.ExpectedCanonicalInputSetSha256 ||
            plan.OwnedRangeCount != expectedOwnedRangeCount ||
            plan.InverseRangeCount != expectedOwnedRangeCount ||
            plan.ProtectedRangeCount != expectedProtectedRangeCount ||
            plan.OwnedRanges.Count(item => item.Domain ==
                Id65AuthoringEditorAggregateRangeDomain.NativeTexturePages) !=
                    ExpectedTexturePageRangeCount ||
            plan.OwnedRanges.Count(item => item.Domain ==
                Id65AuthoringEditorAggregateRangeDomain.CompositeModel) != 1 ||
            plan.OwnedRanges.Count(item => item.Domain ==
                Id65AuthoringEditorAggregateRangeDomain.Spawn) != 2 ||
            plan.OwnedRanges.Count(item => item.Domain ==
                Id65AuthoringEditorAggregateRangeDomain.Grass) !=
                    expectedGrassRangeCount ||
            plan.AtomicGroups.Count != 3 ||
            plan.GuardedByteCount != expectedGuardedByteCount ||
            plan.ProtectedByteCount != expectedProtectedByteCount ||
            plan.GuardedByteCount + plan.ProtectedByteCount != ExpectedRow80ByteLength ||
            plan.ChangedByteCount != compilerFrozen.ChangedByteCount ||
            plan.EnvelopeCanonicalSha256 != frozen.EnvelopeCanonicalSha256 ||
            plan.StaticAuthoringProjectionSha256 !=
                frozen.StaticAuthoringProjectionSha256 ||
            plan.OutputRow80Sha256 != frozen.OutputRow80Sha256 ||
            plan.OwnedRangeMapSha256 != frozen.OwnedRangeMapSha256 ||
            plan.InverseRangeMapSha256 != frozen.InverseRangeMapSha256 ||
            plan.ProtectedRangeMapSha256 != frozen.ProtectedRangeMapSha256 ||
            plan.AtomicGroupMapSha256 != frozen.AtomicGroupMapSha256 ||
            plan.DeterministicAggregatePlanSha256 !=
                frozen.DeterministicAggregatePlanSha256 ||
            plan.ProtectedComplementSha256 !=
                compilerFrozen.ProtectedComplementSha256 ||
            plan.CompilerOwnedRangeMapSha256 !=
                compilerFrozen.OwnedRangeMapSha256 ||
            plan.CompilerTransactionSha256 !=
                compilerFrozen.TransactionSha256 ||
            plan.CompilerDeterministicPlanSha256 !=
                compilerFrozen.DeterministicPlanSha256 ||
            !plan.FullExactSchema3DocumentRequired ||
            !plan.FrozenStoreEnvelopeIdentityMatched ||
            !plan.FrozenStaticAuthoringProjectionIdentityMatched ||
            plan.StoreEnvelopeIdentityIndependentlyVerified ||
            plan.StaticAuthoringProjectionRecomputedFromStoreBytes ||
            !plan.NativeInputGenerationReverified ||
            !plan.ExactPhysicalDestinationBindingPinned ||
            !plan.PhysicalDestinationReadbackVerified ||
            !plan.T66StructuralTerrainBindingVerified ||
            !plan.RepairedT13995SupportBindingVerified ||
            !plan.RemotePadCatalogBindingVerified ||
            !plan.GrassT107StaticBindingVerified ||
            !plan.GrassT88ExcludedVerified ||
            !plan.SpawnAndT92ExactBindingVerified ||
            !plan.ProtectedComplementVerified || !plan.ExactInverseVerified ||
            !plan.OneAtomicTransactionRequired ||
            !plan.ContainsOwnedAfterimageBytes ||
            plan.ContainsCompleteCompositeAfterimage ||
            plan.ContainsDiscImageBytes || plan.ContainsCueBytes ||
            plan.PartialRangeApplicationAuthorized ||
            plan.CrossSliceCompositionSupported ||
            plan.AfterimageStackingAuthorized || plan.GenericExporterCompatible ||
            plan.RetailWriterCompatible || plan.RetailLevelMutationAuthorized ||
            plan.PersistenceEnabled || plan.WriterAuthorized || plan.WritesFileSystem ||
            plan.WritesDiscImage || plan.WritesBin || plan.WritesCue ||
            plan.AppIntegrated || plan.UiIntegrated || plan.CreateBinEnabled ||
            plan.NormalCreateBinEnabled || plan.RuntimeCandidateAuthorized ||
            plan.RuntimeProven || plan.SaveAuthorized || plan.SavePersistenceProven ||
            plan.TreasureTotal200Proven || plan.MusicLongPlayProven ||
            plan.ExitDestination65Proven || plan.ReleaseIntegrated ||
            plan.ReleaseAuthorized || plan.PromotionAuthorized || plan.Publishable ||
            plan.CurrentOnDiskFreshnessVerified ||
            !plan.RequiresTrustedExclusiveWorkspaceDirectory ||
            plan.HostileFilesystemRaceSafe || plan.CommonWorkspaceLeaseOwned)
        {
            throw new InvalidDataException(
                "The schema-3 aggregate plan identity or authority boundary changed.");
        }
    }

    private static string HashOwnedRanges(
        IEnumerable<Id65AuthoringEditorAggregateOwnedRange> ranges) =>
        Hash(Encoding.UTF8.GetBytes(string.Join('\n', ranges.Select(item =>
            $"{item.StableId}|{item.OwnerId}|{item.DataRelativeOffset:X8}|" +
            $"{item.ByteLength:X8}|{item.BeforeSha256}|{item.AfterSha256}"))));

    private static string HashProtectedRanges(
        IEnumerable<Id65AuthoringEditorAggregateProtectedRange> ranges) =>
        Hash(Encoding.UTF8.GetBytes(string.Join('\n', ranges.Select(item =>
            $"{item.DataRelativeOffset:X8}|{item.ByteLength:X8}|{item.SegmentSha256}"))));

    private static string HashAtomicGroups(
        IEnumerable<Id65AuthoringEditorAggregateAtomicGroup> groups) =>
        Hash(Encoding.UTF8.GetBytes(string.Join('\n', groups.Select(item =>
            $"{item.GroupId}|{item.OwnedRangeCount}|{item.StableIdSetSha256}|" +
            $"{item.AllRangesRequired}|{item.IndependentlyApplicable}"))));

    private static string HashProtectedBytes(
        byte[] source,
        IReadOnlyList<Id65AuthoringEditorAggregateOwnedRange> ranges)
    {
        using IncrementalHash hash =
            IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        int cursor = 0;
        foreach (Id65AuthoringEditorAggregateOwnedRange range in ranges)
        {
            if (cursor < range.DataRelativeOffset)
            {
                hash.AppendData(source.AsSpan(
                    cursor,
                    range.DataRelativeOffset - cursor));
            }
            cursor = checked(range.DataRelativeOffset + range.ByteLength);
        }
        if (cursor < source.Length)
            hash.AppendData(source.AsSpan(cursor));
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    internal static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static bool IsCanonicalSha256(string value) =>
        value.Length == 64 && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private sealed record TransactionPartition(
        IReadOnlyList<Id65AuthoringEditorAggregateProtectedRange> ProtectedRanges,
        int GuardedByteCount,
        int ProtectedByteCount,
        string ProtectedComplementSha256);
}
