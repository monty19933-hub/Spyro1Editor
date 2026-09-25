namespace Spyro.Editor.Core.Exporting;

public sealed record NativeJapaneseEggAuthoredResearchPublication(string OutputDirectory,string ImagePath,string CuePath,string ReceiptPath,
    string ImageSha256,string PlanSha256,int ChangedRawSectors,string RetainedRecoveryDirectory)
{
    public bool WholeImageReadback=>true;
    public bool ResearchOnly=>true;
    public bool IndependentColorVariants=>true;
    public bool RuntimeVerified=>false;
    public bool NormalCreateBinApproved=>false;
}

/// <summary>Closed diagnostic-color profile; all physical publication mechanics remain in the existing engine.</summary>
public static class NativeJapaneseEggAuthoredResearchCandidatePublisher
{
    public static async Task<NativeJapaneseEggAuthoredResearchPublication> PublishAsync(NativeJapaneseEggAuthoredResearchCandidatePlan plan,
        string directory,CancellationToken token=default)=>Convert(await NativeWizardResearchPublicationEngine.PublishAsync(
            NativeWizardResearchPublicationBinding.FromAuthoredJapaneseEgg(plan),directory,token));
    public static async Task<NativeJapaneseEggAuthoredResearchPublication> VerifyAsync(NativeJapaneseEggAuthoredResearchCandidatePlan plan,
        string directory,CancellationToken token=default)=>Convert(await NativeWizardResearchPublicationEngine.VerifyAsync(
            NativeWizardResearchPublicationBinding.FromAuthoredJapaneseEgg(plan),directory,token));
    internal static async Task<NativeJapaneseEggAuthoredResearchPublication> PublishForSmokeAsync(NativeJapaneseEggAuthoredResearchCandidatePlan plan,
        string directory,NativeWizardResearchFault fault,Action<NativeWizardResearchFault,string>? checkpoint=null,CancellationToken token=default)=>
        Convert(await NativeWizardResearchPublicationEngine.PublishForSmokeAsync(NativeWizardResearchPublicationBinding.FromAuthoredJapaneseEgg(plan),directory,fault,checkpoint,token));
    private static NativeJapaneseEggAuthoredResearchPublication Convert(NativeWizardResearchPublication p)=>
        new(p.OutputDirectory,p.ImagePath,p.CuePath,p.ReceiptPath,p.ImageSha256,p.PlanSha256,p.ChangedRawSectors,p.RetainedRecoveryDirectory);
}
