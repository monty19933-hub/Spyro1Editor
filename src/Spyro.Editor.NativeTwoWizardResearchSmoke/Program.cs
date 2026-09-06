using System.Reflection;
using Spyro.Editor.Core.Exporting;

internal static class Program
{
    private static int _checks;
    internal static void Check(bool value,string reason)
    {if(!value)throw new InvalidDataException(reason);_checks++;}
    internal static void Reject(Action action,string reason)
    {
        try{action();}catch(InvalidDataException){_checks++;return;}catch(ArgumentException){_checks++;return;}
        throw new InvalidDataException("Expected refusal: "+reason);
    }
    private static async Task Main(string[] args)
    {
        Check(typeof(NativeDarkHollowTwoWizardResearchCandidatePlan).GetConstructors(BindingFlags.Public|BindingFlags.Instance).Length==0,"new candidate constructor remains source-issued");
        Check(!typeof(NativeDarkHollowWizardResearchCandidatePlan).IsAssignableFrom(typeof(NativeDarkHollowTwoWizardResearchCandidatePlan)),"two-profile plan cannot enter legacy publisher by inheritance");
        var capture=typeof(NativeDarkHollowTwoWizardResearchCandidatePlan).GetMethod("CaptureAsync")!;
        Check(capture.GetParameters().Select(p=>p.Name).SequenceEqual(new[]{"originalImagePath","originalCuePath","catalog","token"}),"fixed factory takes no actor/count/hash/approval/receipt/placement metadata");
        Reject(()=>NativeDarkHollowTwoWizardResearchCandidatePlan.CaptureAsync("missing-original.bin","missing-original.cue",null!).GetAwaiter().GetResult(),"missing catalog before source I/O");
        Reject(()=>NativeDarkHollowTwoWizardResearchCandidatePublisher.VerifyAsync(null!,"missing-output").GetAwaiter().GetResult(),"no verification authority from output metadata");
        Reject(()=>NativeDarkHollowTwoWizardResearchCandidatePublisher.PublishAsync(null!,"missing-output").GetAwaiter().GetResult(),"no publication authority from output metadata");
        using(var canceled=new CancellationTokenSource())
        {
            canceled.Cancel();
            try{await NativeDarkHollowTwoWizardResearchCandidatePlan.CaptureAsync("missing-original.bin","missing-original.cue",null!,canceled.Token);throw new InvalidDataException("Cancellation ignored before I/O");}
            catch(OperationCanceledException){Check(true,"canceled capture does no source/output I/O");}
        }
        // Legacy async entry points returned faulted Tasks for null arguments,
        // rather than throwing synchronously while binding the plan.
        Task<NativeWizardResearchPublication> oldPublish=NativeDarkHollowWizardResearchCandidatePublisher.PublishAsync(null!,"missing-output");
        Task<NativeWizardResearchPublication> oldVerify=NativeDarkHollowWizardResearchCandidatePublisher.VerifyAsync(null!,"missing-output");
        Reject(()=>oldPublish.GetAwaiter().GetResult(),"legacy Publish null remains a faulted Task");
        Reject(()=>oldVerify.GetAwaiter().GetResult(),"legacy Verify null remains a faulted Task");
        ReviewedPublicationCases.RunArgumentTests();
        Console.WriteLine($"NATIVE_TWO_WIZARD_RESEARCH_FREE_OK checks={_checks}");
        if(args.Length==0)return;
        if(ReviewedPublicationCases.IsReviewedMode(args))
        {
            await ReviewedPublicationCases.RunAsync(args);return;
        }
        if(args.Length==4&&args[0]=="--exercise-staging")
        {
            await SharedStagingCases.RunAsync(args[1],args[2],args[3]);return;
        }
        if(args.Length!=4||args[0]!="--source")throw new ArgumentException("Usage: --source original.bin original.cue workspace | --exercise-staging original.bin workspace NEW-directory (faults only; no successful candidate publication)");
        await SourceCases.Run(args[1],args[2],args[3]);
        Console.WriteLine($"NATIVE_TWO_WIZARD_RESEARCH_SOURCE_OK checks={_checks} runtime=false normal-export=false");
    }
}
