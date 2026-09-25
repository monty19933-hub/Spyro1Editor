using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;

internal static class SignFontVramCases
{
    internal static void Run(Action<bool, string> check)
    {
        byte[] atlas = new byte[0xA000], table = new byte[0x200];
        var contract = NativeJapaneseSignFontVramContract.CaptureFixture(atlas, table);
        check(contract.Glyphs.Count == 175 && !contract.SourceBound && !contract.CompleteUsaVramLifetimeProven &&
            !contract.UsaInstallationApproved && !contract.PublicationApproved, "Synthetic font geometry is not lifetime or installation authority.");
        VerifyRefusalsAndOwners(contract, check);
        var kana = NativeJapaneseSignFontVramContract.DecodeGlyph(0xC5, 0x0051);
        check(kana.Page == 16 && kana.Clut == 0x7FD6 && kana.ConservativePixelRows.Count == 24 &&
            kana.ConservativePixelRows[0] == new NativeJapaneseFontAtlasRange(16 * 1024 + 40, 9),
            "Transposed カ glyph protects native inclusive UV endpoints, not a wrongly oriented crop.");
        var ni = NativeJapaneseSignFontVramContract.DecodeGlyph(0xD5, 0x0151);
        check(ni.Page == 17 && ni.ConservativePixelRows[0].Offset - kana.ConservativePixelRows[0].Offset == 128,
            "A native page step is64 VRAM words/128 packed bytes, not256 words.");
        var edge = NativeJapaneseSignFontVramContract.DecodeGlyph(0xC5, 0x00F1);
        check(edge.ConservativePixelRows.All(r => r.ByteLength == 8), "Format1 U=F0 uses native15-pixel correction without endpoint wrap.");
        var normal = NativeJapaneseSignFontVramContract.DecodeGlyph(0x80, 0);
        check(normal.ConservativePixelRows.Count == 17 && normal.ConservativePixelRows[0] == new NativeJapaneseFontAtlasRange(0,13),
            "Zero descriptor is admitted only as a defined glyph and protects boundary texels conservatively.");
        var icon = NativeJapaneseSignFontVramContract.DecodeGlyph(0xF7, 0);
        check(icon.Clut == 0x7F96 && icon.Palette == new NativeJapaneseFontAtlasRange(38 * 1024 + 704,32) &&
            normal.Palette == new NativeJapaneseFontAtlasRange(39 * 1024 + 704,32), "Two independent native CLUT rows remain separately owned.");
        atlas[0] = 99; table[0x80 * 2] = 0x04;
        check(contract.CopyNativeAtlas()[0] == 0 && contract.Glyphs.Single(g => g.Code == 0x80).Descriptor == 0,
            "Font atlas and decoded descriptor owners are immutable snapshots.");
        byte[] copy = contract.CopyNativeAtlas(); copy[0] = 42;
        check(contract.CopyNativeAtlas()[0] == 0, "Detached atlas copy cannot mutate the captured native owner.");
        Reject(() => NativeJapaneseSignFontVramContract.CaptureFixture(new byte[0x9FFF], new byte[0x200]));
        Reject(() => NativeJapaneseSignFontVramContract.CaptureFixture(new byte[0xA000], new byte[0x1FE]));
        foreach (ushort bad in new ushort[] { 4, 0x800, 0xFFFB, 0x00F0, 0x00FA })
            Reject(() => NativeJapaneseSignFontVramContract.DecodeGlyph(0x80, bad));
        foreach (byte bad in new byte[] { 0, 0x19, 0x20, 0x40, 0x47, 0xFF })
            Reject(() => NativeJapaneseSignFontVramContract.DecodeGlyph(bad, 0));
        check(NativeJapaneseSignFontVramContract.IntersectionWords(0,472,512,40,512,472,512,40) == 0,
            "Touching half-open VRAM owners do not alias.");
        check(NativeJapaneseSignFontVramContract.IntersectionWords(0,472,512,40,511,511,1,1) == 1,
            "A single shared word at the physical edge is detected.");
        var assembly = new NativeId65VramAssembly(new byte[0x80000]);
        assembly.CopyDetachedRange("synthetic-font-mirror", NativeJapaneseSignFontVramContract.MirroredRightHalfUploadOffset, contract.CopyNativeAtlas());
        check(assembly.CopyOwnership().AsSpan(0x76000,0xA000).IndexOfAnyExcept((byte)2) < 0 &&
            assembly.CopyOwnership().AsSpan(0,0x76000).IndexOfAnyExcept((byte)0) < 0,
            "Existing VRAM assembly can own a synthetic complete mirrored tail without changing its complement.");
        var protectedZero = new NativeId65VramAssembly(new byte[0x80000]);
        protectedZero.CopySourceRange(0x76000,1);
        Reject(() => protectedZero.CopyDetachedRange("not-free",0x76000,contract.CopyNativeAtlas()));
        Reject(() => NativeMobyTextureMaterialization.Compile(new byte[0x80000],new byte[0x80000],[],
            [new("left-half-native-font",0x7FD6,16,0,216,23,231)]));
        check(NativeJapaneseSignFontVramContract.MirroredGpuOperands.Select(p => p.MirroredWord & 0xFFFF)
            .SequenceEqual(new uint[] {0x7FF6,0x7FB6,24}), "Whole-atlas mirror needs two exact CLUT and one page-base operand changes, not guessed per-glyph UVs.");
        Reject(() => NativeJapaneseSignFontVramContract.IntersectionWords(0,472,512,41,0,0,1,1));
        Reject(() => NativeJapaneseSignFontVramContract.IntersectionWords(-1,0,1,1,0,0,1,1));
        Reject(() => NativeJapaneseSignFontVramContract.IntersectionWords(0,0,int.MaxValue,1,0,0,1,1));
        return;
        void Reject(Action action)
        {
            try { action(); } catch (InvalidDataException) { check(true, "Native font owner/range/control refusal."); return; }
            throw new Exception("Expected font VRAM refusal.");
        }
    }

    internal static void VerifySource(NativeRegionalSourceSession japan, NativeRegionalSourceSession usa, Action<bool,string> check)
    {
        var contract = NativeJapaneseSignFontVramContract.Capture(japan, usa);
        check(contract.SourceBound && contract.Glyphs.Count == 175, "All175 admitted native glyphs bind exact dual-revision source evidence.");
        check(Convert.ToHexStringLower(SHA256.HashData(contract.CopyNativeAtlas())) ==
            "917a2c272859d99b573ee3f79b1a62ca847dff218802b2c65f80bb55cb888ff1", "Complete atlas is retained, not reconstructed from only nonzero glyph pixels.");
        VerifyRefusalsAndOwners(contract, check);
        byte[] sourceTable = japan.ReadExecutableRange(0x69DB8, 0x200);
        foreach (var operand in NativeJapaneseSignFontVramContract.MirroredGpuOperands)
            check(BinaryPrimitives.ReadUInt32LittleEndian(japan.ReadExecutableRange(operand.SourcePc - 0x8000F800,4)) == operand.BeforeWord,
                "Proposed mirror operand retains its exact native source preimage; it is not installed.");
        foreach (var glyph in contract.Glyphs)
        {
            check(glyph.Descriptor == BinaryPrimitives.ReadUInt16LittleEndian(sourceTable.AsSpan(glyph.Code * 2)),
                $"Source glyph descriptor remains exact: {glyph.Code:X2}.");
            check(glyph.ConservativePixelRows.All(r => r.Offset >= 0 && r.Offset + r.ByteLength <= 0xA000) &&
                glyph.Palette.Offset >= 0 && glyph.Palette.Offset + 32 <= 0xA000,
                "Every decoded pixel/CLUT byte lies inside the authenticated complete font owner.");
        }
        check(contract.Glyphs.Where(g => g.Code is >= 0xF7 and <= 0xFA).All(g => g.Clut == 0x7F96) &&
            contract.Glyphs.Where(g => g.Code is < 0xF7 or > 0xFA).All(g => g.Clut == 0x7FD6),
            "Native colored-button palette exception is preserved.");
        japan.VerifyUnchanged(); usa.VerifyUnchanged();
    }

    private static void VerifyRefusalsAndOwners(NativeJapaneseSignFontVramContract contract, Action<bool,string> check)
    {
        var overlaps = contract.AssessUnchangedNativePlacement();
        check(overlaps.Where(o => o.Owner.Use == NativeJapaneseFontUsaVramUse.DrawClip).All(o => o.OverlappingWords == 0),
            "Native bottom atlas is outside both observed ordinary draw clips, a geometric fact only.");
        check(overlaps.Single(o => o.Owner.Role == "display-buffer1").OverlappingWords == 4096,
            "Eight rows overlap the second display envelope; border visibility is not certified safe.");
        check(overlaps.Single(o => o.Owner.Role == "graphics-initialization-clear").OverlappingWords == 20480 &&
            overlaps.Single(o => o.Owner.Role == "post-startup-clear").OverlappingWords == 4096,
            "Full and later480-row clears invalidate atlas lifetime even when draw clips do not intersect.");
        check(overlaps.Where(o => o.Owner.Use is NativeJapaneseFontUsaVramUse.LevelUpload or NativeJapaneseFontUsaVramUse.ModalBackdrop
            or NativeJapaneseFontUsaVramUse.ModalPalette).All(o => o.OverlappingWords == 0),
            "Unchanged left-half placement is geometrically separate from the enumerated right-half transfers.");
        foreach (Action refusal in new Action[] { contract.RequireUsaInstallation, contract.RequireExistingRightHalfMaterializerDomain,
            contract.RequireMirroredRightHalfInstallation })
        {
            bool refused = false;
            try { refusal(); } catch (InvalidDataException) { refused = true; }
            check(refused, "Unclosed font lifetime or wrong atlas address domain cannot acquire installation approval.");
        }
    }
}
