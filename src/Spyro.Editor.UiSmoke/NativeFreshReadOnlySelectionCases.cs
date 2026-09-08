using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using F = NativeFreshMaterialInspectionUiFixture;

// Actual peer calls reproduce the macOS value-provider crash without crashing a
// native process. A read-only value write must not select, edit, save or throw.
internal static class NativeFreshReadOnlySelectionCases
{
    internal static void Run(string root)
    {
        var fixture = new F(root, "readonly-selection");
        List<Exception> failures = [];
        int checks = 0;
        try
        {
            var intent = fixture.Window.CurrentAggregateIntent;
            foreach (string name in new[] { "FreshDocumentMode", "FreshDocumentMusic",
                "FreshMaterial0Kind", "FreshMaterial0SourceLevel", "FreshMaterial1Kind", "FreshMaterial1SourceLevel" })
            {
                var box = fixture.Find<ComboBox>(name);
                var peer = ControlAutomationPeer.CreatePeerForElement(box);
                var value = peer.GetProvider<IValueProvider>() ?? throw new InvalidDataException("Missing live value provider.");
                object? selected = box.SelectedItem;
                int selectedIndex = box.SelectedIndex;
                string? text = value.Value;
                bool enabled = box.IsEnabled;
                int changed = 0;
                box.SelectionChanged += (_, _) => changed++;
                // Integer ItemsSource selections are boxed on retrieval. Observe
                // the unmodified selection before any enabled/value operation;
                // reference identity is meaningful only for reference items.
                Check(SameSelection() && box.SelectedIndex == selectedIndex && changed == 0,
                    "Repeated selection reads preserve the original item value/index: " + name);
                foreach (bool active in new[] { true, false })
                {
                    box.IsEnabled = active;
                    foreach (string? attempt in new[] { "Legacy floor (v1)", "Level aggregate", "", "arbitrary invalid text", "\0", null })
                    {
                        value.SetValue(attempt);
                        Check(SameSelection() && box.SelectedIndex == selectedIndex && value.Value == text && changed == 0,
                            "Read-only accessibility value writes must be nonthrowing and nonmutating: " + name);
                    }
                }
                box.IsEnabled = enabled;
                Check(value.IsReadOnly && ReferenceEquals(value, peer.GetProvider<IValueProvider>()),
                    "One stable readonly wrapper per actual peer.");
                Check(peer.GetAutomationControlType() == AutomationControlType.ComboBox &&
                    peer.GetProvider<ISelectionProvider>() != null && peer.GetProvider<IExpandCollapseProvider>() != null,
                    "Value protection retains ordinary ComboBox/selection/expand accessibility.");
                bool SameSelection() => selected is int number
                    ? box.SelectedItem is int current && current == number
                    : ReferenceEquals(box.SelectedItem, selected);
            }
            Check(ReferenceEquals(intent, fixture.Window.CurrentAggregateIntent) && !fixture.Window.HasUnsavedChanges &&
                fixture.OwnerAttempts == 0 && fixture.SourceCalls == 0, "No implicit document, source or job changes.");

            // A normal selection still changes the actual UI and its live value.
            var kind = fixture.Find<ComboBox>("FreshMaterial0Kind");
            var kindPeer = ControlAutomationPeer.CreatePeerForElement(kind);
            var live = kindPeer.GetProvider<IValueProvider>()!;
            var expand = kindPeer.GetProvider<IExpandCollapseProvider>()!;
            string? before = live.Value;
            expand.Expand(); F.Pump();
            Check(kind.IsDropDownOpen, "Native expand provider still opens the actual menu.");
            expand.Collapse(); F.Pump();
            Check(!kind.IsDropDownOpen, "Native collapse provider still closes the actual menu.");
            kind.SelectedItem = "Base floor color"; F.Pump();
            Check(live.Value != before && live.Value == "Base floor color" && fixture.Window.HasUnappliedFields,
                "Ordinary selection keeps its existing event/draft behavior and live accessible value.");
            kind.SelectedItem = "Retail terrain record"; F.Pump();
            Check(live.Value == before && !fixture.Window.HasUnsavedChanges, "Restoring selection restores the raw form without saving.");
            fixture.Close();
            live.SetValue("after close");
            Check(kind.SelectedItem as string == "Retail terrain record", "Closed owner also ignores a stale value write.");
        }
        catch (Exception error) { failures.Add(error); }
        finally
        {
            Attempt(fixture.Close);
            Attempt(fixture.RequireProtected);
        }
        F.Throw(failures);
        Console.WriteLine($"FRESH_READONLY_SELECTION_OK checks={checks} nativeWindow=false sourceOpened=false");
        void Check(bool condition, string message)
        { if (!condition) throw new InvalidDataException(message); checks++; }
        void Attempt(Action action) { try { action(); } catch (Exception error) { failures.Add(error); } }
    }
}
