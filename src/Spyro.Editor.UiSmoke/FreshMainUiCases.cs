using System.Diagnostics;
using System.Reflection;
using System.Buffers.Binary;
using Spyro.Editor.Core.Primitives;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Universal;

internal static class FreshMainUiCases
{
    internal static void Run(string outputDirectory)
    {
        RunAuthoredMeshChecks();
        MainWindow window=new(){Width=1440,Height=900};window.Show();
        try
        {
            Wait(()=>window.CaptureEditorShellSessionSnapshotForTesting().CurrentMobys.Count>0,"initial retail load");
            var toasty=Get<Spyro.Editor.Core.Levels.LevelCatalog>(window,"_retailCatalog").FindByKey("toasty")!;
            RunTask((Task)Invoke(window,"SelectLevelAsync",toasty,null)!);
            var nativeMoby=window.CaptureEditorShellSessionSnapshotForTesting().CurrentMobys.First(m=>!m.IsAdded&&m.TrueIndex>=0);
            Set(window,"_selectedMoby",nativeMoby);Invoke(window,"NudgeSelectedMoby",0f,0f,16f);
            int editedNativeRow=nativeMoby.TrueIndex;
            RunTask((Task)Invoke(window,"SaveCurrentEditsAsync",new object?[]{null})!);
            var removedNative=window.CaptureEditorShellSessionSnapshotForTesting().CurrentMobys.Last(m=>!m.IsAdded&&!m.IsEditorControl&&m.TrueIndex>editedNativeRow);
            Set(window,"_selectedMoby",removedNative);Invoke(window,"RemoveSelectedMoby");
            var removedInterior=window.CaptureEditorShellSessionSnapshotForTesting().CurrentMobys.Single(m=>m.TrueIndex==49);
            Set(window,"_selectedMoby",removedInterior);Invoke(window,"RemoveSelectedMoby");
            RunTask((Task)Invoke(window,"SaveCurrentEditsAsync",new object?[]{null})!);
            RunTask((Task)Invoke(window,"CreateObjectTestBinAsync")!);
            string deleteOnlyOutput=Path.Combine(window.CaptureEditorShellSessionSnapshotForTesting().Workspace.RootPath,"output","Spyro Editor - All Saved Edits.bin");
            Check(File.Exists(deleteOnlyOutput),"normal retail delete-only Create BIN produces an image: "+Get<TextBlock>(window,"_statusText").Text);
            var deleteOnly=UniversalDiscImage.Read(deleteOnlyOutput);
            Check(new UniversalLevelData(deleteOnly.EntryBytes(toasty.SourceWadEntry)).Scene().RowCount==toasty.SourceRecordCount-1,"normal retail delete-only export compacts the native actor row");
            Check(NativeMobySaveMap.Windows().All(w=>deleteOnly.Exe.AsSpan(checked((int)(w.Address-UniversalExeBindings.ExeBase)),w.Code.Length).SequenceEqual(w.Code)),"normal retail delete-only export installs canonical save-map owners");
            Console.WriteLine("RETAIL_DELETE_ONLY_UI_OK");
            Click(Find<Button>(window,"UniversalMobyLibraryButton"));
            Wait(()=>window.OwnedWindows.Any(w=>w.Title=="Add object — Toasty"),"retail global browser");
            var retailBrowser=window.OwnedWindows.Single(w=>w.Title=="Add object — Toasty");
            var retailList=Find<ListBox>(retailBrowser,"UniversalMobyList");
            Wait(()=>retailList.ItemsSource?.Cast<UniversalMobyCatalogEntry>().Any(e=>e.ClassId==0x00A)==true,"retail catalog");
            Find<TextBox>(retailBrowser,"UniversalMobySearch").Text="0x00A";Pump();
            retailList.SelectedItem=retailList.ItemsSource!.Cast<UniversalMobyCatalogEntry>().Single(e=>e.ClassId==0x00A);Pump();
            Wait(()=>Find<Button>(retailBrowser,"UniversalMobyAddHere").IsEnabled,"retail sheep admission");
            Click(Find<Button>(retailBrowser,"UniversalMobyAddHere"));
            Wait(()=>window.CaptureEditorShellSessionSnapshotForTesting().CurrentMobys.Any(m=>m.IsAdded),"retail sheep placement");
            RunTask((Task)Invoke(window,"SaveCurrentEditsAsync",new object?[]{null})!);
            Get<TextBox>(window,"_levelTextReplacementBox").Text="TWIN TEST";
            RunTask((Task)Invoke(window,"SaveLevelTextPlanAsync")!);
            string retailRoot=window.CaptureEditorShellSessionSnapshotForTesting().Workspace.RootPath;
            bool flightOn=Environment.GetEnvironmentVariable("SPYRO_FRESH_MAIN_FLIGHT_ON")=="1";
            Spyro.Editor.Core.Persistence.NativeFreeFlightSettingsStore.SaveGlobal(
                Spyro.Editor.Core.Persistence.NativeFreeFlightSettingsStore.Capture(retailRoot),flightOn);
            using var retailEdits=System.Text.Json.JsonDocument.Parse(File.ReadAllBytes(Path.Combine(retailRoot,"toasty-native-edits.json")));
            var savedNative=retailEdits.RootElement.GetProperty("edits").EnumerateArray().Single(e=>e.GetProperty("trueIndex").GetInt32()==editedNativeRow);
            int nativeZ=savedNative.GetProperty("rawEdited").GetProperty("z").GetInt32();
            var viewport=Get<object>(window,"_viewport");RunTask((Task)Invoke(window,"NewMainLevelAsync")!);
            Check(ReferenceEquals(viewport,Get<object>(window,"_viewport")),"New retains the same viewport");
            Check(window.FreshSceneForTesting is {Objects.Count:0,Surfaces.Count:2},"neutral authored floor has no source actors");
            var state=window.CaptureEditorShellSessionSnapshotForTesting();
            Check(state.CurrentMobys.Count==0,"main viewport contains no donor actors");
            Directory.CreateDirectory(outputDirectory);Snapshot(window,Path.Combine(outputDirectory,"fresh-main-editor.png"));
            var geometry=Get<GeometryCandidate>(window,"_currentGeometry");
            Set(window,"_selectedTerrain",geometry.Polygons[0]);Set(window,"_selectedTerrainIndex",0);
            Invoke(window,"ShowSelection",ViewportSelectionChangedEventArgs.ForTerrain(0,geometry.Polygons[0]));
            Check(Get<Button>(window,"_terrainTaskPaintFaceButton").IsEnabled,"Paint Face is enabled on an untextured authored face");
            Click(Get<Button>(window,"_terrainTaskPaintFaceButton"));
            Wait(()=>window.OwnedWindows.Any(w=>w.Title=="Choose terrain material"),"shared Paint Face opens native material chooser");
            var materialDialog=window.OwnedWindows.Single(w=>w.Title=="Choose terrain material");
            Find<ComboBox>(materialDialog,"AuthoredMaterialLevel").SelectedItem=Get<Spyro.Editor.Core.Levels.LevelCatalog>(window,"_retailCatalog").Levels.Single(l=>l.SourceWadEntry==10);
            Pump();Find<ComboBox>(materialDialog,"AuthoredMaterialChoice").SelectedIndex=66;Pump();
            Wait(()=>Find<Button>(materialDialog,"AuthoredMaterialApply").IsEnabled,"real static material preview",180);
            Check(Find<Image>(materialDialog,"AuthoredMaterialPreview").Source!=null,"Paint Face previews actual native material pixels");
            Snapshot(materialDialog,Path.Combine(outputDirectory,"fresh-main-material-chooser.png"));
            Click(Find<Button>(materialDialog,"AuthoredMaterialApply"));
            // Exercise the same point-height command used by the main editor's ordinary terrain tools.
            var face = geometry.Polygons[1];
            Set(window,"_selectedTerrain",face);Set(window,"_selectedTerrainIndex",1);
            Set(window,"_selectedTerrainPointIndex",1);
            Invoke(window,"NudgeSelectedTerrainPoint",16f);
            Check(face.ZValues[1]==560,"same main-editor point-height tool edits the authored face");
            Invoke(window,"UndoLastTerrainBrushStroke");
            Check(face.ZValues[1]==544,"same main-editor Undo Stroke restores authored point");
            Invoke(window,"RedoLastTerrainBrushStroke");
            Check(face.ZValues[1]==560,"same main-editor Redo Stroke restores authored edit");
            // Invoke the actual Add Terrain dialog twice, then actual Remove and Undo controls.
            Set(window,"_selectedTerrain",geometry.Polygons[0]);Set(window,"_selectedTerrainIndex",0);
            Invoke(window,"ShowSelection",ViewportSelectionChangedEventArgs.ForTerrain(0,geometry.Polygons[0]));
            for(int add=0;add<2;add++)
            {
                Click(Get<Button>(window,"_terrainTaskAddCopyButton"));
                Wait(()=>window.OwnedWindows.Any(w=>w.Title=="Add Terrain Copy"),"shared Add Terrain dialog");
                var addDialog=window.OwnedWindows.Single(w=>w.Title=="Add Terrain Copy");
                Click(addDialog.GetLogicalDescendants().OfType<Button>().Single(b=>b.Content?.ToString()=="Apply"));
                Wait(()=>geometry.Polygons.Count==3+add,"independent added terrain face");
            }
            Check(window.FreshSceneForTesting!.Surfaces.Count==4,"repeated normal Add Terrain creates independent faces");
            Click(Get<Button>(window,"_terrainTaskRemoveFaceButton"));Pump();
            Check(window.FreshSceneForTesting!.Surfaces.Count==3,"normal Remove Terrain removes an authored face");
            RunTask((Task)Invoke(window,"UndoSelectedTerrainAsync")!);
            Check(window.FreshSceneForTesting!.Surfaces.Count==4,"normal Undo Terrain restores removed face");
            Click(Get<Button>(window,"_terrainTaskRemoveFaceButton"));Pump();
            var gate=new TaskCompletionSource();var entered=new TaskCompletionSource();
            window.FreshSaveWrittenBarrierForTesting=()=>{entered.SetResult();return gate.Task;};
            var saving=(Task<bool>)Invoke(window,"SaveFreshSceneAsync")!;
            RunTask(entered.Task);
            Set(window,"_selectedTerrain",face);Set(window,"_selectedTerrainIndex",1);Set(window,"_selectedTerrainPointIndex",1);
            Invoke(window,"NudgeSelectedTerrainPoint",1f);
            gate.SetResult();RunTask(saving);window.FreshSaveWrittenBarrierForTesting=null;
            Check((bool)Invoke(window,"HasUnsavedTerrainEdits")!,"edits arriving while fresh save completes remain dirty");
            Invoke(window,"NudgeSelectedTerrainPoint",-1f);
            Find<TextBox>(window,"FreshMainLevelName").Text="AUTHORED TEST"; Pump();
            Check((bool)Invoke(window,"HasUnsavedTerrainEdits")!,"authored settings participate in ordinary dirty checks");
            RunTask((Task)Invoke(window,"SaveFreshSceneAsync")!);
            string root=state.Workspace.RootPath;var saved=SceneDocument.Read(Path.Combine(root,SceneDocument.FileName));
            Check(saved.Surfaces[1].Vertices[1].Z==560&&saved.Surfaces[0].Material==new AuthoredMaterial(10,66),"geometry and source material save");
            RunTask((Task)Invoke(window,"OpenMainFreshLevelAsync")!);
            Check(window.FreshSceneForTesting!.Surfaces[1].Vertices[1].Z==560,"save/reopen retains sculpted vertex in main editor");
            Console.WriteLine("fresh main: compiling native floor/material/spawn");
            var compilationTask=(Task<NativeFreshMainCompilation>)Invoke(window,"CaptureFreshNativeAsync")!;RunTask(compilationTask);
            var native=compilationTask.Result;Check(new UniversalLevelData(native.CopyLevelData()).Scene().RowCount==0,"compiled neutral scene has zero donor actors");
            string original=(string)Invoke(window,"ResolveUniversalSourceDisc")!;
            var zeroSpawn=NativeFreshMainCompilation.CaptureAsync(original,saved with{Spawn=new(0,0,0),Objects=[],Ledger=UniversalPlacementLedger.Empty},Path.Combine(root,"_local","authored-native","spawn-boundary.bin"));
            RunTask(zeroSpawn);var boundary=new UniversalLevelData(zeroSpawn.Result.CopyLevelData());
            int sceneAt=boundary.Subfiles()[3].Offset;
            Check(boundary.Bytes.AsSpan(sceneAt,12).SequenceEqual(new byte[12])&&boundary.Scene().RowCount==0,"source-bound native spawn can reach its zero coordinate boundary without a bootstrap floor constraint");
            Console.WriteLine("FRESH_SPAWN_NATIVE_REBIND_OK");
            Console.WriteLine("fresh main: compiled neutral source-bound body");
            RunTask((Task)Invoke(window,"RefreshFreshMaterialPreviewAsync")!);
            Snapshot(window,Path.Combine(outputDirectory,"fresh-main-authored-material.png"));
            RunTask((Task)Invoke(window,"CreateObjectTestBinAsync")!);
            string emptyOutput=Path.Combine(root,"output","Spyro Editor - Authored Level.bin");
            Check(File.Exists(emptyOutput)&&new UniversalLevelData(UniversalDiscImage.Read(emptyOutput).EntryBytes(80)).Scene().RowCount==0,"ordinary Create BIN handles a neutral scene without imported actors: "+Get<TextBlock>(window,"_statusText").Text);
            Click(Find<Button>(window,"UniversalMobyLibraryButton"));
            Wait(()=>window.OwnedWindows.Any(w=>w.Title=="Add object — AUTHORED TEST"),"main global browser opens");
            var browser=window.OwnedWindows.Single(w=>w.Title=="Add object — AUTHORED TEST");
            ListBox list=Find<ListBox>(browser,"UniversalMobyList");
            Wait(()=>list.ItemsSource?.Cast<UniversalMobyCatalogEntry>().Any(e=>e.ClassId==0x00A)==true,"global source catalog populated");
            Find<TextBox>(browser,"UniversalMobySearch").Text="0x00A";Pump();
            list.SelectedItem=list.ItemsSource!.Cast<UniversalMobyCatalogEntry>().Single(e=>e.ClassId==0x00A);Pump();
            Console.WriteLine("fresh admission: " + Find<TextBlock>(browser,"UniversalMobyFitText").Text);
            Console.WriteLine("fresh browser: " + Find<TextBlock>(browser,"UniversalMobySummary").Text);
            Snapshot(browser,Path.Combine(outputDirectory,"fresh-main-browser.png"));
            Wait(()=>Find<Button>(browser,"UniversalMobyAddHere").IsEnabled,"exact neutral destination admits sheep",20);
            Click(Find<Button>(browser,"UniversalMobyAddHere"));Wait(()=>window.CaptureEditorShellSessionSnapshotForTesting().CurrentMobys.Count==1,"standard placement in neutral scene");
            var sheep=window.CaptureEditorShellSessionSnapshotForTesting().CurrentMobys.Single();
            Click(Get<Button>(window,"_objectRemoveButton"));
            Check(sheep.IsRemoved&&window.FreshSceneForTesting!.Objects.All(o=>o.Value.IsRemoved),"normal Remove Object retires the authored placement");
            RunTask((Task)Invoke(window,"SaveCurrentEditsAsync",new object?[]{null})!);
            string retiredId=sheep.UniversalPlacementId;
            Click(Get<Button>(window,"_objectUndoRemoveButton"));
            Check(!sheep.IsRemoved&&sheep.UniversalPlacementId==retiredId,"normal Restore Object keeps the authored lifetime identity");
            int authoredYaw=(sheep.YawByte+17)&255; sheep.YawByte=authoredYaw;
            string id=sheep.UniversalPlacementId;Invoke(window,"NudgeSelectedMoby",16f,0f,0f);
            RunTask((Task)Invoke(window,"SaveCurrentEditsAsync",new object?[]{null})!);
            RunTask((Task)Invoke(window,"OpenMainFreshLevelAsync")!);
            Check(window.CaptureEditorShellSessionSnapshotForTesting().CurrentMobys.Single().UniversalPlacementId==id,"normal Save/Open preserves source placement identity");
            Console.WriteLine("fresh main: normal Create BIN with saved flight " + (flightOn?"ON":"OFF"));
            RunTask((Task)Invoke(window,"CreateObjectTestBinAsync")!);
            string output=Path.Combine(root,"output","Spyro Editor - Authored Level.bin");
            Check(File.Exists(output),"ordinary Create BIN emits authored native image: "+Get<TextBlock>(window,"_statusText").Text);
            var read=UniversalDiscImage.Read(output);var level=new UniversalLevelData(read.EntryBytes(80));
            Check(BinaryPrimitives.ReadUInt32LittleEndian(read.Exe.AsSpan(0x2F80))==0x24020041&&
                BinaryPrimitives.ReadUInt32LittleEndian(read.Exe.AsSpan(0x1DCF0))==0x24020041,"ordinary New Game enters authored65 through both native level stores");
            var originalDisc=UniversalDiscImage.Read(original);
            Check(read.Exe.AsSpan(0x49D94,0x74).SequenceEqual(originalDisc.Exe.AsSpan(0x49D94,0x74))&&
                read.Exe.AsSpan(0x23308,0x150).SequenceEqual(originalDisc.Exe.AsSpan(0x23308,0x150)),"native saved-level loading and card branch are unchanged");
            Check(read.Exe.AsSpan(0x3A30,432).SequenceEqual(originalDisc.Exe.AsSpan(0x3A30,432))&&
                read.Exe.AsSpan(0x347E4,0x3B7C).SequenceEqual(originalDisc.Exe.AsSpan(0x347E4,0x3B7C)),"release output leaves flight loader and movement owners unchanged");
            byte[] conflict=read.Exe.ToArray();conflict[0x2F84]^=1;bool entryRefused=false;
            try{NativeFreshNewGameEntry.Apply(originalDisc.Exe,conflict);}catch(InvalidDataException){entryRefused=true;}
            Check(entryRefused,"New Game refuses an altered neighboring instruction owner");
            var retailLevel=new UniversalLevelData(read.EntryBytes(toasty.SourceWadEntry));
            Check(BinaryPrimitives.ReadInt32LittleEndian(retailLevel.Row(editedNativeRow).AsSpan(20))==nativeZ,"aggregate preserves saved native retail transform");
            Check(retailLevel.Scene().RowCount==toasty.SourceRecordCount-1&&retailLevel.Registrations().Any(r=>r.ClassId==0x00A),"aggregate preserves saved retail removals and universal sheep import");
            Check(NativeMobySaveMap.Windows().All(w=>read.Exe.AsSpan(checked((int)(w.Address-UniversalExeBindings.ExeBase)),w.Code.Length).SequenceEqual(w.Code)),"authored aggregate preserves canonical retail save-map owners");
            Check(level.Scene().RowCount==1&&level.Registrations().Any(r=>r.ClassId==0x00A),"native readback includes imported sheep and no donor actors");
            Check(level.Row(0)[0x46]==authoredYaw,"authored native import retains its main inspector yaw edit");
            Check(File.ReadAllLines(Path.ChangeExtension(output,".cue")).Length==3,"authored output CUE has real line breaks");
            int titleOffset=checked((int)(BinaryPrimitives.ReadUInt32LittleEndian(read.Exe.AsSpan(0x6007C))-BinaryPrimitives.ReadUInt32LittleEndian(read.Exe.AsSpan(0x18)))+0x800);
            Check(read.Exe.AsSpan(titleOffset,14).SequenceEqual("AUTHORED TEST\0"u8),"native ID65 indexed name matches authored document");
            int retailTitleOffset=checked((int)(BinaryPrimitives.ReadUInt32LittleEndian(read.Exe.AsSpan(0x60000))-BinaryPrimitives.ReadUInt32LittleEndian(read.Exe.AsSpan(0x18)))+0x800);
            Check(read.Exe.AsSpan(retailTitleOffset,10).SequenceEqual("TWIN TEST\0"u8),"native shared title allocation preserves saved retail rename with authored name");
            Snapshot(window,Path.Combine(outputDirectory,"fresh-main-saved.png"));
            RunTask((Task)Invoke(window,"SelectLevelAsync",toasty,null)!);
            Check(window.FreshSceneForTesting==null,"actual retail selection is active for aggregate export");
            var preservedPreference=Spyro.Editor.Core.Persistence.NativeFreeFlightSettingsStore.SaveGlobal(
                Spyro.Editor.Core.Persistence.NativeFreeFlightSettingsStore.Capture(root),true);
            RunTask((Task)Invoke(window,"CreateObjectTestBinAsync")!);
            var fromRetail=UniversalDiscImage.Read(output);
            Check(fromRetail.Exe.AsSpan(0x3A30,432).SequenceEqual(originalDisc.Exe.AsSpan(0x3A30,432))&&
                Spyro.Editor.Core.Persistence.NativeFreeFlightSettingsStore.Capture(root).ContentSha256==preservedPreference.ContentSha256,
                "release export ignores saved research flight preferences without rewriting them");
            Check(new UniversalLevelData(fromRetail.EntryBytes(80)).Scene().RowCount==1&&
                BinaryPrimitives.ReadInt32LittleEndian(new UniversalLevelData(fromRetail.EntryBytes(toasty.SourceWadEntry)).Row(editedNativeRow).AsSpan(20))==nativeZ,
                "Create BIN from a retail selection preserves saved authored and retail scenes together");
            // An injected refusal immediately before publication must preserve the previous BIN and launcher.
            string beforeHash;
            using(var previous=File.OpenRead(output))beforeHash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(previous));
            byte[] beforeCue=File.ReadAllBytes(Path.ChangeExtension(output,".cue"));
            bool refused=false;
            var sourcesTask=(Task)Invoke(window,"EnsureUniversalCatalogAsync")!;RunTask(sourcesTask);
            var sourceTuple=sourcesTask.GetType().GetProperty("Result")!.GetValue(sourcesTask)!;
            var sources=(UniversalSourceLibrary)sourceTuple.GetType().GetField("Item2")!.GetValue(sourceTuple)!;
            try
            {
                RunTask(native.ExportAsync(sources,[],null,output,root,Get<Spyro.Editor.Core.Levels.LevelCatalog>(window,"_retailCatalog"),
                    requireDocumentCurrent:()=>throw new InvalidDataException("injected publication refusal")));
            }
            catch(InvalidDataException){refused=true;}
            using(var unchanged=File.OpenRead(output))Check(refused&&Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(unchanged))==beforeHash&&File.ReadAllBytes(Path.ChangeExtension(output,".cue")).SequenceEqual(beforeCue),"failed authored finalization leaves previous complete BIN/CUE intact");
            Console.WriteLine("FRESH_RETAIL_AGGREGATE_OK: retail transform/import + authored add/remove/material/ramp/sheep/NewGame; refused publication preserves prior BIN/CUE");
            if(Environment.GetEnvironmentVariable("SPYRO_FRESH_MAIN_KEEP_OUTPUT") is {Length:>0} keep)
            {
                Directory.CreateDirectory(keep);File.Copy(output,Path.Combine(keep,"fresh-main.bin"),true);
                File.WriteAllLines(Path.Combine(keep,"fresh-main.cue"),["FILE \"fresh-main.bin\" BINARY","  TRACK 01 MODE2/2352","    INDEX 01 00:00:00"]);
                File.Copy(Path.Combine(root,SceneDocument.FileName),Path.Combine(keep,SceneDocument.FileName),true);
            }
            Console.WriteLine("FRESH_MAIN_EDITOR_UI_OK");
        }
        finally{window.Close();Pump();}
    }
    internal static void RunAuthoredMeshChecks()
    {
        var document=SceneDocument.New("MESH TEST"); var geometry=document.ProjectGeometry();
        geometry.Polygons[0].StageTerrainAddClone(); geometry.Polygons[0].ApplyTerrainVertexXYDeltas([new(128,0),new(128,0),new(128,0)]);
        var copy=document.Capture(geometry,[]); var again=document.Capture(geometry,[]);
        Check(copy.Surfaces.Count==3&&copy.Surfaces[0].Id==document.Surfaces[0].Id,"copy preserves original authored surface");
        Check(copy.Fingerprint()==again.Fingerprint(),"copy identity is stable across save/compile snapshots");
        geometry.Polygons[0].ResetTerrainEdit(); geometry.Polygons[1].StageTerrainRemoval();
        Check(document.Capture(geometry,[]).Surfaces.Count==1,"remove omits authored visual and collision surface");
        var ramp=document with{Surfaces=[document.Surfaces[0] with{Vertices=[new(528,528,544),new(656,528,560),new(528,656,544)]}]};
        var kernel=typeof(NativeFreshMainCompilation).Assembly.GetType("Spyro.Editor.Core.Exporting.NativeAuthoredMesh")!;
        byte[] Compile(SceneDocument d)=>(byte[])kernel.GetMethod("Compile",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[d,Array.Empty<AuthoredMaterial>()])!;
        byte[] bytes=Compile(ramp); int Word(int at)=>checked((int)BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at)));
        Check(Word(4)==1,"one actual sector per authored triangle");
        int sector=Word(8)+4, ox=Word(sector+8)>>16, oy=Word(sector+8)&65535, oz=Word(sector+12)>>16;
        var vertices=Enumerable.Range(0,3).Select(i=>{uint w=BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(sector+60+i*4));return new Vector3f(ox+(w>>21),oy+((w>>10)&2047),oz+(w&1023));}).ToArray();
        Check(vertices.SequenceEqual(ramp.Surfaces[0].Vertices),"independent HP decoder retains ramp XYZ");
        ColorRgba[] near=[new(11,22,33),new(44,55,66),new(77,88,99)],far=[new(91,81,71),new(61,51,41),new(31,21,11)];
        byte[] shaded=Compile(ramp with{Surfaces=[ramp.Surfaces[0] with{NearColors=near,FarColors=far}]});
        ColorRgba DecodeColor(int at){uint rgb=BinaryPrimitives.ReadUInt32LittleEndian(shaded.AsSpan(at));return new((byte)rgb,(byte)(rgb>>8),(byte)(rgb>>16));}
        Check(Enumerable.Range(0,3).Select(i=>DecodeColor(sector+84+i*4)).SequenceEqual(near)&&
            Enumerable.Range(0,3).Select(i=>DecodeColor(sector+72+i*4)).SequenceEqual(far)&&
            Enumerable.Range(0,3).Select(i=>DecodeColor(sector+40+i*4)).SequenceEqual(far),"native HP near/far and LP far consumers retain each authored vertex colour");
        int collision=0;for(int i=0;i<3;i++)collision+=Word(collision);
        int triangles=collision+4+Word(collision+20);
        int Sign(int v)=>(v&256)!=0?v-512:v;
        var xyz=Enumerable.Range(0,3).Select(axis=>{uint w=BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(triangles+axis*4));int a=(int)(w&16383);return new[]{a,a+Sign((int)(w>>14)&511),a+Sign((int)(w>>23)&511)};}).ToArray();
        var collisionVertices=Enumerable.Range(0,3).Select(i=>new Vector3f(xyz[0][i],xyz[1][i],xyz[2][i])).ToArray();
        Check(collisionVertices.ToHashSet().SetEquals(ramp.Surfaces[0].Vertices),"independent collision decoder matches ramp visual triangle");
        var quad=document with{Surfaces=[document.Surfaces[0] with{Vertices=[new(528,528,544),new(656,528,544),new(656,656,544),new(528,656,544)]}]};
        Check(BinaryPrimitives.ReadInt32LittleEndian(Compile(quad).AsSpan(4))==2,"authored quad triangulates into two native sectors");
        var wide=ramp with{Surfaces=[ramp.Surfaces[0] with{Vertices=[new(1,1,1),new(600,1,1),new(1,100,1)]}]};
        bool refused=false;try{_=Compile(wide);}catch(TargetInvocationException e)when(e.InnerException is InvalidDataException){refused=true;}
        Check(refused,"collision range overflow refuses rather than truncates authored vertices");
        Console.WriteLine("AUTHORED_MESH_CHECKS_OK: copy/remove, stable IDs, independent visual/collision ramp decode, near/far colours, quad and overflow");
    }
    private static void Set(object owner,string field,object value)=>owner.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(owner,value);
    private static void RunTask(Task task){Wait(()=>task.IsCompleted,"async operation",600);task.GetAwaiter().GetResult();}
    private static void Wait(Func<bool> condition,string label,int seconds=180)
    {var timer=Stopwatch.StartNew();while(timer.Elapsed.TotalSeconds<seconds){Pump();if(condition())return;Thread.Sleep(20);}throw new InvalidOperationException("Fresh main UI timeout: "+label);}
    private static void Pump(){Dispatcher.UIThread.RunJobs();}
    private static object? Invoke(object owner,string name,params object?[] args)=>owner.GetType().GetMethods(BindingFlags.Instance|BindingFlags.NonPublic).Single(m=>m.Name==name&&m.GetParameters().Length==args.Length).Invoke(owner,args);
    private static T Get<T>(object owner,string field)=>(T)owner.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(owner)!;
    private static T Find<T>(Control owner,string name)where T:Control=>owner.GetLogicalDescendants().OfType<T>().First(c=>c.Name==name);
    private static void Click(Button button){button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump();}
    private static void Snapshot(Window w,string path){using var capture=w.CaptureRenderedFrame();if(capture==null)throw new InvalidOperationException("No UI frame.");capture.Save(path);}
    private static void Check(bool condition,string label){if(!condition)throw new InvalidOperationException("Fresh main UI: "+label);}
}
