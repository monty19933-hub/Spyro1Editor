using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;

const int EditedTrueIndex = 21;
const float EditedWorldXDelta = -128f;
const long ExpectedDonorPatchWadOffset = 0x136E8B4;
const long ExpectedTargetPatchWadOffset = 0xD640B4;

string repositoryRoot = FindRepositoryRoot(args.ElementAtOrDefault(0));
(string sourceImage, string sourceCue) = ResolveCleanSource(repositoryRoot, args);
LevelCatalog catalog = LevelCatalog.Load(repositoryRoot);
LevelDefinition townSquare = catalog.FindByKey("townsquare")
    ?? throw new InvalidOperationException("The level catalog is missing Town Square.");

string outputRoot = Path.Combine(
    repositoryRoot,
    "_local",
    "v5-stone-hill-level-replacement",
    "edited-donor-candidate");
Directory.CreateDirectory(outputRoot);
string editPath = Path.Combine(outputRoot, "town-square-T21-x-only-native-edits.json");
string cachePath = Path.Combine(repositoryRoot, "editor-cache", "townsquare-mobys.json");
List<Moby> mobys = MobyLoader.LoadCached(cachePath).ToList();
Moby edited = mobys.Single(moby => moby.TrueIndex == EditedTrueIndex);
Require(
    edited.OriginalPosition == new Vector3f(7813.75f, 7189.75f, 512f),
    "Town Square T21's clean-cache position changed; the fixture must not silently retarget another object.");
edited.Position = edited.OriginalPosition with { X = edited.OriginalPosition.X + EditedWorldXDelta };
edited.Label = "Red Gem";
Require(
    await MobyEditStore.SaveAsync(editPath, mobys, townSquare.DisplayName) == 1,
    "The deterministic fixture did not save exactly one native Moby edit.");

string stagingPrefix = Path.Combine(outputRoot, "town-square-edited-donor-plan-only");
MobySourcePatchPlan editorObjectPlan = MobySourcePatchExporter.BuildPlan(
    sourceImage,
    sourceCue,
    stagingPrefix + ".bin",
    stagingPrefix + ".cue",
    townSquare,
    editPath);
Require(
    editorObjectPlan.LevelKey == "townsquare" &&
    editorObjectPlan.SourceRecordCount == townSquare.SourceRecordCount &&
    editorObjectPlan.PatchCount == 2 &&
    editorObjectPlan.TotalPatchedBytes == 5 &&
    editorObjectPlan.SkippedEdits.Count == 0 &&
    editorObjectPlan.Patches.Count == 2 &&
    editorObjectPlan.Patches.Any(patch =>
        patch.Kind == "moby-position-x" &&
        ParseHexOffset(patch.WadRelativeOffset) == ExpectedDonorPatchWadOffset) &&
    editorObjectPlan.Patches.Any(patch =>
        patch.Kind == "moby-placement-sector" &&
        ParseHexOffset(patch.WadRelativeOffset) == 0x136E8F2),
    "The genuine saved Town Square edit no longer produces its known X patch plus opportunistic placement-sector patch. " +
    $"patchCount={editorObjectPlan.PatchCount}, bytes={editorObjectPlan.TotalPatchedBytes}, " +
    $"patches=[{string.Join(", ", editorObjectPlan.Patches.Select(patch => $"{patch.Kind}@{patch.WadRelativeOffset}+{patch.ByteLength}"))}], " +
    $"skips=[{string.Join("; ", editorObjectPlan.SkippedEdits)}].");
MobySourcePatch donorPatch = editorObjectPlan.Patches.Single(patch =>
    patch.Kind == "moby-position-x");
Require(
    donorPatch.Kind == "moby-position-x" &&
    donorPatch.LevelKey == "townsquare" &&
    donorPatch.TrueIndex == EditedTrueIndex &&
    ParseHexOffset(donorPatch.WadRelativeOffset) == ExpectedDonorPatchWadOffset &&
    donorPatch.RecordOffset == "0xC" &&
    donorPatch.ByteLength == 4 &&
    NormalizeHex(donorPatch.BeforeHexPreview) == "5CE80100" &&
    NormalizeHex(donorPatch.AfterHexPreview) == "5CE00100",
    "Town Square T21's X-only donor patch changed offset, kind, length, or exact bytes.");
MobySourceEditOutcome editorOutcome = editorObjectPlan.EditOutcomes?.Single()
    ?? throw new InvalidDataException("The genuine saved Town Square edit has no unique source outcome.");
Require(
    editorOutcome.PatchKinds.Count == 2 &&
    editorOutcome.PatchKinds.Contains("moby-position-x", StringComparer.Ordinal) &&
    editorOutcome.PatchKinds.Contains("moby-placement-sector", StringComparer.Ordinal),
    "The genuine saved edit outcome no longer identifies both planned patch kinds.");
// This first runtime gate deliberately tests only the editor-requested X word.
// Normalize the disposable candidate plan, not the saved user edit: the general
// exporter derives +0x4A opportunistically from the terrain cache, and that byte
// needs its own relocation/runtime proof before it may join this checked gate.
MobySourcePatchPlan objectPlan = editorObjectPlan with
{
    PatchCount = 1,
    TotalPatchedBytes = donorPatch.ByteLength,
    Patches = [donorPatch],
    EditOutcomes =
    [
        editorOutcome with
        {
            PatchKinds = ["moby-position-x"]
        }
    ],
    Notes =
    [
        .. editorObjectPlan.Notes,
        "V5 edited-donor first gate preserved the genuine saved edit but excluded its opportunistic placement-sector derivation from this disposable candidate."
    ]
};

(string identityImage, string identityCue) = await EnsureExactIdentityBaseAsync(
    repositoryRoot,
    sourceImage,
    sourceCue,
    catalog);
string identityHashBefore = await HashFileAsync(identityImage);
string retailHashBefore = await HashFileAsync(sourceImage);
string outputPrefix = Path.Combine(
    outputRoot,
    "Stone-Hill-slot-Town-Square-edited-T21-X-RUNTIME-CANDIDATE");
StoneHillTownSquareEditedDonorCandidateRequest request = new(
    identityImage,
    identityCue,
    sourceImage,
    objectPlan,
    outputPrefix + ".bin",
    outputPrefix + ".cue");

MobySourcePatch extraPatch = donorPatch with
{
    Label = "townsquare-T21-unsafe-extra-y",
    Kind = "moby-position-y",
    RecordOffset = "0x10",
    WadRelativeOffset = "0x136E8B8"
};
MobySourcePatchPlan extraPatchPlan = objectPlan with
{
    PatchCount = 2,
    TotalPatchedBytes = 8,
    Patches = [donorPatch, extraPatch]
};
await ExpectFailureAsync(
    () => StoneHillTownSquareEditedDonorCandidateComposer.BuildPlanAsync(
        request with { MobyPatchPlan = extraPatchPlan }),
    "An extra Town Square source patch passed the edited-donor first gate.");
MobySourcePatchPlan outsideDonorPlan = objectPlan with
{
    Patches =
    [
        donorPatch with
        {
            WadRelativeOffset = "0x118E800"
        }
    ]
};
await ExpectFailureAsync(
    () => StoneHillTownSquareEditedDonorCandidateComposer.BuildPlanAsync(
        request with { MobyPatchPlan = outsideDonorPlan }),
    "A patch outside the checked donor data/record offset passed the edited-donor first gate.");

StoneHillTownSquareEditedDonorCandidatePlan plan =
    await StoneHillTownSquareEditedDonorCandidateComposer.BuildPlanAsync(request);
Require(
    plan.RecipeId == StoneHillTownSquareEditedDonorCandidateComposer.RecipeId &&
    plan.RecipeVersion == StoneHillTownSquareEditedDonorCandidateComposer.RecipeVersion &&
    plan.Evidence == NativeLevelReplacementEvidenceStatus.StaticBaselineOnly &&
    plan.EvidenceId == StoneHillTownSquareEditedDonorCandidateComposer.EvidenceId &&
    plan.Safety.Status == StoneHillTownSquareEditedDonorCandidateComposer.Status &&
    plan.Safety.RequiresDuckStationRuntimeProof &&
    plan.BaseImageSha256 == identityHashBefore &&
    plan.RetailSourceImageSha256 == retailHashBefore,
    "The edited-donor plan was not bound to the exact pending-runtime recipe, identity control, and retail source.");
Require(
    plan.Patch.Kind == "moby-position-x" &&
    plan.Patch.TrueIndex == EditedTrueIndex &&
    plan.Patch.DonorWadOffset == ExpectedDonorPatchWadOffset &&
    plan.Patch.TargetWadOffset == ExpectedTargetPatchWadOffset &&
    plan.Patch.ByteLength == 4 &&
    plan.Patch.BeforeHex == "5CE80100" &&
    plan.Patch.AfterHex == "5CE00100" &&
    IsSha256(plan.Patch.BeforeSha256) &&
    IsSha256(plan.Patch.AfterSha256) &&
    plan.Patch.BeforeSha256 != plan.Patch.AfterSha256,
    "The checked donor-to-target rebase or exact T21 X byte contract changed.");

string expectedProofPath = outputPrefix + "-static-proof.json";
string expectedChecklistPath = outputPrefix + "-runtime-checklist.md";
await File.WriteAllTextAsync(expectedProofPath, "{\"status\":\"stale-interrupted-sidecar\"}");
await File.WriteAllTextAsync(expectedChecklistPath, "stale interrupted sidecar");
StoneHillTownSquareEditedDonorArtifactResult artifact =
    await StoneHillTownSquareEditedDonorArtifactWriter.ExportAsync(request);
StoneHillTownSquareEditedDonorCandidateResult result = artifact.Candidate;
await File.AppendAllTextAsync(
    artifact.RuntimeChecklistPath,
    """

    ## Exact staged movement

    - Town Square T21 Red Gem original X: `7813.75` (raw `125020`, `5C E8 01 00`).
    - Town Square T21 Red Gem edited X: `7685.75` (raw `122972`, `5C E0 01 00`).
    - Expected visible movement: 128 world units toward negative X.
    - Native placement/culling sector byte at record `+0x4A` is intentionally unchanged in this first gate. The genuine saved editor edit also proposed a derived sector patch; the disposable candidate normalized only that derived patch away without altering the saved edit.
    """);
Require(
    result.Plan.RecipeId == plan.RecipeId &&
    result.Plan.RecipeVersion == plan.RecipeVersion &&
    result.Plan.Patch == plan.Patch,
    "The exported edited-donor candidate did not retain the checked plan recipe.");
Require(
    result.OutputImageSha256 != identityHashBefore &&
    IsSha256(result.OutputImageSha256) &&
    result.ChangedLogicalWadBytes == 1 &&
    result.ChangedPhysicalImageBytes > result.ChangedLogicalWadBytes &&
    result.RebuiltRawSectorCount == 1 &&
    result.ExactLogicalDiffBoundaryVerified &&
    result.ExactPhysicalSectorBoundaryVerified &&
    result.OriginalTownSquareDonorPreserved &&
    result.DisplayIdentityExecutablePreserved &&
    result.BaseImagePreserved &&
    result.RetailSourcePreserved &&
    result.BinCuePublishCompleted,
    "The final edited-donor candidate omitted an exact diff, source, donor, SCUS, sector, or publication proof.");
Require(
    artifact.StaticProofPath == expectedProofPath &&
    artifact.RuntimeChecklistPath == expectedChecklistPath &&
    File.Exists(result.OutputImagePath) &&
    File.Exists(result.OutputCuePath) &&
    File.Exists(artifact.StaticProofPath) &&
    File.Exists(artifact.RuntimeChecklistPath),
    "The edited-donor artifact writer did not publish the complete disposable CUE and evidence set.");
using (JsonDocument proof = JsonDocument.Parse(await File.ReadAllTextAsync(artifact.StaticProofPath)))
{
    Require(
        proof.RootElement.GetProperty("status").GetString() ==
            StoneHillTownSquareEditedDonorCandidateComposer.Status &&
        !proof.RootElement.GetProperty("runtimeClaim").GetBoolean() &&
        proof.RootElement.GetProperty("requiresDuckStationRuntimeProof").GetBoolean() &&
        proof.RootElement.GetProperty("evidenceId").GetString() == plan.EvidenceId &&
        proof.RootElement.GetProperty("outputImageSha256").GetString() == result.OutputImageSha256,
        "The edited-donor static proof omitted its exact pending-runtime boundary or final hash.");
}
string checklist = await File.ReadAllTextAsync(artifact.RuntimeChecklistPath);
Require(
    checklist.Contains(result.OutputImageSha256, StringComparison.Ordinal) &&
    checklist.Contains("not runtime proof", StringComparison.OrdinalIgnoreCase) &&
    checklist.Contains("T21", StringComparison.Ordinal) &&
    checklist.Contains("7813.75", StringComparison.Ordinal) &&
    checklist.Contains("7685.75", StringComparison.Ordinal) &&
    checklist.Contains("placement/culling sector", StringComparison.OrdinalIgnoreCase) &&
    checklist.Contains("intentionally unchanged", StringComparison.OrdinalIgnoreCase) &&
    checklist.Contains("original retail Town Square", StringComparison.Ordinal),
    "The edited-donor checklist omitted the exact candidate, actor, runtime boundary, or donor-isolation checks.");
Require(
    await HashFileAsync(identityImage) == identityHashBefore &&
    await HashFileAsync(sourceImage) == retailHashBefore &&
    !Directory.EnumerateFiles(outputRoot, "*.tmp").Any() &&
    !Directory.EnumerateFiles(outputRoot, "*.bak").Any(),
    "The identity/retail inputs changed or temporary publication files remained after export.");

string determinismPrefix = Path.Combine(outputRoot, "determinism-recheck");
try
{
    StoneHillTownSquareEditedDonorCandidateResult deterministic =
        await StoneHillTownSquareEditedDonorCandidateComposer.ExportAsync(
            request with
            {
                OutputImagePath = determinismPrefix + ".bin",
                OutputCuePath = determinismPrefix + ".cue"
            });
    Require(
        deterministic.OutputImageSha256 == result.OutputImageSha256 &&
        deterministic.ChangedLogicalWadBytes == result.ChangedLogicalWadBytes &&
        deterministic.ChangedPhysicalImageBytes == result.ChangedPhysicalImageBytes,
        "A second edited-donor export was not byte-deterministic.");
}
finally
{
    DeleteIfExists(determinismPrefix + ".bin");
    DeleteIfExists(determinismPrefix + ".cue");
}

Console.WriteLine("PASS: deterministic Town Square T21 X-only edit was rebased into the Stone Hill replacement slot.");
Console.WriteLine($"CUE: {result.OutputCuePath}");
Console.WriteLine($"BIN: {result.OutputImagePath}");
Console.WriteLine($"BIN SHA-256: {result.OutputImageSha256}");
Console.WriteLine($"Checklist: {artifact.RuntimeChecklistPath}");
Console.WriteLine($"Static proof: {artifact.StaticProofPath}");
Console.WriteLine($"Native edit: {editPath}");
Console.WriteLine("T21 X: 7813.75 -> 7685.75 (raw 125020 -> 122972); native +0x4A preserved.");
Console.WriteLine($"Donor WAD offset: 0x{ExpectedDonorPatchWadOffset:X}");
Console.WriteLine($"Expected Stone Hill-slot WAD offset: 0x{ExpectedTargetPatchWadOffset:X}");

static async Task<(string ImagePath, string CuePath)> EnsureExactIdentityBaseAsync(
    string repositoryRoot,
    string sourceImage,
    string sourceCue,
    LevelCatalog catalog)
{
    string replacementRoot = Path.Combine(
        repositoryRoot,
        "_local",
        "v5-stone-hill-level-replacement",
        "town-square-complete-pair-candidate");
    string replacementPrefix = Path.Combine(
        replacementRoot,
        "Stone-Hill-slot-Town-Square-complete-level-RUNTIME-CANDIDATE");
    string replacementImage = replacementPrefix + ".bin";
    string replacementCue = replacementPrefix + ".cue";
    if (!await HasHashAsync(
            replacementImage,
            NativeLevelReplacementProfileRegistry.TownSquareIntoStoneHillOutputImageSha256) ||
        !File.Exists(replacementCue))
    {
        Directory.CreateDirectory(replacementRoot);
        NativeLevelReplacementManifest manifest = await NativeLevelReplacementStore.StartStoneHillAsync(
            replacementRoot,
            sourceImage,
            catalog);
        NativeLevelReplacementIntent intent = await NativeLevelReplacementIntentStore.StartAsync(
            replacementRoot,
            NativeLevelReplacementProfileRegistry.TownSquareIntoStoneHillProfileId,
            manifest,
            catalog);
        await StoneHillTownSquareReplacementCandidateComposer.ExportAsync(new(
            manifest,
            intent,
            catalog,
            sourceImage,
            sourceCue,
            replacementImage,
            replacementCue));
    }

    string identityRoot = Path.Combine(
        repositoryRoot,
        "_local",
        "v5-stone-hill-level-replacement",
        "town-square-display-identity-candidate");
    string identityPrefix = Path.Combine(
        identityRoot,
        "Stone-Hill-slot-Town-Square-complete-level-with-Town-Square-display-name-RUNTIME-CANDIDATE");
    string identityImage = identityPrefix + ".bin";
    string identityCue = identityPrefix + ".cue";
    if (!await HasHashAsync(
            identityImage,
            NativeLevelReplacementIdentityProfileRegistry.TownSquareDisplayIdentityOutputImageSha256) ||
        !File.Exists(identityCue))
    {
        Directory.CreateDirectory(identityRoot);
        StoneHillTownSquareIdentityCandidateResult result =
            await StoneHillTownSquareIdentityCandidateComposer.ExportAsync(new(
                replacementImage,
                replacementCue,
                identityImage,
                identityCue));
        Require(
            result.OutputImageSha256 ==
                NativeLevelReplacementIdentityProfileRegistry.TownSquareDisplayIdentityOutputImageSha256,
            "The regenerated display-identity control did not match its exact runtime-proven hash.");
    }
    return (identityImage, identityCue);
}

static (string ImagePath, string CuePath) ResolveCleanSource(
    string repositoryRoot,
    string[] arguments)
{
    string? suppliedImage = arguments.ElementAtOrDefault(1);
    string? suppliedCue = arguments.ElementAtOrDefault(2);
    if (!string.IsNullOrWhiteSpace(suppliedImage) && !string.IsNullOrWhiteSpace(suppliedCue))
        return (Path.GetFullPath(suppliedImage), Path.GetFullPath(suppliedCue));

    string settingsPath = Path.Combine(repositoryRoot, "_local", "settings", "source-disc.json");
    if (!File.Exists(settingsPath))
        throw new FileNotFoundException(
            "No clean source BIN/CUE arguments or local source-disc settings were available.",
            settingsPath);
    using JsonDocument settings = JsonDocument.Parse(File.ReadAllText(settingsPath));
    string imagePath = settings.RootElement.GetProperty("imagePath").GetString() ?? "";
    string cuePath = settings.RootElement.GetProperty("cuePath").GetString() ?? "";
    if (!File.Exists(imagePath) || !File.Exists(cuePath))
        throw new FileNotFoundException("The configured clean USA BIN/CUE is missing.");
    return (Path.GetFullPath(imagePath), Path.GetFullPath(cuePath));
}

static string FindRepositoryRoot(string? supplied)
{
    if (!string.IsNullOrWhiteSpace(supplied))
        return Path.GetFullPath(supplied);
    DirectoryInfo? cursor = new(Directory.GetCurrentDirectory());
    while (cursor != null)
    {
        if (File.Exists(Path.Combine(cursor.FullName, "spyro-level-catalog.json")) &&
            Directory.Exists(Path.Combine(cursor.FullName, "src", "Spyro.Editor.Core")))
            return cursor.FullName;
        cursor = cursor.Parent;
    }
    throw new DirectoryNotFoundException("Could not locate the Spyro Editor repository root.");
}

static long ParseHexOffset(string text)
{
    if (string.IsNullOrWhiteSpace(text) ||
        !text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ||
        !long.TryParse(text[2..], System.Globalization.NumberStyles.HexNumber, null, out long value))
        throw new InvalidDataException($"Invalid hexadecimal offset '{text}'.");
    return value;
}

static string NormalizeHex(string text) =>
    new((text ?? "").Where(Uri.IsHexDigit).ToArray());

static bool IsSha256(string value) =>
    value.Length == 64 && value.All(Uri.IsHexDigit);

static async Task<bool> HasHashAsync(string path, string expected)
{
    return File.Exists(path) &&
        string.Equals(await HashFileAsync(path), expected, StringComparison.OrdinalIgnoreCase);
}

static async Task<string> HashFileAsync(string path)
{
    await using FileStream stream = File.OpenRead(path);
    byte[] hash = await SHA256.HashDataAsync(stream);
    return Convert.ToHexString(hash).ToLowerInvariant();
}

static async Task ExpectFailureAsync(Func<Task> action, string message)
{
    try
    {
        await action();
    }
    catch (Exception exception) when (
        exception is InvalidOperationException or InvalidDataException or ArgumentException)
    {
        return;
    }
    throw new InvalidOperationException(message);
}

static void DeleteIfExists(string path)
{
    if (File.Exists(path))
        File.Delete(path);
}

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
