using System.Text.Json;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;

if (args.Length is not 0 and not 5)
{
    Console.Error.WriteLine(
        "Usage: ToastyLockedChestIntentSmoke [<retail.bin> <retail.cue> <wad-analysis.json> <catalog-root> <output-directory>]");
    return 2;
}

string repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
string retail = args.Length == 5 ? Path.GetFullPath(args[0]) : Path.Combine(repositoryRoot, "Spyro the Dragon (USA).bin");
string retailCue = args.Length == 5 ? Path.GetFullPath(args[1]) : Path.Combine(repositoryRoot, "Spyro the Dragon (USA).cue");
string analysis = args.Length == 5 ? Path.GetFullPath(args[2]) : Path.Combine(repositoryRoot, "spyro-wad-analysis.json");
string catalogRoot = args.Length == 5 ? Path.GetFullPath(args[3]) : repositoryRoot;
string outputDirectory = args.Length == 5
    ? Path.GetFullPath(args[4])
    : Path.Combine(repositoryRoot, "_local", "research", "toasty-locked-chest-editor-intent");
Directory.CreateDirectory(outputDirectory);

const int keyX = 108319 + 321;
const int keyY = 118467 - 123;
const int keyZ = 19421 + 64;
const int keyYaw = 0x31;
const int chestX = 108534 - 234;
const int chestY = 116541 + 456;
const int chestZ = 19421 + 128;
const int chestYaw = 0xA7;

using JsonDocument manifest = JsonDocument.Parse(BuildManifest(
    keyX, keyY, keyZ, keyYaw,
    chestX, chestY, chestZ, chestYaw));
LevelDefinition toasty = new() { Key = "toasty", DisplayName = "Toasty" };
Expect(
    ToastyNativeLockedChestRuntimeBundleCandidateComposer.TryDetectIntent(
        toasty,
        manifest.RootElement.GetProperty("edits"),
        out ToastyNativeLockedChestRuntimeBundleCandidateIntent? detected) && detected != null,
    "The Toasty composer did not detect the saved editor pair.");
ToastyNativeLockedChestRuntimeBundleCandidateIntent intent = detected!;
Expect(
    intent.KeyRawX == keyX && intent.KeyRawY == keyY && intent.KeyRawZ == keyZ && intent.KeyYawByte == keyYaw &&
    intent.LockedChestRawX == chestX && intent.LockedChestRawY == chestY && intent.LockedChestRawZ == chestZ && intent.LockedChestYawByte == chestYaw,
    "Saved editor XYZ/yaw did not enter the Toasty candidate intent exactly.");
Expect(
    !ToastyNativeLockedChestRuntimeBundleCandidateComposer.TryDetectIntent(
        new LevelDefinition { Key = "stonehill", DisplayName = "Stone Hill" },
        manifest.RootElement.GetProperty("edits"),
        out _),
    "The unregistered Toasty intent incorrectly matched another level.");
Expect(
    LockedChestRuntimeBundleProfileCatalog.Find(
        "toasty",
        LockedChestRuntimeBundleProfileCatalog.CleanUsaImageSha256) == null &&
    !ToastyNativeLockedChestRuntimeBundleCandidateComposer.RegisteredForNormalCreateBin,
    "Toasty was accidentally registered for normal Create BIN.");

string planOnlyPrefix = Path.Combine(outputDirectory, "Toasty-Key-Locked-Chest-editor-intent-plan-only");
ToastyNativeLockedChestRuntimeBundleCandidateResult planOnly =
    ToastyNativeLockedChestRuntimeBundleCandidateComposer.ApplyAndVerify(new(
        SourceImagePath: retail,
        SourceCuePath: retailCue,
        OutputPrefix: planOnlyPrefix,
        WadAnalysisPath: analysis,
        LevelCatalogRootPath: catalogRoot,
        Intent: intent,
        WriteImage: false));
Expect(planOnly.Verified && !planOnly.WroteImage, "The Toasty plan-only intent did not verify cleanly.");
Expect(File.Exists(planOnly.OutputPlanPath), "The Toasty plan-only JSON was not written.");
Expect(
    !File.Exists(planOnly.OutputImagePath) && !File.Exists(planOnly.OutputCuePath) && !File.Exists(planOnly.OutputChecklistPath),
    "The Toasty plan-only path wrote a BIN/CUE/checklist.");
Expect(
    planOnly.Plan.CandidatePlanOnly && !planOnly.Plan.RegisteredForNormalCreateBin && !planOnly.Plan.FastEntryEnabled,
    "The Toasty plan-only output lost its unregistered/no-fast-entry gates.");

string outputPrefix = Path.Combine(outputDirectory, "Toasty-Key-Locked-Chest-editor-intent-disposable-v1");
ToastyNativeLockedChestRuntimeBundleCandidateResult output =
    ToastyNativeLockedChestRuntimeBundleCandidateComposer.ApplyAndVerify(new(
        SourceImagePath: retail,
        SourceCuePath: retailCue,
        OutputPrefix: outputPrefix,
        WadAnalysisPath: analysis,
        LevelCatalogRootPath: catalogRoot,
        Intent: intent,
        WriteImage: true));

Expect(output.Verified && output.WroteImage, "The moved Toasty candidate output did not verify.");
foreach (string path in new[] { output.OutputImagePath, output.OutputCuePath, output.OutputPlanPath, output.OutputChecklistPath })
    Expect(File.Exists(path), $"Missing Toasty candidate artifact: {path}");
Expect(
    output.Plan.InstalledFromNoFastEntryBaseline && !output.Plan.FastEntryEnabled &&
    output.Plan.TranslatedRewardRowCount == 5 && output.Plan.TranslatedRewardPropsCount == 5 &&
    output.Plan.RewardMarkerOutputTrueIndices.SequenceEqual(new[] { 61, 62, 63, 64, 65 }) &&
    output.Plan.SourceRuntimeCountBefore == 59 && output.Plan.SourceRuntimeCountAfter == 66 &&
    output.Plan.TreasureTargetBefore == 100 && output.Plan.TreasureTargetAfter == 110,
    "The Toasty moved output lost its fixed T59-T65/+10 contract.");
Expect(
    output.Plan.KeyRawX == keyX && output.Plan.KeyRawY == keyY && output.Plan.KeyRawZ == keyZ && output.Plan.KeyYawByte == keyYaw &&
    output.Plan.LockedChestRawX == chestX && output.Plan.LockedChestRawY == chestY && output.Plan.LockedChestRawZ == chestZ && output.Plan.LockedChestYawByte == chestYaw,
    "The Toasty plan did not read back the saved editor placement.");
Expect(
    output.Plan.ChangedDataByteCount == 70 &&
    output.Plan.NoFastEntryBaselineOutputSha256 == "61bb32dc9053445aa84790c10fcb1c69afb113df374cf53b5667392967ced199" &&
    output.Plan.FinalDataEntrySha256 == "9cd4d56332bfeb267e64884f932ede158113b4e4a5c6c1d8a11d394d26fb4252" &&
    output.Plan.FinalOverlaySha256 == "0c1b7fcf5997f83a268b050f7487974a8937ee17a9373ad74a57f730472fb26b" &&
    output.Plan.FinalExecutableSha256 == "f7be8a5bc9969effdd84310368267b9c3e8d45c718b4a381bb0442f6160a4436" &&
    output.Plan.OutputImageSha256 == "03ac3bb42acfca03eaddf3fc8944f0a1b9a15ce9800706ed2f494e6473272db6",
    "The pinned Toasty no-fast baseline or moved-intent output changed.");
string checklist = File.ReadAllText(output.OutputChecklistPath);
Expect(
    checklist.Contains("enter Toasty normally", StringComparison.OrdinalIgnoreCase) &&
    checklist.Contains("Reward gems do not remain at the original candidate position", StringComparison.Ordinal) &&
    checklist.Contains("A successful boot alone is not a pass", StringComparison.Ordinal),
    "The fixed Toasty checklist lost its no-fast-entry/moved-reward promotion gate.");

Console.WriteLine("Toasty Key + Locked Chest editor-intent candidate smoke passed.");
Console.WriteLine($"CUE: {output.OutputCuePath}");
Console.WriteLine($"Checklist: {output.OutputChecklistPath}");
Console.WriteLine($"No-fast baseline BIN SHA-256: {output.Plan.NoFastEntryBaselineOutputSha256}");
Console.WriteLine($"No-fast executable SHA-256: {output.Plan.FinalExecutableSha256}");
Console.WriteLine($"Moved data SHA-256: {output.Plan.FinalDataEntrySha256}");
Console.WriteLine($"Moved BIN SHA-256: {output.Plan.OutputImageSha256}");
Console.WriteLine($"Changed placement bytes: {output.Plan.ChangedDataByteCount}");
Console.WriteLine("- saved editor Key/chest XYZ+yaw applied exactly; five reward rows and five props translated with chest");
Console.WriteLine("- candidate remains plan-only, unregistered, and no-fast-entry pending fixed DuckStation proof");
return 0;

static string BuildManifest(
    int keyX,
    int keyY,
    int keyZ,
    int keyYaw,
    int chestX,
    int chestY,
    int chestZ,
    int chestYaw)
{
    object key = new
    {
        trueIndex = 1001,
        label = "Candidate Key",
        editKind = "add",
        added = true,
        removed = false,
        rawEdited = new { x = keyX, y = keyY, z = keyZ },
        typeHex = "0x18",
        typeEditedHex = "0x18",
        stateHex = "0x00",
        stateEditedHex = "0x00",
        sourceByte36Hex = "0xAD",
        sourceByte36EditedHex = "0xAD",
        sourceByte37Hex = "0x00",
        sourceByte37EditedHex = "0x00",
        sourceByte4FHex = "0x02",
        sourceByte4FEditedHex = "0x02",
        flag4AHex = "0x40",
        flag4AEditedHex = "0x40",
        flag4BHex = "0xFF",
        flag4BEditedHex = "0xFF",
        yawByteEditedHex = $"0x{keyYaw:X2}",
        sourceByteEdits = Array.Empty<object>(),
        crossLevelTemplate = new
        {
            id = ToastyNativeLockedChestRuntimeBundleCandidateComposer.KeyTemplateId,
            sourceLevelKey = "PeaceKeepers",
            sourceTrueIndex = 78
        }
    };
    object chest = new
    {
        trueIndex = 1002,
        label = "Candidate Locked Chest",
        editKind = "add",
        added = true,
        removed = false,
        rawEdited = new { x = chestX, y = chestY, z = chestZ },
        typeHex = "0x20",
        typeEditedHex = "0x20",
        stateHex = "0x00",
        stateEditedHex = "0x00",
        sourceByte36Hex = "0xAE",
        sourceByte36EditedHex = "0xAE",
        sourceByte37Hex = "0x00",
        sourceByte37EditedHex = "0x00",
        sourceByte4FHex = "0x00",
        sourceByte4FEditedHex = "0x00",
        flag4AHex = "0x10",
        flag4AEditedHex = "0x10",
        flag4BHex = "0x54",
        flag4BEditedHex = "0x54",
        yawByteEditedHex = $"0x{chestYaw:X2}",
        sourceByteEdits = Array.Empty<object>(),
        crossLevelTemplate = new
        {
            id = ToastyNativeLockedChestRuntimeBundleCandidateComposer.LockedChestTemplateId,
            sourceLevelKey = "PeaceKeepers",
            sourceTrueIndex = 79
        }
    };
    return JsonSerializer.Serialize(new { editCount = 2, edits = new[] { key, chest } }, new JsonSerializerOptions { WriteIndented = true });
}

static void Expect(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
