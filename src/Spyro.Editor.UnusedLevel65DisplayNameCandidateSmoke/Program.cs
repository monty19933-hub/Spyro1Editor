using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;

const string BaseExecutableSha256 =
    "0f6996babd64e815529df648fd7136f478c42f8187567d71439462df12b931ec";
const string OutputExecutableSha256 =
    "fa5fc7981188b78fa7d7b78facca64c1f79dadb107515e9146ad178ade39d442";
const string ExpectedOutputImageSha256 =
    "9e42b43bd1341b40915748432d1b2dc760e22a81c0a320ec09ae6a71ca2efcd8";

string repositoryRoot = FindRepositoryRoot(args.ElementAtOrDefault(0));
string baseRoot = Path.Combine(
    repositoryRoot,
    "_local",
    "v5-stone-hill-level-replacement",
    "unused-level-65-physical-clone");
string basePrefix = Path.Combine(
    baseRoot,
    "Unused-Level-65-Town-Square-independent-storage-RUNTIME-CANDIDATE");
string baseImage = basePrefix + ".bin";
string baseCue = basePrefix + ".cue";
if (!File.Exists(baseImage) || !File.Exists(baseCue))
{
    throw new FileNotFoundException(
        "The authored display-name discriminator requires the exact passed physical ID65 BIN/CUE.",
        !File.Exists(baseImage) ? baseImage : baseCue);
}

string baseHashBefore = await HashFileAsync(baseImage);
Require(
    baseHashBefore == UnusedLevel65PhysicalCloneCandidateExporter.ExpectedOutputImageSha256,
    "The physical ID65 base is not the exact focused-runtime-passed BIN.");
string baseRuntimeEvidencePath = Path.Combine(
    repositoryRoot,
    "docs",
    "runtime-evidence",
    "unused-level-65-town-square-physical-clone-focused-pass-2026-08-08.json");
Require(File.Exists(baseRuntimeEvidencePath), "The focused runtime evidence for the physical ID65 base is missing.");
using (JsonDocument baseRuntimeEvidence = JsonDocument.Parse(
           await File.ReadAllTextAsync(baseRuntimeEvidencePath)))
{
    JsonElement root = baseRuntimeEvidence.RootElement;
    Require(
        root.GetProperty("evidenceId").GetString() ==
            "unused-level-65-town-square-physical-clone-focused-pass-duckstation-2026-08-08" &&
        root.GetProperty("evidenceStatus").GetString() == "focused-runtime-pass" &&
        !root.GetProperty("promotionAuthorized").GetBoolean() &&
        root.GetProperty("profileId").GetString() ==
            UnusedLevel65PhysicalCloneCandidateExporter.ProfileId &&
        root.GetProperty("outputImageSha256").GetString() == baseHashBefore,
        "The physical ID65 runtime evidence no longer binds this exact unpromoted base BIN and profile.");
}

string outputRoot = Path.Combine(
    repositoryRoot,
    "_local",
    "v5-stone-hill-level-replacement",
    "unused-level-65-display-name");
Directory.CreateDirectory(outputRoot);
string outputPrefix = Path.Combine(
    outputRoot,
    "Unused-Level-65-Town-Square-independent-storage-with-Town-Square-display-name-RUNTIME-CANDIDATE");
string staticProofPath = outputPrefix + "-static-proof.json";
string checklistPath = outputPrefix + "-runtime-checklist.md";
await File.WriteAllTextAsync(
    staticProofPath,
    "{\"status\":\"stale-interrupted-sidecar\"}\n",
    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
await File.WriteAllTextAsync(
    checklistPath,
    "stale interrupted sidecar\n",
    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
UnusedLevel65DisplayNameCandidateRequest request = new(
    baseImage,
    baseCue,
    outputPrefix + ".bin",
    outputPrefix + ".cue");

UnusedLevel65DisplayNameCandidateResult result =
    await UnusedLevel65DisplayNameCandidateExporter.ExportAsync(request);
VerifyResult(result);
Require(
    File.Exists(result.OutputImagePath) && File.Exists(result.OutputCuePath),
    "The display-name candidate BIN/CUE was not published.");
Require(
    await HashFileAsync(baseImage) == baseHashBefore,
    "The exact runtime-passed physical-clone base changed during display-name export.");
RequireNoTransactionalDebris(outputRoot);

UnusedLevel65DisplayNameCandidateResult repeat =
    await UnusedLevel65DisplayNameCandidateExporter.ExportAsync(request);
VerifyResult(repeat);
Require(
    repeat.OutputImageSha256 == result.OutputImageSha256 &&
    repeat.Plan.OutputExecutableSha256 == result.Plan.OutputExecutableSha256,
    "A deterministic replacement export changed the display-name candidate hash.");
Require(
    await HashFileAsync(baseImage) == baseHashBefore,
    "The exact runtime-passed physical-clone base changed during repeated export.");
RequireNoTransactionalDebris(outputRoot);

IReadOnlyList<RuntimeCandidateLoadCode> loadCodes =
    RuntimeCandidateTestHandoff.Id65ComparisonLoadCodes;
RuntimeCandidateFinderReveal finderReveal =
    await RuntimeCandidateTestHandoff.WriteFinderRevealHelperAsync(repeat.OutputCuePath);
Require(
    finderReveal.CuePairingVerified &&
    finderReveal.HelperIsExecutable &&
    string.Equals(finderReveal.CuePath, Path.GetFullPath(repeat.OutputCuePath), StringComparison.Ordinal) &&
    string.Equals(finderReveal.PairedBinPath, Path.GetFullPath(repeat.OutputImagePath), StringComparison.Ordinal) &&
    File.Exists(finderReveal.HelperPath),
    "The display-name candidate did not publish an exact, executable Finder reveal handoff for its paired CUE/BIN.");
RequireNoTransactionalDebris(outputRoot);

string runtimeChecklistText = string.Join('\n', repeat.Plan.RuntimeChecklist);
Require(
    Contains(runtimeChecklistText, "memory-card") &&
    Contains(runtimeChecklistText, "Town Square") &&
    Contains(runtimeChecklistText, "instead of A") &&
    Contains(runtimeChecklistText, "collected gem is present again") &&
    Contains(runtimeChecklistText, "Gnasty's Loot") &&
    Contains(runtimeChecklistText, "Sunny Flight") &&
    Contains(runtimeChecklistText, "totals") &&
    Contains(runtimeChecklistText, "music") &&
    Contains(runtimeChecklistText, "portal") &&
    Contains(runtimeChecklistText, "Return Home") &&
    Contains(runtimeChecklistText, "save") &&
    Contains(runtimeChecklistText, "content"),
    "The focused checklist omitted the identity check, retail comparisons, or an excluded scope.");

object proof = new
{
    status = "static-proven-runtime-pending",
    runtimeClaim = false,
    generatedAtUtc = repeat.Plan.GeneratedAtUtc,
    profileId = repeat.Plan.ProfileId,
    baseProfileId = repeat.Plan.BaseProfileId,
    baseRuntimeEvidenceId =
        "unused-level-65-town-square-physical-clone-focused-pass-duckstation-2026-08-08",
    baseRuntimeEvidencePath =
        "docs/runtime-evidence/unused-level-65-town-square-physical-clone-focused-pass-2026-08-08.json",
    baseImageSha256 = repeat.Plan.BaseImageSha256,
    outputImageSha256 = repeat.OutputImageSha256,
    executable = new
    {
        lba = repeat.Plan.ExecutableLba,
        byteLength = repeat.Plan.ExecutableByteLength,
        baseSha256 = repeat.Plan.BaseExecutableSha256,
        outputSha256 = repeat.Plan.OutputExecutableSha256
    },
    displayIdentity = new
    {
        levelId = repeat.Plan.LevelId,
        continuousLevelIndex = repeat.Plan.ContinuousLevelIndex,
        namePointerTableFileOffset = $"0x{repeat.Plan.NamePointerTableFileOffset:X}",
        nameTableIndex = repeat.Plan.NameTableIndex,
        targetNamePointerFileOffset = $"0x{repeat.Plan.TargetNamePointerFileOffset:X}",
        donorNamePointerFileOffset = $"0x{repeat.Plan.DonorNamePointerFileOffset:X}",
        beforeName = repeat.Plan.PlaceholderName,
        afterName = repeat.Plan.AuthoredDisplayName,
        targetNamePointerBeforeHex = repeat.Plan.TargetNamePointerBeforeHex,
        targetNamePointerAfterHex = repeat.Plan.TargetNamePointerAfterHex
    },
    diffBoundary = new
    {
        repeat.ChangedLogicalExecutableBytes,
        repeat.ChangedPhysicalImageBytes,
        repeat.RebuiltRawSectorCount,
        repeat.ChangedRawSectorCount,
        affectedRawSectorLba = 55574,
        repeat.ExactLogicalDiffBoundaryVerified,
        repeat.ExactPhysicalSectorBoundaryVerified
    },
    repeat.NamePointerReadbackVerified,
    repeat.DisplayNameReadbackVerified,
    repeat.ProtectedScopesPreserved,
    repeat.BaseCandidatePreserved,
    repeat.AtomicRenameCompleted,
    logicalPatches = repeat.Plan.LogicalPatches,
    runtimeChecklist = repeat.Plan.RuntimeChecklist,
    testHandoff = new
    {
        cuePath = finderReveal.CuePath,
        pairedBinPath = finderReveal.PairedBinPath,
        finderRevealHelperPath = finderReveal.HelperPath,
        terminalCommand = finderReveal.TerminalCommand,
        cuePairingVerified = finderReveal.CuePairingVerified,
        helperIsExecutable = finderReveal.HelperIsExecutable,
        loadCodes = loadCodes.Select(code => new
        {
            testName = code.TestName,
            levelId = code.LevelId,
            inputCode = code.InputCode
        }).ToArray()
    },
    requiresDuckStationRuntimeProof = repeat.Plan.RequiresDuckStationRuntimeProof,
    limits = new[]
    {
        "This disposable candidate assigns ID65's indexed display-name pointer to the existing Town Square string only.",
        "Authored totals, music-table extension, portal routing, Return Home, save and memory-card ownership, and content edits remain out of scope.",
        "The passed physical-storage layout, retail comparison levels, WAD payloads, and every non-identity executable byte are protected.",
        "No normal Create BIN, editor workspace, release, or update-channel path consumes this candidate.",
        "DuckStation runtime proof is mandatory before any promotion."
    }
};
JsonSerializerOptions jsonOptions = new()
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    WriteIndented = true
};
await WriteTextAtomicallyAsync(
    staticProofPath,
    JsonSerializer.Serialize(proof, jsonOptions) + "\n");

StringBuilder checklist = new();
checklist.AppendLine("# Unused level 65 / Town Square display identity — DuckStation checklist");
checklist.AppendLine();
checklist.AppendLine($"- BIN SHA-256: `{repeat.OutputImageSha256}`");
checklist.AppendLine($"- Base BIN SHA-256: `{repeat.Plan.BaseImageSha256}`");
checklist.AppendLine($"- Profile: `{repeat.Plan.ProfileId}`");
checklist.AppendLine("- Status: static proof complete; focused runtime proof pending.");
checklist.AppendLine();
checklist.AppendLine(
    "This candidate changes only ID65's indexed name pointer from the placeholder A string " +
    "to the existing Town Square string. The physically independent rows 79/80 payload, " +
    "retail levels, totals, music, portals, Return Home, saving, and content are unchanged.");
checklist.AppendLine();
RuntimeCandidateTestHandoff.AppendCandidateDiscSection(checklist, finderReveal);
RuntimeCandidateTestHandoff.AppendLoadCodeTable(checklist, loadCodes);
checklist.AppendLine("## Test");
checklist.AppendLine();
for (int index = 0; index < repeat.Plan.RuntimeChecklist.Count; index++)
    checklist.AppendLine($"{index + 1}. {repeat.Plan.RuntimeChecklist[index]}");
checklist.AppendLine();
checklist.AppendLine("## Report back");
checklist.AppendLine();
checklist.AppendLine(
    "Please report whether ID65 displayed Town Square instead of A, loaded and remained responsive, " +
    "and whether retail Town Square, Gnasty's Loot, and Sunny Flight each retained their expected identity and load behavior. " +
    "Keep memory cards disabled and do not test any prohibited progression or content path.");
await WriteTextAtomicallyAsync(
    checklistPath,
    checklist.ToString());

using (JsonDocument document = JsonDocument.Parse(await File.ReadAllTextAsync(staticProofPath)))
{
    JsonElement root = document.RootElement;
    JsonElement diff = root.GetProperty("diffBoundary");
    JsonElement logicalPatch = root.GetProperty("logicalPatches")[0];
    JsonElement handoff = root.GetProperty("testHandoff");
    Require(
        root.GetProperty("status").GetString() == "static-proven-runtime-pending" &&
        !root.GetProperty("runtimeClaim").GetBoolean() &&
        root.GetProperty("baseImageSha256").GetString() == baseHashBefore &&
        root.GetProperty("outputImageSha256").GetString() == ExpectedOutputImageSha256 &&
        root.GetProperty("requiresDuckStationRuntimeProof").GetBoolean() &&
        diff.GetProperty("changedLogicalExecutableBytes").GetInt64() == 3 &&
        diff.GetProperty("changedPhysicalImageBytes").GetInt64() == 53 &&
        diff.GetProperty("rebuiltRawSectorCount").GetInt32() == 1 &&
        diff.GetProperty("changedRawSectorCount").GetInt32() == 1 &&
        diff.GetProperty("affectedRawSectorLba").GetInt32() == 55574 &&
        root.GetProperty("logicalPatches").GetArrayLength() == 1 &&
        logicalPatch.GetProperty("file").GetString() == "SCUS_942.28" &&
        logicalPatch.GetProperty("kind").GetString() ==
            "alias-level-65-display-name-to-town-square" &&
        logicalPatch.GetProperty("logicalOffset").GetInt64() == 0x6007C &&
        logicalPatch.GetProperty("byteLength").GetInt32() == 4 &&
        NormalizeHex(logicalPatch.GetProperty("beforeHex").GetString() ?? "") == "64550780" &&
        NormalizeHex(logicalPatch.GetProperty("afterHex").GetString() ?? "") == "E4010180" &&
        handoff.GetProperty("cuePath").GetString() == finderReveal.CuePath &&
        handoff.GetProperty("pairedBinPath").GetString() == finderReveal.PairedBinPath &&
        handoff.GetProperty("finderRevealHelperPath").GetString() == finderReveal.HelperPath &&
        handoff.GetProperty("terminalCommand").GetString() == finderReveal.TerminalCommand &&
        handoff.GetProperty("cuePairingVerified").GetBoolean() &&
        handoff.GetProperty("helperIsExecutable").GetBoolean() &&
        handoff.GetProperty("loadCodes").GetArrayLength() == 4,
        "The generated static proof is incomplete or drifted from the exact diff boundary.");
}
string writtenChecklist = await File.ReadAllTextAsync(checklistPath);
RuntimeCandidateTestHandoff.VerifyChecklistReadback(
    writtenChecklist,
    finderReveal,
    loadCodes);
Require(
    writtenChecklist.Contains(ExpectedOutputImageSha256, StringComparison.Ordinal) &&
    Contains(writtenChecklist, "Town Square instead of A") &&
    Contains(writtenChecklist, "collected gem is present again") &&
    Contains(writtenChecklist, "memory cards disabled") &&
    Contains(writtenChecklist, "totals") &&
    Contains(writtenChecklist, "music") &&
    Contains(writtenChecklist, "portals") &&
    Contains(writtenChecklist, "Return Home") &&
    Contains(writtenChecklist, "saving") &&
    Contains(writtenChecklist, "content"),
    "The generated runtime checklist omitted the exact artifact, expected identity, or excluded scopes.");
RequireNoTransactionalDebris(outputRoot);

Console.WriteLine("PASS: ID65 display identity is an exact one-pointer static candidate; DuckStation proof is pending.");
Console.WriteLine($"CUE: {repeat.OutputCuePath}");
Console.WriteLine($"Reveal in Finder: {finderReveal.HelperPath}");
Console.WriteLine($"BIN: {repeat.OutputImagePath}");
Console.WriteLine($"Checklist: {checklistPath}");
Console.WriteLine($"Static proof: {staticProofPath}");
Console.WriteLine($"BIN SHA-256: {repeat.OutputImageSha256}");
Console.WriteLine($"SCUS SHA-256: {repeat.Plan.OutputExecutableSha256}");
Console.WriteLine(
    $"Changed logical SCUS bytes: {repeat.ChangedLogicalExecutableBytes}; " +
    $"changed physical raw-sector bytes: {repeat.ChangedPhysicalImageBytes}; " +
    $"rebuilt/changed raw sectors: {repeat.RebuiltRawSectorCount}/{repeat.ChangedRawSectorCount}.");

static void VerifyResult(UnusedLevel65DisplayNameCandidateResult result)
{
    Require(
        result.Plan.ProfileId == UnusedLevel65DisplayNameCandidateExporter.ProfileId &&
        result.Plan.BaseProfileId == UnusedLevel65PhysicalCloneCandidateExporter.ProfileId &&
        result.Plan.BaseImageSha256 ==
            UnusedLevel65PhysicalCloneCandidateExporter.ExpectedOutputImageSha256 &&
        result.OutputImageSha256 == UnusedLevel65DisplayNameCandidateExporter.ExpectedOutputImageSha256 &&
        result.OutputImageSha256 == ExpectedOutputImageSha256 &&
        result.Plan.RequiresDuckStationRuntimeProof,
        "The display-name candidate lost its exact base, output, profile, or runtime-pending identity.");
    Require(
        result.Plan.BaseExecutableSha256 == BaseExecutableSha256 &&
        result.Plan.OutputExecutableSha256 == OutputExecutableSha256 &&
        result.Plan.LevelId == 65 &&
        result.Plan.ContinuousLevelIndex == 35 &&
        result.Plan.ExecutableLba == 55382 &&
        result.Plan.ExecutableByteLength == 0x66000,
        "The exact relocated SCUS or ID65 layout changed.");
    Require(
        result.Plan.NamePointerTableFileOffset == 0x5FFF0 &&
        result.Plan.NameTableIndex == 35 &&
        result.Plan.TargetNamePointerFileOffset == 0x6007C &&
        result.Plan.DonorNamePointerFileOffset == 0x5FFFC &&
        result.Plan.PlaceholderName == "A" &&
        result.Plan.AuthoredDisplayName == "TOWN SQUARE" &&
        NormalizeHex(result.Plan.TargetNamePointerBeforeHex) == "64550780" &&
        NormalizeHex(result.Plan.TargetNamePointerAfterHex) == "E4010180",
        "The exact slot-35 A-to-Town-Square pointer contract changed.");
    Require(
        result.Plan.LogicalPatches.Count == 1 &&
        result.Plan.LogicalPatches[0].File == "SCUS_942.28" &&
        result.Plan.LogicalPatches[0].Kind ==
            "alias-level-65-display-name-to-town-square" &&
        result.Plan.LogicalPatches[0].LogicalOffset == 0x6007C &&
        result.Plan.LogicalPatches[0].ByteLength == 4 &&
        NormalizeHex(result.Plan.LogicalPatches[0].BeforeHex) == "64550780" &&
        NormalizeHex(result.Plan.LogicalPatches[0].AfterHex) == "E4010180" &&
        result.ChangedLogicalExecutableBytes == 3 &&
        result.ChangedPhysicalImageBytes == 53 &&
        result.RebuiltRawSectorCount == 1 &&
        result.ChangedRawSectorCount == 1 &&
        result.ExactLogicalDiffBoundaryVerified &&
        result.ExactPhysicalSectorBoundaryVerified &&
        result.NamePointerReadbackVerified &&
        result.DisplayNameReadbackVerified &&
        result.ProtectedScopesPreserved &&
        result.BaseCandidatePreserved &&
        result.AtomicRenameCompleted,
        "The candidate omitted an exact logical, raw-sector, name, protected-scope, base, or publication proof.");
}

static string NormalizeHex(string value) =>
    new(value.Where(char.IsAsciiHexDigit).Select(char.ToUpperInvariant).ToArray());

static bool Contains(string value, string expected) =>
    value.Contains(expected, StringComparison.OrdinalIgnoreCase);

static async Task WriteTextAtomicallyAsync(string path, string content)
{
    string fullPath = Path.GetFullPath(path);
    string directory = Path.GetDirectoryName(fullPath) ??
        throw new InvalidOperationException("The sidecar output has no parent directory.");
    Directory.CreateDirectory(directory);
    string temporaryPath = Path.Combine(
        directory,
        $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
    try
    {
        byte[] bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(content);
        await using (FileStream stream = new(
                         temporaryPath,
                         FileMode.CreateNew,
                         FileAccess.Write,
                         FileShare.None,
                         bufferSize: 4096,
                         FileOptions.Asynchronous))
        {
            await stream.WriteAsync(bytes);
            await stream.FlushAsync();
            stream.Flush(flushToDisk: true);
        }
        File.Move(temporaryPath, fullPath, overwrite: true);
    }
    finally
    {
        if (File.Exists(temporaryPath))
            File.Delete(temporaryPath);
    }
}

static void RequireNoTransactionalDebris(string directory)
{
    bool hasDebris = Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
        .Select(Path.GetFileName)
        .Any(name =>
            name != null &&
            (name.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) ||
             name.EndsWith(".bak", StringComparison.OrdinalIgnoreCase) ||
             name.Contains(".tmp.", StringComparison.OrdinalIgnoreCase) ||
             name.Contains(".bak.", StringComparison.OrdinalIgnoreCase)));
    Require(!hasDebris, "The display-name exporter left a temporary or backup file behind.");
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
        {
            return cursor.FullName;
        }
        cursor = cursor.Parent;
    }
    throw new DirectoryNotFoundException("Could not locate the Spyro Editor repository root.");
}

static async Task<string> HashFileAsync(string path)
{
    await using FileStream stream = File.OpenRead(path);
    return Convert.ToHexString(await SHA256.HashDataAsync(stream)).ToLowerInvariant();
}

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
