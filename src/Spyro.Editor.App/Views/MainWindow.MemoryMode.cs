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
    private readonly RadioButton _memoryEmulator = new() { Name = "MemoryModeEmulator8MB", GroupName = "MemoryMode", Content = "Emulator (8 MB RAM), the usual choice" };
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
        content.Children.Add(_memoryEmulator);
        content.Children.Add(Note("Emulator (8 MB RAM): levels can hold more, so fewer of your changes run out of room. Players need DuckStation with " +
            "Settings › Console › Enable 8MB RAM. On a normal PlayStation setup levels that fit 2 MB still work. Create BIN says when a level needs 8 MB."));
        content.Children.Add(_memoryPlayStation);
        content.Children.Add(Note("For a real PlayStation or a disc. Works everywhere, but each level must fit the console's 2 MB, " +
            "and Create BIN stops when one does not."));
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
            ? "Memory: Emulator (8 MB RAM). The next Create BIN makes a game whose levels can hold more; it needs DuckStation with Enable 8MB RAM for levels that do not fit 2 MB."
            : "Memory: PlayStation (2 MB). The next Create BIN makes a game for a real PlayStation or a disc; every level must fit 2 MB.";
    }

    /// <summary>Shows the project's saved choice and hands it to the editor's capacity checks (on project and level
    /// changes, and after each choice).</summary>
    private void RefreshMemoryModeSettings()
    {
        GameMemoryMode mode = GameMemorySettings.Default;
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
            : "The games you create fit a PlayStation's 2 MB. Levels hold less than with 8 MB.";
    }

    /// <summary>Create BIN's memory scope: budgets and the last executable step use the saved project choice.</summary>
    private GameRam.Scope UseProjectMemoryMode()
    {
        GameMemoryMode mode;
        try { mode = GameMemorySettings.Load(_workspace.RootPath); }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException)
        { throw new InvalidDataException("The Memory setting in Game Settings could not be read. Choose PlayStation (2 MB) or Emulator (8 MB RAM) again. (" + e.Message + ")", e); }
        GameRam.SetProjectMode(mode);
        return GameRam.Use(mode);
    }

    /// <summary>What Create BIN says about memory once the game is written (nothing in the 2 MB mode).</summary>
    private string MemoryModeExportNote(GameRam.Scope? scope) => NativeExtendedRam.ExportNote(scope) + ExportRoomNote();

    /// <summary>The game's levels the last Create BIN added objects to, each with the memory left below the game's
    /// draw buffers once they were in (where an added object's model and behaviour go, in both memory settings).</summary>
    private readonly List<(string Level, int Headroom)> _exportRoom = [];

    /// <summary>Says how much room the levels with added objects have left, fullest first, so that running out does not
    /// come as a surprise at some later Create BIN.</summary>
    private string ExportRoomNote()
    {
        if (_exportRoom.Count == 0) return "";
        static string Kb(int bytes) => $"{Math.Max(0, bytes) / 1024.0:0} KB";
        var fullest = _exportRoom.OrderBy(r => r.Headroom).ToList();
        string list = string.Join(", ", fullest.Take(5).Select(r => $"{r.Level} {Kb(r.Headroom)}")) + (fullest.Count > 5 ? $" and {fullest.Count - 5} more with more room" : "");
        string tight = fullest[0].Headroom < 24 * 1024
            ? $" {fullest[0].Level} is nearly full: another kind of object with a model of its own may not fit there (copies of kinds already in the level cost very little)."
            : "";
        return $" Room left for more objects after your changes: {list}.{tight}";
    }

    /// <summary>The retail export's last step: with 8 MB RAM the scene placement goes into the finished working image (a
    /// new step image in the build folder), every level is checked against that layout, and the export's scope learns
    /// which levels need 8 MB; Spyro 3's hover and Spyro's colours go in when Game Settings asks for them. Nothing happens
    /// in the 2 MB mode without them. With no level edits the step starts from the original game, which it leaves alone.</summary>
    private static async Task<(string Image, string Cue)> ApplyMemoryModeStageAsync(string sourceImage, string currentImage,
        string currentCue, string tempDir, int step, Spyro.Editor.Core.Levels.LevelCatalog catalog)
    {
        if (!GameRam.Extended && !NativeHover.Enabled && !NativeSpyroColours.Enabled || !File.Exists(currentImage))
            return (currentImage, currentCue);
        bool disposable = IsDisposableCombinedBuildImage(currentImage, tempDir);
        // From the original game itself only for Hover or Spyro's colours: they alone make a game worth creating.
        if (!disposable && !(PathsEqual(currentImage, sourceImage) && (NativeHover.Enabled || NativeSpyroColours.Enabled)))
            return (currentImage, currentCue);
        string image = Path.Combine(tempDir, $"{step:00}-game-settings.bin");
        await Task.Run(async () =>
        {
            var disc = UniversalDiscImage.Read(currentImage);
            byte[] exe = disc.Exe.ToArray();
            NativeHover.ApplyForExport(exe);
            // Hover's wing flap in every level that lacks it (the levels grow, so the WAD is rebuilt).
            Dictionary<int, byte[]> replacements = [];
            await NativeHoverFlap.ApplyForExportAsync(sourceImage, replacements, disc.EntryBytes, exe, catalog.Levels);
            byte[] Entry(int entry) => replacements.TryGetValue(entry, out var bytes) ? bytes : disc.EntryBytes(entry);
            if (GameRam.Extended) NativeExtendedRam.ApplyForExport(exe, Entry, catalog.Levels, UniversalDiscImage.Read(sourceImage));
            byte[] wad = replacements.Count == 0 ? disc.Wad.ToArray() : disc.RebuildWad(replacements);
            NativeSpyroColours.ApplyForExport(wad);
            disc.WriteImage(image, wad, exe);
            var read = UniversalDiscImage.Read(image);
            if (!read.Exe.AsSpan().SequenceEqual(exe) || !read.Wad.AsSpan().SequenceEqual(wad))
                throw new InvalidDataException("The game settings step's readback changed.");
        });
        string cue = Path.ChangeExtension(image, ".cue");
        await File.WriteAllTextAsync(cue, BuildCueText(currentCue, currentImage, Path.GetFileName(image)), DiscCueSheet.TextEncoding);
        if (disposable) File.Delete(currentImage);
        return (image, cue);
    }
}
