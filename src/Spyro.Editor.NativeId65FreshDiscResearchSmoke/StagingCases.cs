using Spyro.Editor.Core.Exporting;

// Explicit bounded output-producing fault exercise. Every invocation has a
// forced precommit fault even when it additionally attacks another boundary;
// an unexpectedly missing guard must NEVER turn this mode into publication.
internal static class StagingCases
{
    internal static async Task<int> Run(string original, string workspace, NativeId65FreshDiscPlan issued)
    {
        string root = Path.GetFullPath(workspace);
        if (Directory.Exists(root) || File.Exists(root) || new FileInfo(root).LinkTarget != null || new DirectoryInfo(root).LinkTarget != null)
            throw new IOException("Staging exercise requires an absent, non-linked exact workspace.");
        Directory.CreateDirectory(root);
        int checks = 0;
        string cue = Path.Combine(root, "private-source.cue");
        byte[] cueBytes = DiscCueSheet.TextEncoding.GetBytes(
            $"FILE \"{Path.GetFullPath(original)}\" BINARY\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\n");
        File.WriteAllBytes(cue, cueBytes); // This new tiny fixture is NOT the original CUE.
        var plan = await NativeId65FreshDiscResearchCandidatePlan.CaptureAsync(original, cue, issued);
        foreach (var fault in new[] { NativeId65FreshDiscResearchFault.AfterFirstSector,
            NativeId65FreshDiscResearchFault.AfterImageWrite, NativeId65FreshDiscResearchFault.BeforePublication })
        {
            string output = Path.Combine(root, fault.ToString());
            bool reached = false;
            await Reject(() => NativeId65FreshDiscResearchCandidatePublisher.PublishForSmokeAsync(plan, output, fault, (point, image) =>
            {
                if (point != fault) return;
                reached = true;
                if (point != NativeId65FreshDiscResearchFault.BeforePublication) return;
                string stage = Path.GetDirectoryName(image)!;
                var verified = NativeId65FreshDiscResearchCandidatePublisher.VerifyAsync(plan, stage).GetAwaiter().GetResult();
                Check(verified.ImageSha256 == ResearchCases.ExpectedHash && verified.ChangedRawSectors == 1253 &&
                    !verified.RuntimeVerified && !verified.NormalCreateBinApproved,
                    "freshly reopened complete stage verifies without publication authority");
                Reject(() => NativeId65FreshDiscResearchCandidatePublisher.PublishForSmokeAsync(plan, stage,
                    NativeId65FreshDiscResearchFault.BeforePublication), "occupied")
                    .GetAwaiter().GetResult();
            }), "Deliberate");
            Check(reached, "actual requested writer checkpoint reached");
            Empty(output);
        }

        string corrupt = Path.Combine(root, "corrupt-untouched-sector");
        await Reject(() => NativeId65FreshDiscResearchCandidatePublisher.PublishForSmokeAsync(plan, corrupt,
            NativeId65FreshDiscResearchFault.BeforePublication, (point, image) =>
            {
                if (point != NativeId65FreshDiscResearchFault.AfterImageWrite) return;
                using FileStream stream = new(image, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                stream.Position = 30L * 2352 + 2072;
                int before = stream.ReadByte(); stream.Position--;
                stream.WriteByte((byte)(before ^ 1)); stream.Flush(true);
            }), "physical readback");
        Empty(corrupt);

        string recoveryOutput = Path.Combine(root, "retained-unknown-stage-owner");
        string? retainedStage = null;
        await Reject(() => NativeId65FreshDiscResearchCandidatePublisher.PublishForSmokeAsync(plan, recoveryOutput,
            NativeId65FreshDiscResearchFault.AfterFirstSector, (point, image) =>
            {
                if (point != NativeId65FreshDiscResearchFault.AfterFirstSector) return;
                retainedStage = Path.GetDirectoryName(image)!;
                File.WriteAllText(Path.Combine(retainedStage, "foreign.txt"), "retain exact foreign evidence");
            }), "Deliberate");
        Check(retainedStage != null &&
            File.ReadAllText(Path.Combine(retainedStage, "foreign.txt")) == "retain exact foreign evidence" &&
            File.Exists(Path.Combine(recoveryOutput, ".native-id65-fresh.writer.lock")) &&
            !File.Exists(Path.Combine(retainedStage, "native-id65-fresh-lab-track19-r01.bin")),
            "unknown staged owner is preserved with lease while only owned partial image is removed");

        // BeforePublication callback precedes final source/readback checks;
        // its forced fault is raised ONLY AFTER those checks. A wrong guard
        // order raises Deliberate instead of source CUE and fails this fixture.
        foreach (string change in new[] { "pregap", "delete", "rebind" })
        {
            string output = Path.Combine(root, "late-cue-" + change);
            string replacement = Path.Combine(root, "replacement.cue");
            bool reached = false;
            try
            {
                await Reject(() => NativeId65FreshDiscResearchCandidatePublisher.PublishForSmokeAsync(plan, output,
                    NativeId65FreshDiscResearchFault.BeforePublication, (point, _) =>
                    {
                        if (point != NativeId65FreshDiscResearchFault.BeforePublication) return;
                        reached = true;
                        if (change == "pregap") File.WriteAllBytes(cue, cueBytes.Concat("PREGAP 00:01:00\n"u8.ToArray()).ToArray());
                        else
                        {
                            File.Delete(cue);
                            if (change == "rebind")
                            {
                                File.WriteAllBytes(replacement, cueBytes);
                                File.CreateSymbolicLink(cue, replacement);
                            }
                        }
                    }), "source CUE");
            }
            finally
            {
                if (new FileInfo(cue).LinkTarget != null) File.Delete(cue);
                File.WriteAllBytes(cue, cueBytes);
                if (File.Exists(replacement)) File.Delete(replacement);
            }
            Check(reached, "CUE attack occurs after real stage readback");
            Empty(output);
            await plan.RequireFreshAsync();
        }

        foreach (var point in new[] { NativeId65FreshDiscResearchFault.AfterFirstSector, NativeId65FreshDiscResearchFault.BeforePublication })
        {
            string output = Path.Combine(root, "cancel-" + point);
            using var token = new CancellationTokenSource();
            bool cancelled = false;
            try
            {
                await NativeId65FreshDiscResearchCandidatePublisher.PublishForSmokeAsync(plan, output,
                    NativeId65FreshDiscResearchFault.BeforePublication,
                    (at, _) => { if (at == point) token.Cancel(); }, token.Token);
            }
            catch (OperationCanceledException) { cancelled = true; }
            Check(cancelled, "early/late publication cancellation is not success or deliberate fault");
            Empty(output);
        }

        string occupied = Path.Combine(root, "occupied"); Directory.CreateDirectory(occupied);
        string keep = Path.Combine(occupied, "keep.txt"); File.WriteAllText(keep, "preserve user bytes");
        await Reject(() => NativeId65FreshDiscResearchCandidatePublisher.PublishForSmokeAsync(plan, occupied,
            NativeId65FreshDiscResearchFault.BeforePublication), "occupied");
        Check(File.ReadAllText(keep) == "preserve user bytes", "occupied directory is unchanged");
        string linked = Path.Combine(root, "linked"); Directory.CreateSymbolicLink(linked, occupied);
        try
        {
            await Reject(() => NativeId65FreshDiscResearchCandidatePublisher.PublishForSmokeAsync(plan, linked,
                NativeId65FreshDiscResearchFault.BeforePublication), "symbolic");
        }
        finally { File.Delete(linked); }
        await Reject(() => NativeId65FreshDiscResearchCandidatePublisher.PublishForSmokeAsync(plan, original,
            NativeId65FreshDiscResearchFault.BeforePublication), "overlap");
        await Reject(() => NativeId65FreshDiscResearchCandidatePublisher.PublishForSmokeAsync(plan, root,
            NativeId65FreshDiscResearchFault.BeforePublication), "overlap");
        await plan.RequireFreshAsync();
        Console.WriteLine($"NATIVE_ID65_FRESH_DISC_RESEARCH_STAGING_OK checks={checks} published=false");
        return checks;

        void Check(bool value, string label) { if (!value) throw new InvalidDataException(label); checks++; }
        void Empty(string output) => Check(!Directory.Exists(output) || !Directory.EnumerateFileSystemEntries(output).Any(),
            "failed stage retains no candidate/partial/lease when ordinary rollback succeeds");
        async Task Reject(Func<Task<NativeId65FreshDiscResearchPublication>> action, string fragment)
        {
            try { await action(); }
            catch (Exception error) when (error is IOException or InvalidDataException or InvalidOperationException)
            {
                if (!error.Message.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                    throw new Exception("Wrong refusal, expected " + fragment + ": " + error.Message, error);
                checks++; return;
            }
            throw new InvalidDataException("Unexpected acceptance: " + fragment);
        }
    }
}
