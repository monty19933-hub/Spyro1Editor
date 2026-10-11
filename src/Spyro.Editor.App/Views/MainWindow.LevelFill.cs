using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.App.Views;

/// <summary>File ▾ > Fill In What the Level Needs…: in a level you built, adds what finishing it needs and the line under
/// its treasure counts (its own dragons, egg thieves with a run, Return Home) and gems, on floor Spyro can walk to
/// (<see cref="LevelFill"/>). Everything it adds is an ordinary added object: drag it, turn it or remove it.</summary>
public sealed partial class MainWindow
{
    internal sealed record LevelFillChoice(bool Dragons, bool Thieves, bool Exit, int GemTotal, bool Creatures = true);
    /// <summary>Save rows Fill In leaves free for objects you add yourself.</summary>
    private const int LevelFillSpareRows = 6;
    /// <summary>Answers the fill dialog in tests (null: cancel).</summary>
    internal Func<LevelFillChoice?>? LevelFillChoiceForTesting { get; set; }

    private Task FillInLevelAsync() => FillInLevelAsync(null);

    /// <summary><paramref name="preset"/>: fill in without asking (after New Level from Year of the Dragon); a negative gem
    /// total means the original level's treasure (none when the level already has gems).</summary>
    private async Task FillInLevelAsync(LevelFillChoice? preset)
    {
        if(_startBlankBusy||_createBinBusy||_regularEditorPersistenceBusy||_workspaceTransitionBusy||_loadingLevel)return;
        if(_freshSceneDocument==null||_currentLevel==null||_currentGeometry==null)
        { _statusText.Text="Fill In works in a level you built: New Level, Start Blank or New Level from Year of the Dragon. Open one first.";return; }
        var level=_currentLevel;string root=_workspace.RootPath;
        _statusText.Text="Reading the original disc's objects…";
        UniversalSourceLibrary lib;IReadOnlyList<UniversalMobyCatalogEntry> entries;
        try{(_,lib,entries)=await EnsureUniversalCatalogAsync();entries=[.. entries,.. UniversalSpecialCatalog.Entries(false).Where(e=>UniversalHighValueGems.IsClass(e.ClassId))];}
        catch(Exception e)when(e is IOException or InvalidDataException or UnauthorizedAccessException)
        { _statusText.Text="Fill In needs the original Spyro the Dragon disc: "+e.Message;return; }
        if(_currentLevel!=level||_workspace.RootPath!=root||_freshSceneDocument==null)return;
        string? key=_freshSceneDocument.TargetBinding?.LevelKey;
        var slot=key==null?null:_retailCatalog.FindByKey(key);
        var own=slot is {SourceWadEntry:>0}?LevelCompletionCheck.OriginalRows(lib.Get(key!).Data):LevelOwnRows.None;
        var live=_currentMobys.Where(m=>m.IsAdded&&!m.IsRemoved).ToList();
        bool Has(int row)=>live.Any(m=>string.Equals(m.CrossLevelSourceLevelKey,key,StringComparison.OrdinalIgnoreCase)&&m.CrossLevelSourceTrueIndex==row);
        var dragonRows=own.Dragons.Where(r=>!Has(r)).ToList();
        var thiefRows=own.Thieves.Where(r=>!Has(r)).ToList();
        var gap=LevelCompletionCheck.Check(own,key??"",true,[],live.Select(m=>(m.CrossLevelSourceLevelKey,m.CrossLevelSourceTrueIndex)),LevelCompletionCheck.SourceClass(lib));
        bool exitMissing=gap.ExitNeeded&&!gap.ExitPresent;
        bool hasGems=_currentMobys.Any(m=>!m.IsRemoved&&m.TreasureValue>0);
        var scene=CaptureFreshScene();
        int free=Math.Max(0,256-(scene.TargetBinding?.NativePrefixRows??0)-scene.Ledger.Entries.Count);
        string slotName=slot?.DisplayName??level.DisplayName;
        // The original level's treasure (the game's table), as a starting total for the gems.
        int treasure=slot is {LevelId:>=10 and <=64}?System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(lib.Disc.Exe.AsSpan(0x5FC38+2*Spyro.Editor.Core.Exporting.NativeKillHeights.Index(slot.LevelId))):0;
        if(treasure is <=0 or >900)treasure=300;
        var choice=preset??(LevelFillChoiceForTesting!=null?LevelFillChoiceForTesting()
            :await ShowLevelFillDialogAsync(slotName,dragonRows.Count,thiefRows.Count,exitMissing,hasGems?0:treasure,free,scene.GemSpots?.Count??0,
                key!=null&&!live.Any(m=>m.TreasureValue==0&&!IsReleaseProtectedControlMoby(m)&&m.CrossLevelSourceTrueIndex>=0&&!own.Dragons.Concat(own.Thieves).Contains(m.CrossLevelSourceTrueIndex))));
        if(preset is {GemTotal:<0})choice=preset with{GemTotal=hasGems?0:treasure};
        if(choice==null||_currentLevel!=level||_workspace.RootPath!=root||_freshSceneDocument==null)return;

        // The thieves' routes keep their own number of points.
        List<(int Row,UniversalRowTemplate Template,int Nodes)> thieves=[];
        if(choice.Thieves)
            foreach(int row in thiefRows)
            {
                var template=lib.Get(key!).Data.CaptureRow(key!,row);
                thieves.Add((row,template,UniversalPropertyCoordinates.DefaultPath(template,0,0,0)?.Count??0));
            }
        var occupied=_currentMobys.Where(m=>!m.IsRemoved).Select(m=>m.Position).ToList();
        // The level's own kinds of enemies and animals (one of each kind; its template tells its route and what it drops).
        List<(UniversalMobyCatalogEntry Entry,UniversalMobyVariant Variant,UniversalRowTemplate Template)> Kinds(Func<UniversalMobyCatalogEntry,bool> which)=>key==null?[]:
            [.. entries.Where(e=>e.Placeable&&which(e)).SelectMany(e=>e.Variants.Where(v=>string.Equals(v.SourceLevelKey,key,StringComparison.OrdinalIgnoreCase)&&v.RestrictionReason==null).Select(v=>(e,v)))
                .GroupBy(p=>p.v.ClassId).Select(g=>g.First())
                .Select(p=>{try{return (p.e,p.v,(UniversalRowTemplate?)lib.Get(key).Data.CaptureRow(key,p.v.SourceRow));}catch(Exception e)when(e is InvalidDataException or ArgumentException or IndexOutOfRangeException){return (p.e,p.v,null);}})
                .Where(p=>p.Item3!=null).Select(p=>(p.e,p.v,p.Item3!))];
        var foeKinds=choice.Creatures?Kinds(e=>e.Category=="Enemies"&&e.SubCategory!="Egg Thieves"):[];
        var animalKinds=choice.Creatures?Kinds(e=>e.Category=="NPCs"&&e.SubCategory=="Fodder & Animals"):[];
        // Every object takes one of the level's save rows: what finishing needs first, then creatures, then gems, keeping a few spare.
        // A dragon is three objects (statue, pedestal and the dragon).
        int needed=(choice.Dragons?3*dragonRows.Count:0)+thieves.Count+(choice.Exit&&exitMissing?1:0);
        int left=Math.Max(0,free-needed-LevelFillSpareRows);
        int foeCount=foeKinds.Count>0?Math.Min(12,left/4):0,animalCount=animalKinds.Count>0?Math.Min(6,left/8):0;
        var plan=LevelFill.Make(scene,choice.Dragons?dragonRows.Count:0,[.. thieves.Select(t=>t.Nodes)],choice.Exit&&exitMissing,choice.GemTotal,scene.GemSpots,occupied,
            Math.Max(0,left-foeCount-animalCount),foeCount,animalCount);
        // Each creature spot gets the next kind whose route (if it has one) fits this level's floor there, shrunk if need be.
        ((UniversalMobyCatalogEntry,UniversalMobyVariant,UniversalRowTemplate) Kind,List<int[]>? Route)? Fit(IReadOnlyList<(UniversalMobyCatalogEntry,UniversalMobyVariant,UniversalRowTemplate)> kinds,int i,LevelFill.Spot spot)
        {
            for(int k=0;k<kinds.Count;k++)
            {
                var kind=kinds[(i+k)%kinds.Count];
                var row=kind.Item3.Row;
                var home=new Vector3f(BitConverter.ToInt32(row,0x0C)/16f,BitConverter.ToInt32(row,0x10)/16f,BitConverter.ToInt32(row,0x14)/16f);
                var raw=UniversalPropertyCoordinates.DefaultPath(kind.Item3,BitConverter.ToInt32(row,0x0C),BitConverter.ToInt32(row,0x10),BitConverter.ToInt32(row,0x14));
                if(raw==null)return (kind,null);
                if(plan.Grid==null)continue;
                var fitted=LevelFill.FitRoute(plan.Grid,home,[.. raw.Select(n=>new Vector3f(n[0]/16f,n[1]/16f,n[2]/16f))],spot.Position);
                if(fitted!=null)return (kind,[.. raw.Select((n,j)=>{var p=(int[])n.Clone();p[0]=(int)MathF.Round(fitted[j].X*16);p[1]=(int)MathF.Round(fitted[j].Y*16);p[2]=(int)MathF.Round(fitted[j].Z*16);return p;})]);
            }
            return null;
        }
        var foes=plan.Enemies.Select((spot,i)=>(spot,kind:Fit(foeKinds,i,spot))).Where(p=>p.kind!=null).ToList();
        var animals=plan.Animals.Select((spot,i)=>(spot,kind:Fit(animalKinds,i,spot))).Where(p=>p.kind!=null).ToList();
        // What they drop on defeat counts toward the level's treasure, so the gems carry the rest.
        int drops=foes.Concat(animals).Sum(p=>GemValue.TryFromIdByte(p.kind!.Value.Kind.Item3.Row[0x53],out var gem)?gem.Value:0);
        var gems=drops>0&&plan.Gems.Count>0
            ?LevelFill.Values([.. plan.Gems.Select(g=>g.Position)],Math.Max(plan.Gems.Count,choice.GemTotal-drops),scene.Spawn).ToList()
            :plan.Gems.ToList();
        UniversalMobyVariant? Variant(Func<UniversalMobyVariant,bool> match,out UniversalMobyCatalogEntry? entry)
        {
            foreach(var e in entries)foreach(var v in e.Variants)if(match(v)){entry=e;return v;}
            entry=null;return null;
        }
        _startBlankBusy=true;
        List<string> added=[],missing=[];
        try
        {
            async Task<IReadOnlyList<Moby>> Add(Func<UniversalMobyVariant,bool> match,Vector3f at,int yaw,string what)
            {
                if(Variant(match,out var entry) is not {} v){missing.Add(what);return [];}
                return await AddUniversalMobyAtAsync(lib,entry!,v,at,yaw,ResolveUniversalJapaneseSource());
            }
            for(int i=0;i<plan.Dragons.Count;i++)
            {
                _statusText.Text=$"Adding {slotName}'s dragons ({i+1} of {plan.Dragons.Count})…";
                int row=dragonRows[i];var spot=plan.Dragons[i];
                if((await Add(v=>string.Equals(v.SourceLevelKey,key,StringComparison.OrdinalIgnoreCase)&&v.SourceRow==row,spot.Position,spot.Yaw,"dragon")).Count>0)added.Add("dragon");
            }
            for(int i=0;i<plan.Thieves.Count;i++)
            {
                _statusText.Text=$"Adding {slotName}'s egg thief…";
                var (row,template,_)=thieves[i];var spot=plan.Thieves[i];
                var objects=await Add(v=>string.Equals(v.SourceLevelKey,key,StringComparison.OrdinalIgnoreCase)&&v.SourceRow==row,spot.Position,spot.Yaw,"egg thief");
                if(objects.Count==0)continue;
                var thief=objects[0];
                // Its run: the planned loop, each point in the route's own raw form (world units).
                if(UniversalPropertyCoordinates.DefaultPath(template,(int)MathF.Round(thief.Position.X*16),(int)MathF.Round(thief.Position.Y*16),(int)MathF.Round(thief.Position.Z*16)) is {} original&&
                    original.Count==spot.Route.Count)
                    thief.UniversalPathNodes=[.. original.Select((n,k)=>{var p=(int[])n.Clone();p[0]=(int)MathF.Round(spot.Route[k].X*16);p[1]=(int)MathF.Round(spot.Route[k].Y*16);p[2]=(int)MathF.Round(spot.Route[k].Z*16);return p;})];
                added.Add("egg thief");
            }
            if(plan.Exit is {} exit)
            {
                _statusText.Text="Adding Return Home…";
                int ownExit=own.Exits.Count>0?own.Exits[0]:-1;
                var objects=ownExit>=0
                    ?await Add(v=>string.Equals(v.SourceLevelKey,key,StringComparison.OrdinalIgnoreCase)&&v.SourceRow==ownExit,exit.Position,exit.Yaw,"Return Home")
                    :await Add(v=>v.ClassId==LevelCompletionCheck.ReturnHomeClass,exit.Position,exit.Yaw,"Return Home");
                if(objects.Count>0)added.Add("Return Home");
            }
            foreach(var (list,word) in new[]{(foes,"enemy"),(animals,"animal")})
                for(int i=0;i<list.Count;i++)
                {
                    _statusText.Text=$"Adding {(word=="enemy"?"enemies":"animals")} ({i+1} of {list.Count})…";
                    var (spot,kind)=list[i];var (entry,v,_)=kind!.Value.Kind;
                    var objects=await AddUniversalMobyAtAsync(lib,entry,v,spot.Position,spot.Yaw);
                    if(objects.Count==0)continue;
                    if(kind.Value.Route is {} route)objects[0].UniversalPathNodes=route;
                    added.Add(word);
                }
            for(int i=0;i<gems.Count;i++)
            {
                if(i%5==0)_statusText.Text=$"Adding gems ({i+1} of {gems.Count})…";
                var (at,value)=gems[i];
                int cls=value switch{50=>UniversalHighValueGems.TealClass,100=>UniversalHighValueGems.RainbowClass,_=>GemValue.Known.First(g=>g.Value==value).IdByte};
                bool ownGem=Variant(v=>v.ClassId==cls&&string.Equals(v.SourceLevelKey,key,StringComparison.OrdinalIgnoreCase),out _)!=null;
                if((await Add(v=>v.ClassId==cls&&(!ownGem||string.Equals(v.SourceLevelKey,key,StringComparison.OrdinalIgnoreCase)),at,0,"gem")).Count==0)continue;
                added.Add("gem");
            }
        }
        catch(Exception e)when(e is IOException or InvalidDataException or ArgumentException or InvalidOperationException)
        { missing.Add(e.Message); }
        finally{_startBlankBusy=false;RefreshPlacedObjectRoutes();RefreshMobyList(null);RefreshCurrentLevelDetails();}
        int gemCount=added.Count(a=>a=="gem");
        List<string> parts=[];
        foreach(var (what,word) in new[]{("dragon","dragon"),("egg thief","egg thief"),("Return Home","Return Home vortex"),("enemy","enemy"),("animal","animal")})
        { int n=added.Count(a=>a==what);if(n>0)parts.Add(n==1?"1 "+word:$"{n} {word switch{"enemy"=>"enemies","egg thief"=>"egg thieves",_=>word+"s"}}"); }
        if(gemCount>0)parts.Add($"{gemCount} gems worth {gems.Take(gemCount).Sum(g=>g.Value)}"+(drops>0?$" (and {drops} from what the creatures drop)":""));
        _statusText.Text=parts.Count==0
            ?"Nothing was added"+(missing.Count>0?": "+string.Join("; ",missing.Distinct()):".")
            :$"Added {string.Join(", ",parts)}. Drag any of them to move it; Save Level keeps them."+
             (plan.Notes.Count>0?" "+string.Join(" ",plan.Notes):"")+
             
             (missing.Count>0?" Not added: "+string.Join(", ",missing.Distinct())+".":"");
    }

    private async Task<LevelFillChoice?> ShowLevelFillDialogAsync(string slotName,int dragons,int thieves,bool exit,int gemTotal,int freeRows,int gemSpots,bool creatures)
    {
        var content=new StackPanel{Margin=new Thickness(22),Spacing=12};
        content.Children.Add(new TextBlock{Text="Fill in what the level needs?",FontSize=20,FontWeight=FontWeight.SemiBold,TextWrapping=TextWrapping.Wrap});
        content.Children.Add(new TextBlock{Name="LevelFillExplanation",TextWrapping=TextWrapping.Wrap,Text=
            "Places these on open floor Spyro can walk (or glide) to from the start: the dragons spread out, Return Home at the far end, "+
            "the egg thief on open ground with a run around it, and gems along the way"+(gemSpots>0?" (where the original game had its gems)":"")+". "+
            "They are ordinary objects: drag, turn or remove any of them afterwards."});
        CheckBox Box(string name,string text,bool on,bool enabled)=>new(){Name=name,Content=text,IsChecked=on&&enabled,IsEnabled=enabled};
        var d=Box("LevelFillDragons",dragons>0?$"{slotName}'s {(dragons==1?"dragon":dragons+" dragons")} still missing":"All of its dragons are in the level",true,dragons>0);
        var t=Box("LevelFillThieves",thieves>0?$"{slotName}'s {(thieves==1?"egg thief":thieves+" egg thieves")}, with a run":"No egg thief is missing",true,thieves>0);
        var x=Box("LevelFillExit",exit?"A Return Home vortex":"Return Home is already there (or not needed here)",true,exit);
        var g=Box("LevelFillGems","Gems worth about",gemTotal>0,true);
        var total=new NumericUpDown{Name="LevelFillGemTotal",Minimum=10,Maximum=900,Increment=10,Value=gemTotal>0?gemTotal:200,Width=120,FormatString="0"};
        var gemRow=new StackPanel{Orientation=Orientation.Horizontal,Spacing=8};gemRow.Children.Add(g);gemRow.Children.Add(total);
        var a=Box("LevelFillCreatures",$"Enemies and animals of {slotName}'s kinds, spread along the way",creatures,true);
        foreach(var c in new Control[]{d,t,x,a,gemRow})content.Children.Add(c);
        content.Children.Add(new TextBlock{TextWrapping=TextWrapping.Wrap,Foreground=new SolidColorBrush(Color.FromRgb(90,98,110)),Text=
            $"The level has room for {Count(freeRows,"more object","more objects")} (each object takes one of its 256 save rows; a dragon takes three). If the gems don't all fit, fewer gems carry the same total."});
        var actions=new StackPanel{Orientation=Orientation.Horizontal,Spacing=10,HorizontalAlignment=HorizontalAlignment.Right};
        Button cancel=NewButton("Cancel"),confirm=NewButton("Fill In");
        cancel.Name="LevelFillCancel";confirm.Name="LevelFillConfirm";
        var dialog=new Window{Name="LevelFillDialog",Title="Fill In What the Level Needs",Width=540,SizeToContent=SizeToContent.Height,CanResize=false,
            WindowStartupLocation=WindowStartupLocation.CenterOwner,Content=content};
        cancel.Click+=(_,_)=>dialog.Close(false);confirm.Click+=(_,_)=>dialog.Close(true);
        StyleModernPrimaryButton(confirm,ModernBlue,110);actions.Children.Add(cancel);actions.Children.Add(confirm);content.Children.Add(actions);
        if(!await dialog.ShowDialog<bool>(this))return null;
        return new(d.IsChecked==true,t.IsChecked==true,x.IsChecked==true,g.IsChecked==true?(int)(total.Value??0):0,a.IsChecked==true);
    }
}
