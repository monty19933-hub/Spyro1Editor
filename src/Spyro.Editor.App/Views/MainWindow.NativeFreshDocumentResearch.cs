using Avalonia.Threading;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

namespace Spyro.Editor.App.Views;

public sealed partial class MainWindow
{
    private bool _freshNativeResearchBusy;
    private int _freshNativeResearchGeneration;

    private IDisposable BeginFreshDocumentResearch(EditorProjectLayout project)
    {
        Dispatcher.UIThread.VerifyAccess();
        if (!IsNativeFreshDocumentProjectCurrent(project, ReleaseProjectBootstrap.Current?.Project, _workspace.RootPath))
            throw new InvalidDataException("The active project root or project ID changed before the fresh document source operation.");
        if (_freshNativeResearchBusy || _id65BlankLabBusy || _id65BlankLabManualOperation != null ||
            HasConflictingId65BlankLabManualOperationState())
            throw new InvalidDataException("Finish the active editor save, source, load, Lab or export operation before fresh document source work.");
        int generation = checked(_freshNativeResearchGeneration + 1);
        _freshNativeResearchGeneration = generation;
        _freshNativeResearchBusy = true;
        try { RefreshFreshDocumentResearchOwnerUi(); }
        catch
        {
            _freshNativeResearchBusy = false;
            throw;
        }
        return new FreshDocumentResearchOwner(this, generation);
    }

    private bool TryBlockFreshDocumentResearchMutation(string action)
    {
        if (!_freshNativeResearchBusy) return false;
        _statusText.Text = $"Wait for fresh document source work to finish before {action}. The active catalogue, inspection or research operation still owns the editor.";
        return true;
    }

    private void RefreshFreshDocumentResearchOwnerUi()
    {
        RefreshLevelSelectionAvailability();
        RefreshActionAvailability();
    }

    private (string Image, string Cue) CaptureFreshDocumentResearchSourcePaths()
    {
        Dispatcher.UIThread.VerifyAccess();
        string image = FirstExistingDiscImagePath(_discImagePathBox.Text, _skyboxDiscImagePathBox.Text,
            DiscImageLocator.FindImage(_workspace));
        if (string.IsNullOrWhiteSpace(image))
            throw new InvalidDataException("Select your original source BIN/CUE in the main editor first.");
        string cue = DiscImageLocator.FindCueForImage(image);
        if (string.IsNullOrWhiteSpace(cue) || !File.Exists(cue))
            throw new InvalidDataException("Fresh document source work requires the existing source CUE; no source CUE is generated or configured here.");
        return (Path.GetFullPath(image), Path.GetFullPath(cue));
    }

    private sealed class FreshDocumentResearchOwner(MainWindow window, int generation) : IDisposable
    {
        private MainWindow? _window = window;
        public void Dispose()
        {
            MainWindow? current = _window;
            if (current == null) return;
            Dispatcher.UIThread.VerifyAccess();
            _window = null;
            if (current._freshNativeResearchGeneration != generation) return;
            current._freshNativeResearchBusy = false;
            current.RefreshFreshDocumentResearchOwnerUi();
        }
    }

    internal IDisposable BeginFreshDocumentResearchForTesting() => BeginFreshDocumentResearch(
        ReleaseProjectBootstrap.Current?.Project ?? throw new InvalidDataException("The isolated shell test needs an actual active project."));
    internal Task ChooseSourceDiscForFreshResearchTestingAsync() => ChooseSourceDiscImageAsync();
    internal bool TryWorkspaceTransitionForFreshResearchTesting()
    {
        bool started = TryBeginWorkspaceTransition("fresh research guard regression", out var operation);
        if (started) CompleteWorkspaceTransition(operation!);
        return started;
    }
    internal bool TryRegularSaveForFreshResearchTesting()
    {
        bool started = TryBeginRegularEditorPersistenceOperation("fresh research guard regression", out var operation);
        if (started) CompleteRegularEditorPersistenceOperation(operation!);
        return started;
    }
}
