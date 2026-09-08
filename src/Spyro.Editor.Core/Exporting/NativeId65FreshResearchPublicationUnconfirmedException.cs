namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// PublishNew was actually invoked but did not return a confirmed outcome.
/// Preserve evidence; never infer commit/abort or retry permission from bytes.
/// Paths identify known inspection locations, not existence or ownership claims.
/// </summary>
public sealed class NativeId65FreshResearchPublicationUnconfirmedException : IOException
{
    internal NativeId65FreshResearchPublicationUnconfirmedException(NativeId65FreshResearchPublicationBinding binding,
        string root, string stage, string lease, string projectLease, Exception inner)
        : base(binding.Label + " publication was attempted but its outcome is unconfirmed. Do not retry or remove evidence in " + root, inner)
    {
        OutputDirectory = root;
        ImagePath = Path.Combine(root, binding.ImageName);
        CuePath = Path.Combine(root, binding.CueName);
        ReceiptPath = Path.Combine(root, binding.ReceiptName);
        PlanSha256 = binding.PlanSha256;
        RetainedRecoveryDirectory = root;
        RetainedStageDirectory = stage;
        RetainedOutputLeasePath = lease;
        RetainedProjectLeasePath = projectLease;
        PublicationLockPath = Path.Combine(root, ".export-publication.lock");
    }
    public string OutputDirectory { get; }
    public string ImagePath { get; }
    public string CuePath { get; }
    public string ReceiptPath { get; }
    public string PlanSha256 { get; }
    public string RetainedRecoveryDirectory { get; }
    public string RetainedStageDirectory { get; }
    public string RetainedOutputLeasePath { get; }
    public string RetainedProjectLeasePath { get; }
    public string PublicationLockPath { get; }
}
