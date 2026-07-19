using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Exporting;

public sealed record ArtisansNativeLockedChestRuntimeBundleIntent(
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
    int TreasureTargetAfter)
{
    public int AppendedSourceRowCount => SourceRuntimeCountAfter - SourceRuntimeCountBefore;
    public int TreasureDelta => TreasureTargetAfter - TreasureTargetBefore;
}

public sealed record ArtisansNativeLockedChestRuntimeBundleRequest(
    string SourceImagePath,
    string SourceCuePath,
    string OutputPrefix,
    string WadAnalysisPath,
    ArtisansNativeLockedChestRuntimeBundleIntent Intent,
    bool WriteImage = true);

public sealed record ArtisansNativeLockedChestRuntimeBundlePlan(
    DateTimeOffset GeneratedAt,
    string RecipeId,
    string RequiredExporterFeature,
    string SourceImagePath,
    string OutputImagePath,
    string OutputCuePath,
    bool InstalledFresh,
    bool ReusedInstalledBundle,
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
    string FinalDataEntrySha256,
    string FinalOverlaySha256,
    string FinalExecutableSha256,
    string Verification);

public sealed record ArtisansNativeLockedChestRuntimeBundleResult(
    string OutputImagePath,
    string OutputCuePath,
    string OutputPlanPath,
    ArtisansNativeLockedChestRuntimeBundlePlan Plan,
    bool WroteImage,
    bool Verified);

/// <summary>
/// Promoted normal-Create-BIN composer for the one runtime-proven Artisans
/// Key + Locked Chest bundle. The pair is deliberately atomic: the visible
/// Key and chest reserve T174/T175, while T176-T180 and their scene props are
/// the hidden native reward markers required by the chest's one-shot +10 drop.
/// </summary>
public static class ArtisansNativeLockedChestRuntimeBundleComposer
{
    public const string RecipeId = ArtisansNativeLockedChestVisualCandidateExporter.RecipeId;
    public const string RequiredExporterFeature = "ArtisansNativeLockedChestRuntimeBundleV2";
    public const string TargetLevelKey = "artisans";
    public const string KeyTemplateId = "common.key.peacekeepers.t78";
    public const string LockedChestTemplateId = "common.locked_chest.peacekeepers.t79";
    public const int KeyOutputTrueIndex = 174;
    public const int LockedChestOutputTrueIndex = 175;
    public const int SourceRuntimeCountBefore = 174;
    public const int SourceRuntimeCountAfter = 181;
    public const int TreasureTargetBefore = 100;
    public const int TreasureTargetAfter = 110;

    public static bool IsAvailable => true;

    private const int WadLba = 37;
    private const int RetailWadSize = 0x6927000;
    private const int ExpandedWadSize = 0x6929000;
    private const int RetailExecutableLba = 53875;
    private const int RelocatedExecutableLba = 53879;
    private const int ExecutableSize = 0x66000;
    private const int ExpandedArtisansOverlayEntryIndex = 9;
    private const long ExpandedArtisansOverlayOffset = 0x7F2800;
    private const int ExpandedArtisansOverlaySize = 0xE800;
    private const int ExpandedArtisansDataEntryIndex = 10;
    private const long ExpandedArtisansDataOffset = 0x801000;
    private const int ExpandedArtisansDataSize = 0x384800;
    private const int ExpandedSceneOffset = 0x1CB000;
    private const int SourceCountEntryOffset = 0x1D52A8;
    private const int SourceTableEntryOffset = 0x1D52AC;
    private const int RecordStride = 0x58;
    private const int YawByteOffset = 0x46;
    private const int TexturePagesSubfileOffset = 0x800;
    private const int AddressableTexturePageBytes = 0x80000;
    private const int LockedChestPackageOffset = 0x1C9800;
    private const int LockedChestPackageLength = 0x1008;
    private const int ScenePointerFixupCountEntryOffset = 0x1DEED8;
    private const int FinalScenePointerFixupCount = 0xD0;
    private const int ArtisansTreasureTableFileOffset = 0x5FC38;
    private const int CopyBufferHiFileOffset = 0x4AF18;
    private const int CopyBufferLoFileOffset = 0x4AF1C;

    private const string ExpectedFinalTexturePagesSha256 = "374d658026a23bd3ab1b929a832c1f61f6cd0fbca2e5117395f6594235a5fde7";
    private const string ExpectedFinalLockedChestPackageSha256 = "1915dffaf52f7c7d5cf005971eb9f680e1b2d5d4c280a7752868a67db2a99103";
    private const string ExpectedFinalOverlaySha256 = "c93581c05a4c70c1803ece5268ebfada34034ce76e663dcaf19da8fe0faa0459";

    private static readonly int[] RewardMarkerTrueIndices = [176, 177, 178, 179, 180];

    // These are the exact native marker offsets from the runtime-proven V2
    // chest anchor. They are translated, not independently placed, so each
    // hidden source row and its scene-props copy stay byte-for-byte coherent.
    private static readonly RewardMarkerPlacement[] RewardMarkerPlacements =
    [
        new(176, 0x11EF8, -410, 1229, 512),
        new(177, 0x11F20, 0, 1229, 512),
        new(178, 0x11F48, -615, 205, 512),
        new(179, 0x11F70, -410, 615, 512),
        new(180, 0x11F98, 204, 819, 512)
    ];

    public static bool TryDetectIntent(
        LevelDefinition level,
        JsonElement editsElement,
        out ArtisansNativeLockedChestRuntimeBundleIntent? intent)
    {
        intent = null;
        if (!string.Equals(LevelCatalog.NormalizeKey(level.Key), TargetLevelKey, StringComparison.OrdinalIgnoreCase))
            return false;
        if (editsElement.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("The native Moby edit manifest does not contain an edits array.");

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

        // A Key by itself remains the universal lightweight direct-append
        // template. The atomic V2 bundle is activated only by a Locked Chest;
        // once a chest is present, exactly one matching Key is mandatory.
        if (chestEdits.Count == 0)
            return false;
        if (keyEdits.Count != 1 || chestEdits.Count != 1)
        {
            throw new InvalidOperationException(
                $"The promoted Artisans Key Chest is an atomic one-pair bundle. Found {keyEdits.Count} Key edit(s) and {chestEdits.Count} Locked Chest edit(s); add exactly one matching pair and remove standalone or duplicate copies.");
        }
        if (editsElement.GetArrayLength() != 2)
        {
            throw new InvalidOperationException(
                "The promoted Artisans Key + Locked Chest bundle cannot yet be combined with other Artisans object edits in one build. Keep exactly the two paired edits so the guarded T174-T180 allocation cannot collide with another source row.");
        }

        JsonElement key = keyEdits[0];
        JsonElement chest = chestEdits[0];
        GuardAddedEdit(key, "Key");
        GuardAddedEdit(chest, "Locked Chest");
        GuardTemplateSource(key, "PeaceKeepers", 78, "Key");
        GuardTemplateSource(chest, "PeaceKeepers", 79, "Locked Chest");
        GuardIdentity(key, "Key", 0x18, 0x00, 0x00AD, 0x02, 0x40, 0xFF);
        GuardIdentity(chest, "Locked Chest", 0x20, 0x00, 0x00AE, 0x00, 0x10, 0x54);

        (int keyX, int keyY, int keyZ) = ReadRequiredRawPosition(key, "Key");
        (int chestX, int chestY, int chestZ) = ReadRequiredRawPosition(chest, "Locked Chest");
        int keyEditorTrueIndex = ReadInt32(key, "trueIndex", -1);
        int chestEditorTrueIndex = ReadInt32(chest, "trueIndex", -1);
        if (keyEditorTrueIndex < 0 || chestEditorTrueIndex < 0 || keyEditorTrueIndex == chestEditorTrueIndex)
            throw new InvalidOperationException("The paired Key and Locked Chest require distinct saved editor object indices.");

        intent = new ArtisansNativeLockedChestRuntimeBundleIntent(
            RecipeId: RecipeId,
            RequiredExporterFeature: RequiredExporterFeature,
            TargetLevelKey: TargetLevelKey,
            KeyTemplateId: KeyTemplateId,
            KeyEditorTrueIndex: keyEditorTrueIndex,
            KeyLabel: ReadString(key, "label", "Key"),
            KeyRawX: keyX,
            KeyRawY: keyY,
            KeyRawZ: keyZ,
            KeyYawByte: ReadYawByte(key),
            LockedChestTemplateId: LockedChestTemplateId,
            LockedChestEditorTrueIndex: chestEditorTrueIndex,
            LockedChestLabel: ReadString(chest, "label", "Locked Chest"),
            LockedChestRawX: chestX,
            LockedChestRawY: chestY,
            LockedChestRawZ: chestZ,
            LockedChestYawByte: ReadYawByte(chest),
            KeyOutputTrueIndex: KeyOutputTrueIndex,
            LockedChestOutputTrueIndex: LockedChestOutputTrueIndex,
            RewardMarkerOutputTrueIndices: RewardMarkerTrueIndices,
            SourceRuntimeCountBefore: SourceRuntimeCountBefore,
            SourceRuntimeCountAfter: SourceRuntimeCountAfter,
            TreasureTargetBefore: TreasureTargetBefore,
            TreasureTargetAfter: TreasureTargetAfter);
        return true;
    }

    public static bool IsBundleTemplateEdit(JsonElement edit)
    {
        string templateId = ReadTemplateId(edit);
        return string.Equals(templateId, KeyTemplateId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(templateId, LockedChestTemplateId, StringComparison.OrdinalIgnoreCase);
    }

    public static ArtisansNativeLockedChestRuntimeBundleResult ApplyAndVerify(
        ArtisansNativeLockedChestRuntimeBundleRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateIntent(request.Intent);
        if (!File.Exists(request.SourceImagePath))
            throw new FileNotFoundException("The source disc image was not found.", request.SourceImagePath);
        if (!File.Exists(request.WadAnalysisPath))
            throw new FileNotFoundException("The guarded WAD analysis was not found.", request.WadAnalysisPath);
        if (string.IsNullOrWhiteSpace(request.OutputPrefix))
            throw new ArgumentException("An output prefix is required.", nameof(request));

        string sourcePath = Path.GetFullPath(request.SourceImagePath);
        string outputImagePath = Path.GetFullPath(request.OutputPrefix + ".bin");
        string outputCuePath = Path.GetFullPath(request.OutputPrefix + ".cue");
        string outputPlanPath = Path.GetFullPath(request.OutputPrefix + ".artisans-native-key-locked-chest-plan.json");
        if (string.Equals(sourcePath, outputImagePath, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The runtime-bundle output must not overwrite its source image.", nameof(request));

        DeleteIfExists(outputImagePath);
        DeleteIfExists(outputCuePath);
        DeleteIfExists(outputPlanPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputImagePath) ?? ".");

        DiscLayout sourceLayout = DiscImage.DetectLayout(sourcePath);
        int wadSizeBefore;
        int executableLbaBefore;
        using (FileStream source = File.Open(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            DiscFileRecord wad = DiscImage.FindRootFileRecord(source, sourceLayout, IsWadName);
            DiscFileRecord executable = DiscImage.FindRootFileRecord(source, sourceLayout, IsExecutableName);
            wadSizeBefore = wad.Size;
            executableLbaBefore = executable.Lba;
        }

        bool installedFresh;
        bool reusedInstalled;
        try
        {
            if (!request.WriteImage)
            {
                string planOnlyVerification = "Validated one atomic Artisans Key + Locked Chest intent; no image was requested.";
                ArtisansNativeLockedChestRuntimeBundlePlan planOnly = BuildPlan(
                    request,
                    outputImagePath,
                    outputCuePath,
                    installedFresh: false,
                    reusedInstalled: false,
                    wadSizeBefore,
                    executableLbaBefore,
                    finalDataSha256: "",
                    finalOverlaySha256: "",
                    finalExecutableSha256: "",
                    planOnlyVerification);
                File.WriteAllText(outputPlanPath, JsonSerializer.Serialize(planOnly, JsonOptions));
                return new ArtisansNativeLockedChestRuntimeBundleResult(outputImagePath, outputCuePath, outputPlanPath, planOnly, false, true);
            }

            if (wadSizeBefore == RetailWadSize && executableLbaBefore == RetailExecutableLba)
            {
                ArtisansNativeLockedChestVisualCandidateResult baseline =
                    ArtisansNativeLockedChestVisualCandidateExporter.Export(
                        new ArtisansNativeLockedChestVisualCandidateRequest(
                            SourceImagePath: sourcePath,
                            OutputImagePath: outputImagePath,
                            WadAnalysisPath: request.WadAnalysisPath,
                            AllowUnrelatedExecutableEdits: true));
                if (!baseline.Verified || !string.Equals(baseline.Plan.RecipeId, RecipeId, StringComparison.Ordinal))
                    throw new InvalidDataException("The runtime-proven V2 visual/behavior baseline did not verify.");
                installedFresh = true;
                reusedInstalled = false;
            }
            else if (wadSizeBefore == ExpandedWadSize && executableLbaBefore == RelocatedExecutableLba)
            {
                File.Copy(sourcePath, outputImagePath, true);
                installedFresh = false;
                reusedInstalled = true;
            }
            else
            {
                throw new InvalidDataException(
                    $"The Artisans native Key + Locked Chest composer requires either the retail WAD/SCUS layout (0x{RetailWadSize:X}, LBA {RetailExecutableLba}) or its already-installed V2 layout (0x{ExpandedWadSize:X}, LBA {RelocatedExecutableLba}). Found WAD 0x{wadSizeBefore:X}, SCUS LBA {executableLbaBefore}. A separate structural WAD relocation cannot be combined yet.");
            }

            DiscLayout outputLayout = DiscImage.DetectLayout(outputImagePath);
            (string dataSha, string overlaySha, string executableSha, string verification) =
                PatchPlacementsAndVerify(outputImagePath, outputLayout, request.Intent);
            string cueText = BuildCueText(request.SourceCuePath, Path.GetFileName(outputImagePath));
            File.WriteAllText(outputCuePath, cueText, Encoding.ASCII);

            ArtisansNativeLockedChestRuntimeBundlePlan plan = BuildPlan(
                request,
                outputImagePath,
                outputCuePath,
                installedFresh,
                reusedInstalled,
                wadSizeBefore,
                executableLbaBefore,
                dataSha,
                overlaySha,
                executableSha,
                verification);
            File.WriteAllText(outputPlanPath, JsonSerializer.Serialize(plan, JsonOptions));
            return new ArtisansNativeLockedChestRuntimeBundleResult(
                outputImagePath,
                outputCuePath,
                outputPlanPath,
                plan,
                WroteImage: true,
                Verified: true);
        }
        catch
        {
            DeleteIfExists(outputImagePath);
            DeleteIfExists(outputCuePath);
            DeleteIfExists(outputPlanPath);
            throw;
        }
    }

    private static ArtisansNativeLockedChestRuntimeBundlePlan BuildPlan(
        ArtisansNativeLockedChestRuntimeBundleRequest request,
        string outputImagePath,
        string outputCuePath,
        bool installedFresh,
        bool reusedInstalled,
        int wadSizeBefore,
        int executableLbaBefore,
        string finalDataSha256,
        string finalOverlaySha256,
        string finalExecutableSha256,
        string verification)
    {
        ArtisansNativeLockedChestRuntimeBundleIntent intent = request.Intent;
        return new ArtisansNativeLockedChestRuntimeBundlePlan(
            GeneratedAt: DateTimeOffset.UtcNow,
            RecipeId: RecipeId,
            RequiredExporterFeature: RequiredExporterFeature,
            SourceImagePath: Path.GetFullPath(request.SourceImagePath),
            OutputImagePath: outputImagePath,
            OutputCuePath: outputCuePath,
            InstalledFresh: installedFresh,
            ReusedInstalledBundle: reusedInstalled,
            WadSizeBefore: wadSizeBefore,
            WadSizeAfter: request.WriteImage ? ExpandedWadSize : wadSizeBefore,
            ExecutableLbaBefore: executableLbaBefore,
            ExecutableLbaAfter: request.WriteImage ? RelocatedExecutableLba : executableLbaBefore,
            KeyEditorTrueIndex: intent.KeyEditorTrueIndex,
            KeyOutputTrueIndex: KeyOutputTrueIndex,
            KeyRawX: intent.KeyRawX,
            KeyRawY: intent.KeyRawY,
            KeyRawZ: intent.KeyRawZ,
            KeyYawByte: intent.KeyYawByte,
            LockedChestEditorTrueIndex: intent.LockedChestEditorTrueIndex,
            LockedChestOutputTrueIndex: LockedChestOutputTrueIndex,
            LockedChestRawX: intent.LockedChestRawX,
            LockedChestRawY: intent.LockedChestRawY,
            LockedChestRawZ: intent.LockedChestRawZ,
            LockedChestYawByte: intent.LockedChestYawByte,
            RewardMarkerOutputTrueIndices: RewardMarkerTrueIndices,
            SourceRuntimeCountBefore: SourceRuntimeCountBefore,
            SourceRuntimeCountAfter: SourceRuntimeCountAfter,
            TreasureTargetBefore: TreasureTargetBefore,
            TreasureTargetAfter: TreasureTargetAfter,
            TranslatedRewardRowCount: RewardMarkerPlacements.Length,
            TranslatedRewardPropsCount: RewardMarkerPlacements.Length,
            FinalDataEntrySha256: finalDataSha256,
            FinalOverlaySha256: finalOverlaySha256,
            FinalExecutableSha256: finalExecutableSha256,
            Verification: verification);
    }

    private static (string DataSha, string OverlaySha, string ExecutableSha, string Verification) PatchPlacementsAndVerify(
        string outputImagePath,
        DiscLayout layout,
        ArtisansNativeLockedChestRuntimeBundleIntent intent)
    {
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
                throw new InvalidDataException("The promoted bundle did not retain the proven expanded-WAD/relocated-SCUS ISO layout.");
            }

            byte[] header = ReadWad(output, layout, 0, 0x800);
            GuardWadEntry(header, ExpandedArtisansOverlayEntryIndex, ExpandedArtisansOverlayOffset, ExpandedArtisansOverlaySize, "expanded Artisans overlay");
            GuardWadEntry(header, ExpandedArtisansDataEntryIndex, ExpandedArtisansDataOffset, ExpandedArtisansDataSize, "expanded Artisans data");
            overlay = ReadWad(output, layout, ExpandedArtisansOverlayOffset, ExpandedArtisansOverlaySize);
            GuardSha256(overlay, ExpectedFinalOverlaySha256, "runtime-proven Artisans handler overlay");

            expectedData = ReadWad(output, layout, ExpandedArtisansDataOffset, ExpandedArtisansDataSize);
            GuardExpandedDataContract(expectedData);
            PatchVisiblePlacement(expectedData, KeyOutputTrueIndex, intent.KeyRawX, intent.KeyRawY, intent.KeyRawZ, intent.KeyYawByte);
            PatchVisiblePlacement(expectedData, LockedChestOutputTrueIndex, intent.LockedChestRawX, intent.LockedChestRawY, intent.LockedChestRawZ, intent.LockedChestYawByte);
            foreach (RewardMarkerPlacement marker in RewardMarkerPlacements)
            {
                int x = checked(intent.LockedChestRawX + marker.DeltaX);
                int y = checked(intent.LockedChestRawY + marker.DeltaY);
                int z = checked(intent.LockedChestRawZ + marker.DeltaZ);
                WriteRowPosition(expectedData, marker.TrueIndex, x, y, z);
                WritePropsPosition(expectedData, marker.ScenePropsOffset, x, y, z);
            }

            DiscImage.WriteFileBytes(output, layout, WadLba, ExpandedArtisansDataOffset, expectedData);
            output.Flush(flushToDisk: true);
            executable = DiscImage.ReadFileBytes(output, layout, executableRecord.Lba, 0, executableRecord.Size);
            GuardExecutableContract(executable);
        }

        using (FileStream readback = File.Open(outputImagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            byte[] actualData = ReadWad(readback, layout, ExpandedArtisansDataOffset, ExpandedArtisansDataSize);
            if (!actualData.AsSpan().SequenceEqual(expectedData))
                throw new InvalidDataException("The moved Key/chest/reward source rows and props failed byte-for-byte readback.");
            GuardExpandedDataContract(actualData);
            VerifyPlacement(actualData, KeyOutputTrueIndex, intent.KeyRawX, intent.KeyRawY, intent.KeyRawZ, intent.KeyYawByte, "Key");
            VerifyPlacement(actualData, LockedChestOutputTrueIndex, intent.LockedChestRawX, intent.LockedChestRawY, intent.LockedChestRawZ, intent.LockedChestYawByte, "Locked Chest");
            foreach (RewardMarkerPlacement marker in RewardMarkerPlacements)
            {
                int x = checked(intent.LockedChestRawX + marker.DeltaX);
                int y = checked(intent.LockedChestRawY + marker.DeltaY);
                int z = checked(intent.LockedChestRawZ + marker.DeltaZ);
                VerifyRowPosition(actualData, marker.TrueIndex, x, y, z, $"reward marker T{marker.TrueIndex}");
                VerifyPropsPosition(actualData, marker.ScenePropsOffset, x, y, z, $"reward marker T{marker.TrueIndex} props");
            }
        }

        string dataSha = Sha256(expectedData);
        string overlaySha = Sha256(overlay);
        string executableSha = Sha256(executable);
        string verification =
            $"Verified atomic {RecipeId}: saved Key position/yaw -> T174, saved Locked Chest position/yaw -> T175, translated T176-T180 source rows and all five matching scene-props XYZ copies by the chest delta, source count 174->181, fixed treasure target 100->110, exact native handler/model/private-texture invariants, and byte-identical readback. Reapplying to the installed V2 layout does not grow the WAD a second time.";
        return (dataSha, overlaySha, executableSha, verification);
    }

    private static void GuardExpandedDataContract(byte[] data)
    {
        if (data.Length != ExpandedArtisansDataSize)
            throw new InvalidDataException($"The expanded Artisans data entry is 0x{data.Length:X}, expected 0x{ExpandedArtisansDataSize:X}.");
        if (BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(SourceCountEntryOffset, 4)) != SourceRuntimeCountAfter)
            throw new InvalidDataException("The promoted Artisans bundle no longer owns the exact T174-T180 source-count allocation.");
        if (BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(ScenePointerFixupCountEntryOffset, 4)) != FinalScenePointerFixupCount)
            throw new InvalidDataException("The promoted Artisans bundle no longer owns its exact seven source-row pointer-fixup contract.");

        GuardRowIdentity(data, KeyOutputTrueIndex, 0x00011EDC, 0x00AD, 0x18, 0x40, 0xFF, "native Key T174");
        GuardRowIdentity(data, LockedChestOutputTrueIndex, 0x00011EE0, 0x00AE, 0x20, 0xFF, 0x54, "native Locked Chest T175");
        foreach (RewardMarkerPlacement marker in RewardMarkerPlacements)
            GuardRowIdentity(data, marker.TrueIndex, (uint)marker.ScenePropsOffset, 0x000D, 0x00, 0xFF, (byte)(marker.TrueIndex is 177 or 179 ? 0x53 : 0x54), $"native reward marker T{marker.TrueIndex}");

        GuardSha256(data.AsSpan(TexturePagesSubfileOffset, AddressableTexturePageBytes).ToArray(), ExpectedFinalTexturePagesSha256, "private Artisans Locked Chest texture pages");
        GuardSha256(data.AsSpan(LockedChestPackageOffset, LockedChestPackageLength).ToArray(), ExpectedFinalLockedChestPackageSha256, "rebased Locked Chest model package");
    }

    private static void GuardRowIdentity(
        byte[] data,
        int trueIndex,
        uint expectedPropsPointer,
        ushort expectedActorId,
        byte expectedType,
        byte expectedFlag4A,
        byte expectedFlag4B,
        string label)
    {
        int offset = SourceTableEntryOffset + (trueIndex * RecordStride);
        ReadOnlySpan<byte> row = data.AsSpan(offset, RecordStride);
        if (BinaryPrimitives.ReadUInt32LittleEndian(row[..4]) != expectedPropsPointer ||
            BinaryPrimitives.ReadUInt16LittleEndian(row.Slice(0x36, 2)) != expectedActorId ||
            row[0x50] != expectedType || row[0x52] != expectedFlag4A || row[0x53] != expectedFlag4B)
        {
            throw new InvalidDataException($"The {label} runtime identity/pointer envelope changed.");
        }
    }

    private static void GuardExecutableContract(byte[] executable)
    {
        if (executable.Length != ExecutableSize)
            throw new InvalidDataException("The relocated executable size changed.");
        if (BinaryPrimitives.ReadUInt16LittleEndian(executable.AsSpan(ArtisansTreasureTableFileOffset, 2)) != TreasureTargetAfter)
            throw new InvalidDataException("The promoted Locked Chest did not retain the fixed Artisans treasure target 110.");
        if (BinaryPrimitives.ReadUInt32LittleEndian(executable.AsSpan(CopyBufferHiFileOffset, 4)) != 0x3C028009 ||
            BinaryPrimitives.ReadUInt32LittleEndian(executable.AsSpan(CopyBufferLoFileOffset, 4)) != 0x24429238)
        {
            throw new InvalidDataException("The relocated Artisans overlay copy-buffer address changed.");
        }
    }

    private static void PatchVisiblePlacement(byte[] data, int trueIndex, int x, int y, int z, int yawByte)
    {
        WriteRowPosition(data, trueIndex, x, y, z);
        int offset = SourceTableEntryOffset + (trueIndex * RecordStride);
        // The proven imported source rows use loader-owned matrix storage and
        // their native source yaw byte. Rebuilding the 0x20 matrix here makes
        // the baseline differ from the tested V2 image, so preserve the exact
        // matrix envelope and edit only the same source yaw byte the donor uses.
        data[offset + YawByteOffset] = (byte)yawByte;
    }

    private static void WriteRowPosition(byte[] data, int trueIndex, int x, int y, int z)
    {
        int offset = SourceTableEntryOffset + (trueIndex * RecordStride);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset + 0x0C, 4), x);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset + 0x10, 4), y);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset + 0x14, 4), z);
    }

    private static void WritePropsPosition(byte[] data, int scenePropsOffset, int x, int y, int z)
    {
        int offset = ExpandedSceneOffset + scenePropsOffset;
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset + 0x04, 4), x);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset + 0x08, 4), y);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset + 0x0C, 4), z);
    }

    private static void VerifyPlacement(byte[] data, int trueIndex, int x, int y, int z, int yawByte, string label)
    {
        VerifyRowPosition(data, trueIndex, x, y, z, label);
        int offset = SourceTableEntryOffset + (trueIndex * RecordStride);
        if (data[offset + YawByteOffset] != (byte)yawByte)
        {
            throw new InvalidDataException($"The saved {label} yaw did not read back from its output source row.");
        }
    }

    private static void VerifyRowPosition(byte[] data, int trueIndex, int x, int y, int z, string label)
    {
        int offset = SourceTableEntryOffset + (trueIndex * RecordStride);
        if (BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset + 0x0C, 4)) != x ||
            BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset + 0x10, 4)) != y ||
            BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset + 0x14, 4)) != z)
        {
            throw new InvalidDataException($"The saved {label} XYZ did not read back from its output source row.");
        }
    }

    private static void VerifyPropsPosition(byte[] data, int scenePropsOffset, int x, int y, int z, string label)
    {
        int offset = ExpandedSceneOffset + scenePropsOffset;
        if (BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset + 0x04, 4)) != x ||
            BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset + 0x08, 4)) != y ||
            BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(offset + 0x0C, 4)) != z)
        {
            throw new InvalidDataException($"The translated {label} XYZ did not read back.");
        }
    }

    private static void ValidateIntent(ArtisansNativeLockedChestRuntimeBundleIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        if (!string.Equals(intent.RecipeId, RecipeId, StringComparison.Ordinal) ||
            !string.Equals(intent.RequiredExporterFeature, RequiredExporterFeature, StringComparison.Ordinal) ||
            !string.Equals(LevelCatalog.NormalizeKey(intent.TargetLevelKey), TargetLevelKey, StringComparison.OrdinalIgnoreCase) ||
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
            throw new InvalidOperationException("The saved Artisans Key + Locked Chest intent does not match the promoted V2 allocation contract.");
        }
        if (intent.KeyYawByte is < 0 or > 255 || intent.LockedChestYawByte is < 0 or > 255)
            throw new InvalidOperationException("The paired Key and Locked Chest yaw bytes must be in the native 0x00-0xFF range.");
    }

    private static void GuardAddedEdit(JsonElement edit, string label)
    {
        bool added = ReadBoolean(edit, "added") || string.Equals(ReadString(edit, "editKind"), "add", StringComparison.OrdinalIgnoreCase);
        if (!added || ReadBoolean(edit, "removed"))
            throw new InvalidOperationException($"The promoted Artisans pair requires an added {label}; replacement, removal, and standalone forms are not supported.");
    }

    private static void GuardTemplateSource(JsonElement edit, string expectedLevelKey, int expectedTrueIndex, string label)
    {
        if (!edit.TryGetProperty("crossLevelTemplate", out JsonElement template) || template.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException($"The {label} is missing its cross-level template metadata.");
        string sourceKey = LevelCatalog.NormalizeKey(ReadString(template, "sourceLevelKey"));
        int sourceTrueIndex = ReadInt32(template, "sourceTrueIndex", -1);
        if (!string.Equals(sourceKey, LevelCatalog.NormalizeKey(expectedLevelKey), StringComparison.OrdinalIgnoreCase) || sourceTrueIndex != expectedTrueIndex)
            throw new InvalidOperationException($"The promoted {label} must retain the exact Peace Keepers T{expectedTrueIndex} donor identity.");
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
        bool hasUnexpectedSourceByteEdits = HasUnexpectedSourceByteEdits(
            edit,
            expectedActorId,
            expectedByte4F,
            expectedFlag4A,
            expectedFlag4B);
        if (type != expectedType || state != expectedState || actorId != expectedActorId || byte4F != expectedByte4F ||
            flag4A != expectedFlag4A || flag4B != expectedFlag4B || hasUnexpectedSourceByteEdits)
        {
            throw new InvalidOperationException(
                $"The promoted {label} identity fields were edited. Restore its template defaults before Create BIN; this V2 composer only supports moving and rotating the proven object pair.");
        }
    }

    private static bool HasUnexpectedSourceByteEdits(
        JsonElement edit,
        int expectedActorId,
        int expectedByte4F,
        int expectedFlag4A,
        int expectedFlag4B)
    {
        if (!edit.TryGetProperty("sourceByteEdits", out JsonElement sourceByteEdits) || sourceByteEdits.ValueKind != JsonValueKind.Array)
            return false;

        foreach (JsonElement byteEdit in sourceByteEdits.EnumerateArray())
        {
            int offset = ReadInt32(byteEdit, "offset", ReadInt32(byteEdit, "offsetHex", -1));
            int value = ReadInt32(byteEdit, "value", ReadInt32(byteEdit, "valueHex", -1));
            int expected = offset switch
            {
                0x36 => expectedActorId & 0xFF,
                0x37 => (expectedActorId >> 8) & 0xFF,
                0x4F => expectedByte4F,
                0x52 => expectedFlag4A,
                0x53 => expectedFlag4B,
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
        throw new InvalidOperationException($"The promoted {label} is missing a complete saved XYZ position.");
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
        float degrees = ReadSingle(edit, "yawDegreesEdited", ReadSingle(edit, "yawDegrees", 0));
        return Moby.DegreesToYawByte(degrees);
    }

    private static string ReadTemplateId(JsonElement edit)
    {
        return edit.TryGetProperty("crossLevelTemplate", out JsonElement template) && template.ValueKind == JsonValueKind.Object
            ? ReadString(template, "id")
            : "";
    }

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
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && int.TryParse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int hex))
            return hex;
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

    private static void GuardWadEntry(byte[] header, int index, long expectedOffset, int expectedSize, string label)
    {
        int offset = index * 8;
        long actualOffset = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(offset, 4));
        int actualSize = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(offset + 4, 4)));
        if (actualOffset != expectedOffset || actualSize != expectedSize)
            throw new InvalidDataException($"The {label} WAD entry changed: 0x{actualOffset:X}/0x{actualSize:X}.");
    }

    private static byte[] ReadWad(FileStream stream, DiscLayout layout, long offset, int length) =>
        DiscImage.ReadFileBytes(stream, layout, WadLba, offset, length);

    private static void GuardSha256(byte[] bytes, string expected, string label)
    {
        string actual = Sha256(bytes);
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"The {label} SHA-256 changed: expected {expected}, found {actual}.");
    }

    private static string Sha256(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static string BuildCueText(string sourceCuePath, string outputBinName)
    {
        if (!File.Exists(sourceCuePath))
            return $"FILE \"{outputBinName}\" BINARY\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\n";
        string[] lines = File.ReadAllLines(sourceCuePath, Encoding.ASCII);
        bool replaced = false;
        for (int i = 0; i < lines.Length; i++)
        {
            string trimmed = lines[i].Trim();
            if (!replaced && trimmed.StartsWith("FILE ", StringComparison.OrdinalIgnoreCase) && trimmed.EndsWith(" BINARY", StringComparison.OrdinalIgnoreCase))
            {
                lines[i] = $"FILE \"{outputBinName}\" BINARY";
                replaced = true;
            }
        }
        return string.Join('\n', replaced ? lines : new[] { $"FILE \"{outputBinName}\" BINARY" }.Concat(lines)) + "\n";
    }

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

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private sealed record RewardMarkerPlacement(int TrueIndex, int ScenePropsOffset, int DeltaX, int DeltaY, int DeltaZ);
}
