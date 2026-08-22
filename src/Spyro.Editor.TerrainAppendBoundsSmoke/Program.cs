using System.Collections;
using System.Reflection;
using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;

const long UnsafeAppendBoundary = 0x6A3C4F8;
const long NextMappedSector = 0x6A3C530;
const long RejectedRepackWadOffset = 0x6A3C180;
const int RejectedRepackByteLength = 916;
const long FullyMappedSector214WadOffset = 0x6A3F9CC;
const int FullyMappedSector214ByteLength = 0x568;
const long FinalMappedSector215WadOffset = 0x6A3FF34;
const int FinalMappedSector215ByteLength = 0x4E8;
const long OcclusionComponentWadOffset = 0x6A4041C;
const int RejectedSuffixShiftBytes = 28;
const string ExpectedBaseSha256 =
    "9e42b43bd1341b40915748432d1b2dc760e22a81c0a320ec09ae6a71ca2efcd8";

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
    "The exact focused-runtime-passed ID65 display-name BIN/CUE is missing.");
Require(await HashFileAsync(baseImage) == ExpectedBaseSha256,
    "The ID65 append-bounds smoke base is not the exact focused-runtime-passed display-name BIN.");
Require(
    RejectedRepackWadOffset + RejectedRepackByteLength - UnsafeAppendBoundary == 28,
    "The pinned rejected v1 repack no longer overlaps the following scene-sector header by exactly 28 bytes.");

MethodInfo boundaryFinder = typeof(TerrainPatchExporter).GetMethod(
    "FindFirstInterveningSceneSectorWadOffset",
    BindingFlags.NonPublic | BindingFlags.Static)
    ?? throw new MissingMethodException(
        typeof(TerrainPatchExporter).FullName,
        "FindFirstInterveningSceneSectorWadOffset");
Type discImageType = typeof(TerrainPatchExporter).Assembly.GetType(
    "Spyro.Editor.Core.Exporting.DiscImage",
    throwOnError: true)!;
MethodInfo detectLayout = discImageType.GetMethod(
    "DetectLayout",
    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
    binder: null,
    types: [typeof(string)],
    modifiers: null)
    ?? throw new MissingMethodException(discImageType.FullName, "DetectLayout");
object layout = detectLayout.Invoke(null, [baseImage])
    ?? throw new InvalidOperationException("Could not detect the ID65 base BIN layout.");
MethodInfo suffixShiftPlanner = typeof(TerrainPatchExporter).GetMethod(
    "TryBuildTerrainSectorSuffixShiftPlan",
    BindingFlags.NonPublic | BindingFlags.Static)
    ?? throw new MissingMethodException(
        typeof(TerrainPatchExporter).FullName,
        "TryBuildTerrainSectorSuffixShiftPlan");
Type sourceSectorLocationType = typeof(TerrainPatchExporter).GetNestedType(
    "SourceSectorLocation",
    BindingFlags.NonPublic)
    ?? throw new TypeLoadException("Could not locate TerrainPatchExporter.SourceSectorLocation.");
Type assetSubfileInfoType = typeof(TerrainPatchExporter).GetNestedType(
    "AssetSubfileInfo",
    BindingFlags.NonPublic)
    ?? throw new TypeLoadException("Could not locate TerrainPatchExporter.AssetSubfileInfo.");
await using (FileStream input = File.OpenRead(baseImage))
{
    long boundary = (long)(boundaryFinder.Invoke(
        null,
        [input, layout, UnsafeAppendBoundary, NextMappedSector]) ?? -1L);
    Require(boundary == UnsafeAppendBoundary,
        $"Expected the omitted LP-only sector at WAD 0x{UnsafeAppendBoundary:X}, got 0x{boundary:X}.");

    Require(
        FullyMappedSector214WadOffset + FullyMappedSector214ByteLength ==
            FinalMappedSector215WadOffset &&
        FinalMappedSector215WadOffset + FinalMappedSector215ByteLength ==
            OcclusionComponentWadOffset,
        "The pinned final Town Square scene-sector chain no longer ends exactly at the occlusion component.");

    Type sourceMapType = typeof(Dictionary<,>).MakeGenericType(
        typeof(string),
        sourceSectorLocationType);
    IDictionary sourceMap = (IDictionary)(Activator.CreateInstance(sourceMapType)
        ?? throw new InvalidOperationException("Could not create the private source-sector map."));
    sourceMap.Add(
        "214:0:hp",
        CreatePrivateRecord(
            sourceSectorLocationType,
            "214:0:hp",
            "0x6A3F9CC",
            FullyMappedSector214WadOffset,
            FullyMappedSector214ByteLength));
    sourceMap.Add(
        "215:0:hp",
        CreatePrivateRecord(
            sourceSectorLocationType,
            "215:0:hp",
            "0x6A3FF34",
            FinalMappedSector215WadOffset,
            FinalMappedSector215ByteLength));
    object modelSubfileInfo = CreatePrivateRecord(
        assetSubfileInfoType,
        80,
        1,
        0L,
        0x6C18800L,
        0L,
        0x6C18800L,
        0L);
    object?[] suffixShiftArguments =
    [
        input,
        layout,
        sourceMap,
        modelSubfileInfo,
        Array.Empty<long>(),
        FullyMappedSector214WadOffset,
        FullyMappedSector214ByteLength,
        RejectedSuffixShiftBytes,
        null,
        null
    ];
    bool suffixShiftPlanned = (bool)(suffixShiftPlanner.Invoke(
        null,
        suffixShiftArguments) ?? false);
    string suffixShiftReason = suffixShiftArguments[9] as string ?? "";
    Require(
        !suffixShiftPlanned &&
        suffixShiftArguments[8] == null &&
        suffixShiftReason.Contains("disabled", StringComparison.OrdinalIgnoreCase) &&
        suffixShiftReason.Contains("environment-component", StringComparison.OrdinalIgnoreCase),
        "A packed final scene-sector suffix was still allowed to grow across the occlusion-component header.");
}

string outputRoot = Path.Combine(
    repositoryRoot,
    "_local",
    "smoke",
    "terrain-append-bounds",
    Guid.NewGuid().ToString("N"));
string outputPrefix = Path.Combine(outputRoot, "unsafe-id65-v1-must-not-publish");
string outputImage = outputPrefix + ".bin";
string outputCue = outputPrefix + ".cue";
bool rejected = false;
bool publishedOutput = false;
try
{
    await UnusedLevel65AuthoredTerrainAddCopyCandidateExporter.ExportAsync(
        new UnusedLevel65AuthoredTerrainAddCopyCandidateRequest(
            baseImage,
            baseCue,
            outputImage,
            outputCue));
}
catch (InvalidDataException ex) when (
    ex.Message.Contains("0 patch record(s)", StringComparison.Ordinal) &&
    ex.Message.Contains("expected 6", StringComparison.Ordinal))
{
    rejected = true;
}
finally
{
    publishedOutput = File.Exists(outputImage) || File.Exists(outputCue);
    if (Directory.Exists(outputRoot))
        Directory.Delete(outputRoot, recursive: true);
}

Require(!publishedOutput, "Rejected structural add-copy published a BIN or CUE.");
Require(rejected, "Unsafe ID65 v1 structural add-copy was not rejected before publication.");
Require(await HashFileAsync(baseImage) == ExpectedBaseSha256,
    "The append-bounds rejection changed its focused-runtime-passed base BIN.");

Console.WriteLine("Terrain structural-append bounds smoke passed.");
Console.WriteLine($"Protected following LP-only scene-sector header: WAD 0x{UnsafeAppendBoundary:X}.");
Console.WriteLine("Rejected v1 repack overlap prevented: 28 bytes.");
Console.WriteLine($"Protected occlusion-component header: WAD 0x{OcclusionComponentWadOffset:X}.");
Console.WriteLine("Scene-sector suffix shifting: disabled pending component-aware growth/rebase.");
Console.WriteLine("Unsafe ID65 v1 structural add-copy: rejected before BIN/CUE publication.");

static object CreatePrivateRecord(Type type, params object[] arguments) =>
    Activator.CreateInstance(
        type,
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
        binder: null,
        args: arguments,
        culture: null)
    ?? throw new InvalidOperationException($"Could not create private record {type.FullName}.");

static async Task<string> HashFileAsync(string path)
{
    await using FileStream input = File.OpenRead(path);
    return Convert.ToHexString(await SHA256.HashDataAsync(input)).ToLowerInvariant();
}

static string FindRepositoryRoot(string? requestedRoot)
{
    if (!string.IsNullOrWhiteSpace(requestedRoot))
    {
        string explicitRoot = Path.GetFullPath(requestedRoot);
        if (File.Exists(Path.Combine(
                explicitRoot,
                "src",
                "Spyro.Editor.Core",
                "Spyro.Editor.Core.csproj")))
        {
            return explicitRoot;
        }
    }

    DirectoryInfo? current = new(AppContext.BaseDirectory);
    while (current != null)
    {
        if (File.Exists(Path.Combine(
                current.FullName,
                "src",
                "Spyro.Editor.Core",
                "Spyro.Editor.Core.csproj")))
        {
            return current.FullName;
        }
        current = current.Parent;
    }

    throw new DirectoryNotFoundException("Could not locate the Spyro Editor repository root.");
}

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
