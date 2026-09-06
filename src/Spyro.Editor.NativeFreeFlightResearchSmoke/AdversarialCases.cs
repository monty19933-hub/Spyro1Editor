using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;

internal static class AdversarialCases
{
    internal static int Run()
    {
        int checks = 0;
        byte[] source = Enumerable.Range(0, 2352 * 3).Select(i => (byte)(i * 37)).ToArray();
        string hash = Convert.ToHexStringLower(SHA256.HashData(source));
        var result = NativeTownSquareClosurePhysicalReadback.Verify(new MemoryStream(source), new MemoryStream(source),
            source.Length, hash, (_, before) => before);
        Check(result.Hash == hash && result.Changed == 0);
        foreach (int sector in Enumerable.Range(0, 3))
        foreach (int offset in new[] { 0, 16, 18, 22, 24, 1000, 2071, 2072, 2351 })
        {
            byte[] changed = source.ToArray();
            changed[sector * 2352 + offset] ^= 1;
            Reject(() => NativeTownSquareClosurePhysicalReadback.Verify(new MemoryStream(source), new MemoryStream(changed),
                source.Length, hash, (_, before) => before));
        }
        Reject(() => NativeTownSquareClosurePhysicalReadback.Verify(new MemoryStream(source), new MemoryStream(source[..^1]),
            source.Length, hash, (_, before) => before));
        Reject(() => NativeTownSquareClosurePhysicalReadback.Verify(new MemoryStream(source), new MemoryStream(source),
            source.Length, new string('0', 64), (_, before) => before));
        Reject(() => NativeTownSquareClosurePhysicalReadback.Verify(new MemoryStream(source), new MemoryStream(source),
            source.Length, hash, (_, before) => before[..^1]));

        string root = Directory.CreateTempSubdirectory("spyro-free-flight-cue-").FullName;
        string image = Path.Combine(root, "source.bin"), cue = Path.Combine(root, "source.cue");
        byte[] original = DiscCueSheet.TextEncoding.GetBytes("FILE \"source.bin\" BINARY\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\n");
        try
        {
            File.WriteAllBytes(image, [1, 2, 3]); // CUE binding test, never a native plan.
            File.WriteAllBytes(cue, original);
            var snapshot = NativeFreeFlightResearchSourceCue.Capture(cue, image, "output.bin");
            snapshot.RequireFresh();
            Check(snapshot.SourceSha256 == Convert.ToHexStringLower(SHA256.HashData(original)));
            byte[] copy = snapshot.CopyOutputBytes();
            copy[0] ^= 1;
            Check(!copy.SequenceEqual(snapshot.CopyOutputBytes()));
            foreach (string changed in new[]
            {
                DiscCueSheet.TextEncoding.GetString(original) + "    PREGAP 00:01:00\n",
                DiscCueSheet.TextEncoding.GetString(original).Replace("source.bin", "./source.bin"),
                DiscCueSheet.TextEncoding.GetString(original).Replace("MODE2/2352", "MODE1/2048")
            })
            {
                File.WriteAllText(cue, changed, DiscCueSheet.TextEncoding);
                Reject(snapshot.RequireFresh);
                File.WriteAllBytes(cue, original);
                snapshot.RequireFresh();
                checks++;
            }
            File.Delete(cue);
            Reject(snapshot.RequireFresh);
            File.WriteAllBytes(cue, original);
            string replacement = Path.Combine(root, "other.cue");
            File.WriteAllBytes(replacement, original);
            File.Delete(cue);
            File.CreateSymbolicLink(cue, replacement);
            Reject(snapshot.RequireFresh);
            File.Delete(cue);
            File.WriteAllBytes(cue, original);
            snapshot.RequireFresh();
            checks++;
            foreach (string malformed in new[] { "", "FILE \"source.bin\" BINARY",
                DiscCueSheet.TextEncoding.GetString(original) + "FILE \"source.bin\" BINARY\n",
                DiscCueSheet.TextEncoding.GetString(original).Replace("00:00:00", "00:02:00") })
            {
                File.WriteAllText(cue, malformed, DiscCueSheet.TextEncoding);
                Reject(() => NativeFreeFlightResearchSourceCue.Capture(cue, image, "output.bin"));
            }
            File.WriteAllBytes(cue, original);
            Reject(() => NativeFreeFlightResearchSourceCue.Capture(cue, image, "../unsafe.bin"));
        }
        finally
        {
            Directory.Delete(root, recursive: true); // Only this invocation's synthetic temporary fixture.
        }
        return checks;

        void Check(bool value)
        {
            if (!value) throw new InvalidDataException("Research adversarial assertion failed.");
            checks++;
        }
        void Reject(Action action)
        {
            try { action(); }
            catch (Exception error) when (error is IOException or InvalidDataException or ArgumentException)
            {
                checks++;
                return;
            }
            throw new InvalidDataException("Research adversarial fixture was accepted.");
        }
    }
}
