using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Exporting;

public sealed record ToastyNativeLockedChestRuntimeBundleCandidateIntent(
    string RecipeId,
    string RequiredExporterFeature,
    string TargetLevelKey,
    string KeyTemplateId,
    int KeyEditorTrueIndex,
    string KeyLabel,
    int KeyRawX,
    int KeyRawY,
    int KeyRawZ,
    int KeyYawByte,
    string LockedChestTemplateId,
    int LockedChestEditorTrueIndex,
    string LockedChestLabel,
    int LockedChestRawX,
    int LockedChestRawY,
    int LockedChestRawZ,
    int LockedChestYawByte,
    int KeyOutputTrueIndex,
    int LockedChestOutputTrueIndex,
    IReadOnlyList<int> RewardMarkerOutputTrueIndices,
    int SourceRuntimeCountBefore,
    int SourceRuntimeCountAfter,
    int TreasureTargetBefore,
    int TreasureTargetAfter);

public sealed record ToastyNativeLockedChestRuntimeBundleCandidateRequest(
    string SourceImagePath,
    string SourceCuePath,
    string OutputPrefix,
    string WadAnalysisPath,
    string LevelCatalogRootPath,
    ToastyNativeLockedChestRuntimeBundleCandidateIntent Intent,
    bool WriteImage = true);

public sealed record ToastyNativeLockedChestRuntimeBundleCandidatePlan(
    DateTimeOffset GeneratedAt,
    string RecipeId,
    string RequiredExporterFeature,
    string TargetLevelKey,
    bool CandidatePlanOnly,
    bool RegisteredForNormalCreateBin,
    string SourceImagePath,
    string OutputImagePath,
    string OutputCuePath,
    string OutputChecklistPath,
    bool InstalledFromNoFastEntryBaseline,
    bool FastEntryEnabled,
    int WadSizeBefore,
    int WadSizeAfter,
    int ExecutableLbaBefore,
    int ExecutableLbaAfter,
    int KeyEditorTrueIndex,
    int KeyOutputTrueIndex,
    int KeyRawX,
    int KeyRawY,
    int KeyRawZ,
    int KeyYawByte,
    int LockedChestEditorTrueIndex,
    int LockedChestOutputTrueIndex,
    int LockedChestRawX,
    int LockedChestRawY,
    int LockedChestRawZ,
    int LockedChestYawByte,
    IReadOnlyList<int> RewardMarkerOutputTrueIndices,
    int SourceRuntimeCountBefore,
    int SourceRuntimeCountAfter,
    int TreasureTargetBefore,
    int TreasureTargetAfter,
    int TranslatedRewardRowCount,
    int TranslatedRewardPropsCount,
    int ChangedDataByteCount,
    string NoFastEntryBaselineOutputSha256,
    string FinalDataEntrySha256,
    string FinalOverlaySha256,
    string FinalExecutableSha256,
    string OutputImageSha256,
    string Verification);

public sealed record ToastyNativeLockedChestRuntimeBundleCandidateResult(
    string OutputImagePath,
    string OutputCuePath,
    string OutputPlanPath,
    string OutputChecklistPath,
    ToastyNativeLockedChestRuntimeBundleCandidatePlan Plan,
    bool WroteImage,
    bool Verified);

/// <summary>
/// Unregistered promotion candidate for Toasty's checked Key + Locked Chest transplant.
/// It installs the deterministic no-fast-entry research baseline, then applies only saved
/// editor XYZ/yaw intent for the visible pair and chest-relative XYZ for all five hidden
/// reward source rows and props. It is intentionally not referenced by the runtime profile
/// catalog or normal Create BIN dispatcher until the fixed DuckStation checklist passes.
/// </summary>
public static class ToastyNativeLockedChestRuntimeBundleCandidateComposer
{
    public const string RecipeId = ToastyNativeLockedChestCandidateExporter.RecipeId;
    public const string RequiredExporterFeature = "ToastyNativeLockedChestRuntimeBundleCandidateV1";
    public const string TargetLevelKey = "toasty";
    public const string KeyTemplateId = "common.key.peacekeepers.t78";
    public const string LockedChestTemplateId = "common.locked_chest.peacekeepers.t79";
    public const int KeyOutputTrueIndex = 59;
    public const int LockedChestOutputTrueIndex = 60;
    public const int SourceRuntimeCountBefore = 59;
    public const int SourceRuntimeCountAfter = 66;
    public const int TreasureTargetBefore = 100;
    public const int TreasureTargetAfter = 110;
    public const bool RegisteredForNormalCreateBin = false;

    private const int WadLba = 37;
    private const int RetailWadSize = 0x6927000;
    private const int ExpandedWadSize = 0x6929000;
    private const int RetailExecutableLba = 53875;
    private const int RelocatedExecutableLba = 53879;
    private const int ExecutableSize = 0x66000;
    private const int OverlayEntryIndex = 17;
    private const long ExpandedOverlayOffset = 0x1480000;
    private const int ExpandedOverlaySize = 0xA800;
    private const int DataEntryIndex = 18;
    private const long ExpandedDataOffset = 0x148A800;
    private const int ExpandedDataSize = 0x21F800;
    private const int ExpandedSceneOffset = 0x1B8800;
    private const int SourceCountEntryOffset = ExpandedSceneOffset + 0x6298;
    private const int SourceTableEntryOffset = ExpandedSceneOffset + 0x629C;
    private const int ScenePointerFixupCountEntryOffset = ExpandedSceneOffset + 0xD314;
    private const int ScenePointerFixupCountAfter = 0x69;
    private const int RecordStride = 0x58;
    private const int YawByteOffset = 0x46;
    private const int FastEntryFileOffset = 0x1E034;

    private const string ExpectedBaselineHandlerSha256 = "d43541718b37b104fa409648e6f355af3d8ac51032d15dda50dde5b27189ff87";
    private const string ExpectedBaselinePackageSha256 = "d7d90e7067295370587e0faf4708d6f63002b71e84ebb1a2602344b6a28496fd";
    private const string ExpectedBaselineOverlaySha256 = "0c1b7fcf5997f83a268b050f7487974a8937ee17a9373ad74a57f730472fb26b";
    private const string ExpectedBaselineDataSha256 = "cc35e035616a5c3d73f469b02d262f81cca3ba2d262d0be86e49a688b017c961";
    private const string ExpectedNoFastEntryExecutableSha256 = "f7be8a5bc9969effdd84310368267b9c3e8d45c718b4a381bb0442f6160a4436";
    private const string ExpectedNoFastEntryOutputSha256 = "61bb32dc9053445aa84790c10fcb1c69afb113df374cf53b5667392967ced199";

    private static readonly byte[] FastEntryRetailBytes = [0x37, 0xB6, 0x00, 0x08, 0, 0, 0, 0];
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);
    private static readonly int[] RewardMarkerTrueIndices = [61, 62, 63, 64, 65];
    private static readonly RewardMarkerPlacement[] RewardMarkerPlacements =
    [
        new(61, 0x796C, -410, 1229, 512),
        new(62, 0x7994, 0, 1229, 512),
        new(63, 0x79BC, -615, 205, 512),
        new(64, 0x79E4, -410, 615, 512),
        new(65, 0x7A0C, 204, 819, 512)
    ];

    public static bool TryDetectIntent(
        LevelDefinition level,
        JsonElement editsElement,
        out ToastyNativeLockedChestRuntimeBundleCandidateIntent? intent)
    {
        ArgumentNullException.ThrowIfNull(level);
        intent = null;
        if (!string.Equals(LevelCatalog.NormalizeKey(level.Key), TargetLevelKey, StringComparison.Ordinal))
            return false;
        if (editsElement.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("The Toasty native Moby edit manifest does not contain an edits array.");

        List<JsonElement> keyEdits = [];
        List<JsonElement> chestEdits = [];
        foreach (JsonElement edit in editsElement.EnumerateArray())
        {
            string templateId = ReadTemplateId(edit);
            if (string.Equals(templateId, KeyTemplateId, StringComparison.OrdinalIgnoreCase))
                keyEdits.Add(edit);
            else if (string.Equals(templateId, LockedChestTemplateId, StringComparison.OrdinalIgnoreCase))
                chestEdits.Add(edit);
        }

        if (chestEdits.Count == 0)
            return false;
        if (keyEdits.Count != 1 || chestEdits.Count != 1 || editsElement.GetArrayLength() != 2)
        {
            throw new InvalidOperationException(
                $"The Toasty candidate intent is one isolated atomic pair. Found {keyEdits.Count} Key edit(s), {chestEdits.Count} Locked Chest edit(s), and {editsElement.GetArrayLength()} total edit(s). Keep exactly one matching pair for its disposable output test.");
        }

        JsonElement key = keyEdits[0];
        JsonElement chest = chestEdits[0];
        GuardAddedEdit(key, "Key");
        GuardAddedEdit(chest, "Locked Chest");
        GuardTemplateSource(key, 78, "Key");
        GuardTemplateSource(chest, 79, "Locked Chest");
        GuardIdentity(key, "Key", 0x18, 0x00, 0x00AD, 0x02, 0x40, 0xFF);
        GuardIdentity(chest, "Locked Chest", 0x20, 0x00, 0x00AE, 0x00, 0x10, 0x54);

        (int keyX, int keyY, int keyZ) = ReadRequiredRawPosition(key, "Key");
        (int chestX, int chestY, int chestZ) = ReadRequiredRawPosition(chest, "Locked Chest");
        int keyEditorTrueIndex = ReadInt32(key, "trueIndex", -1);
        int chestEditorTrueIndex = ReadInt32(chest, "trueIndex", -1);
        if (keyEditorTrueIndex < 0 || chestEditorTrueIndex < 0 || keyEditorTrueIndex == chestEditorTrueIndex)
            throw new InvalidOperationException("The Toasty Key and Locked Chest require distinct saved editor object indices.");

        intent = new ToastyNativeLockedChestRuntimeBundleCandidateIntent(
            RecipeId,
            RequiredExporterFeature,
            TargetLevelKey,
            KeyTemplateId,
            keyEditorTrueIndex,
            ReadString(key, "label", "Key"),
            keyX,
            keyY,
            keyZ,
            ReadYawByte(key),
            LockedChestTemplateId,
            chestEditorTrueIndex,
            ReadString(chest, "label", "Locked Chest"),
            chestX,
            chestY,
            chestZ,
            ReadYawByte(chest),
            KeyOutputTrueIndex,
            LockedChestOutputTrueIndex,
            RewardMarkerTrueIndices,
            SourceRuntimeCountBefore,
            SourceRuntimeCountAfter,
            TreasureTargetBefore,
            TreasureTargetAfter);
        return true;
    }

    public static bool IsBundleTemplateEdit(JsonElement edit)
    {
        string templateId = ReadTemplateId(edit);
        return string.Equals(templateId, KeyTemplateId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(templateId, LockedChestTemplateId, StringComparison.OrdinalIgnoreCase);
    }

    public static ToastyNativeLockedChestRuntimeBundleCandidateResult ApplyAndVerify(
        ToastyNativeLockedChestRuntimeBundleCandidateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateIntent(request.Intent);
        ValidateRequest(request);

        string sourceImagePath = Path.GetFullPath(request.SourceImagePath);
        string outputImagePath = Path.GetFullPath(request.OutputPrefix + ".bin");
        string outputCuePath = Path.GetFullPath(request.OutputPrefix + ".cue");
        string outputPlanPath = Path.GetFullPath(request.OutputPrefix + ".toasty-key-locked-chest-candidate-intent-plan.json");
        string outputChecklistPath = Path.GetFullPath(request.OutputPrefix + ".toasty-runtime-checklist.md");
        if (PathsEqual(sourceImagePath, outputImagePath))
            throw new ArgumentException("The Toasty intent output cannot overwrite its clean retail source.", nameof(request));

        string[] artifacts = [outputImagePath, outputCuePath, outputPlanPath, outputChecklistPath];
        foreach (string artifact in artifacts)
            DeleteIfExists(artifact);
        Directory.CreateDirectory(Path.GetDirectoryName(outputImagePath) ?? ".");

        (DiscLayout sourceLayout, int wadSizeBefore, int executableLbaBefore) = GuardCleanRetailSource(sourceImagePath);
        _ = sourceLayout;
        try
        {
            if (!request.WriteImage)
            {
                const string planOnlyVerification =
                    "Validated one isolated Toasty Key + Locked Chest candidate intent against the clean retail disc; no BIN/CUE was requested and normal Create BIN remains unregistered.";
                ToastyNativeLockedChestRuntimeBundleCandidatePlan planOnly = BuildPlan(
                    request,
                    outputImagePath,
                    outputCuePath,
                    outputChecklistPath,
                    installed: false,
                    wadSizeBefore,
                    executableLbaBefore,
                    changedDataByteCount: 0,
                    baselineOutputSha256: "",
                    finalDataSha256: "",
                    finalOverlaySha256: "",
                    finalExecutableSha256: "",
                    outputImageSha256: "",
                    planOnlyVerification);
                File.WriteAllText(outputPlanPath, JsonSerializer.Serialize(planOnly, JsonOptions), Utf8WithoutBom);
                return new ToastyNativeLockedChestRuntimeBundleCandidateResult(
                    outputImagePath,
                    outputCuePath,
                    outputPlanPath,
                    outputChecklistPath,
                    planOnly,
                    WroteImage: false,
                    Verified: true);
            }

            ToastyNativeLockedChestCandidateResult baseline =
                ToastyNativeLockedChestCandidateExporter.Export(
                    new ToastyNativeLockedChestCandidateRequest(
                        SourceImagePath: sourceImagePath,
                        OutputImagePath: outputImagePath,
                        WadAnalysisPath: request.WadAnalysisPath,
                        LevelCatalogRootPath: request.LevelCatalogRootPath,
                        EnableFastEntry: false));
            GuardNoFastEntryBaseline(baseline);

            DiscLayout outputLayout = DiscImage.DetectLayout(outputImagePath);
            (int changedDataBytes, string dataSha, string overlaySha, string executableSha, string verification) =
                PatchPlacementsAndVerify(outputImagePath, outputLayout, request.Intent, baseline.Plan);
            string outputSha = Sha256File(outputImagePath);
            string cueText = DiscImage.BuildCueText(request.SourceCuePath, Path.GetFileName(outputImagePath));
            File.WriteAllText(outputCuePath, cueText, Encoding.ASCII);

            ToastyNativeLockedChestRuntimeBundleCandidatePlan plan = BuildPlan(
                request,
                outputImagePath,
                outputCuePath,
                outputChecklistPath,
                installed: true,
                wadSizeBefore,
                executableLbaBefore,
                changedDataBytes,
                baseline.Plan.OutputImageSha256,
                dataSha,
                overlaySha,
                executableSha,
                outputSha,
                verification);
            File.WriteAllText(outputPlanPath, JsonSerializer.Serialize(plan, JsonOptions), Utf8WithoutBom);
            File.WriteAllText(outputChecklistPath, BuildRuntimeChecklist(plan), Utf8WithoutBom);
            VerifyArtifactReadback(outputCuePath, outputPlanPath, outputChecklistPath, plan);
            return new ToastyNativeLockedChestRuntimeBundleCandidateResult(
                outputImagePath,
                outputCuePath,
                outputPlanPath,
                outputChecklistPath,
                plan,
                WroteImage: true,
                Verified: true);
        }
        catch
        {
            foreach (string artifact in artifacts)
                DeleteIfExists(artifact);
            throw;
        }
    }

    private static ToastyNativeLockedChestRuntimeBundleCandidatePlan BuildPlan(
        ToastyNativeLockedChestRuntimeBundleCandidateRequest request,
        string outputImagePath,
        string outputCuePath,
        string outputChecklistPath,
        bool installed,
        int wadSizeBefore,
        int executableLbaBefore,
        int changedDataByteCount,
        string baselineOutputSha256,
        string finalDataSha256,
        string finalOverlaySha256,
        string finalExecutableSha256,
        string outputImageSha256,
        string verification)
    {
        ToastyNativeLockedChestRuntimeBundleCandidateIntent intent = request.Intent;
        return new ToastyNativeLockedChestRuntimeBundleCandidatePlan(
            DateTimeOffset.UtcNow,
            RecipeId,
            RequiredExporterFeature,
            TargetLevelKey,
            CandidatePlanOnly: true,
            RegisteredForNormalCreateBin,
            Path.GetFullPath(request.SourceImagePath),
            outputImagePath,
            outputCuePath,
            outputChecklistPath,
            InstalledFromNoFastEntryBaseline: installed,
            FastEntryEnabled: false,
            wadSizeBefore,
            installed ? ExpandedWadSize : wadSizeBefore,
            executableLbaBefore,
            installed ? RelocatedExecutableLba : executableLbaBefore,
            intent.KeyEditorTrueIndex,
            KeyOutputTrueIndex,
            intent.KeyRawX,
            intent.KeyRawY,
            intent.KeyRawZ,
            intent.KeyYawByte,
            intent.LockedChestEditorTrueIndex,
            LockedChestOutputTrueIndex,
            intent.LockedChestRawX,
            intent.LockedChestRawY,
            intent.LockedChestRawZ,
            intent.LockedChestYawByte,
            RewardMarkerTrueIndices,
            SourceRuntimeCountBefore,
            SourceRuntimeCountAfter,
            TreasureTargetBefore,
            TreasureTargetAfter,
            RewardMarkerPlacements.Length,
            RewardMarkerPlacements.Length,
            changedDataByteCount,
            baselineOutputSha256,
            finalDataSha256,
            finalOverlaySha256,
            finalExecutableSha256,
            outputImageSha256,
            verification);
    }

    private static (int ChangedDataBytes, string DataSha, string OverlaySha, string ExecutableSha, string Verification)
        PatchPlacementsAndVerify(
            string outputImagePath,
            DiscLayout layout,
            ToastyNativeLockedChestRuntimeBundleCandidateIntent intent,
            ToastyNativeLockedChestCandidatePlan baselinePlan)
    {
        byte[] baselineData;
        byte[] expectedData;
        byte[] overlay;
        byte[] executable;
        using (FileStream output = File.Open(outputImagePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
        {
            DiscFileRecord wad = DiscImage.FindRootFileRecord(output, layout, IsWadName);
            DiscFileRecord executableRecord = DiscImage.FindRootFileRecord(output, layout, IsExecutableName);
            if (wad.Lba != WadLba || wad.Size != ExpandedWadSize ||
                executableRecord.Lba != RelocatedExecutableLba || executableRecord.Size != ExecutableSize)
            {
                throw new InvalidDataException("The Toasty candidate intent lost its checked expanded-WAD/relocated-SCUS layout.");
            }

            byte[] header = ReadWad(output, layout, 0, 0x800);
            GuardWadEntry(header, OverlayEntryIndex, ExpandedOverlayOffset, ExpandedOverlaySize, "expanded Toasty overlay");
            GuardWadEntry(header, DataEntryIndex, ExpandedDataOffset, ExpandedDataSize, "expanded Toasty data");
            overlay = ReadWad(output, layout, ExpandedOverlayOffset, ExpandedOverlaySize);
            GuardSha256(overlay, ExpectedBaselineOverlaySha256, "checked Toasty handler overlay");
            baselineData = ReadWad(output, layout, ExpandedDataOffset, ExpandedDataSize);
            GuardSha256(baselineData, ExpectedBaselineDataSha256, "checked Toasty no-fast-entry data baseline");
            GuardExpandedDataContract(baselineData);

            expectedData = baselineData.ToArray();
            PatchVisiblePlacement(expectedData, KeyOutputTrueIndex, intent.KeyRawX, intent.KeyRawY, intent.KeyRawZ, intent.KeyYawByte);
            PatchVisiblePlacement(expectedData, LockedChestOutputTrueIndex, intent.LockedChestRawX, intent.LockedChestRawY, intent.LockedChestRawZ, intent.LockedChestYawByte);
            foreach (RewardMarkerPlacement marker in RewardMarkerPlacements)
            {
                int x = checked(intent.LockedChestRawX + marker.DeltaX);
                int y = checked(intent.LockedChestRawY + marker.DeltaY);
                int z = checked(intent.LockedChestRawZ + marker.DeltaZ);
                WriteRowPosition(expectedData, marker.TrueIndex, x, y, z);
                WritePropsPosition(expectedData, marker.PropsRelativeOffset, x, y, z);
            }
            int changedDataBytes = GuardOnlyPlacementBytesChanged(baselineData, expectedData);

            DiscImage.WriteFileBytes(output, layout, WadLba, ExpandedDataOffset, expectedData);
            output.Flush(flushToDisk: true);
            executable = DiscImage.ReadFileBytes(output, layout, executableRecord.Lba, 0, executableRecord.Size);
            GuardSha256(executable, baselinePlan.FinalExecutableSha256, "no-fast-entry relocated executable");
            GuardEqual(executable.AsSpan(FastEntryFileOffset, FastEntryRetailBytes.Length).ToArray(), FastEntryRetailBytes, "retail fast-entry bytes");

            using FileStream readback = File.Open(outputImagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            byte[] actualData = ReadWad(readback, layout, ExpandedDataOffset, ExpandedDataSize);
            GuardEqual(actualData, expectedData, "Toasty moved Key/chest/reward data readback");
            GuardExpandedDataContract(actualData);
            VerifyPlacement(actualData, KeyOutputTrueIndex, intent.KeyRawX, intent.KeyRawY, intent.KeyRawZ, intent.KeyYawByte, "Key");
            VerifyPlacement(actualData, LockedChestOutputTrueIndex, intent.LockedChestRawX, intent.LockedChestRawY, intent.LockedChestRawZ, intent.LockedChestYawByte, "Locked Chest");
            foreach (RewardMarkerPlacement marker in RewardMarkerPlacements)
            {
                int x = checked(intent.LockedChestRawX + marker.DeltaX);
                int y = checked(intent.LockedChestRawY + marker.DeltaY);
                int z = checked(intent.LockedChestRawZ + marker.DeltaZ);
                VerifyRowPosition(actualData, marker.TrueIndex, x, y, z, $"reward marker T{marker.TrueIndex}");
                VerifyPropsPosition(actualData, marker.PropsRelativeOffset, x, y, z, $"reward marker T{marker.TrueIndex} props");
            }

            byte[] overlayReadback = ReadWad(readback, layout, ExpandedOverlayOffset, ExpandedOverlaySize);
            GuardEqual(overlayReadback, overlay, "unchanged Toasty handler overlay readback");
            byte[] executableReadback = DiscImage.ReadFileBytes(readback, layout, executableRecord.Lba, 0, executableRecord.Size);
            GuardEqual(executableReadback, executable, "unchanged no-fast-entry executable readback");

            string dataSha = Sha256(expectedData);
            string overlaySha = Sha256(overlay);
            string executableSha = Sha256(executable);
            string verification =
                $"Verified isolated {RecipeId} editor intent: saved Key XYZ/yaw -> T59, saved Locked Chest XYZ/yaw -> T60, translated all T61-T65 source-row XYZ and matching 0x28-byte props XYZ by the chest anchor, exactly {changedDataBytes} data byte(s) changed within the allowed placement fields, source count 59->66, treasure target 100->110, fast-entry bytes remained retail, handler/model/private textures remained hash-identical, and final data/overlay/executable/artifact files passed exact readback. Candidate-plan-only; not registered for normal Create BIN.";
            return (changedDataBytes, dataSha, overlaySha, executableSha, verification);
        }
    }

    private static void GuardNoFastEntryBaseline(ToastyNativeLockedChestCandidateResult baseline)
    {
        ToastyNativeLockedChestCandidatePlan plan = baseline.Plan;
        if (!baseline.Verified ||
            !string.Equals(plan.RecipeId, RecipeId, StringComparison.Ordinal) ||
            plan.FastEntryEnabled ||
            plan.KeyTrueIndex != KeyOutputTrueIndex ||
            plan.LockedChestTrueIndex != LockedChestOutputTrueIndex ||
            !plan.RewardMarkerTrueIndices.SequenceEqual(RewardMarkerTrueIndices) ||
            plan.SourceCountBefore != SourceRuntimeCountBefore ||
            plan.SourceCountAfter != SourceRuntimeCountAfter ||
            plan.TreasureTargetBefore != TreasureTargetBefore ||
            plan.TreasureTargetAfter != TreasureTargetAfter ||
            !string.Equals(plan.HandlerPayloadSha256, ExpectedBaselineHandlerSha256, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(plan.RebasedLockedChestPackageSha256, ExpectedBaselinePackageSha256, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(plan.FinalOverlaySha256, ExpectedBaselineOverlaySha256, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(plan.FinalDataEntrySha256, ExpectedBaselineDataSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The Toasty no-fast-entry baseline no longer matches its checked candidate allocation.");
        }
        GuardPinnedHash(plan.FinalExecutableSha256, ExpectedNoFastEntryExecutableSha256, "no-fast-entry executable");
        GuardPinnedHash(plan.OutputImageSha256, ExpectedNoFastEntryOutputSha256, "no-fast-entry candidate BIN");
    }

    private static void GuardExpandedDataContract(byte[] data)
    {
        if (data.Length != ExpandedDataSize)
            throw new InvalidDataException($"The expanded Toasty data entry is 0x{data.Length:X}, expected 0x{ExpandedDataSize:X}.");
        if (BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(SourceCountEntryOffset, 4)) != SourceRuntimeCountAfter)
            throw new InvalidDataException("The Toasty candidate no longer owns T59-T65.");
        if (BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(ScenePointerFixupCountEntryOffset, 4)) != ScenePointerFixupCountAfter)
            throw new InvalidDataException("The Toasty candidate pointer-fixup count changed.");
        GuardRowIdentity(data, KeyOutputTrueIndex, 0x7950, 0x00AD, "Key T59");
        GuardRowIdentity(data, LockedChestOutputTrueIndex, 0x7954, 0x00AE, "Locked Chest T60");
        foreach (RewardMarkerPlacement marker in RewardMarkerPlacements)
        {
            GuardRowIdentity(data, marker.TrueIndex, marker.PropsRelativeOffset, 0x000D, $"reward marker T{marker.TrueIndex}");
            int props = ExpandedSceneOffset + marker.PropsRelativeOffset;
            if (BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(props, 4)) != LockedChestOutputTrueIndex)
                throw new InvalidDataException($"Reward marker T{marker.TrueIndex} lost its T60 chest owner.");
        }
    }

    private static int GuardOnlyPlacementBytesChanged(byte[] before, byte[] after)
    {
        HashSet<int> allowed = [];
        AddVisibleAllowed(KeyOutputTrueIndex);
        AddVisibleAllowed(LockedChestOutputTrueIndex);
        foreach (RewardMarkerPlacement marker in RewardMarkerPlacements)
        {
            AddRange(allowed, SourceTableEntryOffset + (marker.TrueIndex * RecordStride) + 0x0C, 12);
            AddRange(allowed, ExpandedSceneOffset + marker.PropsRelativeOffset + 0x04, 12);
        }

        int changed = 0;
        for (int index = 0; index < before.Length; index++)
        {
            if (before[index] == after[index])
                continue;
            changed++;
            if (!allowed.Contains(index))
                throw new InvalidDataException($"The Toasty intent attempted to change non-placement data byte 0x{index:X}.");
        }
        return changed;

        void AddVisibleAllowed(int trueIndex)
        {
            int row = SourceTableEntryOffset + (trueIndex * RecordStride);
            AddRange(allowed, row + 0x0C, 12);
            allowed.Add(row + YawByteOffset);
        }
    }

    private static void AddRange(HashSet<int> target, int offset, int length)
    {
        for (int index = 0; index < length; index++)
            target.Add(offset + index);
    }

    private static void PatchVisiblePlacement(byte[] data, int trueIndex, int x, int y, int z, int yawByte)
    {
        WriteRowPosition(data, trueIndex, x, y, z);
        data[SourceTableEntryOffset + (trueIndex * RecordStride) + YawByteOffset] = checked((byte)yawByte);
    }

    private static void WriteRowPosition(byte[] data, int trueIndex, int x, int y, int z)
    {
        int offset = SourceTableEntryOffset + (trueIndex * RecordStride);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset + 0x0C, 4), x);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset + 0x10, 4), y);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset + 0x14, 4), z);
    }

    private static void WritePropsPosition(byte[] data, int propsRelativeOffset, int x, int y, int z)
    {
        int offset = ExpandedSceneOffset + propsRelativeOffset;
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset + 0x04, 4), x);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset + 0x08, 4), y);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset + 0x0C, 4), z);
    }

    private static void VerifyPlacement(byte[] data, int trueIndex, int x, int y, int z, int yawByte, string label)
    {
        VerifyRowPosition(data, trueIndex, x, y, z, label);
        int offset = SourceTableEntryOffset + (trueIndex * RecordStride) + YawByteOffset;
        if (data[offset] != (byte)yawByte)
            throw new InvalidDataException($"The saved Toasty {label} yaw failed source-row readback.");
    }

    private static void VerifyRowPosition(byte[] data, int trueIndex, int x, int y, int z, string label)
    {
        int offset = SourceTableEntryOffset + (trueIndex * RecordStride);
        if (BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset + 0x0C, 4)) != x ||
            BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset + 0x10, 4)) != y ||
            BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset + 0x14, 4)) != z)
        {
            throw new InvalidDataException($"The saved Toasty {label} XYZ failed source-row readback.");
        }
    }

    private static void VerifyPropsPosition(byte[] data, int propsRelativeOffset, int x, int y, int z, string label)
    {
        int offset = ExpandedSceneOffset + propsRelativeOffset;
        if (BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset + 0x04, 4)) != x ||
            BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset + 0x08, 4)) != y ||
            BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset + 0x0C, 4)) != z)
        {
            throw new InvalidDataException($"The translated Toasty {label} XYZ failed props readback.");
        }
    }

    private static void GuardRowIdentity(byte[] data, int trueIndex, int expectedProps, ushort expectedActorId, string label)
    {
        int offset = SourceTableEntryOffset + (trueIndex * RecordStride);
        ReadOnlySpan<byte> row = data.AsSpan(offset, RecordStride);
        if (BinaryPrimitives.ReadInt32LittleEndian(row[..4]) != expectedProps ||
            BinaryPrimitives.ReadUInt16LittleEndian(row.Slice(0x36, 2)) != expectedActorId)
        {
            throw new InvalidDataException($"The Toasty {label} pointer/actor identity changed.");
        }
    }

    private static void ValidateIntent(ToastyNativeLockedChestRuntimeBundleCandidateIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        if (!string.Equals(intent.RecipeId, RecipeId, StringComparison.Ordinal) ||
            !string.Equals(intent.RequiredExporterFeature, RequiredExporterFeature, StringComparison.Ordinal) ||
            !string.Equals(LevelCatalog.NormalizeKey(intent.TargetLevelKey), TargetLevelKey, StringComparison.Ordinal) ||
            !string.Equals(intent.KeyTemplateId, KeyTemplateId, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(intent.LockedChestTemplateId, LockedChestTemplateId, StringComparison.OrdinalIgnoreCase) ||
            intent.KeyOutputTrueIndex != KeyOutputTrueIndex ||
            intent.LockedChestOutputTrueIndex != LockedChestOutputTrueIndex ||
            !intent.RewardMarkerOutputTrueIndices.SequenceEqual(RewardMarkerTrueIndices) ||
            intent.SourceRuntimeCountBefore != SourceRuntimeCountBefore ||
            intent.SourceRuntimeCountAfter != SourceRuntimeCountAfter ||
            intent.TreasureTargetBefore != TreasureTargetBefore ||
            intent.TreasureTargetAfter != TreasureTargetAfter)
        {
            throw new InvalidOperationException("The Toasty Key + Locked Chest candidate intent does not match its fixed T59-T65 allocation.");
        }
        if (intent.KeyEditorTrueIndex < 0 || intent.LockedChestEditorTrueIndex < 0 ||
            intent.KeyEditorTrueIndex == intent.LockedChestEditorTrueIndex)
        {
            throw new InvalidOperationException("The Toasty Key and Locked Chest require distinct non-negative editor indices.");
        }
        if (intent.KeyYawByte is < 0 or > 255 || intent.LockedChestYawByte is < 0 or > 255)
            throw new InvalidOperationException("The Toasty Key/chest yaw must be a native byte.");
    }

    private static void ValidateRequest(ToastyNativeLockedChestRuntimeBundleCandidateRequest request)
    {
        if (!File.Exists(request.SourceImagePath))
            throw new FileNotFoundException("The clean retail Toasty intent source BIN was not found.", request.SourceImagePath);
        if (!File.Exists(request.SourceCuePath))
            throw new FileNotFoundException("The clean retail Toasty intent source CUE was not found.", request.SourceCuePath);
        if (!File.Exists(request.WadAnalysisPath))
            throw new FileNotFoundException("The guarded WAD analysis was not found.", request.WadAnalysisPath);
        if (!File.Exists(Path.Combine(request.LevelCatalogRootPath, "spyro-level-catalog.json")))
            throw new FileNotFoundException("The level catalog root was not found.", request.LevelCatalogRootPath);
        if (string.IsNullOrWhiteSpace(request.OutputPrefix))
            throw new ArgumentException("A Toasty candidate-intent output prefix is required.", nameof(request));
    }

    private static (DiscLayout Layout, int WadSize, int ExecutableLba) GuardCleanRetailSource(string sourceImagePath)
    {
        string sha = Sha256File(sourceImagePath);
        if (!string.Equals(sha, LockedChestRuntimeBundleProfileCatalog.CleanUsaImageSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"The Toasty candidate intent requires the clean USA retail BIN; found SHA-256 {sha}.");
        DiscLayout layout = DiscImage.DetectLayout(sourceImagePath);
        if (layout.SectorSize != 2352 || layout.UserOffset != 24)
            throw new InvalidDataException("The Toasty candidate intent requires retail MODE2/2352 layout.");
        using FileStream stream = File.Open(sourceImagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        DiscFileRecord wad = DiscImage.FindRootFileRecord(stream, layout, IsWadName);
        DiscFileRecord executable = DiscImage.FindRootFileRecord(stream, layout, IsExecutableName);
        if (wad.Lba != WadLba || wad.Size != RetailWadSize ||
            executable.Lba != RetailExecutableLba || executable.Size != ExecutableSize)
        {
            throw new InvalidDataException(
                $"The Toasty candidate intent requires clean WAD 0x{RetailWadSize:X}/SCUS LBA {RetailExecutableLba}; found 0x{wad.Size:X}/LBA {executable.Lba}.");
        }
        return (layout, wad.Size, executable.Lba);
    }

    private static void GuardAddedEdit(JsonElement edit, string label)
    {
        bool added = ReadBoolean(edit, "added") || string.Equals(ReadString(edit, "editKind"), "add", StringComparison.OrdinalIgnoreCase);
        if (!added || ReadBoolean(edit, "removed"))
            throw new InvalidOperationException($"The Toasty candidate intent requires an added {label}.");
    }

    private static void GuardTemplateSource(JsonElement edit, int expectedTrueIndex, string label)
    {
        if (!edit.TryGetProperty("crossLevelTemplate", out JsonElement template) || template.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException($"The Toasty {label} is missing cross-level donor metadata.");
        string sourceKey = LevelCatalog.NormalizeKey(ReadString(template, "sourceLevelKey"));
        int sourceTrueIndex = ReadInt32(template, "sourceTrueIndex", -1);
        if (sourceKey != "peacekeepers" || sourceTrueIndex != expectedTrueIndex)
            throw new InvalidOperationException($"The Toasty {label} must retain Peace Keepers T{expectedTrueIndex} donor identity.");
    }

    private static void GuardIdentity(
        JsonElement edit,
        string label,
        int expectedType,
        int expectedState,
        int expectedActorId,
        int expectedByte4F,
        int expectedFlag4A,
        int expectedFlag4B)
    {
        int type = ReadInt32(edit, "typeEditedHex", ReadInt32(edit, "typeHex", -1));
        int state = ReadInt32(edit, "stateEditedHex", ReadInt32(edit, "stateHex", -1));
        int actorLow = ReadInt32(edit, "sourceByte36EditedHex", ReadInt32(edit, "sourceByte36Hex", -1));
        int actorHigh = ReadInt32(edit, "sourceByte37EditedHex", ReadInt32(edit, "sourceByte37Hex", -1));
        int byte4F = ReadInt32(edit, "sourceByte4FEditedHex", ReadInt32(edit, "sourceByte4FHex", -1));
        int flag4A = ReadInt32(edit, "flag4AEditedHex", ReadInt32(edit, "flag4AHex", -1));
        int flag4B = ReadInt32(edit, "flag4BEditedHex", ReadInt32(edit, "flag4BHex", -1));
        int actorId = actorLow | (actorHigh << 8);
        if (type != expectedType || state != expectedState || actorId != expectedActorId ||
            byte4F != expectedByte4F || flag4A != expectedFlag4A || flag4B != expectedFlag4B ||
            HasUnexpectedSourceByteEdits(edit, expectedActorId, expectedByte4F, expectedFlag4A, expectedFlag4B))
        {
            throw new InvalidOperationException(
                $"The Toasty {label} identity was edited. This candidate composer accepts only saved XYZ and yaw.");
        }
    }

    private static bool HasUnexpectedSourceByteEdits(JsonElement edit, int actorId, int byte4F, int flag4A, int flag4B)
    {
        if (!edit.TryGetProperty("sourceByteEdits", out JsonElement edits) || edits.ValueKind != JsonValueKind.Array)
            return false;
        foreach (JsonElement byteEdit in edits.EnumerateArray())
        {
            int offset = ReadInt32(byteEdit, "offset", ReadInt32(byteEdit, "offsetHex", -1));
            int value = ReadInt32(byteEdit, "value", ReadInt32(byteEdit, "valueHex", -1));
            int expected = offset switch
            {
                0x36 => actorId & 0xFF,
                0x37 => (actorId >> 8) & 0xFF,
                0x4F => byte4F,
                0x52 => flag4A,
                0x53 => flag4B,
                _ => -1
            };
            if (value != expected)
                return true;
        }
        return false;
    }

    private static (int X, int Y, int Z) ReadRequiredRawPosition(JsonElement edit, string label)
    {
        if (TryReadRawAxis(edit, "x", out int x) && TryReadRawAxis(edit, "y", out int y) && TryReadRawAxis(edit, "z", out int z))
            return (x, y, z);
        throw new InvalidOperationException($"The Toasty {label} is missing saved XYZ.");
    }

    private static bool TryReadRawAxis(JsonElement edit, string axis, out int value)
    {
        value = 0;
        if (edit.TryGetProperty("rawEdited", out JsonElement raw) && raw.ValueKind == JsonValueKind.Object && raw.TryGetProperty(axis, out _))
        {
            value = ReadInt32(raw, axis);
            return true;
        }
        if (edit.TryGetProperty("edited", out JsonElement edited) && edited.ValueKind == JsonValueKind.Object && edited.TryGetProperty(axis, out _))
        {
            value = checked((int)Math.Round(ReadSingle(edited, axis) * 16f));
            return true;
        }
        return false;
    }

    private static int ReadYawByte(JsonElement edit)
    {
        int yaw = ReadInt32(edit, "yawByteEditedHex", ReadInt32(edit, "yawByteHex", ReadInt32(edit, "facingByteHex", -1)));
        if (yaw >= 0)
            return Math.Clamp(yaw, 0, 255);
        return Moby.DegreesToYawByte(ReadSingle(edit, "yawDegreesEdited", ReadSingle(edit, "yawDegrees", 0)));
    }

    private static string ReadTemplateId(JsonElement edit) =>
        edit.TryGetProperty("crossLevelTemplate", out JsonElement template) && template.ValueKind == JsonValueKind.Object
            ? ReadString(template, "id")
            : "";

    private static string ReadString(JsonElement element, string name, string fallback = "")
    {
        if (!element.TryGetProperty(name, out JsonElement value))
            return fallback;
        return value.ValueKind == JsonValueKind.String ? value.GetString() ?? fallback : value.ToString();
    }

    private static bool ReadBoolean(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out JsonElement value))
            return false;
        return value.ValueKind == JsonValueKind.True ||
            value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out bool parsed) && parsed;
    }

    private static int ReadInt32(JsonElement element, string name, int fallback = 0)
    {
        if (!element.TryGetProperty(name, out JsonElement value))
            return fallback;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int numeric))
            return numeric;
        string text = value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : value.ToString();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int hex))
        {
            return hex;
        }
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed) ? parsed : fallback;
    }

    private static float ReadSingle(JsonElement element, string name, float fallback = 0)
    {
        if (!element.TryGetProperty(name, out JsonElement value))
            return fallback;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetSingle(out float numeric))
            return numeric;
        return float.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) ? parsed : fallback;
    }

    private static string BuildRuntimeChecklist(ToastyNativeLockedChestRuntimeBundleCandidatePlan plan) => $$"""
        # Toasty Key + Locked Chest editor-intent candidate checklist

        > **RESEARCH ONLY. This CUE is not registered for normal Add/Create BIN. Do not use it as a project release.**

        Mount `{{Path.GetFileName(plan.OutputCuePath)}}` in DuckStation and cold boot it. This candidate deliberately preserves the retail executable's no-fast-entry bytes, so enter Toasty normally through the Artisans portal.

        ## Fixed placement intent

        - Key T59: raw XYZ `({{plan.KeyRawX}}, {{plan.KeyRawY}}, {{plan.KeyRawZ}})`, yaw `0x{{plan.KeyYawByte:X2}}`.
        - Locked Chest T60: raw XYZ `({{plan.LockedChestRawX}}, {{plan.LockedChestRawY}}, {{plan.LockedChestRawZ}})`, yaw `0x{{plan.LockedChestYawByte:X2}}`.
        - Hidden reward rows T61-T65 and all five props copies are translated with the chest.

        ## Required runtime evidence before any editor promotion

        - Cold boot and normal travel into Toasty complete without a black screen, freeze, or GTE assertion.
        - The Key is gold, appears at the saved position, is collectible once, and persists through death/reload.
        - The Locked Chest appears at the saved position with correct metal model/textures and saved facing.
        - The chest cannot open before the Key.
        - After the Key, the chest opens once with native animation and sound.
        - Exactly six gems worth `+10` appear at the moved chest; Toasty total becomes 110 exactly once.
        - Reward gems do not remain at the original candidate position.
        - Death/reload and leave/re-enter do not repeat the chest, sound, animation, or reward.
        - Toasty, dogs, shepherds, native class-0D rows T54/T55, metal debris, portal, and nearby gameplay remain normal.

        A successful boot alone is not a pass. Record every item above before this intent may be registered in the runtime profile catalog or normal Create BIN.

        ## Static proof

        - Baseline BIN SHA-256: `{{plan.NoFastEntryBaselineOutputSha256}}`
        - Moved BIN SHA-256: `{{plan.OutputImageSha256}}`
        - Final data SHA-256: `{{plan.FinalDataEntrySha256}}`
        - Final overlay SHA-256: `{{plan.FinalOverlaySha256}}`
        - Final executable SHA-256: `{{plan.FinalExecutableSha256}}`
        - {{plan.Verification}}
        """;

    private static void VerifyArtifactReadback(
        string outputCuePath,
        string outputPlanPath,
        string outputChecklistPath,
        ToastyNativeLockedChestRuntimeBundleCandidatePlan expectedPlan)
    {
        string cue = File.ReadAllText(outputCuePath, Encoding.ASCII);
        if (!cue.Contains($"FILE \"{Path.GetFileName(expectedPlan.OutputImagePath)}\" BINARY", StringComparison.Ordinal))
            throw new InvalidDataException("The Toasty candidate CUE does not point at its output BIN.");
        ToastyNativeLockedChestRuntimeBundleCandidatePlan? plan = JsonSerializer.Deserialize<ToastyNativeLockedChestRuntimeBundleCandidatePlan>(File.ReadAllText(outputPlanPath));
        if (plan == null || plan.OutputImageSha256 != expectedPlan.OutputImageSha256 ||
            plan.RegisteredForNormalCreateBin || !plan.CandidatePlanOnly || plan.FastEntryEnabled)
        {
            throw new InvalidDataException("The Toasty candidate intent plan failed serialization readback.");
        }
        string checklist = File.ReadAllText(outputChecklistPath);
        if (!checklist.Contains("RESEARCH ONLY", StringComparison.Ordinal) ||
            !checklist.Contains("not registered for normal Add/Create BIN", StringComparison.OrdinalIgnoreCase) ||
            !checklist.Contains("A successful boot alone is not a pass", StringComparison.Ordinal))
        {
            throw new InvalidDataException("The fixed Toasty runtime checklist lost its promotion gate.");
        }
    }

    private static void GuardWadEntry(byte[] header, int index, long expectedOffset, int expectedSize, string label)
    {
        int offset = index * 8;
        long actualOffset = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(offset, 4));
        int actualSize = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(offset + 4, 4)));
        if (actualOffset != expectedOffset || actualSize != expectedSize)
            throw new InvalidDataException($"The {label} changed: 0x{actualOffset:X}/0x{actualSize:X}.");
    }

    private static byte[] ReadWad(FileStream stream, DiscLayout layout, long offset, int length) =>
        DiscImage.ReadFileBytes(stream, layout, WadLba, offset, length);

    private static void GuardPinnedHash(string actual, string expected, string label)
    {
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"The Toasty {label} SHA-256 changed: expected {expected}, found {actual}.");
    }

    private static void GuardSha256(byte[] bytes, string expected, string label) =>
        GuardPinnedHash(Sha256(bytes), expected, label);

    private static void GuardEqual(byte[] actual, byte[] expected, string label)
    {
        if (!actual.AsSpan().SequenceEqual(expected))
            throw new InvalidDataException($"The Toasty {label} failed exact byte readback.");
    }

    private static string Sha256(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static string Sha256File(string path)
    {
        using FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static bool IsWadName(string name) =>
        string.Equals(name, "WAD.WAD", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, "WAD", StringComparison.OrdinalIgnoreCase);

    private static bool IsExecutableName(string name) =>
        name.StartsWith("SCUS_", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("SLUS_", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("SLES_", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("SCES_", StringComparison.OrdinalIgnoreCase);

    private static void DeleteIfExists(string path)
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

    private sealed record RewardMarkerPlacement(
        int TrueIndex,
        int PropsRelativeOffset,
        int DeltaX,
        int DeltaY,
        int DeltaZ);
}
