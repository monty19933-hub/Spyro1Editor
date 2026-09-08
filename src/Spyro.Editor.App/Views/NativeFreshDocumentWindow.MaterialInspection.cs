using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Spyro.Editor.Core.Editing;

namespace Spyro.Editor.App.Views;

// Test work may fail or delay actual worker delivery, never construct a native result.
internal interface INativeFreshMaterialInspectionUiWork
{
    Task<NativeFreshMaterialInspectionWork> CaptureAsync(
        NativeFreshMaterialInspectionRequest request, CancellationToken token);
}

internal sealed partial class NativeFreshDocumentWindow
{
    private readonly StackPanel _materialInspectionPanel = new() { Spacing = 8, IsVisible = false };
    private readonly Button _inspectMaterials = Button("FreshMaterialsInspect", "Inspect material sources", null);
    private readonly Button _cancelMaterialInspection = Button("FreshMaterialsCancelInspect", "Cancel inspection", null);
    private readonly Image[] _materialInspectionImages = [InspectionImage(0), InspectionImage(1)];
    private readonly TextBlock[] _materialInspectionLabels = [InspectionLabel(0), InspectionLabel(1)];
    private readonly TextBlock _materialInspectionStatus = new()
    {
        Name = "FreshMaterialInspectionStatus", TextWrapping = TextWrapping.Wrap, FontSize = 11
    };
    private readonly WriteableBitmap?[] _materialInspectionBitmaps = new WriteableBitmap?[2];
    private bool _materialInspectionBusy;
    private bool _materialInspectionCloseRequested;
    private bool _materialInspectionClosed;
    private bool _materialInspectionCleanupBlocked;
    private long _materialInspectionGeneration;
    private CancellationTokenSource? _materialInspectionCancellation;
    private Task? _materialInspectionTask;
    private List<Exception>? _materialInspectionActiveFailures;
    private NativeFreshMaterialInspectionWork? _materialInspectionResult;
    private Exception? _materialInspectionFailure;
    private INativeFreshMaterialInspectionUiWork? _materialInspectionWorkForTesting;

    private sealed record MaterialInspectionTicket(NativeFreshMaterialInspectionRequest Request,
        NativeFreshLevelDocumentSession Session, NativeFreshLevelDocumentSnapshot Snapshot,
        string? SelectedId, MaterialFields Fields, long Generation, string Image, string Cue);

    internal INativeFreshMaterialInspectionUiWork? MaterialInspectionWorkForTesting
    {
        get => _materialInspectionWorkForTesting;
        set
        {
            if (_materialInspectionBusy) throw new InvalidOperationException("An active inspection worker cannot be replaced.");
            _materialInspectionWorkForTesting = value;
            RefreshChrome();
        }
    }
    internal Task? MaterialInspectionTaskForTesting => _materialInspectionTask;
    internal bool MaterialInspectionBusyForTesting => _materialInspectionBusy;
    internal NativeFreshMaterialInspectionWork? MaterialInspectionResultForTesting => _materialInspectionResult;
    internal Exception? MaterialInspectionFailureForTesting => _materialInspectionFailure;
    internal Action<string>? MaterialInspectionCheckpointForTesting { get; set; }
    // Observes real acquired UI owners, including pre-install owners; never supplies them.
    internal WriteableBitmap?[] MaterialInspectionOwnedBitmapsForTesting => _materialInspectionBitmaps.ToArray();

    private Control BuildMaterialInspectionFields()
    {
        _inspectMaterials.Click += (_, _) =>
        {
            if (!_materialInspectionBusy) _materialInspectionTask = InspectMaterialSourcesAsync();
        };
        _cancelMaterialInspection.Click += (_, _) =>
        {
            if (!_materialInspectionBusy) return;
            InvalidateMaterialInspection();
            _materialInspectionStatus.Text = "Cancellation requested. Waiting for the actual source work and final checks to finish.";
            RefreshChrome();
        };
        var slots = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = 8 };
        for (int slot = 0; slot < 2; slot++)
        {
            var panel = new StackPanel
            {
                Spacing = 5,
                Children =
                {
                    new TextBlock { Text = $"Triangle {slot} source", FontWeight = FontWeight.SemiBold },
                    _materialInspectionImages[slot], _materialInspectionLabels[slot]
                }
            };
            Grid.SetColumn(panel, slot);
            slots.Children.Add(panel);
        }
        _materialInspectionPanel.Children.Add(new WrapPanel { Children = { _inspectMaterials, _cancelMaterialInspection } });
        _materialInspectionPanel.Children.Add(slots);
        _materialInspectionPanel.Children.Add(_materialInspectionStatus);
        Closed += (_, _) =>
        {
            _materialInspectionClosed = true;
            InvalidateMaterialInspection();
        };
        return _materialInspectionPanel;
    }

    private static Image InspectionImage(int slot)
    {
        var image = new Image
        {
            Name = $"FreshMaterial{slot}Preview", Width = 96, Height = 96,
            Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Left
        };
        RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.None);
        return image;
    }

    private static TextBlock InspectionLabel(int slot) => new()
    {
        Name = $"FreshMaterial{slot}Inspection", Text = "Not inspected",
        TextWrapping = TextWrapping.Wrap, FontSize = 11
    };

    private bool TryReadInspectionPair(out NativeFreshFloorMaterialsIntent? pair)
    {
        pair = null;
        if (!_aggregateMode || CurrentAggregateIntent == null || !_materialsEnabled ||
            _addingWizard || AggregateCommandsBlocked || _materialInspectionCleanupBlocked ||
            _materialInspectionClosed || _beginResearch == null ||
            (_researchSourceForTesting ?? _researchSourcePaths) == null) return false;
        try
        {
            pair = NativeFreshFloorMaterialsIntent.Create(
                ReadMaterialSlot(_materialSlots[0]), ReadMaterialSlot(_materialSlots[1]));
            return pair.Triangle0 is NativeFreshRetailTerrainMaterialIntent ||
                pair.Triangle1 is NativeFreshRetailTerrainMaterialIntent;
        }
        catch (Exception error) when (error is ArgumentException or InvalidDataException or OverflowException) { return false; }
    }

    private void RefreshMaterialInspectionChrome()
    {
        bool present = _aggregateMode && CurrentAggregateIntent != null;
        _materialInspectionPanel.IsVisible = present;
        _inspectMaterials.IsVisible = present;
        _inspectMaterials.IsEnabled = TryReadInspectionPair(out _);
        _cancelMaterialInspection.IsVisible = present && _materialInspectionBusy;
        _cancelMaterialInspection.IsEnabled = _materialInspectionBusy &&
            _materialInspectionCancellation?.IsCancellationRequested == false;
    }

    private async Task InspectMaterialSourcesAsync()
    {
        if (!TryReadInspectionPair(out var pair))
        {
            _materialInspectionStatus.Text = "Inspection requires an explicit valid retail slot in a present aggregate, with no other operation or pending route.";
            return;
        }
        List<Exception> failures = [];
        _materialInspectionFailure = null;
        _materialInspectionActiveFailures = failures;
        InvalidateMaterialInspection();
        if (failures.Count != 0 || _materialInspectionCleanupBlocked)
        {
            _materialInspectionActiveFailures = null;
            RefreshChrome();
            return;
        }
        _materialInspectionBusy = true;
        _materialInspectionCancellation = new CancellationTokenSource();
        CancellationToken token = _materialInspectionCancellation.Token;
        IDisposable? owner = null;
        MaterialInspectionTicket? ticket = null;
        NativeFreshMaterialInspectionWork? result = null;
        try
        {
            RefreshChrome();
            RequireCurrentProject();
            owner = _beginResearch!() ??
                throw new InvalidDataException("Material inspection requires an actual editor owner.");
            var paths = CaptureMaterialInspectionPaths();
            var session = _aggregateSession!;
            var request = NativeFreshMaterialInspectionRequest.Create(session.CurrentIntent!, pair!, paths.Image);
            ticket = new(request, session, session.Snapshot, session.SelectedEditorId,
                MaterialFieldSignature(), _materialInspectionGeneration, paths.Image, paths.Cue);
            _materialInspectionStatus.Text = "Inspecting the two explicit raw slots. No fields are applied or saved.";
            INativeFreshMaterialInspectionUiWork work = _materialInspectionWorkForTesting ?? new ActualMaterialInspectionUiWork();
            result = await work.CaptureAsync(request, token);
            RequireMaterialInspectionCurrent(ticket, result, token);
            for (int slot = 0; slot < 2; slot++)
            {
                var preview = result.GetPreview(slot);
                if (preview == null) continue;
                MaterialInspectionCheckpointForTesting?.Invoke($"before-bitmap{slot}");
                CreateInspectionBitmap(slot, preview.CopyRgba());
                MaterialInspectionCheckpointForTesting?.Invoke($"after-bitmap{slot}");
            }
        }
        catch (Exception error) { failures.Add(error); }
        finally
        {
            // Every actual cleanup runs independently of optional notifications.
            Attempt(() => owner?.Dispose());
            Attempt(() => _materialInspectionCancellation?.Dispose());
            _materialInspectionCancellation = null;
            if (failures.Count == 0 && ticket != null && result != null)
            {
                try
                {
                    MaterialInspectionCheckpointForTesting?.Invoke("before-install");
                    // Owner release callbacks may have changed UI state. Recheck
                    // all associations; token remains observable after CTS.Dispose.
                    RequireMaterialInspectionCurrent(ticket, result, token);
                    for (int slot = 0; slot < 2; slot++)
                    {
                        _materialInspectionImages[slot].Source = _materialInspectionBitmaps[slot];
                        var source = result.GetPreview(slot)?.Source;
                        _materialInspectionLabels[slot].Text = source == null
                            ? "Base floor color; no source inspection needed"
                            : $"Captured source WAD {source.SourceWadEntry} / texture {source.SourceTextureId} · LQ descriptor 0 · palette row 0";
                    }
                    _materialInspectionResult = result;
                    _materialInspectionStatus.Text = "Source thumbnail only; not the projected floor or a native output approval.";
                }
                catch (Exception error) { failures.Add(error); }
            }
            if (failures.Count != 0)
            {
                ClearMaterialInspectionImages(failures);
                _materialInspectionFailure = InspectionFailure(failures);
                _materialInspectionStatus.Text = "Inspection did not complete: " + DescribeInspectionFailures(failures) +
                    " Raw fields and the applied/saved document are retained.";
            }
            _materialInspectionActiveFailures = null;
            _materialInspectionBusy = false;
            try { RefreshChrome(); }
            catch (Exception error)
            {
                failures.Add(error);
                _materialInspectionFailure = InspectionFailure(failures);
                _materialInspectionStatus.Text = "Inspection controls could not be refreshed: " + DescribeInspectionFailures(failures);
            }
            bool close = _materialInspectionCloseRequested;
            _materialInspectionCloseRequested = false;
            if (close && !_materialInspectionClosed) Dispatcher.UIThread.Post(Close);
        }
        void Attempt(Action action)
        {
            try { action(); }
            catch (Exception error) { failures.Add(error); }
        }
    }

    private void RequireMaterialInspectionCurrent(MaterialInspectionTicket ticket,
        NativeFreshMaterialInspectionWork result, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        RequireCurrentProject();
        if (_materialInspectionClosed || _materialInspectionCloseRequested || !_aggregateMode ||
            !IsMaterialInspectionContextCurrent(ticket.Session, ticket.Snapshot, ticket.Request.Document, _aggregateSession) ||
            ticket.SelectedId != _aggregateSession?.SelectedEditorId || ticket.Generation != _materialInspectionGeneration ||
            ticket.Fields != MaterialFieldSignature() || CaptureMaterialInspectionPaths() != (ticket.Image, ticket.Cue))
            throw new InvalidDataException("The inspection's actual document, raw fields, source selection or window state changed. Inspect again explicitly.");
        result.RequireRequest(ticket.Request);
        token.ThrowIfCancellationRequested();
    }

    internal static bool IsMaterialInspectionContextCurrent(NativeFreshLevelDocumentSession expectedSession,
        NativeFreshLevelDocumentSnapshot expectedSnapshot, NativeFreshLevelDocumentIntent expectedIntent,
        NativeFreshLevelDocumentSession? currentSession) =>
        expectedSession != null && expectedSnapshot != null && expectedIntent != null &&
        ReferenceEquals(expectedSession, currentSession) && ReferenceEquals(expectedSnapshot, currentSession?.Snapshot) &&
        ReferenceEquals(expectedIntent, currentSession?.CurrentIntent);

    private (string Image, string Cue) CaptureMaterialInspectionPaths()
    {
        var provider = _researchSourceForTesting ?? _researchSourcePaths ??
            throw new InvalidDataException("Select the original BIN/CUE in the main editor before material inspection.");
        var paths = provider();
        ArgumentException.ThrowIfNullOrWhiteSpace(paths.Image);
        ArgumentException.ThrowIfNullOrWhiteSpace(paths.Cue);
        return (Path.GetFullPath(paths.Image), Path.GetFullPath(paths.Cue));
    }

    private void InvalidateMaterialInspection()
    {
        _materialInspectionGeneration = checked(_materialInspectionGeneration + 1);
        List<Exception> failures = _materialInspectionActiveFailures ?? [];
        if (_materialInspectionCancellation is { IsCancellationRequested: false } cancellation)
            try { cancellation.Cancel(); } catch (Exception error) { failures.Add(error); }
        ClearMaterialInspectionImages(failures);
        if (failures.Count != 0)
        {
            _materialInspectionFailure = InspectionFailure(failures);
            _materialInspectionStatus.Text = "Inspection cleanup did not complete: " + DescribeInspectionFailures(failures);
        }
    }

    private void ClearMaterialInspectionImages(List<Exception> failures)
    {
        _materialInspectionResult = null;
        for (int slot = 0; slot < 2; slot++)
        {
            WriteableBitmap? owned = _materialInspectionBitmaps[slot];
            _materialInspectionBitmaps[slot] = null; // An owner is attempted at most once.
            Attempt(() => _materialInspectionImages[slot].Source = null);
            Attempt(() => _materialInspectionLabels[slot].Text = "Not inspected");
            if (owned == null) continue;
            Attempt(() => MaterialInspectionCheckpointForTesting?.Invoke($"before-dispose-bitmap{slot}"));
            try { owned.Dispose(); }
            catch (Exception error)
            {
                failures.Add(error);
                // No retry/new allocation after an uncertain actual bitmap release.
                _materialInspectionCleanupBlocked = true;
            }
            Attempt(() => MaterialInspectionCheckpointForTesting?.Invoke($"after-dispose-bitmap{slot}"));
        }
        void Attempt(Action action)
        {
            try { action(); }
            catch (Exception error) { failures.Add(error); }
        }
    }

    private bool DeferMaterialInspectionClose(WindowClosingEventArgs e)
    {
        InvalidateMaterialInspection();
        if (!_materialInspectionBusy) return false;
        e.Cancel = true;
        _materialInspectionCloseRequested = true;
        _materialInspectionStatus.Text = "Close requested. Waiting for actual inspection and source final checks; normal unsaved-change choices follow.";
        RefreshChrome();
        return true;
    }

    private void CreateInspectionBitmap(int slot, Rgba32[] pixels)
    {
        if (pixels.Length != 32 * 32) throw new InvalidDataException("Expected the actual 32×32 source preview.");
        var bitmap = new WriteableBitmap(new PixelSize(32, 32), new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Unpremul);
        // Acknowledge the acquired owner BEFORE any fallible Lock/pixel work.
        // The outer all-attempted cleanup owns its sole eventual Dispose.
        _materialInspectionBitmaps[slot] = bitmap;
        ILockedFramebuffer? locked = null;
        List<Exception> failures = [];
        try
        {
            locked = bitmap.Lock();
            if (locked.Format != PixelFormat.Rgba8888) throw new InvalidDataException("Inspection bitmap requires RGBA8888.");
            byte[] bytes = pixels.SelectMany(p => new[] { p.R, p.G, p.B, p.A }).ToArray();
            for (int row = 0; row < 32; row++)
                Marshal.Copy(bytes, row * 32 * 4, IntPtr.Add(locked.Address, row * locked.RowBytes), 32 * 4);
        }
        catch (Exception error) { failures.Add(error); }
        finally
        {
            try { locked?.Dispose(); }
            catch (Exception error) { failures.Add(error); }
        }
        if (failures.Count == 1) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures.Count > 1) throw InspectionFailure(failures);
    }

    private static Exception InspectionFailure(List<Exception> failures) => failures.Count == 1 ? failures[0] :
        new IOException("Material inspection and UI owner accounting failures.", new AggregateException(failures));
    private static string DescribeInspectionFailures(IEnumerable<Exception> failures) =>
        string.Join(" | ", failures.SelectMany(InspectionFailureMessages));
    private static IEnumerable<string> InspectionFailureMessages(Exception error) =>
        error is AggregateException group ? group.InnerExceptions.SelectMany(InspectionFailureMessages) :
        error.InnerException != null ? new[] { error.Message }.Concat(InspectionFailureMessages(error.InnerException)) : [error.Message];

    private sealed class ActualMaterialInspectionUiWork : INativeFreshMaterialInspectionUiWork
    {
        public Task<NativeFreshMaterialInspectionWork> CaptureAsync(NativeFreshMaterialInspectionRequest request, CancellationToken token) =>
            NativeFreshMaterialInspectionWork.CaptureAsync(request, token);
    }
}
