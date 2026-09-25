namespace Spyro.Editor.Core.Exporting;

public sealed record NativeJapaneseEggResearchPublication(string OutputDirectory,string ImagePath,string CuePath,string ReceiptPath,
    string ImageSha256,string PlanSha256,int ChangedRawSectors,string RetainedRecoveryDirectory)
{
    public bool WholeImageReadback=>true;
    public bool ResearchOnly=>true;
    public bool RuntimeVerified=>false;
    public bool NormalCreateBinApproved=>false;
}

/// <summary>Explicit fixed-profile private experiment; reuses the existing staging, readback and transaction engine.</summary>
public static class NativeJapaneseEggResearchCandidatePublisher
{
    public static async Task<NativeJapaneseEggResearchPublication> PublishAsync(NativeJapaneseEggResearchCandidatePlan plan,string directory,
        CancellationToken token=default)=>Convert(await NativeWizardResearchPublicationEngine.PublishAsync(
            NativeWizardResearchPublicationBinding.FromJapaneseEgg(plan),directory,token));
    public static async Task<NativeJapaneseEggResearchPublication> VerifyAsync(NativeJapaneseEggResearchCandidatePlan plan,string directory,
        CancellationToken token=default)=>Convert(await NativeWizardResearchPublicationEngine.VerifyAsync(
            NativeWizardResearchPublicationBinding.FromJapaneseEgg(plan),directory,token));
    internal static async Task<NativeJapaneseEggResearchPublication> PublishForSmokeAsync(NativeJapaneseEggResearchCandidatePlan plan,string directory,
        NativeWizardResearchFault fault,Action<NativeWizardResearchFault,string>? checkpoint=null,CancellationToken token=default)=>
        Convert(await NativeWizardResearchPublicationEngine.PublishForSmokeAsync(NativeWizardResearchPublicationBinding.FromJapaneseEgg(plan),directory,fault,checkpoint,token));
    private static NativeJapaneseEggResearchPublication Convert(NativeWizardResearchPublication p)=>
        new(p.OutputDirectory,p.ImagePath,p.CuePath,p.ReceiptPath,p.ImageSha256,p.PlanSha256,p.ChangedRawSectors,p.RetainedRecoveryDirectory);
}
