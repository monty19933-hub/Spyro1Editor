using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Workspace;

string workspacePath = args.FirstOrDefault(argument => !argument.StartsWith("--", StringComparison.Ordinal)) ??
    Environment.CurrentDirectory;
bool allLevels = args.Any(argument => argument.Equals("--all", StringComparison.OrdinalIgnoreCase));

EditorWorkspace workspace = EditorWorkspace.Find(workspacePath);
LevelCatalog catalog = LevelCatalog.Load(workspace.RootPath);
string sourceImage = DiscImageLocator.FindImage(workspace);
string wadAnalysis = WadAnalysisLocator.Find(workspace);
if (!File.Exists(sourceImage) || !File.Exists(wadAnalysis))
    throw new InvalidOperationException("The occlusion smoke requires the configured retail BIN and WAD analysis.");

IReadOnlyList<LevelDefinition> levels = allLevels
    ? catalog.Levels
    : [catalog.Levels.Single(level => level.Key == "artisans")];
string temporaryRoot = Path.Combine(Path.GetTempPath(), $"spyro-occlusion-smoke-{Guid.NewGuid():N}");
Directory.CreateDirectory(temporaryRoot);
try
{
    foreach (LevelDefinition level in levels)
    {
        string output = Path.Combine(temporaryRoot, $"{level.Key}-runtime-scene-editor-overlay.json");
        SourceSceneOverlayExporter.Export(sourceImage, wadAnalysis, level, output);
        GeometryCandidate geometry = GeometryOverlayLoader.LoadFirstCandidate(output);
        NativeTerrainOcclusionData occlusion = geometry.NativeTerrainOcclusion ??
            throw new InvalidOperationException($"{level.DisplayName} did not load native terrain occlusion data.");
        if (occlusion.CollisionTriangles.Count == 0)
            throw new InvalidOperationException($"{level.DisplayName} has no collision-triangle assignments.");
        if (occlusion.EnvironmentGroups.Any(group => group.Any(index => index < 0 || index >= geometry.SourceSectors.Count)))
            throw new InvalidOperationException($"{level.DisplayName} has an out-of-range environment sector reference.");

        int resolvedGroup = -1;
        if (level.Key == "artisans")
        {
            FlyInLandingData landing = FlyInLandingLocator.Locate(sourceImage, level);
            Moby entry = FlyInLandingEditorControl.Create(level, landing);
            if (!occlusion.TryResolveGroup(
                    entry.Position.X,
                    entry.Position.Y,
                    entry.Position.Z + 220,
                    out resolvedGroup,
                    out int triangleIndex,
                    out double floorZ) ||
                resolvedGroup < 0)
            {
                throw new InvalidOperationException("Artisans entry camera did not resolve to a retail occlusion group.");
            }

            HashSet<int> planeSectors = geometry.Polygons
                .Where(face => face.NativeTextureId == 27 && Math.Abs(face.AvgZ - 192) < 0.01)
                .Select(face => face.SectorIndex)
                .ToHashSet();
            IReadOnlySet<int> entrySectors = occlusion.VisibleSectorsForGroup(resolvedGroup) ??
                throw new InvalidOperationException("Artisans entry group has no sector list.");
            int activePlaneSectors = planeSectors.Count(entrySectors.Contains);
            Console.WriteLine(
                $"Artisans entry group {resolvedGroup}, triangle {triangleIndex}, floor Z {floorZ:0.##}: " +
                $"{entrySectors.Count}/{geometry.SourceSectors.Count} sectors; texture-27 Z192 plane {activePlaneSectors}/{planeSectors.Count} sectors active.");
        }

        Console.WriteLine(
            $"{level.Key}: {occlusion.EnvironmentGroups.Count} groups, " +
            $"{occlusion.CollisionTriangles.Count} collision assignments, resolved group {resolvedGroup}.");
    }
}
finally
{
    try
    {
        Directory.Delete(temporaryRoot, recursive: true);
    }
    catch (IOException)
    {
    }
}

Console.WriteLine($"Native terrain occlusion smoke passed for {levels.Count} level(s).");
