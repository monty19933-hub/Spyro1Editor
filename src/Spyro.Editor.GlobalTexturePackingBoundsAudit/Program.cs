using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using System.Text;
using System.Text.Json;

const int AddressableBytes = NativeTexturePageOwnershipScanner.AddressableTexturePageBytes;
string workspaceRoot = args.FirstOrDefault() ?? Directory.GetCurrentDirectory();
string sourceImagePath = Path.Combine(workspaceRoot, "Spyro the Dragon (USA).bin");
string[] requestedKeys = args.Skip(1).ToArray();
if (requestedKeys.Length == 0)
{
    requestedKeys = ["alpineridge", "highcaves", "treetops", "loftycastle", "jacques", "gnorccove"];
}

LevelCatalog catalog = LevelCatalog.Load(workspaceRoot);
LevelDefinition donorLevel = catalog.FindByKey("gnastysworld")
    ?? throw new InvalidOperationException("Gnasty's World is missing from the level catalog.");
NativeTexturePageTargetStorageIsolationReport donorIsolation =
    NativeTexturePageOwnershipScanner.InspectTargetStorageIsolation(sourceImagePath, donorLevel, 17);
bool[] donorMask = BuildMask(donorIsolation.TargetOwnedRanges);
int donorUniqueBytes = donorMask.Count(value => value);

Console.WriteLine($"Donor Gnasty's World T17 exact unique footprint: {donorUniqueBytes:N0} bytes");
Console.WriteLine("level\tprotected\treleased\ttargetUnique\ttargetProtectedAlias\tdonorUnique\tanchoredLowerBound\trelocateAllLowerBound\theadroomAnchored\theadroomRelocateAll");
List<PackingBoundsRow> rows = [];

foreach (string key in requestedKeys)
{
    LevelDefinition level = catalog.FindByKey(key)
        ?? throw new InvalidOperationException($"Unknown level key '{key}'.");
    NativeTerrainTextureRuntimeControlAudit runtime =
        NativeTerrainTextureRuntimeControlScanner.Inspect(sourceImagePath, level);
    if (!runtime.Complete)
        throw new InvalidOperationException($"{level.DisplayName}: runtime-control scan is incomplete.");
    int[] movableIds = Enumerable.Range(0, runtime.TextureCount)
        .Except(runtime.ControlledTextureIds)
        .ToArray();
    if (!NativeTexturePageOwnershipScanner.TryBuildRelocationOwnershipProof(
            sourceImagePath,
            level,
            movableIds,
            out NativeTexturePageRelocationOwnershipProofResult? proof,
            out string failure) || proof == null)
    {
        throw new InvalidOperationException($"{level.DisplayName}: {failure}");
    }

    bool[] protectedMask = BuildMask(proof.Proof.OwnedRanges);
    bool[] targetMask = new bool[AddressableBytes];
    foreach (NativeTexturePageTargetStorageIsolationReport isolation in proof.TargetIsolations)
        OrRanges(targetMask, isolation.TargetOwnedRanges);

    int protectedBytes = protectedMask.Count(value => value);
    int targetUniqueBytes = targetMask.Count(value => value);
    int targetProtectedAliasBytes = Enumerable.Range(0, AddressableBytes)
        .Count(index => targetMask[index] && protectedMask[index]);
    int targetReleasedBytes = Enumerable.Range(0, AddressableBytes)
        .Count(index => targetMask[index] && !protectedMask[index]);
    if (targetReleasedBytes != proof.ReleasedTargetByteCount)
    {
        throw new InvalidOperationException(
            $"{level.DisplayName}: target-exclusive union {targetReleasedBytes:N0} does not equal proof release {proof.ReleasedTargetByteCount:N0}.");
    }

    int anchoredLowerBound = checked(protectedBytes + targetReleasedBytes + donorUniqueBytes);
    int relocateAllLowerBound = checked(protectedBytes + targetUniqueBytes + donorUniqueBytes);
    PackingBoundsRow row = new(
        level.DisplayName,
        protectedBytes,
        targetReleasedBytes,
        targetUniqueBytes,
        targetProtectedAliasBytes,
        donorUniqueBytes,
        anchoredLowerBound,
        relocateAllLowerBound,
        AddressableBytes - anchoredLowerBound,
        AddressableBytes - relocateAllLowerBound);
    rows.Add(row);
    Console.WriteLine(string.Join('\t',
        row.Level,
        row.ProtectedBytes,
        row.ReleasedTargetBytes,
        row.TargetUniqueBytes,
        row.TargetProtectedAliasBytes,
        row.DonorUniqueBytes,
        row.AnchoredLowerBoundBytes,
        row.RelocateAllLowerBoundBytes,
        row.AnchoredHeadroomBytes,
        row.RelocateAllHeadroomBytes));
}

string outputRoot = Path.Combine(
    workspaceRoot,
    "_local",
    "research",
    "global-texture-packing-bounds-audit");
Directory.CreateDirectory(outputRoot);
string jsonPath = Path.Combine(outputRoot, "global-texture-packing-bounds-audit.json");
string markdownPath = Path.Combine(outputRoot, "global-texture-packing-bounds-audit.md");
await File.WriteAllTextAsync(
    jsonPath,
    JsonSerializer.Serialize(new
    {
        addressableBytes = AddressableBytes,
        donor = new { level = donorLevel.DisplayName, textureId = 17, uniqueBytes = donorUniqueBytes },
        rows
    }, new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
await File.WriteAllTextAsync(markdownPath, BuildMarkdown(rows, donorUniqueBytes));
Console.WriteLine($"Markdown: {markdownPath}");
Console.WriteLine($"JSON: {jsonPath}");

static bool[] BuildMask(IEnumerable<NativeTexturePageOwnedRange> ranges)
{
    bool[] mask = new bool[AddressableBytes];
    OrRanges(mask, ranges);
    return mask;
}

static void OrRanges(bool[] mask, IEnumerable<NativeTexturePageOwnedRange> ranges)
{
    foreach (NativeTexturePageOwnedRange range in ranges)
    {
        int start = checked((int)range.Offset);
        int end = checked(start + range.Length);
        if (start < 0 || end > mask.Length)
            throw new InvalidOperationException($"Ownership range 0x{start:X}+0x{range.Length:X} is outside the addressable page.");
        Array.Fill(mask, true, start, range.Length);
    }
}

static string BuildMarkdown(IReadOnlyList<PackingBoundsRow> rows, int donorUniqueBytes)
{
    StringBuilder text = new();
    text.AppendLine("# Global terrain-texture packing lower-bound audit");
    text.AppendLine();
    text.AppendLine($"- Addressable page: **{AddressableBytes:N0} bytes** (`0x{AddressableBytes:X}`).");
    text.AppendLine($"- Synthetic donor: **Gnasty's World T17**, exact unique footprint **{donorUniqueBytes:N0} bytes**.");
    text.AppendLine("- The anchored lower bound counts protected storage once and leaves target components that alias it at their native locations.");
    text.AppendLine("- The relocate-all lower bound duplicates target bytes shared with protected consumers, as the original global packer did.");
    text.AppendLine();
    text.AppendLine("| Level | Protected | Released target | Exact target union | Target/protected alias | Anchored lower bound | Anchored headroom | Relocate-all lower bound | Relocate-all headroom |");
    text.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|");
    foreach (PackingBoundsRow row in rows)
    {
        text.AppendLine($"| {row.Level} | {row.ProtectedBytes:N0} | {row.ReleasedTargetBytes:N0} | {row.TargetUniqueBytes:N0} | {row.TargetProtectedAliasBytes:N0} | {row.AnchoredLowerBoundBytes:N0} | {row.AnchoredHeadroomBytes:N0} | {row.RelocateAllLowerBoundBytes:N0} | {row.RelocateAllHeadroomBytes:N0} |");
    }
    text.AppendLine();
    text.AppendLine("## Interpretation");
    text.AppendLine();
    text.AppendLine("- High Caves, Tree Tops, and Lofty Castle cannot fit under a relocate-everything model: their relocate-all bounds exceed `0x80000`. Their target components that touch protected consumers must remain at native offsets and count only once.");
    text.AppendLine("- Jacques and Gnorc Cove fit by byte count even without anchoring. A failure there is a geometry/fragmentation failure, not a physical-capacity failure.");
    text.AppendLine("- Alpine Ridge has substantial physical headroom. Its failure is an alias-component/CLUT-encoding geometry issue, not capacity.");
    text.AppendLine("- A positive lower-bound headroom proves only byte capacity. Descriptor encoding, fixed obstacles, and component geometry still require an exact placement proof.");
    text.AppendLine();
    text.AppendLine("## Proof-preserving compactor shape");
    text.AppendLine();
    text.AppendLine("1. Union every referenced source byte across pixel formats, palettes, and cross-role overlaps, keyed by source asset plus source offset. One connected component receives one linear translation; this counts each physical byte once and preserves every alias.");
    text.AppendLine("2. Keep the external-protected mask immutable. Any native-target component intersecting it is forced to its retail origin. Track the full anchored mask separately and reserve `external protected OR anchored` before allocation.");
    text.AppendLine("3. Start every other target component at its known-valid retail placement. Insert donor components with deterministic selective eviction instead of discarding the valid native layout and greedily rebuilding all storage.");
    text.AppendLine("4. Enumerate descriptor-valid placements for each component. Choose the next displaced/incoming component by minimum remaining legal placements, then larger byte count, then stable component key. Order candidate placements by fewest target conflicts, fewest bytes evicted, displacement, then Y/X.");
    text.AppendLine("5. Use iterative deepening on moved target components/bytes. For each candidate donor footprint, evict its exact conflicting movable components and recursively place that set. Expand the conflict closure until a solution is found or the complete finite placement search is exhausted.");
    text.AppendLine("6. Forward-check total free bytes and require at least one legal placement for every unplaced component. Memoize the canonical placement/occupancy state. Candidate and tie ordering must be stable so the same source produces the same plan.");
    text.AppendLine("7. Clear only bytes outside both retained masks, copy non-anchored components, preserve native coordinate bytes for unchanged anchored descriptors, and independently verify indexed pixels, palettes, material bits, protected bytes, anchored bytes, and a bidirectional source-byte-to-target-byte alias map.");
    return text.ToString();
}

internal sealed record PackingBoundsRow(
    string Level,
    int ProtectedBytes,
    int ReleasedTargetBytes,
    int TargetUniqueBytes,
    int TargetProtectedAliasBytes,
    int DonorUniqueBytes,
    int AnchoredLowerBoundBytes,
    int RelocateAllLowerBoundBytes,
    int AnchoredHeadroomBytes,
    int RelocateAllHeadroomBytes);
