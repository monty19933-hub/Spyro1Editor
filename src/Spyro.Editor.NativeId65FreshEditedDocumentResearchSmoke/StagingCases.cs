using Spyro.Editor.Core.Exporting;

// Explicit bounded output-producing fault exercise. Every invocation has a
// forced precommit fault even when it additionally attacks another boundary;
// an unexpectedly missing guard must NEVER turn this mode into publication.
internal static class StagingCases
{
    internal static async Task<int> Run(string original, string workspace, NativeId65FreshDocumentCompilation issued)
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
        var plan = await NativeId65FreshEditedDocumentResearchPlan.CaptureAsync(original, cue, issued);
        foreach (var fault in new[] { NativeId65FreshEditedDocumentResearchFault.AfterFirstSector,
            NativeId65FreshEditedDocumentResearchFault.AfterImageWrite, NativeId65FreshEditedDocumentResearchFault.BeforePublication })
        {
            string output = Path.Combine(root, fault.ToString());
            bool reached = false;
            await Reject(() => NativeId65FreshEditedDocumentResearchPublisher.PublishForSmokeAsync(plan, output, fault, (point, image) =>
            {
                if (point != fault) return;
                reached = true;
                if (point != NativeId65FreshEditedDocumentResearchFault.BeforePublication) return;
                string stage = Path.GetDirectoryName(image)!;
                var verified = NativeId65FreshEditedDocumentResearchPublisher.VerifyAsync(plan, stage).GetAwaiter().GetResult();
                Check(verified.ImageSha256 == EditedCases.ImageHash && verified.ChangedRawSectors == 1253 &&
                    !verified.RuntimeVerified && !verified.NormalCreateBinApproved,
                    "freshly reopened complete stage verifies without publication authority");
                Reject(() => NativeId65FreshEditedDocumentResearchPublisher.PublishForSmokeAsync(plan, stage,
                    NativeId65FreshEditedDocumentResearchFault.BeforePublication), "occupied")
                    .GetAwaiter().GetResult();
            }), "Deliberate");
            Check(reached, "actual requested writer checkpoint reached");
            Empty(output);
        }

        string corrupt = Path.Combine(root, "corrupt-untouched-sector");
        await Reject(() => NativeId65FreshEditedDocumentResearchPublisher.PublishForSmokeAsync(plan, corrupt,
            NativeId65FreshEditedDocumentResearchFault.BeforePublication, (point, image) =>
            {
                if (point != NativeId65FreshEditedDocumentResearchFault.AfterImageWrite) return;
                using FileStream stream = new(image, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                stream.Position = 30L * 2352 + 2072;
                int before = stream.ReadByte(); stream.Position--;
                stream.WriteByte((byte)(before ^ 1)); stream.Flush(true);
            }), "physical readback");
        Empty(corrupt);

        foreach (string name in new[] { NativeId65FreshEditedDocumentResearchPlan.CueName,
            NativeId65FreshEditedDocumentResearchPlan.ReceiptName })
        {
            string output = Path.Combine(root, "late-companion-" + Path.GetExtension(name));
            await Reject(() => NativeId65FreshEditedDocumentResearchPublisher.PublishForSmokeAsync(plan, output,
                NativeId65FreshEditedDocumentResearchFault.BeforePublication, (point, image) =>
                {
                    if (point != NativeId65FreshEditedDocumentResearchFault.BeforePublication) return;
                    string companion = Path.Combine(Path.GetDirectoryName(image)!, name);
                    byte[] bytes = File.ReadAllBytes(companion);
                    bytes[0] ^= 1;
                    File.WriteAllBytes(companion, bytes);
                }), "companion exact bytes");
            Empty(output);
        }

        string recoveryOutput = Path.Combine(root, "retained-unknown-stage-owner");
        string? retainedStage = null;
        await Reject(() => NativeId65FreshEditedDocumentResearchPublisher.PublishForSmokeAsync(plan, recoveryOutput,
            NativeId65FreshEditedDocumentResearchFault.AfterFirstSector, (point, image) =>
            {
                if (point != NativeId65FreshEditedDocumentResearchFault.AfterFirstSector) return;
                retainedStage = Path.GetDirectoryName(image)!;
                File.WriteAllText(Path.Combine(retainedStage, "foreign.txt"), "retain exact foreign evidence");
            }), "Deliberate");
        Check(retainedStage != null &&
            File.ReadAllText(Path.Combine(retainedStage, "foreign.txt")) == "retain exact foreign evidence" &&
            File.Exists(Path.Combine(recoveryOutput, ".native-id65-fresh-edited.writer.lock")) &&
            !File.Exists(Path.Combine(retainedStage, "native-id65-fresh-edited-landing-track02-r01.bin")),
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
                await Reject(() => NativeId65FreshEditedDocumentResearchPublisher.PublishForSmokeAsync(plan, output,
                    NativeId65FreshEditedDocumentResearchFault.BeforePublication, (point, _) =>
                    {
                        if (point != NativeId65FreshEditedDocumentResearchFault.BeforePublication) return;
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

        foreach (var point in new[] { NativeId65FreshEditedDocumentResearchFault.AfterFirstSector, NativeId65FreshEditedDocumentResearchFault.BeforePublication })
        {
            string output = Path.Combine(root, "cancel-" + point);
            using var token = new CancellationTokenSource();
            bool cancelled = false;
            try
            {
                await NativeId65FreshEditedDocumentResearchPublisher.PublishForSmokeAsync(plan, output,
                    NativeId65FreshEditedDocumentResearchFault.BeforePublication,
                    (at, _) => { if (at == point) token.Cancel(); }, token.Token);
            }
            catch (OperationCanceledException) { cancelled = true; }
            Check(cancelled, "early/late publication cancellation is not success or deliberate fault");
            Empty(output);
        }

        string occupied = Path.Combine(root, "occupied"); Directory.CreateDirectory(occupied);
        string keep = Path.Combine(occupied, "keep.txt"); File.WriteAllText(keep, "preserve user bytes");
        await Reject(() => NativeId65FreshEditedDocumentResearchPublisher.PublishForSmokeAsync(plan, occupied,
            NativeId65FreshEditedDocumentResearchFault.BeforePublication), "occupied");
        Check(File.ReadAllText(keep) == "preserve user bytes", "occupied directory is unchanged");
        string linked = Path.Combine(root, "linked"); Directory.CreateSymbolicLink(linked, occupied);
        try
        {
            await Reject(() => NativeId65FreshEditedDocumentResearchPublisher.PublishForSmokeAsync(plan, linked,
                NativeId65FreshEditedDocumentResearchFault.BeforePublication), "symbolic");
        }
        finally { File.Delete(linked); }
        await Reject(() => NativeId65FreshEditedDocumentResearchPublisher.PublishForSmokeAsync(plan, original,
            NativeId65FreshEditedDocumentResearchFault.BeforePublication), "overlap");
        await Reject(() => NativeId65FreshEditedDocumentResearchPublisher.PublishForSmokeAsync(plan, root,
            NativeId65FreshEditedDocumentResearchFault.BeforePublication), "overlap");
        string locked = Path.Combine(root, "pre-existing-lock");
        Directory.CreateDirectory(locked);
        string oldLock = Path.Combine(locked, ".export-publication.lock");
        File.WriteAllText(oldLock, "foreign publisher lock");
        await Reject(() => NativeId65FreshEditedDocumentResearchPublisher.PublishForSmokeAsync(plan, locked,
            NativeId65FreshEditedDocumentResearchFault.BeforePublication), "occupied");
        Check(File.ReadAllText(oldLock) == "foreign publisher lock" &&
            Directory.GetFileSystemEntries(locked).Length == 1, "pre-existing transaction lock is untouched");

        string artifactDirectory = Path.Combine(root, "artifact-directory");
        Directory.CreateDirectory(Path.Combine(artifactDirectory, NativeId65FreshEditedDocumentResearchPlan.ImageName));
        await Reject(() => NativeId65FreshEditedDocumentResearchPublisher.PublishForSmokeAsync(plan, artifactDirectory,
            NativeId65FreshEditedDocumentResearchFault.BeforePublication), "occupied");
        Check(Directory.Exists(Path.Combine(artifactDirectory, NativeId65FreshEditedDocumentResearchPlan.ImageName)),
            "directory occupying final artifact is preserved");
        string finalLinkRoot = Path.Combine(root, "final-link");
        Directory.CreateDirectory(finalLinkRoot);
        string finalLink = Path.Combine(finalLinkRoot, NativeId65FreshEditedDocumentResearchPlan.ImageName);
        File.CreateSymbolicLink(finalLink, keep);
        try
        {
            await Reject(() => NativeId65FreshEditedDocumentResearchPublisher.PublishForSmokeAsync(plan, finalLinkRoot,
                NativeId65FreshEditedDocumentResearchFault.BeforePublication), "occupied");
            Check(File.ReadAllText(keep) == "preserve user bytes" && new FileInfo(finalLink).LinkTarget != null,
                "final symlink and its target survive refusal");
        }
        finally { File.Delete(finalLink); }
        await plan.RequireFreshAsync();
        Console.WriteLine($"NATIVE_ID65_FRESH_EDITED_DOCUMENT_RESEARCH_STAGING_OK checks={checks} published=false");
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
