using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using Spyro.Editor.Core.Editing;

[assembly: InternalsVisibleTo("Spyro.Editor.NativeFreshSavedMaterialSmoke")]

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Actual saved schema-2, zero-placement, two-static-material lineage compiled
/// through the existing selected direct-LQ factories. This detached object has
/// no writer, CUE, publication role, long Store lock or runtime approval.
/// </summary>
internal sealed class NativeFreshSavedMaterialCompilation
{
    private readonly byte[] _canonical;

    private NativeFreshSavedMaterialCompilation(string image,
        NativeFreshSavedLevelDocumentReadContext saved,
        NativeId65FreshDocumentCompilation compilation,
        NativeStaticTerrainTextureSource material0, NativeStaticTerrainTextureSource material1,
        NativeId65FreshTerrainTexturePlan texture, NativeId65NormalUploadStaging staging,
        NativeId65NormalBodyPlan body, NativeId65FreshDiscPlan disc)
    {
        SourceImagePath = image;
        Saved = saved;
        BaseCompilation = compilation;
        Material0 = material0;
        Material1 = material1;
        Texture = texture;
        Staging = staging;
        Body = body;
        Disc = disc;
        _canonical = NativeFreshLevelDocumentCodec.SerializeCanonical(saved.Document);
        RequireAncestry(saved, compilation, material0, material1, texture, staging, body, disc);
    }

    internal string SourceImagePath { get; }
    internal NativeFreshSavedLevelDocumentReadContext Saved { get; }
    internal NativeFreshLevelDocumentIntent Document => Saved.Document;
    internal NativeId65FreshDocumentCompilation BaseCompilation { get; }
    internal NativeStaticTerrainTextureSource Material0 { get; }
    internal NativeStaticTerrainTextureSource Material1 { get; }
    internal NativeId65FreshTerrainTexturePlan Texture { get; }
    internal NativeId65NormalUploadStaging Staging { get; }
    internal NativeId65NormalBodyPlan Body { get; }
    internal NativeId65FreshDiscPlan Disc { get; }
    internal bool RuntimeVerified => false;
    internal bool PublicationApproved => false;
    internal byte[] CopyCanonicalDocument() => _canonical.ToArray();

    internal static Task<NativeFreshSavedMaterialCompilation> CaptureAsync(string originalBin,
        NativeFreshSavedLevelDocumentReadContext saved, CancellationToken token = default) =>
        CaptureCoreAsync(originalBin, saved, null, token);

    // Timing/failure observations only. This cannot return a source, substitute
    // a native result, or choose a stream/path/serializer for an inner factory.
    internal static Task<NativeFreshSavedMaterialCompilation> CaptureForSmokeAsync(string originalBin,
        NativeFreshSavedLevelDocumentReadContext saved, Action<string> checkpoint,
        CancellationToken token = default) => CaptureCoreAsync(originalBin, saved, checkpoint, token);

    private static async Task<NativeFreshSavedMaterialCompilation> CaptureCoreAsync(string originalBin,
        NativeFreshSavedLevelDocumentReadContext saved, Action<string>? checkpoint, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(saved);
        ArgumentException.ThrowIfNullOrWhiteSpace(originalBin);
        var (first, second) = RequireProfile(saved.Document);
        NativeFreshDocumentStorage.RequireAggregateCurrent(saved.Snapshot, token);
        string image = Path.GetFullPath(originalBin);

        FileStream? original = null;
        NativeFreshSavedMaterialCompilation? result = null;
        List<Exception> failures = [];
        try
        {
            // Assign the concrete owner before its first callback/identity read.
            original = new FileStream(image, FileMode.Open, FileAccess.Read, FileShare.Read);
            Captured("source-opened");
            await NativeId65FreshDiscSource.RequireWholeOriginalAsync(original, token);

            var compilation = await NativeId65FreshDocumentCompilation.CaptureAsync(image, saved.Document.Base, token);
            Captured("base-captured");
            var material0 = NativeStaticTerrainTextureSource.Capture(image, first.SourceWadEntry, first.TextureId);
            Captured("material0-captured");
            var material1 = SameDescriptor(first, second) ? material0 :
                NativeStaticTerrainTextureSource.Capture(image, second.SourceWadEntry, second.TextureId);
            Captured("material1-captured");
            var texture = await NativeId65FreshTerrainTexturePlan.CaptureSelectedAsync(image,
                compilation.FreshResourceAncestor, material0, material1, token);
            Captured("texture-captured");
            var staging = await NativeId65NormalUploadStaging.CaptureTexturedAsync(image, texture, token);
            Captured("staging-captured");
            var body = await NativeId65NormalBodyPlan.CaptureTexturedAsync(image, texture, staging, token);
            Captured("body-captured");
            var disc = await NativeId65FreshDiscPlan.CaptureTexturedAsync(image, body, saved.Document.Base.MusicTrackId, token);
            Captured("disc-captured");
            result = new(image, saved, compilation, material0, material1, texture, staging, body, disc);
            token.ThrowIfCancellationRequested();
        }
        catch (Exception error) { failures.Add(error); }
        finally
        {
            // A failed observation never skips another owner. In particular the
            // old static-source factory's success-only check is not our finally.
            Observe(() => checkpoint?.Invoke("before-final-source"));
            if (original != null)
                await ObserveAsync(() => NativeId65FreshDiscSource.RequireWholeOriginalAsync(original, CancellationToken.None));
            FileStream? current = null;
            try
            {
                current = new FileStream(image, FileMode.Open, FileAccess.Read, FileShare.Read);
                await NativeId65FreshDiscSource.RequireWholeOriginalAsync(current, CancellationToken.None);
            }
            catch (Exception error) { failures.Add(error); }
            finally { if (current != null) Observe(current.Dispose); }

            Observe(() => checkpoint?.Invoke("before-final-snapshot"));
            Observe(() => NativeFreshDocumentStorage.RequireAggregateCurrent(saved.Snapshot, CancellationToken.None));
            Observe(() => checkpoint?.Invoke("before-close"));
            if (original != null) Observe(original.Dispose);
        }
        if (failures.Count == 1) ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures.Count > 1)
            throw new IOException("Saved material compilation failed with additional final source, saved-version or close failures.",
                new AggregateException(failures));
        return result!;

        void Captured(string phase)
        {
            checkpoint?.Invoke(phase);
            token.ThrowIfCancellationRequested();
        }
        void Observe(Action operation)
        {
            try { operation(); }
            catch (Exception error) { failures.Add(error); }
        }
        async Task ObserveAsync(Func<Task> operation)
        {
            try { await operation(); }
            catch (Exception error) { failures.Add(error); }
        }
    }

    internal void RequireAncestry(NativeFreshSavedLevelDocumentReadContext saved,
        NativeId65FreshDocumentCompilation compilation,
        NativeStaticTerrainTextureSource material0, NativeStaticTerrainTextureSource material1,
        NativeId65FreshTerrainTexturePlan texture, NativeId65NormalUploadStaging staging,
        NativeId65NormalBodyPlan body, NativeId65FreshDiscPlan disc)
    {
        if (!ReferenceEquals(saved, Saved) || !ReferenceEquals(compilation, BaseCompilation) ||
            !ReferenceEquals(material0, Material0) || !ReferenceEquals(material1, Material1) ||
            !ReferenceEquals(texture, Texture) || !ReferenceEquals(staging, Staging) ||
            !ReferenceEquals(body, Body) || !ReferenceEquals(disc, Disc))
            throw new InvalidDataException("Saved material bridge belongs to its exact actual ancestors, not interchangeable equal hashes.");
        var (first, second) = RequireProfile(Document);
        saved.RequireDocument(Document);
        if (!ReferenceEquals(compilation.Intent, Document.Base) ||
            !ReferenceEquals(body.TextureAncestor, texture) ||
            texture.BindingProfileId != NativeId65FreshTerrainTexturePlan.SelectedBindingProfileId ||
            !Matches(first, material0) || !Matches(second, material1) ||
            staging.ResourceProfile != NativeId65NormalResourceProfile.DirectLqTwoStaticMaterials ||
            staging.UpstreamPlanSha256 != texture.PlanSha256 ||
            body.StagingPlanSha256 != staging.PlanSha256 ||
            disc.Executable.SelectedBuiltInTrack != Document.Base.MusicTrackId ||
            !_canonical.AsSpan().SequenceEqual(NativeFreshLevelDocumentCodec.SerializeCanonical(Document)))
            throw new InvalidDataException("Actual saved descriptor/Base or selected native composition changed.");
        texture.RequireAncestry(compilation.FreshResourceAncestor, material0, material1);
        disc.RequireAncestry(body);
        // Old staging/Body accepts equal actual payloads. The stricter outer
        // references above belong only to this newly issued bridge instance.
    }

    private static (NativeFreshRetailTerrainMaterialIntent First, NativeFreshRetailTerrainMaterialIntent Second)
        RequireProfile(NativeFreshLevelDocumentIntent document) =>
        NativeFreshSavedLevelResearchEligibility.RequireMaterialProfile(document);

    private static bool SameDescriptor(NativeFreshRetailTerrainMaterialIntent first, NativeFreshRetailTerrainMaterialIntent second) =>
        first.SourceVariant == second.SourceVariant && first.SourceWadEntry == second.SourceWadEntry && first.TextureId == second.TextureId;

    private static bool Matches(NativeFreshRetailTerrainMaterialIntent descriptor, NativeStaticTerrainTextureSource source) =>
        descriptor.SourceVariant == NativeFreshRetailTerrainMaterialIntent.SupportedSourceVariant &&
        source.SourceImageSha256 == NativeId65FreshDiscSource.OriginalSha256 &&
        source.SourceWadEntry == descriptor.SourceWadEntry && source.SourceTextureId == descriptor.TextureId;
}
