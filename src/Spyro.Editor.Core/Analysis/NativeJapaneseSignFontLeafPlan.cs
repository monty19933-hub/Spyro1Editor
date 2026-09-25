using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Analysis;

internal enum NativeJapaneseFontOperandRole { Colors, GlyphDescriptors, PrimitiveCursor, UiPrimitiveQueue }
internal sealed record NativeJapaneseFontOperandPatch(NativeJapaneseFontOperandRole Role,
    uint SourcePc, uint FinalPc, uint BeforeWord, uint AfterWord, uint BoundAddress);

/// <summary>
/// Detached CPU-only experiment. The two data tables have materialized owners;
/// USA mutable-global operands have exact native consumers, not new storage.
/// No VRAM allocation/upload, primitive capacity, lifetime, hook, or publication
/// authority follows from this plan. The caller must not install it as a sign.
/// </summary>
internal sealed class NativeJapaneseSignFontCpuPlan
{
    private readonly byte[] _bytes;
    internal NativeJapaneseSignFontCpuPlan(byte[] bytes, NativeByteExtent owner, uint runtimeBase,
        IReadOnlyDictionary<uint, uint> mapping, NativeJapaneseFontOperandPatch[] patches, bool sourceBound)
    {
        _bytes = bytes.ToArray(); Owner = owner; RuntimeBase = runtimeBase; SourceBound = sourceBound;
        SourceToFinalPc = new System.Collections.ObjectModel.ReadOnlyDictionary<uint, uint>(new Dictionary<uint, uint>(mapping));
        OperandPatches = Array.AsReadOnly(patches.ToArray());
    }
    internal NativeByteExtent Owner { get; }
    internal uint RuntimeBase { get; }
    internal bool SourceBound { get; }
    internal IReadOnlyDictionary<uint, uint> SourceToFinalPc { get; }
    internal IReadOnlyList<NativeJapaneseFontOperandPatch> OperandPatches { get; }
    internal byte[] CopyBytes() => _bytes.ToArray();
    internal bool CompleteNativeClosureProven => false;
    internal bool VramInstalled => false;
    internal bool RuntimeFitProven => false;
    internal bool PublicationApproved => false;
}

/// <summary>Code-owned, exact-revision capture of the smallest complete CPU font leaf.</summary>
internal sealed class NativeJapaneseSignFontLeafSource
{
    internal const int CodeByteLength = 0x3A4;
    internal const int ColorsOffset = CodeByteLength;
    internal const int GlyphDescriptorsOffset = ColorsOffset + 0x20;
    internal const int CpuPlanByteLength = GlyphDescriptorsOffset + 0x200;
    private const uint ExeBase = 0x8000F800;
    private const uint WidthPc = 0x800176F0, GlyphPc = 0x80022868;
    private const string WidthHash = "4690d076e4549472b14435a12a3208d1b34fc734bf7a4f9820ee453ff630ef2b";
    private const string GlyphHash = "592e7b33f9ed7f898b0ce475a8567aad7d6cd69c7c28a14c9c30cc078d799820";
    private const string ColorsHash = "6f267eba5b81e5ae2abd5a82d393938a6be96b1db08bf1bd56bcee67b6837f75";
    private const string DescriptorsHash = "3ebf51c29f337e461de260343b0e11b85eadeec9972b3eefd1afd04b33085913";
    private const string AtlasHash = "917a2c272859d99b573ee3f79b1a62ca847dff218802b2c65f80bb55cb888ff1";
    private readonly NativeMipsBehaviorFragmentPackage _package;
    private readonly byte[] _colors, _descriptors;
    private readonly bool _sourceBound;

    // Each pair is an observed LUI followed by signed ADDIU, not an ORI pair.
    // Duplicate semantic references remain separately certified instruction sites.
    private static readonly (NativeJapaneseFontOperandRole Role, uint Pc, uint Hi, uint Lo)[] Pairs =
    [
        (NativeJapaneseFontOperandRole.Colors, 0x80022884, 0x3C018008, 0x24219598),
        (NativeJapaneseFontOperandRole.GlyphDescriptors, 0x80022898, 0x3C0B8008, 0x256B95B8),
        (NativeJapaneseFontOperandRole.PrimitiveCursor, 0x800228A4, 0x3C188008, 0x2718F328),
        (NativeJapaneseFontOperandRole.UiPrimitiveQueue, 0x800228B0, 0x3C198008, 0x2739F3AC),
        (NativeJapaneseFontOperandRole.Colors, 0x80022950, 0x3C078008, 0x24E79598),
        (NativeJapaneseFontOperandRole.PrimitiveCursor, 0x80022B28, 0x3C018008, 0x2421F328)
    ];

    private NativeJapaneseSignFontLeafSource(byte[] width, byte[] glyph, byte[] colors, byte[] descriptors, bool sourceBound)
    {
        if (width.Length != 0xD0 || glyph.Length != 0x2D4 || colors.Length != 0x20 || descriptors.Length != 0x200)
            throw new InvalidDataException("Font leaf owners have a different complete extent.");
        _colors = colors.ToArray(); _descriptors = descriptors.ToArray(); _sourceBound = sourceBound;
        byte[] source = new byte[0x70000]; width.CopyTo(source, (int)(WidthPc - ExeBase)); glyph.CopyTo(source, (int)(GlyphPc - ExeBase));
        var domain = new NativeAddressSpace(NativeAddressKind.ExecutableFile,
            sourceBound ? "jp-font-leaf-exact-scps10083" : "jp-font-leaf-synthetic-fixture", NativeAddressRevision.Original, source.Length);
        byte[] Read(NativeByteExtent e) => e.Space == domain
            ? source.AsSpan(checked((int)e.Offset), checked((int)e.ByteLength)).ToArray()
            : throw new InvalidDataException("Unbound font source address domain.");
        _package = NativeMipsBehaviorFragmentCodec.Capture(
            [new("measure-and-center", WidthPc, new("width", new(domain, WidthPc - ExeBase, width.Length), Hash(width))),
             new("draw-glyphs", GlyphPc, new("glyph", new(domain, GlyphPc - ExeBase, glyph.Length), Hash(glyph)))],
            [WidthPc, 0x80017798, GlyphPc], [], Read);
        if (_package.InstructionCount != CodeByteLength / 4 || _package.Fragments.Any(f => f.HasFallthrough) ||
            _package.Transfers.Any(t => t.Kind != NativeMipsBehaviorTransferKind.ReturnRegister &&
                (t.TargetPc is not uint target || !_package.OwnsPc(target))))
            throw new InvalidDataException("Font CPU leaf has an unclosed transfer or changed instruction extent.");
        foreach (var pair in Pairs)
            if (Word(glyph, (int)(pair.Pc - GlyphPc)) != pair.Hi || Word(glyph, (int)(pair.Pc - GlyphPc + 4)) != pair.Lo)
                throw new InvalidDataException("Font semantic operand preimage changed.");
    }

    internal static NativeJapaneseSignFontLeafSource Capture(NativeRegionalSourceSession japan, NativeRegionalSourceSession usa)
    {
        ArgumentNullException.ThrowIfNull(japan); ArgumentNullException.ThrowIfNull(usa);
        if (japan.Revision != NativeDiscRevision.JapanScps10083 || usa.Revision != NativeDiscRevision.UsaScus94228)
            throw new InvalidDataException("Font portability requires distinct exact Japanese and USA source sessions.");
        byte[] Read(NativeRegionalSourceSession s, uint pc, int length, string hash)
        {
            byte[] value = s.ReadExecutableRange(pc - ExeBase, length);
            if (Hash(value) != hash) throw new InvalidDataException("Font native consumer or resource preimage changed.");
            return value;
        }
        byte[] width = Read(japan, WidthPc, 0xD0, WidthHash), glyph = Read(japan, GlyphPc, 0x2D4, GlyphHash);
        byte[] colors = Read(japan, 0x80079598, 0x20, ColorsHash), descriptors = Read(japan, 0x800795B8, 0x200, DescriptorsHash);
        // This is the UI queue head/tail service, NOT the world ordering table at 80075820.
        _ = Read(usa, 0x800168DC, 0x38, "1eb9f092558eb96c40be2ab0c9b60716c434e5e51eba954348dc526856800110");
        _ = Read(japan, 0x80015EC0, 0x38, "cd0b30f8efebfc24f3804bf75f6e7e45ee5f2a035b9a2139a76c3511ae636800");
        _ = Read(usa, 0x80058070, 0x70, "b04980d58c31a81ec8d35699e36d0e53bd77b1ac993e86c021adba6a1c12c08e");
        var atlasOwner = japan.Entries[2];
        if (atlasOwner.Offset != 0x6F800 || atlasOwner.ByteLength != 0xA000 || Hash(japan.ReadWadEntry(2)) != AtlasHash)
            throw new InvalidDataException("Native Japanese font atlas owner changed.");
        var result = new NativeJapaneseSignFontLeafSource(width, glyph, colors, descriptors, true);
        japan.VerifyUnchanged(); usa.VerifyUnchanged(); return result;
    }

    // Fixture entry point is internal and can never issue SourceBound=true.
    internal static NativeJapaneseSignFontLeafSource CaptureFixture(byte[] width, byte[] glyph, byte[] colors, byte[] descriptors) =>
        new(width, glyph, colors, descriptors, false);

    internal NativeJapaneseSignFontCpuPlan CompileCpu(NativeByteExtent owner, uint runtimeBase)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (owner.Space.Revision != NativeAddressRevision.Final || owner.Space.Kind != NativeAddressKind.LevelData ||
            owner.ByteLength != CpuPlanByteLength || owner.Offset % 4 != 0 || runtimeBase % 4 != 0 ||
            runtimeBase < 0x80000000 || (ulong)runtimeBase + CpuPlanByteLength > 0x80200000)
            throw new InvalidDataException("Font CPU plan requires its exact aligned detached final overlay extent.");
        foreach (uint global in new uint[] { 0x800757B0, 0x8007581C })
            if ((ulong)runtimeBase < (ulong)global + 4 && (ulong)runtimeBase + CpuPlanByteLength > global)
                throw new InvalidDataException("Font CPU plan overlaps a bound destination mutable global.");
        // This mapping is only a proposed arena. It proves neither free RAM nor
        // the lifetime of the two pointed-to renderer buffers.
        NativeMipsBehaviorLinkedCode linked = _package.Link(owner, runtimeBase, []);
        byte[] output = new byte[CpuPlanByteLength], code = linked.CopyBytes();
        if (code.Length != CodeByteLength || linked.UnmaterializedExternalTargets.Count != 0)
            throw new InvalidDataException("Font leaf code unexpectedly acquired external reservations.");
        code.CopyTo(output, 0); _colors.CopyTo(output, ColorsOffset); _descriptors.CopyTo(output, GlyphDescriptorsOffset);
        List<NativeJapaneseFontOperandPatch> patches = [];
        foreach (var pair in Pairs)
        {
            uint address = pair.Role switch
            {
                NativeJapaneseFontOperandRole.Colors => runtimeBase + ColorsOffset,
                NativeJapaneseFontOperandRole.GlyphDescriptors => runtimeBase + GlyphDescriptorsOffset,
                NativeJapaneseFontOperandRole.PrimitiveCursor => 0x800757B0,
                NativeJapaneseFontOperandRole.UiPrimitiveQueue => 0x8007581C,
                _ => throw new InvalidDataException("Unknown font semantic owner.")
            };
            uint hi = (pair.Hi & 0xFFFF0000) | ((address + 0x8000) >> 16);
            uint lo = (pair.Lo & 0xFFFF0000) | (address & 0xFFFF);
            Patch(pair.Pc, pair.Hi, hi); Patch(pair.Pc + 4, pair.Lo, lo);
            void Patch(uint pc, uint before, uint after)
            {
                uint finalPc = linked.SourceToFinalPc[pc]; int offset = checked((int)(finalPc - runtimeBase));
                if (Word(output, offset) != before) throw new InvalidDataException("Linked font operand preimage changed.");
                BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(offset), after);
                patches.Add(new(pair.Role, pc, finalPc, before, after, address));
            }
        }
        return new(output, owner, runtimeBase, linked.SourceToFinalPc, patches.ToArray(), _sourceBound);
    }

    private static uint Word(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
