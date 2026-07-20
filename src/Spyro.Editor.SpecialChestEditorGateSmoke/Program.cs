using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Levels;

LevelDefinition artisans = Level("artisans", "Artisans");
Expect(
    SpecialChestEditorTemplateGate.TryResolve(artisans, "lockedChest", out CrossLevelTemplateLevelStatus artisansLocked),
    "Locked Chest must be recognized as a special-chest family.");
Expect(artisansLocked.Ready && artisansLocked.Placeable && artisansLocked.TrueAddPlaceable, "The runtime-proven Artisans pair must remain visible to release Add.");
CrossLevelTemplateLevelStatus artisansResolved = CrossLevelEditorTemplateSupport.ResolveLevelStatus(
    artisans,
    "peacekeepers",
    "lockedChest",
    "supported-target-runtime-bundle");
Expect(artisansResolved.Ready, "The normal cross-level resolver must expose the proven Artisans pair.");

Expect(
    SpecialChestEditorTemplateGate.TryResolve(artisans, "key", out CrossLevelTemplateLevelStatus artisansKey),
    "Key must share the Locked Chest atomic evidence gate.");
Expect(artisansKey.Ready, "The Artisans key half must remain available with the proven pair.");

LevelDefinition townSquare = Level("townsquare", "Town Square");
SpecialChestEditorTemplateGate.TryResolve(townSquare, "lockedChest", out CrossLevelTemplateLevelStatus townSquareLocked);
Expect(!townSquareLocked.Ready && townSquareLocked.Placeable, "Town Square's checked static candidate must stay disposable-test-only.");
CrossLevelTemplateLevelStatus townSquareResolved = CrossLevelEditorTemplateSupport.ResolveLevelStatus(
    townSquare,
    "peacekeepers",
    "lockedChest",
    "supported-target-runtime-bundle");
Expect(!townSquareResolved.Ready && townSquareResolved.Placeable, "The normal resolver must keep Town Square plan-only instead of inheriting Artisans readiness.");

LevelDefinition toasty = Level("toasty", "Toasty");
SpecialChestEditorTemplateGate.TryResolve(toasty, "lifeChest", out CrossLevelTemplateLevelStatus toastyLife);
Expect(!toastyLife.Ready && !toastyLife.Placeable, "A missing Life Chest dependency closure must not appear in Add.");

LevelDefinition sunnyFlight = Level("sunnyflight", "Sunny Flight");
foreach (string family in new[] { "key", "lockedChest", "lifeChest", "springChest", "fireworkChest", "multiGemChest", "armoredChest" })
{
    Expect(
        SpecialChestEditorTemplateGate.TryResolve(sunnyFlight, family, out CrossLevelTemplateLevelStatus flightStatus),
        $"{family} must be recognized in the flight exclusion check.");
    Expect(!flightStatus.Ready && !flightStatus.Placeable && !flightStatus.TrueAddPlaceable, $"{family} must remain blocked in flights.");
}

Expect(
    !SpecialChestEditorTemplateGate.TryResolve(artisans, "enemyTransform", out _),
    "Unrelated object families must continue through their existing support resolver.");

Console.WriteLine("Special-chest editor gate smoke passed.");
Console.WriteLine("- runtime-proven Artisans pair remains release-visible");
Console.WriteLine("- candidates stay test-only, incomplete closures stay hidden, and flights stay blocked");
return 0;

static LevelDefinition Level(string key, string displayName) => new()
{
    Key = key,
    DisplayName = displayName,
    SourceRecordCount = 100,
    SourceTableWadOffset = "0x100"
};

static void Expect(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
