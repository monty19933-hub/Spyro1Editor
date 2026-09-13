using System.Reflection;
using System.Security.Cryptography;
using Avalonia.Threading;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

internal static class FreeFlightProductionCases
{
    // Opt-in exact-source regression of the SAME MainWindow method and final
    // transaction used by Create BIN. Never called by the source-free suite.
    internal static void Run(MainWindow window, string workspace, string originalImage, string originalCue, string newDirectory)
    {
        string root = Path.GetFullPath(newDirectory);
        if (File.Exists(root) || Directory.Exists(root)) throw new IOException("Flight production regression requires a new exact output directory.");
        if (Hash(originalImage) != NativeFreeFlightPatchPlan.SourceImageSha256)
            throw new InvalidDataException("Production regression requires the exact original disc.");
        string cueBefore = Hash(originalCue);
        Directory.CreateDirectory(root);
        string output = Directory.CreateDirectory(Path.Combine(root, "normal-create-bin")).FullName;
        var method = typeof(MainWindow).GetMethod("CreateAllSavedEditsTestAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var target = typeof(MainWindow).GetNestedType("EditedLevelExportTarget", BindingFlags.NonPublic)!;
        var targets = Array.CreateInstance(target, 0);
        string image = Path.Combine(output, "Spyro Editor - All Saved Edits.bin");
        string cue = Path.ChangeExtension(image, ".cue");
        var saved = NativeFreeFlightSettingsStore.Save(NativeFreeFlightSettingsStore.Capture(workspace), 14, true);
        byte[] onSettings = File.ReadAllBytes(saved.Path);
        Build();
        string onHash = Hash(image);
        if (onHash == NativeFreeFlightPatchPlan.SourceImageSha256 || !File.Exists(cue))
            throw new InvalidDataException("Saved ON did not publish a changed complete BIN/CUE through Create BIN.");
        string onImage = Path.Combine(root, "toasty-flight-on.bin");
        File.Copy(image, onImage);
        File.WriteAllText(Path.Combine(root, "toasty-flight-on.cue"),
            File.ReadAllText(cue).Replace(Path.GetFileName(image), Path.GetFileName(onImage), StringComparison.Ordinal));
        string onCueHash = Hash(cue);
        var hook = typeof(MainWindow).GetProperty("FreeFlightExportDelayOverrideForTesting", BindingFlags.Instance | BindingFlags.NonPublic)!;
        hook.SetValue(window, (Func<string, Task>)(stage =>
        {
            if (stage != "free-flight-before-publication") throw new InvalidOperationException("Unexpected delayed stage.");
            NativeFreeFlightSettingsStore.Save(NativeFreeFlightSettingsStore.Capture(workspace), 14, false);
            return Task.CompletedTask;
        }));
        bool refused = false;
        try { Build(); }
        catch (InvalidDataException) { refused = true; }
        finally { hook.SetValue(window, null); }
        if (!refused || Hash(image) != onHash || Hash(cue) != onCueHash)
            throw new InvalidDataException("A stale ON export replaced the previous usable BIN/CUE after OFF was saved.");
        // The rejected ON operation left explicit OFF saved. There are no
        // ordinary target flags, so this is the former no-op regression.
        Build();
        if (Hash(image) != NativeFreeFlightPatchPlan.SourceImageSha256)
            throw new InvalidDataException("Explicit OFF did not replace the old ON output with the complete original ROM.");
        if (Hash(originalImage) != NativeFreeFlightPatchPlan.SourceImageSha256 || Hash(originalCue) != cueBefore)
            throw new InvalidDataException("Production export modified its original disc pair.");
        File.WriteAllBytes(Path.Combine(root, "on-project-settings.json"), onSettings);
        File.Copy(saved.Path, Path.Combine(root, "off-project-settings.json"));
        Console.WriteLine($"FREE_FLIGHT_NORMAL_CREATE_BIN_SOURCE_OK on={onHash} off={Hash(image)} staleOnPreservedPrevious=1 output={output} nativeGameplayVerified=0");

        void Build()
        {
            Task task = (Task)method.Invoke(window, [originalImage, originalCue, output, targets])!;
            DateTime deadline = DateTime.UtcNow.AddMinutes(10);
            while (!task.IsCompleted && DateTime.UtcNow < deadline)
            { Dispatcher.UIThread.RunJobs(); Thread.Sleep(1); }
            if (!task.IsCompleted) throw new TimeoutException("Normal flight export timed out.");
            task.GetAwaiter().GetResult();
            object result = task.GetType().GetProperty("Result")!.GetValue(task)!;
            if (!(bool)result.GetType().GetProperty("WroteImage")!.GetValue(result)!)
                throw new InvalidDataException("An explicit saved flight choice returned no ready patches.");
        }
    }

    private static string Hash(string path)
    { using var file = File.OpenRead(path); return Convert.ToHexStringLower(SHA256.HashData(file)); }
}
