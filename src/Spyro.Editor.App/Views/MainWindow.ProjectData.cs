using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Spyro.Editor.Core.Diagnostics;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

namespace Spyro.Editor.App.Views;

public sealed partial class MainWindow
{
    private Border? _previousBetaProjectReminder;

    private Control BuildPreviousBetaProjectReminder()
    {
        TextBlock message = new()
        {
            Text = "Already used Spyro Editor Beta V1? Import that old beta folder once so its saved edits and custom assets are copied into protected project storage.",
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Color.FromRgb(72, 55, 20)),
            FontWeight = FontWeight.SemiBold
        };
        Button import = NewButton("Import Beta V1 Project");
        import.Click += async (_, _) =>
        {
            if (_previousBetaProjectReminder != null)
                _previousBetaProjectReminder.IsVisible = false;
            await ShowProjectDataAsync();
        };
        Button dismiss = NewButton("Not Needed");
        dismiss.Click += async (_, _) =>
        {
            if (_previousBetaProjectReminder != null)
                _previousBetaProjectReminder.IsVisible = false;
            await MarkPreviousBetaProjectReminderHandledAsync();
        };
        Grid content = new()
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 8
        };
        content.Children.Add(message);
        Grid.SetColumn(import, 1);
        content.Children.Add(import);
        Grid.SetColumn(dismiss, 2);
        content.Children.Add(dismiss);
        _previousBetaProjectReminder = new Border
        {
            IsVisible = false,
            Background = new SolidColorBrush(Color.FromRgb(255, 247, 224)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(224, 195, 119)),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(14, 8),
            Child = content
        };
        return _previousBetaProjectReminder;
    }

    internal void ShowPreviousBetaProjectReminderForTesting()
    {
        if (_previousBetaProjectReminder == null)
            throw new InvalidOperationException("The previous-beta project reminder UI is not initialized.");
        _previousBetaProjectReminder.IsVisible = true;
    }

    private void BeginPreviousBetaProjectReminder()
    {
        // Keep the V2 reminder marker and behavior for every later release so
        // a Beta V1 user who skips directly to V3 (or an incremental release)
        // can still copy their old project into protected storage. Users who
        // already handled the reminder in V2 retain the same marker and will
        // not see it again.
        if (!_releaseMode || AppReleaseIdentity.BetaVersion.Major < 2 || ReleaseProjectBootstrap.Current == null)
            return;
        _ = ShowPreviousBetaProjectReminderOnceAsync();
    }

    private async Task ShowPreviousBetaProjectReminderOnceAsync()
    {
        ReleaseProjectContext? context = ReleaseProjectBootstrap.Current;
        if (context == null || _previousBetaProjectReminder == null)
            return;
        string markerPath = PreviousBetaProjectReminderMarkerPath(context);
        if (File.Exists(markerPath))
            return;
        if (context.AutomaticMigration != null)
        {
            await MarkPreviousBetaProjectReminderHandledAsync();
            return;
        }
        for (int attempt = 0; attempt < 300 && (!IsVisible || _loadingLevel); attempt++)
            await Task.Delay(100);
        if (!IsVisible)
            return;
        _previousBetaProjectReminder.IsVisible = true;
    }

    private static string PreviousBetaProjectReminderMarkerPath(ReleaseProjectContext context) =>
        Path.Combine(context.UserData.SettingsPath, "beta-v2-project-reminder.json");

    private static async Task MarkPreviousBetaProjectReminderHandledAsync()
    {
        ReleaseProjectContext? context = ReleaseProjectBootstrap.Current;
        if (context == null)
            return;
        string markerPath = PreviousBetaProjectReminderMarkerPath(context);
        if (File.Exists(markerPath))
            return;
        string temporaryPath = $"{markerPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            Directory.CreateDirectory(context.UserData.SettingsPath);
            await File.WriteAllTextAsync(temporaryPath, "{\n  \"version\": 1,\n  \"shownFor\": \"Spyro Editor Beta V2\"\n}\n");
            File.Move(temporaryPath, markerPath, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            EditorDiagnostics.RecordWarning("Could not remember the Beta V1 import reminder", exception.Message);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private async Task ShowProjectDataAsync()
    {
        ReleaseProjectContext? context = ReleaseProjectBootstrap.Current;
        if (!_releaseMode || context == null)
        {
            _statusText.Text = "External project storage is active in packaged release builds.";
            return;
        }

        Window dialog = new()
        {
            Title = "Project Data",
            Width = 680,
            Height = 500,
            MinWidth = 560,
            MinHeight = 420,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = Brushes.White
        };
        StackPanel body = new()
        {
            Margin = new Thickness(24),
            Spacing = 14
        };
        body.Children.Add(new TextBlock
        {
            Text = "Protected project storage",
            FontSize = 22,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(ModernInk)
        });
        body.Children.Add(new TextBlock
        {
            Text = "Your edits and generated files now live outside the replaceable application folder. Installing or updating the editor does not overwrite this project.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(ModernMutedInk),
            LineHeight = 20
        });
        body.Children.Add(new TextBlock
        {
            Text = "Current project",
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(ModernInk)
        });
        body.Children.Add(new TextBox
        {
            Text = context.Project.RootPath,
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 58
        });

        StackPanel pathActions = new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };
        pathActions.Children.Add(NewButton("Open Project Folder", () => OpenPath(context.Project.RootPath)));
        pathActions.Children.Add(NewButton("Open Backups / Updates", () => OpenPath(context.UserData.RootPath)));
        body.Children.Add(pathActions);

        body.Children.Add(new Border
        {
            BorderBrush = new SolidColorBrush(ModernLine),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Margin = new Thickness(0, 4, 0, 0)
        });
        body.Children.Add(new TextBlock
        {
            Text = "Import a previous portable beta",
            FontSize = 17,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(ModernInk)
        });
        body.Children.Add(new TextBlock
        {
            Text = "Choose the old SpyroEditor-beta folder. Saved edits, custom skies/textures, settings, and research files are copied into this project. The old folder is never moved or deleted, and existing project files are never overwritten.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(ModernMutedInk),
            LineHeight = 20
        });
        CheckBox includeOutputs = new()
        {
            Content = "Also copy generated output BIN/CUE files (this can be several GB)",
            IsChecked = false
        };
        body.Children.Add(includeOutputs);
        TextBlock resultText = new()
        {
            Text = "No previous beta imported in this session.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(ModernMutedInk)
        };
        Button import = NewButton("Choose Previous Beta Folder...");
        import.Click += async (_, _) =>
        {
            if (HasUnsavedTerrainEdits() || HasUnsavedMobyEdits())
            {
                resultText.Text = "Save the current level before importing another beta project.";
                return;
            }

            IReadOnlyList<IStorageFolder> folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Choose the previous Spyro Editor beta folder",
                AllowMultiple = false
            });
            string sourceRoot = folders.FirstOrDefault()?.Path.LocalPath ?? "";
            if (string.IsNullOrWhiteSpace(sourceRoot))
                return;
            if (string.Equals(Path.GetFullPath(sourceRoot), Path.GetFullPath(context.Project.RootPath), StringComparison.OrdinalIgnoreCase))
            {
                resultText.Text = "Choose the old application folder, not the current protected project folder.";
                return;
            }

            import.IsEnabled = false;
            resultText.Text = "Copying project data. Large generated outputs may take several minutes...";
            try
            {
                PortableProjectMigrationResult result = await ReleaseProjectBootstrap.ImportPortableProjectAsync(
                    sourceRoot,
                    includeGeneratedOutputs: includeOutputs.IsChecked == true);
                await MarkPreviousBetaProjectReminderHandledAsync();
                if (_previousBetaProjectReminder != null)
                    _previousBetaProjectReminder.IsVisible = false;
                string conflictNote = result.ConflictCount == 0
                    ? "No files were overwritten."
                    : $" {result.ConflictCount} differing existing file(s) were preserved under _migration/conflicts.";
                resultText.Text =
                    $"Imported {result.CopiedCount} file(s), kept {result.ExistingCount} identical file(s), and skipped {result.SkippedCount} application/cache/game file(s)." +
                    conflictNote + $" Migration report: {result.ReportPath}";
                _nativeSkyReport = null;
                _nativeSkyReportSourcePath = "";
                string sourceImagePath = DiscImageLocator.FindImage(_workspace);
                _skyboxDiscImagePathBox.Text = sourceImagePath;
                _skyboxWadAnalysisPathBox.Text = WadAnalysisLocator.Find(_workspace);
                _discImagePathBox.Text = sourceImagePath;
                RefreshSourceDiscStatus();
                LoadSavedExeStringPlan();
                LoadLevels();
                RefreshDiagnosticContext(includeSavedEdits: true);
                EditorDiagnostics.RecordAction("Previous beta project imported", result.ReportPath);
            }
            catch (Exception exception)
            {
                resultText.Text = $"Could not import that beta folder: {exception.Message}";
                EditorDiagnostics.RecordException("importing a previous beta project", exception);
            }
            finally
            {
                import.IsEnabled = true;
            }
        };
        body.Children.Add(import);
        body.Children.Add(resultText);

        Button close = NewButton("Close", () => dialog.Close());
        close.HorizontalAlignment = HorizontalAlignment.Right;
        body.Children.Add(close);
        dialog.Content = new ScrollViewer { Content = body };
        await dialog.ShowDialog(this);
    }
}
