using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Persistence;
using Spyro.Editor.Core.Workspace;

// Actual MainWindow controls and real tiny per-project preference files only.
// The Window is measured, not opened: OnOpened's source/cache/update work must
// not run in this source-free controller regression. No export is dispatched.
internal static class NativeFreeFlightUiCases
{
    private static int checks;

    internal static void Run()
    {
        checks = 0;
        string parent = Directory.CreateTempSubdirectory("spyro-free-flight-ui-").FullName;
        string first = Project(parent, "first"), second = Project(parent, "second");
        string previousDirectory = Environment.CurrentDirectory;
        string? previousWorkspace = Environment.GetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE");
        string? previousRelease = Environment.GetEnvironmentVariable("SPYRO_EDITOR_RELEASE");
        MainWindow? window = null;
        try
        {
            Environment.CurrentDirectory = first;
            Environment.SetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE", first);
            // Deferred controls remain testable only in the internal research profile.
            Environment.SetEnvironmentVariable("SPYRO_EDITOR_RELEASE", "0");
            window = new MainWindow { Width = 1200, Height = 760 };
            Check(!window.TryGetFreeFlightSettingsExportBlockReason(out _),
                "An unshown Window with no flight-editor refresh does not block an absent-store combined export caller.");
            Get<TabControl>(window, "_modernWorkspaceTabs").SelectedItem = Get<TabItem>(window, "_modernLevelWorkspaceTab");
            Bind(window, first, 14);
            Check(Find<CheckBox>(window, "FreeFlightEnabled").Content?.ToString() == "Fly Anywhere Enabled",
                "The actual level-settings control has the requested exact label.");
            Check(Find<CheckBox>(window, "FreeFlightEnabled").IsChecked == false &&
                !NativeFreeFlightSettingsStore.Capture(first).Exists &&
                !window.TryGetFreeFlightSettingsExportBlockReason(out _),
                "Missing settings render Off without writing intent or blocking otherwise unchanged retail export.");
            Check((Find<TextBlock>(window, "FreeFlightDetails").Text ?? "").Contains("default: Off", StringComparison.Ordinal),
                "A never-saved default Off is identified as default, not a saved player ability.");
            string tooltip = ToolTip.GetTip(Find<CheckBox>(window, "FreeFlightEnabled"))?.ToString() ?? "";
            Check(tooltip.Contains("Spyro", StringComparison.Ordinal) && tooltip.Contains("exported game", StringComparison.Ordinal) &&
                tooltip.Contains("Free Camera", StringComparison.Ordinal),
                "The existing checkbox distinguishes native Spyro flight in exported gameplay from the editor camera.");
            ActualLevelSelection(window, first);
            DraftAndPersistence(window, first, second);
            window = ReopenSavedChoice(window, first);
            UnsupportedTargets(window, first);
            BusyRefusal(window, first);
            CreateBinBusyUi(window, first);
            foreach (string drift in new[] { "workspace", "level", "settings" })
                DelayedStaleSave(window, first, second, drift);
            Malformed(window, first);
            Check(!Directory.EnumerateFiles(parent, "*.bin", SearchOption.AllDirectories).Any() &&
                !Directory.EnumerateFiles(parent, "*.cue", SearchOption.AllDirectories).Any(),
                "Controller tests never create a native image or launcher.");
            Check(new[] { first, second }.All(root => File.ReadAllText(Path.Combine(root, "unrelated-project-notes.txt")) == "protected"),
                "Both projects retain unrelated user-owned file bytes throughout all flight operations.");
            Console.WriteLine($"FREE_FLIGHT_UI_OK checks={checks} sourceOpened=false nativeOutputWritten=false fixtures={parent}");
        }
        finally
        {
            if (window != null)
            {
                window.RegularEditorPersistenceDelayOverrideForTesting = null;
                window.LevelSelectionDelayOverrideForTesting = null;
                window.Close(); Pump();
            }
            Environment.CurrentDirectory = previousDirectory;
            Environment.SetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE", previousWorkspace);
            Environment.SetEnvironmentVariable("SPYRO_EDITOR_RELEASE", previousRelease);
        }
    }

    private static void ActualLevelSelection(MainWindow window, string root)
    {
        int beforeChecks = checks;
        Bind(window, root, 11); // Initial source-free fixture only; transitions below use the actual loaders.
        Check(!File.Exists(DiscImageLocator.FindImage(Get<EditorWorkspace>(window, "_workspace"))),
            "Real selection regression has no source disc available to discover or open.");
        var catalog = Get<LevelCatalog>(window, "_catalog");
        LevelDefinition stone = catalog.Levels.Single(l => l.LevelId == 11), toasty = catalog.Levels.Single(l => l.LevelId == 14);
        var picker = Get<ComboBox>(window, "_levelJumpBox");
        Set(window, "_syncingLevelSelection", true);
        try
        {
            picker.ItemsSource = catalog.Levels;
            Get<ListBox>(window, "_levelList").ItemsSource = catalog.Levels;
            picker.SelectedItem = stone;
        }
        finally { Set(window, "_syncingLevelSelection", false); }
        bool Selecting() => Get<bool>(window, "_loadingLevel") || Get<int>(window, "_levelSelectionAsyncInFlight") != 0;
        bool ControlsEnabled() => Find<CheckBox>(window, "FreeFlightEnabled").IsEnabled &&
            Find<Button>(window, "FreeFlightSave").IsEnabled && Find<Button>(window, "FreeFlightRevert").IsEnabled;
        bool ControlsDisabled() => !Find<CheckBox>(window, "FreeFlightEnabled").IsEnabled &&
            !Find<Button>(window, "FreeFlightSave").IsEnabled && !Find<Button>(window, "FreeFlightRevert").IsEnabled;
        void WaitSelected(LevelDefinition target)
        {
            Wait(() => !Selecting() && ReferenceEquals(Get<LevelDefinition>(window, "_currentLevel"), target), "real selection of " + target.DisplayName);
        }
        Task LoadDirect(LevelDefinition target) => (Task)typeof(MainWindow).GetMethod("SelectLevelAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(window, new object?[] { target, null })!;

        picker.SelectedItem = toasty; WaitSelected(toasty);
        Check(ControlsEnabled(), "Completed actual Stone Hill-to-Toasty picker load releases all three Fly Anywhere controls without manual refresh.");
        Check((Find<TextBlock>(window, "FreeFlightDetails").Text ?? "").Contains("Toasty — default: Off", StringComparison.Ordinal) &&
            !window.TryGetFreeFlightSettingsExportBlockReason(out _), "Actual loaded Toasty owns current default-Off binding and unchanged export gates.");

        // Direct awaited loads own only the async counter. Picker loads also
        // own the outer loading flag; neither release may refresh too early.
        foreach (bool viaPicker in new[] { false, true })
        {
            LevelDefinition target = viaPicker ? stone : toasty;
            TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
            window.LevelSelectionDelayOverrideForTesting = stage =>
            {
                if (stage != "level-load-work-completed") return Task.CompletedTask;
                entered.TrySetResult(); return release.Task;
            };
            Task? direct = null;
            try
            {
                if (viaPicker) picker.SelectedItem = target; else direct = LoadDirect(target);
                Wait(() => entered.Task.IsCompleted, "deferred actual level loader");
                Check(Get<int>(window, "_levelSelectionAsyncInFlight") == 1 && Get<bool>(window, "_loadingLevel") == viaPicker && ControlsDisabled(),
                    "Fly Anywhere remains disabled while the real " + (viaPicker ? "picker and inner load" : "direct awaited load") + " owns its busy fences.");
            }
            finally
            {
                release.TrySetResult();
                if (direct != null) { Wait(() => direct.IsCompleted, "direct level load completion"); direct.GetAwaiter().GetResult(); }
                else WaitSelected(target);
                window.LevelSelectionDelayOverrideForTesting = null;
            }
            WaitSelected(target);
            Check(ControlsEnabled(), "Actual " + (viaPicker ? "outer picker" : "inner load") + " finalizer restores current Fly Anywhere availability.");
        }
        picker.SelectedItem = toasty; WaitSelected(toasty);
        Check(ControlsEnabled(), "Repeated actual picker transitions keep the supported Toasty controls usable.");
        Check(!NativeFreeFlightSettingsStore.Capture(root).Exists && !window.TryGetFreeFlightSettingsExportBlockReason(out _),
            "Level selection and availability refresh never save a flight choice or broaden export support.");
        Console.WriteLine($"FREE_FLIGHT_SELECTION_UI_OK checks={checks-beforeChecks} actualPickerAndLoader=true sourceOpened=false nativeOutputWritten=false");
    }

    private static MainWindow ReopenSavedChoice(MainWindow window, string root)
    {
        Bind(window, root, 14);
        Find<CheckBox>(window, "FreeFlightEnabled").IsChecked = true; Pump();
        Fire(window, "FreeFlightSave");
        Wait(() => NativeFreeFlightSettingsStore.Capture(root).IsEnabled(14) && !Busy(window), "saved On before closing editor");
        byte[] savedOn = File.ReadAllBytes(SettingsPath(root));
        Find<CheckBox>(window, "FreeFlightEnabled").IsChecked = false; Pump();
        Check(window.TryGetFreeFlightSettingsExportBlockReason(out _), "An unsaved Off draft cannot replace the saved starting choice.");
        window.Close(); Pump();

        window = new MainWindow { Width = 1200, Height = 760 };
        Get<TabControl>(window, "_modernWorkspaceTabs").SelectedItem = Get<TabItem>(window, "_modernLevelWorkspaceTab");
        Bind(window, root, 14);
        Check(Find<CheckBox>(window, "FreeFlightEnabled").IsChecked == true &&
            File.ReadAllBytes(SettingsPath(root)).SequenceEqual(savedOn) &&
            NativeFreeFlightProjectExportIntent.Capture(root).EnabledLevelId == 14 &&
            !window.TryGetFreeFlightSettingsExportBlockReason(out _),
            "A fresh editor instance reloads saved On into both the control and export intent; a closed unsaved draft cannot leak.");
        string details = Find<TextBlock>(window, "FreeFlightDetails").Text ?? "";
        Check(details.Contains("saved: On", StringComparison.Ordinal) &&
            details.Contains("flight ability", StringComparison.OrdinalIgnoreCase) &&
            details.Contains("Cross", StringComparison.Ordinal) &&
            details.Contains("memory card", StringComparison.OrdinalIgnoreCase),
            "Saved On describes starting flight availability, its native entry control and the separate memory-card boundary.");
        Find<CheckBox>(window, "FreeFlightEnabled").IsChecked = false; Pump();
        Fire(window, "FreeFlightSave");
        Wait(() => !NativeFreeFlightSettingsStore.Capture(root).IsEnabled(14) && !Busy(window), "saved Off before closing editor");
        byte[] savedOff = File.ReadAllBytes(SettingsPath(root));
        window.Close(); Pump();

        window = new MainWindow { Width = 1200, Height = 760 };
        Get<TabControl>(window, "_modernWorkspaceTabs").SelectedItem = Get<TabItem>(window, "_modernLevelWorkspaceTab");
        Bind(window, root, 14);
        var intent = NativeFreeFlightProjectExportIntent.Capture(root);
        Check(Find<CheckBox>(window, "FreeFlightEnabled").IsChecked == false &&
            File.ReadAllBytes(SettingsPath(root)).SequenceEqual(savedOff) &&
            intent.HasExplicitSelection(14) && intent.RequiresRebuild && intent.EnabledLevelId == null &&
            !window.TryGetFreeFlightSettingsExportBlockReason(out _),
            "A second fresh editor instance reloads explicit Off and still requires the original-source rebuild.");
        Check((Find<TextBlock>(window, "FreeFlightDetails").Text ?? "").Contains("saved: Off", StringComparison.Ordinal),
            "Reopened explicit Off remains visibly saved, distinct from a never-saved default.");
        return window;
    }

    private static void DraftAndPersistence(MainWindow window, string first, string second)
    {
        var enabled = Find<CheckBox>(window, "FreeFlightEnabled");
        enabled.IsChecked = true; Pump();
        Check(!NativeFreeFlightSettingsStore.Capture(first).Exists,
            "Checking the draft alone never writes settings or native output.");
        Check((Find<TextBlock>(window, "FreeFlightDetails").Text ?? "").Contains("default: Off. Draft: On.", StringComparison.Ordinal),
            "A changed draft displays the desired On separately from the untouched default Off.");
        Check(window.TryGetFreeFlightSettingsExportBlockReason(out string dirtyReason) &&
            dirtyReason.Contains("Save", StringComparison.OrdinalIgnoreCase) && dirtyReason.Contains("Revert", StringComparison.OrdinalIgnoreCase),
            "An unsaved On draft explicitly blocks export until Save or Revert.");
        Fire(window, "FreeFlightRevert");
        Check(enabled.IsChecked == false && !NativeFreeFlightSettingsStore.Capture(first).Exists,
            "Actual Revert restores the bound saved/default choice without materializing a preference file.");
        Check(!window.TryGetFreeFlightSettingsExportBlockReason(out _), "Revert clears the dirty-draft export refusal without saving.");
        enabled.IsChecked = true; Pump();
        Check(Find<Button>(window, "FreeFlightSave").IsEnabled, "The admitted Toasty draft can be saved.");
        Fire(window, "FreeFlightSave");
        Wait(() => NativeFreeFlightSettingsStore.Capture(first).IsEnabled(14) && !Busy(window), "actual Save button publication");
        byte[] saved = File.ReadAllBytes(SettingsPath(first));
        enabled.IsChecked = false; Pump();
        Check(window.TryGetFreeFlightSettingsExportBlockReason(out _), "An unsaved Off draft also blocks export; persisted On cannot silently win.");
        Fire(window, "FreeFlightRevert");
        Check(enabled.IsChecked == true && File.ReadAllBytes(SettingsPath(first)).SequenceEqual(saved),
            "Revert restores saved On and leaves its persisted bytes exact.");
        Bind(window, first, 11);
        Check(enabled.IsChecked == true && NativeFreeFlightSettingsStore.Capture(first).Preferences.All(p=>p.EnableFreeFlight),
            "Another normal retail level shows the same saved global On choice.");
        Bind(window, second, 14);
        Check(enabled.IsChecked == false && !NativeFreeFlightSettingsStore.Capture(second).Exists,
            "The same retail LevelId in another actual project cannot inherit the first project's On intent.");
        Bind(window, first, 14);
        Check(enabled.IsChecked == true, "Returning to the first project reloads its actual persisted setting.");
        enabled.IsChecked = false; Pump(); Fire(window, "FreeFlightSave");
        Wait(() => !NativeFreeFlightSettingsStore.Capture(first).IsEnabled(14) && !Busy(window), "explicit Off publication");
        Check(NativeFreeFlightSettingsStore.Capture(first).HasExplicitSelection(14) &&
            !NativeFreeFlightSettingsStore.Capture(second).Exists,
            "Saving Off is an explicit future rebuild instruction, not deletion or a cross-project change.");
    }

    private static void UnsupportedTargets(MainWindow window, string root)
    {
        Bind(window, root, 11);
        byte[] before = File.ReadAllBytes(SettingsPath(root));
        Find<CheckBox>(window, "FreeFlightEnabled").IsChecked = true; Pump();
        Drain(window.SaveFreeFlightSettingsAsync());
        Check(!File.ReadAllBytes(SettingsPath(root)).SequenceEqual(before) &&
            NativeFreeFlightSettingsStore.Capture(root).Preferences.All(p=>p.EnableFreeFlight) &&
            !window.TryGetFreeFlightSettingsExportBlockReason(out _),
            "Normal-level On saves one global choice for all30 normal levels and is exportable.");
        Find<CheckBox>(window, "FreeFlightEnabled").IsChecked = false; Pump();
        Drain(window.SaveFreeFlightSettingsAsync());
        Check(NativeFreeFlightSettingsStore.Capture(root).HasExplicitSelection(11) &&
            !NativeFreeFlightSettingsStore.Capture(root).IsEnabled(11),
            "An explicit Off preference for another normal retail level may still be retained.");

        var expected = NativeFreeFlightSettingsStore.Capture(root);
        var unsupportedOn = NativeFreeFlightSettingsStore.Save(expected, 11, true);
        Bind(window, root, 11);
        Check(Find<CheckBox>(window, "FreeFlightEnabled").IsChecked == true &&
            !window.TryGetFreeFlightSettingsExportBlockReason(out _),
            "A legacy per-level On migrates visibly into the global export intent without becoming Off.");
        NativeFreeFlightSettingsStore.Save(unsupportedOn, 11, false);
        Bind(window, root, 65);
        before = File.ReadAllBytes(SettingsPath(root));
        Check(!Find<CheckBox>(window, "FreeFlightEnabled").IsEnabled &&
            !Find<Button>(window, "FreeFlightSave").IsEnabled,
            "Independent ID65 does not borrow the retail flight controller.");
        Find<CheckBox>(window, "FreeFlightEnabled").IsChecked = true;
        Drain(window.SaveFreeFlightSettingsAsync());
        Check(File.ReadAllBytes(SettingsPath(root)).SequenceEqual(before), "Direct ID65 Save cannot bypass unavailable controls.");
    }

    private static void BusyRefusal(MainWindow window, string root)
    {
        Bind(window, root, 14);
        Find<CheckBox>(window, "FreeFlightEnabled").IsChecked = true; Pump();
        byte[] before = File.ReadAllBytes(SettingsPath(root));
        Set(window, "_createBinBusy", true);
        try
        {
            Drain(window.SaveFreeFlightSettingsAsync());
            Check(File.ReadAllBytes(SettingsPath(root)).SequenceEqual(before) && !Busy(window),
                "Direct flight Save refuses while Create BIN owns its captured preferences, even without a UI refresh.");
        }
        finally { Set(window, "_createBinBusy", false); }
    }

    private static void DelayedStaleSave(MainWindow window, string first, string second, string drift)
    {
        Bind(window, first, 14);
        Find<CheckBox>(window, "FreeFlightEnabled").IsChecked = true; Pump();
        byte[] before = File.ReadAllBytes(SettingsPath(first));
        TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        window.RegularEditorPersistenceDelayOverrideForTesting = stage =>
        {
            if (stage != "free-flight-before-save") return Task.CompletedTask;
            started.TrySetResult(); return release.Task;
        };
        Task? pending = null;
        try
        {
            pending = window.SaveFreeFlightSettingsAsync();
            Wait(() => started.Task.IsCompleted, "flight pre-write ownership hook");
            Check(Busy(window) && !Find<Button>(window, "FreeFlightSave").IsEnabled &&
                File.ReadAllBytes(SettingsPath(first)).SequenceEqual(before),
                "Delayed Save holds the existing editor persistence owner and has not written early.");
            if (drift == "workspace") Bind(window, second, 14);
            else if (drift == "level") Bind(window, first, 11);
            else
            {
                // The global Off fixture already contains all thirty Off rows.
                // Change an actual value so the independent writer changes the CAS hash.
                NativeFreeFlightSettingsStore.Save(NativeFreeFlightSettingsStore.Capture(first), 12, true);
                before = File.ReadAllBytes(SettingsPath(first));
            }
            release.TrySetResult(); Drain(pending);
            Check(!Busy(window) && File.ReadAllBytes(SettingsPath(first)).SequenceEqual(before) &&
                !NativeFreeFlightSettingsStore.Capture(second).Exists,
                "Stale " + drift + " continuation neither publishes its old draft nor writes into the newly active project.");
            if (drift != "settings")
                Check(Find<CheckBox>(window, "FreeFlightEnabled").IsChecked == false,
                    "A rejected old Save cannot replace the newly bound project's or level's visible draft.");
        }
        finally
        {
            release.TrySetResult();
            if (pending != null && !pending.IsCompleted) Drain(pending);
            window.RegularEditorPersistenceDelayOverrideForTesting = null;
        }
    }

    private static void CreateBinBusyUi(MainWindow window, string root)
    {
        Bind(window, root, 14);
        byte[] before = File.ReadAllBytes(SettingsPath(root));
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task active = window.RunCreateBinCommandWithVisibleFeedbackAsync(() => release.Task);
        try
        {
            Pump();
            Check(!active.IsCompleted && !Find<CheckBox>(window, "FreeFlightEnabled").IsEnabled &&
                !Find<Button>(window, "FreeFlightSave").IsEnabled && !Find<Button>(window, "FreeFlightRevert").IsEnabled,
                "The actual normal Create BIN lifecycle disables all flight controls while its fake command is pending.");
            // Disabled widgets are presentation, not the ownership check.
            Find<CheckBox>(window, "FreeFlightEnabled").IsChecked = true; Pump();
            Check(window.TryGetFreeFlightSettingsExportBlockReason(out string reason) &&
                reason.Contains("draft", StringComparison.OrdinalIgnoreCase),
                "A programmatic dirty draft during Create BIN remains detectable by the publication owner guard.");
            Drain(window.SaveFreeFlightSettingsAsync());
            Check(File.ReadAllBytes(SettingsPath(root)).SequenceEqual(before),
                "Create BIN never auto-saves a changed flight draft and direct Save cannot overlap its owner.");
        }
        finally { release.TrySetResult(); Drain(active); }
        Check(Find<CheckBox>(window, "FreeFlightEnabled").IsEnabled && Find<Button>(window, "FreeFlightSave").IsEnabled &&
            window.TryGetFreeFlightSettingsExportBlockReason(out _),
            "Create BIN restores flight control availability without clearing the unsaved draft refusal.");
        Fire(window, "FreeFlightRevert");
        Check(!window.TryGetFreeFlightSettingsExportBlockReason(out _) && File.ReadAllBytes(SettingsPath(root)).SequenceEqual(before),
            "Explicit Revert after the build clears the draft and preserves saved intent.");
    }

    private static void Malformed(MainWindow window, string root)
    {
        string path = SettingsPath(root); byte[] valid = File.ReadAllBytes(path);
        try
        {
            File.WriteAllText(path, "{not valid flight preferences}");
            byte[] malformed = File.ReadAllBytes(path);
            Bind(window, root, 14);
            Check(!Find<CheckBox>(window, "FreeFlightEnabled").IsEnabled &&
                !Find<Button>(window, "FreeFlightSave").IsEnabled &&
                ErrorVisible(window) &&
                window.TryGetFreeFlightSettingsExportBlockReason(out string reason) &&
                reason.Contains("flight", StringComparison.OrdinalIgnoreCase),
                "Malformed project settings display an error and block Save/export instead of becoming default Off.");
            Drain(window.SaveFreeFlightSettingsAsync()); window.RevertFreeFlightSettings(); Pump();
            Check(File.ReadAllBytes(path).SequenceEqual(malformed), "Save/Revert cannot repair or overwrite malformed settings.");
        }
        finally { File.WriteAllBytes(path, valid); }
        Bind(window, root, 14);
        Check(!window.TryGetFreeFlightSettingsExportBlockReason(out _), "Reloading exact valid preferences clears the flight export refusal.");
    }

    private static string Project(string parent, string name)
    {
        string root = Directory.CreateDirectory(Path.Combine(parent, name)).FullName;
        File.WriteAllText(Path.Combine(root, "unrelated-project-notes.txt"), "protected");
        File.WriteAllText(Path.Combine(root, "spyro-level-catalog.json"), JsonSerializer.Serialize(new
        {
            levels = new[] { new { key = "toasty", displayName = "Toasty", levelId = 14 },
                new { key = "stonehill", displayName = "Stone Hill", levelId = 11 } }
        }));
        return root;
    }
    private static void Bind(MainWindow window, string root, int levelId)
    {
        var catalog = LevelCatalog.Load(root);
        LevelDefinition level = levelId == 65
            ? new() { Key = "unusedlevel65blank", DisplayName = "Independent ID65", LevelId = 65 }
            : catalog.Levels.Single(l => l.LevelId == levelId);
        if (Get<EditorWorkspace>(window, "_workspace").RootPath != root)
            Set(window, "_workspace", new EditorWorkspace(root));
        Set(window, "_retailCatalog", catalog); Set(window, "_catalog", catalog);
        Set(window, "_currentLevel", level);
        Set(window, "_levelLoadRequestId", Get<int>(window, "_levelLoadRequestId") + 1);
        window.RefreshFreeFlightSettingsEditor(level);
        window.Measure(new Size(1200, 760)); window.Arrange(new Rect(0, 0, 1200, 760)); Pump();
    }
    private static string SettingsPath(string root) => Path.Combine(root, NativeFreeFlightSettingsStore.FileName);
    private static bool ErrorVisible(MainWindow window)
    {
        string details = Find<TextBlock>(window, "FreeFlightDetails").Text ?? "";
        return new[] { "error", "invalid", "malformed", "unavailable", "could not", "failed" }
            .Any(word => details.Contains(word, StringComparison.OrdinalIgnoreCase));
    }
    private static bool Busy(MainWindow window) => Get<bool>(window, "_regularEditorPersistenceBusy");
    private static T Get<T>(MainWindow window, string name) => (T)typeof(MainWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
    private static void Set(MainWindow window, string name, object value) => typeof(MainWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, value);
    private static T Find<T>(MainWindow window, string name) where T : Control =>
        window.GetLogicalDescendants().OfType<T>().FirstOrDefault(c => c.Name == name)
        ?? throw new InvalidOperationException("Missing actual flight settings control: " + name);
    private static void Fire(MainWindow window, string name)
    { var button = Find<Button>(window, name); button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button)); Pump(); }
    private static void Drain(Task task)
    {
        Wait(() => task.IsCompleted, "flight command completion");
        try { task.GetAwaiter().GetResult(); }
        catch (Exception error) when (error is OperationCanceledException or InvalidDataException or IOException) { }
    }
    private static void Wait(Func<bool> ready, string label)
    {
        var clock = Stopwatch.StartNew();
        while (!ready())
        { if (clock.Elapsed > TimeSpan.FromSeconds(10)) throw new TimeoutException(label); Pump(); Thread.Sleep(1); }
        Pump();
    }
    private static void Pump() { Dispatcher.UIThread.RunJobs(); }
    private static void Check(bool condition, string label)
    { if (!condition) throw new InvalidOperationException(label); checks++; }
}
