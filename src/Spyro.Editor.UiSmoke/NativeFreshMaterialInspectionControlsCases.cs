using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Persistence;
using System.Runtime.ExceptionServices;

// PRIVATE first RED: compiles against existing Window, with no new seam/API dependency.
// Named-control absence is the intended failure; no original/worker/output is invoked.
internal static class NativeFreshMaterialInspectionControlsCases
{
    private const string Variant = "spyro1-usa-retail-mode2-2352-v1";
    private const string Retail = "Retail terrain record", Color = "Base floor color";
    private static int checks;

    internal static void Run()
    {
        checks = 0;
        string root = Directory.CreateTempSubdirectory("spyro-material-inspection-controls-").FullName;
        List<Fixture> fixtures = [];
        List<Exception> failures = [];
        try
        {
            // No replacement worker or reflection-set Window state is installed.
            // This first genuine Window/control lookup is the expected initial RED.
            Fixture present = Add("present", Document().WithFloorMaterials(Pair()));
            present.OpenAggregate();
            Check(present.Window.CurrentAggregateIntent?.SchemaVersion == 2, "actual saved schema2 fixture opened");
            Button inspect = present.Find<Button>("FreshMaterialsInspect");
            Check(inspect.Content?.ToString() == "Inspect material sources", "explicit, discoverable inspection action");
            foreach (string name in new[] { "FreshMaterialsCancelInspect", "FreshMaterial0Preview",
                "FreshMaterial1Preview", "FreshMaterial0Inspection", "FreshMaterial1Inspection" })
                present.Find<Control>(name);
            Check(inspect.IsEffectivelyEnabled && !present.Find<Button>("FreshMaterialsCancelInspect").IsEffectivelyEnabled,
                "valid explicit raw pair is enabled without evaluating source provider");
            RequireEmptyPreviews(present);
            present.Fire("FreshDocumentSave"); present.Fire("FreshDocumentReopen"); present.Fire("FreshDocumentApply");
            present.Fire("FreshDocumentUndo"); present.Fire("FreshDocumentRedo");
            Check(present.OwnerCalls == 0 && present.SourceCalls == 0, "Open/Save/Reopen/Apply/history never auto-inspect");

            foreach (string raw in new[] { "", "bad", "-1", "128" })
            {
                present.Set("FreshMaterial0TextureId", raw);
                RefuseBeforeSource(present);
                Check(present.Text("FreshMaterial0TextureId") == raw, "invalid last raw ID retained");
            }
            present.Fire("FreshDocumentUndo");
            present.Set("FreshMaterial0SourceVariant", "unrecognized-retained");
            RefuseBeforeSource(present);
            Check(present.Text("FreshMaterial0SourceVariant") == "unrecognized-retained", "unknown variant not normalized");
            present.Fire("FreshDocumentUndo");
            var kind = present.Find<ComboBox>("FreshMaterial0Kind"); var originalItems = kind.ItemsSource;
            object unknown = new();
            kind.ItemsSource = new object[] { Retail, Color, "Unknown kind", unknown };
            foreach (object value in new object[] { "Unknown kind", unknown })
            {
                kind.SelectedItem = value; Pump();
                Check(ReferenceEquals(kind.SelectedItem, value), "actual unknown/non-string selection installed");
                RefuseBeforeSource(present);
                Check(ReferenceEquals(kind.SelectedItem, value), "unknown selection retained after direct event");
            }
            kind.ItemsSource = originalItems; present.Fire("FreshDocumentUndo");
            present.Find<ComboBox>("FreshMaterial0SourceLevel").SelectedItem = null; Pump();
            RefuseBeforeSource(present);
            present.Fire("FreshDocumentUndo");
            present.SetKind(0, Color); present.SetKind(1, Color);
            RefuseBeforeSource(present);
            present.Fire("FreshDocumentUndo");
            present.Fire("FreshWizardAdd");
            RefuseBeforeSource(present);
            Check(present.Window.HasUnappliedFields, "blank pending Add remains dirty and cannot start inspection");
            present.Fire("FreshWizardCancelAdd");

            // Inspect parses only raw materials, not independent invalid Base/route text.
            // Do not click a valid inspection in this controls-only no-worker fixture.
            present.Set("FreshFloorWidth", "bad-base");
            Check(inspect.IsEffectivelyEnabled && present.Text("FreshFloorWidth") == "bad-base",
                "valid raw material intent is independently inspectable without applying invalid Base");
            Check(present.Window.CurrentAggregateIntent!.Base.FloorWidth == 2048, "no partial application");

            Fixture schema1 = Add("schema1", Document());
            schema1.OpenAggregate(); RefuseBeforeSource(schema1);
            schema1.Fire("FreshMaterialsEnable");
            RefuseBeforeSource(schema1); // Explicit upgrade starts as two color slots.
            schema1.SetKind(0, Retail);
            schema1.Find<ComboBox>("FreshMaterial0SourceLevel").SelectedItem = 14; Pump();
            schema1.Set("FreshMaterial0TextureId", "0");
            Check(schema1.Find<Button>("FreshMaterialsInspect").IsEffectivelyEnabled &&
                schema1.Window.CurrentAggregateIntent!.SchemaVersion == 1 && schema1.Window.HasUnappliedFields,
                "pending explicit upgrade is inspectable raw intent, not an applied/saved schema2");

            Fixture absent = Add("absent", null); absent.OpenAggregate();
            RefuseBeforeSource(absent);
            Check(absent.Window.CurrentAggregateIntent == null, "inspection never forks or infers a document");
            Fixture legacy = Add("legacy", Document().WithFloorMaterials(Pair()));
            Check(!legacy.Find<Button>("FreshMaterialsInspect").IsVisible, "legacy retains hidden but discoverable controls");
            RefuseBeforeSource(legacy);
            foreach (var f in fixtures) RequireEmptyPreviews(f);
        }
        catch (Exception error) { failures.Add(error); }
        finally
        {
            // All fixtures retained. Every close and every actual file observation attempted.
            foreach (var f in fixtures) Attempt(f.Close);
            foreach (var f in fixtures) Attempt(f.RequireProtected);
            Console.WriteLine("MATERIAL_INSPECTION_CONTROLS_RETAINED path=" + root);
        }
        if (failures.Count == 1) ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures.Count > 1) throw new IOException("Inspection control case and independent final failures.", new AggregateException(failures));
        Console.WriteLine($"FRESH_MATERIAL_INSPECTION_CONTROLS_OK checks={checks} sourceOpened=false workerStarted=false publication=false");
        Fixture Add(string id, NativeFreshLevelDocumentIntent? intent)
        { var f = new Fixture(root, id, intent); fixtures.Add(f); f.OpenWindow(); return f; }
        void Attempt(Action action) { try { action(); } catch (Exception error) { failures.Add(error); } }
    }

    private static void RefuseBeforeSource(Fixture f)
    {
        Check(!f.Find<Button>("FreshMaterialsInspect").IsEffectivelyEnabled, "invalid/absent intent disables Inspect");
        f.Fire("FreshMaterialsInspect"); // Direct event must repeat the closed guard.
        Check(f.OwnerCalls == 0 && f.SourceCalls == 0, "direct refused event acquires no owner/source/work");
    }
    private static void RequireEmptyPreviews(Fixture f) => Check(
        f.Find<Image>("FreshMaterial0Preview").Source == null && f.Find<Image>("FreshMaterial1Preview").Source == null,
        "no source bitmap, placeholder, auto inspection or schematic texture");
    private static NativeFreshFloorMaterialsIntent Pair() => NativeFreshFloorMaterialsIntent.Create(
        NativeFreshFloorMaterialIntent.RetailTerrain(Variant, 14, 0),
        NativeFreshFloorMaterialIntent.RetailTerrain(Variant, 70, 1));
    private static NativeFreshLevelDocumentIntent Document() => NativeFreshLevelDocumentIntent.Create(
        NativeId65FreshDocumentIntent.Create(8448, 8448, 8704, 2048, 2048, 8960, 8960, 64,
            new(80, 128, 112), new(128, 96, 64), 19), []);
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidDataException("Material inspection controls: " + message); checks++; }
    private static void Pump() => Dispatcher.UIThread.RunJobs();

    private sealed class Fixture
    {
        internal readonly EditorProjectLayout Project;
        internal NativeFreshDocumentWindow Window = null!;
        internal int OwnerCalls, SourceCalls;
        private readonly Dictionary<string, byte[]?> protectedFiles = new(StringComparer.Ordinal);
        internal Fixture(string parent, string id, NativeFreshLevelDocumentIntent? intent)
        {
            string root = Path.Combine(parent, id); Directory.CreateDirectory(root); Project = new(root, id);
            File.WriteAllText(Project.ManifestPath, "{\"schemaVersion\":1,\"projectId\":\"" + id + "\",\"sentinel\":true}");
            NativeId65FreshDocumentStore.Save(NativeId65FreshDocumentStore.Capture(Project), Document().Base);
            if (intent != null) NativeFreshLevelDocumentStore.Save(NativeFreshLevelDocumentStore.Capture(Project), intent);
            foreach (string path in new[] { Project.ManifestPath, Path.Combine(root, NativeId65FreshDocumentStore.FileName),
                Path.Combine(root, NativeFreshLevelDocumentStore.FileName) })
                protectedFiles.Add(path, File.Exists(path) ? File.ReadAllBytes(path) : null);
        }
        internal void OpenWindow()
        {
            Window = new(Project, () => true,
                () => { SourceCalls++; throw new InvalidOperationException("Control-only test must not resolve original source."); },
                () => { OwnerCalls++; throw new InvalidOperationException("Control-only test must not acquire editor owner."); });
            Window.DecisionForTesting = _ => Task.FromResult(FreshDocumentDecision.Cancel);
            Window.Show(); Pump();
        }
        internal T Find<T>(string name) where T : Control
        {
            var matches = Window.GetLogicalDescendants().OfType<T>().Where(c => c.Name == name).ToArray();
            if (matches.Length != 1) throw new InvalidDataException($"Missing inspection control: {name}; actual count={matches.Length}.");
            return matches[0];
        }
        internal void OpenAggregate() { Find<ComboBox>("FreshDocumentMode").SelectedItem = "Level aggregate"; Pump(); }
        internal void Fire(string name)
        { var b = Find<Button>(name); b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, b)); Pump(); }
        internal void Set(string name, string text) { Find<TextBox>(name).Text = text; Pump(); }
        internal string Text(string name) => Find<TextBox>(name).Text ?? "";
        internal void SetKind(int slot, string value) { Find<ComboBox>($"FreshMaterial{slot}Kind").SelectedItem = value; Pump(); }
        internal void Close()
        { if (Window == null || !Window.IsVisible) return; Window.DecisionForTesting = _ => Task.FromResult(FreshDocumentDecision.Discard); Window.Close(); Pump(); }
        internal void RequireProtected()
        {
            List<Exception> failures = [];
            foreach (var pair in protectedFiles)
                try { Check(pair.Value == null ? !File.Exists(pair.Key) : File.ReadAllBytes(pair.Key).SequenceEqual(pair.Value),
                    "exact complete manifest/legacy/aggregate or actual absent target retained"); }
                catch (Exception error) { failures.Add(error); }
            try { Check(OwnerCalls == 0 && SourceCalls == 0, "no control case resolved or acquired source ownership"); }
            catch (Exception error) { failures.Add(error); }
            if (failures.Count == 1) ExceptionDispatchInfo.Capture(failures[0]).Throw();
            if (failures.Count > 1) throw new IOException("Control fixture final checks.", new AggregateException(failures));
        }
    }
}
