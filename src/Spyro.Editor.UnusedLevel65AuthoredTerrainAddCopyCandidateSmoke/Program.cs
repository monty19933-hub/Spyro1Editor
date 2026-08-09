using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;

const string BaseImageSha256 =
    "9e42b43bd1341b40915748432d1b2dc760e22a81c0a320ec09ae6a71ca2efcd8";
const string RejectedEvidenceId =
    "unused-level-65-town-square-authored-terrain-add-copy-not-observed-duckstation-2026-08-08";
const string RejectedEvidencePath =
    "docs/runtime-evidence/unused-level-65-town-square-authored-terrain-add-copy-not-observed-2026-08-08.json";

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
    "The retired v1 smoke base is not the exact display-name BIN.");

string evidencePath = Path.Combine(repositoryRoot, RejectedEvidencePath);
Require(File.Exists(evidencePath), "The rejected v1 runtime/static evidence is missing.");
using (JsonDocument evidence = JsonDocument.Parse(await File.ReadAllTextAsync(evidencePath)))
{
    JsonElement root = evidence.RootElement;
    JsonElement defect = root.GetProperty("staticSafetyDefect");
    Require(
        root.GetProperty("evidenceId").GetString() == RejectedEvidenceId &&
        root.GetProperty("evidenceStatus").GetString() ==
            "runtime-rejected-visibility-discriminator-not-observed" &&
        root.GetProperty("profileId").GetString() ==
            UnusedLevel65AuthoredTerrainAddCopyCandidateExporter.ProfileId &&
        root.GetProperty("outputImageSha256").GetString() ==
            UnusedLevel65AuthoredTerrainAddCopyCandidateExporter.ExpectedOutputImageSha256 &&
        !root.GetProperty("promotionAuthorized").GetBoolean() &&
        defect.GetProperty("conclusion").GetString()!.Contains("structurally unsafe", StringComparison.OrdinalIgnoreCase) &&
        defect.GetProperty("sourceSectorEndExclusive").GetString() == "0x6A3C4F8" &&
        defect.GetProperty("followingSectorWadOffset").GetString() == "0x6A3C4F8" &&
        defect.GetProperty("repackPatchEndExclusive").GetString() == "0x6A3C514" &&
        defect.GetProperty("overlapByteLength").GetInt32() == 28,
        "The retired v1 evidence lost its exact rejected profile or 28-byte overlap boundary.");
}

string temporaryRoot = Path.Combine(
    Path.GetTempPath(),
    $"spyro-id65-retired-terrain-add-copy-{Guid.NewGuid():N}");
Directory.CreateDirectory(temporaryRoot);
string outputPrefix = Path.Combine(temporaryRoot, "unsafe-v1-must-not-publish");
string outputImage = outputPrefix + ".bin";
string outputCue = outputPrefix + ".cue";
bool rejectedBeforePublication = false;
try
{
    try
    {
        await UnusedLevel65AuthoredTerrainAddCopyCandidateExporter.ExportAsync(new(
            baseImage,
            baseCue,
            outputImage,
            outputCue));
    }
    catch (InvalidDataException ex)
    {
        rejectedBeforePublication =
            ex.Message.Contains("0 patch record", StringComparison.OrdinalIgnoreCase) &&
            ex.Message.Contains("expected 6", StringComparison.OrdinalIgnoreCase);
    }

    Require(rejectedBeforePublication,
        "The unsafe v1 terrain add-copy was not rejected by the corrected append-bound guard.");
    Require(!File.Exists(outputImage) && !File.Exists(outputCue),
        "The retired v1 exporter published a BIN or CUE despite its structural overlap.");
    Require(await HashFileAsync(baseImage) == BaseImageSha256,
        "The retired v1 rejection mutated the focused-runtime-passed base BIN.");
    Require(!Directory.EnumerateFileSystemEntries(temporaryRoot, "*", SearchOption.AllDirectories)
        .Any(path =>
            path.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".bak", StringComparison.OrdinalIgnoreCase) ||
            path.EndsWith(".work", StringComparison.OrdinalIgnoreCase)),
        "The retired v1 rejection left transaction debris.");
}
finally
{
    if (Directory.Exists(temporaryRoot))
        Directory.Delete(temporaryRoot, recursive: true);
}

Console.WriteLine(
    "PASS: the rejected ID65 v1 terrain add-copy remains retired; its 28-byte following-sector " +
    "overlap is blocked before BIN/CUE publication and promotion remains unauthorized.");

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
