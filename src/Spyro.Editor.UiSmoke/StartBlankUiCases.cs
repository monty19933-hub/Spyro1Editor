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
            Check(window.FreshSceneForTesting is{Objects.Count:0} cleared&&cleared.Surfaces.Count>100&&cleared.Id!=original.Id,"actual confirmation replaces scene with the starter meadow");
            Check(ReferenceEquals(viewport,Get<object>(window,"_viewport")),"clear retains shared viewport");
            Check(StartBlankRecoveryStore.Read(root,"authored65")!.Active,"before-clear recovery durable");
            // The meadow has well over a hundred tiles: all but one go as a multi-selection, the last on its own.
            geometry=Get<GeometryCandidate>(window,"_currentGeometry");
            var shownViewport=Get<Spyro.Editor.App.Views.EditorViewport>(window,"_viewport");
            shownViewport.SetTerrainMultiSelection(Enumerable.Range(0,geometry.Polygons.Count-1));Pump();
            Check(Get<Button>(window,"_terrainTaskRemoveFaceButton").IsEnabled,"Remove Terrain enabled for the selected tiles");
            Click(Get<Button>(window,"_terrainTaskRemoveFaceButton"));
            shownViewport.ClearTerrainMultiSelection();Pump();
            var last=geometry.Polygons[^1];
            Invoke(window,"ShowSelection",ViewportSelectionChangedEventArgs.ForTerrain(geometry.Polygons.Count-1,last));
            Check(Get<Button>(window,"_terrainTaskRemoveFaceButton").IsEnabled,"Remove Terrain remains enabled for final face");
            Click(Get<Button>(window,"_terrainTaskRemoveFaceButton"));
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
            var stoneHill=new LevelDefinition{Key="stonehill",LevelId=11,SourceWadEntry=12,DisplayName="Stone Hill",SourceRecordCount=120};
            var darkHollow=new LevelDefinition{Key="darkhollow",LevelId=12,SourceWadEntry=14,DisplayName="Dark Hollow",SourceRecordCount=90};
            var catalog=new LevelCatalog([retail,stoneHill,darkHollow]);Set(window,"_retailCatalog",catalog);Set(window,"_catalog",catalog);
            var replacement=SceneDocument.New("New Artisans") with{TargetBinding=new("artisans",10,10,9,Spyro.Editor.Core.Universal.UniversalSourceLibrary.OriginalUsaSha256,195)};
            Complete(replacement.SaveAsync(SceneDocumentStore.PathForTarget(root,"artisans")));
            var second=SceneDocument.New("New Stone Hill") with{TargetBinding=new("stonehill",11,12,11,Spyro.Editor.Core.Universal.UniversalSourceLibrary.OriginalUsaSha256,120)};
            var third=SceneDocument.New("New Dark Hollow") with{TargetBinding=new("darkhollow",12,14,13,Spyro.Editor.Core.Universal.UniversalSourceLibrary.OriginalUsaSha256,90)};
            Complete(second.SaveAsync(SceneDocumentStore.PathForTarget(root,"stonehill")));
            Complete(third.SaveAsync(SceneDocumentStore.PathForTarget(root,"darkhollow")));
            Complete((Task)Invoke(window,"SelectLevelAsync",retail,null!)!);
            Check(Get<LevelDefinition>(window,"_currentLevel") is{Key:"artisans",LevelId:10,SourceWadEntry:10,SourceRecordCount:0},"normal retail picker loader resolves same-slot authored override before native cache");
            Check(window.FreshSceneForTesting!.Id==replacement.Id&&Get<ComboBox>(window,"_levelJumpBox").Items.OfType<LevelDefinition>().Count(l=>l.Key=="artisans")==1,"replacement retains real slot and does not duplicate picker rows");
            Check(ReferenceEquals(viewport,Get<object>(window,"_viewport")),"retail override uses same shared viewport");
            Check(Get<TextBlock>(window,"_statusText").Text=="Opened New Artisans: 2 authored faces, 0 objects.","reopened replacement finishes its loading status");
            // These loaded identities are copies of their catalog records; reference-only
            // navigation would remain at Artisans on the first forward step.
            foreach(var step in new[]{(Delta:1,Level:stoneHill,Scene:second),(Delta:1,Level:darkHollow,Scene:third),
                (Delta:-1,Level:stoneHill,Scene:second),(Delta:-1,Level:retail,Scene:replacement)})
            {
                Complete((Task)Invoke(window,"SelectAdjacentLevelAsync",step.Delta)!);
                var selected=Get<LevelDefinition>(window,"_currentLevel");
                Check(selected.Key==step.Level.Key&&selected.LevelId==step.Level.LevelId&&selected.SourceWadEntry==step.Level.SourceWadEntry&&
                    !ReferenceEquals(selected,step.Level)&&window.FreshSceneForTesting!.Id==step.Scene.Id&&ReferenceEquals(viewport,Get<object>(window,"_viewport")),
                    $"adjacent step {step.Delta} opens copied retail slot {step.Level.Key} in the shared viewport");
            }
            var reload=(Task)Invoke(window,"RestoreCurrentLevelAsync")!;
            Wait(()=>window.OwnedWindows.Any(w=>w.Name=="RestoreLevelDialog"));
            var reloadDialog=window.OwnedWindows.Single(w=>w.Name=="RestoreLevelDialog");
            Check(Get<Button>(window,"_objectRestoreLevelButton").Content?.ToString()=="Reload saved level"&&
                Find<TextBlock>(reloadDialog,"RestoreLevelExplanation").Text!.Contains("last saved version")&&
                !Find<TextBlock>(reloadDialog,"RestoreLevelExplanation").Text!.Contains("clears saved object edits"),
                "cleared-scene reload confirmation describes saved scene recovery, not deletion of original sidecars");
            Click(reloadDialog.GetLogicalDescendants().OfType<Button>().Single(b=>b.Content?.ToString()=="Cancel"));Complete(reload);
            File.Delete(SceneDocumentStore.PathForTarget(root,"artisans"));
            Complete((Task)Invoke(window,"SelectLevelAsync",retail,null!)!);
            Check(Get<ComboBox>(window,"_levelJumpBox").SelectedItem is LevelDefinition {Key:"artisans"} &&
                Get<ListBox>(window,"_levelList").SelectedItem is LevelDefinition {Key:"artisans"},
                "returning from a replacement to original retail data keeps both level pickers selected");
            Check(File.ReadAllText(Path.Combine(root,"protected.txt"))=="unrelated"&&!Directory.EnumerateFiles(root,"*.bin",SearchOption.AllDirectories).Any(),"unrelated file unchanged and no native image authored");
            Console.WriteLine($"START_BLANK_UI_OK checks={checks} sourceOpened=false nativeOutputWritten=false fixtures={root}");
        }
        finally
        {
            if(window!=null){window.StartBlankSnapshotFaultForTesting=null;window.Close();Pump();}
            Environment.CurrentDirectory=previous;Environment.SetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE",workspace);Environment.SetEnvironmentVariable("SPYRO_EDITOR_RELEASE",release);
        }
    }
    private static void Check(bool value,string name){if(!value)throw new Exception(name);checks++;if(Environment.GetEnvironmentVariable("START_BLANK_TRACE")=="1")Console.WriteLine("ok: "+name);}
    private static object? Invoke(object target,string name,params object[] args)=>target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(target,args);
    private static T Get<T>(object target,string name)=>(T)target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(target)!;
    private static void Set(object target,string name,object value)=>target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(target,value);
    private static T Find<T>(Control parent,string name)where T:Control=>parent.GetLogicalDescendants().OfType<T>().Single(c=>c.Name==name);
    private static void Click(Button button){button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();}
    private static void Pump()=>Dispatcher.UIThread.RunJobs();
    private static void Wait(Func<bool> predicate){var until=DateTime.UtcNow.AddSeconds(30);while(!predicate()){Pump();Thread.Sleep(10);if(DateTime.UtcNow>until)throw new Exception("UI timeout");}}
    private static void Complete(Task task){Wait(()=>task.IsCompleted);task.GetAwaiter().GetResult();Pump();}
}
