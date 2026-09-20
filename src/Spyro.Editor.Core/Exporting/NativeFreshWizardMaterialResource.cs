using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.Core.Exporting;

// Private-construction selection, never an external export-policy flag.
internal enum NativeFreshMixedMaterialProfile { LegacySeparated, MaterialSeparated, SameLayoutMaterial }

/// <summary>
/// PRIVATE DRAFT: one source-owned resource composition, not a body or saved
/// schema2 compiler. The retained Host/Scene still describe their old untextured
/// bytes; no downstream native pointer/EXE/Disc factory accepts this component.
/// </summary>
internal sealed class NativeFreshWizardMaterialResource
{
    private const int GpuLength = 0x80000, UploadLength = 0xE5800;
    private const string TransferPin = "92820254671062b6b20e2cfd2c243faac32ae6223e22d50d06221b4be8539883";
    private readonly byte[] _upload, _data, _models, _scene;
    private readonly NativeId65FreshWizardUploadTransferLayout _transfer;
    private readonly NativeFreshMixedMaterialProfile _profile;

    private NativeFreshWizardMaterialResource(NativeId65FreshWizardResearchHostPlan host,
        NativeStaticTerrainTextureSource material0, NativeStaticTerrainTextureSource material1,
        NativeId65FreshWizardResearchFallbackResourceSource source, byte[] executable, NativeFreshMixedMaterialProfile profile)
    {
        RequireInputs(host, material0, material1, profile);
        _profile = profile;
        HostAncestor = host;
        DocumentAncestor = SceneAncestor.AuthoringDocumentAncestor!;
        MaterialSources = Array.AsReadOnly(new[] { material0, material1 });
        if (!ReferenceEquals(source.Resource, FallbackAncestor.ResourceAncestor))
            throw new InvalidDataException("Mixed capture requires the actual fallback's resource ancestor.");
        byte[] originalExecutable = CompilationAncestor.FreshResourceAncestor.ResourceChainAncestor.Resources.Surface.CopySourceExecutable();
        if (!executable.AsSpan().SequenceEqual(originalExecutable))
            throw new InvalidDataException("Mixed consumer capture lost its actual original executable ancestor.");
        _ = NativeId65AuthoredSurfaceConsumers.Capture(executable);
        Evidence = NativeId65FreshTerrainTexturePlan.CaptureConsumerEvidence(executable);
        NativeId65NormalUploadStagingSource.ValidateSourceExecutable(executable);

        byte[] before = FallbackAncestor.CopyUploadPayload();
        byte[] data = FallbackAncestor.CopyDataComponents();
        _models = FallbackAncestor.CopyModelPayload(); _scene = SceneAncestor.CopyScene();
        FallbackAncestor.RequireExactOutputs(before, data, _models);
        RequirePartition(FallbackAncestor.Owners, [before, data, _models]);
        if (before.Length != UploadLength || data.Length != 0x8FC || _models.Length != 0x19000 || _scene.Length != 0x3000 ||
            Hash(before) != "8c4438fc13eca3b3bd40e9c056094c308fb272aded227577fb812ec9b16d96dc" ||
            (!IsSameLayoutMaterialAuthored&&(Hash(data) != "b650a22bd06fe8d983a3422321981682e45c35c477aed176a88331034492983b" ||
            Hash(_scene) != "08718e7594702ab21e17f49c459b99cf4e35c8190258abbcaaf720106a880343")))
            throw new InvalidDataException("Only the existing exact separatedB resource/scene extents are supported in this component.");
        if(IsSameLayoutMaterialAuthored)
        {
            // Only canonical surface bytes may vary. Normalize for a complete
            // complement comparison, never as a replacement for authored data.
            NativeFreshWizardAuthoringShape.RequireSurfaceComplement(data,0x8FC,
                "bea93c58d6d568c7c6162816ddad1afff32a51886f9412a0809b22887e0b83d4");
            var b=DocumentAncestor.Base;
            var actual=NativeId65AuthoredSurfaceLayout.DecodeAndValidate(data.AsSpan(8,0x144));
            if(actual!=NativeId65AuthoredFloor.FromDisplayColors(b.FloorX,b.FloorY,b.FloorZ,
                b.FloorWidth,b.FloorDepth,b.FloorColor,b.SkyColor))
                throw new InvalidDataException("Same-layout mixed surface differs from its actual compiled authored floor.");
            SceneAncestor.RequireExactScene(_scene);
        }
        NativeId65FreshWizardResearchResourceSource.RequireInstalledGrammar(data, _models.AsSpan(0, 0x17800));
        byte[] occupied = NativeFreshWizardGpuOwnership.ReproduceFallback(source, FallbackAncestor, before, data, _models);
        var inputs = new[] { Input(material0, 0), Input(material1, 1) };
        Allocation = NativeStaticTerrainGpuAllocation.Compile(before[..GpuLength], occupied, inputs);
        _upload = before.ToArray(); Allocation.CopyGpuPayload().CopyTo(_upload, 0);
        for (int i = 0; i < _upload.Length; i++)
            if ((i >= GpuLength || occupied[i] != 0 || !Allocation.IsAllocated(i)) && _upload[i] != before[i])
                throw new InvalidDataException("Mixed allocation changed a complete existing owner, zero residue, free complement or SPU byte.");
        _data = NativeStaticTerrainGpuAllocation.ComposeTwoFaceMetadata(data, Allocation.Records);
        _transfer = NativeId65FreshWizardUploadTransferLayout.Compile(_upload);
        var originalTransfer = NativeId65FreshWizardUploadTransferLayout.Compile(before);
        originalTransfer.RequireExactTransferSource(_transfer.CopyTransferSourceImage());
        if (_transfer.TransferSourceSha256 != TransferPin)
            throw new InvalidDataException("Mixed allocation changed the actual fallback's complete928202 CPU/DMA source image.");
        UploadSha256 = Hash(_upload); DataSha256 = Hash(_data);
        PlanSha256 = Hash(JsonSerializer.SerializeToUtf8Bytes(new
        {
            Profile = IsSameLayoutMaterialAuthored ? "internal-schema2-same-layout-three-T1-pairs-mixed-resource-v1" :
                Schema2MaterialBindingProven ? "internal-schema2-separated-B-three-T1-pairs-mixed-resource-v1" :
                "internal-separated-B-three-T1-pairs-mixed-resource-v1",
            SourceImageSha256 = NativeMobySourceInventory.OriginalImageSha256,
            HostPlanSha256 = host.PlanSha256, FallbackPlanSha256 = FallbackAncestor.PlanSha256,
            ScenePlanSha256 = SceneAncestor.PlanSha256,
            AggregateSha256 = Hash(NativeFreshLevelDocumentCodec.SerializeCanonical(DocumentAncestor)),
            Sources = MaterialSources.Select(s => new { s.Handle, s.SourceDataSha256, s.SourceSceneSha256,
                s.Layout.RecordSha256, s.Evidence }).ToArray(),
            UploadSha256, DataSha256, ModelsSha256 = Hash(_models), SceneSha256 = Hash(_scene),
            Records = Allocation.Records.Select(r => r.RecordSha256).ToArray(),
            Allocation.Allocations, Allocation.ContainedAliases, Evidence,
            TransferSourceSha256, _transfer.SourceReads, _transfer.Owners,
            SourceCertified, Schema2MaterialBindingProven, BodyIssued, RuntimeVerified,
            PublicationApproved, HardwareTransfersProven
        }));
    }

    internal NativeId65FreshWizardResearchHostPlan HostAncestor { get; }
    internal NativeId65FreshWizardResearchFallbackResourcePlan FallbackAncestor => HostAncestor.FallbackAncestor;
    internal NativeId65FreshWizardResearchScenePlan SceneAncestor => HostAncestor.SceneAncestor;
    internal NativeId65FreshDocumentCompilation CompilationAncestor => SceneAncestor.CompilationAncestor;
    internal NativeFreshLevelDocumentIntent DocumentAncestor { get; }
    internal IReadOnlyList<NativeStaticTerrainTextureSource> MaterialSources { get; }
    internal IReadOnlyList<NativeId65SurfaceConsumerEvidence> Evidence { get; }
    internal NativeStaticTerrainGpuAllocation Allocation { get; }
    internal string PlanSha256 { get; }
    internal string UploadSha256 { get; }
    internal string DataSha256 { get; }
    internal string TransferSourceSha256 => _transfer.TransferSourceSha256;
    internal bool SourceCertified => true;
    internal bool Schema2MaterialBindingProven => _profile!=NativeFreshMixedMaterialProfile.LegacySeparated;
    internal bool IsSameLayoutMaterialAuthored => _profile==NativeFreshMixedMaterialProfile.SameLayoutMaterial;
    internal bool BodyIssued => false;
    internal bool RuntimeVerified => false;
    internal bool PublicationApproved => false;
    internal bool HardwareTransfersProven => false;
    internal byte[] CopyUploadPayload() => _upload.ToArray();
    internal byte[] CopyDataComponents() => _data.ToArray();
    internal byte[] CopyModelPayload() => _models.ToArray();
    internal byte[] CopyScene() => _scene.ToArray();
    internal byte[] CopyTransferSourceImage() => _transfer.CopyTransferSourceImage();

    internal void RequireAncestry(NativeId65FreshWizardResearchHostPlan host,
        NativeFreshLevelDocumentIntent document, NativeStaticTerrainTextureSource material0,
        NativeStaticTerrainTextureSource material1)
    {
        if (!ReferenceEquals(host, HostAncestor) || !ReferenceEquals(document, DocumentAncestor) ||
            !ReferenceEquals(material0, MaterialSources[0]) || !ReferenceEquals(material1, MaterialSources[1]))
            throw new InvalidDataException("Mixed resource requires its actual Host/aggregate/ordered source ancestors, not equal-looking replacements.");
        RequireInputs(host, material0, material1, _profile);
    }

    internal void RequireExactOutputs(ReadOnlySpan<byte> upload, ReadOnlySpan<byte> data,
        ReadOnlySpan<byte> models, ReadOnlySpan<byte> scene)
    {
        if (!upload.SequenceEqual(_upload) || !data.SequenceEqual(_data) ||
            !models.SequenceEqual(_models) || !scene.SequenceEqual(_scene))
            throw new InvalidDataException("Mixed complete descriptor/owner/tail/model/scene/residue complement changed.");
    }

    internal static Task<NativeFreshWizardMaterialResource> CaptureAsync(string originalBin,
        NativeId65FreshWizardResearchHostPlan host, NativeStaticTerrainTextureSource material0,
        NativeStaticTerrainTextureSource material1, CancellationToken token = default) =>
        CaptureCoreAsync(originalBin,host,material0,material1,NativeFreshMixedMaterialProfile.LegacySeparated,token);

    internal static Task<NativeFreshWizardMaterialResource> CaptureMaterialAuthoredAsync(string originalBin,
        NativeId65FreshWizardResearchHostPlan host, NativeStaticTerrainTextureSource material0,
        NativeStaticTerrainTextureSource material1, CancellationToken token = default) =>
        CaptureCoreAsync(originalBin,host,material0,material1,NativeFreshMixedMaterialProfile.MaterialSeparated,token);

    internal static Task<NativeFreshWizardMaterialResource> CaptureSameLayoutMaterialAuthoredAsync(string originalBin,
        NativeId65FreshWizardResearchHostPlan host, NativeStaticTerrainTextureSource material0,
        NativeStaticTerrainTextureSource material1, CancellationToken token = default) =>
        CaptureCoreAsync(originalBin,host,material0,material1,NativeFreshMixedMaterialProfile.SameLayoutMaterial,token);

    // One unchanged source-acquisition/finalization body, three named closed arms.
    private static async Task<NativeFreshWizardMaterialResource> CaptureCoreAsync(string originalBin,
        NativeId65FreshWizardResearchHostPlan host, NativeStaticTerrainTextureSource material0,
        NativeStaticTerrainTextureSource material1, NativeFreshMixedMaterialProfile profile, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        RequireInputs(host, material0, material1, profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(originalBin);
        string path = Path.GetFullPath(originalBin);
        FileStream? held = null; NativeFreshWizardMaterialResource? result = null;
        List<Exception> failures = [];
        try
        {
            held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            await RequireOriginal(held, token);
            var source = await NativeId65FreshWizardResearchFallbackResourceSource.CaptureAsync(path, host.FallbackAncestor.ResourceAncestor, token);
            token.ThrowIfCancellationRequested();
            DiscLayout layout = DiscImage.DetectLayout(held);
            if (layout.SectorSize != 2352 || layout.UserOffset != 24)
                throw new InvalidDataException("Mixed source requires original MODE2/2352 geometry.");
            var entry = DiscImage.FindRootFileRecord(held, layout, n => n.Equals("SCUS_942.28", StringComparison.OrdinalIgnoreCase));
            if (entry.Lba != 53875 || entry.Size != 0x66000)
                throw new InvalidDataException("Mixed original executable directory owner changed.");
            byte[] executable = DiscImage.ReadFileBytes(held, layout, entry.Lba, 0, 0x66000);
            token.ThrowIfCancellationRequested();
            result = new(host, material0, material1, source, executable, profile);
        }
        catch (Exception error) { failures.Add(error); }
        finally
        {
            // After invoking source acquisition, every acquired/current owner
            // is independently observed and every acquired close is attempted.
            // None applies even after early authentication failure or cancel.
            if (held != null) await TryAsync(() => RequireOriginal(held, CancellationToken.None));
            FileStream? current = null;
            try
            {
                current = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                await RequireOriginal(current, CancellationToken.None);
            }
            catch (Exception error) { failures.Add(error); }
            finally { if (current != null) Try(current.Dispose); }
            if (held != null) Try(held.Dispose);
        }
        NativeFreshResearchSourceOwners.ThrowFailures(failures);
        token.ThrowIfCancellationRequested();
        return result ?? throw new InvalidDataException("Mixed source capture returned no result.");
        async Task TryAsync(Func<Task> action) { try { await action(); } catch (Exception error) { failures.Add(error); } }
        void Try(Action action) { try { action(); } catch (Exception error) { failures.Add(error); } }
    }

    private static void RequireInputs(NativeId65FreshWizardResearchHostPlan host,
        NativeStaticTerrainTextureSource material0, NativeStaticTerrainTextureSource material1, NativeFreshMixedMaterialProfile profile)
    {
        ArgumentNullException.ThrowIfNull(host); ArgumentNullException.ThrowIfNull(material0); ArgumentNullException.ThrowIfNull(material1);
        if(profile is not (NativeFreshMixedMaterialProfile.LegacySeparated or NativeFreshMixedMaterialProfile.MaterialSeparated or NativeFreshMixedMaterialProfile.SameLayoutMaterial))
            throw new InvalidDataException("Unknown closed mixed material producer.");
        host.RequireAncestry(host.SceneAncestor, host.FallbackAncestor);
        var scene = host.SceneAncestor; var compilation = scene.CompilationAncestor;
        scene.RequireAncestry(compilation, scene.FamilyAncestor, scene.ResourceAncestor);
        var document = scene.AuthoringDocumentAncestor;
        if (document == null || scene.ResourceAncestor.ResourceProfile != NativeFreshWizardResourceProfile.AuthoredSameLayout)
            throw new InvalidDataException("Mixed source requires the actual authored schema1 separatedB aggregate ancestor.");
        var route = profile!=NativeFreshMixedMaterialProfile.LegacySeparated
            ? RequireMaterialInputs(scene,document,compilation,material0,material1,profile)
            : NativeFreshWizardAuthoringShape.RequireDocument(document, compilation);
        var b = document.Base;
        if ((profile!=NativeFreshMixedMaterialProfile.SameLayoutMaterial&&(
            b.FloorX != 8448 || b.FloorY != 8448 || b.FloorZ != 8704 || b.FloorWidth != 2048 || b.FloorDepth != 2048 ||
            b.SpawnX != 8960 || b.SpawnY != 8960 || b.SpawnZ != 9216 || b.Heading != 64 || b.MusicTrackId != 19 ||
            b.FloorColor != new ColorRgba(80, 128, 112) || b.SkyColor != new ColorRgba(128, 96, 64) ||
            route != new NativeWizardPlacementIntent(9984, 9984, 8704, 9472, 9984))) ||
            host.CopyAnchor != 0x8008BA38 || host.OverlayByteLength != 0x11000 ||
            host.OverlaySha256 != "ad6d952cbf70ab8b1eab53f7637ffd0235271b7b20696956c9406f36f714fa7e")
            throw new InvalidDataException("Only the existing exact separatedB Host/floor/route/music is supported in this component.");
        if (material0.SourceTextureId != 1 || material1.SourceTextureId != 1 ||
            (material0.SourceWadEntry, material1.SourceWadEntry) is not ((14, 70) or (70, 14) or (14, 14)))
            throw new InvalidDataException("Only the three reviewed T1 ordered pairs14/70,70/14,14/14 are supported in this component.");
        foreach (var source in new[] { material0, material1 })
        {
            (string data, string record) = source.SourceWadEntry == 14
                ? ("a937aecb3626ffd2311ba136684a34c9f36877054b52557b4524fcacee0e2d97", "7bf72a8271c6cd6f32dad0293c80f825fb2a7d6eccfa6a056622cf406a27d276")
                : ("b2a25147c4d81e7fd366a830546b92e6a482fe9a56f8a429a30fcb4857091a02", "2b35495f55799c05ffe003d1265f94fffc70a9f18e33f3b1814f2f70570a1da9");
            if (!source.SourceCertified || !source.StaticRecordProven || source.SourceImageSha256 != NativeMobySourceInventory.OriginalImageSha256 ||
                source.SourceDataSha256 != data || source.Layout.RecordSha256 != record)
                throw new InvalidDataException("Mixed material source does not retain the actual original T1 static issuer and complete record.");
            source.RequireExactSourceRecord(source.Layout.CopyRecord());
        }
    }

    private static NativeWizardPlacementIntent RequireMaterialInputs(NativeId65FreshWizardResearchScenePlan scene,
        NativeFreshLevelDocumentIntent document, NativeId65FreshDocumentCompilation compilation,
        NativeStaticTerrainTextureSource material0, NativeStaticTerrainTextureSource material1,NativeFreshMixedMaterialProfile profile)
    {
        bool sameLayout=profile==NativeFreshMixedMaterialProfile.SameLayoutMaterial;
        if (scene.Producer != (sameLayout?NativeFreshWizardSceneProducer.SameLayoutMaterialAuthored:NativeFreshWizardSceneProducer.MaterialAuthored))
            throw new InvalidDataException("Schema2 mixed resource requires its material-authored Scene producer.");
        var route = sameLayout?NativeFreshWizardAuthoringShape.RequireSameLayoutMaterialDocument(document, compilation):
            NativeFreshWizardAuthoringShape.RequireMaterialDocument(document, compilation);
        var first = (NativeFreshRetailTerrainMaterialIntent)document.FloorMaterials!.Triangle0;
        var second = (NativeFreshRetailTerrainMaterialIntent)document.FloorMaterials.Triangle1;
        if (!Matches(first,material0) || !Matches(second,material1) ||
            !scene.CopyMaterialCanonicalDocument().AsSpan().SequenceEqual(NativeFreshLevelDocumentCodec.SerializeCanonical(document)))
            throw new InvalidDataException("Schema2 material descriptors do not match the actual ordered source instances and full Scene aggregate.");
        return route;
        static bool Matches(NativeFreshRetailTerrainMaterialIntent descriptor,NativeStaticTerrainTextureSource source) =>
            descriptor.SourceVariant == NativeFreshRetailTerrainMaterialIntent.SupportedSourceVariant &&
            source.SourceImageSha256 == NativeMobySourceInventory.OriginalImageSha256 &&
            descriptor.SourceWadEntry == source.SourceWadEntry && descriptor.TextureId == source.SourceTextureId;
    }

    // Pure finite positive-extent/hash validation, never an issuer or free-space
    // declaration. Actual source-specific ownership is checked separately above.
    internal static void RequirePartition(IReadOnlyList<NativeFreshWizardResourceOwner> owners, byte[][] outputs)
    {
        ArgumentNullException.ThrowIfNull(owners); ArgumentNullException.ThrowIfNull(outputs);
        if (outputs.Length != 3 || outputs.Any(o => o is null || o.Length == 0) ||
            owners.Count <= 0 || owners.Count > outputs.Sum(o => (long)o.Length))
            throw new InvalidDataException("Mixed owner partition exceeds finite positive output extents.");
        int[] cursors = new int[3];
        foreach (var owner in owners)
        {
            if (owner is null || (int)owner.Domain is < 0 or > 2)
                throw new InvalidDataException("Mixed owner partition has an unknown domain.");
            int d = (int)owner.Domain;
            if (owner.Offset != cursors[d] || owner.ByteLength <= 0 || owner.ByteLength > outputs[d].Length - cursors[d] ||
                Hash(outputs[d].AsSpan(owner.Offset, owner.ByteLength)) != owner.Sha256)
                throw new InvalidDataException("Mixed owner partition has a gap, overlap, overflow or changed byte hash.");
            cursors[d] += owner.ByteLength;
        }
        if (cursors.Where((n, d) => n != outputs[d].Length).Any())
            throw new InvalidDataException("Mixed owner partition has an unowned suffix.");
    }

    private static Task RequireOriginal(Stream image, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); image.Position = 0;
        return NativeId65FreshWizardResearchResourceSource.RequireSnapshotIdentityAsync(image,
            NativeMobySourceInventory.OriginalImageByteLength, NativeMobySourceInventory.OriginalImageSha256, token);
    }
    private static NativeStaticTerrainTextureInput Input(NativeStaticTerrainTextureSource source, int material) =>
        new(material, source.Handle, "source-right-half-vram:wad-" + source.SourceWadEntry,
            NativeStaticTerrainTextureAllocation.RelocationPolicyId, source.Layout.CopyRecord(), source.CopySourceVram());
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
