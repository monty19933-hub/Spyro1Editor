using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.Core.Analysis;

public enum NativeMobyPortabilityStatus { Unresolved }
public enum NativeMobyEvidenceKind { SourceDiscovery, CodeContract, RecordedValidation }

public sealed record NativeMobyMatrixEvidence(string Id, NativeMobyEvidenceKind Kind, string RepositoryPath, string Scope);
public sealed record NativeMobyDependencyObligation(string Id, string RequiredEvidence, IReadOnlyList<string> EvidenceIds);
public sealed record NativeMobyMatrixDonor(string LevelKey, int TrueIndex, ushort ActorId);

/// <summary>Indexed prior evidence, not a newly issued factory, runtime or publication certificate.</summary>
public sealed record NativeMobyScopedCapability(string Id, string ProfileId, string? Variant,
    string DestinationScope, string? RetailTargetKey, IReadOnlyList<ushort> RelatedActorIds,
    IReadOnlyList<NativeMobyMatrixDonor> ExactDonors, IReadOnlyList<string> ObservedDonorVariantIds,
    IReadOnlyList<string> ValidatedCapabilities, IReadOnlyList<string> RemainingGates,
    IReadOnlyList<string> EvidenceIds, string? ExactCandidateImageSha256)
{
    public bool GrantsMatrixImportAuthority => false;
    public bool ValidationReexecutedByMatrix => false;
}

public sealed record NativeMobyMatrixVariant(string SourceVariantId, ushort ActorId,
    NativeMobyCatalogueEntryKind Kind, string SourceLevelKey, int? SourceLevelId, int? SourceWadEntry,
    string SourceDataSha256, string? SourceRecordSha256, int? TrueIndex, int? RegistrationSlot,
    uint? LocalRootDataOffset, uint? SharedRootPeteOffset, NativeMobyPropertiesLocation? PropertiesLocation,
    string Label, string ClassificationEvidence, IReadOnlyList<string> ExactDonorCapabilityIds)
{
    // A registration's next root is not its package end. Package contracts are
    // indexed separately and must be issued by their original source factories.
    public int? DiscoveredPackageByteLength => null;
}

public sealed record NativeMobyMatrixDestination(string TargetLevelKey, int TargetLevelId,
    NativeMobyLocalModelStatus LocalModelStatus, uint? LocalRootDataOffset, uint? SharedRootPeteOffset,
    int NativePlacedRowCount, int GenericRegistrationTailSlots, IReadOnlyList<string> ScopedCapabilityIds)
{
    public NativeMobyPortabilityStatus UniversalImportStatus => NativeMobyPortabilityStatus.Unresolved;
    public string UnresolvedDependencySetId => "universal-import";
    public bool NativeImportApproved => false;
    public bool MemoryFitEvaluated => false;
}

public sealed record NativeMobyMatrixClass(ushort ActorId, int PlacedRowCount, int LocalRegistrationCount,
    int SharedRegistrationCount, IReadOnlyList<string> SourceVariantIds,
    IReadOnlyList<string> RelatedCapabilityIds, IReadOnlyList<NativeMobyMatrixDestination> Destinations);

public sealed record NativeMobyMatrixCounts(int RetailLevels, int PlacedRows, int LocalRegistrations,
    int PlacedOrLocalClasses, int SharedRegistrations, int SharedOnlyClasses, int OverlappingClasses,
    int UnionClasses, int ObservedVariants, int DestinationCells);

/// <summary>
/// Deterministic read-only comparison of every observed class/variant with every retail destination.
/// Unknown means unresolved, not impossible. Existing closed recipes retain their separate authority.
/// </summary>
public sealed class NativeMobyPortabilityMatrix
{
    private NativeMobyPortabilityMatrix(IReadOnlyList<NativeMobySourceLevel> levels,
        NativeSharedMobySourceInventory shared, bool exactSource)
    {
        SourceIdentityVerified = exactSource;
        var orderedLevels = levels.OrderBy(l => l.LevelId).ToArray();
        var entries = orderedLevels.SelectMany(NativeMobyCatalogue.DescribeLevel)
            .Concat(shared.Registrations.Select(r => new NativeMobyCatalogueEntry(shared, r)))
            .OrderBy(e => e.ActorId).ThenBy(e => e.SourceLevelId).ThenBy(e => e.TrueIndex ?? int.MaxValue)
            .ThenBy(e => e.RegistrationSlot).ToArray();
        if (entries.Select(e => e.SourceVariantId).Distinct(StringComparer.Ordinal).Count() != entries.Length ||
            orderedLevels.Select(l => l.LevelKey).Distinct(StringComparer.Ordinal).Count() != orderedLevels.Length)
            throw new InvalidDataException("Matrix observations contain duplicate variant or destination identities.");
        SharedHeaderSha256 = shared.HeaderSha256;
        SharedRuntimeCopySha256 = shared.RuntimeModelDataSha256;
        SharedRuntimeCopyByteLength = shared.RuntimeModelDataByteLength;
        Evidence = EvidenceIndex();
        DependencyObligations = Obligations();
        KnownScopedCapabilities = CapabilityIndex(entries, exactSource);
        SourceVariants = Freeze(entries.Select(e => new NativeMobyMatrixVariant(e.SourceVariantId, e.ActorId,
            e.Kind, e.SourceLevelKey, e.SourceLevelId, e.SourceWadEntry, e.SourceDataSha256, e.SourceRecordSha256,
            e.TrueIndex, e.RegistrationSlot,
            e.SourceLevelId is int levelId && e.RegistrationSlot is int slot
                ? orderedLevels.Single(l => l.LevelId == levelId).Registrations.Single(r => r.Slot == slot).RootDataOffset : null,
            e.SharedRootPeteOffset, e.PropertiesLocation, e.Label, e.ClassificationEvidence,
            Freeze(KnownScopedCapabilities.Where(c => c.ObservedDonorVariantIds.Contains(e.SourceVariantId)).Select(c => c.Id)))));
        var localIds = entries.Where(e => !e.IsSharedResource).Select(e => e.ActorId).ToHashSet();
        var sharedIds = shared.Registrations.Select(r => r.ActorId).ToHashSet();
        Classes = Freeze(entries.GroupBy(e => e.ActorId).Select(group =>
        {
            ushort id = group.Key;
            var sharedRoot = shared.Registrations.SingleOrDefault(r => r.ActorId == id);
            var related = KnownScopedCapabilities.Where(c => c.RelatedActorIds.Contains(id)).ToArray();
            return new NativeMobyMatrixClass(id, group.Count(e => e.Kind == NativeMobyCatalogueEntryKind.PlacedRow),
                group.Count(e => e.Kind is NativeMobyCatalogueEntryKind.LocalRegistration or NativeMobyCatalogueEntryKind.PlayerAnimationStream),
                group.Count(e => e.IsSharedResource), Freeze(group.Select(e => e.SourceVariantId)), Freeze(related.Select(c => c.Id)),
                Freeze(orderedLevels.Select(target =>
                {
                    var root = target.Registrations.SingleOrDefault(r => r.ActorId == id);
                    return new NativeMobyMatrixDestination(target.LevelKey, target.LevelId,
                        root == null ? NativeMobyLocalModelStatus.Absent : NativeMobyLocalModelStatus.Present,
                        root?.RootDataOffset, sharedRoot?.RootPeteOffset, target.Rows.Count(r => r.ActorId == id),
                        63 - target.Registrations.Count(r => r.Slot > 0),
                        Freeze(related.Where(c => c.RetailTargetKey == target.LevelKey).Select(c => c.Id)));
                })));
        }));
        Counts = new(orderedLevels.Length, orderedLevels.Sum(l => l.Rows.Count), orderedLevels.Sum(l => l.Registrations.Count),
            localIds.Count, shared.Registrations.Count, sharedIds.Except(localIds).Count(), sharedIds.Intersect(localIds).Count(),
            Classes.Count, SourceVariants.Count, Classes.Sum(c => c.Destinations.Count));
    }

    public int SchemaVersion => 1;
    public bool SourceIdentityVerified { get; }
    public string? SourceImageSha256 => SourceIdentityVerified ? NativeMobySourceInventory.OriginalImageSha256 : null;
    public long? SourceImageByteLength => SourceIdentityVerified ? NativeMobySourceInventory.OriginalImageByteLength : null;
    public string Scope => "Union of observed USA placed/local classes and shared PETE registrations, retaining every source variant, across all inspected retail destinations. Dynamically emitted and other undiscovered classes are not asserted exhaustive. No universal import authority is issued.";
    public string DependencySetId => "universal-import";
    public string DependencyMeaning => "Every listed category remains unresolved for arbitrary source-variant/destination interchange. A root, spare header slot or native placement is observation, not equivalence, writable capacity or dependency closure. Scoped capability references preserve exact prior contracts; they do not certify the whole cell or every same-class variant.";
    public string SharedHeaderSha256 { get; }
    public string SharedRuntimeCopySha256 { get; }
    public int SharedRuntimeCopyByteLength { get; }
    public NativeMobyMatrixCounts Counts { get; }
    public IReadOnlyList<NativeMobyMatrixEvidence> Evidence { get; }
    public IReadOnlyList<NativeMobyDependencyObligation> DependencyObligations { get; }
    public IReadOnlyList<NativeMobyScopedCapability> KnownScopedCapabilities { get; }
    public IReadOnlyList<NativeMobyMatrixVariant> SourceVariants { get; }
    public IReadOnlyList<NativeMobyMatrixClass> Classes { get; }

    public static NativeMobyPortabilityMatrix Create(NativeMobySourceInventory inventory)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        return new(inventory.Levels, inventory.SharedModels, true);
    }

    // Synthetic regression snapshots never acquire the original-image identity
    // or donor bindings of the public, source-captured path.
    internal static NativeMobyPortabilityMatrix DescribeObservations(IReadOnlyList<NativeMobySourceLevel> levels,
        NativeSharedMobySourceInventory shared) => new(levels, shared, false);

    private static IReadOnlyList<T> Freeze<T>(IEnumerable<T> items) => Array.AsReadOnly(items.ToArray());

    private static IReadOnlyList<NativeMobyMatrixEvidence> EvidenceIndex() => Freeze(new NativeMobyMatrixEvidence[]
    {
        new("inventory", NativeMobyEvidenceKind.SourceDiscovery, "src/Spyro.Editor.Core/Analysis/NativeMobySourceInventory.cs", "Complete original USA identity, 35 level owners, rows and roots; no package extent or import approval."),
        new("pete", NativeMobyEvidenceKind.SourceDiscovery, "src/Spyro.Editor.Core/Analysis/NativeSharedMobySourceInventory.cs", "Complete PETE header and 0x39C48 runtime-copy owner, including 0x448 beyond the directory entry; roots are not package extents."),
        new("registration", NativeMobyEvidenceKind.CodeContract, "docs/research/native-moby-model-registration-producer.md", "Source-bound append-only registration of already closed packages; no generic package decoder or runtime-memory certificate."),
        new("instance", NativeMobyEvidenceKind.CodeContract, "docs/research/native-moby-instance-allocation-contract.md", "Private rows/properties/fixups, owner remap and immutable reuse; native key/chest singleton globals exclude two independent families."),
        new("town-chest", NativeMobyEvidenceKind.CodeContract, "docs/native-moby-dependency-closure-contract.md", "Exact Peace-to-Town V2/V3 seven-row family, behavior, texture, sound, effects and physical publication contracts."),
        new("wizard-assets", NativeMobyEvidenceKind.CodeContract, "src/Spyro.Editor.Core/Exporting/NativeWizardFamilyAssetPlan.cs", "Wizard Peak T6 source-form models, player animation 8, particle 7, texture/palette and sound owners; global sound 26 requires exact resident reuse."),
        new("wizard-dark", NativeMobyEvidenceKind.CodeContract, "docs/research/native-dark-wizard-research-candidate-contract.md", "One fixed Dark Hollow research recipe; does not certify arbitrary placement, complete closure or normal export."),
        new("grass-assets", NativeMobyEvidenceKind.CodeContract, "src/Spyro.Editor.Core/Exporting/NativeMobyFamilyAssetPlan.cs", "Artisans T121 class 0x01F5 exact 0x174 model, row/private properties and native passive dispatch; detached source assets alone do not bind a destination."),
        new("r08", NativeMobyEvidenceKind.RecordedValidation, "docs/research/native-fresh-tiled-family-editor-r08.md", "Exact schema5 four-tile family saved publication, packaged GUI, physical readback/inverse and fault cases; recorded evidence is not rerun by this matrix."),
        new("r08-runtime", NativeMobyEvidenceKind.RecordedValidation, "docs/research/native-fresh-tiled-family-r01-runtime.md", "Exact 689fcf image full-US-firmware LAB65 witness: Wizard defeat/reward, chest20/20, outer-edge fall/reset and post-reset movement. Not arbitrary history or complete sound/trajectory proof."),
        new("c2-next", NativeMobyEvidenceKind.CodeContract, "docs/research/next-native-moby-family-evidence.md", "Ordinary chest properties: observed zero windows and next-pointer distances are not allocation/serialization schemas.")
    });

    private static IReadOnlyList<NativeMobyDependencyObligation> Obligations() => Freeze(new NativeMobyDependencyObligation[]
    {
        new("model-package-and-relocation", "Prove complete owned model/animation intervals, internal references and relocation policy. PETE root adjacency supplies no size.", Freeze(new[]{"pete", "registration"})),
        new("texture-clut-vram", "Decode all model/frame descriptors, texture/CLUT dependencies and destination ownership/allocation.", Freeze(new[]{"town-chest", "wizard-assets"})),
        new("behavior-overlay-and-globals", "Close initializer/update/callback code, calls, address domains and mutable globals for the selected destination.", Freeze(new[]{"town-chest", "wizard-dark"})),
        new("properties-pointers-and-instance-ownership", "Derive the native property extent/schema, aliases, fixups, row capacity and independent mutable allocation.", Freeze(new[]{"instance", "c2-next"})),
        new("routes-links-and-collision", "Decode routes/owner links and certify authored placement, collision response and required trajectory behavior.", Freeze(new[]{"instance", "wizard-dark"})),
        new("sound-and-spu", "Close local/global sound descriptors, controls, exact-resident reuse and SPU allocation/lifetime.", Freeze(new[]{"town-chest", "wizard-assets"})),
        new("children-effects-and-rewards", "Close emitted classes, particles/debris, selector values, reward ownership and target accounting.", Freeze(new[]{"town-chest", "wizard-dark"})),
        new("player-and-shared-resources", "Resolve player animation, shared model and other global dependencies without overwriting special stream/registration ownership.", Freeze(new[]{"pete", "registration", "wizard-assets"})),
        new("persistent-state-and-reentry", "Prove save/death/reentry owner allocation, singleton behavior and duplicate-reward handling for the exact family.", Freeze(new[]{"instance", "r08-runtime"})),
        new("ram-vram-spu-and-dynamic-capacity", "Prove runtime memory and lifetime capacity; free registration slots are not RAM/VRAM/SPU or dynamic-pool capacity.", Freeze(new[]{"registration", "wizard-dark"})),
        new("physical-output-and-runtime", "Use the existing exact-profile publisher/readback/inverse and exact-disc gameplay gates for a new candidate.", Freeze(new[]{"r08", "r08-runtime"}))
    });

    private static IReadOnlyList<NativeMobyScopedCapability> CapabilityIndex(NativeMobyCatalogueEntry[] entries, bool exactSource)
    {
        NativeMobyMatrixDonor[] chest = [new("peacekeepers", 78, 0xAD), new("peacekeepers", 79, 0xAE),
            ..new[]{118,119,120,121,122}.Select(i => new NativeMobyMatrixDonor("peacekeepers", i, 0xD))];
        NativeMobyMatrixDonor wizard = new("wizardpeak", 6, 0x11B), grass = new("artisans", 121, 0x1F5);
        List<NativeMobyScopedCapability> result = [];
        Add("town-chest-v2", NativeTownSquareLockedChestClosureCompiler.Profile, null, "Fixed Town Square V2 recipe; known occupied-site conflict remains.", "townsquare", [0xAD,0xAE,0xD,0x53,0x54], chest,
            ["Seven-row native family dependency graph and corrected private properties/sound ownership; exact source-to-final preservation and publication/readback contracts."],
            ["V2 placement conflict; runtime acceptance cannot be borrowed from V3 or another destination.", "Two independent native chest families remain excluded by singleton globals."], ["town-chest", "instance", "registration"]);
        Add("town-chest-v3", NativeTownSquareClosurePlacement.ClearProfile, null, "Fixed spatially separated Town Square V3 recipe.", "townsquare", [0xAD,0xAE,0xD,0x53,0x54], chest,
            ["Source-bound seven-row family, private allocation, append-only model registration, dry support/center separation and full physical readback contract."],
            ["No arbitrary actor, source variant, destination, mesh envelope or independent multiple-chest approval."], ["town-chest", "instance", "registration"]);
        Add("wizard-source-assets", "native-wizard33-T6-source-form-assets-v1", null, "Detached exact Wizard Peak T6 source assets; no destination selected.", null, [0x11B,0x26], [wizard],
            ["26 immutable source owners; two model packages, player animation8, particle7, texture/palette groups and sound requirements."],
            ["Destination installer/behavior/private state/VRAM/SPU/player bindings; global sound26 exact-resident requirement."], ["wizard-assets"]);
        Add("grass-source-assets", NativeMobyFamilyAssetPlan.ProfileId, null, "Detached exact Artisans T121 source assets; no destination selected.", null, [0x1F5], [grass],
            ["Exact 0x174 model package, row, eight-byte source properties and passive dispatch source contract."],
            ["Destination binding, scene allocation and runtime acceptance remain separate."], ["grass-assets"]);
        Add("dark-wizard-fixed", NativeDarkHollowWizardResearchCandidatePlan.Profile, null, "Fixed Dark Hollow dry-corridor research recipe.", "darkhollow", [0x11B,0x26], [wizard],
            ["Source-issued player animation, texture, sound, particle, behavior/overlay, appended scene, reward and whole-disc recipe."],
            ["Complete transitive closure, peak memory, body/route behavior and new runtime acceptance are not inferred from the candidate contract."], ["wizard-dark", "wizard-assets"]);
        Add("r08-tiled-family", NativeFreshSavedLevelResearchCandidatePlan.ProfileId,
            "schema5-four1536-tile-r03-wizard-grass-locked-chest-p9-n32", "Authored LAB65 exact four1536-tile/two-material R03 family; not a retail-target cell.", null,
            [0x11B,0x26,0x1F5,0xAD,0xAE,0xD,0x53,0x54,0x56], [wizard,grass,..chest],
            ["Recorded saved/editor publication, complete physical readback and inverse to Original.",
             "Recorded exact-disc full-firmware witness: Wizard10 treasure plus chest10,20/20, outer-edge death/reset, retained treasure and post-reset movement."],
            ["Only the exact canonical profile is admitted; arbitrary family/terrain/destination changes remain authored-only.",
             "Complete dependency closure, native persistent-save ownership, universal trajectory/peak-memory/audio proof and normal Create BIN flags remain false.",
             "Key pickup occurred in an unsampled interval; second Wizard kill after reset and every RNG history were not tested."],
            ["r08", "r08-runtime", "wizard-assets", "grass-assets", "instance"],
            "689fcf296e26d635d9c5ce6fc3fc687c2b47decce10ade2a0903f0c6bd877192");
        return Freeze(result);

        void Add(string id, string profile, string? variant, string destination, string? retail, ushort[] classes,
            NativeMobyMatrixDonor[] donors, string[] capabilities, string[] pending, string[] evidence, string? image = null)
        {
            var matched = exactSource ? donors.Select(d => entries.Single(e => e.Kind == NativeMobyCatalogueEntryKind.PlacedRow &&
                e.SourceLevelKey == d.LevelKey && e.TrueIndex == d.TrueIndex && e.ActorId == d.ActorId).SourceVariantId).ToArray() : [];
            result.Add(new(id, profile, variant, destination, retail, Freeze(classes.Order()), Freeze(donors), Freeze(matched),
                Freeze(capabilities), Freeze(pending), Freeze(evidence), image));
        }
    }
}
