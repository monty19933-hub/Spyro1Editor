using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using static Cases;

internal static class BoundaryCases
{
    internal static async Task Run(string path)
    {
        foreach (string mode in new[] { "primary", "cancel", "before-disc-cancel", "after-disc-primary",
            "raw-final", "manifest-final", "writer-final", "combined" })
        {
            var (project, saved) = Save("boundary-" + mode);
            var primary = new IOException("actual stage callback failure, not an injected issuer result: " + mode);
            using var cancel = new CancellationTokenSource();
            List<string> observed = [];
            NativeFreshLevelDocumentStore.ReadLease? heldGuard = null;
            NativeFreshSavedMaterialCompilation? result = null;
            Exception? actual = null;
            try
            {
                result = await NativeFreshSavedMaterialCompilation.CaptureForSmokeAsync(path, saved, phase =>
                {
                    observed.Add(phase);
                    if (phase == "material0-captured" && mode == "primary") throw primary;
                    if (phase == "material0-captured" && mode == "cancel") cancel.Cancel();
                    if (phase == "body-captured" && mode == "before-disc-cancel") cancel.Cancel();
                    if (phase == "disc-captured" && mode == "after-disc-primary") throw primary;
                    if (phase == "before-final-snapshot" && mode is "raw-final" or "combined") File.AppendAllText(saved.Snapshot.Path, " ");
                    if (phase == "before-final-snapshot" && mode == "manifest-final") File.AppendAllText(project.ManifestPath, " ");
                    if (phase == "before-final-snapshot" && mode == "writer-final")
                        heldGuard = saved.AcquireFinalReadGuard();
                    if (phase == "material0-captured" && mode == "combined") throw primary;
                }, cancel.Token);
            }
            catch (Exception error) { actual = error; }
            finally
            {
                try
                {
                    if (mode == "writer-final")
                    {
                        Check(actual == null && result != null && ReferenceEquals(result.Saved, saved),
                            "Another real read guard permits detached compilation/raw-version observation, not publication.");
                        Check(heldGuard != null && new FileInfo(heldGuard.LeasePath) is { Length: 32, LinkTarget: null },
                            "Bridge leaves the other actual regular32 read guard present; its FileShare.None bytes are not externally read.");
                        await Throws<IOException>(() =>
                        { NativeFreshLevelDocumentStore.Save(saved.Snapshot, saved.Document); return Task.CompletedTask; });
                        Check(new FileInfo(heldGuard!.LeasePath) is { Length: 32, LinkTarget: null },
                            "Actual cooperating Save refuses while the other regular32 read guard remains present.");
                    }
                }
                finally { heldGuard?.Complete(); }
            }
            if (mode == "writer-final") Check(heldGuard!.TerminalFailure == null && heldGuard.RetainedEvidencePath == "" &&
                !File.Exists(heldGuard.LeasePath), "Fixture Complete performed exact owner-token verification and normal release.");
            if (mode != "writer-final") Check(actual != null, "Every explicit failure mode refuses rather than returning an issued bridge.");
            Check(observed.TakeLast(3).SequenceEqual(new[] { "before-final-source", "before-final-snapshot", "before-close" }),
                "Actual source and saved final observations and closing occur after primary/cancel/snapshot failure.");
            if (mode is "primary" or "after-disc-primary") Check(ReferenceEquals(actual, primary), "Exact sole callback exception is rethrown without wrapping.");
            if (mode is "cancel" or "before-disc-cancel") Check(actual is OperationCanceledException oce && oce.CancellationToken == cancel.Token,
                "Canceled native capture remains the same caller cancellation after noncancellable successful final checks.");
            if (mode == "before-disc-cancel") Check(observed.Contains("body-captured") && !observed.Contains("disc-captured"),
                "Cancellation after actual Body is observed before attempting Disc issuance.");
            if (mode == "after-disc-primary") Check(observed.Contains("disc-captured"),
                "Failure after actual Disc return still refuses the new outer bridge and performs final accounting.");
            if (mode == "combined")
            {
                var leaves = Leaves(actual!);
                Check(actual is IOException && leaves.Count == 2 && ReferenceEquals(leaves[0], primary) && leaves[1] is InvalidDataException,
                    "Primary and actual final raw-version failure remain ordered/catchable, without inferred saved adoption.");
            }
            Check(!File.Exists(Path.Combine(project.RootPath, LockName)) &&
                !Directory.EnumerateFiles(project.RootPath, "*.bin").Any() && !Directory.EnumerateFiles(project.RootPath, "*.cue").Any(),
                "No bridge-created lease, BIN/CUE or publication tree.");
            using var final = File.OpenRead(path); RequireOriginal(final);
        }
    }
}
