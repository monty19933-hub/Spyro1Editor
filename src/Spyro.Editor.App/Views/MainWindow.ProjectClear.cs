using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Spyro.Editor.Core.Diagnostics;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Persistence;

namespace Spyro.Editor.App.Views;

/// <summary>File ▾ › Clear Entire Project to Base Game…: one command that puts the project back to the original game
/// (<see cref="ProjectClear"/>), behind a question that must be answered "Yes, I am sure". The work is moved to a
/// dated folder, not deleted; the chosen game stays chosen. The editor then opens the project afresh in a new window,
/// exactly as at a start, so nothing of the old project lingers in memory.</summary>
public sealed partial class MainWindow
{
    internal const string ClearProjectCommand = "Clear Entire Project to Base Game…";
    internal const string ClearProjectYes = "Yes, I am sure";

    /// <summary>Said by the window that opens after a clear, once its first level is shown.</summary>
    private static string? _statusAfterReopen;
    private bool _clearingProject;

    internal Action<string>? ClearProjectFaultForTesting { get; set; }
    /// <summary>The window that replaced this one after a clear.</summary>
    internal MainWindow? ReopenedForTesting { get; private set; }
    internal Task ClearEntireProjectForTestingAsync() => ClearEntireProjectAsync();

    private async Task ClearEntireProjectAsync()
    {
        if (!_releaseMode)
        {
            _statusText.Text = "Clearing a project is for the packaged editor. In a development workspace, remove the saved edit files by hand.";
            return;
        }
        if (_clearingProject) return;
        if (ToolbarSaveBlocked || _saveCommandRunning || _startBlankBusy)
        {
            _statusText.Text = "Wait for the current editor operation to finish, then clear the project.";
            return;
        }
        string root = _workspace.RootPath;
        if (!await ConfirmClearProjectAsync(root))
        {
            _statusText.Text = "Nothing was cleared: your project is as it was.";
            return;
        }
        if (!PathsEqual(root, _workspace.RootPath)) return;

        bool restartValidation = _id65BlankLabCatalogValidationBusy;
        if (restartValidation) CancelId65BlankLabCatalogValidation();
        if (!TryBeginWorkspaceTransition("clearing the project", out WorkspaceTransitionOperation? operation) || operation == null)
        {
            if (restartValidation) BeginId65BlankLabCatalogValidationInBackground();
            return;
        }
        _clearingProject = true;
        bool cleared = false;
        try
        {
            _statusText.Text = "Clearing the project…";
            Action<string>? fault = ClearProjectFaultForTesting;
            ProjectClear.Result result = await Task.Run(() => ProjectClear.Clear(root, DateTimeOffset.Now, fault));
            cleared = true;
            EditorDiagnostics.RecordAction("Project cleared to the base game", result.KeptPath == null ? $"{root}; nothing saved was in it" : $"{root}; work moved to {result.KeptPath}");
            _statusAfterReopen = result.KeptPath == null
                ? "Your project is the original Spyro game: nothing saved was in it."
                : $"Your project is back to the original Spyro game. Everything you had made was moved, not deleted: it is in {result.KeptPath}";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            EditorDiagnostics.RecordException("clearing the project to the base game", e);
            _statusText.Text = "Could not clear the project: " + e.Message;
        }
        finally
        {
            _clearingProject = false;
            CompleteWorkspaceTransition(operation);
            if (!cleared && restartValidation) BeginId65BlankLabCatalogValidationInBackground();
        }
        if (cleared) Reopen();
    }

    /// <summary>Opens the project in a new window, as at a start, and closes this one without asking about unsaved
    /// changes (they belong to the project that was cleared).</summary>
    private void Reopen()
    {
        MainWindow fresh;
        try
        {
            fresh = new MainWindow { WindowStartupLocation = WindowStartupLocation.Manual, Position = Position };
            if (WindowState is WindowState.Maximized or WindowState.FullScreen) fresh.WindowState = WindowState;
            else if (Bounds.Width >= MinWidth && Bounds.Height >= MinHeight) { fresh.Width = Bounds.Width; fresh.Height = Bounds.Height; }
        }
        catch (Exception e)
        {
            EditorDiagnostics.RecordException("opening the cleared project", e);
            _statusText.Text = $"Your project was cleared, but the editor could not show it again ({e.Message}). Close the editor and open it again.";
            return;
        }
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) desktop.MainWindow = fresh;
        ReopenedForTesting = fresh;
        fresh.Show();
        _allowCloseWithUnsavedTerrain = true;
        Close();
    }

    private void SayStatusAfterReopen()
    {
        if (_statusAfterReopen is not { } note) return;
        _statusAfterReopen = null;
        _statusText.Text = note;
    }

    /// <summary>What is in the project, in the words of the question: levels built, the new world, the game's levels
    /// with saved changes, game settings.</summary>
    private List<string> WhatTheProjectHolds()
    {
        string root = _workspace.RootPath;
        List<string> holds = [];
        static string Names(IReadOnlyList<string> names) => string.Join(", ", names.Take(6)) + (names.Count > 6 ? $" and {names.Count - 6} more" : "");
        void Try(Action read) { try { read(); } catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidOperationException) { } }
        Try(() =>
        {
            string[] built = [.. SceneDocumentStore.ReadAll(root, _retailCatalog).Select(s => s.Name)];
            if (built.Length > 0) holds.Add($"{Count(built.Length, "level you built", "levels you built")} ({Names(built)})");
        });
        Try(() => { if (ProjectHasNewWorldLevels() && NewWorldDocument.Read(root) is { } world) holds.Add($"your new world {world.Name}"); });
        Try(() =>
        {
            string[] changed = [.. FindEditedLevelExportTargets().Select(t => t.Level.DisplayName)];
            if (changed.Length > 0) holds.Add($"your changes to {changed.Length} of the game's levels ({Names(changed)})");
        });
        List<string> settings = [];
        Try(() => { if (SpyroMovesSettings.LoadHover(root)) settings.Add("Hover"); });
        Try(() => { var book = SpyroColourSettings.LoadAll(root); if (book.Whole.Count > 0 || book.Levels.Count > 0) settings.Add("Spyro's colours"); });
        Try(() => { if (CreatureColourSettings.Load(root).Count > 0) settings.Add("the creatures' colours"); });
        Try(() => { if (EggThiefColourSettings.Load(root) != null) settings.Add("the egg thieves' colour"); });
        Try(() => { if (NativeFreeFlightProjectExportIntent.Capture(root).RequiresRebuild) settings.Add("Fly Anywhere"); });
        Try(() => { if (PortalRouteStore.Capture(root).Document.HasEdits) settings.Add("doorways sent to other levels"); });
        Try(() => { if (CrashPlayerSettings.Load(root).Count > 0) settings.Add("Play as Crash"); });
        if (settings.Count > 0) holds.Add("game settings (" + string.Join(", ", settings) + ")");
        return holds;
    }

    private async Task<bool> ConfirmClearProjectAsync(string root)
    {
        const string heading = "Clear the entire project back to the base Spyro game?";
        List<string> holds = WhatTheProjectHolds();
        bool unsaved = HasUnsavedTerrainEdits() || HasUnsavedMobyEdits();
        string what =
            "Everything you have made is taken out of your project: your changes to the game's levels, the levels and the world you built, " +
            "and your game settings. The editor then shows the original game again, as on the day you first opened it." +
            (unsaved ? " Changes you have not saved yet are lost." : "");
        string now = holds.Count == 0
            ? "Nothing saved was found in your project, so there is little to clear."
            : "In your project now: " + string.Join("; ", holds) + ".";
        string kept =
            $"Your work is moved, not deleted. It goes into a dated folder in \"{ProjectClear.KeptFolderName}\" ({ProjectClear.KeptRootFor(root)}), in case you want it back. " +
            "Games you have already created stay where they are, and you do not have to choose your game again.";

        var content = new StackPanel { Margin = new Thickness(22), Spacing = 12 };
        content.Children.Add(new TextBlock { Text = heading, FontSize = 20, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(ModernInk) });
        content.Children.Add(new TextBlock { Name = "ClearProjectExplanation", Text = what, TextWrapping = TextWrapping.Wrap, LineHeight = 20, Foreground = new SolidColorBrush(ModernInk) });
        content.Children.Add(new TextBlock { Name = "ClearProjectHolds", Text = now, TextWrapping = TextWrapping.Wrap, LineHeight = 20, FontWeight = FontWeight.SemiBold, Foreground = new SolidColorBrush(ModernInk) });
        content.Children.Add(new TextBlock { Name = "ClearProjectKept", Text = kept, TextWrapping = TextWrapping.Wrap, LineHeight = 20, Foreground = new SolidColorBrush(ModernMutedInk) });
        var dialog = new Window
        {
            Name = "ClearProjectDialog", Title = "Clear Entire Project to Base Game", Width = 560, SizeToContent = SizeToContent.Height, CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = Brushes.White, Content = content,
        };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Right };
        Button cancel = NewButton("Cancel"), confirm = NewButton(ClearProjectYes);
        cancel.Name = "ClearProjectCancel"; confirm.Name = "ClearProjectConfirm";
        // Return and Escape both mean Cancel: only a click on "Yes, I am sure" clears.
        cancel.IsDefault = true; cancel.IsCancel = true;
        cancel.Click += (_, _) => dialog.Close(false); confirm.Click += (_, _) => dialog.Close(true);
        StyleModernPrimaryButton(confirm, ModernRed, 132);
        actions.Children.Add(cancel); actions.Children.Add(confirm); content.Children.Add(actions);
        dialog.Opened += (_, _) => cancel.Focus();
        return await dialog.ShowDialog<bool>(this);
    }
}
