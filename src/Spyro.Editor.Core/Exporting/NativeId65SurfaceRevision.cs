namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Closed adapter over actual private source issuers. Shared resource assembly
/// consumes their real bytes; a tiled document never acquires a legacy Floor.
/// No byte/hash/enum constructor or neutral-source upgrade is available.
/// </summary>
internal sealed class NativeId65SurfaceRevision
{
    private readonly NativeId65AuthoredSurfaceBundle? _legacy;
    private readonly NativeId65TiledSurfaceBundle? _tiled;

    private NativeId65SurfaceRevision(NativeId65AuthoredSurfaceBundle legacy) => _legacy = legacy;
    private NativeId65SurfaceRevision(NativeId65TiledSurfaceBundle tiled) => _tiled = tiled;

    internal NativeId65AuthoredSurfaceBundle LegacySurface => _legacy ??
        throw new InvalidOperationException("Tiled resources have no legacy authored Surface; use their actual source revision.");
    internal NativeId65TiledSurfaceBundle? TiledSurface => _tiled;
    internal string SourceImageSha256 => _legacy?.SourceImageSha256 ?? _tiled!.SourceImageSha256;
    internal bool SourceCertified => _legacy?.SourceCertified ?? _tiled!.SourceCertified;
    internal string BundleSha256 => _legacy?.BundleSha256 ?? _tiled!.BundleSha256;
    internal IReadOnlyList<NativeId65SurfaceComponent> Components => _legacy?.Components ?? _tiled!.Components;
    internal byte[] CopyBundle() => _legacy?.CopyBundle() ?? _tiled!.CopyBundle();
    internal byte[] CopySourceExecutable() => _legacy?.CopySourceExecutable() ?? _tiled!.CopySourceExecutable();

    internal static NativeId65SurfaceRevision From(NativeId65AuthoredSurfaceBundle surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        if (!surface.SourceCertified || surface.SourceImageSha256 != NativeId65AuthoredSurfaceConsumers.ImageSha256)
            throw new InvalidDataException("A surface revision requires the private original-source issuer, not neutral surface bytes.");
        return new(surface);
    }

    internal static NativeId65SurfaceRevision From(NativeId65TiledSurfaceBundle surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        if (!surface.SourceCertified || surface.SourceImageSha256 != NativeId65AuthoredSurfaceConsumers.ImageSha256)
            throw new InvalidDataException("A tiled revision requires its private original-source document issuer.");
        return new(surface);
    }
}
