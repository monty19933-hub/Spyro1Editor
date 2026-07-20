using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Analysis;

public enum SpecialChestFamily
{
    LockedChest,
    LifeChest,
    SpringChest,
    FireworkChest,
    MultiGemChest,
    ArmoredChest
}

public enum SpecialChestBundleEvidenceKind
{
    NativeRetail,
    StaticCandidate,
    RuntimeRejected,
    DependencyClosureIncomplete,
    RuntimeProven
}

public enum SpecialChestBundleAvailability
{
    NativeClosurePresent,
    CandidatePlanOnly,
    Blocked,
    NormalCreateBinReady
}

public enum SpecialChestDependencyKind
{
    ActorPackage,
    BehaviorHandler,
    InstanceProperties,
    SpawnedActorClosure,
    TextureAndClut,
    RewardLifecycle,
    ScenePointerFixups,
    ExecutableTreasureTarget,
    DamagePredicate,
    AudioVisualEffects
}

public enum SpecialChestComponentEvidence
{
    Unmapped,
    NativeObserved,
    StaticMapped,
    RuntimeProven
}

public enum SpecialChestPrivateAllocationKind
{
    SourceRows,
    SceneProperties,
    ScenePointerFixups,
    RuntimeProperties,
    OverlayCode,
    ActorPackages,
    ExecutableCode,
    TextureAndPalette
}

public enum SpecialChestResourceKind
{
    TexturePixels,
    PaletteClut,
    AudioVisualEffects,
    RewardModel
}

public sealed record SpecialChestDiscFingerprint(
    string Id,
    string Region,
    string ExecutableName,
    string ImageSha256)
{
    public void Validate()
    {
        RequireText(Id, nameof(Id));
        RequireText(Region, nameof(Region));
        RequireText(ExecutableName, nameof(ExecutableName));
        if (ImageSha256.Length != 64 || ImageSha256.Any(character => !Uri.IsHexDigit(character)))
            throw new InvalidDataException($"Disc fingerprint '{Id}' does not contain a SHA-256 digest.");
    }

    private static void RequireText(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidDataException($"Special-chest disc fingerprint field '{label}' is empty.");
    }
}

public readonly record struct SpecialChestBundleProfileKey(
    SpecialChestFamily Family,
    string TargetLevelKey,
    string DiscImageSha256)
{
    public static SpecialChestBundleProfileKey Create(
        SpecialChestFamily family,
        string targetLevelKey,
        string discImageSha256) =>
        new(
            family,
            LevelCatalog.NormalizeKey(targetLevelKey),
            (discImageSha256 ?? "").Trim().ToLowerInvariant());
}

public sealed record SpecialChestFamilyDefinition(
    SpecialChestFamily Family,
    string DisplayName,
    ushort RootActorId,
    IReadOnlyList<ushort> ActorClosure,
    IReadOnlyList<string> ReferenceDonorLevelKeys,
    string Description);

public sealed record SpecialChestDependencyRequirement(
    string Id,
    SpecialChestDependencyKind Kind,
    bool Required,
    string Note);

public sealed record SpecialChestRowDeclaration(
    string Id,
    string Role,
    string DonorLevelKey,
    int? DonorTrueIndex,
    int? TargetTrueIndex,
    ushort? ActorId,
    bool Hidden,
    int? RowCount,
    SpecialChestComponentEvidence Evidence,
    string Note);

public sealed record SpecialChestActorPackageDeclaration(
    string Id,
    ushort ActorId,
    string DonorLevelKey,
    bool Imported,
    string PackageOrSharedRoot,
    SpecialChestComponentEvidence Evidence,
    string Note);

public sealed record SpecialChestRootRegistrationDeclaration(
    string Id,
    ushort ActorId,
    string RegistrationOrDispatch,
    SpecialChestComponentEvidence Evidence,
    string Note);

public sealed record SpecialChestCodeDeclaration(
    string Id,
    string Handler,
    IReadOnlyList<string> SharedCalls,
    IReadOnlyList<string> RebaseSites,
    SpecialChestComponentEvidence Evidence,
    string Note);

public sealed record SpecialChestPrivateAllocationDeclaration(
    string Id,
    SpecialChestPrivateAllocationKind Kind,
    string Region,
    int? SizeBytes,
    int? AlignmentBytes,
    SpecialChestComponentEvidence Evidence,
    string Note);

public sealed record SpecialChestResourceDeclaration(
    string Id,
    SpecialChestResourceKind Kind,
    string DonorLevelKey,
    int? SizeBytes,
    SpecialChestComponentEvidence Evidence,
    string Note);

public sealed record SpecialChestRewardDeclaration(
    string Id,
    int? TreasureDelta,
    int? LifeDelta,
    bool OneShotRequired,
    SpecialChestComponentEvidence Evidence,
    string Note);

public sealed record SpecialChestIncompatibilityDeclaration(
    string Id,
    string Condition,
    string Note);

/// <summary>
/// Declarative closure for one family/destination profile.  Unmapped entries
/// are intentional evidence: they keep a profile research-only until every
/// target-specific row, registration, code path, allocation, resource, and
/// reward dependency has been mapped and runtime-proven.
/// </summary>
public sealed record SpecialChestBundleDeclaration(
    IReadOnlyList<SpecialChestRowDeclaration> Rows,
    IReadOnlyList<SpecialChestActorPackageDeclaration> ActorPackages,
    IReadOnlyList<SpecialChestRootRegistrationDeclaration> RootRegistrations,
    IReadOnlyList<SpecialChestCodeDeclaration> Code,
    IReadOnlyList<SpecialChestPrivateAllocationDeclaration> PrivateAllocations,
    IReadOnlyList<SpecialChestResourceDeclaration> Resources,
    SpecialChestRewardDeclaration Reward,
    IReadOnlyList<SpecialChestIncompatibilityDeclaration> Incompatibilities)
{
    public IEnumerable<SpecialChestComponentEvidence> ComponentEvidence()
    {
        foreach (SpecialChestRowDeclaration row in Rows)
            yield return row.Evidence;
        foreach (SpecialChestActorPackageDeclaration package in ActorPackages)
            yield return package.Evidence;
        foreach (SpecialChestRootRegistrationDeclaration registration in RootRegistrations)
            yield return registration.Evidence;
        foreach (SpecialChestCodeDeclaration code in Code)
            yield return code.Evidence;
        foreach (SpecialChestPrivateAllocationDeclaration allocation in PrivateAllocations)
            yield return allocation.Evidence;
        foreach (SpecialChestResourceDeclaration resource in Resources)
            yield return resource.Evidence;
        yield return Reward.Evidence;
    }

    public void Validate(string profileId)
    {
        ValidateUnique(Rows.Select(row => row.Id), "row");
        ValidateUnique(ActorPackages.Select(package => package.Id), "actor-package");
        ValidateUnique(RootRegistrations.Select(registration => registration.Id), "root-registration");
        ValidateUnique(Code.Select(code => code.Id), "code");
        ValidateUnique(PrivateAllocations.Select(allocation => allocation.Id), "private-allocation");
        ValidateUnique(Resources.Select(resource => resource.Id), "resource");
        ValidateUnique(Incompatibilities.Select(incompatibility => incompatibility.Id), "incompatibility");

        if (Rows.Count == 0 || ActorPackages.Count == 0 || RootRegistrations.Count == 0 || Code.Count == 0 ||
            PrivateAllocations.Count == 0 || Resources.Count == 0 || Incompatibilities.Count == 0)
        {
            throw new InvalidDataException($"Special-chest profile '{profileId}' has an incomplete declarative bundle closure.");
        }

        if (Rows.Any(row => string.IsNullOrWhiteSpace(row.Role) || string.IsNullOrWhiteSpace(row.DonorLevelKey) ||
                string.IsNullOrWhiteSpace(row.Note) || row.DonorTrueIndex < 0 || row.TargetTrueIndex < 0 || row.RowCount is <= 0))
        {
            throw new InvalidDataException($"Special-chest profile '{profileId}' has an invalid row declaration.");
        }
        if (ActorPackages.Any(package => string.IsNullOrWhiteSpace(package.DonorLevelKey) ||
                string.IsNullOrWhiteSpace(package.PackageOrSharedRoot) || string.IsNullOrWhiteSpace(package.Note)))
        {
            throw new InvalidDataException($"Special-chest profile '{profileId}' has an invalid actor-package declaration.");
        }
        if (RootRegistrations.Any(registration => string.IsNullOrWhiteSpace(registration.RegistrationOrDispatch) ||
                string.IsNullOrWhiteSpace(registration.Note)))
        {
            throw new InvalidDataException($"Special-chest profile '{profileId}' has an invalid root-registration declaration.");
        }
        if (Code.Any(code => string.IsNullOrWhiteSpace(code.Handler) || string.IsNullOrWhiteSpace(code.Note) ||
                code.SharedCalls.Any(string.IsNullOrWhiteSpace) || code.RebaseSites.Any(string.IsNullOrWhiteSpace)))
        {
            throw new InvalidDataException($"Special-chest profile '{profileId}' has an invalid code declaration.");
        }
        if (PrivateAllocations.Any(allocation => string.IsNullOrWhiteSpace(allocation.Region) ||
                string.IsNullOrWhiteSpace(allocation.Note) || allocation.SizeBytes < 0 || allocation.AlignmentBytes is <= 0))
        {
            throw new InvalidDataException($"Special-chest profile '{profileId}' has an invalid private-allocation declaration.");
        }
        if (Resources.Any(resource => string.IsNullOrWhiteSpace(resource.DonorLevelKey) ||
                string.IsNullOrWhiteSpace(resource.Note) || resource.SizeBytes < 0))
        {
            throw new InvalidDataException($"Special-chest profile '{profileId}' has an invalid resource declaration.");
        }
        if (string.IsNullOrWhiteSpace(Reward.Id) || string.IsNullOrWhiteSpace(Reward.Note) ||
            Reward.TreasureDelta < 0 || Reward.LifeDelta < 0)
        {
            throw new InvalidDataException($"Special-chest profile '{profileId}' has an invalid reward declaration.");
        }
        if (Incompatibilities.Any(incompatibility => string.IsNullOrWhiteSpace(incompatibility.Condition) ||
                string.IsNullOrWhiteSpace(incompatibility.Note)))
        {
            throw new InvalidDataException($"Special-chest profile '{profileId}' has an invalid incompatibility declaration.");
        }

        void ValidateUnique(IEnumerable<string> identifiers, string kind)
        {
            string[] values = identifiers.ToArray();
            if (values.Any(string.IsNullOrWhiteSpace) || values.Distinct(StringComparer.OrdinalIgnoreCase).Count() != values.Length)
                throw new InvalidDataException($"Special-chest profile '{profileId}' has an empty or duplicate {kind} declaration.");
        }
    }
}

public sealed record SpecialChestStructuralBudget(
    int? SourceRowsAdded,
    int? RuntimeSlotsConsumed,
    int? TreasureDelta,
    int? WadGrowthBytes,
    int? PointerFixupsAdded,
    bool RelocatesExecutable,
    int? MaxBundleInstances,
    bool Exact)
{
    public static SpecialChestStructuralBudget Unknown { get; } = new(
        SourceRowsAdded: null,
        RuntimeSlotsConsumed: null,
        TreasureDelta: null,
        WadGrowthBytes: null,
        PointerFixupsAdded: null,
        RelocatesExecutable: false,
        MaxBundleInstances: null,
        Exact: false);

    public void Validate(string profileId)
    {
        ValidateNonNegative(SourceRowsAdded, nameof(SourceRowsAdded));
        ValidateNonNegative(RuntimeSlotsConsumed, nameof(RuntimeSlotsConsumed));
        ValidateNonNegative(TreasureDelta, nameof(TreasureDelta));
        ValidateNonNegative(WadGrowthBytes, nameof(WadGrowthBytes));
        ValidateNonNegative(PointerFixupsAdded, nameof(PointerFixupsAdded));
        if (MaxBundleInstances is <= 0)
            throw new InvalidDataException($"Special-chest profile '{profileId}' has a non-positive bundle-instance limit.");
        if (Exact && (SourceRowsAdded == null || RuntimeSlotsConsumed == null || TreasureDelta == null || WadGrowthBytes == null || MaxBundleInstances == null))
            throw new InvalidDataException($"Special-chest profile '{profileId}' claims an exact structural budget with unknown required fields.");

        void ValidateNonNegative(int? value, string label)
        {
            if (value < 0)
                throw new InvalidDataException($"Special-chest profile '{profileId}' has a negative {label} budget.");
        }
    }
}

public sealed record SpecialChestBundleConstraints(
    bool AtomicBundle,
    bool DisallowOtherObjectEdits,
    bool DisallowOtherStructuralRelocations,
    bool RequiresCleanRetailFingerprint,
    bool RequiresFreshRuntimeSmoke);

public sealed record SpecialChestBundleProfile(
    string Id,
    SpecialChestFamily Family,
    string TargetLevelKey,
    SpecialChestDiscFingerprint Disc,
    SpecialChestBundleEvidenceKind Evidence,
    SpecialChestBundleAvailability Availability,
    bool NativeFamilyPresent,
    string SourceLevelKey,
    IReadOnlyList<ushort> ActorClosure,
    IReadOnlyList<SpecialChestDependencyRequirement> Dependencies,
    SpecialChestStructuralBudget StructuralBudget,
    SpecialChestBundleConstraints Constraints,
    SpecialChestBundleDeclaration Declaration,
    string RecipeId,
    string RequiredExporterFeature,
    string EvidenceNote,
    IReadOnlyList<string> RequiredWork,
    string RuntimeProofOutputSha256 = "")
{
    public SpecialChestBundleProfileKey Key =>
        SpecialChestBundleProfileKey.Create(Family, TargetLevelKey, Disc.ImageSha256);

    public bool NormalCreateBinReady =>
        Availability == SpecialChestBundleAvailability.NormalCreateBinReady;

    public bool CandidatePlanOnly =>
        Availability == SpecialChestBundleAvailability.CandidatePlanOnly;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id))
            throw new InvalidDataException("A special-chest profile id is empty.");
        if (string.IsNullOrWhiteSpace(TargetLevelKey) || TargetLevelKey != LevelCatalog.NormalizeKey(TargetLevelKey))
            throw new InvalidDataException($"Special-chest profile '{Id}' does not use a normalized target-level key.");
        Disc.Validate();
        if (ActorClosure.Count == 0 || ActorClosure.Distinct().Count() != ActorClosure.Count)
            throw new InvalidDataException($"Special-chest profile '{Id}' has an empty or duplicate actor closure.");
        if (Dependencies.Count == 0 || Dependencies.Select(dependency => dependency.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != Dependencies.Count)
            throw new InvalidDataException($"Special-chest profile '{Id}' has an empty or duplicate dependency list.");
        if (Dependencies.Any(dependency => string.IsNullOrWhiteSpace(dependency.Id) || string.IsNullOrWhiteSpace(dependency.Note)))
            throw new InvalidDataException($"Special-chest profile '{Id}' has an incomplete dependency declaration.");
        if (string.IsNullOrWhiteSpace(EvidenceNote))
            throw new InvalidDataException($"Special-chest profile '{Id}' has no evidence note.");
        StructuralBudget.Validate(Id);
        Declaration.Validate(Id);

        if (StructuralBudget.TreasureDelta is int treasureDelta &&
            Declaration.Reward.TreasureDelta is int declaredTreasureDelta &&
            treasureDelta != declaredTreasureDelta)
        {
            throw new InvalidDataException($"Special-chest profile '{Id}' has conflicting structural and reward treasure deltas.");
        }

        if (NativeFamilyPresent != (Availability == SpecialChestBundleAvailability.NativeClosurePresent))
            throw new InvalidDataException($"Special-chest profile '{Id}' has inconsistent native-family availability.");
        if (NativeFamilyPresent && Evidence != SpecialChestBundleEvidenceKind.NativeRetail)
            throw new InvalidDataException($"Native special-chest profile '{Id}' must use native-retail evidence.");
        if (Availability == SpecialChestBundleAvailability.CandidatePlanOnly && Evidence != SpecialChestBundleEvidenceKind.StaticCandidate)
            throw new InvalidDataException($"Plan-only special-chest profile '{Id}' must use static-candidate evidence.");
        if (Availability == SpecialChestBundleAvailability.Blocked && RequiredWork.Count == 0)
            throw new InvalidDataException($"Blocked special-chest profile '{Id}' must state the work needed to unblock it.");

        if (NormalCreateBinReady)
        {
            if (Evidence != SpecialChestBundleEvidenceKind.RuntimeProven || string.IsNullOrWhiteSpace(RecipeId) ||
                string.IsNullOrWhiteSpace(RequiredExporterFeature) || !IsSha256(RuntimeProofOutputSha256))
            {
                throw new InvalidDataException($"Normal-build profile '{Id}' lacks exact runtime-proof and exporter evidence.");
            }
            if (Declaration.ComponentEvidence().Any(evidence => evidence != SpecialChestComponentEvidence.RuntimeProven))
                throw new InvalidDataException($"Normal-build profile '{Id}' contains an unproven declarative bundle component.");
        }
        else if (!string.IsNullOrWhiteSpace(RuntimeProofOutputSha256))
        {
            throw new InvalidDataException($"Unproven special-chest profile '{Id}' supplies a runtime-proof output digest.");
        }
    }

    private static bool IsSha256(string value) =>
        value.Length == 64 && value.All(Uri.IsHexDigit);
}

/// <summary>
/// Checked coverage for the six requested special-chest families on the clean
/// USA retail disc.  Every family/regular-level pair is present.  Native retail
/// presence is deliberately not treated as proof that an arbitrary new copy is
/// safe; the Artisans Key + Locked Chest V2 pair is the sole normal-build-ready
/// transplant profile.
/// </summary>
public static class SpecialChestBundleProfileRegistry
{
    public const string CleanUsaImageSha256 = "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37";
    public const string ArtisansLockedChestProofOutputSha256 = "67b82ae66851d770b99d5e2d78400671268c31ad62b1815163e63324adc07d34";

    public static SpecialChestDiscFingerprint CleanUsaRetailDisc { get; } = new(
        Id: "spyro1-usa-retail-scus-94228",
        Region: "USA",
        ExecutableName: "SCUS_942.28",
        ImageSha256: CleanUsaImageSha256);

    private static readonly (string Key, string Name)[] RegularLevels =
    [
        ("artisans", "Artisans"),
        ("stonehill", "Stone Hill"),
        ("darkhollow", "Dark Hollow"),
        ("townsquare", "Town Square"),
        ("toasty", "Toasty"),
        ("peacekeepers", "Peace Keepers"),
        ("drycanyon", "Dry Canyon"),
        ("clifftown", "Cliff Town"),
        ("icecavern", "Ice Cavern"),
        ("doctorshemp", "Doctor Shemp"),
        ("magiccrafters", "Magic Crafters"),
        ("alpineridge", "Alpine Ridge"),
        ("highcaves", "High Caves"),
        ("wizardpeak", "Wizard Peak"),
        ("blowhard", "Blowhard"),
        ("beastmakers", "Beast Makers"),
        ("terracevillage", "Terrace Village"),
        ("mistybog", "Misty Bog"),
        ("treetops", "Tree Tops"),
        ("metalhead", "Metalhead"),
        ("dreamweavers", "Dream Weavers"),
        ("darkpassage", "Dark Passage"),
        ("loftycastle", "Lofty Castle"),
        ("hauntedtowers", "Haunted Towers"),
        ("jacques", "Jacques"),
        ("gnastysworld", "Gnasty's World"),
        ("gnorccove", "Gnorc Cove"),
        ("twilightharbor", "Twilight Harbor"),
        ("gnastygnorc", "Gnasty Gnorc"),
        ("gnastysloot", "Gnasty's Loot")
    ];

    private static readonly IReadOnlyList<SpecialChestFamilyDefinition> FamilyDefinitions =
    [
        new(
            SpecialChestFamily.LockedChest,
            "Key + Locked Chest",
            0x00AE,
            [0x00AD, 0x00AE, 0x000D],
            ["peacekeepers"],
            "Key-gated locked chest, hidden native reward markers, one-shot treasure lifecycle, handler/model, and private texture closure."),
        new(
            SpecialChestFamily.LifeChest,
            "Life Chest",
            0x01A5,
            [0x01A5, 0x01A6, 0x000E],
            ["artisans", "peacekeepers"],
            "Life Chest shell plus spawned butterfly/statue-orb reward closure and unique runtime properties storage."),
        new(
            SpecialChestFamily.SpringChest,
            "Spring Chest",
            0x0149,
            [0x0149, 0x0043, 0x0044, 0x0045],
            ["peacekeepers", "townsquare"],
            "Spring Chest handler, real pop motion, fragment actors, SpawnMoby initialization, reward, texture, and cleanup closure."),
        new(
            SpecialChestFamily.FireworkChest,
            "Firework Chest",
            0x0138,
            [0x0138],
            ["gnastysloot"],
            "Firework shell plus fuse, rocket/explosion children, target collision, reward, texture, and cleanup closure."),
        new(
            SpecialChestFamily.MultiGemChest,
            "3x Flame / Multi-hit Chest",
            0x0186,
            [0x0186],
            ["townsquare", "gnastysloot"],
            "Three-hit chest shell plus linked state/controller rows, staged hit effects, reward spawn, texture, and cleanup closure."),
        new(
            SpecialChestFamily.ArmoredChest,
            "Armored Chest",
            0x0191,
            [0x0191],
            ["peacekeepers", "terracevillage", "hauntedtowers"],
            "Armored chest handler/model, normal-hit rejection, supercharge/superflame acceptance, effects, reward, and linked-control closure.")
    ];

    private static readonly IReadOnlyDictionary<SpecialChestFamily, HashSet<string>> MissingNativeFamily =
        new Dictionary<SpecialChestFamily, HashSet<string>>
        {
            [SpecialChestFamily.LockedChest] = Set(
                "artisans", "townsquare", "toasty", "clifftown", "alpineridge", "highcaves", "wizardpeak", "blowhard",
                "terracevillage", "mistybog", "dreamweavers", "darkpassage", "hauntedtowers", "gnastysworld",
                "twilightharbor", "gnastygnorc", "gnastysloot"),
            [SpecialChestFamily.LifeChest] = Set(
                "toasty", "doctorshemp", "alpineridge", "highcaves", "blowhard", "gnastysworld", "gnastygnorc", "gnastysloot"),
            [SpecialChestFamily.SpringChest] = Set(
                "artisans", "stonehill", "darkhollow", "toasty", "clifftown", "gnastysworld"),
            [SpecialChestFamily.FireworkChest] = Set(
                "artisans", "stonehill", "darkhollow", "townsquare", "toasty", "peacekeepers", "clifftown", "icecavern",
                "doctorshemp", "magiccrafters", "wizardpeak", "terracevillage", "mistybog", "treetops", "jacques",
                "gnastysworld", "twilightharbor"),
            [SpecialChestFamily.MultiGemChest] = Set(
                "artisans", "stonehill", "darkhollow", "toasty", "peacekeepers", "clifftown", "icecavern", "doctorshemp",
                "blowhard", "metalhead", "dreamweavers", "darkpassage", "jacques", "gnastysworld", "gnorccove",
                "twilightharbor", "gnastygnorc"),
            [SpecialChestFamily.ArmoredChest] = Set(
                "artisans", "stonehill", "darkhollow", "townsquare", "toasty", "drycanyon", "icecavern", "doctorshemp",
                "alpineridge", "highcaves", "blowhard", "beastmakers", "mistybog", "metalhead", "dreamweavers",
                "loftycastle", "jacques", "gnastysworld", "gnastygnorc")
        };

    private static readonly IReadOnlyDictionary<SpecialChestFamily, IReadOnlyList<SpecialChestDependencyRequirement>> Dependencies =
        new Dictionary<SpecialChestFamily, IReadOnlyList<SpecialChestDependencyRequirement>>
        {
            [SpecialChestFamily.LockedChest] =
            [
                Required("key-and-shell-packages", SpecialChestDependencyKind.ActorPackage, "Actors 0x00AD and 0x00AE must retain their checked model and actor packages."),
                Required("locked-handler", SpecialChestDependencyKind.BehaviorHandler, "The target overlay must dispatch the native key gate, unlock, death, and retirement paths."),
                Required("reward-marker-group", SpecialChestDependencyKind.SpawnedActorClosure, "Five hidden translated marker rows and matching scene properties feed the six-gem reward."),
                Required("private-texture-clut", SpecialChestDependencyKind.TextureAndClut, "Key and locked-chest pixels, palettes, and descriptors must not alias target content."),
                Required("one-shot-reward", SpecialChestDependencyKind.RewardLifecycle, "Key consumption, six gems, +10 treasure, and persistent retirement must occur once."),
                Required("treasure-target", SpecialChestDependencyKind.ExecutableTreasureTarget, "The executable level treasure target must increase by the exact reward delta."),
                Required("scene-fixups", SpecialChestDependencyKind.ScenePointerFixups, "Expanded source rows, reward properties, and pointer fixups must remain coherent.")
            ],
            [SpecialChestFamily.LifeChest] =
            [
                Required("life-shell-package", SpecialChestDependencyKind.ActorPackage, "Actor 0x01A5 needs its native shell model and behavior package."),
                Required("life-handler", SpecialChestDependencyKind.BehaviorHandler, "The target overlay must dispatch the native shell, break, child-spawn, reward, and retirement paths."),
                Required("life-child-closure", SpecialChestDependencyKind.SpawnedActorClosure, "Actors 0x01A6 and 0x000E cover the spawned butterfly/statue-orb reward path."),
                Required("private-runtime-properties", SpecialChestDependencyKind.InstanceProperties, "Each Life Chest requires an independent relocated runtime-properties block."),
                Required("life-reward", SpecialChestDependencyKind.RewardLifecycle, "The life increment, visible reward, cleanup, and persistence must execute exactly once."),
                Required("life-textures", SpecialChestDependencyKind.TextureAndClut, "Shell and spawned reward textures/CLUTs must be mapped without target collisions.")
            ],
            [SpecialChestFamily.SpringChest] =
            [
                Required("spring-package", SpecialChestDependencyKind.ActorPackage, "Actor 0x0149 needs its complete model and controller data."),
                Required("spring-handler", SpecialChestDependencyKind.BehaviorHandler, "The real native interaction/pop/update path must be transplanted and rebased."),
                Required("spring-fragments", SpecialChestDependencyKind.SpawnedActorClosure, "Fragment actors 0x0043, 0x0044, and 0x0045 plus SpawnMoby initialization are required."),
                Required("spring-reward", SpecialChestDependencyKind.RewardLifecycle, "The gem must become visible, collectible, and retired exactly once."),
                Required("spring-effects", SpecialChestDependencyKind.AudioVisualEffects, "Native motion, sound, particles, and cleanup must remain synchronized."),
                Required("spring-textures", SpecialChestDependencyKind.TextureAndClut, "Shell, fragments, reward, pixels, CLUTs, and descriptors need target-safe allocation.")
            ],
            [SpecialChestFamily.FireworkChest] =
            [
                Required("firework-package", SpecialChestDependencyKind.ActorPackage, "Actor 0x0138 needs its shell package and model."),
                Required("firework-handler", SpecialChestDependencyKind.BehaviorHandler, "Fuse, launch, impact, and cleanup code must be transplanted and rebased."),
                Required("rocket-explosion-closure", SpecialChestDependencyKind.SpawnedActorClosure, "All spawned rocket and explosion children must be identified and installed."),
                Required("firework-collision", SpecialChestDependencyKind.DamagePredicate, "Explosion collision must preserve native chest-target interactions."),
                Required("firework-reward", SpecialChestDependencyKind.RewardLifecycle, "Reward and one-shot retirement must survive reload and re-entry."),
                Required("firework-effects", SpecialChestDependencyKind.AudioVisualEffects, "Fuse, rocket, explosion, sound, particles, and textures must remain synchronized.")
            ],
            [SpecialChestFamily.MultiGemChest] =
            [
                Required("multi-hit-package", SpecialChestDependencyKind.ActorPackage, "Actor 0x0186 needs its model plus any linked/controller actor roots."),
                Required("multi-hit-handler", SpecialChestDependencyKind.BehaviorHandler, "The exact three-state damage and transition handler must be installed."),
                Required("multi-hit-controller", SpecialChestDependencyKind.SpawnedActorClosure, "Town Square's linked companion/control role must be represented as part of the atomic group."),
                Required("multi-hit-state", SpecialChestDependencyKind.InstanceProperties, "Each hit stage requires independent state and checked pointer fixups."),
                Required("multi-hit-reward", SpecialChestDependencyKind.RewardLifecycle, "Exactly three valid hits must lead to one reward and permanent cleanup."),
                Required("multi-hit-effects", SpecialChestDependencyKind.AudioVisualEffects, "Stage visuals, sounds, particles, and textures must match native behavior.")
            ],
            [SpecialChestFamily.ArmoredChest] =
            [
                Required("armored-package", SpecialChestDependencyKind.ActorPackage, "Actor 0x0191 needs its shell/model and any linked-control packages."),
                Required("armored-handler", SpecialChestDependencyKind.BehaviorHandler, "The target overlay needs the complete armored-chest update and break path."),
                Required("armored-damage-gate", SpecialChestDependencyKind.DamagePredicate, "Normal flame/charge rejection and supercharge/superflame acceptance must remain exact."),
                Required("armored-interactions", SpecialChestDependencyKind.SpawnedActorClosure, "Native firework/cannon/supercharge interactions and linked rows must remain connected."),
                Required("armored-reward", SpecialChestDependencyKind.RewardLifecycle, "A valid special hit must produce one reward and persistent retirement."),
                Required("armored-effects", SpecialChestDependencyKind.AudioVisualEffects, "Hit rejection, destruction effects, sound, particles, and textures must remain native.")
            ]
        };

    private static readonly IReadOnlyList<SpecialChestBundleProfile> Profiles = BuildProfiles();
    private static readonly IReadOnlyDictionary<SpecialChestBundleProfileKey, SpecialChestBundleProfile> ProfilesByKey =
        Profiles.ToDictionary(profile => profile.Key);

    static SpecialChestBundleProfileRegistry()
    {
        CleanUsaRetailDisc.Validate();
        foreach (SpecialChestBundleProfile profile in Profiles)
            profile.Validate();

        int expectedProfileCount = RegularLevels.Length * FamilyDefinitions.Count;
        if (Profiles.Count != expectedProfileCount || ProfilesByKey.Count != expectedProfileCount)
            throw new InvalidDataException($"Special-chest registry must contain exactly {expectedProfileCount} unique family/level/disc profiles.");

        foreach (SpecialChestFamilyDefinition family in FamilyDefinitions)
        {
            foreach ((string levelKey, _) in RegularLevels)
            {
                if (!ProfilesByKey.ContainsKey(SpecialChestBundleProfileKey.Create(family.Family, levelKey, CleanUsaImageSha256)))
                    throw new InvalidDataException($"Special-chest registry is missing {family.Family} coverage for '{levelKey}'.");
            }
        }

        SpecialChestBundleProfile[] ready = Profiles.Where(profile => profile.NormalCreateBinReady).ToArray();
        if (ready.Length != 1 || ready[0].Family != SpecialChestFamily.LockedChest || ready[0].TargetLevelKey != "artisans")
            throw new InvalidDataException("The Artisans Key + Locked Chest pair must remain the sole normal-build-ready special-chest transplant.");
    }

    public static IReadOnlyList<string> RegularLevelKeys =>
        RegularLevels.Select(level => level.Key).ToArray();

    public static IReadOnlyList<SpecialChestFamilyDefinition> Families => FamilyDefinitions;

    public static IReadOnlyList<SpecialChestBundleProfile> AllProfiles => Profiles;

    public static SpecialChestFamilyDefinition Definition(SpecialChestFamily family) =>
        FamilyDefinitions.Single(definition => definition.Family == family);

    public static SpecialChestBundleProfile? Find(
        SpecialChestFamily family,
        string targetLevelKey,
        string discImageSha256)
    {
        ProfilesByKey.TryGetValue(
            SpecialChestBundleProfileKey.Create(family, targetLevelKey, discImageSha256),
            out SpecialChestBundleProfile? profile);
        return profile;
    }

    public static IReadOnlyList<SpecialChestBundleProfile> ForLevel(string targetLevelKey) =>
        Profiles
            .Where(profile => string.Equals(profile.TargetLevelKey, LevelCatalog.NormalizeKey(targetLevelKey), StringComparison.OrdinalIgnoreCase))
            .OrderBy(profile => profile.Family)
            .ToArray();

    public static IReadOnlyList<SpecialChestBundleProfile> ForFamily(SpecialChestFamily family) =>
        Profiles.Where(profile => profile.Family == family).ToArray();

    private static IReadOnlyList<SpecialChestBundleProfile> BuildProfiles()
    {
        List<SpecialChestBundleProfile> profiles = [];
        foreach (SpecialChestFamilyDefinition family in FamilyDefinitions)
        {
            HashSet<string> missing = MissingNativeFamily[family.Family];
            foreach ((string levelKey, string levelName) in RegularLevels)
            {
                profiles.Add(!missing.Contains(levelKey)
                    ? BuildNativeProfile(family, levelKey, levelName)
                    : BuildMissingProfile(family, levelKey, levelName));
            }
        }

        return profiles;
    }

    private static SpecialChestBundleProfile BuildNativeProfile(
        SpecialChestFamilyDefinition family,
        string levelKey,
        string levelName) =>
        new(
            Id: $"spyro1.special-chest.{FamilyKey(family.Family)}.{levelKey}.native.v1",
            Family: family.Family,
            TargetLevelKey: levelKey,
            Disc: CleanUsaRetailDisc,
            Evidence: SpecialChestBundleEvidenceKind.NativeRetail,
            Availability: SpecialChestBundleAvailability.NativeClosurePresent,
            NativeFamilyPresent: true,
            SourceLevelKey: levelKey,
            ActorClosure: family.ActorClosure,
            Dependencies: Dependencies[family.Family],
            StructuralBudget: SpecialChestStructuralBudget.Unknown,
            Constraints: new(
                AtomicBundle: true,
                DisallowOtherObjectEdits: false,
                DisallowOtherStructuralRelocations: false,
                RequiresCleanRetailFingerprint: true,
                RequiresFreshRuntimeSmoke: true),
            Declaration: BuildResearchDeclaration(
                family,
                sourceLevelKey: levelKey,
                mappedEvidence: SpecialChestComponentEvidence.NativeObserved,
                treasureDelta: null,
                lifeDelta: family.Family == SpecialChestFamily.LifeChest ? 1 : null,
                disallowOtherObjectEdits: false,
                disallowOtherStructuralRelocations: false,
                maxInstances: null,
                nativeFamilyPresent: true),
            RecipeId: "",
            RequiredExporterFeature: "",
            EvidenceNote: $"{levelName} contains native {family.DisplayName} source/actor evidence on the clean retail disc. This proves the resident dependency closure exists, not that an arbitrary added copy is runtime-safe.",
            RequiredWork:
            [
                "Build a checked same-level instance plan with independent properties/companions and exact source-row accounting.",
                "Pass the family-specific DuckStation add, reward, cleanup, persistence, and nearby-regression smoke before normal Create BIN promotion."
            ]);

    private static SpecialChestBundleProfile BuildMissingProfile(
        SpecialChestFamilyDefinition family,
        string levelKey,
        string levelName)
    {
        if (family.Family == SpecialChestFamily.LockedChest && levelKey == "artisans")
        {
            return new SpecialChestBundleProfile(
                Id: "spyro1.special-chest.locked.artisans.runtime-bundle.v2",
                Family: family.Family,
                TargetLevelKey: levelKey,
                Disc: CleanUsaRetailDisc,
                Evidence: SpecialChestBundleEvidenceKind.RuntimeProven,
                Availability: SpecialChestBundleAvailability.NormalCreateBinReady,
                NativeFamilyPresent: false,
                SourceLevelKey: "peacekeepers",
                ActorClosure: family.ActorClosure,
                Dependencies: Dependencies[family.Family],
                StructuralBudget: new(
                    SourceRowsAdded: ArtisansNativeLockedChestRuntimeBundleComposer.SourceRuntimeCountAfter - ArtisansNativeLockedChestRuntimeBundleComposer.SourceRuntimeCountBefore,
                    RuntimeSlotsConsumed: 6,
                    TreasureDelta: ArtisansNativeLockedChestRuntimeBundleComposer.TreasureTargetAfter - ArtisansNativeLockedChestRuntimeBundleComposer.TreasureTargetBefore,
                    WadGrowthBytes: 0x2000,
                    PointerFixupsAdded: null,
                    RelocatesExecutable: true,
                    MaxBundleInstances: 1,
                    Exact: true),
                Constraints: new(
                    AtomicBundle: true,
                    DisallowOtherObjectEdits: true,
                    DisallowOtherStructuralRelocations: true,
                    RequiresCleanRetailFingerprint: true,
                    RequiresFreshRuntimeSmoke: false),
                Declaration: BuildArtisansLockedChestDeclaration(),
                RecipeId: ArtisansNativeLockedChestRuntimeBundleComposer.RecipeId,
                RequiredExporterFeature: ArtisansNativeLockedChestRuntimeBundleComposer.RequiredExporterFeature,
                EvidenceNote: "The exact Artisans V2 Key + Locked Chest bundle passed DuckStation: gold key, gated unlock, correct textures, six gems worth +10, one-shot death/retirement, and normal nearby behavior. The fixed T174-T180 allocation remains one atomic pair.",
                RequiredWork: [],
                RuntimeProofOutputSha256: ArtisansLockedChestProofOutputSha256);
        }

        if (family.Family == SpecialChestFamily.LockedChest)
        {
            return new SpecialChestBundleProfile(
                Id: $"spyro1.special-chest.locked.{levelKey}.candidate.v1",
                Family: family.Family,
                TargetLevelKey: levelKey,
                Disc: CleanUsaRetailDisc,
                Evidence: SpecialChestBundleEvidenceKind.StaticCandidate,
                Availability: SpecialChestBundleAvailability.CandidatePlanOnly,
                NativeFamilyPresent: false,
                SourceLevelKey: "peacekeepers",
                ActorClosure: family.ActorClosure,
                Dependencies: Dependencies[family.Family],
                StructuralBudget: SpecialChestStructuralBudget.Unknown,
                Constraints: new(
                    AtomicBundle: true,
                    DisallowOtherObjectEdits: true,
                    DisallowOtherStructuralRelocations: true,
                    RequiresCleanRetailFingerprint: true,
                    RequiresFreshRuntimeSmoke: true),
                Declaration: BuildResearchDeclaration(
                    family,
                    sourceLevelKey: "peacekeepers",
                    mappedEvidence: SpecialChestComponentEvidence.StaticMapped,
                    treasureDelta: 10,
                    lifeDelta: null,
                    disallowOtherObjectEdits: true,
                    disallowOtherStructuralRelocations: true,
                    maxInstances: 1,
                    nativeFamilyPresent: false),
                RecipeId: $"spyro1.special-chest.locked.{levelKey}.target-profile.pending",
                RequiredExporterFeature: "",
                EvidenceNote: $"{levelName} lacks native actor 0x00AE. The complete Artisans transplant closure is known, but its fixed addresses and T174-T180 allocation are not portable to this destination.",
                RequiredWork:
                [
                    "Map destination-specific overlay, actor/model, scene/fixup, texture/CLUT, WAD-growth, executable, and source-row allocations.",
                    "Stage a disposable exact-target candidate and pass key gate, six-gem +10 reward, one-shot cleanup, reload, and nearby-regression tests."
                ]);
        }

        SpecialChestBundleEvidenceKind evidence = SpecialChestBundleEvidenceKind.DependencyClosureIncomplete;
        string evidenceNote = $"{levelName} does not contain native {family.DisplayName}; no complete target-safe runtime bundle is mapped.";
        if (family.Family == SpecialChestFamily.SpringChest && levelKey == "artisans")
        {
            evidence = SpecialChestBundleEvidenceKind.RuntimeRejected;
            evidenceNote = "The Artisans spring-chest research layout booted and yielded a collectible reward, but the synthetic stiff pop/disappearance did not reproduce the native handler, fragment, animation, and cleanup path.";
        }
        else if (family.Family == SpecialChestFamily.SpringChest && levelKey == "stonehill")
        {
            evidence = SpecialChestBundleEvidenceKind.RuntimeRejected;
            evidenceNote = "Stone Hill source-row/package candidates were inert or produced invalid nearby/runtime state; they are retained as failed research evidence, not a buildable profile.";
        }

        return new SpecialChestBundleProfile(
            Id: $"spyro1.special-chest.{FamilyKey(family.Family)}.{levelKey}.blocked.v1",
            Family: family.Family,
            TargetLevelKey: levelKey,
            Disc: CleanUsaRetailDisc,
            Evidence: evidence,
            Availability: SpecialChestBundleAvailability.Blocked,
            NativeFamilyPresent: false,
            SourceLevelKey: family.ReferenceDonorLevelKeys[0],
            ActorClosure: family.ActorClosure,
            Dependencies: Dependencies[family.Family],
            StructuralBudget: SpecialChestStructuralBudget.Unknown,
            Constraints: new(
                AtomicBundle: true,
                DisallowOtherObjectEdits: true,
                DisallowOtherStructuralRelocations: true,
                RequiresCleanRetailFingerprint: true,
                RequiresFreshRuntimeSmoke: true),
            Declaration: BuildResearchDeclaration(
                family,
                sourceLevelKey: family.ReferenceDonorLevelKeys[0],
                mappedEvidence: SpecialChestComponentEvidence.Unmapped,
                treasureDelta: null,
                lifeDelta: family.Family == SpecialChestFamily.LifeChest ? 1 : null,
                disallowOtherObjectEdits: true,
                disallowOtherStructuralRelocations: true,
                maxInstances: null,
                nativeFamilyPresent: false),
            RecipeId: "",
            RequiredExporterFeature: "",
            EvidenceNote: evidenceNote,
            RequiredWork: RequiredWork(family, levelName));
    }

    private static SpecialChestBundleDeclaration BuildResearchDeclaration(
        SpecialChestFamilyDefinition family,
        string sourceLevelKey,
        SpecialChestComponentEvidence mappedEvidence,
        int? treasureDelta,
        int? lifeDelta,
        bool disallowOtherObjectEdits,
        bool disallowOtherStructuralRelocations,
        int? maxInstances,
        bool nativeFamilyPresent)
    {
        string normalizedSource = LevelCatalog.NormalizeKey(sourceLevelKey);
        List<SpecialChestRowDeclaration> rows = [];
        if (family.Family == SpecialChestFamily.LockedChest)
        {
            rows.Add(new(
                Id: "visible-key-row",
                Role: "visible key root",
                DonorLevelKey: normalizedSource,
                DonorTrueIndex: normalizedSource == "peacekeepers" ? 78 : null,
                TargetTrueIndex: null,
                ActorId: 0x00AD,
                Hidden: false,
                RowCount: 1,
                Evidence: mappedEvidence,
                Note: nativeFamilyPresent
                    ? "The resident key row is observed; an added instance still needs an independent row/properties allocation."
                    : "The donor key row is identified, but its target row and properties allocation are not runtime-proven."));
            rows.Add(new(
                Id: "visible-locked-chest-row",
                Role: "visible locked-chest root",
                DonorLevelKey: normalizedSource,
                DonorTrueIndex: normalizedSource == "peacekeepers" ? 79 : null,
                TargetTrueIndex: null,
                ActorId: 0x00AE,
                Hidden: false,
                RowCount: 1,
                Evidence: mappedEvidence,
                Note: nativeFamilyPresent
                    ? "The resident locked-chest row is observed; an added instance still needs an independent row/properties allocation."
                    : "The donor locked-chest row is identified, but its target row and properties allocation are not runtime-proven."));
        }
        else
        {
            rows.Add(new(
                Id: "visible-root-row",
                Role: "visible chest root",
                DonorLevelKey: normalizedSource,
                DonorTrueIndex: null,
                TargetTrueIndex: null,
                ActorId: family.RootActorId,
                Hidden: false,
                RowCount: 1,
                Evidence: mappedEvidence,
                Note: nativeFamilyPresent
                    ? "A resident root row is observed, but a duplicate-safe row/properties allocation is not yet proven."
                    : "The family root actor is known, but the exact donor and destination row are not mapped."));
        }

        rows.Add(new(
            Id: "hidden-companion-rows",
            Role: "hidden companion, controller, or reward rows",
            DonorLevelKey: normalizedSource,
            DonorTrueIndex: null,
            TargetTrueIndex: null,
            ActorId: null,
            Hidden: true,
            RowCount: null,
            Evidence: nativeFamilyPresent ? SpecialChestComponentEvidence.NativeObserved : SpecialChestComponentEvidence.Unmapped,
            Note: nativeFamilyPresent
                ? "Resident hidden/linked roles are observable, but their duplicate-safe ownership and row count remain unmapped."
                : "The target-specific hidden companion/controller/reward row set and count remain unmapped."));

        IReadOnlyList<SpecialChestActorPackageDeclaration> packages = family.ActorClosure
            .Select((actorId, index) => new SpecialChestActorPackageDeclaration(
                Id: $"actor-package-{actorId:x4}",
                ActorId: actorId,
                DonorLevelKey: normalizedSource,
                Imported: !nativeFamilyPresent,
                PackageOrSharedRoot: nativeFamilyPresent
                    ? $"resident actor/package root 0x{actorId:X4}"
                    : $"donor actor/package root 0x{actorId:X4}; destination root unresolved",
                Evidence: mappedEvidence,
                Note: index == 0
                    ? "Visible family root package requirement."
                    : "Required spawned, controller, or reward closure package; exact per-target usage must be mapped."))
            .ToArray();

        SpecialChestComponentEvidence destinationEvidence = nativeFamilyPresent
            ? SpecialChestComponentEvidence.NativeObserved
            : SpecialChestComponentEvidence.Unmapped;
        IReadOnlyList<SpecialChestRootRegistrationDeclaration> registrations = family.ActorClosure
            .Select(actorId => new SpecialChestRootRegistrationDeclaration(
                Id: $"root-registration-{actorId:x4}",
                ActorId: actorId,
                RegistrationOrDispatch: nativeFamilyPresent
                    ? $"resident class 0x{actorId:X4} registration"
                    : $"target class 0x{actorId:X4} registration not allocated",
                Evidence: destinationEvidence,
                Note: nativeFamilyPresent
                    ? "Retail registration exists, but safe reuse by a new bundle instance still needs proof."
                    : "Destination package root and dispatch registration remain unmapped."))
            .ToArray();

        SpecialChestDependencyRequirement[] behaviorDependencies = Dependencies[family.Family]
            .Where(dependency => dependency.Kind is SpecialChestDependencyKind.BehaviorHandler or SpecialChestDependencyKind.DamagePredicate)
            .ToArray();
        IReadOnlyList<SpecialChestCodeDeclaration> code = behaviorDependencies
            .Select(dependency => new SpecialChestCodeDeclaration(
                Id: dependency.Id,
                Handler: nativeFamilyPresent ? "resident retail handler" : "destination overlay handler not allocated",
                SharedCalls: [],
                RebaseSites: [],
                Evidence: destinationEvidence,
                Note: nativeFamilyPresent
                    ? $"{dependency.Note} Existing retail code is not proof of a relocatable added-instance path."
                    : $"{dependency.Note} Shared calls and target rebase sites remain unmapped."))
            .ToArray();

        IReadOnlyList<SpecialChestPrivateAllocationDeclaration> allocations =
        [
            new(
                Id: "source-and-hidden-row-allocation",
                Kind: SpecialChestPrivateAllocationKind.SourceRows,
                Region: "destination source table; exact range unresolved",
                SizeBytes: null,
                AlignmentBytes: 4,
                Evidence: SpecialChestComponentEvidence.Unmapped,
                Note: "The visible and hidden rows require one collision-checked atomic range."),
            new(
                Id: "runtime-and-scene-properties-allocation",
                Kind: SpecialChestPrivateAllocationKind.RuntimeProperties,
                Region: "destination scene/runtime properties; exact range unresolved",
                SizeBytes: null,
                AlignmentBytes: 4,
                Evidence: SpecialChestComponentEvidence.Unmapped,
                Note: "Every instance requires private properties and coherent scene pointer fixups."),
            new(
                Id: "handler-and-package-allocation",
                Kind: SpecialChestPrivateAllocationKind.OverlayCode,
                Region: "destination overlay/actor package space; exact ranges unresolved",
                SizeBytes: null,
                AlignmentBytes: 4,
                Evidence: SpecialChestComponentEvidence.Unmapped,
                Note: "Overlay code, actor packages, shared-call rebases, and collision checks are not yet allocated."),
            new(
                Id: "texture-palette-allocation",
                Kind: SpecialChestPrivateAllocationKind.TextureAndPalette,
                Region: "destination texture pages and CLUTs; exact ranges unresolved",
                SizeBytes: null,
                AlignmentBytes: null,
                Evidence: SpecialChestComponentEvidence.Unmapped,
                Note: "Pixels, palettes, descriptors, and any effect frames need private target-safe allocation.")
        ];

        IReadOnlyList<SpecialChestResourceDeclaration> resources =
        [
            new(
                Id: "texture-pixels",
                Kind: SpecialChestResourceKind.TexturePixels,
                DonorLevelKey: normalizedSource,
                SizeBytes: null,
                Evidence: mappedEvidence,
                Note: "Family texture pixels are required; target page placement remains profile-specific."),
            new(
                Id: "palette-cluts",
                Kind: SpecialChestResourceKind.PaletteClut,
                DonorLevelKey: normalizedSource,
                SizeBytes: null,
                Evidence: mappedEvidence,
                Note: "Family palettes/CLUTs are required; target placement remains profile-specific."),
            new(
                Id: "effects-and-reward-models",
                Kind: SpecialChestResourceKind.AudioVisualEffects,
                DonorLevelKey: normalizedSource,
                SizeBytes: null,
                Evidence: mappedEvidence,
                Note: "Animation, sound, particles, fragments, and reward visuals must remain part of the atomic closure.")
        ];

        List<SpecialChestIncompatibilityDeclaration> incompatibilities =
        [
            new(
                Id: "retail-disc-fingerprint",
                Condition: "source disc fingerprint differs from the checked USA retail image",
                Note: "Reject stale or unsupported disc layouts instead of reinterpreting offsets.")
        ];
        if (disallowOtherObjectEdits)
        {
            incompatibilities.Add(new(
                Id: "other-object-edits",
                Condition: "the same target level contains unrelated object edits",
                Note: "The current allocation has not been collision-tested with other object edits."));
        }
        if (disallowOtherStructuralRelocations)
        {
            incompatibilities.Add(new(
                Id: "other-structural-relocations",
                Condition: "another edit relocates the WAD, executable, overlay, source table, or scene",
                Note: "All structural growth must be composed through one checked collision planner."));
        }
        if (maxInstances is int limit)
        {
            incompatibilities.Add(new(
                Id: "bundle-instance-capacity",
                Condition: $"more than {limit} bundle instance(s) are requested",
                Note: "Additional copies remain disabled until a multi-copy runtime test proves capacity."));
        }

        return new SpecialChestBundleDeclaration(
            Rows: rows,
            ActorPackages: packages,
            RootRegistrations: registrations,
            Code: code,
            PrivateAllocations: allocations,
            Resources: resources,
            Reward: new(
                Id: "family-reward-lifecycle",
                TreasureDelta: treasureDelta,
                LifeDelta: lifeDelta,
                OneShotRequired: true,
                Evidence: mappedEvidence,
                Note: treasureDelta is int
                    ? $"The donor behavior awards +{treasureDelta} treasure; target total, one-shot cleanup, and persistence still require proof."
                    : lifeDelta is int
                        ? $"The family contract awards +{lifeDelta} life; target child effects, cleanup, and persistence still require proof."
                        : "The exact target reward delta, one-shot cleanup, and persistence remain profile-specific."),
            Incompatibilities: incompatibilities);
    }

    private static SpecialChestBundleDeclaration BuildArtisansLockedChestDeclaration()
    {
        List<SpecialChestRowDeclaration> rows =
        [
            new(
                Id: "visible-key-t174",
                Role: "visible key root",
                DonorLevelKey: "peacekeepers",
                DonorTrueIndex: 78,
                TargetTrueIndex: ArtisansNativeLockedChestRuntimeBundleComposer.KeyOutputTrueIndex,
                ActorId: 0x00AD,
                Hidden: false,
                RowCount: 1,
                Evidence: SpecialChestComponentEvidence.RuntimeProven,
                Note: "Saved editor Key position/yaw composes into the guarded T174 native-key proxy row."),
            new(
                Id: "visible-locked-chest-t175",
                Role: "visible locked-chest root",
                DonorLevelKey: "peacekeepers",
                DonorTrueIndex: 79,
                TargetTrueIndex: ArtisansNativeLockedChestRuntimeBundleComposer.LockedChestOutputTrueIndex,
                ActorId: 0x00AE,
                Hidden: false,
                RowCount: 1,
                Evidence: SpecialChestComponentEvidence.RuntimeProven,
                Note: "Saved editor Locked Chest position/yaw composes into the guarded T175 row.")
        ];
        foreach (int targetTrueIndex in Enumerable.Range(176, 5))
        {
            rows.Add(new(
                Id: $"hidden-reward-marker-t{targetTrueIndex}",
                Role: "hidden translated native reward marker",
                DonorLevelKey: "peacekeepers",
                DonorTrueIndex: null,
                TargetTrueIndex: targetTrueIndex,
                ActorId: 0x000D,
                Hidden: true,
                RowCount: 1,
                Evidence: SpecialChestComponentEvidence.RuntimeProven,
                Note: "The installed V2 preimage and its matching scene-properties row are hash-guarded and translated with the visible chest."));
        }

        IReadOnlyList<SpecialChestActorPackageDeclaration> packages =
        [
            new("shared-key-model", 0x00AD, "peacekeepers", false, "shared PETE.WAD key model plus executable key proxy", SpecialChestComponentEvidence.RuntimeProven, "The gold key model/texture and native pickup path passed DuckStation."),
            new("locked-chest-package", 0x00AE, "peacekeepers", true, "private expanded Artisans actor-package root", SpecialChestComponentEvidence.RuntimeProven, "The exact imported package hash and runtime model are guarded."),
            new("locked-debris-0135", 0x0135, "peacekeepers", true, "contiguous imported debris package root", SpecialChestComponentEvidence.RuntimeProven, "The checked package bundle retains the native debris asset closure."),
            new("locked-debris-0136", 0x0136, "peacekeepers", true, "contiguous imported debris package root", SpecialChestComponentEvidence.RuntimeProven, "The checked package bundle retains the native debris asset closure."),
            new("locked-debris-0137", 0x0137, "peacekeepers", true, "contiguous imported debris package root", SpecialChestComponentEvidence.RuntimeProven, "The checked package bundle retains the native debris asset closure."),
            new("reward-marker-class", 0x000D, "peacekeepers", false, "relocated overlay handler using resident reward visuals", SpecialChestComponentEvidence.RuntimeProven, "The five hidden class-0x000D reward markers produce the one-shot six-gem reward.")
        ];

        IReadOnlyList<SpecialChestRootRegistrationDeclaration> registrations =
        [
            new("class-000d-dispatch", 0x000D, "Artisans class-0x000D overlay dispatch -> private reward-marker handler", SpecialChestComponentEvidence.RuntimeProven, "Dispatch and relocated payload hashes are guarded."),
            new("class-00ad-key-proxy", 0x00AD, "SCUS key-proxy hook -> native key handler", SpecialChestComponentEvidence.RuntimeProven, "The safe executable cave avoids the rejected pre-logo range and passed runtime pickup."),
            new("class-00ae-dispatch", 0x00AE, "Artisans class-0x00AE overlay dispatch -> private locked-chest handler", SpecialChestComponentEvidence.RuntimeProven, "Unlock, animation, sound, death, and retirement passed runtime proof."),
            new("class-0135-root", 0x0135, "expanded actor-subfile root", SpecialChestComponentEvidence.RuntimeProven, "Contiguous actor-package root is included in the guarded bundle."),
            new("class-0136-root", 0x0136, "expanded actor-subfile root", SpecialChestComponentEvidence.RuntimeProven, "Contiguous actor-package root is included in the guarded bundle."),
            new("class-0137-root", 0x0137, "expanded actor-subfile root", SpecialChestComponentEvidence.RuntimeProven, "Contiguous actor-package root is included in the guarded bundle.")
        ];

        IReadOnlyList<SpecialChestCodeDeclaration> code =
        [
            new(
                Id: "key-proxy-handler",
                Handler: "SCUS key-proxy executable cave",
                SharedCalls: ["native key pickup handler", "Artisans loop exit"],
                RebaseSites: ["green-gem handler hook", "relocated executable copy-buffer words"],
                Evidence: SpecialChestComponentEvidence.RuntimeProven,
                Note: "Gold key pickup, inventory state, and later chest consumption passed runtime proof."),
            new(
                Id: "locked-chest-handler",
                Handler: "private Artisans overlay locked-chest setup/state payload",
                SharedCalls: ["native SpawnMoby/reward services", "resident Artisans fragment services"],
                RebaseSites: ["class-0x00AE dispatch", "private overlay branches and calls"],
                Evidence: SpecialChestComponentEvidence.RuntimeProven,
                Note: "The key gate, unlock animation/sound, reward, and one-shot cleanup passed DuckStation."),
            new(
                Id: "reward-marker-handler",
                Handler: "private Artisans overlay class-0x000D reward-marker payload",
                SharedCalls: ["native gem spawn/list services"],
                RebaseSites: ["class-0x000D dispatch", "private overlay branches and calls"],
                Evidence: SpecialChestComponentEvidence.RuntimeProven,
                Note: "Five translated markers produce six gems worth +10 once and retire correctly.")
        ];

        IReadOnlyList<SpecialChestPrivateAllocationDeclaration> allocations =
        [
            new("source-rows-t174-t180", SpecialChestPrivateAllocationKind.SourceRows, "Artisans T174-T180", 7 * 0x58, 4, SpecialChestComponentEvidence.RuntimeProven, "The exact seven-row allocation is guarded against stale or colliding edits."),
            new("scene-properties-and-fixups", SpecialChestPrivateAllocationKind.SceneProperties, "expanded Artisans scene properties for T174-T180", null, 4, SpecialChestComponentEvidence.RuntimeProven, "All five hidden props copies and final pointer-fixup count are verified."),
            new("private-overlay-payload", SpecialChestPrivateAllocationKind.OverlayCode, "expanded Artisans overlay private tail", 0xBA0, 4, SpecialChestComponentEvidence.RuntimeProven, "Locked-chest and reward-marker payloads, dispatches, and rebases are hash-guarded."),
            new("actor-package-growth", SpecialChestPrivateAllocationKind.ActorPackages, "expanded Artisans actor subfile", 0x1800, 4, SpecialChestComponentEvidence.RuntimeProven, "The private actor-package bundle occupies the checked WAD-growth allocation."),
            new("key-proxy-executable-cave", SpecialChestPrivateAllocationKind.ExecutableCode, "safe SCUS key-proxy cave", 0x400, 4, SpecialChestComponentEvidence.RuntimeProven, "The executable relocation and safe code region are guarded."),
            new("locked-texture-and-clut", SpecialChestPrivateAllocationKind.TextureAndPalette, "four private 32x32 4-bpp tiles and four private CLUTs", 2176, null, SpecialChestComponentEvidence.RuntimeProven, "The final texture page and package hashes are verified.")
        ];

        IReadOnlyList<SpecialChestResourceDeclaration> resources =
        [
            new("gold-key-texture", SpecialChestResourceKind.TexturePixels, "peacekeepers", null, SpecialChestComponentEvidence.RuntimeProven, "The key renders gold instead of the rejected green-gem proxy."),
            new("locked-chest-textures", SpecialChestResourceKind.TexturePixels, "peacekeepers", 2048, SpecialChestComponentEvidence.RuntimeProven, "Four private 32x32 4-bpp tiles reproduce the proven chest surface."),
            new("locked-chest-cluts", SpecialChestResourceKind.PaletteClut, "peacekeepers", 128, SpecialChestComponentEvidence.RuntimeProven, "Four private CLUTs preserve the proven colors without target aliasing."),
            new("unlock-and-reward-effects", SpecialChestResourceKind.AudioVisualEffects, "peacekeepers", null, SpecialChestComponentEvidence.RuntimeProven, "Unlock sound/animation, fragments, gem effects, and cleanup passed runtime proof."),
            new("six-gem-reward-models", SpecialChestResourceKind.RewardModel, "peacekeepers", null, SpecialChestComponentEvidence.RuntimeProven, "The six visible gems are collectible and total exactly +10 once.")
        ];

        return new SpecialChestBundleDeclaration(
            Rows: rows,
            ActorPackages: packages,
            RootRegistrations: registrations,
            Code: code,
            PrivateAllocations: allocations,
            Resources: resources,
            Reward: new(
                Id: "six-gem-plus-ten",
                TreasureDelta: 10,
                LifeDelta: 0,
                OneShotRequired: true,
                Evidence: SpecialChestComponentEvidence.RuntimeProven,
                Note: "The fixed Artisans treasure target changes 100->110; six gems award exactly +10 and persist retired."),
            Incompatibilities:
            [
                new("one-pair-capacity", "more than one Key + Locked Chest pair is requested", "The T174-T180 allocation is proven for exactly one pair."),
                new("other-object-edits", "the same Artisans build contains any other object edit", "The promoted composer currently reserves the entire exact two-edit manifest."),
                new("other-structural-relocations", "another edit relocates the WAD, executable, overlay, source table, or scene", "Structural changes must be composed before this profile can share a build."),
                new("retail-disc-fingerprint", "source disc fingerprint or guarded V2 preimage differs", "Reject stale layouts rather than applying fixed addresses to unknown bytes.")
            ]);
    }

    private static IReadOnlyList<string> RequiredWork(SpecialChestFamilyDefinition family, string levelName) =>
        family.Family switch
        {
            SpecialChestFamily.LifeChest =>
            [
                $"Map {levelName}'s 0x01A5 shell plus 0x01A6 and 0x000E spawned-child closure with independent runtime storage.",
                "Pass +1 life, visible child/reward, one-shot cleanup, reload, re-entry, and multi-instance DuckStation tests."
            ],
            SpecialChestFamily.SpringChest =>
            [
                $"Transplant and rebase the real spring handler, fragment actors, SpawnMoby initialization, reward lifecycle, and textures into {levelName}.",
                "Replace the synthetic motion experiment with native interaction/animation and pass flame, charge, reward, Sparx, cleanup, and reload tests."
            ],
            SpecialChestFamily.FireworkChest =>
            [
                $"Map the fuse, rocket/explosion children, collision, reward, cleanup, texture, and handler closure for {levelName}.",
                "Pass timing, nearby-target, Firework-to-Armored, reward-once, reload, and re-entry DuckStation tests."
            ],
            SpecialChestFamily.MultiGemChest =>
            [
                $"Map the 0x0186 shell and linked state/controller group into {levelName} without exceeding source/runtime limits.",
                "Pass exactly-three-hit, per-stage visual, one reward, one cleanup, reload, and nearby-regression DuckStation tests."
            ],
            SpecialChestFamily.ArmoredChest =>
            [
                $"Map the 0x0191 model/handler, linked roles, damage gate, effects, reward, and target-safe textures for {levelName}.",
                "Prove normal flame/charge rejection, supercharge/superflame acceptance, native firework/cannon interactions, reward-once, and persistence."
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(family))
        };

    private static string FamilyKey(SpecialChestFamily family) => family switch
    {
        SpecialChestFamily.LockedChest => "locked",
        SpecialChestFamily.LifeChest => "life",
        SpecialChestFamily.SpringChest => "spring",
        SpecialChestFamily.FireworkChest => "firework",
        SpecialChestFamily.MultiGemChest => "multi-hit",
        SpecialChestFamily.ArmoredChest => "armored",
        _ => throw new ArgumentOutOfRangeException(nameof(family))
    };

    private static SpecialChestDependencyRequirement Required(
        string id,
        SpecialChestDependencyKind kind,
        string note) =>
        new(id, kind, Required: true, note);

    private static HashSet<string> Set(params string[] keys) =>
        keys.Select(LevelCatalog.NormalizeKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
}
