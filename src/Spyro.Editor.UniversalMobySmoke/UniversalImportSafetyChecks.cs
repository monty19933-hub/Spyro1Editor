using System.Buffers.Binary;
using Spyro.Editor.Core.Universal;
using Spyro.Editor.Core.Levels;

internal static class UniversalImportSafetyChecks
{
    public static void Run(Action<bool, string> check)
    {
        bool invalidLedger = false;
        try
        {
            UniversalMobyComposer.Compose(new LevelDefinition(), [], [], [], _ => throw new Exception("source must not be read"),
                new HashSet<int>(), [], [new("", -1, 0, 0, 0, EditorId: "same", PersistentSlot: 0, Retired: true),
                    new("", -1, 0, 0, 0, EditorId: "same", PersistentSlot: 0, Retired: true)]);
        }
        catch (InvalidDataException) { invalidLedger = true; }
        check(invalidLedger, "duplicate lifetime slots and IDs are refused before source assets are read");
        var alloc = new UniversalVramAllocator([]);
        alloc.Reserve(1024 + 20, 8);
        check(alloc.FreeBytes == UniversalVramAllocator.ImageBytes - 8, "explicit texture reuse reservation protects zero bytes");
        alloc.Reserve(1024 + 20, 8);
        check(alloc.FreeBytes == UniversalVramAllocator.ImageBytes - 8, "overlapping ownership reservation is idempotent");
        bool badRange = false;
        try { alloc.Reserve(-1, 8); } catch (InvalidDataException) { badRange = true; }
        check(badRange, "out-of-VRAM reservation is refused");
        byte[] src = new byte[UniversalVramAllocator.ImageBytes], dst = src.ToArray();
        byte[] pkg = TexturedPackage();
        var textureAlloc = new UniversalVramAllocator([]);
        var report = UniversalAssetImport.ImportTextures(pkg, src, dst, textureAlloc);
        check(report.Reused == 1 && report.PixelBytes == 0 && textureAlloc.FreeBytes == UniversalVramAllocator.ImageBytes - 48,
            "identical all-zero texture reserves both 16 pixel bytes and 32 palette bytes");
        var clone = textureAlloc.Clone();
        clone.Reserve(4000, 8);
        check(textureAlloc.FreeBytes - clone.FreeBytes == 8, "allocator clones keep independent ownership maps");
        byte[] malformed = TexturedPackage();
        BinaryPrimitives.WriteInt32LittleEndian(malformed.AsSpan(0x64), 19);
        bool badFace = false;
        try { UniversalAssetImport.TextureDescriptorOffsets(malformed); } catch (InvalidDataException) { badFace = true; }
        check(badFace, "truncated face commands are refused instead of producing incomplete texture imports");
        byte[] badRoot = TexturedPackage();
        BinaryPrimitives.WriteInt32LittleEndian(badRoot.AsSpan(0x38), int.MaxValue);
        bool badAnimation = false;
        try { UniversalAssetImport.TextureDescriptorOffsets(badRoot); } catch (InvalidDataException) { badAnimation = true; }
        check(badAnimation, "invalid animation roots are refused instead of skipped");
        foreach (int depth in new[] { 0, 1, 2 }) TextureReadback(depth, check);
        ParticleRecordReadback(check);
        byte[] crossing = TexturedPackage();
        crossing[0x70] = crossing[0x78] = 0; crossing[0x74] = crossing[0x7A] = 255;
        BinaryPrimitives.WriteUInt16LittleEndian(crossing.AsSpan(0x76), 0x8F);
        bool crossingRefused = false;
        try { UniversalAssetImport.ImportTextures(crossing, src, dst, new UniversalVramAllocator([])); }
        catch (InvalidDataException) { crossingRefused = true; }
        check(crossingRefused, "8-bpp source rectangles crossing the VRAM half cannot wrap into the next row");
        check(UniversalMobyCoverage.FailureKind(new UniversalSourceCollisionException("collision")) == "source-class-collision" &&
            UniversalMobyCoverage.FailureKind(new InvalidDataException("non-4bpp texture")) == "unsupported-texture-depth",
            "coverage separates class identity conflicts from unsupported texture formats");
    }

    private static void ParticleRecordReadback(Action<bool, string> check)
    {
        int f0 = 0x800, f1 = f0 + 0x80800, f2 = f1 + 0x800, f3 = f2 + 0x800;
        byte[] bytes = new byte[f3 + 0x800];
        int[] offsets = [f0, f1, f2, f3], lengths = [0x80800, 0x800, 0x800, 0x800];
        for (int i = 0; i < 4; i++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(i * 8), offsets[i]);
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(i * 8 + 4), lengths[i]);
        }
        // A complete native level owns a model root and an SPU bank. Particle
        // insertion now validates the following sound component's exact extent.
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x50), f2);
        bytes[f0 + 0x80000 + 1] = 1; // one terminating silent ADPCM frame
        for (int i = 0; i < 6; i++) BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(f1 + i * 4), 4);
        int component = f1 + 28;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(component), 32);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(component + 4), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(component + 8), 7);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(component + 10), 8);
        TexturedPackage().AsSpan(0x70, 8).CopyTo(bytes.AsSpan(component + 12));
        int sound = component + 32;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(sound), 0x11C);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(sound + 0x104), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(sound + 0x112), 0x1000);
        byte[] soundBefore = bytes.AsSpan(sound, 0x11C).ToArray();
        var data = new UniversalLevelData(bytes);
        byte[] pkg = UniversalParticleDependencies.TexturePackage(TexturedPackage().AsSpan(0x70, 8).ToArray());
        UniversalParticleDependencies.AppendTexture(data, 15, pkg);
        var records = UniversalParticleDependencies.Records(data);
        check(records.Count == 2 && records[7].AsSpan().SequenceEqual(records[15]) &&
            BinaryPrimitives.ReadInt32LittleEndian(data.Bytes.AsSpan(component)) == 44 &&
            data.Bytes.AsSpan(component + 44, soundBefore.Length).SequenceEqual(soundBefore),
            "particle record append preserves count, component padding and the entire following sound component");
        bool duplicate = false;
        try { UniversalParticleDependencies.AppendTexture(data, 15, pkg); } catch (InvalidDataException) { duplicate = true; }
        check(duplicate, "particle imports refuse overwriting an existing destination type");
        check(UniversalParticleDependencies.EquivalentTexture(records[7], data.Vram(), records[15], data.Vram()),
            "particle texture equivalence checks matching UV, pixels and complete palette");
    }

    private static void TextureReadback(int depth, Action<bool, string> check)
    {
        byte[] pkg = TexturedPackage(), source = new byte[UniversalVramAllocator.ImageBytes], dest = source.ToArray();
        // A full-width page checks that 8/16bpp regions can cross 128-byte 4bpp columns.
        pkg[0x70] = 0; pkg[0x74] = 255; pkg[0x78] = 0; pkg[0x7A] = 255;
        BinaryPrimitives.WriteUInt16LittleEndian(pkg.AsSpan(0x76), (ushort)(8 | 0x20 | (depth << 7)));
        int width = 128 << depth;
        for (int y = 10; y <= 13; y++)
            for (int x = 0; x < width; x++) source[y * 1024 + x] = (byte)(1 + ((x + y) % 250));
        int paletteBytes = depth == 0 ? 32 : depth == 1 ? 512 : 0;
        for (int x = 0; x < paletteBytes; x++) source[300 * 1024 + x] = (byte)(x % 251);
        var report = UniversalAssetImport.ImportTextures(pkg, source, dest, new UniversalVramAllocator([]));
        int page = BinaryPrimitives.ReadUInt16LittleEndian(pkg.AsSpan(0x76)), pal = BinaryPrimitives.ReadUInt16LittleEndian(pkg.AsSpan(0x72));
        int x0 = (page & 15) * 128 - 1024 + (depth == 0 ? pkg[0x70] / 2 : pkg[0x70] << (depth - 1));
        int y0 = ((page & 16) != 0 ? 256 : 0) + pkg[0x71];
        bool pixelsMatch = true;
        for (int y = 0; y < 4; y++) pixelsMatch &= source.AsSpan((10 + y) * 1024, width).SequenceEqual(dest.AsSpan((y0 + y) * 1024 + x0, width));
        int clut = (pal >> 6) * 1024 + (pal & 63) * 32 - 1024;
        bool paletteMatch = paletteBytes == 0 || source.AsSpan(300 * 1024, paletteBytes).SequenceEqual(dest.AsSpan(clut, paletteBytes));
        check(pixelsMatch && paletteMatch && ((page >> 7) & 3) == depth && (page & 0x60) == 0x20 &&
            report.PixelBytes == width * 4 && report.Cluts == (depth == 2 ? 0 : 1),
            $"{4 << depth}-bpp full-page texture and palette survive relocation readback with blend/depth flags");
    }

    private static byte[] TexturedPackage()
    {
        byte[] p = new byte[0x80];
        BinaryPrimitives.WriteInt32LittleEndian(p, 1);
        Array.Fill(p, (byte)0xFF, 4, 16);
        BinaryPrimitives.WriteInt32LittleEndian(p.AsSpan(0x34), 0x64);
        BinaryPrimitives.WriteInt32LittleEndian(p.AsSpan(0x38), 0x40);
        BinaryPrimitives.WriteInt32LittleEndian(p.AsSpan(0x64), 20);
        BinaryPrimitives.WriteInt32LittleEndian(p.AsSpan(0x68), 2);
        p[0x70] = 64; p[0x71] = 10; p[0x74] = 71; p[0x75] = 13;
        p[0x78] = 64; p[0x79] = 13; p[0x7A] = 71; p[0x7B] = 10;
        BinaryPrimitives.WriteUInt16LittleEndian(p.AsSpan(0x72), (ushort)((300 << 6) | 32));
        BinaryPrimitives.WriteUInt16LittleEndian(p.AsSpan(0x76), 8);
        return p;
    }
}
