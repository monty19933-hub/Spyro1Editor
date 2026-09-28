using System.Reflection;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Threading;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Levels;

internal static class LevelPickerAutomationCases
{
    internal static void Run()
    {
        int checks = 0;
        void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); checks++; }
        string fixture = Directory.CreateTempSubdirectory("spyro-level-picker-automation-").FullName;
        string previousDirectory = Environment.CurrentDirectory;
        string? previousWorkspace = Environment.GetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE");
        string? previousRelease = Environment.GetEnvironmentVariable("SPYRO_EDITOR_RELEASE");
        MainWindow? window = null;
        try
        {
            Environment.CurrentDirectory = fixture;
            Environment.SetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE", fixture);
            Environment.SetEnvironmentVariable("SPYRO_EDITOR_RELEASE", "1");
            // Do not Show: source/cache/update OnOpened work and native AX are outside this test.
            window = new MainWindow { Width = 1200, Height = 760 };
            var picker = Get<ComboBox>(window, "_levelJumpBox");
            var stone = new LevelDefinition { Key = "stonehill", DisplayName = "Stone Hill", LevelId = 11 };
            var toasty = new LevelDefinition { Key = "toasty", DisplayName = "Toasty", LevelId = 14 };
            var duplicate = new LevelDefinition { Key = "ambiguous", DisplayName = "Toasty", LevelId = 99 };
            Set(window, "_syncingLevelSelection", true);
            picker.ItemsSource = new[] { stone, toasty, duplicate };
            picker.SelectedItem = stone;
            window.Measure(new Size(1200, 760)); window.Arrange(new Rect(0, 0, 1200, 760));
            Dispatcher.UIThread.RunJobs();
            AutomationPeer peer = ControlAutomationPeer.CreatePeerForElement(picker)!;
            var value = (IValueProvider)peer;
            var selection = (ISelectionProvider)peer;
            var expand = (IExpandCollapseProvider)peer;
            Check(ReferenceEquals(peer.GetProvider<IValueProvider>(), value),
                "Provider discovery reaches the same safe interface used by the native bridge.");
            Check(value.IsReadOnly && value.Value == "Stone Hill", "Level picker exposes read-only selected label.");
            Check(peer.GetAutomationControlType() == AutomationControlType.ComboBox,
                "The app-owned peer preserves the ComboBox role.");
            Check(picker.StyleKey == typeof(ComboBox), "The subclass retains the normal ComboBox theme.");
            int changed = 0;
            picker.SelectionChanged += (_, _) => changed++;
            int request = Get<int>(window, "_levelLoadRequestId");
            foreach (string? text in new string?[] { "Toasty", "Stone Hill", "unknown", "", null, "14" })
            {
                // Regression: stock Avalonia 12.0.3 throws NotSupportedException here,
                // including when the caller disregards the already-true IsReadOnly flag.
                value.SetValue(text);
                Dispatcher.UIThread.RunJobs();
                Check(ReferenceEquals(picker.SelectedItem, stone) && changed == 0 && value.Value == "Stone Hill",
                    "AX value writes must be safe no-ops, never label-to-selection inference.");
                Check(Get<int>(window, "_levelLoadRequestId") == request &&
                      Get<int>(window, "_levelSelectionAsyncInFlight") == 0 && !Get<bool>(window, "_loadingLevel"),
                    "Rejected AX values cannot initiate loading.");
            }
            Check(selection.GetSelection().Count == 1 && selection.GetSelection()[0].GetName() == "Stone Hill",
                "Inherited selection automation still reports the selected item.");
            // Opening a templated popup on an unshown headless Window is not a
            // native-window test. Exercise inherited state on an untemplated twin.
            var detached = (ComboBox)Activator.CreateInstance(picker.GetType(), nonPublic: true)!;
            var detachedExpand = (IExpandCollapseProvider)ControlAutomationPeer.CreatePeerForElement(detached)!;
            detachedExpand.Expand(); Check(detached.IsDropDownOpen && expand.ShowsMenu, "Inherited expansion remains available.");
            detachedExpand.Collapse(); Check(!detached.IsDropDownOpen, "Inherited collapse remains available.");
            picker.SelectedItem = toasty;
            Check(changed == 1 && value.Value == "Toasty", "Normal selection still updates automation value.");
            picker.IsEnabled = false;
            value.SetValue("Stone Hill");
            Check(ReferenceEquals(picker.SelectedItem, toasty) && changed == 1, "Disabled picker also rejects value writes safely.");
            picker.SelectedItem = null;
            value.SetValue("Toasty");
            Check(picker.SelectedItem == null && value.Value == null && selection.GetSelection().Count == 0,
                "An empty selection cannot be populated through the read-only value pattern.");
            // Realize the actual picker's items without opening an unshown Window's
            // popup. Only this fixture replaces the template; production stays themed.
            picker.IsEnabled = true;
            picker.ItemsPanel = new FuncTemplate<Panel?>(() => new StackPanel());
            picker.Template = new FuncControlTemplate<ComboBox>((owner, scope) =>
            {
                scope.Register("PART_Popup", new Popup()); // Required template part; never attached or opened.
                return new ItemsPresenter { Name = "PART_ItemsPresenter", ItemsPanel = owner.ItemsPanel }.RegisterInNameScope(scope);
            });
            picker.ApplyTemplate();
            picker.Measure(new Size(400, 200)); picker.Arrange(new Rect(0, 0, 400, 200));
            Control item = picker.ContainerFromIndex(1)
                ?? throw new InvalidOperationException("Popup-free fixture must realize the Toasty item.");
            var itemSelection = ControlAutomationPeer.CreatePeerForElement(item)!.GetProvider<ISelectionItemProvider>()
                ?? throw new InvalidOperationException("Realized Toasty item must expose selection automation.");
            Check(ReferenceEquals(itemSelection.SelectionContainer, selection),
                "Realized item selection is owned by the actual main picker peer.");
            itemSelection.Select();
            Check(ReferenceEquals(picker.SelectedItem, toasty) && value.Value == "Toasty" && itemSelection.IsSelected,
                "Inherited selection-item automation selects Toasty and updates its label.");
            value.SetValue("Stone Hill");
            Check(ReferenceEquals(picker.SelectedItem, toasty) && itemSelection.IsSelected,
                "Value writes remain rejected after legitimate selection-item automation.");
            Check(!Directory.EnumerateFiles(fixture, "*.bin", SearchOption.AllDirectories).Any() &&
                  !Directory.EnumerateFiles(fixture, "*.cue", SearchOption.AllDirectories).Any(),
                "Automation regression creates no native image.");
            Console.WriteLine($"LEVEL_PICKER_AUTOMATION_OK checks={checks} sourceOpened=false nativeWindowOpened=false nativeOutputWritten=false fixtures={fixture}");
        }
        finally
        {
            if (window != null) { Set(window, "_syncingLevelSelection", false); window.Close(); Dispatcher.UIThread.RunJobs(); }
            Environment.CurrentDirectory = previousDirectory;
            Environment.SetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE", previousWorkspace);
            Environment.SetEnvironmentVariable("SPYRO_EDITOR_RELEASE", previousRelease);
        }
    }

    private static T Get<T>(MainWindow window, string name) =>
        (T)typeof(MainWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
    private static void Set(MainWindow window, string name, object value) =>
        typeof(MainWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, value);
}
