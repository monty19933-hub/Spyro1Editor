using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Spyro.Editor.Core.Crash;
using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.App.Views;

/// <summary>File ▾ > New Level from Crash Bandicoot: Warped…: a Crash level's path (its ground, walls, pits, textures and
/// backdrop) rebuilt from the user's own disc in the open level's place (<see cref="CrashLevelImport"/>), through the same
/// steps as a Year of the Dragon level.</summary>
public sealed partial class MainWindow
{
    /// <summary><paramref name="Part"/>: 0 the whole level, 1 its first half, 2 its second half (long levels).</summary>
    internal sealed record CrashLevelChoice(CrashLevel Level, int Part, bool DeadlyPits, bool InvisibleWalls, bool FillIn = true);
    /// <summary>Answers the Crash level dialog in tests (null: cancel).</summary>
    internal Func<CrashLevelChoice?>? CrashLevelChoiceForTesting { get; set; }

    private async Task NewLevelFromCrashAsync()
    {
        if(!ReadyForOtherGameLevel())return;
        var slot=_currentLevel!;string root=_workspace.RootPath;
        var choice=CrashLevelChoiceForTesting!=null?CrashLevelChoiceForTesting():await ShowCrashLevelDialogAsync(slot.DisplayName,OwnerLevelName(slot));
        if(choice==null||_currentLevel!=slot||_workspace.RootPath!=root)return;
        string disc=CrashWarpedDiscLocator.Find(_workspace);
        if(!CrashWarpedDiscLocator.IsUsable(disc)){_statusText.Text="Choose your Crash Bandicoot: Warped disc first.";return;}
        var level=choice.Level;var catalog=_retailCatalog;
        string part=choice.Part switch{1=>" (first half)",2=>" (second half)",_=>""};
        await NewLevelFromOtherGameAsync(slot,new("Crash Bandicoot: Warped",level.Name+part,CrashLevelImport.Slug(level.Name+part),choice.FillIn,
            async(binding,usa,pictures,progress,token)=>
            {
                var r=await Task.Run(async()=>
                {
                    int? first=null,last=null;
                    if(choice.Part!=0)
                    {
                        int zones=CrashLevels.Read(new CrashWarpedDisc(disc),level).MainZones.Count;
                        (first,last)=choice.Part==1?(0,zones/2-1):(zones/2,zones-1);
                    }
                    return await CrashLevelImport.ImportAsync(disc,level,usa,catalog,binding,
                        new(){PictureFolder=pictures,FirstZone=first,LastZone=last,DeadlyPits=choice.DeadlyPits,InvisibleWalls=choice.InvisibleWalls},progress,token);
                },token);
                return (r.ApplyTo,r.Scene.Name,PlainCrashSummary(r.Report));
            }));
    }

    /// <summary>The Crash import's report in everyday words.</summary>
    private static string PlainCrashSummary(CrashLevelImportReport r)
    {
        List<string> lines=[$"{Count(r.Faces,"terrain tile","terrain tiles")}, {Count(r.KeptPictures,"picture","pictures")}"+
            (r.Pictures>r.KeptPictures?$" ({r.Pictures-r.KeptPictures} similar ones merged to fit the game's texture memory)":"")+"."];
        if(r.WallFaces>0)lines.Add($"Invisible walls run along Crash's path ({r.WallLength:N0} units), so Spyro stays on it.");
        if(r.PitFloors>0)lines.Add($"{Count(r.PitFloors,"pit","pits")} kill Spyro as they kill Crash.");
        lines.Add("Spyro starts where Crash starts"+(r.RouteLength>0?$"; the way to the end is {r.RouteLength:N0} units long.":"."));
        lines.AddRange(r.Warnings.Select(w=>"Note: "+w));
        return string.Join("\n",lines);
    }

    private async Task<CrashLevelChoice?> ShowCrashLevelDialogAsync(string levelName,string slotName)
    {
        var content=new StackPanel{Margin=new Thickness(22),Spacing=12};
        content.Children.Add(new TextBlock{Text=$"Make {levelName} into a Crash Bandicoot: Warped level?",FontSize=20,FontWeight=FontWeight.SemiBold,TextWrapping=TextWrapping.Wrap});
        var levels=CrashLevels.All.ToList();
        var pick=new ComboBox{Name="CrashLevelChoice",HorizontalAlignment=HorizontalAlignment.Stretch,ItemsSource=levels.Select(l=>l.Name).ToArray(),SelectedIndex=0};
        var notes=new TextBlock{Name="CrashLevelNotes",TextWrapping=TextWrapping.Wrap,Foreground=new SolidColorBrush(Color.FromRgb(90,98,110))};
        var part=new ComboBox{Name="CrashLevelPart",HorizontalAlignment=HorizontalAlignment.Stretch,ItemsSource=new[]{"The whole level","Its first half","Its second half"},SelectedIndex=0};
        void Notes()
        {
            if(pick.SelectedIndex<0)return;
            var l=levels[pick.SelectedIndex];notes.Text=char.ToUpperInvariant(l.Notes[0])+l.Notes[1..]+".";
            // Long levels hold more than a Spyro level can; they come in halves.
            if(l.Notes.Contains("needs a part",StringComparison.Ordinal)&&part.SelectedIndex==0)part.SelectedIndex=1;
        }
        pick.SelectionChanged+=(_,_)=>Notes();Notes();
        content.Children.Add(pick);content.Children.Add(notes);content.Children.Add(part);
        content.Children.Add(new TextBlock{Name="CrashLevelExplanation",TextWrapping=TextWrapping.Wrap,Text=
            $"Crash's path, its ground, walls, pits and the scenery around it are rebuilt from your Crash Bandicoot: Warped disc and take the place of {levelName}'s terrain and objects. "+
            $"A recovery copy of {levelName} is saved first: File ▾ > Restore Before Clear… brings it back.\n"+
            "Building takes about a minute. Crash's boxes and enemies are not brought over (Add Object has them under Crash Bandicoot)."});
        var pits=new CheckBox{Name="CrashDeadlyPits",Content="Pits kill Spyro, as they kill Crash",IsChecked=true};
        var walls=new CheckBox{Name="CrashInvisibleWalls",Content="Invisible walls keep Spyro on Crash's path",IsChecked=true};
        var fill=new CheckBox{Name="CrashFillIn",Content=$"Then put in {slotName}'s dragons and egg thieves, Return Home and gems (you can move them afterwards)",IsChecked=true};
        content.Children.Add(pits);content.Children.Add(walls);content.Children.Add(fill);
        var discNote=new TextBlock{Name="CrashDiscNote",TextWrapping=TextWrapping.Wrap,Foreground=new SolidColorBrush(Color.FromRgb(168,98,0))};
        var chooseDisc=NewButton("Choose Crash disc…");chooseDisc.Name="CrashChooseDisc";
        var actions=new StackPanel{Orientation=Orientation.Horizontal,Spacing=10,HorizontalAlignment=HorizontalAlignment.Right};
        Button cancel=NewButton("Cancel"),confirm=NewButton("Make Level");
        cancel.Name="CrashLevelCancel";confirm.Name="CrashLevelConfirm";
        void RefreshDisc()
        {
            bool ok=CrashWarpedDiscLocator.IsUsable(CrashWarpedDiscLocator.Find(_workspace));
            discNote.Text=ok?"":"This needs your Crash Bandicoot: Warped (USA) disc image. Nothing from that game ships with the editor.";
            discNote.IsVisible=chooseDisc.IsVisible=!ok;confirm.IsEnabled=ok;
        }
        chooseDisc.Click+=async(_,_)=>
        {
            var files=await StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
                {Title="Select your Crash Bandicoot: Warped (USA) disc image (.bin)",AllowMultiple=false});
            if(files.FirstOrDefault()?.Path.LocalPath is not {} picked)return;
            try{string bin=DiscImageLocator.ResolveImagePath(picked);await Task.Run(()=>CrashWarpedDiscLocator.Configure(_workspace,bin));}
            catch(Exception e)when(e is IOException or InvalidDataException or UnauthorizedAccessException){discNote.Text=e.Message;return;}
            RefreshDisc();
        };
        content.Children.Add(discNote);content.Children.Add(chooseDisc);
        var dialog=new Window{Name="CrashLevelDialog",Title="New Level from Crash Bandicoot: Warped",Width=560,SizeToContent=SizeToContent.Height,CanResize=false,
            WindowStartupLocation=WindowStartupLocation.CenterOwner,Content=content};
        cancel.Click+=(_,_)=>dialog.Close(false);confirm.Click+=(_,_)=>dialog.Close(true);
        StyleModernPrimaryButton(confirm,ModernBlue,132);actions.Children.Add(cancel);actions.Children.Add(confirm);content.Children.Add(actions);
        RefreshDisc();
        if(!await dialog.ShowDialog<bool>(this)||pick.SelectedIndex<0)return null;
        return new(levels[pick.SelectedIndex],Math.Max(0,part.SelectedIndex),pits.IsChecked==true,walls.IsChecked==true,fill.IsChecked==true);
    }
}
