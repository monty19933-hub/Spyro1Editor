using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Persistence;
using System.Runtime.ExceptionServices;

// Headless existing-window/tiny Store controls only; no source handles,
// source inspection, texture rendering, compilation or publication.
internal static class NativeFreshMaterialPickerUiCases
{
    private const string Variant = "spyro1-usa-retail-mode2-2352-v1";
    private const string ColorKind = "Base floor color", RetailKind = "Retail terrain record";
    private const string A = "11111111111111111111111111111111", B = "22222222222222222222222222222222";
    private static int checks;
    private static string artifacts = "";

    internal static void Run()
    {
        checks = 0;
        string root = Directory.CreateTempSubdirectory("spyro-material-picker-ui-").FullName;
        artifacts = root;
        List<Fixture> fixtures = [];
        List<Exception> errors = [];
        try
        {
            Upgrade(Add("upgrade", Document()));
            Atomic(Add("atomic", Document().WithFloorMaterials(Pair(14, 0, 70, 1))));
            KindAndBaseFailures(Add("kind-base-failures", Document().WithFloorMaterials(Pair(14, 0, 70, 1))));
            PendingAdd(Add("pending-add", Document().WithFloorMaterials(Pair(14, 0, 70, 1))));
            AllIds(Add("all-ids", Document().WithContent(Document().Base,
                Enumerable.Range(1, 128).Select(i => NativeFreshWizardRouteIntent.Create(
                    i.ToString("x32"), 9984, 9984, 8909, 9472, 9984)))));
            Failures(Add("failures", Document().WithFloorMaterials(Pair(14, 0, 70, 1))));
            foreach (var choice in new[] { FreshDocumentDecision.Save, FreshDocumentDecision.Discard, FreshDocumentDecision.Cancel })
                Close(Add("close-" + choice.ToString().ToLowerInvariant(), Document()), choice);
        }
        catch (Exception error) { errors.Add(error); }
        finally
        {
            foreach (var f in fixtures)
                try { f.RequireProtected(); } catch (Exception error) { errors.Add(error); }
            foreach (var f in fixtures)
                try { f.Dispose(); } catch (Exception error) { errors.Add(error); }
            Console.WriteLine("MATERIAL_PICKER_FIXTURES_RETAINED path=" + root);
        }
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new IOException("Picker case and final fixture failures.", new AggregateException(errors));
        Console.WriteLine($"FRESH_MATERIAL_PICKER_UI_OK checks={checks} sourceOpened=false publication=false");
        Fixture Add(string id, NativeFreshLevelDocumentIntent document)
        { var f = new Fixture(root, id, document); fixtures.Add(f); return f; }
    }

    private static void Upgrade(Fixture f)
    {
        byte[] original = f.Disk();
        f.Open();
        Check(f.Window.CurrentAggregateIntent!.SchemaVersion == 1, "open is not upgrade");
        f.Click("FreshDocumentSave");
        Check(f.Disk().SequenceEqual(original), "untouched schema1 Save byte exact");
        f.Click("FreshMaterialsEnable");
        Check(f.Window.HasUnappliedFields && f.Window.CurrentAggregateIntent.SchemaVersion == 1 &&
            f.Kind(0) == ColorKind && f.Kind(1) == ColorKind, "explicit pending upgrade: two color tags, no applied schema change");
        Check(f.Find<ComboBox>("FreshMaterial0SourceLevel").SelectedItem == null &&
            f.Find<TextBox>("FreshMaterial0TextureId").Text == "", "no inferred source donor or ID");
        Check(f.Find<ComboBox>("FreshMaterial0SourceLevel").ItemsSource!.Cast<int>().SequenceEqual(
            Enumerable.Range(0, 35).Select(i => 10 + 2 * i)), "all35 exact data-entry choices without source IO");
        f.Click("FreshDocumentUndo");
        Check(!f.Window.HasUnappliedFields && f.Window.CurrentAggregateIntent.SchemaVersion == 1, "raw upgrade Undo restores prior form");
        f.Click("FreshMaterialsEnable"); f.Click("FreshDocumentApply");
        Check(f.Window.CurrentAggregateIntent.SchemaVersion == 2 &&
            f.Window.CurrentAggregateIntent.FloorMaterials!.Triangle0.Tag == "base-floor-color", "explicit applied upgrade");
        f.Click("FreshDocumentUndo");
        Check(f.Window.CurrentAggregateIntent.SchemaVersion == 1, "history may restore actual prior schema1");
        f.Click("FreshDocumentApply");
        Check(f.Button("FreshDocumentRedo").IsEnabled, "canonical no-op retains upgrade redo");
        f.Click("FreshDocumentRedo"); f.Click("FreshDocumentSave"); f.Click("FreshDocumentReopen");
        Check(f.Window.CurrentAggregateIntent!.SchemaVersion == 2 && !f.Window.HasUnsavedChanges &&
            f.Label("FreshLevelMaterialPreviewNotice") == "Selected materials are not rendered in this schematic.",
            "saved/reopened schema2 remains explicit/unrendered");
        f.Set("FreshFloorColor", "#112233"); f.Click("FreshDocumentSave");
        var saved = f.Snapshot().Intent!;
        Check(saved.Base.FloorColor.R == 0x11 && saved.Base.FloorColor.G == 0x22 &&
            saved.FloorMaterials!.Triangle0.Tag == "base-floor-color" && saved.FloorMaterials.Triangle1.Tag == "base-floor-color",
            "color tags follow actual changed Base color, no hidden per-slot color copy");
    }

    private static void Atomic(Fixture f)
    {
        f.Open(); f.Select(A); var original = f.Window.CurrentAggregateIntent!; byte[] disk = f.Disk();
        var originalPair = original.FloorMaterials!;
        f.Set("FreshHeading", "65"); f.Click("FreshDocumentApply");
        Check(ReferenceEquals(f.Window.CurrentAggregateIntent!.FloorMaterials, originalPair),
            "Base-only edit preserves actual unchanged material pair identity");
        f.Click("FreshDocumentUndo");
        f.Set("FreshWizardOriginZ", "8910"); f.Click("FreshDocumentApply");
        Check(ReferenceEquals(f.Window.CurrentAggregateIntent!.FloorMaterials, originalPair),
            "selected-route-only edit preserves actual unchanged material pair identity");
        f.Click("FreshDocumentUndo");
        f.Set("FreshMaterial0TextureId", "1"); f.Click("FreshDocumentApply");
        var slotOnly = f.Window.CurrentAggregateIntent!;
        Check(!ReferenceEquals(slotOnly, original) && IsRetail(slotOnly, 0, 14, 1) && IsRetail(slotOnly, 1, 70, 1) &&
            NativeFreshLevelDocumentCodec.SerializeCanonical(slotOnly.WithFloorMaterials(originalPair)).SequenceEqual(
                NativeFreshLevelDocumentCodec.SerializeCanonical(original)),
            "one-slot edit changes only material intent, preserving every unrelated Base/placement/ID value");
        f.Click("FreshDocumentUndo");
        f.Set("FreshHeading", "75"); f.Set("FreshWizardOriginZ", "8910");
        f.Retail(0, 70, "12"); f.Retail(1, 14, "1");
        f.Set("FreshMaterial0SourceVariant", "unknown-unapproved-variant");
        f.Click("FreshDocumentApply");
        Check(ReferenceEquals(original, f.Window.CurrentAggregateIntent) && f.Window.HasUnappliedFields &&
            f.Text("FreshMaterial0SourceVariant") == "unknown-unapproved-variant" && f.Disk().SequenceEqual(disk),
            "unknown raw variant refuses whole Base/route/pair; retained, never defaulted");
        f.Set("FreshMaterial0SourceVariant", Variant);
        foreach (string id in new[] { "", "invalid", "-1", "128" })
        {
            f.Set("FreshMaterial0TextureId", id); f.Click("FreshDocumentApply");
            Check(ReferenceEquals(original, f.Window.CurrentAggregateIntent) && f.Text("FreshMaterial0TextureId") == id,
                "invalid/blank raw ID is retained and cannot partially apply other fields");
        }
        f.Set("FreshMaterial0TextureId", "12");
        f.Find<ComboBox>("FreshMaterial0SourceLevel").SelectedItem = null; Pump();
        f.Click("FreshDocumentApply");
        Check(ReferenceEquals(original, f.Window.CurrentAggregateIntent), "missing WAD cannot choose default donor");
        f.Find<ComboBox>("FreshMaterial0SourceLevel").SelectedItem = 70; Pump();
        f.Click("FreshDocumentApply");
        var applied = f.Window.CurrentAggregateIntent!;
        Check(applied.Base.Heading == 75 && ((NativeFreshWizardRouteIntent)applied.Placements[0]).OriginZ == 8910 &&
            IsRetail(applied, 0, 70, 12) && IsRetail(applied, 1, 14, 1),
            "one atomic whole-form application retains native-ineligible but neutral-valid record");
        Check(applied.Placements.Select(p => p.EditorId).SequenceEqual(new[] { A, B }), "material order does not reorder placements");
        CaptureRetailControls(f);
        f.Click("FreshDocumentUndo"); Check(ReferenceEquals(f.Window.CurrentAggregateIntent, original), "one Undo restores entire earlier object");
        f.Click("FreshDocumentRedo");
        f.Retail(0, 14, "1"); f.Retail(1, 14, "1"); f.Click("FreshDocumentSave"); f.Click("FreshDocumentReopen");
        Check(IsRetail(f.Window.CurrentAggregateIntent!, 0, 14, 1) && IsRetail(f.Window.CurrentAggregateIntent!, 1, 14, 1),
            "same descriptor in both independent ordered slots saves normally");
        f.SetKind(0, ColorKind); f.SetKind(1, ColorKind); f.Click("FreshDocumentSave"); f.Click("FreshDocumentReopen");
        Check(f.Window.CurrentAggregateIntent!.SchemaVersion == 2 &&
            f.Window.CurrentAggregateIntent.FloorMaterials!.Triangle0.Tag == "base-floor-color" &&
            f.Window.CurrentAggregateIntent.FloorMaterials.Triangle1.Tag == "base-floor-color", "clearing both slots never downgrades");
        f.Fire("FreshDocumentResearch");
        Check(f.SourceCalls == 0 && f.Label("FreshDocumentStatus").Contains("unavailable", StringComparison.OrdinalIgnoreCase),
            "material editing never enables whole-level export or source access");
    }

    private static void KindAndBaseFailures(Fixture f)
    {
        f.Open(); f.Select(A); var original = f.Window.CurrentAggregateIntent!; byte[] disk = f.Disk();
        var kind = f.Find<ComboBox>("FreshMaterial0Kind");
        var items = kind.ItemsSource;
        object nonString = new();
        kind.ItemsSource = new object[] { ColorKind, RetailKind, "Unknown material kind", nonString };
        foreach (object invalid in new object[] { "Unknown material kind", nonString })
        {
            kind.SelectedItem = invalid; Pump();
            Check(ReferenceEquals(kind.SelectedItem, invalid), "fixture installs actual unknown/non-string Kind, not just null selection");
            f.Set("FreshHeading", "75"); f.Set("FreshWizardOriginZ", "8910"); f.Retail(1, 14, "1");
            f.Click("FreshDocumentApply");
            Check(ReferenceEquals(f.Window.CurrentAggregateIntent, original) &&
                ReferenceEquals(kind.SelectedItem, invalid) && f.Text("FreshHeading") == "75" &&
                f.Text("FreshWizardOriginZ") == "8910" && f.Disk().SequenceEqual(disk),
                "unknown/non-string Kind refuses the entire changed Base/route/pair and retains exact raw selection");
        }
        kind.ItemsSource = items; kind.SelectedItem = RetailKind; Pump();
        f.Click("FreshDocumentUndo");
        f.Set("FreshFloorWidth", "15"); f.Set("FreshWizardOriginZ", "8910"); f.Retail(0, 70, "0");
        foreach (string action in new[] { "FreshDocumentApply", "FreshDocumentSave" })
        {
            f.Click(action);
            Check(ReferenceEquals(f.Window.CurrentAggregateIntent, original) &&
                ReferenceEquals(f.Window.CurrentAggregateIntent!.Base, original.Base) &&
                ReferenceEquals(f.Window.CurrentAggregateIntent.FloorMaterials, original.FloorMaterials) &&
                f.Window.CurrentAggregateIntent.Placements.Select(p => p.EditorId).SequenceEqual(new[] { A, B }) &&
                f.Text("FreshFloorWidth") == "15" && f.Text("FreshMaterial0TextureId") == "0" &&
                f.Text("FreshWizardOriginZ") == "8910" && f.Disk().SequenceEqual(disk) && f.SourceCalls == 0,
                "invalid Base with valid changed materials leaves all applied Base/route/pair/IDs and disk unchanged");
        }
    }

    private static void PendingAdd(Fixture f)
    {
        f.Open(); f.Select(A); var original = f.Window.CurrentAggregateIntent!;
        f.Set("FreshMaterial0TextureId", "bad");
        f.Select(B);
        Check(f.Window.Preview.SelectedEditorId == A && f.Text("FreshMaterial0TextureId") == "bad",
            "selection cannot discard invalid material raw fields");
        f.Fire("FreshWizardMoveDown"); f.Fire("FreshWizardAdd");
        Check(ReferenceEquals(f.Window.CurrentAggregateIntent, original), "reorder/Add guarded by material dirtiness");
        f.Click("FreshDocumentUndo"); f.Click("FreshWizardAdd");
        Check(!f.Find<TextBox>("FreshMaterial0TextureId").IsEffectivelyEnabled, "pending Add locks material controls");
        foreach (var pair in new[] { ("FreshWizardOriginX","9728"), ("FreshWizardOriginY","9728"),
            ("FreshWizardOriginZ","8909"), ("FreshWizardEndX","9472"), ("FreshWizardEndY","9728") }) f.Set(pair.Item1, pair.Item2);
        f.Set("FreshMaterial0TextureId", "1"); // direct-event drift despite disabled UI
        f.Click("FreshDocumentApply");
        Check(ReferenceEquals(f.Window.CurrentAggregateIntent, original) && original.Placements.Count == 2 &&
            f.Window.HasUnappliedFields, "pending material drift refuses before route/ID creation");
        f.Click("FreshWizardCancelAdd");
        Check(ReferenceEquals(f.Window.CurrentAggregateIntent, original) && !f.Window.HasUnappliedFields, "explicit Cancel restores whole raw form");
    }

    private static void CaptureRetailControls(Fixture f)
    {
        // Actual default-size headless Window frames, not just the schematic.
        // Both slots remain retail; two scroll positions make their fields readable.
        foreach (var (control, name) in new[] { ("FreshMaterial0TextureId", "material-picker-retail-slot0.png"),
            ("FreshMaterial1TextureId", "material-picker-retail-slot1.png") })
        {
            f.Find<Control>(control).BringIntoView(); Pump();
            f.Window.InvalidateVisual(); Pump();
            using var frame = f.Window.CaptureRenderedFrame() ?? throw new InvalidDataException("Expected actual picker Window frame.");
            string path = System.IO.Path.Combine(artifacts, name); frame.Save(path);
            Console.WriteLine("MATERIAL_PICKER_WINDOW_FRAME path=" + path);
        }
        var scroll = f.Find<TextBox>("FreshMaterial1TextureId").GetLogicalAncestors().OfType<ScrollViewer>().First();
        Check(scroll.Offset.Y > 0, "actual material controls are reachable through the existing inspector scroll");
    }

    private static void AllIds(Fixture f)
    {
        f.Open(); var ids = f.Window.CurrentAggregateIntent!.Placements.Select(p => p.EditorId).ToArray();
        f.Click("FreshMaterialsEnable"); f.Retail(0, 16, "1"); f.Retail(1, 40, "70");
        f.Click("FreshDocumentSave"); f.Click("FreshDocumentReopen");
        Check(f.Window.CurrentAggregateIntent!.Placements.Select(p => p.EditorId).SequenceEqual(ids) &&
            f.Window.CurrentAggregateIntent.Placements.Cast<NativeFreshWizardRouteIntent>().All(p => p.OriginZ == 8909) &&
            IsRetail(f.Window.CurrentAggregateIntent, 0, 16, 1) && IsRetail(f.Window.CurrentAggregateIntent, 1, 40, 70),
            "all128 exact IDs and non-grid Z preserved; static eligibility not a neutral Save condition");
    }

    private static void Failures(Fixture f)
    {
        f.Open(); f.Retail(0, 70, "12");
        File.AppendAllText(f.Path, " "); byte[] external = f.Disk();
        f.Click("FreshDocumentSave");
        Check(f.Disk().SequenceEqual(external) && f.Window.HasUnsavedChanges &&
            IsRetail(f.Window.CurrentAggregateIntent!, 0, 70, 12), "rawCAS failure preserves applied material draft and external bytes");
        f.Set("FreshMaterial0TextureId", "invalid-retained"); var applied = f.Window.CurrentAggregateIntent;
        File.WriteAllText(f.Path, "{malformed-owned-fixture}");
        f.Window.DecisionForTesting = _ => Task.FromResult(FreshDocumentDecision.Discard);
        f.Click("FreshDocumentReopen");
        Check(ReferenceEquals(f.Window.CurrentAggregateIntent, applied) && f.Text("FreshMaterial0TextureId") == "invalid-retained",
            "failed target capture after Discard keeps prior applied and raw materials");
    }

    private static void Close(Fixture f, FreshDocumentDecision decision)
    {
        f.Open(); byte[] before = f.Disk(); f.Click("FreshMaterialsEnable"); f.Retail(0, 14, "1");
        f.Window.DecisionForTesting = _ => Task.FromResult(decision); f.Window.Close(); Pump();
        if (decision == FreshDocumentDecision.Save)
            Check(!f.Window.IsVisible && IsRetail(f.Snapshot().Intent!, 0, 14, 1), "close Save applies complete material raw form and saves");
        else if (decision == FreshDocumentDecision.Discard)
            Check(!f.Window.IsVisible && f.Disk().SequenceEqual(before), "close Discard leaves original saved bytes");
        else Check(f.Window.IsVisible && f.Window.HasUnappliedFields && f.Text("FreshMaterial0TextureId") == "1" &&
            f.Disk().SequenceEqual(before), "close Cancel preserves raw material text");
    }

    private static NativeFreshFloorMaterialsIntent Pair(int a, int x, int b, int y) => NativeFreshFloorMaterialsIntent.Create(
        NativeFreshFloorMaterialIntent.RetailTerrain(Variant,a,x), NativeFreshFloorMaterialIntent.RetailTerrain(Variant,b,y));
    private static bool IsRetail(NativeFreshLevelDocumentIntent d, int slot, int wad, int id) =>
        (slot == 0 ? d.FloorMaterials!.Triangle0 : d.FloorMaterials!.Triangle1) is NativeFreshRetailTerrainMaterialIntent m &&
        m.SourceVariant == Variant && m.SourceWadEntry == wad && m.TextureId == id;
    private static NativeFreshLevelDocumentIntent Document() => NativeFreshLevelDocumentIntent.Create(
        NativeId65FreshDocumentIntent.Create(8448,8448,8704,2048,2048,8960,8960,64,new(80,128,112),new(128,96,64),19),
        [NativeFreshWizardRouteIntent.Create(A,9984,9984,8909,9472,9984), NativeFreshWizardRouteIntent.Create(B,9728,9984,8909,9472,9984)]);
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidDataException(message); checks++; }
    private static void Pump() => Dispatcher.UIThread.RunJobs();

    private sealed class Fixture : IDisposable
    {
        internal readonly EditorProjectLayout Project;
        internal readonly NativeFreshDocumentWindow Window;
        internal int SourceCalls;
        private readonly byte[] manifest, legacy;
        internal string Path => System.IO.Path.Combine(Project.RootPath, NativeFreshLevelDocumentStore.FileName);
        internal Fixture(string parent, string id, NativeFreshLevelDocumentIntent document)
        {
            string root = System.IO.Path.Combine(parent, id); Directory.CreateDirectory(root); Project = new(root,id);
            File.WriteAllText(Project.ManifestPath, "{\"schemaVersion\":1,\"projectId\":\"" + id + "\",\"preserve\":true}");
            manifest = File.ReadAllBytes(Project.ManifestPath);
            var savedLegacy = NativeId65FreshDocumentStore.Save(NativeId65FreshDocumentStore.Capture(Project), document.Base);
            legacy = File.ReadAllBytes(savedLegacy.Path);
            NativeFreshLevelDocumentStore.Save(NativeFreshLevelDocumentStore.Capture(Project), document);
            Window = new(Project, () => true);
            Window.ResearchSourcePathsForTesting = () => { SourceCalls++; throw new InvalidOperationException("Raw material UI must not resolve a source"); };
            Window.DecisionForTesting = _ => Task.FromResult(FreshDocumentDecision.Cancel);
            Window.Show(); Pump();
        }
        internal T Find<T>(string name) where T : Control => Window.GetLogicalDescendants().OfType<T>().Single(c => c.Name == name);
        internal Button Button(string name) => Find<Button>(name);
        internal string Text(string name) => Find<TextBox>(name).Text ?? "";
        internal string Label(string name) => Find<TextBlock>(name).Text ?? "";
        internal string Kind(int slot) => Find<ComboBox>($"FreshMaterial{slot}Kind").SelectedItem as string ?? "";
        internal void Set(string name,string value) { Find<TextBox>(name).Text = value; Pump(); }
        internal void SetKind(int slot,string kind) { Find<ComboBox>($"FreshMaterial{slot}Kind").SelectedItem = kind; Pump(); }
        internal void Retail(int slot,int wad,string id)
        {
            SetKind(slot,RetailKind); Set($"FreshMaterial{slot}SourceVariant",Variant);
            Find<ComboBox>($"FreshMaterial{slot}SourceLevel").SelectedItem = wad; Pump(); Set($"FreshMaterial{slot}TextureId",id);
        }
        internal void Open() { Find<ComboBox>("FreshDocumentMode").SelectedItem = "Level aggregate"; Pump(); }
        internal void Select(string id)
        { var list=Find<ListBox>("FreshLevelPlacements"); list.SelectedItem=list.ItemsSource!.Cast<NativeFreshPlacementIntent>().Single(p=>p.EditorId==id); Pump(); }
        internal void Fire(string name) { var b=Button(name); b.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent,b)); Pump(); }
        internal void Click(string name) { Check(Button(name).IsEffectivelyEnabled,"enabled "+name); Fire(name); }
        internal byte[] Disk() => File.ReadAllBytes(Path);
        internal NativeFreshLevelDocumentSnapshot Snapshot() => NativeFreshLevelDocumentStore.Capture(Project);
        internal void RequireProtected() => Check(SourceCalls == 0 && File.ReadAllBytes(Project.ManifestPath).SequenceEqual(manifest) &&
            File.ReadAllBytes(System.IO.Path.Combine(Project.RootPath,NativeId65FreshDocumentStore.FileName)).SequenceEqual(legacy),
            "raw-only controls preserve manifest/legacy and invoke no source provider");
        public void Dispose()
        { if (!Window.IsVisible) return; Window.DecisionForTesting=_=>Task.FromResult(FreshDocumentDecision.Discard); Window.Close(); Pump(); }
    }
}
