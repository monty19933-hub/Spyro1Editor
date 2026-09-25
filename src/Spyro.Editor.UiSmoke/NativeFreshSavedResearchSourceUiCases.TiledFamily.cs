using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Avalonia.Controls;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using F = NativeFreshMaterialInspectionUiFixture;

// Explicit reviewed physical mode only. No successful worker/Candidate/result
// is injected; the actual saved Window invokes the production saved publisher.
internal static partial class NativeFreshSavedResearchSourceUiCases
{
    private const string TiledFamilyImage = "689fcf296e26d635d9c5ce6fc3fc687c2b47decce10ade2a0903f0c6bd877192";
    private const string TiledFamilyVariant = "schema5-four1536-tile-r03-wizard-grass-locked-chest-p9-n32";

    internal static void RunTiledFamily(string bin, string cue, string fixtureParent, string output)
    {
        foreach (string path in new[] { bin, cue, fixtureParent, output }) RequireAbsoluteUnlinked(path);
        const string fixtureId = "saved-tiled-family-689fcf";
        string fixturePath = Path.Combine(fixtureParent, fixtureId);
        if (!Directory.Exists(fixtureParent) || !Directory.Exists(Path.GetDirectoryName(output)) ||
            Directory.Exists(output) || File.Exists(output) || new FileInfo(output).LinkTarget != null ||
            output == bin || output == cue ||
            Path.GetFullPath(output).StartsWith(Path.GetFullPath(fixtureParent) + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
            Directory.Exists(fixturePath) || File.Exists(fixturePath) || new FileInfo(fixturePath).LinkTarget != null)
            throw new InvalidDataException("Reviewed tiled-family output and fixture must be new, unlinked siblings separate from original files.");
        var sources = new HeldPair(bin, cue);
        List<Exception> failures = [];
        F? fixture = null;
        Task? operation = null;
        int checks = 0;
        try
        {
            sources.Open();
            var expected = NativeFreshTiledFamilyReadinessCases.Document();
            byte[] canonical = NativeFreshLevelDocumentCodec.SerializeCanonical(expected);
            // F's protected legacy document requires Initial.Base. Keep that
            // owner intact, then use real Store/Window Reopen for the aggregate.
            fixture = new F(fixtureParent, fixtureId, document: NativeFreshFamilyDocumentSessionCases.Document());
            NativeFreshLevelDocumentStore.Save(NativeFreshLevelDocumentStore.Capture(fixture.Project), expected);
            fixture.ExpectedDocument = canonical;
            fixture.Fire("FreshDocumentReopen");
            fixture.Select(NativeFreshFamilyDocumentSessionCases.FamilyId);
            var applied = fixture.Window.CurrentAggregateIntent!;
            fixture.Find<ListBox>("FreshLevelTiles").SelectedItem = applied.TiledContent!.Tiles[1]; F.Pump();
            fixture.SelectedSource = (bin, cue);
            fixture.Window.ResearchOutputPickerForTesting = _ => Task.FromResult<string?>(output);
            var session = TiledSession(fixture);
            Check(applied is { SchemaVersion: 5, LegacyContent: null, TiledContent.Tiles.Count: 4 } &&
                applied.Placements.Count == 3 && NativeFreshLevelDocumentCodec.SerializeCanonical(applied).AsSpan().SequenceEqual(canonical) &&
                session.SelectedEditorId == NativeFreshFamilyDocumentSessionCases.FamilyId &&
                session.SelectedTileId == new string('5', 32) &&
                Equals(fixture.Find<ComboBox>("FreshMaterial0SourceLevel").SelectedItem, 14) &&
                Equals(fixture.Find<ComboBox>("FreshMaterial1SourceLevel").SelectedItem, 70) &&
                fixture.Text("FreshMaterial0TextureId") == "1" && fixture.Text("FreshMaterial1TextureId") == "1" &&
                !fixture.Window.HasUnsavedChanges && !fixture.Window.HasUnappliedFields &&
                fixture.Find<Button>("FreshDocumentResearch").IsEnabled && fixture.Window.SavedResearchWorkForTesting == null,
                "Actual Store/Reopen owns the complete four-tile schema5 fixture, selected Family/tile5 and both materials; no fake worker or legacy projection.");
            var openedSnapshot = session.Snapshot;
            fixture.Fire("FreshDocumentSave");
            var savedSnapshot = session.Snapshot;
            Check(!ReferenceEquals(openedSnapshot, savedSnapshot) && ReferenceEquals(applied, fixture.Window.CurrentAggregateIntent) &&
                savedSnapshot.Intent != null && !ReferenceEquals(savedSnapshot.Intent, applied) &&
                NativeFreshLevelDocumentCodec.SerializeCanonical(savedSnapshot.Intent).AsSpan().SequenceEqual(canonical),
                "Actual Save returns a distinct complete parsed Snapshot while retaining the applied tiled/family object.");
            var releaseError = new IOException("actual-tiled-ui-owner-release-after-real-publication-simulation");
            fixture.OnRelease = () =>
            {
                fixture.Fire("FreshDocumentCancelResearch");
                fixture.CurrentProject = false;
            };
            fixture.ReleaseFailure = releaseError;
            fixture.Fire("FreshDocumentResearch");
            operation = fixture.Window.ResearchTaskForTesting ?? throw new InvalidDataException("Missing actual tiled publication task.");
            F.Drain(operation);
            var result = fixture.Window.ResearchPublicationForTesting;
            Check(ReferenceEquals(savedSnapshot, fixture.Window.SavedResearchSnapshotForTesting),
                "Actual production preparation/export retains the returned Save Snapshot, not an equal object or later capture.");
            Check(result != null && result.WholeImageReadback && result.ImageSha256 == TiledFamilyImage &&
                result.ChangedRawSectors == 1297 && result.OutputDirectory == output && result.RetainedRecoveryDirectory.Length == 0 &&
                result.ImagePath == Path.Combine(output, "native-fresh-saved-level-r01.bin") &&
                result.CuePath == Path.Combine(output, "native-fresh-saved-level-r01.cue") &&
                result.ReceiptPath == Path.Combine(output, "native-fresh-saved-level-r01.receipt.json") &&
                !result.RuntimeVerified && !result.NormalCreateBinApproved && !result.CompleteNativeDependencyClosureProven,
                "Actual saved tiled-family publication matches the independent complete native image and sector count without runtime approval.");
            Check(ReferenceEquals(applied, fixture.Window.CurrentAggregateIntent) &&
                ReferenceEquals(savedSnapshot, session.Snapshot) && session.SelectedEditorId == NativeFreshFamilyDocumentSessionCases.FamilyId &&
                session.SelectedTileId == new string('5', 32) && fixture.AcquiredOwners == 1 && fixture.ReleaseAttempts == 1 &&
                !fixture.Window.ResearchBusyForTesting,
                "Awaited actual owner settles once without replacing the complete document, saved version or selections.");
            string status = fixture.Find<TextBlock>("FreshDocumentStatus").Text ?? "";
            Check(ReferenceEquals(releaseError, fixture.Window.SavedResearchFailureForTesting) &&
                status.Contains("byte-verified research candidate", StringComparison.Ordinal) &&
                status.Contains(result!.CuePath, StringComparison.Ordinal) && status.Contains(result.ReceiptPath, StringComparison.Ordinal) &&
                status.Contains("captured version", StringComparison.Ordinal) && status.Contains(releaseError.Message, StringComparison.Ordinal),
                "Real successful publication, exact paths and classification survive advisory cancellation and actual owner-release failure.");
            Check(Directory.EnumerateFileSystemEntries(output).Select(Path.GetFileName).Order().SequenceEqual(new[]
                { "native-fresh-saved-level-r01.bin", "native-fresh-saved-level-r01.cue", "native-fresh-saved-level-r01.receipt.json" }.Order()),
                "Exactly the three real saved-publisher artifacts; no stage or recovery adoption.");
            RequireTiledFamilyReceipt(savedSnapshot, result!, bin, cue, Check);
        }
        catch (Exception error) { failures.Add(error); }
        finally
        {
            Attempt(() => { if (operation != null) F.Drain(operation); });
            RetainFailure();
            Attempt(() => fixture?.Close());
            Attempt(() => fixture?.RequireProtected());
            // Keep all actual artifacts even after an unknown/failed return.
            sources.Complete(failures);
            RetainFailure();
        }
        F.Throw(failures);
        Console.WriteLine($"FRESH_SAVED_TILED_FAMILY_RESEARCH_SOURCE_UI_OK checks={checks} originalHeldNamedFinalAndClosed=true nativeOutputWritten=true actualPublication=true image={TiledFamilyImage} changedRawSectors=1297 runtime=false output={output}");
        void Check(bool condition, string message) { if (!condition) throw new InvalidDataException(message); checks++; }
        void Attempt(Action action) { try { action(); } catch (Exception error) { failures.Add(error); } }
        void RetainFailure()
        {
            // Match the retained old fixture's failed-test accounting: append
            // the actual stored error only after an earlier failure, once.
            if (failures.Count == 0 || fixture?.Window.SavedResearchFailureForTesting is not { } actual) return;
            if (!failures.SelectMany(Nodes).Any(error => ReferenceEquals(error, actual))) failures.Add(actual);
        }
        static IEnumerable<Exception> Nodes(Exception error)
        {
            yield return error;
            if (error is AggregateException group)
                foreach (var child in group.InnerExceptions)
                    foreach (var node in Nodes(child)) yield return node;
            else if (error.InnerException != null)
                foreach (var node in Nodes(error.InnerException)) yield return node;
        }
    }

    private static NativeFreshLevelDocumentSession TiledSession(F fixture) =>
        (NativeFreshLevelDocumentSession)typeof(NativeFreshDocumentWindow).GetField("_aggregateSession",
            BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(fixture.Window)!;

    private static void RequireTiledFamilyReceipt(NativeFreshLevelDocumentSnapshot snapshot,
        NativeId65FreshDiscResearchPublication publication, string bin, string cue, Action<bool, string> check)
    {
        var expected = NativeFreshTiledFamilyReadinessCases.Document();
        byte[] canonicalBytes = NativeFreshLevelDocumentCodec.SerializeCanonical(expected);
        using var canonical = JsonDocument.Parse(canonicalBytes);
        RequireAbsoluteUnlinked(publication.ImagePath);
        using (var image = new FileStream(publication.ImagePath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            check(image.Length == 661547040, "Actual published image has the complete independent physical length.");
            string hash = Convert.ToHexStringLower(SHA256.HashData(image));
            check(hash == TiledFamilyImage && image.Position == 661547040 && image.ReadByte() == -1 && image.Length == 661547040,
                "Independent physical readback consumes the complete 689fcf image through true EOF, not a prefix or receipt-only hash.");
        }
        RequireAbsoluteUnlinked(publication.ReceiptPath);
        byte[] receiptBytes;
        using (var input = new FileStream(publication.ReceiptPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            if (input.Length is < 1 or > 65536) throw new InvalidDataException("Complete tiled-family receipt exceeds its bounded owner.");
            receiptBytes = new byte[checked((int)input.Length)]; input.ReadExactly(receiptBytes);
            check(input.Position == receiptBytes.Length && input.ReadByte() == -1 && input.Length == receiptBytes.Length,
                "Complete bounded physical receipt reaches actual EOF without omitted lineage or truncation.");
        }
        using var receipt = JsonDocument.Parse(receiptBytes);
        var root = receipt.RootElement; var saved = root.GetProperty("saved"); var lineage = root.GetProperty("lineage");
        check(snapshot.Intent is { SchemaVersion: 5, LegacyContent: null, TiledContent.Tiles.Count: 4 } &&
            NativeFreshLevelDocumentCodec.SerializeCanonical(snapshot.Intent).AsSpan().SequenceEqual(canonicalBytes) &&
            root.GetProperty("variant").GetString() == TiledFamilyVariant &&
            root.GetProperty("planSha256").GetString() == publication.PlanSha256 &&
            root.GetProperty("imageSha256").GetString() == TiledFamilyImage && root.GetProperty("imageByteLength").GetInt64() == 661547040 &&
            root.GetProperty("changedRawSectors").GetInt32() == 1297 && root.GetProperty("savedRevisionBound").GetBoolean() &&
            root.GetProperty("wholeImageInverseRequired").GetBoolean() &&
            saved.GetProperty("projectId").GetString() == snapshot.ProjectId && saved.GetProperty("root").GetString() == snapshot.RootPath &&
            saved.GetProperty("path").GetString() == snapshot.Path && saved.GetProperty("rawSha256").GetString() == snapshot.ContentSha256 &&
            saved.GetProperty("manifestSha256").GetString() == snapshot.ManifestSha256 &&
            saved.GetProperty("canonicalSha256").GetString() == Convert.ToHexStringLower(SHA256.HashData(canonicalBytes)) &&
            JsonElement.DeepEquals(saved.GetProperty("canonical"), canonical.RootElement),
            "Physical receipt binds the complete actual Save revision and exact tiled schema5 canonical owner, never the legacy R03 arm.");
        check(root.GetProperty("SourceImagePath").GetString() == Path.GetFullPath(bin) &&
            root.GetProperty("SourceCuePath").GetString() == Path.GetFullPath(cue) &&
            root.GetProperty("sourceImageSha256").GetString() == "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37" &&
            root.GetProperty("SourceCueSha256").GetString() == "05142502c75d3a0b6374003a8fe41893d6faf0ad55863ff0b62a265a1b152766",
            "Receipt names the separately held, completely authenticated Original BIN/CUE pair.");
        var content = canonical.RootElement.GetProperty("content"); var tiles = content.GetProperty("tiles");
        check(content.GetProperty("tag").GetString() == "ordered-flat-tiles-v1" && tiles.GetArrayLength() == 4 &&
            Enumerable.Range(0, 4).All(i => tiles[i].GetProperty("id").GetString() == new string((char)('4' + i), 32)) &&
            JsonElement.DeepEquals(lineage.GetProperty("tiledContent"), content) && !lineage.TryGetProperty("floor", out _) &&
            lineage.GetProperty("tileCount").GetInt32() == 4 && lineage.GetProperty("orderedFaceMaterials").EnumerateArray()
                .Select(value => value.GetInt32()).SequenceEqual(new[] { 0, 0, 0, 1, 0, 1, 1, 1 }),
            "All four stable tile IDs, exact whole geometry/settings and eight face assignments remain in the actual receipt; no Base projection.");
        var selections = lineage.GetProperty("materialSelections"); var materials = lineage.GetProperty("materials");
        check(selections.GetArrayLength() == 2 && materials.GetArrayLength() == 2 && Enumerable.Range(0, 2).All(i =>
            selections[i].GetProperty("SourceVariant").GetString() == F.Variant &&
            selections[i].GetProperty("SourceWadEntry").GetInt32() == (i == 0 ? 14 : 70) && selections[i].GetProperty("TextureId").GetInt32() == 1 &&
            materials[i].GetProperty("slot").GetInt32() == i && materials[i].GetProperty("SourceWadEntry").GetInt32() == (i == 0 ? 14 : 70) &&
            materials[i].GetProperty("SourceTextureId").GetInt32() == 1 &&
            materials[i].GetProperty("SourceImageSha256").GetString() == root.GetProperty("sourceImageSha256").GetString() &&
            IsSha256(materials[i].GetProperty("SourceDataSha256").GetString()) && IsSha256(materials[i].GetProperty("sourceRecordSha256").GetString())),
            "Both first-occurrence material descriptors retain their exact14/70 T1 native sources and complete source evidence.");
        var authored = canonical.RootElement.GetProperty("placements"); var families = lineage.GetProperty("families");
        check(lineage.GetProperty("authoredPlacementCount").GetInt32() == 3 && lineage.GetProperty("nativePlacedCount").GetInt32() == 9 &&
            lineage.GetProperty("nativeNominalCapacity").GetInt32() == 32 && families.GetArrayLength() == 3 && Enumerable.Range(0, 3).All(i =>
            families[i].GetProperty("EditorId").GetString() == expected.Placements[i].EditorId &&
            families[i].GetProperty("FirstRowIndex").GetInt32() == i && families[i].GetProperty("RowCount").GetInt32() == (i == 2 ? 7 : 1) &&
            JsonElement.DeepEquals(families[i].GetProperty("authoredPlacement"), authored[i])),
            "Actual ordered Wizard/Grass/Family authorship expands1+1+7, retaining full placement/source/Reward2 content and P9/N32 counts.");
        int[] classes = [0x11B, 0x1F5, 0xAD, 0xAE, 0xD, 0xD, 0xD, 0xD, 0xD];
        int[] privateOffsets = [0x1248, 0x1298, 0x12A0, 0x12A4, 0x12BC, 0x12E4, 0x130C, 0x1334, 0x135C];
        int[] privateLengths = [0x50, 8, 4, 0x18, 0x28, 0x28, 0x28, 0x28, 0x28];
        var owners = lineage.GetProperty("nativeRowOwners"); var selectors = lineage.GetProperty("sourceSelectorRows");
        check(owners.GetArrayLength() == 9 && selectors.GetArrayLength() == 9 && Enumerable.Range(0, 9).All(i =>
            owners[i].GetProperty("nativeIndex").GetInt32() == i && owners[i].GetProperty("actorClass").GetInt32() == classes[i] &&
            owners[i].GetProperty("rowOffset").GetInt32() == 0xD4 + i * 0x58 && owners[i].GetProperty("rowLength").GetInt32() == 0x58 &&
            owners[i].GetProperty("privateOffset").GetInt32() == privateOffsets[i] && owners[i].GetProperty("privateLength").GetInt32() == privateLengths[i] &&
            selectors[i].GetProperty("PlacedOrdinal").GetInt32() == i && selectors[i].GetProperty("ActorClassId").GetInt32() == classes[i] &&
            IsSha256(owners[i].GetProperty("finalRowSha256").GetString()) && IsSha256(owners[i].GetProperty("sourceRowSha256").GetString()) &&
            owners[i].GetProperty("finalRowSha256").GetString() == selectors[i].GetProperty("SceneRowSha256").GetString() &&
            owners[i].GetProperty("sourceRowSha256").GetString() == selectors[i].GetProperty("SourceRowSha256").GetString()),
            "Every complete native row/private extent, class, selector and source/final row hash survives the actual saved UI publication.");
        foreach (string plan in new[] { "compilationPlan", "discPlan", "executablePlan", "bodyPlan", "terrainPlan", "parentDiscPlan",
            "parentExecutablePlan", "parentPlacementPlan", "parentClearancePlan", "tiledUnionClearancePlan", "familyCompositionPlan" })
            check(IsSha256(lineage.GetProperty(plan).GetString()), "Complete typed family ancestry remains named in the physical receipt: " + plan);
        check(lineage.GetProperty("sceneSha256").GetString() == "4f560dcbfa2d997a3f02554cf297d95c1778f67fc5d8fcf8ff47f64d609a89e8" &&
            lineage.GetProperty("bodySha256").GetString() == "c8c85e71b633e29ce573dddccb04a4a0adcd6696d292658304328e435dfaa9ca" &&
            lineage.GetProperty("modelSha256").GetString() == "b3fdc6639588149657de506300930e5f96cb69dc3b02a4362fac52a7c3f764bf" &&
            lineage.GetProperty("dataSha256").GetString() == "fe754f8e5e4686fdfe03d7e516dab8f1b52ad605b1541615e98d3f0511e4835e" &&
            lineage.GetProperty("executableSha256").GetString() == "81501f07ec9ce336089ec5a92f81bf51569aed723ac4f8ba1cf85ba0ef69a431" &&
            lineage.GetProperty("UsedLevelDataBytes").GetInt32() == 0xCB8 && lineage.GetProperty("ModelRamStart").GetInt64() == 0x8008D6F0 &&
            lineage.GetProperty("SceneRamStart").GetInt64() == 0x800A86F0 && lineage.GetProperty("SceneReadEnd").GetInt64() == 0x800ABEF0,
            "Actual emitted data/model/Scene/Body/EXE hashes and runtime layout match the independent tiled child, not old rectangle bounds.");
        var clearance = lineage.GetProperty("clearance");
        check(lineage.GetProperty("clearanceScope").GetString() == "exact-four-tile-union-initial-and-fixed-xy-landing-only" &&
            clearance.GetProperty("UnionX").GetInt32() == 8448 && clearance.GetProperty("UnionY").GetInt32() == 8448 &&
            clearance.GetProperty("UnionWidth").GetInt32() == 3072 && clearance.GetProperty("UnionDepth").GetInt32() == 3072 &&
            clearance.GetProperty("FloorZ").GetInt32() == 8704 && clearance.GetProperty("OuterUnionEquivalentProven").GetBoolean() &&
            clearance.GetProperty("StaticLandingClearanceProven").GetBoolean() &&
            JsonElement.DeepEquals(lineage.GetProperty("rewardDestinationOverrides"), JsonSerializer.SerializeToElement(
                ((NativeFreshLockedChestFamilyPlacementIntent)expected.Placements[2]).RewardDestinations!.Values)) &&
            lineage.GetProperty("KnownSelectorCount").GetInt32() == 7 && lineage.GetProperty("KnownDeterministicSelectorSubtotal").GetInt32() == 20 &&
            lineage.GetProperty("ExperimentalTreasureTarget").GetInt32() == 20,
            "Exact tile-union static clearance, explicit Reward2 WORLD override and known selector subtotal remain narrow evidence.");
        foreach (var (name, values) in new[] {
            ("ChestMargins", new[] { "2416", "656", "1440", "1632" }),
            ("PlayerMargins", new[] { "512", "2560", "512", "2560" }) })
        {
            var margins = clearance.GetProperty(name);
            check(margins.EnumerateObject().Count() == 4 && new[] { "Left", "Right", "Top", "Bottom" }
                .Select(key => margins.GetProperty(key).GetString()).SequenceEqual(values),
                "All four exact invariant-string union clearance margins survive complete receipt serialization: " + name);
        }
        foreach (string flag in new[] { "runtimeVerified", "normalCreateBinApproved", "completeNativeDependencyClosureProven", "nativePersistentSaveOwnerProven",
            "publicationApproved", "hqRuntimeProven", "audioRuntimeProven", "peakRuntimeMemoryProven", "checkpointOrForeignStateSupported",
            "keyZeroContributionProven", "completeStaticCensusProven", "runtimeDropGuarantee" })
            check(!root.GetProperty(flag).GetBoolean(), "Actual saved tiled UI publication does not grant pending authority: " + flag);
        check(root.GetProperty("experimentalKeyZeroAssumption").GetBoolean() && lineage.GetProperty("experimentalKeyZeroAssumption").GetBoolean() &&
            !lineage.GetProperty("keyZeroContributionProven").GetBoolean() && !lineage.GetProperty("completeStaticCensusProven").GetBoolean() &&
            !lineage.GetProperty("runtimeDropGuarantee").GetBoolean() && !lineage.GetProperty("footprintAndTrajectoryProven").GetBoolean(),
            "Key-zero stays experimental; no census, complete trajectory or runtime-drop guarantee is fabricated.");
        static bool IsSha256(string? value) => value is { Length: 64 } && value.Any(c => c != '0') &&
            value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    }
}
