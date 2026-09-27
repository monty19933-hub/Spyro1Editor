using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Workspace;

namespace Spyro.Editor.App.Views;

public sealed partial class MainWindow
{
    private Button BuildNativeMobyLibraryButton(string name, bool allowExistingTemplateSelection)
    {
        Button button = NewButton("Browse Native Library…");
        button.Name = name;
        StyleModernSecondaryButton(button);
        button.Click += async (_, _) =>
        {
            button.IsEnabled = false;
            try
            {
                IReadOnlyList<AddMobyTemplate> templates = allowExistingTemplateSelection && _currentLevel != null
                    ? GetCreateBinSafeObjectGalleryTemplates(BuildAddMobyTemplates(_selectedMoby, _currentMobys)) : [];
                var chosen = await ShowNativeMobyLibraryAsync(this, templates);
                if (chosen != null) await AddMobyNearSelectionAsync(chosen);
            }
            finally { button.IsEnabled = true; }
        };
        return button;
    }

    private AddMobyTemplate? FindExistingNativeCatalogueTemplate(NativeMobyCatalogueEntry entry,
        IReadOnlyList<AddMobyTemplate> templates) => templates.FirstOrDefault(template =>
        (template.FromLevelTemplate || template.FromCrossLevelTemplate) &&
        entry.MatchesExistingTemplate(string.IsNullOrWhiteSpace(template.SourceLevelKey)
                ? _currentLevel?.Key ?? "" : template.SourceLevelKey,
            template.SourceTrueIndex, template.Type, template.State, template.SourceByte36,
            template.SourceByte37, template.SourceByte4F, template.Flag4A, template.Flag4B) &&
        IsCreateBinSafeObjectGalleryTemplate(template, _currentLevel?.Key ?? ""));

    private async Task<AddMobyTemplate?> ShowNativeMobyLibraryAsync(Window owner,
        IReadOnlyList<AddMobyTemplate> approvedTemplates)
    {
        var captured = CaptureSourceDiscOperationIdentity();
        string capturedSignature = BuildMobyEditSignature(_currentMobys);
        string targetKey = _currentLevel?.Key ?? "";
        string targetName = _currentLevel?.DisplayName ?? "No destination";
        LevelCatalog catalog = _retailCatalog;
        string ResolveSource() => FirstExistingDiscImagePath(_discImagePathBox.Text,
            _skyboxDiscImagePathBox.Text, DiscImageLocator.FindImage(_workspace));
        string sourcePath = ResolveSource();
        bool Current() => IsCurrentSourceDiscOperationIdentity(captured) &&
            PathsEqual(sourcePath, ResolveSource()) &&
            capturedSignature == BuildMobyEditSignature(_currentMobys);

        Window dialog = new()
        {
            Title = "Native Moby Library", Width = 1080, Height = 750, MinWidth = 860, MinHeight = 640,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, CanResize = true
        };
        bool closed = false;
        dialog.Closed += (_, _) => closed = true;
        NativeMobyCatalogue? catalogue = null;
        TextBox search = new() { Name = "NativeMobyLibrarySearch", PlaceholderText = "Search name, category, class (0x011B), level or T index", MaxLength = 256, MinHeight = 34 };
        ComboBox sources = new() { Name = "NativeMobyLibrarySource", MinWidth = 200, IsEnabled = false };
        ComboBox view = new()
        {
            Name = "NativeMobyLibraryView", ItemsSource = new[] { NativeMobyCatalogueView.All, NativeMobyCatalogueView.PlacedRows, NativeMobyCatalogueView.Resources },
            SelectedItem = NativeMobyCatalogueView.All, MinWidth = 170,
            ItemTemplate = new FuncDataTemplate<NativeMobyCatalogueView>((choice, _) => new TextBlock
            {
                Text = choice switch { NativeMobyCatalogueView.PlacedRows => "Placed objects", NativeMobyCatalogueView.Resources => "Registered resources", _ => "All observed objects/resources" }
            })
        };
        ListBox list = new()
        {
            Name = "NativeMobyLibraryList", MinHeight = 340, HorizontalAlignment = HorizontalAlignment.Stretch,
            ItemTemplate = new FuncDataTemplate<NativeMobyCatalogueEntry>((entry, _) => new StackPanel
            {
                Margin = new Thickness(6), Spacing = 3,
                Children =
                {
                    new TextBlock { Text = entry?.Label ?? "", FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis },
                    new TextBlock { Text = entry == null ? "" : $"0x{entry.ActorId:X4} · {entry.SourceLabel}", FontSize = 11, Foreground = new SolidColorBrush(ModernMutedInk) },
                    new TextBlock { Text = entry?.Category ?? "", FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis },
                    new TextBlock { Text = entry == null || entry.RelatedPlacedNames.Count == 0 ? "" : "Related: " + string.Join("; ", entry.RelatedPlacedNames),
                        FontSize = 11, TextWrapping = TextWrapping.Wrap, IsVisible = entry?.RelatedPlacedNames.Count > 0 }
                }
            })
        };
        TextBlock summary = NewSmallNote("Reading and verifying the original retail disc…");
        summary.Name = "NativeMobyLibrarySummary";
        TextBlock details = new()
        {
            Name = "NativeMobyLibraryDetails", Text = "Source capture is read-only; your scene and saved files are unchanged.",
            TextWrapping = TextWrapping.Wrap, FontSize = 12, LineHeight = 18
        };
        TextBlock dependencies = new() { Name = "NativeMobyLibraryDependencies", TextWrapping = TextWrapping.Wrap, FontSize = 12, LineHeight = 18 };
        Button use = NewButton("Use existing approved template");
        use.Name = "NativeMobyLibraryUseTemplate"; use.IsEnabled = false;
        StyleModernPrimaryButton(use, Color.FromRgb(35, 112, 165));
        Button cancel = NewButton("Close"); cancel.Name = "NativeMobyLibraryCancel";
        StyleModernSecondaryButton(cancel);
        cancel.Click += (_, _) => dialog.Close(null);

        void RefreshDetails()
        {
            use.IsEnabled = false;
            if (catalogue == null || list.SelectedItem is not NativeMobyCatalogueEntry entry)
            {
                if (catalogue != null) details.Text = "Select a native source variant to inspect its identity and destination status.";
                dependencies.Text = "Select an entry to inspect its dependency requirements and exact source evidence.";
                return;
            }
            var target = catalogue.DescribeTarget(entry, targetKey);
            AddMobyTemplate? matching = Current() && target.RetailTargetInspected
                ? FindExistingNativeCatalogueTemplate(entry, approvedTemplates) : null;
            use.IsEnabled = matching != null;
            string action = matching == null
                ? "No existing approved Add template matches this exact variant here. Browsing does not enable Apply or Create BIN."
                : "An existing approved Add template matches this exact source row. Use it to open the normal Add workflow; nothing is inserted by browsing.";
            if (!Current()) action = "The source, scene or edits changed while browsing. Close and reopen the library before selecting a template.";
            details.Text = $"{entry.Label}\n{entry.SourceLabel} · class 0x{entry.ActorId:X4}\n{entry.Category}\n\n" +
                (entry.RelatedPlacedNames.Count == 0 ? "" : NativeMobyCataloguePresentation.RelatedNames(entry.RelatedPlacedNames, entry.Kind) + "\n\n") +
                (entry.Kind == NativeMobyCatalogueEntryKind.PlacedRow
                    ? $"Properties location: {entry.PropertiesLocation}. This is not a private payload schema.\n\n"
                    : "This is not a placed object. A resource registration cannot be added as a Moby.\n\n") +
                $"Destination: {targetName}\n{target.Description}\n\n{action}\n\n" +
                $"Classification only: {entry.ClassificationEvidence}\n\nSource identity:\n{entry.SourceVariantId}";
            dependencies.Text = NativeMobyCataloguePresentation.Dependencies(catalogue.DescribeDependencies(entry));
        }
        void RefreshList()
        {
            if (catalogue == null) return;
            string? selectedId = (list.SelectedItem as NativeMobyCatalogueEntry)?.SourceVariantId;
            var found = catalogue.Search(search.Text, (sources.SelectedItem as NativeLibrarySourceChoice)?.Key,
                view.SelectedItem is NativeMobyCatalogueView selectedView ? selectedView : NativeMobyCatalogueView.All);
            list.ItemsSource = found;
            list.SelectedItem = found.FirstOrDefault(e => e.SourceVariantId == selectedId);
            summary.Text = $"{found.Count:N0} matching entries · {found.Select(entry => entry.ActorId).Distinct().Count()} of {catalogue.ObservedClassCount} observed classes · " +
                "all retail sources + shared models · browsing does not approve imports";
            RefreshDetails();
        }
        search.TextChanged += (_, _) => RefreshList();
        sources.SelectionChanged += (_, _) => RefreshList();
        view.SelectionChanged += (_, _) => RefreshList();
        list.SelectionChanged += (_, _) => RefreshDetails();
        use.Click += (_, _) =>
        {
            if (!Current()) { RefreshDetails(); return; }
            var chosen = list.SelectedItem is NativeMobyCatalogueEntry entry
                ? FindExistingNativeCatalogueTemplate(entry, approvedTemplates) : null;
            if (chosen != null && catalogue?.DescribeTarget((NativeMobyCatalogueEntry)list.SelectedItem!, targetKey).RetailTargetInspected == true)
                dialog.Close(chosen);
        };

        Grid filters = new() { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Auto) }, ColumnSpacing = 8 };
        filters.Children.Add(search); Grid.SetColumn(sources, 1); filters.Children.Add(sources); Grid.SetColumn(view, 2); filters.Children.Add(view);
        Grid content = new() { ColumnDefinitions = { new ColumnDefinition(new GridLength(3, GridUnitType.Star)), new ColumnDefinition(new GridLength(2, GridUnitType.Star)) }, ColumnSpacing = 14, Height = 455 };
        content.Children.Add(list);
        Border detailPanel = new()
        {
            Padding = new Thickness(12), Background = new SolidColorBrush(ModernSurface),
            Child = new ScrollViewer { Content = new StackPanel
            {
                Spacing = 12, Margin = new Thickness(0, 0, 12, 0), Children = { new Expander
                {
                    Name = "NativeMobyLibraryDependencyPanel", Header = "Dependencies and existing evidence",
                    HorizontalAlignment = HorizontalAlignment.Stretch, Content = dependencies
                }, details }
            } }
        };
        Grid.SetColumn(detailPanel, 1); content.Children.Add(detailPanel);
        StackPanel body = new()
        {
            Margin = new Thickness(18), Spacing = 12,
            Children = { BuildModernDialogHeader("Native Moby Library", $"Browse exact retail source variants. Destination: {targetName}."), filters, summary, content }
        };
        dialog.Content = BuildModernDialogFrame(body, cancel, use);
        var result = dialog.ShowDialog<AddMobyTemplate?>(owner);
        try
        {
            var capturedCatalogue = await Task.Run(() => new NativeMobyCatalogue(NativeMobySourceInventory.Capture(sourcePath, catalog)));
            if (!closed)
            {
                if (!Current()) details.Text = summary.Text = "Source or destination changed during capture. Close and reopen the library; no state was changed.";
                else
                {
                    catalogue = capturedCatalogue;
                    sources.ItemsSource = new[] { new NativeLibrarySourceChoice("", "All sources"),
                            new NativeLibrarySourceChoice(NativeMobyCatalogue.SharedSourceKey, "Shared PETE models") }
                        .Concat(catalogue.Levels.Select(l => new NativeLibrarySourceChoice(l.LevelKey, l.DisplayName))).ToArray();
                    sources.SelectedIndex = 0; sources.IsEnabled = true;
                    RefreshList();
                }
            }
        }
        catch (Exception error) when (error is IOException or InvalidDataException or ArgumentException or UnauthorizedAccessException)
        {
            if (!closed) details.Text = summary.Text = $"Native library unavailable: {error.Message}\nUse the original USA source BIN/CUE. No scene or saved file was changed.";
        }
        return await result;
    }
    private sealed record NativeLibrarySourceChoice(string Key, string Label)
    { public override string ToString() => Label; }
}
