using System.Buffers.Binary;
using Spyro.Editor.Core.Analysis;

internal static class EggModelCases
{
    private static void Word(byte[] b, int p, uint v) => BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(p), v);
    private static byte[] Fixture()
    {
        byte[] b = new byte[0xA0]; Word(b, 0, 1);
        for (int i = 4; i < 0x14; i += 4) Word(b, i, uint.MaxValue);
        Word(b, 0x34, 0x70); Word(b, 0x38, 0x3C);
        Word(b, 0x3C, 0x00010002); b[0x42] = 2; b[0x44] = b[0x45] = 3;
        Word(b, 0x50, 0); Word(b, 0x54, 0x18); Word(b, 0x58, 0xC); Word(b, 0x5C, 0x1C);
        Word(b, 0x60, 0xFF000020); Word(b, 0x64, 0x01000000);
        Word(b, 0x68, 0xFF000028); Word(b, 0x6C, 0x01000000);
        Word(b, 0x70, 8); Word(b, 0x74, (1u << 13) | (2u << 4));
        Word(b, 0x7C, 8); Word(b, 0x80, (1u << 13) | (2u << 4));
        Word(b, 0x88, 0xFF010203); Word(b, 0x8C, 0x00010203);
        Word(b, 0x90, 1); Word(b, 0x94, 0x00020003);
        Word(b, 0x98, 1); Word(b, 0x9C, 0x00020003);
        return b;
    }

    public static void Run(Action<bool, string> check)
    {
        void Refuse(Action action, string reason)
        { try { action(); } catch (InvalidDataException) { check(true, "Egg model refusal: " + reason); return; } throw new Exception("Expected egg geometry refusal: " + reason); }
        byte[] fixture = Fixture();
        var model = NativeJapaneseEggModel.DecodeFixture(fixture);
        check(model.PackageByteLength == 0xA0 && model.Frames.Count == 2, "Extent follows owners, not next-root distance.");
        check(!model.SourceGeometryClosureProven && !model.NativeImportApproved && !model.DestinationRuntimeVerified,
            "Structural fixture is not source or import evidence.");
        foreach (var frame in model.Frames)
        {
            check(frame.AbsoluteVertexCount == 1 && frame.DeltaVertexCount == 2 && frame.ByteLength == 8,
                "Paired frame identifies separate absolute/delta streams.");
            check(frame.Vertices.SequenceEqual(new NativeJapaneseEggVertex[] { new(0, 0, 2), new(0, 0, -6), new(0, 0, -14) }),
                "Native signed delta scaling and subtraction are decoded.");
            check(frame.LookAheadByteLength == 4 && frame.LookAheadOffset == frame.PackageOffset + 4,
                "Final pipeline read aliases already-owned bytes without adding a fake vertex.");
        }
        check(model.NearFaces[0].VertexIndices.SequenceEqual(new[] { 0, 1, 2 }) && model.NearPackedShadingWords[0] == 0xFF010203,
            "Faces retain indexed geometry and packed shading words separately.");
        byte[] changed = model.CopyPackage(); changed[0] = 0; fixture[0] = 0;
        check(model.CopyPackage()[0] == 1, "Package is detached and immutable.");
        byte[] trailing = new byte[0x100]; Fixture().CopyTo(trailing, 0); trailing[0xA0] = 0xFF;
        check(NativeJapaneseEggModel.DecodeFixture(trailing).PackageByteLength == 0xA0, "Unowned trailing bytes never enlarge closure.");
        for (int length = 0; length < 0xA0; length++)
        { int captured = length; Refuse(() => NativeJapaneseEggModel.DecodeFixture(Fixture().AsSpan(0, captured)), $"truncated owner length {length:X}"); }
        foreach (var mutation in new (int Offset, uint Word)[]
        {
            (0, 2), (0x14, 4), (0x34, 0x74), (0x38, 0x40), (0x3C, 0x10003),
            (0x40, 1), (0x44, 0), (0x4C, 4), (0x50, 1), (0x54, 0x14), (0x58, 0),
            (0x60, 0xFF000021), (0x64, 0), (0x64, 0x01000001), (0x64, 0x04000000),
            (0x70, 4), (0x74, 2), (0x74, 3u << 22), (0x78, 1u << 19),
            (0x90, 0), (0x9C, 0x00030003)
        })
        {
            byte[] bad = Fixture(); Word(bad, mutation.Offset, mutation.Word);
            Refuse(() => NativeJapaneseEggModel.DecodeFixture(bad), $"field +{mutation.Offset:X} = {mutation.Word:X8}");
        }
        byte[] aliasedLookAhead = Fixture(); Word(aliasedLookAhead, 0x94, 0x00030003);
        check(NativeJapaneseEggModel.DecodeFixture(aliasedLookAhead).Frames[0].LookAheadOffset == 0x98,
            "A frame look-ahead into another proved frame owner is legal, not an invented geometry vertex.");
    }

    public static void VerifySource(NativeRegionalSourceSession source, Action<bool, string> check)
    {
        var model = NativeJapaneseEggModel.Capture(source);
        check(model.SourceGeometryClosureProven && model.SourceImageSha256 == source.SourceImageSha256,
            "Egg geometry is bound to original source and pinned consumers.");
        check(model.PackageByteLength == 0x808 && model.PackageSha256 == NativeJapaneseEggModel.SourcePackageSha256,
            "Exact F5 closure is 2056 bytes, including two zero alignment bytes.");
        check(model.SourceRootWadOffset == 10291036 && model.AnimationCount == 1 && model.ScaleExponent == 0,
            "Exact Artisans F5 registration and scale.");
        check(model.NearFaces.Count == 57 && model.FarFaces.Count == 32 &&
            model.NearFaces.Count(f => f.VertexIndices.Count == 4) == 55 && model.FarFaces.Count(f => f.VertexIndices.Count == 4) == 16,
            "Native near/far triangle/quad grammar closes.");
        check(model.NearPackedShadingWords.Count == 70 && model.FarPackedShadingWords.Count == 70 &&
            model.NearPackedShadingWords[0] == 0xFF6A9230 && model.FarPackedShadingWords[0] == 0x006A9230,
            "Near/far packed shading owners remain distinct.");
        check(model.Frames.All(f => f.Vertices.Count == 82 && f.Vertices[0] == new NativeJapaneseEggVertex(0, 191, -158) &&
            f.Vertices[^1] == new NativeJapaneseEggVertex(-136, -135, -158)), "All native vertices decoded in both frames.");
        check(model.Frames[0].AbsoluteVertexCount == 12 && model.Frames[0].DeltaVertexCount == 70 && model.Frames[0].ByteLength == 0xBC &&
            model.Frames[1].AbsoluteVertexCount == 13 && model.Frames[1].DeltaVertexCount == 69 && model.Frames[1].ByteLength == 0xBE,
            "Native stream partitions close from decoder counts.");
        check(!model.ExternalTextureDependencyPresent && !model.NativeImportApproved && !model.DestinationRuntimeVerified,
            "Untextured source geometry does not approve native behaviour/USA import.");
        check(model.LookAheadReads.Count == 6 && model.Ranges.Sum(r => r.ByteLength) == model.PackageByteLength,
            "Every owned byte and all renderer pipeline look-aheads are accounted for.");
        byte[] package = model.CopyPackage();
        check(NativeJapaneseEggModel.DecodeFixture(package).CopyPackage().SequenceEqual(package), "Exact detached geometry readback.");
        source.VerifyUnchanged();
    }
}
