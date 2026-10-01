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
            Check(Get<Control>(window,"_retailTerrainDetailsPanel").IsVisible,"retail terrain helpers remain visible in an existing level");
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
            CreateBin(window);
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
            Check(!Get<Control>(window,"_retailTerrainDetailsPanel").IsVisible,"fresh terrain hides retail-only collision/texture capacity helpers");
            var starterGeometry=Get<GeometryCandidate>(window,"_currentGeometry");
            Invoke(window,"ShowSelection",ViewportSelectionChangedEventArgs.ForTerrain(0,starterGeometry.Polygons[0]));
            Click(Get<Button>(window,"_terrainTaskAddCopyButton"));
            Wait(()=>window.OwnedWindows.Any(w=>w.Title=="Add Terrain Copy"),"fresh add before save");
            var undoAddDialog=window.OwnedWindows.Single(w=>w.Title=="Add Terrain Copy");
            Click(undoAddDialog.GetLogicalDescendants().OfType<Button>().Single(b=>b.Content?.ToString()=="Apply"));
            Wait(()=>window.FreshSceneForTesting!.Surfaces.Count==3,"fresh copy is staged");
            Check(Get<Button>(window,"_terrainUndoButton").IsEnabled,"normal Undo Terrain is enabled immediately after a fresh copy");
            Click(Get<Button>(window,"_terrainUndoButton"));
            Wait(()=>window.FreshSceneForTesting!.Surfaces.Count==2,"normal Undo Terrain removes the added fresh face");
            string initialFreshPath=Path.Combine(state.Workspace.RootPath,SceneDocument.FileName);
            Wait(()=>File.Exists(initialFreshPath)&&SceneDocument.Read(initialFreshPath).Surfaces.Count==2,"fresh copy undo persists the restored authored scene");
            RunTask((Task)Invoke(window,"NewMainLevelAsync")!);
            Console.WriteLine("FRESH_ADD_UNDO_AND_RETAIL_HELPER_VISIBILITY_OK");
            Directory.CreateDirectory(outputDirectory);Snapshot(window,Path.Combine(outputDirectory,"fresh-main-editor.png"));
            Click(Find<Button>(window,"UniversalMobyLibraryButton"));
            Wait(()=>window.OwnedWindows.Any(w=>w.Title=="Add object — Untitled Level"),"default untextured browser");
            var untexturedBrowser=window.OwnedWindows.Single(w=>w.Title=="Add object — Untitled Level");
            var untexturedList=Find<ListBox>(untexturedBrowser,"UniversalMobyList");
            Wait(()=>untexturedList.ItemsSource?.Cast<UniversalMobyCatalogEntry>().Any(e=>e.ClassId==0x00A)==true,"default untextured catalog",180);
            Find<TextBox>(untexturedBrowser,"UniversalMobySearch").Text="0x00A";Pump();
            var defaultSheep=untexturedList.ItemsSource!.Cast<UniversalMobyCatalogEntry>().Single(e=>e.ClassId==0x00A);
            untexturedList.SelectedItem=defaultSheep;Pump();
            Check(Find<ComboBox>(untexturedBrowser,"UniversalMobyVariant").SelectedItem?.Equals(defaultSheep.Default)==true,"default native sheep version is retained");
            Wait(()=>Find<Button>(untexturedBrowser,"UniversalMobyAddHere").IsEnabled,"untextured fresh scene admits default sheep",180);
            Click(Find<Button>(untexturedBrowser,"UniversalMobyAddHere"));
            CreateBin(window);
            string untexturedOutput=Path.Combine(window.CaptureEditorShellSessionSnapshotForTesting().Workspace.RootPath,"output","Spyro Editor - Authored Level.bin");
            var untexturedRead=UniversalDiscImage.Read(untexturedOutput);var untexturedData=new UniversalLevelData(untexturedRead.EntryBytes(80));
            var untexturedSubfile=untexturedData.Subfiles()[1];
            Check(untexturedData.Scene().RowCount==1&&untexturedData.Registrations().Any(r=>r.ClassId==0x00A)&&
                BinaryPrimitives.ReadInt32LittleEndian(untexturedData.Bytes.AsSpan(untexturedSubfile.Offset))==8&&
                BinaryPrimitives.ReadInt32LittleEndian(untexturedData.Bytes.AsSpan(untexturedSubfile.Offset+4))==0,
                "normal Create BIN retains zero terrain textures plus the default sheep actor/model");
            if(Environment.GetEnvironmentVariable("SPYRO_FRESH_MAIN_KEEP_OUTPUT") is {Length:>0} untexturedKeep)
            {
                Directory.CreateDirectory(untexturedKeep);File.Copy(untexturedOutput,Path.Combine(untexturedKeep,"untextured-main.bin"),true);
                File.WriteAllLines(Path.Combine(untexturedKeep,"untextured-main.cue"),["FILE \"untextured-main.bin\" BINARY","  TRACK 01 MODE2/2352","    INDEX 01 00:00:00"]);
            }
            Console.WriteLine("FRESH_UNTEXTURED_DEFAULT_UI_OK defaultSheep=1 nativeTextureCount=0 normalCreateBin=1");
            RunTask((Task)Invoke(window,"NewMainLevelAsync")!);
            Check(window.FreshSceneForTesting is {Objects.Count:0,Surfaces.Count:2},"second actual New resets the authored scene for material checks");
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
            CreateBin(window);
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
            CreateBin(window);
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
            Check(Get<Control>(window,"_retailTerrainDetailsPanel").IsVisible,"retail terrain helpers return after leaving the fresh level");
            Check(window.FreshSceneForTesting==null,"actual retail selection is active for aggregate export");
            var preservedPreference=Spyro.Editor.Core.Persistence.NativeFreeFlightSettingsStore.SaveGlobal(
                Spyro.Editor.Core.Persistence.NativeFreeFlightSettingsStore.Capture(root),true);
            CreateBin(window);
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
        byte[] Compile(SceneDocument d)=>(byte[])kernel.GetMethod("Compile",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,
            [d,d.Surfaces.Where(f=>f.Material!=null).Select(f=>f.Material!).Distinct().ToArray()])!;
        int Word(byte[] data,int at)=>checked((int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(at)));
        Vector3f[] Vertices(byte[] data,int i)
        {
            int at=Word(data,8+i*4)+4,ox=Word(data,at+8)>>16,oy=Word(data,at+8)&65535,oz=Word(data,at+12)>>16;
            return Enumerable.Range(0,3).Select(v=>{uint w=BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(at+60+v*4));
                return new Vector3f(ox+(w>>21),oy+((w>>10)&2047),oz+(w&1023));}).ToArray();
        }
        byte[] bytes=Compile(ramp);int nativeCount=Word(bytes,4);
        Check(nativeCount==4&&ramp.Surfaces.Count==1,"one editor triangle retains its identity while four bounded native triangles render it");
        var nativeVertices=Enumerable.Range(0,nativeCount).Select(i=>Vertices(bytes,i)).ToArray();
        Vector3f[] expected=[new(528,528,544),new(656,528,560),new(528,656,544),new(592,528,552),new(528,592,544),new(592,592,552)];
        Check(nativeVertices.SelectMany(v=>v).ToHashSet().SetEquals(expected)&&
            nativeVertices.All(v=>Enumerable.Range(0,3).All(axis=>v.Max(p=>axis==0?p.X:axis==1?p.Y:p.Z)-v.Min(p=>axis==0?p.X:axis==1?p.Y:p.Z)<=64)),
            "independent HP decoder retains exact ramp corners and midpoint lattice within native direct-packet span");
        double Area(Vector3f[] v)=>Math.Abs((v[1].X-v[0].X)*(v[2].Y-v[0].Y)-(v[1].Y-v[0].Y)*(v[2].X-v[0].X))/2;
        Check(nativeVertices.Sum(Area)==8192,"generated triangle areas retain the complete original ramp footprint");
        ColorRgba[] near=[new(11,22,33),new(44,55,66),new(77,88,99)],far=[new(91,81,71),new(61,51,41),new(31,21,11)];
        byte[] shaded=Compile(ramp with{Surfaces=[ramp.Surfaces[0] with{NearColors=near,FarColors=far}]});
        ColorRgba DecodeColor(int at){uint rgb=BinaryPrimitives.ReadUInt32LittleEndian(shaded.AsSpan(at));return new((byte)rgb,(byte)(rgb>>8),(byte)(rgb>>16));}
        ColorRgba ExpectedColor(ColorRgba[] colors,Vector3f point)
        {
            double b=(point.X-528)/128,c=(point.Y-528)/128,a=1-b-c;
            byte Channel(byte x,byte y,byte z)=>(byte)Math.Floor(a*x+b*y+c*z+0.5);
            return new(Channel(colors[0].R,colors[1].R,colors[2].R),Channel(colors[0].G,colors[1].G,colors[2].G),Channel(colors[0].B,colors[1].B,colors[2].B));
        }
        Check(Enumerable.Range(0,nativeCount).All(i=>
        {
            int at=Word(shaded,8+i*4)+4;
            return Enumerable.Range(0,3).All(v=>DecodeColor(at+84+v*4)==ExpectedColor(near,nativeVertices[i][v])&&
                DecodeColor(at+72+v*4)==ExpectedColor(far,nativeVertices[i][v])&&DecodeColor(at+40+v*4)==ExpectedColor(far,nativeVertices[i][v]));
        }),"native HP near/far and LP far consumers retain independent affine colour interpolation at all generated vertices");
        int collision=0;for(int i=0;i<3;i++)collision+=Word(bytes,collision);
        int triangles=collision+4+Word(bytes,collision+20);
        int Sign(int v)=>(v&256)!=0?v-512:v;
        Check(Word(bytes,collision+8)==nativeCount&&Enumerable.Range(0,nativeCount).All(i=>
        {
            var xyz=Enumerable.Range(0,3).Select(axis=>
            {
                uint w=BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(triangles+i*12+axis*4));int origin=(int)(w&16383);
                return axis==2?new[]{origin,origin+(int)((w>>16)&255),origin+(int)(w>>24)}:
                    new[]{origin,origin+Sign((int)(w>>14)&511),origin+Sign((int)(w>>23)&511)};
            }).ToArray();
            var vertices=Enumerable.Range(0,3).Select(v=>new Vector3f(xyz[0][v],xyz[1][v],xyz[2][v])).ToArray();
            return vertices.ToHashSet().SetEquals(nativeVertices[i])&&vertices[0].Z==vertices.Min(v=>v.Z);
        }),"independent native collision decoder matches every ramp triangle with unsigned Z byte deltas and minimum-Z cyclic origin");
        var quad=document with{Surfaces=[document.Surfaces[0] with{Vertices=[new(528,528,544),new(656,528,544),new(656,656,544),new(528,656,544)]}]};
        Check(BinaryPrimitives.ReadInt32LittleEndian(Compile(quad).AsSpan(4))==8&&quad.Surfaces.Count==1,"one editor quad compiles to eight bounded native triangles");
        var textured=ramp with{Surfaces=[ramp.Surfaces[0] with{Material=new AuthoredMaterial(10,66)}]};
        byte[] texturedMesh=Compile(textured);int texturedSector=Word(texturedMesh,8)+4;
        Check(Word(texturedMesh,4)==1&&Word(texturedMesh,texturedSector+108)==0&&Word(bytes,Word(bytes,8)+4+108)==128,
            "textured terrain keeps native near-clipping flag while direct untextured packets use bounded lattice");
        var wide=textured with{Surfaces=[textured.Surfaces[0] with{Vertices=[new(1,1,1),new(600,1,1),new(1,100,1)]}]};
        bool refused=false;try{_=Compile(wide);}catch(TargetInvocationException e)when(e.InnerException is InvalidDataException){refused=true;}
        Check(refused,"textured collision range overflow refuses rather than truncates authored vertices");
        Console.WriteLine("AUTHORED_MESH_CHECKS_OK: copy/remove, stable IDs, exact lattice and ramp area, independent unsigned-Z collision decode, interpolated near/far colours, textured flags, quad and overflow");
    }

    private static void CreateBin(MainWindow window)
    {
        var task=(Task)Invoke(window,"CreateObjectTestBinAsync")!;
        Wait(()=>task.IsCompleted||window.OwnedWindows.Any(w=>w.Name=="ProjectExportScope"),"export scope");
        if(!task.IsCompleted)
        {
            var dialog=window.OwnedWindows.Single(w=>w.Name=="ProjectExportScope");
            var start=Find<ComboBox>(dialog,"ExportStartLevel");
            Check(start.SelectedIndex==(window.FreshSceneForTesting==null?0:1),"export starts in the selected scene by default");
            start.SelectedIndex=1;
            Click(Find<Button>(dialog,"ExportCreate"));
        }
        RunTask(task);
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
