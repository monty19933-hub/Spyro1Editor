using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;

if (args.Length > 1)
    throw new ArgumentException("Usage: NativeId65CompanionAssetSmoke [original.bin]");

int checks = 0;
byte[] package = Package();
byte[] pixels = Enumerable.Range(0, 512).Select(i => (byte)i).ToArray();
byte[] palette = Enumerable.Range(0, 32).Select(i => (byte)(i + 16)).ToArray();
var bank = Sound();
var block = NativeId65CompanionAssetLayout.Compile(package, pixels, palette, bank);
Require(!block.SourceCertified, "neutral asset compiler cannot issue original-image authority");
Require(block.Owners.Count == 5, "five distinct native resource owners");
Require(block.References.Count == 76, "36 CLUT/page pairs plus two local selectors and sample offsets");
Require(block.VramPlacements.Count == 33, "32 strided pixel rows plus independently owned CLUT");
Require(block.LocalSoundDescriptorCount == 2 && block.CopySampleBank().Length == 32, "two actual samples materialized");
Require(block.CopyLocalDescriptors().Length == 40, "native twenty-byte descriptors, not map entries");
Require(Word(block.CopyLocalDescriptors(), 0) == 0 && Word(block.CopyLocalDescriptors(), 20) == 16,
    "sample pointers are relative to emitted local bank");
Require(block.CopySampleBank().SequenceEqual(bank.Descriptors[72].Sample.CopyBytes().Concat(bank.Descriptors[73].Sample.CopyBytes())),
    "local indices72/73 are used directly, not global mapping72/73");
Require(block.CopyRelocatedPackage()[4] == 0 && block.CopyRelocatedPackage()[5] == 1,
    "source local slots rebound only into detached two-descriptor namespace");
Require(block.CopyRelocatedPackage().AsSpan(6, 14).ContainsAnyExcept((byte)255) == false,
    "all other unbound slots preserved");
Require(!block.CompleteCompanionClosureProven && !block.CompleteLevelVramProven && !block.PublicationApproved,
    "asset materialization is not an actor, VRAM or publication approval");
Require(block.CopyAssetBlock().Length == 0x2B98, "complete registered package plus actual compact resource bytes");
byte[] inverse = block.CopyRelocatedPackage();
foreach (var reference in NativeMobyModelTextureLayout.Decode(Package()).References)
{
    byte[] expected = reference.CopyBytes();
    foreach (int at in new[] { 0, 4, 8, 10 })
    {
        expected[at] -= 64;
        expected[at + 1] -= 96;
    }
    BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(2), 33);
    BinaryPrimitives.WriteUInt16LittleEndian(expected.AsSpan(6), 8);
    Require(inverse.AsSpan(reference.PackageOffset, 12).SequenceEqual(expected), "independent native UV/page/CLUT arithmetic");
    reference.CopyBytes().CopyTo(inverse, reference.PackageOffset);
}
inverse[4] = 72;
inverse[5] = 73;
Require(inverse.SequenceEqual(Package()), "every animation/frame/model/padding byte outside declared changes survives");
foreach (var owner in block.Owners)
    Require(Hash(block.CopyAssetBlock().AsSpan(owner.BlockOffset, owner.ByteLength).ToArray()) == owner.Sha256,
        "independent owner byte hash");
Require(block.Owners.Select(o => o.BlockOffset).SequenceEqual(new[] { 0, 0x2930, 0x2B30, 0x2B50, 0x2B78 }),
    "compact owner domains are nonoverlapping and gap-free");
Require(block.VramPlacements.Take(32).Select(p => (p.PayloadRelativeOffset, p.VramRelativeOffset, p.ByteLength))
    .SequenceEqual(Enumerable.Range(0, 32).Select(i => (i * 16, i * 1024, 16))) &&
    block.VramPlacements[^1] == new NativeId65CompanionVramPlacement("clut", 0, 32, 32),
    "independent indexed pixel rows and CLUT placement oracle");
block.RequireExactBlock(block.CopyAssetBlock());
Require(true, "exact independent block readback");
var repeat = NativeId65CompanionAssetLayout.Compile(Package(), pixels, palette, Sound());
Require(repeat.BlockSha256 == block.BlockSha256, "deterministic repeated materialization");
byte[] returned = block.CopyAssetBlock();
returned[0] ^= 1;
Require(block.CopyAssetBlock()[0] != returned[0], "returned block buffer detached");
package[0] = 0;
pixels[0] ^= 1;
palette[0] ^= 1;
Require(block.CopyRelocatedPackage()[0] == 6 && block.CopyPixelPayload()[0] == 0 && block.CopyPalettePayload()[0] == 16,
    "all producer inputs snapshotted");
foreach (var owner in block.Owners)
{
    byte[] corrupt = block.CopyAssetBlock();
    corrupt[owner.BlockOffset] ^= 1;
    Reject(() => block.RequireExactBlock(corrupt), "each resource owner corruption rejected");
}
Reject(() => block.RequireExactBlock(block.CopyAssetBlock()[..^1]), "truncated owner refused");
Reject(() => block.RequireExactBlock(block.CopyAssetBlock().Concat(new byte[4]).ToArray()), "extra unowned bytes refused");
Reject(() => NativeId65CompanionAssetLayout.Compile(Package()[..^4], new byte[512], new byte[32], Sound()), "registered padding cannot be dropped");
Reject(() => NativeId65CompanionAssetLayout.Compile(Package(), new byte[511], new byte[32], Sound()), "truncated pixels refused");
Reject(() => NativeId65CompanionAssetLayout.Compile(Package(), new byte[512], new byte[31], Sound()), "truncated CLUT refused");
foreach (int offset in new[] { 0, 4, 5, 6, 0x14, 0x30, 0x34, 0x38, 0x50, 0x54 })
{
    byte[] corrupt = Package();
    corrupt[offset] ^= 1;
    Reject(() => NativeId65CompanionAssetLayout.Compile(corrupt, new byte[512], new byte[32], Sound()), "model grammar/preimage mutation");
}
byte[] changedUv = Package();
changedUv[0x408] = 63;
Reject(() => NativeId65CompanionAssetLayout.Compile(changedUv, new byte[512], new byte[32], Sound()), "texture footprint outside captured rectangle");
byte[] changedPalette = Package();
changedPalette[0x406] ^= 1;
Reject(() => NativeId65CompanionAssetLayout.Compile(changedPalette, new byte[512], new byte[32], Sound()), "foreign source palette refused");
Reject(() => NativeId65CompanionAssetLayout.Compile(Package(), new byte[512], new byte[32], Sound(loop: true)), "looping source cannot inherit one-shot profile");
Reject(() => NativeId65CompanionAssetLayout.Compile(Package(), new byte[512], new byte[32], Sound(missing: true)), "missing local descriptor73 refused");
Reject(() => NativeId65CompanionAssetLayout.Compile(Package(), new byte[512], new byte[32], Sound(unknown: true)), "unknown sound parameter refused");
Reject(() => NativeId65CompanionAssetLayout.Compile(Package(), new byte[512], new byte[32], Sound(oversized: true)), "sample materialization budget checked before output allocation");
Require(typeof(NativeId65CompanionAssetPlan).GetConstructors().Length == 0, "no caller-metadata source issuer");

if (args.Length == 1)
{
    var source = await NativeId65CompanionAssetPlan.CaptureAsync(args[0]);
    var native = source.CompileAssetBlock();
    Require(native.SourceCertified && source.SourceImageSha256 == NativeId65EngineProfileContract.OriginalImageSha256,
        "exact native public source issuer");
    Require(native.CopyAssetBlock().Length == 0x5168 && native.CopySampleBank().Length == 0x25F0,
        "exact complete source-native materialized lengths");
    Require(native.BlockSha256 == "0a7bcb56eaedded6878ae739aae17d8cb3857eaf74edec52e6f8c01a1667fb2a",
        "independent Python native byte-splice oracle golden");
    Require(Hash(native.CopyPixelPayload()) == "929c6ec41e8be9595ccf1ee140fb67c3f08be94cb65744c16231e56996124d6a", "source pixel golden");
    Require(Hash(native.CopyPalettePayload()) == "d76c613b9fe73bf5232c70143c622a61b3d7ba02c5a59b07692765182bb86656", "source CLUT golden");
    Require(native.SoundSources.Select(s => s.OriginalDescriptorIndex).SequenceEqual(new[] { 72, 73 }), "source local namespace explicit");
    Require(native.SoundSources.Select(s => s.SampleByteLength).SequenceEqual(new[] { 0xC60, 0x1990 }), "complete terminal ADPCM frames included");
    Require(native.SoundSources.Select(s => s.SampleSha256).SequenceEqual(new[]
    {
        "78dde2c2a84b201d248a8e28afaaab83efc8cb615ab6d03d9c80c8a98edaa75f",
        "cf2648e50743f07a2117891bc1fc2f1b8289070109a1e11ad1a0c0ed3cf1b4b3"
    }), "exact two sample source goldens");
    Require(source.Particles.Requirements.Select(p => p.Selector).SequenceEqual(new[] { 0x0C, 0x42 }), "both actual companion emissions required");
    Require(source.Particles.Requirements.Select(p => p.AllocationKind).SequenceEqual(new[] { 2, 0 }), "particle allocation kind is not texture ID");
    Require(source.Particles.SourceCertified && !source.Particles.TransitiveClosureProven && source.Particles.PendingRequirements.Count >= 3,
        "exact spawn/tick code is not complete render/state closure");
    Require(!source.PublicationApproved && !source.IndependentLevelSupported && !source.RuntimeVerified,
        "native asset production does not promote ID65");
    Require(native.BlockSha256 == source.CompileAssetBlock().BlockSha256, "native repeated compilation stable");
    foreach (var owner in native.Owners)
    {
        byte[] corrupt = native.CopyAssetBlock();
        corrupt[owner.BlockOffset + owner.ByteLength - 1] ^= 1;
        Reject(() => native.RequireExactBlock(corrupt), "last byte of complete source resource owner protected");
    }
    byte[] exe = source.EngineProfile.CopyExecutable();
    byte[] overlay = source.EngineProfile.CopyOverlay();
    foreach (uint address in new uint[] { 0x8007ACB0, 0x8007AD88, 0x8007ADF4, 0x8007AECC, 0x800881E8, 0x80088510, 0x800892C0, 0x8008962C })
    {
        byte[] corrupt = overlay.ToArray();
        corrupt[checked((int)(address - 0x8007AA38))] ^= 1;
        Reject(() => NativeId65CompanionParticleContract.Capture(exe, corrupt), "source particle table or actual case owner corruption");
    }
    Console.WriteLine($"NATIVE_ID65_COMPANION_ASSET_SOURCE_OK checks={checks} block={native.BlockSha256} plan={source.PlanSha256} bytes={native.CopyAssetBlock().Length} closure=false publication=false");
}
else
    Console.WriteLine($"NATIVE_ID65_COMPANION_ASSET_SOURCE_FREE_OK checks={checks}");

byte[] Package()
{
    byte[] b = new byte[0x2930];
    U32(b, 0, 6);
    b.AsSpan(4, 16).Fill(255);
    b[4] = 72;
    b[5] = 73;
    U32(b, 0x34, 0x3F8);
    for (int animation = 0; animation < 6; animation++)
    {
        int root = 0x50 + animation * 0x9C;
        U32(b, 0x38 + animation * 4, (uint)root);
        BinaryPrimitives.WriteInt16LittleEndian(b.AsSpan(root), 15);
        for (int frame = 0; frame < 15; frame++)
            U32(b, root + 0x24 + frame * 8, 4);
    }
    U32(b, 0x3F8, 36 * 20);
    for (int i = 0; i < 36; i++)
    {
        int offset = 0x3FC + i * 20;
        U32(b, offset, 0x80000004);
        new byte[] { 64, 96, 0x36, 0x7A, 95, 96, 0x1D, 0, 64, 127, 95, 127 }.CopyTo(b, offset + 8);
    }
    return b;
}

NativeSoundBank Sound(bool loop = false, bool missing = false, bool unknown = false, bool oversized = false)
{
    int count = missing ? 73 : 74;
    byte[] component = new byte[0x108 + count * 20];
    U32(component, 0, (uint)component.Length);
    U32(component, 0x104, (uint)count);
    component[4 + 72] = 1; // deliberate global/local namespace trap
    component[4 + 73] = 1;
    int thirdOffset = oversized ? 0x10020 : 32;
    byte[] samples = new byte[thirdOffset + 16];
    foreach (int offset in new[] { 0, thirdOffset - 16, thirdOffset })
    {
        samples[offset + 1] = 1;
        samples[offset + 2] = (byte)(offset == 0 ? 1 : offset == thirdOffset ? 3 : 2);
    }
    if (loop)
        samples[17] = 7;
    for (int i = 0; i < count; i++)
    {
        int at = 0x108 + i * 20;
        U32(component, at, (uint)(i == 72 ? 16 : i == 73 ? thirdOffset : 0));
        BinaryPrimitives.WriteUInt16LittleEndian(component.AsSpan(at + 10), 0x400);
        if (unknown && i == 72)
            U32(component, at + 4, 1);
    }
    return NativeSoundBankCodec.Decode(component, samples);
}

static uint Word(byte[] bytes, int at) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at));
static void U32(byte[] bytes, int at, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at), value);
static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
void Require(bool ok, string label)
{
    if (!ok)
        throw new InvalidOperationException(label);
    checks++;
}
void Reject(Action action, string label)
{
    try
    {
        action();
    }
    catch (InvalidDataException)
    {
        checks++;
        return;
    }
    throw new InvalidOperationException("Expected refusal: " + label);
}
