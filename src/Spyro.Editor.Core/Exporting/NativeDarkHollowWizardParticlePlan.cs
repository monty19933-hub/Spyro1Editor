using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;

namespace Spyro.Editor.Core.Exporting;

public sealed class NativeParticleSourceComponent
{
    private readonly byte[] _bytes;
    internal NativeParticleSourceComponent(int levelId, int offset, NativeParticleDescriptorTable table)
    { LevelId = levelId; DataOffset = offset; _bytes = table.CopyBytes(); RecordCount = table.Count; Sha256 = NativeSoundBankCodec.Hash(_bytes); }
    public int LevelId { get; }
    public int DataOffset { get; }
    public int RecordCount { get; }
    public string Sha256 { get; }
    public byte[] CopyComponent() => _bytes.ToArray();
}

public sealed class NativeDarkHollowWizardParticlePlan
{
    private const string DarkSha = "a937aecb3626ffd2311ba136684a34c9f36877054b52557b4524fcacee0e2d97";
    private const string WizardSha = "9973493f865b5e86bc17b32c5af9e3d7596e25615618c007ed28e0379161f2d8";
    private readonly byte[] _original;
    private readonly NativeParticleDescriptorAppend _append;
    private readonly int _dataSubfileOffset;
    private static readonly IReadOnlyList<NativeGlobalSoundSourceCertificate> Certificates = Array.AsReadOnly<NativeGlobalSoundSourceCertificate>(
    [
        new("particle-count-signed-ID-width-table-and-next-sound", 0x39A4, 0x88, "3919736090b47e74391a4eff0bce53a9aa009dac25f0a9151356a7fbe46e4f5a"),
        new("model-root0-at-particle-ID64-boundary", 0x3A48, 0x10, "51ad7f5df52c80e5a6796e8423310a9d2849eec0b38f00a3038874c9d92d6736"),
        new("particle-renderer-descriptor-table-base", 0x47CA4, 0x18, "b0e6a46f040c4fd9e962504501414bbb8008c052307a38cd81c50aeb93cdec09"),
        new("kind2-renderer-indexes-selector-low-byte", 0x48444, 0x14, "ea1a5df56dfd67a77b6e80b865694023796d565ab567662c45a16a0ecedcb5fe")
    ]);
    private NativeDarkHollowWizardParticlePlan(NativeParticleSourceComponent[] components, byte[] dark,
        byte[] donor, NativeDarkHollowWizardTexturePlan texture)
    {
        SourceComponents = Array.AsReadOnly(components.ToArray());
        if (components.Length != 35 || components.Sum(c => c.RecordCount) != 428)
            throw new InvalidDataException("Particle exact all-level source inventory changed.");
        var source = components.Single(c => c.LevelId == 12); var donorComponent = components.Single(c => c.LevelId == 33);
        _original = source.CopyComponent();
        if (source.DataOffset != 0x128F7C || _original.Length != 0x8C || source.RecordCount != 11 ||
            source.Sha256 != "b3594b40a08a83e32f464f7717379280706a35a92b2955ea922201ca37820e49" ||
            donorComponent.DataOffset != 0x173504 || donorComponent.Sha256 != "06a8f7ac0de9ecd0043e4533c1fc4739f90fc192b9826d97e3483d934f58fbb5" ||
            NativeSoundBankCodec.Hash(dark) != DarkSha || NativeSoundBankCodec.Hash(donor) != WizardSha)
            throw new InvalidDataException("Particle exact original component/data owners changed.");
        _dataSubfileOffset = new NativeLevelSoundLayout(dark).DataOffset;
        byte[] donorRecord = donor.AsSpan(0x173518, 12).ToArray();
        var patch = texture.Patches.Single(p => p.Owner == "particle7");
        if (patch.Offset != 0 || patch.ByteLength != 12 || !patch.CopyBefore().AsSpan().SequenceEqual(donorRecord) ||
            !patch.CopyAfter().AsSpan().SequenceEqual(texture.CopyParticle7Descriptor()) || NativeParticleDescriptorCodec.RequireRecord(donorRecord) != 7)
            throw new InvalidDataException("Particle descriptor does not bind its source-issued complete texture patch.");
        _append = NativeParticleDescriptorAppend.Build(NativeParticleDescriptorCodec.Decode(_original), texture.CopyParticle7Descriptor());
        RequireOriginalAndTexture(dark, texture);
    }
    public string SourceImageSha256 => NativeMobySourceInventory.OriginalImageSha256;
    public string SourceDataSha256 => DarkSha;
    public string DonorDataSha256 => WizardSha;
    public IReadOnlyList<NativeParticleSourceComponent> SourceComponents { get; }
    public IReadOnlyList<NativeGlobalSoundSourceCertificate> NativeCertificates => Certificates;
    public int OriginalComponentDataOffset => 0x128F7C;
    public int OriginalCount => _append.OriginalCount;
    public int FinalCount => _append.FinalCount;
    public byte[] CopyOriginalComponent() => _original.ToArray();
    public byte[] CopyFinalComponent() => _append.CopyComponent();
    public byte[] CopyDescriptor() => _append.CopyInsertion();
    public bool BehaviorInstalled => false;
    public bool CompleteNativeClosureProven => false;
    public bool PublicationApproved => false;
    internal void RequireOriginalAndTexture(ReadOnlySpan<byte> original, NativeDarkHollowWizardTexturePlan texture)
    {
        if (original.Length != 0x28D800 || NativeSoundBankCodec.Hash(original) != DarkSha ||
            texture.SourceImageSha256 != SourceImageSha256 || texture.SourceDataSha256 != DarkSha || texture.DonorDataSha256 != WizardSha ||
            !texture.CopyParticle7Descriptor().AsSpan().SequenceEqual(CopyDescriptor()))
            throw new InvalidDataException("Particle stage must share the exact original Dark and issued relocated texture owner.");
    }
    internal NativeSkyRelocationPayload CopyOriginalAddressPayload(int index) =>
        new(index, 37, 0xF01000L + OriginalComponentDataOffset + _original.Length, 14,
            OriginalComponentDataOffset + _original.Length - _dataSubfileOffset, 0, CopyDescriptor(), 1, false);
    internal IReadOnlyList<NativeResourceOriginalBytePatch> CopyOriginalFieldPatches()
    {
        byte[] final = CopyFinalComponent();
        return Array.AsReadOnly(new[]
        {
            new NativeResourceOriginalBytePatch("particle.component-size", OriginalComponentDataOffset, _original.AsSpan(0, 4), final.AsSpan(0, 4)),
            new NativeResourceOriginalBytePatch("particle.record-count", OriginalComponentDataOffset + 4, _original.AsSpan(4, 4), final.AsSpan(4, 4))
        });
    }
    public static async Task<NativeDarkHollowWizardParticlePlan> CaptureAsync(string path, NativeDarkHollowWizardTexturePlan texture, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path); ArgumentNullException.ThrowIfNull(texture);
        path = Path.GetFullPath(path);
        await using FileStream image = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        await RequireImage(image, token);
        var layout = DiscImage.DetectLayout(path);
        if (layout.SectorSize != 2352 || layout.UserOffset != 24) throw new InvalidDataException("Particle source requires exact native disc geometry.");
        byte[] directory = DiscImage.ReadFileBytes(image, layout, 37, 0, 0x800);
        if (NativeSoundBankCodec.Hash(directory) != "15d3e9c45f07e27a0ddd3f3a49b23a077e2c7750021cc89c55b7b16c551186cd")
            throw new InvalidDataException("Particle source WAD directory changed.");
        byte[] executable = DiscImage.ReadFileBytes(image, layout, 53875, 0, 0x66000);
        if (NativeSoundBankCodec.Hash(executable) != "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998")
            throw new InvalidDataException("Particle source executable changed.");
        foreach (var certificate in Certificates)
            if (NativeSoundBankCodec.Hash(executable.AsSpan(certificate.ExecutableOffset, certificate.ByteLength)) != certificate.Sha256)
                throw new InvalidDataException("Particle native loader/renderer boundary certificate changed.");
        List<NativeParticleSourceComponent> components = []; byte[]? dark = null, donor = null;
        for (int i = 0; i < 35; i++)
        {
            token.ThrowIfCancellationRequested();
            int id = (i / 6 + 1) * 10 + i % 6, entry = 10 + i * 2;
            int offset = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(directory.AsSpan(entry * 8)));
            int length = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(directory.AsSpan(entry * 8 + 4)));
            if (offset < 0x800 || length < 0x2800 || length > 0x800000 || offset + (long)length > 0x6927000)
                throw new InvalidDataException("Particle data owner exceeds the native WAD boundary.");
            byte[] data = DiscImage.ReadFileBytes(image, layout, 37, offset, length); var sound = new NativeLevelSoundLayout(data);
            int start = sound.ParticleOffset;
            var table = NativeParticleDescriptorCodec.Decode(data.AsSpan(start, sound.SoundOffset - start));
            if (!table.CopyBytes().AsSpan().SequenceEqual(data.AsSpan(start, table.ByteLength)))
                throw new InvalidDataException("Particle source roundtrip lost original bytes.");
            components.Add(new(id, start, table));
            if (id == 12) dark = data; if (id == 33) donor = data;
        }
        var result = new NativeDarkHollowWizardParticlePlan(components.ToArray(), dark!, donor!, texture);
        await RequireImage(image, token);
        return result;
    }
    private static async Task RequireImage(FileStream image, CancellationToken token)
    {
        if (image.Length != NativeMobySourceInventory.OriginalImageByteLength) throw new InvalidDataException("Particle source requires the exact original image length.");
        image.Position = 0;
        if (Convert.ToHexStringLower(await SHA256.HashDataAsync(image, token)) != NativeMobySourceInventory.OriginalImageSha256 ||
            image.Length != NativeMobySourceInventory.OriginalImageByteLength || image.Position != NativeMobySourceInventory.OriginalImageByteLength)
            throw new InvalidDataException("Particle source requires the unchanged original image identity.");
    }
}
