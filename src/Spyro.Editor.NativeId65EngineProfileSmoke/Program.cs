using System.Buffers.Binary;
using Spyro.Editor.Core.Analysis;

if (args.Length > 1)
    throw new ArgumentException("Usage: NativeId65EngineProfileSmoke [original.bin]");

int checks = 0;
Require(typeof(NativeId65EngineProfileContract).GetConstructors().Length == 0,
    "no public caller-metadata source issuer");
Require(typeof(NativeId65CompanionRequirements).GetConstructors().Length == 0,
    "no public caller-metadata companion certificate");
string invalidImage = Path.Combine(Path.GetTempPath(), "spyro-id65-engine-invalid-" + Guid.NewGuid().ToString("N") + ".bin");
bool fixtureCreated = false;
try
{
    await using (FileStream fixture = new(invalidImage, FileMode.CreateNew, FileAccess.Write, FileShare.None))
    {
        fixtureCreated = true;
        fixture.WriteByte(0);
    }
    try
    {
        _ = await NativeId65EngineProfileContract.CaptureAsync(invalidImage);
        throw new InvalidOperationException("Invalid native source fixture was accepted.");
    }
    catch (InvalidDataException)
    {
        checks++;
    }
}
finally
{
    if (fixtureCreated)
        File.Delete(invalidImage);
}
byte[] initializer = Initializer();
byte[] tail = Convert.FromHexString("0880023CD885428C0880013CDC8522AC0800E00300000000");
var decoded = NativeId65EngineProfileDecoder.DecodeInitializer(initializer, tail);
Require(decoded.CopyBufferAddress == 0x80089ECC, "native copy buffer follows complete Town code");
Require(decoded.Callbacks.Count == 8, "all eight callback owners are explicit");
Require(decoded.Callbacks.Select(c => c.GlobalAddress).Distinct().Count() == 8, "unique globals");
Require(decoded.Callbacks.Select(c => c.RuntimeAddress).SequenceEqual(new uint[]
{
    0x800872A4, 0x8007DA54, 0x80088F68, 0x80087E20,
    0x8007AF2C, 0x8007B068, 0x8007B0AC, 0x8007D04C
}), "signed low-half initialization");
Require(!decoded.SourceCertified, "detached parser cannot issue source authority");
for (int word = 0; word < initializer.Length / 4; word++)
{
    byte[] damaged = initializer.ToArray();
    damaged[word * 4] ^= 1;
    Reject(() => NativeId65EngineProfileDecoder.DecodeInitializer(damaged, tail), "every initializer instruction bound");
}
for (int word = 0; word < tail.Length / 4; word++)
{
    byte[] damaged = tail.ToArray();
    damaged[word * 4] ^= 1;
    Reject(() => NativeId65EngineProfileDecoder.DecodeInitializer(initializer, damaged), "complete default tail bound");
}
Reject(() => NativeId65EngineProfileDecoder.DecodeInitializer(initializer[..^4], tail), "missing callback tail");
Reject(() => NativeId65EngineProfileDecoder.DecodeInitializer(initializer, tail[..^4]), "missing delay slot");
byte[] reordered = initializer.ToArray();
initializer.AsSpan(16, 16).CopyTo(reordered.AsSpan(32));
initializer.AsSpan(32, 16).CopyTo(reordered.AsSpan(16));
Reject(() => NativeId65EngineProfileDecoder.DecodeInitializer(reordered, tail), "setter order is native behavior");
byte[] duplicate = initializer.ToArray();
initializer.AsSpan(16, 16).CopyTo(duplicate.AsSpan(32));
Reject(() => NativeId65EngineProfileDecoder.DecodeInitializer(duplicate, tail), "duplicate global cannot replace missing callback");

byte[] companion = CompanionFixture();
var requirements = NativeId65EngineProfileDecoder.DecodeCompanion(companion);
Require(requirements.AnimationIds.SequenceEqual(Enumerable.Range(0, 6)), "six animation states retained");
Require(requirements.LocalSoundDescriptorIndices.SequenceEqual(new byte[] { 0x48, 0x49 }), "model-local descriptor indices are not global selector keys");
Require(!requirements.SourceCertified && !requirements.CompleteDependencyClosureProven, "detached package is not an actor closure");
Require(requirements.RequiredParticleSelectors.SequenceEqual(new[] { 0x0C, 0x42 }), "companion native particle prerequisites");
Require(requirements.TextureReferences.Count == 0, "fixture owns an empty face table, not source texture evidence");
Require(requirements.CopyPackage().SequenceEqual(companion), "detached package exact readback");
companion[0x800] = 1;
Require(requirements.CopyPackage()[0x800] == 0, "input buffer detached");
byte[] returned = requirements.CopyPackage();
returned[0] = 0;
Require(requirements.CopyPackage()[0] == 6, "output buffer detached");
foreach (int offset in new[] { 0, 4, 5, 6, 0x14, 0x30, 0x34, 0x38, 0x4C })
{
    byte[] damaged = CompanionFixture();
    damaged[offset] ^= 1;
    Reject(() => NativeId65EngineProfileDecoder.DecodeCompanion(damaged), "companion header contract");
}
for (int animation = 0; animation < 6; animation++)
{
    int root = 0x50 + animation * 0x9C;
    foreach (int offset in new[] { root, root + 4, root + 0x14, root + 0x24 })
    {
        byte[] damaged = CompanionFixture();
        if (offset == root + 0x24)
            U32(damaged, offset, 0x001FFFFF);
        else
            damaged[offset] ^= 1;
        Reject(() => NativeId65EngineProfileDecoder.DecodeCompanion(damaged), "animation frame/table ownership");
    }
}
Reject(() => NativeId65EngineProfileDecoder.DecodeCompanion(CompanionFixture()[..^1]), "truncated package");

byte[] header = HeaderFixture();
var registration = NativeId65EngineProfileDecoder.DecodeLocalRegistration(13, header, 0x5000);
Require(registration.Slot == 1 && registration.RootDataOffset == 0x1900 && registration.PackageByteLength == 0x2F00,
    "local root owner comes from native header boundaries");
foreach (int badLevel in new[] { 9, 16, 19, 65, 66, 100, int.MaxValue })
    Reject(() => NativeId65EngineProfileDecoder.DecodeLocalRegistration(badLevel, header, 0x5000), "no decimal slot alias or ID65 promotion");
foreach (int offset in new[] { 0, 4, 0x10, 0x14, 0x18, 0x50, 0x54, 0x152 })
{
    byte[] damaged = header.ToArray();
    damaged[offset] ^= 1;
    Reject(() => NativeId65EngineProfileDecoder.DecodeLocalRegistration(13, damaged, 0x5000), "native registration owner mutation");
}
byte[] missing = header.ToArray();
U32(missing, 0x54, 0);
BinaryPrimitives.WriteUInt16LittleEndian(missing.AsSpan(0x152), 0);
Reject(() => NativeId65EngineProfileDecoder.DecodeLocalRegistration(13, missing, 0x5000), "PETE cannot replace missing local companion");

if (args.Length == 1)
{
    var source = await NativeId65EngineProfileContract.CaptureAsync(args[0]);
    Require(source.SourceCertified, "source issuer binds actual original image");
    Require(source.Callbacks.Count == 8, "source complete callbacks");
    Require(source.RequiredCompanion.SourceCertified, "source companion owner bound");
    Require(source.RequiredCompanion.PackageWadOffset == 0x1367C7C, "native local package owner");
    Require(source.RequiredCompanion.PackageByteLength == 0x2930, "complete next-root interval");
    Require(source.RequiredCompanion.TextureReferences.Count == 36, "native renderer texture consumers");
    Require(source.RequiredCompanion.TextureReferences.All(t => t.Page == 0x1D && t.Palette == 0x7A36), "actual destination texture references");
    Require(source.LocalCompanionRegistrations.Count == 35, "all retail headers independently require local class120");
    Require(!source.SharedPeteClasses.Contains((ushort)0x78), "PETE is not class120 authority");
    Require(!source.IndependentLevelSupported && !source.NoOpCallbacksAuthorized &&
        !source.PublicationApproved && !source.CompleteNativeDependencyClosureProven, "no existing gate promotion");
    Require(source.PendingPrerequisites.Count >= 5, "unclosed texture/sound/effect/arrival/data gates visible");
    byte[] executable = source.CopyExecutable();
    byte[] overlay = source.CopyOverlay();
    byte[] data = source.CopyTownData();
    source.RequireCapturedDomains(executable, overlay, data);
    Require(true, "complete captured domain readback");
    foreach (var callback in source.Callbacks)
    {
        byte[] corrupt = overlay.ToArray();
        corrupt[checked((int)(callback.RuntimeAddress - 0x8007AA38))] ^= 1;
        Reject(() => source.RequireCapturedDomains(executable, corrupt, data), "every callback body protected");
    }
    foreach (int offset in new[] { 0xA0, 0x178, 0x1C9C7C, 0x1CC5AB, 0x500 })
    {
        byte[] corrupt = data.ToArray();
        corrupt[offset] ^= 1;
        Reject(() => source.RequireCapturedDomains(executable, overlay, corrupt), "model owner and whole data complement protected");
    }
    foreach (int offset in new[] { 0x1CA8, 0x4B098, 0x4BEE0, 0x47910, 0x5F828, 0x5FA00, 0x49D94 })
    {
        byte[] corrupt = executable.ToArray();
        corrupt[offset] ^= 1;
        Reject(() => source.RequireCapturedDomains(corrupt, overlay, data), "source identity not patched candidate authority");
    }
    executable[0] ^= 1;
    Require(source.CopyExecutable()[0] != executable[0], "source snapshot detached");
    Console.WriteLine($"NATIVE_ID65_ENGINE_PROFILE_SOURCE_OK checks={checks} contract={source.ContractSha256}");
}
else
    Console.WriteLine($"NATIVE_ID65_ENGINE_PROFILE_SOURCE_FREE_OK checks={checks}");

byte[] Initializer()
{
    var values = new (uint Global, uint Value)[]
    {
        (0x800785D8, 0x80089ECC), (0x800758CC, 0x800872A4),
        (0x80075734, 0x8007DA54), (0x800758E4, 0x80088F68),
        (0x800756BC, 0x80087E20), (0x800757A0, 0x8007AF2C),
        (0x800758A8, 0x8007B068), (0x8007574C, 0x8007B0AC),
        (0x800758D8, 0x8007D04C)
    };
    byte[] bytes = new byte[0x98];
    for (int i = 0; i < values.Length; i++)
    {
        var (global, value) = values[i];
        U32(bytes, i * 16, 0x3C020000 | ((value + 0x8000) >> 16));
        U32(bytes, i * 16 + 4, 0x24420000 | (value & 0xFFFF));
        U32(bytes, i * 16 + 8, 0x3C010000 | ((global + 0x8000) >> 16));
        U32(bytes, i * 16 + 12, 0xAC220000 | (global & 0xFFFF));
    }
    U32(bytes, 0x90, 0x08016DB8);
    return bytes;
}

byte[] CompanionFixture()
{
    byte[] bytes = new byte[0x2930];
    U32(bytes, 0, 6);
    bytes.AsSpan(4, 16).Fill(0xFF);
    bytes[4] = 0x48;
    bytes[5] = 0x49;
    U32(bytes, 0x34, 0x3F8);
    for (int animation = 0; animation < 6; animation++)
    {
        int root = 0x50 + animation * 0x9C;
        U32(bytes, 0x38 + animation * 4, (uint)root);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(root), 15);
        bytes[root + 0xC] = 0x20;
        for (int frame = 0; frame < 15; frame++)
            U32(bytes, root + 0x24 + frame * 8, 4);
    }
    return bytes;
}

byte[] HeaderFixture()
{
    byte[] bytes = new byte[0x800];
    foreach (var (index, offset, length) in new[] { (0, 0x800, 0x800), (1, 0x1000, 0x800), (2, 0x1800, 0x3000), (3, 0x4800, 0x800) })
    {
        U32(bytes, index * 8, (uint)offset);
        U32(bytes, index * 8 + 4, (uint)length);
    }
    U32(bytes, 0x50, 0x1800);
    U32(bytes, 0x54, 0x1900);
    BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x152), 0x78);
    return bytes;
}

void Require(bool condition, string label)
{
    if (!condition)
        throw new InvalidDataException("FAILED: " + label);
    checks++;
}
void Reject(Action action, string label)
{
    try { action(); }
    catch (InvalidDataException) { checks++; return; }
    throw new InvalidDataException("FAILED to reject: " + label);
}
void U32(byte[] bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
