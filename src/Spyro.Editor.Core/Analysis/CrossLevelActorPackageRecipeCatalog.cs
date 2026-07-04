using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.Core.Analysis;

public static class CrossLevelActorPackageRecipeCatalog
{
    private static readonly IReadOnlyList<CrossLevelActorPackageRecipe> Recipes =
    [
        new CrossLevelActorPackageRecipe(
            Id: "artisans.peacekeepers.lockedChest.package.safeGapActorId.v4",
            TargetLevelKey: "artisans",
            SourceLevelKey: "peacekeepers",
            Family: "lockedChest",
            Mode: "RegisterCompanionRoot",
            Status: "experimental-image-write",
            Risk: "Copies a missing chest actor package into an Artisans zero gap and registers one actor root. This now writes only to disposable test BIN/CUE output and still needs in-game key/open validation before becoming normal release behavior.",
            Description: "Peace Keepers key chest actor package copied into the proven Artisans 0x30800 zero gap with a single actor-id root registration.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3978", "0x30800", "0x2830")
            ],
            RootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xDC", "0x30800", "0x00AE", "", "")
            ],
            ReplaceRootEntries: [],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "artisans.townsquare.springChest.package.minimal00C2SafeGap30800.v1",
            TargetLevelKey: "artisans",
            SourceLevelKey: "townsquare",
            Family: "springChest",
            Mode: "RegisterCompanionRoot",
            Status: "experimental-plan-only",
            Risk: "Imports the Town Square spring chest controller/helper/shell route into Artisans. It replaces the existing 0x00C2 route, so it must stay opt-in until disposable-disc testing proves no vanilla chest behavior is broken.",
            Description: "Town Square spring controller plus minimal helper/shell roots copied into the proven Artisans 0x30800 zero gap.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1BF470", "0x30800", "0x03E8"),
                new CrossLevelActorPackageCopySegment("0x1BF858", "0x30BE8", "0x08BC"),
                new CrossLevelActorPackageCopySegment("0x1C0114", "0x314A4", "0x0528")
            ],
            RootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xDC", "0x30BE8", "0x0186", "", ""),
                new CrossLevelActorPackageRootEntry("0xE0", "0x314A4", "0x0149", "", "")
            ],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0x74", "0x30800", "0x00C2", "0x00C2", "")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.townsquare.springChest.package.local00C2Shell0149Over000E.v2",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "townsquare",
            Family: "springChest",
            Mode: "ReplaceUnusedRoot",
            Status: "experimental-plan-only",
            Risk: "Preferred Stone Hill follow-up from the legacy pair exporter. It preserves Stone Hill's native 0x00C2 controller route, copies only the Town Square 0x0149 shell over unused-looking Stone Hill actor 0x000E, and rebases shell-internal dependency pointers to Stone Hill resident roots. Keep guarded until the paired controller/shell candidate boots and pops correctly in-game.",
            Description: "Town Square Spring Chest shell copied over Stone Hill actor 0x000E while using Stone Hill's local 0x00C2 controller package.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1C0114", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces an unused-looking Stone Hill actor root with the imported Spring Chest shell while preserving native 0x00C2 controller behavior.")
            ],
            InternalDependencyRebases:
            [
                new CrossLevelActorPackageDependencyRebase("0x1C063C", "0x1D03A0", "0x48EC", "0x0021", "Reuse Stone Hill resident actor 0x0021 dependency root."),
                new CrossLevelActorPackageDependencyRebase("0x1C4F28", "0x1D64B4", "0x0868", "0x0009", "Reuse Stone Hill resident actor 0x0009 dependency root."),
                new CrossLevelActorPackageDependencyRebase("0x1C7CC8", "0x1D4C8C", "0x103C", "0x01A5", "Reuse Stone Hill resident actor 0x01A5 dependency root.")
            ]),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.townsquare.springChest.package.preserveNative00C2ZeroGap26928.v3",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "townsquare",
            Family: "springChest",
            Mode: "RegisterCompanionRoot",
            Status: "experimental-plan-only",
            Risk: "Safer follow-up after the native 0x00C2 replacement candidate froze Stone Hill on the flying/loading screen. Stone Hill already has 21 records using actor 0x00C2, so this recipe preserves Stone Hill's native 0x00C2 package and only imports the missing Town Square 0x0186/0x0149 helper roots into zero gaps.",
            Description: "Town Square spring helper/shell packages imported into Stone Hill without replacing Stone Hill's existing 0x00C2 package.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1BF858", "0x26928", "0x08BC"),
                new CrossLevelActorPackageCopySegment("0x1C0114", "0x271E4", "0x0528")
            ],
            RootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xE8", "0x26928", "0x0186", "", "Registers the imported Town Square helper package while leaving Stone Hill's native 0x00C2 route intact."),
                new CrossLevelActorPackageRootEntry("0xEC", "0x271E4", "0x0149", "", "Registers the imported Town Square Spring Chest actor package while preserving native Stone Hill actors.")
            ],
            ReplaceRootEntries: [],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.townsquare.springChest.package.preserveNative00C2ZeroGap26928NoExeHelper.v4",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "townsquare",
            Family: "springChest",
            Mode: "RegisterCompanionRootNoExeHelper",
            Status: "experimental-plan-only",
            Risk: "Isolation follow-up after the zero-gap Spring Chest candidate still froze Stone Hill on the flying/loading screen. This keeps the same Town Square helper/shell package import but suppresses the custom EXE helper and reward-row shim so the next test can distinguish actor-package load failure from helper-hook failure.",
            Description: "Town Square spring helper/shell packages imported into Stone Hill without replacing Stone Hill's native 0x00C2 package and without the custom helper hook.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1BF858", "0x26928", "0x08BC"),
                new CrossLevelActorPackageCopySegment("0x1C0114", "0x271E4", "0x0528")
            ],
            RootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xE8", "0x26928", "0x0186", "", "Registers the imported Town Square helper package while leaving Stone Hill's native 0x00C2 route intact."),
                new CrossLevelActorPackageRootEntry("0xEC", "0x271E4", "0x0149", "", "Registers the imported Town Square Spring Chest actor package while preserving native Stone Hill actors.")
            ],
            ReplaceRootEntries: [],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.townsquare.springChest.recordsOnlyNoActorPackage.v5",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "townsquare",
            Family: "springChest",
            Mode: "RecordsOnlyNoActorPackage",
            Status: "experimental-plan-only",
            Risk: "Narrow isolation test after Stone Hill froze even without the custom EXE helper. This imports only the Town Square Spring Chest source records and special data, with no actor-package copy and no actor-root registration. It is not expected to provide final behavior; it distinguishes record/special-data load safety from actor-package/root load safety.",
            Description: "Town Square Spring Chest controller/shell source records copied into Stone Hill without actor package imports.",
            CopySegments: [],
            RootEntries: [],
            ReplaceRootEntries: [],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.townsquare.springChest.packageCopyOnlyNoRoot.v6",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "townsquare",
            Family: "springChest",
            Mode: "CopyOnlyNoRoot",
            Status: "experimental-plan-only",
            Risk: "Isolation follow-up after records-only loaded but behaved like a normal Stone Hill charge/flame chest, while package-plus-root candidates froze Stone Hill. This copies the Town Square helper/shell package bytes into zero gaps but intentionally does not register actor roots or add the custom helper/reward row, so the next test can distinguish copied package-byte safety from root registration/loading failure.",
            Description: "Town Square spring helper/shell package bytes copied into Stone Hill without actor-root registration.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1BF858", "0x26928", "0x08BC"),
                new CrossLevelActorPackageCopySegment("0x1C0114", "0x271E4", "0x0528")
            ],
            RootEntries: [],
            ReplaceRootEntries: [],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.townsquare.springChest.helperSegmentCopyOnlyNoRoot.v7",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "townsquare",
            Family: "springChest",
            Mode: "CopyOnlyNoRootHelperSegment",
            Status: "experimental-plan-only",
            Risk: "Narrow isolation follow-up after copying both Town Square spring helper/shell package segments froze Stone Hill even without actor roots. This copies only the first 0x0186 helper segment into the same zero range, with no roots and no helper/reward row, to determine whether this segment or range alone breaks level loading.",
            Description: "Town Square spring helper package bytes copied into Stone Hill without actor-root registration.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1BF858", "0x26928", "0x08BC")
            ],
            RootEntries: [],
            ReplaceRootEntries: [],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.townsquare.springChest.rangeProbeCopyOnlyNoRoot.v8",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "townsquare",
            Family: "springChest",
            Mode: "CopyOnlyNoRootRangeProbe",
            Status: "experimental-plan-only",
            Risk: "Tiny range probe after copying the first helper segment froze Stone Hill. This writes only the first four nonzero bytes from the Town Square helper segment into the same target range, with no roots and no helper/reward row, to determine whether the target range itself must remain zero or whether the full actor-package structure is the problem.",
            Description: "Four-byte nonzero probe at the Stone Hill spring helper target range.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1BF858", "0x26928", "0x0004")
            ],
            RootEntries: [],
            ReplaceRootEntries: [],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.townsquare.springChest.prefix0100CopyOnlyNoRoot.v9",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "townsquare",
            Family: "springChest",
            Mode: "CopyOnlyNoRootPrefix0100",
            Status: "experimental-plan-only",
            Risk: "Larger range probe after the four-byte write loaded but still behaved like a normal Stone Hill flame/charge chest. This writes the first 0x0100 bytes of the Town Square helper segment into the same target range, with no roots and no helper/reward row, to bracket the package-copy length that starts breaking Stone Hill loading.",
            Description: "First 0x0100 bytes of the Town Square spring helper package copied into Stone Hill without actor-root registration.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1BF858", "0x26928", "0x0100")
            ],
            RootEntries: [],
            ReplaceRootEntries: [],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.townsquare.springChest.prefix0400CopyOnlyNoRoot.v10",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "townsquare",
            Family: "springChest",
            Mode: "CopyOnlyNoRootPrefix0400",
            Status: "experimental-plan-only",
            Risk: "Larger prefix probe after the first 0x0100 bytes loaded but still behaved like a normal Stone Hill flame/charge chest. This writes the first 0x0400 bytes of the Town Square helper segment into the same target range, with no roots and no helper/reward row, to continue bracketing the package-copy length that starts breaking Stone Hill loading.",
            Description: "First 0x0400 bytes of the Town Square spring helper package copied into Stone Hill without actor-root registration.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1BF858", "0x26928", "0x0400")
            ],
            RootEntries: [],
            ReplaceRootEntries: [],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.townsquare.springChest.prefix0600CopyOnlyNoRoot.v11",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "townsquare",
            Family: "springChest",
            Mode: "CopyOnlyNoRootPrefix0600",
            Status: "experimental-plan-only",
            Risk: "Middle prefix probe after the first 0x0400 bytes loaded but still behaved like a normal Stone Hill flame/charge chest, while the full 0x08BC helper segment soft-locked. This writes the first 0x0600 bytes of the Town Square helper segment into the same target range, with no roots and no helper/reward row, to narrow the soft-lock boundary.",
            Description: "First 0x0600 bytes of the Town Square spring helper package copied into Stone Hill without actor-root registration.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1BF858", "0x26928", "0x0600")
            ],
            RootEntries: [],
            ReplaceRootEntries: [],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.townsquare.springChest.prefix0700CopyOnlyNoRoot.v12",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "townsquare",
            Family: "springChest",
            Mode: "CopyOnlyNoRootPrefix0700",
            Status: "experimental-plan-only",
            Risk: "Middle prefix probe after the first 0x0600 bytes loaded but still behaved like a normal Stone Hill flame/charge chest, while the full 0x08BC helper segment soft-locked. This writes the first 0x0700 bytes of the Town Square helper segment into the same target range, with no roots and no helper/reward row, to narrow the soft-lock boundary.",
            Description: "First 0x0700 bytes of the Town Square spring helper package copied into Stone Hill without actor-root registration.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1BF858", "0x26928", "0x0700")
            ],
            RootEntries: [],
            ReplaceRootEntries: [],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.townsquare.springChest.package.fullRebaseZeroGap26928.v13",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "townsquare",
            Family: "springChest",
            Mode: "RegisterCompanionRootFullDependencyRebase",
            Status: "experimental-plan-only",
            Risk: "Follow-up after prefix-only package probes loaded but behaved as a normal flame/charge chest. This imports the Town Square helper and Spring Chest shell together, registers their native actor roots, and rebases internal references to matching Stone Hill resident packages. Keep guarded until a disposable-disc test proves Stone Hill boots and the chest pops correctly.",
            Description: "Town Square spring helper/shell packages imported into Stone Hill with dependency rebases to resident Stone Hill actor packages.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1BF858", "0x26928", "0x08BC"),
                new CrossLevelActorPackageCopySegment("0x1C0114", "0x271E4", "0x0528")
            ],
            RootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xE8", "0x26928", "0x0186", "", "Registers the imported Town Square helper package while leaving Stone Hill's native 0x00C2 route intact."),
                new CrossLevelActorPackageRootEntry("0xEC", "0x271E4", "0x0149", "", "Registers the imported Town Square Spring Chest actor package while preserving native Stone Hill actors.")
            ],
            ReplaceRootEntries: [],
            InternalDependencyRebases:
            [
                new CrossLevelActorPackageDependencyRebase("0x1C063C", "0x1D03A0", "0x48EC", "0x0021", "Reuse Stone Hill resident actor 0x0021 dependency root."),
                new CrossLevelActorPackageDependencyRebase("0x1C4F28", "0x1D64B4", "0x0868", "0x0009", "Reuse Stone Hill resident actor 0x0009 dependency root."),
                new CrossLevelActorPackageDependencyRebase("0x1C5790", "0x1D6D1C", "0x2538", "0x006E", "Reuse Stone Hill resident actor 0x006E dependency root."),
                new CrossLevelActorPackageDependencyRebase("0x1C7CC8", "0x1D4C8C", "0x103C", "0x01A5", "Reuse Stone Hill resident actor 0x01A5 dependency root."),
                new CrossLevelActorPackageDependencyRebase("0x1C8D04", "0x1D5CC8", "0x07EC", "0x000E", "Reuse Stone Hill resident actor 0x000E dependency root.")
            ]),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.townsquare.springChest.package.fullControllerAlias01FEZeroGap26928.v14",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "townsquare",
            Family: "springChest",
            Mode: "RegisterCompanionRootAlias01FEFullDependencyRebase",
            Status: "experimental-plan-only",
            Risk: "Follow-up after the full helper/shell rebase loaded but still behaved as a normal Stone Hill flame/charge chest. This imports Town Square's spring controller as a private 0x01FE actor alias, plus its helper/shell packages, so the added controller no longer uses Stone Hill's native chest actor. Keep guarded until a disposable-disc test proves Stone Hill boots and the placed chest pops correctly.",
            Description: "Town Square spring controller/helper/shell packages imported into Stone Hill as an isolated 0x01FE controller alias with dependency rebases.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1BF470", "0x26928", "0x03E8"),
                new CrossLevelActorPackageCopySegment("0x1BF858", "0x26D10", "0x08BC"),
                new CrossLevelActorPackageCopySegment("0x1C0114", "0x275CC", "0x0528")
            ],
            RootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xE8", "0x26928", "0x01FE", "", "Registers Town Square's spring controller as an isolated alias instead of using Stone Hill's native 0x00C2 chest route."),
                new CrossLevelActorPackageRootEntry("0xEC", "0x26D10", "0x0186", "", "Registers the imported Town Square helper package after the controller alias."),
                new CrossLevelActorPackageRootEntry("0xF0", "0x275CC", "0x0149", "", "Registers the imported Town Square Spring Chest shell package after the helper.")
            ],
            ReplaceRootEntries: [],
            InternalDependencyRebases:
            [
                new CrossLevelActorPackageDependencyRebase("0x1BF470", "0x26928", "0x03E8", "0x01FE", "Rebase imported Town Square spring controller self/peer references to the Stone Hill alias location."),
                new CrossLevelActorPackageDependencyRebase("0x1BF858", "0x26D10", "0x08BC", "0x0186", "Rebase imported Town Square helper references to the Stone Hill alias location."),
                new CrossLevelActorPackageDependencyRebase("0x1C0114", "0x275CC", "0x0528", "0x0149", "Rebase imported Town Square Spring Chest shell references to the Stone Hill alias location."),
                new CrossLevelActorPackageDependencyRebase("0x1C063C", "0x1D03A0", "0x48EC", "0x0021", "Reuse Stone Hill resident actor 0x0021 dependency root."),
                new CrossLevelActorPackageDependencyRebase("0x1C4F28", "0x1D64B4", "0x0868", "0x0009", "Reuse Stone Hill resident actor 0x0009 dependency root."),
                new CrossLevelActorPackageDependencyRebase("0x1C5790", "0x1D6D1C", "0x2538", "0x006E", "Reuse Stone Hill resident actor 0x006E dependency root."),
                new CrossLevelActorPackageDependencyRebase("0x1C7CC8", "0x1D4C8C", "0x103C", "0x01A5", "Reuse Stone Hill resident actor 0x01A5 dependency root."),
                new CrossLevelActorPackageDependencyRebase("0x1C8D04", "0x1D5CC8", "0x07EC", "0x000E", "Reuse Stone Hill resident actor 0x000E dependency root.")
            ]),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.townsquare.springChest.package.native00C2ReplaceZeroGap26928.v2",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "townsquare",
            Family: "springChest",
            Mode: "RegisterCompanionRoot",
            Status: "experimental-plan-only",
            Risk: "This is the native-shaped follow-up to the frozen alias recipe. The old 0x01FE alias load froze in Stone Hill; this candidate instead replaces Stone Hill's existing 0x00C2 package with Town Square's 0x00C2 package and adds only the native 0x0186/0x0149 helper roots. Keep it guarded until a disposable-disc load test proves Stone Hill boots and vanilla chests still work.",
            Description: "Town Square spring controller/helper/shell route mapped onto Stone Hill using the same actor-root shape seen in native spring-chest levels, with no private 0x01FE alias.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1BF470", "0x1BB164", "0x03E8", "overwrite-existing-actor-package", "0x74", "0x00C2"),
                new CrossLevelActorPackageCopySegment("0x1BF858", "0x26928", "0x08BC"),
                new CrossLevelActorPackageCopySegment("0x1C0114", "0x271E4", "0x0528")
            ],
            RootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xE8", "0x26928", "0x0186", "", ""),
                new CrossLevelActorPackageRootEntry("0xEC", "0x271E4", "0x0149", "", "")
            ],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0x74", "0x1BB164", "0x00C2", "0x00C2", "Keeps the native Stone Hill 0x00C2 actor slot while replacing its package bytes with Town Square's spring-compatible package.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.drycanyon.springChest.package.native0186And0149T107NoExeHelper.v110",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "drycanyon",
            Family: "springChest",
            Mode: "RegisterDryCanyonNative0186AndReplaceUnused000ENative0149T107NoExeHelper",
            Status: "experimental-plan-only",
            Risk: "Dry Canyon route after repeated Peace Keepers candidates either crashed or lacked complete Spring Chest behavior in Stone Hill. Dry Canyon natively registers both helper actor 0x0186 and Spring Chest actor 0x0149, so this imports that native pair while preserving Stone Hill's 0x00C2 chest package. It is disposable-candidate only until in-game testing proves Stone Hill boots and the chest reacts safely.",
            Description: "Dry Canyon Spring Chest helper and shell packages imported into Stone Hill, with one native Dry Canyon T107 Spring Chest row and no custom helper.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1A1694", "0x26928", "0x08BC"),
                new CrossLevelActorPackageCopySegment("0x1689E0", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xE8", "0x26928", "0x0186", "", "Registers Dry Canyon's native Spring Chest helper actor while preserving Stone Hill's native 0x00C2 chest package.")
            ],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Dry Canyon Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.drycanyon.springChest.package.native0186And0149T107RebasedNoExeHelper.v111",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "drycanyon",
            Family: "springChest",
            Mode: "RegisterDryCanyonNative0186AndReplaceUnused000ENative0149T107RebasedNoExeHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after the unrebased Dry Canyon helper/shell import crashed Stone Hill while loading. This keeps the same one-row Dry Canyon T107 candidate, but rebases Dry Canyon actor-package references to their Stone Hill resident equivalents before writing. It is disposable-candidate only until in-game testing proves Stone Hill boots and the chest reacts safely.",
            Description: "Dry Canyon Spring Chest helper and shell packages imported into Stone Hill with internal actor-package references rebased to Stone Hill equivalents.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1A1694", "0x26928", "0x08BC"),
                new CrossLevelActorPackageCopySegment("0x1689E0", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xE8", "0x26928", "0x0186", "", "Registers Dry Canyon's native Spring Chest helper actor while preserving Stone Hill's native 0x00C2 chest package.")
            ],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Dry Canyon Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases:
            [
                new CrossLevelActorPackageDependencyRebase("0x1685F8", "0x1BB164", "0x03E8", "0x00C2", "Reuse Stone Hill resident 0x00C2 chest controller package instead of Dry Canyon's controller package."),
                new CrossLevelActorPackageDependencyRebase("0x1689E0", "0x1D5CC8", "0x0528", "0x0149", "Rebase imported Dry Canyon Spring Chest shell self/peer references to the Stone Hill target location."),
                new CrossLevelActorPackageDependencyRebase("0x19CDA8", "0x1D03A0", "0x48EC", "0x0021", "Reuse Stone Hill resident actor 0x0021 dependency root."),
                new CrossLevelActorPackageDependencyRebase("0x1A1694", "0x26928", "0x08BC", "0x0186", "Rebase imported Dry Canyon Spring Chest helper self/peer references to the Stone Hill target location."),
                new CrossLevelActorPackageDependencyRebase("0x1A4988", "0x1D4C8C", "0x103C", "0x01A5", "Reuse Stone Hill resident actor 0x01A5 dependency root."),
                new CrossLevelActorPackageDependencyRebase("0x1A59C4", "0x1D5CC8", "0x07EC", "0x000E", "Reuse the Stone Hill overwritten 0x000E package slot for Dry Canyon 0x000E dependency references."),
                new CrossLevelActorPackageDependencyRebase("0x1A6A70", "0x1D64B4", "0x0868", "0x0009", "Reuse Stone Hill resident actor 0x0009 dependency root."),
                new CrossLevelActorPackageDependencyRebase("0x1A72D8", "0x1D6D1C", "0x2538", "0x006E", "Reuse Stone Hill resident actor 0x006E dependency root.")
            ]),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.drycanyon.springChest.package.native0186And0149T107Import00CERebasedNoExeHelper.v112",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "drycanyon",
            Family: "springChest",
            Mode: "RegisterDryCanyonNative0186AndReplaceUnused000ENative0149T107Import00CERebasedNoExeHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after the unrebased Dry Canyon helper/shell import crashed Stone Hill while loading and the dependency scan found many helper/shell references into Dry Canyon actor 0x00CE. This replaces Stone Hill's unused source-record-free actor 0x0000 root with Dry Canyon 0x00CE, then rebases 0x00CE references to that imported package. Dry Canyon 0x00E6 remains unresolved in this candidate, so it is a disposable diagnostic only.",
            Description: "Dry Canyon Spring Chest helper and shell packages imported into Stone Hill with the heavily referenced 0x00CE support package imported into an unused Stone Hill actor slot.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x181130", "0x18E800", "0x9430", "overwrite-unused-actor-package", "0x50", "0x0000"),
                new CrossLevelActorPackageCopySegment("0x1A1694", "0x26928", "0x08BC"),
                new CrossLevelActorPackageCopySegment("0x1689E0", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xE8", "0x26928", "0x0186", "", "Registers Dry Canyon's native Spring Chest helper actor while preserving Stone Hill's native 0x00C2 chest package.")
            ],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0x50", "0x18E800", "0x00CE", "0x0000", "Replaces Stone Hill's source-record-free actor 0x0000 package with Dry Canyon's 0x00CE support package for this disposable Spring Chest diagnostic."),
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Dry Canyon Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases:
            [
                new CrossLevelActorPackageDependencyRebase("0x1685F8", "0x1BB164", "0x03E8", "0x00C2", "Reuse Stone Hill resident 0x00C2 chest controller package instead of Dry Canyon's controller package."),
                new CrossLevelActorPackageDependencyRebase("0x1689E0", "0x1D5CC8", "0x0528", "0x0149", "Rebase imported Dry Canyon Spring Chest shell self/peer references to the Stone Hill target location."),
                new CrossLevelActorPackageDependencyRebase("0x181130", "0x18E800", "0x9430", "0x00CE", "Rebase Dry Canyon 0x00CE support references to the imported Stone Hill diagnostic package."),
                new CrossLevelActorPackageDependencyRebase("0x19CDA8", "0x1D03A0", "0x48EC", "0x0021", "Reuse Stone Hill resident actor 0x0021 dependency root."),
                new CrossLevelActorPackageDependencyRebase("0x1A1694", "0x26928", "0x08BC", "0x0186", "Rebase imported Dry Canyon Spring Chest helper self/peer references to the Stone Hill target location."),
                new CrossLevelActorPackageDependencyRebase("0x1A4988", "0x1D4C8C", "0x103C", "0x01A5", "Reuse Stone Hill resident actor 0x01A5 dependency root."),
                new CrossLevelActorPackageDependencyRebase("0x1A59C4", "0x1D5CC8", "0x07EC", "0x000E", "Reuse the Stone Hill overwritten 0x000E package slot for Dry Canyon 0x000E dependency references."),
                new CrossLevelActorPackageDependencyRebase("0x1A6A70", "0x1D64B4", "0x0868", "0x0009", "Reuse Stone Hill resident actor 0x0009 dependency root."),
                new CrossLevelActorPackageDependencyRebase("0x1A72D8", "0x1D6D1C", "0x2538", "0x006E", "Reuse Stone Hill resident actor 0x006E dependency root.")
            ]),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.drycanyon.springChest.package.native0186And0149T107Import00CE00E6RebasedNoExeHelper.v113",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "drycanyon",
            Family: "springChest",
            Mode: "RegisterDryCanyonNative0186AndReplaceUnused000ENative0149T107Import00CE00E6RebasedNoExeHelper",
            Status: "experimental-plan-only",
            Risk: "Disposable follow-up after importing Dry Canyon 0x00CE left the Spring Chest missing and Stone Hill black-screened near the placed chest. The dependency scan found remaining helper references into Dry Canyon actor 0x00E6, so this candidate imports 0x00CE and 0x00E6 into Stone Hill's source-record-free package gap. It also clears the stale root slot that lands inside the imported 0x00E6 range.",
            Description: "Dry Canyon Spring Chest helper and shell packages imported into Stone Hill with both heavily referenced Dry Canyon support packages 0x00CE and 0x00E6 imported into unused Stone Hill actor-package space.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x181130", "0x18E800", "0x9430", "overwrite-unused-actor-package", "0x50", "0x0000"),
                new CrossLevelActorPackageCopySegment("0x18F520", "0x197C30", "0xD1A0", "overwrite-unused-actor-package-subrange", "0x50", "0x0000", "0x00EA,0x0195"),
                new CrossLevelActorPackageCopySegment("0x1A1694", "0x26928", "0x08BC"),
                new CrossLevelActorPackageCopySegment("0x1689E0", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xE8", "0x26928", "0x0186", "", "Registers Dry Canyon's native Spring Chest helper actor while preserving Stone Hill's native 0x00C2 chest package.")
            ],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0x50", "0x18E800", "0x00CE", "0x0000", "Replaces Stone Hill's source-record-free actor 0x0000 package with Dry Canyon's 0x00CE support package for this disposable Spring Chest diagnostic."),
                new CrossLevelActorPackageRootEntry("0x54", "0x197C30", "0x00E6", "0x00EA", "Replaces Stone Hill's source-record-free 0x00EA package with Dry Canyon's 0x00E6 support package, packed after the imported 0x00CE support package."),
                new CrossLevelActorPackageRootEntry("0x58", "0x00000000", "0x0000", "0x0195", "Clears Stone Hill's stale source-record-free 0x0195 root because its old package start is inside the imported Dry Canyon 0x00E6 byte range."),
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Dry Canyon Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases:
            [
                new CrossLevelActorPackageDependencyRebase("0x1685F8", "0x1BB164", "0x03E8", "0x00C2", "Reuse Stone Hill resident 0x00C2 chest controller package instead of Dry Canyon's controller package."),
                new CrossLevelActorPackageDependencyRebase("0x1689E0", "0x1D5CC8", "0x0528", "0x0149", "Rebase imported Dry Canyon Spring Chest shell self/peer references to the Stone Hill target location."),
                new CrossLevelActorPackageDependencyRebase("0x181130", "0x18E800", "0x9430", "0x00CE", "Rebase Dry Canyon 0x00CE support references to the imported Stone Hill diagnostic package."),
                new CrossLevelActorPackageDependencyRebase("0x18F520", "0x197C30", "0xD1A0", "0x00E6", "Rebase Dry Canyon 0x00E6 support references to the imported Stone Hill diagnostic package."),
                new CrossLevelActorPackageDependencyRebase("0x19CDA8", "0x1D03A0", "0x48EC", "0x0021", "Reuse Stone Hill resident actor 0x0021 dependency root."),
                new CrossLevelActorPackageDependencyRebase("0x1A1694", "0x26928", "0x08BC", "0x0186", "Rebase imported Dry Canyon Spring Chest helper self/peer references to the Stone Hill target location."),
                new CrossLevelActorPackageDependencyRebase("0x1A4988", "0x1D4C8C", "0x103C", "0x01A5", "Reuse Stone Hill resident actor 0x01A5 dependency root."),
                new CrossLevelActorPackageDependencyRebase("0x1A59C4", "0x1D5CC8", "0x07EC", "0x000E", "Reuse the Stone Hill overwritten 0x000E package slot for Dry Canyon 0x000E dependency references."),
                new CrossLevelActorPackageDependencyRebase("0x1A6A70", "0x1D64B4", "0x0868", "0x0009", "Reuse Stone Hill resident actor 0x0009 dependency root."),
                new CrossLevelActorPackageDependencyRebase("0x1A72D8", "0x1D6D1C", "0x2538", "0x006E", "Reuse Stone Hill resident actor 0x006E dependency root.")
            ]),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.drycanyon.springChest.package.native0186And0149T107Import00CE00E6PrefixRebasedNoExeHelper.v114",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "drycanyon",
            Family: "springChest",
            Mode: "RegisterDryCanyonNative0186AndReplaceUnused000ENative0149T107Import00CE00E6PrefixRebasedNoExeHelper",
            Status: "experimental-plan-only",
            Risk: "Disposable rollback from v113 after registering Dry Canyon 0x00E6 made Stone Hill crash while entering. This keeps v112's root-table shape, imports only the referenced prefix of 0x00E6 into the unused gap immediately after imported 0x00CE, and rebases 0x00E6 pointers without registering 0x00E6 as a Stone Hill actor root.",
            Description: "Dry Canyon Spring Chest helper and shell packages imported into Stone Hill with 0x00CE plus a non-rooted 0x00E6 data prefix copied into unused space.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x181130", "0x18E800", "0x9430", "overwrite-unused-actor-package", "0x50", "0x0000"),
                new CrossLevelActorPackageCopySegment("0x18F520", "0x197C30", "0x7000", "overwrite-unused-actor-package-subrange", "0x50", "0x0000", "0x00EA"),
                new CrossLevelActorPackageCopySegment("0x1A1694", "0x26928", "0x08BC"),
                new CrossLevelActorPackageCopySegment("0x1689E0", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xE8", "0x26928", "0x0186", "", "Registers Dry Canyon's native Spring Chest helper actor while preserving Stone Hill's native 0x00C2 chest package.")
            ],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0x50", "0x18E800", "0x00CE", "0x0000", "Replaces Stone Hill's source-record-free actor 0x0000 package with Dry Canyon's 0x00CE support package for this disposable Spring Chest diagnostic."),
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Dry Canyon Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases:
            [
                new CrossLevelActorPackageDependencyRebase("0x1685F8", "0x1BB164", "0x03E8", "0x00C2", "Reuse Stone Hill resident 0x00C2 chest controller package instead of Dry Canyon's controller package."),
                new CrossLevelActorPackageDependencyRebase("0x1689E0", "0x1D5CC8", "0x0528", "0x0149", "Rebase imported Dry Canyon Spring Chest shell self/peer references to the Stone Hill target location."),
                new CrossLevelActorPackageDependencyRebase("0x181130", "0x18E800", "0x9430", "0x00CE", "Rebase Dry Canyon 0x00CE support references to the imported Stone Hill diagnostic package."),
                new CrossLevelActorPackageDependencyRebase("0x18F520", "0x197C30", "0x7000", "0x00E6", "Rebase only the referenced Dry Canyon 0x00E6 support prefix into the non-rooted Stone Hill diagnostic copy."),
                new CrossLevelActorPackageDependencyRebase("0x19CDA8", "0x1D03A0", "0x48EC", "0x0021", "Reuse Stone Hill resident actor 0x0021 dependency root."),
                new CrossLevelActorPackageDependencyRebase("0x1A1694", "0x26928", "0x08BC", "0x0186", "Rebase imported Dry Canyon Spring Chest helper self/peer references to the Stone Hill target location."),
                new CrossLevelActorPackageDependencyRebase("0x1A4988", "0x1D4C8C", "0x103C", "0x01A5", "Reuse Stone Hill resident actor 0x01A5 dependency root."),
                new CrossLevelActorPackageDependencyRebase("0x1A59C4", "0x1D5CC8", "0x07EC", "0x000E", "Reuse the Stone Hill overwritten 0x000E package slot for Dry Canyon 0x000E dependency references."),
                new CrossLevelActorPackageDependencyRebase("0x1A6A70", "0x1D64B4", "0x0868", "0x0009", "Reuse Stone Hill resident actor 0x0009 dependency root."),
                new CrossLevelActorPackageDependencyRebase("0x1A72D8", "0x1D6D1C", "0x2538", "0x006E", "Reuse Stone Hill resident actor 0x006E dependency root.")
            ]),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.drycanyon.springChest.package.native0186And0149T107Import00CE00E6FullNonRootRebasedNoExeHelper.v115",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "drycanyon",
            Family: "springChest",
            Mode: "RegisterDryCanyonNative0186AndReplaceUnused000ENative0149T107Import00CE00E6FullNonRootRebasedNoExeHelper",
            Status: "experimental-plan-only",
            Risk: "Disposable follow-up after v114 still black-screened near the missing chest and v113 crashed during Stone Hill entry. This imports the full 0x00E6 support package bytes so all rebased pointers have backing data, but clears the Stone Hill root slots that would otherwise make the loader treat those bytes as live actors.",
            Description: "Dry Canyon Spring Chest helper and shell packages imported into Stone Hill with 0x00CE plus a full non-rooted 0x00E6 support copy.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x181130", "0x18E800", "0x9430", "overwrite-unused-actor-package", "0x50", "0x0000"),
                new CrossLevelActorPackageCopySegment("0x18F520", "0x197C30", "0xD1A0", "overwrite-unused-actor-package-subrange", "0x50", "0x0000", "0x00EA,0x0195"),
                new CrossLevelActorPackageCopySegment("0x1A1694", "0x26928", "0x08BC"),
                new CrossLevelActorPackageCopySegment("0x1689E0", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xE8", "0x26928", "0x0186", "", "Registers Dry Canyon's native Spring Chest helper actor while preserving Stone Hill's native 0x00C2 chest package.")
            ],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0x50", "0x18E800", "0x00CE", "0x0000", "Replaces Stone Hill's source-record-free actor 0x0000 package with Dry Canyon's 0x00CE support package for this disposable Spring Chest diagnostic."),
                new CrossLevelActorPackageRootEntry("0x54", "0x00000000", "0x0000", "0x00EA", "Clears Stone Hill's source-record-free 0x00EA root because its old package start is inside the imported Dry Canyon 0x00E6 byte range."),
                new CrossLevelActorPackageRootEntry("0x58", "0x00000000", "0x0000", "0x0195", "Clears Stone Hill's source-record-free 0x0195 root because its old package start is inside the imported Dry Canyon 0x00E6 byte range."),
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Dry Canyon Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases:
            [
                new CrossLevelActorPackageDependencyRebase("0x1685F8", "0x1BB164", "0x03E8", "0x00C2", "Reuse Stone Hill resident 0x00C2 chest controller package instead of Dry Canyon's controller package."),
                new CrossLevelActorPackageDependencyRebase("0x1689E0", "0x1D5CC8", "0x0528", "0x0149", "Rebase imported Dry Canyon Spring Chest shell self/peer references to the Stone Hill target location."),
                new CrossLevelActorPackageDependencyRebase("0x181130", "0x18E800", "0x9430", "0x00CE", "Rebase Dry Canyon 0x00CE support references to the imported Stone Hill diagnostic package."),
                new CrossLevelActorPackageDependencyRebase("0x18F520", "0x197C30", "0xD1A0", "0x00E6", "Rebase Dry Canyon 0x00E6 support references to the imported non-rooted Stone Hill diagnostic package."),
                new CrossLevelActorPackageDependencyRebase("0x19CDA8", "0x1D03A0", "0x48EC", "0x0021", "Reuse Stone Hill resident actor 0x0021 dependency root."),
                new CrossLevelActorPackageDependencyRebase("0x1A1694", "0x26928", "0x08BC", "0x0186", "Rebase imported Dry Canyon Spring Chest helper self/peer references to the Stone Hill target location."),
                new CrossLevelActorPackageDependencyRebase("0x1A4988", "0x1D4C8C", "0x103C", "0x01A5", "Reuse Stone Hill resident actor 0x01A5 dependency root."),
                new CrossLevelActorPackageDependencyRebase("0x1A59C4", "0x1D5CC8", "0x07EC", "0x000E", "Reuse the Stone Hill overwritten 0x000E package slot for Dry Canyon 0x000E dependency references."),
                new CrossLevelActorPackageDependencyRebase("0x1A6A70", "0x1D64B4", "0x0868", "0x0009", "Reuse Stone Hill resident actor 0x0009 dependency root."),
                new CrossLevelActorPackageDependencyRebase("0x1A72D8", "0x1D6D1C", "0x2538", "0x006E", "Reuse Stone Hill resident actor 0x006E dependency root.")
            ]),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T80OnlySourceNativeLinkedContainedGemNoExeHelper.v109",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T80OnlySourceNativeLinkedContainedGemNoExeHelper",
            Status: "experimental-plan-only",
            Risk: "Focused follow-up after the raw Peace Keepers 0x000D contained-helper rows crashed Stone Hill. This keeps a single native T80 Spring Chest import, skips custom EXE reward scripting, and pairs it with one Stone Hill-style contained gem whose special data is explicitly linked to the appended Spring Chest row. It is disposable-candidate only until in-game testing proves it boots and avoids Sparx/collection crashes.",
            Description: "Peace Keepers T80 native Spring Chest single-row import into Stone Hill, paired with one same-level contained-gem link row instead of raw copied helper rows.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T80OnlySourceNativeContainedHelperRowsNoExeHelper.v108",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T80OnlySourceNativeContainedHelperRowsNoExeHelper",
            Status: "experimental-plan-only",
            Risk: "Focused follow-up after diagnostics showed the custom helper creates the wrong 0x01A6/0x10 reward/effect rows and drives Sparx underground. This keeps the single-row native T80 Spring Chest import, avoids all custom EXE helper/reward scripting, and appends only the native Peace Keepers 0x000D contained-helper rows T118-T122 beside it. It is disposable-candidate only until in-game testing proves Stone Hill boots and the chest reacts safely.",
            Description: "Peace Keepers T80 native Spring Chest single-row import into Stone Hill, paired only with its nearby native 0x000D contained-helper rows and no custom helper.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T80OnlySourceNativeNoHelperNoExeHelper.v102",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T80OnlySourceNativeNoHelperNoExeHelper",
            Status: "experimental-plan-only",
            Risk: "Crash-isolation follow-up after the T80-T83 cluster still crashed Stone Hill. This imports the same Peace Keepers 0x0149 actor package/root, but appends only native source row T80 with no helper, contained, context, reward, or EXE rows. If this still crashes, the actor package/root registration is the primary suspect; if it loads, the broader T80-T83 cluster is unsafe.",
            Description: "Peace Keepers T80 native Spring Chest single-row import into Stone Hill without helper rows or scripted reward logic.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T80T83SharedClusterSourceNativeNoContainedHelperRowsNoExeHelper.v101",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T80T83SharedClusterSourceNativeNoContainedHelperRowsNoExeHelper",
            Status: "experimental-plan-only",
            Risk: "Isolation follow-up after v100 crashed while loading Stone Hill. This keeps the native Peace Keepers T80-T83 Spring Chest cluster and imported 0x0149 package/root, but removes the five live 0x000D contained-helper rows T118-T122 so the next test can prove whether those helper rows caused the load crash.",
            Description: "Peace Keepers T80-T83 native Spring Chest cluster copied into Stone Hill without contained-helper rows and without a custom EXE helper.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T80T83SharedClusterSourceNativeContainedHelperRowsNoExeHelper.v100",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T80T83SharedClusterSourceNativeNativeContainedHelperRowsNoExeHelper",
            Status: "experimental-plan-only",
            Risk: "Diagnostic follow-up after v99/v98 still crashed or failed in user testing. This avoids the scripted reward helper, avoids loose native reward rows, and instead imports the native Peace Keepers T80-T83 Spring Chest cluster with the nearby hidden 0x000D contained-gem/helper rows T118-T122. It is disposable-candidate only until in-game testing proves the cluster boots and reacts.",
            Description: "Peace Keepers T80-T83 native Spring Chest cluster copied into Stone Hill with hidden contained-gem/helper rows and no custom EXE helper.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6RollbackStableNativePreHitVisualOnlyStackHelper.v99",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6NativePreHitSafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorVisualOnlyScriptedMotionRearmNativePreHitShellLifecycleStackHelper",
            Status: "experimental-plan-only",
            Risk: "Rollback checkpoint after the v98 native-effect-only route was reported to still crash. This intentionally returns to the last user-proven load-stable branch so the next DuckStation/RAM pass can isolate the missing trigger without introducing another load crash.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill 0x00C2 controller, native pre-hit shell lifecycle words, and the dormant visual-only scratch helper from the last no-crash checkpoint.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6StaticNativePreHitSourceSafeNativeSpringEffectOnlyNoPickupNativeReadyShellStackHelper.v98",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6StaticNativePreHitSourceSafeNativeSpringEffectOnlyStackHelperNativeReadyShell",
            Status: "blocked-failed-in-game",
            Risk: "Do not use for new exports. User testing reported that v98 still crashes, so the native-effect-only no-pickup helper route is not a stable diagnostic base.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill 0x00C2 controller, static native pre-hit source bytes, and a native-effect-only helper that does not create a Sparx-targetable reward gem.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6SafeNativeSpringEffectOnlyNoPickupNativeStateZeroShellStackHelper.v97",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6SafeNativeSpringEffectOnlyStackHelperNativeStateZeroShell",
            Status: "experimental-plan-only",
            Risk: "Diagnostic follow-up after the helper reward-gem path repeatedly caused Sparx underground targeting and collection crashes. This candidate writes the native after-hit 0x0022/0x20 spring effect row instead of a loose collectible gem row, so it is expected to test hit/react stability only and may not award treasure.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill 0x00C2 controller and a native-effect-only helper that does not create a Sparx-targetable reward gem.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6SafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorVisualOnlyScriptedMotionRearmNativeStateZeroShellStackHelper.v96",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6SafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorVisualOnlyScriptedMotionRearmNativeStateZeroShellStackHelper",
            Status: "experimental-plan-only",
            Risk: "Recovery checkpoint after v94/v95 loading hangs. This returns to the last known stable no-Sparx-dive branch: native state-zero shell words plus a visual-only scratch reward row. Expected result is Stone Hill loads and the chest animates, but the reward may still be missing or non-collectible.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill 0x00C2 controller, native state-zero shell words, and a visual-only dormant scratch reward row.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6StaticNativePreHitNoRuntimeForceStackHelper.v95",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6NativePreHitSafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorVisualOnlyScriptedMotionRearmNativePreHitShellLifecycleStackHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after v94: forcing native pre-hit lifecycle words every frame caused Stone Hill to hang on the flying/loading screen. This keeps the static native pre-hit source row edits and known load-stable helper path, but removes the runtime pre-hit force so loading stability can be re-established before another trigger/linkage change.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill 0x00C2 controller and static native pre-hit shell source bytes, without rewriting pre-hit lifecycle bytes during level load.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6ForcedNativePreHitLifecycleStackHelper.v94",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6NativePreHitSafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorVisualOnlyScriptedMotionRearmNativePreHitShellLifecycleStackHelper",
            Status: "blocked-failed-in-game",
            Risk: "Do not use for new exports. v94 caused Stone Hill to hang on the flying/loading screen at 0 FPS after forcing the visible shell back to native pre-hit lifecycle bytes during level load.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill 0x00C2 controller and a stack-safe helper that keeps the shell in native pre-hit state until contact.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70NativeD8DataLocalC2CompanionOver000ERuntime01A6NativePreHitShellLifecycleNoExeHelper.v90",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70NativeD8DataLocalC2CompanionRuntime01A6NativePreHitShellLifecycleNoExeHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after v89: appending the native Peace Keepers 0x00D8 companion row was safe but inert because Stone Hill does not register actor 0x00D8. A full 0x00D8 package import is too large for the known safe gaps, so this candidate keeps Stone Hill's native 0x00C2 chest/controller actor running while using Peace Keepers T113 companion special-data and native lifecycle words beside the imported T70 shell.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with Peace Keepers T113 companion data running through Stone Hill's local 0x00C2 actor, with no EXE helper.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70NativeD8CompanionOver000ERuntime01A6NativePreHitShellLifecycleNoExeHelper.v89",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70NativeD8CompanionRuntime01A6NativePreHitShellLifecycleNoExeHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after v88: Stone Hill loaded with one stable imported Spring Chest and Sparx stayed normal, but flame/charge only changed the top gem color and did not trigger the native spring-pop cycle. Native Peace Keepers RAM shows a nearby 0x00D8 companion row T113 sharing the same 0x01A6 runtime variant as T70, so this candidate appends that companion row beside the visible T70 shell while keeping the known-safe 0x0149 package/root route and no custom helper.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with native Peace Keepers 0x00D8 companion row T113 and no EXE helper.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6NativePreHitSafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorVisualOnlyScriptedMotionRearmNativePreHitShellLifecycleStackHelper.v88",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6NativePreHitSafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorVisualOnlyScriptedMotionRearmNativePreHitShellLifecycleStackHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after v87: live capture proved the shell was being forced into the already-popped/native-ready flag group before the first hit, so the chest had no clean reward cycle. v88 restores the exact Peace Keepers pre-hit shell flag group before interaction and the exact native popped shell flag group after interaction.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill 0x00C2 controller, native pre-hit source bytes, dormant visual-only scratch reward row, and stack-safe helper that rewrites the full native pre-hit/popped shell lifecycle flag group.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6NativePreHitSafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorVisualOnlyScriptedMotionRearmNativeReadyShellStackHelper.v87",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6NativePreHitSafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorVisualOnlyScriptedMotionRearmNativeReadyShellStackHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after v86: any visible pickup identity still re-entered the unsafe Sparx/collect path. v87 stops faking a collectible reward and instead corrects the shell state machine to match native Peace Keepers: pre-hit 0x55FF0020 while waiting and after-hit 0x55100020 when triggered.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill 0x00C2 controller, native pre-hit source bytes, dormant visual-only scratch reward row, and stack-safe helper that uses native-ready/native-after-hit shell words without restoring normal pickup identity.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6SafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorTypeOnlyScriptedMotionRearmNativeStateZeroShellStackHelper.v86",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6SafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorTypeOnlyScriptedMotionRearmNativeStateZeroShellStackHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after v85: native state-zero shell words stopped Sparx from chasing the hidden reward, but visual-only reward bytes did not render a popped gem. v86 restores the donor type/model word from v84 while keeping the native state-zero shell words to isolate whether the crash was caused by shell state or reward identity.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill 0x00C2 controller, dormant donor-scaffold scratch reward row, and stack-safe helper that restores donor visual bytes plus the type/model word while popping, leaves the final pickup tail word blank, and writes native state-zero shell words.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6SafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorVisualOnlyScriptedMotionRearmNativeStateZeroShellStackHelper.v85",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6SafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorVisualOnlyScriptedMotionRearmNativeStateZeroShellStackHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after v84: restoring the donor type/model word made a visible blue gem, but Sparx targeted the underground reward and collecting the gem crashed. v85 returns to the safer visual-only reward row and changes the helper's shell words to native state-zero bytes observed on Peace Keepers spring chests.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill 0x00C2 controller, dormant donor-scaffold scratch reward row, and stack-safe helper that restores donor visual bytes only while popping, leaves pickup/type words blank, and writes native state-zero shell words.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6SafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorTypeOnlyScriptedMotionRearmStackHelper.v84",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6SafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorTypeOnlyScriptedMotionRearmStackHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after v83: leaving both pickup/type words blank stopped Sparx and the invisible collectible but also prevented the gem from rendering. v84 restores the donor type/model word while leaving the final pickup tail word blank.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill 0x00C2 controller, dormant donor-scaffold scratch reward row, and stack-safe helper that restores donor visual bytes plus the type/model word while popping but leaves the final pickup tail word blank.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6SafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorVisualOnlyScriptedMotionRearmStackHelper.v83",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6SafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorVisualOnlyScriptedMotionRearmStackHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after v82: masking the reward value turned the gem black and still crashed on touch. v83 preserves the donor color/visual bytes but leaves the pickup/type words blank during the pop to isolate whether those words trigger the unsafe collect path.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill 0x00C2 controller, dormant donor-scaffold scratch reward row, and stack-safe helper that restores donor visual bytes only while popping but leaves pickup/type words blank.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6SafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorShapeNoValueScriptedMotionRearmStackHelper.v82",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6SafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorShapeNoValueScriptedMotionRearmStackHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after v81: v81 proved the donor gem shape can render, but Sparx still chased an invisible target and touching the visible gem crashed the game. v82 masks the airborne reward value so the gem can render without entering the unsafe collect path.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill 0x00C2 controller, dormant donor-scaffold scratch reward row, and stack-safe helper that restores the donor gem shape only while popping but masks the airborne reward value for crash isolation.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6SafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorShapeScriptedMotionRearmStackHelper.v81",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6SafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorShapeScriptedMotionRearmStackHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after v80: v80 kept the reward row's parked visual scaffold alive and Sparx still chased an invisible underground target. v81 keeps the scratch row dormant while parked, then restores the donor standalone-gem shape only during the scripted pop.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill 0x00C2 controller, dormant donor-scaffold scratch reward row, and stack-safe helper that restores the donor gem shape only while popping before blanking the parked row again.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6SafeVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankScriptedMotionRearmStackHelper.v80",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6SafeVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankScriptedMotionRearmStackHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after v79: v79 fixed the Sparx underground target and repeat rearming, but the fully blank reward row no longer rendered a popped gem. v80 preserves the donor gem visual scaffold while blanking the parked collectible identity/value/type bytes, then restores the visible blue reward only during the pop.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill 0x00C2 controller, donor-scaffold scratch reward row, and stack-safe helper that scripts spring-gem motion while blanking the parked reward identity.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6SafeBlankScratchVisibleGemHangReturnArcPostReturnFullBlankScriptedMotionRearmStackHelper.v79",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6SafeBlankScratchVisibleGemHangReturnArcPostReturnFullBlankScriptedMotionRearmStackHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after v78: v78 still left a parked reward target that sent Sparx underground, and delaying the reward value made the first popped gem black. v79 uses a blank scratch reward row while parked, rebuilds the visible blue reward only during the pop, and avoids delaying the value byte so the gem should not turn black.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill 0x00C2 controller, blank scratch reward row, and stack-safe helper that scripts spring-gem motion while blanking the reward row again after it returns.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6SafePreinitializedVisibleGemHangReturnArcPostReturnFullQuarantineScriptedMotionRearmStackHelper.v78",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6SafePreinitializedVisibleGemHangReturnArcPostReturnFullQuarantineScriptedMotionRearmStackHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after v77: the first pop produced a gem, but Sparx still targeted the parked reward underground and the second hit animated the chest without restoring a visible reward. v78 explicitly blanks the collectible value while airborne and scripts the reward motion without depending on the active pickup list.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill 0x00C2 controller, preinitialized visible blue reward row, and stack-safe helper that scripts spring-gem motion outside the normal pickup-list path.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6SafePreinitializedVisibleGemHangReturnArcPostReturnFullQuarantineDelayedIdentityRearmStackHelper.v77",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6SafePreinitializedVisibleGemHangReturnArcPostReturnFullQuarantineDelayedIdentityRearmStackHelper",
            Status: "experimental-plan-only",
            Risk: "Pivot back to the v68 helper branch after v74-v76 proved the no-helper native T70/T71 pair can draw but cannot react in Stone Hill. v77 keeps v68's best first-hit pop/return behavior, but delays restoring both the parked reward row's visible type and collectible value until the next pop has risen, avoiding v69's early Sparx target.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill 0x00C2 controller, preinitialized visible blue reward row, and stack-safe helper with delayed type/value rearm after the reward rises.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70T71PairSharedNativeSpecialClusterSourceNativeOver000EExtendedClusterNoRebaseNativeRewardOnlyRowsNoExeHelper.v76",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70T71PairSharedClusterNativeSpecialClusterSourceNativeNativeRewardOnlyRowsNoExeHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after v75 crashed with only native context rows T72-T74. v76 appends only native reward rows T92-T94 against the same shared spring-data cluster, skipping context rows, to prove whether reward rows are safe and whether they influence behavior without the context rows.",
            Description: "Peace Keepers T70/T71 native Spring Chest pair plus T92-T94 reward rows copied into Stone Hill while preserving their offsets inside one shared native spring-data cluster.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving Stone Hill's native 0x00C2 chest package.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70T71PairSharedNativeSpecialClusterSourceNativeOver000EExtendedClusterNoRebaseNativeContextOnlyRowsNoExeHelper.v75",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70T71PairSharedClusterNativeSpecialClusterSourceNativeNativeContextOnlyRowsNoExeHelper",
            Status: "in-game-blocked-bad-level-state",
            Risk: "Blocked after user testing showed Spyro loading into an invalid empty/ocean level state. v75 appends only the native Peace Keepers T72-T74 context rows against the same shared spring-data cluster, so those live context rows remain unsafe in Stone Hill.",
            Description: "Peace Keepers T70/T71 native Spring Chest pair plus T72-T74 context rows copied into Stone Hill while preserving their offsets inside one shared native spring-data cluster.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving Stone Hill's native 0x00C2 chest package.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70T71PairSharedNativeSpecialClusterSourceNativeOver000EExtendedClusterNoRebaseNoLiveNativeRowsNoExeHelper.v74",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70T71PairSharedClusterNativeSpecialClusterSourceNativeNoLiveNativeRowsNoExeHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after v73 booted Stone Hill into an invalid empty/ocean state. v74 keeps the Peace Keepers T70/T71 actor pair and copied shared native spring-data cluster, but does not append the native T72-T74/T92-T94 support rows as live Stone Hill mobys. Use this to prove whether the startup corruption is caused by live support-row appends versus the actor package and shared spring cluster itself.",
            Description: "Peace Keepers T70/T71 native Spring Chest pair copied into Stone Hill with one shared native spring-data cluster, but without live native context/reward rows.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving Stone Hill's native 0x00C2 chest package.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70T71PairSharedNativeSpecialClusterSourceNativeOver000EExtendedClusterNoRebaseNativeContextRowsNoExeHelper.v73",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70T71PairSharedClusterNativeSpecialClusterSourceNativeNativeContextRowsNoExeHelper",
            Status: "in-game-blocked-bad-level-state",
            Risk: "Blocked after user testing showed Stone Hill loading into an invalid empty/ocean state with Spyro flying over nothing. This preserves the native Peace Keepers special-data cluster-relative offsets for T70/T71, T72-T74, and T92-T94, but the live support-row append route is unsafe.",
            Description: "Peace Keepers T70/T71 native Spring Chest pair plus context/reward rows copied into Stone Hill while preserving their original offsets inside one shared native spring-data cluster.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving Stone Hill's native 0x00C2 chest package.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70T71PairSharedClusterSourceNativeOver000EExtendedClusterNoRebaseNativeContextRowsNoExeHelper.v72",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70T71PairSharedClusterSourceNativeNativeContextRowsNoExeHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after the interaction diagnostic showed a real Peace Keepers Spring Chest hit mutating T72-T74 and T92-T94 together. Earlier no-helper native candidates only copied the reward rows and stayed inert; this candidate copies the native context and reward rows while preserving Stone Hill's native 0x00C2 package and avoiding the custom EXE helper. Keep it disposable until in-game testing proves the chest reacts, springs, resets, and does not disturb nearby objects.",
            Description: "Peace Keepers T70/T71 native Spring Chest pair plus shared spring-data cluster and native context/reward rows copied into Stone Hill without the custom visual helper.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving Stone Hill's native 0x00C2 chest package.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6BluePreHitSafeNativeSpringEffectOnlyNoPickupBlueGemNativeSpringEffectVisualBlueGemNativeSpringEffectOrdinalManualSpyroDelayedJumpTouchCollectBlankVisualAwardPlainTreasureWordsManualRewardPopArcNativeEffectVisualNativeStateZeroShellStackHelper.v136",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6BluePreHitSafeNativeSpringEffectOnlyStackHelperBlueGemNativeSpringEffectVisualBlueGemNativeSpringEffectOrdinalManualSpyroTouchCollectNativeEffectVisualManualSpyroDelayedJumpTouchCollectBlankVisualAwardFixedTreasureWordsManualRewardPopArcNativeEffectVisualNativeStateZeroShell",
            Status: "experimental-plan-only",
            Risk: "Follow-up after v135 proved the stable lifecycle and plain +5 treasure award. v136 keeps that reward path unchanged and only scripts the visible blue gem through a short rise, hang, and drop arc so it no longer appears as a static floating gem.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill 0x00C2 controller, blue/top-safe pre-hit Spring Chest bytes, and a native-effect-only helper that pops the blue gem through a scripted arc, awards plain +5 on touch, then blanks the visual.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6BluePreHitSafeNativeSpringEffectOnlyNoPickupBlueGemNativeSpringEffectVisualBlueGemNativeSpringEffectOrdinalManualSpyroDelayedJumpTouchCollectBlankVisualAwardPlainTreasureWordsNativeEffectVisualNativeStateZeroShellStackHelper.v135",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6BluePreHitSafeNativeSpringEffectOnlyStackHelperBlueGemNativeSpringEffectVisualBlueGemNativeSpringEffectOrdinalManualSpyroTouchCollectNativeEffectVisualManualSpyroDelayedJumpTouchCollectBlankVisualAwardFixedTreasureWordsNativeEffectVisualNativeStateZeroShell",
            Status: "experimental-plan-only",
            Risk: "Follow-up after v134: writing +0x00050000 avoided the crash but made the HUD award a runaway huge value. v135 keeps the same stable visual cleanup and global/per-level write path, but writes plain +5 treasure instead.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill 0x00C2 controller, blue/top-safe pre-hit Spring Chest bytes, and a native-effect-only helper that delays the manual finish check, writes plain +5 treasure totals, then blanks the blue visual.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6BluePreHitSafeNativeSpringEffectOnlyNoPickupBlueGemNativeSpringEffectVisualBlueGemNativeSpringEffectOrdinalManualSpyroDelayedJumpTouchCollectBlankVisualAwardFixedTreasureWordsNativeEffectVisualNativeStateZeroShellStackHelper.v134",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6BluePreHitSafeNativeSpringEffectOnlyStackHelperBlueGemNativeSpringEffectVisualBlueGemNativeSpringEffectOrdinalManualSpyroTouchCollectNativeEffectVisualManualSpyroDelayedJumpTouchCollectBlankVisualAwardFixedTreasureWordsNativeEffectVisualNativeStateZeroShell",
            Status: "experimental-plan-only",
            Risk: "Follow-up after v133: calling the native treasure-award routine reached the reward path but crashed when Spyro touched the popped visual. RAM captures show real blue spring chests add treasure as +0x00050000 to the global and per-level treasure words, so v134 restores the stable visual cleanup and writes those fixed-format treasure words directly.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill 0x00C2 controller, blue/top-safe pre-hit Spring Chest bytes, and a native-effect-only helper that delays the manual finish check, writes fixed-format blue-gem treasure totals, then blanks the blue visual.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6BluePreHitSafeNativeSpringEffectOnlyNoPickupBlueGemNativeSpringEffectVisualBlueGemNativeSpringEffectOrdinalManualSpyroDelayedJumpTouchCollectBlankVisualAwardNativeTreasureRoutineNativeEffectVisualNativeStateZeroShellStackHelper.v133",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6BluePreHitSafeNativeSpringEffectOnlyStackHelperBlueGemNativeSpringEffectVisualBlueGemNativeSpringEffectOrdinalManualSpyroTouchCollectNativeEffectVisualManualSpyroDelayedJumpTouchCollectBlankVisualAwardNativeTreasureRoutineNativeEffectVisualNativeStateZeroShell",
            Status: "experimental-plan-only",
            Risk: "Follow-up after v132: holding the public treasure counters did not visibly award treasure. RAM captures show the game stores treasure in a fixed counter word and has a native award routine at 0x800420D4 that also marks collection flags. v133 keeps the stable v129-style visual cleanup but calls that native routine with a blue-gem value and the spawned visual row when Spyro touches the reward.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill 0x00C2 controller, blue/top-safe pre-hit Spring Chest bytes, and a native-effect-only helper that delays the manual finish check, calls the game treasure-award routine for a blue gem, then blanks the blue visual.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6BluePreHitSafeNativeSpringEffectOnlyNoPickupBlueGemNativeSpringEffectVisualBlueGemNativeSpringEffectOrdinalManualSpyroDelayedJumpTouchCollectBlankVisualHoldDualTreasureCountersNativeEffectVisualNativeStateZeroShellStackHelper.v132",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6BluePreHitSafeNativeSpringEffectOnlyStackHelperBlueGemNativeSpringEffectVisualBlueGemNativeSpringEffectOrdinalManualSpyroTouchCollectNativeEffectVisualManualSpyroDelayedJumpTouchCollectBlankVisualHoldDualTreasureCountersNativeEffectVisualNativeStateZeroShell",
            Status: "experimental-plan-only",
            Risk: "Follow-up after v131: writing both known treasure counters once did not visibly award treasure. v132 stores the +5 target values when the manual finish path runs, then keeps writing them while the helper is in its finished state to test whether the normal game loop is overwriting the one-time write.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill 0x00C2 controller, blue/top-safe pre-hit Spring Chest bytes, and a native-effect-only helper that delays the manual finish check, blanks the blue visual, and holds two treasure counters at +5 after Spyro touches it.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6BluePreHitSafeNativeSpringEffectOnlyNoPickupBlueGemNativeSpringEffectVisualBlueGemNativeSpringEffectOrdinalManualSpyroDelayedJumpTouchCollectBlankVisualAwardDualTreasureCountersNativeEffectVisualNativeStateZeroShellStackHelper.v131",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6BluePreHitSafeNativeSpringEffectOnlyStackHelperBlueGemNativeSpringEffectVisualBlueGemNativeSpringEffectOrdinalManualSpyroTouchCollectNativeEffectVisualManualSpyroDelayedJumpTouchCollectBlankVisualAwardDualTreasureCountersNativeEffectVisualNativeStateZeroShell",
            Status: "experimental-plan-only",
            Risk: "Follow-up after v130: the stable pop/touch cleanup remained, but writing only _globalGemCount did not visibly award treasure. v131 writes +5 to both _globalGemCount and the total-treasure address used by public cheat tables; if this still does not move the HUD, the missing path is probably collection-state flags or a display cache.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill 0x00C2 controller, blue/top-safe pre-hit Spring Chest bytes, and a native-effect-only helper that delays the manual finish check, blanks the blue visual, and awards five treasure through two known treasure counters when Spyro touches it.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6BluePreHitSafeNativeSpringEffectOnlyNoPickupBlueGemNativeSpringEffectVisualBlueGemNativeSpringEffectOrdinalManualSpyroDelayedJumpTouchCollectBlankVisualAwardTotalJewelsNativeEffectVisualNativeStateZeroShellStackHelper.v130",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6BluePreHitSafeNativeSpringEffectOnlyStackHelperBlueGemNativeSpringEffectVisualBlueGemNativeSpringEffectOrdinalManualSpyroTouchCollectNativeEffectVisualManualSpyroDelayedJumpTouchCollectBlankVisualAwardTotalJewelsNativeEffectVisualNativeStateZeroShell",
            Status: "experimental-plan-only",
            Risk: "Follow-up after v129: pop, no-Sparx touch, and visual cleanup are stable, but the reward remains visual-only. v130 adds a narrow +5 total-jewels write when the manual finish path runs; verify HUD/count behavior before promoting it.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill 0x00C2 controller, blue/top-safe pre-hit Spring Chest bytes, and a native-effect-only helper that delays the manual finish check, blanks the blue visual, and awards five total jewels when Spyro touches it.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6BluePreHitSafeNativeSpringEffectOnlyNoPickupBlueGemNativeSpringEffectVisualBlueGemNativeSpringEffectOrdinalManualSpyroDelayedJumpTouchCollectBlankVisualNativeEffectVisualNativeStateZeroShellStackHelper.v129",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6BluePreHitSafeNativeSpringEffectOnlyStackHelperBlueGemNativeSpringEffectVisualBlueGemNativeSpringEffectOrdinalManualSpyroTouchCollectNativeEffectVisualManualSpyroDelayedJumpTouchCollectBlankVisualNativeEffectVisualNativeStateZeroShell",
            Status: "experimental-plan-only",
            Risk: "Follow-up after v128: touching the blue visual finished and hid the chest, but left the blue visual floating forever. v129 keeps the delayed no-Sparx touch finish and also blanks the spawned visual row during cleanup.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill 0x00C2 controller, blue/top-safe pre-hit Spring Chest bytes, and a native-effect-only helper that delays the manual finish check and blanks the blue visual when the chest finishes.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6BluePreHitSafeNativeSpringEffectOnlyNoPickupBlueGemNativeSpringEffectVisualBlueGemNativeSpringEffectOrdinalManualSpyroDelayedJumpTouchCollectNativeEffectVisualNativeStateZeroShellStackHelper.v128",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6BluePreHitSafeNativeSpringEffectOnlyStackHelperBlueGemNativeSpringEffectVisualBlueGemNativeSpringEffectOrdinalManualSpyroTouchCollectNativeEffectVisualManualSpyroDelayedJumpTouchCollectNativeEffectVisualNativeStateZeroShell",
            Status: "experimental-plan-only",
            Risk: "Follow-up after v127: the chest still finished immediately, proving the first height check can be true at pop time. v128 adds a short arming delay and requires Spyro to rise above the chest before the manual finish path can run.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill 0x00C2 controller, blue/top-safe pre-hit Spring Chest bytes, and a native-effect-only helper that keeps the blue no-pickup visual while delaying and height-gating the manual finish check.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6BluePreHitSafeNativeSpringEffectOnlyNoPickupBlueGemNativeSpringEffectVisualBlueGemNativeSpringEffectOrdinalManualSpyroZTouchCollectNativeEffectVisualNativeStateZeroShellStackHelper.v127",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6BluePreHitSafeNativeSpringEffectOnlyStackHelperBlueGemNativeSpringEffectVisualBlueGemNativeSpringEffectOrdinalManualSpyroTouchCollectNativeEffectVisualManualSpyroZTouchCollectNativeEffectVisualNativeStateZeroShell",
            Status: "experimental-plan-only",
            Risk: "Follow-up after v126: the chest disappeared immediately because the manual finish check used Spyro's ground X/Y distance from the chest. v127 keeps the stable no-pickup blue visual path, but only finishes when Spyro reaches the popped visual's height, so flaming beside the chest should no longer consume it early.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill 0x00C2 controller, blue/top-safe pre-hit Spring Chest bytes, and a native-effect-only helper that uses a blue-gem visual plus blue ordinal and height-gates the manual finish check without creating a Sparx-targetable pickup gem.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6BluePreHitSafeNativeSpringEffectOnlyNoPickupBlueGemNativeSpringEffectVisualBlueGemNativeSpringEffectOrdinalManualSpyroTouchCollectNativeEffectVisualNativeStateZeroShellStackHelper.v126",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6BluePreHitSafeNativeSpringEffectOnlyStackHelperBlueGemNativeSpringEffectVisualBlueGemNativeSpringEffectOrdinalManualSpyroTouchCollectNativeEffectVisualNativeStateZeroShell",
            Status: "experimental-plan-only",
            Risk: "Follow-up after v125: the no-pickup blue visual stayed stable, blue, crash-free, and Sparx ignored it, but it remained visual-only. v126 adds a manual Spyro-overlap finish check while keeping the visual out of the Sparx pickup list; this should test cleanup/disappear behavior before any treasure counter write is attempted.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill 0x00C2 controller, blue/top-safe pre-hit Spring Chest bytes, and a native-effect-only helper that uses a blue-gem visual plus blue ordinal and manually finishes the chest when Spyro touches it, without creating a Sparx-targetable pickup gem.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6BluePreHitSafeNativeSpringEffectOnlyNoPickupBlueGemNativeSpringEffectVisualBlueGemNativeSpringEffectOrdinalNativeStateZeroShellStackHelper.v125",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6BluePreHitSafeNativeSpringEffectOnlyStackHelperBlueGemNativeSpringEffectVisualBlueGemNativeSpringEffectOrdinalNativeStateZeroShell",
            Status: "experimental-plan-only",
            Risk: "Follow-up after v124: the no-pickup blue-gem visual stayed stable and kept Sparx idle, but rendered black. v125 keeps the same non-pickup scratch-row route and adds only the blue gem ordinal/value byte while the visual is airborne.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill 0x00C2 controller, blue/top-safe pre-hit Spring Chest bytes, and a native-effect-only helper that tries a blue-gem visual plus blue ordinal without creating a Sparx-targetable pickup gem.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6BluePreHitSafeNativeSpringEffectOnlyNoPickupBlueGemNativeSpringEffectVisualNativeStateZeroShellStackHelper.v124",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6BluePreHitSafeNativeSpringEffectOnlyStackHelperBlueGemNativeSpringEffectVisualNativeStateZeroShell",
            Status: "experimental-plan-only",
            Risk: "Follow-up after v123: the no-pickup native-effect route stopped Sparx and avoided crashes, but rendered the pop as an egg. v124 keeps the same non-pickup scratch-row route and changes only the temporary effect-row visual id from 0x0022 to blue gem id 0x0055.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill 0x00C2 controller, blue/top-safe pre-hit Spring Chest bytes, and a native-effect-only helper that tries a blue-gem visual without creating a Sparx-targetable pickup gem.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6BluePreHitSafeNativeSpringEffectOnlyNoPickupNativeStateZeroShellStackHelper.v123",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6BluePreHitSafeNativeSpringEffectOnlyStackHelperNativeStateZeroShell",
            Status: "experimental-plan-only",
            Risk: "Follow-up after v122: restoring the donor type/model word made the gem visible, but Sparx still targeted it and Spyro collection crashed. v123 stops using a collectible gem row for the pop and instead writes the native after-hit 0x0022/0x20 spring-effect row with the newer blue pre-hit shell route.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill 0x00C2 controller, blue/top-safe pre-hit Spring Chest bytes, and a native-effect-only helper that does not create a Sparx-targetable reward gem.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6BluePreHitSafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorTypeOnlyScriptedMotionRearmStackHelper.v122",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6BluePreHitSafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorTypeOnlyScriptedMotionRearmStackHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after v121: leaving pickup/type words blank stopped Sparx but also prevented a collectible gem visual. v122 restores the donor type/model word while leaving the final pickup tail word blank, testing whether the visual can draw without entering the Sparx collection path.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill 0x00C2 controller, blue/top-safe pre-hit Spring Chest bytes, dormant donor-scaffold scratch reward row, and stack-safe helper that restores donor visual bytes plus the type/model word while popping but leaves the final pickup tail word blank.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6BluePreHitSafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorVisualOnlyScriptedMotionRearmStackHelper.v121",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6BluePreHitSafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorVisualOnlyScriptedMotionRearmStackHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after v120: scripting motion without value delay still let Sparx target and collect the reward, proving any normal pickup/type identity is unsafe. v121 keeps the newer blue pre-hit shell but restores only donor visual bytes during the pop while leaving pickup/type words blank so Sparx should not interact with the spring-chest gem.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill 0x00C2 controller, blue/top-safe pre-hit Spring Chest bytes, dormant donor-scaffold scratch reward row, and stack-safe helper that restores donor visual bytes only while popping but leaves pickup/type words blank.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6BluePreHitSafePreinitializedVisibleGemHangReturnArcPostReturnFullQuarantineScriptedMotionRearmNoValueDelayStackHelper.v120",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6BluePreHitSafePreinitializedVisibleGemHangReturnArcPostReturnFullQuarantineScriptedMotionRearmNoValueDelayStackHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after v119: restoring visible reward identity made the gem pop, but Sparx immediately treated it as a loose gem target, then collected it on the second hit. v120 keeps the blue pre-hit shell and full parked quarantine, but scripts the reward motion outside the pickup-list path and does not use the delayed-value branch that hid/blackened the gem.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill 0x00C2 controller, blue/top-safe pre-hit Spring Chest bytes, preinitialized visible blue reward row, and stack-safe helper that animates the reward outside the normal pickup-list route while fully quarantining the returned row.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6BluePreHitSafePreinitializedVisibleGemHangReturnArcPostReturnFullQuarantineRearmStackHelper.v119",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6BluePreHitSafePreinitializedVisibleGemHangReturnArcPostReturnFullQuarantineRearmStackHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after v118: Sparx stayed stationary, but delaying both reward type and value hid the popped gem completely. v119 restores the visible reward identity immediately during the hit, then fully clears and quarantines the returned reward row while parked.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill 0x00C2 controller, blue/top-safe pre-hit Spring Chest bytes, preinitialized visible blue reward row, and stack-safe helper with hang-return motion, post-return active-pickup unlink sweep, full inert returned-gem quarantine, and repeat-hit rearm.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6BluePreHitSafePreinitializedVisibleGemHangReturnArcPostReturnFullQuarantineDelayedIdentityRearmStackHelper.v118",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6BluePreHitSafePreinitializedVisibleGemHangReturnArcPostReturnFullQuarantineDelayedIdentityRearmStackHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after v117: the first hit produced a black-looking reward, Sparx still chased the parked reward underground, and a second hit let Sparx collect it as a blue gem. v118 keeps the v117 blue pre-hit shell route but delays restoring both the reward type and value until the next pop has actually risen.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill 0x00C2 controller, blue/top-safe pre-hit Spring Chest bytes, preinitialized visible blue reward row, and stack-safe helper with hang-return motion, post-return active-pickup unlink sweep, full inert returned-gem quarantine, delayed identity rearm, and repeat-hit support.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6BluePreHitSafePreinitializedVisibleGemHangReturnArcPostReturnFullQuarantineDelayedCollectRearmStackHelper.v71",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6BluePreHitSafePreinitializedVisibleGemHangReturnArcPostReturnFullQuarantineDelayedCollectRearmStackHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after v70 regressed to a green-top chest and reopened the Sparx underground collection path. This keeps the blue/top-safe reward identity from v68 but removes the forced pre-armed activation/collision bytes that live RAM proved were making the shell start in the native after-hit state.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill 0x00C2 controller, blue/top-safe pre-hit Spring Chest bytes, preinitialized visible blue reward row, and stack-safe helper with hang-return motion, post-return active-pickup unlink sweep, full inert returned-gem quarantine, delayed collectability, and repeat-hit rearm.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6NativePreHitSafePreinitializedVisibleGemHangReturnArcPostReturnFullQuarantineDelayedCollectRearmStackHelper.v70",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6NativePreHitSafePreinitializedVisibleGemHangReturnArcPostReturnFullQuarantineDelayedCollectRearmStackHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after the live RAM capture: v68/v69 started the imported shell in the native after-hit state before the player touched it. This candidate preserves the native pre-hit source bytes so the first flame/charge can perform the same activation transition seen in Peace Keepers, while keeping the existing stable actor-package route and guarded helper.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill 0x00C2 controller, native pre-hit Spring Chest bytes, preinitialized visible blue reward row, and stack-safe helper with hang-return motion, post-return active-pickup unlink sweep, full inert returned-gem quarantine, delayed collectability, and repeat-hit rearm.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6SafePreinitializedVisibleGemHangReturnArcPostReturnFullQuarantineDelayedCollectRearmTypeOnHitStackHelper.v69",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6SafePreinitializedVisibleGemHangReturnArcPostReturnFullQuarantineDelayedCollectRearmTypeOnHitStackHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after v68: first hit showed and returned the gem, but the second hit behaved like a pop without showing the gem. This restores the visible gem type on every hit while keeping the collectible value delayed until the gem has risen, so repeat hits should show the reward without letting Sparx or standing Spyro collect the parked row.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill 0x00C2 controller, preinitialized visible blue reward row, and stack-safe helper with hang-return motion, post-return active-pickup unlink sweep, full inert returned-gem quarantine, delayed collectability, visible type restore on hit, and repeat-hit rearm.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6SafePreinitializedVisibleGemHangReturnArcPostReturnFullQuarantineDelayedCollectRearmStackHelper.v68",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6SafePreinitializedVisibleGemHangReturnArcPostReturnFullQuarantineDelayedCollectRearmStackHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after v67 stopped Sparx from collecting but still let Spyro auto-collect on the repeat hit. This keeps the full returned-gem quarantine, restores the visible source on hit, and delays reward value/type restoration until the gem has risen so standing at the chest should not auto-collect it.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill 0x00C2 controller, preinitialized visible blue reward row, and stack-safe helper with hang-return motion, post-return active-pickup unlink sweep, full inert returned-gem quarantine, delayed collectability, and repeat-hit rearm.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6SafePreinitializedVisibleGemHangReturnArcPostReturnFullQuarantineRearmStackHelper.v67",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6SafePreinitializedVisibleGemHangReturnArcPostReturnFullQuarantineRearmStackHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after v66 behaved the same as v65, meaning clearing only the returned reward source-id byte was not enough. This fully clears the returned reward identity/value/type while parked, keeps sweeping stale pickup-list entries, and restores the full reward identity only when the chest is hit again.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill 0x00C2 controller, preinitialized visible blue reward row, and stack-safe helper with hang-return motion, post-return active-pickup unlink sweep, full inert returned-gem quarantine, and full reward identity restore on the next hit.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6SafePreinitializedVisibleGemHangReturnArcPostReturnQuarantineRearmStackHelper.v66",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6SafePreinitializedVisibleGemHangReturnArcPostReturnQuarantineRearmStackHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after v65 still allowed Sparx to dive and collect the gem on the second hit because the returned reward identity was preserved too early. This parks the returned reward as inert, keeps sweeping stale pickup-list entries, and restores the reward identity only when the chest is hit again.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill 0x00C2 controller, preinitialized visible blue reward row, and stack-safe helper with hang-return motion, post-return active-pickup unlink sweep, inert returned-gem quarantine, and reward identity restore on the next hit.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6SafePreinitializedVisibleGemHangReturnArcPostReturnUnlinkRearmStackHelper.v65",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6SafePreinitializedVisibleGemHangReturnArcPostReturnUnlinkRearmStackHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after v64 stopped Sparx from collecting the hidden returned gem but left the chest in a post-return watch state that could not be hit again. This keeps the active-pickup unlink sweep but preserves the returned reward identity and lets a new hit re-enter the pop cycle.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill 0x00C2 controller, preinitialized visible blue reward row, and stack-safe helper with hang-return motion, post-return active-pickup unlink sweep, and re-arm on the next hit after return.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6SafePreinitializedVisibleGemHangReturnArcPostReturnUnlinkStackHelper.v64",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6SafePreinitializedVisibleGemHangReturnArcPostReturnUnlinkStackHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after v61 still let Sparx collect the returned reward, while v62/v63 regressed boot/terrain. This stays in the older boot-safe helper cave and only keeps clearing the returned reward from the active pickup list after the chest visually resets; first pass may not re-trigger cleanly after one return.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill 0x00C2 controller, preinitialized visible blue reward row, and stack-safe helper with hang-return motion, post-return active-pickup unlink sweep, and inert hidden parking after return.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6SafePreinitializedVisibleGemHangReturnArcRewardQuarantineStackHelper.v63",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6SafePreinitializedVisibleGemHangReturnArcRewardQuarantineStackHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after v62 used a larger blank-looking executable area that prevented the game from booting beyond the logos. This returns to the older boot-safe helper cave and fully clears the returned reward row's gem identity/active bytes as a one-return Sparx quarantine diagnostic.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill 0x00C2 controller, preinitialized visible blue reward row, and stack-safe helper with hang-return motion, active-pickup-list unlinking, and fully quarantined inert hidden parking after return.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6SafePreinitializedVisibleGemHangReturnArcPostReturnMonitorStackHelper.v62",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6SafePreinitializedVisibleGemHangReturnArcPostReturnMonitorStackHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after v61 still let Sparx collect the returned reward because the helper went idle after the chest visually reset. This keeps the inert hidden parking and leaves a lightweight monitor active after return so relisted pickup entries are cleared until the chest is hit again.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill 0x00C2 controller, preinitialized visible blue reward row, and stack-safe helper with hang-return motion, post-return parking monitor, persistent active-pickup-list unlinking, and inert hidden parking after return.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6SafePreinitializedVisibleGemHangReturnArcInertParkPersistentUnlinkStackHelper.v61",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6SafePreinitializedVisibleGemHangReturnArcInertParkPersistentUnlinkStackHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after v60 still let Sparx collect the returned reward. This keeps the inert hidden parking but changes the active pickup list handling from one-time unlink to a per-frame scan so relisted reward pointers are cleared before Sparx can chase them.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill 0x00C2 controller, preinitialized visible blue reward row, and stack-safe helper with hang-return motion, persistent active-pickup-list unlinking, and inert hidden parking after return.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6SafePreinitializedVisibleGemHangReturnArcInertParkStackHelper.v60",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6SafePreinitializedVisibleGemHangReturnArcInertParkStackHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after v59 proved Sparx still collects the reward after it returns into the chest. This keeps the v56/v59 visible blue hang-return motion, but parks the returned reward as an inert hidden row and restores it to a blue gem only on the next hit.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill 0x00C2 controller, preinitialized visible blue reward row, and stack-safe helper with hang-return motion, active-pickup-list unlinking, and inert hidden parking after return.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6SafePreinitializedVisibleGemUnlistedHangReturnArcStackHelper.v59",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6SafePreinitializedVisibleGemUnlistedHangReturnArcStackHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after v58 proved Sparx still collects the airborne reward. This keeps the v56 blue reward motion but unlinks the reward from the active pickup list after the game first registers it, testing whether Sparx pickup can be blocked without losing the visible gem.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill 0x00C2 controller, preinitialized visible blue reward row, and stack-safe helper with v56 hang-return motion plus active-pickup-list unlinking while airborne.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6SafePreinitializedVisibleGemEarlyPickupRearmHangReturnArcStackHelper.v58",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6SafePreinitializedVisibleGemEarlyPickupRearmHangReturnArcStackHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after v57 kept Sparx pickup and made the gem black. This restores the normal v56 blue reward visuals and instead re-arms the chest if the reward disappears before the spring-return window completes.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill 0x00C2 controller, preinitialized visible blue reward row, and stack-safe helper with v56 hang-return motion plus early-pickup rearm handling.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6SafePreinitializedVisibleGemNoSparxHangReturnArcStackHelper.v57",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6SafePreinitializedVisibleGemNoSparxHangReturnArcStackHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after v56 had the most natural motion but Sparx could still collect the airborne reward. This keeps the v56 motion and masks the reward value byte while airborne to test whether Sparx is treating it as a normal loose gem.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill 0x00C2 controller, preinitialized visible blue reward row, and stack-safe helper with v56 hang-return motion plus airborne reward-value masking.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6SafePreinitializedVisibleGemHangReturnArcStackHelper.v56",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6SafePreinitializedVisibleGemHangReturnArcStackHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after v55 felt stable and close, but returned too quickly. This keeps the v54/v55 height, adds a short top hang, and slows the visible reward descent before cleanup.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill 0x00C2 controller, preinitialized visible blue reward row, and stack-safe helper with a taller spring arc, short hang, and gentler return timing.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6SafePreinitializedVisibleGemFullReturnArcStackHelper.v55",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6SafePreinitializedVisibleGemFullReturnArcStackHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after v54 reached a natural height and stayed stable, but parked the reward before the descent finished. This keeps the v54 height and extends the return window so the visible reward can drop back into the chest before cleanup.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill 0x00C2 controller, preinitialized visible blue reward row, and stack-safe helper with a taller spring arc plus full return timing.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6SafePreinitializedVisibleGemHigherArcStackHelper.v54",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6SafePreinitializedVisibleGemHigherArcStackHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after v53 looked better and avoided auto-collection, but still needed a taller visible reward pop. This keeps the v53 early-park/reset behavior and raises only the initial reward arc.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill 0x00C2 controller, preinitialized visible blue reward row, and stack-safe helper with early-park reset plus a taller spring arc.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6SafePreinitializedVisibleGemHeightTuneArcStackHelper.v53",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6SafePreinitializedVisibleGemHeightTuneArcStackHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after v52 fixed the auto-collection and felt more natural, but the reward height was still wrong. This preserves the early-park reset and only raises the visible reward envelope for height tuning.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill 0x00C2 controller, preinitialized visible blue reward row, and stack-safe helper with early-park reset plus a higher tuned spring arc.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6SafePreinitializedVisibleGemEarlyParkArcStackHelper.v52",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6SafePreinitializedVisibleGemEarlyParkArcStackHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after v51 still auto-collected when the reward reached inside the chest. This keeps the visible pop/return idea but parks and re-arms the reward much earlier, before it drops into the chest/player pickup zone.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill 0x00C2 controller, preinitialized visible blue reward row, and stack-safe helper with an early-park spring arc to avoid auto-collection on return.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6SafePreinitializedVisibleGemSnappyArcStackHelper.v51",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6SafePreinitializedVisibleGemSnappyArcStackHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after v50 produced a slow pop-like effect but let the reward auto-collect when it dropped into the chest. This keeps the visible preinitialized reward and spring arc, but makes the pop much shorter and parks/re-arms before the gem reaches the bad pickup zone.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill 0x00C2 controller, preinitialized visible blue reward row, and stack-safe helper with a shorter snappy spring arc that avoids the v50 auto-collect return.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6SafePreinitializedVisibleGemSpringArcStackHelper.v50",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6SafePreinitializedVisibleGemSpringArcStackHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after v49 confirmed the visible gem returns into the chest and re-arms, but the return was too slow and the reward appeared instantly at the top. This keeps the working preinitialized reward/return path and tunes the visible reward into a quicker spring arc: lower initial position, short upward burst, immediate faster drop, shorter return window.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill 0x00C2 controller, preinitialized visible blue reward row, and stack-safe helper with a tuned visible spring arc.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6SafePreinitializedVisibleGemReturnLoopStackHelper.v49",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6SafePreinitializedVisibleGemReturnLoopStackHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after v48 made the reward visible and cleaned up on collection, but left the gem hovering instead of doing a spring pop/return. This keeps the preinitialized visible reward row and adds a timed return loop that lowers an uncollected gem, parks it, and re-arms the chest for another hit.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill 0x00C2 controller, preinitialized visible blue reward row, and stack-safe helper that returns uncollected rewards instead of leaving them hovering.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6SafePreinitializedVisibleGemOneShotCleanupStackHelper.v48",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6SafePreinitializedVisibleGemOneShotCleanupStackHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after v47 proved the reward is collectible but the low height caused an invisible auto-collect. This keeps the working one-shot cleanup path, but appends the reward as a normal visible blue gem at level load and moves that preinitialized gem when the chest opens, testing whether runtime gem morphing was the reason the reward stayed invisible.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill 0x00C2 controller, preinitialized visible blue reward row, and stack-safe one-shot helper that moves the existing reward gem instead of rebuilding its identity bytes.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6SafeLowVisibleGemOneShotCleanupStackHelper.v47",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6SafeLowVisibleGemOneShotCleanupStackHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after v46 kept correct collection and chest cleanup but the reward stayed invisible. This keeps the same row shape and cleanup path, but lowers the reward spawn height from 0x0558 to 0x0080 to test whether the reward model is spawning too high to see.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill 0x00C2 controller, hidden reward row, and stack-safe one-shot helper using a low visible-gem reward spawn.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6SafeLooseVisibleGemOneShotCleanupStackHelper.v46",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6SafeLooseVisibleGemOneShotCleanupStackHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after v45 kept the working pop/collect/cleanup path but the reward gem remained invisible. This keeps loose-gem flags while restoring the visible-gem type word used by the earlier collectible reward path.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill 0x00C2 controller, hidden reward row, and stack-safe one-shot helper using loose-gem flags plus visible-gem type word.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6SafeLooseGemOneShotCleanupStackHelper.v45",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6SafeLooseGemOneShotCleanupStackHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after v44 made the chest pop a single collectible gem and disappear after collection, but the reward gem was still invisible. This keeps v44's cleanup path and changes only the reward row shape to the local loose-gem form instead of the active reward-list form.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill 0x00C2 controller, hidden reward row, and stack-safe loose-gem one-shot reward helper with cleanup fallback.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6SafeVisibleOneShotCleanupStackHelper.v44",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6SafeVisibleOneShotCleanupStackHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after v43 fixed the infinite gem loop but spawned one invisible blue gem and left the flattened Spring Chest shell visible after collection. This keeps the one-shot path, clears the reward row's runtime visibility word at spawn time, and adds a fallback cleanup timer for the shell.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill 0x00C2 controller, hidden reward row, and stack-safe visible one-shot reward helper with cleanup fallback.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6SafeOneShotStackHelper.v43",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6SafeOneShotStackHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after v42 proved the hit/reward path could spawn a blue reward but made it invisible and infinite. This keeps the same local-controller plus Peace Keepers T70 shape and fuller reward bytes, but removes the reward-refresh loop so the helper can only spawn the reward row once.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill 0x00C2 controller, hidden reward row, and stack-safe one-shot reward helper.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6SafeRewardRefreshStackHelper.v42",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6SafeRewardRefreshStackHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after the first stack-pop helper export. This keeps the same local-controller plus Peace Keepers T70 shape, but ports the safer reward-refresh behavior from the legacy diagnostic helper: avoid risky controller pointer/tail writes, populate the reward row's active gem bytes more completely, and keep refreshing the reward row until the game lists it as active.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill 0x00C2 controller, hidden reward row, and stack-safe reward-refresh helper.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6StackMinimalPopHelper.v41",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6StackMinimalPopHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after the T91/T131 source-native context-row candidates stayed visually stable but still produced no gem pop. This returns to the simpler local-controller route, uses Peace Keepers T70's observed 0x01A6 runtime variant, and swaps the custom pop helper to stack-based register saving so the helper no longer uses the payload scratch cave for the caller return state.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill 0x00C2 controller, hidden reward row, and stack-safe minimal pop helper.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T91T131IsolatedSourceNativeOver000EExtendedRebasedNativeContextRowsNoExeHelper.v40",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T91T131IsolatedSourceNativeRebasedNativeContextRowsNoExeHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after the T91/T131 context-row candidate still drew inert Spring Chests. This keeps the v39 source/context/reward row set, but restores the single internal 0x0149 actor-package dependency rebase used by earlier Peace Keepers recipes to test whether hit/break behavior depends on a Stone Hill-local actor dependency pointer.",
            Description: "Peace Keepers isolated Spring Chest records T91 and T131 copied into Stone Hill with native source identity bytes, nearby context rows, native reward rows, and the one known 0x0149 actor-package dependency pointer rebased to a Stone Hill resident package.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases:
            [
                new CrossLevelActorPackageDependencyRebase("0x1C23D0", "0x1DEBDC", "0x4000", "0x00FB", "Reuse Stone Hill resident actor 0x00FB dependency root for the one internal pointer found in the Peace Keepers 0x0149 package.")
            ]),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T91T131IsolatedSourceNativeOver000EExtendedNoRebaseNativeContextRowsNoExeHelper.v39",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T91T131IsolatedSourceNativeNoRebaseNativeContextRowsNoExeHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after the isolated T91/T131 visible Spring Chest records plus native reward rows still did not pop when flamed or charged. This keeps the no-helper actor-package route, but also appends the nearby Peace Keepers source-table context rows around each isolated donor to test whether Spring Chest interaction depends on adjacent helper/control rows.",
            Description: "Peace Keepers isolated Spring Chest records T91 and T131 copied into Stone Hill with native source identity bytes, individual native special data, nearby context rows T88/T89/T90 and T128/T129/T130, native reward rows T92/T93/T94 and T132/T133/T134, no package rebase, and no EXE helper.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T91T131IsolatedSourceNativeOver000EExtendedNoRebaseNativeRewardRowsNoExeHelper.v38",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T91T131IsolatedSourceNativeNoRebaseNativeRewardRowsNoExeHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after the isolated T91/T131 visible Spring Chest records still did not pop when flamed or charged. This keeps the same no-helper actor-package route, but also appends the native reward gem rows associated with the isolated Peace Keepers donors to test whether the pop needs those source rows present.",
            Description: "Peace Keepers isolated Spring Chest records T91 and T131 copied into Stone Hill with native source identity bytes, individual native special data, native reward rows T92/T93/T94 and T132/T133/T134, no package rebase, and no EXE helper.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T91T131IsolatedSourceNativeOver000EExtendedNoRebaseNoRewardRowsNoExeHelper.v37",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T91T131IsolatedSourceNativeNoRebaseNoRewardRowsNoExeHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after the T70/T71 shared-cluster source-native route changed the visible top color but still did not pop or react. This keeps the no-helper data-only route but switches to Peace Keepers isolated native Spring Chest donors T91 and T131, avoiding the T70/T71 paired/reward-row cluster relationship.",
            Description: "Peace Keepers isolated Spring Chest records T91 and T131 copied into Stone Hill with native source identity bytes, individual native special data, no package rebase, no reward rows, and no EXE helper.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70T71PairSharedClusterSourceNativeOver000EExtendedClusterNoRebaseNoRewardRowsNoExeHelper.v36",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70T71PairSharedClusterSourceNativeNoRebaseNoRewardRowsNoExeHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after the row-repair helper candidate produced a bad Stone Hill load state. This returns to a data-only route and preserves the native Peace Keepers source-table identity bytes, especially +0x52 = 0xFF, instead of forcing live pre-hit bytes into the source records.",
            Description: "Peace Keepers T70 and T71 native Spring Chest records copied into Stone Hill with one shared spring-data cluster, no package rebase, no reward rows, no EXE helper, and native source identity bytes preserved.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149ZeroGap26928.v1",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "RegisterSingleNative0149Root",
            Status: "experimental-plan-only",
            Risk: "Follow-up after all Town Square controller/helper imports either soft-locked Stone Hill or behaved as a regular Flame/Charge chest. Peace Keepers has native Spring Chests without the Town Square 0x0186 helper root, so this copies only the 0x0149 Spring Chest actor package into a Stone Hill zero gap and preserves Stone Hill's native 0x00C2 chest actor.",
            Description: "Peace Keepers Spring Chest actor package copied into Stone Hill as a single 0x0149 root, with no imported controller/helper actor.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x26928", "0x0528")
            ],
            RootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xE8", "0x26928", "0x0149", "", "Registers the imported Peace Keepers Spring Chest actor package while preserving Stone Hill's native 0x00C2 route.")
            ],
            ReplaceRootEntries: [],
            InternalDependencyRebases:
            [
                new CrossLevelActorPackageDependencyRebase("0x1C23D0", "0x1DEBDC", "0x4000", "0x00FB", "Reuse Stone Hill resident actor 0x00FB dependency root for the one internal pointer found in the Peace Keepers 0x0149 package.")
            ]),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T71SpecialZeroGap26928.v2",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "RegisterSingleNative0149RootNonzeroSpecialDonor",
            Status: "experimental-plan-only",
            Risk: "Follow-up after the Peace Keepers T70 0x0149 donor loaded in Stone Hill but behaved as a normal Flame/Charge chest with one red gem. This keeps the same safe 0x0149 package/root import, but pairs it with Peace Keepers donor T71, whose source special data is nonzero, to test whether the previous donor row was the behavior problem rather than the package import.",
            Description: "Peace Keepers Spring Chest actor package copied into Stone Hill as a single 0x0149 root, tested with nonzero-special donor T71.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x26928", "0x0528")
            ],
            RootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xE8", "0x26928", "0x0149", "", "Registers the imported Peace Keepers Spring Chest actor package while preserving Stone Hill's native 0x00C2 route.")
            ],
            ReplaceRootEntries: [],
            InternalDependencyRebases:
            [
                new CrossLevelActorPackageDependencyRebase("0x1C23D0", "0x1DEBDC", "0x4000", "0x00FB", "Reuse Stone Hill resident actor 0x00FB dependency root for the one internal pointer found in the Peace Keepers 0x0149 package.")
            ]),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T71SourceFFZeroGap26928.v3",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "RegisterSingleNative0149RootNativeSourceFlag",
            Status: "experimental-plan-only",
            Risk: "Follow-up after the Peace Keepers T71 nonzero-special donor loaded in Stone Hill but behaved as a normal Flame/Charge chest with one red gem when the exported source identity byte at +0x52 was 0x10. Native same-level Spring Chest clone proofs preserve source +0x52 as 0xFF, so this candidate keeps the same safe actor-package/root import and tests the source-authentic flag byte.",
            Description: "Peace Keepers Spring Chest actor package copied into Stone Hill as a single 0x0149 root, tested with donor T71 and native source flag4A 0xFF.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x26928", "0x0528")
            ],
            RootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xE8", "0x26928", "0x0149", "", "Registers the imported Peace Keepers Spring Chest actor package while preserving Stone Hill's native 0x00C2 route.")
            ],
            ReplaceRootEntries: [],
            InternalDependencyRebases:
            [
                new CrossLevelActorPackageDependencyRebase("0x1C23D0", "0x1DEBDC", "0x4000", "0x00FB", "Reuse Stone Hill resident actor 0x00FB dependency root for the one internal pointer found in the Peace Keepers 0x0149 package.")
            ]),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T71LocalControllerHelperZeroGap26928.v4",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "RegisterSingleNative0149RootLocalControllerHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after the visible Peace Keepers T71 shell-only candidate behaved as a normal Flame/Charge chest and the source-FF shell-only candidate became invisible. This returns to the visible source identity byte 0x10, preserves Stone Hill's native 0x00C2 actor package, and adds a local controller/helper companion so the custom helper can arm the shell and spawn the reward.",
            Description: "Peace Keepers Spring Chest actor package copied into Stone Hill as a single 0x0149 root, paired with a local Stone Hill 0x00C2 controller/helper candidate.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x26928", "0x0528")
            ],
            RootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xE8", "0x26928", "0x0149", "", "Registers the imported Peace Keepers Spring Chest actor package while preserving Stone Hill's native 0x00C2 route.")
            ],
            ReplaceRootEntries: [],
            InternalDependencyRebases:
            [
                new CrossLevelActorPackageDependencyRebase("0x1C23D0", "0x1DEBDC", "0x4000", "0x00FB", "Reuse Stone Hill resident actor 0x00FB dependency root for the one internal pointer found in the Peace Keepers 0x0149 package.")
            ]),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T71LocalControllerNoExeHelperZeroGap26928.v5",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "RegisterSingleNative0149RootLocalControllerNoExeHelper",
            Status: "experimental-plan-only",
            Risk: "Isolation follow-up after the visible local-controller/helper candidate still looked like a normal Flame/Charge chest and crashed or hung in-game when Spyro approached to flame it. This keeps the same Peace Keepers 0x0149 package/root and local Stone Hill 0x00C2 controller record, but suppresses the custom EXE helper and reward row so the next test can distinguish actor/controller instability from helper/reward corruption.",
            Description: "Peace Keepers Spring Chest actor package copied into Stone Hill as a single 0x0149 root, paired with a local Stone Hill 0x00C2 controller record, with no custom helper or reward-row injection.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x26928", "0x0528")
            ],
            RootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xE8", "0x26928", "0x0149", "", "Registers the imported Peace Keepers Spring Chest actor package while preserving Stone Hill's native 0x00C2 route.")
            ],
            ReplaceRootEntries: [],
            InternalDependencyRebases:
            [
                new CrossLevelActorPackageDependencyRebase("0x1C23D0", "0x1DEBDC", "0x4000", "0x00FB", "Reuse Stone Hill resident actor 0x00FB dependency root for the one internal pointer found in the Peace Keepers 0x0149 package.")
            ]),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T71LocalControllerRewardRowNoExeHelperZeroGap26928.v6",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "RegisterSingleNative0149RootLocalControllerRewardRowNoExeHelper",
            Status: "experimental-plan-only",
            Risk: "Isolation follow-up after the no-helper candidate booted, appeared, and broke safely as a normal chest with a blue gem. This keeps the stable Peace Keepers 0x0149 package/root and local Stone Hill 0x00C2 controller record, adds only the hidden reward row, and still suppresses the custom EXE helper so the next test can distinguish reward-row safety from helper-code corruption.",
            Description: "Peace Keepers Spring Chest actor package copied into Stone Hill as a single 0x0149 root, paired with a local Stone Hill 0x00C2 controller record and hidden reward row, with no custom helper injection.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x26928", "0x0528")
            ],
            RootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xE8", "0x26928", "0x0149", "", "Registers the imported Peace Keepers Spring Chest actor package while preserving Stone Hill's native 0x00C2 route.")
            ],
            ReplaceRootEntries: [],
            InternalDependencyRebases:
            [
                new CrossLevelActorPackageDependencyRebase("0x1C23D0", "0x1DEBDC", "0x4000", "0x00FB", "Reuse Stone Hill resident actor 0x00FB dependency root for the one internal pointer found in the Peace Keepers 0x0149 package.")
            ]),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T71LocalControllerArmOnlyHelperZeroGap26928.v7",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "RegisterSingleNative0149RootLocalControllerArmOnlyHelper",
            Status: "experimental-plan-only",
            Risk: "Isolation follow-up after both the no-helper and reward-row no-helper candidates loaded, appeared, and broke safely as normal chests. This keeps the stable Peace Keepers 0x0149 package/root and local Stone Hill 0x00C2 controller record, then adds only a tiny arm-only EXE helper that writes controller/shell linkage once. It intentionally skips reward spawning and post-break object moves so the next test can tell whether the hook/arming step is safe.",
            Description: "Peace Keepers Spring Chest actor package copied into Stone Hill as a single 0x0149 root, paired with a local Stone Hill 0x00C2 controller record and an arm-only helper hook.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x26928", "0x0528")
            ],
            RootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xE8", "0x26928", "0x0149", "", "Registers the imported Peace Keepers Spring Chest actor package while preserving Stone Hill's native 0x00C2 route.")
            ],
            ReplaceRootEntries: [],
            InternalDependencyRebases:
            [
                new CrossLevelActorPackageDependencyRebase("0x1C23D0", "0x1DEBDC", "0x4000", "0x00FB", "Reuse Stone Hill resident actor 0x00FB dependency root for the one internal pointer found in the Peace Keepers 0x0149 package.")
            ]),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T71LocalControllerHookOnlyZeroGap26928.v8",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "RegisterSingleNative0149RootLocalControllerHookOnly",
            Status: "experimental-plan-only",
            Risk: "Isolation follow-up after the arm-only helper loaded and survived approach, but crashed when Spyro flamed the object. This keeps the stable Peace Keepers 0x0149 package/root and local Stone Hill 0x00C2 controller record, then adds only a pass-through EXE hook that calls the original game function and returns. It writes no controller, shell, or reward fields.",
            Description: "Peace Keepers Spring Chest actor package copied into Stone Hill as a single 0x0149 root, paired with a local Stone Hill 0x00C2 controller record and a pass-through helper hook.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x26928", "0x0528")
            ],
            RootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xE8", "0x26928", "0x0149", "", "Registers the imported Peace Keepers Spring Chest actor package while preserving Stone Hill's native 0x00C2 route.")
            ],
            ReplaceRootEntries: [],
            InternalDependencyRebases:
            [
                new CrossLevelActorPackageDependencyRebase("0x1C23D0", "0x1DEBDC", "0x4000", "0x00FB", "Reuse Stone Hill resident actor 0x00FB dependency root for the one internal pointer found in the Peace Keepers 0x0149 package.")
            ]),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T71LocalControllerStackHookOnlyZeroGap26928.v9",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "RegisterSingleNative0149RootLocalControllerStackHookOnly",
            Status: "experimental-plan-only",
            Risk: "Isolation follow-up after the pass-through helper hook loaded and survived approach, but crashed when Spyro flamed the object. This keeps the same Peace Keepers 0x0149 package/root and local Stone Hill 0x00C2 controller record, but the pass-through hook saves its return address on the normal stack instead of writing into the payload scratch area.",
            Description: "Peace Keepers Spring Chest actor package copied into Stone Hill as a single 0x0149 root, paired with a local Stone Hill 0x00C2 controller record and a stack-save pass-through helper hook.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x26928", "0x0528")
            ],
            RootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xE8", "0x26928", "0x0149", "", "Registers the imported Peace Keepers Spring Chest actor package while preserving Stone Hill's native 0x00C2 route.")
            ],
            ReplaceRootEntries: [],
            InternalDependencyRebases:
            [
                new CrossLevelActorPackageDependencyRebase("0x1C23D0", "0x1DEBDC", "0x4000", "0x00FB", "Reuse Stone Hill resident actor 0x00FB dependency root for the one internal pointer found in the Peace Keepers 0x0149 package.")
            ]),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T71LocalControllerEntryHookOnlyAltCaveZeroGap26928.v10",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "RegisterSingleNative0149RootLocalControllerEntryHookOnlyAltCave",
            Status: "experimental-plan-only",
            Risk: "Isolation follow-up after the stack-save pass-through call-site helper loaded and survived approach, but crashed when Spyro flamed the object. This keeps the same Peace Keepers 0x0149 package/root and local Stone Hill 0x00C2 controller record, but hooks the original routine entry instead of calling it from a wrapper, and moves the payload to a later blank executable cave.",
            Description: "Peace Keepers Spring Chest actor package copied into Stone Hill as a single 0x0149 root, paired with a local Stone Hill 0x00C2 controller record and a pass-through entry trampoline in an alternate code cave.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x26928", "0x0528")
            ],
            RootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xE8", "0x26928", "0x0149", "", "Registers the imported Peace Keepers Spring Chest actor package while preserving Stone Hill's native 0x00C2 route.")
            ],
            ReplaceRootEntries: [],
            InternalDependencyRebases:
            [
                new CrossLevelActorPackageDependencyRebase("0x1C23D0", "0x1DEBDC", "0x4000", "0x00FB", "Reuse Stone Hill resident actor 0x00FB dependency root for the one internal pointer found in the Peace Keepers 0x0149 package.")
            ]),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T71FirstEmptyRootSourceFFNoExeHelperZeroGap26928.v11",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "RegisterSingleNative0149FirstEmptyRootSourceFFNoExeHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after the entry-hook alt-cave candidate failed before the title screen and earlier no-helper candidates behaved like normal chests. The older Peace Keepers recipes registered 0x0149 at Stone Hill root slot 0xE8 while root slot 0xE4 stayed zero; if the game treats that zero as the end of the actor-root list, the Spring Chest actor was never actually loaded. This candidate uses Stone Hill's first empty root slot 0xE4, preserves the native source flag 0xFF on the T71 record, and writes no controller/helper code.",
            Description: "Peace Keepers Spring Chest actor package copied into Stone Hill as the next contiguous 0x0149 root, with a source-native T71 shell and no custom helper.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x26928", "0x0528")
            ],
            RootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xE4", "0x26928", "0x0149", "", "Registers the imported Peace Keepers Spring Chest actor package in Stone Hill's first empty actor-root slot, keeping the root list contiguous.")
            ],
            ReplaceRootEntries: [],
            InternalDependencyRebases:
            [
                new CrossLevelActorPackageDependencyRebase("0x1C23D0", "0x1DEBDC", "0x4000", "0x00FB", "Reuse Stone Hill resident actor 0x00FB dependency root for the one internal pointer found in the Peace Keepers 0x0149 package.")
            ]),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70FirstEmptyRootSourceFFNoExeHelperZeroGap26928.v12",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "RegisterSingleNative0149FirstEmptyRootSourceFFZeroSpecialNoExeHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after the first-empty-root T71 candidate loaded and the object appeared, but flaming it crashed the game. T71 carries a nonzero source special-data block, while Peace Keepers T70 uses the same native 0x0149 Spring Chest actor/package shape with an all-zero source special-data block. This candidate keeps the fixed contiguous 0xE4 actor-root registration, preserves the native source flag 0xFF, and tests whether the simpler T70 donor avoids the flame-time crash.",
            Description: "Peace Keepers T70 Spring Chest actor package copied into Stone Hill as the next contiguous 0x0149 root, with source-native zero special data and no custom helper.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x26928", "0x0528")
            ],
            RootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xE4", "0x26928", "0x0149", "", "Registers the imported Peace Keepers Spring Chest actor package in Stone Hill's first empty actor-root slot, keeping the root list contiguous.")
            ],
            ReplaceRootEntries: [],
            InternalDependencyRebases:
            [
                new CrossLevelActorPackageDependencyRebase("0x1C23D0", "0x1DEBDC", "0x4000", "0x00FB", "Reuse Stone Hill resident actor 0x00FB dependency root for the one internal pointer found in the Peace Keepers 0x0149 package.")
            ]),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T71FirstEmptyRootSourceFFNoRebaseNoExeHelperZeroGap26928.v13",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "RegisterSingleNative0149FirstEmptyRootSourceFFNoRebaseNoExeHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after both first-empty-root data-only candidates crashed in-game. The previous recipes rebased a 4-byte word inside the copied Peace Keepers 0x0149 package because it fell inside a source actor-root range, but that same literal word appears in multiple native Spring Chest packages and is likely package data rather than a dependency pointer. This candidate keeps the fixed contiguous 0xE4 actor-root registration and T71 donor row, but copies the 0x0149 actor package byte-for-byte with no internal rebase.",
            Description: "Peace Keepers T71 Spring Chest actor package copied byte-for-byte into Stone Hill as the next contiguous 0x0149 root, with source-native special data and no custom helper.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x26928", "0x0528")
            ],
            RootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xE4", "0x26928", "0x0149", "", "Registers the imported Peace Keepers Spring Chest actor package in Stone Hill's first empty actor-root slot, keeping the root list contiguous.")
            ],
            ReplaceRootEntries: [],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T71LocalControllerFirstEmptyRootNoRebaseNoExeHelperZeroGap26928.v14",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "RegisterSingleNative0149T71LocalControllerFirstEmptyRootNoRebaseNoExeHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after the shell-only first-empty-root no-rebase candidate crashed to black screen in-game. The earlier local-controller no-helper candidate was stable but registered 0x0149 after an empty root slot, so the imported actor probably never loaded. This candidate keeps the byte-for-byte Peace Keepers T71 0x0149 package copy and first-empty 0xE4 root, but also appends the local Stone Hill 0x00C2 controller record before the visible Spring Chest shell.",
            Description: "Peace Keepers T71 Spring Chest shell plus local Stone Hill controller, using the first contiguous 0x0149 root, byte-for-byte package copy, and no custom helper.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x26928", "0x0528")
            ],
            RootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xE4", "0x26928", "0x0149", "", "Registers the imported Peace Keepers Spring Chest actor package in Stone Hill's first empty actor-root slot, keeping the root list contiguous.")
            ],
            ReplaceRootEntries: [],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T71LocalControllerOver000ENoRebaseNoExeHelper.v15",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T71LocalControllerNoRebaseNoExeHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after both no-rebase first-empty-root candidates crashed to a black screen with a screeching audio bug. Those candidates put the imported package at low zero-gap offset 0x26928 but registered it at the end of the actor-root table, unlike native sorted package roots. This candidate copies the Peace Keepers 0x0149 package byte-for-byte over Stone Hill's unused-looking 0x000E package/root slot, preserving the root table order and appending the local Stone Hill 0x00C2 controller plus visible T71 shell.",
            Description: "Peace Keepers T71 Spring Chest shell plus local Stone Hill controller, copied over Stone Hill's unused 0x000E actor slot with no helper and no package-byte rebase.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest shell while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T71LocalControllerOver000EFixedExeHelper.v16",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T71LocalControllerFixedExeHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after the over-0x000E no-helper candidate booted and showed the imported object without crashing, but flame/charge did nothing. This keeps the stable byte-for-byte 0x0149 package copy over Stone Hill's unused-looking 0x000E root, then adds the reward row plus EXE helper after fixing sector-aware executable patch writes.",
            Description: "Peace Keepers T71 Spring Chest shell plus local Stone Hill controller, copied over Stone Hill's unused 0x000E actor slot with the fixed sector-safe helper/reward patch enabled.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest shell while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T71LocalControllerOver000EStackHookOnly.v17",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T71LocalControllerStackHookOnly",
            Status: "experimental-plan-only",
            Risk: "Isolation follow-up after the fixed-helper/reward candidate booted and appeared but crashed when Spyro approached the object. This keeps the same stable over-0x000E package/root layout and local controller, but injects only a stack-save pass-through hook that writes no controller, shell, or reward fields.",
            Description: "Peace Keepers T71 Spring Chest shell plus local Stone Hill controller, copied over Stone Hill's unused 0x000E actor slot with only a stack-save pass-through helper hook.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest shell while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T71LocalControllerOver000EArmOnlyHelper.v18",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T71LocalControllerArmOnlyHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after the over-0x000E stack hook-only candidate stayed stable but did not change behavior. This keeps the same stable package/root layout and adds only the arm-only helper writes that link the local controller and visible shell; it still skips reward spawning and post-break movement.",
            Description: "Peace Keepers T71 Spring Chest shell plus local Stone Hill controller, copied over Stone Hill's unused 0x000E actor slot with only the arm/link helper writes enabled.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest shell while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70Over000ENoControllerNoExeHelper.v19",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70NoControllerNoExeHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after the over-0x000E arm-only helper candidate stayed stable but did not trigger Spring Chest behavior. This keeps the safe actor package/root layout, switches to the Peace Keepers T70 all-zero-special native spring chest row, and removes the invented local Stone Hill controller/helper path entirely.",
            Description: "Peace Keepers T70 native Spring Chest copied over Stone Hill's unused 0x000E actor slot with no local controller, reward row, or EXE helper.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70LocalControllerOver000ERuntime01A6ArmOnlyHelper.v20",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70LocalControllerRuntime01A6ArmOnlyHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after the T70 native-only over-0x000E candidate booted, appeared, and stayed stable but did not react to flame or charge. This keeps the same stable actor package/root slot, re-adds a local Stone Hill 0x00C2 controller, and changes the helper writes to use Peace Keepers T70's observed runtime variant 0x01A6 instead of the older Town Square 0x01AC value. It still skips reward spawning so the next test can isolate whether the donor-specific runtime link makes the native actor respond.",
            Description: "Peace Keepers T70 Spring Chest copied over Stone Hill's unused 0x000E actor slot, paired with a local Stone Hill controller and donor-specific 0x01A6 arm/link helper.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70Over000EExtendedSpecialNoControllerNoExeHelper.v21",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70ExtendedSpecialNoControllerNoExeHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after the donor-specific 0x01A6 arm-only helper candidate stayed stable but did not trigger Spring Chest behavior. This removes the invented helper/controller path again and changes the data import instead: the Peace Keepers T70 special-data copy is widened to preserve the overlapping native spring-chest stream that the old source-table length inference truncated.",
            Description: "Peace Keepers T70 native Spring Chest copied over Stone Hill's unused 0x000E actor slot with an extended native special-data stream, no local controller, and no EXE helper.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70Over000EExtendedSpecialFlag10NoControllerNoExeHelper.v22",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70ExtendedSpecialFlag10NoControllerNoExeHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after the extended-special T70 candidate booted, appeared, and stayed stable but still did not react to flame or charge. This keeps the same stable package/root and extended special-data stream, but writes the runtime-observed 0x10 identity byte at source offset 0x52 instead of preserving the source-side 0xFF value, to isolate whether hit/react behavior depends on that loader field.",
            Description: "Peace Keepers T70 native Spring Chest copied over Stone Hill's unused 0x000E actor slot with extended native special data and runtime-shaped flag4A 0x10.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70Over000EExtendedClusterFlag10NoControllerNoExeHelper.v23",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70ExtendedClusterFlag10NoControllerNoExeHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after the extended-special flag4A-0x10 candidate booted, appeared, and stayed stable but did not react to flame or charge. This keeps the same stable package/root and runtime-shaped flag4A 0x10, but widens the Peace Keepers T70 donor data copy to the larger shared native spring-chest cluster so the actor has the neighboring behavior data it appears to reference at runtime.",
            Description: "Peace Keepers T70 native Spring Chest copied over Stone Hill's unused 0x000E actor slot with a larger shared native spring-data cluster and runtime-shaped flag4A 0x10.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70Over000EExtendedClusterFlag10RebasedNoControllerNoExeHelper.v24",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70ExtendedClusterFlag10RebasedNoControllerNoExeHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after the larger shared spring-data cluster booted and drew the chest but still did not react to flame or charge. This keeps the stable over-0x000E actor/root slot and the larger T70 data cluster, then restores the single internal dependency rebase used by earlier Peace Keepers 0x0149 imports to test whether that package pointer is what arms hit/break behavior.",
            Description: "Peace Keepers T70 native Spring Chest copied over Stone Hill's unused 0x000E actor slot with a larger shared native spring-data cluster, runtime-shaped flag4A 0x10, and the known 0x0149 package dependency rebase.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases:
            [
                new CrossLevelActorPackageDependencyRebase("0x1C23D0", "0x1DEBDC", "0x4000", "0x00FB", "Reuse Stone Hill resident actor 0x00FB dependency root for the one internal pointer found in the Peace Keepers 0x0149 package.")
            ]),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70Peacekeepers00C2Over000EExtendedClusterFlag10RebasedNoExeHelper.v25",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceNative00C2AndUnused000ENative0149T70Peacekeepers00C2ExtendedClusterFlag10RebasedNoExeHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after the rebased larger-cluster T70 candidate booted, drew the chest, and stayed stable but remained inert. This keeps the stable over-0x000E 0x0149 import, then also replaces Stone Hill's native 0x00C2 chest/controller package with the matching Peace Keepers 0x00C2 package to test whether the imported spring chest needs the donor-level hit/controller behavior package.",
            Description: "Peace Keepers T70 native Spring Chest copied over Stone Hill's unused 0x000E actor slot while the Peace Keepers 0x00C2 chest/controller package replaces Stone Hill's native 0x00C2 package in the disposable test image.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1A8E6C", "0x1BB164", "0x03E8", "overwrite-existing-actor-package", "0x74", "0x00C2"),
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases:
            [
                new CrossLevelActorPackageDependencyRebase("0x1C23D0", "0x1DEBDC", "0x4000", "0x00FB", "Reuse Stone Hill resident actor 0x00FB dependency root for the one internal pointer found in the Peace Keepers 0x0149 package.")
            ]),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70T71PairOver000EExtendedClusterFlag10RebasedNoControllerNoExeHelper.v26",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70T71PairExtendedClusterFlag10RebasedNoControllerNoExeHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after replacing Stone Hill's native 0x00C2 package changed normal chest visuals and rewards but still left the imported spring chest inert. This preserves Stone Hill's native 0x00C2 package again and instead appends the adjacent Peace Keepers T70/T71 native 0x0149 spring pair together, because the native level stores these spring actors as close paired records that may provide the missing hit/link relationship.",
            Description: "Peace Keepers T70 and T71 native Spring Chest records copied into Stone Hill as a paired import while keeping Stone Hill's native 0x00C2 chest package untouched.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases:
            [
                new CrossLevelActorPackageDependencyRebase("0x1C23D0", "0x1DEBDC", "0x4000", "0x00FB", "Reuse Stone Hill resident actor 0x00FB dependency root for the one internal pointer found in the Peace Keepers 0x0149 package.")
            ]),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70T71PairSharedClusterOver000EExtendedClusterFlag10RebasedNoControllerNoExeHelper.v27",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70T71PairSharedClusterExtendedClusterFlag10RebasedNoControllerNoExeHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after the native Peace Keepers T70/T71 pair appeared as two inert spring chests when each record was given an independent copied special-data cluster. This preserves Stone Hill's native 0x00C2 package and copies one shared Peace Keepers spring-data cluster so T70 points at the base and T71 points at the native +0x14 offset.",
            Description: "Peace Keepers T70 and T71 native Spring Chest records copied into Stone Hill as a paired import with their native shared spring-data cluster relationship preserved.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases:
            [
                new CrossLevelActorPackageDependencyRebase("0x1C23D0", "0x1DEBDC", "0x4000", "0x00FB", "Reuse Stone Hill resident actor 0x00FB dependency root for the one internal pointer found in the Peace Keepers 0x0149 package.")
            ]),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70T71PairSharedClusterSourceFFOver000EExtendedClusterRebasedNoControllerNoExeHelper.v28",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70T71PairSharedClusterExtendedClusterSourceFFRebasedNoControllerNoExeHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after the shared-cluster T70/T71 pair appeared as two inert Spring Chests when both appended records were forced to runtime-shaped source byte +0x52 = 0x10. The clean Peace Keepers source records T70 and T71 both carry +0x52 = 0xFF, so this keeps the shared cluster and package/root route but preserves the native source flag byte.",
            Description: "Peace Keepers T70 and T71 native Spring Chest records copied into Stone Hill as a paired import with one shared spring-data cluster and native +0x52 source flags preserved.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases:
            [
                new CrossLevelActorPackageDependencyRebase("0x1C23D0", "0x1DEBDC", "0x4000", "0x00FB", "Reuse Stone Hill resident actor 0x00FB dependency root for the one internal pointer found in the Peace Keepers 0x0149 package.")
            ]),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70T71PairSharedClusterLiveInteractionRowRepairHelperOver000EExtendedClusterNoRebaseNoRewardRows.v35",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70T71PairSharedClusterLiveInteractionRowRepairHelperNoRebaseNoRewardRows",
            Status: "experimental-plan-only",
            Risk: "Follow-up after the v34 fresh RAM capture proved the safe no-00C2 route loads most live pre-hit bytes correctly, but Stone Hill's loader rewrites both imported Spring Chest rows to pair marker 0x0212 and state 0x01. This keeps the v34 package and shared-data route, then adds a one-shot main-loop repair that restores T195/T196 to native pair markers 0x01A6/0x01A7 and state 0x00 after Stone Hill's loader finishes. This candidate produced a bad Stone Hill load state in user testing and must remain below safer data-only routes.",
            Description: "Peace Keepers T70 and T71 native Spring Chest records copied into Stone Hill with shared spring-data and a one-shot post-load row repair for the pair marker/state bytes that Stone Hill rewrites.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70T71PairSharedClusterLiveInteractionOver000EExtendedClusterNoRebaseNoRewardRowsNoExeHelper.v34",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70T71PairSharedClusterLiveInteractionNoRebaseNoRewardRowsNoExeHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after replacing Stone Hill's native 0x00C2 package corrupted normal chest visuals and still left the Spring Chests inert. This returns to the stable no-00C2 route, keeps the shared Peace Keepers T70/T71 spring-data cluster and fuller live pre-hit row bytes, but removes the single internal 0x0149 package rebase to test whether that suspected dependency word was actually package-local data needed for hit/break behavior.",
            Description: "Peace Keepers T70 and T71 native Spring Chest records copied into Stone Hill with shared spring-data and fuller live pre-hit row bytes, while the 0x0149 actor package is copied byte-for-byte with no internal rebase.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases: []),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70T71PairLiveInteractionPeacekeepers00C2Over000EExtendedClusterRebasedNoRewardRowsNoExeHelper.v33",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceNative00C2AndUnused000ENative0149T70T71PairLiveInteractionPeacekeepers00C2RebasedNoRewardRowsNoExeHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after the fuller live-row candidate changed the visible Spring Chest top color but still did not make flame or charge pop a gem. This keeps the no-reward-row T70/T71 pair and live pre-hit row bytes, then also replaces Stone Hill's native 0x00C2 chest/controller package with Peace Keepers' matching 0x00C2 package to test whether hit/break handling lives in the donor controller package. This can alter normal Stone Hill chest behavior and must remain disposable-candidate only.",
            Description: "Peace Keepers T70/T71 Spring Chest pair copied into Stone Hill with live pre-hit row bytes while the matching Peace Keepers 0x00C2 chest/controller package replaces Stone Hill's native 0x00C2 package, without copying loose/reward rows.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1A8E6C", "0x1BB164", "0x03E8", "overwrite-existing-actor-package", "0x74", "0x00C2"),
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases:
            [
                new CrossLevelActorPackageDependencyRebase("0x1C23D0", "0x1DEBDC", "0x4000", "0x00FB", "Reuse Stone Hill resident actor 0x00FB dependency root for the one internal pointer found in the Peace Keepers 0x0149 package.")
            ]),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70T71PairSharedClusterLiveInteractionOver000EExtendedClusterRebasedNoRewardRowsNoExeHelper.v32",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70T71PairSharedClusterLiveInteractionRebasedNoRewardRowsNoExeHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after the runtime-armed no-reward-row candidate drew both Spring Chests but they still did not react to flame or charge. Native live Peace Keepers T70/T71 rows carry additional pre-hit runtime bytes at +0x25, +0x29, +0x2D, +0x41, +0x4C, +0x4D, and +0x55; this candidate stamps that fuller live row shape while still avoiding loose copied reward rows.",
            Description: "Peace Keepers T70 and T71 native Spring Chest records copied into Stone Hill with shared spring-data and the fuller live pre-hit row byte shape, without copying native loose/reward rows.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases:
            [
                new CrossLevelActorPackageDependencyRebase("0x1C23D0", "0x1DEBDC", "0x4000", "0x00FB", "Reuse Stone Hill resident actor 0x00FB dependency root for the one internal pointer found in the Peace Keepers 0x0149 package.")
            ]),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70T71PairSharedClusterRuntimeArmedOver000EExtendedClusterRebasedNoRewardRowsNoExeHelper.v31",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70T71PairSharedClusterRuntimeArmedRebasedNoRewardRowsNoExeHelper",
            Status: "experimental-plan-only",
            Risk: "Isolation follow-up after the native reward rows appeared immediately as loose gems and did not make the imported Spring Chests react. This keeps the live Peace Keepers armed source shape for T70/T71, but removes the copied T92/T93/T94 rows so the next in-game/RAM test isolates whether the imported actors themselves ever receive flame or charge interaction.",
            Description: "Peace Keepers T70 and T71 native Spring Chest records copied into Stone Hill with shared spring-data and source bytes shaped to match the armed live Peace Keepers rows, without copying native loose/reward rows.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases:
            [
                new CrossLevelActorPackageDependencyRebase("0x1C23D0", "0x1DEBDC", "0x4000", "0x00FB", "Reuse Stone Hill resident actor 0x00FB dependency root for the one internal pointer found in the Peace Keepers 0x0149 package.")
            ]),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70T71PairSharedClusterRuntimeArmedOver000EExtendedClusterRebasedNativeRewardRowsNoExeHelper.v30",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70T71PairSharedClusterRuntimeArmedRebasedNativeRewardRowsNoExeHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after the native reward rows appeared immediately as loose gems while the imported spring chests stayed inert. Live RAM shows native Peace Keepers T70/T71 are armed as variants 0x01A6/0x01A7 with flag byte 0x10 and runtime byte +0x49 = 1; this candidate writes those source/runtime-shaping bytes into the copied rows and keeps the native reward rows.",
            Description: "Peace Keepers T70 and T71 native Spring Chest records copied into Stone Hill with shared spring-data, native reward rows, and source bytes shaped to match the armed live Peace Keepers rows.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases:
            [
                new CrossLevelActorPackageDependencyRebase("0x1C23D0", "0x1DEBDC", "0x4000", "0x00FB", "Reuse Stone Hill resident actor 0x00FB dependency root for the one internal pointer found in the Peace Keepers 0x0149 package.")
            ]),

        new CrossLevelActorPackageRecipe(
            Id: "stonehill.peacekeepers.springChest.package.native0149T70T71PairSharedClusterSourceFFOver000EExtendedClusterRebasedNativeRewardRowsNoExeHelper.v29",
            TargetLevelKey: "stonehill",
            SourceLevelKey: "peacekeepers",
            Family: "springChest",
            Mode: "ReplaceUnused000ENative0149T70T71PairSharedClusterExtendedClusterSourceFFRebasedNativeRewardRowsNoExeHelper",
            Status: "experimental-plan-only",
            Risk: "Follow-up after live RAM proved native Peace Keepers T70/T71 did not change their own rows when hit; instead nearby reward rows T92/T93/T94 activated. This preserves the stable no-helper package/root route and appends those native reward rows beside the imported Stone Hill test chests.",
            Description: "Peace Keepers T70 and T71 native Spring Chest records copied into Stone Hill with one shared spring-data cluster, native +0x52 source flags, and the native T92/T93/T94 reward rows appended without a custom EXE helper.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x1B3450", "0x1D5CC8", "0x0528", "overwrite-unused-actor-package", "0xA4", "0x000E")
            ],
            RootEntries: [],
            ReplaceRootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xA4", "0x1D5CC8", "0x0149", "0x000E", "Replaces Stone Hill's unused-looking actor 0x000E root with the imported Peace Keepers Spring Chest actor while preserving root-table order.")
            ],
            InternalDependencyRebases:
            [
                new CrossLevelActorPackageDependencyRebase("0x1C23D0", "0x1DEBDC", "0x4000", "0x00FB", "Reuse Stone Hill resident actor 0x00FB dependency root for the one internal pointer found in the Peace Keepers 0x0149 package.")
            ]),

        new CrossLevelActorPackageRecipe(
            Id: "toasty.wizardpeak.greenWizard.package.overwriteUnused00EA.v2",
            TargetLevelKey: "toasty",
            SourceLevelKey: "wizardpeak",
            Family: "enemyTransform",
            Mode: "RegisterCompanionRoot",
            Status: "experimental-image-write",
            Risk: "Overwrites Toasty's actor package at 0x15326C, whose actor id is 0x00EA and which is not referenced by Toasty's source moby records. This writes only to disposable test BIN/CUE output until in-game validation proves no hidden Toasty behavior depends on that package.",
            Description: "Green Wizard actor package copied over Toasty's apparently unused 0x00EA package, then registered in an empty Toasty actor-root slot as actor 0x011B. This avoids the too-small zero gaps while preserving the active dog/shepherd/chest roots.",
            CopySegments:
            [
                new CrossLevelActorPackageCopySegment("0x19B518", "0x15326C", "0x35AC", "overwrite-unused-actor-package", "0x54", "0x00EA")
            ],
            RootEntries:
            [
                new CrossLevelActorPackageRootEntry("0xD0", "0x15326C", "0x011B", "", "Registers Green Wizard as a new Toasty actor root using the imported package.")
            ],
            ReplaceRootEntries: [],
            InternalDependencyRebases: [])
    ];

    public static IReadOnlyList<CrossLevelActorPackageRecipe> All => Recipes;

    public static CrossLevelActorPackageRecipe? FindPreferred(string targetLevelKey, string sourceLevelKey, string family, string workspaceRoot = "")
    {
        List<CrossLevelActorPackageRecipe> candidates = FindAll(targetLevelKey, sourceLevelKey, family).ToList();
        if (candidates.Count == 0)
            return null;
        if (string.IsNullOrWhiteSpace(workspaceRoot))
            return candidates.FirstOrDefault(recipe => !IsBlockedStatus(recipe.Status)) ?? candidates[0];

        CrossLevelActorPackageRecipe? unfailed = null;
        foreach (CrossLevelActorPackageRecipe recipe in candidates)
        {
            if (IsBlockedStatus(recipe.Status))
                continue;

            CrossLevelCandidateEvidence evidence = CrossLevelCandidateEvidenceStore.FindBestEvidence(workspaceRoot, recipe.TargetLevelKey, recipe.Id);
            if (evidence.Passed)
                return recipe;
            if (!evidence.Failed && unfailed == null)
                unfailed = recipe;
        }

        bool stoneHillTownSquareSpringChest =
            string.Equals(LevelCatalog.NormalizeKey(targetLevelKey), "stonehill", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(LevelCatalog.NormalizeKey(sourceLevelKey), "townsquare", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(NormalizeFamily(family), "springchest", StringComparison.OrdinalIgnoreCase);
        if (stoneHillTownSquareSpringChest)
            return null;

        if (unfailed == null && string.Equals(NormalizeFamily(family), "springChest", StringComparison.OrdinalIgnoreCase))
            return null;

        return unfailed ?? candidates[0];
    }

    public static IEnumerable<CrossLevelActorPackageRecipe> FindAll(string targetLevelKey, string sourceLevelKey, string family)
    {
        string target = LevelCatalog.NormalizeKey(targetLevelKey);
        string source = LevelCatalog.NormalizeKey(sourceLevelKey);
        string normalizedFamily = NormalizeFamily(family);
        return Recipes.Where(recipe =>
            string.Equals(LevelCatalog.NormalizeKey(recipe.TargetLevelKey), target, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(LevelCatalog.NormalizeKey(recipe.SourceLevelKey), source, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(NormalizeFamily(recipe.Family), normalizedFamily, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsBlockedStatus(string status)
    {
        return status.StartsWith("in-game-blocked", StringComparison.OrdinalIgnoreCase) ||
            status.StartsWith("blocked", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeFamily(string value)
    {
        return (value ?? "").Trim().Replace(" ", "", StringComparison.Ordinal).Replace("_", "", StringComparison.Ordinal).ToLowerInvariant();
    }
}

public sealed record CrossLevelActorPackageRecipe(
    string Id,
    string TargetLevelKey,
    string SourceLevelKey,
    string Family,
    string Mode,
    string Status,
    string Risk,
    string Description,
    IReadOnlyList<CrossLevelActorPackageCopySegment> CopySegments,
    IReadOnlyList<CrossLevelActorPackageRootEntry> RootEntries,
    IReadOnlyList<CrossLevelActorPackageRootEntry> ReplaceRootEntries,
    IReadOnlyList<CrossLevelActorPackageDependencyRebase> InternalDependencyRebases)
{
    public int CopySegmentCount => CopySegments.Count;
    public int RootEntryCount => RootEntries.Count;
    public int ReplaceRootEntryCount => ReplaceRootEntries.Count;
    public int InternalDependencyRebaseCount => InternalDependencyRebases.Count;
}

public sealed record CrossLevelActorPackageCopySegment(
    string SourceStart,
    string TargetStart,
    string Length,
    string TargetSafety = "zero",
    string ExpectedExistingRootSlot = "",
    string ExpectedExistingActorId = "",
    string AllowedOverwrittenActorIds = "");

public sealed record CrossLevelActorPackageRootEntry(
    string TargetRootSlot,
    string TargetRoot,
    string ActorId,
    string OriginalActor,
    string Note);

public sealed record CrossLevelActorPackageDependencyRebase(
    string SourceRoot,
    string TargetRoot,
    string Length,
    string ActorId,
    string Note);
