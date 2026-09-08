using Spyro.Editor.Core.Analysis;

namespace Spyro.Editor.Core.Exporting;

/// <summary>Instance byte profile only, never complete resource or runtime approval.</summary>
public enum NativeId65NormalResourceProfile
{
    UntexturedGouraudTwoTriangles,
    DirectLqTwoStaticMaterials
}

/// <summary>
/// Closed adapter over two actual private issuers. No caller bytes, hashes,
/// enum or interface implementation can manufacture an upstream revision.
/// Both downstream profiles use the same transfer and native body assemblers.
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
        ResourceProfile = texture == null ? NativeId65NormalResourceProfile.UntexturedGouraudTwoTriangles
            : NativeId65NormalResourceProfile.DirectLqTwoStaticMaterials;
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
        DataUsedByteLength = texture == null
            ? NativeId65NormalDataLayout.DecodeUntextured(_data).UsedByteLength
            : 0xA38;
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

    internal static NativeId65NormalResourceRevision From(NativeId65FreshSceneBundle fresh)
    {
        ArgumentNullException.ThrowIfNull(fresh);
        return new(fresh, null);
    }

    internal static NativeId65NormalResourceRevision From(NativeId65FreshTerrainTexturePlan texture)
    {
        ArgumentNullException.ThrowIfNull(texture);
        return new(texture.FreshAncestor, texture);
    }

    internal NativeId65FreshTerrainTexturePlan? TextureAncestor { get; }
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
