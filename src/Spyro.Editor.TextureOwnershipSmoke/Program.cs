using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;

string workspaceRoot = args.Length > 0 ? Path.GetFullPath(args[0]) : Directory.GetCurrentDirectory();
string sourceImagePath = Path.Combine(workspaceRoot, "Spyro the Dragon (USA).bin");
string outputRoot = Path.Combine(workspaceRoot, "_local", "research", "native-texture-page-ownership");
Directory.CreateDirectory(outputRoot);

LevelCatalog catalog = LevelCatalog.Load(workspaceRoot);
LevelDefinition[] levels = LevelRealmCatalog
    .OrderLevels(catalog.Levels.Where(level => level.SourceWadEntry >= 0))
    .ToArray();
Assert(levels.Length == 35, $"Expected all 35 mapped retail levels, got {levels.Length}.");

List<NativeTexturePageOwnershipReport> reports = [];
foreach (LevelDefinition level in levels)
{
    NativeTexturePageOwnershipReport report = NativeTexturePageOwnershipScanner.Scan(sourceImagePath, level);
    ValidateReport(report);
    reports.Add(report);
    Console.WriteLine(
        $"{report.LevelName}: terrain={report.Terrain.TextureCount} records; known={report.KnownOwnedByteCount:N0}; unknown-nonzero={report.UnknownNonZeroByteCount:N0}; private={report.ProvablyPrivateByteCount:N0}; safe={report.ProvablyPrivatePixelAndClutSpace}");
}

Assert(reports.Count == 35, $"Ownership scan returned {reports.Count} reports instead of 35.");
Assert(reports.Sum(report => report.Terrain.CloseSide16Count) > 0,
    "Retail close-tier audit found no extent-derived 16x16 descriptors; the scanner likely regressed to a fixed 32x32 assumption.");
Assert(reports.All(report => report.AllConsumerClosureComplete && report.ProvablyPrivatePixelAndClutSpace),
    "At least one retail level did not close every source-bound texture-page consumer or expose its remaining private zero-byte union.");
foreach (LevelDefinition level in levels)
{
    bool proofReady = NativeTexturePageOwnershipScanner.TryBuildExternalOwnershipProof(
        sourceImagePath,
        level,
        out NativeTexturePageOwnershipReport proofReport,
        out NativeTexturePageExternalOwnershipProof? proof,
        out string proofFailure);
    Assert(proofReady && proof != null,
        $"{level.DisplayName}: source-bound external ownership proof was not emitted after closure: {proofFailure}");
    Assert(proof!.TargetWadEntry == level.SourceWadEntry &&
           proof.TexturePagesLength == proofReport.TexturePagesSubfileByteLength &&
           proof.TexturePagesSha256 == proofReport.TexturePagesSubfileSha256 &&
           proof.OwnedRanges.Count > 0,
        $"{level.DisplayName}: emitted ownership proof is not bound to the complete source texture-page subfile and protected ranges.");
}

NativeTexturePageOwnershipReport artisans = reports.Single(report =>
    string.Equals(report.LevelKey, "artisans", StringComparison.OrdinalIgnoreCase));
NativeTexturePageOwnershipReport gnastysWorld = reports.Single(report =>
    string.Equals(report.LevelKey, "gnastysworld", StringComparison.OrdinalIgnoreCase));
LevelDefinition artisansLevel = levels.Single(level =>
    string.Equals(level.Key, "artisans", StringComparison.OrdinalIgnoreCase));
LevelDefinition gnastysWorldLevel = levels.Single(level =>
    string.Equals(level.Key, "gnastysworld", StringComparison.OrdinalIgnoreCase));
NativeTexturePageTargetStorageIsolationReport artisansTexture54Isolation =
    NativeTexturePageOwnershipScanner.InspectTargetStorageIsolation(sourceImagePath, artisansLevel, 54);
Assert(artisansTexture54Isolation.TargetOwnedByteCount == 5_632 &&
       artisansTexture54Isolation.TargetExclusiveByteCount == 5_632 &&
       artisansTexture54Isolation.AnyDecodedConsumerOverlapByteCount == 0 &&
       artisansTexture54Isolation.DecodedConsumerExclusive &&
       artisansTexture54Isolation.AllConsumerClosureComplete,
    "Artisans texture 54 did not retain its source-proven 5,632-byte all-consumer-exclusive footprint.");
NativeTextureOwnershipScopeResult artisansResidents = artisans.Scopes.Single(scope =>
    scope.Scope == "resident-actors-and-scenery");
Assert(artisansResidents.ProofState == NativeTextureOwnershipProofState.ProvenEnumerated,
    "Artisans resident ownership did not close under the source-backed retail model grammars.");
Assert(artisansResidents.ModelCount == 34 &&
       artisansResidents.AnimatedModelCount == 32 &&
       artisansResidents.SimpleModelCount == 2,
    "Artisans resident root classification changed from the source-validated 34/32/2 result.");
Assert(artisansResidents.FaceTableCount == 72 &&
       artisansResidents.TexturedFaceCount == 985 &&
       artisansResidents.UnresolvedFaceTableCount == 0 &&
       artisansResidents.AmbiguousFaceTableCount == 0,
    "Artisans animated face parsing changed from the exact 72-table/985-textured/zero-error renderer result.");

NativeTerrainTextureRecordStorageRequirement donorRequirement =
    NativeTexturePageOwnershipScanner.InspectTerrainRecordStorage(sourceImagePath, gnastysWorldLevel, textureId: 22);
Assert(donorRequirement.FormatComplete, "Gnasty's World texture 22 did not decode as one complete two-LQ/21-HQ record.");
Assert(donorRequirement.LowDetailDescriptorCount == 2 && donorRequirement.HqDescriptorCount == 21,
    "Gnasty's World texture 22 storage requirement omitted a native terrain tier.");
Assert(donorRequirement.HqSideHistogram.Keys.Any(side => side != 32),
    "Gnasty's World texture 22 did not retain raw extent-derived HQ sides; fixed 32x32 accounting may have returned.");

NativeTexturePrivateSpaceAssessment groundToWater =
    NativeTexturePageOwnershipScanner.AssessNativeTextureReuse(
        artisans,
        "Artisans ground -> already resident Artisans water texture");
Assert(groundToWater.Safe && !groundToWater.RequiresNewStorage && groundToWater.RequiredTotalByteCount == 0,
    "Same-level Artisans ground-to-water reuse unexpectedly requested private storage.");

NativeTexturePrivateSpaceAssessment dragonMetal =
    NativeTexturePageOwnershipScanner.AssessPrivateSpace(
        artisans,
        "Artisans dragon texture 54 <- Gnasty's World texture 22",
        donorRequirement.CompleteRecordPixelByteCount,
        donorRequirement.CompleteRecordClutByteCount);
Assert(dragonMetal.RequiresNewStorage && !dragonMetal.Safe,
    "A coarse Artisans private-byte count was incorrectly promoted to a descriptor-encodable relocation plan.");
Assert(dragonMetal.Blockers.Any(blocker => blocker.Contains("descriptor-encodable", StringComparison.OrdinalIgnoreCase)),
    "Artisans cross-level assessment did not preserve the concrete allocator/readback gate.");

string jsonPath = Path.Combine(outputRoot, "texture-page-ownership-readiness.json");
string markdownPath = Path.Combine(outputRoot, "texture-page-ownership-readiness.md");
JsonSerializerOptions jsonOptions = new()
{
    WriteIndented = true,
    Converters = { new JsonStringEnumConverter() }
};
await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(new
{
    GeneratedAtUtc = DateTimeOffset.UtcNow,
    SourceImage = sourceImagePath,
    RetailMapping = new
    {
        AddressableBytes = NativeTexturePageOwnershipScanner.AddressableTexturePageBytes,
        Upload = "RECT(512,0,512,512) in 16-bit VRAM words",
        PackedPixelAddress = "packed = y * 0x400 + (full VRAM byte X - 0x400)",
        SourceBoundary = "Only the first 0x80000 bytes of m_VramSramOffset are VRAM; the remainder is SPU payload.",
        HqFootprint = "side=max(abs(xmax-xmin),abs(ymax-ymin))+1, projected through descriptor orientation; 8-bpp",
        HqClut = "512 bytes per decoded descriptor palette",
        LqFootprint = "32x32 4-bpp plus sixteen 32-byte palette rows"
    },
    ProofPolicy = new
    {
        Rule = "Never infer free storage from terrain-only nonuse or zero-filled bytes.",
        UnknownNonZero = "Protected as an unclassified runtime-owned region.",
        UnknownZero = "Protected as UnknownUnprovenZero until all runtime consumer scopes are closed.",
        CurrentResult = "All 35 levels have source-bound ownership closure. Allocation still requires a pair-specific descriptor-encodable allocator plan; in-place replacement requires a target-exclusive footprint proof."
    },
    Artisans = new
    {
        ResidentOwnershipClosure = artisansResidents,
        SourceBackedGrammar = new
        {
            Animated = "Per-command retail renderer grammar: triangle 8/20, quad 12/24, special textured 20; descriptors are the final three words; exact table byte end required.",
            Simple = "Header byte 1 face count, +0x0C root-relative face pointer, exactly count*8 bytes; renderer emits no textured primitives.",
            PrimarySources = new[]
            {
                "work/spyro-1-decomp/include/moby.h",
                "work/spyro-1-decomp/src/loaders.c:PatchMobyModelPointers",
                "work/spyro-1-decomp/asm/renderers/r_moby.s:func_8001F798/func_800208FC/func_80022A2C"
            },
            SpyroEditCrossCheck = "/tmp/spyroedit-source uses a simplified table-wide 8/20-byte heuristic selected from the second word and first animation; it is not used as retail ownership authority."
        },
        GroundToWater = groundToWater,
        DragonTexture54FromGnastysWorldTexture22 = dragonMetal,
        Texture54TargetStorageIsolation = artisansTexture54Isolation,
        DonorRequirement = donorRequirement,
        TargetTextureId = 54,
        DonorTextureId = 22,
        DonorLevel = gnastysWorld.LevelName
    },
    Levels = reports
}, jsonOptions));
await File.WriteAllTextAsync(markdownPath, BuildMarkdown(reports, artisans, gnastysWorld, groundToWater, dragonMetal, donorRequirement, artisansTexture54Isolation));

Console.WriteLine();
Console.WriteLine("Native texture-page ownership smoke passed for all 35 levels.");
Console.WriteLine($"Artisans ground -> water: no allocation required, storage-safe={groundToWater.Safe}.");
Console.WriteLine($"Artisans tex54 <- Gnasty's World tex22: required={dragonMetal.RequiredTotalByteCount:N0} bytes, safe={dragonMetal.Safe}.");
Console.WriteLine($"Artisans texture 54 target-owned footprint: {artisansTexture54Isolation.TargetExclusiveByteCount:N0} exclusive bytes; decoded overlap={artisansTexture54Isolation.AnyDecodedConsumerOverlapByteCount}.");
Console.WriteLine($"JSON: {jsonPath}");
Console.WriteLine($"Markdown: {markdownPath}");

static void ValidateReport(NativeTexturePageOwnershipReport report)
{
    Assert(report.AddressableTexturePageByteLength == NativeTexturePageOwnershipScanner.AddressableTexturePageBytes,
        $"{report.LevelName}: scanner did not bind exactly the retail first 0x80000 VRAM bytes.");
    Assert(report.AddressableTexturePagesSha256.Length == 64,
        $"{report.LevelName}: source-bound addressable texture SHA256 is missing.");
    Assert(report.TexturePagesSubfileByteLength >= report.AddressableTexturePageByteLength,
        $"{report.LevelName}: VRAM/SPU subfile is smaller than its addressable texture prefix.");
    Assert(report.Terrain.TextureCount > 0,
        $"{report.LevelName}: no native terrain records decoded.");
    Assert(report.Terrain.LowDetailDescriptorCount == report.Terrain.TextureCount * 2,
        $"{report.LevelName}: LQ ownership did not enumerate two descriptors per terrain record.");
    Assert(report.Terrain.LeadingDescriptorCount == report.Terrain.TextureCount,
        $"{report.LevelName}: leading HQ ownership did not enumerate one descriptor per terrain record.");
    Assert(report.Terrain.NormalDescriptorCount == report.Terrain.TextureCount * 4,
        $"{report.LevelName}: normal HQ ownership did not enumerate four descriptors per terrain record.");
    Assert(report.Terrain.CloseDescriptorCount == report.Terrain.TextureCount * 16,
        $"{report.LevelName}: close HQ ownership did not enumerate sixteen descriptors per terrain record.");
    Assert(report.Terrain.CloseSide16Count + report.Terrain.CloseSide32Count + report.Terrain.CloseOtherSideCount == report.Terrain.CloseDescriptorCount,
        $"{report.LevelName}: close-tier side histogram does not cover every descriptor.");
    Assert(report.Scopes.Select(scope => scope.Scope).Distinct(StringComparer.Ordinal).Count() == 9,
        $"{report.LevelName}: expected nine explicit ownership proof scopes.");
    Assert(report.Scopes.Any(scope => scope.Scope == "particle-tables" && scope.ProofState == NativeTextureOwnershipProofState.ProvenEnumerated),
        $"{report.LevelName}: validated particle-table proof scope is missing or unproven.");
    Assert(report.Scopes.Any(scope => scope.Scope == "resident-actors-and-scenery"),
        $"{report.LevelName}: resident actor/scenery proof scope is missing.");
    NativeTextureOwnershipScopeResult playerScope = report.Scopes.Single(scope => scope.Scope == "spyro-player");
    Assert(playerScope.ProofState == NativeTextureOwnershipProofState.ProvenEnumerated &&
           playerScope.ModelCount == 1 &&
           playerScope.FaceTableCount > 0 &&
           playerScope.UnresolvedFaceTableCount == 0,
        $"{report.LevelName}: shared PETE.WAD Spyro ownership is missing or unproven: " +
        string.Join(" ", playerScope.Blockers));
    NativeTextureOwnershipScopeResult hudScope = report.Scopes.Single(scope => scope.Scope == "hud-and-level-global");
    Assert(hudScope.ProofState == NativeTextureOwnershipProofState.ProvenEnumerated &&
           hudScope.ModelCount > 0 &&
           hudScope.AnimatedModelCount + hudScope.SimpleModelCount == hudScope.ModelCount &&
           hudScope.UnresolvedFaceTableCount == 0,
        $"{report.LevelName}: shared PETE.WAD HUD/global ownership is missing or unproven: " +
        $"models={hudScope.ModelCount}, animated={hudScope.AnimatedModelCount}, simple={hudScope.SimpleModelCount}, " +
        $"tables={hudScope.FaceTableCount}, textured={hudScope.TexturedFaceCount}, unresolved={hudScope.UnresolvedFaceTableCount}; " +
        string.Join(" ", hudScope.Blockers));
    NativeTextureOwnershipScopeResult otherScope = report.Scopes.Single(scope => scope.Scope == "other-runtime-consumers");
    Assert(otherScope.ProofState == NativeTextureOwnershipProofState.ProvenEnumerated &&
           otherScope.UnresolvedFaceTableCount == 0 &&
           otherScope.Blockers.Count == 0,
        $"{report.LevelName}: dragon/overlay/cyclorama runtime-consumer closure is missing or unresolved.");
    Assert(report.KnownOwnedByteCount + report.UnknownNonZeroByteCount + report.UnknownUnprovenZeroByteCount == report.AddressableTexturePageByteLength,
        $"{report.LevelName}: known/unknown ownership classes do not partition the addressable bytes.");
    Assert(report.UnknownNonZeroRangesSha256.Length == 64 &&
           ((report.UnknownNonZeroByteCount == 0 && report.UnknownNonZeroRegionCount == 0) ||
            (report.UnknownNonZeroByteCount > 0 && report.UnknownNonZeroRegionCount > 0)),
        $"{report.LevelName}: unclassified nonzero regions were not consistently enumerated and hashed.");
    Assert(report.AllConsumerClosureComplete && report.SafetyBlockers.Count == 0,
        $"{report.LevelName}: source-bound ownership did not close cleanly: {string.Join(" ", report.SafetyBlockers)}");
    Assert(report.ProtectedByteCount == report.KnownOwnedByteCount + report.UnknownNonZeroByteCount,
        $"{report.LevelName}: protected-byte count does not equal decoded consumers plus conservative nonzero storage.");
    Assert(report.ProvablyPrivateByteCount == report.UnknownUnprovenZeroByteCount &&
           report.ProvablyPrivateByteCount > 0 && report.ProvablyPrivatePixelAndClutSpace,
        $"{report.LevelName}: remaining source-zero bytes were not exposed only after complete consumer closure.");
    Assert(report.ProtectedRanges.Sum(range => range.Length) == report.ProtectedByteCount,
        $"{report.LevelName}: compressed protected ranges do not cover the exact protected-byte union.");
}

static string BuildMarkdown(
    IReadOnlyList<NativeTexturePageOwnershipReport> reports,
    NativeTexturePageOwnershipReport artisans,
    NativeTexturePageOwnershipReport gnastysWorld,
    NativeTexturePrivateSpaceAssessment groundToWater,
    NativeTexturePrivateSpaceAssessment dragonMetal,
    NativeTerrainTextureRecordStorageRequirement donorRequirement,
    NativeTexturePageTargetStorageIsolationReport texture54Isolation)
{
    StringBuilder text = new();
    text.AppendLine("# Native texture-page ownership readiness");
    text.AppendLine();
    text.AppendLine("## Result");
    text.AppendLine();
    text.AppendLine("**All 35 retail levels now have source-bound texture-page ownership closure.** Terrain, particles, resident models, shared Spyro/HUD models, level Tiledefs, and every LevelHeader `m_Dragons` cutscene model root are enumerated. Cycloramas are untextured; level-overlay searches found no source-bound texture-page owner; pause/flight captures are transient framebuffer operations. Nonzero unclassified bytes remain protected. Remaining zero bytes become allocator candidates only after this closure, and every donor/target pair still needs a descriptor-encodable plan plus logical readback.");
    text.AppendLine();
    text.AppendLine("The retail loader reads exactly the first `0x80000` bytes from `m_VramSramOffset` and uploads them to `RECT(512,0,512,512)`. In packed file coordinates, `offset = y * 0x400 + (full VRAM byte X - 0x400)`. Bytes after that prefix are SPU payload and are excluded from texture capacity.");
    text.AppendLine();
    text.AppendLine("Terrain coverage includes both TexLq descriptors, the byte-identical leading 4-bpp LQ/sprite alias, four normal HQ descriptors, and all sixteen close HQ descriptors per record. HQ is 8-bpp with a full 512-byte CLUT. Its side is derived from the raw encoded extents and projected through the eight-entry orientation matrix; close descriptors are commonly 16x16 and are not forced to 32x32. LQ is protected as 32x32 4-bpp plus all sixteen 16-color fade-palette rows.");
    text.AppendLine();
    text.AppendLine("Particle textures are located through the validated component chain and parsed as `(type:u16, payloadLength:u16, ParticleTexture[8])`. Resident model coverage walks every positive `LevelHeader.m_ModelOffsets[1..]`, every unique animation/LP face table, and every textured face. PETE.WAD coverage adds the actor-0 Spyro model plus all shared HUD, gem, key, number, punctuation, and letter models. The 15 LevelSceneHeader Tiledefs protect flame, shadow, unused, orb/egg sprites, superflame, and specular-metal.");
    text.AppendLine();
    text.AppendLine("## Artisans decisions");
    text.AppendLine();
    NativeTextureOwnershipScopeResult artisansResidentClosure = artisans.Scopes.Single(scope => scope.Scope == "resident-actors-and-scenery");
    text.AppendLine($"- **Resident-model ownership is source-closed:** `{artisansResidentClosure.ProofState}`. The scanner classified {artisansResidentClosure.ModelCount} unique roots ({artisansResidentClosure.AnimatedModelCount} animated, {artisansResidentClosure.SimpleModelCount} simple), consumed {artisansResidentClosure.FaceTableCount} unique normal/LP face tables to their exact byte ends, found {artisansResidentClosure.TexturedFaceCount} textured renderer commands, and found {artisansResidentClosure.UnresolvedFaceTableCount} unresolved / {artisansResidentClosure.AmbiguousFaceTableCount} ambiguous tables. The two simple roots use fixed 8-byte untextured faces and consume no texture-page bytes.");
    text.AppendLine("- **Primary-source grammar:** `moby.h` and `PatchMobyModelPointers` prove the normal/LP and simple pointer layouts. `r_moby.s` proves per-command 8/20-byte triangles, 12/24-byte quads, the 20-byte special textured command, descriptor placement in the final three words, exact table termination, and the untextured simple renderer.");
    text.AppendLine("- **SpyroEdit cross-check:** `/tmp/spyroedit-source` uses a simplified table-wide 8/20-byte heuristic selected from the second word and first animation. That heuristic cannot represent the retail renderer's mixed command stream, so it is documented but not used as ownership authority.");
    text.AppendLine($"- **Ground -> existing Artisans water:** storage-safe = `{groundToWater.Safe}`. It reuses a resident native texture and needs `{groundToWater.RequiredTotalByteCount}` new bytes. This does not prove cross-level capacity.");
    text.AppendLine($"- **Dragon texture 54 <- Gnasty's World texture 22 new-space relocation:** plan-ready = `{dragonMetal.Safe}`. A conservative independent complete-record allocation asks for `{donorRequirement.CompleteRecordPixelByteCount:N0}` pixel bytes plus `{donorRequirement.CompleteRecordClutByteCount:N0}` CLUT bytes (`{donorRequirement.CompleteRecordIndependentByteCount:N0}` total), but a coarse byte count is deliberately not treated as an encodable relocation plan.");
    text.AppendLine($"- **Texture 54 in-place target extent:** `{texture54Isolation.TargetExclusiveByteCount:N0}` / `{texture54Isolation.TargetOwnedByteCount:N0}` physical bytes are exclusive under complete consumer closure; decoded overlap is `{texture54Isolation.AnyDecodedConsumerOverlapByteCount}`. This is the correct proof input for a no-descriptor-change, exact logical in-place transplant.");
    text.AppendLine($"- Donor texture 22 HQ sides: {FormatHistogram(donorRequirement.HqSideHistogram)}. Gnasty's World page SHA: `{gnastysWorld.AddressableTexturePagesSha256}`. Artisans page SHA: `{artisans.AddressableTexturePagesSha256}`.");
    text.AppendLine();
    text.AppendLine("## All-level summary");
    text.AppendLine();
    text.AppendLine("| Level | WAD | Terrain records | Close sides | Particles | Resident models / textured faces / unresolved / ambiguous tables | Known bytes | Unknown nonzero | Unknown zero | Private pixel+CLUT |");
    text.AppendLine("|---|---:|---:|---|---:|---:|---:|---:|---:|---|");
    foreach (NativeTexturePageOwnershipReport report in reports)
    {
        NativeTextureOwnershipScopeResult particles = report.Scopes.Single(scope => scope.Scope == "particle-tables");
        NativeTextureOwnershipScopeResult residents = report.Scopes.Single(scope => scope.Scope == "resident-actors-and-scenery");
        text.AppendLine($"| {Escape(report.LevelName)} | {report.WadEntry} | {report.Terrain.TextureCount} | {Escape(FormatHistogram(report.Terrain.CloseSideHistogram))} | {particles.DescriptorCount} | {residents.ModelCount} / {residents.TexturedFaceCount} / {residents.UnresolvedFaceTableCount} / {residents.AmbiguousFaceTableCount} | {report.KnownOwnedByteCount:N0} | {report.UnknownNonZeroByteCount:N0} | {report.UnknownUnprovenZeroByteCount:N0} | **{report.ProvablyPrivateByteCount:N0} candidate bytes** |");
    }
    text.AppendLine();
    text.AppendLine("## Explicit proof scopes and blockers");
    text.AppendLine();
    foreach (NativeTexturePageOwnershipReport report in reports)
    {
        text.AppendLine($"### {report.LevelName}");
        text.AppendLine();
        foreach (NativeTextureOwnershipScopeResult scope in report.Scopes)
        {
            text.AppendLine($"- `{scope.Scope}`: **{scope.ProofState}**; descriptors {scope.DescriptorCount}, owned bytes {scope.OwnedByteCount:N0}, mapped/partial/external {scope.MappedDescriptorCount}/{scope.PartiallyMappedDescriptorCount}/{scope.ExternalDescriptorCount}.");
            foreach (string blocker in scope.Blockers)
                text.AppendLine($"  - Blocker: {blocker}");
        }
        text.AppendLine($"- `UnknownNonZero`: {report.UnknownNonZeroByteCount:N0} bytes across {report.UnknownNonZeroRegionCount:N0} runs, range digest `{report.UnknownNonZeroRangesSha256}`.");
        text.AppendLine($"- `SourceZeroAfterClosure`: {report.UnknownUnprovenZeroByteCount:N0} bytes; eligible only for pair-specific allocator geometry/readback, never by byte count alone.");
        text.AppendLine();
    }
    return text.ToString();
}

static string FormatHistogram(IReadOnlyDictionary<int, int> histogram) =>
    histogram.Count == 0
        ? "none"
        : string.Join(", ", histogram.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}x{pair.Key}:{pair.Value}"));

static string Escape(string value) => value.Replace("|", "\\|", StringComparison.Ordinal);

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
