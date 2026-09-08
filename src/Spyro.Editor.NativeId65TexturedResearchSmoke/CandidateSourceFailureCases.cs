using Spyro.Editor.Core.Exporting;
using static Inspection;

internal static class CandidateSourceFailureCases
{
    internal static void Free()
    {
        Type type = typeof(NativeId65FreshTexturedResearchCandidatePlan);
        Call(null, type, "ThrowFailures", new List<Exception>());
        Check(true, "Neutral empty error fold has no outcome claim.");
        var primary = new InvalidOperationException("simulated operation primary");
        Check(ReferenceEquals(Refuse<InvalidOperationException>(() => Call(null, type, "ThrowFailures",
            new List<Exception> { primary }), "Exact single original error object."), primary), "Single fold preserves primary identity.");
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        var canceled = new OperationCanceledException(cancel.Token);
        Check(ReferenceEquals(Refuse<OperationCanceledException>(() => Call(null, type, "ThrowFailures",
            new List<Exception> { canceled }), "Exact single canceled error."), canceled), "Single cancellation identity/token preserved.");
        var errors = new Exception[] { primary, new IOException("simulated BIN final"), new InvalidDataException("simulated CUE final"),
            new IOException("simulated output close"), new IOException("simulated CUE close"), new IOException("simulated BIN close") };
        foreach (var set in new[] { errors, errors.Skip(1).ToArray(), new Exception[] { canceled, errors[1] } })
        {
            var actual = Refuse<IOException>(() => Call(null, type, "ThrowFailures", set.ToList()), "Catchable ordered neutral accounting.");
            Check(actual.InnerException is AggregateException aggregate && aggregate.InnerExceptions.Count == set.Length &&
                aggregate.InnerExceptions.Zip(set).All(pair => ReferenceEquals(pair.First, pair.Second)),
                "All exact simulated failure objects retained in original order; no fake commit/IO execution.");
        }
    }

    internal static async Task ActualInvalidOwners(NativeId65FreshDiscPlan disc)
    {
        string root = Directory.CreateTempSubdirectory("textured-candidate-invalid-sources-").FullName;
        string bin = Path.Combine(root, "source.bin"), cue = Path.Combine(root, "source.cue");
        byte[] binBytes = [1,2,3], cueBytes = [0xFF]; bool passed = false;
        try
        {
            File.WriteAllBytes(bin, binBytes); File.WriteAllBytes(cue, cueBytes);
            using (var occupiedCue = new FileStream(cue, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                var partial = await RefuseAsync<IOException>(() => NativeId65FreshTexturedResearchCandidatePlan.CaptureAsync(bin, cue, disc),
                    "Actual exclusive CUE refuses after the BIN was acquired; no injected IO factory.");
                Check(partial.InnerException is AggregateException failed && failed.InnerExceptions.Count >= 4 &&
                    failed.InnerExceptions[0] is IOException &&
                    failed.InnerExceptions.Count(e => e.Message.Contains("original", StringComparison.OrdinalIgnoreCase)) >= 2,
                    "Actual partial acquisition retains primary plus held/current BIN and unacquirable current CUE observations.");
                using var exclusiveBin = new FileStream(bin, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                Check(exclusiveBin.Length == binBytes.Length && occupiedCue.Length == cueBytes.Length &&
                    occupiedCue.ReadByte() == 0xFF, "Partial acquisition closes its BIN but never closes or changes the foreign CUE owner.");
            }
            var actual = await RefuseAsync<IOException>(() => NativeId65FreshTexturedResearchCandidatePlan.CaptureAsync(bin, cue, disc),
                "Actual tiny invalid BIN and malformed CUE remain independently observed after initial failure.");
            Check(actual.InnerException is AggregateException aggregate && aggregate.InnerExceptions.Count >= 5 &&
                aggregate.InnerExceptions.Any(e => e.Message.Contains("original", StringComparison.OrdinalIgnoreCase)) &&
                aggregate.InnerExceptions.Any(e => e.Message.Contains("CUE", StringComparison.OrdinalIgnoreCase)),
                "Primary plus held/current BIN and held/current CUE final failures all retained.");
            using (var exclusiveBin = new FileStream(bin, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            using (var exclusiveCue = new FileStream(cue, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                Check(exclusiveBin.Length == binBytes.Length && exclusiveCue.Length == cueBytes.Length,
                    "All acquired failed source handles are closed before returning failure.");
            Check(File.ReadAllBytes(bin).SequenceEqual(binBytes) && File.ReadAllBytes(cue).SequenceEqual(cueBytes), "Tiny original test owners not rewritten.");
            passed = true;
        }
        finally
        {
            if (passed && Directory.GetFileSystemEntries(root).Order().SequenceEqual(new[] { bin, cue }.Order()) &&
                new DirectoryInfo(root).LinkTarget == null && new FileInfo(bin).LinkTarget == null && new FileInfo(cue).LinkTarget == null &&
                File.ReadAllBytes(bin).SequenceEqual(binBytes) && File.ReadAllBytes(cue).SequenceEqual(cueBytes))
            { File.Delete(bin); File.Delete(cue); Directory.Delete(root); }
            else Console.Error.WriteLine("Preserve tiny invalid-source fixture: " + root);
        }
    }
}
