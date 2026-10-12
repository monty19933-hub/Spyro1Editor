using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Spyro.Editor.Core.Diagnostics;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.App.Views;

/// <summary>Game Settings › Memory: the console memory the games Create BIN makes may use (<see cref="GameMemoryMode"/>),
/// saved with the project (<see cref="GameMemorySettings"/>) as soon as it is chosen.</summary>
public sealed partial class MainWindow
{
    private readonly RadioButton _memoryPlayStation = new() { Name = "MemoryModePlayStation", GroupName = "MemoryMode", Content = "PlayStation (2 MB)" };
    private readonly RadioButton _memoryEmulator = new() { Name = "MemoryModeEmulator8MB", GroupName = "MemoryMode", Content = "Emulator only (8 MB RAM)" };
    private readonly TextBlock _memorySummary = new() { Name = "MemoryModeSummary", TextWrapping = TextWrapping.Wrap, FontSize = 12 };
    private bool _syncingMemoryMode;

    /// <summary>The Game Settings card (a disclosure like Fly Anywhere and Play as Crash).</summary>
    private Control BuildMemoryModeSection()
    {
        StackPanel content = new() { Name = "MemoryModePanel", Spacing = 6, Margin = new Avalonia.Thickness(2, 4, 2, 2) };
        TextBlock Note(string text) => new()
        {
            Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 12, LineHeight = 17, Foreground = new SolidColorBrush(ModernMutedInk),
            Margin = new Avalonia.Thickness(26, 0, 0, 4)
        };
        ToolTip.SetTip(_memoryPlayStation, "The game's own memory layout: the games you export work on a PlayStation and in every emulator.");
        ToolTip.SetTip(_memoryEmulator, "Each level's objects and layout load into DuckStation's extra 6 MB, so the memory below the game's draw buffers can hold more.");
        content.Children.Add(_memoryPlayStation);
        content.Children.Add(Note("Works everywhere: on a PlayStation and in every emulator. Each level must fit the console's 2 MB."));
        content.Children.Add(_memoryEmulator);
        content.Children.Add(Note("Emulator only (8 MB RAM): levels can hold more. Players need DuckStation with Settings › Console › " +
            "Enable 8MB RAM. On a normal PlayStation setup levels that fit 2 MB still work. Create BIN says when a level needs 8 MB."));
        content.Children.Add(_memorySummary);
        foreach (var (button, mode) in new[] { (_memoryPlayStation, GameMemoryMode.PlayStation2MB), (_memoryEmulator, GameMemoryMode.Emulator8MB) })
            button.PropertyChanged += (_, change) =>
            {
                if (_syncingMemoryMode || change.Property != ToggleButton.IsCheckedProperty || button.IsChecked != true) return;
                try { SaveMemoryMode(mode); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException or InvalidDataException)
                {
                    EditorDiagnostics.RecordException("saving the Memory choice", error);
                    _statusText.Text = "Could not save the Memory choice: " + error.Message;
                }
                RefreshMemoryModeSettings();
            };
        RefreshMemoryModeSettings();
        var section = BuildModernDisclosure("Memory", content, false);
        section.Name = "MemoryModeSection";
        return section;
    }

    private void SaveMemoryMode(GameMemoryMode mode)
    {
        if (_createBinBusy || _workspaceTransitionBusy) throw new InvalidOperationException("Wait for the current task to finish.");
        GameMemorySettings.Save(_workspace.RootPath, mode);
        GameRam.SetProjectMode(mode);
        EditorDiagnostics.RecordAction("Memory choice", mode.ToString());
        _statusText.Text = mode == GameMemoryMode.Emulator8MB
            ? "Memory: Emulator only (8 MB RAM). The next Create BIN makes a game whose levels can hold more; it needs DuckStation with Enable 8MB RAM for levels that do not fit 2 MB."
            : "Memory: PlayStation (2 MB). The next Create BIN makes a game for a normal PlayStation setup.";
    }

    /// <summary>Shows the project's saved choice and hands it to the editor's capacity checks (on project and level
    /// changes, and after each choice).</summary>
    private void RefreshMemoryModeSettings()
    {
        GameMemoryMode mode = GameMemoryMode.PlayStation2MB;
        string error = "";
        try { mode = GameMemorySettings.Load(_workspace.RootPath); }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException)
        { error = e.Message; }
        GameRam.SetProjectMode(mode);
        _syncingMemoryMode = true;
        try
        {
            _memoryPlayStation.IsChecked = error.Length == 0 && mode == GameMemoryMode.PlayStation2MB;
            _memoryEmulator.IsChecked = error.Length == 0 && mode == GameMemoryMode.Emulator8MB;
            _memoryPlayStation.IsEnabled = _memoryEmulator.IsEnabled = !_createBinBusy;
        }
        finally { _syncingMemoryMode = false; }
        _memorySummary.Text = error.Length > 0 ? "The saved choice could not be read, so Create BIN stops until you choose again: " + error
            : mode == GameMemoryMode.Emulator8MB ? "On: games you create use 8 MB RAM when the emulator has it."
            : "The games you create fit a PlayStation's 2 MB.";
    }

    /// <summary>Create BIN's memory scope: budgets and the last executable step use the saved project choice.</summary>
    private GameRam.Scope UseProjectMemoryMode()
    {
        GameMemoryMode mode;
        try { mode = GameMemorySettings.Load(_workspace.RootPath); }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException)
        { throw new InvalidDataException("The Memory setting in Game Settings could not be read. Choose PlayStation (2 MB) or Emulator only (8 MB RAM) again. (" + e.Message + ")", e); }
        GameRam.SetProjectMode(mode);
        return GameRam.Use(mode);
    }

    /// <summary>What Create BIN says about memory once the game is written (nothing in the 2 MB mode).</summary>
    private static string MemoryModeExportNote(GameRam.Scope? scope) => NativeExtendedRam.ExportNote(scope);

    /// <summary>The retail export's last executable step: with 8 MB RAM the scene placement goes into the finished
    /// working image (a new step image in the build folder), every level is checked against that layout, and the
    /// export's scope learns which levels need 8 MB. Nothing happens in the 2 MB mode or when nothing was written.</summary>
    private static async Task<(string Image, string Cue)> ApplyMemoryModeStageAsync(string sourceImage, string currentImage,
        string currentCue, string tempDir, int step, Spyro.Editor.Core.Levels.LevelCatalog catalog)
    {
        if (!GameRam.Extended || !IsDisposableCombinedBuildImage(currentImage, tempDir) || !File.Exists(currentImage))
            return (currentImage, currentCue);
        string image = Path.Combine(tempDir, $"{step:00}-memory-8mb.bin");
        await Task.Run(() =>
        {
            var disc = UniversalDiscImage.Read(currentImage);
            byte[] exe = disc.Exe.ToArray();
            NativeExtendedRam.ApplyForExport(exe, disc.EntryBytes, catalog.Levels, UniversalDiscImage.Read(sourceImage));
            disc.WriteImage(image, disc.Wad, exe);
            if (!UniversalDiscImage.Read(image).Exe.AsSpan().SequenceEqual(exe))
                throw new InvalidDataException("The 8 MB memory step's executable readback changed.");
        });
        string cue = Path.ChangeExtension(image, ".cue");
        await File.WriteAllTextAsync(cue, BuildCueText(currentCue, currentImage, Path.GetFileName(image)), DiscCueSheet.TextEncoding);
        File.Delete(currentImage);
        return (image, cue);
    }
}
