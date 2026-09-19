using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using Spyro.Editor.Core.Analysis;

[assembly: InternalsVisibleTo("Spyro.Editor.NativeId65NormalResourceSmoke")]

namespace Spyro.Editor.Core.Exporting;

public enum NativeId65ModelRootKind { SpecialPlayerStream, GenericClass }
public enum NativeId65ResourceDomain { UploadPayload, DataComponentTail, ModelPayload }
public sealed record NativeId65ModelRootOwner(NativeId65ModelRootKind Kind, ushort ClassId,
    int ModelRelativeOffset, int PayloadByteLength, int RegistrationAllocationByteLength,
    string PayloadSha256, string AllocationSha256);
public sealed record NativeId65NormalResourceOwner(string Id, NativeId65ResourceDomain Domain,
    int Offset, int ByteLength, string Sha256, string Provenance, long? SourceWadOffset);
public sealed record NativeId65NormalSoundLink(int ModelRelativeFieldOffset, byte DetachedIndex,
    byte RetainedDescriptorIndex, int DataTailDescriptorOffset, uint SampleBankRelativeOffset,
    string DescriptorSha256, string SampleSha256);

/// <summary>
/// Actual fresh upload/model bytes and an environment-through-sound component
/// tail. No terrain metadata is invented: the complete data-chain accessor
/// refuses until the separate scene animation/scroll owner is source-issued.
/// No level header, scene, overlay, inventory, RAM approval or disc plan exists.
/// </summary>
public sealed class NativeId65NormalResourceBundle
{
    private readonly byte[] _upload, _tail, _model;
    private readonly byte[] _particleComponent;
    private NativeId65NormalResourceBundle(NativeId65NormalResourceSource source,
        NativeId65SurfaceRevision surface, NativeId65CompanionAssetPlan companion)
    {
        RequireInputs(surface, companion);
        byte[] data = source.CopyData(), player = source.CopyPlayerPackage();
        byte[] executable = source.CopyExecutable();
        if (!surface.CopySourceExecutable().AsSpan().SequenceEqual(executable))
            throw new InvalidDataException("Surface belongs to another executable/consumer capture.");
        companion.EngineProfile.RequireCapturedDomains(executable, source.CopyOverlay(), data);
        NativeId65CompanionAssetBlock block = companion.CompileAssetBlock();
        var recompiled = NativeId65CompanionAssetLayout.CompileExactTown(companion.EngineProfile);
        block.RequireExactBlock(recompiled.CopyAssetBlock());
        SurfaceRevision = surface;
        CompanionPlanSha256 = companion.PlanSha256;
        Evidence = source.Evidence;
        PlayerTextures = NativeId65PlayerTextureOwners.Capture(player, data.AsSpan(0x173000, 0xCC30));
        var vram = new NativeId65VramAssembly(data.AsSpan(0x800, 0x80000));
        foreach (var reference in PlayerTextures.References)
            CopyFootprint(reference.Footprint);

        _particleComponent = data.AsSpan(0x172590, 0x80).ToArray();
        var particles = NativeParticleDescriptorCodec.Decode(_particleComponent);
        int[] particleIds = [0x15, 0x18, 0x02, 0x21, 0x0C, 0x0B, 0x0A, 0x09, 0x01, 0x00];
        if (!particles.Ids.SequenceEqual(particleIds))
            throw new InvalidDataException("Retained native particle table differs from its exact ten-record baseline.");
        ParticleDescriptorIds = Array.AsReadOnly(particleIds);
        for (int i = 0; i < particles.Count; i++)
        {
            // Each native frame is eight bytes; expand the two opposite UV
            // corners into the same neutral rectangular footprint grammar.
            ReadOnlySpan<byte> frame = _particleComponent.AsSpan(12 + i * 12, 8);
            byte[] descriptor = new byte[12];
            frame.CopyTo(descriptor);
            descriptor[8] = frame[0]; descriptor[9] = frame[5];
            descriptor[10] = frame[4]; descriptor[11] = frame[1];
            CopyFootprint(NativeId65PlayerTextureOwners.DecodeFootprint(descriptor));
        }
        foreach (var placement in block.VramPlacements)
        {
            byte[] bytes = placement.PayloadOwnerId switch
            {
                "pixels" => block.CopyPixelPayload(),
                "clut" => block.CopyPalettePayload(),
                _ => throw new InvalidDataException("Unexpected detached companion VRAM owner.")
            };
            if (placement.PayloadRelativeOffset < 0 || placement.ByteLength <= 0 ||
                placement.PayloadRelativeOffset + (long)placement.ByteLength > bytes.Length)
                throw new InvalidDataException("Detached companion VRAM reference exceeds its source-issued owner.");
            vram.CopyDetachedRange(placement.PayloadOwnerId, placement.VramRelativeOffset,
                bytes.AsSpan(placement.PayloadRelativeOffset, placement.ByteLength));
        }
        SourceVramOwnedByteCount = vram.SourceOwnedByteCount;
        var sound = NativeSoundBankCodec.Decode(data.AsSpan(0x172610, 0x6F8), data.AsSpan(0x80800, 0x5E000));
        if (sound.Descriptors.Count != 76 || sound.BankByteLength != 0x5E000)
            throw new InvalidDataException("Retained normal audio baseline changed.");
        _upload = new byte[0xDE000];
        vram.CopyBytes().CopyTo(_upload, 0);
        sound.CopyBank().CopyTo(_upload, 0x80000);

        byte[] surfaces = surface.CopyBundle();
        _tail = new byte[surfaces.Length + 4 + _particleComponent.Length + sound.ComponentByteLength];
        surfaces.CopyTo(_tail, 0);
        // Native portal storage is a raw count, not a length/count component.
        _particleComponent.CopyTo(_tail, surfaces.Length + 4);
        int soundOffset = surfaces.Length + 4 + _particleComponent.Length;
        sound.CopyComponent().CopyTo(_tail, soundOffset);

        byte[] package = block.CopyRelocatedPackage();
        byte[] originalDetached = package.ToArray();
        byte[] detachedDescriptors = block.CopyLocalDescriptors(), detachedSamples = block.CopySampleBank();
        var links = new List<NativeId65NormalSoundLink>();
        int sampleCursor = 0;
        for (int i = 0; i < 2; i++)
        {
            NativeSoundDescriptor retained = sound.Descriptors[72 + i];
            byte[] exact = retained.CopyDescriptor();
            byte[] detached = detachedDescriptors.AsSpan(i * 20, 20).ToArray();
            if (package[4 + i] != i || BinaryPrimitives.ReadUInt32LittleEndian(detached) != sampleCursor ||
                !detached.AsSpan(4).SequenceEqual(exact.AsSpan(4)) ||
                !detachedSamples.AsSpan(sampleCursor, retained.Sample.ByteLength).SequenceEqual(retained.Sample.CopyBytes()))
                throw new InvalidDataException("Companion local slots/descriptors/samples do not match retained native72/73 owners.");
            sampleCursor += retained.Sample.ByteLength;
            package[4 + i] = (byte)(72 + i);
            links.Add(new(0xCC30 + 4 + i, (byte)i, (byte)(72 + i), soundOffset + 0x108 + (72 + i) * 20,
                retained.SampleOffset, retained.Sha256, retained.Sample.Sha256));
        }
        byte[] inverse = package.ToArray(); inverse[4] = 0; inverse[5] = 1;
        if (sampleCursor != detachedSamples.Length || !inverse.AsSpan().SequenceEqual(originalDetached))
            throw new InvalidDataException("Sound integration changed an undeclared package byte or lost a sample owner.");
        _model = new byte[0xF800];
        data.AsSpan(0x173000, 0xCC30).CopyTo(_model);
        package.CopyTo(_model, 0xCC30);
        SoundLinks = links.AsReadOnly();
        ModelRootOwners = Array.AsReadOnly(new[]
        {
            new NativeId65ModelRootOwner(NativeId65ModelRootKind.SpecialPlayerStream, 0, 0, 0xCC30, 0xCC30,
                Hash(_model.AsSpan(0, 0xCC30)), Hash(_model.AsSpan(0, 0xCC30))),
            new NativeId65ModelRootOwner(NativeId65ModelRootKind.GenericClass, 0x78, 0xCC30, 0x2930, 0x2BD0,
                Hash(package), Hash(_model.AsSpan(0xCC30)))
        });
        var owners = new List<NativeId65NormalResourceOwner>();
        byte[] ownership = vram.CopyOwnership();
        for (int start = 0; start < ownership.Length;)
        {
            int end = start + 1;
            while (end < ownership.Length && ownership[end] == ownership[start]) end++;
            string provenance = ownership[start] switch { 0 => "fresh-zero-upload-background", 1 => "retained-fixed-Town-VRAM", _ => "issued-detached-companion-VRAM" };
            Add("vram-" + start.ToString("X"), NativeId65ResourceDomain.UploadPayload, start, end - start,
                provenance, ownership[start] == 1 ? 0x119E800L + start : null);
            start = end;
        }
        Add("retained-complete-Town-SPU-bank", NativeId65ResourceDomain.UploadPayload, 0x80000, 0x5E000, "explicit-retained-audio-baseline", 0x121E800);
        foreach (var component in surface.Components)
            Add("fresh-" + component.Kind, NativeId65ResourceDomain.DataComponentTail, component.BlockOffset, component.ByteLength, surface.BundleSha256, null);
        Add("raw-zero-portals", NativeId65ResourceDomain.DataComponentTail, surfaces.Length, 4, "fresh-count-only-owner", null);
        Add("retained-particle-descriptors", NativeId65ResourceDomain.DataComponentTail, surfaces.Length + 4, 0x80, "ten-source-frame0-assets-not-callback-closure", 0x1310590);
        Add("retained-complete-sound-component", NativeId65ResourceDomain.DataComponentTail, soundOffset, 0x6F8, "explicit-retained-audio-baseline", 0x1310610);
        Add("special-player-root0", NativeId65ResourceDomain.ModelPayload, 0, 0xCC30, "retained-Town-player-stream", 0x1311000);
        Add("generic-companion78", NativeId65ResourceDomain.ModelPayload, 0xCC30, 0x2930, companion.PlanSha256 + ";local-slot-link72/73", null);
        Add("new-sector-padding", NativeId65ResourceDomain.ModelPayload, 0xF560, 0x2A0, "fresh-explicit-registration-tail", null);
        Owners = owners.AsReadOnly();
        UploadSha256 = Hash(_upload); DataTailSha256 = Hash(_tail); ModelSha256 = Hash(_model);
        VerifyPartition();

        void CopyFootprint(NativeId65TextureFootprint footprint)
        {
            foreach (var row in footprint.PixelRows) vram.CopySourceRange(row.VramRelativeOffset, row.ByteLength);
            vram.CopySourceRange(footprint.Palette.VramRelativeOffset, footprint.Palette.ByteLength);
        }
        void Add(string id, NativeId65ResourceDomain domain, int offset, int length, string provenance, long? wadOffset)
        {
            byte[] bytes = domain switch { NativeId65ResourceDomain.UploadPayload => _upload,
                NativeId65ResourceDomain.DataComponentTail => _tail, _ => _model };
            owners.Add(new(id, domain, offset, length, Hash(bytes.AsSpan(offset, length)), provenance, wadOffset));
        }
    }

    public const string ProfileId = "native-id65-normal-detached-resource-assets-clean-usa-v1";
    public string SourceImageSha256 => NativeId65EngineProfileContract.OriginalImageSha256;
    public string SharedPlayerPackageSha256 => NativeId65NormalResourceSource.PlayerPackageHash;
    public long SharedPlayerPackageWadOffset => 0x7B9000;
    public int SharedPlayerPackageByteLength => 0x32E1C;
    public NativeId65AuthoredSurfaceBundle Surface => SurfaceRevision.LegacySurface;
    internal NativeId65SurfaceRevision SurfaceRevision { get; }
    public string CompanionPlanSha256 { get; }
    public NativeId65PlayerTextureOwners PlayerTextures { get; }
    public IReadOnlyList<NativeId65EngineEvidence> Evidence { get; }
    public IReadOnlyList<NativeId65ModelRootOwner> ModelRootOwners { get; }
    public IReadOnlyList<NativeId65NormalResourceOwner> Owners { get; }
    public IReadOnlyList<NativeId65NormalSoundLink> SoundLinks { get; }
    public IReadOnlyList<int> ParticleDescriptorIds { get; }
    public int SourceVramOwnedByteCount { get; }
    public string UploadSha256 { get; }
    public string DataTailSha256 { get; }
    public string ModelSha256 { get; }
    public bool SourceCertified => true;
    public bool CompleteDataChainAvailable => false;
    public bool CompleteLevelResourcesProven => false;
    public bool IndependentLevelSupported => false;
    public bool PublicationApproved => false;
    public bool RuntimeVerified => false;
    public bool CompletePlayerParticleClosureProven => false;
    public IReadOnlyList<string> PendingOwners { get; } = Array.AsReadOnly(new[]
    {
        "Source-issued scene texture-animation/scroll lists and speculative guard reads; no empty terrain metadata is emitted.",
        "Remaining native player particle spawn/tick frame schemas; only companion selector0C frame0 is bound here.",
        "Shared resident PETE fixed-address geometry, decoded player roots, native particle-pool/PETE sentinel overlap and renderer state remain external owners.",
        "Fresh scene/arenas, callbacks, overlay, full player states, mode/rescue/transition closure, authored RAM, native header/ID65 linkage and publication."
    });
    public byte[] CopyUploadPayload() => _upload.ToArray();
    public byte[] CopyDataComponentTail() => _tail.ToArray();
    public byte[] CopyModelPayload() => _model.ToArray();
    public byte[] CopyDataComponents() => throw new InvalidOperationException(
        "Complete data chain refused: no source-issued scene texture-animation/scroll-list owner and guard-read certificate.");
    public byte[] CopyParticleFrame(int descriptorId, int frameIndex)
    {
        int index = ParticleDescriptorIds.ToList().IndexOf(descriptorId);
        if (index < 0 || frameIndex != 0)
            throw new InvalidDataException("Retained particle descriptor owns only one eight-byte frame, not an arbitrary runtime frame index.");
        return _particleComponent.AsSpan(12 + index * 12, 8).ToArray();
    }
    public void RequireExactPayloads(ReadOnlySpan<byte> upload, ReadOnlySpan<byte> dataTail, ReadOnlySpan<byte> model)
    {
        if (!upload.SequenceEqual(_upload) || !dataTail.SequenceEqual(_tail) || !model.SequenceEqual(_model))
            throw new InvalidDataException("Normal resource byte owner or complete untouched/fresh complement changed.");
    }
    internal static void RequireInputs(NativeId65AuthoredSurfaceBundle surface, NativeId65CompanionAssetPlan companion)
        => RequireInputs(NativeId65SurfaceRevision.From(surface), companion);

    private static void RequireInputs(NativeId65SurfaceRevision surface, NativeId65CompanionAssetPlan companion)
    {
        ArgumentNullException.ThrowIfNull(surface); ArgumentNullException.ThrowIfNull(companion);
        if (!surface.SourceCertified || surface.SourceImageSha256 != companion.SourceImageSha256 ||
            !companion.CompileAssetBlock().SourceCertified)
            throw new InvalidDataException("Normal resources require source-issued surface and companion assets, not caller metadata.");
    }
    public static async Task<NativeId65NormalResourceBundle> CaptureAsync(string originalImagePath,
        NativeId65AuthoredSurfaceBundle surface, NativeId65CompanionAssetPlan companion, CancellationToken cancellationToken = default)
    {
        var revision = NativeId65SurfaceRevision.From(surface);
        RequireInputs(revision, companion);
        var source = await NativeId65NormalResourceSource.CaptureAsync(originalImagePath, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return new(source, revision, companion);
    }

    internal static async Task<NativeId65NormalResourceBundle> CaptureTiledAsync(string originalImagePath,
        NativeId65TiledSurfaceBundle surface, NativeId65CompanionAssetPlan companion, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var revision = NativeId65SurfaceRevision.From(surface);
        RequireInputs(revision, companion);
        var source = await NativeId65NormalResourceSource.CaptureAsync(originalImagePath, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return new(source, revision, companion);
    }
    private void VerifyPartition()
    {
        foreach (var (domain, length) in new[] { (NativeId65ResourceDomain.UploadPayload, _upload.Length),
            (NativeId65ResourceDomain.DataComponentTail, _tail.Length), (NativeId65ResourceDomain.ModelPayload, _model.Length) })
        {
            int cursor = 0;
            foreach (var owner in Owners.Where(o => o.Domain == domain).OrderBy(o => o.Offset))
            {
                if (owner.Offset != cursor || owner.ByteLength <= 0)
                    throw new InvalidDataException("Normal resource ownership contains a hole or overlapping storage.");
                cursor = checked(cursor + owner.ByteLength);
            }
            if (cursor != length) throw new InvalidDataException("Normal resource ownership does not cover its complete payload.");
        }
    }
    private static string Hash(ReadOnlySpan<byte> bytes) => NativeId65NormalResourceSource.Hash(bytes);
}
