using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;

internal static class ArtisansDragonMetalSmoke
{
    private const int TargetTextureId = 54;
    private const int DonorTextureId = 22;
    private const int ExpectedDescriptorCount = 23;
    private const int ExpectedTargetOwnedBytes = 5_632;
    private const int ExpectedChangedBytes = 5_181;
    private const string PreferredTargetRuntimeKey = "24:4:hp";
    private const string DonorRuntimeKey = "34:3:hp";

    public static async Task RunAsync(string workspaceRoot)
    {
        string sourceImagePath = Path.Combine(workspaceRoot, "Spyro the Dragon (USA).bin");
        string sourceCuePath = Path.Combine(workspaceRoot, "Spyro the Dragon (USA).cue");
        if (!File.Exists(sourceImagePath) || !File.Exists(sourceCuePath))
            throw new FileNotFoundException("The focused Artisans dragon smoke needs the real Spyro BIN/CUE.");

        string canonicalRoot = Path.Combine(workspaceRoot, "_local", "smoke", "native-terrain-texture-export");
        string canonicalPrefix = Path.Combine(canonicalRoot, "SpyroEditor-Artisans-Dragon-GnastyMetal-NativeTextureExport");
        string candidateImagePath = $"{canonicalPrefix}.bin";
        string candidateCuePath = $"{canonicalPrefix}.cue";
        string candidatePlanPath = $"{canonicalPrefix}.terrain-patch-plan.json";
        string candidateReportPath = Path.Combine(canonicalRoot, "artisans-dragon-gnasty-metal-export-smoke.json");
        string candidateMarkdownPath = Path.Combine(canonicalRoot, "artisans-dragon-gnasty-metal-export-smoke.md");
        string candidateManifestPath = Path.Combine(canonicalRoot, "artisans-native-terrain-texture-relocations.json");
        string[] requiredCandidateArtifacts =
        [
            candidateImagePath,
            candidateCuePath,
            candidatePlanPath,
            candidateReportPath,
            candidateMarkdownPath,
            candidateManifestPath
        ];
        string[] missingCandidateArtifacts = requiredCandidateArtifacts.Where(path => !File.Exists(path)).ToArray();
        if (missingCandidateArtifacts.Length > 0)
        {
            throw new FileNotFoundException(
                "The successful public-export candidate is missing. Run Spyro.Editor.TerrainTextureExportSmoke first; " +
                "this compatibility smoke deliberately references that canonical 631 MB candidate instead of copying it again. " +
                $"Missing: {string.Join(", ", missingCandidateArtifacts)}");
        }

        string reportRoot = Path.Combine(workspaceRoot, "_local", "smoke", "artisans-dragon-gnasty-metal-runtime");
        Directory.CreateDirectory(reportRoot);
        string markdownPath = Path.Combine(reportRoot, "artisans-dragon-gnasty-metal-runtime.md");
        string jsonPath = Path.Combine(reportRoot, "artisans-dragon-gnasty-metal-runtime.json");
        string legacyPrefix = Path.Combine(reportRoot, "SpyroEditor-Artisans-Dragon-GnastyMetal22-RESEARCH");
        foreach (string staleLegacyOutput in new[]
                 {
                     $"{legacyPrefix}.bin",
                     $"{legacyPrefix}.cue",
                     $"{legacyPrefix}.terrain-patch-plan.json"
                 })
        {
            if (File.Exists(staleLegacyOutput))
                File.Delete(staleLegacyOutput);
        }

        LevelCatalog catalog = LevelCatalog.Load(workspaceRoot);
        LevelDefinition targetLevel = catalog.FindByKey("artisans")
            ?? throw new InvalidOperationException("Artisans is missing from the level catalog.");
        LevelDefinition donorLevel = catalog.FindByKey("gnastysworld")
            ?? throw new InvalidOperationException("Gnasty's World is missing from the level catalog.");
        Assert(targetLevel.SourceWadEntry == 10 && donorLevel.SourceWadEntry == 70,
            "The focused target/donor WAD entries changed from 10/70.");

        GeometryCandidate targetGeometry = GeometryOverlayLoader.LoadFirstCandidate(
            Path.Combine(workspaceRoot, "editor-cache", "artisans-runtime-scene-editor-overlay.json"));
        GeometryCandidate donorGeometry = GeometryOverlayLoader.LoadFirstCandidate(
            Path.Combine(workspaceRoot, "editor-cache", "gnastysworld-runtime-scene-editor-overlay.json"));
        TerrainPolygon[] knownDragonFaces = targetGeometry.Polygons
            .Where(face => face.SectorIndex == 24 && face.FaceIndex is >= 3 and <= 6 &&
                           face.Detail.Equals("hp", StringComparison.OrdinalIgnoreCase))
            .OrderBy(face => face.FaceIndex)
            .ToArray();
        Assert(knownDragonFaces.Length == 4,
            $"Expected the four known Artisans dragon-head faces, found {knownDragonFaces.Length}.");
        TerrainPolygon targetFace = knownDragonFaces.Single(face =>
            face.RuntimeKey.Equals(PreferredTargetRuntimeKey, StringComparison.OrdinalIgnoreCase));
        TerrainPolygon donorFace = donorGeometry.Polygons.Single(face =>
            face.RuntimeKey.Equals(DonorRuntimeKey, StringComparison.OrdinalIgnoreCase));
        Assert(targetFace.OriginalTextureId == TargetTextureId,
            $"The preferred dragon panel uses texture {targetFace.OriginalTextureId}, expected {TargetTextureId}.");
        Assert(donorFace.OriginalTextureId == DonorTextureId,
            $"The Gnasty metal donor face uses texture {donorFace.OriginalTextureId}, expected {DonorTextureId}.");

        TerrainPatchPlan plan = JsonSerializer.Deserialize<TerrainPatchPlan>(
                await File.ReadAllTextAsync(candidatePlanPath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("The canonical public-export terrain patch plan could not be deserialized.");
        Assert(plan.LevelKey.Equals(targetLevel.Key, StringComparison.OrdinalIgnoreCase) &&
               plan.LevelName.Equals(targetLevel.DisplayName, StringComparison.OrdinalIgnoreCase),
            "The canonical plan is not bound to Artisans.");
        Assert(plan.NativeTextureRelocationCount == 1 && plan.NativeTextureRelocations.Count == 1,
            "The canonical plan did not retain exactly one native terrain texture relocation.");
        NativeTerrainTextureRelocationPatchSummary summary = plan.NativeTextureRelocations.Single();
        Assert(summary.Strategy == "target-owned-in-place" &&
               summary.TargetTextureIds.SequenceEqual([TargetTextureId]) &&
               summary.DonorTextures.Count == 1 &&
               summary.DonorTextures[0].Contains("Gnasty's World", StringComparison.Ordinal) &&
               summary.DonorTextures[0].Contains($"texture {DonorTextureId}", StringComparison.Ordinal) &&
               summary.DonorTextures[0].Contains("WAD 70", StringComparison.Ordinal),
            "The canonical plan lost the exact Artisans/Gnasty target and donor provenance.");
        Assert(summary.CompleteDescriptorCount == ExpectedDescriptorCount &&
               summary.TargetOwnedByteCount == ExpectedTargetOwnedBytes &&
               summary.PatchedByteCount == ExpectedChangedBytes &&
               plan.TotalPatchedBytes == ExpectedChangedBytes,
            "The canonical plan no longer has the source-locked 23-descriptor / 5,632-owned / 5,181-changed proof.");
        Assert(summary.PatchCount == plan.PatchCount &&
               plan.NativeTextureRelocationBytePatchCount == plan.PatchCount &&
               plan.Patches.All(patch =>
                   patch.Kind.Equals("native-terrain-texture-in-place", StringComparison.OrdinalIgnoreCase) &&
                   patch.RuntimeKey.Equals($"texture-{TargetTextureId}", StringComparison.OrdinalIgnoreCase)),
            "The canonical plan contains a non-native-art patch or inconsistent patch counts.");
        Assert(summary.RuntimeControlVerified &&
               summary.OwnershipVerified &&
               summary.ExactIndexedPixelsVerified &&
               summary.ExactPalettesVerified &&
               summary.LogicalReadbackVerified &&
               plan.SkippedEdits.Count == 0,
            $"The canonical plan omitted a required proof or skipped an edit: {string.Join(" | ", plan.SkippedEdits)}");

        using JsonDocument candidateReport = JsonDocument.Parse(await File.ReadAllTextAsync(candidateReportPath));
        JsonElement report = candidateReport.RootElement;
        Assert(report.GetProperty("Passed").GetBoolean(), "The canonical exporter report does not say Passed=true.");
        JsonElement pair = report.GetProperty("Pair");
        Assert(pair.GetProperty("TargetWadEntry").GetInt32() == targetLevel.SourceWadEntry &&
               pair.GetProperty("TargetTextureId").GetInt32() == TargetTextureId &&
               pair.GetProperty("DonorWadEntry").GetInt32() == donorLevel.SourceWadEntry &&
               pair.GetProperty("DonorTextureId").GetInt32() == DonorTextureId,
            "The canonical exporter report pair does not match Artisans texture 54 <- Gnasty's World texture 22.");
        JsonElement exactDiff = report.GetProperty("ExactPhysicalDiff");
        Assert(exactDiff.GetProperty("ExpectedDifferenceCount").GetInt32() == ExpectedChangedBytes &&
               exactDiff.GetProperty("ObservedExpectedDifferenceCount").GetInt32() == ExpectedChangedBytes &&
               exactDiff.GetProperty("UnexpectedDifferenceCount").GetInt32() == 0,
            "The canonical exporter report no longer proves 5,181 exact changes and zero out-of-plan differences.");

        string sourceSha256 = await HashFileAsync(sourceImagePath);
        string candidateSha256 = await HashFileAsync(candidateImagePath);
        long sourceLength = ReadFileLength(sourceImagePath);
        long candidateLength = ReadFileLength(candidateImagePath);
        JsonElement sourceReport = report.GetProperty("Source");
        JsonElement outputReport = report.GetProperty("Output");
        Assert(sourceReport.GetProperty("Unchanged").GetBoolean() &&
               sourceReport.GetProperty("Length").GetInt64() == sourceLength &&
               sourceReport.GetProperty("Sha256").GetString()?.Equals(sourceSha256, StringComparison.OrdinalIgnoreCase) == true,
            "The live retail source no longer matches the unchanged source identity in the canonical report.");
        Assert(candidateLength == sourceLength &&
               outputReport.GetProperty("Length").GetInt64() == candidateLength &&
               outputReport.GetProperty("Sha256").GetString()?.Equals(candidateSha256, StringComparison.OrdinalIgnoreCase) == true,
            "The canonical candidate's live length/hash no longer matches its exporter report.");

        NativeTerrainTextureInPlaceTransplantRequest transplantRequest = new(
            TargetTextureId,
            donorLevel.SourceWadEntry,
            DonorTextureId);
        NativeTerrainTextureInPlaceTransplantSourceProof sourceProof =
            NativeTerrainTextureInPlaceTransplantBuilder.InspectSourceProof(
                sourceImagePath,
                targetLevel,
                transplantRequest);
        NativeTerrainTextureInPlaceTransplantSourceProof candidateProof =
            NativeTerrainTextureInPlaceTransplantBuilder.InspectSourceProof(
                candidateImagePath,
                targetLevel,
                transplantRequest);
        Assert(sourceProof.TargetDescriptorTableSha256.Equals(candidateProof.TargetDescriptorTableSha256, StringComparison.OrdinalIgnoreCase),
            "The canonical candidate changed Artisans' descriptor table.");
        Assert(sourceProof.DonorTexturePagesSha256.Equals(candidateProof.DonorTexturePagesSha256, StringComparison.OrdinalIgnoreCase) &&
               sourceProof.DonorDescriptorTableSha256.Equals(candidateProof.DonorDescriptorTableSha256, StringComparison.OrdinalIgnoreCase),
            "The canonical candidate changed the Gnasty's World donor pages or descriptor table.");
        Assert(!sourceProof.TargetTexturePagesSha256.Equals(candidateProof.TargetTexturePagesSha256, StringComparison.OrdinalIgnoreCase),
            "The canonical candidate did not change Artisans' target texture pages.");

        string cueText = await File.ReadAllTextAsync(candidateCuePath, Encoding.ASCII);
        Assert(cueText.Contains(Path.GetFileName(candidateImagePath), StringComparison.Ordinal),
            "The canonical public-export CUE does not reference its BIN.");
        Assert(!Directory.EnumerateFiles(canonicalRoot, $"{Path.GetFileName(candidateImagePath)}.*.tmp").Any() &&
               !Directory.EnumerateFiles(canonicalRoot, $"{Path.GetFileName(candidateCuePath)}.*.tmp").Any(),
            "The canonical public exporter left a temporary BIN or CUE beside the candidate.");

        string center = $"({targetFace.Center.X:0.##}, {targetFace.Center.Y:0.##}, {targetFace.AvgZ:0.##})";
        StringBuilder markdown = new();
        markdown.AppendLine("# Artisans Dragon Head <- Gnasty Metal 22");
        markdown.AppendLine();
        markdown.AppendLine("Status: **PASSED — PUBLIC EXPORT CANDIDATE VERIFIED**");
        markdown.AppendLine();
        markdown.AppendLine($"- Target record: Artisans texture {TargetTextureId}; representative dragon panel `{targetFace.RuntimeKey}` at `{center}`.");
        markdown.AppendLine($"- Donor record: Gnasty's World texture {DonorTextureId}; representative face `{donorFace.RuntimeKey}`.");
        markdown.AppendLine($"- Public export strategy: `{summary.Strategy}`; {summary.CompleteDescriptorCount} complete descriptors; {summary.TargetOwnedByteCount:N0} target-owned bytes.");
        markdown.AppendLine($"- Exact full-BIN proof: {ExpectedChangedBytes:N0} planned/observed differences and zero outside the retained plan.");
        markdown.AppendLine($"- Safety: runtime control, ownership, indexed pixels, palettes, and final logical readback all verified; no skipped edits.");
        markdown.AppendLine($"- Retail source remained {sourceLength:N0} bytes with SHA-256 `{sourceSha256}`.");
        markdown.AppendLine($"- Candidate remained {candidateLength:N0} bytes with SHA-256 `{candidateSha256}`.");
        markdown.AppendLine();
        markdown.AppendLine($"Runtime candidate: `{candidateCuePath}`");
        markdown.AppendLine();
        markdown.AppendLine($"Canonical exporter report: `{candidateReportPath}`");
        markdown.AppendLine();
        markdown.AppendLine("This compatibility smoke reused the canonical successful candidate and did not create a second 631 MB BIN.");
        await File.WriteAllTextAsync(markdownPath, markdown.ToString());

        await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(new
        {
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            Status = "passed-public-export-candidate",
            Passed = true,
            CandidateReused = true,
            DuplicateBinCreated = false,
            Target = new
            {
                Level = targetLevel.DisplayName,
                TextureId = TargetTextureId,
                targetFace.RuntimeKey,
                Center = new { X = targetFace.Center.X, Y = targetFace.Center.Y, Z = targetFace.AvgZ },
                CandidateFaces = knownDragonFaces.Select(face => new
                {
                    face.RuntimeKey,
                    face.OriginalTextureId,
                    Center = new { X = face.Center.X, Y = face.Center.Y, Z = face.AvgZ }
                }).ToArray()
            },
            Donor = new
            {
                Level = donorLevel.DisplayName,
                TextureId = DonorTextureId,
                donorFace.RuntimeKey
            },
            PublicExporterProof = summary,
            ExactPhysicalDiff = new
            {
                ExpectedDifferenceCount = ExpectedChangedBytes,
                ObservedExpectedDifferenceCount = ExpectedChangedBytes,
                UnexpectedDifferenceCount = 0
            },
            Source = new { Path = sourceImagePath, Length = sourceLength, Sha256 = sourceSha256, Unchanged = true },
            Candidate = new
            {
                BinPath = candidateImagePath,
                CuePath = candidateCuePath,
                PlanPath = candidatePlanPath,
                ExporterReportPath = candidateReportPath,
                ExporterMarkdownPath = candidateMarkdownPath,
                ManifestPath = candidateManifestPath,
                Length = candidateLength,
                Sha256 = candidateSha256
            },
            StructuralReadback = new
            {
                TargetTexturePagesChanged = true,
                TargetDescriptorTableUnchanged = true,
                DonorTexturePagesUnchanged = true,
                DonorDescriptorTableUnchanged = true
            }
        }, new JsonSerializerOptions { WriteIndented = true }));

        Console.WriteLine("Artisans dragon <- Gnasty metal 22: PASSED; canonical public-export candidate verified.");
        Console.WriteLine($"Face: {targetFace.RuntimeKey}; center: {center}; descriptors: {summary.CompleteDescriptorCount}; changed bytes: {summary.PatchedByteCount:N0}; unexpected differences: 0.");
        Console.WriteLine($"Reused CUE: {candidateCuePath}");
        Console.WriteLine($"Compatibility report: {markdownPath}");
    }

    private static async Task<string> HashFileAsync(string path)
    {
        await using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream));
    }

    private static long ReadFileLength(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return stream.Length;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
