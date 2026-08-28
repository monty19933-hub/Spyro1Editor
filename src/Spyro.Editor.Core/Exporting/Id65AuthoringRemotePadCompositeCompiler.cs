using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Spyro.Editor.Core.Exporting;

internal sealed record Id65AuthoringRemotePadCompositeCompileRequest(
    Id65AuthoringEditorDocument Document,
    Id65AuthoringCompositeLockedSource Source,
    Id65V2NativeTextureWitness TextureWitness,
    Id65MobyDependencyBundleDescriptor GrassBundle,
    Id65AuthoringCompositeCompilerLimits? Limits = null);

internal sealed record Id65AuthoringRemotePadCompositePlacementReadback(
    string PositionId,
    int PositionOrdinal,
    int EditorX,
    int EditorY,
    int EditorZ,
    int RawX,
    int RawY,
    int RawZ,
    int TargetTrueIndex,
    ushort ActorId,
    byte YawByte,
    string SupportHandle,
    int SupportCollisionTrueIndex,
    string RowSha256,
    bool Present);

internal sealed record Id65AuthoringRemotePadCompositeFrozenPin(
    string PositionId,
    string OutputRow80Sha256,
    string OwnedRangeMapSha256,
    string TransactionSha256,
    string DeterministicPlanSha256);

internal sealed record Id65AuthoringRemotePadCompositeStateFrozenPin(
    bool StructuralTerrainPresent,
    bool GrassT107Present,
    string PositionId,
    string BaselineOutputRow80Sha256,
    string BaselineDeterministicPlanSha256,
    string OutputRow80Sha256,
    string OutputModelSha256,
    int OwnedRangeCount,
    int GrassOwnedRangeCount,
    int ChangedByteCount,
    string ProtectedComplementSha256,
    string OwnedRangeMapSha256,
    string TransactionSha256,
    string DeterministicPlanSha256);

internal sealed class Id65CompiledAuthoringRemotePadComposite
{
    private readonly byte[] _sourceRow80;
    private readonly byte[] _outputRow80;
    private readonly ReadOnlyCollection<Id65AuthoringCompositeOwnedRange> _ownedRanges;
    private readonly ReadOnlyCollection<Id65AuthoringCompositeOwnedRange> _inverseRanges;

    internal Id65CompiledAuthoringRemotePadComposite(
        Id65AuthoringEditorDocument document,
        byte[] sourceRow80,
        byte[] outputRow80,
        IEnumerable<Id65AuthoringCompositeOwnedRange> ownedRanges,
        IEnumerable<Id65AuthoringCompositeOwnedRange> inverseRanges,
        Id65AuthoringRemotePadCompositePlacementReadback placement,
        string baselineOutputRow80Sha256,
        string baselineDeterministicPlanSha256,
        int changedByteCount,
        string ownedRangeMapSha256,
        string transactionSha256,
        string protectedComplementSha256,
        string deterministicPlanSha256,
        string targetOverlaySha256,
        string globalExecutableSha256)
    {
        Document = document;
        DocumentCanonicalSha256 = document.CanonicalSha256;
        StructuralTerrainPresent = document.Terrain.Count == 1;
        GrassT107Present = document.MobyPlacements.Any(item =>
            item.TargetTrueIndex == Id65RemotePadGrassT107Catalog.TargetTrueIndex);
        _sourceRow80 = sourceRow80.ToArray();
        _outputRow80 = outputRow80.ToArray();
        _ownedRanges = Array.AsReadOnly(
            ownedRanges.Select(item => item.DeepCopy()).ToArray());
        _inverseRanges = Array.AsReadOnly(
            inverseRanges.Select(item => item.DeepCopy()).ToArray());
        Placement = placement;
        SourceRow80Sha256 = Id65AuthoringRemotePadCompositeCompiler.Hash(_sourceRow80);
        OutputRow80Sha256 = Id65AuthoringRemotePadCompositeCompiler.Hash(_outputRow80);
        OutputModelSha256 = Id65AuthoringRemotePadCompositeCompiler.Hash(
            _outputRow80.AsSpan(
                Id65AuthoringRemotePadCompositeCompiler.ModelOffset,
                Id65AuthoringRemotePadCompositeCompiler.ModelLength));
        BaselineOutputRow80Sha256 = baselineOutputRow80Sha256;
        BaselineDeterministicPlanSha256 = baselineDeterministicPlanSha256;
        TargetOverlaySha256 = targetOverlaySha256;
        GlobalExecutableSha256 = globalExecutableSha256;
        ChangedByteCount = changedByteCount;
        OwnedRangeMapSha256 = ownedRangeMapSha256;
        TransactionSha256 = transactionSha256;
        ProtectedComplementSha256 = protectedComplementSha256;
        DeterministicPlanSha256 = deterministicPlanSha256;
    }

    public string ProfileId => Id65AuthoringRemotePadCompositeCompiler.ProfileId;
    public int SchemaVersion => Id65AuthoringRemotePadCompositeCompiler.SchemaVersion;
    public Id65AuthoringEditorDocument Document { get; }
    public string DocumentCanonicalSha256 { get; }
    public bool StructuralTerrainPresent { get; }
    public bool GrassT107Present { get; }
    public string CatalogCanonicalSha256 => Id65RemotePadGrassT107Catalog.CanonicalSha256;
    public string SourceRow80Sha256 { get; }
    public string OutputRow80Sha256 { get; }
    public string OutputModelSha256 { get; }
    public string BaselineOutputRow80Sha256 { get; }
    public string BaselineDeterministicPlanSha256 { get; }
    public string TargetOverlaySha256 { get; }
    public string GlobalExecutableSha256 { get; }
    public IReadOnlyList<Id65AuthoringCompositeOwnedRange> OwnedRanges => _ownedRanges;
    public IReadOnlyList<Id65AuthoringCompositeOwnedRange> InverseRanges => _inverseRanges;
    public Id65AuthoringRemotePadCompositePlacementReadback Placement { get; }
    public int ChangedByteCount { get; }
    public string OwnedRangeMapSha256 { get; }
    public string TransactionSha256 { get; }
    public string ProtectedComplementSha256 { get; }
    public string DeterministicPlanSha256 { get; }

    public bool BuildsFromLockedSourceThroughFrozenLegacyBaseline => true;
    public bool PlacementDerivedFromCanonicalDocumentAndCatalog => true;
    public bool AcceptsCallerCompiledAfterimages => false;
    public bool AcceptsAggregatePlans => false;
    public bool AcceptsMaterializedCandidates => false;
    public bool AppliesOneNonOverlappingLedger => true;
    public bool ExactInverseVerified => true;
    public bool ContainsOutputRow80Bytes => true;
    public bool WritesFileSystem => false;
    public bool WritesDiscImage => false;
    public bool WritesCue => false;
    public bool AppIntegrated => false;
    public bool UiIntegrated => false;
    public bool CreateBinEnabled => false;
    public bool NormalCreateBinEnabled => false;
    public bool RuntimeCandidateAuthorized => false;
    public bool RuntimeProven => false;
    public bool ReleaseIntegrated => false;
    public bool PromotionAuthorized => false;
    public bool Publishable => false;

    internal byte[] CopySourceRow80() => _sourceRow80.ToArray();
    internal byte[] CopyOutputRow80() => _outputRow80.ToArray();
}

/// <summary>
/// Schema-3 native compiler slice. It invokes the frozen legacy optional-terrain,
/// T66, optional-T107 compiler as a baseline, replaces an enabled T107 row with
/// the exact catalog-derived row, and independently rebuilds and verifies its
/// own locked-source ledger and inverse. It has no persistence, candidate,
/// runtime, Create BIN, or release surface.
/// </summary>
internal static class Id65AuthoringRemotePadCompositeCompiler
{
    public const string ProfileId =
        "id65-authoring-remote-pad-composite-schema3-in-memory-e1";
    public const int SchemaVersion = 1;
    public const int ExpectedOwnedRangeCount = 7_960;
    public const int ExpectedOwnedRangeCountWithoutT107 = 7_956;
    public const int ExpectedTextureOwnedRangeCount = 7_949;
    public const int ExpectedGrassOwnedRangeCount = 8;
    public const int ExpectedGrassOwnedRangeCountWithoutT107 = 4;
    public const string ExpectedOutputModelSha256 =
        Id65AuthoringCompositeCompiler.ExpectedCombinedModelSha256;
    public const string ExpectedCatalogCanonicalSha256 =
        Id65RemotePadGrassT107Catalog.ExpectedCanonicalSha256;
    public const string ExpectedBaselineOutputRow80Sha256 =
        "dc6dd888b9b62390516fcb1665d80f045b9822b033bfc7c5211c48a3dbbdc4b6";
    public const string ExpectedBaselineDeterministicPlanSha256 =
        "9f7d2f8e0b279b1e957570d0ddd4d8d6bbcf2a932048b3283a8c8c40db9188f2";
    public const int ExpectedChangedByteCount = 921_108;
    public const string ExpectedProtectedComplementSha256 =
        "2211814c105583eecf0d1e3f259ce8dd867a2c4a3988ac77c2c1541adf4fc5a4";

    private static readonly Id65AuthoringRemotePadCompositeFrozenPin[] FrozenPinArray =
    [
        new(
            Id65RemotePadGrassT107Catalog.NorthWestPositionId,
            "0701cb4de1ceca00143976ef6a7dfbc1da4d528a020cfad616169a3a06a60e60",
            "6e5d2972456c2394a37e399e85f22b3907cbaa7f24be0763084831134f199fd4",
            "d627fd48ad49919d651cb2a51e6cd94a59ed625d0f4bed07371e996da40a6e58",
            "7b3b65899b8021d9409d0b0880e7edf082042ee4b1cf4ccbcfb7898fc560cc34"),
        new(
            Id65RemotePadGrassT107Catalog.NorthPositionId,
            "097517a1d19c6f8bd947401fcaa159062caed8e552f757777c58aaac8cdaf550",
            "48e7cdfa42563ccb05901f72d4034f87b839989bbd315bfc7366e01f7a872920",
            "00d3adabdf22a53fcd4fb5967851e7be01f0c957f930f90b9af395266d3a23f3",
            "6a6117cef892b0afc3859c0dde8649de5958ab559d4be2f8981ef6c3db8cc4fc"),
        new(
            Id65RemotePadGrassT107Catalog.NorthEastPositionId,
            "48e6069d5f764a851dca4f65e3d978130c52c2dcb27f2879eaeadd87597d62b2",
            "3917ad93d90995685d0a628bcedd2fcbfc004fbb102833a87a8f023079035651",
            "7884a62b32e92e87073d5211656aa857d9c92bdf6ba3513b3ac6b202fa4927f1",
            "1bbf278985bcd8a6bf0733a0c58b9e4707139f0b01d1ee67646a9076c191d837"),
        new(
            Id65RemotePadGrassT107Catalog.WestPositionId,
            "2c09d59715026b062f1c2d570a28fc42d787e940870bb8622cfa3353417eb161",
            "7865dbf42c1cde3d7c0119a4752616f75b97df25c747408bce4125ee13fe547d",
            "e55baec98264eea330bdf4fa46692ef3be5533a430875a5064827434aa014920",
            "30f55463af79c7cfddee6dfe3286bcc9efd97bb5ee595a6ab563bd5619c8e765"),
        new(
            Id65RemotePadGrassT107Catalog.CenterPositionId,
            "5deb3c24686fba84f4d25c30eec5f1bc6159df2e2165b98b31f7c32279c10f80",
            "7b8a11a3fc877b4f0c8ad2cde9b81609a65832e506ca67bb65ff2a5719c6e63a",
            "e1f4068875d2e85312d8267989c0951aefa88c762f2261ef5906e25176a23964",
            "296fb94633c4b4837377f3f326f583a37573210ec1eaef069e55a660d94af7f6"),
        new(
            Id65RemotePadGrassT107Catalog.EastPositionId,
            "00f5869e8d1398f4a0264eef9b52332a0a90ff66254995639eba09f2d00063f2",
            "352a9347a9d0ca10235643279b240db4926c1712479b957010690d2f75f71738",
            "f68150e955005443a0b90229a07aae5135bfe7d53d5d68773ba9893aa729c35a",
            "ae5a8f6fec549108ff23d17c9649a86b6d02d1cbb7a4f89e2c192273ab954021"),
        new(
            Id65RemotePadGrassT107Catalog.SouthWestPositionId,
            "4041dd88c2e31a4bc425d1a929b2e763959963530197ca46893b5a2f69adedba",
            "3d8d5e61e2c3d74d183ebfcab8bcce8da097735c95f7e0fb8cd10725aa55b0a7",
            "37e5f47d8aaf01efcdc51a46a4cb1311337aac1dd97c02c00d306f0d6aed21f3",
            "7bd2aec0a530a3d5a4a38b1c49207caa871647032f4937fdfee0bda0286c7fd1"),
        new(
            Id65RemotePadGrassT107Catalog.SouthPositionId,
            "95c1b2f746ce5c212978dd0a2af1e13cac71ef1026c01765d215384c7dd20776",
            "0f454af09ad10b1f20435be73aadb5ecd790d77f0eb841e292afd2b7bcc2e0e5",
            "0d426962c724a6733500c2292d048e5a3998b6e9c9fc13b1a8201e5e4eb563ba",
            "19680e124d5963afbc2c786f2639c49a983c517c816174cf11e2069d99eb9bf3"),
        new(
            Id65RemotePadGrassT107Catalog.SouthEastPositionId,
            "a6d4f09522dfafc046c2f61ad031034ff7eef4d32a79a8c1bad1a8e13f22fcd3",
            "8bf320049d91e2504ddcf241e43d0f641fe66419797d26109a7f7b7548e19a13",
            "9729ca15da30bbaec97ed8612891b57d035c130bf45265ce67eb11973ca3a458",
            "2b04bf1c9679bd24aea14fab97dacdb6bbd597b20d223399529b2dc0eefad1f4")
    ];

    private static readonly ReadOnlyDictionary<
        string,
        Id65AuthoringRemotePadCompositeFrozenPin> FrozenPins =
        new(FrozenPinArray.ToDictionary(item => item.PositionId, StringComparer.Ordinal));

    // Frozen from the locked source for the three additional state families;
    // the original terrain-plus-Grass family remains pinned by FrozenPinArray.
    private static readonly Id65AuthoringRemotePadCompositeStateFrozenPin[]
        AdditionalStateFrozenPinArray =
    [
        new(
            false,
            false,
            "remote-pad-grid.x368-y336",
            "5d670998972bde939cf4b0cac9ab1812072a3a5f9d477ed13346bb55862a3fd0",
            "6a3800186f60616a7668679950d0c2a0e03871fda5fc0720c2e055a32b95711c",
            "5d670998972bde939cf4b0cac9ab1812072a3a5f9d477ed13346bb55862a3fd0",
            "01981912169cb118dd095ec709a3326927b1ea5371247486670d1e48fc85ccd0",
            7956,
            4,
            883813,
            "6bd7d0b3fe576d2b4250307f0e8a29780416619d769e86e5eb5cce03ca4537ee",
            "464ee30a56ed6362dfc3d205ec503489c9bcdde9dc674435643b339ea13dae1c",
            "f4b787e56a5c708a214ab82201f518de7d4cbb979d269f23b33b549bfff8f977",
            "301a1b5dd9e5d6e48dff05242a83320f0a346256e8d380128e6b89c1c30ec39a"),
        new(
            false,
            false,
            "remote-pad-grid.x384-y336",
            "5d670998972bde939cf4b0cac9ab1812072a3a5f9d477ed13346bb55862a3fd0",
            "6a3800186f60616a7668679950d0c2a0e03871fda5fc0720c2e055a32b95711c",
            "5d670998972bde939cf4b0cac9ab1812072a3a5f9d477ed13346bb55862a3fd0",
            "01981912169cb118dd095ec709a3326927b1ea5371247486670d1e48fc85ccd0",
            7956,
            4,
            883813,
            "6bd7d0b3fe576d2b4250307f0e8a29780416619d769e86e5eb5cce03ca4537ee",
            "464ee30a56ed6362dfc3d205ec503489c9bcdde9dc674435643b339ea13dae1c",
            "f4b787e56a5c708a214ab82201f518de7d4cbb979d269f23b33b549bfff8f977",
            "032dabe721fb129cde7837388df9e6919b43d780133ebaaa6b0055c0e2d510f2"),
        new(
            false,
            false,
            "remote-pad-grid.x400-y336",
            "5d670998972bde939cf4b0cac9ab1812072a3a5f9d477ed13346bb55862a3fd0",
            "6a3800186f60616a7668679950d0c2a0e03871fda5fc0720c2e055a32b95711c",
            "5d670998972bde939cf4b0cac9ab1812072a3a5f9d477ed13346bb55862a3fd0",
            "01981912169cb118dd095ec709a3326927b1ea5371247486670d1e48fc85ccd0",
            7956,
            4,
            883813,
            "6bd7d0b3fe576d2b4250307f0e8a29780416619d769e86e5eb5cce03ca4537ee",
            "464ee30a56ed6362dfc3d205ec503489c9bcdde9dc674435643b339ea13dae1c",
            "f4b787e56a5c708a214ab82201f518de7d4cbb979d269f23b33b549bfff8f977",
            "bc99356b0ac3129447f40847567dfb19861c4a002df08efca0e9c20facd7d1cd"),
        new(
            false,
            false,
            "remote-pad-grid.x368-y352",
            "5d670998972bde939cf4b0cac9ab1812072a3a5f9d477ed13346bb55862a3fd0",
            "6a3800186f60616a7668679950d0c2a0e03871fda5fc0720c2e055a32b95711c",
            "5d670998972bde939cf4b0cac9ab1812072a3a5f9d477ed13346bb55862a3fd0",
            "01981912169cb118dd095ec709a3326927b1ea5371247486670d1e48fc85ccd0",
            7956,
            4,
            883813,
            "6bd7d0b3fe576d2b4250307f0e8a29780416619d769e86e5eb5cce03ca4537ee",
            "464ee30a56ed6362dfc3d205ec503489c9bcdde9dc674435643b339ea13dae1c",
            "f4b787e56a5c708a214ab82201f518de7d4cbb979d269f23b33b549bfff8f977",
            "4385cadf2e789d834b2fbc3a588ad8590ccf9e639513046ce393c0b4872787d6"),
        new(
            false,
            false,
            "remote-pad-grid.x384-y352",
            "5d670998972bde939cf4b0cac9ab1812072a3a5f9d477ed13346bb55862a3fd0",
            "6a3800186f60616a7668679950d0c2a0e03871fda5fc0720c2e055a32b95711c",
            "5d670998972bde939cf4b0cac9ab1812072a3a5f9d477ed13346bb55862a3fd0",
            "01981912169cb118dd095ec709a3326927b1ea5371247486670d1e48fc85ccd0",
            7956,
            4,
            883813,
            "6bd7d0b3fe576d2b4250307f0e8a29780416619d769e86e5eb5cce03ca4537ee",
            "464ee30a56ed6362dfc3d205ec503489c9bcdde9dc674435643b339ea13dae1c",
            "f4b787e56a5c708a214ab82201f518de7d4cbb979d269f23b33b549bfff8f977",
            "3c094517c4e137ffdc07f9a5d76bab9e39b4cbf5aff8ed422bdacde383abc561"),
        new(
            false,
            false,
            "remote-pad-grid.x400-y352",
            "5d670998972bde939cf4b0cac9ab1812072a3a5f9d477ed13346bb55862a3fd0",
            "6a3800186f60616a7668679950d0c2a0e03871fda5fc0720c2e055a32b95711c",
            "5d670998972bde939cf4b0cac9ab1812072a3a5f9d477ed13346bb55862a3fd0",
            "01981912169cb118dd095ec709a3326927b1ea5371247486670d1e48fc85ccd0",
            7956,
            4,
            883813,
            "6bd7d0b3fe576d2b4250307f0e8a29780416619d769e86e5eb5cce03ca4537ee",
            "464ee30a56ed6362dfc3d205ec503489c9bcdde9dc674435643b339ea13dae1c",
            "f4b787e56a5c708a214ab82201f518de7d4cbb979d269f23b33b549bfff8f977",
            "c71c5030403bbcdb2b8de48cf38a5cffa5068d50483cd5804893cd8e692b055c"),
        new(
            false,
            false,
            "remote-pad-grid.x368-y368",
            "5d670998972bde939cf4b0cac9ab1812072a3a5f9d477ed13346bb55862a3fd0",
            "6a3800186f60616a7668679950d0c2a0e03871fda5fc0720c2e055a32b95711c",
            "5d670998972bde939cf4b0cac9ab1812072a3a5f9d477ed13346bb55862a3fd0",
            "01981912169cb118dd095ec709a3326927b1ea5371247486670d1e48fc85ccd0",
            7956,
            4,
            883813,
            "6bd7d0b3fe576d2b4250307f0e8a29780416619d769e86e5eb5cce03ca4537ee",
            "464ee30a56ed6362dfc3d205ec503489c9bcdde9dc674435643b339ea13dae1c",
            "f4b787e56a5c708a214ab82201f518de7d4cbb979d269f23b33b549bfff8f977",
            "8a5dd61e22d47f68a6358f975e16127af37cb132da4dfdcfc3c2d919c4380749"),
        new(
            false,
            false,
            "remote-pad-grid.x384-y368",
            "5d670998972bde939cf4b0cac9ab1812072a3a5f9d477ed13346bb55862a3fd0",
            "6a3800186f60616a7668679950d0c2a0e03871fda5fc0720c2e055a32b95711c",
            "5d670998972bde939cf4b0cac9ab1812072a3a5f9d477ed13346bb55862a3fd0",
            "01981912169cb118dd095ec709a3326927b1ea5371247486670d1e48fc85ccd0",
            7956,
            4,
            883813,
            "6bd7d0b3fe576d2b4250307f0e8a29780416619d769e86e5eb5cce03ca4537ee",
            "464ee30a56ed6362dfc3d205ec503489c9bcdde9dc674435643b339ea13dae1c",
            "f4b787e56a5c708a214ab82201f518de7d4cbb979d269f23b33b549bfff8f977",
            "501a1791bb9af6685ab0f3327436abd0280187632a0dd50113077e352e647c36"),
        new(
            false,
            false,
            "remote-pad-grid.x400-y368",
            "5d670998972bde939cf4b0cac9ab1812072a3a5f9d477ed13346bb55862a3fd0",
            "6a3800186f60616a7668679950d0c2a0e03871fda5fc0720c2e055a32b95711c",
            "5d670998972bde939cf4b0cac9ab1812072a3a5f9d477ed13346bb55862a3fd0",
            "01981912169cb118dd095ec709a3326927b1ea5371247486670d1e48fc85ccd0",
            7956,
            4,
            883813,
            "6bd7d0b3fe576d2b4250307f0e8a29780416619d769e86e5eb5cce03ca4537ee",
            "464ee30a56ed6362dfc3d205ec503489c9bcdde9dc674435643b339ea13dae1c",
            "f4b787e56a5c708a214ab82201f518de7d4cbb979d269f23b33b549bfff8f977",
            "4aa5858f1f21b6e5c9a92ea7af6f282996d18228cbf2597ec20e5b3bfe7514f0"),
        new(
            false,
            true,
            "remote-pad-grid.x368-y336",
            "4521657f104ee121e3e87ca342a4d8a600623907b9f6aa4081340c57e3ab4326",
            "7573df9aa4549aba4a806c51138aeb52e222f3a8f42184cea14b2e79e68a1e3e",
            "43819fc49ac133633b3676f1621161e400e7739f5efa816780b13fa09497a47c",
            "01981912169cb118dd095ec709a3326927b1ea5371247486670d1e48fc85ccd0",
            7960,
            8,
            883835,
            "2211814c105583eecf0d1e3f259ce8dd867a2c4a3988ac77c2c1541adf4fc5a4",
            "196521b51b80de67601411c751b06cd9c9a7ab52a7d57d0470f396dc1d44a50e",
            "a8ee621e1ea0411668c811e6f622016530639973705ae3b5425ea535d63335ed",
            "22df01430e4f4aafdf96ea81241bf50f621af137dd116865b9ae06cc70e03d7c"),
        new(
            false,
            true,
            "remote-pad-grid.x384-y336",
            "4521657f104ee121e3e87ca342a4d8a600623907b9f6aa4081340c57e3ab4326",
            "7573df9aa4549aba4a806c51138aeb52e222f3a8f42184cea14b2e79e68a1e3e",
            "603cb4855f97f69d8f7d03c9afb13472f35f4fa009db4acfa9b0cee41245151c",
            "01981912169cb118dd095ec709a3326927b1ea5371247486670d1e48fc85ccd0",
            7960,
            8,
            883835,
            "2211814c105583eecf0d1e3f259ce8dd867a2c4a3988ac77c2c1541adf4fc5a4",
            "2e9bc7968035efb7a715f312e730fa3118bd08fb8a665c35e11eba2ba0fee18e",
            "5f7c90f7d3b040034d64b4c474802016a4dedf9b00e5f792045e08f6f6693dbb",
            "c2d76add300e48b9cd9b98189f88540925265e54040748af99990caa6be6e4f6"),
        new(
            false,
            true,
            "remote-pad-grid.x400-y336",
            "4521657f104ee121e3e87ca342a4d8a600623907b9f6aa4081340c57e3ab4326",
            "7573df9aa4549aba4a806c51138aeb52e222f3a8f42184cea14b2e79e68a1e3e",
            "94ad4b3acc6570d8f9233746ec179250807a3d17a189a4059db41d322299f195",
            "01981912169cb118dd095ec709a3326927b1ea5371247486670d1e48fc85ccd0",
            7960,
            8,
            883835,
            "2211814c105583eecf0d1e3f259ce8dd867a2c4a3988ac77c2c1541adf4fc5a4",
            "77bd2cd695959d4cb1d3b3315141649d6f0f38a76c0f365ec697b0865fb5ce78",
            "321ae4f3bfb57148d82ab01861d896e5097cfe7a100fcb09d91a9994cfec0c8b",
            "caa3cf5d37db0c3e16172b570a0f229625b03bfeeb3c0dc3411e6805430bbc2f"),
        new(
            false,
            true,
            "remote-pad-grid.x368-y352",
            "4521657f104ee121e3e87ca342a4d8a600623907b9f6aa4081340c57e3ab4326",
            "7573df9aa4549aba4a806c51138aeb52e222f3a8f42184cea14b2e79e68a1e3e",
            "7ad0236af974e7542f35e039eae9e567aca971dc39d224c9d63ae3d8eebc962b",
            "01981912169cb118dd095ec709a3326927b1ea5371247486670d1e48fc85ccd0",
            7960,
            8,
            883835,
            "2211814c105583eecf0d1e3f259ce8dd867a2c4a3988ac77c2c1541adf4fc5a4",
            "36765811d97536329597bc50ff42d134363a2dfeca06bd4c137f180deda86693",
            "33dd5f02cbe16c2ae9edf9c642f164010e5748aab28ccdbb88b4a6c3258dea18",
            "19eb45022a25fa3f54236030e634e0cdc429c79ca8d859498b25ef5f36ae4332"),
        new(
            false,
            true,
            "remote-pad-grid.x384-y352",
            "4521657f104ee121e3e87ca342a4d8a600623907b9f6aa4081340c57e3ab4326",
            "7573df9aa4549aba4a806c51138aeb52e222f3a8f42184cea14b2e79e68a1e3e",
            "e9c377d1a594b0bc712b231ad00ad5a0259ac604d6b7b928c448253110549068",
            "01981912169cb118dd095ec709a3326927b1ea5371247486670d1e48fc85ccd0",
            7960,
            8,
            883835,
            "2211814c105583eecf0d1e3f259ce8dd867a2c4a3988ac77c2c1541adf4fc5a4",
            "ce95c554b9c8fbf09ef1f22acf6eb1648718065db64a02c75c1c8dc64a270b76",
            "1c70988edd5b14feeb397c4d6a93a44848ebebc4e98d4d77cc52e616c4b66591",
            "520a154cc43994be314a77cf83cd23cdb7e485d767ac595e116f8064381cb240"),
        new(
            false,
            true,
            "remote-pad-grid.x400-y352",
            "4521657f104ee121e3e87ca342a4d8a600623907b9f6aa4081340c57e3ab4326",
            "7573df9aa4549aba4a806c51138aeb52e222f3a8f42184cea14b2e79e68a1e3e",
            "a94ab9f3c825e57c4110f74937622d5210cb94d3219aad572aab42a7dce5a528",
            "01981912169cb118dd095ec709a3326927b1ea5371247486670d1e48fc85ccd0",
            7960,
            8,
            883835,
            "2211814c105583eecf0d1e3f259ce8dd867a2c4a3988ac77c2c1541adf4fc5a4",
            "878a5e2d306ef5d81c793b229e572e572c85515df85865659e2a4a625fbf6476",
            "9d83633c44b998fb7c758f038d7163f4a4288e493c4b1835d8ef8492eb4e7ba3",
            "86a9a5c7132bf853a631053d400e608e230a7d84a1bc904957b544a5bcdbe652"),
        new(
            false,
            true,
            "remote-pad-grid.x368-y368",
            "4521657f104ee121e3e87ca342a4d8a600623907b9f6aa4081340c57e3ab4326",
            "7573df9aa4549aba4a806c51138aeb52e222f3a8f42184cea14b2e79e68a1e3e",
            "45b95366fff8e8f0a78613d120843cd4eff5cb79c9b98beebc458af2d3527ba7",
            "01981912169cb118dd095ec709a3326927b1ea5371247486670d1e48fc85ccd0",
            7960,
            8,
            883835,
            "2211814c105583eecf0d1e3f259ce8dd867a2c4a3988ac77c2c1541adf4fc5a4",
            "b87f37e5d66aa7777b2352149cef677549e79c22c956b86529399d0b37fcf7e6",
            "ff66007cbf6545fde41bf7569b3c6e0801cde2a473919301f56c15c063f11498",
            "6006e139606f807f406da6f257af7a63f51123f46274c5deada7094cda394b3c"),
        new(
            false,
            true,
            "remote-pad-grid.x384-y368",
            "4521657f104ee121e3e87ca342a4d8a600623907b9f6aa4081340c57e3ab4326",
            "7573df9aa4549aba4a806c51138aeb52e222f3a8f42184cea14b2e79e68a1e3e",
            "157f857cb66077c4c79d72117180a7818a453f053a1e65be4cbb5a6d2555badc",
            "01981912169cb118dd095ec709a3326927b1ea5371247486670d1e48fc85ccd0",
            7960,
            8,
            883835,
            "2211814c105583eecf0d1e3f259ce8dd867a2c4a3988ac77c2c1541adf4fc5a4",
            "66bf679a693ae00e1f72fcd8bfb567f3f1ec472b3be32b8ca8435577a83c15d9",
            "253a2e8efbd944f7e1bb2fbce3d53c3429b06e2a1d57c6551a3bf0be8e577c63",
            "d55b01f36761d630129f0653dde604333bdd1bc53de01f0c64743151ce2bd087"),
        new(
            false,
            true,
            "remote-pad-grid.x400-y368",
            "4521657f104ee121e3e87ca342a4d8a600623907b9f6aa4081340c57e3ab4326",
            "7573df9aa4549aba4a806c51138aeb52e222f3a8f42184cea14b2e79e68a1e3e",
            "35abd2fd140a46218d3ca473638a48cc0490b2463582b2bacd055a2d7913ae85",
            "01981912169cb118dd095ec709a3326927b1ea5371247486670d1e48fc85ccd0",
            7960,
            8,
            883835,
            "2211814c105583eecf0d1e3f259ce8dd867a2c4a3988ac77c2c1541adf4fc5a4",
            "5f790a66699fa9b5cbda5937c9fd0c14b7a374b56748c6ad4ee11231347c1958",
            "0099c10c070c140da4506464139d3c4b1c9cd6a215a47af5484314aeaee03073",
            "1b38b2f212183bfe8ce7837bfc63751eabed10444aed350995302a52f8ef3f1c"),
        new(
            true,
            false,
            "remote-pad-grid.x368-y336",
            "51b677eb4313dccd2f6718ccfa6ea43797ee6110e23b073b362e979cab3eb380",
            "47b839dbdf60c76fed9efcbc4e6420328607a76149a0f29d255ab6f614f48482",
            "51b677eb4313dccd2f6718ccfa6ea43797ee6110e23b073b362e979cab3eb380",
            "fcbd5bcd1abdf0d55ce2673a1743670760e800f0460b0150576a7f88c371ae63",
            7956,
            4,
            921086,
            "6bd7d0b3fe576d2b4250307f0e8a29780416619d769e86e5eb5cce03ca4537ee",
            "5630f6f74ac05e705bee49ee534f4764bd2807a02ab13b397655494f084671c7",
            "165b0b5c28e6a44802d23e1ac704e58f8ace73e01288a58726f6d5afb531522a",
            "83e09480830ec0b2cb2d842a2a7bb21b052a166c8bac3a95be39ce610d7717af"),
        new(
            true,
            false,
            "remote-pad-grid.x384-y336",
            "51b677eb4313dccd2f6718ccfa6ea43797ee6110e23b073b362e979cab3eb380",
            "47b839dbdf60c76fed9efcbc4e6420328607a76149a0f29d255ab6f614f48482",
            "51b677eb4313dccd2f6718ccfa6ea43797ee6110e23b073b362e979cab3eb380",
            "fcbd5bcd1abdf0d55ce2673a1743670760e800f0460b0150576a7f88c371ae63",
            7956,
            4,
            921086,
            "6bd7d0b3fe576d2b4250307f0e8a29780416619d769e86e5eb5cce03ca4537ee",
            "5630f6f74ac05e705bee49ee534f4764bd2807a02ab13b397655494f084671c7",
            "165b0b5c28e6a44802d23e1ac704e58f8ace73e01288a58726f6d5afb531522a",
            "f9add3d70347afef9cf8ddc57d78b18c551bd6773cbbe32543aeeb062be56e05"),
        new(
            true,
            false,
            "remote-pad-grid.x400-y336",
            "51b677eb4313dccd2f6718ccfa6ea43797ee6110e23b073b362e979cab3eb380",
            "47b839dbdf60c76fed9efcbc4e6420328607a76149a0f29d255ab6f614f48482",
            "51b677eb4313dccd2f6718ccfa6ea43797ee6110e23b073b362e979cab3eb380",
            "fcbd5bcd1abdf0d55ce2673a1743670760e800f0460b0150576a7f88c371ae63",
            7956,
            4,
            921086,
            "6bd7d0b3fe576d2b4250307f0e8a29780416619d769e86e5eb5cce03ca4537ee",
            "5630f6f74ac05e705bee49ee534f4764bd2807a02ab13b397655494f084671c7",
            "165b0b5c28e6a44802d23e1ac704e58f8ace73e01288a58726f6d5afb531522a",
            "3f6bc94ecbb3104f72fcca1da558fa91b107d5d65ef8c9c8d3a4a2ce540ea0b6"),
        new(
            true,
            false,
            "remote-pad-grid.x368-y352",
            "51b677eb4313dccd2f6718ccfa6ea43797ee6110e23b073b362e979cab3eb380",
            "47b839dbdf60c76fed9efcbc4e6420328607a76149a0f29d255ab6f614f48482",
            "51b677eb4313dccd2f6718ccfa6ea43797ee6110e23b073b362e979cab3eb380",
            "fcbd5bcd1abdf0d55ce2673a1743670760e800f0460b0150576a7f88c371ae63",
            7956,
            4,
            921086,
            "6bd7d0b3fe576d2b4250307f0e8a29780416619d769e86e5eb5cce03ca4537ee",
            "5630f6f74ac05e705bee49ee534f4764bd2807a02ab13b397655494f084671c7",
            "165b0b5c28e6a44802d23e1ac704e58f8ace73e01288a58726f6d5afb531522a",
            "7ec79c796178423fcdaddd1dcc9faf84658b260add8530c9c1d9e2b45ee241d9"),
        new(
            true,
            false,
            "remote-pad-grid.x384-y352",
            "51b677eb4313dccd2f6718ccfa6ea43797ee6110e23b073b362e979cab3eb380",
            "47b839dbdf60c76fed9efcbc4e6420328607a76149a0f29d255ab6f614f48482",
            "51b677eb4313dccd2f6718ccfa6ea43797ee6110e23b073b362e979cab3eb380",
            "fcbd5bcd1abdf0d55ce2673a1743670760e800f0460b0150576a7f88c371ae63",
            7956,
            4,
            921086,
            "6bd7d0b3fe576d2b4250307f0e8a29780416619d769e86e5eb5cce03ca4537ee",
            "5630f6f74ac05e705bee49ee534f4764bd2807a02ab13b397655494f084671c7",
            "165b0b5c28e6a44802d23e1ac704e58f8ace73e01288a58726f6d5afb531522a",
            "e26a7b0ea83349d04410a78ddd4ffa48e57e3b62ff1c5a26bc5e6a79e32955e5"),
        new(
            true,
            false,
            "remote-pad-grid.x400-y352",
            "51b677eb4313dccd2f6718ccfa6ea43797ee6110e23b073b362e979cab3eb380",
            "47b839dbdf60c76fed9efcbc4e6420328607a76149a0f29d255ab6f614f48482",
            "51b677eb4313dccd2f6718ccfa6ea43797ee6110e23b073b362e979cab3eb380",
            "fcbd5bcd1abdf0d55ce2673a1743670760e800f0460b0150576a7f88c371ae63",
            7956,
            4,
            921086,
            "6bd7d0b3fe576d2b4250307f0e8a29780416619d769e86e5eb5cce03ca4537ee",
            "5630f6f74ac05e705bee49ee534f4764bd2807a02ab13b397655494f084671c7",
            "165b0b5c28e6a44802d23e1ac704e58f8ace73e01288a58726f6d5afb531522a",
            "c183bd46f245b3472217ce5ec0c8e806e064d4abb40993005305e9fec2f18b74"),
        new(
            true,
            false,
            "remote-pad-grid.x368-y368",
            "51b677eb4313dccd2f6718ccfa6ea43797ee6110e23b073b362e979cab3eb380",
            "47b839dbdf60c76fed9efcbc4e6420328607a76149a0f29d255ab6f614f48482",
            "51b677eb4313dccd2f6718ccfa6ea43797ee6110e23b073b362e979cab3eb380",
            "fcbd5bcd1abdf0d55ce2673a1743670760e800f0460b0150576a7f88c371ae63",
            7956,
            4,
            921086,
            "6bd7d0b3fe576d2b4250307f0e8a29780416619d769e86e5eb5cce03ca4537ee",
            "5630f6f74ac05e705bee49ee534f4764bd2807a02ab13b397655494f084671c7",
            "165b0b5c28e6a44802d23e1ac704e58f8ace73e01288a58726f6d5afb531522a",
            "482c2f10645b4a39ee0c1724fbde102300451b1ffbd34c7403f9164a1617e4f8"),
        new(
            true,
            false,
            "remote-pad-grid.x384-y368",
            "51b677eb4313dccd2f6718ccfa6ea43797ee6110e23b073b362e979cab3eb380",
            "47b839dbdf60c76fed9efcbc4e6420328607a76149a0f29d255ab6f614f48482",
            "51b677eb4313dccd2f6718ccfa6ea43797ee6110e23b073b362e979cab3eb380",
            "fcbd5bcd1abdf0d55ce2673a1743670760e800f0460b0150576a7f88c371ae63",
            7956,
            4,
            921086,
            "6bd7d0b3fe576d2b4250307f0e8a29780416619d769e86e5eb5cce03ca4537ee",
            "5630f6f74ac05e705bee49ee534f4764bd2807a02ab13b397655494f084671c7",
            "165b0b5c28e6a44802d23e1ac704e58f8ace73e01288a58726f6d5afb531522a",
            "00bea6a48aaf3747322b2fa6a4519f470e769f5e5eb3e05f7ef13d1dc5b93463"),
        new(
            true,
            false,
            "remote-pad-grid.x400-y368",
            "51b677eb4313dccd2f6718ccfa6ea43797ee6110e23b073b362e979cab3eb380",
            "47b839dbdf60c76fed9efcbc4e6420328607a76149a0f29d255ab6f614f48482",
            "51b677eb4313dccd2f6718ccfa6ea43797ee6110e23b073b362e979cab3eb380",
            "fcbd5bcd1abdf0d55ce2673a1743670760e800f0460b0150576a7f88c371ae63",
            7956,
            4,
            921086,
            "6bd7d0b3fe576d2b4250307f0e8a29780416619d769e86e5eb5cce03ca4537ee",
            "5630f6f74ac05e705bee49ee534f4764bd2807a02ab13b397655494f084671c7",
            "165b0b5c28e6a44802d23e1ac704e58f8ace73e01288a58726f6d5afb531522a",
            "1bd5771d1f4d612add79826667de762110bbaab3fd7cc5a418d286e0e9a282c2")
    ];

    private static readonly ReadOnlyDictionary<
        string,
        Id65AuthoringRemotePadCompositeStateFrozenPin> AdditionalStateFrozenPins =
        new(AdditionalStateFrozenPinArray.ToDictionary(
            item => BuildStateKey(
                item.StructuralTerrainPresent,
                item.GrassT107Present,
                item.PositionId),
            StringComparer.Ordinal));

    private const int Row80ByteLength = 0x2E2000;
    private const int TexturePagesOffset = 0x800;
    private const int TexturePagesLength = 0xDE000;
    internal const int ModelOffset = 0xDE800;
    internal const int ModelLength = 0x94800;
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
    private const int Root37Offset = 0xE4;
    private const int ActorId37Offset = 0x19A;
    private const int PackageOffset = 0x1CFA44;
    private const int PackageLength = 0x174;
    private const string LegacyT107RowSha256 =
        "d42e49248278759d2c2020bba4d4c4db18817bf120d73829a2bbba7dddcef9fb";

    static Id65AuthoringRemotePadCompositeCompiler()
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
        if (FrozenPinArray.Length != 9 ||
            FrozenPins.Count != 9 ||
            !FrozenPinArray.Select(item => item.PositionId).SequenceEqual(
                Id65RemotePadGrassT107Catalog.Positions.Select(item => item.PositionId)) ||
            FrozenPinArray.Any(item =>
                item.OutputRow80Sha256.Length != 64 ||
                item.OwnedRangeMapSha256.Length != 64 ||
                item.TransactionSha256.Length != 64 ||
                item.DeterministicPlanSha256.Length != 64) ||
            AdditionalStateFrozenPinArray.Length != 27 ||
            AdditionalStateFrozenPins.Count != 27 ||
            !AdditionalStateFrozenPinArray.Select(item => BuildStateKey(
                    item.StructuralTerrainPresent,
                    item.GrassT107Present,
                    item.PositionId))
                .SequenceEqual(expectedAdditionalKeys) ||
            AdditionalStateFrozenPinArray.Any(item =>
                item.StructuralTerrainPresent && item.GrassT107Present ||
                item.OwnedRangeCount != (item.GrassT107Present
                    ? ExpectedOwnedRangeCount
                    : ExpectedOwnedRangeCountWithoutT107) ||
                item.GrassOwnedRangeCount != (item.GrassT107Present
                    ? ExpectedGrassOwnedRangeCount
                    : ExpectedGrassOwnedRangeCountWithoutT107) ||
                item.OutputModelSha256 != (item.StructuralTerrainPresent
                    ? ExpectedOutputModelSha256
                    : Id65V2NativeTextureCompositionCompiler
                        .ExpectedOutputModelSha256) ||
                item.ChangedByteCount <= 0 ||
                new[]
                {
                    item.BaselineOutputRow80Sha256,
                    item.BaselineDeterministicPlanSha256,
                    item.OutputRow80Sha256,
                    item.OutputModelSha256,
                    item.ProtectedComplementSha256,
                    item.OwnedRangeMapSha256,
                    item.TransactionSha256,
                    item.DeterministicPlanSha256
                }.Any(hash => hash.Length != 64)) ||
            AdditionalStateFrozenPinArray.Select(item =>
                    item.DeterministicPlanSha256)
                .Distinct(StringComparer.Ordinal).Count() != 27)
        {
            throw new InvalidDataException(
                "The frozen 36-state schema-3 result contract changed.");
        }
    }

    public static Id65AuthoringRemotePadCompositeFrozenPin GetRequiredFrozenPin(
        string positionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(positionId);
        if (!FrozenPins.TryGetValue(
                positionId,
                out Id65AuthoringRemotePadCompositeFrozenPin? pin))
        {
            throw new KeyNotFoundException(
                "Unknown frozen schema-3 remote-pad result position " + positionId + ".");
        }
        return pin;
    }

    public static Id65AuthoringRemotePadCompositeStateFrozenPin
        GetRequiredStateFrozenPin(
            bool structuralTerrainPresent,
            bool grassT107Present,
            string positionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(positionId);
        _ = Id65RemotePadGrassT107Catalog.GetRequiredPosition(positionId);
        Id65AuthoringRemotePadCompositeStateFrozenPin? pin =
            TryGetStateFrozenPin(
                structuralTerrainPresent,
                grassT107Present,
                positionId);
        if (pin is null)
        {
            throw new KeyNotFoundException(
                "Unknown frozen schema-3 state terrain=" +
                structuralTerrainPresent + ", Grass T107=" +
                grassT107Present + ", position=" + positionId + ".");
        }
        return pin;
    }

    private static Id65AuthoringRemotePadCompositeStateFrozenPin?
        TryGetStateFrozenPin(
            bool structuralTerrainPresent,
            bool grassT107Present,
            string positionId)
    {
        if (structuralTerrainPresent && grassT107Present)
        {
            Id65AuthoringRemotePadCompositeFrozenPin current =
                GetRequiredFrozenPin(positionId);
            return new(
                StructuralTerrainPresent: true,
                GrassT107Present: true,
                positionId,
                ExpectedBaselineOutputRow80Sha256,
                ExpectedBaselineDeterministicPlanSha256,
                current.OutputRow80Sha256,
                ExpectedOutputModelSha256,
                ExpectedOwnedRangeCount,
                ExpectedGrassOwnedRangeCount,
                ExpectedChangedByteCount,
                ExpectedProtectedComplementSha256,
                current.OwnedRangeMapSha256,
                current.TransactionSha256,
                current.DeterministicPlanSha256);
        }

        AdditionalStateFrozenPins.TryGetValue(
            BuildStateKey(
                structuralTerrainPresent,
                grassT107Present,
                positionId),
            out Id65AuthoringRemotePadCompositeStateFrozenPin? pin);
        return pin;
    }

    private static string BuildStateKey(
        bool structuralTerrainPresent,
        bool grassT107Present,
        string positionId) =>
        (structuralTerrainPresent ? "terrain-on" : "terrain-off") + "|" +
        (grassT107Present ? "grass-on" : "grass-off") + "|" + positionId;

    public static Id65CompiledAuthoringRemotePadComposite Compile(
        Id65AuthoringRemotePadCompositeCompileRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Document);
        ArgumentNullException.ThrowIfNull(request.Source);
        ArgumentNullException.ThrowIfNull(request.TextureWitness);
        ArgumentNullException.ThrowIfNull(request.GrassBundle);

        (Id65AuthoringEditorDocument document,
            Id65RemotePadGrassT107Position position,
            Id65MobyPlacementIntent? catalogPlacement,
            bool structuralTerrainPresent,
            bool grassT107Present) =
            ValidateDocument(request.Document);

        Id65MobyPlacementIntent[] legacyPlacements = grassT107Present
            ? [Id65AuthoringMobyDependencyBundleCompiler.CreateT107Placement()]
            : [];
        Id65TerrainTriangleIntent? triangle = structuralTerrainPresent
            ? Id65AuthoringTerrainMutationCompiler.CreateConcreteTwoTileWitness()
            : null;
        Id65AuthoringCompositeManifest baselineManifest =
            Id65AuthoringCompositeCompiler.CreateManifest(
                triangle,
                request.TextureWitness,
                request.GrassBundle,
                legacyPlacements,
                Id65AuthoringEditorDocument.PrivateMandatoryBaseFaceTextureId);
        Id65CompiledAuthoringComposite baseline =
            Id65AuthoringCompositeCompiler.Compile(
                new(
                    request.Source,
                    baselineManifest,
                    request.TextureWitness,
                    [request.GrassBundle],
                    legacyPlacements,
                    request.Limits));
        ValidateBaseline(
            baseline,
            baselineManifest,
            request.Source,
            structuralTerrainPresent,
            grassT107Present);

        byte[] source = baseline.CopySourceRow80();
        byte[] baselineOutput = baseline.CopyOutputRow80();
        byte[]? catalogRow = grassT107Present
            ? BuildCatalogRow(
                request.GrassBundle,
                catalogPlacement ?? throw new InvalidDataException(
                    "The enabled schema-3 Grass placement is missing."),
                position)
            : null;
        List<Id65AuthoringCompositeOwnedRange> ledger =
            RebuildLedger(baseline, source, catalogRow);
        ValidateLedger(source, ledger, grassT107Present);

        byte[] output = ApplyOwnedRanges(source, ledger);
        Id65AuthoringCompositeOwnedRange[] inverse = ledger
            .Reverse<Id65AuthoringCompositeOwnedRange>()
            .Select(item => new Id65AuthoringCompositeOwnedRange(
                item.StableId,
                item.OwnerId,
                item.DataRelativeOffset,
                item.CopyAfter(),
                item.CopyBefore()))
            .ToArray();
        byte[] rolledBack = ApplyOwnedRanges(output, inverse);
        if (!rolledBack.SequenceEqual(source))
        {
            throw new InvalidDataException(
                "The schema-3 remote-pad transaction did not invert exactly.");
        }

        VerifyReadback(
            source,
            baselineOutput,
            output,
            catalogRow,
            position,
            structuralTerrainPresent,
            grassT107Present);
        string protectedSource = HashProtectedComplement(source, ledger);
        RequireHash(
            HashProtectedComplement(output, ledger),
            protectedSource,
            "schema-3 protected complement");
        string ownedHash = HashOwnedRanges(ledger);
        string transactionHash = HashTransaction(ledger);
        int changedByteCount = CountChangedBytes(source, output);
        string deterministicHash = Hash(Encoding.UTF8.GetBytes(string.Join('\n',
        [
            ProfileId,
            SchemaVersion.ToString(CultureInfo.InvariantCulture),
            document.CanonicalSha256,
            Id65RemotePadGrassT107Catalog.CanonicalSha256,
            position.PositionId,
            position.RowSha256,
            request.TextureWitness.CanonicalSha256,
            request.GrassBundle.CanonicalSha256,
            baseline.ManifestSha256,
            baseline.OutputRow80Sha256,
            baseline.DeterministicPlanSha256,
            Hash(source),
            Hash(output),
            Hash(output.AsSpan(ModelOffset, ModelLength)),
            changedByteCount.ToString(CultureInfo.InvariantCulture),
            ownedHash,
            transactionHash,
            protectedSource
        ])));

        Id65AuthoringRemotePadCompositeStateFrozenPin frozen =
            GetRequiredStateFrozenPin(
                structuralTerrainPresent,
                grassT107Present,
                position.PositionId);
        RequireHash(
            baseline.OutputRow80Sha256,
            frozen.BaselineOutputRow80Sha256,
            "schema-3 frozen legacy baseline row-80");
        RequireHash(
            baseline.DeterministicPlanSha256,
            frozen.BaselineDeterministicPlanSha256,
            "schema-3 frozen legacy baseline plan");
        RequireHash(
            Hash(output.AsSpan(ModelOffset, ModelLength)),
            frozen.OutputModelSha256,
            "schema-3 frozen output model");
        if (ledger.Count != frozen.OwnedRangeCount ||
            CountGrassOwnedRanges(ledger) != frozen.GrassOwnedRangeCount ||
            changedByteCount != frozen.ChangedByteCount)
        {
            throw new InvalidDataException(
                "The schema-3 frozen state range or changed-byte count changed.");
        }
        RequireHash(
            protectedSource,
            frozen.ProtectedComplementSha256,
            "schema-3 protected complement pin");
        RequireHash(Hash(output), frozen.OutputRow80Sha256,
            "schema-3 state output row-80");
        RequireHash(ownedHash, frozen.OwnedRangeMapSha256,
            "schema-3 state owned-range map");
        RequireHash(transactionHash, frozen.TransactionSha256,
            "schema-3 state transaction");
        RequireHash(deterministicHash, frozen.DeterministicPlanSha256,
            "schema-3 state deterministic plan");

        Id65AuthoringRemotePadCompositePlacementReadback readback = new(
            position.PositionId,
            position.Ordinal,
            position.EditorX,
            position.EditorY,
            position.EditorZ,
            position.RawX,
            position.RawY,
            position.RawZ,
            Id65RemotePadGrassT107Catalog.TargetTrueIndex,
            Id65RemotePadGrassT107Catalog.ActorId,
            Id65RemotePadGrassT107Catalog.YawByte,
            Id65RemotePadGrassT107Catalog.SupportHandle,
            Id65RemotePadGrassT107Catalog.SupportCollisionTrueIndex,
            position.RowSha256,
            grassT107Present);
        Id65CompiledAuthoringRemotePadComposite compiled = new(
            document,
            source,
            output,
            ledger,
            inverse,
            readback,
            baseline.OutputRow80Sha256,
            baseline.DeterministicPlanSha256,
            changedByteCount,
            ownedHash,
            transactionHash,
            protectedSource,
            deterministicHash,
            request.Source.TargetOverlaySha256,
            request.Source.GlobalExecutableSha256);

        byte[] applied =
            ApplyTransactional(compiled, compiled.CopySourceRow80(), reverse: false);
        byte[] inverseApplied =
            ApplyTransactional(compiled, applied, reverse: true);
        if (!applied.SequenceEqual(output) || !inverseApplied.SequenceEqual(source))
        {
            throw new InvalidDataException(
                "The schema-3 compiled transaction failed its exact round trip.");
        }
        return compiled;
    }

    public static byte[] ApplyTransactional(
        Id65CompiledAuthoringRemotePadComposite compiled,
        byte[] input,
        bool reverse)
    {
        ArgumentNullException.ThrowIfNull(compiled);
        ArgumentNullException.ThrowIfNull(input);
        byte[] expected =
            reverse ? compiled.CopyOutputRow80() : compiled.CopySourceRow80();
        if (!input.SequenceEqual(expected))
        {
            throw new InvalidDataException(
                reverse
                    ? "The schema-3 remote-pad rollback afterimage changed."
                    : "The schema-3 remote-pad locked-source preimage changed.");
        }
        IReadOnlyList<Id65AuthoringCompositeOwnedRange> ranges =
            reverse ? compiled.InverseRanges : compiled.OwnedRanges;
        byte[] output = ApplyOwnedRanges(input, ranges);
        byte[] expectedOutput =
            reverse ? compiled.CopySourceRow80() : compiled.CopyOutputRow80();
        if (!output.SequenceEqual(expectedOutput))
        {
            throw new InvalidDataException(
                "The schema-3 remote-pad transaction output changed.");
        }
        return output;
    }

    private static (
        Id65AuthoringEditorDocument Document,
        Id65RemotePadGrassT107Position Position,
        Id65MobyPlacementIntent? Placement,
        bool StructuralTerrainPresent,
        bool GrassT107Present) ValidateDocument(
        Id65AuthoringEditorDocument supplied)
    {
        byte[] canonical = supplied.SerializeUtf8();
        Id65AuthoringEditorDocument reparsed =
            Id65AuthoringEditorDocument.Parse(canonical);
        if (supplied.CanonicalJson != reparsed.CanonicalJson ||
            supplied.CanonicalSha256 != reparsed.CanonicalSha256 ||
            supplied.ContentSha256 != reparsed.ContentSha256 ||
            supplied.SchemaVersion != reparsed.SchemaVersion ||
            supplied.ProfileId != reparsed.ProfileId ||
            supplied.MandatoryBaseFaceTextureId !=
                reparsed.MandatoryBaseFaceTextureId ||
            supplied.T107RemotePadPositionId !=
                reparsed.T107RemotePadPositionId)
        {
            throw new InvalidDataException(
                "The supplied schema-3 document disagrees with its canonical reparse.");
        }
        if (reparsed.SchemaVersion !=
                Id65AuthoringEditorDocument.RemotePadGridSchemaVersion ||
            reparsed.ProfileId !=
                Id65AuthoringEditorDocument.RemotePadGridDocumentProfileId ||
            !reparsed.UsesRemotePadT107Grid ||
            reparsed.MandatoryBaseFaceTextureId !=
                Id65AuthoringEditorDocument.PrivateMandatoryBaseFaceTextureId ||
            reparsed.Terrain.Count > 1 ||
            reparsed.MobyPlacements.Count > 1 ||
            reparsed.MobyPlacements.Any(item =>
                item.Id != Id65RemotePadGrassT107Catalog.PlacementId))
        {
            throw new InvalidDataException(
                "E1 supports schema-3 T66 with optional structural terrain, " +
                "optional Grass T107, and T88 excluded.");
        }

        bool structuralTerrainPresent = reparsed.Terrain.Count == 1;
        bool grassT107Present = reparsed.MobyPlacements.Count == 1;
        string positionId = reparsed.T107RemotePadPositionId!;
        Id65RemotePadGrassT107Position position =
            Id65RemotePadGrassT107Catalog.GetRequiredPosition(positionId);
        Id65AuthoringEditorDocument expected =
            Id65AuthoringEditorDocument.CreateRemotePadGridExact(
                position.PositionId,
                includeOptionalTerrain: structuralTerrainPresent,
                placementIds: grassT107Present
                    ? [Id65RemotePadGrassT107Catalog.PlacementId]
                    : [],
                mandatoryBaseFaceTextureId:
                    Id65AuthoringEditorDocument.PrivateMandatoryBaseFaceTextureId);
        if (reparsed.CanonicalJson != expected.CanonicalJson ||
            reparsed.CanonicalSha256 != expected.CanonicalSha256 ||
            Id65RemotePadGrassT107Catalog.CanonicalSha256 !=
                ExpectedCatalogCanonicalSha256 ||
            !Id65RemotePadGrassT107Catalog.IsStrictlyInsideSupportTriangle(
                position.EditorX,
                position.EditorY,
                position.EditorZ) ||
            Id65RemotePadGrassT107Catalog.RuntimeAcceptancePassed ||
            Id65RemotePadGrassT107Catalog.RuntimeCandidateAuthorized ||
            Id65RemotePadGrassT107Catalog.NormalCreateBinAuthorized ||
            Id65RemotePadGrassT107Catalog.ReleasePublicationAuthorized)
        {
            throw new InvalidDataException(
                "The schema-3 remote-pad document or frozen catalog identity changed.");
        }

        Id65MobyPlacementIntent? placement = grassT107Present
            ? Id65RemotePadGrassT107Catalog.CreateRemotePadT107Placement(
                position.PositionId)
            : null;
        if (placement is not null &&
            (placement.Id != Id65RemotePadGrassT107Catalog.PlacementId ||
            placement.AtomicGroupId != Id65RemotePadGrassT107Catalog.AtomicGroupId ||
            placement.BundleId != Id65RemotePadGrassT107Catalog.BundleId ||
            placement.RowTemplateId != Id65RemotePadGrassT107Catalog.RowTemplateId ||
            placement.RowPolicy != Id65MobyRowAllocationPolicy.AppendContiguous ||
            placement.TargetTrueIndex !=
                Id65RemotePadGrassT107Catalog.TargetTrueIndex ||
            placement.CoordinatePolicy !=
                Id65MobyCoordinatePolicy.AuthorExact ||
            placement.CoordinatesOrDelta !=
                new Id65AuthoringPoint(
                    position.RawX,
                    position.RawY,
                    position.RawZ) ||
            placement.YawByte != Id65RemotePadGrassT107Catalog.YawByte ||
            placement.SupportHandle != Id65RemotePadGrassT107Catalog.SupportHandle))
        {
            throw new InvalidDataException(
                "The catalog-derived schema-3 placement identity changed.");
        }
        return (
            reparsed,
            position,
            placement,
            structuralTerrainPresent,
            grassT107Present);
    }

    private static void ValidateBaseline(
        Id65CompiledAuthoringComposite baseline,
        Id65AuthoringCompositeManifest manifest,
        Id65AuthoringCompositeLockedSource source,
        bool structuralTerrainPresent,
        bool grassT107Present)
    {
        Id65AuthoringCompositeOwnedRange[] t107 = baseline.OwnedRanges
            .Where(item => item.StableId == "placement.t107")
            .ToArray();
        int expectedOwnedRangeCount = grassT107Present
            ? ExpectedOwnedRangeCount
            : ExpectedOwnedRangeCountWithoutT107;
        string expectedOutputModelSha256 = structuralTerrainPresent
            ? ExpectedOutputModelSha256
            : Id65V2NativeTextureCompositionCompiler.ExpectedOutputModelSha256;
        bool placementIdentityValid = grassT107Present
            ? t107.Length == 1 &&
                t107[0].DataRelativeOffset == T107Offset &&
                t107[0].ByteLength == RecordLength &&
                t107[0].AfterSha256 == LegacyT107RowSha256 &&
                baseline.MobyPlacements.Count == 1 &&
                baseline.MobyPlacements[0].TargetTrueIndex == 107 &&
                baseline.MobyPlacements[0].RowSha256 == LegacyT107RowSha256
            : t107.Length == 0 && baseline.MobyPlacements.Count == 0;
        if (baseline.ProfileId != Id65AuthoringCompositeCompiler.ProfileId ||
            baseline.Manifest != manifest ||
            baseline.ManifestSha256 != manifest.CanonicalSha256 ||
            baseline.SourceRow80Sha256 !=
                Id65AuthoringCompositeCompiler.ExpectedLockedRow80Sha256 ||
            baseline.SourceModelSha256 !=
                Id65AuthoringCompositeCompiler.ExpectedLockedModelSha256 ||
            baseline.OutputModelSha256 != expectedOutputModelSha256 ||
            baseline.TargetOverlaySha256 != source.TargetOverlaySha256 ||
            baseline.GlobalExecutableSha256 != source.GlobalExecutableSha256 ||
            baseline.OwnedRanges.Count != expectedOwnedRangeCount ||
            baseline.OwnedRanges.Count(item => item.StableId.StartsWith(
                "texture.page.", StringComparison.Ordinal)) !=
                    ExpectedTextureOwnedRangeCount ||
            baseline.OwnedRanges.Count(item => item.StableId == "model.final") != 1 ||
            baseline.OwnedRanges.Count(item => item.OwnerId == "composite.v3-spawn") != 2 ||
            baseline.OwnedRanges.Any(item => item.StableId == "placement.t88") ||
            !placementIdentityValid ||
            baseline.WritesFileSystem ||
            baseline.WritesDiscImage ||
            baseline.WritesCue ||
            baseline.AppIntegrated ||
            baseline.CreateBinEnabled ||
            baseline.NormalCreateBinEnabled ||
            baseline.RuntimeCandidateAuthorized ||
            baseline.ReleaseIntegrated ||
            baseline.PromotionAuthorized ||
            baseline.Publishable)
        {
            throw new InvalidDataException(
                "The frozen optional-structural, T66, optional-T107 legacy " +
                "baseline changed.");
        }
    }

    private static byte[] BuildCatalogRow(
        Id65MobyDependencyBundleDescriptor bundle,
        Id65MobyPlacementIntent placement,
        Id65RemotePadGrassT107Position position)
    {
        if (bundle.Id != Id65RemotePadGrassT107Catalog.BundleId ||
            bundle.CanonicalSha256 !=
                Id65RemotePadGrassT107Catalog.BundleCanonicalSha256 ||
            bundle.Rows.Count != 1 ||
            bundle.Rows[0].Id != Id65RemotePadGrassT107Catalog.RowTemplateId ||
            bundle.Rows[0].ActorId != Id65RemotePadGrassT107Catalog.ActorId)
        {
            throw new InvalidDataException(
                "The exact catalog Grass bundle or row template changed.");
        }

        byte[] row = Convert.FromHexString(bundle.Rows[0].ExactHex);
        if (row.Length != RecordLength ||
            Hash(row) != bundle.Rows[0].Sha256)
        {
            throw new InvalidDataException(
                "The exact Grass row-template bytes changed.");
        }
        WriteInt32(row, 0, 0x8138);
        Id65AuthoringPoint coordinates = placement.CoordinatesOrDelta!.Value;
        WriteInt32(row, 0x0C, coordinates.X);
        WriteInt32(row, 0x10, coordinates.Y);
        WriteInt32(row, 0x14, coordinates.Z);
        row[0x47] = placement.YawByte!.Value;
        RequireHash(Hash(row), position.RowSha256, "catalog-derived T107 row");
        return row;
    }

    private static List<Id65AuthoringCompositeOwnedRange> RebuildLedger(
        Id65CompiledAuthoringComposite baseline,
        byte[] source,
        byte[]? catalogRow)
    {
        List<Id65AuthoringCompositeOwnedRange> ranges = [];
        foreach (Id65AuthoringCompositeOwnedRange range in baseline.OwnedRanges)
        {
            if (range.StableId == "placement.t107")
            {
                byte[] before =
                    source.AsSpan(T107Offset, RecordLength).ToArray();
                if (!before.SequenceEqual(range.CopyBefore()))
                {
                    throw new InvalidDataException(
                        "The legacy T107 ownership preimage changed.");
                }
                ranges.Add(new(
                    "placement.remote-pad.t107",
                    Id65RemotePadGrassT107Catalog.PlacementId,
                    T107Offset,
                    before,
                    catalogRow ?? throw new InvalidDataException(
                        "The enabled remote-pad T107 row is missing.")));
            }
            else
            {
                ranges.Add(range.DeepCopy());
            }
        }
        return ranges;
    }

    private static void ValidateLedger(
        byte[] source,
        IReadOnlyList<Id65AuthoringCompositeOwnedRange> ledger,
        bool grassT107Present)
    {
        string[] grassStableIds = grassT107Present
            ?
            [
                "asset.root37",
                "asset.actor-id37",
                "asset.package.01f5",
                "scene.object-count",
                "placement.remote-pad.t107",
                "scene.fixup-count",
                "scene.fixup.t107-properties",
                "asset.properties.grass"
            ]
            :
            [
                "asset.root37",
                "asset.actor-id37",
                "asset.package.01f5",
                "asset.properties.grass"
            ];
        (int Offset, int Length)[] expectedGrassRanges = grassT107Present
            ?
            [
                (0xE4, 4),
                (0x19A, 2),
                (0x1CFA44, 0x174),
                (0x1D016C, 4),
                (0x1D2638, 0x58),
                (0x1D7F28, 4),
                (0x1D8130, 4),
                (0x1D8138, 8)
            ]
            :
            [
                (0xE4, 4),
                (0x19A, 2),
                (0x1CFA44, 0x174),
                (0x1D8138, 8)
            ];
        int expectedOwnedRangeCount = grassT107Present
            ? ExpectedOwnedRangeCount
            : ExpectedOwnedRangeCountWithoutT107;
        int expectedGrassOwnedRangeCount = grassT107Present
            ? ExpectedGrassOwnedRangeCount
            : ExpectedGrassOwnedRangeCountWithoutT107;
        Id65AuthoringCompositeOwnedRange[] grass = ledger
            .Where(item =>
                grassStableIds.Contains(item.StableId, StringComparer.Ordinal))
            .ToArray();
        if (ledger.Count != expectedOwnedRangeCount ||
            ledger.Select(item => item.StableId)
                .Distinct(StringComparer.Ordinal).Count() != ledger.Count ||
            ledger.Count(item => item.StableId.StartsWith(
                "texture.page.", StringComparison.Ordinal)) !=
                    ExpectedTextureOwnedRangeCount ||
            ledger.Count(item => item.StableId == "model.final") != 1 ||
            ledger.Count(item => item.OwnerId == "composite.v3-spawn") != 2 ||
            ledger.Any(item => item.StableId == "placement.t88") ||
            grass.Length != expectedGrassOwnedRangeCount ||
            !grass.Select(item => (item.DataRelativeOffset, item.ByteLength))
                .SequenceEqual(expectedGrassRanges))
        {
            throw new InvalidDataException(
                "The schema-3 ownership ledger partition changed.");
        }

        for (int index = 0; index < ledger.Count; index++)
        {
            Id65AuthoringCompositeOwnedRange range = ledger[index];
            byte[] before = range.CopyBefore();
            byte[] after = range.CopyAfter();
            if (range.DataRelativeOffset < 0 ||
                range.ByteLength <= 0 ||
                range.DataRelativeOffset + (long)range.ByteLength >
                    Row80ByteLength ||
                before.Length != range.ByteLength ||
                after.Length != range.ByteLength ||
                Hash(before) != range.BeforeSha256 ||
                Hash(after) != range.AfterSha256 ||
                !source.AsSpan(range.DataRelativeOffset, range.ByteLength)
                    .SequenceEqual(before) ||
                before.SequenceEqual(after) ||
                index > 0 &&
                    RangesOverlap(
                        ledger[index - 1].DataRelativeOffset,
                        ledger[index - 1].ByteLength,
                        range.DataRelativeOffset,
                        range.ByteLength))
            {
                throw new InvalidDataException(
                    "Schema-3 owned range " + range.StableId +
                    " lost bounds, hash, preimage, or nonoverlap identity.");
            }
        }
    }

    private static int CountGrassOwnedRanges(
        IReadOnlyList<Id65AuthoringCompositeOwnedRange> ledger) =>
        ledger.Count(item => item.StableId is
            "asset.root37" or
            "asset.actor-id37" or
            "asset.package.01f5" or
            "scene.object-count" or
            "placement.remote-pad.t107" or
            "scene.fixup-count" or
            "scene.fixup.t107-properties" or
            "asset.properties.grass");

    private static void VerifyReadback(
        byte[] source,
        byte[] baselineOutput,
        byte[] output,
        byte[]? catalogRow,
        Id65RemotePadGrassT107Position position,
        bool structuralTerrainPresent,
        bool grassT107Present)
    {
        if (source.Length != Row80ByteLength ||
            baselineOutput.Length != Row80ByteLength ||
            output.Length != Row80ByteLength ||
            grassT107Present &&
                (catalogRow is null ||
                !output.AsSpan(T107Offset, RecordLength).SequenceEqual(catalogRow)) ||
            !grassT107Present && catalogRow is not null)
        {
            throw new InvalidDataException(
                "The schema-3 row-80 or T107 readback length changed.");
        }
        if (grassT107Present)
        {
            for (int index = 0; index < output.Length; index++)
            {
                if ((index < T107Offset ||
                        index >= T107Offset + RecordLength) &&
                    output[index] != baselineOutput[index])
                {
                    throw new InvalidDataException(
                        "The schema-3 wrapper changed a byte outside the owned T107 row.");
                }
            }
        }
        else if (!output.SequenceEqual(baselineOutput))
        {
            throw new InvalidDataException(
                "The schema-3 placement-free wrapper changed its legacy baseline.");
        }

        RequireHash(Hash(output.AsSpan(ModelOffset, ModelLength)),
            structuralTerrainPresent
                ? ExpectedOutputModelSha256
                : Id65V2NativeTextureCompositionCompiler.ExpectedOutputModelSha256,
            "schema-3 optional-structural T66 model");
        RequireHash(
            Hash(output.AsSpan(TexturePagesOffset, TexturePagesLength)),
            Id65V2NativeTextureCompositionCompiler.ExpectedOutputTexturePagesSha256,
            "schema-3 T66 pages");
        if (grassT107Present)
        {
            RequireHash(
                Hash(output.AsSpan(T107Offset, RecordLength)),
                position.RowSha256,
                "schema-3 remote-pad T107 row");
        }

        bool placementReadbackInvalid = grassT107Present
            ? ReadInt32(output, FixupAppendOffset) != 0x2638 ||
                ReadInt32(output, T107Offset) != 0x8138 ||
                ReadInt32(output, T107Offset + 0x0C) != position.RawX ||
                ReadInt32(output, T107Offset + 0x10) != position.RawY ||
                ReadInt32(output, T107Offset + 0x14) != position.RawZ ||
                ReadUInt16(output, T107Offset + 0x36) !=
                    Id65RemotePadGrassT107Catalog.ActorId ||
                output[T107Offset + 0x47] !=
                    Id65RemotePadGrassT107Catalog.YawByte ||
                CountFixup(output, 0x2638) != 1
            : !source.AsSpan(T107Offset, RecordLength)
                    .SequenceEqual(output.AsSpan(T107Offset, RecordLength)) ||
                CountFixup(output, 0x2638) != 0;
        if (!source.AsSpan(T88Offset, RecordLength)
                .SequenceEqual(output.AsSpan(T88Offset, RecordLength)) ||
            ReadUInt32(output, Root37Offset) != 0x1CFA44 ||
            ReadUInt16(output, ActorId37Offset) !=
                Id65RemotePadGrassT107Catalog.ActorId ||
            ReadInt32(output, ObjectCountOffset) !=
                (grassT107Present ? 108 : 107) ||
            ReadInt32(output, FixupCountOffset) !=
                (grassT107Present ? 0x82 : 0x81) ||
            CountFixup(output, 0x1FB0) != 1 ||
            CountFixup(output, 0x5AFC) != 1 ||
            placementReadbackInvalid)
        {
            throw new InvalidDataException(
                "The schema-3 T88 exclusion, Grass, or fixup readback changed.");
        }
        RequireHash(Hash(output.AsSpan(PackageOffset, PackageLength)),
            "90ca71a190c4567817d728753f25df667d56515e1721d24e19e6da9dc07edcc3",
            "schema-3 Grass package");
        RequireHash(Hash(output.AsSpan(PropertiesOffset, 8)),
            "9eeeff662fd5b77dbc35de8ed01e0d1fd149cee49126625b69f65553c4b7c20b",
            "schema-3 Grass properties");

        int collisionOffset = ModelOffset +
            (structuralTerrainPresent ? 0x2BF0C : 0x2BEEC);
        int supportTriangleOffset =
            collisionOffset + 4 + 0x1E400 +
            (Id65RemotePadGrassT107Catalog.SupportCollisionTrueIndex * 12);
        if (Convert.ToHexString(output.AsSpan(supportTriangleOffset, 12)) !=
            Id65RemotePadGrassT107Catalog.SupportCollisionPackedHex)
        {
            throw new InvalidDataException(
                "The schema-3 remote-pad support collision identity changed.");
        }
    }

    private static int CountFixup(byte[] row80, int fieldOffset)
    {
        int count = ReadInt32(row80, FixupCountOffset);
        int matches = 0;
        for (int index = 0; index < count; index++)
        {
            if (ReadInt32(row80, FixupListOffset + (index * 4)) == fieldOffset)
                matches++;
        }
        return matches;
    }

    private static byte[] ApplyOwnedRanges(
        byte[] input,
        IReadOnlyList<Id65AuthoringCompositeOwnedRange> ranges)
    {
        byte[] output = input.ToArray();
        foreach (Id65AuthoringCompositeOwnedRange range in ranges)
        {
            byte[] before = range.CopyBefore();
            if (!output.AsSpan(range.DataRelativeOffset, range.ByteLength)
                .SequenceEqual(before))
            {
                throw new InvalidDataException(
                    "Schema-3 transactional range " + range.StableId +
                    " lost its exact preimage.");
            }
            range.CopyAfter().CopyTo(output, range.DataRelativeOffset);
        }
        return output;
    }

    private static string HashOwnedRanges(
        IReadOnlyList<Id65AuthoringCompositeOwnedRange> ranges)
    {
        StringBuilder text = new();
        foreach (Id65AuthoringCompositeOwnedRange range in ranges)
        {
            text.Append(range.StableId).Append('|')
                .Append(range.OwnerId).Append('|')
                .Append(range.DataRelativeOffset.ToString(
                    "X8", CultureInfo.InvariantCulture)).Append('|')
                .Append(range.ByteLength).Append('|')
                .Append(range.BeforeSha256).Append('|')
                .Append(range.AfterSha256).Append('\n');
        }
        return Hash(Encoding.UTF8.GetBytes(text.ToString()));
    }

    private static string HashTransaction(
        IReadOnlyList<Id65AuthoringCompositeOwnedRange> ranges)
    {
        using IncrementalHash hash =
            IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (Id65AuthoringCompositeOwnedRange range in ranges)
        {
            hash.AppendData(Encoding.UTF8.GetBytes(
                range.StableId + "\n" + range.OwnerId + "\n"));
            hash.AppendData(UInt32(checked((uint)range.DataRelativeOffset)));
            hash.AppendData(UInt32(checked((uint)range.ByteLength)));
            hash.AppendData(range.CopyBefore());
            hash.AppendData(range.CopyAfter());
        }
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static string HashProtectedComplement(
        byte[] bytes,
        IReadOnlyList<Id65AuthoringCompositeOwnedRange> ranges)
    {
        using IncrementalHash hash =
            IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        int cursor = 0;
        foreach (Id65AuthoringCompositeOwnedRange range in ranges)
        {
            if (cursor < range.DataRelativeOffset)
            {
                hash.AppendData(bytes.AsSpan(
                    cursor,
                    range.DataRelativeOffset - cursor));
            }
            cursor = checked(range.DataRelativeOffset + range.ByteLength);
        }
        if (cursor < bytes.Length)
            hash.AppendData(bytes.AsSpan(cursor));
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static int CountChangedBytes(
        ReadOnlySpan<byte> source,
        ReadOnlySpan<byte> output)
    {
        if (source.Length != output.Length)
            throw new InvalidDataException(
                "Schema-3 changed-byte inputs have different lengths.");
        int changed = 0;
        for (int index = 0; index < source.Length; index++)
        {
            if (source[index] != output[index])
                changed++;
        }
        return changed;
    }

    private static bool RangesOverlap(
        int leftOffset,
        int leftLength,
        int rightOffset,
        int rightLength) =>
        leftOffset < rightOffset + rightLength &&
        rightOffset < leftOffset + leftLength;

    private static byte[] UInt32(uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
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
            throw new InvalidDataException(
                "A 32-bit unsigned read is out of range.");
        return BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(offset, 4));
    }

    private static ushort ReadUInt16(ReadOnlySpan<byte> bytes, int offset)
    {
        if (offset < 0 || offset + 2 > bytes.Length)
            throw new InvalidDataException(
                "A 16-bit read is out of range.");
        return BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(offset, 2));
    }

    private static void WriteInt32(Span<byte> bytes, int offset, int value)
    {
        if (offset < 0 || offset + 4 > bytes.Length)
            throw new InvalidDataException("A 32-bit write is out of range.");
        BinaryPrimitives.WriteInt32LittleEndian(bytes.Slice(offset, 4), value);
    }

    internal static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static void RequireHash(string actual, string expected, string label)
    {
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                label + " SHA-256 changed: expected " + expected +
                ", got " + actual + ".");
        }
    }
}
