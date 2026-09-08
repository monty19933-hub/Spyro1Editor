namespace Spyro.Editor.Core.Exporting;

// Only actual sealed source plans or saved contexts can bind the workflow. No
// caller-supplied filenames, hashes, callbacks, profile labels or serializers.
internal sealed class NativeId65FreshResearchPublicationBinding
{
    private readonly Role _role;
    private NativeId65FreshResearchPublicationBinding(Role role) => _role = role;

    internal static NativeId65FreshResearchPublicationBinding FromStarter(NativeId65FreshDiscResearchCandidatePlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return new(new StarterRole(plan));
    }

    internal static NativeId65FreshResearchPublicationBinding FromEdited(NativeId65FreshEditedDocumentResearchPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return new(new EditedRole(plan));
    }

    internal static NativeId65FreshResearchPublicationBinding FromSaved(NativeId65FreshSavedDocumentExportContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new(new SavedRole(context));
    }

    internal static NativeId65FreshResearchPublicationBinding FromWizard(NativeId65FreshWizardResearchCandidatePlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return new(new WizardRole(plan));
    }

    internal NativeId65FreshWizardResearchCandidatePlan? WizardPlan => _role.WizardPlan;
    internal bool TracksCommittedFailure => _role.TracksCommittedFailure;
    internal NativeId65FreshSavedDocumentExportContext? SavedContext => _role.SavedContext;
    internal string SourceImagePath => _role.SourceImagePath;
    internal string SourceCuePath => _role.SourceCuePath;
    internal string PlanSha256 => _role.PlanSha256;
    internal string Label => _role.Label;
    internal string ImageName => _role.ImageName;
    internal string CueName => _role.CueName;
    internal string ReceiptName => _role.ReceiptName;
    internal string[] OutputNames => _role.OutputNames;
    internal string LeaseName => _role.LeaseName;
    internal string StagePrefix => _role.StagePrefix;

    internal CancellationToken PostCommitToken(CancellationToken supplied) => _role.PostCommitToken(supplied);
    internal Task RequireFreshAsync(CancellationToken token) => _role.RequireFreshAsync(token);
    internal byte[] CopyCueBytes() => _role.CopyCueBytes();
    internal byte[] BuildReceipt(NativeId65FreshDiscVerification verified) => _role.BuildReceipt(verified);
    internal NativeId65FreshDiscVerification VerifyImage(string path, CancellationToken token) => _role.VerifyImage(path, token);
    internal void WriteNewImage(string path, CancellationToken token, Action firstSector) => _role.WriteNewImage(path, token, firstSector);

    // Private nested roles are a closed implementation detail, never an
    // externally implementable source authority or caller-selected policy.
    private abstract class Role
    {
        internal abstract NativeId65FreshWizardResearchCandidatePlan? WizardPlan { get; }
        internal abstract bool TracksCommittedFailure { get; }
        internal abstract NativeId65FreshSavedDocumentExportContext? SavedContext { get; }
        internal abstract string SourceImagePath { get; }
        internal abstract string SourceCuePath { get; }
        internal abstract string PlanSha256 { get; }
        internal abstract string Label { get; }
        internal abstract string ImageName { get; }
        internal abstract string CueName { get; }
        internal abstract string ReceiptName { get; }
        internal abstract string[] OutputNames { get; }
        internal abstract string LeaseName { get; }
        internal abstract string StagePrefix { get; }
        internal abstract CancellationToken PostCommitToken(CancellationToken supplied);
        internal abstract Task RequireFreshAsync(CancellationToken token);
        internal abstract byte[] CopyCueBytes();
        internal abstract byte[] BuildReceipt(NativeId65FreshDiscVerification verified);
        internal abstract NativeId65FreshDiscVerification VerifyImage(string path, CancellationToken token);
        internal abstract void WriteNewImage(string path, CancellationToken token, Action firstSector);
    }

    private sealed class StarterRole : Role
    {
        private readonly NativeId65FreshDiscResearchCandidatePlan _plan;
        internal StarterRole(NativeId65FreshDiscResearchCandidatePlan plan) => _plan = plan;

        internal override NativeId65FreshWizardResearchCandidatePlan? WizardPlan => null;
        internal override bool TracksCommittedFailure => false;
        internal override NativeId65FreshSavedDocumentExportContext? SavedContext => null;
        internal override string SourceImagePath => _plan.SourceImagePath;
        internal override string SourceCuePath => _plan.SourceCuePath;
        internal override string PlanSha256 => _plan.PlanSha256;
        internal override string Label => "Fresh LAB";
        internal override string ImageName => NativeId65FreshDiscResearchCandidatePlan.ImageName;
        internal override string CueName => NativeId65FreshDiscResearchCandidatePlan.CueName;
        internal override string ReceiptName => NativeId65FreshDiscResearchCandidatePlan.ReceiptName;
        internal override string[] OutputNames => NativeId65FreshDiscResearchCandidatePlan.OutputNames;
        internal override string LeaseName => ".native-id65-fresh.writer.lock";
        internal override string StagePrefix => ".native-id65-fresh-stage-";
        internal override CancellationToken PostCommitToken(CancellationToken supplied) => supplied;
        internal override Task RequireFreshAsync(CancellationToken token) => _plan.RequireFreshAsync(token);
        internal override byte[] CopyCueBytes() => _plan.CopyCueBytes();
        internal override byte[] BuildReceipt(NativeId65FreshDiscVerification verified) => _plan.BuildReceipt(verified);
        internal override NativeId65FreshDiscVerification VerifyImage(string path, CancellationToken token) => _plan.VerifyImage(path, token);
        internal override void WriteNewImage(string path, CancellationToken token, Action firstSector) => _plan.WriteNewImage(path, token, firstSector);
    }

    private sealed class EditedRole : Role
    {
        private readonly NativeId65FreshEditedDocumentResearchPlan _plan;
        internal EditedRole(NativeId65FreshEditedDocumentResearchPlan plan) => _plan = plan;

        internal override NativeId65FreshWizardResearchCandidatePlan? WizardPlan => null;
        internal override bool TracksCommittedFailure => false;
        internal override NativeId65FreshSavedDocumentExportContext? SavedContext => null;
        internal override string SourceImagePath => _plan.SourceImagePath;
        internal override string SourceCuePath => _plan.SourceCuePath;
        internal override string PlanSha256 => _plan.PlanSha256;
        internal override string Label => "Edited fresh LAB";
        internal override string ImageName => NativeId65FreshEditedDocumentResearchPlan.ImageName;
        internal override string CueName => NativeId65FreshEditedDocumentResearchPlan.CueName;
        internal override string ReceiptName => NativeId65FreshEditedDocumentResearchPlan.ReceiptName;
        internal override string[] OutputNames => NativeId65FreshEditedDocumentResearchPlan.OutputNames;
        internal override string LeaseName => ".native-id65-fresh-edited.writer.lock";
        internal override string StagePrefix => ".native-id65-fresh-edited-stage-";
        internal override CancellationToken PostCommitToken(CancellationToken supplied) => CancellationToken.None;
        internal override Task RequireFreshAsync(CancellationToken token) => _plan.RequireFreshAsync(token);
        internal override byte[] CopyCueBytes() => _plan.CopyCueBytes();
        internal override byte[] BuildReceipt(NativeId65FreshDiscVerification verified) => _plan.BuildReceipt(verified);
        internal override NativeId65FreshDiscVerification VerifyImage(string path, CancellationToken token) => _plan.VerifyImage(path, token);
        internal override void WriteNewImage(string path, CancellationToken token, Action firstSector) => _plan.WriteNewImage(path, token, firstSector);
    }

    private sealed class SavedRole : Role
    {
        private readonly NativeId65FreshSavedDocumentExportContext _context;
        internal SavedRole(NativeId65FreshSavedDocumentExportContext context) => _context = context;

        internal override NativeId65FreshWizardResearchCandidatePlan? WizardPlan => null;
        internal override bool TracksCommittedFailure => true;
        internal override NativeId65FreshSavedDocumentExportContext? SavedContext => _context;
        internal override string SourceImagePath => _context.Plan.SourceImagePath;
        internal override string SourceCuePath => _context.Plan.SourceCuePath;
        internal override string PlanSha256 => _context.Plan.PlanSha256;
        internal override string Label => "Saved fresh LAB";
        internal override string ImageName => NativeId65FreshDocumentResearchPlan.ImageName;
        internal override string CueName => NativeId65FreshDocumentResearchPublisher.CueName;
        internal override string ReceiptName => NativeId65FreshDocumentResearchPublisher.ReceiptName;
        internal override string[] OutputNames => [ImageName, ReceiptName, CueName];
        internal override string LeaseName => ".native-id65-fresh-document-research.writer.lock";
        internal override string StagePrefix => ".native-id65-fresh-document-research-stage-";
        internal override CancellationToken PostCommitToken(CancellationToken supplied) => CancellationToken.None;
        internal override Task RequireFreshAsync(CancellationToken token) => _context.Plan.RequireFreshAsync(token);
        internal override byte[] CopyCueBytes() => _context.Plan.CopyCueBytes();
        internal override byte[] BuildReceipt(NativeId65FreshDiscVerification verified) => NativeId65FreshDocumentResearchPublisher.BuildReceipt(_context, verified);
        internal override NativeId65FreshDiscVerification VerifyImage(string path, CancellationToken token) => _context.Plan.VerifyImage(path, token);
        internal override void WriteNewImage(string path, CancellationToken token, Action firstSector) => _context.Plan.WriteNewImage(path, token, firstSector);
    }

    private sealed class WizardRole : Role
    {
        private readonly NativeId65FreshWizardResearchCandidatePlan _plan;
        internal WizardRole(NativeId65FreshWizardResearchCandidatePlan plan) => _plan = plan;

        internal override NativeId65FreshWizardResearchCandidatePlan? WizardPlan => _plan;
        internal override bool TracksCommittedFailure => true;
        internal override NativeId65FreshSavedDocumentExportContext? SavedContext => null;
        internal override string SourceImagePath => _plan.SourceImagePath;
        internal override string SourceCuePath => _plan.SourceCuePath;
        internal override string PlanSha256 => _plan.PlanSha256;
        internal override string Label => "Fresh Wizard";
        internal override string ImageName => _plan.BoundImageName;
        internal override string CueName => _plan.BoundCueName;
        internal override string ReceiptName => _plan.BoundReceiptName;
        internal override string[] OutputNames => _plan.BoundOutputNames;
        internal override string LeaseName => ".native-id65-fresh-wizard.writer.lock";
        internal override string StagePrefix => ".native-id65-fresh-wizard-stage-";
        internal override CancellationToken PostCommitToken(CancellationToken supplied) => CancellationToken.None;
        internal override Task RequireFreshAsync(CancellationToken token) => _plan.RequireFreshAsync(token);
        internal override byte[] CopyCueBytes() => _plan.CopyCueBytes();
        internal override byte[] BuildReceipt(NativeId65FreshDiscVerification verified) => _plan.BuildReceipt(verified);
        internal override NativeId65FreshDiscVerification VerifyImage(string path, CancellationToken token) => _plan.VerifyImage(path, token);
        internal override void WriteNewImage(string path, CancellationToken token, Action firstSector) => _plan.WriteNewImage(path, token, firstSector);
    }
}
