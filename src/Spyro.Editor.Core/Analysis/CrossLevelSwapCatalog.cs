using System.Globalization;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Workspace;

namespace Spyro.Editor.Core.Analysis;

public static class CrossLevelSwapCatalogBuilder
{
    public const string CandidateSupportStatus = "experimental-cross-level-slot-swap";
    public const string CandidateExporterFeature = "CrossLevelExistingSlotCandidate";
    public const string ResidentActorCompatibility = "resident-actor-root";
    public const string PackageBackedCompatibility = "mapped-actor-package";
    public const string BlockedBehaviorCompatibility = "blocked-target-behavior";
    public const string MissingActorPackageCompatibility = "missing-actor-package";
    public const string GreenWizardVerifiedCompatibility = "green-wizard-runtime-bundle-verified";
    public const string GreenWizardCandidateCompatibility = "green-wizard-runtime-bundle-candidate";
    public const string GreenWizardUnsupportedCompatibility = "green-wizard-runtime-bundle-unsupported";

    public static bool TryGetEligibleTargetCategory(
        Moby moby,
        out CrossLevelSwapCategory category,
        out string reason)
    {
        SwapSourceClassification classification = Classify(moby);
        category = classification.Category;
        reason = classification.Reason;
        if (!classification.Include)
        {
            reason = "Select an ordinary chest or self-contained enemy source slot.";
            return false;
        }

        return classification.Eligible;
    }

    public static CrossLevelSwapCatalog Build(
        EditorWorkspace workspace,
        LevelCatalog catalog,
        string targetLevelKey)
    {
        string normalizedTarget = LevelCatalog.NormalizeKey(targetLevelKey);
        LevelDefinition? targetLevel = catalog.FindByKey(targetLevelKey);
        TargetActorRootMap targetActorRoots = ReadTargetActorRoots(workspace, targetLevel);
        IReadOnlyList<MappedCrossLevelTemplate> mappedTemplates = LoadMappedTemplates(workspace);
        List<CrossLevelSwapCatalogEntry> entries = [];

        foreach (LevelDefinition sourceLevel in catalog.Levels
            .Where(level => level.HasSourceTable)
            .OrderBy(level => level.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            string cachePath = Path.Combine(workspace.RootPath, "editor-cache", $"{sourceLevel.Key}-mobys.json");
            if (!File.Exists(cachePath))
                continue;

            List<Moby> mobys;
            try
            {
                mobys = MobyLoader.LoadCached(cachePath).ToList();
                MobyMetadataEnricher.Apply(workspace, sourceLevel.Key, mobys);
            }
            catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException)
            {
                continue;
            }

            HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
            foreach (Moby moby in mobys
                .Where(moby => !moby.IsRemoved && !moby.IsAdded)
                .Where(moby => moby.TrueIndex >= 0 && moby.TrueIndex < sourceLevel.SourceRecordCount)
                .OrderBy(moby => moby.DisplayLabel, StringComparer.OrdinalIgnoreCase)
                .ThenBy(moby => moby.TrueIndex))
            {
                SwapSourceClassification classification = Classify(moby);
                if (!classification.Include)
                    continue;

                int preliminaryActorId = moby.SourceByte36 | (moby.SourceByte37 << 8);
                MappedCrossLevelTemplate? exactMappedGreenWizard = mappedTemplates.FirstOrDefault(template =>
                    IsGreenWizardTemplate(template) &&
                    template.SourceTrueIndex == moby.TrueIndex &&
                    template.ActorId == preliminaryActorId &&
                    string.Equals(
                        LevelCatalog.NormalizeKey(template.SourceLevelKey),
                        LevelCatalog.NormalizeKey(sourceLevel.Key),
                        StringComparison.OrdinalIgnoreCase));
                string signature = BuildSignature(moby, classification.Category);
                if (exactMappedGreenWizard != null)
                    signature = $"{signature}:mapped:{exactMappedGreenWizard.Id}";
                if (!seen.Add(signature))
                    continue;

                bool sameLevel = string.Equals(
                    LevelCatalog.NormalizeKey(sourceLevel.Key),
                    normalizedTarget,
                    StringComparison.OrdinalIgnoreCase);
                int actorId = moby.SourceByte36 | (moby.SourceByte37 << 8);
                bool targetActorResident = targetActorRoots.TryGet(actorId, out uint targetActorRoot);
                MappedCrossLevelTemplate? mappedTemplate = !sameLevel
                    ? mappedTemplates.FirstOrDefault(template =>
                        template.SourceTrueIndex == moby.TrueIndex &&
                        template.ActorId == actorId &&
                        string.Equals(
                            LevelCatalog.NormalizeKey(template.SourceLevelKey),
                            LevelCatalog.NormalizeKey(sourceLevel.Key),
                            StringComparison.OrdinalIgnoreCase) &&
                        IsActorPackageTemplate(template))
                    : null;
                if (sameLevel)
                {
                    mappedTemplate = mappedTemplates.FirstOrDefault(template =>
                        IsGreenWizardTemplate(template) &&
                        template.SourceTrueIndex == moby.TrueIndex &&
                        template.ActorId == actorId &&
                        string.Equals(
                            LevelCatalog.NormalizeKey(template.SourceLevelKey),
                            LevelCatalog.NormalizeKey(sourceLevel.Key),
                            StringComparison.OrdinalIgnoreCase));
                }
                bool knownGreenWizardDonor =
                    actorId == GreenWizardRuntimeBundleCompatibility.ActorId &&
                    moby.Type == 0x20 &&
                    (LevelCatalog.NormalizeKey(sourceLevel.Key).Equals("wizardpeak", StringComparison.OrdinalIgnoreCase) ||
                     LevelCatalog.NormalizeKey(sourceLevel.Key).Equals("blowhard", StringComparison.OrdinalIgnoreCase) ||
                     LevelCatalog.NormalizeKey(sourceLevel.Key).Equals("magiccrafters", StringComparison.OrdinalIgnoreCase));
                bool nativeGreenWizard = sameLevel && knownGreenWizardDonor;
                bool greenWizardTemplate = knownGreenWizardDonor || mappedTemplate != null && IsGreenWizardTemplate(mappedTemplate);
                GreenWizardRuntimeBundleLevelSupport? greenWizardSupport = greenWizardTemplate
                    ? GreenWizardRuntimeBundleCompatibility.Resolve(targetLevel, workspace.RootPath)
                    : null;
                string targetKey = LevelCatalog.NormalizeKey(targetLevelKey);
                string donorKey = LevelCatalog.NormalizeKey(sourceLevel.Key);
                bool preferredInstanceDonor = greenWizardSupport != null &&
                    (targetKey switch
                    {
                        "wizardpeak" => donorKey.Equals("wizardpeak", StringComparison.OrdinalIgnoreCase) && moby.TrueIndex == 6,
                        "blowhard" => donorKey.Equals("blowhard", StringComparison.OrdinalIgnoreCase) && moby.TrueIndex == 0,
                        "magiccrafters" => donorKey.Equals("magiccrafters", StringComparison.OrdinalIgnoreCase) && moby.TrueIndex == 107,
                        "toasty" => donorKey.Equals("wizardpeak", StringComparison.OrdinalIgnoreCase) && moby.TrueIndex == 6,
                        _ => true
                    });
                if (greenWizardSupport != null)
                {
                    if (!preferredInstanceDonor)
                    {
                        string preferred = targetKey switch
                        {
                            "wizardpeak" => "Wizard Peak T6",
                            "blowhard" => "Blowhard T0",
                            "magiccrafters" => "Magic Crafters T107",
                            "toasty" => "Wizard Peak T6",
                            _ => "the target profile's native donor"
                        };
                        greenWizardSupport = greenWizardSupport with
                        {
                            Status = GreenWizardRuntimeBundleStatus.Unsupported,
                            Explanation = $"Unsupported donor for {targetLevel?.DisplayName ?? targetLevelKey}: use {preferred}, whose properties and route layout match this runtime-bundle profile.",
                            SwapPlaceable = false,
                            CandidateWritable = false,
                            NormalCreateBinReady = false,
                            PerInstancePropertiesAndRoute = false,
                            TrueAddPlaceable = false
                        };
                    }
                }
                bool normalizeMagicCraftersSourceRow =
                    preferredInstanceDonor &&
                    targetKey.Equals("magiccrafters", StringComparison.OrdinalIgnoreCase) &&
                    donorKey.Equals("magiccrafters", StringComparison.OrdinalIgnoreCase) &&
                    moby.TrueIndex == GreenWizardRuntimeBundleCompatibility.MagicCraftersSourceTrueIndex;
                bool mappedEnemySourceEligible = mappedTemplate != null &&
                    string.Equals(mappedTemplate.Family, "enemyTransform", StringComparison.OrdinalIgnoreCase) &&
                    moby.VisualKind == MobyVisualKind.Actor &&
                    moby.Type == 0x20 &&
                    !moby.Links.Any(MobyLinkTraversal.IsActiveMoveLink);
                bool sourceEligible = classification.Eligible || mappedEnemySourceEligible || nativeGreenWizard;
                CrossLevelTemplateLevelStatus? packageStatus = mappedTemplate == null || targetLevel == null
                    ? null
                    : CrossLevelEditorTemplateSupport.ResolveLevelStatus(
                        targetLevel,
                        mappedTemplate.SourceLevelKey,
                        mappedTemplate.Family,
                        mappedTemplate.SupportStatus,
                        workspace.RootPath);
                bool packagePlaceable = packageStatus?.Placeable == true;
                bool mappedPackageBlocked = mappedTemplate != null &&
                    string.Equals(packageStatus?.ShortLabel, "blocked here", StringComparison.OrdinalIgnoreCase);
                bool usePackageRoute = !targetActorResident && packagePlaceable;
                bool placeable = sourceEligible &&
                    (sameLevel || targetActorResident || packagePlaceable);
                bool normalCreateBinReady = sourceEligible &&
                    (sameLevel || usePackageRoute && packageStatus?.Ready == true);
                string compatibilityTier = !sourceEligible
                    ? "linked-or-uncertain"
                    : sameLevel
                    ? "same-level-native"
                    : targetActorResident
                    ? ResidentActorCompatibility
                    : mappedPackageBlocked
                    ? BlockedBehaviorCompatibility
                    : usePackageRoute
                    ? PackageBackedCompatibility
                    : targetActorRoots.Available
                    ? MissingActorPackageCompatibility
                    : "actor-root-scan-unavailable";
                string supportStatus = sameLevel && placeable
                    ? "native-slot-reuse"
                    : mappedPackageBlocked
                    ? "in-game-blocked"
                    : usePackageRoute && mappedTemplate != null
                    ? mappedTemplate.SupportStatus
                    : targetActorResident && placeable
                    ? CandidateSupportStatus
                    : sourceEligible
                    ? "requires-actor-package-and-overlay-map"
                    : "deferred-linked-or-uncertain";
                string requiredFeature = sameLevel && placeable
                    ? "CloneSourceRecordIntoSlot"
                    : mappedPackageBlocked
                    ? "TargetOverlayBehaviorTransplant"
                    : usePackageRoute && mappedTemplate != null
                    ? mappedTemplate.RequiredExporterFeature
                    : targetActorResident && placeable
                    ? CandidateExporterFeature
                    : sourceEligible
                    ? "ActorPackageAndOverlayMapping"
                    : "DependencyBundleResearch";
                string shortStatus = sameLevel && placeable
                    ? "ready in this level"
                    : mappedPackageBlocked
                    ? "blocked: missing target behavior"
                    : usePackageRoute
                    ? packageStatus?.Ready == true ? "package verified" : "package test"
                    : targetActorResident && placeable
                    ? "resident class test"
                    : sourceEligible && !targetActorRoots.Available
                    ? "compatibility unknown"
                    : sourceEligible
                    ? "needs model/behavior map"
                    : "needs linked research";
                string supportNote = sameLevel && placeable
                    ? $"Uses {sourceLevel.DisplayName}'s native donor T{moby.TrueIndex} in an existing slot."
                    : mappedPackageBlocked
                    ? packageStatus!.FullLabel
                    : usePackageRoute && mappedTemplate != null
                    ? $"{packageStatus!.FullLabel} The mapped package supplies actor 0x{actorId:X4}; target-level update behavior, animation, textures, and sounds still require DuckStation validation."
                    : targetActorResident && placeable
                    ? $"{targetLevel?.DisplayName ?? targetLevelKey} already loads actor 0x{actorId:X4} at root 0x{targetActorRoot:X}. Create Swap Test can replace one existing slot without increasing the object count; behavior and rendering still require DuckStation validation."
                    : sourceEligible && targetActorRoots.Available
                    ? $"{targetLevel?.DisplayName ?? targetLevelKey} does not load actor 0x{actorId:X4}. Copying only the 0x58-byte object row would be invisible, corrupt, inert, or crash-prone; map its actor package and target overlay behavior first."
                    : sourceEligible
                    ? "The target actor-root table could not be inspected, so the editor will not stage this cross-level row yet."
                    : classification.Reason;
                string dependencyRisk = mappedTemplate == null || string.IsNullOrWhiteSpace(mappedTemplate.DependencyRisk)
                    ? classification.Reason
                    : $"{mappedTemplate.DependencyRisk} {classification.Reason}";
                string entryId = mappedTemplate != null
                    ? mappedTemplate.Id
                    : BuildId(sourceLevel, moby, classification.Category);
                string family = mappedTemplate != null
                    ? mappedTemplate.Family
                    :
                    (classification.Category == CrossLevelSwapCategory.Chest ? "catalogChest" : "catalogEnemy");

                if (greenWizardSupport != null)
                {
                    sourceEligible = greenWizardSupport.SourceEvidencePresent;
                    placeable = greenWizardSupport.SwapPlaceable;
                    normalCreateBinReady = greenWizardSupport.NormalCreateBinReady;
                    compatibilityTier = greenWizardSupport.Status switch
                    {
                        GreenWizardRuntimeBundleStatus.Verified => GreenWizardVerifiedCompatibility,
                        GreenWizardRuntimeBundleStatus.Candidate => GreenWizardCandidateCompatibility,
                        _ => GreenWizardUnsupportedCompatibility
                    };
                    supportStatus = greenWizardSupport.Status switch
                    {
                        GreenWizardRuntimeBundleStatus.Verified => "verified-green-wizard-runtime-bundle",
                        GreenWizardRuntimeBundleStatus.Candidate => "candidate-green-wizard-runtime-bundle",
                        _ => "unsupported-green-wizard-runtime-bundle"
                    };
                    requiredFeature = greenWizardSupport.Status is
                        GreenWizardRuntimeBundleStatus.Verified or GreenWizardRuntimeBundleStatus.Candidate
                        ? CandidateExporterFeature
                        : "GreenWizardRuntimeBundleProfile";
                    shortStatus = greenWizardSupport.Status.ToString();
                    supportNote = greenWizardSupport.Explanation;
                    dependencyRisk = $"Green Wizard is a dependency-bundle enemy, not a row-only actor. {greenWizardSupport.Explanation}";
                    family = "enemyTransform";
                }

                entries.Add(new CrossLevelSwapCatalogEntry(
                    Id: entryId,
                    DisplayName: BuildDisplayName(moby, classification.Category),
                    Category: classification.Category,
                    Family: family,
                    SourceLevelKey: sourceLevel.Key,
                    SourceLevelName: sourceLevel.DisplayName,
                    SourceTrueIndex: moby.TrueIndex,
                    Type: moby.Type,
                    State: normalizeMagicCraftersSourceRow ? 0x00 : moby.State,
                    SourceByte36: moby.SourceByte36,
                    SourceByte37: moby.SourceByte37,
                    SourceByte4F: normalizeMagicCraftersSourceRow ? 0x00 : moby.SourceByte4F,
                    Flag4A: normalizeMagicCraftersSourceRow ? 0x10 : moby.Flag4A,
                    Flag4B: moby.Flag4B,
                    YawByte: moby.YawByte >= 0 ? moby.YawByte : 0,
                    SpecialDataPointer: moby.SpecialDataPointer,
                    SameLevel: sameLevel,
                    Eligible: sourceEligible,
                    Placeable: placeable,
                    NormalCreateBinReady: normalCreateBinReady,
                    ActorId: actorId,
                    TargetActorRootPresent: targetActorResident,
                    TargetActorRoot: targetActorResident ? $"0x{targetActorRoot:X}" : "",
                    CompatibilityTier: compatibilityTier,
                    PackageRecipeId: greenWizardSupport?.RecipeId ?? packageStatus?.RecipeId ?? "",
                    SupportStatus: supportStatus,
                    RequiredExporterFeature: requiredFeature,
                    ShortStatus: shortStatus,
                    SupportNote: supportNote,
                    DependencyRisk: dependencyRisk,
                    CandidateKind: mappedTemplate == null || string.IsNullOrWhiteSpace(mappedTemplate.CandidateKind)
                        ? moby.CandidateKind
                        : mappedTemplate.CandidateKind,
                    Confidence: moby.Confidence));
            }
        }

        return new CrossLevelSwapCatalog(
            GeneratedAt: DateTimeOffset.Now,
            TargetLevelKey: targetLevelKey,
            EntryCount: entries.Count,
            ReadyCount: entries.Count(entry => entry.NormalCreateBinReady),
            CandidateCount: entries.Count(entry => entry.Placeable && !entry.NormalCreateBinReady),
            ResidentCandidateCount: entries.Count(entry =>
                entry.Placeable &&
                entry.CompatibilityTier is
                    ResidentActorCompatibility or
                    GreenWizardCandidateCompatibility or
                    GreenWizardVerifiedCompatibility),
            PackageCandidateCount: entries.Count(entry => entry.CompatibilityTier == PackageBackedCompatibility && entry.Placeable),
            UnmappedCount: entries.Count(entry => entry.CompatibilityTier is MissingActorPackageCompatibility or "actor-root-scan-unavailable"),
            DeferredCount: entries.Count(entry => !entry.Placeable),
            Entries: entries
                .OrderBy(entry => entry.Category)
                .ThenByDescending(entry => entry.SameLevel && entry.Eligible)
                .ThenByDescending(entry => entry.Placeable)
                .ThenBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(entry => entry.SourceLevelName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(entry => entry.SourceTrueIndex)
                .ToArray());
    }

    public static async Task WriteAsync(
        CrossLevelSwapCatalog catalog,
        string jsonPath,
        string markdownPath,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(jsonPath) ?? ".");
        await File.WriteAllTextAsync(
            jsonPath,
            JsonSerializer.Serialize(catalog, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);
        await File.WriteAllTextAsync(markdownPath, BuildMarkdown(catalog), Encoding.UTF8, cancellationToken);
    }

    private static SwapSourceClassification Classify(Moby moby)
    {
        string text = $"{moby.DisplayLabel} {moby.CandidateKind} {moby.BehaviorNote}".ToLowerInvariant();
        bool supportedSourceType = moby.Type is 0x18 or 0x20;
        bool uncertain = ContainsAny(text,
            "needs id",
            "placeholder",
            "unknown actor",
            "object candidate",
            "scene/route",
            "control marker",
            "family candidate");

        if (moby.VisualKind == MobyVisualKind.Chest)
        {
            bool specialOrLinkedFamily = ContainsAny(text,
                "spring chest",
                "firework chest",
                "3x flame",
                "super flame",
                "multi-gem",
                "multi gem",
                "life chest",
                "key chest",
                "locked chest",
                "unlock chest",
                "chest link",
                "chest-linked",
                "linked record");
            bool nonRewardLink = moby.Links.Any(link =>
                MobyLinkTraversal.IsActiveMoveLink(link) &&
                !string.Equals(link.Kind, "chest contents", StringComparison.OrdinalIgnoreCase));
            bool eligible = supportedSourceType && !uncertain && !specialOrLinkedFamily && !nonRewardLink;
            string reason = eligible
                ? "Ordinary chest shell. Existing-slot candidate preserves the target slot's placement and chest behavior data; cross-level visuals and interaction still require an in-game test."
                : !supportedSourceType
                ? $"Chest source type 0x{moby.Type:X2} is outside the existing-slot candidate writer."
                : uncertain
                ? "Identity is not strong enough for a user-facing swap yet."
                : specialOrLinkedFamily
                ? "Special chest family has linked controller, reward, or lifecycle dependencies and stays deferred for a later dependency-bundle pass."
                : "Chest has non-reward behavior links and stays deferred until those companions can move with the swap.";
            return new SwapSourceClassification(true, CrossLevelSwapCategory.Chest, eligible, reason);
        }

        if (moby.VisualKind != MobyVisualKind.Actor)
            return SwapSourceClassification.Excluded;

        bool passiveOrScripted = ContainsAny(text,
            "fodder",
            "sheep",
            "chicken",
            "fairy",
            "balloonist",
            "thief",
            "boss",
            "dragon",
            "flight target",
            "cutscene");
        bool linked = moby.Links.Any(MobyLinkTraversal.IsActiveMoveLink);
        bool hasPerInstanceRuntimeData = moby.SpecialDataPointer != 0;
        string candidateKind = moby.CandidateKind.Trim().ToLowerInvariant();
        string confidence = moby.Confidence.Trim().ToLowerInvariant();
        bool explicitEnemyIdentity = candidateKind == "enemy" || candidateKind.EndsWith(" enemy", StringComparison.Ordinal);
        bool trustedEnemyEvidence = confidence is
            "guide-roster-count" or
            "observed-fingerprint" or
            "source-signature-observed" or
            "live-observed" or
            "user-observed" or
            "user override" or
            "user-override";
        bool suspiciousActorFamily = ContainsAny(text,
            "control enemy",
            "linked record",
            "structural scenery",
            "treasure-route prop",
            "actor/reward family");
        bool enemyEligible = moby.Type == 0x20 &&
            !uncertain &&
            !passiveOrScripted &&
            !linked &&
            !hasPerInstanceRuntimeData &&
            explicitEnemyIdentity &&
            trustedEnemyEvidence &&
            !suspiciousActorFamily;
        string enemyReason = enemyEligible
            ? "No linked companion or runtime special-data pointer was found. The actor package is still level-local, so this remains a disposable existing-slot candidate until tested."
            : moby.Type != 0x20
            ? $"Actor source type 0x{moby.Type:X2} is outside the self-contained enemy candidate writer."
            : uncertain
            ? "Enemy identity is not strong enough for a user-facing swap yet."
            : passiveOrScripted
            ? "Passive, scripted, boss, thief, dragon, or flight actor is outside the self-contained enemy slice."
            : linked
            ? "Enemy has linked route, helper, reward, or companion records and needs a dependency bundle."
            : hasPerInstanceRuntimeData
            ? "Enemy uses per-instance runtime special data and needs a dependency-aware import."
            : !explicitEnemyIdentity
            ? "Actor is not explicitly identified as an enemy, so it stays out of the self-contained catalogue."
            : !trustedEnemyEvidence
            ? "Enemy identity comes from a provisional family or position inference and needs stronger evidence."
            : "Enemy label still indicates a linked, control, scenery, route, or reward-family dependency.";
        return new SwapSourceClassification(true, CrossLevelSwapCategory.Enemy, enemyEligible, enemyReason);
    }

    private static bool ContainsAny(string text, params string[] values) =>
        values.Any(value => text.Contains(value, StringComparison.Ordinal));

    private static TargetActorRootMap ReadTargetActorRoots(EditorWorkspace workspace, LevelDefinition? targetLevel)
    {
        if (targetLevel == null || !targetLevel.HasSourceTable || string.IsNullOrWhiteSpace(targetLevel.SourceTableRelativeOffset))
            return TargetActorRootMap.Unavailable;

        try
        {
            string sourceImage = DiscImageLocator.FindImage(workspace);
            if (!File.Exists(sourceImage))
                return TargetActorRootMap.Unavailable;

            DiscLayout layout = DiscImage.DetectLayout(sourceImage);
            using FileStream stream = File.OpenRead(sourceImage);
            long tableWadOffset = ParseNumber(targetLevel.SourceTableWadOffset);
            long tableRelativeOffset = ParseNumber(targetLevel.SourceTableRelativeOffset);
            long entryBase = tableWadOffset - tableRelativeOffset;
            Dictionary<int, uint> roots = [];
            for (int index = 0; index < 64; index++)
            {
                ushort actorId = BitConverter.ToUInt16(
                    DiscImage.ReadFileBytes(stream, layout, 37, entryBase + 0x150 + (index * 2L), 2),
                    0);
                uint root = BitConverter.ToUInt32(
                    DiscImage.ReadFileBytes(stream, layout, 37, entryBase + 0x50 + (index * 4L), 4),
                    0);
                if (root != 0)
                    roots.TryAdd(actorId, root);
            }

            return new TargetActorRootMap(true, roots);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or InvalidOperationException or FormatException or OverflowException or UnauthorizedAccessException)
        {
            return TargetActorRootMap.Unavailable;
        }
    }

    private static IReadOnlyList<MappedCrossLevelTemplate> LoadMappedTemplates(EditorWorkspace workspace)
    {
        string path = workspace.ResolveFile("spyro-object-templates.json");
        if (!File.Exists(path))
            return [];

        try
        {
            using FileStream stream = File.OpenRead(path);
            using JsonDocument document = JsonDocument.Parse(stream);
            if (!document.RootElement.TryGetProperty("templates", out JsonElement templates) ||
                templates.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            List<MappedCrossLevelTemplate> result = [];
            foreach (JsonElement template in templates.EnumerateArray())
            {
                int sourceByte36 = GetJsonInt32(template, "sourceByte36Hex", -1);
                int sourceByte37 = GetJsonInt32(template, "sourceByte37Hex", -1);
                int sourceTrueIndex = GetJsonInt32(template, "sourceTrueIndex", -1);
                if (sourceByte36 < 0 || sourceByte37 < 0 || sourceTrueIndex < 0)
                    continue;

                result.Add(new MappedCrossLevelTemplate(
                    Id: GetJsonString(template, "id"),
                    Family: GetJsonString(template, "family"),
                    SourceLevelKey: GetJsonString(template, "sourceLevelKey"),
                    SourceTrueIndex: sourceTrueIndex,
                    ActorId: sourceByte36 | (sourceByte37 << 8),
                    SupportStatus: GetJsonString(template, "addSupportStatus"),
                    RequiredExporterFeature: GetJsonString(template, "requiredExporterFeature"),
                    DependencyRisk: GetJsonString(template, "dependencyRisk"),
                    CandidateKind: GetJsonString(template, "kind")));
            }

            return result;
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException or InvalidOperationException)
        {
            return [];
        }
    }

    private static bool IsActorPackageTemplate(MappedCrossLevelTemplate template) =>
        template.SupportStatus.Contains("actor-package", StringComparison.OrdinalIgnoreCase) ||
        template.RequiredExporterFeature.Contains("ActorPackage", StringComparison.OrdinalIgnoreCase);

    private static bool IsGreenWizardTemplate(MappedCrossLevelTemplate template) =>
        template.ActorId == GreenWizardRuntimeBundleCompatibility.ActorId &&
        string.Equals(template.Family, "enemyTransform", StringComparison.OrdinalIgnoreCase) &&
        (string.Equals(template.Id, GreenWizardRuntimeBundleCompatibility.TemplateId, StringComparison.OrdinalIgnoreCase) ||
         string.Equals(template.Id, GreenWizardRuntimeBundleCompatibility.BlowhardTemplateId, StringComparison.OrdinalIgnoreCase) ||
         string.Equals(template.Id, GreenWizardRuntimeBundleCompatibility.MagicCraftersTemplateId, StringComparison.OrdinalIgnoreCase));

    private static string GetJsonString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement value))
            return "";
        return value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : value.ToString();
    }

    private static int GetJsonInt32(JsonElement element, string propertyName, int fallback)
    {
        string text = GetJsonString(element, propertyName).Trim();
        if (string.IsNullOrWhiteSpace(text))
            return element.TryGetProperty(propertyName, out JsonElement value) && value.TryGetInt32(out int number)
                ? number
                : fallback;
        try
        {
            return checked((int)ParseNumber(text));
        }
        catch (Exception ex) when (ex is FormatException or OverflowException)
        {
            return fallback;
        }
    }

    private static long ParseNumber(string value)
    {
        string text = (value ?? "").Trim();
        return text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? long.Parse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture)
            : long.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture);
    }

    private static string BuildSignature(Moby moby, CrossLevelSwapCategory category) =>
        $"{category}:{moby.Type:X2}:{moby.State:X2}:{moby.SourceByte36:X2}:{moby.SourceByte37:X2}:{moby.SourceByte4F:X2}:{moby.Flag4A:X2}:{moby.Flag4B:X2}:{BuildDisplayName(moby, category)}";

    private static string BuildId(LevelDefinition level, Moby moby, CrossLevelSwapCategory category) =>
        $"catalog.{LevelCatalog.NormalizeKey(level.Key)}.{category.ToString().ToLowerInvariant()}.{moby.SourceByte37:X2}{moby.SourceByte36:X2}.t{moby.TrueIndex}";

    private static string CleanDisplayName(string value)
    {
        string label = string.IsNullOrWhiteSpace(value) ? "Unnamed object" : value.Trim();
        return label.StartsWith("New ", StringComparison.OrdinalIgnoreCase) ? label[4..] : label;
    }

    private static string BuildDisplayName(Moby moby, CrossLevelSwapCategory category)
    {
        string label = CleanDisplayName(moby.DisplayLabel);
        if ((category == CrossLevelSwapCategory.Chest || category == CrossLevelSwapCategory.Enemy) &&
            moby.RewardGem != GemValue.Unknown &&
            !label.Contains("reward", StringComparison.OrdinalIgnoreCase))
        {
            string color = moby.RewardGem.Name.Replace(" gem", "", StringComparison.OrdinalIgnoreCase);
            label += $" ({color} reward)";
        }

        return label;
    }

    private readonly record struct SwapSourceClassification(
        bool Include,
        CrossLevelSwapCategory Category,
        bool Eligible,
        string Reason)
    {
        public static SwapSourceClassification Excluded { get; } = new(false, default, false, "");
    }

    private sealed record MappedCrossLevelTemplate(
        string Id,
        string Family,
        string SourceLevelKey,
        int SourceTrueIndex,
        int ActorId,
        string SupportStatus,
        string RequiredExporterFeature,
        string DependencyRisk,
        string CandidateKind);

    private sealed record TargetActorRootMap(bool Available, IReadOnlyDictionary<int, uint> Roots)
    {
        public static TargetActorRootMap Unavailable { get; } = new(false, new Dictionary<int, uint>());

        public bool TryGet(int actorId, out uint root) => Roots.TryGetValue(actorId, out root);
    }

    private static string BuildMarkdown(CrossLevelSwapCatalog catalog)
    {
        StringBuilder builder = new();
        builder.AppendLine("# Cross-Level Chest And Enemy Swap Catalogue");
        builder.AppendLine();
        builder.AppendLine($"Target level: `{catalog.TargetLevelKey}`");
        builder.AppendLine();
        builder.AppendLine($"- Entries: {catalog.EntryCount}");
        builder.AppendLine($"- Same-level ready: {catalog.ReadyCount}");
        builder.AppendLine($"- Cross-level guarded candidates: {catalog.CandidateCount}");
        builder.AppendLine($"- Resident actor-root candidates: {catalog.ResidentCandidateCount}");
        builder.AppendLine($"- Package-backed candidates: {catalog.PackageCandidateCount}");
        builder.AppendLine($"- Missing package/scan mappings: {catalog.UnmappedCount}");
        builder.AppendLine($"- Deferred total: {catalog.DeferredCount}");
        builder.AppendLine();
        builder.AppendLine("Normal Create BIN never writes an unverified cross-level candidate from this catalogue. Candidate mode either reuses a target-resident actor root or uses an explicitly mapped actor-package recipe, preserves the source object count, and still requires DuckStation behavior validation.");
        builder.AppendLine();
        builder.AppendLine("| Object | Category | Source | Donor | Actor | Compatibility | Status | Reason |");
        builder.AppendLine("|---|---|---|---:|---:|---|---|---|");
        foreach (CrossLevelSwapCatalogEntry entry in catalog.Entries)
        {
            builder.AppendLine($"| {Escape(entry.DisplayName)} | {entry.Category} | {Escape(entry.SourceLevelName)} | T{entry.SourceTrueIndex} | `0x{entry.ActorId:X4}` | {Escape(entry.CompatibilityTier)} | {Escape(entry.ShortStatus)} | {Escape(entry.DependencyRisk)} |");
        }
        return builder.ToString();
    }

    private static string Escape(string value) =>
        (value ?? "").Replace("|", "\\|", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
}

public enum CrossLevelSwapCategory
{
    Chest,
    Enemy
}

public sealed record CrossLevelSwapCatalog(
    DateTimeOffset GeneratedAt,
    string TargetLevelKey,
    int EntryCount,
    int ReadyCount,
    int CandidateCount,
    int ResidentCandidateCount,
    int PackageCandidateCount,
    int UnmappedCount,
    int DeferredCount,
    IReadOnlyList<CrossLevelSwapCatalogEntry> Entries);

public sealed record CrossLevelSwapCatalogEntry(
    string Id,
    string DisplayName,
    CrossLevelSwapCategory Category,
    string Family,
    string SourceLevelKey,
    string SourceLevelName,
    int SourceTrueIndex,
    int Type,
    int State,
    int SourceByte36,
    int SourceByte37,
    int SourceByte4F,
    int Flag4A,
    int Flag4B,
    int YawByte,
    uint SpecialDataPointer,
    bool SameLevel,
    bool Eligible,
    bool Placeable,
    bool NormalCreateBinReady,
    int ActorId,
    bool TargetActorRootPresent,
    string TargetActorRoot,
    string CompatibilityTier,
    string PackageRecipeId,
    string SupportStatus,
    string RequiredExporterFeature,
    string ShortStatus,
    string SupportNote,
    string DependencyRisk,
    string CandidateKind,
    string Confidence)
{
    public string SearchText => $"{DisplayName} {Category} {SourceLevelName} T{SourceTrueIndex} 0x{ActorId:X4} {CompatibilityTier} {PackageRecipeId} {ShortStatus} {CandidateKind} {Confidence}";
    public string ListText => $"{DisplayName} | {SourceLevelName} T{SourceTrueIndex} | {ShortStatus}";
}
