using System.Diagnostics;
using System.Runtime.ExceptionServices;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Persistence;

// Private shared test fixture; actual tiny Store files are always retained.
internal sealed class NativeFreshMaterialInspectionUiFixture
{
    internal const string Variant = "spyro1-usa-retail-mode2-2352-v1";
    internal const string A = "11111111111111111111111111111111", B = "22222222222222222222222222222222";
    internal readonly EditorProjectLayout Project;
    internal readonly NativeFreshDocumentWindow Window;
    internal readonly NativeFreshLevelDocumentIntent Initial;
    internal bool CurrentProject = true, ReturnNullOwner;
    internal int OwnerAttempts, AcquiredOwners, ReleaseAttempts, SourceCalls;
    internal Exception? OwnerFailure, ReleaseFailure;
    internal Action? OnRelease;
    internal Func<(string Image, string Cue)>? SourceOverride;
    internal (string Image, string Cue) SelectedSource;
    internal byte[] ExpectedDocument;
    private readonly byte[] manifest, legacy;
    internal string DocumentPath => Path.Combine(Project.RootPath, NativeFreshLevelDocumentStore.FileName);

    internal NativeFreshMaterialInspectionUiFixture(string parent, string id, bool owner = true,
        NativeFreshLevelDocumentIntent? document = null)
    {
        string root = Path.Combine(parent, id); Directory.CreateDirectory(root); Project = new(root, id);
        File.WriteAllText(Project.ManifestPath, "{\"schemaVersion\":1,\"projectId\":\"" + id + "\",\"sentinel\":true}");
        manifest = File.ReadAllBytes(Project.ManifestPath);
        Initial = document ?? Document();
        var old = NativeId65FreshDocumentStore.Save(NativeId65FreshDocumentStore.Capture(Project), Initial.Base);
        legacy = File.ReadAllBytes(old.Path);
        NativeFreshLevelDocumentStore.Save(NativeFreshLevelDocumentStore.Capture(Project), Initial);
        ExpectedDocument = NativeFreshLevelDocumentCodec.SerializeCanonical(Initial);
        SelectedSource = (Path.Combine(root, "neutral-not-opened.bin"), Path.Combine(root, "neutral-not-opened.cue"));
        Window = new(Project, () => CurrentProject,
            () => { SourceCalls++; return SourceOverride?.Invoke() ?? SelectedSource; },
            owner ? Acquire : null);
        Window.DecisionForTesting = _ => Task.FromResult(FreshDocumentDecision.Cancel);
        Window.Show(); Pump();
        Find<ComboBox>("FreshDocumentMode").SelectedItem = "Level aggregate"; Pump();
        Select(A);
    }

    private IDisposable Acquire()
    {
        OwnerAttempts++;
        if (OwnerFailure != null) ExceptionDispatchInfo.Capture(OwnerFailure).Throw();
        if (ReturnNullOwner) return null!;
        AcquiredOwners++;
        return new Owner(this);
    }
    private sealed class Owner(NativeFreshMaterialInspectionUiFixture fixture) : IDisposable
    {
        private bool attempted;
        public void Dispose()
        {
            if (attempted) throw new InvalidDataException("Window retried an already attempted editor owner.");
            attempted = true; fixture.ReleaseAttempts++; fixture.OnRelease?.Invoke();
            if (fixture.ReleaseFailure != null) ExceptionDispatchInfo.Capture(fixture.ReleaseFailure).Throw();
        }
    }

    internal T Find<T>(string name) where T : Control =>
        Window.GetLogicalDescendants().OfType<T>().Single(control => control.Name == name);
    internal void Fire(string name)
    { var b = Find<Button>(name); b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, b)); Pump(); }
    internal void Set(string name, string value) { Find<TextBox>(name).Text = value; Pump(); }
    internal string Text(string name) => Find<TextBox>(name).Text ?? "";
    internal string InspectionStatus => Find<TextBlock>("FreshMaterialInspectionStatus").Text ?? "";
    internal void Select(string id)
    {
        var list = Find<ListBox>("FreshLevelPlacements");
        list.SelectedItem = list.ItemsSource!.Cast<NativeFreshPlacementIntent>().Single(row => row.EditorId == id); Pump();
    }
    internal void Retail(int slot, int wad, int id)
    {
        Find<ComboBox>($"FreshMaterial{slot}Kind").SelectedItem = "Retail terrain record";
        Find<ComboBox>($"FreshMaterial{slot}SourceLevel").SelectedItem = wad;
        Set($"FreshMaterial{slot}SourceVariant", Variant); Set($"FreshMaterial{slot}TextureId", id.ToString());
    }
    internal Task Start()
    {
        Fire("FreshMaterialsInspect");
        return Window.MaterialInspectionTaskForTesting ?? throw new InvalidDataException("Expected actual UI inspection task.");
    }
    internal void CmdSave()
    {
        Window.RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent, Key = Key.S,
            KeyModifiers = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control
        });
        Pump();
    }
    internal bool NoImages => Find<Image>("FreshMaterial0Preview").Source == null &&
        Find<Image>("FreshMaterial1Preview").Source == null && Window.MaterialInspectionResultForTesting == null;
    internal void RequireProtected()
    {
        List<Exception> failures = [];
        Attempt(() => Require(File.ReadAllBytes(Project.ManifestPath).SequenceEqual(manifest), "complete manifest bytes retained"));
        Attempt(() => Require(File.ReadAllBytes(Path.Combine(Project.RootPath, NativeId65FreshDocumentStore.FileName)).SequenceEqual(legacy), "complete legacy bytes retained"));
        Attempt(() => Require(File.ReadAllBytes(DocumentPath).SequenceEqual(ExpectedDocument), "exact expected aggregate bytes retained"));
        Attempt(() => Require(AcquiredOwners == ReleaseAttempts, "every actually acquired test editor owner attempted once"));
        Throw(failures);
        void Attempt(Action action) { try { action(); } catch (Exception error) { failures.Add(error); } }
    }
    internal void Close()
    {
        if (Window.MaterialInspectionTaskForTesting is { } task) Drain(task);
        if (!Window.IsVisible) return;
        Window.DecisionForTesting = _ => Task.FromResult(FreshDocumentDecision.Discard);
        Window.Close(); Pump();
    }
    internal static void Wait(Task task, int seconds = 30)
    {
        var timer = Stopwatch.StartNew();
        while (!task.IsCompleted && timer.Elapsed < TimeSpan.FromSeconds(seconds)) { Pump(); Thread.Sleep(1); }
        Require(task.IsCompleted, "test gate timeout; caller must still release and drain in finally");
        task.GetAwaiter().GetResult(); Pump();
    }
    internal static void Drain(Task task)
    {
        // Called only AFTER releasing any test gate. Never dispose a busy owner on timeout.
        while (!task.IsCompleted) { Pump(); Thread.Sleep(1); }
        task.GetAwaiter().GetResult(); Pump();
    }
    internal static void Pump() => Dispatcher.UIThread.RunJobs();
    internal static void Require(bool condition, string message)
    { if (!condition) throw new InvalidDataException("Inspection UI fixture: " + message); }
    internal static void Throw(List<Exception> failures)
    {
        if (failures.Count == 1) ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures.Count > 1) throw new IOException("Inspection UI test and independent final failures.", new AggregateException(failures));
    }
    internal static List<Exception> Leaves(Exception error) => error is AggregateException group
        ? group.InnerExceptions.SelectMany(Leaves).ToList() : error.InnerException != null ? Leaves(error.InnerException) : [error];
    internal static NativeFreshFloorMaterialsIntent Pair(int a = 14, int x = 0, int b = 70, int y = 1) =>
        NativeFreshFloorMaterialsIntent.Create(NativeFreshFloorMaterialIntent.RetailTerrain(Variant, a, x),
            NativeFreshFloorMaterialIntent.RetailTerrain(Variant, b, y));
    internal static NativeFreshLevelDocumentIntent Document() => NativeFreshLevelDocumentIntent.Create(
        NativeId65FreshDocumentIntent.Create(8448, 8448, 8704, 2048, 2048, 8960, 8960, 64,
            new(80, 128, 112), new(128, 96, 64), 19),
        [NativeFreshWizardRouteIntent.Create(A, 9984, 9984, 8909, 9472, 9984),
         NativeFreshWizardRouteIntent.Create(B, 9728, 9984, 8909, 9472, 9984)]).WithFloorMaterials(Pair());
}
