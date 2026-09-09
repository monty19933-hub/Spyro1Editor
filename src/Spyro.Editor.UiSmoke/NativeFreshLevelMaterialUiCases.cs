using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Persistence;

// Headless existing-window controls/tiny actual Store only.
// No material picker, native source, texture rendering, compiler or publisher.
internal static class NativeFreshLevelMaterialUiCases
{
    private const string A = "11111111111111111111111111111111", B = "22222222222222222222222222222222";
    private const string Variant = "spyro1-usa-retail-mode2-2352-v1";
    private const string Notice = "Selected materials are not rendered in this schematic.";
    private const string Summary = "Triangle 0: retail terrain record · spyro1-usa-retail-mode2-2352-v1 · WAD 14 · texture 0\nTriangle 1: base floor color";
    private static int _checks;

    internal static void Run(string artifacts)
    {
        _checks = 0;
        string root = Directory.CreateTempSubdirectory("spyro-material-ui-").FullName;
        var fixtures = new List<Fixture>(); bool passed = false;
        try
        {
            Session(new Fixture(root, "session", Document()).Track(fixtures));
            ExistingWindow(new Fixture(root, "window", Document()).Track(fixtures), artifacts);
            VersionAndFailure(new Fixture(root, "version1", LegacyDocument()).Track(fixtures));
            var clear = LegacyDocument().WithFloorMaterials(NativeFreshFloorMaterialsIntent.Create(Color(), Color()));
            using (var f = new Fixture(root, "clear", clear).Track(fixtures))
            {
                f.OpenAggregate();
                Check(f.Window.CurrentAggregateIntent!.SchemaVersion == 2 && f.Label("FreshLevelMaterialPreviewNotice") == Notice &&
                    f.Label("FreshLevelMaterialSummary") == "Triangle 0: base floor color\nTriangle 1: base floor color",
                    "schema2 both-color clear stays visibly schema2/unrendered, never inferred schema1");
            }
            foreach (var f in fixtures) f.RequireProtected();
            passed = true;
        }
        finally
        {
            foreach (var f in fixtures) f.Dispose();
            if (passed) Directory.Delete(root, recursive: true);
            else Console.WriteLine("MATERIAL_UI_FAILURE_RETAINED path=" + root);
        }
        Console.WriteLine($"FRESH_LEVEL_MATERIAL_UI_OK checks={_checks} headless=true sourceRequired=false nativeRendering=false publication=false");
    }

    private static void Session(Fixture f)
    {
        var snapshot = NativeFreshLevelDocumentStore.Capture(f.Project);
        var s = new NativeFreshLevelDocumentSession(snapshot);
        var original = s.CurrentIntent!; var materials = original.FloorMaterials!;
        s.Select(A); s.ReplaceWizardRoute(A, 9984,9984,8910,9472,9984);
        Check(s.CurrentIntent!.SchemaVersion == 2 && ReferenceEquals(s.CurrentIntent.FloorMaterials, materials), "session Replace preserves schema/materials");
        s.Undo(); Check(ReferenceEquals(s.CurrentIntent, original) && s.SelectedEditorId == A, "undo restores actual old whole object and stable selection");
        s.Apply(NativeFreshLevelDocumentCodec.Parse(Bytes(original)));
        Check(ReferenceEquals(s.CurrentIntent, original) && s.CanRedo, "canonical no-op preserves actual current and redo with materials");
        s.Redo();
        var added = s.AddWizardRoute(9728,9728,8909,9472,9728);
        Check(s.CurrentIntent!.FloorMaterials == materials && added.OriginZ == 8909 && s.SelectedEditorId == added.EditorId,
            "explicit Add creates only new route ID and preserves material pair");
        s.RemovePlacement(added.EditorId);
        Check(s.CurrentIntent!.FloorMaterials == materials && s.SelectedEditorId == null, "Remove retains materials and clears missing selection");
        s.Undo(); Check(s.SelectedEditorId == added.EditorId && s.CurrentIntent!.FloorMaterials == materials, "undo Remove restores same ID/materials");
        s.Redo();
        var applied = s.CurrentIntent!;
        var saved = s.Save();
        Check(ReferenceEquals(s.CurrentIntent, applied) && !ReferenceEquals(saved.Intent, applied) &&
            saved.Intent!.SchemaVersion == 2 && Bytes(saved.Intent).SequenceEqual(Bytes(applied)),
            "Save retains applied reference but actual parsed Snapshot is future authority, including materials");
        s.Reopen(); Check(ReferenceEquals(s.CurrentIntent, s.Snapshot.Intent) && s.CurrentIntent!.SchemaVersion == 2, "Reopen installs actual whole saved schema2");
        s.Apply(LegacyDocument()); // Explicit applied history, not automatic downgrade.
        s.Apply(s.CurrentIntent!.WithFloorMaterials(materials));
        Check(s.Undo() && s.CurrentIntent!.SchemaVersion == 1 && s.CurrentIntent.FloorMaterials == null,
            "Undo may honestly restore the actual earlier schema1 history item");
        Check(s.Redo() && s.CurrentIntent!.SchemaVersion == 2 && s.CurrentIntent.FloorMaterials == materials, "Redo restores explicit upgrade unchanged");
        var all = s.CurrentIntent.WithContent(s.CurrentIntent.Base,
            Enumerable.Range(1,128).Select(i => NativeFreshWizardRouteIntent.Create(i.ToString("x32"),9984,9984,8909,9472,9984)));
        s.Apply(all); s.Save(); s.Reopen();
        Check(s.CurrentIntent!.Placements.Select(p=>p.EditorId).SequenceEqual(all.Placements.Select(p=>p.EditorId)) &&
            s.CurrentIntent.Placements.Cast<NativeFreshWizardRouteIntent>().All(p=>p.OriginZ==8909) && s.CurrentIntent.SchemaVersion==2,
            "session Save/Reopen retains every128 stable ID/non-grid Z and material schema");
    }

    private static void ExistingWindow(Fixture f, string artifacts)
    {
        byte[] original = File.ReadAllBytes(f.Path); f.OpenAggregate();
        f.RequireNotice();
        Check(f.Window.Preview.MaterialPreviewNotice == Notice &&
            ReferenceEquals(f.Window.Preview.AggregateIntent, f.Window.CurrentAggregateIntent), "preview derives exact current intent and unrendered notice");
        f.Select(A); var materials = f.Window.CurrentAggregateIntent!.FloorMaterials;
        f.Set("FreshHeading", "75"); f.Set("FreshWizardOriginZ", "invalid"); f.Click("FreshDocumentApply");
        Check(f.Window.CurrentAggregateIntent.Base.Heading == 64 && f.Window.HasUnappliedFields &&
            f.Window.CurrentAggregateIntent.FloorMaterials == materials && File.ReadAllBytes(f.Path).SequenceEqual(original),
            "invalid selected route refuses entire raw Base+route Apply without material loss");
        f.Set("FreshWizardOriginZ", "8910"); f.Click("FreshDocumentApply");
        Check(f.Window.CurrentAggregateIntent.Base.Heading == 75 && f.Window.CurrentAggregateIntent.FloorMaterials == materials,
            "Window whole-form Apply preserves material pair while changing selected route and Base atomically");
        f.Click("FreshDocumentUndo"); f.Click("FreshDocumentApply");
        Check(f.Button("FreshDocumentRedo").IsEnabled && f.Window.CurrentAggregateIntent.FloorMaterials == materials,
            "no-op Window Apply retains redo and materials");
        f.Click("FreshDocumentRedo"); f.Click("FreshWizardMoveDown");
        Check(f.Window.CurrentAggregateIntent.Placements.Select(p=>p.EditorId).SequenceEqual(new[]{B,A}) &&
            f.Window.CurrentAggregateIntent.FloorMaterials == materials, "Move preserves slot order independently from placement reorder");
        f.Click("FreshWizardAdd");
        f.SetRoute(9728,9728,8909,9472,9728); f.Click("FreshDocumentApply");
        Check(f.Window.CurrentAggregateIntent.Placements.Count == 3 && f.Window.CurrentAggregateIntent.FloorMaterials == materials,
            "Window Add routes through material-preserving session");
        f.Click("FreshWizardRemove"); f.Click("FreshDocumentSave"); f.Click("FreshDocumentReopen"); f.RequireNotice();
        var disk = NativeFreshLevelDocumentStore.Capture(f.Project);
        Check(disk.Intent!.SchemaVersion == 2 && Bytes(disk.Intent).SequenceEqual(Bytes(f.Window.CurrentAggregateIntent!)) &&
            disk.Intent.Placements.Select(p=>p.EditorId).SequenceEqual(new[]{B,A}) && !f.Window.HasUnsavedChanges,
            "Save/Reopen persists all edited content and both material slots");
        Check(!f.Button("FreshDocumentResearch").IsEnabled, "schema2 with Wizard placements does not enable material-only research export");
        f.Fire("FreshDocumentResearch");
        Check(f.SourceCalls == 0 && f.PickerCalls == 0 &&
            f.Label("FreshDocumentStatus").Contains("two retail materials",StringComparison.Ordinal) &&
            f.Label("FreshDocumentStatus").Contains("no placements",StringComparison.Ordinal),
            "direct schema2-with-Wizards export refuses before source/picker/worker with exact material-only restrictions");
        var floorBounds = f.Window.Preview.FloorSchematicBounds ?? throw new InvalidDataException("Actual projected floor required");
        Check(floorBounds.Left >= 0 && floorBounds.Right <= f.Window.Preview.Bounds.Width && floorBounds.Top >= 76 &&
            floorBounds.Bottom < f.Window.Preview.SchematicCaptionTop,
            "complete schema2 diamond, including lower vertex and contained XY routes, stays above the actual notice caption");
        using var image = f.Window.CaptureRenderedFrame() ?? throw new InvalidDataException("Expected actual headless material notice frame");
        image.Save(Path.Combine(artifacts,"fresh-level-schema2-unrendered.png"));
    }

    private static void VersionAndFailure(Fixture f)
    {
        byte[] before = File.ReadAllBytes(f.Path); f.OpenAggregate();
        Check(f.Label("FreshLevelSchemaVersion") == "Aggregate schema 1" &&
            !f.Find<TextBlock>("FreshLevelMaterialPreviewNotice").IsVisible && f.Window.Preview.MaterialPreviewNotice == "",
            "schema1 opening has no schema2 notice or automatic upgrade");
        f.Click("FreshDocumentSave"); Check(File.ReadAllBytes(f.Path).SequenceEqual(before), "unedited schema1 Save is still byte exact");
        var edited = Document(); NativeFreshLevelDocumentStore.Save(NativeFreshLevelDocumentStore.Capture(f.Project),edited);
        f.Click("FreshDocumentReopen"); f.RequireNotice(); f.Select(A);
        f.Set("FreshWizardOriginZ","8911"); var prior = f.Window.CurrentAggregateIntent;
        File.WriteAllText(f.Path,"{malformed schema2 fixture}"); f.Click("FreshDocumentSave");
        Check(f.Window.CurrentAggregateIntent!.SchemaVersion==2 &&
            f.Window.CurrentAggregateIntent.FloorMaterials == prior!.FloorMaterials && f.Window.HasUnsavedChanges &&
            File.ReadAllText(f.Path)=="{malformed schema2 fixture}", "failed Save preserves material intent and external bytes; no partial Store adoption");
        f.Window.DecisionForTesting = _ => Task.FromResult(FreshDocumentDecision.Discard);
        var applied = f.Window.CurrentAggregateIntent; f.Click("FreshDocumentReopen");
        Check(ReferenceEquals(f.Window.CurrentAggregateIntent,applied), "failed Reopen after Discard retains actual prior whole schema2 state");
    }

    private static NativeFreshFloorMaterialIntent Color() => NativeFreshFloorMaterialIntent.BaseFloorColor();
    private static NativeFreshLevelDocumentIntent LegacyDocument() => NativeFreshLevelDocumentIntent.Create(
        NativeId65FreshDocumentIntent.Create(8448,8448,8704,2048,2048,8960,8960,64,new(80,128,112),new(128,96,64),19),
        [NativeFreshWizardRouteIntent.Create(A,9984,9984,8909,9472,9984),NativeFreshWizardRouteIntent.Create(B,9728,9984,8909,9472,9984)]);
    private static NativeFreshLevelDocumentIntent Document() => LegacyDocument().WithFloorMaterials(NativeFreshFloorMaterialsIntent.Create(
        NativeFreshFloorMaterialIntent.RetailTerrain(Variant,14,0),Color()));
    private static byte[] Bytes(NativeFreshLevelDocumentIntent value) => NativeFreshLevelDocumentCodec.SerializeCanonical(value);
    private static void Check(bool value,string label) { if(!value) throw new InvalidDataException("Material UI: "+label); _checks++; }
    private static void Pump() => Dispatcher.UIThread.RunJobs();

    private sealed class Fixture : IDisposable
    {
        internal readonly EditorProjectLayout Project;
        internal readonly NativeFreshDocumentWindow Window;
        internal int SourceCalls,PickerCalls;
        internal string Path => System.IO.Path.Combine(Project.RootPath,NativeFreshLevelDocumentStore.FileName);
        private readonly byte[] _manifest,_legacy;
        internal Fixture(string root,string id,NativeFreshLevelDocumentIntent value)
        {
            string path=System.IO.Path.Combine(root,id); Directory.CreateDirectory(path); Project=new(path,id);
            File.WriteAllText(Project.ManifestPath,"{\"schemaVersion\":1,\"projectId\":\""+id+"\",\"unrelated\":\"preserve\"}\n");
            _manifest=File.ReadAllBytes(Project.ManifestPath);
            var legacy=NativeId65FreshDocumentStore.Save(NativeId65FreshDocumentStore.Capture(Project),value.Base);
            _legacy=File.ReadAllBytes(legacy.Path);
            NativeFreshLevelDocumentStore.Save(NativeFreshLevelDocumentStore.Capture(Project),value);
            Window=new(Project,()=>true);
            Window.DecisionForTesting=_=>Task.FromResult(FreshDocumentDecision.Cancel);
            Window.ResearchSourcePathsForTesting=()=>{SourceCalls++; throw new InvalidOperationException("Schema2 must not resolve sources");};
            Window.ResearchOutputPickerForTesting=_=>{PickerCalls++; throw new InvalidOperationException("Schema2 must not select output");};
            Window.Show(); Pump();
        }
        internal Fixture Track(List<Fixture> fixtures) { fixtures.Add(this); return this; }
        internal T Find<T>(string name) where T:Control => Window.GetLogicalDescendants().OfType<T>().Single(c=>c.Name==name);
        internal Button Button(string name)=>Find<Button>(name);
        internal string Label(string name)=>Find<TextBlock>(name).Text??"";
        internal void OpenAggregate() { Find<ComboBox>("FreshDocumentMode").SelectedItem="Level aggregate"; Pump(); }
        internal void Select(string id) { var rows=Find<ListBox>("FreshLevelPlacements"); rows.SelectedItem=rows.ItemsSource!.Cast<NativeFreshPlacementIntent>().Single(p=>p.EditorId==id); Pump(); }
        internal void Set(string name,string text) { Find<TextBox>(name).Text=text; Pump(); }
        internal void SetRoute(params int[] values)
        { string[] names=["FreshWizardOriginX","FreshWizardOriginY","FreshWizardOriginZ","FreshWizardEndX","FreshWizardEndY"]; for(int i=0;i<5;i++) Set(names[i],values[i].ToString()); }
        internal void Fire(string name) { var b=Button(name); b.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent,b)); Pump(); }
        internal void Click(string name) { Check(Button(name).IsEffectivelyEnabled,"enabled "+name); Fire(name); }
        internal void RequireNotice()
        {
            Check(Label("FreshLevelSchemaVersion")=="Aggregate schema 2" && Label("FreshLevelMaterialSummary")==Summary &&
                Find<TextBlock>("FreshLevelSchemaVersion").IsEffectivelyVisible && Find<TextBlock>("FreshLevelMaterialSummary").IsEffectivelyVisible &&
                Find<TextBlock>("FreshLevelMaterialPreviewNotice").IsEffectivelyVisible && Label("FreshLevelMaterialPreviewNotice")==Notice,
                "existing window visibly labels version, exact material source intent and unrendered schematic");
        }
        internal void RequireProtected()=>Check(File.ReadAllBytes(Project.ManifestPath).SequenceEqual(_manifest) &&
            File.ReadAllBytes(System.IO.Path.Combine(Project.RootPath,NativeId65FreshDocumentStore.FileName)).SequenceEqual(_legacy), "whole manifest/legacy unchanged");
        public void Dispose() { if(!Window.IsVisible)return; Window.DecisionForTesting=_=>Task.FromResult(FreshDocumentDecision.Discard); Window.Close(); Pump(); }
    }
}
