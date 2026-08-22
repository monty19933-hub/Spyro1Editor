using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Spyro.Editor.Core.Exporting;

internal sealed record Id65TerrainTriangleIntent(
    string Id,
    string SectorId,
    string TextureIntentId,
    Id65TerrainVertexIntent AddedVertex,
    string RenderA,
    string RenderB,
    string RenderC,
    string CollisionA,
    string CollisionB,
    string CollisionC,
    int CollisionAssignment,
    int OcclusionGroupIndex);

/// <summary>
/// Immutable terrain layer over the exact minimal negative-winding v2
/// authoring manifest. The first gate owns at most one optional triangle and
/// deliberately carries no path, stream, image, publisher, or App state.
/// </summary>
internal sealed class Id65TerrainAuthoringManifest
{
    private readonly ReadOnlyCollection<Id65TerrainTriangleIntent> _optionalTriangles;

    public Id65TerrainAuthoringManifest(
        string profileId,
        Id65AuthoringManifest lockedV2Base,
        IEnumerable<Id65TerrainTriangleIntent> optionalTriangles)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        ArgumentNullException.ThrowIfNull(lockedV2Base);
        ArgumentNullException.ThrowIfNull(optionalTriangles);
        ProfileId = profileId;
        LockedV2Base = lockedV2Base;
        _optionalTriangles = Array.AsReadOnly(optionalTriangles.ToArray());
        CanonicalJson = BuildCanonicalJson();
        CanonicalSha256 = Id65AuthoringTerrainMutationCompiler.Hash(
            Encoding.UTF8.GetBytes(CanonicalJson));
    }

    public string ProfileId { get; }
    public Id65AuthoringManifest LockedV2Base { get; }
    public IReadOnlyList<Id65TerrainTriangleIntent> OptionalTriangles => _optionalTriangles;
    public string CanonicalJson { get; }
    public string CanonicalSha256 { get; }

    private string BuildCanonicalJson()
    {
        using MemoryStream buffer = new();
        using (Utf8JsonWriter json = new(buffer, new JsonWriterOptions { Indented = false }))
        {
            json.WriteStartObject();
            json.WriteString("profileId", ProfileId);
            json.WriteString("lockedV2BaseManifestSha256", LockedV2Base.CanonicalSha256);
            json.WritePropertyName("optionalTriangles");
            json.WriteStartArray();
            foreach (Id65TerrainTriangleIntent triangle in
                     _optionalTriangles.OrderBy(item => item.Id, StringComparer.Ordinal))
            {
                json.WriteStartObject();
                json.WriteString("id", triangle.Id);
                json.WriteString("sectorId", triangle.SectorId);
                json.WriteString("textureIntentId", triangle.TextureIntentId);
                json.WritePropertyName("addedVertex");
                json.WriteStartObject();
                json.WriteString("handle", triangle.AddedVertex.Handle);
                json.WritePropertyName("point");
                json.WriteStartArray();
                json.WriteNumberValue(triangle.AddedVertex.Point.X);
                json.WriteNumberValue(triangle.AddedVertex.Point.Y);
                json.WriteNumberValue(triangle.AddedVertex.Point.Z);
                json.WriteEndArray();
                json.WriteEndObject();
                WriteHandles(json, "render", triangle.RenderA, triangle.RenderB, triangle.RenderC);
                WriteHandles(json, "collision", triangle.CollisionA, triangle.CollisionB, triangle.CollisionC);
                json.WriteNumber("collisionAssignment", triangle.CollisionAssignment);
                json.WriteNumber("occlusionGroupIndex", triangle.OcclusionGroupIndex);
                json.WriteEndObject();
            }
            json.WriteEndArray();
            json.WriteEndObject();
        }
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static void WriteHandles(Utf8JsonWriter json, string name, string a, string b, string c)
    {
        json.WritePropertyName(name);
        json.WriteStartArray();
        json.WriteStringValue(a);
        json.WriteStringValue(b);
        json.WriteStringValue(c);
        json.WriteEndArray();
    }
}

internal enum Id65TerrainMutationKind
{
    AddTriangle,
    RemoveTriangle
}

internal sealed record Id65TerrainCompilerLimits(
    int ModelByteCapacity = UnusedLevel65RemoteBlankIsolationConstruction.ModelByteLength,
    int AvailableV2TailBytes = UnusedLevel65RemoteBlankIsolationConstruction.OutputZeroTailByteCount,
    int MaximumOutputUsedModelBytes = UnusedLevel65RemoteBlankIsolationConstruction.ModelByteLength,
    int MaximumLowDetailVertexCount = 64,
    int MaximumHighDetailVertexCount = 256,
    int MaximumSectorFaceCount = 255,
    int MaximumCollisionTriangleCount = 0x7FFF,
    int CollisionTreeCapacityBytes = 0x6A60,
    int CollisionBlockCapacityBytes = UnusedLevel65RemoteBlankIsolationConstruction.CollisionBlocksCapacityBytes);

internal sealed record Id65TerrainCapacityReadback(
    int ModelByteCapacity,
    int SourceUsedModelBytes,
    int OutputUsedModelBytes,
    int SourceZeroTailBytes,
    int OutputZeroTailBytes,
    int EnvironmentSourceBytes,
    int EnvironmentOutputBytes,
    int CollisionSourceBytes,
    int CollisionOutputBytes,
    int SourceLowDetailVertexCount,
    int OutputLowDetailVertexCount,
    int SourceLowDetailFaceCount,
    int OutputLowDetailFaceCount,
    int SourceHighDetailVertexCount,
    int OutputHighDetailVertexCount,
    int SourceHighDetailFaceCount,
    int OutputHighDetailFaceCount,
    int SourceCollisionTriangleCount,
    int OutputCollisionTriangleCount,
    int SourceCollisionBlocksUsedBytes,
    int OutputCollisionBlocksUsedBytes,
    int CollisionTreeCapacityBytes,
    int CollisionBlocksCapacityBytes)
{
    internal Id65TerrainCapacityReadback Invert() => new(
        ModelByteCapacity,
        OutputUsedModelBytes,
        SourceUsedModelBytes,
        OutputZeroTailBytes,
        SourceZeroTailBytes,
        EnvironmentOutputBytes,
        EnvironmentSourceBytes,
        CollisionOutputBytes,
        CollisionSourceBytes,
        OutputLowDetailVertexCount,
        SourceLowDetailVertexCount,
        OutputLowDetailFaceCount,
        SourceLowDetailFaceCount,
        OutputHighDetailVertexCount,
        SourceHighDetailVertexCount,
        OutputHighDetailFaceCount,
        SourceHighDetailFaceCount,
        OutputCollisionTriangleCount,
        SourceCollisionTriangleCount,
        OutputCollisionBlocksUsedBytes,
        SourceCollisionBlocksUsedBytes,
        CollisionTreeCapacityBytes,
        CollisionBlocksCapacityBytes);
}

internal sealed record Id65TerrainTriangleReadback(
    string TriangleId,
    string AddedVertexHandle,
    bool PresentInSource,
    bool PresentInOutput,
    int SectorIndex,
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
    IReadOnlyList<int> TargetCellSequence,
    int OcclusionGroupIndex)
{
    internal Id65TerrainTriangleReadback Invert() => this with
    {
        PresentInSource = PresentInOutput,
        PresentInOutput = PresentInSource
    };
}

internal sealed class Id65CompiledTerrainMutation
{
    private readonly byte[] _sourceModel;
    private readonly byte[] _outputModel;
    private readonly ReadOnlyCollection<Id65ModelComponentRelocation> _relocations;
    private readonly ReadOnlyCollection<Id65StableHandleRebase> _handleRebases;

    internal Id65CompiledTerrainMutation(
        Id65TerrainMutationKind kind,
        Id65TerrainAuthoringManifest sourceManifest,
        Id65TerrainAuthoringManifest outputManifest,
        byte[] sourceModel,
        byte[] outputModel,
        int changedModelByteCount,
        int diffRangeCount,
        string diffManifestSha256,
        IEnumerable<Id65ModelComponentRelocation> relocations,
        string relocationMapSha256,
        IEnumerable<Id65StableHandleRebase> handleRebases,
        string handleRebaseMapSha256,
        Id65TerrainCapacityReadback capacity,
        Id65TerrainTriangleReadback triangle,
        string deterministicPlanSha256)
    {
        Kind = kind;
        SourceManifest = sourceManifest;
        OutputManifest = outputManifest;
        SourceManifestSha256 = sourceManifest.CanonicalSha256;
        OutputManifestSha256 = outputManifest.CanonicalSha256;
        _sourceModel = sourceModel.ToArray();
        _outputModel = outputModel.ToArray();
        SourceModelSha256 = Id65AuthoringTerrainMutationCompiler.Hash(_sourceModel);
        OutputModelSha256 = Id65AuthoringTerrainMutationCompiler.Hash(_outputModel);
        ChangedModelByteCount = changedModelByteCount;
        DiffRangeCount = diffRangeCount;
        DiffManifestSha256 = diffManifestSha256;
        _relocations = Array.AsReadOnly(relocations.ToArray());
        RelocationMapSha256 = relocationMapSha256;
        _handleRebases = Array.AsReadOnly(handleRebases.ToArray());
        HandleRebaseMapSha256 = handleRebaseMapSha256;
        Capacity = capacity;
        Triangle = triangle;
        DeterministicPlanSha256 = deterministicPlanSha256;
    }

    public Id65TerrainMutationKind Kind { get; }
    public Id65TerrainAuthoringManifest SourceManifest { get; }
    public Id65TerrainAuthoringManifest OutputManifest { get; }
    public string SourceManifestSha256 { get; }
    public string OutputManifestSha256 { get; }
    public string SourceModelSha256 { get; }
    public string OutputModelSha256 { get; }
    public int ChangedModelByteCount { get; }
    public int DiffRangeCount { get; }
    public string DiffManifestSha256 { get; }
    public IReadOnlyList<Id65ModelComponentRelocation> Relocations => _relocations;
    public string RelocationMapSha256 { get; }
    public IReadOnlyList<Id65StableHandleRebase> HandleRebases => _handleRebases;
    public string HandleRebaseMapSha256 { get; }
    public Id65TerrainCapacityReadback Capacity { get; }
    public Id65TerrainTriangleReadback Triangle { get; }
    public string DeterministicPlanSha256 { get; }
    public bool ExactV2SubstrateVerified => true;
    public bool ExactHpLpPairingVerified => true;
    public bool NegativeCollisionWindingVerified => true;
    public bool CollisionCellTreeReadbackVerified => true;
    public bool OcclusionOwnershipVerified => true;
    public bool CapacityAndRebaseVerified => true;
    public bool ExactInverseVerified => true;
    public bool DeterministicReadbackRequired => true;
    public bool WritesFileSystem => false;
    public bool WritesDiscImage => false;
    public bool WritesCue => false;
    public bool AppIntegrated => false;
    public bool CreateBinEnabled => false;
    public bool NormalCreateBinEnabled => false;
    public bool ReleaseIntegrated => false;
    public bool RuntimeCandidateAuthorized => false;
    public bool RetiredComposerCalled => false;
    public bool CrossSliceCompositionAuthorized => false;
    public bool SinglePassCompositeRebuildOwned => false;
    public bool PromotionAuthorized => false;
    public bool Publishable => false;

    internal byte[] CopySourceModel() => _sourceModel.ToArray();
    internal byte[] CopyOutputModel() => _outputModel.ToArray();
}

/// <summary>
/// Pure in-memory, exact-preimage terrain transaction compiler layered over
/// the runtime-discriminator v2 negative-winding model. It supports the first
/// concrete removable adjacent triangle only. Every target model is rebuilt
/// from immutable v2 bytes; no retired composer or publisher is callable.
/// Its afterimage is not authorized to stack with another exact-v2-only
/// compiler; a later single-pass composite compiler must own all slices.
/// </summary>
internal static class Id65AuthoringTerrainMutationCompiler
{
    public const string ProfileId = "id65-authoring-terrain-mutation-v2-one-optional-triangle-v1";
    public const string TriangleId = "tile.remote-pad.1";
    public const string AddedVertexHandle = "vertex.remote-pad.d";
    public const string ExpectedSubstrateManifestSha256 =
        "17e6b91722b3c975e615d19b1eb0646c578ecbd3080156edb6dae29a8aa540e8";
    public const string ExpectedTwoTileManifestSha256 =
        "58bc4bc2a1116b05b0c5d25e4fad206164ca60abe785a33db26568a506bcb919";
    public const string ExpectedV2ModelSha256 =
        UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.ExpectedOutputModelSha256;
    public const string ExpectedTwoTileSectorSha256 =
        "d13af9610af732ca91c5abd31ea70d987906fc902a30fb4302a2c9d0bf0f9565";
    public const string ExpectedTwoTileEnvironmentSha256 =
        "f306844498a86a3566d66fc5e8233d83711d58013027fbedcd4f9b99eb3d1023";
    public const string ExpectedTwoTileTreeSha256 =
        UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.ExpectedOutputTreeSha256;
    public const string ExpectedTwoTileBlocksSha256 =
        "f813f0708c5747e59ad559e43da7e44495cc7a4db60caf55a1fc97391003e719";
    public const string ExpectedTwoTileCollisionSha256 =
        "0f6507872d7f29fefd3f70cebcedc8e47b145d08a1cc0ebc48eb47e56ed7cfcb";
    public const string ExpectedTwoTileModelSha256 =
        "fb22ec02de9fc1eb1ce27dfacf2bdf92e671e8e9ac7a7f5d9248a586db770908";
    public const string ExpectedAddedVertexWordHex = "00C0031E";
    public const string ExpectedLowDetailFaceHex = "0082300400821000";
    public const string ExpectedHighDetailFaceHex = "010103020101000219024001080A1000";
    public const string ExpectedCollisionTriangleHex = "F00164001001387000020000";
    public const int ExpectedTwoTileUsedModelBytes = 0x945AC;
    public const int ExpectedTwoTileZeroTailBytes = 0x254;
    public const int ExpectedTwoTileBlocksUsedBytes = 0x17962;
    public const int ExpectedTwoTileChangedModelByteCount = 342_464;
    public const int ExpectedTwoTileDiffRangeCount = 46_189;
    public const string ExpectedAddDiffManifestSha256 =
        "9bddd4248cafe2c07e704ba4fa784a65f8ade5db586e1405024bec73233a4015";
    public const string ExpectedAddRelocationMapSha256 =
        "a42761dfcab42857d0c9f1de714414c97715d0c325f1bcd361de0fe15f694efd";
    public const string ExpectedAddHandleRebaseMapSha256 =
        "153c8236a582d22cb0fb9a3cbc9547853679e046342ddaae2f217c2a95e154d0";
    public const string ExpectedAddDeterministicPlanSha256 =
        "456d68512c544ad293427c7b1b65682321cf21c46e0f8d799028e8ba51cb34aa";
    public const string ExpectedRemoveDiffManifestSha256 =
        "a2e0e8e418a5bed9e901aee9a8870ef19c0a418f18eb393c839534b874237ea2";
    public const string ExpectedRemoveRelocationMapSha256 =
        "0ff175a94c6135369ba0d390babe6b9fa47158c6774c413dd298d5f446da752e";
    public const string ExpectedRemoveHandleRebaseMapSha256 =
        "d947a341ddc8d23d989cbe42fd45840c77e5b7adbae5015550854947d2f46885";
    public const string ExpectedRemoveDeterministicPlanSha256 =
        "147e29257629311539b8ac3698d9817adddb23c23c5fbaa148d38f7c9bcb67dd";

    private const int ModelByteLength = 0x94800;
    private const int V2UsedModelBytes = 0x9457C;
    private const int V2ZeroTailBytes = 0x284;
    private const int TextureOffset = 0x00000;
    private const int TextureLength = 0x02F78;
    private const int V2EnvironmentOffset = 0x02F78;
    private const int V2EnvironmentLength = 0x28518;
    private const int TwoTileEnvironmentLength = 0x28538;
    private const int V2OcclusionOffset = 0x2B490;
    private const int OcclusionLength = 0x974;
    private const int V2SpecialOffset = 0x2BE04;
    private const int SpecialLength = 0x30;
    private const int V2CollisionOffset = 0x2BE34;
    private const int V2CollisionLength = 0x5FAE8;
    private const int TwoTileCollisionLength = 0x5FAF8;
    private const int V2CycloramaOffset = 0x8B91C;
    private const int CycloramaLength = 0x84E4;
    private const int V2PortalOffset = 0x93E00;
    private const int PortalLength = 4;
    private const int V2ParticlesOffset = 0x93E04;
    private const int ParticlesLength = 0x80;
    private const int V2SoundOffset = 0x93E84;
    private const int SoundLength = 0x6F8;
    private const int TwoTileOcclusionOffset = 0x2B4B0;
    private const int TwoTileSpecialOffset = 0x2BE24;
    private const int TwoTileCollisionOffset = 0x2BE54;
    private const int TwoTileCycloramaOffset = 0x8B94C;
    private const int TwoTilePortalOffset = 0x93E30;
    private const int TwoTileParticlesOffset = 0x93E34;
    private const int TwoTileSoundOffset = 0x93EB4;
    private const int SectorIndex = 216;
    private const int BaseSectorLength = 0x70;
    private const int TwoTileSectorLength = 0x90;
    private const int CollisionTreeRelativeOffset = 0x1C;
    private const int CollisionBlocksRelativeOffset = 0x6A7C;
    private const int CollisionTrianglesRelativeOffset = 0x1E400;
    private const int V2CollisionAssignmentsRelativeOffset = 0x58480;
    private const int V2CollisionFlagsRelativeOffset = 0x5D1E0;
    private const int TwoTileCollisionAssignmentsRelativeOffset = 0x5848C;
    private const int TwoTileCollisionFlagsRelativeOffset = 0x5D1F0;
    private const int CollisionTreeCapacityBytes = 0x6A60;
    private const int CollisionBlocksCapacityBytes = 0x17984;
    private const int V2CollisionBlocksUsedBytes = 0x17960;
    private const int BaseCollisionTriangleCount = 19_808;
    private const int AddedCollisionTriangleIndex = 19_808;
    private const int CollisionFlagsLength = 0x2904;
    private const int ExistingCollisionTriangleIndex = 13_995;
    private const string SectorId = "sector.remote-pad.216";
    private const string TextureId = "texture.locked.record25";
    private const string VertexB = "vertex.remote-pad.b";
    private const string VertexC = "vertex.remote-pad.c";
    private const string ExistingCollisionHex = "10011C701001380000020000";

    private static readonly Id65AuthoringPoint PointB = new(496, 272, 512);
    private static readonly Id65AuthoringPoint PointC = new(384, 496, 512);
    private static readonly Id65AuthoringPoint PointD = new(496, 496, 512);
    private static readonly Id65AuthoringCollisionCell TargetCell = new(1, 1, 2);

    public static Id65TerrainAuthoringManifest CreateV2SubstrateManifest() =>
        new(
            ProfileId,
            Id65AuthoringModelCompiler.CreateMinimalV2EquivalentManifest(),
            Array.Empty<Id65TerrainTriangleIntent>());

    public static Id65TerrainTriangleIntent CreateConcreteTwoTileWitness() =>
        new(
            TriangleId,
            SectorId,
            TextureId,
            new(AddedVertexHandle, PointD),
            VertexB,
            AddedVertexHandle,
            VertexC,
            VertexB,
            VertexC,
            AddedVertexHandle,
            0,
            0);

    public static Id65TerrainAuthoringManifest AddTriangle(
        Id65TerrainAuthoringManifest source,
        Id65TerrainTriangleIntent triangle)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(triangle);
        ValidateLayerManifest(source);
        if (source.OptionalTriangles.Count != 0)
            throw new InvalidDataException("The v2 terrain manifest already owns its one optional triangle.");
        ValidateExactTriangle(triangle);
        Id65TerrainAuthoringManifest output = new(ProfileId, source.LockedV2Base, [triangle]);
        ValidateLayerManifest(output);
        return output;
    }

    public static Id65TerrainAuthoringManifest RemoveTriangle(
        Id65TerrainAuthoringManifest source,
        string triangleId)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(triangleId);
        ValidateLayerManifest(source);
        if (source.OptionalTriangles.Count != 1 ||
            source.OptionalTriangles[0].Id != triangleId)
        {
            throw new InvalidDataException("The requested optional terrain triangle is not active exactly once.");
        }
        Id65TerrainAuthoringManifest output = new(ProfileId, source.LockedV2Base, []);
        ValidateLayerManifest(output);
        return output;
    }

    public static Id65CompiledTerrainMutation CompileTransition(
        Id65TerrainAuthoringManifest source,
        Id65TerrainAuthoringManifest target,
        UnusedLevel65RemoteBlankCollisionWindingRepairV2StaticPlan v2Template,
        Id65TerrainCompilerLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(v2Template);
        limits ??= new();
        ValidateLayerManifest(source);
        ValidateLayerManifest(target);
        if (source.LockedV2Base.CanonicalSha256 != target.LockedV2Base.CanonicalSha256)
            throw new InvalidDataException("The terrain transition changed its locked v2 base manifest.");

        Id65TerrainMutationKind kind;
        if (source.OptionalTriangles.Count == 0 && target.OptionalTriangles.Count == 1)
            kind = Id65TerrainMutationKind.AddTriangle;
        else if (source.OptionalTriangles.Count == 1 && target.OptionalTriangles.Count == 0)
            kind = Id65TerrainMutationKind.RemoveTriangle;
        else
            throw new InvalidDataException("A terrain transition must add or remove exactly one optional triangle.");

        Id65CompiledAuthoringModel lockedV2 = Id65AuthoringModelCompiler.Compile(
            source.LockedV2Base,
            v2Template);
        byte[] v2Model = lockedV2.CopyOutputModel();
        RequireHash(Hash(v2Model), ExpectedV2ModelSha256, "negative-winding v2 substrate model");

        ModelState baseState = ParseAndValidateV2(v2Model, v2Template, lockedV2);
        ModelState twoTileState = BuildAndValidateTwoTile(v2Model, baseState);
        ValidateLimits(limits, baseState, twoTileState);

        ModelState sourceState = source.OptionalTriangles.Count == 0 ? baseState : twoTileState;
        ModelState outputState = target.OptionalTriangles.Count == 0 ? baseState : twoTileState;
        ModelDiffProof diff = BuildDiffProof(sourceState.Model, outputState.Model);
        if (diff.ChangedByteCount != ExpectedTwoTileChangedModelByteCount ||
            diff.RangeCount != ExpectedTwoTileDiffRangeCount)
        {
            throw new InvalidDataException("The exact two-tile model diff boundary changed.");
        }

        Id65ModelComponentRelocation[] relocations = BuildRelocations(sourceState, outputState);
        string relocationHash = HashRelocations(relocations);
        if (!Id65AuthoringModelCompiler.InvertRelocations(
                Id65AuthoringModelCompiler.InvertRelocations(relocations)).SequenceEqual(relocations))
        {
            throw new InvalidDataException("The terrain component relocation map is not exactly invertible.");
        }

        Id65StableHandleRebase[] rebases = BuildHandleRebases(sourceState, outputState, lockedV2);
        string rebaseHash = HashHandleRebases(rebases);
        if (!Id65AuthoringModelCompiler.InvertHandleRebases(
                Id65AuthoringModelCompiler.InvertHandleRebases(rebases)).SequenceEqual(rebases))
        {
            throw new InvalidDataException("The terrain stable-handle rebase map is not exactly invertible.");
        }

        Id65TerrainCapacityReadback capacity = BuildCapacity(sourceState, outputState);
        Id65TerrainTriangleReadback triangle = BuildTriangleReadback(
            source.OptionalTriangles.Count == 1,
            target.OptionalTriangles.Count == 1,
            twoTileState);
        string deterministicHash = HashDeterministicIdentity(
            kind,
            source,
            target,
            sourceState,
            outputState,
            diff,
            relocationHash,
            rebaseHash);
        ValidateDeterministicPins(
            kind,
            source,
            target,
            diff,
            relocationHash,
            rebaseHash,
            deterministicHash);
        Id65CompiledTerrainMutation compiled = new(
            kind,
            source,
            target,
            sourceState.Model,
            outputState.Model,
            diff.ChangedByteCount,
            diff.RangeCount,
            diff.ManifestSha256,
            relocations,
            relocationHash,
            rebases,
            rebaseHash,
            capacity,
            triangle,
            deterministicHash);
        byte[] applied = ApplyTransactional(compiled, sourceState.Model, reverse: false);
        byte[] reversed = ApplyTransactional(compiled, applied, reverse: true);
        if (!applied.SequenceEqual(outputState.Model) || !reversed.SequenceEqual(sourceState.Model))
            throw new InvalidDataException("The terrain transaction is not exactly byte-invertible.");
        return compiled;
    }

    public static Id65CompiledTerrainMutation Invert(Id65CompiledTerrainMutation mutation)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        byte[] source = mutation.CopyOutputModel();
        byte[] output = mutation.CopySourceModel();
        ModelDiffProof diff = BuildDiffProof(source, output);
        Id65ModelComponentRelocation[] relocations =
            Id65AuthoringModelCompiler.InvertRelocations(mutation.Relocations).ToArray();
        Id65StableHandleRebase[] rebases =
            Id65AuthoringModelCompiler.InvertHandleRebases(mutation.HandleRebases).ToArray();
        Id65TerrainMutationKind kind = mutation.Kind == Id65TerrainMutationKind.AddTriangle
            ? Id65TerrainMutationKind.RemoveTriangle
            : Id65TerrainMutationKind.AddTriangle;
        string relocationHash = HashRelocations(relocations);
        string rebaseHash = HashHandleRebases(rebases);
        ModelState sourceState = StateForCompiledModel(source, mutation.Capacity.OutputUsedModelBytes);
        ModelState outputState = StateForCompiledModel(output, mutation.Capacity.SourceUsedModelBytes);
        string deterministic = HashDeterministicIdentity(
            kind,
            mutation.OutputManifest,
            mutation.SourceManifest,
            sourceState,
            outputState,
            diff,
            relocationHash,
            rebaseHash);
        ValidateDeterministicPins(
            kind,
            mutation.OutputManifest,
            mutation.SourceManifest,
            diff,
            relocationHash,
            rebaseHash,
            deterministic);
        return new(
            kind,
            mutation.OutputManifest,
            mutation.SourceManifest,
            source,
            output,
            diff.ChangedByteCount,
            diff.RangeCount,
            diff.ManifestSha256,
            relocations,
            relocationHash,
            rebases,
            rebaseHash,
            mutation.Capacity.Invert(),
            mutation.Triangle.Invert(),
            deterministic);
    }

    public static byte[] ApplyTransactional(
        Id65CompiledTerrainMutation mutation,
        byte[] input,
        bool reverse)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        ArgumentNullException.ThrowIfNull(input);
        byte[] expected = reverse ? mutation.CopyOutputModel() : mutation.CopySourceModel();
        byte[] replacement = reverse ? mutation.CopySourceModel() : mutation.CopyOutputModel();
        if (!input.SequenceEqual(expected))
        {
            throw new InvalidDataException(
                $"The terrain transaction {(reverse ? "afterimage" : "preimage")} changed.");
        }
        return replacement;
    }

    private static void ValidateLayerManifest(Id65TerrainAuthoringManifest manifest)
    {
        Id65AuthoringManifest expectedBase =
            Id65AuthoringModelCompiler.CreateMinimalV2EquivalentManifest();
        if (manifest.ProfileId != ProfileId ||
            manifest.LockedV2Base.CanonicalSha256 != expectedBase.CanonicalSha256 ||
            manifest.LockedV2Base.CanonicalJson != expectedBase.CanonicalJson)
        {
            throw new InvalidDataException("The immutable minimal-v2 terrain base manifest changed.");
        }
        if (manifest.OptionalTriangles.Count > 1)
            throw new InvalidDataException("The first terrain gate accepts at most one optional triangle.");
        if (manifest.OptionalTriangles.Count == 1)
            ValidateExactTriangle(manifest.OptionalTriangles[0]);
    }

    private static void ValidateExactTriangle(Id65TerrainTriangleIntent triangle)
    {
        Id65TerrainTriangleIntent expected = CreateConcreteTwoTileWitness();
        if (triangle != expected)
            throw new InvalidDataException("The optional triangle differs from the exact negative-winding two-tile witness.");
        string[] stableTokens =
        [
            triangle.Id,
            triangle.SectorId,
            triangle.TextureIntentId,
            triangle.AddedVertex.Handle,
            triangle.RenderA,
            triangle.RenderB,
            triangle.RenderC,
            triangle.CollisionA,
            triangle.CollisionB,
            triangle.CollisionC
        ];
        if (stableTokens.Any(token => !IsStableToken(token)) ||
            stableTokens.Take(4).Distinct(StringComparer.Ordinal).Count() != 4)
        {
            throw new InvalidDataException("The optional triangle has a noncanonical or conflicting stable handle.");
        }
        Dictionary<string, Id65AuthoringPoint> points = new(StringComparer.Ordinal)
        {
            [VertexB] = PointB,
            [VertexC] = PointC,
            [AddedVertexHandle] = PointD
        };
        long renderNormal = NormalZ(
            points[triangle.RenderA], points[triangle.RenderB], points[triangle.RenderC]);
        long collisionNormal = NormalZ(
            points[triangle.CollisionA], points[triangle.CollisionB], points[triangle.CollisionC]);
        if (renderNormal != 25_088 || collisionNormal != -25_088)
            throw new InvalidDataException("The optional triangle lost explicit positive render / negative collision winding.");
        if (!new[] { triangle.RenderA, triangle.RenderB, triangle.RenderC }
                .Order(StringComparer.Ordinal).SequenceEqual(
                    new[] { triangle.CollisionA, triangle.CollisionB, triangle.CollisionC }
                        .Order(StringComparer.Ordinal)))
        {
            throw new InvalidDataException("The render and collision point sets no longer match.");
        }
    }

    private static ModelState ParseAndValidateV2(
        byte[] model,
        UnusedLevel65RemoteBlankCollisionWindingRepairV2StaticPlan template,
        Id65CompiledAuthoringModel lockedV2)
    {
        RequireHash(Hash(model), ExpectedV2ModelSha256, "v2 terrain substrate");
        if (template.ProfileId != UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.ProfileId ||
            template.OutputModelSha256 != ExpectedV2ModelSha256 ||
            lockedV2.OutputModelSha256 != ExpectedV2ModelSha256 ||
            lockedV2.CollisionNormalZ != -50_176 || !lockedV2.NegativeCollisionWindingVerified)
        {
            throw new InvalidDataException("The terrain compiler requires the exact negative-winding v2 proof.");
        }
        ComponentState[] components =
        [
            Component(model, "texture", TextureOffset, TextureLength),
            Component(model, "environment", V2EnvironmentOffset, V2EnvironmentLength),
            Component(model, "occlusion", V2OcclusionOffset, OcclusionLength),
            Component(model, "special-surface", V2SpecialOffset, SpecialLength),
            Component(model, "collision", V2CollisionOffset, V2CollisionLength),
            Component(model, "cyclorama", V2CycloramaOffset, CycloramaLength),
            Component(model, "portal-table", V2PortalOffset, PortalLength, hasLengthWord: false),
            Component(model, "particles", V2ParticlesOffset, ParticlesLength),
            Component(model, "sound", V2SoundOffset, SoundLength)
        ];
        RequireZeroTail(model, V2UsedModelBytes);
        byte[] environment = components.Single(item => item.Name == "environment").Bytes;
        byte[] sector = ExtractLastSector216(environment, BaseSectorLength);
        RequireHash(
            Hash(sector),
            UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.ExpectedNewSectorSha256,
            "v2 sector 216");
        ValidateSectorCounts(sector, 3, 1, 3, 1);
        byte[] occlusion = components.Single(item => item.Name == "occlusion").Bytes;
        ValidateOcclusion(occlusion);
        byte[] collision = components.Single(item => item.Name == "collision").Bytes;
        CollisionIndexReadback collisionIndex = ValidateCollisionComponent(
            collision,
            BaseCollisionTriangleCount,
            V2CollisionAssignmentsRelativeOffset,
            V2CollisionFlagsRelativeOffset,
            V2CollisionBlocksUsedBytes,
            UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.ExpectedOutputTreeSha256,
            UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.ExpectedOutputBlocksSha256,
            UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.ExpectedOutputCollisionComponentSha256,
            expectedTargetSequence: [ExistingCollisionTriangleIndex]);
        return new(
            model.ToArray(),
            components,
            V2UsedModelBytes,
            V2ZeroTailBytes,
            environment.Length,
            collision.Length,
            3,
            1,
            3,
            1,
            BaseCollisionTriangleCount,
            collisionIndex.UsedBlockBytes,
            collisionIndex.TargetSequence);
    }

    private static ModelState BuildAndValidateTwoTile(byte[] v2Model, ModelState baseState)
    {
        byte[] environment = BuildTwoTileEnvironment(
            baseState.Components.Single(item => item.Name == "environment").Bytes);
        byte[] collision = BuildTwoTileCollision(
            baseState.Components.Single(item => item.Name == "collision").Bytes);
        Dictionary<string, byte[]> replacements = new(StringComparer.Ordinal)
        {
            ["environment"] = environment,
            ["collision"] = collision
        };
        byte[] output = new byte[ModelByteLength];
        int cursor = 0;
        foreach (ComponentState component in baseState.Components)
        {
            byte[] bytes = replacements.TryGetValue(component.Name, out byte[]? replacement)
                ? replacement
                : component.Bytes;
            bytes.CopyTo(output, cursor);
            cursor += bytes.Length;
        }
        if (cursor != ExpectedTwoTileUsedModelBytes)
            throw new InvalidDataException($"The two-tile model ended at 0x{cursor:X}.");
        RequireZeroTail(output, cursor);
        RequireHash(Hash(output), ExpectedTwoTileModelSha256, "two-tile model");
        ComponentState[] components =
        [
            Component(output, "texture", TextureOffset, TextureLength),
            Component(output, "environment", V2EnvironmentOffset, TwoTileEnvironmentLength),
            Component(output, "occlusion", TwoTileOcclusionOffset, OcclusionLength),
            Component(output, "special-surface", TwoTileSpecialOffset, SpecialLength),
            Component(output, "collision", TwoTileCollisionOffset, TwoTileCollisionLength),
            Component(output, "cyclorama", TwoTileCycloramaOffset, CycloramaLength),
            Component(output, "portal-table", TwoTilePortalOffset, PortalLength, hasLengthWord: false),
            Component(output, "particles", TwoTileParticlesOffset, ParticlesLength),
            Component(output, "sound", TwoTileSoundOffset, SoundLength)
        ];
        foreach (string preserved in
                 new[] { "texture", "occlusion", "special-surface", "cyclorama", "portal-table", "particles", "sound" })
        {
            if (!baseState.Components.Single(item => item.Name == preserved).Bytes
                    .SequenceEqual(components.Single(item => item.Name == preserved).Bytes))
            {
                throw new InvalidDataException($"Protected component {preserved} changed instead of relocating intact.");
            }
        }
        byte[] sector = ExtractLastSector216(environment, TwoTileSectorLength);
        RequireHash(Hash(sector), ExpectedTwoTileSectorSha256, "two-tile sector 216");
        ValidateSectorCounts(sector, 4, 2, 4, 2);
        ValidateTwoTileSectorSemantic(sector);
        ValidateOcclusion(components.Single(item => item.Name == "occlusion").Bytes);
        CollisionIndexReadback index = ValidateCollisionComponent(
            collision,
            BaseCollisionTriangleCount + 1,
            TwoTileCollisionAssignmentsRelativeOffset,
            TwoTileCollisionFlagsRelativeOffset,
            ExpectedTwoTileBlocksUsedBytes,
            ExpectedTwoTileTreeSha256,
            ExpectedTwoTileBlocksSha256,
            ExpectedTwoTileCollisionSha256,
            expectedTargetSequence: [AddedCollisionTriangleIndex, ExistingCollisionTriangleIndex]);
        return new(
            output,
            components,
            ExpectedTwoTileUsedModelBytes,
            ExpectedTwoTileZeroTailBytes,
            environment.Length,
            collision.Length,
            4,
            2,
            4,
            2,
            BaseCollisionTriangleCount + 1,
            index.UsedBlockBytes,
            index.TargetSequence);
    }

    private static byte[] BuildTwoTileEnvironment(byte[] v2Environment)
    {
        if (v2Environment.Length != V2EnvironmentLength || ReadInt32(v2Environment, 0) != V2EnvironmentLength ||
            ReadInt32(v2Environment, 4) != 217)
        {
            throw new InvalidDataException("The exact v2 environment header changed.");
        }
        int sectorOffset = checked(4 + ReadInt32(v2Environment, 8 + (SectorIndex * 4)));
        if (sectorOffset + BaseSectorLength != v2Environment.Length)
            throw new InvalidDataException("Sector 216 is no longer the exact final v2 sector.");
        byte[] source = v2Environment.AsSpan(sectorOffset, BaseSectorLength).ToArray();
        byte[] header = source.AsSpan(0, 28).ToArray();
        header[16] = 4;
        header[18] = 2;
        header[20] = 4;
        header[22] = 2;
        byte[] d = Convert.FromHexString(ExpectedAddedVertexWordHex);
        byte[] lowFace = Convert.FromHexString(ExpectedLowDetailFaceHex);
        byte[] highFace = Convert.FromHexString(ExpectedHighDetailFaceHex);
        byte[] sector = new byte[TwoTileSectorLength];
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
            throw new InvalidDataException("The exact two-tile sector did not consume 0x90 bytes.");
        RequireHash(Hash(sector), ExpectedTwoTileSectorSha256, "two-tile sector bytes");
        byte[] output = new byte[TwoTileEnvironmentLength];
        v2Environment.AsSpan(0, sectorOffset).CopyTo(output);
        sector.CopyTo(output, sectorOffset);
        WriteInt32(output, 0, output.Length);
        if (sectorOffset + sector.Length != output.Length)
            throw new InvalidDataException("The expanded sector did not end at the environment boundary.");
        RequireHash(Hash(output), ExpectedTwoTileEnvironmentSha256, "two-tile environment");
        return output;
    }

    private static byte[] BuildTwoTileCollision(byte[] v2Collision)
    {
        CollisionLayout layout = ParseCollisionLayout(
            v2Collision,
            BaseCollisionTriangleCount,
            V2CollisionAssignmentsRelativeOffset,
            V2CollisionFlagsRelativeOffset);
        byte[] tree = v2Collision.AsSpan(layout.TreeOffset, layout.TreeCapacityBytes).ToArray();
        byte[] blocks = v2Collision.AsSpan(layout.BlocksOffset, layout.BlocksCapacityBytes).ToArray();
        NativeCollisionIndex native = DecodeCollisionIndex(
            tree, blocks, V2CollisionBlocksUsedBytes, BaseCollisionTriangleCount);
        if (!native.Cells.TryGetValue(TargetCell, out CollisionCellBinding? target) ||
            !target.OrderedTriangleIndexes.SequenceEqual([ExistingCollisionTriangleIndex]))
        {
            throw new InvalidDataException("The exact v2 target cell no longer owns only T13995.");
        }
        Dictionary<Id65AuthoringCollisionCell, int[]> desired = native.Cells
            .ToDictionary(pair => pair.Key, pair => pair.Value.OrderedTriangleIndexes.ToArray());
        desired[TargetCell] = [AddedCollisionTriangleIndex, ExistingCollisionTriangleIndex];
        CollisionRepack repack = RepackCollisionIndex(
            native,
            desired,
            BaseCollisionTriangleCount + 1,
            CollisionBlocksCapacityBytes);
        if (!repack.ChangedCells.SequenceEqual([TargetCell]) ||
            repack.UsedBlockBytes != ExpectedTwoTileBlocksUsedBytes)
        {
            throw new InvalidDataException("The optional triangle changed an unexpected collision cell or block capacity.");
        }
        RequireHash(Hash(repack.TreeBytes), ExpectedTwoTileTreeSha256, "two-tile collision tree");
        RequireHash(Hash(repack.BlockBytes), ExpectedTwoTileBlocksSha256, "two-tile collision blocks");

        byte[] result = new byte[TwoTileCollisionLength];
        WriteInt32(result, 0, result.Length);
        WriteInt32(result, 4, BaseCollisionTriangleCount + 1);
        WriteInt32(result, 8, CollisionFlagsLength);
        WriteInt32(result, 12, CollisionTreeRelativeOffset);
        WriteInt32(result, 16, CollisionBlocksRelativeOffset);
        WriteInt32(result, 20, CollisionTrianglesRelativeOffset);
        WriteInt32(result, 24, TwoTileCollisionAssignmentsRelativeOffset);
        WriteInt32(result, 28, TwoTileCollisionFlagsRelativeOffset);
        int treeOffset = 4 + CollisionTreeRelativeOffset;
        int blocksOffset = 4 + CollisionBlocksRelativeOffset;
        int trianglesOffset = 4 + CollisionTrianglesRelativeOffset;
        int assignmentsOffset = 4 + TwoTileCollisionAssignmentsRelativeOffset;
        int flagsOffset = 4 + TwoTileCollisionFlagsRelativeOffset;
        repack.TreeBytes.CopyTo(result, treeOffset);
        repack.BlockBytes.CopyTo(result, blocksOffset);
        v2Collision.AsSpan(layout.TriangleOffset, BaseCollisionTriangleCount * 12)
            .CopyTo(result.AsSpan(trianglesOffset));
        Convert.FromHexString(ExpectedCollisionTriangleHex)
            .CopyTo(result, trianglesOffset + (AddedCollisionTriangleIndex * 12));
        v2Collision.AsSpan(layout.AssignmentsOffset, BaseCollisionTriangleCount)
            .CopyTo(result.AsSpan(assignmentsOffset));
        result[assignmentsOffset + AddedCollisionTriangleIndex] = 0;
        if (result.AsSpan(
                assignmentsOffset + BaseCollisionTriangleCount + 1,
                Align4(BaseCollisionTriangleCount + 1) - (BaseCollisionTriangleCount + 1))
            .IndexOfAnyExcept((byte)0) >= 0)
        {
            throw new InvalidDataException("The expanded collision assignment alignment is not zero.");
        }
        v2Collision.AsSpan(layout.FlagsOffset, CollisionFlagsLength).CopyTo(result.AsSpan(flagsOffset));
        RequireHash(Hash(result), ExpectedTwoTileCollisionSha256, "two-tile collision component");
        return result;
    }

    private static CollisionIndexReadback ValidateCollisionComponent(
        byte[] collision,
        int triangleCount,
        int assignmentsRelative,
        int flagsRelative,
        int usedBlockBytes,
        string expectedTreeHash,
        string expectedBlocksHash,
        string expectedCollisionHash,
        IReadOnlyList<int> expectedTargetSequence)
    {
        CollisionLayout layout = ParseCollisionLayout(
            collision, triangleCount, assignmentsRelative, flagsRelative);
        byte[] tree = collision.AsSpan(layout.TreeOffset, layout.TreeCapacityBytes).ToArray();
        byte[] blocks = collision.AsSpan(layout.BlocksOffset, layout.BlocksCapacityBytes).ToArray();
        RequireHash(Hash(tree), expectedTreeHash, "collision tree");
        RequireHash(Hash(blocks), expectedBlocksHash, "collision blocks");
        RequireHash(Hash(collision), expectedCollisionHash, "collision component");
        NativeCollisionIndex index = DecodeCollisionIndex(tree, blocks, usedBlockBytes, triangleCount);
        if (!index.Cells.TryGetValue(TargetCell, out CollisionCellBinding? target) ||
            !target.OrderedTriangleIndexes.SequenceEqual(expectedTargetSequence))
        {
            throw new InvalidDataException("Collision cell (1,1,2) failed semantic sequence readback.");
        }
        string existing = Convert.ToHexString(collision.AsSpan(
            layout.TriangleOffset + (ExistingCollisionTriangleIndex * 12), 12));
        if (existing != ExistingCollisionHex ||
            collision[layout.AssignmentsOffset + ExistingCollisionTriangleIndex] != 0)
        {
            throw new InvalidDataException("The locked negative-winding T13995 row or assignment changed.");
        }
        if (triangleCount == BaseCollisionTriangleCount + 1)
        {
            string added = Convert.ToHexString(collision.AsSpan(
                layout.TriangleOffset + (AddedCollisionTriangleIndex * 12), 12));
            if (added != ExpectedCollisionTriangleHex ||
                collision[layout.AssignmentsOffset + AddedCollisionTriangleIndex] != 0)
            {
                throw new InvalidDataException("The appended negative-winding collision row or assignment changed.");
            }
        }
        return new(usedBlockBytes, target.OrderedTriangleIndexes.ToArray());
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
            ReadInt32(collision, 12) != CollisionTreeRelativeOffset ||
            ReadInt32(collision, 16) != CollisionBlocksRelativeOffset ||
            ReadInt32(collision, 20) != CollisionTrianglesRelativeOffset ||
            ReadInt32(collision, 24) != assignmentsRelative ||
            ReadInt32(collision, 28) != flagsRelative)
        {
            throw new InvalidDataException("The collision component header changed.");
        }
        int tree = 4 + CollisionTreeRelativeOffset;
        int blocks = 4 + CollisionBlocksRelativeOffset;
        int triangles = 4 + CollisionTrianglesRelativeOffset;
        int assignments = 4 + assignmentsRelative;
        int flags = 4 + flagsRelative;
        if (blocks - tree != CollisionTreeCapacityBytes ||
            triangles - blocks != CollisionBlocksCapacityBytes ||
            triangles + (triangleCount * 12) != assignments ||
            assignments + triangleCount > flags ||
            flags + CollisionFlagsLength != collision.Length)
        {
            throw new InvalidDataException("The collision component partitions changed.");
        }
        return new(tree, blocks, triangles, assignments, flags,
            CollisionTreeCapacityBytes, CollisionBlocksCapacityBytes);
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
        return new(tree.ToArray(), blocks.ToArray(), cells, starts.Count, usedBlockBytes);
    }

    private static CollisionRepack RepackCollisionIndex(
        NativeCollisionIndex native,
        IReadOnlyDictionary<Id65AuthoringCollisionCell, int[]> desired,
        int triangleCount,
        int blockCapacityLimit)
    {
        if (desired.Count != native.Cells.Count || blockCapacityLimit > native.BlockBytes.Length)
            throw new InvalidDataException("The collision repack changed cell ownership or authorized capacity.");
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
        if (usedBytes > blockCapacityLimit)
        {
            throw new InvalidDataException(
                $"The collision repack needs 0x{usedBytes:X} block bytes; capacity is 0x{blockCapacityLimit:X}.");
        }
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
            changed.OrderBy(cell => cell.Z).ThenBy(cell => cell.Y).ThenBy(cell => cell.X).ToArray());
    }

    private static void ValidateTwoTileSectorSemantic(byte[] sector)
    {
        uint dLow = ReadUInt32(sector, 40);
        uint dHigh = ReadUInt32(sector, 84);
        if (dLow != 0x1E03C000 || dHigh != dLow ||
            Convert.ToHexString(sector.AsSpan(64, 8)) != ExpectedLowDetailFaceHex ||
            Convert.ToHexString(sector.AsSpan(128, 16)) != ExpectedHighDetailFaceHex)
        {
            throw new InvalidDataException("The added HP/LP vertex or face bytes failed exact readback.");
        }
        Id65AuthoringPoint decoded = DecodeSceneVertex(sector, dLow);
        if (decoded != PointD)
            throw new InvalidDataException("The added HP/LP vertex does not decode to stable point D.");
    }

    private static void ValidateSectorCounts(
        byte[] sector,
        int lowVertices,
        int lowFaces,
        int highVertices,
        int highFaces)
    {
        if (sector[16] != lowVertices || sector[17] != 3 || sector[18] != lowFaces ||
            sector[20] != highVertices || sector[21] != 3 || sector[22] != highFaces)
        {
            throw new InvalidDataException("Sector 216 HP/LP vertex, color, or face counts changed.");
        }
        int expectedLength = checked((7 + lowVertices + 3 + (lowFaces * 2) +
                                      highVertices + 6 + (highFaces * 4)) * 4);
        if (sector.Length != expectedLength)
            throw new InvalidDataException("Sector 216 table counts do not exactly partition its bytes.");
    }

    private static void ValidateOcclusion(byte[] occlusion)
    {
        if (occlusion.Length != OcclusionLength || ReadInt32(occlusion, 0) != occlusion.Length ||
            ReadInt32(occlusion, 8) != 16)
        {
            throw new InvalidDataException("The fixed v2 occlusion directory changed.");
        }
        int environmentEnd = 4 + ReadInt32(occlusion, 4);
        int owners = 0;
        for (int group = 0; group < 16; group++)
        {
            int offset = 4 + ReadInt32(occlusion, 12 + (group * 4));
            if (offset < 76 || offset >= environmentEnd)
                throw new InvalidDataException("An occlusion group pointer is outside its environment portion.");
            while (offset < environmentEnd && occlusion[offset] != 0xFF)
            {
                if (occlusion[offset] == SectorIndex)
                {
                    owners++;
                    if (group != 0)
                        throw new InvalidDataException("Sector 216 escaped occlusion group 0.");
                }
                offset++;
            }
            if (offset >= environmentEnd)
                throw new InvalidDataException("An occlusion group has no terminator.");
        }
        if (owners != 1)
            throw new InvalidDataException("Sector 216 must have exactly one group-0 occlusion owner.");
    }

    private static void ValidateLimits(
        Id65TerrainCompilerLimits limits,
        ModelState baseState,
        ModelState twoTileState)
    {
        if (limits.ModelByteCapacity < ModelByteLength)
            throw new InvalidDataException("ID65 terrain model byte capacity is insufficient.");
        if (limits.AvailableV2TailBytes < ExpectedTwoTileUsedModelBytes - V2UsedModelBytes)
            throw new InvalidDataException("ID65 terrain v2 tail capacity is insufficient.");
        if (limits.MaximumOutputUsedModelBytes < ExpectedTwoTileUsedModelBytes)
            throw new InvalidDataException("ID65 terrain used-model capacity is insufficient.");
        if (limits.MaximumLowDetailVertexCount < twoTileState.LowVertices)
            throw new InvalidDataException("ID65 terrain LP vertex capacity is insufficient.");
        if (limits.MaximumHighDetailVertexCount < twoTileState.HighVertices)
            throw new InvalidDataException("ID65 terrain HP vertex capacity is insufficient.");
        if (limits.MaximumSectorFaceCount < Math.Max(twoTileState.LowFaces, twoTileState.HighFaces))
            throw new InvalidDataException("ID65 terrain sector face capacity is insufficient.");
        if (limits.MaximumCollisionTriangleCount < twoTileState.CollisionTriangles)
            throw new InvalidDataException("ID65 terrain collision triangle capacity is insufficient.");
        if (limits.CollisionTreeCapacityBytes < CollisionTreeCapacityBytes)
            throw new InvalidDataException("ID65 terrain collision tree capacity is insufficient.");
        if (limits.CollisionBlockCapacityBytes < twoTileState.CollisionBlocksUsedBytes)
            throw new InvalidDataException("ID65 terrain collision block capacity is insufficient.");
        if (baseState.Model.Length > limits.ModelByteCapacity || twoTileState.Model.Length > limits.ModelByteCapacity)
            throw new InvalidDataException("ID65 terrain fixed model exceeds authorized capacity.");
    }

    private static Id65ModelComponentRelocation[] BuildRelocations(ModelState source, ModelState output)
    {
        if (!source.Components.Select(item => item.Name).SequenceEqual(output.Components.Select(item => item.Name)))
            throw new InvalidDataException("The terrain component ownership sequence changed.");
        return source.Components.Zip(output.Components).Select(pair => new Id65ModelComponentRelocation(
            $"component.{pair.First.Name}",
            pair.First.Offset,
            pair.First.ByteLength,
            pair.Second.Offset,
            pair.Second.ByteLength,
            pair.First.Sha256,
            pair.Second.Sha256,
            pair.First.Bytes.SequenceEqual(pair.Second.Bytes))).ToArray();
    }

    private static Id65StableHandleRebase[] BuildHandleRebases(
        ModelState source,
        ModelState output,
        Id65CompiledAuthoringModel lockedV2)
    {
        Dictionary<string, (string Kind, Id65LogicalAddress Address)> sourceAddresses =
            BuildStateAddresses(source, lockedV2);
        Dictionary<string, (string Kind, Id65LogicalAddress Address)> outputAddresses =
            BuildStateAddresses(output, lockedV2);
        return sourceAddresses.Keys.Concat(outputAddresses.Keys).Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .Select(id =>
            {
                bool hasSource = sourceAddresses.TryGetValue(id, out var before);
                bool hasOutput = outputAddresses.TryGetValue(id, out var after);
                string kind = hasSource ? before.Kind : after.Kind;
                if (hasSource && hasOutput && before.Kind != after.Kind)
                    throw new InvalidDataException($"Stable terrain handle {id} changed kind.");
                return new Id65StableHandleRebase(
                    id,
                    kind,
                    hasSource ? before.Address : null,
                    hasOutput ? after.Address : null);
            }).ToArray();
    }

    private static Dictionary<string, (string Kind, Id65LogicalAddress Address)> BuildStateAddresses(
        ModelState state,
        Id65CompiledAuthoringModel lockedV2)
    {
        Dictionary<string, (string Kind, Id65LogicalAddress Address)> result = new(StringComparer.Ordinal);
        foreach (Id65StableHandleRebase rebase in lockedV2.HandleRebases)
        {
            if (!rebase.Output.HasValue)
                throw new InvalidDataException("The locked v2 base has an unresolved stable handle.");
            result.Add(rebase.StableId, (rebase.Kind, rebase.Output.Value));
        }
        if (state.CollisionTriangles == BaseCollisionTriangleCount + 1)
        {
            result.Add(TriangleId, ("terrain-tile", new("terrain-tile", SectorIndex, 1)));
            result.Add($"{AddedVertexHandle}:lp", ("low-detail-vertex", new("scene-sector", SectorIndex, 3)));
            result.Add($"{AddedVertexHandle}:hp", ("high-detail-vertex", new("scene-sector", SectorIndex, 3)));
            result.Add($"{TriangleId}:lp", ("low-detail-face", new("scene-sector", SectorIndex, 1)));
            result.Add($"{TriangleId}:hp", ("high-detail-face", new("scene-sector", SectorIndex, 1)));
            result.Add($"{TriangleId}:collision", ("collision-triangle", new("collision-triangle", AddedCollisionTriangleIndex, 0)));
        }
        return result;
    }

    private static Id65TerrainCapacityReadback BuildCapacity(ModelState source, ModelState output) => new(
        ModelByteLength,
        source.UsedModelBytes,
        output.UsedModelBytes,
        source.ZeroTailBytes,
        output.ZeroTailBytes,
        source.EnvironmentBytes,
        output.EnvironmentBytes,
        source.CollisionBytes,
        output.CollisionBytes,
        source.LowVertices,
        output.LowVertices,
        source.LowFaces,
        output.LowFaces,
        source.HighVertices,
        output.HighVertices,
        source.HighFaces,
        output.HighFaces,
        source.CollisionTriangles,
        output.CollisionTriangles,
        source.CollisionBlocksUsedBytes,
        output.CollisionBlocksUsedBytes,
        CollisionTreeCapacityBytes,
        CollisionBlocksCapacityBytes);

    private static Id65TerrainTriangleReadback BuildTriangleReadback(
        bool sourcePresent,
        bool outputPresent,
        ModelState twoTileState) =>
        new(
            TriangleId,
            AddedVertexHandle,
            sourcePresent,
            outputPresent,
            SectorIndex,
            3,
            3,
            1,
            1,
            AddedCollisionTriangleIndex,
            ExpectedAddedVertexWordHex,
            ExpectedLowDetailFaceHex,
            ExpectedHighDetailFaceHex,
            ExpectedCollisionTriangleHex,
            25_088,
            -25_088,
            TargetCell,
            Array.AsReadOnly(twoTileState.TargetCellSequence.ToArray()),
            0);

    private static ModelDiffProof BuildDiffProof(byte[] before, byte[] after)
    {
        if (before.Length != after.Length)
            throw new InvalidDataException("The terrain transaction changed fixed model capacity.");
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

    private static string HashDeterministicIdentity(
        Id65TerrainMutationKind kind,
        Id65TerrainAuthoringManifest source,
        Id65TerrainAuthoringManifest output,
        ModelState sourceState,
        ModelState outputState,
        ModelDiffProof diff,
        string relocationHash,
        string rebaseHash) =>
        Hash(Encoding.UTF8.GetBytes(string.Join('\n',
        [
            ProfileId,
            kind.ToString(),
            source.CanonicalSha256,
            output.CanonicalSha256,
            Hash(sourceState.Model),
            Hash(outputState.Model),
            diff.ManifestSha256,
            relocationHash,
            rebaseHash
        ])));

    private static void ValidateDeterministicPins(
        Id65TerrainMutationKind kind,
        Id65TerrainAuthoringManifest source,
        Id65TerrainAuthoringManifest output,
        ModelDiffProof diff,
        string relocationHash,
        string rebaseHash,
        string deterministicHash)
    {
        string expectedSourceManifest = kind == Id65TerrainMutationKind.AddTriangle
            ? ExpectedSubstrateManifestSha256
            : ExpectedTwoTileManifestSha256;
        string expectedOutputManifest = kind == Id65TerrainMutationKind.AddTriangle
            ? ExpectedTwoTileManifestSha256
            : ExpectedSubstrateManifestSha256;
        string expectedDiff = kind == Id65TerrainMutationKind.AddTriangle
            ? ExpectedAddDiffManifestSha256
            : ExpectedRemoveDiffManifestSha256;
        string expectedRelocations = kind == Id65TerrainMutationKind.AddTriangle
            ? ExpectedAddRelocationMapSha256
            : ExpectedRemoveRelocationMapSha256;
        string expectedRebases = kind == Id65TerrainMutationKind.AddTriangle
            ? ExpectedAddHandleRebaseMapSha256
            : ExpectedRemoveHandleRebaseMapSha256;
        string expectedPlan = kind == Id65TerrainMutationKind.AddTriangle
            ? ExpectedAddDeterministicPlanSha256
            : ExpectedRemoveDeterministicPlanSha256;
        if (source.CanonicalSha256 != expectedSourceManifest ||
            output.CanonicalSha256 != expectedOutputManifest ||
            diff.ManifestSha256 != expectedDiff ||
            relocationHash != expectedRelocations ||
            rebaseHash != expectedRebases ||
            deterministicHash != expectedPlan)
        {
            throw new InvalidDataException("The frozen deterministic ID65 terrain transaction pins changed.");
        }
    }

    private static string Address(Id65LogicalAddress? address) => address.HasValue
        ? string.Create(CultureInfo.InvariantCulture,
            $"{address.Value.Space}:{address.Value.Primary}:{address.Value.Secondary}")
        : "none";

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
            throw new InvalidDataException("The v2 portal count changed from zero.");
        return new(name, offset, length, bytes, Hash(bytes));
    }

    private static byte[] ExtractLastSector216(byte[] environment, int expectedLength)
    {
        if (ReadInt32(environment, 4) != 217)
            throw new InvalidDataException("The v2 environment no longer owns 217 sectors.");
        int offset = checked(4 + ReadInt32(environment, 8 + (SectorIndex * 4)));
        if (offset + expectedLength != environment.Length)
            throw new InvalidDataException("Sector 216 no longer exactly terminates the environment.");
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

    private static ModelState StateForCompiledModel(byte[] model, int usedBytes)
    {
        bool expanded = Hash(model) == ExpectedTwoTileModelSha256;
        if (!expanded && Hash(model) != ExpectedV2ModelSha256)
            throw new InvalidDataException("An inverted terrain plan contains an unknown model.");
        ComponentState[] components = expanded
            ?
            [
                Component(model, "texture", TextureOffset, TextureLength),
                Component(model, "environment", V2EnvironmentOffset, TwoTileEnvironmentLength),
                Component(model, "occlusion", TwoTileOcclusionOffset, OcclusionLength),
                Component(model, "special-surface", TwoTileSpecialOffset, SpecialLength),
                Component(model, "collision", TwoTileCollisionOffset, TwoTileCollisionLength),
                Component(model, "cyclorama", TwoTileCycloramaOffset, CycloramaLength),
                Component(model, "portal-table", TwoTilePortalOffset, PortalLength, false),
                Component(model, "particles", TwoTileParticlesOffset, ParticlesLength),
                Component(model, "sound", TwoTileSoundOffset, SoundLength)
            ]
            :
            [
                Component(model, "texture", TextureOffset, TextureLength),
                Component(model, "environment", V2EnvironmentOffset, V2EnvironmentLength),
                Component(model, "occlusion", V2OcclusionOffset, OcclusionLength),
                Component(model, "special-surface", V2SpecialOffset, SpecialLength),
                Component(model, "collision", V2CollisionOffset, V2CollisionLength),
                Component(model, "cyclorama", V2CycloramaOffset, CycloramaLength),
                Component(model, "portal-table", V2PortalOffset, PortalLength, false),
                Component(model, "particles", V2ParticlesOffset, ParticlesLength),
                Component(model, "sound", V2SoundOffset, SoundLength)
            ];
        return new(
            model.ToArray(),
            components,
            usedBytes,
            ModelByteLength - usedBytes,
            expanded ? TwoTileEnvironmentLength : V2EnvironmentLength,
            expanded ? TwoTileCollisionLength : V2CollisionLength,
            expanded ? 4 : 3,
            expanded ? 2 : 1,
            expanded ? 4 : 3,
            expanded ? 2 : 1,
            expanded ? BaseCollisionTriangleCount + 1 : BaseCollisionTriangleCount,
            expanded ? ExpectedTwoTileBlocksUsedBytes : V2CollisionBlocksUsedBytes,
            expanded ? [AddedCollisionTriangleIndex, ExistingCollisionTriangleIndex] : [ExistingCollisionTriangleIndex]);
    }

    private static bool IsStableToken(string value) =>
        !string.IsNullOrWhiteSpace(value) && value.All(character =>
            (character >= 'a' && character <= 'z') ||
            (character >= '0' && character <= '9') ||
            character is '.' or '-' or ':');

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
        if (usedBytes < 0 || usedBytes > model.Length ||
            model.AsSpan(usedBytes).IndexOfAnyExcept((byte)0) >= 0)
        {
            throw new InvalidDataException("The ID65 model suffix is not exact zero tail.");
        }
    }

    private static void RequireHash(string actual, string expected, string label)
    {
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
            throw new InvalidDataException($"The exact {label} SHA-256 changed: {actual}.");
    }

    internal static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private sealed record ComponentState(
        string Name,
        int Offset,
        int ByteLength,
        byte[] Bytes,
        string Sha256);

    private sealed record ModelState(
        byte[] Model,
        IReadOnlyList<ComponentState> Components,
        int UsedModelBytes,
        int ZeroTailBytes,
        int EnvironmentBytes,
        int CollisionBytes,
        int LowVertices,
        int LowFaces,
        int HighVertices,
        int HighFaces,
        int CollisionTriangles,
        int CollisionBlocksUsedBytes,
        IReadOnlyList<int> TargetCellSequence);

    private sealed record CollisionLayout(
        int TreeOffset,
        int BlocksOffset,
        int TriangleOffset,
        int AssignmentsOffset,
        int FlagsOffset,
        int TreeCapacityBytes,
        int BlocksCapacityBytes);

    private sealed record CollisionCellBinding(
        Id65AuthoringCollisionCell Cell,
        int TreePointerByteOffset,
        int SourceBlockWordOffset,
        int[] OrderedTriangleIndexes);

    private sealed record NativeCollisionIndex(
        byte[] TreeBytes,
        byte[] BlockBytes,
        IReadOnlyDictionary<Id65AuthoringCollisionCell, CollisionCellBinding> Cells,
        int GroupStartCount,
        int UsedBlockBytes);

    private sealed record CollisionRepack(
        byte[] TreeBytes,
        byte[] BlockBytes,
        int UsedBlockBytes,
        IReadOnlyList<Id65AuthoringCollisionCell> ChangedCells);

    private sealed record CollisionIndexReadback(
        int UsedBlockBytes,
        IReadOnlyList<int> TargetSequence);

    private sealed record ModelDiffProof(
        int ChangedByteCount,
        int RangeCount,
        string ManifestSha256);
}
