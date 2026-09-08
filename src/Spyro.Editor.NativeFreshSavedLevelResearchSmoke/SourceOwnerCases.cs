// Source-free accounting using actual tiny file owners, not native proof.
using System.Reflection;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using static Cases;

internal static class SourceOwnerCases
{
    internal static async Task Free()
    {
        // Resolve and validate the test inspection BEFORE any owner acquisition.
        var complete = typeof(NativeFreshSavedLevelResearchCandidatePlan).GetMethod("CompleteOperation",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        Check(complete != null && complete.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] {
            typeof(NativeFreshResearchSourceOwners), typeof(NativeFreshSavedLevelDocumentReadContext), typeof(Exception) }),
            "Exact private completion method, no forged Candidate construction.");
        foreach (string mode in new[] { "early-missing-cue", "both-tiny", "partial-exclusive-cue" })
        {
            var (project, saved) = Save("concrete-" + mode);
            string bin = Path.Combine(project.RootPath, "source.bin"), cue = Path.Combine(project.RootPath, "source.cue");
            File.WriteAllBytes(bin, [1, 2, 3, 4]);
            if (mode != "early-missing-cue")
                File.WriteAllText(cue, "FILE \"source.bin\" BINARY\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\n");
            var owners = new NativeFreshResearchSourceOwners(bin, cue);
            Exception? primary = null, setupFailure = null, outcome = null, testFailure = null;
            FileStream? occupiedCue = null;
            try
            {
                if (mode == "partial-exclusive-cue")
                    occupiedCue = new FileStream(cue, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                try
                {
                    try { owners.Open(CancellationToken.None); }
                    catch (Exception error) { primary = error; }
                    if (mode == "early-missing-cue")
                        Check(owners.Bin == null && owners.Cue == null && primary is InvalidDataException,
                            "Missing CUE identity is an early refusal, not partial acquisition.");
                    else if (mode == "partial-exclusive-cue")
                        Check(owners.Bin != null && owners.Cue == null && primary is IOException,
                            "Real exclusive CUE refuses after actual BIN acquisition.");
                    else
                    {
                        Check(owners.Bin != null && owners.Cue != null && primary == null,
                            "Two actual held tiny owners; no native source admitted.");
                        primary = new IOException("synthetic operation error; actual file finalization follows");
                    }
                    File.AppendAllText(saved.Snapshot.Path, " ");
                }
                catch (Exception error) { setupFailure = error; }
                finally
                {
                    Exception? operation = setupFailure == null ? primary : primary == null ? setupFailure :
                        new IOException("Initial source and unexpected test setup failure.", new AggregateException(primary, setupFailure));
                    try { complete!.Invoke(null, [owners, saved, operation]); }
                    catch (TargetInvocationException error) { outcome = error.InnerException!; }
                    catch (Exception reflectionFailure)
                    {
                        // Binding/access failures occur before method execution;
                        // close concrete owners even if the test inspection broke.
                        Exception failedOperation = operation == null ? reflectionFailure : new IOException(
                            "Operation plus test reflection failure.", new AggregateException(operation, reflectionFailure));
                        try { owners.Complete(failedOperation); }
                        catch (Exception completedFailure) { outcome = completedFailure; }
                        setupFailure = reflectionFailure;
                    }
                }
                if (setupFailure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(outcome!).Throw();
                var leaves = Leaves(outcome!);
                Check(leaves.Count(e => ReferenceEquals(e, primary)) == 1 && ReferenceEquals(leaves[0], primary),
                    "Primary occurs once before independent final checks.");
                if (mode == "both-tiny")
                    Check(leaves.Count == 4 && leaves.Skip(1).All(e => e is InvalidDataException) &&
                        !ReferenceEquals(leaves[1], leaves[2]), "Held/current BIN and final raw version fail independently.");
                else if (mode == "early-missing-cue")
                    Check(leaves.Count == 4 && leaves[1] is InvalidDataException && leaves[2] is FileNotFoundException &&
                        leaves[3] is InvalidDataException, "Both current names and raw version run without held owners.");
                else
                    Check(leaves.Count == 5 && leaves[1] is InvalidDataException && leaves[2] is InvalidDataException &&
                        leaves[3] is IOException && leaves[4] is InvalidDataException,
                        "Real primary, held/current BIN, unacquirable current CUE, then final raw drift.");
                using var reopened = new FileStream(bin, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                Check(reopened.Length == 4, "Actually acquired BIN closed before failure inspection.");
                if (occupiedCue != null)
                    Check(occupiedCue.Length > 0 && occupiedCue.ReadByte() == (byte)'F', "Foreign held CUE is not closed or changed.");
            }
            catch (Exception error) { testFailure = error; }
            finally
            {
                // Even a failed completion/assertion never leaves the test blocker.
                try { occupiedCue?.Dispose(); }
                catch (Exception close)
                {
                    testFailure = testFailure == null ? close : new IOException(
                        "Source fixture plus blocker close failure.", new AggregateException(testFailure, close));
                }
            }
            if (testFailure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(testFailure).Throw();
            if (mode != "early-missing-cue")
            { using var released = new FileStream(cue, FileMode.Open, FileAccess.ReadWrite, FileShare.None); Check(released.Length > 0, "CUE owners closed."); }
        }
        await Task.CompletedTask;
    }
}
