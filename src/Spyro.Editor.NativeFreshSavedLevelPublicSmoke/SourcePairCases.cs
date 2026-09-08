using System.Security.Cryptography;
using static PublicCases;

// Tiny-file controls. These invalid bytes are NOT originals,
// source-issued objects, native outputs or notified-success substitutes.
internal static class SourcePairCases
{
    internal static void Free()
    {
        string root = Path.Combine(Root, "partial-source-owners"); Directory.CreateDirectory(root);
        string bin = Path.Combine(root, "tiny.bin"), cue = Path.Combine(root, "tiny.cue"), moved = bin + ".retained";
        byte[] heldBytes = [0x31, 0x52, 0x73], namedBytes = [0x24, 0x46, 0x68, 0x80, 0xA2], cueBytes = [0x5A, 0x32];
        File.WriteAllBytes(bin, heldBytes); File.WriteAllBytes(cue, cueBytes);
        var owners = new SourcePair(bin, cue);
        Exception? openFailure = null, testFailure = null, outcome = null;
        try
        {
            try { owners.Open(); } catch (Exception error) { openFailure = error; }
            Check(openFailure != null, "Tiny invalid original must fail after partial BIN acquisition.");
            // The initial wrong length must still consume actual held bytes and
            // produce their complete digest failure, before any metadata drift.
            Check(Digests(openFailure!, heldBytes) == 1, "Initial length refusal still computed the actual full tiny BIN digest.");
            Check(Leaves(openFailure!).Select(e => e.Message).SequenceEqual(new[] {
                "Initial original length.", DigestMessage(heldBytes, BinHash),
                "Consumed original position.", "Final original position.", "Final original length."
            }), "Initial length/hash/EOF observations have the exact five failing leaves; EOF itself passed.");
            File.Move(bin, moved); // Retain actual acquired file; do not overwrite/remove it.
            File.WriteAllBytes(bin, namedBytes); // A distinct later name/length/digest.
        }
        catch (Exception error) { testFailure = error; }
        finally
        {
            // Protect actual acquired owners immediately, even when an assertion
            // or the test-only rename/create fails. Never finalize twice.
            List<Exception> primary = [];
            if (openFailure != null) primary.Add(openFailure);
            if (testFailure != null) primary.Add(testFailure);
            Exception? combined = null;
            try { ThrowFailures(primary); } catch (Exception error) { combined = error; }
            try { owners.Complete(combined); } catch (Exception error) { outcome = error; }
        }
        if (testFailure != null) ThrowFailures([outcome ?? testFailure]);
        Check(openFailure != null && outcome != null, "Actual primary/final refusal remains visible after all closes.");
        Check(All(outcome!).Count(e => ReferenceEquals(e, openFailure)) == 1, "Initial primary object preserved exactly once.");
        var leaves = Leaves(outcome!);
        Check(leaves.Count == 24, "Exactly initial5 + held7 + named7 + current-CUE5 leaves; no hidden unexpected assertion.");
        Check(Digests(outcome!, heldBytes) == 2, "Initial and held-final full tiny digest errors both present despite later metadata rejection.");
        Check(Digests(outcome!, namedBytes) == 1 && Digests(outcome!, cueBytes) == 1,
            "Current BIN and unacquired current CUE were independently fully hashed, not merely notified.");
        Check(leaves.Count(e => e.Message == "Source metadata before full read changed.") == 2 &&
            leaves.Count(e => e.Message == "Source metadata after full read changed.") == 2,
            "Both held/current BIN paths independently reject old metadata before and after byte reads.");
        Check(!leaves.Any(e => e.Message == "Actual original EOF."), "Actual EOF reads succeeded for every complete tiny stream.");
        CheckClosed(moved, heldBytes); CheckClosed(bin, namedBytes); CheckClosed(cue, cueBytes);
        Check(Directory.GetFiles(root).Select(Path.GetFileName).Order().SequenceEqual(new[] { "tiny.bin", "tiny.bin.retained", "tiny.cue" }),
            "Only all three retained tiny owners; no deletion, native source or output.");
    }
    private const string BinHash = "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37";
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static string DigestMessage(byte[] bytes, string expected) => "Complete original SHA actual=" + Hash(bytes) + " expected=" + expected;
    private static int Digests(Exception error, byte[] bytes) => Leaves(error)
        .Count(e => e.Message.StartsWith("Complete original SHA actual=" + Hash(bytes) + " expected=", StringComparison.Ordinal));
    private static IEnumerable<Exception> All(Exception error)
    {
        yield return error;
        if (error is AggregateException aggregate)
            foreach (var child in aggregate.InnerExceptions) foreach (var entry in All(child)) yield return entry;
        else if (error.InnerException != null) foreach (var entry in All(error.InnerException)) yield return entry;
    }
    private static void CheckClosed(string path, byte[] bytes)
    {
        FileStream? file = null; List<Exception> errors = [];
        try
        {
            file = new(path, FileMode.Open, FileAccess.Read, FileShare.None);
            byte[] read = new byte[bytes.Length]; file.ReadExactly(read);
            Check(read.SequenceEqual(bytes) && file.Position == bytes.Length && file.ReadByte() == -1 && file.Length == bytes.Length,
                "Exclusive reopened actual tiny owner has exact bytes/length/EOF after finalization.");
        }
        catch (Exception error) { errors.Add(error); }
        finally { if (file != null) Attempt(file.Dispose, errors); }
        ThrowFailures(errors);
    }
}
