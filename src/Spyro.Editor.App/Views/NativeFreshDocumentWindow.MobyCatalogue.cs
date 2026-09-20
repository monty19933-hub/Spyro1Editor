using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Editing;

namespace Spyro.Editor.App.Views;

internal sealed partial class NativeFreshDocumentWindow
{
    private const string MobyAuthoringStatus = "Authoring reference only — research eligibility depends on the complete document; native dependencies are not resolved by this catalogue.";
    private readonly Button _browseMobys = Button("FreshNativeMobyBrowse", "Browse native Moby sources…", null);
    private readonly Button _cancelNativeMobyAdd = Button("FreshNativeMobyCancelAdd", "Cancel new native Moby", null);
    private readonly StackPanel _mobyCataloguePanel = new() { Name = "FreshMobyCataloguePanel", Spacing = 8, IsVisible = false };
    private readonly TextBox _mobyCatalogueSearch = new() { Name = "FreshMobyCatalogueSearch", PlaceholderText = "Search name, class, level or true index", MaxLength = 256 };
    private readonly ComboBox _mobyCatalogueLevel = new ReadOnlySelectionComboBox { Name = "FreshMobyCatalogueLevel", HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly ComboBox _mobyCatalogueView = new ReadOnlySelectionComboBox
    {
        Name = "FreshMobyCatalogueView", ItemsSource = Enum.GetValues<NativeMobyCatalogueView>(), SelectedItem = NativeMobyCatalogueView.PlacedRows,
        HorizontalAlignment = HorizontalAlignment.Stretch
    };
    private readonly ListBox _mobyCatalogueEntries = new() { Name = "FreshMobyCatalogueEntries", Height = 220 };
    private readonly TextBlock _mobyCatalogueDetails = new() { Name = "FreshMobyCatalogueDetails", TextWrapping = TextWrapping.Wrap, FontSize = 11 };
    private readonly TextBlock _mobyCatalogueStatus = new() { Name = "FreshMobyCatalogueStatus", TextWrapping = TextWrapping.Wrap, FontSize = 11 };
    private readonly Button _stageCatalogueMoby = Button("FreshMobyCatalogueStage", "Use source for new placement", null);
    private readonly Button _cancelMobyCatalogue = Button("FreshMobyCatalogueCancel", "Cancel catalogue", null);
    private INativeFreshMobyCatalogueProvider? _mobyCatalogueProvider;
    private NativeFreshMobyCatalogueListing? _mobyCatalogueListing;
    private TaskCompletionSource<NativeFreshMobyCatalogueItem?>? _mobyCatalogueChoice;
    private Task? _mobyCatalogueTask;
    private Exception? _mobyCatalogueFailure;
    private bool _mobyCatalogueBusy, _mobyCatalogueCancelRequested, _mobyCatalogueClosed, _mobyCatalogueCloseRequested;
    private bool _mobyCatalogueCleanupBlocked, _refreshingMobyCatalogue;
    private long _mobyCatalogueGeneration;
    private PendingNativeMobyAdd? _pendingNativeMobyAdd;

    private sealed record PendingNativeMobyAdd(NativeFreshRetailMobyReference Source, string Label, string? PriorSelection);
    private sealed record MobyCatalogueLevel(string Key, string Name) { public override string ToString() => Name; }
    private sealed record MobyCatalogueTicket(NativeFreshLevelDocumentSession Session, NativeFreshLevelDocumentSnapshot Snapshot,
        NativeFreshLevelDocumentIntent Applied, string? Selection, string?[] BaseFields, object? Music, string?[] RouteFields,
        NativeMobyFields NativeFields, MaterialFields Materials, FamilyFields Family,
        long RawGeneration, long RequestGeneration, string Image, string Cue);

    internal INativeFreshMobyCatalogueProvider? MobyCatalogueProviderForTesting
    {
        get => _mobyCatalogueProvider;
        set
        {
            if (_mobyCatalogueBusy) throw new InvalidOperationException("An active catalogue provider cannot be replaced.");
            _mobyCatalogueProvider = value;
            RefreshChrome();
        }
    }
    internal Task? MobyCatalogueTaskForTesting => _mobyCatalogueTask;
    internal bool MobyCatalogueBusyForTesting => _mobyCatalogueBusy;
    internal Exception? MobyCatalogueFailureForTesting => _mobyCatalogueFailure;

    private bool CanBrowseMobys => _aggregateMode && CurrentAggregateIntent != null && !AggregateCommandsBlocked &&
        !_forkRecoveryPending && !_materialInspectionCleanupBlocked && !_mobyCatalogueCleanupBlocked && !_mobyCatalogueClosed &&
        !HasPendingPlacementAdd && !HasUnappliedFields &&
        CurrentAggregateIntent.Placements.Count < NativeFreshLevelDocumentIntent.MaximumPlacements &&
        _mobyCatalogueProvider != null && _beginResearch != null && (_researchSourceForTesting ?? _researchSourcePaths) != null;

    private Control BuildMobyCataloguePanel()
    {
        _browseMobys.Click += (_, _) => { if (!_mobyCatalogueBusy) _mobyCatalogueTask = BrowseMobySourcesAsync(); };
        _cancelNativeMobyAdd.Click += (_, _) => CancelNativeMobyAdd();
        _cancelMobyCatalogue.Click += (_, _) =>
        {
            if (!_mobyCatalogueBusy || _mobyCatalogueCancelRequested) return;
            InvalidateMobyCatalogue();
            _mobyCatalogueStatus.Text = "Cancellation requested. Inventory capture cannot be interrupted; waiting for its actual task to finish before releasing the editor owner.";
            RefreshChrome();
        };
        _stageCatalogueMoby.Click += (_, _) =>
        {
            if (!_mobyCatalogueBusy || _mobyCatalogueCancelRequested || _mobyCatalogueListing == null ||
                _mobyCatalogueEntries.SelectedItem is not NativeFreshMobyCatalogueItem entry ||
                _mobyCatalogueListing.GetAuthoringSource(entry) == null) return;
            _mobyCatalogueChoice?.TrySetResult(entry);
        };
        _mobyCatalogueSearch.TextChanged += (_, _) => RefreshMobyCatalogueRows();
        _mobyCatalogueLevel.SelectionChanged += (_, _) => RefreshMobyCatalogueRows();
        _mobyCatalogueView.SelectionChanged += (_, _) => RefreshMobyCatalogueRows();
        _mobyCatalogueEntries.SelectionChanged += (_, _) => RefreshMobyCatalogueDetails();
        _mobyCatalogueView.ItemTemplate = new FuncDataTemplate<NativeMobyCatalogueView>((view, _) => new TextBlock
        {
            Text = view switch { NativeMobyCatalogueView.PlacedRows => "Placed donor rows", NativeMobyCatalogueView.Resources => "Resources (inspect-only)", _ => "All catalogue entries" }
        });
        _mobyCatalogueEntries.ItemTemplate = new FuncDataTemplate<NativeFreshMobyCatalogueItem>((entry, _) => new TextBlock
        {
            Text = entry == null ? "" : $"{entry.Label} · 0x{entry.ActorId:X4}\n{entry.SourceLevelName} · " +
                (entry.TrueIndex is int index ? $"T{index}" : $"root {entry.RegistrationSlot}") + "\n" +
                (_mobyCatalogueListing?.GetAuthoringSource(entry) == null ? "Inspect-only" : "Authoring reference only"),
            TextWrapping = TextWrapping.Wrap, FontSize = 11
        });
        _mobyCataloguePanel.Children.Add(Section("Native source catalogue", "Browse placed donors from all inventoried levels. Resources remain inspect-only. Choosing a row does not import a model, behavior or dependency package."));
        _mobyCataloguePanel.Children.Add(_mobyCatalogueSearch);
        _mobyCataloguePanel.Children.Add(_mobyCatalogueLevel);
        _mobyCataloguePanel.Children.Add(_mobyCatalogueView);
        _mobyCataloguePanel.Children.Add(_mobyCatalogueEntries);
        _mobyCataloguePanel.Children.Add(_mobyCatalogueDetails);
        _mobyCataloguePanel.Children.Add(new WrapPanel { Children = { _stageCatalogueMoby, _cancelMobyCatalogue } });
        _mobyCataloguePanel.Children.Add(_mobyCatalogueStatus);
        Closed += (_, _) => { _mobyCatalogueClosed = true; InvalidateMobyCatalogue(); };
        return _mobyCataloguePanel;
    }

    private void RefreshMobyCatalogueChrome(bool editable)
    {
        _browseMobys.IsVisible = _aggregateMode;
        _browseMobys.IsEnabled = CanBrowseMobys;
        _cancelNativeMobyAdd.IsVisible = _aggregateMode && _pendingNativeMobyAdd != null;
        _cancelNativeMobyAdd.IsEnabled = editable && _pendingNativeMobyAdd != null;
        _mobyCataloguePanel.IsVisible = _mobyCatalogueBusy;
        bool choosing = _mobyCatalogueBusy && !_mobyCatalogueCancelRequested && _mobyCatalogueListing != null;
        _mobyCatalogueSearch.IsEnabled = _mobyCatalogueLevel.IsEnabled = _mobyCatalogueView.IsEnabled = _mobyCatalogueEntries.IsEnabled = choosing;
        _cancelMobyCatalogue.IsEnabled = _mobyCatalogueBusy && !_mobyCatalogueCancelRequested;
        RefreshMobyCatalogueDetails();
    }

    private void RefreshMobyCatalogueRows()
    {
        if (_refreshingMobyCatalogue || _mobyCatalogueListing == null) return;
        try
        {
            string? selected = (_mobyCatalogueEntries.SelectedItem as NativeFreshMobyCatalogueItem)?.SourceVariantId;
            if (_mobyCatalogueView.SelectedItem is not NativeMobyCatalogueView view)
                throw new InvalidDataException("Select an explicit catalogue view.");
            var entries = _mobyCatalogueListing.Search(_mobyCatalogueSearch.Text,
                (_mobyCatalogueLevel.SelectedItem as MobyCatalogueLevel)?.Key, view);
            _mobyCatalogueEntries.ItemsSource = entries;
            _mobyCatalogueEntries.SelectedItem = entries.FirstOrDefault(entry => entry.SourceVariantId == selected);
            _mobyCatalogueStatus.Text = $"{entries.Count} matching entries. Source variants stay distinct. Discovery grants no native import or runtime approval.";
        }
        catch (Exception error) when (error is InvalidDataException or ArgumentException)
        {
            _mobyCatalogueEntries.ItemsSource = null;
            _mobyCatalogueStatus.Text = "Catalogue search refused: " + error.Message;
        }
        RefreshMobyCatalogueDetails();
    }

    private void RefreshMobyCatalogueDetails()
    {
        var entry = _mobyCatalogueEntries.SelectedItem as NativeFreshMobyCatalogueItem;
        var source = _mobyCatalogueListing?.GetAuthoringSource(entry);
        _stageCatalogueMoby.IsEnabled = _mobyCatalogueBusy && !_mobyCatalogueCancelRequested && source != null;
        _mobyCatalogueDetails.Text = entry == null ? "Select an entry to inspect its source identity and authoring status." :
            $"{entry.Label}\n{entry.SourceLevelName} · WAD {entry.SourceWadEntry} · " +
            (entry.TrueIndex is int index ? $"true index {index}" : $"registration slot {entry.RegistrationSlot}") +
            $" · class {entry.ActorId} (0x{entry.ActorId:X4})\n{entry.ClassificationEvidence}\n" +
            (source == null ? "Inspect-only — no original-retail placed-row reference is available for this entry." : MobyAuthoringStatus);
    }

    private async Task BrowseMobySourcesAsync()
    {
        if (!CanBrowseMobys)
        {
            _status.Text = "Catalogue requires an applied aggregate with no pending fields, the active editor owner and an available source provider. No fields were applied, saved or discarded.";
            return;
        }
        _mobyCatalogueBusy = true;
        _mobyCatalogueCancelRequested = false;
        _mobyCatalogueGeneration = checked(_mobyCatalogueGeneration + 1);
        _mobyCatalogueFailure = null;
        _mobyCatalogueListing = null;
        List<Exception> failures = [];
        IDisposable? owner = null;
        MobyCatalogueTicket? ticket = null;
        PendingNativeMobyAdd? pending = null;
        try
        {
            RefreshChrome();
            RequireCurrentProject();
            owner = _beginResearch!() ?? throw new InvalidDataException("Catalogue capture requires an actual editor owner.");
            var paths = CaptureResearchSourcePaths();
            var session = _aggregateSession!;
            ticket = new(session, session.Snapshot, session.CurrentIntent!, session.SelectedEditorId,
                _fields.Values.Select(field => field.Text).ToArray(), _music.SelectedItem,
                _routeFields.Values.Select(field => field.Text).ToArray(), NativeMobyFieldSignature(), MaterialFieldSignature(), FamilyFieldSignature(),
                _materialInspectionGeneration, _mobyCatalogueGeneration, paths.Image, paths.Cue);
            RequireMobyCatalogueCurrent(ticket);
            _mobyCatalogueStatus.Text = "Reading the selected original source inventory. No fields are applied or saved. Cancel requests wait for the actual capture to finish.";
            // Do not pass a cancellation token to Task.Run, abandon an await, or
            // release the actual owner while the synchronous Inventory still runs.
            var listing = await _mobyCatalogueProvider!.CaptureAsync(ticket.Image);
            RequireMobyCatalogueCurrent(ticket);
            ArgumentNullException.ThrowIfNull(listing);
            _mobyCatalogueListing = listing;
            _mobyCatalogueChoice = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _refreshingMobyCatalogue = true;
            try
            {
                _mobyCatalogueSearch.Text = "";
                _mobyCatalogueView.SelectedItem = NativeMobyCatalogueView.PlacedRows;
                MobyCatalogueLevel[] levels = [new("", "All source levels"), .. listing.Entries
                    .GroupBy(entry => entry.SourceLevelKey, StringComparer.Ordinal)
                    .OrderBy(group => group.First().SourceLevelId)
                    .Select(group => new MobyCatalogueLevel(group.Key, group.First().SourceLevelName))];
                _mobyCatalogueLevel.ItemsSource = levels;
                _mobyCatalogueLevel.SelectedItem = levels[0];
            }
            finally { _refreshingMobyCatalogue = false; }
            RefreshMobyCatalogueRows();
            RefreshChrome();
            QueueMobyCatalogueReveal(ticket, listing);
            NativeFreshMobyCatalogueItem? chosen = await _mobyCatalogueChoice.Task;
            if (chosen != null)
            {
                RequireMobyCatalogueCurrent(ticket);
                var source = listing.GetAuthoringSource(chosen) ?? throw new InvalidDataException("The selected entry is inspect-only.");
                pending = new(source, chosen.Label, ticket.Selection);
            }
        }
        catch (Exception error) { failures.Add(error); }
        finally
        {
            // Capture AND the chooser have settled before the sole release.
            // A release callback may mutate state: recheck before staging Add.
            try { owner?.Dispose(); }
            catch (Exception error) { failures.Add(error); _mobyCatalogueCleanupBlocked = true; }
            if (failures.Count == 0 && pending != null && ticket != null)
                try { RequireMobyCatalogueCurrent(ticket); }
                catch (Exception error) { failures.Add(error); }
            _mobyCatalogueListing = null;
            _mobyCatalogueChoice = null;
            _mobyCatalogueEntries.ItemsSource = null;
            _mobyCatalogueBusy = false;
            try
            {
                if (failures.Count == 0 && pending != null && ticket != null && !_mobyCatalogueCancelRequested)
                {
                    _pendingNativeMobyAdd = pending;
                    WriteFields();
                    _status.Text = $"New native Moby ({pending.Label}) is pending. Enter raw XYZ and native yaw, then Apply. No editor ID exists yet. Cancel restores the prior selection. " + MobyAuthoringStatus;
                    QueuePendingNativeMobyReveal(ticket, pending);
                }
                else
                {
                    RefreshChrome();
                    _status.Text = "Catalogue closed without adding or saving a placement. Existing document and raw fields are retained.";
                }
            }
            catch (Exception error) { failures.Add(error); }
            if (failures.Count != 0)
            {
                _mobyCatalogueFailure = failures.Count == 1 ? failures[0] :
                    new IOException("Catalogue capture and UI owner accounting failures.", new AggregateException(failures));
                _status.Text = "Catalogue did not complete: " + DescribeInspectionFailures(failures) + " Existing document and raw fields are retained.";
            }
            bool close = _mobyCatalogueCloseRequested;
            _mobyCatalogueCloseRequested = false;
            if (close && !_mobyCatalogueClosed) Dispatcher.UIThread.Post(Close);
        }
    }

    private void RequireMobyCatalogueCurrent(MobyCatalogueTicket ticket)
    {
        if (_mobyCatalogueCancelRequested || ticket.RequestGeneration != _mobyCatalogueGeneration)
            throw new OperationCanceledException("Catalogue delivery was canceled; the actual capture was drained.");
        RequireCurrentProject();
        if (!_aggregateMode || _documentMode.SelectedItem as string != AggregateModeLabel || _mobyCatalogueClosed ||
            _decisionBusy || _modeSwitchBusy || _forkBusy || _forkRecoveryPending || _researchBusy || _materialInspectionBusy ||
            HasPendingPlacementAdd || !ReferenceEquals(ticket.Session, _aggregateSession) ||
            !ReferenceEquals(ticket.Snapshot, _aggregateSession?.Snapshot) || !ReferenceEquals(ticket.Applied, CurrentAggregateIntent) ||
            ticket.Selection != _aggregateSession?.SelectedEditorId || !ticket.BaseFields.SequenceEqual(_fields.Values.Select(field => field.Text)) ||
            !ReferenceEquals(ticket.Music, _music.SelectedItem) || !ticket.RouteFields.SequenceEqual(_routeFields.Values.Select(field => field.Text)) ||
            ticket.NativeFields != NativeMobyFieldSignature() || ticket.Materials != MaterialFieldSignature() || ticket.Family != FamilyFieldSignature() ||
            ticket.RawGeneration != _materialInspectionGeneration || HasUnappliedFields || CaptureResearchSourcePaths() != (ticket.Image, ticket.Cue))
            throw new InvalidDataException("The catalogue's document, raw fields, selection, mode, project or source changed. Browse again explicitly; no replacement was adopted.");
    }

    private void QueueMobyCatalogueReveal(MobyCatalogueTicket ticket, NativeFreshMobyCatalogueListing listing) =>
        QueueMobyReveal(_mobyCatalogueSearch, _mobyCatalogueEntries, () =>
        {
            if (!_mobyCatalogueBusy || _mobyCatalogueCleanupBlocked || !ReferenceEquals(listing, _mobyCatalogueListing)) return false;
            RequireMobyCatalogueCurrent(ticket);
            return true;
        });

    private void QueuePendingNativeMobyReveal(MobyCatalogueTicket ticket, PendingNativeMobyAdd pending)
    {
        long rawGeneration = _materialInspectionGeneration;
        QueueMobyReveal(_nativeMobySourceIdentity, _nativeMobyFields["FreshNativeMobyYaw"], () =>
        {
            if (!ReferenceEquals(pending, _pendingNativeMobyAdd) || AggregateCommandsBlocked || !_aggregateMode ||
                _documentMode.SelectedItem as string != AggregateModeLabel || _mobyCatalogueClosed || _mobyCatalogueCloseRequested ||
                _mobyCatalogueCancelRequested || _materialInspectionCleanupBlocked || _addingWizard || _addingFamily || _forkRecoveryPending ||
                ticket.RequestGeneration != _mobyCatalogueGeneration || rawGeneration != _materialInspectionGeneration ||
                !ReferenceEquals(ticket.Session, _aggregateSession) || !ReferenceEquals(ticket.Snapshot, _aggregateSession?.Snapshot) ||
                !ReferenceEquals(ticket.Applied, CurrentAggregateIntent) || ticket.Selection != _aggregateSession?.SelectedEditorId)
                return false;
            RequireCurrentProject();
            return CaptureResearchSourcePaths() == (ticket.Image, ticket.Cue);
        });
    }

    private static void QueueMobyReveal(Control first, Control last, Func<bool> isCurrent) =>
        Dispatcher.UIThread.Post(() =>
        {
            // The queued layout callback grants no authoring permission. Recheck
            // its exact operation after layout; canceled/stale work cannot scroll.
            try
            {
                if (!isCurrent() || !first.IsVisible || !last.IsVisible ||
                    first.Bounds.Width <= 0 || last.Bounds.Height <= 0 || last.TranslatePoint(default, first) is not { } end)
                    return;
                first.BringIntoView(new Rect(0, 0, Math.Max(first.Bounds.Width, end.X + last.Bounds.Width),
                    end.Y + last.Bounds.Height));
            }
            catch (Exception)
            {
                // Source/project checks may fail after the UI operation settled.
                // Skip optional scrolling; never turn it into a late Add or error.
            }
        }, DispatcherPriority.Loaded);

    private void InvalidateMobyCatalogue()
    {
        if (!_mobyCatalogueBusy || _mobyCatalogueCancelRequested) return;
        _mobyCatalogueCancelRequested = true;
        _mobyCatalogueGeneration = checked(_mobyCatalogueGeneration + 1);
        _mobyCatalogueChoice?.TrySetResult(null);
    }

    private bool DeferMobyCatalogueClose(WindowClosingEventArgs e)
    {
        if (!_mobyCatalogueBusy) return false;
        e.Cancel = true;
        _mobyCatalogueCloseRequested = true;
        InvalidateMobyCatalogue();
        _mobyCatalogueStatus.Text = "Close requested. Waiting for the actual inventory task and owner release; normal unsaved-change choices follow.";
        RefreshChrome();
        return true;
    }

    private void CancelNativeMobyAdd()
    {
        if (!_aggregateMode || AggregateCommandsBlocked || _pendingNativeMobyAdd == null) return;
        _aggregateSession!.Select(_pendingNativeMobyAdd.PriorSelection);
        _pendingNativeMobyAdd = null;
        WriteFields();
        _status.Text = "Canceled the pending native Moby. No placement, editor ID or history entry was added; the prior selected form is restored.";
    }

    private void ApplyPendingNativeMoby()
    {
        RequireCurrentProject();
        var pending = _pendingNativeMobyAdd ?? throw new InvalidOperationException("No native Moby Add is pending.");
        RequireFamilyFieldsUnchanged();
        if (FieldSignature() != _appliedFields || MaterialFieldSignature() != _appliedMaterialFields || RouteFieldSignature() != _appliedRouteFields)
            throw new InvalidDataException("Base, material and Wizard fields are locked during native Add. Cancel before editing them; no raw field changes were discarded.");
        _aggregateSession!.AddNativeMoby(pending.Source, NativeMobyNumber("FreshNativeMobyX"), NativeMobyNumber("FreshNativeMobyY"),
            NativeMobyNumber("FreshNativeMobyZ"), NativeMobyNumber("FreshNativeMobyYaw"));
        _pendingNativeMobyAdd = null;
    }
}
