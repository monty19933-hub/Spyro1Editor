using System.Buffers.Binary;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using Avalonia.Controls;
using Avalonia.Threading;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Music;
using Spyro.Editor.Core.Workspace;

internal static class PublishedDiagnosticsCases
{
    public static string Run(MainWindow window, string fixtureRoot)
    {
        string workspace = Path.Combine(fixtureRoot, "published-diagnostics-workspace");
        string output = Path.Combine(fixtureRoot, "published-diagnostics-output");
        Directory.CreateDirectory(workspace);
        Directory.CreateDirectory(output);
        FieldInfo workspaceField = Field("_workspace");
        object previousWorkspace = workspaceField.GetValue(window)!;
        workspaceField.SetValue(window, new EditorWorkspace(workspace));
        try
        {
            const int selectedTrack = 2;
            LevelDefinition level = new() { Key = "artisans", DisplayName = "Artisans", LevelId = 10 };
            Complete(LevelMusicEditStore.SaveAsync(workspace, level, selectedTrack));
            string sourceImage = Path.Combine(workspace, "synthetic-music.bin");
            string sourceCue = Path.Combine(workspace, "synthetic-music.cue");
            byte[] original = BuildMusicFixture();
            File.WriteAllBytes(sourceImage, original);
            File.WriteAllText(sourceCue, "FILE \"synthetic-music.bin\" BINARY\n  TRACK 01 MODE1/2048\n    INDEX 01 00:00:00\n");
            string finalPrefix = Path.Combine(output, "Spyro Editor - All Saved Edits");
            foreach (string extension in new[] { ".bin", ".cue", ".build-safety.json", ".build-safety.md" })
                File.WriteAllText(finalPrefix + extension, "historical unrelated artifact " + extension);

            Type targetType = typeof(MainWindow).GetNestedType("EditedLevelExportTarget", BindingFlags.NonPublic)!;
            Array targets = Array.CreateInstance(targetType, 1);
            targets.SetValue(Activator.CreateInstance(targetType,
                level, false, false, false, false, false, true, false), 0);
            Task build = (Task)Invoke(window, "CreateAllSavedEditsTestAsync", sourceImage, sourceCue, output, targets)!;
            Complete(build);
            object result = build.GetType().GetProperty("Result")!.GetValue(build)!;
            Require((bool)result.GetType().GetProperty("WroteImage")!.GetValue(result)!, "Synthetic music build did not publish through the real coordinator.");
            byte[] publishedImage = File.ReadAllBytes(finalPrefix + ".bin");
            string publishedCue = File.ReadAllText(finalPrefix + ".cue");
            byte[] expected = original.ToArray();
            BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(20 * 2048 + 0x900), selectedTrack);
            Require(publishedImage.AsSpan().SequenceEqual(expected), "Successful coordinator changed bytes beyond the selected music-table value.");
            Require(File.ReadAllBytes(sourceImage).AsSpan().SequenceEqual(original), "Successful coordinator changed its original source image.");
            Require(publishedCue.Contains("FILE \"Spyro Editor - All Saved Edits.bin\" BINARY", StringComparison.Ordinal), "Final CUE does not reference the published image.");

            List<string> failures = [];
            IReadOnlyList<string> artifacts = (IReadOnlyList<string>)Invoke(window,
                "CollectCombinedExportDiagnosticArtifacts", output, targets, result, Array.Empty<string>())!;
            if (artifacts.Any(path => path.EndsWith(".build-safety.json", StringComparison.Ordinal) || path.EndsWith(".build-safety.md", StringComparison.Ordinal)))
                failures.Add("Music-only publication included historical Build Safety reports in its current support artifacts.");

            Complete((Task)Invoke(window, "CompleteCombinedExportPublicationAsync",
                sourceImage, output, targets, result, "Artisans [music=true]", null)!);
            using (ZipArchive bundle = ZipFile.OpenRead(finalPrefix + ".diagnostics.zip"))
                Require(!bundle.Entries.Any(entry => entry.Name.EndsWith(".build-safety.json", StringComparison.Ordinal)),
                    "Music-only support ZIP contains a historical Build Safety report.");

            // A captured report is serialized from this invocation's object,
            // even if the reusable UI report files contain unrelated content.
            MobyBuildSafetyReport currentReport = new(
                DateTimeOffset.Parse("2026-09-04T12:34:56Z"), sourceImage,
                MobyBuildSafetyStatus.Stable, [], ["current-report-provenance"]);
            Complete((Task)Invoke(window, "CompleteCombinedExportPublicationAsync",
                sourceImage, output, targets, result, "Artisans [music=true]", currentReport)!);
            string buildDirectory = (string)result.GetType().GetProperty("BuildDirectory")!.GetValue(result)!;
            string snapshot = Path.Combine(buildDirectory, "current.build-safety.json");
            Require(File.ReadAllText(snapshot).Contains("current-report-provenance", StringComparison.Ordinal) &&
                File.Exists(Path.Combine(buildDirectory, "current.build-safety.md")),
                "Current safety snapshot was not serialized into the successful run directory.");
            using (ZipArchive bundle = ZipFile.OpenRead(finalPrefix + ".diagnostics.zip"))
            {
                ZipArchiveEntry[] safety = bundle.Entries.Where(entry => entry.Name.EndsWith(".build-safety.json", StringComparison.Ordinal)).ToArray();
                Require(safety.Length == 1, "Support ZIP did not include exactly its current safety snapshot.");
                using StreamReader reader = new(safety[0].Open());
                Require(reader.ReadToEnd().Contains("current-report-provenance", StringComparison.Ordinal),
                    "Support ZIP used a reusable historical report instead of the captured current report.");
            }

            // A workspace directory may disappear/unmount after publication.
            // Output is deliberately outside it so this faults diagnostics only.
            string movedWorkspace = workspace + "-moved";
            Directory.Move(workspace, movedWorkspace);
            try
            {
                Task completion = (Task)Invoke(window, "CompleteCombinedExportPublicationAsync",
                    sourceImage, output, targets, result, "Artisans [music=true]", null)!;
                Complete(completion);
            }
            catch (Exception exception)
            {
                failures.Add($"Postpublication artifact collection escaped as a failed Create BIN: {exception.GetType().Name}.");
            }
            string status = ((TextBlock)Field("_statusText").GetValue(window)!).Text ?? "";
            if (!status.StartsWith("Created all-edits test ", StringComparison.Ordinal) ||
                !status.Contains("diagnostic", StringComparison.OrdinalIgnoreCase))
                failures.Add("A published BIN with unavailable diagnostics was not reported as successful with a diagnostic warning.");
            Require(File.ReadAllBytes(finalPrefix + ".bin").AsSpan().SequenceEqual(publishedImage) &&
                File.ReadAllText(finalPrefix + ".cue") == publishedCue, "Diagnostic failure altered the published BIN/CUE.");
            Require(failures.Count == 0, string.Join("\n", failures));
            return "successfulCoordinator=1 staleSafetyExcluded=1 currentSafetySnapshotBundled=1 postpublicationDiagnosticFailurePreservesSuccess=1";
        }
        finally { workspaceField.SetValue(window, previousWorkspace); }
    }

    private static byte[] BuildMusicFixture()
    {
        byte[] image = new byte[40 * 2048];
        Span<byte> pvd = image.AsSpan(16 * 2048, 2048);
        pvd[0] = 1;
        Encoding.ASCII.GetBytes("CD001").CopyTo(pvd[1..]);
        pvd[6] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(pvd[128..], 2048);
        BinaryPrimitives.WriteUInt16BigEndian(pvd[130..], 2048);
        Record(pvd[156..], "\0", 18, 2048);
        Span<byte> directory = image.AsSpan(18 * 2048, 2048);
        int cursor = Record(directory, "SCUS_942.28;1", 20, 4096);
        for (int index = 0; index < 6; index++)
            cursor += Record(directory[cursor..], $"PETEXA{index}.STR;1", 30 + index, 2048);
        Span<byte> exe = image.AsSpan(20 * 2048, 4096);
        Encoding.ASCII.GetBytes("PS-X EXE").CopyTo(exe);
        BinaryPrimitives.WriteUInt32LittleEndian(exe[0x18..], 0x80010000);
        BinaryPrimitives.WriteInt32LittleEndian(exe[0x1C..], 2048);
        for (int index = 0; index < 48; index++)
            BinaryPrimitives.WriteInt32LittleEndian(exe[(0x900 + index * 4)..], index);
        for (int index = 0; index < 6; index++)
            BinaryPrimitives.WriteInt32LittleEndian(exe[(0x900 + (48 + 35 * 3) * 4 + index * 4)..], 30 + index);
        return image;
    }

    private static int Record(Span<byte> record, string name, int lba, int length)
    {
        byte[] identifier = Encoding.ASCII.GetBytes(name);
        int size = 33 + identifier.Length + (identifier.Length % 2 == 0 ? 1 : 0);
        record[0] = checked((byte)size);
        BinaryPrimitives.WriteInt32LittleEndian(record[2..], lba);
        BinaryPrimitives.WriteInt32BigEndian(record[6..], lba);
        BinaryPrimitives.WriteInt32LittleEndian(record[10..], length);
        BinaryPrimitives.WriteInt32BigEndian(record[14..], length);
        record[28] = record[31] = 1;
        record[32] = checked((byte)identifier.Length);
        identifier.CopyTo(record[33..]);
        return size;
    }

    private static object? Invoke(MainWindow window, string name, params object?[] args)
    {
        try { return typeof(MainWindow).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, args); }
        catch (TargetInvocationException exception) when (exception.InnerException != null)
        { ExceptionDispatchInfo.Capture(exception.InnerException).Throw(); throw; }
    }

    private static FieldInfo Field(string name) => typeof(MainWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static void Complete(Task task)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(20);
        while (!task.IsCompleted && DateTime.UtcNow < deadline) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(1); }
        Require(task.IsCompleted, "Published diagnostics operation timed out.");
        task.GetAwaiter().GetResult();
    }
}
