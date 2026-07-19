using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;

const int WadLba = 37;
const int ExpectedDescriptorCount = 23;
const int ExpectedTargetOwnedBytes = 5_632;
const int ExpectedPatchedBytes = 5_181;

bool cleanOutputs = args.Contains("--clean-output", StringComparer.OrdinalIgnoreCase);
string workspaceRoot = ResolveWorkspaceRoot(args);
string sourceImagePath = Path.Combine(workspaceRoot, "Spyro the Dragon (USA).bin");
string sourceCuePath = Path.Combine(workspaceRoot, "Spyro the Dragon (USA).cue");
Assert(File.Exists(sourceImagePath), $"Missing retail source BIN: {sourceImagePath}");
Assert(File.Exists(sourceCuePath), $"Missing retail source CUE: {sourceCuePath}");

LevelCatalog catalog = LevelCatalog.Load(workspaceRoot);
LevelDefinition artisans = catalog.FindByKey("artisans")
    ?? throw new InvalidOperationException("Artisans is missing from the level catalog.");
Assert(artisans.SourceWadEntry == 10,
    $"Artisans source WAD entry changed from the source-locked value 10 to {artisans.SourceWadEntry}.");

if (args.Contains("--preview-decoder-only", StringComparer.OrdinalIgnoreCase))
{
    await RunNativeTexturePreviewDecoderSmokeAsync(workspaceRoot, sourceImagePath, catalog);
    return;
}
if (args.Contains("--art-only-preserve-target-only", StringComparer.OrdinalIgnoreCase))
{
    await RunNativeUnreferencedArtOnlyExportSmokeAsync(
        workspaceRoot,
        sourceImagePath,
        sourceCuePath,
        catalog,
        cleanOutputs);
    return;
}

string outputRoot = Path.Combine(workspaceRoot, "_local", "smoke", "native-terrain-texture-export");
Directory.CreateDirectory(outputRoot);
string outputPrefix = Path.Combine(outputRoot, "SpyroEditor-Artisans-Dragon-GnastyMetal-NativeTextureExport");
string outputImagePath = $"{outputPrefix}.bin";
string outputCuePath = $"{outputPrefix}.cue";
string outputPlanPath = $"{outputPrefix}.terrain-patch-plan.json";
string manifestPath = Path.Combine(outputRoot, "artisans-native-terrain-texture-relocations.json");
string reportPath = Path.Combine(outputRoot, "artisans-dragon-gnasty-metal-export-smoke.json");
string markdownPath = Path.Combine(outputRoot, "artisans-dragon-gnasty-metal-export-smoke.md");
foreach (string stalePath in new[]
         {
             outputImagePath,
             outputCuePath,
             outputPlanPath,
             manifestPath,
             reportPath,
             markdownPath
         })
{
    if (File.Exists(stalePath))
        File.Delete(stalePath);
}

NativeTerrainTextureRelocationEdit edit = new(
    TargetTextureId: 54,
    DonorLevelKey: "gnastysworld",
    DonorLevelName: "Gnasty's World",
    DonorWadEntry: 70,
    DonorTextureId: 22,
    DonorRuntimeKey: "34:3:hp",
    DescriptorTier: NativeTerrainTextureRelocationEditStore.CompleteDescriptorTier,
    PreviewImagePath: "",
    PreviewImageName: "",
    CreatedAt: "2026-07-15T00:00:00.0000000+00:00");
await NativeTerrainTextureRelocationEditStore.SaveManifestAsync(
    manifestPath,
    artisans.Key,
    artisans.DisplayName,
    [edit]);
IReadOnlyList<NativeTerrainTextureRelocationEdit> manifestReadback =
    NativeTerrainTextureRelocationEditStore.LoadManifest(manifestPath, artisans.Key);
Assert(manifestReadback is [{ TargetTextureId: 54, DonorWadEntry: 70, DonorTextureId: 22, DescriptorTier: "both" }],
    "The focused native relocation manifest failed exact store readback.");

NativeTerrainTextureInPlaceTransplantRequest transplantRequest = new(
    TargetTextureId: 54,
    DonorWadEntry: 70,
    DonorTextureId: 22);
string sourceShaBefore = Sha256File(sourceImagePath);
long sourceLengthBefore = ReadFileLength(sourceImagePath);
NativeTerrainTextureInPlaceTransplantSourceProof sourceProof =
    NativeTerrainTextureInPlaceTransplantBuilder.InspectSourceProof(
        sourceImagePath,
        artisans,
        transplantRequest);

TerrainPatchPlan planned = TerrainPatchExporter.BuildPlan(
    sourceImagePath,
    sourceCuePath,
    outputImagePath,
    outputCuePath,
    artisans,
    ramPath: "",
    sourceSearchPath: "",
    terrainEditsPath: "",
    customTexturesPath: "",
    nativeTextureRelocationsPath: manifestPath);
AssertPlan(planned, "BuildPlan");

TerrainPatchResult result = await TerrainPatchExporter.ExportAsync(new TerrainPatchRequest(
    SourceImagePath: sourceImagePath,
    SourceCuePath: sourceCuePath,
    OutputPrefix: outputPrefix,
    Level: artisans,
    RamPath: "",
    SourceSearchPath: "",
    TerrainEditsPath: "",
    CustomTexturesPath: "",
    WriteImage: true,
    NativeTextureRelocationsPath: manifestPath));
Assert(result.WroteImage, "ExportAsync did not report a written image.");
Assert(File.Exists(outputImagePath) && File.Exists(outputCuePath) && File.Exists(outputPlanPath),
    "ExportAsync did not retain the focused BIN, CUE, and terrain patch plan.");
AssertPlan(result.Plan, "ExportAsync");
Assert(result.OutputImagePath == outputImagePath &&
       result.OutputCuePath == outputCuePath &&
       result.OutputPlanPath == outputPlanPath,
    "ExportAsync returned unexpected focused output paths.");

TerrainPatchPlan serializedPlan = JsonSerializer.Deserialize<TerrainPatchPlan>(
        await File.ReadAllTextAsync(outputPlanPath),
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
    ?? throw new InvalidDataException("The retained terrain patch plan could not be deserialized.");
AssertPlan(serializedPlan, "serialized plan");

DiscLayoutInfo disc = DetectDiscLayout(sourceImagePath);
Assert(ReadFileLength(outputImagePath) == sourceLengthBefore,
    "The candidate BIN length differs from the retail source image.");
foreach (TerrainPatch patch in result.Plan.Patches)
{
    long wadOffset = ParseHexLong(patch.WadRelativeOffset);
    byte[] before = ParsePatchBytes(patch.BeforeHexPreview);
    byte[] after = ParsePatchBytes(patch.AfterHexPreview);
    Assert(before.Length == patch.ByteLength && after.Length == patch.ByteLength,
        $"Patch {patch.Label} has a length/hex mismatch.");
    Assert(before.Where((value, index) => value != after[index]).Count() == patch.ByteLength,
        $"Patch {patch.Label} contains unchanged bytes.");
    Assert(ReadLogicalWadBytes(sourceImagePath, disc, wadOffset, before.Length).SequenceEqual(before),
        $"Retail before-byte readback failed for {patch.Label}.");
    Assert(ReadLogicalWadBytes(outputImagePath, disc, wadOffset, after.Length).SequenceEqual(after),
        $"Candidate after-byte readback failed for {patch.Label}.");
}

ExactDiffResult exactDiff = CompareExactPhysicalDiff(sourceImagePath, outputImagePath, disc, result.Plan.Patches);
Assert(exactDiff.UnexpectedDifferenceCount == 0,
    $"The candidate contains {exactDiff.UnexpectedDifferenceCount:N0} byte differences outside the retained plan.");
Assert(exactDiff.ExpectedDifferenceCount == ExpectedPatchedBytes &&
       exactDiff.ObservedExpectedDifferenceCount == ExpectedPatchedBytes,
    $"Exact BIN comparison observed {exactDiff.ObservedExpectedDifferenceCount:N0}/{exactDiff.ExpectedDifferenceCount:N0} expected differences.");

NativeTerrainTextureInPlaceTransplantSourceProof outputProof =
    NativeTerrainTextureInPlaceTransplantBuilder.InspectSourceProof(
        outputImagePath,
        artisans,
        transplantRequest);
Assert(outputProof.TargetDescriptorTableByteLength == sourceProof.TargetDescriptorTableByteLength &&
       string.Equals(outputProof.TargetDescriptorTableSha256, sourceProof.TargetDescriptorTableSha256, StringComparison.OrdinalIgnoreCase),
    "The Artisans descriptor table changed in the exported BIN.");
Assert(outputProof.DonorTexturePagesByteLength == sourceProof.DonorTexturePagesByteLength &&
       string.Equals(outputProof.DonorTexturePagesSha256, sourceProof.DonorTexturePagesSha256, StringComparison.OrdinalIgnoreCase),
    "The Gnasty's World donor texture-page source changed in the exported BIN.");
Assert(outputProof.DonorDescriptorTableByteLength == sourceProof.DonorDescriptorTableByteLength &&
       string.Equals(outputProof.DonorDescriptorTableSha256, sourceProof.DonorDescriptorTableSha256, StringComparison.OrdinalIgnoreCase),
    "The Gnasty's World donor descriptor table changed in the exported BIN.");
Assert(!string.Equals(outputProof.TargetTexturePagesSha256, sourceProof.TargetTexturePagesSha256, StringComparison.OrdinalIgnoreCase),
    "The Artisans target texture pages did not change.");

string sourceShaAfter = Sha256File(sourceImagePath);
Assert(ReadFileLength(sourceImagePath) == sourceLengthBefore &&
       string.Equals(sourceShaAfter, sourceShaBefore, StringComparison.OrdinalIgnoreCase),
    "The retail source BIN was modified by BuildPlan or ExportAsync.");
string outputSha = Sha256File(outputImagePath);

string cueText = await File.ReadAllTextAsync(outputCuePath, Encoding.ASCII);
Assert(cueText.Contains(Path.GetFileName(outputImagePath), StringComparison.Ordinal),
    "The focused CUE does not reference the focused BIN.");

var report = new
{
    GeneratedAtUtc = DateTimeOffset.UtcNow,
    Passed = true,
    Pair = new
    {
        TargetLevel = artisans.DisplayName,
        TargetWadEntry = artisans.SourceWadEntry,
        TargetTextureId = 54,
        DonorLevel = "Gnasty's World",
        DonorWadEntry = 70,
        DonorTextureId = 22
    },
    Strategy = result.Plan.NativeTextureRelocations.Single().Strategy,
    CompleteDescriptorCount = result.Plan.NativeTextureRelocations.Single().CompleteDescriptorCount,
    TargetOwnedByteCount = result.Plan.NativeTextureRelocations.Single().TargetOwnedByteCount,
    PatchCount = result.Plan.PatchCount,
    PatchedByteCount = result.Plan.TotalPatchedBytes,
    ExactPhysicalDiff = exactDiff,
    Source = new { Path = sourceImagePath, Length = sourceLengthBefore, Sha256 = sourceShaAfter, Unchanged = true },
    Output = new { BinPath = outputImagePath, CuePath = outputCuePath, PlanPath = outputPlanPath, Length = sourceLengthBefore, Sha256 = outputSha },
    ManifestPath = manifestPath,
    TargetDescriptorTable = new
    {
        sourceProof.TargetDescriptorTableByteLength,
        Sha256 = sourceProof.TargetDescriptorTableSha256,
        Unchanged = true
    },
    DonorTexturePages = new
    {
        sourceProof.DonorTexturePagesByteLength,
        Sha256 = sourceProof.DonorTexturePagesSha256,
        Unchanged = true
    },
    DonorDescriptorTable = new
    {
        sourceProof.DonorDescriptorTableByteLength,
        Sha256 = sourceProof.DonorDescriptorTableSha256,
        Unchanged = true
    },
    Proofs = result.Plan.NativeTextureRelocations.Single()
};
await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
await File.WriteAllTextAsync(markdownPath, $"""
    # Native Terrain Texture Export Smoke

    Status: **PASSED**

    - Pair: Artisans WAD 10 texture 54 <- Gnasty's World WAD 70 texture 22.
    - Export strategy: `target-owned-in-place`.
    - Complete descriptors: {ExpectedDescriptorCount}.
    - Target-owned physical footprint: {ExpectedTargetOwnedBytes:N0} bytes.
    - Exact changed/output-readback bytes: {ExpectedPatchedBytes:N0}.
    - Retail source BIN remained SHA-256 `{sourceShaAfter}`.
    - Output BIN SHA-256: `{outputSha}`.
    - Artisans descriptor-table SHA-256 remained `{sourceProof.TargetDescriptorTableSha256}`.
    - Gnasty's World donor pages and descriptor table remained byte-identical.

    Runtime candidate: `{outputCuePath}`

    Retained patch plan: `{outputPlanPath}`
    """);

MissingSurfaceAppendEvidence missingSurfaceAppend = await RunMissingSurfaceDescriptorAppendAsync(
    workspaceRoot,
    sourceImagePath,
    sourceCuePath,
    catalog,
    sourceShaBefore,
    sourceLengthBefore);

Console.WriteLine("Native terrain texture exporter smoke passed.");
Console.WriteLine("Pair: Artisans WAD 10 texture 54 <- Gnasty's World WAD 70 texture 22.");
Console.WriteLine($"Strategy: target-owned-in-place; descriptors: {ExpectedDescriptorCount}; target-owned bytes: {ExpectedTargetOwnedBytes:N0}; patched bytes: {ExpectedPatchedBytes:N0}.");
Console.WriteLine($"Source SHA-256 unchanged: {sourceShaAfter}");
Console.WriteLine($"Output SHA-256: {outputSha}");
Console.WriteLine($"BIN: {outputImagePath}");
Console.WriteLine($"CUE: {outputCuePath}");
Console.WriteLine($"Plan: {outputPlanPath}");
Console.WriteLine($"Report: {reportPath}");
Console.WriteLine(
    $"Missing-descriptor append: {missingSurfaceAppend.TargetLevel} texture {missingSurfaceAppend.TargetTextureId} <- " +
    $"{missingSurfaceAppend.DonorLevel} texture {missingSurfaceAppend.DonorTextureId}; " +
    $"surfaces {missingSurfaceAppend.OldSurfaceCount}->{missingSurfaceAppend.NewSurfaceCount}; " +
    $"descriptor growth {missingSurfaceAppend.DescriptorGrowthBytes} bytes; exact changed bytes {missingSurfaceAppend.ExactChangedByteCount:N0}.");
Console.WriteLine($"Missing-descriptor CUE: {missingSurfaceAppend.OutputCuePath}");
Console.WriteLine($"Missing-descriptor report: {missingSurfaceAppend.ReportPath}");
if (cleanOutputs)
{
    DeleteOutput(outputImagePath);
    DeleteOutput(outputCuePath);
    DeleteOutput(Path.ChangeExtension(missingSurfaceAppend.OutputCuePath, ".bin"));
    DeleteOutput(missingSurfaceAppend.OutputCuePath);
    Console.WriteLine("Disposable terrain texture smoke BIN/CUE pairs were removed after exact write/readback verification.");
}

static async Task RunNativeUnreferencedArtOnlyExportSmokeAsync(
    string workspaceRoot,
    string sourceImagePath,
    string sourceCuePath,
    LevelCatalog catalog,
    bool cleanOutputs)
{
    const int TargetTextureId = 54;
    const int DonorTextureId = 10;
    LevelDefinition target = catalog.FindByKey("artisans")
        ?? throw new InvalidOperationException("Artisans is missing from the level catalog.");
    LevelDefinition donor = catalog.FindByKey("beastmakers")
        ?? throw new InvalidOperationException("Beast Makers is missing from the level catalog.");
    Assert(target.SourceWadEntry == 10 && donor.SourceWadEntry == 46,
        "The focused Artisans/Beast Makers WAD identities changed from 10/46.");

    string outputRoot = Path.Combine(workspaceRoot, "_local", "smoke", "native-terrain-texture-export");
    Directory.CreateDirectory(outputRoot);
    string outputPrefix = Path.Combine(outputRoot, "SpyroEditor-Artisans-T54-From-BeastMakers-T10-ArtOnly");
    string outputImagePath = $"{outputPrefix}.bin";
    string outputCuePath = $"{outputPrefix}.cue";
    string outputPlanPath = $"{outputPrefix}.terrain-patch-plan.json";
    string manifestPath = Path.Combine(outputRoot, "artisans-native-unreferenced-art-only-relocation.json");
    string reportPath = Path.Combine(outputRoot, "artisans-native-unreferenced-art-only-export-smoke.json");
    string markdownPath = Path.Combine(outputRoot, "artisans-native-unreferenced-art-only-export-smoke.md");
    foreach (string path in new[] { outputImagePath, outputCuePath, outputPlanPath, manifestPath, reportPath, markdownPath })
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    (string LevelKey, int TextureId)[] exactNativeUnreferencedDonors =
    [
        ("beastmakers", 10), ("beastmakers", 13), ("beastmakers", 15), ("beastmakers", 16),
        ("beastmakers", 17), ("beastmakers", 19), ("beastmakers", 25), ("beastmakers", 27),
        ("beastmakers", 28), ("beastmakers", 34), ("beastmakers", 39), ("beastmakers", 43),
        ("beastmakers", 48), ("beastmakers", 50), ("beastmakers", 53), ("beastmakers", 54),
        ("beastmakers", 58), ("icyflight", 11), ("peacekeepers", 11)
    ];
    List<string> allDonorProofs = [];
    foreach ((string donorLevelKey, int donorTextureId) in exactNativeUnreferencedDonors)
    {
        LevelDefinition exactDonor = catalog.FindByKey(donorLevelKey)
            ?? throw new InvalidOperationException($"{donorLevelKey} is missing from the level catalog.");
        await NativeTerrainTextureRelocationEditStore.SaveManifestAsync(
            manifestPath,
            target.Key,
            target.DisplayName,
            [
                new NativeTerrainTextureRelocationEdit(
                    TargetTextureId,
                    exactDonor.Key,
                    exactDonor.DisplayName,
                    exactDonor.SourceWadEntry,
                    donorTextureId,
                    NativeTerrainTextureRelocationEditStore.BuildTextureRecordProvenanceKey(exactDonor.Key, donorTextureId),
                    NativeTerrainTextureRelocationEditStore.CompleteDescriptorTier,
                    "",
                    "",
                    "2026-07-16T00:00:00.0000000+00:00",
                    NativeTerrainTextureRelocationApplyMode.ArtOnlyPreserveTarget)
            ]);
        TerrainPatchPlan donorPlan = TerrainPatchExporter.BuildPlan(
            sourceImagePath,
            sourceCuePath,
            outputImagePath,
            outputCuePath,
            target,
            ramPath: "",
            sourceSearchPath: "",
            terrainEditsPath: "",
            customTexturesPath: "",
            nativeTextureRelocationsPath: manifestPath);
        AssertArtOnlyPlan(donorPlan, $"{exactDonor.DisplayName} texture {donorTextureId} preflight");
        NativeTerrainTextureRelocationPatchSummary donorSummary = donorPlan.NativeTextureRelocations.Single();
        allDonorProofs.Add($"{exactDonor.Key}:T{donorTextureId}:{donorSummary.Strategy}:{donorSummary.PatchedByteCount}");
    }
    Assert(allDonorProofs.Count == 19,
        $"The exact art-only export preflight covered {allDonorProofs.Count}/19 native-unreferenced static donors.");

    await NativeTerrainTextureRelocationEditStore.SaveManifestAsync(
        manifestPath,
        target.Key,
        target.DisplayName,
        [
            new NativeTerrainTextureRelocationEdit(
                TargetTextureId,
                donor.Key,
                donor.DisplayName,
                donor.SourceWadEntry,
                DonorTextureId,
                NativeTerrainTextureRelocationEditStore.BuildTextureRecordProvenanceKey(donor.Key, DonorTextureId),
                NativeTerrainTextureRelocationEditStore.CompleteDescriptorTier,
                "",
                "",
                "2026-07-16T00:00:00.0000000+00:00",
                NativeTerrainTextureRelocationApplyMode.ArtOnlyPreserveTarget)
        ]);
    NativeTerrainTextureRelocationEdit saved = NativeTerrainTextureRelocationEditStore
        .LoadManifest(manifestPath, target.Key)
        .Single();
    Assert(saved.PreservesTargetNativeSurface &&
           saved.DonorProvenanceKey == "native-texture-record:beastmakers:10",
        "The focused export manifest lost art-only mode or canonical texture-record provenance.");

    string sourceShaBefore = Sha256File(sourceImagePath);
    long sourceLengthBefore = ReadFileLength(sourceImagePath);
    NativeTerrainTextureInPlaceTransplantRequest focusedRequest = new(
        TargetTextureId,
        donor.SourceWadEntry,
        DonorTextureId);
    NativeTerrainTextureInPlaceTransplantSourceProof focusedSourceProof =
        NativeTerrainTextureInPlaceTransplantBuilder.InspectSourceProof(
            sourceImagePath,
            target,
            focusedRequest);
    TerrainPatchPlan plan = TerrainPatchExporter.BuildPlan(
        sourceImagePath,
        sourceCuePath,
        outputImagePath,
        outputCuePath,
        target,
        ramPath: "",
        sourceSearchPath: "",
        terrainEditsPath: "",
        customTexturesPath: "",
        nativeTextureRelocationsPath: manifestPath);
    AssertArtOnlyPlan(plan, "BuildPlan");

    TerrainPatchResult result = await TerrainPatchExporter.ExportAsync(new TerrainPatchRequest(
        sourceImagePath,
        sourceCuePath,
        outputPrefix,
        target,
        RamPath: "",
        SourceSearchPath: "",
        TerrainEditsPath: "",
        CustomTexturesPath: "",
        WriteImage: true,
        NativeTextureRelocationsPath: manifestPath));
    Assert(result.WroteImage && File.Exists(outputImagePath) && File.Exists(outputCuePath) && File.Exists(outputPlanPath),
        "Art-only ExportAsync did not retain its BIN/CUE/plan.");
    AssertArtOnlyPlan(result.Plan, "ExportAsync");
    TerrainPatchPlan serialized = JsonSerializer.Deserialize<TerrainPatchPlan>(
            await File.ReadAllTextAsync(outputPlanPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
        ?? throw new InvalidDataException("The art-only retained plan could not be deserialized.");
    AssertArtOnlyPlan(serialized, "serialized plan");

    DiscLayoutInfo disc = DetectDiscLayout(sourceImagePath);
    foreach (TerrainPatch patch in result.Plan.Patches)
    {
        long wadOffset = ParseHexLong(patch.WadRelativeOffset);
        byte[] before = ParsePatchBytes(patch.BeforeHexPreview);
        byte[] after = ParsePatchBytes(patch.AfterHexPreview);
        Assert(ReadLogicalWadBytes(sourceImagePath, disc, wadOffset, before.Length).SequenceEqual(before),
            $"Art-only retail before-byte readback failed for {patch.Label}.");
        Assert(ReadLogicalWadBytes(outputImagePath, disc, wadOffset, after.Length).SequenceEqual(after),
            $"Art-only output after-byte readback failed for {patch.Label}.");
    }
    ExactDiffResult exactDiff = CompareExactPhysicalDiff(sourceImagePath, outputImagePath, disc, result.Plan.Patches);
    Assert(exactDiff.UnexpectedDifferenceCount == 0 &&
           exactDiff.ExpectedDifferenceCount > 0 &&
           exactDiff.ObservedExpectedDifferenceCount == exactDiff.ExpectedDifferenceCount,
        "Art-only output contains an absent planned difference or any byte difference outside native texture relocation patches.");
    Assert(ReadFileLength(outputImagePath) == sourceLengthBefore &&
           ReadFileLength(sourceImagePath) == sourceLengthBefore &&
           Sha256File(sourceImagePath) == sourceShaBefore,
        "Art-only export changed the retail source or fixed output length.");
    NativeTerrainTextureInPlaceTransplantSourceProof focusedOutputProof =
        NativeTerrainTextureInPlaceTransplantBuilder.InspectSourceProof(
            outputImagePath,
            target,
            focusedRequest);
    Assert(focusedOutputProof.TargetDescriptorTableByteLength == focusedSourceProof.TargetDescriptorTableByteLength &&
           string.Equals(
               focusedOutputProof.TargetDescriptorTableSha256,
               focusedSourceProof.TargetDescriptorTableSha256,
               StringComparison.OrdinalIgnoreCase),
        "The final art-only BIN changed the target descriptor table, so target ABR/material control preservation was not byte-exact.");

    NativeTerrainTextureRelocationPatchSummary focusedSummary = result.Plan.NativeTextureRelocations.Single();
    string outputSha = Sha256File(outputImagePath);
    await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(new
    {
        GeneratedAtUtc = DateTimeOffset.UtcNow,
        Passed = true,
        ExactNativeUnreferencedDonorCount = allDonorProofs.Count,
        ExactNativeUnreferencedDonorPlans = allDonorProofs,
        FocusedPair = new
        {
            TargetLevel = target.DisplayName,
            TargetWadEntry = target.SourceWadEntry,
            TargetTextureId,
            DonorLevel = donor.DisplayName,
            DonorWadEntry = donor.SourceWadEntry,
            DonorTextureId,
            ApplyMode = NativeTerrainTextureRelocationEditStore.ArtOnlyPreserveTargetMode,
            Provenance = saved.DonorProvenanceKey
        },
        ExactPhysicalDiff = exactDiff,
        PatchKinds = result.Plan.Patches.Select(patch => patch.Kind).Distinct().Order().ToArray(),
        Proofs = focusedSummary,
        TargetDescriptorTable = new
        {
            focusedSourceProof.TargetDescriptorTableByteLength,
            Sha256 = focusedSourceProof.TargetDescriptorTableSha256,
            Unchanged = true
        },
        Source = new { Path = sourceImagePath, Length = sourceLengthBefore, Sha256 = sourceShaBefore, Unchanged = true },
        Output = new { BinPath = outputImagePath, CuePath = outputCuePath, PlanPath = outputPlanPath, Sha256 = outputSha },
        ManifestPath = manifestPath
    }, new JsonSerializerOptions { WriteIndented = true }));
    await File.WriteAllTextAsync(markdownPath, $"""
        # Native-Unreferenced Art-Only Terrain Export Smoke

        Status: **PASSED**

        - Exact source role set: **{allDonorProofs.Count}/19** complete-record plans passed against Artisans texture {TargetTextureId}.
        - Focused output: Artisans WAD {target.SourceWadEntry} texture {TargetTextureId} <- Beast Makers WAD {donor.SourceWadEntry} texture {DonorTextureId}.
        - Apply mode: `{NativeTerrainTextureRelocationEditStore.ArtOnlyPreserveTargetMode}`.
        - Canonical provenance: `{saved.DonorProvenanceKey}`.
        - Strategy: `{focusedSummary.Strategy}`; descriptors: {focusedSummary.CompleteDescriptorCount}; exact changed bytes: {exactDiff.ExpectedDifferenceCount:N0}.
        - Patch kinds are native-terrain-texture only; target face IDs, HP material/semitransparency bits, tint, editor material, and collision/surface bytes receive no patch.
        - The target descriptor table remained byte-identical at SHA-256 `{focusedSourceProof.TargetDescriptorTableSha256}`, locking target ABR/material controls in the final BIN.
        - Retail source SHA-256 remained `{sourceShaBefore}`.
        - Output SHA-256: `{outputSha}`.

        Runtime candidate: `{outputCuePath}`

        Retained patch plan: `{outputPlanPath}`
        """);

    Console.WriteLine("Native-unreferenced art-only terrain export smoke passed.");
    Console.WriteLine($"All exact donors preflighted: {allDonorProofs.Count}/19 complete-record plans.");
    Console.WriteLine($"Pair: Artisans texture {TargetTextureId} <- Beast Makers native-unreferenced record {DonorTextureId}.");
    Console.WriteLine($"Strategy: {result.Plan.NativeTextureRelocations.Single().Strategy}; exact changed bytes: {exactDiff.ExpectedDifferenceCount:N0}; no face/material/tint/collision patches.");
    Console.WriteLine($"BIN: {outputImagePath}");
    Console.WriteLine($"CUE: {outputCuePath}");
    Console.WriteLine($"Plan: {outputPlanPath}");
    Console.WriteLine($"Report: {reportPath}");

    if (cleanOutputs)
    {
        DeleteOutput(outputImagePath);
        DeleteOutput(outputCuePath);
    }
}

static void AssertArtOnlyPlan(TerrainPatchPlan plan, string stage)
{
    Assert(plan.NativeTextureRelocationCount == 1 &&
           plan.NativeTextureRelocations.Count == 1 &&
           plan.PatchCount > 0 &&
           plan.SkippedEdits.Count == 0,
        $"{stage}: art-only relocation was missing, empty, or partially skipped.");
    Assert(plan.Patches.All(patch =>
            patch.Kind.StartsWith("native-terrain-texture-", StringComparison.OrdinalIgnoreCase)),
        $"{stage}: art-only plan emitted a face, tint, scene, material, collision, or surface-property patch.");
    Assert(plan.NativeTextureRelocationBytePatchCount == plan.PatchCount,
        $"{stage}: art-only plan contains a patch outside the native relocation count.");
    NativeTerrainTextureRelocationPatchSummary summary = plan.NativeTextureRelocations.Single();
    Assert(summary.CompleteDescriptorCount == 23 &&
           summary.RuntimeControlVerified &&
           summary.OwnershipVerified &&
           summary.ExactIndexedPixelsVerified &&
           summary.ExactPalettesVerified &&
           summary.LogicalReadbackVerified &&
           summary.TargetDescriptorMaterialPolicyVerified &&
           summary.Notes.Any(note => note.Contains("art-only preserve-target mode", StringComparison.OrdinalIgnoreCase)),
        $"{stage}: art-only relocation omitted complete-record, runtime, ownership, exact-readback, or preserve-target evidence.");
}

static async Task<MissingSurfaceAppendEvidence> RunMissingSurfaceDescriptorAppendAsync(
    string workspaceRoot,
    string sourceImagePath,
    string sourceCuePath,
    LevelCatalog catalog,
    string expectedSourceSha256,
    long expectedSourceLength)
{
    const int DonorTextureId = 23;
    NativeTerrainSurfaceSignature water = new(0, 0, 0);
    LevelDefinition targetLevel = catalog.FindByKey("toasty")
        ?? throw new InvalidOperationException("Toasty is missing from the level catalog.");
    LevelDefinition donorLevel = catalog.FindByKey("artisans")
        ?? throw new InvalidOperationException("Artisans is missing from the level catalog.");
    Assert(targetLevel.SourceWadEntry == 18 && donorLevel.SourceWadEntry == 10,
        "The Toasty/Artisans WAD entries changed from their source-locked values 18/10.");

    string targetOverlayPath = Path.Combine(workspaceRoot, "editor-cache", "toasty-runtime-scene-editor-overlay.json");
    string donorOverlayPath = Path.Combine(workspaceRoot, "editor-cache", "artisans-runtime-scene-editor-overlay.json");
    Assert(File.Exists(targetOverlayPath) && File.Exists(donorOverlayPath),
        "The real missing-descriptor smoke requires the source-derived Toasty and Artisans terrain overlays.");
    GeometryCandidate targetGeometry = GeometryOverlayLoader.LoadFirstCandidate(targetOverlayPath);
    GeometryCandidate donorGeometry = GeometryOverlayLoader.LoadFirstCandidate(donorOverlayPath);
    NativeTerrainSurfaceSourceData targetSource = PortalSourceDataLocator.LocateTerrainSurfaces(sourceImagePath, targetLevel);
    NativeTerrainSurfaceSourceData donorSource = PortalSourceDataLocator.LocateTerrainSurfaces(sourceImagePath, donorLevel);
    Assert(!targetSource.SpecialSurfaces.Any(surface =>
            surface.Type == water.SurfaceType && surface.Param1 == water.Param1 && surface.Param2 == water.Param2),
        "Toasty unexpectedly already contains the focused damaging-water descriptor; this would not exercise append.");
    PortalSpecialSurfaceRecord donorWaterDescriptor = donorSource.SpecialSurfaces.Single(surface =>
        surface.Type == water.SurfaceType && surface.Param1 == water.Param1 && surface.Param2 == water.Param2);

    NativeTerrainSurfaceLevelCatalog targetSurfaceCatalog =
        NativeTerrainSurfaceCatalogBuilder.Build(targetLevel, targetGeometry, targetSource);
    NativeTerrainSurfaceLevelCatalog donorSurfaceCatalog =
        NativeTerrainSurfaceCatalogBuilder.Build(donorLevel, donorGeometry, donorSource);
    NativeTerrainTextureSurfaceVariant donorWaterVariant = donorSurfaceCatalog.TextureVariants.Single(variant =>
        variant.TextureId == DonorTextureId && variant.Signature == water);
    (int TextureId, TerrainPolygon[] Faces, NativeTerrainSurfaceBatchTransferReadiness Readiness)[] behaviorCandidates =
        targetGeometry.Polygons
            .GroupBy(face => face.OriginalTextureId)
            .Select(group =>
            {
                TerrainPolygon[] faces = group.OrderBy(face => face.RuntimeKey, StringComparer.OrdinalIgnoreCase).ToArray();
                NativeTerrainSurfaceBatchTransferReadiness candidateReadiness = targetSurfaceCatalog.EvaluateBatchTransfer(
                    faces.Select(face => face.RuntimeKey),
                    water,
                    crossLevel: true);
                return (TextureId: group.Key, Faces: faces, Readiness: candidateReadiness);
            })
            .Where(candidate =>
                candidate.Readiness.CanApply &&
                candidate.Readiness.HasCompleteFaceCoverage &&
                candidate.Readiness.TargetSurfaceIndex == targetSource.SpecialSurfaces.Count)
            .OrderBy(candidate => candidate.Faces.Length)
            .ThenBy(candidate => candidate.TextureId)
            .ToArray();
    (int TextureId, TerrainPolygon[] Faces, NativeTerrainSurfaceBatchTransferReadiness Readiness)? selected = null;
    List<string> artCandidateFailures = [];
    foreach ((int textureId, TerrainPolygon[] faces, NativeTerrainSurfaceBatchTransferReadiness candidateReadiness) in behaviorCandidates)
    {
        try
        {
            NativeTerrainTextureInPlaceTransplantRequest candidateRequest = new(textureId, donorLevel.SourceWadEntry, DonorTextureId);
            NativeTerrainTextureInPlaceTransplantSourceProof candidateProof =
                NativeTerrainTextureInPlaceTransplantBuilder.InspectSourceProof(sourceImagePath, targetLevel, candidateRequest);
            if (NativeTerrainTextureInPlaceTransplantBuilder.TryBuild(
                    sourceImagePath,
                    targetLevel,
                    candidateRequest,
                    candidateProof,
                    out NativeTerrainTextureInPlaceTransplantPlan? candidatePlan,
                    out string candidateFailure) &&
                candidatePlan != null)
            {
                selected = (textureId, faces, candidateReadiness);
                break;
            }
            artCandidateFailures.Add($"texture {textureId}: {candidateFailure}");
        }
        catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or IOException or ArgumentOutOfRangeException)
        {
            artCandidateFailures.Add($"texture {textureId}: {ex.Message}");
        }
    }
    Assert(selected.HasValue,
        $"No complete Toasty missing-water behavior target also supports the Artisans texture {DonorTextureId} art transplant. " +
        string.Join(" | ", artCandidateFailures.Take(5)));
    int targetTextureId = selected!.Value.TextureId;
    TerrainPolygon[] affectedFaces = selected.Value.Faces;
    NativeTerrainSurfaceBatchTransferReadiness readiness = selected.Value.Readiness;

    string outputRoot = Path.Combine(workspaceRoot, "_local", "smoke", "native-terrain-texture-export");
    string outputPrefix = Path.Combine(outputRoot, $"SpyroEditor-Toasty-Texture{targetTextureId}-To-Artisans-DamagingWater-Append");
    string outputImagePath = $"{outputPrefix}.bin";
    string outputCuePath = $"{outputPrefix}.cue";
    string outputPlanPath = $"{outputPrefix}.terrain-patch-plan.json";
    string editsPath = Path.Combine(outputRoot, $"toasty-texture{targetTextureId}-damaging-water-terrain-edits.json");
    string sourceSearchPath = Path.Combine(outputRoot, $"toasty-texture{targetTextureId}-source-derived-terrain-source-search-native.json");
    string manifestPath = Path.Combine(outputRoot, "toasty-native-terrain-texture-relocations.json");
    string reportPath = Path.Combine(outputRoot, "toasty-missing-water-descriptor-export-smoke.json");
    string markdownPath = Path.Combine(outputRoot, "toasty-missing-water-descriptor-export-smoke.md");
    foreach (string stalePath in new[]
             {
                 outputImagePath,
                 outputCuePath,
                 outputPlanPath,
                 editsPath,
                 sourceSearchPath,
                 manifestPath,
                 reportPath,
                 markdownPath
             })
    {
        if (File.Exists(stalePath))
            File.Delete(stalePath);
    }

    TerrainSurfaceBehaviorEdit stagedBehavior = new(
        water.SurfaceType,
        water.Param1,
        water.Param2,
        donorLevel.Key,
        donorWaterVariant.RepresentativeRuntimeKey,
        water.Label);
    foreach (TerrainPolygon face in affectedFaces)
        face.ApplySurfaceBehaviorEdit(stagedBehavior);
    int savedEditCount = await TerrainEditStore.SaveAsync(
        editsPath,
        affectedFaces,
        $"Toasty shared texture {targetTextureId} to Artisans damaging-water texture {DonorTextureId} append proof");
    Assert(savedEditCount == affectedFaces.Length,
        $"The shared Toasty behavior edit saved {savedEditCount}/{affectedFaces.Length} affected faces.");

    TerrainSourceSearchResult sourceSearch = await TerrainSourceSearchBuilder.BuildSourceDerivedAsync(
        new SourceDerivedTerrainSourceSearchRequest(
            sourceImagePath,
            sourceSearchPath,
            targetLevel,
            targetGeometry));
    string[] affectedRuntimeKeys = affectedFaces.Select(face => face.RuntimeKey).ToArray();
    TerrainSourceSearchEntry[] mappedAffectedFaces = sourceSearch.Report.Results
        .Where(entry => affectedRuntimeKeys.Contains(entry.Edit, StringComparer.OrdinalIgnoreCase))
        .ToArray();
    Assert(mappedAffectedFaces.Length == affectedFaces.Length &&
           mappedAffectedFaces.All(entry => entry.FullSectorHits.Count == 1),
        "The source-derived Toasty map did not bind every affected texture-59 face exactly once.");

    NativeTerrainTextureRelocationEdit relocation = new(
        targetTextureId,
        donorLevel.Key,
        donorLevel.DisplayName,
        donorLevel.SourceWadEntry,
        DonorTextureId,
        donorWaterVariant.RepresentativeRuntimeKey,
        NativeTerrainTextureRelocationEditStore.CompleteDescriptorTier,
        "",
        "",
        "2026-07-15T00:00:00.0000000+00:00");
    await NativeTerrainTextureRelocationEditStore.SaveManifestAsync(
        manifestPath,
        targetLevel.Key,
        targetLevel.DisplayName,
        [relocation]);
    NativeTerrainTextureRelocationEdit manifestReadback =
        NativeTerrainTextureRelocationEditStore.LoadManifest(manifestPath, targetLevel.Key).Single();
    Assert(manifestReadback.TargetTextureId == targetTextureId &&
           manifestReadback.DonorWadEntry == donorLevel.SourceWadEntry &&
           manifestReadback.DonorTextureId == DonorTextureId,
        "The Toasty first-class relocation manifest failed exact readback.");

    TerrainPatchPlan preflight = TerrainPatchExporter.BuildPlan(
        sourceImagePath,
        sourceCuePath,
        outputImagePath,
        outputCuePath,
        targetLevel,
        ramPath: "",
        sourceSearchPath,
        editsPath,
        customTexturesPath: "",
        nativeTextureRelocationsPath: manifestPath);
    AssertMissingSurfaceAppendPlan(preflight, "BuildPlan", targetSource, affectedFaces.Length, targetTextureId, DonorTextureId);

    TerrainPatchResult result = await TerrainPatchExporter.ExportAsync(new TerrainPatchRequest(
        sourceImagePath,
        sourceCuePath,
        outputPrefix,
        targetLevel,
        RamPath: "",
        SourceSearchPath: sourceSearchPath,
        TerrainEditsPath: editsPath,
        CustomTexturesPath: "",
        WriteImage: true,
        NativeTextureRelocationsPath: manifestPath));
    Assert(result.WroteImage && File.Exists(outputImagePath) && File.Exists(outputCuePath) && File.Exists(outputPlanPath),
        "The missing-descriptor ExportAsync path did not retain its BIN/CUE/plan.");
    AssertMissingSurfaceAppendPlan(result.Plan, "ExportAsync", targetSource, affectedFaces.Length, targetTextureId, DonorTextureId);
    TerrainPatchPlan serializedPlan = JsonSerializer.Deserialize<TerrainPatchPlan>(
            await File.ReadAllTextAsync(outputPlanPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
        ?? throw new InvalidDataException("The missing-descriptor retained plan could not be deserialized.");
    AssertMissingSurfaceAppendPlan(serializedPlan, "serialized plan", targetSource, affectedFaces.Length, targetTextureId, DonorTextureId);

    DiscLayoutInfo disc = DetectDiscLayout(sourceImagePath);
    foreach (TerrainPatch patch in result.Plan.Patches)
    {
        long wadOffset = ParseHexLong(patch.WadRelativeOffset);
        byte[] before = ParsePatchBytes(patch.BeforeHexPreview);
        byte[] after = ParsePatchBytes(patch.AfterHexPreview);
        Assert(before.Length == patch.ByteLength && after.Length == patch.ByteLength,
            $"Missing-descriptor patch {patch.Label} has a length/hex mismatch.");
        Assert(ReadLogicalWadBytes(sourceImagePath, disc, wadOffset, before.Length).SequenceEqual(before),
            $"Missing-descriptor retail before-byte readback failed for {patch.Label}.");
        Assert(ReadLogicalWadBytes(outputImagePath, disc, wadOffset, after.Length).SequenceEqual(after),
            $"Missing-descriptor candidate after-byte readback failed for {patch.Label}.");
    }
    ExactDiffResult exactDiff = CompareExactPhysicalDiff(sourceImagePath, outputImagePath, disc, result.Plan.Patches);
    Assert(exactDiff.UnexpectedDifferenceCount == 0 &&
           exactDiff.ObservedExpectedDifferenceCount == exactDiff.ExpectedDifferenceCount &&
           exactDiff.ExpectedDifferenceCount > 0,
        "The missing-descriptor candidate contains an unexpected or absent physical byte difference.");
    Assert(ReadFileLength(outputImagePath) == expectedSourceLength,
        "The missing-descriptor candidate changed the fixed retail BIN length.");

    NativeTerrainSurfaceSourceData outputSurface = PortalSourceDataLocator.LocateTerrainSurfaces(outputImagePath, targetLevel);
    Assert(outputSurface.SpecialSurfaces.Count == targetSource.SpecialSurfaces.Count + 1,
        "The output did not append exactly one native surface descriptor.");
    int appendedIndex = targetSource.SpecialSurfaces.Count;
    PortalSpecialSurfaceRecord appended = outputSurface.SpecialSurfaces.Single(surface => surface.Index == appendedIndex);
    Assert(appended.Type == water.SurfaceType && appended.Param1 == water.Param1 && appended.Param2 == water.Param2 &&
           appended.RawBytes.SequenceEqual(donorWaterDescriptor.RawBytes),
        "The output appended descriptor does not exactly match the Artisans damaging-water raw record.");
    Assert(outputSurface.CollisionComponentWadOffset == targetSource.CollisionComponentWadOffset + 16,
        "The output collision component did not move by the 16-byte type-0 descriptor import.");
    for (int index = 0; index < targetSource.SpecialSurfaces.Count; index++)
    {
        Assert(outputSurface.SpecialSurfaces[index].RawBytes.SequenceEqual(targetSource.SpecialSurfaces[index].RawBytes) &&
               outputSurface.SpecialSurfaces[index].RelativeOffset == targetSource.SpecialSurfaces[index].RelativeOffset + 4,
            $"Existing Toasty surface descriptor {index} was not preserved with the exact one-pointer shift.");
    }

    NativeTerrainSurfaceLevelCatalog outputSurfaceCatalog =
        NativeTerrainSurfaceCatalogBuilder.Build(targetLevel, targetGeometry, outputSurface);
    foreach (TerrainPolygon face in affectedFaces)
    {
        NativeTerrainFaceSurfaceBinding binding = outputSurfaceCatalog.FindFace(face.RuntimeKey)
            ?? throw new InvalidOperationException($"Affected Toasty face {face.RuntimeKey} disappeared after output reparse.");
        Assert(binding.HasExactTriangleMapping && binding.NativeTriangles.Count > 0 &&
               binding.NativeTriangles.All(triangle =>
                   triangle.SurfaceIndex == appendedIndex &&
                   triangle.SurfaceType == water.SurfaceType &&
                   triangle.Param1 == water.Param1 &&
                   triangle.Param2 == water.Param2),
            $"Affected Toasty face {face.RuntimeKey} does not reparse entirely as appended damaging water.");
    }

    string sourceShaAfter = Sha256File(sourceImagePath);
    long sourceLengthAfter = ReadFileLength(sourceImagePath);
    Assert(sourceShaAfter.Equals(expectedSourceSha256, StringComparison.OrdinalIgnoreCase) &&
           sourceLengthAfter == expectedSourceLength,
        $"The missing-descriptor BuildPlan/ExportAsync path modified the retail source BIN: " +
        $"length {expectedSourceLength}->{sourceLengthAfter}, SHA {expectedSourceSha256}->{sourceShaAfter}.");
    string cueText = await File.ReadAllTextAsync(outputCuePath, Encoding.ASCII);
    Assert(cueText.Contains(Path.GetFileName(outputImagePath), StringComparison.Ordinal),
        "The missing-descriptor CUE does not reference its retained BIN.");
    Assert(!Directory.EnumerateFiles(outputRoot, $"{Path.GetFileName(outputImagePath)}.*.tmp").Any() &&
           !Directory.EnumerateFiles(outputRoot, $"{Path.GetFileName(outputCuePath)}.*.tmp").Any(),
        "ExportAsync left a temporary BIN or CUE beside the missing-descriptor candidate.");

    TerrainPatch surfacePatch = result.Plan.Patches.Single(patch =>
        patch.Kind.Equals("native-surface-layout-import", StringComparison.OrdinalIgnoreCase));
    NativeTerrainTextureRelocationPatchSummary artSummary = result.Plan.NativeTextureRelocations.Single();
    int changedSurfaceBytes = CountChangedBytes(surfacePatch);
    var report = new
    {
        GeneratedAtUtc = DateTimeOffset.UtcNow,
        Passed = true,
        Target = new
        {
            Level = targetLevel.DisplayName,
            targetLevel.SourceWadEntry,
            TextureId = targetTextureId,
            FaceCount = affectedFaces.Length,
            RuntimeKeys = affectedRuntimeKeys
        },
        Donor = new
        {
            Level = donorLevel.DisplayName,
            donorLevel.SourceWadEntry,
            TextureId = DonorTextureId,
            RuntimeKey = donorWaterVariant.RepresentativeRuntimeKey,
            SurfaceIndex = donorWaterDescriptor.Index,
            RawSha256 = Convert.ToHexString(SHA256.HashData(donorWaterDescriptor.RawBytes))
        },
        Art = artSummary,
        Surface = new
        {
            OldSurfaceCount = targetSource.SpecialSurfaces.Count,
            NewSurfaceCount = outputSurface.SpecialSurfaces.Count,
            AppendedSurfaceIndex = appendedIndex,
            DescriptorGrowthBytes = 16,
            OldCollisionWadOffset = targetSource.CollisionComponentWadOffset,
            NewCollisionWadOffset = outputSurface.CollisionComponentWadOffset,
            OldFlagCount = targetSource.Collision.FlagCount,
            NewFlagCount = outputSurface.Collision.FlagCount,
            TargetTriangleCount = readiness.TargetTriangleCount,
            PromotionCount = readiness.PromotionCount,
            SurfacePatchByteSpan = surfacePatch.ByteLength,
            ChangedSurfaceBytes = changedSurfaceBytes
        },
        ExactPhysicalDiff = exactDiff,
        Source = new { Path = sourceImagePath, Length = expectedSourceLength, Sha256 = expectedSourceSha256, Unchanged = true },
        Output = new
        {
            BinPath = outputImagePath,
            CuePath = outputCuePath,
            PlanPath = outputPlanPath,
            Length = ReadFileLength(outputImagePath),
            Sha256 = Sha256File(outputImagePath)
        },
        Inputs = new { TerrainEditsPath = editsPath, SourceSearchPath = sourceSearchPath, RelocationManifestPath = manifestPath }
    };
    await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    await File.WriteAllTextAsync(markdownPath, $"""
        # Missing Native Surface Descriptor Export Smoke

        Status: **PASSED**

        - Pair: Toasty WAD {targetLevel.SourceWadEntry} texture {targetTextureId} <- Artisans WAD {donorLevel.SourceWadEntry} texture {DonorTextureId}.
        - Shared affected faces: {affectedFaces.Length}; collision triangles: {readiness.TargetTriangleCount}; promoted flags: {readiness.PromotionCount}.
        - Native art strategy: `{artSummary.Strategy}`; complete descriptors: {artSummary.CompleteDescriptorCount}; art changed bytes: {artSummary.PatchedByteCount:N0}.
        - Native surface table: {targetSource.SpecialSurfaces.Count}->{outputSurface.SpecialSurfaces.Count}; appended descriptor {appendedIndex}; exact descriptor growth: 16 bytes.
        - Final reparse: all affected faces resolve to appended type 0 / param1 0 / param2 0 damaging water.
        - Exact full-BIN comparison: {exactDiff.ObservedExpectedDifferenceCount:N0} planned changed bytes, zero differences outside the retained plan.
        - Retail source SHA-256 remained `{expectedSourceSha256}`.

        Runtime candidate: `{outputCuePath}`

        Retained patch plan: `{outputPlanPath}`
        """);

    return new MissingSurfaceAppendEvidence(
        targetLevel.DisplayName,
        targetTextureId,
        donorLevel.DisplayName,
        DonorTextureId,
        targetSource.SpecialSurfaces.Count,
        outputSurface.SpecialSurfaces.Count,
        16,
        exactDiff.ExpectedDifferenceCount,
        outputCuePath,
        reportPath);
}

static void AssertMissingSurfaceAppendPlan(
    TerrainPatchPlan plan,
    string label,
    NativeTerrainSurfaceSourceData targetSource,
    int expectedFaceCount,
    int targetTextureId,
    int donorTextureId)
{
    Assert(plan.NativeTextureRelocationCount == 1 && plan.NativeTextureRelocations.Count == 1,
        $"{label} did not retain exactly one first-class native texture relocation.");
    NativeTerrainTextureRelocationPatchSummary summary = plan.NativeTextureRelocations.Single();
    Assert(summary.TargetTextureIds.SequenceEqual([targetTextureId]) &&
           summary.DonorTextures.Count == 1 &&
           summary.DonorTextures[0].Contains($"texture {donorTextureId}", StringComparison.Ordinal) &&
           summary.CompleteDescriptorCount == ExpectedDescriptorCount &&
           summary.PatchCount > 0 && summary.PatchedByteCount > 0,
        $"{label} lost the missing-descriptor art target/donor proof.");
    Assert(summary.RuntimeControlVerified && summary.OwnershipVerified &&
           summary.ExactIndexedPixelsVerified && summary.ExactPalettesVerified && summary.LogicalReadbackVerified,
        $"{label} omitted one or more native art safety/readback proofs.");
    TerrainPatch[] surfaceImports = plan.Patches.Where(patch =>
        patch.Kind.Equals("native-surface-layout-import", StringComparison.OrdinalIgnoreCase)).ToArray();
    Assert(surfaceImports.Length == 1 &&
           surfaceImports[0].Description.Contains("import 1 portable native surface descriptor", StringComparison.OrdinalIgnoreCase) &&
           surfaceImports[0].Description.Contains("move collision data by 16 byte", StringComparison.OrdinalIgnoreCase),
        $"{label} did not compose exactly one real 16-byte descriptor append.");
    Assert(plan.Patches.Count(patch => patch.Kind.StartsWith("native-terrain-texture-", StringComparison.OrdinalIgnoreCase)) ==
           plan.NativeTextureRelocationBytePatchCount,
        $"{label} native art patch counts disagree.");
    Assert(plan.SkippedEdits.Count == 0,
        $"{label} contains skipped/blocked edits: {string.Join(" | ", plan.SkippedEdits)}");
    Assert(expectedFaceCount > 0 && targetSource.SpecialSurfaces.Count == 2,
        "The focused Toasty append preconditions changed.");
}

static int CountChangedBytes(TerrainPatch patch)
{
    byte[] before = ParsePatchBytes(patch.BeforeHexPreview);
    byte[] after = ParsePatchBytes(patch.AfterHexPreview);
    Assert(before.Length == after.Length, $"Patch {patch.Label} has unequal before/after lengths.");
    return before.Where((value, index) => value != after[index]).Count();
}

static void AssertPlan(TerrainPatchPlan plan, string label)
{
    Assert(plan.NativeTextureRelocationCount == 1,
        $"{label} did not load exactly one native relocation edit.");
    NativeTerrainTextureRelocationPatchSummary summary = plan.NativeTextureRelocations.Single();
    Assert(summary.Strategy == "target-owned-in-place",
        $"{label} selected {summary.Strategy} instead of target-owned-in-place.");
    Assert(summary.TargetTextureIds.SequenceEqual([54]) &&
           summary.DonorTextures.Count == 1 &&
           summary.DonorTextures[0].Contains("Gnasty's World", StringComparison.Ordinal) &&
           summary.DonorTextures[0].Contains("texture 22", StringComparison.Ordinal) &&
           summary.DonorTextures[0].Contains("WAD 70", StringComparison.Ordinal),
        $"{label} summary lost the exact target/donor provenance.");
    Assert(summary.CompleteDescriptorCount == ExpectedDescriptorCount,
        $"{label} covered {summary.CompleteDescriptorCount} descriptors instead of {ExpectedDescriptorCount}.");
    Assert(summary.TargetOwnedByteCount == ExpectedTargetOwnedBytes,
        $"{label} reported {summary.TargetOwnedByteCount:N0} target-owned bytes instead of {ExpectedTargetOwnedBytes:N0}.");
    Assert(summary.PatchedByteCount == ExpectedPatchedBytes &&
           plan.TotalPatchedBytes == ExpectedPatchedBytes,
        $"{label} patched {plan.TotalPatchedBytes:N0} bytes instead of {ExpectedPatchedBytes:N0}.");
    Assert(summary.PatchCount == plan.PatchCount &&
           plan.NativeTextureRelocationBytePatchCount == plan.PatchCount &&
           plan.Patches.All(patch =>
               patch.Kind == "native-terrain-texture-in-place" &&
               patch.RuntimeKey == "texture-54"),
        $"{label} contains a non-relocation patch or mismatched patch counts.");
    Assert(summary.RuntimeControlVerified &&
           summary.OwnershipVerified &&
           summary.ExactIndexedPixelsVerified &&
           summary.ExactPalettesVerified &&
           summary.LogicalReadbackVerified,
        $"{label} omitted one or more required native relocation proofs.");
    Assert(plan.SkippedEdits.Count == 0,
        $"{label} contains skipped/blocked edits: {string.Join(" | ", plan.SkippedEdits)}");
}

static string ResolveWorkspaceRoot(string[] arguments)
{
    string candidate = arguments.FirstOrDefault(argument =>
        !argument.StartsWith("--", StringComparison.Ordinal))
        ?? Directory.GetCurrentDirectory();
    string current = Path.GetFullPath(candidate);
    while (!File.Exists(Path.Combine(current, "spyro-level-catalog.json")))
    {
        string? parent = Directory.GetParent(current)?.FullName;
        if (string.IsNullOrWhiteSpace(parent) || parent == current)
            throw new DirectoryNotFoundException($"Could not resolve the Spyro editor workspace from {candidate}.");
        current = parent;
    }
    return current;
}

static string Sha256File(string path)
{
    using FileStream stream = File.OpenRead(path);
    return Convert.ToHexString(SHA256.HashData(stream));
}

static long ReadFileLength(string path)
{
    using FileStream stream = File.OpenRead(path);
    return stream.Length;
}

static DiscLayoutInfo DetectDiscLayout(string path)
{
    using FileStream stream = File.OpenRead(path);
    byte[] header = new byte[7];
    foreach ((int sectorSize, int userOffset) in new[] { (2048, 0), (2352, 24), (2336, 8) })
    {
        long pvdOffset = (16L * sectorSize) + userOffset;
        if (stream.Length < pvdOffset + 7)
            continue;
        stream.Position = pvdOffset;
        stream.ReadExactly(header);
        if (header[0] == 1 && Encoding.ASCII.GetString(header, 1, 5) == "CD001")
            return new DiscLayoutInfo(sectorSize, userOffset);
    }
    throw new InvalidDataException("Could not detect the source disc layout.");
}

static byte[] ReadLogicalWadBytes(string imagePath, DiscLayoutInfo layout, long wadOffset, int length)
{
    byte[] result = new byte[length];
    using FileStream stream = File.OpenRead(imagePath);
    int remaining = length;
    int written = 0;
    long logical = wadOffset;
    while (remaining > 0)
    {
        int userSectorOffset = checked((int)(logical % 2048));
        long sector = WadLba + (logical / 2048);
        int count = Math.Min(2048 - userSectorOffset, remaining);
        stream.Position = (sector * layout.SectorSize) + layout.UserOffset + userSectorOffset;
        stream.ReadExactly(result.AsSpan(written, count));
        logical += count;
        written += count;
        remaining -= count;
    }
    return result;
}

static ExactDiffResult CompareExactPhysicalDiff(
    string sourcePath,
    string outputPath,
    DiscLayoutInfo layout,
    IReadOnlyList<TerrainPatch> patches)
{
    Dictionary<long, byte> expectedAfterByPhysicalOffset = [];
    foreach (TerrainPatch patch in patches)
    {
        long wadOffset = ParseHexLong(patch.WadRelativeOffset);
        byte[] before = ParsePatchBytes(patch.BeforeHexPreview);
        byte[] after = ParsePatchBytes(patch.AfterHexPreview);
        Assert(before.Length == after.Length,
            $"Patch {patch.Label} has unequal before/after lengths during exact BIN comparison.");
        for (int index = 0; index < after.Length; index++)
        {
            if (before[index] == after[index])
                continue;
            long logical = wadOffset + index;
            long sector = WadLba + (logical / 2048);
            long physical = (sector * layout.SectorSize) + layout.UserOffset + (logical % 2048);
            if (!expectedAfterByPhysicalOffset.TryAdd(physical, after[index]))
                throw new InvalidDataException($"The patch plan overlaps physical byte 0x{physical:X}.");
        }
    }

    using FileStream source = File.OpenRead(sourcePath);
    using FileStream output = File.OpenRead(outputPath);
    Assert(source.Length == output.Length, "Exact BIN comparison requires equal file lengths.");
    byte[] sourceBuffer = new byte[1024 * 1024];
    byte[] outputBuffer = new byte[sourceBuffer.Length];
    long physicalBase = 0;
    int observedExpected = 0;
    int unexpected = 0;
    while (physicalBase < source.Length)
    {
        int requested = checked((int)Math.Min(sourceBuffer.Length, source.Length - physicalBase));
        source.ReadExactly(sourceBuffer.AsSpan(0, requested));
        output.ReadExactly(outputBuffer.AsSpan(0, requested));
        for (int index = 0; index < requested; index++)
        {
            long physical = physicalBase + index;
            bool differs = sourceBuffer[index] != outputBuffer[index];
            if (expectedAfterByPhysicalOffset.TryGetValue(physical, out byte expectedAfter))
            {
                if (!differs || outputBuffer[index] != expectedAfter)
                    throw new InvalidDataException($"Expected planned difference failed at physical image byte 0x{physical:X}.");
                observedExpected++;
            }
            else if (differs)
            {
                unexpected++;
            }
        }
        physicalBase += requested;
    }
    return new ExactDiffResult(expectedAfterByPhysicalOffset.Count, observedExpected, unexpected);
}

static long ParseHexLong(string text)
{
    string value = (text ?? "").Trim();
    if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        value = value[2..];
    return Convert.ToInt64(value, 16);
}

static byte[] ParsePatchBytes(string text)
{
    string compact = new((text ?? "").Where(Uri.IsHexDigit).ToArray());
    if ((compact.Length & 1) != 0)
        throw new InvalidDataException("Patch hex text contains an odd number of digits.");
    return Convert.FromHexString(compact);
}

static async Task RunNativeTexturePreviewDecoderSmokeAsync(
    string workspaceRoot,
    string sourceImagePath,
    LevelCatalog catalog)
{
    string outputRoot = Path.Combine(workspaceRoot, "_local", "smoke", "native-terrain-texture-preview-decoder");
    if (Directory.Exists(outputRoot))
        Directory.Delete(outputRoot, recursive: true);
    Directory.CreateDirectory(outputRoot);

    LevelDefinition artisans = catalog.FindByKey("artisans")
        ?? throw new InvalidOperationException("Artisans is missing from the level catalog.");
    LevelDefinition stoneHill = catalog.FindByKey("stonehill")
        ?? throw new InvalidOperationException("Stone Hill is missing from the level catalog.");

    AssertPsxTextureWordTransparencySemantics();
    Assert(
        NativeTerrainTexturePreviewLod.SelectForMinimumCameraDepth(79.999) == NativeTerrainTexturePreviewTier.Close,
        "The native terrain preview did not select close HQ below the 80-unit cutoff.");
    Assert(
        NativeTerrainTexturePreviewLod.SelectForMinimumCameraDepth(80) == NativeTerrainTexturePreviewTier.Normal,
        "The native terrain preview did not select normal HQ at the retail 80-unit cutoff.");
    Assert(
        NativeTerrainTexturePreviewLod.SelectForMinimumCameraDepth(double.NaN) == NativeTerrainTexturePreviewTier.Normal &&
        NativeTerrainTexturePreviewLod.SelectForMinimumCameraDepth(-1) == NativeTerrainTexturePreviewTier.Normal,
        "Invalid terrain camera depths must conservatively select normal HQ.");

    string stoneHillNormalPath = Path.Combine(outputRoot, "stonehill-texture-040-normal.png");
    TerrainTextureImageExport stoneHillNormal =
        await TerrainPatchExporter.TryExportTerrainTextureImageTierAsync(
            sourceImagePath,
            stoneHill,
            40,
            stoneHillNormalPath,
            "hqData")
        ?? throw new InvalidOperationException("Stone Hill texture 40 normal HQ preview did not decode.");
    AssertTextureImage(
        stoneHillNormal,
        stoneHillNormalPath,
        "hqData",
        64,
        4,
        "BE4F144B08DB0A911AB2500BE4211614F1F0A9C77C595A3ABE627EF6F80444E5");

    NativeTerrainTextureRuntimeControlAudit stoneHillRuntime =
        NativeTerrainTextureRuntimeControlScanner.Inspect(sourceImagePath, stoneHill);
    string stoneHillInitializedRoot = Path.Combine(outputRoot, "stonehill-native-load-initialized");
    NativeTerrainTextureInitialStateImageExport stoneHillInitialized =
        await TerrainPatchExporter.ExportTerrainTextureInitialStateImageTiersAsync(
            sourceImagePath,
            stoneHill,
            stoneHillRuntime,
            [40],
            stoneHillInitializedRoot,
            overwrite: true);
    Assert(stoneHillInitialized.LowDetailTextures is { TextureCount: 1, RetailAliasComplete: true },
        "Stone Hill native load-state export did not retain texture 40's complete TexLq payload.");
    NativeTerrainLqTextureSet stoneHillLq = stoneHillInitialized.LowDetailTextures!;
    NativeTerrainLqTextureRecordPayload stoneHillTexture40 = stoneHillLq.Textures.Single(texture => texture.TextureId == 40);
    NativeTerrainLqTextureDescriptorPayload stoneHillLq0 = stoneHillTexture40.GetDescriptor(0);
    NativeTerrainLqTextureDescriptorPayload stoneHillLq1 = stoneHillTexture40.GetDescriptor(1);
    Assert(Convert.ToHexString(stoneHillLq0.RawDescriptor.Span) == "E0A02A78FFA01880" &&
           stoneHillLq0.RawDescriptorSha256 == "F090EC3B596F4927740F66F514F2961235D1DA73946A1F27D006B9B1B0114335" &&
           stoneHillLq0.PackedIndicesSha256 == "571D04D7B1B7964D033B88D598033F1C485BE7D55CBC759BADB0AED5DCED712A" &&
           stoneHillLq0.PaletteWordsSha256 == "2294EDE7FACE95514ACB6163F08B7852BBBC633D235D5C892AEF3A74A0E15DB7" &&
           stoneHillLq0.RawDescriptor.Span.SequenceEqual(stoneHillLq1.RawDescriptor.Span) &&
           stoneHillLq0.PackedIndices.Span.SequenceEqual(stoneHillLq1.PackedIndices.Span) &&
           stoneHillLq0.PaletteWords.Span.SequenceEqual(stoneHillLq1.PaletteWords.Span),
        "Stone Hill texture 40 TexLq descriptor/index/palette fixture changed.");
    Assert(Sha256Rgba(stoneHillLq0.MaterializeRgba(0)) == "0AF1239F450345F9A72B6EE4A3411D4F88EBE2A02AE61F9A523EE9C4F4279470" &&
           Sha256Rgba(stoneHillLq0.MaterializeRgba(8)) == "DD782682719238B3CC3C9B6FD1D2286AECA51443FD4F99131D6EDFE919E0F91C" &&
           Sha256Rgba(stoneHillLq0.MaterializeRgba(15)) == "B01E3293BCA9BBD0EEA7D4E70824F590750CD6F7B3B201730E3E6AA6954F27DF",
        "Stone Hill texture 40 TexLq palette-row RGBA fixtures changed.");

    string stoneHillLqSidecar = Path.Combine(stoneHillInitializedRoot, NativeTerrainLqTextureCacheCodec.PayloadFileName);
    NativeTerrainLqTextureCacheWriteResult stoneHillLqWrite =
        await NativeTerrainLqTextureCacheCodec.WriteAsync(stoneHillLqSidecar, stoneHillLq);
    NativeTerrainLqTextureCacheProof stoneHillLqProof = new(
        NativeTerrainLqTextureCacheCodec.PayloadFormatVersion,
        NativeTerrainLqTextureCacheCodec.RecordSize,
        stoneHillLqWrite.ByteLength,
        stoneHillLqWrite.Sha256,
        stoneHillLq.NativeTextureCount,
        stoneHillLq.TextureCount,
        stoneHillLq.TexturePagesByteLength,
        stoneHillLq.TexturePagesSha256,
        stoneHillLq.OriginalTextureComponentSha256,
        stoneHillLq.InitializedLqTableSha256,
        stoneHillLq.RetailAliasCount);
    NativeTerrainLqTextureCacheReadResult stoneHillLqRead =
        NativeTerrainLqTextureCacheCodec.ReadValidated(stoneHillLqSidecar, stoneHillLqProof);
    Assert(stoneHillLqRead.Complete &&
           stoneHillLqRead.TextureSet?.Textures.Single().GetDescriptor(0).PackedIndicesSha256 == stoneHillLq0.PackedIndicesSha256,
        $"Stone Hill TexLq sidecar round trip failed: {string.Join("; ", stoneHillLqRead.SafetyBlockers)}");

    byte[] validLqSidecarBytes = File.ReadAllBytes(stoneHillLqSidecar);
    string corruptLqSidecar = Path.Combine(stoneHillInitializedRoot, "lq-indexed-corrupt.bin");
    byte[] corruptBytes = validLqSidecarBytes.ToArray();
    corruptBytes[^1] ^= 0x01;
    await File.WriteAllBytesAsync(corruptLqSidecar, corruptBytes);
    Assert(!NativeTerrainLqTextureCacheCodec.ReadValidated(corruptLqSidecar, stoneHillLqProof).Complete,
        "A TexLq sidecar with a changed payload byte did not fail closed.");
    string truncatedLqSidecar = Path.Combine(stoneHillInitializedRoot, "lq-indexed-truncated.bin");
    await File.WriteAllBytesAsync(truncatedLqSidecar, validLqSidecarBytes[..^1]);
    Assert(!NativeTerrainLqTextureCacheCodec.ReadValidated(truncatedLqSidecar, stoneHillLqProof).Complete,
        "A truncated TexLq sidecar did not fail closed.");

    string stoneHillWaterPath = Path.Combine(outputRoot, "stonehill-texture-032-water.png");
    TerrainTextureImageExport stoneHillWater =
        await TerrainPatchExporter.TryExportTerrainTextureImageTierAsync(
            sourceImagePath,
            stoneHill,
            32,
            stoneHillWaterPath,
            "hqData")
        ?? throw new InvalidOperationException("Stone Hill texture 32 water preview did not decode.");
    AssertTextureImage(
        stoneHillWater,
        stoneHillWaterPath,
        "hqData",
        64,
        4,
        "34831AC71F526EA6CE4D90EF383EABAFD0122F55AE1E11BD2F8C19DE856B9F0A");

    string artisansClose32Path = Path.Combine(outputRoot, "artisans-texture-000-close32.png");
    TerrainTextureImageExport artisansClose32 =
        await TerrainPatchExporter.TryExportTerrainTextureImageTierAsync(
            sourceImagePath,
            artisans,
            0,
            artisansClose32Path,
            "hqDataClose")
        ?? throw new InvalidOperationException("Artisans texture 0 close HQ preview did not decode.");
    AssertTextureImage(
        artisansClose32,
        artisansClose32Path,
        "hqDataClose",
        128,
        16,
        "2FB1FC641101B21527F460C3A9AA184494A50A73A2C9CABBE9900F4DDF5BFFEC");

    string artisansAnimatedPath = Path.Combine(outputRoot, "artisans-texture-023-runtime-controlled.png");
    TerrainTextureImageExport artisansAnimated =
        await TerrainPatchExporter.TryExportTerrainTextureImageTierAsync(
            sourceImagePath,
            artisans,
            23,
            artisansAnimatedPath,
            "hqData")
        ?? throw new InvalidOperationException("Artisans texture 23 runtime-controlled preview did not decode.");
    AssertTextureImage(
        artisansAnimated,
        artisansAnimatedPath,
        "hqData",
        64,
        4,
        "2B2DA9828F85BEBF537E7E0B320DDE80C171D5B8AB54B502C10E37834E2C3D20");

    string sourceShaBeforeInitializedExport = Sha256File(sourceImagePath);
    NativeTerrainTextureRuntimeControlAudit artisansRuntime =
        NativeTerrainTextureRuntimeControlScanner.Inspect(sourceImagePath, artisans);
    string artisansInitializedRoot = Path.Combine(outputRoot, "artisans-native-load-initialized");
    NativeTerrainTextureInitialStateImageExport artisansInitialized =
        await TerrainPatchExporter.ExportTerrainTextureInitialStateImageTiersAsync(
            sourceImagePath,
            artisans,
            artisansRuntime,
            [23],
            artisansInitializedRoot,
            overwrite: true);
    Assert(artisansInitialized.InitialState.Complete && artisansInitialized.Exports.Count == 2,
        "Artisans native load-state export did not produce both exact HQ tiers for scrolling texture 23.");
    Assert(artisansInitialized.LowDetailTextures is { TextureCount: 1, RetailAliasComplete: true },
        "Artisans native load-state export did not retain scrolling texture 23's TexLq payload.");
    NativeTerrainLqTextureDescriptorPayload artisansPhaseZeroLq =
        artisansInitialized.LowDetailTextures!.Textures.Single().GetDescriptor(0);
    Assert(Convert.ToHexString(artisansPhaseZeroLq.RawDescriptor.Span) == "20003F143F000880" &&
           artisansPhaseZeroLq.RawDescriptorSha256 == "2A503A9A8D4618FBF6A952955B32DBF0822D8239166D5E6F14EDB5584808A569" &&
           artisansPhaseZeroLq.PackedIndicesSha256 == "9266EC7B8AAB2DE5B6D5D70E22DA9CC7FACE471D7EF1C9AFCC919442BC8D6EC8" &&
           artisansPhaseZeroLq.PaletteWordsSha256 == "3D4CC89AA09A708B2801A11DFC7D2120CBF7DACD0F018AB1C3D08F0DF3A65906" &&
           Sha256Rgba(artisansPhaseZeroLq.MaterializeRgba(0)) == "9AEAC748A2C4B17613D92C22382177C8092910514F956D323EE68E1E236E263A",
        "Artisans texture 23 phase-zero TexLq fixture changed.");
    TerrainTextureImageExport artisansInitializedNormal = artisansInitialized.Exports.Single(export => export.DescriptorTier == "hqData");
    TerrainTextureImageExport artisansInitializedClose = artisansInitialized.Exports.Single(export => export.DescriptorTier == "hqDataClose");
    string artisansInitializedNormalPath = Path.Combine(artisansInitializedRoot, "artisans-texture-023-normal.png");
    string artisansInitializedClosePath = Path.Combine(artisansInitializedRoot, "artisans-texture-023-close.png");
    AssertTextureImage(
        artisansInitializedNormal,
        artisansInitializedNormalPath,
        "hqData",
        64,
        4,
        "2B2DA9828F85BEBF537E7E0B320DDE80C171D5B8AB54B502C10E37834E2C3D20");
    AssertTextureImage(
        artisansInitializedClose,
        artisansInitializedClosePath,
        "hqDataClose",
        64,
        16,
        "2B2DA9828F85BEBF537E7E0B320DDE80C171D5B8AB54B502C10E37834E2C3D20");

    string artisansRawClosePath = Path.Combine(outputRoot, "artisans-texture-023-runtime-controlled-close-raw.png");
    TerrainTextureImageExport artisansRawClose =
        await TerrainPatchExporter.TryExportTerrainTextureImageTierAsync(
            sourceImagePath,
            artisans,
            23,
            artisansRawClosePath,
            "hqDataClose")
        ?? throw new InvalidOperationException("Artisans texture 23 raw close-HQ preview did not decode.");
    AssertTextureImage(
        artisansRawClose,
        artisansRawClosePath,
        "hqDataClose",
        64,
        16,
        "2B2DA9828F85BEBF537E7E0B320DDE80C171D5B8AB54B502C10E37834E2C3D20");
    NativeTerrainTextureInitialStateMutation artisansScrollMutation = artisansInitialized.InitialState.Mutations.Single();
    Assert(artisansScrollMutation.LqChangedByteCount == 4 && artisansScrollMutation.HqChangedByteCount == 22 &&
           artisansScrollMutation.OriginalLqSha256 == "431E531008EA3F923C929EA3C0719D33E2E928B41731880680B2A96D83AE4468" &&
           artisansScrollMutation.InitializedLqSha256 == "0E198600A0268727A260DB06F21239822918B780726584961D5457B55860C78C" &&
           artisansScrollMutation.OriginalHqSha256 == "BC4515795A9582BFC3220B370099B7F0B564F3AFAB55AD49B490EC240875D516" &&
           artisansScrollMutation.InitializedHqSha256 == "B230F0ACC2AE7A2688682E2E5C54BA9D0EE319780FA9F638CBECDFB0F96BB3D1",
        "Artisans scrolling texture 23 did not apply its exact native load-state descriptor rewrite.");

    // Retail phase zero lands on pixel-identical duplicated VRAM rows for every
    // scrolling target. A synthetic phase-two control fixture proves the same
    // initialized-record decode path changes rendered pixels when coordinates
    // select a genuinely different row; this fixture is never written to cache.
    NativeTerrainTextureRuntimeControlAudit artisansPhaseTwoAudit = artisansRuntime with
    {
        ScrollingControls = artisansRuntime.ScrollingControls
            .Select(control => control with { InitialPhase = 2 })
            .ToArray()
    };
    string artisansPhaseTwoRoot = Path.Combine(outputRoot, "artisans-synthetic-phase-two");
    NativeTerrainTextureInitialStateImageExport artisansPhaseTwo =
        await TerrainPatchExporter.ExportTerrainTextureInitialStateImageTiersAsync(
            sourceImagePath,
            artisans,
            artisansPhaseTwoAudit,
            [23],
            artisansPhaseTwoRoot,
            overwrite: true);
    NativeTerrainTextureInitialStateMutation phaseTwoMutation = artisansPhaseTwo.InitialState.Mutations.Single();
    string phaseTwoNormalPath = Path.Combine(artisansPhaseTwoRoot, "artisans-texture-023-normal.png");
    string phaseTwoClosePath = Path.Combine(artisansPhaseTwoRoot, "artisans-texture-023-close.png");
    Assert(phaseTwoMutation.InitialPhase == 2 &&
           phaseTwoMutation.InitializedHqSha256 == "3308E4561E29D268032CE37A787C2E9B3E2F33ED26478451A1517CF2EBF1E21C" &&
           phaseTwoMutation.InitializedHqSha256 != artisansScrollMutation.InitializedHqSha256 &&
           Sha256File(phaseTwoNormalPath) == "E343C096B5ACAE0C5F8C6F32EA8865CA18B9ECB4489F634F35A38D680D155890" &&
           Sha256File(phaseTwoClosePath) == "E343C096B5ACAE0C5F8C6F32EA8865CA18B9ECB4489F634F35A38D680D155890" &&
           Sha256File(phaseTwoNormalPath) != Sha256File(artisansInitializedNormalPath) &&
           Sha256File(phaseTwoClosePath) != Sha256File(artisansInitializedClosePath),
        "Synthetic Artisans phase-two coordinates did not flow through DecodeTextureRecords into changed rendered frames.");
    Console.WriteLine($"Artisans synthetic phase-two initialized HQ record SHA: {phaseTwoMutation.InitializedHqSha256}");

    // TexLq scrolling advances in quarter-phase units, so phase four is the
    // first synthetic fixture which must select a different logical LQ row.
    NativeTerrainTextureRuntimeControlAudit artisansPhaseFourAudit = artisansRuntime with
    {
        ScrollingControls = artisansRuntime.ScrollingControls
            .Select(control => control with { InitialPhase = 4 })
            .ToArray()
    };
    string artisansPhaseFourRoot = Path.Combine(outputRoot, "artisans-synthetic-phase-four-lq");
    NativeTerrainTextureInitialStateImageExport artisansPhaseFour =
        await TerrainPatchExporter.ExportTerrainTextureInitialStateImageTiersAsync(
            sourceImagePath,
            artisans,
            artisansPhaseFourAudit,
            [23],
            artisansPhaseFourRoot,
            overwrite: true);
    NativeTerrainTextureInitialStateMutation phaseFourMutation = artisansPhaseFour.InitialState.Mutations.Single();
    NativeTerrainLqTextureDescriptorPayload artisansPhaseFourLq =
        artisansPhaseFour.LowDetailTextures?.Textures.Single().GetDescriptor(0)
        ?? throw new InvalidOperationException("Synthetic Artisans phase-four export omitted TexLq.");
    Assert(phaseFourMutation.InitialPhase == 4 &&
           phaseFourMutation.InitializedLqSha256 == "2C0F88E43B6FBBDEC1EBF3F0DC3A4F3D07DC61BF9A80DF8B3129838A9B0685BF" &&
           Convert.ToHexString(artisansPhaseFourLq.RawDescriptor.Span) == "20013F143F010880" &&
           artisansPhaseFourLq.RawDescriptorSha256 == "774D60B8AFCF819D43E3AA7DBDA9615C27A57911C4D9FE99BFAB2AB72CC60802" &&
           artisansPhaseFourLq.PackedIndicesSha256 == "DB1C2C5C0C6D5DF0461F0A4012CD40DD8D98AE2BBBDCB46A60DFEE74517CAB22" &&
           artisansPhaseFourLq.PaletteWordsSha256 == artisansPhaseZeroLq.PaletteWordsSha256 &&
           Sha256Rgba(artisansPhaseFourLq.MaterializeRgba(0)) == "0E75977BAAE53343407C74392A02DB71619093F60A6FB43034858FDDE910DAC2" &&
           artisansPhaseFourLq.PackedIndicesSha256 != artisansPhaseZeroLq.PackedIndicesSha256,
        "Synthetic Artisans phase-four coordinates did not flow into changed TexLq logical indexes/pixels.");

    LevelDefinition magicCrafters = catalog.FindByKey("magiccrafters")
        ?? throw new InvalidOperationException("Magic Crafters is missing from the level catalog.");
    NativeTerrainTextureRuntimeControlAudit magicRuntime =
        NativeTerrainTextureRuntimeControlScanner.Inspect(sourceImagePath, magicCrafters);
    string magicInitializedRoot = Path.Combine(outputRoot, "magiccrafters-native-load-initialized");
    NativeTerrainTextureInitialStateImageExport magicInitialized =
        await TerrainPatchExporter.ExportTerrainTextureInitialStateImageTiersAsync(
            sourceImagePath,
            magicCrafters,
            magicRuntime,
            [61, 64, 65, 66],
            magicInitializedRoot,
            overwrite: true);
    Assert(magicInitialized.InitialState.Complete && magicInitialized.Exports.Count == 8,
        "Magic Crafters native load-state export did not include both destination and otherwise-unreferenced source tiers.");
    foreach (string suffix in new[] { "normal", "close" })
    {
        string source64Hash = Sha256File(Path.Combine(magicInitializedRoot, $"magiccrafters-texture-064-{suffix}.png"));
        string source66Hash = Sha256File(Path.Combine(magicInitializedRoot, $"magiccrafters-texture-066-{suffix}.png"));
        Assert(source64Hash == "BCCB06E574D2AD15714379A0D436F02A626C3078D498708C3A1F11D63150529F" &&
               source66Hash == "37009397A577C43EF164AF0F383BE87036A2C8843F8209FB7B43E06679A7D2C3",
            $"Magic Crafters initialized source fixture hashes changed ({suffix}).");
        Assert(Sha256File(Path.Combine(magicInitializedRoot, $"magiccrafters-texture-061-{suffix}.png")) == source64Hash,
            $"Magic Crafters initialized destination 61 does not match selected source 64 ({suffix}).");
        Assert(Sha256File(Path.Combine(magicInitializedRoot, $"magiccrafters-texture-065-{suffix}.png")) == source66Hash,
            $"Magic Crafters initialized destination 65 does not match selected source 66 ({suffix}).");
    }
    Assert(string.Equals(sourceShaBeforeInitializedExport, Sha256File(sourceImagePath), StringComparison.OrdinalIgnoreCase),
        "Native load-state preview export changed the retail source BIN.");

    string batchRoot = Path.Combine(outputRoot, "batch-close");
    IReadOnlyList<TerrainTextureImageExport> batch = await TerrainPatchExporter.ExportTerrainTextureImagesAsync(
        sourceImagePath,
        artisans,
        [0, 27],
        batchRoot,
        overwrite: true,
        preferredDescriptorTier: "hqDataClose");
    Assert(batch.Count == 2, $"Close-HQ batch decoded {batch.Count}/2 requested Artisans textures.");
    TerrainTextureImageExport batchClose32 = batch.Single(item => item.TextureId == 0);
    TerrainTextureImageExport batchClose16 = batch.Single(item => item.TextureId == 27);
    AssertTextureImage(
        batchClose32,
        Path.Combine(batchRoot, "artisans-texture-000.png"),
        "hqDataClose",
        128,
        16,
        "2FB1FC641101B21527F460C3A9AA184494A50A73A2C9CABBE9900F4DDF5BFFEC");
    AssertTextureImage(
        batchClose16,
        Path.Combine(batchRoot, "artisans-texture-027.png"),
        "hqDataClose",
        64,
        16,
        "16BCF9ECC4FF72589D4F14876316D3953E74BCD779BB5F475E2C0B28B2389963");

    string dualTierRoot = Path.Combine(outputRoot, "batch-dual-tier");
    IReadOnlyList<TerrainTextureImageExport> dualTier = await TerrainPatchExporter.ExportTerrainTextureImageTiersAsync(
        sourceImagePath,
        artisans,
        [0, 27],
        dualTierRoot,
        overwrite: true);
    Assert(dualTier.Count == 4, $"Dual-tier batch decoded {dualTier.Count}/4 requested Artisans texture frames.");
    Assert(dualTier.Select(export => export.TextureId).Distinct().Count() == 2,
        "Dual-tier batch did not preserve both requested texture IDs.");
    Assert(dualTier.Count(export => export.DescriptorTier == "hqData") == 2 &&
        dualTier.Count(export => export.DescriptorTier == "hqDataClose") == 2,
        "Dual-tier batch did not produce one exact normal and close frame per texture.");

    TerrainTextureImageExport dualNormal32 = dualTier.Single(export => export.TextureId == 0 && export.DescriptorTier == "hqData");
    TerrainTextureImageExport dualClose32 = dualTier.Single(export => export.TextureId == 0 && export.DescriptorTier == "hqDataClose");
    string dualNormal32Path = Path.Combine(dualTierRoot, "artisans-texture-000-normal.png");
    string dualClose32Path = Path.Combine(dualTierRoot, "artisans-texture-000-close.png");
    AssertTextureImage(
        dualNormal32,
        dualNormal32Path,
        "hqData",
        64,
        4,
        "C1CEDD16CB74757A949FD519172C6E2071525D10AB01618DC4B4128E6EB37AA4");
    AssertTextureImage(
        dualClose32,
        dualClose32Path,
        "hqDataClose",
        128,
        16,
        "2FB1FC641101B21527F460C3A9AA184494A50A73A2C9CABBE9900F4DDF5BFFEC");
    Assert(!string.Equals(Sha256File(dualNormal32Path), Sha256File(dualClose32Path), StringComparison.Ordinal),
        "Artisans texture 0 normal and close frames unexpectedly decoded to the same PNG.");

    IReadOnlyList<ScrollingRenderDifference> scrollingRenderDifferences =
        await AuditScrollingRenderDifferencesAsync(outputRoot, sourceImagePath, catalog);
    Assert(scrollingRenderDifferences.Count == 0,
        "A retail phase-zero scrolling rewrite unexpectedly changed decoded pixels; refresh the load-state fixtures and semantics before shipping.");
    Console.WriteLine($"Scrolling load-state render differential: {scrollingRenderDifferences.Count} changed tier(s) across 26 controls.");
    foreach (ScrollingRenderDifference difference in scrollingRenderDifferences)
    {
        Console.WriteLine(
            $"  {difference.LevelKey} texture {difference.TextureId} {difference.DescriptorTier}: " +
            $"{difference.RawSha256[..12]} -> {difference.InitializedSha256[..12]}");
    }

    Console.WriteLine("Native terrain texture preview decoder smoke passed.");
    foreach (string path in Directory.EnumerateFiles(outputRoot, "*.png", SearchOption.AllDirectories).Order())
        Console.WriteLine($"{Path.GetRelativePath(outputRoot, path)}  {Sha256File(path)}");
}

static async Task<IReadOnlyList<ScrollingRenderDifference>> AuditScrollingRenderDifferencesAsync(
    string outputRoot,
    string sourceImagePath,
    LevelCatalog catalog)
{
    string auditRoot = Path.Combine(outputRoot, "scrolling-render-differential");
    List<ScrollingRenderDifference> differences = [];
    int controlCount = 0;
    foreach (LevelDefinition level in catalog.Levels.Where(level => level.SourceWadEntry >= 0))
    {
        NativeTerrainTextureRuntimeControlAudit audit =
            NativeTerrainTextureRuntimeControlScanner.Inspect(sourceImagePath, level);
        if (audit.ScrollingControls.Count == 0)
            continue;

        int[] textureIds = audit.ScrollingTextureIds.ToArray();
        controlCount += textureIds.Length;
        string rawDirectory = Path.Combine(auditRoot, level.Key, "raw");
        string initializedDirectory = Path.Combine(auditRoot, level.Key, "initialized");
        IReadOnlyList<TerrainTextureImageExport> raw =
            await TerrainPatchExporter.ExportTerrainTextureImageTiersAsync(
                sourceImagePath,
                level,
                textureIds,
                rawDirectory,
                overwrite: true);
        NativeTerrainTextureInitialStateImageExport initialized =
            await TerrainPatchExporter.ExportTerrainTextureInitialStateImageTiersAsync(
                sourceImagePath,
                level,
                audit,
                textureIds,
                initializedDirectory,
                overwrite: true);
        Assert(initialized.InitialState.Complete && raw.Count == textureIds.Length * 2 && initialized.Exports.Count == textureIds.Length * 2,
            $"{level.DisplayName}: scrolling differential did not decode both tiers for every controlled destination.");

        foreach (int textureId in textureIds)
        {
            foreach (string descriptorTier in new[] { "hqData", "hqDataClose" })
            {
                string suffix = descriptorTier == "hqDataClose" ? "close" : "normal";
                string rawPath = Path.Combine(rawDirectory, $"{level.Key}-texture-{textureId:000}-{suffix}.png");
                string initializedPath = Path.Combine(initializedDirectory, $"{level.Key}-texture-{textureId:000}-{suffix}.png");
                string rawSha256 = Sha256File(rawPath);
                string initializedSha256 = Sha256File(initializedPath);
                if (!string.Equals(rawSha256, initializedSha256, StringComparison.Ordinal))
                {
                    differences.Add(new ScrollingRenderDifference(
                        level.Key,
                        textureId,
                        descriptorTier,
                        rawSha256,
                        initializedSha256));
                }
            }
        }
    }

    Assert(controlCount == 26, $"Scrolling render differential expected 26 controls, got {controlCount}.");
    return differences;
}

static void AssertTextureImage(
    TerrainTextureImageExport export,
    string path,
    string expectedTier,
    int expectedSize,
    int expectedDescriptorCount,
    string expectedSha256,
    int expectedTransparentPixelCount = 0)
{
    Assert(File.Exists(path), $"Missing decoded terrain PNG: {path}");
    Assert(string.Equals(export.DescriptorTier, expectedTier, StringComparison.OrdinalIgnoreCase),
        $"{Path.GetFileName(path)} decoded tier {export.DescriptorTier}, expected {expectedTier}.");
    Assert(export.Width == expectedSize && export.Height == expectedSize,
        $"{Path.GetFileName(path)} decoded {export.Width}x{export.Height}, expected {expectedSize}x{expectedSize}.");
    Assert(export.DescriptorCount == expectedDescriptorCount,
        $"{Path.GetFileName(path)} decoded {export.DescriptorCount} descriptors, expected {expectedDescriptorCount}.");
    Assert(export.PixelCount == expectedSize * expectedSize,
        $"{Path.GetFileName(path)} decoded {export.PixelCount} pixels, expected {expectedSize * expectedSize}.");

    byte[] header = File.ReadAllBytes(path);
    Assert(header.Length >= 24 && header.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
        $"{Path.GetFileName(path)} is not a PNG.");
    int pngWidth = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(16, 4));
    int pngHeight = BinaryPrimitives.ReadInt32BigEndian(header.AsSpan(20, 4));
    Assert(pngWidth == expectedSize && pngHeight == expectedSize,
        $"{Path.GetFileName(path)} IHDR is {pngWidth}x{pngHeight}, expected {expectedSize}x{expectedSize}.");

    Rgba32[] pixels = PngRgbaImage.ReadRgba(path, out int decodedWidth, out int decodedHeight);
    Assert(decodedWidth == expectedSize && decodedHeight == expectedSize,
        $"{Path.GetFileName(path)} decoded to {decodedWidth}x{decodedHeight}, expected {expectedSize}x{expectedSize}.");
    int transparentPixelCount = pixels.Count(pixel => pixel.A == 0);
    int partialAlphaPixelCount = pixels.Count(pixel => pixel.A is > 0 and < 255);
    Assert(partialAlphaPixelCount == 0,
        $"{Path.GetFileName(path)} invented {partialAlphaPixelCount} partial-alpha pixels without primitive translucency state.");
    if (expectedTransparentPixelCount >= 0)
    {
        Assert(transparentPixelCount == expectedTransparentPixelCount,
            $"{Path.GetFileName(path)} has {transparentPixelCount} transparent pixels, expected {expectedTransparentPixelCount}.");
    }

    string actualSha256 = Sha256File(path);
    if (!string.IsNullOrWhiteSpace(expectedSha256))
    {
        Assert(string.Equals(actualSha256, expectedSha256, StringComparison.OrdinalIgnoreCase),
            $"{Path.GetFileName(path)} SHA-256 {actualSha256} does not match the source-locked decoded image {expectedSha256}.");
    }
    Console.WriteLine($"{Path.GetFileName(path)} alpha: transparent={transparentPixelCount}, opaque={pixels.Length - transparentPixelCount}, partial=0");
}

static string Sha256Rgba(IReadOnlyList<Rgba32> pixels)
{
    byte[] bytes = new byte[checked(pixels.Count * 4)];
    for (int index = 0; index < pixels.Count; index++)
    {
        Rgba32 pixel = pixels[index];
        int offset = index * 4;
        bytes[offset] = pixel.R;
        bytes[offset + 1] = pixel.G;
        bytes[offset + 2] = pixel.B;
        bytes[offset + 3] = pixel.A;
    }
    return Convert.ToHexString(SHA256.HashData(bytes));
}

static void AssertPsxTextureWordTransparencySemantics()
{
    MethodInfo converter = typeof(TerrainPatchExporter).GetMethod(
        "ConvertPsx555ToRgba32",
        BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new MissingMethodException(typeof(TerrainPatchExporter).FullName, "ConvertPsx555ToRgba32");

    Rgba32 Decode(ushort word) => (Rgba32)(converter.Invoke(null, [word])
        ?? throw new InvalidOperationException($"PSX555 converter returned null for 0x{word:X4}."));

    Rgba32 transparentBlack = Decode(0x0000);
    Rgba32 opaqueBlack = Decode(0x8000);
    Rgba32 opaqueRed = Decode(0x001F);
    Rgba32 stpRed = Decode(0x801F);
    Assert(transparentBlack == new Rgba32(0, 0, 0, 0), "PSX texture word 0x0000 must decode as transparent black.");
    Assert(opaqueBlack == new Rgba32(0, 0, 0, 255), "PSX texture word 0x8000 must remain opaque black without primitive translucency state.");
    Assert(opaqueRed == new Rgba32(255, 0, 0, 255), "PSX STP-clear nonblack colors must decode as opaque.");
    Assert(stpRed == new Rgba32(255, 0, 0, 255), "PSX STP-set nonblack colors must remain opaque without primitive translucency state.");
}

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

static void DeleteOutput(string path)
{
    if (File.Exists(path))
        File.Delete(path);
}

sealed record DiscLayoutInfo(int SectorSize, int UserOffset);

sealed record ExactDiffResult(
    int ExpectedDifferenceCount,
    int ObservedExpectedDifferenceCount,
    int UnexpectedDifferenceCount);

sealed record MissingSurfaceAppendEvidence(
    string TargetLevel,
    int TargetTextureId,
    string DonorLevel,
    int DonorTextureId,
    int OldSurfaceCount,
    int NewSurfaceCount,
    int DescriptorGrowthBytes,
    int ExactChangedByteCount,
    string OutputCuePath,
    string ReportPath);

sealed record ScrollingRenderDifference(
    string LevelKey,
    int TextureId,
    string DescriptorTier,
    string RawSha256,
    string InitializedSha256);
