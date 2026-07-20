using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Rendering;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Workspace;

namespace Spyro.Editor.Core.Exporting;

public static class MobySourcePatchExporter
{
    private const int WadLba = 37;
    private const uint ExeDestination = 0x80010000;
    private const int RecordStride = 0x58;
    private const string CrossLevelSourceRecordCandidateAppendFeature = "CrossLevelSourceRecordCandidateAppend";
    private const string CrossLevelSourceRecordCandidateRecipeMode = "SourceRecordCandidateAppend";
    private const int XOffset = 0x0C;
    private const int YOffset = 0x10;
    private const int ZOffset = 0x14;
    private const int YawMatrixOffset = 0x20;
    private const int YawByteOffset = 0x46;
    private const int TypeOffset = 0x50;
    private const int StateOffset = 0x51;
    private const int HiddenRawCoordinate = -480000;
    private const long TreasureTotalTableImageOffset = 0x7945FB0;

    public static async Task<MobySourcePatchResult> ExportAsync(MobySourcePatchRequest request, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(request.SourceImagePath))
            throw new FileNotFoundException("Missing source disc image.", request.SourceImagePath);
        if (!File.Exists(request.NativeEditsPath))
            throw new FileNotFoundException("Missing native moby edit file.", request.NativeEditsPath);

        string outputPrefix = string.IsNullOrWhiteSpace(request.OutputPrefix)
            ? Path.Combine(Path.GetDirectoryName(request.SourceImagePath) ?? "", $"Spyro the Dragon (USA)-{ToSafeSlug(request.Level.Key)}-object-edits")
            : request.OutputPrefix;
        string outputImagePath = $"{outputPrefix}.bin";
        string outputCuePath = $"{outputPrefix}.cue";
        string outputPlanPath = $"{outputPrefix}.moby-source-patch-plan.json";
        string greenWizardResidentPlanPath = $"{outputPrefix}.green-wizard-resident-plan.json";

        MobySourcePatchPlan plan = BuildPlan(
            request.SourceImagePath,
            request.SourceCuePath,
            outputImagePath,
            outputCuePath,
            request.Level,
            request.NativeEditsPath,
            request.AllowPlanOnlyActorPackageImports,
            request.AllowGuardedNativeCloneAppend,
            request.NativeMobyPathEditsPath);

        Directory.CreateDirectory(Path.GetDirectoryName(outputPlanPath) ?? ".");
        await File.WriteAllTextAsync(outputPlanPath, JsonSerializer.Serialize(plan, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);

        if (request.WriteImage)
        {
            DeleteStaleOutput(greenWizardResidentPlanPath);

            if (plan.PatchCount == 0)
            {
                DeleteStaleOutput(outputImagePath);
                DeleteStaleOutput(outputCuePath);
                return new MobySourcePatchResult(outputImagePath, outputCuePath, outputPlanPath, plan, false);
            }

            File.Copy(request.SourceImagePath, outputImagePath, true);
            DiscLayout layout = DiscImage.DetectLayout(outputImagePath);
            await using (FileStream stream = File.Open(outputImagePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
            {
                DiscFileRecord? executable = null;
                foreach (MobySourcePatch patch in plan.Patches)
                {
                    byte[] bytes = HexToBytes(patch.AfterHexPreview);
                    if (TryParseWadRelativeOffset(patch.WadRelativeOffset, out long wadOffset))
                    {
                        DiscImage.WriteFileBytes(stream, layout, WadLba, wadOffset, bytes);
                        continue;
                    }

                    if (TryParseExeRuntimeAddress(patch.WadRelativeOffset, out uint exeAddress))
                    {
                        executable ??= FindExecutable(stream, layout);
                        long fileOffset = ExeFileOffset(exeAddress, patch.WadRelativeOffset);
                        if (TestLevelWarpPatch.IsPatch(patch))
                            TestLevelWarpPatch.VerifyBeforeWrite(stream, layout);
                        DiscImage.WriteFileBytes(stream, layout, executable.Lba, fileOffset, bytes);
                        continue;
                    }

                    stream.Position = ParseRequiredLong(patch.ImageOffset, "patch.imageOffset");
                    stream.Write(bytes);
                }

                if (plan.Patches.Any(TestLevelWarpPatch.IsPatch))
                {
                    stream.Flush();
                    TestLevelWarpPatch.VerifyWritten(stream, layout);
                }
            }

            string cueText = DiscImage.BuildCueText(request.SourceCuePath, Path.GetFileName(outputImagePath));
            await File.WriteAllTextAsync(outputCuePath, cueText, Encoding.ASCII, cancellationToken);

            if (GreenWizardResidentSwapComposer.CountResidentWizardPatches(plan) > 0)
            {
                try
                {
                    GreenWizardResidentSwapBuildMode buildMode = request.AllowPlanOnlyActorPackageImports
                        ? GreenWizardResidentSwapBuildMode.Candidate
                        : GreenWizardResidentSwapBuildMode.Normal;
                    GreenWizardResidentSwapComposer.ApplyAndVerify(
                        outputImagePath,
                        request.Level,
                        plan,
                        greenWizardResidentPlanPath,
                        buildMode);
                }
                catch (Exception ex)
                {
                    DeleteStaleOutput(outputCuePath);
                    DeleteStaleOutput(outputImagePath);
                    DeleteStaleOutput(greenWizardResidentPlanPath);
                    throw new InvalidOperationException(
                        $"The {request.Level.DisplayName} Green Wizard resident bundle could not be completed, so its unsafe BIN/CUE were removed: {ex.Message}",
                        ex);
                }
            }
        }

        return new MobySourcePatchResult(outputImagePath, outputCuePath, outputPlanPath, plan, request.WriteImage && plan.PatchCount > 0);
    }

    private static void DeleteStaleOutput(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    public static MobySourcePatchPlan BuildPlan(
        string sourceImagePath,
        string sourceCuePath,
        string outputImagePath,
        string outputCuePath,
        LevelDefinition level,
        string nativeEditsPath,
        bool allowPlanOnlyActorPackageImports = false,
        bool allowGuardedNativeCloneAppend = false,
        string nativeMobyPathEditsPath = "")
    {
        if (!level.HasSourceTable)
            throw new InvalidOperationException($"{level.DisplayName} does not have a mapped source moby table yet.");
        if (!File.Exists(sourceImagePath))
            throw new FileNotFoundException("Missing source disc image.", sourceImagePath);
        if (!File.Exists(nativeEditsPath))
            throw new FileNotFoundException("Missing native moby edit file.", nativeEditsPath);

        long tableWadOffset = ParseRequiredLong(level.SourceTableWadOffset, "level.sourceTableWadOffset");
        long tableRelativeOffset = string.IsNullOrWhiteSpace(level.SourceTableRelativeOffset)
            ? 0
            : ParseRequiredLong(level.SourceTableRelativeOffset, "level.sourceTableRelativeOffset");
        DiscLayout layout = DiscImage.DetectLayout(sourceImagePath);
        using FileStream imageStream = File.OpenRead(sourceImagePath);
        using FileStream editStream = File.OpenRead(nativeEditsPath);
        using JsonDocument editDocument = JsonDocument.Parse(editStream);

        if (!editDocument.RootElement.TryGetProperty("edits", out JsonElement editsElement) || editsElement.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("The native moby edit file does not contain an edits array.");

        ArtisansNativeLockedChestRuntimeBundleIntent? artisansLockedChestBundle = null;
        if (!allowPlanOnlyActorPackageImports)
        {
            ArtisansNativeLockedChestRuntimeBundleComposer.TryDetectIntent(
                level,
                editsElement,
                out artisansLockedChestBundle);
        }

        List<MobySourcePatch> patches = new();
        List<MobyActorPackageImportPreview> packageImportPreviews = new();
        List<string> skippedEdits = new();
        List<MobySourceEditOutcome> editOutcomes = new();
        List<string> sourceCountNotes = new();
        HashSet<long> writtenWadOffsets = new();
        HashSet<string> writtenActorPackageRecipes = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, CrossLevelSharedSpecialCluster> sharedCrossLevelSpecialClusters = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> releaseLimitedNativeCloneAppendFamilies = new(StringComparer.OrdinalIgnoreCase);
        int appendNextTrueIndex = level.SourceRecordCount;
        bool hasAppend = false;
        int exportedTreasureDelta = 0;
        int springChestControllerAppendTrueIndex = -1;
        int springChestShellAppendTrueIndex = -1;
        int springChestControllerActorId = 0x00C2;
        List<SpringChestAppendAnchor> peaceKeepersSpringChestAnchors = [];
        bool suppressStoneHillSpringChestHelper = JsonValue.GetBoolean(editDocument.RootElement, "suppressStoneHillSpringChestHelper");
        bool suppressActorPackageImports = JsonValue.GetBoolean(editDocument.RootElement, "suppressActorPackageImports");
        string workspaceRoot = FindCatalogRoot(Path.GetDirectoryName(nativeEditsPath) ?? ".");
        LevelCatalog catalog = LevelCatalog.Load(workspaceRoot);
        GeometryCandidate? levelGeometry = TryLoadLevelGeometry(workspaceRoot, level.Key);
        HashSet<int> protectedSourceSlots = BuildProtectedSourceSlots(editsElement, level.SourceRecordCount);
        HashSet<int> autoReuseTargetSlots = new();
        Lazy<IReadOnlyDictionary<int, DragonRescueCameraData>> dragonRescueCameras = new(() =>
            DragonRescueCameraLocator.Locate(imageStream, layout, level));
        IReadOnlyDictionary<int, (int RawX, int RawY)> finalDragonRawPositions =
            BuildFinalDragonRawPositions(editsElement, tableWadOffset, imageStream, layout, level);
        Lazy<FlyInLandingData> flyInLanding = new(() =>
            FlyInLandingLocator.Locate(imageStream, layout, level));
        Lazy<PortalSourceLevelData> portalSourceData = new(() =>
            PortalSourceDataLocator.Locate(imageStream, layout, level));
        List<PortalSourceMovement> portalMovements = [];

        foreach (JsonElement edit in editsElement.EnumerateArray())
        {
            bool isDragonRunToEdit = IsDragonRunToEdit(edit);
            int trueIndex = isDragonRunToEdit
                ? GetDragonRunToOwnerTrueIndex(edit)
                : JsonValue.GetInt32(edit, "trueIndex", -1);
            string label = JsonValue.GetString(edit, "label", JsonValue.GetString(edit, "labelEdited", trueIndex >= 0 ? $"T{trueIndex}" : "moby"));
            string editKind = JsonValue.GetString(edit, "editKind", JsonValue.GetBoolean(edit, "added") ? "add" : JsonValue.GetBoolean(edit, "removed") ? "remove" : "update");
            bool isArtisansLockedChestBundleEdit = artisansLockedChestBundle != null &&
                ArtisansNativeLockedChestRuntimeBundleComposer.IsBundleTemplateEdit(edit);
            int patchStart = patches.Count;
            int packagePreviewStart = packageImportPreviews.Count;
            int skippedEditStart = skippedEdits.Count;
            List<MobySourceEditSafetyFinding> editSafetyFindings = [];
            try
            {
                if (isArtisansLockedChestBundleEdit)
                    continue;

                if (isDragonRunToEdit)
                {
                    AddDragonRunToEndpointPatches(
                        imageStream,
                        layout,
                        level,
                        label,
                        edit,
                        dragonRescueCameras,
                        finalDragonRawPositions,
                        levelGeometry,
                        patches,
                        writtenWadOffsets,
                        skippedEdits,
                        editSafetyFindings);
                    continue;
                }

                if (IsFlyInLandingEdit(edit))
                {
                    AddFlyInLandingPatches(imageStream, layout, level, label, edit, flyInLanding, patches, writtenWadOffsets);
                    continue;
                }

                if (JsonValue.GetBoolean(edit, "added") || string.Equals(JsonValue.GetString(edit, "editKind"), "add", StringComparison.OrdinalIgnoreCase))
                {
                    if (!allowGuardedNativeCloneAppend &&
                        TryPatchAddedNativeCloneIntoAutoSlot(imageStream, layout, catalog, level, levelGeometry, tableWadOffset, label, edit, protectedSourceSlots, autoReuseTargetSlots, patches, writtenWadOffsets, skippedEdits, out int autoSlotTreasureDelta))
                    {
                        exportedTreasureDelta += autoSlotTreasureDelta;
                        continue;
                    }

                    string releaseLimitedFamilyKey = "";
                    string releaseLimitedFamilyName = "this enemy/chest family";
                    bool releaseLimitedNativeCloneAppend = !allowGuardedNativeCloneAppend &&
                        TryGetReleaseLimitedNativeCloneAppendFamily(edit, level, out releaseLimitedFamilyKey, out releaseLimitedFamilyName);
                    if (releaseLimitedNativeCloneAppend && releaseLimitedNativeCloneAppendFamilies.Contains(releaseLimitedFamilyKey))
                    {
                        skippedEdits.Add(
                            $"{label}: the normal Create BIN safety budget for {releaseLimitedFamilyName} is exhausted. " +
                            "One same-family true-add is currently validated after reusable slots; later copies stay saved in the editor but are skipped because multiple active enemy/chest appends can crash in-game. " +
                            "Use Change To / slot replacement, or create a disposable research BIN for further allocation testing.");
                        continue;
                    }

                    int assignedAppendTrueIndex = appendNextTrueIndex;
                    if (TryAddAppendPatch(imageStream, layout, catalog, level, levelGeometry, tableWadOffset, tableRelativeOffset, appendNextTrueIndex, label, edit, sourceImagePath, workspaceRoot, allowPlanOnlyActorPackageImports, suppressActorPackageImports, allowGuardedNativeCloneAppend, patches, packageImportPreviews, writtenWadOffsets, writtenActorPackageRecipes, sharedCrossLevelSpecialClusters, skippedEdits))
                    {
                        exportedTreasureDelta += ComputeTreasureDelta(edit);
                        TrackSpringChestPairAppend(edit, assignedAppendTrueIndex, packageImportPreviews, ref springChestControllerAppendTrueIndex, ref springChestShellAppendTrueIndex, ref springChestControllerActorId);
                        TrackPeaceKeepersSpringChestAppend(edit, assignedAppendTrueIndex, peaceKeepersSpringChestAnchors);
                        appendNextTrueIndex++;
                        hasAppend = true;
                        if (releaseLimitedNativeCloneAppend)
                            releaseLimitedNativeCloneAppendFamilies.Add(releaseLimitedFamilyKey);
                    }

                    continue;
                }

                if (trueIndex < 0 || trueIndex >= level.SourceRecordCount)
                {
                    skippedEdits.Add($"{label}: source index is outside {level.DisplayName}'s source table.");
                    continue;
                }

                if (JsonValue.GetBoolean(edit, "removed") || string.Equals(JsonValue.GetString(edit, "editKind"), "remove", StringComparison.OrdinalIgnoreCase))
                {
                    AddRemoveHidePatches(imageStream, layout, level, tableWadOffset, trueIndex, label, patches, writtenWadOffsets);
                    exportedTreasureDelta += ComputeTreasureDelta(edit);
                    continue;
                }

                if (TryGetCrossLevelTemplate(edit, out JsonElement slotCandidateTemplate) &&
                    IsCrossLevelExistingSlotCandidateTemplate(slotCandidateTemplate))
                {
                    GreenWizardResidentSwapBuildMode residentBuildMode = allowPlanOnlyActorPackageImports
                        ? GreenWizardResidentSwapBuildMode.Candidate
                        : GreenWizardResidentSwapBuildMode.Normal;
                    string slotFamily = JsonValue.GetString(slotCandidateTemplate, "family");
                    string slotSourceLevelKey = JsonValue.GetString(slotCandidateTemplate, "sourceLevelKey");
                    int slotSourceTrueIndex = JsonValue.GetInt32(slotCandidateTemplate, "sourceTrueIndex", -1);
                    bool normalResidentWizard =
                        GreenWizardResidentSwapComposer.IsTemplateEligible(
                            level,
                            slotFamily,
                            slotSourceLevelKey,
                            slotSourceTrueIndex,
                            GreenWizardResidentSwapBuildMode.Normal,
                            out _,
                            out _);
                    if (!allowPlanOnlyActorPackageImports && !normalResidentWizard)
                    {
                        string sourceLevelName = JsonValue.GetString(slotCandidateTemplate, "sourceLevelName", "another level");
                        string source = slotSourceTrueIndex >= 0 ? $"{sourceLevelName} T{slotSourceTrueIndex}" : sourceLevelName;
                        skippedEdits.Add(
                            $"{label}: guarded cross-level existing-slot swap from {source} stays saved but is skipped by normal Create BIN. " +
                            "Use Create Swap Test to write a disposable DuckStation candidate.");
                        continue;
                    }

                    if (TryPatchCrossLevelSourceRecordIntoExistingSlotCandidate(
                        imageStream,
                        layout,
                        catalog,
                        level,
                        levelGeometry,
                        tableWadOffset,
                        tableRelativeOffset,
                        trueIndex,
                        label,
                        edit,
                        slotCandidateTemplate,
                        residentBuildMode,
                        patches,
                        writtenWadOffsets,
                        sharedCrossLevelSpecialClusters,
                        skippedEdits))
                    {
                        exportedTreasureDelta += ComputeTreasureDelta(edit);
                    }
                    continue;
                }

                if (TryPatchSourceRecordCloneIntoSlot(imageStream, layout, catalog, level, levelGeometry, tableWadOffset, trueIndex, label, edit, patches, writtenWadOffsets, skippedEdits))
                {
                    exportedTreasureDelta += ComputeTreasureDelta(edit);
                    continue;
                }

                if (TryGetCrossLevelTemplate(edit, out JsonElement crossLevelTemplate) && !IsSimpleCrossLevelTemplate(crossLevelTemplate))
                {
                    MobyActorPackageImportPreview preview = AddActorPackageImportPreview(imageStream, layout, catalog, level, tableWadOffset, tableRelativeOffset, label, crossLevelTemplate, sourceImagePath, workspaceRoot, allowPlanOnlyActorPackageImports, packageImportPreviews, patches, writtenWadOffsets, writtenActorPackageRecipes);
                    string templateId = JsonValue.GetString(crossLevelTemplate, "id", "cross-level template");
                    string requiredFeature = JsonValue.GetString(crossLevelTemplate, "requiredExporterFeature");
                    if (!preview.CanWriteImage)
                    {
                        skippedEdits.Add($"{label}: {templateId} identity bytes can be patched, but the required actor package step ({requiredFeature}) is still guarded preview-only.");
                        continue;
                    }
                    else if (!TryPatchExistingCrossLevelSpecialData(imageStream, layout, catalog, level, tableWadOffset, tableRelativeOffset, trueIndex, label, crossLevelTemplate, patches, writtenWadOffsets, skippedEdits))
                        continue;
                }

                AddCoordinatePatches(imageStream, layout, level, tableWadOffset, trueIndex, label, edit, patches, writtenWadOffsets);
                TrackMovedPortalSourceData(level, trueIndex, label, edit, portalSourceData, portalMovements);
                AddMovedDragonRescueCameraPatches(imageStream, layout, level, tableWadOffset, trueIndex, label, edit, dragonRescueCameras, patches, writtenWadOffsets);
                AddMovedExistingPlacementSectorPatch(imageStream, layout, level, levelGeometry, tableWadOffset, trueIndex, label, edit, patches, writtenWadOffsets);
                AddChangedBytePatch(imageStream, layout, level, tableWadOffset, trueIndex, label, "type", TypeOffset, edit, "typeOriginalHex", "typeEditedHex", patches, writtenWadOffsets);
                AddChangedBytePatch(imageStream, layout, level, tableWadOffset, trueIndex, label, "state", StateOffset, edit, "stateOriginalHex", "stateEditedHex", patches, writtenWadOffsets);
                AddYawPatches(imageStream, layout, level, tableWadOffset, trueIndex, label, edit, patches, writtenWadOffsets);
                AddSourceByteEdits(imageStream, layout, level, tableWadOffset, trueIndex, label, edit, patches, writtenWadOffsets);
                exportedTreasureDelta += ComputeTreasureDelta(edit);
            }
            finally
            {
                editOutcomes.Add(new MobySourceEditOutcome(
                    EditorTrueIndex: trueIndex,
                    MobyLabel: label,
                    EditKind: editKind,
                    PatchKinds: patches.Skip(patchStart).Select(patch => patch.Kind)
                        .Concat(isArtisansLockedChestBundleEdit ? ["moby-record-append", "artisans-native-key-locked-chest-runtime-bundle-v2"] : [])
                        .ToArray(),
                    SkippedReasons: skippedEdits.Skip(skippedEditStart).ToArray(),
                    PackageOutcomes: packageImportPreviews
                        .Skip(packagePreviewStart)
                        .Select(preview => new MobySourceEditPackageOutcome(
                            preview.TemplateId,
                            preview.RecipeId,
                            preview.RecipeStatus,
                            preview.CanWriteImage,
                            preview.GuardReason))
                        .ToArray(),
                    SafetyFindings: editSafetyFindings.ToArray()));
            }
        }

        AddMovedPortalSourcePatches(
            imageStream,
            layout,
            level,
            portalSourceData,
            portalMovements,
            patches,
            writtenWadOffsets);

        bool addStoneHillSpringChestRewardRowOnly = packageImportPreviews.Any(preview =>
            string.Equals(preview.Family, "springChest", StringComparison.OrdinalIgnoreCase) &&
            preview.CanWriteImage &&
            preview.RecipeMode.Contains("RewardRowNoExeHelper", StringComparison.OrdinalIgnoreCase));
        bool addStoneHillSpringChestNativeRewardRows = packageImportPreviews.Any(preview =>
            string.Equals(preview.Family, "springChest", StringComparison.OrdinalIgnoreCase) &&
            preview.CanWriteImage &&
            preview.RecipeMode.Contains("NativeRewardRowsNoExeHelper", StringComparison.OrdinalIgnoreCase));
        bool addStoneHillSpringChestNativeContextRows = packageImportPreviews.Any(preview =>
            string.Equals(preview.Family, "springChest", StringComparison.OrdinalIgnoreCase) &&
            preview.CanWriteImage &&
            preview.RecipeMode.Contains("NativeContextRowsNoExeHelper", StringComparison.OrdinalIgnoreCase));
        bool addStoneHillSpringChestNativeContextOnlyRows = packageImportPreviews.Any(preview =>
            string.Equals(preview.Family, "springChest", StringComparison.OrdinalIgnoreCase) &&
            preview.CanWriteImage &&
            preview.RecipeMode.Contains("NativeContextOnlyRowsNoExeHelper", StringComparison.OrdinalIgnoreCase));
        bool addStoneHillSpringChestNativeRewardOnlyRows = packageImportPreviews.Any(preview =>
            string.Equals(preview.Family, "springChest", StringComparison.OrdinalIgnoreCase) &&
            preview.CanWriteImage &&
            preview.RecipeMode.Contains("NativeRewardOnlyRowsNoExeHelper", StringComparison.OrdinalIgnoreCase));
        bool addStoneHillSpringChestNativeContainedHelperRows = packageImportPreviews.Any(preview =>
            string.Equals(preview.Family, "springChest", StringComparison.OrdinalIgnoreCase) &&
            preview.CanWriteImage &&
            preview.RecipeMode.Contains("NativeContainedHelperRowsNoExeHelper", StringComparison.OrdinalIgnoreCase));
        bool addStoneHillSpringChestArmOnlyHelper = packageImportPreviews.Any(preview =>
            string.Equals(preview.Family, "springChest", StringComparison.OrdinalIgnoreCase) &&
            preview.CanWriteImage &&
            preview.RecipeMode.Contains("ArmOnlyHelper", StringComparison.OrdinalIgnoreCase));
        bool addStoneHillSpringChestSafeOneShotHelper = packageImportPreviews.Any(preview =>
            string.Equals(preview.Family, "springChest", StringComparison.OrdinalIgnoreCase) &&
            preview.CanWriteImage &&
            preview.RecipeMode.Contains("SafeOneShotStackHelper", StringComparison.OrdinalIgnoreCase));
        bool addStoneHillSpringChestSafeVisibleOneShotCleanupHelper = packageImportPreviews.Any(preview =>
            string.Equals(preview.Family, "springChest", StringComparison.OrdinalIgnoreCase) &&
            preview.CanWriteImage &&
            preview.RecipeMode.Contains("SafeVisibleOneShotCleanupStackHelper", StringComparison.OrdinalIgnoreCase));
        bool addStoneHillSpringChestSafeLooseGemOneShotCleanupHelper = packageImportPreviews.Any(preview =>
            string.Equals(preview.Family, "springChest", StringComparison.OrdinalIgnoreCase) &&
            preview.CanWriteImage &&
            preview.RecipeMode.Contains("SafeLooseGemOneShotCleanupStackHelper", StringComparison.OrdinalIgnoreCase));
        bool addStoneHillSpringChestSafeLooseVisibleGemOneShotCleanupHelper = packageImportPreviews.Any(preview =>
            string.Equals(preview.Family, "springChest", StringComparison.OrdinalIgnoreCase) &&
            preview.CanWriteImage &&
            preview.RecipeMode.Contains("SafeLooseVisibleGemOneShotCleanupStackHelper", StringComparison.OrdinalIgnoreCase));
        bool addStoneHillSpringChestSafeLowVisibleGemOneShotCleanupHelper = packageImportPreviews.Any(preview =>
            string.Equals(preview.Family, "springChest", StringComparison.OrdinalIgnoreCase) &&
            preview.CanWriteImage &&
            preview.RecipeMode.Contains("SafeLowVisibleGemOneShotCleanupStackHelper", StringComparison.OrdinalIgnoreCase));
        bool addStoneHillSpringChestSafePreinitializedVisibleGemOneShotCleanupHelper = packageImportPreviews.Any(preview =>
            string.Equals(preview.Family, "springChest", StringComparison.OrdinalIgnoreCase) &&
            preview.CanWriteImage &&
            preview.RecipeMode.Contains("SafePreinitializedVisibleGemOneShotCleanupStackHelper", StringComparison.OrdinalIgnoreCase));
        bool addStoneHillSpringChestSafePreinitializedVisibleGemReturnLoopHelper = packageImportPreviews.Any(preview =>
            string.Equals(preview.Family, "springChest", StringComparison.OrdinalIgnoreCase) &&
            preview.CanWriteImage &&
            preview.RecipeMode.Contains("SafePreinitializedVisibleGemReturnLoopStackHelper", StringComparison.OrdinalIgnoreCase));
        bool addStoneHillSpringChestSafePreinitializedVisibleGemSpringArcHelper = packageImportPreviews.Any(preview =>
            string.Equals(preview.Family, "springChest", StringComparison.OrdinalIgnoreCase) &&
            preview.CanWriteImage &&
            preview.RecipeMode.Contains("SafePreinitializedVisibleGemSpringArcStackHelper", StringComparison.OrdinalIgnoreCase));
        bool addStoneHillSpringChestSafePreinitializedVisibleGemSnappyArcHelper = packageImportPreviews.Any(preview =>
            string.Equals(preview.Family, "springChest", StringComparison.OrdinalIgnoreCase) &&
            preview.CanWriteImage &&
            preview.RecipeMode.Contains("SafePreinitializedVisibleGemSnappyArcStackHelper", StringComparison.OrdinalIgnoreCase));
        bool addStoneHillSpringChestSafePreinitializedVisibleGemEarlyParkArcHelper = packageImportPreviews.Any(preview =>
            string.Equals(preview.Family, "springChest", StringComparison.OrdinalIgnoreCase) &&
            preview.CanWriteImage &&
            preview.RecipeMode.Contains("SafePreinitializedVisibleGemEarlyParkArcStackHelper", StringComparison.OrdinalIgnoreCase));
        bool addStoneHillSpringChestSafePreinitializedVisibleGemHeightTuneArcHelper = packageImportPreviews.Any(preview =>
            string.Equals(preview.Family, "springChest", StringComparison.OrdinalIgnoreCase) &&
            preview.CanWriteImage &&
            preview.RecipeMode.Contains("SafePreinitializedVisibleGemHeightTuneArcStackHelper", StringComparison.OrdinalIgnoreCase));
        bool addStoneHillSpringChestSafePreinitializedVisibleGemHigherArcHelper = packageImportPreviews.Any(preview =>
            string.Equals(preview.Family, "springChest", StringComparison.OrdinalIgnoreCase) &&
            preview.CanWriteImage &&
            preview.RecipeMode.Contains("SafePreinitializedVisibleGemHigherArcStackHelper", StringComparison.OrdinalIgnoreCase));
        bool addStoneHillSpringChestSafePreinitializedVisibleGemFullReturnArcHelper = packageImportPreviews.Any(preview =>
            string.Equals(preview.Family, "springChest", StringComparison.OrdinalIgnoreCase) &&
            preview.CanWriteImage &&
            preview.RecipeMode.Contains("SafePreinitializedVisibleGemFullReturnArcStackHelper", StringComparison.OrdinalIgnoreCase));
        bool addStoneHillSpringChestSafePreinitializedVisibleGemHangReturnArcHelper = packageImportPreviews.Any(preview =>
            string.Equals(preview.Family, "springChest", StringComparison.OrdinalIgnoreCase) &&
            preview.CanWriteImage &&
            preview.RecipeMode.Contains("SafePreinitializedVisibleGemHangReturnArcStackHelper", StringComparison.OrdinalIgnoreCase));
        bool addStoneHillSpringChestSafePreinitializedVisibleGemNoSparxHangReturnArcHelper = packageImportPreviews.Any(preview =>
            string.Equals(preview.Family, "springChest", StringComparison.OrdinalIgnoreCase) &&
            preview.CanWriteImage &&
            preview.RecipeMode.Contains("SafePreinitializedVisibleGemNoSparxHangReturnArcStackHelper", StringComparison.OrdinalIgnoreCase));
        bool addStoneHillSpringChestSafePreinitializedVisibleGemEarlyPickupRearmHangReturnArcHelper = packageImportPreviews.Any(preview =>
            string.Equals(preview.Family, "springChest", StringComparison.OrdinalIgnoreCase) &&
            preview.CanWriteImage &&
            preview.RecipeMode.Contains("SafePreinitializedVisibleGemEarlyPickupRearmHangReturnArcStackHelper", StringComparison.OrdinalIgnoreCase));
        bool addStoneHillSpringChestSafePreinitializedVisibleGemUnlistedHangReturnArcHelper = packageImportPreviews.Any(preview =>
            string.Equals(preview.Family, "springChest", StringComparison.OrdinalIgnoreCase) &&
            preview.CanWriteImage &&
            preview.RecipeMode.Contains("SafePreinitializedVisibleGemUnlistedHangReturnArcStackHelper", StringComparison.OrdinalIgnoreCase));
        bool addStoneHillSpringChestSafePreinitializedVisibleGemHangReturnArcInertParkHelper = packageImportPreviews.Any(preview =>
            string.Equals(preview.Family, "springChest", StringComparison.OrdinalIgnoreCase) &&
            preview.CanWriteImage &&
            (preview.RecipeMode.Contains("SafePreinitializedVisibleGemHangReturnArcInertPark", StringComparison.OrdinalIgnoreCase) ||
             preview.RecipeMode.Contains("SafePreinitializedVisibleGemHangReturnArcPostReturnFullQuarantineRearm", StringComparison.OrdinalIgnoreCase) ||
             preview.RecipeMode.Contains("SafePreinitializedVisibleGemHangReturnArcPostReturnFullQuarantineDelayedCollectRearm", StringComparison.OrdinalIgnoreCase) ||
             preview.RecipeMode.Contains("SafePreinitializedVisibleGemHangReturnArcPostReturnFullQuarantineDelayedIdentityRearm", StringComparison.OrdinalIgnoreCase) ||
             preview.RecipeMode.Contains("SafePreinitializedVisibleGemHangReturnArcPostReturnFullQuarantineScriptedMotionRearm", StringComparison.OrdinalIgnoreCase) ||
             preview.RecipeMode.Contains("SafeBlankScratchVisibleGemHangReturnArcPostReturnFullBlankScriptedMotionRearm", StringComparison.OrdinalIgnoreCase) ||
             preview.RecipeMode.Contains("SafeVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankScriptedMotionRearm", StringComparison.OrdinalIgnoreCase) ||
             preview.RecipeMode.Contains("SafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorShapeScriptedMotionRearm", StringComparison.OrdinalIgnoreCase) ||
             preview.RecipeMode.Contains("SafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorShapeNoValueScriptedMotionRearm", StringComparison.OrdinalIgnoreCase) ||
             preview.RecipeMode.Contains("SafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorVisualOnlyScriptedMotionRearm", StringComparison.OrdinalIgnoreCase) ||
             preview.RecipeMode.Contains("SafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorTypeOnlyScriptedMotionRearm", StringComparison.OrdinalIgnoreCase) ||
             preview.RecipeMode.Contains("SafePreinitializedVisibleGemHangReturnArcPostReturnQuarantineRearm", StringComparison.OrdinalIgnoreCase) ||
             preview.RecipeMode.Contains("SafePreinitializedVisibleGemHangReturnArcPostReturnUnlinkRearm", StringComparison.OrdinalIgnoreCase) ||
             preview.RecipeMode.Contains("SafePreinitializedVisibleGemHangReturnArcPostReturnUnlink", StringComparison.OrdinalIgnoreCase) ||
             preview.RecipeMode.Contains("SafePreinitializedVisibleGemHangReturnArcPostReturnMonitor", StringComparison.OrdinalIgnoreCase) ||
             preview.RecipeMode.Contains("SafePreinitializedVisibleGemHangReturnArcRewardQuarantine", StringComparison.OrdinalIgnoreCase)));
        bool addStoneHillSpringChestRowRepairHelper = packageImportPreviews.Any(preview =>
            string.Equals(preview.Family, "springChest", StringComparison.OrdinalIgnoreCase) &&
            preview.CanWriteImage &&
            preview.RecipeMode.Contains("RowRepairHelper", StringComparison.OrdinalIgnoreCase));
        bool addStoneHillSpringChestStackHookOnlyHelper = packageImportPreviews.Any(preview =>
            string.Equals(preview.Family, "springChest", StringComparison.OrdinalIgnoreCase) &&
            preview.CanWriteImage &&
            preview.RecipeMode.Contains("StackHookOnly", StringComparison.OrdinalIgnoreCase));
        bool addStoneHillSpringChestEntryHookOnlyHelper = packageImportPreviews.Any(preview =>
            string.Equals(preview.Family, "springChest", StringComparison.OrdinalIgnoreCase) &&
            preview.CanWriteImage &&
            preview.RecipeMode.Contains("EntryHookOnly", StringComparison.OrdinalIgnoreCase));
        bool addStoneHillSpringChestHookOnlyHelper = packageImportPreviews.Any(preview =>
            string.Equals(preview.Family, "springChest", StringComparison.OrdinalIgnoreCase) &&
            preview.CanWriteImage &&
            preview.RecipeMode.Contains("HookOnly", StringComparison.OrdinalIgnoreCase));
        int stoneHillSpringChestRuntimeVariantId = ResolveStoneHillSpringChestRuntimeVariantId(packageImportPreviews);
        bool addStoneHillSpringChestSafePreinitializedVisibleGemHangReturnArcPostReturnUnlinkRearmHelper = packageImportPreviews.Any(preview =>
            string.Equals(preview.Family, "springChest", StringComparison.OrdinalIgnoreCase) &&
            preview.CanWriteImage &&
            preview.RecipeMode.Contains("SafePreinitializedVisibleGemHangReturnArcPostReturnUnlinkRearm", StringComparison.OrdinalIgnoreCase));
        bool addStoneHillSpringChestSafePreinitializedVisibleGemHangReturnArcPostReturnQuarantineRearmHelper = packageImportPreviews.Any(preview =>
            string.Equals(preview.Family, "springChest", StringComparison.OrdinalIgnoreCase) &&
            preview.CanWriteImage &&
            preview.RecipeMode.Contains("SafePreinitializedVisibleGemHangReturnArcPostReturnQuarantineRearm", StringComparison.OrdinalIgnoreCase));
        bool addStoneHillSpringChestSafePreinitializedVisibleGemHangReturnArcPostReturnFullQuarantineRearmHelper = packageImportPreviews.Any(preview =>
            string.Equals(preview.Family, "springChest", StringComparison.OrdinalIgnoreCase) &&
            preview.CanWriteImage &&
            preview.RecipeMode.Contains("SafePreinitializedVisibleGemHangReturnArcPostReturnFullQuarantineRearm", StringComparison.OrdinalIgnoreCase));
        bool addStoneHillSpringChestSafePreinitializedVisibleGemHangReturnArcPostReturnFullQuarantineDelayedCollectRearmHelper = packageImportPreviews.Any(preview =>
            string.Equals(preview.Family, "springChest", StringComparison.OrdinalIgnoreCase) &&
            preview.CanWriteImage &&
            preview.RecipeMode.Contains("SafePreinitializedVisibleGemHangReturnArcPostReturnFullQuarantineDelayedCollectRearm", StringComparison.OrdinalIgnoreCase));
        bool addStoneHillSpringChestSafePreinitializedVisibleGemHangReturnArcPostReturnFullQuarantineDelayedIdentityRearmHelper = packageImportPreviews.Any(preview =>
            string.Equals(preview.Family, "springChest", StringComparison.OrdinalIgnoreCase) &&
            preview.CanWriteImage &&
            preview.RecipeMode.Contains("SafePreinitializedVisibleGemHangReturnArcPostReturnFullQuarantineDelayedIdentityRearm", StringComparison.OrdinalIgnoreCase));
        bool addStoneHillSpringChestSafePreinitializedVisibleGemHangReturnArcPostReturnFullQuarantineScriptedMotionRearmHelper = packageImportPreviews.Any(preview =>
            string.Equals(preview.Family, "springChest", StringComparison.OrdinalIgnoreCase) &&
            preview.CanWriteImage &&
            preview.RecipeMode.Contains("SafePreinitializedVisibleGemHangReturnArcPostReturnFullQuarantineScriptedMotionRearm", StringComparison.OrdinalIgnoreCase));
        bool useStoneHillSpringChestScriptedMotionNoValueDelay = packageImportPreviews.Any(preview =>
            string.Equals(preview.Family, "springChest", StringComparison.OrdinalIgnoreCase) &&
            preview.CanWriteImage &&
            preview.RecipeMode.Contains("ScriptedMotionRearmNoValueDelay", StringComparison.OrdinalIgnoreCase));
        bool addStoneHillSpringChestSafeBlankScratchVisibleGemHangReturnArcPostReturnFullBlankScriptedMotionRearmHelper = packageImportPreviews.Any(preview =>
            string.Equals(preview.Family, "springChest", StringComparison.OrdinalIgnoreCase) &&
            preview.CanWriteImage &&
            preview.RecipeMode.Contains("SafeBlankScratchVisibleGemHangReturnArcPostReturnFullBlankScriptedMotionRearm", StringComparison.OrdinalIgnoreCase));
        bool addStoneHillSpringChestSafeVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankScriptedMotionRearmHelper = packageImportPreviews.Any(preview =>
            string.Equals(preview.Family, "springChest", StringComparison.OrdinalIgnoreCase) &&
            preview.CanWriteImage &&
            preview.RecipeMode.Contains("SafeVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankScriptedMotionRearm", StringComparison.OrdinalIgnoreCase));
        bool addStoneHillSpringChestSafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorShapeScriptedMotionRearmHelper = packageImportPreviews.Any(preview =>
            string.Equals(preview.Family, "springChest", StringComparison.OrdinalIgnoreCase) &&
            preview.CanWriteImage &&
            preview.RecipeMode.Contains("SafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorShapeScriptedMotionRearm", StringComparison.OrdinalIgnoreCase));
        bool addStoneHillSpringChestSafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorShapeNoValueScriptedMotionRearmHelper = packageImportPreviews.Any(preview =>
            string.Equals(preview.Family, "springChest", StringComparison.OrdinalIgnoreCase) &&
            preview.CanWriteImage &&
            preview.RecipeMode.Contains("SafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorShapeNoValueScriptedMotionRearm", StringComparison.OrdinalIgnoreCase));
        bool addStoneHillSpringChestSafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorVisualOnlyScriptedMotionRearmHelper = packageImportPreviews.Any(preview =>
            string.Equals(preview.Family, "springChest", StringComparison.OrdinalIgnoreCase) &&
            preview.CanWriteImage &&
            preview.RecipeMode.Contains("SafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorVisualOnlyScriptedMotionRearm", StringComparison.OrdinalIgnoreCase));
        bool addStoneHillSpringChestSafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorTypeOnlyScriptedMotionRearmHelper = packageImportPreviews.Any(preview =>
            string.Equals(preview.Family, "springChest", StringComparison.OrdinalIgnoreCase) &&
            preview.CanWriteImage &&
            preview.RecipeMode.Contains("SafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorTypeOnlyScriptedMotionRearm", StringComparison.OrdinalIgnoreCase));
        bool addStoneHillSpringChestSafeNativeSpringEffectOnlyHelper = packageImportPreviews.Any(preview =>
            string.Equals(preview.Family, "springChest", StringComparison.OrdinalIgnoreCase) &&
            preview.CanWriteImage &&
            preview.RecipeMode.Contains("SafeNativeSpringEffectOnlyStackHelper", StringComparison.OrdinalIgnoreCase));
        bool useStoneHillSpringChestBlueGemNativeSpringEffectVisual = packageImportPreviews.Any(preview =>
            string.Equals(preview.Family, "springChest", StringComparison.OrdinalIgnoreCase) &&
            preview.CanWriteImage &&
            preview.RecipeMode.Contains("BlueGemNativeSpringEffectVisual", StringComparison.OrdinalIgnoreCase));
        bool useStoneHillSpringChestBlueGemNativeSpringEffectOrdinal = packageImportPreviews.Any(preview =>
            string.Equals(preview.Family, "springChest", StringComparison.OrdinalIgnoreCase) &&
            preview.CanWriteImage &&
            preview.RecipeMode.Contains("BlueGemNativeSpringEffectOrdinal", StringComparison.OrdinalIgnoreCase));
        bool useStoneHillSpringChestManualSpyroTouchCollectNativeEffectVisual = packageImportPreviews.Any(preview =>
            string.Equals(preview.Family, "springChest", StringComparison.OrdinalIgnoreCase) &&
            preview.CanWriteImage &&
            preview.RecipeMode.Contains("ManualSpyroTouchCollectNativeEffectVisual", StringComparison.OrdinalIgnoreCase));
        bool useStoneHillSpringChestManualAwardTreasureCounters = packageImportPreviews.Any(preview =>
            string.Equals(preview.Family, "springChest", StringComparison.OrdinalIgnoreCase) &&
            preview.CanWriteImage &&
            (preview.RecipeMode.Contains("AwardTotalJewels", StringComparison.OrdinalIgnoreCase) ||
             preview.RecipeMode.Contains("AwardDualTreasureCounters", StringComparison.OrdinalIgnoreCase) ||
             preview.RecipeMode.Contains("HoldDualTreasureCounters", StringComparison.OrdinalIgnoreCase)));
        bool useStoneHillSpringChestManualAwardNativeTreasureRoutine = packageImportPreviews.Any(preview =>
            string.Equals(preview.Family, "springChest", StringComparison.OrdinalIgnoreCase) &&
            preview.CanWriteImage &&
            preview.RecipeMode.Contains("AwardNativeTreasureRoutine", StringComparison.OrdinalIgnoreCase));
        bool useStoneHillSpringChestManualAwardFixedTreasureWords = packageImportPreviews.Any(preview =>
            string.Equals(preview.Family, "springChest", StringComparison.OrdinalIgnoreCase) &&
            preview.CanWriteImage &&
            preview.RecipeMode.Contains("AwardFixedTreasureWords", StringComparison.OrdinalIgnoreCase));
        bool useStoneHillSpringChestManualRewardPopArc = packageImportPreviews.Any(preview =>
            string.Equals(preview.Family, "springChest", StringComparison.OrdinalIgnoreCase) &&
            preview.CanWriteImage &&
            preview.RecipeMode.Contains("ManualRewardPopArc", StringComparison.OrdinalIgnoreCase));
        bool useStoneHillSpringChestNativeStateZeroShellWords = packageImportPreviews.Any(preview =>
            string.Equals(preview.Family, "springChest", StringComparison.OrdinalIgnoreCase) &&
            preview.CanWriteImage &&
            preview.RecipeMode.Contains("NativeStateZeroShell", StringComparison.OrdinalIgnoreCase));
        bool useStoneHillSpringChestNativeReadyShellWords = packageImportPreviews.Any(preview =>
            string.Equals(preview.Family, "springChest", StringComparison.OrdinalIgnoreCase) &&
            preview.CanWriteImage &&
            preview.RecipeMode.Contains("NativeReadyShell", StringComparison.OrdinalIgnoreCase));
        bool useStoneHillSpringChestNativePreHitShellLifecycleWords = packageImportPreviews.Any(preview =>
            string.Equals(preview.Family, "springChest", StringComparison.OrdinalIgnoreCase) &&
            preview.CanWriteImage &&
            preview.RecipeMode.Contains("NativePreHitShellLifecycle", StringComparison.OrdinalIgnoreCase));
        bool addStoneHillSpringChestSafePreinitializedVisibleGemHangReturnArcPostReturnUnlinkHelper = packageImportPreviews.Any(preview =>
            string.Equals(preview.Family, "springChest", StringComparison.OrdinalIgnoreCase) &&
            preview.CanWriteImage &&
            preview.RecipeMode.Contains("SafePreinitializedVisibleGemHangReturnArcPostReturnUnlink", StringComparison.OrdinalIgnoreCase));
        suppressStoneHillSpringChestHelper = suppressStoneHillSpringChestHelper || suppressActorPackageImports || packageImportPreviews.Any(preview =>
            string.Equals(preview.Family, "springChest", StringComparison.OrdinalIgnoreCase) &&
            preview.CanWriteImage &&
            (preview.RecipeMode.Contains("NoExeHelper", StringComparison.OrdinalIgnoreCase) ||
             preview.RecipeMode.Contains("RecordsOnly", StringComparison.OrdinalIgnoreCase) ||
             preview.RecipeMode.Contains("CopyOnlyNoRoot", StringComparison.OrdinalIgnoreCase)));

        if ((addStoneHillSpringChestNativeRewardRows || addStoneHillSpringChestNativeContextRows || addStoneHillSpringChestNativeContextOnlyRows || addStoneHillSpringChestNativeRewardOnlyRows || addStoneHillSpringChestNativeContainedHelperRows) &&
            TryAddStoneHillSpringChestNativeRewardRows(
                imageStream,
                layout,
                catalog,
                level,
                tableWadOffset,
                tableRelativeOffset,
                appendNextTrueIndex,
                peaceKeepersSpringChestAnchors,
                patches,
                writtenWadOffsets,
                sharedCrossLevelSpecialClusters,
                skippedEdits,
                includeContextRows: addStoneHillSpringChestNativeContextRows,
                contextRowsOnly: addStoneHillSpringChestNativeContextOnlyRows,
                containedHelperRowsOnly: addStoneHillSpringChestNativeContainedHelperRows,
                useSharedClusterForRows: addStoneHillSpringChestNativeContextRows || addStoneHillSpringChestNativeContextOnlyRows || addStoneHillSpringChestNativeRewardOnlyRows,
                out int nativeRewardRowsAdded))
        {
            appendNextTrueIndex += nativeRewardRowsAdded;
            hasAppend = true;
        }
        else if (addStoneHillSpringChestRewardRowOnly &&
            springChestControllerAppendTrueIndex >= 0 &&
            springChestShellAppendTrueIndex >= 0 &&
            TryAddStoneHillSpringChestRewardRow(
            imageStream,
            layout,
            level,
            tableWadOffset,
            appendNextTrueIndex,
            patches,
            writtenWadOffsets))
        {
            appendNextTrueIndex++;
            hasAppend = true;
        }
        else if (addStoneHillSpringChestEntryHookOnlyHelper && TryAddStoneHillSpringChestEntryHookOnlyHelper(
            sourceImagePath,
            level,
            patches))
        {
        }
        else if (addStoneHillSpringChestStackHookOnlyHelper && TryAddStoneHillSpringChestStackHookOnlyHelper(
            sourceImagePath,
            level,
            patches))
        {
        }
        else if (addStoneHillSpringChestHookOnlyHelper && TryAddStoneHillSpringChestHookOnlyHelper(
            sourceImagePath,
            level,
            patches))
        {
        }
        else if (addStoneHillSpringChestRowRepairHelper && TryAddStoneHillSpringChestRowRepairHelper(
            sourceImagePath,
            level,
            springChestControllerAppendTrueIndex,
            springChestShellAppendTrueIndex,
            peaceKeepersSpringChestAnchors,
            patches))
        {
        }
        else if (addStoneHillSpringChestArmOnlyHelper && TryAddStoneHillSpringChestArmOnlyHelper(
            sourceImagePath,
            level,
            springChestControllerAppendTrueIndex,
            springChestShellAppendTrueIndex,
            patches,
            springChestControllerActorId,
            stoneHillSpringChestRuntimeVariantId))
        {
        }
        else if (addStoneHillSpringChestSafeNativeSpringEffectOnlyHelper && TryAddStoneHillSpringChestRewardRowAndHelper(
            imageStream,
            layout,
            level,
            tableWadOffset,
            springChestControllerAppendTrueIndex,
            springChestShellAppendTrueIndex,
            appendNextTrueIndex,
            sourceImagePath,
            patches,
            writtenWadOffsets,
            springChestControllerActorId,
            stoneHillSpringChestRuntimeVariantId,
            refreshRewardUntilListed: false,
            fallbackCleanupFrames: 72,
            rewardZOffset: 0x0300,
            uncollectedReturnFrames: useStoneHillSpringChestManualSpyroTouchCollectNativeEffectVisual ? 0 : 48,
            rewardReturnDropPerFrame: useStoneHillSpringChestManualSpyroTouchCollectNativeEffectVisual ? 0 : 0x30,
            rewardReturnDropStartFrames: useStoneHillSpringChestManualSpyroTouchCollectNativeEffectVisual ? 0 : 20,
            rewardRiseFrames: useStoneHillSpringChestManualSpyroTouchCollectNativeEffectVisual ? 0 : 4,
            rewardRisePerFrame: useStoneHillSpringChestManualSpyroTouchCollectNativeEffectVisual ? 0 : 0xC0,
            scriptRewardMotionWithoutPickupList: true,
            blankRewardRow: true,
            blankReturnedRewardRow: true,
            useNativeStateZeroShellWords: useStoneHillSpringChestNativeStateZeroShellWords || useStoneHillSpringChestNativeReadyShellWords || useStoneHillSpringChestNativePreHitShellLifecycleWords,
            useNativeReadyShellWords: useStoneHillSpringChestNativeReadyShellWords,
            useNativePreHitShellLifecycleWords: useStoneHillSpringChestNativePreHitShellLifecycleWords,
            useNativeSpringEffectRow: true,
            useBlueGemNativeSpringEffectVisual: useStoneHillSpringChestBlueGemNativeSpringEffectVisual,
            useBlueGemNativeSpringEffectOrdinal: useStoneHillSpringChestBlueGemNativeSpringEffectOrdinal,
            manualSpyroTouchCollectNativeEffectVisual: useStoneHillSpringChestManualSpyroTouchCollectNativeEffectVisual,
            manualAwardTreasureCounters: useStoneHillSpringChestManualAwardTreasureCounters,
            manualAwardNativeTreasureRoutine: useStoneHillSpringChestManualAwardNativeTreasureRoutine,
            manualAwardFixedTreasureWords: useStoneHillSpringChestManualAwardFixedTreasureWords,
            manualAnimateRewardPopArc: useStoneHillSpringChestManualRewardPopArc))
        {
            appendNextTrueIndex++;
            hasAppend = true;
        }
        else if (addStoneHillSpringChestSafePreinitializedVisibleGemHangReturnArcInertParkHelper && TryAddStoneHillSpringChestRewardRowAndHelper(
            imageStream,
            layout,
            level,
            tableWadOffset,
            springChestControllerAppendTrueIndex,
            springChestShellAppendTrueIndex,
            appendNextTrueIndex,
            sourceImagePath,
            patches,
            writtenWadOffsets,
            springChestControllerActorId,
            stoneHillSpringChestRuntimeVariantId,
            refreshRewardUntilListed: false,
            clearRewardRuntimeWord: false,
            fallbackCleanupFrames: 48,
            useVisibleGemTypeWord: true,
            rewardZOffset: 0x0480,
            preinitializeVisibleRewardRow: !addStoneHillSpringChestSafeBlankScratchVisibleGemHangReturnArcPostReturnFullBlankScriptedMotionRearmHelper && !addStoneHillSpringChestSafeVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankScriptedMotionRearmHelper && !addStoneHillSpringChestSafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorShapeScriptedMotionRearmHelper && !addStoneHillSpringChestSafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorShapeNoValueScriptedMotionRearmHelper && !addStoneHillSpringChestSafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorVisualOnlyScriptedMotionRearmHelper && !addStoneHillSpringChestSafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorTypeOnlyScriptedMotionRearmHelper,
            preservePreinitializedRewardRow: !addStoneHillSpringChestSafeBlankScratchVisibleGemHangReturnArcPostReturnFullBlankScriptedMotionRearmHelper && !addStoneHillSpringChestSafeVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankScriptedMotionRearmHelper && !addStoneHillSpringChestSafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorShapeScriptedMotionRearmHelper && !addStoneHillSpringChestSafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorShapeNoValueScriptedMotionRearmHelper && !addStoneHillSpringChestSafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorVisualOnlyScriptedMotionRearmHelper && !addStoneHillSpringChestSafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorTypeOnlyScriptedMotionRearmHelper,
            uncollectedReturnFrames: 56,
            rewardReturnDropPerFrame: 0x30,
            rewardReturnDropStartFrames: 28,
            rewardRiseFrames: 4,
            rewardRisePerFrame: 0xC0,
            maskAirborneRewardValue: addStoneHillSpringChestSafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorShapeNoValueScriptedMotionRearmHelper,
            unlinkAirborneRewardFromActiveList: true,
            parkReturnedRewardAsInert: !addStoneHillSpringChestSafePreinitializedVisibleGemHangReturnArcPostReturnUnlinkRearmHelper || addStoneHillSpringChestSafePreinitializedVisibleGemHangReturnArcPostReturnQuarantineRearmHelper || addStoneHillSpringChestSafePreinitializedVisibleGemHangReturnArcPostReturnFullQuarantineRearmHelper || addStoneHillSpringChestSafePreinitializedVisibleGemHangReturnArcPostReturnFullQuarantineDelayedCollectRearmHelper || addStoneHillSpringChestSafePreinitializedVisibleGemHangReturnArcPostReturnFullQuarantineDelayedIdentityRearmHelper || addStoneHillSpringChestSafePreinitializedVisibleGemHangReturnArcPostReturnFullQuarantineScriptedMotionRearmHelper || addStoneHillSpringChestSafeBlankScratchVisibleGemHangReturnArcPostReturnFullBlankScriptedMotionRearmHelper || addStoneHillSpringChestSafeVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankScriptedMotionRearmHelper || addStoneHillSpringChestSafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorShapeScriptedMotionRearmHelper || addStoneHillSpringChestSafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorShapeNoValueScriptedMotionRearmHelper || addStoneHillSpringChestSafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorVisualOnlyScriptedMotionRearmHelper || addStoneHillSpringChestSafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorTypeOnlyScriptedMotionRearmHelper,
            monitorReturnedRewardPickupList: addStoneHillSpringChestSafePreinitializedVisibleGemHangReturnArcPostReturnUnlinkHelper || addStoneHillSpringChestSafePreinitializedVisibleGemHangReturnArcPostReturnQuarantineRearmHelper || addStoneHillSpringChestSafePreinitializedVisibleGemHangReturnArcPostReturnFullQuarantineRearmHelper || addStoneHillSpringChestSafePreinitializedVisibleGemHangReturnArcPostReturnFullQuarantineDelayedCollectRearmHelper || addStoneHillSpringChestSafePreinitializedVisibleGemHangReturnArcPostReturnFullQuarantineDelayedIdentityRearmHelper || addStoneHillSpringChestSafePreinitializedVisibleGemHangReturnArcPostReturnFullQuarantineScriptedMotionRearmHelper,
            restoreReturnedRewardOnHit: addStoneHillSpringChestSafePreinitializedVisibleGemHangReturnArcPostReturnQuarantineRearmHelper || addStoneHillSpringChestSafePreinitializedVisibleGemHangReturnArcPostReturnFullQuarantineRearmHelper || addStoneHillSpringChestSafePreinitializedVisibleGemHangReturnArcPostReturnFullQuarantineDelayedCollectRearmHelper || addStoneHillSpringChestSafePreinitializedVisibleGemHangReturnArcPostReturnFullQuarantineDelayedIdentityRearmHelper || addStoneHillSpringChestSafePreinitializedVisibleGemHangReturnArcPostReturnFullQuarantineScriptedMotionRearmHelper,
            fullQuarantineReturnedReward: addStoneHillSpringChestSafePreinitializedVisibleGemHangReturnArcPostReturnFullQuarantineRearmHelper || addStoneHillSpringChestSafePreinitializedVisibleGemHangReturnArcPostReturnFullQuarantineDelayedCollectRearmHelper || addStoneHillSpringChestSafePreinitializedVisibleGemHangReturnArcPostReturnFullQuarantineDelayedIdentityRearmHelper || addStoneHillSpringChestSafePreinitializedVisibleGemHangReturnArcPostReturnFullQuarantineScriptedMotionRearmHelper,
            delayReturnedRewardCollection: addStoneHillSpringChestSafePreinitializedVisibleGemHangReturnArcPostReturnFullQuarantineDelayedCollectRearmHelper || addStoneHillSpringChestSafePreinitializedVisibleGemHangReturnArcPostReturnFullQuarantineDelayedIdentityRearmHelper || (addStoneHillSpringChestSafePreinitializedVisibleGemHangReturnArcPostReturnFullQuarantineScriptedMotionRearmHelper && !useStoneHillSpringChestScriptedMotionNoValueDelay),
            delayReturnedRewardTypeWord: addStoneHillSpringChestSafePreinitializedVisibleGemHangReturnArcPostReturnFullQuarantineDelayedIdentityRearmHelper,
            scriptRewardMotionWithoutPickupList: addStoneHillSpringChestSafePreinitializedVisibleGemHangReturnArcPostReturnFullQuarantineScriptedMotionRearmHelper || addStoneHillSpringChestSafeBlankScratchVisibleGemHangReturnArcPostReturnFullBlankScriptedMotionRearmHelper || addStoneHillSpringChestSafeVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankScriptedMotionRearmHelper || addStoneHillSpringChestSafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorShapeScriptedMotionRearmHelper || addStoneHillSpringChestSafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorShapeNoValueScriptedMotionRearmHelper || addStoneHillSpringChestSafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorVisualOnlyScriptedMotionRearmHelper || addStoneHillSpringChestSafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorTypeOnlyScriptedMotionRearmHelper,
            blankRewardRow: addStoneHillSpringChestSafeBlankScratchVisibleGemHangReturnArcPostReturnFullBlankScriptedMotionRearmHelper,
            visualScratchRewardRow: addStoneHillSpringChestSafeVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankScriptedMotionRearmHelper || addStoneHillSpringChestSafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorShapeScriptedMotionRearmHelper || addStoneHillSpringChestSafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorShapeNoValueScriptedMotionRearmHelper || addStoneHillSpringChestSafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorVisualOnlyScriptedMotionRearmHelper || addStoneHillSpringChestSafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorTypeOnlyScriptedMotionRearmHelper,
            clearVisualScratchRuntimeWord: addStoneHillSpringChestSafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorShapeScriptedMotionRearmHelper || addStoneHillSpringChestSafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorShapeNoValueScriptedMotionRearmHelper || addStoneHillSpringChestSafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorVisualOnlyScriptedMotionRearmHelper || addStoneHillSpringChestSafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorTypeOnlyScriptedMotionRearmHelper,
            blankReturnedRewardRow: addStoneHillSpringChestSafeBlankScratchVisibleGemHangReturnArcPostReturnFullBlankScriptedMotionRearmHelper || addStoneHillSpringChestSafeVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankScriptedMotionRearmHelper || addStoneHillSpringChestSafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorShapeScriptedMotionRearmHelper || addStoneHillSpringChestSafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorShapeNoValueScriptedMotionRearmHelper || addStoneHillSpringChestSafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorVisualOnlyScriptedMotionRearmHelper || addStoneHillSpringChestSafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorTypeOnlyScriptedMotionRearmHelper,
            preserveRewardVisualScaffold: addStoneHillSpringChestSafeVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankScriptedMotionRearmHelper,
            restoreDonorGemShapeOnPop: addStoneHillSpringChestSafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorShapeScriptedMotionRearmHelper || addStoneHillSpringChestSafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorShapeNoValueScriptedMotionRearmHelper || addStoneHillSpringChestSafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorVisualOnlyScriptedMotionRearmHelper || addStoneHillSpringChestSafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorTypeOnlyScriptedMotionRearmHelper,
            leaveDonorGemPickupTypeBlankOnPop: addStoneHillSpringChestSafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorVisualOnlyScriptedMotionRearmHelper,
            leaveDonorGemTailWordBlankOnPop: addStoneHillSpringChestSafeDormantVisualScratchVisibleGemHangReturnArcPostReturnIdentityBlankDonorTypeOnlyScriptedMotionRearmHelper,
            useNativeStateZeroShellWords: useStoneHillSpringChestNativeStateZeroShellWords || useStoneHillSpringChestNativeReadyShellWords || useStoneHillSpringChestNativePreHitShellLifecycleWords,
            useNativeReadyShellWords: useStoneHillSpringChestNativeReadyShellWords,
            useNativePreHitShellLifecycleWords: useStoneHillSpringChestNativePreHitShellLifecycleWords))
        {
            appendNextTrueIndex++;
            hasAppend = true;
        }
        else if (addStoneHillSpringChestSafePreinitializedVisibleGemUnlistedHangReturnArcHelper && TryAddStoneHillSpringChestRewardRowAndHelper(
            imageStream,
            layout,
            level,
            tableWadOffset,
            springChestControllerAppendTrueIndex,
            springChestShellAppendTrueIndex,
            appendNextTrueIndex,
            sourceImagePath,
            patches,
            writtenWadOffsets,
            springChestControllerActorId,
            stoneHillSpringChestRuntimeVariantId,
            refreshRewardUntilListed: false,
            clearRewardRuntimeWord: false,
            fallbackCleanupFrames: 48,
            useVisibleGemTypeWord: true,
            rewardZOffset: 0x0480,
            preinitializeVisibleRewardRow: true,
            preservePreinitializedRewardRow: true,
            uncollectedReturnFrames: 56,
            rewardReturnDropPerFrame: 0x30,
            rewardReturnDropStartFrames: 28,
            rewardRiseFrames: 4,
            rewardRisePerFrame: 0xC0,
            unlinkAirborneRewardFromActiveList: true))
        {
            appendNextTrueIndex++;
            hasAppend = true;
        }
        else if (addStoneHillSpringChestSafePreinitializedVisibleGemEarlyPickupRearmHangReturnArcHelper && TryAddStoneHillSpringChestRewardRowAndHelper(
            imageStream,
            layout,
            level,
            tableWadOffset,
            springChestControllerAppendTrueIndex,
            springChestShellAppendTrueIndex,
            appendNextTrueIndex,
            sourceImagePath,
            patches,
            writtenWadOffsets,
            springChestControllerActorId,
            stoneHillSpringChestRuntimeVariantId,
            refreshRewardUntilListed: false,
            clearRewardRuntimeWord: false,
            fallbackCleanupFrames: 48,
            useVisibleGemTypeWord: true,
            rewardZOffset: 0x0480,
            preinitializeVisibleRewardRow: true,
            preservePreinitializedRewardRow: true,
            uncollectedReturnFrames: 56,
            rewardReturnDropPerFrame: 0x30,
            rewardReturnDropStartFrames: 28,
            rewardRiseFrames: 4,
            rewardRisePerFrame: 0xC0,
            rearmOnEarlyRewardPickup: true))
        {
            appendNextTrueIndex++;
            hasAppend = true;
        }
        else if (addStoneHillSpringChestSafePreinitializedVisibleGemNoSparxHangReturnArcHelper && TryAddStoneHillSpringChestRewardRowAndHelper(
            imageStream,
            layout,
            level,
            tableWadOffset,
            springChestControllerAppendTrueIndex,
            springChestShellAppendTrueIndex,
            appendNextTrueIndex,
            sourceImagePath,
            patches,
            writtenWadOffsets,
            springChestControllerActorId,
            stoneHillSpringChestRuntimeVariantId,
            refreshRewardUntilListed: false,
            clearRewardRuntimeWord: false,
            fallbackCleanupFrames: 48,
            useVisibleGemTypeWord: true,
            rewardZOffset: 0x0480,
            preinitializeVisibleRewardRow: true,
            preservePreinitializedRewardRow: true,
            uncollectedReturnFrames: 56,
            rewardReturnDropPerFrame: 0x30,
            rewardReturnDropStartFrames: 28,
            rewardRiseFrames: 4,
            rewardRisePerFrame: 0xC0,
            maskAirborneRewardValue: true))
        {
            appendNextTrueIndex++;
            hasAppend = true;
        }
        else if (addStoneHillSpringChestSafePreinitializedVisibleGemHangReturnArcHelper && TryAddStoneHillSpringChestRewardRowAndHelper(
            imageStream,
            layout,
            level,
            tableWadOffset,
            springChestControllerAppendTrueIndex,
            springChestShellAppendTrueIndex,
            appendNextTrueIndex,
            sourceImagePath,
            patches,
            writtenWadOffsets,
            springChestControllerActorId,
            stoneHillSpringChestRuntimeVariantId,
            refreshRewardUntilListed: false,
            clearRewardRuntimeWord: false,
            fallbackCleanupFrames: 48,
            useVisibleGemTypeWord: true,
            rewardZOffset: 0x0480,
            preinitializeVisibleRewardRow: true,
            preservePreinitializedRewardRow: true,
            uncollectedReturnFrames: 56,
            rewardReturnDropPerFrame: 0x30,
            rewardReturnDropStartFrames: 28,
            rewardRiseFrames: 4,
            rewardRisePerFrame: 0xC0))
        {
            appendNextTrueIndex++;
            hasAppend = true;
        }
        else if (addStoneHillSpringChestSafePreinitializedVisibleGemFullReturnArcHelper && TryAddStoneHillSpringChestRewardRowAndHelper(
            imageStream,
            layout,
            level,
            tableWadOffset,
            springChestControllerAppendTrueIndex,
            springChestShellAppendTrueIndex,
            appendNextTrueIndex,
            sourceImagePath,
            patches,
            writtenWadOffsets,
            springChestControllerActorId,
            stoneHillSpringChestRuntimeVariantId,
            refreshRewardUntilListed: false,
            clearRewardRuntimeWord: false,
            fallbackCleanupFrames: 48,
            useVisibleGemTypeWord: true,
            rewardZOffset: 0x0480,
            preinitializeVisibleRewardRow: true,
            preservePreinitializedRewardRow: true,
            uncollectedReturnFrames: 28,
            rewardReturnDropPerFrame: 0x48,
            rewardReturnDropStartFrames: 0,
            rewardRiseFrames: 4,
            rewardRisePerFrame: 0xC0))
        {
            appendNextTrueIndex++;
            hasAppend = true;
        }
        else if (addStoneHillSpringChestSafePreinitializedVisibleGemHigherArcHelper && TryAddStoneHillSpringChestRewardRowAndHelper(
            imageStream,
            layout,
            level,
            tableWadOffset,
            springChestControllerAppendTrueIndex,
            springChestShellAppendTrueIndex,
            appendNextTrueIndex,
            sourceImagePath,
            patches,
            writtenWadOffsets,
            springChestControllerActorId,
            stoneHillSpringChestRuntimeVariantId,
            refreshRewardUntilListed: false,
            clearRewardRuntimeWord: false,
            fallbackCleanupFrames: 48,
            useVisibleGemTypeWord: true,
            rewardZOffset: 0x0480,
            preinitializeVisibleRewardRow: true,
            preservePreinitializedRewardRow: true,
            uncollectedReturnFrames: 12,
            rewardReturnDropPerFrame: 0x18,
            rewardReturnDropStartFrames: 0,
            rewardRiseFrames: 4,
            rewardRisePerFrame: 0xC0))
        {
            appendNextTrueIndex++;
            hasAppend = true;
        }
        else if (addStoneHillSpringChestSafePreinitializedVisibleGemHeightTuneArcHelper && TryAddStoneHillSpringChestRewardRowAndHelper(
            imageStream,
            layout,
            level,
            tableWadOffset,
            springChestControllerAppendTrueIndex,
            springChestShellAppendTrueIndex,
            appendNextTrueIndex,
            sourceImagePath,
            patches,
            writtenWadOffsets,
            springChestControllerActorId,
            stoneHillSpringChestRuntimeVariantId,
            refreshRewardUntilListed: false,
            clearRewardRuntimeWord: false,
            fallbackCleanupFrames: 48,
            useVisibleGemTypeWord: true,
            rewardZOffset: 0x0380,
            preinitializeVisibleRewardRow: true,
            preservePreinitializedRewardRow: true,
            uncollectedReturnFrames: 12,
            rewardReturnDropPerFrame: 0x18,
            rewardReturnDropStartFrames: 0,
            rewardRiseFrames: 3,
            rewardRisePerFrame: 0xA0))
        {
            appendNextTrueIndex++;
            hasAppend = true;
        }
        else if (addStoneHillSpringChestSafePreinitializedVisibleGemEarlyParkArcHelper && TryAddStoneHillSpringChestRewardRowAndHelper(
            imageStream,
            layout,
            level,
            tableWadOffset,
            springChestControllerAppendTrueIndex,
            springChestShellAppendTrueIndex,
            appendNextTrueIndex,
            sourceImagePath,
            patches,
            writtenWadOffsets,
            springChestControllerActorId,
            stoneHillSpringChestRuntimeVariantId,
            refreshRewardUntilListed: false,
            clearRewardRuntimeWord: false,
            fallbackCleanupFrames: 48,
            useVisibleGemTypeWord: true,
            rewardZOffset: 0x0300,
            preinitializeVisibleRewardRow: true,
            preservePreinitializedRewardRow: true,
            uncollectedReturnFrames: 12,
            rewardReturnDropPerFrame: 0x18,
            rewardReturnDropStartFrames: 0,
            rewardRiseFrames: 3,
            rewardRisePerFrame: 0x80))
        {
            appendNextTrueIndex++;
            hasAppend = true;
        }
        else if (addStoneHillSpringChestSafePreinitializedVisibleGemSnappyArcHelper && TryAddStoneHillSpringChestRewardRowAndHelper(
            imageStream,
            layout,
            level,
            tableWadOffset,
            springChestControllerAppendTrueIndex,
            springChestShellAppendTrueIndex,
            appendNextTrueIndex,
            sourceImagePath,
            patches,
            writtenWadOffsets,
            springChestControllerActorId,
            stoneHillSpringChestRuntimeVariantId,
            refreshRewardUntilListed: false,
            clearRewardRuntimeWord: false,
            fallbackCleanupFrames: 64,
            useVisibleGemTypeWord: true,
            rewardZOffset: 0x0280,
            preinitializeVisibleRewardRow: true,
            preservePreinitializedRewardRow: true,
            uncollectedReturnFrames: 28,
            rewardReturnDropPerFrame: 0x28,
            rewardReturnDropStartFrames: 0,
            rewardRiseFrames: 4,
            rewardRisePerFrame: 0xC0))
        {
            appendNextTrueIndex++;
            hasAppend = true;
        }
        else if (addStoneHillSpringChestSafePreinitializedVisibleGemSpringArcHelper && TryAddStoneHillSpringChestRewardRowAndHelper(
            imageStream,
            layout,
            level,
            tableWadOffset,
            springChestControllerAppendTrueIndex,
            springChestShellAppendTrueIndex,
            appendNextTrueIndex,
            sourceImagePath,
            patches,
            writtenWadOffsets,
            springChestControllerActorId,
            stoneHillSpringChestRuntimeVariantId,
            refreshRewardUntilListed: false,
            clearRewardRuntimeWord: false,
            fallbackCleanupFrames: 90,
            useVisibleGemTypeWord: true,
            rewardZOffset: 0x0240,
            preinitializeVisibleRewardRow: true,
            preservePreinitializedRewardRow: true,
            uncollectedReturnFrames: 80,
            rewardReturnDropPerFrame: 0x30,
            rewardReturnDropStartFrames: 0,
            rewardRiseFrames: 12,
            rewardRisePerFrame: 0x48))
        {
            appendNextTrueIndex++;
            hasAppend = true;
        }
        else if (addStoneHillSpringChestSafePreinitializedVisibleGemReturnLoopHelper && TryAddStoneHillSpringChestRewardRowAndHelper(
            imageStream,
            layout,
            level,
            tableWadOffset,
            springChestControllerAppendTrueIndex,
            springChestShellAppendTrueIndex,
            appendNextTrueIndex,
            sourceImagePath,
            patches,
            writtenWadOffsets,
            springChestControllerActorId,
            stoneHillSpringChestRuntimeVariantId,
            refreshRewardUntilListed: false,
            clearRewardRuntimeWord: false,
            fallbackCleanupFrames: 120,
            useVisibleGemTypeWord: true,
            preinitializeVisibleRewardRow: true,
            preservePreinitializedRewardRow: true,
            uncollectedReturnFrames: 150,
            rewardReturnDropPerFrame: 0x08,
            rewardReturnDropStartFrames: 30))
        {
            appendNextTrueIndex++;
            hasAppend = true;
        }
        else if (addStoneHillSpringChestSafePreinitializedVisibleGemOneShotCleanupHelper && TryAddStoneHillSpringChestRewardRowAndHelper(
            imageStream,
            layout,
            level,
            tableWadOffset,
            springChestControllerAppendTrueIndex,
            springChestShellAppendTrueIndex,
            appendNextTrueIndex,
            sourceImagePath,
            patches,
            writtenWadOffsets,
            springChestControllerActorId,
            stoneHillSpringChestRuntimeVariantId,
            refreshRewardUntilListed: false,
            clearRewardRuntimeWord: false,
            fallbackCleanupFrames: 120,
            useVisibleGemTypeWord: true,
            preinitializeVisibleRewardRow: true,
            preservePreinitializedRewardRow: true))
        {
            appendNextTrueIndex++;
            hasAppend = true;
        }
        else if (addStoneHillSpringChestSafeLowVisibleGemOneShotCleanupHelper && TryAddStoneHillSpringChestRewardRowAndHelper(
            imageStream,
            layout,
            level,
            tableWadOffset,
            springChestControllerAppendTrueIndex,
            springChestShellAppendTrueIndex,
            appendNextTrueIndex,
            sourceImagePath,
            patches,
            writtenWadOffsets,
            springChestControllerActorId,
            stoneHillSpringChestRuntimeVariantId,
            refreshRewardUntilListed: false,
            clearRewardRuntimeWord: true,
            fallbackCleanupFrames: 120,
            useLooseGemRowShape: true,
            useVisibleGemTypeWord: true,
            rewardZOffset: 0x80))
        {
            appendNextTrueIndex++;
            hasAppend = true;
        }
        else if (addStoneHillSpringChestSafeLooseVisibleGemOneShotCleanupHelper && TryAddStoneHillSpringChestRewardRowAndHelper(
            imageStream,
            layout,
            level,
            tableWadOffset,
            springChestControllerAppendTrueIndex,
            springChestShellAppendTrueIndex,
            appendNextTrueIndex,
            sourceImagePath,
            patches,
            writtenWadOffsets,
            springChestControllerActorId,
            stoneHillSpringChestRuntimeVariantId,
            refreshRewardUntilListed: false,
            clearRewardRuntimeWord: true,
            fallbackCleanupFrames: 120,
            useLooseGemRowShape: true,
            useVisibleGemTypeWord: true))
        {
            appendNextTrueIndex++;
            hasAppend = true;
        }
        else if (addStoneHillSpringChestSafeLooseGemOneShotCleanupHelper && TryAddStoneHillSpringChestRewardRowAndHelper(
            imageStream,
            layout,
            level,
            tableWadOffset,
            springChestControllerAppendTrueIndex,
            springChestShellAppendTrueIndex,
            appendNextTrueIndex,
            sourceImagePath,
            patches,
            writtenWadOffsets,
            springChestControllerActorId,
            stoneHillSpringChestRuntimeVariantId,
            refreshRewardUntilListed: false,
            clearRewardRuntimeWord: true,
            fallbackCleanupFrames: 120,
            useLooseGemRowShape: true))
        {
            appendNextTrueIndex++;
            hasAppend = true;
        }
        else if (addStoneHillSpringChestSafeVisibleOneShotCleanupHelper && TryAddStoneHillSpringChestRewardRowAndHelper(
            imageStream,
            layout,
            level,
            tableWadOffset,
            springChestControllerAppendTrueIndex,
            springChestShellAppendTrueIndex,
            appendNextTrueIndex,
            sourceImagePath,
            patches,
            writtenWadOffsets,
            springChestControllerActorId,
            stoneHillSpringChestRuntimeVariantId,
            refreshRewardUntilListed: false,
            clearRewardRuntimeWord: true,
            fallbackCleanupFrames: 120))
        {
            appendNextTrueIndex++;
            hasAppend = true;
        }
        else if (addStoneHillSpringChestSafeOneShotHelper && TryAddStoneHillSpringChestRewardRowAndHelper(
            imageStream,
            layout,
            level,
            tableWadOffset,
            springChestControllerAppendTrueIndex,
            springChestShellAppendTrueIndex,
            appendNextTrueIndex,
            sourceImagePath,
            patches,
            writtenWadOffsets,
            springChestControllerActorId,
            stoneHillSpringChestRuntimeVariantId,
            refreshRewardUntilListed: false))
        {
            appendNextTrueIndex++;
            hasAppend = true;
        }
        else if (!suppressStoneHillSpringChestHelper && TryAddStoneHillSpringChestRewardRowAndHelper(
            imageStream,
            layout,
            level,
            tableWadOffset,
            springChestControllerAppendTrueIndex,
            springChestShellAppendTrueIndex,
            appendNextTrueIndex,
            sourceImagePath,
            patches,
            writtenWadOffsets,
            springChestControllerActorId,
            stoneHillSpringChestRuntimeVariantId))
        {
            appendNextTrueIndex++;
            hasAppend = true;
        }

        if (hasAppend)
        {
            AddSourceCountPatch(imageStream, layout, level, tableWadOffset, appendNextTrueIndex, patches, writtenWadOffsets, sourceCountNotes);
        }

        AddTreasureTotalPatch(imageStream, catalog, level, exportedTreasureDelta, patches, sourceCountNotes);

        List<string> notes =
        [
            "Patches existing source moby records only.",
            "Movement edits write raw X/Y/Z coordinates at record offsets 0x0C/0x10/0x14.",
            "Yaw/facing edits write the native source rotation matrix at record offset 0x20, plus the legacy yaw byte at 0x46 for compatibility.",
            "Fly-in landing edits use the separate destination entry record: XYZ at +0x00/+0x04/+0x08 and the direct flight heading byte at +0x0E.",
            "Remove edits are exported as a soft remove by moving the source record to -30000, -30000, -30000 world units.",
            "Type, state, and chest/gem source-byte edits write one-byte source table fields.",
            "Loose gems and proven lightweight same-level true adds clone a matching source record into the next empty slot and bump the source count; release-limited actor/chest families export at most one validated true-add per family after slot reuse, and unsafe excess rows stay saved but are skipped.",
            "Treasure edits update the level's in-game pause/inventory treasure target so added gems count toward completion.",
            "Existing contained-gem chest content recolors export as +0x53 source-byte patches; brand-new contained-gem markers still need the special-data chest-link append path."
        ];
        AddNativeMobyPathPatches(
            sourceImagePath,
            level,
            nativeMobyPathEditsPath,
            imageStream,
            layout,
            patches,
            writtenWadOffsets,
            editOutcomes,
            skippedEdits,
            notes);
        if (patches.Any(patch => patch.Kind.StartsWith("portal-", StringComparison.OrdinalIgnoreCase)))
        {
            notes.Add("Homeworld portal location edits move the linked source mobys, dedicated portal center/points, and type-6 walk-in collision triangles, then rebuild and rebalance the native collision lookup. Decorative stone arches remain terrain scenery.");
        }
        notes.AddRange(sourceCountNotes);
        if (artisansLockedChestBundle != null)
        {
            notes.Add(
                $"The runtime-proven Artisans Key + Locked Chest V2 bundle is reserved atomically: editor objects T{artisansLockedChestBundle.KeyEditorTrueIndex}/T{artisansLockedChestBundle.LockedChestEditorTrueIndex} compose as output T174/T175, hidden native reward markers occupy T176-T180, and the fixed treasure target changes 100->110.");
            notes.Add("The bundle is intentionally deferred to the final structural-WAD composition stage; its two visible edits are suppressed from generic source-row append and actor-package preview paths.");
        }
        if (allowGuardedNativeCloneAppend)
        {
            notes.Add("Disposable native-clone append research mode is enabled: guarded same-level enemy/chest true-add rows may write for emulator testing. Normal Create BIN keeps these guarded unless this research flag is explicitly enabled.");
        }
        if (patches.Any(patch =>
            string.Equals(patch.Kind, "spring-chest-helper-hook", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(patch.Kind, "spring-chest-helper-payload", StringComparison.OrdinalIgnoreCase)))
        {
            notes.Add(addStoneHillSpringChestSafeNativeSpringEffectOnlyHelper
                ? useStoneHillSpringChestBlueGemNativeSpringEffectVisual
                    ? $"{level.DisplayName} Spring Chest native-effect diagnostic exports the paired controller/shell records, a blank scratch row, and a small helper that writes a blue-gem visual native-effect shape without adding a Sparx-targetable pickup gem."
                    : $"{level.DisplayName} Spring Chest native-effect diagnostic exports the paired controller/shell records, a blank scratch row, and a small helper that writes the native 0x0022/0x20 after-hit effect shape without adding a Sparx-targetable pickup gem."
                : $"{level.DisplayName} Spring Chest candidate exports add the paired controller/shell records, a hidden reward-gem row, and the small in-game helper needed for pop/collect behavior.");
        }
        else if (patches.Any(patch =>
            string.Equals(patch.Kind, "spring-chest-arm-only-helper-hook", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(patch.Kind, "spring-chest-arm-only-helper-payload", StringComparison.OrdinalIgnoreCase)))
        {
            notes.Add("Stone Hill Spring Chest arm-only helper candidate exports the paired controller/shell records, actor package, and a small helper hook that only links the controller and shell once. It intentionally skips reward spawning and post-break object movement.");
        }
        else if (patches.Any(patch =>
            string.Equals(patch.Kind, "spring-chest-entry-hook-only-helper-hook", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(patch.Kind, "spring-chest-entry-hook-only-helper-payload", StringComparison.OrdinalIgnoreCase)))
        {
            notes.Add("Stone Hill Spring Chest entry hook-only candidate exports the paired controller/shell records, actor package, and a pass-through entry trampoline that preserves the original game routine's normal return address. It writes no chest fields.");
        }
        else if (patches.Any(patch =>
            string.Equals(patch.Kind, "spring-chest-stack-hook-only-helper-hook", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(patch.Kind, "spring-chest-stack-hook-only-helper-payload", StringComparison.OrdinalIgnoreCase)))
        {
            notes.Add("Stone Hill Spring Chest stack hook-only candidate exports the paired controller/shell records, actor package, and a pass-through helper hook that only calls the original game function while saving RA on the normal stack. It writes no chest fields.");
        }
        else if (patches.Any(patch =>
            string.Equals(patch.Kind, "spring-chest-hook-only-helper-hook", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(patch.Kind, "spring-chest-hook-only-helper-payload", StringComparison.OrdinalIgnoreCase)))
        {
            notes.Add("Stone Hill Spring Chest hook-only candidate exports the paired controller/shell records, actor package, and a pass-through helper hook that only calls the original game function. It writes no chest fields.");
        }
        else if (patches.Any(patch => string.Equals(patch.Kind, "spring-chest-reward-row-append", StringComparison.OrdinalIgnoreCase)))
        {
            notes.Add("Stone Hill Spring Chest reward-row isolation candidate exports the paired controller/shell records, actor package, and hidden reward-gem row, but intentionally skips the custom EXE helper to isolate the previous in-game crash/hang.");
        }
        else if (patches.Any(patch => string.Equals(patch.Kind, "spring-chest-native-reward-row-append", StringComparison.OrdinalIgnoreCase)))
        {
            notes.Add("Stone Hill Spring Chest native reward-row candidate exports the paired Peace Keepers spring records, actor package, and the native T92/T93/T94 reward rows observed changing in live Peace Keepers RAM, without a custom EXE helper.");
        }
        else if (suppressActorPackageImports && springChestControllerAppendTrueIndex >= 0 && springChestShellAppendTrueIndex >= 0)
        {
            notes.Add("Stone Hill Spring Chest records-only candidate exports only the paired controller/shell source records and special data; it intentionally skips actor packages, actor roots, and helper hooks to isolate loading freezes.");
        }
        else if (suppressStoneHillSpringChestHelper && springChestControllerAppendTrueIndex >= 0 && springChestShellAppendTrueIndex >= 0)
        {
            notes.Add(packageImportPreviews.Any(preview => preview.RecipeMode.Contains("CopyOnlyNoRoot", StringComparison.OrdinalIgnoreCase))
                ? "Stone Hill Spring Chest copy-only candidate exports the paired controller/shell records and actor package bytes but intentionally skips actor roots and the custom EXE helper/reward row to isolate loading freezes."
                : "Stone Hill Spring Chest no-helper candidate exports only the paired controller/shell records and actor packages; it intentionally skips the custom EXE helper/reward row to isolate loading freezes.");
        }
        if (allowPlanOnlyActorPackageImports && patches.Count > 0)
        {
            MobySourcePatch fastEntryPatch = TestLevelWarpPatch.CreateGuarded(imageStream, layout, level);
            patches.Add(fastEntryPatch);
            notes.Add($"Disposable Swap Test fast entry is enabled for {level.DisplayName}: open Inventory, enter {TestLevelWarpPatch.ActivationSequence}, then {TestLevelWarpPatch.TargetSelectionText(level.LevelId)}. The patch is absent from normal Create BIN.");
        }

        notes.Add(allowPlanOnlyActorPackageImports
            ? "Disposable candidate mode is enabled: plan-only actor-package recipes can write for explicit emulator testing."
            : "Promoted or in-game-verified cross-level actor-package recipes write only to disposable test BIN/CUE output; unmapped or unpromoted recipes remain guarded previews.");

        return new MobySourcePatchPlan(
            GeneratedAt: DateTimeOffset.UtcNow,
            SourceImagePath: sourceImagePath,
            OutputImagePath: outputImagePath,
            OutputCuePath: outputCuePath,
            NativeEditsPath: nativeEditsPath,
            LevelKey: level.Key,
            LevelName: level.DisplayName,
            SourceTableWadOffset: $"0x{tableWadOffset:X}",
            SourceRecordCount: level.SourceRecordCount,
            RecordStride: RecordStride,
            PatchCount: patches.Count,
            TotalPatchedBytes: patches.Sum(patch => patch.ByteLength),
            Patches: patches,
            PackageImportPreviews: packageImportPreviews,
            SkippedEdits: skippedEdits,
            Notes: notes,
            EditOutcomes: editOutcomes,
            ArtisansNativeLockedChestRuntimeBundle: artisansLockedChestBundle);
    }

    private static void TrackSpringChestPairAppend(
        JsonElement edit,
        int appendTrueIndex,
        IReadOnlyList<MobyActorPackageImportPreview> packageImportPreviews,
        ref int controllerAppendTrueIndex,
        ref int shellAppendTrueIndex,
        ref int controllerActorId)
    {
        if (!TryGetCrossLevelTemplate(edit, out JsonElement crossLevelTemplate))
            return;

        string family = JsonValue.GetString(crossLevelTemplate, "family", InferCrossLevelFamily(JsonValue.GetString(crossLevelTemplate, "id")));
        if (!string.Equals(family, "springChest", StringComparison.OrdinalIgnoreCase))
            return;

        string templateId = JsonValue.GetString(crossLevelTemplate, "id");
        int sourceTrueIndex = JsonValue.GetInt32(crossLevelTemplate, "sourceTrueIndex", -1);
        if (templateId.Contains("spring_chest_controller", StringComparison.OrdinalIgnoreCase) ||
            templateId.Contains("spring_chest_companion", StringComparison.OrdinalIgnoreCase) ||
            sourceTrueIndex is 30 or 113)
        {
            controllerAppendTrueIndex = appendTrueIndex;
            controllerActorId = UsesSpringChestControllerAlias(packageImportPreviews)
                ? 0x01FE
                : ReadActorIdFromEdit(edit, fallbackActorId: 0x00C2);
        }
        else if (templateId.Contains("spring_chest", StringComparison.OrdinalIgnoreCase) || sourceTrueIndex == 82)
            shellAppendTrueIndex = appendTrueIndex;
    }

    private static void TrackPeaceKeepersSpringChestAppend(JsonElement edit, int appendTrueIndex, List<SpringChestAppendAnchor> anchors)
    {
        if (!TryGetCrossLevelTemplate(edit, out JsonElement crossLevelTemplate))
            return;

        string family = JsonValue.GetString(crossLevelTemplate, "family", InferCrossLevelFamily(JsonValue.GetString(crossLevelTemplate, "id")));
        string sourceLevelKey = LevelCatalog.NormalizeKey(JsonValue.GetString(crossLevelTemplate, "sourceLevelKey"));
        int sourceTrueIndex = JsonValue.GetInt32(crossLevelTemplate, "sourceTrueIndex", -1);
        if (!string.Equals(family, "springChest", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(sourceLevelKey, "peacekeepers", StringComparison.OrdinalIgnoreCase) ||
            sourceTrueIndex is not (70 or 71 or 80 or 81 or 82 or 83 or 91 or 131))
        {
            return;
        }

        if (!TryReadEditRawAxis(edit, "x", out int rawX) ||
            !TryReadEditRawAxis(edit, "y", out int rawY) ||
            !TryReadEditRawAxis(edit, "z", out int rawZ))
        {
            return;
        }

        anchors.Add(new SpringChestAppendAnchor(sourceTrueIndex, appendTrueIndex, rawX, rawY, rawZ));
    }

    private static bool TryReadEditRawAxis(JsonElement edit, string axis, out int rawValue)
    {
        rawValue = 0;
        if (edit.TryGetProperty("rawEdited", out JsonElement rawEdited) &&
            rawEdited.ValueKind == JsonValueKind.Object &&
            rawEdited.TryGetProperty(axis, out _))
        {
            rawValue = JsonValue.GetInt32(rawEdited, axis);
            return true;
        }

        if (edit.TryGetProperty("edited", out JsonElement edited) &&
            edited.ValueKind == JsonValueKind.Object &&
            edited.TryGetProperty(axis, out _))
        {
            rawValue = (int)Math.Round(JsonValue.GetSingle(edited, axis) * 16f);
            return true;
        }

        return false;
    }

    private static bool TryAddStoneHillSpringChestNativeRewardRows(
        FileStream stream,
        DiscLayout layout,
        LevelCatalog catalog,
        LevelDefinition level,
        long tableWadOffset,
        long tableRelativeOffset,
        int firstRewardAppendTrueIndex,
        IReadOnlyList<SpringChestAppendAnchor> anchors,
        List<MobySourcePatch> patches,
        HashSet<long> writtenWadOffsets,
        Dictionary<string, CrossLevelSharedSpecialCluster> sharedCrossLevelSpecialClusters,
        List<string> skippedEdits,
        bool includeContextRows,
        bool contextRowsOnly,
        bool containedHelperRowsOnly,
        bool useSharedClusterForRows,
        out int addedRows)
    {
        addedRows = 0;
        if (!string.Equals(LevelCatalog.NormalizeKey(level.Key), "stonehill", StringComparison.OrdinalIgnoreCase))
            return false;

        SpringChestRewardRowSet[] rewardSets = BuildSpringChestRewardRowSets(anchors, includeContextRows, contextRowsOnly, containedHelperRowsOnly);
        if (rewardSets.Length == 0)
            return false;

        LevelDefinition? sourceLevel = catalog.FindByKey("peacekeepers");
        if (sourceLevel?.HasSourceTable != true)
        {
            skippedEdits.Add("Spring Chest native reward rows: Peace Keepers source table is not mapped.");
            return false;
        }

        if (tableRelativeOffset <= 0 || string.IsNullOrWhiteSpace(sourceLevel.SourceTableRelativeOffset))
        {
            skippedEdits.Add("Spring Chest native reward rows: source and target table-relative offsets are required.");
            return false;
        }

        long sourceTableWadOffset = ParseRequiredLong(sourceLevel.SourceTableWadOffset, "peacekeepers.sourceTableWadOffset");
        long sourceTableRelativeOffset = ParseRequiredLong(sourceLevel.SourceTableRelativeOffset, "peacekeepers.sourceTableRelativeOffset");
        int nextAppendTrueIndex = firstRewardAppendTrueIndex;
        foreach (SpringChestRewardRowSet rewardSet in rewardSets)
        {
            byte[] sourceBaseRecord = ReadWadBytes(stream, layout, sourceTableWadOffset + ((long)rewardSet.SourceSpringTrueIndex * RecordStride), RecordStride);
            int sourceBaseX = BitConverter.ToInt32(sourceBaseRecord, XOffset);
            int sourceBaseY = BitConverter.ToInt32(sourceBaseRecord, YOffset);
            int sourceBaseZ = BitConverter.ToInt32(sourceBaseRecord, ZOffset);

            foreach (int sourceTrueIndex in rewardSet.RewardSourceTrueIndices)
            {
                int appendTrueIndex = nextAppendTrueIndex++;
                long appendWadOffset = tableWadOffset + ((long)appendTrueIndex * RecordStride);
                byte[] beforeAppend = ReadWadBytes(stream, layout, appendWadOffset, RecordStride);
                if (!beforeAppend.All(value => value == 0))
                {
                    skippedEdits.Add($"Spring Chest native reward row T{appendTrueIndex}: append slot is not empty in the source image.");
                    return addedRows > 0;
                }

                byte[] rewardRow = ReadWadBytes(stream, layout, sourceTableWadOffset + ((long)sourceTrueIndex * RecordStride), RecordStride);
                int sourceX = BitConverter.ToInt32(rewardRow, XOffset);
                int sourceY = BitConverter.ToInt32(rewardRow, YOffset);
                int sourceZ = BitConverter.ToInt32(rewardRow, ZOffset);
                WriteInt32(rewardRow, XOffset, rewardSet.Anchor.RawX + (sourceX - sourceBaseX));
                WriteInt32(rewardRow, YOffset, rewardSet.Anchor.RawY + (sourceY - sourceBaseY));
                WriteInt32(rewardRow, ZOffset, rewardSet.Anchor.RawZ + (sourceZ - sourceBaseZ));

                CrossLevelAppendDonor donor = new(
                    sourceLevel,
                    sourceTableWadOffset,
                    sourceTableRelativeOffset,
                    sourceTrueIndex,
                    useSharedClusterForRows ? "SharedClusterNativeRewardRowsNoExeHelper" : "NativeRewardRowsNoExeHelper");
                if (!TryAppendCrossLevelSpecialData(
                    stream,
                    layout,
                    level,
                    tableWadOffset,
                    tableRelativeOffset,
                    donor,
                    appendTrueIndex,
                    $"Spring Chest native reward T{sourceTrueIndex}",
                    rewardRow,
                    patches,
                    writtenWadOffsets,
                    sharedCrossLevelSpecialClusters,
                    skippedEdits,
                    out string specialDataDescription))
                {
                    return addedRows > 0;
                }

                bool isRewardRow = sourceTrueIndex is 92 or 93 or 94 or 132 or 133 or 134;
                bool isContainedHelperRow = sourceTrueIndex is >= 118 and <= 122;
                AddPatch(
                    stream,
                    layout,
                    level,
                    tableWadOffset,
                    appendTrueIndex,
                    0,
                    rewardRow,
                    isRewardRow ? "spring-chest-native-reward-row-append" : isContainedHelperRow ? "spring-chest-native-contained-helper-row-append" : "spring-chest-native-context-row-append",
                    isRewardRow ? "Spring Chest native reward row" : isContainedHelperRow ? "Spring Chest native contained helper row" : "Spring Chest native context row",
                    $"Append Peace Keepers native {(isRewardRow ? "reward" : isContainedHelperRow ? "contained helper" : "context")} row T{sourceTrueIndex} as Stone Hill T{appendTrueIndex}, preserving its source-special data and offsetting it beside imported Spring Chest T{rewardSet.Anchor.TargetTrueIndex}.{specialDataDescription}",
                    patches,
                    writtenWadOffsets);
                addedRows++;
            }
        }

        return addedRows > 0;
    }

    private static SpringChestRewardRowSet[] BuildSpringChestRewardRowSets(IReadOnlyList<SpringChestAppendAnchor> anchors, bool includeContextRows, bool contextRowsOnly, bool containedHelperRowsOnly)
    {
        List<SpringChestRewardRowSet> sets = [];
        foreach (SpringChestAppendAnchor anchor in anchors.OrderBy(anchor => anchor.TargetTrueIndex))
        {
            int[] rewardSourceTrueIndices = anchor.SourceTrueIndex switch
            {
                80 when containedHelperRowsOnly => [118, 119, 120, 121, 122],
                70 when contextRowsOnly => [72, 73, 74],
                91 when contextRowsOnly => [88, 89, 90],
                131 when contextRowsOnly => [128, 129, 130],
                70 => includeContextRows ? [72, 73, 74, 92, 93, 94] : [92, 93, 94],
                91 => includeContextRows ? [88, 89, 90, 92, 93, 94] : [92, 93, 94],
                131 => includeContextRows ? [128, 129, 130, 132, 133, 134] : [132, 133, 134],
                _ => []
            };
            if (rewardSourceTrueIndices.Length > 0)
                sets.Add(new SpringChestRewardRowSet(anchor, anchor.SourceTrueIndex, rewardSourceTrueIndices));
        }

        return sets.ToArray();
    }

    private static bool TryAddStoneHillSpringChestRewardRow(
        FileStream stream,
        DiscLayout layout,
        LevelDefinition level,
        long tableWadOffset,
        int spawnGemAppendTrueIndex,
        List<MobySourcePatch> patches,
        HashSet<long> writtenWadOffsets,
        bool preinitializeVisibleRewardRow = false,
        bool blankRewardRow = false,
        bool visualScratchRewardRow = false,
        bool clearVisualScratchRuntimeWord = false)
    {
        bool isStoneHill = string.Equals(LevelCatalog.NormalizeKey(level.Key), "stonehill", StringComparison.OrdinalIgnoreCase);
        if (!blankRewardRow && !isStoneHill)
            return false;

        const int stoneHillLocalGemDonorTrueIndex = 79;
        if (!blankRewardRow && stoneHillLocalGemDonorTrueIndex >= level.SourceRecordCount)
            throw new InvalidOperationException("Stone Hill Spring Chest helper needs local gem donor T79, but the source table is shorter than expected.");

        long appendWadOffset = tableWadOffset + ((long)spawnGemAppendTrueIndex * RecordStride);
        byte[] beforeAppend = ReadWadBytes(stream, layout, appendWadOffset, RecordStride);
        if (!beforeAppend.All(value => value == 0))
            throw new InvalidOperationException($"Stone Hill Spring Chest helper reward row T{spawnGemAppendTrueIndex} is not empty in the source image.");

        byte[] rewardRow = blankRewardRow
            ? new byte[RecordStride]
            : ReadWadBytes(stream, layout, tableWadOffset + (stoneHillLocalGemDonorTrueIndex * RecordStride), RecordStride);
        if (blankRewardRow)
        {
            // Leave the row as scratch space until the in-game helper builds the visible reward.
        }
        else if (visualScratchRewardRow)
        {
            WriteInt32(rewardRow, XOffset, 0);
            WriteInt32(rewardRow, YOffset, 0);
            WriteInt32(rewardRow, ZOffset, -65536);
            if (clearVisualScratchRuntimeWord)
                WriteInt32(rewardRow, 0x1C, 0);
            rewardRow[0x34] = 0x00;
            rewardRow[0x35] = 0x00;
            rewardRow[0x36] = 0x00;
            rewardRow[0x37] = 0x00;
            rewardRow[0x38] = 0x00;
            rewardRow[0x39] = 0x00;
            rewardRow[0x3A] = 0x00;
            rewardRow[0x3B] = 0x00;
            rewardRow[0x3C] = 0x00;
            rewardRow[0x3D] = 0x00;
            rewardRow[0x3E] = 0x00;
            rewardRow[0x3F] = 0x00;
            rewardRow[0x48] = 0x00;
            rewardRow[0x49] = 0x00;
            rewardRow[0x4A] = 0x00;
            rewardRow[0x4B] = 0x00;
            rewardRow[0x4C] = 0x00;
            rewardRow[0x4D] = 0x00;
            rewardRow[0x4E] = 0x00;
            rewardRow[0x4F] = 0x00;
            WriteInt32(rewardRow, TypeOffset, 0);
            WriteInt32(rewardRow, 0x54, 0);
        }
        else if (!preinitializeVisibleRewardRow)
        {
            WriteInt32(rewardRow, XOffset, 0);
            WriteInt32(rewardRow, YOffset, 0);
            WriteInt32(rewardRow, ZOffset, -65536);
            rewardRow[TypeOffset] = 0x20;
        }
        else
        {
            rewardRow[0x36] = 0x55;
            rewardRow[0x37] = 0x00;
            rewardRow[0x4F] = 0x03;
            rewardRow[TypeOffset] = 0x18;
        }
        rewardRow[StateOffset] = 0x00;

        AddRawPatch(
            stream,
            layout,
            level,
            appendWadOffset,
            rewardRow,
            "spring-chest-reward-row-append",
            "Spring Chest reward row",
            spawnGemAppendTrueIndex,
            "0x0",
            blankRewardRow
                ? $"Append blank scratch reward row T{spawnGemAppendTrueIndex} in {level.DisplayName}; the in-game helper rebuilds it only while the Spring Chest gem is popping, then blanks it again so Sparx cannot target a parked reward."
                : visualScratchRewardRow
                ? clearVisualScratchRuntimeWord
                    ? $"Append dormant donor-scaffold scratch reward row T{spawnGemAppendTrueIndex} cloned from Stone Hill local gem donor T{stoneHillLocalGemDonorTrueIndex}; model scaffold bytes are preserved, but runtime and parked collectible identity bytes are blanked so Sparx cannot target it while parked."
                    : $"Append donor-scaffold scratch reward row T{spawnGemAppendTrueIndex} cloned from Stone Hill local gem donor T{stoneHillLocalGemDonorTrueIndex}; model/runtime scaffold bytes are preserved, but parked collectible identity bytes are blanked so Sparx cannot target it."
                : preinitializeVisibleRewardRow
                ? $"Append preinitialized visible blue reward-gem row T{spawnGemAppendTrueIndex} cloned from Stone Hill local gem donor T{stoneHillLocalGemDonorTrueIndex}; the in-game helper moves this already-initialized gem to the Spring Chest instead of rebuilding its visible identity at runtime."
                : $"Append hidden reward-gem row T{spawnGemAppendTrueIndex} cloned from Stone Hill local gem donor T{stoneHillLocalGemDonorTrueIndex}; the in-game helper turns this row into the collectible Spring Chest reward.",
            patches,
            writtenWadOffsets);

        return true;
    }

    private static bool TryAddStoneHillSpringChestRewardRowAndHelper(
        FileStream stream,
        DiscLayout layout,
        LevelDefinition level,
        long tableWadOffset,
        int controllerAppendTrueIndex,
        int shellAppendTrueIndex,
        int spawnGemAppendTrueIndex,
        string sourceImagePath,
        List<MobySourcePatch> patches,
        HashSet<long> writtenWadOffsets,
        int controllerActorId,
        int runtimeVariantId,
        bool refreshRewardUntilListed = true,
        bool clearRewardRuntimeWord = false,
        int fallbackCleanupFrames = 0,
        bool useLooseGemRowShape = false,
        bool useVisibleGemTypeWord = false,
        int rewardZOffset = 0x0558,
        bool preinitializeVisibleRewardRow = false,
        bool preservePreinitializedRewardRow = false,
        int uncollectedReturnFrames = 0,
        int rewardReturnDropPerFrame = 0,
        int rewardReturnDropStartFrames = 0,
        int rewardRiseFrames = 0,
        int rewardRisePerFrame = 0,
        bool maskAirborneRewardValue = false,
        bool rearmOnEarlyRewardPickup = false,
        bool unlinkAirborneRewardFromActiveList = false,
        bool parkReturnedRewardAsInert = false,
        bool monitorReturnedRewardPickupList = false,
        bool restoreReturnedRewardOnHit = false,
        bool fullQuarantineReturnedReward = false,
        bool delayReturnedRewardCollection = false,
        bool delayReturnedRewardTypeWord = false,
        bool scriptRewardMotionWithoutPickupList = false,
        bool blankRewardRow = false,
        bool visualScratchRewardRow = false,
        bool clearVisualScratchRuntimeWord = false,
        bool blankReturnedRewardRow = false,
        bool preserveRewardVisualScaffold = false,
        bool restoreDonorGemShapeOnPop = false,
        bool leaveDonorGemPickupTypeBlankOnPop = false,
        bool leaveDonorGemTailWordBlankOnPop = false,
        bool useNativeStateZeroShellWords = false,
        bool useNativeReadyShellWords = false,
        bool useNativePreHitShellLifecycleWords = false,
        bool useNativeSpringEffectRow = false,
        bool useBlueGemNativeSpringEffectVisual = false,
        bool useBlueGemNativeSpringEffectOrdinal = false,
        bool manualSpyroTouchCollectNativeEffectVisual = false,
        bool manualAwardTreasureCounters = false,
        bool manualAwardNativeTreasureRoutine = false,
        bool manualAwardFixedTreasureWords = false,
        bool manualAnimateRewardPopArc = false)
    {
        if (controllerAppendTrueIndex < 0 || shellAppendTrueIndex < 0)
            return false;
        if (!TryAddStoneHillSpringChestRewardRow(stream, layout, level, tableWadOffset, spawnGemAppendTrueIndex, patches, writtenWadOffsets, preinitializeVisibleRewardRow, blankRewardRow, visualScratchRewardRow, clearVisualScratchRuntimeWord))
            return false;

        foreach (MobySourcePatch helperPatch in SpringChestInGamePatchBuilder.BuildSmallCaveMinimalPopPatches(
            sourceImagePath,
            level,
            controllerAppendTrueIndex,
            shellAppendTrueIndex,
            spawnGemAppendTrueIndex,
            controllerActorId: controllerActorId,
            runtimeVariantId: runtimeVariantId,
            refreshRewardUntilListed: refreshRewardUntilListed,
            clearRewardRuntimeWord: clearRewardRuntimeWord,
            fallbackCleanupFrames: fallbackCleanupFrames,
            useLooseGemRowShape: useLooseGemRowShape,
            useVisibleGemTypeWord: useVisibleGemTypeWord,
            rewardZOffset: rewardZOffset,
            preservePreinitializedRewardRow: preservePreinitializedRewardRow,
            uncollectedReturnFrames: uncollectedReturnFrames,
            rewardReturnDropPerFrame: rewardReturnDropPerFrame,
            rewardReturnDropStartFrames: rewardReturnDropStartFrames,
            rewardRiseFrames: rewardRiseFrames,
            rewardRisePerFrame: rewardRisePerFrame,
            maskAirborneRewardValue: maskAirborneRewardValue,
            rearmOnEarlyRewardPickup: rearmOnEarlyRewardPickup,
            unlinkAirborneRewardFromActiveList: unlinkAirborneRewardFromActiveList,
            parkReturnedRewardAsInert: parkReturnedRewardAsInert,
            monitorReturnedRewardPickupList: monitorReturnedRewardPickupList,
            restoreReturnedRewardOnHit: restoreReturnedRewardOnHit,
            fullQuarantineReturnedReward: fullQuarantineReturnedReward,
            delayReturnedRewardCollection: delayReturnedRewardCollection,
            delayReturnedRewardTypeWord: delayReturnedRewardTypeWord,
            scriptRewardMotionWithoutPickupList: scriptRewardMotionWithoutPickupList,
            blankReturnedRewardRow: blankReturnedRewardRow,
            preserveRewardVisualScaffold: preserveRewardVisualScaffold,
            restoreDonorGemShapeOnPop: restoreDonorGemShapeOnPop,
            leaveDonorGemPickupTypeBlankOnPop: leaveDonorGemPickupTypeBlankOnPop,
            leaveDonorGemTailWordBlankOnPop: leaveDonorGemTailWordBlankOnPop,
            useNativeStateZeroShellWords: useNativeStateZeroShellWords,
            useNativeReadyShellWords: useNativeReadyShellWords,
            useNativePreHitShellLifecycleWords: useNativePreHitShellLifecycleWords,
            useNativeSpringEffectRow: useNativeSpringEffectRow,
            useBlueGemNativeSpringEffectVisual: useBlueGemNativeSpringEffectVisual,
            useBlueGemNativeSpringEffectOrdinal: useBlueGemNativeSpringEffectOrdinal,
            manualSpyroTouchCollectNativeEffectVisual: manualSpyroTouchCollectNativeEffectVisual,
            manualAwardTreasureCounters: manualAwardTreasureCounters,
            manualAwardNativeTreasureRoutine: manualAwardNativeTreasureRoutine,
            manualAwardFixedTreasureWords: manualAwardFixedTreasureWords,
            manualAnimateRewardPopArc: manualAnimateRewardPopArc))
        {
            patches.Add(helperPatch);
        }

        return true;
    }

    private static bool TryAddStoneHillSpringChestArmOnlyHelper(
        string sourceImagePath,
        LevelDefinition level,
        int controllerAppendTrueIndex,
        int shellAppendTrueIndex,
        List<MobySourcePatch> patches,
        int controllerActorId,
        int runtimeVariantId)
    {
        if (!string.Equals(LevelCatalog.NormalizeKey(level.Key), "stonehill", StringComparison.OrdinalIgnoreCase) ||
            controllerAppendTrueIndex < 0 ||
            shellAppendTrueIndex < 0)
        {
            return false;
        }

        foreach (MobySourcePatch helperPatch in SpringChestInGamePatchBuilder.BuildSmallCaveArmOnlyPatches(
            sourceImagePath,
            level,
            controllerAppendTrueIndex,
            shellAppendTrueIndex,
            controllerActorId: controllerActorId,
            runtimeVariantId: runtimeVariantId))
        {
            patches.Add(helperPatch);
        }

        return true;
    }

    private static bool TryAddStoneHillSpringChestRowRepairHelper(
        string sourceImagePath,
        LevelDefinition level,
        int firstAppendTrueIndex,
        int secondAppendTrueIndex,
        IReadOnlyList<SpringChestAppendAnchor> peaceKeepersSpringChestAnchors,
        List<MobySourcePatch> patches)
    {
        SpringChestAppendAnchor? t70 = peaceKeepersSpringChestAnchors.FirstOrDefault(anchor => anchor.SourceTrueIndex == 70);
        SpringChestAppendAnchor? t71 = peaceKeepersSpringChestAnchors.FirstOrDefault(anchor => anchor.SourceTrueIndex == 71);
        if (t70 != null && t71 != null)
        {
            firstAppendTrueIndex = t70.TargetTrueIndex;
            secondAppendTrueIndex = t71.TargetTrueIndex;
        }

        if (!string.Equals(LevelCatalog.NormalizeKey(level.Key), "stonehill", StringComparison.OrdinalIgnoreCase) ||
            firstAppendTrueIndex < 0 ||
            secondAppendTrueIndex < 0)
        {
            return false;
        }

        foreach (MobySourcePatch helperPatch in SpringChestInGamePatchBuilder.BuildSmallCaveRowRepairPatches(
            sourceImagePath,
            level,
            firstAppendTrueIndex,
            secondAppendTrueIndex))
        {
            patches.Add(helperPatch);
        }

        return true;
    }

    private static int ResolveStoneHillSpringChestRuntimeVariantId(IReadOnlyList<MobyActorPackageImportPreview> packageImportPreviews)
    {
        foreach (MobyActorPackageImportPreview preview in packageImportPreviews)
        {
            if (!string.Equals(preview.Family, "springChest", StringComparison.OrdinalIgnoreCase))
                continue;

            string mode = preview.RecipeMode;
            if (mode.Contains("Runtime01A6", StringComparison.OrdinalIgnoreCase) ||
                mode.Contains("T70", StringComparison.OrdinalIgnoreCase))
                return 0x01A6;
            if (mode.Contains("Runtime01A7", StringComparison.OrdinalIgnoreCase) ||
                mode.Contains("T71", StringComparison.OrdinalIgnoreCase))
                return 0x01A7;
        }

        return 0x01AC;
    }

    private static bool TryAddStoneHillSpringChestHookOnlyHelper(
        string sourceImagePath,
        LevelDefinition level,
        List<MobySourcePatch> patches)
    {
        if (!string.Equals(LevelCatalog.NormalizeKey(level.Key), "stonehill", StringComparison.OrdinalIgnoreCase))
            return false;

        foreach (MobySourcePatch helperPatch in SpringChestInGamePatchBuilder.BuildSmallCaveHookOnlyPatches(
            sourceImagePath,
            level))
        {
            patches.Add(helperPatch);
        }

        return true;
    }

    private static bool TryAddStoneHillSpringChestStackHookOnlyHelper(
        string sourceImagePath,
        LevelDefinition level,
        List<MobySourcePatch> patches)
    {
        if (!string.Equals(LevelCatalog.NormalizeKey(level.Key), "stonehill", StringComparison.OrdinalIgnoreCase))
            return false;

        foreach (MobySourcePatch helperPatch in SpringChestInGamePatchBuilder.BuildSmallCaveStackHookOnlyPatches(
            sourceImagePath,
            level))
        {
            patches.Add(helperPatch);
        }

        return true;
    }

    private static bool TryAddStoneHillSpringChestEntryHookOnlyHelper(
        string sourceImagePath,
        LevelDefinition level,
        List<MobySourcePatch> patches)
    {
        if (!string.Equals(LevelCatalog.NormalizeKey(level.Key), "stonehill", StringComparison.OrdinalIgnoreCase))
            return false;

        foreach (MobySourcePatch helperPatch in SpringChestInGamePatchBuilder.BuildSmallCaveEntryHookOnlyPatches(
            sourceImagePath,
            level))
        {
            patches.Add(helperPatch);
        }

        return true;
    }

    private static bool TryPatchSourceRecordCloneIntoSlot(
        FileStream stream,
        DiscLayout layout,
        LevelCatalog catalog,
        LevelDefinition targetLevel,
        GeometryCandidate? levelGeometry,
        long targetTableWadOffset,
        int targetTrueIndex,
        string label,
        JsonElement edit,
        List<MobySourcePatch> patches,
        HashSet<long> writtenWadOffsets,
        List<string> skippedEdits)
    {
        if (!edit.TryGetProperty("recordMutation", out JsonElement mutation) || mutation.ValueKind != JsonValueKind.Object)
            return false;

        string mode = JsonValue.GetString(mutation, "mode");
        if (!string.Equals(mode, "cloneSourceRecordIntoSlot", StringComparison.OrdinalIgnoreCase))
            return false;

        return TryPatchSourceRecordCloneIntoSlotCore(
            stream,
            layout,
            catalog,
            targetLevel,
            levelGeometry,
            targetTableWadOffset,
            JsonValue.GetString(mutation, "sourceLevelKey"),
            JsonValue.GetInt32(mutation, "sourceTrueIndex", -1),
            targetTrueIndex,
            label,
            edit,
            "moby-record-slot-clone",
            $"Clone same-level donor T{{0}} into existing source slot T{{1}}, preserving this slot's placement and donor behavior data.",
            preserveDonorState: false,
            updatePlacementSectorFromPlacement: true,
            patches,
            writtenWadOffsets,
            skippedEdits);
    }

    private static bool TryPatchAddedNativeCloneIntoAutoSlot(
        FileStream stream,
        DiscLayout layout,
        LevelCatalog catalog,
        LevelDefinition targetLevel,
        GeometryCandidate? levelGeometry,
        long targetTableWadOffset,
        string label,
        JsonElement edit,
        HashSet<int> protectedSourceSlots,
        HashSet<int> autoReuseTargetSlots,
        List<MobySourcePatch> patches,
        HashSet<long> writtenWadOffsets,
        List<string> skippedEdits,
        out int treasureDelta)
    {
        treasureDelta = 0;
        if (JsonValue.GetBoolean(edit, "disableAutoSlotReuse"))
            return false;
        if (!TryGetAutoSlotReuseIdentity(edit, out int targetType, out int targetState, out int targetSourceByte36, out int targetSourceByte37, out int targetSourceByte4F, out int targetFlag4A, out int targetFlag4B))
            return false;
        if (IsNativeLifeChestIdentity(
                targetType,
                targetSourceByte36,
                targetSourceByte37,
                targetSourceByte4F,
                targetFlag4A,
                targetFlag4B))
        {
            // A Life Chest row is an active object, not a spare slot. Reusing its donor
            // moves the original chest instead of creating a copy.
            return false;
        }
        bool canTrueAppendAfterReuseSlots = IsPromotedSameLevelNativeCloneAppendIdentity(
            targetLevel.Key,
            targetType,
            targetSourceByte36,
            targetSourceByte37,
            targetSourceByte4F,
            targetFlag4A,
            targetFlag4B);
        int sourceTrueIndex = TryGetSameLevelAppendDonor(edit, targetLevel, out int explicitSourceTrueIndex)
            ? explicitSourceTrueIndex
            : FindExactSameLevelDonor(
                stream,
                layout,
                targetTableWadOffset,
                targetLevel.SourceRecordCount,
                targetType,
                targetState,
                targetSourceByte36,
                targetSourceByte37,
                targetSourceByte4F,
                targetFlag4A,
                targetFlag4B);
        if (sourceTrueIndex < 0)
        {
            if (IsGuardedNativeCloneAppend(edit))
            {
                skippedEdits.Add($"{label}: same-level enemy/chest copy was kept saved but skipped because no exact same-level donor record could be proven.");
                return true;
            }

            return false;
        }

        int targetTrueIndex = FindAutoReuseTargetSlot(
            stream,
            layout,
            targetLevel,
            targetTableWadOffset,
            sourceTrueIndex,
            targetType,
            targetState,
            targetSourceByte36,
            targetSourceByte37,
            targetSourceByte4F,
            targetFlag4A,
            targetFlag4B,
            protectedSourceSlots,
            autoReuseTargetSlots,
            writtenWadOffsets);
        if (targetTrueIndex < 0)
        {
            if (canTrueAppendAfterReuseSlots)
                return false;

            skippedEdits.Add($"{label}: same-level enemy/chest copy was kept saved but skipped because no matching source slot is available to reuse.");
            return true;
        }

        int patchCountBefore = patches.Count;
        bool patched = TryPatchSourceRecordCloneIntoSlotCore(
            stream,
            layout,
            catalog,
            targetLevel,
            levelGeometry,
            targetTableWadOffset,
            targetLevel.Key,
            sourceTrueIndex,
            targetTrueIndex,
            label,
            edit,
            "moby-record-auto-slot-clone",
            $"Export added/pasted same-level clone by reusing source slot T{{1}} instead of appending a crash-prone new enemy/chest row; cloned donor T{{0}} and preserved the pasted placement.",
            preserveDonorState: true,
            updatePlacementSectorFromPlacement: true,
            patches,
            writtenWadOffsets,
            skippedEdits);
        if (patched && patches.Count > patchCountBefore)
        {
            autoReuseTargetSlots.Add(targetTrueIndex);
            MobySourcePatch? slotPatch = patches
                .Skip(patchCountBefore)
                .FirstOrDefault(patch =>
                    string.Equals(patch.Kind, "moby-record-auto-slot-clone", StringComparison.OrdinalIgnoreCase) &&
                    patch.TrueIndex == targetTrueIndex);
            if (slotPatch != null)
            {
                treasureDelta =
                    TreasureValueFromRecordBytes(HexToBytes(slotPatch.AfterHexPreview)) -
                    TreasureValueFromRecordBytes(HexToBytes(slotPatch.BeforeHexPreview));
            }
        }
        return patched;
    }

    private static bool TryGetAutoSlotReuseIdentity(
        JsonElement edit,
        out int targetType,
        out int targetState,
        out int targetSourceByte36,
        out int targetSourceByte37,
        out int targetSourceByte4F,
        out int targetFlag4A,
        out int targetFlag4B)
    {
        targetType = JsonValue.GetInt32(edit, "typeEditedHex", JsonValue.GetInt32(edit, "typeHex", -1));
        targetState = JsonValue.GetInt32(edit, "stateEditedHex", JsonValue.GetInt32(edit, "stateHex", -1));
        targetSourceByte36 = JsonValue.GetInt32(edit, "sourceByte36EditedHex", JsonValue.GetInt32(edit, "sourceByte36Hex", -1));
        targetSourceByte37 = JsonValue.GetInt32(edit, "sourceByte37EditedHex", JsonValue.GetInt32(edit, "sourceByte37Hex", -1));
        targetSourceByte4F = JsonValue.GetInt32(edit, "sourceByte4FEditedHex", JsonValue.GetInt32(edit, "sourceByte4FHex", -1));
        targetFlag4A = JsonValue.GetInt32(edit, "flag4AEditedHex", JsonValue.GetInt32(edit, "flag4AHex", -1));
        targetFlag4B = JsonValue.GetInt32(edit, "flag4BEditedHex", JsonValue.GetInt32(edit, "flag4BHex", -1));
        if (targetType < 0 || targetState < 0 || targetSourceByte36 < 0 || targetSourceByte37 < 0 || targetSourceByte4F < 0 || targetFlag4A < 0 || targetFlag4B < 0)
            return false;
        bool linkedCompanionClone = IsLinkedCompanionNativeClone(edit);
        if (targetType is not (0x18 or 0x20) && !linkedCompanionClone)
            return false;
        if (linkedCompanionClone && !IsCloneableLinkedCompanionType(targetType))
            return false;
        if (TryGetCrossLevelTemplate(edit, out _))
            return false;
        if (IsContainedGemAppend(edit, targetType))
            return false;
        if (LooksLikeLooseVisibleGemIdentity(targetType, targetSourceByte36, targetSourceByte37, targetFlag4A))
            return false;
        if (IsKnownSameLevelLightweightAppend(targetType, targetSourceByte36, targetSourceByte37, targetFlag4A, targetFlag4B))
            return false;

        return true;
    }

    private static bool IsLinkedCompanionNativeClone(JsonElement edit)
    {
        if (!string.Equals(JsonValue.GetString(edit, "patchStatus"), "native-clone", StringComparison.OrdinalIgnoreCase))
            return false;

        string text = $"{JsonValue.GetString(edit, "patchLead")} {JsonValue.GetString(edit, "confidence")} {JsonValue.GetString(edit, "label")} {JsonValue.GetString(edit, "labelEdited")}";
        return MobyCompanionClonePlanner.IsLinkedCompanionCloneText(text);
    }

    private static bool IsCloneableLinkedCompanionType(int targetType)
    {
        return targetType is 0x00 or 0x0A or 0x10 or 0x18 or 0x20 or 0x30 or 0x33 or 0x52;
    }

    private static bool TryPatchSourceRecordCloneIntoSlotCore(
        FileStream stream,
        DiscLayout layout,
        LevelCatalog catalog,
        LevelDefinition targetLevel,
        GeometryCandidate? levelGeometry,
        long targetTableWadOffset,
        string sourceLevelKey,
        int sourceTrueIndex,
        int targetTrueIndex,
        string label,
        JsonElement edit,
        string patchKind,
        string descriptionFormat,
        bool preserveDonorState,
        bool updatePlacementSectorFromPlacement,
        List<MobySourcePatch> patches,
        HashSet<long> writtenWadOffsets,
        List<string> skippedEdits)
    {
        if (sourceTrueIndex < 0)
        {
            skippedEdits.Add($"{label}: native slot reuse is missing a donor source index.");
            return true;
        }

        LevelDefinition? sourceLevel = string.IsNullOrWhiteSpace(sourceLevelKey)
            ? targetLevel
            : catalog.FindByKey(sourceLevelKey);
        if (sourceLevel == null || !sourceLevel.HasSourceTable || sourceTrueIndex >= sourceLevel.SourceRecordCount)
        {
            skippedEdits.Add($"{label}: native slot reuse donor T{sourceTrueIndex} is not mapped.");
            return true;
        }

        if (!string.Equals(LevelCatalog.NormalizeKey(sourceLevel.Key), LevelCatalog.NormalizeKey(targetLevel.Key), StringComparison.OrdinalIgnoreCase))
        {
            skippedEdits.Add($"{label}: cross-level slot reuse still needs actor-package import support; use a same-level donor first.");
            return true;
        }

        long sourceTableWadOffset = string.Equals(sourceLevel.Key, targetLevel.Key, StringComparison.OrdinalIgnoreCase)
            ? targetTableWadOffset
            : ParseRequiredLong(sourceLevel.SourceTableWadOffset, "sourceLevel.sourceTableWadOffset");
        byte[] targetRecord = ReadWadBytes(stream, layout, targetTableWadOffset + ((long)targetTrueIndex * RecordStride), RecordStride);
        byte[] donorRecord = ReadWadBytes(stream, layout, sourceTableWadOffset + ((long)sourceTrueIndex * RecordStride), RecordStride);
        bool preserveTargetBehaviorData = ShouldPreserveTargetBehaviorDataForSlotReuse(patchKind, targetRecord, donorRecord);
        if (preserveTargetBehaviorData)
            PreserveTargetBehaviorData(targetRecord, donorRecord);

        WriteInt32(donorRecord, XOffset, ReadRawAxis(edit, "x", targetRecord, XOffset));
        WriteInt32(donorRecord, YOffset, ReadRawAxis(edit, "y", targetRecord, YOffset));
        WriteInt32(donorRecord, ZOffset, ReadRawAxis(edit, "z", targetRecord, ZOffset));
        WriteYawFromEdit(donorRecord, edit);
        WriteByteFromEdit(donorRecord, TypeOffset, edit, "typeEditedHex", "typeHex");
        if (!preserveDonorState)
            WriteByteFromEdit(donorRecord, StateOffset, edit, "stateEditedHex", "stateHex");
        WriteByteFromEdit(donorRecord, 0x36, edit, "sourceByte36EditedHex", "sourceByte36Hex");
        WriteByteFromEdit(donorRecord, 0x37, edit, "sourceByte37EditedHex", "sourceByte37Hex");
        WriteByteFromEdit(donorRecord, 0x4F, edit, "sourceByte4FEditedHex", "sourceByte4FHex");
        WriteByteFromEdit(donorRecord, 0x52, edit, "flag4AEditedHex", "flag4AHex");
        WriteByteFromEdit(donorRecord, 0x53, edit, "flag4BEditedHex", "flag4BHex");
        ApplySourceByteEdits(donorRecord, edit);

        string placementSectorDescription = "";
        if (updatePlacementSectorFromPlacement && !HasSourceByteEdit(edit, 0x4A) && targetRecord.Length > 0x4A && donorRecord.Length > 0x4A)
        {
            donorRecord[0x4A] = targetRecord[0x4A];
            placementSectorDescription = $" Placement sector byte preserved from reused slot as 0x{donorRecord[0x4A]:X2}.";
        }

        bool allowPlacementSector = updatePlacementSectorFromPlacement || ShouldApplyPlacementSector(donorRecord);
        if (TryApplySourceRecordPlacementSector(levelGeometry, edit, donorRecord, allowPlacementSector, out int placementSectorIndex))
            placementSectorDescription = $" Placement sector byte set to 0x{placementSectorIndex:X2}.";
        string behaviorDescription = preserveTargetBehaviorData
            ? " Reused slot behavior data preserved for this behavior-linked native actor."
            : "";

        AddRawPatch(
            stream,
            layout,
            targetLevel,
            targetTableWadOffset + ((long)targetTrueIndex * RecordStride),
            donorRecord,
            patchKind,
            label,
            targetTrueIndex,
            "0x0",
            $"{string.Format(CultureInfo.InvariantCulture, descriptionFormat, sourceTrueIndex, targetTrueIndex)}{placementSectorDescription}{behaviorDescription}",
            patches,
            writtenWadOffsets);
        return true;
    }

    private static bool TryPatchCrossLevelSourceRecordIntoExistingSlotCandidate(
        FileStream stream,
        DiscLayout layout,
        LevelCatalog catalog,
        LevelDefinition targetLevel,
        GeometryCandidate? levelGeometry,
        long targetTableWadOffset,
        long targetTableRelativeOffset,
        int targetTrueIndex,
        string label,
        JsonElement edit,
        JsonElement crossLevelTemplate,
        GreenWizardResidentSwapBuildMode residentBuildMode,
        List<MobySourcePatch> patches,
        HashSet<long> writtenWadOffsets,
        Dictionary<string, CrossLevelSharedSpecialCluster> sharedCrossLevelSpecialClusters,
        List<string> skippedEdits)
    {
        string templateId = JsonValue.GetString(crossLevelTemplate, "id", "cross-level swap");
        string sourceLevelKey = JsonValue.GetString(crossLevelTemplate, "sourceLevelKey");
        string family = JsonValue.GetString(crossLevelTemplate, "family");
        int sourceTrueIndex = JsonValue.GetInt32(crossLevelTemplate, "sourceTrueIndex", -1);
        LevelDefinition? sourceLevel = catalog.FindByKey(sourceLevelKey);
        if (sourceLevel == null || !sourceLevel.HasSourceTable || sourceTrueIndex < 0 || sourceTrueIndex >= sourceLevel.SourceRecordCount)
        {
            skippedEdits.Add($"{label}: {templateId} does not have a mapped source donor row.");
            return false;
        }

        if (string.Equals(
                LevelCatalog.NormalizeKey(sourceLevel.Key),
                LevelCatalog.NormalizeKey(targetLevel.Key),
                StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(family, "enemyTransform", StringComparison.OrdinalIgnoreCase))
        {
            skippedEdits.Add($"{label}: {templateId} is from this level and should use native same-level slot reuse instead of candidate mode.");
            return false;
        }

        long sourceTableWadOffset = ParseRequiredLong(sourceLevel.SourceTableWadOffset, "sourceLevel.sourceTableWadOffset");
        long sourceTableRelativeOffset = string.IsNullOrWhiteSpace(sourceLevel.SourceTableRelativeOffset)
            ? 0
            : ParseRequiredLong(sourceLevel.SourceTableRelativeOffset, "sourceLevel.sourceTableRelativeOffset");
        byte[] targetRecord = ReadWadBytes(
            stream,
            layout,
            targetTableWadOffset + ((long)targetTrueIndex * RecordStride),
            RecordStride);
        byte[] donorRecord = ReadWadBytes(
            stream,
            layout,
            sourceTableWadOffset + ((long)sourceTrueIndex * RecordStride),
            RecordStride);

        bool chestSwap = string.Equals(family, "catalogChest", StringComparison.OrdinalIgnoreCase);
        bool enemySwap = string.Equals(family, "catalogEnemy", StringComparison.OrdinalIgnoreCase);
        bool enemyTransform = string.Equals(family, "enemyTransform", StringComparison.OrdinalIgnoreCase);
        if (!chestSwap && !enemySwap && !enemyTransform)
        {
            skippedEdits.Add($"{label}: {templateId} is not in the guarded chest or self-contained enemy catalogue.");
            return false;
        }

        if (targetRecord[TypeOffset] is not (0x18 or 0x20) || donorRecord[TypeOffset] is not (0x18 or 0x20))
        {
            skippedEdits.Add($"{label}: {templateId} does not fit the existing chest/enemy slot candidate writer.");
            return false;
        }

        int donorActorId = donorRecord[0x36] | (donorRecord[0x37] << 8);
        uint targetActorRoot = 0;
        bool targetActorRootPresent = targetTableRelativeOffset > 0 &&
            TryFindTargetActorRoot(
                stream,
                layout,
                targetTableWadOffset - targetTableRelativeOffset,
                donorActorId,
                out targetActorRoot);
        if (!targetActorRootPresent)
        {
            skippedEdits.Add(
                $"{label}: {targetLevel.DisplayName} does not load actor 0x{donorActorId:X4}; " +
                "the row-only swap was refused because it needs a mapped actor/model package and target behavior route.");
            return false;
        }

        bool greenWizardResidentCandidate = false;
        GreenWizardResidentSwapRoute? greenWizardResidentRoute = null;
        if (enemyTransform)
        {
            if (donorActorId == GreenWizardRuntimeBundleCompatibility.ActorId &&
                string.Equals(
                    LevelCatalog.NormalizeKey(targetLevel.Key),
                    GreenWizardRuntimeBundleCompatibility.SourceLevelKey,
                    StringComparison.OrdinalIgnoreCase) &&
                targetTrueIndex != GreenWizardRuntimeBundleCompatibility.WizardPeakFocusedTargetTrueIndex)
            {
                skippedEdits.Add(
                    $"{label}: the first Wizard Peak Green Wizard in-place recipe is frozen to Elder Wizard " +
                    $"T{GreenWizardRuntimeBundleCompatibility.WizardPeakFocusedTargetTrueIndex}; T{targetTrueIndex} remains unavailable until it receives its own properties-extent and pointer-fixup proof.");
                return false;
            }

            if (donorActorId == GreenWizardRuntimeBundleCompatibility.ActorId &&
                string.Equals(
                    LevelCatalog.NormalizeKey(targetLevel.Key),
                    GreenWizardRuntimeBundleCompatibility.MagicCraftersSourceLevelKey,
                    StringComparison.OrdinalIgnoreCase) &&
                targetTrueIndex != GreenWizardRuntimeBundleCompatibility.MagicCraftersFocusedTargetTrueIndex)
            {
                skippedEdits.Add(
                    $"{label}: the runtime-proven Magic Crafters Green Wizard in-place properties recipe is frozen to " +
                    $"T{GreenWizardRuntimeBundleCompatibility.MagicCraftersFocusedTargetTrueIndex}; T{targetTrueIndex} remains unavailable until it receives its own properties-extent and pointer-fixup proof.");
                return false;
            }

            bool sourceRecordHasPerInstanceData = BinaryPrimitives.ReadUInt32LittleEndian(donorRecord) != 0;
            if (!GreenWizardResidentSwapComposer.IsTemplateEligible(
                    targetLevel,
                    family,
                    sourceLevel.Key,
                    sourceTrueIndex,
                    donorActorId,
                    sourceRecordHasPerInstanceData,
                    targetActorRootPresent,
                    residentBuildMode,
                    out GreenWizardResidentSwapRoute residentRoute,
                    out string residentReason))
            {
                skippedEdits.Add(
                    $"{label}: {residentReason} " +
                    "An actor-root match alone does not prove the Wizard handler, lightning dependency, particles, or route data are safe in this level.");
                return false;
            }
            greenWizardResidentCandidate = true;
            greenWizardResidentRoute = residentRoute;
        }

        if (chestSwap)
            PreserveTargetBehaviorData(targetRecord, donorRecord);

        WriteInt32(donorRecord, XOffset, ReadRawAxis(edit, "x", targetRecord, XOffset));
        WriteInt32(donorRecord, YOffset, ReadRawAxis(edit, "y", targetRecord, YOffset));
        WriteInt32(donorRecord, ZOffset, ReadRawAxis(edit, "z", targetRecord, ZOffset));
        if (greenWizardResidentCandidate)
        {
            Array.Copy(targetRecord, YawMatrixOffset, donorRecord, YawMatrixOffset, 18);
            donorRecord[YawByteOffset] = targetRecord[YawByteOffset];
        }
        if (!greenWizardResidentCandidate)
            WriteYawFromEdit(donorRecord, edit);
        if (!greenWizardResidentCandidate)
        {
            WriteByteFromEdit(donorRecord, TypeOffset, edit, "typeEditedHex", "typeHex");
            WriteByteFromEdit(donorRecord, StateOffset, edit, "stateEditedHex", "stateHex");
            WriteByteFromEdit(donorRecord, 0x36, edit, "sourceByte36EditedHex", "sourceByte36Hex");
            WriteByteFromEdit(donorRecord, 0x37, edit, "sourceByte37EditedHex", "sourceByte37Hex");
            WriteByteFromEdit(donorRecord, 0x4F, edit, "sourceByte4FEditedHex", "sourceByte4FHex");
            WriteByteFromEdit(donorRecord, 0x52, edit, "flag4AEditedHex", "flag4AHex");
            WriteByteFromEdit(donorRecord, 0x53, edit, "flag4BEditedHex", "flag4BHex");
            ApplySourceByteEdits(donorRecord, edit);
        }
        if (greenWizardResidentCandidate)
            donorRecord[0x53] = targetRecord[0x53];

        string specialDataDescription = "";
        if (enemySwap && BitConverter.ToUInt32(donorRecord, 0) != 0)
        {
            CrossLevelAppendDonor donor = new(
                sourceLevel,
                sourceTableWadOffset,
                sourceTableRelativeOffset,
                sourceTrueIndex,
                "ExistingSlotCandidate");
            if (!TryAppendCrossLevelSpecialData(
                stream,
                layout,
                targetLevel,
                targetTableWadOffset,
                targetTableRelativeOffset,
                donor,
                targetTrueIndex,
                label,
                donorRecord,
                patches,
                writtenWadOffsets,
                sharedCrossLevelSpecialClusters,
                skippedEdits,
                out specialDataDescription))
            {
                return false;
            }
        }

        string placementSectorDescription = "";
        if (greenWizardResidentCandidate)
        {
            donorRecord[0x4A] = targetRecord[0x4A];
            placementSectorDescription = $" Placement sector byte preserved from target T{targetTrueIndex} as 0x{donorRecord[0x4A]:X2}.";
        }
        else if (!HasSourceByteEdit(edit, 0x4A))
        {
            donorRecord[0x4A] = targetRecord[0x4A];
            placementSectorDescription = $" Placement sector byte preserved from target T{targetTrueIndex} as 0x{donorRecord[0x4A]:X2}.";
        }
        if (!greenWizardResidentCandidate &&
            TryApplySourceRecordPlacementSector(levelGeometry, edit, donorRecord, allowPlacementSector: true, out int placementSectorIndex))
            placementSectorDescription = $" Placement sector byte set from the selected placement to 0x{placementSectorIndex:X2}.";

        string routePointDescription = greenWizardResidentRoute?.RoutePointCount switch
        {
            1 => "one-point",
            2 => "two-point",
            int count => $"{count}-point",
            _ => "profile-defined"
        };
        bool magicCraftersWizardInPlaceControl = greenWizardResidentCandidate &&
            string.Equals(LevelCatalog.NormalizeKey(targetLevel.Key), "magiccrafters", StringComparison.OrdinalIgnoreCase);
        bool wizardPeakInPlaceControl = greenWizardResidentCandidate &&
            string.Equals(LevelCatalog.NormalizeKey(targetLevel.Key), "wizardpeak", StringComparison.OrdinalIgnoreCase);
        string behaviorDescription = chestSwap
            ? " Target chest behavior/reward bytes were preserved while borrowing the donor chest shell."
            : greenWizardResidentCandidate
            ? magicCraftersWizardInPlaceControl
                ? $" The selected enemy's XYZ, yaw, culling sector, pod/group, and reward stay in this slot. The v2 control installs donor T{greenWizardResidentRoute!.DonorTrueIndex}'s private 0x{greenWizardResidentRoute.PropertiesBytes:X} {routePointDescription} properties in T{targetTrueIndex}'s existing extent, removes its stale route-padding fixup, and does not resize or shift scene components."
                : wizardPeakInPlaceControl
                ? $" The selected Elder Wizard's XYZ, yaw, culling sector, native pod/group 0xFF, and reward stay in this slot. Runtime-proven v3 installs donor T{greenWizardResidentRoute!.DonorTrueIndex}'s private 0x{greenWizardResidentRoute.PropertiesBytes:X} {routePointDescription} properties in pod-matched T{targetTrueIndex}'s existing extent, replaces the stale private-pointer fixup in place, and does not resize or shift scene components."
                : $" The selected enemy's XYZ, yaw, culling sector, pod/group, and reward stay in this slot after the composed {targetLevel.DisplayName} resident-bundle step expands the native properties component for a private 0x{greenWizardResidentRoute!.PropertiesBytes:X} Wizard block, translates donor T{greenWizardResidentRoute.DonorTrueIndex}'s {routePointDescription} route, shifts the pod/collision/fixup components intact, and appends the internal-pointer fixup."
            : " Donor enemy behavior bytes were retained; this self-contained classification still requires DuckStation validation.";
        string swapDescription = greenWizardResidentCandidate
            ? $"{(greenWizardResidentRoute!.NormalCreateBinReady ? "Verified" : "Guarded")} {targetLevel.DisplayName} resident-bundle replacement: replace {targetLevel.DisplayName} T{targetTrueIndex} with {sourceLevel.DisplayName} donor T{sourceTrueIndex}. Target-resident actor 0x{donorActorId:X4} uses root 0x{targetActorRoot:X}. Source object count remains {targetLevel.SourceRecordCount}; no row is appended. Registered structural recipe {greenWizardResidentRoute.RecipeId} completes this source-row patch during {(residentBuildMode == GreenWizardResidentSwapBuildMode.Normal ? "normal Create BIN" : "Create Swap Test")}.{placementSectorDescription}{behaviorDescription}{specialDataDescription}"
            : $"Disposable cross-level existing-slot candidate: replace {targetLevel.DisplayName} T{targetTrueIndex} with {sourceLevel.DisplayName} donor T{sourceTrueIndex}. Target-resident actor 0x{donorActorId:X4} uses root 0x{targetActorRoot:X}. Source object count remains {targetLevel.SourceRecordCount}; no row is appended.{placementSectorDescription}{behaviorDescription}{specialDataDescription}";
        AddRawPatch(
            stream,
            layout,
            targetLevel,
            targetTableWadOffset + ((long)targetTrueIndex * RecordStride),
            donorRecord,
            "cross-level-existing-slot-candidate",
            label,
            targetTrueIndex,
            "0x0",
            swapDescription,
            patches,
            writtenWadOffsets);
        return true;
    }

    private static bool TryFindTargetActorRoot(
        FileStream stream,
        DiscLayout layout,
        long targetEntryBase,
        int actorId,
        out uint root)
    {
        root = 0;
        for (int index = 0; index < 64; index++)
        {
            ushort candidateActor = BitConverter.ToUInt16(
                ReadWadBytes(stream, layout, targetEntryBase + 0x150 + (index * 2L), 2),
                0);
            if (candidateActor != actorId)
                continue;

            uint candidateRoot = BitConverter.ToUInt32(
                ReadWadBytes(stream, layout, targetEntryBase + 0x50 + (index * 4L), 4),
                0);
            if (candidateRoot == 0)
                continue;

            root = candidateRoot;
            return true;
        }

        return false;
    }

    private static bool ShouldPreserveTargetBehaviorDataForSlotReuse(string patchKind, byte[] targetRecord, byte[] donorRecord)
    {
        if (!string.Equals(patchKind, "moby-record-auto-slot-clone", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(patchKind, "moby-record-slot-clone", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (targetRecord.Length <= 0x53 || donorRecord.Length <= 0x53)
            return false;
        if (targetRecord[TypeOffset] != 0x20 || donorRecord[TypeOffset] != 0x20)
            return false;
        if (targetRecord[0x36] != donorRecord[0x36] || targetRecord[0x37] != donorRecord[0x37])
            return false;

        ushort targetBehaviorMarker = BitConverter.ToUInt16(targetRecord, 0x38);
        ushort donorBehaviorMarker = BitConverter.ToUInt16(donorRecord, 0x38);
        return targetRecord[0x4F] != 0x00 ||
            donorRecord[0x4F] != 0x00 ||
            targetBehaviorMarker != 0x0000 ||
            donorBehaviorMarker != 0x0000;
    }

    private static void PreserveTargetBehaviorData(byte[] targetRecord, byte[] donorRecord)
    {
        Array.Copy(targetRecord, 0x00, donorRecord, 0x00, 0x04);
        Array.Copy(targetRecord, 0x38, donorRecord, 0x38, 0x0E);
    }

    private static HashSet<int> BuildProtectedSourceSlots(JsonElement editsElement, int sourceRecordCount)
    {
        HashSet<int> protectedSlots = new();
        foreach (JsonElement edit in editsElement.EnumerateArray())
        {
            bool added = JsonValue.GetBoolean(edit, "added") ||
                string.Equals(JsonValue.GetString(edit, "editKind"), "add", StringComparison.OrdinalIgnoreCase);
            if (added)
            {
                if (IsLinkedCompanionNativeClone(edit) &&
                    edit.TryGetProperty("recordMutation", out JsonElement mutation) &&
                    mutation.ValueKind == JsonValueKind.Object)
                {
                    int sourceTrueIndex = JsonValue.GetInt32(mutation, "sourceTrueIndex", -1);
                    if (sourceTrueIndex >= 0 && sourceTrueIndex < sourceRecordCount)
                        protectedSlots.Add(sourceTrueIndex);
                }
                continue;
            }

            int trueIndex = JsonValue.GetInt32(edit, "trueIndex", -1);
            if (trueIndex >= 0 && trueIndex < sourceRecordCount)
                protectedSlots.Add(trueIndex);
        }

        return protectedSlots;
    }

    private static int FindAutoReuseTargetSlot(
        FileStream stream,
        DiscLayout layout,
        LevelDefinition level,
        long tableWadOffset,
        int donorTrueIndex,
        int targetType,
        int targetState,
        int targetSourceByte36,
        int targetSourceByte37,
        int targetSourceByte4F,
        int targetFlag4A,
        int targetFlag4B,
        HashSet<int> protectedSourceSlots,
        HashSet<int> autoReuseTargetSlots,
        HashSet<long> writtenWadOffsets)
    {
        if (donorTrueIndex < 0 || donorTrueIndex >= level.SourceRecordCount)
            return -1;

        List<AutoReuseSlotCandidate> candidates = new();
        for (int trueIndex = 0; trueIndex < level.SourceRecordCount; trueIndex++)
        {
            if (autoReuseTargetSlots.Contains(trueIndex) ||
                protectedSourceSlots.Contains(trueIndex))
                continue;

            long recordWadOffset = tableWadOffset + ((long)trueIndex * RecordStride);
            if (OverlapsWrittenOffsets(recordWadOffset, RecordStride, writtenWadOffsets))
                continue;

            byte[] record = ReadWadBytes(stream, layout, recordWadOffset, RecordStride);
            int score = ScoreAutoReuseSlot(
                record,
                trueIndex == donorTrueIndex,
                targetType,
                targetState,
                targetSourceByte36,
                targetSourceByte37,
                targetSourceByte4F,
                targetFlag4A,
                targetFlag4B);
            if (score >= 0)
                candidates.Add(new AutoReuseSlotCandidate(trueIndex, score));
        }

        return candidates
            .OrderBy(candidate => candidate.Score)
            .ThenBy(candidate => candidate.TrueIndex)
            .Select(candidate => candidate.TrueIndex)
            .FirstOrDefault(-1);
    }

    private static int ScoreAutoReuseSlot(
        byte[] record,
        bool isDonorSlot,
        int targetType,
        int targetState,
        int targetSourceByte36,
        int targetSourceByte37,
        int targetSourceByte4F,
        int targetFlag4A,
        int targetFlag4B)
    {
        if (record.Length <= 0x53)
            return -1;
        if (record[TypeOffset] != (byte)targetType)
            return -1;

        bool exactFamily =
            record[0x36] == (byte)targetSourceByte36 &&
            record[0x37] == (byte)targetSourceByte37 &&
            record[0x4F] == (byte)targetSourceByte4F &&
            record[0x52] == (byte)targetFlag4A &&
            record[0x53] == (byte)targetFlag4B;
        if (exactFamily)
            return (record[StateOffset] == (byte)targetState ? 0 : 2) + (isDonorSlot ? 10 : 0);

        bool sameActorOrChestFamily =
            record[0x36] == (byte)targetSourceByte36 &&
            record[0x37] == (byte)targetSourceByte37 &&
            record[0x52] == (byte)targetFlag4A;
        if (sameActorOrChestFamily)
            return (record[StateOffset] == (byte)targetState ? 20 : 22) + (isDonorSlot ? 10 : 0);

        return -1;
    }

    private readonly record struct AutoReuseSlotCandidate(int TrueIndex, int Score);

    private static bool TryAddAppendPatch(
        FileStream stream,
        DiscLayout layout,
        LevelCatalog catalog,
        LevelDefinition level,
        GeometryCandidate? levelGeometry,
        long tableWadOffset,
        long tableRelativeOffset,
        int appendTrueIndex,
        string label,
        JsonElement edit,
        string sourceImagePath,
        string workspaceRoot,
        bool allowPlanOnlyActorPackageImports,
        bool suppressActorPackageImports,
        bool allowGuardedNativeCloneAppend,
        List<MobySourcePatch> patches,
        List<MobyActorPackageImportPreview> packageImportPreviews,
        HashSet<long> writtenWadOffsets,
        HashSet<string> writtenActorPackageRecipes,
        Dictionary<string, CrossLevelSharedSpecialCluster> sharedCrossLevelSpecialClusters,
        List<string> skippedEdits)
    {
        int targetType = JsonValue.GetInt32(edit, "typeEditedHex", JsonValue.GetInt32(edit, "typeHex", -1));
        CrossLevelAppendDonor? crossLevelDonor = null;
        if (TryGetCrossLevelTemplate(edit, out JsonElement crossLevelTemplate))
        {
            string templateId = JsonValue.GetString(crossLevelTemplate, "id", "cross-level template");
            string sourceLevelName = JsonValue.GetString(crossLevelTemplate, "sourceLevelName", "another level");
            int sourceTrueIndex = JsonValue.GetInt32(crossLevelTemplate, "sourceTrueIndex", -1);
            bool isSourceRecordCandidate = IsCrossLevelSourceRecordCandidateTemplate(crossLevelTemplate);
            if (isSourceRecordCandidate || !IsSimpleCrossLevelTemplate(crossLevelTemplate))
            {
                if (isSourceRecordCandidate && !allowPlanOnlyActorPackageImports)
                {
                    skippedEdits.Add($"{label}: {templateId} is a guarded cross-level source-record candidate. Use Create Candidate BIN to write a disposable test; normal Create BIN keeps it saved but skipped.");
                    return false;
                }

                string recipeMode = isSourceRecordCandidate ? CrossLevelSourceRecordCandidateRecipeMode : "";
                if (!isSourceRecordCandidate && !suppressActorPackageImports)
                {
                    MobyActorPackageImportPreview preview = AddActorPackageImportPreview(stream, layout, catalog, level, tableWadOffset, tableRelativeOffset, label, crossLevelTemplate, sourceImagePath, workspaceRoot, allowPlanOnlyActorPackageImports, packageImportPreviews, patches, writtenWadOffsets, writtenActorPackageRecipes);
                    recipeMode = preview.RecipeMode;
                    string source = sourceTrueIndex >= 0 ? $"{sourceLevelName} T{sourceTrueIndex}" : sourceLevelName;
                    string requiredFeature = JsonValue.GetString(crossLevelTemplate, "requiredExporterFeature");
                    if (!preview.CanWriteImage)
                    {
                        skippedEdits.Add($"{label}: {templateId} comes from {source} and needs {requiredFeature} before Create Test BIN can make it playable.");
                        return false;
                    }
                }

                LevelDefinition? sourceLevel = catalog.FindByKey(JsonValue.GetString(crossLevelTemplate, "sourceLevelKey"));
                if (sourceLevel == null || !sourceLevel.HasSourceTable || sourceTrueIndex < 0 || sourceTrueIndex >= sourceLevel.SourceRecordCount)
                {
                    skippedEdits.Add($"{label}: {templateId} has a writable package recipe, but its donor source record is not mapped.");
                    return false;
                }

                long sourceTableWadOffset = ParseRequiredLong(sourceLevel.SourceTableWadOffset, "sourceLevel.sourceTableWadOffset");
                long sourceTableRelativeOffset = string.IsNullOrWhiteSpace(sourceLevel.SourceTableRelativeOffset)
                    ? 0
                    : ParseRequiredLong(sourceLevel.SourceTableRelativeOffset, "sourceLevel.sourceTableRelativeOffset");
                crossLevelDonor = new CrossLevelAppendDonor(sourceLevel, sourceTableWadOffset, sourceTableRelativeOffset, sourceTrueIndex, recipeMode);
            }
        }

        int targetSourceByte36 = JsonValue.GetInt32(edit, "sourceByte36EditedHex", JsonValue.GetInt32(edit, "sourceByte36Hex", -1));
        int targetSourceByte37 = JsonValue.GetInt32(edit, "sourceByte37EditedHex", JsonValue.GetInt32(edit, "sourceByte37Hex", -1));
        int targetSourceByte4F = JsonValue.GetInt32(edit, "sourceByte4FEditedHex", JsonValue.GetInt32(edit, "sourceByte4FHex", -1));
        int targetFlag4A = JsonValue.GetInt32(edit, "flag4AEditedHex", JsonValue.GetInt32(edit, "flag4AHex", -1));
        int targetFlag4B = JsonValue.GetInt32(edit, "flag4BEditedHex", JsonValue.GetInt32(edit, "flag4BHex", -1));
        bool isContainedGemAppend = IsContainedGemAppend(edit, targetType);
        bool isLooseVisibleGemAppend = IsLooseVisibleGemIdentity(targetType, targetSourceByte36, targetSourceByte37, targetSourceByte4F, targetFlag4A, targetFlag4B);
        bool isKnownSameLevelLightweightAppend = IsKnownSameLevelLightweightAppend(targetType, targetSourceByte36, targetSourceByte37, targetFlag4A, targetFlag4B);
        bool isPortableSpringChestControllerAppend = IsPortableSpringChestControllerAppend(edit, targetType, targetSourceByte36, targetSourceByte37);
        bool isPromotedSameLevelNativeCloneAppend = crossLevelDonor == null && IsPromotedSameLevelNativeCloneAppendIdentity(
            level.Key,
            targetType,
            targetSourceByte36,
            targetSourceByte37,
            targetSourceByte4F,
            targetFlag4A,
            targetFlag4B);
        bool isProvenNativeCloneAppend = IsProvenNativeCloneAppend(edit);
        bool isGuardedNativeCloneAppend = IsGuardedNativeCloneAppend(
            edit,
            targetType,
            targetSourceByte36,
            targetSourceByte37,
            targetSourceByte4F,
            targetFlag4A,
            targetFlag4B);

        if (crossLevelDonor == null &&
            !isContainedGemAppend &&
            !isLooseVisibleGemAppend &&
            !isKnownSameLevelLightweightAppend &&
            !isPortableSpringChestControllerAppend &&
            !isPromotedSameLevelNativeCloneAppend &&
            !isProvenNativeCloneAppend)
        {
            skippedEdits.Add($"{label}: copied object export is guarded because this object class does not yet have a proven native append recipe; it remains saved in the editor.");
            return false;
        }

        if (crossLevelDonor == null &&
            isGuardedNativeCloneAppend &&
            !allowGuardedNativeCloneAppend &&
            !isPromotedSameLevelNativeCloneAppend)
        {
            skippedEdits.Add($"{label}: same-level enemy/chest true-adds are kept saved but skipped from Create BIN for now because native actor/chest clones can freeze in-game. Use Change To / slot replacement for safe enemy or chest swaps.");
            return false;
        }

        if (targetType is not (0x18 or 0x20) &&
            !isContainedGemAppend &&
            !isKnownSameLevelLightweightAppend)
        {
            skippedEdits.Add($"{label}: true-add export for type 0x{Math.Clamp(targetType, 0, 255):X2} needs actor-package handling first.");
            return false;
        }

        long appendWadOffset = tableWadOffset + (appendTrueIndex * RecordStride);
        byte[] beforeAppend = ReadWadBytes(stream, layout, appendWadOffset, RecordStride);
        if (!beforeAppend.All(value => value == 0))
        {
            skippedEdits.Add($"{label}: append slot T{appendTrueIndex} is not empty in the source image.");
            return false;
        }

        int sameLevelAppendDonorTrueIndex = TryGetSameLevelAppendDonor(edit, level, out int explicitSameLevelDonorTrueIndex)
            ? explicitSameLevelDonorTrueIndex
            : -1;
        int donorTrueIndex = crossLevelDonor?.SourceTrueIndex ?? (isContainedGemAppend
            ? FindContainedGemDonor(stream, layout, tableWadOffset, tableRelativeOffset, level.SourceRecordCount)
            : isLooseVisibleGemAppend && TryFindNearestLooseVisibleGemDonor(
                stream,
                layout,
                tableWadOffset,
                tableRelativeOffset,
                level.SourceRecordCount,
                edit,
                targetSourceByte36,
                out int nearestLooseGemDonor)
            ? nearestLooseGemDonor
            : sameLevelAppendDonorTrueIndex >= 0
            ? sameLevelAppendDonorTrueIndex
            : FindSameLevelDonor(
                stream,
                layout,
                tableWadOffset,
                level.SourceRecordCount,
                targetType,
                targetSourceByte36,
                targetSourceByte37,
                targetSourceByte4F,
                targetFlag4A,
                targetFlag4B));
        if (donorTrueIndex < 0)
        {
            skippedEdits.Add(isContainedGemAppend
                ? $"{label}: no same-level contained-gem donor with source special data was found."
                : $"{label}: no simple same-level donor record was found for type 0x{targetType:X2}.");
            return false;
        }

        int requestedDonorTrueIndex = donorTrueIndex;
        if (crossLevelDonor == null)
        {
            donorTrueIndex = SelectBehaviorLinkedAppendDonorWithUsableSpecialData(
                stream,
                layout,
                tableWadOffset,
                tableRelativeOffset,
                level.SourceRecordCount,
                donorTrueIndex,
                targetType,
                targetSourceByte36,
                targetSourceByte37,
                targetSourceByte4F,
                targetFlag4A,
                targetFlag4B);
        }

        long donorTableWadOffset = crossLevelDonor?.SourceTableWadOffset ?? tableWadOffset;
        byte[] recordBytes = ReadWadBytes(stream, layout, donorTableWadOffset + (donorTrueIndex * RecordStride), RecordStride);
        bool preserveDonorStartupBytesForNativeCloneAppend =
            crossLevelDonor == null &&
            isProvenNativeCloneAppend &&
            targetType == 0x20 &&
            (allowGuardedNativeCloneAppend || isPromotedSameLevelNativeCloneAppend);
        byte donorSourceState = recordBytes.Length > StateOffset ? recordBytes[StateOffset] : (byte)0;
        byte donorPlacementSector = recordBytes.Length > 0x4A ? recordBytes[0x4A] : (byte)0;
        byte donorFlag4A = recordBytes.Length > 0x52 ? recordBytes[0x52] : (byte)0;
        WriteInt32(recordBytes, XOffset, ReadRawAxis(edit, "x", recordBytes, XOffset));
        WriteInt32(recordBytes, YOffset, ReadRawAxis(edit, "y", recordBytes, YOffset));
        WriteInt32(recordBytes, ZOffset, ReadRawAxis(edit, "z", recordBytes, ZOffset));
        WriteYawFromEdit(recordBytes, edit);
        WriteByteFromEdit(recordBytes, TypeOffset, edit, "typeEditedHex", "typeHex");
        if (!preserveDonorStartupBytesForNativeCloneAppend || HasSourceByteEdit(edit, StateOffset))
            WriteByteFromEdit(recordBytes, StateOffset, edit, "stateEditedHex", "stateHex");
        WriteByteFromEdit(recordBytes, 0x36, edit, "sourceByte36EditedHex", "sourceByte36Hex");
        WriteByteFromEdit(recordBytes, 0x37, edit, "sourceByte37EditedHex", "sourceByte37Hex");
        WriteByteFromEdit(recordBytes, 0x4F, edit, "sourceByte4FEditedHex", "sourceByte4FHex");
        bool preserveDonorFlag4AForNativeCloneAppend =
            preserveDonorStartupBytesForNativeCloneAppend &&
            targetSourceByte37 != 0x00 &&
            !HasExplicitSourceByteEdit(edit, 0x52);
        if (!preserveDonorFlag4AForNativeCloneAppend)
            WriteByteFromEdit(recordBytes, 0x52, edit, "flag4AEditedHex", "flag4AHex");
        WriteByteFromEdit(recordBytes, 0x53, edit, "flag4BEditedHex", "flag4BHex");
        ApplySourceByteEdits(recordBytes, edit);
        if (preserveDonorFlag4AForNativeCloneAppend && recordBytes.Length > 0x52)
            recordBytes[0x52] = donorFlag4A;
        if (isLooseVisibleGemAppend)
            NormalizeLooseVisibleGemRecord(recordBytes);
        bool preserveDonorPlacementSectorForGuardedNativeCloneAppend =
            allowGuardedNativeCloneAppend &&
            LevelCatalog.NormalizeKey(level.Key).Equals("townsquare", StringComparison.OrdinalIgnoreCase) &&
            !isPromotedSameLevelNativeCloneAppend &&
            preserveDonorStartupBytesForNativeCloneAppend &&
            donorPlacementSector == 0xFF &&
            !HasSourceByteEdit(edit, 0x4A);
        bool forcePlacementSectorFromPlacement = !preserveDonorPlacementSectorForGuardedNativeCloneAppend &&
            (allowGuardedNativeCloneAppend || isPromotedSameLevelNativeCloneAppend) &&
            crossLevelDonor == null &&
            isProvenNativeCloneAppend;
        string placementSectorDescription = "";
        if (preserveDonorPlacementSectorForGuardedNativeCloneAppend && recordBytes.Length > 0x4A)
        {
            recordBytes[0x4A] = donorPlacementSector;
            placementSectorDescription = $" Donor placement sector preserved as 0x{donorPlacementSector:X2} for guarded native clone append.";
        }
        else if (TryApplySourceRecordPlacementSector(levelGeometry, edit, recordBytes, forcePlacementSectorFromPlacement || ShouldApplyPlacementSector(recordBytes), out int placementSectorIndex))
            placementSectorDescription = $" Placement sector byte set to 0x{placementSectorIndex:X2}.";
        string donorUpgradeDescription = donorTrueIndex != requestedDonorTrueIndex
            ? $" Behavior donor upgraded from copied T{requestedDonorTrueIndex} to same-family T{donorTrueIndex} because the copied donor's behavior data was blank."
            : "";
        string sourceStateDescription = preserveDonorStartupBytesForNativeCloneAppend
            ? $" Source startup state preserved from donor as 0x{donorSourceState:X2} for native clone append."
            : "";
        if (preserveDonorFlag4AForNativeCloneAppend)
            sourceStateDescription += $" Donor flag4A preserved as 0x{donorFlag4A:X2} for native clone append.";
        if (IsSpringChestControllerAliasAppend(edit, packageImportPreviews))
        {
            recordBytes[0x36] = 0xFE;
            recordBytes[0x37] = 0x01;
        }
        string specialDataDescription = "";
        if (isContainedGemAppend)
        {
            if (!TryAppendContainedGemSpecialData(
                stream,
                layout,
                level,
                tableWadOffset,
                tableRelativeOffset,
                donorTrueIndex,
                appendTrueIndex,
                label,
                edit,
                recordBytes,
                patches,
                writtenWadOffsets,
                skippedEdits,
                out specialDataDescription))
            {
                return false;
            }
        }
        else if (crossLevelDonor != null)
        {
            if (!TryAppendCrossLevelSpecialData(
                stream,
                layout,
                level,
                tableWadOffset,
                tableRelativeOffset,
                crossLevelDonor,
                appendTrueIndex,
                label,
                recordBytes,
                patches,
                writtenWadOffsets,
                sharedCrossLevelSpecialClusters,
                skippedEdits,
                out specialDataDescription))
            {
                return false;
            }
        }
        else if (IsLooseVisibleGemRecord(recordBytes))
        {
            if (!TryAppendSameLevelSpecialData(
                stream,
                layout,
                level,
                tableWadOffset,
                tableRelativeOffset,
                donorTrueIndex,
                appendTrueIndex,
                label,
                recordBytes,
                patches,
                writtenWadOffsets,
                skippedEdits,
                out specialDataDescription))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(specialDataDescription))
            {
                skippedEdits.Add($"{label}: loose gem donor T{donorTrueIndex} did not expose cloneable visual data.");
                return false;
            }

            specialDataDescription = specialDataDescription.Replace(
                "Cloned same-level special data",
                "Cloned same-level donor gem visual data",
                StringComparison.Ordinal);
        }
        else if (TryHasSameLevelSourceSpecialData(tableRelativeOffset, recordBytes))
        {
            bool aliasNativeLifeChestSpecialData = IsNativeLifeChestIdentity(
                recordBytes[TypeOffset],
                recordBytes[0x36],
                recordBytes[0x37],
                recordBytes[0x4F],
                recordBytes[0x52],
                recordBytes[0x53]);
            bool usePrivateLifeChestRuntimePointer =
                TryGetPrivateLifeChestRuntimeBase(level.Key, edit, recordBytes, out uint privateLifeChestRuntimeBase);
            bool aliasSpecialDataForResearch = edit.TryGetProperty("aliasSameLevelSpecialData", out JsonElement aliasSpecialData) &&
                aliasSpecialData.ValueKind == JsonValueKind.True;
            if (usePrivateLifeChestRuntimePointer)
            {
                if (!TryAppendLifeChestPrivateSpecialData(
                        stream,
                        layout,
                        level,
                        tableWadOffset,
                        donorTrueIndex,
                        appendTrueIndex,
                        label,
                        recordBytes,
                        patches,
                        writtenWadOffsets,
                        skippedEdits,
                        out specialDataDescription))
                {
                    return false;
                }

                uint privateSourceOffset = BitConverter.ToUInt32(recordBytes, 0);
                uint privateRuntimePointer = checked(privateLifeChestRuntimeBase + privateSourceOffset);
                WriteInt32(recordBytes, 0, unchecked((int)privateRuntimePointer));
                specialDataDescription +=
                    $" Wrote pre-relocated private runtime pointer 0x{privateRuntimePointer:X8} " +
                    $"(base 0x{privateLifeChestRuntimeBase:X8} + source offset 0x{privateSourceOffset:X}) for appended T{appendTrueIndex}.";
            }
            else if (aliasNativeLifeChestSpecialData || aliasSpecialDataForResearch)
            {
                uint donorSpecialDataOffset = BitConverter.ToUInt32(recordBytes, 0);
                specialDataDescription = $" Reused same-level donor T{donorTrueIndex} source special-data offset 0x{donorSpecialDataOffset:X} so the native loader can relocate it for appended T{appendTrueIndex}.";
            }
            else if (!TryAppendSameLevelSpecialData(
                         stream,
                         layout,
                         level,
                         tableWadOffset,
                         tableRelativeOffset,
                         donorTrueIndex,
                         appendTrueIndex,
                         label,
                         recordBytes,
                         patches,
                         writtenWadOffsets,
                         skippedEdits,
                         out specialDataDescription))
            {
                return false;
            }
        }

        AddPatch(
            stream,
            layout,
            level,
            tableWadOffset,
            appendTrueIndex,
            0,
            recordBytes,
            "moby-record-append",
            label,
            crossLevelDonor == null
                ? $"Append {label} as source record T{appendTrueIndex} cloned from same-level donor T{donorTrueIndex}.{specialDataDescription}{placementSectorDescription}{donorUpgradeDescription}{sourceStateDescription}"
                : string.Equals(crossLevelDonor.RecipeMode, CrossLevelSourceRecordCandidateRecipeMode, StringComparison.OrdinalIgnoreCase)
                ? $"Append {label} as guarded cross-level source-record candidate T{appendTrueIndex} cloned from {crossLevelDonor.SourceLevel.DisplayName} donor T{donorTrueIndex}.{specialDataDescription}{placementSectorDescription}"
                : $"Append {label} as source record T{appendTrueIndex} cloned from {crossLevelDonor.SourceLevel.DisplayName} donor T{donorTrueIndex}.{specialDataDescription}{placementSectorDescription}",
            patches,
            writtenWadOffsets);
        return true;
    }

    private static bool TryAppendLifeChestPrivateSpecialData(
        FileStream stream,
        DiscLayout layout,
        LevelDefinition level,
        long tableWadOffset,
        int donorTrueIndex,
        int appendTrueIndex,
        string label,
        byte[] recordBytes,
        List<MobySourcePatch> patches,
        HashSet<long> writtenWadOffsets,
        List<string> skippedEdits,
        out string description)
    {
        description = "";
        uint donorSpecialDataOffset = BitConverter.ToUInt32(recordBytes, 0);
        LevelSceneSourceLayout scene = ReadLevelSceneSourceLayout(stream, layout, level, tableWadOffset);
        if (!IsSourceSpecialDataOffset(scene.ByteLength, donorSpecialDataOffset))
        {
            skippedEdits.Add($"{label}: Life Chest donor T{donorTrueIndex} has no valid level-scene special-data offset.");
            return false;
        }

        SpecialDataAllocator allocator = BuildSpecialDataAllocatorCore(
            stream,
            layout,
            tableWadOffset,
            level.SourceRecordCount,
            scene.WadBaseOffset,
            scene.ByteLength);
        int specialDataLength = GetSpecialDataLength(allocator, donorSpecialDataOffset, recordBytes);
        uint appendSpecialDataOffset = ReserveSpecialDataOffset(stream, layout, allocator, specialDataLength, writtenWadOffsets);
        long donorSpecialWadOffset = scene.WadBaseOffset + donorSpecialDataOffset;
        long appendSpecialWadOffset = scene.WadBaseOffset + appendSpecialDataOffset;
        byte[] specialBytes = ReadWadBytes(stream, layout, donorSpecialWadOffset, specialDataLength);

        AddRawPatch(
            stream,
            layout,
            level,
            appendSpecialWadOffset,
            specialBytes,
            "moby-special-data-clone",
            label,
            appendTrueIndex,
            "special",
            $"Clone same-level Life Chest donor T{donorTrueIndex} scene data to source offset 0x{appendSpecialDataOffset:X} for appended T{appendTrueIndex}.",
            patches,
            writtenWadOffsets);
        WriteInt32(recordBytes, 0, (int)appendSpecialDataOffset);
        description = $" Cloned same-level Life Chest scene data to source offset 0x{appendSpecialDataOffset:X}.";
        return true;
    }

    private static GeometryCandidate? TryLoadLevelGeometry(string workspaceRoot, string levelKey)
    {
        if (string.IsNullOrWhiteSpace(workspaceRoot) || string.IsNullOrWhiteSpace(levelKey))
            return null;

        string[] candidates =
        [
            Path.Combine(workspaceRoot, "editor-cache", $"{levelKey}-runtime-scene-editor-overlay.json"),
            Path.Combine(workspaceRoot, "_local", "terrain", "source-derived-overlays", $"{levelKey}-runtime-scene-editor-overlay.json"),
            Path.Combine(workspaceRoot, $"{levelKey}-runtime-scene-editor-overlay.json")
        ];

        foreach (string path in candidates)
        {
            if (!File.Exists(path))
                continue;

            try
            {
                return GeometryOverlayLoader.LoadFirstCandidate(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or InvalidDataException)
            {
                return null;
            }
        }

        return null;
    }

    private static bool ShouldApplyPlacementSector(byte[] recordBytes)
    {
        if (recordBytes.Length <= 0x53)
            return false;

        return IsLooseVisibleGemRecord(recordBytes) ||
            IsKnownSameLevelLightweightAppend(recordBytes[TypeOffset], recordBytes[0x36], recordBytes[0x37], recordBytes[0x52], recordBytes[0x53]);
    }

    private static bool ShouldApplyMovedExistingPlacementSector(byte[] recordBytes)
    {
        return recordBytes.Length > 0x53 && recordBytes[TypeOffset] is 0x18 or 0x20;
    }

    private static void AddMovedExistingPlacementSectorPatch(
        FileStream stream,
        DiscLayout layout,
        LevelDefinition level,
        GeometryCandidate? levelGeometry,
        long tableWadOffset,
        int trueIndex,
        string label,
        JsonElement edit,
        List<MobySourcePatch> patches,
        HashSet<long> writtenWadOffsets)
    {
        if (!HasPositionEdit(edit) || HasSourceByteEdit(edit, 0x4A))
            return;

        byte[] recordBytes = ReadWadBytes(stream, layout, tableWadOffset + ((long)trueIndex * RecordStride), RecordStride);
        if (!ShouldApplyMovedExistingPlacementSector(recordBytes))
            return;

        byte[] updatedRecord = recordBytes.ToArray();
        if (!TryApplySourceRecordPlacementSector(levelGeometry, edit, updatedRecord, allowPlacementSector: true, out int sectorIndex))
            return;
        if (updatedRecord[0x4A] == recordBytes[0x4A])
            return;

        AddPatch(
            stream,
            layout,
            level,
            tableWadOffset,
            trueIndex,
            0x4A,
            [updatedRecord[0x4A]],
            "moby-placement-sector",
            label,
            $"Set {label} placement/culling sector byte to 0x{sectorIndex:X2} for its edited position.",
            patches,
            writtenWadOffsets);
    }

    private static bool TryApplySourceRecordPlacementSector(GeometryCandidate? geometry, JsonElement edit, byte[] recordBytes, bool allowPlacementSector, out int sectorIndex)
    {
        sectorIndex = -1;
        if (!allowPlacementSector)
            return false;
        if (geometry == null || geometry.Polygons.Count == 0 || recordBytes.Length <= 0x4A)
            return false;
        if (HasSourceByteEdit(edit, 0x4A))
            return false;
        if (!TryReadRawPosition(edit, out int rawX, out int rawY, out int rawZ))
            return false;

        float x = rawX / 16f;
        float y = rawY / 16f;
        float referenceZ = rawZ / 16f;
        TerrainSectorCandidate? best = null;
        foreach (TerrainPolygon polygon in geometry.Polygons)
        {
            if (polygon.IsTerrainRemoved || polygon.SectorIndex is < 0 or > 255)
                continue;
            if (!polygon.TryGetZ(x, y, out float terrainZ))
                continue;

            TerrainSectorCandidate candidate = new(polygon.SectorIndex, terrainZ, Math.Abs(terrainZ - referenceZ));
            if (best == null || IsBetterPlacementSector(candidate, best.Value))
                best = candidate;
        }

        if (best == null)
            return false;

        sectorIndex = best.Value.SectorIndex;
        recordBytes[0x4A] = (byte)sectorIndex;
        return true;
    }

    private static bool IsBetterPlacementSector(TerrainSectorCandidate candidate, TerrainSectorCandidate best)
    {
        return candidate.DistanceToReference < best.DistanceToReference - 0.001f ||
            Math.Abs(candidate.DistanceToReference - best.DistanceToReference) <= 0.001f && candidate.Z > best.Z;
    }

    private readonly record struct TerrainSectorCandidate(int SectorIndex, float Z, float DistanceToReference);

    private static bool HasSourceByteEdit(JsonElement edit, int offset)
    {
        if (!edit.TryGetProperty("sourceByteEdits", out JsonElement sourceByteEdits) || sourceByteEdits.ValueKind != JsonValueKind.Array)
            return false;

        foreach (JsonElement byteEdit in sourceByteEdits.EnumerateArray())
        {
            int editedOffset = JsonValue.GetInt32(byteEdit, "offset", JsonValue.GetInt32(byteEdit, "offsetHex", -1));
            if (editedOffset == offset)
                return true;
        }

        return false;
    }

    private static bool HasExplicitSourceByteEdit(JsonElement edit, int offset)
    {
        if (!edit.TryGetProperty("sourceByteEdits", out JsonElement sourceByteEdits) || sourceByteEdits.ValueKind != JsonValueKind.Array)
            return false;

        foreach (JsonElement byteEdit in sourceByteEdits.EnumerateArray())
        {
            int editedOffset = JsonValue.GetInt32(byteEdit, "offset", JsonValue.GetInt32(byteEdit, "offsetHex", -1));
            if (editedOffset != offset)
                continue;

            string field = JsonValue.GetString(byteEdit, "field");
            if (!string.Equals(field, "flag4A-identity-byte", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool IsLooseVisibleGemRecord(byte[] recordBytes)
    {
        return recordBytes.Length > 0x53 &&
            IsLooseVisibleGemIdentity(recordBytes[TypeOffset], recordBytes[0x36], recordBytes[0x37], recordBytes[0x4F], recordBytes[0x52], recordBytes[0x53]);
    }

    private static bool IsLooseVisibleGemIdentity(int type, int sourceByte36, int sourceByte37, int sourceByte4F, int flag4A, int flag4B)
    {
        return LooksLikeLooseVisibleGemIdentity(type, sourceByte36, sourceByte37, flag4A) &&
            GemValue.TryFromEncoding(sourceByte36, sourceByte4F, out _) &&
            (flag4B == 0xFF || GemIdByteValue(flag4B) > 0);
    }

    private static bool LooksLikeLooseVisibleGemIdentity(int type, int sourceByte36, int sourceByte37, int flag4A)
    {
        return type == 0x18 &&
            sourceByte37 == 0x00 &&
            flag4A == 0x40 &&
            GemIdByteValue(sourceByte36) > 0;
    }

    private static void NormalizeLooseVisibleGemRecord(byte[] recordBytes)
    {
        if (recordBytes.Length <= 0x53 || recordBytes[TypeOffset] != 0x18)
            return;
        if (!GemValue.TryFromIdByte(recordBytes[0x36], out GemValue gem))
            return;

        recordBytes[0x37] = 0x00;
        recordBytes[0x4F] = (byte)gem.ValueByte;
        recordBytes[0x52] = 0x40;
        recordBytes[0x53] = 0xFF;
    }

    private static bool IsKnownSameLevelLightweightAppend(int type, int sourceByte36, int sourceByte37, int flag4A, int flag4B)
    {
        if (sourceByte37 != 0x00)
            return false;

        bool isNativeKey = sourceByte36 == 0xAD &&
            flag4B == 0xFF &&
            (type == 0x18 && flag4A == 0x40 ||
                type == 0x00 && flag4A == 0x00);
        bool isNativeKeyChest = type == 0x20 &&
            sourceByte36 == 0xAE &&
            flag4A == 0x10 &&
            GemIdByteValue(flag4B) > 0;

        return isNativeKey || isNativeKeyChest;
    }

    private static bool IsPortableSpringChestControllerAppend(JsonElement edit, int type, int sourceByte36, int sourceByte37)
    {
        if (type != 0x20 || sourceByte36 != 0xC2 || sourceByte37 != 0x00 ||
            !TryGetCrossLevelTemplate(edit, out JsonElement template))
        {
            return false;
        }

        string family = JsonValue.GetString(template, "family", InferCrossLevelFamily(JsonValue.GetString(template, "id")));
        string templateId = JsonValue.GetString(template, "id");
        string requiredFeature = JsonValue.GetString(template, "requiredExporterFeature");
        return string.Equals(family, "springChest", StringComparison.OrdinalIgnoreCase) &&
            templateId.Contains("spring_chest_controller", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(requiredFeature, "DirectSourceRecordAppend", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsNativeLifeChestIdentity(
        int type,
        int sourceByte36,
        int sourceByte37,
        int sourceByte4F,
        int flag4A,
        int flag4B)
    {
        return type == 0x20 &&
            sourceByte36 == 0xA5 &&
            sourceByte37 == 0x01 &&
            sourceByte4F == 0x00 &&
            flag4A == 0x10 &&
            flag4B == 0x0E;
    }

    private static bool TryGetPrivateLifeChestRuntimeBase(string levelKey, JsonElement edit, byte[] recordBytes, out uint runtimeBase)
    {
        runtimeBase = 0;
        if (recordBytes.Length <= 0x53 ||
            !IsNativeLifeChestIdentity(
                recordBytes[TypeOffset],
                recordBytes[0x36],
                recordBytes[0x37],
                recordBytes[0x4F],
                recordBytes[0x52],
                recordBytes[0x53]))
        {
            return false;
        }

        string text = JsonValue.GetString(edit, "lifeChestPrivateRuntimeBase").Trim();
        if (!string.IsNullOrWhiteSpace(text))
        {
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return uint.TryParse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out runtimeBase);
            return uint.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out runtimeBase);
        }

        return TryGetPromotedLifeChestPrivateRuntimeBase(levelKey, out runtimeBase);
    }

    private static bool TryGetPromotedLifeChestPrivateRuntimeBase(string levelKey, out uint runtimeBase)
    {
        return LifeChestRuntimeLayout.TryGetRuntimeBase(levelKey, out runtimeBase);
    }

    private static bool IsPromotedSameLevelNativeCloneAppendIdentity(
        string levelKey,
        int type,
        int sourceByte36,
        int sourceByte37,
        int sourceByte4F,
        int flag4A,
        int flag4B)
    {
        if (IsNativeLifeChestIdentity(type, sourceByte36, sourceByte37, sourceByte4F, flag4A, flag4B))
        {
            return TryGetPromotedLifeChestPrivateRuntimeBase(levelKey, out _);
        }

        if (type != 0x20 ||
            sourceByte4F != 0x00 ||
            flag4A != 0x10 ||
            flag4B != 0x54)
        {
            return false;
        }

        string normalizedLevelKey = LevelCatalog.NormalizeKey(levelKey);
        if (normalizedLevelKey.Equals("townsquare", StringComparison.OrdinalIgnoreCase))
        {
            // User live-test on 2026-07-05: true-appended Town Square Bulls behave.
            // Torro Gnorcs, 0xC2 flame/charge chests, and 0xC3 charge chests remain guarded.
            return sourceByte36 == 0x17 && sourceByte37 == 0x00;
        }

        if (sourceByte37 != 0x00)
            return false;

        // Live Dark Hollow/Artisans testing proved these same-level true-appends in those levels only.
        // Small/Regular Gnorc and Town Square chest families are intentionally not included yet.
        bool darkHollowOrArtisans = normalizedLevelKey.Equals("darkhollow", StringComparison.OrdinalIgnoreCase) ||
            normalizedLevelKey.Equals("artisans", StringComparison.OrdinalIgnoreCase);
        return darkHollowOrArtisans && (sourceByte36 == 0x73 || sourceByte36 == 0xC2);
    }

    private static bool TryGetReleaseLimitedNativeCloneAppendFamily(
        JsonElement edit,
        LevelDefinition level,
        out string familyKey,
        out string familyName)
    {
        familyKey = "";
        familyName = "this enemy/chest family";
        if (TryGetCrossLevelTemplate(edit, out _))
            return false;

        int type = JsonValue.GetInt32(edit, "typeEditedHex", JsonValue.GetInt32(edit, "typeHex", -1));
        int sourceByte36 = JsonValue.GetInt32(edit, "sourceByte36EditedHex", JsonValue.GetInt32(edit, "sourceByte36Hex", -1));
        int sourceByte37 = JsonValue.GetInt32(edit, "sourceByte37EditedHex", JsonValue.GetInt32(edit, "sourceByte37Hex", -1));
        int sourceByte4F = JsonValue.GetInt32(edit, "sourceByte4FEditedHex", JsonValue.GetInt32(edit, "sourceByte4FHex", -1));
        int flag4A = JsonValue.GetInt32(edit, "flag4AEditedHex", JsonValue.GetInt32(edit, "flag4AHex", -1));
        int flag4B = JsonValue.GetInt32(edit, "flag4BEditedHex", JsonValue.GetInt32(edit, "flag4BHex", -1));
        if (IsNativeLifeChestIdentity(type, sourceByte36, sourceByte37, sourceByte4F, flag4A, flag4B) ||
            !IsPromotedSameLevelNativeCloneAppendIdentity(level.Key, type, sourceByte36, sourceByte37, sourceByte4F, flag4A, flag4B))
        {
            return false;
        }

        string normalizedLevelKey = LevelCatalog.NormalizeKey(level.Key);
        familyKey = $"{normalizedLevelKey}:{type:X2}:{sourceByte36:X2}:{sourceByte37:X2}:{sourceByte4F:X2}:{flag4A:X2}:{flag4B:X2}";
        familyName = sourceByte36 switch
        {
            0x17 => "Bull",
            0x73 => "Large Gnorc",
            0xC2 => "Flame/Charge Chest",
            _ => $"type 0x{type:X2} / family 0x{sourceByte36:X2}"
        };
        return true;
    }

    private static bool IsProvenNativeCloneAppend(JsonElement edit)
    {
        return string.Equals(JsonValue.GetString(edit, "patchStatus"), "native-clone", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetSameLevelAppendDonor(JsonElement edit, LevelDefinition level, out int sourceTrueIndex)
    {
        sourceTrueIndex = -1;
        if (!edit.TryGetProperty("recordMutation", out JsonElement mutation) || mutation.ValueKind != JsonValueKind.Object)
            return false;

        string mode = JsonValue.GetString(mutation, "mode");
        if (!string.Equals(mode, "appendSourceRecordClone", StringComparison.OrdinalIgnoreCase))
            return false;

        string sourceLevelKey = JsonValue.GetString(mutation, "sourceLevelKey");
        if (!string.IsNullOrWhiteSpace(sourceLevelKey) &&
            !string.Equals(LevelCatalog.NormalizeKey(sourceLevelKey), LevelCatalog.NormalizeKey(level.Key), StringComparison.OrdinalIgnoreCase))
            return false;

        int candidate = JsonValue.GetInt32(mutation, "sourceTrueIndex", -1);
        if (candidate < 0 || candidate >= level.SourceRecordCount)
            return false;

        sourceTrueIndex = candidate;
        return true;
    }

    private static bool IsGuardedNativeCloneAppend(JsonElement edit)
    {
        int targetType = JsonValue.GetInt32(edit, "typeEditedHex", JsonValue.GetInt32(edit, "typeHex", -1));
        int targetSourceByte36 = JsonValue.GetInt32(edit, "sourceByte36EditedHex", JsonValue.GetInt32(edit, "sourceByte36Hex", -1));
        int targetSourceByte37 = JsonValue.GetInt32(edit, "sourceByte37EditedHex", JsonValue.GetInt32(edit, "sourceByte37Hex", -1));
        int targetSourceByte4F = JsonValue.GetInt32(edit, "sourceByte4FEditedHex", JsonValue.GetInt32(edit, "sourceByte4FHex", -1));
        int targetFlag4A = JsonValue.GetInt32(edit, "flag4AEditedHex", JsonValue.GetInt32(edit, "flag4AHex", -1));
        int targetFlag4B = JsonValue.GetInt32(edit, "flag4BEditedHex", JsonValue.GetInt32(edit, "flag4BHex", -1));
        return IsGuardedNativeCloneAppend(edit, targetType, targetSourceByte36, targetSourceByte37, targetSourceByte4F, targetFlag4A, targetFlag4B);
    }

    private static bool IsGuardedNativeCloneAppend(
        JsonElement edit,
        int targetType,
        int targetSourceByte36,
        int targetSourceByte37,
        int targetSourceByte4F,
        int targetFlag4A,
        int targetFlag4B)
    {
        if (!IsProvenNativeCloneAppend(edit))
            return false;
        if (string.Equals(JsonValue.GetString(edit, "confidence"), "native-clone-proof", StringComparison.OrdinalIgnoreCase))
            return false;
        if (JsonValue.GetString(edit, "patchLead").Contains("no actor-package import expected", StringComparison.OrdinalIgnoreCase))
            return false;
        if (IsContainedGemAppend(edit, targetType))
            return false;
        if (IsLooseVisibleGemIdentity(targetType, targetSourceByte36, targetSourceByte37, targetSourceByte4F, targetFlag4A, targetFlag4B))
            return false;
        if (IsKnownSameLevelLightweightAppend(targetType, targetSourceByte36, targetSourceByte37, targetFlag4A, targetFlag4B))
            return false;
        return true;
    }

    private static bool TryFindNearestLooseVisibleGemDonor(
        FileStream stream,
        DiscLayout layout,
        long tableWadOffset,
        long tableRelativeOffset,
        int sourceRecordCount,
        JsonElement edit,
        int preferredGemIdByte,
        out int donorTrueIndex)
    {
        donorTrueIndex = -1;
        if (!TryReadRawPosition(edit, out int targetX, out int targetY, out int targetZ))
            return false;

        int nearestAnyTrueIndex = -1;
        long nearestAnyDistance = long.MaxValue;
        int nearestPreferredTrueIndex = -1;
        long nearestPreferredDistance = long.MaxValue;

        for (int trueIndex = 0; trueIndex < sourceRecordCount; trueIndex++)
        {
            byte[] record = ReadWadBytes(stream, layout, tableWadOffset + (trueIndex * RecordStride), RecordStride);
            if (!IsLooseVisibleGemRecord(record) || !TryHasSameLevelSourceSpecialData(tableRelativeOffset, record))
                continue;

            long distance = SquaredDistance(
                targetX,
                targetY,
                targetZ,
                BitConverter.ToInt32(record, XOffset),
                BitConverter.ToInt32(record, YOffset),
                BitConverter.ToInt32(record, ZOffset));
            if (distance < nearestAnyDistance)
            {
                nearestAnyDistance = distance;
                nearestAnyTrueIndex = trueIndex;
            }

            if (record[0x36] == (byte)preferredGemIdByte && distance < nearestPreferredDistance)
            {
                nearestPreferredDistance = distance;
                nearestPreferredTrueIndex = trueIndex;
            }
        }

        if (nearestAnyTrueIndex < 0)
            return false;

        if (nearestPreferredTrueIndex >= 0 && nearestPreferredDistance <= nearestAnyDistance * 4)
        {
            donorTrueIndex = nearestPreferredTrueIndex;
            return true;
        }

        donorTrueIndex = nearestAnyTrueIndex;
        return true;
    }

    private static bool TryReadRawPosition(JsonElement edit, out int x, out int y, out int z)
    {
        x = 0;
        y = 0;
        z = 0;
        return TryReadRawAxis(edit, "x", out x) &&
            TryReadRawAxis(edit, "y", out y) &&
            TryReadRawAxis(edit, "z", out z);
    }

    private static bool TryReadRawAxis(JsonElement edit, string axis, out int value)
    {
        value = 0;
        if (edit.TryGetProperty("rawEdited", out JsonElement rawEdited)
            && rawEdited.ValueKind == JsonValueKind.Object
            && rawEdited.TryGetProperty(axis, out _))
        {
            value = JsonValue.GetInt32(rawEdited, axis);
            return true;
        }

        if (edit.TryGetProperty("edited", out JsonElement edited)
            && edited.ValueKind == JsonValueKind.Object
            && edited.TryGetProperty(axis, out _))
        {
            value = (int)Math.Round(JsonValue.GetSingle(edited, axis) * 16f);
            return true;
        }

        return false;
    }

    private static bool HasPositionEdit(JsonElement edit)
    {
        if (!edit.TryGetProperty("rawEdited", out JsonElement rawEdited) || rawEdited.ValueKind != JsonValueKind.Object)
            return false;
        if (!edit.TryGetProperty("rawOriginal", out JsonElement rawOriginal) || rawOriginal.ValueKind != JsonValueKind.Object)
            return rawEdited.TryGetProperty("x", out _) ||
                rawEdited.TryGetProperty("y", out _) ||
                rawEdited.TryGetProperty("z", out _);

        return HasRawAxisEdit(rawEdited, rawOriginal, "x") ||
            HasRawAxisEdit(rawEdited, rawOriginal, "y") ||
            HasRawAxisEdit(rawEdited, rawOriginal, "z");
    }

    private static bool HasRawAxisEdit(JsonElement rawEdited, JsonElement rawOriginal, string axis)
    {
        if (!rawEdited.TryGetProperty(axis, out _))
            return false;

        int edited = JsonValue.GetInt32(rawEdited, axis);
        int original = JsonValue.GetInt32(rawOriginal, axis, edited);
        return edited != original;
    }

    private static long SquaredDistance(int ax, int ay, int az, int bx, int by, int bz)
    {
        long dx = (long)ax - bx;
        long dy = (long)ay - by;
        long dz = (long)az - bz;
        return (dx * dx) + (dy * dy) + (dz * dz);
    }

    private static bool IsSpringChestControllerAliasAppend(JsonElement edit, IReadOnlyList<MobyActorPackageImportPreview> packageImportPreviews)
    {
        if (!TryGetCrossLevelTemplate(edit, out JsonElement crossLevelTemplate))
            return false;

        string templateId = JsonValue.GetString(crossLevelTemplate, "id");
        int sourceTrueIndex = JsonValue.GetInt32(crossLevelTemplate, "sourceTrueIndex", -1);
        if (!templateId.Contains("spring_chest_controller", StringComparison.OrdinalIgnoreCase) &&
            !templateId.Contains("spring_chest_companion", StringComparison.OrdinalIgnoreCase) &&
            sourceTrueIndex is not (30 or 113))
            return false;

        return UsesSpringChestControllerAlias(packageImportPreviews);
    }

    private static bool UsesSpringChestControllerAlias(IReadOnlyList<MobyActorPackageImportPreview> packageImportPreviews)
    {
        return packageImportPreviews.Any(preview =>
            string.Equals(preview.Family, "springChest", StringComparison.OrdinalIgnoreCase) &&
            preview.RecipeMode.Contains("Alias01FE", StringComparison.OrdinalIgnoreCase));
    }

    private static int ReadActorIdFromEdit(JsonElement edit, int fallbackActorId)
    {
        int low = JsonValue.GetInt32(edit, "sourceByte36EditedHex", JsonValue.GetInt32(edit, "sourceByte36Hex", fallbackActorId & 0xFF));
        int high = JsonValue.GetInt32(edit, "sourceByte37EditedHex", JsonValue.GetInt32(edit, "sourceByte37Hex", (fallbackActorId >> 8) & 0xFF));
        if (low is < 0 or > 0xFF || high is < 0 or > 0xFF)
            return fallbackActorId;
        return low | (high << 8);
    }

    private static bool TryHasSameLevelSourceSpecialData(long tableRelativeOffset, byte[] recordBytes)
    {
        uint sourceSpecialDataOffset = BitConverter.ToUInt32(recordBytes, 0);
        return IsSourceSpecialDataOffset(tableRelativeOffset, sourceSpecialDataOffset);
    }

    private static bool TryGetCrossLevelTemplate(JsonElement edit, out JsonElement template)
    {
        return edit.TryGetProperty("crossLevelTemplate", out template) && template.ValueKind == JsonValueKind.Object;
    }

    private static bool TryAppendCrossLevelSpecialData(
        FileStream stream,
        DiscLayout layout,
        LevelDefinition targetLevel,
        long targetTableWadOffset,
        long targetTableRelativeOffset,
        CrossLevelAppendDonor donor,
        int appendTrueIndex,
        string label,
        byte[] recordBytes,
        List<MobySourcePatch> patches,
        HashSet<long> writtenWadOffsets,
        Dictionary<string, CrossLevelSharedSpecialCluster> sharedCrossLevelSpecialClusters,
        List<string> skippedEdits,
        out string description)
    {
        description = "";
        uint sourceSpecialDataOffset = BitConverter.ToUInt32(recordBytes, 0);
        if (sourceSpecialDataOffset == 0)
            return true;

        if (targetTableRelativeOffset <= 0 || donor.SourceTableRelativeOffset <= 0)
        {
            skippedEdits.Add($"{label}: cross-level source-record append needs mapped source and target table-relative offsets for special data.");
            return false;
        }

        if (!IsSourceSpecialDataOffset(donor.SourceTableRelativeOffset, sourceSpecialDataOffset))
        {
            skippedEdits.Add($"{label}: donor {donor.SourceLevel.DisplayName} T{donor.SourceTrueIndex} has a special-data offset that is not safe to copy.");
            return false;
        }

        SpecialDataAllocator sourceAllocator = BuildSpecialDataAllocator(stream, layout, donor.SourceTableWadOffset, donor.SourceTableRelativeOffset, donor.SourceLevel.SourceRecordCount);
        SpecialDataAllocator targetAllocator = BuildSpecialDataAllocator(stream, layout, targetTableWadOffset, targetTableRelativeOffset, targetLevel.SourceRecordCount);
        if (ShouldSharePeaceKeepersSpringChestCluster(donor, recordBytes))
        {
            if (!TryAppendSharedPeaceKeepersSpringChestCluster(
                stream,
                layout,
                targetLevel,
                sourceAllocator,
                targetAllocator,
                donor,
                sourceSpecialDataOffset,
                appendTrueIndex,
                label,
                recordBytes,
                patches,
                writtenWadOffsets,
                sharedCrossLevelSpecialClusters,
                skippedEdits,
                out description))
            {
                return false;
            }

            return true;
        }

        int length = GetCrossLevelSpecialDataLength(sourceAllocator, donor, sourceSpecialDataOffset, recordBytes, out string lengthNote);
        uint targetSpecialDataOffset = ReserveSpecialDataOffset(stream, layout, targetAllocator, length, writtenWadOffsets);
        long sourceSpecialWadOffset = sourceAllocator.WadBaseOffset + sourceSpecialDataOffset;
        long targetSpecialWadOffset = targetAllocator.WadBaseOffset + targetSpecialDataOffset;
        byte[] specialBytes = ReadWadBytes(stream, layout, sourceSpecialWadOffset, length);

        AddRawPatch(
            stream,
            layout,
            targetLevel,
            targetSpecialWadOffset,
            specialBytes,
            "cross-level-moby-special-data-import",
            label,
            appendTrueIndex,
            "special",
            string.IsNullOrWhiteSpace(lengthNote)
                ? $"Copy {donor.SourceLevel.DisplayName} donor T{donor.SourceTrueIndex} special data to source offset 0x{targetSpecialDataOffset:X} for appended T{appendTrueIndex}."
                : $"Copy {donor.SourceLevel.DisplayName} donor T{donor.SourceTrueIndex} extended special data ({length} bytes; {lengthNote}) to source offset 0x{targetSpecialDataOffset:X} for appended T{appendTrueIndex}.",
            patches,
            writtenWadOffsets);

        WriteInt32(recordBytes, 0, (int)targetSpecialDataOffset);
        description = string.IsNullOrWhiteSpace(lengthNote)
            ? $" Copied {donor.SourceLevel.DisplayName} special data to source offset 0x{targetSpecialDataOffset:X}."
            : $" Copied {donor.SourceLevel.DisplayName} extended special data ({length} bytes; {lengthNote}) to source offset 0x{targetSpecialDataOffset:X}.";
        return true;
    }

    private static bool TryAppendSharedPeaceKeepersSpringChestCluster(
        FileStream stream,
        DiscLayout layout,
        LevelDefinition targetLevel,
        SpecialDataAllocator sourceAllocator,
        SpecialDataAllocator targetAllocator,
        CrossLevelAppendDonor donor,
        uint sourceSpecialDataOffset,
        int appendTrueIndex,
        string label,
        byte[] recordBytes,
        List<MobySourcePatch> patches,
        HashSet<long> writtenWadOffsets,
        Dictionary<string, CrossLevelSharedSpecialCluster> sharedCrossLevelSpecialClusters,
        List<string> skippedEdits,
        out string description)
    {
        description = "";
        if (!TryGetPeaceKeepersSpringClusterRelativeOffset(donor.SourceTrueIndex, out uint sourceRecordRelativeOffset))
        {
            skippedEdits.Add($"{label}: shared Peace Keepers spring cluster does not support donor T{donor.SourceTrueIndex}.");
            return false;
        }

        if (sourceSpecialDataOffset < sourceRecordRelativeOffset)
        {
            skippedEdits.Add($"{label}: Peace Keepers T{donor.SourceTrueIndex} spring special-data offset is too small to preserve the native cluster relationship.");
            return false;
        }

        uint sourceClusterBaseOffset = sourceSpecialDataOffset - sourceRecordRelativeOffset;
        if (!IsSourceSpecialDataOffset(donor.SourceTableRelativeOffset, sourceClusterBaseOffset))
        {
            skippedEdits.Add($"{label}: donor {donor.SourceLevel.DisplayName} T{donor.SourceTrueIndex} shared spring cluster base is not safe to copy.");
            return false;
        }

        string key = $"{LevelCatalog.NormalizeKey(targetLevel.Key)}|{LevelCatalog.NormalizeKey(donor.SourceLevel.Key)}|peacekeepers-native-spring-cluster|{sourceClusterBaseOffset:X8}";
        if (!sharedCrossLevelSpecialClusters.TryGetValue(key, out CrossLevelSharedSpecialCluster? sharedCluster))
        {
            uint remaining = sourceAllocator.TableRelativeOffset > sourceClusterBaseOffset
                ? sourceAllocator.TableRelativeOffset - sourceClusterBaseOffset
                : 0;
            int length = (int)Math.Min(remaining, 0x800);
            if (length <= sourceRecordRelativeOffset)
            {
                skippedEdits.Add($"{label}: shared Peace Keepers spring cluster has no room for the donor record's relative special-data offset.");
                return false;
            }

            uint targetClusterBaseOffset = ReserveSpecialDataOffset(stream, layout, targetAllocator, length, writtenWadOffsets);
            long sourceSpecialWadOffset = sourceAllocator.WadBaseOffset + sourceClusterBaseOffset;
            long targetSpecialWadOffset = targetAllocator.WadBaseOffset + targetClusterBaseOffset;
            byte[] specialBytes = ReadWadBytes(stream, layout, sourceSpecialWadOffset, length);

            AddRawPatch(
                stream,
                layout,
                targetLevel,
                targetSpecialWadOffset,
                specialBytes,
                "cross-level-moby-special-data-import",
                label,
                appendTrueIndex,
                "special",
                $"Copy Peace Keepers shared spring-data cluster ({length} bytes; native spring/context/reward rows keep their cluster-relative offsets) to source offset 0x{targetClusterBaseOffset:X} for appended T{appendTrueIndex}.",
                patches,
                writtenWadOffsets);

            sharedCluster = new CrossLevelSharedSpecialCluster(sourceClusterBaseOffset, targetClusterBaseOffset, length);
            sharedCrossLevelSpecialClusters[key] = sharedCluster;
        }

        uint targetSpecialDataOffset = sharedCluster.TargetBaseOffset + sourceRecordRelativeOffset;
        if (sourceRecordRelativeOffset >= sharedCluster.Length)
        {
            skippedEdits.Add($"{label}: shared Peace Keepers spring cluster copy is too short for the donor record's relative special-data offset.");
            return false;
        }

        WriteInt32(recordBytes, 0, (int)targetSpecialDataOffset);
        description = sourceRecordRelativeOffset == 0
            ? $" Copied Peace Keepers shared spring-data cluster to source offset 0x{sharedCluster.TargetBaseOffset:X} and pointed this record at its base."
            : $" Reused Peace Keepers shared spring-data cluster at source offset 0x{targetSpecialDataOffset:X} (+0x{sourceRecordRelativeOffset:X} from the copied base).";
        return true;
    }

    private static bool TryPatchExistingCrossLevelSpecialData(
        FileStream stream,
        DiscLayout layout,
        LevelCatalog catalog,
        LevelDefinition targetLevel,
        long targetTableWadOffset,
        long targetTableRelativeOffset,
        int targetTrueIndex,
        string label,
        JsonElement crossLevelTemplate,
        List<MobySourcePatch> patches,
        HashSet<long> writtenWadOffsets,
        List<string> skippedEdits)
    {
        int sourceTrueIndex = JsonValue.GetInt32(crossLevelTemplate, "sourceTrueIndex", -1);
        LevelDefinition? sourceLevel = catalog.FindByKey(JsonValue.GetString(crossLevelTemplate, "sourceLevelKey"));
        if (sourceLevel == null || !sourceLevel.HasSourceTable || sourceTrueIndex < 0 || sourceTrueIndex >= sourceLevel.SourceRecordCount)
            return true;

        long sourceTableWadOffset = ParseRequiredLong(sourceLevel.SourceTableWadOffset, "sourceLevel.sourceTableWadOffset");
        long sourceTableRelativeOffset = string.IsNullOrWhiteSpace(sourceLevel.SourceTableRelativeOffset)
            ? 0
            : ParseRequiredLong(sourceLevel.SourceTableRelativeOffset, "sourceLevel.sourceTableRelativeOffset");
        byte[] donorRecordBytes = ReadWadBytes(stream, layout, sourceTableWadOffset + ((long)sourceTrueIndex * RecordStride), RecordStride);
        uint sourceSpecialDataOffset = BitConverter.ToUInt32(donorRecordBytes, 0);
        if (sourceSpecialDataOffset == 0)
            return true;

        if (targetTableRelativeOffset <= 0 || sourceTableRelativeOffset <= 0)
        {
            skippedEdits.Add($"{label}: cross-level transform needs mapped source and target table-relative offsets for special data.");
            return false;
        }

        if (!IsSourceSpecialDataOffset(sourceTableRelativeOffset, sourceSpecialDataOffset))
        {
            skippedEdits.Add($"{label}: donor {sourceLevel.DisplayName} T{sourceTrueIndex} has a special-data offset that is not safe to copy.");
            return false;
        }

        SpecialDataAllocator sourceAllocator = BuildSpecialDataAllocator(stream, layout, sourceTableWadOffset, sourceTableRelativeOffset, sourceLevel.SourceRecordCount);
        SpecialDataAllocator targetAllocator = BuildSpecialDataAllocator(stream, layout, targetTableWadOffset, targetTableRelativeOffset, targetLevel.SourceRecordCount);
        int length = GetSpecialDataLength(sourceAllocator, sourceSpecialDataOffset, donorRecordBytes);
        uint targetSpecialDataOffset = ReserveSpecialDataOffset(stream, layout, targetAllocator, length, writtenWadOffsets);
        long sourceSpecialWadOffset = sourceAllocator.WadBaseOffset + sourceSpecialDataOffset;
        long targetSpecialWadOffset = targetAllocator.WadBaseOffset + targetSpecialDataOffset;
        byte[] specialBytes = ReadWadBytes(stream, layout, sourceSpecialWadOffset, length);

        AddRawPatch(
            stream,
            layout,
            targetLevel,
            targetSpecialWadOffset,
            specialBytes,
            "cross-level-existing-moby-special-data-import",
            label,
            targetTrueIndex,
            "special",
            $"Copy {sourceLevel.DisplayName} donor T{sourceTrueIndex} special data to source offset 0x{targetSpecialDataOffset:X} for transformed T{targetTrueIndex}.",
            patches,
            writtenWadOffsets);

        AddRawPatch(
            stream,
            layout,
            targetLevel,
            targetTableWadOffset + ((long)targetTrueIndex * RecordStride),
            BitConverter.GetBytes(targetSpecialDataOffset),
            "cross-level-existing-moby-special-pointer",
            label,
            targetTrueIndex,
            "0x00",
            $"Point transformed T{targetTrueIndex} at imported {sourceLevel.DisplayName} donor T{sourceTrueIndex} special data.",
            patches,
            writtenWadOffsets);
        return true;
    }

    private static bool IsSimpleCrossLevelTemplate(JsonElement crossLevelTemplate)
    {
        string requiredFeature = JsonValue.GetString(crossLevelTemplate, "requiredExporterFeature");
        string supportStatus = JsonValue.GetString(crossLevelTemplate, "addSupportStatus");
        return string.Equals(requiredFeature, "DirectSourceRecordAppend", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(supportStatus, "supported-lightweight-object", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCrossLevelSourceRecordCandidateTemplate(JsonElement crossLevelTemplate)
    {
        string requiredFeature = JsonValue.GetString(crossLevelTemplate, "requiredExporterFeature");
        string supportStatus = JsonValue.GetString(crossLevelTemplate, "addSupportStatus");
        return string.Equals(requiredFeature, CrossLevelSourceRecordCandidateAppendFeature, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(supportStatus, "experimental-source-record-candidate", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCrossLevelExistingSlotCandidateTemplate(JsonElement crossLevelTemplate)
    {
        string requiredFeature = JsonValue.GetString(crossLevelTemplate, "requiredExporterFeature");
        string supportStatus = JsonValue.GetString(crossLevelTemplate, "addSupportStatus");
        return string.Equals(requiredFeature, CrossLevelSwapCatalogBuilder.CandidateExporterFeature, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(supportStatus, CrossLevelSwapCatalogBuilder.CandidateSupportStatus, StringComparison.OrdinalIgnoreCase);
    }

    private static MobyActorPackageImportPreview AddActorPackageImportPreview(
        FileStream stream,
        DiscLayout layout,
        LevelCatalog catalog,
        LevelDefinition targetLevel,
        long targetTableWadOffset,
        long targetTableRelativeOffset,
        string label,
        JsonElement crossLevelTemplate,
        string sourceImagePath,
        string workspaceRoot,
        bool allowPlanOnlyActorPackageImports,
        List<MobyActorPackageImportPreview> previews,
        List<MobySourcePatch> patches,
        HashSet<long> writtenWadOffsets,
        HashSet<string> writtenActorPackageRecipes)
    {
        string templateId = JsonValue.GetString(crossLevelTemplate, "id", "cross-level-template");
        string sourceLevelKey = JsonValue.GetString(crossLevelTemplate, "sourceLevelKey");
        string family = JsonValue.GetString(crossLevelTemplate, "family", InferCrossLevelFamily(templateId));
        string requestedRecipeId = JsonValue.GetString(crossLevelTemplate, "recipeId",
            JsonValue.GetString(crossLevelTemplate, "preferredRecipeId"));
        CrossLevelActorPackageRecipe? recipe = string.IsNullOrWhiteSpace(requestedRecipeId)
            ? null
            : CrossLevelActorPackageRecipeCatalog.All.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, requestedRecipeId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(LevelCatalog.NormalizeKey(candidate.TargetLevelKey), LevelCatalog.NormalizeKey(targetLevel.Key), StringComparison.OrdinalIgnoreCase) &&
                string.Equals(LevelCatalog.NormalizeKey(candidate.SourceLevelKey), LevelCatalog.NormalizeKey(sourceLevelKey), StringComparison.OrdinalIgnoreCase) &&
                string.Equals(NormalizeRecipeFamily(candidate.Family), NormalizeRecipeFamily(family), StringComparison.OrdinalIgnoreCase));
        recipe ??= CrossLevelActorPackageRecipeCatalog.FindPreferred(targetLevel.Key, sourceLevelKey, family, workspaceRoot);
        if (recipe == null && !string.IsNullOrWhiteSpace(workspaceRoot))
        {
            try
            {
                recipe = CrossLevelChestPackageRecipePlanner.TryCreateRecipe(
                    new EditorWorkspace(workspaceRoot),
                    catalog,
                    sourceImagePath,
                    targetLevel.Key,
                    sourceLevelKey,
                    family);
            }
            catch
            {
                recipe = null;
            }
        }
        if (recipe == null)
        {
            MobyActorPackageImportPreview preview = MobyActorPackageImportPreview.Unmapped(
                Label: label,
                TemplateId: templateId,
                TargetLevelKey: targetLevel.Key,
                SourceLevelKey: sourceLevelKey,
                Family: family,
                GuardReason: "No native actor-package recipe is mapped for this target/source/family yet.");
            previews.Add(preview);
            return preview;
        }

        LevelDefinition? sourceLevel = catalog.FindByKey(sourceLevelKey);
        if (sourceLevel == null || !sourceLevel.HasSourceTable)
        {
            MobyActorPackageImportPreview preview = MobyActorPackageImportPreview.Unmapped(
                Label: label,
                TemplateId: templateId,
                TargetLevelKey: targetLevel.Key,
                SourceLevelKey: sourceLevelKey,
                Family: family,
                GuardReason: $"The recipe {recipe.Id} exists, but the source level is not available in the native catalog.");
            previews.Add(preview);
            return preview;
        }

        long targetEntryBase = targetTableWadOffset - targetTableRelativeOffset;
        long sourceEntryBase = ParseRequiredLong(sourceLevel.SourceTableWadOffset, "sourceLevel.sourceTableWadOffset") -
            ParseRequiredLong(sourceLevel.SourceTableRelativeOffset, "sourceLevel.sourceTableRelativeOffset");

        CrossLevelActorPackageRecipeSafety packageLayoutSafety = CrossLevelActorPackageLayoutSafety.ValidateRecipe(
            stream,
            layout,
            WadLba,
            targetEntryBase,
            recipe);
        if (!packageLayoutSafety.Safe)
        {
            MobyActorPackageImportPreview preview = new(
                Label: label,
                TemplateId: templateId,
                TargetLevelKey: targetLevel.Key,
                SourceLevelKey: sourceLevel.Key,
                Family: family,
                RecipeId: recipe.Id,
                RecipeMode: recipe.Mode,
                RecipeStatus: recipe.Status,
                CanWriteImage: false,
                GuardReason: $"Blocked by actor-package layout safety: {packageLayoutSafety.Reason}",
                CopySegments: [],
                RootEntries: []);
            previews.Add(preview);
            return preview;
        }

        List<MobyActorPackageCopyPreview> copyPreviews = new();
        foreach (CrossLevelActorPackageCopySegment segment in recipe.CopySegments)
        {
            long sourceStart = ParseRequiredLong(segment.SourceStart, "recipe.copySegment.sourceStart");
            long targetStart = ParseRequiredLong(segment.TargetStart, "recipe.copySegment.targetStart");
            int length = checked((int)ParseRequiredLong(segment.Length, "recipe.copySegment.length"));
            long sourceWadOffset = sourceEntryBase + sourceStart;
            long targetWadOffset = targetEntryBase + targetStart;
            byte[] before = ReadWadBytes(stream, layout, targetWadOffset, length);
            byte[] after = ReadWadBytes(stream, layout, sourceWadOffset, length);
            RebaseActorPackageInternalReferences(after, recipe);
            copyPreviews.Add(new MobyActorPackageCopyPreview(
                SourceStart: segment.SourceStart,
                TargetStart: segment.TargetStart,
                SourceWadRelativeOffset: HexOffset(sourceWadOffset),
                TargetWadRelativeOffset: HexOffset(targetWadOffset),
                SourceImageOffset: HexOffset(ConvertWadOffsetToImageOffset(layout, sourceWadOffset)),
                TargetImageOffset: HexOffset(ConvertWadOffsetToImageOffset(layout, targetWadOffset)),
                ByteLength: length,
                TargetBeforeIsZero: before.All(value => value == 0),
                TargetSafety: string.IsNullOrWhiteSpace(segment.TargetSafety) ? "zero" : segment.TargetSafety,
                BeforeHexPreview: ToHexPreview(before),
                AfterHexPreview: ToHexPreview(after)));
        }

        List<MobyActorPackageRootPreview> rootPreviews = new();
        AddRootPreviews(stream, layout, targetLevel, targetEntryBase, recipe.RootEntries, "register", true, rootPreviews);
        AddRootPreviews(stream, layout, targetLevel, targetEntryBase, recipe.ReplaceRootEntries, "replace", false, rootPreviews);

        bool allCopiesSafe = recipe.CopySegments
            .Zip(copyPreviews)
            .All(pair => IsCopySegmentSafe(stream, layout, targetEntryBase, pair.First, pair.Second));
        bool allRootsSafe = rootPreviews.All(preview => preview.RootSlotSafe && preview.ActorIdSlotSafe);
        bool recipeAllowsNormalWrite = string.Equals(recipe.Status, "verified-image-write", StringComparison.OrdinalIgnoreCase);
        bool isExperimentalWriteRecipe = string.Equals(recipe.Status, "experimental-image-write", StringComparison.OrdinalIgnoreCase);
        bool isPlanOnlyRecipe = string.Equals(recipe.Status, "experimental-plan-only", StringComparison.OrdinalIgnoreCase);
        bool isBlockedRecipe = recipe.Status.StartsWith("in-game-blocked", StringComparison.OrdinalIgnoreCase) ||
            recipe.Status.StartsWith("blocked", StringComparison.OrdinalIgnoreCase);
        bool isCandidateGatedRecipe = isExperimentalWriteRecipe || isPlanOnlyRecipe;
        string expectedRecipeFingerprint = CrossLevelCandidateRecipeFingerprint.Create(recipe.Id, recipe.Status, copyPreviews, rootPreviews);
        CrossLevelCandidateEvidence latestRecipeEvidence = isCandidateGatedRecipe
            ? CrossLevelCandidateEvidenceStore.FindBestEvidence(workspaceRoot, targetLevel.Key, recipe.Id)
            : CrossLevelCandidateEvidence.NotFound;
        CrossLevelCandidateEvidence passedEvidence = isCandidateGatedRecipe
            ? CrossLevelCandidateEvidenceStore.FindPassedEvidence(workspaceRoot, targetLevel.Key, recipe.Id, expectedRecipeFingerprint)
            : CrossLevelCandidateEvidence.NotFound;
        bool candidateWriteAllowed = isCandidateGatedRecipe &&
            (allowPlanOnlyActorPackageImports || passedEvidence.Passed);
        bool canWriteImage = recipeAllowsNormalWrite && allCopiesSafe && allRootsSafe;
        if (!canWriteImage && candidateWriteAllowed && allCopiesSafe && allRootsSafe)
            canWriteImage = true;
        string guardReason = isBlockedRecipe
            ? $"Blocked after in-game validation: {recipe.Risk}"
            : canWriteImage
            ? candidateWriteAllowed && !recipeAllowsNormalWrite && allowPlanOnlyActorPackageImports
                ? "Writable only because disposable candidate mode is enabled: recipe target ranges are safe, but the recipe still needs in-game validation before normal Create BIN can use it."
                : candidateWriteAllowed && !recipeAllowsNormalWrite && passedEvidence.Passed
                ? $"Writable because candidate result {Path.GetFileName(passedEvidence.ResultPath)} records passed in-game evidence for this recipe."
                : "Writable in normal BIN output: this recipe has been verified for image writes."
            : allCopiesSafe && allRootsSafe
            ? latestRecipeEvidence.Passed && !passedEvidence.Passed
                ? "Preview only: a prior pass exists for this recipe id, but the package/root fingerprint changed; create and test a fresh candidate before normal Create BIN can use it."
                : "Preview only: recipe target ranges look writable, but this recipe must pass a disposable candidate test before normal Create BIN can use it."
            : "Preview only: one or more target copy/root slots are not in the expected state; do not write this recipe yet.";

        MobyActorPackageImportPreview importPreview = new(
            Label: label,
            TemplateId: templateId,
            TargetLevelKey: targetLevel.Key,
            SourceLevelKey: sourceLevel.Key,
            Family: family,
            RecipeId: recipe.Id,
            RecipeMode: recipe.Mode,
            RecipeStatus: recipe.Status,
            CanWriteImage: canWriteImage,
            GuardReason: guardReason,
            CopySegments: copyPreviews,
            RootEntries: rootPreviews);
        previews.Add(importPreview);

        if (canWriteImage && writtenActorPackageRecipes.Add(recipe.Id))
        {
            AddActorPackageImportPatches(stream, layout, targetLevel, recipe, targetEntryBase, sourceEntryBase, label, patches, writtenWadOffsets);
        }

        return importPreview;
    }

    private static string NormalizeRecipeFamily(string value)
    {
        return (value ?? "").Trim().Replace(" ", "", StringComparison.Ordinal).Replace("_", "", StringComparison.Ordinal).ToLowerInvariant();
    }

    private static void AddActorPackageImportPatches(
        FileStream stream,
        DiscLayout layout,
        LevelDefinition targetLevel,
        CrossLevelActorPackageRecipe recipe,
        long targetEntryBase,
        long sourceEntryBase,
        string label,
        List<MobySourcePatch> patches,
        HashSet<long> writtenWadOffsets)
    {
        foreach (CrossLevelActorPackageCopySegment segment in recipe.CopySegments)
        {
            long sourceStart = ParseRequiredLong(segment.SourceStart, "recipe.copySegment.sourceStart");
            long targetStart = ParseRequiredLong(segment.TargetStart, "recipe.copySegment.targetStart");
            int length = checked((int)ParseRequiredLong(segment.Length, "recipe.copySegment.length"));
            byte[] after = ReadWadBytes(stream, layout, sourceEntryBase + sourceStart, length);
            IReadOnlyList<string> rebaseNotes = RebaseActorPackageInternalReferences(after, recipe);
            AddRawPatch(
                stream,
                layout,
                targetLevel,
                targetEntryBase + targetStart,
                after,
                "actor-package-copy",
                label,
                -1,
                segment.TargetStart,
                $"Copy cross-level actor package segment {segment.SourceStart}+{segment.Length} to target {segment.TargetStart} for recipe {recipe.Id}.{FormatRebaseNotes(rebaseNotes)}",
                patches,
                writtenWadOffsets);
        }

        AddActorRootPatches(stream, layout, targetLevel, targetEntryBase, recipe, recipe.RootEntries, "actor-package-root-register", label, patches, writtenWadOffsets);
        AddActorRootPatches(stream, layout, targetLevel, targetEntryBase, recipe, recipe.ReplaceRootEntries, "actor-package-root-replace", label, patches, writtenWadOffsets);
    }

    private static IReadOnlyList<string> RebaseActorPackageInternalReferences(byte[] bytes, CrossLevelActorPackageRecipe recipe)
    {
        if (recipe.InternalDependencyRebases.Count == 0)
            return [];

        List<string> rewrites = new();
        for (int offset = 0; offset <= bytes.Length - 4; offset += 4)
        {
            uint value = BitConverter.ToUInt32(bytes, offset);
            foreach (CrossLevelActorPackageDependencyRebase rebase in recipe.InternalDependencyRebases)
            {
                uint sourceRoot = checked((uint)ParseRequiredLong(rebase.SourceRoot, "recipe.internalDependencyRebase.sourceRoot"));
                uint targetRoot = checked((uint)ParseRequiredLong(rebase.TargetRoot, "recipe.internalDependencyRebase.targetRoot"));
                uint length = checked((uint)ParseRequiredLong(rebase.Length, "recipe.internalDependencyRebase.length"));
                if (sourceRoot == 0 || targetRoot == 0 || length == 0)
                    continue;
                if (value < sourceRoot || value >= sourceRoot + length)
                    continue;

                uint rebased = targetRoot + (value - sourceRoot);
                Array.Copy(BitConverter.GetBytes(rebased), 0, bytes, offset, 4);
                rewrites.Add($"+0x{offset:X}: 0x{value:X}->0x{rebased:X} actor {rebase.ActorId}");
                break;
            }
        }

        return rewrites;
    }

    private static string FormatRebaseNotes(IReadOnlyList<string> rebaseNotes)
    {
        return rebaseNotes.Count == 0
            ? ""
            : $" Rebased internal dependency pointer(s): {string.Join("; ", rebaseNotes)}.";
    }

    private static bool IsCopySegmentSafe(
        FileStream stream,
        DiscLayout layout,
        long targetEntryBase,
        CrossLevelActorPackageCopySegment segment,
        MobyActorPackageCopyPreview preview)
    {
        string safety = string.IsNullOrWhiteSpace(segment.TargetSafety) ? "zero" : segment.TargetSafety.Trim();
        if (string.Equals(safety, "zero", StringComparison.OrdinalIgnoreCase))
            return preview.TargetBeforeIsZero;

        bool isExpectedActorPackageOverwrite =
            string.Equals(safety, "overwrite-unused-actor-package", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(safety, "overwrite-existing-actor-package", StringComparison.OrdinalIgnoreCase);
        bool isExpectedActorPackageSubrangeOverwrite =
            string.Equals(safety, "overwrite-unused-actor-package-subrange", StringComparison.OrdinalIgnoreCase);
        if ((!isExpectedActorPackageOverwrite && !isExpectedActorPackageSubrangeOverwrite) ||
            string.IsNullOrWhiteSpace(segment.ExpectedExistingRootSlot) ||
            string.IsNullOrWhiteSpace(segment.ExpectedExistingActorId))
        {
            return false;
        }

        int rootSlot = checked((int)ParseRequiredLong(segment.ExpectedExistingRootSlot, "recipe.copySegment.expectedExistingRootSlot"));
        int rootIndex = (rootSlot - 0x50) / 4;
        if (rootIndex < 0)
            return false;

        uint targetStart = checked((uint)ParseRequiredLong(segment.TargetStart, "recipe.copySegment.targetStart"));
        uint length = checked((uint)ParseRequiredLong(segment.Length, "recipe.copySegment.length"));
        ushort expectedActor = checked((ushort)ParseRequiredLong(segment.ExpectedExistingActorId, "recipe.copySegment.expectedExistingActorId"));
        uint existingRoot = BitConverter.ToUInt32(ReadWadBytes(stream, layout, targetEntryBase + rootSlot, 4), 0);
        ushort existingActor = BitConverter.ToUInt16(ReadWadBytes(stream, layout, targetEntryBase + 0x150 + ((long)rootIndex * 2), 2), 0);
        if (existingActor != expectedActor)
            return false;

        if (isExpectedActorPackageSubrangeOverwrite)
            return IsActorPackageSubrangeCopySafe(stream, layout, targetEntryBase, existingRoot, targetStart, length, segment);

        if (existingRoot != targetStart)
            return false;

        uint nextRoot = FindNextActorPackageRoot(stream, layout, targetEntryBase, existingRoot);
        if (nextRoot > existingRoot)
        {
            return existingRoot + length <= nextRoot;
        }

        return true;
    }

    private static bool IsActorPackageSubrangeCopySafe(
        FileStream stream,
        DiscLayout layout,
        long targetEntryBase,
        uint containingRoot,
        uint targetStart,
        uint length,
        CrossLevelActorPackageCopySegment segment)
    {
        if (containingRoot == 0 || targetStart < containingRoot || length == 0)
            return false;

        uint targetEnd = checked(targetStart + length);
        HashSet<ushort> allowedOverwrittenActors = ParseActorIdList(segment.AllowedOverwrittenActorIds);
        if (allowedOverwrittenActors.Count == 0)
            return false;

        uint nextRootAfterTarget = 0;
        for (int index = 0; index < 64; index++)
        {
            uint root = BitConverter.ToUInt32(ReadWadBytes(stream, layout, targetEntryBase + 0x50 + (index * 4), 4), 0);
            if (root == 0)
                continue;

            ushort actorId = BitConverter.ToUInt16(ReadWadBytes(stream, layout, targetEntryBase + 0x150 + ((long)index * 2), 2), 0);
            if (root > containingRoot && root < targetStart)
                return false;
            if (root >= targetStart && root < targetEnd && !allowedOverwrittenActors.Contains(actorId))
                return false;
            if (root >= targetEnd && (nextRootAfterTarget == 0 || root < nextRootAfterTarget))
                nextRootAfterTarget = root;
        }

        return nextRootAfterTarget != 0 && targetEnd <= nextRootAfterTarget;
    }

    private static HashSet<ushort> ParseActorIdList(string actorIds)
    {
        HashSet<ushort> values = new();
        foreach (string part in (actorIds ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            values.Add(checked((ushort)ParseRequiredLong(part, "recipe.copySegment.allowedOverwrittenActorIds")));
        }

        return values;
    }

    private static uint FindNextActorPackageRoot(FileStream stream, DiscLayout layout, long targetEntryBase, uint afterRoot)
    {
        uint nextRoot = 0;
        for (int index = 0; index < 64; index++)
        {
            uint root = BitConverter.ToUInt32(ReadWadBytes(stream, layout, targetEntryBase + 0x50 + (index * 4), 4), 0);
            if (root > afterRoot && (nextRoot == 0 || root < nextRoot))
                nextRoot = root;
        }

        return nextRoot;
    }

    private static void AddActorRootPatches(
        FileStream stream,
        DiscLayout layout,
        LevelDefinition targetLevel,
        long targetEntryBase,
        CrossLevelActorPackageRecipe recipe,
        IReadOnlyList<CrossLevelActorPackageRootEntry> entries,
        string kind,
        string label,
        List<MobySourcePatch> patches,
        HashSet<long> writtenWadOffsets)
    {
        foreach (CrossLevelActorPackageRootEntry entry in entries)
        {
            int targetRootSlot = checked((int)ParseRequiredLong(entry.TargetRootSlot, "recipe.rootEntry.targetRootSlot"));
            int rootIndex = (targetRootSlot - 0x50) / 4;
            uint targetRoot = checked((uint)ParseRequiredLong(entry.TargetRoot, "recipe.rootEntry.targetRoot"));
            ushort actorId = checked((ushort)ParseRequiredLong(entry.ActorId, "recipe.rootEntry.actorId"));
            long rootWadOffset = targetEntryBase + targetRootSlot;
            long actorIdWadOffset = targetEntryBase + 0x150 + ((long)rootIndex * 2);

            AddRawPatch(
                stream,
                layout,
                targetLevel,
                rootWadOffset,
                BitConverter.GetBytes(targetRoot),
                kind,
                label,
                -1,
                entry.TargetRootSlot,
                $"Set actor root slot {entry.TargetRootSlot} to {entry.TargetRoot} for recipe {recipe.Id}.",
                patches,
                writtenWadOffsets);

            AddRawPatch(
                stream,
                layout,
                targetLevel,
                actorIdWadOffset,
                BitConverter.GetBytes(actorId),
                $"{kind}-actor-id",
                label,
                -1,
                $"actor-id:{entry.TargetRootSlot}",
                $"Set actor id for root slot {entry.TargetRootSlot} to {entry.ActorId} for recipe {recipe.Id}.",
                patches,
                writtenWadOffsets);
        }
    }

    private static void AddRootPreviews(
        FileStream stream,
        DiscLayout layout,
        LevelDefinition targetLevel,
        long targetEntryBase,
        IReadOnlyList<CrossLevelActorPackageRootEntry> entries,
        string operation,
        bool expectEmpty,
        List<MobyActorPackageRootPreview> rootPreviews)
    {
        foreach (CrossLevelActorPackageRootEntry entry in entries)
        {
            int targetRootSlot = checked((int)ParseRequiredLong(entry.TargetRootSlot, "recipe.rootEntry.targetRootSlot"));
            int rootIndex = (targetRootSlot - 0x50) / 4;
            long rootWadOffset = targetEntryBase + targetRootSlot;
            long actorIdWadOffset = targetEntryBase + 0x150 + ((long)rootIndex * 2);
            byte[] oldRootBytes = ReadWadBytes(stream, layout, rootWadOffset, 4);
            byte[] oldActorBytes = ReadWadBytes(stream, layout, actorIdWadOffset, 2);
            uint existingRoot = BitConverter.ToUInt32(oldRootBytes, 0);
            ushort existingActorId = BitConverter.ToUInt16(oldActorBytes, 0);
            bool rootSlotSafe = expectEmpty ? existingRoot == 0 : true;
            bool actorIdSlotSafe = expectEmpty ? existingActorId == 0 : true;
            rootPreviews.Add(new MobyActorPackageRootPreview(
                Operation: operation,
                TargetRootSlot: entry.TargetRootSlot,
                RootIndex: rootIndex,
                TargetRoot: entry.TargetRoot,
                TargetActorId: entry.ActorId,
                ExistingRoot: $"0x{existingRoot:X8}",
                ExistingActorId: $"0x{existingActorId:X4}",
                RootListWadRelativeOffset: HexOffset(rootWadOffset),
                ActorIdListWadRelativeOffset: HexOffset(actorIdWadOffset),
                RootListImageOffset: HexOffset(ConvertWadOffsetToImageOffset(layout, rootWadOffset)),
                ActorIdListImageOffset: HexOffset(ConvertWadOffsetToImageOffset(layout, actorIdWadOffset)),
                RootSlotSafe: rootSlotSafe,
                ActorIdSlotSafe: actorIdSlotSafe,
                Note: string.IsNullOrWhiteSpace(entry.Note)
                    ? $"{operation} {targetLevel.DisplayName} actor-root index {rootIndex}."
                    : entry.Note));
        }
    }

    private static string InferCrossLevelFamily(string templateId)
    {
        string normalized = (templateId ?? "").ToLowerInvariant();
        if (normalized.Contains("spring_chest", StringComparison.Ordinal) || normalized.Contains("springchest", StringComparison.Ordinal))
            return "springChest";
        if (normalized.Contains("locked_chest", StringComparison.Ordinal) || normalized.Contains("lockedchest", StringComparison.Ordinal))
            return "lockedChest";
        if (normalized.Contains("green_wizard", StringComparison.Ordinal) || normalized.Contains("wizard", StringComparison.Ordinal))
            return "enemyTransform";
        if (normalized.Contains(".key.", StringComparison.Ordinal))
            return "key";
        return "";
    }

    private static bool IsContainedGemAppend(JsonElement edit, int targetType)
    {
        int flag4A = JsonValue.GetInt32(edit, "flag4AHex", -1);
        int flag4B = JsonValue.GetInt32(edit, "flag4BEditedHex", JsonValue.GetInt32(edit, "flag4BHex", -1));
        return targetType == 0x00 &&
            flag4A == 0xFF &&
            GemIdByteValue(flag4B) > 0 &&
            edit.TryGetProperty("chestContentLinkEdit", out JsonElement link) &&
            link.ValueKind == JsonValueKind.Object;
    }

    private static int FindContainedGemDonor(FileStream stream, DiscLayout layout, long tableWadOffset, long tableRelativeOffset, int sourceRecordCount)
    {
        for (int trueIndex = 0; trueIndex < sourceRecordCount; trueIndex++)
        {
            byte[] record = ReadWadBytes(stream, layout, tableWadOffset + (trueIndex * RecordStride), RecordStride);
            uint specialDataOffset = BitConverter.ToUInt32(record, 0);
            if (record[TypeOffset] == 0x00 &&
                record[0x52] == 0xFF &&
                GemIdByteValue(record[0x53]) > 0 &&
                IsSourceSpecialDataOffset(tableRelativeOffset, specialDataOffset))
                return trueIndex;
        }

        return -1;
    }

    private static bool TryAppendContainedGemSpecialData(
        FileStream stream,
        DiscLayout layout,
        LevelDefinition level,
        long tableWadOffset,
        long tableRelativeOffset,
        int donorTrueIndex,
        int appendTrueIndex,
        string label,
        JsonElement edit,
        byte[] recordBytes,
        List<MobySourcePatch> patches,
        HashSet<long> writtenWadOffsets,
        List<string> skippedEdits,
        out string description)
    {
        description = "";
        if (tableRelativeOffset <= 0)
        {
            skippedEdits.Add($"{label}: contained-gem add needs {level.DisplayName}'s source table-relative offset.");
            return false;
        }

        uint donorSpecialDataOffset = BitConverter.ToUInt32(recordBytes, 0);
        if (!IsSourceSpecialDataOffset(tableRelativeOffset, donorSpecialDataOffset))
        {
            skippedEdits.Add($"{label}: contained-gem donor T{donorTrueIndex} has no valid source special-data offset.");
            return false;
        }

        SpecialDataAllocator allocator = BuildSpecialDataAllocator(stream, layout, tableWadOffset, tableRelativeOffset, level.SourceRecordCount);
        int specialDataLength = GetSpecialDataLength(allocator, donorSpecialDataOffset, recordBytes);
        uint appendSpecialDataOffset = ReserveSpecialDataOffset(stream, layout, allocator, specialDataLength, writtenWadOffsets);
        long donorSpecialWadOffset = allocator.WadBaseOffset + donorSpecialDataOffset;
        long appendSpecialWadOffset = allocator.WadBaseOffset + appendSpecialDataOffset;
        byte[] specialBytes = ReadWadBytes(stream, layout, donorSpecialWadOffset, specialDataLength);
        WriteChestContentLinkBytes(specialBytes, edit, level, label, appendTrueIndex);

        AddRawPatch(
            stream,
            layout,
            level,
            appendSpecialWadOffset,
            specialBytes,
            "moby-special-data-append",
            label,
            appendTrueIndex,
            "special",
            $"Copy contained-gem donor T{donorTrueIndex} special data to source offset 0x{appendSpecialDataOffset:X} for appended T{appendTrueIndex}.",
            patches,
            writtenWadOffsets);
        WriteInt32(recordBytes, 0, (int)appendSpecialDataOffset);
        description = $" Copied contained-gem special data to source offset 0x{appendSpecialDataOffset:X} and linked it to its owning chest.";
        return true;
    }

    private static bool TryAppendSameLevelSpecialData(
        FileStream stream,
        DiscLayout layout,
        LevelDefinition level,
        long tableWadOffset,
        long tableRelativeOffset,
        int donorTrueIndex,
        int appendTrueIndex,
        string label,
        byte[] recordBytes,
        List<MobySourcePatch> patches,
        HashSet<long> writtenWadOffsets,
        List<string> skippedEdits,
        out string description)
    {
        description = "";
        if (tableRelativeOffset <= 0)
        {
            skippedEdits.Add($"{label}: same-level add needs {level.DisplayName}'s source table-relative offset to clone special data.");
            return false;
        }

        uint donorSpecialDataOffset = BitConverter.ToUInt32(recordBytes, 0);
        if (!IsSourceSpecialDataOffset(tableRelativeOffset, donorSpecialDataOffset))
            return true;

        SpecialDataAllocator allocator = BuildSpecialDataAllocator(stream, layout, tableWadOffset, tableRelativeOffset, level.SourceRecordCount);
        int specialDataLength = GetSpecialDataLength(allocator, donorSpecialDataOffset, recordBytes);
        uint appendSpecialDataOffset = ReserveSpecialDataOffset(stream, layout, allocator, specialDataLength, writtenWadOffsets);
        long donorSpecialWadOffset = allocator.WadBaseOffset + donorSpecialDataOffset;
        long appendSpecialWadOffset = allocator.WadBaseOffset + appendSpecialDataOffset;
        byte[] specialBytes = ReadWadBytes(stream, layout, donorSpecialWadOffset, specialDataLength);

        AddRawPatch(
            stream,
            layout,
            level,
            appendSpecialWadOffset,
            specialBytes,
            "moby-special-data-clone",
            label,
            appendTrueIndex,
            "special",
            $"Clone same-level donor T{donorTrueIndex} special data to source offset 0x{appendSpecialDataOffset:X} for appended T{appendTrueIndex}.",
            patches,
            writtenWadOffsets);
        WriteInt32(recordBytes, 0, (int)appendSpecialDataOffset);
        description = $" Cloned same-level special data to source offset 0x{appendSpecialDataOffset:X}.";
        return true;
    }

    private static int SelectBehaviorLinkedAppendDonorWithUsableSpecialData(
        FileStream stream,
        DiscLayout layout,
        long tableWadOffset,
        long tableRelativeOffset,
        int sourceRecordCount,
        int donorTrueIndex,
        int targetType,
        int sourceByte36,
        int sourceByte37,
        int sourceByte4F,
        int flag4A,
        int flag4B)
    {
        if (targetType != 0x20 ||
            sourceByte4F == 0x00 ||
            donorTrueIndex < 0 ||
            donorTrueIndex >= sourceRecordCount ||
            tableRelativeOffset <= 0)
        {
            return donorTrueIndex;
        }

        SpecialDataAllocator allocator = BuildSpecialDataAllocator(stream, layout, tableWadOffset, tableRelativeOffset, sourceRecordCount);
        byte[] requestedRecord = ReadWadBytes(stream, layout, tableWadOffset + ((long)donorTrueIndex * RecordStride), RecordStride);
        if (!HasBlankSourceSpecialData(stream, layout, allocator, tableRelativeOffset, requestedRecord))
            return donorTrueIndex;

        ushort requestedBehaviorMarker = requestedRecord.Length > 0x39
            ? BitConverter.ToUInt16(requestedRecord, 0x38)
            : (ushort)0;
        int bestTrueIndex = -1;
        int bestScore = int.MaxValue;
        for (int trueIndex = 0; trueIndex < sourceRecordCount; trueIndex++)
        {
            byte[] candidate = ReadWadBytes(stream, layout, tableWadOffset + ((long)trueIndex * RecordStride), RecordStride);
            if (candidate.Length <= 0x53 ||
                candidate[TypeOffset] != (byte)targetType ||
                candidate[0x36] != (byte)sourceByte36 ||
                candidate[0x37] != (byte)sourceByte37 ||
                candidate[0x4F] != (byte)sourceByte4F ||
                candidate[0x52] != (byte)flag4A)
            {
                continue;
            }

            if (requestedBehaviorMarker != 0 &&
                candidate.Length > 0x39 &&
                BitConverter.ToUInt16(candidate, 0x38) != requestedBehaviorMarker)
            {
                continue;
            }

            if (!HasNonBlankSourceSpecialData(stream, layout, allocator, tableRelativeOffset, candidate))
                continue;

            int score = 0;
            if (candidate[0x53] != (byte)flag4B)
                score += 100;
            score += Math.Abs(trueIndex - donorTrueIndex);
            if (score < bestScore)
            {
                bestScore = score;
                bestTrueIndex = trueIndex;
            }
        }

        return bestTrueIndex >= 0 ? bestTrueIndex : donorTrueIndex;
    }

    private static bool HasBlankSourceSpecialData(
        FileStream stream,
        DiscLayout layout,
        SpecialDataAllocator allocator,
        long tableRelativeOffset,
        byte[] recordBytes)
    {
        if (!TryReadSourceSpecialData(stream, layout, allocator, tableRelativeOffset, recordBytes, out byte[] bytes))
            return false;
        return bytes.All(value => value == 0);
    }

    private static bool HasNonBlankSourceSpecialData(
        FileStream stream,
        DiscLayout layout,
        SpecialDataAllocator allocator,
        long tableRelativeOffset,
        byte[] recordBytes)
    {
        return TryReadSourceSpecialData(stream, layout, allocator, tableRelativeOffset, recordBytes, out byte[] bytes) &&
            bytes.Any(value => value != 0);
    }

    private static bool TryReadSourceSpecialData(
        FileStream stream,
        DiscLayout layout,
        SpecialDataAllocator allocator,
        long tableRelativeOffset,
        byte[] recordBytes,
        out byte[] bytes)
    {
        bytes = [];
        if (recordBytes.Length <= TypeOffset)
            return false;

        uint offset = BitConverter.ToUInt32(recordBytes, 0);
        if (!IsSourceSpecialDataOffset(tableRelativeOffset, offset))
            return false;

        int length = GetSpecialDataLength(allocator, offset, recordBytes);
        if (length <= 0)
            return false;

        bytes = ReadWadBytes(stream, layout, allocator.WadBaseOffset + offset, length);
        return true;
    }

    private static void WriteChestContentLinkBytes(byte[] bytes, JsonElement edit, LevelDefinition level, string label, int appendTrueIndex)
    {
        if (bytes.Length < 16)
            throw new InvalidOperationException($"{label}: contained-gem special data needs at least 16 bytes for the chest link.");
        if (!edit.TryGetProperty("chestContentLinkEdit", out JsonElement link) || link.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException($"{label}: missing chest-content link edit.");

        int chestTrueIndex = JsonValue.GetInt32(link, "chestTrueIndex", -1);
        if (chestTrueIndex < 0 || chestTrueIndex >= level.SourceRecordCount && chestTrueIndex >= appendTrueIndex)
            throw new InvalidOperationException($"{label}: chest-content link target T{chestTrueIndex} is outside {level.DisplayName}'s source table and has not been appended before this row.");

        JsonElement rawOffset = link.TryGetProperty("rawOffset", out JsonElement raw) && raw.ValueKind == JsonValueKind.Object
            ? raw
            : default;
        WriteInt32(bytes, 0, chestTrueIndex);
        WriteInt32(bytes, 4, JsonValue.GetInt32(rawOffset, "x", 0));
        WriteInt32(bytes, 8, JsonValue.GetInt32(rawOffset, "y", 0));
        WriteInt32(bytes, 12, JsonValue.GetInt32(rawOffset, "z", 0));
    }

    private static SpecialDataAllocator BuildSpecialDataAllocator(FileStream stream, DiscLayout layout, long tableWadOffset, long tableRelativeOffset, int sourceRecordCount)
    {
        return BuildSpecialDataAllocatorCore(
            stream,
            layout,
            tableWadOffset,
            sourceRecordCount,
            tableWadOffset - tableRelativeOffset,
            checked((uint)tableRelativeOffset));
    }

    private static SpecialDataAllocator BuildSpecialDataAllocatorCore(
        FileStream stream,
        DiscLayout layout,
        long tableWadOffset,
        int sourceRecordCount,
        long wadBaseOffset,
        uint maximumSourceOffset)
    {
        List<SpecialDataEntry> entries = new();
        for (int trueIndex = 0; trueIndex < sourceRecordCount; trueIndex++)
        {
            byte[] record = ReadWadBytes(stream, layout, tableWadOffset + (trueIndex * RecordStride), RecordStride);
            uint offset = BitConverter.ToUInt32(record, 0);
            if (IsSourceSpecialDataOffset(maximumSourceOffset, offset))
                entries.Add(new SpecialDataEntry(offset, record[TypeOffset]));
        }

        List<SpecialDataEntry> unique = entries
            .GroupBy(entry => entry.Offset)
            .Select(group => group.First())
            .OrderBy(entry => entry.Offset)
            .ToList();
        Dictionary<uint, int> lengths = new();
        uint maxEnd = 0;
        for (int i = 0; i < unique.Count; i++)
        {
            SpecialDataEntry entry = unique[i];
            int length = 0;
            if (i + 1 < unique.Count)
            {
                uint delta = unique[i + 1].Offset - entry.Offset;
                if (delta > 0 && delta <= 0x400)
                    length = (int)delta;
            }

            if (length <= 0)
                length = GetDefaultSpecialDataLength(entry.Type);
            lengths[entry.Offset] = length;
            maxEnd = Math.Max(maxEnd, entry.Offset + (uint)length);
        }

        return new SpecialDataAllocator(wadBaseOffset, maximumSourceOffset, Align(maxEnd, 4), lengths);
    }

    private static LevelSceneSourceLayout ReadLevelSceneSourceLayout(
        FileStream stream,
        DiscLayout layout,
        LevelDefinition level,
        long tableWadOffset)
    {
        if (level.SourceWadEntry < 0)
            throw new InvalidOperationException($"{level.DisplayName} has no source WAD entry for its Life Chest scene data.");

        byte[] entryHeader = ReadWadBytes(stream, layout, level.SourceWadEntry * 8L, 8);
        long entryWadOffset = BitConverter.ToUInt32(entryHeader, 0);
        byte[] levelHeader = ReadWadBytes(stream, layout, entryWadOffset, 0x20);
        uint sceneRelativeOffset = BitConverter.ToUInt32(levelHeader, 0x18);
        uint sceneByteLength = BitConverter.ToUInt32(levelHeader, 0x1C);
        long sceneWadOffset = checked(entryWadOffset + sceneRelativeOffset);
        long tableRelativeToScene = tableWadOffset - sceneWadOffset;
        long tableEndRelativeToScene = tableRelativeToScene + ((long)level.SourceRecordCount * RecordStride);
        if (sceneRelativeOffset == 0 ||
            sceneByteLength == 0 ||
            tableRelativeToScene < 0 ||
            tableEndRelativeToScene > sceneByteLength)
        {
            throw new InvalidOperationException(
                $"{level.DisplayName}'s Life Chest table is not contained in its mapped level-scene block.");
        }

        return new LevelSceneSourceLayout(sceneWadOffset, sceneByteLength);
    }

    private static int GetSpecialDataLength(SpecialDataAllocator allocator, uint sourceOffset, byte[] recordBytes)
    {
        if (allocator.Lengths.TryGetValue(sourceOffset, out int length))
            return length;
        return GetDefaultSpecialDataLength(recordBytes[TypeOffset]);
    }

    private static int GetCrossLevelSpecialDataLength(SpecialDataAllocator allocator, CrossLevelAppendDonor donor, uint sourceOffset, byte[] recordBytes, out string note)
    {
        int length = GetSpecialDataLength(allocator, sourceOffset, recordBytes);
        note = "";

        if (!ShouldUseExtendedPeaceKeepersSpringChestSpecialData(donor, recordBytes))
            return length;

        uint remaining = allocator.TableRelativeOffset > sourceOffset
            ? allocator.TableRelativeOffset - sourceOffset
            : 0;
        bool useSharedCluster = donor.RecipeMode.Contains("ExtendedCluster", StringComparison.OrdinalIgnoreCase);
        int desiredLength = useSharedCluster ? 0x800 : 0x100;
        int extendedLength = (int)Math.Min(remaining, desiredLength);
        if (extendedLength <= length)
            return length;

        note = useSharedCluster
            ? "native spring chest shared-data cluster spans multiple referenced blocks"
            : "native spring chest data overlaps the next referenced block";
        return extendedLength;
    }

    private static bool ShouldUseExtendedPeaceKeepersSpringChestSpecialData(CrossLevelAppendDonor donor, byte[] recordBytes)
    {
        if (!string.Equals(LevelCatalog.NormalizeKey(donor.SourceLevel.Key), "peacekeepers", StringComparison.OrdinalIgnoreCase))
            return false;
        if (donor.SourceTrueIndex is not (70 or 71))
            return false;
        return recordBytes[TypeOffset] == 0x20 &&
               recordBytes[0x36] == 0x49 &&
               recordBytes[0x37] == 0x01;
    }

    private static bool ShouldSharePeaceKeepersSpringChestCluster(CrossLevelAppendDonor donor, byte[] recordBytes)
    {
        return donor.RecipeMode.Contains("SharedCluster", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(LevelCatalog.NormalizeKey(donor.SourceLevel.Key), "peacekeepers", StringComparison.OrdinalIgnoreCase) &&
               TryGetPeaceKeepersSpringClusterRelativeOffset(donor.SourceTrueIndex, out _) &&
               recordBytes[TypeOffset] is 0x18 or 0x20;
    }

    private static bool TryGetPeaceKeepersSpringClusterRelativeOffset(int sourceTrueIndex, out uint relativeOffset)
    {
        relativeOffset = sourceTrueIndex switch
        {
            70 => 0x000,
            71 => 0x014,
            72 => 0x028,
            73 => 0x040,
            74 => 0x058,
            80 => 0x000,
            81 => 0x014,
            82 => 0x028,
            83 => 0x03C,
            92 => 0x1A8,
            93 => 0x1C0,
            94 => 0x1D8,
            _ => uint.MaxValue
        };
        return relativeOffset != uint.MaxValue;
    }

    private static int GetDefaultSpecialDataLength(int type) => type == 0x00 ? 0x28 : 0x18;

    private static uint ReserveSpecialDataOffset(FileStream stream, DiscLayout layout, SpecialDataAllocator allocator, int length, HashSet<long>? writtenWadOffsets = null)
    {
        uint candidate = Align(allocator.NextSearchOffset, 4);
        while (candidate + length <= allocator.TableRelativeOffset)
        {
            long wadOffset = allocator.WadBaseOffset + candidate;
            byte[] bytes = ReadWadBytes(stream, layout, wadOffset, length);
            if (bytes.All(value => value == 0) && !OverlapsWrittenOffsets(wadOffset, length, writtenWadOffsets))
            {
                allocator.NextSearchOffset = Align(candidate + (uint)length, 4);
                return candidate;
            }

            candidate = Align(candidate + 4, 4);
        }

        throw new InvalidOperationException($"Could not find {length} free special-data bytes before the source moby table.");
    }

    private static bool OverlapsWrittenOffsets(long start, int length, HashSet<long>? writtenWadOffsets)
    {
        if (writtenWadOffsets == null || writtenWadOffsets.Count == 0)
            return false;

        long end = start + length;
        for (long offset = start; offset < end; offset++)
        {
            if (writtenWadOffsets.Contains(offset))
                return true;
        }

        return false;
    }

    private static bool IsSourceSpecialDataOffset(long tableRelativeOffset, uint offset)
    {
        return tableRelativeOffset > 0 && offset > 0 && offset < (uint)tableRelativeOffset;
    }

    private static uint Align(uint value, uint alignment)
    {
        uint mask = alignment - 1;
        return (value + mask) & ~mask;
    }

    private static int FindSameLevelDonor(
        FileStream stream,
        DiscLayout layout,
        long tableWadOffset,
        int sourceRecordCount,
        int targetType,
        int sourceByte36,
        int sourceByte37,
        int sourceByte4F,
        int flag4A,
        int flag4B)
    {
        int fallback = -1;
        int actorMatch = -1;
        int identityMatch = -1;
        int gemLikeMatch = -1;
        bool hasActorPreference = sourceByte36 >= 0 || sourceByte37 >= 0;
        for (int trueIndex = 0; trueIndex < sourceRecordCount; trueIndex++)
        {
            byte[] record = ReadWadBytes(stream, layout, tableWadOffset + (trueIndex * RecordStride), RecordStride);
            if (record[TypeOffset] != targetType)
                continue;

            fallback = trueIndex;
            bool matchesActor =
                (sourceByte36 < 0 || record[0x36] == (byte)sourceByte36) &&
                (sourceByte37 < 0 || record[0x37] == (byte)sourceByte37);
            if (matchesActor)
            {
                actorMatch = actorMatch < 0 ? trueIndex : actorMatch;
                bool matchesIdentity =
                    (sourceByte4F < 0 || record[0x4F] == (byte)sourceByte4F) &&
                    (flag4A < 0 || record[0x52] == (byte)flag4A) &&
                    (flag4B < 0 || record[0x53] == (byte)flag4B);
                if (matchesIdentity)
                    identityMatch = identityMatch < 0 ? trueIndex : identityMatch;
            }

            if (targetType == 0x20 && record[0x52] == 0x10 && GemIdByteValue(record[0x53]) > 0)
                gemLikeMatch = gemLikeMatch < 0 ? trueIndex : gemLikeMatch;
            if (targetType == 0x18 && record[0x52] == 0x40 && GemIdByteValue(record[0x36]) > 0)
                gemLikeMatch = gemLikeMatch < 0 ? trueIndex : gemLikeMatch;
        }

        if (identityMatch >= 0)
            return identityMatch;
        if (actorMatch >= 0)
            return actorMatch;
        if (!hasActorPreference && gemLikeMatch >= 0)
            return gemLikeMatch;
        if (gemLikeMatch >= 0 && targetType == 0x18)
            return gemLikeMatch;
        return fallback;
    }

    private static int FindExactSameLevelDonor(
        FileStream stream,
        DiscLayout layout,
        long tableWadOffset,
        int sourceRecordCount,
        int targetType,
        int targetState,
        int sourceByte36,
        int sourceByte37,
        int sourceByte4F,
        int flag4A,
        int flag4B)
    {
        for (int trueIndex = 0; trueIndex < sourceRecordCount; trueIndex++)
        {
            byte[] record = ReadWadBytes(stream, layout, tableWadOffset + ((long)trueIndex * RecordStride), RecordStride);
            if (record.Length > 0x53 &&
                record[TypeOffset] == (byte)targetType &&
                record[0x36] == (byte)sourceByte36 &&
                record[0x37] == (byte)sourceByte37 &&
                record[0x4F] == (byte)sourceByte4F &&
                record[0x52] == (byte)flag4A &&
                record[0x53] == (byte)flag4B)
            {
                return trueIndex;
            }
        }

        return -1;
    }

    private static void AddTreasureTotalPatch(
        FileStream stream,
        LevelCatalog catalog,
        LevelDefinition level,
        int delta,
        List<MobySourcePatch> patches,
        List<string> notes)
    {
        if (delta == 0)
            return;

        int levelIndex = -1;
        for (int i = 0; i < catalog.Levels.Count; i++)
        {
            if (string.Equals(catalog.Levels[i].Key, level.Key, StringComparison.OrdinalIgnoreCase))
            {
                levelIndex = i;
                break;
            }
        }

        if (levelIndex < 0)
        {
            notes.Add($"{level.DisplayName}'s in-game treasure target was not updated because the level was not found in the release catalog.");
            return;
        }

        long imageOffset = TreasureTotalTableImageOffset + (levelIndex * 2L);
        if (imageOffset < 0 || imageOffset + 2 > stream.Length)
        {
            notes.Add($"{level.DisplayName}'s in-game treasure target was not updated because the target table offset 0x{imageOffset:X} is outside this disc image.");
            return;
        }

        byte[] before = ReadImageBytes(stream, imageOffset, 2);
        int originalTotal = BitConverter.ToUInt16(before, 0);
        int editedTotal = originalTotal + delta;
        if (editedTotal is < 0 or > ushort.MaxValue)
            throw new InvalidOperationException($"{level.DisplayName}'s edited treasure total would be {editedTotal}, which cannot fit in the game's level treasure target table.");

        byte[] after = BitConverter.GetBytes((ushort)editedTotal);
        patches.Add(new MobySourcePatch(
            Label: $"{level.Key}-level-treasure-total",
            Kind: "level-treasure-total",
            LevelKey: level.Key,
            MobyLabel: "Inventory treasure total",
            TrueIndex: -1,
            RecordOffset: "",
            WadRelativeOffset: "",
            ImageOffset: $"0x{imageOffset:X}",
            ByteLength: after.Length,
            BeforeHexPreview: ToHex(before),
            AfterHexPreview: ToHex(after),
            Description: $"Set {level.DisplayName} inventory treasure target from {originalTotal} to {editedTotal} so the pause/inventory denominator matches the edited level treasure total."));
        notes.Add($"Updated {level.DisplayName}'s in-game treasure target from {originalTotal} to {editedTotal}.");
    }

    private static int ComputeTreasureTotalDelta(IEnumerable<JsonElement> edits)
    {
        int delta = 0;
        foreach (JsonElement edit in edits)
        {
            delta += ComputeTreasureDelta(edit);
        }

        return delta;
    }

    private static int ComputeTreasureDelta(JsonElement edit)
    {
        if (IsSpringChestControllerOrCompanionEdit(edit))
            return 0;

        bool added = JsonValue.GetBoolean(edit, "added") || string.Equals(JsonValue.GetString(edit, "editKind"), "add", StringComparison.OrdinalIgnoreCase);
        bool removed = JsonValue.GetBoolean(edit, "removed") || string.Equals(JsonValue.GetString(edit, "editKind"), "remove", StringComparison.OrdinalIgnoreCase);

        int originalType = ReadEditByte(edit, "typeOriginalHex", "typeHex", "typeEditedHex");
        int editedType = ReadEditByte(edit, "typeEditedHex", "typeHex", "typeOriginalHex");
        int originalSourceByte36 = ReadEditByte(edit, "sourceByte36OriginalHex", "sourceByte36Hex", "sourceByte36EditedHex");
        int editedSourceByte36 = ReadEditByte(edit, "sourceByte36EditedHex", "sourceByte36Hex", "sourceByte36OriginalHex");
        int originalSourceByte4F = ReadEditByte(edit, "sourceByte4FOriginalHex", "sourceByte4FHex", "sourceByte4FEditedHex");
        int editedSourceByte4F = ReadEditByte(edit, "sourceByte4FEditedHex", "sourceByte4FHex", "sourceByte4FOriginalHex");
        int originalFlag4A = ReadEditByte(edit, "flag4AOriginalHex", "flag4AHex", "flag4AEditedHex");
        int editedFlag4A = ReadEditByte(edit, "flag4AEditedHex", "flag4AHex", "flag4AOriginalHex");
        int originalFlag4B = ReadEditByte(edit, "flag4BOriginalHex", "flag4BHex", "flag4BEditedHex");
        int editedFlag4B = ReadEditByte(edit, "flag4BEditedHex", "flag4BHex", "flag4BOriginalHex");

        int originalValue = TreasureValueFromSourceBytes(originalType, originalSourceByte36, originalSourceByte4F, originalFlag4A, originalFlag4B);
        int editedValue = TreasureValueFromSourceBytes(editedType, editedSourceByte36, editedSourceByte4F, editedFlag4A, editedFlag4B);
        return removed ? -originalValue : added ? editedValue : editedValue - originalValue;
    }

    private static bool IsSpringChestControllerOrCompanionEdit(JsonElement edit)
    {
        if (!TryGetCrossLevelTemplate(edit, out JsonElement template))
            return false;

        string family = JsonValue.GetString(template, "family", InferCrossLevelFamily(JsonValue.GetString(template, "id")));
        string templateId = JsonValue.GetString(template, "id");
        return string.Equals(family, "springChest", StringComparison.OrdinalIgnoreCase) &&
            (templateId.Contains("spring_chest_controller", StringComparison.OrdinalIgnoreCase) ||
             templateId.Contains("spring_chest_companion", StringComparison.OrdinalIgnoreCase));
    }

    private static int TreasureValueFromSourceBytes(int type, int sourceByte36, int sourceByte4F, int flag4A, int flag4B)
    {
        if (type == 0x18 && GemValue.TryFromEncoding(sourceByte36, sourceByte4F, out GemValue visibleGem))
            return visibleGem.Value;

        if (GemValue.TryFromIdByte(flag4B, out GemValue rewardGem))
            return rewardGem.Value;

        return 0;
    }

    private static int TreasureValueFromRecordBytes(byte[] recordBytes)
    {
        if (recordBytes.Length <= 0x53)
            return 0;

        return TreasureValueFromSourceBytes(
            recordBytes[TypeOffset],
            recordBytes[0x36],
            recordBytes[0x4F],
            recordBytes[0x52],
            recordBytes[0x53]);
    }

    private static int ReadEditByte(JsonElement edit, string primaryName, string secondaryName, string tertiaryName)
    {
        int value = JsonValue.GetInt32(edit, primaryName, -1);
        if (value >= 0)
            return value;

        value = JsonValue.GetInt32(edit, secondaryName, -1);
        if (value >= 0)
            return value;

        return JsonValue.GetInt32(edit, tertiaryName, -1);
    }

    private static byte[] ReadImageBytes(FileStream stream, long imageOffset, int length)
    {
        long previousPosition = stream.Position;
        try
        {
            byte[] result = new byte[length];
            stream.Position = imageOffset;
            int read = stream.Read(result, 0, length);
            if (read != length)
                throw new EndOfStreamException($"Could not read {length} byte(s) at image offset 0x{imageOffset:X}.");
            return result;
        }
        finally
        {
            stream.Position = previousPosition;
        }
    }

    private static void AddSourceCountPatch(
        FileStream stream,
        DiscLayout layout,
        LevelDefinition level,
        long tableWadOffset,
        int newCount,
        List<MobySourcePatch> patches,
        HashSet<long> writtenWadOffsets,
        List<string> notes)
    {
        long wadOffset = tableWadOffset - 4;
        byte[] before = ReadWadBytes(stream, layout, wadOffset, 4);
        int currentCount = BitConverter.ToInt32(before, 0);
        int catalogPrefixRows = 0;
        if (currentCount != level.SourceRecordCount)
        {
            long oneRowLaterWadOffset = tableWadOffset + RecordStride - 4;
            byte[] oneRowLaterBefore = ReadWadBytes(stream, layout, oneRowLaterWadOffset, 4);
            int oneRowLaterCount = BitConverter.ToInt32(oneRowLaterBefore, 0);
            if (oneRowLaterCount == level.SourceRecordCount - 1)
            {
                catalogPrefixRows = 1;
                wadOffset = oneRowLaterWadOffset;
                before = oneRowLaterBefore;
                currentCount = oneRowLaterCount;
            }
            else if (currentCount > newCount)
            {
                notes.Add($"{level.DisplayName}'s bytes before the source moby table read as {currentCount}, not the catalog count {level.SourceRecordCount}. The exporter left that suspicious field unchanged and appended records into the already-addressable table range; this needs emulator validation before treating new objects as promoted for this level.");
                return;
            }
            else
            {
                throw new InvalidOperationException(
                    $"{level.DisplayName}'s source-count fields are {currentCount} and {oneRowLaterCount}, expected {level.SourceRecordCount} or one-row-prefix count {level.SourceRecordCount - 1}.");
            }
        }

        int newRuntimeCount = newCount - catalogPrefixRows;

        AddRawPatch(
            stream,
            layout,
            level,
            wadOffset,
            BitConverter.GetBytes(newRuntimeCount),
            "moby-source-count",
            "source count",
            -1,
            catalogPrefixRows == 0 ? "0x-4" : "prefix+0x54",
            catalogPrefixRows == 0
                ? $"Increase {level.DisplayName} source moby count from {level.SourceRecordCount} to {newCount}."
                : $"Increase {level.DisplayName} one-row-prefix source moby count from {currentCount} to {newRuntimeCount} (catalog rows {level.SourceRecordCount} to {newCount}).",
            patches,
            writtenWadOffsets);
    }

    private static void AddRemoveHidePatches(
        FileStream stream,
        DiscLayout layout,
        LevelDefinition level,
        long tableWadOffset,
        int trueIndex,
        string label,
        List<MobySourcePatch> patches,
        HashSet<long> writtenWadOffsets)
    {
        AddPatch(
            stream,
            layout,
            level,
            tableWadOffset,
            trueIndex,
            XOffset,
            BitConverter.GetBytes(HiddenRawCoordinate),
            "moby-coordinate-hide-x",
            label,
            $"Soft-remove {label} by moving X to hidden raw {HiddenRawCoordinate}.",
            patches,
            writtenWadOffsets);
        AddPatch(
            stream,
            layout,
            level,
            tableWadOffset,
            trueIndex,
            YOffset,
            BitConverter.GetBytes(HiddenRawCoordinate),
            "moby-coordinate-hide-y",
            label,
            $"Soft-remove {label} by moving Y to hidden raw {HiddenRawCoordinate}.",
            patches,
            writtenWadOffsets);
        AddPatch(
            stream,
            layout,
            level,
            tableWadOffset,
            trueIndex,
            ZOffset,
            BitConverter.GetBytes(HiddenRawCoordinate),
            "moby-coordinate-hide-z",
            label,
            $"Soft-remove {label} by moving Z to hidden raw {HiddenRawCoordinate}.",
            patches,
            writtenWadOffsets);
    }

    private static bool IsFlyInLandingEdit(JsonElement edit) =>
        string.Equals(
            JsonValue.GetString(edit, "editorControlKind"),
            Moby.FlyInLandingControlKind,
            StringComparison.OrdinalIgnoreCase);

    private static void AddFlyInLandingPatches(
        FileStream stream,
        DiscLayout layout,
        LevelDefinition level,
        string label,
        JsonElement edit,
        Lazy<FlyInLandingData> flyInLanding,
        List<MobySourcePatch> patches,
        HashSet<long> writtenWadOffsets)
    {
        if (!edit.TryGetProperty("rawEdited", out JsonElement rawEdited) || rawEdited.ValueKind != JsonValueKind.Object)
            return;

        FlyInLandingData landing = flyInLanding.Value;
        if (edit.TryGetProperty("rawOriginal", out JsonElement rawOriginal) && rawOriginal.ValueKind == JsonValueKind.Object)
        {
            int manifestX = JsonValue.GetInt32(rawOriginal, "x", landing.RawX);
            int manifestY = JsonValue.GetInt32(rawOriginal, "y", landing.RawY);
            int manifestZ = JsonValue.GetInt32(rawOriginal, "z", landing.RawZ);
            if (manifestX != landing.RawX || manifestY != landing.RawY || manifestZ != landing.RawZ)
            {
                throw new InvalidOperationException(
                    $"{level.DisplayName}'s saved fly-in landing source XYZ does not match the selected disc. Reopen that BIN/CUE before exporting this entry edit.");
            }
        }

        int manifestHeading = JsonValue.GetInt32(
            edit,
            "yawByteOriginalHex",
            JsonValue.GetInt32(edit, "yawByteHex", landing.YawByte));
        if (manifestHeading != landing.YawByte)
        {
            throw new InvalidOperationException(
                $"{level.DisplayName}'s saved fly-in heading does not match the selected disc. Reopen that BIN/CUE before exporting this entry edit.");
        }

        AddFlyInLandingAxisPatch(stream, layout, level, label, landing.WadOffset, "x", 0x00, landing.RawX, rawEdited, patches, writtenWadOffsets);
        AddFlyInLandingAxisPatch(stream, layout, level, label, landing.WadOffset, "y", 0x04, landing.RawY, rawEdited, patches, writtenWadOffsets);
        AddFlyInLandingAxisPatch(stream, layout, level, label, landing.WadOffset, "z", 0x08, landing.RawZ, rawEdited, patches, writtenWadOffsets);
        AddFlyInLandingHeadingPatch(stream, layout, level, label, landing, edit, patches, writtenWadOffsets);
    }

    private static void AddFlyInLandingHeadingPatch(
        FileStream stream,
        DiscLayout layout,
        LevelDefinition level,
        string label,
        FlyInLandingData landing,
        JsonElement edit,
        List<MobySourcePatch> patches,
        HashSet<long> writtenWadOffsets)
    {
        int edited = JsonValue.GetInt32(
            edit,
            "yawByteEditedHex",
            JsonValue.GetInt32(edit, "yawByteHex", landing.YawByte));
        edited = Math.Clamp(edited, 0, 255);
        if (edited == landing.YawByte)
            return;

        AddRawPatch(
            stream,
            layout,
            level,
            landing.WadOffset + 0x0E,
            [(byte)edited],
            "fly-in-landing-heading",
            label,
            -1,
            "entry+0x0E",
            $"Set {level.DisplayName}'s homeworld-to-level fly-in heading to {FlyInLandingEditorControl.HeadingByteToDegrees(edited):0.#} degrees; return-home data is unchanged.",
            patches,
            writtenWadOffsets);
    }

    private static void AddFlyInLandingAxisPatch(
        FileStream stream,
        DiscLayout layout,
        LevelDefinition level,
        string label,
        long landingWadOffset,
        string axis,
        int fieldOffset,
        int original,
        JsonElement rawEdited,
        List<MobySourcePatch> patches,
        HashSet<long> writtenWadOffsets)
    {
        if (!rawEdited.TryGetProperty(axis, out _))
            return;

        int edited = JsonValue.GetInt32(rawEdited, axis, original);
        if (edited == original)
            return;

        AddRawPatch(
            stream,
            layout,
            level,
            landingWadOffset + fieldOffset,
            BitConverter.GetBytes(edited),
            $"fly-in-landing-position-{axis}",
            label,
            -1,
            $"entry+0x{fieldOffset:X}",
            $"Move {level.DisplayName}'s homeworld-to-level fly-in landing {axis.ToUpperInvariant()} to raw {edited}; return-home data is unchanged.",
            patches,
            writtenWadOffsets);
    }

    private static void TrackMovedPortalSourceData(
        LevelDefinition level,
        int trueIndex,
        string label,
        JsonElement edit,
        Lazy<PortalSourceLevelData> portalSourceData,
        List<PortalSourceMovement> movements)
    {
        if (level.LevelId <= 0 || level.LevelId % 10 != 0 ||
            JsonValue.GetInt32(edit, "sourceByte36OriginalHex", -1) != 0x8E ||
            !edit.TryGetProperty("rawOriginal", out JsonElement rawOriginal) || rawOriginal.ValueKind != JsonValueKind.Object ||
            !edit.TryGetProperty("rawEdited", out JsonElement rawEdited) || rawEdited.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        int originalX = JsonValue.GetInt32(rawOriginal, "x");
        int originalY = JsonValue.GetInt32(rawOriginal, "y");
        int originalZ = JsonValue.GetInt32(rawOriginal, "z");
        int deltaX = checked(JsonValue.GetInt32(rawEdited, "x", originalX) - originalX);
        int deltaY = checked(JsonValue.GetInt32(rawEdited, "y", originalY) - originalY);
        int deltaZ = checked(JsonValue.GetInt32(rawEdited, "z", originalZ) - originalZ);
        if (deltaX == 0 && deltaY == 0 && deltaZ == 0)
            return;

        PortalSourceRecord? portal = portalSourceData.Value.Portals
            .SingleOrDefault(candidate => candidate.SourcePathMobyTrueIndex == trueIndex);
        if (portal == null)
            return;
        if (movements.Any(movement => movement.Portal.Index == portal.Index))
            throw new InvalidOperationException($"{level.DisplayName} portal {portal.Index} has more than one movement edit.");

        movements.Add(new PortalSourceMovement(
            portal,
            label,
            deltaX,
            deltaY,
            deltaZ,
            RawDeltaToCollisionDelta(deltaX),
            RawDeltaToCollisionDelta(deltaY),
            RawDeltaToCollisionDelta(deltaZ)));
    }

    private static void AddMovedPortalSourcePatches(
        FileStream stream,
        DiscLayout layout,
        LevelDefinition level,
        Lazy<PortalSourceLevelData> portalSourceData,
        IReadOnlyList<PortalSourceMovement> movements,
        List<MobySourcePatch> patches,
        HashSet<long> writtenWadOffsets)
    {
        if (movements.Count == 0)
            return;

        PortalSourceLevelData source = portalSourceData.Value;
        PortalCollisionSourceLayout collision = source.Collision;
        int triangleTableLength = checked(collision.TriangleCount * 12);
        byte[] translatedTriangleTable = ReadWadBytes(stream, layout, collision.TriangleTableWadOffset, triangleTableLength);
        List<(PortalSourceMovement Movement, PortalTriggerTriangle Triangle, byte[] After)> trianglePatches = [];

        foreach (PortalSourceMovement movement in movements)
        {
            if (movement.CollisionDeltaX == 0 && movement.CollisionDeltaY == 0 && movement.CollisionDeltaZ == 0)
                continue;

            PortalTriggerTriangle[] triggers = source.TriggerTriangles
                .Where(triangle => triangle.PortalIndex == movement.Portal.Index &&
                    triangle.DestinationLevelId == movement.Portal.DestinationLevelId)
                .ToArray();
            if (triggers.Length == 0)
            {
                throw new InvalidOperationException(
                    $"{level.DisplayName} portal {movement.Portal.Index} to level {movement.Portal.DestinationLevelId} has no proven entry collision triangles.");
            }

            foreach (PortalTriggerTriangle triangle in triggers)
            {
                byte[] translated = TranslatePortalTriggerTriangle(level, movement, triangle, translatedTriangleTable, collision);
                int tableOffset = checked((int)(triangle.WadOffset - collision.TriangleTableWadOffset));
                translated.CopyTo(translatedTriangleTable, tableOffset);
                trianglePatches.Add((movement, triangle, translated));
            }
        }

        TerrainPatchExporter.CollisionIndexBytes? rebuiltIndex = null;
        int combinedCollisionIndexCapacity = checked(collision.BlockTreeByteCapacity + collision.BlocksByteCapacity);
        if (trianglePatches.Count > 0 &&
            (!TerrainPatchExporter.TryBuildCollisionIndexBytes(
                translatedTriangleTable,
                collision.TriangleCount,
                combinedCollisionIndexCapacity,
                combinedCollisionIndexCapacity,
                out rebuiltIndex,
                out string collisionIndexFailure) || rebuiltIndex == null))
        {
            throw new InvalidOperationException(
                $"{level.DisplayName}'s portal entry moved, but its collision lookup could not be rebuilt: {collisionIndexFailure}");
        }

        foreach (PortalSourceMovement movement in movements)
        {
            AddPortalRecordMovementPatches(stream, layout, level, movement, patches, writtenWadOffsets);
            AddPortalPathMovementPatches(stream, layout, level, movement, patches, writtenWadOffsets);
        }

        foreach ((PortalSourceMovement movement, PortalTriggerTriangle triangle, byte[] after) in trianglePatches)
        {
            AddRawPatch(
                stream,
                layout,
                level,
                triangle.WadOffset,
                after,
                "portal-entry-collision-triangle",
                $"{movement.Label} walk-in trigger",
                movement.Portal.SourcePathMobyTrueIndex,
                $"collision-triangle-{triangle.TriangleIndex}",
                $"Move portal {movement.Portal.Index}'s level-{movement.Portal.DestinationLevelId} walk-in collision triangle {triangle.TriangleIndex} by ({movement.CollisionDeltaX}, {movement.CollisionDeltaY}, {movement.CollisionDeltaZ}) world units.",
                patches,
                writtenWadOffsets);
        }

        if (rebuiltIndex != null)
        {
            long rebuiltBlocksWadOffset = collision.BlocksWadOffset;
            if (rebuiltIndex.TreeBytes.Length > collision.BlockTreeByteCapacity)
                rebuiltBlocksWadOffset = AlignUp(collision.BlockTreeWadOffset + rebuiltIndex.TreeBytes.Length, 4);
            int rebuiltBlockCapacity = checked((int)(collision.TriangleTableWadOffset - rebuiltBlocksWadOffset));
            if (rebuiltBlockCapacity < rebuiltIndex.BlockBytes.Length)
            {
                throw new InvalidOperationException(
                    $"{level.DisplayName}'s moved portal collision index needs {rebuiltIndex.TreeBytes.Length + rebuiltIndex.BlockBytes.Length} bytes, but its native tree/block region has {combinedCollisionIndexCapacity} bytes.");
            }

            long collisionBodyWadOffset = source.CollisionComponentWadOffset + 4;
            int rebuiltBlocksRelativeOffset = checked((int)(rebuiltBlocksWadOffset - collisionBodyWadOffset));
            AddRawPatchIfChanged(
                stream,
                layout,
                level,
                collisionBodyWadOffset + 0x0C,
                BitConverter.GetBytes(rebuiltBlocksRelativeOffset),
                "portal-entry-collision-blocks-pointer",
                "Portal entry collision lookup",
                "collision-header-blocks-pointer",
                $"Point the collision header at the rebalanced block lookup table (+0x{rebuiltBlocksRelativeOffset:X}).",
                patches,
                writtenWadOffsets);
            AddRawPatchIfChanged(
                stream,
                layout,
                level,
                collision.BlockTreeWadOffset,
                rebuiltIndex.TreeBytes,
                "portal-entry-collision-index-tree",
                "Portal entry collision lookup",
                "collision-block-tree",
                "Rebuild the collision block tree so moved portal entry surfaces are found at their edited locations.",
                patches,
                writtenWadOffsets);
            AddRawPatchIfChanged(
                stream,
                layout,
                level,
                rebuiltBlocksWadOffset,
                rebuiltIndex.BlockBytes,
                "portal-entry-collision-index-blocks",
                "Portal entry collision lookup",
                "collision-blocks",
                "Rebuild the collision block lookup so moved portal entry surfaces are found at their edited locations.",
                patches,
                writtenWadOffsets);
        }
    }

    private static void AddPortalRecordMovementPatches(
        FileStream stream,
        DiscLayout layout,
        LevelDefinition level,
        PortalSourceMovement movement,
        List<MobySourcePatch> patches,
        HashSet<long> writtenWadOffsets)
    {
        PortalSourceRecord portal = movement.Portal;
        AddPortalAxisPatch(stream, layout, level, movement, portal.WadOffset + 0x20, "center-x", portal.CenterX, movement.RawDeltaX, patches, writtenWadOffsets);
        AddPortalAxisPatch(stream, layout, level, movement, portal.WadOffset + 0x24, "center-y", portal.CenterY, movement.RawDeltaY, patches, writtenWadOffsets);
        AddPortalAxisPatch(stream, layout, level, movement, portal.WadOffset + 0x28, "center-z", portal.CenterZ, movement.RawDeltaZ, patches, writtenWadOffsets);
        for (int pointIndex = 0; pointIndex < portal.Points.Count; pointIndex++)
        {
            PortalSourcePoint point = portal.Points[pointIndex];
            AddPortalAxisPatch(stream, layout, level, movement, point.WadOffset, $"point-{pointIndex}-x", point.X, movement.RawDeltaX, patches, writtenWadOffsets);
            AddPortalAxisPatch(stream, layout, level, movement, point.WadOffset + 4, $"point-{pointIndex}-y", point.Y, movement.RawDeltaY, patches, writtenWadOffsets);
            AddPortalAxisPatch(stream, layout, level, movement, point.WadOffset + 8, $"point-{pointIndex}-z", point.Z, movement.RawDeltaZ, patches, writtenWadOffsets);
        }
    }

    private static void AddPortalPathMovementPatches(
        FileStream stream,
        DiscLayout layout,
        LevelDefinition level,
        PortalSourceMovement movement,
        List<MobySourcePatch> patches,
        HashSet<long> writtenWadOffsets)
    {
        PortalPathSourceRecord path = movement.Portal.Path;
        if (path.Nodes.Count < 2)
        {
            throw new InvalidOperationException(
                $"{level.DisplayName} portal {movement.Portal.Index} does not have the two native transition nodes required by the level loader.");
        }

        foreach (PortalPathSourceNode node in path.Nodes)
        {
            AddPortalPathAxisPatch(stream, layout, level, movement, node, 0, "x", node.X, movement.RawDeltaX, patches, writtenWadOffsets);
            AddPortalPathAxisPatch(stream, layout, level, movement, node, 4, "y", node.Y, movement.RawDeltaY, patches, writtenWadOffsets);
            AddPortalPathAxisPatch(stream, layout, level, movement, node, 8, "z", node.Z, movement.RawDeltaZ, patches, writtenWadOffsets);
        }
    }

    private static void AddPortalPathAxisPatch(
        FileStream stream,
        DiscLayout layout,
        LevelDefinition level,
        PortalSourceMovement movement,
        PortalPathSourceNode node,
        int axisOffset,
        string axis,
        int original,
        int delta,
        List<MobySourcePatch> patches,
        HashSet<long> writtenWadOffsets)
    {
        if (delta == 0)
            return;

        long wadOffset = node.WadOffset + axisOffset;
        int sourceValue = BitConverter.ToInt32(ReadWadBytes(stream, layout, wadOffset, 4));
        if (sourceValue != original)
        {
            throw new InvalidOperationException(
                $"{level.DisplayName} portal {movement.Portal.Index} path node {node.Index} {axis.ToUpperInvariant()} changed from the decoded source value {original} to {sourceValue}; refusing an unsafe portal move.");
        }

        int edited = checked(original + delta);
        AddRawPatch(
            stream,
            layout,
            level,
            wadOffset,
            BitConverter.GetBytes(edited),
            $"portal-transition-path-node-{node.Index}-{axis}",
            $"{movement.Label} transition path",
            movement.Portal.SourcePathMobyTrueIndex,
            $"portal-{movement.Portal.Index}-path-node-{node.Index}-{axis}",
            $"Move portal {movement.Portal.Index}'s transition path node {node.Index} {axis.ToUpperInvariant()} by raw {delta:+#;-#;0} to {edited} so Spyro enters and exits at the edited doorway.",
            patches,
            writtenWadOffsets);
    }

    private static void AddPortalAxisPatch(
        FileStream stream,
        DiscLayout layout,
        LevelDefinition level,
        PortalSourceMovement movement,
        long wadOffset,
        string field,
        int original,
        int delta,
        List<MobySourcePatch> patches,
        HashSet<long> writtenWadOffsets)
    {
        if (delta == 0)
            return;

        int edited = checked(original + delta);
        AddRawPatch(
            stream,
            layout,
            level,
            wadOffset,
            BitConverter.GetBytes(edited),
            $"portal-plane-{field}",
            movement.Label,
            movement.Portal.SourcePathMobyTrueIndex,
            $"portal-{movement.Portal.Index}-{field}",
            $"Move portal {movement.Portal.Index}'s native {field} by raw {delta:+#;-#;0} to {edited}.",
            patches,
            writtenWadOffsets);
    }

    private static byte[] TranslatePortalTriggerTriangle(
        LevelDefinition level,
        PortalSourceMovement movement,
        PortalTriggerTriangle triangle,
        byte[] triangleTable,
        PortalCollisionSourceLayout collision)
    {
        int tableOffset = checked((int)(triangle.WadOffset - collision.TriangleTableWadOffset));
        if (tableOffset < 0 || tableOffset + 12 > triangleTable.Length || tableOffset % 12 != 0)
            throw new InvalidOperationException($"{level.DisplayName} portal collision triangle {triangle.TriangleIndex} is outside its native table.");

        ValidateTranslatedPortalPoint(level, movement, triangle.TriangleIndex, triangle.P1);
        ValidateTranslatedPortalPoint(level, movement, triangle.TriangleIndex, triangle.P2);
        ValidateTranslatedPortalPoint(level, movement, triangle.TriangleIndex, triangle.P3);

        uint xWord = BinaryPrimitives.ReadUInt32LittleEndian(triangleTable.AsSpan(tableOffset, 4));
        uint yWord = BinaryPrimitives.ReadUInt32LittleEndian(triangleTable.AsSpan(tableOffset + 4, 4));
        uint zWord = BinaryPrimitives.ReadUInt32LittleEndian(triangleTable.AsSpan(tableOffset + 8, 4));
        int translatedX = triangle.P1.X + movement.CollisionDeltaX;
        int translatedY = triangle.P1.Y + movement.CollisionDeltaY;
        int translatedZ = triangle.P1.Z + movement.CollisionDeltaZ;
        xWord = (xWord & 0xFFFFC000u) | (uint)translatedX;
        yWord = (yWord & 0xFFFFC000u) | (uint)translatedY;
        zWord = (zWord & 0xFFFFC000u) | (uint)translatedZ;

        byte[] after = new byte[12];
        BinaryPrimitives.WriteUInt32LittleEndian(after.AsSpan(0, 4), xWord);
        BinaryPrimitives.WriteUInt32LittleEndian(after.AsSpan(4, 4), yWord);
        BinaryPrimitives.WriteUInt32LittleEndian(after.AsSpan(8, 4), zWord);
        return after;
    }

    private static void ValidateTranslatedPortalPoint(
        LevelDefinition level,
        PortalSourceMovement movement,
        int triangleIndex,
        PortalSourcePoint point)
    {
        int x = point.X + movement.CollisionDeltaX;
        int y = point.Y + movement.CollisionDeltaY;
        int z = point.Z + movement.CollisionDeltaZ;
        if (x is < 0 or > 0x3FFF || y is < 0 or > 0x3FFF || z is < 0 or > 0x3FFF)
        {
            throw new InvalidOperationException(
                $"{level.DisplayName} portal {movement.Portal.Index} cannot move to that location because collision triangle {triangleIndex} would leave the native 0..16383 coordinate range.");
        }
    }

    private static void AddRawPatchIfChanged(
        FileStream stream,
        DiscLayout layout,
        LevelDefinition level,
        long wadOffset,
        byte[] after,
        string kind,
        string label,
        string recordOffset,
        string description,
        List<MobySourcePatch> patches,
        HashSet<long> writtenWadOffsets)
    {
        byte[] before = ReadWadBytes(stream, layout, wadOffset, after.Length);
        if (before.SequenceEqual(after))
            return;

        AddRawPatch(
            stream,
            layout,
            level,
            wadOffset,
            after,
            kind,
            label,
            -1,
            recordOffset,
            description,
            patches,
            writtenWadOffsets);
    }

    private static int RawDeltaToCollisionDelta(int rawDelta) =>
        checked((int)Math.Round(rawDelta / 16d, MidpointRounding.AwayFromZero));

    private static long AlignUp(long value, int alignment)
    {
        long mask = alignment - 1L;
        return checked((value + mask) & ~mask);
    }

    private static bool IsDragonRunToEdit(JsonElement edit) =>
        JsonValue.GetString(edit, "editorControlKind")
            .StartsWith(DragonRunToEditStore.ControlPrefix, StringComparison.OrdinalIgnoreCase);

    private static int GetDragonRunToOwnerTrueIndex(JsonElement edit)
    {
        if (TryGetRequiredInt32(edit, "ownerTrueIndex", out int ownerTrueIndex))
            return ownerTrueIndex;

        string controlKind = JsonValue.GetString(edit, "editorControlKind");
        return controlKind.StartsWith(DragonRunToEditStore.ControlPrefix, StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(
                controlKind.AsSpan(DragonRunToEditStore.ControlPrefix.Length),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out ownerTrueIndex)
            ? ownerTrueIndex
            : -1;
    }

    private static bool TryGetRequiredInt32(JsonElement element, string name, out int value)
    {
        value = 0;
        return element.TryGetProperty(name, out JsonElement property) &&
            property.ValueKind == JsonValueKind.Number &&
            property.TryGetInt32(out value);
    }

    private static IReadOnlyDictionary<int, (int RawX, int RawY)> BuildFinalDragonRawPositions(
        JsonElement edits,
        long tableWadOffset,
        FileStream stream,
        DiscLayout layout,
        LevelDefinition level)
    {
        Dictionary<int, (int RawX, int RawY)> result = new();
        foreach (JsonElement edit in edits.EnumerateArray())
        {
            if (IsDragonRunToEdit(edit))
                continue;
            int trueIndex = JsonValue.GetInt32(edit, "trueIndex", -1);
            if (trueIndex < 0 || trueIndex >= level.SourceRecordCount)
                continue;
            byte[] record = ReadWadBytes(stream, layout, tableWadOffset + ((long)trueIndex * RecordStride), RecordStride);
            if (!IsNativeDragonActorRecord(record))
                continue;
            result[trueIndex] = (
                ReadRawAxis(edit, "x", record, XOffset),
                ReadRawAxis(edit, "y", record, YOffset));
        }
        return result;
    }

    private static void AddDragonRunToEndpointPatches(
        FileStream stream,
        DiscLayout layout,
        LevelDefinition level,
        string label,
        JsonElement edit,
        Lazy<IReadOnlyDictionary<int, DragonRescueCameraData>> dragonRescueCameras,
        IReadOnlyDictionary<int, (int RawX, int RawY)> finalDragonRawPositions,
        GeometryCandidate? levelGeometry,
        List<MobySourcePatch> patches,
        HashSet<long> writtenWadOffsets,
        List<string> skippedEdits,
        List<MobySourceEditSafetyFinding> safetyFindings)
    {
        int trueIndex = GetDragonRunToOwnerTrueIndex(edit);
        string controlKind = JsonValue.GetString(edit, "editorControlKind");
        string expectedControlKind = $"{DragonRunToEditStore.ControlPrefix}{trueIndex}";
        string editKind = JsonValue.GetString(edit, "editKind");
        if (trueIndex < 0 ||
            !string.Equals(controlKind, expectedControlKind, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(editKind, "synthetic-control", StringComparison.OrdinalIgnoreCase) ||
            JsonValue.GetBoolean(edit, "added") ||
            JsonValue.GetBoolean(edit, "removed") ||
            edit.TryGetProperty("trueIndex", out _))
        {
            skippedEdits.Add(
                $"{label}: invalid copied/new dragon run-to control; only the original synthetic control for one native dragon scene can be exported.");
            return;
        }
        if (!dragonRescueCameras.Value.TryGetValue(trueIndex, out DragonRescueCameraData? scene))
        {
            skippedEdits.Add($"{label}: native dragon rescue scene T{trueIndex} is not present in the selected disc.");
            return;
        }
        string savedLevelKey = LevelCatalog.NormalizeKey(JsonValue.GetString(edit, "levelKey"));
        if (!string.Equals(savedLevelKey, LevelCatalog.NormalizeKey(level.Key), StringComparison.Ordinal))
        {
            skippedEdits.Add($"{label}: saved run-to level identity no longer matches the selected level.");
            return;
        }
        if (!TryParseWadRelativeOffset(JsonValue.GetString(edit, "cameraDataWadOffset"), out long savedCameraDataWadOffset) ||
            savedCameraDataWadOffset != scene.CameraDataWadOffset)
        {
            skippedEdits.Add($"{label}: saved run-to packed scene address no longer matches the selected disc.");
            return;
        }
        if (!edit.TryGetProperty("rawOriginal", out JsonElement original) || original.ValueKind != JsonValueKind.Object ||
            !edit.TryGetProperty("rawEdited", out JsonElement edited) || edited.ValueKind != JsonValueKind.Object)
        {
            skippedEdits.Add($"{label}: synthetic run-to control is missing original/edited raw XY coordinates.");
            return;
        }
        if (JsonValue.GetInt32(original, "x", int.MinValue) != scene.RunToRawX ||
            JsonValue.GetInt32(original, "y", int.MinValue) != scene.RunToRawY ||
            JsonValue.GetInt32(edit, "originalAngle", int.MinValue) != scene.RunToAngle ||
            JsonValue.GetInt32(edit, "originalRadius", int.MinValue) != scene.RunToRadius ||
            JsonValue.GetInt32(edit, "preservedAuxiliary", int.MinValue) != scene.RunToAuxiliary)
        {
            skippedEdits.Add($"{label}: saved run-to source preimage no longer matches the selected disc.");
            return;
        }

        (int RawX, int RawY) finalDragon = finalDragonRawPositions.TryGetValue(trueIndex, out (int RawX, int RawY) moved)
            ? moved
            : (scene.DragonRawX, scene.DragonRawY);
        if (!TryGetRequiredInt32(edited, "x", out int endpointRawX) ||
            !TryGetRequiredInt32(edited, "y", out int endpointRawY))
        {
            skippedEdits.Add($"{label}: invalid dragon run-to endpoint; edited raw X and Y must both be signed 32-bit integers.");
            return;
        }
        byte[] packed = DiscImage.ReadFileBytes(
            stream,
            layout,
            WadLba,
            scene.CameraDataWadOffset,
            DragonRescueRunTo.PackedPropertiesByteLength);
        DragonRunToPatchPlan plan = DragonRescueRunTo.BuildPatchPlan(
            scene,
            finalDragon.RawX,
            finalDragon.RawY,
            endpointRawX,
            endpointRawY,
            packed);
        if (!plan.CanPatch)
        {
            skippedEdits.Add($"{label}: [{plan.Result.Validation.Code}] {plan.Result.Validation.Message}");
            return;
        }

        if (plan.Result.Validation.Status == MobyBuildSafetyStatus.Review)
        {
            safetyFindings.Add(new MobySourceEditSafetyFinding(
                plan.Result.Validation.Code,
                plan.Result.Validation.Status,
                plan.Result.Validation.Message));
        }

        bool hasTerrainHit = levelGeometry is { Polygons.Count: > 0 } &&
            TerrainSnapper.TryFindZAt(
                levelGeometry.Polygons,
                endpointRawX / (float)SpyroNativeTerrainCamera.CameraPositionScale,
                endpointRawY / (float)SpyroNativeTerrainCamera.CameraPositionScale,
                scene.DragonRawZ / (float)SpyroNativeTerrainCamera.CameraPositionScale,
                out _,
                preferTopSurface: true);
        if (!hasTerrainHit)
        {
            safetyFindings.Add(new MobySourceEditSafetyFinding(
                "dragon-run-to-no-terrain-hit",
                MobyBuildSafetyStatus.Review,
                "The edited run-to endpoint has no source-derived terrain hit; verify Spyro does not run into a void, wall, or non-walkable surface."));
        }

        foreach (MobySourcePatch patch in plan.ToMobySourcePatches(level, label))
        {
            long wadOffset = ParseRequiredLong(patch.WadRelativeOffset, "dragon run-to patch offset");
            if (OverlapsWrittenOffsets(wadOffset, patch.ByteLength, writtenWadOffsets))
                throw new InvalidOperationException($"Dragon run-to patch for T{trueIndex} overlaps another source patch at 0x{wadOffset:X}.");
            for (long offset = wadOffset; offset < wadOffset + patch.ByteLength; offset++)
                writtenWadOffsets.Add(offset);
            patches.Add(patch with
            {
                ImageOffset = $"0x{ConvertWadOffsetToImageOffset(layout, wadOffset):X}"
            });
        }
    }

    private static void AddNativeMobyPathPatches(
        string sourceImagePath,
        LevelDefinition level,
        string nativeMobyPathEditsPath,
        FileStream stream,
        DiscLayout layout,
        List<MobySourcePatch> patches,
        HashSet<long> writtenWadOffsets,
        List<MobySourceEditOutcome> editOutcomes,
        List<string> skippedEdits,
        List<string> notes)
    {
        if (string.IsNullOrWhiteSpace(nativeMobyPathEditsPath) || !File.Exists(nativeMobyPathEditsPath))
            return;

        IReadOnlyList<NativeMobyPath> paths = EggThiefPathLocator.Locate(sourceImagePath, level);
        NativeMobyPathEditLoadResult loaded = NativeMobyPathEditStore.Load(nativeMobyPathEditsPath, paths);
        if (loaded.BlockedReasons.Count > 0)
        {
            IReadOnlyList<NativeMobyPathBlockedEdit> targeted = loaded.TargetedBlockedEdits;
            if (targeted.Count > 0)
            {
                foreach (NativeMobyPathBlockedEdit blocked in targeted)
                    AddBlockedNativePathOutcome(blocked.OwnerTrueIndex, blocked.Reason);
            }
            else
            {
                foreach (string reason in loaded.BlockedReasons)
                    AddBlockedNativePathOutcome(TryParseMovementOwnerTrueIndex(reason), reason);
            }

            void AddBlockedNativePathOutcome(int ownerTrueIndex, string reason)
            {
                string label = ownerTrueIndex >= 0 ? $"Egg thief T{ownerTrueIndex}" : "Egg thief path";
                string skipped = $"{label}: saved egg-thief path no longer matches the selected disc: {reason}";
                skippedEdits.Add(skipped);
                editOutcomes.Add(new MobySourceEditOutcome(
                    ownerTrueIndex,
                    label,
                    "native-path",
                    Array.Empty<string>(),
                    [skipped],
                    Array.Empty<MobySourceEditPackageOutcome>()));
            }
        }
        NativeMobyPathPatchPlan plan = NativeMobyPathPatchExporter.BuildPlan(paths);
        foreach (NativeMobyPathCoordinatePatch coordinate in plan.Patches)
        {
            if (coordinate.WadLba != WadLba)
                throw new InvalidOperationException($"{coordinate.LevelName} T{coordinate.OwnerTrueIndex} path targets an unexpected WAD LBA.");
            if (OverlapsWrittenOffsets(coordinate.WadOffset, coordinate.ByteLength, writtenWadOffsets))
                throw new InvalidOperationException($"Native path node T{coordinate.OwnerTrueIndex}/#{coordinate.NodeIndex + 1} overlaps another source patch.");
            byte[] actual = ReadWadBytes(stream, layout, coordinate.WadOffset, coordinate.ByteLength);
            if (!actual.SequenceEqual(coordinate.Before))
            {
                throw new InvalidOperationException(
                    $"{coordinate.LevelName} T{coordinate.OwnerTrueIndex} path node {coordinate.NodeIndex + 1} source XYZ bytes are stale.");
            }
            for (long offset = coordinate.WadOffset; offset < coordinate.WadOffset + coordinate.ByteLength; offset++)
                writtenWadOffsets.Add(offset);
            patches.Add(new MobySourcePatch(
                Label: $"{coordinate.LevelKey}-T{coordinate.OwnerTrueIndex}-path-node-{coordinate.NodeIndex + 1}",
                Kind: "native-moby-path-node-xyz",
                LevelKey: coordinate.LevelKey,
                MobyLabel: $"Egg thief T{coordinate.OwnerTrueIndex}",
                TrueIndex: coordinate.OwnerTrueIndex,
                RecordOffset: $"PathData node {coordinate.NodeIndex + 1} XYZ",
                WadRelativeOffset: $"0x{coordinate.WadOffset:X}",
                ImageOffset: $"0x{ConvertWadOffsetToImageOffset(layout, coordinate.WadOffset):X}",
                ByteLength: coordinate.ByteLength,
                BeforeHexPreview: ToHex(coordinate.Before),
                AfterHexPreview: ToHex(coordinate.After),
                Description: $"Move native egg-thief route node {coordinate.NodeIndex + 1}; preserve PathData header/order and unknown word."));
        }

        foreach (IGrouping<int, NativeMobyPathCoordinatePatch> owner in plan.Patches.GroupBy(patch => patch.OwnerTrueIndex))
        {
            editOutcomes.Add(new MobySourceEditOutcome(
                owner.Key,
                $"Egg thief T{owner.Key}",
                "native-path",
                owner.Select(_ => "native-moby-path-node-xyz").ToArray(),
                Array.Empty<string>(),
                Array.Empty<MobySourceEditPackageOutcome>()));
        }
        if (plan.Patches.Count > 0)
        {
            notes.Add($"Native egg-thief paths: {plan.EditedPathCount} route(s), {plan.EditedNodeCount} node(s); each write is exactly 12 XYZ bytes and preserves fixed headers/order/unknown words.");
        }
    }

    private static int TryParseMovementOwnerTrueIndex(string text)
    {
        int marker = text.IndexOf(" T", StringComparison.Ordinal);
        if (marker < 0)
            return -1;
        int start = marker + 2;
        int end = start;
        while (end < text.Length && char.IsAsciiDigit(text[end]))
            end++;
        return end > start && int.TryParse(text.AsSpan(start, end - start), out int value)
            ? value
            : -1;
    }

    private static void AddMovedDragonRescueCameraPatches(
        FileStream stream,
        DiscLayout layout,
        LevelDefinition level,
        long tableWadOffset,
        int trueIndex,
        string label,
        JsonElement edit,
        Lazy<IReadOnlyDictionary<int, DragonRescueCameraData>> dragonRescueCameras,
        List<MobySourcePatch> patches,
        HashSet<long> writtenWadOffsets)
    {
        if (!edit.TryGetProperty("rawOriginal", out JsonElement rawOriginal) || rawOriginal.ValueKind != JsonValueKind.Object ||
            !edit.TryGetProperty("rawEdited", out JsonElement rawEdited) || rawEdited.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        int originalX = JsonValue.GetInt32(rawOriginal, "x");
        int originalY = JsonValue.GetInt32(rawOriginal, "y");
        int originalZ = JsonValue.GetInt32(rawOriginal, "z");
        int deltaX = checked(JsonValue.GetInt32(rawEdited, "x", originalX) - originalX);
        int deltaY = checked(JsonValue.GetInt32(rawEdited, "y", originalY) - originalY);
        int deltaZ = checked(JsonValue.GetInt32(rawEdited, "z", originalZ) - originalZ);
        if (deltaX == 0 && deltaY == 0 && deltaZ == 0)
            return;

        byte[] sourceRecord = ReadWadBytes(stream, layout, tableWadOffset + ((long)trueIndex * RecordStride), RecordStride);
        if (!IsNativeDragonActorRecord(sourceRecord))
            return;

        int editedType = JsonValue.GetInt32(edit, "typeEditedHex", sourceRecord[TypeOffset]);
        int editedSourceByte36 = JsonValue.GetInt32(edit, "sourceByte36EditedHex", sourceRecord[0x36]);
        if (editedType is not (0x20 or 0x3C) || editedSourceByte36 != 0xFA)
            return;

        if (!dragonRescueCameras.Value.TryGetValue(trueIndex, out DragonRescueCameraData? camera))
        {
            throw new InvalidOperationException(
                $"{level.DisplayName} dragon T{trueIndex} does not have one proven packed rescue-camera record.");
        }

        AddDragonCameraAxisPatch(stream, layout, level, trueIndex, label, camera.CameraDataWadOffset, 0x00, "x", camera.CameraRawX, deltaX, patches, writtenWadOffsets);
        AddDragonCameraAxisPatch(stream, layout, level, trueIndex, label, camera.CameraDataWadOffset, 0x04, "y", camera.CameraRawY, deltaY, patches, writtenWadOffsets);
        AddDragonCameraAxisPatch(stream, layout, level, trueIndex, label, camera.CameraDataWadOffset, 0x08, "z", camera.CameraRawZ, deltaZ, patches, writtenWadOffsets);
        AddDragonCutsceneCameraTrackPatch(
            stream,
            layout,
            level,
            trueIndex,
            label,
            camera,
            deltaX,
            deltaY,
            deltaZ,
            patches,
            writtenWadOffsets);
    }

    private static void AddDragonCameraAxisPatch(
        FileStream stream,
        DiscLayout layout,
        LevelDefinition level,
        int trueIndex,
        string label,
        long cameraDataWadOffset,
        int cameraFieldOffset,
        string axis,
        int originalCameraValue,
        int delta,
        List<MobySourcePatch> patches,
        HashSet<long> writtenWadOffsets)
    {
        if (delta == 0)
            return;

        int editedCameraValue = checked(originalCameraValue + delta);
        AddRawPatch(
            stream,
            layout,
            level,
            cameraDataWadOffset + cameraFieldOffset,
            BitConverter.GetBytes(editedCameraValue),
            $"dragon-rescue-camera-position-{axis}",
            $"{label} rescue camera",
            trueIndex,
            $"camera+0x{cameraFieldOffset:X}",
            $"Move {label}'s rescue camera {axis.ToUpperInvariant()} by raw {delta:+#;-#;0} to {editedCameraValue}, preserving the native shot framing and angles.",
            patches,
            writtenWadOffsets);
    }

    private static void AddDragonCutsceneCameraTrackPatch(
        FileStream stream,
        DiscLayout layout,
        LevelDefinition level,
        int trueIndex,
        string label,
        DragonRescueCameraData camera,
        int deltaX,
        int deltaY,
        int deltaZ,
        List<MobySourcePatch> patches,
        HashSet<long> writtenWadOffsets)
    {
        byte[] cameraTrack = ReadWadBytes(
            stream,
            layout,
            camera.CutsceneCameraTrackWadOffset,
            camera.CutsceneCameraTrackByteLength);
        for (int frameOffset = 0; frameOffset < cameraTrack.Length; frameOffset += 0x18)
        {
            WriteInt32(cameraTrack, frameOffset, checked(BitConverter.ToInt32(cameraTrack, frameOffset) + deltaX));
            WriteInt32(cameraTrack, frameOffset + 4, checked(BitConverter.ToInt32(cameraTrack, frameOffset + 4) + deltaY));
            WriteInt32(cameraTrack, frameOffset + 8, checked(BitConverter.ToInt32(cameraTrack, frameOffset + 8) + deltaZ));
        }

        AddRawPatch(
            stream,
            layout,
            level,
            camera.CutsceneCameraTrackWadOffset,
            cameraTrack,
            "dragon-rescue-cutscene-camera-track",
            $"{label} rescue cinematic",
            trueIndex,
            $"cutscene-{camera.CutsceneIndex}-camera-track",
            $"Translate all {camera.CutsceneCameraFrameCount} frames of {label}'s later rescue cinematic by raw ({deltaX:+#;-#;0}, {deltaY:+#;-#;0}, {deltaZ:+#;-#;0}), preserving camera angles and timing.",
            patches,
            writtenWadOffsets);
    }

    private static bool IsNativeDragonActorRecord(byte[] record)
    {
        return record[TypeOffset] is 0x20 or 0x3C &&
            record[0x36] == 0xFA &&
            record[0x37] == 0x00 &&
            record[0x4F] == 0x00 &&
            record[0x52] == 0x10 &&
            record[0x53] == 0xFF;
    }

    private static void AddCoordinatePatches(
        FileStream stream,
        DiscLayout layout,
        LevelDefinition level,
        long tableWadOffset,
        int trueIndex,
        string label,
        JsonElement edit,
        List<MobySourcePatch> patches,
        HashSet<long> writtenWadOffsets)
    {
        if (!edit.TryGetProperty("rawEdited", out JsonElement rawEdited) || rawEdited.ValueKind != JsonValueKind.Object)
            return;

        bool hasOriginal = edit.TryGetProperty("rawOriginal", out JsonElement rawOriginal) && rawOriginal.ValueKind == JsonValueKind.Object;
        AddCoordinateAxisPatch(stream, layout, level, tableWadOffset, trueIndex, label, "x", XOffset, rawEdited, hasOriginal ? rawOriginal : default, hasOriginal, patches, writtenWadOffsets);
        AddCoordinateAxisPatch(stream, layout, level, tableWadOffset, trueIndex, label, "y", YOffset, rawEdited, hasOriginal ? rawOriginal : default, hasOriginal, patches, writtenWadOffsets);
        AddCoordinateAxisPatch(stream, layout, level, tableWadOffset, trueIndex, label, "z", ZOffset, rawEdited, hasOriginal ? rawOriginal : default, hasOriginal, patches, writtenWadOffsets);
    }

    private static void AddCoordinateAxisPatch(
        FileStream stream,
        DiscLayout layout,
        LevelDefinition level,
        long tableWadOffset,
        int trueIndex,
        string label,
        string axis,
        int fieldOffset,
        JsonElement rawEdited,
        JsonElement rawOriginal,
        bool hasOriginal,
        List<MobySourcePatch> patches,
        HashSet<long> writtenWadOffsets)
    {
        if (!rawEdited.TryGetProperty(axis, out _))
            return;

        int edited = JsonValue.GetInt32(rawEdited, axis);
        if (hasOriginal && edited == JsonValue.GetInt32(rawOriginal, axis, edited))
            return;

        AddPatch(
            stream,
            layout,
            level,
            tableWadOffset,
            trueIndex,
            fieldOffset,
            BitConverter.GetBytes(edited),
            $"moby-position-{axis}",
            label,
            $"Move {label} {axis.ToUpperInvariant()} to raw {edited}.",
            patches,
            writtenWadOffsets);
    }

    private static void AddChangedBytePatch(
        FileStream stream,
        DiscLayout layout,
        LevelDefinition level,
        long tableWadOffset,
        int trueIndex,
        string label,
        string field,
        int fieldOffset,
        JsonElement edit,
        string originalName,
        string editedName,
        List<MobySourcePatch> patches,
        HashSet<long> writtenWadOffsets)
    {
        int edited = JsonValue.GetInt32(edit, editedName, -1);
        if (edited < 0)
            return;

        int original = JsonValue.GetInt32(edit, originalName, edited);
        if (edited == original)
            return;

        AddPatch(
            stream,
            layout,
            level,
            tableWadOffset,
            trueIndex,
            fieldOffset,
            [(byte)Math.Clamp(edited, 0, 255)],
            $"moby-{field}",
            label,
            $"Set {label} {field} to 0x{edited:X2}.",
            patches,
            writtenWadOffsets);
    }

    private static void AddYawPatches(
        FileStream stream,
        DiscLayout layout,
        LevelDefinition level,
        long tableWadOffset,
        int trueIndex,
        string label,
        JsonElement edit,
        List<MobySourcePatch> patches,
        HashSet<long> writtenWadOffsets)
    {
        int edited = ReadYawByteFromEdit(edit, -1);
        if (edited < 0)
            return;

        int original = JsonValue.GetInt32(edit, "yawByteOriginalHex", edited);
        if (edited == original)
            return;

        AddPatch(
            stream,
            layout,
            level,
            tableWadOffset,
            trueIndex,
            YawMatrixOffset,
            Moby.YawByteToMatrixBytes(edited),
            "moby-yaw-facing-matrix",
            label,
            $"Set {label} yaw/facing matrix to {Moby.YawByteToDegrees(edited):0.#} degrees.",
            patches,
            writtenWadOffsets);
        AddPatch(
            stream,
            layout,
            level,
            tableWadOffset,
            trueIndex,
            YawByteOffset,
            [(byte)Math.Clamp(edited, 0, 255)],
            "moby-yaw-facing-byte",
            label,
            $"Set {label} legacy yaw/facing byte to 0x{edited:X2}.",
            patches,
            writtenWadOffsets);
    }

    private static void AddSourceByteEdits(
        FileStream stream,
        DiscLayout layout,
        LevelDefinition level,
        long tableWadOffset,
        int trueIndex,
        string label,
        JsonElement edit,
        List<MobySourcePatch> patches,
        HashSet<long> writtenWadOffsets)
    {
        if (!edit.TryGetProperty("sourceByteEdits", out JsonElement sourceByteEdits) || sourceByteEdits.ValueKind != JsonValueKind.Array)
            return;

        foreach (JsonElement byteEdit in sourceByteEdits.EnumerateArray())
        {
            int offset = JsonValue.GetInt32(byteEdit, "offset", JsonValue.GetInt32(byteEdit, "offsetHex", -1));
            int value = JsonValue.GetInt32(byteEdit, "value", JsonValue.GetInt32(byteEdit, "valueHex", -1));
            if (offset < 0 || offset >= RecordStride || value < 0 || value > 255)
                continue;

            string field = JsonValue.GetString(byteEdit, "field", "source-byte");
            AddPatch(
                stream,
                layout,
                level,
                tableWadOffset,
                trueIndex,
                offset,
                [(byte)value],
                $"moby-{field}",
                label,
                $"Set {label} {field} at +0x{offset:X2} to 0x{value:X2}.",
                patches,
                writtenWadOffsets);
        }
    }

    private static void AddPatch(
        FileStream stream,
        DiscLayout layout,
        LevelDefinition level,
        long tableWadOffset,
        int trueIndex,
        int fieldOffset,
        byte[] after,
        string kind,
        string label,
        string description,
        List<MobySourcePatch> patches,
        HashSet<long> writtenWadOffsets)
    {
        long wadOffset = tableWadOffset + (trueIndex * RecordStride) + fieldOffset;
        AddRawPatch(
            stream,
            layout,
            level,
            wadOffset,
            after,
            kind,
            label,
            trueIndex,
            $"0x{fieldOffset:X}",
            description,
            patches,
            writtenWadOffsets);
    }

    private static void AddRawPatch(
        FileStream stream,
        DiscLayout layout,
        LevelDefinition level,
        long wadOffset,
        byte[] after,
        string kind,
        string label,
        int trueIndex,
        string recordOffset,
        string description,
        List<MobySourcePatch> patches,
        HashSet<long> writtenWadOffsets)
    {
        if (OverlapsWrittenOffsets(wadOffset, after.Length, writtenWadOffsets))
            throw new InvalidOperationException($"More than one moby source patch targets WAD offset range 0x{wadOffset:X}+0x{after.Length:X}.");

        byte[] before = ReadWadBytes(stream, layout, wadOffset, after.Length);
        for (long offset = wadOffset; offset < wadOffset + after.Length; offset++)
            writtenWadOffsets.Add(offset);

        patches.Add(new MobySourcePatch(
            Label: trueIndex >= 0
                ? $"{level.Key}-T{trueIndex}-{kind}-{recordOffset}"
                : $"{level.Key}-{kind}-{recordOffset}",
            Kind: kind,
            LevelKey: level.Key,
            MobyLabel: label,
            TrueIndex: trueIndex,
            RecordOffset: recordOffset,
            WadRelativeOffset: $"0x{wadOffset:X}",
            ImageOffset: $"0x{ConvertWadOffsetToImageOffset(layout, wadOffset):X}",
            ByteLength: after.Length,
            BeforeHexPreview: ToHex(before),
            AfterHexPreview: ToHex(after),
            Description: description));
    }

    private static int ReadRawAxis(JsonElement edit, string axis, byte[] fallbackRecord, int fallbackOffset)
    {
        if (edit.TryGetProperty("rawEdited", out JsonElement rawEdited)
            && rawEdited.ValueKind == JsonValueKind.Object
            && rawEdited.TryGetProperty(axis, out _))
            return JsonValue.GetInt32(rawEdited, axis);

        if (edit.TryGetProperty("edited", out JsonElement edited)
            && edited.ValueKind == JsonValueKind.Object
            && edited.TryGetProperty(axis, out _))
            return (int)Math.Round(JsonValue.GetSingle(edited, axis) * 16f);

        return BitConverter.ToInt32(fallbackRecord, fallbackOffset);
    }

    private static void WriteInt32(byte[] bytes, int offset, int value)
    {
        byte[] raw = BitConverter.GetBytes(value);
        Array.Copy(raw, 0, bytes, offset, raw.Length);
    }

    private static void WriteByteFromEdit(byte[] bytes, int offset, JsonElement edit, string primaryName, string fallbackName)
    {
        int value = JsonValue.GetInt32(edit, primaryName, JsonValue.GetInt32(edit, fallbackName, -1));
        if (value >= 0)
            bytes[offset] = (byte)Math.Clamp(value, 0, 255);
    }

    private static void WriteYawFromEdit(byte[] bytes, JsonElement edit)
    {
        int yawByte = ReadYawByteFromEdit(edit, -1);
        if (yawByte < 0)
            return;

        byte[] matrixBytes = Moby.YawByteToMatrixBytes(yawByte);
        Array.Copy(matrixBytes, 0, bytes, YawMatrixOffset, matrixBytes.Length);
        bytes[YawByteOffset] = (byte)Math.Clamp(yawByte, 0, 255);
    }

    private static int ReadYawByteFromEdit(JsonElement edit, int fallback)
    {
        return JsonValue.GetInt32(
            edit,
            "yawByteEditedHex",
            JsonValue.GetInt32(
                edit,
                "yawByteHex",
                JsonValue.GetInt32(edit, "facingByteHex", fallback)));
    }

    private static void ApplySourceByteEdits(byte[] bytes, JsonElement edit)
    {
        if (!edit.TryGetProperty("sourceByteEdits", out JsonElement sourceByteEdits) || sourceByteEdits.ValueKind != JsonValueKind.Array)
            return;

        foreach (JsonElement byteEdit in sourceByteEdits.EnumerateArray())
        {
            int offset = JsonValue.GetInt32(byteEdit, "offset", JsonValue.GetInt32(byteEdit, "offsetHex", -1));
            int value = JsonValue.GetInt32(byteEdit, "value", JsonValue.GetInt32(byteEdit, "valueHex", -1));
            if (offset >= 0 && offset < bytes.Length && value >= 0 && value <= 255)
                bytes[offset] = (byte)value;
        }
    }

    private static int GemIdByteValue(int id)
    {
        return id switch
        {
            0x53 => 1,
            0x54 => 2,
            0x55 => 5,
            0x56 => 10,
            0x57 => 25,
            _ => 0
        };
    }

    private static byte[] ReadWadBytes(FileStream stream, DiscLayout layout, long wadOffset, int length)
    {
        byte[] result = new byte[length];
        int remaining = length;
        int written = 0;
        long absolute = wadOffset;
        while (remaining > 0)
        {
            int sectorOffset = (int)(absolute % 2048);
            int toRead = Math.Min(2048 - sectorOffset, remaining);
            stream.Position = ConvertWadOffsetToImageOffset(layout, absolute);
            int read = stream.Read(result, written, toRead);
            if (read != toRead)
                throw new EndOfStreamException("Could not read WAD bytes.");

            written += toRead;
            remaining -= toRead;
            absolute += toRead;
        }

        return result;
    }

    private static long ConvertWadOffsetToImageOffset(DiscLayout layout, long wadOffset)
    {
        long sector = WadLba + (long)Math.Floor(wadOffset / 2048d);
        long sectorOffset = wadOffset % 2048;
        return (sector * layout.SectorSize) + layout.UserOffset + sectorOffset;
    }

    private static long ParseRequiredLong(string text, string field)
    {
        text = (text ?? "").Trim();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            && long.TryParse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long hex))
            return hex;

        if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed))
            return parsed;

        throw new InvalidOperationException($"Could not parse {field}: {text}");
    }

    private static bool TryParseWadRelativeOffset(string text, out long value)
    {
        value = 0;
        text = (text ?? "").Trim();
        if (text.StartsWith("exe:", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(text))
            return false;
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return long.TryParse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);

        return long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryParseExeRuntimeAddress(string text, out uint value)
    {
        value = 0;
        text = (text ?? "").Trim();
        if (!text.StartsWith("exe:", StringComparison.OrdinalIgnoreCase))
            return false;

        string addressText = text[4..].Trim();
        if (addressText.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            addressText = addressText[2..];

        return uint.TryParse(addressText, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
    }

    private static DiscFileRecord FindExecutable(FileStream stream, DiscLayout layout)
    {
        return DiscImage.FindRootFileRecord(stream, layout, name =>
            name.StartsWith("SCUS", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("SCES", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("SCPS", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("SLUS", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("SLES", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("SLPS", StringComparison.OrdinalIgnoreCase));
    }

    private static long ExeFileOffset(uint runtimeAddress, string source)
    {
        if (runtimeAddress < ExeDestination)
            throw new InvalidOperationException($"Executable patch address is outside the loaded EXE range: {source}");

        return 0x800 + ((long)runtimeAddress - ExeDestination);
    }

    private static string ToHex(byte[] bytes) => string.Join(" ", bytes.Select(value => $"{value:X2}"));

    private static string ToHexPreview(byte[] bytes, int limit = 32)
    {
        string suffix = bytes.Length > limit ? " ..." : "";
        return string.Join(" ", bytes.Take(limit).Select(value => $"{value:X2}")) + suffix;
    }

    private static string HexOffset(long value) => $"0x{value:X}";

    private static byte[] HexToBytes(string text)
    {
        string[] parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Select(part => Convert.ToByte(part, 16)).ToArray();
    }

    private static string ToSafeSlug(string value)
    {
        string slug = new string((value ?? "").ToLowerInvariant().Select(ch => char.IsAsciiLetterOrDigit(ch) ? ch : '-').ToArray()).Trim('-');
        while (slug.Contains("--", StringComparison.Ordinal))
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        return string.IsNullOrWhiteSpace(slug) ? "level" : slug;
    }

    private static string FindCatalogRoot(string startDirectory)
    {
        DirectoryInfo? directory = new(startDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "spyro-level-catalog.json")) ||
                File.Exists(Path.Combine(directory.FullName, "support", "spyro-level-catalog.json")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }

        return startDirectory;
    }
}

public sealed record MobySourcePatchRequest(
    string SourceImagePath,
    string SourceCuePath,
    string OutputPrefix,
    LevelDefinition Level,
    string NativeEditsPath,
    bool WriteImage,
    bool AllowPlanOnlyActorPackageImports = false,
    bool AllowGuardedNativeCloneAppend = false,
    string NativeMobyPathEditsPath = "");

internal sealed record CrossLevelAppendDonor(
    LevelDefinition SourceLevel,
    long SourceTableWadOffset,
    long SourceTableRelativeOffset,
    int SourceTrueIndex,
    string RecipeMode);

internal sealed record CrossLevelSharedSpecialCluster(
    uint SourceBaseOffset,
    uint TargetBaseOffset,
    int Length);

internal sealed record SpringChestAppendAnchor(
    int SourceTrueIndex,
    int TargetTrueIndex,
    int RawX,
    int RawY,
    int RawZ);

internal sealed record PortalSourceMovement(
    PortalSourceRecord Portal,
    string Label,
    int RawDeltaX,
    int RawDeltaY,
    int RawDeltaZ,
    int CollisionDeltaX,
    int CollisionDeltaY,
    int CollisionDeltaZ);

public sealed record MobySourcePatchResult(
    string OutputImagePath,
    string OutputCuePath,
    string OutputPlanPath,
    MobySourcePatchPlan Plan,
    bool WroteImage);

public sealed record MobySourcePatchPlan(
    DateTimeOffset GeneratedAt,
    string SourceImagePath,
    string OutputImagePath,
    string OutputCuePath,
    string NativeEditsPath,
    string LevelKey,
    string LevelName,
    string SourceTableWadOffset,
    int SourceRecordCount,
    int RecordStride,
    int PatchCount,
    int TotalPatchedBytes,
    IReadOnlyList<MobySourcePatch> Patches,
    IReadOnlyList<MobyActorPackageImportPreview> PackageImportPreviews,
    IReadOnlyList<string> SkippedEdits,
    IReadOnlyList<string> Notes,
    IReadOnlyList<MobySourceEditOutcome>? EditOutcomes = null,
    ArtisansNativeLockedChestRuntimeBundleIntent? ArtisansNativeLockedChestRuntimeBundle = null);

public sealed record MobySourceEditOutcome(
    int EditorTrueIndex,
    string MobyLabel,
    string EditKind,
    IReadOnlyList<string> PatchKinds,
    IReadOnlyList<string> SkippedReasons,
    IReadOnlyList<MobySourceEditPackageOutcome> PackageOutcomes,
    IReadOnlyList<MobySourceEditSafetyFinding>? SafetyFindings = null);

public sealed record MobySourceEditSafetyFinding(
    string Code,
    MobyBuildSafetyStatus Status,
    string Message);

public sealed record MobySourceEditPackageOutcome(
    string TemplateId,
    string RecipeId,
    string RecipeStatus,
    bool CanWriteImage,
    string GuardReason);

public sealed record MobySourcePatch(
    string Label,
    string Kind,
    string LevelKey,
    string MobyLabel,
    int TrueIndex,
    string RecordOffset,
    string WadRelativeOffset,
    string ImageOffset,
    int ByteLength,
    string BeforeHexPreview,
    string AfterHexPreview,
    string Description);

public sealed record MobyActorPackageImportPreview(
    string Label,
    string TemplateId,
    string TargetLevelKey,
    string SourceLevelKey,
    string Family,
    string RecipeId,
    string RecipeMode,
    string RecipeStatus,
    bool CanWriteImage,
    string GuardReason,
    IReadOnlyList<MobyActorPackageCopyPreview> CopySegments,
    IReadOnlyList<MobyActorPackageRootPreview> RootEntries)
{
    public static MobyActorPackageImportPreview Unmapped(
        string Label,
        string TemplateId,
        string TargetLevelKey,
        string SourceLevelKey,
        string Family,
        string GuardReason)
    {
        return new MobyActorPackageImportPreview(
            Label,
            TemplateId,
            TargetLevelKey,
            SourceLevelKey,
            Family,
            RecipeId: "",
            RecipeMode: "",
            RecipeStatus: "unmapped",
            CanWriteImage: false,
            GuardReason,
            CopySegments: [],
            RootEntries: []);
    }
}

public sealed record MobyActorPackageCopyPreview(
    string SourceStart,
    string TargetStart,
    string SourceWadRelativeOffset,
    string TargetWadRelativeOffset,
    string SourceImageOffset,
    string TargetImageOffset,
    int ByteLength,
    bool TargetBeforeIsZero,
    string TargetSafety,
    string BeforeHexPreview,
    string AfterHexPreview);

public sealed record MobyActorPackageRootPreview(
    string Operation,
    string TargetRootSlot,
    int RootIndex,
    string TargetRoot,
    string TargetActorId,
    string ExistingRoot,
    string ExistingActorId,
    string RootListWadRelativeOffset,
    string ActorIdListWadRelativeOffset,
    string RootListImageOffset,
    string ActorIdListImageOffset,
    bool RootSlotSafe,
    bool ActorIdSlotSafe,
    string Note);

internal sealed record SpecialDataEntry(uint Offset, int Type);

internal sealed record LevelSceneSourceLayout(long WadBaseOffset, uint ByteLength);

internal sealed record SpringChestRewardRowSet(
    SpringChestAppendAnchor Anchor,
    int SourceSpringTrueIndex,
    IReadOnlyList<int> RewardSourceTrueIndices);

internal sealed record SpecialDataAllocator(long WadBaseOffset, uint TableRelativeOffset, uint InitialNextSearchOffset, IReadOnlyDictionary<uint, int> Lengths)
{
    public uint NextSearchOffset { get; set; } = InitialNextSearchOffset;
}
