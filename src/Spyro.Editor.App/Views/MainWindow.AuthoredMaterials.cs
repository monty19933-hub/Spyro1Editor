using System.Buffers.Binary;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.App.Views;

public sealed partial class MainWindow
{
    private async Task ChooseFreshMaterialAsync()
    {
        if(_freshSceneDocument==null||_selectedTerrain==null||_selectedTerrain.IsTerrainRemoved)
        { _statusText.Text="Select a terrain face before choosing its material.";return; }
        var owner=_freshSceneDocument;var face=_selectedTerrain;
        var(_,sources,_)=await EnsureUniversalCatalogAsync();
        if(_freshSceneDocument?.Id!=owner.Id)return;
        var levels=_retailCatalog.Levels.Where(l=>l.SourceWadEntry is >=10 and <=78).ToArray();
        var existing=owner.Surfaces[face.SectorIndex].Material;
        ComboBox level=new(){Name="AuthoredMaterialLevel",ItemsSource=levels,SelectedItem=levels.FirstOrDefault(l=>l.SourceWadEntry==existing?.SourceWadEntry)??levels[0]};
        ComboBox material=new(){Name="AuthoredMaterialChoice"};
        Image preview=new(){Name="AuthoredMaterialPreview",Height=180,Stretch=Avalonia.Media.Stretch.Uniform};
        TextBlock note=new(){Text="Choose a material from an original level.",TextWrapping=Avalonia.Media.TextWrapping.Wrap};
        Button apply=NewButton("Apply material");apply.Name="AuthoredMaterialApply";apply.IsEnabled=false;
        Window dialog=new(){Title="Choose terrain material",Width=450,Height=380,WindowStartupLocation=WindowStartupLocation.CenterOwner};
        Bitmap? bitmap=null;int revision=0;
        async Task RefreshPreview()
        {
            int request=++revision;apply.IsEnabled=false;
            if(level.SelectedItem is not LevelDefinition source||material.SelectedItem is not AuthoredMaterialChoice choice)return;
            try
            {
                string original=ResolveUniversalSourceDisc();
                await Task.Run(()=>NativeStaticTerrainTextureSource.Capture(original,source.SourceWadEntry,choice.Id));
                string path=Path.Combine(_workspace.RootPath,"_local","authored-materials",$"wad-{source.SourceWadEntry}-t-{choice.Id}.png");
                if(!File.Exists(path))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    var result=await TerrainPatchExporter.TryExportTerrainTextureImageTierAsync(original,source,choice.Id,path,"hqData");
                    if(result==null)throw new InvalidDataException("This texture has no static material preview.");
                }
                if(request!=revision)return;
                var next=new Bitmap(path);preview.Source=next;bitmap?.Dispose();bitmap=next;
                note.Text="This is the original game material. It will be copied into your level.";apply.IsEnabled=true;
            }
            catch(Exception error)when(error is IOException or InvalidDataException or ArgumentException)
            {if(request==revision){preview.Source=null;bitmap?.Dispose();bitmap=null;note.Text=error.Message;}}
        }
        void RefreshChoices()
        {
            if(level.SelectedItem is not LevelDefinition source)return;
            var data=sources.Get(source.Key).Item2;var part=data.Subfiles()[1];
            int count=BinaryPrimitives.ReadInt32LittleEndian(data.Bytes.AsSpan(part.Offset+4));
            if(count is <0 or >256)throw new InvalidDataException("The original material table has an invalid count.");
            material.ItemsSource=Enumerable.Range(0,count).Select(i=>new AuthoredMaterialChoice(i)).ToArray();
            material.SelectedIndex=existing?.SourceWadEntry==source.SourceWadEntry&&existing.TextureId<count?existing.TextureId:0;
        }
        level.SelectionChanged+=(_,_)=>RefreshChoices();
        material.SelectionChanged+=async(_,_)=>await RefreshPreview();
        apply.Click+=(_,_)=>
        {
            if(_freshSceneDocument?.Id!=owner.Id||!_currentGeometry!.Polygons.Contains(face))return;
            if(level.SelectedItem is LevelDefinition source&&material.SelectedItem is AuthoredMaterialChoice choice)
            {
                ApplyFreshMaterial(face,new(source.SourceWadEntry,choice.Id));
                _statusText.Text="Material applied. Save Level records it with your terrain.";dialog.Close();
            }
        };
        Button cancel=NewButton("Cancel",()=>dialog.Close());
        dialog.Content=new StackPanel{Spacing=10,Margin=new(16),Children={level,material,preview,note,
            new StackPanel{Orientation=Orientation.Horizontal,Spacing=8,HorizontalAlignment=HorizontalAlignment.Right,Children={cancel,apply}}}};
        dialog.Closed+=(_,_)=>{revision++;preview.Source=null;bitmap?.Dispose();};
        RefreshChoices();await dialog.ShowDialog(this);
    }
    private sealed record AuthoredMaterialChoice(int Id)
    {public override string ToString()=>"Material "+(Id+1);}
}
