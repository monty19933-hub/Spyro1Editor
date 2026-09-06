using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;

[assembly: InternalsVisibleTo("Spyro.Editor.NativeId65CompanionAssetSmoke")]

namespace Spyro.Editor.Core.Exporting;

public enum NativeId65CompanionReferenceKind
{
    LocalDescriptorIndex,
    SampleBankRelativeOffset,
    VramTexturePage,
    VramPalette
}

public sealed record NativeId65CompanionAssetOwner(string Id, int BlockOffset, int ByteLength, string Sha256);
public sealed record NativeId65CompanionAssetReference(string FieldOwnerId, int FieldRelativeOffset,
    int ByteWidth, NativeId65CompanionReferenceKind Kind, string TargetOwnerId, int TargetRelativeOffset,
    uint EncodedValue);
public sealed record NativeId65CompanionVramPlacement(string PayloadOwnerId, int PayloadRelativeOffset,
    int VramRelativeOffset, int ByteLength);
public sealed record NativeId65CompanionSoundSource(int OriginalDescriptorIndex, int SampleByteLength,
    string DescriptorSha256, string SampleSha256, long? SampleWadOffset);

/// <summary>
/// Compact detached native fragments, not a complete native level/subfile. VRAM
/// placements and local descriptor indices belong only to this new asset block's
/// namespace. Installing it into a level requires separate ownership/composition.
/// </summary>
public sealed class NativeId65CompanionAssetBlock
{
    private readonly byte[] _bytes;

    internal NativeId65CompanionAssetBlock(byte[] bytes, IEnumerable<NativeId65CompanionAssetOwner> owners,
        IEnumerable<NativeId65CompanionAssetReference> references,
        IEnumerable<NativeId65CompanionVramPlacement> placements,
        IEnumerable<NativeId65CompanionSoundSource> sounds, bool sourceCertified)
    {
        _bytes = bytes.ToArray();
        Owners = Array.AsReadOnly(owners.ToArray());
        References = Array.AsReadOnly(references.ToArray());
        VramPlacements = Array.AsReadOnly(placements.ToArray());
        SoundSources = Array.AsReadOnly(sounds.ToArray());
        SourceCertified = sourceCertified;
        BlockSha256 = NativeSoundBankCodec.Hash(bytes);
    }

    public bool SourceCertified { get; }
    public string BlockSha256 { get; }
    public IReadOnlyList<NativeId65CompanionAssetOwner> Owners { get; }
    public IReadOnlyList<NativeId65CompanionAssetReference> References { get; }
    public IReadOnlyList<NativeId65CompanionVramPlacement> VramPlacements { get; }
    public IReadOnlyList<NativeId65CompanionSoundSource> SoundSources { get; }
    public int LocalSoundDescriptorCount => 2;
    public bool CompleteCompanionClosureProven => false;
    public bool CompleteLevelVramProven => false;
    public bool PublicationApproved => false;
    public byte[] CopyAssetBlock() => _bytes.ToArray();
    public byte[] CopyRelocatedPackage() => Copy("model");
    public byte[] CopyPixelPayload() => Copy("pixels");
    public byte[] CopyPalettePayload() => Copy("clut");
    public byte[] CopyLocalDescriptors() => Copy("sound-descriptors");
    public byte[] CopySampleBank() => Copy("sample-bank");

    public void RequireExactBlock(ReadOnlySpan<byte> bytes)
    {
        if (!bytes.SequenceEqual(_bytes))
            throw new InvalidDataException("Detached companion asset block changed or has extra/unowned bytes.");
    }

    private byte[] Copy(string id)
    {
        var owner = Owners.Single(o => o.Id == id);
        return _bytes.AsSpan(owner.BlockOffset, owner.ByteLength).ToArray();
    }
}

/// <summary>
/// Issues actual detached companion resources from the exact original source.
/// Existing Wizard-only adapters, retail merge preimages and ID65 gates are not
/// widened. Source certification is a frozen capture, not publication freshness.
/// </summary>
public sealed class NativeId65CompanionAssetPlan
{
    private readonly NativeId65CompanionAssetBlock _block;

    private NativeId65CompanionAssetPlan(NativeId65EngineProfileContract engine,
        NativeId65CompanionParticleContract particles, NativeId65CompanionAssetBlock block)
    {
        EngineProfile = engine;
        Particles = particles;
        _block = block;
        PlanSha256 = NativeSoundBankCodec.Hash(JsonSerializer.SerializeToUtf8Bytes(new
        {
            profile = "native-id65-companion-detached-assets-clean-usa-v1",
            engine.ContractSha256, block.BlockSha256, block.Owners, block.References,
            block.VramPlacements, block.SoundSources, particles.Evidence, particles.PendingRequirements
        }));
    }

    public string SourceImageSha256 => NativeId65EngineProfileContract.OriginalImageSha256;
    public string PlanSha256 { get; }
    public NativeId65EngineProfileContract EngineProfile { get; }
    public NativeId65CompanionParticleContract Particles { get; }
    public bool IndependentLevelSupported => false;
    public bool PublicationApproved => false;
    public bool RuntimeVerified => false;
    public NativeId65CompanionAssetBlock CompileAssetBlock() => _block;

    public static async Task<NativeId65CompanionAssetPlan> CaptureAsync(string originalImagePath,
        CancellationToken cancellationToken = default)
    {
        // The source issuer hashes the same open image before and after all
        // reads. Everything below consumes detached bytes from that capture;
        // no later path reads can substitute a different package/resource owner.
        var engine = await NativeId65EngineProfileContract.CaptureAsync(originalImagePath, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var particles = NativeId65CompanionParticleContract.Capture(engine.CopyExecutable(), engine.CopyOverlay());
        var block = NativeId65CompanionAssetLayout.CompileExactTown(engine);
        return new(engine, particles, block);
    }
}

/// <summary>Bounded neutral materializer. Only the exact source adapter issues SourceCertified=true.</summary>
internal static class NativeId65CompanionAssetLayout
{
    internal static NativeId65CompanionAssetBlock Compile(ReadOnlySpan<byte> package,
        ReadOnlySpan<byte> pixels, ReadOnlySpan<byte> palette, NativeSoundBank sound) =>
        CompileCore(package, pixels, palette, sound, false);

    internal static NativeId65CompanionAssetBlock CompileExactTown(NativeId65EngineProfileContract source)
    {
        ArgumentNullException.ThrowIfNull(source);
        byte[] data = source.CopyTownData();
        var package = NativeId65EngineProfileDecoder.CaptureExactCompanion(data);
        var layout = new NativeLevelSoundLayout(data);
        if (layout.SoundOffset != 0x172610 || layout.BankOffset != 0x80800 ||
            layout.Sound.Descriptors.Count != 76 || layout.Sound.BankByteLength != 0x5E000)
            throw new InvalidDataException("Companion local sound owners changed from the exact Town source.");
        byte[] pixels = new byte[512];
        for (int row = 0; row < 32; row++)
            data.AsSpan(0x800 + 0x582A0 + row * 1024, 16).CopyTo(pixels.AsSpan(row * 16));
        byte[] palette = data.AsSpan(0x800 + 0x7A2C0, 32).ToArray();
        if (Hash(pixels) != "929c6ec41e8be9595ccf1ee140fb67c3f08be94cb65744c16231e56996124d6a" ||
            Hash(palette) != "d76c613b9fe73bf5232c70143c622a61b3d7ba02c5a59b07692765182bb86656" ||
            layout.Sound.Descriptors[72].Sample.ByteLength != 0xC60 ||
            layout.Sound.Descriptors[73].Sample.ByteLength != 0x1990 ||
            layout.Sound.Descriptors[72].Sample.Sha256 != "78dde2c2a84b201d248a8e28afaaab83efc8cb615ab6d03d9c80c8a98edaa75f" ||
            layout.Sound.Descriptors[73].Sample.Sha256 != "cf2648e50743f07a2117891bc1fc2f1b8289070109a1e11ad1a0c0ed3cf1b4b3")
            throw new InvalidDataException("Companion exact native pixel/palette/sample resource changed.");
        return CompileCore(package.CopyPackage(), pixels, palette, layout.Sound, true);
    }

    private static NativeId65CompanionAssetBlock CompileCore(ReadOnlySpan<byte> originalPackage,
        ReadOnlySpan<byte> pixels, ReadOnlySpan<byte> palette, NativeSoundBank sound, bool sourceCertified)
    {
        ArgumentNullException.ThrowIfNull(sound);
        if (pixels.Length != 512 || palette.Length != 32 || sound.Descriptors.Count <= 73)
            throw new InvalidDataException("Companion requires one exact32x32 indexed tile,16-color CLUT and owned local descriptors72/73.");
        var requirements = NativeId65EngineProfileDecoder.DecodeCompanion(originalPackage);
        if (requirements.TextureReferences.Count != 36 ||
            requirements.TextureReferences.Any(r => r.Page != 0x1D || r.Palette != 0x7A36))
            throw new InvalidDataException("Companion texture reference schema is not the supported complete36-command owner.");
        var uv = requirements.TextureReferences.Select(r => r.CopyBytes()).ToArray();
        if (uv.SelectMany(b => new[] { b[0], b[4], b[8], b[10] }).Min() != 64 ||
            uv.SelectMany(b => new[] { b[0], b[4], b[8], b[10] }).Max() != 95 ||
            uv.SelectMany(b => new[] { b[1], b[5], b[9], b[11] }).Min() != 96 ||
            uv.SelectMany(b => new[] { b[1], b[5], b[9], b[11] }).Max() != 127)
            throw new InvalidDataException("Companion texture footprint does not fit its exact source pixel owner.");

        // This is a newly allocated detached VRAM namespace, not a certificate
        // that any retail/new level has free or completely populated VRAM.
        var allocator = new NativeMobyIndexedTextureAllocation(new byte[0x80000], []);
        var tile = allocator.Reserve(32, 32);
        byte[] package = originalPackage.ToArray();
        var references = new List<NativeId65CompanionAssetReference>();
        foreach (var reference in requirements.TextureReferences)
        {
            byte[] rebased = NativeMobyModelTextureLayout.Rebase(reference,
                tile.U - 64, tile.V - 96, tile.Palette, tile.Page);
            rebased.CopyTo(package, reference.PackageOffset);
            references.Add(new("model", reference.PackageOffset + 2, 2,
                NativeId65CompanionReferenceKind.VramPalette, "clut", 0, tile.Palette));
            references.Add(new("model", reference.PackageOffset + 6, 2,
                NativeId65CompanionReferenceKind.VramTexturePage, "pixels", 0, tile.Page));
        }
        byte[] descriptors = new byte[40];
        var samples = new List<byte>();
        var sounds = new List<NativeId65CompanionSoundSource>();
        for (int index = 0; index < 2; index++)
        {
            NativeSoundDescriptor descriptor = sound.Descriptors[72 + index];
            if (!descriptor.IsSupportedOneShot || descriptor.Sample.ByteLength is <= 0 or > 0x10000)
                throw new InvalidDataException("Companion sound requires a bounded supported native one-shot descriptor/sample.");
            byte[] bytes = descriptor.CopyDescriptor();
            int sampleOffset = samples.Count;
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, (uint)sampleOffset);
            bytes.CopyTo(descriptors, index * 20);
            samples.AddRange(descriptor.Sample.CopyBytes());
            package[4 + index] = (byte)index;
            references.Add(new("model", 4 + index, 1,
                NativeId65CompanionReferenceKind.LocalDescriptorIndex, "sound-descriptors", index * 20, (uint)index));
            references.Add(new("sound-descriptors", index * 20, 4,
                NativeId65CompanionReferenceKind.SampleBankRelativeOffset, "sample-bank", sampleOffset, (uint)sampleOffset));
            sounds.Add(new(72 + index, descriptor.Sample.ByteLength, descriptor.Sha256,
                descriptor.Sample.Sha256, sourceCertified ? 0x121E800L + descriptor.SampleOffset : null));
        }

        // Inverse every permitted package field/UV edit, then compare every byte
        // of the complete registered allocation, including any opaque padding.
        byte[] inverse = package.ToArray();
        foreach (var reference in requirements.TextureReferences)
            reference.CopyBytes().CopyTo(inverse, reference.PackageOffset);
        inverse[4] = 72;
        inverse[5] = 73;
        if (!inverse.AsSpan().SequenceEqual(originalPackage))
            throw new InvalidDataException("Companion asset materialization changed an unowned package byte.");
        var finalTextures = NativeMobyModelTextureLayout.Decode(package);
        if (finalTextures.References.Count != 36 ||
            finalTextures.References.Any(r => r.Page != tile.Page || r.Palette != tile.Palette))
            throw new InvalidDataException("Companion native texture references failed final readback.");

        var owners = new List<NativeId65CompanionAssetOwner>();
        var output = new List<byte>();
        foreach (var (id, bytes) in new[]
        {
            ("model", package), ("pixels", pixels.ToArray()), ("clut", palette.ToArray()),
            ("sound-descriptors", descriptors), ("sample-bank", samples.ToArray())
        })
        {
            if (output.Count % 4 != 0 || bytes.Length % 4 != 0)
                throw new InvalidDataException("Companion native asset owner lost its four-byte alignment.");
            owners.Add(new(id, output.Count, bytes.Length, Hash(bytes)));
            output.AddRange(bytes);
        }
        var placements = new List<NativeId65CompanionVramPlacement>();
        for (int row = 0; row < 32; row++)
            placements.Add(new("pixels", row * 16, tile.PixelOffset + row * 1024, 16));
        placements.Add(new("clut", 0, tile.PaletteOffset, 32));
        VerifyReferences(output.ToArray(), owners, references, placements);
        return new(output.ToArray(), owners, references, placements, sounds, sourceCertified);
    }

    private static void VerifyReferences(byte[] bytes, List<NativeId65CompanionAssetOwner> owners,
        List<NativeId65CompanionAssetReference> references, List<NativeId65CompanionVramPlacement> placements)
    {
        foreach (var reference in references)
        {
            var field = owners.Single(o => o.Id == reference.FieldOwnerId);
            var target = owners.Single(o => o.Id == reference.TargetOwnerId);
            if (reference.FieldRelativeOffset < 0 || reference.FieldRelativeOffset + reference.ByteWidth > field.ByteLength ||
                reference.TargetRelativeOffset < 0 || reference.TargetRelativeOffset >= target.ByteLength)
                throw new InvalidDataException("Companion typed reference exceeds its detached owner.");
            int at = field.BlockOffset + reference.FieldRelativeOffset;
            uint encoded = reference.ByteWidth switch
            {
                1 => bytes[at],
                2 => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(at)),
                4 => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at)),
                _ => throw new InvalidDataException("Unknown companion reference width.")
            };
            if (encoded != reference.EncodedValue)
                throw new InvalidDataException("Companion typed reference encoded value failed readback.");
        }
        var occupied = new bool[0x80000];
        foreach (var placement in placements)
        {
            var owner = owners.Single(o => o.Id == placement.PayloadOwnerId);
            if (placement.ByteLength <= 0 || placement.PayloadRelativeOffset < 0 ||
                placement.PayloadRelativeOffset + placement.ByteLength > owner.ByteLength ||
                placement.VramRelativeOffset < 0 || placement.VramRelativeOffset + placement.ByteLength > occupied.Length ||
                occupied.AsSpan(placement.VramRelativeOffset, placement.ByteLength).Contains(true))
                throw new InvalidDataException("Companion pixel/CLUT footprints overlap or exceed their detached owner.");
            Array.Fill(occupied, true, placement.VramRelativeOffset, placement.ByteLength);
        }
    }

    private static string Hash(ReadOnlySpan<byte> bytes) => NativeSoundBankCodec.Hash(bytes);
}
