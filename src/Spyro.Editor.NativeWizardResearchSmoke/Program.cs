using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using System.Text.Json;

int checks=PackedEntryCases.Run()+AdversarialCases.Run();
if(args.Length==0)
{
    Console.WriteLine($"NATIVE_WIZARD_RESEARCH_SOURCE_FREE_OK checks={checks}");return;
}
if(args.Length is not(3 or 5))throw new ArgumentException("Usage: NativeWizardResearchSmoke [original.bin original.cue workspace [--verify|--publish|--exercise-staging directory]]");
if(args.Length==5&&args[3] is not("--verify" or "--publish" or "--exercise-staging"))throw new ArgumentException("Unknown explicit research mode.");
var plan=await NativeDarkHollowWizardResearchCandidatePlan.CaptureAsync(args[0],args[1],LevelCatalog.Load(args[2]));
checks+=await SourceCases.RunAsync(plan);
if(args.Length==5)
{
    if(args[3]=="--verify")Console.WriteLine("NATIVE_WIZARD_RESEARCH_VERIFIED "+JsonSerializer.Serialize(await NativeDarkHollowWizardResearchCandidatePublisher.VerifyAsync(plan,args[4])));
    else if(args[3]=="--publish")Console.WriteLine("NATIVE_WIZARD_RESEARCH_PUBLICATION "+JsonSerializer.Serialize(await NativeDarkHollowWizardResearchCandidatePublisher.PublishAsync(plan,args[4])));
    else checks+=await StagingCases.RunAsync(args[0],args[2],args[4]);
}
Console.WriteLine($"NATIVE_WIZARD_RESEARCH_SOURCE_OK checks={checks} plan={plan.PlanSha256} wad={plan.WadSha256} growth={plan.GrowthBytes:X} runtime/normal-export=false");
