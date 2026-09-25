using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Spyro.Editor.Core.Analysis;

/// <summary>
/// One descriptor from the pinned STATIC/simple Moby renderer. RGB is its base
/// value, not a final pixel prediction or an animated F5 appearance selector.
/// </summary>
public sealed record NativeMobyShadingProfile(byte Index, uint Descriptor)
{
    public byte Red => (byte)Descriptor;
    public byte Green => (byte)(Descriptor >> 8);
    public byte Blue => (byte)(Descriptor >> 16);
    public byte LightingControl => (byte)(Descriptor >> 24);
}

/// <summary>
/// An exact-revision, source-bound 32-entry observation of the Moby shader table.
/// This deliberately does not assert the complete table extent or model closure.
/// The table belongs to the static/simple renderer. Animated F5 does not select
/// this table: its row+4F is an opaque render-mode byte and model-owned packed RGB
/// arrays feed its colour path. Numeric row/table projections below do not prove
/// that either identified actor uses this renderer. These are not CLUT entries.
/// </summary>
public sealed class NativeJapaneseObjectAppearance
{
    public const int CapturedProfileCount = 32;
    private readonly NativeMobyShadingProfile[] _profiles;
    private NativeJapaneseObjectAppearance(NativeDiscRevision revision, string imageHash, uint tablePc,
        int tableOffset, ReadOnlySpan<byte> bytes)
    {
        Revision = revision; SourceImageSha256 = imageHash; TableRuntimeAddress = tablePc;
        TableExecutableOffset = tableOffset; TableWindowSha256 = Hash(bytes);
        _profiles = Enumerable.Range(0, CapturedProfileCount)
            .Select(i => new NativeMobyShadingProfile((byte)i, 0)).ToArray();
        for (int i = 0; i < _profiles.Length; i++)
            _profiles[i] = new((byte)i, BinaryPrimitives.ReadUInt32LittleEndian(bytes[(i * 4)..]));
        Profiles = Array.AsReadOnly(_profiles);
    }
    public NativeDiscRevision Revision { get; }
    public string SourceImageSha256 { get; }
    public uint TableRuntimeAddress { get; }
    public int TableExecutableOffset { get; }
    public string TableWindowSha256 { get; }
    public IReadOnlyList<NativeMobyShadingProfile> Profiles { get; }
    public bool NativeImportApproved => false;
    public bool FinalRenderedColorVerified => false;
    public bool AnimatedEggDescriptorSelectionProven => false;

    public static NativeJapaneseObjectAppearance Capture(NativeRegionalSourceSession source)
    {
        ArgumentNullException.ThrowIfNull(source);
        bool japan = source.Revision == NativeDiscRevision.JapanScps10083;
        int renderer = japan ? 0x12698 : 0x13904;
        string rendererHash = japan ? "4f894553f8ea16ac0b39e5d1147de06e522e2c74383167c09acf2c0ad30cdfa5" :
            "66c82cfa7abbb5923cab63cc59f4b78d55900fd2a4dcaa6c8504e663c11d4d47";
        Require(source.ReadExecutableRange(renderer, 0x134), rendererHash);
        // Both paths load row+4C then ASR by22. ASR produces a byte offset;
        // bits22/23 must be zero for aligned words, and selectors>=128 become
        // negative. Admission below is only the observed nonnegative prefix.
        byte[] instructions = source.ReadExecutableRange(renderer, 0x2C);
        if (BinaryPrimitives.ReadUInt32LittleEndian(instructions) != 0x8FD1004C ||
            BinaryPrimitives.ReadUInt32LittleEndian(instructions.AsSpan(0x20)) != 0x00118D83)
            throw new InvalidDataException("Native per-instance shading lookup changed.");
        int tableOffset = japan ? 0x67674 : 0x5EBD8;
        byte[] table = source.ReadExecutableRange(tableOffset, CapturedProfileCount * 4);
        Require(table, japan ? "8436afe7fe544b2ff21e65754706f712ce9185f8f1369c5258426fb80ce32909" :
            "7cf8206c8ac05f3b0acfd9209a45c866ca29a459b5142294702ecfb24f7d52a3");
        source.VerifyUnchanged();
        return new(source.Revision, source.SourceImageSha256, japan ? 0x80076E74u : 0x8006E3D8u, tableOffset, table);
    }

    /// <summary>
    /// Decode caller-supplied fields and numerically project the packed value
    /// into the static table. This is NOT evidence that the actor selects that
    /// table; source provenance and renderer applicability are separate.
    /// </summary>
    public NativeJapaneseMobyAppearanceFields DescribeRow(ReadOnlySpan<byte> row)
    {
        RequireRow(row);
        uint packed = BinaryPrimitives.ReadUInt32LittleEndian(row[0x4C..]);
        int byteOffset = unchecked((int)packed) >> 22;
        if (byteOffset < 0 || byteOffset % 4 != 0 || byteOffset >= CapturedProfileCount * 4)
            throw new InvalidDataException("Packed row numerical projection is unaligned or outside the captured static-table window.");
        return new(BinaryPrimitives.ReadUInt16LittleEndian(row[0x36..]), row[0x3C], row[0x3D],
            row[0x3E], row[0x3F], packed, _profiles[byteOffset / 4]);
    }

    /// <summary>
    /// Produce a detached revision changing only opaque native row+4F. The name
    /// is retained for compatibility; for animated F5 this is NOT a tint/profile
    /// setter. Model, animation and low22bits remain intact. No renderer, import
    /// or independently coloured instance approval is conferred.
    /// </summary>
    public byte[] WithProfile(ReadOnlySpan<byte> row, int profileIndex)
    {
        _ = DescribeRow(row);
        if (profileIndex is < 0 or >= CapturedProfileCount)
            throw new InvalidDataException("Native shade profile has not been captured from this revision.");
        byte[] output = row.ToArray();
        output[0x4F] = (byte)profileIndex;
        _ = DescribeRow(output);
        return output;
    }

    /// <summary>Exact static-table descriptor matching; neither indices nor matches prove renderer applicability.</summary>
    public IReadOnlyList<byte> FindExactProfileMatches(NativeMobyShadingProfile profile) =>
        Array.AsReadOnly(_profiles.Where(p => p.Descriptor == profile.Descriptor).Select(p => p.Index).ToArray());

    internal static NativeJapaneseObjectAppearance DecodeFixture(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != CapturedProfileCount * 4)
            throw new InvalidDataException("Fixture shader window must contain32 complete words.");
        return new(NativeDiscRevision.JapanScps10083, "fixture-no-source-certificate", 0, 0, bytes);
    }
    private static void RequireRow(ReadOnlySpan<byte> row)
    {
        if (row.Length != 0x58)
            throw new InvalidDataException("Native Moby appearance needs one complete row.");
        ushort actor = BinaryPrimitives.ReadUInt16LittleEndian(row[0x36..]);
        if (actor is not (0x47 or 0xF5))
            throw new InvalidDataException("This appearance contract covers identified Japanese signs and dragonfly eggs only.");
        if (row[0x3C] != row[0x3D] || row[0x3C] > (actor == 0x47 ? 2 : 0))
            throw new InvalidDataException("Unsupported native source animation variant.");
    }
    private static void Require(byte[] bytes, string expected)
    { if (Hash(bytes) != expected) throw new InvalidDataException("Native shader source preimage changed."); }
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}

/// <summary>ShadingProfile is a numerical static-table projection, not a proven actor selection.</summary>
public sealed record NativeJapaneseMobyAppearanceFields(ushort ActorId, byte CurrentAnimation,
    byte NextAnimation, byte CurrentFrame, byte NextFrame, uint PackedRowColorWord,
    NativeMobyShadingProfile ShadingProfile);
