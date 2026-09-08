using SourceOwners = Spyro.Editor.Core.Exporting.NativeFreshResearchSourceOwners;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Spyro.Editor.Core.Exporting;

/// <summary>One exact source-issued fixed-a/P0/N16, two-static-material/track19
/// research afterimage. It grants neither normal export nor native runtime approval.</summary>
public sealed class NativeId65FreshTexturedResearchCandidatePlan
{
    public const string ProfileId = "native-id65-fresh-textured-fixed-a-track19-research-r01";
    internal const string ImageName = "native-id65-fresh-textured-fixed-a-track19-r01.bin";
    internal const string CueName = "native-id65-fresh-textured-fixed-a-track19-r01.cue";
    internal const string ReceiptName = "native-id65-fresh-textured-fixed-a-track19-r01.receipt.json";
    internal static string[] OutputNames => [ImageName, ReceiptName, CueName];
    private const string ImageHash = "998a47930c5f20e36cf87d41bfb9ec3cab237d8d42c443ed706bd50c692611a3";
    private readonly NativeId65FreshDiscPlan _disc;
    private readonly byte[] _sourceCue, _outputCue, _lineage;
    private readonly string _imageIdentity, _cueIdentity;

    private NativeId65FreshTexturedResearchCandidatePlan(NativeId65FreshDiscPlan disc, SourceOwners owners)
    {
        _disc = disc;
        SourceImagePath = owners.ImagePath; SourceCuePath = owners.CuePath;
        _imageIdentity = owners.ImageIdentity!; _cueIdentity = owners.CueIdentity!;
        _sourceCue = owners.CueSnapshot!.ToArray();
        _outputCue = Encoding.UTF8.GetBytes("FILE \"" + ImageName + "\" BINARY\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\n");
        SourceCueSha256 = Hash(_sourceCue);
        var body = disc.BodyAncestor; var texture = body.TextureAncestor!; var fresh = texture.FreshAncestor;
        _lineage = JsonSerializer.SerializeToUtf8Bytes(new
        {
            discPlan = disc.PlanSha256, bodyPlan = body.PlanSha256,
            stagingPlan = body.StagingPlanSha256, texturePlan = texture.PlanSha256, freshPlan = fresh.PlanSha256,
            floor = fresh.ResourceChainAncestor.Resources.Surface.Floor,
            spawn = fresh.SceneLayout.Spawn, placedCount = fresh.SceneLayout.PlacedCount,
            nativeNominalCapacity = fresh.SceneLayout.NativeNominalCapacity,
            materials = texture.MaterialSources.Select((s, i) => new
            {
                slot = i, s.Handle, s.SourceImageSha256, s.SourceWadEntry, s.SourceTextureId,
                s.SourceDataSha256, sourceRecordSha256 = s.Layout.RecordSha256,
                relocatedRecordSha256 = texture.Allocation.Records[i].RecordSha256
            }).ToArray(),
            texture.NativeClip, texture.AdditionalPacketBytesPerPass,
            texture.Allocation.Allocations, texture.Allocation.ContainedAliases, texture.Evidence,
            texture.UploadSha256, texture.DataSha256, texture.ModelSha256, texture.SceneSha256,
            bodySha256 = Hash(disc.CopyBody()), overlaySha256 = Hash(disc.CopyOverlay()),
            executableSha256 = Hash(disc.CopyExecutable()), wadDirectorySha256 = Hash(disc.CopyWadDirectory()),
            isoRootSha256 = Hash(disc.CopyIsoRootDirectory()),
            disc.FinalExecutableLba, disc.FinalWadByteLength,
            upstreamPendingOwners = new { texture = texture.PendingOwners, body = body.PendingOwners }
        });
        using var lineage = JsonDocument.Parse(_lineage);
        PlanSha256 = Hash(JsonSerializer.SerializeToUtf8Bytes(new
        {
            profile = ProfileId, source = NativeId65FreshDiscSource.OriginalSha256,
            SourceImagePath, SourceCuePath, SourceCueSha256, imageIdentity = _imageIdentity, cueIdentity = _cueIdentity,
            lineage = lineage.RootElement, cueSha256 = Hash(_outputCue),
            expectedImage = ImageHash, expectedImageByteLength = ExpectedImageByteLength,
            changedRawSectors = ExpectedChangedRawSectors,
            projectSaveBindingClaimed = false, materialDocumentPersistenceBound = false,
            runtimeVerified = false, normalCreateBinApproved = false, completeNativeDependencyClosureProven = false
        }));
    }

    public string SourceImagePath { get; }
    public string SourceCuePath { get; }
    public string SourceCueSha256 { get; }
    public string PlanSha256 { get; }
    public string ExpectedImageSha256 => ImageHash;
    public long ExpectedImageByteLength => NativeId65FreshDiscSource.ImageByteLength;
    public int ExpectedChangedRawSectors => 1253;
    public bool RuntimeVerified => false;
    public bool NormalCreateBinApproved => false;
    public bool CompleteNativeDependencyClosureProven => false;
    public bool ProjectSaveBindingClaimed => false;
    public bool MaterialDocumentPersistenceBound => false;

    public static async Task<NativeId65FreshTexturedResearchCandidatePlan> CaptureAsync(string originalBin,
        string sourceCue, NativeId65FreshDiscPlan issuedDisc, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(issuedDisc);
        token.ThrowIfCancellationRequested();
        RequireRecipe(issuedDisc);
        var owners = new SourceOwners(Path.GetFullPath(originalBin), Path.GetFullPath(sourceCue));
        NativeId65FreshTexturedResearchCandidatePlan? result = null;
        Exception? primary = null;
        try
        {
            owners.Open(token);
            await NativeId65FreshDiscSource.RequireWholeOriginalAsync(owners.Bin!, token);
            owners.Bin!.Position = 0;
            RequireVerification(NativeId65FreshDiscStreaming.Measure(issuedDisc, owners.Bin, token));
            result = new(issuedDisc, owners);
            token.ThrowIfCancellationRequested();
        }
        catch (Exception error) { primary = error; }
        owners.Complete(primary);
        return result!;
    }

    internal byte[] CopyCueBytes() => _outputCue.ToArray();
    internal byte[] BuildReceipt(NativeId65FreshDiscVerification verified)
    {
        RequireVerification(verified);
        using var lineage = JsonDocument.Parse(_lineage);
        return JsonSerializer.SerializeToUtf8Bytes(new
        {
            schema = 1, profile = ProfileId, planSha256 = PlanSha256,
            SourceImagePath, SourceCuePath, SourceCueSha256,
            sourceImageSha256 = NativeId65FreshDiscSource.OriginalSha256,
            lineage = lineage.RootElement,
            imageFile = ImageName, imageSha256 = verified.OutputSha256, imageByteLength = ExpectedImageByteLength,
            changedRawSectors = verified.ChangedSectors, cueFile = CueName, cueSha256 = Hash(_outputCue),
            receiptFile = ReceiptName,
            projectSaveBindingClaimed = false, materialDocumentPersistenceBound = false,
            runtimeVerified = false, normalCreateBinApproved = false, completeNativeDependencyClosureProven = false,
            hqRuntimeProven = false, peakRuntimeMemoryProven = false, checkpointOrForeignStateSupported = false
        }, new JsonSerializerOptions { WriteIndented = true });
    }

    internal async Task RequireFreshAsync(CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        var owners = NewOwners(); Exception? primary = null;
        try
        {
            owners.Open(token);
            await NativeId65FreshDiscSource.RequireWholeOriginalAsync(owners.Bin!, token);
        }
        catch (Exception error) { primary = error; }
        owners.Complete(primary);
    }

    internal NativeId65FreshDiscVerification MeasureExpectedImage(Stream source, CancellationToken token)
    {
        var result = NativeId65FreshDiscStreaming.Measure(_disc, source, token);
        RequireVerification(result); return result;
    }

    internal NativeId65FreshDiscVerification VerifyImage(Stream source, Stream candidate, CancellationToken token)
    {
        var result = _disc.VerifyImage(source, candidate, token);
        RequireVerification(result); return result;
    }

    internal NativeId65FreshDiscVerification VerifyImage(string path, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var owners = NewOwners(); Exception? primary = null; NativeId65FreshDiscVerification? result = null;
        try
        {
            owners.Open(token);
            owners.Output = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            result = VerifyImage(owners.Bin!, owners.Output, token);
        }
        catch (Exception error) { primary = error; }
        owners.Complete(primary); return result!;
    }

    internal void WriteNewImage(string path, CancellationToken token, Action firstSector)
    {
        token.ThrowIfCancellationRequested();
        var owners = NewOwners(); Exception? primary = null;
        try
        {
            owners.Open(token);
            owners.Output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            RequireVerification(NativeId65FreshDiscStreaming.Measure(_disc, owners.Bin!, token, owners.Output, firstSector));
            owners.Output.Flush(flushToDisk: true);
            token.ThrowIfCancellationRequested();
        }
        catch (Exception error) { primary = error; }
        owners.Complete(primary);
    }

    private SourceOwners NewOwners() => new(SourceImagePath, SourceCuePath, _sourceCue, _imageIdentity, _cueIdentity);

    // Compatibility shim: old callers and failure wording remain unchanged.
    internal static void ThrowFailures(IReadOnlyList<Exception> failures) =>
        NativeFreshResearchSourceOwners.ThrowFailures(failures);

    private static void RequireRecipe(NativeId65FreshDiscPlan disc)
    {
        ArgumentNullException.ThrowIfNull(disc);
        var body = disc.BodyAncestor;
        var texture = body.TextureAncestor;
        if (body.ResourceProfile != NativeId65NormalResourceProfile.DirectLqTwoStaticMaterials || texture == null ||
            disc.BindingProfileId != "native-id65-fresh-textured-normal-disc-direct-lq-source-only-usa-v1" ||
            texture.BindingProfileId != NativeId65FreshTerrainTexturePlan.ProfileId ||
            texture.MaterialSources.Count != 2 || texture.NativeClip != 0x8000 || texture.AdditionalPacketBytesPerPass != 24)
            throw new InvalidDataException("Textured research requires its actual fixed two-material profile, not an equal length or selected binding.");
        var fresh = texture.FreshAncestor;
        var first = texture.MaterialSources[0]; var second = texture.MaterialSources[1];
        disc.RequireAncestry(body);
        texture.RequireAncestry(fresh, first, second);
        if (fresh.ResourceChainAncestor.Resources.Surface.Floor !=
                new NativeId65AuthoredFloor(8448, 8448, 8704, 1024, 1024, 0x708050, 0x406080) ||
            fresh.SceneLayout.Spawn != new NativeId65SceneSpawn(8960, 8960, 9216, 64) ||
            fresh.SceneLayout.PlacedCount != 0 || fresh.SceneLayout.NativeNominalCapacity != 16 ||
            fresh.SceneLayout.DynamicArenaByteLength != 16 * 0x70 + 0x58 ||
            first.SourceImageSha256 != NativeId65FreshDiscSource.OriginalSha256 ||
            second.SourceImageSha256 != NativeId65FreshDiscSource.OriginalSha256 ||
            first.SourceWadEntry != 14 || second.SourceWadEntry != 70 ||
            first.SourceTextureId != 0 || second.SourceTextureId != 0 ||
            first.Layout.RecordSha256 != "7b074611db43208653a931fb12c98ee64dbc5f8f41ef4edd7b1e5e796a4e0954" ||
            second.Layout.RecordSha256 != "2ffdf4bc69645c31a1cc75862288bf3620955567ef65e35b2f8f65862b7dda72")
            throw new InvalidDataException("Textured research requires actual A/P0/N16 and ordered Dark14/T0 plus Gnasty70/T0.");
        fresh.SceneLayout.RequireExactScene(texture.CopyScene());
        texture.RequireExactOutputs(texture.CopyUploadPayload(), texture.CopyDataComponents());
        if (disc.Executable.SelectedBuiltInTrack != 19 || disc.FinalExecutableLba != 54921 ||
            disc.FinalWadByteLength != 0x6B32000 || disc.CopyBody().Length != 0x1FB800 ||
            disc.PlanSha256 != "b037b67223806a8dfd6844be35a5564079e3aa462336d718c67eae6855385fcf" ||
            Hash(disc.CopyBody()) != "347ab532881afb3975088e0872792192d8567b0abe05e7708f271741b9568cdd" ||
            texture.UploadSha256 != "2ab548f540fbfadddc38d96e70a9633a1c6f37c37ee2d1a896919ac30f929333" ||
            texture.DataSha256 != "c13c21171e5ed4755f1598914eba89b0b31795b91aaeb2605efc6fed9aa17160" ||
            Hash(disc.CopyOverlay()) != "9bc923cc8d27703537b81b01f51fde351e58aabee87d38a8afc630f1031878a5" ||
            Hash(disc.CopyExecutable()) != "3473dc92e087c87858434125d5fb1ec879450682e3be64c508de6805029ce7a2" ||
            Hash(disc.CopyWadDirectory()) != "0a227cbf0d9f6b269ae7d41ce610129b0b48590519bbaaa2eac375e8818e23e0" ||
            Hash(disc.CopyIsoRootDirectory()) != "2412be680f008e5ebbf19e64948e84ad1d5eeadf07ab1c858c825c6608631089")
            throw new InvalidDataException("Textured research requires the independently known complete fixed-a physical recipe.");
        if (!texture.Allocation.Records.Select(r => r.RecordSha256).SequenceEqual(new[] {
            "06138c4791b32b848dd81cd4dbab46419d7f3bd1c6b46a6f38194c8645ba7251",
            "b57966bf69be49d581dfd73d764155de838906d5d7550ff4d1c85de7c272d100" }))
            throw new InvalidDataException("Complete fixed-a relocated material records changed.");
    }

    internal void RequireAncestry(NativeId65FreshDiscPlan disc, NativeId65NormalBodyPlan body,
        NativeId65FreshTerrainTexturePlan texture, NativeId65FreshSceneBundle fresh,
        NativeStaticTerrainTextureSource material0, NativeStaticTerrainTextureSource material1)
    {
        ArgumentNullException.ThrowIfNull(disc); ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(texture); ArgumentNullException.ThrowIfNull(fresh);
        ArgumentNullException.ThrowIfNull(material0); ArgumentNullException.ThrowIfNull(material1);
        if (!ReferenceEquals(_disc, disc) || !ReferenceEquals(body.TextureAncestor, texture))
            throw new InvalidDataException("Candidate retains its actual Disc and Texture lineage, not interchangeable equal hashes.");
        disc.RequireAncestry(body);
        texture.RequireAncestry(fresh, material0, material1);
        RequireRecipe(disc);
    }

    private static void RequireVerification(NativeId65FreshDiscVerification verified)
    {
        ArgumentNullException.ThrowIfNull(verified);
        if (verified.OutputSha256 != ImageHash || verified.ChangedSectors != 1253)
            throw new InvalidDataException("Textured research must match independent full fixed-a image/1253 sectors.");
    }
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
