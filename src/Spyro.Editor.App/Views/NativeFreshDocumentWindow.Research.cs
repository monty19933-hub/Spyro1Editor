using System.Globalization;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.App.Views;

// App-only async seam. A fake implementation exercises UI state, never issues
// a Core snapshot/compilation/plan/context or bypasses the production issuer.
internal interface INativeFreshDocumentResearchUiWork
{
    Task PrepareAsync(NativeId65FreshDocumentSnapshot captured, string image, string cue, CancellationToken token);
    Task<NativeId65FreshDiscResearchPublication> PublishAsync(string outputDirectory, CancellationToken token);
}

internal sealed partial class NativeFreshDocumentWindow
{
    private readonly Button _research = Button("FreshDocumentResearch", "Create research candidate…", null);
    private readonly Button _cancelResearch = Button("FreshDocumentCancelResearch", "Cancel operation", null);
    private readonly Func<(string Image, string Cue)>? _researchSourcePaths;
    private readonly Func<IDisposable>? _beginResearch;
    private bool _researchBusy;
    private CancellationTokenSource? _researchCancellation;
    private Task? _researchTask;
    private NativeId65FreshDocumentSnapshot? _researchSnapshot;
    private NativeId65FreshDiscResearchPublication? _researchPublication;
    private INativeFreshDocumentResearchUiWork? _researchWorkForTesting;
    private Func<(string Image, string Cue)>? _researchSourceForTesting;

    internal INativeFreshDocumentResearchUiWork? ResearchWorkForTesting
    {
        get => _researchWorkForTesting;
        set { _researchWorkForTesting = value; RefreshChrome(); }
    }
    internal Func<(string Image, string Cue)>? ResearchSourcePathsForTesting
    {
        get => _researchSourceForTesting;
        set { _researchSourceForTesting = value; RefreshChrome(); }
    }
    internal Func<CancellationToken, Task<string?>>? ResearchOutputPickerForTesting { get; set; }
    internal Task? ResearchTaskForTesting => _researchTask;
    internal bool ResearchBusyForTesting => _researchBusy;
    internal NativeId65FreshDocumentSnapshot? ResearchSnapshotForTesting => _researchSnapshot;
    internal NativeId65FreshDiscResearchPublication? ResearchPublicationForTesting => _researchPublication;

    private Control BuildResearchActions()
    {
        _research.Click += (_, _) =>
        {
            if (!_researchBusy && !_decisionBusy) _researchTask = CreateResearchCandidateAsync();
        };
        _cancelResearch.Click += (_, _) =>
        {
            if (!_researchBusy || _researchCancellation == null || _researchCancellation.IsCancellationRequested) return;
            _researchCancellation.Cancel();
            _status.Text = "Cancellation requested. Any committed files will be retained and final verification will finish.";
            RefreshChrome();
        };
        return new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 8,
            Children = { _research, _cancelResearch }
        };
    }

    private void RefreshResearchChrome()
    {
        bool hasSource = _researchSourceForTesting != null || _researchSourcePaths != null;
        bool hasOwner = _researchWorkForTesting != null || _beginResearch != null;
        _research.IsEnabled = !_aggregateMode && !_researchBusy && !_decisionBusy && hasSource && hasOwner &&
            _snapshot.Exists && _snapshot.RetainedRecoveryDirectory.Length == 0 && CurrentIntent != null && !HasUnsavedChanges;
        _cancelResearch.IsVisible = _researchBusy;
        _cancelResearch.IsEnabled = _researchBusy && _researchCancellation?.IsCancellationRequested == false;
    }

    private async Task CreateResearchCandidateAsync()
    {
        if (_researchBusy || _decisionBusy) return;
        if (_aggregateMode)
        {
            _status.Text = "Aggregate research export is unavailable until an actual saved aggregate revision and final Store publication guard are bound. No legacy or partial aggregate will be exported.";
            return;
        }
        if (!_snapshot.Exists || CurrentIntent == null || HasUnsavedChanges || _snapshot.RetainedRecoveryDirectory.Length != 0)
        {
            _status.Text = "Research candidates require an applied, saved document with resolved save recovery. Apply and Save explicitly, or Reopen; no content was replaced.";
            return;
        }

        // Set the window guard before acquiring the owner or awaiting a picker.
        // OnClosing checks it before the clean-document early return.
        _researchBusy = true;
        _researchCancellation = new CancellationTokenSource();
        CancellationToken token = _researchCancellation.Token;
        IDisposable? owner = null;
        bool publisherStarted = false;
        _researchPublication = null;
        _researchSnapshot = null;
        RefreshChrome();
        try
        {
            RequireCurrentProject();
            if (_beginResearch != null) owner = _beginResearch();
            else if (_researchWorkForTesting == null)
                throw new InvalidDataException("Open Fresh Level from the active editor project before creating a research candidate.");

            NativeId65FreshDocumentSnapshot captured = NativeId65FreshDocumentStore.Capture(_project, token);
            RequireSameSavedVersion(_snapshot, captured);
            if (!Equal(CurrentIntent, captured.Intent) || HasUnappliedFields)
                throw new InvalidDataException("The applied intent differs from the actual saved document. Reopen before exporting.");
            _researchSnapshot = captured; // Keep the exact existing Snapshot object.
            (string image, string cue) = CaptureResearchSourcePaths();
            string? chosen = ResearchOutputPickerForTesting != null
                ? await ResearchOutputPickerForTesting(token)
                : await PickResearchOutputDirectoryAsync(token);
            token.ThrowIfCancellationRequested();
            if (chosen == null)
            {
                _status.Text = "Research output selection canceled. The saved document and draft are unchanged.";
                return;
            }
            string outputDirectory = Path.GetFullPath(chosen);
            if (Directory.Exists(outputDirectory) || File.Exists(outputDirectory) ||
                new FileInfo(outputDirectory).LinkTarget != null)
                throw new InvalidDataException("Research output must be a new absent directory; existing outputs are never overwritten.");

            RequireResearchCurrent(captured, image, cue, token);
            _status.Text = "Compiling the captured saved document and verifying the original BIN/CUE. The form is locked until this operation finishes.";
            INativeFreshDocumentResearchUiWork work = _researchWorkForTesting ?? new NativeFreshDocumentResearchUiWork();
            await work.PrepareAsync(captured, image, cue, token);
            // This App check is supplementary. The actual closed Core context
            // owns the final Store read lease/check across PublishNew after staging.
            RequireResearchCurrent(captured, image, cue, token);
            _status.Text = "Writing and reading back a new research candidate for the captured saved version…";
            publisherStarted = true;
            NativeId65FreshDiscResearchPublication result = await work.PublishAsync(outputDirectory, token);

            // Never observe caller cancellation after known publisher success.
            // Artifact/source readback failure is the Core typed exception below;
            // a later project/file change is only an advisory about current state.
            _researchPublication = result;
            string advisory = ObserveCapturedVersionAfterCommit(captured, image, cue);
            string recovery = result.RetainedRecoveryDirectory.Length == 0 ? "" :
                $" Recovery files were retained: {result.RetainedRecoveryDirectory}.";
            _status.Text = $"Published captured version as a byte-verified research candidate. Gameplay is not certified. CUE: {result.CuePath}. " +
                $"Receipt: {result.ReceiptPath}.{recovery}{advisory}";
        }
        catch (NativeId65FreshDocumentResearchCommittedException error)
        {
            // This internal-constructed Core exception is not a successful
            // Publication and never supplies a WholeImageReadback claim.
            _status.Text = "Research files were committed, but final verification or recovery did not complete. Preserve these files; do not overwrite or retry in this folder. " +
                $"BIN: {error.ImagePath}. CUE: {error.CuePath}. Receipt: {error.ReceiptPath}. Plan: {error.PlanSha256}. " +
                $"{error.Message}" + DescribeRetainedEvidence(error);
        }
        catch (ExportArtifactRollbackException error)
        {
            _status.Text = $"Research publication rollback is incomplete. Preserve recovery files in {error.RetainedRecoveryDirectory}. " +
                "This is not a successful publication; do not delete or overwrite the output folder. " + error.Message;
        }
        catch (OperationCanceledException)
        {
            _status.Text = publisherStarted
                ? "Research operation canceled without a confirmed publication. Inspect any output/recovery before retrying; no files were deleted by this window."
                : "Research operation canceled before publication. The saved document, applied intent and field text are retained.";
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            _status.Text = $"Research operation did not complete cleanly: {error.Message} " +
                "Your document and draft are retained. Reopen if the saved version changed, and inspect any output/recovery before retrying.";
        }
        finally
        {
            try { owner?.Dispose(); }
            catch (Exception error)
            {
                // Owner disposal changes App guards only, never native output.
                // Do not replace a known publisher outcome with a UI refresh error.
                _status.Text += $" Warning: editor controls could not be refreshed after the operation ({error.Message}).";
            }
            finally
            {
                _researchCancellation?.Dispose();
                _researchCancellation = null;
                _researchBusy = false;
                RefreshChrome();
            }
        }
    }

    private (string Image, string Cue) CaptureResearchSourcePaths()
    {
        var provider = _researchSourceForTesting ?? _researchSourcePaths ??
            throw new InvalidDataException("Select the original BIN/CUE in the main editor before creating a research candidate.");
        var (image, cue) = provider();
        ArgumentException.ThrowIfNullOrWhiteSpace(image);
        ArgumentException.ThrowIfNullOrWhiteSpace(cue);
        return (Path.GetFullPath(image), Path.GetFullPath(cue));
    }

    private void RequireResearchCurrent(NativeId65FreshDocumentSnapshot captured, string image, string cue, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        RequireCurrentProject();
        var currentSource = CaptureResearchSourcePaths();
        if (currentSource != (image, cue))
            throw new InvalidDataException("The selected source BIN/CUE changed. Start again from the current project and source.");
        RequireSameSavedVersion(captured, NativeId65FreshDocumentStore.Capture(_project, token));
        if (HasUnsavedChanges || !Equal(CurrentIntent, captured.Intent))
            throw new InvalidDataException("The current document differs from the captured saved version. Apply and Save explicitly before retrying.");
        token.ThrowIfCancellationRequested();
    }

    private static void RequireSameSavedVersion(NativeId65FreshDocumentSnapshot expected, NativeId65FreshDocumentSnapshot current)
    {
        if (!current.Exists || current.Intent == null || current.RootPath != expected.RootPath || current.ProjectId != expected.ProjectId ||
            current.ManifestSha256 != expected.ManifestSha256 || current.ContentSha256 != expected.ContentSha256)
            throw new InvalidDataException("The saved document or complete project manifest changed. Reopen before exporting.");
    }

    private string ObserveCapturedVersionAfterCommit(NativeId65FreshDocumentSnapshot captured, string image, string cue)
    {
        try
        {
            NativeId65FreshDocumentSnapshot current = NativeId65FreshDocumentStore.Capture(_project, CancellationToken.None);
            bool changed = !_isCurrentProject() || CaptureResearchSourcePaths() != (image, cue) || HasUnsavedChanges ||
                current.RootPath != captured.RootPath || current.ProjectId != captured.ProjectId ||
                current.ManifestSha256 != captured.ManifestSha256 || current.ContentSha256 != captured.ContentSha256;
            return changed ? " Warning: the project, saved document or source selection changed; the files contain the captured version, not a later edit." : "";
        }
        catch (Exception error)
        {
            // This bounded read-only observation must not erase a known committed
            // result. Even an unexpected observation error is advisory here.
            return $" Warning: the current saved/project state could not be rechecked ({error.Message}); the published files retain the captured version.";
        }
    }

    private async Task<string?> PickResearchOutputDirectoryAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (StorageProvider == null) throw new InvalidOperationException("The output folder picker is unavailable.");
        IReadOnlyList<IStorageFolder> folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose a parent folder for a new fresh research candidate", AllowMultiple = false
        });
        token.ThrowIfCancellationRequested();
        if (folders.Count == 0) return null;
        string parent = folders[0].Path.LocalPath;
        if (string.IsNullOrWhiteSpace(parent) || !Directory.Exists(parent))
            throw new InvalidDataException("An existing local output parent directory is required.");
        string name = "fresh-native-research-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N")[..8];
        return Path.Combine(parent, name); // Does not create or reserve it; Core checks ownership.
    }

    private static string DescribeRetainedEvidence(NativeId65FreshDocumentResearchCommittedException error)
    {
        string[] paths = [error.RetainedRecoveryDirectory, error.RetainedStageDirectory,
            error.RetainedOutputLeasePath, error.RetainedProjectLeasePath];
        string[] retained = paths.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.Ordinal).ToArray();
        return retained.Length == 0 ? "" : " Retained evidence: " + string.Join("; ", retained) + ".";
    }

    private sealed class NativeFreshDocumentResearchUiWork : INativeFreshDocumentResearchUiWork
    {
        private NativeId65FreshSavedDocumentExportContext? _context;
        private bool _preparationStarted;
        public async Task PrepareAsync(NativeId65FreshDocumentSnapshot captured, string image, string cue, CancellationToken token)
        {
            if (_preparationStarted) throw new InvalidOperationException("A research worker cannot be rebound to another document.");
            _preparationStarted = true;
            _context = await Task.Run(async () =>
            {
                var compilation = await NativeId65FreshDocumentCompilation.CaptureAsync(image,
                    captured.Intent ?? throw new InvalidDataException("The actual saved document is absent."), token);
                var plan = await NativeId65FreshDocumentResearchPlan.CaptureAsync(image, cue, compilation, token);
                return NativeId65FreshSavedDocumentExportContext.Capture(captured, plan, token);
            }, token);
        }

        public Task<NativeId65FreshDiscResearchPublication> PublishAsync(string outputDirectory, CancellationToken token)
        {
            NativeId65FreshSavedDocumentExportContext context = _context ?? throw new InvalidOperationException("The actual saved context is not prepared.");
            // No Task.Run scheduling token can turn a completed Core commit into
            // a canceled outer task. Core alone owns pre/postcommit cancellation.
            return Task.Run(() => NativeId65FreshDocumentResearchPublisher.PublishAsync(context, outputDirectory, token));
        }
    }
}
