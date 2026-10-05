using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Diagnostics;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.App.Views;

/// <summary>
/// First-launch guidance and "where was I" memory. Until a game disc has been chosen there is nothing to
/// edit, so the viewport shows one clear next step instead of an empty scene and a wall of disabled tools.
/// </summary>
public sealed partial class MainWindow
{
    private Border? _welcomeOverlay;
    private Control? _modernSidebarHost;

    private Control BuildWelcomeOverlay()
    {
        Button open = NewAsyncButton("Open Game…", ChooseSourceDiscImageAsync);
        open.Name = "WelcomeOpenGame";
        StyleModernPrimaryButton(open, ModernBlue, 170);
        open.HorizontalAlignment = HorizontalAlignment.Left;
        StackPanel steps = new() { Spacing = 10 };
        steps.Children.Add(new TextBlock
        {
            Text = "Welcome to Spyro Editor",
            FontSize = 24,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(ModernInk)
        });
        steps.Children.Add(new TextBlock
        {
            Text = "Edit the levels of Spyro the Dragon and play your changes.",
            FontSize = 14,
            Foreground = new SolidColorBrush(ModernMutedInk),
            TextWrapping = TextWrapping.Wrap
        });
        foreach (string step in new[]
                 {
                     "1.  Open your own Spyro the Dragon (USA) disc image — a .bin or .cue file.",
                     "2.  Pick a level, then move, add or remove objects and terrain.",
                     "3.  Save, then choose Create BIN to make a playable copy for your emulator."
                 })
        {
            steps.Children.Add(new TextBlock
            {
                Text = step,
                FontSize = 14,
                Foreground = new SolidColorBrush(ModernInk),
                TextWrapping = TextWrapping.Wrap
            });
        }
        steps.Children.Add(open);
        steps.Children.Add(new TextBlock
        {
            Text = "Your disc image is only read, never changed. The editor does not include the game.",
            FontSize = 12,
            Foreground = new SolidColorBrush(ModernMutedInk),
            TextWrapping = TextWrapping.Wrap
        });
        _welcomeOverlay = new Border
        {
            Name = "WelcomeOverlay",
            IsVisible = false,
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(ModernLine),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(28, 24),
            MaxWidth = 560,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = steps
        };
        return _welcomeOverlay;
    }

    /// <summary>Shows the welcome card while no disc is chosen and nothing is loaded; the tools wait with it.</summary>
    private void RefreshWelcomeOverlay()
    {
        if (_welcomeOverlay == null)
            return;
        bool nothingLoaded = _freshSceneDocument == null && _currentGeometry == null && _currentMobys.Count == 0;
        bool discChosen = File.Exists(_discImagePathBox.Text) || File.Exists(DiscImageLocator.FindImage(_workspace));
        bool show = _releaseMode && nothingLoaded && !discChosen && !_loadingLevel && !_buildingPortableCache;
        if (show)
        {
            // The card says everything; an empty-scene message behind it would only repeat it.
            _welcomeOverlay.IsVisible = true;
            if (!_viewport.HideEmptyMessage) { _viewport.HideEmptyMessage = true; _viewport.InvalidateVisual(); }
            if (_visibleStatusText.Text != WelcomeStatus) _visibleStatusText.Text = WelcomeStatus;
        }
        else if (_welcomeOverlay.IsVisible)
        {
            _welcomeOverlay.IsVisible = false;
            _viewport.HideEmptyMessage = false;
            _viewport.InvalidateVisual();
            _visibleStatusText.Text = SummarizeEditorStatus(_statusText.Text ?? "");
        }
        if (_modernSidebarHost != null)
        {
            _modernSidebarHost.IsEnabled = !show;
            _modernSidebarHost.Opacity = show ? 0.45 : 1;
        }
    }

    private const string WelcomeStatus = "Choose Open Game… and select your Spyro the Dragon (USA) disc image to begin.";

    /// <summary>What the status bar shows: the welcome hint on the first-run screen, else the summarised status.</summary>
    private string VisibleEditorStatus(string message) =>
        _welcomeOverlay?.IsVisible == true ? WelcomeStatus : SummarizeEditorStatus(message);

    private string LastLevelPath => Path.Combine(_workspace.RootPath, "_local", "settings", "last-level.json");

    /// <summary>The level to open at startup: the one last worked on in this project, else the first level.</summary>
    private LevelDefinition? ResolveStartupLevel()
    {
        try
        {
            if (File.Exists(LastLevelPath))
            {
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(LastLevelPath));
                if (document.RootElement.TryGetProperty("levelKey", out JsonElement key) &&
                    key.ValueKind == JsonValueKind.String &&
                    _catalog.FindByKey(key.GetString() ?? "") is { } remembered)
                {
                    return remembered;
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            EditorDiagnostics.RecordWarning("Could not read the last opened level", exception.Message);
        }
        return _catalog.FindByKey("artisans") ?? _catalog.Levels.FirstOrDefault();
    }

    private void RememberLastLevel(LevelDefinition level)
    {
        if (!_releaseMode)
            return;
        try
        {
            string path = LastLevelPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(new { levelKey = level.Key }));
            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            EditorDiagnostics.RecordWarning("Could not remember the last opened level", exception.Message);
        }
    }
}
