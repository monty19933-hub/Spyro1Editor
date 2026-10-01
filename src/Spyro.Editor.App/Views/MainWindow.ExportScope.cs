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

    private async Task<bool?> ShowProjectExportScopeAsync(SceneDocument authored, bool authoredActive)
    {
        var targets = FindEditedLevelExportTargets();
        string existing = targets.Count == 0 ? "No saved changes to existing levels." :
            "Saved changes: " + string.Join(", ", targets.Select(t => t.Level.DisplayName)) + ".";
        var start = new ComboBox
        {
            Name = "ExportStartLevel", HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemsSource = new[] { "Original game — Artisans", authored.Name + " — authored level" },
            SelectedIndex = authoredActive ? 1 : 0
        };
        var content = new StackPanel { Spacing = 12, Margin = new Thickness(22) };
        content.Children.Add(new TextBlock
        {
            Text = "Create a playable copy", FontSize = 20, FontWeight = FontWeight.SemiBold
        });
        content.Children.Add(new TextBlock
        {
            Name = "ExportIncludedLevels", TextWrapping = TextWrapping.Wrap,
            Text = existing + "\nYour authored level, " + authored.Name + ", is also included."
        });
        content.Children.Add(new TextBlock { Text = "New Game starts in" });
        content.Children.Add(start);
        content.Children.Add(new TextBlock
        {
            Text = "Loading an existing game keeps its saved level. To visit your authored level directly, choose it above and start a New Game.",
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
        create.Click += (_, _) => dialog.Close((bool?)(start.SelectedIndex == 1));
        actions.Children.Add(cancel); actions.Children.Add(create); content.Children.Add(actions);
        return await dialog.ShowDialog<bool?>(this);
    }
}
