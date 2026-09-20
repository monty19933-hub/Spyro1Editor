using Spyro.Editor.Core.Editing;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Source-issued whole-document tiled compilation through the shared normal
/// scene/resource/body/disc producers. Current allocation: two data sectors,
/// zero placements, base-color or two distinct static material selections, N16.
/// No file writer, UI export, candidate
/// approval or gameplay certificate is conferred by this internal compilation.
/// </summary>
internal sealed class NativeId65TiledDocumentCompilation
{
    private readonly byte[] _canonical;
    private NativeId65TiledDocumentCompilation(NativeFreshLevelDocumentIntent document,
        byte[] canonical, NativeId65FreshSceneBundle fresh, NativeId65FreshDiscPlan disc,
        NativeId65TiledMaterialPlan? materials = null)
    {
        Document = document;
        _canonical = canonical.ToArray();
        CanonicalSha256 = NativeId65NormalResourceSource.Hash(_canonical);
        FreshResourceAncestor = fresh;
        TiledMaterialAncestor = materials;
        DiscPlan = disc;
        var surface = fresh.TiledSurfaceAncestor ??
            throw new InvalidDataException("Tiled compilation has no actual source-issued document ancestor.");
        if (!ReferenceEquals(surface.Document, document) ||
            !ReferenceEquals(disc.BodyAncestor.TiledSceneAncestor, fresh) ||
            CanonicalSha256 != surface.DocumentSha256 ||
            !ReferenceEquals(disc.BodyAncestor.TiledMaterialAncestor, materials))
            throw new InvalidDataException("Tiled compilation lost its complete actual document/source/body ancestry.");
        if (materials == null)
        {
            if (disc.BodyAncestor.ResourceProfile != NativeId65NormalResourceProfile.UntexturedGouraudFlatTiles)
                throw new InvalidDataException("Base-only tiled compilation cannot infer a material profile.");
            _ = NativeId65TiledSurfaceBundle.PrepareDocument(document);
        }
        else
        {
            materials.RequireAncestry(fresh, document, materials.MaterialSources[0], materials.MaterialSources[1]);
            if (disc.BodyAncestor.ResourceProfile != NativeId65NormalResourceProfile.DirectLqTwoStaticMaterialsFlatTiles ||
                CanonicalSha256 != materials.CanonicalDocumentSha256)
                throw new InvalidDataException("Material compilation requires its exact complete material revision.");
        }
    }

    internal NativeFreshLevelDocumentIntent Document { get; }
    internal string CanonicalSha256 { get; }
    internal NativeId65FreshSceneBundle FreshResourceAncestor { get; }
    internal NativeId65TiledMaterialPlan? TiledMaterialAncestor { get; }
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
        var fresh = await CaptureFreshAsync(originalBin, surface, token);
        var staging = await NativeId65NormalUploadStaging.CaptureTiledAsync(originalBin, fresh, token);
        var body = await NativeId65NormalBodyPlan.CaptureTiledAsync(originalBin, fresh, staging, token);
        var disc = await NativeId65FreshDiscPlan.CaptureTiledAsync(originalBin, body, token);
        token.ThrowIfCancellationRequested();
        return new(document, canonical, fresh, disc);
    }

    internal static async Task<NativeId65TiledDocumentCompilation> CaptureMaterialsAsync(string originalBin,
        NativeFreshLevelDocumentIntent document, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        var prepared = NativeId65TiledSurfaceBundle.PrepareGeometryForMaterials(document);
        token.ThrowIfCancellationRequested();
        var surface = await NativeId65TiledSurfaceBundle.CaptureGeometryForMaterialsAsync(originalBin, document, token);
        var fresh = await CaptureFreshAsync(originalBin, surface, token);
        token.ThrowIfCancellationRequested();
        var first = prepared.Selections[0];
        var source0 = NativeStaticTerrainTextureSource.Capture(originalBin, first.SourceWadEntry, first.TextureId);
        token.ThrowIfCancellationRequested();
        var second = prepared.Selections[1];
        var source1 = NativeStaticTerrainTextureSource.Capture(originalBin, second.SourceWadEntry, second.TextureId);
        token.ThrowIfCancellationRequested();
        var materials = await NativeId65TiledMaterialPlan.CaptureAsync(originalBin, fresh, source0, source1, token);
        var staging = await NativeId65NormalUploadStaging.CaptureTiledMaterialsAsync(originalBin, materials, token);
        var body = await NativeId65NormalBodyPlan.CaptureTiledMaterialsAsync(originalBin, materials, staging, token);
        var disc = await NativeId65FreshDiscPlan.CaptureTiledMaterialsAsync(originalBin, body, token);
        token.ThrowIfCancellationRequested();
        return new(document, prepared.Canonical, fresh, disc, materials);
    }

    // Both named variants keep the same actual document-bearing surface through
    // the shared companion/resource/list/scene issuers. No Base-only projection.
    private static async Task<NativeId65FreshSceneBundle> CaptureFreshAsync(string originalBin,
        NativeId65TiledSurfaceBundle surface, CancellationToken token)
    {
        var companion = await NativeId65CompanionAssetPlan.CaptureAsync(originalBin, token);
        var resources = await NativeId65NormalResourceBundle.CaptureTiledAsync(originalBin, surface, companion, token);
        var lists = await NativeId65ZeroSceneAnimationLists.CaptureAsync(originalBin, token);
        token.ThrowIfCancellationRequested();
        var chain = NativeId65NormalResourceChain.Compile(resources, lists);
        return await NativeId65FreshSceneBundle.CaptureTiledAsync(originalBin, chain, token);
    }
}
