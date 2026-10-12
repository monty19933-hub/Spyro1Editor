using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.App.Views;

public sealed partial class MainWindow
{
    private bool _startBlankBusy;
    internal Action<string>? StartBlankSnapshotFaultForTesting { get; set; }
    private bool TryLoadReplacementScene(LevelDefinition level)
    {
        var document=SceneDocumentStore.ReadForTarget(_workspace.RootPath,level,_retailCatalog);
        if(document==null)return false;
        string key=document.TargetKey;
        document=document with{Ledger=StartBlankRecoveryStore.ReconcileLifetime(_workspace.RootPath,key,document.Ledger,document.TargetBinding?.NativePrefixRows??0)};
        ApplyFreshScene(document);
        _statusText.Text=_releaseMode
            ?$"Opened {document.Name}: {Count(document.Surfaces.Count,"terrain tile","terrain tiles")}, {Count(document.Objects.Count,"object","objects")}."
            :$"Opened {document.Name}: {document.Surfaces.Count} authored faces, {document.Objects.Count} objects.";
        return true;
    }
    private bool CanStartBlankCurrentLevel() => _currentLevel!=null&&
        (_freshSceneDocument!=null || _currentLevel.LevelId is >=10 and <=64&&_currentLevel.LevelId%10<=4);
    private async Task StartBlankCurrentLevelAsync()
    {
        if(_startBlankBusy||_createBinBusy||_regularEditorPersistenceBusy||_workspaceTransitionBusy||_loadingLevel)return;
        if(!CanStartBlankCurrentLevel())
        { _statusText.Text="Flight courses cannot be cleared. Select a normal level or an authored level.";return; }
        var level=_currentLevel!;string root=_workspace.RootPath;
        int faces=_currentGeometry?.Polygons.Count(p=>!p.IsTerrainRemoved)??0,objects=_currentMobys.Count(m=>!m.IsRemoved);
        bool accepted=await ShowStartBlankConfirmationAsync(level.DisplayName,faces,objects,false);
        if(!accepted)return;
        if(_currentLevel!=level||_workspace.RootPath!=root)return;
        _startBlankBusy=true;
        try
        {
            if(await StartBlankConfirmedAsync(level) is {})
                _statusText.Text=$"{level.DisplayName} is blank: a small starting floor and no objects. Recovery > Restore Before Clear brings back your saved work.";
        }
        finally{_startBlankBusy=false;RefreshCurrentLevelDetails();}
    }
    /// <summary>The object identities a level emptied with Start Blank keeps (its retail level and its original object
    /// rows); a built level keeps its own.</summary>
    private async Task<SceneTargetBinding?> StartBlankBindingAsync(LevelDefinition level)
    {
        if(_freshSceneDocument!=null)return _freshSceneDocument.TargetBinding;
        string image=ResolveUniversalSourceDisc();
        int nativeRows=await Task.Run(()=>
        {
            UniversalSourceLibrary.RequireOriginal(image);
            var source=new UniversalSourceLibrary(image,_retailCatalog);
            try{_=NativeClearedRetailBinding.Prepare(source.Disc.Exe,source.Disc.Exe,level);}
            catch(InvalidDataException e){throw new InvalidDataException("Start Blank is not available for this level yet. Your saved level is unchanged. Try Artisans or another supported level listed in the user guide.",e);}
            return source.Get(level.Key).Data.Scene().RowCount;
        });
        var binding=new SceneTargetBinding(level.Key,level.LevelId,level.SourceWadEntry,level.SourceWadEntry-1,UniversalSourceLibrary.OriginalUsaSha256,nativeRows);
        _=binding.Resolve(_retailCatalog);
        return binding;
    }
    /// <summary>Start Blank once the user has agreed (the caller holds <c>_startBlankBusy</c>): saves the level, keeps a
    /// recovery copy and opens the blank scene. Returns it, or null when it stopped (the status line says why).</summary>
    /// <param name="starter">Begin with the meadow of <see cref="StarterLevel"/> (Start Blank); an import replaces the
    /// terrain at once and starts from an empty level.</param>
    private async Task<SceneDocument?> StartBlankConfirmedAsync(LevelDefinition level,bool starter=true)
    {
        string root=_workspace.RootPath;
        try
        {
            if(!await SaveCurrentEditsAsync())return null;
            var geometry=_currentGeometry;string terrainSignature=BuildTerrainEditSignature(geometry),mobySignature=BuildMobyEditSignature(_currentMobys);
            string? sceneSignature=_freshSceneDocument==null?null:CaptureFreshScene().Fingerprint();
            void RequireCurrent()
            {
                if(_workspace.RootPath!=root||_currentLevel!=level||_currentGeometry!=geometry||
                    BuildTerrainEditSignature(geometry)!=terrainSignature||BuildMobyEditSignature(_currentMobys)!=mobySignature||
                    (_freshSceneDocument==null?null:CaptureFreshScene().Fingerprint())!=sceneSignature)
                    throw new InvalidDataException("The current level changed while its recovery snapshot was prepared. Start Blank was canceled.");
            }
            bool built=_freshSceneDocument!=null;
            SceneTargetBinding? binding=await StartBlankBindingAsync(level);
            UniversalPlacementLedger ledger;
            if(built)ledger=CaptureFreshScene().Ledger;
            else
            {
                RequireCurrent();UniversalNativeLayoutStore.Validate(root,level.Key,binding!.NativePrefixRows);
                ledger=UniversalPlacementLedger.Reconcile(Path.Combine(root,level.Key+"-native-edits.json"),_currentMobys);
            }
            string key=binding?.LevelKey??"authored65";
            ledger=StartBlankRecoveryStore.ReconcileLifetime(root,key,ledger,binding?.NativePrefixRows??0);
            var blank=(starter?StarterLevel.Create(level.DisplayName,_retailCatalog):SceneDocument.New(level.DisplayName)) with{TargetBinding=binding,Ledger=ledger};
            await StartBlankRecoveryStore.ClearAsync(root,key,blank,RequireCurrent,StartBlankSnapshotFaultForTesting);
            ApplyFreshScene(blank);
            if(starter)ApplyEditorExperienceForCurrentLevel(_modernTerrainWorkspaceTab);
            return blank;
        }
        catch(Exception e)when(e is IOException or InvalidDataException or ArgumentException or UnauthorizedAccessException or System.Text.Json.JsonException)
        { _statusText.Text="Start Blank stopped: "+e.Message;return null; }
    }
    private async Task RestoreBeforeClearAsync()
    {
        if(_startBlankBusy||_createBinBusy||_regularEditorPersistenceBusy||_workspaceTransitionBusy||_loadingLevel||_currentLevel==null)return;
        string root=_workspace.RootPath,key=_freshSceneDocument?.TargetBinding?.LevelKey??_currentLevel.Key;
        var level=_currentLevel;
        try
        {
            var record=StartBlankRecoveryStore.Read(root,key);
            if(record?.Active!=true){_statusText.Text=_releaseMode?$"{level.DisplayName} has not been emptied with Start Blank, so there is nothing to restore.":"No active before-clear snapshot is available for this level.";return;}
            if(!await ShowStartBlankConfirmationAsync(level.DisplayName,0,0,true))return;
            if(_workspace.RootPath!=root||_currentLevel!=level)return;
            _startBlankBusy=true;
            if(!await SaveCurrentEditsAsync())return;
            string fingerprint=CaptureFreshScene().Fingerprint();
            void RequireCurrent()
            {
                if(_workspace.RootPath!=root||_currentLevel!=level||_freshSceneDocument==null||CaptureFreshScene().Fingerprint()!=fingerprint)
                    throw new InvalidDataException("The current scene changed during recovery; restore stopped.");
            }
            await StartBlankRecoveryStore.RestoreAsync(root,key,RequireCurrent);
            string path=SceneDocumentStore.PathForTarget(root,key);
            if(File.Exists(path))ApplyFreshScene(SceneDocument.Read(path));
            else
            {
                var original=_retailCatalog.FindByKey(key)??throw new InvalidDataException("The original level is not in the source catalog.");
                await SelectLevelAsync(original);
            }
            _statusText.Text=_releaseMode?$"{level.DisplayName} is back the way it was before Start Blank. The blank version you built was kept in the project's recovery folder.":"Restored the saved level from before clear. The blank scene was preserved in its recovery folder.";
        }
        catch(Exception e)when(e is IOException or InvalidDataException or ArgumentException or UnauthorizedAccessException or System.Text.Json.JsonException)
        { _statusText.Text="Restore Before Clear stopped: "+e.Message; }
        finally{_startBlankBusy=false;RefreshCurrentLevelDetails();}
    }
    /// <summary>File ▾ > Free Deleted Objects' Save Rows…: a level keeps one of the game's 256 save rows for every object
    /// ever added, deleted ones included, so an older save never marks a different object as collected. This gives
    /// the deleted objects' rows back (for a new game).</summary>
    private async Task FreeRetiredSaveRowsAsync()
    {
        if(_startBlankBusy||_createBinBusy||_regularEditorPersistenceBusy||_workspaceTransitionBusy||_loadingLevel)return;
        if(_freshSceneDocument==null||_currentLevel==null)
        { _statusText.Text="Save rows are freed in a level you emptied with Start Blank or built with New Level. Open one first.";return; }
        var level=_currentLevel;string root=_workspace.RootPath;
        var before=CaptureFreshScene();
        int prefix=before.TargetBinding?.NativePrefixRows??0,deleted=before.Ledger.Entries.Count-before.Objects.Count(o=>!o.Value.IsRemoved);
        if(deleted<=0){_statusText.Text=$"{level.DisplayName} has no save rows held by deleted objects.";return;}
        var content=new StackPanel{Margin=new Thickness(22),Spacing=12};
        content.Children.Add(new TextBlock{Text="Free the save rows of deleted objects?",FontSize=20,FontWeight=FontWeight.SemiBold,TextWrapping=TextWrapping.Wrap});
        content.Children.Add(new TextBlock{Name="FreeSaveRowsExplanation",TextWrapping=TextWrapping.Wrap,Text=
            $"The game saves what you've collected in {level.DisplayName} in 256 rows. Every object you add takes one, and a deleted object keeps its row so an older save never marks a different object as collected. "+
            $"{Count(deleted,"row is","rows are")} held by deleted objects, and {Math.Max(0,256-prefix-before.Ledger.Entries.Count)} free.\n"+
            "Freeing them lets you add that many more objects. Play this level from a new game afterwards: a memory card that already played it from an earlier export can show some gems, eggs or dragons as already collected."});
        var dialog=new Window{Name="FreeSaveRowsDialog",Title="Free Deleted Objects' Save Rows",Width=520,SizeToContent=SizeToContent.Height,CanResize=false,WindowStartupLocation=WindowStartupLocation.CenterOwner,Content=content};
        var actions=new StackPanel{Orientation=Orientation.Horizontal,Spacing=10,HorizontalAlignment=HorizontalAlignment.Right};
        Button cancel=NewButton("Cancel"),confirm=NewButton("Free Save Rows");
        cancel.Name="FreeSaveRowsCancel";confirm.Name="FreeSaveRowsConfirm";
        cancel.Click+=(_,_)=>dialog.Close(false);confirm.Click+=(_,_)=>dialog.Close(true);
        StyleModernPrimaryButton(confirm,ModernBlue,150);actions.Children.Add(cancel);actions.Children.Add(confirm);content.Children.Add(actions);
        if(!await dialog.ShowDialog<bool>(this))return;
        if(_currentLevel!=level||_workspace.RootPath!=root)return;
        _startBlankBusy=true;
        try{int freed=await FreeRetiredSaveRowsConfirmedAsync();_statusText.Text=$"Freed {Count(freed,"save row","save rows")} in {level.DisplayName}. Play it from a new game.";}
        catch(Exception e)when(e is IOException or InvalidDataException or ArgumentException or UnauthorizedAccessException or System.Text.Json.JsonException)
        { _statusText.Text="Freeing save rows stopped: "+e.Message; }
        finally{_startBlankBusy=false;RefreshCurrentLevelDetails();}
    }
    /// <summary>Saves the open built level, gives back its deleted objects' save rows and reopens it.</summary>
    internal async Task<int> FreeRetiredSaveRowsConfirmedAsync()
    {
        if(_freshSceneDocument==null)throw new InvalidDataException("No built level is open.");
        if(!await SaveFreshSceneAsync())throw new InvalidDataException("The level could not be saved first.");
        var (scene,freed)=CaptureFreshScene().FreeRetiredSaveRows();
        string key=scene.TargetKey;
        if(StartBlankRecoveryStore.Read(_workspace.RootPath,key)!=null)
            StartBlankRecoveryStore.ReplaceLifetime(_workspace.RootPath,key,scene.Ledger,scene.TargetBinding?.NativePrefixRows??0);
        await scene.SaveAsync(FreshScenePath);
        ApplyFreshScene(scene);
        return freed;
    }
    private async Task<bool> ShowStartBlankConfirmationAsync(string name,int faces,int objects,bool restoring)
    {
        var content=new StackPanel{Margin=new Thickness(22),Spacing=12};
        content.Children.Add(new TextBlock{Text=restoring?"Restore "+name+" from before clear?":"Start blank in "+name+"?",FontSize=20,FontWeight=FontWeight.SemiBold,TextWrapping=TextWrapping.Wrap});
        content.Children.Add(new TextBlock{Name="StartBlankExplanation",TextWrapping=TextWrapping.Wrap,Text=_releaseMode
            ?restoring
                ?$"This brings {name} back the way it was before you cleared it: its terrain, objects and settings. The blank version you built is saved first, so nothing is lost. Other levels are not touched."
                :$"This empties {name}: its {Count(objects,"object","objects")} and {Count(faces,"terrain tile","terrain tiles")} are taken out, leaving a small floor to build on. Other levels are not touched.\nA recovery copy of the level as it is now is saved first. File ▾ > Restore Before Clear… brings it back."
            :restoring
            ?"Your current blank scene will be saved and preserved. The previous level's geometry, objects and settings will return. Other levels stay unchanged."
            :$"Remove {objects} objects and {faces} terrain faces from this level, including their collision and unused assets in the exported game. A small editable starting floor will remain.\nYour current changes will be saved in a verified recovery snapshot before clearing. Other levels stay unchanged."});
        var dialog=new Window{Name=restoring?"RestoreBeforeClearDialog":"StartBlankDialog",Title=restoring?"Restore Before Clear":"Start Blank in Current Level",Width=520,SizeToContent=SizeToContent.Height,CanResize=false,WindowStartupLocation=WindowStartupLocation.CenterOwner,Content=content};
        var actions=new StackPanel{Orientation=Orientation.Horizontal,Spacing=10,HorizontalAlignment=HorizontalAlignment.Right};
        Button cancel=NewButton("Cancel"),confirm=NewButton(restoring?"Restore Before Clear":"Start Blank");
        cancel.Name="StartBlankCancel";confirm.Name="StartBlankConfirm";
        cancel.Click+=(_,_)=>dialog.Close(false);confirm.Click+=(_,_)=>dialog.Close(true);
        StyleModernPrimaryButton(confirm,ModernBlue,132);actions.Children.Add(cancel);actions.Children.Add(confirm);content.Children.Add(actions);
        return await dialog.ShowDialog<bool>(this);
    }
}
