using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Spyro.Editor.Core.Editing;

namespace Spyro.Editor.App.Views;

public sealed partial class MainWindow
{
    private string PreservedResearchExportNote() =>
        _releaseMode && (Id65BlankLabHasCanonicalAuthoringDocument() || Id65BlankLabHasAnyAuthoredArtifacts())
            ? " Older experimental drafts are preserved separately and are not included."
            : "";

    private async Task<int?> ShowAuthoredProjectExportScopeAsync(IReadOnlyList<SceneDocument> scenes, int currentLevelId)
    {
        var targets = FindEditedLevelExportTargets();
        string existing = targets.Count == 0 ? "No saved changes to existing levels." :
            "Saved changes: " + string.Join(", ", targets.Select(t => t.Level.DisplayName)) + ".";
        var choices = new[] { (Id:10, Name:scenes.FirstOrDefault(s=>s.TargetBinding?.LevelId==10)?.Name??"Artisans") }
            .Concat(scenes.Where(s=>s.ResolveTarget(_retailCatalog).LevelId!=10).Select(s=>(Id:s.ResolveTarget(_retailCatalog).LevelId,Name:s.Name))).ToArray();
        var start = new ComboBox
        {
            Name = "ExportStartLevel", HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = choices.Select(c=>c.Name).ToArray(),
            SelectedIndex = Math.Max(0,Array.FindIndex(choices,c=>c.Id==currentLevelId))
        };
        var content = new StackPanel { Spacing = 12, Margin = new Thickness(22) };
        content.Children.Add(new TextBlock
        {
            Text = "Create a playable copy", FontSize = 20, FontWeight = FontWeight.SemiBold
        });
        content.Children.Add(new TextBlock
        {
            Name = "ExportIncludedLevels", TextWrapping = TextWrapping.Wrap,
            Text = existing + "\nRebuilt levels: " + string.Join(", ",scenes.Select(s=>s.Name)) + "."
        });
        content.Children.Add(new TextBlock { Text = "New Game starts in" });
        content.Children.Add(start);
        content.Children.Add(new TextBlock
        {
            Text = "Start a New Game to play the rebuilt layout from its safe player start. Loading an existing game keeps its previous collected state.",
            TextWrapping = TextWrapping.Wrap
        });
        string research = PreservedResearchExportNote().Trim();
        if (research.Length > 0)
            content.Children.Add(new TextBlock { Name = "ExportPreservedDrafts", Text = research, TextWrapping = TextWrapping.Wrap });
        var dialog = new Window
        {
            Title = "Create BIN/CUE", Name = "ProjectExportScope", Width = 520,
            SizeToContent = SizeToContent.Height, CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Content = content
        };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = "Cancel", Name = "ExportCancel" };
        var create = new Button { Content = "Create BIN/CUE", Name = "ExportCreate" };
        StyleModernPrimaryButton(create, ModernBlue, 132);
        cancel.Click += (_, _) => dialog.Close(null);
        create.Click += (_, _) => dialog.Close((int?)choices[start.SelectedIndex].Id);
        actions.Children.Add(cancel); actions.Children.Add(create); content.Children.Add(actions);
        return await dialog.ShowDialog<int?>(this);
    }
}
