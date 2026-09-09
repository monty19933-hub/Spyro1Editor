using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// PRIVATE DRAFT: one source-owned resource composition, not a body or saved
/// schema2 compiler. The retained Host/Scene still describe their old untextured
/// bytes; no downstream native pointer/EXE/Disc factory accepts this component.
/// </summary>
internal sealed class NativeFreshWizardMaterialResource
{
    private const int GpuLength = 0x80000, UploadLength = 0xE5800;
    private const int ResidueStart = 0x65800, ResidueEnd = 0x7F000;
    private const string TransferPin = "92820254671062b6b20e2cfd2c243faac32ae6223e22d50d06221b4be8539883";
    private readonly byte[] _upload, _data, _models, _scene;
    private readonly NativeId65FreshWizardUploadTransferLayout _transfer;

    private NativeFreshWizardMaterialResource(NativeId65FreshWizardResearchHostPlan host,
        NativeStaticTerrainTextureSource material0, NativeStaticTerrainTextureSource material1,
        NativeId65FreshWizardResearchFallbackResourceSource source, byte[] executable)
    {
        RequireInputs(host, material0, material1);
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
            Hash(data) != "b650a22bd06fe8d983a3422321981682e45c35c477aed176a88331034492983b" ||
            Hash(_scene) != "08718e7594702ab21e17f49c459b99cf4e35c8190258abbcaaf720106a880343")
            throw new InvalidDataException("Only the existing exact separatedB resource/scene extents are supported in this component.");
        NativeId65FreshWizardResearchResourceSource.RequireInstalledGrammar(data, _models.AsSpan(0, 0x17800));
        byte[] occupied = ReproduceFallback(source, FallbackAncestor, before, data, _models);
        // Native CPU/DMA source reads include this WHOLE same-upload residue,
        // including protected zero and the final DMA-only16 bytes. It is not free.
        occupied.AsSpan(ResidueStart, ResidueEnd - ResidueStart).Fill(1);
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
            Profile = "internal-separated-B-three-T1-pairs-mixed-resource-v1",
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
    internal bool Schema2MaterialBindingProven => false;
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
        RequireInputs(host, material0, material1);
    }

    internal void RequireExactOutputs(ReadOnlySpan<byte> upload, ReadOnlySpan<byte> data,
        ReadOnlySpan<byte> models, ReadOnlySpan<byte> scene)
    {
        if (!upload.SequenceEqual(_upload) || !data.SequenceEqual(_data) ||
            !models.SequenceEqual(_models) || !scene.SequenceEqual(_scene))
            throw new InvalidDataException("Mixed complete descriptor/owner/tail/model/scene/residue complement changed.");
    }

    internal static async Task<NativeFreshWizardMaterialResource> CaptureAsync(string originalBin,
        NativeId65FreshWizardResearchHostPlan host, NativeStaticTerrainTextureSource material0,
        NativeStaticTerrainTextureSource material1, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        RequireInputs(host, material0, material1);
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
            result = new(host, material0, material1, source, executable);
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
        NativeStaticTerrainTextureSource material0, NativeStaticTerrainTextureSource material1)
    {
        ArgumentNullException.ThrowIfNull(host); ArgumentNullException.ThrowIfNull(material0); ArgumentNullException.ThrowIfNull(material1);
        host.RequireAncestry(host.SceneAncestor, host.FallbackAncestor);
        var scene = host.SceneAncestor; var compilation = scene.CompilationAncestor;
        scene.RequireAncestry(compilation, scene.FamilyAncestor, scene.ResourceAncestor);
        var document = scene.AuthoringDocumentAncestor;
        if (document == null || scene.ResourceAncestor.ResourceProfile != NativeFreshWizardResourceProfile.AuthoredSameLayout)
            throw new InvalidDataException("Mixed source requires the actual authored schema1 separatedB aggregate ancestor.");
        var route = NativeFreshWizardAuthoringShape.RequireDocument(document, compilation);
        var b = document.Base;
        if (b.FloorX != 8448 || b.FloorY != 8448 || b.FloorZ != 8704 || b.FloorWidth != 2048 || b.FloorDepth != 2048 ||
            b.SpawnX != 8960 || b.SpawnY != 8960 || b.SpawnZ != 9216 || b.Heading != 64 || b.MusicTrackId != 19 ||
            b.FloorColor != new ColorRgba(80, 128, 112) || b.SkyColor != new ColorRgba(128, 96, 64) ||
            route != new NativeWizardPlacementIntent(9984, 9984, 8704, 9472, 9984) ||
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

    // Reconstruct only the already-issued fallback's actual bytes and ownership;
    // no caller labels, equal-content aliasing or fabricated fallback is admitted.
    private static byte[] ReproduceFallback(NativeId65FreshWizardResearchFallbackResourceSource source,
        NativeId65FreshWizardResearchFallbackResourcePlan fallback, byte[] upload, byte[] data, byte[] models)
    {
        RequirePartition(source.Resource.Owners, [source.Upload, source.Data, source.Models]);
        if (!source.TextureSourceSpans.SequenceEqual(fallback.TextureSourceSpans) ||
            !source.RetainedTextureAliases.SequenceEqual(fallback.RetainedTextureAliases) ||
            source.Mask.Length != GpuLength || source.Mask.Any(b => b > 3) ||
            !data.AsSpan().SequenceEqual(source.Data))
            throw new InvalidDataException("Mixed allocation lost complete actual fallback/source ownership.");
        byte[] expected = source.Upload.ToArray(), occupied = source.Mask.Select(b => b == 0 ? (byte)0 : (byte)1).ToArray();
        var assigned = new byte[GpuLength];
        foreach (bool alias in new[] { false, true })
        foreach (var span in alias ? source.RetainedTextureAliases : source.TextureSourceSpans)
        {
            if (span.Offset < 0 || span.ByteLength <= 0 || span.ByteLength > GpuLength - span.Offset ||
                span.SourceWadOffset != NativeId65FreshWizardResearchFallbackResourceSource.TownWad + 0x800 + span.Offset ||
                assigned.AsSpan(span.Offset, span.ByteLength).ContainsAnyExcept((byte)0) ||
                Hash(source.Town.AsSpan(0x800 + span.Offset, span.ByteLength)) != span.Sha256)
                throw new InvalidDataException("Mixed fallback source span is not its complete original byte owner.");
            for (int i = span.Offset; i < span.Offset + span.ByteLength; i++)
            {
                NativeId65FreshWizardResearchFallbackResourceSource.RequireTextureOwner(source.Mask[i], source.Upload[i], source.Town[0x800 + i], alias);
                expected[i] = source.Town[0x800 + i]; occupied[i] = 1; assigned[i] = 1;
            }
        }
        byte[] expectedModels = new byte[0x19000]; source.Models.CopyTo(expectedModels, 0);
        int cursor = source.Models.Length;
        foreach (var spec in NativeId65FreshWizardResearchFallbackResourceSource.PackageSpecs)
        { source.Packages[spec.Class].CopyTo(expectedModels, cursor); cursor += spec.Length; }
        if (cursor != 0x18AA8 || !upload.AsSpan().SequenceEqual(expected) || !models.AsSpan().SequenceEqual(expectedModels))
            throw new InvalidDataException("Mixed capture changed the full actual fallback/upload/bank/model/padding complement.");
        return occupied;
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
