using System.Reflection;
using Avalonia;
using Avalonia.Threading;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Universal;

internal static class UniversalGroupUiCases
{
    internal static void Run(string image, string catalogRoot)
    {
        image=Path.GetFullPath(image);catalogRoot=Path.GetFullPath(catalogRoot);
        RunCase(image,catalogRoot,"loftycastle",0x12C,1,[1,22,23,24,25,26,27]);
        RunCase(image,catalogRoot,"gnorccove",0xCF,34,[32,34]);
    }
    private static void RunCase(string image,string catalogRoot,string sourceKey,int sourceClass,int sourceRow,int[] expectedRows)
    {
        int size=expectedRows.Length;
        int checks=0; void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);checks++;Console.WriteLine($"GROUP_UI {sourceKey} {checks}: {message}");}
        UniversalSourceLibrary.RequireOriginal(image);
        var library=new UniversalSourceLibrary(image,LevelCatalog.Load(catalogRoot));
        string root=Directory.CreateTempSubdirectory("spyro-universal-group-ui-").FullName;
        string previous=Environment.CurrentDirectory;
        string? workspace=Environment.GetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE"),release=Environment.GetEnvironmentVariable("SPYRO_EDITOR_RELEASE");
        File.WriteAllText(Path.Combine(root,"spyro-level-catalog.json"),"{\"levels\":[]}");
        MainWindow? window=null;
        try
        {
            Environment.CurrentDirectory=root;Environment.SetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE",root);Environment.SetEnvironmentVariable("SPYRO_EDITOR_RELEASE","1");
            window=new MainWindow(); Invoke(window,"ApplyFreshScene",SceneDocument.New("Group Test"));window.Show();Pump();
            var variant=new UniversalMobyVariant(sourceClass,sourceKey,sourceRow,24,0,0,true,false,true);
            var entry=new UniversalMobyCatalogEntry(sourceClass,"Required object group","Interactive","",[variant],variant,[sourceKey],null);
            Complete((Task)Invoke(window,"AddUniversalMobyAsync",library,entry,variant,false,null)!);
            var mobys=Get<List<Moby>>(window,"_currentMobys");
            Check(mobys.Count==size,"actual Add creates the two required source objects");
            Check(mobys.Select(m=>m.CrossLevelSourceTrueIndex).Order().SequenceEqual(expectedRows),"exact reciprocal source pair retained");
            Check(mobys.All(m=>m.UniversalLinkGroupId.Length>0)&&mobys.Select(m=>m.UniversalLinkGroupId).Distinct().Count()==1,"added pair shares one group");
            var selected=mobys.Single(m=>m.CrossLevelSourceTrueIndex==sourceRow);
            Set(window,"_selectedMoby",selected);
            Check(((IEnumerable<Moby>)Invoke(window,"GetLinkedMoveMobys",selected)!).Count()==size,"move operates on both required objects");
            Invoke(window,"CopySelectedMoby");
            Check(Get<object?>(window,"_mobyClipboard")!=null,"Copy captures the actual group clipboard");
            Complete((Task)Invoke(window,"PasteMobyClipboardAtViewportAsync",new Point(300,300))!);
            Check(mobys.Count==size*2,$"Paste duplicates the complete group: actual={mobys.Count}, expected={size*2}, status={Get<Avalonia.Controls.TextBlock>(window,"_statusText").Text}");
            Check(mobys.Select(m=>m.TrueIndex).Distinct().Count()==size*2&&mobys.Select(m=>m.UniversalPlacementId).Distinct().Count()==size*2,"paste creates unique row and save identities");
            Check(mobys.GroupBy(m=>m.UniversalLinkGroupId).Count()==2&&mobys.GroupBy(m=>m.UniversalLinkGroupId).All(g=>g.Count()==size),"duplicate pair has independent link namespace");
            var savedClipboard=Get<object>(window,"_mobyClipboard");
            Set(window,"_selectedMoby",new Moby{Label="Camera control",Type=0,EditorControlKind="camera"});
            Invoke(window,"CopySelectedMoby");
            Check(ReferenceEquals(savedClipboard,Get<object>(window,"_mobyClipboard")),"legacy editor controls remain protected from copying");
            var copied=mobys.Last();Set(window,"_selectedMoby",copied);Invoke(window,"RemoveSelectedMoby");
            Check(mobys.Count(m=>m.IsRemoved)==size&&mobys.Where(m=>m.IsRemoved).All(m=>m.UniversalLinkGroupId==copied.UniversalLinkGroupId),"Remove retires only selected whole group");
            Invoke(window,"UndoLastRemovedMoby");Check(mobys.All(m=>!m.IsRemoved),"Undo restores the complete group");
            string path=Path.Combine(root,"artisans-native-edits.json");Complete(MobyEditStore.SaveAsync(path,mobys,"Artisans"));
            var placements=UniversalPlacementReader.Read(path);
            Check(placements.GroupBy(p=>p.LinkGroupId).All(g=>g.Count()==size)&&placements.Count==size*2,"retail save/export reader retains duplicate group ownership");
            List<Moby> loaded=[];MobyEditStore.Load(path,loaded);
            Check(loaded.Select(m=>m.UniversalLinkGroupId).SequenceEqual(mobys.Select(m=>m.UniversalLinkGroupId)),"retail editor reopens each exact group");
            var document=SceneDocument.New("Group Reload").Capture(window.FreshSceneForTesting!.ProjectGeometry(),mobys);
            string scene=Path.Combine(root,SceneDocument.FileName);File.WriteAllBytes(scene,document.Serialize());
            Invoke(window,"ApplyFreshScene",SceneDocument.Read(scene));
            var fresh=Get<List<Moby>>(window,"_currentMobys");
            Check(fresh.Count==size*2&&fresh.GroupBy(m=>m.UniversalLinkGroupId).All(g=>g.Count()==size),"fresh editor reopens both exact groups");
            var actual=(List<UniversalMobyPlacement>)Invoke(window,"CurrentUniversalPlacements")!;
            Check(actual.Count==size*2&&actual.GroupBy(p=>p.LinkGroupId).Count()==2,"actual fresh export projection retains group identity");
            string output=Path.Combine(root,"group-export.bin");
            var exportTask=Task.Run(()=>UniversalMobyExport.Apply(library,image,output,LevelCatalog.Load(catalogRoot),
                new Dictionary<string,IReadOnlyList<UniversalMobyPlacement>>{{"darkhollow",actual}}));
            Complete(exportTask);var exported=exportTask.GetAwaiter().GetResult();
            var compose=exported.Levels.Single().Compose;
            var written=UniversalDiscImage.Read(output);
            var target=library.Get("darkhollow");
            var data=new UniversalLevelData(written.EntryBytes(target.Level.SourceWadEntry));
            Check(data.Bytes.SequenceEqual(compose.LevelData),"ordinary export written data readback");
            Check(compose.NewRowIndices.Count==size*2,"ordinary export retains both complete groups");
            for(int i=0;i<actual.Count;i++)
            {
                var source=library.Get(actual[i].SourceLevelKey);
                var template=UniversalIndexedRows.Capture(source.Data,source.Overlay,source.Data.CaptureRow(actual[i].SourceLevelKey,actual[i].SourceRowIndex));
                var placed=data.CaptureRow("darkhollow",compose.NewRowIndices[i]);
                foreach(var link in template.SourceRowIndices)
                {
                    int owner=actual.FindIndex(p=>p.SourceLevelKey==actual[i].SourceLevelKey&&p.SourceRowIndex==link.SourceRow&&p.LinkGroupId==actual[i].LinkGroupId);
                    Check(owner>=0&&BitConverter.ToInt32(placed.Properties,link.Offset)==compose.NewRowIndices[owner],"written native integer points to its own copied group");
                }
            }
            Console.WriteLine($"UNIVERSAL_GROUP_UI_OK source={sourceKey} checks={checks} evidence={root}");
        }
        finally {if(window!=null){Set(window,"_allowCloseWithUnsavedTerrain",true);window.Close();}Pump();Environment.CurrentDirectory=previous;Environment.SetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE",workspace);Environment.SetEnvironmentVariable("SPYRO_EDITOR_RELEASE",release);}
    }
    private static T Get<T>(object o,string n)=>(T)o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(o)!;
    private static void Set(object o,string n,object v)=>o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(o,v);
    private static object? Invoke(object o,string n,params object?[] a)=>o.GetType().GetMethod(n,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(o,a);
    private static void Pump()=>Dispatcher.UIThread.RunJobs();
    private static void Complete(Task t){DateTime until=DateTime.UtcNow.AddSeconds(30);while(!t.IsCompleted){Pump();if(DateTime.UtcNow>until)throw new TimeoutException();Thread.Sleep(5);}t.GetAwaiter().GetResult();Pump();}
}
