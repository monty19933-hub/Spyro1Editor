using System.Text.Json;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Editing;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Source-issued detached material revision of an actual P0/schema4 scene.
/// The complete original document, geometry and ordered source instances remain
/// ancestors. This installs two static records and every authored face binding;
/// it grants no staging, Body, disc, runtime or publication authority.
/// </summary>
internal sealed class NativeId65TiledMaterialPlan
{
    private readonly byte[] _canonical, _upload, _data, _model, _scene;

    private NativeId65TiledMaterialPlan(NativeId65FreshSceneBundle fresh,
        NativeStaticTerrainTextureSource material0, NativeStaticTerrainTextureSource material1, byte[] executable)
    {
        var prepared = RequireInputs(fresh, material0, material1);
        FreshAncestor = fresh;
        SurfaceAncestor = fresh.TiledSurfaceAncestor!;
        Document = SurfaceAncestor.Document;
        MaterialSources = Array.AsReadOnly(new[] { material0, material1 });
        SourceHandles = Array.AsReadOnly(new[] { material0.Handle, material1.Handle });
        OrderedFaceMaterials = Array.AsReadOnly(prepared.OrderedFaceMaterials.ToArray());
        _canonical = prepared.Canonical.ToArray();
        CanonicalDocumentSha256 = Hash(_canonical);
        if (!executable.AsSpan().SequenceEqual(SurfaceAncestor.CopySourceExecutable()))
            throw new InvalidDataException("Tiled material consumers must retain the actual geometry's complete original executable.");
        _ = NativeId65AuthoredSurfaceConsumers.Capture(executable);
        Evidence = NativeId65FreshTerrainTexturePlan.CaptureConsumerEvidence(executable);

        _scene = fresh.CopyScene();
        _model = fresh.CopyModelPayload();
        byte[] upload = fresh.CopyUploadPayload(), data = fresh.CopyDataComponents();
        Allocation = NativeStaticTerrainTextureAllocation.Compile(upload, fresh.UploadOwners,
            [Input(material0, 0), Input(material1, 1)], NativeClip);
        _upload = Allocation.CopyUploadPayload();
        _data = NativeId65TiledMaterialDataLayout.Compose(data, Allocation.Records, OrderedFaceMaterials, NativeClip);
        if (_data.Length != data.Length + 2 * NativeStaticTerrainTextureLayout.RecordByteLength || _data.Length > 0x1000 ||
            _upload.Length != upload.Length || _model.Length != 0xF800 || _scene.Length != 0x3000)
            throw new InvalidDataException("Tiled material revision escaped the complete two-record/data/upload/model/scene allocation.");
        fresh.SceneLayout.RequireExactScene(_scene);
        if (!_model.AsSpan().SequenceEqual(fresh.CopyModelPayload()))
            throw new InvalidDataException("Tiled material composition cannot substitute the actual model owner.");
        UploadSha256 = Hash(_upload);
        DataSha256 = Hash(_data);
        ModelSha256 = Hash(_model);
        SceneSha256 = Hash(_scene);
        UpstreamPlanSha256 = fresh.PlanSha256;
        AdditionalPacketBytesPerPass = checked(12 * OrderedFaceMaterials.Count(slot => slot != 0xFF));
        PlanSha256 = Hash(JsonSerializer.SerializeToUtf8Bytes(new
        {
            ProfileId, SourceImageSha256, UpstreamPlanSha256, CanonicalDocumentSha256,
            SurfaceAncestor.BundleSha256,
            Selections = prepared.Selections.Select(s => new { s.SourceVariant, s.SourceWadEntry, s.TextureId }).ToArray(),
            OrderedFaceMaterials, SourceHandles,
            BeforeUploadSha256 = fresh.UploadSha256, BeforeDataSha256 = fresh.DataSha256,
            UploadSha256, DataSha256, ModelSha256, SceneSha256, NativeClip, AdditionalPacketBytesPerPass,
            Records = Allocation.Records.Select(r => r.RecordSha256).ToArray(),
            Allocation.Allocations, Allocation.ContainedAliases, Evidence
        }));
    }

    internal const string ProfileId = "native-id65-tiled-two-selected-static-materials-source-revision-clean-usa-v1";
    internal NativeId65FreshSceneBundle FreshAncestor { get; }
    internal NativeId65TiledSurfaceBundle SurfaceAncestor { get; }
    internal NativeFreshLevelDocumentIntent Document { get; }
    internal IReadOnlyList<NativeStaticTerrainTextureSource> MaterialSources { get; }
    internal IReadOnlyList<int> OrderedFaceMaterials { get; }
    internal NativeStaticTerrainTextureAllocation Allocation { get; }
    internal IReadOnlyList<NativeId65SurfaceConsumerEvidence> Evidence { get; }
    internal IReadOnlyList<string> SourceHandles { get; }
    internal string SourceImageSha256 => NativeId65AuthoredSurfaceConsumers.ImageSha256;
    internal string CanonicalDocumentSha256 { get; }
    internal string UpstreamPlanSha256 { get; }
    internal string PlanSha256 { get; }
    internal string UploadSha256 { get; }
    internal string DataSha256 { get; }
    internal string ModelSha256 { get; }
    internal string SceneSha256 { get; }
    internal int NativeClip => 0x8000;
    internal int AdditionalPacketBytesPerPass { get; }
    internal bool SourceCertified => true;
    internal bool DestinationAllocationProven => true;
    internal bool BodyIssued => false;
    internal bool HqRuntimeProven => false;
    internal bool CompleteLevelResourcesProven => false;
    internal bool RuntimeVerified => false;
    internal bool PublicationApproved => false;
    internal IReadOnlyList<string> PendingOwners { get; } = Array.AsReadOnly(new[]
    {
        "This is source-bound upload/data composition only; unresolved material geometry and this new revision are not old untextured staging or Body inputs.",
        "Both complete184-byte source records are retained; clip8000 direct-LQ evidence does not approve HQ/close execution or full-frame GPU demand.",
        "The12 additional packet bytes per textured face are per emitted pass, not a complete GTE/GPU/RAM peak or measured gameplay budget.",
        "Actual staging, Body, disc installation, native gameplay and publication remain separate unissued stages."
    });

    internal static async Task<NativeId65TiledMaterialPlan> CaptureAsync(string originalImagePath,
        NativeId65FreshSceneBundle fresh, NativeStaticTerrainTextureSource material0,
        NativeStaticTerrainTextureSource material1, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        _ = RequireInputs(fresh, material0, material1);
        token.ThrowIfCancellationRequested();
        byte[] executable = await NativeId65AuthoredSurfaceConsumers.ReadOriginalExecutableAsync(originalImagePath, token);
        token.ThrowIfCancellationRequested();
        var result = new NativeId65TiledMaterialPlan(fresh, material0, material1, executable);
        token.ThrowIfCancellationRequested();
        return result;
    }

    internal void RequireAncestry(NativeId65FreshSceneBundle fresh, NativeFreshLevelDocumentIntent document,
        NativeStaticTerrainTextureSource material0, NativeStaticTerrainTextureSource material1)
    {
        if (!ReferenceEquals(fresh, FreshAncestor) || !ReferenceEquals(document, Document) ||
            !ReferenceEquals(material0, MaterialSources[0]) || !ReferenceEquals(material1, MaterialSources[1]) ||
            !ReferenceEquals(fresh.TiledSurfaceAncestor, SurfaceAncestor) || !ReferenceEquals(SurfaceAncestor.Document, document))
            throw new InvalidDataException("Tiled material revision requires its actual fresh scene, whole document, surface and ordered source instances, not equal hashes or handles.");
    }

    internal byte[] CopyCanonicalDocument() => _canonical.ToArray();
    internal byte[] CopyUploadPayload() => _upload.ToArray();
    internal byte[] CopyDataComponents() => _data.ToArray();
    internal byte[] CopyModelPayload() => _model.ToArray();
    internal byte[] CopyScene() => _scene.ToArray();

    internal void RequireExactOutputs(ReadOnlySpan<byte> upload, ReadOnlySpan<byte> data,
        ReadOnlySpan<byte> model, ReadOnlySpan<byte> scene)
    {
        if (!upload.SequenceEqual(_upload) || !data.SequenceEqual(_data) ||
            !model.SequenceEqual(_model) || !scene.SequenceEqual(_scene))
            throw new InvalidDataException("Tiled material revision changed a complete upload/data/model/scene owner, face binding or protected complement.");
    }

    private static (byte[] Canonical, NativeFlatTileSurfaceSnapshot Surface,
        NativeFreshRetailTerrainMaterialIntent[] Selections, int[] OrderedFaceMaterials) RequireInputs(
        NativeId65FreshSceneBundle fresh, NativeStaticTerrainTextureSource material0, NativeStaticTerrainTextureSource material1)
    {
        ArgumentNullException.ThrowIfNull(fresh);
        ArgumentNullException.ThrowIfNull(material0);
        ArgumentNullException.ThrowIfNull(material1);
        var surface = fresh.TiledSurfaceAncestor ??
            throw new InvalidDataException("Tiled material revision requires an actual source-issued tiled scene and whole document.");
        var prepared = NativeId65TiledSurfaceBundle.PrepareGeometryForMaterials(surface.Document);
        if (!fresh.SourceCertified || !surface.SourceCertified ||
            fresh.SourceImageSha256 != NativeId65AuthoredSurfaceConsumers.ImageSha256 ||
            surface.SourceImageSha256 != fresh.SourceImageSha256 ||
            !prepared.Canonical.AsSpan().SequenceEqual(surface.CopyDocumentBytes()) ||
            Hash(prepared.Canonical) != surface.DocumentSha256)
            throw new InvalidDataException("Tiled material geometry lost its actual complete original-source document ancestry.");
        surface.RequireExactBundle(prepared.Surface.CopyBundle());
        RequireSource(material0, prepared.Selections[0]);
        RequireSource(material1, prepared.Selections[1]);

        byte[] data = fresh.CopyDataComponents(), upload = fresh.CopyUploadPayload();
        byte[] scene = fresh.CopyScene(), model = fresh.CopyModelPayload();
        var decoded = NativeId65TiledNormalDataLayout.DecodeForFixedBody(data);
        surface.RequireExactBundle(decoded.Surface.CopyBundle());
        if (data.Length + (long)(2 * NativeStaticTerrainTextureLayout.RecordByteLength) > 0x1000 ||
            upload.Length != 0xDE000 || model.Length != 0xF800 || scene.Length != 0x3000 ||
            Hash(data) != fresh.DataSha256 || Hash(upload) != fresh.UploadSha256 ||
            Hash(scene) != fresh.SceneSha256 || Hash(model) != fresh.ModelSha256 ||
            fresh.SceneLayout.PlacedCount != 0 || fresh.SceneLayout.NativeNominalCapacity != 16 ||
            fresh.SceneLayout.Spawn != new NativeId65SceneSpawn(surface.Document.SpawnX, surface.Document.SpawnY,
                surface.Document.SpawnZ, surface.Document.Heading))
            throw new InvalidDataException("Tiled material input escaped its actual P0/N16 document or complete fixed resource allocation.");
        fresh.RequireExactOutputs(scene, upload);
        NativeId65ZeroSceneAnimationLists.ValidateCanonicalBytes(scene.AsSpan(0x88, 0x44));
        fresh.ResourceChainAncestor.RequireExactPairedBytes(data, scene.AsSpan(0x88, 0x44));
        return prepared;
    }

    private static void RequireSource(NativeStaticTerrainTextureSource source, NativeFreshRetailTerrainMaterialIntent selection)
    {
        if (selection.SourceVariant != NativeFreshRetailTerrainMaterialIntent.SupportedSourceVariant ||
            !source.SourceCertified || !source.StaticRecordProven ||
            source.SourceImageSha256 != NativeId65AuthoredSurfaceConsumers.ImageSha256 ||
            source.SourceWadEntry != selection.SourceWadEntry || source.SourceTextureId != selection.TextureId)
            throw new InvalidDataException("Tiled material slot requires its exact first-occurrence original static source selection.");
        source.RequireExactSourceRecord(source.Layout.CopyRecord());
    }

    private static NativeStaticTerrainTextureInput Input(NativeStaticTerrainTextureSource source, int material) =>
        new(material, source.Handle, "source-right-half-vram:wad-" + source.SourceWadEntry,
            NativeStaticTerrainTextureAllocation.RelocationPolicyId, source.Layout.CopyRecord(), source.CopySourceVram());
    private static string Hash(ReadOnlySpan<byte> bytes) => NativeId65NormalResourceSource.Hash(bytes);
}
