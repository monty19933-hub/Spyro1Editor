namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Bounded neutral tiled data readback. The shared walk establishes component
/// offsets; the existing flat-tile decoder independently binds every surface
/// byte to canonical geometry. Neither establishes source issuance, native
/// loader acceptance, model/scene ownership or permission to emit a disc.
/// </summary>
internal sealed class NativeId65TiledNormalDataLayout
{
    private NativeId65TiledNormalDataLayout(NativeId65NormalComponentLayout layout,
        NativeFlatTileSurfaceSnapshot surface)
    {
        Layout = layout;
        Surface = surface;
    }

    internal NativeId65NormalComponentLayout Layout { get; }
    internal NativeFlatTileSurfaceSnapshot Surface { get; }

    internal static NativeId65TiledNormalDataLayout DecodeUntextured(ReadOnlySpan<byte> data)
    {
        var layout = NativeId65NormalComponentLayout.Decode(data);
        var surface = NativeFlatTileSurfaceLayout.DecodeAndValidate(
            data.Slice(layout.SurfaceOffset, layout.SurfaceByteLength));
        return new(layout, surface);
    }
}
