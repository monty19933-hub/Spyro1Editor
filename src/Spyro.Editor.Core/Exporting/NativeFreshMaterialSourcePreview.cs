using System.Runtime.CompilerServices;
using Spyro.Editor.Core.Editing;

[assembly: InternalsVisibleTo("Spyro.Editor.NativeId65StaticTerrainTextureSmoke")]

namespace Spyro.Editor.Core.Exporting;

/// <summary>Read-only descriptor0 thumbnail of an actual static source, not a destination or runtime certificate.</summary>
public sealed class NativeFreshMaterialSourcePreview
{
    private readonly NativeTerrainLqTextureDescriptorPayload _payload;

    private NativeFreshMaterialSourcePreview(NativeStaticTerrainTextureSource source)
    {
        Source = source;
        var descriptor = source.Layout.Descriptors[0];
        // The actual source already validates all23 descriptors and three aliases.
        // Reuse its exact descriptor0 extraction and the existing LQ renderer.
        byte[] vram = source.CopySourceVram();
        _payload = new NativeTerrainLqTextureDescriptorPayload(0, descriptor.CopyDescriptor(),
            descriptor.CopyPixels(vram), descriptor.CopyPalette(vram));
    }

    public NativeStaticTerrainTextureSource Source { get; }
    public int DescriptorIndex => 0;
    public int Width => NativeTerrainLqTextureCacheCodec.TextureSide;
    public int Height => NativeTerrainLqTextureCacheCodec.TextureSide;
    public int PaletteRowCount => NativeTerrainLqTextureCacheCodec.PaletteRowCount;
    public bool DestinationAllocationProven => false;
    public bool SchematicRenderingProven => false;
    public bool RuntimeVerified => false;
    public bool PublicationApproved => false;

    public static NativeFreshMaterialSourcePreview Capture(NativeStaticTerrainTextureSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new(source);
    }

    // Each call returns a new array. No exposed raw payload/ReadOnlyMemory alias.
    // Palette row is not inferred camera distance; ABR/tint/LOD is not simulated.
    public Rgba32[] CopyRgba(int paletteRow = 0) => _payload.MaterializeRgba(paletteRow);
}
