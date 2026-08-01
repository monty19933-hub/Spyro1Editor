using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;

const int DonorTextureId = 17;
const int StructuralTemplateTextureId = 0;

string workspaceRoot = args.Length > 0 ? Path.GetFullPath(args[0]) : Directory.GetCurrentDirectory();
string sourceImagePath = Path.Combine(workspaceRoot, "Spyro the Dragon (USA).bin");
string sourceCuePath = Path.Combine(workspaceRoot, "Spyro the Dragon (USA).cue");
string outputRoot = Path.Combine(workspaceRoot, "_local", "research", "global-terrain-texture-repack");
Directory.CreateDirectory(outputRoot);
string sourceShaBefore = Sha256File(sourceImagePath);

LevelCatalog catalog = LevelCatalog.Load(workspaceRoot);
LevelDefinition artisans = catalog.FindByKey("artisans")
    ?? throw new InvalidOperationException("Artisans is missing from the catalog.");
LevelDefinition gnastysWorld = catalog.FindByKey("gnastysworld")
    ?? throw new InvalidOperationException("Gnasty's World is missing from the catalog.");

NativeTerrainTextureRecordAppendResearchRequest appendRequest = new(
    sourceImagePath,
    sourceCuePath,
    artisans,
    artisans,
    StructuralTemplateTextureId);
if (!NativeTerrainTextureRecordAppendResearch.TryBuild(
        appendRequest,
        out NativeTerrainTextureRecordAppendResearchPlan? maybeAppendPlan,
        out string appendFailure) || maybeAppendPlan == null)
{
    throw new InvalidOperationException($"Structural 68->69 append failed: {appendFailure}");
}
NativeTerrainTextureRecordAppendResearchPlan appendPlan = maybeAppendPlan;
NativeTerrainTextureRecordAppendResearchOutput structural =
    await NativeTerrainTextureRecordAppendResearch.WriteCandidateAsync(
        appendRequest,
        appendPlan,
        Path.Combine(outputRoot, "Artisans-T68-STRUCTURAL"));

NativeTerrainTextureRelocationImport appendedDonor = new(
    appendPlan.AppendedTextureId,
    gnastysWorld.SourceWadEntry,
    DonorTextureId,
    "both");
if (!NativeTerrainTextureGlobalRepackerResearch.TryBuild(
        new NativeTerrainTextureGlobalRepackResearchRequest(
            structural.OutputImagePath,
            artisans,
            [appendedDonor]),
        out NativeTerrainTextureGlobalRepackResearchPlan? maybeRepack,
        out string repackFailure) || maybeRepack == null)
{
    throw new InvalidOperationException($"Global terrain texture repack failed: {repackFailure}");
}
NativeTerrainTextureGlobalRepackResearchPlan repack = maybeRepack;

Assert(repack.TextureCount == 69, "Artisans candidate must contain 69 native texture records.");
Assert(repack.MovableTextureIds.Count == 68 && repack.FixedRuntimeControlledTextureIds.SequenceEqual([23]),
    "Artisans movable/fixed texture closure changed.");
Assert(repack.ProtectedByteCount == 111_822 && repack.ReleasedTerrainByteCount == 365_056,
    $"Artisans protected/released byte proof changed: {repack.ProtectedByteCount}/{repack.ReleasedTerrainByteCount}.");
Assert(repack.AllocatedByteCount == 370_688 && repack.FreeByteCount == 41_778,
    $"Artisans global packed/free byte proof changed: {repack.AllocatedByteCount}/{repack.FreeByteCount}.");
Assert(repack.PixelStorageGroupCount == 329 && repack.PaletteStorageGroupCount == 134,
    "Artisans storage-group closure changed.");
Assert(repack.PixelAliasPairCount == 1_099 && repack.PaletteAliasPairCount == 14_320,
    "Artisans exact alias inventory changed.");
Assert(repack.RewrittenDescriptorCount == 68 * 23 && repack.FixedDescriptorCount == 23,
    "Artisans rewritten/fixed descriptor count changed.");
Assert(repack.PackingStrategy == "lq-first-palette-components-best-contact",
    "Artisans deterministic packing strategy changed.");
Assert(repack.ExactIndexedPixelReadbackVerified && repack.ExactPaletteReadbackVerified &&
       repack.PixelAliasRelationshipsPreserved && repack.PaletteAliasRelationshipsPreserved &&
       repack.LowDetailAliasPreserved && repack.ProtectedStoragePreserved &&
       repack.FixedDescriptorRowsPreserved && repack.TargetMaterialBitsPreserved,
    "One or more global static proof invariants failed.");
Assert(repack.RequiresDuckStationRuntimeProof, "Static proof must not be described as runtime proof.");
Assert(repack.PackedOverrideRecords.Count == 1 &&
       repack.PackedOverrideRecords[0].TargetTextureId == 68 &&
       repack.PackedOverrideRecords[0].DonorWadEntry == gnastysWorld.SourceWadEntry &&
       repack.PackedOverrideRecords[0].DonorTextureId == DonorTextureId &&
       repack.PackedOverrideRecords[0].LowDetailRow.Length == 16 &&
       repack.PackedOverrideRecords[0].HighDetailRow.Length == 168,
    "The candidate did not expose one exact packed T68 donor row.");

NativeTerrainTextureGlobalRepackSyntheticRequest syntheticRequest = new(
    sourceImagePath,
    artisans,
    [],
    [new NativeTerrainTextureGlobalRepackSyntheticRecord(
        appendPlan.AppendedTextureId,
        gnastysWorld.SourceWadEntry,
        DonorTextureId,
        MaterialTemplateTextureId: 0)]);
if (!NativeTerrainTextureGlobalRepackerResearch.TryBuild(
        syntheticRequest,
        out NativeTerrainTextureGlobalRepackSyntheticPlan? maybeSynthetic,
        out string syntheticFailure) || maybeSynthetic == null)
{
    throw new InvalidOperationException($"Retail-plus-synthetic global repack failed: {syntheticFailure}");
}
NativeTerrainTextureGlobalRepackSyntheticPlan synthetic = maybeSynthetic;
Assert(synthetic.SourceTextureCount == 68 && synthetic.OutputTextureCount == 69,
    "Synthetic Artisans texture count proof changed.");
Assert(synthetic.OriginalMovableRecords.Count == 67 && synthetic.SyntheticRecords.Count == 1,
    "Synthetic Artisans did not return every original movable row plus T68.");
Assert(synthetic.OriginalDescriptorPatches.Count > 0 && synthetic.TexturePagePatches.Count > 0,
    "Synthetic Artisans did not expose retail-offset descriptor and page patches.");
Assert(synthetic.SourceDescriptorOffsetsRemainUnmodified && synthetic.SyntheticRowsModeledOnlyInMemory &&
       synthetic.RequiresStructuralComposer && synthetic.RequiresDuckStationRuntimeProof,
    "Synthetic planning boundary was weakened.");
Assert(PatchesEquivalent(repack.TexturePagePatches, synthetic.TexturePagePatches),
    $"Candidate and retail-plus-synthetic paths produced different texture-page patches: " +
    $"candidate={PatchDigest(repack.TexturePagePatches)} ({repack.TexturePagePatches.Count}), " +
    $"synthetic={PatchDigest(synthetic.TexturePagePatches)} ({synthetic.TexturePagePatches.Count}); " +
    $"protected={repack.ProtectedByteCount}/{synthetic.PackingProof.ProtectedByteCount}, " +
    $"released={repack.ReleasedTerrainByteCount}/{synthetic.PackingProof.ReleasedTerrainByteCount}, " +
    $"packing={repack.PackingStrategy}/{synthetic.PackingProof.PackingStrategy}.");
Assert(synthetic.PackingProof.AllocatedByteCount == repack.AllocatedByteCount &&
       synthetic.PackingProof.FreeByteCount == repack.FreeByteCount &&
       synthetic.PackingProof.PixelAliasPairCount == repack.PixelAliasPairCount &&
       synthetic.PackingProof.PaletteAliasPairCount == repack.PaletteAliasPairCount,
    "Candidate and retail-plus-synthetic paths produced different packing metrics.");

string packedPrefix = Path.Combine(outputRoot, "Artisans-private-T68-Gnasty-T17-GLOBAL-REPACK-NO-FACE");
NativeTerrainTextureGlobalRepackResearchOutput packedOutput =
    await NativeTerrainTextureGlobalRepackerResearch.WriteCandidateAsync(
        structural.OutputImagePath,
        structural.OutputCuePath,
        repack,
        packedPrefix);
Assert(packedOutput.PatchPreimagesVerified && packedOutput.ExactPatchReadbackVerified &&
       packedOutput.SourceImageUnchanged && packedOutput.RequiresDuckStationRuntimeProof,
    "Global pack candidate writer did not retain every source/readback/runtime boundary.");

Assert(Sha256File(sourceImagePath) == sourceShaBefore, "Retail source BIN changed during research export.");
Assert(FileLength(packedOutput.OutputImagePath) == FileLength(sourceImagePath),
    "Fixed-size test BIN length changed.");
IReadOnlyList<TerrainTextureSlot> finalSlots = TerrainPatchExporter.InspectTextureSlots(packedOutput.OutputImagePath, artisans);
Assert(finalSlots.Count == 69 && finalSlots.Single(item => item.TextureId == 68) is
    { HasNormalDescriptors: true, HasCloseDescriptors: true, NormalDescriptorCount: 4, CloseDescriptorCount: 16 },
    "Final test BIN did not reparse T68 as one complete native record.");
NativeTerrainTextureRuntimeControlAudit finalRuntime =
    NativeTerrainTextureRuntimeControlScanner.Inspect(packedOutput.OutputImagePath, artisans);
Assert(finalRuntime.Complete && finalRuntime.TextureCount == 69 && finalRuntime.IsRuntimePersistentTarget(68),
    "Final test BIN runtime-control scan did not prove T68 persistent.");
Assert(NativeTerrainTextureGlobalRepackerResearch.TryVerifyCandidateLogicalReadback(
        packedOutput.OutputImagePath,
        artisans,
        repack.PackedOverrideRecords,
        sourceImagePath,
        out string logicalFailure),
    $"Final test BIN donor logical readback failed: {logicalFailure}");

string reportPath = packedPrefix + "-static-proof.json";
await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(new
{
    Status = "static-proof-passed-runtime-test-required",
    Source = new { Path = sourceImagePath, Sha256 = sourceShaBefore, Unchanged = true },
    Output = new { Bin = packedOutput.OutputImagePath, Cue = packedOutput.OutputCuePath, packedOutput.OutputImageSha256 },
    Target = new { Level = artisans.DisplayName, TextureId = 68 },
    Donor = new { Level = gnastysWorld.DisplayName, TextureId = DonorTextureId },
    repack.TextureCount,
    Movable = repack.MovableTextureIds.Count,
    Fixed = repack.FixedRuntimeControlledTextureIds,
    repack.ProtectedByteCount,
    repack.ReleasedTerrainByteCount,
    repack.AllocatedByteCount,
    repack.FreeByteCount,
    repack.PixelStorageGroupCount,
    repack.PaletteStorageGroupCount,
    repack.PixelAliasPairCount,
    repack.PaletteAliasPairCount,
    repack.ChangedTexturePageByteCount,
    repack.ChangedDescriptorByteCount,
    PatchCount = repack.Patches.Count,
    CandidateAndSyntheticPagePatchesEquivalent = true,
    FinalPatchPreimagesAndReadbackVerified = true,
    FaceAssignment = "none-this-is-a-packer-cross-check-only",
    FinalRuntimeScannerPersistent = true,
    FinalDonorLogicalReadbackVerified = true,
    DuckStationRuntimeProof = false
}, new JsonSerializerOptions { WriteIndented = true }));

Console.WriteLine("Global terrain texture repack static proof passed.");
Console.WriteLine($"Records: {repack.TextureCount}; movable/fixed: {repack.MovableTextureIds.Count}/{repack.FixedRuntimeControlledTextureIds.Count}.");
Console.WriteLine($"Protected/released/allocated/free: {repack.ProtectedByteCount:N0}/{repack.ReleasedTerrainByteCount:N0}/{repack.AllocatedByteCount:N0}/{repack.FreeByteCount:N0} bytes.");
Console.WriteLine($"Groups: {repack.PixelStorageGroupCount} pixel + {repack.PaletteStorageGroupCount} palette; aliases: {repack.PixelAliasPairCount} pixel + {repack.PaletteAliasPairCount} palette.");
Console.WriteLine($"Packing: {repack.PackingStrategy}; patches: {repack.Patches.Count}.");
Console.WriteLine($"Static no-face candidate CUE: {packedOutput.OutputCuePath}");
Console.WriteLine($"Static proof report: {reportPath}");
Console.WriteLine("DuckStation runtime proof remains required; this smoke does not promote the research path.");

static bool PatchesEquivalent(
    IReadOnlyList<NativeTerrainTextureRelocationPatch> first,
    IReadOnlyList<NativeTerrainTextureRelocationPatch> second)
{
    NativeTerrainTextureRelocationPatch[] left = first.OrderBy(item => item.WadOffset).ToArray();
    NativeTerrainTextureRelocationPatch[] right = second.OrderBy(item => item.WadOffset).ToArray();
    return left.Length == right.Length && left.Zip(right).All(pair =>
        pair.First.WadOffset == pair.Second.WadOffset &&
        pair.First.Before.SequenceEqual(pair.Second.Before) &&
        pair.First.After.SequenceEqual(pair.Second.After));
}

static string PatchDigest(IReadOnlyList<NativeTerrainTextureRelocationPatch> patches)
{
    using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    foreach (NativeTerrainTextureRelocationPatch patch in patches.OrderBy(item => item.WadOffset))
    {
        hash.AppendData(BitConverter.GetBytes(patch.WadOffset));
        hash.AppendData(patch.Before);
        hash.AppendData(patch.After);
    }
    return Convert.ToHexString(hash.GetHashAndReset());
}

static string Sha256File(string path)
{
    using FileStream stream = File.OpenRead(path);
    return Convert.ToHexString(SHA256.HashData(stream));
}

static long FileLength(string path)
{
    using FileStream stream = File.OpenRead(path);
    return stream.Length;
}

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
