using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;

internal static class StagingCases
{
    internal static async Task<int> RunAsync(string image, string workspace, string directory)
    {
        int checks = 0;
        string root = Path.GetFullPath(directory);
        if (Directory.Exists(root) || File.Exists(root))
            throw new IOException("Staging regression requires a new exact directory.");
        Directory.CreateDirectory(root);
        string cue = Path.Combine(root, "source-test.cue");
        byte[] cueBytes = DiscCueSheet.TextEncoding.GetBytes($"FILE \"{Path.GetFullPath(image)}\" BINARY\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\n");
        File.WriteAllBytes(cue, cueBytes); // Private source binding; original source CUE is never edited.
        var plan = await NativeFreeFlightResearchCandidatePlan.CaptureAsync(image, cue, LevelCatalog.Load(workspace));
        foreach (var fault in new[] { NativeFreeFlightResearchFault.AfterRelocation, NativeFreeFlightResearchFault.AfterExecutable,
            NativeFreeFlightResearchFault.BeforePublication })
        {
            string output = Path.Combine(root, fault.ToString());
            await RejectAsync(() => NativeFreeFlightResearchCandidatePublisher.PublishForSmokeAsync(plan, output, fault,
                (point, imagePath) =>
                {
                    if (fault != NativeFreeFlightResearchFault.BeforePublication || point != fault) return;
                    string stage = Path.GetDirectoryName(imagePath)!;
                    string analysis = Path.Combine(stage, "source-wad-analysis.json");
                    byte[] analysisBytes = File.ReadAllBytes(analysis);
                    File.Delete(analysis);
                    try
                    {
                        // Reuse is tested against the complete private staged set,
                        // never by publishing a second candidate or modifying an old one.
                        var reuse = NativeFreeFlightResearchCandidatePublisher.PublishAsync(plan, stage).GetAwaiter().GetResult();
                        if (reuse.PlanSha256 != plan.PlanSha256 || reuse.RuntimeVerified)
                            throw new InvalidDataException("Research staged reuse changed identity or authority.");
                        checks++;
                        foreach (string name in new[] { NativeFreeFlightResearchCandidatePlan.CueName,
                            NativeFreeFlightResearchCandidatePlan.ReceiptName })
                        {
                            string path = Path.Combine(stage, name);
                            byte[] before = File.ReadAllBytes(path);
                            byte[] changed = before.ToArray();
                            changed[^1] ^= 1;
                            File.WriteAllBytes(path, changed);
                            try
                            {
                                RejectAsync(() => NativeFreeFlightResearchCandidatePublisher.PublishAsync(plan, stage),
                                    "companion binding").GetAwaiter().GetResult();
                            }
                            finally { File.WriteAllBytes(path, before); }
                        }
                    }
                    finally { File.WriteAllBytes(analysis, analysisBytes); }
                }),
                "Deliberate research publication fault: " + fault);
            RequireEmpty(output);
            Console.WriteLine($"NATIVE_FREE_FLIGHT_RESEARCH_ROLLBACK_OK stage={fault}");
        }
        string corruptOutput = Path.Combine(root, "corrupt-sector");
        await RejectAsync(() => NativeFreeFlightResearchCandidatePublisher.PublishForSmokeAsync(plan, corruptOutput,
            NativeFreeFlightResearchFault.None, (point, path) =>
            {
                if (point != NativeFreeFlightResearchFault.AfterExecutable) return;
                using FileStream output = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                output.Position = 30L * 2352 + 24; // Completely unrelated source-owned sector.
                int old = output.ReadByte();
                output.Position--;
                output.WriteByte((byte)(old ^ 1));
            }), "physical readback differs at raw sector 30");
        RequireEmpty(corruptOutput);
        string cueOutput = Path.Combine(root, "late-cue");
        try
        {
            await RejectAsync(() => NativeFreeFlightResearchCandidatePublisher.PublishForSmokeAsync(plan, cueOutput,
                NativeFreeFlightResearchFault.None, (point, _) =>
                {
                    if (point == NativeFreeFlightResearchFault.BeforePublication)
                        File.AppendAllText(cue, "    PREGAP 00:01:00\n", DiscCueSheet.TextEncoding);
                }), "source CUE changed");
        }
        finally { File.WriteAllBytes(cue, cueBytes); }
        RequireEmpty(cueOutput);
        await plan.RequireFreshAsync();
        checks++;
        string occupied = Path.Combine(root, "occupied");
        Directory.CreateDirectory(occupied);
        File.WriteAllText(Path.Combine(occupied, "keep.txt"), "user fixture");
        await RejectAsync(() => NativeFreeFlightResearchCandidatePublisher.PublishAsync(plan, occupied), "partial, occupied");
        if (File.ReadAllText(Path.Combine(occupied, "keep.txt")) != "user fixture") throw new InvalidDataException("Occupied data changed.");
        checks++;
        string directoryArtifact = Path.Combine(root, "directory-artifact");
        MakePartialSet(directoryArtifact);
        File.Delete(Path.Combine(directoryArtifact, NativeFreeFlightResearchCandidatePlan.ImageName));
        Directory.CreateDirectory(Path.Combine(directoryArtifact, NativeFreeFlightResearchCandidatePlan.ImageName));
        await RejectAsync(() => NativeFreeFlightResearchCandidatePublisher.PublishAsync(plan, directoryArtifact), "artifact is not a file");
        string linkArtifact = Path.Combine(root, "symbolic-artifact");
        MakePartialSet(linkArtifact);
        File.Delete(Path.Combine(linkArtifact, NativeFreeFlightResearchCandidatePlan.ImageName));
        File.CreateSymbolicLink(Path.Combine(linkArtifact, NativeFreeFlightResearchCandidatePlan.ImageName), Path.GetFullPath(image));
        await RejectAsync(() => NativeFreeFlightResearchCandidatePublisher.PublishAsync(plan, linkArtifact), "symbolic link");
        await RejectAsync(() => NativeFreeFlightResearchCandidatePublisher.PublishAsync(plan, Path.GetDirectoryName(Path.GetFullPath(image))!),
            "overlaps source BIN");
        await RejectAsync(() => NativeFreeFlightResearchCandidatePublisher.PublishAsync(plan, Path.GetFullPath(image)),
            "overlaps source BIN");
        Console.WriteLine($"NATIVE_FREE_FLIGHT_RESEARCH_STAGING_OK checks={checks} noCandidatePublished=true");
        return checks;

        void RequireEmpty(string path)
        {
            if (!Directory.Exists(path) || Directory.EnumerateFileSystemEntries(path).Any())
                throw new InvalidDataException("Failed research stage retained unexpected artifacts: " + path);
            checks++;
        }
        async Task RejectAsync(Func<Task<NativeFreeFlightResearchPublication>> action, string expected)
        {
            try { await action(); }
            catch (Exception error) when (error is IOException or InvalidDataException or InvalidOperationException)
            {
                if (!error.Message.Contains(expected, StringComparison.Ordinal))
                    throw new InvalidDataException("Research negative failed before its intended assertion: " + expected, error);
                checks++;
                return;
            }
            throw new InvalidDataException("Research negative unexpectedly published.");
        }
        void MakePartialSet(string path)
        {
            Directory.CreateDirectory(path);
            foreach (string name in new[] { NativeFreeFlightResearchCandidatePlan.ImageName,
                NativeFreeFlightResearchCandidatePlan.CueName, NativeFreeFlightResearchCandidatePlan.ReceiptName })
                File.WriteAllBytes(Path.Combine(path, name), []);
        }
    }
}
