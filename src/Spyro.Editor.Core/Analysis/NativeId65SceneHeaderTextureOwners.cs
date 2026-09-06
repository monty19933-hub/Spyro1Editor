using System.Buffers.Binary;
using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.Core.Analysis;

/// <summary>Conservative native page and fade-palette storage, not a rendered UV envelope.</summary>
public sealed class NativeId65SceneHeaderTextureFootprint
{
    internal NativeId65SceneHeaderTextureFootprint(int index, ReadOnlySpan<byte> raw,
        NativeId65VramRange[] pixels, NativeId65VramRange[] palettes)
    {
        DescriptorIndex = index;
        RawHex = Convert.ToHexStringLower(raw);
        PixelRows = Array.AsReadOnly(pixels);
        PaletteRows = Array.AsReadOnly(palettes);
    }

    public int DescriptorIndex { get; }
    public string RawHex { get; }
    public IReadOnlyList<NativeId65VramRange> PixelRows { get; }
    public IReadOnlyList<NativeId65VramRange> PaletteRows { get; }
}

/// <summary>
/// Exact Town presentation descriptors. Slot13 remains unavailable; it does not
/// acquire a texture owner at VRAM zero. Native dynamic UVs require full pages.
/// </summary>
public sealed class NativeId65SceneHeaderTextureOwners
{
    internal const string SourceHeaderSha256 = "1ba95c48a81510329cacf0b50b2de56fdc134d777efdb8cdddd09984c968da02";
    private readonly byte[] _tiledefs;

    private NativeId65SceneHeaderTextureOwners(ReadOnlySpan<byte> header)
    {
        if (header.Length != 0x88 || NativeId65NormalResourceSource.Hash(header) != SourceHeaderSha256)
            throw new InvalidDataException("Header texture authority requires the complete exact native Town header.");
        _tiledefs = header[0x10..].ToArray();
        List<NativeId65SceneHeaderTextureFootprint> footprints = [];
        for (int i = 0; i < 15; i++)
        {
            ReadOnlySpan<byte> raw = _tiledefs.AsSpan(i * 8, 8);
            if (i == 13)
            {
                if (raw.ContainsAnyExcept((byte)0))
                    throw new InvalidDataException("The source profile's unavailable superflame descriptor changed.");
                continue;
            }
            footprints.Add(DecodeAvailableTiledef(i, raw));
        }
        Footprints = footprints.AsReadOnly();
    }

    public int DescriptorCount => 15;
    public IReadOnlyList<int> UnavailableDescriptorIds { get; } = Array.AsReadOnly(new[] { 13 });
    public IReadOnlyList<NativeId65SceneHeaderTextureFootprint> Footprints { get; }
    public bool CompleteHeaderTextureConsumerClosureProven => false;
    public byte[] CopyTiledefs() => _tiledefs.ToArray();

    public NativeId65SceneHeaderTextureFootprint RequireAvailableDescriptor(int index)
    {
        if (index < 0 || index >= 15 || index == 13)
            throw new InvalidDataException("This exact presentation profile does not own the requested header descriptor.");
        return Footprints.Single(f => f.DescriptorIndex == index);
    }

    internal static NativeId65SceneHeaderTextureOwners Capture(ReadOnlySpan<byte> header) => new(header);

    internal static NativeId65SceneHeaderTextureFootprint DecodeAvailableTiledef(int index, ReadOnlySpan<byte> raw)
    {
        if (index < 0 || index >= 15 || raw.Length != 8 || !raw.ContainsAnyExcept((byte)0))
            throw new InvalidDataException("An available native header Tiledef requires one complete nonzero eight-byte slot.");
        ushort page = BinaryPrimitives.ReadUInt16LittleEndian(raw[6..]);
        ushort palette = BinaryPrimitives.ReadUInt16LittleEndian(raw[2..]);
        if ((page & 0x8000) == 0 || (page & ~0x80FF) != 0)
            throw new InvalidDataException("Unsupported native header page tag or pixel mode.");
        int mode = (page >> 7) & 1;
        int pageX = (page & 15) * 64;
        int pageY = ((page >> 4) & 1) * 256;
        int pageWords = mode == 0 ? 64 : 128;
        int colors = mode == 0 ? 16 : 256;
        int paletteX = (palette & 63) * 16;
        int paletteY = palette >> 6;
        if (pageX < 512 || pageX + pageWords > 1024 || pageY + 256 > 512 ||
            paletteX < 512 || paletteX + colors > 1024 || paletteY + 8 > 512)
            throw new InvalidDataException("Complete header page or eight-row fade palette escapes the fixed right-half upload owner.");
        // The bound alternate fade path can add up to0xFFE to the low UV
        // halfword before the native consumer replaces UV. Do not permit a
        // carry into another CLUT address even in the neutral byte decoder.
        if (BinaryPrimitives.ReadUInt16LittleEndian(raw) > 0xF001)
            throw new InvalidDataException("Header UV plus native fade offset could select an unowned palette.");
        var pixels = new NativeId65VramRange[256];
        for (int row = 0; row < pixels.Length; row++)
            pixels[row] = new((pageY + row) * 1024 + (pageX - 512) * 2, pageWords * 2);
        var palettes = new NativeId65VramRange[8];
        for (int row = 0; row < palettes.Length; row++)
            palettes[row] = new((paletteY + row) * 1024 + (paletteX - 512) * 2, colors * 2);
        return new(index, raw, pixels, palettes);
    }
}
