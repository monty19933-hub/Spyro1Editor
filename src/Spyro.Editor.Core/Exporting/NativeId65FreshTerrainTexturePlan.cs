using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Exact-source detached two-material revision of an actual fresh scene bundle.
/// It cannot be passed as the old untextured staging/body revision. Complete HQ,
/// player/effect state, GPU/RAM budget, entry, runtime and publication stay open.
/// </summary>
public sealed class NativeId65FreshTerrainTexturePlan
{
    private readonly byte[] _upload, _data, _scene, _model;
    private static readonly NativeId65SurfaceConsumerEvidence[] Consumers =
    [
        new("native-default-clip8000-store", 0x34B4, 0x80012CB4, 0x14, "126c3d68a117fc390d8ff0fb09e492f75c07409a49c0f1a409eca2f2346063a4"),
        new("native-separated-LQ-HQ-metadata-loader", 0x3558, 0x80012D58, 0x50, "66e883135b85b1351d89cbd5084b7f32b22bb4628eb47344e498feae204a17e7"),
        new("native-LQ-base-and-clip-consumer", 0x16FA8, 0x800267A8, 0x10, "9bb12d9fe396e32cd2108fd3d5b0825b3fa69bd2b5dcc587b4cba7e9eb03380a"),
        new("native-direct-triangle-versus-HQ-queue-choice", 0x17A44, 0x80027244, 0xD8, "2c0428afe5796e649d8cc30d52f6b8da12ac53766e3d8a9003ea0113a15c48c5"),
        new("native-fade-t5-clip-quarter-delay-preparation", 0x17B34, 0x80027334, 0x10, "17e41b82fc988353e0a2c3a90c5a2df86c7006488b32bb4094b198d304dc809f"),
        new("native-LQ-CLUT-fade-and-GT3-versus-G3-packet", 0x17D08, 0x80027508, 0x180, "0d4409a7774fbbedb55fd4a28e35dea40bd7cab50542026495b6d14eb1a34910")
    ];

    private NativeId65FreshTerrainTexturePlan(NativeId65FreshSceneBundle fresh,
        NativeStaticTerrainTextureSource dark, NativeStaticTerrainTextureSource gnasty, byte[] executable)
    {
        FreshAncestor = fresh;
        // The shared reader already binds the complete original image/EXE. Keep
        // exact small consumer obligations visible, without claiming a full GPU.
        foreach (var span in Consumers)
            if ((long)span.ExecutableFileOffset + 0x8000F800L != span.RuntimeAddress ||
                Hash(executable.AsSpan(span.ExecutableFileOffset, span.ByteLength)) != span.Sha256)
                throw new InvalidDataException("A native static-texture clip/loader/renderer consumer changed.");
        Evidence = Array.AsReadOnly(Consumers.ToArray());
        _scene = fresh.CopyScene(); _model = fresh.CopyModelPayload();
        byte[] upload = fresh.CopyUploadPayload(), data = fresh.CopyDataComponents();
        if (Hash(_scene) != fresh.SceneSha256 || Hash(_model) != fresh.ModelSha256 ||
            Hash(upload) != fresh.UploadSha256 || Hash(data) != fresh.DataSha256)
            throw new InvalidDataException("Actual immutable fresh scene/data/model/upload revision changed.");
        NativeId65ZeroSceneAnimationLists.ValidateCanonicalBytes(_scene.AsSpan(0x88, 0x44));
        fresh.RequireExactOutputs(_scene, upload);
        SourceHandles = Array.AsReadOnly(new[] { dark.Handle, gnasty.Handle });
        var inputs = new[] { Input(dark, 0), Input(gnasty, 1) };
        Allocation = NativeStaticTerrainTextureAllocation.Compile(upload, fresh.UploadOwners, inputs, NativeClip);
        _upload = Allocation.CopyUploadPayload(); _data = Allocation.ComposeTwoFaceData(data);
        UploadSha256 = Hash(_upload); DataSha256 = Hash(_data);
        SceneSha256 = Hash(_scene); ModelSha256 = Hash(_model); UpstreamPlanSha256 = fresh.PlanSha256;
        PlanSha256 = Hash(JsonSerializer.SerializeToUtf8Bytes(new
        {
            ProfileId, SourceImageSha256, UpstreamPlanSha256, SourceHandles,
            BeforeUploadSha256 = fresh.UploadSha256, BeforeDataSha256 = fresh.DataSha256,
            UploadSha256, DataSha256, SceneSha256, ModelSha256, NativeClip, AdditionalPacketBytesPerPass,
            Records = Allocation.Records.Select(r => r.RecordSha256).ToArray(),
            Allocation.Allocations, Allocation.ContainedAliases, Evidence
        }));
    }

    public const string ProfileId = "native-id65-fresh-two-static-materials-dark14-gnastyworld70-clean-usa-v1";
    // The exact private ancestor is retained for closed downstream composition;
    // it is never reconstructed from caller-visible hashes or profile labels.
    internal NativeId65FreshSceneBundle FreshAncestor { get; }
    public string SourceImageSha256 => NativeId65AuthoredSurfaceConsumers.ImageSha256;
    public string UpstreamPlanSha256 { get; }
    public string PlanSha256 { get; }
    public string UploadSha256 { get; }
    public string DataSha256 { get; }
    public string SceneSha256 { get; }
    public string ModelSha256 { get; }
    public IReadOnlyList<string> SourceHandles { get; }
    public IReadOnlyList<NativeId65SurfaceConsumerEvidence> Evidence { get; }
    public NativeStaticTerrainTextureAllocation Allocation { get; }
    public int NativeClip => 0x8000;
    public int AdditionalPacketBytesPerPass => 24;
    public bool SourceCertified => true;
    public bool DestinationAllocationProven => true;
    public bool HqRuntimeProven => false;
    public bool CompleteLevelResourcesProven => false;
    public bool RuntimeVerified => false;
    public bool PublicationApproved => false;
    public IReadOnlyList<string> PendingOwners { get; } = Array.AsReadOnly(new[]
    {
        "This is detached upload/data composition only: the old untextured staging/body cannot be paired with these new bytes.",
        "Full184-byte resources are retained; only the fixed direct-LQ two-triangle and exact native clip8000 path has bounded source evidence. HQ/close execution remains unapproved.",
        "The24 extra packet bytes are per emitted pass, not a full-frame GTE/GPU/RAM peak or runtime resource-demand bound.",
        "Player/particle/callback/entry/persistence/ID65 linkage, actual body installation, gameplay and publication remain separate unapproved work."
    });

    public static async Task<NativeId65FreshTerrainTexturePlan> CaptureAsync(string originalImagePath,
        NativeId65FreshSceneBundle fresh, NativeStaticTerrainTextureSource dark,
        NativeStaticTerrainTextureSource gnasty, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fresh); ArgumentNullException.ThrowIfNull(dark); ArgumentNullException.ThrowIfNull(gnasty);
        cancellationToken.ThrowIfCancellationRequested();
        if (!fresh.SourceCertified || fresh.SourceImageSha256 != NativeId65AuthoredSurfaceConsumers.ImageSha256)
            throw new InvalidDataException("Two-material composition requires an actual source-issued fresh bundle.");
        RequireSource(dark, 14, "a937aecb3626ffd2311ba136684a34c9f36877054b52557b4524fcacee0e2d97",
            "7b074611db43208653a931fb12c98ee64dbc5f8f41ef4edd7b1e5e796a4e0954");
        RequireSource(gnasty, 70, "b2a25147c4d81e7fd366a830546b92e6a482fe9a56f8a429a30fcb4857091a02",
            "2ffdf4bc69645c31a1cc75862288bf3620955567ef65e35b2f8f65862b7dda72");
        byte[] executable = await NativeId65AuthoredSurfaceConsumers.ReadOriginalExecutableAsync(originalImagePath, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var result = new NativeId65FreshTerrainTexturePlan(fresh, dark, gnasty, executable);
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }
    public byte[] CopyUploadPayload() => _upload.ToArray();
    public byte[] CopyDataComponents() => _data.ToArray();
    public byte[] CopyScene() => _scene.ToArray();
    public byte[] CopyModelPayload() => _model.ToArray();
    public void RequireExactOutputs(ReadOnlySpan<byte> upload, ReadOnlySpan<byte> data)
    {
        if (!upload.SequenceEqual(_upload) || !data.SequenceEqual(_data))
            throw new InvalidDataException("An allocated resource, descriptor, shifted tail or whole protected/free/SPU complement changed.");
    }
    private static void RequireSource(NativeStaticTerrainTextureSource source, int entry, string dataHash, string recordHash)
    {
        if (!source.SourceCertified || !source.StaticRecordProven || source.SourceImageSha256 != NativeId65AuthoredSurfaceConsumers.ImageSha256 ||
            source.SourceWadEntry != entry || source.SourceTextureId != 0 || source.SourceDataSha256 != dataHash ||
            source.Layout.RecordSha256 != recordHash)
            throw new InvalidDataException("The code-owned Dark14/T0 and Gnasty's World70/T0 profile cannot substitute another source variant.");
        source.RequireExactSourceRecord(source.Layout.CopyRecord());
    }
    private static NativeStaticTerrainTextureInput Input(NativeStaticTerrainTextureSource source, int material) =>
        new(material, source.Handle, "source-right-half-vram:wad-" + source.SourceWadEntry,
            NativeStaticTerrainTextureAllocation.RelocationPolicyId, source.Layout.CopyRecord(), source.CopySourceVram());
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
