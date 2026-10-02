using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Universal;

internal static class DragonColorUiCases
{
    internal static void Run()
    {
        int checks=0;void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;}
        string root=Directory.CreateTempSubdirectory("spyro-dragon-colour-ui-").FullName,previous=Environment.CurrentDirectory;
        string? workspace=Environment.GetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE"),release=Environment.GetEnvironmentVariable("SPYRO_EDITOR_RELEASE");
        File.WriteAllText(Path.Combine(root,"spyro-level-catalog.json"),"{\"levels\":[]}");
        MainWindow? window=null;
        try
        {
            Environment.CurrentDirectory=root;Environment.SetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE",root);Environment.SetEnvironmentVariable("SPYRO_EDITOR_RELEASE","1");
            window=new MainWindow();Invoke(window,"ApplyFreshScene",SceneDocument.New("Dragon Colour"));window.Show();Pump();
            var mobys=Get<List<Moby>>(window,"_currentMobys");
            var dragon=new Moby{Index=0,TrueIndex=0,Type=0x30,OriginalType=0x30,SourceByte36=0xFA,OriginalSourceByte36=0xFA,
                Label="Dragon",OriginalLabel="Dragon",Position=new(560,560,560),OriginalPosition=new(560,560,560),IsAdded=true,
                CrossLevelSourceLevelKey="artisans",CrossLevelSourceTrueIndex=88,CrossLevelTemplateId="universal:artisans:88",
                CrossLevelRequiredExporterFeature=UniversalMobyExport.RequiredExporterFeature};mobys.Add(dragon);
            Task Edit()=> (Task)Invoke(window,"EditMobyAsync",dragon)!;
            Window Dialog()=>window.OwnedWindows.Single();
            T Find<T>(Window d,string name)where T:Control=>d.GetLogicalDescendants().OfType<T>().Single(c=>c.Name==name);
            TextBox Hex(Window d)=>Find<Control>(d,"DragonColorPicker").GetLogicalDescendants().OfType<TextBox>().Single();
            void Click(Window d,string label)=>d.GetLogicalDescendants().OfType<Button>().Single(b=>b.Content is string s&&s==label).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            string signature=(string)Invoke(window,"BuildMobyEditSignature",mobys)!;
            var edit=Edit();Pump();var dialog=Dialog();
            Check(!Find<Control>(dialog,"DragonColorPicker").IsEnabled,"original appearance is the default");
            Find<CheckBox>(dialog,"DragonCustomColor").IsChecked=true;
            Hex(dialog).Text="invalid";Click(dialog,"Apply changes");Pump();
            Check(!edit.IsCompleted&&dragon.DragonColor==null,"invalid colour leaves dialog open and object unchanged");
            Hex(dialog).Text="#E62AB8";Click(dialog,"Apply changes");Complete(edit);
            var magenta=ColorRgba.FromRgb(230,42,184);
            Check(dragon.DragonColor==magenta,"actual edit dialog applies arbitrary hex colour");
            Check(signature!=(string)Invoke(window,"BuildMobyEditSignature",mobys)!,"colour-only edits mark project dirty");
            edit=Edit();Pump();dialog=Dialog();Hex(dialog).Text="#00FF00";Click(dialog,"Cancel");Complete(edit);
            Check(dragon.DragonColor==magenta,"Cancel preserves existing colour");
            Set(window,"_selectedMoby",dragon);Invoke(window,"CopySelectedMoby");
            var clipboard=Get<object>(window,"_mobyClipboard");
            var copy=(Moby)Invoke(window,"CreateMobyFromClipboard",clipboard,new Vector3f(600,560,560),(int?)1,(int?)1)!;
            Check(copy.DragonColor==magenta&&copy.UniversalPlacementId!=dragon.UniversalPlacementId,"Copy retains colour with independent identity");mobys.Add(copy);
            copy.DragonColor=ColorRgba.FromRgb(40,220,96);
            string path=Path.Combine(root,"artisans-native-edits.json");Complete(MobyEditStore.SaveAsync(path,mobys,"Artisans"));
            List<Moby> reopened=[];MobyEditStore.Load(path,reopened);
            Check(reopened[0].DragonColor==magenta&&reopened[1].DragonColor==copy.DragonColor,"retail save/reopen retains independent colours");
            var placements=UniversalPlacementReader.Read(path);
            Check(placements[0].DragonColor==magenta&&placements[1].DragonColor==copy.DragonColor,"normal export reader retains colours");
            string scene=Path.Combine(root,SceneDocument.FileName);
            File.WriteAllBytes(scene,window.FreshSceneForTesting!.Serialize());
            Check(SceneDocument.Read(scene).ProjectObjects()[0].DragonColor==magenta,"fresh project retains custom colour");
            var actual=(List<UniversalMobyPlacement>)Invoke(window,"CurrentUniversalPlacements")!;
            Check(actual[0].DragonColor==magenta&&actual[1].DragonColor==copy.DragonColor,"fresh normal export retains per-statue colours");
            edit=Edit();Pump();dialog=Dialog();Click(dialog,"Use original color");Click(dialog,"Apply changes");Complete(edit);
            Check(dragon.DragonColor==null&&copy.DragonColor!=null,"reset restores only chosen statue");
            Complete(MobyEditStore.SaveAsync(path,mobys,"Artisans"));MobyEditStore.Load(path,reopened);
            Check(reopened[0].DragonColor==null&&reopened[1].DragonColor==copy.DragonColor,"reset survives save/reopen");
            var native=new Moby{Index=88,TrueIndex=88,Type=0x30,OriginalType=0x30,SourceByte36=0xFA,OriginalSourceByte36=0xFA,DragonColor=magenta};
            string nativePath=Path.Combine(root,"native-colour-only.json");Complete(MobyEditStore.SaveAsync(nativePath,[native],"Artisans"));
            native.DragonColor=null;Check(MobyEditStore.Load(nativePath,[native])==1&&native.DragonColor==magenta,"native colour-only edits reload and count as edits");
            native.UndoEdits();Check(native.DragonColor==null&&!native.HasMetadataEdit,"Undo restores original native colour");
            Console.WriteLine($"DRAGON_COLOUR_UI_OK checks={checks} evidence={root}");
        }
        finally{if(window!=null){Set(window,"_allowCloseWithUnsavedTerrain",true);window.Close();}Pump();Environment.CurrentDirectory=previous;Environment.SetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE",workspace);Environment.SetEnvironmentVariable("SPYRO_EDITOR_RELEASE",release);}
    }
    private static T Get<T>(object o,string n)=>(T)o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(o)!;
    private static void Set(object o,string n,object v)=>o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(o,v);
    private static object? Invoke(object o,string n,params object?[] a)=>o.GetType().GetMethod(n,BindingFlags.Instance|BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(o,a);
    private static void Pump()=>Dispatcher.UIThread.RunJobs();
    private static void Complete(Task task){DateTime until=DateTime.UtcNow.AddSeconds(30);while(!task.IsCompleted){Pump();if(DateTime.UtcNow>until)throw new TimeoutException();Thread.Sleep(5);}task.GetAwaiter().GetResult();Pump();}
}
