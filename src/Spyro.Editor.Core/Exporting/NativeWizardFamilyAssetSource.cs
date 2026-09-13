using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;

[assembly: InternalsVisibleTo("Spyro.Editor.NativeWizardFamilyAssetSmoke")]

namespace Spyro.Editor.Core.Exporting;

/// <summary>Private fixed33/T6 capture. Neutral decoders and metadata never issue this source.</summary>
internal sealed class NativeWizardFamilyAssetSource
{
    internal const long DonorWadOffset = 0x2F6B000;
    internal const string DonorSha256 = "9973493f865b5e86bc17b32c5af9e3d7596e25615618c007ed28e0379161f2d8";
    private readonly byte[] _donor;
    private readonly byte[] _executable;
    private readonly NativeMobyModelLoaderCertificate _loader;

    private NativeWizardFamilyAssetSource(NativeId65EngineProfileContract engine,
        byte[] donor, byte[] peteHeader, byte[] pete)
    {
        byte[] town = engine.CopyTownData(), exe = engine.CopyExecutable();
        ValidateCapturedBytes(donor, town, exe, peteHeader, pete);
        _donor = donor;
        _executable = exe;
        OriginalExecutableDomain = new(NativeAddressKind.ExecutableFile,
            "wizard-family-original-executable:" + Hash(exe), NativeAddressRevision.Original, exe.Length);
        EngineContractSha256 = engine.ContractSha256;
        // This fixed definition is an independent native locator assertion,
        // not caller catalog authority. DecodeLevel remains a neutral decoder;
        // this private factory's exact source capture supplies provenance.
        Level = NativeMobySourceInventory.DecodeLevel(donor, new LevelDefinition
        {
            Key = "wizardpeak", DisplayName = "Wizard Peak", LevelId = 33,
            SourceWadEntry = 40, SourceRecordCount = 165,
            SourceTableRelativeOffset = "0x1D4978", SourceTableWadOffset = "0x313F978"
        }, DonorWadOffset);
        Row = Level.Rows[6];
        if (Row.ActorId != 0x11B || Row.RecordWadOffset != 0x313FB88 ||
            Row.PropertiesPointer != 0xC350 || !Row.HasLocalModelRegistration ||
            Row.PropertiesLocation != NativeMobyPropertiesLocation.InsideComponent)
            throw new InvalidDataException("Fixed Wizard33/T6 lost its native source row/registered/private identity.");
        var original = new NativeAddressSpace(NativeAddressKind.LevelData, "wizard-family-source:" + DonorSha256,
            NativeAddressRevision.Original, donor.Length);
        Models = NativeMobyModelRegistrationSource.CaptureInventory(Level, original, Read);
        NativeMobyModelLoaderCertificate loader = NativeMobyModelLoaderCertificate.CaptureRetail(exe, peteHeader, pete);
        _loader = loader;
        if (loader.SharedClasses.Contains((ushort)0x11B) || loader.SharedClasses.Contains((ushort)0x26) ||
            !loader.SharedClasses.Contains((ushort)0x56))
            throw new InvalidDataException("Wizard/local and reward/shared native model registrations changed.");
        DonorSound = new NativeLevelSoundLayout(donor);
        TownSound = new NativeLevelSoundLayout(town);
        byte[] geometry = pete.AsSpan(0xAA8, 0x32374).ToArray();
        int root0 = checked((int)Models.Registrations[0].RootDataOffset);
        int root1 = checked((int)Models.Registrations[1].RootDataOffset);
        var animations = NativePlayerAnimationStreamCodec.Decode(donor.AsSpan(root0, root1 - root0), geometry);
        Animation = animations.Records.Single(r => r.AnimationId == 8);
        AnimationDataOffset = checked(root0 + Animation.StreamOffset);
        var townAnimations = NativePlayerAnimationStreamCodec.Decode(town.AsSpan(0x173000, 0xCC30), geometry);
        byte[] incoming = Animation.CopyRecord(), resident = townAnimations.Records.Single(r => r.AnimationId == 18).CopyRecord();
        if (!townAnimations.Records.Select(r => r.AnimationId).SequenceEqual(new[] {18,19,29,40}) ||
            Word(pete, 0x58) != uint.MaxValue || incoming.Length != 0x3E1C ||
            Hash(incoming) != "cf194a65fc9b3b8790f6cd96eae5ef309156d00ad205d85790b2128e57243eb0" ||
            !incoming.AsSpan(0x0A, 0x22).SequenceEqual(resident.AsSpan(0x0A, 0x22)))
            throw new InvalidDataException("Wizard animation8/Town18/shared-slot texture/geometry source binding changed.");
        AnimationTextureHeaderSha256 = Hash(incoming.AsSpan(0x0A, 0x22));
        SharedGeometrySha256 = Hash(geometry);
        return;

        byte[] Read(NativeByteExtent extent)
        {
            if (extent.Space != original || extent.Offset < 0 || extent.ByteLength < 0 || extent.EndExclusive > donor.Length)
                throw new InvalidDataException("Source read escaped the private original donor domain.");
            return donor.AsSpan((int)extent.Offset, (int)extent.ByteLength).ToArray();
        }
    }

    internal NativeMobySourceLevel Level { get; }
    internal NativeMobySourceRow Row { get; }
    internal NativeMobyModelRegistrationSource Models { get; }
    internal NativeAddressSpace OriginalExecutableDomain { get; }
    internal NativeLevelSoundLayout DonorSound { get; }
    internal NativeLevelSoundLayout TownSound { get; }
    internal NativePlayerAnimationRecord Animation { get; }
    internal int AnimationDataOffset { get; }
    internal string AnimationTextureHeaderSha256 { get; }
    internal string SharedGeometrySha256 { get; }
    internal string EngineContractSha256 { get; }
    internal byte[] CopyDonor() => _donor.ToArray();

    // Only exact original owners retained by this existing issuer are readable.
    // Its emitted source-asset block and future authored Body/Scene never become
    // Original domains merely because their bytes are available to a caller.
    internal byte[] ReadPrivateSource(NativeByteExtent extent)
    {
        ArgumentNullException.ThrowIfNull(extent);
        ReadOnlySpan<byte> source = ReferenceEquals(extent.Space, Models.Original) ? _donor :
            ReferenceEquals(extent.Space, OriginalExecutableDomain) ? _executable :
            throw new InvalidDataException("Wizard private read belongs to another actual original source domain.");
        return source.Slice(checked((int)extent.Offset), checked((int)extent.ByteLength)).ToArray();
    }

    internal void RequirePrivateStorageSource()
    {
        Models.RequireSource(ReadPrivateSource);
        Require(_executable, 0x66000, "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998");
    }

    internal void RequireNonSharedModelClass(ushort nativeClass)
    {
        if (!_loader.IsRetailCertified || nativeClass == 0 || nativeClass >= 512 || _loader.SharedClasses.Contains(nativeClass))
            throw new InvalidDataException("A new local generic model cannot replace special class 0, escape 512 entries or collide with an actual PETE shared class.");
        // Absence from this actual shared table is not model-space, native
        // behavior, placement, runtime or publication approval.
    }

    internal static async Task<NativeWizardFamilyAssetSource> CaptureAsync(string originalBin, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(originalBin);
        string path = Path.GetFullPath(originalBin);
        // Reuse the existing private Town/EXE/PETE engine issuer. Its level
        // installation/closure flags are not inherited by this detached asset.
        var engine = await NativeId65EngineProfileContract.CaptureAsync(path, token);
        token.ThrowIfCancellationRequested();
        await using FileStream image = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        await RequireOriginalAsync(image, token);
        DiscLayout layout = DiscImage.DetectLayout(image);
        var wad = DiscImage.FindRootFileRecord(image, layout, n => n.Equals("WAD.WAD", StringComparison.OrdinalIgnoreCase));
        var exe = DiscImage.FindRootFileRecord(image, layout, n => n.Equals("SCUS_942.28", StringComparison.OrdinalIgnoreCase));
        if (layout.SectorSize != 2352 || layout.UserOffset != 24 || wad.Lba != 37 || wad.Size != 0x6927000 ||
            exe.Lba != 53875 || exe.Size != 0x66000)
            throw new InvalidDataException("Wizard source requires exact original ISO/WAD owners.");
        byte[] directory = Read(37, 0, 0x800);
        Require(directory, 0x800, "15d3e9c45f07e27a0ddd3f3a49b23a077e2c7750021cc89c55b7b16c551186cd");
        if (Word(directory, 40 * 8) != DonorWadOffset || Word(directory, 40 * 8 + 4) != 0x28A000)
            throw new InvalidDataException("Wizard33 native directory slot changed.");
        byte[] donor = Read(37, DonorWadOffset, 0x28A000), peteHeader = Read(37, 0x7B8800, 0x800),
            pete = Read(37, 0x7B9000, 0x39C48);
        if (!Read(53875, 0, 0x66000).AsSpan().SequenceEqual(engine.CopyExecutable()) ||
            !Read(37, 0x119E000, 0x2E2000).AsSpan().SequenceEqual(engine.CopyTownData()))
            throw new InvalidDataException("Wizard capture lost the actual existing engine source snapshot.");
        var result = new NativeWizardFamilyAssetSource(engine, donor, peteHeader, pete);
        await RequireOriginalAsync(image, token);
        return result;

        byte[] Read(int lba, long offset, int length)
        {
            token.ThrowIfCancellationRequested();
            byte[] bytes = DiscImage.ReadFileBytes(image, layout, lba, offset, length);
            if (bytes.Length != length) throw new InvalidDataException("Incomplete native source read.");
            token.ThrowIfCancellationRequested();
            return bytes;
        }
    }

    internal static void ValidateCapturedBytes(ReadOnlySpan<byte> donor, ReadOnlySpan<byte> town,
        ReadOnlySpan<byte> exe, ReadOnlySpan<byte> peteHeader, ReadOnlySpan<byte> pete)
    {
        // All fixed lengths before hashing, decoding or cloning any owner.
        if (donor.Length != 0x28A000 || town.Length != 0x2E2000 || exe.Length != 0x66000 ||
            peteHeader.Length != 0x800 || pete.Length != 0x39C48)
            throw new InvalidDataException("Wizard capture requires five complete bounded source owners.");
        Require(donor, 0x28A000, DonorSha256);
        Require(town, 0x2E2000, NativeId65EngineProfileDecoder.TownDataSha256);
        Require(exe, 0x66000, "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998");
        _ = NativeMobyModelLoaderCertificate.CaptureRetail(exe, peteHeader, pete);
        Require(donor.Slice(0x19B518, 0x35B4), 0x35B4, "070a57069c8747ada4caacc0394b93e9a970e5ba7ff21159584abdc7a0fd4faf");
        Require(donor.Slice(0x1CAC88, 0x564), 0x564, "9ea5c72ba675e9c6a81433f7408695399e19bc188b4b2a923069d71de601789a");
        Require(donor.Slice(0x1D4B88, 0x58), 0x58, "59401eafeab453fd9d26f14eedca58390653dc5f4fcc3d2560f7b3e8fc7afa82");
        Require(donor.Slice(0x1DAB50, 0x50), 0x50, "5b3d762f6117ea30e959a21bf2778f3442b03eba93125c9a0e0a642cc58d345b");
    }

    internal static (int PixelOffset, int PaletteOffset, int RowBytes, int Rows) ValidateFootprint(
        ushort clut, ushort page, int u0, int v0, int u1, int v1)
    {
        if (((page >> 7) & 3) != 0 || u0 < 0 || v0 < 0 || u1 > 255 || v1 > 255 ||
            u1 < u0 || v1 < v0 || (u0 & 1) != 0 || ((u1 - u0 + 1) & 1) != 0)
            throw new InvalidDataException("Wizard source texture needs a bounded even-width native4bpp footprint.");
        int rowBytes = (u1 - u0 + 1) / 2, rows = v1 - v0 + 1;
        int x = (page & 15) * 128 + u0 / 2 - 1024, y = ((page & 16) != 0 ? 256 : 0) + v0;
        int cx = (clut & 63) * 32 - 1024, cy = clut >> 6;
        if (x < 0 || x + rowBytes > 1024 || y < 0 || y + rows > 512 || cx < 0 || cx + 32 > 1024 || cy >= 512)
            throw new InvalidDataException("Wizard pixels/CLUT escape the complete native level VRAM source owner.");
        return (y * 1024 + x, cy * 1024 + cx, rowBytes, rows);
    }

    private static async Task RequireOriginalAsync(Stream image, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!image.CanRead || !image.CanSeek || image.Length != NativeMobySourceInventory.OriginalImageByteLength)
            throw new InvalidDataException("Wizard source requires the exact original bounded image.");
        image.Position = 0;
        string hash = Convert.ToHexStringLower(await SHA256.HashDataAsync(image, token));
        token.ThrowIfCancellationRequested();
        if (hash != NativeMobySourceInventory.OriginalImageSha256 || image.Position != NativeMobySourceInventory.OriginalImageByteLength ||
            image.ReadByte() != -1 || image.Position != NativeMobySourceInventory.OriginalImageByteLength ||
            image.Length != NativeMobySourceInventory.OriginalImageByteLength)
            throw new InvalidDataException("Wizard source changed SHA/position/actual EOF/length.");
        token.ThrowIfCancellationRequested();
    }
    internal static uint Word(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
    internal static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Require(ReadOnlySpan<byte> bytes, int length, string hash)
    { if (bytes.Length != length || Hash(bytes) != hash) throw new InvalidDataException("Exact Wizard family source owner changed."); }
}
