using System.Security.Cryptography;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using F = NativeFreshMaterialInspectionUiFixture;
using Fixture = NativeFreshSavedResearchLifecycleCases.Fixture;

// PRIVATE explicit actual publication proposal. Never called by default/source-only.
// Root must select the existing private fixture parent and absent native output.
internal static class NativeFreshSavedResearchSourceUiCases
{
    private const string Image = "2533ee961f13e43db03b08f221921cb60e35465e662e51bf221e6c096eb75b70";
    internal static void Run(string bin, string cue, string fixtureParent, string output)
    {
        foreach (string path in new[] { bin, cue, fixtureParent, output }) RequireAbsoluteUnlinked(path);
        if (!Directory.Exists(fixtureParent) || !Directory.Exists(Path.GetDirectoryName(output)) ||
            Directory.Exists(output) || File.Exists(output) || new FileInfo(output).LinkTarget != null ||
            output == bin || output == cue || Path.GetFullPath(output).StartsWith(Path.GetFullPath(fixtureParent) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidDataException("Reviewed output must be an absent sibling, separate from original files and the fixture tree.");
        List<Exception> failures = [];
        var sources = new HeldPair(bin, cue);
        Fixture? fixture = null;
        Task? operation = null;
        NativeId65FreshDiscResearchPublication? result = null;
        int checks = 0;
        try
        {
            sources.Open();
            fixture = new Fixture(fixtureParent, "actual-joint2533", output);
            fixture.Source = (bin, cue);
            fixture.Window.ResearchOutputPickerForTesting = _ => Task.FromResult<string?>(output);
            var applied = fixture.Window.CurrentAggregateIntent!;
            var openedSnapshot = fixture.CurrentSession.Snapshot;
            fixture.Fire("FreshDocumentSave"); // The real unchanged Save→Export boundary.
            var savedSnapshot = fixture.CurrentSession.Snapshot;
            Check(!ReferenceEquals(openedSnapshot, savedSnapshot) && ReferenceEquals(applied, fixture.Window.CurrentAggregateIntent) &&
                savedSnapshot.Intent != null && !ReferenceEquals(savedSnapshot.Intent, applied) &&
                NativeFreshLevelDocumentCodec.SerializeCanonical(applied).AsSpan()
                    .SequenceEqual(NativeFreshLevelDocumentCodec.SerializeCanonical(savedSnapshot.Intent)),
                "Actual Save retains applied object but returns a distinct parsed Snapshot.Intent with equal canonical bytes.");
            var releaseError = new IOException("actual-ui-owner-release-after-real-publication-simulation");
            fixture.OnRelease = () =>
            {
                fixture.Fire("FreshDocumentCancelResearch"); // Too late to erase actual success.
                fixture.Current = false; // Advisory after actual returned publication.
            };
            fixture.ReleaseFailure = releaseError;
            // No fake work: Window's real public Candidate/Publisher path runs.
            fixture.Fire("FreshDocumentResearch");
            operation = fixture.Window.ResearchTaskForTesting ?? throw new InvalidDataException("Missing actual publication task.");
            F.Drain(operation);
            result = fixture.Window.ResearchPublicationForTesting;
            Check(ReferenceEquals(savedSnapshot, fixture.Window.SavedResearchSnapshotForTesting),
                "Actual export retains the returned Save Snapshot, not an equal applied object or later recapture.");
            Check(result != null && result.WholeImageReadback && result.ImageSha256 == Image && result.ChangedRawSectors == 1253 &&
                result.OutputDirectory == output && result.RetainedRecoveryDirectory.Length == 0 && !result.RuntimeVerified,
                "Actual returned publication preserves the independent joint golden without runtime promotion.");
            Check(ReferenceEquals(applied, fixture.Window.CurrentAggregateIntent) && fixture.Released == 1 && !fixture.Window.ResearchBusyForTesting,
                "Actual owner settled and applied object was not replaced or implicitly saved.");
            Check(ReferenceEquals(releaseError, fixture.Window.SavedResearchFailureForTesting) &&
                fixture.Status.Contains("byte-verified research candidate", StringComparison.Ordinal) &&
                fixture.Status.Contains(result!.CuePath, StringComparison.Ordinal) && fixture.Status.Contains(result.ReceiptPath, StringComparison.Ordinal) &&
                fixture.Status.Contains("captured version", StringComparison.Ordinal) && fixture.Status.Contains(releaseError.Message, StringComparison.Ordinal),
                "Real result, literal paths and verified classification survive advisory plus actual UI release failure.");
            Check(Directory.EnumerateFileSystemEntries(output).Select(Path.GetFileName).Order().SequenceEqual(new[]
                { "native-fresh-saved-level-r01.bin", "native-fresh-saved-level-r01.cue", "native-fresh-saved-level-r01.receipt.json" }.Order()),
                "Exactly the three real normal-publisher artifacts; no stage/recovery adoption.");
        }
        catch (Exception error) { failures.Add(error); }
        finally
        {
            Attempt(() => { if (operation != null) F.Drain(operation); });
            NativeFreshSavedResearchLifecycleCases.RetainWindowFailure(fixture, failures);
            Attempt(() => fixture?.Close());
            Attempt(() => fixture?.RequireProtected(expectOutputAbsent: false));
            // Always retain physical outputs, including unknown or failed returns.
            sources.Complete(failures);
            NativeFreshSavedResearchLifecycleCases.RetainWindowFailure(fixture, failures);
        }
        F.Throw(failures);
        Console.WriteLine($"FRESH_SAVED_RESEARCH_SOURCE_UI_OK checks={checks} originalHeldNamedFinalAndClosed=true nativeOutputWritten=true actualPublication=true image={Image} output={output}");
        void Check(bool condition, string message) { if (!condition) throw new InvalidDataException(message); checks++; }
        void Attempt(Action action) { try { action(); } catch (Exception error) { failures.Add(error); } }
    }

    private static void RequireAbsoluteUnlinked(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Contains('\0') || !Path.IsPathFullyQualified(path))
            throw new ArgumentException("Explicit absolute paths are required.");
        for (string? current = Path.GetFullPath(path); current != null; current = Path.GetDirectoryName(current))
            if (new FileInfo(current).LinkTarget != null || new DirectoryInfo(current).LinkTarget != null)
                throw new InvalidDataException("Reviewed paths and existing ancestors must be unlinked.");
    }

    // Concrete test-owned source guard only. Independent metadata/byte/EOF attempts,
    // current named recaptures and all acquired closes; no injected authority.
    private sealed class HeldPair(string bin, string cue)
    {
        private readonly string[] paths = [bin, cue];
        private readonly long[] lengths = [661547040, 88];
        private readonly string[] hashes = ["fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37", "05142502c75d3a0b6374003a8fe41893d6faf0ad55863ff0b62a265a1b152766"];
        private readonly FileStream?[] held = new FileStream?[2];
        private readonly string?[] metadata = new string?[2];
        internal void Open()
        {
            for (int i = 0; i < 2; i++)
            {
                held[i] = new FileStream(paths[i], FileMode.Open, FileAccess.Read, FileShare.Read);
                metadata[i] = Metadata(paths[i]);
                List<Exception> errors = []; Verify(i, held[i]!, errors); F.Throw(errors);
            }
        }
        internal void Complete(List<Exception> failures)
        {
            for (int i = 0; i < 2; i++)
            {
                if (held[i] != null) Verify(i, held[i]!, failures);
                FileStream? current = null;
                try { current = new(paths[i], FileMode.Open, FileAccess.Read, FileShare.Read); Verify(i, current, failures); }
                catch (Exception error) { failures.Add(error); }
                finally { Attempt(() => current?.Dispose(), failures); }
            }
            foreach (var file in held) Attempt(() => file?.Dispose(), failures);
        }
        private void Verify(int i, Stream stream, List<Exception> failures)
        {
            string? before = null, digest = null;
            Attempt(() => { before = Metadata(paths[i]); Require(metadata[i] == null || before == metadata[i], "Original metadata before changed."); }, failures);
            Attempt(() => Require(stream.Length == lengths[i], "Original initial length changed."), failures);
            Attempt(() => stream.Position = 0, failures);
            Attempt(() => digest = Convert.ToHexStringLower(SHA256.HashData(stream)), failures);
            if (digest != null) Attempt(() => Require(digest == hashes[i], "Complete original SHA changed: " + digest), failures);
            Attempt(() => Require(stream.Position == lengths[i], "Consumed complete original position changed."), failures);
            Attempt(() => Require(stream.ReadByte() == -1, "Original true EOF changed."), failures);
            Attempt(() => Require(stream.Position == lengths[i], "Original final position changed."), failures);
            Attempt(() => Require(stream.Length == lengths[i], "Original final length changed."), failures);
            Attempt(() => { string after = Metadata(paths[i]); Require((metadata[i] == null || after == metadata[i]) && (before == null || after == before), "Original metadata after changed."); }, failures);
        }
        private static string Metadata(string path)
        {
            var file = new FileInfo(path); Require(file.Exists && file.LinkTarget == null && !Directory.Exists(path), "Original regular nonlink required.");
            return $"{file.FullName}|{file.Length}|{file.CreationTimeUtc.Ticks}|{file.LastWriteTimeUtc.Ticks}";
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
        private static void Attempt(Action action, List<Exception> failures) { try { action(); } catch (Exception error) { failures.Add(error); } }
    }
}
