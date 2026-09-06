using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;

if (args.Length > 1)
    throw new ArgumentException("Usage: NativeId65AuthoredSurfaceSmoke [original.bin]");

int checks = 0;
var floor = new NativeId65AuthoredFloor(8448, 8448, 8704, 1024, 1024, 0x708050, 0x406080);
var bundle = NativeId65AuthoredSurfaceLayout.Compile(floor);
Require(!bundle.SourceCertified && !bundle.IndependentLevelSupported && !bundle.PublicationApproved &&
    !bundle.RuntimeVerified && !bundle.CompleteLevelResourcesProven, "neutral bytes cannot issue native level authority");
Require(bundle.Components.Count == 5, "only five detached surface components");
Require(bundle.Owners.Sum(o => o.ByteLength) == bundle.ByteLength, "all emitted storage owned exactly once");
Require(bundle.Owners.OrderBy(o => o.BlockOffset).Select((o, i) => o.BlockOffset ==
    bundle.Owners.Take(i).Sum(p => p.ByteLength)).All(x => x), "contiguous owner partition");
Require(bundle.CopyBundle().SequenceEqual(NativeId65AuthoredSurfaceLayout.Compile(floor).CopyBundle()),
    "deterministic fresh compilation");
Require(NativeId65AuthoredSurfaceLayout.DecodeAndValidate(bundle.CopyBundle()) == floor, "strict independent readback");
Oracle(bundle, floor);

foreach (var alternative in new[]
{
    floor with { Width = 16, Depth = 16 },
    floor with { Width = 2048, Depth = 2048 },
    floor with { X = 0, Y = 0, Z = 0 },
    floor with { X = 258048, Y = 258048, Z = 262128 },
    floor with { FloorRgb = 0xFFFFFF, SkyRgb = 0 }
})
{
    var changed = NativeId65AuthoredSurfaceLayout.Compile(alternative);
    Require(!changed.CopyBundle().SequenceEqual(bundle.CopyBundle()), "distinct authored intent survives encoding");
    Require(NativeId65AuthoredSurfaceLayout.DecodeAndValidate(changed.CopyBundle()) == alternative,
        "noncollapsed authored intent roundtrips");
    Oracle(changed, alternative);
}

foreach (var invalid in new[]
{
    floor with { X = -16 }, floor with { Y = -16 }, floor with { Z = -16 },
    floor with { X = 262144 }, floor with { Y = 262144 }, floor with { Z = 262144 },
    floor with { X = 8449 }, floor with { Y = 8449 }, floor with { Z = 8705 },
    floor with { Width = 0 }, floor with { Depth = 0 }, floor with { Width = 15 },
    floor with { Depth = 2049 }, floor with { Width = 2064 },
    floor with { X = 11264, Width = 1024 }, floor with { Y = 11264, Depth = 1024 },
    floor with { X = int.MaxValue }, floor with { Width = int.MaxValue },
    floor with { FloorRgb = 0x1000000 }, floor with { SkyRgb = 0xFF000000 }
})
    Reject(() => NativeId65AuthoredSurfaceLayout.Compile(invalid), "unsupported coordinate/range/cell/RGB refuses");
Reject(() => NativeId65AuthoredSurfaceLayout.Compile(null!), "null intent refuses");

byte[] exact = bundle.CopyBundle();
Require(bundle.BundleSha256 == "ee82c4d3bb2589eb821028337499d062762c47d3f6af2267f6b0fabee8df58b9",
    "independent Python native-field oracle golden");
foreach (var owner in bundle.Owners)
    Require(Hash(exact.AsSpan(owner.BlockOffset, owner.ByteLength)) == owner.Sha256, "complete owner digest");
foreach (var reference in bundle.References)
{
    var field = bundle.Owners.Single(o => o.Id == reference.FieldOwnerId);
    var target = bundle.Owners.Single(o => o.Id == reference.TargetOwnerId);
    int at = field.BlockOffset + reference.FieldRelativeOffset;
    uint raw = reference.ByteWidth switch { 1 => exact[at], 2 => Half(exact, at), 4 => Word(exact, at),
        _ => throw new InvalidDataException("unsupported reference width") };
    Require(raw == reference.EncodedValue, "typed reference names its actual emitted field bytes");
    Require(reference.BaseBlockOffset + (long)(raw & reference.ValueMask) * reference.UnitByteLength ==
        target.BlockOffset + reference.TargetRelativeOffset, "native byte/word/index reference resolves inside actual owner");
}
for (int i = 0; i < exact.Length; i++)
{
    byte[] corrupt = exact.ToArray();
    corrupt[i] ^= 0x80;
    Reject(() => bundle.RequireExactBundle(corrupt), "every owned byte is bound");
}
Reject(() => bundle.RequireExactBundle(exact[..^1]), "truncation refuses");
Reject(() => bundle.RequireExactBundle(exact.Concat(new byte[4]).ToArray()), "unowned appended storage refuses");
foreach (int length in new[] { 0, 4, 31, exact.Length - 1, exact.Length + 4, 4097 })
    Reject(() => NativeId65AuthoredSurfaceLayout.DecodeAndValidate(new byte[length]), "malformed bundle bounded before allocation");

// These independent offsets target native ownership and semantic failure modes,
// not only an afterimage digest. The decoder must reject each before issuing a floor.
int collision = bundle.Components.Single(c => c.Kind == NativeId65SurfaceComponentKind.Collision).BlockOffset;
int flags = collision + 4 + (int)Word(exact, collision + 28);
int triangles = collision + 4 + (int)Word(exact, collision + 20);
int assignments = collision + 4 + (int)Word(exact, collision + 24);
int tree = collision + 4 + (int)Word(exact, collision + 12);
int blocks = collision + 4 + (int)Word(exact, collision + 16);
Corrupt(flags, 0, "missing special descriptor must not become ordinary floor");
Corrupt(flags, 0x7F, "unsupported high flag bits");
Corrupt(triangles + 9, 0x40, "raw packed Z flags");
Corrupt(assignments, 1, "undefined visibility group");
Corrupt(blocks, 2, "foreign triangle index");
Corrupt(blocks + 5, 0, "missing terminal block sentinel");
Corrupt(tree + 7, 0xFF, "missing native tree branch");
for (int pointer = 12; pointer <= 28; pointer += 4)
    Corrupt(collision + pointer, 0xFF, "every collision pointer owns its exact native base");
Corrupt(12 + 0x13, 1, "reserved sector header byte");
Corrupt(12 + 0x17, 0, "dependent HP-table offset");
Corrupt(12 + 0x18, 0, "missing signed-byte scene-consumer sentinel");
Corrupt(12 + 124 + 8, 0, "texture index zero is not an untextured sentinel");
Corrupt(12 + 124 + 12, 0, "unproven subdivision queue must stay bypassed");
Corrupt(12 + 124, 1, "HP repeated-corner triangle convention");
Corrupt(12 + 156, 1, "lookahead guard remains owned zero storage");
int sky = bundle.Components.Single(c => c.Kind == NativeId65SurfaceComponentKind.Cyclorama).BlockOffset;
Corrupt(sky + 8, 1, "missing sky-part owner");
Corrupt(sky + 12, 1, "count-zero speculative pointer-load guard");

byte[] copy = bundle.CopyBundle();
copy[0] ^= 1;
Require(bundle.CopyBundle().SequenceEqual(exact), "bundle is immutable");
foreach (var component in bundle.Components)
{
    byte[] payload = bundle.CopyComponent(component.Kind);
    Require(Hash(payload) == component.Sha256, "component digest");
    payload[0] ^= 1;
    Require(Hash(bundle.CopyComponent(component.Kind)) == component.Sha256, "component copies detached");
}
Reject(() => NativeId65AuthoredSurfaceConsumers.Capture(new byte[0x66000]), "source-free bytes cannot fake consumer certificates");

if (args.Length == 1)
{
    var source = await NativeId65AuthoredSurfaceBundle.CaptureAsync(args[0], floor);
    Require(source.SourceCertified, "actual original source issuer");
    Require(source.CopyBundle().SequenceEqual(exact), "source binding introduces no donor surface storage");
    Require(source.ConsumerEvidence.Count >= 8, "loader/render/index consumer evidence bound");
    Require(!source.IndependentLevelSupported && !source.PublicationApproved && !source.RuntimeVerified &&
        !source.CompleteLevelResourcesProven, "source certification does not widen acceptance");
    byte[] sourceExe = source.CopySourceExecutable();
    var evidence = NativeId65AuthoredSurfaceConsumers.Capture(sourceExe);
    foreach (var span in evidence)
    {
        Require(Hash(sourceExe.AsSpan(span.ExecutableFileOffset, span.ByteLength)) == span.Sha256,
            "actual native source span digest");
        byte[] changed = sourceExe.ToArray();
        changed[span.ExecutableFileOffset] ^= 1;
        Reject(() => NativeId65AuthoredSurfaceConsumers.Capture(changed), "changed native consumer refuses");
    }
    sourceExe[0] ^= 1;
    Require(source.CopySourceExecutable()[0] != sourceExe[0], "source executable snapshot immutable");
    Console.WriteLine($"SOURCE image={source.SourceImageSha256} bundle={source.BundleSha256} bytes={source.ByteLength}");
    Console.WriteLine("NATIVE_ID65_AUTHORED_SURFACE_SOURCE_OK");
}
Console.WriteLine($"NATIVE_ID65_AUTHORED_SURFACE_SMOKE_OK checks={checks}");

void Oracle(NativeId65AuthoredSurfaceBundle value, NativeId65AuthoredFloor intent)
{
    byte[] e = value.CopyComponent(NativeId65SurfaceComponentKind.Environment);
    Require(Word(e, 0) == e.Length && Word(e, 4) == 1 && Word(e, 8) == 8, "fresh one-sector environment pointer");
    int s = 12;
    int ox = (int)(Word(e, s + 8) >> 16) * 16;
    int oy = (int)(Word(e, s + 8) & 65535) * 16;
    int oz = (int)(Word(e, s + 12) >> 16) * 16;
    Require((ox, oy, oz) == (intent.X, intent.Y, intent.Z), "native origin is actual world/16, not screen coordinates");
    Require(e.AsSpan(s + 16, 8).SequenceEqual(new byte[] { 4, 4, 2, 0, 4, 4, 2, 12 }), "LP/HP counts and header-dependent word offset");
    Require(Word(e, s + 24) == uint.MaxValue, "four negative-byte sentinels disable unowned scene-consumer indices");
    var points = new (int X, int Y, int Z)[4];
    for (int i = 0; i < 4; i++)
    {
        uint v = Word(e, s + 28 + 4 * i);
        points[i] = (ox + (int)(v >> 21) * 16, oy + (int)((v >> 10) & 2047) * 16,
            oz + (int)(v & 1023) * 16);
        Require(Word(e, s + 76 + 4 * i) == v, "LP/HP exact same native vertices");
        Require(Word(e, s + 44 + 4 * i) == intent.FloorRgb &&
            Word(e, s + 92 + 4 * i) == intent.FloorRgb && Word(e, s + 108 + 4 * i) == intent.FloorRgb,
            "three complete Gouraud color tables");
    }
    Require(points.SequenceEqual(new[] { (intent.X, intent.Y, intent.Z),
        (intent.X + intent.Width, intent.Y, intent.Z), (intent.X, intent.Y + intent.Depth, intent.Z),
        (intent.X + intent.Width, intent.Y + intent.Depth, intent.Z) }), "exact authored rectangle");
    byte[] c = value.CopyComponent(NativeId65SurfaceComponentKind.Collision);
    int tr = 4 + (int)Word(c, 20);
    int fl = 4 + (int)Word(c, 28);
    int gr = 4 + (int)Word(c, 24);
    Require(Word(c, 4) == 2 && Word(c, 8) == 2, "no inherited collision count or fallback flags");
    for (int i = 0; i < 2; i++)
    {
        uint x = Word(c, tr + i * 12);
        uint y = Word(c, tr + i * 12 + 4);
        uint z = Word(c, tr + i * 12 + 8);
        var a = ((int)(x & 16383) * 16, (int)(y & 16383) * 16, (int)(z & 16383) * 16);
        var b = (a.Item1 + Signed9(x >> 14) * 16, a.Item2 + Signed9(y >> 14) * 16, a.Item3 + (int)((z >> 16) & 255) * 16);
        var d = (a.Item1 + Signed9(x >> 23) * 16, a.Item2 + Signed9(y >> 23) * 16, a.Item3 + (int)(z >> 24) * 16);
        Require((long)(b.Item1 - a.Item1) * (d.Item2 - a.Item2) -
            (long)(b.Item2 - a.Item2) * (d.Item1 - a.Item1) < 0, "native upward negative collision winding");
        Require(new[] { a, b, d }.All(points.Contains) && a.Item3 == intent.Z && b.Item3 == intent.Z && d.Item3 == intent.Z,
            "every emitted collision corner matches authored visible geometry");
        Require(c[fl + i] == 0x3F && c[gr + i] == 0 && (z & 0xC000) == 0,
            "ordinary dry land with owned visibility and no raw Z flags");
    }
    int t = 4 + (int)Word(c, 12);
    int q = 4 + (int)Word(c, 16);
    int zo = Half(c, t + 2 + (intent.Z >> 12) * 2);
    int yo = Half(c, t + zo + 2 + (intent.Y >> 12) * 2);
    int leaf = Half(c, t + yo + 2 + (intent.X >> 12) * 2);
    Require(leaf == 0 && Half(c, q) == 0x8001 && Half(c, q + 2) == 0 && Half(c, q + 4) == 0x8000,
        "true emitted triangle0/1 reachable through native Z/Y/X and word-indexed block pointer");
    byte[] vis = value.CopyComponent(NativeId65SurfaceComponentKind.Visibility);
    Require(Word(vis, 8) == 1 && vis[4 + Word(vis, 12)] == 0 && vis[5 + Word(vis, 12)] == 0xFF,
        "collision group0 resolves only fresh environment sector0");
    int skyBase = 4 + (int)Word(vis, 4);
    Require(Word(vis, skyBase) == 1 && vis[skyBase + Word(vis, skyBase + 4)] == 0xFF,
        "empty sky group terminates without foreign sky-part indices");
    byte[] skyBytes = value.CopyComponent(NativeId65SurfaceComponentKind.Cyclorama);
    Require(Word(skyBytes, 4) == intent.SkyRgb && Word(skyBytes, 8) == 0 && Word(skyBytes, 12) == 0,
        "zero-part RGB cyclorama owns its native delay-slot guard");
}

void Corrupt(int offset, byte replacement, string label)
{
    byte[] bytes = exact.ToArray();
    Require(bytes[offset] != replacement, "hostile fixture changes a byte: " + label);
    bytes[offset] = replacement;
    Reject(() => NativeId65AuthoredSurfaceLayout.DecodeAndValidate(bytes), label);
}
void Require(bool condition, string label)
{
    if (!condition)
        throw new InvalidDataException(label);
    checks++;
}
void Reject(Action action, string label)
{
    try { action(); }
    catch (Exception ex) when (ex is InvalidDataException or ArgumentException) { checks++; return; }
    throw new InvalidDataException("Expected refusal: " + label);
}
static uint Word(ReadOnlySpan<byte> b, int o) => BinaryPrimitives.ReadUInt32LittleEndian(b[o..]);
static ushort Half(ReadOnlySpan<byte> b, int o) => BinaryPrimitives.ReadUInt16LittleEndian(b[o..]);
static int Signed9(uint n) => ((int)n & 511) is var v && v >= 256 ? v - 512 : v;
static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
