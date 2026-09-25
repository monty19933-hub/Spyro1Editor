using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Spyro.Editor.Core.Analysis;

/// <summary>Native component order; signed source bytes multiplied by two, before GTE/world transforms.</summary>
public sealed record NativeSimpleMobyVertex(int X, int Y, int Z);
public sealed record NativeSimpleMobyGeometryRange(string Role, int RootRelativeOffset, int ByteLength, string Sha256);
public sealed record NativeSimpleMobyFace(uint PackedVertexWord, uint PackedColorWord,
    IReadOnlyList<int> VertexIndices, IReadOnlyList<int> ColorIndices)
{
    public bool IsTriangle => VertexIndices[2] == VertexIndices[3];
}

/// <summary>
/// Bounded negative simple-model grammar: no normal prefix, fixed eight-byte
/// Gouraud faces, and indexed RGB words. This is a structural decoder, not a
/// source issuer or a general simple-model/animated-model import certificate.
/// Pipeline read-ahead bytes remain separate from the logical package.
/// </summary>
public sealed class NativeSimpleGouraudModelGeometry
{
    private readonly byte[] _package;
    private readonly byte[] _faceReadAhead;
    private NativeSimpleGouraudModelGeometry(byte[] package, byte[] readAhead,
        NativeSimpleMobyVertex[] vertices, NativeSimpleMobyFace[] faces, uint[] colors,
        List<NativeSimpleMobyGeometryRange> ranges, List<NativeSimpleMobyGeometryRange> reads)
    {
        _package = package; _faceReadAhead = readAhead;
        PackageSha256 = Hash(package); FaceReadAheadSha256 = Hash(readAhead);
        Vertices = Array.AsReadOnly(vertices); Faces = Array.AsReadOnly(faces); PackedRgbWords = Array.AsReadOnly(colors);
        Ranges = ranges.AsReadOnly(); PipelineReads = reads.AsReadOnly();
        PackedSourceHeader = Word(package, 0);
    }
    public uint PackedSourceHeader { get; }
    public int PackageByteLength => _package.Length;
    public string PackageSha256 { get; }
    public int FaceReadAheadByteLength => _faceReadAhead.Length;
    public string FaceReadAheadSha256 { get; }
    public int RequiredSourceWindowByteLength => checked(PackageByteLength + FaceReadAheadByteLength);
    public IReadOnlyList<NativeSimpleMobyVertex> Vertices { get; }
    public IReadOnlyList<NativeSimpleMobyFace> Faces { get; }
    public IReadOnlyList<uint> PackedRgbWords { get; }
    public IReadOnlyList<NativeSimpleMobyGeometryRange> Ranges { get; }
    public IReadOnlyList<NativeSimpleMobyGeometryRange> PipelineReads { get; }
    public bool NormalPrefixPresent => false;
    public bool TextureReferencePresent => false;
    public bool SourceIdentityProven => false;
    public bool NativeImportApproved => false;
    public byte[] CopyPackage() => _package.ToArray();
    public byte[] CopyFaceReadAhead() => _faceReadAhead.ToArray();

    /// <summary>
    /// The caller supplies a bounded source owner beginning at the root, including
    /// eight readable bytes after the logical face end. Extra trailing bytes do
    /// not enlarge the model. Their source provenance belongs to a separate issuer.
    /// </summary>
    public static NativeSimpleGouraudModelGeometry Decode(ReadOnlySpan<byte> owner)
    {
        Require(owner.Length >= 0x18 && owner.Length <= 0x800000, "A bounded simple-model source window is required.");
        Require(unchecked((int)Word(owner, 0)) < 0, "An animated model is not this simple-model grammar.");
        int vertexCount = unchecked((sbyte)owner[0]), faceCount = owner[1];
        Require(vertexCount > 0 && faceCount > 0, "Native signed vertex count and unsigned face count must be positive.");
        int vertexStart = Offset(owner, 4), colorStart = Offset(owner, 8), faceStart = Offset(owner, 12);
        Require(vertexStart == 0x10, "A normal-prefix simple model requires a separately proved decoder.");
        int vertexEnd = checked(vertexStart + vertexCount * 3);
        Require(colorStart == ((vertexEnd + 3) & ~3), "Vertex/alignment/color ownership is not contiguous.");
        Require(faceStart % 4 == 0 && faceStart >= colorStart + 4, "Unaligned or empty indexed RGB owner.");
        int faceEnd = checked(faceStart + faceCount * 8);
        Require(faceEnd <= owner.Length - 8, "The fixed face stream or its separate eight-byte pipeline read-ahead is truncated.");
        NativeSimpleMobyVertex[] vertices = new NativeSimpleMobyVertex[vertexCount];
        for (int i = 0, p = vertexStart; i < vertexCount; i++, p += 3)
            vertices[i] = new(unchecked((sbyte)owner[p]) * 2, unchecked((sbyte)owner[p + 1]) * 2,
                unchecked((sbyte)owner[p + 2]) * 2);
        for (int p = vertexEnd; p < colorStart; p++) Require(owner[p] == 0, "Unsupported nonzero vertex-alignment bytes.");
        NativeSimpleMobyFace[] faces = new NativeSimpleMobyFace[faceCount];
        int highestColor = -1;
        for (int i = 0, p = faceStart; i < faceCount; i++, p += 8)
        {
            uint vertexWord = Word(owner, p), colorWord = Word(owner, p + 4);
            Require((vertexWord & 3) == 0, "Flat/profile-normal face commands are outside the admitted Gouraud grammar.");
            int[] vi = Indices(vertexWord);
            Require(vi.All(index => index < vertexCount), "Face references an unowned vertex.");
            int[] allColors = Indices(colorWord);
            // The triangle branch consumes only the first three colours; its
            // unused fourth packed field must not invent an external owner.
            int[] ci = vi[2] == vi[3] ? allColors[..3] : allColors;
            highestColor = Math.Max(highestColor, ci.Max());
            faces[i] = new(vertexWord, colorWord, Array.AsReadOnly(vi), Array.AsReadOnly(ci));
        }
        int colorCount = checked(highestColor + 1);
        Require(colorStart + colorCount * 4 == faceStart,
            "Referenced RGB prefix does not exactly close the declared color-to-face interval.");
        uint[] colors = new uint[colorCount];
        for (int i = 0; i < colorCount; i++)
        {
            colors[i] = Word(owner, colorStart + i * 4);
            Require((colors[i] & 0xFF000000) == 0, "Gouraud RGB word contains unsupported packet-control bits.");
        }
        List<NativeSimpleMobyGeometryRange> ranges = [], reads = [];
        Add(ranges, owner, "root-and-three-relative-pointers", 0, 0x10);
        Add(ranges, owner, "signed-byte-vertices", vertexStart, vertexCount * 3);
        if (vertexEnd != colorStart) Add(ranges, owner, "word-alignment", vertexEnd, colorStart - vertexEnd);
        Add(ranges, owner, "indexed-packed-rgb", colorStart, colorCount * 4);
        Add(ranges, owner, "fixed-eight-byte-faces", faceStart, faceCount * 8);
        // LWR/LWL preload the following triple as a four-byte value. Include
        // the aligned memory-word envelope as well as its semantic byte range.
        Add(reads, owner, "vertex-final-four-byte-preload", vertexEnd, 4);
        int busStart = vertexEnd & ~3, busEnd = ((vertexEnd + 3) & ~3) + 4;
        Require(busEnd <= faceEnd, "Vertex read-ahead leaves the logical geometry owners.");
        Add(reads, owner, "vertex-final-aligned-word-envelope", busStart, busEnd - busStart);
        Add(reads, owner, "face-end-discarded-read-ahead-external-owner", faceEnd, 8);
        return new(owner[..faceEnd].ToArray(), owner.Slice(faceEnd, 8).ToArray(), vertices, faces, colors, ranges, reads);
    }

    private static int[] Indices(uint word) => [(int)(word >> 23 & 127), (int)(word >> 16 & 127),
        (int)(word >> 9 & 127), (int)(word >> 2 & 127)];
    private static void Add(List<NativeSimpleMobyGeometryRange> ranges, ReadOnlySpan<byte> owner, string role, int offset, int length)
    {
        Require(offset >= 0 && length > 0 && offset <= owner.Length - length, "Geometry range leaves its declared source owner.");
        ranges.Add(new(role, offset, length, Hash(owner.Slice(offset, length))));
    }
    private static int Offset(ReadOnlySpan<byte> owner, int p)
    { uint value = Word(owner, p); Require(value <= 0x800000, "Unbounded root-relative pointer."); return (int)value; }
    private static uint Word(ReadOnlySpan<byte> owner, int p)
    { Require(p >= 0 && p <= owner.Length - 4, "Truncated geometry word."); return BinaryPrimitives.ReadUInt32LittleEndian(owner[p..]); }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
