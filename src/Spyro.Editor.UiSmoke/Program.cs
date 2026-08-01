using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Styling;
using Avalonia.Threading;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Spyro.Editor.App;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Cache;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Persistence;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Updates;
using Spyro.Editor.Core.Workspace;

string sourceWorkspace = FindWorkspace(args.FirstOrDefault(argument =>
    !argument.StartsWith("--", StringComparison.Ordinal)));
bool sourceFreeViewportSmoke =
    args.Contains("--terrain-occlusion-only", StringComparer.OrdinalIgnoreCase) ||
    args.Contains("--private-texture-feasibility-cache-only", StringComparer.OrdinalIgnoreCase);
string workspace = CreateIsolatedWorkspace(sourceWorkspace, includeSourceDiscSetting: !sourceFreeViewportSmoke);
string previousDirectory = Environment.CurrentDirectory;
string? previousWorkspace = Environment.GetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE");
string? previousReleaseMode = Environment.GetEnvironmentVariable("SPYRO_EDITOR_RELEASE");
string outputDirectory = Path.Combine(sourceWorkspace, "_local", "ui-overhaul");
Directory.CreateDirectory(outputDirectory);

try
{
    Environment.CurrentDirectory = workspace;
    Environment.SetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE", workspace);
    Environment.SetEnvironmentVariable("SPYRO_EDITOR_RELEASE", "1");
    string resolvedSmokeWorkspace = EditorWorkspace.Find().RootPath;
    if (!string.Equals(resolvedSmokeWorkspace, workspace, StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            $"The UI smoke workspace locator resolved '{resolvedSmokeWorkspace}' instead of isolated workspace '{workspace}'. " +
            $"Environment='{Environment.GetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE")}', catalog={File.Exists(Path.Combine(workspace, "spyro-level-catalog.json"))}, " +
            $"cache={Directory.Exists(Path.Combine(workspace, "editor-cache"))}.");
    }

    HeadlessEntryPoint.BuildAvaloniaApp().SetupWithoutStarting();
    if (Application.Current?.RequestedThemeVariant != ThemeVariant.Light)
        throw new InvalidOperationException("The release shell must use the light theme so its white surfaces keep readable controls.");
    if (args.Contains("--moby-atlas-only", StringComparer.OrdinalIgnoreCase))
    {
        RunMobyAtlasOnly();
        return 0;
    }
    if (args.Contains("--terrain-atomic-only", StringComparer.OrdinalIgnoreCase))
    {
        RunTerrainAtomicOnly();
        return 0;
    }
    if (args.Contains("--terrain-native-roundtrip-only", StringComparer.OrdinalIgnoreCase))
    {
        RunTerrainNativeRoundTripOnly();
        return 0;
    }
    if (args.Contains("--terrain-unreferenced-art-only", StringComparer.OrdinalIgnoreCase))
    {
        RunTerrainUnreferencedArtOnly();
        return 0;
    }
    if (args.Contains("--terrain-same-level-roundtrip-only", StringComparer.OrdinalIgnoreCase))
    {
        RunTerrainSameLevelRoundTripOnly();
        return 0;
    }
    if (args.Contains("--terrain-texture-paint-mode-only", StringComparer.OrdinalIgnoreCase))
    {
        RunTerrainTexturePaintModeOnly();
        return 0;
    }
    if (args.Contains("--terrain-cross-level-paint-only", StringComparer.OrdinalIgnoreCase))
    {
        RunTerrainCrossLevelPaintOnly();
        return 0;
    }
    if (args.Contains("--terrain-shared-route-matrix-only", StringComparer.OrdinalIgnoreCase))
    {
        string result = MainWindow.AssertAllLevelSharedTerrainTextureRouteForTesting(
            LevelCatalog.Load(workspace));
        Console.WriteLine($"Shared terrain replacement route matrix: {result}");
        return 0;
    }
    if (args.Contains("--appended-private-texture-gate-only", StringComparer.OrdinalIgnoreCase))
    {
        RunAppendedPrivateTextureGateOnly();
        return 0;
    }
    if (args.Contains("--private-texture-feasibility-cache-only", StringComparer.OrdinalIgnoreCase))
    {
        string result = Task
            .Run(MainWindow.AssertPrivateTextureFeasibilityMemoizerForTestingAsync)
            .GetAwaiter()
            .GetResult();
        Console.WriteLine($"Private-texture feasibility cache: {result}.");
        return 0;
    }
    if (args.Contains("--appended-private-build-safety-only", StringComparer.OrdinalIgnoreCase))
    {
        RunAppendedPrivateBuildSafetyOnly();
        return 0;
    }
    if (args.Contains("--terrain-texture-paint-gallery-only", StringComparer.OrdinalIgnoreCase))
    {
        RunTerrainTexturePaintGalleryOnly();
        return 0;
    }
    if (args.Contains("--object-gallery-only", StringComparer.OrdinalIgnoreCase))
    {
        RunObjectGalleryOnly();
        return 0;
    }
    if (args.Contains("--create-bin-feedback-only", StringComparer.OrdinalIgnoreCase))
    {
        RunCreateBinFeedbackOnly();
        return 0;
    }
    if (args.Contains("--update-only", StringComparer.OrdinalIgnoreCase))
    {
        RunUpdateOnly();
        return 0;
    }
    if (args.Contains("--viewport-fit-only", StringComparer.OrdinalIgnoreCase))
    {
        RunViewportFitOnly();
        return 0;
    }
    if (args.Contains("--terrain-depth-cue-only", StringComparer.OrdinalIgnoreCase))
    {
        RunTerrainDepthCueOnly();
        return 0;
    }
    if (args.Contains("--terrain-texture-lod-only", StringComparer.OrdinalIgnoreCase))
    {
        RunTerrainTextureLodOnly();
        return 0;
    }
    if (args.Contains("--terrain-bounded-compositor-only", StringComparer.OrdinalIgnoreCase))
    {
        RunTerrainBoundedCompositorOnly();
        return 0;
    }
    if (args.Contains("--terrain-native-gte-projection-only", StringComparer.OrdinalIgnoreCase))
    {
        RunTerrainNativeGteProjectionOnly();
        return 0;
    }
    if (args.Contains("--terrain-native-map-materials-only", StringComparer.OrdinalIgnoreCase))
    {
        RunTerrainNativeMapMaterialsOnly();
        return 0;
    }
    if (args.Contains("--terrain-lp-topology-only", StringComparer.OrdinalIgnoreCase))
    {
        AssertNativeLowDetailTopology();
        Console.WriteLine("Native LP topology smoke passed: raw quad coverage is exact and repeated-slot triangles emit once.");
        return 0;
    }
    if (args.Contains("--terrain-scene-view-only", StringComparer.OrdinalIgnoreCase))
    {
        RunShippingTerrainViewOnly();
        return 0;
    }
    if (args.Contains("--terrain-fly-game-view-only", StringComparer.OrdinalIgnoreCase))
    {
        RunTerrainFlyGameViewOnly();
        return 0;
    }
    if (args.Contains("--terrain-occlusion-only", StringComparer.OrdinalIgnoreCase))
    {
        RunTerrainOcclusionOnly();
        return 0;
    }
    if (args.Contains("--native-path-only", StringComparer.OrdinalIgnoreCase))
    {
        RunNativePathOnly();
        return 0;
    }
    Render(1440, 900, "spyro-editor-ui-1440x900.png");
    Render(1024, 720, "spyro-editor-ui-1024x720.png");

    Console.WriteLine("UI smoke used an isolated workspace; source editor files and disc image remained read-only.");
    return 0;
}
finally
{
    Environment.CurrentDirectory = previousDirectory;
    Environment.SetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE", previousWorkspace);
    Environment.SetEnvironmentVariable("SPYRO_EDITOR_RELEASE", previousReleaseMode);
    TryDeleteDirectory(workspace);
}

void RunTerrainAtomicOnly()
{
    MainWindow window = new()
    {
        Width = 1200,
        Height = 760,
        WindowStartupLocation = WindowStartupLocation.Manual,
        Position = new PixelPoint(0, 0)
    };
    window.Show();
    try
    {
        WaitForLevelData(window);
        AssertTerrainCatalogAtomicGuards(window);
    }
    finally
    {
        window.Close();
        FlushUi();
    }
}

void RunNativePathOnly()
{
    MainWindow window = new()
    {
        Width = 1440,
        Height = 900,
        WindowStartupLocation = WindowStartupLocation.Manual,
        Position = new PixelPoint(0, 0)
    };
    window.Show();
    try
    {
        WaitForLevelData(window);
        LevelCatalog catalog = LevelCatalog.Load(sourceWorkspace);
        LevelDefinition stoneHill = catalog.FindByKey("stonehill")
            ?? throw new InvalidOperationException("Native path UI smoke could not find Stone Hill.");
        SelectLevelForViewportFit(window, stoneHill);

        FieldInfo mobysField = typeof(MainWindow).GetField(
            "_currentMobys",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Native path UI smoke could not inspect current Mobys.");
        List<Moby> mobys = (List<Moby>)(mobysField.GetValue(window)
            ?? throw new InvalidOperationException("Native path UI smoke found no current Moby list."));
        Moby thief = mobys.Single(moby => moby.TrueIndex == 166);
        EditorViewport viewport = window.GetLogicalDescendants().OfType<EditorViewport>().Single();
        viewport.SetViewMode(ViewportViewMode.Map);
        viewport.SelectMoby(thief, true);
        FlushUi();

        FieldInfo actionField = typeof(MainWindow).GetField(
            "_objectNativeMovementButton",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Native path UI smoke could not inspect the path action.");
        Button action = (Button)(actionField.GetValue(window)
            ?? throw new InvalidOperationException("Native path UI smoke found no path action button."));
        if (!action.IsVisible || !action.IsEnabled || !string.Equals(action.Content?.ToString(), "Edit Run Path", StringComparison.Ordinal))
            throw new InvalidOperationException("Selecting verified Stone Hill egg thief T166 did not reveal the enabled Edit Run Path action.");

        FieldInfo pathsField = typeof(MainWindow).GetField(
            "_currentNativeMobyPaths",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Native path UI smoke could not inspect decoded routes.");
        List<NativeMobyPath> paths = (List<NativeMobyPath>)(pathsField.GetValue(window)
            ?? throw new InvalidOperationException("Native path UI smoke found no decoded routes."));
        NativeMobyPath path = paths.Single(route => route.OwnerTrueIndex == thief.TrueIndex);
        if (path.NodeCount != 13 || path.Nodes.Select(node => node.Index).SequenceEqual(Enumerable.Range(0, 13)) == false)
            throw new InvalidOperationException("Stone Hill's verified native route did not expose its fixed ordered 13 nodes.");

        SaveFrame(window, "spyro-editor-native-path-map.png");
        NativePathOverlaySnapshot mapOverlay = viewport.CaptureNativePathOverlaySnapshotForTesting();
        if (mapOverlay.ViewMode != ViewportViewMode.Map ||
            mapOverlay.OwnerTrueIndex != thief.TrueIndex ||
            mapOverlay.ProjectedNodeHandleCount != path.NodeCount ||
            mapOverlay.PolylineSegmentCount != path.NodeCount)
        {
            throw new InvalidOperationException($"Stone Hill Edit Map did not render all 13 numbered nodes and all 13 possible forward/reverse handler traversal segments: {mapOverlay}.");
        }

        viewport.SetMapYFlipped(true);
        viewport.SetViewMode(ViewportViewMode.Fly3D);
        viewport.SelectMoby(thief, true);
        FlushUi();
        SaveFrame(window, "spyro-editor-native-path-game-camera.png");
        NativePathOverlaySnapshot gameCameraOverlay = viewport.CaptureNativePathOverlaySnapshotForTesting();
        if (gameCameraOverlay.ViewMode != ViewportViewMode.Fly3D ||
            gameCameraOverlay.OwnerTrueIndex != thief.TrueIndex ||
            gameCameraOverlay.ProjectedNodeHandleCount <= 0 ||
            gameCameraOverlay.PolylineSegmentCount <= 0)
        {
            throw new InvalidOperationException($"Game Camera did not render draggable path handles and an ordered polyline: {gameCameraOverlay}.");
        }

        FieldInfo moveTogetherField = typeof(MainWindow).GetField(
            "_moveSelectedThiefPathWithOwner",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Native path UI smoke could not inspect move-together state.");
        if (moveTogetherField.GetValue(window) is not true)
            throw new InvalidOperationException("Move thief and path together is not enabled by default.");
        FieldInfo terrainSnapField = typeof(MainWindow).GetField(
            "_snapNativePathNodesToTerrain",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Native path UI smoke could not inspect terrain-snap state.");
        if (terrainSnapField.GetValue(window) is not true)
            throw new InvalidOperationException("Egg-thief path terrain snapping is not enabled by default.");

        NativePathNode first = path.Nodes[0];
        Vector3f ownerBefore = thief.Position;
        Vector3f nodeBefore = first.Position;
        MethodInfo moveGroup = typeof(MainWindow).GetMethod(
            "MoveLinkedMobyGroup",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Native path UI smoke could not move the selected thief.");
        moveGroup.Invoke(window, [thief, 1f, -2f, 0.5f, false]);
        if (first.RawX != first.OriginalRawX + 16 ||
            first.RawY != first.OriginalRawY - 32 ||
            first.RawZ != first.OriginalRawZ + 8)
        {
            throw new InvalidOperationException("Moving the thief did not translate every native path coordinate by the same fixed-16 delta.");
        }

        MethodInfo hasUndo = typeof(MainWindow).GetMethod(
            "HasUndoableMobyEdits",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Native path UI smoke could not inspect Undo availability.");
        if (hasUndo.Invoke(window, [thief]) is not true)
            throw new InvalidOperationException("A moved native path did not enable object Undo.");
        MethodInfo undo = typeof(MainWindow).GetMethod(
            "UndoMoby",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Native path UI smoke could not invoke Undo.");
        undo.Invoke(window, [thief]);
        if (thief.Position != ownerBefore || first.Position != nodeBefore || path.HasEdits)
            throw new InvalidOperationException("Undo did not restore the thief and its complete native route together.");

        first.SetRawPosition(
            checked(first.OriginalRawX + 16),
            checked(first.OriginalRawY - 32),
            checked(first.OriginalRawZ + 48));
        MethodInfo persist = typeof(MainWindow).GetMethod(
            "PersistCurrentNativeMovementEditsAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Native path UI smoke could not save route edits.");
        Task<int> saveTask = (Task<int>)(persist.Invoke(window, null)
            ?? throw new InvalidOperationException("Saving native route edits returned no task."));
        for (int attempt = 0; attempt < 1000 && !saveTask.IsCompleted; attempt++)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(5);
        }
        if (!saveTask.IsCompleted)
            throw new TimeoutException("Saving the native path document exceeded five seconds.");
        saveTask.GetAwaiter().GetResult();
        string editPath = Path.Combine(workspace, NativeMobyPathEditStore.DefaultFileName(stoneHill.Key));
        using (JsonDocument document = JsonDocument.Parse(File.ReadAllText(editPath)))
        {
            JsonElement saved = document.RootElement.GetProperty("edits")[0];
            JsonElement savedNode = saved.GetProperty("nodes")[0];
            if (document.RootElement.GetProperty("version").GetInt32() != NativeMobyPathEditStore.CurrentVersion ||
                saved.GetProperty("ownerTrueIndex").GetInt32() != thief.TrueIndex ||
                saved.GetProperty("nodeCount").GetInt32() != path.NodeCount ||
                savedNode.GetProperty("index").GetInt32() != first.Index ||
                savedNode.GetProperty("editedRawX").GetInt32() != first.RawX ||
                savedNode.GetProperty("preservedUnknownWord").GetInt32() != first.UnknownWord)
            {
                throw new InvalidOperationException("The versioned native path document did not preserve fixed identity, order, coordinates, and unknown-word preimage.");
            }
        }

        first.Reset();
        if (first.HasEdit || path.NodeCount != 13)
            throw new InvalidOperationException("Reset Node changed route structure or failed to restore its source XYZ.");
        path.Nodes[1].SetRawPosition(path.Nodes[1].OriginalRawX + 16, path.Nodes[1].OriginalRawY, path.Nodes[1].OriginalRawZ);
        path.ResetEdits();
        if (path.HasEdits || path.NodeCount != 13 || !path.Nodes.Select(node => node.Index).SequenceEqual(Enumerable.Range(0, 13)))
            throw new InvalidOperationException("Reset Whole Path changed fixed node count/order or left edited coordinates behind.");

        NativeMobyPathTraversalProfile[] cyclicProfiles =
            NativeMobyPathTraversalProfileRegistry.Profiles.ToArray();
        if (cyclicProfiles.Length != 12 ||
            cyclicProfiles.Any(profile =>
                profile.ClosingTraversal != NativeMobyPathClosingTraversal.CyclicForwardOrReverse))
        {
            throw new InvalidOperationException(
                $"Native path UI smoke expected 12 proven class-0x21 cyclic profiles, found {cyclicProfiles.Length}.");
        }

        int checkedCyclicPaths = 0;
        foreach (IGrouping<string, NativeMobyPathTraversalProfile> levelProfiles in cyclicProfiles
            .GroupBy(profile => LevelCatalog.NormalizeKey(profile.LevelKey)))
        {
            LevelDefinition level = catalog.FindByKey(levelProfiles.Key)
                ?? throw new InvalidOperationException(
                    $"Native path UI smoke could not find traversal-profile level {levelProfiles.Key}.");
            SelectLevelForViewportFit(window, level);
            List<Moby> levelMobys = (List<Moby>)(mobysField.GetValue(window)
                ?? throw new InvalidOperationException(
                    $"Native path UI smoke found no {level.DisplayName} Moby list."));
            List<NativeMobyPath> levelPaths = (List<NativeMobyPath>)(pathsField.GetValue(window)
                ?? throw new InvalidOperationException(
                    $"Native path UI smoke found no {level.DisplayName} routes."));
            foreach (NativeMobyPathTraversalProfile profile in levelProfiles)
            {
                Moby pathOwner = levelMobys.Single(moby => moby.TrueIndex == profile.OwnerTrueIndex);
                NativeMobyPath cyclicPath = levelPaths.Single(route =>
                    route.OwnerTrueIndex == pathOwner.TrueIndex);
                if (cyclicPath.OwnerNativeClass != EggThiefPathLocator.EggThiefNativeClass ||
                    NativeMobyPathTraversalProfileRegistry.Resolve(cyclicPath)?.ClosingTraversal !=
                        NativeMobyPathClosingTraversal.CyclicForwardOrReverse)
                {
                    throw new InvalidOperationException(
                        $"{level.DisplayName} T{pathOwner.TrueIndex} did not retain proven class-0x21 cyclic semantics.");
                }

                viewport.SetViewMode(ViewportViewMode.Map);
                viewport.SelectMoby(pathOwner, true);
                FlushUi();
                NativePathOverlaySnapshot cyclicOverlay =
                    viewport.CaptureNativePathOverlaySnapshotForTesting();
                if (cyclicPath.NodeCount > 1 &&
                    (cyclicOverlay.OwnerTrueIndex != pathOwner.TrueIndex ||
                     cyclicOverlay.ProjectedNodeHandleCount != cyclicPath.NodeCount ||
                     cyclicOverlay.PolylineSegmentCount != cyclicPath.NodeCount))
                {
                    throw new InvalidOperationException(
                        $"{level.DisplayName} T{pathOwner.TrueIndex} did not render N nodes and N possible forward/reverse traversal segments: {cyclicOverlay}.");
                }

                checkedCyclicPaths++;
            }
        }

        if (checkedCyclicPaths != cyclicProfiles.Length)
            throw new InvalidOperationException($"Checked {checkedCyclicPaths}/{cyclicProfiles.Length} cyclic path profiles.");

        Console.WriteLine("Native path UI smoke passed: all 12 decoded class-0x21 routes render the proven forward/reverse closing seam, Map/Game Camera handles and fixed ordering remain intact, terrain snap and move-together default on, and Undo/persistence/node reset/whole-path reset passed.");
    }
    finally
    {
        window.Close();
        FlushUi();
    }
}

void RunCreateBinFeedbackOnly()
{
    MainWindow window = new()
    {
        Width = 1200,
        Height = 760,
        WindowStartupLocation = WindowStartupLocation.Manual,
        Position = new PixelPoint(0, 0)
    };
    window.Show();
    try
    {
        FieldInfo createButtonField = typeof(MainWindow).GetField(
            "_toolbarCreateBinButton",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                "Create BIN feedback smoke could not inspect the toolbar button.");
        Button createButton = (Button)(createButtonField.GetValue(window)
            ?? throw new InvalidOperationException(
                "Create BIN feedback smoke found no toolbar button."));
        FieldInfo statusField = typeof(MainWindow).GetField(
            "_statusText",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                "Create BIN feedback smoke could not inspect the status line.");
        TextBlock statusText = (TextBlock)(statusField.GetValue(window)
            ?? throw new InvalidOperationException(
                "Create BIN feedback smoke found no status line."));

        TaskCompletionSource pending = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        Task active = window.RunCreateBinCommandWithVisibleFeedbackAsync(
            () => pending.Task);
        FlushUi();
        if (!string.Equals(
                createButton.Content?.ToString(),
                "Preparing BIN...",
                StringComparison.Ordinal) ||
            statusText.Text?.StartsWith(
                "Create BIN started",
                StringComparison.Ordinal) != true)
        {
            throw new InvalidOperationException(
                "Create BIN did not expose immediate toolbar and status-line feedback while work was pending.");
        }
        MethodInfo refreshActions = typeof(MainWindow).GetMethod(
            "RefreshActionAvailability",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                "Create BIN feedback smoke could not refresh toolbar availability.");
        refreshActions.Invoke(window, null);
        if (createButton.IsEnabled)
        {
            throw new InvalidOperationException(
                "Refreshing editor actions re-enabled Create BIN while its first build was still pending.");
        }

        bool duplicateCommandRan = false;
        Task duplicate = window.RunCreateBinCommandWithVisibleFeedbackAsync(
            () =>
            {
                duplicateCommandRan = true;
                return Task.CompletedTask;
            });
        WaitForUiTask(duplicate, "rejecting a duplicate Create BIN request");
        if (duplicateCommandRan ||
            statusText.Text?.Contains(
                "Create BIN is already running",
                StringComparison.Ordinal) != true)
        {
            throw new InvalidOperationException(
                "Create BIN allowed a second overlapping export instead of reporting that the first one was active.");
        }

        pending.SetResult();
        WaitForUiTask(active, "completing visible Create BIN feedback");
        if (!string.Equals(
                createButton.Content?.ToString(),
                "Create BIN",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Create BIN did not restore its toolbar label after completion.");
        }

        Task failed = window.RunCreateBinCommandWithVisibleFeedbackAsync(
            () => Task.FromException(
                new InvalidOperationException("focused feedback failure")));
        WaitForUiTask(failed, "showing a Create BIN failure");
        if (statusText.Text?.Contains(
                "Could not create BIN: focused feedback failure",
                StringComparison.Ordinal) != true)
        {
            throw new InvalidOperationException(
                "An unexpected Create BIN exception was not surfaced in the visible status line.");
        }

        Console.WriteLine(
            "Create BIN feedback smoke passed: pending work is visible, the toolbar label restores, and unexpected failures reach the status line.");
    }
    finally
    {
        window.Close();
        FlushUi();
    }
}

void RunTerrainNativeMapMaterialsOnly()
{
    MainWindow window = new()
    {
        Width = 1200,
        Height = 760,
        WindowStartupLocation = WindowStartupLocation.Manual,
        Position = new PixelPoint(0, 0)
    };
    window.Show();
    try
    {
        WaitForLevelData(window);
        LevelCatalog catalog = LevelCatalog.Load(sourceWorkspace);
        List<object> captures = [];
        int verifiedOverviewEndpointCorners = 0;
        int distinctOverviewEndpointCorners = 0;
        foreach (LevelDefinition level in catalog.Levels)
        {
            SelectLevelForViewportFit(window, level);
            EditorViewport viewport = window.GetLogicalDescendants().OfType<EditorViewport>().Single();
            viewport.SetViewMode(ViewportViewMode.Map);
            viewport.SetTerrainSceneViewMode(TerrainSceneViewMode.CompleteScene);
            viewport.ResetView();
            FlushUi();

            string frame = $"terrain-native-map-{level.Key}.png";
            SaveFrame(window, frame);
            NativeTerrainMapMaterialSnapshot snapshot =
                viewport.CaptureNativeTerrainMapMaterialSnapshotForTesting();
            GeometryCandidate geometry = viewport.Geometry
                ?? throw new InvalidOperationException($"{level.DisplayName} Map material smoke did not load geometry.");
            TerrainSceneViewSnapshot sceneView = viewport.CaptureTerrainSceneViewSnapshot();
            int materialOutcomeCount =
                snapshot.NativeTextureFaceCount +
                snapshot.NativeUntexturedGouraudFaceCount +
                snapshot.GuardedUnderlayFallbackFaceCount +
                snapshot.ProjectionDegenerateFaceCount +
                snapshot.UnresolvedFallbackFaceCount;
            if (!string.Equals(
                snapshot.Contract,
                    EditorViewport.NativeTerrainMapMaterialContract,
                    StringComparison.Ordinal) ||
                snapshot.VisibleFaceCount <= 0 ||
                sceneView.Mode != TerrainSceneViewMode.CompleteScene ||
                sceneView.PresentedFaceCount != sceneView.StoredFaceCount ||
                sceneView.HiddenFaceCount != 0 ||
                snapshot.NativeMaterialCandidateFaceCount <= 0 ||
                snapshot.VisibleFaceCount != snapshot.NativeMaterialCandidateFaceCount ||
                snapshot.NativeTextureFaceCount <= 0 ||
                snapshot.AvailableNormalTextureCount <= 0 ||
                materialOutcomeCount != snapshot.NativeMaterialCandidateFaceCount ||
                snapshot.OpaqueGuardedUnderlayFallbackFaceCount != 0 ||
                snapshot.UnresolvedFallbackFaceCount != 0 ||
                snapshot.SubduedBroadUnderlayFaceCount != 0 ||
                snapshot.OffCameraSourceOutlineFaceCount != 0)
            {
                throw new InvalidOperationException(
                    $"{level.DisplayName} did not material-render every complete-scene Map face: {snapshot}.");
            }

            foreach (TerrainPolygon polygon in geometry.Polygons.Where(face => face.HasNativeHighPolyMaterialPayload))
            {
                for (int slot = 0; slot < SourceSceneOverlayContract.CornerSlotCount; slot++)
                {
                    ColorRgba expected;
                    ColorRgba legacyRuntimeFar;
                    if (polygon.TextureVisualEdit is TerrainTextureVisualEdit visual &&
                        slot < visual.Corners.Count)
                    {
                        expected = visual.Corners[slot].NearColor;
                        legacyRuntimeFar = visual.Corners[slot].FarColor;
                    }
                    else
                    {
                        if (slot >= polygon.FarColors.Count || slot >= polygon.NearColors.Count)
                            continue;
                        expected = polygon.NearColors[slot];
                        legacyRuntimeFar = polygon.FarColors[slot];
                    }

                    if (!EditorViewport.TryGetNativeTerrainOverviewSourceColor(polygon, slot, out ColorRgba actual) ||
                        actual != expected)
                    {
                        throw new InvalidOperationException(
                            $"{level.DisplayName} {polygon.RuntimeKey} corner {slot} did not use physical HP table 2 / the proven all-level overview endpoint.");
                    }

                    verifiedOverviewEndpointCorners++;
                    if (expected != legacyRuntimeFar)
                        distinctOverviewEndpointCorners++;
                }
            }
            if (level.Key.Equals("clifftown", StringComparison.OrdinalIgnoreCase) &&
                (snapshot.GuardedUnderlayFallbackFaceCount != 0 ||
                 snapshot.NativeTextureFaceCount != snapshot.NativeMaterialCandidateFaceCount ||
                 snapshot.SubduedBroadUnderlayFaceCount != 0 ||
                 snapshot.OffCameraSourceOutlineFaceCount != 0))
            {
                throw new InvalidOperationException(
                    $"Cliff Town did not render all 5,339 opaque native faces with their materials: {snapshot}.");
            }

            captures.Add(new
            {
                levelKey = level.Key,
                levelName = level.DisplayName,
                frame,
                snapshot
            });
            Console.WriteLine(
                $"{level.DisplayName} Map materials: {snapshot.NativeTextureFaceCount:N0} textured, " +
                $"{snapshot.NativeUntexturedGouraudFaceCount:N0} exact untextured Gouraud, " +
                $"{snapshot.GuardedUnderlayFallbackFaceCount:N0} guarded translucent underlay, " +
                $"{snapshot.OpaqueGuardedUnderlayFallbackFaceCount:N0} opaque guard violations, " +
                $"{snapshot.ProjectionDegenerateFaceCount:N0} top-down-degenerate, " +
                $"{snapshot.UnresolvedFallbackFaceCount:N0} unresolved; " +
                $"{snapshot.AvailableNormalTextureCount} normal-HQ textures available.");
        }

        if (captures.Count != 35)
            throw new InvalidOperationException($"Expected 35 all-level Map material captures, produced {captures.Count}.");
        if (verifiedOverviewEndpointCorners <= 0 ||
            distinctOverviewEndpointCorners <= 0)
        {
            throw new InvalidOperationException(
                $"All-level complete-source policy drifted: physical table-2 corners {verifiedOverviewEndpointCorners:N0}, " +
                $"distinct from table 1 on {distinctOverviewEndpointCorners:N0} corners.");
        }

        string metricsPath = Path.Combine(outputDirectory, "terrain-native-map-materials-metrics.json");
        File.WriteAllText(metricsPath, JsonSerializer.Serialize(new
        {
            contract = EditorViewport.NativeTerrainMapMaterialContract,
            levelCount = captures.Count,
            verifiedOverviewEndpointCorners,
            distinctOverviewEndpointCorners,
            captures
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"All-level native Map material smoke passed: {metricsPath}");
    }
    finally
    {
        window.Close();
        FlushUi();
    }
}

void RunTerrainOcclusionOnly()
{
    MainWindow window = new()
    {
        Width = 1280,
        Height = 800,
        WindowStartupLocation = WindowStartupLocation.Manual,
        Position = new PixelPoint(0, 0)
    };
    window.Show();
    try
    {
        WaitForLevelData(window);
        LevelCatalog catalog = LevelCatalog.Load(sourceWorkspace);
        IReadOnlyDictionary<string, int> knownEntryGroups = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["artisans"] = 17,
            ["peacekeepers"] = 0,
            ["magiccrafters"] = 0,
            ["dreamweavers"] = 15
        };
        int levelsWithGroups = 0;
        int levelsWithResolvedEntryGroups = 0;
        foreach (LevelDefinition level in catalog.Levels)
        {
            SelectLevelForViewportFit(window, level);
            EditorViewport viewport = window.GetLogicalDescendants().OfType<EditorViewport>().Single();
            GeometryCandidate geometry = viewport.Geometry
                ?? throw new InvalidOperationException($"{level.DisplayName} occlusion smoke did not load geometry.");
            NativeTerrainOcclusionData occlusion = geometry.NativeTerrainOcclusion
                ?? throw new InvalidOperationException($"{level.DisplayName} is missing native terrain occlusion data.");

            viewport.SetViewMode(ViewportViewMode.Fly3D);
            viewport.RefreshFlyCameraForLoadedLevel();
            FlushUi();
            NativeTerrainOcclusionSnapshot gameCamera =
                viewport.CaptureNativeTerrainOcclusionSnapshotForTesting();
            if (!gameCamera.PayloadAvailable ||
                gameCamera.StoredSectorCount != geometry.SourceSectors.Count ||
                gameCamera.NativeGroupSectorCount <= 0 ||
                gameCamera.NativeGroupSectorCount > gameCamera.StoredSectorCount ||
                gameCamera.VisibleSectorCount != gameCamera.StoredSectorCount ||
                !string.Equals(gameCamera.Contract, SourceSceneOverlayContract.TerrainOcclusion, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"{level.DisplayName} Game Camera did not retain native group provenance while keeping all source sectors eligible: {gameCamera}.");
            }

            viewport.SetViewMode(ViewportViewMode.Map);
            FlushUi();
            NativeTerrainOcclusionSnapshot editMap =
                viewport.CaptureNativeTerrainOcclusionSnapshotForTesting();
            if (!editMap.PayloadAvailable ||
                editMap.StoredSectorCount != geometry.SourceSectors.Count ||
                editMap.NativeGroupSectorCount <= 0 ||
                editMap.VisibleSectorCount != editMap.StoredSectorCount)
            {
                throw new InvalidOperationException(
                    $"{level.DisplayName} Edit Map did not retain every source sector while preserving native group provenance: {editMap}.");
            }

            if (occlusion.EnvironmentGroups.Count > 0)
            {
                levelsWithGroups++;
                if (gameCamera.GroupIndex >= 0)
                    levelsWithResolvedEntryGroups++;
            }

            if (knownEntryGroups.TryGetValue(level.Key, out int knownGroup) &&
                gameCamera.GroupIndex != knownGroup)
            {
                throw new InvalidOperationException(
                    $"{level.DisplayName} resolved entry group {gameCamera.GroupIndex}, expected source-proven group {knownGroup}.");
            }

            if (level.Key.Equals("artisans", StringComparison.OrdinalIgnoreCase))
            {
                if (gameCamera.GroupIndex < 0 ||
                    gameCamera.NativeGroupSectorCount != 86 ||
                    gameCamera.VisibleSectorCount != gameCamera.StoredSectorCount ||
                    editMap.GroupIndex != gameCamera.GroupIndex ||
                    editMap.NativeGroupSectorCount != 86 ||
                    editMap.VisibleSectorCount != editMap.StoredSectorCount)
                {
                    throw new InvalidOperationException(
                        $"Artisans did not retain its collision-resolved 86-sector provenance while both views kept every source sector eligible: Game={gameCamera}; Map={editMap}.");
                }

                viewport.SetViewMode(ViewportViewMode.Fly3D);
                viewport.RefreshFlyCameraForLoadedLevel();
                FlushUi();
                double outsideX = occlusion.CollisionTriangles
                    .SelectMany(triangle => new[] { triangle.P1.X, triangle.P2.X, triangle.P3.X })
                    .Max() + 4096;
                double outsideY = occlusion.CollisionTriangles
                    .SelectMany(triangle => new[] { triangle.P1.Y, triangle.P2.Y, triangle.P3.Y })
                    .Max() + 4096;
                double outsideZ = occlusion.CollisionTriangles
                    .SelectMany(triangle => new[] { triangle.P1.Z, triangle.P2.Z, triangle.P3.Z })
                    .Max() + 2048;
                viewport.SetFlyGameCameraForTesting(outsideX, outsideY, outsideZ, -Math.PI * 0.5, -0.2);
                FlushUi();
                NativeTerrainOcclusionSnapshot outsideCollision =
                    viewport.CaptureNativeTerrainOcclusionSnapshotForTesting();
                if (outsideCollision.CollisionTriangleResolved ||
                    outsideCollision.GroupIndex != gameCamera.GroupIndex ||
                    outsideCollision.NativeGroupSectorCount != gameCamera.NativeGroupSectorCount ||
                    outsideCollision.VisibleSectorCount != outsideCollision.StoredSectorCount)
                {
                    throw new InvalidOperationException(
                        $"Artisans free camera lost its last proven native group or stopped keeping all source sectors eligible: Entry={gameCamera}; Outside={outsideCollision}.");
                }

                IReadOnlyList<int> entryGroup = occlusion.EnvironmentGroups[gameCamera.GroupIndex];
                int promotedOffGroupFace = Enumerable.Range(0, geometry.Polygons.Count)
                    .Single(index =>
                        geometry.Polygons[index].SectorIndex == 132 &&
                        geometry.Polygons[index].FaceIndex == 2 &&
                        geometry.Polygons[index].NativeTextureId == 58 &&
                        geometry.Polygons[index].HasCompleteNativeHighPolyFacePayload &&
                        geometry.Polygons[index].HasNativeCornerPayload &&
                        !geometry.Polygons[index].IsNativeUntexturedSentinel &&
                        !geometry.Polygons[index].NativePrimitiveSemiTransparent &&
                        geometry.Polygons[index].Points.Count == 4);
                viewport.RefreshFlyCameraForLoadedLevel();
                FlushUi();
                viewport.AimFlyCameraAtTerrainForTesting(promotedOffGroupFace, 900);
                FlushUi();
                NativeTerrainOcclusionSnapshot promotedContext =
                    viewport.CaptureNativeTerrainOcclusionSnapshotForTesting();
                FlyTerrainVisibilitySnapshot promotedVisibility =
                    viewport.CaptureFlyTerrainVisibilitySnapshotForTesting();
                if (entryGroup.Contains(132) ||
                    promotedContext.CollisionTriangleResolved ||
                    promotedContext.GroupIndex != gameCamera.GroupIndex ||
                    promotedVisibility.ForcedVisibleSectorCount != 0 ||
                    viewport.CaptureFlyCameraInactiveBackdropFaceIndexesForTesting().Contains(promotedOffGroupFace) ||
                    !viewport.CaptureVisibleHighDetailTerrainFaceIndexesForTesting().Contains(promotedOffGroupFace) ||
                    !viewport.WasHighDetailTerrainFaceNativeTexturedForTesting(promotedOffGroupFace))
                {
                    throw new InvalidOperationException(
                        $"Artisans opaque textured sector 132 face 2 did not remain on nearby HP outside retained group {gameCamera.GroupIndex}: " +
                        $"HP index visible={viewport.CaptureVisibleHighDetailTerrainFaceIndexesForTesting().Contains(promotedOffGroupFace)}; " +
                        $"native textured={viewport.WasHighDetailTerrainFaceNativeTexturedForTesting(promotedOffGroupFace)}; " +
                        $"context={promotedContext}; visibility={promotedVisibility}.");
                }
                SaveFrame(window, "terrain-fly-game-view-artisans-off-group-hp.png");

                LowDetailTerrainPolygon promotedLowDetailFace = geometry.LowDetailPolygons.Single(face =>
                    face.SectorIndex == 132 &&
                    face.FaceIndex == 0 &&
                    face.VertexIndexes.SequenceEqual(new[] { 0, 1, 3, 2 }));
                viewport.AimFlyCameraAtTerrainForTesting(promotedOffGroupFace, 2400);
                FlushUi();
                if (viewport.CaptureVisibleHighDetailTerrainFaceIndexesForTesting().Contains(promotedOffGroupFace) ||
                    !viewport.CaptureVisibleLowDetailTerrainFaceRuntimeKeysForTesting().Contains(promotedLowDetailFace.RuntimeKey) ||
                    viewport.CaptureVisibleLowDetailTerrainCornerColorCountForTesting(promotedLowDetailFace.RuntimeKey) < 4)
                {
                    throw new InvalidOperationException(
                        $"Artisans sector 132 did not transition from textured HP to its four-color source LP face at distance: " +
                        $"HP visible={viewport.CaptureVisibleHighDetailTerrainFaceIndexesForTesting().Contains(promotedOffGroupFace)}; " +
                        $"LP key={promotedLowDetailFace.RuntimeKey}; " +
                        $"LP colors={viewport.CaptureVisibleLowDetailTerrainCornerColorCountForTesting(promotedLowDetailFace.RuntimeKey)}.");
                }
                SaveFrame(window, "terrain-fly-game-view-artisans-off-group-lp.png");

                int offGroupBackgroundFace = Enumerable.Range(0, geometry.Polygons.Count)
                    .Single(index =>
                        geometry.Polygons[index].SectorIndex == 38 &&
                        geometry.Polygons[index].FaceIndex == 36 &&
                        geometry.Polygons[index].NativeTextureId == 27 &&
                        Math.Abs(geometry.Polygons[index].AvgZ - 192) < 0.01);
                viewport.AimFlyCameraAtTerrainForTesting(offGroupBackgroundFace, 900);
                FlushUi();
                FlyTerrainVisibilitySnapshot backgroundVisibility =
                    viewport.CaptureFlyTerrainVisibilitySnapshotForTesting();
                if (entryGroup.Contains(38) ||
                    backgroundVisibility.SuppressedHighDetailByGroupCount != 0 ||
                    !viewport.CaptureVisibleHighDetailTerrainFaceIndexesForTesting().Contains(offGroupBackgroundFace) ||
                    viewport.CaptureFlyCameraInactiveBackdropFaceIndexesForTesting().Contains(offGroupBackgroundFace) ||
                    !viewport.WasHighDetailTerrainFaceNativeTexturedForTesting(offGroupBackgroundFace))
                {
                    throw new InvalidOperationException(
                        $"Artisans' unique off-group background face {geometry.Polygons[offGroupBackgroundFace].RuntimeKey} " +
                        $"was not retained as visible native-textured terrain outside the active retail group: {backgroundVisibility}.");
                }
                SaveFrame(window, "terrain-fly-game-view-artisans-off-group-background-full-material.png");

                viewport.SetViewMode(ViewportViewMode.Map);
                FlushUi();

                IReadOnlyList<int> activeGroup = occlusion.EnvironmentGroups[gameCamera.GroupIndex];
                int activePlaneSectors = geometry.Polygons
                    .Where(face => face.NativeTextureId == 27 && Math.Abs(face.AvgZ - 192) < 0.01)
                    .Select(face => face.SectorIndex)
                    .Distinct()
                    .Count(activeGroup.Contains);
                if (activePlaneSectors != 1)
                    throw new InvalidOperationException($"Artisans camera context activated {activePlaneSectors} texture-27 Z192 sectors instead of one.");

                int offGroupPlaneIndex = Enumerable.Range(0, geometry.Polygons.Count)
                    .First(index =>
                        geometry.Polygons[index].NativeTextureId == 27 &&
                        Math.Abs(geometry.Polygons[index].AvgZ - 192) < 0.01 &&
                        !activeGroup.Contains(geometry.Polygons[index].SectorIndex));
                viewport.FocusTerrain(offGroupPlaneIndex);
                FlushUi();
                NativeTerrainOcclusionSnapshot selectedContext =
                    viewport.CaptureNativeTerrainOcclusionSnapshotForTesting();
                int selectedSector = geometry.Polygons[offGroupPlaneIndex].SectorIndex;
                if (selectedContext.GroupIndex < 0 ||
                    !occlusion.EnvironmentGroups[selectedContext.GroupIndex].Contains(selectedSector))
                {
                    throw new InvalidOperationException(
                        $"Selecting Artisans off-camera source face {offGroupPlaneIndex} did not activate a retail group containing sector {selectedSector}: {selectedContext}.");
                }
            }

            Console.WriteLine(
                $"{level.DisplayName}: groups {occlusion.EnvironmentGroups.Count}, " +
                $"entry {gameCamera.GroupIndex}, native group {gameCamera.NativeGroupSectorCount}/{gameCamera.StoredSectorCount}, " +
                $"Game Camera eligible {gameCamera.VisibleSectorCount}/{gameCamera.StoredSectorCount}.");
        }

        if (levelsWithGroups <= 0 || levelsWithResolvedEntryGroups <= 0)
            throw new InvalidOperationException("No level resolved a source-native entry-camera occlusion group.");
        Console.WriteLine(
            $"Native terrain occlusion UI smoke passed: {catalog.Levels.Count}/{catalog.Levels.Count} payloads, " +
            $"{levelsWithResolvedEntryGroups}/{levelsWithGroups} grouped levels resolved at entry.");
    }
    finally
    {
        window.Close();
        FlushUi();
    }
}

void RunShippingTerrainViewOnly()
{
    MainWindow window = new()
    {
        Width = 1320,
        Height = 860,
        WindowStartupLocation = WindowStartupLocation.Manual,
        Position = new PixelPoint(0, 0)
    };
    window.Show();
    try
    {
        WaitForLevelData(window);
        EditorViewport viewport = window.GetLogicalDescendants().OfType<EditorViewport>().Single();
        ToggleButton editMap = window.GetLogicalDescendants()
            .OfType<ToggleButton>()
            .Single(button => string.Equals(button.Name, "EditMapViewButton", StringComparison.Ordinal));
        ToggleButton gameCamera = window.GetLogicalDescendants()
            .OfType<ToggleButton>()
            .Single(button => string.Equals(button.Name, "GameCameraViewButton", StringComparison.Ordinal));
        Button viewportAction = window.GetLogicalDescendants()
            .OfType<Button>()
            .Single(button => string.Equals(button.Name, "ViewportActionButton", StringComparison.Ordinal));

        if (window.GetLogicalDescendants().OfType<Button>().Any(button =>
                string.Equals(button.Name, "TerrainSceneViewButton", StringComparison.Ordinal) ||
                string.Equals(button.Content?.ToString(), "Playable View", StringComparison.Ordinal) ||
                string.Equals(button.Content?.ToString(), "Complete Scene", StringComparison.Ordinal) ||
                string.Equals(button.Content?.ToString(), "Game View: On", StringComparison.Ordinal) ||
                string.Equals(button.Content?.ToString(), "Raw Capture View", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                "The shipping toolbar still exposes a content-hiding or raw-orientation presentation control.");
        }

        viewport.ResetSelection();
        FlushUi();
        TerrainSceneViewSnapshot initial = viewport.CaptureTerrainSceneViewSnapshot();
        if (viewport.ViewMode != ViewportViewMode.Fly3D ||
            !viewport.IsMapYFlipped ||
            initial.Mode != TerrainSceneViewMode.CompleteScene ||
            initial.HasPlayablePredicate ||
            initial.StoredFaceCount <= 0 ||
            initial.PresentedFaceCount != initial.StoredFaceCount ||
            initial.HiddenFaceCount != 0 ||
            !string.Equals(editMap.Content?.ToString(), "Edit Map", StringComparison.Ordinal) ||
            !string.Equals(gameCamera.Content?.ToString(), "Game Camera", StringComparison.Ordinal) ||
            !string.Equals(viewportAction.Content?.ToString(), "Return to Entry", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"The shipping viewport did not start in complete-source Game Camera mode: {initial}; " +
                $"mode={viewport.ViewMode}; action={viewportAction.Content}.");
        }

        editMap.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, editMap));
        FlushUi();
        TerrainSceneViewSnapshot editMapScene = viewport.CaptureTerrainSceneViewSnapshot();
        if (viewport.ViewMode != ViewportViewMode.Map ||
            editMapScene.PresentedFaceCount != editMapScene.StoredFaceCount ||
            editMapScene.HiddenFaceCount != 0 ||
            !string.Equals(viewportAction.Content?.ToString(), "Fit All", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Edit Map did not retain the complete source scene: {editMapScene}; action={viewportAction.Content}.");
        }
        viewportAction.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, viewportAction));
        FlushUi();
        if (viewport.ViewMode != ViewportViewMode.Map)
            throw new InvalidOperationException("Fit All unexpectedly left Edit Map.");

        gameCamera.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, gameCamera));
        FlushUi();
        Moby focus = viewport.Mobys.First(moby => !moby.IsRemoved);
        viewport.SelectMoby(focus, focus: false);
        FlushUi();
        if (!string.Equals(viewportAction.Content?.ToString(), "Frame Selection", StringComparison.Ordinal))
            throw new InvalidOperationException("Selecting an object did not expose the Game Camera Frame Selection action.");
        viewportAction.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, viewportAction));
        FlushUi();
        FlyTerrainVisibilitySnapshot framed = viewport.CaptureFlyTerrainVisibilitySnapshotForTesting();
        if (viewport.ViewMode != ViewportViewMode.Fly3D ||
            framed.Mode != FlyTerrainVisibilityMode.GameViewCameraLocalHighDetail)
        {
            throw new InvalidOperationException(
                $"Frame Selection did not preserve the local Game Camera renderer: {framed}.");
        }

        LevelCatalog catalog = LevelCatalog.Load(sourceWorkspace);
        int verifiedLevels = 0;
        foreach (LevelDefinition level in catalog.Levels)
        {
            SelectLevelForViewportFit(window, level);
            viewport = window.GetLogicalDescendants().OfType<EditorViewport>().Single();
            TerrainSceneViewSnapshot scene = viewport.CaptureTerrainSceneViewSnapshot();
            if (viewport.ViewMode != ViewportViewMode.Fly3D ||
                scene.Mode != TerrainSceneViewMode.CompleteScene ||
                scene.HasPlayablePredicate ||
                scene.StoredFaceCount <= 0 ||
                scene.PresentedFaceCount != scene.StoredFaceCount ||
                scene.HiddenFaceCount != 0)
            {
                throw new InvalidOperationException(
                    $"{level.DisplayName} did not load into complete-source Game Camera mode: {scene}; " +
                    $"mode={viewport.ViewMode}.");
            }
            verifiedLevels++;
        }

        SaveFrame(window, "terrain-shipping-game-camera.png");
        Console.WriteLine(
            $"Shipping terrain view: {verifiedLevels}/{catalog.Levels.Count} levels loaded into Game Camera with every captured face retained; " +
            "Edit Map/Fit All and object Frame Selection passed, and no Playable/Complete or raw-orientation control is exposed.");
    }
    finally
    {
        window.Close();
        FlushUi();
    }
}

#pragma warning disable CS8321 // Retained as an internal classifier regression; the shipping CLI now exercises the complete-source UI.
void RunTerrainSceneViewOnly()
{
    MainWindow window = new()
    {
        Width = 1320,
        Height = 860,
        WindowStartupLocation = WindowStartupLocation.Manual,
        Position = new PixelPoint(0, 0)
    };
    window.Show();
    try
    {
        WaitForLevelData(window);
        EditorViewport viewport = window.GetLogicalDescendants().OfType<EditorViewport>().Single();
        LevelCatalog catalog = LevelCatalog.Load(sourceWorkspace);
        int productLevelsWithSuppression = 0;
        int productHiddenFaces = 0;
        int artisansHiddenFaces = -1;
        int artisansPresentedFaces = -1;
        int beastMakersHiddenFaces = -1;
        int beastMakersPresentedFaces = -1;
        Button toggle = window.GetLogicalDescendants()
            .OfType<Button>()
            .Single(button => string.Equals(button.Name, "TerrainSceneViewButton", StringComparison.Ordinal));
        TerrainSceneViewSnapshot defaultView = viewport.CaptureTerrainSceneViewSnapshot();
        if (defaultView.Mode != TerrainSceneViewMode.CompleteScene ||
            defaultView.StoredFaceCount <= 0 ||
            defaultView.PresentedFaceCount != defaultView.StoredFaceCount ||
            defaultView.HiddenFaceCount != 0 ||
            !string.Equals(toggle.Content?.ToString(), "Complete Scene", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"The release viewport did not default to the complete visible terrain scene: {defaultView}; button={toggle.Content}.");
        }
        viewport.SetTerrainSceneViewMode(TerrainSceneViewMode.Playable);
        foreach (LevelDefinition level in catalog.Levels)
        {
            SelectLevelForViewportFit(window, level);
            viewport = window.GetLogicalDescendants().OfType<EditorViewport>().Single();
            TerrainSceneViewSnapshot productView = viewport.CaptureTerrainSceneViewSnapshot();
            if (productView.Mode != TerrainSceneViewMode.Playable ||
                !productView.HasPlayablePredicate ||
                productView.StoredFaceCount <= 0 ||
                productView.PresentedFaceCount <= 0 ||
                productView.PresentedFaceCount + productView.HiddenFaceCount != productView.StoredFaceCount)
            {
                throw new InvalidOperationException(
                    $"{level.DisplayName} did not load the level-aware Playable View plan: {productView}.");
            }
            if (productView.HiddenFaceCount > 0)
            {
                productLevelsWithSuppression++;
                productHiddenFaces += productView.HiddenFaceCount;

                viewport.SetViewMode(ViewportViewMode.Map);
                viewport.ResetView();
                FlushUi();
                if (!string.Equals(toggle.Content?.ToString(), "Playable View", StringComparison.Ordinal))
                    throw new InvalidOperationException($"{level.DisplayName} Playable capture had a stale terrain-view toolbar label: {toggle.Content}.");
                SaveFrame(window, $"terrain-playable-view-{level.Key}.png");

                toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, toggle));
                viewport.ResetView();
                FlushUi();
                TerrainSceneViewSnapshot completeProductView = viewport.CaptureTerrainSceneViewSnapshot();
                if (completeProductView.PresentedFaceCount != productView.StoredFaceCount ||
                    completeProductView.HiddenFaceCount != 0 ||
                    !string.Equals(toggle.Content?.ToString(), "Complete Scene", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"{level.DisplayName} Complete Scene did not restore its production terrain and toolbar label after the paired Playable capture: {completeProductView}; button={toggle.Content}.");
                }
                SaveFrame(window, $"terrain-complete-scene-{level.Key}.png");
                toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, toggle));
                FlushUi();
                if (viewport.TerrainSceneViewMode != TerrainSceneViewMode.Playable ||
                    !string.Equals(toggle.Content?.ToString(), "Playable View", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"{level.DisplayName} paired capture did not return to Playable View.");
                }
            }
            if (level.Key.Equals("artisans", StringComparison.OrdinalIgnoreCase))
            {
                artisansHiddenFaces = productView.HiddenFaceCount;
                artisansPresentedFaces = productView.PresentedFaceCount;
                SaveFrame(window, "terrain-playable-view-artisans.png");
            }
            if (level.Key.Equals("beastmakers", StringComparison.OrdinalIgnoreCase))
            {
                beastMakersHiddenFaces = productView.HiddenFaceCount;
                beastMakersPresentedFaces = productView.PresentedFaceCount;
                viewport.SetViewMode(ViewportViewMode.Map);
                viewport.ResetView();
                FlushUi();
                SaveFrame(window, "terrain-playable-view-beastmakers-retained.png");
                toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, toggle));
                viewport.ResetView();
                FlushUi();
                TerrainSceneViewSnapshot beastMakersComplete = viewport.CaptureTerrainSceneViewSnapshot();
                if (beastMakersComplete.PresentedFaceCount != productView.StoredFaceCount ||
                    beastMakersComplete.HiddenFaceCount != 0 ||
                    !string.Equals(toggle.Content?.ToString(), "Complete Scene", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Beast Makers retained-context Complete Scene drifted: {beastMakersComplete}; button={toggle.Content}.");
                }
                SaveFrame(window, "terrain-complete-scene-beastmakers-retained.png");
                toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, toggle));
                FlushUi();
            }
        }
        if (productLevelsWithSuppression != 7 || productHiddenFaces != 9_008 ||
            artisansHiddenFaces != 780 || artisansPresentedFaces != 4419 ||
            beastMakersHiddenFaces != 0 || beastMakersPresentedFaces != 1977)
        {
            throw new InvalidOperationException(
                $"All-level Playable View did not preserve the audited suppression/retention contract: " +
                $"levels={productLevelsWithSuppression}, hidden={productHiddenFaces}, " +
                $"Artisans={artisansPresentedFaces} presented/{artisansHiddenFaces} hidden (expected 4,419/780), " +
                $"Beast Makers={beastMakersPresentedFaces} presented/{beastMakersHiddenFaces} hidden (expected 1,977/0). ");
        }
        Console.WriteLine(
            $"All-level Playable View plans: {productHiddenFaces:N0} audited oversized overview faces hidden across " +
            $"{productLevelsWithSuppression}/{catalog.Levels.Count} levels; Artisans hides {artisansHiddenFaces:N0}.");

        LevelDefinition stoneHill = catalog.Levels.Single(level =>
            level.Key.Equals("stonehill", StringComparison.OrdinalIgnoreCase));
        SelectLevelForViewportFit(window, stoneHill);
        viewport = window.GetLogicalDescendants().OfType<EditorViewport>().Single();
        GeometryCandidate geometry = viewport.Geometry
            ?? throw new InvalidOperationException("Terrain scene-view smoke did not load geometry.");
        if (geometry.Polygons.Count < 4)
            throw new InvalidOperationException("Terrain scene-view smoke needs a representative multi-face level.");

        int keptCount = Math.Max(1, geometry.Polygons.Count / 2);
        HashSet<TerrainPolygon> playableFixture = new(
            geometry.Polygons.Take(keptCount),
            ReferenceEqualityComparer.Instance);
        viewport.PlayableTerrainViewPredicate = (_, polygon) => playableFixture.Contains(polygon);
        viewport.SetTerrainSceneViewMode(TerrainSceneViewMode.Playable);
        viewport.SetViewMode(ViewportViewMode.Map);
        viewport.ResetView();
        FlushUi();

        TerrainSceneViewSnapshot playable = viewport.CaptureTerrainSceneViewSnapshot();
        if (playable.Mode != TerrainSceneViewMode.Playable ||
            !playable.HasPlayablePredicate ||
            playable.StoredFaceCount != geometry.Polygons.Count ||
            playable.PresentedFaceCount != keptCount ||
            playable.HiddenFaceCount != geometry.Polygons.Count - keptCount ||
            !string.Equals(toggle.Content?.ToString(), "Playable View", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Playable View did not default to the filtered editor-only presentation: {playable}; button={toggle.Content}.");
        }

        int pinnedHiddenIndex = Enumerable.Range(keptCount, geometry.Polygons.Count - keptCount)
            .FirstOrDefault(index =>
                !geometry.Polygons[index].IsTerrainEdited &&
                !geometry.Polygons[index].IsTerrainRemoved &&
                !geometry.Polygons[index].IsTerrainAddClone,
                -1);
        if (pinnedHiddenIndex < 0)
            throw new InvalidOperationException("Terrain scene-view smoke could not find an unedited hidden face for pinning.");
        TerrainPolygon visibleEditingFace = geometry.Polygons[0];
        TerrainPolygon hiddenEditingFace = geometry.Polygons[pinnedHiddenIndex];
        if (!viewport.IsTerrainPresentedForEditing(visibleEditingFace) ||
            viewport.IsTerrainPresentedForEditing(hiddenEditingFace))
        {
            throw new InvalidOperationException(
                "Playable Map editing did not use the same visible-face predicate as its brush preview and hit testing.");
        }

        string[] pinnedIndexFields =
        [
            "_selectedTerrainIndex",
            "_hoverTerrainIndex",
            "_terrainBrushPreviewTerrainIndex"
        ];
        foreach (string fieldName in pinnedIndexFields)
        {
            viewport.SetTerrainSceneViewMode(TerrainSceneViewMode.CompleteScene);
            foreach (string resetFieldName in pinnedIndexFields)
            {
                typeof(EditorViewport).GetField(resetFieldName, BindingFlags.Instance | BindingFlags.NonPublic)!
                    .SetValue(viewport, -1);
            }
            typeof(EditorViewport).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(viewport, pinnedHiddenIndex);
            viewport.SetTerrainSceneViewMode(TerrainSceneViewMode.Playable);
            TerrainSceneViewSnapshot pinned = viewport.CaptureTerrainSceneViewSnapshot();
            if (pinned.PresentedFaceCount != keptCount + 1)
            {
                throw new InvalidOperationException(
                    $"Playable View made the active {fieldName} face disappear: {pinned}.");
            }
        }

        viewport.SetTerrainSceneViewMode(TerrainSceneViewMode.CompleteScene);
        foreach (string fieldName in pinnedIndexFields)
        {
            typeof(EditorViewport).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(viewport, -1);
        }
        typeof(EditorViewport).GetField("_selectedTerrainIndex", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(viewport, pinnedHiddenIndex);
        viewport.SetTerrainSceneViewMode(TerrainSceneViewMode.Playable);
        ViewportFitSnapshot pinnedFit = viewport.CaptureFitSnapshotForTesting();
        Moby selectionTarget = viewport.Mobys.First(moby => !moby.IsRemoved);
        viewport.SelectMoby(selectionTarget, focus: false);
        FlushUi();
        ViewportFitSnapshot mobyClearedFit = viewport.CaptureFitSnapshotForTesting();
        viewport.ResetSelection();
        ViewportFitSnapshot resetFit = viewport.CaptureFitSnapshotForTesting();
        if (pinnedFit.SourcePolygonCount != keptCount + 1 ||
            mobyClearedFit.SourcePolygonCount != keptCount ||
            resetFit.SourcePolygonCount != keptCount)
        {
            throw new InvalidOperationException(
                $"Selecting a Moby did not clear the hidden-face pin and invalidate Playable Fit: " +
                $"pinned={pinnedFit.SourcePolygonCount}, Moby={mobyClearedFit.SourcePolygonCount}, reset={resetFit.SourcePolygonCount}, " +
                $"expected={keptCount + 1}/{keptCount}/{keptCount}.");
        }

        hiddenEditingFace.ApplyTerrainDeltaZ(1f);
        viewport.NotifyTerrainPresentationDataChanged();
        ViewportFitSnapshot editedVisibleFit = viewport.CaptureFitSnapshotForTesting();
        hiddenEditingFace.ResetTerrainEdit();
        viewport.NotifyTerrainPresentationDataChanged();
        ViewportFitSnapshot editResetFit = viewport.CaptureFitSnapshotForTesting();
        if (editedVisibleFit.SourcePolygonCount != keptCount + 1 ||
            editResetFit.SourcePolygonCount != keptCount)
        {
            throw new InvalidOperationException(
                $"Terrain edit/reset state did not invalidate Playable Fit visibility: " +
                $"edited={editedVisibleFit.SourcePolygonCount}, reset={editResetFit.SourcePolygonCount}, " +
                $"expected={keptCount + 1}/{keptCount}.");
        }

        viewport.SetTerrainSceneViewMode(TerrainSceneViewMode.CompleteScene);
        typeof(EditorViewport).GetField("_selectedTerrainIndex", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(viewport, pinnedHiddenIndex);
        viewport.SetTerrainSceneViewMode(TerrainSceneViewMode.Playable);
        viewport.FocusMobys(viewport.Mobys.Where(moby => !moby.IsRemoved).Take(2).ToArray());
        ViewportFitSnapshot groupFocusFit = viewport.CaptureFitSnapshotForTesting();
        if (groupFocusFit.SourcePolygonCount != keptCount)
        {
            throw new InvalidOperationException(
                $"Focusing a Moby group left a hidden terrain selection pinned in Playable Fit: {groupFocusFit.SourcePolygonCount}/{keptCount}.");
        }

        viewport.SetTerrainSceneViewMode(TerrainSceneViewMode.CompleteScene);
        if (!viewport.IsTerrainPresentedForEditing(hiddenEditingFace))
            throw new InvalidOperationException("Complete Scene did not restore its hidden face to terrain editing and snapping.");
        viewport.SetTerrainSceneViewMode(TerrainSceneViewMode.Playable);
        SaveFrame(window, "terrain-playable-view.png");
        window.Width = 1024;
        window.Height = 720;
        SaveFrame(window, "terrain-playable-view-1024x720.png");
        window.Width = 980;
        window.Height = 640;
        SaveFrame(window, "terrain-playable-view-980x640.png");
        window.Width = 1320;
        window.Height = 860;
        FlushUi();

        viewport.SetMapYFlipped(true);
        viewport.SetViewMode(ViewportViewMode.Fly3D);
        FlushUi();
        FlyTerrainVisibilitySnapshot playableLocalGameView =
            viewport.CaptureFlyTerrainVisibilitySnapshotForTesting();
        if (!viewport.IsTerrainPresentedForEditing(hiddenEditingFace))
            throw new InvalidOperationException("Local Fly Game View incorrectly excluded real nearby source terrain from editing and snapping.");
        viewport.SetTerrainSceneViewMode(TerrainSceneViewMode.CompleteScene);
        FlushUi();
        FlyTerrainVisibilitySnapshot completeLocalGameView =
            viewport.CaptureFlyTerrainVisibilitySnapshotForTesting();
        if (playableLocalGameView.Mode != FlyTerrainVisibilityMode.GameViewCameraLocalHighDetail ||
            completeLocalGameView.Mode != FlyTerrainVisibilityMode.GameViewCameraLocalHighDetail ||
            playableLocalGameView.VisibleHighDetailFaceCount != completeLocalGameView.VisibleHighDetailFaceCount ||
            playableLocalGameView.SuppressedHighDetailBySectorCount != completeLocalGameView.SuppressedHighDetailBySectorCount ||
            playableLocalGameView.SuppressedHighDetailByFaceDistanceCount != completeLocalGameView.SuppressedHighDetailByFaceDistanceCount)
        {
            throw new InvalidOperationException(
                $"Playable filtering leaked into local Fly Game View: playable={playableLocalGameView}; complete={completeLocalGameView}.");
        }
        viewport.SetViewMode(ViewportViewMode.Map);
        viewport.SetTerrainSceneViewMode(TerrainSceneViewMode.Playable);
        viewport.ResetView();
        FlushUi();

        toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, toggle));
        FlushUi();
        TerrainSceneViewSnapshot complete = viewport.CaptureTerrainSceneViewSnapshot();
        if (complete.Mode != TerrainSceneViewMode.CompleteScene ||
            complete.PresentedFaceCount != geometry.Polygons.Count ||
            complete.HiddenFaceCount != 0 ||
            !string.Equals(toggle.Content?.ToString(), "Complete Scene", StringComparison.Ordinal) ||
            !ReferenceEquals(viewport.Geometry, geometry))
        {
            throw new InvalidOperationException(
                $"Complete Scene did not restore every source face without replacing geometry: {complete}; button={toggle.Content}.");
        }
        SaveFrame(window, "terrain-complete-scene.png");

        toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, toggle));
        FlushUi();
        TerrainSceneViewSnapshot restored = viewport.CaptureTerrainSceneViewSnapshot();
        if (restored != playable || !ReferenceEquals(viewport.Geometry, geometry))
        {
            throw new InvalidOperationException(
                $"Returning to Playable View changed source geometry or lost the presentation filter: {restored}.");
        }

        string baselineEditSignature = CaptureProductionTerrainEditSignature(geometry);
        if (visibleEditingFace.IsTerrainEdited)
            throw new InvalidOperationException("Terrain scene-view export-input proof requires an initially unedited visible face.");
        visibleEditingFace.ApplyTerrainDeltaZ(1f);
        string editSignatureBefore = CaptureProductionTerrainEditSignature(geometry);
        if (string.IsNullOrWhiteSpace(editSignatureBefore) ||
            string.Equals(editSignatureBefore, baselineEditSignature, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The representative terrain edit did not reach the production terrain-edit signature.");
        }

        string proofDirectory = Path.Combine(workspace, "_local", "playable-view-export-input-proof");
        Directory.CreateDirectory(proofDirectory);
        string editStoreBeforePath = Path.Combine(proofDirectory, "stonehill-before.json");
        string editStoreAfterPath = Path.Combine(proofDirectory, "stonehill-after.json");
        Task.Run(async () => await TerrainEditStore.SaveAsync(
                editStoreBeforePath,
                geometry.Polygons,
                stoneHill.DisplayName))
            .GetAwaiter()
            .GetResult();
        string editStoreHashBefore = CanonicalJsonSha256(editStoreBeforePath, "generatedAt");
        SortedDictionary<string, string> projectInputsBefore =
            CaptureTerrainProjectInputHashes(workspace, stoneHill.Key);

        toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, toggle));
        FlushUi();
        if (viewport.TerrainSceneViewMode != TerrainSceneViewMode.CompleteScene)
            throw new InvalidOperationException("Export-input proof did not enter Complete Scene through the production toggle.");
        toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, toggle));
        FlushUi();
        if (viewport.TerrainSceneViewMode != TerrainSceneViewMode.Playable)
            throw new InvalidOperationException("Export-input proof did not return to Playable View through the production toggle.");

        string editSignatureAfter = CaptureProductionTerrainEditSignature(geometry);
        Task.Run(async () => await TerrainEditStore.SaveAsync(
                editStoreAfterPath,
                geometry.Polygons,
                stoneHill.DisplayName))
            .GetAwaiter()
            .GetResult();
        string editStoreHashAfter = CanonicalJsonSha256(editStoreAfterPath, "generatedAt");
        SortedDictionary<string, string> projectInputsAfter =
            CaptureTerrainProjectInputHashes(workspace, stoneHill.Key);
        if (!string.Equals(editSignatureAfter, editSignatureBefore, StringComparison.Ordinal) ||
            !string.Equals(editStoreHashAfter, editStoreHashBefore, StringComparison.Ordinal) ||
            !projectInputsAfter.SequenceEqual(projectInputsBefore))
        {
            throw new InvalidOperationException(
                "A pure Playable/Complete view toggle changed the production terrain signature, " +
                "canonical terrain edit-store serialization, or project terrain-export input files.");
        }

        string signatureSha256 = Sha256Utf8(editSignatureBefore);
        string projectInputSetSha256 = Sha256Utf8(string.Join(
            "\n",
            projectInputsBefore.Select(pair => $"{pair.Key}={pair.Value}")));
        string exportInputProofPath = Path.Combine(outputDirectory, "terrain-scene-view-export-input-proof.json");
        File.WriteAllText(exportInputProofPath, JsonSerializer.Serialize(new
        {
            levelKey = stoneHill.Key,
            levelName = stoneHill.DisplayName,
            representativeEditedRuntimeKey = visibleEditingFace.RuntimeKey,
            productionTerrainEditSignatureSha256 = signatureSha256,
            canonicalTerrainEditStoreSha256 = editStoreHashBefore,
            projectTerrainInputSetSha256 = projectInputSetSha256,
            projectTerrainInputs = projectInputsBefore,
            toggles = new[] { "Playable View", "Complete Scene", "Playable View" },
            result = "Production terrain signature, canonical edit-store serialization, and project terrain-export inputs remained identical."
        }, new JsonSerializerOptions { WriteIndented = true }));
        visibleEditingFace.ResetTerrainEdit();
        if (!string.Equals(
                CaptureProductionTerrainEditSignature(geometry),
                baselineEditSignature,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Terrain scene-view export-input proof did not restore its isolated representative edit.");
        }
        Console.WriteLine(
            $"Terrain view export-input proof: production signature {signatureSha256[..12]}, " +
            $"canonical edit store {editStoreHashBefore[..12]}, and {projectInputsBefore.Count} project inputs unchanged; " +
            $"report {exportInputProofPath}.");

        foreach (string levelKey in new[] { "sunnyflight", "beastmakers" })
        {
            LevelDefinition level = catalog.Levels.Single(candidate =>
                candidate.Key.Equals(levelKey, StringComparison.OrdinalIgnoreCase));
            SelectLevelForViewportFit(window, level);
            viewport = window.GetLogicalDescendants().OfType<EditorViewport>().Single();
            geometry = viewport.Geometry
                ?? throw new InvalidOperationException($"{level.DisplayName} geometry was not loaded for Playable View Fit.");
            HashSet<TerrainPolygon> fitFixture = new(
                geometry.Polygons.Take(Math.Max(1, geometry.Polygons.Count / 2)),
                ReferenceEqualityComparer.Instance);
            viewport.PlayableTerrainViewPredicate = (_, polygon) => fitFixture.Contains(polygon);
            viewport.SetTerrainSceneViewMode(TerrainSceneViewMode.Playable);
            viewport.SetViewMode(ViewportViewMode.Map);
            viewport.ResetView();
            FlushUi();

            ViewportFitSnapshot fit = viewport.CaptureFitSnapshotForTesting();
            Moby[] visibleMobys = viewport.Mobys.Where(moby => !moby.IsRemoved).ToArray();
            Moby[] outsideFit = visibleMobys.Where(moby =>
                moby.Position.X < fit.FocusGeometryBounds.Left - 0.01 ||
                moby.Position.X > fit.FocusGeometryBounds.Right + 0.01 ||
                moby.Position.Y < fit.FocusGeometryBounds.Top - 0.01 ||
                moby.Position.Y > fit.FocusGeometryBounds.Bottom + 0.01)
                .ToArray();
            if (visibleMobys.Length == 0 || outsideFit.Length > 0)
            {
                throw new InvalidOperationException(
                    $"{level.DisplayName} Playable View Fit omitted {outsideFit.Length}/{visibleMobys.Length} visible mobys.");
            }
            Console.WriteLine(
                $"{level.DisplayName} Playable View Fit retained all {visibleMobys.Length:N0} visible mobys inside the filtered terrain bounds.");
        }

        Console.WriteLine(
            $"Terrain scene view: Playable View presented {playable.PresentedFaceCount:N0}/{playable.StoredFaceCount:N0} faces, " +
            $"Complete Scene restored all {complete.PresentedFaceCount:N0}; the Geometry instance and export source stayed untouched.");
    }
    finally
    {
        window.Close();
        FlushUi();
    }
}
#pragma warning restore CS8321

void RunTerrainFlyGameViewOnly()
{
    AssertNativeLowDetailTopology();
    MainWindow window = new()
    {
        Width = 1440,
        Height = 900,
        WindowStartupLocation = WindowStartupLocation.Manual,
        Position = new PixelPoint(0, 0)
    };
    window.Show();
    try
    {
        WaitForLevelData(window);
        LevelCatalog catalog = LevelCatalog.Load(sourceWorkspace);
        List<object> captures = [];
        int levelsWithDistanceSuppression = 0;
        int levelsWithLowDetail = 0;
        foreach (LevelDefinition level in catalog.Levels)
        {
            SelectLevelForViewportFit(window, level);
            EditorViewport viewport = window.GetLogicalDescendants().OfType<EditorViewport>().Single();
            viewport.SetViewMode(ViewportViewMode.Map);
            viewport.SetMapYFlipped(true);
            viewport.SetViewMode(ViewportViewMode.Fly3D);
            FlushUi();

            string frame = $"terrain-fly-game-view-{level.Key}.png";
            SaveFrame(window, frame);
            FlyTerrainVisibilitySnapshot gameView =
                viewport.CaptureFlyTerrainVisibilitySnapshotForTesting();
            NativeTerrainOcclusionSnapshot occlusion =
                viewport.CaptureNativeTerrainOcclusionSnapshotForTesting();
            GameCameraEntrySnapshot entryPose =
                viewport.CaptureGameCameraEntrySnapshotForTesting();
            object entryCamera = CaptureFlyCameraState(viewport);
            int distanceSuppressed =
                gameView.SuppressedHighDetailBySectorCount +
                gameView.SuppressedHighDetailByFaceDistanceCount;
            if (distanceSuppressed > 0)
                levelsWithDistanceSuppression++;
            if (gameView.VisibleLowDetailFaceCount > 0)
                levelsWithLowDetail++;

            if (gameView.Mode != FlyTerrainVisibilityMode.GameViewCameraLocalHighDetail ||
                !gameView.GameViewEnabled ||
                gameView.CameraIsOverview ||
                gameView.SectorCount <= 0 ||
                gameView.QueuedHighDetailSectorCount <= 0 ||
                gameView.StoredHighDetailFaceCount <= 0 ||
                gameView.VisibleHighDetailFaceCount <= 0 ||
                gameView.SuppressedHighDetailByGroupCount != 0)
            {
                throw new InvalidOperationException(
                    $"{level.DisplayName} did not enter the all-sector native-distance HP/LP Game Camera path: {gameView}.");
            }
            if (occlusion.VisibleSectorCount != occlusion.StoredSectorCount ||
                occlusion.NativeGroupSectorCount <= 0 ||
                occlusion.NativeGroupSectorCount > occlusion.StoredSectorCount)
            {
                throw new InvalidOperationException(
                    $"{level.DisplayName} did not keep all source sectors eligible while retaining native group provenance: {occlusion}.");
            }
            if (!entryPose.PoseAvailable ||
                !entryPose.Source.Contains("portable retail entry cache", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"{level.DisplayName} did not start from its portable retail entry pose: {entryPose}.");
            }

            if (level.Key.Equals("artisans", StringComparison.OrdinalIgnoreCase))
            {
                string beforeGestureSha256 = Convert.ToHexString(
                    System.Security.Cryptography.SHA256.HashData(
                        File.ReadAllBytes(Path.Combine(outputDirectory, frame))));
                viewport.SimulateFlyNavigationForTesting();
                string activeFrame = "terrain-fly-game-view-artisans-input-active.png";
                SaveFrame(window, activeFrame);
                FlyTerrainInteractiveLodSnapshot activeGesture =
                    viewport.CaptureFlyTerrainInteractiveLodSnapshotForTesting();
                string activeGestureSha256 = Convert.ToHexString(
                    System.Security.Cryptography.SHA256.HashData(
                        File.ReadAllBytes(Path.Combine(outputDirectory, activeFrame))));
                viewport.SettleFlyNavigationForTesting();
                string settledFrame = "terrain-fly-game-view-artisans-input-settled.png";
                SaveFrame(window, settledFrame);
                string settledGestureSha256 = Convert.ToHexString(
                    System.Security.Cryptography.SHA256.HashData(
                        File.ReadAllBytes(Path.Combine(outputDirectory, settledFrame))));
                if (!activeGesture.Active ||
                    activeGesture.SimplifiedMaterialFaceCount != 0 ||
                    activeGesture.FullMaterialFaceCount != activeGesture.VisibleTerrainFaceCount ||
                    !string.Equals(beforeGestureSha256, activeGestureSha256, StringComparison.Ordinal) ||
                    !string.Equals(beforeGestureSha256, settledGestureSha256, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Artisans Game Camera changed material pixels across a camera gesture: " +
                        $"{beforeGestureSha256} -> {activeGestureSha256} -> {settledGestureSha256}; {activeGesture}.");
                }
            }

            if (level.Key.Equals("clifftown", StringComparison.OrdinalIgnoreCase) &&
                (Math.Abs(entryPose.CameraX - 9152.0) > 0.01 ||
                 Math.Abs(entryPose.CameraY - 2591.045) > 0.02 ||
                 Math.Abs(entryPose.CameraZ - 1021.252) > 0.02 ||
                 entryPose.CollisionGroup != 0 ||
                 entryPose.CollisionConstrained))
            {
                throw new InvalidOperationException(
                    $"Cliff Town did not apply the retail spherical third-person entry camera: {entryPose}.");
            }

            if (level.Key.Equals("metalhead", StringComparison.OrdinalIgnoreCase) &&
                (gameView.StoredHighDetailFaceCount != 6800 ||
                 gameView.StoredLowDetailFaceCount != 2899 ||
                 distanceSuppressed <= 0 ||
                 gameView.VisibleLowDetailFaceCount <= 0))
            {
                throw new InvalidOperationException(
                    $"Metal Head did not transition distant outer terrain through the native HP/LP Game Camera path: {gameView}.");
            }

            viewport.ResetView();
            FlushUi();
            FlyTerrainVisibilitySnapshot overview =
                viewport.CaptureFlyTerrainVisibilitySnapshotForTesting();
            if (overview.Mode != FlyTerrainVisibilityMode.EditorOverviewHighDetail ||
                !overview.CameraIsOverview ||
                overview.StoredHighDetailFaceCount != gameView.StoredHighDetailFaceCount ||
                overview.VisibleHighDetailFaceCount <= 0 ||
                overview.SuppressedHighDetailByGroupCount != 0 ||
                overview.SuppressedHighDetailBySectorCount != 0 ||
                overview.SuppressedHighDetailByFaceDistanceCount != 0 ||
                overview.FoggedHighDetailFaceCount != 0 ||
                overview.VisibleLowDetailFaceCount != 0)
            {
                throw new InvalidOperationException(
                    $"{level.DisplayName} Fit did not restore the complete HP editor overview: {overview}.");
            }

            captures.Add(new
            {
                levelKey = level.Key,
                levelName = level.DisplayName,
                frame,
                entryCamera,
                entryPose,
                occlusion,
                gameView,
                overview
            });
            Console.WriteLine(
                $"{level.DisplayName} Game View: HP visible {gameView.VisibleHighDetailFaceCount:N0}/{gameView.StoredHighDetailFaceCount:N0}, " +
                $"local-group-suppressed {gameView.SuppressedHighDetailByGroupCount:N0}, distance-suppressed {distanceSuppressed:N0}, " +
                $"LP visible {gameView.VisibleLowDetailFaceCount:N0}, mobys {gameView.VisibleMobyCount:N0} visible/{gameView.SuppressedFarMobyCount:N0} far-hidden.");
        }

        if (levelsWithDistanceSuppression < 15 || levelsWithLowDetail < 15)
        {
            throw new InvalidOperationException(
                $"All-level Game View coverage was too narrow: distance suppression in {levelsWithDistanceSuppression}/{catalog.Levels.Count}, " +
                $"native low-detail terrain in {levelsWithLowDetail}/{catalog.Levels.Count}.");
        }

        string metricsPath = Path.Combine(outputDirectory, "terrain-fly-game-view-metrics.json");
        File.WriteAllText(metricsPath, JsonSerializer.Serialize(new
        {
            levelCount = catalog.Levels.Count,
            levelsWithDistanceSuppression,
            levelsWithLowDetail,
            hpLpTransitionDistance = NativeTerrainFarLod.LodDistanceEditorUnits,
            contract = "all-unique-source-sectors-native-distance-hp-lp-full-material-v6",
            scope = "Every visible HP face keeps its source material. The retail collision group remains provenance and ordering context while every stored unique sector stays available on the native HP/LP distance queues. Edit Map retains and material-renders every source face.",
            captures
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"All-level Fly Game View metrics: {metricsPath}");
    }
    finally
    {
        window.Close();
        FlushUi();
    }
}

static void AssertNativeLowDetailTopology()
{
    IReadOnlyList<(int A, int B, int C)> triangles =
        EditorViewport.CaptureNativeLowDetailTriangleSlotsForTesting();
    if (!triangles.SequenceEqual(new[] { (0, 1, 2), (3, 1, 2) }))
        throw new InvalidOperationException("Native LP POLY_G4 raster order drifted from [0,1,2]/[3,1,2].");

    (double X, double Y)[] quad = [(0, 0), (0, 1), (1, 0), (1, 1)];
    double quadArea = triangles.Sum(triangle => TriangleArea(
        quad[triangle.A], quad[triangle.B], quad[triangle.C]));
    if (Math.Abs(quadArea - 1.0) > 0.000001)
        throw new InvalidOperationException($"Native LP quad topology covers {quadArea:0.######} instead of exactly one square.");
    for (int y = 0; y < 16; y++)
    {
        for (int x = 0; x < 16; x++)
        {
            (double X, double Y) sample = ((x + 0.31) / 16.0, (y + 0.67) / 16.0);
            int coverage = triangles.Count(triangle => PointInTriangle(
                sample,
                quad[triangle.A],
                quad[triangle.B],
                quad[triangle.C]));
            if (coverage != 1)
            {
                throw new InvalidOperationException(
                    $"Native LP quad sample ({sample.X:0.###},{sample.Y:0.###}) has {coverage}x coverage instead of exactly one primitive.");
            }
        }
    }

    (double X, double Y)[] nativeTriangle = [(0, 0), (0, 1), (1, 0), (1, 0)];
    int nonDegenerate = triangles.Count(triangle => TriangleArea(
        nativeTriangle[triangle.A], nativeTriangle[triangle.B], nativeTriangle[triangle.C]) > 0.000001);
    if (nonDegenerate != 1)
        throw new InvalidOperationException($"Native repeated-slot LP triangle emitted {nonDegenerate} nondegenerate primitives instead of one.");

    static double TriangleArea(
        (double X, double Y) a,
        (double X, double Y) b,
        (double X, double Y) c) =>
        Math.Abs(((b.X - a.X) * (c.Y - a.Y)) - ((b.Y - a.Y) * (c.X - a.X))) * 0.5;

    static bool PointInTriangle(
        (double X, double Y) point,
        (double X, double Y) a,
        (double X, double Y) b,
        (double X, double Y) c)
    {
        double d1 = Cross(point, a, b);
        double d2 = Cross(point, b, c);
        double d3 = Cross(point, c, a);
        bool hasNegative = d1 < -0.0000001 || d2 < -0.0000001 || d3 < -0.0000001;
        bool hasPositive = d1 > 0.0000001 || d2 > 0.0000001 || d3 > 0.0000001;
        return !(hasNegative && hasPositive);

        static double Cross(
            (double X, double Y) p,
            (double X, double Y) q,
            (double X, double Y) r) =>
            ((p.X - r.X) * (q.Y - r.Y)) - ((q.X - r.X) * (p.Y - r.Y));
    }
}

void RunTerrainNativeGteProjectionOnly()
{
    MainWindow window = new()
    {
        Width = 1200,
        Height = 760,
        WindowStartupLocation = WindowStartupLocation.Manual,
        Position = new PixelPoint(0, 0)
    };
    window.Show();
    try
    {
        WaitForLevelData(window);
        LevelCatalog catalog = LevelCatalog.Load(sourceWorkspace);
        List<object> captures = [];
        foreach ((string levelKey, double distance) in new[]
                 {
                     ("stonehill", 420d),
                     ("darkpassage", 650d),
                     ("gnastysworld", 650d)
                 })
        {
            LevelDefinition level = catalog.Levels.Single(candidate =>
                candidate.Key.Equals(levelKey, StringComparison.OrdinalIgnoreCase));
            SelectLevelForViewportFit(window, level);
            EditorViewport viewport = window.GetLogicalDescendants().OfType<EditorViewport>().Single();
            GeometryCandidate geometry = viewport.Geometry
                ?? throw new InvalidOperationException($"{level.DisplayName} geometry was not loaded for the native GTE projection smoke.");
            int anchor = geometry.Polygons.FindIndex(polygon =>
                polygon.HasCompleteNativeHighPolyFacePayload && !polygon.IsNativeUntexturedSentinel);
            if (anchor < 0)
                throw new InvalidOperationException($"{level.DisplayName} has no textured exact HP face for the native GTE projection smoke.");

            viewport.SetNativeGteProjectionExperimentForTesting(true);
            string frame = $"terrain-native-gte-{levelKey}-near.png";
            NativeTerrainBoundedCompositorSnapshot snapshot = RenderBoundedTerrainFixture(
                window,
                viewport,
                anchor,
                distance,
                frame);
            if (snapshot.Width != 512 || snapshot.Height != 224 ||
                !string.Equals(
                    snapshot.NativeProjectionContract,
                    "editor-camera-derived-native-gte-base-points-fixed-512x224-v1",
                    StringComparison.Ordinal) ||
                !string.Equals(
                    snapshot.NativeCameraContract,
                    Spyro.Editor.Core.Rendering.SpyroNativeTerrainCamera.Contract,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    snapshot.NativeCameraSource,
                    Spyro.Editor.Core.Rendering.SpyroEditorDerivedTerrainCamera.Contract,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"{level.DisplayName} native GTE projection did not retain its fixed-frame/camera provenance: {snapshot}.");
            }
            captures.Add(new { levelKey, levelName = level.DisplayName, distance, frame, snapshot });
        }

        string metricsPath = Path.Combine(outputDirectory, "terrain-native-gte-projection-metrics.json");
        File.WriteAllText(metricsPath, JsonSerializer.Serialize(new
        {
            projectionContract = "editor-camera-derived-native-gte-base-points-fixed-512x224-v1",
            captures
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Native GTE terrain projection experiment passed: {metricsPath}");
    }
    finally
    {
        window.Close();
        FlushUi();
    }
}

void RunTerrainNativeRoundTripOnly()
{
    MainWindow window = new()
    {
        Width = 1200,
        Height = 760,
        WindowStartupLocation = WindowStartupLocation.Manual,
        Position = new PixelPoint(0, 0)
    };
    window.Show();
    try
    {
        WaitForLevelData(window);
        Task<string> task = window.AssertNativeTerrainTextureRelocationRoundTripForTestingAsync();
        for (int attempt = 0; attempt < 12000 && !task.IsCompleted; attempt++)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(5);
        }
        if (!task.IsCompleted)
            throw new TimeoutException("The focused native terrain texture stage/reload/Undo UI smoke exceeded 60 seconds.");
        string result = task.GetAwaiter().GetResult();
        Console.WriteLine($"Native terrain texture UI round-trip: {result}.");
    }
    finally
    {
        window.Close();
        FlushUi();
    }
}

void RunTerrainUnreferencedArtOnly()
{
    MainWindow window = new()
    {
        Width = 1200,
        Height = 760,
        WindowStartupLocation = WindowStartupLocation.Manual,
        Position = new PixelPoint(0, 0)
    };
    window.Show();
    try
    {
        WaitForLevelData(window);
        Task<string> task = window.AssertNativeUnreferencedTerrainArtOnlyRoundTripForTestingAsync();
        for (int attempt = 0; attempt < 18000 && !task.IsCompleted; attempt++)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(5);
        }
        if (!task.IsCompleted)
            throw new TimeoutException("The focused native-unreferenced art-only Apply/reload/Undo UI smoke exceeded 90 seconds.");
        string result = task.GetAwaiter().GetResult();
        Console.WriteLine($"Native-unreferenced terrain art-only UI round-trip: {result}.");
    }
    finally
    {
        window.Close();
        FlushUi();
    }
}

void RunTerrainSameLevelRoundTripOnly()
{
    MainWindow window = new()
    {
        Width = 1200,
        Height = 760,
        WindowStartupLocation = WindowStartupLocation.Manual,
        Position = new PixelPoint(0, 0)
    };
    window.Show();
    try
    {
        WaitForLevelData(window);
        Task<string> task = window.AssertNativeTerrainFaceTextureRoundTripForTestingAsync();
        for (int attempt = 0; attempt < 12000 && !task.IsCompleted; attempt++)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(5);
        }
        if (!task.IsCompleted)
            throw new TimeoutException("The focused same-level terrain texture Apply/save/reload/Undo UI smoke exceeded 60 seconds.");
        string result = task.GetAwaiter().GetResult();
        Console.WriteLine($"Same-level terrain texture UI round-trip: {result}.");
    }
    finally
    {
        window.Close();
        FlushUi();
    }
}

void RunTerrainTexturePaintModeOnly()
{
    MainWindow window = new()
    {
        Width = 1200,
        Height = 760,
        WindowStartupLocation = WindowStartupLocation.Manual,
        Position = new PixelPoint(0, 0)
    };
    window.Show();
    try
    {
        WaitForLevelData(window);
        FindButton(window, "Choose Texture & Start Painting");
        Task<string> task = window.AssertTerrainTexturePaintModeForTestingAsync();
        for (int attempt = 0; attempt < 12000 && !task.IsCompleted; attempt++)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(5);
        }
        if (!task.IsCompleted)
            throw new TimeoutException("The focused texture chooser/paint/apply/Undo UI smoke exceeded 60 seconds.");
        string result = task.GetAwaiter().GetResult();
        Console.WriteLine($"Terrain texture paint-mode UI: {result}.");
    }
    finally
    {
        window.Close();
        FlushUi();
    }
}

void RunTerrainCrossLevelPaintOnly()
{
    MainWindow window = new()
    {
        Width = 1200,
        Height = 760,
        WindowStartupLocation = WindowStartupLocation.Manual,
        Position = new PixelPoint(0, 0)
    };
    window.Show();
    try
    {
        WaitForLevelData(window);
        Task<string> task = window.AssertCrossLevelTerrainTexturePaintForTestingAsync();
        for (int attempt = 0; attempt < 24000 && !task.IsCompleted; attempt++)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(5);
        }
        if (!task.IsCompleted)
            throw new TimeoutException("The focused Artisans <- Gnasty's World texture 17 paint smoke exceeded 120 seconds.");
        string result = task.GetAwaiter().GetResult();
        Console.WriteLine($"Cross-level terrain texture paint UI: {result}.");
    }
    finally
    {
        window.Close();
        FlushUi();
    }
}

void RunAppendedPrivateTextureGateOnly()
{
    MainWindow window = new()
    {
        Width = 1200,
        Height = 760,
        WindowStartupLocation = WindowStartupLocation.Manual,
        Position = new PixelPoint(0, 0)
    };
    window.Show();
    try
    {
        WaitForLevelData(window);
        Task<string> task =
            window.AssertAppendedPrivateTerrainTextureGateForTestingAsync();
        for (int attempt = 0; attempt < 36000 && !task.IsCompleted; attempt++)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(5);
        }
        if (!task.IsCompleted)
        {
            throw new TimeoutException(
                "The focused appended-private gate/stage/build-plan/Undo smoke exceeded 180 seconds.");
        }
        string result = task.GetAwaiter().GetResult();
        Console.WriteLine($"Appended-private terrain texture gate UI/build: {result}.");
    }
    finally
    {
        window.Close();
        FlushUi();
    }
}

void RunAppendedPrivateBuildSafetyOnly()
{
    AssertAppendedPrivateBuildSafetyClassification();
    MainWindow window = new()
    {
        Width = 1200,
        Height = 760,
        WindowStartupLocation = WindowStartupLocation.Manual,
        Position = new PixelPoint(0, 0)
    };
    window.Show();
    try
    {
        WaitForLevelData(window);
        AssertAppendedPrivateBuildSafetyTerrainNavigation(window);
    }
    finally
    {
        window.Close();
        FlushUi();
    }
}

void AssertAppendedPrivateBuildSafetyClassification()
{
    LevelDefinition level = LevelCatalog.Load(workspace).FindByKey("artisans")
        ?? throw new InvalidOperationException("Appended-private Build Safety smoke could not find Artisans.");
    NativeTerrainTextureRecordAppendSourceBinding binding = new(
        Version: NativeTerrainTextureRecordAppendBuilder.CurrentBindingVersion,
        SourceImageSha256: new string('a', 64),
        TargetLevelKey: level.Key,
        TargetWadEntry: level.SourceWadEntry,
        ExpectedSourceTextureCount: 68,
        ExpectedTextureComponentSha256: new string('b', 64),
        ExpectedLevelDataSha256: new string('c', 64));
    NativeTerrainTextureRelocationEdit valid = new(
        TargetTextureId: 68,
        DonorLevelKey: "gnastysworld",
        DonorLevelName: "Gnasty's World",
        DonorWadEntry: 31,
        DonorTextureId: 17,
        DonorRuntimeKey: NativeTerrainTextureRelocationEditStore
            .BuildTextureRecordProvenanceKey("gnastysworld", 17),
        DescriptorTier: NativeTerrainTextureRelocationEditStore.CompleteDescriptorTier,
        PreviewImagePath: "",
        PreviewImageName: "",
        CreatedAt: "2026-07-23T00:00:00.0000000Z",
        ApplyMode: NativeTerrainTextureRelocationApplyMode.ArtOnlyPreserveTarget,
        TargetRecordKind: NativeTerrainTextureTargetRecordKind.AppendedPrivate,
        TargetWadEntry: binding.TargetWadEntry,
        SourceImageSha256: binding.SourceImageSha256,
        SourceTextureRecordCount: binding.ExpectedSourceTextureCount,
        SourceTextureComponentSha256: binding.ExpectedTextureComponentSha256,
        SourceLevelDataSha256: binding.ExpectedLevelDataSha256,
        MaterialTemplateTextureId: 55,
        PrivateRecordEditId: NativeTerrainTextureRelocationEditStore
            .BuildAppendedPrivateRecordId(68));
    NativeTerrainTextureRelocationEdit staleOrphan = valid with
    {
        TargetTextureId = 70,
        DonorTextureId = 18,
        DonorRuntimeKey = NativeTerrainTextureRelocationEditStore
            .BuildTextureRecordProvenanceKey("gnastysworld", 18),
        MaterialTemplateTextureId = 31,
        PrivateRecordEditId = "stale-private-id"
    };
    MainWindow.TerrainTextureBuildSafetyFaceSnapshot[] faces =
    [
        new(12, "4:7:hp", 68, 55, false),
        new(13, "4:8:hp", 69, 31, false),
        new(14, "4:9:hp", 31, 31, false)
    ];

    IReadOnlyList<MobyBuildSafetyIssue> issues =
        MainWindow.InspectAppendedPrivateTerrainTextureState(
            level,
            [valid, staleOrphan],
            faces,
            binding,
            manifestError: "",
            geometryError: "",
            bindingError: "",
            gateAllowed: false,
            gateReason: "Normal Beta V4 remains fail-closed.");
    string[] requiredCodes =
    [
        "terrain-appended-private-gate-disallowed-row",
        "terrain-appended-private-manifest-needs-compaction",
        "terrain-appended-private-row-invalid",
        "terrain-appended-private-orphan-row",
        "terrain-appended-private-missing-manifest-row"
    ];
    foreach (string code in requiredCodes)
    {
        if (!issues.Any(issue => string.Equals(issue.Code, code, StringComparison.Ordinal)))
            throw new InvalidOperationException($"Appended-private Build Safety omitted required finding '{code}'.");
    }
    if (issues.Any(issue => issue.Status != MobyBuildSafetyStatus.Blocked))
        throw new InvalidOperationException("An invalid appended-private texture finding was not blocking.");
    NativeTerrainTextureRelocationEdit[] promotedRows = Enumerable.Range(68, 4)
        .Select((textureId, index) => valid with
        {
            TargetTextureId = textureId,
            DonorTextureId = 17 + index,
            DonorRuntimeKey = NativeTerrainTextureRelocationEditStore
                .BuildTextureRecordProvenanceKey("gnastysworld", 17 + index),
            MaterialTemplateTextureId = index == 0 ? 55 : 31,
            PrivateRecordEditId = NativeTerrainTextureRelocationEditStore
                .BuildAppendedPrivateRecordId(textureId)
        })
        .ToArray();
    MainWindow.TerrainTextureBuildSafetyFaceSnapshot[] promotedFaces = promotedRows
        .Select((row, index) => new MainWindow.TerrainTextureBuildSafetyFaceSnapshot(
            12 + index,
            $"4:{7 + index}:hp",
            row.TargetTextureId,
            row.MaterialTemplateTextureId,
            false))
        .ToArray();
    IReadOnlyList<MobyBuildSafetyIssue> promotedIssues =
        MainWindow.InspectAppendedPrivateTerrainTextureState(
            level,
            promotedRows,
            promotedFaces,
            binding,
            manifestError: "",
            geometryError: "",
            bindingError: "",
            gateAllowed: true,
            gateReason:
                "Runtime-proven exact Artisans FixedTail profile matched four appended private records.");
    if (promotedIssues.Count != 0)
    {
        throw new InvalidOperationException(
            $"The valid four-record Artisans promotion produced Build Safety findings: " +
            $"{string.Join(" | ", promotedIssues.Select(issue => $"{issue.Code}: {issue.Message}"))}");
    }
    IReadOnlyList<MobyBuildSafetyIssue> staleIssues =
        MainWindow.InspectAppendedPrivateTerrainTextureState(
            level,
            [valid with { SourceImageSha256 = new string('d', 64) }],
            faces.Take(1).ToArray(),
            binding,
            manifestError: "",
            geometryError: "",
            bindingError: "",
            gateAllowed: false,
            gateReason: "Normal Beta V4 remains fail-closed.");
    if (!staleIssues.Any(issue =>
            string.Equals(
                issue.Code,
                "terrain-appended-private-stale-source-binding",
                StringComparison.Ordinal)))
    {
        throw new InvalidOperationException(
            "Appended-private Build Safety omitted the stale source-preimage finding.");
    }
    MobyBuildSafetyIssue missing = issues.Single(issue =>
        string.Equals(
            issue.Code,
            "terrain-appended-private-missing-manifest-row",
            StringComparison.Ordinal));
    if (missing.EditorTrueIndex != 13 || !string.Equals(missing.MobyLabel, "4:8:hp", StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            "The missing-manifest finding did not target its exact visible terrain section.");
    }

    IReadOnlyList<MobyBuildSafetyIssue> malformed =
        MainWindow.InspectAppendedPrivateTerrainTextureState(
            level,
            [],
            faces,
            binding,
            manifestError: "version-3 row is malformed",
            geometryError: "",
            bindingError: "",
            gateAllowed: false,
            gateReason: "Normal Beta V4 remains fail-closed.");
    if (!malformed.Any(issue =>
            string.Equals(
                issue.Code,
                "terrain-appended-private-manifest-invalid",
                StringComparison.Ordinal)) ||
        !malformed.Any(issue =>
            string.Equals(
                issue.Code,
                "terrain-appended-private-missing-manifest-row",
                StringComparison.Ordinal)))
    {
        throw new InvalidOperationException(
            "Malformed-manifest Build Safety did not retain both the manifest and live future-ID findings.");
    }

    Console.WriteLine(
        $"Appended-private Build Safety classification: the promoted four-record Artisans profile produced zero findings; {issues.Count} blocking issue(s), exact missing-row target, gate, orphan, stale preimage, invalid row, and compaction coverage passed.");
}

void AssertAppendedPrivateBuildSafetyTerrainNavigation(MainWindow owner)
{
    EditorViewport viewport = owner.GetLogicalDescendants()
        .OfType<EditorViewport>()
        .Single();
    GeometryCandidate geometry = viewport.Geometry
        ?? throw new InvalidOperationException("Build Safety terrain navigation has no loaded geometry.");
    TerrainPolygon terrain = geometry.Polygons
        .Where(face => !face.IsTerrainRemoved)
        .OrderBy(face => face.SectorIndex)
        .ThenBy(face => face.FaceIndex)
        .First();
    int terrainIndex = geometry.Polygons.IndexOf(terrain);
    FieldInfo currentLevelField = typeof(MainWindow).GetField(
        "_currentLevel",
        BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Could not inspect the current level for terrain Build Safety navigation.");
    LevelDefinition level = (LevelDefinition)(currentLevelField.GetValue(owner)
        ?? throw new InvalidOperationException("Terrain Build Safety navigation has no current level."));
    MobyBuildSafetyIssue issue = new(
        Code: "terrain-appended-private-ui-navigation",
        Status: MobyBuildSafetyStatus.Blocked,
        Message: "Synthetic appended-private issue used to verify terrain targeting.",
        LevelKey: level.Key,
        LevelName: level.DisplayName,
        EditorTrueIndex: terrainIndex,
        MobyLabel: terrain.RuntimeKey);

    MethodInfo buildRow = typeof(MainWindow).GetMethod(
        "BuildBuildSafetyIssueRow",
        BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Could not find the Build Safety issue-row builder.");
    Window dialog = new()
    {
        Title = "Terrain Build Safety navigation smoke",
        Width = 660,
        Height = 220,
        WindowStartupLocation = WindowStartupLocation.CenterOwner
    };
    Control row = (Control)(buildRow.Invoke(owner, [dialog, issue])
        ?? throw new InvalidOperationException("Build Safety did not create a terrain issue row."));
    dialog.Content = row;
    Task<object?> dialogTask = dialog.ShowDialog<object?>(owner);
    FlushUi();

    AssertText(row, $"Terrain section {terrain.RuntimeKey}");
    AssertTextContains(row, "Double-click to select and center this terrain section");
    if (row.Cursor == null)
        throw new InvalidOperationException("The terrain Build Safety issue was not visibly actionable.");
    SaveFrame(dialog, "spyro-editor-build-safety-terrain-issue.png");

    Point issuePoint = new(30, 25);
    dialog.MouseDown(issuePoint, MouseButton.Left, RawInputModifiers.None);
    dialog.MouseUp(issuePoint, MouseButton.Left, RawInputModifiers.None);
    dialog.MouseDown(issuePoint, MouseButton.Left, RawInputModifiers.None);
    if (!dialogTask.IsCompleted)
        dialog.MouseUp(issuePoint, MouseButton.Left, RawInputModifiers.None);

    FieldInfo selectedTerrainField = typeof(MainWindow).GetField(
        "_selectedTerrain",
        BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Could not inspect selected terrain after Build Safety navigation.");
    TerrainPolygon? selected = null;
    for (int attempt = 0; attempt < 1000; attempt++)
    {
        Dispatcher.UIThread.RunJobs();
        Thread.Sleep(5);
        selected = selectedTerrainField.GetValue(owner) as TerrainPolygon;
        if (string.Equals(selected?.RuntimeKey, terrain.RuntimeKey, StringComparison.OrdinalIgnoreCase))
            break;
    }
    FlushUi();
    if (!dialogTask.IsCompleted ||
        !string.Equals(selected?.RuntimeKey, terrain.RuntimeKey, StringComparison.OrdinalIgnoreCase) ||
        viewport.ViewMode != ViewportViewMode.Map)
    {
        throw new InvalidOperationException(
            $"Double-click did not select and center terrain {terrain.RuntimeKey}; selected '{selected?.RuntimeKey}', view {viewport.ViewMode}.");
    }
    AssertTextContains(
        owner,
        $"Build Safety: selected and centered {level.DisplayName} terrain section {terrain.RuntimeKey}");
    Console.WriteLine(
        $"Appended-private Build Safety navigation: {level.DisplayName} terrain {terrain.RuntimeKey} selected and centered from a double-click.");
}

void RunTerrainTexturePaintGalleryOnly()
{
    MainWindow window = new()
    {
        Width = 1400,
        Height = 900,
        WindowStartupLocation = WindowStartupLocation.Manual,
        Position = new PixelPoint(0, 0)
    };
    List<string> donorLoads = [];
    window.TerrainTextureDonorLoadObserverForTesting = levelKey => donorLoads.Add(levelKey);
    window.Show();
    try
    {
        WaitForLevelData(window);
        TextBlock privateCapacity =
            FindNamed<TextBlock>(window, "TerrainPrivateTextureCapacityText");
        string privateCapacityText = privateCapacity.Text ?? "";
        bool expectedCapacityText = AppendedPrivateTerrainTextureResearchGate.IsEnabled
            ? privateCapacityText.Contains(
                    "Research maximum: 20",
                    StringComparison.OrdinalIgnoreCase) &&
                privateCapacityText.Contains(
                    "not guaranteed physical capacity or normal-release authorization",
                    StringComparison.OrdinalIgnoreCase) &&
                privateCapacityText.Contains(
                    "reused at the maximum without adding a row",
                    StringComparison.OrdinalIgnoreCase)
            : privateCapacityText.Contains(
                    "Single-tile cross-level painting is not available for this level yet",
                    StringComparison.OrdinalIgnoreCase) &&
                privateCapacityText.Contains(
                    "0 of 0 proven native slot(s) remain",
                    StringComparison.OrdinalIgnoreCase) &&
                !privateCapacityText.Contains(
                    "Research maximum: 20",
                    StringComparison.OrdinalIgnoreCase);
        if (!expectedCapacityText)
        {
            throw new InvalidOperationException(
                $"The private-texture capacity panel did not match its normal/research safety mode: {privateCapacityText}");
        }
        int suggestionTargetTextureId =
            window.SelectTerrainTextureSuggestionTargetForTesting();
        Window dialog = OpenAsyncDialog(
            window,
            FindButton(window, "Choose Texture & Start Painting"),
            "Choose Terrain Texture",
            "terrain texture paint gallery");
        FlushUi();

        if (donorLoads.Count != 0)
        {
            throw new InvalidOperationException(
                $"Opening the texture gallery eagerly loaded donor level(s): {string.Join(", ", donorLoads)}.");
        }

        ListBox gallery = FindNamed<ListBox>(dialog, "TerrainTextureGallery");
        ListBox suggestionGallery =
            FindNamed<ListBox>(dialog, "TerrainTextureSuggestionGallery");
        ComboBox sourceLevelPicker = FindNamed<ComboBox>(dialog, "TerrainTextureSourceLevelPicker");
        Button loadLevelButton = FindNamed<Button>(dialog, "TerrainTextureLoadLevelButton");
        Button suggestButton = FindNamed<Button>(dialog, "TerrainTextureSuggestButton");
        Border suggestionPanel =
            FindNamed<Border>(dialog, "TerrainTextureSuggestionPanel");
        if (sourceLevelPicker.SelectedItem is not object currentOption ||
            !TemplateValue<bool>(currentOption, "IsCurrent"))
        {
            throw new InvalidOperationException("The texture gallery did not initially select the current level.");
        }

        LevelDefinition currentLevel = TemplateValue<LevelDefinition>(currentOption, "Level");
        object[] initialItems = ReadItemsSource(gallery, "initial current-level texture gallery");
        if (initialItems.Length == 0)
            throw new InvalidOperationException("The current-level texture gallery opened empty.");
        string normalizedCurrentLevel = LevelCatalog.NormalizeKey(currentLevel.Key);
        object? wrongInitialLevel = initialItems.FirstOrDefault(item =>
            !string.Equals(
                LevelCatalog.NormalizeKey(TemplateValue<string>(item, "LevelKey")),
                normalizedCurrentLevel,
                StringComparison.OrdinalIgnoreCase));
        if (wrongInitialLevel != null)
        {
            throw new InvalidOperationException(
                $"The initial gallery included {TemplateValue<string>(wrongInitialLevel, "LevelKey")} while {currentLevel.DisplayName} was loaded.");
        }

        dialog.InvalidateMeasure();
        dialog.InvalidateArrange();
        dialog.InvalidateVisual();
        FlushUi();
        if (gallery.ItemsPanelRoot is not UniformGrid grid || grid.Columns != 6)
        {
            throw new InvalidOperationException(
                $"The texture gallery did not render through a six-column UniformGrid (found {gallery.ItemsPanelRoot?.GetType().Name ?? "no panel"}).");
        }
        SaveFrame(dialog, "spyro-editor-terrain-texture-gallery.png");

        if (!suggestButton.IsEnabled)
            throw new InvalidOperationException("The nearby-tile suggestion button stayed disabled with a selected terrain section.");
        suggestButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, suggestButton));
        FlushUi();
        object[] suggestedItems = ReadItemsSource(
            suggestionGallery,
            "nearby terrain texture suggestions");
        if (!suggestionPanel.IsVisible ||
            suggestedItems.Length is < 4 or > 6 ||
            suggestedItems.Any(item => TemplateValue<bool>(item, "IsBlocked")) ||
            suggestedItems.Any(item =>
                string.Equals(
                    LevelCatalog.NormalizeKey(TemplateValue<string>(item, "LevelKey")),
                    normalizedCurrentLevel,
                    StringComparison.OrdinalIgnoreCase) &&
                TemplateValue<int>(item, "TextureId") == suggestionTargetTextureId))
        {
            throw new InvalidOperationException(
                "Suggest Nearby Tiles did not show four-to-six usable alternatives or included the selected source texture.");
        }
        SaveFrame(dialog, "spyro-editor-terrain-texture-suggestions.png");
        suggestionGallery.SelectedItem = suggestedItems[0];
        FlushUi();
        if (!window.OwnedWindows.Contains(dialog))
        {
            throw new InvalidOperationException(
                "Single-click-style selection in the suggestion row activated painting; suggestions must also require a mouse double-click.");
        }

        object[] blockedCurrentItems = initialItems
            .Where(item => TemplateValue<bool>(item, "IsBlocked"))
            .ToArray();
        if (blockedCurrentItems.Length != 0)
        {
            throw new InvalidOperationException(
                $"The clean current-level gallery still blocked {blockedCurrentItems.Length:N0} load-initialized native art record(s). Face-less/controller records must be exposed as copy-only sources, not spare targets.");
        }

        if (sourceLevelPicker.ItemsSource is not System.Collections.IEnumerable sourceOptions)
            throw new InvalidOperationException("The source-level picker did not expose its level options.");
        object[] sourceOptionItems = sourceOptions.Cast<object>().ToArray();
        object donorOption = sourceOptionItems
            .FirstOrDefault(option =>
                string.Equals(
                    LevelCatalog.NormalizeKey(TemplateValue<LevelDefinition>(option, "Level").Key),
                    "gnastysworld",
                    StringComparison.OrdinalIgnoreCase))
            ?? sourceOptionItems.FirstOrDefault(option => !TemplateValue<bool>(option, "IsCurrent"))
            ?? throw new InvalidOperationException("The source-level picker exposed no other level.");
        LevelDefinition donorLevel = TemplateValue<LevelDefinition>(donorOption, "Level");
        sourceLevelPicker.SelectedItem = donorOption;
        FlushUi();
        if (donorLoads.Count != 0)
        {
            throw new InvalidOperationException(
                $"Selecting {donorLevel.DisplayName} loaded donor data before the explicit button click: {string.Join(", ", donorLoads)}.");
        }
        if (ReadItemsSource(gallery, "unloaded donor texture gallery").Length != 0)
            throw new InvalidOperationException("Selecting another source level retained or eagerly populated texture tiles before Load Level Textures.");

        loadLevelButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, loadLevelButton));
        for (int attempt = 0; attempt < 12000; attempt++)
        {
            Dispatcher.UIThread.RunJobs();
            if (loadLevelButton.IsEnabled && donorLoads.Count > 0)
                break;
            Thread.Sleep(5);
        }
        FlushUi();

        string normalizedDonorLevel = LevelCatalog.NormalizeKey(donorLevel.Key);
        if (donorLoads.Count != 1 ||
            !string.Equals(donorLoads[0], normalizedDonorLevel, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Loading {donorLevel.DisplayName} touched {donorLoads.Count} donor level(s): {string.Join(", ", donorLoads)}.");
        }
        object[] donorItems = ReadItemsSource(gallery, $"{donorLevel.DisplayName} texture gallery");
        if (donorItems.Length == 0 || donorItems.Any(item =>
                !string.Equals(
                    LevelCatalog.NormalizeKey(TemplateValue<string>(item, "LevelKey")),
                    normalizedDonorLevel,
                    StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"The explicit {donorLevel.DisplayName} load did not produce a non-empty gallery containing only that level.");
        }

        object secondDonorOption = sourceOptionItems
            .FirstOrDefault(option =>
                string.Equals(
                    LevelCatalog.NormalizeKey(TemplateValue<LevelDefinition>(option, "Level").Key),
                    "darkhollow",
                    StringComparison.OrdinalIgnoreCase))
            ?? sourceOptionItems.FirstOrDefault(option =>
                !TemplateValue<bool>(option, "IsCurrent") &&
                !ReferenceEquals(option, donorOption))
            ?? throw new InvalidOperationException("The source-level picker exposed no second donor level.");
        LevelDefinition secondDonorLevel = TemplateValue<LevelDefinition>(secondDonorOption, "Level");
        sourceLevelPicker.SelectedItem = secondDonorOption;
        FlushUi();
        if (donorLoads.Count != 1 || ReadItemsSource(gallery, "unloaded second donor texture gallery").Length != 0)
        {
            throw new InvalidOperationException(
                $"Selecting {secondDonorLevel.DisplayName} either loaded it eagerly or retained the previous donor's tiles.");
        }
        loadLevelButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, loadLevelButton));
        for (int attempt = 0; attempt < 12000; attempt++)
        {
            Dispatcher.UIThread.RunJobs();
            if (loadLevelButton.IsEnabled && donorLoads.Count > 1)
                break;
            Thread.Sleep(5);
        }
        FlushUi();
        if (donorLoads.Count != 2)
        {
            throw new InvalidOperationException(
                $"Loading {secondDonorLevel.DisplayName} touched {donorLoads.Count} donor level(s) instead of exactly two independently loaded donors.");
        }
        object[] secondDonorItems = ReadItemsSource(gallery, $"{secondDonorLevel.DisplayName} texture gallery");
        if (secondDonorItems.Length == 0)
            throw new InvalidOperationException($"{secondDonorLevel.DisplayName} loaded an empty texture gallery.");

        sourceLevelPicker.SelectedItem = donorOption;
        FlushUi();
        object[] restoredFirstDonorItems = ReadItemsSource(gallery, $"restored {donorLevel.DisplayName} texture gallery");
        if (donorLoads.Count != 2 || restoredFirstDonorItems.Length != donorItems.Length)
        {
            throw new InvalidOperationException(
                $"Switching back to {donorLevel.DisplayName} reloaded it or lost its {donorItems.Length:N0}-tile session cache.");
        }

        object usableItem = restoredFirstDonorItems.FirstOrDefault(item => !TemplateValue<bool>(item, "IsBlocked"))
            ?? throw new InvalidOperationException($"{donorLevel.DisplayName} exposed no usable tile for double-click activation.");
        EditorViewport viewport = window.GetLogicalDescendants().OfType<EditorViewport>().Single();
        gallery.SelectedItem = usableItem;
        FlushUi();
        if (!window.OwnedWindows.Contains(dialog) || viewport.TerrainTexturePaintMode)
        {
            throw new InvalidOperationException(
                "Selecting a texture tile started painting; selection must only update the palette details.");
        }
        if (!MainWindow.IsStrictTerrainTextureGalleryDoubleClick(
                PointerType.Mouse,
                isPrimaryPointer: true,
                clickCount: 2,
                isLeftButtonPressed: true) ||
            MainWindow.IsStrictTerrainTextureGalleryDoubleClick(
                PointerType.Mouse,
                isPrimaryPointer: true,
                clickCount: 1,
                isLeftButtonPressed: true) ||
            MainWindow.IsStrictTerrainTextureGalleryDoubleClick(
                PointerType.Mouse,
                isPrimaryPointer: true,
                clickCount: 3,
                isLeftButtonPressed: true) ||
            MainWindow.IsStrictTerrainTextureGalleryDoubleClick(
                PointerType.Mouse,
                isPrimaryPointer: false,
                clickCount: 2,
                isLeftButtonPressed: true) ||
            MainWindow.IsStrictTerrainTextureGalleryDoubleClick(
                PointerType.Mouse,
                isPrimaryPointer: true,
                clickCount: 2,
                isLeftButtonPressed: false) ||
            MainWindow.IsStrictTerrainTextureGalleryDoubleClick(
                PointerType.Touch,
                isPrimaryPointer: true,
                clickCount: 2,
                isLeftButtonPressed: true) ||
            MainWindow.IsStrictTerrainTextureGalleryDoubleClick(
                PointerType.Pen,
                isPrimaryPointer: true,
                clickCount: 2,
                isLeftButtonPressed: true))
        {
            throw new InvalidOperationException(
                "The texture gallery activation gate accepted a single/triple/right/non-primary/touch/pen gesture or rejected a primary left-mouse double-click.");
        }
        ListBoxItem usableContainer = dialog.GetLogicalDescendants()
            .OfType<ListBoxItem>()
            .FirstOrDefault(item => ReferenceEquals(item.DataContext, usableItem))
            ?? throw new InvalidOperationException(
                "The selected usable texture tile had no realized ListBoxItem for its keyboard activation guard.");
        if (!usableContainer.Focus())
            throw new InvalidOperationException("The selected texture tile could not receive keyboard focus for its activation guard.");
        FlushUi();
        dialog.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, "\r");
        dialog.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
        FlushUi();
        if (!window.OwnedWindows.Contains(dialog) || viewport.TerrainTexturePaintMode)
        {
            throw new InvalidOperationException(
                "Enter or Space activated a selected texture; keyboard input must remain selection/details-only.");
        }
        Border usableTile = dialog.GetLogicalDescendants()
            .OfType<Border>()
            .FirstOrDefault(tile =>
                tile.Classes.Contains("terrain-texture-card") &&
                ReferenceEquals(tile.DataContext, usableItem))
            ?? throw new InvalidOperationException("The selected usable texture tile was not realized for double-click activation.");
        Point? translatedCenter = usableTile.TranslatePoint(
            new Point(usableTile.Bounds.Width / 2, usableTile.Bounds.Height / 2),
            dialog);
        if (translatedCenter is not Point activationPoint)
            throw new InvalidOperationException("The usable texture tile could not be located in the chooser dialog.");

        dialog.MouseDown(activationPoint, MouseButton.Left, RawInputModifiers.None);
        dialog.MouseUp(activationPoint, MouseButton.Left, RawInputModifiers.None);
        FlushUi();
        if (!window.OwnedWindows.Contains(dialog) || viewport.TerrainTexturePaintMode)
        {
            throw new InvalidOperationException(
                "A single texture-tile pointer/tap started painting; texture activation must require a deliberate double-click.");
        }
        dialog.MouseDown(activationPoint, MouseButton.Left, RawInputModifiers.None);
        if (window.OwnedWindows.Contains(dialog))
            dialog.MouseUp(activationPoint, MouseButton.Left, RawInputModifiers.None);
        for (int attempt = 0; attempt < 1000 && window.OwnedWindows.Contains(dialog); attempt++)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(5);
        }
        FlushUi();
        if (window.OwnedWindows.Contains(dialog) || !viewport.TerrainTexturePaintMode)
            throw new InvalidOperationException("Double-clicking a usable texture tile did not close the chooser and enter Texture Paint mode.");

        Button returnToPalette = FindButton(window, "Return to Texture Palette");
        if (!returnToPalette.IsEnabled)
            throw new InvalidOperationException("The active Texture Paint panel did not enable Return to Texture Palette.");
        int selectedTextureId = TemplateValue<int>(usableItem, "TextureId");
        Window returnedDialog = OpenAsyncDialog(
            window,
            returnToPalette,
            "Choose Terrain Texture",
            "retained terrain texture palette");
        FlushUi();

        if (viewport.TerrainTexturePaintMode)
            throw new InvalidOperationException("Returning to the texture palette left terrain-click interception active behind the dialog.");
        if (donorLoads.Count != 2)
        {
            throw new InvalidOperationException(
                $"Returning to {donorLevel.DisplayName} reloaded donor data {donorLoads.Count} time(s) instead of retaining the loaded catalog.");
        }

        ComboBox returnedSourcePicker = FindNamed<ComboBox>(returnedDialog, "TerrainTextureSourceLevelPicker");
        if (returnedSourcePicker.SelectedItem is not object returnedSourceOption)
            throw new InvalidOperationException("The returned texture palette did not retain a selected source level.");
        LevelDefinition returnedSourceLevel = TemplateValue<LevelDefinition>(returnedSourceOption, "Level");
        if (!string.Equals(
                LevelCatalog.NormalizeKey(returnedSourceLevel.Key),
                normalizedDonorLevel,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Return to Texture Palette opened {returnedSourceLevel.DisplayName} instead of retained source {donorLevel.DisplayName}.");
        }

        ListBox returnedGallery = FindNamed<ListBox>(returnedDialog, "TerrainTextureGallery");
        object[] returnedItems = ReadItemsSource(returnedGallery, "returned donor texture gallery");
        if (returnedItems.Length != donorItems.Length || returnedItems.Any(item =>
                !string.Equals(
                    LevelCatalog.NormalizeKey(TemplateValue<string>(item, "LevelKey")),
                    normalizedDonorLevel,
                    StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                "Return to Texture Palette did not restore the already-loaded donor-only gallery.");
        }
        if (returnedGallery.SelectedItem is not object returnedSelection ||
            TemplateValue<int>(returnedSelection, "TextureId") != selectedTextureId)
        {
            throw new InvalidOperationException(
                "Return to Texture Palette did not restore the active donor texture selection.");
        }

        Button cancelReturnedPalette = FindButton(returnedDialog, "Cancel");
        cancelReturnedPalette.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, cancelReturnedPalette));
        for (int attempt = 0; attempt < 1000 &&
             (window.OwnedWindows.Contains(returnedDialog) || !viewport.TerrainTexturePaintMode); attempt++)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(5);
        }
        FlushUi();
        if (window.OwnedWindows.Contains(returnedDialog) ||
            !viewport.TerrainTexturePaintMode ||
            donorLoads.Count != 2)
        {
            throw new InvalidOperationException(
                "Canceling the retained palette did not resume the previous brush without reloading its donor.");
        }

        Button stopPainting = FindButton(window, "Stop Painting");
        stopPainting.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, stopPainting));
        FlushUi();
        if (viewport.TerrainTexturePaintMode)
            throw new InvalidOperationException("Stop Painting did not leave texture paint mode before the session-cache check.");

        Window reopenedDialog = OpenAsyncDialog(
            window,
            FindButton(window, "Choose Texture & Start Painting"),
            "Choose Terrain Texture",
            "session-cached terrain texture palette");
        FlushUi();
        if (donorLoads.Count != 2)
        {
            throw new InvalidOperationException(
                $"Reopening the palette in the same level reloaded donor data {donorLoads.Count} time(s) instead of retaining the working-session cache.");
        }
        ComboBox reopenedSourcePicker = FindNamed<ComboBox>(reopenedDialog, "TerrainTextureSourceLevelPicker");
        if (reopenedSourcePicker.SelectedItem is not object reopenedSourceOption ||
            !string.Equals(
                LevelCatalog.NormalizeKey(TemplateValue<LevelDefinition>(reopenedSourceOption, "Level").Key),
                normalizedDonorLevel,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Reopening the palette in the same level did not retain the last loaded donor world.");
        }
        ListBox reopenedGallery = FindNamed<ListBox>(reopenedDialog, "TerrainTextureGallery");
        object[] reopenedItems = ReadItemsSource(reopenedGallery, "session-cached donor texture gallery");
        if (reopenedItems.Length != donorItems.Length)
        {
            throw new InvalidOperationException(
                $"The session-cached donor gallery retained {reopenedItems.Length}/{donorItems.Length} texture tiles.");
        }
        Button cancelReopenedPalette = FindButton(reopenedDialog, "Cancel");
        cancelReopenedPalette.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, cancelReopenedPalette));
        for (int attempt = 0; attempt < 1000 && window.OwnedWindows.Contains(reopenedDialog); attempt++)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(5);
        }
        FlushUi();
        if (window.OwnedWindows.Contains(reopenedDialog) || viewport.TerrainTexturePaintMode)
        {
            throw new InvalidOperationException(
                "Canceling the freshly reopened session palette unexpectedly resumed paint mode.");
        }

        Window manageDialog = OpenAsyncDialog(
            window,
            FindButton(window, "Manage Added Textures"),
            "Manage Staged Cross-Level Textures",
            "staged cross-level texture manager");
        FlushUi();
        TextBlock emptyManagerSummary = manageDialog.GetLogicalDescendants()
            .OfType<TextBlock>()
            .FirstOrDefault(text =>
                (text.Text ?? "").Contains(
                    "No cross-level texture records are staged",
                    StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                "The staged texture manager did not explain its empty state.");
        _ = emptyManagerSummary;
        if (FindButton(manageDialog, "Remove Selected Staged Texture").IsEnabled ||
            FindButton(manageDialog, "Remove Most Recently Staged Target").IsEnabled ||
            FindButton(manageDialog, "Undo Last Texture Action").IsEnabled)
        {
            throw new InvalidOperationException(
                "The empty staged texture manager enabled a destructive or unavailable history action.");
        }
        Button closeManager = FindButton(manageDialog, "Close");
        closeManager.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, closeManager));
        for (int attempt = 0; attempt < 1000 && window.OwnedWindows.Contains(manageDialog); attempt++)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(5);
        }
        FlushUi();
        if (window.OwnedWindows.Contains(manageDialog))
            throw new InvalidOperationException("The staged texture manager did not close cleanly.");

        Console.WriteLine(
            $"Terrain texture gallery UI: {currentLevel.DisplayName}-only initial open; six columns; every load-initialized resident/face-less/controller record exposed as direct or copy-only art; " +
            "Suggest Nearby Tiles returned four-to-six usable local matches without activating on selection; " +
            $"{donorLevel.DisplayName} and {secondDonorLevel.DisplayName} remained unloaded until individually requested, then both stayed cached; selection, Enter/Space, touch/pen, and a single pointer/tap stayed non-mutating while only a primary left-mouse double-click entered paint mode; " +
            "Return to Texture Palette and a later same-level reopen restored the donor world, tile, and catalog without another donor load; Manage Staged Cross-Level Textures opened with safe empty-state actions disabled.");
    }
    finally
    {
        window.TerrainTextureDonorLoadObserverForTesting = null;
        foreach (Window owned in window.OwnedWindows.ToArray())
            owned.Close();
        window.Close();
        FlushUi();
    }
}

void RunUpdateOnly()
{
    string userDataRoot = Path.Combine(
        Path.GetTempPath(),
        $"spyro-editor-ui-update-{Guid.NewGuid():N}");
    string installRoot = Path.Combine(
        Path.GetTempPath(),
        $"spyro-editor-ui-install-{Guid.NewGuid():N}");
    string supportRoot = Path.Combine(installRoot, "support");
    Directory.CreateDirectory(supportRoot);
    File.Copy(
        Path.Combine(workspace, "spyro-level-catalog.json"),
        Path.Combine(supportRoot, "spyro-level-catalog.json"));
    File.Copy(
        Path.Combine(workspace, "spyro-object-templates.json"),
        Path.Combine(supportRoot, "spyro-object-templates.json"));
    string? previousInstallRoot = Environment.GetEnvironmentVariable("SPYRO_EDITOR_INSTALL_ROOT");
    string? previousDataRoot = Environment.GetEnvironmentVariable(EditorUserDataLayout.DataRootEnvironmentVariable);
    string? previousProjectsRoot = Environment.GetEnvironmentVariable(EditorUserDataLayout.ProjectsRootEnvironmentVariable);
    string? previousUpdateWorkspace = Environment.GetEnvironmentVariable(ReleaseProjectBootstrap.WorkspaceEnvironmentVariable);
    Environment.SetEnvironmentVariable("SPYRO_EDITOR_INSTALL_ROOT", installRoot);
    Environment.SetEnvironmentVariable(EditorUserDataLayout.DataRootEnvironmentVariable, userDataRoot);
    Environment.SetEnvironmentVariable(
        EditorUserDataLayout.ProjectsRootEnvironmentVariable,
        Path.Combine(userDataRoot, "Projects"));
    Environment.SetEnvironmentVariable(
        ReleaseProjectBootstrap.WorkspaceEnvironmentVariable,
        previousUpdateWorkspace);
    ReleaseProjectContext context = Task.Run(() => ReleaseProjectBootstrap.PrepareAsync(
        "Spyro Editor Beta V2 UI smoke",
        explicitInstallRoot: installRoot,
        forceReleaseMode: true)).GetAwaiter().GetResult()
        ?? throw new InvalidOperationException("The focused update UI smoke did not initialize release project storage.");
    string legacyState = JsonSerializer.Serialize(new
    {
        schemaVersion = 2,
        lastCheckedUtc = DateTimeOffset.UtcNow,
        availableBetaVersion = 3,
        availableDisplayName = "Spyro Editor Beta V3",
        releaseUrl = "https://github.com/monty19933-hub/Spyro1Editor/releases/tag/beta-v3",
        lastAutoNotificationBetaVersion = 2,
        lastDownloadedBetaVersion = 2,
        lastDownloadedAtUtc = (DateTimeOffset?)null
    });
    JsonNode migratedState = JsonNode.Parse(MainWindow.MigrateUpdateStateJsonForTesting(legacyState))
        ?? throw new InvalidOperationException("The update-state migration returned empty JSON.");
    if (migratedState["schemaVersion"]?.GetValue<int>() != 3 ||
        migratedState["availablePublicVersion"]?.GetValue<string>() != "3" ||
        migratedState["lastAutoNotificationPublicVersion"]?.GetValue<string>() != "2" ||
        migratedState["lastDownloadedPublicVersion"]?.GetValue<string>() != "2" ||
        migratedState["availableBetaVersion"] != null)
    {
        throw new InvalidOperationException("Schema-2 integer update state did not migrate to canonical schema-3 strings.");
    }

    File.WriteAllText(
        Path.Combine(context.UserData.SettingsPath, "update-check.json"),
        JsonSerializer.Serialize(new
        {
            schemaVersion = 3,
            lastCheckedUtc = DateTimeOffset.UtcNow,
            availablePublicVersion = "",
            availableDisplayName = "",
            releaseUrl = "",
            lastAutoNotificationPublicVersion = "",
            lastDownloadedPublicVersion = "",
            lastDownloadedAtUtc = (DateTimeOffset?)null
        }));

    MainWindow window = new()
    {
        Width = 1200,
        Height = 760,
        WindowStartupLocation = WindowStartupLocation.Manual,
        Position = new PixelPoint(0, 0)
    };
    window.Show();
    try
    {
        WaitForLevelData(window);
        RenderPreviousBetaProjectReminder(window);
        RenderUpdateNotification(window);
    }
    finally
    {
        window.Close();
        FlushUi();
        Environment.SetEnvironmentVariable("SPYRO_EDITOR_INSTALL_ROOT", previousInstallRoot);
        Environment.SetEnvironmentVariable(EditorUserDataLayout.DataRootEnvironmentVariable, previousDataRoot);
        Environment.SetEnvironmentVariable(EditorUserDataLayout.ProjectsRootEnvironmentVariable, previousProjectsRoot);
        Environment.SetEnvironmentVariable(ReleaseProjectBootstrap.WorkspaceEnvironmentVariable, previousUpdateWorkspace);
        TryDeleteDirectory(userDataRoot);
        TryDeleteDirectory(installRoot);
    }
}

void RunViewportFitOnly()
{
    MainWindow window = new()
    {
        Width = 1440,
        Height = 900,
        WindowStartupLocation = WindowStartupLocation.Manual,
        Position = new PixelPoint(0, 0)
    };
    window.Show();
    try
    {
        WaitForLevelData(window);
        var catalog = LevelCatalog.Load(sourceWorkspace);
        List<object> lodPreviewMetrics = [];
        foreach (string levelKey in new[] { "stonehill", "clifftown", "darkpassage", "dreamweavers", "gnastysworld" })
        {
            LevelDefinition level = catalog.Levels.Single(candidate =>
                candidate.Key.Equals(levelKey, StringComparison.OrdinalIgnoreCase));
            SelectLevelForViewportFit(window, level);
            EditorViewport viewport = window.GetLogicalDescendants().OfType<EditorViewport>().Single();
            viewport.SetViewMode(ViewportViewMode.Fly3D);
            viewport.ResetView();
            FlushUi();

            if (levelKey.Equals("stonehill", StringComparison.OrdinalIgnoreCase))
            {
                AssertFineFlyHeightNavigation(window, viewport);
                viewport.ResetView();
                FlushUi();
            }

            ViewportFitSnapshot snapshot = viewport.CaptureFitSnapshotForTesting();
            AssertViewportFit(level.DisplayName, snapshot);
            NativeTerrainFarLodSnapshot overview = viewport.CaptureNativeTerrainFarLodSnapshotForTesting();
            if (overview.PreviewMode != NativeTerrainLodPreviewMode.EditorOverviewHighDetail ||
                !overview.HasStaticLowDetailPreview ||
                !overview.Contract.Equals(SourceSceneOverlayContract.TerrainLodPreview, StringComparison.Ordinal) ||
                overview.SectorCount <= 0 || overview.StoredLowDetailFaces <= 0 ||
                overview.VisibleHighDetailFaces <= 0 || overview.VisibleLowDetailFaces != 0 ||
                overview.SuppressedHighDetailFaces != 0 ||
                overview.VisibleHighDetailFacesAtOrBeyondCutoff <= 0)
            {
                throw new InvalidOperationException(
                    $"{level.DisplayName} Fit did not preserve the explicit all-HP editor overview: {overview}.");
            }

            string overviewFrame = $"spyro-editor-{levelKey}-fly-fit.png";
            System.Diagnostics.Stopwatch overviewRenderTimer = System.Diagnostics.Stopwatch.StartNew();
            SaveFrame(window, overviewFrame);
            overviewRenderTimer.Stop();

            // Keyboard/wheel/right-drag navigation must not expose the bounded
            // native-distance research renderer from the impossible whole-level
            // Fit pose or switch visual quality while input is active. The
            // identical source-material frame must remain before, during, and
            // after a camera gesture.
            viewport.SimulateFlyNavigationForTesting();
            if (viewport.CaptureNativeTerrainFarLodSnapshotForTesting().PreviewMode !=
                NativeTerrainLodPreviewMode.EditorOverviewHighDetail)
            {
                throw new InvalidOperationException(
                    $"{level.DisplayName} Fly navigation escaped the coherent high-detail editor renderer.");
            }
            string protectedNavigationFrame = $"spyro-editor-{levelKey}-fly-navigation-protected.png";
            System.Diagnostics.Stopwatch interactiveRenderTimer = System.Diagnostics.Stopwatch.StartNew();
            SaveFrame(window, protectedNavigationFrame);
            interactiveRenderTimer.Stop();
            FlyTerrainInteractiveLodSnapshot interactiveMaterialLod =
                viewport.CaptureFlyTerrainInteractiveLodSnapshotForTesting();
            if (!interactiveMaterialLod.Active ||
                interactiveMaterialLod.VisibleTerrainFaceCount <= 0 ||
                interactiveMaterialLod.SimplifiedMaterialFaceCount != 0 ||
                interactiveMaterialLod.FullMaterialFaceCount !=
                    interactiveMaterialLod.VisibleTerrainFaceCount ||
                interactiveMaterialLod.PartiallyFullMaterialSectorCount != 0)
            {
                throw new InvalidOperationException(
                    $"{level.DisplayName} Fly navigation changed source-material quality while input was active: " +
                    $"{interactiveMaterialLod}.");
            }
            string overviewSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                File.ReadAllBytes(Path.Combine(outputDirectory, overviewFrame))));
            string protectedNavigationSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                File.ReadAllBytes(Path.Combine(outputDirectory, protectedNavigationFrame))));
            if (!string.Equals(overviewSha256, protectedNavigationSha256, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"{level.DisplayName} Fly navigation changed pixels when the gesture began: " +
                    $"{overviewSha256} -> {protectedNavigationSha256}; {interactiveMaterialLod}.");
            }

            viewport.SettleFlyNavigationForTesting();
            string settledNavigationFrame = $"spyro-editor-{levelKey}-fly-navigation-settled.png";
            System.Diagnostics.Stopwatch settledRenderTimer = System.Diagnostics.Stopwatch.StartNew();
            SaveFrame(window, settledNavigationFrame);
            settledRenderTimer.Stop();
            FlyTerrainInteractiveLodSnapshot settledMaterialLod =
                viewport.CaptureFlyTerrainInteractiveLodSnapshotForTesting();
            string settledNavigationSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                File.ReadAllBytes(Path.Combine(outputDirectory, settledNavigationFrame))));
            if (settledMaterialLod.Active ||
                settledMaterialLod.SimplifiedMaterialFaceCount != 0 ||
                settledMaterialLod.FullMaterialFaceCount != settledMaterialLod.VisibleTerrainFaceCount ||
                !string.Equals(overviewSha256, settledNavigationSha256, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"{level.DisplayName} Fly navigation did not restore the identical full-HQ frame: " +
                    $"{overviewSha256} -> {settledNavigationSha256}; {settledMaterialLod}.");
            }
            viewport.SimulateMissedFlyNavigationReleaseForTesting();
            if (viewport.CaptureFlyTerrainInteractiveLodSnapshotForTesting().Active)
            {
                throw new InvalidOperationException(
                    $"{level.DisplayName} Fly material watchdog did not recover from a missed KeyUp.");
            }

            // The incomplete native-distance renderer remains available only
            // to focused research smokes at an explicit game-scale camera.
            viewport.EnableNativeTerrainDistanceForTesting();

            (int anchorIndex, double distance, NativeTerrainFarLodSnapshot nativeLod) =
                SelectNativeLodProofPose(viewport);
            FlushUi();
            if (nativeLod.PreviewMode != NativeTerrainLodPreviewMode.NativeDistance ||
                nativeLod.VisibleHighDetailFaces <= 0 || nativeLod.VisibleLowDetailFaces <= 0 ||
                nativeLod.SuppressedHighDetailFaces <= 0 ||
                nativeLod.VisibleHighDetailFacesAtOrBeyondCutoff != 0 ||
                nativeLod.DistinctVisibleLowDetailCornerColors <= 1)
            {
                throw new InvalidOperationException(
                    $"{level.DisplayName} game-scale camera did not prove the bounded native HP/LP transition: {nativeLod}.");
            }
            string nativeLodFrame = $"spyro-editor-{levelKey}-native-lod-game-scale.png";
            SaveFrame(window, nativeLodFrame);

            lodPreviewMetrics.Add(new
            {
                levelKey,
                levelName = level.DisplayName,
                contract = overview.Contract,
                overview = new
                {
                    mode = overview.PreviewMode.ToString(),
                    overview.VisibleHighDetailFaces,
                    overview.VisibleLowDetailFaces,
                    overview.SuppressedHighDetailFaces,
                    overview.VisibleHighDetailFacesAtOrBeyondCutoff,
                    frame = overviewFrame,
                    protectedNavigationFrame,
                    protectedNavigationSha256,
                    settledNavigationFrame,
                    settledNavigationSha256,
                    interactiveMaterialLod,
                    settledMaterialLod,
                    overviewCaptureMilliseconds = overviewRenderTimer.Elapsed.TotalMilliseconds,
                    interactiveCaptureMilliseconds = interactiveRenderTimer.Elapsed.TotalMilliseconds,
                    settledCaptureMilliseconds = settledRenderTimer.Elapsed.TotalMilliseconds
                },
                nativeLod = new
                {
                    mode = nativeLod.PreviewMode.ToString(),
                    anchorIndex,
                    distance,
                    nativeLod.SectorCount,
                    nativeLod.StoredHighDetailFaces,
                    nativeLod.StoredLowDetailFaces,
                    nativeLod.QueuedHighDetailSectors,
                    nativeLod.QueuedLowDetailSectors,
                    nativeLod.VisibleHighDetailFaces,
                    nativeLod.VisibleLowDetailFaces,
                    nativeLod.SuppressedHighDetailFaces,
                    nativeLod.VisibleHighDetailFacesAtOrBeyondCutoff,
                    nativeLod.VisibleLowDetailOnlyFaces,
                    nativeLod.VisibleForcedLowDetailFaces,
                    nativeLod.DistinctVisibleLowDetailCornerColors,
                    nativeLod.HqOverlayEligibleFaces,
                    nativeLod.LqDescriptorZeroFaces,
                    nativeLod.LqDescriptorOneFaces,
                    nativeLod.MinimumLqPaletteRow,
                    nativeLod.MaximumLqPaletteRow,
                    frame = nativeLodFrame
                }
            });
            Console.WriteLine(
                $"{level.DisplayName} viewport fit: focus {snapshot.FocusGeometryBounds.Width:0}x{snapshot.FocusGeometryBounds.Height:0} " +
                $"from full {snapshot.FullGeometryBounds.Width:0}x{snapshot.FullGeometryBounds.Height:0}; " +
                $"Fly {snapshot.FlyProjectedBounds.Width / snapshot.ViewportBounds.Width:P0}x{snapshot.FlyProjectedBounds.Height / snapshot.ViewportBounds.Height:P0}, " +
                $"Map {snapshot.MapProjectedBounds.Width / snapshot.ViewportBounds.Width:P0}x{snapshot.MapProjectedBounds.Height / snapshot.ViewportBounds.Height:P0}; " +
                $"pitch {snapshot.Pitch * 180 / Math.PI:0.0} degrees; {snapshot.FocusPolygonCount}/{snapshot.SourcePolygonCount} drawable faces drive Fit; " +
                $"overview HP/LP {overview.VisibleHighDetailFaces:N0}/{overview.VisibleLowDetailFaces:N0}; " +
                $"interactive full/simplified {interactiveMaterialLod.FullMaterialFaceCount:N0}/{interactiveMaterialLod.SimplifiedMaterialFaceCount:N0}; " +
                $"capture full/interactive {overviewRenderTimer.Elapsed.TotalMilliseconds:0}/{interactiveRenderTimer.Elapsed.TotalMilliseconds:0} ms; " +
                $"game-scale native HP/LP {nativeLod.VisibleHighDetailFaces:N0}/{nativeLod.VisibleLowDetailFaces:N0}, " +
                $"suppressed HP {nativeLod.SuppressedHighDetailFaces:N0}, " +
                $"LP colors {nativeLod.DistinctVisibleLowDetailCornerColors:N0}.");

            viewport.ResetView();
            if (viewport.CaptureNativeTerrainFarLodSnapshotForTesting().PreviewMode !=
                NativeTerrainLodPreviewMode.EditorOverviewHighDetail)
            {
                throw new InvalidOperationException($"{level.DisplayName} Reset did not restore the all-HP editor overview.");
            }
        }

        string farLodMetricsPath = Path.Combine(outputDirectory, "terrain-far-lod-fit-metrics.json");
        File.WriteAllText(farLodMetricsPath, JsonSerializer.Serialize(new
        {
            contract = SourceSceneOverlayContract.TerrainLodPreview,
            fitSemantics = "Fit/Reset is an editor-only all-HP overview. Active keyboard, wheel, right-drag look, and focused-object navigation retain identical source-material pixels before, during, and after input. Native distance LOD remains research-only.",
            nativeLodScope = "Static all-sector game-scale Fly 3D preview; exact sector and face distance gates; no runtime occlusion-group, PSX ordering-table, clipping, or animation equivalence claim.",
            hpCutoffEditorUnits = NativeTerrainFarLod.LodDistanceEditorUnits,
            sectorOverlapEditorUnits = NativeTerrainFarLod.SectorQueueOverlapEditorUnits,
            levels = lodPreviewMetrics
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Editor-overview and native terrain LOD metrics: {farLodMetricsPath}");
    }
    finally
    {
        window.Close();
        FlushUi();
    }
}

void AssertFineFlyHeightNavigation(Window window, EditorViewport viewport)
{
    viewport.Focus();
    FlushUi();

    (double X, double Y, double Z) before = CaptureFlyCameraPosition(viewport);
    window.KeyPress(Key.Q, RawInputModifiers.None, PhysicalKey.Q, "q");
    window.KeyRelease(Key.Q, RawInputModifiers.None, PhysicalKey.Q, "q");
    FlushUi();
    (double X, double Y, double Z) afterUp = CaptureFlyCameraPosition(viewport);

    if (Math.Abs(afterUp.X - before.X) > 0.001 ||
        Math.Abs(afterUp.Y - before.Y) > 0.001 ||
        Math.Abs((afterUp.Z - before.Z) - EditorViewport.FlyVerticalMoveStep) > 0.001)
    {
        throw new InvalidOperationException(
            $"Fly Q fine-height input moved the camera incorrectly: before {before}, after {afterUp}, " +
            $"expected +{EditorViewport.FlyVerticalMoveStep:0.###} Z only.");
    }

    window.KeyPress(Key.E, RawInputModifiers.None, PhysicalKey.E, "e");
    window.KeyRelease(Key.E, RawInputModifiers.None, PhysicalKey.E, "e");
    FlushUi();
    (double X, double Y, double Z) afterDown = CaptureFlyCameraPosition(viewport);
    if (Math.Abs(afterDown.X - before.X) > 0.001 ||
        Math.Abs(afterDown.Y - before.Y) > 0.001 ||
        Math.Abs(afterDown.Z - before.Z) > 0.001)
    {
        throw new InvalidOperationException(
            $"Fly E fine-height input did not return the camera exactly: before {before}, after {afterDown}.");
    }

    window.KeyPress(Key.W, RawInputModifiers.None, PhysicalKey.W, "w");
    window.KeyRelease(Key.W, RawInputModifiers.None, PhysicalKey.W, "w");
    FlushUi();
    (double X, double Y, double Z) afterForward = CaptureFlyCameraPosition(viewport);
    double planarDistance = Math.Sqrt(
        Math.Pow(afterForward.X - before.X, 2) +
        Math.Pow(afterForward.Y - before.Y, 2));
    if (Math.Abs(planarDistance - EditorViewport.FlyPlanarMoveStep) > 0.001 ||
        Math.Abs(afterForward.Z - before.Z) > 0.001)
    {
        throw new InvalidOperationException(
            $"Fly fine-height input changed planar navigation: distance {planarDistance:0.###}, " +
            $"Z delta {afterForward.Z - before.Z:0.###}.");
    }

    Console.WriteLine(
        $"Fly fine-height navigation: Q +{EditorViewport.FlyVerticalMoveStep:0} Z, " +
        $"E -{EditorViewport.FlyVerticalMoveStep:0} Z, W remains {EditorViewport.FlyPlanarMoveStep:0} planar units.");
}

static (double X, double Y, double Z) CaptureFlyCameraPosition(EditorViewport viewport)
{
    FieldInfo cameraField = typeof(EditorViewport).GetField(
        "_flyCamera",
        BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Could not inspect the Fly 3D camera for fine-height navigation.");
    object camera = cameraField.GetValue(viewport)
        ?? throw new InvalidOperationException("The Fly 3D camera was not initialized.");
    Type cameraType = camera.GetType();

    double Read(string propertyName) =>
        (double)(cameraType.GetProperty(propertyName)?.GetValue(camera)
            ?? throw new InvalidOperationException($"The Fly 3D camera has no {propertyName} coordinate."));

    return (Read("X"), Read("Y"), Read("Z"));
}

static object CaptureFlyCameraState(EditorViewport viewport)
{
    FieldInfo cameraField = typeof(EditorViewport).GetField(
        "_flyCamera",
        BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Could not inspect the Game Camera state.");
    object camera = cameraField.GetValue(viewport)
        ?? throw new InvalidOperationException("The Game Camera was not initialized.");
    Type cameraType = camera.GetType();

    double Read(string propertyName) =>
        (double)(cameraType.GetProperty(propertyName)?.GetValue(camera)
            ?? throw new InvalidOperationException($"The Game Camera has no {propertyName} value."));

    return new
    {
        x = Read("X"),
        y = Read("Y"),
        z = Read("Z"),
        yaw = Read("Yaw"),
        pitch = Read("Pitch")
    };
}

void RunTerrainDepthCueOnly()
{
    AssertNativeTerrainDepthCueFormula();
    MainWindow window = new()
    {
        Width = 1440,
        Height = 900,
        WindowStartupLocation = WindowStartupLocation.Manual,
        Position = new PixelPoint(0, 0)
    };
    window.Show();
    try
    {
        WaitForLevelData(window);
        LevelCatalog catalog = LevelCatalog.Load(sourceWorkspace);
        List<object> metrics = new();
        foreach (string levelKey in new[] { "stonehill", "darkpassage", "dreamweavers", "gnastysworld" })
        {
            LevelDefinition level = catalog.Levels.Single(candidate =>
                candidate.Key.Equals(levelKey, StringComparison.OrdinalIgnoreCase));
            SelectLevelForViewportFit(window, level);
            EditorViewport viewport = window.GetLogicalDescendants().OfType<EditorViewport>().Single();
            viewport.SetViewMode(ViewportViewMode.Fly3D);
            viewport.AimFlyCameraAtTerrainForTesting(SelectDepthCueAnchor(viewport), 1400);

            viewport.SetNativeTerrainDepthCueForTesting(false);
            FlushUi();
            string beforeName = $"terrain-depth-cue-before-{levelKey}.png";
            SaveFrame(window, beforeName);

            viewport.SetNativeTerrainDepthCueForTesting(true);
            FlushUi();
            NativeTerrainDepthCueSnapshot snapshot = viewport.CaptureNativeTerrainDepthCueSnapshotForTesting();
            string afterName = $"terrain-depth-cue-after-{levelKey}.png";
            SaveFrame(window, afterName);

            if (snapshot.VisibleFaceCount <= 0 || snapshot.VisibleCornerCount <= 0 ||
                snapshot.DistinctEndpointCornerCount <= 0 || snapshot.BlendedCornerCount <= 0 ||
                snapshot.ChangedFromLegacyCornerCount <= 0)
            {
                throw new InvalidOperationException(
                    $"{level.DisplayName} did not exercise native depth cueing: {snapshot}.");
            }

            string beforeHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                File.ReadAllBytes(Path.Combine(outputDirectory, beforeName))));
            string afterHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                File.ReadAllBytes(Path.Combine(outputDirectory, afterName))));
            if (beforeHash == afterHash)
                throw new InvalidOperationException($"{level.DisplayName} depth-cued frame is pixel-identical to the legacy near-only frame.");

            metrics.Add(new
            {
                levelKey,
                levelName = level.DisplayName,
                snapshot.VisibleFaceCount,
                snapshot.VisibleCornerCount,
                snapshot.DistinctEndpointCornerCount,
                snapshot.NearEndpointCornerCount,
                snapshot.BlendedCornerCount,
                snapshot.FarEndpointCornerCount,
                snapshot.ChangedFromLegacyCornerCount,
                snapshot.AverageNearWeight,
                snapshot.MeanChannelDeltaFromLegacy,
                snapshot.MinCameraDepth,
                snapshot.MaxCameraDepth,
                beforeFrame = beforeName,
                afterFrame = afterName,
                beforeSha256 = beforeHash,
                afterSha256 = afterHash
            });

            Console.WriteLine(
                $"{level.DisplayName} native depth cue: {snapshot.VisibleFaceCount} visible face(s), " +
                $"{snapshot.VisibleCornerCount} corner(s), endpoints near/blend/far " +
                $"{snapshot.NearEndpointCornerCount}/{snapshot.BlendedCornerCount}/{snapshot.FarEndpointCornerCount}, " +
                $"{snapshot.ChangedFromLegacyCornerCount} changed from legacy, " +
                $"mean channel delta {snapshot.MeanChannelDeltaFromLegacy:0.00}, " +
                $"mean near weight {snapshot.AverageNearWeight:P1}, " +
                $"camera depth {snapshot.MinCameraDepth:0.0}..{snapshot.MaxCameraDepth:0.0}; " +
                $"SHA {beforeHash[..12]} -> {afterHash[..12]}.");
        }

        string metricsPath = Path.Combine(outputDirectory, "terrain-depth-cue-metrics.json");
        File.WriteAllText(metricsPath, JsonSerializer.Serialize(new
        {
            rendererEvidence = "Spyro 1 retail r_environment: table1 near, table2 far; DPCS IR0=(0x2000-GTE_SZ)/0x1000 clamped",
            editorDepthScale = "GTE_SZ = editorCameraDepth * 4",
            editorRamp = new
            {
                nearThrough = EditorViewport.NativeTerrainDepthCueNearEnd,
                farAt = EditorViewport.NativeTerrainDepthCueFarEnd
            },
            faceDepthUse = "ordering-table bias only; excluded from color interpolation",
            levels = metrics
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Native terrain depth-cue metrics: {metricsPath}");
    }
    finally
    {
        window.Close();
        FlushUi();
    }
}

void RunTerrainTextureLodOnly()
{
    MainWindow window = new()
    {
        Width = 1200,
        Height = 760,
        WindowStartupLocation = WindowStartupLocation.Manual,
        Position = new PixelPoint(0, 0)
    };
    window.Show();
    try
    {
        WaitForLevelData(window);
        LevelCatalog catalog = LevelCatalog.Load(sourceWorkspace);
        LevelDefinition artisans = catalog.Levels.Single(level =>
            level.Key.Equals("artisans", StringComparison.OrdinalIgnoreCase));
        SelectLevelForViewportFit(window, artisans);
        EditorViewport viewport = window.GetLogicalDescendants().OfType<EditorViewport>().Single();

        string normalPath = viewport.ResolveTerrainTextureImagePathForTesting(0, 80)
            ?? throw new InvalidOperationException("Artisans texture 0 normal HQ cache frame was not loaded.");
        string closePath = viewport.ResolveTerrainTextureImagePathForTesting(0, 79.999)
            ?? throw new InvalidOperationException("Artisans texture 0 close HQ cache frame was not loaded.");
        if (!Path.GetFileName(normalPath).Equals("artisans-texture-000-normal.png", StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFileName(closePath).Equals("artisans-texture-000-close.png", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalPath, closePath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Native terrain LOD boundary selected unexpected files: normal={normalPath}, close={closePath}.");
        }

        string initializedScrollNormal = viewport.ResolveTerrainTextureImagePathForTesting(23, 80)
            ?? throw new InvalidOperationException("Artisans initialized scrolling texture 23 normal frame was not loaded.");
        string initializedScrollClose = viewport.ResolveTerrainTextureImagePathForTesting(23, 79.999)
            ?? throw new InvalidOperationException("Artisans initialized scrolling texture 23 close frame was not loaded.");
        if (!Path.GetFileName(initializedScrollNormal).Equals("artisans-texture-023-normal.png", StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFileName(initializedScrollClose).Equals("artisans-texture-023-close.png", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(initializedScrollNormal, initializedScrollClose, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Initialized Artisans scrolling preview selected unexpected files: normal={initializedScrollNormal}, close={initializedScrollClose}.");
        }

        AssertRuntimeTextureManifestFailsClosed(workspace, "artisans", "runtimeControlInitialStateComplete");
        AssertRuntimeTextureManifestFailsClosed(workspace, "artisans", "initializationMutations");
        AssertRuntimeTextureManifestFailsClosed(workspace, "artisans", "runtimeInitializationApplied");

        AssertNativeLqTextureFrames(window, catalog);
        SelectLevelForViewportFit(window, artisans);
        viewport = window.GetLogicalDescendants().OfType<EditorViewport>().Single();

        viewport.SetTerrainTextureImageFiles(
            new Dictionary<int, string>
            {
                [900] = normalPath,
                [901] = normalPath
            },
            new Dictionary<int, string>());
        string fallbackPath = viewport.ResolveTerrainTextureImagePathForTesting(900, 1)
            ?? throw new InvalidOperationException("Missing close HQ frame did not fall back to the available normal frame.");
        if (!string.Equals(fallbackPath, normalPath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Close HQ fallback selected {fallbackPath}, expected {normalPath}.");
        if (!viewport.LoadTerrainTextureImageForTesting(900, 80) ||
            !viewport.LoadTerrainTextureImageForTesting(901, 80))
        {
            throw new InvalidOperationException(
                "Texture cache regression setup did not load both test textures.");
        }
        viewport.SetTerrainTextureImageFiles(
            new Dictionary<int, string>
            {
                [900] = normalPath,
                [901] = normalPath
            },
            new Dictionary<int, string>());
        IReadOnlyList<(int TextureId, NativeTerrainTexturePreviewTier Tier)> stableCache =
            viewport.GetLoadedTerrainTextureImageCacheKeysForTesting();
        if (!stableCache.Any(key => key.TextureId == 900) ||
            !stableCache.Any(key => key.TextureId == 901))
        {
            throw new InvalidOperationException(
                "An unchanged texture-file refresh flushed warm viewport bitmaps.");
        }
        viewport.SetTerrainTextureImageFiles(
            new Dictionary<int, string>
            {
                [900] = closePath,
                [901] = normalPath
            },
            new Dictionary<int, string>());
        IReadOnlyList<(int TextureId, NativeTerrainTexturePreviewTier Tier)> deltaCache =
            viewport.GetLoadedTerrainTextureImageCacheKeysForTesting();
        if (deltaCache.Any(key => key.TextureId == 900) ||
            !deltaCache.Any(key => key.TextureId == 901))
        {
            throw new InvalidOperationException(
                "A one-texture preview change did not invalidate exactly its target bitmap.");
        }
        string overwritePath = Path.Combine(
            outputDirectory,
            "terrain-texture-in-place-overwrite-regression.png");
        try
        {
            File.Copy(normalPath, overwritePath, overwrite: true);
            viewport.SetTerrainTextureImageFiles(
                new Dictionary<int, string> { [902] = overwritePath },
                new Dictionary<int, string>());
            if (!viewport.LoadTerrainTextureImageForTesting(902, 80))
            {
                throw new InvalidOperationException(
                    "In-place texture overwrite regression setup did not load its test bitmap.");
            }

            DateTime firstWrite = File.GetLastWriteTimeUtc(overwritePath);
            File.Copy(closePath, overwritePath, overwrite: true);
            File.SetLastWriteTimeUtc(overwritePath, firstWrite.AddSeconds(2));
            viewport.SetTerrainTextureImageFiles(
                new Dictionary<int, string> { [902] = overwritePath },
                new Dictionary<int, string>());
            if (viewport.GetLoadedTerrainTextureImageCacheKeysForTesting()
                .Any(key => key.TextureId == 902))
            {
                throw new InvalidOperationException(
                    "Overwriting a texture PNG at the same path retained a stale viewport bitmap.");
            }
        }
        finally
        {
            if (File.Exists(overwritePath))
                File.Delete(overwritePath);
        }

        List<object> runtimePreviewCaptures = [];
        foreach ((string levelKey, int controlledTextureId, int? diagnosticSourceId) in new[]
                 {
                     ("artisans", 23, (int?)null),
                     ("magiccrafters", 61, (int?)64),
                     ("treetops", 0, (int?)3),
                     ("hauntedtowers", 83, (int?)86)
                 })
        {
            LevelDefinition level = catalog.Levels.Single(candidate =>
                candidate.Key.Equals(levelKey, StringComparison.OrdinalIgnoreCase));
            SelectLevelForViewportFit(window, level);
            viewport = window.GetLogicalDescendants().OfType<EditorViewport>().Single();
            viewport.SetViewMode(ViewportViewMode.Fly3D);
            viewport.ResetView();
            FlushUi();

            string controlledNormal = viewport.ResolveTerrainTextureImagePathForTesting(controlledTextureId, 80)
                ?? throw new InvalidOperationException($"{level.DisplayName} controlled texture {controlledTextureId} normal initialized frame was not loaded.");
            string controlledClose = viewport.ResolveTerrainTextureImagePathForTesting(controlledTextureId, 79.999)
                ?? throw new InvalidOperationException($"{level.DisplayName} controlled texture {controlledTextureId} close initialized frame was not loaded.");
            if (diagnosticSourceId.HasValue &&
                (viewport.ResolveTerrainTextureImagePathForTesting(diagnosticSourceId.Value, 80) == null ||
                 viewport.ResolveTerrainTextureImagePathForTesting(diagnosticSourceId.Value, 79.999) == null))
            {
                throw new InvalidOperationException(
                    $"{level.DisplayName} otherwise-unreferenced animation source {diagnosticSourceId.Value} was not exported in both tiers.");
            }

            string captureName = $"runtime-initial-texture-preview-{levelKey}.png";
            SaveFrame(window, captureName);
            string capturePath = Path.Combine(outputDirectory, captureName);
            string captureSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(capturePath)));
            runtimePreviewCaptures.Add(new
            {
                levelKey,
                levelName = level.DisplayName,
                controlledTextureId,
                diagnosticSourceId,
                controlledNormal = Path.GetFileName(controlledNormal),
                controlledClose = Path.GetFileName(controlledClose),
                captureName,
                captureSha256
            });
        }
        string captureReport = Path.Combine(outputDirectory, "runtime-initial-texture-preview-captures.json");
        File.WriteAllText(captureReport, JsonSerializer.Serialize(new
        {
            stateSemantics = PortableEditorCacheBuilder.TerrainTexturePreviewStateSemantics,
            playbackIncluded = false,
            currentGameplayState = false,
            captures = runtimePreviewCaptures
        }, new JsonSerializerOptions { WriteIndented = true }));

        Console.WriteLine(
            $"Native terrain texture LOD UI: 79.999 -> {Path.GetFileName(closePath)}, " +
            $"80.000 -> {Path.GetFileName(normalPath)}; initialized scrolling texture 23 resolves both tiers; " +
            "missing global/mutation/per-entry initialization proof fails closed; exact-tier fallback passed. " +
            $"Representative initial-state captures: {captureReport}");
    }
    finally
    {
        window.Close();
        FlushUi();
    }
}

void RunTerrainBoundedCompositorOnly()
{
    MainWindow window = new()
    {
        Width = 1200,
        Height = 760,
        WindowStartupLocation = WindowStartupLocation.Manual,
        Position = new PixelPoint(0, 0)
    };
    window.Show();
    try
    {
        WaitForLevelData(window);
        LevelCatalog catalog = LevelCatalog.Load(sourceWorkspace);
        List<object> captures = [];
        NativeTerrainOrderingUiInventory nativeOrderingInventory =
            AssertNativeTerrainOrderingUiInventory(sourceWorkspace);

        LevelDefinition stoneHill = catalog.Levels.Single(level =>
            level.Key.Equals("stonehill", StringComparison.OrdinalIgnoreCase));
        SelectLevelForViewportFit(window, stoneHill);
        EditorViewport viewport = window.GetLogicalDescendants().OfType<EditorViewport>().Single();
        NativeTerrainBoundedTileSeamProof normalSeam = viewport.CaptureNativeTerrainBoundedTileSeamProofForTesting(2, 32);
        NativeTerrainBoundedTileSeamProof closeSeam = viewport.CaptureNativeTerrainBoundedTileSeamProofForTesting(4, 32);
        NativeTerrainBoundedTileSeamProof normalSeamRepeat = viewport.CaptureNativeTerrainBoundedTileSeamProofForTesting(2, 32);
        NativeTerrainBoundedTileSeamProof closeSeamRepeat = viewport.CaptureNativeTerrainBoundedTileSeamProofForTesting(4, 32);
        foreach (NativeTerrainBoundedTileSeamProof proof in new[] { normalSeam, closeSeam })
        {
            if (proof.BackgroundPixelCount != 0 || proof.MismatchedPixelCount != 0 ||
                string.IsNullOrWhiteSpace(proof.Rgb5Sha256))
            {
                throw new InvalidOperationException($"HQ descriptor-local tile clipping left a seam or sampled the wrong composite tile: {proof}.");
            }
        }
        if (normalSeam.Rgb5Sha256 != normalSeamRepeat.Rgb5Sha256 ||
            normalSeam.CommandCount != normalSeamRepeat.CommandCount ||
            closeSeam.Rgb5Sha256 != closeSeamRepeat.Rgb5Sha256 ||
            closeSeam.CommandCount != closeSeamRepeat.CommandCount)
        {
            throw new InvalidOperationException("HQ descriptor-local 2x2/4x4 clipping is not deterministic across identical composites.");
        }
        GeometryCandidate stoneHillGeometry = viewport.Geometry
            ?? throw new InvalidOperationException("Stone Hill geometry was not loaded for the bounded compositor smoke.");
        int stoneHillAnchor = stoneHillGeometry.Polygons.FindIndex(polygon =>
            polygon.HasNativeHighPolyMaterialPayload && !polygon.IsNativeUntexturedSentinel);
        if (stoneHillAnchor < 0)
            throw new InvalidOperationException("Stone Hill has no textured native HP face for the bounded compositor smoke.");

        NativeTerrainBoundedCompositorSnapshot stoneNear = RenderBoundedTerrainFixture(
            window,
            viewport,
            stoneHillAnchor,
            420,
            "terrain-bounded-stonehill-near.png");
        if (stoneNear.HighQualityFaceCount <= 0 || stoneNear.LowQualityTriangleCount <= 0)
            throw new InvalidOperationException($"Stone Hill near pose did not exercise LQ plus HQ overlay commands: {stoneNear}.");
        captures.Add(new { levelKey = stoneHill.Key, pose = "near-hq", snapshot = stoneNear, frame = "terrain-bounded-stonehill-near.png" });

        NativeTerrainBoundedCompositorSnapshot? stoneMixed = null;
        double stoneMixedDistance = 0;
        foreach (double distance in new[] { 1700d, 1850d, 2000d, 2150d })
        {
            NativeTerrainBoundedCompositorSnapshot candidate = RenderBoundedTerrainFixture(
                window,
                viewport,
                stoneHillAnchor,
                distance,
                "terrain-bounded-stonehill-mixed.png",
                saveFrame: false);
            if (candidate.LowPolyFaceCount > 0 && candidate.TexturedHighPolyFaceCount > 0)
            {
                stoneMixed = candidate;
                stoneMixedDistance = distance;
                break;
            }
        }
        if (stoneMixed == null)
            throw new InvalidOperationException("Stone Hill did not produce a mixed HP/LP bounded frame at the focused native-distance smoke poses.");
        SaveFrame(window, "terrain-bounded-stonehill-mixed.png");
        captures.Add(new { levelKey = stoneHill.Key, pose = "mixed-hp-lp", distance = stoneMixedDistance, snapshot = stoneMixed, frame = "terrain-bounded-stonehill-mixed.png" });

        LevelDefinition alpineRidge = catalog.Levels.Single(level =>
            level.Key.Equals("alpineridge", StringComparison.OrdinalIgnoreCase));
        SelectLevelForViewportFit(window, alpineRidge);
        viewport = window.GetLogicalDescendants().OfType<EditorViewport>().Single();
        GeometryCandidate alpineGeometry = viewport.Geometry
            ?? throw new InvalidOperationException("Alpine Ridge geometry was not loaded for the bounded compositor smoke.");
        SceneSectorRenderMetadata? forcedSector = alpineGeometry.SourceSectors.FirstOrDefault(sector => sector.ForceLowDetail);
        int alpineAnchor = forcedSector != null
            ? alpineGeometry.Polygons.FindIndex(polygon => polygon.SectorIndex == forcedSector.SectorIndex)
            : alpineGeometry.Polygons.FindIndex(polygon => polygon.HasNativeHighPolyMaterialPayload);
        NativeTerrainBoundedCompositorSnapshot alpine = RenderBoundedTerrainFixture(
            window,
            viewport,
            Math.Max(0, alpineAnchor),
            2050,
            "terrain-bounded-alpineridge-lp.png");
        if (alpine.LowPolyFaceCount <= 0 || alpine.LowPolyTriangleCount <= 0)
            throw new InvalidOperationException($"Alpine Ridge did not exercise forced/biased LP commands: {alpine}.");
        captures.Add(new { levelKey = alpineRidge.Key, pose = "forced-or-biased-lp", forcedSector = forcedSector?.SectorIndex, snapshot = alpine, frame = "terrain-bounded-alpineridge-lp.png" });

        LevelDefinition artisans = catalog.Levels.Single(level =>
            level.Key.Equals("artisans", StringComparison.OrdinalIgnoreCase));
        SelectLevelForViewportFit(window, artisans);
        viewport = window.GetLogicalDescendants().OfType<EditorViewport>().Single();
        GeometryCandidate artisansGeometry = viewport.Geometry
            ?? throw new InvalidOperationException("Artisans geometry was not loaded for the bounded compositor smoke.");
        int sentinelAnchor = artisansGeometry.Polygons.FindIndex(polygon => polygon.IsNativeUntexturedSentinel);
        if (sentinelAnchor < 0)
            throw new InvalidOperationException("Artisans has no 0xFF native untextured HP sentinel fixture.");
        NativeTerrainBoundedCompositorSnapshot sentinel = RenderBoundedTerrainFixture(
            window,
            viewport,
            sentinelAnchor,
            420,
            "terrain-bounded-artisans-sentinel.png");
        if (sentinel.SentinelHighPolyFaceCount <= 0)
            throw new InvalidOperationException($"Artisans did not exercise the 0xFF untextured HP sentinel command path: {sentinel}.");
        captures.Add(new { levelKey = artisans.Key, pose = "untextured-ff-sentinel", snapshot = sentinel, frame = "terrain-bounded-artisans-sentinel.png" });

        LevelDefinition twilightHarbor = catalog.Levels.Single(level =>
            level.Key.Equals("twilightharbor", StringComparison.OrdinalIgnoreCase));
        SelectLevelForViewportFit(window, twilightHarbor);
        viewport = window.GetLogicalDescendants().OfType<EditorViewport>().Single();
        GeometryCandidate twilightGeometry = viewport.Geometry
            ?? throw new InvalidOperationException("Twilight Harbor geometry was not loaded for the bounded compositor smoke.");
        int twilightAnchor = twilightGeometry.Polygons.FindIndex(polygon =>
            polygon.NativeTextureId == 8 && polygon.NativePrimitiveSemiTransparent);
        if (twilightAnchor < 0)
            throw new InvalidOperationException("Twilight Harbor has no native semitransparent texture-8 HP fixture.");
        NativeTerrainBoundedCompositorSnapshot twilight = RenderBoundedTerrainFixture(
            window,
            viewport,
            twilightAnchor,
            420,
            "terrain-bounded-twilightharbor-abr1.png");
        if (twilight.RasterStatistics == null || twilight.RasterStatistics.SemiTransparentWrites <= 0 ||
            twilight.RasterStatistics.Abr1Writes <= 0)
        {
            throw new InvalidOperationException($"Twilight Harbor texture 8 did not exercise STP-gated ABR1 writes: {twilight}.");
        }
        captures.Add(new { levelKey = twilightHarbor.Key, pose = "texture-8-stp-abr1", snapshot = twilight, frame = "terrain-bounded-twilightharbor-abr1.png" });

        foreach (string noLpLevelKey in new[] { "beastmakers", "mistybog" })
        {
            LevelDefinition noLpLevel = catalog.Levels.Single(level =>
                level.Key.Equals(noLpLevelKey, StringComparison.OrdinalIgnoreCase));
            SelectLevelForViewportFit(window, noLpLevel);
            viewport = window.GetLogicalDescendants().OfType<EditorViewport>().Single();
            GeometryCandidate geometry = viewport.Geometry
                ?? throw new InvalidOperationException($"{noLpLevel.DisplayName} geometry was not loaded for the bounded compositor smoke.");
            if (geometry.LowDetailPolygons.Count != 0)
                throw new InvalidOperationException($"{noLpLevel.DisplayName} unexpectedly has LP geometry in the no-LP fixture.");
            int anchor = geometry.Polygons.FindIndex(polygon => polygon.HasNativeHighPolyMaterialPayload && !polygon.IsNativeUntexturedSentinel);
            string frame = $"terrain-bounded-{noLpLevelKey}-retained-lq15.png";
            NativeTerrainBoundedCompositorSnapshot retained = RenderBoundedTerrainFixture(
                window,
                viewport,
                Math.Max(0, anchor),
                2400,
                frame);
            if (retained.LowPolyFaceCount != 0 || retained.TexturedHighPolyFaceCount <= 0 ||
                retained.LowQualityTriangleCount <= 0 || retained.HighQualityTriangleCount != 0 ||
                retained.MaximumLqPaletteRow != 15 || retained.LqPaletteRow15FaceCount <= 0)
            {
                throw new InvalidOperationException(
                    $"{noLpLevel.DisplayName} did not retain far HP on TexLq palette row 15 without LP: {retained}.");
            }
            captures.Add(new { levelKey = noLpLevel.Key, pose = "no-lp-retained-lq15", snapshot = retained, frame });
        }

        // Focused whole-frame fail-closed checks. These setters model the exact
        // compatibility decisions made by MainWindow for project material state.
        SelectLevelForViewportFit(window, stoneHill);
        viewport = window.GetLogicalDescendants().OfType<EditorViewport>().Single();
        viewport.SetViewMode(ViewportViewMode.Fly3D);
        viewport.ResetView();
        FlushUi();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
        NativeTerrainBoundedCompositorSnapshot fitFallback = viewport.CaptureNativeTerrainBoundedCompositorSnapshotForTesting();
        if (fitFallback.Used || !fitFallback.FallbackReason.Contains("Fit", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Fit/editor-overview did not retain the whole-frame legacy fallback: {fitFallback}.");

        List<object> fallbacks = [new { kind = "fit", snapshot = fitFallback }];
        foreach ((string kind, string reason) in new[]
                 {
                     ("custom-import", "Custom terrain texture imports are not raw-material compatible."),
                     ("native-relocation", "Native terrain relocation raw descriptor rebasing is unproven."),
                     ("missing-material", "The validated raw HQ sidecar is missing.")
                 })
        {
            viewport.SetNativeTerrainHqMaterialSet(null, compatible: false, incompatibilityReason: reason);
            viewport.AimFlyCameraAtTerrainForTesting(stoneHillAnchor, 420);
            viewport.EnableNativeTerrainDistanceForTesting();
            FlushUi();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
            NativeTerrainBoundedCompositorSnapshot fallback = viewport.CaptureNativeTerrainBoundedCompositorSnapshotForTesting();
            if (fallback.Used || !string.Equals(fallback.FallbackReason, reason, StringComparison.Ordinal))
                throw new InvalidOperationException($"{kind} did not fail closed as one whole legacy frame: {fallback}.");
            fallbacks.Add(new { kind, snapshot = fallback });
        }

        if (!PortableEditorCacheBuilder.TryLoadNativeTerrainHqMaterialCache(
                workspace,
                stoneHill.Key,
                out NativeTerrainHqMaterialSet? restoredMaterials) ||
            restoredMaterials == null)
        {
            throw new InvalidOperationException("Stone Hill raw HQ materials could not be restored for edit fail-closed checks.");
        }
        viewport.SetNativeTerrainHqMaterialSet(restoredMaterials);
        GeometryCandidate fallbackGeometry = viewport.Geometry
            ?? throw new InvalidOperationException("Stone Hill geometry disappeared during fallback checks.");
        TerrainPolygon editedPolygon = fallbackGeometry.Polygons[stoneHillAnchor];
        editedPolygon.ApplyTerrainDeltaZ(1);
        viewport.AimFlyCameraAtTerrainForTesting(stoneHillAnchor, 420);
        viewport.EnableNativeTerrainDistanceForTesting();
        FlushUi();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
        NativeTerrainBoundedCompositorSnapshot editedFallback = viewport.CaptureNativeTerrainBoundedCompositorSnapshotForTesting();
        if (editedFallback.Used || !editedFallback.FallbackReason.Contains("Terrain edits", StringComparison.Ordinal))
            throw new InvalidOperationException($"Edited terrain did not fail closed to one whole legacy frame: {editedFallback}.");
        editedPolygon.ResetTerrainEdit();
        fallbacks.Add(new { kind = "edited-terrain", snapshot = editedFallback });

        LowDetailTerrainPolygon malformedLowPoly = new(
            [new Vector2f(0, 0), new Vector2f(1, 0), new Vector2f(0, 1)],
            [0, 0, 0],
            sectorIndex: fallbackGeometry.SourceSectors[0].SectorIndex,
            faceIndex: int.MaxValue,
            sectorOffset: -1,
            faceOffset: -1,
            vertexIndexes: [0, 1, 2],
            cornerPointIndexes: [0, 1, 2],
            colorIndexes: [],
            cornerColors: [],
            rawWord0: 0,
            rawWord1: 0,
            transitionBias: 0,
            doubleSided: false,
            semiTransparent: false,
            blendMode: 0,
            orderingTableBias: 0);
        fallbackGeometry.LowDetailPolygons.Add(malformedLowPoly);
        viewport.InvalidateVisual();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
        FlushUi();
        NativeTerrainBoundedCompositorSnapshot malformedLowPolyFallback = viewport.CaptureNativeTerrainBoundedCompositorSnapshotForTesting();
        fallbackGeometry.LowDetailPolygons.Remove(malformedLowPoly);
        if (malformedLowPolyFallback.Used || !malformedLowPolyFallback.FallbackReason.Contains("LP record", StringComparison.Ordinal))
            throw new InvalidOperationException($"Malformed LP payload did not fail closed to one whole legacy frame: {malformedLowPolyFallback}.");
        fallbacks.Add(new { kind = "malformed-lp", snapshot = malformedLowPolyFallback });

        string metricsPath = Path.Combine(outputDirectory, "terrain-bounded-compositor-metrics.json");
        File.WriteAllText(metricsPath, JsonSerializer.Serialize(new
        {
            contract = Spyro.Editor.Core.Rendering.PsxTerrainBoundedRasterContract.Name,
            ditheringContract = Spyro.Editor.Core.Rendering.PsxTerrainBoundedRasterContract.Dithering,
            nativeBaseOrderingContract = Spyro.Editor.Core.Rendering.NativeTerrainOrderingTableContract.Name,
            nativePassOrderContract = Spyro.Editor.Core.Rendering.NativeTerrainOrderingTableContract.PassOrder,
            worldOrderingTableBucketCount = Spyro.Editor.Core.Rendering.NativeTerrainOrderingTableContract.WorldBucketCount,
            coarsePhaseOrder = new[] { "LowPoly", "HighPolyBase", "NormalHighQuality", "CloseHighQuality" },
            nativeOrderingInventory,
            claim = "bounded RGB5/STP/ABR/modulation stream with exact native 4x4 dither quantization, exact native LP/HP base buckets, and coarse FIFO phases; no HQ tile-local OT/GTE/clipping exactness claim",
            descriptorTileSeamProofs = new[] { normalSeam, closeSeam },
            captures,
            fallbacks
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Bounded native PSX terrain compositor smoke passed: {metricsPath}");
    }
    finally
    {
        window.Close();
        FlushUi();
    }
}

NativeTerrainBoundedCompositorSnapshot RenderBoundedTerrainFixture(
    MainWindow window,
    EditorViewport viewport,
    int terrainIndex,
    double distance,
    string frameName,
    bool saveFrame = true)
{
    viewport.SetViewMode(ViewportViewMode.Fly3D);
    viewport.AimFlyCameraAtTerrainForTesting(terrainIndex, distance);
    viewport.EnableNativeTerrainDistanceForTesting();
    FlushUi();
    AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
    FlushUi();
    NativeTerrainBoundedCompositorSnapshot first = viewport.CaptureNativeTerrainBoundedCompositorSnapshotForTesting();
    if (!first.Used || first.CacheHit || first.CommandCount <= 0 ||
        string.IsNullOrWhiteSpace(first.CommandSha256) || string.IsNullOrWhiteSpace(first.Rgb5Sha256))
    {
        throw new InvalidOperationException($"{frameName} did not build a fresh bounded compositor frame: {first}.");
    }
    AssertNativeBoundedOrderingSnapshot(first, frameName);

    viewport.InvalidateVisual();
    AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
    FlushUi();
    NativeTerrainBoundedCompositorSnapshot cached = viewport.CaptureNativeTerrainBoundedCompositorSnapshotForTesting();
    if (!cached.Used || !cached.CacheHit || cached.CommandSha256 != first.CommandSha256 ||
        cached.Rgb5Sha256 != first.Rgb5Sha256 || cached.RgbaSha256 != first.RgbaSha256)
    {
        throw new InvalidOperationException($"{frameName} did not reuse the deterministic bounded framebuffer: first={first}; cached={cached}.");
    }
    AssertNativeBoundedOrderingSnapshot(cached, $"{frameName} cached");
    if (saveFrame)
        SaveFrame(window, frameName);
    return first;
}

void AssertNativeBoundedOrderingSnapshot(
    NativeTerrainBoundedCompositorSnapshot snapshot,
    string frameName)
{
    if (!string.Equals(
            snapshot.NativeBaseOrderingContract,
            Spyro.Editor.Core.Rendering.NativeTerrainOrderingTableContract.Name,
            StringComparison.Ordinal) ||
        !string.Equals(
            snapshot.NativePassOrderContract,
            Spyro.Editor.Core.Rendering.NativeTerrainOrderingTableContract.PassOrder,
            StringComparison.Ordinal) ||
        !string.Equals(
            snapshot.DitheringContract,
            Spyro.Editor.Core.Rendering.PsxTerrainBoundedRasterContract.Dithering,
            StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            $"{frameName} did not persist the native dither/base-bucket/coarse-phase sub-contract: {snapshot}.");
    }

    if (snapshot.MinimumOtBucket < Spyro.Editor.Core.Rendering.NativeTerrainOrderingTableContract.MinimumBucket ||
        snapshot.MaximumOtBucket > Spyro.Editor.Core.Rendering.NativeTerrainOrderingTableContract.MaximumBucket ||
        snapshot.MinimumOtBucket > snapshot.MaximumOtBucket)
    {
        throw new InvalidOperationException(
            $"{frameName} emitted a terrain command outside the retail 0x800-entry world OT: " +
            $"{snapshot.MinimumOtBucket}..{snapshot.MaximumOtBucket}.");
    }

    int coarsePhaseCommandCount = checked(
        snapshot.LowPolyPhaseCommandCount +
        snapshot.HighPolyBasePhaseCommandCount +
        snapshot.NormalHighQualityPhaseCommandCount +
        snapshot.CloseHighQualityPhaseCommandCount);
    if (snapshot.LowPolyPhaseCommandCount != snapshot.LowPolyTriangleCount ||
        snapshot.HighPolyBasePhaseCommandCount < snapshot.LowQualityTriangleCount ||
        snapshot.NormalHighQualityPhaseCommandCount + snapshot.CloseHighQualityPhaseCommandCount !=
            snapshot.HighQualityTriangleCount ||
        coarsePhaseCommandCount != snapshot.CommandCount)
    {
        throw new InvalidOperationException(
            $"{frameName} coarse LP -> HP base -> normal HQ -> close HQ phase counts do not account for the full FIFO stream: {snapshot}.");
    }
}

NativeTerrainOrderingUiInventory AssertNativeTerrainOrderingUiInventory(string workspaceRoot)
{
    string cacheRoot = Path.Combine(workspaceRoot, "editor-cache");
    string[] overlays = Directory.GetFiles(
            cacheRoot,
            "*-runtime-scene-editor-overlay.json",
            SearchOption.TopDirectoryOnly)
        .Order(StringComparer.Ordinal)
        .ToArray();
    if (overlays.Length != 35)
        throw new InvalidOperationException($"Native ordering UI proof expected 35 overlays, found {overlays.Length}.");

    int highPolyFaceCount = 0;
    int rendererFlagAliasFaceCount = 0;
    int maximumHighPolyBaseBias = 0;
    foreach (string overlay in overlays)
    {
        using FileStream stream = File.OpenRead(overlay);
        using JsonDocument document = JsonDocument.Parse(stream);
        JsonElement highPoly = document.RootElement.GetProperty("candidates")[0].GetProperty("polygons");
        foreach (JsonElement face in highPoly.EnumerateArray())
        {
            string text = face.GetProperty("word4").GetString()
                ?? throw new InvalidOperationException($"{Path.GetFileName(overlay)} has an HP face without word4.");
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                text = text[2..];
            if (!uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint word3))
                throw new InvalidOperationException($"{Path.GetFileName(overlay)} has invalid HP word4 '{text}'.");

            int nativeBias = (int)((word3 & 0x38u) >> 1);
            maximumHighPolyBaseBias = Math.Max(maximumHighPolyBaseBias, nativeBias);
            if ((word3 & 0xC0u) != 0)
            {
                bool withFlags = Spyro.Editor.Core.Rendering.NativeTerrainOrderingTableContract.TryComputeHighPolyBaseBucket(
                    [512, 512, 512, 512], word3, out int flaggedBucket, out _);
                bool withoutFlags = Spyro.Editor.Core.Rendering.NativeTerrainOrderingTableContract.TryComputeHighPolyBaseBucket(
                    [512, 512, 512, 512], word3 & ~0xC0u, out int maskedBucket, out _);
                if (!withFlags || !withoutFlags || flaggedBucket != maskedBucket)
                {
                    throw new InvalidOperationException(
                        $"{Path.GetFileName(overlay)} HP renderer flag bits 6/7 inflated a base OT bucket.");
                }
                rendererFlagAliasFaceCount++;
            }
            highPolyFaceCount++;
        }
    }

    if (highPolyFaceCount != 190640 || rendererFlagAliasFaceCount != 1353 || maximumHighPolyBaseBias != 28)
    {
        throw new InvalidOperationException(
            $"Native ordering UI inventory changed: {highPolyFaceCount:N0} HP faces, " +
            $"{rendererFlagAliasFaceCount:N0} renderer-flag aliases, maximum base bias {maximumHighPolyBaseBias}.");
    }

    return new NativeTerrainOrderingUiInventory(
        Spyro.Editor.Core.Rendering.NativeTerrainOrderingTableContract.Name,
        Spyro.Editor.Core.Rendering.NativeTerrainOrderingTableContract.PassOrder,
        overlays.Length,
        highPolyFaceCount,
        rendererFlagAliasFaceCount,
        maximumHighPolyBaseBias,
        RendererFlagAliasBucketsUnaffected: true);
}

void AssertNativeLqTextureFrames(MainWindow window, LevelCatalog catalog)
{
    LevelDefinition stoneHill = catalog.Levels.Single(level =>
        level.Key.Equals("stonehill", StringComparison.OrdinalIgnoreCase));
    SelectLevelForViewportFit(window, stoneHill);
    EditorViewport viewport = window.GetLogicalDescendants().OfType<EditorViewport>().Single();

    NativeTerrainLqFrameSnapshot close = viewport.CaptureNativeTerrainLqFrameForTesting(40, 79.999);
    NativeTerrainLqFrameSnapshot normal = viewport.CaptureNativeTerrainLqFrameForTesting(40, 511);
    NativeTerrainLqFrameSnapshot lqDescriptorOne = viewport.CaptureNativeTerrainLqFrameForTesting(40, 512);
    NativeTerrainLqFrameSnapshot descriptorOneEnd = viewport.CaptureNativeTerrainLqFrameForTesting(40, 1031);
    NativeTerrainLqFrameSnapshot descriptorZeroStart = viewport.CaptureNativeTerrainLqFrameForTesting(40, 1032);
    NativeTerrainLqFrameSnapshot rowZeroEnd = viewport.CaptureNativeTerrainLqFrameForTesting(40, 1087);
    NativeTerrainLqFrameSnapshot rowOneStart = viewport.CaptureNativeTerrainLqFrameForTesting(40, 1088);
    NativeTerrainLqFrameSnapshot rowFourteenEnd = viewport.CaptureNativeTerrainLqFrameForTesting(40, 1983);
    NativeTerrainLqFrameSnapshot rowFifteenStart = viewport.CaptureNativeTerrainLqFrameForTesting(40, 1984);
    NativeTerrainLqFrameSnapshot hpLast = viewport.CaptureNativeTerrainLqFrameForTesting(40, 2047);
    NativeTerrainLqFrameSnapshot hpRemoved = viewport.CaptureNativeTerrainLqFrameForTesting(40, 2048);
    NativeTerrainLqFrameSnapshot fadeBypass = viewport.CaptureNativeTerrainLqFrameForTesting(
        40,
        2047,
        lqFadeBypass: true);
    NativeTerrainLqFrameSnapshot scrollingRowZero = viewport.CaptureNativeTerrainLqFrameForTesting(0, 1032);
    NativeTerrainLqFrameSnapshot scrollingRowOne = viewport.CaptureNativeTerrainLqFrameForTesting(0, 1088);

    const string rawDescriptorSha = "F090EC3B596F4927740F66F514F2961235D1DA73946A1F27D006B9B1B0114335";
    const string packedIndicesSha = "571D04D7B1B7964D033B88D598033F1C485BE7D55CBC759BADB0AED5DCED712A";
    const string paletteWordsSha = "2294EDE7FACE95514ACB6163F08B7852BBBC633D235D5C892AEF3A74A0E15DB7";
    foreach (NativeTerrainLqFrameSnapshot snapshot in new[]
             {
                 close, normal, lqDescriptorOne, descriptorOneEnd, descriptorZeroStart,
                 rowZeroEnd, rowOneStart, rowFourteenEnd, rowFifteenStart, hpLast, fadeBypass
             })
    {
        if (!snapshot.HasIndexedPayload || !snapshot.BitmapResolved ||
            snapshot.RawDescriptorSha256 != rawDescriptorSha ||
            snapshot.PackedIndicesSha256 != packedIndicesSha ||
            snapshot.PaletteWordsSha256 != paletteWordsSha ||
            string.IsNullOrWhiteSpace(snapshot.BitmapSha256))
        {
            throw new InvalidOperationException(
                $"Stone Hill texture 40 did not resolve its exact indexed TexLq proof at depth {snapshot.PlanarEditorDepth}: {snapshot}.");
        }
    }

    AssertLqSelection(close, highVisible: true, hq: true, descriptor: 1, row: 0, NativeTerrainTexturePreviewTier.Close, 64);
    AssertLqSelection(normal, highVisible: true, hq: true, descriptor: 1, row: 0, NativeTerrainTexturePreviewTier.Normal, 64);
    AssertLqSelection(lqDescriptorOne, highVisible: true, hq: false, descriptor: 1, row: 0, null, 32);
    AssertLqSelection(descriptorOneEnd, highVisible: true, hq: false, descriptor: 1, row: 0, null, 32);
    AssertLqSelection(descriptorZeroStart, highVisible: true, hq: false, descriptor: 0, row: 0, null, 32);
    AssertLqSelection(rowZeroEnd, highVisible: true, hq: false, descriptor: 0, row: 0, null, 32);
    AssertLqSelection(rowOneStart, highVisible: true, hq: false, descriptor: 0, row: 1, null, 32);
    AssertLqSelection(rowFourteenEnd, highVisible: true, hq: false, descriptor: 0, row: 14, null, 32);
    AssertLqSelection(rowFifteenStart, highVisible: true, hq: false, descriptor: 0, row: 15, null, 32);
    AssertLqSelection(hpLast, highVisible: true, hq: false, descriptor: 0, row: 15, null, 32);
    AssertLqSelection(fadeBypass, highVisible: true, hq: false, descriptor: 1, row: 0, null, 32);
    AssertLqSelection(scrollingRowZero, highVisible: true, hq: false, descriptor: 0, row: 0, null, 32);
    AssertLqSelection(scrollingRowOne, highVisible: true, hq: false, descriptor: 0, row: 1, null, 32);
    if (hpRemoved.HighPolyVisible || hpRemoved.RetainedWithoutLowDetail || hpRemoved.BitmapResolved)
        throw new InvalidOperationException($"Stone Hill HP texture remained visible at the 2048 cutoff: {hpRemoved}.");

    // The retail load/default state aliases LQ descriptor 1, LQ descriptor 0,
    // and the leading HQ descriptor. Stone Hill T40 also repeats its initial
    // palette through row 1, so those equalities are native evidence rather
    // than a failed transition. T0 is the scrolling fixture whose row 1 is
    // genuinely distinct, while T40 proves the final palette fade row.
    if (close.BitmapSha256 != normal.BitmapSha256 ||
        lqDescriptorOne.BitmapSha256 != descriptorZeroStart.BitmapSha256 ||
        rowOneStart.BitmapSha256 != descriptorZeroStart.BitmapSha256 ||
        rowFifteenStart.BitmapSha256 == rowOneStart.BitmapSha256 ||
        scrollingRowZero.BitmapSha256 != "69843B5D25C16D6F5BC19F1194D7146C4B7A42DF285003C0C54AB6C44A22E859" ||
        scrollingRowOne.BitmapSha256 != "229E80A3106966A1405FDB5362FDECA45056BDDF6C34867070070D387E0A1925" ||
        scrollingRowZero.BitmapSha256 == scrollingRowOne.BitmapSha256)
    {
        throw new InvalidOperationException(
            "Native load-state aliases or LQ palette-row transitions changed unexpectedly: " +
            $"close={close.BitmapSha256}, normal={normal.BitmapSha256}, " +
            $"descriptor1={lqDescriptorOne.BitmapSha256}, row0={descriptorZeroStart.BitmapSha256}, " +
            $"row1={rowOneStart.BitmapSha256}, row15={rowFifteenStart.BitmapSha256}, " +
            $"scrollingRow0={scrollingRowZero.BitmapSha256}, scrollingRow1={scrollingRowOne.BitmapSha256}.");
    }

    List<object> noLowDetailLevels = [];
    foreach (string levelKey in new[] { "beastmakers", "mistybog" })
    {
        LevelDefinition level = catalog.Levels.Single(candidate =>
            candidate.Key.Equals(levelKey, StringComparison.OrdinalIgnoreCase));
        SelectLevelForViewportFit(window, level);
        viewport = window.GetLogicalDescendants().OfType<EditorViewport>().Single();
        GeometryCandidate geometry = viewport.Geometry
            ?? throw new InvalidOperationException($"{level.DisplayName} geometry was not loaded.");
        if (geometry.LowDetailPolygons.Count != 0)
            throw new InvalidOperationException($"{level.DisplayName} unexpectedly contains LP geometry.");
        int textureId = geometry.Polygons.First(polygon => polygon.TextureId >= 0).TextureId;
        NativeTerrainLqFrameSnapshot retained = viewport.CaptureNativeTerrainLqFrameForTesting(textureId, 2400);
        if (retained.HighPolyVisible || !retained.RetainedWithoutLowDetail ||
            !retained.HasIndexedPayload || !retained.BitmapResolved ||
            retained.DescriptorIndex != 0 || retained.PaletteRow != 15 || retained.HqTier != null ||
            retained.BitmapWidth != 32 || retained.BitmapHeight != 32)
        {
            throw new InvalidOperationException(
                $"{level.DisplayName} did not retain HP with the far LQ row when no LP payload exists: {retained}.");
        }
        viewport.SetViewMode(ViewportViewMode.Fly3D);
        viewport.ResetView();
        viewport.EnableNativeTerrainDistanceForTesting();
        FlushUi();
        string frame = $"spyro-editor-{levelKey}-no-lp-native-lq.png";
        SaveFrame(window, frame);
        noLowDetailLevels.Add(new
        {
            levelKey,
            levelName = level.DisplayName,
            textureId,
            retained.PlanarEditorDepth,
            retained.DescriptorIndex,
            retained.PaletteRow,
            retained.BitmapSha256,
            frame
        });
    }

    string reportPath = Path.Combine(outputDirectory, "terrain-lq-texture-frame-metrics.json");
    File.WriteAllText(reportPath, JsonSerializer.Serialize(new
    {
        cacheFormatVersion = PortableEditorCacheBuilder.TerrainTexturePreviewCacheFormatVersion,
        decoder = PortableEditorCacheBuilder.TerrainTexturePreviewDecoder,
        stoneHillTexture40 = new
        {
            close,
            normal,
            lqDescriptorOne,
            descriptorOneEnd,
            descriptorZeroStart,
            rowZeroEnd,
            rowOneStart,
            rowFourteenEnd,
            rowFifteenStart,
            hpLast,
            hpRemoved,
            fadeBypass,
            scrollingRowZero,
            scrollingRowOne
        },
        noLowDetailLevels
    }, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"Native indexed LQ frame selection/composition passed: {reportPath}");
}

static void AssertLqSelection(
    NativeTerrainLqFrameSnapshot snapshot,
    bool highVisible,
    bool hq,
    int descriptor,
    int row,
    NativeTerrainTexturePreviewTier? hqTier,
    int expectedSide)
{
    if (snapshot.HighPolyVisible != highVisible || snapshot.RetainedWithoutLowDetail ||
        snapshot.HqOverlayEligible != hq || snapshot.DescriptorIndex != descriptor ||
        snapshot.PaletteRow != row || snapshot.HqTier != hqTier ||
        snapshot.BitmapWidth != expectedSide || snapshot.BitmapHeight != expectedSide)
    {
        throw new InvalidOperationException($"Unexpected native TexLq/HQ selection at depth {snapshot.PlanarEditorDepth}: {snapshot}.");
    }
}

static void AssertRuntimeTextureManifestFailsClosed(string workspaceRoot, string levelKey, string proofToRemove)
{
    string sourceDirectory = Path.Combine(workspaceRoot, "editor-cache", "terrain-textures", levelKey);
    string testDirectory = Path.Combine(workspaceRoot, "_runtime-preview-negative", proofToRemove);
    Directory.CreateDirectory(testDirectory);
    foreach (string sourcePath in Directory.EnumerateFiles(sourceDirectory))
        File.Copy(sourcePath, Path.Combine(testDirectory, Path.GetFileName(sourcePath)), true);

    string manifestPath = Path.Combine(testDirectory, "manifest.json");
    JsonNode root = JsonNode.Parse(File.ReadAllText(manifestPath))
        ?? throw new InvalidOperationException("Could not parse runtime texture manifest for the fail-closed UI fixture.");
    if (proofToRemove == "runtimeControlInitialStateComplete")
    {
        root[proofToRemove] = false;
    }
    else if (proofToRemove == "initializationMutations")
    {
        root.AsObject().Remove(proofToRemove);
    }
    else
    {
        JsonArray textures = root["textures"]?.AsArray()
            ?? throw new InvalidOperationException("Runtime texture manifest has no texture entries for the fail-closed UI fixture.");
        JsonObject controlled = textures.Select(node => node?.AsObject())
            .FirstOrDefault(node => node?["runtimeControlled"]?.GetValue<bool>() == true)
            ?? throw new InvalidOperationException("Runtime texture manifest has no controlled entry for the fail-closed UI fixture.");
        controlled.Remove(proofToRemove);
    }
    File.WriteAllText(manifestPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

    MethodInfo reader = typeof(MainWindow).GetMethod(
        "TryReadNativeTerrainTexturePreviewFiles",
        BindingFlags.Static | BindingFlags.NonPublic)
        ?? throw new MissingMethodException(typeof(MainWindow).FullName, "TryReadNativeTerrainTexturePreviewFiles");
    object?[] invokeArgs = [manifestPath, testDirectory, null, null];
    bool accepted = (bool)(reader.Invoke(null, invokeArgs) ?? false);
    if (accepted)
        throw new InvalidOperationException($"Runtime texture manifest remained loadable after removing proof '{proofToRemove}'.");
}

static int SelectDepthCueAnchor(EditorViewport viewport)
{
    GeometryCandidate geometry = viewport.Geometry
        ?? throw new InvalidOperationException("The depth-cue smoke requires loaded geometry.");
    float centerX = Median(viewport.Mobys.Where(moby => !moby.IsRemoved).Select(moby => moby.Position.X),
        geometry.Polygons.Average(polygon => polygon.Center.X));
    float centerY = Median(viewport.Mobys.Where(moby => !moby.IsRemoved).Select(moby => moby.Position.Y),
        geometry.Polygons.Average(polygon => polygon.Center.Y));

    return geometry.Polygons
        .Select((polygon, index) => (polygon, index))
        .Where(item => item.polygon.HasNativeCornerPayload && item.polygon.TextureId >= 0 &&
            item.polygon.Points.Count >= 3 && item.polygon.ZValues.Length >= 3)
        .OrderBy(item =>
        {
            double dx = item.polygon.Center.X - centerX;
            double dy = item.polygon.Center.Y - centerY;
            return (dx * dx) + (dy * dy);
        })
        .ThenByDescending(item => item.polygon.Bounds.Width * item.polygon.Bounds.Height)
        .Select(item => item.index)
        .First();
}

static (int TerrainIndex, double Distance, NativeTerrainFarLodSnapshot Snapshot) SelectNativeLodProofPose(
    EditorViewport viewport)
{
    GeometryCandidate geometry = viewport.Geometry
        ?? throw new InvalidOperationException("The native-LOD smoke requires loaded geometry.");
    float centerX = Median(
        viewport.Mobys.Where(moby => !moby.IsRemoved).Select(moby => moby.Position.X),
        geometry.Polygons.Average(polygon => polygon.Center.X));
    float centerY = Median(
        viewport.Mobys.Where(moby => !moby.IsRemoved).Select(moby => moby.Position.Y),
        geometry.Polygons.Average(polygon => polygon.Center.Y));
    int preferredAnchor = SelectDepthCueAnchor(viewport);
    int[] anchors = geometry.Polygons
        .Select((polygon, index) => (polygon, index))
        .Where(item => item.polygon.Points.Count >= 3 && item.polygon.ZValues.Length >= 3)
        .OrderBy(item => item.index == preferredAnchor ? 0 : 1)
        .ThenBy(item =>
        {
            double dx = item.polygon.Center.X - centerX;
            double dy = item.polygon.Center.Y - centerY;
            return (dx * dx) + (dy * dy);
        })
        .Take(16)
        .Select(item => item.index)
        .ToArray();

    (int TerrainIndex, double Distance, NativeTerrainFarLodSnapshot Snapshot, long Score)? best = null;
    foreach (int anchor in anchors)
    {
        foreach (double distance in new[] { 900d, 1100d, 1400d, 1700d, 2000d, 2400d })
        {
            viewport.AimFlyCameraAtTerrainForTesting(anchor, distance);
            viewport.SetNativeTerrainLodPreviewModeForTesting(NativeTerrainLodPreviewMode.NativeDistance);
            NativeTerrainFarLodSnapshot candidate = viewport.CaptureNativeTerrainFarLodSnapshotForTesting();
            long score =
                (candidate.VisibleHighDetailFaces > 0 ? 1_000_000_000L : 0) +
                (candidate.VisibleLowDetailFaces > 0 ? 1_000_000_000L : 0) +
                (candidate.SuppressedHighDetailFaces > 0 ? 500_000_000L : 0) +
                (candidate.VisibleHighDetailFacesAtOrBeyondCutoff == 0 ? 250_000_000L : 0) +
                (Math.Min(candidate.VisibleHighDetailFaces, candidate.VisibleLowDetailFaces) * 100_000L) +
                (candidate.VisibleHighDetailFaces * 100L) +
                candidate.VisibleLowDetailFaces;
            if (best == null || score > best.Value.Score)
                best = (anchor, distance, candidate, score);
        }
    }

    if (best == null)
        throw new InvalidOperationException("Could not construct a game-scale native terrain LOD camera pose.");

    viewport.AimFlyCameraAtTerrainForTesting(best.Value.TerrainIndex, best.Value.Distance);
    viewport.SetNativeTerrainLodPreviewModeForTesting(NativeTerrainLodPreviewMode.NativeDistance);
    return (
        best.Value.TerrainIndex,
        best.Value.Distance,
        viewport.CaptureNativeTerrainFarLodSnapshotForTesting());
}

static float Median(IEnumerable<float> source, float fallback)
{
    float[] values = source.OrderBy(value => value).ToArray();
    return values.Length == 0 ? fallback : values[values.Length / 2];
}

static void AssertNativeTerrainDepthCueFormula()
{
    ColorRgba legacyNearField = ColorRgba.FromRgb(10, 20, 30);   // Runtime far endpoint (table 2).
    ColorRgba legacyFarField = ColorRgba.FromRgb(110, 120, 130); // Runtime near endpoint (table 1).
    AssertDepthCueColor("near clamp", 900, legacyNearField, legacyFarField, ColorRgba.FromRgb(110, 120, 130));
    AssertDepthCueColor("near boundary", 1024, legacyNearField, legacyFarField, ColorRgba.FromRgb(110, 120, 130));
    AssertDepthCueColor("DPCS midpoint", 1536, legacyNearField, legacyFarField, ColorRgba.FromRgb(60, 70, 80));
    AssertDepthCueColor("far boundary", 2048, legacyNearField, legacyFarField, ColorRgba.FromRgb(10, 20, 30));
    AssertDepthCueColor("far clamp", 2400, legacyNearField, legacyFarField, ColorRgba.FromRgb(10, 20, 30));

    if (EditorViewport.NativeTerrainDepthCueIr0(1024) != 0x1000 ||
        EditorViewport.NativeTerrainDepthCueIr0(1536) != 0x800 ||
        EditorViewport.NativeTerrainDepthCueIr0(2048) != 0)
    {
        throw new InvalidOperationException("Native terrain DPCS IR0 boundaries no longer match the retail 0x1000-unit ramp.");
    }
    Console.WriteLine("Native terrain depth-cue formula: table1 near/table2 far, 1024..2048 editor-unit DPCS ramp verified.");
}

static void AssertDepthCueColor(
    string label,
    double cameraDepth,
    ColorRgba legacyNearField,
    ColorRgba legacyFarField,
    ColorRgba expected)
{
    ColorRgba actual = EditorViewport.NativeTerrainDepthCueColor(legacyNearField, legacyFarField, cameraDepth);
    if (actual != expected)
        throw new InvalidOperationException($"Native terrain {label} expected {expected}, found {actual}.");
}

void RunMobyAtlasOnly()
{
    MobyRasterIconAtlasContract atlas1 = MobyRasterIconCatalog.InstalledAtlas1;
    MobyRasterIconAtlasContract atlas2 = MobyRasterIconCatalog.InstalledAtlas2;
    MobyRasterIconAtlasContract atlas3 = MobyRasterIconCatalog.InstalledAtlas3;
    Dictionary<string, string> expectedSourceHashes = new(StringComparer.Ordinal)
    {
        [atlas1.Key] = "57F81567E89447D193C08C4CCD5CF15B75C2EA996691E98935272E15D9FD7F31",
        [atlas2.Key] = "80387FE566D1366B6756299C88FD8DC6B4BA3113586CBAE848C2D109FC9480D8",
        [atlas3.Key] = "C6157C97434A9B3DF6952E2E39752ECECC297C7349EAD75C240FEF53AE7EF29D"
    };
    Dictionary<string, string> expectedMaskedHashes = new(StringComparer.Ordinal)
    {
        [atlas1.Key] = "230E1E850BE307B673DD653CA0D684882164713F60BC3D0E3E905B2138921280",
        [atlas2.Key] = "C15450EC289C2B3A335D22CD0EA452ED20D792691A163066E8B7CBAD07381E08",
        [atlas3.Key] = "05D78EB1331C8D77AF86EC99B836B2700940DE94E3F9D1A955026D8DA18C4AEA"
    };
    (int OpaquePixels, int TransparentPixels, string Sha256)[] expectedAtlas3Cells =
    [
        (32_542, 65_427, "ba64df7eb8d07abc4914ef3b2e2fa468080b5ee90aad4940a1835013238dbb46"),
        (28_762, 69_520, "a470626aa5f1d381c6aa4863bd9cec156ef014d79cfa17316c1baa36a9289481"),
        (30_110, 67_859, "a19ab5d917ce08ad5eeb5016f0e8f0b57560aad7fb31a4ca89a417cb074090dc"),
        (22_560, 75_722, "a05d94094bea107634551bf5c87b449c15fdb943665ca3ab0d9cf24517390993"),
        (42_839, 55_443, "609860f8f8606da1694535c7324a1c06a167a32c6f7ccfeae2044ebcbb262f37"),
        (35_209, 63_387, "76852797c76f97fc42c54c470491e4ecc2d849e4a98a14289e63d69bec23a2de"),
        (50_890, 47_392, "d39bb53be12df491cb8a5d768336b366db23e43a96bdbaaaeb18616ac3c1b432"),
        (21_674, 76_922, "c00d9357fe9a67b24254158bd2715baa32bb3e8194d9558f754d78392e1a159b"),
        (31_986, 65_983, "da2131cf91cb1c6f82d145de37aaf73289aac2a228840a82341aafebcdbef609"),
        (26_558, 71_724, "d96f0b200095f2bdd369700b8e6d1ec9bb651907783b26f73e29e08a0f245562"),
        (38_121, 59_848, "4a2f006eef40938838d9ee0a4c63b1ea03826474099a07beaba65adc70e2a434"),
        (53_380, 44_902, "f3f398edc890e9fb507a348a1a36038b09b572fa21779c2d4384bfcfd282a0b2"),
        (0, 98_282, "7a240896fa3d0d560db3f596ed80a6f3238cf446007fb24fe7ff44b9ed406a8d"),
        (0, 98_596, "9caffba97f02fe2cc21f9db5e0fc3128206a1945c792778cfb4a7eef0ebcbe6f"),
        (0, 98_282, "7a240896fa3d0d560db3f596ed80a6f3238cf446007fb24fe7ff44b9ed406a8d"),
        (0, 98_596, "9caffba97f02fe2cc21f9db5e0fc3128206a1945c792778cfb4a7eef0ebcbe6f")
    ];
    foreach (MobyRasterIconAtlasContract atlas in new[] { atlas1, atlas2, atlas3 })
    {
        (
            int expectedTransparentPixels,
            int expectedPreservedLightPixels,
            double minimumSubjectTransparency,
            int expectedUsedCellCount,
            int expectedEmptyCellCount) =
            atlas.Key switch
            {
                "moby-family-atlas-1" => (1_008_714, 19_118, 0.20, 14, 2),
                "moby-family-atlas-2" => (1_050_596, 3_657, 0.40, 14, 2),
                "moby-family-atlas-3" => (1_157_885, 2_735, 0.40, 12, 4),
                _ => throw new InvalidOperationException($"No source-locked masking baseline exists for {atlas.Key}.")
            };
        string atlasPath = MobyRasterIconCatalog.GetAtlasAssetPath(AppContext.BaseDirectory, atlas);
        string sourceHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(atlasPath)));
        if (!string.Equals(sourceHash, expectedSourceHashes[atlas.Key], StringComparison.Ordinal))
            throw new InvalidOperationException($"{atlas.FileName} SHA-256 {sourceHash} does not match its generated source.");

        MobyIconAtlasLoadResult loaded = MobyIconAtlasLoader.Load(atlasPath, atlas);
        if (!loaded.Succeeded || loaded.Bitmap == null)
            throw new InvalidOperationException($"The generated Moby atlas {atlas.Key} did not load: {loaded.Diagnostics.FailureReason}");
        MobyIconAtlasDiagnostics diagnostics = loaded.Diagnostics;
        if (diagnostics.Atlas != atlas ||
            atlas.Columns != 4 ||
            atlas.Rows != 4 ||
            atlas.UsedCellCount != expectedUsedCellCount ||
            atlas.EmptyCellCount != expectedEmptyCellCount)
        {
            throw new InvalidOperationException(
                $"{atlas.Key} diagnostics do not retain the installed 4 x 4 / " +
                $"{expectedUsedCellCount}-used-cell contract.");
        }
        if (diagnostics.Width != 1254 || diagnostics.Height != 1254)
            throw new InvalidOperationException($"Expected {atlas.Key} to remain 1254 x 1254, found {diagnostics.Width} x {diagnostics.Height}.");
        if (diagnostics.SourceTransparentPixelCount != 0)
            throw new InvalidOperationException($"{atlas.Key} should remain the original opaque RGB source image.");
        if (diagnostics.CheckerboardColors.Count != 2 || diagnostics.CheckerboardSampleCoverage < 0.98)
            throw new InvalidOperationException($"{atlas.Key}'s unused cells did not provide a stable two-color checkerboard sample.");
        if (diagnostics.TransparentFraction < 0.40)
            throw new InvalidOperationException($"Only {diagnostics.TransparentFraction:P1} of masked {atlas.Key} became transparent.");
        if ((expectedTransparentPixels >= 0 &&
                (diagnostics.TransparentPixelCount != expectedTransparentPixels ||
                diagnostics.ConnectedBackgroundPixelCount != expectedTransparentPixels)) ||
            (expectedPreservedLightPixels >= 0 &&
                diagnostics.PreservedDisconnectedBackgroundLikePixelCount != expectedPreservedLightPixels))
        {
            throw new InvalidOperationException(
                $"{atlas.Key} masking drifted from its source-locked clean-background baseline: " +
                $"transparent {diagnostics.TransparentPixelCount:N0}/{expectedTransparentPixels:N0}, " +
                $"connected {diagnostics.ConnectedBackgroundPixelCount:N0}/{expectedTransparentPixels:N0}, " +
                $"preserved enclosed light {diagnostics.PreservedDisconnectedBackgroundLikePixelCount:N0}/{expectedPreservedLightPixels:N0}.");
        }
        if (diagnostics.Cells.Count != atlas.CellCount)
            throw new InvalidOperationException($"{atlas.Key} should expose {atlas.CellCount} cell diagnostics, found {diagnostics.Cells.Count}.");

        MobyIconAtlasCellDiagnostics[] subjectCells = diagnostics.Cells
            .Where(cell => cell.AtlasCell < atlas.UsedCellCount)
            .ToArray();
        if (subjectCells.Length != atlas.UsedCellCount ||
            subjectCells.Select(cell => cell.Sha256).Distinct(StringComparer.Ordinal).Count() != atlas.UsedCellCount)
        {
            throw new InvalidOperationException($"{atlas.Key} does not contain {atlas.UsedCellCount} unique subject cells.");
        }
        if (subjectCells.Any(cell =>
                cell.OpaquePixelCount < 4_000 || cell.TransparentFraction < minimumSubjectTransparency))
            throw new InvalidOperationException($"At least one {atlas.Key} subject cell is empty or retains too much checkerboard background.");

        if (atlas == atlas3)
        {
            foreach (MobyIconAtlasCellDiagnostics cell in diagnostics.Cells)
            {
                (int expectedOpaque, int expectedTransparent, string expectedSha256) =
                    expectedAtlas3Cells[cell.AtlasCell];
                if (cell.OpaquePixelCount != expectedOpaque ||
                    cell.TransparentPixelCount != expectedTransparent ||
                    !string.Equals(cell.Sha256, expectedSha256, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"{atlas.Key} cell {cell.AtlasCell} masking drifted: " +
                        $"opaque {cell.OpaquePixelCount:N0}/{expectedOpaque:N0}, " +
                        $"transparent {cell.TransparentPixelCount:N0}/{expectedTransparent:N0}, " +
                        $"SHA-256 {cell.Sha256}/{expectedSha256}.");
                }
                Console.WriteLine(
                    $"{atlas.Key} cell {cell.AtlasCell}: bounds {cell.Bounds}, " +
                    $"opaque {cell.OpaquePixelCount:N0}, transparent {cell.TransparentPixelCount:N0}, SHA-256 {cell.Sha256}.");
            }
        }

        MobyIconAtlasCellDiagnostics[] emptyCells = diagnostics.Cells
            .Where(cell => cell.AtlasCell >= atlas.UsedCellCount)
            .ToArray();
        if (emptyCells.Length != atlas.EmptyCellCount || emptyCells.Any(cell => cell.OpaquePixelCount != 0))
            throw new InvalidOperationException($"The contractually empty {atlas.Key} cells did not become fully transparent.");

        MobyIconAtlasLoadResult cached = MobyIconAtlasLoader.Load(atlasPath, atlas);
        if (!ReferenceEquals(loaded, cached) || !ReferenceEquals(loaded.Bitmap, cached.Bitmap))
            throw new InvalidOperationException($"Masked {atlas.Key} was decoded more than once instead of being reused from memory.");

        // Same source path, different explicit used-cell boundary: this must be
        // a separate cache entry and must sample only cell 15.
        MobyRasterIconAtlasContract contractVariant = new(
            atlas.Key,
            atlas.FileName,
            atlas.Columns,
            atlas.Rows,
            usedCellCount: 15);
        MobyIconAtlasLoadResult variantLoaded = MobyIconAtlasLoader.Load(atlasPath, contractVariant);
        if (!variantLoaded.Succeeded ||
            ReferenceEquals(loaded, variantLoaded) ||
            variantLoaded.Diagnostics.Atlas != contractVariant ||
            variantLoaded.Diagnostics.CheckerboardSamplePixelCount >= diagnostics.CheckerboardSamplePixelCount)
        {
            throw new InvalidOperationException($"{atlas.Key} cache identity did not honor the contract-specific used-cell boundary.");
        }

        string maskedAtlasCapture = Path.Combine(
            outputDirectory,
            $"{Path.GetFileNameWithoutExtension(atlas.FileName)}-masked.png");
        loaded.Bitmap.Save(maskedAtlasCapture);
        if (atlas == atlas2 || atlas == atlas3)
            AssertNoVisibleAlternatingCheckerTiles(atlasPath, maskedAtlasCapture, atlas, diagnostics);
        string maskedHash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(maskedAtlasCapture)));
        if (!string.Equals(maskedHash, expectedMaskedHashes[atlas.Key], StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Masked {atlas.Key} SHA-256 {maskedHash} does not match its source-locked baseline " +
                $"{expectedMaskedHashes[atlas.Key]}.");
        }
        Console.WriteLine(
            $"{atlas.Key} mask: {diagnostics.Width}x{diagnostics.Height}, " +
            $"checker {string.Join("/", diagnostics.CheckerboardColors.Select(color => color.Hex))}, " +
            $"{diagnostics.TransparentPixelCount:N0}/{diagnostics.TotalPixelCount:N0} transparent ({diagnostics.TransparentFraction:P1}), " +
            $"{diagnostics.PreservedDisconnectedBackgroundLikePixelCount:N0} disconnected light pixels preserved.");
        Console.WriteLine($"Rendered masked atlas: {maskedAtlasCapture} (SHA-256 {maskedHash}).");
    }

    if (new[] { atlas1, atlas2, atlas3 }
        .Select(atlas => new MobyRasterIconAtlasCellId(atlas.Key, 0))
        .Distinct()
        .Count() != 3)
    {
        throw new InvalidOperationException("Atlas-qualified viewport diagnostics collapsed cell zero across installed atlases.");
    }

    string missingPath = Path.Combine(workspace, "missing-moby-icon-atlas.png");
    MobyIconAtlasLoadResult missing = MobyIconAtlasLoader.Load(missingPath, atlas1);
    if (missing.Succeeded || missing.Bitmap != null || string.IsNullOrWhiteSpace(missing.Diagnostics.FailureReason))
        throw new InvalidOperationException("A missing optional atlas did not return the safe procedural-marker fallback result.");
    if (!ReferenceEquals(missing, MobyIconAtlasLoader.Load(missingPath, atlas1)))
        throw new InvalidOperationException("A missing optional atlas should also be cached as one safe fallback result.");

    string invalidPath = Path.Combine(workspace, "invalid-moby-icon-atlas.png");
    File.WriteAllText(invalidPath, "not a PNG");
    MobyIconAtlasLoadResult invalid = MobyIconAtlasLoader.Load(invalidPath, atlas1);
    if (invalid.Succeeded || invalid.Bitmap != null || string.IsNullOrWhiteSpace(invalid.Diagnostics.FailureReason))
        throw new InvalidOperationException("An unreadable optional atlas did not return the safe procedural-marker fallback result.");

    MobyRasterIconAtlasCellId A1(int cell) => new(atlas1.Key, cell);
    MobyRasterIconAtlasCellId A2(int cell) => new(atlas2.Key, cell);
    MobyRasterIconAtlasCellId A3(int cell) => new(atlas3.Key, cell);
    (string LevelKey, MobyRasterIconAtlasCellId[] ExpectedCells)[] fixtures =
    [
        ("mistybog", [A1(0), A3(0), A3(6), A3(9)]),
        ("hauntedtowers", [A1(1), A1(9), A2(10), A2(12)]),
        ("loftycastle", [A1(6), A1(7), A1(10)]),
        ("gnorccove", [A1(2), A1(11), A2(1), A2(3)]),
        ("sunnyflight", [A1(12), A1(13)]),
        ("metalhead", [A1(3), A3(11)]),
        ("terracevillage", [A1(4), A2(7)]),
        ("nightflight", [A1(5), A2(11)]),
        ("dreamweavers", [A1(8), A3(8)]),
        ("jacques", [A2(0), A2(8)]),
        ("alpineridge", [A2(2)]),
        ("gnastysloot", [A2(4)]),
        ("townsquare", [A2(5)]),
        ("stonehill", [A2(6), A2(9)]),
        ("icecavern", [A2(13), A3(2)]),
        ("beastmakers", [A3(0), A3(1)]),
        ("highcaves", [A3(3)]),
        ("twilightharbor", [A3(4), A3(7)]),
        ("darkhollow", [A3(5)]),
        ("artisans", [A3(10)])
    ];

    MainWindow window = new()
    {
        Width = 1440,
        Height = 900,
        WindowStartupLocation = WindowStartupLocation.Manual,
        Position = new PixelPoint(0, 0)
    };
    window.Show();
    try
    {
        WaitForLevelData(window);
        LevelCatalog catalog = LevelCatalog.Load(sourceWorkspace);
        HashSet<MobyRasterIconAtlasCellId> renderedCells = new();
        List<object> captureRows = [];
        foreach ((string levelKey, MobyRasterIconAtlasCellId[] expectedCellIds) in fixtures)
        {
            LevelDefinition level = catalog.Levels.Single(candidate =>
                candidate.Key.Equals(levelKey, StringComparison.OrdinalIgnoreCase));
            SelectLevelForViewportFit(window, level);
            EditorViewport viewport = window.GetLogicalDescendants().OfType<EditorViewport>().Single();
            viewport.SetViewMode(ViewportViewMode.Map);

            List<Moby> matchingMobys = viewport.Mobys
                .Where(moby => !moby.IsRemoved && MobyRasterIconCatalog.TryMatch(moby, out _))
                .ToList();
            MobyRasterIconAtlasCellId[] matchingCells = matchingMobys
                .Select(moby =>
                {
                    MobyRasterIconCatalog.TryMatch(moby, out MobyRasterIconDefinition definition);
                    return definition.AtlasCellId;
                })
                .Distinct()
                .OrderBy(cell => cell.AtlasKey, StringComparer.Ordinal)
                .ThenBy(cell => cell.AtlasCell)
                .ToArray();
            MobyRasterIconAtlasCellId[] absent = expectedCellIds.Except(matchingCells).ToArray();
            if (matchingMobys.Count == 0 || absent.Length > 0)
            {
                throw new InvalidOperationException(
                    $"{level.DisplayName} did not load the expected atlas fixtures: {string.Join(",", absent)}.");
            }

            EditorViewport.ResetMobyRasterIconRenderDiagnosticsForTesting();
            viewport.FocusMobys(matchingMobys);
            FlushUi();
            string captureName = $"spyro-editor-moby-atlas-{levelKey}.png";
            SaveFrame(window, captureName);
            IReadOnlyDictionary<MobyRasterIconAtlasCellId, int> drawCounts =
                EditorViewport.CaptureMobyRasterIconRenderDiagnosticsForTesting();
            MobyRasterIconAtlasCellId[] notDrawn = expectedCellIds
                .Where(cell => !drawCounts.TryGetValue(cell, out int count) || count <= 0)
                .ToArray();
            if (notDrawn.Length > 0)
            {
                throw new InvalidOperationException(
                    $"{level.DisplayName} matched but did not render atlas cell(s) {string.Join(",", notDrawn)}.");
            }

            renderedCells.UnionWith(drawCounts.Keys);
            string capturePath = Path.Combine(outputDirectory, captureName);
            captureRows.Add(new
            {
                levelKey,
                levelName = level.DisplayName,
                expectedCells = expectedCellIds.Select(cell => cell.ToString()).ToArray(),
                drawnCells = drawCounts.Keys
                    .OrderBy(cell => cell.AtlasKey, StringComparer.Ordinal)
                    .ThenBy(cell => cell.AtlasCell)
                    .Select(cell => cell.ToString())
                    .ToArray(),
                captureName,
                captureSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(capturePath)))
            });
            Console.WriteLine(
                $"{level.DisplayName}: rendered {matchingMobys.Count} atlas-backed Mobys from cell(s) " +
                $"{string.Join(",", drawCounts.Keys.OrderBy(cell => cell.AtlasKey, StringComparer.Ordinal).ThenBy(cell => cell.AtlasCell))}.");
        }

        MobyRasterIconAtlasCellId[] missingRenderedCells = MobyRasterIconCatalog.Definitions
            .Select(definition => definition.AtlasCellId)
            .Except(renderedCells)
            .ToArray();
        if (missingRenderedCells.Length > 0)
            throw new InvalidOperationException($"Focused level captures never rendered atlas cell(s) {string.Join(",", missingRenderedCells)}.");

        string captureReport = Path.Combine(outputDirectory, "moby-icon-atlas-captures.json");
        File.WriteAllText(captureReport, JsonSerializer.Serialize(new
        {
            atlasCount = 3,
            usedCellCount = 40,
            renderedCellCount = renderedCells.Count,
            captures = captureRows
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Moby atlas capture report: {captureReport}");
    }
    finally
    {
        window.Close();
        FlushUi();
    }

    Console.WriteLine(
        "Moby atlas UI smoke: PASSED 40 unique atlas-qualified cells across three source-locked atlases, " +
        "contract-owned empty-cell masking, contract-aware caching, safe fallbacks, and 20 representative level captures.");
}

static void AssertNoVisibleAlternatingCheckerTiles(
    string sourcePath,
    string maskedPath,
    MobyRasterIconAtlasContract atlas,
    MobyIconAtlasDiagnostics diagnostics)
{
    Rgba32[] source = PngRgbaImage.ReadRgba(sourcePath, out int width, out int height);
    Rgba32[] masked = PngRgbaImage.ReadRgba(maskedPath, out int maskedWidth, out int maskedHeight);
    if (maskedWidth != width || maskedHeight != height || source.Length != masked.Length)
        throw new InvalidOperationException($"Masked {atlas.Key} dimensions no longer match its source.");

    int[] expectedStarts = [0, 313, 627, 940];
    int[] expectedSizes = [313, 314, 313, 314];
    for (int row = 0; row < atlas.Rows; row++)
    {
        for (int column = 0; column < atlas.Columns; column++)
        {
            int atlasCell = (row * atlas.Columns) + column;
            PixelRect bounds = MobyIconAtlasLoader.GetCellPixelRect(
                new PixelSize(width, height),
                atlas,
                atlasCell);
            if (bounds.X != expectedStarts[column] || bounds.Width != expectedSizes[column] ||
                bounds.Y != expectedStarts[row] || bounds.Height != expectedSizes[row])
            {
                throw new InvalidOperationException(
                    $"{atlas.Key} cell {atlasCell} no longer uses the exact gap-free 1254/4 integer bounds.");
            }
        }
    }

    HashSet<int> exactEmptyCellColors = new();
    for (int atlasCell = atlas.UsedCellCount; atlasCell < atlas.CellCount; atlasCell++)
    {
        PixelRect bounds = MobyIconAtlasLoader.GetCellPixelRect(new PixelSize(width, height), atlas, atlasCell);
        for (int y = bounds.Y; y < bounds.Bottom; y++)
        {
            for (int x = bounds.X; x < bounds.Right; x++)
                exactEmptyCellColors.Add(RgbKey(source[(y * width) + x]));
        }
    }

    (int checkerTilesPerAxis, double learnedParityDelta) = DetectCheckerPeriod(
        source,
        width,
        height,
        atlas);
    int expectedCheckerTilesPerAxis = atlas.Key switch
    {
        "moby-family-atlas-2" => 60,
        "moby-family-atlas-3" => 48,
        _ => throw new InvalidOperationException($"No enclosed-checker baseline exists for {atlas.Key}.")
    };
    if (checkerTilesPerAxis != expectedCheckerTilesPerAxis || learnedParityDelta < 10)
    {
        throw new InvalidOperationException(
            $"{atlas.Key}'s source-locked empty cells no longer expose the audited " +
            $"{expectedCheckerTilesPerAxis}-tile checker " +
            $"({checkerTilesPerAxis} tiles, {learnedParityDelta:0.00} luma delta).");
    }

    int variation = Math.Max(0, diagnostics.CheckerboardMaximumChroma - 4);
    int connectedMinimumLight = Math.Max(
        190,
        diagnostics.CheckerboardMinimumLightChannel - (variation * 2));
    int connectedMaximumChroma = Math.Min(
        32,
        diagnostics.CheckerboardMaximumChroma + variation);
    double minimumComponentParityDelta = Math.Max(4, learnedParityDelta * 0.40);
    bool[] visited = new bool[masked.Length];
    int[] queue = new int[masked.Length];

    bool IsCheckerCandidate(int index)
    {
        Rgba32 pixel = masked[index];
        if (pixel.A == 0)
            return false;
        int key = RgbKey(pixel);
        int minimum = Math.Min(pixel.R, Math.Min(pixel.G, pixel.B));
        int maximum = Math.Max(pixel.R, Math.Max(pixel.G, pixel.B));
        return exactEmptyCellColors.Contains(key) ||
            (minimum >= connectedMinimumLight && maximum - minimum <= connectedMaximumChroma);
    }

    for (int atlasCell = 0; atlasCell < atlas.UsedCellCount; atlasCell++)
    {
        PixelRect bounds = MobyIconAtlasLoader.GetCellPixelRect(new PixelSize(width, height), atlas, atlasCell);
        for (int seedY = bounds.Y; seedY < bounds.Bottom; seedY++)
        {
            for (int seedX = bounds.X; seedX < bounds.Right; seedX++)
            {
                int seedIndex = (seedY * width) + seedX;
                if (visited[seedIndex] || !IsCheckerCandidate(seedIndex))
                    continue;

                int readIndex = 0;
                int writeIndex = 0;
                int exactColorPixels = 0;
                int minimumX = seedX;
                int maximumX = seedX;
                int minimumY = seedY;
                int maximumY = seedY;
                long[] parityLumaSums = new long[2];
                int[] parityCounts = new int[2];

                void Enqueue(int x, int y)
                {
                    int index = (y * width) + x;
                    if (visited[index] || !IsCheckerCandidate(index))
                        return;
                    visited[index] = true;
                    queue[writeIndex++] = index;
                }

                Enqueue(seedX, seedY);
                while (readIndex < writeIndex)
                {
                    int index = queue[readIndex++];
                    int x = index % width;
                    int y = index / width;
                    Rgba32 pixel = masked[index];
                    exactColorPixels += exactEmptyCellColors.Contains(RgbKey(pixel)) ? 1 : 0;
                    int parity =
                        ((int)((long)x * checkerTilesPerAxis / width) +
                        (int)((long)y * checkerTilesPerAxis / height)) & 1;
                    parityLumaSums[parity] += pixel.R + pixel.G + pixel.B;
                    parityCounts[parity]++;
                    minimumX = Math.Min(minimumX, x);
                    maximumX = Math.Max(maximumX, x);
                    minimumY = Math.Min(minimumY, y);
                    maximumY = Math.Max(maximumY, y);

                    if (x > bounds.X)
                        Enqueue(x - 1, y);
                    if (x + 1 < bounds.Right)
                        Enqueue(x + 1, y);
                    if (y > bounds.Y)
                        Enqueue(x, y - 1);
                    if (y + 1 < bounds.Bottom)
                        Enqueue(x, y + 1);
                }

                if (writeIndex < 64 || exactColorPixels / (double)writeIndex < 0.50 ||
                    parityCounts[0] < 8 || parityCounts[1] < 8)
                {
                    continue;
                }

                double parity0Luma = parityLumaSums[0] / (parityCounts[0] * 3.0);
                double parity1Luma = parityLumaSums[1] / (parityCounts[1] * 3.0);
                double parityDelta = Math.Abs(parity0Luma - parity1Luma);
                if (parityDelta >= minimumComponentParityDelta)
                {
                    throw new InvalidOperationException(
                        $"Masked {atlas.Key} still contains a visible alternating checker component in cell " +
                        $"{atlasCell}: {writeIndex:N0} pixels at ({minimumX},{minimumY})-({maximumX},{maximumY}), " +
                        $"{exactColorPixels / (double)writeIndex:P1} exact empty-cell colors, " +
                        $"{parityDelta:0.00} luma delta.");
                }
            }
        }
    }

    Console.WriteLine(
        $"{atlas.Key} enclosed-checker regression: 0 visible alternating components " +
        $"across {atlas.UsedCellCount} used cells ({checkerTilesPerAxis}-tile learned period).");
}

static (int TilesPerAxis, double ParityLumaDelta) DetectCheckerPeriod(
    IReadOnlyList<Rgba32> pixels,
    int width,
    int height,
    MobyRasterIconAtlasContract atlas)
{
    int maximumTilesPerAxis = Math.Min(128, Math.Min(width, height) / 4);
    int bestTilesPerAxis = 0;
    double bestDelta = 0;
    for (int tilesPerAxis = 8; tilesPerAxis <= maximumTilesPerAxis; tilesPerAxis++)
    {
        long[] lumaSums = new long[2];
        int[] counts = new int[2];
        for (int atlasCell = atlas.UsedCellCount; atlasCell < atlas.CellCount; atlasCell++)
        {
            PixelRect bounds = MobyIconAtlasLoader.GetCellPixelRect(new PixelSize(width, height), atlas, atlasCell);
            for (int y = bounds.Y; y < bounds.Bottom; y += 2)
            {
                for (int x = bounds.X; x < bounds.Right; x += 2)
                {
                    int parity =
                        ((int)((long)x * tilesPerAxis / width) +
                        (int)((long)y * tilesPerAxis / height)) & 1;
                    Rgba32 pixel = pixels[(y * width) + x];
                    lumaSums[parity] += pixel.R + pixel.G + pixel.B;
                    counts[parity]++;
                }
            }
        }

        if (counts[0] == 0 || counts[1] == 0)
            continue;
        double parity0Luma = lumaSums[0] / (counts[0] * 3.0);
        double parity1Luma = lumaSums[1] / (counts[1] * 3.0);
        double delta = Math.Abs(parity0Luma - parity1Luma);
        if (delta > bestDelta)
        {
            bestDelta = delta;
            bestTilesPerAxis = tilesPerAxis;
        }
    }

    return (bestTilesPerAxis, bestDelta);
}

static int RgbKey(Rgba32 pixel) => (pixel.R << 16) | (pixel.G << 8) | pixel.B;

void SelectLevelForViewportFit(MainWindow owner, LevelDefinition level)
{
    MethodInfo selectLevelMethod = typeof(MainWindow).GetMethod(
        "SelectLevelAsync",
        BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Could not select a viewport-fit fixture level.");
    Task task = (Task)(selectLevelMethod.Invoke(owner, [level])
        ?? throw new InvalidOperationException($"Selecting {level.DisplayName} returned no task."));
    for (int attempt = 0; attempt < 2000 && !task.IsCompleted; attempt++)
    {
        Dispatcher.UIThread.RunJobs();
        Thread.Sleep(10);
    }
    if (!task.IsCompleted)
        throw new TimeoutException($"Selecting {level.DisplayName} exceeded 20 seconds during viewport-fit smoke.");
    task.GetAwaiter().GetResult();
    MethodInfo syncLevelPickers = typeof(MainWindow).GetMethod(
        "SyncLevelPickers",
        BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Could not synchronize the selected level in the UI smoke.");
    syncLevelPickers.Invoke(owner, [level]);
    FlushUi();
}

static void AssertViewportFit(string levelName, ViewportFitSnapshot snapshot)
{
    if (snapshot.FocusPointCount <= 0 || snapshot.FlyProjectedPointCount != snapshot.FocusPointCount)
    {
        throw new InvalidOperationException(
            $"{levelName} Fit did not project every playable-focus point ({snapshot.FlyProjectedPointCount}/{snapshot.FocusPointCount}).");
    }
    double pitchDegrees = snapshot.Pitch * 180 / Math.PI;
    if (pitchDegrees > -32 || pitchDegrees < -46)
        throw new InvalidOperationException($"{levelName} Fit pitch {pitchDegrees:0.0} degrees is not an elevated three-quarter overview.");

    Rect viewport = snapshot.ViewportBounds;
    Rect fly = snapshot.FlyProjectedBounds;
    if (fly.Left < viewport.Left - 1 || fly.Top < viewport.Top - 1 ||
        fly.Right > viewport.Right + 1 || fly.Bottom > viewport.Bottom + 1)
    {
        throw new InvalidOperationException($"{levelName} Fit projects playable terrain outside the viewport.");
    }

    double flyWidthRatio = fly.Width / viewport.Width;
    double flyHeightRatio = fly.Height / viewport.Height;
    if (Math.Max(flyWidthRatio, flyHeightRatio) < 0.55 || flyHeightRatio < 0.22)
    {
        throw new InvalidOperationException(
            $"{levelName} Fly Fit remains too small or horizon-flat ({flyWidthRatio:P0} wide, {flyHeightRatio:P0} tall). ");
    }

    Rect map = snapshot.MapProjectedBounds;
    double mapWidthRatio = map.Width / viewport.Width;
    double mapHeightRatio = map.Height / viewport.Height;
    if (Math.Max(mapWidthRatio, mapHeightRatio) < 0.74)
    {
        throw new InvalidOperationException(
            $"{levelName} Map Fit does not use the viewport ({mapWidthRatio:P0} wide, {mapHeightRatio:P0} tall).");
    }
}

void Render(double width, double height, string fileName)
{
    MainWindow window = new()
    {
        Width = width,
        Height = height,
        WindowStartupLocation = WindowStartupLocation.Manual,
        Position = new PixelPoint(0, 0)
    };
    window.Show();
    for (int attempt = 0; attempt < 20; attempt++)
    {
        Dispatcher.UIThread.RunJobs();
        Thread.Sleep(25);
    }
    WaitForLevelData(window);
    AvaloniaHeadlessPlatform.ForceRenderTimerTick(3);

    AssertText(window, "Objects");
    AssertText(window, "Level");
    AssertText(window, "Environment");
    AssertText(window, "Add");
    AssertText(window, "Edit");
    AssertText(window, "Replace");
    AssertText(window, "Build safety");
    AssertText(window, "Create BIN");
    FindButton(window, "Edit Map");
    FindButton(window, "Game Camera");
    if (window.GetLogicalDescendants().OfType<Button>().Any(button =>
            string.Equals(button.Content?.ToString(), "Playable View", StringComparison.Ordinal) ||
            string.Equals(button.Content?.ToString(), "Complete Scene", StringComparison.Ordinal) ||
            string.Equals(button.Content?.ToString(), "Game View: On", StringComparison.Ordinal) ||
            string.Equals(button.Content?.ToString(), "Raw Capture View", StringComparison.Ordinal)))
    {
        throw new InvalidOperationException("The normal release shell exposed a retired terrain-presentation control.");
    }
    FindButton(window, "Inspect Build Safety");
    AssertWorkspaceTabContrast(window);

    using Avalonia.Media.Imaging.Bitmap bitmap = window.CaptureRenderedFrame()
        ?? throw new InvalidOperationException($"Avalonia did not render the {width}x{height} editor frame.");
    string path = Path.Combine(outputDirectory, fileName);
    bitmap.Save(path);
    Console.WriteLine($"Rendered {width}x{height}: {path}");

    if (width >= 1400)
    {
        AssertTerrainCatalogAtomicGuards(window);
        RenderWorkspaceTabs(window);
        RenderObjectDialogs(window);
        RenderPreviousBetaProjectReminder(window);
        RenderUpdateNotification(window);
    }

    window.Close();
}

void AssertTerrainCatalogAtomicGuards(MainWindow owner)
{
    Task<string> task = owner.AssertTerrainCatalogAtomicGuardsForTestingAsync();
    for (int attempt = 0; attempt < 1000 && !task.IsCompleted; attempt++)
    {
        Dispatcher.UIThread.RunJobs();
        Thread.Sleep(5);
    }
    string result = task.GetAwaiter().GetResult();
    Console.WriteLine($"Terrain catalog atomic UI: {result}.");
}

void RenderPreviousBetaProjectReminder(MainWindow owner)
{
    ReleaseProjectContext? context = ReleaseProjectBootstrap.Current;
    string markerPath = context == null
        ? ""
        : Path.Combine(context.UserData.SettingsPath, "beta-v2-project-reminder.json");
    owner.ShowPreviousBetaProjectReminderForTesting();
    FlushUi();
    if (markerPath.Length > 0 && File.Exists(markerPath))
        throw new InvalidOperationException("The Beta V1 reminder was marked handled merely because it was displayed.");
    AssertTextContains(owner, "Already used Spyro Editor Beta V1");
    FindButton(owner, "Import Beta V1 Project");
    SaveFrame(owner, "spyro-editor-beta-v1-import-reminder.png");
    FindButton(owner, "Not Needed").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    FlushUi();
    if (markerPath.Length > 0)
    {
        for (int attempt = 0; attempt < 100 && !File.Exists(markerPath); attempt++)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(5);
        }
        if (!File.Exists(markerPath))
            throw new InvalidOperationException("Explicitly dismissing the Beta V1 reminder did not persist its handled state.");
    }
    Console.WriteLine("Beta V1 migration UI: one-time copy-import reminder and protected-project action rendered.");
}

void RenderUpdateNotification(MainWindow owner)
{
    const string notes = "# Spyro Editor Beta V3.1\n\n- Keeps project edits untouched.\n- Bundles the next tested editor improvements.";
    EditorUpdateInfo update = new(
        BetaVersion: 3,
        ReleaseTag: "beta-v3.1",
        DisplayName: "Spyro Editor Beta V3.1",
        ReleaseNotes: notes,
        ReleasePage: new Uri("https://github.com/monty19933-hub/Spyro1Editor/releases/tag/beta-v3.1"),
        Prerelease: true,
        Asset: new EditorUpdateAsset(
            "SpyroEditor-Beta-V3.1-osx-arm64.zip",
            new Uri("https://github.com/monty19933-hub/Spyro1Editor/releases/download/beta-v3.1/SpyroEditor-Beta-V3.1-osx-arm64.zip"),
            54_000_000,
            new string('a', 64)),
        PublicVersion: "3.1");
    owner.ShowUpdateNotificationForTesting(update);
    FlushUi();
    AssertTextContains(owner, "Spyro Editor Beta V3.1 is available");
    Button details = FindButton(owner, "What's New & Download");
    SaveFrame(owner, "spyro-editor-update-notification.png");

    Window dialog = OpenAsyncDialog(
        owner,
        details,
        "Update Available - Spyro Editor Beta V3.1",
        "incremental beta update changelog");
    AssertText(dialog, "Spyro Editor Beta V3.1");
    AssertText(dialog, "What's new in Spyro Editor Beta V3.1");
    AssertTextContains(dialog, "Keeps project edits untouched");
    FindButton(dialog, "View Full Release Notes");
    FindButton(dialog, "Download Update");
    SaveFrame(dialog, "spyro-editor-update-changelog.png");
    FindButton(dialog, "Later").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    WaitForDialogToClose(owner, dialog, "incremental beta update changelog");
    Console.WriteLine("Update notification UI: schema-2 state migration plus visible Beta V3.1 banner, scrollable changelog, project-safety message, and download action rendered.");
}

static void WaitForLevelData(MainWindow window)
{
    EditorViewport viewport = window.GetLogicalDescendants()
        .OfType<EditorViewport>()
        .First();
    for (int attempt = 0; attempt < 1000 && (viewport.Geometry == null || viewport.Mobys.Count == 0); attempt++)
    {
        Dispatcher.UIThread.RunJobs();
        Thread.Sleep(10);
    }

    if (viewport.Geometry == null || viewport.Mobys.Count == 0)
        throw new InvalidOperationException("Stone Hill did not finish loading before the headless UI captures.");
    FlushUi();
}

void AssertWorkspaceTabContrast(MainWindow window)
{
    TabControl workspaceTabs = window.GetLogicalDescendants()
        .OfType<TabControl>()
        .First(tabs => tabs.ItemsSource is IEnumerable<TabItem> items &&
            items.Any(item => string.Equals((item.Header as TextBlock)?.Text, "Objects", StringComparison.OrdinalIgnoreCase)));

    foreach (TabItem tab in (IEnumerable<TabItem>)workspaceTabs.ItemsSource!)
    {
        if (tab.Header is not TextBlock label || label.Foreground is null)
            throw new InvalidOperationException("A workspace tab is missing its explicit readable label color.");
    }
}

void RenderWorkspaceTabs(MainWindow window)
{
    RenderWorkspaceTab(window.Width, window.Height, 1, "spyro-editor-level-tab.png");
    RenderWorkspaceTab(window.Width, window.Height, 2, "spyro-editor-environment-tab.png");
}

void RenderWorkspaceTab(double width, double height, int selectedIndex, string fileName)
{
    MainWindow window = new()
    {
        Width = width,
        Height = height,
        WindowStartupLocation = WindowStartupLocation.Manual,
        Position = new PixelPoint(0, 0)
    };
    TabControl workspaceTabs = window.GetLogicalDescendants()
        .OfType<TabControl>()
        .First(tabs => tabs.ItemsSource is IEnumerable<TabItem> items &&
            items.Any(item => string.Equals((item.Header as TextBlock)?.Text, "Objects", StringComparison.OrdinalIgnoreCase)));
    workspaceTabs.SelectedIndex = selectedIndex;
    window.Show();
    try
    {
        WaitForLevelData(window);
        SaveFrame(window, fileName);
    }
    finally
    {
        window.Close();
        FlushUi();
    }
}

void RenderObjectDialogs(MainWindow owner)
{
    Button add = FindButton(owner, "Add");
    Window addDialog = OpenDialog(owner, add, "add object window");
    AssertText(addDialog, "Add object");
    Expander addTechnical = addDialog.GetLogicalDescendants()
        .OfType<Expander>()
        .First(expander => string.Equals((expander.Header as TextBlock)?.Text, "Exact position", StringComparison.Ordinal));
    addTechnical.IsExpanded = true;
    FlushUi();
    AssertTextAbsent(addDialog, "Render radius (+0x50)");
    AssertTextAbsent(addDialog, "Was drawn (+0x51)");
    SaveFrame(addDialog, "spyro-editor-add-object-dialog.png");
    addDialog.Close(false);
    FlushUi();

    ListBox browser = owner.GetLogicalDescendants()
        .OfType<ListBox>()
        .First(list => list.ItemsSource is IEnumerable<Moby> mobys && mobys.Any(moby => moby.VisualKind == MobyVisualKind.Chest));
    Moby chest = ((IEnumerable<Moby>)browser.ItemsSource!)
        .First(moby => moby.VisualKind == MobyVisualKind.Chest && !moby.IsRemoved && !moby.IsAdded);
    browser.SelectedItem = chest;
    FlushUi();

    Slider zSlider = owner.GetLogicalDescendants()
        .OfType<Slider>()
        .First(slider => string.Equals(slider.Name, "SelectedMobyZSlider", StringComparison.Ordinal));
    CheckBox snapZ = owner.GetLogicalDescendants()
        .OfType<CheckBox>()
        .First(box => string.Equals(box.Name, "SelectedMobySnapZBox", StringComparison.Ordinal));
    if (!zSlider.IsEnabled || !snapZ.IsEnabled || snapZ.IsChecked != true)
        throw new InvalidOperationException("Selected-object Z controls did not enable with terrain snapping on for an ordinary chest.");

    float originalZ = chest.Position.Z;
    double targetZ = Math.Min(zSlider.Maximum, originalZ + 8);
    zSlider.Value = targetZ;
    FlushUi();
    if (Math.Abs(chest.Position.Z - targetZ) > 0.01)
        throw new InvalidOperationException($"Selected-object Z slider did not move the chest: expected {targetZ:0.###}, got {chest.Position.Z:0.###}.");
    if (snapZ.IsChecked != false)
        throw new InvalidOperationException("Manual selected-object Z movement did not turn terrain-Z snapping off.");

    AssertBuildSafetyInspection(owner);

    zSlider.Value = originalZ;
    FlushUi();
    snapZ.IsChecked = true;
    FlushUi();
    if (snapZ.IsChecked != true)
        throw new InvalidOperationException("Selected-object terrain-Z snap could not be re-enabled after manual Z movement.");
    ScrollViewer objectWorkspaceScroll = owner.GetLogicalDescendants()
        .OfType<ScrollViewer>()
        .First(scroll => scroll.GetLogicalDescendants().Contains(zSlider));
    objectWorkspaceScroll.Offset = new Vector(0, 0);
    FlushUi();
    SaveFrame(owner, "spyro-editor-object-z-controls.png");
    AssertStoneHillControlIdentityAndTransportAnchors(owner, browser, chest);

    Button replace = FindButton(owner, "Replace");
    if (!replace.IsEnabled)
        throw new InvalidOperationException($"Replace did not enable for ordinary chest {chest.DisplayLabel}.");
    Window swapDialog = OpenDialog(owner, replace, "swap catalogue");
    AssertText(swapDialog, $"Replace {chest.DisplayLabel}");
    SaveFrame(swapDialog, "spyro-editor-swap-catalog.png");

    ComboBox sourceFilter = swapDialog.GetLogicalDescendants()
        .OfType<ComboBox>()
        .First(box => box.ItemsSource is IEnumerable<string> values && values.Contains("All available", StringComparer.Ordinal));
    AssertTextAbsent(swapDialog, "Actor class:");
    AssertTextAbsent(swapDialog, "Compatibility:");
    FindButton(swapDialog, "Use This Object");
    SaveFrame(swapDialog, "spyro-editor-swap-catalog-available.png");
    swapDialog.Close(null);
    FlushUi();

    Button edit = FindButton(owner, "Edit");
    Window editDialog = OpenDialog(owner, edit, "object editor");
    AssertTextAbsent(editDialog, "Technical properties");
    AssertTextAbsent(editDialog, "Render radius (+0x50)");
    AssertTextAbsent(editDialog, "Was drawn (+0x51)");
    AssertTextAbsent(editDialog, "Native class (+0x36/+0x37)");
    SaveFrame(editDialog, "spyro-editor-object-dialog.png");
    editDialog.Close(false);
    FlushUi();

    AssertLooseGemValueEdit(owner, browser);
    AssertGemFirstThenTransformReset(owner, browser);
    AssertRewardCarrierGemLabelEdit(owner, browser);
    AssertLinkedChestNoOpApply(owner, browser);
    AssertIncompatibleLinkedChestTransformAndUndo(owner, browser);
    AssertLinkedChildTransformKeepsCompatibleSiblings(owner, browser);
    AssertLinkedContentRemoveAndUndo(owner, browser);
    AssertAddedLinkedUndoCleansTopology(owner, browser);
    AssertPreEditedGemTransformAndWholeObjectUndo(owner, browser);
    AssertLinkedChestContentGemLabelEdit(owner, browser);
    AssertBuildSafetyIssueNavigation(owner);
    AssertObservedIdentitySavePreservesScopes(owner, browser);
}

void RunObjectGalleryOnly()
{
    IReadOnlyList<MobyIconAtlasDiagnostics> generatedIconAtlases =
        ObjectGalleryIconCatalog.LoadDiagnosticsForTesting();
    if (generatedIconAtlases.Count != 12 ||
        generatedIconAtlases.Any(diagnostic =>
            !diagnostic.Succeeded ||
            diagnostic.Width < 1200 ||
            diagnostic.Height < 1200 ||
            diagnostic.CheckerboardSampleCoverage < 0.98 ||
            diagnostic.Cells
                .Where(cell => cell.AtlasCell < diagnostic.Atlas.UsedCellCount)
                .Any(cell => cell.OpaquePixelCount < 5000) ||
            diagnostic.Cells
                .Where(cell => cell.AtlasCell >= diagnostic.Atlas.UsedCellCount)
                .Any(cell => cell.OpaquePixelCount != 0)))
    {
        throw new InvalidOperationException(
            "One or more high-definition object-gallery atlases failed their decode, subject, or empty-cell contract:\n" +
            string.Join(
                "\n",
                generatedIconAtlases.Select(diagnostic =>
                    $"{diagnostic.Atlas.Key}: success={diagnostic.Succeeded}, size={diagnostic.Width}x{diagnostic.Height}, " +
                    $"checker={diagnostic.CheckerboardSampleCoverage:P1}, cells=" +
                    string.Join(",", diagnostic.Cells.Select(cell => $"{cell.AtlasCell}:{cell.OpaquePixelCount}")) +
                    (string.IsNullOrWhiteSpace(diagnostic.FailureReason) ? "" : $", error={diagnostic.FailureReason}"))));
    }
    if (ObjectGalleryIconCatalog.DefinitionCount != 114)
    {
        throw new InvalidOperationException(
            $"The high-definition object-gallery catalog exposed {ObjectGalleryIconCatalog.DefinitionCount} families instead of 114.");
    }
    string[] requiredHdGalleryLabels =
    [
        "Armored Gnorc",
        "Barrel Engineer",
        "Beast",
        "Beast Makers Banner",
        "Bull",
        "Caged Fairy",
        "Campfire",
        "Chicken Cage",
        "Clock Fool",
        "Crocodile",
        "Dockworker TNT Wrangler",
        "Drawbridge Lever",
        "Fairy Cage Prop",
        "Fat Bat",
        "Fat Claw Monster",
        "Flight Direction Arrow Sign",
        "Floor Shocker",
        "Lantern Post",
        "Metal Claw Monster",
        "Ram",
        "Rescue Fairy",
        "Shepard",
        "Shepherd",
        "Shielded Greenie",
        "Summoning Wizard",
        "Volt Shooter",
        "Wall Lantern"
    ];
    string[] unresolvedHdGalleryLabels = requiredHdGalleryLabels
        .Where(label => !ObjectGalleryIconCatalog.TryResolve(label, out _))
        .ToArray();
    if (unresolvedHdGalleryLabels.Length > 0)
    {
        throw new InvalidOperationException(
            $"Public object-gallery labels still use fallback previews: {string.Join(", ", unresolvedHdGalleryLabels)}.");
    }

    MainWindow window = new()
    {
        Width = 1440,
        Height = 900,
        WindowStartupLocation = WindowStartupLocation.Manual,
        Position = new PixelPoint(0, 0)
    };
    window.Show();
    try
    {
        WaitForLevelData(window);
        Window addDialog = OpenDialog(window, FindButton(window, "Add"), "add object window");
        Button chooser = FindNamed<Button>(addDialog, "ObjectGalleryButton");
        if (!chooser.Content?.ToString()?.StartsWith("Choose Object", StringComparison.Ordinal) == true)
            throw new InvalidOperationException("The Add Object dialog did not replace the technical object dropdown with the picture-gallery button.");

        chooser.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, chooser));
        Window? galleryDialog = null;
        for (int attempt = 0; attempt < 200 && galleryDialog == null; attempt++)
        {
            Dispatcher.UIThread.RunJobs();
            galleryDialog = addDialog.OwnedWindows.LastOrDefault(candidate =>
                string.Equals(candidate.Title, "Choose Object", StringComparison.Ordinal));
            Thread.Sleep(10);
        }
        if (galleryDialog == null)
            throw new InvalidOperationException("The Choose Object picture gallery did not open.");
        ListBox gallery = FindNamed<ListBox>(galleryDialog, "ObjectGalleryList");
        object[] items = ReadItemsSource(gallery, "safe object gallery");
        if (items.Length < 2)
            throw new InvalidOperationException($"The Stone Hill safe object gallery exposed only {items.Length} item(s).");
        if (items.Any(item => !TemplateValue<bool>(item, "IsCreateBinSafe")))
            throw new InvalidOperationException("The object gallery exposed an item that was not approved for normal Create BIN.");

        string[] names = items.Select(item => TemplateValue<string>(item, "DisplayName")).ToArray();
        object[] gemItems = items
            .Where(item => string.Equals(
                TemplateValue<string>(item, "DisplayName"),
                "Gem / treasure",
                StringComparison.Ordinal))
            .ToArray();
        if (gemItems.Length != 1)
            throw new InvalidOperationException($"The object gallery exposed {gemItems.Length} Gem / treasure tiles instead of one.");
        object gemTemplate = TemplateValue<object>(gemItems[0], "Template");
        if (!TemplateValue<bool>(gemTemplate, "UsesGem") ||
            TemplateValue<int>(gemTemplate, "Type") != 0x18 ||
            TemplateValue<int>(gemTemplate, "SourceByte36") != 0x53 ||
            TemplateValue<int>(gemTemplate, "SourceByte37") != 0x00 ||
            TemplateValue<int>(gemTemplate, "SourceByte4F") != 0x01 ||
            TemplateValue<int>(gemTemplate, "Flag4A") != 0x40 ||
            TemplateValue<int>(gemTemplate, "Flag4B") != 0xFF)
        {
            throw new InvalidOperationException("The single Gem / treasure tile was not the canonical red loose-gem template.");
        }

        object[] flameChargeChests = items
            .Where(item => ObjectGalleryActorId(item) == 0x00C2)
            .ToArray();
        object[] chargeChests = items
            .Where(item => ObjectGalleryActorId(item) == 0x00C3)
            .ToArray();
        if (flameChargeChests.Length != 1 ||
            !string.Equals(
                TemplateValue<string>(flameChargeChests[0], "DisplayName"),
                "Flame/charge chest",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"The object gallery did not collapse Flame/charge reward variants to one exact tile: " +
                $"{string.Join(" | ", flameChargeChests.Select(item => TemplateValue<string>(item, "DisplayName")))}");
        }
        if (chargeChests.Length != 1 ||
            !string.Equals(
                TemplateValue<string>(chargeChests[0], "DisplayName"),
                "Charge chest",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"The object gallery did not collapse Charge chest reward variants to one exact tile: " +
                $"{string.Join(" | ", chargeChests.Select(item => TemplateValue<string>(item, "DisplayName")))}");
        }
        if (names.Any(name =>
                name.StartsWith("Red gem", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("Green gem", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("Blue gem", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("Yellow gem", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("Purple gem", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Flame/charge chest (", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Charge chest (", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"The object gallery retained a redundant gem/chest reward variant: {string.Join(" | ", names)}");
        }

        string[] forbidden =
        [
            "From this level",
            "borrowed",
            "native donor",
            "runtime-proven",
            "recipe"
        ];
        if (names.Any(name => forbidden.Any(word => name.Contains(word, StringComparison.OrdinalIgnoreCase))))
        {
            throw new InvalidOperationException(
                $"The object gallery leaked technical provenance wording: {string.Join(" | ", names)}");
        }

        FlushUi();
        Button[] tiles = galleryDialog.GetLogicalDescendants()
            .OfType<Button>()
            .Where(button => string.Equals(button.Name, "ObjectGalleryTileButton", StringComparison.Ordinal))
            .ToArray();
        if (tiles.Length == 0 ||
            galleryDialog.GetLogicalDescendants()
                .OfType<Control>()
                .Count(control => string.Equals(control.Name, "ObjectGalleryPreview", StringComparison.Ordinal)) == 0)
        {
            throw new InvalidOperationException("The object gallery did not render picture-first object tiles.");
        }
        SaveFrame(galleryDialog, "spyro-editor-object-gallery.png");

        Button selectedTile = tiles.Length > 1 ? tiles[1] : tiles[0];
        object selectedItem = selectedTile.Tag
            ?? throw new InvalidOperationException("The object gallery tile did not retain its safe template.");
        string selectedName = TemplateValue<string>(selectedItem, "DisplayName");
        selectedTile.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, selectedTile));
        for (int attempt = 0; attempt < 200 && addDialog.OwnedWindows.Contains(galleryDialog); attempt++)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(10);
        }
        if (addDialog.OwnedWindows.Contains(galleryDialog))
            throw new InvalidOperationException("The object gallery did not close after choosing a picture tile.");
        FlushUi();
        if (chooser.Content?.ToString()?.Contains(selectedName, StringComparison.OrdinalIgnoreCase) != true)
            throw new InvalidOperationException($"Choosing '{selectedName}' did not update the Add Object selection.");

        Button add = FindButton(addDialog, "Add object");
        add.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, add));
        WaitForDialogToClose(window, addDialog, "add object");
        EditorViewport viewport = window.GetLogicalDescendants().OfType<EditorViewport>().Single();
        if (!viewport.ObjectPlacementMode)
            throw new InvalidOperationException("Choosing a gallery object did not continue into the existing click-to-place flow.");
        viewport.ObjectPlacementMode = false;

        Console.WriteLine(
            $"Object gallery UI: {items.Length} normal-Create-BIN-safe picture tile(s), concise names, click selection, and existing viewport placement flow passed.");
    }
    finally
    {
        foreach (Window owned in window.OwnedWindows.ToArray())
            owned.Close();
        window.Close();
        FlushUi();
    }
}

int ObjectGalleryActorId(object galleryItem)
{
    object template = TemplateValue<object>(galleryItem, "Template");
    return ((TemplateValue<int>(template, "SourceByte37") & 0xFF) << 8) |
        (TemplateValue<int>(template, "SourceByte36") & 0xFF);
}

void AssertLooseGemValueEdit(MainWindow owner, ListBox browser)
{
    Moby gem = ((IEnumerable<Moby>)browser.ItemsSource!)
        .First(moby => !moby.IsRemoved && !moby.IsAdded && moby.IsVisibleGem && moby.Gem == GemValue.Red);
    var original = new
    {
        gem.Position,
        gem.Type,
        gem.State,
        gem.YawByte,
        gem.SourceByte36,
        gem.SourceByte37,
        gem.SourceByte4F,
        gem.Flag4A,
        gem.Flag4B,
        gem.Label,
        gem.Color,
        gem.HasLoadedNativeEdit,
        gem.LoadedNativeEditSummary,
        gem.IsRemoved
    };
    browser.SelectedItem = gem;
    FlushUi();

    Button edit = FindButton(owner, "Edit");
    if (!edit.IsEnabled)
        throw new InvalidOperationException($"Edit did not enable for loose gem {gem.DisplayLabel}.");
    Window dialog = OpenDialog(owner, edit, "loose-gem editor");
    AssertText(dialog, "Gem value");
    ComboBox valueBox = dialog.GetLogicalDescendants()
        .OfType<ComboBox>()
        .Single(box => box.ItemsSource is IEnumerable<GemValue>);
    GemValue[] options = ((IEnumerable<GemValue>)valueBox.ItemsSource!).ToArray();
    if (!options.SequenceEqual(GemValue.Known))
        throw new InvalidOperationException("The loose-gem editor did not expose red, green, blue, yellow, and purple values in order.");

    TextBox nameBox = dialog.GetLogicalDescendants()
        .OfType<TextBox>()
        .Single(box => string.Equals(box.Name, "MobyNameBox", StringComparison.Ordinal));
    TextBox renderRadiusBox = dialog.GetLogicalDescendants()
        .OfType<TextBox>()
        .Single(box => string.Equals(box.Name, "MobyRenderRadiusBox", StringComparison.Ordinal));

    valueBox.SelectedItem = GemValue.Green;
    FlushUi();
    if (!string.Equals(nameBox.Text, GemValue.Green.DisplayName, StringComparison.Ordinal))
        throw new InvalidOperationException($"The loose-gem editor did not update its generated name after selecting green: '{nameBox.Text}'.");
    valueBox.SelectedItem = GemValue.Purple;
    FlushUi();
    if (!string.Equals(nameBox.Text, GemValue.Purple.DisplayName, StringComparison.Ordinal))
        throw new InvalidOperationException($"The loose-gem editor did not update its generated name to the final purple selection: '{nameBox.Text}'.");

    renderRadiusBox.Text = "0x20";
    FlushUi();
    Button apply = FindButton(dialog, "Apply changes");
    apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, apply));
    FlushUi();
    if (!owner.OwnedWindows.Contains(dialog))
        throw new InvalidOperationException("The loose-gem editor closed after an invalid combined render-radius and gem edit.");
    AssertTextContains(dialog, "Gem value requires render radius 0x18");
    if (gem.Position != original.Position ||
        gem.Type != original.Type ||
        gem.State != original.State ||
        gem.YawByte != original.YawByte ||
        gem.SourceByte36 != original.SourceByte36 ||
        gem.SourceByte37 != original.SourceByte37 ||
        gem.SourceByte4F != original.SourceByte4F ||
        gem.Flag4A != original.Flag4A ||
        gem.Flag4B != original.Flag4B ||
        !string.Equals(gem.Label, original.Label, StringComparison.Ordinal) ||
        gem.Color != original.Color ||
        gem.HasLoadedNativeEdit != original.HasLoadedNativeEdit ||
        !string.Equals(gem.LoadedNativeEditSummary, original.LoadedNativeEditSummary, StringComparison.Ordinal) ||
        gem.IsRemoved != original.IsRemoved)
    {
        throw new InvalidOperationException("The invalid combined render-radius and gem edit partially mutated the selected Moby.");
    }

    renderRadiusBox.Text = "0x18";
    FlushUi();
    SaveFrame(dialog, "spyro-editor-gem-value-dialog.png");
    apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, apply));
    WaitForDialogToClose(owner, dialog, "loose-gem editor");

    if (gem.Gem != GemValue.Purple ||
        gem.SourceByte36 != GemValue.Purple.IdByte ||
        gem.SourceByte4F != GemValue.Purple.ValueByte ||
        !gem.HasMetadataEdit)
    {
        throw new InvalidOperationException(
            $"The loose-gem editor did not apply purple ID/value bytes: 0x{gem.SourceByte36:X2}/0x{gem.SourceByte4F:X2}.");
    }
    if (!string.Equals(gem.DisplayLabel, GemValue.Purple.DisplayName, StringComparison.Ordinal))
        throw new InvalidOperationException($"The loose-gem editor applied purple bytes but retained the stale generated label '{gem.DisplayLabel}'.");
    AssertTextContains(owner, "Purple gem (25)");
    Console.WriteLine($"Gem value UI: {gem.DisplayIndex} red -> green -> purple; invalid radius stayed transactional; bytes 0x53/0x01 -> 0x57/0x05.");
}

void AssertGemFirstThenTransformReset(MainWindow owner, ListBox browser)
{
    Moby[] mobys = ((IEnumerable<Moby>)browser.ItemsSource!).ToArray();
    Moby carrier = FindGeneratedRewardCarrier(mobys);
    browser.SelectedItem = carrier;
    FlushUi();

    Window dialog = OpenDialog(owner, FindButton(owner, "Edit"), "gem-first transform editor");
    ComboBox gemBox = FindNamed<ComboBox>(dialog, "MobyGemValueBox");
    ComboBox transformBox = FindNamed<ComboBox>(dialog, "MobyTransformBox");
    TextBox nameBox = FindNamed<TextBox>(dialog, "MobyNameBox");
    TextBox typeBox = FindNamed<TextBox>(dialog, "MobyRenderRadiusBox");
    TextBox yawBox = FindNamed<TextBox>(dialog, "MobyYawBox");
    string originalName = nameBox.Text ?? "";
    string originalType = typeBox.Text ?? "";
    string originalYaw = yawBox.Text ?? "";

    gemBox.SelectedItem = GemValue.Purple;
    FlushUi();
    string purpleName = carrier.SuggestedRewardGemLabel(GemValue.Purple, originalName);
    if (!string.Equals(nameBox.Text, purpleName, StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            $"Gem-first transform setup did not produce the generated purple reward name: expected '{purpleName}', got '{nameBox.Text}'.");
    }

    object replacement = FindNonChestLevelTransform(transformBox, mobys);
    string replacementLabel = TemplateValue<string>(replacement, "DefaultLabel");
    int replacementType = TemplateValue<int>(replacement, "Type");
    int replacementYaw = TemplateValue<int>(replacement, "YawByte");
    transformBox.SelectedItem = replacement;
    FlushUi();
    if (!string.Equals(nameBox.Text, replacementLabel, StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            $"Selecting a transform after the gem value left its generated gem name behind: expected '{replacementLabel}', got '{nameBox.Text}'.");
    }
    if (!string.Equals(typeBox.Text, $"0x{replacementType:X2}", StringComparison.Ordinal) ||
        !string.Equals(yawBox.Text, FormatYawForSmoke(replacementYaw), StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            $"The selected transform did not populate its type/yaw controls: '{typeBox.Text}' / '{yawBox.Text}'.");
    }

    transformBox.SelectedIndex = 0;
    FlushUi();
    if (!string.Equals(nameBox.Text, purpleName, StringComparison.Ordinal) ||
        !string.Equals(typeBox.Text, originalType, StringComparison.Ordinal) ||
        !string.Equals(yawBox.Text, originalYaw, StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            $"Keep Current did not restore the pre-transform generated name and original type/yaw after a gem-first transform: '{nameBox.Text}' / '{typeBox.Text}' / '{yawBox.Text}'.");
    }

    FindButton(dialog, "Cancel").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    WaitForDialogToClose(owner, dialog, "gem-first transform editor");
    Console.WriteLine($"Gem-first transform UI: generated reward name changed to '{replacementLabel}', then Keep Current restored the selected-gem name '{purpleName}'.");
}

void AssertRewardCarrierGemLabelEdit(MainWindow owner, ListBox browser)
{
    Moby carrier = FindGeneratedRewardCarrier((IEnumerable<Moby>)browser.ItemsSource!);
    string expectedLabel = carrier.SuggestedRewardGemLabel(GemValue.Purple, carrier.Label);
    if (string.Equals(expectedLabel, carrier.Label, StringComparison.Ordinal))
        throw new InvalidOperationException($"The generated reward label was not recognized before opening the editor: '{carrier.Label}'.");

    browser.SelectedItem = carrier;
    FlushUi();
    Window dialog = OpenDialog(owner, FindButton(owner, "Edit"), "reward-carrier editor");
    ComboBox gemBox = dialog.GetLogicalDescendants()
        .OfType<ComboBox>()
        .Single(box => string.Equals(box.Name, "MobyGemValueBox", StringComparison.Ordinal));
    TextBox nameBox = dialog.GetLogicalDescendants()
        .OfType<TextBox>()
        .Single(box => string.Equals(box.Name, "MobyNameBox", StringComparison.Ordinal));

    gemBox.SelectedItem = GemValue.Purple;
    FlushUi();
    if (!string.Equals(nameBox.Text, expectedLabel, StringComparison.Ordinal))
        throw new InvalidOperationException($"Reward-carrier selection left a stale label: expected '{expectedLabel}', got '{nameBox.Text}'.");

    Button apply = FindButton(dialog, "Apply changes");
    apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, apply));
    WaitForDialogToClose(owner, dialog, "reward-carrier editor");
    if (carrier.Flag4B != GemValue.Purple.IdByte ||
        carrier.RewardGem != GemValue.Purple ||
        !string.Equals(carrier.Label, expectedLabel, StringComparison.Ordinal) ||
        !carrier.HasMetadataEdit)
    {
        throw new InvalidOperationException(
            $"Reward-carrier edit did not keep byte and label together: 0x{carrier.Flag4B:X2}, '{carrier.Label}'.");
    }

    Console.WriteLine($"Reward gem UI: {carrier.DisplayIndex} -> purple byte 0x{carrier.Flag4B:X2}; generated role label stayed synchronized.");
}

void AssertLinkedChestNoOpApply(MainWindow owner, ListBox browser)
{
    Moby[] mobys = ((IEnumerable<Moby>)browser.ItemsSource!).ToArray();
    (Moby chest, Moby[] contents) = FindCleanLinkedChest(mobys);
    MobyRegressionSnapshot chestBefore = CaptureMoby(chest);
    Dictionary<int, MobyRegressionSnapshot> before = contents.ToDictionary(moby => moby.TrueIndex, CaptureMoby);
    MobyLinkSemanticSnapshot[] linksBefore = CaptureMobyLinkSemantics([chest, .. contents]);
    if (before.Values.Any(snapshot => snapshot.HasAnyEdit))
        throw new InvalidOperationException("The no-op chest regression did not start with clean linked content rows.");

    browser.SelectedItem = chest;
    FlushUi();
    Window dialog = OpenDialog(owner, FindButton(owner, "Edit"), "linked chest no-op editor");
    Button apply = FindButton(dialog, "Apply changes");
    apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, apply));
    WaitForDialogToClose(owner, dialog, "linked chest no-op editor");

    AssertMobyMatchesSnapshot(chest, chestBefore, $"No-op chest Apply changed parent {chest.DisplayIndex}");
    foreach (Moby content in contents)
    {
        AssertMobyMatchesSnapshot(content, before[content.TrueIndex], $"No-op chest Apply changed linked content T{content.TrueIndex}");
        if (content.HasAnyEdit)
            throw new InvalidOperationException($"No-op chest Apply marked linked content T{content.TrueIndex} as edited.");
    }
    AssertMobyLinkSemantics([chest, .. contents], linksBefore, $"No-op chest Apply changed semantic links for {chest.DisplayIndex}");

    Console.WriteLine($"Linked chest no-op UI: {chest.DisplayIndex} and {contents.Length} child row(s) stayed exact; link keys, kinds, and member sets were unchanged.");
}

void AssertIncompatibleLinkedChestTransformAndUndo(MainWindow owner, ListBox browser)
{
    Moby[] mobys = ((IEnumerable<Moby>)browser.ItemsSource!).ToArray();
    (Moby chest, Moby[] contents) = FindCleanLinkedChest(mobys);
    MobyRegressionSnapshot rootBefore = CaptureMoby(chest);
    Dictionary<int, MobyRegressionSnapshot> childrenBefore = contents.ToDictionary(moby => moby.TrueIndex, CaptureMoby);
    HashSet<int> originalLinkedIndexes = LinkedChestContentIndexes(chest);
    if (!contents.All(content => originalLinkedIndexes.Contains(content.TrueIndex)))
        throw new InvalidOperationException($"The linked chest transform setup did not include every content row for {chest.DisplayIndex}.");

    browser.SelectedItem = chest;
    FlushUi();
    Window dialog = OpenDialog(owner, FindButton(owner, "Edit"), "incompatible linked chest transform editor");
    ComboBox transformBox = FindNamed<ComboBox>(dialog, "MobyTransformBox");
    object replacement = FindNonChestLevelTransform(transformBox, mobys);
    int donorTrueIndex = TemplateValue<int>(replacement, "SourceTrueIndex");
    string replacementLabel = TemplateValue<string>(replacement, "DefaultLabel");
    transformBox.SelectedItem = replacement;
    FlushUi();

    TextBox xBox = FindNamed<TextBox>(dialog, "MobyPositionXBox");
    TextBox yBox = FindNamed<TextBox>(dialog, "MobyPositionYBox");
    TextBox zBox = FindNamed<TextBox>(dialog, "MobyPositionZBox");
    Vector3f target = new(chest.Position.X + 7.25f, chest.Position.Y + 5.5f, chest.Position.Z + 3.75f);
    xBox.Text = target.X.ToString("0.###", CultureInfo.InvariantCulture);
    yBox.Text = target.Y.ToString("0.###", CultureInfo.InvariantCulture);
    zBox.Text = target.Z.ToString("0.###", CultureInfo.InvariantCulture);
    FlushUi();

    Button apply = FindButton(dialog, "Apply changes");
    apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, apply));
    WaitForDialogToClose(owner, dialog, "incompatible linked chest transform editor");
    if (chest.Position != target || chest.SourceCloneTrueIndex != donorTrueIndex ||
        !string.Equals(chest.Label, replacementLabel, StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            $"The incompatible chest transform did not update only its root as requested: position {chest.Position}, donor T{chest.SourceCloneTrueIndex}, label '{chest.Label}'.");
    }
    if (LinkedChestContentIndexes(chest).Count != 0 || contents.Any(content =>
            content.Links.Any(link =>
                string.Equals(link.Kind, "chest contents", StringComparison.OrdinalIgnoreCase) &&
                link.TrueIndexes.Contains(chest.TrueIndex))))
    {
        throw new InvalidOperationException("The non-chest replacement retained a stale chest-content relationship.");
    }
    foreach (Moby content in contents)
        AssertMobyMatchesSnapshot(content, childrenBefore[content.TrueIndex], $"Transforming chest {chest.DisplayIndex} moved or mutated child T{content.TrueIndex}");

    if (!ReferenceEquals(browser.SelectedItem, chest))
        throw new InvalidOperationException("Applying the linked chest transform did not keep its root selected for Undo.");
    Button undo = FindButton(owner, "Undo");
    if (!undo.IsEnabled)
        throw new InvalidOperationException("Undo did not enable after transforming and detaching a linked chest root.");
    undo.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, undo));
    FlushUi();

    AssertMobyMatchesSnapshot(chest, rootBefore, $"Undo did not restore linked chest root {chest.DisplayIndex}");
    foreach (Moby content in contents)
        AssertMobyMatchesSnapshot(content, childrenBefore[content.TrueIndex], $"Undo of chest {chest.DisplayIndex} changed child T{content.TrueIndex}");
    HashSet<int> restoredLinkedIndexes = LinkedChestContentIndexes(chest);
    if (!originalLinkedIndexes.SetEquals(restoredLinkedIndexes) ||
        contents.Any(content => !content.Links.Any(link =>
            string.Equals(link.Kind, "chest contents", StringComparison.OrdinalIgnoreCase) &&
            link.TrueIndexes.Contains(chest.TrueIndex))))
    {
        throw new InvalidOperationException(
            $"Undo did not restore the original chest-content links for {chest.DisplayIndex}: expected [{string.Join(", ", originalLinkedIndexes)}], got [{string.Join(", ", restoredLinkedIndexes)}].");
    }

    Console.WriteLine($"Linked chest transform UI: {chest.DisplayIndex} moved in XYZ and became donor T{donorTrueIndex}; children stayed unchanged; Undo restored root and links.");
}

void AssertLinkedChildTransformKeepsCompatibleSiblings(MainWindow owner, ListBox browser)
{
    Moby[] mobys = ((IEnumerable<Moby>)browser.ItemsSource!).ToArray();
    (Moby chest, Moby[] contents) = FindCleanLinkedChest(mobys);
    if (contents.Length < 2)
        throw new InvalidOperationException("The linked-child transform regression requires a chest with at least two content rows.");

    Moby transformedChild = contents[0];
    Moby[] compatibleSiblings = contents.Skip(1).ToArray();
    Dictionary<int, MobyRegressionSnapshot> before = new[] { chest }
        .Concat(contents)
        .ToDictionary(moby => moby.TrueIndex, CaptureMoby);
    MobyLinkSemanticSnapshot[] activeBefore = CaptureMobyLinkSemantics([chest, .. contents]);
    MobyLinkSemanticSnapshot[] dormantBefore = CaptureMobyLinkSemantics([chest, .. contents], dormant: true);

    browser.SelectedItem = transformedChild;
    FlushUi();
    Window dialog = OpenDialog(owner, FindButton(owner, "Edit"), "linked child transform editor");
    ComboBox transformBox = FindNamed<ComboBox>(dialog, "MobyTransformBox");
    object replacement = FindNonChestLevelTransform(transformBox, mobys);
    int donorTrueIndex = TemplateValue<int>(replacement, "SourceTrueIndex");
    transformBox.SelectedItem = replacement;
    FlushUi();
    Button apply = FindButton(dialog, "Apply changes");
    apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, apply));
    WaitForDialogToClose(owner, dialog, "linked child transform editor");

    if (transformedChild.SourceCloneTrueIndex != donorTrueIndex || transformedChild.IsChestContent)
    {
        throw new InvalidOperationException(
            $"Linked child {transformedChild.DisplayIndex} did not become incompatible donor T{donorTrueIndex}.");
    }
    AssertMobyMatchesSnapshot(chest, before[chest.TrueIndex], $"Transforming child {transformedChild.DisplayIndex} changed root {chest.DisplayIndex}");
    foreach (Moby sibling in compatibleSiblings)
        AssertMobyMatchesSnapshot(sibling, before[sibling.TrueIndex], $"Transforming child {transformedChild.DisplayIndex} changed compatible sibling {sibling.DisplayIndex}");
    AssertActiveChestTopology(chest, compatibleSiblings, $"Transforming child {transformedChild.DisplayIndex}");
    if (transformedChild.Links.Any(link => IsChestContentLink(link, chest.TrueIndex)))
        throw new InvalidOperationException($"Incompatible transformed child {transformedChild.DisplayIndex} remained in the active chest relationship.");
    AssertDormantChestTopology([chest, .. contents], chest.TrueIndex, contents.Select(moby => moby.TrueIndex),
        $"Transforming child {transformedChild.DisplayIndex}");

    Button undo = FindButton(owner, "Undo");
    if (!undo.IsEnabled)
        throw new InvalidOperationException("Undo did not enable for the transformed linked child.");
    undo.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, undo));
    FlushUi();

    foreach (Moby member in new[] { chest }.Concat(contents))
        AssertMobyMatchesSnapshot(member, before[member.TrueIndex], $"Linked-child Undo did not restore {member.DisplayIndex}");
    AssertMobyLinkSemantics([chest, .. contents], activeBefore, "Linked-child Undo did not restore the original active topology");
    AssertMobyLinkSemantics([chest, .. contents], dormantBefore, "Linked-child Undo did not restore the original dormant topology", dormant: true);

    Console.WriteLine($"Linked child transform UI: {transformedChild.DisplayIndex} detached alone; {compatibleSiblings.Length} sibling(s) stayed actively linked to {chest.DisplayIndex}; Undo restored full topology.");
}

void AssertLinkedContentRemoveAndUndo(MainWindow owner, ListBox browser)
{
    Moby[] mobys = ((IEnumerable<Moby>)browser.ItemsSource!).ToArray();
    (Moby chest, Moby[] contents) = FindCleanLinkedChest(mobys);
    Moby removedContent = contents[0];
    Moby[] remainingContents = contents.Skip(1).ToArray();
    Dictionary<int, MobyRegressionSnapshot> before = new[] { chest }
        .Concat(contents)
        .ToDictionary(moby => moby.TrueIndex, CaptureMoby);
    MobyLinkSemanticSnapshot[] activeBefore = CaptureMobyLinkSemantics([chest, .. contents]);
    MobyLinkSemanticSnapshot[] dormantBefore = CaptureMobyLinkSemantics([chest, .. contents], dormant: true);

    browser.SelectedItem = removedContent;
    FlushUi();
    Button remove = FindButton(owner, "Remove");
    if (!remove.IsEnabled)
        throw new InvalidOperationException($"Remove did not enable for linked content {removedContent.DisplayIndex}.");
    remove.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, remove));
    FlushUi();
    if (!removedContent.IsRemoved)
        throw new InvalidOperationException($"Remove did not mark linked content {removedContent.DisplayIndex} as removed.");
    AssertMobyMatchesSnapshot(chest, before[chest.TrueIndex], $"Removing child {removedContent.DisplayIndex} changed root {chest.DisplayIndex}");
    foreach (Moby sibling in remainingContents)
        AssertMobyMatchesSnapshot(sibling, before[sibling.TrueIndex], $"Removing child {removedContent.DisplayIndex} changed sibling {sibling.DisplayIndex}");
    AssertActiveChestTopology(chest, remainingContents, $"Removing child {removedContent.DisplayIndex}");

    Button undoRemove = FindButtonEither(owner, "Undo Remove", "Undo last removal");
    if (!undoRemove.IsEnabled)
        throw new InvalidOperationException("Undo last removal did not enable for linked chest content.");
    undoRemove.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, undoRemove));
    FlushUi();

    foreach (Moby member in new[] { chest }.Concat(contents))
        AssertMobyMatchesSnapshot(member, before[member.TrueIndex], $"Undo last removal did not restore {member.DisplayIndex}");
    AssertMobyLinkSemantics([chest, .. contents], activeBefore, "Undo last removal did not restore the original active chest topology");
    AssertMobyLinkSemantics([chest, .. contents], dormantBefore, "Undo last removal did not restore the original dormant chest topology", dormant: true);

    Console.WriteLine($"Linked content removal UI: removed {removedContent.DisplayIndex} while siblings stayed linked; Undo last removal restored the full {chest.DisplayIndex} relationship.");
}

void AssertAddedLinkedUndoCleansTopology(MainWindow owner, ListBox browser)
{
    List<Moby> currentMobys = CurrentMobys(owner);
    (Moby chest, Moby[] contents) = FindCleanLinkedChest(currentMobys);
    MobyLinkSemanticSnapshot[] originalActive = CaptureMobyLinkSemantics([chest, .. contents]);
    MobyLinkSemanticSnapshot[] originalDormant = CaptureMobyLinkSemantics([chest, .. contents], dormant: true);

    Moby addedChild = InvokePrivate<Moby>(owner, "CreateChestContentMoby", chest, GemValue.Green);
    currentMobys.Add(addedChild);
    InvokePrivateVoid(owner, "RefreshChestContentLink", chest, addedChild);
    if (!addedChild.IsAdded || !addedChild.Links.Any(link => IsChestContentLink(link, chest.TrueIndex)))
        throw new InvalidOperationException("The added linked-child cleanup regression did not create an active added chest-content row.");
    int childTrueIndex = addedChild.TrueIndex;
    InvokePrivateVoid(owner, "UndoMoby", addedChild);
    if (currentMobys.Contains(addedChild))
        throw new InvalidOperationException($"UndoMoby left added linked child T{childTrueIndex} in the current level.");
    AssertNoLinkReferences(currentMobys, [childTrueIndex], "UndoMoby of added linked child");
    AssertMobyLinkSemantics([chest, .. contents], originalActive, "UndoMoby of added linked child did not restore source active topology");
    AssertMobyLinkSemantics([chest, .. contents], originalDormant, "UndoMoby of added linked child did not restore source dormant topology", dormant: true);
    AssertReusedTrueIndexDoesNotAttach(currentMobys, childTrueIndex, "added linked child");

    int nextIndex = currentMobys.Max(moby => moby.Index) + 1;
    int nextTrueIndex = currentMobys.Max(moby => moby.TrueIndex) + 1;
    Moby addedRoot = CreateAddedCloneForSmoke(chest, nextIndex, nextTrueIndex);
    List<Moby> addedRows = [addedRoot];
    int companionIndex = nextIndex + 1;
    int companionTrueIndex = nextTrueIndex + 1;
    int companionCount = MobyCompanionCloneBuilder.AddLinkedCompanionClones(
        chest,
        addedRoot,
        currentMobys,
        addedRows,
        ref companionIndex,
        ref companionTrueIndex,
        "stonehill",
        "Stone Hill");
    if (companionCount < 2)
        throw new InvalidOperationException($"The added linked-root cleanup regression created only {companionCount} companion(s).");
    currentMobys.AddRange(addedRows);
    MobyRelationshipRepair.RepairChestContentLinks("stonehill", currentMobys);
    int[] addedTrueIndexes = addedRows.Select(moby => moby.TrueIndex).ToArray();
    if (!addedRoot.Links.Any(link => link.TrueIndexes.Count == addedRows.Count && link.TrueIndexes[0] == addedRoot.TrueIndex))
        throw new InvalidOperationException("The added root and companion rows did not form an owned active link before UndoMoby.");

    InvokePrivateVoid(owner, "UndoMoby", addedRoot);
    if (addedRows.Any(currentMobys.Contains))
        throw new InvalidOperationException("UndoMoby left an added root or owned companion row in the current level.");
    AssertNoLinkReferences(currentMobys, addedTrueIndexes, "UndoMoby of added linked root");
    AssertReusedTrueIndexDoesNotAttach(currentMobys, addedRoot.TrueIndex, "added linked root");
    AssertMobyLinkSemantics([chest, .. contents], originalActive, "Added-root Undo did not preserve source active topology");
    AssertMobyLinkSemantics([chest, .. contents], originalDormant, "Added-root Undo did not preserve source dormant topology", dormant: true);

    Console.WriteLine($"Added linked Undo UI: child T{childTrueIndex} and root T{addedRoot.TrueIndex} plus {companionCount} companion(s) were removed without stale or reused-index links.");
}

void AssertPreEditedGemTransformAndWholeObjectUndo(MainWindow owner, ListBox browser)
{
    Moby[] mobys = ((IEnumerable<Moby>)browser.ItemsSource!).ToArray();
    Moby gem = mobys.First(moby =>
        !moby.IsRemoved &&
        !moby.IsAdded &&
        !moby.HasAnyEdit &&
        moby.IsVisibleGem &&
        moby.Gem == GemValue.Red);
    MobyRegressionSnapshot original = CaptureMoby(gem);
    string greenLabel = gem.SuggestedGemLabel(GemValue.Green, gem.Label);

    browser.SelectedItem = gem;
    FlushUi();
    Window gemDialog = OpenDialog(owner, FindButton(owner, "Edit"), "pre-transform gem editor");
    ComboBox gemBox = FindNamed<ComboBox>(gemDialog, "MobyGemValueBox");
    gemBox.SelectedItem = GemValue.Green;
    FlushUi();
    Button applyGem = FindButton(gemDialog, "Apply changes");
    applyGem.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, applyGem));
    WaitForDialogToClose(owner, gemDialog, "pre-transform gem editor");
    if (gem.Gem != GemValue.Green ||
        gem.SourceByte36 != GemValue.Green.IdByte ||
        gem.SourceByte4F != GemValue.Green.ValueByte ||
        gem.Color != GemValue.Green.Color ||
        !string.Equals(gem.Label, greenLabel, StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            $"The Red-to-Green pre-edit was incomplete: gem {gem.Gem}, bytes 0x{gem.SourceByte36:X2}/0x{gem.SourceByte4F:X2}, color {gem.Color}, label '{gem.Label}'.");
    }

    browser.SelectedItem = gem;
    FlushUi();
    Window transformDialog = OpenDialog(owner, FindButton(owner, "Edit"), "pre-edited gem transform editor");
    ComboBox transformBox = FindNamed<ComboBox>(transformDialog, "MobyTransformBox");
    object replacement = FindNonChestLevelTransform(transformBox, mobys);
    int donorTrueIndex = TemplateValue<int>(replacement, "SourceTrueIndex");
    transformBox.SelectedItem = replacement;
    FlushUi();
    Button applyTransform = FindButton(transformDialog, "Apply changes");
    applyTransform.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, applyTransform));
    WaitForDialogToClose(owner, transformDialog, "pre-edited gem transform editor");
    if (gem.SourceCloneTrueIndex != donorTrueIndex || gem.IsVisibleGem)
        throw new InvalidOperationException($"The pre-edited Green gem did not transform to donor T{donorTrueIndex}.");

    Button undo = FindButton(owner, "Undo");
    if (!undo.IsEnabled)
        throw new InvalidOperationException("Whole-object Undo did not enable for the pre-edited transformed gem.");
    undo.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, undo));
    FlushUi();
    AssertMobyMatchesSnapshot(gem, original, $"Whole-object Undo did not restore original Red gem {gem.DisplayIndex}");
    if (gem.Gem != GemValue.Red ||
        gem.SourceByte36 != original.SourceByte36 ||
        gem.SourceByte4F != original.SourceByte4F ||
        gem.Color != original.Color)
    {
        throw new InvalidOperationException(
            $"Whole-object Undo did not restore the original Red bytes/color for {gem.DisplayIndex}: 0x{gem.SourceByte36:X2}/0x{gem.SourceByte4F:X2}, {gem.Color}.");
    }

    Console.WriteLine($"Pre-edit transform Undo UI: {gem.DisplayIndex} Red -> Green -> donor T{donorTrueIndex} -> original Red bytes, color, and descriptive metadata.");
}

void AssertLinkedChestContentGemLabelEdit(MainWindow owner, ListBox browser)
{
    Moby[] mobys = ((IEnumerable<Moby>)browser.ItemsSource!).ToArray();
    (Moby chest, Moby[] contents) = FindCleanLinkedChest(mobys);
    Moby? content = contents.FirstOrDefault(moby => moby.Gem != GemValue.Purple);
    if (content == null)
        throw new InvalidOperationException("Stone Hill did not expose a linked non-purple chest-content row for the UI regression.");

    MobyRegressionSnapshot chestBefore = CaptureMoby(chest);
    Dictionary<int, MobyRegressionSnapshot> before = contents.ToDictionary(moby => moby.TrueIndex, CaptureMoby);

    string expectedLabel = content.SuggestedGemLabel(GemValue.Purple, content.Label);
    if (string.Equals(expectedLabel, content.Label, StringComparison.Ordinal))
        throw new InvalidOperationException($"The generated chest-content label was not recognized before opening the editor: '{content.Label}'.");

    browser.SelectedItem = chest;
    FlushUi();
    Window dialog = OpenDialog(owner, FindButton(owner, "Edit"), "linked chest-content editor");
    ComboBox gemBox = dialog.GetLogicalDescendants()
        .OfType<ComboBox>()
        .Single(box => string.Equals(box.Name, $"MobyChestContentGemT{content.TrueIndex}", StringComparison.Ordinal));
    TextBox labelBox = dialog.GetLogicalDescendants()
        .OfType<TextBox>()
        .Single(box => string.Equals(box.Name, $"MobyChestContentLabelT{content.TrueIndex}", StringComparison.Ordinal));

    gemBox.SelectedItem = GemValue.Purple;
    FlushUi();
    if (!string.Equals(labelBox.Text, expectedLabel, StringComparison.Ordinal))
        throw new InvalidOperationException($"Chest-content selection left a stale label: expected '{expectedLabel}', got '{labelBox.Text}'.");

    Button apply = FindButton(dialog, "Apply changes");
    apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, apply));
    WaitForDialogToClose(owner, dialog, "linked chest-content editor");
    if (content.Gem != GemValue.Purple ||
        content.Flag4B != GemValue.Purple.IdByte ||
        !string.Equals(content.Label, expectedLabel, StringComparison.Ordinal) ||
        !content.HasMetadataEdit)
    {
        throw new InvalidOperationException(
            $"Linked chest-content edit did not keep byte and label together: 0x{content.Flag4B:X2}, '{content.Label}'.");
    }
    AssertMobyMatchesSnapshot(chest, chestBefore, $"Editing linked child {content.DisplayIndex} changed parent chest {chest.DisplayIndex}");

    if (!ReferenceEquals(browser.SelectedItem, chest))
        throw new InvalidOperationException("Applying a linked child gem edit did not keep its parent chest selected.");
    Button undo = FindButton(owner, "Undo");
    if (!undo.IsEnabled)
        throw new InvalidOperationException("A linked child gem edit did not make Undo available from the selected parent chest.");
    undo.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, undo));
    FlushUi();
    AssertMobyMatchesSnapshot(chest, chestBefore, $"Parent-shell Undo changed parent chest {chest.DisplayIndex}");
    foreach (Moby linkedContent in contents)
        AssertMobyMatchesSnapshot(linkedContent, before[linkedContent.TrueIndex], $"Parent-shell Undo did not restore child T{linkedContent.TrueIndex}");
    if (!ReferenceEquals(browser.SelectedItem, chest))
        throw new InvalidOperationException("Parent-shell Undo did not keep the chest selected after restoring its child gem.");

    Console.WriteLine($"Chest-content gem UI: {chest.DisplayIndex}/{content.DisplayIndex} -> purple; parent-shell Undo enabled and restored every linked child field.");
    AssertDirectChestContentGemValidation(owner, browser, content);
}

void AssertDirectChestContentGemValidation(MainWindow owner, ListBox browser, Moby content)
{
    var original = new
    {
        content.Position,
        content.Type,
        content.State,
        content.YawByte,
        content.SourceByte36,
        content.SourceByte37,
        content.SourceByte4F,
        content.Flag4A,
        content.Flag4B,
        content.Label,
        content.Color
    };
    string expectedLabel = content.SuggestedGemLabel(GemValue.Yellow, content.Label);
    browser.SelectedItem = content;
    FlushUi();
    Window dialog = OpenDialog(owner, FindButton(owner, "Edit"), "direct chest-content editor");
    ComboBox gemBox = dialog.GetLogicalDescendants()
        .OfType<ComboBox>()
        .Single(box => string.Equals(box.Name, "MobyGemValueBox", StringComparison.Ordinal));
    TextBox renderRadiusBox = dialog.GetLogicalDescendants()
        .OfType<TextBox>()
        .Single(box => string.Equals(box.Name, "MobyRenderRadiusBox", StringComparison.Ordinal));
    TextBox nameBox = dialog.GetLogicalDescendants()
        .OfType<TextBox>()
        .Single(box => string.Equals(box.Name, "MobyNameBox", StringComparison.Ordinal));

    gemBox.SelectedItem = GemValue.Yellow;
    renderRadiusBox.Text = "0x18";
    FlushUi();
    Button apply = FindButton(dialog, "Apply changes");
    apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, apply));
    FlushUi();
    if (!owner.OwnedWindows.Contains(dialog))
        throw new InvalidOperationException("The direct chest-content editor closed after an invalid loose-gem render radius was entered.");
    AssertTextContains(dialog, "Chest-content gem value requires render radius 0x00");
    if (content.Position != original.Position ||
        content.Type != original.Type ||
        content.State != original.State ||
        content.YawByte != original.YawByte ||
        content.SourceByte36 != original.SourceByte36 ||
        content.SourceByte37 != original.SourceByte37 ||
        content.SourceByte4F != original.SourceByte4F ||
        content.Flag4A != original.Flag4A ||
        content.Flag4B != original.Flag4B ||
        !string.Equals(content.Label, original.Label, StringComparison.Ordinal) ||
        content.Color != original.Color)
    {
        throw new InvalidOperationException("The invalid direct chest-content type/gem edit partially mutated the contained-reward Moby.");
    }

    if (!string.Equals(nameBox.Text, expectedLabel, StringComparison.Ordinal))
        throw new InvalidOperationException($"Direct chest-content selection left a stale generated label: '{nameBox.Text}'.");
    renderRadiusBox.Text = "0x00";
    FlushUi();
    apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, apply));
    WaitForDialogToClose(owner, dialog, "direct chest-content editor");
    if (content.Gem != GemValue.Yellow ||
        content.Flag4B != GemValue.Yellow.IdByte ||
        content.Type != 0x00 ||
        !string.Equals(content.Label, expectedLabel, StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            $"Corrected direct chest-content edit did not apply yellow: type 0x{content.Type:X2}, byte 0x{content.Flag4B:X2}, '{content.Label}'.");
    }

    Console.WriteLine($"Direct chest-content UI: invalid radius stayed transactional; corrected radius applied {content.Gem.DisplayName}.");
}

void AssertBuildSafetyInspection(MainWindow owner)
{
    string sourceImagePath = ReadConfiguredSourceImagePath(workspace);
    FileInfo sourceImageBefore = new(sourceImagePath);
    if (!sourceImageBefore.Exists)
        throw new FileNotFoundException("The configured original Spyro image is required for the build-safety UI smoke.", sourceImagePath);
    long sourceLength = sourceImageBefore.Length;
    DateTime sourceWriteTime = sourceImageBefore.LastWriteTimeUtc;

    Button inspect = FindButton(owner, "Inspect Build Safety");
    if (!inspect.IsEnabled)
        throw new InvalidOperationException("Inspect Build Safety was not enabled before the smoke action.");

    Window dialog = OpenAsyncDialog(owner, inspect, "Build Safety", "build-safety report dialog");
    AssertText(dialog, "Build Safety: Stable");
    AssertTextContains(dialog, "1 edited level(s) checked. No problems were found.");
    AssertTextContains(dialog, "Stone Hill  |  Stable");
    FindButton(dialog, "Open Report Folder");
    Button close = FindButton(dialog, "Close");

    string reportPrefix = Path.Combine(workspace, "output", "Spyro Editor - All Saved Edits.build-safety");
    string jsonPath = $"{reportPrefix}.json";
    string markdownPath = $"{reportPrefix}.md";
    AssertTextAbsent(dialog, "true source-row append");
    AssertTextAbsent(dialog, "projected runtime slot");
    AssertTextAbsent(dialog, "JSON:");
    if (!File.Exists(jsonPath) || !File.Exists(markdownPath))
        throw new InvalidOperationException("The build-safety action opened its dialog without writing both reports.");

    using (JsonDocument document = JsonDocument.Parse(File.ReadAllText(jsonPath)))
    {
        JsonElement root = document.RootElement;
        AssertJsonString(root, "StatusLabel", "Stable");
        AssertJsonInt(root, "TrueAppendCount", 0);
        AssertJsonInt(root, "RuntimeSlotsConsumed", 0);
        AssertJsonInt(root, "SkippedEditCount", 0);
        JsonElement levels = root.GetProperty("Levels");
        if (levels.GetArrayLength() != 1)
            throw new InvalidOperationException($"Expected one edited level in the build-safety report, found {levels.GetArrayLength()}.");
        JsonElement level = levels[0];
        AssertJsonString(level, "LevelKey", "stonehill");
        AssertJsonString(level, "StatusLabel", "Stable");
        AssertJsonInt(level, "TrueAppendCount", 0);
        AssertJsonInt(level, "RuntimeSlotsConsumed", 0);
        AssertJsonInt(level, "SkippedEditCount", 0);
        if (level.GetProperty("ComponentByteLength").GetInt32() <= 0 ||
            level.GetProperty("SourceDynamicCapacity").GetInt32() <= 0)
        {
            throw new InvalidOperationException("The stable Stone Hill report did not resolve the native component and dynamic capacity.");
        }
    }

    string markdown = File.ReadAllText(markdownPath);
    foreach (string expected in new[]
    {
        "# Spyro Editor Build Safety",
        "Overall status: **Stable**",
        "Stone Hill",
        "The report contains editor metadata and byte counts only. It does not include game data."
    })
    {
        if (!markdown.Contains(expected, StringComparison.Ordinal))
            throw new InvalidOperationException($"The build-safety Markdown report is missing '{expected}'.");
    }

    FieldInfo inspectionField = typeof(MainWindow).GetField(
        "_lastBuildSafetyInspection",
        BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Could not inspect the build-safety state field.");
    if (inspectionField.GetValue(owner) == null)
        throw new InvalidOperationException("The build-safety dialog opened without retaining its inspection state.");

    FieldInfo summaryField = typeof(MainWindow).GetField(
        "_buildSafetySummaryText",
        BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Could not inspect the build-safety summary field.");
    TextBlock summary = (TextBlock)(summaryField.GetValue(owner)
        ?? throw new InvalidOperationException("The build-safety summary control was unavailable."));
    const string expectedSummary = "Ready: 1 edited level(s) checked";
    if (!string.Equals(summary.Text, expectedSummary, StringComparison.Ordinal))
        throw new InvalidOperationException($"Unexpected build-safety summary: '{summary.Text}'.");

    SaveFrame(dialog, "spyro-editor-build-safety-dialog.png");
    close.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, close));
    WaitForDialogToClose(owner, dialog, "build-safety report dialog");
    if (!inspect.IsEnabled)
        throw new InvalidOperationException("Inspect Build Safety did not re-enable after its report dialog closed.");

    sourceImageBefore.Refresh();
    if (!sourceImageBefore.Exists || sourceImageBefore.Length != sourceLength || sourceImageBefore.LastWriteTimeUtc != sourceWriteTime)
        throw new InvalidOperationException("The source disc image metadata changed during the read-only build-safety inspection.");

    Console.WriteLine($"Build safety UI: Stable; 1 edited level; reports validated in isolated workspace ({jsonPath}).");
}

void AssertBuildSafetyIssueNavigation(MainWindow owner)
{
    const string targetLevelKey = "darkhollow";
    const string targetLevelName = "Dark Hollow";
    string cachePath = Path.Combine(workspace, "editor-cache", $"{targetLevelKey}-mobys.json");
    Moby targetRecord = MobyLoader.LoadCached(cachePath)
        .First(moby => moby.TrueIndex >= 0 && !moby.IsEditorControl);
    MobyBuildSafetyIssue issue = new(
        Code: "ui-smoke-target",
        Status: MobyBuildSafetyStatus.Review,
        Message: "Synthetic actionable issue used to verify exact editor navigation.",
        LevelKey: targetLevelKey,
        LevelName: targetLevelName,
        EditorTrueIndex: targetRecord.TrueIndex,
        MobyLabel: targetRecord.DisplayLabel);

    MethodInfo buildRow = typeof(MainWindow).GetMethod(
        "BuildBuildSafetyIssueRow",
        BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Could not find the Build Safety issue-row builder.");
    Window dialog = new()
    {
        Title = "Build Safety navigation smoke",
        Width = 620,
        Height = 220,
        WindowStartupLocation = WindowStartupLocation.CenterOwner
    };
    Control row = (Control)(buildRow.Invoke(owner, [dialog, issue])
        ?? throw new InvalidOperationException("Build Safety did not create an actionable issue row."));
    dialog.Content = row;
    Task<object?> dialogTask = dialog.ShowDialog<object?>(owner);
    FlushUi();

    if (row.Cursor == null || !ReferenceEquals(row.DataContext, issue))
        throw new InvalidOperationException("The object-specific Build Safety issue is not visibly actionable.");
    AssertText(row, issue.TargetLabel);
    AssertTextContains(row, "Double-click to select and center this object");
    SaveFrame(dialog, "spyro-editor-build-safety-actionable-issue.png");

    EditorViewport viewport = owner.GetLogicalDescendants().OfType<EditorViewport>().Single();
    viewport.SetViewMode(ViewportViewMode.Fly3D);
    FieldInfo flyCameraField = typeof(EditorViewport).GetField(
        "_flyCamera",
        BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Could not inspect the Fly 3D camera for Build Safety navigation.");
    object? flyCameraBefore = flyCameraField.GetValue(viewport);

    Point issuePoint = new(30, 25);
    dialog.MouseDown(issuePoint, MouseButton.Left, RawInputModifiers.None);
    dialog.MouseUp(issuePoint, MouseButton.Left, RawInputModifiers.None);
    dialog.MouseDown(issuePoint, MouseButton.Left, RawInputModifiers.None);
    if (!dialogTask.IsCompleted)
        dialog.MouseUp(issuePoint, MouseButton.Left, RawInputModifiers.None);

    FieldInfo selectedMobyField = typeof(MainWindow).GetField(
        "_selectedMoby",
        BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Could not inspect the selected Moby after Build Safety navigation.");
    FieldInfo currentLevelField = typeof(MainWindow).GetField(
        "_currentLevel",
        BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Could not inspect the current level after Build Safety navigation.");
    Moby? selected = null;
    string currentLevelKey = "";
    for (int attempt = 0; attempt < 1000; attempt++)
    {
        Dispatcher.UIThread.RunJobs();
        Thread.Sleep(10);
        selected = selectedMobyField.GetValue(owner) as Moby;
        object? currentLevel = currentLevelField.GetValue(owner);
        currentLevelKey = currentLevel?.GetType().GetProperty("Key")?.GetValue(currentLevel) as string ?? "";
        if (string.Equals(currentLevelKey, targetLevelKey, StringComparison.OrdinalIgnoreCase) &&
            selected?.TrueIndex == targetRecord.TrueIndex)
        {
            break;
        }
    }
    FlushUi();

    if (!dialogTask.IsCompleted)
        throw new InvalidOperationException("Activating a Build Safety issue did not close its modal dialog.");
    if (!string.Equals(currentLevelKey, targetLevelKey, StringComparison.OrdinalIgnoreCase) ||
        selected?.TrueIndex != targetRecord.TrueIndex)
    {
        throw new InvalidOperationException(
            $"Build Safety navigation did not select {targetLevelName} T{targetRecord.TrueIndex}; current level '{currentLevelKey}', selected T{selected?.TrueIndex}.");
    }
    if (viewport.ViewMode != ViewportViewMode.Fly3D || Equals(flyCameraBefore, flyCameraField.GetValue(viewport)))
        throw new InvalidOperationException("Build Safety navigation did not preserve and recenter the Fly 3D camera.");

    ListBox browser = owner.GetLogicalDescendants()
        .OfType<ListBox>()
        .First(list => list.ItemsSource is IEnumerable<Moby> && ReferenceEquals(list.SelectedItem, selected));
    if (!ReferenceEquals(browser.SelectedItem, selected))
        throw new InvalidOperationException("Build Safety navigation did not reveal the target in the object browser.");
    AssertTextContains(owner, $"Build Safety: selected and centered {targetLevelName} T{targetRecord.TrueIndex}");
    Console.WriteLine($"Build Safety navigation UI: {targetLevelName} T{targetRecord.TrueIndex} selected, revealed, and centered in Fly 3D.");
}

void AssertObservedIdentitySavePreservesScopes(MainWindow owner, ListBox browser)
{
    const string identityFixtureLevelKey = "nightflight";
    LevelDefinition identityFixtureLevel = LevelCatalog.Load(workspace).Levels.Single(level =>
        level.Key.Equals(identityFixtureLevelKey, StringComparison.OrdinalIgnoreCase));
    MethodInfo selectLevelMethod = typeof(MainWindow).GetMethod(
        "SelectLevelAsync",
        BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Could not select the observed-ID fixture level.");
    Task selectLevelTask = (Task)(selectLevelMethod.Invoke(owner, [identityFixtureLevel])
        ?? throw new InvalidOperationException("Selecting the observed-ID fixture level returned no task."));
    for (int attempt = 0; attempt < 1000 && !selectLevelTask.IsCompleted; attempt++)
    {
        Dispatcher.UIThread.RunJobs();
        Thread.Sleep(10);
    }
    selectLevelTask.GetAwaiter().GetResult();
    FlushUi();

    List<Moby> mobys = ((IEnumerable<Moby>)browser.ItemsSource!)
        .Where(moby =>
            !moby.IsRemoved &&
            !moby.IsAdded &&
            moby.SpecialDataPointer == 0 &&
            moby.TrueIndex >= 0 &&
            moby.TrueIndex < identityFixtureLevel.SourceRecordCount)
        .ToList();
    Moby selected = mobys
        .Where(IsIdentityObservationEligible)
        .OrderBy(moby => moby.TrueIndex)
        .FirstOrDefault()
        ?? throw new InvalidOperationException(
            $"{identityFixtureLevel.DisplayName} has no source-backed no-pointer inferred row for the observed-ID save-scope smoke.");
    browser.SelectedItem = selected;
    FlushUi();

    string canonical = MobyIdentityFingerprint.BuildV2(selected);
    string legacy = MobyIdentityFingerprint.BuildLegacyV1(selected);
    int otherHighByte = selected.SourceByte37 == 0 ? 1 : 0;
    int otherNativeClass = (otherHighByte << 8) | selected.SourceByte36;
    string observationPath = Path.Combine(workspace, "_local", "smoke", "moby-identity-observations.json");
    Directory.CreateDirectory(Path.GetDirectoryName(observationPath)!);

    Dictionary<string, object?> differentClassSameScope = new()
    {
        ["fingerprint"] = legacy,
        ["levelKey"] = identityFixtureLevelKey,
        ["nativeClassHex"] = $"0x{otherNativeClass:X4}",
        ["sourceByte36Hex"] = $"0x{selected.SourceByte36:X2}",
        ["sourceByte37Hex"] = $"0x{otherHighByte:X2}",
        ["specialDataPointerHex"] = "0x00000000",
        ["matchTrueIndex"] = selected.TrueIndex,
        ["label"] = "Preserved other native class",
        ["candidateKind"] = "specific test identity",
        ["evidence"] = "UI smoke preservation seed"
    };
    Dictionary<string, object?> broaderLevelScope = new()
    {
        ["fingerprintVersion"] = MobyIdentityFingerprint.CurrentVersion,
        ["fingerprint"] = canonical,
        ["levelKey"] = identityFixtureLevelKey,
        ["label"] = "Preserved level family",
        ["candidateKind"] = "specific test identity",
        ["evidence"] = "UI smoke broad-scope seed"
    };
    Dictionary<string, object?> staleExactScope = new()
    {
        ["fingerprintVersion"] = MobyIdentityFingerprint.CurrentVersion,
        ["fingerprint"] = canonical,
        ["levelKey"] = identityFixtureLevelKey,
        ["nativeClassHex"] = $"0x{(selected.SourceByte37 << 8) | selected.SourceByte36:X4}",
        ["sourceByte36Hex"] = $"0x{selected.SourceByte36:X2}",
        ["sourceByte37Hex"] = $"0x{selected.SourceByte37:X2}",
        ["specialDataPointerHex"] = "0x00000000",
        ["matchTrueIndex"] = selected.TrueIndex,
        ["label"] = "Replace stale exact row",
        ["candidateKind"] = "specific test identity",
        ["evidence"] = "UI smoke replace seed"
    };
    File.WriteAllText(observationPath, JsonSerializer.Serialize(new
    {
        observations = new object[] { differentClassSameScope, broaderLevelScope, staleExactScope }
    }, new JsonSerializerOptions { WriteIndented = true }));

    FieldInfo nameField = typeof(MainWindow).GetField(
        "_identityObservationNameBox",
        BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Could not inspect the observed-ID name field.");
    TextBox nameBox = (TextBox)(nameField.GetValue(owner)
        ?? throw new InvalidOperationException("The observed-ID name field was unavailable."));
    const string savedLabel = "Observed no-pointer family member";
    nameBox.Text = savedLabel;
    MethodInfo saveMethod = typeof(MainWindow).GetMethod(
        "SaveSelectedIdentityObservationAsync",
        BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Could not invoke the observed-ID save action.");
    Task saveTask = (Task)(saveMethod.Invoke(owner, null)
        ?? throw new InvalidOperationException("The observed-ID save action returned no task."));
    for (int attempt = 0; attempt < 1000 && !saveTask.IsCompleted; attempt++)
    {
        Dispatcher.UIThread.RunJobs();
        Thread.Sleep(10);
    }
    saveTask.GetAwaiter().GetResult();
    FlushUi();

    using (JsonDocument document = JsonDocument.Parse(File.ReadAllText(observationPath)))
    {
        JsonElement observations = document.RootElement.GetProperty("observations");
        string[] labels = observations.EnumerateArray()
            .Select(observation => observation.TryGetProperty("label", out JsonElement label) ? label.GetString() ?? "" : "")
            .ToArray();
        if (observations.GetArrayLength() != 3 ||
            !labels.Contains("Preserved other native class", StringComparer.Ordinal) ||
            !labels.Contains("Preserved level family", StringComparer.Ordinal) ||
            labels.Contains("Replace stale exact row", StringComparer.Ordinal) ||
            labels.Count(label => label.Equals(savedLabel, StringComparison.Ordinal)) != 1)
        {
            throw new InvalidOperationException($"Save Observed ID did not preserve distinct class/broad scopes and replace only its exact scope: {string.Join(", ", labels)}.");
        }

        JsonElement saved = observations.EnumerateArray().Single(observation =>
            observation.GetProperty("label").GetString() == savedLabel);
        if (!string.Equals(saved.GetProperty("fingerprint").GetString(), canonical, StringComparison.Ordinal) ||
            saved.GetProperty("matchTrueIndex").GetInt32() != selected.TrueIndex ||
            !string.Equals(saved.GetProperty("specialDataPointerHex").GetString(), "0x00000000", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Save Observed ID did not emit the canonical v2 fingerprint and exact no-pointer T scope.");
        }
    }

    List<Moby> reloaded = MobyLoader.LoadCached(Path.Combine(workspace, "editor-cache", $"{identityFixtureLevelKey}-mobys.json")).ToList();
    MobyMetadataEnricher.Apply(new EditorWorkspace(workspace), identityFixtureLevelKey, reloaded);
    Moby reloadedSelected = reloaded.Single(moby => moby.TrueIndex == selected.TrueIndex);
    if (!reloadedSelected.DisplayLabel.Equals(savedLabel, StringComparison.Ordinal))
    {
        throw new InvalidOperationException(
            $"Observed-ID save/reload scope precedence failed: selected='{reloadedSelected.DisplayLabel}'.");
    }

    Console.WriteLine($"Observed-ID UI save/reload: preserved a different full class and broader level scope; replaced only {identityFixtureLevel.DisplayName} T{selected.TrueIndex}.");
}

static bool IsIdentityObservationEligible(Moby moby)
{
    string label = moby.Label.Trim();
    if (string.IsNullOrWhiteSpace(label) ||
        label.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ||
        label.Contains("?", StringComparison.Ordinal))
    {
        return true;
    }

    string proof = $"{moby.Confidence} {moby.Evidence}";
    return proof.Contains("inferred", StringComparison.OrdinalIgnoreCase) ||
        proof.Contains("needs live", StringComparison.OrdinalIgnoreCase) ||
        proof.Contains("until live-tested", StringComparison.OrdinalIgnoreCase);
}

void AssertStoneHillControlIdentityAndTransportAnchors(MainWindow owner, ListBox browser, Moby ordinaryMoby)
{
    EditorViewport viewport = owner.GetLogicalDescendants()
        .OfType<EditorViewport>()
        .First();
    Moby t149 = viewport.Mobys.First(moby => moby.TrueIndex == 149);
    if (t149.DisplayLabel.Contains("whirlwind", StringComparison.OrdinalIgnoreCase) ||
        t149.CandidateKind.Contains("whirlwind", StringComparison.OrdinalIgnoreCase) ||
        t149.VisualKind != MobyVisualKind.Control)
    {
        throw new InvalidOperationException(
            $"Stone Hill T149 regressed to a whirlwind identity: {t149.DisplayLabel} / {t149.CandidateKind} / {t149.VisualKind}.");
    }

    Moby flyIn = viewport.Mobys.First(moby => moby.IsFlyInLandingControl);
    Moby returnHome = viewport.Mobys.First(moby =>
        moby.DisplayLabel.Contains("Return Home", StringComparison.OrdinalIgnoreCase) &&
        moby.VisualKind == MobyVisualKind.Portal);
    MethodInfo offsetMethod = typeof(EditorViewport).GetMethod(
        "MobyMarkerDisplayOffset",
        BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("Could not inspect the transport marker anchor offset.");
    MethodInfo hitRadiusMethod = typeof(EditorViewport).GetMethod(
        "MobyHitRadius",
        BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("Could not inspect the viewport Moby hit radius.");

    const double markerSize = 12;
    Vector flyInOffset = (Vector)(offsetMethod.Invoke(null, [flyIn, markerSize])
        ?? throw new InvalidOperationException("Fly-in marker offset was unavailable."));
    Vector returnHomeOffset = (Vector)(offsetMethod.Invoke(null, [returnHome, markerSize])
        ?? throw new InvalidOperationException("Return Home marker offset was unavailable."));
    Vector ordinaryOffset = (Vector)(offsetMethod.Invoke(null, [ordinaryMoby, markerSize])
        ?? throw new InvalidOperationException("Ordinary marker offset was unavailable."));
    if (Math.Abs(flyInOffset.X) > 0.001 || Math.Abs(flyInOffset.Y + (markerSize * 1.35)) > 0.001)
        throw new InvalidOperationException($"Fly-in marker was not pinned by its portal base: {flyInOffset}.");
    if (Math.Abs(returnHomeOffset.X) > 0.001 || Math.Abs(returnHomeOffset.Y + (markerSize * 0.66)) > 0.001)
        throw new InvalidOperationException($"Return Home marker was not pinned by its platform base: {returnHomeOffset}.");
    if (ordinaryOffset != default)
        throw new InvalidOperationException($"Transport anchoring unexpectedly changed the ordinary marker offset: {ordinaryOffset}.");

    Moby gem = viewport.Mobys.First(moby => moby.VisualKind == MobyVisualKind.Gem && moby.Gem != GemValue.Unknown);
    double normalGemHitRadius = (double)(hitRadiusMethod.Invoke(null, [gem, markerSize])
        ?? throw new InvalidOperationException("Normal gem hit radius was unavailable."));
    double zoomedGemHitRadius = (double)(hitRadiusMethod.Invoke(null, [gem, 20d])
        ?? throw new InvalidOperationException("Zoomed gem hit radius was unavailable."));
    double ordinaryHitRadius = (double)(hitRadiusMethod.Invoke(null, [ordinaryMoby, markerSize])
        ?? throw new InvalidOperationException("Ordinary Moby hit radius was unavailable."));
    if (Math.Abs(normalGemHitRadius - 21.6) > 0.001 || Math.Abs(zoomedGemHitRadius - 29) > 0.001 || Math.Abs(ordinaryHitRadius - 12) > 0.001)
    {
        throw new InvalidOperationException(
            $"Viewport hit targets do not match the rendered markers: gem={normalGemHitRadius:0.0}/{zoomedGemHitRadius:0.0}, ordinary={ordinaryHitRadius:0.0}.");
    }
    Console.WriteLine("Gem viewport hit target: 21.6 px at normal size, 29 px when zoomed; ordinary markers remain 12 px.");

    browser.SelectedItem = flyIn;
    viewport.SetViewMode(ViewportViewMode.Fly3D);
    FlushUi();
    SaveFrame(owner, "spyro-editor-fly-in-world-anchor.png");
    browser.SelectedItem = returnHome;
    FlushUi();
    SaveFrame(owner, "spyro-editor-return-home-world-anchor.png");
    viewport.SetViewMode(ViewportViewMode.Map);
    browser.SelectedItem = ordinaryMoby;
    FlushUi();
}

static Moby FindGeneratedRewardCarrier(IEnumerable<Moby> mobys)
{
    return mobys.First(moby =>
        !moby.IsRemoved &&
        !moby.IsAdded &&
        !moby.IsGemLike &&
        moby.RewardGem != GemValue.Unknown &&
        GemValue.Known.Any(gem =>
            moby.DisplayLabel.EndsWith(
                $" ({gem.Name.Replace(" gem", "", StringComparison.OrdinalIgnoreCase)} reward)",
                StringComparison.OrdinalIgnoreCase)));
}

static (Moby Chest, Moby[] Contents) FindCleanLinkedChest(IReadOnlyList<Moby> mobys)
{
    foreach (Moby chest in mobys
        .Where(moby => !moby.IsRemoved && !moby.IsAdded && moby.IsChest && !moby.HasAnyEdit)
        .OrderBy(moby => moby.TrueIndex == 75 ? 0 : 1)
        .ThenBy(moby => moby.TrueIndex))
    {
        HashSet<int> linkedIndexes = LinkedChestContentIndexes(chest);
        Moby[] contents = mobys
            .Where(moby =>
                !moby.IsRemoved &&
                linkedIndexes.Contains(moby.TrueIndex) &&
                moby.IsChestContent)
            .OrderBy(moby => moby.TrueIndex)
            .ToArray();
        if (contents.Length > 0 && contents.All(content => !content.HasAnyEdit))
            return (chest, contents);
    }

    throw new InvalidOperationException("Stone Hill did not expose a clean linked chest and content rows for the headless UI regressions.");
}

static HashSet<int> LinkedChestContentIndexes(Moby chest)
{
    return chest.Links
        .Where(link =>
            string.Equals(link.Kind, "chest contents", StringComparison.OrdinalIgnoreCase) &&
            link.TrueIndexes.Contains(chest.TrueIndex))
        .SelectMany(link => link.TrueIndexes)
        .Where(trueIndex => trueIndex != chest.TrueIndex)
        .ToHashSet();
}

static object FindNonChestLevelTransform(ComboBox transformBox, IReadOnlyList<Moby> mobys)
{
    if (transformBox.ItemsSource is not System.Collections.IEnumerable source)
        throw new InvalidOperationException("The transform selector did not expose its template list.");

    foreach (object template in source)
    {
        if (!TemplateValue<bool>(template, "FromLevelTemplate"))
            continue;
        int donorTrueIndex = TemplateValue<int>(template, "SourceTrueIndex");
        Moby? donor = mobys.FirstOrDefault(moby =>
            moby.TrueIndex == donorTrueIndex &&
            !moby.IsRemoved &&
            !moby.IsAdded);
        if (donor == null || donor.IsChest || donor.IsChestContent || donor.IsGemLike || donor.IsEditorControl)
            continue;

        return template;
    }

    throw new InvalidOperationException("The edit dialog did not offer a same-level non-chest transform donor.");
}

static T TemplateValue<T>(object template, string propertyName)
{
    PropertyInfo property = template.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)
        ?? throw new InvalidOperationException($"Transform template property '{propertyName}' was unavailable.");
    object? value = property.GetValue(template);
    return value is T typed
        ? typed
        : throw new InvalidOperationException($"Transform template property '{propertyName}' had unexpected value '{value}'.");
}

static string FormatYawForSmoke(int yawByte)
{
    return Moby.YawByteToDegrees(yawByte).ToString("0.###", CultureInfo.InvariantCulture);
}

static T FindNamed<T>(Control root, string name) where T : Control
{
    return root.GetLogicalDescendants()
        .OfType<T>()
        .Single(control => string.Equals(control.Name, name, StringComparison.Ordinal));
}

static object[] ReadItemsSource(ItemsControl control, string description)
{
    if (control.ItemsSource is not System.Collections.IEnumerable items)
        throw new InvalidOperationException($"The {description} did not expose an ItemsSource.");
    return items.Cast<object>().ToArray();
}

static MobyRegressionSnapshot CaptureMoby(Moby moby)
{
    return new MobyRegressionSnapshot(
        moby.Position,
        moby.Type,
        moby.State,
        moby.YawByte,
        moby.SourceByte36,
        moby.SourceByte37,
        moby.SourceByte4F,
        moby.Flag4A,
        moby.Flag4B,
        moby.Label,
        moby.Color,
        moby.HasLoadedNativeEdit,
        moby.LoadedNativeEditSummary,
        moby.IsAdded,
        moby.IsRemoved,
        moby.HasAnyEdit,
        moby.CrossLevelTemplateId,
        moby.CrossLevelFamily,
        moby.CrossLevelSourceLevelKey,
        moby.CrossLevelSourceLevelName,
        moby.CrossLevelSourceTrueIndex,
        moby.CrossLevelRequiredExporterFeature,
        moby.SourceCloneLevelKey,
        moby.SourceCloneLevelName,
        moby.SourceCloneTrueIndex,
        moby.CandidateKind,
        moby.Confidence,
        moby.Evidence,
        moby.BehaviorNote,
        moby.ZoneLabel,
        moby.PatchStatus,
        moby.PatchLead);
}

static void AssertMobyMatchesSnapshot(Moby moby, MobyRegressionSnapshot expected, string context)
{
    MobyRegressionSnapshot actual = CaptureMoby(moby);
    if (actual != expected)
    {
        throw new InvalidOperationException(
            $"{context}. Expected {expected}; actual {actual}.");
    }
}

static MobyLinkSemanticSnapshot[] CaptureMobyLinkSemantics(IEnumerable<Moby> mobys, bool dormant = false)
{
    return mobys
        .OrderBy(moby => moby.TrueIndex)
        .SelectMany(moby => (dormant ? moby.DormantGemRelationshipLinks : moby.Links).Select(link => new MobyLinkSemanticSnapshot(
            moby.TrueIndex,
            link.Key,
            link.Kind,
            string.Join(",", link.TrueIndexes.OrderBy(trueIndex => trueIndex)))))
        .OrderBy(link => link.OwnerTrueIndex)
        .ThenBy(link => link.Key, StringComparer.Ordinal)
        .ThenBy(link => link.Kind, StringComparer.Ordinal)
        .ThenBy(link => link.MemberSet, StringComparer.Ordinal)
        .ToArray();
}

static void AssertMobyLinkSemantics(
    IEnumerable<Moby> mobys,
    IReadOnlyList<MobyLinkSemanticSnapshot> expected,
    string context,
    bool dormant = false)
{
    MobyLinkSemanticSnapshot[] actual = CaptureMobyLinkSemantics(mobys, dormant);
    if (!actual.SequenceEqual(expected))
    {
        throw new InvalidOperationException(
            $"{context}. Expected [{string.Join("; ", expected)}]; actual [{string.Join("; ", actual)}].");
    }
}

static bool IsChestContentLink(MobyLink link, int rootTrueIndex)
{
    return string.Equals(link.Kind, "chest contents", StringComparison.OrdinalIgnoreCase) &&
        link.TrueIndexes.Count >= 2 &&
        link.TrueIndexes[0] == rootTrueIndex;
}

static void AssertActiveChestTopology(Moby chest, IReadOnlyList<Moby> activeContents, string context)
{
    HashSet<int> expected = new[] { chest.TrueIndex }
        .Concat(activeContents.Select(moby => moby.TrueIndex))
        .ToHashSet();
    MobyLink? rootLink = chest.Links.FirstOrDefault(link =>
        IsChestContentLink(link, chest.TrueIndex) &&
        link.TrueIndexes.ToHashSet().SetEquals(expected));
    if (rootLink == null)
    {
        throw new InvalidOperationException(
            $"{context} did not leave the expected active topology on {chest.DisplayIndex}: [{string.Join(", ", expected.Order())}].");
    }

    foreach (Moby content in activeContents)
    {
        if (!content.Links.Any(link =>
                IsChestContentLink(link, chest.TrueIndex) &&
                link.TrueIndexes.ToHashSet().SetEquals(expected)))
        {
            throw new InvalidOperationException(
                $"{context} left compatible sibling {content.DisplayIndex} without the active {chest.DisplayIndex} relationship.");
        }
    }
}

static void AssertDormantChestTopology(
    IEnumerable<Moby> members,
    int rootTrueIndex,
    IEnumerable<int> contentTrueIndexes,
    string context)
{
    HashSet<int> expected = new[] { rootTrueIndex }
        .Concat(contentTrueIndexes)
        .ToHashSet();
    foreach (Moby member in members)
    {
        if (!member.DormantGemRelationshipLinks.Any(link =>
                IsChestContentLink(link, rootTrueIndex) &&
                link.TrueIndexes.ToHashSet().SetEquals(expected)))
        {
            throw new InvalidOperationException(
                $"{context} lost full dormant topology [{string.Join(", ", expected.Order())}] on {member.DisplayIndex}.");
        }
    }
}

static List<Moby> CurrentMobys(MainWindow owner)
{
    FieldInfo field = typeof(MainWindow).GetField("_currentMobys", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Could not inspect the current Moby list.");
    return field.GetValue(owner) as List<Moby>
        ?? throw new InvalidOperationException("The current Moby list was unavailable.");
}

static T InvokePrivate<T>(MainWindow owner, string methodName, params object?[] arguments)
{
    MethodInfo method = FindPrivateMethod(methodName, arguments.Length);
    object? result = method.Invoke(owner, arguments);
    return result is T typed
        ? typed
        : throw new InvalidOperationException($"Private MainWindow method {methodName} returned unexpected value '{result}'.");
}

static void InvokePrivateVoid(MainWindow owner, string methodName, params object?[] arguments)
{
    FindPrivateMethod(methodName, arguments.Length).Invoke(owner, arguments);
}

static MethodInfo FindPrivateMethod(string methodName, int parameterCount)
{
    MethodInfo[] matches = typeof(MainWindow)
        .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
        .Where(method => string.Equals(method.Name, methodName, StringComparison.Ordinal) &&
            method.GetParameters().Length == parameterCount)
        .ToArray();
    return matches.Length == 1
        ? matches[0]
        : throw new InvalidOperationException(
            $"Expected one private MainWindow.{methodName} overload with {parameterCount} parameter(s), found {matches.Length}.");
}

static void AssertNoLinkReferences(IEnumerable<Moby> mobys, IEnumerable<int> removedTrueIndexes, string context)
{
    HashSet<int> removed = removedTrueIndexes.ToHashSet();
    string[] stale = mobys
        .SelectMany(moby => moby.Links
            .Select(link => (Owner: moby, Link: link, Store: "active"))
            .Concat(moby.DormantGemRelationshipLinks.Select(link => (Owner: moby, Link: link, Store: "dormant"))))
        .Where(item => item.Link.TrueIndexes.Any(removed.Contains))
        .Select(item => $"{item.Owner.DisplayIndex}:{item.Store}:{item.Link.Key}[{string.Join(",", item.Link.TrueIndexes)}]")
        .ToArray();
    if (stale.Length > 0)
        throw new InvalidOperationException($"{context} left stale link reference(s): {string.Join("; ", stale)}.");
}

static void AssertReusedTrueIndexDoesNotAttach(List<Moby> mobys, int reusedTrueIndex, string sourceDescription)
{
    int expectedNext = mobys.Max(moby => moby.TrueIndex) + 1;
    if (reusedTrueIndex != expectedNext)
    {
        throw new InvalidOperationException(
            $"The removed {sourceDescription} index T{reusedTrueIndex} was not immediately reusable; next is T{expectedNext}.");
    }

    Moby probe = CreateReusableIndexProbe(mobys.Max(moby => moby.Index) + 1, reusedTrueIndex);
    mobys.Add(probe);
    try
    {
        MobyRelationshipRepair.RepairChestContentLinks("stonehill", mobys);
        AssertNoLinkReferences(mobys, [reusedTrueIndex], $"Reusing T{reusedTrueIndex} after UndoMoby of {sourceDescription}");
    }
    finally
    {
        mobys.Remove(probe);
        foreach (Moby moby in mobys)
        {
            moby.Links.RemoveAll(link => link.TrueIndexes.Contains(reusedTrueIndex));
            moby.DormantGemRelationshipLinks.RemoveAll(link => link.TrueIndexes.Contains(reusedTrueIndex));
        }
        MobyRelationshipRepair.RepairChestContentLinks("stonehill", mobys);
    }
}

static Moby CreateReusableIndexProbe(int index, int trueIndex)
{
    Vector3f position = new(-12000, -12000, -12000);
    ColorRgba color = Moby.ColorForType(0x20);
    return new Moby
    {
        Index = index,
        TrueIndex = trueIndex,
        LegacyIndex = MobyLoader.GetLegacyAliasIndex(trueIndex),
        Position = position,
        OriginalPosition = position,
        Type = 0x20,
        OriginalType = 0x20,
        State = 0x00,
        OriginalState = 0x00,
        YawByte = 0,
        OriginalYawByte = 0,
        SourceByte36 = 0x12,
        OriginalSourceByte36 = 0x12,
        SourceByte37 = 0x00,
        OriginalSourceByte37 = 0x00,
        SourceByte4F = 0x00,
        OriginalSourceByte4F = 0x00,
        Flag4A = 0x00,
        OriginalFlag4A = 0x00,
        Flag4B = 0xFF,
        OriginalFlag4B = 0xFF,
        Color = color,
        OriginalColor = color,
        Label = "UI smoke reusable-index probe",
        OriginalLabel = "UI smoke reusable-index probe",
        CandidateKind = "isolated non-gem probe",
        OriginalCandidateKind = "isolated non-gem probe",
        Confidence = "headless-smoke",
        OriginalConfidence = "headless-smoke",
        Evidence = "Temporary non-gem object used to verify stale link cleanup.",
        OriginalEvidence = "Temporary non-gem object used to verify stale link cleanup.",
        IsAdded = true
    };
}

static Moby CreateAddedCloneForSmoke(Moby source, int index, int trueIndex)
{
    Vector3f position = new(source.Position.X + 256, source.Position.Y + 256, source.Position.Z);
    string label = $"Copy of {source.DisplayLabel}";
    return new Moby
    {
        Index = index,
        TrueIndex = trueIndex,
        LegacyIndex = MobyLoader.GetLegacyAliasIndex(trueIndex),
        Position = position,
        OriginalPosition = position,
        Type = source.Type,
        OriginalType = source.Type,
        State = source.State,
        OriginalState = source.State,
        YawByte = source.YawByte,
        OriginalYawByte = source.YawByte,
        SourceByte36 = source.SourceByte36,
        OriginalSourceByte36 = source.SourceByte36,
        SourceByte37 = source.SourceByte37,
        OriginalSourceByte37 = source.SourceByte37,
        SourceByte4F = source.SourceByte4F,
        OriginalSourceByte4F = source.SourceByte4F,
        Flag4A = source.Flag4A,
        OriginalFlag4A = source.Flag4A,
        Flag4B = source.Flag4B,
        OriginalFlag4B = source.Flag4B,
        Color = source.Color,
        OriginalColor = source.Color,
        Label = label,
        OriginalLabel = label,
        CandidateKind = source.CandidateKind,
        OriginalCandidateKind = source.CandidateKind,
        Confidence = "same-level-native-clone",
        OriginalConfidence = "same-level-native-clone",
        Evidence = $"Headless clone of T{source.TrueIndex}.",
        OriginalEvidence = $"Headless clone of T{source.TrueIndex}.",
        BehaviorNote = source.BehaviorNote,
        OriginalBehaviorNote = source.BehaviorNote,
        ZoneLabel = source.ZoneLabel,
        OriginalZoneLabel = source.ZoneLabel,
        PatchStatus = "native-clone",
        OriginalPatchStatus = "native-clone",
        PatchLead = $"Headless same-level clone of T{source.TrueIndex}.",
        OriginalPatchLead = $"Headless same-level clone of T{source.TrueIndex}.",
        SourceCloneLevelKey = "stonehill",
        SourceCloneLevelName = "Stone Hill",
        SourceCloneTrueIndex = source.TrueIndex,
        IsAdded = true
    };
}

Window OpenDialog(MainWindow owner, Button button, string description)
{
    button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button));
    for (int attempt = 0; attempt < 80 && owner.OwnedWindows.Count == 0; attempt++)
    {
        Dispatcher.UIThread.RunJobs();
        Thread.Sleep(10);
    }

    return owner.OwnedWindows.LastOrDefault()
        ?? throw new InvalidOperationException($"The {description} did not open.");
}

Window OpenAsyncDialog(MainWindow owner, Button button, string title, string description)
{
    button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button));
    for (int attempt = 0; attempt < 1000; attempt++)
    {
        Dispatcher.UIThread.RunJobs();
        Window? dialog = owner.OwnedWindows.LastOrDefault(window =>
            string.Equals(window.Title, title, StringComparison.Ordinal));
        if (dialog != null)
            return dialog;
        Thread.Sleep(10);
    }

    throw new InvalidOperationException($"The {description} did not open.");
}

static void WaitForDialogToClose(MainWindow owner, Window dialog, string description)
{
    for (int attempt = 0; attempt < 200 && owner.OwnedWindows.Contains(dialog); attempt++)
    {
        Dispatcher.UIThread.RunJobs();
        Thread.Sleep(10);
    }

    if (owner.OwnedWindows.Contains(dialog))
        throw new InvalidOperationException($"The {description} did not close.");
    FlushUi();
}

void SaveFrame(Window window, string fileName)
{
    string path = Path.Combine(outputDirectory, fileName);
    string? bestCandidate = null;
    int bestBlackPixelCount = int.MaxValue;
    int pixelCount = 0;
    List<string> candidates = [];
    for (int attempt = 0; attempt < 3; attempt++)
    {
        window.InvalidateMeasure();
        window.InvalidateArrange();
        window.InvalidateVisual();
        if (window.Content is Control content)
            content.InvalidateVisual();
        FlushUi();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(4);
        FlushUi();
        using Avalonia.Media.Imaging.Bitmap bitmap = window.CaptureRenderedFrame()
            ?? throw new InvalidOperationException($"Avalonia did not render {window.Title}.");
        string candidate = $"{path}.candidate-{attempt}.png";
        candidates.Add(candidate);
        bitmap.Save(candidate);
        Rgba32[] pixels = PngRgbaImage.ReadRgba(candidate, out _, out _);
        pixelCount = pixels.Length;
        int blackPixelCount = pixels.Count(pixel => pixel.A > 0 && pixel.R < 4 && pixel.G < 4 && pixel.B < 4);
        if (blackPixelCount < bestBlackPixelCount)
        {
            bestBlackPixelCount = blackPixelCount;
            bestCandidate = candidate;
        }
    }

    if (bestCandidate == null || pixelCount <= 0 || bestBlackPixelCount > pixelCount / 10)
        throw new InvalidOperationException($"Avalonia produced only partial/black captures for {window.Title} ({bestBlackPixelCount}/{pixelCount} black pixels).");
    File.Copy(bestCandidate, path, true);
    foreach (string candidate in candidates)
        File.Delete(candidate);
    Console.WriteLine($"Rendered dialog: {path}");
}

static void FlushUi()
{
    for (int attempt = 0; attempt < 8; attempt++)
    {
        Dispatcher.UIThread.RunJobs();
        Thread.Sleep(10);
    }
    AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
}

static void WaitForUiTask(Task task, string operation)
{
    for (int attempt = 0; attempt < 1000 && !task.IsCompleted; attempt++)
    {
        Dispatcher.UIThread.RunJobs();
        Thread.Sleep(5);
    }
    if (!task.IsCompleted)
        throw new TimeoutException($"{operation} exceeded five seconds.");
    task.GetAwaiter().GetResult();
    FlushUi();
}

static Button FindButton(Control root, string content) =>
    root.GetLogicalDescendants()
        .OfType<Button>()
        .First(button => string.Equals(button.Content?.ToString(), content, StringComparison.OrdinalIgnoreCase));

static Button FindButtonEither(Control root, string firstContent, string secondContent) =>
    root.GetLogicalDescendants()
        .OfType<Button>()
        .First(button =>
            string.Equals(button.Content?.ToString(), firstContent, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(button.Content?.ToString(), secondContent, StringComparison.OrdinalIgnoreCase));

static void AssertText(Control root, string expected)
{
    bool found = root.GetLogicalDescendants()
        .OfType<TextBlock>()
        .Any(text => string.Equals(text.Text, expected, StringComparison.OrdinalIgnoreCase));
    if (!found)
        throw new InvalidOperationException($"Expected UI label '{expected}' was not present.");
}

static void AssertTextAbsent(Control root, string forbidden)
{
    bool found = root.GetLogicalDescendants()
        .OfType<TextBlock>()
        .Any(text => text.Text?.Contains(forbidden, StringComparison.OrdinalIgnoreCase) == true);
    if (found)
        throw new InvalidOperationException($"Release UI unexpectedly exposed '{forbidden}'.");
}

static void AssertTextContains(Control root, string expected)
{
    bool found = root.GetLogicalDescendants()
        .OfType<TextBlock>()
        .Any(text => text.Text?.Contains(expected, StringComparison.OrdinalIgnoreCase) == true);
    if (!found)
    {
        string available = string.Join(" | ", root.GetLogicalDescendants()
            .OfType<TextBlock>()
            .Select(text => text.Text)
            .Where(text => !string.IsNullOrWhiteSpace(text)));
        throw new InvalidOperationException(
            $"Expected UI text containing '{expected}' was not present. Available text: {available}");
    }
}

static void AssertJsonString(JsonElement element, string propertyName, string expected)
{
    string? actual = element.GetProperty(propertyName).GetString();
    if (!string.Equals(actual, expected, StringComparison.Ordinal))
        throw new InvalidOperationException($"Expected JSON {propertyName} '{expected}', found '{actual}'.");
}

static void AssertJsonInt(JsonElement element, string propertyName, int expected)
{
    int actual = element.GetProperty(propertyName).GetInt32();
    if (actual != expected)
        throw new InvalidOperationException($"Expected JSON {propertyName} {expected}, found {actual}.");
}

static string CaptureProductionTerrainEditSignature(GeometryCandidate geometry)
{
    MethodInfo method = typeof(MainWindow).GetMethod(
        "BuildTerrainEditSignature",
        BindingFlags.Static | BindingFlags.NonPublic)
        ?? throw new MissingMethodException(typeof(MainWindow).FullName, "BuildTerrainEditSignature");
    return method.Invoke(null, [geometry]) as string
        ?? throw new InvalidOperationException("Production terrain-edit signature returned no value.");
}

static SortedDictionary<string, string> CaptureTerrainProjectInputHashes(
    string workspaceRoot,
    string levelKey)
{
    SortedDictionary<string, string> result = new(StringComparer.Ordinal);
    foreach (string path in Directory.EnumerateFiles(workspaceRoot, "*.json", SearchOption.TopDirectoryOnly))
    {
        string relativePath = Path.GetRelativePath(workspaceRoot, path).Replace('\\', '/');
        result[relativePath] = FileSha256(path);
    }

    string[] expectedTerrainInputs =
    [
        Path.Combine(workspaceRoot, $"{levelKey}-terrain-edits.json"),
        Path.Combine(workspaceRoot, $"{levelKey}-terrain-material-overrides.json"),
        CustomTerrainTextureStore.ManifestPath(workspaceRoot, levelKey),
        NativeTerrainTextureRelocationEditStore.ManifestPath(workspaceRoot, levelKey),
        Path.Combine(workspaceRoot, "spyro-project.json"),
        Path.Combine(workspaceRoot, "_local", "settings", "source-disc.json")
    ];
    foreach (string path in expectedTerrainInputs)
    {
        string relativePath = Path.GetRelativePath(workspaceRoot, path).Replace('\\', '/');
        result[relativePath] = File.Exists(path) ? FileSha256(path) : "<missing>";
    }

    return result;
}

static string CanonicalJsonSha256(string path, params string[] ignoredRootProperties)
{
    JsonNode root = JsonNode.Parse(File.ReadAllText(path))
        ?? throw new InvalidDataException($"Could not parse JSON for canonical hash: {path}");
    if (root is JsonObject rootObject)
    {
        foreach (string property in ignoredRootProperties)
            rootObject.Remove(property);
    }

    return Sha256Utf8(root.ToJsonString(new JsonSerializerOptions { WriteIndented = false }));
}

static string FileSha256(string path)
{
    return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)));
}

static string Sha256Utf8(string value)
{
    return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
        System.Text.Encoding.UTF8.GetBytes(value)));
}

static string ReadConfiguredSourceImagePath(string workspaceRoot)
{
    string path = Path.Combine(workspaceRoot, "_local", "settings", "source-disc.json");
    using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
    string? imagePath = document.RootElement.GetProperty("imagePath").GetString();
    return !string.IsNullOrWhiteSpace(imagePath)
        ? Path.GetFullPath(imagePath)
        : throw new InvalidOperationException("The isolated workspace source-disc setting has no image path.");
}

static string CreateIsolatedWorkspace(string sourceWorkspace, bool includeSourceDiscSetting)
{
    string isolated = Path.Combine(Path.GetTempPath(), $"spyro-editor-ui-smoke-{Guid.NewGuid():N}");
    Directory.CreateDirectory(isolated);
    try
    {
        string[] metadataRoots =
        [
            sourceWorkspace,
            Path.Combine(sourceWorkspace, "support")
        ];
        foreach (string metadataRoot in metadataRoots.Where(Directory.Exists))
        {
            foreach (string sourcePath in Directory.EnumerateFiles(metadataRoot, "*.json", SearchOption.TopDirectoryOnly))
            {
                string fileName = Path.GetFileName(sourcePath);
                bool isReadOnlyEditorMetadata =
                    fileName is "spyro-level-catalog.json" or "spyro-object-templates.json" or
                        "spyro-skybox-catalog.json" or "spyro-wad-analysis.json" or "global.json" ||
                    fileName.EndsWith("-behavior-links.json", StringComparison.OrdinalIgnoreCase) ||
                    fileName.EndsWith("-live-validation-overrides.json", StringComparison.OrdinalIgnoreCase) ||
                    fileName.EndsWith("-moby-user-overrides.json", StringComparison.OrdinalIgnoreCase);
                string isolatedPath = Path.Combine(isolated, fileName);
                if (isReadOnlyEditorMetadata && !File.Exists(isolatedPath))
                    File.Copy(sourcePath, isolatedPath);
            }
        }

        string sourceCache = Path.Combine(sourceWorkspace, "editor-cache");
        string isolatedCache = Path.Combine(isolated, "editor-cache");
        Directory.CreateDirectory(isolatedCache);
        foreach (string sourcePath in Directory.EnumerateFiles(sourceCache, "*", SearchOption.TopDirectoryOnly))
        {
            string isolatedPath = Path.Combine(isolatedCache, Path.GetFileName(sourcePath));
            File.Copy(sourcePath, isolatedPath);
        }

        // Native terrain preview PNGs are part of the viewport's read-only cache,
        // but live below the top-level geometry files. Preserve them in the
        // isolated smoke so terrain captures exercise the same bitmap path as
        // the packaged editor rather than silently falling back to flat fills.
        string sourceTerrainTextures = Path.Combine(sourceCache, "terrain-textures");
        if (Directory.Exists(sourceTerrainTextures))
        {
            foreach (string sourcePath in Directory.EnumerateFiles(sourceTerrainTextures, "*", SearchOption.AllDirectories))
            {
                string relativePath = Path.GetRelativePath(sourceCache, sourcePath);
                string isolatedPath = Path.Combine(isolatedCache, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(isolatedPath)!);
                File.Copy(sourcePath, isolatedPath);
            }
        }

        if (includeSourceDiscSetting)
        {
            string settingsDirectory = Path.Combine(isolated, "_local", "settings");
            Directory.CreateDirectory(settingsDirectory);
            string sourceDiscSettings = Path.Combine(sourceWorkspace, "_local", "settings", "source-disc.json");
            if (!File.Exists(sourceDiscSettings))
                throw new FileNotFoundException("The UI smoke requires the editor's existing source-disc setting.", sourceDiscSettings);
            File.Copy(sourceDiscSettings, Path.Combine(settingsDirectory, "source-disc.json"));
        }

        return isolated;
    }
    catch
    {
        TryDeleteDirectory(isolated);
        throw;
    }
}

static void TryDeleteDirectory(string path)
{
    try
    {
        if (Directory.Exists(path))
            Directory.Delete(path, recursive: true);
    }
    catch (IOException)
    {
        // A failed best-effort cleanup should not hide the smoke result.
    }
    catch (UnauthorizedAccessException)
    {
        // A failed best-effort cleanup should not hide the smoke result.
    }
}

static string FindWorkspace(string? explicitPath)
{
    if (!string.IsNullOrWhiteSpace(explicitPath))
        return Path.GetFullPath(explicitPath);

    DirectoryInfo? current = new(Environment.CurrentDirectory);
    while (current != null)
    {
        if (File.Exists(Path.Combine(current.FullName, "spyro-level-catalog.json")))
            return current.FullName;
        current = current.Parent;
    }

    throw new DirectoryNotFoundException("Could not find the Spyro editor workspace.");
}

public static class HeadlessEntryPoint
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = false
            });
}

file sealed record NativeTerrainOrderingUiInventory(
    string NativeBaseOrderingContract,
    string NativePassOrderContract,
    int LevelCount,
    int HighPolyFaceCount,
    int HighPolyRendererFlagAliasFaceCount,
    int MaximumHighPolyBaseBias,
    bool RendererFlagAliasBucketsUnaffected);

file sealed record MobyRegressionSnapshot(
    Vector3f Position,
    int Type,
    int State,
    int YawByte,
    int SourceByte36,
    int SourceByte37,
    int SourceByte4F,
    int Flag4A,
    int Flag4B,
    string Label,
    ColorRgba Color,
    bool HasLoadedNativeEdit,
    string LoadedNativeEditSummary,
    bool IsAdded,
    bool IsRemoved,
    bool HasAnyEdit,
    string CrossLevelTemplateId,
    string CrossLevelFamily,
    string CrossLevelSourceLevelKey,
    string CrossLevelSourceLevelName,
    int CrossLevelSourceTrueIndex,
    string CrossLevelRequiredExporterFeature,
    string SourceCloneLevelKey,
    string SourceCloneLevelName,
    int SourceCloneTrueIndex,
    string CandidateKind,
    string Confidence,
    string Evidence,
    string BehaviorNote,
    string ZoneLabel,
    string PatchStatus,
    string PatchLead);

file sealed record MobyLinkSemanticSnapshot(
    int OwnerTrueIndex,
    string Key,
    string Kind,
    string MemberSet);
