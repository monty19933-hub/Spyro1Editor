using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
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
    private Task<INativeAuthoredCompilation>? _freshNativeCompilation;
    private string? _freshNativeCompilationKey;
    private string? _savedFreshSettingsSignature;
    private readonly HashSet<TerrainPolygon> _freshAddedFaces = [];
    internal Func<Task>? FreshSaveWrittenBarrierForTesting { get; set; }
    private string? FreshSettingsSignature() => _freshSceneDocument?.Fingerprint();
    private StackPanel? _freshSettingsPanel, _freshTerrainPanel;
    private Control? _retailTerrainWorkspace,_retailNameDisclosure,_retailMusicDisclosure;
    private Spyro.Editor.Core.Levels.LevelDefinition? _freshLevelPickerOption;
    private void BindFreshLevelPickerOption(Spyro.Editor.Core.Levels.LevelDefinition? authored)
    {
        _freshLevelPickerOption=authored;
        _syncingLevelSelection=true;
        try
        {
            var levels=_catalog.Levels.Select(l=>authored!=null&&l.Key==authored.Key?authored:l).ToList();
            if(authored!=null&&!levels.Any(l=>l.Key==authored.Key))levels.Add(authored);
            // The new world's levels (File ▾ › New World…) after the game's own, under their current names.
            foreach(var world in WorldLevelPickerOptions())
                if(!levels.Any(l=>l.Key==world.Key))levels.Add(authored!=null&&authored.Key==world.Key?authored:world);
            _levelList.ItemsSource=levels;_levelJumpBox.ItemsSource=levels;
        }
        finally{_syncingLevelSelection=false;}
    }
    private string FreshScenePath => SceneDocumentStore.PathForTarget(_workspace.RootPath,_freshSceneDocument?.TargetKey??"authored65");
    internal SceneDocument? FreshSceneForTesting => _freshSceneDocument == null || _currentGeometry == null ? null : CaptureFreshScene();

    private async Task NewMainLevelAsync()
    {
        if(_createBinBusy||_regularEditorPersistenceBusy||_workspaceTransitionBusy)return;
        if((HasUnsavedTerrainEdits()||HasUnsavedMobyEdits())&&!await SaveCurrentEditsAsync())return;
        string root=_workspace.RootPath,existing=SceneDocumentStore.PathForTarget(root,"authored65");
        string? keptCopy=null;
        if(File.Exists(existing))
        {
            // A project holds one created level, so a second New Level would overwrite the first on the next save.
            string name="your created level";
            try{name="\""+SceneDocument.Read(existing).Name+"\"";}catch(Exception e)when(e is IOException or InvalidDataException or System.Text.Json.JsonException){}
            if(!await ShowConfirmationAsync("NewLevelReplaceDialog","New Level",$"Replace {name} with a new level?",
                "This project already has a level you created, and a project holds one created level. Starting a new one replaces it the next time you save.\n"+
                "A copy of the current one is kept first in the project's \"backups\" folder. To keep working on it instead, choose Cancel and use File > Open Created Level.",
                "Start New Level"))return;
            if(_workspace.RootPath!=root)return;
            try
            {
                string folder=Path.Combine(root,"backups");Directory.CreateDirectory(folder);
                keptCopy=Path.Combine(folder,$"created-level-{DateTime.Now:yyyyMMdd-HHmmss}.scene.json");
                File.Copy(existing,keptCopy,overwrite:false);
            }
            catch(Exception e)when(e is IOException or UnauthorizedAccessException)
            { _statusText.Text="Could not keep a copy of the current created level, so it was left unchanged: "+e.Message;return; }
        }
        ApplyFreshScene(StarterLevel.Create("Untitled Level",_retailCatalog));
        // The Ground card is the first thing a new level needs.
        ApplyEditorExperienceForCurrentLevel(_modernTerrainWorkspaceTab);
        _statusText.Text="New level created: a meadow with a path, a ledge and a pond. Paint the ground in the Terrain tab (Ground), add objects and save; Create BIN makes a game that opens your level from New Game."+
            (keptCopy==null?"":" The previous one was copied to backups/"+Path.GetFileName(keptCopy)+".");
    }

    /// <summary>A plain yes/no question for an action that replaces or removes the user's work.</summary>
    private async Task<bool> ShowConfirmationAsync(string dialogName,string title,string heading,string explanation,string confirmLabel)
    {
        var content=new StackPanel{Margin=new Thickness(22),Spacing=12};
        content.Children.Add(new TextBlock{Text=heading,FontSize=20,FontWeight=FontWeight.SemiBold,TextWrapping=TextWrapping.Wrap});
        content.Children.Add(new TextBlock{Name="ConfirmationExplanation",TextWrapping=TextWrapping.Wrap,Text=explanation,LineHeight=20});
        var dialog=new Window{Name=dialogName,Title=title,Width=520,SizeToContent=SizeToContent.Height,CanResize=false,
            WindowStartupLocation=WindowStartupLocation.CenterOwner,Content=content};
        var actions=new StackPanel{Orientation=Orientation.Horizontal,Spacing=10,HorizontalAlignment=HorizontalAlignment.Right};
        Button cancel=NewButton("Cancel"),confirm=NewButton(confirmLabel);
        cancel.Name="ConfirmationCancel";confirm.Name="ConfirmationConfirm";cancel.IsCancel=true;
        cancel.Click+=(_,_)=>dialog.Close(false);confirm.Click+=(_,_)=>dialog.Close(true);
        StyleModernPrimaryButton(confirm,ModernBlue,Math.Max(132,confirmLabel.Length*9+28));
        actions.Children.Add(cancel);actions.Children.Add(confirm);content.Children.Add(actions);
        return await dialog.ShowDialog<bool>(this);
    }

    private async Task OpenMainFreshLevelAsync()
    {
        string path=SceneDocumentStore.PathForTarget(_workspace.RootPath,"authored65");
        if(!File.Exists(path)){_statusText.Text="This project has no created level yet. Choose File > New Level… to start one.";return;}
        if ((HasUnsavedTerrainEdits() || HasUnsavedMobyEdits()) && !await SaveCurrentEditsAsync()) return;
        ApplyFreshScene(SceneDocument.Read(path));
        _statusText.Text="Opened your created level.";

    }

    private void ApplyFreshScene(SceneDocument document)
    {
        document.Validate();ClearLoadedSceneForWorkspaceCommit();
        _freshSceneDocument=document with { OriginalSurfaces=document.Surfaces,LevelSourceEntry=key=>_retailCatalog.FindByKey(key)?.SourceWadEntry??throw new InvalidDataException("Unknown material source level.") };
        _freshAddedFaces.Clear();
        _freshNativeCompilation=null;_freshNativeCompilationKey=null;
        _currentLevel=document.ResolveTarget(_retailCatalog);
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
        _viewport.SetLevelEntryPose(new PortableLevelEntryPose(_currentLevel.Key,document.Name,_currentLevel.LevelId,_currentLevel.SourceWadEntry,0,
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
            string targetKey=snapshot.TargetKey;
            if(StartBlankRecoveryStore.Read(_workspace.RootPath,targetKey)!=null)
                snapshot=snapshot with{Ledger=StartBlankRecoveryStore.SaveLifetime(_workspace.RootPath,targetKey,snapshot.Ledger,snapshot.TargetBinding?.NativePrefixRows??0)};
            string path=FreshScenePath;
            string settingsSignature=(_freshSceneDocument! with { Ledger=snapshot.Ledger }).Fingerprint();
            string terrainSignature=BuildTerrainEditSignature(_currentGeometry),mobySignature=BuildMobyEditSignature(_currentMobys);
            await snapshot.SaveAsync(path);
            if(FreshSaveWrittenBarrierForTesting is { } barrier) await barrier();
            if(_freshSceneDocument?.Id!=snapshot.Id || path!=FreshScenePath) return true;
            _freshSceneDocument=_freshSceneDocument with { Ledger=snapshot.Ledger };
            _savedFreshSettingsSignature=settingsSignature;
            _savedTerrainEditSignature=terrainSignature;_savedMobyEditSignature=mobySignature;
            _statusText.Text=_releaseMode
                ?$"Saved {snapshot.Name}: {Count(snapshot.Surfaces.Count,"terrain tile","terrain tiles")}, {Count(snapshot.Objects.Count(o=>!o.Value.IsRemoved),"object","objects")}."
                :$"Saved {snapshot.Name}: {snapshot.Surfaces.Count} authored faces, {snapshot.Objects.Count(o=>!o.Value.IsRemoved)} objects.";
            RefreshCurrentLevelDetails();return true;
        }
        catch(Exception e)when(e is IOException or InvalidDataException or ArgumentException or System.Text.Json.JsonException)
        {_statusText.Text=(_releaseMode?"Could not save your level, so the last saved version is unchanged: ":"Authored scene save failed: ")+e.Message;return false;}
    }

    /// <summary>Adds a copy of a face moved by (dx, dy, dz) and selects it; null (with a message) when the copy would
    /// leave the game's map (coordinates 0-16383).</summary>
    private TerrainPolygon? AddFreshTerrainCopy(TerrainPolygon source,float dx,float dy,float dz)
    {
        if(_freshSceneDocument==null||_currentGeometry==null)throw new InvalidOperationException("No authored terrain is open.");
        var old=_freshSceneDocument.Surfaces[source.SectorIndex];
        var current=CaptureFreshScene().Surfaces.Single(s=>s.Id==old.Id);
        var copy=current with { Id=Guid.NewGuid().ToString("N"),Vertices=current.Vertices.Select(v=>new Vector3f(MathF.Round(v.X+dx),MathF.Round(v.Y+dy),MathF.Round(v.Z+dz))).ToArray() };
        if(copy.Vertices.Any(v=>v.X<0||v.Y<0||v.Z<0||v.X>16383||v.Y>16383||v.Z>16383))
        {
            string side=copy.Vertices.Min(v=>v.X)<0?"west":copy.Vertices.Min(v=>v.Y)<0?"south":copy.Vertices.Max(v=>v.X)>16383?"east":copy.Vertices.Max(v=>v.Y)>16383?"north":"bottom or top";
            _statusText.Text=$"There is no room for that tile: it would be past the {side} edge of the game's map. Build in another direction.";
            return null;
        }
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
    private void AddFreshTerrainFromEmpty()
    {
        if(_freshSceneDocument==null||_currentGeometry==null)return;
        float x=Math.Clamp(MathF.Round(_freshSceneDocument.Spawn.X-32),0,16255),y=Math.Clamp(MathF.Round(_freshSceneDocument.Spawn.Y-32),0,16255);
        float z=MathF.Max(0,MathF.Round(_freshSceneDocument.Spawn.Z-32));
        var face=new AuthoredSurface(Guid.NewGuid().ToString("N"),[new(x,y,z),new(x+128,y,z),new(x,y+128,z)],new(128,96,64),null);
        var extended=_freshSceneDocument with{Surfaces=_freshSceneDocument.Surfaces.Append(face).ToArray()};extended.Validate();
        var projection=extended.ProjectGeometry();var added=projection.Polygons[^1];_freshSceneDocument=extended;
        _currentGeometry.Polygons.Add(added);_currentGeometry.Bounds=projection.Bounds;_freshAddedFaces.Add(added);
        _selectedTerrain=added;_selectedTerrainIndex=_currentGeometry.Polygons.Count-1;
        _viewport.NotifyTerrainPresentationDataChanged();ShowSelection(ViewportSelectionChangedEventArgs.ForTerrain(_selectedTerrainIndex,added));
        RefreshCurrentLevelDetails();RefreshTerrainReadinessHint();_statusText.Text="Added a terrain face beneath the player start. Move or edit it, then Save Terrain.";
    }
    private Task<INativeAuthoredCompilation> CaptureFreshNativeAsync()
    {
        var scene=CaptureFreshScene() with {Objects=[],Ledger=UniversalPlacementLedger.Empty};
        string sourceImage = ResolveUniversalSourceDisc();
        // No flight plan at all unless Fly Anywhere is on.
        var flightSettings = NativeFreeFlightSettingsStore.Capture(_workspace.RootPath);
        bool flightEnabled = flightSettings.GlobalEnabled, flightChosen = !_releaseMode || flightEnabled;
        IReadOnlyList<int> flightLevels = flightSettings.EnabledLevelIds;
        string key=scene.Fingerprint()+"|"+sourceImage+"|flight="+(flightChosen?string.Join(",",flightLevels):"unset")+"|memory="+GameRam.Mode;
        if(_freshNativeCompilation==null||_freshNativeCompilation.IsFaulted||_freshNativeCompilationKey!=key)
        {
            _freshNativeCompilationKey=key;
            string baseline=Path.Combine(_workspace.RootPath,"_local","authored-native",scene.Id+"-"+scene.Fingerprint()+".bin");
            ExportScratchSpace.PruneAuthoredBaselines(_workspace.RootPath,baseline);
            _freshNativeCompilation=Task.Run<INativeAuthoredCompilation>(async () =>
            {
                var flight = flightChosen ? await NativeFreeFlightCompositionPlan.CaptureAsync(sourceImage, flightLevels) : null;
                return scene.TargetBinding==null
                    ? await NativeFreshMainCompilation.CaptureAsync(sourceImage,scene,baseline,flight:flight)
                    : await NativeClearedLevelCompilation.CaptureAsync(sourceImage,scene,_retailCatalog,baseline);
            });
        }
        return _freshNativeCompilation;
    }
    private (UniversalComposeResult? Result,string? Error) DryRunFresh(INativeAuthoredCompilation compilation,UniversalSourceLibrary library,
        IReadOnlyList<UniversalMobyPlacement> placements,Func<string,UniversalSpecialAsset?>? special)
    {try{return(compilation.Compose(library,placements,special),null);}catch(Exception e)when(e is IOException or InvalidDataException){return(null,e.Message);}}

    private async Task CreateFreshMainBinAsync()
    {
        using var memory = UseProjectMemoryMode();
        bool authoredActive=_freshSceneDocument!=null;
        if(authoredActive?!await SaveFreshSceneAsync():!await SaveCurrentEditsAsync())return;
        var owner=CaptureSourceDiscOperationIdentity();
        string workspacePath=_workspace.RootPath, original=ResolveUniversalSourceDisc();
        string? imageSelection=_discImagePathBox.Text,skySelection=_skyboxDiscImagePathBox.Text;
        var flightIntent=NativeFreeFlightProjectExportIntent.Capture(workspacePath);
        var portalSnapshot=PortalRouteStore.Capture(workspacePath);
        var saved=SceneDocumentStore.ReadAll(workspacePath,_retailCatalog);
        var fingerprints=saved.ToDictionary(s=>s.ResolveTarget(_retailCatalog).Key,s=>s.Fingerprint());
        int? startLevelId=await ShowAuthoredProjectExportScopeAsync(saved,authoredActive?_currentLevel!.LevelId:10);
        if(startLevelId==null){_statusText.Text="BIN creation canceled.";return;}
        void RequireOwner()
        {
            if(!IsCurrentSourceDiscOperationIdentity(owner)||imageSelection!=_discImagePathBox.Text||skySelection!=_skyboxDiscImagePathBox.Text||
                authoredActive&&CaptureFreshScene().Fingerprint()!=fingerprints[_currentLevel!.Key])
                throw new InvalidDataException("The project, scene or source selection changed during export.");
            var current=SceneDocumentStore.ReadAll(workspacePath,_retailCatalog);
            if(current.Count!=saved.Count||current.Any(s=>!fingerprints.TryGetValue(s.ResolveTarget(_retailCatalog).Key,out var hash)||hash!=s.Fingerprint()))
                throw new InvalidDataException("A saved authored level changed during export. Save and create the BIN again.");
            flightIntent?.RequireCurrent(workspacePath);
            portalSnapshot.RequireCurrent(workspacePath);
            if(TryGetFreeFlightSettingsExportBlockReason(out string reason))throw new InvalidDataException(reason);
            if(TryGetId65BlankLabNormalCreateBinBlockReason(out reason))throw new InvalidDataException(reason);
        }
        RequireOwner();
        foreach(var scene in saved)NativeAuthoredSpawn.RequireSupported(scene);
        var(_,donors,_)=await EnsureUniversalCatalogAsync();var japanese=ResolveUniversalJapaneseSource();
        RequireOwner();
        var crashLevels=CrashPlayerSettings.Load(workspacePath);
        var plans=saved.Select(scene=>
        {
            var objects=scene.ProjectObjects();
            IReadOnlyList<UniversalMobyPlacement> placements=PlacementsFor(scene.ReconcileLedger(objects),objects);
            var target=scene.ResolveTarget(_retailCatalog);
            // Play as Crash: his two objects join the level's own.
            if(crashLevels.Contains(target.Key)&&UniversalCrashPlayer.IsPlayableLevel(target.LevelId))placements=UniversalCrashPlayer.WithPlayer(placements);
            return new NativeAuthoredExportScene(scene,placements);
        }).ToArray();
        string outputFolder=Path.Combine(workspacePath,"output");Directory.CreateDirectory(outputFolder);
        CombinedTestBinResult? retail=null;
        string? retailStage=null;
        try
        {
        var targets=FindEditedLevelExportTargets();
        if(targets.Count>0)
        {
            ReportCreateBinProgress("Merging saved levels…","Create BIN: composing saved changes from the other levels…");
            retailStage=Path.Combine(ExportScratchSpace.AggregateRoot(workspacePath),Guid.NewGuid().ToString("N"));
            retail=await CreateSavedEditsStageAsync(original,DiscImageLocator.FindCueForImage(original),retailStage,targets,true);
            if(retail.SkippedEdits>0)throw new InvalidDataException("Saved changes could not be included: "+string.Join("; ",retail.SkippedEditDetails));
            RequireOwner();
        }
        ReportCreateBinProgress("Building levels…","Create BIN: rebuilding geometry, collision, objects and assets…");
        string output=Path.Combine(outputFolder,"Spyro Editor - Authored Level.bin");
        var crash=ResolveUniversalCrashDisc();
        var yotd=ResolveYearOfTheDragonDisc();
        string hash=await Task.Run(async()=>
        {
            // A level you built is always rebuilt from the original disc, so "off" needs no flight plan.
            NativeFreeFlightCompositionPlan? flight=null;
            if(!_releaseMode||flightIntent.Enabled)
            {
                try{flight=await NativeFreeFlightCompositionPlan.CaptureAsync(original,flightIntent.EnabledLevelIds);}
                catch(InvalidDataException e)when(_releaseMode)
                {throw new InvalidDataException("Fly Anywhere needs the unchanged original USA disc image, and the game you opened is different. Open the original disc with Open Game…, or untick Fly Anywhere in Game Settings. ("+e.Message+")",e);}
            }
            return await NativeAuthoredProjectExporter.ExportAsync(donors,_retailCatalog,plans,japanese,output,workspacePath,startLevelId.Value,
                requireCurrent:()=>Dispatcher.UIThread.InvokeAsync(RequireOwner).GetAwaiter().GetResult(),
                stagedRetailImage:retail?.WroteImage==true?retail.OutputImagePath:null,retailEggs:retail?.ActiveDragonflyEggs,
                retailSignLevels:retail?.SignLevelKeys,retailNames:retail?.DeferredLevelNames,flight:flight,portalRoutes:portalSnapshot.Document,
                retailReturnClasses:retail?.ReturnClasses,crash:crash,yotd:yotd);
        });
        RequireOwner();
        string startName=saved.FirstOrDefault(s=>s.ResolveTarget(_retailCatalog).LevelId==startLevelId)?.Name??"Artisans";
        string flightNote=!flightIntent.RequiresRebuild?"":!flightIntent.Enabled?" Fly Anywhere is off in this game."
            :flightIntent.WholeGame?" Fly Anywhere is on in this game.":" Fly Anywhere is on in "+DescribeFlightLevels(flightIntent.EnabledLevelIds)+".";
        RememberExportedGame(Path.ChangeExtension(output,".cue"));
        _statusText.Text=$"Created {Path.ChangeExtension(output,".cue")}. New Game starts in {startName}. All saved level replacements and other saved edits are included."+flightNote+PreservedResearchExportNote()+MemoryModeExportNote(memory);
        }
        // The merged retail image is a working copy for this export only.
        finally{if(retailStage!=null)ExportScratchSpace.Delete(retailStage);}
    }

    private Control BuildFreshDocumentSettings()
    {
        _freshSettingsPanel=new StackPanel{Spacing=8,IsVisible=false,Name="FreshMainSettings"};return _freshSettingsPanel;
    }
    private Control BuildFreshTerrainTools()
    {
        _freshTerrainPanel=new StackPanel{Spacing=8,IsVisible=false,Name="FreshMainTerrainTools"};
        return _freshTerrainPanel;
    }
    private void RefreshFreshDocumentPanels()
    {
        if(_freshSettingsPanel==null||_freshTerrainPanel==null)return;
        _freshSettingsPanel.Children.Clear();_freshTerrainPanel.Children.Clear();
        _freshSettingsPanel.IsVisible=_freshTerrainPanel.IsVisible=_freshSceneDocument!=null;
        if(_retailTerrainWorkspace!=null)_retailTerrainWorkspace.IsVisible=true;
        if(_retailTerrainDetailsPanel!=null)_retailTerrainDetailsPanel.IsVisible=_freshSceneDocument==null;
        if(_retailNameDisclosure!=null)_retailNameDisclosure.IsVisible=_freshSceneDocument==null;
        if(_retailMusicDisclosure!=null)_retailMusicDisclosure.IsVisible=_freshSceneDocument==null;
        if(_homeworldTextGroup!=null)_homeworldTextGroup.IsVisible=true;
        RefreshPortalControlPanel();
        _terrainPlayableOnlyBox.IsVisible=_terrainBrushSafetyText.IsVisible=_freshSceneDocument==null;
        if(_freshSceneDocument is not { } scene)return;
        _freshSettingsPanel.Children.Add(NewSmallNote(_freshSceneDocument.TargetBinding==null?"Your level opens when you choose New Game in the exported game.":"This blank scene replaces the current level in your exported game."));
        int maximumNameLength=scene.TargetBinding==null?19:_textTargets.FindForLevel(_currentLevel!)?.MaxLength??19;
        TextBox name=new(){Name="FreshMainLevelName",Text=scene.Name,MaxLength=maximumNameLength};
        name.TextChanged+=(_,_)=>
        {
            if(!TextTargetCatalog.IsSafeReplacement(name.Text??"",maximumNameLength))return;
            bool flightDraft=_freeFlightDraft;
            _freshSceneDocument=_freshSceneDocument! with{Name=name.Text!};_currentLevel=_freshSceneDocument.ResolveTarget(_retailCatalog);
            BindFreshLevelPickerOption(_currentLevel);SyncLevelPickers(_currentLevel);RefreshFreeFlightSettingsEditor(_currentLevel);
            _freeFlightEnabled.IsChecked=flightDraft;RefreshCurrentLevelDetails();
        };
        _freshSettingsPanel.Children.Add(new TextBlock{Text=$"Level name (up to {maximumNameLength} letters, spaces or apostrophes)"});_freshSettingsPanel.Children.Add(name);
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
            _viewport.SetLevelEntryPose(new PortableLevelEntryPose(_currentLevel!.Key,_freshSceneDocument.Name,_currentLevel.LevelId,_currentLevel.SourceWadEntry,0,(int)point.X*16,(int)point.Y*16,(int)point.Z*16,_freshSceneDocument.Heading,16));
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
        // Flight skies are left out: their camera bounds pop outside flight levels.
        List<object> skyChoices=["Plain sky color",.._retailCatalog.Levels.Where(l=>l.SourceWadEntry is >=10 and <=78&&l.SourceWadEntry%2==0&&!NativeSkyGeometrySafety.IsFlight(l.Key))];
        ComboBox skySource=new(){Name="FreshMainSkySource",ItemsSource=skyChoices,MinHeight=32,
            SelectedItem=skyChoices.OfType<Spyro.Editor.Core.Levels.LevelDefinition>().FirstOrDefault(l=>l.SourceWadEntry==scene.SkySourceWadEntry)??skyChoices[0]};
        _freshSettingsPanel.Children.Add(new TextBlock{Text="Skybox (borrow a level's sky)"});_freshSettingsPanel.Children.Add(skySource);
        Button applySkySource=NewButton("Use this skybox",()=>
        {
            var level=skySource.SelectedItem as Spyro.Editor.Core.Levels.LevelDefinition;
            _freshSceneDocument=_freshSceneDocument! with{SkySourceWadEntry=level?.SourceWadEntry};
            _statusText.Text=level==null
                ? "The level uses its plain sky color again; Save Level records it."
                : $"The level will use {level.DisplayName}'s skybox in the game. Save Level records it.";
        });applySkySource.Name="FreshMainSetSkySource";_freshSettingsPanel.Children.Add(applySkySource);
        ComboBox source=new(){Name="FreshMainMaterialSource",ItemsSource=_retailCatalog.Levels.Where(l=>l.SourceWadEntry is >=10 and <=78).ToList(),SelectedItem=_retailCatalog.FindByKey("stonehill"),MinHeight=32};
        TextBox texture=new(){Name="FreshMainMaterialId",Text="0"};
        _freshTerrainPanel.Children.Add(NewSmallNote(_releaseMode
            ? "This is a level you built. Select a tile to move it, edit its corners or paint it. Add Terrain extends the level; Remove Terrain cuts out the selected tile."
            : "Select a face to move it, edit its points or choose a material. Add Terrain extends your level; Remove Terrain cuts out the selected face."));
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
