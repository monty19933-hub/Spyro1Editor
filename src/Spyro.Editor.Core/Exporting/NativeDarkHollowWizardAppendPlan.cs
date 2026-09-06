using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Exporting;

/// <summary>A source-issued plan's copied receipt, not an input certificate or a runtime address.</summary>
public sealed record NativeDarkHollowWizardAppendFamily(string FamilyId, string RoleId, int NativeIndex,
    int DonorLevelId, int DonorTrueIndex, string SourceVariantId, int RowOffset, int PropertiesOffset);

internal sealed record NativeWizardAppendPlacementOwner(string FamilyId, string RoleId, int NativeIndex,
    NativeByteExtent Row, NativeByteExtent Properties, string SourceRowSha256, string SourcePropertiesSha256,
    int SourcePropertiesPointer);

/// <summary>
/// Detached original-address scene bytes and proposed logical identities only.
/// All native donor coordinates/routes remain unchanged and may overlap. Existing
/// authored scene edits must not be overwritten by this retail-derived image.
/// Full behavior/resources/rewards, authored placement, accepted export history,
/// App transactions, native runtime and publication remain separate gates.
/// </summary>
public sealed class NativeDarkHollowWizardAppendPlan
{
    private readonly NativeMobySceneAppendPlan _plan;
    private readonly NativeDarkHollowWizardAppendSource _source;
    private readonly byte[] _originalScene;

    private NativeDarkHollowWizardAppendPlan(NativeDarkHollowWizardAppendSource source,
        NativeMobyPersistentOwnerLedger owners, NativeMobySceneAppendPlan plan)
    {
        _source = source; _plan = plan; ProposedOwners = owners;
        _originalScene = plan.CopyOriginalScene();
        Families = Array.AsReadOnly(plan.Allocation.Families.Select(f =>
        {
            var row = f.Rows.Single();
            var entry = owners.Entries.Single(e => e.FamilyId == f.Id);
            if (entry.NativeIndex != row.Index || entry.RoleId != row.Source.Role)
                throw new InvalidDataException("Native materialized receipt disagrees with its accepted source-owned ordinal.");
            return new NativeDarkHollowWizardAppendFamily(f.Id, entry.RoleId, row.Index,
                entry.Role.DonorLevelId, entry.Role.DonorTrueIndex, entry.SourceVariantId,
                checked((int)row.Row.Offset), checked((int)row.Properties.Offset));
        }).ToArray());
    }

    internal static NativeDarkHollowWizardAppendPlan Create(NativeDarkHollowWizardAppendSource source,
        NativeMobyPersistentOwnerLedger owners, NativeMobySceneAppendPlan plan) => new(source, owners, plan);

    public NativeMobyPersistentOwnerLedger ProposedOwners { get; }
    public IReadOnlyList<NativeDarkHollowWizardAppendFamily> Families { get; }
    public string SourceImageSha256 => _source.SourceImageSha256;
    public string SourceSchemaSha256 => _source.SourceSchemaSha256;
    public string FinalSceneSha256 => _plan.FinalSha256;
    public string GraphFingerprint => _plan.Graph.Fingerprint;
    public int PlacedCountBefore => _plan.PlacedCountBefore;
    public int PlacedCountAfter => _plan.PlacedCountAfter;
    public int FixupCountAfter => _plan.FixupCountAfter;
    public int DynamicCapacityBefore => _plan.DynamicCapacityBefore;
    public int DynamicCapacityAfter => _plan.DynamicCapacityAfter;
    public int SceneByteLength => NativeDarkHollowWizardAppendSource.SceneLength;
    public bool PlacementApproved => false;
    public bool BehaviorClosureProven => false;
    public bool RuntimeFitProven => false;
    public bool RuntimeVerified => false;
    public bool PublicationApproved => false;
    public bool ProjectTransactionIntegrated => false;
    public bool AcceptedExportHistoryProven => false;
    public byte[] CopyOriginalScene() => _originalScene.ToArray();
    public byte[] CopyScene() => _plan.CopyScene();
    public void VerifyOriginalAndFinal(ReadOnlySpan<byte> original, ReadOnlySpan<byte> final) => _plan.Verify(original, final);

    // Source-created extents retain the allocator's actual canonical Final
    // domain. Public integer receipts/hashes cannot substitute for these owners.
    internal IReadOnlyList<NativeWizardAppendPlacementOwner> CopyPlacementOwners() =>
        Array.AsReadOnly(_plan.Allocation.Families.Select(f =>
        {
            var row = f.Rows.Single();
            return new NativeWizardAppendPlacementOwner(f.Id, row.Source.Role, row.Index, row.Row, row.Properties,
                row.Source.Row.ExpectedSha256, row.Source.Properties.ExpectedSha256,
                checked((int)(row.Source.Properties.Extent.Offset - row.Source.PropertiesPointerBaseOffset)));
        }).ToArray());

    // One exact existing subfile3 replacement, not a full-retail afterimage.
    // Consumers must bind this original extent with every other authored owner.
    internal NativeSkyRelocationPayload CopyOriginalAddressPayload(int patchIndex) =>
        new(patchIndex, 37, 0x1095000, 14, 0, SceneByteLength, _plan.CopyScene(), 3, false);
}
