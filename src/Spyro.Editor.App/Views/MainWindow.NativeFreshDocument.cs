using Avalonia.Controls;
using Spyro.Editor.Core.Persistence;

namespace Spyro.Editor.App.Views;

public sealed partial class MainWindow
{
    private Button BuildNativeFreshDocumentButton()
    {
        Button button = NewAsyncButton("Fresh Level…", ShowNativeFreshDocumentAsync);
        button.Name = "FreshNativeDocumentButton";
        StyleModernToolbarButton(button, 116);
        return button;
    }

    private async Task ShowNativeFreshDocumentAsync()
    {
        if (TryBlockEditorMutationDuringId65BlankLabManualOperation("opening a separate fresh-native document")) return;
        if (_workspaceTransitionBusy || _projectDataImportBusy || _regularEditorPersistenceBusy || _buildSafetyBusy || _buildingPortableCache)
        {
            _statusText.Text = "Finish the current project operation before opening Fresh Level.";
            return;
        }
        ReleaseProjectContext? captured = ReleaseProjectBootstrap.Current;
        if (captured == null || !IsNativeFreshDocumentProjectCurrent(captured.Project, captured.Project, _workspace.RootPath))
        {
            _statusText.Text = "Fresh Level requires the active protected project and its existing project manifest. No default project was created.";
            return;
        }
        try
        {
            bool Current() => IsNativeFreshDocumentProjectCurrent(captured.Project,
                ReleaseProjectBootstrap.Current?.Project, _workspace.RootPath);
            var dialog = new NativeFreshDocumentWindow(captured.Project, Current);
            await dialog.ShowDialog(this);
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
        {
            _statusText.Text = $"Fresh document could not be opened: {error.Message} Existing Lab and source files are unchanged.";
        }
    }

    internal static bool IsNativeFreshDocumentProjectCurrent(EditorProjectLayout captured, EditorProjectLayout? current, string workspaceRoot) =>
        current != null && string.Equals(captured.ProjectId, current.ProjectId, StringComparison.Ordinal) &&
        PathsEqual(captured.RootPath, current.RootPath) && PathsEqual(captured.RootPath, workspaceRoot);
}
