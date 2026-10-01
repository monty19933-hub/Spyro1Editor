using System.Globalization;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Cache;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Persistence;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Universal;
using Spyro.Editor.Core.Text;

namespace Spyro.Editor.App.Views;

public sealed partial class MainWindow
{
    private SceneDocument? _freshSceneDocument;
    private Task<NativeFreshMainCompilation>? _freshNativeCompilation;
    private string? _freshNativeCompilationKey;
    private string? _savedFreshSettingsSignature;
    private readonly HashSet<TerrainPolygon> _freshAddedFaces = [];
    internal Func<Task>? FreshSaveWrittenBarrierForTesting { get; set; }
    private string? FreshSettingsSignature() => _freshSceneDocument?.Fingerprint();
    private StackPanel? _freshSettingsPanel, _freshTerrainPanel;
    private Control? _retailTerrainWorkspace,_retailNameDisclosure,_retailMusicDisclosure;
    private Control? _freshSharedTerrainTools;
    private Spyro.Editor.Core.Levels.LevelDefinition? _freshLevelPickerOption;
    private void BindFreshLevelPickerOption(Spyro.Editor.Core.Levels.LevelDefinition? authored)
    {
        _freshLevelPickerOption=authored;
        _syncingLevelSelection=true;
        try
        {
            var levels=authored==null?_catalog.Levels.ToList():_catalog.Levels.Append(authored).ToList();
            _levelList.ItemsSource=levels;_levelJumpBox.ItemsSource=levels;
        }
        finally{_syncingLevelSelection=false;}
    }
    private string FreshScenePath => Path.Combine(_workspace.RootPath,SceneDocument.FileName);
    internal SceneDocument? FreshSceneForTesting => _freshSceneDocument == null || _currentGeometry == null ? null : CaptureFreshScene();

    private async Task NewMainLevelAsync()
    {
        if(_createBinBusy||_regularEditorPersistenceBusy||_workspaceTransitionBusy)return;
        if((HasUnsavedTerrainEdits()||HasUnsavedMobyEdits())&&!await SaveCurrentEditsAsync())return;
        ApplyFreshScene(SceneDocument.New());
        _statusText.Text="New level created. Edit the floor, add objects and save; Create BIN makes a game that opens your level from New Game.";
    }

    private async Task OpenMainFreshLevelAsync()
    {
        if(!File.Exists(FreshScenePath)){_statusText.Text="No authored level is saved in this project. Choose File > New Level.";return;}
        if ((HasUnsavedTerrainEdits() || HasUnsavedMobyEdits()) && !await SaveCurrentEditsAsync()) return;
        ApplyFreshScene(SceneDocument.Read(FreshScenePath));
        _statusText.Text="Opened the saved authored level in the main editor.";

    }

    private void ApplyFreshScene(SceneDocument document)
    {
        document.Validate();ClearLoadedSceneForWorkspaceCommit();
        _freshSceneDocument=document with { OriginalSurfaces=document.Surfaces,LevelSourceEntry=key=>_retailCatalog.FindByKey(key)?.SourceWadEntry??throw new InvalidDataException("Unknown material source level.") };
        _freshAddedFaces.Clear();
        _freshNativeCompilation=null;_freshNativeCompilationKey=null;
        _currentLevel=SceneDocument.NativeTarget(document.Name);
        BindFreshLevelPickerOption(_currentLevel);SyncLevelPickers(_currentLevel);
        _currentGeometry=document.ProjectGeometry();_currentMobys=document.ProjectObjects();
        _savedFreshSettingsSignature=FreshSettingsSignature();
        _savedTerrainEditSignature=BuildTerrainEditSignature(_currentGeometry);_savedMobyEditSignature=BuildMobyEditSignature(_currentMobys);
        _viewport.SetAuthoredSkyColor(document.SkyColor);
        _viewport.SetTerrainTextureImageFiles(null);
        _viewport.Geometry=_currentGeometry;_viewport.Mobys=_currentMobys;
        _ = RefreshFreshMaterialPreviewAsync();
        _viewport.TerrainPatchSafetyClassifier=ClassifyTerrainPatchSafety;
        _terrainCollisionMatchedFaces=_currentGeometry.Polygons.Count;
        _terrainCollisionReadinessMessage="Authored triangles: native collision is compiled with their current geometry.";
        ConfigureCompleteTerrainEditing();
        _viewport.SetLevelEntryPose(new PortableLevelEntryPose(_currentLevel.Key,document.Name,65,80,0,
            (int)(document.Spawn.X*16),(int)(document.Spawn.Y*16),(int)(document.Spawn.Z*16),document.Heading,16));
        _viewport.SetViewMode(ViewportViewMode.Fly3D);_viewport.RefreshFlyCameraForLoadedLevel();
        _viewport.EmptyMessage="File > New Level creates an authored floor in this viewport.";
        ApplyEditorExperienceForCurrentLevel();RefreshMobyList();RefreshCurrentLevelDetails();
        RefreshContextualViewportAction();RefreshFreshDocumentPanels();RefreshFreeFlightSettingsEditor(_currentLevel);
        if (_modernWorkspaceTabs != null)
            foreach (var tab in _modernWorkspaceTabs.Items.OfType<TabItem>())
                if (tab.Content is ScrollViewer scroll) scroll.Offset = default;
    }

    private SceneDocument CaptureFreshScene()
    {
        if(_freshSceneDocument==null||_currentGeometry==null)throw new InvalidOperationException("No authored scene is open.");
        return _freshSceneDocument.Capture(_currentGeometry,_currentMobys);
    }
    private async Task<bool> SaveFreshSceneAsync()
    {
        try
        {
            var snapshot=CaptureFreshScene();
            string path=FreshScenePath;
            string settingsSignature=(_freshSceneDocument! with { Ledger=snapshot.Ledger }).Fingerprint();
            string terrainSignature=BuildTerrainEditSignature(_currentGeometry),mobySignature=BuildMobyEditSignature(_currentMobys);
            await snapshot.SaveAsync(path);
            if(FreshSaveWrittenBarrierForTesting is { } barrier) await barrier();
            if(_freshSceneDocument?.Id!=snapshot.Id || path!=FreshScenePath) return true;
            _freshSceneDocument=_freshSceneDocument with { Ledger=snapshot.Ledger };
            _savedFreshSettingsSignature=settingsSignature;
            _savedTerrainEditSignature=terrainSignature;_savedMobyEditSignature=mobySignature;
            _statusText.Text=$"Saved {snapshot.Name}: {snapshot.Surfaces.Count} authored faces, {snapshot.Objects.Count(o=>!o.Value.IsRemoved)} objects.";
            RefreshCurrentLevelDetails();return true;
        }
        catch(Exception e)when(e is IOException or InvalidDataException or ArgumentException)
        {_statusText.Text="Authored scene save failed: "+e.Message;return false;}
    }

    private TerrainPolygon AddFreshTerrainCopy(TerrainPolygon source,float dx,float dy,float dz)
    {
        if(_freshSceneDocument==null||_currentGeometry==null)throw new InvalidOperationException("No authored terrain is open.");
        var old=_freshSceneDocument.Surfaces[source.SectorIndex];
        var current=CaptureFreshScene().Surfaces.Single(s=>s.Id==old.Id);
        var copy=current with { Id=Guid.NewGuid().ToString("N"),Vertices=current.Vertices.Select(v=>new Vector3f(MathF.Round(v.X+dx),MathF.Round(v.Y+dy),MathF.Round(v.Z+dz))).ToArray() };
        var extended=_freshSceneDocument with { Surfaces=_freshSceneDocument.Surfaces.Append(copy).ToArray() };
        extended.Validate();
        var projected=extended.ProjectGeometry();var face=projected.Polygons[^1];
        _freshSceneDocument=extended;
        _currentGeometry.Polygons.Add(face);_currentGeometry.Bounds=projected.Bounds;
        _freshAddedFaces.Add(face);ClearTerrainBrushUndoHistory();
        _selectedTerrain=face;_selectedTerrainIndex=_currentGeometry.Polygons.Count-1;
        _viewport.NotifyTerrainPresentationDataChanged();
        ShowSelection(ViewportSelectionChangedEventArgs.ForTerrain(_selectedTerrainIndex,face));
        RefreshCurrentLevelDetails();RefreshTerrainReadinessHint();
        _ = RefreshFreshMaterialPreviewAsync();
        _statusText.Text="Added a terrain face. Move or sculpt it, then Save Level and Create BIN.";
        return face;
    }
    private Task<NativeFreshMainCompilation> CaptureFreshNativeAsync()
    {
        var scene=CaptureFreshScene() with {Objects=[],Ledger=UniversalPlacementLedger.Empty};
        string sourceImage = ResolveUniversalSourceDisc();
        bool flightEnabled = !_releaseMode && NativeFreeFlightSettingsStore.Capture(_workspace.RootPath).GlobalEnabled;
        string key=scene.Fingerprint()+"|"+sourceImage+"|flight="+flightEnabled;
        if(_freshNativeCompilation==null||_freshNativeCompilation.IsFaulted||_freshNativeCompilationKey!=key)
        {
            _freshNativeCompilationKey=key;
            string baseline=Path.Combine(_workspace.RootPath,"_local","authored-native",scene.Id+"-"+scene.Fingerprint()+".bin");
            _freshNativeCompilation=Task.Run(async () =>
            {
                var flight = _releaseMode ? null : await NativeFreeFlightCompositionPlan.CaptureAsync(sourceImage, flightEnabled);
                return await NativeFreshMainCompilation.CaptureAsync(sourceImage,scene,baseline,flight:flight);
            });
        }
        return _freshNativeCompilation;
    }
    private (UniversalComposeResult? Result,string? Error) DryRunFresh(NativeFreshMainCompilation compilation,UniversalSourceLibrary library,
        IReadOnlyList<UniversalMobyPlacement> placements,Func<string,UniversalSpecialAsset?>? special)
    {try{return(compilation.Compose(library,placements,special),null);}catch(Exception e)when(e is IOException or InvalidDataException){return(null,e.Message);}}

    private async Task CreateFreshMainBinAsync()
    {
        bool authoredActive=_freshSceneDocument!=null;
        if(authoredActive?!await SaveFreshSceneAsync():!await SaveCurrentEditsAsync())return;
        var owner=CaptureSourceDiscOperationIdentity();
        string workspacePath=_workspace.RootPath,scenePath=FreshScenePath;
        string original=ResolveUniversalSourceDisc();
        string? imageSelection=_discImagePathBox.Text,skySelection=_skyboxDiscImagePathBox.Text;
        var flightIntent=_releaseMode?null:NativeFreeFlightProjectExportIntent.Capture(workspacePath);
        var saved=SceneDocument.Read(FreshScenePath);
        void RequireOwner()
        {
            if(!IsCurrentSourceDiscOperationIdentity(owner)||imageSelection!=_discImagePathBox.Text||skySelection!=_skyboxDiscImagePathBox.Text||
                authoredActive&&CaptureFreshScene().Fingerprint()!=saved.Fingerprint()||SceneDocument.Read(scenePath).Fingerprint()!=saved.Fingerprint())
                throw new InvalidDataException("The authored project, scene, source selection or saved revision changed during export.");
            flightIntent?.RequireCurrent(workspacePath);
            if(TryGetFreeFlightSettingsExportBlockReason(out string reason))throw new InvalidDataException(reason);
        }
        RequireOwner();
        ReportCreateBinProgress("Compiling level…","Create BIN: compiling authored geometry, collision, materials and spawn…");
        var compilation=authoredActive?await CaptureFreshNativeAsync():await Task.Run(async()=>
        {
            var scene=saved with{Objects=[],Ledger=UniversalPlacementLedger.Empty};
            var flight=_releaseMode?null:await NativeFreeFlightCompositionPlan.CaptureAsync(original,flightIntent?.Enabled==true);
            return await NativeFreshMainCompilation.CaptureAsync(original,scene,
                Path.Combine(workspacePath,"_local","authored-native",scene.Id+"-"+scene.Fingerprint()+".bin"),flight:flight);
        });
        RequireOwner();
        var(_,donors,_)=await EnsureUniversalCatalogAsync();var japanese=ResolveUniversalJapaneseSource();
        RequireOwner();
        var objects=saved.ProjectObjects();
        var placements=PlacementsFor(saved.ReconcileLedger(objects),objects);
        string outputFolder=Path.Combine(workspacePath,"output");Directory.CreateDirectory(outputFolder);
        CombinedTestBinResult? retail=null;
        var targets=FindEditedLevelExportTargets();
        if(targets.Count>0)
        {
            ReportCreateBinProgress("Merging saved levels…","Create BIN: composing the project's saved retail edits before the authored level…");
            retail=await CreateSavedEditsStageAsync(original,DiscImageLocator.FindCueForImage(original),
                Path.Combine(workspacePath,"_local","authored-aggregate",Guid.NewGuid().ToString("N")),targets,true);
            if(retail.SkippedEdits>0)throw new InvalidDataException("The authored aggregate refuses saved retail edits without an export route: "+string.Join("; ",retail.SkippedEditDetails));
            RequireOwner();
        }
        string output=Path.Combine(outputFolder,"Spyro Editor - Authored Level.bin");
        string hash=await Task.Run(()=>compilation.ExportAsync(donors,placements,japanese,output,workspacePath,_retailCatalog,
            requireDocumentCurrent: () => Dispatcher.UIThread.InvokeAsync(RequireOwner).GetAwaiter().GetResult(),
            stagedRetailImage:retail?.WroteImage==true?retail.OutputImagePath:null,retailEggs:retail?.ActiveDragonflyEggs,retailSignLevels:retail?.SignLevelKeys,
            retailNames:retail?.DeferredLevelNames));
        RequireOwner();
        _statusText.Text=$"Created {Path.ChangeExtension(output,".cue")}. Choose New Game to play {saved.Name}"+
            (retail?.WroteImage==true?$"; saved edits from {retail.PatchedLevelNames.Count} existing levels are included":"")+".";
    }

    private Control BuildFreshDocumentSettings()
    {
        _freshSettingsPanel=new StackPanel{Spacing=8,IsVisible=false,Name="FreshMainSettings"};return _freshSettingsPanel;
    }
    private Control BuildFreshTerrainTools()
    {
        _freshTerrainPanel=new StackPanel{Spacing=8,IsVisible=false,Name="FreshMainTerrainTools"};
        if (!_releaseMode) return _freshTerrainPanel;
        StackPanel shared = new(){Spacing=9};
        shared.Children.Add(BuildTerrainTaskControls());
        shared.Children.Add(BuildTerrainBrushControls());
        shared.Children.Add(BuildTerrainPointControls());
        if(!_releaseMode) shared.Children.Add(BuildAdvancedTerrainControls());
        _freshSharedTerrainTools=shared;
        return _freshTerrainPanel;
    }
    private void RefreshFreshDocumentPanels()
    {
        if(_freshSettingsPanel==null||_freshTerrainPanel==null)return;
        _freshSettingsPanel.Children.Clear();_freshTerrainPanel.Children.Clear();
        _freshSettingsPanel.IsVisible=_freshTerrainPanel.IsVisible=_freshSceneDocument!=null;
        if(_retailTerrainWorkspace!=null)_retailTerrainWorkspace.IsVisible=!_releaseMode||_freshSceneDocument==null;
        if(_retailNameDisclosure!=null)_retailNameDisclosure.IsVisible=_freshSceneDocument==null;
        if(_retailMusicDisclosure!=null)_retailMusicDisclosure.IsVisible=_freshSceneDocument==null;
        if(_homeworldTextGroup!=null)_homeworldTextGroup.IsVisible=_freshSceneDocument==null;
        _terrainPlayableOnlyBox.IsVisible=_terrainBrushSafetyText.IsVisible=_freshSceneDocument==null;
        if(_freshSceneDocument is not { } scene)return;
        _freshSettingsPanel.Children.Add(NewSmallNote("Your level opens when you choose New Game in the exported game."));
        TextBox name=new(){Name="FreshMainLevelName",Text=scene.Name,MaxLength=19};
        name.TextChanged+=(_,_)=>
        {
            if(!TextTargetCatalog.IsSafeReplacement(name.Text??"",19))return;
            bool flightDraft=_freeFlightDraft;
            _freshSceneDocument=_freshSceneDocument! with{Name=name.Text!};_currentLevel=SceneDocument.NativeTarget(name.Text!);
            BindFreshLevelPickerOption(_currentLevel);SyncLevelPickers(_currentLevel);RefreshFreeFlightSettingsEditor(_currentLevel);
            _freeFlightEnabled.IsChecked=flightDraft;RefreshCurrentLevelDetails();
        };
        _freshSettingsPanel.Children.Add(new TextBlock{Text="Level name (up to 19 letters, spaces or apostrophes)"});_freshSettingsPanel.Children.Add(name);
        Grid spawnGrid=new(){ColumnDefinitions=new("*,*,*"),ColumnSpacing=6};
        TextBox[] spawn=new[]{scene.Spawn.X,scene.Spawn.Y,scene.Spawn.Z}.Select((v,i)=>new TextBox{Name="FreshMainSpawn"+"XYZ"[i],Text=v.ToString(CultureInfo.InvariantCulture)}).ToArray();
        for(int i=0;i<3;i++){Grid.SetColumn(spawn[i],i);spawnGrid.Children.Add(spawn[i]);}
        _freshSettingsPanel.Children.Add(new TextBlock{Text="Spawn X / Y / Z (editor units)"});_freshSettingsPanel.Children.Add(spawnGrid);
        Button applySpawn=NewButton("Set spawn",()=>
        {
            if(!spawn.All(s=>float.TryParse(s.Text,NumberStyles.Float,CultureInfo.InvariantCulture,out float value)&&float.IsFinite(value)&&value>=0&&value<=16383&&value==MathF.Truncate(value)))
            { _statusText.Text="Spawn needs whole editor units within 0..16383.";return; }
            _freshSceneDocument=_freshSceneDocument! with{Spawn=new(float.Parse(spawn[0].Text!,CultureInfo.InvariantCulture),float.Parse(spawn[1].Text!,CultureInfo.InvariantCulture),float.Parse(spawn[2].Text!,CultureInfo.InvariantCulture))};
            var point=_freshSceneDocument.Spawn;
            _viewport.SetLevelEntryPose(new PortableLevelEntryPose(_currentLevel!.Key,_freshSceneDocument.Name,65,80,0,(int)point.X*16,(int)point.Y*16,(int)point.Z*16,_freshSceneDocument.Heading,16));
            _statusText.Text="Spawn changed; Save Level records it with the geometry.";
        });
        applySpawn.Name="FreshMainSetSpawn";_freshSettingsPanel.Children.Add(applySpawn);
        TextBox sky=new(){Name="FreshMainSkyColor",Text=$"{scene.SkyColor.R}, {scene.SkyColor.G}, {scene.SkyColor.B}"};
        _freshSettingsPanel.Children.Add(new TextBlock{Text="Sky color (R, G, B)"});_freshSettingsPanel.Children.Add(sky);
        Button applySky=NewButton("Set sky color",()=>
        {
            string[] values=(sky.Text??"").Split(',');
            if(values.Length==3&&values.All(v=>byte.TryParse(v.Trim(),out _)))
            {
                _freshSceneDocument=_freshSceneDocument! with{SkyColor=new(byte.Parse(values[0]),byte.Parse(values[1]),byte.Parse(values[2]))};
                _viewport.SetAuthoredSkyColor(_freshSceneDocument.SkyColor);
                _statusText.Text="Authored sky color changed; Save Level records it with the scene.";
            }
            else _statusText.Text="Use three color values from 0 through 255, separated by commas.";
        });applySky.Name="FreshMainSetSkyColor";_freshSettingsPanel.Children.Add(applySky);
        ComboBox source=new(){Name="FreshMainMaterialSource",ItemsSource=_retailCatalog.Levels.Where(l=>l.SourceWadEntry is >=10 and <=78).ToList(),SelectedItem=_retailCatalog.FindByKey("stonehill"),MinHeight=32};
        TextBox texture=new(){Name="FreshMainMaterialId",Text="0"};
        _freshTerrainPanel.Children.Add(NewSmallNote("Select a face to move it, edit its points or choose a material. Add Terrain extends your level; Remove Terrain cuts out the selected face."));
        if(!_releaseMode){_freshTerrainPanel.Children.Add(source);_freshTerrainPanel.Children.Add(texture);}
        Button apply=NewAsyncButton("Apply source material",async()=>
        {
            if(_selectedTerrain==null||source.SelectedItem is not Spyro.Editor.Core.Levels.LevelDefinition level||!int.TryParse(texture.Text,out int id))return;
            string sourceImage = ResolveUniversalSourceDisc();
            try
            {
                _=await Task.Run(()=>NativeStaticTerrainTextureSource.Capture(sourceImage,level.SourceWadEntry,id));
                ApplyFreshMaterial(_selectedTerrain,new(level.SourceWadEntry,id));
                _statusText.Text="Authored face material selected from "+level.DisplayName+"; Save Level retains its source identity.";
            }catch(Exception e){_statusText.Text="Material was not applied: "+e.Message;}
        });apply.Name="FreshMainApplyMaterial";if(!_releaseMode)_freshTerrainPanel.Children.Add(apply);
        if(_freshSharedTerrainTools!=null)_freshTerrainPanel.Children.Add(_freshSharedTerrainTools);
    }
    private void ApplyFreshMaterial(TerrainPolygon face,AuthoredMaterial material)
    {
        if(_freshSceneDocument==null||face.SectorIndex<0||face.SectorIndex>=_freshSceneDocument.Surfaces.Count)return;
        var surfaces=_freshSceneDocument.Surfaces.ToArray();surfaces[face.SectorIndex]=surfaces[face.SectorIndex] with{Material=material};
        _freshSceneDocument=_freshSceneDocument with{Surfaces=surfaces};face.ApplyTextureOverride(material.TextureId);_viewport.InvalidateVisual();RefreshCurrentLevelDetails();_ = RefreshFreshMaterialPreviewAsync();
    }
    private async Task RefreshFreshMaterialPreviewAsync()
    {
        if(_freshSceneDocument==null||_currentGeometry==null)return;
        var document=_freshSceneDocument;var geometry=_currentGeometry;
        string sourceImage=ResolveUniversalSourceDisc();
        var materials=document.Surfaces.Where(s=>s.Material!=null).Select(s=>s.Material!).Distinct().ToArray();
        Dictionary<AuthoredMaterial,string> paths=[];
        try
        {
            foreach(var material in materials)
            {
                var sourceLevel=_retailCatalog.Levels.Single(l=>l.SourceWadEntry==material.SourceWadEntry);
                string path=Path.Combine(_workspace.RootPath,"_local","authored-materials",$"wad-{material.SourceWadEntry}-t-{material.TextureId}.png");
                if(!File.Exists(path))
                {
                    _ = await Task.Run(()=>NativeStaticTerrainTextureSource.Capture(sourceImage,material.SourceWadEntry,material.TextureId));
                    var result=await Task.Run(()=>TerrainPatchExporter.TryExportTerrainTextureImageTierAsync(sourceImage,sourceLevel,material.TextureId,path,"hqData"));
                    if(result==null)throw new InvalidDataException("The source material image could not be decoded.");
                }
                paths[material]=path;
            }
            if(!ReferenceEquals(_currentGeometry,geometry)||!ReferenceEquals(_freshSceneDocument?.Surfaces,document.Surfaces))return;
            Dictionary<string,string> faces=[];
            foreach(var face in geometry.Polygons)
                if(face.SectorIndex>=0&&face.SectorIndex<document.Surfaces.Count&&document.Surfaces[face.SectorIndex].Material is { } material&&paths.TryGetValue(material,out var path))
                    faces[face.RuntimeKey]=path;
            _viewport.SetAuthoredTerrainTextureFiles(faces);
        }
        catch(Exception e)when(e is IOException or InvalidDataException or InvalidOperationException)
        {if(ReferenceEquals(_currentGeometry,geometry))_statusText.Text="Authored material preview failed: "+e.Message;}
    }
}
