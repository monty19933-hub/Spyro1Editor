using System.Security.Cryptography;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Persistence;
using Spyro.Editor.Core.Workspace;

string repositoryRoot = FindRepositoryRoot(args.ElementAtOrDefault(0));
LevelCatalog catalog = LevelCatalog.Load(repositoryRoot);
EditorWorkspace workspace = EditorWorkspace.Find(repositoryRoot);
string sourceImage = Path.GetFullPath(args.ElementAtOrDefault(1) ?? DiscImageLocator.FindImage(workspace));
string sourceCue = Path.GetFullPath(args.ElementAtOrDefault(2) ?? DiscImageLocator.FindCueForImage(sourceImage));
if (!File.Exists(sourceImage) || !File.Exists(sourceCue))
{
    throw new FileNotFoundException(
        "The Stone Hill replacement baseline smoke requires the configured clean USA retail BIN/CUE.",
        sourceImage);
}

string temporaryRoot = Path.Combine(Path.GetTempPath(), $"spyro-editor-stonehill-replacement-{Guid.NewGuid():N}");
Directory.CreateDirectory(temporaryRoot);
try
{
    NativeLevelSlotContract slot = NativeLevelReplacementSupportCatalog.RequireSupported(catalog, "stonehill");
    NativeLevelReplacementManifest manifest = await NativeLevelReplacementStore.StartStoneHillAsync(
        temporaryRoot,
        sourceImage,
        catalog);
    Require(manifest.Slot == slot, "Stone Hill's immutable retail-slot identity changed.");
    Require(manifest.EvidenceStatus == NativeLevelReplacementEvidenceStatus.StaticBaselineOnly,
        "An unproven replacement manifest escaped the static-baseline gate.");
    Require(manifest.Source.Wad.Lba == 37 && manifest.Source.Wad.ByteLength == 0x6927000,
        "The supported retail WAD extent changed.");
    Require(manifest.Source.WadArchiveHeaderByteLength == 0x800 &&
        manifest.Source.WadArchiveHeaderSha256 == "15d3e9c45f07e27a0ddd3f3a49b23a077e2c7750021cc89c55b7b16c551186cd",
        "The complete retail WAD directory preimage changed.");
    Require(manifest.Source.LevelMetadataEntry.DirectoryIndex == 9 &&
        manifest.Source.LevelMetadataEntry.FirstWord == 11,
        "The preceding Artisans overlay is not WAD entry 9 / loader marker 11.");
    Require(manifest.Source.MetadataAdjacentEntry.DirectoryIndex == 10,
        "The preceding Artisans level-data archive identity changed.");
    Require(manifest.Source.LoadedDataPredecessorEntry.DirectoryIndex == 11 &&
        manifest.Source.LoadedDataPredecessorEntry.FirstWord == 12 &&
        manifest.Source.LoadedDataPredecessorEntry.Sha256 ==
            "876c0145649bb5b26921858d4d677468463974901dcc416dcba5ccea25caa069",
        "Stone Hill's overlay WAD entry 11 preimage changed.");
    Require(manifest.Source.LevelDataEntry.DirectoryIndex == 12 &&
        manifest.Source.LevelDataEntry.WadOffset == 0xB93800 &&
        manifest.Source.LevelDataEntry.ByteLength == 0x362800 &&
        manifest.Source.LevelDataEntry.Sha256 ==
            "c341a3a10a67590c69d23547f6ad147f05f0b6fb7d0fb1360d572793276e796b",
        "Stone Hill's loaded WAD entry 12 preimage changed.");
    Require(manifest.Source.NestedHeaderByteLength == 0x800 &&
        manifest.Source.NestedHeaderSha256 ==
            "36874561802906bdfe36e8f5843867b7452257d5d89e1364f6a7aedead6eb4ac" &&
        manifest.Source.NestedDescriptorTableByteLength == 0x40 &&
        manifest.Source.NestedDescriptorTableSha256 ==
            "2d09198d4197c7c827bfcbb16090e98c10fb28765ca83c950e9e67b60b3360f6",
        "Stone Hill's complete nested header or strict eight-row descriptor table changed.");
    Require(manifest.Source.LevelDataSubfile == new NativeNestedSubfilePreimage(
            1,
            0xF6000,
            0x98800,
            "9224302f1bf19a5f10ffe07ec8b983c9b0be8579874d55ece19e5047bfe6ba2c"),
        "Stone Hill's native level-data subfile preimage changed.");
    Require(manifest.Source.LevelDataSubfiles.Count == 8 &&
        manifest.Source.LevelDataSubfiles.Select(subfile => subfile.SubfileIndex)
            .SequenceEqual(Enumerable.Range(0, 8)) &&
        manifest.Source.LevelDataSubfiles.Sum(subfile => subfile.ByteLength) ==
            manifest.Source.LevelDataEntry.ByteLength - manifest.Source.NestedHeaderByteLength,
        "Stone Hill's complete eight-subfile archive inventory changed.");
    Require(manifest.Source.SourceMobyTableSubfile.SubfileIndex == 3 &&
        manifest.Source.SourceMobyTableSubfile.RelativeOffset == 0x1DF000 &&
        manifest.Source.SourceMobyTableSubfile.ByteLength == 0xC000 &&
        manifest.Source.SourceMobyTableByteLength == 195 * 0x58,
        "Stone Hill's 195-row Moby table no longer lies inside packed subfile 3.");

    NativeLevelReplacementManifest loaded = NativeLevelReplacementStore.Load(
            temporaryRoot,
            "stonehill",
            catalog)
        ?? throw new InvalidOperationException("The saved Stone Hill replacement manifest was not loaded.");
    Require(loaded.Slot == manifest.Slot && loaded.Source.SourceImageSha256 == manifest.Source.SourceImageSha256 &&
        loaded.Source.ArtisansPortalControlRows.SequenceEqual(manifest.Source.ArtisansPortalControlRows) &&
        loaded.Source.StoneHillReturnHomeRows.SequenceEqual(manifest.Source.StoneHillReturnHomeRows),
        "The Stone Hill replacement manifest changed during save/load.");
    Require(PortableProjectMigration.Classify("stonehill-native-level-replacement.json") ==
        PortableProjectArtifactKind.ProjectEdit,
        "Native level-replacement manifests are not protected by portable project migration.");

    Require(NativeLevelReplacementProfileRegistry.Profiles.Count == 1 &&
        NativeLevelReplacementProfileRegistry.Profiles is not NativeLevelReplacementProfile[],
        "The guarded V5 replacement registry unexpectedly contains an unproven route.");
    NativeLevelReplacementProfile profile =
        NativeLevelReplacementProfileRegistry.RequireRuntimeProven(
            NativeLevelReplacementProfileRegistry.TownSquareIntoStoneHillProfileId,
            loaded,
            catalog);
    Require(profile.Evidence == NativeLevelReplacementEvidenceStatus.RuntimeProven &&
        profile.MatchesBaseline(loaded) &&
        profile.OutputImageSha256 ==
            NativeLevelReplacementProfileRegistry.TownSquareIntoStoneHillOutputImageSha256,
        "The exact runtime-proven Town Square -> Stone Hill profile did not match the retail baseline.");

    Require(NativeLevelReplacementIntentStore.Load(
            temporaryRoot,
            "stonehill",
            loaded,
            catalog) is null,
        "A Beta V1-V4 project without a V5 replacement intent did not load as native behavior.");

    NativeLevelReplacementIntent intent = await NativeLevelReplacementIntentStore.StartAsync(
        temporaryRoot,
        profile.Id,
        loaded,
        catalog);
    string manifestPath = NativeLevelReplacementStore.GetPath(temporaryRoot, "stonehill");
    string intentPath = NativeLevelReplacementIntentStore.GetPath(temporaryRoot, "stonehill");
    Require(File.Exists(manifestPath) && File.Exists(intentPath) && manifestPath != intentPath,
        "The replacement intent was not persisted separately from the retail baseline manifest.");
    NativeLevelReplacementIntent loadedIntent = NativeLevelReplacementIntentStore.Load(
            temporaryRoot,
            "stonehill",
            loaded,
            catalog)
        ?? throw new InvalidOperationException("The guarded replacement intent was not loaded.");
    Require(loadedIntent == intent &&
        NativeLevelReplacementIntentStore.Validate(loadedIntent, loaded, catalog) == profile,
        "The guarded replacement intent changed during atomic save/load.");
    Require(PortableProjectMigration.Classify(
            "stonehill-native-level-replacement-intent.json") ==
        PortableProjectArtifactKind.ProjectEdit,
        "Native level-replacement intents are not protected by portable project migration.");
    Require(!Directory.EnumerateFiles(temporaryRoot, "*.tmp").Any(),
        "Atomic replacement-intent persistence left a temporary file behind.");
    ExpectFailure(
        () => NativeLevelReplacementIntentStore.GetPath(temporaryRoot, "../stonehill"),
        "A replacement-intent path traversal escaped the project root.");
    ExpectFailure(
        () => NativeLevelReplacementIntentStore.Validate(
            loadedIntent with { ProfileRecipeVersion = loadedIntent.ProfileRecipeVersion + 1 },
            loaded,
            catalog),
        "A stale replacement recipe version was accepted.");
    ExpectFailure(
        () => NativeLevelReplacementIntentStore.Validate(
            loadedIntent with { ProfileId = loadedIntent.ProfileId.ToUpperInvariant() },
            loaded,
            catalog),
        "A noncanonical replacement profile identity was accepted.");
    ExpectFailure(
        () => NativeLevelReplacementIntentStore.Validate(
            loadedIntent with { DonorLevelKey = "darkhollow" },
            loaded,
            catalog),
        "A modified replacement donor escaped the code-owned profile.");
    ExpectFailure(
        () => NativeLevelReplacementIntentStore.Validate(
            loadedIntent with { SourceImageSha256 = new string('a', 64) },
            loaded,
            catalog),
        "A stale replacement-intent source SHA-256 was accepted.");
    ExpectFailure(
        () => NativeLevelReplacementIntentStore.Validate(
            loadedIntent with { Version = loadedIntent.Version + 1 },
            loaded,
            catalog),
        "An unsupported replacement-intent version was accepted.");
    await ExpectFailureAsync(
        async () => _ = await NativeLevelReplacementIntentStore.StartAsync(
            temporaryRoot,
            "unregistered-profile",
            loaded,
            catalog),
        "An unregistered replacement profile produced a persistent intent.");

    NativeLevelReplacementBaselinePlan baselinePlan =
        NativeLevelReplacementSafetyInspector.BuildBaselinePlan(loaded, catalog);
    Require(baselinePlan.PlannedWrites.Count == 0 && baselinePlan.Safety.PlannedPatchCount == 0,
        "The first Stone Hill baseline unexpectedly planned a byte write.");
    Require(baselinePlan.Safety.Portal.TrueIndexes.SequenceEqual([38, 144, 157]) &&
        loaded.Source.StoneHillReturnHomeRows.Select(row => row.TrueIndex).SequenceEqual([177, 179]),
        "Portal or Return Home identities were not preserved concretely.");

    ExpectFailure(
        () => NativeLevelReplacementStore.GetPath(temporaryRoot, "../stonehill"),
        "A manifest path traversal escaped the project root.");
    ExpectFailure(
        () => NativeLevelReplacementStore.Validate(
            loaded with { EvidenceStatus = NativeLevelReplacementEvidenceStatus.RuntimeProven },
            catalog),
        "A mutable JSON evidence field promoted an unproven replacement.");
    ExpectFailure(
        () => NativeLevelReplacementStore.Validate(
            loaded with { Slot = loaded.Slot with { LevelId = 36 } },
            catalog),
        "A changed retail level identity was accepted.");
    await ExpectFailureAsync(
        () => NativeLevelReplacementStore.ValidateSourceAsync(
            loaded with
            {
                Source = loaded.Source with
                {
                    WadArchiveHeaderSha256 = new string('a', 64)
                }
            },
            sourceImage,
            catalog),
        "A fabricated WAD preimage reached the exporter boundary.");

    string outputImage = Path.Combine(temporaryRoot, "stonehill-no-edit.bin");
    string outputCue = Path.Combine(temporaryRoot, "stonehill-no-edit.cue");
    await ExpectFailureAsync(
        () => NativeLevelReplacementBaselineExporter.ExportAsync(new(
            loaded, catalog, sourceImage, sourceCue, sourceImage, outputCue)),
        "The baseline exporter accepted source BIN/output BIN aliasing.");
    await ExpectFailureAsync(
        () => NativeLevelReplacementBaselineExporter.ExportAsync(new(
            loaded, catalog, sourceImage, sourceCue, outputImage, outputImage)),
        "The baseline exporter accepted output BIN/output CUE aliasing.");
    await ExpectFailureAsync(
        () => NativeLevelReplacementBaselineExporter.ExportAsync(new(
            loaded, catalog, sourceImage, sourceCue, sourceCue, outputCue)),
        "The baseline exporter accepted source CUE/output BIN aliasing.");
    string otherOutputDirectory = Path.Combine(temporaryRoot, "other");
    Directory.CreateDirectory(otherOutputDirectory);
    await ExpectFailureAsync(
        () => NativeLevelReplacementBaselineExporter.ExportAsync(new(
            loaded,
            catalog,
            sourceImage,
            sourceCue,
            outputImage,
            Path.Combine(otherOutputDirectory, "stonehill-no-edit.cue"))),
        "The baseline exporter accepted a CUE outside its BIN directory.");
    string wrongCue = Path.Combine(temporaryRoot, "wrong-source.cue");
    await File.WriteAllTextAsync(wrongCue,
        "FILE \"not-the-source.bin\" BINARY\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\n");
    await ExpectFailureAsync(
        () => NativeLevelReplacementBaselineExporter.ExportAsync(new(
            loaded, catalog, sourceImage, wrongCue, outputImage, outputCue)),
        "The baseline exporter accepted a CUE that does not point to its source BIN.");
    string missingIndexCue = Path.Combine(temporaryRoot, "missing-index-source.cue");
    await File.WriteAllTextAsync(missingIndexCue,
        $"FILE \"{sourceImage}\" BINARY\n  TRACK 01 MODE2/2352\n");
    await ExpectFailureAsync(
        () => NativeLevelReplacementBaselineExporter.ExportAsync(new(
            loaded, catalog, sourceImage, missingIndexCue, outputImage, outputCue)),
        "The baseline exporter accepted a CUE without INDEX 01 00:00:00.");
    string missingBinaryCue = Path.Combine(temporaryRoot, "missing-binary-source.cue");
    await File.WriteAllTextAsync(missingBinaryCue,
        $"FILE \"{sourceImage}\"\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\n");
    await ExpectFailureAsync(
        () => NativeLevelReplacementBaselineExporter.ExportAsync(new(
            loaded, catalog, sourceImage, missingBinaryCue, outputImage, outputCue)),
        "The baseline exporter accepted a CUE FILE directive without BINARY.");

    string recoveryRoot = Path.Combine(temporaryRoot, "forced-restore-failure");
    Directory.CreateDirectory(recoveryRoot);
    string retainedBackup = Path.Combine(recoveryRoot, "previous.bin.bak");
    string blockedRestoreTarget = Path.Combine(recoveryRoot, "previous.bin");
    await File.WriteAllTextAsync(retainedBackup, "previous output");
    Directory.CreateDirectory(blockedRestoreTarget);
    bool backupPending = true;
    Exception? forcedRecoveryFailure = NativeLevelReplacementBaselineExporter.TryRestoreBackup(
        "BIN",
        retainedBackup,
        blockedRestoreTarget,
        ref backupPending);
    Require(forcedRecoveryFailure != null && backupPending && File.Exists(retainedBackup) &&
        forcedRecoveryFailure.Message.Contains(retainedBackup, StringComparison.Ordinal),
        "A forced rollback failure did not retain and identify the previous output backup.");
    Directory.Delete(blockedRestoreTarget);
    Exception? recoveryRetryFailure = NativeLevelReplacementBaselineExporter.TryRestoreBackup(
        "BIN",
        retainedBackup,
        blockedRestoreTarget,
        ref backupPending);
    Require(recoveryRetryFailure == null && !backupPending && !File.Exists(retainedBackup) &&
        await File.ReadAllTextAsync(blockedRestoreTarget) == "previous output",
        "A retained previous-output backup could not be restored after the obstruction was removed.");

    NativeLevelReplacementBaselineResult baseline =
        await NativeLevelReplacementBaselineExporter.ExportAsync(new(
            loaded,
            catalog,
            sourceImage,
            sourceCue,
            outputImage,
            outputCue));
    Require(baseline.ByteIdentical && baseline.SourceSha256 == baseline.OutputSha256 &&
        baseline.WadHeaderIdentical && baseline.MetadataEntryIdentical && baseline.DataEntryIdentical &&
        baseline.NestedHeaderIdentical && baseline.LevelDataIdentical && baseline.MobyTableIdentical &&
        baseline.PortalAndReturnHomePreimagesIdentical,
        "The no-edit baseline did not preserve every explicit Stone Hill preimage.");
    Require((await HashFileAsync(sourceImage)) == loaded.Source.SourceImageSha256,
        "The baseline exporter changed the retail source BIN.");
    Require(File.ReadAllText(outputCue).Contains($"FILE \"{Path.GetFileName(outputImage)}\" BINARY", StringComparison.Ordinal),
        "The generated CUE does not point to the generated BIN.");

    await using (FileStream changed = new(outputImage, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
    {
        int original = changed.ReadByte();
        changed.Position = 0;
        changed.WriteByte((byte)(original ^ 0x01));
    }
    await ExpectFailureAsync(
        () => NativeLevelReplacementStore.ValidateSourceAsync(loaded, outputImage, catalog),
        "A same-length, one-byte stale source preimage was accepted.");

    string malformedRoot = Path.Combine(temporaryRoot, "malformed");
    Directory.CreateDirectory(malformedRoot);
    await File.WriteAllTextAsync(
        NativeLevelReplacementStore.GetPath(malformedRoot, "stonehill"),
        "{\"format\":\"spyro-editor-native-level-replacement\",\"version\":2}");
    ExpectFailure(
        () => NativeLevelReplacementStore.Load(malformedRoot, "stonehill", catalog),
        "A malformed replacement manifest did not fail closed.");
    await File.WriteAllTextAsync(
        NativeLevelReplacementIntentStore.GetPath(malformedRoot, "stonehill"),
        "{\"format\":\"spyro-editor-native-level-replacement-intent\",\"version\":1}");
    ExpectFailure(
        () => NativeLevelReplacementIntentStore.Load(
            malformedRoot,
            "stonehill",
            loaded,
            catalog),
        "A malformed replacement intent did not fail closed.");
    string unmappedRoot = Path.Combine(temporaryRoot, "unmapped-intent");
    Directory.CreateDirectory(unmappedRoot);
    string intentJson = await File.ReadAllTextAsync(intentPath);
    int finalBrace = intentJson.LastIndexOf('}');
    Require(finalBrace >= 0, "The saved replacement intent was not valid JSON text.");
    await File.WriteAllTextAsync(
        NativeLevelReplacementIntentStore.GetPath(unmappedRoot, "stonehill"),
        intentJson.Insert(finalBrace, ",\n  \"evidenceStatus\": \"runtimeProven\"\n"));
    ExpectFailure(
        () => NativeLevelReplacementIntentStore.Load(
            unmappedRoot,
            "stonehill",
            loaded,
            catalog),
        "An unmapped mutable evidence field was ignored instead of failing closed.");
    Require(NativeLevelReplacementIntentStore.Delete(temporaryRoot, "stonehill") &&
        !File.Exists(intentPath) && File.Exists(manifestPath) &&
        !NativeLevelReplacementIntentStore.Delete(temporaryRoot, "stonehill"),
        "Deleting a replacement intent did not leave the baseline manifest untouched and become idempotent.");

    Console.WriteLine(
        "PASS: exact clean-USA Stone Hill slot, runtime-proven code-owned profile, separate atomic/fail-closed intent persistence, metadata/data/archive/table preimages, concrete portal/Return Home guards, path/alias rejection, and byte-identical no-edit BIN/CUE baseline.");
}
finally
{
    if (Directory.Exists(temporaryRoot))
        Directory.Delete(temporaryRoot, recursive: true);
}

static string FindRepositoryRoot(string? supplied)
{
    if (!string.IsNullOrWhiteSpace(supplied))
        return Path.GetFullPath(supplied);
    DirectoryInfo? cursor = new(Directory.GetCurrentDirectory());
    while (cursor != null)
    {
        if (File.Exists(Path.Combine(cursor.FullName, "spyro-level-catalog.json")) &&
            Directory.Exists(Path.Combine(cursor.FullName, "src", "Spyro.Editor.Core")))
            return cursor.FullName;
        cursor = cursor.Parent;
    }
    throw new DirectoryNotFoundException("Could not find the Spyro Editor repository root.");
}

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

static void ExpectFailure(Action action, string message)
{
    try
    {
        action();
    }
    catch (Exception exception) when (
        exception is InvalidOperationException or InvalidDataException or ArgumentException)
    {
        return;
    }
    throw new InvalidOperationException(message);
}

static async Task ExpectFailureAsync(Func<Task> action, string message)
{
    try
    {
        await action();
    }
    catch (Exception exception) when (
        exception is FileNotFoundException or InvalidOperationException or InvalidDataException or ArgumentException)
    {
        return;
    }
    throw new InvalidOperationException(message);
}

static async Task<string> HashFileAsync(string path)
{
    await using FileStream stream = File.OpenRead(path);
    return Convert.ToHexString(await SHA256.HashDataAsync(stream)).ToLowerInvariant();
}
