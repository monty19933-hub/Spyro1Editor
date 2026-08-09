using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;

const string BaseImageSha256 =
    "9e42b43bd1341b40915748432d1b2dc760e22a81c0a320ec09ae6a71ca2efcd8";
const string RejectedImageSha256 =
    "976a1910264c48fbc7cec8a9a1291f849bcad898fc511bae5d986a7af1c66214";
const string RejectedEvidenceId =
    "unused-level-65-town-square-authored-terrain-solid-existing-triangle-control-not-observed-duckstation-2026-08-08";
const string RejectedEvidencePath =
    "docs/runtime-evidence/unused-level-65-town-square-authored-terrain-solid-existing-triangle-control-not-observed-2026-08-08.json";
const string CandidatePrefix =
    "Unused-Level-65-Town-Square-authored-terrain-solid-existing-triangle-control-RUNTIME-CANDIDATE";

string repositoryRoot = FindRepositoryRoot(args.ElementAtOrDefault(0));
string basePrefix = Path.Combine(
    repositoryRoot,
    "_local",
    "v5-stone-hill-level-replacement",
    "unused-level-65-display-name",
    "Unused-Level-65-Town-Square-independent-storage-with-Town-Square-display-name-RUNTIME-CANDIDATE");
string baseImage = basePrefix + ".bin";
string baseCue = basePrefix + ".cue";
Require(File.Exists(baseImage) && File.Exists(baseCue),
    "The exact focused-runtime-passed display-name base BIN/CUE is missing.");
Require(await HashFileAsync(baseImage) == BaseImageSha256,
    "The retired v3 smoke base is not the exact display-name BIN.");

string evidencePath = Path.Combine(repositoryRoot, RejectedEvidencePath);
Require(File.Exists(evidencePath), "The rejected v3 runtime/structural evidence is missing.");
using (JsonDocument evidence = JsonDocument.Parse(await File.ReadAllTextAsync(evidencePath)))
{
    JsonElement root = evidence.RootElement;
    JsonElement defect = root.GetProperty("structuralDiscriminatorDefect");
    Require(
        root.GetProperty("evidenceId").GetString() == RejectedEvidenceId &&
        root.GetProperty("evidenceStatus").GetString() ==
            "runtime-rejected-visibility-discriminator-not-observed" &&
        root.GetProperty("profileId").GetString() ==
            UnusedLevel65AuthoredTerrainSolidTriangleControlCandidateExporter.ProfileId &&
        root.GetProperty("outputImageSha256").GetString() == RejectedImageSha256 &&
        !root.GetProperty("promotionAuthorized").GetBoolean() &&
        root.GetProperty("doNotLoadOrRetest").GetBoolean() &&
        defect.GetProperty("targetRuntimeKey").GetString() == "213:64:hp" &&
        defect.GetProperty("overlappingVisualRuntimeKey").GetString() == "4:6:hp" &&
        defect.GetProperty("overlappingVisualZ").GetInt32() == 560 &&
        defect.GetProperty("maximumProtrusionAboveOverlappingFace").GetInt32() == 16 &&
        defect.GetProperty("maximumProtrudingXyAreaFraction").GetString() == "1/16" &&
        defect.GetProperty("coveredOrBelowXyAreaFraction").GetString() == "15/16" &&
        defect.GetProperty("targetCollisionTriangleIndex").GetInt32() == 13853 &&
        defect.GetProperty("shadowingCollisionTriangleIndexes").EnumerateArray()
            .Select(value => value.GetInt32()).SequenceEqual(new[] { 1189, 1190 }) &&
        defect.GetProperty("shadowingCollisionZ").GetInt32() == 560 &&
        defect.GetProperty("conclusion").GetString()!.Contains("do not load or retest", StringComparison.OrdinalIgnoreCase) &&
        defect.GetProperty("conclusion").GetString()!.Contains("does not indicate a renderer", StringComparison.OrdinalIgnoreCase),
        "The retired v3 evidence lost its exact visual/collision shadow boundary or no-retest conclusion.");
}

string outputRoot = Path.Combine(
    repositoryRoot,
    "_local",
    "v5-stone-hill-level-replacement",
    "unused-level-65-authored-terrain-solid-existing-triangle-control");
string historicalPrefix = Path.Combine(outputRoot, CandidatePrefix);
string historicalImage = historicalPrefix + ".bin";
string historicalCue = historicalPrefix + ".cue";
string staticProofPath = historicalPrefix + "-static-proof.json";
string checklistPath = historicalPrefix + "-runtime-checklist.md";
string helperPath = historicalPrefix + "-Reveal-in-Finder.command";
Require(File.Exists(historicalImage) && File.Exists(historicalCue),
    "The exact rejected v3 BIN/CUE must remain available for audit.");
Require(await HashFileAsync(historicalImage) == RejectedImageSha256,
    "The retained rejected v3 BIN hash changed.");

using (JsonDocument proof = JsonDocument.Parse(await File.ReadAllTextAsync(staticProofPath)))
{
    JsonElement root = proof.RootElement;
    JsonElement defect = root.GetProperty("structuralDiscriminatorDefect");
    JsonElement handoff = root.GetProperty("testHandoff");
    Require(
        root.GetProperty("status").GetString() == "runtime-rejected-structurally-invalid-discriminator" &&
        !root.GetProperty("runtimeClaim").GetBoolean() &&
        !root.GetProperty("promotionAuthorized").GetBoolean() &&
        root.GetProperty("doNotLoadOrRetest").GetBoolean() &&
        root.GetProperty("evidenceId").GetString() == RejectedEvidenceId &&
        root.GetProperty("outputImageSha256").GetString() == RejectedImageSha256 &&
        defect.GetProperty("overlappingVisualRuntimeKey").GetString() == "4:6:hp" &&
        defect.GetProperty("shadowingCollisionTriangleIndexes").EnumerateArray()
            .Select(value => value.GetInt32()).SequenceEqual(new[] { 1189, 1190 }) &&
        !handoff.GetProperty("enabled").GetBoolean() &&
        handoff.GetProperty("finderRevealHelperDisabled").GetBoolean() &&
        !handoff.GetProperty("loadCodesAuthorized").GetBoolean(),
        "The rejected v3 proof is not a complete poison-pill tombstone.");
}

string checklist = await File.ReadAllTextAsync(checklistPath);
string helper = await File.ReadAllTextAsync(helperPath);
Require(
    checklist.StartsWith("# REJECTED — DO NOT LOAD OR RETEST", StringComparison.Ordinal) &&
    checklist.Contains("sector 4 HP face 6", StringComparison.OrdinalIgnoreCase) &&
    checklist.Contains("triangles 1189 and 1190", StringComparison.OrdinalIgnoreCase) &&
    !checklist.Contains("## Load codes", StringComparison.OrdinalIgnoreCase) &&
    helper.Contains("REJECTED", StringComparison.OrdinalIgnoreCase) &&
    helper.Contains("Do not load or retest", StringComparison.OrdinalIgnoreCase) &&
    helper.Contains("exit 1", StringComparison.Ordinal),
    "The rejected v3 checklist or Finder helper is still actionable.");

string temporaryRoot = Path.Combine(
    Path.GetTempPath(),
    $"spyro-id65-retired-solid-triangle-{Guid.NewGuid():N}");
Directory.CreateDirectory(temporaryRoot);
string attemptedPrefix = Path.Combine(temporaryRoot, "retired-v3-must-not-publish");
string attemptedImage = attemptedPrefix + ".bin";
string attemptedCue = attemptedPrefix + ".cue";
bool rejectedBeforePublication = false;
try
{
    try
    {
        await UnusedLevel65AuthoredTerrainSolidTriangleControlCandidateExporter.ExportAsync(new(
            baseImage,
            baseCue,
            attemptedImage,
            attemptedCue));
    }
    catch (InvalidOperationException ex)
    {
        rejectedBeforePublication =
            ex.Message.Contains("retired", StringComparison.OrdinalIgnoreCase) &&
            ex.Message.Contains("sector 4 face 6", StringComparison.OrdinalIgnoreCase) &&
            ex.Message.Contains("1189/1190", StringComparison.OrdinalIgnoreCase);
    }

    Require(rejectedBeforePublication,
        "The structurally invalid v3 exporter was not rejected before publication.");
    Require(!File.Exists(attemptedImage) && !File.Exists(attemptedCue),
        "The retired v3 exporter published a BIN or CUE.");
    Require(await HashFileAsync(baseImage) == BaseImageSha256,
        "The retired v3 rejection mutated the focused-runtime-passed base BIN.");
    Require(!Directory.EnumerateFileSystemEntries(temporaryRoot, "*", SearchOption.AllDirectories).Any(),
        "The retired v3 rejection left transaction debris.");
}
finally
{
    if (Directory.Exists(temporaryRoot))
        Directory.Delete(temporaryRoot, recursive: true);
}

Console.WriteLine(
    "PASS: the rejected ID65 v3 solid-triangle candidate remains retired; sector 4 face 6 and " +
    "collision triangles 1189/1190 shadow its target, publication is blocked, and promotion remains unauthorized.");

static async Task<string> HashFileAsync(string path)
{
    await using FileStream stream = File.OpenRead(path);
    return Convert.ToHexString(await SHA256.HashDataAsync(stream)).ToLowerInvariant();
}

static string FindRepositoryRoot(string? supplied)
{
    if (!string.IsNullOrWhiteSpace(supplied))
        return Path.GetFullPath(supplied);
    DirectoryInfo? cursor = new(Directory.GetCurrentDirectory());
    while (cursor != null)
    {
        if (Directory.Exists(Path.Combine(cursor.FullName, ".git")) &&
            Directory.Exists(Path.Combine(cursor.FullName, "src")))
        {
            return cursor.FullName;
        }
        cursor = cursor.Parent;
    }
    throw new DirectoryNotFoundException("Could not locate the Spyro editor repository root.");
}

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
