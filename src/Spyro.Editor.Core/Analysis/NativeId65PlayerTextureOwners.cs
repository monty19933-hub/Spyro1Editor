using System.Buffers.Binary;
using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.Core.Analysis;

public sealed record NativeId65VramRange(int VramRelativeOffset, int ByteLength);
public sealed record NativeId65PlayerTextureReference(int SharedPackageFieldOffset, ushort Page,
    ushort Palette, NativeId65TextureFootprint Footprint);

/// <summary>Conservative byte ownership of the complete native UV rectangle, not a triangle rasterizer.</summary>
public sealed class NativeId65TextureFootprint
{
    internal NativeId65TextureFootprint(IEnumerable<NativeId65VramRange> rows, NativeId65VramRange palette)
    {
        PixelRows = Array.AsReadOnly(rows.ToArray());
        Palette = palette;
    }
    public IReadOnlyList<NativeId65VramRange> PixelRows { get; }
    public NativeId65VramRange Palette { get; }
}

/// <summary>
/// The exact resident PETE face-table owners also selected by Town's four local
/// player animations. GPU addresses stay fixed: this does not rewrite PETE or
/// claim complete variable-frame decoding, all player states, or runtime fit.
/// </summary>
public sealed class NativeId65PlayerTextureOwners
{
    private NativeId65PlayerTextureOwners(IEnumerable<NativeId65PlayerTextureReference> references)
    {
        References = Array.AsReadOnly(references.ToArray());
        PaletteIds = Array.AsReadOnly(References.Select(r => r.Palette).Distinct().Order().ToArray());
    }
    public IReadOnlyList<NativeId65PlayerTextureReference> References { get; }
    public IReadOnlyList<ushort> PaletteIds { get; }
    public int ReferenceCount => References.Count;
    public bool FixedResidentGeometryPreserved => true;
    public bool CompletePlayerStateClosureProven => false;
    public bool FramePayloadDecodingProven => false;

    internal static NativeId65PlayerTextureOwners Capture(ReadOnlySpan<byte> sharedPackage,
        ReadOnlySpan<byte> townStream)
    {
        NativeId65NormalResourceSource.RequireHash(sharedPackage,
            NativeId65NormalResourceSource.PlayerPackageHash, "complete resident player package");
        NativeId65NormalResourceSource.RequireHash(townStream,
            NativeId65NormalResourceSource.TownStreamHash, "complete Town special player stream");
        var textures = NativeMobyModelTextureLayout.Decode(sharedPackage);
        var stream = NativePlayerAnimationStreamCodec.Decode(townStream, sharedPackage[0xAA8..]);
        if (textures.References.Count != 355 || !textures.FaceTableOffsets.SequenceEqual(new[] { 0xAA8 }) ||
            !stream.Records.Select(r => r.AnimationId).SequenceEqual(new[] { 18, 19, 29, 40 }))
            throw new InvalidDataException("Player texture union differs from its exact resident/local schema.");
        foreach (var record in stream.Records)
        {
            byte[] bytes = record.CopyRecord();
            // Native local header +14 is geometry-relative, not a local stream
            // pointer. The optional low-detail face table +1C is absent here.
            if (Word(bytes, 8 + 0x14) != 0 || Word(bytes, 8 + 0x1C) != 0)
                throw new InvalidDataException("Town player local header introduces another face-table owner.");
        }
        var references = textures.References.Select(r => new NativeId65PlayerTextureReference(
            r.PackageOffset, r.Page, r.Palette, DecodeFootprint(r.CopyBytes()))).ToArray();
        if (references.Any(r => r.Page != 0x1F) || references.Select(r => r.Palette).Distinct().Count() != 17)
            throw new InvalidDataException("Player native page/CLUT ownership changed.");
        return new(references);
    }

    internal static NativeId65TextureFootprint DecodeFootprint(ReadOnlySpan<byte> descriptor)
    {
        if (descriptor.Length != 12)
            throw new InvalidDataException("A native model texture command requires exactly twelve owned bytes.");
        ushort page = BinaryPrimitives.ReadUInt16LittleEndian(descriptor[6..]);
        ushort palette = BinaryPrimitives.ReadUInt16LittleEndian(descriptor[2..]);
        if ((page & ~0x7F) != 0 || (page & 15) < 8)
            throw new InvalidDataException("Texture depth/page lies outside the supported right-half4bpp upload.");
        int minU = 255, minV = 255, maxU = 0, maxV = 0;
        foreach (int at in new[] { 0, 4, 8, 10 })
        {
            minU = Math.Min(minU, descriptor[at]); maxU = Math.Max(maxU, descriptor[at]);
            minV = Math.Min(minV, descriptor[at + 1]); maxV = Math.Max(maxV, descriptor[at + 1]);
        }
        int pageWordX = (page & 15) * 64;
        int pageY = ((page >> 4) & 1) * 256;
        int width = maxU / 2 - minU / 2 + 1;
        var rows = new NativeId65VramRange[maxV - minV + 1];
        for (int v = minV; v <= maxV; v++)
        {
            int offset = (pageY + v) * 1024 + (pageWordX - 512) * 2 + minU / 2;
            Range(offset, width);
            if (offset % 1024 + width > 1024)
                throw new InvalidDataException("Native texture rectangle crosses its physical VRAM row.");
            rows[v - minV] = new(offset, width);
        }
        int clutX = (palette & 63) * 16, clutY = palette >> 6;
        if (clutX < 512 || clutX + 16 > 1024 || clutY >= 512)
            throw new InvalidDataException("Native CLUT lies outside the complete right-half VRAM owner.");
        var clut = new NativeId65VramRange(clutY * 1024 + (clutX - 512) * 2, 32);
        Range(clut.VramRelativeOffset, clut.ByteLength);
        return new(rows, clut);
    }

    private static void Range(int offset, int length)
    {
        if (offset < 0 || length <= 0 || offset + (long)length > 0x80000)
            throw new InvalidDataException("VRAM byte owner exceeds the complete upload domain.");
    }
    private static uint Word(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
}

/// <summary>
/// One immutable source atlas plus independent detached owners. Only the same
/// source address can alias; matching bytes in unrelated owners are insufficient.
/// Zero-valued referenced bytes are protected exactly like nonzero pixels.
/// </summary>
internal sealed class NativeId65VramAssembly
{
    private readonly byte[] _source, _bytes = new byte[0x80000];
    private readonly byte[] _ownership = new byte[0x80000];
    private long _chargedBytes;
    private int _requests;
    internal NativeId65VramAssembly(ReadOnlySpan<byte> source)
    {
        if (source.Length != 0x80000)
            throw new InvalidDataException("VRAM assembly requires the exact complete right-half source upload.");
        _source = source.ToArray();
    }
    internal int SourceOwnedByteCount { get; private set; }
    internal byte[] CopyBytes() => _bytes.ToArray();
    internal byte[] CopyOwnership() => _ownership.ToArray();
    internal void CopySourceRange(int offset, int length)
    {
        Charge(offset, length);
        if (_ownership.AsSpan(offset, length).Contains((byte)2))
            throw new InvalidDataException("Source texture aliases a distinct detached owner.");
        for (int i = offset; i < offset + length; i++)
        {
            if (_ownership[i] == 0) SourceOwnedByteCount++;
            _ownership[i] = 1;
        }
        _source.AsSpan(offset, length).CopyTo(_bytes.AsSpan(offset));
    }
    internal void CopyDetachedRange(string id, int offset, ReadOnlySpan<byte> bytes)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 128)
            throw new InvalidDataException("Detached VRAM owner has no bounded identity.");
        Charge(offset, bytes.Length);
        if (_ownership.AsSpan(offset, bytes.Length).ContainsAnyExcept((byte)0))
            throw new InvalidDataException("Detached VRAM owner overlaps protected source or another detached owner.");
        _ownership.AsSpan(offset, bytes.Length).Fill(2);
        bytes.CopyTo(_bytes.AsSpan(offset));
    }
    private void Charge(int offset, int length)
    {
        if (offset < 0 || length <= 0 || offset + (long)length > _bytes.Length)
            throw new InvalidDataException("VRAM owner is outside its bounded upload domain.");
        if (++_requests > 65536 || (_chargedBytes += length) > 0x1000000)
            throw new InvalidDataException("VRAM ownership materialization work budget exceeded.");
    }
}
