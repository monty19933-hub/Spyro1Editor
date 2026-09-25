using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Spyro.Editor.Core.Analysis;

public sealed record NativeJapaneseEggVertex(int X, int Y, int Z);
public sealed record NativeJapaneseEggModelRange(string Role, int PackageOffset, int ByteLength, string Sha256);
public sealed record NativeJapaneseEggModelConsumer(uint RuntimeAddress, int ExecutableOffset, int ByteLength, string Sha256);
public sealed record NativeJapaneseEggFace(int PackageOffset, IReadOnlyList<int> VertexIndices, IReadOnlyList<int> ShadingIndices);
public sealed record NativeJapaneseEggFrame(int Index, uint PackedSourceWord, uint AuxiliarySourceWord,
    int PackageOffset, int ByteLength, int AbsoluteVertexCount, int DeltaVertexCount,
    int LookAheadOffset, int LookAheadByteLength, IReadOnlyList<NativeJapaneseEggVertex> Vertices);

/// <summary>
/// Exact Artisans SCPS-10083 F5 model closure, not an actor-behaviour or USA import certificate.
/// Extents come from the pinned native renderer's counts and mixed vertex stream, never a next-root gap.
/// Vertices are decoded model coordinates, before per-instance/world/GTE transforms.
/// </summary>
public sealed class NativeJapaneseEggModel
{
    public const int DataWadEntry = 23;
    public const int SourceRegistrationSlot = 26;
    public const int SourceRootDataOffset = 0x1CCF5C;
    public const string SourcePackageSha256 = "da0c1ae7ab529a8ab5b43954452f368adb93c09f1a9b17013c52ee3039f4a810";
    private readonly byte[] _package;
    private static readonly NativeJapaneseEggModelConsumer[] Consumers =
    [
        new(0x800128D8, 0x30D8, 0x26C, "67eb37fcb000ba1634438c7d45311481c766e171114dae214429731cb8d3f7a4"),
        new(0x8001FEC4, 0x106C4, 0x474, "cf1bf8f5b7852586b9d63f49d9ccf759fb426f3515f62880d617bee3887a6db6"),
        new(0x80020354, 0x10B54, 0x174, "b9df3e24377794671aa0ee4d6f84b716ad36a4c5c761f4ba946550f54e8d8995"),
        new(0x80020550, 0x10D50, 0x438, "46de50c881f11fdc3e79b5e807dd920069c5439be5bb041d905cf1ecf0249a70")
    ];

    private NativeJapaneseEggModel(byte[] package, string? sourceHash, int sourceWadOffset,
        List<NativeJapaneseEggModelRange> ranges, List<NativeJapaneseEggModelRange> lookAheads,
        NativeJapaneseEggFrame[] frames, List<NativeJapaneseEggFace> near, List<NativeJapaneseEggFace> far,
        uint[] nearShading, uint[] farShading, byte scaleExponent)
    {
        _package = package; SourceImageSha256 = sourceHash; SourceRootWadOffset = sourceWadOffset;
        PackageSha256 = Hash(package); Ranges = ranges.AsReadOnly(); LookAheadReads = lookAheads.AsReadOnly();
        Frames = Array.AsReadOnly(frames); NearFaces = near.AsReadOnly(); FarFaces = far.AsReadOnly();
        NearPackedShadingWords = Array.AsReadOnly(nearShading); FarPackedShadingWords = Array.AsReadOnly(farShading);
        ScaleExponent = scaleExponent;
    }
    public string? SourceImageSha256 { get; }
    public int SourceRootWadOffset { get; }
    public string PackageSha256 { get; }
    public int PackageByteLength => _package.Length;
    public int AnimationCount => 1;
    public byte ScaleExponent { get; }
    public IReadOnlyList<NativeJapaneseEggFrame> Frames { get; }
    public IReadOnlyList<NativeJapaneseEggFace> NearFaces { get; }
    public IReadOnlyList<NativeJapaneseEggFace> FarFaces { get; }
    // Native +18/+20 words feed the RGB/GTE colour path. They are NOT a proven array of XYZ normals.
    public IReadOnlyList<uint> NearPackedShadingWords { get; }
    public IReadOnlyList<uint> FarPackedShadingWords { get; }
    public IReadOnlyList<NativeJapaneseEggModelRange> Ranges { get; }
    public IReadOnlyList<NativeJapaneseEggModelRange> LookAheadReads { get; }
    public IReadOnlyList<NativeJapaneseEggModelConsumer> ConsumerEvidence => Array.AsReadOnly(Consumers);
    public bool SourceGeometryClosureProven => SourceImageSha256 is not null;
    public bool NativeImportApproved => false;
    public bool DestinationRuntimeVerified => false;
    public bool ExternalTextureDependencyPresent => false;
    public byte[] CopyPackage() => _package.ToArray();

    public static NativeJapaneseEggModel Capture(NativeRegionalSourceSession source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Revision != NativeDiscRevision.JapanScps10083)
            throw new InvalidDataException("The F5 source contract requires the exact Japanese revision.");
        foreach (var consumer in Consumers)
            Require(Hash(source.ReadExecutableRange(consumer.ExecutableOffset, consumer.ByteLength)) == consumer.Sha256,
                "Japanese model consumer preimage changed.");
        byte[] data = source.ReadWadEntry(DataWadEntry);
        Require(Hash(data) == "7184fff950efa2bdffa37f97875ef7de2a3219480c61ec3ab37f784778d8cfde",
            "Artisans data owner changed.");
        Require(Word(data, 0x50 + SourceRegistrationSlot * 4) == SourceRootDataOffset &&
            Half(data, 0x150 + SourceRegistrationSlot * 2) == 0xF5, "Native F5 registration changed.");
        int modelStart = Offset(data, 0x10), modelLength = Offset(data, 0x14);
        Require(modelStart <= SourceRootDataOffset && modelLength <= data.Length - modelStart &&
            SourceRootDataOffset < modelStart + modelLength, "F5 root leaves the declared model subfile.");
        // The model subfile is the read bound, not the model's inferred size.
        var result = Decode(data.AsSpan(SourceRootDataOffset, modelStart + modelLength - SourceRootDataOffset),
            source.SourceImageSha256, checked(source.Entries[DataWadEntry].Offset + SourceRootDataOffset));
        Require(result.PackageSha256 == SourcePackageSha256, "Derived F5 geometry closure changed.");
        source.VerifyUnchanged();
        return result;
    }

    internal static NativeJapaneseEggModel DecodeFixture(ReadOnlySpan<byte> owner) => Decode(owner, null, 0);

    private static NativeJapaneseEggModel Decode(ReadOnlySpan<byte> owner, string? sourceHash, int wadOffset)
    {
        Require(owner.Length >= 0x70, "Truncated F5 root/animation owner.");
        Require(Word(owner, 0) == 1 && Word(owner, 0x38) == 0x3C && Word(owner, 0x34) == 0x70,
            "Only the observed single-animation F5 root layout is admitted.");
        for (int i = 4; i < 0x14; i += 4) Require(Word(owner, i) == uint.MaxValue, "Unsupported root metadata.");
        for (int i = 0x14; i < 0x34; i += 4) Require(Word(owner, i) == 0, "Unsupported animation-owned auxiliary pointer.");
        const int animation = 0x3C, modelData = 0x70;
        Require(Half(owner, animation) == 2 && owner[animation + 4] == 0,
            "Only two paired frames, encoding zero, are covered by this F5 contract.");
        int shadingCount = Half(owner, animation + 2);
        int nearCount = owner[animation + 8], farCount = owner[animation + 9];
        Require(shadingCount is > 0 and <= 512 && nearCount > 0 && nearCount == farCount,
            "F5 geometry requires positive equal near/far vertex counts and bounded shading entries.");
        Require(owner[animation + 5] <= 8 && owner[animation + 6] <= 8 &&
            Word(owner, animation + 0x10) == 0, "Unsupported scale or encoding-zero auxiliary data.");
        List<NativeJapaneseEggModelRange> ranges = [], lookAheads = [];
        AddRange(ranges, owner, "root-and-animation-pointer", 0, animation);
        AddRange(ranges, owner, "animation-header-and-two-paired-frames", animation, 0x34);
        int nearFace = Target(owner, animation + 0x14, modelData);
        int farFace = Target(owner, animation + 0x1C, modelData);
        int nearShade = Target(owner, animation + 0x18, modelData);
        int farShade = Target(owner, animation + 0x20, modelData);
        var near = Faces(owner, nearFace, nearCount, shadingCount, "near", ranges, lookAheads);
        var far = Faces(owner, farFace, farCount, shadingCount, "far", ranges, lookAheads);
        uint[] nearShading = Shading(owner, nearShade, shadingCount, "near", ranges, lookAheads);
        uint[] farShading = Shading(owner, farShade, shadingCount, "far", ranges, lookAheads);
        NativeJapaneseEggFrame[] frames = new NativeJapaneseEggFrame[2];
        for (int i = 0; i < frames.Length; i++)
        {
            int field = animation + 0x24 + 8 * i;
            uint packed = Word(owner, field), aux = Word(owner, field + 4);
            Require((aux & 0xFFFF) == 0, "Per-frame face-patch stream is not covered by this F5 contract.");
            int start = checked(modelData + (int)(packed & 0x1FFFFF));
            Require(start % 4 == 0 && start >= modelData, "Unaligned frame stream.");
            int absoluteCount = (int)(aux >> 24);
            Require(absoluteCount > 0 && absoluteCount <= nearCount, "Invalid absolute/delta partition.");
            int absolute = start, deltaStart = checked(start + 4 * absoluteCount), delta = deltaStart;
            List<NativeJapaneseEggVertex> vertices = [];
            int x = 0, y = 0, z = 0, flag = 0, absRead = 0, deltaRead = 0;
            for (int vertex = 0; vertex < nearCount; vertex++)
            {
                int word;
                if (flag == 0)
                {
                    Require(absolute < deltaStart, "Absolute vertex stream crosses its encoded partition.");
                    word = unchecked((int)Word(owner, absolute)); absolute += 4; absRead++;
                    x = word >> 21; y = unchecked(word << 10) >> 21; z = unchecked(word << 20) >> 19;
                }
                else
                {
                    word = unchecked((short)Half(owner, delta)); delta += 2; deltaRead++;
                    int shift = owner[animation + 6] + 1;
                    x += (word >> 11) << shift;
                    y -= (unchecked(word << 21) >> 27) << shift;
                    z -= (unchecked(word << 26) >> 27) << shift;
                }
                vertices.Add(new(x, y, z)); flag = word & 1;
            }
            Require(absRead == absoluteCount, "Frame does not consume exactly its declared absolute partition.");
            int end = Math.Max(absolute, delta);
            AddRange(ranges, owner, $"frame-{i}-mixed-vertices", start, end - start);
            // Native loop preloads one more item before testing its output count.
            // This is a bounded read, NOT an extra vertex. It may alias an owned stream.
            int lookAhead = flag == 0 ? absolute : delta, lookAheadBytes = flag == 0 ? 4 : 2;
            AddRange(lookAheads, owner, $"frame-{i}-pipeline-look-ahead", lookAhead, lookAheadBytes);
            frames[i] = new(i, packed, aux, start, end - start, absRead, deltaRead,
                lookAhead, lookAheadBytes, vertices.AsReadOnly());
        }
        ranges.Sort((a, b) => a.PackageOffset.CompareTo(b.PackageOffset));
        int closedEnd = 0;
        foreach (var range in ranges)
        {
            Require(range.PackageOffset == closedEnd, "F5 owners overlap or leave an unexplained package gap.");
            closedEnd = checked(closedEnd + range.ByteLength);
        }
        int alignedEnd = checked((closedEnd + 3) & ~3);
        Require(alignedEnd <= owner.Length, "Truncated package alignment.");
        for (int i = closedEnd; i < alignedEnd; i++) Require(owner[i] == 0, "Nonzero unowned alignment byte.");
        if (closedEnd != alignedEnd) AddRange(ranges, owner, "word-alignment", closedEnd, alignedEnd - closedEnd);
        foreach (var read in lookAheads)
            Require(read.PackageOffset + read.ByteLength <= closedEnd, "Pipeline look-ahead escapes proved geometry owners.");
        byte[] package = owner[..alignedEnd].ToArray();
        var textures = NativeMobyModelTextureLayout.Decode(package);
        Require(textures.FaceTableOffsets.Count == 2 && textures.References.Count == 0,
            "Unexpected egg texture dependency or face-table layout.");
        return new(package, sourceHash, wadOffset, ranges, lookAheads, frames, near, far,
            nearShading, farShading, owner[animation + 5]);
    }

    private static List<NativeJapaneseEggFace> Faces(ReadOnlySpan<byte> owner, int start, int vertices,
        int shading, string role, List<NativeJapaneseEggModelRange> ranges, List<NativeJapaneseEggModelRange> lookAheads)
    {
        int length = Offset(owner, start);
        Require(length > 0 && length <= 0x20000 && length % 4 == 0, "Invalid face stream length.");
        int end = checked(start + 4 + length);
        Require(end <= owner.Length, "Face stream leaves its model owner.");
        AddRange(ranges, owner, role + "-face-table", start, length + 4);
        AddRange(lookAheads, owner, role + "-face-pipeline-look-ahead", end, 8);
        List<NativeJapaneseEggFace> result = [];
        for (int p = start + 4; p < end;)
        {
            uint word = Word(owner, p), colors = Word(owner, p + 4);
            bool quad = (word & 0x80000000) != 0;
            Require((word & 2) == 0 && (!quad || (word & 4) == 0), "Textured/alternate face command is outside F5 geometry scope.");
            int bytes = quad ? 12 : 8;
            Require(p <= end - bytes, "Face command straddles its length owner.");
            List<int> vi = [(int)(word >> 22 & 511), (int)(word >> 13 & 511), (int)(word >> 4 & 511)];
            List<int> si = [(int)(colors >> 19 & 511), (int)(colors >> 10 & 511), (int)(colors >> 1 & 511)];
            if (quad)
            {
                uint fourth = Word(owner, p + 8);
                vi.Add((int)(fourth >> 2 & 511)); si.Add((int)(fourth >> 11 & 511));
            }
            Require(vi.All(i => i < vertices) && si.All(i => i < shading), "Face indexes an unowned vertex or shading entry.");
            result.Add(new(p, vi.AsReadOnly(), si.AsReadOnly())); p += bytes;
        }
        return result;
    }

    private static uint[] Shading(ReadOnlySpan<byte> owner, int start, int count, string role,
        List<NativeJapaneseEggModelRange> ranges, List<NativeJapaneseEggModelRange> lookAheads)
    {
        AddRange(ranges, owner, role + "-packed-shading-words", start, checked(count * 4));
        AddRange(lookAheads, owner, role + "-shading-pipeline-look-ahead", start + count * 4, 4);
        uint[] words = new uint[count];
        for (int i = 0; i < count; i++) words[i] = Word(owner, start + 4 * i);
        return words;
    }
    private static int Target(ReadOnlySpan<byte> b, int field, int basis)
    {
        int target = checked(Offset(b, field) + basis);
        Require(target % 4 == 0, "Unaligned geometry pointer."); return target;
    }
    private static void AddRange(List<NativeJapaneseEggModelRange> ranges, ReadOnlySpan<byte> owner, string role, int start, int length)
    {
        Require(start >= 0 && length > 0 && start <= owner.Length - length, "Geometry read exceeds declared model subfile.");
        ranges.Add(new(role, start, length, Hash(owner.Slice(start, length))));
    }
    private static int Offset(ReadOnlySpan<byte> b, int p)
    { uint value = Word(b, p); Require(value <= 0x1000000, "Unbounded source offset/length."); return (int)value; }
    private static uint Word(ReadOnlySpan<byte> b, int p)
    { Require(p >= 0 && p <= b.Length - 4, "Truncated geometry word."); return BinaryPrimitives.ReadUInt32LittleEndian(b[p..]); }
    private static ushort Half(ReadOnlySpan<byte> b, int p)
    { Require(p >= 0 && p <= b.Length - 2, "Truncated geometry halfword."); return BinaryPrimitives.ReadUInt16LittleEndian(b[p..]); }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
    private static string Hash(ReadOnlySpan<byte> b) => Convert.ToHexStringLower(SHA256.HashData(b));
}
