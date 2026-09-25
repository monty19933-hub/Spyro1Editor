using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;

int checks = 0;
void Check(bool condition, string message)
{ if (!condition) throw new Exception(message); checks++; }
void Refuse(Action action, string message)
{
    try { action(); }
    catch (InvalidDataException) { checks++; return; }
    throw new Exception("Expected refusal: " + message);
}
static void Word(byte[] bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
static uint Indices(int a, int b, int c, int d) => (uint)(a << 23 | b << 16 | c << 9 | d << 2);
static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
static byte[] Fixture()
{
    byte[] owner = new byte[0x44];
    Word(owner, 0, 0xFF020204); Word(owner, 4, 0x10); Word(owner, 8, 0x1C); Word(owner, 12, 0x2C);
    new byte[] { 128, 0, 127, 1, 255, 2, 3, 4, 5, 6, 7, 8 }.CopyTo(owner, 0x10);
    for (int i = 0; i < 4; i++) Word(owner, 0x1C + 4 * i, (uint)(0x010203 + i));
    Word(owner, 0x2C, Indices(0, 1, 2, 3)); Word(owner, 0x30, Indices(0, 1, 2, 3));
    Word(owner, 0x34, Indices(0, 2, 1, 1)); Word(owner, 0x38, Indices(2, 1, 0, 127));
    // Distinct external owner: not logical model bytes, and never rendered.
    Convert.FromHexString("0E1236FF10000000").CopyTo(owner, 0x3C);
    return owner;
}

if (args.Length == 2 && args[0] == "--source")
{
    using var source = NativeRegionalSourceSession.Open(args[1], NativeDiscRevision.UsaScus94228);
    var capture = NativeUsaSharedEggModel.Capture(source);
    var geometry = capture.Geometry;
    Check(capture.SourceImageSha256 == source.SourceImageSha256 && capture.ActorId == 0x22 && capture.RegistrationSlot == 3,
        "Exact original image and class registration remain bound.");
    Check(capture.RootWadOffset == 0x7EC02C && geometry.PackageByteLength == 0x5BC && geometry.RequiredSourceWindowByteLength == 0x5C4,
        "Logical geometry and discarded read-ahead window have different extents.");
    Check(geometry.PackedSourceHeader == 0xFF127375 && geometry.PackageSha256 == NativeUsaSharedEggModel.ExpectedPackageSha256,
        "Exact derived logical egg package.");
    Check(geometry.Vertices.Count == 117 && geometry.Faces.Count == 115 && geometry.Faces.All(f => !f.IsTriangle) &&
        geometry.PackedRgbWords.Count == 45, "Native vertex, quad and RGB closure.");
    Check(geometry.Vertices[0] == new NativeSimpleMobyVertex(42, 0, 34), "Source signed-byte coordinates are doubled before GTE.");
    Check(geometry.Faces.SelectMany(f => f.VertexIndices).Distinct().Order().SequenceEqual(Enumerable.Range(0, 117)) &&
        geometry.Faces.SelectMany(f => f.ColorIndices).Distinct().Order().SequenceEqual(Enumerable.Range(0, 45)),
        "All source vertex and color owners are referenced.");
    Check(geometry.Faces.All(f => (f.PackedVertexWord & 3) == 0) && !geometry.NormalPrefixPresent && !geometry.TextureReferencePresent,
        "Original egg has direct Gouraud colours, not profile/normal or texture commands.");
    foreach (var expected in new (string Role, int Offset, int Length, string Hash)[]
    {
        ("root-and-three-relative-pointers", 0, 0x10, "c137863b85cbcf7d7044b5c3d9a1ca411181c7ff2e052a63e2db276880281716"),
        ("signed-byte-vertices", 0x10, 0x15F, "131d19958c71b885a685815bcb5e1c3f53d32631ce58a7bd21f34a8fd2c16af2"),
        ("indexed-packed-rgb", 0x170, 0xB4, "8303997e60554a81a4d5d8be62742e45eb79994d14afd2875381b5352508fcb3"),
        ("fixed-eight-byte-faces", 0x224, 0x398, "b7ccc3761e094326f740a2a12082196785ddbac2c72c401d587fa0c6880879ad")
    })
        Check(geometry.Ranges.Contains(new(expected.Role, expected.Offset, expected.Length, expected.Hash)), "Independent source range " + expected.Role);
    Check(geometry.Ranges.Sum(r => r.ByteLength) == geometry.PackageByteLength, "Every logical byte has a distinct owner.");
    Check(geometry.PipelineReads[0].RootRelativeOffset == 0x16F && geometry.PipelineReads[0].ByteLength == 4 &&
        geometry.PipelineReads[1].RootRelativeOffset == 0x16C && geometry.PipelineReads[1].ByteLength == 8,
        "Semantic vertex preload and aligned LWR/LWL envelope are separately bounded.");
    Check(capture.ReadAheadOwner == new NativeSharedMobyReadAheadOwner(0x54, 4, 0x33DE8, 0, 8,
        "76df2a96a85a7aedab18e72c11dd27bc9fb7948518fdea7817ddf6a72096fd4c"), "External read owner is class54 header, not part of egg22.");
    Check(capture.ReadAheadOwner.PipelineValuesDiscarded && !capture.ReadAheadOwner.WholeFollowingModelRequired,
        "Read-ahead does not invent a whole class54 import dependency.");
    byte[] readWindow = [.. geometry.CopyPackage(), .. geometry.CopyFaceReadAhead()];
    Check(Hash(readWindow) == "a587c0fa7ef06bc76812929af86691b90f6731a184bf9dfb0bfc4cb914bed2ce",
        "Whole source read window hash is separate from logical package hash.");
    Check(source.ReadWadRange(capture.RootWadOffset, readWindow.Length).SequenceEqual(readWindow), "Exact original-source byte readback.");
    Check(NativeSimpleGouraudModelGeometry.Decode(readWindow).CopyPackage().SequenceEqual(geometry.CopyPackage()), "Detached source geometry readback.");
    Refuse(() => NativeSimpleGouraudModelGeometry.Decode(geometry.CopyPackage()), "source package without external read-ahead");
    Check(capture.SourceGeometryClosureProven && capture.SourceReadWindowClosureProven && !capture.NativeImportApproved &&
        !capture.DestinationRuntimeVerified && !geometry.SourceIdentityProven && !geometry.NativeImportApproved,
        "Exact source evidence is not destination/runtime approval; neutral decoder issues no source identity.");
    Check(capture.ConsumerEvidence.Count == 3 && capture.ConsumerEvidence.All(e => e.RuntimeAddress - 0x8000F800 == e.ExecutableOffset),
        "Source consumer windows use actual PS-X executable mapping.");
    source.VerifyUnchanged();
    Console.WriteLine($"NATIVE_SIMPLE_MOBY_GEOMETRY_DISC_OK checks={checks} actor=0022 bytes={geometry.PackageByteLength} readAheadBytes=8 originalFinal=true candidateWrites=0");
    return;
}
if (args.Length != 0) throw new ArgumentException("Use no arguments or --source <original-usa.bin>.");

byte[] fixture = Fixture();
var model = NativeSimpleGouraudModelGeometry.Decode(fixture);
Check(model.PackageByteLength == 0x3C && model.FaceReadAheadByteLength == 8 && model.RequiredSourceWindowByteLength == 0x44,
    "Logical and pipeline owners remain separate.");
Check(model.Vertices[0] == new NativeSimpleMobyVertex(-256, 0, 254) && model.Vertices[1] == new NativeSimpleMobyVertex(2, -2, 4),
    "Signed byte extremes and native doubling.");
Check(model.Faces.Count == 2 && !model.Faces[0].IsTriangle && model.Faces[1].IsTriangle, "Fixed8-byte quad and triangle distinction.");
Check(model.PackedRgbWords.Count == 4 && model.Faces[1].ColorIndices.SequenceEqual(new[] { 2, 1, 0 }),
    "Triangle's ignored fourth colour field127 does not enlarge the read owner.");
Check(model.Ranges.Sum(r => r.ByteLength) == model.PackageByteLength && model.PipelineReads.Count == 3,
    "Complete byte owners and explicit pipeline reads.");
Check(!model.SourceIdentityProven && !model.NativeImportApproved && !model.NormalPrefixPresent && !model.TextureReferencePresent,
    "Neutral structural decoding cannot promote source/import authority.");
fixture[0] = 0; byte[] detached = model.CopyPackage(); detached[0] = 0;
byte[] detachedReadAhead = model.CopyFaceReadAhead(); detachedReadAhead[0] = 0;
Check(model.CopyPackage()[0] == 4 && model.CopyFaceReadAhead()[0] == 0x0E, "Returned logical/read-ahead arrays and input are detached.");
byte[] trailing = new byte[0x100]; Fixture().CopyTo(trailing, 0); trailing[0x44] = 0xFF;
Check(NativeSimpleGouraudModelGeometry.Decode(trailing).PackageByteLength == 0x3C, "Next-root distance/trailing data cannot enlarge logical geometry.");
byte[] alternativeTail = Fixture(); Array.Fill(alternativeTail, (byte)0, 0x3C, 8);
var alternate = NativeSimpleGouraudModelGeometry.Decode(alternativeTail);
Check(alternate.PackageSha256 == model.PackageSha256 && alternate.FaceReadAheadSha256 != model.FaceReadAheadSha256,
    "Discarded bytes have an independent identity; neutral decoder does not forge their provenance.");
byte[] orderBias = Fixture(); Word(orderBias, 0x30, Indices(0, 1, 2, 3) | 3);
Check(NativeSimpleGouraudModelGeometry.Decode(orderBias).Faces[0].PackedColorWord % 4 == 3,
    "Native colour-word ordering bias is retained, not treated as texture flags.");
for (int length = 0; length < 0x44; length++)
{ int n = length; Refuse(() => NativeSimpleGouraudModelGeometry.Decode(Fixture().AsSpan(0, n)), $"truncated source window {n:X}"); }
foreach (var mutation in new (int Offset, uint Value)[]
{
    (0, 1), (0, 0xFF020200), (0, 0xFF020280), (0, 0xFF0202FF), (0, 0xFF020004),
    (4, 0), (4, 0x14), (4, uint.MaxValue), (8, 0), (8, 0x1D), (8, 0x20),
    (12, 0), (12, 0x2D), (12, 0x28), (12, 0x30), (12, uint.MaxValue),
    (0x1C, 0xFF010203), (0x2C, Indices(0, 1, 2, 3) | 1),
    (0x2C, Indices(0, 1, 2, 3) | 2), (0x2C, Indices(0, 1, 2, 3) | 3),
    (0x2C, Indices(4, 1, 2, 3)), (0x30, Indices(0, 1, 2, 4)), (0x30, Indices(0, 1, 2, 127))
})
{
    byte[] bad = Fixture(); Word(bad, mutation.Offset, mutation.Value);
    Refuse(() => NativeSimpleGouraudModelGeometry.Decode(bad), $"field +{mutation.Offset:X} = {mutation.Value:X8}");
}
// An odd number of vertex triples has explicit, checked word padding.
byte[] padded = new byte[0x3C];
Word(padded, 0, 0xFF010103); Word(padded, 4, 0x10); Word(padded, 8, 0x1C); Word(padded, 12, 0x2C);
for (int i = 0; i < 4; i++) Word(padded, 0x1C + i * 4, (uint)i);
Word(padded, 0x2C, Indices(0, 1, 2, 0)); Word(padded, 0x30, Indices(0, 1, 2, 3));
var paddedModel = NativeSimpleGouraudModelGeometry.Decode(padded);
Check(paddedModel.Ranges.Single(r => r.Role == "word-alignment").ByteLength == 3, "Exact triple-to-word alignment owner.");
for (int i = 0x19; i < 0x1C; i++)
{
    byte[] bad = padded.ToArray(); bad[i] = 1;
    Refuse(() => NativeSimpleGouraudModelGeometry.Decode(bad), $"nonzero padding at {i:X}");
}
Console.WriteLine($"NATIVE_SIMPLE_MOBY_GEOMETRY_SOURCE_FREE_OK checks={checks} sourceReads=0 candidateWrites=0");
