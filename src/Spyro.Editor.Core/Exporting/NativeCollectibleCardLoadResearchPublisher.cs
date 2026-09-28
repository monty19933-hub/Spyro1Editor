namespace Spyro.Editor.Core.Exporting;

public sealed record NativeCollectibleCardLoadResearchPublication(string OutputDirectory,string ImagePath,string CuePath,string ReceiptPath,
    string ImageSha256,string PlanSha256,int ChangedRawSectors,string RetainedRecoveryDirectory)
{
    public bool WholeImageReadback => true;
    public bool ResearchOnly => true;
    public bool ReadyStateIssued => false;
    public bool CollectorEnabled => false;
    public bool CardWrites => false;
    public bool RuntimeVerified => false;
    public bool NormalCreateBinApproved => false;
}

/// <summary>Closed card-read profile; physical staging/readback/transaction use the existing publication engine.</summary>
public static class NativeCollectibleCardLoadResearchPublisher
{
    public static async Task<NativeCollectibleCardLoadResearchPublication> PublishAsync(NativeCollectibleCardLoadResearchPlan plan,
        string directory,CancellationToken token = default)
    {
        var binding = NativeWizardResearchPublicationBinding.FromCardLoad(plan);
        token.ThrowIfCancellationRequested(); RequireNewDirectory(directory);
        return Convert(await NativeWizardResearchPublicationEngine.PublishAsync(binding,directory,token));
    }
    public static async Task<NativeCollectibleCardLoadResearchPublication> VerifyAsync(NativeCollectibleCardLoadResearchPlan plan,
        string directory,CancellationToken token = default) => Convert(await NativeWizardResearchPublicationEngine.VerifyAsync(
            NativeWizardResearchPublicationBinding.FromCardLoad(plan),directory,token));
    internal static async Task<NativeCollectibleCardLoadResearchPublication> PublishForSmokeAsync(NativeCollectibleCardLoadResearchPlan plan,
        string directory,NativeWizardResearchFault fault,Action<NativeWizardResearchFault,string>? checkpoint = null,CancellationToken token = default)
    {
        var binding = NativeWizardResearchPublicationBinding.FromCardLoad(plan);
        token.ThrowIfCancellationRequested(); RequireNewDirectory(directory);
        return Convert(await NativeWizardResearchPublicationEngine.PublishForSmokeAsync(binding,directory,fault,checkpoint,token));
    }
    private static void RequireNewDirectory(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory); string path = Path.GetFullPath(directory);
        if (new FileInfo(path).LinkTarget != null || new DirectoryInfo(path).LinkTarget != null)
            throw new InvalidDataException("Card-load research output cannot be a symbolic link.");
        if (Directory.Exists(path) || File.Exists(path))
            throw new IOException("Card-load research publication requires a NEW directory; existing output cannot be reused or overwritten.");
    }
    private static NativeCollectibleCardLoadResearchPublication Convert(NativeWizardResearchPublication p) =>
        new(p.OutputDirectory,p.ImagePath,p.CuePath,p.ReceiptPath,p.ImageSha256,p.PlanSha256,p.ChangedRawSectors,p.RetainedRecoveryDirectory);
}
