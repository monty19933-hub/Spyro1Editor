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

internal static class MagicArtisansCapacityUiCases
{
    internal static void Run(string image,string catalogRoot)
    {
        image=Path.GetFullPath(image);catalogRoot=Path.GetFullPath(catalogRoot);
        UniversalSourceLibrary.RequireOriginal(image);
        var catalog=LevelCatalog.Load(catalogRoot);var library=new UniversalSourceLibrary(image,catalog);
        string? evidence=Environment.GetEnvironmentVariable("SPYRO_MAGIC_UI_EVIDENCE");
        string root=evidence==null?Directory.CreateTempSubdirectory("spyro-magic-artisans-ui-").FullName:Path.Combine(evidence,"run-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);if(evidence!=null)File.WriteAllText(Path.Combine(evidence,"latest-evidence.txt"),root);string previous=Environment.CurrentDirectory;
        string? workspace=Environment.GetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE"),release=Environment.GetEnvironmentVariable("SPYRO_EDITOR_RELEASE");
        File.Copy(Path.Combine(catalogRoot,"spyro-level-catalog.json"),Path.Combine(root,"spyro-level-catalog.json"),true);
        int checks=0;void Check(bool b,string m){if(!b)throw new InvalidOperationException(m);checks++;Console.WriteLine($"MAGIC_UI {checks}: {m}");}
        MainWindow? window=null;
        try
        {
            Environment.CurrentDirectory=root;Environment.SetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE",root);Environment.SetEnvironmentVariable("SPYRO_EDITOR_RELEASE","1");
            window=new MainWindow();Set(window,"_retailCatalog",catalog);Get<TextBox>(window,"_discImagePathBox").Text=image;
            var dst=library.Get("artisans");var originals=Rows(dst.Data);MobyIdentityClassifier.Apply("artisans",originals);
            Check(originals[29].DisplayLabel=="Regular Gnorc"&&originals[7].DisplayLabel=="Treasure Gnorc","names come from the actual native catalog identities");
            Set(window,"_currentLevel",dst.Level);Set(window,"_currentMobys",originals);Get<EditorViewport>(window,"_viewport").Mobys=originals;Set(window,"_selectedMoby",originals[7]);
            var source=library.Get("magiccrafters");
            var sourceVariants=UniversalMobyCatalogBuilder.AllVariants(library,new LevelCatalog([source.Level])).Where(v=>v.ClassId==271).ToArray();
            UniversalMobyVariant Variant(int row)=>sourceVariants.Single(v=>v.SourceRow==row);
            Check(sourceVariants.Length==14&&Variant(12).Extractable&&Variant(13).Extractable,"the actual catalog issues both pair members among fourteen native variants");
            var unsupported=Variant(11);var pair=Variant(12);var otherRoot=Variant(13);
            var entry=new UniversalMobyCatalogEntry(271,"Armored Druid","Enemies","",sourceVariants,unsupported,["magiccrafters"],null);
            Set(window,"_universalCatalog",(image,library,(IReadOnlyList<UniversalMobyCatalogEntry>)new[]{entry}));
            window.Show();Pump();
            Task open=null!;Window dialog=null!;
            void Open()
            {
                open=(Task)Invoke(window,"ShowUniversalMobyLibraryAsync",window,null,null)!;Wait(()=>window.OwnedWindows.Any(),"catalog modal");dialog=window.OwnedWindows.Single();
                Wait(()=>Find<ListBox>(dialog,"UniversalMobyList").ItemCount>0,"catalog loaded");Find<ListBox>(dialog,"UniversalMobyList").SelectedItem=entry;
                Wait(()=>Find<TextBlock>(dialog,"UniversalMagicArtisansAdvice").IsVisible,"scoped advice");
            }
            void Close(){Click(Find<Button>(dialog,"UniversalMobyClose"));Complete(open);}
            string Signature()=>string.Join('|',originals.Select(m=>$"{m.TrueIndex}:{m.IsRemoved}:{m.Position}:{m.Type}:{m.State}"));
            Open();string before=Signature();var advice=Find<TextBlock>(dialog,"UniversalMagicArtisansAdvice").Text!;
            Check(advice.Contains("Regular Gnorc: T29, T30, T32, T33")&&advice.Contains("Treasure Gnorc: T7, T31"),"picker lists all six exact native objects with catalog names");
            Check(advice.Contains("13 gems")&&advice.Contains("remaining original objects unchanged"),"reward loss and retained-host limits are visible");
            Check(Find<Button>(dialog,"UniversalMagicArtisansPair").IsVisible&&!Find<Button>(dialog,"UniversalMobyAddHere").IsEnabled,"unsupported version offers explicit supported-pair selection and cannot add");
            Click(Find<Button>(dialog,"UniversalMagicArtisansPair"));
            Check(((UniversalMobyVariant)Find<ComboBox>(dialog,"UniversalMobyVariant").SelectedItem!).SourceRow==12,"supported-pair action selects original row12 explicitly");
            Check(Find<TextBlock>(dialog,"UniversalMagicArtisansAdvice").Text!.Contains("T12 and T13")&&!Find<Button>(dialog,"UniversalMagicArtisansPair").IsVisible,"pair requirements replace unsupported-version advice");
            Click(Find<Button>(dialog,"UniversalMobyFindRoom"));
            Wait(()=>Find<Button>(dialog,"UniversalMobyInspectPlan").IsVisible||Find<TextBlock>(dialog,"UniversalMobyRoomExplanation").Text!.Contains("did not pass"),"prospective exact removal proof");
            string space=Find<TextBlock>(dialog,"UniversalMobyRoomExplanation").Text!;Console.WriteLine(space);
            Check(Find<Button>(dialog,"UniversalMobyInspectPlan").IsVisible,"full export check issues the exact six-row plan");
            Check(space.Contains("132.0 KB of model memory")&&space.Contains("2.1 KB of texture space")&&space.Contains("86.6 KB of game memory"),"memory figures derive from the actual successful compose result");
            Check(before==Signature()&&!Find<Button>(dialog,"UniversalMobyAddHere").IsEnabled,"review does not delete objects or enable premature Add");
            Find<ComboBox>(dialog,"UniversalMobyVariant").SelectedItem=unsupported;
            Check(!Find<Button>(dialog,"UniversalMobyInspectPlan").IsVisible&&!Find<ListBox>(dialog,"UniversalMobyRemovalPlans").IsVisible,"switching versions clears the previous checked plan");
            Click(Find<Button>(dialog,"UniversalMagicArtisansPair"));Click(Find<Button>(dialog,"UniversalMobyFindRoom"));Wait(()=>Find<Button>(dialog,"UniversalMobyInspectPlan").IsVisible,"rechecked supported plan");
            Click(Find<Button>(dialog,"UniversalMobyInspectPlan"));Complete(open);Wait(()=>window.OwnedWindows.Any(),"review window");var review=window.OwnedWindows.Single();
            var objects=Find<ListBox>(review,"UniversalRemovalObjects");var listed=objects.ItemsSource!.Cast<Moby>().ToArray();
            Check(listed.Select(m=>m.TrueIndex).SequenceEqual(new[]{7,29,30,31,32,33}),"review displays precisely the six required original identities");
            objects.SelectedItem=originals[31];Click(Find<Button>(review,"UniversalRemovalLocate"));Pump();
            Check(Get<Moby>(window,"_selectedMoby")==originals[31]&&before==Signature(),"Select and show locates an existing Gnorc without removing it");
            var camera=Get<EditorViewport>(window,"_viewport").CaptureNavigationSnapshotForTesting();
            Click(Find<Button>(review,"UniversalRemovalRemove"));
            Check(originals[31].IsRemoved&&!objects.ItemsSource!.Cast<Moby>().Contains(originals[31]),"inspector Remove uses normal removal and updates its list");
            var afterRemoval=Get<EditorViewport>(window,"_viewport").CaptureNavigationSnapshotForTesting();
            Check(afterRemoval with { SelectedMobyIndex=camera.SelectedMobyIndex }==camera,"inspector removal preserves the camera position");
            Check(!Find<Button>(review,"UniversalRemovalRemove").IsEnabled,"removal clears selection so a second click cannot remove another object");
            Invoke(window,"UndoLastRemovedMoby");
            Check(before==Signature(),"normal Undo last removal restores the inspector deletion");
            objects.SelectedItem=originals[29];var currentLevel=Get<LevelDefinition>(window,"_currentLevel");
            Set(window,"_currentLevel",catalog.FindByKey("stonehill"));Click(Find<Button>(review,"UniversalRemovalRemove"));
            Check(before==Signature()&&!Find<Button>(review,"UniversalRemovalRemove").IsEnabled,"an old inspector cannot delete after changing levels");
            Set(window,"_currentLevel",currentLevel);
            review.Close();Pump();
            Set(window,"_selectedMoby",originals[7]);Invoke(window,"RemoveSelectedMoby");
            Open();Click(Find<Button>(dialog,"UniversalMagicArtisansPair"));Click(Find<Button>(dialog,"UniversalMobyFindRoom"));
            Wait(()=>Find<Button>(dialog,"UniversalMobyInspectPlan").IsVisible,"partial-removal plan");
            Check(Find<TextBlock>(dialog,"UniversalMagicArtisansAdvice").Text!.Contains("T7 (removed)")&&Find<TextBlock>(dialog,"UniversalMobyRoomExplanation").Text!.Contains("5 of the six"),"partial progress shows only the five remaining removals");Close();
            Set(window,"_selectedMoby",originals[0]);Invoke(window,"RemoveSelectedMoby");Open();Click(Find<Button>(dialog,"UniversalMagicArtisansPair"));Click(Find<Button>(dialog,"UniversalMobyFindRoom"));
            Check(Find<TextBlock>(dialog,"UniversalMobyRoomExplanation").Text!.Contains("Undo other original-object removals")&&!Find<Button>(dialog,"UniversalMobyAddHere").IsEnabled,"additional native removal gives an actionable refusal");Close();Invoke(window,"UndoLastRemovedMoby");
            originals[1].Position=new(originals[1].Position.X+1,originals[1].Position.Y,originals[1].Position.Z);
            Open();Click(Find<Button>(dialog,"UniversalMagicArtisansPair"));
            Check(Find<TextBlock>(dialog,"UniversalMobyFitText").Text!.Contains("Undo changes")&&!Find<Button>(dialog,"UniversalMobyAddHere").IsEnabled,"retained original pose edits cannot inherit a checked fit");Close();originals[1].Position=originals[1].OriginalPosition;
            foreach(int row in new[]{29,30,31,32,33}){Set(window,"_selectedMoby",originals[row]);Invoke(window,"RemoveSelectedMoby");}
            Check(originals.Where(m=>m.IsRemoved).Select(m=>m.TrueIndex).Order().SequenceEqual(new[]{7,29,30,31,32,33}),"normal Remove actions change exactly the allowed six objects");
            string edits=Path.Combine(root,"artisans-native-edits.json");Complete(MobyEditStore.SaveAsync(edits,originals,dst.Level.DisplayName));
            var reopened=Rows(dst.Data);MobyIdentityClassifier.Apply("artisans",reopened);MobyEditStore.Load(edits,reopened);originals=reopened;Set(window,"_currentMobys",reopened);Get<EditorViewport>(window,"_viewport").Mobys=reopened;Set(window,"_selectedMoby",reopened[8]);
            Open();Click(Find<Button>(dialog,"UniversalMagicArtisansPair"));
            Wait(()=>Find<Button>(dialog,"UniversalMobyAddHere").IsEnabled,"saved/reopened six deletion fit");
            Check(Find<TextBlock>(dialog,"UniversalMobyFitText").Text!.Contains("ready to place"),"saved/reopened removals enable the ordinary pair Add action");
            Find<ComboBox>(dialog,"UniversalMobyVariant").SelectedItem=otherRoot;
            Wait(()=>Find<Button>(dialog,"UniversalMobyAddHere").IsEnabled,"other pair member asynchronous fit");
            Check(Find<Button>(dialog,"UniversalMobyAddHere").IsEnabled,"either original pair member can root the supported group");
            Click(Find<Button>(dialog,"UniversalMobyAddHere"));Complete(open);
            Check(originals.Count(m=>m.IsAdded&&!m.IsRemoved)==2,"ordinary Add action creates the two linked actors");
            var placements=(List<UniversalMobyPlacement>)Invoke(window,"CurrentUniversalPlacements")!;
            Check(placements.Select(p=>p.SourceRowIndex).Order().SequenceEqual(new[]{12,13})&&placements.Select(p=>p.LinkGroupId).Distinct().Count()==1,"ordinary UI retains exact pair source identities and one link group");
            Complete(MobyEditStore.SaveAsync(edits,originals,dst.Level.DisplayName));
            var exported=UniversalMobyCatalogBuilder.DryRun(library,dst.Level,UniversalPlacementReader.Read(edits),removedRows:UniversalPlacementReader.ReadRemovedNativeRows(edits));
            Check(exported.Error==null&&exported.Result?.DroppedModelBytes==135168,"saved UI pair passes the actual ordinary composer with precise reclaimed memory");
            Open();Click(Find<Button>(dialog,"UniversalMagicArtisansPair"));
            Check(Find<TextBlock>(dialog,"UniversalMobyFitText").Text!.Contains("Remove the current added objects")&&!Find<Button>(dialog,"UniversalMobyAddHere").IsEnabled,"a second active pair is clearly refused");Close();
            var dark=library.Get("darkhollow");Set(window,"_currentLevel",dark.Level);Set(window,"_currentMobys",Rows(dark.Data));
            open=(Task)Invoke(window,"ShowUniversalMobyLibraryAsync",window,null,null)!;Wait(()=>window.OwnedWindows.Any(),"other host modal");dialog=window.OwnedWindows.Single();Wait(()=>Find<ListBox>(dialog,"UniversalMobyList").ItemCount>0,"other host catalog");Find<ListBox>(dialog,"UniversalMobyList").SelectedItem=entry;
            Check(!Find<TextBlock>(dialog,"UniversalMagicArtisansAdvice").IsVisible&&!Find<Button>(dialog,"UniversalMagicArtisansPair").IsVisible,"Artisans advice stays scoped to its destination");Close();
            UniversalSourceLibrary.RequireOriginal(image);Check(true,"unchanged original disc hash verified after UI work");
            File.WriteAllText(Path.Combine(root,"ui-receipt.json"),JsonSerializer.Serialize(new{Checks=checks,RemovedRows=new[]{7,29,30,31,32,33},ModelBytes=135168,TextureBytes=2176,FinalHeadroom=88728,Evidence="Headless actual picker/review/locate/manual Remove/save-reopen/Add controls and ordinary composer. No new gameplay acceptance."},new JsonSerializerOptions{WriteIndented=true}));
            Console.WriteLine($"MAGIC_ARTISANS_CAPACITY_UI_OK checks={checks} evidence={root}");
        }
        finally {if(window!=null){foreach(var owned in window.OwnedWindows.ToArray())owned.Close();Set(window,"_allowCloseWithUnsavedTerrain",true);window.Close();}Pump();Environment.CurrentDirectory=previous;Environment.SetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE",workspace);Environment.SetEnvironmentVariable("SPYRO_EDITOR_RELEASE",release);}
    }
    private static List<Moby> Rows(UniversalLevelData data)=>Enumerable.Range(0,data.Scene().RowCount).Select(i=>
    {
        var r=data.Row(i);Vector3f p=new(BitConverter.ToInt32(r,12)/16f,BitConverter.ToInt32(r,16)/16f,BitConverter.ToInt32(r,20)/16f);string label=Moby.FallbackLabel(r[0x50]);
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
