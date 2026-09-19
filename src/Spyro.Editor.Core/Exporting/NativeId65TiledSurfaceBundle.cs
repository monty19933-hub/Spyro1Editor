using Spyro.Editor.Core.Editing;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Exact-original source capture paired with the actual schema4 document and
/// its canonical, color-only flat-tile surface. This internal slice admits no
/// placements or retail materials and issues no scene, body or disc authority.
/// </summary>
internal sealed class NativeId65TiledSurfaceBundle
{
    private readonly byte[] _documentBytes, _bytes, _executable;

    private NativeId65TiledSurfaceBundle(NativeFreshLevelDocumentIntent document,
        byte[] canonical, NativeFlatTileSurfaceSnapshot surface, byte[] executable)
    {
        Document = document;
        _documentBytes = canonical.ToArray();
        _bytes = surface.CopyBundle();
        _executable = executable.ToArray();
        ConsumerEvidence = NativeId65AuthoredSurfaceConsumers.Capture(_executable);
        Components = surface.Components;
        DocumentSha256 = NativeId65NormalResourceSource.Hash(_documentBytes);
        BundleSha256 = NativeId65NormalResourceSource.Hash(_bytes);
    }

    internal const string ProfileId = "native-id65-source-authored-untextured-flat-tiles-clean-usa-v1";
    internal NativeFreshLevelDocumentIntent Document { get; }
    internal string DocumentSha256 { get; }
    internal string BundleSha256 { get; }
    internal int ByteLength => _bytes.Length;
    internal IReadOnlyList<NativeId65SurfaceComponent> Components { get; }
    internal IReadOnlyList<NativeId65SurfaceConsumerEvidence> ConsumerEvidence { get; }
    internal string SourceImageSha256 => NativeId65AuthoredSurfaceConsumers.ImageSha256;
    internal bool SourceCertified => true;
    internal bool PublicationApproved => false;
    internal bool RuntimeVerified => false;
    internal bool IndependentLevelSupported => false;
    internal byte[] CopyDocumentBytes() => _documentBytes.ToArray();
    internal byte[] CopyBundle() => _bytes.ToArray();
    internal byte[] CopySourceExecutable() => _executable.ToArray();

    internal void RequireExactBundle(ReadOnlySpan<byte> bytes)
    {
        if (!bytes.SequenceEqual(_bytes))
            throw new InvalidDataException("Source-issued tiled surface owner or complete byte complement changed.");
    }

    internal static async Task<NativeId65TiledSurfaceBundle> CaptureAsync(string originalImagePath,
        NativeFreshLevelDocumentIntent document, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var (canonical, surface) = PrepareDocument(document);
        cancellationToken.ThrowIfCancellationRequested();
        byte[] executable = await NativeId65AuthoredSurfaceConsumers.ReadOriginalExecutableAsync(
            originalImagePath, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return new(document, canonical, surface, executable);
    }

    // Neutral preparation can establish bounds before I/O. It cannot create a
    // source issuer or promote caller bytes/hashes to the private constructor.
    internal static (byte[] Canonical, NativeFlatTileSurfaceSnapshot Surface) PrepareDocument(
        NativeFreshLevelDocumentIntent document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.SchemaVersion != 4 || document.TiledContent is not { } content ||
            document.Placements.Count != 0)
            throw new InvalidDataException("Tiled surface source capture requires actual schema4 content and zero placements.");
        foreach (var tile in content.Tiles)
            if (tile.Materials.Triangle0 is not NativeFreshBaseFloorColorMaterialIntent ||
                tile.Materials.Triangle1 is not NativeFreshBaseFloorColorMaterialIntent)
                throw new InvalidDataException("Tiled source capture currently requires both faces of every tile to use base floor color; retail materials are not omitted.");
        // Bound and freeze the WHOLE document before touching source storage.
        // IDs, spawn and music remain ancestry even when surface bytes match.
        byte[] canonical = NativeFreshLevelDocumentCodec.SerializeCanonical(document);
        var surface = NativeFlatTileSurfaceLayout.Compile(content.Tiles.Select(t => t.ToNativeTile()),
            NativeFreshFlatTileIntent.ColorWord(content.SkyColor));
        return (canonical, surface);
    }
}
