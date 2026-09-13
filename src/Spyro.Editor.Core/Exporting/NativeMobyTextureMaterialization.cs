using System.Security.Cryptography;

namespace Spyro.Editor.Core.Exporting;

internal sealed record NativeMobyTextureTileRequest(string Id, ushort Palette, ushort Page,
    int MinU, int MinV, int MaxU, int MaxV);

/// <summary>
/// Atomic, neutral 4-bpp tile/CLUT materialization into a complete current upload.
/// The caller must supply all live owners, including zero-valued consumers. This
/// geometry kernel does not certify that ownership, source ancestry, model
/// consumers, GPU lifetime, runtime fit or native import/publication eligibility.
/// </summary>
internal sealed class NativeMobyTextureMaterialization
{
    private readonly byte[] _upload;

    private NativeMobyTextureMaterialization(byte[] upload,
        List<NativeMobyIndexedTextureLocation> locations, List<NativeMobyTextureBytePatch> patches,
        List<NativeMobyTextureAllocationEvidence> allocations)
    {
        _upload = upload;
        Locations = Array.AsReadOnly(locations.ToArray());
        Patches = Array.AsReadOnly(patches.ToArray());
        Allocations = Array.AsReadOnly(allocations.ToArray());
    }

    internal IReadOnlyList<NativeMobyIndexedTextureLocation> Locations { get; }
    internal IReadOnlyList<NativeMobyTextureBytePatch> Patches { get; }
    internal IReadOnlyList<NativeMobyTextureAllocationEvidence> Allocations { get; }
    internal byte[] CopyUpload() => _upload.ToArray();

    internal static NativeMobyTextureMaterialization Compile(ReadOnlySpan<byte> sourceUpload,
        ReadOnlySpan<byte> targetUpload, IReadOnlyList<NativeTexturePageOwnedRange> protectedRanges,
        IReadOnlyList<NativeMobyTextureTileRequest> requests)
    {
        ArgumentNullException.ThrowIfNull(protectedRanges);
        ArgumentNullException.ThrowIfNull(requests);
        if (sourceUpload.Length != 0x80000 || targetUpload.Length != 0x80000)
            throw new InvalidDataException("Native texture materialization requires two complete right-half VRAM uploads.");
        if (requests.Count > 4096)
            throw new InvalidDataException("Native texture request count exceeds the defensive compiler bound.");
        // Do not trust an arbitrary IReadOnlyList's Count to bound enumeration.
        // Refusal also disposes the enumerator before any source allocation.
        List<NativeMobyTextureTileRequest> pending = [];
        foreach (var request in requests)
        {
            if (pending.Count == 4096)
                throw new InvalidDataException("Native texture request count exceeds the defensive compiler bound.");
            pending.Add(request);
        }
        var ordered = pending.ToArray();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var sourceExtents = new (int Pixel, int Palette, int RowBytes, int Rows)[ordered.Length];
        // Validate the entire batch before making even a private allocation.
        for (int i = 0; i < ordered.Length; i++)
        {
            var request = ordered[i];
            if (request is null || string.IsNullOrWhiteSpace(request.Id) || !ids.Add(request.Id) ||
                request.MinU < 0 || request.MinV < 0 || request.MaxU > 255 || request.MaxV > 255 ||
                request.MaxU < request.MinU || request.MaxV < request.MinV ||
                request.MinU % 2 != 0 || (request.MaxU - request.MinU + 1) % 2 != 0 ||
                ((request.Page >> 7) & 3) != 0)
                throw new InvalidDataException("Native texture request has invalid identity, UV bounds or 4-bpp dimensions/depth.");
            int width = request.MaxU - request.MinU + 1, rows = request.MaxV - request.MinV + 1;
            int x = (request.Page & 15) * 128 + request.MinU / 2 - 1024;
            int y = ((request.Page & 16) != 0 ? 256 : 0) + request.MinV;
            int paletteX = (request.Palette & 63) * 32 - 1024, paletteY = request.Palette >> 6;
            if (x < 0 || x + width / 2 > 1024 || y + rows > 512 ||
                paletteX < 0 || paletteX + 32 > 1024 || paletteY >= 512)
                throw new InvalidDataException("Native texture pixels/CLUT are outside the source upload.");
            sourceExtents[i] = (y * 1024 + x, paletteY * 1024 + paletteX, width / 2, rows);
        }

        // All mutable allocation/output state belongs to this call. A failed
        // later reservation cannot leak a partial install or consume a retry.
        var source = sourceUpload.ToArray();
        var before = targetUpload.ToArray();
        var upload = before.ToArray();
        var allocator = new NativeMobyIndexedTextureAllocation(before, protectedRanges.ToArray());
        List<NativeMobyIndexedTextureLocation> locations = [];
        List<NativeMobyTextureBytePatch> patches = [];
        List<NativeMobyTextureAllocationEvidence> allocations = [];
        for (int i = 0; i < ordered.Length; i++)
        {
            var extent = sourceExtents[i];
            var location = allocator.Reserve(extent.RowBytes * 2, extent.Rows);
            locations.Add(location);
            var pixels = new byte[extent.RowBytes * extent.Rows];
            for (int row = 0; row < extent.Rows; row++)
            {
                var bytes = source.AsSpan(extent.Pixel + row * 1024, extent.RowBytes);
                bytes.CopyTo(pixels.AsSpan(row * extent.RowBytes));
                Install(location.PixelOffset + row * 1024, bytes);
            }
            var palette = source.AsSpan(extent.Palette, 32);
            Install(location.PaletteOffset, palette);
            allocations.Add(new(ordered[i].Id, extent.Pixel, location.PixelOffset, extent.RowBytes, extent.Rows,
                extent.Palette, location.PaletteOffset, Hash(pixels), Hash(palette)));
        }
        return new(upload, locations, patches, allocations);

        void Install(int offset, ReadOnlySpan<byte> bytes)
        {
            patches.Add(new("target-vram", offset, before.AsSpan(offset, bytes.Length), bytes));
            bytes.CopyTo(upload.AsSpan(offset));
        }
    }

    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
