using System.Reflection;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Threading;
using Spyro.Editor.App;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Persistence;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Workspace;

// Synthetic documents exercise the real UI history/restore boundary without a
// retail disc, extracted cache, emulator, or the user's project registration.
string fixtureRoot = Path.Combine(Path.GetTempPath(), $"spyro-texture-history-{Guid.NewGuid():N}");
string projectA = Path.Combine(fixtureRoot, "project-a");
string projectB = Path.Combine(fixtureRoot, "project-b");
Directory.CreateDirectory(projectA);
Directory.CreateDirectory(projectB);
File.WriteAllText(Path.Combine(projectA, "spyro-level-catalog.json"), "{\"levels\":[]}");
File.WriteAllText(Path.Combine(projectB, "spyro-level-catalog.json"), "{\"levels\":[]}");
string? previousWorkspace = Environment.GetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE");
string? previousRelease = Environment.GetEnvironmentVariable("SPYRO_EDITOR_RELEASE");

try
{
    Environment.SetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE", projectA);
    Environment.SetEnvironmentVariable("SPYRO_EDITOR_RELEASE", "1");
    AppBuilder.Configure<App>().UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
        .SetupWithoutStarting();
    MainWindow window = new();
    EditorWorkspace originalWorkspace = GetField<EditorWorkspace>(window, "_workspace");
    LevelDefinition level = new() { Key = "stonehill", DisplayName = "Stone Hill", LevelId = 11 };
    SetField(window, "_currentLevel", level);
    GeometryCandidate originalGeometry = MakeGeometry(textureId: 1);
    SetField(window, "_currentGeometry", originalGeometry);
    object checkpoint = Invoke<object>(window, "CaptureTerrainTexturePaintHistoryCheckpoint", "fixture paint");
    originalGeometry.Polygons[0].ApplyTextureOverride(2);
    InvokeVoid(window, "PushTerrainTexturePaintHistory", checkpoint);

    // A level reload in the same project may replace scene objects, but must
    // preserve the session's texture undo history and unrelated height edits.
    GeometryCandidate reloadedGeometry = MakeGeometry(textureId: 1);
    reloadedGeometry.Polygons[0].ApplyTextureOverride(2);
    reloadedGeometry.Polygons[0].ApplyTerrainVertexDeltas([3f, 3f, 3f]);
    SetField(window, "_currentGeometry", reloadedGeometry);
    Require(Invoke<int>(window, "TerrainTexturePaintHistoryCountForCurrentLevel") == 1,
        "A same-project scene reload lost its texture history.");
    Require(CompleteResult(Invoke<Task<bool>>(window, "UndoLastTerrainTextureActionAsync")),
        "Same-project texture Undo Last was refused.");
    Require(reloadedGeometry.Polygons[0].TextureId == 1 &&
        reloadedGeometry.Polygons[0].TerrainVertexDeltas().All(delta => delta == 3f),
        "Same-project texture Undo Last did not preserve later height edits.");

    // Keep an actual history checkpoint across a workspace commit. Its files
    // still name project A, although project B has the same level/face keys.
    object oldCheckpoint = Invoke<object>(window, "CaptureTerrainTexturePaintHistoryCheckpoint", "old project");
    reloadedGeometry.Polygons[0].ApplyTextureOverride(2);
    InvokeVoid(window, "PushTerrainTexturePaintHistory", oldCheckpoint);
    string oldManifest = NativeTerrainTextureRelocationEditStore.ManifestPath(projectA, level.Key);
    const string marker = "preserve the previous project's newer manifest";
    File.WriteAllText(oldManifest, marker);
    SetField(window, "_workspace", new EditorWorkspace(projectB));
    InvokeVoid(window, "ClearLoadedSceneForWorkspaceCommit");
    SetField(window, "_currentLevel", level);
    SetField(window, "_currentGeometry", MakeGeometry(textureId: 2));

    // Even a stale retained callback must be rejected before touching any
    // absolute path in its checkpoint, independently of history invalidation.
    Exception? rejected = null;
    try
    {
        Complete(Invoke<Task>(window, "RestoreTerrainTexturePaintHistoryCheckpointAsync", oldCheckpoint));
    }
    catch (Exception exception)
    {
        rejected = exception;
    }
    Require(File.Exists(oldManifest) && File.ReadAllText(oldManifest) == marker,
        "Cross-workspace texture history modified a file in the previous project.");
    Require(rejected is InvalidOperationException &&
        rejected.Message.Contains("workspace", StringComparison.OrdinalIgnoreCase),
        "A stale texture checkpoint was not refused by workspace identity before restoration.");
    Require(!File.Exists(Path.Combine(projectB, "stonehill-terrain-edits.json")),
        "A stale texture checkpoint wrote terrain edits into the new project.");
    Require(Invoke<int>(window, "TerrainTexturePaintHistoryCountForCurrentLevel") == 0,
        "A workspace commit retained the previous project's texture history.");
    Require(!CompleteResult(Invoke<Task<bool>>(window, "UndoLastTerrainTextureActionAsync")),
        "Undo Last remained available after the project changed.");

    // Reopening the old path starts a new project session; object identity
    // must prevent an old retained callback from regaining write authority.
    SetField(window, "_workspace", new EditorWorkspace(originalWorkspace.RootPath));
    rejected = null;
    try
    {
        Complete(Invoke<Task>(window, "RestoreTerrainTexturePaintHistoryCheckpointAsync", oldCheckpoint));
    }
    catch (Exception exception)
    {
        rejected = exception;
    }
    Require(rejected is InvalidOperationException &&
        File.Exists(oldManifest) && File.ReadAllText(oldManifest) == marker,
        "Reopening the old path reauthorized a stale texture checkpoint.");
    Console.WriteLine("TEXTURE_HISTORY_WORKSPACE_SMOKE_OK sameWorkspaceReload=1 heightPreserved=1 crossWorkspaceRefused=1 oldProjectUntouched=1 newProjectUntouched=1 historyCleared=1 reopenedSessionRefused=1");
    return 0;
}
finally
{
    Environment.SetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE", previousWorkspace);
    Environment.SetEnvironmentVariable("SPYRO_EDITOR_RELEASE", previousRelease);
    Directory.Delete(fixtureRoot, recursive: true);
}

static GeometryCandidate MakeGeometry(int textureId)
{
    GeometryCandidate geometry = new();
    geometry.Polygons.Add(new TerrainPolygon(
        [new Vector2f(0, 0), new Vector2f(10, 0), new Vector2f(0, 10)],
        [0f, 0f, 0f], textureId, 0, 0, "hp", new ColorRgba(255, 255, 255, 255)));
    return geometry;
}

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

static T GetField<T>(MainWindow window, string name) =>
    (T)typeof(MainWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;

static void SetField(MainWindow window, string name, object value) =>
    typeof(MainWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, value);

static MethodInfo Method(string name) => typeof(MainWindow)
    .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic).Single(method => method.Name == name);

static T Invoke<T>(MainWindow window, string name, params object?[] arguments) =>
    (T)Method(name).Invoke(window, arguments)!;

static void InvokeVoid(MainWindow window, string name, params object?[] arguments) =>
    Method(name).Invoke(window, arguments);

static bool CompleteResult(Task<bool> task)
{
    Complete((Task)task);
    return task.GetAwaiter().GetResult();
}

static void Complete(Task task)
{
    DateTime deadline = DateTime.UtcNow.AddSeconds(20);
    while (!task.IsCompleted && DateTime.UtcNow < deadline)
    {
        Dispatcher.UIThread.RunJobs();
        Thread.Sleep(1);
    }
    Require(task.IsCompleted, "Texture history operation timed out.");
    task.GetAwaiter().GetResult();
}
