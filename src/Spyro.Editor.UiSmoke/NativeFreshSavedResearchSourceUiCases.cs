using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using F = NativeFreshMaterialInspectionUiFixture;
using Fixture = NativeFreshSavedResearchLifecycleCases.Fixture;

// PRIVATE explicit actual publication proposal. Never called by default/source-only.
// Root must select the existing private fixture parent and absent native output.
internal static class NativeFreshSavedResearchSourceUiCases
{
    private const string Image = "2533ee961f13e43db03b08f221921cb60e35465e662e51bf221e6c096eb75b70";
    // Observed whole-source family reference, not an independent image oracle.
    private const string FamilyImage = "408dfdfc577f991e635e4c0b636344333ca4b2654d89afabd88af88cc1a55c98";
    internal static void Run(string bin, string cue, string fixtureParent, string output) =>
        RunCore(bin, cue, fixtureParent, output, family: false);
    internal static void RunFamily(string bin, string cue, string fixtureParent, string output) =>
        RunCore(bin, cue, fixtureParent, output, family: true);

    private static void RunCore(string bin, string cue, string fixtureParent, string output, bool family)
    {
        string expectedImage = family ? FamilyImage : Image;
        int expectedSectors = family ? 1292 : 1253;
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
            fixture = new Fixture(fixtureParent, family ? "actual-family408d" : "actual-joint2533", output,
                document: family ? SavedFamilyDocumentFixture.Document() : null,
                selectedEditorId: family ? SavedFamilyDocumentFixture.GrassId : null);
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
            Check(result != null && result.WholeImageReadback && result.ImageSha256 == expectedImage && result.ChangedRawSectors == expectedSectors &&
                result.OutputDirectory == output && result.RetainedRecoveryDirectory.Length == 0 && !result.RuntimeVerified,
                family ? "Actual returned publication matches the source-measured family reference without runtime promotion." :
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
            if (family)
            {
                byte[] savedCanonical = NativeFreshLevelDocumentCodec.SerializeCanonical(savedSnapshot.Intent!);
                using var canonical = JsonDocument.Parse(savedCanonical);
                using var receipt = JsonDocument.Parse(File.ReadAllBytes(result!.ReceiptPath));
                var root = receipt.RootElement;
                var saved = root.GetProperty("saved");
                var authored = saved.GetProperty("canonical");
                var rows = authored.GetProperty("placements");
                Check(root.GetProperty("variant").GetString() == "schema3-two-retail-materials-wizard-grass-p2-n32" &&
                    root.GetProperty("savedRevisionBound").GetBoolean() && authored.GetProperty("schema").GetInt32() == 3 &&
                    saved.GetProperty("projectId").GetString() == savedSnapshot.ProjectId &&
                    saved.GetProperty("root").GetString() == savedSnapshot.RootPath &&
                    saved.GetProperty("path").GetString() == savedSnapshot.Path &&
                    saved.GetProperty("rawSha256").GetString() == savedSnapshot.ContentSha256 &&
                    saved.GetProperty("manifestSha256").GetString() == savedSnapshot.ManifestSha256 &&
                    saved.GetProperty("canonicalSha256").GetString() == Convert.ToHexStringLower(SHA256.HashData(savedCanonical)) &&
                    JsonElement.DeepEquals(authored, canonical.RootElement),
                    "Physical family receipt binds the exact complete schema3 returned Save Snapshot and actual project revision.");
                Check(rows.GetArrayLength() == 2 && rows[0].GetProperty("id").GetString() == SavedFamilyDocumentFixture.WizardId &&
                    rows[0].GetProperty("tag").GetString() == "wizard-route" &&
                    rows[1].GetProperty("id").GetString() == SavedFamilyDocumentFixture.GrassId &&
                    rows[1].GetProperty("tag").GetString() == "native-moby" &&
                    savedCanonical.AsSpan().SequenceEqual(NativeFreshLevelDocumentCodec.SerializeCanonical(SavedFamilyDocumentFixture.Document())),
                    "Physical receipt preserves exact authored Wizard then Grass IDs, source tuple, transforms, materials and floor.");
                var resolved = root.GetProperty("lineage").GetProperty("families");
                Check(resolved.GetArrayLength() == 2 && Enumerable.Range(0, 2).All(i =>
                    resolved[i].GetProperty("EditorId").GetString() == rows[i].GetProperty("id").GetString() &&
                    JsonElement.DeepEquals(resolved[i].GetProperty("authoredPlacement"), rows[i])) &&
                    !root.GetProperty("runtimeVerified").GetBoolean() && !root.GetProperty("normalCreateBinApproved").GetBoolean() &&
                    !root.GetProperty("completeNativeDependencyClosureProven").GetBoolean() &&
                    !root.GetProperty("nativePersistentSaveOwnerProven").GetBoolean(),
                    "Resolved family receipt rows retain authored order without normal export, general closure, runtime or durable-save promotion.");
            }
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
        string marker = family ? "FRESH_SAVED_FAMILY_RESEARCH_SOURCE_UI_OK" : "FRESH_SAVED_RESEARCH_SOURCE_UI_OK";
        Console.WriteLine($"{marker} checks={checks} originalHeldNamedFinalAndClosed=true nativeOutputWritten=true actualPublication=true image={expectedImage} output={output}");
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
