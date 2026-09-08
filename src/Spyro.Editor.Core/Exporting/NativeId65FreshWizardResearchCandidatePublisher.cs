namespace Spyro.Editor.Core.Exporting;

/// <summary>PublishNew actually returned, but final verification or accounted cleanup failed. Paths are retained evidence, not verified success.</summary>
public sealed class NativeId65FreshWizardResearchCommittedException:IOException
{
    internal NativeId65FreshWizardResearchCommittedException(string root,NativeId65FreshWizardResearchCandidatePlan plan,string recovery,string stage,string lease,Exception inner)
        :base("Fresh Wizard artifacts were committed but final verification or cleanup did not complete. Preserve "+root,inner)
    {
        OutputDirectory=root;ImagePath=Path.Combine(root,plan.BoundImageName);
        CuePath=Path.Combine(root,plan.BoundCueName);ReceiptPath=Path.Combine(root,plan.BoundReceiptName);
        PlanSha256=plan.PlanSha256;RetainedRecoveryDirectory=recovery;RetainedStageDirectory=stage;RetainedOutputLeasePath=lease;
    }
    public string OutputDirectory{get;}
    public string ImagePath{get;}
    public string CuePath{get;}
    public string ReceiptPath{get;}
    public string PlanSha256{get;}
    public string RetainedRecoveryDirectory{get;}
    public string RetainedStageDirectory{get;}
    public string RetainedOutputLeasePath{get;}
}
internal enum NativeId65FreshWizardResearchFault{None,AfterFirstSector,AfterImageWrite,BeforePublication}
internal enum NativeId65FreshWizardPostcommitPoint{AfterCommit,BeforeStageCleanup}

/// <summary>Exact new-only research materialization; no normal exporter or runtime approval. Trusted ancestors/cooperating writers required.</summary>
public static class NativeId65FreshWizardResearchCandidatePublisher
{
    public static async Task<NativeId65FreshDiscResearchPublication> PublishAsync(NativeId65FreshWizardResearchCandidatePlan plan,
        string outputDirectory,CancellationToken token=default)=>await NativeId65FreshResearchPublicationEngine.PublishAsync(
            NativeId65FreshResearchPublicationBinding.FromWizard(plan),outputDirectory,NativeId65FreshResearchFault.None,null,token);
    public static async Task<NativeId65FreshDiscResearchPublication> VerifyAsync(NativeId65FreshWizardResearchCandidatePlan plan,
        string outputDirectory,CancellationToken token=default)=>await NativeId65FreshResearchPublicationEngine.VerifyAsync(
            NativeId65FreshResearchPublicationBinding.FromWizard(plan),outputDirectory,token);
    internal static Task<NativeId65FreshDiscResearchPublication> PublishForSmokeAsync(NativeId65FreshWizardResearchCandidatePlan plan,
        string outputDirectory,NativeId65FreshWizardResearchFault fault,Action<NativeId65FreshWizardResearchFault,string>? checkpoint=null,CancellationToken token=default)
    {
        if(fault==NativeId65FreshWizardResearchFault.None)throw new InvalidDataException("A Wizard staging exercise must force a precommit fault.");
        return PublishFaultCore(plan,outputDirectory,fault,checkpoint,token);
    }
    private static async Task<NativeId65FreshDiscResearchPublication> PublishFaultCore(NativeId65FreshWizardResearchCandidatePlan plan,
        string outputDirectory,NativeId65FreshWizardResearchFault fault,Action<NativeId65FreshWizardResearchFault,string>? checkpoint,CancellationToken token)
        =>await NativeId65FreshResearchPublicationEngine.PublishAsync(NativeId65FreshResearchPublicationBinding.FromWizard(plan),outputDirectory,
            (NativeId65FreshResearchFault)fault,checkpoint==null?null:(point,image)=>checkpoint((NativeId65FreshWizardResearchFault)point,image),token);
    internal static async Task<NativeId65FreshDiscResearchPublication> PublishPostcommitForSmokeAsync(NativeId65FreshWizardResearchCandidatePlan plan,
        string outputDirectory,Action<NativeId65FreshWizardPostcommitPoint,string> checkpoint,CancellationToken token=default)
        =>await NativeId65FreshResearchPublicationEngine.PublishWizardPostcommitForSmokeAsync(
            NativeId65FreshResearchPublicationBinding.FromWizard(plan),outputDirectory,checkpoint,token);
    internal static async Task<NativeId65FreshDiscResearchPublication> PublishUnconfirmedForSmokeAsync(NativeId65FreshWizardResearchCandidatePlan plan,
        string outputDirectory,Action<string> checkpoint,CancellationToken token=default)
        =>await NativeId65FreshResearchPublicationEngine.PublishWizardUnconfirmedForSmokeAsync(
            NativeId65FreshResearchPublicationBinding.FromWizard(plan),outputDirectory,checkpoint,token);
}
