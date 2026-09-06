using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

// Explicitly invoked publication/verification exercise, never a default/source
// regression mode. All corruptions affect a new private copy, not the candidate.
internal static class SuccessCases
{
    internal static async Task<int> Run(NativeId65FreshDiscResearchCandidatePlan plan, string workspace,
        Func<Task<NativeId65FreshDiscResearchCandidatePlan>> recapture)
    {
        string root = Path.GetFullPath(workspace);
        PersistencePathSafety.EnsureTreesDoNotOverlap(plan.SourceImagePath, root, "Exercise overlaps source BIN.");
        PersistencePathSafety.EnsureTreesDoNotOverlap(plan.SourceCuePath, root, "Exercise overlaps source CUE.");
        if (Directory.Exists(root) || File.Exists(root) || new FileInfo(root).LinkTarget != null || new DirectoryInfo(root).LinkTarget != null)
            throw new IOException("Publication exercise requires an absent, non-linked workspace.");
        Directory.CreateDirectory(root);
        int checks = 0;
        string candidate = Path.Combine(root, "candidate");
        var published = await NativeId65FreshDiscResearchCandidatePublisher.PublishAsync(plan, candidate);
        Check(published.ImageSha256 == ResearchCases.ExpectedHash && published.ChangedRawSectors == 1253 &&
              published.RetainedRecoveryDirectory == "" && !published.RuntimeVerified && !published.NormalCreateBinApproved,
              "new-only publication returns the exact physically verified research candidate");
        Check(Directory.EnumerateFileSystemEntries(candidate).Count() == 3, "successful cleanup leaves exactly three artifacts");
        // Recapture the complete typed source chain, not just an old receipt.
        var fresh = await recapture();
        var reopened = await NativeId65FreshDiscResearchCandidatePublisher.VerifyAsync(fresh, candidate);
        Check(reopened == published, "independently recaptured plan reopens exactly the same final artifact set");
        await Reject(() => NativeId65FreshDiscResearchCandidatePublisher.PublishAsync(fresh, candidate), "occupied");
        Check(await NativeId65FreshDiscResearchCandidatePublisher.VerifyAsync(fresh, candidate) == reopened,
              "duplicate publication refuses without changing retained candidate");

        string missing = Path.Combine(root, "never-created");
        await Reject(() => NativeId65FreshDiscResearchCandidatePublisher.VerifyAsync(fresh, missing), "missing");
        Check(!Directory.Exists(missing), "read-only verify never creates its missing target");
        using (var cancelled = new CancellationTokenSource())
        {
            cancelled.Cancel();
            bool refused = false;
            try { await NativeId65FreshDiscResearchCandidatePublisher.VerifyAsync(fresh, candidate, cancelled.Token); }
            catch (OperationCanceledException) { refused = true; }
            Check(refused, "cancelled verification does not return a success receipt");
        }

        string damaged = Path.Combine(root, "private-verification-fixture");
        Directory.CreateDirectory(damaged);
        foreach (string name in NativeId65FreshDiscResearchCandidatePlan.OutputNames)
            File.Copy(Path.Combine(candidate, name), Path.Combine(damaged, name), overwrite: false);
        string image = Path.Combine(damaged, NativeId65FreshDiscResearchCandidatePlan.ImageName);
        string cue = Path.Combine(damaged, NativeId65FreshDiscResearchCandidatePlan.CueName);
        string receipt = Path.Combine(damaged, NativeId65FreshDiscResearchCandidatePlan.ReceiptName);

        // Missing and unexpected members are refused without modifying them.
        string heldReceipt = Path.Combine(root, "temporarily-held-receipt.json");
        File.Move(receipt, heldReceipt);
        try { await Reject(() => NativeId65FreshDiscResearchCandidatePublisher.VerifyAsync(fresh, damaged), "incomplete"); }
        finally { File.Move(heldReceipt, receipt); }
        string keep = Path.Combine(damaged, "private-test-note.txt");
        File.WriteAllText(keep, "preserve this exact test-owned note");
        await Reject(() => NativeId65FreshDiscResearchCandidatePublisher.VerifyAsync(fresh, damaged), "occupied");
        Check(File.ReadAllText(keep) == "preserve this exact test-owned note", "verification preserves unexpected files");
        File.Delete(keep); // Only the exact test-owned note created above.

        foreach (string path in new[] { cue, receipt })
        {
            byte[] original = File.ReadAllBytes(path);
            try
            {
                byte[] wrong = original.ToArray(); wrong[0] ^= 1;
                File.WriteAllBytes(path, wrong);
                await Reject(() => NativeId65FreshDiscResearchCandidatePublisher.VerifyAsync(fresh, damaged), "companion");
                Check(File.ReadAllBytes(path).SequenceEqual(wrong), "verification does not repair changed companion bytes");
            }
            finally { File.WriteAllBytes(path, original); }
        }
        byte[] cueBytes = File.ReadAllBytes(cue);
        File.Delete(cue); // Only the new fixture CUE, never the retained candidate.
        try
        {
            File.CreateSymbolicLink(cue, published.CuePath);
            await Reject(() => NativeId65FreshDiscResearchCandidatePublisher.VerifyAsync(fresh, damaged), "symbolic");
            Check(new FileInfo(cue).LinkTarget != null, "verification does not replace a linked companion");
        }
        finally
        {
            if (new FileInfo(cue).LinkTarget != null) File.Delete(cue);
            File.WriteAllBytes(cue, cueBytes);
        }

        // LBA38 is unchanged MODE2 Form1; +2076 is actual P parity.
        // LBA30 is Form2, so +2072 there would only test payload corruption.
        const long untouchedParityOffset = 38L * 2352 + 2076;
        byte parity;
        using (FileStream stream = new(image, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            stream.Position = untouchedParityOffset;
            int before = stream.ReadByte();
            if (before < 0) throw new EndOfStreamException();
            parity = (byte)before;
            stream.Position = untouchedParityOffset; stream.WriteByte((byte)(parity ^ 1)); stream.Flush(true);
        }
        try { await Reject(() => NativeId65FreshDiscResearchCandidatePublisher.VerifyAsync(fresh, damaged), "physical readback"); }
        finally
        {
            using FileStream stream = new(image, FileMode.Open, FileAccess.Write, FileShare.None);
            stream.Position = untouchedParityOffset; stream.WriteByte(parity); stream.Flush(true);
        }
        Check((await NativeId65FreshDiscResearchCandidatePublisher.VerifyAsync(fresh, damaged)).ImageSha256 == ResearchCases.ExpectedHash,
              "restored private test copy is exact again");
        Check(await NativeId65FreshDiscResearchCandidatePublisher.VerifyAsync(fresh, candidate) == reopened,
              "final original-recaptured candidate verification survives all separate-copy negatives");
        Console.WriteLine($"NATIVE_ID65_FRESH_DISC_RESEARCH_PUBLICATION_OK checks={checks} candidate={candidate} runtimeVerified=false");
        return checks;

        void Check(bool value, string label) { if (!value) throw new InvalidDataException(label); checks++; }
        async Task Reject(Func<Task<NativeId65FreshDiscResearchPublication>> action, string fragment)
        {
            try { await action(); }
            catch (Exception error) when (error is IOException or InvalidDataException or InvalidOperationException)
            {
                if (!error.Message.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                    throw new Exception("Wrong verification refusal, expected " + fragment + ": " + error.Message, error);
                checks++; return;
            }
            throw new InvalidDataException("Unexpected verification acceptance: " + fragment);
        }
    }
}
