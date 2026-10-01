using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Universal;

internal static class RuntimeInteractionGuardCases
{
    internal static Task Run(string image, string workspace, Action<bool,string> check)
    {
        var catalog=LevelCatalog.Load(workspace);
        var library=new UniversalSourceLibrary(image,catalog);
        var destination=library.Get("artisans").Level;
        UniversalMobyPlacement[] failed=[new("alpineridge",1,84992,48960,8960,EditorId:"known-failure",PersistentSlot:0)];
        int[] removed=[29,30,32,33,94,109,113,121,124];
        var result=UniversalMobyCatalogBuilder.DryRun(library,destination,failed,removedRows:removed);
        check(result.Error==null && result.Result!.PlayerAnimationImports.Any(i=>i.AddedAnimationIds.Contains(23)),"Actual source-qualified dry run supplies missing Druid hurt animation: "+result.Error);
        var advice=UniversalCapacityPlanner.Analyze(library,destination,failed,removed);
        check(advice.CurrentError==null && advice.Plans.Count==0,"Already-fitting Druid needs no extra removal plan");
        string scratch=Path.Combine(Path.GetTempPath(),"spyro-runtime-guard-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(scratch);
        try
        {
            string previous=Path.Combine(scratch,"previous.bin"),output=Path.Combine(scratch,"attempt.bin");
            byte[] sentinel=[17,29,31,43];File.WriteAllBytes(previous,sentinel);
            var exported=UniversalMobyExport.Apply(library,image,output,catalog,new Dictionary<string,IReadOnlyList<UniversalMobyPlacement>>{{"artisans",failed}},
                removedRowsByLevel:new Dictionary<string,IReadOnlyCollection<int>>{{"artisans",removed}});
            check(File.Exists(output)&&File.ReadAllBytes(previous).SequenceEqual(sentinel),"Normal exporter publishes repaired candidate without replacing prior output");
            check(exported.Levels.Single().Compose.PlayerAnimationImports.Any(i=>i.AddedAnimationIds.Contains(23)),"Normal export includes the certified reaction dependency");
            UniversalMobyPlacement[] wizard=[new("wizardpeak",18,84992,48960,8965,EditorId:"independent-wizard",PersistentSlot:0)];
            var independent=UniversalMobyCatalogBuilder.DryRun(library,destination,wizard,removedRows:removed);
            check(independent.Error==null && independent.Result!.Classes.Any(c=>c.SourceLevelKey=="wizardpeak"&&c.ClassId==0x11C),
                "Independent actual Wizard source still composes under native safety limits: "+independent.Error);
        }
        finally{Directory.Delete(scratch,true);}
        return Task.CompletedTask;
    }
}
