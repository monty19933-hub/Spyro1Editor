using Spyro.Editor.Core.Exporting;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Levels;

const string SourceImageHash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
byte[] originalPayload = Enumerable.Range(0, 128).Select(value => (byte)value).ToArray();
byte[] originalTarget = new byte[128];
MobyActorPackageCopyPreview original = new(
    SourceStart: "0x100", TargetStart: "0x200", SourceWadRelativeOffset: "0x10100",
    TargetWadRelativeOffset: "0x20200", SourceImageOffset: "0x30100",
    TargetImageOffset: "0x40200", ByteLength: originalPayload.Length, TargetBeforeIsZero: true,
    TargetSafety: "zero", BeforeHexPreview: new string('0', 64),
    AfterHexPreview: Convert.ToHexString(originalPayload.AsSpan(0, 32)),
    BeforeSha256: Hash(originalTarget), AfterSha256: Hash(originalPayload));
string baseline = Fingerprint(original);
byte[] changedPayload = originalPayload.ToArray();
changedPayload[^1] ^= 0x80;
Assert(baseline != Fingerprint(original with { AfterSha256 = Hash(changedPayload) }),
    "Changed imported model bytes beyond the 32-byte preview retained prior runtime evidence.");
byte[] changedTarget = originalTarget.ToArray();
changedTarget[^1] = 1;
Assert(baseline != Fingerprint(original with { BeforeSha256 = Hash(changedTarget) }),
    "Changed target preimage beyond the preview retained prior runtime evidence.");
byte[] rebasedPayload = originalPayload.ToArray();
BitConverter.GetBytes(0x00123456).CopyTo(rebasedPayload, 48);
Assert(baseline != Fingerprint(original with { AfterSha256 = Hash(rebasedPayload) }),
    "Changed internal pointer rebase retained prior runtime evidence.");
Assert(baseline == Fingerprint(original with { AfterHexPreview = "display-only preview" }),
    "Display-only preview formatting changed the exact payload identity.");
Assert(baseline != Fingerprint(original with { SourceWadRelativeOffset = "0x50100" }),
    "Changed donor source location retained prior runtime evidence.");
Assert(baseline != Fingerprint(original with { TargetWadRelativeOffset = "0x60200" }),
    "Changed destination location retained prior runtime evidence.");
Assert(baseline != CrossLevelCandidateRecipeFingerprint.Create(
        "test.recipe", "experimental-plan-only", [original], [], "other-donor", "target", SourceImageHash),
    "Changed donor level retained prior runtime evidence.");
Assert(baseline != CrossLevelCandidateRecipeFingerprint.Create(
        "test.recipe", "experimental-plan-only", [original], [], "donor", "other-target", SourceImageHash),
    "Changed destination level retained prior runtime evidence.");
Reject(() => Fingerprint(original with { BeforeSha256 = "" }));
Reject(() => Fingerprint(original with { AfterSha256 = "" }));
Reject(() => Fingerprint(original with { AfterSha256 = new string('z', 64) }));
MobyActorPackageCopyPreview roundTrip = JsonSerializer.Deserialize<MobyActorPackageCopyPreview>(
    JsonSerializer.Serialize(original))!;
Assert(Fingerprint(roundTrip) == baseline, "Plan JSON lost exact payload identities.");
MobyActorPackageCopyPreview second = original with { TargetStart = "0x300" };
Assert(CrossLevelCandidateRecipeFingerprint.Create("test.recipe", "experimental-plan-only", [original, second], [], sourceImageSha256: SourceImageHash) ==
    CrossLevelCandidateRecipeFingerprint.Create("test.recipe", "experimental-plan-only", [second, original], [], sourceImageSha256: SourceImageHash),
    "Copy enumeration order changed the recipe fingerprint.");
MobyActorPackageRootPreview root = new(
    "replace", "0x50", 0, "0x200", "0x0100", "0x100", "0x0001",
    "0x20050", "0x20150", "0x40050", "0x40150", true, true, "test root");
string rootBaseline = CrossLevelCandidateRecipeFingerprint.Create("test.recipe", "experimental-plan-only", [original], [root], sourceImageSha256: SourceImageHash);
Assert(rootBaseline != CrossLevelCandidateRecipeFingerprint.Create("test.recipe", "experimental-plan-only", [original],
    [root with { ExistingRoot = "0x101" }], sourceImageSha256: SourceImageHash), "Changed target root preimage retained prior evidence.");
Assert(rootBaseline != CrossLevelCandidateRecipeFingerprint.Create("test.recipe", "experimental-plan-only", [original],
    [root with { ExistingActorId = "0x0002" }], sourceImageSha256: SourceImageHash), "Changed target actor identity retained prior evidence.");
Assert(baseline != CrossLevelCandidateRecipeFingerprint.Create("test.recipe", "experimental-plan-only", [original], [],
    "donor", "target", new string('b', 64)), "Changed executable or behavior overlay outside the package retained prior runtime evidence.");
Reject(() => CrossLevelCandidateRecipeFingerprint.Create("test.recipe", "experimental-plan-only", [original], []));
Reject(() => CrossLevelCandidateRecipeFingerprint.Create("test.recipe", "experimental-plan-only", [original, original], [], sourceImageSha256: SourceImageHash));
Reject(() => CrossLevelCandidateRecipeFingerprint.Create("test.recipe", "experimental-plan-only", [original, original with { TargetStart = "0x0200" }], [], sourceImageSha256: SourceImageHash));
Reject(() => CrossLevelCandidateRecipeFingerprint.Create("test.recipe", "experimental-plan-only", [original], [root, root], sourceImageSha256: SourceImageHash));
// Captures the old algorithm for this fixture, without adding a legacy approval fallback.
string legacy = Hash(Encoding.UTF8.GetBytes(
    "v1|test.recipe|experimental-plan-only|copy:0x100>0x200+128:zero"));
Assert(legacy != baseline, "Offset-only v1 evidence was grandfathered into v2.");
CheckStaleCatalogEvidence(original);
Console.WriteLine("CROSS_LEVEL_CANDIDATE_FINGERPRINT_SMOKE_OK");

static string Fingerprint(MobyActorPackageCopyPreview copy) =>
    CrossLevelCandidateRecipeFingerprint.Create("test.recipe", "experimental-plan-only", [copy], [], "donor", "target", SourceImageHash);
static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
static void Reject(Action action)
{
    try { action(); }
    catch (InvalidDataException) { return; }
    throw new InvalidOperationException("Missing or malformed exact payload hash was accepted.");
}

static void CheckStaleCatalogEvidence(MobyActorPackageCopyPreview copy)
{
    string directory = Path.Combine(Path.GetTempPath(), "spyro-fingerprint-smoke-" + Guid.NewGuid().ToString("N"));
    string objects = Path.Combine(directory, "_local", "objects");
    Directory.CreateDirectory(objects);
    try
    {
        // The recipe catalog accepts underscore-normalized family names, so this
        // exercises the generic decision without a dedicated chest profile.
        CrossLevelActorPackageRecipe recipe = CrossLevelActorPackageRecipeCatalog.All.First(item =>
            item.TargetLevelKey == "stonehill" && item.SourceLevelKey == "peacekeepers" &&
            item.Family == "springChest" && item.Status == "experimental-plan-only");
        string evidencePath = Path.Combine(objects, "old.candidate-result.json");
        File.WriteAllText(evidencePath, JsonSerializer.Serialize(new
        {
            candidate = new { levelKey = "stonehill", recipes = new[] { new { recipeId = recipe.Id, recipeFingerprint = new string('a', 64) } } },
            result = new { status = "passed", bootedLevel = true, objectAppeared = true, behaviorCorrect = true, noNearbyRegression = true }
        }));
        LevelDefinition target = new() { Key = "stonehill", DisplayName = "Stone Hill", LevelId = 11 };
        CrossLevelTemplateLevelStatus status = CrossLevelEditorTemplateSupport.ResolveLevelStatus(
            target, "peacekeepers", "spring_chest", "experimental-plan-only", directory);
        Assert(!status.Ready, "Recipe-id-only historical evidence marked a catalog item Ready without current source validation.");
        string currentFingerprint = CrossLevelCandidateRecipeFingerprint.Create(
            recipe.Id, recipe.Status, [copy], [], "peacekeepers", "stonehill", SourceImageHash);
        File.WriteAllText(evidencePath, JsonSerializer.Serialize(new
        {
            candidate = new { levelKey = "stonehill", recipes = new[] { new { recipeId = recipe.Id, recipeFingerprint = currentFingerprint } } },
            result = new { status = "passed", bootedLevel = true, objectAppeared = true, behaviorCorrect = true, noNearbyRegression = true }
        }));
        Assert(CrossLevelCandidateEvidenceStore.FindPassedEvidence(directory, "stonehill", recipe.Id, currentFingerprint).Passed,
            "Exact current v2 evidence was disabled along with stale evidence.");
        string changedSourceFingerprint = CrossLevelCandidateRecipeFingerprint.Create(
            recipe.Id, recipe.Status, [copy], [], "peacekeepers", "stonehill", new string('b', 64));
        Assert(!CrossLevelCandidateEvidenceStore.FindPassedEvidence(directory, "stonehill", recipe.Id, changedSourceFingerprint).Passed,
            "A current pass promoted changed source-image bytes.");
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}
