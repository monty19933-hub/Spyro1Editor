using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Analysis;

internal static class StartBlankUiCases
{
    private static int checks;
    internal static void Run()
    {
        string root=Directory.CreateTempSubdirectory("spyro-start-blank-ui-").FullName;
        string? workspace=Environment.GetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE"),release=Environment.GetEnvironmentVariable("SPYRO_EDITOR_RELEASE");
        string previous=Environment.CurrentDirectory;
        File.WriteAllText(Path.Combine(root,"spyro-level-catalog.json"),"{\"levels\":[]}");
        File.WriteAllText(Path.Combine(root,"protected.txt"),"unrelated");
        MainWindow? window=null;
        try
        {
            Environment.CurrentDirectory=root;Environment.SetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE",root);Environment.SetEnvironmentVariable("SPYRO_EDITOR_RELEASE","0");
            window=new MainWindow();window.Show();Pump();
            Invoke(window,"ApplyFreshScene",SceneDocument.New("My Level"));
            var original=window.FreshSceneForTesting!;
            var viewport=Get<object>(window,"_viewport");
            var cancel=(Task)Invoke(window,"StartBlankCurrentLevelAsync")!;
            Wait(()=>window.OwnedWindows.Any(w=>w.Name=="StartBlankDialog"));
            var dialog=window.OwnedWindows.Single(w=>w.Name=="StartBlankDialog");
            Check(Find<TextBlock>(dialog,"StartBlankExplanation").Text!.Contains("2 terrain faces"),"actual confirmation identifies current faces");
            Click(Find<Button>(dialog,"StartBlankCancel"));Complete(cancel);
            Check(window.FreshSceneForTesting!.Id==original.Id&&!File.Exists(Path.Combine(root,SceneDocument.FileName)),"Cancel preserves unsaved scene without saving or clearing");
            // A real unsaved point edit must be saved before the recovery snapshot.
            var geometry=Get<GeometryCandidate>(window,"_currentGeometry");geometry.Polygons[0].ApplyTerrainDeltaZ(8);
            var savedBefore=window.FreshSceneForTesting!;
            var failed=(Task)Invoke(window,"StartBlankCurrentLevelAsync")!;
            Wait(()=>window.OwnedWindows.Any(w=>w.Name=="StartBlankDialog"));
            window.StartBlankSnapshotFaultForTesting=p=>File.WriteAllText(Path.Combine(p,"before.scene.json"),"corrupt");
            Click(Find<Button>(window.OwnedWindows.Single(w=>w.Name=="StartBlankDialog"),"StartBlankConfirm"));Complete(failed);
            Check(window.FreshSceneForTesting!.Id==original.Id&&SceneDocument.Read(Path.Combine(root,SceneDocument.FileName)).Surfaces[0].Vertices[0].Z==552,"snapshot failure aborts clear while saved draft remains recoverable");
            window.StartBlankSnapshotFaultForTesting=null;
            var clear=(Task)Invoke(window,"StartBlankCurrentLevelAsync")!;
            Wait(()=>window.OwnedWindows.Any(w=>w.Name=="StartBlankDialog"));
            Click(Find<Button>(window.OwnedWindows.Single(w=>w.Name=="StartBlankDialog"),"StartBlankConfirm"));Complete(clear);
            Check(window.FreshSceneForTesting is{Surfaces.Count:2,Objects.Count:0}&&window.FreshSceneForTesting.Id!=original.Id,"actual confirmation replaces scene with neutral floor");
            Check(ReferenceEquals(viewport,Get<object>(window,"_viewport")),"clear retains shared viewport");
            Check(StartBlankRecoveryStore.Read(root,"authored65")!.Active,"before-clear recovery durable");
            geometry=Get<GeometryCandidate>(window,"_currentGeometry");
            foreach(var face in geometry.Polygons.ToArray())
            {
                Invoke(window,"ShowSelection",ViewportSelectionChangedEventArgs.ForTerrain(geometry.Polygons.IndexOf(face),face));
                Check(Get<Button>(window,"_terrainTaskRemoveFaceButton").IsEnabled,"Remove Terrain remains enabled for final face");
                Click(Get<Button>(window,"_terrainTaskRemoveFaceButton"));
            }
            Check(window.FreshSceneForTesting!.Surfaces.Count==0,"normal Remove Terrain empties scene");
            Complete((Task)Invoke(window,"SaveFreshSceneAsync")!);Invoke(window,"ApplyFreshScene",SceneDocument.Read(Path.Combine(root,SceneDocument.FileName)));
            Check(window.FreshSceneForTesting!.Surfaces.Count==0&&Get<GeometryCandidate>(window,"_currentGeometry").Polygons.Count==0,"empty save reopen invents no floor");
            Check(Get<Button>(window,"_terrainTaskAddCopyButton").IsEnabled,"Add Terrain enabled from empty");
            Click(Get<Button>(window,"_terrainTaskAddCopyButton"));Pump();
            Check(window.FreshSceneForTesting!.Surfaces.Count==1,"Add Terrain explicitly creates one new face from empty");
            Check(Get<Button>(window,"_terrainUndoButton").IsEnabled,"new face can be undone");
            Click(Get<Button>(window,"_terrainUndoButton"));Pump();
            Check(window.FreshSceneForTesting!.Surfaces.Count==0,"Undo returns to truly empty scene");
            var restore=(Task)Invoke(window,"RestoreBeforeClearAsync")!;
            Wait(()=>window.OwnedWindows.Any(w=>w.Name=="RestoreBeforeClearDialog"));
            Click(Find<Button>(window.OwnedWindows.Single(w=>w.Name=="RestoreBeforeClearDialog"),"StartBlankConfirm"));Complete(restore);
            Check(window.FreshSceneForTesting!.Id==savedBefore.Id&&window.FreshSceneForTesting.Surfaces[0].Vertices[0].Z==552,"restore brings back real unsaved draft that clear saved");
            Check(!StartBlankRecoveryStore.Read(root,"authored65")!.Active,"recovery is recorded as restored");
            var retail=new LevelDefinition{Key="artisans",LevelId=10,SourceWadEntry=10,DisplayName="Artisans",SourceRecordCount=195};
            var catalog=new LevelCatalog([retail]);Set(window,"_retailCatalog",catalog);Set(window,"_catalog",catalog);
            var replacement=SceneDocument.New("New Artisans") with{TargetBinding=new("artisans",10,10,9,Spyro.Editor.Core.Universal.UniversalSourceLibrary.OriginalUsaSha256,195)};
            Complete(replacement.SaveAsync(SceneDocumentStore.PathForTarget(root,"artisans")));
            Complete((Task)Invoke(window,"SelectLevelAsync",retail,null!)!);
            Check(Get<LevelDefinition>(window,"_currentLevel") is{Key:"artisans",LevelId:10,SourceWadEntry:10,SourceRecordCount:0},"normal retail picker loader resolves same-slot authored override before native cache");
            Check(window.FreshSceneForTesting!.Id==replacement.Id&&Get<ComboBox>(window,"_levelJumpBox").Items.OfType<LevelDefinition>().Count(l=>l.Key=="artisans")==1,"replacement retains real slot and does not duplicate picker rows");
            Check(ReferenceEquals(viewport,Get<object>(window,"_viewport")),"retail override uses same shared viewport");
            Check(File.ReadAllText(Path.Combine(root,"protected.txt"))=="unrelated"&&!Directory.EnumerateFiles(root,"*.bin",SearchOption.AllDirectories).Any(),"unrelated file unchanged and no native image authored");
            Console.WriteLine($"START_BLANK_UI_OK checks={checks} sourceOpened=false nativeOutputWritten=false fixtures={root}");
        }
        finally
        {
            if(window!=null){window.StartBlankSnapshotFaultForTesting=null;window.Close();Pump();}
            Environment.CurrentDirectory=previous;Environment.SetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE",workspace);Environment.SetEnvironmentVariable("SPYRO_EDITOR_RELEASE",release);
        }
    }
    private static void Check(bool value,string name){if(!value)throw new Exception(name);checks++;}
    private static object? Invoke(object target,string name,params object[] args)=>target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(target,args);
    private static T Get<T>(object target,string name)=>(T)target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(target)!;
    private static void Set(object target,string name,object value)=>target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(target,value);
    private static T Find<T>(Control parent,string name)where T:Control=>parent.GetLogicalDescendants().OfType<T>().Single(c=>c.Name==name);
    private static void Click(Button button){button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();}
    private static void Pump()=>Dispatcher.UIThread.RunJobs();
    private static void Wait(Func<bool> predicate){var until=DateTime.UtcNow.AddSeconds(30);while(!predicate()){Pump();Thread.Sleep(10);if(DateTime.UtcNow>until)throw new Exception("UI timeout");}}
    private static void Complete(Task task){Wait(()=>task.IsCompleted);task.GetAwaiter().GetResult();Pump();}
}
