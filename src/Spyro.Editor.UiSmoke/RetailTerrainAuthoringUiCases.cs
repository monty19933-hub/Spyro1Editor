using System.Diagnostics;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Universal;

internal static class RetailTerrainAuthoringUiCases
{
    internal static void Run()
    {
        var window=new MainWindow{Width=1440,Height=900};window.Show();
        try
        {
            Wait(()=>window.CaptureEditorShellSessionSnapshotForTesting().CurrentMobys.Count>0,"initial scene");
            var artisans=Get<LevelCatalog>(window,"_retailCatalog").FindByKey("artisans")!;
            RunTask((Task)Invoke(window,"SelectLevelAsync",artisans,null)!);
            var geometry=Get<GeometryCandidate>(window,"_currentGeometry");
            int index=geometry.Polygons.FindIndex(p=>p.RuntimeKey=="90:0:hp");Check(index>=0,"source-native reachable floor");
            var face=geometry.Polygons[index];window.CaptureEditorShellSessionSnapshotForTesting().Viewport.SelectTerrainForTesting(index);Pump();
            foreach(string field in new[]{"_terrainTaskMoveFaceButton","_terrainTaskSinglePointButton","_terrainTaskAddCopyButton","_terrainTaskRemoveFaceButton"})
                Check(Get<Button>(window,field).IsEffectivelyVisible&&Get<Button>(window,field).IsEnabled,"normal shared control: "+field);
            Check((bool)Invoke(window,"CanStageTerrainAddCopy",face)! && (bool)Invoke(window,"CanStageTerrainRemoval",face)!,"source-bound add and remove admission");
            // Exercise the actual viewport copy command, then Save and Create BIN.
            Check((bool)Invoke(window,"StageTerrainAddCopyFromViewport",index,face)!,"normal viewport copy stages a native source face");
            Invoke(window,"MoveTerrainFromViewport",new ViewportTerrainMoveRequestedEventArgs(index,face,160,0,0,stageAddCopy:false));
            Check(face.TerrainVertexXYDeltas().All(p=>p.X==160&&p.Y==0),"normal viewport drag moves a staged source copy");
            Console.WriteLine("RETAIL_TERRAIN_UI saving staged terrain");RunTask((Task)Invoke(window,"SaveCurrentTerrainEditsAsync")!);Console.WriteLine("RETAIL_TERRAIN_UI creating native BIN");Create(window);
            string root=window.CaptureEditorShellSessionSnapshotForTesting().Workspace.RootPath;
            string output=Path.Combine(root,"output","Spyro Editor - All Saved Edits.bin");
            Check(File.Exists(output),"normal Create BIN publishes terrain copy: "+Get<TextBlock>(window,"_statusText").Text);
            var copy=UniversalDiscImage.Read(output);Check(new UniversalLevelData(copy.EntryBytes(artisans.SourceWadEntry)).Scene().RowCount>0,"retail actors remain native");
            var undoClock=Stopwatch.StartNew();RunTask((Task)Invoke(window,"UndoSelectedTerrainAsync")!);Console.WriteLine("RETAIL_TERRAIN_UI Undo seconds="+undoClock.Elapsed.TotalSeconds.ToString("F2"));
            window.CaptureEditorShellSessionSnapshotForTesting().Viewport.SelectTerrainForTesting(index);Pump();
            var removeClock=Stopwatch.StartNew();Click(Get<Button>(window,"_terrainTaskRemoveFaceButton"));Wait(()=>window.OwnedWindows.Any(w=>w.Title=="Remove Terrain Face"),"normal remove confirmation");
            var dialog=window.OwnedWindows.Single(w=>w.Title=="Remove Terrain Face");
            Click(dialog.GetLogicalDescendants().OfType<Button>().Single(b=>b.Content?.ToString()=="Remove Face"));
            Console.WriteLine("RETAIL_TERRAIN_UI Remove seconds="+removeClock.Elapsed.TotalSeconds.ToString("F2"));Check(face.IsTerrainRemoved,"normal remove authoring");Console.WriteLine("RETAIL_TERRAIN_UI saving staged terrain");RunTask((Task)Invoke(window,"SaveCurrentTerrainEditsAsync")!);Console.WriteLine("RETAIL_TERRAIN_UI creating native BIN");Create(window);
            Check(File.Exists(output),"normal Create BIN publishes terrain removal");
            Check(!copy.EntryBytes(artisans.SourceWadEntry).SequenceEqual(UniversalDiscImage.Read(output).EntryBytes(artisans.SourceWadEntry)),"copy/remove produce distinct native terrain bodies");
            Console.WriteLine("RETAIL_TERRAIN_AUTHORING_UI_OK source90:0 sharedmove/point/copy/remove + save/normalCreateBIN");
        }
        finally{window.Close();Pump();}
    }
    private static void Create(MainWindow window)
    {
        var task=(Task)Invoke(window,"CreateObjectTestBinAsync")!;
        Wait(()=>
        {
            foreach(var dialog in window.OwnedWindows.ToArray())
            {
                if(dialog.Name=="ProjectExportScope")
                {
                    dialog.GetLogicalDescendants().OfType<ComboBox>().Single(c=>c.Name=="ExportStartLevel").SelectedIndex=0;
                    Click(dialog.GetLogicalDescendants().OfType<Button>().Single(b=>b.Name=="ExportCreate"));
                }
                else if(dialog.Title=="Terrain Patch Check")
                    Click(dialog.GetLogicalDescendants().OfType<Button>().Single(b=>b.Content?.ToString()=="Create Anyway"));
            }
            return task.IsCompleted;
        },"normal export completion",600);
        task.GetAwaiter().GetResult();Console.WriteLine("RETAIL_TERRAIN_UI export status: "+Get<TextBlock>(window,"_statusText").Text);
    }

    private static void RunTask(Task task){Wait(()=>task.IsCompleted,"async operation",600);task.GetAwaiter().GetResult();}
    private static void Wait(Func<bool> condition,string label,int seconds=180){var timer=Stopwatch.StartNew();while(timer.Elapsed.TotalSeconds<seconds){Pump();if(condition())return;Thread.Sleep(20);}throw new InvalidOperationException("Retail terrain UI timeout: "+label);}
    private static void Pump()=>Dispatcher.UIThread.RunJobs();
    private static object? Invoke(object owner,string name,params object?[] args)=>owner.GetType().GetMethods(BindingFlags.Instance|BindingFlags.NonPublic).Single(m=>m.Name==name&&m.GetParameters().Length==args.Length).Invoke(owner,args);
    private static T Get<T>(object owner,string field)=>(T)owner.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(owner)!;
    private static void Click(Button button){button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();}
    private static void Check(bool condition,string label){if(!condition)throw new InvalidOperationException("Retail terrain UI: "+label);}
}
