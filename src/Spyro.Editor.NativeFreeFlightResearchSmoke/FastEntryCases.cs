using System.Text.Json;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;

internal static class FastEntryCases
{
    internal static async Task<(NativeFreeFlightResearchCandidatePlan Plan, int Checks)> RunAsync(string image, string cue, LevelCatalog catalog,
        NativeFreeFlightResearchCandidatePlan original)
    {
        int checks = 0;
        var diagnostic = await NativeFreeFlightResearchCandidatePlan.CaptureWithTestLevelSelectorAsync(image, cue, catalog);
        Check(!original.TestLevelSelectorEnabled && diagnostic.TestLevelSelectorEnabled);
        Check(original.ProfileId == NativeFreeFlightResearchCandidatePlan.Profile);
        Check(diagnostic.ProfileId == "native-toasty-free-flight-fast-entry-research-v2");
        Check(original.OutputImageName != diagnostic.OutputImageName && original.OutputCueName != diagnostic.OutputCueName);
        Check(original.AnimationMerge.PlanSha256 == diagnostic.AnimationMerge.PlanSha256);
        Check(original.ExecutablePatch.PlanSha256 == diagnostic.ExecutablePatch.PlanSha256);
        Check(original.PlanSha256 != diagnostic.PlanSha256);
        Check(original.Entries.SequenceEqual(diagnostic.Entries));
        byte[] before = original.CopyExecutable(), after = diagnostic.CopyExecutable();
        byte[] expected = before.ToArray();
        Check(before.AsSpan(0x1E034, 8).SequenceEqual(Convert.FromHexString("37B6000800000000")));
        Convert.FromHexString("1C0684AF880680AF").CopyTo(expected, 0x1E034);
        Check(after.AsSpan().SequenceEqual(expected));
        byte[] source = original.ExecutablePatch.CopySourceExecutable();
        Check(diagnostic.ApplyToExecutable(source).AsSpan().SequenceEqual(after));
        Check(source.AsSpan().SequenceEqual(original.ExecutablePatch.CopySourceExecutable()));
        Reject(() => diagnostic.ApplyToExecutable(before)); // Flight-already-patched input is not original authority.
        Reject(() => diagnostic.ApplyToExecutable(after));
        foreach (int offset in new[] { 0, 0x1E034, 0x1E03C, 0x31000, source.Length - 1 })
        {
            byte[] stale = source.ToArray();
            stale[offset] ^= 1;
            Reject(() => diagnostic.ApplyToExecutable(stale));
        }
        using var receipt = JsonDocument.Parse(diagnostic.BuildReceipt((new string('0', 64), 0)));
        Check(receipt.RootElement.GetProperty("profile").GetString() == diagnostic.ProfileId);
        Check(receipt.RootElement.GetProperty("testLevelSelector").GetProperty("Kind").GetString() == TestLevelWarpPatch.PatchKind);
        Check(!receipt.RootElement.GetProperty("normalPortalEntryVerified").GetBoolean());
        Check(DiscCueSheet.TextEncoding.GetString(diagnostic.CopyCueBytes()).Contains(
            "FILE \"toasty-native-free-flight-fast-entry-r02.bin\" BINARY", StringComparison.Ordinal));
        Check(!receipt.RootElement.GetProperty("RuntimeVerified").GetBoolean());
        Check(!receipt.RootElement.GetProperty("NormalCreateBinApproved").GetBoolean());
        using var oldReceipt = JsonDocument.Parse(original.BuildReceipt((new string('0', 64), 0)));
        Check(!oldReceipt.RootElement.TryGetProperty("testLevelSelector", out _));
        string scratch = Directory.CreateTempSubdirectory("spyro-flight-profile-identity-").FullName;
        try
        {
            foreach (var (present, requested) in new[] { (original, diagnostic), (diagnostic, original) })
            {
                string directory = Directory.CreateDirectory(Path.Combine(scratch, present.ProfileId)).FullName;
                foreach (string name in present.OutputNames) File.WriteAllBytes(Path.Combine(directory, name), [1, 2, 3]);
                Reject(() => NativeFreeFlightResearchCandidatePublisher.PublishAsync(requested, directory).GetAwaiter().GetResult());
                Check(Directory.GetFiles(directory).Select(Path.GetFileName).Order().SequenceEqual(present.OutputNames.Order()));
                Check(Directory.GetFiles(directory).All(p => File.ReadAllBytes(p).SequenceEqual(new byte[] { 1, 2, 3 })));
            }
        }
        finally { Directory.Delete(scratch, recursive: true); } // This invocation's tiny synthetic fixtures only.
        await diagnostic.RequireFreshAsync();
        Console.WriteLine($"NATIVE_FREE_FLIGHT_FAST_ENTRY_COMPOSITION_OK checks={checks} plan={diagnostic.PlanSha256}");
        return (diagnostic, checks);

        void Check(bool ok)
        {
            if (!ok) throw new InvalidDataException("Fast-entry research composition assertion failed.");
            checks++;
        }
        void Reject(Action action)
        {
            try { action(); }
            catch (Exception error) when (error is IOException or InvalidDataException or InvalidOperationException or ArgumentException)
            { checks++; return; }
            throw new InvalidDataException("Fast-entry research accepted a stale/non-original executable.");
        }
    }
}
