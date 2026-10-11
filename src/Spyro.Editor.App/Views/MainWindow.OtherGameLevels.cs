using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Music;
using Spyro.Editor.Core.YearOfTheDragon;

namespace Spyro.Editor.App.Views;

/// <summary>File ▾ > New Level from Spyro: Year of the Dragon… and New Level from Crash Bandicoot: Warped…: the open
/// level's place is taken by a level of another game, rebuilt from the user's own disc (<see cref="YearOfTheDragonLevelImport"/>,
/// <see cref="Spyro.Editor.Core.Crash.CrashLevelImport"/>): its ground, walls, textures, water and lava. The open level is emptied the way Start Blank empties it (with its recovery copy), so its own dragons,
/// egg thieves and save rows stay; objects are added afterwards.</summary>
public sealed partial class MainWindow
{
    internal sealed record OtherGameLevelChoice(YearOfTheDragonLevel Level, bool HotGroundIsDeadly, bool WaterCostsALife, bool FillIn = true);
    /// <summary>Answers the level dialog in tests (null: cancel).</summary>
    internal Func<OtherGameLevelChoice?>? OtherGameLevelChoiceForTesting { get; set; }
    private CancellationTokenSource? _otherGameImportCancel;

    /// <summary>What the shared flow needs from one game's importer: the game, the chosen level, its pictures folder name,
    /// whether to fill in afterwards, and the import itself (binding, original USA disc, pictures folder, progress, stop)
    /// giving the scene for the blank level, its name and a plain summary.</summary>
    private sealed record OtherGameImport(string Game, string LevelName, string Slug, bool FillIn,
        Func<SceneTargetBinding?, string, string, IProgress<string>, CancellationToken, Task<(Func<SceneDocument, SceneDocument> ApplyTo, string SceneName, string Summary)>> Build);

    /// <summary>The open level can take another game's level (a level or homeworld, not a flight course).</summary>
    private bool ReadyForOtherGameLevel()
    {
        if(_startBlankBusy||_createBinBusy||_regularEditorPersistenceBusy||_workspaceTransitionBusy||_loadingLevel)return false;
        if(CanStartBlankCurrentLevel())return true;
        _statusText.Text="Open the level whose place the new level takes (a level or homeworld, not a flight course), then choose this again.";
        return false;
    }

    /// <summary>The name of the game's level whose place the open level has (its dragons and egg thieves are the ones it needs).</summary>
    private string OwnerLevelName(LevelDefinition slot) =>
        _retailCatalog.FindByKey(_freshSceneDocument?.TargetBinding?.LevelKey??slot.Key)?.DisplayName??slot.DisplayName;

    private async Task NewLevelFromYearOfTheDragonAsync()
    {
        if(!ReadyForOtherGameLevel())return;
        var slot=_currentLevel!;string root=_workspace.RootPath;
        var choice=OtherGameLevelChoiceForTesting!=null?OtherGameLevelChoiceForTesting():await ShowYearOfTheDragonLevelDialogAsync(slot.DisplayName,OwnerLevelName(slot));
        if(choice==null||_currentLevel!=slot||_workspace.RootPath!=root)return;
        string disc=YearOfTheDragonDiscLocator.Find(_workspace);
        if(!YearOfTheDragonDiscLocator.IsUsable(disc)){_statusText.Text="Choose your Spyro: Year of the Dragon disc first.";return;}
        var level=choice.Level;var catalog=_retailCatalog;
        await NewLevelFromOtherGameAsync(slot,new("Year of the Dragon",level.Name,YearOfTheDragonLevelImport.Slug(level.Name),choice.FillIn,
            async(binding,usa,pictures,progress,token)=>
            {
                var r=await Task.Run(()=>YearOfTheDragonLevelImport.ImportAsync(disc,level,usa,catalog,binding,
                    new(){PictureFolder=pictures,HotGroundIsDeadly=choice.HotGroundIsDeadly,WaterCostsALife=choice.WaterCostsALife},progress,token),token);
                return (r.ApplyTo,r.Scene.Name,PlainImportSummary(r.Report));
            }));
    }

    /// <summary>Builds another game's level in the open level's place: the import first (if it can't be made, the open
    /// level is left as it was), then Start Blank (with its recovery copy), the scene, the level's own song, a save, and
    /// (when asked) Fill In.</summary>
    private async Task NewLevelFromOtherGameAsync(LevelDefinition slot,OtherGameImport import)
    {
        string root=_workspace.RootPath,name=import.LevelName;
        _startBlankBusy=true;
        using var cancel=_otherGameImportCancel=new CancellationTokenSource();
        var (progressWindow,progressLine)=BuildOtherGameProgressWindow(name,cancel);
        _=progressWindow.ShowDialog(this);
        try
        {
            _statusText.Text=progressLine.Text=$"Building {name} from your {import.Game} disc (about a minute)…";
            var binding=await StartBlankBindingAsync(slot);
            string slotName=_retailCatalog.FindByKey(binding?.LevelKey??"")?.DisplayName??slot.DisplayName;
            string usa=ResolveTerrainTextureSourceImage(_retailCatalog.FindByKey(binding?.LevelKey??"")??slot);
            if(string.IsNullOrEmpty(usa))usa=ResolveUniversalSourceDisc();
            string pictures=Path.Combine(root,"custom-terrain-textures",import.Slug);
            var progress=new Progress<string>(m=>{ if(!cancel.IsCancellationRequested)_statusText.Text=progressLine.Text=$"{name}: {m}"; });
            var built=await import.Build(binding,usa,pictures,progress,cancel.Token);
            if(_currentLevel!=slot||_workspace.RootPath!=root)return;
            progressLine.Text=$"Emptying {slot.DisplayName} (a recovery copy is saved first)…";
            var blank=await StartBlankConfirmedAsync(slot);
            if(blank==null)return;
            // Replacing a level built earlier: its objects are gone, so their save rows go to the new level (new game).
            string freedNote="";
            if(blank.Ledger.Entries.Count>0)
            {
                int freed=await FreeRetiredSaveRowsConfirmedAsync();
                if(freed>0){blank=CaptureFreshScene();freedNote=$"\nThe {freed} save rows of the level it replaces were given back for its objects: play it from a new game.";}
            }
            // The level it replaces keeps playing its own song (Game Settings changes it).
            if(_retailCatalog.FindByKey(binding?.LevelKey??"") is {LevelId:>=10 and <=64} owner)
                blank=blank with{MusicTrack=MusicTrackCatalog.GetNativeTrackId(owner)};
            ApplyFreshScene(built.ApplyTo(blank));
            if(!await SaveFreshSceneAsync()){_statusText.Text=$"{name} was built but could not be saved. Save Level tries again.";return;}
            progressWindow.Close();
            if(import.FillIn)
            {
                _startBlankBusy=false;
                await FillInLevelAsync(new LevelFillChoice(true,true,true,-1));
                string fillSummary=_statusText.Text??"";
                _startBlankBusy=true;
                if(_freshSceneDocument!=null)await SaveFreshSceneAsync();
                _statusText.Text=$"{built.SceneName} takes {slotName}'s place, built from {import.Game}'s {name}. {fillSummary}";
            }
            else _statusText.Text=$"{built.SceneName} takes {slotName}'s place, built from {import.Game}'s {name}. "+
                "Add what the line under the treasure lists, then gems and enemies, then Create BIN.";
            string filled=import.FillIn?"\n"+_statusText.Text:"";
            await ShowOtherGameLevelReadyAsync(built.SceneName,slotName,built.Summary+freedNote+filled,import.FillIn);
        }
        catch(OperationCanceledException){_statusText.Text=$"Stopped building {name}. {slot.DisplayName} is unchanged.";}
        catch(Exception e)when(e is YearOfTheDragonImportException or Spyro.Editor.Core.Crash.CrashLevelImportException)
        {_statusText.Text=$"{name} can't be made into a Spyro the Dragon level: {e.Message} {slot.DisplayName} is unchanged.";}
        catch(Exception e)when(e is IOException or InvalidDataException or ArgumentException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {_statusText.Text=$"Building {name} stopped: {e.Message}";}
        finally{progressWindow.Close();_otherGameImportCancel=null;_startBlankBusy=false;RefreshCurrentLevelDetails();}
    }

    /// <summary>A small window while a level is built: what it is doing, and Stop (before the open level is emptied).</summary>
    private (Window Window,TextBlock Line) BuildOtherGameProgressWindow(string name,CancellationTokenSource cancel)
    {
        var content=new StackPanel{Margin=new Thickness(22),Spacing=12};
        content.Children.Add(new TextBlock{Text=$"Building {name}…",FontSize=18,FontWeight=FontWeight.SemiBold});
        var line=new TextBlock{Name="OtherGameProgressLine",TextWrapping=TextWrapping.Wrap,MinHeight=40};
        content.Children.Add(line);
        content.Children.Add(new ProgressBar{IsIndeterminate=true,Height=6});
        var stop=NewButton("Stop");stop.Name="OtherGameProgressStop";stop.HorizontalAlignment=HorizontalAlignment.Right;
        stop.Click+=(_,_)=>{cancel.Cancel();line.Text="Stopping…";stop.IsEnabled=false;};
        content.Children.Add(stop);
        var window=new Window{Name="OtherGameProgressDialog",Title="New Level",Width=460,SizeToContent=SizeToContent.Height,CanResize=false,
            WindowStartupLocation=WindowStartupLocation.CenterOwner,Content=content};
        return (window,line);
    }

    /// <summary>The report in everyday words (the full one is in the import's technical lines).</summary>
    private static string PlainImportSummary(YearOfTheDragonImportReport r)
    {
        List<string> lines=[$"{Count(r.Faces,"terrain tile","terrain tiles")}, {Count(r.KeptTextures,"picture","pictures")}"+
            (r.Textures>r.KeptTextures?$" ({r.Textures-r.KeptTextures} similar ones merged to fit the game's texture memory)":"")+"."];
        var liquids=r.GroundKinds.Where(k=>k.Key is not ("solid" or "other")).Select(k=>$"{k.Value} {k.Key}").ToList();
        if(liquids.Count>0)lines.Add("Special ground: "+string.Join(", ",liquids)+".");
        lines.Add(r.SpawnSource.Contains("start",StringComparison.OrdinalIgnoreCase)
            ?"Spyro starts where Year of the Dragon's level begins."
            :"Spyro starts on open ground near the middle of the level; move the start marker if you like.");
        lines.AddRange(r.Warnings.Select(w=>"Note: "+w));
        return string.Join("\n",lines);
    }

    /// <summary><paramref name="levelName"/>: the open level; <paramref name="slotName"/>: the game's level whose place it
    /// has (its dragons and egg thieves are the ones the level needs).</summary>
    private async Task<OtherGameLevelChoice?> ShowYearOfTheDragonLevelDialogAsync(string levelName,string slotName)
    {
        var content=new StackPanel{Margin=new Thickness(22),Spacing=12};
        content.Children.Add(new TextBlock{Text=$"Make {levelName} into a Year of the Dragon level?",FontSize=20,FontWeight=FontWeight.SemiBold,TextWrapping=TextWrapping.Wrap});
        var levels=YearOfTheDragonLevels.All.Where(l=>l.Kind!=YearOfTheDragonLevelKind.Bonus).ToList();
        string Label(YearOfTheDragonLevel l)=>l.Kind switch
        {
            YearOfTheDragonLevelKind.Homeworld=>l.Name+" (homeworld)",
            YearOfTheDragonLevelKind.Speedway=>l.Name+" (speedway course)",
            YearOfTheDragonLevelKind.Boss=>l.Name+" (boss arena)",
            _=>l.Name
        };
        var pick=new ComboBox{Name="YotdLevelChoice",HorizontalAlignment=HorizontalAlignment.Stretch,ItemsSource=levels.Select(Label).ToArray(),
            SelectedIndex=Math.Max(0,levels.FindIndex(l=>l.Name=="Sunny Villa"))};
        content.Children.Add(pick);
        content.Children.Add(new TextBlock{Name="YotdLevelExplanation",TextWrapping=TextWrapping.Wrap,Text=
            $"Its ground, walls, textures, water and lava are rebuilt from your Spyro: Year of the Dragon disc and take the place of {levelName}'s terrain and objects. "+
            $"A recovery copy of {levelName} is saved first: File ▾ > Restore Before Clear… brings it back.\n"+
            $"Building takes about a minute. The line under the level's treasure then says which of {slotName}'s dragons, egg thieves and Return Home it still needs. "+
            "Characters, gems and enemies of the other game are not brought over, and neither are its side areas."});
        var hot=new CheckBox{Name="YotdHotGroundDeadly",Content="Lava and acid are deadly, as in Year of the Dragon",IsChecked=true};
        var water=new CheckBox{Name="YotdWaterCostsALife",Content="Deep water costs a life (Spyro can't swim in this game)",IsChecked=true};
        var fill=new CheckBox{Name="YotdFillIn",Content=$"Then put in {slotName}'s dragons and egg thieves, Return Home and gems (you can move them afterwards)",IsChecked=true};
        content.Children.Add(hot);content.Children.Add(water);content.Children.Add(fill);
        var discNote=new TextBlock{Name="YotdDiscNote",TextWrapping=TextWrapping.Wrap,Foreground=new SolidColorBrush(Color.FromRgb(168,98,0))};
        var chooseDisc=NewButton("Choose Year of the Dragon disc…");chooseDisc.Name="YotdChooseDisc";
        var actions=new StackPanel{Orientation=Orientation.Horizontal,Spacing=10,HorizontalAlignment=HorizontalAlignment.Right};
        Button cancel=NewButton("Cancel"),confirm=NewButton("Make Level");
        cancel.Name="YotdLevelCancel";confirm.Name="YotdLevelConfirm";
        void RefreshDisc()
        {
            bool ok=YearOfTheDragonDiscLocator.IsUsable(YearOfTheDragonDiscLocator.Find(_workspace));
            discNote.Text=ok?"":"This needs your Spyro: Year of the Dragon (USA) disc image. Nothing from that game ships with the editor.";
            discNote.IsVisible=chooseDisc.IsVisible=!ok;confirm.IsEnabled=ok;
        }
        chooseDisc.Click+=async(_,_)=>
        {
            var files=await StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
                {Title="Select your Spyro: Year of the Dragon (USA) disc image (.bin)",AllowMultiple=false});
            if(files.FirstOrDefault()?.Path.LocalPath is not {} picked)return;
            try{string bin=DiscImageLocator.ResolveImagePath(picked);await Task.Run(()=>YearOfTheDragonDiscLocator.Configure(_workspace,bin));_yearOfTheDragon=null;}
            catch(Exception e)when(e is IOException or InvalidDataException or UnauthorizedAccessException){discNote.Text=e.Message;return;}
            RefreshDisc();
        };
        content.Children.Add(discNote);content.Children.Add(chooseDisc);
        var dialog=new Window{Name="YotdLevelDialog",Title="New Level from Spyro: Year of the Dragon",Width=560,SizeToContent=SizeToContent.Height,CanResize=false,
            WindowStartupLocation=WindowStartupLocation.CenterOwner,Content=content};
        cancel.Click+=(_,_)=>dialog.Close(false);confirm.Click+=(_,_)=>dialog.Close(true);
        StyleModernPrimaryButton(confirm,ModernBlue,132);actions.Children.Add(cancel);actions.Children.Add(confirm);content.Children.Add(actions);
        RefreshDisc();
        if(!await dialog.ShowDialog<bool>(this)||pick.SelectedIndex<0)return null;
        return new(levels[pick.SelectedIndex],hot.IsChecked==true,water.IsChecked==true,fill.IsChecked==true);
    }

    private async Task ShowOtherGameLevelReadyAsync(string name,string slotName,string summary,bool filled)
    {
        if(OtherGameLevelChoiceForTesting!=null||CrashLevelChoiceForTesting!=null)return;
        var content=new StackPanel{Margin=new Thickness(22),Spacing=12};
        content.Children.Add(new TextBlock{Text=$"{name} is ready to build on",FontSize=20,FontWeight=FontWeight.SemiBold,TextWrapping=TextWrapping.Wrap});
        content.Children.Add(new TextBlock{Name="OtherGameLevelSummary",TextWrapping=TextWrapping.Wrap,Text=summary});
        content.Children.Add(new TextBlock{TextWrapping=TextWrapping.Wrap,Text=filled
            ?"Next, play it through once and move anything that sits oddly, then add enemies and boxes with Add Object. Game Settings sets the music and sky."
            :$"To make it a full level, open Add Object and add {slotName}'s dragons and egg thief (the line under the treasure counts them), "+
             "a Return Home vortex near the end, gems along the paths, and some enemies. File ▾ > Fill In What the Level Needs… does the first part for you. Game Settings sets the music and sky."});
        var ok=NewButton("OK");ok.Name="OtherGameLevelReadyOk";StyleModernPrimaryButton(ok,ModernBlue,100);ok.HorizontalAlignment=HorizontalAlignment.Right;
        content.Children.Add(ok);
        var dialog=new Window{Name="OtherGameLevelReadyDialog",Title="New Level",Width=520,SizeToContent=SizeToContent.Height,CanResize=false,
            WindowStartupLocation=WindowStartupLocation.CenterOwner,Content=content};
        ok.Click+=(_,_)=>dialog.Close();
        await dialog.ShowDialog(this);
    }
}
