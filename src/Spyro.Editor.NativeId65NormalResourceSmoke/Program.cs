using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;

if (args.Length > 1)
    throw new ArgumentException("Usage: NativeId65NormalResourceSmoke [original.bin]");

int checks = 0;
byte[] sourceVram = new byte[0x80000];
var vram = new NativeId65VramAssembly(sourceVram);
vram.CopySourceRange(0x100, 16);
vram.CopySourceRange(0x108, 16);
Require(vram.SourceOwnedByteCount == 24, "exact source aliases own the union, including zero bytes");
Reject(() => vram.CopyDetachedRange("different-owner", 0x100, new byte[16]),
    "zero-valued referenced pixels are not available for another owner");
vram.CopyDetachedRange("new-tile", 0x200, new byte[] { 1, 2, 3, 4 });
Reject(() => vram.CopyDetachedRange("new-tile", 0x202, new byte[] { 3, 4 }),
    "numeric equality does not authorize independent detached aliases");
Require(vram.CopyBytes()[0x201] == 2, "actual detached bytes are materialized");
byte[] isolated = vram.CopyBytes();
isolated[0x201] = 9;
Require(vram.CopyBytes()[0x201] == 2, "VRAM snapshots do not expose mutable storage");
foreach (var (offset, length) in new[] { (-1, 1), (0, 0), (0x7FFFF, 2), (int.MaxValue, 4), (0, int.MaxValue) })
    Reject(() => vram.CopySourceRange(offset, length), "bounded VRAM owners before allocation");
Reject(() => new NativeId65VramAssembly(new byte[0x80001]), "complete exact upload domain required");
var requestBound = new NativeId65VramAssembly(sourceVram);
for (int i = 0; i < 65536; i++) requestBound.CopySourceRange(0, 1);
Reject(() => requestBound.CopySourceRange(0, 1), "repeated aliases cannot amplify work beyond the request budget");
var byteBound = new NativeId65VramAssembly(sourceVram);
for (int i = 0; i < 32; i++) byteBound.CopySourceRange(0, sourceVram.Length);
Reject(() => byteBound.CopySourceRange(0, 1), "aggregate copied-byte work is bounded before another loop");
var inverseCollision = new NativeId65VramAssembly(sourceVram);
inverseCollision.CopyDetachedRange("first", 4, new byte[4]);
Reject(() => inverseCollision.CopySourceRange(4, 4), "source-after-detached alias also refuses");
Reject(() => inverseCollision.CopyDetachedRange(new string('x', 129), 40, new byte[1]), "owner labels are bounded");

byte[] texture = new byte[12];
texture[0] = 0; texture[1] = 0;
texture[4] = 31; texture[5] = 0;
texture[8] = 0; texture[9] = 31;
texture[10] = 31; texture[11] = 31;
BinaryPrimitives.WriteUInt16LittleEndian(texture.AsSpan(2), 0x7B3A);
BinaryPrimitives.WriteUInt16LittleEndian(texture.AsSpan(6), 0x1D);
var footprint = NativeId65PlayerTextureOwners.DecodeFootprint(texture);
Require(footprint.PixelRows.Count == 32 && footprint.PixelRows.All(r => r.ByteLength == 16),
    "native4bpp UV bounds produce actual strided byte owners");
Require(footprint.Palette.ByteLength == 32, "native CLUT owns16 complete entries");
foreach (ushort page in new ushort[] { 0, 7, 0x9D, 0x11D, 0x21D, 0xFFFF })
{
    byte[] malformed = texture.ToArray();
    BinaryPrimitives.WriteUInt16LittleEndian(malformed.AsSpan(6), page);
    Reject(() => NativeId65PlayerTextureOwners.DecodeFootprint(malformed), "unsupported depth/page/domain refuses");
}
byte[] badClut = texture.ToArray();
BinaryPrimitives.WriteUInt16LittleEndian(badClut.AsSpan(2), 0);
Reject(() => NativeId65PlayerTextureOwners.DecodeFootprint(badClut), "CLUT outside right-half owner refuses");
Reject(() => NativeId65PlayerTextureOwners.DecodeFootprint(texture[..^1]), "complete12-byte renderer command required");
foreach (ushort page in new ushort[] { 0x1D, 0x3D, 0x5D, 0x7D })
{
    byte[] blended = texture.ToArray();
    BinaryPrimitives.WriteUInt16LittleEndian(blended.AsSpan(6), page);
    var variant = NativeId65PlayerTextureOwners.DecodeFootprint(blended);
    Require(variant.PixelRows.SequenceEqual(footprint.PixelRows) && variant.Palette == footprint.Palette,
        "native blend bits are retained while physical ownership remains on the same page");
}
byte[] odd = texture.ToArray();
foreach (int i in new[] { 0, 4, 8, 10 }) odd[i] = 3;
var oneNibble = NativeId65PlayerTextureOwners.DecodeFootprint(odd);
Require(oneNibble.PixelRows.All(r => r.ByteLength == 1) && oneNibble.PixelRows[0].VramRelativeOffset ==
    footprint.PixelRows[0].VramRelativeOffset + 1, "odd native U conservatively owns its containing byte");
Reject(() => NativeId65NormalResourceSource.ValidateCapturedBytes([], [], [], []), "missing complete native sources refuse before decode");
Reject(() => NativeId65PlayerTextureOwners.Capture([], []), "neutral player bytes cannot acquire fixed source ownership");

if (args.Length == 1)
{
    var floor = new NativeId65AuthoredFloor(8448, 8448, 8704, 1024, 1024, 0x708050, 0x406080);
    var surface = await NativeId65AuthoredSurfaceBundle.CaptureAsync(args[0], floor);
    var companion = await NativeId65CompanionAssetPlan.CaptureAsync(args[0]);
    var neutral = NativeId65AuthoredSurfaceLayout.Compile(floor);
    Reject(() => NativeId65NormalResourceBundle.RequireInputs(neutral, companion), "neutral surface cannot acquire original-source authority");
    Reject(() => NativeId65NormalResourceBundle.RequireInputs(surface, null!), "missing companion refuses");
    var bundle = await NativeId65NormalResourceBundle.CaptureAsync(args[0], surface, companion);
    Require(bundle.SourceCertified && !bundle.IndependentLevelSupported && !bundle.PublicationApproved &&
        !bundle.RuntimeVerified && !bundle.CompleteLevelResourcesProven, "source resources do not issue native level authority");
    Require(bundle.CopyUploadPayload().Length == 0xDE000 && bundle.CopyModelPayload().Length == 0xF800,
        "actual fixed upload and padded model owners");
    Require(bundle.ModelRootOwners.Count == 2 && bundle.ModelRootOwners[0].Kind == NativeId65ModelRootKind.SpecialPlayerStream &&
        bundle.ModelRootOwners[1].Kind == NativeId65ModelRootKind.GenericClass && bundle.ModelRootOwners[1].ClassId == 0x78,
        "special root0 is not a generic model or a fabricated native header");
    byte[] model = bundle.CopyModelPayload();
    Require(Hash(model.AsSpan(0, 0xCC30)) == "7c9ca6faf03324ef102fe424e3e60ea7d7b6d54dde9c8382c589f960be9db4e0",
        "complete source player stream and terminator preserved");
    byte[] expected = companion.CompileAssetBlock().CopyRelocatedPackage();
    Require(expected[4] == 0 && expected[5] == 1, "detached local descriptor namespace");
    expected[4] = 72; expected[5] = 73;
    Require(model.AsSpan(0xCC30, 0x2930).SequenceEqual(expected), "exact two-byte local sound link and complete package complement");
    Require(model.AsSpan(0xF560).ToArray().All(b => b == 0), "new model sector padding is a distinct fresh owner");
    Require(bundle.PlayerTextures.ReferenceCount == 355 && bundle.PlayerTextures.PaletteIds.Count == 17,
        "actual resident and Town-local player texture union");
    Require(bundle.SourceVramOwnedByteCount == 11424, "independent raw-sector oracle's complete fixed source footprint union");
    Require(bundle.UploadSha256 == "ea5e5e56b7ba35abaaa55f7fc3cabe388cc64a47a00a5a07b8d72fecd475e1e4" &&
        bundle.DataTailSha256 == "a862c007087cfc2281e6a3cc1426794d335d96717c8f53f1d24fd2026c039a56" &&
        bundle.ModelSha256 == "88c78343c283256de6ce8d3151b43fa72ba4d96b046d869bd0a7209cbff4c23d",
        "all actual payload bytes match independent Python assembly from source sectors");
    Require(!bundle.CompleteDataChainAvailable, "missing issued scene texture-list owner refuses whole chain");
    Reject(() => bundle.CopyDataComponents(), "count-zero texture metadata is not approved by loader alone");
    Require(bundle.CopyDataComponentTail().Length == surface.ByteLength + 4 + 0x80 + 0x6F8,
        "actual issued surface plus raw portal count and retained particle/audio tail");
    bundle.RequireExactPayloads(bundle.CopyUploadPayload(), bundle.CopyDataComponentTail(), model);
    Require(true, "exact independent payload readback");
    byte[] upload = bundle.CopyUploadPayload(), tail = bundle.CopyDataComponentTail();
    byte[] sourceData = companion.EngineProfile.CopyTownData();
    byte[] sourceExe = companion.EngineProfile.CopyExecutable(), sourceOverlay = companion.EngineProfile.CopyOverlay();
    byte[] sourcePete = ReadWad(args[0], 0x7B9000, 0x32E1C);
    _ = NativeId65NormalResourceSource.ValidateCapturedBytes(sourceExe, sourceOverlay, sourceData, sourcePete);
    Require(true, "same captured native owners and exact consumer spans revalidate");
    foreach (var (domain, bytes, offsets) in new[]
    {
        (0, sourceExe, new[] { 0, 0x3558, 0x20F18, 0x21CC4, 0x2921C, 0x3AE38, 0x4E408, sourceExe.Length - 1 }),
        (1, sourceOverlay, new[] { 0, 0xD3E8, 0xE530, 0xE7A0, sourceOverlay.Length - 1 }),
        (2, sourceData, new[] { 0, 0x800 + 0x7B000, 0x80800, 0x172590, 0x172610, 0x173000, 0x1C9C80, sourceData.Length - 1 }),
        (3, sourcePete, new[] { 0, 0x34, 0x38, 0xAA8, sourcePete.Length - 1 })
    })
    {
        foreach (int offset in offsets)
        {
            byte[] corrupt = bytes.ToArray(); corrupt[offset] ^= 1;
            Reject(() => NativeId65NormalResourceSource.ValidateCapturedBytes(domain == 0 ? corrupt : sourceExe,
                domain == 1 ? corrupt : sourceOverlay, domain == 2 ? corrupt : sourceData, domain == 3 ? corrupt : sourcePete),
                "changed source consumer/resource/complement never becomes a new native certificate");
        }
    }
    foreach (var owner in bundle.Owners)
    {
        byte[] bytes = owner.Domain switch { NativeId65ResourceDomain.UploadPayload => upload,
            NativeId65ResourceDomain.DataComponentTail => tail, _ => model };
        Require(Hash(bytes.AsSpan(owner.Offset, owner.ByteLength)) == owner.Sha256, "actual typed byte owner digest");
        if (owner.SourceWadOffset is long offset)
            Require(bytes.AsSpan(owner.Offset, owner.ByteLength).SequenceEqual(ReadWad(args[0], offset, owner.ByteLength)),
                "each retained owner directly matches raw source sectors at its declared WAD address");
        if (owner.Provenance.StartsWith("fresh-zero", StringComparison.Ordinal))
            Require(bytes.AsSpan(owner.Offset, owner.ByteLength).ToArray().All(b => b == 0), "all unpopulated upload background is explicitly fresh zero storage");
    }
    foreach (var domain in Enum.GetValues<NativeId65ResourceDomain>())
    {
        var owners = bundle.Owners.Where(o => o.Domain == domain).OrderBy(o => o.Offset).ToArray();
        Require(owners[0].Offset == 0 && owners.Zip(owners.Skip(1)).All(p => p.First.Offset + p.First.ByteLength == p.Second.Offset),
            "each actual payload is partitioned without holes or aliases");
    }
    foreach (var link in bundle.SoundLinks)
    {
        Require(model[link.ModelRelativeFieldOffset] == link.RetainedDescriptorIndex &&
            Hash(tail.AsSpan(link.DataTailDescriptorOffset, 20)) == link.DescriptorSha256,
            "local model byte links to its actual retained descriptor, not a global selector");
        Require(BinaryPrimitives.ReadUInt32LittleEndian(tail.AsSpan(link.DataTailDescriptorOffset)) == link.SampleBankRelativeOffset,
            "descriptor sample offset keeps its SPU-bank-relative domain");
    }
    int soundAt = tail.Length - 0x6F8;
    Require(tail.AsSpan(soundAt, 0x6F8).SequenceEqual(sourceData.AsSpan(0x172610, 0x6F8)) &&
        upload.AsSpan(0x80000).SequenceEqual(sourceData.AsSpan(0x80800, 0x5E000)),
        "entire native sound map, descriptors, padding and bank survive exactly");
    Require(tail[soundAt + 4] == 17 && tail[soundAt + 5] == 0 && model[0xCC34] == 72,
        "global-selector/local-descriptor distinction is observable in actual source bytes");
    foreach (int descriptorId in bundle.ParticleDescriptorIds)
    {
        Require(bundle.CopyParticleFrame(descriptorId, 0).Length == 8, "source asset owns exactly one native eight-byte frame");
        Reject(() => bundle.CopyParticleFrame(descriptorId, 1), "frame1 cannot read into the following descriptor owner");
        Reject(() => bundle.CopyParticleFrame(descriptorId, -1), "negative frame refuses");
    }
    Reject(() => bundle.CopyParticleFrame(64, 0), "descriptor64 must not alias gModels0");
    Require(!bundle.CompletePlayerParticleClosureProven && !bundle.PlayerTextures.FramePayloadDecodingProven,
        "retained bytes do not certify every runtime player frame or particle state");
    foreach (var (kind, originalBytes) in new[] { (0, upload), (1, tail), (2, model) })
    {
        foreach (int offset in new[] { 0, originalBytes.Length / 2, originalBytes.Length - 1 })
        {
            byte[] corrupt = originalBytes.ToArray(); corrupt[offset] ^= 1;
            Reject(() => bundle.RequireExactPayloads(kind == 0 ? corrupt : upload, kind == 1 ? corrupt : tail, kind == 2 ? corrupt : model),
                "complete final payload corruption refuses including fresh zeros and retained complements");
        }
        Reject(() => bundle.RequireExactPayloads(kind == 0 ? upload[..^1] : upload,
            kind == 1 ? tail[..^1] : tail, kind == 2 ? model[..^1] : model), "truncated output refuses");
    }
    foreach (int at in new[] { 0, 4, 0xCC30 + 4, 0xF560, model.Length - 1 })
    {
        byte[] corrupt = model.ToArray(); corrupt[at] ^= 1;
        Reject(() => bundle.RequireExactPayloads(bundle.CopyUploadPayload(), bundle.CopyDataComponentTail(), corrupt),
            "model owner/padding corruption refused");
    }
    var secondSurface = await NativeId65AuthoredSurfaceBundle.CaptureAsync(args[0], floor with { FloorRgb = 0xE03040 });
    var second = await NativeId65NormalResourceBundle.CaptureAsync(args[0], secondSurface, companion);
    Require(second.UploadSha256 == bundle.UploadSha256 && second.ModelSha256 == bundle.ModelSha256 &&
        second.DataTailSha256 == "3c213c148f01a5ddea5b7a1210c1882fca2349fd429b773b8248efa0821dea89",
        "two distinct authored surface intents preserve resource independence and match the independent oracle");
    var repeat = await NativeId65NormalResourceBundle.CaptureAsync(args[0], surface, companion);
    Require(repeat.UploadSha256 == bundle.UploadSha256 && repeat.DataTailSha256 == bundle.DataTailSha256 &&
        repeat.ModelSha256 == bundle.ModelSha256, "repeat original-source compilation is deterministic");
    Console.WriteLine($"PAYLOADS upload={bundle.UploadSha256} tail={bundle.DataTailSha256} model={bundle.ModelSha256}");
}

Console.WriteLine($"NATIVE_ID65_NORMAL_RESOURCE_SMOKE_OK checks={checks}");

void Require(bool value, string label)
{
    if (!value) throw new InvalidDataException(label);
    checks++;
}
void Reject(Action action, string label)
{
    try { action(); }
    catch (Exception e) when (e is InvalidDataException or ArgumentException or InvalidOperationException)
    { checks++; return; }
    throw new InvalidDataException("Unexpected acceptance: " + label);
}
static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
static byte[] ReadWad(string path, long offset, int length)
{
    byte[] bytes = new byte[length];
    using FileStream image = File.OpenRead(path);
    int copied = 0;
    while (copied < bytes.Length)
    {
        int count = Math.Min(bytes.Length - copied, 2048 - (int)(offset % 2048));
        image.Position = (37 + offset / 2048) * 2352 + 24 + offset % 2048;
        image.ReadExactly(bytes.AsSpan(copied, count));
        copied += count; offset += count;
    }
    return bytes;
}
