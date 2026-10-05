using System.Reflection;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Threading;
using Spyro.Editor.App;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Persistence;

if (args.Length != 0 && (args.Length != 4 || args[0] != "--flight-production"))
    throw new ArgumentException("Usage: CombinedExportPublicationSmoke [--flight-production original.bin original.cue NEW_DIRECTORY]");

VerifyExportScratchSpace();

string fixtureRoot = Path.Combine(Path.GetTempPath(), $"spyro-combined-publication-{Guid.NewGuid():N}");
Directory.CreateDirectory(fixtureRoot);
File.WriteAllText(Path.Combine(fixtureRoot, "spyro-level-catalog.json"), "{\"levels\":[]}");
string? previousWorkspace = Environment.GetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE");
string? previousRelease = Environment.GetEnvironmentVariable("SPYRO_EDITOR_RELEASE");
try
{
    Environment.SetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE", fixtureRoot);
    Environment.SetEnvironmentVariable("SPYRO_EDITOR_RELEASE", "1");
    AppBuilder.Configure<App>().UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
        .SetupWithoutStarting();
    MainWindow window = new();
    string sourceImage = Path.Combine(fixtureRoot, "synthetic-source.bin");
    string sourceCue = Path.Combine(fixtureRoot, "synthetic-source.cue");
    File.WriteAllText(sourceImage, "not a retail disc; only no-op and early-failure paths may read this fixture");
    File.WriteAllText(sourceCue, "FILE \"synthetic-source.bin\" BINARY\n");
    Type targetType = typeof(MainWindow).GetNestedType("EditedLevelExportTarget", BindingFlags.NonPublic)!;
    MethodInfo build = typeof(MainWindow).GetMethod("CreateAllSavedEditsTestAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;

    foreach (bool upstreamFailure in new[] { false, true })
    {
        string output = Path.Combine(fixtureRoot, upstreamFailure ? "upstream-failure" : "no-ready-patches");
        Directory.CreateDirectory(output);
        Dictionary<string, string> originals = SeedPreviousOutput(output);
        Array targets = Array.CreateInstance(targetType, upstreamFailure ? 1 : 0);
        if (upstreamFailure)
        {
            LevelDefinition level = new()
            {
                Key = "stonehill", DisplayName = "Stone Hill", LevelId = 11,
                SourceWadEntry = 0, SourceRecordCount = 1, SourceTableWadOffset = "0x0"
            };
            targets.SetValue(Activator.CreateInstance(targetType,
                level, true, false, false, false, false, false, false), 0);
        }
        Exception? failure = null;
        try
        {
            Task work = (Task)build.Invoke(window, [sourceImage, sourceCue, output, targets])!;
            Complete(work);
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        Require(!upstreamFailure || failure != null, "The invalid source fixture did not exercise an upstream build failure.");
        Require(upstreamFailure || failure == null, $"No-patch build failed unexpectedly: {failure}");
        foreach ((string path, string expected) in originals)
            Require(File.Exists(path) && File.ReadAllText(path) == expected,
                $"{(upstreamFailure ? "Upstream failure" : "No ready patches")} destroyed previous usable output: {Path.GetFileName(path)}");
    }
    foreach (bool enabled in new[] { false, true })
    {
        var saved = NativeFreeFlightSettingsStore.Capture(fixtureRoot);
        var preference = NativeFreeFlightSettingsStore.Save(saved, 14, enabled);
        string output = Directory.CreateDirectory(Path.Combine(fixtureRoot, enabled ? "flight-on" : "flight-off")).FullName;
        var previous = SeedPreviousOutput(output);
        Exception? failure = null;
        try { Complete((Task)build.Invoke(window, [sourceImage, sourceCue, output, Array.CreateInstance(targetType, 0)])!); }
        catch (Exception error) { failure = error; }
        // Fly Anywhere ships: a saved choice with nothing else to export asks for a rebuild, which needs
        // the exact original disc. This fixture is not one, so the export must refuse, and refuse cleanly.
        string refusal = (failure is System.Reflection.TargetInvocationException wrapped ? wrapped.InnerException : failure)?.ToString() ?? "";
        Require(failure != null && refusal.Contains("unchanged original USA disc image", StringComparison.Ordinal),
            "A saved Fly Anywhere choice must be refused for a source that is not the exact original disc: " + refusal);
        Require(NativeFreeFlightSettingsStore.Capture(fixtureRoot).ContentSha256 == preference.ContentSha256,
            "A refused export must preserve the saved flight preference.");
        foreach (var artifact in previous)
            Require(File.ReadAllText(artifact.Key) == artifact.Value, "Rejected flight source replaced previous output.");
    }
    File.Delete(Path.Combine(fixtureRoot, NativeFreeFlightSettingsStore.FileName));
    if (!OperatingSystem.IsWindows())
    {
        foreach (bool aliasCue in new[] { false, true })
        {
            string output = Directory.CreateDirectory(Path.Combine(fixtureRoot, aliasCue ? "source-cue-alias" : "source-bin-alias")).FullName;
            var previous = SeedPreviousOutput(output);
            string alias = Path.Combine(fixtureRoot, aliasCue ? "chosen-cue-alias.cue" : "chosen-bin-alias.bin");
            File.CreateSymbolicLink(alias, Path.Combine(output, "Spyro Editor - All Saved Edits" + (aliasCue ? ".cue" : ".bin")));
            Exception? failure = null;
            try { Complete((Task)build.Invoke(window, [aliasCue ? sourceImage : alias, aliasCue ? alias : sourceCue,
                output, Array.CreateInstance(targetType, 0)])!); }
            catch (Exception error) { failure = error; }
            Require(failure != null, "Create BIN must refuse a selected source alias to its final publication destination before staging, even with no ordinary edits.");
            Require(new FileInfo(alias).LinkTarget != null && previous.All(p => File.ReadAllText(p.Key) == p.Value),
                "Source-alias refusal changed a caller-owned source or previous artifact.");
        }
    }
    string transactionCoverage = ArtifactTransactionCases.Run(fixtureRoot);
    string newOnlyCoverage = NewArtifactPublicationCases.Run(fixtureRoot);
    string diagnosticCoverage = PublishedDiagnosticsCases.Run(window, fixtureRoot);
    string privateTextureEvidenceCoverage = PrivateTextureEvidenceCases.Run();
    if (args.Length == 4)
        FreeFlightProductionCases.Run(window, fixtureRoot, args[1], args[2], args[3]);
    Console.WriteLine($"COMBINED_EXPORT_PUBLICATION_SMOKE_OK noPatchPreservesPrevious=1 upstreamFailurePreservesPrevious=1 {transactionCoverage} {newOnlyCoverage} {diagnosticCoverage} {privateTextureEvidenceCoverage}");
    return 0;
}
finally
{
    Environment.SetEnvironmentVariable("SPYRO_EDITOR_WORKSPACE", previousWorkspace);
    Environment.SetEnvironmentVariable("SPYRO_EDITOR_RELEASE", previousRelease);
    Directory.Delete(fixtureRoot, recursive: true);
}

static Dictionary<string, string> SeedPreviousOutput(string directory)
{
    Dictionary<string, string> originals = [];
    foreach (string suffix in new[] { ".bin", ".cue", ".combined-export-summary.json", ".skipped-edits.txt", ".diagnostics.txt", ".diagnostics.zip" })
    {
        string path = Path.Combine(directory, "Spyro Editor - All Saved Edits" + suffix);
        string content = "previous verified artifact " + suffix;
        File.WriteAllText(path, content);
        originals.Add(path, content);
    }
    return originals;
}

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

// Create BIN working copies must not accumulate in the project: stale staging folders are swept,
// a running export's folder is kept, and only the newest authored baseline image survives.
static void VerifyExportScratchSpace()
{
    string workspace = Path.Combine(Path.GetTempPath(), $"spyro-export-scratch-{Guid.NewGuid():N}");
    try
    {
        string root = Spyro.Editor.Core.Exporting.ExportScratchSpace.AggregateRoot(workspace);
        string stale = Path.Combine(root, "stale"), nested = Path.Combine(stale, "_combined-build"), live = Path.Combine(root, "live");
        Directory.CreateDirectory(nested); Directory.CreateDirectory(live);
        File.WriteAllBytes(Path.Combine(stale, "retail-stage.bin"), new byte[4096]);
        File.WriteAllBytes(Path.Combine(nested, "step.bin"), new byte[1024]);
        File.WriteAllBytes(Path.Combine(live, "fresh-host.bin"), new byte[2048]);
        DateTime old = DateTime.UtcNow.AddHours(-3);
        foreach (string file in Directory.GetFiles(stale, "*", SearchOption.AllDirectories)) File.SetLastWriteTimeUtc(file, old);
        Directory.SetLastWriteTimeUtc(nested, old); Directory.SetLastWriteTimeUtc(stale, old);
        long freed = Spyro.Editor.Core.Exporting.ExportScratchSpace.SweepStale(workspace, TimeSpan.FromHours(1));
        Require(freed == 5120 && !Directory.Exists(stale), "Stale Create BIN working copies were not removed.");
        Require(File.Exists(Path.Combine(live, "fresh-host.bin")), "A recent export's working copy must be kept.");
        Spyro.Editor.Core.Exporting.ExportScratchSpace.Delete(live);
        Spyro.Editor.Core.Exporting.ExportScratchSpace.Delete(Path.Combine(root, "never-created"));
        Require(!Directory.Exists(live), "An export's working copy must be deletable when it finishes.");
        string baselines = Path.Combine(workspace, "_local", "authored-native");
        Directory.CreateDirectory(baselines);
        string keep = Path.Combine(baselines, "scene-new.bin"), dead = Path.Combine(baselines, "scene-old.bin");
        File.WriteAllBytes(keep, new byte[16]); File.WriteAllBytes(dead, new byte[32]);
        Require(Spyro.Editor.Core.Exporting.ExportScratchSpace.PruneAuthoredBaselines(workspace, keep) == 32 &&
                File.Exists(keep) && !File.Exists(dead), "Only the newest authored baseline image may remain.");
        Require(Spyro.Editor.Core.Exporting.ExportScratchSpace.SweepStale(Path.Combine(workspace, "missing"), TimeSpan.Zero) == 0,
            "Sweeping a project without working copies must be a no-op.");
        // The ordinary export stages images under output/_combined-build/run-*: images go, reports and the game stay.
        string output = Path.Combine(workspace, "output"), run = Path.Combine(output, "_combined-build", "run-abc");
        Directory.CreateDirectory(run);
        string game = Path.Combine(output, "Spyro Editor - All Saved Edits.bin"), staged = Path.Combine(run, "03-terrain.bin"),
            report = Path.Combine(run, "current.build-safety.json"), tiny = Path.Combine(run, "note.bin");
        File.WriteAllBytes(game, new byte[4096]); File.WriteAllBytes(staged, new byte[3000]);
        File.WriteAllText(report, "{}"); File.WriteAllBytes(tiny, new byte[10]);
        Require(Spyro.Editor.Core.Exporting.ExportScratchSpace.DeleteStagedImages(output, smallestImageBytes: 1000) == 3000 &&
                !File.Exists(staged) && File.Exists(report) && File.Exists(tiny) && File.Exists(game),
            "Only staged disc images may be removed from the ordinary export's working folder.");
        Require(Spyro.Editor.Core.Exporting.ExportScratchSpace.DeleteStagedImages(Path.Combine(workspace, "no-output")) == 0,
            "Cleaning a project that never exported must be a no-op.");
    }
    finally
    {
        if (Directory.Exists(workspace)) Directory.Delete(workspace, recursive: true);
    }
}

static void Complete(Task task)
{
    DateTime deadline = DateTime.UtcNow.AddSeconds(20);
    while (!task.IsCompleted && DateTime.UtcNow < deadline)
    {
        Dispatcher.UIThread.RunJobs();
        Thread.Sleep(1);
    }
    Require(task.IsCompleted, "Combined publication operation timed out.");
    task.GetAwaiter().GetResult();
}
