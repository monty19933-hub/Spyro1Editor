using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Analysis;

/// <summary>
/// Catalog for the runtime-proven Green Wizard dependency closure.  A profile
/// is level-scoped; GreenWizardInstancePlan remains object-scoped so the bundle
/// is installed no more than once per target level.
/// </summary>
public static class GreenWizardRuntimeBundleCatalog
{
    public const string BundleId = "spyro1.green-wizard.runtime-bundle.v11";
    public const string ManifestFingerprint = "37a223c61d1b6c10904de0d9175266c878d3092b8c5fee62296a8b791bf61703";
    public const string WizardPeakProfileFingerprint = "31f535e3e95b52bea36f45852c3736f164ccc0b6cdc75085eb94a46f3523f3b9";
    public const string BlowhardProfileFingerprint = "82dcc68f3f76af7e6217e50c7d486a2cb6be685f2e1ada5e8fd45dc090f323a7";
    public const string MagicCraftersProfileFingerprint = "f47031c456277cf057df654e16cda4ab32c757c36583705503f8157ea97e27e6";
    public const string ToastyProfileFingerprint = "ce4fae6cdcd430c4cc81745fa646602cd547037af9d4b2481c59a522412bc7f2";
    public const string ToastyProofInstanceFingerprint = "6cc3368a63e2d82b6bb40244a280732467644cb080f8bf48b3dfd1fae88a6d0e";

    public static GreenWizardRuntimeBundleManifest Manifest { get; } = BuildManifest();

    private static readonly IReadOnlyList<LevelRuntimeBundleProfile> Profiles =
    [
        BuildWizardPeakProfile(),
        BuildBlowhardProfile(),
        BuildMagicCraftersProfile(),
        BuildToastyProfile()
    ];

    private static readonly IReadOnlyList<GreenWizardInstancePlan> ProvenInstances =
    [
        BuildToastyProofInstance()
    ];

    static GreenWizardRuntimeBundleCatalog()
    {
        Manifest.Validate();
        RequireFingerprint("manifest", Manifest.Fingerprint, ManifestFingerprint);
        if (Profiles.Select(profile => LevelCatalog.NormalizeKey(profile.LevelKey)).Distinct(StringComparer.OrdinalIgnoreCase).Count() != Profiles.Count)
            throw new InvalidDataException("Green Wizard runtime-bundle profile level keys must be unique.");

        foreach (LevelRuntimeBundleProfile profile in Profiles)
        {
            profile.Validate(Manifest);
            string expectedFingerprint = LevelCatalog.NormalizeKey(profile.LevelKey) switch
            {
                "wizardpeak" => WizardPeakProfileFingerprint,
                "blowhard" => BlowhardProfileFingerprint,
                "magiccrafters" => MagicCraftersProfileFingerprint,
                "toasty" => ToastyProfileFingerprint,
                _ => throw new InvalidDataException($"Green Wizard profile '{profile.LevelKey}' has no frozen fingerprint.")
            };
            RequireFingerprint($"{profile.LevelKey} profile", profile.Fingerprint, expectedFingerprint);
        }
        foreach (GreenWizardInstancePlan instance in ProvenInstances)
        {
            LevelRuntimeBundleProfile profile = FindProfile(instance.TargetLevelKey) ??
                throw new InvalidDataException($"Green Wizard proof instance '{instance.Id}' has no level profile.");
            instance.Validate(Manifest, profile);
            RequireFingerprint($"{instance.Id} proof instance", instance.Fingerprint, ToastyProofInstanceFingerprint);
        }
    }

    public static IReadOnlyList<LevelRuntimeBundleProfile> AllProfiles => Profiles;

    public static IReadOnlyList<GreenWizardInstancePlan> AllProvenInstances => ProvenInstances;

    public static LevelRuntimeBundleProfile? FindProfile(string targetLevelKey)
    {
        string target = LevelCatalog.NormalizeKey(targetLevelKey);
        return Profiles.FirstOrDefault(profile =>
            string.Equals(LevelCatalog.NormalizeKey(profile.LevelKey), target, StringComparison.OrdinalIgnoreCase));
    }

    public static GreenWizardInstancePlan? FindProvenInstance(string targetLevelKey, int targetTrueIndex)
    {
        string target = LevelCatalog.NormalizeKey(targetLevelKey);
        return ProvenInstances.FirstOrDefault(instance =>
            instance.TargetTrueIndex == targetTrueIndex &&
            string.Equals(LevelCatalog.NormalizeKey(instance.TargetLevelKey), target, StringComparison.OrdinalIgnoreCase));
    }

    public static RuntimeBundleCompatibilityResult Evaluate(string targetLevelKey)
    {
        string target = LevelCatalog.NormalizeKey(targetLevelKey);
        LevelRuntimeBundleProfile? profile = FindProfile(target);
        if (profile == null)
        {
            return new RuntimeBundleCompatibilityResult(
                BundleId: Manifest.BundleId,
                BundleFingerprint: Manifest.Fingerprint,
                TargetLevelKey: target,
                Status: RuntimeBundleCompatibilityStatus.NeedsTargetProfile,
                Deployment: null,
                CanStageInstance: false,
                NormalCreateBinReady: false,
                NeedsLevelBundleInstall: true,
                RequiresRuntimeSmoke: true,
                ProfileFingerprint: "",
                Findings:
                [
                    "The complete donor-side Green Wizard behavior, lightning, particle, texture, and instance schema is known.",
                    "This destination does not yet have checked overlay, actor-root, RAM-capacity, and VRAM allocations."
                ],
                RequiredWork:
                [
                    "Build and validate one LevelRuntimeBundleProfile for this destination.",
                    "Create a guarded candidate BIN and complete the focused DuckStation behavior smoke before promotion."
                ]);
        }

        profile.Validate(Manifest);
        if (profile.Deployment == RuntimeBundleDeploymentKind.Native)
        {
            bool nativeRuntimeProven = profile.Evidence == RuntimeBundleEvidenceKind.RuntimeProven;
            return new RuntimeBundleCompatibilityResult(
                Manifest.BundleId,
                Manifest.Fingerprint,
                profile.LevelKey,
                RuntimeBundleCompatibilityStatus.Ready,
                profile.Deployment,
                CanStageInstance: true,
                NormalCreateBinReady: nativeRuntimeProven,
                NeedsLevelBundleInstall: false,
                RequiresRuntimeSmoke: !nativeRuntimeProven,
                profile.Fingerprint,
                Findings:
                [
                    "Wizard Peak is the native donor and already owns actor 0x011B plus its lightning closure.",
                    "Only per-instance source-row, reward, yaw, culling-sector, properties, and route work is required.",
                    nativeRuntimeProven
                        ? "The exact native T6-to-T24 in-place recipe was accepted in DuckStation with visible lightning, the translated route, and the expected gem; live RAM separately confirmed actor/model initialization, attack state, movement, death, and retirement."
                        : "The exact destination-slot recipe still needs focused DuckStation proof before normal Create BIN."
                ],
                RequiredWork: nativeRuntimeProven
                    ? []
                    : ["Create a guarded candidate BIN and complete the focused DuckStation behavior smoke."]);
        }

        if (profile.Deployment == RuntimeBundleDeploymentKind.ResidentActor)
        {
            bool residentRuntimeProven = profile.Evidence == RuntimeBundleEvidenceKind.RuntimeProven;
            RuntimeBundleResidentPropertiesLayout? residentProperties = profile.ResidentDetection?.PropertiesLayout;
            bool donorInstanceLayoutMatches = residentProperties != null &&
                residentProperties.PropertiesBytes == Manifest.InstanceSchema.PropertiesBytes &&
                residentProperties.RoutePointCount == Manifest.InstanceSchema.RoutePointCount;
            List<string> findings =
            [
                $"{FormatLevelName(profile.LevelKey)} already contains checked roots and overlay dispatch handlers for actors 0x011B and 0x0026.",
                "No cross-level behavior-overlay transplant is required for this resident-class recipe."
            ];
            List<string> requiredWork = [];
            if (residentRuntimeProven)
            {
                findings.Add($"{FormatLevelName(profile.LevelKey)}'s resident recipe passed live DuckStation proof: repeated real lightning hits, one terminating death animation/sound, one gem, persistent retirement, and normal nearby behavior.");
                findings.Add(LevelCatalog.NormalizeKey(profile.LevelKey) == "magiccrafters"
                    ? "Normal Create BIN may compose the checked in-place private properties extent, target pod preservation, translated native route, and stale-fixup removal for the exact T107-to-T27 replacement."
                    : "Normal Create BIN may compose the checked properties-component growth, target pod preservation, translated native route, and moved pointer-fixup list for one existing-slot replacement.");
            }
            else
            {
                requiredWork.Add(
                    $"Stage the guarded existing-slot candidate with {FormatLevelName(profile.InstanceLayout.PropertiesDonorLevelKey)} " +
                    $"T{profile.InstanceLayout.PropertiesDonorTrueIndex}'s checked native Wizard properties and route convention.");
                requiredWork.Add("Run casting, hit, death, gem, route, and nearby-regression checks in DuckStation before normal-build promotion.");
            }
            if (!donorInstanceLayoutMatches && residentProperties != null)
            {
                findings.Add(
                    $"The resident properties layout is 0x{residentProperties.PropertiesBytes:X} bytes with {residentProperties.RoutePointCount} route point, " +
                    $"while the Wizard Peak donor template is 0x{Manifest.InstanceSchema.PropertiesBytes:X} bytes with {Manifest.InstanceSchema.RoutePointCount} points.");
                if (!residentRuntimeProven)
                {
                    requiredWork.Insert(
                        0,
                        $"Use {FormatLevelName(profile.InstanceLayout.PropertiesDonorLevelKey)} T{profile.InstanceLayout.PropertiesDonorTrueIndex}'s " +
                        "resident properties layout for the candidate; do not blindly install the Wizard Peak T6 block.");
                }
            }

            return new RuntimeBundleCompatibilityResult(
                Manifest.BundleId,
                Manifest.Fingerprint,
                profile.LevelKey,
                residentRuntimeProven ? RuntimeBundleCompatibilityStatus.Ready : RuntimeBundleCompatibilityStatus.Candidate,
                profile.Deployment,
                CanStageInstance: true,
                NormalCreateBinReady: residentRuntimeProven,
                NeedsLevelBundleInstall: false,
                RequiresRuntimeSmoke: !residentRuntimeProven,
                profile.Fingerprint,
                findings,
                requiredWork);
        }

        bool runtimeProven = profile.Evidence == RuntimeBundleEvidenceKind.RuntimeProven;
        return new RuntimeBundleCompatibilityResult(
            Manifest.BundleId,
            Manifest.Fingerprint,
            profile.LevelKey,
            runtimeProven ? RuntimeBundleCompatibilityStatus.Ready : RuntimeBundleCompatibilityStatus.Candidate,
            profile.Deployment,
            CanStageInstance: true,
            NormalCreateBinReady: runtimeProven,
            NeedsLevelBundleInstall: true,
            RequiresRuntimeSmoke: !runtimeProven,
            profile.Fingerprint,
            Findings: runtimeProven
                ?
                [
                    "The complete bundle and proof instance passed live DuckStation casting, lightning-hit, death, gem, route, animation, and texture checks.",
                    "Additional Wizards in this level reuse the installed level bundle; only their instance records, properties, and routes are new."
                ]
                :
                [
                    "The destination has a complete static target profile, but its bundle has not yet passed live runtime proof."
                ],
            RequiredWork: runtimeProven
                ? []
                : ["Create a guarded candidate BIN and complete the focused DuckStation behavior smoke."]);
    }

    public static RuntimeBundleCompatibilityResult Evaluate(string targetLevelKey, GreenWizardInstancePlan instance)
    {
        RuntimeBundleCompatibilityResult compatibility = Evaluate(targetLevelKey);
        LevelRuntimeBundleProfile? profile = FindProfile(targetLevelKey);
        if (profile == null)
            return compatibility;

        instance.Validate(Manifest, profile);
        return compatibility;
    }

    private static void RequireFingerprint(string label, string actual, string expected)
    {
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"The frozen Green Wizard {label} fingerprint changed: expected {expected}, found {actual}. " +
                "Bump the bundle/profile version only after regenerating its static and runtime evidence.");
        }
    }

    private static string FormatLevelName(string levelKey) =>
        LevelCatalog.NormalizeKey(levelKey) switch
        {
            "wizardpeak" => "Wizard Peak",
            "blowhard" => "Blowhard",
            "magiccrafters" => "Magic Crafters",
            "toasty" => "Toasty",
            _ => levelKey
        };

    private static GreenWizardRuntimeBundleManifest BuildManifest() => new(
        SchemaVersion: 1,
        BundleId: BundleId,
        DisplayName: "Green Wizard runtime bundle",
        DonorLevelKey: "wizardpeak",
        DonorOverlayEntry: 39,
        DonorDataEntry: 40,
        OverlayLoadAddress: 0x8007AA38,
        GreenWizardActorId: 0x011B,
        LightningActorId: 0x0026,
        ParticleTypes: [0x07, 0x41],
        Components:
        [
            new(
                "green-wizard-actor-package",
                RuntimeBundleStorageKind.LevelData,
                0x19B518,
                0x35AC,
                "24df9ae279f32ba10a8e3bd25880d6989991b9225acf45755e6aaf985d66f529",
                RuntimeBundleRelocationKind.CopyActorPackage,
                "Actor 0x011B model and animation package; target root registration remains level-specific."),
            new(
                "compact-overlay-prefix",
                RuntimeBundleStorageKind.Overlay,
                0,
                0xD108,
                "6cc3fc694ebefc487e0609c7ef86155ad880910b2e56c6873707c6c44f4d7a25",
                RuntimeBundleRelocationKind.RelocateOverlayCode,
                "Relocatable Wizard Peak overlay prefix containing the Wizard, lightning, spawn, animation, death, and shared control paths."),
            new(
                "lightning-actor-package",
                RuntimeBundleStorageKind.LevelData,
                0x1CAC88,
                0x564,
                "9ea5c72ba675e9c6a81433f7408695399e19bc188b4b2a923069d71de601789a",
                RuntimeBundleRelocationKind.CopyActorPackage,
                "Actor 0x0026 projectile model package."),
            new(
                "particle-handler-bundle",
                RuntimeBundleStorageKind.Overlay,
                0xD938,
                0x380,
                "eaab7adf4f1d5657c9d1ed66a5e944b007a560b5cebb3516705ee6cda62b38fc",
                RuntimeBundleRelocationKind.RelocateOverlayCode,
                "Checked assembly of the non-contiguous type-0x07 and type-0x41 spawn/update fragments after target adapters."),
            new(
                "wizard-properties-template",
                RuntimeBundleStorageKind.Scene,
                0xC350,
                0x50,
                "5b3d762f6117ea30e959a21bf2778f3442b03eba93125c9a0e0a642cc58d345b",
                RuntimeBundleRelocationKind.CloneAndRebaseInstanceProperties,
                "Native Wizard Peak T6 properties template; each destination instance receives its own route and internal-pointer rebase."),
            new(
                "texture-dependency-aggregate",
                RuntimeBundleStorageKind.TextureData,
                0,
                0x8A8,
                "9448477af6f60a253f43c49e0642f8ce406027ff811dd37b41a4b1d1f4efbfc7",
                RuntimeBundleRelocationKind.AllocateTargetTextureStorage,
                "2,056 pixel bytes plus 160 CLUT bytes for the Wizard, primary lightning, and trail particle."),
            new(
                "lightning-trail-descriptor-template",
                RuntimeBundleStorageKind.LevelData,
                0x173518,
                0x0C,
                "4baed93eb98ac71d27f59e3ddf8b5382dda7e74319de188749dce3831397f7b3",
                RuntimeBundleRelocationKind.RebuildTargetDescriptor,
                "Type-0x07 particle descriptor; UV, CLUT, and texture-page fields are rebuilt for each target allocation.")
        ],
        PatchContract: new GreenWizardRuntimePatchContract(
            DispatchShimBytes: 0xA0,
            ParticleDescriptorBytes: 0x0C,
            InstalledParticleDescriptorSha256: "82f2f94882c9e383d7fdbf278cd615c1a78a015751a4d617a108d399081c38f5",
            OriginalLightningDamageFlags: 0x00020020,
            PatchedLightningDamageFlags: 1,
            PatchedSpyroHurtState: 0x0E,
            PatchedSpyroAnimation: 0x0F,
            SuppressedImpactChildCount: 4,
            RelocatedJumpCount: 357,
            PreservedBranchCount: 1029,
            RelocatedPointerCount: 675,
            ExcludedTailPointerCount: 178,
            RelocatedHiLoPairCount: 9),
        InstanceSchema: new GreenWizardInstanceSchema(
            SourceRecordBytes: 0x58,
            PropertiesBytes: 0x50,
            ActorIdLowOffset: 0x36,
            ActorIdHighOffset: 0x37,
            PodOrGroupOffset: 0x43,
            PodOrGroupValue: 0xFF,
            YawOffset: 0x46,
            CullingSectorOffset: 0x4A,
            RendererDistanceOffset: 0x4B,
            RendererDistanceValue: 0x09,
            RewardOffset: 0x53,
            InternalRoutePointerOffset: 0,
            InternalRouteAnchorOffset: 0x28,
            FirstRoutePointOffset: 0x30,
            RoutePointStride: 0x10,
            RoutePointCount: 2,
            PreserveTargetYaw: true,
            RecomputeTargetCullingSector: true,
            PreserveTargetReward: true),
        ProvenRecipeId: "toasty.wizardpeak.greenWizard.compactDependencyBundle.propsFixup.impactBurstQuarantine.genericHurtDamage.textureDependencies.lightningTrailParticleTexture.v11",
        ProvenOutputSha256: "e2ec0b112c9f7782ec85e66118cf157550938e9c49146339df6d1000ffbff5e7");

    private static LevelRuntimeBundleProfile BuildWizardPeakProfile() => new(
        BundleId,
        LevelKey: "wizardpeak",
        Deployment: RuntimeBundleDeploymentKind.Native,
        Evidence: RuntimeBundleEvidenceKind.RuntimeProven,
        ResidentActorIds: [0x011B, 0x0026],
        InstanceLayout: new RuntimeBundleInstanceLayout(
            PropertiesDonorLevelKey: "wizardpeak",
            PropertiesDonorTrueIndex: 6,
            PropertiesBytes: 0x50,
            InternalRouteAnchorOffset: 0x28,
            FirstRoutePointOffset: 0x30,
            RoutePointStride: 0x10,
            RoutePointCount: 2,
            AddPointerFixupDelta: 1,
            SwapPointerFixupDelta: 0,
            PreserveTargetPodOrGroup: false,
            TranslateDonorRouteFromSpawn: true,
            Note: "Wizard Peak T6 is the proven 0x50-byte/two-point donor. The exact runtime-proven replacement uses pod-matched Elder Wizard T24, preserves its placement/yaw/culling/reward, installs T6's detached pod/group 0xFF and translated route shape, and replaces T24's stale internal fixup without changing the native fixup count."),
        ResidentDetection: null,
        Overlay: null,
        Data: null,
        Hooks: null,
        Textures: [],
        Guards: [],
        EvidenceNote: "Wizard Peak is the native donor for actor 0x011B, actor 0x0026, the behavior overlay, properties, particles, and texture dependencies. The exact T6-to-T24 v3 recipe was accepted in DuckStation with visible lightning, its translated route, and one gem; live RAM separately confirmed actor/model initialization, attack state, movement, death, and retirement. T10 v1/v2 remain retired because they never attacked.",
        RuntimeProofRecipeId: WizardPeakGreenWizardNativeSwapExporter.RecipeId,
        RuntimeProofOutputSha256: "918b2a21d5bee8f653ce52678c3be490321981a8721135aa30c0aa9923714f26");

    private static LevelRuntimeBundleProfile BuildBlowhardProfile() => new(
        BundleId,
        LevelKey: "blowhard",
        Deployment: RuntimeBundleDeploymentKind.ResidentActor,
        Evidence: RuntimeBundleEvidenceKind.RuntimeProven,
        ResidentActorIds: [0x011B, 0x0026],
        InstanceLayout: new RuntimeBundleInstanceLayout(
            PropertiesDonorLevelKey: "blowhard",
            PropertiesDonorTrueIndex: 0,
            PropertiesBytes: 0x40,
            InternalRouteAnchorOffset: 0x28,
            FirstRoutePointOffset: 0x30,
            RoutePointStride: 0x10,
            RoutePointCount: 1,
            AddPointerFixupDelta: 1,
            SwapPointerFixupDelta: 1,
            PreserveTargetPodOrGroup: true,
            TranslateDonorRouteFromSpawn: true,
            Note: "Blowhard T0 supplies the native 0x40-byte/one-point properties layout. The destination slot keeps its pod/group, the T0 route delta is translated from the destination spawn, and the real properties component grows by 0x40 before the following scene components are shifted intact. The new internal pointer adds one fixup (0x65 to 0x66)."),
        ResidentDetection: new RuntimeBundleResidentDetection(
            OverlayEntry: 41,
            DataEntry: 42,
            SceneBaseOffset: 0x16B000,
            SceneBytes: 0x27800,
            ActorRoots:
            [
                new(0x011B, 0x5C, 0x133AAC),
                new(0x0026, 0xA4, 0x1658B4)
            ],
            Dispatches:
            [
                new(0x011B, 0x8007B190),
                new(0x0026, 0x8007B074)
            ],
            PropertiesLayout: new RuntimeBundleResidentPropertiesLayout(
                ExampleTrueIndex: 0,
                PropertiesSceneOffset: 0x243A8,
                PropertiesBytes: 0x40,
                InternalRoutePointer: 0x243D0,
                RoutePointCount: 1,
                PropertiesComponentHeaderSceneOffset: 0x243A4,
                PropertiesComponentBytes: 0x0AC4,
                PointerFixupListSceneOffset: 0x26E94,
                PointerFixupCount: 0x65,
                TrailingScenePaddingBytes: 0x7D4),
            EvidenceNote: "Blowhard data entry 42 and overlay 41 contain checked roots and dispatch comparisons for both the Wizard and lightning actor families. The original scene ends with 0x7D4 bytes of sector padding after the pointer-fixup component; the zero bytes immediately before that component belong to the live collision chain and are never allocation space."),
        Overlay: null,
        Data: null,
        Hooks: null,
        Textures: [],
        Guards: [],
        EvidenceNote: "Blowhard contains seven native actor-0x011B Lightning Wizard source rows plus checked actor-0x011B/0x0026 roots and dispatch comparisons. Structural v2 passed DuckStation play at T7: two separate real lightning hits, one terminating death animation/sound, one gem, persistent retirement, and normal nearby behavior/collision.",
        RuntimeProofRecipeId: BlowhardGreenWizardResidentSwapExporter.RecipeId,
        RuntimeProofOutputSha256: "89defda4af21c1e623f62b40537178e12daa0ecfd84cf4ef0ee00041c91bc008");

    private static LevelRuntimeBundleProfile BuildMagicCraftersProfile() => new(
        BundleId,
        LevelKey: "magiccrafters",
        Deployment: RuntimeBundleDeploymentKind.ResidentActor,
        Evidence: RuntimeBundleEvidenceKind.RuntimeProven,
        ResidentActorIds: [0x011B, 0x0026],
        InstanceLayout: new RuntimeBundleInstanceLayout(
            PropertiesDonorLevelKey: "magiccrafters",
            PropertiesDonorTrueIndex: 107,
            PropertiesBytes: 0x50,
            InternalRouteAnchorOffset: 0x28,
            FirstRoutePointOffset: 0x30,
            RoutePointStride: 0x10,
            RoutePointCount: 2,
            AddPointerFixupDelta: 1,
            SwapPointerFixupDelta: -1,
            PreserveTargetPodOrGroup: true,
            TranslateDonorRouteFromSpawn: true,
            Note: "Magic Crafters T107 supplies the checked native 0x50-byte/two-point properties layout. The focused T27 replacement reuses T27's unique 0x74-byte properties extent at 0xE9BC, preserves placement, yaw, culling, pod/group, reward, and the unused trailing 0x24 bytes, translates both T107 route points, and removes stale target fixup 0xE9F8 (0xBE to 0xBD). No scene component is resized or shifted."),
        ResidentDetection: new RuntimeBundleResidentDetection(
            OverlayEntry: 33,
            DataEntry: 34,
            SceneBaseOffset: 0x1CC800,
            SceneBytes: 0x12000,
            ActorRoots:
            [
                new(0x011B, 0x98, 0x1BAFDC),
                new(0x0026, 0xBC, 0x1C8A54)
            ],
            Dispatches:
            [
                new(0x011B, 0x800883A0),
                new(0x0026, 0x800818FC)
            ],
            PropertiesLayout: new RuntimeBundleResidentPropertiesLayout(
                ExampleTrueIndex: 107,
                PropertiesSceneOffset: 0xF28C,
                PropertiesBytes: 0x50,
                InternalRoutePointer: 0xF2B4,
                RoutePointCount: 2,
                PropertiesComponentHeaderSceneOffset: 0xDF54,
                PropertiesComponentBytes: 0x18B4,
                PointerFixupListSceneOffset: 0x11830,
                PointerFixupCount: 0xBE,
                TrailingScenePaddingBytes: 0x4D4),
            EvidenceNote: "Magic Crafters data entry 34 and overlay entry 33 contain checked actor roots and dispatch handlers for both the Wizard and lightning families. T107 owns a checked 0x50-byte/two-point properties block. T27 owns a unique 0x74-byte properties extent and the native fixup list contains both its reusable internal-pointer fixup at 0xE9BC and one target-only fixup at 0xE9F8 that must be removed when the Wizard route padding replaces that field."),
        Overlay: null,
        Data: null,
        Hooks: null,
        Textures: [],
        Guards: [],
        EvidenceNote: "Magic Crafters natively contains four actor-0x011B Green Wizards plus the 0x0026 lightning dependency. V1's expanded-properties candidate isolated a runtime miss after casting, attackability, death, sound, and reward had passed. The replacement v2 recipe reused T27's private properties extent without scene growth; DuckStation then confirmed the real lightning hit path while the previously proven Wizard behavior remained normal. The exact T107-to-T27 route is promoted for normal Create BIN; other target slots remain guarded until they receive their own extent and fixup proof.",
        RuntimeProofRecipeId: MagicCraftersGreenWizardResidentSwapExporter.RecipeId,
        RuntimeProofOutputSha256: "a94679cffe20075aae54c2a9c6f2f29580857204e43606a033227c3d4705901b");

    private static LevelRuntimeBundleProfile BuildToastyProfile() => new(
        BundleId,
        LevelKey: "toasty",
        Deployment: RuntimeBundleDeploymentKind.Transplanted,
        Evidence: RuntimeBundleEvidenceKind.RuntimeProven,
        ResidentActorIds: [0x011B, 0x0026],
        InstanceLayout: new RuntimeBundleInstanceLayout(
            PropertiesDonorLevelKey: "wizardpeak",
            PropertiesDonorTrueIndex: 6,
            PropertiesBytes: 0x50,
            InternalRouteAnchorOffset: 0x28,
            FirstRoutePointOffset: 0x30,
            RoutePointStride: 0x10,
            RoutePointCount: 2,
            AddPointerFixupDelta: 1,
            SwapPointerFixupDelta: -1,
            PreserveTargetPodOrGroup: false,
            TranslateDonorRouteFromSpawn: true,
            Note: "The proven T0 replacement removes one stale imported properties-word fixup (0x62 to 0x61). A genuinely additional Toasty Wizard must allocate a fresh block and add its pointer fixup instead."),
        ResidentDetection: null,
        Overlay: new RuntimeBundleOverlayTarget(
            OverlayEntry: 17,
            NativeOverlayBytes: 0xA000,
            RequiredExpandedOverlayBytes: 0x17800,
            MaximumExpandedOverlayBytes: 0x17800,
            RequiredWadGrowthBytes: 0xD800,
            AvailableIsoGrowthBytes: 0xB90800,
            OverlayLoadAddress: 0x8007AA38,
            OriginalCopyBufferAddress: 0x80084A10,
            RelocatedCopyBufferAddress: 0x80092238,
            CopyBufferCeilingAddress: 0x800926D4,
            PostCopyBufferFootprintBytes: 0xF5220,
            LowerPolygonBufferAddress: 0x80187BB0,
            MinimumPolygonBufferMarginBytes: 0x2BC,
            ActualPolygonBufferMarginBytes: 0x758,
            ExecutableCopyBufferHiOffset: 0x4B130,
            ExecutableCopyBufferLoOffset: 0x4B134),
        Data: new RuntimeBundleDataTarget(
            DataEntry: 18,
            SceneBaseOffset: 0x1B7000,
            SceneBytes: 0xD800,
            GreenWizardPackageOffset: 0x15326C,
            GreenWizardPackageBytes: 0x35AC,
            GreenWizardRootSlotOffset: 0x54,
            ReplacedActorId: 0x00EA,
            LightningPackageOffset: 0x156818,
            LightningPackageBytes: 0x564,
            LightningRootSlotOffset: 0xB0,
            ActorIdTableOffset: 0x128),
        Hooks: new RuntimeBundleHookTarget(
            DispatchHookAddress: 0x8007B078,
            DispatchContinueAddress: 0x8007B084,
            NextMobyAddress: 0x80081D5C,
            DispatchShimAddress: 0x80091EC0,
            GreenWizardHandlerAddress: 0x8008C818,
            LightningHandlerAddress: 0x80088AD0,
            LightningDamagePatchAddress: 0x80088B90,
            LightningImpactBurstPatchAddress: 0x80088B9C,
            LightningImpactSafeExitAddress: 0x80090A50,
            WizardSpawnInitializerAddress: 0x80090DD8,
            ParticleSpawnTableAddress: 0x8007AE0C,
            ParticleUpdateTableAddress: 0x8007ACC8,
            ParticleSpawnType7Address: 0x80091B40,
            ParticleSpawnType41Address: 0x80091C68,
            ParticleUpdateType7Address: 0x80091D2C,
            ParticleUpdateType41Address: 0x80091DC4,
            ParticleTexturePointerSlotAddress: 0x80076294,
            ParticleDescriptorAddress: 0x80091F54),
        Textures: BuildToastyTextureTargets(),
        Guards:
        [
            new("native-overlay", RuntimeBundleStorageKind.Overlay, 0, 0xA000, RuntimeBundleGuardKind.Sha256, "3b412fceb81e9ab60eef189fc3292387efae0886f4ed0ada0e169f9a190eb049", "Clean Toasty overlay before bundle installation."),
            new("dispatch-hook", RuntimeBundleStorageKind.Overlay, 0x640, 4, RuntimeBundleGuardKind.UInt32, "0x240200FB", "Native Toasty actor-dispatch compare."),
            new("particle-spawn-07-default", RuntimeBundleStorageKind.Overlay, 0x3F0, 4, RuntimeBundleGuardKind.UInt32, "0x800849B8", "Native default type-0x07 spawn target."),
            new("particle-spawn-41-default", RuntimeBundleStorageKind.Overlay, 0x4D8, 4, RuntimeBundleGuardKind.UInt32, "0x800849B8", "Native default type-0x41 spawn target."),
            new("particle-update-07-default", RuntimeBundleStorageKind.Overlay, 0x2AC, 4, RuntimeBundleGuardKind.UInt32, "0x800838CC", "Native default type-0x07 update target."),
            new("particle-update-41-default", RuntimeBundleStorageKind.Overlay, 0x394, 4, RuntimeBundleGuardKind.UInt32, "0x800838CC", "Native default type-0x41 update target."),
            new("green-wizard-package-destination", RuntimeBundleStorageKind.LevelData, 0x15326C, 0x35AC, RuntimeBundleGuardKind.Sha256, "ede30c6f83889d80c8c9a3878a18f8cf5c0ef974f86b059069b1a508dc9049df", "Clean unused-looking actor-0x00EA package replaced in place."),
            new("lightning-package-gap", RuntimeBundleStorageKind.LevelData, 0x156818, 0x564, RuntimeBundleGuardKind.Sha256, "f4655445a60a5c0a3f9632eb534deb72861cd1acd7e40aaa67292d18afcde834", "Checked Toasty zero-gap destination for actor 0x0026."),
            new("instance-properties-destination", RuntimeBundleStorageKind.Scene, 0x9EE8, 0x50, RuntimeBundleGuardKind.Sha256, "7ba44839ac22e9fe3e6bc5512b502d716639e18390d09a42567450d4979c0c47", "Toasty T0 properties-space preimage."),
            new("scene-pointer-fixups", RuntimeBundleStorageKind.Scene, 0xD314, 0x18C, RuntimeBundleGuardKind.Sha256, "0d9d2f824e1a22c9eaea0a8f01b4142d3b37795387e27c42b7fee12596c75c6e", "Toasty scene pointer-fixup count and list preimage."),
            new("copy-buffer-hi16", RuntimeBundleStorageKind.Executable, 0x4B130, 4, RuntimeBundleGuardKind.UInt32, "0x3C028008", "SCUS Toasty copy-buffer HI16 word."),
            new("copy-buffer-lo16", RuntimeBundleStorageKind.Executable, 0x4B134, 4, RuntimeBundleGuardKind.UInt32, "0x24424A10", "SCUS Toasty copy-buffer LO16 word.")
        ],
        EvidenceNote: "Toasty v11 is runtime-proven in DuckStation for casting, textured lightning and trail particles, taking a hit without corruption, death, gem reward, route, animation, and model textures.",
        RuntimeProofRecipeId: "toasty.wizardpeak.greenWizard.compactDependencyBundle.propsFixup.impactBurstQuarantine.genericHurtDamage.textureDependencies.lightningTrailParticleTexture.v11",
        RuntimeProofOutputSha256: "e2ec0b112c9f7782ec85e66118cf157550938e9c49146339df6d1000ffbff5e7");

    private static IReadOnlyList<RuntimeBundleTextureTarget> BuildToastyTextureTargets() =>
    [
        new(
            "wizard-accent",
            0x50900,
            0x50900,
            32,
            0x10,
            0x400,
            0x7A8E0,
            0x7A8E0,
            0x20,
            "8ff165f1d889594f9221913fdfa69f471eb33c57ea0780be4b1ddf8f22317c12",
            "2d0ba2bbf0173d3abfdae683ca0addbcb837b8296f46218b444871d7fa09b38d",
            "60a0eb6d64d068d96e76de01db8bf4b942d0771fefbef6baed7d2964940522af",
            "d09a0a5129dbe7a6918a245979964d2b7cdfba2dc28cad3bf38cb2d7534d991a"),
        new(
            "wizard-special",
            0x58910,
            0x58910,
            32,
            0x10,
            0x400,
            0x708A0,
            0x708A0,
            0x20,
            "2229d62ddadd04752fcc5790b019b55d166b774007ab646e46f1d7a440069dea",
            "6a50dfa98235d46a36c173c6e52440c08e549ec541db76545ef1ca950b964d06",
            "5cb82309aae7685152d17f22df8e9e8a55d5202b449adf08f580a254038798a1",
            "c3532586916a3ab267e554e3ec749c693d221833095527d96f3d141e0b4cf4d1"),
        new(
            "wizard-main",
            0x58960,
            0x58960,
            32,
            0x10,
            0x400,
            0x72980,
            0x72980,
            0x20,
            "6832e7f686f47f7224b1e7b1bb136885d2326650bde4eaf99cffce3c74dba445",
            "8c691b13eb6cf14816f01d18335277075783b482065dec7eddb5903b86b85401",
            "98c5ea6b2eaeeb703c65f10e6ba11e9f2d5b4272a4bbc261d00ef178cfd397ba",
            "6e684213fd836b22512bcad1dc2c7d0558adda6fbefece005598688bbc2e6257"),
        new(
            "lightning",
            0x70850,
            0x70850,
            4,
            0x02,
            0x400,
            0x78840,
            0x78840,
            0x20,
            "e6f48a0036f29213687545ad901eb55949d15e150213f2db8b32f248d55ec411",
            "015d0f8bec5b09f6315e270c74f087aaa8afdd3bbedae61231a040a33bb949f1",
            "ab654734be1754fd47a82b2974389dac5b3bcca0e0015604e6724b56d1492d00",
            "b1d0e05858a79820fc5dec9197e6678e9075fc4f05dba5548736a17c8bd6eb71"),
        new(
            "lightning-trail-particle-07",
            0x78810,
            0x78AB0,
            32,
            0x10,
            0x400,
            0x759A0,
            0x34C60,
            0x20,
            "31f14b4cd1994f468e2bd1d9d1937ca4e7bccd276cd32efa37ecfba4ecb92249",
            "076a27c79e5ace2a3d47f9dd2e83e4ff6ea8872b3c2218f66c92b89b55f36560",
            "c8c516666d7b96b4039b451cffcf69e8c610a93a112532859932befbd2ea9384",
            "66687aadf862bd776c8fc18b8e9f8e20089714856ee233b3902a591d0d5f2925")
    ];

    private static GreenWizardInstancePlan BuildToastyProofInstance() => new(
        Id: "toasty.green-wizard.t0.runtime-proof.v11",
        TargetLevelKey: "toasty",
        Mode: GreenWizardPlacementMode.ReplaceExistingSlot,
        TargetTrueIndex: 0,
        PropertiesPointerFieldSceneOffset: 0x629C,
        PropertiesPointerValue: 0x9EE8,
        PropertiesSceneOffset: 0x9EE8,
        PropertiesBytes: 0x50,
        RebasedInternalRoutePointer: 0x9F10,
        PointerFixupListSceneOffset: 0xD314,
        PointerFixupCountBefore: 0x62,
        PointerFixupCountAfter: 0x61,
        RemovedPointerFixup: 0x9F28,
        RewardValue: 1,
        RoutePoints:
        [
            new(119081, 109773, 16163),
            new(112558, 106629, 16163)
        ],
        PropertiesTargetPreimageSha256: "7ba44839ac22e9fe3e6bc5512b502d716639e18390d09a42567450d4979c0c47",
        PropertiesInstalledSha256: "7355013684269348d96a79107c7e6ad18ba991ceb00c02a0ed3bf986d54e15c7",
        PointerFixupsTargetPreimageSha256: "0d9d2f824e1a22c9eaea0a8f01b4142d3b37795387e27c42b7fee12596c75c6e",
        PointerFixupsInstalledSha256: "2a4b43e97eb7fa8d995fb00169fa42c8bed785e37f7790d38aa20efa9176f037");
}
