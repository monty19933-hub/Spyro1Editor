using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.App.Views;

public sealed partial class MainWindow
{
    private const double SelectedMobyZRange = 512;

    private readonly Slider _selectedMobyZSlider = new()
    {
        Name = "SelectedMobyZSlider",
        Minimum = 0,
        Maximum = 1,
        Value = 0,
        TickFrequency = 1,
        IsSnapToTickEnabled = true,
        SmallChange = 1,
        LargeChange = 16,
        IsEnabled = false
    };
    private readonly TextBlock _selectedMobyZValueText = new()
    {
        Text = "--",
        HorizontalAlignment = HorizontalAlignment.Right,
        VerticalAlignment = VerticalAlignment.Center
    };
    private readonly CheckBox _selectedMobySnapZBox = new()
    {
        Name = "SelectedMobySnapZBox",
        Content = "Snap to terrain Z",
        IsChecked = false,
        IsEnabled = false,
        Foreground = new SolidColorBrush(ModernInk)
    };
    private readonly HashSet<string> _mobyTerrainSnapDisabledKeys = new(StringComparer.OrdinalIgnoreCase);
    private bool _syncingSelectedMobyZControls;
    private bool _selectedMobyZControlsWired;
    private string _selectedMobyZRangeKey = "";

    private Control BuildSelectedMobyZControls()
    {
        if (!_selectedMobyZControlsWired)
        {
            _selectedMobyZControlsWired = true;
            _selectedMobyZSlider.PropertyChanged += (_, e) =>
            {
                if (e.Property == RangeBase.ValueProperty)
                    ApplySelectedMobyZSliderValue();
            };
            _selectedMobySnapZBox.IsCheckedChanged += (_, _) => ApplySelectedMobyTerrainSnapChoice();
        }

        ToolTip.SetTip(_selectedMobyZSlider, "Move the selected object and its linked companions vertically in one-unit steps. Manual Z movement turns terrain snap off for this object.");
        ToolTip.SetTip(_selectedMobySnapZBox, "When enabled, horizontal object movement follows the nearest terrain surface. Enabling it also snaps the object at its current X/Y.");

        Grid zRow = new()
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(52)),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(78))
            },
            ColumnSpacing = 8
        };
        zRow.Children.Add(new TextBlock
        {
            Text = "Z axis",
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(ModernMutedInk),
            VerticalAlignment = VerticalAlignment.Center
        });
        AddGridControl(zRow, _selectedMobyZSlider, 1, 0);
        AddGridControl(zRow, _selectedMobyZValueText, 2, 0);

        StackPanel panel = new() { Spacing = 6 };
        panel.Children.Add(zRow);
        panel.Children.Add(new Border
        {
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(ModernLine),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(8, 5),
            Child = _selectedMobySnapZBox
        });
        RefreshSelectedMobyZControls(null);
        return panel;
    }

    private void ApplySelectedMobyZSliderValue()
    {
        if (_syncingSelectedMobyZControls || _selectedMoby == null || _selectedMoby.IsRemoved)
            return;

        Moby moby = _selectedMoby;
        float targetZ = (float)_selectedMobyZSlider.Value;
        float dz = targetZ - moby.Position.Z;
        if (Math.Abs(dz) <= 0.001f)
            return;

        if (ShouldSnapMobyToTerrain(moby))
            _mobyTerrainSnapDisabledKeys.Add(MobyTerrainSnapKey(moby));
        (List<Moby> moved, _) = MoveLinkedMobyGroup(moby, 0, 0, dz, snapToTerrain: false);
        _viewport.InvalidateVisual();
        RefreshMobyList(moby);
        ShowSelection(ViewportSelectionChangedEventArgs.ForMoby(moby));
        _statusText.Text = moved.Count > 1
            ? $"Set {moby.DisplayLabel} Z to {moby.Position.Z:0.0} with {moved.Count - 1} linked object(s)."
            : $"Set {moby.DisplayLabel} Z to {moby.Position.Z:0.0}.";
    }

    private void ApplySelectedMobyTerrainSnapChoice()
    {
        if (_syncingSelectedMobyZControls || _selectedMoby == null || _selectedMoby.IsRemoved)
            return;

        Moby moby = _selectedMoby;
        if (!ShouldSnapMobyToTerrain(moby))
        {
            RefreshSelectedMobyZControls(moby);
            return;
        }

        string key = MobyTerrainSnapKey(moby);
        bool enabled = _selectedMobySnapZBox.IsChecked == true;
        if (enabled)
            _mobyTerrainSnapDisabledKeys.Remove(key);
        else
            _mobyTerrainSnapDisabledKeys.Add(key);

        int linkedCount = 1;
        bool snapped = enabled && SnapSelectedMobyToCurrentTerrain(moby, out linkedCount);
        _viewport.InvalidateVisual();
        RefreshMobyList(moby);
        ShowSelection(ViewportSelectionChangedEventArgs.ForMoby(moby));
        _statusText.Text = enabled
            ? snapped
                ? linkedCount > 1
                    ? $"Snapped {moby.DisplayLabel} and {linkedCount - 1} linked object(s) to terrain Z."
                    : $"Snapped {moby.DisplayLabel} to terrain Z."
                : $"Terrain Z snapping is enabled for {moby.DisplayLabel}."
            : $"Terrain Z snapping is off for {moby.DisplayLabel}; horizontal movement will preserve its current Z.";
    }

    private bool SnapSelectedMobyToCurrentTerrain(Moby moby, out int linkedCount)
    {
        linkedCount = 1;
        if (!TryFindTerrainZAt(moby.Position.X, moby.Position.Y, moby.Position.Z, out float terrainZ))
            return false;

        float dz = terrainZ - moby.Position.Z;
        if (Math.Abs(dz) <= 0.001f)
            return true;

        (List<Moby> moved, _) = MoveLinkedMobyGroup(moby, 0, 0, dz, snapToTerrain: false);
        linkedCount = moved.Count;
        return true;
    }

    private void RefreshSelectedMobyZControls(Moby? moby)
    {
        _syncingSelectedMobyZControls = true;
        try
        {
            bool hasSelection = moby != null && !moby.IsRemoved;
            _selectedMobyZSlider.IsEnabled = hasSelection;
            if (!hasSelection)
            {
                _selectedMobyZRangeKey = "";
                _selectedMobyZSlider.Minimum = 0;
                _selectedMobyZSlider.Maximum = 1;
                _selectedMobyZSlider.Value = 0;
                _selectedMobyZValueText.Text = "--";
                _selectedMobySnapZBox.IsEnabled = false;
                _selectedMobySnapZBox.IsChecked = false;
                return;
            }

            Moby selected = moby!;
            string key = MobyTerrainSnapKey(selected);
            if (!string.Equals(_selectedMobyZRangeKey, key, StringComparison.OrdinalIgnoreCase))
            {
                _selectedMobyZRangeKey = key;
                _selectedMobyZSlider.Minimum = Math.Floor(selected.Position.Z - SelectedMobyZRange);
                _selectedMobyZSlider.Maximum = Math.Ceiling(selected.Position.Z + SelectedMobyZRange);
            }
            else
            {
                if (selected.Position.Z <= _selectedMobyZSlider.Minimum + 0.001)
                    _selectedMobyZSlider.Minimum = Math.Floor(selected.Position.Z - 256);
                if (selected.Position.Z >= _selectedMobyZSlider.Maximum - 0.001)
                    _selectedMobyZSlider.Maximum = Math.Ceiling(selected.Position.Z + 256);
            }

            _selectedMobyZSlider.Value = Math.Clamp(
                selected.Position.Z,
                (float)_selectedMobyZSlider.Minimum,
                (float)_selectedMobyZSlider.Maximum);
            _selectedMobyZValueText.Text = selected.Position.Z.ToString("0.0");

            bool supportsSnap = ShouldSnapMobyToTerrain(selected);
            _selectedMobySnapZBox.IsEnabled = supportsSnap;
            _selectedMobySnapZBox.IsChecked = supportsSnap && ShouldAutoSnapMobyToTerrain(selected);
            if (supportsSnap && TryFindTerrainZAt(selected.Position.X, selected.Position.Y, selected.Position.Z, out float terrainZ))
            {
                float offset = selected.Position.Z - terrainZ;
                ToolTip.SetTip(_selectedMobyZValueText, $"Current Z {selected.Position.Z:0.###}; nearest terrain offset {offset:+0.###;-0.###;0}.");
            }
            else
            {
                ToolTip.SetTip(_selectedMobyZValueText, $"Current Z {selected.Position.Z:0.###}.");
            }
        }
        finally
        {
            _syncingSelectedMobyZControls = false;
        }
    }

    private bool ShouldAutoSnapMobyToTerrain(Moby moby)
    {
        return ShouldSnapMobyToTerrain(moby) &&
            !_mobyTerrainSnapDisabledKeys.Contains(MobyTerrainSnapKey(moby));
    }

    private string MobyTerrainSnapKey(Moby moby)
    {
        string levelKey = _currentLevel?.Key ?? "no-level";
        string objectKey = moby.IsEditorControl
            ? $"control:{moby.EditorControlKind}"
            : moby.TrueIndex >= 0
                ? $"true:{moby.TrueIndex}"
                : $"index:{moby.Index}";
        return $"{levelKey}:{objectKey}";
    }
}
