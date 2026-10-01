using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Universal;

int checks=0;
void Check(bool condition,string name)
{checks++;if(!condition)throw new InvalidOperationException(name);}
SceneDocument Retail(string key,int id,int entry)=>SceneDocument.New(key) with
{
    TargetBinding=new(key,id,entry,entry-1,UniversalSourceLibrary.OriginalUsaSha256,0)
};
var town=Retail("townsquare",13,16);
var artisans=Retail("artisans",10,10);
var fresh=SceneDocument.New();
var catalog=new LevelCatalog(new[]{
    new LevelDefinition{Key="townsquare",LevelId=13,SourceWadEntry=16},
    new LevelDefinition{Key="artisans",LevelId=10,SourceWadEntry=10}});
string root=Path.Combine(Path.GetTempPath(),"spyro-authored-binding-conflict-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
string output=Path.Combine(root,"existing.bin"),cue=Path.ChangeExtension(output,".cue"),workspace=Path.Combine(root,"workspace");
byte[] sentinel=[9,3,1,7];
File.WriteAllBytes(output,sentinel);File.WriteAllBytes(cue,sentinel);
try
{
    foreach(var documents in new[]{new[]{town,fresh},new[]{fresh,town},new[]{artisans,town,fresh},
        new[]{fresh,artisans,town},new[]{town with{Name="Renamed retail"},fresh with{Name="Renamed fresh"}},
        new[]{town with{Surfaces=[]},fresh}})
    {
        string[] before=documents.Select(d=>d.Fingerprint()).ToArray();
        bool guarded=false;
        try
        {
            // A null donor is deliberate: the ownership refusal must happen
            // before any donor access, geometry validation or artifact staging.
            await NativeAuthoredProjectExporter.ExportAsync(null!,catalog,
                documents.Select(d=>new NativeAuthoredExportScene(d,Array.Empty<UniversalMobyPlacement>())).ToArray(),
                null,output,workspace,10);
        }
        catch(InvalidDataException e) when(e.Message.StartsWith("Town Square and the new level cannot be exported together",StringComparison.Ordinal))
        {guarded=true;}
        Check(guarded,"shared Town/65 callback owner refused before donor access");
        Check(!Directory.Exists(workspace),"refusal creates no workspace or scratch artifacts");
        Check(File.ReadAllBytes(output).AsSpan().SequenceEqual(sentinel)&&File.ReadAllBytes(cue).AsSpan().SequenceEqual(sentinel),
            "existing BIN/CUE preserved");
        Check(documents.Select(d=>d.Fingerprint()).SequenceEqual(before),"source documents unchanged");
    }
    foreach(var documents in new[]{new[]{town},new[]{fresh},new[]{artisans,fresh},new[]{artisans,town}})
    {
        bool reachedDonor=false;
        try
        {
            await NativeAuthoredProjectExporter.ExportAsync(null!,catalog,
                documents.Select(d=>new NativeAuthoredExportScene(d,Array.Empty<UniversalMobyPlacement>())).ToArray(),
                null,output,workspace,10);
        }
        catch(NullReferenceException){reachedDonor=true;}
        Check(reachedDonor,"independent target selections pass the early ownership guard; no composition claim");
        Check(!Directory.Exists(workspace),"donor-free boundary leaves artifacts unchanged");
    }
}
finally{Directory.Delete(root,true);}
Console.WriteLine("NATIVE_AUTHORED_AGGREGATE_OWNERSHIP_CHECKS_OK checks="+checks);
