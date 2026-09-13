using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.App.Views;

// Failure/delay tests only; production always retains an actually issued Candidate.
internal interface INativeFreshSavedLevelResearchUiWork
{
    Task PrepareAsync(NativeFreshLevelDocumentSnapshot snapshot, string image, string cue, CancellationToken token);
    Task<NativeId65FreshDiscResearchPublication> PublishAsync(string outputDirectory, CancellationToken token);
}

internal sealed partial class NativeFreshDocumentWindow
{
    private INativeFreshSavedLevelResearchUiWork? _savedResearchWorkForTesting;
    private NativeFreshLevelDocumentSnapshot? _savedResearchSnapshot;
    private Exception? _savedResearchFailure;
    internal INativeFreshSavedLevelResearchUiWork? SavedResearchWorkForTesting
    {
        get => _savedResearchWorkForTesting;
        set
        {
            if (_researchBusy) throw new InvalidOperationException("An active research worker cannot be replaced.");
            _savedResearchWorkForTesting = value;
            RefreshChrome();
        }
    }
    internal NativeFreshLevelDocumentSnapshot? SavedResearchSnapshotForTesting => _savedResearchSnapshot;
    internal Exception? SavedResearchFailureForTesting => _savedResearchFailure;

    private sealed record SavedResearchTicket(NativeFreshLevelDocumentSession Session,
        NativeFreshLevelDocumentSnapshot Snapshot, NativeFreshLevelDocumentIntent Applied,
        string? Selection, string?[] BaseFields, object? Music, string?[] RouteFields,
        MaterialFields Materials, long RawGeneration, string Image, string Cue);

    private bool IsSavedResearchReady => _aggregateMode && !AggregateCommandsBlocked &&
        !_addingWizard && !_forkRecoveryPending && !_materialInspectionCleanupBlocked &&
        !_materialInspectionClosed && _beginResearch != null &&
        (_researchSourceForTesting ?? _researchSourcePaths) != null &&
        _aggregateSession?.Snapshot is { Exists: true, RetainedRecoveryDirectory.Length: 0 } &&
        (CurrentAggregateIntent is { SchemaVersion: 2, Placements.Count: 0,
            FloorMaterials.Triangle0: NativeFreshRetailTerrainMaterialIntent,
            FloorMaterials.Triangle1: NativeFreshRetailTerrainMaterialIntent } ||
            NativeFreshSavedLevelResearchEligibility.IsSupportedMixedProfile(CurrentAggregateIntent)) && !HasUnsavedChanges;

    private async Task CreateSavedResearchCandidateAsync()
    {
        if (!IsSavedResearchReady)
        {
            _status.Text = "Aggregate research requires an explicitly applied and saved schema-2 document with two retail materials and either no placements or the supported one-Wizard mixed profile. Resolve pending fields and recovery first. No legacy or partial document was exported.";
            return;
        }
        _researchBusy = true;
        _researchCancellation = new CancellationTokenSource();
        CancellationToken token = _researchCancellation.Token;
        IDisposable? owner = null;
        bool publisherStarted = false;
        _researchPublication = null;
        _researchSnapshot = null;
        _savedResearchSnapshot = null;
        _savedResearchFailure = null;
        List<Exception> failures = [];
        SavedResearchTicket? ticket = null;
        try
        {
            RefreshChrome();
            RequireCurrentProject();
            owner = _beginResearch!() ?? throw new InvalidDataException("Saved-level research requires an actual editor owner.");
            var source = CaptureResearchSourcePaths();
            var session = _aggregateSession!;
            ticket = new SavedResearchTicket(session, session.Snapshot, session.CurrentIntent!, session.SelectedEditorId,
                _fields.Values.Select(f => f.Text).ToArray(), _music.SelectedItem,
                _routeFields.Values.Select(f => f.Text).ToArray(), MaterialFieldSignature(),
                _materialInspectionGeneration, source.Image, source.Cue);
            RequireSavedResearchCurrent(ticket, token);
            _savedResearchSnapshot = ticket.Snapshot;
            string? chosen = ResearchOutputPickerForTesting != null
                ? await ResearchOutputPickerForTesting(token) : await PickResearchOutputDirectoryAsync(token);
            RequireSavedResearchCurrent(ticket, token);
            if (chosen == null)
            {
                _status.Text = "Research output selection canceled. The saved aggregate and raw fields are unchanged.";
                return;
            }
            string output = Path.GetFullPath(chosen);
            if (Directory.Exists(output) || File.Exists(output) || new FileInfo(output).LinkTarget != null)
                throw new InvalidDataException("Research output must be a new absent directory; existing outputs are never overwritten.");
            _status.Text = "Preparing the captured saved aggregate and checking the original BIN/CUE. No fields are applied or saved.";
            var work = _savedResearchWorkForTesting ?? new ActualSavedResearchUiWork();
            await work.PrepareAsync(ticket.Snapshot, ticket.Image, ticket.Cue, token);
            RequireSavedResearchCurrent(ticket, token);
            _status.Text = "Publishing and reading back a new research candidate for the captured saved aggregate…";
            publisherStarted = true;
            var result = await work.PublishAsync(output, token);
            _researchPublication = result; // A real return is recorded BEFORE all advisory/cleanup work.
        }
        catch (Exception error)
        {
            failures.Add(error);
            _savedResearchFailure = error;
        }
        finally
        {
            Attempt(() => owner?.Dispose());
            Attempt(() => _researchCancellation?.Dispose());
            _researchCancellation = null;
            _researchBusy = false;
            Attempt(RefreshChrome);
            if (failures.Count != 0)
            {
                _savedResearchFailure = failures.Count == 1 ? failures[0] :
                    new IOException("Saved-level research and UI owner accounting failures.", new AggregateException(failures));
            }
            // Derive the final status from the retained actual return FIRST, even
            // when a later observation, formatting or owner refresh failed.
            if (_researchPublication is { } published)
            {
                string advisory = ticket == null ? " Warning: captured UI ticket is unavailable." : ObserveSavedResearchAfterReturn(ticket);
                _status.Text = $"Published captured aggregate as a byte-verified research candidate. This is not general gameplay certification. CUE: {published.CuePath}. Receipt: {published.ReceiptPath}." +
                    (published.RetainedRecoveryDirectory.Length == 0 ? "" : $" Retained recovery: {published.RetainedRecoveryDirectory}.") + advisory +
                    (failures.Count == 0 ? "" : " Warning: later UI work reported " + string.Join(" | ", failures.SelectMany(SavedResearchLeafMessages)));
            }
            else if (_savedResearchFailure != null)
                _status.Text = DescribeSavedResearchFailure(_savedResearchFailure, publisherStarted);
        }
        void Attempt(Action action) { try { action(); } catch (Exception error) { failures.Add(error); } }
    }

    private void RequireSavedResearchCurrent(SavedResearchTicket ticket, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        RequireCurrentProject();
        if (!_aggregateMode || _documentMode.SelectedItem as string != AggregateModeLabel ||
            _materialInspectionClosed || _addingWizard || _forkRecoveryPending || _materialInspectionBusy ||
            !ReferenceEquals(ticket.Session, _aggregateSession) || !ReferenceEquals(ticket.Snapshot, _aggregateSession?.Snapshot) ||
            !ReferenceEquals(ticket.Applied, CurrentAggregateIntent) || ticket.Selection != _aggregateSession?.SelectedEditorId ||
            !ticket.BaseFields.SequenceEqual(_fields.Values.Select(f => f.Text)) || !ReferenceEquals(ticket.Music, _music.SelectedItem) ||
            !ticket.RouteFields.SequenceEqual(_routeFields.Values.Select(f => f.Text)) || ticket.Materials != MaterialFieldSignature() ||
            // Existing generation increments for FieldsChanged and WriteFields,
            // not RefreshChrome: exact raw ABA during an await is still stale.
            ticket.RawGeneration != _materialInspectionGeneration ||
            HasUnsavedChanges || CaptureResearchSourcePaths() != (ticket.Image, ticket.Cue))
            throw new InvalidDataException("The saved research session, applied document, raw fields, selection, source or window mode changed. No replacement was adopted.");
        RequireSameSavedAggregate(ticket.Snapshot, NativeFreshLevelDocumentStore.Capture(_project, token));
        if (ticket.Snapshot.Intent == null || !NativeFreshLevelDocumentCodec.SerializeCanonical(ticket.Applied).AsSpan()
            .SequenceEqual(NativeFreshLevelDocumentCodec.SerializeCanonical(ticket.Snapshot.Intent)))
            throw new InvalidDataException("The applied aggregate differs from the captured saved aggregate. Apply and Save explicitly.");
        token.ThrowIfCancellationRequested();
    }

    private static void RequireSameSavedAggregate(NativeFreshLevelDocumentSnapshot expected, NativeFreshLevelDocumentSnapshot current)
    {
        if (!current.Exists || current.Intent == null || current.RootPath != expected.RootPath || current.ProjectId != expected.ProjectId ||
            current.Path != expected.Path || current.ManifestSha256 != expected.ManifestSha256 || current.ContentSha256 != expected.ContentSha256 ||
            current.RetainedRecoveryDirectory.Length != 0 || expected.RetainedRecoveryDirectory.Length != 0)
            throw new InvalidDataException("The saved aggregate or complete project manifest changed. Reopen explicitly before exporting.");
    }

    private string ObserveSavedResearchAfterReturn(SavedResearchTicket ticket)
    {
        try { RequireSavedResearchCurrent(ticket, CancellationToken.None); return ""; }
        catch (Exception error)
        { return $" Warning: current UI/project/source observation changed or failed ({error.Message}); files contain the captured version."; }
    }

    private static IEnumerable<Exception> SavedResearchErrors(Exception error)
    {
        yield return error;
        if (error is AggregateException group)
        {
            foreach (var child in group.InnerExceptions)
                foreach (var item in SavedResearchErrors(child)) yield return item;
        }
        else if (error.InnerException != null)
            foreach (var item in SavedResearchErrors(error.InnerException)) yield return item;
    }

    private static string DescribeSavedResearchFailure(Exception error, bool publisherStarted)
    {
        var tree = SavedResearchErrors(error).ToArray();
        string detail = string.Join(" | ", SavedResearchLeafMessages(error));
        if (tree.OfType<NativeFreshSavedLevelResearchCommittedException>().FirstOrDefault() is { } committed)
            return "Files were committed, but final verification or owner accounting failed. Preserve files; do not overwrite or retry this folder. " +
                $"BIN: {committed.ImagePath}. CUE: {committed.CuePath}. Receipt: {committed.ReceiptPath}. Plan: {committed.PlanSha256}. " +
                Retained([committed.RetainedRecoveryDirectory, committed.RetainedStageDirectory, committed.RetainedOutputLeasePath, committed.RetainedProjectLeasePath]) + detail;
        if (tree.OfType<NativeId65FreshResearchPublicationUnconfirmedException>().FirstOrDefault() is { } unconfirmed)
            return "Publication was invoked but its outcome is unconfirmed. Preserve evidence; do not infer commit or retry permission from files. " +
                $"BIN: {unconfirmed.ImagePath}. CUE: {unconfirmed.CuePath}. Receipt: {unconfirmed.ReceiptPath}. Plan: {unconfirmed.PlanSha256}. " +
                Retained([unconfirmed.RetainedRecoveryDirectory, unconfirmed.RetainedStageDirectory, unconfirmed.RetainedOutputLeasePath, unconfirmed.RetainedProjectLeasePath, unconfirmed.PublicationLockPath]) + detail;
        if (tree.OfType<ExportArtifactRollbackException>().FirstOrDefault() is { } rollback)
            return $"Publication rollback did not complete. Preserve recovery: {rollback.RetainedRecoveryDirectory}. {detail}";
        if (tree.OfType<OperationCanceledException>().Any())
            return publisherStarted ? "Canceled without a confirmed publication. Inspect output/recovery before retrying; no files were deleted. " + detail :
                "Canceled before publication. Saved aggregate, applied document and raw fields are retained. " + detail;
        return "Research did not complete cleanly. Document and raw fields are retained; inspect any output/recovery before retrying. " + detail;
        static string Retained(string[] paths) => "Retained inspection paths: " +
            string.Join("; ", paths.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.Ordinal)) + ". ";
    }

    private static IEnumerable<string> SavedResearchLeafMessages(Exception error) => error is AggregateException group
        ? group.InnerExceptions.SelectMany(SavedResearchLeafMessages) : error.InnerException != null
            ? SavedResearchLeafMessages(error.InnerException) : [error.Message];

    private sealed class ActualSavedResearchUiWork : INativeFreshSavedLevelResearchUiWork
    {
        private NativeFreshSavedLevelResearchCandidatePlan? _candidate;
        private bool _preparationStarted, _publicationStarted;
        public async Task PrepareAsync(NativeFreshLevelDocumentSnapshot snapshot, string image, string cue, CancellationToken token)
        {
            if (_preparationStarted) throw new InvalidOperationException("A saved research worker cannot be rebound.");
            _preparationStarted = true;
            _candidate = await Task.Run(() => NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync(image, cue, snapshot, token));
            if (!ReferenceEquals(_candidate.SavedSnapshot, snapshot)) throw new InvalidDataException("The actual public issuer did not retain the supplied Snapshot.");
        }
        public Task<NativeId65FreshDiscResearchPublication> PublishAsync(string outputDirectory, CancellationToken token)
        {
            var candidate = _candidate ?? throw new InvalidOperationException("The actual saved Candidate is not prepared.");
            if (_publicationStarted) throw new InvalidOperationException("A saved research worker cannot publish twice.");
            _publicationStarted = true;
            return Task.Run(() => NativeFreshSavedLevelResearchCandidatePublisher.PublishAsync(candidate, outputDirectory, token));
        }
    }
}
