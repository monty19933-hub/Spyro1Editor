using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.App.Views;

public sealed partial class MainWindow
{
    private static readonly HashSet<string> SpecialChestFlightLevelKeys =
    [
        "sunnyflight",
        "nightflight",
        "crystalflight",
        "wildflight",
        "icyflight"
    ];
    private List<Moby> _lastRemovedMobyBundle = [];

    private void AttachSpecialChestBundleMetadata(
        AddMobyTemplate selectedTemplate,
        List<Moby> addedMobys,
        Moby selectedRoot)
    {
        if (_currentLevel == null)
            return;

        int selectedActorId = ((selectedTemplate.SourceByte37 & 0xFF) << 8) |
            (selectedTemplate.SourceByte36 & 0xFF);
        if (!SpecialChestEditorTemplateGate.TryMapFamily(selectedTemplate.Family, out SpecialChestFamily family) &&
            !TryResolveSavedSpecialChestFamily("", selectedActorId, out family))
            return;

        SpecialChestBundleProfile? profile = SpecialChestBundleProfileRegistry.Find(
            family,
            _currentLevel.Key,
            SpecialChestBundleProfileRegistry.CleanUsaImageSha256);
        if (profile == null)
            return;

        List<Moby> members = addedMobys.ToList();
        if (family == SpecialChestFamily.LockedChest)
        {
            bool addsLockedChest = members.Any(member => NativeActorId(member) == 0x00AE);
            bool addsKey = members.Any(member => NativeActorId(member) == 0x00AD);
            if (addsLockedChest && !addsKey)
            {
                Moby? existingKey = _currentMobys.FirstOrDefault(member =>
                    member.IsAdded &&
                    !member.IsRemoved &&
                    NativeActorId(member) == 0x00AD &&
                    (string.IsNullOrWhiteSpace(member.SpecialChestBundleId) ||
                     !_currentMobys.Any(other =>
                         !other.IsRemoved &&
                         NativeActorId(other) == 0x00AE &&
                         string.Equals(other.SpecialChestBundleId, member.SpecialChestBundleId, StringComparison.Ordinal))));
                if (existingKey != null)
                    members.Insert(0, existingKey);
            }
            else if (addsKey && !addsLockedChest)
            {
                Moby? existingChest = _currentMobys.FirstOrDefault(member =>
                    member.IsAdded &&
                    !member.IsRemoved &&
                    NativeActorId(member) == 0x00AE &&
                    (string.IsNullOrWhiteSpace(member.SpecialChestBundleId) ||
                     !_currentMobys.Any(other =>
                         !other.IsRemoved &&
                         NativeActorId(other) == 0x00AD &&
                         string.Equals(other.SpecialChestBundleId, member.SpecialChestBundleId, StringComparison.Ordinal))));
                if (existingChest != null)
                    members.Add(existingChest);
            }
        }

        ushort visibleActor = SpecialChestBundleProfileRegistry.Definition(family).RootActorId;
        Moby visibleRoot = members.FirstOrDefault(member => NativeActorId(member) == visibleActor) ?? selectedRoot;
        string existingBundleId = members
            .Select(member => member.SpecialChestBundleId)
            .FirstOrDefault(id => !string.IsNullOrWhiteSpace(id)) ?? "";
        string bundleId = string.IsNullOrWhiteSpace(existingBundleId)
            ? $"special-chest:{family.ToString().ToLowerInvariant()}:{LevelCatalog.NormalizeKey(_currentLevel.Key)}:T{visibleRoot.TrueIndex}"
            : existingBundleId;
        int[] hiddenReferences = members
            .Where(member => !ReferenceEquals(member, visibleRoot))
            .Select(member => member.TrueIndex)
            .Where(trueIndex => trueIndex >= 0)
            .Distinct()
            .Order()
            .ToArray();

        foreach (Moby member in members)
        {
            member.SpecialChestBundleId = bundleId;
            member.SpecialChestProfileId = profile.Id;
            member.SpecialChestVisibleRootTrueIndex = visibleRoot.TrueIndex;
            member.SpecialChestHiddenCompanionTrueIndexes.Clear();
            member.SpecialChestHiddenCompanionTrueIndexes.AddRange(hiddenReferences);
            member.SpecialChestCapacity = profile.StructuralBudget.MaxBundleInstances ?? 0;
            member.SpecialChestEvidenceStatus = profile.Evidence.ToString();
        }
    }

    private IEnumerable<Moby> GetAtomicSpecialChestBundleMembers(Moby member)
    {
        if (string.IsNullOrWhiteSpace(member.SpecialChestBundleId))
            return [member];

        return _currentMobys.Where(candidate =>
            string.Equals(candidate.SpecialChestBundleId, member.SpecialChestBundleId, StringComparison.Ordinal));
    }

    private static int NativeActorId(Moby moby) =>
        ((moby.SourceByte37 & 0xFF) << 8) | (moby.SourceByte36 & 0xFF);

    private Control BuildSpecialChestAvailabilityControl()
    {
        StackPanel panel = new() { Spacing = 6 };
        panel.Children.Add(new TextBlock
        {
            Text = "Special chests use checked per-level profiles. Normal Add/Create BIN shows only runtime-proven imports; candidates stay disposable-test-only, and all five flight stages are blocked.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 11,
            Foreground = new SolidColorBrush(ModernMutedInk)
        });
        Button inspect = NewAsyncButton("Special Chest Support", ShowSpecialChestAvailabilityAsync);
        ToolTip.SetTip(inspect, "Show native, runtime-proven, test-only, and blocked chest-family evidence for the loaded level.");
        panel.Children.Add(inspect);
        return panel;
    }

    private async Task ShowSpecialChestAvailabilityAsync()
    {
        if (_currentLevel == null)
        {
            _statusText.Text = "Load a level before checking special-chest support.";
            return;
        }

        string levelKey = LevelCatalog.NormalizeKey(_currentLevel.Key);
        bool flight = SpecialChestFlightLevelKeys.Contains(levelKey);
        Window dialog = new()
        {
            Title = $"Special Chest Support — {_currentLevel.DisplayName}",
            Width = 760,
            Height = 680,
            MinWidth = 640,
            MinHeight = 520,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = true
        };

        StackPanel body = new() { Spacing = 10, Margin = new Thickness(18) };
        body.Children.Add(new TextBlock
        {
            Text = flight
                ? "Blocked — flight stage"
                : $"{_currentLevel.DisplayName}: six special-chest family profiles",
            FontSize = 20,
            FontWeight = FontWeight.SemiBold,
            Foreground = flight ? new SolidColorBrush(ModernRed) : new SolidColorBrush(ModernInk)
        });
        body.Children.Add(new TextBlock
        {
            Text = flight
                ? "Special chests are explicitly excluded from Sunny Flight, Night Flight, Crystal Flight, Wild Flight, and Icy Flight. They cannot be added or exported there."
                : "Native presence proves the retail dependency closure exists, but does not automatically prove a safe extra copy. Only a destination profile with recorded DuckStation evidence is available to normal Add/Create BIN.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(ModernMutedInk)
        });

        foreach (SpecialChestFamilyDefinition family in SpecialChestBundleProfileRegistry.Families)
        {
            SpecialChestBundleProfile? profile = flight
                ? null
                : SpecialChestBundleProfileRegistry.Find(
                    family.Family,
                    levelKey,
                    SpecialChestBundleProfileRegistry.CleanUsaImageSha256);
            body.Children.Add(BuildSpecialChestProfileRow(family, profile, flight));
        }

        Button close = NewButton("Close");
        close.HorizontalAlignment = HorizontalAlignment.Right;
        close.Click += (_, _) => dialog.Close();
        body.Children.Add(close);
        dialog.Content = new ScrollViewer
        {
            Content = body,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        await dialog.ShowDialog(this);
    }

    private Control BuildSpecialChestProfileRow(
        SpecialChestFamilyDefinition family,
        SpecialChestBundleProfile? profile,
        bool flight)
    {
        string status;
        string evidence;
        string work = "";
        Color statusColor;
        if (flight)
        {
            status = "Blocked in flight stages";
            evidence = "No flight-stage structural profile exists.";
            statusColor = ModernRed;
        }
        else if (profile == null)
        {
            status = "Blocked — no checked profile";
            evidence = "The current level/disc pair is not present in the checked registry.";
            statusColor = ModernRed;
        }
        else
        {
            (status, statusColor) = profile.Availability switch
            {
                SpecialChestBundleAvailability.NormalCreateBinReady => ("Runtime-proven — normal Add/Create BIN", ModernGreen),
                SpecialChestBundleAvailability.NativeClosurePresent => ("Native family — extra copy still test-only", Color.FromRgb(173, 99, 15)),
                SpecialChestBundleAvailability.CandidatePlanOnly => ("Static candidate — disposable test CUE only", Color.FromRgb(173, 99, 15)),
                _ => ("Blocked — dependency closure incomplete", ModernRed)
            };
            evidence = profile.EvidenceNote;
            if (profile.RequiredWork.Count > 0)
                work = $"Next proof gate: {string.Join(" ", profile.RequiredWork)}";
        }

        StackPanel content = new() { Spacing = 3 };
        content.Children.Add(new TextBlock
        {
            Text = family.DisplayName,
            FontSize = 14,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(ModernInk)
        });
        content.Children.Add(new TextBlock
        {
            Text = status,
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(statusColor)
        });
        content.Children.Add(new TextBlock
        {
            Text = evidence,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 11,
            Foreground = new SolidColorBrush(ModernMutedInk)
        });
        if (!string.IsNullOrWhiteSpace(work))
        {
            content.Children.Add(new TextBlock
            {
                Text = work,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 10,
                Foreground = new SolidColorBrush(ModernMutedInk)
            });
        }

        bool disposableLockedChestCandidate =
            !flight &&
            profile is
            {
                Family: SpecialChestFamily.LockedChest,
                Availability: SpecialChestBundleAvailability.CandidatePlanOnly
            } &&
            !string.Equals(profile.TargetLevelKey, "stonehill", StringComparison.Ordinal) &&
            NativeLockedChestResearchArtifactWriter.CanWrite(profile.TargetLevelKey);
        if (disposableLockedChestCandidate)
        {
            TextBlock candidateStatus = new()
            {
                Text = "Research-only: this creates an isolated BIN/CUE for DuckStation. It does not add the chest to your project or normal Create BIN.",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(143, 80, 10))
            };
            Button createCandidate = NewAsyncButton(
                "Create Disposable Key + Locked Chest Test",
                () => CreateNativeLockedChestResearchCandidateAsync(profile!, candidateStatus));
            createCandidate.HorizontalAlignment = HorizontalAlignment.Left;
            ToolTip.SetTip(
                createCandidate,
                "Use the selected clean USA retail BIN/CUE to write a research-only DuckStation test in the normal output folder.");
            content.Children.Add(candidateStatus);
            content.Children.Add(createCandidate);
        }

        return new Border
        {
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush(ModernLine),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(10, 8),
            Child = content
        };
    }

    private async Task CreateNativeLockedChestResearchCandidateAsync(
        SpecialChestBundleProfile profile,
        TextBlock candidateStatus)
    {
        string currentLevelKey = LevelCatalog.NormalizeKey(_currentLevel?.Key ?? "");
        if (_currentLevel == null ||
            !string.Equals(currentLevelKey, profile.TargetLevelKey, StringComparison.Ordinal))
        {
            SetCandidateStatus("Reload this level, then reopen Special Chest Support before creating its test.", isError: true);
            return;
        }
        if (currentLevelKey == "stonehill" || !NativeLockedChestResearchArtifactWriter.CanWrite(currentLevelKey))
        {
            SetCandidateStatus("This level does not have a guarded disposable Key + Locked Chest writer.", isError: true);
            return;
        }

        DiscImageSelection selected = DiscImageLocator.ResolveSelection(_discImagePathBox.Text?.Trim() ?? "");
        if (!selected.ImageExists || !selected.CueExists)
        {
            SetCandidateStatus("Choose the clean Spyro the Dragon USA BIN/CUE first; both files must be available.", isError: true);
            return;
        }

        string wadAnalysisPath = _skyboxWadAnalysisPathBox.Text?.Trim() ?? "";
        if (!File.Exists(wadAnalysisPath))
            wadAnalysisPath = WadAnalysisLocator.Find(_workspace);
        if (!File.Exists(wadAnalysisPath))
        {
            SetCandidateStatus("The selected disc has no WAD analysis yet. Reopen the clean BIN/CUE and let its cache build finish.", isError: true);
            return;
        }

        string catalogRootPath = ResolveLockedChestCandidateCatalogRoot();
        if (string.IsNullOrWhiteSpace(catalogRootPath))
        {
            SetCandidateStatus("The level catalog required by the guarded candidate writer was not found.", isError: true);
            return;
        }

        string outputDirectory = EnsureUserOutputDirectory();
        string outputPrefix = Path.Combine(
            outputDirectory,
            $"Spyro Editor - {_currentLevel.DisplayName} - Key Locked Chest - RESEARCH ONLY");
        SetCandidateStatus($"Creating the isolated {_currentLevel.DisplayName} research CUE from the selected clean retail disc...", isError: false);

        try
        {
            NativeLockedChestResearchArtifactResult result =
                await NativeLockedChestResearchArtifactWriter.WriteAsync(
                    new NativeLockedChestResearchArtifactRequest(
                        DestinationLevelKey: currentLevelKey,
                        SourceImagePath: selected.ImagePath,
                        SourceCuePath: selected.CuePath,
                        OutputPrefix: outputPrefix,
                        WadAnalysisPath: wadAnalysisPath,
                        LevelCatalogRootPath: catalogRootPath,
                        EnableFastEntry: true));

            string message =
                $"Created research-only {Path.GetFileName(result.OutputCuePath)}. This is not normal Create BIN. " +
                $"Follow {Path.GetFileName(result.OutputChecklistPath)} in DuckStation before reporting the result.";
            SetCandidateStatus(message, isError: false);
            _statusText.Text = message + OpenContainingFolderStatus(result.OutputCuePath);
        }
        catch (Exception ex)
        {
            SetCandidateStatus($"Could not create the research-only test: {ex.Message}", isError: true);
        }

        void SetCandidateStatus(string message, bool isError)
        {
            candidateStatus.Text = message;
            candidateStatus.Foreground = new SolidColorBrush(isError ? ModernRed : Color.FromRgb(143, 80, 10));
            _statusText.Text = message;
        }
    }

    private string ResolveLockedChestCandidateCatalogRoot()
    {
        string[] candidates =
        [
            _workspace.RootPath,
            Path.Combine(_workspace.RootPath, "support"),
            AppContext.BaseDirectory
        ];
        return candidates.FirstOrDefault(path => File.Exists(Path.Combine(path, "spyro-level-catalog.json"))) ?? "";
    }

    private async Task<MobyBuildSafetyReport> AddSpecialChestBuildSafetyAsync(
        MobyBuildSafetyReport report,
        IReadOnlyList<EditedLevelExportTarget> targets,
        string sourceImagePath)
    {
        List<SavedSpecialChestLevel> savedLevels = [];
        foreach (EditedLevelExportTarget target in targets.Where(candidate => candidate.HasObjectEdits))
        {
            string editsPath = Path.Combine(_workspace.RootPath, $"{target.Level.Key}-native-edits.json");
            SavedSpecialChestLevel saved = ReadSavedSpecialChestLevel(target, editsPath);
            if (saved.Edits.Count > 0)
                savedLevels.Add(saved);
        }

        if (savedLevels.Count == 0)
            return report;

        string sourceSha256 = await Task.Run(() => ComputeFileSha256(sourceImagePath));
        Dictionary<string, SavedSpecialChestLevel> savedByLevel = savedLevels.ToDictionary(
            saved => LevelCatalog.NormalizeKey(saved.Target.Level.Key),
            StringComparer.OrdinalIgnoreCase);
        List<MobyBuildSafetyLevelReport> levels = [];
        foreach (MobyBuildSafetyLevelReport level in report.Levels)
        {
            if (!savedByLevel.TryGetValue(LevelCatalog.NormalizeKey(level.LevelKey), out SavedSpecialChestLevel? saved))
            {
                levels.Add(level);
                continue;
            }

            levels.Add(AddSpecialChestIssues(level, saved, sourceSha256));
        }

        MobyBuildSafetyStatus status = levels.Count == 0
            ? report.Status
            : levels.Max(level => level.Status);
        return report with { Status = status, Levels = levels };
    }

    private static MobyBuildSafetyLevelReport AddSpecialChestIssues(
        MobyBuildSafetyLevelReport level,
        SavedSpecialChestLevel saved,
        string sourceSha256)
    {
        List<MobyBuildSafetyIssue> issues = level.Issues.ToList();
        List<string> findings = level.Findings.ToList();
        List<string> recommendations = level.Recommendations.ToList();
        MobyBuildSafetyStatus status = level.Status;
        string normalizedLevelKey = LevelCatalog.NormalizeKey(level.LevelKey);

        foreach (IGrouping<string, SavedSpecialChestEdit> duplicateBundle in saved.Edits
            .Where(edit => edit.HasExplicitBundleMetadata)
            .GroupBy(edit => edit.BundleId, StringComparer.Ordinal)
            .Where(group => group.Select(edit => edit.Family).Distinct().Count() > 1))
        {
            SavedSpecialChestEdit duplicateRoot = SelectVisibleChestRoot(duplicateBundle.ToArray());
            issues.Add(NewSpecialChestIssue(
                "special-chest-duplicate-bundle-allocation",
                MobyBuildSafetyStatus.Blocked,
                $"Bundle id '{duplicateBundle.Key}' is claimed by more than one special-chest family.",
                level,
                duplicateRoot));
            status = MobyBuildSafetyStatus.Blocked;
        }

        foreach (IGrouping<SpecialChestFamily, SavedSpecialChestEdit> familyGroup in saved.Edits.GroupBy(edit => edit.Family))
        {
            SavedSpecialChestEdit[] familyEdits = familyGroup.ToArray();
            SavedSpecialChestEdit root = SelectVisibleChestRoot(familyEdits);
            if (SpecialChestFlightLevelKeys.Contains(normalizedLevelKey))
            {
                issues.Add(NewSpecialChestIssue(
                    "special-chest-flight-stage-blocked",
                    MobyBuildSafetyStatus.Blocked,
                    "Special-chest imports are explicitly blocked in all five flight stages.",
                    level,
                    root));
                status = MobyBuildSafetyStatus.Blocked;
                continue;
            }

            SavedSpecialChestEdit[] explicitRows = familyEdits
                .Where(edit => edit.HasExplicitBundleMetadata)
                .ToArray();
            if (explicitRows.Length > 0)
            {
                if (explicitRows.Length != familyEdits.Length)
                {
                    issues.Add(NewSpecialChestIssue(
                        "special-chest-partial-bundle-metadata",
                        MobyBuildSafetyStatus.Blocked,
                        "This imported special-chest family mixes explicit bundle rows with unowned rows. Restore the complete group or remove it before Create BIN.",
                        level,
                        root));
                    status = MobyBuildSafetyStatus.Blocked;
                }

                foreach (IGrouping<string, SavedSpecialChestEdit> bundleGroup in explicitRows.GroupBy(edit => edit.BundleId, StringComparer.Ordinal))
                {
                    SavedSpecialChestEdit[] bundleRows = bundleGroup.ToArray();
                    SavedSpecialChestEdit bundleRoot = SelectVisibleChestRoot(bundleRows);
                    int[] memberIndexes = bundleRows.Select(edit => edit.TrueIndex).Where(index => index >= 0).Distinct().Order().ToArray();
                    int[] expectedHidden = memberIndexes.Where(index => index != bundleRoot.TrueIndex).ToArray();
                    bool rootMismatch = bundleRows.Count(edit => edit.IsVisibleRoot) != 1 ||
                        bundleRows.Any(edit => edit.VisibleRootTrueIndex != bundleRoot.TrueIndex);
                    bool referenceMismatch = bundleRows.Any(edit =>
                        !edit.HiddenCompanionReferences.Order().SequenceEqual(expectedHidden));
                    bool duplicateOrInvalidRow = memberIndexes.Length != bundleRows.Length ||
                        bundleRows.Any(edit => edit.TrueIndex < 0);
                    if (rootMismatch || referenceMismatch || duplicateOrInvalidRow)
                    {
                        issues.Add(NewSpecialChestIssue(
                            "special-chest-orphaned-bundle-row",
                            MobyBuildSafetyStatus.Blocked,
                            $"Bundle '{bundleGroup.Key}' does not contain one coherent visible root and the exact saved companion/reward row set.",
                            level,
                            bundleRoot));
                        status = MobyBuildSafetyStatus.Blocked;
                    }

                    if (familyGroup.Key == SpecialChestFamily.LockedChest &&
                        (bundleRows.Count(edit => edit.IsKey) != 1 || bundleRows.Count(edit => edit.IsLockedChest) != 1))
                    {
                        issues.Add(NewSpecialChestIssue(
                            "special-chest-orphaned-key-chest",
                            MobyBuildSafetyStatus.Blocked,
                            $"Bundle '{bundleGroup.Key}' must contain exactly one Key and one Locked Chest.",
                            level,
                            bundleRoot));
                        status = MobyBuildSafetyStatus.Blocked;
                    }
                }
            }

            if (familyGroup.Key == SpecialChestFamily.LockedChest)
            {
                int keyCount = familyEdits.Count(edit => edit.IsKey);
                int chestCount = familyEdits.Count(edit => edit.IsLockedChest);
                if (keyCount != chestCount || keyCount == 0)
                {
                    issues.Add(NewSpecialChestIssue(
                        "special-chest-partial-locked-bundle",
                        MobyBuildSafetyStatus.Blocked,
                        $"Key + Locked Chest is atomic, but this level has {keyCount} imported key(s) and {chestCount} imported locked chest(s). Add or remove the matching half before Create BIN.",
                        level,
                        root));
                    status = MobyBuildSafetyStatus.Blocked;
                }
            }

            if (familyGroup.Key == SpecialChestFamily.SpringChest && !familyEdits.Any(edit => edit.IsVisibleRoot))
            {
                issues.Add(NewSpecialChestIssue(
                    "special-chest-orphaned-spring-controller",
                    MobyBuildSafetyStatus.Blocked,
                    "The Spring Chest controller has no visible shell. Restore or delete the complete atomic group.",
                    level,
                    root));
                status = MobyBuildSafetyStatus.Blocked;
            }

            int bundleInstances = CountBundleInstances(familyGroup.Key, familyEdits);
            int otherObjectEdits = Math.Max(0, saved.TotalSavedObjectEdits - familyEdits.Length);
            SpecialChestStructuralPlan plan = SpecialChestStructuralPlanner.Plan(new SpecialChestStructuralPlanningRequest(
                Family: familyGroup.Key,
                TargetLevelKey: normalizedLevelKey,
                DiscImageSha256: sourceSha256,
                RequestedBundleInstances: Math.Max(1, bundleInstances),
                ExistingBundleInstances: 0,
                ExistingSourceRecordCount: level.SourceRuntimeRecordCount,
                SourceRecordLimit: MobyBuildSafetyInspector.PersistentStaticMobyCapacity,
                HasOtherObjectEdits: otherObjectEdits > 0,
                HasOtherStructuralRelocations: saved.HasOtherStructuralRelocations,
                AllowResearchCandidate: false));

            if (explicitRows.Length > 0)
            {
                string expectedProfileId = plan.Profile?.Id ?? "";
                int expectedCapacity = plan.Profile?.StructuralBudget.MaxBundleInstances ?? 0;
                string expectedEvidence = plan.Profile?.Evidence.ToString() ?? "";
                bool metadataMismatch = explicitRows.Any(edit =>
                    !string.Equals(edit.ProfileId, expectedProfileId, StringComparison.Ordinal) ||
                    edit.Capacity != expectedCapacity ||
                    !string.Equals(edit.EvidenceStatus, expectedEvidence, StringComparison.Ordinal));
                if (metadataMismatch)
                {
                    issues.Add(NewSpecialChestIssue(
                        "special-chest-stale-profile-metadata",
                        MobyBuildSafetyStatus.Blocked,
                        "The saved bundle profile, evidence status, or capacity no longer matches the checked destination/disc profile.",
                        level,
                        root));
                    status = MobyBuildSafetyStatus.Blocked;
                }
            }

            foreach (SpecialChestSafetyIssue planIssue in plan.Issues.Where(issue => issue.Severity != SpecialChestSafetySeverity.Info))
            {
                MobyBuildSafetyStatus issueStatus = planIssue.Severity == SpecialChestSafetySeverity.Block
                    ? MobyBuildSafetyStatus.Blocked
                    : MobyBuildSafetyStatus.Review;
                issues.Add(NewSpecialChestIssue(
                    $"special-chest-{planIssue.Code}",
                    issueStatus,
                    planIssue.Message,
                    level,
                    root));
                status = MaxBuildSafety(status, issueStatus);
            }

            string familyName = SpecialChestBundleProfileRegistry.Definition(familyGroup.Key).DisplayName;
            if (plan.CanWriteNormalBuild)
            {
                findings.Add($"{familyName}: checked profile '{plan.Profile!.Id}' is runtime-proven for normal Create BIN on this exact disc fingerprint.");
            }
            else
            {
                findings.Add($"{familyName}: normal Create BIN is not available for this saved imported bundle ({plan.Status}).");
                recommendations.Add($"Use a disposable exact-target test CUE for {familyName}; promote the destination profile only after the required DuckStation matrix passes.");
                if (!plan.Issues.Any(issue => issue.Severity == SpecialChestSafetySeverity.Block))
                    status = MaxBuildSafety(status, MobyBuildSafetyStatus.Review);
            }
        }

        return level with
        {
            Status = status,
            Issues = issues
                .DistinctBy(issue => (issue.Code, issue.EditorTrueIndex, issue.Message))
                .OrderByDescending(issue => issue.Status)
                .ThenBy(issue => issue.EditorTrueIndex ?? int.MaxValue)
                .ToArray(),
            Findings = findings.Distinct(StringComparer.Ordinal).ToArray(),
            Recommendations = recommendations.Distinct(StringComparer.Ordinal).ToArray()
        };
    }

    private static SavedSpecialChestLevel ReadSavedSpecialChestLevel(
        EditedLevelExportTarget target,
        string editsPath)
    {
        List<SavedSpecialChestEdit> edits = [];
        int totalEdits = 0;
        if (!File.Exists(editsPath))
            return new SavedSpecialChestLevel(target, 0, false, edits);

        try
        {
            using FileStream stream = File.OpenRead(editsPath);
            using JsonDocument document = JsonDocument.Parse(stream);
            if (!document.RootElement.TryGetProperty("edits", out JsonElement array) || array.ValueKind != JsonValueKind.Array)
                return new SavedSpecialChestLevel(target, 0, false, edits);

            foreach (JsonElement edit in array.EnumerateArray())
            {
                totalEdits++;
                if (GetBoolean(edit, "removed"))
                    continue;

                string templateFamily = "";
                if (edit.TryGetProperty("crossLevelTemplate", out JsonElement crossLevel) && crossLevel.ValueKind == JsonValueKind.Object)
                    templateFamily = GetString(crossLevel, "family");
                string bundleId = GetString(edit, "specialChestBundleId");
                string profileId = GetString(edit, "profileId");

                bool importedSpecialChest = !string.IsNullOrWhiteSpace(bundleId) ||
                    SpecialChestEditorTemplateGate.TryMapFamily(templateFamily, out _);
                // V1-V3 same-level/native additions did not carry bundle metadata.
                // Missing fields retain that native behavior; only an explicit
                // bundle or legacy cross-level family is interpreted here.
                if (!importedSpecialChest)
                    continue;

                int actorId = (GetInt32(edit, "sourceByte37EditedHex", GetInt32(edit, "sourceByte37Hex", 0)) << 8) |
                    GetInt32(edit, "sourceByte36EditedHex", GetInt32(edit, "sourceByte36Hex", 0));
                if (!TryResolveSavedSpecialChestFamily(templateFamily, actorId, out SpecialChestFamily family) &&
                    !TryResolveSavedSpecialChestProfileFamily(profileId, out family))
                    continue;

                string label = GetString(edit, "labelEdited", GetString(edit, "label", "Special Chest"));
                int trueIndex = GetInt32(edit, "trueIndex", -1);
                int visibleRootTrueIndex = GetInt32(edit, "specialChestVisibleRootTrueIndex", -1);
                int[] hiddenCompanionReferences = ReadInt32Array(edit, "hiddenCompanionReferences");
                int capacity = GetInt32(edit, "capacity", 0);
                string evidenceStatus = GetString(edit, "evidenceStatus");
                bool isKey = actorId == 0x00AD || string.Equals(templateFamily, "key", StringComparison.OrdinalIgnoreCase);
                bool isLockedChest = actorId == 0x00AE || string.Equals(templateFamily, "lockedChest", StringComparison.OrdinalIgnoreCase);
                bool isVisibleRoot = visibleRootTrueIndex >= 0
                    ? trueIndex == visibleRootTrueIndex
                    : !label.Contains("controller", StringComparison.OrdinalIgnoreCase) &&
                      (family != SpecialChestFamily.LockedChest || isLockedChest);
                edits.Add(new SavedSpecialChestEdit(
                    family,
                    trueIndex,
                    label,
                    templateFamily,
                    actorId,
                    isKey,
                    isLockedChest,
                    isVisibleRoot,
                    bundleId,
                    profileId,
                    visibleRootTrueIndex,
                    hiddenCompanionReferences,
                    capacity,
                    evidenceStatus));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // The normal Moby plan reports malformed edit files as a blocking
            // level issue; this adapter deliberately does not hide that result.
        }

        bool otherStructuralRelocations = target.HasTerrainEdits ||
            target.HasCustomTerrainTextures ||
            target.HasNativeTerrainTextureRelocations ||
            target.HasSkyboxEdit;
        return new SavedSpecialChestLevel(target, totalEdits, otherStructuralRelocations, edits);
    }

    private static bool TryResolveSavedSpecialChestFamily(
        string templateFamily,
        int actorId,
        out SpecialChestFamily family)
    {
        if (SpecialChestEditorTemplateGate.TryMapFamily(templateFamily, out family))
            return true;

        family = actorId switch
        {
            0x00AD or 0x00AE => SpecialChestFamily.LockedChest,
            0x01A5 => SpecialChestFamily.LifeChest,
            0x0149 => SpecialChestFamily.SpringChest,
            0x0138 => SpecialChestFamily.FireworkChest,
            0x0186 => SpecialChestFamily.MultiGemChest,
            0x0191 => SpecialChestFamily.ArmoredChest,
            _ => default
        };
        return actorId is 0x00AD or 0x00AE or 0x01A5 or 0x0149 or 0x0138 or 0x0186 or 0x0191;
    }

    private static bool TryResolveSavedSpecialChestProfileFamily(
        string profileId,
        out SpecialChestFamily family)
    {
        SpecialChestBundleProfile? profile = SpecialChestBundleProfileRegistry.AllProfiles.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, profileId, StringComparison.Ordinal));
        family = profile?.Family ?? default;
        return profile != null;
    }

    private static SavedSpecialChestEdit SelectVisibleChestRoot(IReadOnlyList<SavedSpecialChestEdit> edits) =>
        edits.FirstOrDefault(edit => edit.IsVisibleRoot) ?? edits[0];

    private static int CountBundleInstances(
        SpecialChestFamily family,
        IReadOnlyList<SavedSpecialChestEdit> edits)
    {
        int explicitBundles = edits
            .Select(edit => edit.BundleId)
            .Where(bundleId => !string.IsNullOrWhiteSpace(bundleId))
            .Distinct(StringComparer.Ordinal)
            .Count();
        if (explicitBundles > 0)
            return explicitBundles;
        if (family == SpecialChestFamily.LockedChest)
            return Math.Max(edits.Count(edit => edit.IsKey), edits.Count(edit => edit.IsLockedChest));
        if (family == SpecialChestFamily.SpringChest)
            return Math.Max(1, edits.Count(edit => edit.IsVisibleRoot));
        return Math.Max(1, edits.Count(edit => edit.IsVisibleRoot));
    }

    private static MobyBuildSafetyIssue NewSpecialChestIssue(
        string code,
        MobyBuildSafetyStatus status,
        string message,
        MobyBuildSafetyLevelReport level,
        SavedSpecialChestEdit root) =>
        new(
            code,
            status,
            message,
            level.LevelKey,
            level.LevelName,
            root.TrueIndex >= 0 ? root.TrueIndex : null,
            root.Label);

    private static MobyBuildSafetyStatus MaxBuildSafety(
        MobyBuildSafetyStatus left,
        MobyBuildSafetyStatus right) => left >= right ? left : right;

    private static string ComputeFileSha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static bool GetBoolean(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out JsonElement value))
            return false;
        return value.ValueKind == JsonValueKind.True ||
            value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out bool parsed) && parsed;
    }

    private static string GetString(JsonElement element, string name, string fallback = "")
    {
        if (!element.TryGetProperty(name, out JsonElement value))
            return fallback;
        return value.ValueKind == JsonValueKind.String ? value.GetString() ?? fallback : value.ToString();
    }

    private static int GetInt32(JsonElement element, string name, int fallback)
    {
        if (!element.TryGetProperty(name, out JsonElement value))
            return fallback;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number))
            return number;
        if (value.ValueKind != JsonValueKind.String)
            return fallback;

        string text = value.GetString()?.Trim() ?? "";
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int hex))
        {
            return hex;
        }
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : fallback;
    }

    private static int[] ReadInt32Array(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out JsonElement array) || array.ValueKind != JsonValueKind.Array)
            return [];
        return array.EnumerateArray()
            .Where(value => value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out _))
            .Select(value => value.GetInt32())
            .Where(value => value >= 0)
            .Distinct()
            .Order()
            .ToArray();
    }

    private sealed record SavedSpecialChestEdit(
        SpecialChestFamily Family,
        int TrueIndex,
        string Label,
        string TemplateFamily,
        int ActorId,
        bool IsKey,
        bool IsLockedChest,
        bool IsVisibleRoot,
        string BundleId,
        string ProfileId,
        int VisibleRootTrueIndex,
        IReadOnlyList<int> HiddenCompanionReferences,
        int Capacity,
        string EvidenceStatus)
    {
        public bool HasExplicitBundleMetadata => !string.IsNullOrWhiteSpace(BundleId);
    }

    private sealed record SavedSpecialChestLevel(
        EditedLevelExportTarget Target,
        int TotalSavedObjectEdits,
        bool HasOtherStructuralRelocations,
        IReadOnlyList<SavedSpecialChestEdit> Edits);
}
