namespace Spyro.Editor.Core.Exporting;

public sealed record NativeCollectibleCardAdmissionResearchPublication(string OutputDirectory,string ImagePath,string CuePath,string ReceiptPath,
    string ImageSha256,string PlanSha256,int ChangedRawSectors,string RetainedRecoveryDirectory)
{
    public bool WholeImageReadback => true;
    public bool ResearchOnly => true;
    public bool GuardedReadyAdmissionEmitted => true;
    public bool ReadyStateIssued => false;
    public bool NativeInstallationApproved => false;
    public bool CollectorEnabled => false;
    public bool CardWrites => false;
    public bool LifetimeProven => false;
    public bool WholeEngineFitProven => false;
    public bool RuntimeVerified => false;
    public bool NormalCreateBinApproved => false;
}

/// <summary>New-directory-only admission experiment; uses the existing physical publication transaction unchanged.</summary>
public static class NativeCollectibleCardAdmissionResearchPublisher
{
    public static async Task<NativeCollectibleCardAdmissionResearchPublication> PublishAsync(NativeCollectibleCardAdmissionResearchPlan plan,
        string directory,CancellationToken token = default)
    {
        var binding = NativeWizardResearchPublicationBinding.FromCardAdmission(plan);
        token.ThrowIfCancellationRequested(); RequireNewDirectory(directory);
        return Convert(await NativeWizardResearchPublicationEngine.PublishAsync(binding,directory,token));
    }
    public static async Task<NativeCollectibleCardAdmissionResearchPublication> VerifyAsync(NativeCollectibleCardAdmissionResearchPlan plan,
        string directory,CancellationToken token = default) => Convert(await NativeWizardResearchPublicationEngine.VerifyAsync(
            NativeWizardResearchPublicationBinding.FromCardAdmission(plan),directory,token));
    internal static async Task<NativeCollectibleCardAdmissionResearchPublication> PublishForSmokeAsync(NativeCollectibleCardAdmissionResearchPlan plan,
        string directory,NativeWizardResearchFault fault,Action<NativeWizardResearchFault,string>? checkpoint = null,CancellationToken token = default)
    {
        var binding = NativeWizardResearchPublicationBinding.FromCardAdmission(plan);
        token.ThrowIfCancellationRequested(); RequireNewDirectory(directory);
        return Convert(await NativeWizardResearchPublicationEngine.PublishForSmokeAsync(binding,directory,fault,checkpoint,token));
    }
    private static void RequireNewDirectory(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory); string path = Path.GetFullPath(directory);
        if (new FileInfo(path).LinkTarget != null || new DirectoryInfo(path).LinkTarget != null)
            throw new InvalidDataException("Card-admission research output cannot be a symbolic link.");
        if (Directory.Exists(path) || File.Exists(path))
            throw new IOException("Card-admission research publication requires a NEW directory; existing output cannot be reused or overwritten.");
    }
    private static NativeCollectibleCardAdmissionResearchPublication Convert(NativeWizardResearchPublication p) =>
        new(p.OutputDirectory,p.ImagePath,p.CuePath,p.ReceiptPath,p.ImageSha256,p.PlanSha256,p.ChangedRawSectors,p.RetainedRecoveryDirectory);
}
