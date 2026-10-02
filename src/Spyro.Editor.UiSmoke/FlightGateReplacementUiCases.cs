using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Universal;

internal static class FlightGateReplacementUiCases
{
    internal static void Run(string image,string catalogRoot)
    {
        image=Path.GetFullPath(image);catalogRoot=Path.GetFullPath(catalogRoot);
        UniversalSourceLibrary.RequireOriginal(image);
        var catalog=LevelCatalog.Load(catalogRoot);var library=new UniversalSourceLibrary(image,catalog);
        string root=Directory.CreateTempSubdirectory("spyro-flight-replacement-ui-").FullName;
        string previous=Environment.CurrentDirectory;
        string? workspace=Environment.GetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE"),release=Environment.GetEnvironmentVariable("SPYRO_EDITOR_RELEASE");
        File.Copy(Path.Combine(catalogRoot,"spyro-level-catalog.json"),Path.Combine(root,"spyro-level-catalog.json"));
        int checks=0;void Check(bool b,string m){if(!b)throw new InvalidOperationException(m);checks++;Console.WriteLine($"FLIGHT_UI {checks}: {m}");}
        MainWindow? window=null;
        try
        {
            Environment.CurrentDirectory=root;Environment.SetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE",root);Environment.SetEnvironmentVariable("SPYRO_EDITOR_RELEASE","1");
            window=new MainWindow();Set(window,"_retailCatalog",catalog);Get<TextBox>(window,"_discImagePathBox").Text=image;
            Set(window,"_universalCatalog",(image,library,(IReadOnlyList<UniversalMobyCatalogEntry>)Array.Empty<UniversalMobyCatalogEntry>()));
            var dst=library.Get("crystalflight");var originals=Rows(dst.Data);Set(window,"_currentLevel",dst.Level);Set(window,"_currentMobys",originals);Set(window,"_selectedMoby",originals[15]);
            window.Show();Pump();Invoke(window,"RefreshActionAvailability");
            Check(Get<Button>(window,"_replaceFlightGateButton").IsVisible&&Get<Button>(window,"_replaceFlightGateButton").IsEnabled,"selected native gate exposes dedicated action");
            string Signature()=>string.Join('|',Get<List<Moby>>(window,"_currentMobys").Select(m=>$"{m.TrueIndex}:{m.IsRemoved}:{m.Position}:{m.Type}:{m.State}:{m.UniversalPlacementId}"));
            var before=Signature();var open=(Task)Invoke(window,"ShowFlightGateReplacementAsync")!;
            Wait(()=>window.OwnedWindows.Any(),"replacement modal");var dialog=window.OwnedWindows.Single();
            Wait(()=>Find<Button>(dialog,"FlightGateReplacementConfirm").IsEnabled,"first preflight");
            var choice=Find<ComboBox>(dialog,"FlightGateReplacementSource");
            Check(choice.ItemCount==3,"only same-category Sunny, Crystal, Wild gate choices appear");
            Check(choice.SelectedItem!.ToString()!.Contains("Sunny"),"foreign source selected with its bonus seconds");
            Check(before==Signature(),"successful preflight makes no scene mutation");
            Click(Find<Button>(dialog,"FlightGateReplacementCancel"));Complete(open);
            Check(before==Signature(),"Cancel preserves all native rows");
            open=(Task)Invoke(window,"ShowFlightGateReplacementAsync")!;Wait(()=>window.OwnedWindows.Any(),"second modal");dialog=window.OwnedWindows.Single();
            Wait(()=>Find<Button>(dialog,"FlightGateReplacementConfirm").IsEnabled,"second preflight");
            // Re-check the input after ready: a mutable original target cannot inherit the old certificate.
            var target=originals[15];target.Position=new(target.Position.X+1,target.Position.Y,target.Position.Z);
            Click(Find<Button>(dialog,"FlightGateReplacementConfirm"));
            Wait(()=>Find<TextBlock>(dialog,"FlightGateReplacementFit").Text!.Contains("Undo edits"),"changed-pose refusal");
            Check(!originals.Any(m=>m.IsAdded)&&!target.IsRemoved,"changed target is rejected before transaction mutation");
            target.Position=target.OriginalPosition;Click(Find<Button>(dialog,"FlightGateReplacementCancel"));Complete(open);
            open=(Task)Invoke(window,"ShowFlightGateReplacementAsync")!;Wait(()=>window.OwnedWindows.Any(),"commit modal");dialog=window.OwnedWindows.Single();
            Wait(()=>Find<Button>(dialog,"FlightGateReplacementConfirm").IsEnabled,"commit preflight");Click(Find<Button>(dialog,"FlightGateReplacementConfirm"));Complete(open);
            var added=originals.Single(m=>m.IsAdded&&!m.IsRemoved);
            Check(target.IsRemoved&&added.CrossLevelSourceLevelKey=="sunnyflight"&&added.CrossLevelSourceTrueIndex==0,"Confirm atomically removes native gate and adds exact source gate");
            Check(added.Position==target.OriginalPosition&&added.YawByte==target.OriginalYawByte,"native target XYZ and yaw preserved");
            var projected=(List<UniversalMobyPlacement>)Invoke(window,"CurrentUniversalPlacements")!;
            Check(projected.Count==1&&projected[0].RowByteOverrides![0x50]==library.Get("sunnyflight").Data.Row(0)[0x50],"actual export projection preserves donor type byte");
            Invoke(window,"RefreshActionAvailability");Check(!Get<Slider>(window,"_selectedMobyZSlider").IsEnabled&&!Get<Button>(window,"_objectEditButton").IsEnabled&&!Get<Button>(window,"_objectCopyButton").IsEnabled,"locked replacement has no Edit or Copy action");
            var pose=added.Position;Invoke(window,"MoveMoby",added,1f,2f,3f,false);Invoke(window,"CopySelectedMoby");Invoke(window,"RotateMobyFromViewport",new MobyRotateRequestedEventArgs(added,(added.YawByte+1)%256));
            Check(added.YawByte==target.OriginalYawByte&&added.Position==pose&&Get<object?>(window,"_mobyClipboard")==null,"viewport movement and direct Copy cannot bypass lock");
            string edits=Path.Combine(root,"crystalflight-native-edits.json");Complete(MobyEditStore.SaveAsync(edits,originals,dst.Level.DisplayName));
            var loaded=Rows(dst.Data);MobyEditStore.Load(edits,loaded);var loadedAdded=loaded.Single(m=>m.IsAdded&&!m.IsRemoved);
            Check(loadedAdded.CrossLevelRecipeId==added.CrossLevelRecipeId&&loaded[15].IsRemoved,"save/reopen retains atomic replacement ownership");
            Check(UniversalPlacementReader.Read(edits).Count==1&&UniversalPlacementReader.ReadRemovedNativeRows(edits).SequenceEqual(new[]{15}),"ordinary readers retain one source placement and one removal");
            var stage=MobySourcePatchExporter.ExportAsync(new(image,Path.ChangeExtension(image,".cue"),Path.Combine(root,"native-removal"),dst.Level,edits,true));Complete(stage);
            Check(stage.Result.Plan.PatchCount==3,"ordinary native writer stages three coordinate words");
            string staged=Path.Combine(root,"native-removal.bin"),output=Path.Combine(root,"ordinary-export.bin");
            var export=Task.Run(()=>UniversalMobyExport.Apply(library,staged,output,catalog,new Dictionary<string,IReadOnlyList<UniversalMobyPlacement>>{{"crystalflight",UniversalPlacementReader.Read(edits)}},removedRowsByLevel:new Dictionary<string,IReadOnlyCollection<int>>{{"crystalflight",[15]}},removedTreasureAlreadyApplied:true));Complete(export);
            var compose=export.Result.Levels.Single().Compose;var written=UniversalDiscImage.Read(output);var data=new UniversalLevelData(written.EntryBytes(dst.Level.SourceWadEntry));
            Check(data.Bytes.SequenceEqual(compose.LevelData)&&written.EntryBytes(dst.Level.SourceWadEntry-1).SequenceEqual(compose.Overlay),"ordinary exported full data and overlay read back");
            Check(data.Scene().RowCount==dst.Data.Scene().RowCount&&compose.NewRowIndices.Single()==15,"export reuses original objective row and preserves population");
            var final=data.CaptureRow("crystalflight",15);var native=dst.Data.CaptureRow("crystalflight",15);
            Check(final.Properties.AsSpan(0,16).SequenceEqual(native.Properties.AsSpan(0,16))&&BitConverter.ToInt32(final.Properties,24)==1,"native crossing plane and original category read back");
            Set(window,"_currentMobys",loaded);Set(window,"_selectedMoby",loadedAdded);Invoke(window,"UndoMoby",loadedAdded);
            Check(loadedAdded.IsRemoved&&!loaded[15].IsRemoved,"Undo Object after reopen restores native row and retires import");
            Complete(MobyEditStore.SaveAsync(edits,loaded,dst.Level.DisplayName));Check(UniversalPlacementReader.Read(edits).All(p=>p.Retired),"undo retains lifetime identity without an active import");
            Set(window,"_selectedMoby",loaded[15]);open=(Task)Invoke(window,"ShowFlightGateReplacementAsync")!;Wait(()=>window.OwnedWindows.Any(),"repeat modal");dialog=window.OwnedWindows.Single();Wait(()=>Find<Button>(dialog,"FlightGateReplacementConfirm").IsEnabled,"repeat preflight with retired identity");Click(Find<Button>(dialog,"FlightGateReplacementConfirm"));Complete(open);
            var second=loaded.Single(m=>m.IsAdded&&!m.IsRemoved);Check(second.UniversalPlacementId!=loadedAdded.UniversalPlacementId,"repeat replacement uses a new lifetime identity");
            Check(!(bool)Invoke(window,"UndoFlightGateReplacement",loadedAdded)!&&!second.IsRemoved&&loaded[15].IsRemoved,"retired prior replacement cannot undo a newer active transaction");
            Invoke(window,"UndoLastRemovedMoby");Check(second.IsRemoved&&!loaded[15].IsRemoved,"Undo Remove atomically restores original gate");
            // Catalog route: 12B permits normal placement after one matching-category removal.
            var night=library.Get("nightflight");var chestRows=Rows(night.Data);int chest=Enumerable.Range(0,night.Data.Scene().RowCount).First(i=>BitConverter.ToUInt16(night.Data.Row(i),0x36)==0x12B);
            Set(window,"_currentMobys",chestRows);Set(window,"_currentLevel",night.Level);Set(window,"_selectedMoby",chestRows[chest]);Invoke(window,"RemoveSelectedMoby");
            var source=library.Get("icyflight");int sourceRow=28;var sourceTemplate=source.Data.CaptureRow("icyflight",sourceRow);
            var variant=new UniversalMobyVariant(0x12B,"icyflight",sourceRow,24,0,0,true,false,true);
            var entry=new UniversalMobyCatalogEntry(0x12B,"Flight chest","Interactive","Chests & Containers",[variant],variant,["icyflight"],null);
            Set(window,"_universalCatalog",(image,library,(IReadOnlyList<UniversalMobyCatalogEntry>)new[]{entry}));
            open=(Task)Invoke(window,"ShowUniversalMobyLibraryAsync",window,null,null)!;Wait(()=>window.OwnedWindows.Any(),"normal catalog modal");dialog=window.OwnedWindows.Single();
            Wait(()=>Find<ListBox>(dialog,"UniversalMobyList").ItemCount>0,"catalog loaded");Find<ListBox>(dialog,"UniversalMobyList").SelectedItem=entry;
            Wait(()=>Find<Button>(dialog,"UniversalMobyAddHere").IsEnabled,"12B normal Add preflight after removal");
            Check(chestRows.Count==night.Data.Scene().RowCount,"catalog preflight makes no authored mutation");
            Click(Find<Button>(dialog,"UniversalMobyAddHere"));Complete(open);
            var chestAdded=chestRows.Single(m=>m.IsAdded&&!m.IsRemoved);Check(chestRows[chest].IsRemoved&&chestAdded.CrossLevelSourceLevelKey=="icyflight","normal catalog Add reaches supported 12B replacement");
            string chestEdits=Path.Combine(root,"nightflight-native-edits.json");Complete(MobyEditStore.SaveAsync(chestEdits,chestRows,night.Level.DisplayName));
            var chestStage=MobySourcePatchExporter.ExportAsync(new(image,Path.ChangeExtension(image,".cue"),Path.Combine(root,"chest-native-removal"),night.Level,chestEdits,true));Complete(chestStage);
            var chestPrepared=UniversalDiscImage.Read(Path.Combine(root,"chest-native-removal.bin"));
            var host=UniversalCompositionHost.FromPreparedImage(image,Path.Combine(root,"chest-native-removal.bin"),night.Level);
            var check=host.DryRun(library,UniversalPlacementReader.Read(chestEdits),null,UniversalPlacementReader.ReadRemovedNativeRows(chestEdits));
            Check(check.Error==null&&check.Result!=null,"actual saved catalog type/state projection composes over native staged removal bytes");
            File.WriteAllText(Path.Combine(root,"ui-receipt.json"),JsonSerializer.Serialize(new{Checks=checks,Gate="Sunny0 -> Crystal15",Chest=$"Icy28 -> Night{chest}",Evidence="Headless actual controls, source-authenticated preflight, saved ordinary export/readback. No new gameplay acceptance."},new JsonSerializerOptions{WriteIndented=true}));
            Console.WriteLine($"FLIGHT_REPLACEMENT_UI_OK checks={checks} evidence={root}");
        }
        finally {if(window!=null){foreach(var owned in window.OwnedWindows.ToArray())owned.Close();Set(window,"_allowCloseWithUnsavedTerrain",true);window.Close();}Pump();Environment.CurrentDirectory=previous;Environment.SetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE",workspace);Environment.SetEnvironmentVariable("SPYRO_EDITOR_RELEASE",release);}
    }
    private static List<Moby> Rows(UniversalLevelData data)=>Enumerable.Range(0,data.Scene().RowCount).Select(i=>
    {
        var r=data.Row(i);Vector3f p=new(BitConverter.ToInt32(r,12)/16f,BitConverter.ToInt32(r,16)/16f,BitConverter.ToInt32(r,20)/16f);string label=$"Native row {i}";
        return new Moby{Index=i,TrueIndex=i,Position=p,OriginalPosition=p,Label=label,OriginalLabel=label,Type=r[0x50],OriginalType=r[0x50],State=r[0x51],OriginalState=r[0x51],YawByte=r[0x46],OriginalYawByte=r[0x46],SourceByte36=r[0x36],OriginalSourceByte36=r[0x36],SourceByte37=r[0x37],OriginalSourceByte37=r[0x37],SourceByte4F=r[0x4F],OriginalSourceByte4F=r[0x4F],Flag4A=r[0x52],OriginalFlag4A=r[0x52],Flag4B=r[0x53],OriginalFlag4B=r[0x53]};
    }).ToList();
    private static T Get<T>(object o,string n)=>(T)o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(o)!;
    private static void Set(object o,string n,object? v)=>o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(o,v);
    private static object? Invoke(object o,string n,params object?[] a)=>o.GetType().GetMethod(n,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(o,a);
    private static T Find<T>(Control o,string n) where T:Control=>o.GetLogicalDescendants().OfType<T>().Single(c=>c.Name==n);
    private static void Click(Button b)=>b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Pump()=>Dispatcher.UIThread.RunJobs();
    private static void Wait(Func<bool> p,string name){DateTime until=DateTime.UtcNow.AddSeconds(120);while(!p()){Pump();if(DateTime.UtcNow>until)throw new TimeoutException(name);Thread.Sleep(5);}Pump();}
    private static void Complete(Task t){Wait(()=>t.IsCompleted,"async action");t.GetAwaiter().GetResult();Pump();}
}
