using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Spyro.Editor.Core.Analysis;

internal sealed record NativeJapaneseFontAtlasRange(int Offset, int ByteLength);
internal sealed record NativeJapaneseFontGlyphVram(byte Code, ushort Descriptor, ushort Page, ushort Clut,
    IReadOnlyList<NativeJapaneseFontAtlasRange> ConservativePixelRows, NativeJapaneseFontAtlasRange Palette);
internal sealed record NativeJapaneseFontGpuEvidence(uint Pc, int ByteLength, string Sha256, string Role);
internal enum NativeJapaneseFontUsaVramUse { DrawClip, DisplayEnvelope, ResetClear, LevelUpload, ModalBackdrop, ModalPalette }
internal sealed record NativeJapaneseFontUsaVramOwner(NativeJapaneseFontUsaVramUse Use, string Role,
    int WordX, int Y, int WordWidth, int Height);
internal sealed record NativeJapaneseFontVramOverlap(NativeJapaneseFontUsaVramOwner Owner, int OverlappingWords);
internal sealed record NativeJapaneseFontGpuOperandRequirement(uint SourcePc, uint BeforeWord, uint MirroredWord, string Role);

/// <summary>
/// Exact-source geometry and unresolved lifetime contract, not a VRAM allocator.
/// Native JP font coordinates occupy the LEFT half. Existing right-half level
/// texture allocators cannot accept these offsets or certify this lifetime.
/// </summary>
internal sealed class NativeJapaneseSignFontVramContract
{
    internal const int AtlasByteLength = 0xA000;
    // Fixed geometric mirror of the source owner, NOT an allocated free range.
    // It occupies the final40 rows of a complete right-half level upload.
    internal const int MirroredRightHalfUploadOffset = 0x76000;
    private const string AtlasHash = "917a2c272859d99b573ee3f79b1a62ca847dff218802b2c65f80bb55cb888ff1";
    private const string TableHash = "3ebf51c29f337e461de260343b0e11b85eadeec9972b3eefd1afd04b33085913";
    private readonly byte[] _atlas;
    private NativeJapaneseSignFontVramContract(byte[] atlas, byte[] table, bool sourceBound)
    {
        if (atlas.Length != AtlasByteLength || table.Length != 0x200)
            throw new InvalidDataException("Font VRAM requires the complete A000 atlas and 256 native descriptor words.");
        _atlas = atlas.ToArray(); SourceBound = sourceBound;
        List<NativeJapaneseFontGlyphVram> glyphs = [];
        for (int code = 1; code <= 255; code++)
        {
            NativeJapaneseSignMessage token;
            try { token = NativeJapaneseSignMessage.FromNativeBytes([(byte)code]); }
            catch (InvalidDataException) { continue; }
            if (token.Tokens[0].Kind != NativeJapaneseSignTokenKind.Glyph) continue;
            ushort descriptor = BinaryPrimitives.ReadUInt16LittleEndian(table.AsSpan(code * 2));
            glyphs.Add(DecodeGlyph((byte)code, descriptor));
        }
        if (glyphs.Count != 175) throw new InvalidDataException("Native sign glyph admission count changed.");
        Glyphs = Array.AsReadOnly(glyphs.ToArray());
        bool[] owned = new bool[AtlasByteLength];
        foreach (var glyph in glyphs)
        foreach (var range in glyph.ConservativePixelRows.Append(glyph.Palette))
            Array.Fill(owned, true, range.Offset, range.ByteLength);
        ConservativeReferencedAtlasBytes = owned.Count(b => b);
        // These source-proven rectangles are observations, NOT an exhaustive
        // live-owner declaration. In particular a display envelope is not a write.
        UsaOwners = Array.AsReadOnly(new NativeJapaneseFontUsaVramOwner[]
        {
            new(NativeJapaneseFontUsaVramUse.DrawClip,"ordinary-draw-buffer0",0,8,512,224),
            new(NativeJapaneseFontUsaVramUse.DrawClip,"ordinary-draw-buffer1",0,248,512,224),
            new(NativeJapaneseFontUsaVramUse.DisplayEnvelope,"display-buffer0",0,0,512,240),
            new(NativeJapaneseFontUsaVramUse.DisplayEnvelope,"display-buffer1",0,240,512,240),
            new(NativeJapaneseFontUsaVramUse.ResetClear,"graphics-initialization-clear",0,0,512,512),
            new(NativeJapaneseFontUsaVramUse.ResetClear,"post-startup-clear",0,0,512,480),
            new(NativeJapaneseFontUsaVramUse.LevelUpload,"level-upload-first-half",512,0,512,256),
            new(NativeJapaneseFontUsaVramUse.LevelUpload,"level-upload-second-half",512,256,512,256),
            new(NativeJapaneseFontUsaVramUse.ModalBackdrop,"modal-backdrop",512,0,256,225),
            new(NativeJapaneseFontUsaVramUse.ModalPalette,"modal-backdrop-clut",512,224,32,1)
        });
    }

    internal bool SourceBound { get; }
    internal IReadOnlyList<NativeJapaneseFontGlyphVram> Glyphs { get; }
    internal int ConservativeReferencedAtlasBytes { get; }
    internal IReadOnlyList<NativeJapaneseFontUsaVramOwner> UsaOwners { get; }
    internal bool CompleteUsaVramLifetimeProven => false;
    internal bool UsaInstallationApproved => false;
    internal bool PublicationApproved => false;
    internal byte[] CopyNativeAtlas() => _atlas.ToArray();
    internal IReadOnlyList<NativeJapaneseFontVramOverlap> AssessUnchangedNativePlacement() =>
        Array.AsReadOnly(UsaOwners.Select(o => new NativeJapaneseFontVramOverlap(o,
            IntersectionWords(0, 472, 512, 40, o.WordX, o.Y, o.WordWidth, o.Height))).ToArray());

    internal void RequireUsaInstallation() => throw new InvalidDataException(
        "Japanese font installation remains refused: native left-bottom placement overlaps the USA display envelope and reset clears; complete GPU lifetime, visibility, and upload/restore phase ownership are unproved.");

    internal void RequireExistingRightHalfMaterializerDomain() => throw new InvalidDataException(
        "The Japanese atlas and both CLUTs are left-half VRAM owners. Existing 80000-byte right-half materializers cannot reinterpret these offsets; a source-owned UV/tpage/CLUT relocation and complete destination ownership partition are required.");

    internal void RequireMirroredRightHalfInstallation() => throw new InvalidDataException(
        "The geometric right-half mirror is not free storage. It requires a complete final level-upload reservation or modal-exclusive backup/restore with owned A000-byte RAM, GPU drain barriers and all exit/reentry paths; neither lifetime is proved.");

    // A whole-atlas horizontal translation by512 words preserves every UV and
    // glyph descriptor. These are requirements only; no code is rewritten here.
    internal static IReadOnlyList<NativeJapaneseFontGpuOperandRequirement> MirroredGpuOperands { get; } =
        Array.AsReadOnly(new NativeJapaneseFontGpuOperandRequirement[]
        {
            new(0x80022978,0x340C7FD6,0x340C7FF6,"ordinary-CLUT-wordX352-to864"),
            new(0x80022990,0x340C7F96,0x340C7FB6,"controller-CLUT-wordX352-to864"),
            new(0x800229C0,0x20420010,0x20420018,"4bpp-page-base16-to24")
        });

    internal static NativeJapaneseSignFontVramContract Capture(NativeRegionalSourceSession japan, NativeRegionalSourceSession usa)
    {
        ArgumentNullException.ThrowIfNull(japan); ArgumentNullException.ThrowIfNull(usa);
        if (japan.Revision != NativeDiscRevision.JapanScps10083 || usa.Revision != NativeDiscRevision.UsaScus94228)
            throw new InvalidDataException("Font VRAM needs both exact regional source sessions.");
        foreach (var evidence in JapaneseEvidence) Require(japan.ReadExecutableRange(evidence.Pc - 0x8000F800, evidence.ByteLength), evidence.Sha256);
        foreach (var evidence in UsaEvidence) Require(usa.ReadExecutableRange(evidence.Pc - 0x8000F800, evidence.ByteLength), evidence.Sha256);
        if (japan.Entries[2] != new NativeRegionalWadEntry(2, 0x6F800, AtlasByteLength) ||
            usa.Entries[2] != new NativeRegionalWadEntry(2, 0x5B800, 0x3800))
            throw new InvalidDataException("Regional WAD2 identities changed; same index is not the same resource kind.");
        byte[] atlas = japan.ReadWadEntry(2), table = japan.ReadExecutableRange(0x69DB8, 0x200);
        Require(atlas, AtlasHash); Require(table, TableHash);
        // USA slot2 is a loadable overlay, not a smaller equivalent font atlas.
        byte[] usaOverlay = usa.ReadWadEntry(2);
        Require(usaOverlay, "8ee66fe644407bf15f93af1615045fbaca17fe3daa9f3b67f38bee2277a94c7c");
        if (!usaOverlay.AsSpan(4, 17).SequenceEqual("BASCUS-94228SPYRO"u8))
            throw new InvalidDataException("USA WAD2 overlay identity changed.");
        var result = new NativeJapaneseSignFontVramContract(atlas, table, true);
        japan.VerifyUnchanged(); usa.VerifyUnchanged(); return result;
    }

    internal static NativeJapaneseSignFontVramContract CaptureFixture(byte[] atlas, byte[] table) => new(atlas, table, false);

    internal static NativeJapaneseFontGlyphVram DecodeGlyph(byte code, ushort descriptor)
    {
        var token = NativeJapaneseSignMessage.FromNativeBytes([code]);
        if (token.Tokens[0].Kind != NativeJapaneseSignTokenKind.Glyph || descriptor > 0x7FB || (descriptor & 4) != 0)
            throw new InvalidDataException("Font footprint has an undefined glyph or non-native descriptor bits.");
        int kind = descriptor & 3, u = descriptor & 0xF8;
        int width = kind == 0 ? 24 : 23, height = kind == 3 || kind == 1 && u == 0xF0 ? 15 : 16;
        int v = kind == 1 ? 232 : kind == 3 ? 236 : 216;
        int lastU = u + (kind == 1 ? height : width), lastV = v + (kind == 1 ? width : height);
        if (lastU > 255 || lastV > 255) throw new InvalidDataException("Font UV endpoint wraps outside its certified native atlas.");
        ushort page = (ushort)(16 + ((descriptor >> 8) & 7)), clut = (ushort)(code is >= 0xF7 and <= 0xFA ? 0x7F96 : 0x7FD6);
        int byteX = (page - 16) * 128 + u / 2, rowBytes = lastU / 2 - u / 2 + 1;
        List<NativeJapaneseFontAtlasRange> rows = [];
        for (int y = v; y <= lastV; y++)
        {
            int at = (y - 216) * 1024 + byteX;
            if (byteX + rowBytes > 1024 || at < 0 || at + rowBytes > AtlasByteLength)
                throw new InvalidDataException("Font glyph footprint leaves its exact atlas owner.");
            rows.Add(new(at, rowBytes));
        }
        int paletteAt = ((clut >> 6) - 472) * 1024 + (clut & 63) * 32;
        if (paletteAt < 0 || paletteAt + 32 > AtlasByteLength)
            throw new InvalidDataException("Font palette footprint leaves its exact atlas owner.");
        return new(code, descriptor, page, clut, Array.AsReadOnly(rows.ToArray()), new(paletteAt, 32));
    }

    internal static int IntersectionWords(int x, int y, int width, int height, int ox, int oy, int ow, int oh)
    {
        foreach (var r in new[] { (x, y, width, height), (ox, oy, ow, oh) })
            if (r.Item1 < 0 || r.Item2 < 0 || r.Item3 <= 0 || r.Item4 <= 0 ||
                r.Item1 > 1024 - r.Item3 || r.Item2 > 512 - r.Item4)
                throw new InvalidDataException("VRAM rectangle leaves the bounded 1024-word by512-row domain.");
        return Math.Max(0, Math.Min(x + width, ox + ow) - Math.Max(x, ox)) *
            Math.Max(0, Math.Min(y + height, oy + oh) - Math.Max(y, oy));
    }

    internal static IReadOnlyList<NativeJapaneseFontGpuEvidence> JapaneseEvidence { get; } = Array.AsReadOnly(new NativeJapaneseFontGpuEvidence[]
    {
        new(0x800120F0,0x54,"6fcc7c159e789ef4a3131119db6b8740ff8926e56e8e680a239eb1d321584ee7","font-source-read-and-left-half-upload"),
        new(0x80022868,0x2D4,"592e7b33f9ed7f898b0ce475a8567aad7d6cd69c7c28a14c9c30cc078d799820","sign-mode-glyph-UV-page-CLUT-consumer")
    });
    internal static IReadOnlyList<NativeJapaneseFontGpuEvidence> UsaEvidence { get; } = Array.AsReadOnly(new NativeJapaneseFontGpuEvidence[]
    {
        new(0x800122D0,0x70,"528c73e18f317432f3435e863ba7dab55a703771c1b283dbaf3f33c4e3c282fb","ordinary-draw-and-display-rectangles"),
        new(0x8005EA94,0xB8,"806bba74974d07db50dc9fdd5233a51b4edb6580d71233e6ac5895517eefe07c","draw-environment-rectangle-ABI"),
        new(0x8005EB4C,0x3C,"306767473184a5a95ed394bd5ae11eb8900b499dab0154a66d45ca0a729aca41","display-environment-rectangle-ABI"),
        new(0x80012380,0x28,"1bc564413cf9590f337801105a0379087e0cbd62f1ac86f4b8180d8b14b98f84","full-left-half-initialization-clear"),
        new(0x80012BF0,0x54,"56b422867b4fddf26696a7a93e358fcbb6381fd7f1a2afaca4e1b40eb9d2785c","post-startup-480-row-clear"),
        new(0x80015770,0x60,"83f1b67457123d22b554498e5d8e65611455c826528664862599e022e9d9e7d6","two-complete-right-half-level-upload-halves"),
        new(0x8001A4DC,0xE4,"e4ea30696355ed3c3386932c2e9f152fb9ee0dc2c476995c7db76fe3f20e6478","modal-right-half-backdrop-and-palette"),
        new(0x8005FA28,0x64,"18db14f842d5a903afb21394f277e4f3cbe92358f111c16e4767ba9c21119713","native-GPU-upload-queue-service"),
        new(0x8005FA8C,0x64,"8952ec7779ea4c783a6576c4df3d08acd05470acdcca263d322005c84cea3053","native-GPU-readback-queue-service"),
        new(0x8005F764,0x6C,"0e573f70016f10b87ac196ff7fa304e28d16549d9b1c5ef11f756f248e12689d","native-draw-synchronization-service"),
        new(0x8001250C,0x60,"2b41ac823f87f3c223917d8da7eff67e5edc4e6b7837b5cf3c4989534f2ae444","USA-WAD-directory-runtime-copy"),
        new(0x80012978,0x24,"ef93a92b27c18a7ea508bd01646708366321f4af7994b27ef34da67d5455dd49","USA-slot2-overlay-not-font")
    });
    private static void Require(byte[] bytes, string hash)
    {
        if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != hash)
            throw new InvalidDataException("Font VRAM source consumer/resource certificate changed.");
    }
}
