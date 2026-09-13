using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Music;
using Spyro.Editor.Core.Persistence;
using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.App.Views;

internal enum FreshDocumentDecision { Cancel, Discard, Save }

/// <summary>Separate neutral authoring UI. Never inserts a fabricated retail scene into MainWindow.</summary>
internal sealed partial class NativeFreshDocumentWindow : Window
{
    private const string LegacyCapabilityText = "Fresh floor and spawn — not the Town-bound Blank-Level Lab. Normal Create BIN is unavailable for this document. The separate research action compiles the actual applied, saved document to a new BIN/CUE; it does not certify gameplay.";
    private readonly EditorProjectLayout _project;
    private readonly Func<bool> _isCurrentProject;
    private NativeId65FreshDocumentSnapshot _snapshot;
    private readonly Dictionary<string, TextBox> _fields = new(StringComparer.Ordinal);
    private readonly List<NativeId65FreshDocumentIntent?> _undo = [];
    private readonly List<NativeId65FreshDocumentIntent?> _redo = [];
    private readonly StackPanel _fieldsPanel = new() { Spacing = 12 };
    private readonly TextBlock _status = new() { Name = "FreshDocumentStatus", TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _state = new() { FontWeight = FontWeight.SemiBold };
    private readonly TextBlock _spawnHeight = new();
    private readonly TextBlock _capability = new()
    {
        Name = "FreshDocumentCapability", Text = LegacyCapabilityText,
        TextWrapping = TextWrapping.Wrap, FontSize = 12, MaxWidth = 1050
    };
    private readonly ComboBox _music = new ReadOnlySelectionComboBox() { Name = "FreshDocumentMusic", ItemsSource = MusicTrackCatalog.SelectableTracks, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly Button _new;
    private readonly Button _apply;
    private readonly Button _undoButton;
    private readonly Button _redoButton;
    private readonly Button _save;
    private readonly Button _reopen;
    private bool _refreshing;
    private bool _decisionBusy;
    private bool _allowClose;
    private string _appliedFields = "";

    internal NativeFreshDocumentWindow(EditorProjectLayout project, Func<bool> isCurrentProject,
        Func<(string Image, string Cue)>? researchSourcePaths = null, Func<IDisposable>? beginResearch = null,
        INativeFreshMobyCatalogueProvider? mobyCatalogueProvider = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(isCurrentProject);
        _project = project;
        _isCurrentProject = isCurrentProject;
        _researchSourcePaths = researchSourcePaths;
        _beginResearch = beginResearch;
        _mobyCatalogueProvider = mobyCatalogueProvider;
        RequireCurrentProject();
        _snapshot = NativeId65FreshDocumentStore.Capture(project);
        CurrentIntent = _snapshot.Intent;
        Title = "Fresh Native Level Document";
        Width = 1120;
        Height = 800;
        MinWidth = 940;
        MinHeight = 680;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = new SolidColorBrush(Color.Parse("#F1F4F8"));
        Preview = new NativeFreshDocumentPreview { Name = "FreshDocumentPreview", MinHeight = 360 };

        _new = Button("FreshDocumentNew", "New starter", NewStarter);
        _apply = Button("FreshDocumentApply", "Apply to preview", () =>
        {
            if (!_modeSwitchBusy) ApplyFields();
        });
        _undoButton = Button("FreshDocumentUndo", "Undo", Undo);
        _redoButton = Button("FreshDocumentRedo", "Redo", Redo);
        _save = Button("FreshDocumentSave", "Save document", () =>
        {
            if (!_modeSwitchBusy && (!_aggregateMode || !AggregateCommandsBlocked)) SaveDocument();
        });
        _save.Background = new SolidColorBrush(Color.Parse("#2468B2"));
        _save.Foreground = Brushes.White;
        _reopen = Button("FreshDocumentReopen", "Reopen saved", null);
        _reopen.Click += async (_, _) => await ReopenAsync();
        var close = Button("FreshDocumentClose", "Close", Close);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        foreach (var button in new[] { _new, _apply, _undoButton, _redoButton, _reopen, _save, close }) actions.Children.Add(button);

        var heading = new StackPanel
        {
            Spacing = 6,
            Children =
            {
                new TextBlock { Text = "Fresh Native Level", FontSize = 26, FontWeight = FontWeight.SemiBold },
                new TextBlock { Text = $"Project: {project.ProjectId}  ·  separate authored document", Foreground = Brushes.DimGray },
                _capability,
                BuildAggregateActions(),
                actions,
                BuildResearchActions()
            }
        };
        var previewPanel = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto"), RowSpacing = 10 };
        previewPanel.Children.Add(new TextBlock { Text = "Authored schematic · not a gameplay viewport", FontWeight = FontWeight.SemiBold });
        Grid.SetRow(Preview, 1);
        previewPanel.Children.Add(Preview);
        var previewNote = new TextBlock
        {
            Text = "Two schematic triangles; the marker shows the authored spawn +512 above the floor. No player footprint, native camera, terrain clearance or runtime behavior is inferred.",
            TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, FontSize = 12
        };
        Grid.SetRow(previewNote, 2);
        previewPanel.Children.Add(previewNote);

        _fieldsPanel.Children.Add(Section("Floor rectangle", "Native world units. Floor origin and size use multiples of 16; size is 16–2048 and must fit one native cell."));
        _fieldsPanel.Children.Add(Row(Field("FreshFloorX", "X"), Field("FreshFloorY", "Y"), Field("FreshFloorZ", "Z")));
        _fieldsPanel.Children.Add(Row(Field("FreshFloorWidth", "Width"), Field("FreshFloorDepth", "Depth")));
        _fieldsPanel.Children.Add(Section("Spawn", "XY must be strictly inside the floor. Change floor and spawn together, then Apply."));
        _fieldsPanel.Children.Add(Row(Field("FreshSpawnX", "X"), Field("FreshSpawnY", "Y"), Field("FreshHeading", "Heading 0–255")));
        _spawnHeight.FontSize = 12;
        _fieldsPanel.Children.Add(_spawnHeight);
        _fieldsPanel.Children.Add(Section("Display colors", "Opaque RGB hex, #RRGGBB. The schematic uses these authored colors directly."));
        _fieldsPanel.Children.Add(Row(Field("FreshFloorColor", "Floor"), Field("FreshSkyColor", "Sky")));
        _fieldsPanel.Children.Add(Section("Music", "Select an existing native track. Research compilation uses this selection; the schematic has no audio preview."));
        _fieldsPanel.Children.Add(_music);
        _fieldsPanel.Children.Add(BuildAggregateFields());
        _fieldsPanel.Children.Add(BuildMaterialFields());
        _music.SelectionChanged += (_, _) => FieldsChanged();
        // Source-capture cancellation must not sit inside the disabled form.
        var inspectorContent = new StackPanel { Spacing = 12, Children = { _fieldsPanel, BuildMaterialInspectionFields(), BuildMobyCataloguePanel() } };
        var inspector = new ScrollViewer { Content = inspectorContent, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("*,380"), ColumnSpacing = 20 };
        // Long aggregate/status text can leave less than the schematic's
        // minimum height. Scroll the whole left panel instead of allowing its
        // minimum-sized preview to overlap the caption or footer.
        body.Children.Add(new ScrollViewer
        {
            Content = previewPanel, VerticalContentAlignment = VerticalAlignment.Stretch,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled
        });
        Grid.SetColumn(inspector, 1);
        body.Children.Add(inspector);
        var footer = new StackPanel { Spacing = 4, Children = { _state, _status } };
        var layout = new Grid { Margin = new Thickness(22), RowDefinitions = new RowDefinitions("Auto,*,Auto"), RowSpacing = 18 };
        layout.Children.Add(heading);
        Grid.SetRow(body, 1);
        layout.Children.Add(body);
        Grid.SetRow(footer, 2);
        layout.Children.Add(footer);
        Content = layout;
        WriteFields();
        _status.Text = CurrentIntent == null ? "No saved fresh document exists. Choose New starter explicitly to begin." :
            $"Opened saved document: {_snapshot.Path}";
        Closing += OnClosing;
        KeyDown += (_, e) =>
        {
            KeyModifiers command = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;
            if (e.Key == Key.S && e.KeyModifiers == command)
            {
                e.Handled = true;
                if (!_decisionBusy && !_researchBusy && !_materialInspectionBusy && !_mobyCatalogueBusy && !_mobyCatalogueCleanupBlocked) SaveDocument();
            }
        };
    }

    internal NativeId65FreshDocumentIntent? CurrentIntent { get; private set; }
    internal NativeFreshDocumentPreview Preview { get; }
    internal Func<string, Task<FreshDocumentDecision>>? DecisionForTesting { get; set; }
    internal bool HasUnappliedFields => _aggregateMode ? AggregateHasUnappliedFields :
        CurrentIntent != null && FieldSignature() != _appliedFields;
    internal bool HasUnsavedChanges => HasUnappliedFields || (_aggregateMode
        ? _aggregateSession?.HasUnsavedChanges == true : !Equal(CurrentIntent, _snapshot.Intent));

    private void NewStarter()
    {
        if (_aggregateMode || _modeSwitchBusy || CurrentIntent != null || _snapshot.Exists || _decisionBusy || _researchBusy || _materialInspectionBusy) return;
        Push(_undo, CurrentIntent);
        _redo.Clear();
        CurrentIntent = NativeId65FreshDocumentIntent.Create(8448, 8448, 8704, 1024, 1024, 8960, 8960, 64,
            new ColorRgba(80, 128, 112), new ColorRgba(128, 96, 64), 19);
        WriteFields();
        _status.Text = "New starter is unsaved. Edit floor and spawn together, Apply, then Save document.";
    }

    private bool ApplyFields()
    {
        if (_aggregateMode) return ApplyAggregateFields();
        if (CurrentIntent == null || _researchBusy || _materialInspectionBusy) return false;
        try
        {
            var candidate = NativeId65FreshDocumentIntent.Create(Number("FreshFloorX"), Number("FreshFloorY"), Number("FreshFloorZ"),
                Number("FreshFloorWidth"), Number("FreshFloorDepth"), Number("FreshSpawnX"), Number("FreshSpawnY"), Number("FreshHeading"),
                Rgb("FreshFloorColor"), Rgb("FreshSkyColor"), (_music.SelectedItem as MusicTrackEntry)?.TrackId ?? -1);
            if (!Equal(candidate, CurrentIntent))
            {
                Push(_undo, CurrentIntent);
                _redo.Clear();
                CurrentIntent = candidate;
            }
            WriteFields();
            _status.Text = "Applied to the schematic and current document intent. Save document writes these exact values.";
            return true;
        }
        catch (Exception error) when (error is ArgumentException or InvalidDataException or OverflowException)
        {
            _status.Text = $"Changes not applied: {error.Message} The preview still shows the last applied intent; your fields are retained.";
            RefreshChrome();
            return false;
        }
    }

    private bool SaveDocument()
    {
        if (_aggregateMode) return SaveAggregateDocument();
        if (CurrentIntent == null || _researchBusy || _materialInspectionBusy) return false;
        try
        {
            RequireCurrentProject();
            if (!ApplyFields()) return false;
            _snapshot = NativeId65FreshDocumentStore.Save(_snapshot, CurrentIntent!);
            // Once a document exists, absence is not an undoable authored state:
            // the store deliberately has no delete operation. Keep all real edits.
            _undo.RemoveAll(value => value == null);
            _redo.RemoveAll(value => value == null);
            // Keep the actual immutable applied object for preview/undo identity;
            // Store's returned intent is independently parsed and byte-equivalent.
            RefreshChrome();
            _status.Text = _snapshot.RetainedRecoveryDirectory.Length == 0
                ? $"Saved and reopened verified document: {_snapshot.Path}"
                : $"Document saved and verified, but recovery files were retained: {_snapshot.RetainedRecoveryDirectory}. Inspect them before another save.";
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        {
            // A store error can describe an already committed save with failed
            // readback. Never relabel every error as “nothing was saved”.
            _status.Text = $"Save did not complete cleanly: {error.Message} Your draft is retained. Reopen to inspect the current file before retrying.";
            RefreshChrome();
            return false;
        }
    }

    private async Task ReopenAsync()
    {
        if (_aggregateMode) { await ReopenAggregateAsync(); return; }
        if (_modeSwitchBusy) return;
        if (_decisionBusy || _researchBusy || _materialInspectionBusy) return;
        try
        {
            RequireCurrentProject();
            if (!await ResolveDirtyAsync("reopening the saved document")) return;
            RequireCurrentProject();
            NativeId65FreshDocumentSnapshot next = NativeId65FreshDocumentStore.Capture(_project);
            _snapshot = next;
            CurrentIntent = next.Intent;
            _undo.Clear();
            _redo.Clear();
            WriteFields();
            _status.Text = next.Exists ? "Reopened the actual saved document. Undo history was reset." :
                "No fresh document exists now. No default was inferred; New starter is available.";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException)
        {
            _status.Text = $"Reopen refused: {error.Message} Current intent and unapplied fields are unchanged.";
        }
    }

    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (DeferMobyCatalogueClose(e)) return;
        if (DeferMaterialInspectionClose(e)) return;
        if (_modeSwitchBusy || _forkBusy)
        {
            e.Cancel = true;
            return;
        }
        if (_researchBusy)
        {
            e.Cancel = true;
            _status.Text = "Wait for research export to finish before closing. Cancel operation requests cancellation; committed files are always retained.";
            return;
        }
        if (_allowClose || !HasUnsavedChanges) return;
        e.Cancel = true;
        if (_decisionBusy) return;
        if (await ResolveDirtyAsync("closing this document"))
        {
            _allowClose = true;
            Dispatcher.UIThread.Post(Close);
        }
    }

    private async Task<bool> ResolveDirtyAsync(string action)
    {
        if (!HasUnsavedChanges) return true;
        _decisionBusy = true;
        RefreshChrome();
        try
        {
            FreshDocumentDecision decision = DecisionForTesting != null
                ? await DecisionForTesting(action) : await AskDecisionAsync(action);
            return decision == FreshDocumentDecision.Discard || decision == FreshDocumentDecision.Save && SaveDocument();
        }
        finally
        {
            _decisionBusy = false;
            RefreshChrome();
        }
    }

    private Task<FreshDocumentDecision> AskDecisionAsync(string action)
    {
        var dialog = new Window
        {
            Title = "Unsaved fresh document", Width = 460, Height = 210, CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        var choices = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        foreach (var choice in new[] { FreshDocumentDecision.Cancel, FreshDocumentDecision.Discard, FreshDocumentDecision.Save })
        {
            var button = new Button { Content = choice == FreshDocumentDecision.Discard ? "Discard changes" : choice.ToString(), MinHeight = 34 };
            button.Click += (_, _) => dialog.Close(choice);
            choices.Children.Add(button);
        }
        dialog.Content = new StackPanel
        {
            Margin = new Thickness(22), Spacing = 20,
            Children =
            {
                new TextBlock { Text = $"Save your changes before {action}? Unapplied or invalid field text is also unsaved work.", TextWrapping = TextWrapping.Wrap },
                choices
            }
        };
        return dialog.ShowDialog<FreshDocumentDecision>(this);
    }

    private void Undo()
    {
        if (_aggregateMode) { UndoAggregate(); return; }
        if (_modeSwitchBusy) return;
        if (_researchBusy || _materialInspectionBusy) return;
        if (HasUnappliedFields)
        {
            WriteFields();
            _status.Text = "Discarded unapplied field text; the last applied intent is unchanged.";
            return;
        }
        if (_undo.Count == 0) return;
        Push(_redo, CurrentIntent);
        CurrentIntent = Pop(_undo);
        WriteFields();
        _status.Text = "Undid one applied change. Save document to persist this state.";
    }

    private void Redo()
    {
        if (_aggregateMode) { RedoAggregate(); return; }
        if (_modeSwitchBusy) return;
        if (_researchBusy || _materialInspectionBusy) return;
        if (HasUnappliedFields || _redo.Count == 0) return;
        Push(_undo, CurrentIntent);
        CurrentIntent = Pop(_redo);
        WriteFields();
        _status.Text = "Redid one applied change. Save document to persist this state.";
    }

    private void WriteFields()
    {
        InvalidateMobyCatalogue();
        InvalidateMaterialInspection();
        _refreshing = true;
        try
        {
            var value = _aggregateMode ? CurrentAggregateIntent?.Base : CurrentIntent;
            Set("FreshFloorX", value?.FloorX); Set("FreshFloorY", value?.FloorY); Set("FreshFloorZ", value?.FloorZ);
            Set("FreshFloorWidth", value?.FloorWidth); Set("FreshFloorDepth", value?.FloorDepth);
            Set("FreshSpawnX", value?.SpawnX); Set("FreshSpawnY", value?.SpawnY); Set("FreshHeading", value?.Heading);
            _fields["FreshFloorColor"].Text = value == null ? "" : Hex(value.FloorColor);
            _fields["FreshSkyColor"].Text = value == null ? "" : Hex(value.SkyColor);
            _music.SelectedItem = value == null ? null : MusicTrackCatalog.Find(value.MusicTrackId);
            WriteAggregateFields();
            WriteMaterialFields();
            _appliedFields = FieldSignature();
            if (_aggregateMode) Preview.SetAggregate(CurrentAggregateIntent, _aggregateSession?.SelectedEditorId);
            else Preview.Intent = value;
            _spawnHeight.Text = value == null ? "Spawn Z = floor Z +512 (fixed)." : $"Spawn Z: {value.SpawnZ}  ·  floor +512 (fixed)";
        }
        finally { _refreshing = false; }
        RefreshChrome();
    }

    private void FieldsChanged()
    {
        if (_refreshing) return;
        InvalidateMobyCatalogue();
        InvalidateMaterialInspection();
        RefreshChrome();
        if (HasUnappliedFields && !_researchBusy && !_materialInspectionBusy && !_mobyCatalogueBusy) _status.Text = "Unapplied fields — the schematic still shows the last applied intent. Apply or Save validates the complete form.";
    }

    private void RefreshChrome()
    {
        bool editable = !_decisionBusy && !_researchBusy && !_materialInspectionBusy && !_modeSwitchBusy && !_forkBusy &&
            !_mobyCatalogueBusy && !_mobyCatalogueCleanupBlocked;
        bool hasDocument = _aggregateMode ? CurrentAggregateIntent != null : CurrentIntent != null;
        _new.IsEnabled = editable && !_aggregateMode && CurrentIntent == null && !_snapshot.Exists;
        _fieldsPanel.IsEnabled = editable && hasDocument;
        _apply.IsEnabled = editable && hasDocument;
        _save.IsEnabled = editable && hasDocument;
        _reopen.IsEnabled = editable;
        _undoButton.IsEnabled = editable && (HasUnappliedFields || (_aggregateMode ? _aggregateSession?.CanUndo == true : _undo.Count > 0));
        _redoButton.IsEnabled = editable && !HasUnappliedFields && (_aggregateMode ? _aggregateSession?.CanRedo == true : _redo.Count > 0);
        RefreshAggregateChrome(editable);
        RefreshResearchChrome();
        RefreshMaterialInspectionChrome();
        RefreshMobyCatalogueChrome(editable);
        _state.Text = HasUnappliedFields ? "Unapplied fields · preview unchanged" : HasUnsavedChanges ? "Unsaved document" :
            !hasDocument ? "No fresh document" : "Saved document";
        Title = "Fresh Native Level Document" + (HasUnsavedChanges ? " *" : "");
    }

    private void RequireCurrentProject()
    {
        if (!_isCurrentProject()) throw new InvalidDataException("The active project root or project identity changed. Close this window and open Fresh Level from the current project.");
    }

    private Control Field(string name, string label)
    {
        var box = new TextBox { Name = name, MinHeight = 32, MaxLength = 16, HorizontalAlignment = HorizontalAlignment.Stretch };
        _fields.Add(name, box);
        box.TextChanged += (_, _) => FieldsChanged();
        return new StackPanel { Spacing = 4, Children = { new TextBlock { Text = label, FontSize = 12 }, box } };
    }

    private static Control Row(params Control[] children)
    {
        var grid = new Grid { ColumnSpacing = 8 };
        foreach (Control child in children)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            Grid.SetColumn(child, grid.Children.Count);
            grid.Children.Add(child);
        }
        return grid;
    }

    private static Control Section(string title, string note) => new StackPanel
    {
        Spacing = 4,
        Children =
        {
            new TextBlock { Text = title, FontWeight = FontWeight.SemiBold },
            new TextBlock { Text = note, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, FontSize = 11 }
        }
    };

    private static Button Button(string name, string label, Action? action)
    {
        var button = new Button { Name = name, Content = label, MinHeight = 34, Padding = new Thickness(12, 6) };
        if (action != null) button.Click += (_, _) => action();
        return button;
    }

    private int Number(string name) => int.TryParse(_fields[name].Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
        ? value : throw new InvalidDataException($"{name[5..]} requires an integer.");

    private ColorRgba Rgb(string name)
    {
        string value = _fields[name].Text?.Trim() ?? "";
        if (value.Length != 7 || value[0] != '#' || !uint.TryParse(value.AsSpan(1), NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture, out uint rgb))
            throw new InvalidDataException($"{name[5..]} requires #RRGGBB.");
        return new((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
    }

    private void Set(string name, int? value) => _fields[name].Text = value?.ToString(CultureInfo.InvariantCulture) ?? "";
    private string FieldSignature() => string.Join("\n", _fields.Values.Select(box => box.Text ?? "")) + "\n" + (_music.SelectedItem as MusicTrackEntry)?.TrackId;
    private static string Hex(ColorRgba color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    private static bool Equal(NativeId65FreshDocumentIntent? a, NativeId65FreshDocumentIntent? b) =>
        ReferenceEquals(a, b) || a != null && b != null && NativeId65FreshDocumentCodec.SerializeCanonical(a).AsSpan()
            .SequenceEqual(NativeId65FreshDocumentCodec.SerializeCanonical(b));
    private static void Push(List<NativeId65FreshDocumentIntent?> stack, NativeId65FreshDocumentIntent? value)
    {
        if (stack.Count == 100) stack.RemoveAt(0);
        stack.Add(value);
    }
    private static NativeId65FreshDocumentIntent? Pop(List<NativeId65FreshDocumentIntent?> stack)
    {
        var value = stack[^1];
        stack.RemoveAt(stack.Count - 1);
        return value;
    }
}
