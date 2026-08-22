using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Spyro.Editor.Core.Exporting;

internal sealed record Id65V2NativeTextureCompilerLimits(
    int ModelByteCapacity = Id65V2NativeTextureCompositionCompiler.ModelByteLength,
    int AvailableV2TailBytes = Id65V2NativeTextureCompositionCompiler.SourceZeroTailBytes,
    int MaximumOutputUsedModelBytes = Id65V2NativeTextureCompositionCompiler.ModelByteLength,
    int MaximumNativeTextureId = 127,
    int AuthorizedWitnessRecordCount = 1);

internal sealed record Id65V2NativeTextureAllocationReadback(
    int SourceTextureCount,
    int OutputTextureCount,
    int FirstPrivateTextureId,
    int LastPrivateTextureId,
    int RecordGrowthBytes,
    int SourceTextureComponentBytes,
    int OutputTextureComponentBytes,
    int SourceUsedModelBytes,
    int OutputUsedModelBytes,
    int SourceZeroTailBytes,
    int OutputZeroTailBytes,
    int SevenBitRemainingIds,
    int TailRemainingWholeRecords);

internal sealed record Id65V2NativeTextureFaceBindingReadback(
    string RenderFaceId,
    string TextureIntentId,
    int SectorIndex,
    int LowDetailFaceIndex,
    int HighDetailFaceIndex,
    int SourceLowFaceModelOffset,
    int OutputLowFaceModelOffset,
    int SourceHighFaceModelOffset,
    int OutputHighFaceModelOffset,
    int SourceHighTextureWordModelOffset,
    int OutputHighTextureWordModelOffset,
    string SourceLowFaceHex,
    string OutputLowFaceHex,
    string SourceHighFaceHex,
    string OutputHighFaceHex,
    int SourceTextureId,
    int OutputTextureId,
    int SourceT25UseCount,
    int OutputT25UseCount,
    int OutputT66UseCount,
    bool HighTextureIdOnlyChanged,
    bool LowFacePreserved,
    bool ExplicitHpLpPairingVerified);

internal sealed record Id65V2NativeTextureDiffRange(
    string Space,
    int RelativeOffset,
    long WadOffset,
    int ByteLength,
    string BeforeSha256,
    string AfterSha256,
    string Owner);

/// <summary>
/// Defensive projection of the already-proven T66 page/row allocation. It
/// deliberately has no path, foundation model, foundation face binding,
/// append-level afterimage, exporter, or publication surface. A later
/// single-pass composite compiler can consume the immutable row/page proof,
/// but no current compiler may stack this afterimage with another exact-v2
/// slice.
/// </summary>
internal sealed class Id65V2NativeTextureWitness
{
    private readonly ReadOnlyCollection<Id65V2NativeTexturePackedRow> _packedRows;
    private readonly ReadOnlyCollection<Id65V2NativeTexturePagePatch> _pagePatches;

    internal Id65V2NativeTextureWitness(
        string profileId,
        string donorManifestId,
        string donorCompleteRecordSha256,
        string sourceTexturePagesSha256,
        string outputTexturePagesSha256,
        string sourceTextureComponentSha256,
        string outputTextureComponentSha256,
        int sourceTextureCount,
        int outputTextureCount,
        int privateTextureId,
        int donorWadEntry,
        int donorTextureId,
        int materialTemplateTextureId,
        IEnumerable<Id65V2NativeTexturePackedRow> packedRows,
        IEnumerable<Id65V2NativeTexturePagePatch> pagePatches,
        string canonicalSha256)
    {
        ProfileId = profileId;
        DonorManifestId = donorManifestId;
        DonorCompleteRecordSha256 = donorCompleteRecordSha256;
        SourceTexturePagesSha256 = sourceTexturePagesSha256;
        OutputTexturePagesSha256 = outputTexturePagesSha256;
        SourceTextureComponentSha256 = sourceTextureComponentSha256;
        OutputTextureComponentSha256 = outputTextureComponentSha256;
        SourceTextureCount = sourceTextureCount;
        OutputTextureCount = outputTextureCount;
        PrivateTextureId = privateTextureId;
        DonorWadEntry = donorWadEntry;
        DonorTextureId = donorTextureId;
        MaterialTemplateTextureId = materialTemplateTextureId;
        _packedRows = Array.AsReadOnly(packedRows.Select(row => row.DeepCopy()).ToArray());
        _pagePatches = Array.AsReadOnly(pagePatches.Select(patch => patch.DeepCopy()).ToArray());
        CanonicalSha256 = canonicalSha256;
    }

    public string ProfileId { get; }
    public string DonorManifestId { get; }
    public string DonorCompleteRecordSha256 { get; }
    public string SourceTexturePagesSha256 { get; }
    public string OutputTexturePagesSha256 { get; }
    public string SourceTextureComponentSha256 { get; }
    public string OutputTextureComponentSha256 { get; }
    public int SourceTextureCount { get; }
    public int OutputTextureCount { get; }
    public int PrivateTextureId { get; }
    public int DonorWadEntry { get; }
    public int DonorTextureId { get; }
    public int MaterialTemplateTextureId { get; }
    public int PackedRowCount => _packedRows.Count;
    public int PagePatchCount => _pagePatches.Count;
    public string CanonicalSha256 { get; }
    public bool ContainsFoundationModelBytes => false;
    public bool ContainsFoundationFaceBinding => false;
    public bool ContainsAppendAfterimage => false;
    public bool ContainsPublisherState => false;
    public bool LaterSinglePassCompositeInputAvailable => true;
    public bool CrossSliceCompositionAuthorized => false;
    public bool AfterimageStackingAuthorized => false;

    internal IReadOnlyList<Id65V2NativeTexturePackedRow> CopyPackedRows() =>
        _packedRows.Select(row => row.DeepCopy()).ToArray();

    internal IReadOnlyList<Id65V2NativeTexturePagePatch> CopyPagePatches() =>
        _pagePatches.Select(patch => patch.DeepCopy()).ToArray();
}

internal sealed class Id65V2NativeTexturePackedRow
{
    private readonly byte[] _low;
    private readonly byte[] _high;

    internal Id65V2NativeTexturePackedRow(
        int textureId,
        int donorWadEntry,
        int donorTextureId,
        bool synthetic,
        byte[] low,
        byte[] high,
        string lowSha256,
        string highSha256)
    {
        TextureId = textureId;
        DonorWadEntry = donorWadEntry;
        DonorTextureId = donorTextureId;
        Synthetic = synthetic;
        _low = low.ToArray();
        _high = high.ToArray();
        LowSha256 = lowSha256;
        HighSha256 = highSha256;
    }

    public int TextureId { get; }
    public int DonorWadEntry { get; }
    public int DonorTextureId { get; }
    public bool Synthetic { get; }
    public string LowSha256 { get; }
    public string HighSha256 { get; }
    internal byte[] CopyLow() => _low.ToArray();
    internal byte[] CopyHigh() => _high.ToArray();
    internal Id65V2NativeTexturePackedRow DeepCopy() =>
        new(TextureId, DonorWadEntry, DonorTextureId, Synthetic, _low, _high, LowSha256, HighSha256);
}

internal sealed class Id65V2NativeTexturePagePatch
{
    private readonly byte[] _before;
    private readonly byte[] _after;

    internal Id65V2NativeTexturePagePatch(
        int relativeOffset,
        byte[] before,
        byte[] after,
        string beforeSha256,
        string afterSha256,
        string owner)
    {
        RelativeOffset = relativeOffset;
        _before = before.ToArray();
        _after = after.ToArray();
        BeforeSha256 = beforeSha256;
        AfterSha256 = afterSha256;
        Owner = owner;
    }

    public int RelativeOffset { get; }
    public int ByteLength => _before.Length;
    public string BeforeSha256 { get; }
    public string AfterSha256 { get; }
    public string Owner { get; }
    internal byte[] CopyBefore() => _before.ToArray();
    internal byte[] CopyAfter() => _after.ToArray();
    internal Id65V2NativeTexturePagePatch DeepCopy() =>
        new(RelativeOffset, _before, _after, BeforeSha256, AfterSha256, Owner);
}

internal sealed class Id65CompiledV2NativeTextureComposition
{
    private readonly byte[] _sourceRow80;
    private readonly byte[] _outputRow80;
    private readonly byte[] _sourcePages;
    private readonly byte[] _outputPages;
    private readonly byte[] _sourceModel;
    private readonly byte[] _outputModel;
    private readonly ReadOnlyCollection<Id65ModelComponentRelocation> _relocations;
    private readonly ReadOnlyCollection<Id65StableHandleRebase> _handleRebases;
    private readonly ReadOnlyCollection<Id65V2NativeTextureDiffRange> _diffRanges;

    internal Id65CompiledV2NativeTextureComposition(
        Id65AuthoringManifest manifest,
        Id65V2NativeTextureWitness witness,
        byte[] sourceRow80,
        byte[] outputRow80,
        byte[] sourcePages,
        byte[] outputPages,
        byte[] sourceModel,
        byte[] outputModel,
        int changedPageByteCount,
        int changedModelByteCount,
        int changedRow80ByteCount,
        IEnumerable<Id65V2NativeTextureDiffRange> diffRanges,
        string diffManifestSha256,
        IEnumerable<Id65ModelComponentRelocation> relocations,
        string relocationMapSha256,
        IEnumerable<Id65StableHandleRebase> handleRebases,
        string handleRebaseMapSha256,
        Id65V2NativeTextureAllocationReadback allocation,
        Id65V2NativeTextureFaceBindingReadback faceBinding,
        string deterministicPlanSha256)
    {
        ProfileId = Id65V2NativeTextureCompositionCompiler.ProfileId;
        Manifest = manifest;
        Witness = witness;
        ManifestSha256 = manifest.CanonicalSha256;
        WitnessSha256 = witness.CanonicalSha256;
        _sourceRow80 = sourceRow80.ToArray();
        _outputRow80 = outputRow80.ToArray();
        _sourcePages = sourcePages.ToArray();
        _outputPages = outputPages.ToArray();
        _sourceModel = sourceModel.ToArray();
        _outputModel = outputModel.ToArray();
        SourceRow80Sha256 = Hash(sourceRow80);
        OutputRow80Sha256 = Hash(outputRow80);
        SourceTexturePagesSha256 = Hash(sourcePages);
        OutputTexturePagesSha256 = Hash(outputPages);
        SourceModelSha256 = Hash(sourceModel);
        OutputModelSha256 = Hash(outputModel);
        ChangedTexturePageByteCount = changedPageByteCount;
        ChangedModelByteCount = changedModelByteCount;
        ChangedRow80ByteCount = changedRow80ByteCount;
        _diffRanges = Array.AsReadOnly(diffRanges.ToArray());
        DiffManifestSha256 = diffManifestSha256;
        _relocations = Array.AsReadOnly(relocations.ToArray());
        RelocationMapSha256 = relocationMapSha256;
        _handleRebases = Array.AsReadOnly(handleRebases.ToArray());
        HandleRebaseMapSha256 = handleRebaseMapSha256;
        Allocation = allocation;
        FaceBinding = faceBinding;
        DeterministicPlanSha256 = deterministicPlanSha256;
    }

    public string ProfileId { get; }
    public Id65AuthoringManifest Manifest { get; }
    public Id65V2NativeTextureWitness Witness { get; }
    public string ManifestSha256 { get; }
    public string WitnessSha256 { get; }
    public string SourceRow80Sha256 { get; }
    public string OutputRow80Sha256 { get; }
    public string SourceTexturePagesSha256 { get; }
    public string OutputTexturePagesSha256 { get; }
    public string SourceModelSha256 { get; }
    public string OutputModelSha256 { get; }
    public int ChangedTexturePageByteCount { get; }
    public int ChangedModelByteCount { get; }
    public int ChangedRow80ByteCount { get; }
    public IReadOnlyList<Id65V2NativeTextureDiffRange> DiffRanges => _diffRanges;
    public string DiffManifestSha256 { get; }
    public IReadOnlyList<Id65ModelComponentRelocation> Relocations => _relocations;
    public string RelocationMapSha256 { get; }
    public IReadOnlyList<Id65StableHandleRebase> HandleRebases => _handleRebases;
    public string HandleRebaseMapSha256 { get; }
    public Id65V2NativeTextureAllocationReadback Allocation { get; }
    public Id65V2NativeTextureFaceBindingReadback FaceBinding { get; }
    public string DeterministicPlanSha256 { get; }
    public bool TypedWitnessProjectionVerified => true;
    public bool DestinationPrivateRecordVerified => true;
    public bool DestinationPrivatePageAllocationVerified => true;
    public bool ExactHpLpBindingReadbackVerified => true;
    public bool CollisionAndOcclusionPreserved => true;
    public bool TargetEntryHeaderPreserved => true;
    public bool ProtectedRow80SubfilesPreserved => true;
    public bool RetailWadEntriesExcluded => true;
    public bool ExecutableExcluded => true;
    public bool ScusPreserved => true;
    public bool ExactInverseVerified => true;
    public bool DeterministicReadbackRequired => true;
    public bool LaterSinglePassCompositeInputAvailable => true;
    public bool CrossSliceCompositionAuthorized => false;
    public bool AfterimageStackingAuthorized => false;
    public bool ExactV2OnlyTerrainCompilerAfterimageCompatible => false;
    public bool WritesFileSystem => false;
    public bool WritesDiscImage => false;
    public bool WritesCue => false;
    public bool AppIntegrated => false;
    public bool CreateBinEnabled => false;
    public bool NormalCreateBinEnabled => false;
    public bool RuntimeCandidateAuthorized => false;
    public bool RetiredPublisherCalled => false;
    public bool ReleaseIntegrated => false;
    public bool PromotionAuthorized => false;
    public bool Publishable => false;

    internal byte[] CopySourceRow80() => _sourceRow80.ToArray();
    internal byte[] CopyOutputRow80() => _outputRow80.ToArray();
    internal byte[] CopySourceTexturePages() => _sourcePages.ToArray();
    internal byte[] CopyOutputTexturePages() => _outputPages.ToArray();
    internal byte[] CopySourceModel() => _sourceModel.ToArray();
    internal byte[] CopyOutputModel() => _outputModel.ToArray();

    private static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes));
}

/// <summary>
/// Pure in-memory T66 texture proof over the exact negative-winding v2 model.
/// It consumes a typed projection of the old static page/row proof, never its
/// retired positive-wound model bytes. It has no path, writer, CUE, App,
/// Create-BIN, runtime-candidate, release, or promotion surface.
/// </summary>
internal static class Id65V2NativeTextureCompositionCompiler
{
    public const string ProfileId = "id65-v2-native-texture-composition-static-in-memory-v1";
    public const string ManifestProfileId = "id65-authoring-manifest-minimal-v2-private-t66-v1";
    public const string WitnessProfileId = "id65-v2-native-texture-witness-gnastysworld-t12-private-t66-v1";
    public const string PrivateTextureIntentId = "texture.private.record66.gnastysworld-t12";
    public const string PrivateTextureSourceHandle = "donor.gnastysworld.wad70.t12";
    public const int Row80ByteLength = 0x2E2000;
    public const int TexturePagesDataRelativeOffset = 0x800;
    public const int TexturePagesByteLength = 0xDE000;
    public const int ModelDataRelativeOffset = 0xDE800;
    public const int ModelByteLength = 0x94800;
    public const int SourceTextureCount = 66;
    public const int OutputTextureCount = 67;
    public const int PrivateTextureId = 66;
    public const int MaterialTemplateTextureId = 25;
    public const int RecordGrowthBytes = 0xB8;
    public const int SourceTextureComponentBytes = 0x2F78;
    public const int OutputTextureComponentBytes = 0x3030;
    public const int SourceUsedModelBytes = 0x9457C;
    public const int OutputUsedModelBytes = 0x94634;
    public const int SourceZeroTailBytes = 0x284;
    public const int OutputZeroTailBytes = 0x1CC;
    public const int SectorIndex = 216;
    public const int LowDetailFaceIndex = 0;
    public const int HighDetailFaceIndex = 0;
    public const int SourceLowFaceModelOffset = 0x2B454;
    public const int OutputLowFaceModelOffset = 0x2B50C;
    public const int SourceHighFaceModelOffset = 0x2B480;
    public const int OutputHighFaceModelOffset = 0x2B538;
    public const int SourceHighTextureWordModelOffset = 0x2B488;
    public const int OutputHighTextureWordModelOffset = 0x2B540;
    public const long TexturePagesWadOffset = 0x6937000;
    public const long ModelWadOffset = 0x6A15000;
    public const string ExpectedSourceRow80Sha256 =
        "a21e3ace8f16e981dc1d75c4d2210e96ba2833200bd4e3054b2a1c0bb540ade9";
    public const string ExpectedOutputRow80Sha256 =
        "9a7ba28aa977c7f2211c5a467d09f5f0f8e8ebc6bcdd5366d22564f44dadd237";
    public const string ExpectedSourceTexturePagesSha256 =
        "5fb81c4ac63eb235a172c4f16f83ef08efb148a6f543ef6100ba21359ade408f";
    public const string ExpectedOutputTexturePagesSha256 =
        "34a6d81c740c1e2494ede138e45556941e6daf59d21668ca2b594055f61ac47a";
    public const string ExpectedSourceTextureComponentSha256 =
        "f0ee8ff0d2e8554418b1bb17de860fada7af5106b07bf4908dfee179aaee6e92";
    public const string ExpectedOutputTextureComponentSha256 =
        "cb1002ff28dd862dc441a388dc09de57bc821467850791de67fb990b8f6ea79f";
    public const string ExpectedSourceModelSha256 =
        "2ea39a0c19f51f16abfd3cae73d13df01191e16d70f31644ae8c14c977581b79";
    public const string ExpectedOutputModelSha256 =
        "e9650867deb28a9b1673d56b7ae6f8576c0c9b1e739c6f52fdd321a53bab376f";
    public const string ExpectedExecutableSha256 =
        "fa5fc7981188b78fa7d7b78facca64c1f79dadb107515e9146ad178ade39d442";
    public const string ExpectedPrivateLowRowSha256 =
        "c32b270ab41d9e9299cca05a4a0d14f0addfa32a108c5aa8c4f509eef3b98d80";
    public const string ExpectedPrivateHighRowSha256 =
        "d63d613b56b7dcd5adf506847bab76fb9aa69e3ebb9d00643c76aec929557fcc";
    public const string ExpectedManifestSha256 =
        "94b2afea05dcc1539395ed3517c7d122d1f5433380240ece698f0b985a7b9f2b";
    public const string ExpectedWitnessSha256 =
        "b21ea01647b6a5b9484f5ce54efa8ed848836c9757a3fe927d15c84fa3b07b20";
    public const string ExpectedDiffManifestSha256 =
        "ad5df7fcf67bca07f9751004ba75a05a45267e85c2e982eda8b7951f43116ac4";
    public const string ExpectedRelocationMapSha256 =
        "d3f447d0d6278380b466afa4419d889e1607bb2cf3f4ce666ba8a6c28c534ebd";
    public const string ExpectedHandleRebaseMapSha256 =
        "59d08d33ce7177d7e9f762bb40311a154ce26f2dd70dcaf90f3c24c606d219d5";
    public const string ExpectedDeterministicPlanSha256 =
        "e1d1d8f6247951aa6b99ab48426877fae8a48306fef8b84dcc7e14c3878d03bf";
    public const int ExpectedChangedPageBytes = 402_194;
    public const int ExpectedChangedModelBytes = 517_811;
    public const int ExpectedChangedRow80Bytes = 920_005;
    public const int ExpectedPageDiffRangeCount = 7_949;
    public const int ExpectedModelDiffRangeCount = 47_294;
    public const int ExpectedRow80DiffRangeCount = 55_243;

    private const string RenderFaceId = "terrain.remote-pad.face.0";
    private const string LockedTextureIntentId = "texture.locked.record25";
    private const string ExpectedLowFaceHex = "0082100000821000";
    private const string ExpectedSourceHighFaceHex = "000001020101000219024001080A1000";
    private const string ExpectedOutputHighFaceHex = "000001020101000242024001080A1000";

    public static Id65AuthoringManifest CreateMinimalV2T66Manifest()
    {
        Id65AuthoringManifest baseline = Id65AuthoringModelCompiler.CreateMinimalV2EquivalentManifest();
        Id65TextureIntent locked = baseline.Textures.Single();
        Id65TextureIntent authored = new(
            PrivateTextureIntentId,
            Id65AuthoringIntentDisposition.AuthorExact,
            PrivateTextureId,
            PrivateTextureSourceHandle);
        return new(
            ManifestProfileId,
            baseline.LockedBase,
            baseline.Sectors,
            baseline.Vertices,
            baseline.RenderFaces.Select(face => face.Id == RenderFaceId
                ? face with { TextureIntentId = PrivateTextureIntentId }
                : face),
            baseline.Collision,
            baseline.Occlusion,
            [locked, authored],
            baseline.Mobys,
            baseline.Spawn,
            baseline.Music,
            baseline.Totals,
            baseline.Exit,
            baseline.Save);
    }

    public static Id65V2NativeTextureWitness ImportPinnedT66Witness(
        UnusedLevel65NativeTextureStaticPlan staticPlan)
    {
        ArgumentNullException.ThrowIfNull(staticPlan);
        RequirePinnedStaticTexturePlan(staticPlan);
        NativeTerrainTextureGlobalRepackSyntheticPlan global = staticPlan.Composition.GlobalPacking;
        NativeTerrainTextureGlobalRepackResearchPlan packing = global.PackingProof;
        if (!packing.ExactIndexedPixelReadbackVerified || !packing.ExactPaletteReadbackVerified ||
            !packing.PixelAliasRelationshipsPreserved || !packing.PaletteAliasRelationshipsPreserved ||
            !packing.LowDetailAliasPreserved || !packing.ProtectedStoragePreserved ||
            !packing.FixedDescriptorRowsPreserved || !packing.TargetMaterialBitsPreserved)
        {
            throw new InvalidDataException("The T66 witness lost an indexed-pixel, palette, alias, protected-page, fixed-descriptor, or material proof.");
        }

        List<Id65V2NativeTexturePackedRow> rows = [];
        foreach (NativeTerrainTextureGlobalRepackPackedRecord row in global.OriginalMovableRecords)
            rows.Add(ProjectRow(row, synthetic: false));
        foreach (NativeTerrainTextureGlobalRepackPackedRecord row in global.SyntheticRecords)
            rows.Add(ProjectRow(row, synthetic: true));
        ValidateProjectedRows(rows);

        Id65V2NativeTexturePagePatch[] patches = global.TexturePagePatches
            .OrderBy(patch => patch.WadOffset)
            .Select(patch =>
            {
                int relative = checked((int)(patch.WadOffset - staticPlan.TargetTexturePagesWadOffset));
                if (patch.Kind != "terrain-texture-global-repack-data" ||
                    patch.ByteLength <= 0 || patch.Before.Length != patch.ByteLength ||
                    patch.After.Length != patch.ByteLength || relative < 0 ||
                    relative + (long)patch.ByteLength > TexturePagesByteLength)
                {
                    throw new InvalidDataException("A T66 page patch escaped the exact ID65 texture-pages subfile.");
                }
                return new Id65V2NativeTexturePagePatch(
                    relative,
                    patch.Before,
                    patch.After,
                    Hash(patch.Before),
                    Hash(patch.After),
                    patch.Kind);
            })
            .ToArray();
        ValidateProjectedPagePatches(patches);

        string witnessHash = ComputeWitnessCanonicalSha256(
            WitnessProfileId,
            staticPlan.Donor.ManifestId,
            staticPlan.Donor.CompleteRecordSha256,
            staticPlan.SourceTexturePagesSha256,
            staticPlan.OutputTexturePagesSha256,
            ExpectedSourceTextureComponentSha256,
            ExpectedOutputTextureComponentSha256,
            rows,
            patches);
        RequireHash(witnessHash, ExpectedWitnessSha256, "typed T66 witness identity");
        return new(
            WitnessProfileId,
            staticPlan.Donor.ManifestId,
            staticPlan.Donor.CompleteRecordSha256,
            staticPlan.SourceTexturePagesSha256,
            staticPlan.OutputTexturePagesSha256,
            ExpectedSourceTextureComponentSha256,
            ExpectedOutputTextureComponentSha256,
            staticPlan.SourceTextureCount,
            staticPlan.OutputTextureCount,
            staticPlan.PrivateTextureId,
            staticPlan.Donor.DonorWadEntry,
            staticPlan.Donor.DonorTextureId,
            staticPlan.MaterialTemplateTextureId,
            rows,
            patches,
            witnessHash);
    }

    public static Id65CompiledV2NativeTextureComposition Compile(
        Id65AuthoringManifest manifest,
        Id65CompiledAuthoringModel v2Model,
        UnusedLevel65RemoteBlankCollisionWindingRepairV2StaticPlan v2Template,
        Id65V2NativeTextureWitness witness,
        Id65V2NativeTextureCompilerLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(v2Model);
        ArgumentNullException.ThrowIfNull(v2Template);
        ArgumentNullException.ThrowIfNull(witness);
        limits ??= new();
        ValidateManifest(manifest);
        ValidateV2Inputs(v2Model, v2Template);
        ValidateWitness(witness);
        ValidateCapacity(limits);

        byte[] sourceRow80 = v2Template.OutputData.ToArray();
        RequireHash(Hash(sourceRow80), ExpectedSourceRow80Sha256, "negative-winding v2 row80");
        byte[] sourcePages = sourceRow80.AsSpan(TexturePagesDataRelativeOffset, TexturePagesByteLength).ToArray();
        byte[] sourceModel = v2Model.CopyOutputModel();
        if (!sourceRow80.AsSpan(ModelDataRelativeOffset, ModelByteLength).SequenceEqual(sourceModel))
            throw new InvalidDataException("The compiled v2 model and v2 row80 model subfile conflict.");
        RequireHash(Hash(sourcePages), ExpectedSourceTexturePagesSha256, "v2 texture pages");
        RequireHash(Hash(sourceModel), ExpectedSourceModelSha256, "v2 source model");
        RequireHash(Hash(sourceModel.AsSpan(0, SourceTextureComponentBytes)),
            ExpectedSourceTextureComponentSha256, "v2 source texture component");

        byte[] outputPages = ApplyPagePatches(sourcePages, witness.CopyPagePatches(), reverse: false);
        byte[] outputTexture = BuildOutputTextureComponent(sourceModel, witness.CopyPackedRows());
        byte[] outputModel = ComposeOutputModel(sourceModel, outputTexture);
        byte[] outputRow80 = sourceRow80.ToArray();
        outputPages.CopyTo(outputRow80, TexturePagesDataRelativeOffset);
        outputModel.CopyTo(outputRow80, ModelDataRelativeOffset);

        RequireHash(Hash(outputPages), ExpectedOutputTexturePagesSha256, "T66 output texture pages");
        RequireHash(Hash(outputTexture), ExpectedOutputTextureComponentSha256, "T66 output texture component");
        RequireHash(Hash(outputModel), ExpectedOutputModelSha256, "T66 output model");
        RequireHash(Hash(outputRow80), ExpectedOutputRow80Sha256, "T66 output row80");

        Id65V2NativeTextureFaceBindingReadback face = ValidateFaceBinding(sourceModel, outputModel);
        Id65ModelComponentRelocation[] relocations = BuildAndValidateRelocations(sourceModel, outputModel);
        string relocationHash = HashRelocations(relocations);
        Id65StableHandleRebase[] rebases = BuildAndValidateHandleRebases(v2Model, manifest);
        string rebaseHash = HashHandleRebases(rebases);
        Id65V2NativeTextureDiffRange[] ranges = BuildDiffRanges(sourcePages, outputPages, sourceModel, outputModel);
        string diffHash = HashDiffManifest(ranges);
        int changedPages = CountChangedBytes(sourcePages, outputPages);
        int changedModel = CountChangedBytes(sourceModel, outputModel);
        int changedRow80 = CountChangedBytes(sourceRow80, outputRow80);
        if (changedPages != ExpectedChangedPageBytes || changedModel != ExpectedChangedModelBytes ||
            changedRow80 != ExpectedChangedRow80Bytes ||
            ranges.Count(range => range.Space == "texture-pages") != ExpectedPageDiffRangeCount ||
            ranges.Count(range => range.Space == "model") != ExpectedModelDiffRangeCount ||
            ranges.Length != ExpectedRow80DiffRangeCount)
        {
            throw new InvalidDataException("The exact T66 page/model/row80 diff boundary changed.");
        }
        ValidateProtectedData(sourceRow80, outputRow80, v2Template, relocations);

        Id65V2NativeTextureAllocationReadback allocation = new(
            SourceTextureCount,
            OutputTextureCount,
            PrivateTextureId,
            PrivateTextureId,
            RecordGrowthBytes,
            SourceTextureComponentBytes,
            OutputTextureComponentBytes,
            SourceUsedModelBytes,
            OutputUsedModelBytes,
            SourceZeroTailBytes,
            OutputZeroTailBytes,
            127 - PrivateTextureId,
            OutputZeroTailBytes / RecordGrowthBytes);
        string deterministicHash = Hash(Encoding.UTF8.GetBytes(string.Join('\n',
        [
            ProfileId,
            manifest.CanonicalSha256,
            witness.CanonicalSha256,
            ExpectedSourceRow80Sha256,
            ExpectedOutputRow80Sha256,
            ExpectedSourceModelSha256,
            ExpectedOutputModelSha256,
            ExpectedSourceTexturePagesSha256,
            ExpectedOutputTexturePagesSha256,
            diffHash,
            relocationHash,
            rebaseHash
        ])));
        RequireHash(diffHash, ExpectedDiffManifestSha256, "T66 logical-diff manifest");
        RequireHash(relocationHash, ExpectedRelocationMapSha256, "T66 component relocation map");
        RequireHash(rebaseHash, ExpectedHandleRebaseMapSha256, "T66 stable-handle rebase map");
        RequireHash(deterministicHash, ExpectedDeterministicPlanSha256, "T66 deterministic plan identity");

        Id65CompiledV2NativeTextureComposition compiled = new(
            manifest,
            witness,
            sourceRow80,
            outputRow80,
            sourcePages,
            outputPages,
            sourceModel,
            outputModel,
            changedPages,
            changedModel,
            changedRow80,
            ranges,
            diffHash,
            relocations,
            relocationHash,
            rebases,
            rebaseHash,
            allocation,
            face,
            deterministicHash);
        byte[] applied = ApplyRow80Transactional(compiled, sourceRow80, reverse: false);
        byte[] reversed = ApplyRow80Transactional(compiled, applied, reverse: true);
        if (!applied.SequenceEqual(outputRow80) || !reversed.SequenceEqual(sourceRow80) ||
            !InvertRelocations(InvertRelocations(relocations)).SequenceEqual(relocations) ||
            !InvertHandleRebases(InvertHandleRebases(rebases)).SequenceEqual(rebases))
        {
            throw new InvalidDataException("The T66 composition or its relocation/rebase map is not exactly invertible.");
        }
        return compiled;
    }

    internal static byte[] ApplyRow80Transactional(
        Id65CompiledV2NativeTextureComposition compiled,
        byte[] input,
        bool reverse)
    {
        ArgumentNullException.ThrowIfNull(compiled);
        ArgumentNullException.ThrowIfNull(input);
        byte[] expected = reverse ? compiled.CopyOutputRow80() : compiled.CopySourceRow80();
        byte[] replacement = reverse ? compiled.CopySourceRow80() : compiled.CopyOutputRow80();
        if (!input.SequenceEqual(expected))
            throw new InvalidDataException($"The T66 row80 {(reverse ? "afterimage" : "preimage")} changed.");
        return replacement;
    }

    internal static IReadOnlyList<Id65ModelComponentRelocation> InvertRelocations(
        IEnumerable<Id65ModelComponentRelocation> relocations)
    {
        ArgumentNullException.ThrowIfNull(relocations);
        return relocations.Select(item => new Id65ModelComponentRelocation(
            item.StableId,
            item.OutputRelativeOffset,
            item.OutputByteLength,
            item.SourceRelativeOffset,
            item.SourceByteLength,
            item.OutputSha256,
            item.SourceSha256,
            item.ContentsPreserved)).ToArray();
    }

    internal static IReadOnlyList<Id65StableHandleRebase> InvertHandleRebases(
        IEnumerable<Id65StableHandleRebase> rebases)
    {
        ArgumentNullException.ThrowIfNull(rebases);
        return rebases.Select(item => item with { Source = item.Output, Output = item.Source }).ToArray();
    }

    private static void RequirePinnedStaticTexturePlan(UnusedLevel65NativeTextureStaticPlan plan)
    {
        UnusedLevel65NativeTextureDonorManifest donor = plan.Donor;
        if (plan.ProfileId != UnusedLevel65NativeTextureAuthoringContract.ProfileId ||
            plan.SourceTextureCount != SourceTextureCount || plan.OutputTextureCount != OutputTextureCount ||
            plan.PrivateTextureId != PrivateTextureId || plan.MaterialTemplateTextureId != MaterialTemplateTextureId ||
            plan.RecordGrowthByteCount != RecordGrowthBytes ||
            plan.SourceTextureComponentByteLength != SourceTextureComponentBytes ||
            plan.OutputTextureComponentByteLength != OutputTextureComponentBytes ||
            !HashEquals(plan.SourceTexturePagesSha256, ExpectedSourceTexturePagesSha256) ||
            !HashEquals(plan.OutputTexturePagesSha256, ExpectedOutputTexturePagesSha256) ||
            !HashEquals(plan.OutputPrivateLowDetailRowSha256, ExpectedPrivateLowRowSha256) ||
            !HashEquals(plan.OutputPrivateHighDetailRowSha256, ExpectedPrivateHighRowSha256) ||
            donor.ManifestId != UnusedLevel65NativeTextureAuthoringContract.DonorManifestId ||
            donor.DonorWadEntry != 70 || donor.DonorTextureId != 12 ||
            !HashEquals(donor.CompleteRecordSha256,
                UnusedLevel65NativeTextureAuthoringContract.ExpectedDonorCompleteRecordSha256) ||
            donor.RuntimeControlled || donor.AnimationSource || !donor.RuntimeControlAuditComplete ||
            !donor.CompleteNativeGrammar || !donor.SourceOwnedImmutable ||
            !plan.DestinationOwnershipClosureComplete || !plan.DestinationRuntimeControlAuditComplete ||
            !plan.PrivateRecordRuntimePersistent || !plan.DonorDependencyClosureVerified ||
            !plan.PageAndClutRelocationVerified || !plan.PrivatePerFaceStorageVerified ||
            !plan.HpLpPairingVerified || !plan.CollisionAndOcclusionPreserved ||
            !plan.TargetEntryHeaderPreserved || !plan.ProtectedSubfilesPreserved ||
            !plan.RetailWadEntriesExcluded || !plan.ExecutableExcluded ||
            plan.DisposableRuntimeCandidateAuthorized || plan.PromotionAuthorized || plan.NormalCreateBinEnabled ||
            !HashEquals(plan.Composition.Append.SourceBinding.ExpectedTextureComponentSha256,
                ExpectedSourceTextureComponentSha256))
        {
            throw new InvalidDataException("The pinned static T66 donor/page/row witness changed or weakened.");
        }
    }

    private static Id65V2NativeTexturePackedRow ProjectRow(
        NativeTerrainTextureGlobalRepackPackedRecord row,
        bool synthetic)
    {
        if (row.LowDetailRow.Length != 16 || row.HighDetailRow.Length != 168 ||
            !HashEquals(Hash(row.LowDetailRow), row.LowDetailRowSha256) ||
            !HashEquals(Hash(row.HighDetailRow), row.HighDetailRowSha256))
        {
            throw new InvalidDataException($"Packed T{row.TargetTextureId} has malformed or unbound native rows.");
        }
        return new(
            row.TargetTextureId,
            row.DonorWadEntry,
            row.DonorTextureId,
            synthetic,
            row.LowDetailRow,
            row.HighDetailRow,
            row.LowDetailRowSha256,
            row.HighDetailRowSha256);
    }

    private static void ValidateProjectedRows(IReadOnlyList<Id65V2NativeTexturePackedRow> rows)
    {
        if (rows.Select(row => row.TextureId).Distinct().Count() != rows.Count ||
            rows.Any(row => row.TextureId < 0 || row.TextureId > PrivateTextureId) ||
            rows.Count(row => row.Synthetic) != 1)
        {
            throw new InvalidDataException("The T66 witness has duplicate, out-of-range, or conflicting packed rows.");
        }
        Id65V2NativeTexturePackedRow synthetic = rows.Single(row => row.Synthetic);
        if (rows.Where(row => !row.Synthetic).Any(row =>
                row.DonorWadEntry != UnusedLevel65NativeTextureAuthoringContract.TargetWadEntry ||
                row.DonorTextureId != row.TextureId))
        {
            throw new InvalidDataException(
                "A non-synthetic packed texture row lost its exact destination-owned WAD80/self-record provenance.");
        }
        if (synthetic.TextureId != PrivateTextureId || synthetic.DonorWadEntry != 70 ||
            synthetic.DonorTextureId != 12 || !HashEquals(synthetic.LowSha256, ExpectedPrivateLowRowSha256) ||
            !HashEquals(synthetic.HighSha256, ExpectedPrivateHighRowSha256))
        {
            throw new InvalidDataException("The synthetic T66 Gnasty's World T12 row identity changed.");
        }
    }

    private static void ValidateProjectedPagePatches(IReadOnlyList<Id65V2NativeTexturePagePatch> patches)
    {
        if (patches.Count != UnusedLevel65NativeTextureAuthoringContract.ExpectedTexturePagePatchCount)
            throw new InvalidDataException("The exact T66 texture-page patch count changed.");
        for (int index = 0; index < patches.Count; index++)
        {
            Id65V2NativeTexturePagePatch patch = patches[index];
            if (patch.ByteLength <= 0 || patch.RelativeOffset < 0 ||
                patch.RelativeOffset + (long)patch.ByteLength > TexturePagesByteLength ||
                patch.Owner != "terrain-texture-global-repack-data" ||
                !HashEquals(Hash(patch.CopyBefore()), patch.BeforeSha256) ||
                !HashEquals(Hash(patch.CopyAfter()), patch.AfterSha256))
            {
                throw new InvalidDataException(
                    "A projected T66 page patch lost its canonical owner or exact byte ownership.");
            }
            if (index > 0 && patches[index - 1].RelativeOffset + patches[index - 1].ByteLength > patch.RelativeOffset)
                throw new InvalidDataException("Projected T66 page patches overlap.");
        }
    }

    private static string ComputeWitnessCanonicalSha256(
        string profileId,
        string donorManifestId,
        string donorCompleteRecordSha256,
        string sourceTexturePagesSha256,
        string outputTexturePagesSha256,
        string sourceTextureComponentSha256,
        string outputTextureComponentSha256,
        IReadOnlyList<Id65V2NativeTexturePackedRow> rows,
        IReadOnlyList<Id65V2NativeTexturePagePatch> patches)
    {
        StringBuilder text = new();
        text.AppendLine(profileId).AppendLine(donorManifestId)
            .AppendLine(donorCompleteRecordSha256)
            .AppendLine(sourceTexturePagesSha256).AppendLine(outputTexturePagesSha256)
            .AppendLine(sourceTextureComponentSha256).AppendLine(outputTextureComponentSha256);
        foreach (Id65V2NativeTexturePackedRow row in rows.OrderBy(row => row.TextureId))
            text.Append(row.TextureId).Append('|').Append(row.DonorWadEntry).Append('|').Append(row.DonorTextureId)
                .Append('|').Append(row.Synthetic ? '1' : '0').Append('|').Append(row.LowSha256)
                .Append('|').Append(row.HighSha256).Append('\n');
        foreach (Id65V2NativeTexturePagePatch patch in patches)
            text.Append(patch.RelativeOffset.ToString("X8", CultureInfo.InvariantCulture)).Append('|')
                .Append(patch.ByteLength).Append('|').Append(patch.BeforeSha256).Append('|')
                .Append(patch.AfterSha256).Append('|').Append(patch.Owner).Append('\n');
        return Hash(Encoding.UTF8.GetBytes(text.ToString()));
    }

    private static void ValidateManifest(Id65AuthoringManifest manifest)
    {
        Id65AuthoringManifest expected = CreateMinimalV2T66Manifest();
        if (manifest.ProfileId != ManifestProfileId || manifest.CanonicalJson != expected.CanonicalJson ||
            !HashEquals(manifest.CanonicalSha256, ExpectedManifestSha256))
            throw new InvalidDataException("The exact minimal-v2 T66 authoring manifest changed or conflicts with another slice.");
        Id65TextureIntent[] textures = manifest.Textures.OrderBy(item => item.DestinationRecordIndex).ToArray();
        if (textures.Length != 2 || textures[0].Id != LockedTextureIntentId ||
            textures[0].Disposition != Id65AuthoringIntentDisposition.PreserveLockedBase ||
            textures[0].DestinationRecordIndex != MaterialTemplateTextureId ||
            textures[1].Id != PrivateTextureIntentId ||
            textures[1].Disposition != Id65AuthoringIntentDisposition.AuthorExact ||
            textures[1].DestinationRecordIndex != PrivateTextureId ||
            textures[1].SourceHandle != PrivateTextureSourceHandle ||
            manifest.RenderFaces.Single().TextureIntentId != PrivateTextureIntentId)
        {
            throw new InvalidDataException("The locked T25/private T66 allocator or face binding changed.");
        }
    }

    private static void ValidateV2Inputs(
        Id65CompiledAuthoringModel v2Model,
        UnusedLevel65RemoteBlankCollisionWindingRepairV2StaticPlan v2)
    {
        if (v2Model.ProfileId != Id65AuthoringModelCompiler.ProfileId ||
            !HashEquals(v2Model.ManifestSha256, Id65AuthoringModelCompiler.ExpectedMinimalManifestSha256) ||
            !HashEquals(v2Model.OutputModelSha256, ExpectedSourceModelSha256) ||
            v2.ProfileId != UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.ProfileId ||
            !HashEquals(v2.OutputModelSha256, ExpectedSourceModelSha256) ||
            !HashEquals(v2.OutputDataSha256, ExpectedSourceRow80Sha256) ||
            !HashEquals(v2.ExecutableSha256, ExpectedExecutableSha256) || v2.Scene.NewSectorIndex != SectorIndex ||
            v2.Scene.MaterialTextureId != MaterialTemplateTextureId || !v2.Scene.ExactHpLpPairing ||
            v2.Collision.OutputTriangleHex != "10011C701001380000020000" || v2.Collision.UpwardWinding ||
            v2.Repair.RepairedNormalZ != -50_176 || !v2.Repair.AcbOrderVerified ||
            !v2.Repair.CollisionTreeIdentical || !v2.Repair.CollisionBlocksIdentical ||
            !v2.Repair.CollisionAssignmentIdentical || !v2.Repair.OcclusionIdentical ||
            !v2.Repair.ProtectedDataIdentical || !v2.ProtectedSubfilesPreserved ||
            !v2.RetailWadEntriesExcluded || !v2.ExecutableExcluded ||
            v2.DisposableRuntimeCandidateAuthorized || v2.PromotionAuthorized || v2.NormalCreateBinEnabled)
        {
            throw new InvalidDataException("The exact negative-winding v2 input changed, weakened, or was replaced by positive-wound foundation bytes.");
        }
        if (HashEquals(v2Model.OutputModelSha256,
                UnusedLevel65NativeTextureAuthoringContract.ExpectedSourceModelSha256) ||
            HashEquals(v2Model.OutputModelSha256,
                UnusedLevel65NativeTextureAuthoringContract.ExpectedOutputModelSha256))
        {
            throw new InvalidDataException("Retired positive-wound foundation/native-texture model bytes are forbidden.");
        }
    }

    private static void ValidateWitness(Id65V2NativeTextureWitness witness)
    {
        if (witness.ProfileId != WitnessProfileId || witness.SourceTextureCount != SourceTextureCount ||
            witness.OutputTextureCount != OutputTextureCount || witness.PrivateTextureId != PrivateTextureId ||
            witness.DonorWadEntry != 70 || witness.DonorTextureId != 12 ||
            witness.MaterialTemplateTextureId != MaterialTemplateTextureId ||
            !HashEquals(witness.SourceTexturePagesSha256, ExpectedSourceTexturePagesSha256) ||
            !HashEquals(witness.OutputTexturePagesSha256, ExpectedOutputTexturePagesSha256) ||
            !HashEquals(witness.SourceTextureComponentSha256, ExpectedSourceTextureComponentSha256) ||
            !HashEquals(witness.OutputTextureComponentSha256, ExpectedOutputTextureComponentSha256) ||
            witness.ContainsFoundationModelBytes || witness.ContainsFoundationFaceBinding ||
            witness.ContainsAppendAfterimage || witness.ContainsPublisherState ||
            !witness.LaterSinglePassCompositeInputAvailable || witness.CrossSliceCompositionAuthorized ||
            witness.AfterimageStackingAuthorized)
        {
            throw new InvalidDataException("The typed T66 witness identity, safety boundary, or composition authority changed.");
        }
        if (witness.DonorManifestId != UnusedLevel65NativeTextureAuthoringContract.DonorManifestId ||
            !HashEquals(witness.DonorCompleteRecordSha256,
                UnusedLevel65NativeTextureAuthoringContract.ExpectedDonorCompleteRecordSha256))
        {
            throw new InvalidDataException("The typed T66 donor manifest/complete-record identity changed.");
        }
        IReadOnlyList<Id65V2NativeTexturePackedRow> rows = witness.CopyPackedRows();
        IReadOnlyList<Id65V2NativeTexturePagePatch> patches = witness.CopyPagePatches();
        ValidateProjectedRows(rows);
        ValidateProjectedPagePatches(patches);
        string recomputedCanonical = ComputeWitnessCanonicalSha256(
            witness.ProfileId,
            witness.DonorManifestId,
            witness.DonorCompleteRecordSha256,
            witness.SourceTexturePagesSha256,
            witness.OutputTexturePagesSha256,
            witness.SourceTextureComponentSha256,
            witness.OutputTextureComponentSha256,
            rows,
            patches);
        if (!HashEquals(recomputedCanonical, witness.CanonicalSha256) ||
            !HashEquals(recomputedCanonical, ExpectedWitnessSha256))
        {
            throw new InvalidDataException("The typed T66 witness canonical identity does not match its immutable contents.");
        }
    }

    private static void ValidateCapacity(Id65V2NativeTextureCompilerLimits limits)
    {
        if (limits.ModelByteCapacity < ModelByteLength)
            throw new InvalidDataException("T66 model byte capacity is insufficient.");
        if (limits.AvailableV2TailBytes < RecordGrowthBytes)
            throw new InvalidDataException("T66 v2 model tail capacity is insufficient.");
        if (limits.MaximumOutputUsedModelBytes < OutputUsedModelBytes)
            throw new InvalidDataException("T66 output used-model capacity is insufficient.");
        if (limits.MaximumNativeTextureId < PrivateTextureId || limits.MaximumNativeTextureId > 127)
            throw new InvalidDataException("T66 seven-bit texture-record capacity is insufficient or invalid.");
        if (limits.AuthorizedWitnessRecordCount != 1)
            throw new InvalidDataException("Only the exact one-record T66 page-packing witness is authorized.");
    }

    private static byte[] ApplyPagePatches(
        byte[] input,
        IReadOnlyList<Id65V2NativeTexturePagePatch> patches,
        bool reverse)
    {
        if (input.Length != TexturePagesByteLength)
            throw new InvalidDataException("The T66 texture-pages buffer has the wrong length.");
        RequireHash(Hash(input), reverse ? ExpectedOutputTexturePagesSha256 : ExpectedSourceTexturePagesSha256,
            reverse ? "T66 texture-pages afterimage" : "T66 texture-pages preimage");
        byte[] output = input.ToArray();
        foreach (Id65V2NativeTexturePagePatch patch in patches)
        {
            byte[] expected = reverse ? patch.CopyAfter() : patch.CopyBefore();
            byte[] replacement = reverse ? patch.CopyBefore() : patch.CopyAfter();
            if (!output.AsSpan(patch.RelativeOffset, expected.Length).SequenceEqual(expected))
                throw new InvalidDataException("A T66 page patch lost its exact preimage.");
            replacement.CopyTo(output, patch.RelativeOffset);
        }
        RequireHash(Hash(output), reverse ? ExpectedSourceTexturePagesSha256 : ExpectedOutputTexturePagesSha256,
            reverse ? "reversed T66 texture pages" : "composed T66 texture pages");
        return output;
    }

    private static byte[] BuildOutputTextureComponent(
        byte[] sourceModel,
        IReadOnlyList<Id65V2NativeTexturePackedRow> packedRows)
    {
        ReadOnlySpan<byte> source = sourceModel.AsSpan(0, SourceTextureComponentBytes);
        if (BinaryPrimitives.ReadInt32LittleEndian(source) != SourceTextureComponentBytes ||
            BinaryPrimitives.ReadInt32LittleEndian(source[4..]) != SourceTextureCount)
            throw new InvalidDataException("The v2 native texture table length/count changed.");
        Dictionary<int, Id65V2NativeTexturePackedRow> packed = packedRows.ToDictionary(row => row.TextureId);
        byte[] output = new byte[OutputTextureComponentBytes];
        BinaryPrimitives.WriteInt32LittleEndian(output, OutputTextureComponentBytes);
        BinaryPrimitives.WriteInt32LittleEndian(output.AsSpan(4), OutputTextureCount);
        int sourceHighStart = 8 + (SourceTextureCount * 16);
        int outputHighStart = 8 + (OutputTextureCount * 16);
        for (int textureId = 0; textureId < SourceTextureCount; textureId++)
        {
            byte[] low = packed.TryGetValue(textureId, out Id65V2NativeTexturePackedRow? row)
                ? row.CopyLow()
                : source.Slice(8 + (textureId * 16), 16).ToArray();
            byte[] high = row != null
                ? row.CopyHigh()
                : source.Slice(sourceHighStart + (textureId * 168), 168).ToArray();
            low.CopyTo(output, 8 + (textureId * 16));
            high.CopyTo(output, outputHighStart + (textureId * 168));
        }
        Id65V2NativeTexturePackedRow synthetic = packedRows.Single(row => row.Synthetic);
        synthetic.CopyLow().CopyTo(output, 8 + (PrivateTextureId * 16));
        synthetic.CopyHigh().CopyTo(output, outputHighStart + (PrivateTextureId * 168));
        if (!HashEquals(Hash(output.AsSpan(8 + (PrivateTextureId * 16), 16)), ExpectedPrivateLowRowSha256) ||
            !HashEquals(Hash(output.AsSpan(outputHighStart + (PrivateTextureId * 168), 168)),
                ExpectedPrivateHighRowSha256))
            throw new InvalidDataException("The exact private T66 LQ/HQ rows failed output readback.");
        RequireHash(Hash(output), ExpectedOutputTextureComponentSha256, "rebuilt 67-row texture component");
        return output;
    }

    private static byte[] ComposeOutputModel(byte[] sourceModel, byte[] outputTexture)
    {
        byte[] output = new byte[ModelByteLength];
        outputTexture.CopyTo(output, 0);
        sourceModel.AsSpan(SourceTextureComponentBytes, SourceUsedModelBytes - SourceTextureComponentBytes)
            .CopyTo(output.AsSpan(OutputTextureComponentBytes));
        if (output.AsSpan(OutputUsedModelBytes).IndexOfAnyExcept((byte)0) >= 0)
            throw new InvalidDataException("The T66 composed model suffix is not exact zero tail.");
        uint sourceWord = BinaryPrimitives.ReadUInt32LittleEndian(
            sourceModel.AsSpan(SourceHighTextureWordModelOffset, 4));
        uint outputWord = BinaryPrimitives.ReadUInt32LittleEndian(
            output.AsSpan(OutputHighTextureWordModelOffset, 4));
        if ((sourceWord & 0x7F) != MaterialTemplateTextureId || sourceWord != outputWord)
            throw new InvalidDataException("The sector-216 HP face is not exact source T25 before binding.");
        BinaryPrimitives.WriteUInt32LittleEndian(
            output.AsSpan(OutputHighTextureWordModelOffset, 4),
            (outputWord & 0xFFFFFF80u) | PrivateTextureId);
        return output;
    }

    private static Id65V2NativeTextureFaceBindingReadback ValidateFaceBinding(
        byte[] sourceModel,
        byte[] outputModel)
    {
        byte[] sourceLow = sourceModel.AsSpan(SourceLowFaceModelOffset, 8).ToArray();
        byte[] outputLow = outputModel.AsSpan(OutputLowFaceModelOffset, 8).ToArray();
        byte[] sourceHigh = sourceModel.AsSpan(SourceHighFaceModelOffset, 16).ToArray();
        byte[] outputHigh = outputModel.AsSpan(OutputHighFaceModelOffset, 16).ToArray();
        int[] changed = Enumerable.Range(0, 16).Where(index => sourceHigh[index] != outputHigh[index]).ToArray();
        (int sourceT25, int sourceT66, int sourceFaces) = CountHighDetailTextureUses(sourceModel);
        (int outputT25, int outputT66, int outputFaces) = CountHighDetailTextureUses(outputModel);
        if (Convert.ToHexString(sourceLow) != ExpectedLowFaceHex ||
            Convert.ToHexString(outputLow) != ExpectedLowFaceHex ||
            Convert.ToHexString(sourceHigh) != ExpectedSourceHighFaceHex ||
            Convert.ToHexString(outputHigh) != ExpectedOutputHighFaceHex ||
            !changed.SequenceEqual([8]) || sourceT25 != 63 || sourceT66 != 0 ||
            outputT25 != 62 || outputT66 != 1 || sourceFaces != 3_888 || outputFaces != 3_888)
        {
            throw new InvalidDataException("The exact sector-216 HP/LP T25-to-T66 face binding changed.");
        }
        return new(
            RenderFaceId,
            PrivateTextureIntentId,
            SectorIndex,
            LowDetailFaceIndex,
            HighDetailFaceIndex,
            SourceLowFaceModelOffset,
            OutputLowFaceModelOffset,
            SourceHighFaceModelOffset,
            OutputHighFaceModelOffset,
            SourceHighTextureWordModelOffset,
            OutputHighTextureWordModelOffset,
            ExpectedLowFaceHex,
            ExpectedLowFaceHex,
            ExpectedSourceHighFaceHex,
            ExpectedOutputHighFaceHex,
            MaterialTemplateTextureId,
            PrivateTextureId,
            sourceT25,
            outputT25,
            outputT66,
            true,
            true,
            true);
    }

    private static (int T25, int T66, int Total) CountHighDetailTextureUses(byte[] model)
    {
        int environment = BinaryPrimitives.ReadInt32LittleEndian(model);
        int sectorCount = BinaryPrimitives.ReadInt32LittleEndian(model.AsSpan(environment + 4));
        if (sectorCount != 217)
            throw new InvalidDataException("The T66 model no longer has exactly 217 sectors.");
        int t25 = 0;
        int t66 = 0;
        int total = 0;
        for (int sectorIndex = 0; sectorIndex < sectorCount; sectorIndex++)
        {
            int pointer = BinaryPrimitives.ReadInt32LittleEndian(model.AsSpan(environment + 8 + (sectorIndex * 4)));
            int sector = checked(environment + 4 + pointer);
            int lpVertices = model[sector + 16];
            int lpColors = model[sector + 17];
            int lpFaces = model[sector + 18];
            int hpVertices = model[sector + 20];
            int hpColors = model[sector + 21];
            int hpFaces = model[sector + 22];
            int hpStart = checked(sector + 28 + (lpVertices * 4) + (lpColors * 4) +
                                  (lpFaces * 8) + (hpVertices * 4) + (hpColors * 8));
            for (int face = 0; face < hpFaces; face++)
            {
                int textureId = model[hpStart + (face * 16) + 8] & 0x7F;
                if (textureId == MaterialTemplateTextureId)
                    t25++;
                if (textureId == PrivateTextureId)
                    t66++;
                total++;
            }
        }
        return (t25, t66, total);
    }

    private static Id65ModelComponentRelocation[] BuildAndValidateRelocations(
        byte[] source,
        byte[] output)
    {
        (string Name, int Source, int Output, int SourceLength, int OutputLength)[] layout =
        [
            ("texture", 0x00000, 0x00000, 0x2F78, 0x3030),
            ("environment", 0x02F78, 0x03030, 0x28518, 0x28518),
            ("occlusion", 0x2B490, 0x2B548, 0x974, 0x974),
            ("special-surface", 0x2BE04, 0x2BEBC, 0x30, 0x30),
            ("collision", 0x2BE34, 0x2BEEC, 0x5FAE8, 0x5FAE8),
            ("cyclorama", 0x8B91C, 0x8B9D4, 0x84E4, 0x84E4),
            ("portal-table", 0x93E00, 0x93EB8, 0x4, 0x4),
            ("particles", 0x93E04, 0x93EBC, 0x80, 0x80),
            ("sound", 0x93E84, 0x93F3C, 0x6F8, 0x6F8)
        ];
        Id65ModelComponentRelocation[] result = layout.Select(item =>
        {
            byte[] before = source.AsSpan(item.Source, item.SourceLength).ToArray();
            byte[] after = output.AsSpan(item.Output, item.OutputLength).ToArray();
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
        if (result.Count(item => item.ContentsPreserved) != 7 ||
            result.Single(item => item.StableId == "component.texture").ContentsPreserved ||
            result.Single(item => item.StableId == "component.environment").ContentsPreserved ||
            result.Where(item => item.StableId is "component.occlusion" or "component.collision")
                .Any(item => !item.ContentsPreserved) ||
            result[^1].OutputRelativeOffset + result[^1].OutputByteLength != OutputUsedModelBytes)
        {
            throw new InvalidDataException("The exact T66 component relocation/preservation map changed.");
        }
        RequireHash(result.Single(item => item.StableId == "component.occlusion").OutputSha256,
            "34c97013761d4ec0833745c3be420a0c416b1a7d6912070af5ce76ce00135ccd", "preserved occlusion");
        RequireHash(result.Single(item => item.StableId == "component.collision").OutputSha256,
            "5eedf23796ba8c9664b9e002cb46a6f5c9f9e37780ba5ab58d6d018ec1a147a2", "preserved collision");
        return result;
    }

    private static Id65StableHandleRebase[] BuildAndValidateHandleRebases(
        Id65CompiledAuthoringModel v2Model,
        Id65AuthoringManifest manifest)
    {
        List<Id65StableHandleRebase> result = v2Model.HandleRebases.ToList();
        result.Add(new(
            PrivateTextureIntentId,
            "texture-record",
            null,
            new("texture-record", PrivateTextureId, 0)));
        Id65StableHandleRebase[] ordered = result.OrderBy(item => item.StableId, StringComparer.Ordinal).ToArray();
        if (ordered.Length != 19 || ordered.Select(item => item.StableId).Distinct().Count() != ordered.Length ||
            ordered.Single(item => item.StableId == PrivateTextureIntentId).Output !=
                new Id65LogicalAddress("texture-record", PrivateTextureId, 0) ||
            manifest.RenderFaces.Single().TextureIntentId != PrivateTextureIntentId)
        {
            throw new InvalidDataException("The T66 stable-handle rebase map is incomplete or conflicting.");
        }
        return ordered;
    }

    private static Id65V2NativeTextureDiffRange[] BuildDiffRanges(
        byte[] sourcePages,
        byte[] outputPages,
        byte[] sourceModel,
        byte[] outputModel)
    {
        return BuildRanges("texture-pages", TexturePagesWadOffset, sourcePages, outputPages,
                "id65-private-texture-pages")
            .Concat(BuildRanges("model", ModelWadOffset, sourceModel, outputModel,
                "id65-v2-private-t66-model"))
            .OrderBy(item => item.WadOffset)
            .ToArray();
    }

    private static IEnumerable<Id65V2NativeTextureDiffRange> BuildRanges(
        string space,
        long wadOffset,
        byte[] before,
        byte[] after,
        string owner)
    {
        int cursor = 0;
        while (cursor < before.Length)
        {
            if (before[cursor] == after[cursor])
            {
                cursor++;
                continue;
            }
            int start = cursor;
            while (cursor < before.Length && before[cursor] != after[cursor])
                cursor++;
            int length = cursor - start;
            yield return new(
                space,
                start,
                wadOffset + start,
                length,
                Hash(before.AsSpan(start, length)),
                Hash(after.AsSpan(start, length)),
                owner);
        }
    }

    private static void ValidateProtectedData(
        byte[] source,
        byte[] output,
        UnusedLevel65RemoteBlankCollisionWindingRepairV2StaticPlan v2,
        IReadOnlyList<Id65ModelComponentRelocation> relocations)
    {
        if (!source.AsSpan(0, 0x800).SequenceEqual(output.AsSpan(0, 0x800)) ||
            !source.AsSpan(0x173000).SequenceEqual(output.AsSpan(0x173000)) ||
            !source.AsSpan(0xDE800 + SourceLowFaceModelOffset, 8)
                .SequenceEqual(output.AsSpan(0xDE800 + OutputLowFaceModelOffset, 8)) ||
            relocations.Where(item => item.StableId is "component.occlusion" or "component.special-surface" or
                    "component.collision" or "component.cyclorama" or "component.portal-table" or
                    "component.particles" or "component.sound")
                .Any(item => !item.ContentsPreserved) ||
            !HashEquals(v2.ExecutableSha256, ExpectedExecutableSha256))
        {
            throw new InvalidDataException("The T66 plan changed the row80 header, protected subfiles, LP face, collision/occlusion, or SCUS identity.");
        }
    }

    private static string HashDiffManifest(IEnumerable<Id65V2NativeTextureDiffRange> ranges)
    {
        StringBuilder text = new();
        foreach (Id65V2NativeTextureDiffRange item in ranges)
            text.Append(item.Space).Append('|').Append(item.RelativeOffset.ToString("X8", CultureInfo.InvariantCulture))
                .Append('|').Append(item.WadOffset.ToString("X8", CultureInfo.InvariantCulture)).Append('|')
                .Append(item.ByteLength).Append('|').Append(item.BeforeSha256).Append('|')
                .Append(item.AfterSha256).Append('|').Append(item.Owner).Append('\n');
        return Hash(Encoding.UTF8.GetBytes(text.ToString()));
    }

    private static string HashRelocations(IEnumerable<Id65ModelComponentRelocation> relocations)
    {
        StringBuilder text = new();
        foreach (Id65ModelComponentRelocation item in relocations)
            text.Append(item.StableId).Append('|').Append(item.SourceRelativeOffset).Append('|')
                .Append(item.SourceByteLength).Append('|').Append(item.OutputRelativeOffset).Append('|')
                .Append(item.OutputByteLength).Append('|').Append(item.SourceSha256).Append('|')
                .Append(item.OutputSha256).Append('|').Append(item.ContentsPreserved ? '1' : '0').Append('\n');
        return Hash(Encoding.UTF8.GetBytes(text.ToString()));
    }

    private static string HashHandleRebases(IEnumerable<Id65StableHandleRebase> rebases)
    {
        StringBuilder text = new();
        foreach (Id65StableHandleRebase item in rebases)
            text.Append(item.StableId).Append('|').Append(item.Kind).Append('|')
                .Append(FormatAddress(item.Source)).Append('|').Append(FormatAddress(item.Output)).Append('\n');
        return Hash(Encoding.UTF8.GetBytes(text.ToString()));
    }

    private static string FormatAddress(Id65LogicalAddress? value) => value.HasValue
        ? $"{value.Value.Space}:{value.Value.Primary}:{value.Value.Secondary}"
        : "none";

    private static int CountChangedBytes(ReadOnlySpan<byte> before, ReadOnlySpan<byte> after)
    {
        if (before.Length != after.Length)
            throw new InvalidDataException("Changed-byte comparison requires equal lengths.");
        int count = 0;
        for (int index = 0; index < before.Length; index++)
            if (before[index] != after[index])
                count++;
        return count;
    }

    private static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static bool HashEquals(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static void RequireHash(string actual, string expected, string label)
    {
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"The exact {label} SHA-256 changed: {actual}.");
    }
}
