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
        foreach (string kind in new[] { "retail", "retail-t2", "schema1", "base-color", "mixed", "wizard", "dirty", "no-owner", "no-source", "owner-null", "owner-throw",
            "mixed-14-70", "mixed-70-14", "mixed-14-14", "mixed-route", "mixed-base", "mixed-music", "mixed-t2", "mixed-pair", "mixed-two-wizards", "mixed-nonretail",
            "mixed-dirty", "mixed-no-owner", "mixed-no-source", "mixed-owner-null", "mixed-owner-throw" })
        {
            string root = Path.Combine(parent, "saved-research-ready-" + kind);
            Directory.CreateDirectory(root);
            var project = new EditorProjectLayout(root, "saved-research-ready-" + kind);
            File.WriteAllText(project.ManifestPath, "{\"schemaVersion\":1,\"projectId\":\"" + project.ProjectId + "\",\"sentinel\":true}");
            var intent = Document(kind);
            bool dirty = kind is "dirty" or "mixed-dirty";
            bool noOwner = kind is "no-owner" or "mixed-no-owner";
            bool noSource = kind is "no-source" or "mixed-no-source";
            bool nullOwner = kind is "owner-null" or "mixed-owner-null";
            bool throwingOwner = kind is "owner-throw" or "mixed-owner-throw";
            bool ready = kind is "retail" or "retail-t2" or "mixed-14-70" or "mixed-70-14" or "mixed-14-14" || nullOwner || throwingOwner;
            bool reachesPicker = ready && !nullOwner && !throwingOwner;
            string dirtyHeading = kind == "mixed-dirty" ? "0064" : "0128";
            var old = NativeId65FreshDocumentStore.Save(NativeId65FreshDocumentStore.Capture(project), intent.Base);
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
                    if (nullOwner) return null!;
                    if (throwingOwner) throw new IOException("exact-UI-owner-acquisition-simulation");
                    acquired++; return new Owner(() => released++);
                };
                window = new(project, () => true, noSource ? null : source, noOwner ? null : begin);
                window.DecisionForTesting = _ => Task.FromResult(FreshDocumentDecision.Discard);
                window.ResearchOutputPickerForTesting = _ => { pickers++; return Task.FromResult<string?>(null); };
                window.Show(); F.Pump();
                Find<ComboBox>("FreshDocumentMode").SelectedItem = "Level aggregate"; F.Pump();
                var actual = window.CurrentAggregateIntent;
                Check(Find<TextBlock>("FreshLevelPlacementSummary").Text!.Contains("no placements or the supported one-Wizard mixed profile", StringComparison.Ordinal) &&
                    Find<TextBlock>("FreshDocumentStatus").Text!.Contains("no placements or the supported one-Wizard mixed profile", StringComparison.Ordinal),
                    "Placement summary and saved-open message describe both bounded research arms.");
                if (dirty) { Find<TextBox>("FreshHeading").Text = dirtyHeading; F.Pump(); }
                var button = Find<Button>("FreshDocumentResearch");
                Check(button.IsEnabled == ready, "Exact saved no-placement or supported mixed-profile readiness: " + kind);
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button)); F.Pump();
                if (window.ResearchTaskForTesting is { } task) F.Wait(task);
                Check(ReferenceEquals(actual, window.CurrentAggregateIntent) && window.ResearchPublicationForTesting == null,
                    "Readiness/cancel never applies or fabricates a publication.");
                Check(acquired == (reachesPicker ? 1 : 0) && released == acquired && pickers == acquired,
                    "Only supported clean arm acquires owner and picker; actual owner releases once.");
                Check(ownerAttempts == (ready ? 1 : 0),
                    "Owner invocation is distinct from an actually returned owner.");
                Check(reachesPicker ? sourceSelections > 0 : sourceSelections == 0,
                    "Unsupported/dirty direct click refuses before source provider.");
                if (dirty) Check(Find<TextBox>("FreshHeading").Text == dirtyHeading && window.HasUnappliedFields,
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

    private static NativeFreshLevelDocumentIntent Document(string kind)
    {
        bool mixed = kind.StartsWith("mixed-", StringComparison.Ordinal);
        var floor = mixed
            ? NativeId65FreshDocumentIntent.Create(8448, 8448, 8704, kind == "mixed-base" ? 1920 : 2048, 2048, 8960, 8960, 64,
                new(80, 128, 112), new(128, 96, 64), kind == "mixed-music" ? 20 : 19)
            : NativeId65FreshDocumentIntent.Create(8448, 8448, 8704, 1024, 1024, 8960, 8960, 128,
                new(80, 128, 112), new(128, 96, 64), 20);
        NativeFreshPlacementIntent[] placements = mixed
            ? [NativeFreshWizardRouteIntent.Create(F.A, 9984, 9984, 8704, kind == "mixed-route" ? 9456 : 9472, 9984)]
            : kind == "wizard" ? [NativeFreshWizardRouteIntent.Create(F.A, 9216, 9216, 8704, 9088, 9216)] : [];
        if (kind == "mixed-two-wizards")
            placements = [.. placements, NativeFreshWizardRouteIntent.Create(F.B, 9728, 9984, 8704, 9472, 9984)];
        var plain = NativeFreshLevelDocumentIntent.Create(floor, placements);
        var pair = kind switch
        {
            "base-color" => NativeFreshFloorMaterialsIntent.Create(NativeFreshFloorMaterialIntent.BaseFloorColor(), NativeFreshFloorMaterialIntent.BaseFloorColor()),
            "mixed" or "mixed-nonretail" => NativeFreshFloorMaterialsIntent.Create(NativeFreshFloorMaterialIntent.BaseFloorColor(), NativeFreshFloorMaterialIntent.RetailTerrain(F.Variant, 70, 1)),
            "retail-t2" or "mixed-t2" => F.Pair(14, 2, 70, 1),
            "mixed-70-14" => F.Pair(70, 1, 14, 1),
            "mixed-14-14" => F.Pair(14, 1, 14, 1),
            "mixed-pair" => F.Pair(70, 1, 70, 1),
            _ => F.Pair(14, 1, 70, 1)
        };
        return kind == "schema1" ? plain : plain.WithFloorMaterials(pair);
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
