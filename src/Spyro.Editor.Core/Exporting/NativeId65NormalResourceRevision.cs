using Spyro.Editor.Core.Analysis;

namespace Spyro.Editor.Core.Exporting;

/// <summary>Instance byte profile only, never complete resource or runtime approval.</summary>
public enum NativeId65NormalResourceProfile
{
    UntexturedGouraudTwoTriangles,
    DirectLqTwoStaticMaterials,
    UntexturedGouraudFlatTiles,
    DirectLqTwoStaticMaterialsFlatTiles,
    DirectLqSelectedStaticMaterialsFlatTiles
}

/// <summary>
/// Closed adapter over actual private issuers. No caller bytes, hashes,
/// enum or interface implementation can manufacture an upstream revision.
/// All downstream profiles use the same transfer and native body assemblers.
/// </summary>
internal sealed class NativeId65NormalResourceRevision
{
    private readonly byte[] _upload, _data, _model, _scene;

    private NativeId65NormalResourceRevision(NativeId65FreshSceneBundle fresh,
        NativeId65FreshTerrainTexturePlan? texture)
    {
        if (!fresh.SourceCertified || fresh.SourceImageSha256 != NativeId65EngineProfileContract.OriginalImageSha256 ||
            (texture != null && (!texture.SourceCertified || !texture.DestinationAllocationProven ||
                texture.SourceImageSha256 != fresh.SourceImageSha256 || texture.UpstreamPlanSha256 != fresh.PlanSha256 ||
                !ReferenceEquals(texture.FreshAncestor, fresh) || texture.NativeClip != 0x8000 ||
                texture.AdditionalPacketBytesPerPass != 24 || texture.SceneSha256 != fresh.SceneSha256 ||
                texture.ModelSha256 != fresh.ModelSha256)))
            throw new InvalidDataException("Normal resource revision lost its actual original-issued fresh ancestor or fixed texture profile.");

        TextureAncestor = texture;
        TiledSceneAncestor = fresh.TiledSurfaceAncestor == null ? null : fresh;
        if (texture != null && TiledSceneAncestor != null)
            throw new InvalidDataException("The legacy two-face texture issuer cannot be attached to tiled scene ancestry.");
        if (TiledSceneAncestor != null)
            RequireBaseTiledSurface(fresh);
        ResourceProfile = TiledSceneAncestor != null ? NativeId65NormalResourceProfile.UntexturedGouraudFlatTiles :
            texture == null ? NativeId65NormalResourceProfile.UntexturedGouraudTwoTriangles :
            NativeId65NormalResourceProfile.DirectLqTwoStaticMaterials;
        PlanSha256 = texture?.PlanSha256 ?? fresh.PlanSha256;
        UploadSha256 = texture?.UploadSha256 ?? fresh.UploadSha256;
        DataSha256 = texture?.DataSha256 ?? fresh.DataSha256;
        ModelSha256 = fresh.ModelSha256;
        SceneSha256 = fresh.SceneSha256;
        EntryProfile = fresh.EntryProfile;
        _upload = texture?.CopyUploadPayload() ?? fresh.CopyUploadPayload();
        _data = texture?.CopyDataComponents() ?? fresh.CopyDataComponents();
        _model = texture?.CopyModelPayload() ?? fresh.CopyModelPayload();
        _scene = texture?.CopyScene() ?? fresh.CopyScene();
        // Derive the live used end from the complete actual chain, not the
        // starter's cell sum. Keep the separately issued texture profile fixed;
        // valid untextured maximum-cell data also has lengthA38.
        DataUsedByteLength = ResourceProfile switch
        {
            NativeId65NormalResourceProfile.UntexturedGouraudTwoTriangles => NativeId65NormalDataLayout.DecodeUntextured(_data).UsedByteLength,
            NativeId65NormalResourceProfile.DirectLqTwoStaticMaterials => 0xA38,
            NativeId65NormalResourceProfile.UntexturedGouraudFlatTiles => NativeId65TiledNormalDataLayout.DecodeForFixedBody(_data).Layout.UsedByteLength,
            _ => throw new InvalidDataException("Unissued normal resource profile.")
        };
        if (_upload.Length != 0xDE000 || _data.Length != DataUsedByteLength || _model.Length != 0xF800 ||
            _scene.Length is not (0x2800 or 0x3000) ||
            NativeId65NormalResourceSource.Hash(_upload) != UploadSha256 ||
            NativeId65NormalResourceSource.Hash(_data) != DataSha256 ||
            NativeId65NormalResourceSource.Hash(_model) != ModelSha256 ||
            NativeId65NormalResourceSource.Hash(_scene) != SceneSha256)
            throw new InvalidDataException("Normal resource revision escaped its private profile's complete byte snapshots.");
        // Unchanged upstream scene/model identity is explicit even for a new
        // upload/data revision. The source issuer, not scalar metadata, owns it.
        fresh.SceneLayout.RequireExactScene(_scene);
        if (!_model.AsSpan().SequenceEqual(fresh.CopyModelPayload()))
            throw new InvalidDataException("Texture composition cannot substitute the fresh model owner.");
        if (texture == null)
            fresh.RequireExactOutputs(_scene, _upload);
        else
            texture.RequireExactOutputs(_upload, _data);

        TextureEvidence = Array.AsReadOnly(texture?.Evidence.ToArray() ?? []);
        SourceHandles = Array.AsReadOnly(texture?.SourceHandles.ToArray() ?? []);
        Evidence = Array.AsReadOnly(fresh.Evidence.Concat(TextureEvidence.Select(e =>
            new NativeId65EngineEvidence(e.Id, "EXE", e.ExecutableFileOffset, e.ByteLength, e.Sha256))).ToArray());
    }

    // A separate closed arm preserves every old constructor/profile decision.
    // Only this privately issued material plan can supply material-bearing
    // tiled data; neither a profile enum nor equal-looking byte buffers can.
    private NativeId65NormalResourceRevision(NativeId65TiledMaterialPlan material)
    {
        var fresh = material.FreshAncestor;
        if (!material.SourceCertified || !material.DestinationAllocationProven || !fresh.SourceCertified ||
            material.SourceImageSha256 != NativeId65EngineProfileContract.OriginalImageSha256 ||
            fresh.SourceImageSha256 != material.SourceImageSha256 || material.UpstreamPlanSha256 != fresh.PlanSha256 ||
            !ReferenceEquals(material.SurfaceAncestor, fresh.TiledSurfaceAncestor) ||
            !ReferenceEquals(material.Document, material.SurfaceAncestor.Document) ||
            material.CanonicalDocumentSha256 != material.SurfaceAncestor.DocumentSha256 ||
            !material.CopyCanonicalDocument().AsSpan().SequenceEqual(material.SurfaceAncestor.CopyDocumentBytes()) ||
            material.MaterialSources.Count is < 1 or > 128 ||
            material.Allocation.Records.Count != material.MaterialSources.Count ||
            material.SourceHandles.Count != material.MaterialSources.Count || material.NativeClip != 0x8000 ||
            material.AdditionalPacketBytesPerPass != 12 * material.OrderedFaceMaterials.Count(slot => slot != 0xFF))
            throw new InvalidDataException("Tiled material revision lost its actual whole-document/static-source/fresh ancestor.");
        material.RequireSelectedAncestry(fresh, material.Document, material.MaterialSources);
        _upload = material.CopyUploadPayload();
        _data = material.CopyDataComponents();
        _model = material.CopyModelPayload();
        _scene = material.CopyScene();
        // Replay the existing strict composer against the actual prior chain.
        // This proves the complete final used end without widening an old
        // untextured decoder or treating BC8/A38/<=1000 alone as byte grammar.
        byte[] composed = NativeId65TiledMaterialDataLayout.Compose(fresh.CopyDataComponents(),
            material.Allocation.Records, material.OrderedFaceMaterials, material.NativeClip);
        material.RequireExactOutputs(_upload, composed, _model, _scene);
        material.RequireExactOutputs(_upload, _data, _model, _scene);
        if (!_data.AsSpan().SequenceEqual(composed) || _data.Length > 0x1000 ||
            _upload.Length != 0xDE000 || _model.Length != 0xF800 || _scene.Length != 0x3000 ||
            NativeId65NormalResourceSource.Hash(_upload) != material.UploadSha256 ||
            NativeId65NormalResourceSource.Hash(_data) != material.DataSha256 ||
            NativeId65NormalResourceSource.Hash(_model) != material.ModelSha256 ||
            NativeId65NormalResourceSource.Hash(_scene) != material.SceneSha256 ||
            !_model.AsSpan().SequenceEqual(fresh.CopyModelPayload()) ||
            fresh.SceneLayout.NativeNominalCapacity != 16 || fresh.SceneLayout.PlacedCount != 0)
            throw new InvalidDataException("Tiled material revision escaped its complete measured P0/N16 resource allocation.");
        fresh.SceneLayout.RequireExactScene(_scene);
        TiledMaterialAncestor = material;
        TiledSceneAncestor = fresh;
        TextureAncestor = null;
        ResourceProfile = material.ResourceProfile;
        PlanSha256 = material.PlanSha256;
        UploadSha256 = material.UploadSha256;
        DataSha256 = material.DataSha256;
        ModelSha256 = material.ModelSha256;
        SceneSha256 = material.SceneSha256;
        DataUsedByteLength = composed.Length;
        EntryProfile = fresh.EntryProfile;
        TextureEvidence = Array.AsReadOnly(material.Evidence.ToArray());
        SourceHandles = Array.AsReadOnly(material.SourceHandles.ToArray());
        Evidence = Array.AsReadOnly(fresh.Evidence.Concat(TextureEvidence.Select(e =>
            new NativeId65EngineEvidence(e.Id, "EXE", e.ExecutableFileOffset, e.ByteLength, e.Sha256))).ToArray());
    }

    internal static NativeId65NormalResourceRevision From(NativeId65FreshSceneBundle fresh)
    {
        ArgumentNullException.ThrowIfNull(fresh);
        fresh.RequireLegacyResourceProfile();
        return new(fresh, null);
    }

    internal static NativeId65NormalResourceRevision From(NativeId65FreshTerrainTexturePlan texture)
    {
        ArgumentNullException.ThrowIfNull(texture);
        return new(texture.FreshAncestor, texture);
    }

    internal static NativeId65NormalResourceRevision FromTiled(NativeId65FreshSceneBundle fresh)
    {
        ArgumentNullException.ThrowIfNull(fresh);
        RequireBaseTiledSurface(fresh);
        return new(fresh, null);
    }

    internal static NativeId65NormalResourceRevision FromTiledMaterials(NativeId65TiledMaterialPlan material)
    {
        ArgumentNullException.ThrowIfNull(material);
        return new(material);
    }

    private static void RequireBaseTiledSurface(NativeId65FreshSceneBundle fresh)
    {
        var tiled = fresh.TiledSurfaceAncestor ??
            throw new InvalidDataException("Tiled revision requires its actual source-issued scene and document.");
        // Material geometry may own the same FF geometry bytes, but its actual
        // whole document still contains unresolved retail intent. It cannot
        // acquire the old untextured staging/Body profile through byte equality.
        var prepared = NativeId65TiledSurfaceBundle.PrepareDocument(tiled.Document);
        if (!prepared.Canonical.AsSpan().SequenceEqual(tiled.CopyDocumentBytes()))
            throw new InvalidDataException("Base-only tiled revision lost its complete original document ancestry.");
        tiled.RequireExactBundle(prepared.Surface.CopyBundle());
        var decoded = NativeId65TiledNormalDataLayout.DecodeForFixedBody(fresh.CopyDataComponents());
        tiled.RequireExactBundle(decoded.Surface.CopyBundle());
    }

    internal NativeId65FreshTerrainTexturePlan? TextureAncestor { get; }
    internal NativeId65FreshSceneBundle? TiledSceneAncestor { get; }
    internal NativeId65TiledMaterialPlan? TiledMaterialAncestor { get; }
    internal NativeId65NormalResourceProfile ResourceProfile { get; }
    internal string SourceImageSha256 => NativeId65EngineProfileContract.OriginalImageSha256;
    internal string PlanSha256 { get; }
    internal string UploadSha256 { get; }
    internal string DataSha256 { get; }
    internal string ModelSha256 { get; }
    internal string SceneSha256 { get; }
    internal NativeId65FreshNormalEntryProfile EntryProfile { get; }
    internal IReadOnlyList<NativeId65EngineEvidence> Evidence { get; }
    internal IReadOnlyList<NativeId65SurfaceConsumerEvidence> TextureEvidence { get; }
    internal IReadOnlyList<string> SourceHandles { get; }
    internal int DataUsedByteLength { get; }
    internal byte[] CopyUploadPayload() => _upload.ToArray();
    internal byte[] CopyDataComponents() => _data.ToArray();
    internal byte[] CopyModelPayload() => _model.ToArray();
    internal byte[] CopyScene() => _scene.ToArray();

    internal void RequireExactOutputs(ReadOnlySpan<byte> upload, ReadOnlySpan<byte> data,
        ReadOnlySpan<byte> model, ReadOnlySpan<byte> scene)
    {
        if (!upload.SequenceEqual(_upload) || !data.SequenceEqual(_data) ||
            !model.SequenceEqual(_model) || !scene.SequenceEqual(_scene))
            throw new InvalidDataException("A complete private normal resource revision or untouched domain changed.");
    }

    internal void RequireExactUpload(ReadOnlySpan<byte> upload)
    {
        if (!upload.SequenceEqual(_upload))
            throw new InvalidDataException("Normal transfer source cannot exchange its complete paired upload revision.");
    }
}
