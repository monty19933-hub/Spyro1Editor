using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Persistence;
using F = NativeFreshMaterialInspectionUiFixture;

// Tests-first: compiles against the old App without any proposed new API.
// Genuine tiny Store documents only; the picker cancels before source preparation.
internal static class NativeFreshSavedResearchReadinessCases
{
    internal static void Run(string parent)
    {
        int checks = 0;
        foreach (string kind in new[] { "retail", "schema1", "base-color", "mixed", "wizard", "dirty", "no-owner", "no-source", "owner-null", "owner-throw" })
        {
            string root = Path.Combine(parent, "saved-research-ready-" + kind);
            Directory.CreateDirectory(root);
            var project = new EditorProjectLayout(root, "saved-research-ready-" + kind);
            File.WriteAllText(project.ManifestPath, "{\"schemaVersion\":1,\"projectId\":\"" + project.ProjectId + "\",\"sentinel\":true}");
            var floor = NativeId65FreshDocumentIntent.Create(8448, 8448, 8704, 1024, 1024, 8960, 8960, 128,
                new(80, 128, 112), new(128, 96, 64), 20);
            var plain = NativeFreshLevelDocumentIntent.Create(floor, kind == "wizard"
                ? [NativeFreshWizardRouteIntent.Create(F.A, 9216, 9216, 8704, 9088, 9216)] : []);
            var pair = kind switch
            {
                "base-color" => NativeFreshFloorMaterialsIntent.Create(NativeFreshFloorMaterialIntent.BaseFloorColor(), NativeFreshFloorMaterialIntent.BaseFloorColor()),
                "mixed" => NativeFreshFloorMaterialsIntent.Create(NativeFreshFloorMaterialIntent.BaseFloorColor(), NativeFreshFloorMaterialIntent.RetailTerrain(F.Variant, 70, 1)),
                _ => F.Pair(14, 1, 70, 1)
            };
            var intent = kind == "schema1" ? plain : plain.WithFloorMaterials(pair);
            var old = NativeId65FreshDocumentStore.Save(NativeId65FreshDocumentStore.Capture(project), floor);
            var saved = NativeFreshLevelDocumentStore.Save(NativeFreshLevelDocumentStore.Capture(project), intent);
            byte[] manifest = File.ReadAllBytes(project.ManifestPath), legacy = File.ReadAllBytes(old.Path), aggregate = File.ReadAllBytes(saved.Path);
            NativeFreshDocumentWindow? window = null;
            List<Exception> failures = [];
            int ownerAttempts = 0, acquired = 0, released = 0, sourceSelections = 0, pickers = 0;
            try
            {
                Func<(string, string)> source = () => { sourceSelections++; return (Path.Combine(root, "never-open.bin"), Path.Combine(root, "never-open.cue")); };
                Func<IDisposable> begin = () =>
                {
                    ownerAttempts++;
                    if (kind == "owner-null") return null!;
                    if (kind == "owner-throw") throw new IOException("exact-UI-owner-acquisition-simulation");
                    acquired++; return new Owner(() => released++);
                };
                window = new(project, () => true, kind == "no-source" ? null : source, kind == "no-owner" ? null : begin);
                window.DecisionForTesting = _ => Task.FromResult(FreshDocumentDecision.Discard);
                window.ResearchOutputPickerForTesting = _ => { pickers++; return Task.FromResult<string?>(null); };
                window.Show(); F.Pump();
                Find<ComboBox>("FreshDocumentMode").SelectedItem = "Level aggregate"; F.Pump();
                var actual = window.CurrentAggregateIntent;
                if (kind == "dirty") { Find<TextBox>("FreshHeading").Text = "0128"; F.Pump(); }
                var button = Find<Button>("FreshDocumentResearch");
                Check(button.IsEnabled == (kind is "retail" or "owner-null" or "owner-throw"), "Exact saved schema2/two-retail/no-placement readiness: " + kind);
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button)); F.Pump();
                if (window.ResearchTaskForTesting is { } task) F.Wait(task);
                Check(ReferenceEquals(actual, window.CurrentAggregateIntent) && window.ResearchPublicationForTesting == null,
                    "Readiness/cancel never applies or fabricates a publication.");
                Check(acquired == (kind == "retail" ? 1 : 0) && released == acquired && pickers == acquired,
                    "Only supported clean arm acquires owner and picker; actual owner releases once.");
                Check(ownerAttempts == (kind is "retail" or "owner-null" or "owner-throw" ? 1 : 0),
                    "Owner invocation is distinct from an actually returned owner.");
                Check(kind == "retail" ? sourceSelections > 0 : sourceSelections == 0,
                    "Unsupported/dirty direct click refuses before source provider.");
                if (kind == "dirty") Check(Find<TextBox>("FreshHeading").Text == "0128" && window.HasUnappliedFields,
                    "Unapplied raw spelling remains untouched.");
            }
            catch (Exception error) { failures.Add(error); }
            finally
            {
                Attempt(() => { if (window?.ResearchTaskForTesting is { } task) F.Drain(task); });
                Attempt(() => { window?.Close(); F.Pump(); });
                Attempt(() => Check(File.ReadAllBytes(project.ManifestPath).SequenceEqual(manifest), "Complete manifest unchanged."));
                Attempt(() => Check(File.ReadAllBytes(old.Path).SequenceEqual(legacy), "Inactive legacy unchanged."));
                Attempt(() => Check(File.ReadAllBytes(saved.Path).SequenceEqual(aggregate), "Saved aggregate unchanged."));
                Attempt(() => Check(acquired == released, "Every acquired UI owner attempted."));
                Attempt(() => Check(!File.Exists(Path.Combine(root, "never-open.bin")) && !File.Exists(Path.Combine(root, "never-open.cue")),
                    "No native source fixture or output created."));
            }
            F.Throw(failures);
            T Find<T>(string name) where T : Control => window!.GetLogicalDescendants().OfType<T>().Single(c => c.Name == name);
            void Attempt(Action action) { try { action(); } catch (Exception error) { failures.Add(error); } }
        }
        Console.WriteLine($"FRESH_SAVED_RESEARCH_READINESS_OK checks={checks} sourceOpened=false nativeOutputWritten=false");
        void Check(bool condition, string message) { if (!condition) throw new InvalidDataException(message); checks++; }
    }

    private sealed class Owner(Action release) : IDisposable
    {
        private bool attempted;
        public void Dispose()
        {
            if (attempted) throw new InvalidDataException("Repeated actual UI owner release.");
            attempted = true; release();
        }
    }
}
