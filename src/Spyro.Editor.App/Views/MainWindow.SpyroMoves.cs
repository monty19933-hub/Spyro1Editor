using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Spyro.Editor.Core.Diagnostics;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

namespace Spyro.Editor.App.Views;

/// <summary>Game Settings › Hover: Spyro 3's hover in the games Create BIN makes (<see cref="NativeHover"/>), one switch for
/// the whole game, saved with the project (<see cref="SpyroMovesSettings"/>) as soon as it is ticked.</summary>
public sealed partial class MainWindow
{
    private readonly CheckBox _hoverEnabled = new() { Name = "HoverEnabled", Content = "Let Spyro hover (from Spyro 3)" };
    private readonly TextBlock _hoverSummary = new() { Name = "HoverSummary", TextWrapping = TextWrapping.Wrap, FontSize = 12 };
    private bool _syncingHover;

    private Control BuildHoverSection()
    {
        StackPanel content = new() { Name = "HoverPanel", Spacing = 6, Margin = new Avalonia.Thickness(2, 4, 2, 2) };
        ToolTip.SetTip(_hoverEnabled, "While gliding, press ▲ (Triangle): Spyro beats his wings, rises a little and carries on forward, then drops, as in Spyro 2 and 3.");
        content.Children.Add(_hoverEnabled);
        content.Children.Add(new TextBlock
        {
            Text = "While gliding, press ▲: Spyro beats his wings (the flap from the flight levels), rises about as high as a " +
                   "short jump and drifts on a little, then drops: good for a ledge just out of reach. As in Spyro 3, he can't " +
                   "glide again until he lands. Without it, ▲ in a glide drops Spyro straight down, as in the original game. It is " +
                   "in every level of the games you create. Tree Tops, Haunted Towers and Twilight Harbor have no memory to spare " +
                   "for the flap, so there he hovers without it. Where Fly Anywhere is on, Spyro flies instead of gliding, so there is " +
                   "no glide to hover from.",
            TextWrapping = TextWrapping.Wrap, FontSize = 12, LineHeight = 17, Foreground = new SolidColorBrush(ModernMutedInk),
            Margin = new Avalonia.Thickness(26, 0, 0, 4)
        });
        content.Children.Add(_hoverSummary);
        _hoverEnabled.PropertyChanged += (_, change) =>
        {
            if (_syncingHover || change.Property != ToggleButton.IsCheckedProperty) return;
            try { SaveHover(_hoverEnabled.IsChecked == true); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException)
            {
                EditorDiagnostics.RecordException("saving the Hover choice", error);
                _statusText.Text = "Could not save the Hover choice: " + error.Message;
            }
            RefreshHoverSettings();
        };
        RefreshHoverSettings();
        var section = BuildModernDisclosure("Hover", content, false);
        section.Name = "HoverSection";
        return section;
    }

    private void SaveHover(bool on)
    {
        if (_createBinBusy || _workspaceTransitionBusy) throw new InvalidOperationException("Wait for the current task to finish.");
        SpyroMovesSettings.SaveHover(_workspace.RootPath, on);
        EditorDiagnostics.RecordAction("Hover choice", on ? "on" : "off");
        _statusText.Text = on
            ? "Hover is on: in the next game you create, ▲ while gliding makes Spyro beat his wings, rise a little and drift on, as in Spyro 3."
            : "Hover is off: ▲ while gliding drops Spyro straight down, as in the original game.";
    }

    /// <summary>Shows the project's saved choice (on project and level changes, and after each choice).</summary>
    private void RefreshHoverSettings()
    {
        bool on = false;
        string error = "";
        try { on = SpyroMovesSettings.LoadHover(_workspace.RootPath); }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException)
        { error = e.Message; }
        _syncingHover = true;
        try
        {
            _hoverEnabled.IsChecked = error.Length == 0 && on;
            _hoverEnabled.IsEnabled = !_createBinBusy;
        }
        finally { _syncingHover = false; }
        _hoverSummary.Text = error.Length > 0 ? "The saved choice could not be read, so Create BIN stops until you tick or untick Hover again: " + error
            : on ? "On: the games you create have the hover." : "";
        _hoverSummary.IsVisible = _hoverSummary.Text.Length > 0;
    }

    /// <summary>Create BIN's hover scope: the export's last executable step installs the hover when the project asks.</summary>
    private NativeHover.Scope UseProjectHover()
    {
        bool on;
        try { on = SpyroMovesSettings.LoadHover(_workspace.RootPath); }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException)
        { throw new InvalidDataException("The Hover setting (Game tab) could not be read. Tick or untick Hover again. (" + e.Message + ")", e); }
        return NativeHover.Use(on && !IsLeftOut(Spyro.Editor.Core.Persistence.ExportParts.Hover));
    }
}
