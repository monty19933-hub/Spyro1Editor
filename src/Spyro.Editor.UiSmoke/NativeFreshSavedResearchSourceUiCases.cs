using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using F = NativeFreshMaterialInspectionUiFixture;
using Fixture = NativeFreshSavedResearchLifecycleCases.Fixture;

// PRIVATE explicit actual publication proposal. Never called by default/source-only.
// Root must select the existing private fixture parent and absent native output.
internal static partial class NativeFreshSavedResearchSourceUiCases
{
    private const string Image = "2533ee961f13e43db03b08f221921cb60e35465e662e51bf221e6c096eb75b70";
    // Observed whole-source family reference, not an independent image oracle.
    private const string FamilyImage = "408dfdfc577f991e635e4c0b636344333ca4b2654d89afabd88af88cc1a55c98";
    // Retained R03 native image observed at runtime; the saved UI path still
    // requires its own actual publication and does not inherit runtime approval.
    private const string LockedChestImage = "a5b00725388f73e7e6f7b9ed356c4a0d0f55b1c869d6a65f38e97ef2641d6c50";
    private enum PublicationProfile { Material, WizardGrass, LockedChest }
    internal static void Run(string bin, string cue, string fixtureParent, string output) =>
        RunCore(bin, cue, fixtureParent, output, PublicationProfile.Material);
    internal static void RunFamily(string bin, string cue, string fixtureParent, string output) =>
        RunCore(bin, cue, fixtureParent, output, PublicationProfile.WizardGrass);
    internal static void RunLockedChest(string bin, string cue, string fixtureParent, string output) =>
        RunCore(bin, cue, fixtureParent, output, PublicationProfile.LockedChest);

    private static void RunCore(string bin, string cue, string fixtureParent, string output, PublicationProfile profile)
    {
        bool family = profile == PublicationProfile.WizardGrass, lockedChest = profile == PublicationProfile.LockedChest;
        string expectedImage = profile switch
        {
            PublicationProfile.Material => Image, PublicationProfile.WizardGrass => FamilyImage,
            PublicationProfile.LockedChest => LockedChestImage, _ => throw new ArgumentOutOfRangeException(nameof(profile))
        };
        int expectedSectors = lockedChest ? 1297 : family ? 1292 : 1253;
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
            fixture = new Fixture(fixtureParent, lockedChest ? "actual-locked-chest-a5b0" : family ? "actual-family408d" : "actual-joint2533", output,
                document: lockedChest ? NativeFreshFamilyDocumentSessionCases.Document() : family ? SavedFamilyDocumentFixture.Document() : null,
                selectedEditorId: lockedChest ? NativeFreshFamilyDocumentSessionCases.FamilyId : family ? SavedFamilyDocumentFixture.GrassId : null);
            fixture.Source = (bin, cue);
            fixture.Window.ResearchOutputPickerForTesting = _ => Task.FromResult<string?>(output);
            var applied = fixture.Window.CurrentAggregateIntent!;
            if (lockedChest)
                Check(applied.SchemaVersion == 5 && applied.Placements.Count == 3 &&
                    NativeFreshLevelDocumentCodec.SerializeCanonical(applied).AsSpan().SequenceEqual(
                        NativeFreshLevelDocumentCodec.SerializeCanonical(NativeFreshFamilyDocumentSessionCases.Document())) &&
                    fixture.CurrentSession.SelectedEditorId == NativeFreshFamilyDocumentSessionCases.FamilyId &&
                    fixture.Window.SavedResearchWorkForTesting == null,
                    "The actual UI owns the complete independently authored R03 schema5 fixture and real worker, not a schema3 projection or injected publisher.");
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
                lockedChest ? "Actual returned publication matches retained R03 native bytes; saved UI/runtime approval remains separate." :
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
            if (lockedChest) RequireLockedChestReceipt(savedSnapshot, result!, Check);
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
        string marker = lockedChest ? "FRESH_SAVED_LOCKED_CHEST_RESEARCH_SOURCE_UI_OK" :
            family ? "FRESH_SAVED_FAMILY_RESEARCH_SOURCE_UI_OK" : "FRESH_SAVED_RESEARCH_SOURCE_UI_OK";
        Console.WriteLine($"{marker} checks={checks} originalHeldNamedFinalAndClosed=true nativeOutputWritten=true actualPublication=true image={expectedImage} output={output}");
        void Check(bool condition, string message) { if (!condition) throw new InvalidDataException(message); checks++; }
        void Attempt(Action action) { try { action(); } catch (Exception error) { failures.Add(error); } }
    }

    private static void RequireLockedChestReceipt(NativeFreshLevelDocumentSnapshot snapshot,
        NativeId65FreshDiscResearchPublication publication, Action<bool, string> check)
    {
        var expected = NativeFreshFamilyDocumentSessionCases.Document();
        byte[] canonicalBytes = NativeFreshLevelDocumentCodec.SerializeCanonical(expected);
        using var canonical = JsonDocument.Parse(canonicalBytes);
        RequireAbsoluteUnlinked(publication.ReceiptPath);
        byte[] receiptBytes;
        using (var input = new FileStream(publication.ReceiptPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            if (input.Length is < 1 or > 65536) throw new InvalidDataException("Complete physical chest receipt exceeds its bounded owner.");
            receiptBytes = new byte[checked((int)input.Length)]; input.ReadExactly(receiptBytes);
            check(input.Position == receiptBytes.Length && input.ReadByte() == -1 && input.Length == receiptBytes.Length,
                "Read the complete bounded physical receipt through actual EOF without truncation.");
        }
        using var receipt = JsonDocument.Parse(receiptBytes);
        var root = receipt.RootElement; var saved = root.GetProperty("saved"); var lineage = root.GetProperty("lineage");
        check(snapshot.Intent is { SchemaVersion: 5 } && NativeFreshLevelDocumentCodec.SerializeCanonical(snapshot.Intent)
                .AsSpan().SequenceEqual(canonicalBytes) &&
            root.GetProperty("variant").GetString() == "schema5-r03-wizard-grass-locked-chest-p9-n32" &&
            root.GetProperty("planSha256").GetString() == publication.PlanSha256 &&
            root.GetProperty("imageSha256").GetString() == LockedChestImage &&
            root.GetProperty("imageByteLength").GetInt64() == 661547040 && root.GetProperty("changedRawSectors").GetInt32() == 1297 &&
            root.GetProperty("savedRevisionBound").GetBoolean() && root.GetProperty("wholeImageInverseRequired").GetBoolean() &&
            saved.GetProperty("projectId").GetString() == snapshot.ProjectId && saved.GetProperty("root").GetString() == snapshot.RootPath &&
            saved.GetProperty("path").GetString() == snapshot.Path && saved.GetProperty("rawSha256").GetString() == snapshot.ContentSha256 &&
            saved.GetProperty("manifestSha256").GetString() == snapshot.ManifestSha256 &&
            saved.GetProperty("canonicalSha256").GetString() == Convert.ToHexStringLower(SHA256.HashData(canonicalBytes)) &&
            JsonElement.DeepEquals(saved.GetProperty("canonical"), canonical.RootElement),
            "Physical schema5 receipt binds the exact returned Save revision and complete literal R03 document, never the old schema3 route.");
        var authored = canonical.RootElement.GetProperty("placements"); var families = lineage.GetProperty("families");
        check(lineage.GetProperty("authoredPlacementCount").GetInt32() == 3 &&
            lineage.GetProperty("nativePlacedCount").GetInt32() == 9 && lineage.GetProperty("nativeNominalCapacity").GetInt32() == 32 &&
            families.GetArrayLength() == 3 && Enumerable.Range(0, 3).All(i =>
                families[i].GetProperty("EditorId").GetString() == expected.Placements[i].EditorId &&
                families[i].GetProperty("FirstRowIndex").GetInt32() == i && families[i].GetProperty("RowCount").GetInt32() == (i == 2 ? 7 : 1) &&
                JsonElement.DeepEquals(families[i].GetProperty("authoredPlacement"), authored[i])),
            "All three actual authored111/222/333 entries retain full contents/order and expand to1+1+7 native rows, not P2/P0.");
        int[] classes = [0x11B, 0x1F5, 0xAD, 0xAE, 0xD, 0xD, 0xD, 0xD, 0xD];
        var owners = lineage.GetProperty("nativeRowOwners"); var selectors = lineage.GetProperty("sourceSelectorRows");
        check(owners.GetArrayLength() == 9 && selectors.GetArrayLength() == 9 && Enumerable.Range(0, 9).All(i =>
            owners[i].GetProperty("nativeIndex").GetInt32() == i && owners[i].GetProperty("actorClass").GetInt32() == classes[i] &&
            selectors[i].GetProperty("PlacedOrdinal").GetInt32() == i && selectors[i].GetProperty("ActorClassId").GetInt32() == classes[i] &&
            owners[i].GetProperty("finalRowSha256").GetString() == selectors[i].GetProperty("SceneRowSha256").GetString() &&
            owners[i].GetProperty("sourceRowSha256").GetString() == selectors[i].GetProperty("SourceRowSha256").GetString()),
            "Physical receipt retains all nine final native classes and corresponding source/final row hashes.");
        check(JsonElement.DeepEquals(lineage.GetProperty("rewardDestinationOverrides"), JsonSerializer.SerializeToElement(
                ((NativeFreshLockedChestFamilyPlacementIntent)expected.Placements[2]).RewardDestinations!.Values)) &&
            lineage.GetProperty("KnownSelectorCount").GetInt32() == 7 &&
            lineage.GetProperty("KnownDeterministicSelectorSubtotal").GetInt32() == 20 &&
            lineage.GetProperty("ExperimentalTreasureTarget").GetInt32() == 20 &&
            lineage.GetProperty("sceneSha256").GetString() == "4f560dcbfa2d997a3f02554cf297d95c1778f67fc5d8fcf8ff47f64d609a89e8" &&
            lineage.GetProperty("bodySha256").GetString() == "2d8c1bf1af35c8e5522b239c564d36c79c828539d133278c461b6178b53bb504" &&
            lineage.GetProperty("executableSha256").GetString() == "81501f07ec9ce336089ec5a92f81bf51569aed723ac4f8ba1cf85ba0ef69a431",
            "The exact Reward2 WORLD override, seven known selectors and experimental20 target preserve retained R03 scene/body/EXE identities.");
        foreach (string flag in new[] { "runtimeVerified", "normalCreateBinApproved", "completeNativeDependencyClosureProven",
            "nativePersistentSaveOwnerProven", "publicationApproved", "keyZeroContributionProven", "completeStaticCensusProven", "runtimeDropGuarantee" })
            check(!root.GetProperty(flag).GetBoolean(), "No saved UI publication promotes pending native authority: " + flag);
        check(root.GetProperty("experimentalKeyZeroAssumption").GetBoolean() &&
            lineage.GetProperty("experimentalKeyZeroAssumption").GetBoolean() &&
            !lineage.GetProperty("footprintAndTrajectoryProven").GetBoolean(),
            "Key-zero remains an explicit experimental premise, not a complete census or trajectory certificate.");
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
