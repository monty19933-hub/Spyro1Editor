using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Spyro.Editor.Core.Exporting;

internal sealed record Id65AuthoringTerrainSetLimits(
    int ModelByteCapacity = UnusedLevel65RemoteBlankIsolationConstruction.ModelByteLength,
    int AvailableV3TailBytes = UnusedLevel65RemoteBlankIsolationConstruction.OutputZeroTailByteCount,
    int MaximumOutputUsedModelBytes = UnusedLevel65RemoteBlankIsolationConstruction.ModelByteLength,
    int MaximumLowDetailVertexCount = 64,
    int MaximumHighDetailVertexCount = 256,
    int MaximumSectorFaceCount = 255,
    int MaximumCollisionTriangleCount = 0x7FFF,
    int CollisionTreeCapacityBytes = 0x6A60,
    int CollisionBlockCapacityBytes = UnusedLevel65RemoteBlankIsolationConstruction.CollisionBlocksCapacityBytes);

internal sealed record Id65AuthoringTerrainSetCapacityReadback(
    int ModelByteCapacity,
    int UsedModelBytes,
    int ZeroTailBytes,
    int EnvironmentBytes,
    int SectorBytes,
    int LowDetailVertexCount,
    int LowDetailFaceCount,
    int HighDetailVertexCount,
    int HighDetailFaceCount,
    int CollisionBytes,
    int CollisionTriangleCount,
    int CollisionBlocksUsedBytes,
    int CollisionTreeCapacityBytes,
    int CollisionBlockCapacityBytes);

internal sealed record Id65AuthoringTerrainUnitReadback(
    string UnitId,
    string AddedVertexHandle,
    int CatalogOrdinal,
    int LowDetailVertexIndex,
    int HighDetailVertexIndex,
    int LowDetailFaceIndex,
    int HighDetailFaceIndex,
    int CollisionTriangleIndex,
    string AddedVertexWordHex,
    string LowDetailFaceHex,
    string HighDetailFaceHex,
    string CollisionTriangleHex,
    long RenderNormalZ,
    long CollisionNormalZ,
    Id65AuthoringCollisionCell CollisionCell,
    int CollisionAssignment,
    int OcclusionGroupIndex);

internal sealed class Id65CompiledAuthoringTerrainSet
{
    private readonly byte[] _model;
    private readonly ReadOnlyCollection<Id65ModelComponentRelocation> _relocations;
    private readonly ReadOnlyCollection<Id65StableHandleRebase> _handleRebases;
    private readonly ReadOnlyCollection<Id65AuthoringTerrainUnitReadback> _units;
    private readonly IReadOnlyDictionary<string, (string Kind, Id65LogicalAddress Address)> _addresses;
    private readonly ReadOnlyCollection<Id65AuthoringTerrainSetCompiler.ComponentState> _components;

    internal Id65CompiledAuthoringTerrainSet(
        Id65AuthoringTerrainSetManifest manifest,
        byte[] model,
        string sectorSha256,
        string environmentSha256,
        string collisionTreeSha256,
        string collisionBlocksSha256,
        string collisionSha256,
        string protectedComplementSha256,
        IEnumerable<Id65ModelComponentRelocation> relocations,
        string relocationMapSha256,
        IEnumerable<Id65StableHandleRebase> handleRebases,
        string handleRebaseMapSha256,
        Id65AuthoringTerrainSetCapacityReadback capacity,
        IEnumerable<Id65AuthoringTerrainUnitReadback> units,
        IEnumerable<int> targetCellSequence,
        string deterministicPlanSha256,
        IReadOnlyDictionary<string, (string Kind, Id65LogicalAddress Address)> addresses,
        IEnumerable<Id65AuthoringTerrainSetCompiler.ComponentState> components)
    {
        Manifest = manifest;
        _model = model.ToArray();
        ModelSha256 = Id65AuthoringTerrainSetCompiler.Hash(_model);
        SectorSha256 = sectorSha256;
        EnvironmentSha256 = environmentSha256;
        CollisionTreeSha256 = collisionTreeSha256;
        CollisionBlocksSha256 = collisionBlocksSha256;
        CollisionSha256 = collisionSha256;
        ProtectedComplementSha256 = protectedComplementSha256;
        _relocations = Array.AsReadOnly(relocations.ToArray());
        RelocationMapSha256 = relocationMapSha256;
        _handleRebases = Array.AsReadOnly(handleRebases.ToArray());
        HandleRebaseMapSha256 = handleRebaseMapSha256;
        Capacity = capacity;
        _units = Array.AsReadOnly(units.ToArray());
        TargetCellSequence = Array.AsReadOnly(targetCellSequence.ToArray());
        DeterministicPlanSha256 = deterministicPlanSha256;
        _addresses = new ReadOnlyDictionary<string, (string Kind, Id65LogicalAddress Address)>(
            addresses.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));
        _components = Array.AsReadOnly(components.ToArray());
    }

    public Id65AuthoringTerrainSetManifest Manifest { get; }
    public string ManifestSha256 => Manifest.CanonicalSha256;
    public string ModelSha256 { get; }
    public string SectorSha256 { get; }
    public string EnvironmentSha256 { get; }
    public string CollisionTreeSha256 { get; }
    public string CollisionBlocksSha256 { get; }
    public string CollisionSha256 { get; }
    public string ProtectedComplementSha256 { get; }
    public IReadOnlyList<Id65ModelComponentRelocation> Relocations => _relocations;
    public string RelocationMapSha256 { get; }
    public IReadOnlyList<Id65StableHandleRebase> HandleRebases => _handleRebases;
    public string HandleRebaseMapSha256 { get; }
    public Id65AuthoringTerrainSetCapacityReadback Capacity { get; }
    public IReadOnlyList<Id65AuthoringTerrainUnitReadback> Units => _units;
    public IReadOnlyList<int> TargetCellSequence { get; }
    public string DeterministicPlanSha256 { get; }
    public bool ImmutableLockedV3SourceVerified => true;
    public bool SinglePassTargetRebuildVerified => true;
    public bool ExactHpLpRowsVerified => true;
    public bool NegativeCollisionWindingVerified => true;
    public bool CollisionCellTreeReadbackVerified => true;
    public bool SingleOcclusionGroupOwnershipVerified => true;
    public bool StableIdsAndRebaseMapVerified => true;
    public bool ProtectedComplementPreserved => true;
    public bool WritesFileSystem => false;
    public bool WritesDiscImage => false;
    public bool WritesCue => false;
    public bool AppIntegrated => false;
    public bool DocumentIntegrated => false;
    public bool PersistenceIntegrated => false;
    public bool MaterializerIntegrated => false;
    public bool CreateBinEnabled => false;
    public bool NormalCreateBinEnabled => false;
    public bool ReleaseIntegrated => false;
    public bool RuntimeCandidateAuthorized => false;
    public bool PhysicalAcceptanceComplete => false;
    public bool PromotionAuthorized => false;
    public bool Publishable => false;

    internal byte[] CopyModel() => _model.ToArray();
    internal IReadOnlyDictionary<string, (string Kind, Id65LogicalAddress Address)> CopyAddresses() =>
        _addresses.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
    internal IReadOnlyList<Id65AuthoringTerrainSetCompiler.ComponentState> CopyComponents() =>
        _components.Select(item => item with { Bytes = item.Bytes.ToArray() }).ToArray();
}

internal sealed class Id65CompiledAuthoringTerrainSetTransition
{
    private readonly byte[] _sourceModel;
    private readonly byte[] _outputModel;
    private readonly ReadOnlyCollection<Id65ModelComponentRelocation> _relocations;
    private readonly ReadOnlyCollection<Id65StableHandleRebase> _handleRebases;

    internal Id65CompiledAuthoringTerrainSetTransition(
        Id65CompiledAuthoringTerrainSet source,
        Id65CompiledAuthoringTerrainSet output,
        string changedUnitId,
        bool added,
        int changedModelByteCount,
        int diffRangeCount,
        string diffManifestSha256,
        IEnumerable<Id65ModelComponentRelocation> relocations,
        string relocationMapSha256,
        IEnumerable<Id65StableHandleRebase> handleRebases,
        string handleRebaseMapSha256,
        string deterministicPlanSha256)
    {
        Source = source;
        Output = output;
        ChangedUnitId = changedUnitId;
        Added = added;
        _sourceModel = source.CopyModel();
        _outputModel = output.CopyModel();
        ChangedModelByteCount = changedModelByteCount;
        DiffRangeCount = diffRangeCount;
        DiffManifestSha256 = diffManifestSha256;
        _relocations = Array.AsReadOnly(relocations.ToArray());
        RelocationMapSha256 = relocationMapSha256;
        _handleRebases = Array.AsReadOnly(handleRebases.ToArray());
        HandleRebaseMapSha256 = handleRebaseMapSha256;
        DeterministicPlanSha256 = deterministicPlanSha256;
    }

    public Id65CompiledAuthoringTerrainSet Source { get; }
    public Id65CompiledAuthoringTerrainSet Output { get; }
    public string ChangedUnitId { get; }
    public bool Added { get; }
    public int ChangedModelByteCount { get; }
    public int DiffRangeCount { get; }
    public string DiffManifestSha256 { get; }
    public IReadOnlyList<Id65ModelComponentRelocation> Relocations => _relocations;
    public string RelocationMapSha256 { get; }
    public IReadOnlyList<Id65StableHandleRebase> HandleRebases => _handleRebases;
    public string HandleRebaseMapSha256 { get; }
    public string DeterministicPlanSha256 { get; }
    public bool ExactPreimageRequired => true;
    public bool ExactAfterimageRequired => true;
    public bool ExactInverseVerified => true;
    public bool WritesFileSystem => false;
    public bool WritesDiscImage => false;
    public bool AppIntegrated => false;
    public bool CreateBinEnabled => false;
    public bool ReleaseIntegrated => false;
    public bool RuntimeCandidateAuthorized => false;
    public bool PhysicalAcceptanceComplete => false;
    public bool Publishable => false;

    internal byte[] CopySourceModel() => _sourceModel.ToArray();
    internal byte[] CopyOutputModel() => _outputModel.ToArray();
}

/// <summary>
/// Pure in-memory compiler for the closed four-state terrain set. Each target
/// is rebuilt directly from the immutable locked v3 source; target states are
/// never stacked from another optional-terrain afterimage.
/// </summary>
internal static class Id65AuthoringTerrainSetCompiler
{
    public const string ProfileId = Id65AuthoringTerrainSetCatalog.ProfileId;
    public const string ExpectedV3ModelSha256 =
        UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3Construction.ExpectedOutputModelSha256;
    public const string ExpectedTile1ModelSha256 =
        Id65AuthoringTerrainMutationCompiler.ExpectedTwoTileModelSha256;
    public const string ExpectedTile1SectorSha256 =
        Id65AuthoringTerrainMutationCompiler.ExpectedTwoTileSectorSha256;
    public const string ExpectedTile1EnvironmentSha256 =
        Id65AuthoringTerrainMutationCompiler.ExpectedTwoTileEnvironmentSha256;
    public const string ExpectedTile1BlocksSha256 =
        Id65AuthoringTerrainMutationCompiler.ExpectedTwoTileBlocksSha256;
    public const string ExpectedTile1CollisionSha256 =
        Id65AuthoringTerrainMutationCompiler.ExpectedTwoTileCollisionSha256;

    public const string ExpectedEmptyManifestSha256 =
        "979642648fd2fad60377fbe2fccd4a6eb5c0bd897be35fc524a37ba192a88164";
    public const string ExpectedTile1ManifestSha256 =
        "3574b051cea44c4191c37cfbf857ae9582a99b5ea142d5b08aa7dea41dc1ceac";
    public const string ExpectedTile2ManifestSha256 =
        "d14861c149cb4d7284cbe545014573f620edba522fb829832d24d945ee0024fd";
    public const string ExpectedBothManifestSha256 =
        "c6ce7aa18570486ceb75eff75cd81f4b2557cc954729c64972596e24015bc549";
    public const string ExpectedTile2ModelSha256 =
        "a8fd9d9018e2d27283fa02d7bd908dc28f24bf2b4dc47f128be71f77588bcfb5";
    public const string ExpectedBothModelSha256 =
        "84291d79c381b709ebff3d6c3cffa31b850d0da7b609bea9db9bcbfb2761b731";
    public const string ExpectedTile2SectorSha256 =
        "829faf5dd168e6cfef0dc05a87528645bdc981954b3c714e3a7e774d3cfe527a";
    public const string ExpectedBothSectorSha256 =
        "3a690995e737ebb4eb66889f49a09e517c00853d61b73c071cee8a99864974f7";
    public const string ExpectedTile2EnvironmentSha256 =
        "7056756e30b23ac55027597f0ec8e804e187a07bcfa1f0df625b0089899402ff";
    public const string ExpectedBothEnvironmentSha256 =
        "94be32457853681ae6c63c82733d106405b283e64a3e0d14b5ddea96cf4a712f";
    public const string ExpectedTile2BlocksSha256 =
        "f813f0708c5747e59ad559e43da7e44495cc7a4db60caf55a1fc97391003e719";
    public const string ExpectedBothBlocksSha256 =
        "1c5fd35a36897a91a2a682c5d9b030c872a17364466700172591f13169b94077";
    public const string ExpectedTile2CollisionSha256 =
        "fa61aeb8cc92a133092631801b0b01d632c9721f2637bcb1627913101afd046d";
    public const string ExpectedBothCollisionSha256 =
        "173774ecd1c310e248ac45f8f44bb2f0a05373c040834e804591ce77ab1f6f09";
    public const string ExpectedProtectedComplementSha256 =
        "e5dcf276be77eff6c1444ea78e402c54e7760d277fa498d1bae5a4ad318ec59c";
    public const string ExpectedCollisionTreeSha256 =
        "c77618c5a80fce590c802b197945348d0b4fc04ab666e9b0b911e3a6ff9b2cb9";
    public const string ExpectedEmptyRelocationMapSha256 =
        "f5cf00d9b70a6e65ad66ff32b66d70f680d740c0e6f8575f668f34276e2e37b7";
    public const string ExpectedEmptyHandleRebaseMapSha256 =
        "bdfcbf7c29bc5e3fce5d547daec340006da1c7e0d0dc93203abaa303fd8d1ced";
    public const string ExpectedEmptyDeterministicPlanSha256 =
        "a61ab73cbcf17dfba1a6527c97aa42e7cdfe7786e39ad9be9ab9ea3c28e23746";
    public const string ExpectedTile1RelocationMapSha256 =
        Id65AuthoringTerrainMutationCompiler.ExpectedAddRelocationMapSha256;
    public const string ExpectedTile1HandleRebaseMapSha256 =
        Id65AuthoringTerrainMutationCompiler.ExpectedAddHandleRebaseMapSha256;
    public const string ExpectedTile1DeterministicPlanSha256 =
        "919d1a65094683131e2f39ad4d2123f37592d6c04da110313d875250c79cfc4f";
    public const string ExpectedTile2RelocationMapSha256 =
        "c111184a82a6723380c6f7994ce5b7d9ff93508f1ef4f9b0d721f6d84a99f574";
    public const string ExpectedTile2HandleRebaseMapSha256 =
        "da4c5c2e90e97902c5a51c28d74d6df526de670d2bbdd38c658d73268e86be40";
    public const string ExpectedTile2DeterministicPlanSha256 =
        "c220b03e7c06c5353f9e5fcf64164a5727c12458651b8ad4712c2fbdb4e1ab01";
    public const string ExpectedBothRelocationMapSha256 =
        "2a8604e31d0d901c67d40bafeff7359a1412e43e925ffbd081feea06504d7554";
    public const string ExpectedBothHandleRebaseMapSha256 =
        "8a78a7e185b28fa71b61f75e1321419d2777e2630752b46098301f471b87a6e5";
    public const string ExpectedBothDeterministicPlanSha256 =
        "87dcbe16a98e786106943b74a604c576fc1af2b3855a58918ce2bdce64bd4e8b";

    private const int ModelLength = 0x94800;
    private const int BaseUsed = 0x9457C;
    private const int TextureOffset = 0;
    private const int TextureLength = 0x2F78;
    private const int BaseEnvironmentOffset = 0x2F78;
    private const int BaseEnvironmentLength = 0x28518;
    private const int BaseOcclusionOffset = 0x2B490;
    private const int OcclusionLength = 0x974;
    private const int BaseSpecialOffset = 0x2BE04;
    private const int SpecialLength = 0x30;
    private const int BaseCollisionOffset = 0x2BE34;
    private const int BaseCollisionLength = 0x5FAE8;
    private const int BaseCycloramaOffset = 0x8B91C;
    private const int CycloramaLength = 0x84E4;
    private const int BasePortalOffset = 0x93E00;
    private const int PortalLength = 4;
    private const int BaseParticlesOffset = 0x93E04;
    private const int ParticlesLength = 0x80;
    private const int BaseSoundOffset = 0x93E84;
    private const int SoundLength = 0x6F8;
    private const int SectorIndex = 216;
    private const int BaseSectorLength = 0x70;
    private const int CollisionTreeRelative = 0x1C;
    private const int CollisionBlocksRelative = 0x6A7C;
    private const int CollisionTrianglesRelative = 0x1E400;
    private const int BaseAssignmentsRelative = 0x58480;
    private const int BaseFlagsRelative = 0x5D1E0;
    private const int CollisionTreeCapacity = 0x6A60;
    private const int CollisionBlocksCapacity = 0x17984;
    private const int BaseBlocksUsed = 0x17960;
    private const int BaseTriangleCount = 19_808;
    private const int ExistingTriangleIndex = 13_995;
    private const int CollisionFlagsLength = 0x2904;
    private const string ExistingTriangleHex = "10011C701001380000020000";
    private static readonly Id65AuthoringCollisionCell TargetCell = new(1, 1, 2);

    public static Id65CompiledAuthoringTerrainSet CompileTarget(
        Id65AuthoringTerrainSetManifest target,
        UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3StaticPlan v3Template,
        Id65AuthoringTerrainSetLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(v3Template);
        limits ??= new();
        Id65TerrainTriangleIntent[] active = ValidateManifest(target);
        Id65CompiledAuthoringModel lockedV3 = Id65AuthoringModelCompiler.Compile(
            target.LockedV3Base,
            v3Template);
        byte[] v3Model = lockedV3.CopyOutputModel();
        if (v3Template.ProfileId != UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3Construction.ProfileId ||
            lockedV3.OutputModelSha256 != ExpectedV3ModelSha256 ||
            !lockedV3.NegativeCollisionWindingVerified)
        {
            throw new InvalidDataException("The terrain-set compiler requires the exact negative-winding v3 proof.");
        }
        RequireHash(Hash(v3Model), ExpectedV3ModelSha256, "locked v3 terrain-set source model");

        ModelState baseState = ParseBase(v3Model);
        ModelState targetState = active.Length == 0 ? baseState : BuildTargetState(baseState, active);
        ValidateLimits(limits, targetState);
        Id65ModelComponentRelocation[] relocations = BuildRelocations(
            baseState.Components,
            targetState.Components);
        Dictionary<string, (string Kind, Id65LogicalAddress Address)> baseAddresses =
            BuildAddresses(Array.Empty<Id65TerrainTriangleIntent>(), lockedV3);
        Dictionary<string, (string Kind, Id65LogicalAddress Address)> targetAddresses =
            BuildAddresses(active, lockedV3);
        Id65StableHandleRebase[] rebases = BuildRebases(baseAddresses, targetAddresses);
        string relocationHash = HashRelocations(relocations);
        string rebaseHash = HashHandleRebases(rebases);
        string protectedHash = HashProtectedComplement(targetState.Components);
        Id65AuthoringTerrainUnitReadback[] readbacks = BuildUnitReadbacks(targetState, active);
        Id65AuthoringTerrainSetCapacityReadback capacity = BuildCapacity(targetState);
        string deterministic = Hash(Encoding.UTF8.GetBytes(string.Join('\n',
        [
            ProfileId,
            target.CanonicalSha256,
            Hash(targetState.Model),
            targetState.SectorSha256,
            targetState.EnvironmentSha256,
            targetState.CollisionSha256,
            targetState.CollisionBlocksSha256,
            protectedHash,
            relocationHash,
            rebaseHash
        ])));
        ValidateFrozenTargetPins(
            target,
            targetState,
            protectedHash,
            relocationHash,
            rebaseHash,
            deterministic);
        return new(
            target,
            targetState.Model,
            targetState.SectorSha256,
            targetState.EnvironmentSha256,
            targetState.CollisionTreeSha256,
            targetState.CollisionBlocksSha256,
            targetState.CollisionSha256,
            protectedHash,
            relocations,
            relocationHash,
            rebases,
            rebaseHash,
            capacity,
            readbacks,
            targetState.TargetCellSequence,
            deterministic,
            targetAddresses,
            targetState.Components);
    }

    public static Id65CompiledAuthoringTerrainSetTransition CompileTransition(
        Id65AuthoringTerrainSetManifest source,
        Id65AuthoringTerrainSetManifest target,
        UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3StaticPlan v3Template,
        Id65AuthoringTerrainSetLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        Id65CompiledAuthoringTerrainSet before = CompileTarget(source, v3Template, limits);
        Id65CompiledAuthoringTerrainSet after = CompileTarget(target, v3Template, limits);
        HashSet<string> beforeIds = source.ActiveUnits.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        HashSet<string> afterIds = target.ActiveUnits.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        HashSet<string> changedIds = new(beforeIds, StringComparer.Ordinal);
        changedIds.SymmetricExceptWith(afterIds);
        string[] changed = changedIds.Order(StringComparer.Ordinal).ToArray();
        if (changed.Length != 1)
            throw new InvalidDataException("A terrain-set transition must add or remove exactly one catalog unit.");
        return BuildTransition(before, after, changed[0], afterIds.Contains(changed[0]));
    }

    public static Id65CompiledAuthoringTerrainSetTransition Invert(
        Id65CompiledAuthoringTerrainSetTransition transition)
    {
        ArgumentNullException.ThrowIfNull(transition);
        return BuildTransition(
            transition.Output,
            transition.Source,
            transition.ChangedUnitId,
            !transition.Added);
    }

    public static byte[] ApplyTransactional(
        Id65CompiledAuthoringTerrainSetTransition transition,
        byte[] input,
        bool reverse = false)
    {
        ArgumentNullException.ThrowIfNull(transition);
        ArgumentNullException.ThrowIfNull(input);
        byte[] expected = reverse ? transition.CopyOutputModel() : transition.CopySourceModel();
        byte[] output = reverse ? transition.CopySourceModel() : transition.CopyOutputModel();
        if (!input.SequenceEqual(expected))
        {
            throw new InvalidDataException(
                $"The terrain-set transaction {(reverse ? "afterimage" : "preimage")} changed.");
        }
        return output;
    }

    /// <summary>
    /// Independently decodes the emitted scene/collision bytes without consulting
    /// the construction-row helpers or the frozen output hashes. Kept internal so
    /// focused smoke can prove semantic corruption is rejected before any writer
    /// or runtime integration exists.
    /// </summary>
    internal static IReadOnlyList<Id65AuthoringTerrainUnitReadback> DecodeOutputSemantics(
        Id65AuthoringTerrainSetManifest manifest,
        byte[] model)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(model);
        Id65TerrainTriangleIntent[] active = ValidateManifest(manifest);
        int count = active.Length;
        int environmentLength = BaseEnvironmentLength + (count * 0x20);
        int occlusionOffset = BaseOcclusionOffset + (count * 0x20);
        int collisionOffset = BaseCollisionOffset + (count * 0x20);
        int assignmentsRelative = BaseAssignmentsRelative + (count * 12);
        int flagsRelative = Align4(assignmentsRelative + BaseTriangleCount + count);
        int collisionLength = checked(4 + flagsRelative + CollisionFlagsLength);
        RequireRange(model, BaseEnvironmentOffset, environmentLength, "decoded terrain-set environment");
        RequireRange(model, occlusionOffset, OcclusionLength, "decoded terrain-set occlusion");
        RequireRange(model, collisionOffset, collisionLength, "decoded terrain-set collision");
        byte[] environment = model.AsSpan(BaseEnvironmentOffset, environmentLength).ToArray();
        byte[] sector = ExtractSector(environment, BaseSectorLength + (count * 0x20));
        byte[] occlusion = model.AsSpan(occlusionOffset, OcclusionLength).ToArray();
        byte[] collision = model.AsSpan(collisionOffset, collisionLength).ToArray();
        int[] targetSequence = active.Select((_, index) => BaseTriangleCount + index)
            .Reverse().Append(ExistingTriangleIndex).ToArray();
        return DecodeUnitEvidence(sector, collision, occlusion, active, targetSequence);
    }

    private static Id65CompiledAuthoringTerrainSetTransition BuildTransition(
        Id65CompiledAuthoringTerrainSet source,
        Id65CompiledAuthoringTerrainSet output,
        string changedUnitId,
        bool added)
    {
        byte[] sourceModel = source.CopyModel();
        byte[] outputModel = output.CopyModel();
        ModelDiffProof diff = BuildDiffProof(sourceModel, outputModel);
        Id65ModelComponentRelocation[] relocations = BuildRelocations(
            source.CopyComponents(),
            output.CopyComponents());
        Id65StableHandleRebase[] rebases = BuildRebases(
            source.CopyAddresses(),
            output.CopyAddresses());
        string relocationHash = HashRelocations(relocations);
        string rebaseHash = HashHandleRebases(rebases);
        string deterministic = Hash(Encoding.UTF8.GetBytes(string.Join('\n',
        [
            ProfileId,
            added ? "add" : "remove",
            changedUnitId,
            source.ManifestSha256,
            output.ManifestSha256,
            source.ModelSha256,
            output.ModelSha256,
            diff.ManifestSha256,
            relocationHash,
            rebaseHash
        ])));
        Id65CompiledAuthoringTerrainSetTransition result = new(
            source,
            output,
            changedUnitId,
            added,
            diff.ChangedByteCount,
            diff.RangeCount,
            diff.ManifestSha256,
            relocations,
            relocationHash,
            rebases,
            rebaseHash,
            deterministic);
        byte[] applied = ApplyTransactional(result, sourceModel);
        byte[] reversed = ApplyTransactional(result, applied, reverse: true);
        if (!applied.SequenceEqual(outputModel) || !reversed.SequenceEqual(sourceModel))
            throw new InvalidDataException("The terrain-set transition is not exactly byte-invertible.");
        return result;
    }

    private static Id65TerrainTriangleIntent[] ValidateManifest(Id65AuthoringTerrainSetManifest manifest)
    {
        Id65AuthoringManifest expectedBase = Id65AuthoringModelCompiler.CreateMinimalV3EquivalentManifest();
        if (manifest.ProfileId != ProfileId ||
            manifest.LockedV3Base.CanonicalJson != expectedBase.CanonicalJson ||
            manifest.LockedV3Base.CanonicalSha256 != expectedBase.CanonicalSha256)
        {
            throw new InvalidDataException("The immutable minimal-v3 terrain-set base manifest changed.");
        }
        if (manifest.ActiveUnits.Count > 2)
            throw new InvalidDataException("The closed terrain-set catalog accepts at most two units.");
        if (manifest.ActiveUnits.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() !=
            manifest.ActiveUnits.Count)
        {
            throw new InvalidDataException("The terrain-set manifest contains a duplicate unit ID.");
        }
        foreach (Id65TerrainTriangleIntent unit in manifest.ActiveUnits)
        {
            Id65TerrainTriangleIntent expected = Id65AuthoringTerrainSetCatalog.ExactById(unit.Id);
            ValidateWinding(unit);
            if (unit != expected)
            {
                throw new InvalidDataException(
                    $"Terrain unit {unit.Id} differs from its exact positive-render/negative-collision catalog witness.");
            }
        }
        return Id65AuthoringTerrainSetCatalog.All
            .Where(catalog => manifest.ActiveUnits.Any(item => item.Id == catalog.Id))
            .ToArray();
    }

    private static void ValidateWinding(Id65TerrainTriangleIntent unit)
    {
        Dictionary<string, Id65AuthoringPoint> points = new(StringComparer.Ordinal)
        {
            ["vertex.remote-pad.a"] = new(272, 272, 512),
            ["vertex.remote-pad.b"] = new(496, 272, 512),
            ["vertex.remote-pad.c"] = new(384, 496, 512),
            [unit.AddedVertex.Handle] = unit.AddedVertex.Point
        };
        if (!points.TryGetValue(unit.RenderA, out Id65AuthoringPoint renderA) ||
            !points.TryGetValue(unit.RenderB, out Id65AuthoringPoint renderB) ||
            !points.TryGetValue(unit.RenderC, out Id65AuthoringPoint renderC) ||
            !points.TryGetValue(unit.CollisionA, out Id65AuthoringPoint collisionA) ||
            !points.TryGetValue(unit.CollisionB, out Id65AuthoringPoint collisionB) ||
            !points.TryGetValue(unit.CollisionC, out Id65AuthoringPoint collisionC) ||
            NormalZ(renderA, renderB, renderC) != 25_088 ||
            NormalZ(collisionA, collisionB, collisionC) != -25_088)
        {
            throw new InvalidDataException(
                $"Terrain unit {unit.Id} lost exact positive render / negative collision winding.");
        }
    }

    private static ModelState ParseBase(byte[] model)
    {
        RequireZeroTail(model, BaseUsed);
        ComponentState[] components =
        [
            Component(model, "texture", TextureOffset, TextureLength),
            Component(model, "environment", BaseEnvironmentOffset, BaseEnvironmentLength),
            Component(model, "occlusion", BaseOcclusionOffset, OcclusionLength),
            Component(model, "special-surface", BaseSpecialOffset, SpecialLength),
            Component(model, "collision", BaseCollisionOffset, BaseCollisionLength),
            Component(model, "cyclorama", BaseCycloramaOffset, CycloramaLength),
            Component(model, "portal-table", BasePortalOffset, PortalLength, false),
            Component(model, "particles", BaseParticlesOffset, ParticlesLength),
            Component(model, "sound", BaseSoundOffset, SoundLength)
        ];
        byte[] environment = components[1].Bytes;
        byte[] sector = ExtractSector(environment, BaseSectorLength);
        RequireHash(Hash(environment),
            UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3Construction.ExpectedOutputEnvironmentSha256,
            "locked v3 terrain-set environment");
        RequireHash(Hash(sector),
            UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3Construction.ExpectedNewSectorSha256,
            "locked v3 terrain-set sector");
        ValidateSectorCounts(sector, 3, 1);
        ValidateOcclusion(components[2].Bytes);
        CollisionValidation collision = ValidateCollision(
            components[4].Bytes,
            BaseTriangleCount,
            BaseBlocksUsed,
            [ExistingTriangleIndex],
            Array.Empty<Id65TerrainTriangleIntent>());
        return new(
            model.ToArray(),
            components,
            BaseUsed,
            BaseEnvironmentLength,
            BaseSectorLength,
            3,
            1,
            BaseCollisionLength,
            BaseTriangleCount,
            BaseBlocksUsed,
            [ExistingTriangleIndex],
            Hash(sector),
            Hash(environment),
            collision.TreeSha256,
            collision.BlocksSha256,
            collision.CollisionSha256);
    }

    private static ModelState BuildTargetState(ModelState baseState, Id65TerrainTriangleIntent[] active)
    {
        byte[] environment = BuildEnvironment(baseState.Components[1].Bytes, active);
        byte[] collision = BuildCollision(baseState.Components[4].Bytes, active);
        Dictionary<string, byte[]> replacements = new(StringComparer.Ordinal)
        {
            ["environment"] = environment,
            ["collision"] = collision
        };
        byte[] model = new byte[ModelLength];
        List<ComponentState> components = [];
        int cursor = 0;
        foreach (ComponentState source in baseState.Components)
        {
            byte[] bytes = replacements.TryGetValue(source.Name, out byte[]? replacement)
                ? replacement
                : source.Bytes;
            bytes.CopyTo(model, cursor);
            components.Add(new(source.Name, cursor, bytes.Length, bytes.ToArray(), Hash(bytes)));
            cursor += bytes.Length;
        }
        RequireZeroTail(model, cursor);
        foreach (string name in ProtectedComponentNames)
        {
            if (!baseState.Components.Single(item => item.Name == name).Bytes.SequenceEqual(
                    components.Single(item => item.Name == name).Bytes))
            {
                throw new InvalidDataException($"Protected component {name} changed instead of relocating intact.");
            }
        }
        byte[] sector = ExtractSector(environment, BaseSectorLength + (active.Length * 0x20));
        ValidateSectorCounts(sector, 3 + active.Length, 1 + active.Length);
        ValidateOcclusion(components.Single(item => item.Name == "occlusion").Bytes);
        int[] targetSequence = active.Select((_, index) => BaseTriangleCount + index)
            .Reverse().Append(ExistingTriangleIndex).ToArray();
        CollisionValidation collisionReadback = ValidateCollision(
            collision,
            BaseTriangleCount + active.Length,
            BaseBlocksUsed + (active.Length * 2),
            targetSequence,
            active);
        return new(
            model,
            components,
            cursor,
            environment.Length,
            sector.Length,
            3 + active.Length,
            1 + active.Length,
            collision.Length,
            BaseTriangleCount + active.Length,
            BaseBlocksUsed + (active.Length * 2),
            targetSequence,
            Hash(sector),
            Hash(environment),
            collisionReadback.TreeSha256,
            collisionReadback.BlocksSha256,
            collisionReadback.CollisionSha256);
    }

    private static byte[] BuildEnvironment(byte[] baseEnvironment, Id65TerrainTriangleIntent[] active)
    {
        int sectorOffset = checked(4 + ReadInt32(baseEnvironment, 8 + (SectorIndex * 4)));
        if (baseEnvironment.Length != BaseEnvironmentLength ||
            ReadInt32(baseEnvironment, 0) != BaseEnvironmentLength ||
            ReadInt32(baseEnvironment, 4) != 217 ||
            sectorOffset + BaseSectorLength != baseEnvironment.Length)
        {
            throw new InvalidDataException("The exact locked v3 environment layout changed.");
        }
        byte[] source = baseEnvironment.AsSpan(sectorOffset, BaseSectorLength).ToArray();
        byte[] sector = new byte[BaseSectorLength + (active.Length * 0x20)];
        byte[] header = source.AsSpan(0, 28).ToArray();
        header[16] = checked((byte)(3 + active.Length));
        header[18] = checked((byte)(1 + active.Length));
        header[20] = checked((byte)(3 + active.Length));
        header[22] = checked((byte)(1 + active.Length));
        int cursor = 0;
        void Append(ReadOnlySpan<byte> bytes)
        {
            bytes.CopyTo(sector.AsSpan(cursor));
            cursor += bytes.Length;
        }
        Append(header);
        Append(source.AsSpan(28, 12));
        foreach (Id65TerrainTriangleIntent unit in active)
            Append(Convert.FromHexString(VertexHex(unit.Id)));
        Append(source.AsSpan(40, 12));
        Append(source.AsSpan(52, 8));
        for (int index = 0; index < active.Length; index++)
            Append(Convert.FromHexString(LowFaceHex(active[index].Id, active.Length)));
        Append(source.AsSpan(60, 12));
        foreach (Id65TerrainTriangleIntent unit in active)
            Append(Convert.FromHexString(VertexHex(unit.Id)));
        Append(source.AsSpan(72, 24));
        Append(source.AsSpan(96, 16));
        for (int index = 0; index < active.Length; index++)
            Append(Convert.FromHexString(HighFaceHex(active[index].Id, active.Length)));
        if (cursor != sector.Length)
            throw new InvalidDataException("The terrain-set sector tables did not consume their exact byte span.");
        Id65EnvironmentSectorHeaderContract.Stamp(sector);
        _ = Id65EnvironmentSectorHeaderContract.ReadExactSector(sector, "terrain-set sector 216");
        byte[] output = new byte[BaseEnvironmentLength + (active.Length * 0x20)];
        baseEnvironment.AsSpan(0, sectorOffset).CopyTo(output);
        sector.CopyTo(output, sectorOffset);
        WriteInt32(output, 0, output.Length);
        return output;
    }

    private static byte[] BuildCollision(byte[] baseCollision, Id65TerrainTriangleIntent[] active)
    {
        CollisionLayout layout = ParseCollisionLayout(
            baseCollision,
            BaseTriangleCount,
            BaseAssignmentsRelative,
            BaseFlagsRelative);
        NativeCollisionIndex native = DecodeCollisionIndex(
            baseCollision.AsSpan(layout.TreeOffset, CollisionTreeCapacity).ToArray(),
            baseCollision.AsSpan(layout.BlocksOffset, CollisionBlocksCapacity).ToArray(),
            BaseBlocksUsed,
            BaseTriangleCount);
        if (!native.Cells.TryGetValue(TargetCell, out CollisionCellBinding? target) ||
            !target.OrderedTriangleIndexes.SequenceEqual([ExistingTriangleIndex]))
        {
            throw new InvalidDataException("The locked v3 target cell no longer owns only T13995.");
        }
        int triangleCount = BaseTriangleCount + active.Length;
        int[] targetSequence = active.Select((_, index) => BaseTriangleCount + index)
            .Reverse().Append(ExistingTriangleIndex).ToArray();
        Dictionary<Id65AuthoringCollisionCell, int[]> desired = native.Cells.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.OrderedTriangleIndexes.ToArray());
        desired[TargetCell] = targetSequence;
        CollisionRepack repack = RepackCollisionIndex(native, desired, triangleCount);
        int expectedBlocksUsed = BaseBlocksUsed + (active.Length * 2);
        if (!repack.ChangedCells.SequenceEqual([TargetCell]) || repack.UsedBlockBytes != expectedBlocksUsed)
            throw new InvalidDataException("The terrain set changed unexpected collision cells or block usage.");
        int assignmentsRelative = BaseAssignmentsRelative + (active.Length * 12);
        int flagsRelative = Align4(assignmentsRelative + triangleCount);
        int collisionLength = checked(4 + flagsRelative + CollisionFlagsLength);
        byte[] output = new byte[collisionLength];
        WriteInt32(output, 0, collisionLength);
        WriteInt32(output, 4, triangleCount);
        WriteInt32(output, 8, CollisionFlagsLength);
        WriteInt32(output, 12, CollisionTreeRelative);
        WriteInt32(output, 16, CollisionBlocksRelative);
        WriteInt32(output, 20, CollisionTrianglesRelative);
        WriteInt32(output, 24, assignmentsRelative);
        WriteInt32(output, 28, flagsRelative);
        int treeOffset = 4 + CollisionTreeRelative;
        int blocksOffset = 4 + CollisionBlocksRelative;
        int trianglesOffset = 4 + CollisionTrianglesRelative;
        int assignmentsOffset = 4 + assignmentsRelative;
        int flagsOffset = 4 + flagsRelative;
        repack.TreeBytes.CopyTo(output, treeOffset);
        repack.BlockBytes.CopyTo(output, blocksOffset);
        baseCollision.AsSpan(layout.TriangleOffset, BaseTriangleCount * 12)
            .CopyTo(output.AsSpan(trianglesOffset));
        for (int index = 0; index < active.Length; index++)
        {
            Convert.FromHexString(CollisionHex(active[index].Id)).CopyTo(
                output,
                trianglesOffset + ((BaseTriangleCount + index) * 12));
        }
        baseCollision.AsSpan(layout.AssignmentsOffset, BaseTriangleCount)
            .CopyTo(output.AsSpan(assignmentsOffset));
        for (int index = 0; index < active.Length; index++)
            output[assignmentsOffset + BaseTriangleCount + index] = 0;
        baseCollision.AsSpan(layout.FlagsOffset, CollisionFlagsLength).CopyTo(output.AsSpan(flagsOffset));
        return output;
    }

    private static CollisionValidation ValidateCollision(
        byte[] collision,
        int triangleCount,
        int usedBlocks,
        IReadOnlyList<int> expectedTargetSequence,
        IReadOnlyList<Id65TerrainTriangleIntent> active)
    {
        int assignmentsRelative = BaseAssignmentsRelative + ((triangleCount - BaseTriangleCount) * 12);
        int flagsRelative = Align4(assignmentsRelative + triangleCount);
        CollisionLayout layout = ParseCollisionLayout(collision, triangleCount, assignmentsRelative, flagsRelative);
        byte[] tree = collision.AsSpan(layout.TreeOffset, CollisionTreeCapacity).ToArray();
        byte[] blocks = collision.AsSpan(layout.BlocksOffset, CollisionBlocksCapacity).ToArray();
        NativeCollisionIndex index = DecodeCollisionIndex(tree, blocks, usedBlocks, triangleCount);
        if (!index.Cells.TryGetValue(TargetCell, out CollisionCellBinding? target) ||
            !target.OrderedTriangleIndexes.SequenceEqual(expectedTargetSequence))
        {
            throw new InvalidDataException("Collision cell (1,1,2) failed terrain-set sequence readback.");
        }
        string existing = Convert.ToHexString(collision.AsSpan(
            layout.TriangleOffset + (ExistingTriangleIndex * 12),
            12));
        if (existing != ExistingTriangleHex || collision[layout.AssignmentsOffset + ExistingTriangleIndex] != 0)
            throw new InvalidDataException("The locked negative-winding T13995 row or assignment changed.");
        for (int ordinal = 0; ordinal < active.Count; ordinal++)
        {
            int triangle = BaseTriangleCount + ordinal;
            if (collision[layout.AssignmentsOffset + triangle] != active[ordinal].CollisionAssignment)
                throw new InvalidDataException($"Terrain unit {active[ordinal].Id} collision assignment changed.");
        }
        return new(Hash(tree), Hash(blocks), Hash(collision));
    }

    private static CollisionLayout ParseCollisionLayout(
        byte[] collision,
        int triangleCount,
        int assignmentsRelative,
        int flagsRelative)
    {
        if (ReadInt32(collision, 0) != collision.Length ||
            ReadInt32(collision, 4) != triangleCount ||
            ReadInt32(collision, 8) != CollisionFlagsLength ||
            ReadInt32(collision, 12) != CollisionTreeRelative ||
            ReadInt32(collision, 16) != CollisionBlocksRelative ||
            ReadInt32(collision, 20) != CollisionTrianglesRelative ||
            ReadInt32(collision, 24) != assignmentsRelative ||
            ReadInt32(collision, 28) != flagsRelative)
        {
            throw new InvalidDataException("The terrain-set collision component header changed.");
        }
        int tree = 4 + CollisionTreeRelative;
        int blocks = 4 + CollisionBlocksRelative;
        int triangles = 4 + CollisionTrianglesRelative;
        int assignments = 4 + assignmentsRelative;
        int flags = 4 + flagsRelative;
        if (blocks - tree != CollisionTreeCapacity ||
            triangles - blocks != CollisionBlocksCapacity ||
            triangles + (triangleCount * 12) != assignments ||
            assignments + triangleCount > flags ||
            flags + CollisionFlagsLength != collision.Length)
        {
            throw new InvalidDataException("The terrain-set collision partitions changed.");
        }
        return new(tree, blocks, triangles, assignments, flags);
    }

    private static NativeCollisionIndex DecodeCollisionIndex(
        byte[] tree,
        byte[] blocks,
        int usedBlockBytes,
        int triangleCount)
    {
        if (usedBlockBytes <= 0 || (usedBlockBytes & 1) != 0 || usedBlockBytes > blocks.Length ||
            blocks.AsSpan(usedBlockBytes).IndexOfAnyExcept((byte)0) >= 0)
        {
            throw new InvalidDataException("The collision block used length or zero tail is invalid.");
        }
        int usedWords = usedBlockBytes / 2;
        List<int> starts = [];
        for (int word = 0; word < usedWords; word++)
        {
            if ((ReadUInt16(blocks, word * 2) & 0x8000) != 0)
                starts.Add(word);
        }
        if (starts.Count < 2 || starts[^1] != usedWords - 1 ||
            ReadUInt16(blocks, starts[^1] * 2) != 0x8000)
        {
            throw new InvalidDataException("The collision blocks lack the exact terminal sentinel.");
        }
        Dictionary<int, int[]> groups = [];
        for (int group = 0; group + 1 < starts.Count; group++)
        {
            int start = starts[group];
            int end = starts[group + 1];
            int[] sequence = new int[end - start];
            for (int index = 0; index < sequence.Length; index++)
            {
                ushort value = ReadUInt16(blocks, (start + index) * 2);
                if ((index == 0) != ((value & 0x8000) != 0))
                    throw new InvalidDataException("A collision block group marker is invalid.");
                int triangle = value & 0x7FFF;
                if (triangle >= triangleCount)
                    throw new InvalidDataException("A collision block references an out-of-range triangle.");
                sequence[index] = triangle;
            }
            if (!IsStrictlyDescending(sequence))
                throw new InvalidDataException("A native collision cell sequence is not strictly descending.");
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
        Dictionary<Id65AuthoringCollisionCell, CollisionCellBinding> cells = [];
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
        {
            throw new InvalidDataException("The collision blocks contain an unreferenced group.");
        }
        return new(tree.ToArray(), blocks.ToArray(), cells);
    }

    private static CollisionRepack RepackCollisionIndex(
        NativeCollisionIndex native,
        IReadOnlyDictionary<Id65AuthoringCollisionCell, int[]> desired,
        int triangleCount)
    {
        if (desired.Count != native.Cells.Count)
            throw new InvalidDataException("The collision repack changed cell ownership.");
        HashSet<Id65AuthoringCollisionCell> changed = [];
        Dictionary<string, int> emitted = new(StringComparer.Ordinal);
        Dictionary<Id65AuthoringCollisionCell, int> offsets = [];
        List<ushort> words = [];
        foreach (CollisionCellBinding binding in native.Cells.Values
                     .OrderBy(item => item.SourceBlockWordOffset)
                     .ThenBy(item => item.Cell.Z)
                     .ThenBy(item => item.Cell.Y)
                     .ThenBy(item => item.Cell.X))
        {
            if (!desired.TryGetValue(binding.Cell, out int[]? sequence) || sequence.Length == 0 ||
                sequence.Any(index => index < 0 || index >= triangleCount || index > 0x7FFF) ||
                !IsStrictlyDescending(sequence))
            {
                throw new InvalidDataException("A desired collision-cell sequence is invalid.");
            }
            if (!sequence.SequenceEqual(binding.OrderedTriangleIndexes))
                changed.Add(binding.Cell);
            string key = string.Join(',', sequence);
            if (!emitted.TryGetValue(key, out int wordOffset))
            {
                wordOffset = words.Count;
                for (int index = 0; index < sequence.Length; index++)
                {
                    ushort value = checked((ushort)sequence[index]);
                    if (index == 0)
                        value |= 0x8000;
                    words.Add(value);
                }
                emitted.Add(key, wordOffset);
            }
            offsets.Add(binding.Cell, wordOffset);
        }
        words.Add(0x8000);
        int usedBytes = checked(words.Count * 2);
        if (usedBytes > CollisionBlocksCapacity)
            throw new InvalidDataException("The collision repack exceeded its fixed block capacity.");
        byte[] tree = native.TreeBytes.ToArray();
        foreach (CollisionCellBinding binding in native.Cells.Values)
            WriteUInt16(tree, binding.TreePointerByteOffset, checked((ushort)offsets[binding.Cell]));
        byte[] blocks = new byte[native.BlockBytes.Length];
        for (int index = 0; index < words.Count; index++)
            WriteUInt16(blocks, index * 2, words[index]);
        NativeCollisionIndex readback = DecodeCollisionIndex(tree, blocks, usedBytes, triangleCount);
        foreach ((Id65AuthoringCollisionCell cell, CollisionCellBinding binding) in readback.Cells)
        {
            if (!desired[cell].SequenceEqual(binding.OrderedTriangleIndexes))
                throw new InvalidDataException($"Collision cell {cell} failed semantic readback.");
        }
        return new(
            tree,
            blocks,
            usedBytes,
            changed.OrderBy(item => item.Z).ThenBy(item => item.Y).ThenBy(item => item.X).ToArray());
    }

    private static void ValidateSectorCounts(byte[] sector, int vertices, int faces)
    {
        Id65EnvironmentSectorHeaderReadback header =
            Id65EnvironmentSectorHeaderContract.ReadExactSector(sector, "terrain-set sector 216");
        if (sector[16] != vertices || sector[17] != 3 || sector[18] != faces ||
            sector[20] != vertices || sector[21] != 3 || sector[22] != faces)
        {
            throw new InvalidDataException("Sector 216 HP/LP terrain-set counts changed.");
        }
        int expectedLength = checked((7 + vertices + 3 + (faces * 2) +
                                      vertices + 6 + (faces * 4)) * 4);
        if (sector.Length != expectedLength ||
            header.HighDetailVertexWordOffset != vertices + 3 + (2 * faces))
        {
            throw new InvalidDataException("Sector 216 terrain-set tables do not partition exactly.");
        }
    }

    private static void ValidateOcclusion(byte[] occlusion)
    {
        _ = ReadSingleOcclusionOwnerGroup(occlusion);
    }

    private static int ReadSingleOcclusionOwnerGroup(byte[] occlusion)
    {
        if (occlusion.Length != OcclusionLength || ReadInt32(occlusion, 0) != occlusion.Length ||
            ReadInt32(occlusion, 8) != 16)
        {
            throw new InvalidDataException("The fixed v3 occlusion directory changed.");
        }
        int environmentEnd = 4 + ReadInt32(occlusion, 4);
        int owners = 0;
        int ownerGroup = -1;
        for (int group = 0; group < 16; group++)
        {
            int offset = 4 + ReadInt32(occlusion, 12 + (group * 4));
            if (offset < 76 || offset >= environmentEnd)
                throw new InvalidDataException("An occlusion group pointer is out of bounds.");
            while (offset < environmentEnd && occlusion[offset] != 0xFF)
            {
                if (occlusion[offset] == SectorIndex)
                {
                    owners++;
                    ownerGroup = group;
                }
                offset++;
            }
            if (offset >= environmentEnd)
                throw new InvalidDataException("An occlusion group has no terminator.");
        }
        if (owners != 1)
            throw new InvalidDataException("Sector 216 must have exactly one occlusion owner.");
        if (ownerGroup != 0)
            throw new InvalidDataException("Sector 216 escaped occlusion group 0.");
        return ownerGroup;
    }

    private static int[] DecodeLowFaceSlots(uint word) =>
        [(int)((word >> 26) & 0x3F), (int)((word >> 20) & 0x3F), (int)((word >> 14) & 0x3F)];

    private static int[] DecodeHighFaceSlots(ReadOnlySpan<byte> slots)
    {
        if (slots.Length != 4 || slots[0] != slots[1])
            throw new InvalidDataException("A decoded HP face lost its repeated leading corner slot.");
        return [slots[0], slots[2], slots[3]];
    }

    private static string[] ResolveRenderHandles(
        IReadOnlyList<int> slots,
        IReadOnlyList<Id65AuthoringPoint> points,
        IReadOnlyList<(string Handle, Id65AuthoringPoint Point)> catalogVertices,
        string lod,
        string unitId)
    {
        if (slots.Count != 3 || slots.Any(index => index < 0 || index >= points.Count))
            throw new InvalidDataException($"Decoded {lod} face slots for {unitId} exceed the vertex table.");
        return slots.Select(index => ResolvePointHandle(points[index], catalogVertices, lod, unitId)).ToArray();
    }

    private static string ResolvePointHandle(
        Id65AuthoringPoint point,
        IReadOnlyList<(string Handle, Id65AuthoringPoint Point)> catalogVertices,
        string source,
        string unitId)
    {
        (string Handle, Id65AuthoringPoint Point)[] matches = catalogVertices
            .Where(item => item.Point == point)
            .ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidDataException(
                $"Decoded {source} point ({point.X},{point.Y},{point.Z}) for {unitId} does not uniquely resolve to a catalog handle.");
        }
        return matches[0].Handle;
    }

    private static Id65AuthoringPoint[] DecodeCollisionPoints(ReadOnlySpan<byte> row)
    {
        if (row.Length != 12)
            throw new InvalidDataException("A decoded collision row is not exactly 12 bytes.");
        uint xWord = BinaryPrimitives.ReadUInt32LittleEndian(row[..4]);
        uint yWord = BinaryPrimitives.ReadUInt32LittleEndian(row.Slice(4, 4));
        uint zWord = BinaryPrimitives.ReadUInt32LittleEndian(row.Slice(8, 4));
        DecodeCollisionAxis(xWord, out int x1, out int x2, out int x3);
        DecodeCollisionAxis(yWord, out int y1, out int y2, out int y3);
        int z1 = (int)(zWord & 0x3FFFu);
        int z2 = checked(z1 + (int)((zWord >> 16) & 0xFFu));
        int z3 = checked(z1 + (int)((zWord >> 24) & 0xFFu));
        return [new(x1, y1, z1), new(x2, y2, z2), new(x3, y3, z3)];
    }

    private static void DecodeCollisionAxis(uint word, out int first, out int second, out int third)
    {
        first = (int)(word & 0x3FFFu);
        second = checked(first + SignExtend9((int)((word >> 14) & 0x1FFu)));
        third = checked(first + SignExtend9((int)((word >> 23) & 0x1FFu)));
    }

    private static int SignExtend9(int value) => (value & 0x100) != 0 ? value - 0x200 : value;

    private static Id65AuthoringTerrainUnitReadback[] BuildUnitReadbacks(
        ModelState state,
        IReadOnlyList<Id65TerrainTriangleIntent> active)
    {
        byte[] sector = ExtractSector(
            state.Components.Single(item => item.Name == "environment").Bytes,
            state.SectorBytes);
        byte[] collision = state.Components.Single(item => item.Name == "collision").Bytes;
        byte[] occlusion = state.Components.Single(item => item.Name == "occlusion").Bytes;
        return DecodeUnitEvidence(sector, collision, occlusion, active, state.TargetCellSequence);
    }

    private static Id65AuthoringTerrainUnitReadback[] DecodeUnitEvidence(
        byte[] sector,
        byte[] collision,
        byte[] occlusion,
        IReadOnlyList<Id65TerrainTriangleIntent> active,
        IReadOnlyList<int> targetCellSequence)
    {
        int count = active.Count;
        ValidateSectorCounts(sector, 3 + count, 1 + count);
        int occlusionOwner = ReadSingleOcclusionOwnerGroup(occlusion);
        int lowVertexStart = 28;
        int lowFaceStart = lowVertexStart + ((3 + count) * 4) + 12;
        int highVertexStart = lowFaceStart + ((1 + count) * 8);
        int highFaceStart = highVertexStart + ((3 + count) * 4) + 24;
        Id65AuthoringPoint[] lowPoints = Enumerable.Range(0, 3 + count)
            .Select(index => DecodeSceneVertex(sector, ReadUInt32(sector, lowVertexStart + (index * 4))))
            .ToArray();
        Id65AuthoringPoint[] highPoints = Enumerable.Range(0, 3 + count)
            .Select(index => DecodeSceneVertex(sector, ReadUInt32(sector, highVertexStart + (index * 4))))
            .ToArray();

        List<(string Handle, Id65AuthoringPoint Point)> catalogVertices =
        [
            ("vertex.remote-pad.a", new(272, 272, 512)),
            ("vertex.remote-pad.b", new(496, 272, 512)),
            ("vertex.remote-pad.c", new(384, 496, 512))
        ];
        catalogVertices.AddRange(active.Select(unit => (unit.AddedVertex.Handle, unit.AddedVertex.Point)));
        if (catalogVertices.Select(item => item.Handle).Distinct(StringComparer.Ordinal).Count() !=
                catalogVertices.Count ||
            catalogVertices.Select(item => item.Point).Distinct().Count() != catalogVertices.Count)
        {
            throw new InvalidDataException("The terrain-set catalog cannot uniquely resolve decoded vertex evidence.");
        }
        for (int index = 0; index < catalogVertices.Count; index++)
        {
            if (lowPoints[index] != catalogVertices[index].Point ||
                highPoints[index] != catalogVertices[index].Point)
            {
                throw new InvalidDataException(
                    $"Decoded HP/LP vertex index {index} does not resolve to catalog handle {catalogVertices[index].Handle}.");
            }
        }

        int triangleCount = BaseTriangleCount + count;
        int assignmentsRelative = BaseAssignmentsRelative + (count * 12);
        int flagsRelative = Align4(assignmentsRelative + triangleCount);
        CollisionLayout collisionLayout = ParseCollisionLayout(
            collision,
            triangleCount,
            assignmentsRelative,
            flagsRelative);
        List<Id65AuthoringTerrainUnitReadback> result = [];
        for (int ordinal = 0; ordinal < count; ordinal++)
        {
            Id65TerrainTriangleIntent unit = active[ordinal];
            int vertexIndex = 3 + ordinal;
            int faceIndex = 1 + ordinal;
            int lowFaceOffset = lowFaceStart + (faceIndex * 8);
            int highFaceOffset = highFaceStart + (faceIndex * 16);
            int[] lowSlots = DecodeLowFaceSlots(ReadUInt32(sector, lowFaceOffset));
            int[] highSlots = DecodeHighFaceSlots(sector.AsSpan(highFaceOffset, 4));
            string[] lowHandles = ResolveRenderHandles(lowSlots, lowPoints, catalogVertices, "LP", unit.Id);
            string[] highHandles = ResolveRenderHandles(highSlots, highPoints, catalogVertices, "HP", unit.Id);
            string[] expectedRender = [unit.RenderA, unit.RenderB, unit.RenderC];
            if (!lowHandles.SequenceEqual(expectedRender) || !highHandles.SequenceEqual(expectedRender) ||
                !lowHandles.SequenceEqual(highHandles))
            {
                throw new InvalidDataException(
                    $"Decoded HP/LP face slots for {unit.Id} do not resolve to its catalog render handles.");
            }
            Id65AuthoringPoint[] lowRenderPoints = lowSlots.Select(index => lowPoints[index]).ToArray();
            Id65AuthoringPoint[] highRenderPoints = highSlots.Select(index => highPoints[index]).ToArray();
            long lowNormal = NormalZ(lowRenderPoints[0], lowRenderPoints[1], lowRenderPoints[2]);
            long highNormal = NormalZ(highRenderPoints[0], highRenderPoints[1], highRenderPoints[2]);
            if (lowNormal <= 0 || highNormal != lowNormal)
            {
                throw new InvalidDataException(
                    $"Decoded HP/LP face bytes for {unit.Id} lost matching positive render winding.");
            }

            int collisionIndex = BaseTriangleCount + ordinal;
            int collisionOffset = collisionLayout.TriangleOffset + (collisionIndex * 12);
            Id65AuthoringPoint[] collisionPoints = DecodeCollisionPoints(
                collision.AsSpan(collisionOffset, 12));
            string[] collisionHandles = collisionPoints.Select(point => ResolvePointHandle(
                    point,
                    catalogVertices,
                    "collision",
                    unit.Id))
                .ToArray();
            string[] expectedCollision = [unit.CollisionA, unit.CollisionB, unit.CollisionC];
            if (!collisionHandles.SequenceEqual(expectedCollision))
            {
                throw new InvalidDataException(
                    $"Decoded collision coordinates for {unit.Id} do not resolve to its catalog collision handles.");
            }
            long collisionNormal = NormalZ(collisionPoints[0], collisionPoints[1], collisionPoints[2]);
            if (collisionNormal >= 0 || collisionNormal != -lowNormal)
            {
                throw new InvalidDataException(
                    $"Decoded collision bytes for {unit.Id} lost exact negative winding against render evidence.");
            }
            int assignment = collision[collisionLayout.AssignmentsOffset + collisionIndex];
            if (assignment != unit.CollisionAssignment || occlusionOwner != unit.OcclusionGroupIndex)
            {
                throw new InvalidDataException(
                    $"Decoded assignment or occlusion owner for {unit.Id} changed.");
            }
            result.Add(new(
                unit.Id,
                unit.AddedVertex.Handle,
                ordinal,
                vertexIndex,
                vertexIndex,
                faceIndex,
                faceIndex,
                collisionIndex,
                Convert.ToHexString(sector.AsSpan(lowVertexStart + (vertexIndex * 4), 4)),
                Convert.ToHexString(sector.AsSpan(lowFaceOffset, 8)),
                Convert.ToHexString(sector.AsSpan(highFaceOffset, 16)),
                Convert.ToHexString(collision.AsSpan(collisionOffset, 12)),
                lowNormal,
                collisionNormal,
                TargetCell,
                assignment,
                occlusionOwner));
        }
        if (count > 0 && !targetCellSequence.SequenceEqual(
                Enumerable.Range(BaseTriangleCount, count).Reverse().Append(ExistingTriangleIndex)))
        {
            throw new InvalidDataException("Decoded terrain-set units do not match the collision-cell sequence.");
        }
        return result.ToArray();
    }

    private static Dictionary<string, (string Kind, Id65LogicalAddress Address)> BuildAddresses(
        IReadOnlyList<Id65TerrainTriangleIntent> active,
        Id65CompiledAuthoringModel lockedV3)
    {
        Dictionary<string, (string Kind, Id65LogicalAddress Address)> result = new(StringComparer.Ordinal);
        foreach (Id65StableHandleRebase rebase in lockedV3.HandleRebases)
        {
            if (!rebase.Output.HasValue)
                throw new InvalidDataException("The locked v3 base has an unresolved stable handle.");
            result.Add(rebase.StableId, (rebase.Kind, rebase.Output.Value));
        }
        for (int ordinal = 0; ordinal < active.Count; ordinal++)
        {
            Id65TerrainTriangleIntent unit = active[ordinal];
            result.Add(unit.Id, ("terrain-tile", new("terrain-tile", SectorIndex, 1 + ordinal)));
            result.Add($"{unit.AddedVertex.Handle}:lp",
                ("low-detail-vertex", new("scene-sector", SectorIndex, 3 + ordinal)));
            result.Add($"{unit.AddedVertex.Handle}:hp",
                ("high-detail-vertex", new("scene-sector", SectorIndex, 3 + ordinal)));
            result.Add($"{unit.Id}:lp",
                ("low-detail-face", new("scene-sector", SectorIndex, 1 + ordinal)));
            result.Add($"{unit.Id}:hp",
                ("high-detail-face", new("scene-sector", SectorIndex, 1 + ordinal)));
            result.Add($"{unit.Id}:collision",
                ("collision-triangle", new("collision-triangle", BaseTriangleCount + ordinal, 0)));
        }
        return result;
    }

    private static Id65StableHandleRebase[] BuildRebases(
        IReadOnlyDictionary<string, (string Kind, Id65LogicalAddress Address)> source,
        IReadOnlyDictionary<string, (string Kind, Id65LogicalAddress Address)> output) =>
        source.Keys.Concat(output.Keys).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)
            .Select(id =>
            {
                bool hasSource = source.TryGetValue(id, out var before);
                bool hasOutput = output.TryGetValue(id, out var after);
                string kind = hasSource ? before.Kind : after.Kind;
                if (hasSource && hasOutput && before.Kind != after.Kind)
                    throw new InvalidDataException($"Stable terrain-set handle {id} changed kind.");
                return new Id65StableHandleRebase(
                    id,
                    kind,
                    hasSource ? before.Address : null,
                    hasOutput ? after.Address : null);
            }).ToArray();

    private static Id65ModelComponentRelocation[] BuildRelocations(
        IReadOnlyList<ComponentState> source,
        IReadOnlyList<ComponentState> output)
    {
        if (!source.Select(item => item.Name).SequenceEqual(output.Select(item => item.Name)))
            throw new InvalidDataException("The terrain-set component sequence changed.");
        return source.Zip(output).Select(pair => new Id65ModelComponentRelocation(
            $"component.{pair.First.Name}",
            pair.First.Offset,
            pair.First.ByteLength,
            pair.Second.Offset,
            pair.Second.ByteLength,
            pair.First.Sha256,
            pair.Second.Sha256,
            pair.First.Bytes.SequenceEqual(pair.Second.Bytes))).ToArray();
    }

    private static Id65AuthoringTerrainSetCapacityReadback BuildCapacity(ModelState state) => new(
        ModelLength,
        state.UsedModelBytes,
        ModelLength - state.UsedModelBytes,
        state.EnvironmentBytes,
        state.SectorBytes,
        state.Vertices,
        state.Faces,
        state.Vertices,
        state.Faces,
        state.CollisionBytes,
        state.CollisionTriangles,
        state.CollisionBlocksUsed,
        CollisionTreeCapacity,
        CollisionBlocksCapacity);

    private static void ValidateLimits(Id65AuthoringTerrainSetLimits limits, ModelState state)
    {
        if (limits.ModelByteCapacity < ModelLength)
            throw new InvalidDataException("ID65 terrain-set model byte capacity is insufficient.");
        if (limits.AvailableV3TailBytes < state.UsedModelBytes - BaseUsed)
            throw new InvalidDataException("ID65 terrain-set v3 tail capacity is insufficient.");
        if (limits.MaximumOutputUsedModelBytes < state.UsedModelBytes)
            throw new InvalidDataException("ID65 terrain-set used-model capacity is insufficient.");
        if (limits.MaximumLowDetailVertexCount < state.Vertices ||
            limits.MaximumHighDetailVertexCount < state.Vertices)
            throw new InvalidDataException("ID65 terrain-set vertex capacity is insufficient.");
        if (limits.MaximumSectorFaceCount < state.Faces)
            throw new InvalidDataException("ID65 terrain-set face capacity is insufficient.");
        if (limits.MaximumCollisionTriangleCount < state.CollisionTriangles)
            throw new InvalidDataException("ID65 terrain-set collision triangle capacity is insufficient.");
        if (limits.CollisionTreeCapacityBytes < CollisionTreeCapacity)
            throw new InvalidDataException("ID65 terrain-set collision tree capacity is insufficient.");
        if (limits.CollisionBlockCapacityBytes < state.CollisionBlocksUsed)
            throw new InvalidDataException("ID65 terrain-set collision block capacity is insufficient.");
    }

    private static void ValidateFrozenTargetPins(
        Id65AuthoringTerrainSetManifest manifest,
        ModelState state,
        string protectedHash,
        string relocationHash,
        string rebaseHash,
        string deterministicHash)
    {
        string key = string.Join(',', manifest.ActiveUnits.Select(item => item.Id).Order(StringComparer.Ordinal));
        TargetPins pins = key switch
        {
            "" => new(
                ExpectedEmptyManifestSha256,
                ExpectedV3ModelSha256,
                UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3Construction.ExpectedNewSectorSha256,
                UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3Construction.ExpectedOutputEnvironmentSha256,
                UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3Construction.ExpectedOutputBlocksSha256,
                UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3Construction.ExpectedOutputCollisionComponentSha256,
                ExpectedEmptyRelocationMapSha256,
                ExpectedEmptyHandleRebaseMapSha256,
                ExpectedEmptyDeterministicPlanSha256),
            Id65AuthoringTerrainSetCatalog.Tile1Id => new(
                ExpectedTile1ManifestSha256,
                ExpectedTile1ModelSha256,
                ExpectedTile1SectorSha256,
                ExpectedTile1EnvironmentSha256,
                ExpectedTile1BlocksSha256,
                ExpectedTile1CollisionSha256,
                ExpectedTile1RelocationMapSha256,
                ExpectedTile1HandleRebaseMapSha256,
                ExpectedTile1DeterministicPlanSha256),
            Id65AuthoringTerrainSetCatalog.Tile2Id => new(
                ExpectedTile2ManifestSha256,
                ExpectedTile2ModelSha256,
                ExpectedTile2SectorSha256,
                ExpectedTile2EnvironmentSha256,
                ExpectedTile2BlocksSha256,
                ExpectedTile2CollisionSha256,
                ExpectedTile2RelocationMapSha256,
                ExpectedTile2HandleRebaseMapSha256,
                ExpectedTile2DeterministicPlanSha256),
            $"{Id65AuthoringTerrainSetCatalog.Tile1Id},{Id65AuthoringTerrainSetCatalog.Tile2Id}" => new(
                ExpectedBothManifestSha256,
                ExpectedBothModelSha256,
                ExpectedBothSectorSha256,
                ExpectedBothEnvironmentSha256,
                ExpectedBothBlocksSha256,
                ExpectedBothCollisionSha256,
                ExpectedBothRelocationMapSha256,
                ExpectedBothHandleRebaseMapSha256,
                ExpectedBothDeterministicPlanSha256),
            _ => throw new InvalidDataException("The terrain-set manifest escaped the exact four-state catalog.")
        };
        RequireHash(manifest.CanonicalSha256, pins.ManifestSha256, "terrain-set manifest");
        RequireHash(Hash(state.Model), pins.ModelSha256, "terrain-set model");
        RequireHash(state.SectorSha256, pins.SectorSha256, "terrain-set sector");
        RequireHash(state.EnvironmentSha256, pins.EnvironmentSha256, "terrain-set environment");
        RequireHash(state.CollisionTreeSha256, ExpectedCollisionTreeSha256, "terrain-set collision tree");
        RequireHash(state.CollisionBlocksSha256, pins.BlocksSha256, "terrain-set collision blocks");
        RequireHash(state.CollisionSha256, pins.CollisionSha256, "terrain-set collision");
        RequireHash(protectedHash, ExpectedProtectedComplementSha256, "terrain-set protected complement");
        RequireHash(relocationHash, pins.RelocationMapSha256, "terrain-set relocation map");
        RequireHash(rebaseHash, pins.HandleRebaseMapSha256, "terrain-set handle rebase map");
        RequireHash(deterministicHash, pins.DeterministicPlanSha256, "terrain-set deterministic plan");
    }

    private static readonly string[] ProtectedComponentNames =
    [
        "texture",
        "occlusion",
        "special-surface",
        "cyclorama",
        "portal-table",
        "particles",
        "sound"
    ];

    private static string HashProtectedComplement(IReadOnlyList<ComponentState> components)
    {
        StringBuilder proof = new();
        foreach (string name in ProtectedComponentNames)
        {
            ComponentState component = components.Single(item => item.Name == name);
            proof.Append(name).Append('|').Append(component.ByteLength).Append('|')
                .Append(component.Sha256).Append('\n');
        }
        return Hash(Encoding.UTF8.GetBytes(proof.ToString()));
    }

    private static ModelDiffProof BuildDiffProof(byte[] before, byte[] after)
    {
        if (before.Length != after.Length)
            throw new InvalidDataException("The terrain-set transition changed fixed model capacity.");
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
            manifest.Append(start.ToString("X8", CultureInfo.InvariantCulture)).Append('|')
                .Append(length.ToString(CultureInfo.InvariantCulture)).Append('|')
                .Append(Hash(before.AsSpan(start, length))).Append('|')
                .Append(Hash(after.AsSpan(start, length))).Append('\n');
        }
        return new(changed, ranges, Hash(Encoding.UTF8.GetBytes(manifest.ToString())));
    }

    private static string HashRelocations(IEnumerable<Id65ModelComponentRelocation> values)
    {
        StringBuilder text = new();
        foreach (Id65ModelComponentRelocation item in values.OrderBy(item => item.StableId, StringComparer.Ordinal))
        {
            text.Append(item.StableId).Append('|')
                .Append(item.SourceRelativeOffset.ToString("X8", CultureInfo.InvariantCulture)).Append('|')
                .Append(item.SourceByteLength).Append('|')
                .Append(item.OutputRelativeOffset.ToString("X8", CultureInfo.InvariantCulture)).Append('|')
                .Append(item.OutputByteLength).Append('|')
                .Append(item.SourceSha256).Append('|').Append(item.OutputSha256).Append('|')
                .Append(item.ContentsPreserved ? '1' : '0').Append('\n');
        }
        return Hash(Encoding.UTF8.GetBytes(text.ToString()));
    }

    private static string HashHandleRebases(IEnumerable<Id65StableHandleRebase> values)
    {
        StringBuilder text = new();
        foreach (Id65StableHandleRebase item in values.OrderBy(item => item.StableId, StringComparer.Ordinal))
        {
            text.Append(item.StableId).Append('|').Append(item.Kind).Append('|')
                .Append(Address(item.Source)).Append('|').Append(Address(item.Output)).Append('\n');
        }
        return Hash(Encoding.UTF8.GetBytes(text.ToString()));
    }

    private static string Address(Id65LogicalAddress? address) => address.HasValue
        ? string.Create(CultureInfo.InvariantCulture,
            $"{address.Value.Space}:{address.Value.Primary}:{address.Value.Secondary}")
        : "none";

    private static string VertexHex(string id) => id switch
    {
        Id65AuthoringTerrainSetCatalog.Tile1Id => "00C0031E",
        Id65AuthoringTerrainSetCatalog.Tile2Id => "00C00302",
        _ => throw new InvalidDataException($"Unknown terrain-set vertex row for {id}.")
    };

    private static string LowFaceHex(string id, int activeCount) => (id, activeCount) switch
    {
        (Id65AuthoringTerrainSetCatalog.Tile1Id, 1) => "0082300400821000",
        (Id65AuthoringTerrainSetCatalog.Tile1Id, 2) => "0082300400821000",
        (Id65AuthoringTerrainSetCatalog.Tile2Id, 1) => "00C3200000821000",
        (Id65AuthoringTerrainSetCatalog.Tile2Id, 2) => "0004210000821000",
        _ => throw new InvalidDataException($"Unknown terrain-set LP row for {id}/{activeCount}.")
    };

    private static string HighFaceHex(string id, int activeCount) => (id, activeCount) switch
    {
        (Id65AuthoringTerrainSetCatalog.Tile1Id, 1) => "010103020101000219024001080A1000",
        (Id65AuthoringTerrainSetCatalog.Tile1Id, 2) => "010103020101000219024001080A1000",
        (Id65AuthoringTerrainSetCatalog.Tile2Id, 1) => "000002030101000219024001080A1000",
        (Id65AuthoringTerrainSetCatalog.Tile2Id, 2) => "000002040101000219024001080A1000",
        _ => throw new InvalidDataException($"Unknown terrain-set HP row for {id}/{activeCount}.")
    };

    private static string CollisionHex(string id) => id switch
    {
        Id65AuthoringTerrainSetCatalog.Tile1Id => "F00164001001387000020000",
        Id65AuthoringTerrainSetCatalog.Tile2Id => "100100381001387000020000",
        _ => throw new InvalidDataException($"Unknown terrain-set collision row for {id}.")
    };

    private static ComponentState Component(
        byte[] model,
        string name,
        int offset,
        int length,
        bool hasLengthWord = true)
    {
        RequireRange(model, offset, length, name);
        byte[] bytes = model.AsSpan(offset, length).ToArray();
        if (hasLengthWord && ReadInt32(bytes, 0) != length)
            throw new InvalidDataException($"The {name} component length word changed.");
        if (!hasLengthWord && name == "portal-table" && ReadInt32(bytes, 0) != 0)
            throw new InvalidDataException("The locked v3 portal count changed from zero.");
        return new(name, offset, length, bytes, Hash(bytes));
    }

    private static byte[] ExtractSector(byte[] environment, int expectedLength)
    {
        if (ReadInt32(environment, 4) != 217)
            throw new InvalidDataException("The terrain-set environment no longer owns 217 sectors.");
        int offset = checked(4 + ReadInt32(environment, 8 + (SectorIndex * 4)));
        if (offset + expectedLength != environment.Length)
            throw new InvalidDataException("Sector 216 no longer terminates the terrain-set environment.");
        return environment.AsSpan(offset, expectedLength).ToArray();
    }

    private static Id65AuthoringPoint DecodeSceneVertex(byte[] sector, uint word)
    {
        uint originXy = ReadUInt32(sector, 8);
        uint originZ = ReadUInt32(sector, 12);
        int originX = (int)(originXy >> 16);
        int originY = (int)(originXy & 0xFFFF);
        int z = (int)((originZ >> 14) & 0xFFFF) >> 2;
        int x = originX + (int)((word >> 21) & 0x7FF);
        int y = originY + (int)((word >> 10) & 0x7FF);
        z += (int)(word & 0x3FF);
        if ((ReadUInt16(sector, 4) & 0x1000) != 0)
            z >>= 3;
        return new(x, y, z);
    }

    private static long NormalZ(Id65AuthoringPoint a, Id65AuthoringPoint b, Id65AuthoringPoint c) =>
        ((long)b.X - a.X) * ((long)c.Y - a.Y) -
        (((long)b.Y - a.Y) * ((long)c.X - a.X));

    private static bool IsStrictlyDescending(IReadOnlyList<int> values)
    {
        for (int index = 1; index < values.Count; index++)
        {
            if (values[index - 1] <= values[index])
                return false;
        }
        return true;
    }

    private static int Align4(int value) => checked((value + 3) & ~3);

    private static int ReadInt32(byte[] bytes, int offset)
    {
        RequireRange(bytes, offset, 4, "32-bit value");
        return BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset, 4));
    }

    private static uint ReadUInt32(byte[] bytes, int offset)
    {
        RequireRange(bytes, offset, 4, "32-bit value");
        return BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
    }

    private static ushort ReadUInt16(byte[] bytes, int offset)
    {
        RequireRange(bytes, offset, 2, "16-bit value");
        return BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset, 2));
    }

    private static void WriteInt32(byte[] bytes, int offset, int value)
    {
        RequireRange(bytes, offset, 4, "32-bit value");
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset, 4), value);
    }

    private static void WriteUInt16(byte[] bytes, int offset, ushort value)
    {
        RequireRange(bytes, offset, 2, "16-bit value");
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset, 2), value);
    }

    private static void RequireRange(byte[] bytes, int offset, int length, string label)
    {
        if (offset < 0 || length < 0 || offset + (long)length > bytes.Length)
            throw new InvalidDataException($"The {label} range is outside its owner.");
    }

    private static void RequireZeroTail(byte[] model, int usedBytes)
    {
        if (model.Length != ModelLength || usedBytes < 0 || usedBytes > model.Length ||
            model.AsSpan(usedBytes).IndexOfAnyExcept((byte)0) >= 0)
        {
            throw new InvalidDataException("The terrain-set model suffix is not exact zero tail.");
        }
    }

    private static void RequireHash(string actual, string expected, string label)
    {
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
            throw new InvalidDataException($"The exact {label} SHA-256 changed: {actual}.");
    }

    internal static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    internal sealed record ComponentState(
        string Name,
        int Offset,
        int ByteLength,
        byte[] Bytes,
        string Sha256);

    private sealed record ModelState(
        byte[] Model,
        IReadOnlyList<ComponentState> Components,
        int UsedModelBytes,
        int EnvironmentBytes,
        int SectorBytes,
        int Vertices,
        int Faces,
        int CollisionBytes,
        int CollisionTriangles,
        int CollisionBlocksUsed,
        IReadOnlyList<int> TargetCellSequence,
        string SectorSha256,
        string EnvironmentSha256,
        string CollisionTreeSha256,
        string CollisionBlocksSha256,
        string CollisionSha256);

    private sealed record CollisionLayout(
        int TreeOffset,
        int BlocksOffset,
        int TriangleOffset,
        int AssignmentsOffset,
        int FlagsOffset);

    private sealed record CollisionCellBinding(
        Id65AuthoringCollisionCell Cell,
        int TreePointerByteOffset,
        int SourceBlockWordOffset,
        int[] OrderedTriangleIndexes);

    private sealed record NativeCollisionIndex(
        byte[] TreeBytes,
        byte[] BlockBytes,
        IReadOnlyDictionary<Id65AuthoringCollisionCell, CollisionCellBinding> Cells);

    private sealed record CollisionRepack(
        byte[] TreeBytes,
        byte[] BlockBytes,
        int UsedBlockBytes,
        IReadOnlyList<Id65AuthoringCollisionCell> ChangedCells);

    private sealed record CollisionValidation(
        string TreeSha256,
        string BlocksSha256,
        string CollisionSha256);

    private sealed record ModelDiffProof(
        int ChangedByteCount,
        int RangeCount,
        string ManifestSha256);

    private sealed record TargetPins(
        string ManifestSha256,
        string ModelSha256,
        string SectorSha256,
        string EnvironmentSha256,
        string BlocksSha256,
        string CollisionSha256,
        string RelocationMapSha256,
        string HandleRebaseMapSha256,
        string DeterministicPlanSha256);
}
