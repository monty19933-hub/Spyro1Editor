using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;

if (args.Length != 5)
{
    Console.Error.WriteLine("Usage: LockedChestBundleSmoke <retail.bin> <retail.cue> <wad-analysis.json> <proven-v2.bin> <output-directory>");
    return 2;
}

string retail = Path.GetFullPath(args[0]);
string cue = Path.GetFullPath(args[1]);
string analysis = Path.GetFullPath(args[2]);
string proven = Path.GetFullPath(args[3]);
string outputDirectory = Path.GetFullPath(args[4]);
Directory.CreateDirectory(outputDirectory);

ArtisansNativeLockedChestRuntimeBundleIntent baselineIntent = Intent(
    keyX: 0x10AEC,
    keyY: 0x15185,
    keyZ: 0x1800,
    keyYaw: 0,
    chestX: 0x114EC,
    chestY: 0x15185,
    chestZ: 0x1800,
    chestYaw: 0xDC);
ArtisansNativeLockedChestRuntimeBundleResult baseline =
    ArtisansNativeLockedChestRuntimeBundleComposer.ApplyAndVerify(
        new ArtisansNativeLockedChestRuntimeBundleRequest(
            retail,
            cue,
            Path.Combine(outputDirectory, "baseline"),
            analysis,
            baselineIntent));
string baselineSha = Sha256File(baseline.OutputImagePath);
string provenSha = Sha256File(proven);
if (!string.Equals(baselineSha, provenSha, StringComparison.OrdinalIgnoreCase) ||
    !string.Equals(baselineSha, "67b82ae66851d770b99d5e2d78400671268c31ad62b1815163e63324adc07d34", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException($"Baseline equivalence failed: composer={baselineSha}; proven={provenSha}.");
}

ArtisansNativeLockedChestRuntimeBundleIntent shiftedIntent = Intent(
    keyX: 0x10AEC + 0x321,
    keyY: 0x15185 - 0x123,
    keyZ: 0x1800 + 0x80,
    keyYaw: 0x31,
    chestX: 0x114EC - 0x234,
    chestY: 0x15185 + 0x456,
    chestZ: 0x1800 + 0x100,
    chestYaw: 0xA7);
ArtisansNativeLockedChestRuntimeBundleResult shifted =
    ArtisansNativeLockedChestRuntimeBundleComposer.ApplyAndVerify(
        new ArtisansNativeLockedChestRuntimeBundleRequest(
            retail,
            cue,
            Path.Combine(outputDirectory, "shifted"),
            analysis,
            shiftedIntent));
ArtisansNativeLockedChestRuntimeBundleResult idempotent =
    ArtisansNativeLockedChestRuntimeBundleComposer.ApplyAndVerify(
        new ArtisansNativeLockedChestRuntimeBundleRequest(
            shifted.OutputImagePath,
            shifted.OutputCuePath,
            Path.Combine(outputDirectory, "shifted-idempotent"),
            analysis,
            shiftedIntent));
string shiftedSha = Sha256File(shifted.OutputImagePath);
string idempotentSha = Sha256File(idempotent.OutputImagePath);
if (!string.Equals(shiftedSha, idempotentSha, StringComparison.OrdinalIgnoreCase) ||
    !idempotent.Plan.ReusedInstalledBundle || idempotent.Plan.InstalledFresh)
{
    throw new InvalidOperationException($"Installed-layout idempotence failed: first={shiftedSha}; second={idempotentSha}.");
}

string catalogPath = Path.Combine(Environment.CurrentDirectory, "spyro-level-catalog.json");
File.Copy(catalogPath, Path.Combine(outputDirectory, "spyro-level-catalog.json"), true);
string editsPath = Path.Combine(outputDirectory, "artisans-native-edits.json");
File.WriteAllText(editsPath, BuildManifest(includeKey: true, includeChest: true, includeThirdEdit: false));
LevelCatalog catalog = LevelCatalog.Load(outputDirectory);
LevelDefinition artisans = catalog.FindByKey("artisans") ?? throw new InvalidOperationException("Artisans is absent from the level catalog.");
MobySourcePatchPlan sourcePlan = MobySourcePatchExporter.BuildPlan(
    retail,
    cue,
    Path.Combine(outputDirectory, "plan-only.bin"),
    Path.Combine(outputDirectory, "plan-only.cue"),
    artisans,
    editsPath);
if (sourcePlan.ArtisansNativeLockedChestRuntimeBundle == null ||
    sourcePlan.PatchCount != 0 ||
    sourcePlan.PackageImportPreviews.Count != 0 ||
    sourcePlan.SkippedEdits.Count != 0 ||
    sourcePlan.EditOutcomes?.Count != 2 ||
    sourcePlan.EditOutcomes.Any(outcome => !outcome.PatchKinds.Contains("artisans-native-key-locked-chest-runtime-bundle-v2", StringComparer.OrdinalIgnoreCase)))
{
    throw new InvalidOperationException("MobySourcePatchExporter did not suppress the atomic pair into one deferred runtime-bundle intent.");
}

MobyBuildSafetyLevelReport safety = MobyBuildSafetyInspector.InspectLevel(retail, artisans, sourcePlan);
ArtisansNativeLockedChestRuntimeBundleIntent sourceIntent = sourcePlan.ArtisansNativeLockedChestRuntimeBundle;
if (safety.Status != MobyBuildSafetyStatus.Review ||
    safety.PlannedRuntimeRecordCount != 181 ||
    safety.TrueAppendCount != 7 ||
    safety.SourceDynamicCapacity != 91 ||
    safety.ProjectedDynamicCapacity != 85 ||
    safety.RuntimeSlotsConsumed != 6 ||
    safety.ComponentRepacked ||
    !safety.Issues.Any(issue =>
        issue.Code == "artisans-native-locked-chest-runtime-bundle-v2" &&
        issue.EditorTrueIndex == sourceIntent.LockedChestEditorTrueIndex))
{
    throw new InvalidOperationException($"Build safety did not report the exact proven 7-row allocation: {JsonSerializer.Serialize(safety)}");
}

string keyOnlyJson = BuildManifest(includeKey: true, includeChest: false, includeThirdEdit: false);
using (JsonDocument keyOnlyDocument = JsonDocument.Parse(keyOnlyJson))
{
    if (ArtisansNativeLockedChestRuntimeBundleComposer.TryDetectIntent(
            artisans,
            keyOnlyDocument.RootElement.GetProperty("edits"),
            out ArtisansNativeLockedChestRuntimeBundleIntent? keyOnlyIntent) ||
        keyOnlyIntent != null)
    {
        throw new InvalidOperationException("A standalone lightweight Key incorrectly activated the atomic Locked Chest bundle.");
    }
}
string keyOnlyEditsPath = Path.Combine(outputDirectory, "artisans-key-only-native-edits.json");
File.WriteAllText(keyOnlyEditsPath, keyOnlyJson);
MobySourcePatchPlan keyOnlyPlan = MobySourcePatchExporter.BuildPlan(
    retail,
    cue,
    Path.Combine(outputDirectory, "key-only-plan.bin"),
    Path.Combine(outputDirectory, "key-only-plan.cue"),
    artisans,
    keyOnlyEditsPath);
if (keyOnlyPlan.ArtisansNativeLockedChestRuntimeBundle != null ||
    !keyOnlyPlan.Patches.Any(patch => string.Equals(patch.Kind, "moby-record-append", StringComparison.OrdinalIgnoreCase)) ||
    !keyOnlyPlan.Patches.Any(patch => string.Equals(patch.Kind, "moby-source-count", StringComparison.OrdinalIgnoreCase)) ||
    keyOnlyPlan.PackageImportPreviews.Count != 0 ||
    keyOnlyPlan.SkippedEdits.Count != 0)
{
    throw new InvalidOperationException("The standalone lightweight Key did not remain on its ordinary direct source-record append path.");
}
AssertRejectedManifest(BuildManifest(includeKey: false, includeChest: true, includeThirdEdit: false), "standalone Locked Chest");
AssertRejectedManifest(BuildManifest(includeKey: true, includeChest: true, includeThirdEdit: false, chestCopies: 2), "duplicate Locked Chest pair");
AssertRejectedManifest(BuildManifest(includeKey: true, includeChest: true, includeThirdEdit: true), "pair plus another Artisans edit");
AssertRejectedManifest(BuildManifest(includeKey: true, includeChest: true, includeThirdEdit: false, keyState: "0x01"), "Key with edited state");
AssertRejectedManifest(BuildManifest(includeKey: true, includeChest: true, includeThirdEdit: false, chestState: "0x01"), "Locked Chest with edited state");

string otherLevelEditSource = Path.Combine(outputDirectory, "other-level-in-place-source.bin");
File.Copy(retail, otherLevelEditSource, true);
byte editedOtherTreasureByte;
using (FileStream other = File.Open(otherLevelEditSource, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
{
    byte originalOtherTreasureByte = ReadMode2FileByte(other, lba: 53875, fileOffset: 0x5FC3A);
    editedOtherTreasureByte = (byte)(originalOtherTreasureByte ^ 0x01);
    WriteMode2FileByte(other, lba: 53875, fileOffset: 0x5FC3A, editedOtherTreasureByte);
}
ArtisansNativeLockedChestRuntimeBundleResult preservedOtherEdit =
    ArtisansNativeLockedChestRuntimeBundleComposer.ApplyAndVerify(
        new ArtisansNativeLockedChestRuntimeBundleRequest(
            otherLevelEditSource,
            cue,
            Path.Combine(outputDirectory, "other-level-in-place-preserved"),
            analysis,
            baselineIntent));
using (FileStream preserved = File.OpenRead(preservedOtherEdit.OutputImagePath))
{
    byte readback = ReadMode2FileByte(preserved, lba: 53879, fileOffset: 0x5FC3A);
    if (readback != editedOtherTreasureByte)
        throw new InvalidOperationException("A non-Artisans in-place executable edit was not preserved through final V2 composition.");
}

string relocatedHeaderSource = Path.Combine(outputDirectory, "unsupported-structural-header-source.bin");
File.Copy(retail, relocatedHeaderSource, true);
using (FileStream incompatible = File.Open(relocatedHeaderSource, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
{
    byte originalHeaderByte = ReadMode2FileByte(incompatible, lba: 37, fileOffset: 0x7F0);
    WriteMode2FileByte(incompatible, lba: 37, fileOffset: 0x7F0, (byte)(originalHeaderByte ^ 0x01));
}
string incompatiblePrefix = Path.Combine(outputDirectory, "unsupported-structural-header-output");
try
{
    ArtisansNativeLockedChestRuntimeBundleComposer.ApplyAndVerify(
        new ArtisansNativeLockedChestRuntimeBundleRequest(
            relocatedHeaderSource,
            cue,
            incompatiblePrefix,
            analysis,
            baselineIntent));
    throw new InvalidOperationException("A conflicting structural WAD-header edit was not rejected.");
}
catch (InvalidDataException)
{
    if (File.Exists(incompatiblePrefix + ".bin") || File.Exists(incompatiblePrefix + ".cue"))
        throw new InvalidOperationException("The rejected structural-relocation candidate left a stale BIN/CUE behind.");
}

Console.WriteLine($"Baseline V2 equivalence SHA-256: {baselineSha}");
Console.WriteLine($"Shifted placement SHA-256: {shiftedSha}");
Console.WriteLine($"Installed-layout idempotence SHA-256: {idempotentSha}");
Console.WriteLine($"Build safety: {safety.TrueAppendCount} rows, {safety.SourceDynamicCapacity}->{safety.ProjectedDynamicCapacity} dynamic slots, {safety.Status}");
Console.WriteLine("Other-level in-place edit preservation: passed");
Console.WriteLine("Structural WAD relocation conflict rejection: passed");
Console.WriteLine("Locked Chest runtime-bundle smoke passed.");
return 0;

static ArtisansNativeLockedChestRuntimeBundleIntent Intent(
    int keyX,
    int keyY,
    int keyZ,
    int keyYaw,
    int chestX,
    int chestY,
    int chestZ,
    int chestYaw) =>
    new(
        RecipeId: ArtisansNativeLockedChestRuntimeBundleComposer.RecipeId,
        RequiredExporterFeature: ArtisansNativeLockedChestRuntimeBundleComposer.RequiredExporterFeature,
        TargetLevelKey: ArtisansNativeLockedChestRuntimeBundleComposer.TargetLevelKey,
        KeyTemplateId: ArtisansNativeLockedChestRuntimeBundleComposer.KeyTemplateId,
        KeyEditorTrueIndex: 175,
        KeyLabel: "Key",
        KeyRawX: keyX,
        KeyRawY: keyY,
        KeyRawZ: keyZ,
        KeyYawByte: keyYaw,
        LockedChestTemplateId: ArtisansNativeLockedChestRuntimeBundleComposer.LockedChestTemplateId,
        LockedChestEditorTrueIndex: 174,
        LockedChestLabel: "Key Chest",
        LockedChestRawX: chestX,
        LockedChestRawY: chestY,
        LockedChestRawZ: chestZ,
        LockedChestYawByte: chestYaw,
        KeyOutputTrueIndex: ArtisansNativeLockedChestRuntimeBundleComposer.KeyOutputTrueIndex,
        LockedChestOutputTrueIndex: ArtisansNativeLockedChestRuntimeBundleComposer.LockedChestOutputTrueIndex,
        RewardMarkerOutputTrueIndices: [176, 177, 178, 179, 180],
        SourceRuntimeCountBefore: ArtisansNativeLockedChestRuntimeBundleComposer.SourceRuntimeCountBefore,
        SourceRuntimeCountAfter: ArtisansNativeLockedChestRuntimeBundleComposer.SourceRuntimeCountAfter,
        TreasureTargetBefore: ArtisansNativeLockedChestRuntimeBundleComposer.TreasureTargetBefore,
        TreasureTargetAfter: ArtisansNativeLockedChestRuntimeBundleComposer.TreasureTargetAfter);

static string Sha256File(string path)
{
    using FileStream stream = File.OpenRead(path);
    return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
}

static byte ReadMode2FileByte(FileStream stream, int lba, int fileOffset)
{
    long imageOffset = ((long)lba + (fileOffset / 2048)) * 2352 + 24 + (fileOffset % 2048);
    stream.Position = imageOffset;
    int value = stream.ReadByte();
    return value < 0 ? throw new EndOfStreamException() : (byte)value;
}

static void WriteMode2FileByte(FileStream stream, int lba, int fileOffset, byte value)
{
    long imageOffset = ((long)lba + (fileOffset / 2048)) * 2352 + 24 + (fileOffset % 2048);
    stream.Position = imageOffset;
    stream.WriteByte(value);
    stream.Flush(flushToDisk: true);
}

static string BuildManifest(
    bool includeKey,
    bool includeChest,
    bool includeThirdEdit,
    string keyState = "0x00",
    string chestState = "0x00",
    int chestCopies = 1)
{
    List<object> edits = [];
    for (int chestCopy = 0; includeChest && chestCopy < chestCopies; chestCopy++)
    {
        edits.Add(new
        {
            trueIndex = 174 + (chestCopy * 2),
            label = "Key Chest",
            editKind = "add",
            added = true,
            removed = false,
            rawEdited = new { x = 0x114EC, y = 0x15185, z = 0x1800 },
            typeHex = "0x20",
            typeEditedHex = "0x20",
            stateHex = "0x00",
            stateEditedHex = chestState,
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
            yawByteEditedHex = "0xDC",
            sourceByteEdits = new[]
            {
                new
                {
                    offset = 82,
                    offsetHex = "0x52",
                    value = 0x10,
                    valueHex = "0x10",
                    field = "flag4A-identity-byte"
                }
            },
            crossLevelTemplate = new
            {
                id = ArtisansNativeLockedChestRuntimeBundleComposer.LockedChestTemplateId,
                family = "lockedChest",
                sourceLevelKey = "PeaceKeepers",
                sourceTrueIndex = 79,
                addSupportStatus = "supported-target-runtime-bundle",
                requiredExporterFeature = ArtisansNativeLockedChestRuntimeBundleComposer.RequiredExporterFeature,
                recipeId = ArtisansNativeLockedChestRuntimeBundleComposer.RecipeId
            }
        });
    }
    if (includeKey)
    {
        edits.Add(new
        {
            trueIndex = 175,
            label = "Key",
            editKind = "add",
            added = true,
            removed = false,
            rawEdited = new { x = 0x10AEC, y = 0x15185, z = 0x1800 },
            typeHex = "0x18",
            typeEditedHex = "0x18",
            stateHex = "0x00",
            stateEditedHex = keyState,
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
            yawByteEditedHex = "0x00",
            sourceByteEdits = new[]
            {
                new
                {
                    offset = 82,
                    offsetHex = "0x52",
                    value = 0x40,
                    valueHex = "0x40",
                    field = "flag4A-identity-byte"
                }
            },
            crossLevelTemplate = new
            {
                id = ArtisansNativeLockedChestRuntimeBundleComposer.KeyTemplateId,
                family = "key",
                sourceLevelKey = "PeaceKeepers",
                sourceTrueIndex = 78,
                addSupportStatus = "supported-lightweight-object",
                requiredExporterFeature = "DirectSourceRecordAppend",
                recipeId = ArtisansNativeLockedChestRuntimeBundleComposer.RecipeId
            }
        });
    }
    if (includeThirdEdit)
        edits.Add(new { trueIndex = 0, label = "Unrelated Artisans edit", editKind = "update", added = false, removed = false });
    return JsonSerializer.Serialize(new { editCount = edits.Count, edits }, new JsonSerializerOptions { WriteIndented = true });
}

static void AssertRejectedManifest(string json, string label)
{
    using JsonDocument document = JsonDocument.Parse(json);
    LevelDefinition level = new() { Key = "Artisans", DisplayName = "Artisans" };
    try
    {
        ArtisansNativeLockedChestRuntimeBundleComposer.TryDetectIntent(level, document.RootElement.GetProperty("edits"), out _);
    }
    catch (InvalidOperationException)
    {
        return;
    }
    throw new InvalidOperationException($"The {label} manifest was not rejected.");
}
