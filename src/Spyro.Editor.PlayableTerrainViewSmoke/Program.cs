using System.Text.Json;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;

string workspace = FindWorkspace(args.FirstOrDefault());
LevelCatalog catalog = LevelCatalog.Load(workspace);
Assert(catalog.Levels.Count == 35, $"Expected 35 retail levels, found {catalog.Levels.Count}.");

List<object> levels = [];
Dictionary<string, PlayableTerrainViewPlan> plans = new(StringComparer.OrdinalIgnoreCase);
int storedFaces = 0;
int candidateSheets = 0;
int suppressedSheets = 0;
int suppressedFaces = 0;
foreach (LevelDefinition level in catalog.Levels)
{
    string path = Path.Combine(workspace, "editor-cache", $"{level.Key}-runtime-scene-editor-overlay.json");
    Assert(File.Exists(path), $"Missing source terrain overlay for {level.DisplayName}: {path}");
    GeometryCandidate geometry = GeometryOverlayLoader.LoadFirstCandidate(path);
    TerrainMaterialClassifier.Apply(level.Key, workspace, geometry);
    int sourceFaceCount = geometry.Polygons.Count;
    PlayableTerrainViewPlan plan = PlayableTerrainViewClassifier.GetOrBuild(level.Key, geometry);
    Assert(sourceFaceCount == geometry.Polygons.Count, $"{level.DisplayName} classifier mutated source face storage.");
    Assert(plan.StoredFaceCount == sourceFaceCount, $"{level.DisplayName} stored-face count drifted.");
    Assert(plan.VisibleFaceCount + plan.SuppressedFaceCount == sourceFaceCount, $"{level.DisplayName} plan counts do not balance.");
    Assert(plan.CandidateGroups.Select(group => group.Signature).Distinct(StringComparer.Ordinal).Count() == plan.CandidateGroups.Count,
        $"{level.DisplayName} emitted duplicate broad-sheet signatures.");
    Assert(plan.SuppressedGroups.All(group => group.Disposition == PlayableTerrainViewDisposition.SuppressedByDefault),
        $"{level.DisplayName} suppressed group has the wrong disposition.");
    Assert(ReferenceEquals(plan, PlayableTerrainViewClassifier.GetOrBuild(level.Key, geometry)),
        $"{level.DisplayName} cached plan was not stable for its loaded geometry instance.");

    plans.Add(level.Key, plan);
    storedFaces += sourceFaceCount;
    candidateSheets += plan.CandidateGroups.Count;
    suppressedSheets += plan.SuppressedSheetCount;
    suppressedFaces += plan.SuppressedFaceCount;
    levels.Add(new
    {
        levelKey = level.Key,
        levelName = level.DisplayName,
        storedFaceCount = plan.StoredFaceCount,
        visibleFaceCount = plan.VisibleFaceCount,
        suppressedFaceCount = plan.SuppressedFaceCount,
        candidateSheetCount = plan.CandidateGroups.Count,
        suppressedSheetCount = plan.SuppressedSheetCount,
        failedOpen = plan.FailedOpen,
        summary = plan.Summary,
        groups = plan.CandidateGroups.Select(group => new
        {
            group.Signature,
            disposition = group.Disposition.ToString(),
            group.TextureId,
            group.PlaneZ,
            group.Surface,
            group.FaceCount,
            group.GeometryFaceShare,
            group.ProjectedFaceArea,
            group.ProjectedFaceAreaShare,
            touchedSceneBoundsSides = group.TouchedSceneBoundsSides,
            group.NativeDepthTwoOrFartherFaceCount,
            group.NativeDepthTwoOrFartherFaceShare,
            group.Reason
        })
    });
}

Assert(storedFaces == 190_640, $"All-level source face total drifted: {storedFaces:N0}.");
Assert(candidateSheets == 10, $"Expected 10 conservative broad-sheet candidates, found {candidateSheets}.");
Assert(suppressedSheets == 8, $"Expected 8 default-suppressed sheets, found {suppressedSheets}.");
Assert(suppressedFaces == 9_008, $"Expected 9,008 default-suppressed faces, found {suppressedFaces:N0}.");

Dictionary<string, int> expectedSuppressedProductionSheets = new(StringComparer.Ordinal)
{
    ["artisans:texture-27:z-192"] = 780,
    ["toasty:texture-52:z-378"] = 364,
    ["sunnyflight:texture-46:z-129"] = 1_841,
    ["mistybog:texture-38:z-605"] = 1_488,
    ["mistybog:texture-38:z-157"] = 664,
    ["metalhead:texture-67:z-128"] = 439,
    ["gnastysworld:texture-0:z-387"] = 1_954,
    ["gnorccove:texture-10:z-1088"] = 1_478
};
Dictionary<string, int> actualSuppressedProductionSheets = plans.Values
    .SelectMany(plan => plan.SuppressedGroups)
    .ToDictionary(group => group.Signature, group => group.FaceCount, StringComparer.Ordinal);
Assert(actualSuppressedProductionSheets.Count == expectedSuppressedProductionSheets.Count,
    $"Expected {expectedSuppressedProductionSheets.Count} exact production suppression selectors, found {actualSuppressedProductionSheets.Count}: " +
    string.Join(", ", actualSuppressedProductionSheets.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}={pair.Value:N0}")));
foreach ((string signature, int expectedFaceCount) in expectedSuppressedProductionSheets)
{
    Assert(actualSuppressedProductionSheets.TryGetValue(signature, out int actualFaceCount),
        $"Missing audited production suppression selector {signature} ({expectedFaceCount:N0} faces).");
    Assert(actualFaceCount == expectedFaceCount,
        $"Audited production suppression selector {signature} drifted from {expectedFaceCount:N0} to {actualFaceCount:N0} faces.");
}

PlayableTerrainViewPlan artisans = plans["artisans"];
PlayableTerrainViewGroup artisansSheet = artisans.SuppressedGroups.Single();
Assert(artisansSheet.Signature == "artisans:texture-27:z-192" &&
       artisansSheet.FaceCount == 780 &&
       artisans.SuppressedFaceCount == 780,
    $"Artisans cyan plate signature drifted: {artisansSheet}.");

PlayableTerrainViewPlan metalHead = plans["metalhead"];
PlayableTerrainViewGroup metalHeadSheet = metalHead.SuppressedGroups.Single();
Assert(metalHeadSheet.Signature == "metalhead:texture-67:z-128" &&
       metalHeadSheet.FaceCount == 439 &&
       metalHead.SuppressedFaceCount == 439,
    $"Metal Head outer sheet signature drifted: {metalHeadSheet}.");

PlayableTerrainViewPlan stoneHill = plans["stonehill"];
PlayableTerrainViewGroup stoneHillOcean = stoneHill.RetainedGroups.Single();
Assert(stoneHill.SuppressedFaceCount == 0 &&
       stoneHillOcean.Signature == "stonehill:texture-32:z-1024" &&
       stoneHillOcean.FaceCount == 327 &&
       stoneHillOcean.Disposition == PlayableTerrainViewDisposition.RetainedLevelException,
    $"Stone Hill ocean was not retained by its explicit source-proven exception: {stoneHillOcean}.");

PlayableTerrainViewPlan beastMakers = plans["beastmakers"];
PlayableTerrainViewGroup beastMakersSwamp = beastMakers.RetainedGroups.Single();
Assert(beastMakers.SuppressedFaceCount == 0 &&
       beastMakersSwamp.Signature == "beastmakers:texture-41:z-951" &&
       beastMakersSwamp.FaceCount == 604 &&
       beastMakersSwamp.Disposition == PlayableTerrainViewDisposition.RetainedLevelException,
    $"Beast Makers swamp/ooze context was not retained by its paired-capture exception: {beastMakersSwamp}.");

Assert(plans["darkpassage"].CandidateGroups.Count == 0 && plans["darkpassage"].SuppressedFaceCount == 0,
    "Dark Passage legitimate separated terrain was classified as a background sheet.");
Assert(plans["dreamweavers"].CandidateGroups.Count == 0 && plans["dreamweavers"].SuppressedFaceCount == 0,
    "Dream Weavers legitimate separated terrain was classified as a background sheet.");

GeometryCandidate unauditedFixture = GeometryOverlayLoader.LoadFirstCandidate(
    Path.Combine(workspace, "editor-cache", "artisans-runtime-scene-editor-overlay.json"));
PlayableTerrainViewPlan unauditedPlan = PlayableTerrainViewClassifier.Build("artisans-modified", unauditedFixture);
Assert(unauditedPlan.CandidateGroups.Count == 1 &&
       unauditedPlan.SuppressedFaceCount == 0 &&
       unauditedPlan.RetainedGroups.Single().Disposition == PlayableTerrainViewDisposition.RetainedUnreviewedCandidate,
    "A broad sheet from an unreviewed level/revision did not fail open.");

string artisansPath = Path.Combine(workspace, "editor-cache", "artisans-runtime-scene-editor-overlay.json");
GeometryCandidate editFixture = GeometryOverlayLoader.LoadFirstCandidate(artisansPath);
TerrainMaterialClassifier.Apply("artisans", workspace, editFixture);
PlayableTerrainViewPlan editPlan = PlayableTerrainViewClassifier.Build("artisans", editFixture);
TerrainPolygon hiddenFace = editFixture.Polygons.First(face => !editPlan.ShouldShow(face));
PlayableTerrainViewFaceClassification hiddenClassification = editPlan.Classify(hiddenFace);
Assert(hiddenClassification.Role == PlayableTerrainViewRole.BroadCoplanarUnderlay &&
       !hiddenClassification.VisibleByDefault,
    "Artisans background face did not report its hidden default classification.");

GeometryCandidate foreignFixture = GeometryOverlayLoader.LoadFirstCandidate(artisansPath);
TerrainPolygon foreignTwin = foreignFixture.Polygons.Single(face => face.RuntimeKey == hiddenFace.RuntimeKey);
Assert(editPlan.ShouldShow(foreignTwin) &&
       editPlan.Classify(foreignTwin).Role == PlayableTerrainViewRole.ForegroundOrContext,
    "A different geometry instance was matched by RuntimeKey instead of reference identity.");

Assert(TerrainSnapper.TryFindZAt(
        new[] { hiddenFace },
        hiddenFace.Center.X,
        hiddenFace.Center.Y,
        hiddenFace.AvgZ,
        out _,
        preferredTerrainIndex: 0),
    "Terrain snap fixture did not hit its broad sheet.");
Assert(!TerrainSnapper.TryFindZAt(
        new[] { hiddenFace },
        hiddenFace.Center.X,
        hiddenFace.Center.Y,
        hiddenFace.AvgZ,
        out _,
        preferredTerrainIndex: 0,
        includePolygon: _ => false),
    "Terrain snapping ignored the Playable View inclusion predicate for a preferred source index.");
Assert(!TerrainSnapper.TryFindAdjacentZAt(
        new[] { hiddenFace },
        hiddenFace.Center.X,
        hiddenFace.Center.Y,
        hiddenFace.AvgZ - 128,
        direction: 1,
        out _,
        includePolygon: _ => false),
    "Adjacent-layer snapping ignored the Playable View inclusion predicate.");

hiddenFace.ApplyTextureOverride(hiddenFace.TextureId + 1);
Assert(hiddenFace.IsTerrainEdited && editPlan.ShouldShow(hiddenFace) && editPlan.Classify(hiddenFace).VisibleByDefault,
    "A staged edit inside a hidden sheet was not forced visible for recovery/editing.");

string outputDirectory = Path.Combine(workspace, "_local", "smoke", "playable-terrain-view");
Directory.CreateDirectory(outputDirectory);
string reportPath = Path.Combine(outputDirectory, "playable-terrain-view-audit.json");
File.WriteAllText(reportPath, JsonSerializer.Serialize(new
{
    generatedAt = DateTime.Now.ToString("s"),
    contract = "texture-scoped-exact-coplanar-boundary-sheet-v1",
    levelCount = catalog.Levels.Count,
    storedFaces,
    candidateSheets,
    suppressedSheets,
    suppressedFaces,
    visibleFaces = storedFaces - suppressedFaces,
    thresholds = new
    {
        PlayableTerrainViewClassifier.MinimumGroupFaceCount,
        PlayableTerrainViewClassifier.MinimumProjectedFaceAreaShare,
        PlayableTerrainViewClassifier.MinimumTouchedSceneBoundsSides,
        PlayableTerrainViewClassifier.CoplanarEpsilon
    },
    levels
}, new JsonSerializerOptions { WriteIndented = true }));

Console.WriteLine(
    $"Playable terrain view smoke passed: {catalog.Levels.Count} levels, {candidateSheets} candidate sheets, " +
    $"{suppressedSheets} suppressed sheets, {suppressedFaces:N0}/{storedFaces:N0} faces hidden by default.");
Console.WriteLine($"Report: {reportPath}");

static string FindWorkspace(string? requested)
{
    if (!string.IsNullOrWhiteSpace(requested))
    {
        string explicitPath = Path.GetFullPath(requested);
        if (File.Exists(Path.Combine(explicitPath, "spyro-level-catalog.json")))
            return explicitPath;
    }

    DirectoryInfo? directory = new(Environment.CurrentDirectory);
    while (directory != null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "spyro-level-catalog.json")))
            return directory.FullName;
        directory = directory.Parent;
    }
    throw new DirectoryNotFoundException("Could not locate the Spyro editor workspace.");
}

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
