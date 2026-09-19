using Spyro.Editor.Core.Editing;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Source-issued whole-document tiled compilation through the shared normal
/// scene/resource/body/disc producers. Current allocation: two data sectors,
/// zero placements, base-color faces, N16. No file writer, UI export, candidate
/// approval or gameplay certificate is conferred by this internal compilation.
/// </summary>
internal sealed class NativeId65TiledDocumentCompilation
{
    private readonly byte[] _canonical;
    private NativeId65TiledDocumentCompilation(NativeFreshLevelDocumentIntent document,
        byte[] canonical, NativeId65FreshSceneBundle fresh, NativeId65FreshDiscPlan disc)
    {
        Document = document;
        _canonical = canonical.ToArray();
        CanonicalSha256 = NativeId65NormalResourceSource.Hash(_canonical);
        FreshResourceAncestor = fresh;
        DiscPlan = disc;
        var surface = fresh.TiledSurfaceAncestor ??
            throw new InvalidDataException("Tiled compilation has no actual source-issued document ancestor.");
        if (!ReferenceEquals(surface.Document, document) ||
            !ReferenceEquals(disc.BodyAncestor.TiledSceneAncestor, fresh) ||
            CanonicalSha256 != surface.DocumentSha256)
            throw new InvalidDataException("Tiled compilation lost its complete actual document/source/body ancestry.");
    }

    internal NativeFreshLevelDocumentIntent Document { get; }
    internal string CanonicalSha256 { get; }
    internal NativeId65FreshSceneBundle FreshResourceAncestor { get; }
    internal NativeId65FreshDiscPlan DiscPlan { get; }
    internal byte[] CopyCanonicalDocument() => _canonical.ToArray();

    internal static async Task<NativeId65TiledDocumentCompilation> CaptureAsync(string originalBin,
        NativeFreshLevelDocumentIntent document, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        var (canonical, neutral) = NativeId65TiledSurfaceBundle.PrepareDocument(document);
        NativeId65TiledNormalDataLayout.RequireFixedSurfaceAllocation(neutral.CopyBundle().Length);
        token.ThrowIfCancellationRequested();
        var surface = await NativeId65TiledSurfaceBundle.CaptureAsync(originalBin, document, token);
        var companion = await NativeId65CompanionAssetPlan.CaptureAsync(originalBin, token);
        var resources = await NativeId65NormalResourceBundle.CaptureTiledAsync(originalBin, surface, companion, token);
        var lists = await NativeId65ZeroSceneAnimationLists.CaptureAsync(originalBin, token);
        token.ThrowIfCancellationRequested();
        var chain = NativeId65NormalResourceChain.Compile(resources, lists);
        var fresh = await NativeId65FreshSceneBundle.CaptureTiledAsync(originalBin, chain, token);
        var staging = await NativeId65NormalUploadStaging.CaptureTiledAsync(originalBin, fresh, token);
        var body = await NativeId65NormalBodyPlan.CaptureTiledAsync(originalBin, fresh, staging, token);
        var disc = await NativeId65FreshDiscPlan.CaptureTiledAsync(originalBin, body, token);
        token.ThrowIfCancellationRequested();
        return new(document, canonical, fresh, disc);
    }
}
