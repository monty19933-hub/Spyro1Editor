using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.App.Views;

/// <summary>Neutral raw inspection intent, not a saved revision or native eligibility certificate.</summary>
internal sealed class NativeFreshMaterialInspectionRequest
{
    private NativeFreshMaterialInspectionRequest(NativeFreshLevelDocumentIntent document,
        NativeFreshFloorMaterialsIntent materials, string image)
    {
        Document = document;
        Materials = materials;
        OriginalImagePath = image;
    }

    internal NativeFreshLevelDocumentIntent Document { get; }
    internal NativeFreshFloorMaterialsIntent Materials { get; }
    internal string OriginalImagePath { get; }

    internal static NativeFreshMaterialInspectionRequest Create(NativeFreshLevelDocumentIntent document,
        NativeFreshFloorMaterialsIntent materials, string originalBin)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(materials);
        ArgumentException.ThrowIfNullOrWhiteSpace(originalBin);
        if (materials.Triangle0 is not NativeFreshRetailTerrainMaterialIntent &&
            materials.Triangle1 is not NativeFreshRetailTerrainMaterialIntent)
            throw new InvalidDataException("At least one explicit retail material is required for source inspection.");
        // The closed model validates both tags, the known variant and integer
        // coordinates. This does not claim the named native record is eligible.
        return new(document, materials, Path.GetFullPath(originalBin));
    }
}

/// <summary>
/// Completed bounded source-inspection result. Only the actual factory below
/// can construct it; no injected IO, source/result constructor or mutable job state.
/// Window generation, source-selection and current-session fences belong to its caller.
/// </summary>
internal sealed class NativeFreshMaterialInspectionWork
{
    private NativeFreshMaterialInspectionWork(NativeFreshMaterialInspectionRequest request,
        NativeFreshMaterialSourcePreview? first, NativeFreshMaterialSourcePreview? second)
    {
        Request = request;
        FirstPreview = first;
        SecondPreview = second;
        RequireSlot(request.Materials.Triangle0, first);
        RequireSlot(request.Materials.Triangle1, second);
    }

    internal NativeFreshMaterialInspectionRequest Request { get; }
    internal NativeFreshMaterialSourcePreview? FirstPreview { get; }
    internal NativeFreshMaterialSourcePreview? SecondPreview { get; }
    internal int CapturedSlotCount => (FirstPreview == null ? 0 : 1) + (SecondPreview == null ? 0 : 1);
    internal bool DocumentSaveBindingClaimed => false;
    internal bool DestinationAllocationProven => false;
    internal bool SchematicRenderingProven => false;
    internal bool RuntimeVerified => false;
    internal bool PublicationApproved => false;

    internal NativeFreshMaterialSourcePreview? GetPreview(int slot) => slot switch
    {
        0 => FirstPreview,
        1 => SecondPreview,
        _ => throw new ArgumentOutOfRangeException(nameof(slot))
    };

    internal void RequireRequest(NativeFreshMaterialInspectionRequest request)
    {
        if (!ReferenceEquals(Request, request))
            throw new InvalidDataException("Inspection belongs to its exact request, not interchangeable equal fields or hashes.");
    }

    internal static Task<NativeFreshMaterialInspectionWork> CaptureAsync(
        NativeFreshMaterialInspectionRequest request, CancellationToken token = default) =>
        CaptureCoreAsync(request, null, token);

    // App-only observation: cannot replace IO, a source, a preview or the result.
    internal static Task<NativeFreshMaterialInspectionWork> CaptureForSmokeAsync(
        NativeFreshMaterialInspectionRequest request, Action<string> checkpoint,
        CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        return CaptureCoreAsync(request, checkpoint, token);
    }

    private static Task<NativeFreshMaterialInspectionWork> CaptureCoreAsync(
        NativeFreshMaterialInspectionRequest request, Action<string>? checkpoint, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(request);
        // Never use a scheduling cancellation token. The returned task owns the
        // actual synchronous source calls and all final observations until settled.
        return Task.Run(() => CaptureOwned(request, checkpoint, token));
    }

    private static NativeFreshMaterialInspectionWork CaptureOwned(
        NativeFreshMaterialInspectionRequest request, Action<string>? checkpoint, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); // No IO or callback for pre-cancel.
        FileStream? held = null;
        NativeFreshMaterialInspectionWork? result = null;
        List<Exception> failures = [];
        try
        {
            held = new FileStream(request.OriginalImagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            Captured("source-opened");
            RequireOriginal(held);
            Captured("source-authenticated");
            var first = CaptureSlot(request.Materials.Triangle0, 0);
            var second = CaptureSlot(request.Materials.Triangle1, 1);
            result = new(request, first, second);
            Captured("result-captured");
        }
        catch (Exception error) { failures.Add(error); }
        finally
        {
            // Every observer and every actual observation/close is attempted
            // independently; a throwing observer cannot skip a source owner.
            Observe(() => checkpoint?.Invoke("before-final-held"));
            if (held != null) Observe(() => RequireOriginal(held));
            Observe(() => checkpoint?.Invoke("after-final-held"));
            Observe(() => checkpoint?.Invoke("before-final-current"));
            FileStream? current = null;
            try
            {
                current = new FileStream(request.OriginalImagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                RequireOriginal(current);
            }
            catch (Exception error) { failures.Add(error); }
            finally
            {
                Observe(() => checkpoint?.Invoke("before-current-close"));
                if (current != null) Observe(current.Dispose);
                Observe(() => checkpoint?.Invoke("after-current-close"));
            }
            Observe(() => checkpoint?.Invoke("after-final-current"));
            Observe(() => checkpoint?.Invoke("before-held-close"));
            if (held != null) Observe(held.Dispose);
            Observe(() => checkpoint?.Invoke("after-held-close"));
        }
        if (failures.Count == 1) ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures.Count > 1)
            throw new IOException("Material inspection and independent original final/close checks did not complete cleanly.",
                new AggregateException(failures));
        // Inspection has no commit boundary. A late cancellation may suppress
        // successful delivery, but only after all real owners settled, and never
        // in place of an already captured source/finalization failure.
        token.ThrowIfCancellationRequested();
        return result!;

        NativeFreshMaterialSourcePreview? CaptureSlot(NativeFreshFloorMaterialIntent material, int slot)
        {
            token.ThrowIfCancellationRequested();
            if (material is not NativeFreshRetailTerrainMaterialIntent retail)
            {
                if (material.Tag != "base-floor-color") throw new InvalidDataException("Unsupported closed material tag.");
                return null;
            }
            Captured($"before-material{slot}");
            var source = NativeStaticTerrainTextureSource.Capture(request.OriginalImagePath,
                retail.SourceWadEntry, retail.TextureId);
            var preview = NativeFreshMaterialSourcePreview.Capture(source);
            Captured($"material{slot}-captured");
            return preview;
        }
        void Captured(string phase)
        {
            checkpoint?.Invoke(phase);
            token.ThrowIfCancellationRequested();
        }
        void Observe(Action action)
        {
            try { action(); }
            catch (Exception error) { failures.Add(error); }
        }
    }

    private static void RequireSlot(NativeFreshFloorMaterialIntent material, NativeFreshMaterialSourcePreview? preview)
    {
        if (material is NativeFreshRetailTerrainMaterialIntent retail)
        {
            if (preview == null || preview.Source.SourceWadEntry != retail.SourceWadEntry ||
                preview.Source.SourceTextureId != retail.TextureId || preview.DescriptorIndex != 0 ||
                preview.Width != 32 || preview.Height != 32 || preview.PaletteRowCount != 16 ||
                preview.Source.SourceImageSha256 != NativeMobySourceInventory.OriginalImageSha256)
                throw new InvalidDataException("Actual source preview does not match its exact ordered requested material.");
        }
        else if (material.Tag != "base-floor-color" || preview != null)
            throw new InvalidDataException("Base-color intent cannot acquire a retail source preview.");
    }

    private static void RequireOriginal(Stream stream)
    {
        long length = NativeMobySourceInventory.OriginalImageByteLength;
        if (!stream.CanRead || !stream.CanSeek || stream.Length != length)
            throw new InvalidDataException("Material inspection requires the complete original BIN length.");
        stream.Position = 0;
        if (Convert.ToHexStringLower(SHA256.HashData(stream)) != NativeMobySourceInventory.OriginalImageSha256 ||
            stream.Length != length || stream.Position != length || stream.ReadByte() != -1 ||
            stream.Position != length || stream.Length != length)
            throw new InvalidDataException("Material inspection requires the unchanged whole original SHA and true EOF.");
    }
}
