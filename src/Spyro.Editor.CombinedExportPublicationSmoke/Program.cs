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
        NativeFreeFlightSettingsStore.Save(saved, 14, enabled);
        string output = Directory.CreateDirectory(Path.Combine(fixtureRoot, enabled ? "flight-on" : "flight-off")).FullName;
        var previous = SeedPreviousOutput(output);
        Exception? failure = null;
        try { Complete((Task)build.Invoke(window, [sourceImage, sourceCue, output, Array.CreateInstance(targetType, 0)])!); }
        catch (Exception error) { failure = error; }
        Require(failure != null, "Explicit Fly Anywhere ON/OFF must reach exact-source validation, not return no-patches and leave an old enabled BIN silently active.");
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
