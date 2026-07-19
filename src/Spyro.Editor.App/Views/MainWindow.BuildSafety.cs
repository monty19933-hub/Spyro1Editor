using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Spyro.Editor.Core.Diagnostics;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.App.Views;

public sealed partial class MainWindow
{
    private readonly TextBlock _buildSafetySummaryText = new()
    {
        Text = "Not inspected yet",
        TextWrapping = TextWrapping.Wrap,
        FontSize = 12,
        LineHeight = 17
    };
    private Button? _inspectBuildSafetyButton;
    private MobyBuildSafetyInspectionResult? _lastBuildSafetyInspection;

    private Control BuildModernBuildSafetyPanel()
    {
        StackPanel panel = new() { Spacing = 8 };
        panel.Children.Add(ModernSectionHeading("Build safety"));

        _buildSafetySummaryText.Foreground = new SolidColorBrush(ModernMutedInk);
        panel.Children.Add(new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(244, 247, 249)),
            BorderBrush = new SolidColorBrush(ModernLine),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(9, 7),
            Child = _buildSafetySummaryText
        });

        _inspectBuildSafetyButton = NewAsyncButton("Inspect Build Safety", ShowBuildSafetyInspectorAsync);
        StyleModernPrimaryButton(_inspectBuildSafetyButton, ModernTeal);
        ToolTip.SetTip(_inspectBuildSafetyButton, "Inspect saved object edits against the native static and dynamic Moby limits.");
        panel.Children.Add(_inspectBuildSafetyButton);
        return panel;
    }

    private async Task ShowBuildSafetyInspectorAsync()
    {
        string sourceImage = FirstExistingDiscImagePath(
            _discImagePathBox.Text,
            _skyboxDiscImagePathBox.Text,
            DiscImageLocator.FindImage(_workspace));
        if (!File.Exists(sourceImage))
        {
            _statusText.Text = "Choose the original Spyro BIN/CUE before inspecting build safety.";
            return;
        }

        await SaveCurrentEditsAsync();
        IReadOnlyList<EditedLevelExportTarget> targets = FindEditedLevelExportTargets();
        MobyBuildSafetyInspectionResult inspection = await InspectSavedObjectBuildSafetyAsync(sourceImage, targets);
        await ShowBuildSafetyDialogAsync(inspection, allowCreateAnyway: false);
    }

    private async Task<MobyBuildSafetyInspectionResult> InspectSavedObjectBuildSafetyAsync(
        string sourceImage,
        IReadOnlyList<EditedLevelExportTarget> targets)
    {
        string outputDirectory = EnsureUserOutputDirectory();
        string sourceCue = DiscImageLocator.FindCueForImage(sourceImage);
        List<MobyBuildSafetyInput> inputs = [];
        foreach (EditedLevelExportTarget target in targets.Where(target => target.HasObjectEdits))
        {
            string editsPath = Path.Combine(_workspace.RootPath, $"{target.Level.Key}-native-edits.json");
            string planPrefix = Path.Combine(outputDirectory, "_combined-build", $"safety-{target.Level.Key}");
            try
            {
                MobySourcePatchPlan plan = await Task.Run(() => MobySourcePatchExporter.BuildPlan(
                    sourceImage,
                    sourceCue,
                    $"{planPrefix}.bin",
                    $"{planPrefix}.cue",
                    target.Level,
                    editsPath));
                inputs.Add(new MobyBuildSafetyInput(target.Level, plan));
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or OverflowException)
            {
                inputs.Add(new MobyBuildSafetyInput(
                    target.Level,
                    CreateEmptyBuildSafetyPlan(sourceImage, sourceCue, target.Level, editsPath, planPrefix),
                    ex.Message));
            }
        }

        MobyBuildSafetyReport report = await Task.Run(() => MobyBuildSafetyInspector.Inspect(sourceImage, inputs));
        string reportPrefix = Path.Combine(outputDirectory, "Spyro Editor - All Saved Edits.build-safety");
        MobyBuildSafetyWriteResult written = await MobyBuildSafetyReportWriter.WriteAsync(
            report,
            $"{reportPrefix}.json",
            $"{reportPrefix}.md");
        MobyBuildSafetyInspectionResult result = new(report, written);
        _lastBuildSafetyInspection = result;
        RefreshBuildSafetySummary(result);
        EditorDiagnostics.RecordAction(
            "Build safety inspected",
            $"Status: {report.StatusLabel}; levels: {report.Levels.Count}; true appends: {report.TrueAppendCount}; runtime slots consumed: {report.RuntimeSlotsConsumed}; skipped edits: {report.SkippedEditCount}; report: {written.JsonPath}");
        return result;
    }

    private static MobySourcePatchPlan CreateEmptyBuildSafetyPlan(
        string sourceImage,
        string sourceCue,
        Spyro.Editor.Core.Levels.LevelDefinition level,
        string editsPath,
        string outputPrefix)
    {
        return new MobySourcePatchPlan(
            GeneratedAt: DateTimeOffset.Now,
            SourceImagePath: sourceImage,
            OutputImagePath: $"{outputPrefix}.bin",
            OutputCuePath: $"{outputPrefix}.cue",
            NativeEditsPath: editsPath,
            LevelKey: level.Key,
            LevelName: level.DisplayName,
            SourceTableWadOffset: level.SourceTableWadOffset,
            SourceRecordCount: level.SourceRecordCount,
            RecordStride: MobyBuildSafetyInspector.MobyRecordStride,
            PatchCount: 0,
            TotalPatchedBytes: 0,
            Patches: [],
            PackageImportPreviews: [],
            SkippedEdits: [],
            Notes: [$"Could not create the normal object patch plan. Source CUE: {Path.GetFileName(sourceCue)}"]);
    }

    private void RefreshBuildSafetySummary(MobyBuildSafetyInspectionResult inspection)
    {
        MobyBuildSafetyReport report = inspection.Report;
        _buildSafetySummaryText.Text = report.Levels.Count == 0
            ? $"{report.StatusLabel}: no saved object edits"
            : $"{report.StatusLabel}: {report.Levels.Count} level(s), {report.TrueAppendCount} true append(s), {report.RuntimeSlotsConsumed} runtime slot(s) consumed, {report.SkippedEditCount} skipped edit(s)";
        _buildSafetySummaryText.Foreground = BuildSafetyBrush(report.Status);
    }

    private void InvalidateBuildSafetySummary()
    {
        if (_lastBuildSafetyInspection == null)
            return;

        _lastBuildSafetyInspection = null;
        _buildSafetySummaryText.Text = "Object edits changed; inspect again before Create BIN";
        _buildSafetySummaryText.Foreground = new SolidColorBrush(ModernMutedInk);
    }

    private async Task<BuildSafetyDecision> ConfirmBuildSafetyAsync(MobyBuildSafetyInspectionResult inspection)
    {
        if (inspection.Report.Status == MobyBuildSafetyStatus.Stable)
            return BuildSafetyDecision.Create;
        return await ShowBuildSafetyDialogAsync(inspection, allowCreateAnyway: true);
    }

    private async Task<BuildSafetyDecision> ShowBuildSafetyDialogAsync(
        MobyBuildSafetyInspectionResult inspection,
        bool allowCreateAnyway)
    {
        Window dialog = new()
        {
            Title = "Build Safety",
            Width = 760,
            Height = 660,
            MinWidth = 620,
            MinHeight = 500,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = true
        };
        dialog.Content = BuildBuildSafetyDialogContent(dialog, inspection, allowCreateAnyway);
        return await dialog.ShowDialog<BuildSafetyDecision>(this);
    }

    private Control BuildBuildSafetyDialogContent(
        Window dialog,
        MobyBuildSafetyInspectionResult inspection,
        bool allowCreateAnyway)
    {
        MobyBuildSafetyReport report = inspection.Report;
        StackPanel body = new() { Spacing = 12, Margin = new Thickness(18) };
        body.Children.Add(new TextBlock
        {
            Text = $"Build Safety: {report.StatusLabel}",
            FontSize = 20,
            FontWeight = FontWeight.SemiBold,
            Foreground = BuildSafetyBrush(report.Status)
        });
        body.Children.Add(new TextBlock
        {
            Text = report.Levels.Count == 0
                ? "No saved object edits are included in this build."
                : $"{report.Levels.Count} edited level(s); {report.TrueAppendCount} true source-row append(s); {report.RuntimeSlotsConsumed} projected runtime slot(s) consumed; {report.SkippedEditCount} skipped edit(s).",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(ModernMutedInk)
        });

        foreach (MobyBuildSafetyLevelReport level in report.Levels)
            body.Children.Add(BuildBuildSafetyLevelRow(dialog, level));

        body.Children.Add(new TextBlock
        {
            Text = $"JSON: {inspection.Written.JsonPath}{Environment.NewLine}Markdown: {inspection.Written.MarkdownPath}",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 11,
            Foreground = new SolidColorBrush(ModernMutedInk)
        });

        WrapPanel actions = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        Button openFolder = NewButton("Open Report Folder", () =>
        {
            string? folder = Path.GetDirectoryName(inspection.Written.JsonPath);
            if (!string.IsNullOrWhiteSpace(folder))
                OpenPath(folder);
        });
        actions.Children.Add(openFolder);

        Button close = NewButton(report.Status == MobyBuildSafetyStatus.Blocked ? "Return to Editor" : "Close");
        close.Click += (_, _) => dialog.Close(BuildSafetyDecision.Cancel);
        actions.Children.Add(close);
        if (allowCreateAnyway && report.Status == MobyBuildSafetyStatus.Review)
        {
            Button create = NewButton("Create BIN Anyway");
            create.Background = new SolidColorBrush(ModernBlue);
            create.Foreground = Brushes.White;
            create.Click += (_, _) => dialog.Close(BuildSafetyDecision.Create);
            actions.Children.Add(create);
        }
        body.Children.Add(actions);

        return new ScrollViewer
        {
            Content = body,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
    }

    private Control BuildBuildSafetyLevelRow(Window dialog, MobyBuildSafetyLevelReport level)
    {
        StackPanel content = new() { Spacing = 6 };
        content.Children.Add(new TextBlock
        {
            Text = $"{level.LevelName}  |  {level.StatusLabel}",
            FontWeight = FontWeight.SemiBold,
            FontSize = 14,
            Foreground = BuildSafetyBrush(level.Status)
        });
        content.Children.Add(new TextBlock
        {
            Text = level.ComponentByteLength > 0
                ? $"Static rows {level.SourceRuntimeRecordCount} -> {level.PlannedRuntimeRecordCount}    Runtime capacity {level.SourceDynamicCapacity} -> {level.ProjectedDynamicCapacity}    Persistent headroom {level.PersistentIndexHeadroom}"
                : "Native Level Moby component unresolved",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Foreground = new SolidColorBrush(ModernInk)
        });
        content.Children.Add(new TextBlock
        {
            Text = $"True appends {level.TrueAppendCount}    Existing-slot reuse {level.SourceSlotReuseCount}    Skipped edits {level.SkippedEditCount}    Row alignment {level.RuntimeRowAlignmentDeltaBytes:+#;-#;0} bytes",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 11,
            Foreground = new SolidColorBrush(ModernMutedInk)
        });
        if (level.Issues.Count > 0)
        {
            content.Children.Add(new TextBlock
            {
                Text = level.Issues.Any(issue => issue.CanNavigate)
                    ? "Issues — double-click a Moby to locate it"
                    : "Issues",
                Margin = new Thickness(0, 5, 0, 1),
                FontWeight = FontWeight.SemiBold,
                FontSize = 12,
                Foreground = new SolidColorBrush(ModernInk)
            });
            foreach (MobyBuildSafetyIssue issue in level.Issues)
                content.Children.Add(BuildBuildSafetyIssueRow(dialog, issue));
        }

        content.Children.Add(new TextBlock
        {
            Text = "Technical findings",
            Margin = new Thickness(0, 5, 0, 0),
            FontWeight = FontWeight.SemiBold,
            FontSize = 11,
            Foreground = new SolidColorBrush(ModernMutedInk)
        });
        foreach (string finding in level.Findings)
        {
            content.Children.Add(new TextBlock
            {
                Text = $"- {finding}",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 11,
                Foreground = new SolidColorBrush(ModernMutedInk)
            });
        }

        return new Border
        {
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(ModernLine),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(11),
            Child = content
        };
    }

    private Control BuildBuildSafetyIssueRow(Window dialog, MobyBuildSafetyIssue issue)
    {
        TextBlock target = new()
        {
            Text = issue.TargetLabel,
            FontWeight = FontWeight.SemiBold,
            FontSize = 12,
            Foreground = BuildSafetyBrush(issue.Status),
            TextWrapping = TextWrapping.Wrap
        };
        TextBlock message = new()
        {
            Text = issue.Message,
            FontSize = 11,
            Foreground = new SolidColorBrush(ModernInk),
            TextWrapping = TextWrapping.Wrap
        };
        StackPanel text = new() { Spacing = 2 };
        text.Children.Add(target);
        text.Children.Add(message);
        if (issue.CanNavigate)
        {
            text.Children.Add(new TextBlock
            {
                Text = "Double-click to select and center this Moby",
                FontSize = 10,
                Foreground = new SolidColorBrush(ModernTeal)
            });
        }

        Border row = new()
        {
            DataContext = issue,
            Background = new SolidColorBrush(issue.Status == MobyBuildSafetyStatus.Blocked
                ? Color.FromRgb(255, 244, 244)
                : Color.FromRgb(255, 249, 238)),
            BorderBrush = new SolidColorBrush(issue.Status == MobyBuildSafetyStatus.Blocked
                ? Color.FromRgb(231, 184, 184)
                : Color.FromRgb(232, 207, 164)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 6),
            Cursor = issue.CanNavigate ? new Cursor(StandardCursorType.Hand) : null,
            Child = text
        };
        row.Classes.Add("build-safety-issue");
        if (issue.CanNavigate)
        {
            row.DoubleTapped += (_, args) =>
            {
                args.Handled = true;
                ActivateBuildSafetyIssue(dialog, issue);
            };
        }

        return row;
    }

    private void ActivateBuildSafetyIssue(Window dialog, MobyBuildSafetyIssue issue)
    {
        dialog.Close(BuildSafetyDecision.Cancel);
        Dispatcher.UIThread.Post(() => _ = NavigateToBuildSafetyIssueAsync(issue));
    }

    private async Task NavigateToBuildSafetyIssueAsync(MobyBuildSafetyIssue issue)
    {
        if (!issue.CanNavigate || issue.EditorTrueIndex is not int trueIndex)
            return;

        try
        {
            LevelDefinition? level = _catalog.FindByKey(issue.LevelKey);
            if (level == null)
            {
                _statusText.Text = $"Could not find level {issue.LevelKey} for {issue.TargetLabel}.";
                return;
            }

            if (!string.Equals(_currentLevel?.Key, level.Key, StringComparison.OrdinalIgnoreCase))
            {
                await SelectLevelFromPickerAsync(level);
                if (!string.Equals(_currentLevel?.Key, level.Key, StringComparison.OrdinalIgnoreCase))
                {
                    _statusText.Text = $"Build Safety navigation to {level.DisplayName} was canceled.";
                    return;
                }
            }

            Moby? moby = _currentMobys.FirstOrDefault(candidate =>
                candidate.TrueIndex == trueIndex && !candidate.IsEditorControl);
            if (moby == null)
            {
                _statusText.Text = $"Loaded {level.DisplayName}, but {issue.TargetLabel} was not found.";
                return;
            }
            if (moby.IsRemoved)
            {
                _statusText.Text = $"Loaded {level.DisplayName}, but {issue.TargetLabel} is currently removed and cannot be centered.";
                return;
            }

            if (_modernWorkspaceTabs != null)
                _modernWorkspaceTabs.SelectedIndex = 0;
            _mobySearchBox.Text = "";
            SelectMobyCategory("All objects");
            RefreshMobyList(moby);
            _viewport.SelectMoby(moby, true);
            _mobyList.ScrollIntoView(moby);
            _mobyList.Focus();
            _statusText.Text = $"Build Safety: selected and centered {level.DisplayName} T{moby.TrueIndex} {moby.DisplayLabel}.";
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException)
        {
            EditorDiagnostics.RecordException("navigating from Build Safety", ex);
            _statusText.Text = $"Could not locate {issue.TargetLabel}: {ex.Message}";
        }
    }

    private static IBrush BuildSafetyBrush(MobyBuildSafetyStatus status) => status switch
    {
        MobyBuildSafetyStatus.Stable => new SolidColorBrush(ModernGreen),
        MobyBuildSafetyStatus.Review => new SolidColorBrush(Color.FromRgb(173, 99, 15)),
        MobyBuildSafetyStatus.Blocked => new SolidColorBrush(ModernRed),
        _ => new SolidColorBrush(ModernInk)
    };

    private sealed record MobyBuildSafetyInspectionResult(
        MobyBuildSafetyReport Report,
        MobyBuildSafetyWriteResult Written);

    private enum BuildSafetyDecision
    {
        Cancel,
        Create
    }
}
