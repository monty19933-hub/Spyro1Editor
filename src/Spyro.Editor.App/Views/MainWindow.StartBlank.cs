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
        string key=document.TargetBinding?.LevelKey??"authored65";
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
            if(!await SaveCurrentEditsAsync())return;
            var geometry=_currentGeometry;string terrainSignature=BuildTerrainEditSignature(geometry),mobySignature=BuildMobyEditSignature(_currentMobys);
            string? sceneSignature=_freshSceneDocument==null?null:CaptureFreshScene().Fingerprint();
            void RequireCurrent()
            {
                if(_workspace.RootPath!=root||_currentLevel!=level||_currentGeometry!=geometry||
                    BuildTerrainEditSignature(geometry)!=terrainSignature||BuildMobyEditSignature(_currentMobys)!=mobySignature||
                    (_freshSceneDocument==null?null:CaptureFreshScene().Fingerprint())!=sceneSignature)
                    throw new InvalidDataException("The current level changed while its recovery snapshot was prepared. Start Blank was canceled.");
            }
            SceneTargetBinding? binding=_freshSceneDocument?.TargetBinding;
            UniversalPlacementLedger ledger;
            if(_freshSceneDocument!=null)ledger=CaptureFreshScene().Ledger;
            else
            {
                string image=ResolveUniversalSourceDisc();
                int nativeRows=await Task.Run(()=>
                {
                    UniversalSourceLibrary.RequireOriginal(image);
                    var source=new UniversalSourceLibrary(image,_retailCatalog);
                    try{_=NativeClearedRetailBinding.Prepare(source.Disc.Exe,source.Disc.Exe,level);}
                    catch(InvalidDataException e){throw new InvalidDataException("Start Blank is not available for this level yet. Your saved level is unchanged. Try Artisans or another supported level listed in the user guide.",e);}
                    return source.Get(level.Key).Data.Scene().RowCount;
                });
                RequireCurrent();UniversalNativeLayoutStore.Validate(root,level.Key,nativeRows);
                binding=new(level.Key,level.LevelId,level.SourceWadEntry,level.SourceWadEntry-1,UniversalSourceLibrary.OriginalUsaSha256,nativeRows);
                _=binding.Resolve(_retailCatalog);
                ledger=UniversalPlacementLedger.Reconcile(Path.Combine(root,level.Key+"-native-edits.json"),_currentMobys);
            }
            string key=binding?.LevelKey??"authored65";
            ledger=StartBlankRecoveryStore.ReconcileLifetime(root,key,ledger,binding?.NativePrefixRows??0);
            var blank=SceneDocument.New(level.DisplayName) with{TargetBinding=binding,Ledger=ledger};
            await StartBlankRecoveryStore.ClearAsync(root,key,blank,RequireCurrent,StartBlankSnapshotFaultForTesting);
            ApplyFreshScene(blank);
            _statusText.Text=$"{level.DisplayName} is blank: a small starting floor and no objects. Recovery > Restore Before Clear brings back your saved work.";
        }
        catch(Exception e)when(e is IOException or InvalidDataException or ArgumentException or UnauthorizedAccessException or System.Text.Json.JsonException)
        { _statusText.Text="Start Blank stopped: "+e.Message; }
        finally{_startBlankBusy=false;RefreshCurrentLevelDetails();}
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
