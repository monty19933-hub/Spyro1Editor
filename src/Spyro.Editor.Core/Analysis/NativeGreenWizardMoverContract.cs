using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;

[assembly: InternalsVisibleTo("Spyro.Editor.NativeGreenWizardMoverSmoke")]

namespace Spyro.Editor.Core.Analysis;

internal sealed record NativeGreenWizardNativeSpanBinding(string Name, string Domain,
    long SourceOffset, uint RuntimeAddress, int ByteLength, string Sha256);
internal sealed record NativeGreenWizardColliderFrame(int Animation, int Frame,
    int SelectorByteOffset, bool HasCollider, byte PackedScale,
    long FrameWordWadOffset, uint RawFrameWord, long? ColliderWadOffset);

/// <summary>
/// Detached package grammar, not full-source ownership or a physical envelope.
/// Null native collider states are data, not zero-radius active colliders.
/// </summary>
internal sealed class NativeGreenWizardColliderPackage
{
    internal NativeGreenWizardColliderPackage(string packageSha, long packageWad,
        string colliderSha, NativeGreenWizardColliderFrame[] frames)
    {
        PackageSha256 = packageSha;
        PackageWadOffset = packageWad;
        ColliderSha256 = colliderSha;
        Frames = Array.AsReadOnly(frames.ToArray());
    }

    public string PackageSha256 { get; }
    public long PackageWadOffset { get; }
    public string ColliderSha256 { get; }
    public long ColliderWadOffset => PackageWadOffset + 0x35A4;
    public int ColliderByteLength => 16;
    public int BroadPhaseRadius => 2011;
    public int SphereRadius => 400;
    public int VertexIndex => 0;
    public IReadOnlyList<NativeGreenWizardColliderFrame> Frames { get; }
    public bool BodyEnvelopeCertified => false;
    public bool SourceImageCertified => false;

    public NativeGreenWizardColliderFrame Select(int animation, int frame)
    {
        if (animation is < 0 or > 2)
            throw new InvalidDataException("Wizard animation is outside the bound package.");
        return Frames.FirstOrDefault(f => f.Animation == animation && f.Frame == frame)
            ?? throw new InvalidDataException("Wizard frame is outside the bound animation.");
    }

    public NativeGreenWizardColliderFrame RequireActive(int animation, int frame)
    {
        NativeGreenWizardColliderFrame selected = Select(animation, frame);
        if (!selected.HasCollider)
            throw new InvalidDataException("Wizard frame selects the native null collider; no active body may be inferred.");
        return selected;
    }
}

/// <summary>
/// Exact USA/Magic resident mover and collider evidence. Not a route compiler,
/// physics engine, rendered-body certificate, publication grant or runtime pass.
/// </summary>
internal sealed class NativeGreenWizardMoverContract
{
    public const string Profile = "native-green-wizard-mover-source-contract-v1";
    public const string Scope = "Pinned original native code, uncorrected helper arithmetic and collider ownership/selector grammar only. No transformed vertex/body envelope, collision-query execution, complete trajectory, dry-route acceptance, candidate or runtime certification.";
    private const int PackageByteLength = 0x35B4;
    private const string ExecutableSha = "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998";
    private const string OverlaySha = "98e135e18876ea094ca9484b33cfb38438e2b99b72ee4d6d66e907722c744a1d";
    private const string DataSha = "ad34af2e3401514e7db740691c80fd2bbf59057a55bdfedbaaa39b6939141fcb";
    private const string PackageSha = "8fdc3c77836135cac07864c550b06a5573fb05927f83cb4f05176e37f95821ac";
    private readonly short[] _cosine;
    private readonly short[] _sine;

    private NativeGreenWizardMoverContract(NativeGreenWizardColliderPackage collider,
        IReadOnlyList<NativeGreenWizardNativeSpanBinding> bindings, short[] cosine, short[] sine)
    {
        Collider = collider;
        Bindings = Array.AsReadOnly(bindings.ToArray());
        _cosine = cosine.ToArray();
        _sine = sine.ToArray();
        ContractSha256 = Hash(JsonSerializer.SerializeToUtf8Bytes(new
        {
            Profile, SourceImageSha256, bindings = Bindings, collider = Collider,
            cosine = _cosine, sine = _sine, Scope
        }));
    }

    public string SourceImageSha256 => NativeGreenWizardCandidateSource.OriginalImageSha256;
    public string ContractSha256 { get; }
    public NativeGreenWizardColliderPackage Collider { get; }
    public IReadOnlyList<NativeGreenWizardNativeSpanBinding> Bindings { get; }
    public bool BodyEnvelopeCertified => false;
    public bool CandidateWritten => false;
    public bool RuntimeVerified => false;

    public NativeGreenWizardUncorrectedMove ProposeUncorrected(int x, int y, int z,
        byte yaw, int nativeGlobalValue, short groundOffset, int callerObservedFloorZ,
        NativeGreenWizardCorrectionObservation residentCorrection,
        NativeGreenWizardCorrectionObservation terrainCorrection) =>
        NativeGreenWizardMoverMath.ProposeUncorrected(x, y, z, _cosine[yaw], _sine[yaw],
            nativeGlobalValue, groundOffset, callerObservedFloorZ, residentCorrection, terrainCorrection);

    public static NativeGreenWizardMoverContract InspectSource(string sourceImagePath)
    {
        // Reuse only the immutable original-source inspector. No v1 plan or
        // candidate is modified, and no historical receipt is runtime authority.
        var source = NativeGreenWizardCandidateSource.Inspect(sourceImagePath);
        var target = source.GetLevel("magiccrafters").Binding;
        using FileStream image = new(source.SourceImagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        DiscLayout layout = DiscImage.DetectLayout(source.SourceImagePath);
        DiscFileRecord wad = DiscImage.FindRootFileRecord(image, layout, n => n == "WAD.WAD");
        DiscFileRecord exeRecord = DiscImage.FindRootFileRecord(image, layout, n => n == "SCUS_942.28");
        if (layout.SectorSize != 2352 || layout.UserOffset != 24 || wad.Lba != 37 ||
            exeRecord.Lba != 53875 || exeRecord.Size != 0x66000)
            throw new InvalidDataException("Wizard mover source ISO domains changed.");
        byte[] nativeEntries = DiscImage.ReadFileBytes(image, layout, wad.Lba, 33 * 8L, 16);
        if (U32(nativeEntries, 0) != 0x2727000 || U32(nativeEntries, 4) != 0x14000 ||
            U32(nativeEntries, 8) != 0x273B000 || U32(nativeEntries, 12) != 0x2BE800 ||
            target.DataWadOffset != 0x273B000 || target.DataByteLength != 0x2BE800)
            throw new InvalidDataException("Wizard mover source WAD entry ownership changed.");
        byte[] executable = DiscImage.ReadFileBytes(image, layout, exeRecord.Lba, 0, exeRecord.Size);
        byte[] overlay = DiscImage.ReadFileBytes(image, layout, wad.Lba, 0x2727000, 0x14000);
        byte[] data = DiscImage.ReadFileBytes(image, layout, wad.Lba, 0x273B000, 0x2BE800);
        ValidateExactDomains(executable, overlay, data);

        int root = checked((int)U32(data, 0x98));
        int next = checked((int)U32(data, 0x9C));
        int nextRegistered = Enumerable.Range(0, 64).Select(i => U32(data, 0x50 + i * 4))
            .Where(p => p > root).Select(p => checked((int)p)).Min();
        if (root != 0x1BAFDC || next != 0x1BE590 || next != nextRegistered ||
            next - root != PackageByteLength || U16(data, 0x174) != 0x011B || U16(data, 0x176) != 0x01F7)
            throw new InvalidDataException("Wizard collider owner does not end at the next registered native root.");
        NativeGreenWizardColliderPackage collider = CaptureDetachedPackage(
            data.AsSpan(root, next - root).ToArray(), 0x273B000L + root);
        if (collider.PackageSha256 != PackageSha)
            throw new InvalidDataException("Wizard source package changed.");

        List<NativeGreenWizardNativeSpanBinding> bindings = [];
        foreach (var span in EvidenceSpans)
        {
            byte[] domain = span.Domain == "EXE" ? executable : overlay;
            int offset = span.Domain == "EXE" ? checked((int)span.SourceOffset)
                : checked((int)(span.SourceOffset - 0x2727000));
            if (Hash(domain.AsSpan(offset, span.ByteLength)) != span.Sha256)
                throw new InvalidDataException("Wizard native evidence span changed: " + span.Name);
            bindings.Add(span);
        }
        bindings.Add(new("Magic package owner", "WAD", 0x28F5FDC, 0, PackageByteLength, collider.PackageSha256));
        bindings.Add(new("Final animated sphere record", "WAD", collider.ColliderWadOffset, 0, 16, collider.ColliderSha256));
        short[] cosine = ReadTrig(executable, 0x5D478);
        short[] sine = ReadTrig(executable, 0x5D3F8);

        image.Position = 0;
        if (image.Length != NativeGreenWizardCandidateSource.OriginalImageByteLength ||
            Convert.ToHexStringLower(SHA256.HashData(image)) != source.SourceImageSha256 ||
            image.Length != NativeGreenWizardCandidateSource.OriginalImageByteLength || image.Position != image.Length)
            throw new InvalidDataException("The complete Wizard mover source changed during capture.");
        return new(collider, bindings, cosine, sine);
    }

    internal static void ValidateExactDomains(byte[] executable, byte[] overlay, byte[] data)
    {
        ArgumentNullException.ThrowIfNull(executable);
        ArgumentNullException.ThrowIfNull(overlay);
        ArgumentNullException.ThrowIfNull(data);
        if (executable.Length != 0x66000 || Hash(executable) != ExecutableSha)
            throw new InvalidDataException("Wizard native executable differs from the pinned source.");
        if (overlay.Length != 0x14000 || Hash(overlay) != OverlaySha)
            throw new InvalidDataException("Wizard native overlay differs from the pinned source.");
        if (data.Length != 0x2BE800 || Hash(data) != DataSha)
            throw new InvalidDataException("Wizard native data differs from the pinned source.");
    }

    public static NativeGreenWizardColliderPackage CaptureDetachedPackage(byte[] package, long packageWadOffset)
    {
        ArgumentNullException.ThrowIfNull(package);
        if (package.Length != PackageByteLength || packageWadOffset < 0 ||
            packageWadOffset > long.MaxValue - PackageByteLength)
            throw new InvalidDataException("Wizard package boundary must end exactly after the 16-byte collider owner.");
        byte[] bytes = package.ToArray();
        if (U32(bytes, 0) != 3 || U32(bytes, 0x34) != 0x238)
            throw new InvalidDataException("Wizard package animation owner/header grammar is unsupported.");
        if (U32(bytes, 0x14) != 0x800035A4)
            throw new InvalidDataException("Wizard collider pointer has unsupported native ownership.");
        for (int slot = 1; slot < 8; slot++)
            if (U32(bytes, 0x14 + slot * 4) != 0)
                throw new InvalidDataException("Wizard expected null collider slot contains unsupported data.");
        ReadOnlySpan<byte> shape = bytes.AsSpan(0x35A4, 16);
        if (!shape.SequenceEqual(Convert.FromHexString("DB070000010390010000000000000000")))
            throw new InvalidDataException("Wizard final animated sphere shape grammar differs from the supported record.");

        int[] offsets = [0x44, 0x100, 0x19C];
        int[] counts = [19, 15, 15];
        List<NativeGreenWizardColliderFrame> frames = [];
        for (int animation = 0; animation < offsets.Length; animation++)
        {
            uint encodedOffset = U32(bytes, 0x38 + animation * 4);
            if (encodedOffset != offsets[animation])
                throw new InvalidDataException("Wizard animation owner aliases or escapes its native extent.");
            int offset = offsets[animation];
            int count = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(offset));
            if (count != counts[animation] || offset + 0x24 + count * 8 !=
                (animation < 2 ? offsets[animation + 1] : 0x238))
                throw new InvalidDataException("Wizard animation frame count changes the exact owner boundary.");
            if (bytes[offset + 4] != 0 || bytes[offset + 5] != 1)
                throw new InvalidDataException("Wizard packed animation type/scale is unsupported.");
            for (int frame = 0; frame < count; frame++)
            {
                int field = offset + 0x24 + frame * 8;
                uint word = U32(bytes, field);
                int selector = (int)((word >> 19) & 0x1C);
                if (selector is not (0 or 4))
                    throw new InvalidDataException("Wizard frame selects an unsupported collider selector.");
                int expected = animation == 2 && frame >= 5 ? 4 : 0;
                if (selector != expected)
                    throw new InvalidDataException("Wizard collider selector schedule differs from the source profile.");
                frames.Add(new(animation, frame, selector, selector == 0, bytes[offset + 5],
                    packageWadOffset + field, word, selector == 0 ? packageWadOffset + 0x35A4 : null));
            }
        }
        return new(Hash(bytes), packageWadOffset, Hash(shape), frames.ToArray());
    }

    private static short[] ReadTrig(byte[] executable, int offset)
    {
        short[] result = new short[256];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = BinaryPrimitives.ReadInt16LittleEndian(executable.AsSpan(offset + i * 2));
            if (result[i] is < -4096 or > 4096)
                throw new InvalidDataException("Wizard trig table escapes the supported native coefficient range.");
        }
        return result;
    }

    private static readonly NativeGreenWizardNativeSpanBinding[] EvidenceSpans =
    [
        new("Mover", "EXE", 0x29B98, 0x80039398, 0x2F0, "bc780cb5dd054b3a0c97fc1b25b1a85f3f0ac5c1c0404b60cb4df1d83bb81eca"),
        new("Heading wrapper", "EXE", 0x296E0, 0x80038EE0, 0xE8, "d58397320748b47f903d597b0b66e3b56397cad4530443b65c82123e739d63b4"),
        new("Angle distance", "EXE", 0x8108, 0x80017908, 0x20, "03690bf77631968cb6e2165178bfce78cef28620e589e78e2f0faa661c850e84"),
        new("Angle step", "EXE", 0x81F0, 0x800179F0, 0x48, "719a346d07b7b2e900b2e1a4adc264d599d7e42fcf2eaa371787e078e732399e"),
        new("Arrival metric", "EXE", 0x8190, 0x80017990, 0x60, "277c87f0a049389cf5c3e0510eac8b878a516d9f773d44545184f67910815d90"),
        new("Root pointer loader", "EXE", 0x3BE0, 0x800133E0, 0x268, "030346c69253fbc971f3a5cea36550afe798a2982070e9da29b6ce8df858fc82"),
        new("Collider selection", "EXE", 0x431E4, 0x800529E4, 0x80, "ec41ef500fe7a57b343c8f0bbaf3dc6d1c96d500b2a0bbdf06092e33c784ce73"),
        new("Animated sphere branch", "EXE", 0x3F030, 0x8004E830, 0x1A8, "02e8b50f16b4ca67d32253a9dda3f918b10f43aa9615bf146c4d26111c702b6b"),
        new("Resident query evidence window", "EXE", 0x3EBC8, 0x8004E3C8, 0x7B8, "5fdc32d3509aa948b0089e4331c1b70dce6132b36067afdd8f8d1e8d1d5a5ea4"),
        new("Terrain query evidence window", "EXE", 0x3C64C, 0x8004BE4C, 0xD34, "3d19782b4ad17cffbdb5239dcdd6412a9d25bceaccdb1671a302865edb52589e"),
        new("Floor query evidence window", "EXE", 0x3DDEC, 0x8004D5EC, 0x524, "776c332d04a58b2c4eec7f03573cd60a788570decbee27965e0a7fdd18a366ab"),
        new("Cosine table", "EXE", 0x5D478, 0x8006CC78, 0x200, "7167a6246b5738b9028a66103a5732873dcf069460d08285bd30d22ecce299da"),
        new("Sine table", "EXE", 0x5D3F8, 0x8006CBF8, 0x200, "877872b39150488ce0baad5491d04ec492f0a7d4ba45248244c46a0ea548a82b"),
        new("Magic Wizard handler", "WAD", 0x2734968, 0x800883A0, 0x668, "715ddf657c49ffce419ad67f09e846708e48f4197dac4edd644d99d7437280f5"),
        new("Magic row-dispatch initialization", "WAD", 0x272A9D0, 0x8007E408, 0xA0, "2ccc7a0ac7330314448da3179d9fe0eeed3613cf9e3e22a0c0ebc204cefece2a"),
        new("Magic Wizard dispatch selector", "WAD", 0x272751C, 0x8007AF54, 4, "518770377f95138b5a19640d8b0252c488d7c535d35a1aef5f949cfabed56ab4"),
        new("Magic row-dispatch tail", "WAD", 0x273783C, 0x8008B274, 0x20, "f45a5238c44bba3c78041e3ac66820183adbf706ec4531dcb9bf3f2fac259952")
    ];

    private static uint U32(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset));
    private static ushort U16(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset));
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
