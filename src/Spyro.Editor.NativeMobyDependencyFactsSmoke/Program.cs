using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Scene;
using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Nodes;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Workspace;
using Spyro.Editor.Core.Editing;

if (args.Length == 2 && args[0] == "--audit-current-cache")
{
    AuditCurrentCache(args[1]);
    return;
}

Moby dependent = Enemy(0x8000);
Require(!CrossLevelSwapCatalogBuilder.TryGetEligibleTargetCategory(dependent, out _, out string reason),
    "A native +0x00 properties pointer was advertised as self-contained because legacy +0x08 was zero.");
Require(reason.Contains("properties", StringComparison.OrdinalIgnoreCase), "Missing native properties dependency reason.");
foreach (string label in new[] { "Enemy", "Ordinary Enemy", "Renamed actor", "Chest", "Grass" })
{
    dependent.Label = label;
    Require(!NativeMobyDependencyFacts.FromMoby(dependent).CanUseSelfContainedRowCandidate,
        "Display rename erased native dependency requirements.");
}
Require(CrossLevelSwapCatalogBuilder.TryGetEligibleTargetCategory(Enemy(0), out _, out _),
    "A known-zero properties pointer must not block an otherwise eligible self-contained candidate.");
foreach (uint pointer in new uint[] { 4, 0x8138, 0x80008138, uint.MaxValue })
    Require(NativeMobyDependencyFacts.FromMoby(Enemy(pointer)).RequiresPropertiesClosure,
        "Nonzero or sentinel properties must require explicit closure instead of being treated as absent.");
Require(!NativeMobyDependencyFacts.FromMoby(Enemy(0, 4)).CanUseSelfContainedRowCandidate,
    "Legacy associated data silently lost its existing guard.");
byte[] row = new byte[0x58];
row[0x36] = 0x49;
row[0x37] = 1;
row[0x50] = 0x20;
NativeMobyDependencyFacts facts = NativeMobyDependencyFacts.FromRecord(row);
Require(facts == NativeMobyDependencyFacts.FromMoby(Enemy(0)), "Record and editor facts disagree.");
row[0x37] = 2;
Require(NativeMobyDependencyFacts.FromRecord(row).ActorId == 0x249 && facts.ActorId == 0x149,
    "Native classes sharing the low byte were conflated.");
Require(facts.TryValidateActorBinding(0x149, [new(0x149, 0x1000)], new HashSet<ushort>(), out _),
    "Exact donor class/root binding was rejected.");
Require(!facts.TryValidateActorBinding(0x149, [new(0x249, 0x1000)], new HashSet<ushort>(), out _),
    "High-byte-different actor root was accepted.");
Require(!facts.TryValidateActorBinding(0x149, [new(0x149, 0)], new HashSet<ushort>(), out _),
    "A zero root claimed an installed dependency.");
NativeMobyDependencyFacts alias = facts with { ActorId = 0x1FE };
Require(alias.TryValidateActorBinding(0x149, [new(0x1FE, 0x1000)], new HashSet<ushort> { 0x1FE }, out _),
    "An explicitly declared, installed actor alias was rejected.");
Require(!alias.TryValidateActorBinding(0x149, [new(0x1FE, 0x1000)], new HashSet<ushort>(), out _),
    "A class edit borrowed unrelated resident assets without declared remap evidence.");
Require(!alias.TryValidateActorBinding(0x149, [new(0x149, 0x1000)], new HashSet<ushort> { 0x1FE }, out _),
    "An alias declaration alone replaced proof of its installed root.");
BinaryPrimitives.WriteUInt32LittleEndian(row, 0x8138);
Require(NativeMobyDependencyFacts.FromRecord(row).RequiresPropertiesClosure, "Raw +0x00 properties were ignored.");
Require(!NativeMobyDependencyFacts.FromRecord(new byte[0x58], hasActiveLinks: true).CanUseSelfContainedRowCandidate,
    "Native companion link did not require closure.");
ExpectInvalid(() => NativeMobyDependencyFacts.FromRecord(new byte[0x57]));
ExpectInvalid(() => NativeMobyDependencyFacts.FromMoby(new Moby { SourceByte36 = 256 }));
Require(!CrossLevelSwapCatalogBuilder.TryGetEligibleTargetCategory(new Moby
    { Type = 0x20, SourceByte36 = 256, Label = "Enemy", CandidateKind = "enemy", Confidence = "observed-fingerprint" }, out _, out _),
    "A malformed cache identity escaped per-entry catalog rejection.");

string temporary = Path.Combine(Path.GetTempPath(), "spyro-native-moby-facts-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temporary);
try
{
    string cache = Path.Combine(temporary, "mobys.json");
    foreach ((object? value, bool present, bool known) in new (object?, bool, bool)[]
    {
        (null, false, false), (null, true, false), ("bad", true, false), (-1, true, false),
        (0x1_0000_0000L, true, false), (0, true, true), ("0x00000000", true, true), (uint.MaxValue, true, true)
    })
    {
        Dictionary<string, object?> item = new() { ["typeHex"] = "0x20", ["sourceByte36Hex"] = "0x49", ["sourceByte37Hex"] = "0x01" };
        if (present) item["propertiesPointer"] = value;
        File.WriteAllText(cache, JsonSerializer.Serialize(new { mobys = new[] { item } }));
        Moby loaded = MobyLoader.LoadCached(cache).Single();
        Require(loaded.HasPropertiesPointerEvidence == known, "Cache pointer missing/malformed state was confused with a known native zero.");
        if (!known) Require(NativeMobyDependencyFacts.FromMoby(loaded).RequiresPropertiesClosure, "Unknown cache pointer was declared absent.");
        loaded.IsAdded = true;
        string editsPath = Path.Combine(temporary, "edits.json");
        MobyEditStore.SaveAsync(editsPath, [loaded], "Synthetic").GetAwaiter().GetResult();
        List<Moby> restored = [];
        MobyEditStore.Load(editsPath, restored);
        Require(restored.Single().HasPropertiesPointerEvidence == known &&
                restored.Single().PropertiesPointer == loaded.PropertiesPointer,
            "Native-edit save/load converted unknown pointer evidence to a known zero or changed a known pointer.");
        using JsonDocument saved = JsonDocument.Parse(File.ReadAllText(editsPath));
        JsonElement savedPointer = saved.RootElement.GetProperty("edits")[0].GetProperty("propertiesPointer");
        Require(known ? savedPointer.GetString() == $"0x{loaded.PropertiesPointer:X8}" : savedPointer.ValueKind == JsonValueKind.Null,
            "Pointer persistence changed the established known-value encoding or forged evidence for an unknown value.");
    }
}
finally { Directory.Delete(temporary, recursive: true); }
Console.WriteLine("NATIVE_MOBY_DEPENDENCY_FACTS_SMOKE_OK");

static Moby Enemy(uint properties, uint associated = 0) => new()
{
    Type = 0x20, SourceByte36 = 0x49, SourceByte37 = 1,
    Label = "Synthetic Enemy", CandidateKind = "enemy", Confidence = "observed-fingerprint",
    PropertiesPointer = properties, SpecialDataPointer = associated
};

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static void ExpectInvalid(Action action)
{
    try { action(); } catch (InvalidDataException) { return; }
    throw new InvalidOperationException("Malformed native record identity was accepted.");
}

static void AuditCurrentCache(string root)
{
    EditorWorkspace workspace = EditorWorkspace.Find(root);
    LevelCatalog catalog = LevelCatalog.Load(workspace.RootPath);
    int total = 0, unknown = 0, oldEligible = 0, currentEligible = 0, oldEnemy = 0, currentEnemy = 0;
    foreach (LevelDefinition level in catalog.Levels.Where(level => level.HasSourceTable))
    {
        string path = Path.Combine(workspace.RootPath, "editor-cache", $"{level.Key}-mobys.json");
        if (!File.Exists(path)) continue;
        List<Moby> mobys = MobyLoader.LoadCached(path).ToList();
        MobyMetadataEnricher.Apply(workspace, level.Key, mobys);
        int oldCount = 0, currentCount = 0;
        foreach (Moby moby in mobys)
        {
            total++;
            if (!moby.HasPropertiesPointerEvidence) unknown++;
            // Reconstruct only the old classification assumption: +00 was
            // ignored, while +08 and all metadata/link guards remain identical.
            JsonObject legacyJson = JsonNode.Parse(JsonSerializer.Serialize(moby))!.AsObject();
            legacyJson[nameof(Moby.PropertiesPointer)] = 0;
            Moby legacyAssumption = legacyJson.Deserialize<Moby>()!;
            legacyAssumption.Links.Clear();
            legacyAssumption.Links.AddRange(moby.Links);
            if (CrossLevelSwapCatalogBuilder.TryGetEligibleTargetCategory(legacyAssumption, out CrossLevelSwapCategory oldCategory, out _))
            {
                oldCount++; oldEligible++;
                if (oldCategory == CrossLevelSwapCategory.Enemy) oldEnemy++;
            }
            if (CrossLevelSwapCatalogBuilder.TryGetEligibleTargetCategory(moby, out CrossLevelSwapCategory category, out _))
            {
                currentCount++; currentEligible++;
                if (category == CrossLevelSwapCategory.Enemy) currentEnemy++;
            }
        }
        Console.WriteLine($"{level.Key}: rows={mobys.Count}, old-row-only-eligible={oldCount}, current-row-only-eligible={currentCount}");
    }
    Console.WriteLine($"CURRENT_CACHE_AUDIT rows={total}; unknown-properties={unknown}; old-eligible={oldEligible}; current-eligible={currentEligible}; old-enemy-eligible={oldEnemy}; current-enemy-eligible={currentEnemy}");
    foreach (string target in new[] { "artisans", "stonehill", "toasty", "wizardpeak", "magiccrafters" })
    {
        CrossLevelSwapCatalog swaps = CrossLevelSwapCatalogBuilder.Build(workspace, catalog, target);
        Console.WriteLine($"CATALOG {target}: ready={swaps.ReadyCount}; candidate={swaps.CandidateCount}; " +
            $"dedicated-wizard-ready={swaps.Entries.Count(entry => entry.CompatibilityTier == CrossLevelSwapCatalogBuilder.GreenWizardVerifiedCompatibility && entry.NormalCreateBinReady)}");
    }
}
