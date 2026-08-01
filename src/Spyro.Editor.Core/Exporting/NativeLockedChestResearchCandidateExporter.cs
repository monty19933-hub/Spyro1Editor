namespace Spyro.Editor.Core.Exporting;

public sealed record NativeLockedChestResearchCandidateRequest(
    string DestinationLevelKey,
    string SourceImagePath,
    string OutputImagePath,
    string WadAnalysisPath,
    string LevelCatalogRootPath,
    bool EnableFastEntry = true);

public sealed record NativeLockedChestResearchCandidatePlan(
    string DestinationLevelKey,
    string DestinationLevelName,
    string RecipeId,
    int WadGrowthBytes,
    int OriginalExecutableLba,
    int RelocatedExecutableLba,
    string OriginalCopyBufferAddress,
    string RelocatedCopyBufferAddress,
    int HandlerPayloadLength,
    string HandlerPayloadSha256,
    int KeyTrueIndex,
    int LockedChestTrueIndex,
    IReadOnlyList<int> RewardMarkerTrueIndices,
    int SourceCountBefore,
    int SourceCountAfter,
    int ScenePointerFixupCountBefore,
    int ScenePointerFixupCountAfter,
    int TreasureTargetBefore,
    int TreasureTargetAfter,
    string RebasedLockedChestPackageSha256,
    int RebasedTexturedFaceCount,
    int TextureRegionCount,
    string FinalOverlaySha256,
    string FinalDataEntrySha256,
    string FinalExecutableSha256,
    string OutputImageSha256,
    bool FastEntryEnabled,
    string FastEntryInstructions,
    string Verification);

public sealed record NativeLockedChestResearchCandidateResult(
    string OutputImagePath,
    NativeLockedChestResearchCandidatePlan Plan,
    object DestinationPlan,
    bool Verified,
    long OutputLength,
    string Verification);

/// <summary>
/// Parameterized, research-only entry point for destination-specific Key + Locked Chest
/// transplants. The shape-ready list is intentionally broader than the implemented list:
/// a destination becomes callable only after its exact retail preimages, private texture
/// allocation, overlay dispatch, scene storage, and final-image hashes are checked in.
/// This exporter never registers an editor profile or changes normal Create BIN behavior.
/// </summary>
public static class NativeLockedChestResearchCandidateExporter
{
    private static readonly string[] ShapeReadyKeys =
    [
        "toasty",
        "clifftown",
        "alpineridge",
        "highcaves",
        "wizardpeak",
        "blowhard",
        "terracevillage",
        "dreamweavers",
        "darkpassage",
        "hauntedtowers",
        "gnastysworld",
        "gnastygnorc",
        "gnastysloot"
    ];

    private static readonly string[] ImplementedKeys =
    [
        "townsquare",
        "toasty",
        "clifftown",
        "alpineridge",
        "highcaves",
        "wizardpeak",
        "blowhard",
        "terracevillage",
        "mistybog",
        "dreamweavers",
        "darkpassage",
        "hauntedtowers",
        "gnastysworld",
        "twilightharbor",
        "gnastygnorc",
        "gnastysloot"
    ];

    public static IReadOnlyList<string> ShapeReadyDestinationKeys => ShapeReadyKeys;

    public static IReadOnlyList<string> ImplementedDestinationKeys => ImplementedKeys;

    public static bool CanExport(string destinationLevelKey) =>
        ImplementedKeys.Contains(NormalizeKey(destinationLevelKey), StringComparer.Ordinal);

    public static NativeLockedChestResearchCandidateResult Export(NativeLockedChestResearchCandidateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        string key = NormalizeKey(request.DestinationLevelKey);
        return key switch
        {
            "townsquare" => FromTownSquare(TownSquareNativeLockedChestCandidateExporter.Export(
                new TownSquareNativeLockedChestCandidateRequest(
                    request.SourceImagePath, request.OutputImagePath, request.WadAnalysisPath,
                    request.LevelCatalogRootPath, request.EnableFastEntry))),
            "toasty" => FromToasty(ToastyNativeLockedChestCandidateExporter.Export(
                new ToastyNativeLockedChestCandidateRequest(
                    request.SourceImagePath, request.OutputImagePath, request.WadAnalysisPath,
                    request.LevelCatalogRootPath, request.EnableFastEntry))),
            "clifftown" => FromCliffTown(CliffTownNativeLockedChestCandidateExporter.Export(
                new CliffTownNativeLockedChestCandidateRequest(
                    request.SourceImagePath, request.OutputImagePath, request.WadAnalysisPath,
                    request.LevelCatalogRootPath, request.EnableFastEntry))),
            "alpineridge" => FromAlpineRidge(AlpineRidgeNativeLockedChestCandidateExporter.Export(
                new AlpineRidgeNativeLockedChestCandidateRequest(
                    request.SourceImagePath, request.OutputImagePath, request.WadAnalysisPath,
                    request.LevelCatalogRootPath, request.EnableFastEntry))),
            "highcaves" => FromHighCaves(HighCavesNativeLockedChestCandidateExporter.Export(
                new HighCavesNativeLockedChestCandidateRequest(
                    request.SourceImagePath, request.OutputImagePath, request.WadAnalysisPath,
                    request.LevelCatalogRootPath, request.EnableFastEntry))),
            "wizardpeak" => FromWizardPeak(WizardPeakNativeLockedChestCandidateExporter.Export(
                new WizardPeakNativeLockedChestCandidateRequest(
                    request.SourceImagePath, request.OutputImagePath, request.WadAnalysisPath,
                    request.LevelCatalogRootPath, request.EnableFastEntry))),
            "blowhard" => FromBlowhard(BlowhardNativeLockedChestCandidateExporter.Export(
                new BlowhardNativeLockedChestCandidateRequest(
                    request.SourceImagePath, request.OutputImagePath, request.WadAnalysisPath,
                    request.LevelCatalogRootPath, request.EnableFastEntry))),
            "terracevillage" => FromTerraceVillage(TerraceVillageNativeLockedChestCandidateExporter.Export(
                new TerraceVillageNativeLockedChestCandidateRequest(
                    request.SourceImagePath, request.OutputImagePath, request.WadAnalysisPath,
                    request.LevelCatalogRootPath, request.EnableFastEntry))),
            "mistybog" => FromMistyBog(MistyBogNativeLockedChestCandidateExporter.Export(
                new MistyBogNativeLockedChestCandidateRequest(
                    request.SourceImagePath, request.OutputImagePath, request.WadAnalysisPath,
                    request.LevelCatalogRootPath, request.EnableFastEntry))),
            "dreamweavers" => FromDreamWeavers(DreamWeaversNativeLockedChestCandidateExporter.Export(
                new DreamWeaversNativeLockedChestCandidateRequest(
                    request.SourceImagePath, request.OutputImagePath, request.WadAnalysisPath,
                    request.LevelCatalogRootPath, request.EnableFastEntry))),
            "darkpassage" => FromDarkPassage(DarkPassageNativeLockedChestCandidateExporter.Export(
                new DarkPassageNativeLockedChestCandidateRequest(
                    request.SourceImagePath, request.OutputImagePath, request.WadAnalysisPath,
                    request.LevelCatalogRootPath, request.EnableFastEntry))),
            "hauntedtowers" => FromHauntedTowers(HauntedTowersNativeLockedChestCandidateExporter.Export(
                new HauntedTowersNativeLockedChestCandidateRequest(
                    request.SourceImagePath, request.OutputImagePath, request.WadAnalysisPath,
                    request.LevelCatalogRootPath, request.EnableFastEntry))),
            "gnastysworld" => FromGnastysWorld(GnastysWorldNativeLockedChestCandidateExporter.Export(
                new GnastysWorldNativeLockedChestCandidateRequest(
                    request.SourceImagePath, request.OutputImagePath, request.WadAnalysisPath,
                    request.LevelCatalogRootPath, request.EnableFastEntry))),
            "twilightharbor" => FromTwilightHarbor(TwilightHarborNativeLockedChestCandidateExporter.Export(
                new TwilightHarborNativeLockedChestCandidateRequest(
                    request.SourceImagePath, request.OutputImagePath, request.WadAnalysisPath,
                    request.LevelCatalogRootPath, request.EnableFastEntry))),
            "gnastygnorc" => FromGnastyGnorc(GnastyGnorcNativeLockedChestCandidateExporter.Export(
                new GnastyGnorcNativeLockedChestCandidateRequest(
                    request.SourceImagePath, request.OutputImagePath, request.WadAnalysisPath,
                    request.LevelCatalogRootPath, request.EnableFastEntry))),
            "gnastysloot" => FromGnastyLoot(GnastyLootNativeLockedChestCandidateExporter.Export(
                new GnastyLootNativeLockedChestCandidateRequest(
                    request.SourceImagePath, request.OutputImagePath, request.WadAnalysisPath,
                    request.LevelCatalogRootPath, request.EnableFastEntry))),
            _ when ShapeReadyKeys.Contains(key, StringComparer.Ordinal) =>
                throw new NotSupportedException(
                    $"Locked Chest destination '{request.DestinationLevelKey}' has a checked structural shape, " +
                    "but its exact private texture allocation and deterministic final-image guard are not checked in yet."),
            _ => throw new NotSupportedException(
                $"Locked Chest destination '{request.DestinationLevelKey}' is not in the guarded minimal-shape research set.")
        };
    }

    private static NativeLockedChestResearchCandidateResult FromTownSquare(TownSquareNativeLockedChestCandidateResult result) =>
        Convert("townsquare", "Town Square", result.OutputImagePath, result.Plan, result.Verified, result.OutputLength, result.Verification,
            result.Plan.RecipeId, result.Plan.WadGrowthBytes, result.Plan.OriginalExecutableLba, result.Plan.RelocatedExecutableLba,
            result.Plan.OriginalCopyBufferAddress, result.Plan.RelocatedCopyBufferAddress, result.Plan.HandlerPayloadLength,
            result.Plan.HandlerPayloadSha256, result.Plan.KeyTrueIndex, result.Plan.LockedChestTrueIndex, result.Plan.RewardMarkerTrueIndices,
            result.Plan.SourceCountBefore, result.Plan.SourceCountAfter, result.Plan.ScenePointerFixupCountBefore,
            result.Plan.ScenePointerFixupCountAfter, result.Plan.TreasureTargetBefore, result.Plan.TreasureTargetAfter,
            result.Plan.RebasedLockedChestPackageSha256, result.Plan.RebasedTexturedFaceCount, result.Plan.TextureRegions.Count,
            result.Plan.FinalOverlaySha256, result.Plan.FinalDataEntrySha256, result.Plan.FinalExecutableSha256,
            result.Plan.OutputImageSha256, result.Plan.FastEntryEnabled, result.Plan.FastEntryInstructions);

    private static NativeLockedChestResearchCandidateResult FromToasty(ToastyNativeLockedChestCandidateResult result) =>
        Convert("toasty", "Toasty", result.OutputImagePath, result.Plan, result.Verified, result.OutputLength, result.Verification,
            result.Plan.RecipeId, result.Plan.WadGrowthBytes, result.Plan.OriginalExecutableLba, result.Plan.RelocatedExecutableLba,
            result.Plan.OriginalCopyBufferAddress, result.Plan.RelocatedCopyBufferAddress, result.Plan.HandlerPayloadLength,
            result.Plan.HandlerPayloadSha256, result.Plan.KeyTrueIndex, result.Plan.LockedChestTrueIndex, result.Plan.RewardMarkerTrueIndices,
            result.Plan.SourceCountBefore, result.Plan.SourceCountAfter, result.Plan.ScenePointerFixupCountBefore,
            result.Plan.ScenePointerFixupCountAfter, result.Plan.TreasureTargetBefore, result.Plan.TreasureTargetAfter,
            result.Plan.RebasedLockedChestPackageSha256, result.Plan.RebasedTexturedFaceCount, result.Plan.TextureRegions.Count,
            result.Plan.FinalOverlaySha256, result.Plan.FinalDataEntrySha256, result.Plan.FinalExecutableSha256,
            result.Plan.OutputImageSha256, result.Plan.FastEntryEnabled, result.Plan.FastEntryInstructions);

    private static NativeLockedChestResearchCandidateResult FromCliffTown(CliffTownNativeLockedChestCandidateResult result) =>
        Convert("clifftown", "Cliff Town", result.OutputImagePath, result.Plan, result.Verified, result.OutputLength, result.Verification,
            result.Plan.RecipeId, result.Plan.WadGrowthBytes, result.Plan.OriginalExecutableLba, result.Plan.RelocatedExecutableLba,
            result.Plan.OriginalCopyBufferAddress, result.Plan.RelocatedCopyBufferAddress, result.Plan.HandlerPayloadLength,
            result.Plan.HandlerPayloadSha256, result.Plan.KeyTrueIndex, result.Plan.LockedChestTrueIndex, result.Plan.RewardMarkerTrueIndices,
            result.Plan.SourceCountBefore, result.Plan.SourceCountAfter, result.Plan.ScenePointerFixupCountBefore,
            result.Plan.ScenePointerFixupCountAfter, result.Plan.TreasureTargetBefore, result.Plan.TreasureTargetAfter,
            result.Plan.RebasedLockedChestPackageSha256, result.Plan.RebasedTexturedFaceCount, result.Plan.TextureRegions.Count,
            result.Plan.FinalOverlaySha256, result.Plan.FinalDataEntrySha256, result.Plan.FinalExecutableSha256,
            result.Plan.OutputImageSha256, result.Plan.FastEntryEnabled, result.Plan.FastEntryInstructions);

    private static NativeLockedChestResearchCandidateResult FromAlpineRidge(AlpineRidgeNativeLockedChestCandidateResult result) =>
        Convert("alpineridge", "Alpine Ridge", result.OutputImagePath, result.Plan, result.Verified, result.OutputLength, result.Verification,
            result.Plan.RecipeId, result.Plan.WadGrowthBytes, result.Plan.OriginalExecutableLba, result.Plan.RelocatedExecutableLba,
            result.Plan.OriginalCopyBufferAddress, result.Plan.RelocatedCopyBufferAddress, result.Plan.HandlerPayloadLength,
            result.Plan.HandlerPayloadSha256, result.Plan.KeyTrueIndex, result.Plan.LockedChestTrueIndex, result.Plan.RewardMarkerTrueIndices,
            result.Plan.SourceCountBefore, result.Plan.SourceCountAfter, result.Plan.ScenePointerFixupCountBefore,
            result.Plan.ScenePointerFixupCountAfter, result.Plan.TreasureTargetBefore, result.Plan.TreasureTargetAfter,
            result.Plan.RebasedLockedChestPackageSha256, result.Plan.RebasedTexturedFaceCount, result.Plan.TextureRegions.Count,
            result.Plan.FinalOverlaySha256, result.Plan.FinalDataEntrySha256, result.Plan.FinalExecutableSha256,
            result.Plan.OutputImageSha256, result.Plan.FastEntryEnabled, result.Plan.FastEntryInstructions);

    private static NativeLockedChestResearchCandidateResult FromHighCaves(HighCavesNativeLockedChestCandidateResult result) =>
        Convert("highcaves", "High Caves", result.OutputImagePath, result.Plan, result.Verified, result.OutputLength, result.Verification,
            result.Plan.RecipeId, result.Plan.WadGrowthBytes, result.Plan.OriginalExecutableLba, result.Plan.RelocatedExecutableLba,
            result.Plan.OriginalCopyBufferAddress, result.Plan.RelocatedCopyBufferAddress, result.Plan.HandlerPayloadLength,
            result.Plan.HandlerPayloadSha256, result.Plan.KeyTrueIndex, result.Plan.LockedChestTrueIndex, result.Plan.RewardMarkerTrueIndices,
            result.Plan.SourceCountBefore, result.Plan.SourceCountAfter, result.Plan.ScenePointerFixupCountBefore,
            result.Plan.ScenePointerFixupCountAfter, result.Plan.TreasureTargetBefore, result.Plan.TreasureTargetAfter,
            result.Plan.RebasedLockedChestPackageSha256, result.Plan.RebasedTexturedFaceCount, result.Plan.TextureRegions.Count,
            result.Plan.FinalOverlaySha256, result.Plan.FinalDataEntrySha256, result.Plan.FinalExecutableSha256,
            result.Plan.OutputImageSha256, result.Plan.FastEntryEnabled, result.Plan.FastEntryInstructions);

    private static NativeLockedChestResearchCandidateResult FromWizardPeak(WizardPeakNativeLockedChestCandidateResult result) =>
        Convert("wizardpeak", "Wizard Peak", result.OutputImagePath, result.Plan, result.Verified, result.OutputLength, result.Verification,
            result.Plan.RecipeId, result.Plan.WadGrowthBytes, result.Plan.OriginalExecutableLba, result.Plan.RelocatedExecutableLba,
            result.Plan.OriginalCopyBufferAddress, result.Plan.RelocatedCopyBufferAddress, result.Plan.HandlerPayloadLength,
            result.Plan.HandlerPayloadSha256, result.Plan.KeyTrueIndex, result.Plan.LockedChestTrueIndex, result.Plan.RewardMarkerTrueIndices,
            result.Plan.SourceCountBefore, result.Plan.SourceCountAfter, result.Plan.ScenePointerFixupCountBefore,
            result.Plan.ScenePointerFixupCountAfter, result.Plan.TreasureTargetBefore, result.Plan.TreasureTargetAfter,
            result.Plan.RebasedLockedChestPackageSha256, result.Plan.RebasedTexturedFaceCount, result.Plan.TextureRegions.Count,
            result.Plan.FinalOverlaySha256, result.Plan.FinalDataEntrySha256, result.Plan.FinalExecutableSha256,
            result.Plan.OutputImageSha256, result.Plan.FastEntryEnabled, result.Plan.FastEntryInstructions);

    private static NativeLockedChestResearchCandidateResult FromBlowhard(BlowhardNativeLockedChestCandidateResult result) =>
        Convert("blowhard", "Blowhard", result.OutputImagePath, result.Plan, result.Verified, result.OutputLength, result.Verification,
            result.Plan.RecipeId, result.Plan.WadGrowthBytes, result.Plan.OriginalExecutableLba, result.Plan.RelocatedExecutableLba,
            result.Plan.OriginalCopyBufferAddress, result.Plan.RelocatedCopyBufferAddress, result.Plan.HandlerPayloadLength,
            result.Plan.HandlerPayloadSha256, result.Plan.KeyTrueIndex, result.Plan.LockedChestTrueIndex, result.Plan.RewardMarkerTrueIndices,
            result.Plan.SourceCountBefore, result.Plan.SourceCountAfter, result.Plan.ScenePointerFixupCountBefore,
            result.Plan.ScenePointerFixupCountAfter, result.Plan.TreasureTargetBefore, result.Plan.TreasureTargetAfter,
            result.Plan.RebasedLockedChestPackageSha256, result.Plan.RebasedTexturedFaceCount, result.Plan.TextureRegions.Count,
            result.Plan.FinalOverlaySha256, result.Plan.FinalDataEntrySha256, result.Plan.FinalExecutableSha256,
            result.Plan.OutputImageSha256, result.Plan.FastEntryEnabled, result.Plan.FastEntryInstructions);

    private static NativeLockedChestResearchCandidateResult FromTerraceVillage(TerraceVillageNativeLockedChestCandidateResult result) =>
        Convert("terracevillage", "Terrace Village", result.OutputImagePath, result.Plan, result.Verified, result.OutputLength, result.Verification,
            result.Plan.RecipeId, result.Plan.WadGrowthBytes, result.Plan.OriginalExecutableLba, result.Plan.RelocatedExecutableLba,
            result.Plan.OriginalCopyBufferAddress, result.Plan.RelocatedCopyBufferAddress, result.Plan.HandlerPayloadLength,
            result.Plan.HandlerPayloadSha256, result.Plan.KeyTrueIndex, result.Plan.LockedChestTrueIndex, result.Plan.RewardMarkerTrueIndices,
            result.Plan.SourceCountBefore, result.Plan.SourceCountAfter, result.Plan.ScenePointerFixupCountBefore,
            result.Plan.ScenePointerFixupCountAfter, result.Plan.TreasureTargetBefore, result.Plan.TreasureTargetAfter,
            result.Plan.RebasedLockedChestPackageSha256, result.Plan.RebasedTexturedFaceCount, result.Plan.TextureRegions.Count,
            result.Plan.FinalOverlaySha256, result.Plan.FinalDataEntrySha256, result.Plan.FinalExecutableSha256,
            result.Plan.OutputImageSha256, result.Plan.FastEntryEnabled, result.Plan.FastEntryInstructions);

    private static NativeLockedChestResearchCandidateResult FromMistyBog(MistyBogNativeLockedChestCandidateResult result) =>
        Convert("mistybog", "Misty Bog", result.OutputImagePath, result.Plan, result.Verified, result.OutputLength, result.Verification,
            result.Plan.RecipeId, result.Plan.WadGrowthBytes, result.Plan.OriginalExecutableLba, result.Plan.RelocatedExecutableLba,
            result.Plan.OriginalCopyBufferAddress, result.Plan.RelocatedCopyBufferAddress, result.Plan.HandlerPayloadLength,
            result.Plan.HandlerPayloadSha256, result.Plan.KeyTrueIndex, result.Plan.LockedChestTrueIndex, result.Plan.RewardMarkerTrueIndices,
            result.Plan.SourceCountBefore, result.Plan.SourceCountAfter, result.Plan.ScenePointerFixupCountBefore,
            result.Plan.ScenePointerFixupCountAfter, result.Plan.TreasureTargetBefore, result.Plan.TreasureTargetAfter,
            result.Plan.RebasedLockedChestPackageSha256, result.Plan.RebasedTexturedFaceCount, result.Plan.TextureRegions.Count,
            result.Plan.FinalOverlaySha256, result.Plan.FinalDataEntrySha256, result.Plan.FinalExecutableSha256,
            result.Plan.OutputImageSha256, result.Plan.FastEntryEnabled, result.Plan.FastEntryInstructions);

    private static NativeLockedChestResearchCandidateResult FromDreamWeavers(DreamWeaversNativeLockedChestCandidateResult result) =>
        Convert("dreamweavers", "Dream Weavers", result.OutputImagePath, result.Plan, result.Verified, result.OutputLength, result.Verification,
            result.Plan.RecipeId, result.Plan.WadGrowthBytes, result.Plan.OriginalExecutableLba, result.Plan.RelocatedExecutableLba,
            result.Plan.OriginalCopyBufferAddress, result.Plan.RelocatedCopyBufferAddress, result.Plan.HandlerPayloadLength,
            result.Plan.HandlerPayloadSha256, result.Plan.KeyTrueIndex, result.Plan.LockedChestTrueIndex, result.Plan.RewardMarkerTrueIndices,
            result.Plan.SourceCountBefore, result.Plan.SourceCountAfter, result.Plan.ScenePointerFixupCountBefore,
            result.Plan.ScenePointerFixupCountAfter, result.Plan.TreasureTargetBefore, result.Plan.TreasureTargetAfter,
            result.Plan.RebasedLockedChestPackageSha256, result.Plan.RebasedTexturedFaceCount, result.Plan.TextureRegions.Count,
            result.Plan.FinalOverlaySha256, result.Plan.FinalDataEntrySha256, result.Plan.FinalExecutableSha256,
            result.Plan.OutputImageSha256, result.Plan.FastEntryEnabled, result.Plan.FastEntryInstructions);

    private static NativeLockedChestResearchCandidateResult FromDarkPassage(DarkPassageNativeLockedChestCandidateResult result) =>
        Convert("darkpassage", "Dark Passage", result.OutputImagePath, result.Plan, result.Verified, result.OutputLength, result.Verification,
            result.Plan.RecipeId, result.Plan.WadGrowthBytes, result.Plan.OriginalExecutableLba, result.Plan.RelocatedExecutableLba,
            result.Plan.OriginalCopyBufferAddress, result.Plan.RelocatedCopyBufferAddress, result.Plan.HandlerPayloadLength,
            result.Plan.HandlerPayloadSha256, result.Plan.KeyTrueIndex, result.Plan.LockedChestTrueIndex, result.Plan.RewardMarkerTrueIndices,
            result.Plan.SourceCountBefore, result.Plan.SourceCountAfter, result.Plan.ScenePointerFixupCountBefore,
            result.Plan.ScenePointerFixupCountAfter, result.Plan.TreasureTargetBefore, result.Plan.TreasureTargetAfter,
            result.Plan.RebasedLockedChestPackageSha256, result.Plan.RebasedTexturedFaceCount, result.Plan.TextureRegions.Count,
            result.Plan.FinalOverlaySha256, result.Plan.FinalDataEntrySha256, result.Plan.FinalExecutableSha256,
            result.Plan.OutputImageSha256, result.Plan.FastEntryEnabled, result.Plan.FastEntryInstructions);

    private static NativeLockedChestResearchCandidateResult FromHauntedTowers(HauntedTowersNativeLockedChestCandidateResult result) =>
        Convert("hauntedtowers", "Haunted Towers", result.OutputImagePath, result.Plan, result.Verified, result.OutputLength, result.Verification,
            result.Plan.RecipeId, result.Plan.WadGrowthBytes, result.Plan.OriginalExecutableLba, result.Plan.RelocatedExecutableLba,
            result.Plan.OriginalCopyBufferAddress, result.Plan.RelocatedCopyBufferAddress, result.Plan.HandlerPayloadLength,
            result.Plan.HandlerPayloadSha256, result.Plan.KeyTrueIndex, result.Plan.LockedChestTrueIndex, result.Plan.RewardMarkerTrueIndices,
            result.Plan.SourceCountBefore, result.Plan.SourceCountAfter, result.Plan.ScenePointerFixupCountBefore,
            result.Plan.ScenePointerFixupCountAfter, result.Plan.TreasureTargetBefore, result.Plan.TreasureTargetAfter,
            result.Plan.RebasedLockedChestPackageSha256, result.Plan.RebasedTexturedFaceCount, result.Plan.TextureRegions.Count,
            result.Plan.FinalOverlaySha256, result.Plan.FinalDataEntrySha256, result.Plan.FinalExecutableSha256,
            result.Plan.OutputImageSha256, result.Plan.FastEntryEnabled, result.Plan.FastEntryInstructions);

    private static NativeLockedChestResearchCandidateResult FromGnastysWorld(GnastysWorldNativeLockedChestCandidateResult result) =>
        Convert("gnastysworld", "Gnasty's World", result.OutputImagePath, result.Plan, result.Verified, result.OutputLength, result.Verification,
            result.Plan.RecipeId, result.Plan.WadGrowthBytes, result.Plan.OriginalExecutableLba, result.Plan.RelocatedExecutableLba,
            result.Plan.OriginalCopyBufferAddress, result.Plan.RelocatedCopyBufferAddress, result.Plan.HandlerPayloadLength,
            result.Plan.HandlerPayloadSha256, result.Plan.KeyTrueIndex, result.Plan.LockedChestTrueIndex, result.Plan.RewardMarkerTrueIndices,
            result.Plan.SourceCountBefore, result.Plan.SourceCountAfter, result.Plan.ScenePointerFixupCountBefore,
            result.Plan.ScenePointerFixupCountAfter, result.Plan.TreasureTargetBefore, result.Plan.TreasureTargetAfter,
            result.Plan.RebasedLockedChestPackageSha256, result.Plan.RebasedTexturedFaceCount, result.Plan.TextureRegions.Count,
            result.Plan.FinalOverlaySha256, result.Plan.FinalDataEntrySha256, result.Plan.FinalExecutableSha256,
            result.Plan.OutputImageSha256, result.Plan.FastEntryEnabled, result.Plan.FastEntryInstructions);

    private static NativeLockedChestResearchCandidateResult FromTwilightHarbor(TwilightHarborNativeLockedChestCandidateResult result) =>
        Convert("twilightharbor", "Twilight Harbor", result.OutputImagePath, result.Plan, result.Verified, result.OutputLength, result.Verification,
            result.Plan.RecipeId, result.Plan.WadGrowthBytes, result.Plan.OriginalExecutableLba, result.Plan.RelocatedExecutableLba,
            result.Plan.OriginalCopyBufferAddress, result.Plan.RelocatedCopyBufferAddress, result.Plan.HandlerPayloadLength,
            result.Plan.HandlerPayloadSha256, result.Plan.KeyTrueIndex, result.Plan.LockedChestTrueIndex, result.Plan.RewardMarkerTrueIndices,
            result.Plan.SourceCountBefore, result.Plan.SourceCountAfter, result.Plan.ScenePointerFixupCountBefore,
            result.Plan.ScenePointerFixupCountAfter, result.Plan.TreasureTargetBefore, result.Plan.TreasureTargetAfter,
            result.Plan.RebasedLockedChestPackageSha256, result.Plan.RebasedTexturedFaceCount, result.Plan.TextureRegions.Count,
            result.Plan.FinalOverlaySha256, result.Plan.FinalDataEntrySha256, result.Plan.FinalExecutableSha256,
            result.Plan.OutputImageSha256, result.Plan.FastEntryEnabled, result.Plan.FastEntryInstructions);

    private static NativeLockedChestResearchCandidateResult FromGnastyGnorc(GnastyGnorcNativeLockedChestCandidateResult result) =>
        Convert("gnastygnorc", "Gnasty Gnorc", result.OutputImagePath, result.Plan, result.Verified, result.OutputLength, result.Verification,
            result.Plan.RecipeId, result.Plan.WadGrowthBytes, result.Plan.OriginalExecutableLba, result.Plan.RelocatedExecutableLba,
            result.Plan.OriginalCopyBufferAddress, result.Plan.RelocatedCopyBufferAddress, result.Plan.HandlerPayloadLength,
            result.Plan.HandlerPayloadSha256, result.Plan.KeyTrueIndex, result.Plan.LockedChestTrueIndex, result.Plan.RewardMarkerTrueIndices,
            result.Plan.SourceCountBefore, result.Plan.SourceCountAfter, result.Plan.ScenePointerFixupCountBefore,
            result.Plan.ScenePointerFixupCountAfter, result.Plan.TreasureTargetBefore, result.Plan.TreasureTargetAfter,
            result.Plan.RebasedLockedChestPackageSha256, result.Plan.RebasedTexturedFaceCount, result.Plan.TextureRegions.Count,
            result.Plan.FinalOverlaySha256, result.Plan.FinalDataEntrySha256, result.Plan.FinalExecutableSha256,
            result.Plan.OutputImageSha256, result.Plan.FastEntryEnabled, result.Plan.FastEntryInstructions);

    private static NativeLockedChestResearchCandidateResult FromGnastyLoot(GnastyLootNativeLockedChestCandidateResult result) =>
        Convert("gnastysloot", "Gnasty's Loot", result.OutputImagePath, result.Plan, result.Verified, result.OutputLength, result.Verification,
            result.Plan.RecipeId, result.Plan.WadGrowthBytes, result.Plan.OriginalExecutableLba, result.Plan.RelocatedExecutableLba,
            result.Plan.OriginalCopyBufferAddress, result.Plan.RelocatedCopyBufferAddress, result.Plan.HandlerPayloadLength,
            result.Plan.HandlerPayloadSha256, result.Plan.KeyTrueIndex, result.Plan.LockedChestTrueIndex, result.Plan.RewardMarkerTrueIndices,
            result.Plan.SourceCountBefore, result.Plan.SourceCountAfter, result.Plan.ScenePointerFixupCountBefore,
            result.Plan.ScenePointerFixupCountAfter, result.Plan.TreasureTargetBefore, result.Plan.TreasureTargetAfter,
            result.Plan.RebasedLockedChestPackageSha256, result.Plan.RebasedTexturedFaceCount, result.Plan.TextureRegions.Count,
            result.Plan.FinalOverlaySha256, result.Plan.FinalDataEntrySha256, result.Plan.FinalExecutableSha256,
            result.Plan.OutputImageSha256, result.Plan.FastEntryEnabled, result.Plan.FastEntryInstructions);

    private static NativeLockedChestResearchCandidateResult Convert(
        string destinationLevelKey,
        string destinationLevelName,
        string outputImagePath,
        object destinationPlan,
        bool verified,
        long outputLength,
        string verification,
        string recipeId,
        int wadGrowthBytes,
        int originalExecutableLba,
        int relocatedExecutableLba,
        string originalCopyBufferAddress,
        string relocatedCopyBufferAddress,
        int handlerPayloadLength,
        string handlerPayloadSha256,
        int keyTrueIndex,
        int lockedChestTrueIndex,
        IReadOnlyList<int> rewardMarkerTrueIndices,
        int sourceCountBefore,
        int sourceCountAfter,
        int scenePointerFixupCountBefore,
        int scenePointerFixupCountAfter,
        int treasureTargetBefore,
        int treasureTargetAfter,
        string rebasedLockedChestPackageSha256,
        int rebasedTexturedFaceCount,
        int textureRegionCount,
        string finalOverlaySha256,
        string finalDataEntrySha256,
        string finalExecutableSha256,
        string outputImageSha256,
        bool fastEntryEnabled,
        string fastEntryInstructions) =>
        new(
            outputImagePath,
            new NativeLockedChestResearchCandidatePlan(
                destinationLevelKey, destinationLevelName, recipeId, wadGrowthBytes,
                originalExecutableLba, relocatedExecutableLba,
                originalCopyBufferAddress, relocatedCopyBufferAddress,
                handlerPayloadLength, handlerPayloadSha256,
                keyTrueIndex, lockedChestTrueIndex, rewardMarkerTrueIndices,
                sourceCountBefore, sourceCountAfter,
                scenePointerFixupCountBefore, scenePointerFixupCountAfter,
                treasureTargetBefore, treasureTargetAfter,
                rebasedLockedChestPackageSha256, rebasedTexturedFaceCount,
                textureRegionCount, finalOverlaySha256, finalDataEntrySha256,
                finalExecutableSha256, outputImageSha256,
                fastEntryEnabled, fastEntryInstructions, verification),
            destinationPlan,
            verified,
            outputLength,
            verification);

    private static string NormalizeKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return "";
        return new string(key.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    }
}
