using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.App.Views;

/// <summary>File ▾ › New World…: a seventh world of the project's own — a homeworld named after the world and up to four
/// levels, each starting as the New Level meadow (<see cref="StarterLevel"/>) and edited like any level you build. They
/// appear in the level picker after the game's own levels.</summary>
public sealed partial class MainWindow
{
    /// <summary>Answers the New World dialog in tests (null: cancel).</summary>
    internal sealed record NewWorldChoice(string Name, string EntryHomeworldKey, int Levels);
    internal Func<NewWorldChoice?>? NewWorldChoiceForTesting { get; set; }

    private static readonly string[] WorldLevelNames = ["LEVEL ONE", "LEVEL TWO", "LEVEL THREE", "LEVEL FOUR"];

    /// <summary>Level-picker entries of the new world's levels that have a saved scene, under their current names.</summary>
    private IReadOnlyList<LevelDefinition> WorldLevelPickerOptions()
    {
        try
        {
            if (string.IsNullOrEmpty(_workspace.RootPath) || NewWorldDocument.Read(_workspace.RootPath) is not { } world)
                return [];
            List<LevelDefinition> options = [];
            foreach (int id in world.LevelIds)
            {
                string path = SceneDocumentStore.PathForTarget(_workspace.RootPath, NewWorldDocument.KeyFor(id));
                if (File.Exists(path))
                    options.Add(NewWorldDocument.Target(id, SceneDocument.Read(path).Name));
            }
            return options;
        }
        catch (Exception e) when (e is IOException or InvalidDataException or System.Text.Json.JsonException or UnauthorizedAccessException)
        {
            Spyro.Editor.Core.Diagnostics.EditorDiagnostics.RecordWarning("New world levels", e.Message);
            return [];
        }
    }

    private async Task OpenWorldLevelAsync(int levelId)
    {
        string path = SceneDocumentStore.PathForTarget(_workspace.RootPath, NewWorldDocument.KeyFor(levelId));
        if (!File.Exists(path)) { _statusText.Text = "This level of your world has not been made yet."; return; }
        if ((HasUnsavedTerrainEdits() || HasUnsavedMobyEdits()) && !await SaveCurrentEditsAsync()) return;
        ApplyFreshScene(SceneDocument.Read(path));
        _statusText.Text = levelId == NewWorldDocument.Homeworld
            ? $"Opened {_freshSceneDocument!.Name}, your world's homeworld."
            : $"Opened {_freshSceneDocument!.Name}, a level of your world.";
    }

    private async Task NewWorldAsync()
    {
        if (_createBinBusy || _regularEditorPersistenceBusy || _workspaceTransitionBusy) return;
        if ((HasUnsavedTerrainEdits() || HasUnsavedMobyEdits()) && !await SaveCurrentEditsAsync()) return;
        string root = _workspace.RootPath;
        var existing = NewWorldDocument.Read(root);
        if (existing != null && NewWorldChoiceForTesting == null &&
            !await ShowConfirmationAsync("NewWorldReplaceDialog", "New World", $"Replace {existing.Name} with a new world?",
                "This project already has a world of its own, and a project holds one. Starting a new one replaces its homeworld and levels.\n" +
                "Copies of them are kept first in the project's \"backups\" folder.", "Start New World"))
            return;
        var choice = NewWorldChoiceForTesting != null ? NewWorldChoiceForTesting() : await ShowNewWorldDialogAsync(existing);
        if (choice == null || _workspace.RootPath != root) return;
        try
        {
            if (existing != null)
            {
                string folder = Path.Combine(root, "backups", $"world-{DateTime.Now:yyyyMMdd-HHmmss}");
                Directory.CreateDirectory(folder);
                File.Copy(NewWorldDocument.PathFor(root), Path.Combine(folder, NewWorldDocument.FileName));
                foreach (int id in existing.LevelIds)
                {
                    string scene = SceneDocumentStore.PathForTarget(root, NewWorldDocument.KeyFor(id));
                    if (File.Exists(scene)) File.Move(scene, Path.Combine(folder, Path.GetFileName(scene)));
                }
            }
            var ids = new[] { NewWorldDocument.Homeworld }.Concat(NewWorldDocument.LevelSlots.Take(choice.Levels)).ToArray();
            var world = new NewWorldDocument(1, choice.Name, choice.EntryHomeworldKey, ids);
            foreach (int id in ids)
            {
                string name = id == NewWorldDocument.Homeworld ? choice.Name : WorldLevelNames[id - NewWorldDocument.LevelSlots[0]];
                var scene = StarterLevel.Create(name, _retailCatalog) with { WorldLevelId = id };
                await scene.SaveAsync(SceneDocumentStore.PathForTarget(root, NewWorldDocument.KeyFor(id)));
            }
            await world.SaveAsync(root);
            await OpenWorldLevelAsync(NewWorldDocument.Homeworld);
            ApplyEditorExperienceForCurrentLevel(_modernTerrainWorkspaceTab);
            string entry = _retailCatalog.FindByKey(choice.EntryHomeworldKey)?.DisplayName ?? choice.EntryHomeworldKey;
            _statusText.Text = $"{choice.Name} is made: its homeworld and {Count(choice.Levels, "level", "levels")}, all in the level list. " +
                $"Players reach it through a doorway in {entry}. Build each level like any level of your own.";
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
        { _statusText.Text = "Could not make the new world: " + e.Message; }
    }

    private async Task<NewWorldChoice?> ShowNewWorldDialogAsync(NewWorldDocument? existing)
    {
        var content = new StackPanel { Margin = new Thickness(22), Spacing = 12 };
        content.Children.Add(new TextBlock { Text = "Make a new world", FontSize = 20, FontWeight = FontWeight.SemiBold });
        content.Children.Add(new TextBlock
        {
            TextWrapping = TextWrapping.Wrap, LineHeight = 20,
            Text = "A world of your own after the game's six: a homeworld and up to four levels. Each starts as a meadow you can " +
                   "reshape and paint (Terrain › Ground) and fill with objects. Players walk in through a doorway in one of the game's homeworlds."
        });
        var name = new TextBox { Name = "NewWorldName", Text = existing?.Name ?? "SKY ISLES", MaxLength = NewWorldDocument.MaxNameLength, Watermark = "WORLD NAME" };
        content.Children.Add(new TextBlock { Text = "Name (shown on the doorway, in the inventory and as its homeworld's name)" });
        content.Children.Add(name);
        var homeworlds = NewWorldDocument.HomeworldKeys.Select(k => _retailCatalog.FindByKey(k)).OfType<LevelDefinition>().ToList();
        var entry = new ComboBox
        {
            Name = "NewWorldEntry", HorizontalAlignment = HorizontalAlignment.Stretch, ItemsSource = homeworlds.Select(h => h.DisplayName).ToArray(),
            SelectedIndex = Math.Max(0, homeworlds.FindIndex(h => h.Key == (existing?.EntryHomeworldKey ?? "artisans")))
        };
        content.Children.Add(new TextBlock { Text = "Its doorway is in" });
        content.Children.Add(entry);
        var count = new ComboBox
        {
            Name = "NewWorldLevels", HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = new[] { "1 level", "2 levels", "3 levels", "4 levels" }, SelectedIndex = 3
        };
        content.Children.Add(new TextBlock { Text = "Levels besides the homeworld" });
        content.Children.Add(count);
        var problem = new TextBlock { Name = "NewWorldProblem", TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.FromRgb(168, 98, 0)) };
        content.Children.Add(problem);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Right };
        Button cancel = NewButton("Cancel"), make = NewButton("Make World");
        cancel.Name = "NewWorldCancel"; make.Name = "NewWorldMake";
        var dialog = new Window
        {
            Name = "NewWorldDialog", Title = "New World", Width = 520, SizeToContent = SizeToContent.Height, CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = content
        };
        void Check()
        {
            string text = (name.Text ?? "").Trim().ToUpperInvariant();
            bool ok = NewWorldDocument.IsValidName(text);
            problem.Text = ok ? "" : $"Use capital letters, digits, spaces and apostrophes, up to {NewWorldDocument.MaxNameLength}: the game's font has no others.";
            make.IsEnabled = ok;
        }
        name.TextChanged += (_, _) => Check(); Check();
        cancel.Click += (_, _) => dialog.Close(false); make.Click += (_, _) => dialog.Close(true);
        StyleModernPrimaryButton(make, ModernBlue, 132);
        actions.Children.Add(cancel); actions.Children.Add(make); content.Children.Add(actions);
        if (!await dialog.ShowDialog<bool>(this)) return null;
        return new((name.Text ?? "").Trim().ToUpperInvariant(), homeworlds[Math.Max(0, entry.SelectedIndex)].Key, Math.Max(0, count.SelectedIndex) + 1);
    }
}
