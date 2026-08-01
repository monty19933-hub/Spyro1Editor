using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;

const int WadLba = 37;
const int OversizedSourceDestinationPairs = 188;
const int ExpectedDarkHollowT0PhysicalBytes = 22_528;
const int ExpectedGnorcCoveConservativeFreeBytes = 15_315;
const int ExpectedUnknownNonZeroDeficitBytes = 7_213;

string workspaceRoot = ResolveWorkspaceRoot(args.FirstOrDefault());
string sourceImagePath = Path.Combine(workspaceRoot, "Spyro the Dragon (USA).bin");
string outputRoot = Path.Combine(workspaceRoot, "_local", "research", "dead-unreferenced-texture-storage");
string jsonPath = Path.Combine(outputRoot, "gnorc-cove-dark-hollow-t0-candidate-range-plan.json");
string markdownPath = Path.Combine(outputRoot, "gnorc-cove-dark-hollow-t0-candidate-range-plan.md");
Directory.CreateDirectory(outputRoot);

Assert(File.Exists(sourceImagePath), $"Missing retail source BIN: {sourceImagePath}");
HashSet<string> imageOutputsBefore = EnumerateImageOutputs(outputRoot);
long sourceLengthBefore;
string sourceShaBefore;
using (FileStream stream = File.OpenRead(sourceImagePath))
{
    sourceLengthBefore = stream.Length;
    sourceShaBefore = Convert.ToHexString(SHA256.HashData(stream));
}
DateTime sourceWriteBefore = File.GetLastWriteTimeUtc(sourceImagePath);

LevelCatalog catalog = LevelCatalog.Load(workspaceRoot);
LevelDefinition gnorcCove = catalog.FindByKey("gnorccove")
    ?? throw new InvalidDataException("Gnorc Cove is missing from the level catalog.");
LevelDefinition darkHollow = catalog.FindByKey("darkhollow")
    ?? throw new InvalidDataException("Dark Hollow is missing from the level catalog.");

NativeTexturePageOwnershipReport gnorcOwnership =
    NativeTexturePageOwnershipScanner.Scan(sourceImagePath, gnorcCove);
NativeTexturePageOwnershipReport donorOwnership =
    NativeTexturePageOwnershipScanner.Scan(sourceImagePath, darkHollow);
NativeTexturePageTargetStorageIsolationReport donorIsolation =
    NativeTexturePageOwnershipScanner.InspectTargetStorageIsolation(sourceImagePath, darkHollow, 0);

Assert(gnorcOwnership.AllConsumerClosureComplete,
    "Gnorc Cove did not close the current decoded-consumer ownership audit.");
Assert(gnorcOwnership.ProvablyPrivateByteCount == ExpectedGnorcCoveConservativeFreeBytes,
    $"Gnorc Cove conservative zero-byte space changed from {ExpectedGnorcCoveConservativeFreeBytes:N0} bytes.");
Assert(donorIsolation.TargetOwnedByteCount == ExpectedDarkHollowT0PhysicalBytes,
    $"Dark Hollow T0 physical union changed from {ExpectedDarkHollowT0PhysicalBytes:N0} bytes.");
Assert(donorIsolation.AllConsumerClosureComplete,
    "Dark Hollow T0 isolation lost decoded-consumer closure.");
int deficitBytes = checked(donorIsolation.TargetOwnedByteCount - gnorcOwnership.ProvablyPrivateByteCount);
Assert(deficitBytes == ExpectedUnknownNonZeroDeficitBytes,
    $"The focused deficit changed from {ExpectedUnknownNonZeroDeficitBytes:N0} to {deficitBytes:N0} bytes.");

NativeTextureDeadStorageCandidatePlan candidatePlan =
    NativeTextureDeadUnreferencedStorageResearch.BuildReadOnlyCandidatePlan(
        sourceImagePath,
        gnorcCove,
        deficitBytes);
Assert(candidatePlan.SelectedUnknownNonZeroByteCount >= deficitBytes,
    "The read-only plan did not select enough UnknownNonZero bytes for the focused deficit.");
Assert(candidatePlan.SelectedRanges.Count > 0 &&
       candidatePlan.SelectedRangeCount == candidatePlan.SelectedRanges.Count,
    "The selected candidate range count is empty or inconsistent.");
Assert(!candidatePlan.WritesRetailOrCandidateImage && !candidatePlan.NormalExporterEligible,
    "The research plan unexpectedly exposed a retail/candidate image writer or normal export eligibility.");
Assert(typeof(NativeTexturePageOwnershipScanner).Assembly.GetExportedTypes().All(type =>
        !type.Name.Contains("DeadUnreferenced", StringComparison.Ordinal) &&
        !type.Name.Contains("DeadStorage", StringComparison.Ordinal)),
    "A dead-unreferenced research type escaped Core as a public API.");
string researchTypeName = nameof(NativeTextureDeadUnreferencedStorageResearch);
Assert(Directory.EnumerateFiles(Path.Combine(workspaceRoot, "src", "Spyro.Editor.App"), "*.cs", SearchOption.AllDirectories)
        .All(path => !File.ReadAllText(path).Contains(researchTypeName, StringComparison.Ordinal)),
    "The normal App source references the dead-unreferenced research API.");
Assert(Directory.EnumerateFiles(Path.Combine(workspaceRoot, "src", "Spyro.Editor.Core", "Exporting"), "*.cs", SearchOption.AllDirectories)
        .All(path => !File.ReadAllText(path).Contains(researchTypeName, StringComparison.Ordinal)),
    "A normal Core exporter references the dead-unreferenced research API.");
Assert(candidatePlan.MachineDecodedDescriptorRootsClosed &&
       candidatePlan.HumanAuditedOtherRuntimeClosureOnly &&
       !candidatePlan.MachineProvenEveryRuntimeReadClosed,
    "The report no longer distinguishes decoded-root proof from the human/source-audited other-runtime boundary.");
foreach (NativeTextureDeadStorageCandidateRange selected in candidatePlan.SelectedRanges)
{
    Assert(gnorcOwnership.UnknownNonZeroRanges.Any(range =>
            range.Offset == selected.TexturePageOffset && range.Length == selected.Length),
        $"Selected range 0x{selected.TexturePageOffset:X}+0x{selected.Length:X} is not one exact Gnorc Cove UnknownNonZero run.");
    Assert(selected.PreimageSha256.Length == 64 && selected.DeterministicPoisonSha256.Length == 64,
        "A selected candidate range is missing an exact preimage or poison hash.");
}

NativeTextureDeadStoragePromotionAttestation incompleteAttestation = new(
    candidatePlan.RetailDiscSha256,
    candidatePlan.AddressableTexturePagesSha256,
    candidatePlan.UnknownNonZeroRangeLayoutSha256,
    candidatePlan.SelectedRangeLayoutSha256,
    candidatePlan.SelectedPreimageSha256,
    Enum.GetValues<NativeTextureDeadStoragePromotionCondition>()
        .Select(condition => new NativeTextureDeadStorageConditionEvidence(
            condition,
            Complete: false,
            Summary: "Not yet machine-proven; this focused smoke emits only the adversarial test plan.",
            Artifacts: Array.Empty<NativeTextureDeadStorageEvidenceArtifact>()))
        .ToArray());
NativeTextureDeadStorageAuthorizationResult denied =
    NativeTextureDeadUnreferencedStorageResearch.TryAuthorizeCandidateDeadStorage(
        candidatePlan,
        incompleteAttestation);
Assert(!denied.Authorized && denied.Proof == null,
    "The research gate authorized UnknownNonZero storage without all six promotion proofs.");
foreach (NativeTextureDeadStoragePromotionCondition condition in
         Enum.GetValues<NativeTextureDeadStoragePromotionCondition>())
{
    Assert(denied.Blockers.Any(blocker => blocker.Contains(condition.ToString(), StringComparison.Ordinal)),
        $"The denied authorization did not name missing condition {condition}.");
}

NativeTextureDeadStoragePromotionAttestation stalePreimage = incompleteAttestation with
{
    ExpectedSelectedPreimageSha256 = new string('0', 64)
};
NativeTextureDeadStorageAuthorizationResult staleDenied =
    NativeTextureDeadUnreferencedStorageResearch.TryAuthorizeCandidateDeadStorage(
        candidatePlan,
        stalePreimage);
Assert(!staleDenied.Authorized &&
       staleDenied.Blockers.Any(blocker => blocker.Contains("preimage", StringComparison.OrdinalIgnoreCase)),
    "A stale selected-range preimage was not explicitly rejected.");
NativeTextureDeadStoragePromotionAttestation duplicateCondition = incompleteAttestation with
{
    Conditions = incompleteAttestation.Conditions
        .Append(incompleteAttestation.Conditions[0])
        .ToArray()
};
NativeTextureDeadStorageAuthorizationResult duplicateDenied =
    NativeTextureDeadUnreferencedStorageResearch.TryAuthorizeCandidateDeadStorage(
        candidatePlan,
        duplicateCondition);
Assert(!duplicateDenied.Authorized &&
       duplicateDenied.Blockers.Any(blocker => blocker.Contains("exactly once", StringComparison.OrdinalIgnoreCase)),
    "Duplicate promotion evidence was not rejected without emitting a proof.");

DiscLayout layout = DiscImage.DetectLayout(sourceImagePath);
byte[] donorPages;
using (FileStream stream = File.OpenRead(sourceImagePath))
{
    donorPages = DiscImage.ReadFileBytes(
        stream,
        layout,
        WadLba,
        donorOwnership.TexturePagesWadOffset,
        donorOwnership.AddressableTexturePageByteLength);
}
DonorRangeRow[] donorRanges = donorIsolation.TargetOwnedRanges
    .OrderBy(range => range.Offset)
    .Select(range =>
    {
        Assert(range.Offset >= 0 && range.Offset + range.Length <= donorPages.Length,
            "A Dark Hollow T0 source range exceeds its addressable texture pages.");
        return new DonorRangeRow(
            checked((int)range.Offset),
            checked(donorOwnership.TexturePagesWadOffset + range.Offset),
            range.Length,
            Convert.ToHexString(SHA256.HashData(donorPages.AsSpan(checked((int)range.Offset), range.Length))));
    })
    .ToArray();
Assert(donorRanges.Sum(range => range.Length) == ExpectedDarkHollowT0PhysicalBytes,
    "Dark Hollow T0 exact source ranges do not sum to its physical union.");
string donorRangeLayoutSha256 = HashRangeLayout(donorRanges.Select(range => (range.TexturePageOffset, range.Length)));
string donorPreimageSha256 = HashRanges(
    donorPages,
    donorRanges.Select(range => (range.TexturePageOffset, range.Length)));

string allDonorAuditPath = Path.Combine(
    workspaceRoot,
    "_local",
    "research",
    "all-donor-texture-feasibility",
    "all-donor-texture-feasibility-audit.json");
if (File.Exists(allDonorAuditPath))
{
    using JsonDocument audit = JsonDocument.Parse(await File.ReadAllTextAsync(allDonorAuditPath));
    Assert(audit.RootElement.GetProperty("ByteCapacityImpossiblePairCount").GetInt64() == OversizedSourceDestinationPairs,
        $"The prerequisite all-donor report no longer contains {OversizedSourceDestinationPairs} oversized pairs.");
    Assert(string.Equals(
            audit.RootElement.GetProperty("Source").GetProperty("Sha256").GetString(),
            sourceShaBefore,
            StringComparison.OrdinalIgnoreCase),
        "The prerequisite all-donor report is bound to a different retail disc.");
}

string[] duckStationChecklist =
[
    "Cold boot from the generated poison CUE; do not use a save state created from another image.",
    "Enter Gnorc Cove through its normal portal and repeat with the fastest available entry route.",
    "Inspect every nearby terrain surface at normal, close, and low-detail distances; look specifically for delayed palette or tpage corruption.",
    "Exercise Spyro, HUD, particles, resident enemies/effects, damage, death, and a full level reload.",
    "Pause/resume repeatedly and capture screenshots so framebuffer save/restore paths execute.",
    "Collect gems and change any reachable key/life/global HUD state; verify icons, totals, sounds, and persistence.",
    "Leave and re-enter through the portal; also exercise a balloon transition from the connected homeworld route where available.",
    "Repeat the same checklist with the Dark Hollow T0 transplant candidate and compare against retail and poison runs.",
    "Capture GPU command/VRAM sampling traces when possible; every sampled source/CLUT/copy-source byte must resolve to KnownOwned or a separately proven runtime scratch range.",
    "Do not promote on visual success: require static producer closure, range provenance/lifetime proof, exact final-BIN readback, trace evidence, and repeated fresh-boot DuckStation evidence."
];

object report = new
{
    GeneratedAtUtc = DateTimeOffset.UtcNow,
    Scope = new
    {
        OversizedSourceDestinationPairs,
        Destination = "Gnorc Cove",
        Donor = "Dark Hollow T0",
        Purpose = "Read-only next-step plan for the conservative allocator's largest-donor deficit; not a candidate image."
    },
    Source = new
    {
        Path = sourceImagePath,
        ByteLength = sourceLengthBefore,
        Sha256 = sourceShaBefore,
        LastWriteTimeUtc = sourceWriteBefore
    },
    Capacity = new
    {
        DarkHollowT0PhysicalBytes = donorIsolation.TargetOwnedByteCount,
        GnorcCoveConservativeFreeBytes = gnorcOwnership.ProvablyPrivateByteCount,
        ExactUnknownNonZeroDeficitBytes = deficitBytes,
        GnorcCoveUnknownNonZeroBytes = gnorcOwnership.UnknownNonZeroByteCount,
        SelectedUnknownNonZeroBytes = candidatePlan.SelectedUnknownNonZeroByteCount
    },
    CurrentProofBoundary = new
    {
        MachineProven = new[]
        {
            "Exact retail-disc, addressable texture-page, UnknownNonZero layout, selected layout, and selected preimage hashes.",
            "Decoded terrain/resident/particle/player/HUD/scene descriptor roots and the exact selected nonzero ranges under that decoded ownership mask.",
            "The source image is unchanged and no BIN/CUE writer exists in this research API."
        },
        HumanOrSourceAuditedOnly = new[]
        {
            "The ownership scanner's other-runtime scope is a source-audit assertion, not a machine-checked whole-program control/data-flow proof."
        },
        NotYetProven = new[]
        {
            "Every executable/overlay GPU coordinate producer and indirect LoadImage/StoreImage/MoveImage source.",
            "Every reachable computed source value and every UnknownNonZero range's archive provenance/lifetime.",
            "Animation, scrolling, capture/restore, portal transitions, and scratch upload non-use.",
            "Exact candidate readback, VRAM trace, and repeated adversarial DuckStation runtime evidence."
        }
    },
    CandidatePlan = candidatePlan,
    DarkHollowT0Source = new
    {
        TexturePagesSha256 = donorOwnership.AddressableTexturePagesSha256,
        PhysicalRangeCount = donorRanges.Length,
        PhysicalByteCount = donorRanges.Sum(range => range.Length),
        RangeLayoutSha256 = donorRangeLayoutSha256,
        PhysicalPreimageSha256 = donorPreimageSha256,
        Ranges = donorRanges
    },
    PromotionConditions = Enum.GetValues<NativeTextureDeadStoragePromotionCondition>(),
    Authorization = new
    {
        Authorized = denied.Authorized,
        ProofEmitted = denied.Proof != null,
        Blockers = denied.Blockers
    },
    PoisonCandidateTestPlan = new
    {
        Generated = false,
        candidatePlan.PoisonAlgorithm,
        candidatePlan.SelectedDeterministicPoisonSha256,
        Ranges = candidatePlan.SelectedRanges,
        RequiredReadback = "KnownOwned must remain byte-identical; every selected range must match its deterministic poison hash; every other byte must retain its preimage."
    },
    TransplantCandidateTestPlan = new
    {
        Generated = false,
        Donor = "Dark Hollow T0",
        DonorBytes = donorIsolation.TargetOwnedByteCount,
        ConservativeZeroBytes = gnorcOwnership.ProvablyPrivateByteCount,
        MinimumUnknownNonZeroBytes = deficitBytes,
        DonorRangeLayoutSha256 = donorRangeLayoutSha256,
        DonorPreimageSha256 = donorPreimageSha256,
        RequiredReadback = "Preserve all donor aliases, target KnownOwned bytes, descriptor preimages, and non-selected UnknownNonZero bytes exactly."
    },
    DuckStationChecklist = duckStationChecklist,
    Outputs = new
    {
        RetailOrCandidateBinWritten = false,
        CueWritten = false,
        NormalCreateBinEligible = false
    }
};

JsonSerializerOptions jsonOptions = new()
{
    WriteIndented = true,
    Converters = { new JsonStringEnumConverter() }
};
await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(report, jsonOptions) + Environment.NewLine);
await File.WriteAllTextAsync(
    markdownPath,
    BuildMarkdown(
        candidatePlan,
        donorRanges,
        donorOwnership,
        donorRangeLayoutSha256,
        donorPreimageSha256,
        gnorcOwnership,
        deficitBytes,
        denied.Blockers,
        duckStationChecklist));

HashSet<string> imageOutputsAfter = EnumerateImageOutputs(outputRoot);
Assert(imageOutputsBefore.SetEquals(imageOutputsAfter),
    "The focused read-only smoke created or removed a BIN/CUE/ISO/CHD image artifact.");
long sourceLengthAfter;
string sourceShaAfter;
using (FileStream stream = File.OpenRead(sourceImagePath))
{
    sourceLengthAfter = stream.Length;
    sourceShaAfter = Convert.ToHexString(SHA256.HashData(stream));
}
Assert(sourceLengthAfter == sourceLengthBefore &&
       string.Equals(sourceShaAfter, sourceShaBefore, StringComparison.OrdinalIgnoreCase) &&
       File.GetLastWriteTimeUtc(sourceImagePath) == sourceWriteBefore,
    "The retail source image changed during the read-only focused smoke.");

Console.WriteLine(
    $"Dead-unreferenced storage research smoke passed: Gnorc Cove needs {deficitBytes:N0} UnknownNonZero bytes for Dark Hollow T0; " +
    $"selected {candidatePlan.SelectedUnknownNonZeroByteCount:N0} bytes across {candidatePlan.SelectedRangeCount} exact native runs.");
Console.WriteLine("No BIN/CUE was written; all six promotion conditions remain blocked.");
Console.WriteLine($"JSON: {jsonPath}");
Console.WriteLine($"Markdown: {markdownPath}");

static string BuildMarkdown(
    NativeTextureDeadStorageCandidatePlan plan,
    IReadOnlyList<DonorRangeRow> donorRanges,
    NativeTexturePageOwnershipReport donorOwnership,
    string donorRangeLayoutSha256,
    string donorPreimageSha256,
    NativeTexturePageOwnershipReport targetOwnership,
    int deficitBytes,
    IReadOnlyList<string> blockers,
    IReadOnlyList<string> duckStationChecklist)
{
    StringBuilder text = new();
    text.AppendLine("# Gnorc Cove dead-unreferenced storage candidate-range plan");
    text.AppendLine();
    text.AppendLine("> Research only. This report writes no retail/candidate BIN or CUE and cannot be called by the normal App/Create BIN path.");
    text.AppendLine();
    text.AppendLine($"- Prerequisite matrix: **{OversizedSourceDestinationPairs}** conservative oversized source/destination pairs.");
    text.AppendLine($"- Donor: **Dark Hollow T0**, `{ExpectedDarkHollowT0PhysicalBytes:N0}` unique physical bytes.");
    text.AppendLine($"- Gnorc Cove conservative private zero union: `{targetOwnership.ProvablyPrivateByteCount:N0}` bytes.");
    text.AppendLine($"- Exact deficit: **{deficitBytes:N0} UnknownNonZero bytes**.");
    text.AppendLine($"- Selected exact native runs: **{plan.SelectedRangeCount}**, totaling **{plan.SelectedUnknownNonZeroByteCount:N0} bytes**.");
    text.AppendLine($"- Retail disc SHA-256: `{plan.RetailDiscSha256}`.");
    text.AppendLine($"- Gnorc Cove pages SHA-256: `{plan.AddressableTexturePagesSha256}`.");
    text.AppendLine($"- Full UnknownNonZero layout SHA-256: `{plan.UnknownNonZeroRangeLayoutSha256}` ({plan.UnknownNonZeroRegionCount:N0} runs / {plan.UnknownNonZeroByteCount:N0} bytes).");
    text.AppendLine($"- Selected layout SHA-256: `{plan.SelectedRangeLayoutSha256}`.");
    text.AppendLine($"- Selected preimage SHA-256: `{plan.SelectedPreimageSha256}`.");
    text.AppendLine($"- Deterministic poison SHA-256: `{plan.SelectedDeterministicPoisonSha256}`.");
    text.AppendLine();
    text.AppendLine("## Proof boundary");
    text.AppendLine();
    text.AppendLine("The decoded terrain/resident/particle/player/HUD/scene roots and exact range hashes are machine checked. The `other-runtime-consumers` closure still includes a human/source-audited assertion; it is **not** a machine-proven whole-program guarantee that every runtime texture or copy-source read is enumerated. UnknownNonZero therefore remains protected, not free.");
    text.AppendLine();
    text.AppendLine("### Six required promotion conditions");
    text.AppendLine();
    foreach (NativeTextureDeadStoragePromotionCondition condition in Enum.GetValues<NativeTextureDeadStoragePromotionCondition>())
        text.AppendLine($"- [ ] `{condition}`");
    text.AppendLine();
    text.AppendLine("Authorization is denied. Current blockers:");
    text.AppendLine();
    foreach (string blocker in blockers)
        text.AppendLine($"- {blocker}");
    text.AppendLine();
    text.AppendLine("## Exact Gnorc Cove UnknownNonZero ranges selected");
    text.AppendLine();
    text.AppendLine("| Texture-page range | WAD range | Bytes | Preimage SHA-256 | Poison SHA-256 |");
    text.AppendLine("|---|---|---:|---|---|");
    foreach (NativeTextureDeadStorageCandidateRange range in plan.SelectedRanges)
    {
        text.AppendLine(
            $"| `0x{range.TexturePageOffset:X6}..0x{range.TexturePageOffset + range.Length:X6}` | " +
            $"`0x{range.TexturePagesWadOffset:X}..0x{range.TexturePagesWadOffset + range.Length:X}` | " +
            $"{range.Length:N0} | `{range.PreimageSha256}` | `{range.DeterministicPoisonSha256}` |");
    }
    text.AppendLine();
    text.AppendLine($"Poison algorithm: `{plan.PoisonAlgorithm}`");
    text.AppendLine();
    text.AppendLine("## Exact Dark Hollow T0 physical source ranges");
    text.AppendLine();
    text.AppendLine($"- Addressable pages SHA-256: `{donorOwnership.AddressableTexturePagesSha256}`.");
    text.AppendLine($"- Range layout SHA-256: `{donorRangeLayoutSha256}`.");
    text.AppendLine($"- Concatenated physical preimage SHA-256: `{donorPreimageSha256}`.");
    text.AppendLine();
    text.AppendLine("| Texture-page range | WAD range | Bytes | Preimage SHA-256 |");
    text.AppendLine("|---|---|---:|---|");
    foreach (DonorRangeRow range in donorRanges)
    {
        text.AppendLine(
            $"| `0x{range.TexturePageOffset:X6}..0x{range.TexturePageOffset + range.Length:X6}` | " +
            $"`0x{range.TexturePagesWadOffset:X}..0x{range.TexturePagesWadOffset + range.Length:X}` | " +
            $"{range.Length:N0} | `{range.PreimageSha256}` |");
    }
    text.AppendLine();
    text.AppendLine("## Poison candidate (not generated)");
    text.AppendLine();
    text.AppendLine("Overwrite only the selected Gnorc Cove ranges with the deterministic poison, preserve KnownOwned and every other byte exactly, then require final readback to match every listed hash. This deliberately tries to make any hidden texture/CLUT/copy-source consumer fail loudly.");
    text.AppendLine();
    text.AppendLine("## Dark Hollow T0 transplant candidate (not generated)");
    text.AppendLine();
    text.AppendLine($"Use the `{targetOwnership.ProvablyPrivateByteCount:N0}` conservative zero bytes plus at least `{deficitBytes:N0}` bytes from the selected UnknownNonZero runs. Preserve the donor's exact {donorRanges.Count} physical ranges and alias relationships, all target KnownOwned bytes, all non-selected unknown bytes, and descriptor preimages. No transplant is authorized until all six conditions pass.");
    text.AppendLine();
    text.AppendLine("## DuckStation checklist");
    text.AppendLine();
    foreach (string item in duckStationChecklist)
        text.AppendLine($"- [ ] {item}");
    return text.ToString();
}

static HashSet<string> EnumerateImageOutputs(string directory) =>
    Directory.Exists(directory)
        ? Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Where(path => new[] { ".bin", ".cue", ".iso", ".chd" }
                .Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .Select(Path.GetFullPath)
            .ToHashSet(StringComparer.Ordinal)
        : new HashSet<string>(StringComparer.Ordinal);

static string HashRangeLayout(IEnumerable<(int Offset, int Length)> ranges)
{
    (int Offset, int Length)[] sorted = ranges.OrderBy(range => range.Offset).ToArray();
    byte[] bytes = new byte[sorted.Length * 8];
    for (int index = 0; index < sorted.Length; index++)
    {
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(index * 8, 4), sorted[index].Offset);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(index * 8 + 4, 4), sorted[index].Length);
    }
    return Convert.ToHexString(SHA256.HashData(bytes));
}

static string HashRanges(byte[] source, IEnumerable<(int Offset, int Length)> ranges)
{
    using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    foreach ((int offset, int length) in ranges.OrderBy(range => range.Offset))
        hash.AppendData(source.AsSpan(offset, length));
    return Convert.ToHexString(hash.GetHashAndReset());
}

static string ResolveWorkspaceRoot(string? candidate)
{
    DirectoryInfo? current = new(Path.GetFullPath(candidate ?? Directory.GetCurrentDirectory()));
    while (current != null)
    {
        if (File.Exists(Path.Combine(current.FullName, "spyro-level-catalog.json")) &&
            Directory.Exists(Path.Combine(current.FullName, "src", "Spyro.Editor.Core")))
        {
            return current.FullName;
        }
        current = current.Parent;
    }
    throw new DirectoryNotFoundException("Could not locate the Spyro editor workspace root.");
}

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidDataException(message);
}

internal sealed record DonorRangeRow(
    int TexturePageOffset,
    long TexturePagesWadOffset,
    int Length,
    string PreimageSha256);
