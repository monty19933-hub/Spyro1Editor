namespace Spyro.Editor.Core.Exporting;

/// <summary>Separately named, new-only two-owner research publication. Never accepts the legacy plan or a receipt as authority.</summary>
public static class NativeDarkHollowTwoWizardResearchCandidatePublisher
{
    public static Task<NativeWizardResearchPublication> PublishAsync(NativeDarkHollowTwoWizardResearchCandidatePlan plan,string directory,CancellationToken token=default)=>
        NativeWizardResearchPublicationEngine.PublishAsync(NativeWizardResearchPublicationBinding.FromTwo(plan),directory,token);
    public static Task<NativeWizardResearchPublication> VerifyAsync(NativeDarkHollowTwoWizardResearchCandidatePlan plan,string directory,CancellationToken token=default)=>
        NativeWizardResearchPublicationEngine.VerifyAsync(NativeWizardResearchPublicationBinding.FromTwo(plan),directory,token);
    internal static Task<NativeWizardResearchPublication> PublishForSmokeAsync(NativeDarkHollowTwoWizardResearchCandidatePlan plan,string directory,
        NativeWizardResearchFault fault,Action<NativeWizardResearchFault,string>? checkpoint=null,CancellationToken token=default)=>
        NativeWizardResearchPublicationEngine.PublishForSmokeAsync(NativeWizardResearchPublicationBinding.FromTwo(plan),directory,fault,checkpoint,token);
}
