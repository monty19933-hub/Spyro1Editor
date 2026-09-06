using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Spyro.Editor.Core.Analysis;

public sealed record NativeId65EngineCallback(
    string Name, uint GlobalAddress, uint RuntimeAddress, uint SetterRuntimeAddress,
    long CodeWadOffset, int CodeByteLength, string CodeSha256, string RegisterContract);

public sealed record NativeId65LocalCompanionRegistration(
    int LevelId, int Slot, int RootDataOffset, int PackageByteLength, string HeaderSha256);

public sealed record NativeId65EngineEvidence(
    string Name, string Domain, long Offset, int ByteLength, string Sha256);

/// <summary>
/// Exact or detached class120 package prerequisites, not a complete actor,
/// texture-pixel, sound-bank, particle or auxiliary-state closure.
/// </summary>
public sealed class NativeId65CompanionRequirements
{
    private readonly byte[] _package;

    internal NativeId65CompanionRequirements(ReadOnlySpan<byte> package,
        NativeMobyModelTextureLayout textures, bool sourceCertified)
    {
        _package = package.ToArray();
        PackageSha256 = NativeId65EngineProfileDecoder.Hash(package);
        SourceCertified = sourceCertified;
        TextureReferences = Array.AsReadOnly(textures.References.ToArray());
    }

    public int ActorClass => 0x78;
    public uint ModelPointerGlobal => 0x80076558;
    public bool SourceCertified { get; }
    public long? PackageWadOffset => SourceCertified ? 0x1367C7C : null;
    public int PackageByteLength => _package.Length;
    public string PackageSha256 { get; }
    public IReadOnlyList<int> AnimationIds { get; } = Array.AsReadOnly(new[] { 0, 1, 2, 3, 4, 5 });
    public IReadOnlyList<byte> LocalSoundDescriptorIndices { get; } = Array.AsReadOnly(new byte[] { 0x48, 0x49 });
    public IReadOnlyList<int> RequiredParticleSelectors { get; } = Array.AsReadOnly(new[] { 0x0C, 0x42 });
    public IReadOnlyList<NativeMobyModelTextureReference> TextureReferences { get; }
    public int DynamicRowByteLength => 0x58;
    public int PrivatePropertiesByteLength => 0x18;
    public bool CompleteDependencyClosureProven => false;
    public bool PhysicsEnvelopeProven => false;
    public byte[] CopyPackage() => _package.ToArray();
}

internal sealed class NativeId65DetachedEngineInitializer
{
    internal NativeId65DetachedEngineInitializer(uint copy, IEnumerable<NativeId65EngineCallback> callbacks)
    {
        CopyBufferAddress = copy;
        Callbacks = Array.AsReadOnly(callbacks.ToArray());
    }

    public uint CopyBufferAddress { get; }
    public IReadOnlyList<NativeId65EngineCallback> Callbacks { get; }
    public bool SourceCertified => false;
}

/// <summary>
/// Bounded source-free structural kernel. These methods never issue a native
/// image certificate and do not authorize callback replacement or emission.
/// </summary>
internal static class NativeId65EngineProfileDecoder
{
    internal const string TownDataSha256 = "7ddbf6d9a6ee6c0f64c8564a89e374ca0ca234ef608ec812aae68e8176de1dc0";
    internal const string CompanionSha256 = "8e0d1cf58e8b164734f4aa3e91a60a1d04e78bb1f5e4cd5c4432b2e3e1e743f1";
    private const uint OverlayBase = 0x8007AA38;
    private const long OverlayWadOffset = 0x118E800;

    private static readonly NativeId65EngineCallback[] ExpectedCallbacks =
    [
        Callback("moby-spawn", 0x800758CC, 0x800872A4, 0x8005A8B4, 0xB7C,
            "3c8dcb5a5a28fd197ad04c9512fbe00519bee1097d62dd27d4461931f53bb225",
            "a0=class; a1=nullable source Moby; v0=allocated Moby"),
        Callback("active-moby-and-animation-tick", 0x80075734, 0x8007DA54, 0x8005A8C4, 0x9850,
            "5393ee600c2c721c382bb6d727de1daa835eb79b5851492cf5c285bac085028d",
            "global active rows and animation lists; cited callers ignore result"),
        Callback("particle-spawn", 0x800758E4, 0x80088F68, 0x8005A8D4, 0xF64,
            "0f41d76490950c9ee0b2c3be847ff9b3f360b4852ce674e7acc04120c975c931",
            "a0=signed count; a1=selector; a2/a3=selector-specific context"),
        Callback("particle-tick", 0x800756BC, 0x80087E20, 0x8005A8E4, 0x1148,
            "1279e327bdbf92e03c991d619cf3190f3015bb63493f13855355588c0ba78cdc",
            "a0=tick delta from g800756CC; native pool sentinel is not a permanent no-op"),
        Callback("balloon-transition-begin", 0x800757A0, 0x8007AF2C, 0x8005A8F4, 0x13C,
            "17705e21b23be58f500b9a55f824d1410bdcd867b41a5635b831049b63bb93aa",
            "a0=balloonist Moby with native linked properties; enters state12"),
        Callback("balloon-transition-end", 0x800758A8, 0x8007B068, 0x8005A904, 0x44,
            "5b7429164bf8659a394b123cd10c5017d67fb34a61b09f4d40adb2e0b2a2a6a4",
            "global transition reset through shared8002D02C"),
        Callback("balloon-transition-tick", 0x8007574C, 0x8007B0AC, 0x8005A914, 0x1FA0,
            "fe0afaefaa7187f50115c708006625c9c055ea44448a63a960546c5ab801bb08",
            "global state12 update; no independently certified empty-scene replacement"),
        Callback("balloon-transition-render", 0x800758D8, 0x8007D04C, 0x8005A924, 0xA08,
            "b5014d6eabf2e9313517398614f2bc5df35ad299ab16adc7198c0dc924b47dde",
            "global state12 render; no independently certified empty-scene replacement")
    ];

    internal static NativeId65DetachedEngineInitializer DecodeInitializer(
        ReadOnlySpan<byte> initializer, ReadOnlySpan<byte> tail)
    {
        if (initializer.Length != 0x98 || tail.Length != 0x18)
            throw new InvalidDataException("Native initializer requires all nine setters, outgoing jump and complete tail.");
        ReadOnlySpan<uint> expectedTail =
            [0x3C028008, 0x8C4285D8, 0x3C018008, 0xAC2285DC, 0x03E00008, 0];
        for (int i = 0; i < expectedTail.Length; i++)
        {
            if (Word(tail, i * 4) != expectedTail[i])
                throw new InvalidDataException("Native common tail no longer copies g785D8 to g785DC exactly.");
        }

        for (int i = 0; i < 9; i++)
        {
            uint global = i == 0 ? 0x800785D8 : ExpectedCallbacks[i - 1].GlobalAddress;
            uint value = i == 0 ? 0x80089ECC : ExpectedCallbacks[i - 1].RuntimeAddress;
            int offset = i * 16;
            // ADDIU sign-extends the low half. Require the native instruction
            // encoding as well as the resulting address, not a caller label.
            uint expectedValueHigh = 0x3C020000 | ((value + 0x8000) >> 16);
            uint expectedGlobalHigh = 0x3C010000 | ((global + 0x8000) >> 16);
            if (Word(initializer, offset) != expectedValueHigh ||
                Word(initializer, offset + 4) != (0x24420000 | (value & 0xFFFF)) ||
                Word(initializer, offset + 8) != expectedGlobalHigh ||
                Word(initializer, offset + 12) != (0xAC220000 | (global & 0xFFFF)))
                throw new InvalidDataException("Native callback/global setter is missing, reordered, aliased or unsupported.");
        }
        if (Word(initializer, 0x90) != 0x08016DB8 || Word(initializer, 0x94) != 0)
            throw new InvalidDataException("Native initializer outgoing jump or delay slot changed.");
        return new(0x80089ECC, ExpectedCallbacks);
    }

    internal static NativeId65CompanionRequirements DecodeCompanion(ReadOnlySpan<byte> package) =>
        DecodeCompanionCore(package, false);

    internal static NativeId65CompanionRequirements CaptureExactCompanion(ReadOnlySpan<byte> completeData)
    {
        if (completeData.Length != 0x2E2000 || Hash(completeData) != TownDataSha256)
            throw new InvalidDataException("Companion source requires the complete exact Town data owner.");
        var registration = DecodeLocalRegistration(13, completeData[..0x800], completeData.Length);
        if (registration.Slot != 20 || registration.RootDataOffset != 0x1C9C7C ||
            registration.PackageByteLength != 0x2930)
            throw new InvalidDataException("Source class120 interval is not the complete native slot20 owner.");
        var package = completeData.Slice(registration.RootDataOffset, registration.PackageByteLength);
        if (Hash(package) != CompanionSha256)
            throw new InvalidDataException("Source class120 package changed.");
        var result = DecodeCompanionCore(package, true);
        if (result.TextureReferences.Count != 36 ||
            result.TextureReferences.Any(t => t.Page != 0x1D || t.Palette != 0x7A36))
            throw new InvalidDataException("Source companion renderer texture consumer set changed.");
        return result;
    }

    private static NativeId65CompanionRequirements DecodeCompanionCore(ReadOnlySpan<byte> package, bool source)
    {
        if (package.Length != 0x2930 || Word(package, 0) != 6 ||
            package[4] != 0x48 || package[5] != 0x49 ||
            package.Slice(6, 14).ContainsAnyExcept((byte)0xFF) ||
            package.Slice(0x14, 0x20).ContainsAnyExcept((byte)0) ||
            Word(package, 0x34) != 0x3F8)
            throw new InvalidDataException("Companion package requires its six animations, native local sound descriptor bytes and exact owner grammar.");
        for (int animation = 0; animation < 6; animation++)
        {
            int root = 0x50 + animation * 0x9C;
            if (Word(package, 0x38 + animation * 4) != root ||
                BinaryPrimitives.ReadInt16LittleEndian(package[root..]) != 15 || package[root + 4] != 0)
                throw new InvalidDataException("Companion animation owner or paired-frame encoding changed.");
            for (int frame = 0; frame < 15; frame++)
            {
                uint word = Word(package, root + 0x24 + frame * 8);
                long relative = word & 0x1FFFFF;
                if (relative % 4 != 0 || relative + 0x3F8 + 4 > package.Length)
                    throw new InvalidDataException("Companion frame pointer exceeds its complete package owner.");
            }
        }
        // The existing bounded grammar protects frame-header/face-table
        // ownership and distinguishes texture bytes from native pointers.
        var textures = NativeMobyModelTextureLayout.Decode(package);
        return new(package, textures, source);
    }

    internal static NativeId65LocalCompanionRegistration DecodeLocalRegistration(
        int levelId, ReadOnlySpan<byte> header, int completeDataLength)
    {
        if (levelId < 10 || levelId > 64 || levelId % 10 > 5 ||
            header.Length != 0x800 || completeDataLength < 0x2800 ||
            completeDataLength > 0x800000 || completeDataLength % 0x800 != 0)
            throw new InvalidDataException("Companion registration requires a bounded retail level identity/header.");
        int cursor = 0x800;
        bool descriptorEnded = false;
        for (int i = 0; i < 10; i++)
        {
            uint offset = Word(header, i * 8), length = Word(header, i * 8 + 4);
            if (offset == 0 && length == 0)
            {
                descriptorEnded = true;
                continue;
            }
            if (descriptorEnded || offset != cursor || length == 0 || length % 0x800 != 0 ||
                offset + (long)length > completeDataLength)
                throw new InvalidDataException("Companion level descriptors do not own a packed native data interval.");
            cursor = checked(cursor + (int)length);
        }
        int model = checked((int)Word(header, 0x10));
        int modelLength = checked((int)Word(header, 0x14));
        if (cursor != completeDataLength || model < 0x800 || modelLength == 0 ||
            model + (long)modelLength != Word(header, 0x18) ||
            header.Slice(0x1D0).ContainsAnyExcept((byte)0))
            throw new InvalidDataException("Companion model owner or native header tail changed.");
        var roots = new List<(int Slot, int Class, int Root)>();
        var classes = new HashSet<ushort>();
        bool ended = false;
        uint previous = 0;
        for (int i = 0; i < 64; i++)
        {
            uint root = Word(header, 0x50 + i * 4);
            ushort actor = BinaryPrimitives.ReadUInt16LittleEndian(header[(0x150 + i * 2)..]);
            if (root == 0)
            {
                if (i == 0 || actor != 0)
                    throw new InvalidDataException("Special player root or model terminator is missing.");
                ended = true;
                continue;
            }
            if (ended || (i == 0 ? actor != 0 : actor == 0) || actor >= 512 || !classes.Add(actor) ||
                root <= previous || root % 4 != 0 || root < model || root + 4L > model + (long)modelLength)
                throw new InvalidDataException("Native local class registration is duplicated, aliased or outside its model owner.");
            roots.Add((i, actor, (int)root));
            previous = root;
        }
        int index = roots.FindIndex(r => r.Class == 0x78);
        if (index < 0)
            throw new InvalidDataException("Required class120 is missing locally; PETE does not supply it.");
        var selected = roots[index];
        int end = index + 1 < roots.Count ? roots[index + 1].Root : checked(model + modelLength);
        return new(levelId, selected.Slot, selected.Root, end - selected.Root, Hash(header));
    }

    private static NativeId65EngineCallback Callback(string name, uint global, uint runtime,
        uint setter, int length, string hash, string signature) =>
        new(name, global, runtime, setter, OverlayWadOffset + runtime - OverlayBase, length, hash, signature);

    internal static uint Word(ReadOnlySpan<byte> bytes, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);

    internal static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
