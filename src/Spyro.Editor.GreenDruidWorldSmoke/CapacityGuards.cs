using Spyro.Editor.Core.Editing;using Spyro.Editor.Core.Exporting;using Spyro.Editor.Core.Universal;
internal static class CapacityGuards
{
 internal static async Task<int> Run(string original,string dir,Spyro.Editor.Core.Levels.LevelCatalog catalog,UniversalSourceLibrary lib,NativeClearedLevelCompilation normal,IReadOnlyList<UniversalMobyPlacement> placements)
 {
  // A genuine121-quad native authored host independently fits its own queue;
  // adding the source22 faces must refuse the combined all-visible budget.
  var surfaces=new List<AuthoredSurface>();for(int x=0;x<11;x++)for(int y=0;y<11;y++){float a=1000+x*240,b=1000+y*240;surfaces.Add(new(Guid.NewGuid().ToString("N"),[new(a,b,416),new(a+240,b,416),new(a+240,b+240,416),new(a,b+240,416)],new(128,112,80),new(10,66)));}
  var document=normal.Document with{Surfaces=surfaces};var full=await NativeClearedLevelCompilation.CaptureAsync(original,document,catalog,Path.Combine(dir,"capacity-baseline.bin"));try{_=full.Compose(lib,placements);}catch(InvalidDataException e)when(e.Message.Contains("combined all-visible native queue partitions")){return 1;}throw new Exception("Combined native queue overflow was accepted or failed without its actual owner.");
 }
}
