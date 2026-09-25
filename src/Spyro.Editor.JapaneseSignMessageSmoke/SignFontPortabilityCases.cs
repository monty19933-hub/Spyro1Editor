using System.Buffers.Binary;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;

internal static class SignFontPortabilityCases
{
    internal static void Run(Action<bool, string> check)
    {
        (byte[] width, byte[] glyph, byte[] colors, byte[] descriptors) = Fixture();
        var source = NativeJapaneseSignFontLeafSource.CaptureFixture(width, glyph, colors, descriptors);
        var domain = new NativeAddressSpace(NativeAddressKind.LevelData, "font-test-final", NativeAddressRevision.Final, 0x1000);
        var owner = new NativeByteExtent(domain, 0x100, NativeJapaneseSignFontLeafSource.CpuPlanByteLength);
        foreach (uint basis in new uint[] { 0x800C0000, 0x800D7C60, 0x801F0000 })
        {
            var plan = source.CompileCpu(owner, basis);
            Verify(plan, width, glyph, colors, descriptors, check);
            check(!plan.SourceBound && !plan.CompleteNativeClosureProven && !plan.VramInstalled &&
                !plan.RuntimeFitProven && !plan.PublicationApproved, "Detached synthetic CPU plan is not installation approval.");
            byte[] copy = plan.CopyBytes(); copy[0] ^= 1;
            check(copy[0] != plan.CopyBytes()[0], "Font plan does not expose mutable output storage.");
        }
        byte[] retained = source.CompileCpu(owner, 0x800C0000).CopyBytes();
        width[0] ^= 1; glyph[0] ^= 1; colors[0] ^= 1; descriptors[0] ^= 1;
        check(retained.SequenceEqual(source.CompileCpu(owner, 0x800C0000).CopyBytes()), "Font capture owns all four input snapshots.");
        Refuse(() => source.CompileCpu(owner, 0x800C0001));
        Refuse(() => source.CompileCpu(owner, 0x801FFF00));
        Refuse(() => source.CompileCpu(owner, 0x7FFFFFFC));
        Refuse(() => source.CompileCpu(owner, 0x800757B0));
        Refuse(() => source.CompileCpu(owner, 0x8007581C));
        Refuse(() => source.CompileCpu(new(domain, 0x100, owner.ByteLength + 4), 0x800C0000));
        Refuse(() => source.CompileCpu(new(domain, 0x102, owner.ByteLength), 0x800C0000));
        var original = new NativeAddressSpace(NativeAddressKind.LevelData, "original", NativeAddressRevision.Original, 0x1000);
        Refuse(() => source.CompileCpu(new(original, 0, owner.ByteLength), 0x800C0000));
        var wrongKind = new NativeAddressSpace(NativeAddressKind.ExecutableFile, "wrong-kind", NativeAddressRevision.Final, 0x1000);
        Refuse(() => source.CompileCpu(new(wrongKind, 0, owner.ByteLength), 0x800C0000));
        (width, glyph, colors, descriptors) = Fixture();
        foreach (int offset in new int[] { 0x1C, 0x20, 0x30, 0x34, 0x3C, 0x40, 0x48, 0x4C, 0xE8, 0xEC, 0x2C0, 0x2C4 })
        {
            byte[] changed = glyph.ToArray(); changed[offset] ^= 1;
            Refuse(() => NativeJapaneseSignFontLeafSource.CaptureFixture(width, changed, colors, descriptors));
        }
        byte[] unclosed = glyph.ToArray(); Put(unclosed, 0, 0x08000400); Put(unclosed, 4, 0);
        Refuse(() => NativeJapaneseSignFontLeafSource.CaptureFixture(width, unclosed, colors, descriptors));
        byte[] fallthrough = glyph.ToArray(); Put(fallthrough, 0x2CC, 0);
        Refuse(() => NativeJapaneseSignFontLeafSource.CaptureFixture(width, fallthrough, colors, descriptors));
        Refuse(() => NativeJapaneseSignFontLeafSource.CaptureFixture(width[..^4], glyph, colors, descriptors));
        Refuse(() => NativeJapaneseSignFontLeafSource.CaptureFixture(width, glyph, colors[..^1], descriptors));
        return;
        void Refuse(Action action)
        {
            try { action(); } catch (InvalidDataException) { check(true, "Font typed owner/operand refusal."); return; }
            throw new Exception("Expected font portability refusal.");
        }
    }

    internal static void VerifySource(NativeRegionalSourceSession japan, NativeRegionalSourceSession usa, Action<bool, string> check)
    {
        var source = NativeJapaneseSignFontLeafSource.Capture(japan, usa);
        var domain = new NativeAddressSpace(NativeAddressKind.LevelData, "font-source-detached-test", NativeAddressRevision.Final, 0x1000);
        var plan = source.CompileCpu(new(domain, 0x100, NativeJapaneseSignFontLeafSource.CpuPlanByteLength), 0x800D7C60);
        Verify(plan, japan.ReadExecutableRange(0x7EF0, 0xD0), japan.ReadExecutableRange(0x13068, 0x2D4),
            japan.ReadExecutableRange(0x69D98, 0x20), japan.ReadExecutableRange(0x69DB8, 0x200), check);
        check(plan.SourceBound && !plan.PublicationApproved && !plan.RuntimeFitProven && !plan.VramInstalled,
            "Exact dual-revision capture does not promote font plan to native sign installation.");
        japan.VerifyUnchanged(); usa.VerifyUnchanged();
    }

    private static void Verify(NativeJapaneseSignFontCpuPlan plan, byte[] width, byte[] glyph,
        byte[] colors, byte[] descriptors, Action<bool, string> check)
    {
        byte[] output = plan.CopyBytes();
        check(output.Length == 0x5C4 && plan.SourceToFinalPc.Count == 233 && plan.OperandPatches.Count == 12,
            "Font CPU leaf has 932 code bytes, 544 table bytes, and twelve typed operand words.");
        check(output.AsSpan(0x3A4, 0x20).SequenceEqual(colors) && output.AsSpan(0x3C4, 0x200).SequenceEqual(descriptors),
            "Complete Japanese immutable tables have materialized owners.");
        var patches = plan.OperandPatches.ToDictionary(p => p.SourcePc);
        foreach ((uint pc, byte[] input) in new[] { (0x800176F0u, width), (0x80022868u, glyph) })
        for (int at = 0; at < input.Length; at += 4)
        {
            uint sourcePc = pc + (uint)at, finalPc = plan.SourceToFinalPc[sourcePc], before = Word(input, at), expected = before;
            if (patches.TryGetValue(sourcePc, out var patch))
            {
                check(patch.BeforeWord == before && patch.FinalPc == finalPc &&
                    (patch.AfterWord & 0xFFFF0000) == (before & 0xFFFF0000), "Typed address operands retain exact register/opcode preimages.");
                expected = patch.AfterWord;
            }
            else if (before >> 26 is 2 or 3)
            {
                uint target = ((sourcePc + 4) & 0xF0000000) | ((before & 0x3FFFFFF) << 2);
                expected = (before & 0xFC000000) | (plan.SourceToFinalPc[target] >> 2 & 0x3FFFFFF);
            }
            else if (before >> 26 is >= 1 and <= 7)
            {
                uint target = unchecked(sourcePc + 4 + (uint)((short)before * 4));
                int delta = checked((int)((long)plan.SourceToFinalPc[target] - finalPc - 4));
                expected = (before & 0xFFFF0000) | (ushort)(short)(delta / 4);
            }
            check(Word(output, checked((int)(finalPc - plan.RuntimeBase))) == expected,
                "Independent source-to-final oracle preserves every non-relocation instruction and delay word.");
        }
        foreach (var high in plan.OperandPatches.Where(p => p.BeforeWord >> 26 == 15))
        {
            var low = patches[high.SourcePc + 4];
            uint decoded = unchecked((high.AfterWord << 16) + (uint)(short)low.AfterWord);
            check(decoded == high.BoundAddress && low.BoundAddress == decoded, "Signed ADDIU high-half carry resolves the complete typed CPU owner.");
            uint expected = high.Role switch
            {
                NativeJapaneseFontOperandRole.Colors => plan.RuntimeBase + 0x3A4,
                NativeJapaneseFontOperandRole.GlyphDescriptors => plan.RuntimeBase + 0x3C4,
                NativeJapaneseFontOperandRole.PrimitiveCursor => 0x800757B0,
                NativeJapaneseFontOperandRole.UiPrimitiveQueue => 0x8007581C,
                _ => throw new Exception("Unexpected font role.")
            };
            check(decoded == expected, "Native UI queue is not substituted by the world ordering table.");
        }
    }

    private static (byte[] Width, byte[] Glyph, byte[] Colors, byte[] Descriptors) Fixture()
    {
        byte[] width = new byte[0xD0], glyph = new byte[0x2D4];
        Put(width, 0, 0x03E00008); Put(width, 0xA8, 0x27BDFFF0); Put(width, 0xAC, 0xAFBF000C);
        Put(width, 0xB0, 0x0C005DBC); Put(width, 0xB8, 0x8FBF000C); Put(width, 0xC0, 0x27BD0010); Put(width, 0xC8, 0x03E00008);
        Put(glyph, 0, 0x10000002); Put(glyph, 0x2CC, 0x03E00008);
        // Synthetic instructions only: the fixture does not embed the native routine.
        foreach (var (offset, hi, lo) in new (int, uint, uint)[]
        {
            (0x1C,0x3C018008,0x24219598),(0x30,0x3C0B8008,0x256B95B8),
            (0x3C,0x3C188008,0x2718F328),(0x48,0x3C198008,0x2739F3AC),
            (0xE8,0x3C078008,0x24E79598),(0x2C0,0x3C018008,0x2421F328)
        }) { Put(glyph, offset, hi); Put(glyph, offset + 4, lo); }
        return (width, glyph, Enumerable.Range(0, 32).Select(i => (byte)i).ToArray(),
            Enumerable.Range(0, 512).Select(i => (byte)(i * 13)).ToArray());
    }
    private static uint Word(ReadOnlySpan<byte> b, int at) => BinaryPrimitives.ReadUInt32LittleEndian(b[at..]);
    private static void Put(byte[] b, int at, uint word) => BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(at), word);
}
