using System.Buffers.Binary;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.App.Views;

public sealed partial class MainWindow
{
    private const string FlightGateReplacementRecipe = "flight-gate-replacement:";
    private Button? _replaceFlightGateButton;
    private sealed record FlightGateChoice(string SourceKey,int SourceRow,string Name,int Seconds)
    {
        public override string ToString() => $"{Name} (+{Seconds} seconds)";
    }
    private static bool IsFlightGate(Moby m) => m.SourceByte36==0x61 && m.SourceByte37==1;
    private static bool IsFlightGateReplacement(Moby m) => m.IsAdded && IsFlightGate(m) &&
        m.CrossLevelRequiredExporterFeature==UniversalMobyExport.RequiredExporterFeature &&
        m.CrossLevelRecipeId.StartsWith(FlightGateReplacementRecipe,StringComparison.Ordinal);
    private bool CanReplaceSelectedFlightGate() => _currentLevel!=null && _freshSceneDocument==null &&
        _currentLevel.Key is "sunnyflight" or "nightflight" or "crystalflight" or "wildflight" &&
        _selectedMoby is { IsAdded:false,IsRemoved:false,IsEditorControl:false } m && IsFlightGate(m);
    private Button BuildReplaceFlightGateButton()
    {
        _replaceFlightGateButton=NewAsyncButton("Replace Flight gate…",ShowFlightGateReplacementAsync);
        _replaceFlightGateButton.Name="ReplaceFlightGateButton";
        StyleObjectEditButton(_replaceFlightGateButton);
        ToolTip.SetTip(_replaceFlightGateButton,"Replace a selected course gate while keeping its position and objective.");
        return _replaceFlightGateButton;
    }
    private void RefreshFlightGateReplacementAction()
    {
        if(_replaceFlightGateButton==null)return;
        _replaceFlightGateButton.IsVisible=CanReplaceSelectedFlightGate();
        _replaceFlightGateButton.IsEnabled=CanReplaceSelectedFlightGate();
    }
    private static int Raw(float value)=>checked((int)MathF.Round(value*16));
    private void RequireFlightGateReplacementTarget(UniversalSourceLibrary library,Moby target)
    {
        if(_currentLevel==null || _freshSceneDocument!=null || target.IsAdded || target.IsRemoved ||
            !IsFlightGate(target) || !_currentMobys.Contains(target) || target.TrueIndex<0)
            throw new InvalidDataException("Select an original Flight gate to replace.");
        var original=library.Get(_currentLevel.Key).Data.Row(target.TrueIndex);
        if(BitConverter.ToUInt16(original,0x36)!=0x161 || Raw(target.Position.X)!=BitConverter.ToInt32(original,12) ||
            Raw(target.Position.Y)!=BitConverter.ToInt32(original,16) || Raw(target.Position.Z)!=BitConverter.ToInt32(original,20) ||
            target.YawByte!=original[0x46] || target.HasAnyEdit)
            throw new InvalidDataException("Undo edits to this gate before replacing it. Its original position is required.");
        if(_currentMobys.Any(m=>m.IsAdded&&!m.IsRemoved) || CurrentRemovedNativeRows().Count!=0 ||
            _currentMobys.Any(m=>!m.IsAdded&&!m.IsRemoved&&m.HasAnyEdit))
            throw new InvalidDataException("This replacement needs a course with no other object edits.");
    }
    private IReadOnlyList<FlightGateChoice> FlightGateChoices(UniversalSourceLibrary library,Moby target)
    {
        RequireFlightGateReplacementTarget(library,target);
        int category=BitConverter.ToInt32(library.Get(_currentLevel!.Key).Data.CaptureRow(_currentLevel.Key,target.TrueIndex).Properties,24);
        List<FlightGateChoice> choices=[];
        foreach(string key in new[]{"sunnyflight","nightflight","crystalflight","wildflight"})
        {
            var s=library.Get(key);
            int row=Enumerable.Range(0,s.Data.Scene().RowCount).First(i=>BitConverter.ToUInt16(s.Data.Row(i),0x36)==0x161);
            var props=s.Data.CaptureRow(key,row).Properties;
            if(BitConverter.ToInt32(props,24)==category)choices.Add(new(key,row,s.Level.DisplayName,BitConverter.ToInt32(props,20)));
        }
        return choices;
    }
    private UniversalMobyPlacement FlightGateReplacementPlacement(UniversalSourceLibrary library,Moby target,FlightGateChoice choice,string identity,int slot)
    {
        var row=library.Get(choice.SourceKey).Data.Row(choice.SourceRow);
        return new(choice.SourceKey,choice.SourceRow,Raw(target.Position.X),Raw(target.Position.Y),Raw(target.Position.Z),
            Yaw:target.YawByte,RowByteOverrides:new Dictionary<int,byte>{{0x50,row[0x50]},{0x51,row[0x51]}},EditorId:identity,PersistentSlot:slot);
    }
    private async Task<(UniversalComposeResult? Result,string? Error)> PreflightFlightGateReplacementAsync(
        UniversalSourceLibrary library,Moby target,FlightGateChoice choice,string identity)
    {
        RequireFlightGateReplacementTarget(library,target);
        var existing=CurrentUniversalPlacements();
        var placement=FlightGateReplacementPlacement(library,target,choice,identity,existing.Count);
        IReadOnlyList<UniversalMobyPlacement> trial=existing.Append(placement).ToArray();
        var removed=new[]{target.TrueIndex};
        var captured=await CaptureUniversalRetailHostAsync(library,_currentLevel!,trial,removed,null);
        return await Task.Run(()=>captured.Host.DryRun(library,trial,null,removed));
    }
    private async Task ShowFlightGateReplacementAsync()
    {
        if(!CanReplaceSelectedFlightGate()){_statusText.Text="Select an original Flight gate to replace.";return;}
        Moby target=_selectedMoby!;LevelDefinition level=_currentLevel!;
        Window dialog=new(){Title="Replace Flight gate",Width=500,Height=300,CanResize=false,WindowStartupLocation=WindowStartupLocation.CenterOwner};
        var source=new ComboBox{Name="FlightGateReplacementSource",MinHeight=34};
        var note=NewSmallNote("Choose a replacement gate. It keeps this position and counts toward the same objective. Undo Object restores the original gate.");
        var fit=NewSmallNote("Checking this course…");fit.Name="FlightGateReplacementFit";
        var replace=NewButton("Replace gate");replace.Name="FlightGateReplacementConfirm";replace.IsEnabled=false;StyleModernPrimaryButton(replace,ModernGreen);
        var cancel=NewButton("Cancel");cancel.Name="FlightGateReplacementCancel";StyleModernSecondaryButton(cancel);cancel.Click+=(_,_)=>dialog.Close();
        var buttons=new StackPanel{Orientation=Orientation.Horizontal,Spacing=10};buttons.Children.Add(replace);buttons.Children.Add(cancel);
        var panel=new StackPanel{Margin=new Thickness(20),Spacing=14};panel.Children.Add(note);panel.Children.Add(source);panel.Children.Add(fit);panel.Children.Add(buttons);dialog.Content=panel;
        UniversalSourceLibrary? library=null;string identity=Guid.NewGuid().ToString("N");long revision=0;bool closed=false,busy=false;
        dialog.Closed+=(_,_)=>{closed=true;revision++;};
        async Task CheckFit()
        {
            long current=++revision;replace.IsEnabled=false;
            if(library==null||source.SelectedItem is not FlightGateChoice choice)return;
            fit.Text="Checking this replacement…";
            try
            {
                var check=await PreflightFlightGateReplacementAsync(library,target,choice,identity);
                if(closed||current!=revision)return;
                fit.Text=check.Error??"Ready. Replace this gate at its original position?";
                replace.IsEnabled=check.Error==null;
            }
            catch(Exception e) when(e is IOException or InvalidDataException or InvalidOperationException)
            {if(!closed&&current==revision)fit.Text=e.Message;}
        }
        source.SelectionChanged+=async(_,_)=>await CheckFit();
        replace.Click+=async(_,_)=>
        {
            if(busy||library==null||source.SelectedItem is not FlightGateChoice choice)return;
            busy=true;replace.IsEnabled=false;source.IsEnabled=false;cancel.IsEnabled=false;
            try
            {
                if(!ReferenceEquals(_currentLevel,level)||!ReferenceEquals(_selectedMoby,target))throw new InvalidDataException("The selected course changed. Open Replace Flight gate again.");
                var check=await PreflightFlightGateReplacementAsync(library,target,choice,identity);
                if(check.Error!=null)throw new InvalidDataException(check.Error);
                RequireFlightGateReplacementTarget(library,target);
                CommitFlightGateReplacement(library,target,choice,identity);
                dialog.Close();
            }
            catch(Exception e) when(e is IOException or InvalidDataException or InvalidOperationException)
            {fit.Text=e.Message;source.IsEnabled=true;cancel.IsEnabled=true;busy=false;await CheckFit();}
        };
        dialog.Opened+=async(_,_)=>
        {
            try
            {
                library=(await EnsureUniversalCatalogAsync()).Item2;
                var choices=FlightGateChoices(library,target);source.ItemsSource=choices;
                source.SelectedItem=choices.FirstOrDefault(c=>c.SourceKey!=level.Key)??choices.FirstOrDefault();
            }
            catch(Exception e) when(e is IOException or InvalidDataException or InvalidOperationException){fit.Text=e.Message;}
        };
        await dialog.ShowDialog(this);
    }
    private void CommitFlightGateReplacement(UniversalSourceLibrary library,Moby target,FlightGateChoice choice,string identity)
    {
        RequireFlightGateReplacementTarget(library,target);
        byte[] row=library.Get(choice.SourceKey).Data.Row(choice.SourceRow);
        var template=new AddMobyTemplate(Name:"Flight gate",Type:row[0x50],State:row[0x51],
            SourceByte36:row[0x36],SourceByte37:row[0x37],SourceByte4F:row[0x4F],Flag4A:row[0x52],Flag4B:row[0x53],UsesGem:false,
            DefaultLabel:"Flight gate",FromCrossLevelTemplate:true,DefaultColor:Moby.ColorForType(row[0x50]),
            TemplateId:$"universal:{choice.SourceKey}:{choice.SourceRow}",Family:"universal",SourceLevelKey:choice.SourceKey,
            SourceLevelName:choice.Name,SourceTrueIndex:choice.SourceRow,AddSupportStatus:UniversalMobyExport.RequiredExporterFeature,
            RequiredExporterFeature:UniversalMobyExport.RequiredExporterFeature,TemplateNote:"Keeps the original course gate position and objective.",
            CandidateKind:"Interactive",CurrentLevelSupportLabel:"importable",CurrentLevelReady:true,CurrentLevelPlaceable:true,YawByte:target.YawByte);
        int index=_currentMobys.Count==0?0:_currentMobys.Max(m=>m.Index)+1;
        int trueIndex=_currentMobys.Count==0?0:_currentMobys.Max(m=>m.TrueIndex)+1;
        var added=CreateAddedMobyFromTemplate(template,"Flight gate",target.Position,index,trueIndex,template.Type,template.State,
            template.SourceByte36,template.SourceByte4F,template.Flag4B,target.YawByte,GemValue.Red);
        added.UniversalPlacementId=identity;added.CrossLevelRecipeId=FlightGateReplacementRecipe+_currentLevel!.Key+":"+target.TrueIndex;
        target.IsRemoved=true;_currentMobys.Add(added);_lastRemovedMoby=target;_lastRemovedMobyBundle=[target];
        RefreshFlightGateReplacementScene(added);
        _statusText.Text="Replaced the Flight gate. Its position is locked. Undo Object restores the original gate.";
    }
    private void RefreshFlightGateReplacementScene(Moby selected)
    {
        InvalidateBuildSafetySummary();_selectedMoby=selected;_viewport.Mobys=_currentMobys;
        RefreshMobyList(selected);RefreshCurrentLevelDetails();_viewport.SelectMoby(selected,focus:false);RefreshDiagnosticContext();
    }
    private bool UndoFlightGateReplacement(Moby selected)
    {
        var added=!selected.IsRemoved&&IsFlightGateReplacement(selected)?selected:_currentMobys.SingleOrDefault(m=>!m.IsRemoved&&IsFlightGateReplacement(m)&&
            m.CrossLevelRecipeId==FlightGateReplacementRecipe+_currentLevel?.Key+":"+selected.TrueIndex);
        if(added==null)return false;
        string prefix=FlightGateReplacementRecipe+_currentLevel?.Key+":";
        if(!added.CrossLevelRecipeId.StartsWith(prefix,StringComparison.Ordinal)||!int.TryParse(added.CrossLevelRecipeId[prefix.Length..],out int row))return false;
        var target=_currentMobys.SingleOrDefault(m=>!m.IsAdded&&m.TrueIndex==row&&m.IsRemoved&&IsFlightGate(m));
        if(target==null)return false;
        added.IsRemoved=true;target.UndoEdits();_lastRemovedMoby=null;_lastRemovedMobyBundle.Clear();
        RefreshFlightGateReplacementScene(target);_statusText.Text="Restored the original Flight gate.";return true;
    }
    private bool BlockFlightGateReplacementEdit(Moby moby)
    {
        if(!IsFlightGateReplacement(moby))return false;
        _statusText.Text="This gate keeps its original position. Use Undo Object to restore the original gate.";return true;
    }
}
