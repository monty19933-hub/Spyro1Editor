using System.Buffers.Binary;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Universal;
using Spyro.Editor.Core.Exporting;

/// <summary>Exact saved user-project regression. All mutations are confined to a new copied project.</summary>
internal static class V1UserProjectUiCases
{
    internal static void Run(string snapshotRoot, string artifacts, string sourceWorkspace)
    {
        snapshotRoot=Path.GetFullPath(snapshotRoot);artifacts=Path.GetFullPath(artifacts);
        Check(!artifacts.StartsWith(snapshotRoot+Path.DirectorySeparatorChar,StringComparison.Ordinal),"artifacts are outside the preserved snapshot");
        Directory.CreateDirectory(artifacts);
        var sourceHashes=Hashes(snapshotRoot);
        string clone=Path.Combine(artifacts,"project-"+Guid.NewGuid().ToString("N"));CopyTree(snapshotRoot,clone);
        foreach(string name in new[]{"spyro-level-catalog.json","spyro-object-templates.json"})
            if(!File.Exists(Path.Combine(clone,name)))File.Copy(Path.Combine(sourceWorkspace,name),Path.Combine(clone,name));
        var before=UniversalPlacementReader.Read(Path.Combine(clone,"artisans-native-edits.json"));
        Check(before.Count(p=>!p.Retired)==8,"exact eight saved universal placements");
        Check(UniversalPlacementReader.ReadRemovedNativeRows(Path.Combine(clone,"artisans-native-edits.json")).Order().SequenceEqual(new[]{29,30,113,121,124}),"exact five native removals");
        using var terrain=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(clone,"artisans-terrain-edits.json")));
        var terrainRows=terrain.RootElement.GetProperty("edits").EnumerateArray().Select(e=>e.Clone()).ToArray();
        Check(terrainRows.Length==8&&terrainRows.All(e=>e.GetProperty("deltaZ").GetSingle()==0),"eight saved texture-only terrain edits");
        var savedScene=SceneDocument.Read(Path.Combine(clone,SceneDocument.FileName));
        Check(savedScene.Surfaces.Count==2&&savedScene.Objects.Count==1,"current fresh two-face egg scene");
        var research=Hashes(Path.Combine(clone,"_research"));
        string oldDirectory=Environment.CurrentDirectory;var oldWorkspace=Environment.GetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE");var oldRelease=Environment.GetEnvironmentVariable("SPYRO_EDITOR_RELEASE");
        MainWindow? window=null;
        try
        {
            Environment.CurrentDirectory=clone;Environment.SetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE",clone);Environment.SetEnvironmentVariable("SPYRO_EDITOR_RELEASE","1");
            HeadlessEntryPoint.BuildAvaloniaApp().SetupWithoutStarting();
            window=new MainWindow{Width=1440,Height=900};window.Show();
            Wait(()=>window.CaptureEditorShellSessionSnapshotForTesting().CurrentMobys.Count>0,"initial project load");
            var state=window.CaptureEditorShellSessionSnapshotForTesting();Check(Path.GetFullPath(state.Workspace.RootPath)==clone,"MainWindow writes only copied project");
            var artisans=Get<LevelCatalog>(window,"_retailCatalog").FindByKey("artisans")!;
            RunTask((Task)Invoke(window,"SelectLevelAsync",artisans,null)!);
            string? jp=Environment.GetEnvironmentVariable("SPYRO_UNIVERSAL_JAPAN_IMAGE");if(!string.IsNullOrWhiteSpace(jp))UniversalJapaneseDiscLocator.Configure(state.Workspace,jp);
            Check(((List<UniversalMobyPlacement>)Invoke(window,"CurrentUniversalPlacements")!).Count(p=>!p.Retired)==8,"legacy in-prefix sign plus seven other imports load once");
            string output=Path.Combine(clone,"output","Spyro Editor - Authored Level.bin");
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);byte[] sentinel={1,8,3,9};File.WriteAllBytes(output,sentinel);File.WriteAllText(Path.ChangeExtension(output,".cue"),"prior fixture output");
            var previousOutput=Hashes(Path.Combine(clone,"output"));
            Scope(window,null,0,artifacts,"cancel-1024",1024,720);
            AssertHashes(Path.Combine(clone,"output"),previousOutput,"cancel preserves every previous output");AssertHashes(Path.Combine(clone,"_research"),research,"cancel preserves experimental documents");
            Scope(window,0,0,artifacts,"artisans-1440",1440,900);
            Check(File.Exists(output)&&!File.ReadAllBytes(output).SequenceEqual(sentinel),"normal Create BIN produces the copied project");
            var originalStart=UniversalDiscImage.Read(output);CheckStart(originalStart,0x2402000Au);
            CheckNativeContents(originalStart,artisans,before,terrainRows,clone);
            Keep(output,artifacts,"v1-artisans-start");
            RunTask((Task)Invoke(window,"OpenMainFreshLevelAsync")!);
            Check(window.FreshSceneForTesting?.Id==savedScene.Id,"saved authored scene opened without resetting identity");
            Scope(window,1,1,artifacts,"authored-1024",1024,720);
            var authoredStart=UniversalDiscImage.Read(output);CheckStart(authoredStart,0x24020041u);CheckNativeContents(authoredStart,artisans,before,terrainRows,clone);
            foreach(int entry in new[]{artisans.SourceWadEntry-1,artisans.SourceWadEntry,79,80})Check(originalStart.EntryBytes(entry).SequenceEqual(authoredStart.EntryBytes(entry)),"start choice leaves saved level payload unchanged: "+entry);
            byte[] first=(byte[])originalStart.Exe.Clone();foreach(int at in new[]{0x2F80,0x1DCF0})BinaryPrimitives.WriteUInt32LittleEndian(first.AsSpan(at),0x24020041u);Check(first.SequenceEqual(authoredStart.Exe),"start choices change exactly the two native instructions");
            Keep(output,artifacts,"v1-authored-start");
            Check(Signatures(before).SequenceEqual(Signatures(UniversalPlacementReader.Read(Path.Combine(clone,"artisans-native-edits.json")))),"all saved universal IDs,slots,positions,yaw,special variants and text preserved");
            Check(SceneDocument.Read(Path.Combine(clone,SceneDocument.FileName)).Fingerprint()==savedScene.Fingerprint(),"current authored scene persisted without semantic loss");
            AssertHashes(Path.Combine(clone,"_research"),research,"both exports preserve experimental documents");
            Console.WriteLine("V1_USER_PROJECT_UI_OK removals=5 imports=8 terrainPaints=8 privateTexture=68 freshFaces=2 freshEggs=1 startChoices=2 cancellationPreserved=true nativeRuntime=false clone="+clone);
        }
        finally
        {
            window?.Close();Dispatcher.UIThread.RunJobs();Environment.CurrentDirectory=oldDirectory;Environment.SetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE",oldWorkspace);Environment.SetEnvironmentVariable("SPYRO_EDITOR_RELEASE",oldRelease);
            AssertHashes(snapshotRoot,sourceHashes,"source user-project snapshot remains byte-identical");
        }
    }
    private static void Scope(MainWindow w,int? choice,int defaultChoice,string artifacts,string label,int width,int height)
    {
        w.Width=width;w.Height=height;Pump();var task=(Task)Invoke(w,"CreateObjectTestBinAsync")!;
        Wait(()=>task.IsCompleted||w.OwnedWindows.Any(d=>d.Name=="ProjectExportScope"),"actual project export scope");
        var dialog=w.OwnedWindows.SingleOrDefault(d=>d.Name=="ProjectExportScope");Check(dialog!=null,"Create BIN reaches scope dialog: "+Get<TextBlock>(w,"_statusText").Text);
        var box=Find<ComboBox>(dialog!,"ExportStartLevel");Check(box.SelectedIndex==defaultChoice,"contextual New Game default");
        string included=Find<TextBlock>(dialog!,"ExportIncludedLevels").Text??"";Check(included.Contains("Artisans")&&included.Contains("Untitled Level"),"scope lists both saved scenes");
        Check((Find<TextBlock>(dialog!,"ExportPreservedDrafts").Text??"").Contains("preserved"),"scope explains experimental drafts preserved separately");
        Check(dialog!.WindowStartupLocation==WindowStartupLocation.CenterOwner,"scope uses actual centered modal");
        Frame(w,Path.Combine(artifacts,label+"-owner.png"));Frame(dialog,Path.Combine(artifacts,label+"-dialog.png"));
        File.WriteAllText(Path.Combine(artifacts,label+"-frame.json"),JsonSerializer.Serialize(new{ownerWidth=width,ownerHeight=height,dialogWidth=dialog.Bounds.Width,dialogHeight=dialog.Bounds.Height,dialog.Bounds,dialog.WindowStartupLocation,included,defaultChoice,choice,framesAreActualSeparateWindowCaptures=true}));
        if(choice.HasValue){box.SelectedIndex=choice.Value;Click(Find<Button>(dialog,"ExportCreate"));}else Click(Find<Button>(dialog,"ExportCancel"));
        RunTask(task);
        Console.WriteLine("V1_SCOPE_RESULT "+label+" "+Get<TextBlock>(w,"_statusText").Text);
    }
    private static void CheckNativeContents(UniversalDiscImage disc,LevelDefinition level,IReadOnlyList<UniversalMobyPlacement> expected,JsonElement[] terrainRows,string clone)
    {
        var data=new UniversalLevelData(disc.EntryBytes(level.SourceWadEntry));byte[] overlay=disc.EntryBytes(level.SourceWadEntry-1);
        int update=checked((int)(UniversalExeBindings.Read(disc.Exe,level.LevelId)[UniversalExeBindings.UpdateGlobal].Value-UniversalOverlay.Base));
        Check(BitConverter.ToUInt32(overlay,update+8)==NativeMobySaveMap.Tag,"retail reused rows carry native logical save map");
        int[] map=Enumerable.Range(0,data.Scene().RowCount).Select(i=>(int)BitConverter.ToInt16(overlay,update+64+i*2)).ToArray();
        foreach(var p in expected.Where(p=>!p.Retired))
        {
            int logical=level.SourceRecordCount+p.PersistentSlot!.Value;int physical=Array.IndexOf(map,logical);Check(physical>=0&&map.Count(x=>x==logical)==1,"each actor has one stable logical save identity: "+p.EditorId);
            var row=data.Row(physical);Check(BitConverter.ToInt32(row,12)==p.X&&BitConverter.ToInt32(row,16)==p.Y&&BitConverter.ToInt32(row,20)==p.Z,"actual actor XYZ matches saved project: "+p.EditorId);
            if(p.Yaw.HasValue)Check(row[0x46]==p.Yaw.Value,"native yaw preserved: "+p.EditorId);
        }
        Check(data.Scene().RowCount==level.SourceRecordCount-5+8,"physical holes reused; only active rows emitted");
        var texture=data.Subfiles()[1];Check(BitConverter.ToInt32(data.Bytes,texture.Offset+4)>=69,"private terrain record68 included");
        string sourcePath=GetSource(clone);var source=UniversalDiscImage.Read(sourcePath);var originalData=new UniversalLevelData(source.EntryBytes(level.SourceWadEntry));
        int oldEnvironment=originalData.Subfiles()[3].Offset,newEnvironment=data.Subfiles()[3].Offset;
        foreach(var e in terrainRows)
        {
            int oldWadOffset=Convert.ToInt32(e.GetProperty("faceOffset").GetString()![2..],16);
            int relative=oldWadOffset-source.Entry(level.SourceWadEntry).Offset-oldEnvironment;
            Check(relative>=0&&relative+12<=originalData.Subfiles()[3].Length,"cached HP face is source-bound");
            Check((BitConverter.ToUInt32(originalData.Bytes,oldEnvironment+relative+8)&0x7F)==e.GetProperty("textureIdOriginal").GetInt32(),"original native face texture preimage");
            int actual=newEnvironment+relative;uint word3=BitConverter.ToUInt32(data.Bytes,actual+8);
            Check((word3&0x7F)==e.GetProperty("textureIdEdited").GetInt32(),"actual HP texture assignment retained: "+e.GetProperty("runtimeKey").GetString());
        }
        var fresh=new UniversalLevelData(disc.EntryBytes(80));Check(fresh.Scene().RowCount==1&&BitConverter.ToUInt16(fresh.Row(0),0x36)==0xF5,"fresh native-colour egg actor preserved");
        foreach(var patch in NativeMobySaveMap.Windows())Check(disc.Exe.AsSpan((int)(patch.Address-UniversalExeBindings.ExeBase),patch.Code.Length).SequenceEqual(patch.Code),"native save owner preserved: "+patch.Address.ToString("X8"));
        Console.WriteLine("V1_SAVED_PROJECT_READBACK_OK rows="+data.Scene().RowCount+" originalNative159Class="+BitConverter.ToUInt16(data.Row(159),0x36).ToString("X3"));
    }
    private static string GetSource(string root){using var d=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root,"_local","settings","source-disc.json")));return d.RootElement.GetProperty("imagePath").GetString()!;}
    private static IEnumerable<string> Signatures(IEnumerable<UniversalMobyPlacement> rows)=>rows.OrderBy(p=>p.EditorId,StringComparer.Ordinal).Select(p=>JsonSerializer.Serialize(new{p.EditorId,p.PersistentSlot,p.SourceLevelKey,p.SourceRowIndex,p.X,p.Y,p.Z,p.Yaw,p.SpecialId,p.Text,p.Retired,Overrides=p.RowByteOverrides?.OrderBy(q=>q.Key).ToArray()}));
    private static void CheckStart(UniversalDiscImage d,uint word){foreach(int at in new[]{0x2F80,0x1DCF0})Check(BitConverter.ToUInt32(d.Exe,at)==word,"exact guarded New Game instruction "+at.ToString("X"));}
    private static void Keep(string bin,string dir,string name){File.Copy(bin,Path.Combine(dir,name+".bin"),true);File.WriteAllLines(Path.Combine(dir,name+".cue"),new[]{"FILE \""+name+".bin\" BINARY","  TRACK 01 MODE2/2352","    INDEX 01 00:00:00"});}
    private static Dictionary<string,string> Hashes(string root)=>Directory.Exists(root)?Directory.EnumerateFiles(root,"*",SearchOption.AllDirectories).ToDictionary(p=>Path.GetRelativePath(root,p),p=>Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(p)))):new();
    private static void AssertHashes(string root,Dictionary<string,string> expected,string label){var actual=Hashes(root);Check(actual.Count==expected.Count&&expected.All(p=>actual.TryGetValue(p.Key,out var h)&&h==p.Value),label);}
    private static void CopyTree(string source,string dest){Directory.CreateDirectory(dest);foreach(var p in Directory.EnumerateFiles(source,"*",SearchOption.AllDirectories)){Check((File.GetAttributes(p)&FileAttributes.ReparsePoint)==0,"snapshot file is not external symlink");string target=Path.Combine(dest,Path.GetRelativePath(source,p));Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(p,target);}}
    private static void Frame(Window w,string path){using var frame=w.CaptureRenderedFrame();Check(frame!=null,"actual headless frame");frame!.Save(path);}
    private static void Click(Button b){b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();}
    private static void Pump()=>Dispatcher.UIThread.RunJobs();
    private static void RunTask(Task t){Wait(()=>t.IsCompleted,"async operation",600);t.GetAwaiter().GetResult();}
    private static void Wait(Func<bool> f,string label,int seconds=180){var watch=Stopwatch.StartNew();while(!f()){Pump();if(watch.Elapsed.TotalSeconds>seconds)throw new TimeoutException(label);Thread.Sleep(10);}Pump();}
    private static T Get<T>(object o,string field)=>(T)o.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(o)!;
    private static object? Invoke(object o,string name,params object?[] args)=>o.GetType().GetMethods(BindingFlags.Instance|BindingFlags.NonPublic).Single(m=>m.Name==name&&m.GetParameters().Length==args.Length).Invoke(o,args);
    private static T Find<T>(Control o,string name)where T:Control=>o.GetLogicalDescendants().OfType<T>().First(c=>c.Name==name);
    private static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException("V1 user project: "+message);}
}
