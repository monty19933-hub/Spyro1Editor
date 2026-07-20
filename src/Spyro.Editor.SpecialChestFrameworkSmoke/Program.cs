using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;

IReadOnlyList<SpecialChestBundleProfile> profiles = SpecialChestBundleProfileRegistry.AllProfiles;
Expect(SpecialChestBundleProfileRegistry.RegularLevelKeys.Count == 30, "The registry must cover exactly 30 non-flight levels.");
Expect(SpecialChestBundleProfileRegistry.Families.Count == 6, "The registry must cover exactly six requested special-chest families.");
Expect(profiles.Count == 180, "The registry must contain one profile per family/non-flight-level pair.");
Expect(profiles.Select(profile => profile.Key).Distinct().Count() == 180, "Every special-chest profile key must be unique.");
Expect(!profiles.Any(profile => profile.TargetLevelKey.Contains("flight", StringComparison.OrdinalIgnoreCase)), "Flight levels must remain explicit first-pass exclusions.");

Dictionary<SpecialChestFamily, int> expectedNativeCounts = new()
{
    [SpecialChestFamily.LockedChest] = 13,
    [SpecialChestFamily.LifeChest] = 22,
    [SpecialChestFamily.SpringChest] = 24,
    [SpecialChestFamily.FireworkChest] = 13,
    [SpecialChestFamily.MultiGemChest] = 13,
    [SpecialChestFamily.ArmoredChest] = 11
};
foreach ((SpecialChestFamily family, int expected) in expectedNativeCounts)
{
    IReadOnlyList<SpecialChestBundleProfile> familyProfiles = SpecialChestBundleProfileRegistry.ForFamily(family);
    Expect(familyProfiles.Count == 30, $"{family} must cover all 30 regular levels.");
    Expect(familyProfiles.Count(profile => profile.NativeFamilyPresent) == expected, $"{family} native coverage changed.");
}

Expect(profiles.Count(profile => profile.Availability == SpecialChestBundleAvailability.NativeClosurePresent) == 96, "The native-closure census changed.");
Expect(profiles.Count(profile => profile.Availability == SpecialChestBundleAvailability.CandidatePlanOnly) == 16, "The target-profile candidate census changed.");
Expect(profiles.Count(profile => profile.Availability == SpecialChestBundleAvailability.Blocked) == 67, "The blocked dependency-closure census changed.");
Expect(profiles.Count(profile => profile.Availability == SpecialChestBundleAvailability.NormalCreateBinReady) == 1, "Only one transplant may be normal-build ready.");
Expect(profiles.Count(profile => profile.Evidence == SpecialChestBundleEvidenceKind.RuntimeRejected) == 2, "The Artisans and Stone Hill spring experiments must remain explicit rejected evidence.");
Expect(profiles.All(profile => profile.Declaration.Rows.Count > 0), "Every profile must declare visible/donor and hidden row requirements.");
Expect(profiles.All(profile => profile.Declaration.ActorPackages.Count > 0 && profile.Declaration.RootRegistrations.Count > 0), "Every profile must declare actor packages and root registrations.");
Expect(profiles.All(profile => profile.Declaration.Code.Count > 0 && profile.Declaration.PrivateAllocations.Count > 0), "Every profile must declare handler/rebase and private allocation requirements.");
Expect(profiles.All(profile => profile.Declaration.Resources.Count > 0 && profile.Declaration.Incompatibilities.Count > 0), "Every profile must declare resources and incompatibilities.");
Expect(!profiles.Where(profile => !profile.NormalCreateBinReady).Any(profile =>
    profile.Declaration.ComponentEvidence().Any(evidence => evidence == SpecialChestComponentEvidence.RuntimeProven)),
    "A destination profile must not claim runtime-proven components before promotion.");

SpecialChestBundleProfile artisansLocked = RequireProfile(SpecialChestFamily.LockedChest, "Artisans");
Expect(artisansLocked.NormalCreateBinReady, "The proven Artisans locked-chest bundle must be normal-build ready.");
Expect(artisansLocked.Evidence == SpecialChestBundleEvidenceKind.RuntimeProven, "The Artisans profile must carry runtime-proven evidence.");
Expect(artisansLocked.RecipeId == ArtisansNativeLockedChestRuntimeBundleComposer.RecipeId, "The profile must point at the preserved hardcoded composer recipe.");
Expect(artisansLocked.RequiredExporterFeature == ArtisansNativeLockedChestRuntimeBundleComposer.RequiredExporterFeature, "The profile must point at the preserved hardcoded composer feature.");
Expect(artisansLocked.RuntimeProofOutputSha256 == SpecialChestBundleProfileRegistry.ArtisansLockedChestProofOutputSha256, "The Artisans proof digest changed.");
Expect(artisansLocked.StructuralBudget.Exact && artisansLocked.StructuralBudget.SourceRowsAdded == 7, "The proven profile must reserve exactly seven source rows.");
Expect(artisansLocked.StructuralBudget.RuntimeSlotsConsumed == 6 && artisansLocked.StructuralBudget.TreasureDelta == 10, "The proven profile must retain the six-slot/+10 reward budget.");
Expect(artisansLocked.StructuralBudget.WadGrowthBytes == 0x2000 && artisansLocked.StructuralBudget.RelocatesExecutable, "The proven profile must retain its checked structural relocation.");
Expect(artisansLocked.Declaration.ComponentEvidence().All(evidence => evidence == SpecialChestComponentEvidence.RuntimeProven), "Every component of the release-ready Artisans bundle must be runtime-proven.");
Expect(artisansLocked.Declaration.Rows.Count == 7 && artisansLocked.Declaration.Rows.Count(row => row.Hidden) == 5, "The Artisans profile must declare two visible rows plus five hidden reward markers.");
Expect(artisansLocked.Declaration.Rows.Select(row => row.TargetTrueIndex).SequenceEqual(Enumerable.Range(174, 7).Select(value => (int?)value)), "The Artisans row declaration must own exactly T174-T180.");
Expect(new ushort[] { 0x00AE, 0x0135, 0x0136, 0x0137 }.All(actorId => artisansLocked.Declaration.ActorPackages.Any(package => package.ActorId == actorId)), "The proven actor-package declaration lost the locked chest or debris closure.");
Expect(artisansLocked.Declaration.Code.All(code => code.SharedCalls.Count > 0 && code.RebaseSites.Count > 0), "The proven handler declaration must retain shared-call and rebase responsibilities.");
Expect(artisansLocked.Declaration.PrivateAllocations.Any(allocation => allocation.Kind == SpecialChestPrivateAllocationKind.SourceRows && allocation.SizeBytes == 7 * 0x58), "The proven profile must declare its exact source-row allocation.");
Expect(artisansLocked.Declaration.PrivateAllocations.Any(allocation => allocation.Kind == SpecialChestPrivateAllocationKind.ExecutableCode && allocation.SizeBytes == 0x400), "The proven profile must declare its checked executable cave.");
Expect(artisansLocked.Declaration.Resources.Any(resource => resource.Kind == SpecialChestResourceKind.TexturePixels) && artisansLocked.Declaration.Resources.Any(resource => resource.Kind == SpecialChestResourceKind.PaletteClut), "The proven profile must declare texture and palette resources separately.");
Expect(artisansLocked.Declaration.Reward.TreasureDelta == 10 && artisansLocked.Declaration.Reward.LifeDelta == 0 && artisansLocked.Declaration.Reward.OneShotRequired, "The proven profile must declare the exact one-shot +10 reward.");

SpecialChestBundleProfile normalizedLookup = SpecialChestBundleProfileRegistry.Find(
    SpecialChestFamily.LockedChest,
    "Town Square",
    SpecialChestBundleProfileRegistry.CleanUsaImageSha256.ToUpperInvariant()) ??
    throw new InvalidOperationException("Normalized family/target/disc lookup failed.");
Expect(normalizedLookup.TargetLevelKey == "townsquare", "Target lookup did not normalize the level key.");
Expect(normalizedLookup.CandidatePlanOnly && !normalizedLookup.NormalCreateBinReady, "Town Square locked chest must remain target-profile plan-only.");
Expect(normalizedLookup.Declaration.ComponentEvidence().Any(evidence => evidence == SpecialChestComponentEvidence.Unmapped), "Town Square must retain explicit unmapped target components.");

SpecialChestStructuralPlan artisansPlan = SpecialChestStructuralPlanner.Plan(new(
    SpecialChestFamily.LockedChest,
    "artisans",
    SpecialChestBundleProfileRegistry.CleanUsaImageSha256,
    RequestedBundleInstances: 1,
    ExistingBundleInstances: 0,
    ExistingSourceRecordCount: 174));
Expect(artisansPlan.Status == SpecialChestStructuralPlanStatus.Ready && artisansPlan.CanWriteNormalBuild, "The exact proven Artisans pair should produce a normal-build-ready structural plan.");
Expect(!artisansPlan.CanStageResearchCandidate, "A promoted profile must not be mislabeled as a research candidate.");
Expect(artisansPlan.ProjectedSourceRecordCount == 181, "The Artisans plan must project T174-T180.");
Expect(artisansPlan.ProjectedTreasureDelta == 10 && artisansPlan.ProjectedLifeDelta == 0 && artisansPlan.ProjectedWadGrowthBytes == 0x2000, "The Artisans plan reward/growth budget changed.");

AssertBlocked(
    new(
        SpecialChestFamily.LockedChest,
        "artisans",
        SpecialChestBundleProfileRegistry.CleanUsaImageSha256,
        RequestedBundleInstances: 2,
        ExistingSourceRecordCount: 174),
    "bundle-instance-limit");
AssertBlocked(
    new(
        SpecialChestFamily.LockedChest,
        "artisans",
        SpecialChestBundleProfileRegistry.CleanUsaImageSha256,
        ExistingSourceRecordCount: 174,
        HasOtherObjectEdits: true),
    "object-edit-allocation-conflict");
AssertBlocked(
    new(
        SpecialChestFamily.LockedChest,
        "artisans",
        SpecialChestBundleProfileRegistry.CleanUsaImageSha256,
        ExistingSourceRecordCount: 174,
        HasOtherStructuralRelocations: true),
    "structural-relocation-conflict");

SpecialChestStructuralPlan normalCandidateGate = SpecialChestStructuralPlanner.Plan(new(
    SpecialChestFamily.LockedChest,
    "townsquare",
    SpecialChestBundleProfileRegistry.CleanUsaImageSha256,
    ExistingSourceRecordCount: 107));
Expect(normalCandidateGate.Blocked && !normalCandidateGate.CanWriteNormalBuild, "An unresolved target profile must be blocked from normal Create BIN.");
Expect(normalCandidateGate.Issues.Any(issue => issue.Code == "normal-build-runtime-proof-required"), "The normal-build candidate gate is missing.");
Expect(normalCandidateGate.Issues.Any(issue => issue.Code == "declarative-closure-unmapped"), "Candidate plans must surface unmapped declarative bundle components.");

SpecialChestStructuralPlan researchCandidate = SpecialChestStructuralPlanner.Plan(new(
    SpecialChestFamily.LockedChest,
    "townsquare",
    SpecialChestBundleProfileRegistry.CleanUsaImageSha256,
    ExistingSourceRecordCount: 107,
    AllowResearchCandidate: true));
Expect(researchCandidate.Status == SpecialChestStructuralPlanStatus.Review, "An opted-in candidate plan must remain Review, not Ready.");
Expect(researchCandidate.CanStageResearchCandidate && !researchCandidate.CanWriteNormalBuild, "Research opt-in must never promote normal Create BIN.");
Expect(researchCandidate.Issues.Any(issue => issue.Code == "structural-budget-unmapped"), "Candidate plans must expose their unmapped structural budget.");

SpecialChestStructuralPlan blockedLife = SpecialChestStructuralPlanner.Plan(new(
    SpecialChestFamily.LifeChest,
    "toasty",
    SpecialChestBundleProfileRegistry.CleanUsaImageSha256,
    ExistingSourceRecordCount: 59,
    AllowResearchCandidate: true));
Expect(blockedLife.Blocked && !blockedLife.CanStageResearchCandidate, "A missing Life Chest dependency closure must remain blocked even with research opt-in.");
Expect(blockedLife.Issues.Any(issue => issue.Code == "dependency-closure-incomplete"), "Blocked Life Chest plans must name the dependency-closure blocker.");

SpecialChestStructuralPlan nativeLife = SpecialChestStructuralPlanner.Plan(new(
    SpecialChestFamily.LifeChest,
    "artisans",
    SpecialChestBundleProfileRegistry.CleanUsaImageSha256,
    ExistingSourceRecordCount: 174,
    AllowResearchCandidate: true));
Expect(nativeLife.Status == SpecialChestStructuralPlanStatus.Review && nativeLife.CanStageResearchCandidate, "Native family presence may support research planning but must still require instance proof.");
Expect(!nativeLife.CanWriteNormalBuild, "Native retail presence must not falsely prove arbitrary adds.");
Expect(nativeLife.ProjectedLifeDelta == 1, "The native Life Chest declaration must retain its +1 life contract without implying build readiness.");

AssertBlocked(
    new(
        SpecialChestFamily.LockedChest,
        "artisans",
        new string('0', 64),
        ExistingSourceRecordCount: 174),
    "unsupported-disc-or-level-profile");

await AssertBundleMetadataRoundTrip(artisansLocked);
await AssertLegacyProjectLeavesBundleMetadataEmpty();
AssertBlocked(
    new(
        SpecialChestFamily.LockedChest,
        "sunnyflight",
        SpecialChestBundleProfileRegistry.CleanUsaImageSha256,
        ExistingSourceRecordCount: 45),
    "unsupported-disc-or-level-profile");

Console.WriteLine("Special-chest framework smoke passed.");
Console.WriteLine("- 180 exact USA family/regular-level profiles (96 native, 16 plan-only, 67 blocked, 1 runtime-proven)");
Console.WriteLine("- Artisans Key + Locked Chest V2 remains the sole normal-build-ready transplant");
Console.WriteLine("- disc/profile keys, exact structural budgets, atomic conflicts, candidate gates, and native-instance proof boundaries passed");
return 0;

SpecialChestBundleProfile RequireProfile(SpecialChestFamily family, string targetLevelKey) =>
    SpecialChestBundleProfileRegistry.Find(
        family,
        targetLevelKey,
        SpecialChestBundleProfileRegistry.CleanUsaImageSha256) ??
    throw new InvalidOperationException($"Missing {family}/{targetLevelKey} profile.");

void AssertBlocked(SpecialChestStructuralPlanningRequest request, string issueCode)
{
    SpecialChestStructuralPlan plan = SpecialChestStructuralPlanner.Plan(request);
    Expect(plan.Blocked && !plan.CanWriteNormalBuild && !plan.CanStageResearchCandidate, $"Expected blocked plan for issue '{issueCode}'.");
    Expect(plan.Issues.Any(issue => issue.Code == issueCode && issue.Severity == SpecialChestSafetySeverity.Block), $"Blocked plan did not report '{issueCode}'.");
}

void Expect(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

async Task AssertBundleMetadataRoundTrip(SpecialChestBundleProfile profile)
{
    string directory = Path.Combine(Path.GetTempPath(), $"spyro-special-chest-metadata-{Guid.NewGuid():N}");
    string path = Path.Combine(directory, "artisans-native-edits.json");
    Directory.CreateDirectory(directory);
    try
    {
        const string bundleId = "special-chest:lockedchest:artisans:T175";
        Moby chest = NewBundleMoby(0, 175, 0xAE, 175, [176]);
        Moby key = NewBundleMoby(1, 176, 0xAD, 175, [176]);
        int saved = await MobyEditStore.SaveAsync(path, [chest, key], "Artisans");
        Expect(saved == 2, "The atomic Key + Locked Chest metadata smoke did not save both rows.");

        List<Moby> loaded = [];
        int applied = MobyEditStore.Load(path, loaded);
        Expect(applied == 2 && loaded.Count == 2, "The atomic Key + Locked Chest metadata smoke did not reload both rows.");
        foreach (Moby row in loaded)
        {
            Expect(row.SpecialChestBundleId == bundleId, "specialChestBundleId did not round-trip.");
            Expect(row.SpecialChestProfileId == profile.Id, "profileId did not round-trip.");
            Expect(row.SpecialChestVisibleRootTrueIndex == 175, "The visible-root reference did not round-trip.");
            Expect(row.SpecialChestHiddenCompanionTrueIndexes.SequenceEqual([176]), "Hidden companion references did not round-trip.");
            Expect(row.SpecialChestCapacity == 1, "The bundle capacity did not round-trip.");
            Expect(row.SpecialChestEvidenceStatus == SpecialChestBundleEvidenceKind.RuntimeProven.ToString(), "The evidence status did not round-trip.");
        }

        loaded[0].UndoEdits();
        Expect(string.IsNullOrEmpty(loaded[0].SpecialChestBundleId) && string.IsNullOrEmpty(loaded[0].SpecialChestProfileId), "Undo must clear bundle/profile metadata.");
        Expect(loaded[0].SpecialChestVisibleRootTrueIndex == -1 && loaded[0].SpecialChestHiddenCompanionTrueIndexes.Count == 0, "Undo must clear bundle ownership references.");
        Expect(loaded[0].SpecialChestCapacity == 0 && string.IsNullOrEmpty(loaded[0].SpecialChestEvidenceStatus), "Undo must clear capacity/evidence metadata.");

        Moby NewBundleMoby(int index, int trueIndex, int actorLow, int rootTrueIndex, int[] hidden)
        {
            Moby moby = new()
            {
                Index = index,
                TrueIndex = trueIndex,
                LegacyIndex = trueIndex,
                Position = new Vector3f(1, 2, 3),
                OriginalPosition = new Vector3f(1, 2, 3),
                Type = actorLow == 0xAD ? 0x18 : 0x20,
                OriginalType = actorLow == 0xAD ? 0x18 : 0x20,
                State = 0,
                OriginalState = 0,
                SourceByte36 = actorLow,
                OriginalSourceByte36 = actorLow,
                SourceByte37 = 0,
                OriginalSourceByte37 = 0,
                SourceByte4F = actorLow == 0xAD ? 2 : 0,
                OriginalSourceByte4F = actorLow == 0xAD ? 2 : 0,
                Flag4A = actorLow == 0xAD ? 0x40 : 0x10,
                OriginalFlag4A = actorLow == 0xAD ? 0x40 : 0x10,
                Flag4B = actorLow == 0xAD ? 0xFF : 0x51,
                OriginalFlag4B = actorLow == 0xAD ? 0xFF : 0x51,
                Label = actorLow == 0xAD ? "Key" : "Locked Chest",
                OriginalLabel = actorLow == 0xAD ? "Key" : "Locked Chest",
                CrossLevelTemplateId = actorLow == 0xAD
                    ? ArtisansNativeLockedChestRuntimeBundleCompatibility.KeyTemplateId
                    : ArtisansNativeLockedChestRuntimeBundleCompatibility.LockedChestTemplateId,
                CrossLevelFamily = actorLow == 0xAD ? "key" : "lockedChest",
                SpecialChestBundleId = bundleId,
                SpecialChestProfileId = profile.Id,
                SpecialChestVisibleRootTrueIndex = rootTrueIndex,
                SpecialChestCapacity = 1,
                SpecialChestEvidenceStatus = SpecialChestBundleEvidenceKind.RuntimeProven.ToString(),
                IsAdded = true
            };
            moby.SpecialChestHiddenCompanionTrueIndexes.AddRange(hidden);
            return moby;
        }
    }
    finally
    {
        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }
}

async Task AssertLegacyProjectLeavesBundleMetadataEmpty()
{
    string directory = Path.Combine(Path.GetTempPath(), $"spyro-special-chest-legacy-{Guid.NewGuid():N}");
    string path = Path.Combine(directory, "legacy-v3-native-edits.json");
    Directory.CreateDirectory(directory);
    try
    {
        string json = """
        {
          "editor": "Spyro.Editor.Core",
          "levelName": "Artisans",
          "edits": [
            {
              "editKind": "add",
              "added": true,
              "index": 174,
              "trueIndex": 174,
              "label": "Legacy same-level chest",
              "labelEdited": "Legacy same-level chest",
              "typeHex": "0x20",
              "stateHex": "0x00",
              "sourceByte36Hex": "0xAE",
              "sourceByte37Hex": "0x00",
              "sourceByte4FHex": "0x00",
              "flag4AHex": "0x10",
              "flag4BHex": "0x54",
              "edited": { "x": 1, "y": 2, "z": 3 }
            }
          ]
        }
        """;
        await File.WriteAllTextAsync(path, json);

        List<Moby> loaded = [];
        int applied = MobyEditStore.Load(path, loaded);
        Expect(applied == 1 && loaded.Count == 1, "The legacy V1-V3 add manifest did not load.");
        Moby legacy = loaded[0];
        Expect(string.IsNullOrEmpty(legacy.SpecialChestBundleId) && string.IsNullOrEmpty(legacy.SpecialChestProfileId), "Missing V1-V3 fields must not infer a new special-chest bundle.");
        Expect(legacy.SpecialChestVisibleRootTrueIndex == -1 && legacy.SpecialChestHiddenCompanionTrueIndexes.Count == 0, "Missing V1-V3 fields must retain native ownership behavior.");
        Expect(legacy.SpecialChestCapacity == 0 && string.IsNullOrEmpty(legacy.SpecialChestEvidenceStatus), "Missing V1-V3 fields must not invent profile evidence or capacity.");
    }
    finally
    {
        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }
}
