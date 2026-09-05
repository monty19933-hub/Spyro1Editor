using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

const string SmokeRootPrefix =
    "Spyro.Editor.Id65AuthoringEditorDocumentWorkspaceWriterSmoke-";
const int MaximumLegacyTerrainEditByteCount = 16 * 1024 * 1024;
const string ExpectedLegacyFinalEnvelopeSha256 =
    "6892522ac22b13788ea6e66fcc14165baccd9fdedc9ec0ca29752f91b2bc254f";

Case[] cases =
[
    new(
        "terrain-t88-t107", true, true, true,
        "8f27bfc3f97238402c0b7dd59a081335ae19df1acfaefa9575d9ca7110dce808",
        "9d853ccfcf18627a46dc730844efc9eeeda28ee9a0d53c70ae3b7144652b4c3b"),
    new(
        "terrain-t88", true, true, false,
        "2af4d72a73ced5d133f77c48174c410a843276efd5de1c5b36b4eac3be01a690",
        "2f10969d64daad7344b78956585b41e5099a823426ecf446e14e6ab346708914"),
    new(
        "terrain-t107", true, false, true,
        "28d024e184ac4a0befc769c6ed77cb40fd12512bd4d6f75d0fac9b2b895d86cd",
        "09d538be3ce5936093370423d75310dbde04c3b8d81db13fb1da9e582e722c53"),
    new(
        "no-terrain-t88-t107", false, true, true,
        "82f5551bafa2fbc554ec0634cb6daf4878a19e851691c1b3c5841f09beaba531",
        "023b02c8a0623d05f40c95651bec421b616c92f4d470ac39bf523b2c816a685c"),
    new(
        "no-terrain-t88", false, true, false,
        "4224c2ef96dc8ce1e7d8e629fd51072afc1b78f70f0f7de4f3dcd84e345e8db7",
        "7c5db1f5bd2f186d4847d1ceb22e68168e35715c5c3312a55027f3e0c17c676a"),
    new(
        "no-terrain-t107", false, false, true,
        "30fa4c31c997bd41b93d0d8001bad62eea4c33882a8282504420f409a5b91642",
        "625c4349569dda2a24388fee7da6de2bb3bab7ab7a96e997b75c99b37701009e"),
    new("t25-terrain-both", true, true, true, "ef66ec698c9acccb57c31754985f7f8f42553dfd8cbca6fe3a4d94c11a5d6ea1", "2249e27f53f2f3d26e028ab7a94f47ab3f8aecf1251f6d070fd52b9789df77b2", 25),
    new("t25-terrain-t88", true, true, false, "592f3ab132ed88d33a91419d20664cd1e59004073e6d05ebaf98029d7df04a8a", "b46246d76093727d36e373fac72ffdb5332e5c7310a24a3e4e95dd731f47c764", 25),
    new("t25-terrain-t107", true, false, true, "6016ea37052ca2722f5236b79eceec1e28b0d8f788cf83ceafe5dfb0b9384adc", "98e02f353719003b4491d24e509ff90c7c1e7442597b25c670e9706e5646d14e", 25),
    new("t25-base-both", false, true, true, "3e382861087cdca0d105e25fac5fe10a899bf844eac51ad43eee69f7f29af8d5", "cfd90b4aa81485e9503cf0785d7966040755b1227dddde649777f6859e4d19b5", 25),
    new("t25-base-t88", false, true, false, "d8fcf0cf9f994b094b24631504ed433e8728bb93512c5690ccbd626369849bf9", "35c700e8a4390c027a53c502169bf68e97178f5597407e4dce61800420a29f4b", 25),
    new("t25-base-t107", false, false, true, "d30c63a0f188d5d6fa233813d423f4d9059f77c3d73a2dca3be22f3aa6e4f0f0", "15735dfdd2dda935c963e4f32a59320e3ffcb3a87d3a55a3a3a4d93b887a084f", 25)
];

string repositoryRoot = FindRepositoryRoot(args.ElementAtOrDefault(0));
string fixtureImagePath = Path.Combine(
    repositoryRoot,
    "_local",
    "v5-stone-hill-level-replacement",
    "unused-level-65-display-name",
    "Unused-Level-65-Town-Square-independent-storage-with-Town-Square-display-name-RUNTIME-CANDIDATE.bin");
string fixtureCuePath = Path.ChangeExtension(fixtureImagePath, ".cue");
Require(File.Exists(fixtureImagePath) && File.Exists(fixtureCuePath),
    "The writer smoke is missing its exact locked-base BIN/CUE fixture.");
FileState fixtureImageBefore = SnapshotFile(fixtureImagePath);
FileState fixtureCueBefore = SnapshotFile(fixtureCuePath);

string smokeRoot = Path.Combine(
    Path.GetTempPath(),
    SmokeRootPrefix + Guid.NewGuid().ToString("N"));
string workspaceContainer = Path.Combine(smokeRoot, "workspace-container");
Directory.CreateDirectory(smokeRoot);
int rejects = 0;
int publications = 0;

try
{
    UnusedLevel65BlankLevelLabWorkspacePaths paths =
        UnusedLevel65BlankLevelLabProfileRegistry.CreateWorkspacePaths(
            workspaceContainer);
    Directory.CreateDirectory(paths.LockedBaseDirectoryPath);
    Directory.CreateDirectory(paths.AuthoredEditsDirectoryPath);
    File.Copy(fixtureImagePath, paths.LockedBaseImagePath, overwrite: false);
    string cue = (await File.ReadAllTextAsync(fixtureCuePath, Encoding.ASCII))
        .Replace(
            Path.GetFileName(fixtureImagePath),
            Path.GetFileName(paths.LockedBaseImagePath),
            StringComparison.Ordinal);
    await File.WriteAllTextAsync(
        paths.LockedBaseCuePath,
        cue,
        Encoding.ASCII);
    UnusedLevel65BlankLevelLabManifest manifest =
        UnusedLevel65BlankLevelLabProfileRegistry.CreateManifest(paths);
    await File.WriteAllTextAsync(
        paths.ManifestPath,
        JsonSerializer.Serialize(manifest, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        }) + "\n",
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    Require(
        await UnusedLevel65BlankLevelLabBootstrapper
            .ValidatePublishedWorkspaceAsync(paths) == manifest,
        "The writer smoke workspace did not validate exactly.");

    FileState labImageBefore = SnapshotFile(paths.LockedBaseImagePath);
    FileState labCueBefore = SnapshotFile(paths.LockedBaseCuePath);
    FileState labManifestBefore = SnapshotFile(paths.ManifestPath);
    string documentPath = Path.Combine(
        paths.AuthoredEditsDirectoryPath,
        Id65AuthoringEditorDocumentWorkspaceInspector.DocumentFileName);
    string legacyPath = Path.Combine(
        paths.AuthoredEditsDirectoryPath,
        $"{UnusedLevel65BlankLevelLabProfileRegistry.Key}-terrain-edits.json");

    using (CancellationTokenSource canceled = new())
    {
        canceled.Cancel();
        await ExpectCanceledAsync(
            "pre-cancelled first save",
            async () => _ = await Id65AuthoringEditorDocumentWorkspaceWriter.SaveExactAsync(
                paths,
                manifest,
                Id65AuthoringEditorControlState.CreateDefault(),
                expectedPriorEnvelopeCanonicalSha256: null,
                canceled.Token));
    }
    rejects++;
    Require(!File.Exists(documentPath),
        "A pre-cancelled writer call published an editor document.");

    await File.WriteAllTextAsync(legacyPath, "{", Encoding.UTF8);
    await ExpectRejectedAsync(
        "malformed legacy terrain file",
        async () => _ = await SaveDefaultAsync(paths, manifest, null));
    rejects++;
    File.Delete(legacyPath);

    await File.WriteAllTextAsync(
        legacyPath,
        "{\"editCount\":0,\"edits\":[],\"editCount\":0}",
        Encoding.UTF8);
    await ExpectRejectedAsync(
        "duplicate legacy editCount",
        async () => _ = await SaveDefaultAsync(paths, manifest, null),
        "one exact editCount");
    rejects++;
    File.Delete(legacyPath);

    await File.WriteAllTextAsync(
        legacyPath,
        "{\"editCount\":1,\"edits\":[{}]}",
        Encoding.UTF8);
    await ExpectRejectedAsync(
        "positive legacy terrain edit",
        async () => _ = await SaveDefaultAsync(paths, manifest, null),
        "explicitly converted or removed");
    rejects++;
    File.Delete(legacyPath);

    Directory.CreateDirectory(legacyPath);
    await ExpectRejectedAsync(
        "legacy terrain directory",
        async () => _ = await SaveDefaultAsync(paths, manifest, null),
        "directory or symbolic link");
    rejects++;
    Directory.Delete(legacyPath);

    string symlinkTarget = Path.Combine(
        paths.AuthoredEditsDirectoryPath,
        "legacy-zero-target.json");
    await File.WriteAllTextAsync(
        symlinkTarget,
        "{\"editCount\":0,\"edits\":[]}",
        Encoding.UTF8);
    File.CreateSymbolicLink(legacyPath, symlinkTarget);
    await ExpectRejectedAsync(
        "legacy terrain symbolic link",
        async () => _ = await SaveDefaultAsync(paths, manifest, null),
        "symbolic link");
    rejects++;
    File.Delete(legacyPath);
    File.Delete(symlinkTarget);

    await File.WriteAllBytesAsync(legacyPath, []);
    await ExpectRejectedAsync(
        "empty legacy terrain file",
        async () => _ = await SaveDefaultAsync(paths, manifest, null),
        "empty or exceeds");
    rejects++;
    File.Delete(legacyPath);

    await using (FileStream oversized = new(
                     legacyPath,
                     FileMode.CreateNew,
                     FileAccess.Write,
                     FileShare.None))
    {
        oversized.SetLength(
            MaximumLegacyTerrainEditByteCount + 1L);
    }
    await ExpectRejectedAsync(
        "oversized legacy terrain file",
        async () => _ = await SaveDefaultAsync(paths, manifest, null),
        "empty or exceeds");
    rejects++;
    File.Delete(legacyPath);

    await File.WriteAllTextAsync(
        legacyPath,
        JsonSerializer.Serialize(new
        {
            generatedAt = "writer-smoke",
            editor = "Spyro.Editor.Core",
            levelName = "Unused Level 65 Blank",
            note = "Exact empty legacy layer accepted only as non-conflicting.",
            editCount = 0,
            edits = Array.Empty<object>()
        }) + "\n",
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    byte[] emptyLegacyBytes = await File.ReadAllBytesAsync(legacyPath);
    FileState emptyLegacyBefore = SnapshotFile(legacyPath);

    await ExpectRejectedAsync(
        "nonexistent document with prior identity",
        async () => _ = await SaveDefaultAsync(paths, manifest, new string('0', 64)),
        "is missing");
    rejects++;
    Require(!File.Exists(documentPath),
        "A missing-prior refusal published an editor document.");

    string? expectedPrior = null;
    string? firstEnvelope = null;
    Id65AuthoringEditorDocumentWriteResult? finalResult = null;
    HashSet<string> distinctEnvelopes = new(StringComparer.Ordinal);
    foreach (Case item in cases)
    {
        Id65AuthoringEditorControlState requested = new(
            item.Terrain,
            item.T88,
            item.T107,
            item.TextureId);
        Id65AuthoringEditorDocumentWriteResult result =
            await Id65AuthoringEditorDocumentWorkspaceWriter.SaveExactAsync(
                paths,
                manifest,
                requested,
                expectedPrior);
        publications++;
        AssertResult(result, requested, item, created: expectedPrior == null);
        Require(!ReferenceEquals(result.ControlState, requested),
            $"The {item.Key} receipt retained a caller-owned control-state object.");

        Id65AuthoringEditorDocumentReadModel inspected =
            await Id65AuthoringEditorDocumentWorkspaceInspector.InspectAsync(
                paths,
                manifest)
            ?? throw new InvalidOperationException(
                $"The {item.Key} publication was absent during readback.");
        Id65AuthoringEditorControlState recovered =
            Id65AuthoringEditorControlStateCompiler.RecoverControlState(inspected);
        Require(
            Equivalent(requested, recovered) &&
            inspected.CompositeProfileId == "id65-authoring-single-pass-composite-v3" &&
            inspected.DocumentCanonicalSha256 == result.DocumentCanonicalSha256 &&
            inspected.DocumentContentSha256 == result.DocumentContentSha256 &&
            inspected.EnvelopeCanonicalSha256 == result.EnvelopeCanonicalSha256,
            $"The {item.Key} explicit save did not read back as its exact control state and hashes.");
        Require(
            distinctEnvelopes.Add(result.EnvelopeCanonicalSha256) &&
            SnapshotFile(documentPath).Sha256 == result.EnvelopeCanonicalSha256,
            $"The {item.Key} envelope was reused across distinct states or did not hash to its on-disk bytes.");

        firstEnvelope ??= result.EnvelopeCanonicalSha256;
        expectedPrior = result.EnvelopeCanonicalSha256;
        finalResult = result;
    }

    Id65AuthoringEditorDocumentWriteResult final = finalResult ??
        throw new InvalidOperationException(
            "The writer smoke did not publish its complete state matrix.");
    string initialEnvelope = firstEnvelope ??
        throw new InvalidOperationException(
            "The writer smoke did not retain its first envelope identity.");
    string currentEnvelope = expectedPrior ??
        throw new InvalidOperationException(
            "The writer smoke did not retain its final envelope identity.");
    byte[] stableEnvelope = await File.ReadAllBytesAsync(documentPath);
    Require(
        currentEnvelope == ExpectedLegacyFinalEnvelopeSha256 &&
        SnapshotFile(documentPath).Sha256 == ExpectedLegacyFinalEnvelopeSha256,
        "The legacy schema-1 final envelope bytes/hash changed during schema-3 persistence work.");

    Id65AuthoringEditorDocumentWriteResult deterministic =
        await Id65AuthoringEditorDocumentWorkspaceWriter.SaveExactAsync(
            paths,
            manifest,
            final.ControlState,
            currentEnvelope);
    publications++;
    Require(
        deterministic.ReplacedExistingDocument &&
        deterministic.EnvelopeCanonicalSha256 == currentEnvelope &&
        (await File.ReadAllBytesAsync(documentPath)).SequenceEqual(stableEnvelope),
        "A same-state CAS replacement was not byte-for-byte deterministic.");

    await ExpectRejectedAsync(
        "stale prior identity",
        async () => _ = await SaveDefaultAsync(paths, manifest, initialEnvelope),
        "changed after its prior envelope identity");
    rejects++;
    await ExpectRejectedAsync(
        "create-only existing document",
        async () => _ = await SaveDefaultAsync(paths, manifest, null),
        "already exists");
    rejects++;
    await ExpectRejectedAsync(
        "transient no-placement state",
        async () => _ = await Id65AuthoringEditorDocumentWorkspaceWriter.SaveExactAsync(
            paths,
            manifest,
            new(
                includeStructuralTerrain: true,
                includeGrassAtT88: false,
                includeGrassAtT107: false),
            deterministic.EnvelopeCanonicalSha256),
        "lost an exact choice");
    rejects++;

    await File.WriteAllTextAsync(
        legacyPath,
        "{\"editCount\":1,\"edits\":[{}]}",
        Encoding.UTF8);
    await ExpectRejectedAsync(
        "new legacy conflict after canonical publication",
        async () => _ = await SaveDefaultAsync(
            paths,
            manifest,
            deterministic.EnvelopeCanonicalSha256),
        "explicitly converted or removed");
    rejects++;
    await File.WriteAllBytesAsync(legacyPath, emptyLegacyBytes);
    File.SetLastWriteTimeUtc(legacyPath, emptyLegacyBefore.LastWriteUtc);

    using (CancellationTokenSource canceled = new())
    {
        canceled.Cancel();
        await ExpectCanceledAsync(
            "pre-cancelled replacement",
            async () => _ = await Id65AuthoringEditorDocumentWorkspaceWriter.SaveExactAsync(
                paths,
                manifest,
                Id65AuthoringEditorControlState.CreateDefault(),
                deterministic.EnvelopeCanonicalSha256,
                canceled.Token));
    }
    rejects++;

    Require(
        (await File.ReadAllBytesAsync(documentPath)).SequenceEqual(stableEnvelope) &&
        SnapshotFile(legacyPath) == emptyLegacyBefore,
        "A rejected or cancelled writer call changed the canonical document or empty legacy layer.");

    Id65AuthoringEditorControlState remoteCenter =
        Id65AuthoringEditorControlState.MigrateLegacyToRemotePadGrid(
            final.ControlState,
            Id65RemotePadGrassT107Catalog.CenterPositionId);
    await ExpectRejectedAsync(
        "automatic legacy-to-remote save",
        async () => _ = await Id65AuthoringEditorDocumentWorkspaceWriter.SaveExactAsync(
            paths,
            manifest,
            remoteCenter,
            currentEnvelope),
        "explicit migration API");
    rejects++;
    Require(
        (await File.ReadAllBytesAsync(documentPath)).SequenceEqual(stableEnvelope),
        "A refused automatic legacy-to-remote save changed the legacy envelope.");

    Id65AuthoringEditorControlState changedDuringMigration =
        Id65AuthoringEditorControlState.CreateRemotePad(
            final.ControlState.IncludeStructuralTerrain,
            includeGrassAtT88: true,
            final.ControlState.IncludeGrassAtT107,
            Id65RemotePadGrassT107Catalog.CenterPositionId,
            final.ControlState.MandatoryBaseFaceTextureId);
    await ExpectRejectedAsync(
        "migration that changes legacy toggles",
        async () => _ = await Id65AuthoringEditorDocumentWorkspaceWriter
            .MigrateLegacyToRemotePadGridExactAsync(
                paths,
                manifest,
                changedDuringMigration,
                currentEnvelope),
        "must preserve");
    rejects++;
    await ExpectRejectedAsync(
        "stale explicit legacy-to-remote migration",
        async () => _ = await Id65AuthoringEditorDocumentWorkspaceWriter
            .MigrateLegacyToRemotePadGridExactAsync(
                paths,
                manifest,
                remoteCenter,
                initialEnvelope),
        "changed after its prior envelope identity");
    rejects++;
    Require(
        (await File.ReadAllBytesAsync(documentPath)).SequenceEqual(stableEnvelope),
        "A rejected explicit migration changed the legacy envelope.");

    Id65AuthoringEditorDocumentWriteResult migrated =
        await Id65AuthoringEditorDocumentWorkspaceWriter
            .MigrateLegacyToRemotePadGridExactAsync(
                paths,
                manifest,
                remoteCenter,
                currentEnvelope);
    publications++;
    AssertRemoteResult(migrated, remoteCenter, explicitMigration: true);
    Require(
        !ReferenceEquals(migrated.ControlState, remoteCenter) &&
        migrated.ReplacedExistingDocument &&
        migrated.Document.T107RemotePadPositionId ==
            Id65RemotePadGrassT107Catalog.CenterPositionId,
        "The explicit migration receipt did not copy and retain the caller-supplied Center state.");
    currentEnvelope = migrated.EnvelopeCanonicalSha256;

    Id65AuthoringEditorControlState remoteOff =
        Id65AuthoringEditorControlState.CreateRemotePad(
            includeStructuralTerrain: false,
            includeGrassAtT88: false,
            includeGrassAtT107: false,
            Id65RemotePadGrassT107Catalog.SouthEastPositionId,
            mandatoryBaseFaceTextureId: 25);
    Id65AuthoringEditorDocumentWriteResult offResult =
        await Id65AuthoringEditorDocumentWorkspaceWriter.SaveExactAsync(
            paths,
            manifest,
            remoteOff,
            currentEnvelope);
    publications++;
    AssertRemoteResult(offResult, remoteOff, explicitMigration: false);
    currentEnvelope = offResult.EnvelopeCanonicalSha256;

    Id65AuthoringEditorControlState remoteOn =
        Id65AuthoringEditorControlState.CreateRemotePad(
            includeStructuralTerrain: false,
            includeGrassAtT88: false,
            includeGrassAtT107: true,
            Id65RemotePadGrassT107Catalog.SouthEastPositionId,
            mandatoryBaseFaceTextureId: 25);
    Id65AuthoringEditorDocumentWriteResult remoteFinal =
        await Id65AuthoringEditorDocumentWorkspaceWriter.SaveExactAsync(
            paths,
            manifest,
            remoteOn,
            currentEnvelope);
    publications++;
    AssertRemoteResult(remoteFinal, remoteOn, explicitMigration: false);
    currentEnvelope = remoteFinal.EnvelopeCanonicalSha256;
    byte[] stableRemoteEnvelope = await File.ReadAllBytesAsync(documentPath);
    Id65AuthoringEditorDocumentReadModel inspectedRemote =
        await Id65AuthoringEditorDocumentWorkspaceInspector.InspectAsync(paths, manifest)
        ?? throw new InvalidOperationException("The remote writer result inspected as absent.");
    Require(
        Equivalent(remoteOn, remoteFinal.ControlState) &&
        Equivalent(remoteOn,
            Id65AuthoringEditorControlStateCompiler.RecoverControlState(inspectedRemote)) &&
        inspectedRemote.T107RemotePadPositionId ==
            Id65RemotePadGrassT107Catalog.SouthEastPositionId &&
        inspectedRemote.MobyPlacements.Count == 1,
        "The public writer/readback path did not preserve dormant-off then active-on remote position semantics.");

    await ExpectRejectedAsync(
        "stale remote-grid CAS",
        async () => _ = await Id65AuthoringEditorDocumentWorkspaceWriter.SaveExactAsync(
            paths,
            manifest,
            remoteOff,
            migrated.EnvelopeCanonicalSha256),
        "changed after its prior envelope identity");
    rejects++;
    await ExpectRejectedAsync(
        "automatic remote-to-legacy save",
        async () => _ = await Id65AuthoringEditorDocumentWorkspaceWriter.SaveExactAsync(
            paths,
            manifest,
            final.ControlState,
            currentEnvelope),
        "explicit migration API");
    rejects++;
    await ExpectRejectedAsync(
        "second explicit migration from remote state",
        async () => _ = await Id65AuthoringEditorDocumentWorkspaceWriter
            .MigrateLegacyToRemotePadGridExactAsync(
                paths,
                manifest,
                remoteOn,
                currentEnvelope),
        "only legacy native-apron");
    rejects++;
    Require(
        (await File.ReadAllBytesAsync(documentPath)).SequenceEqual(stableRemoteEnvelope),
        "A stale, reverse, or repeated migration attempt mutated the remote envelope.");
    File.Delete(documentPath);
    Id65AuthoringEditorControlState freshRemoteState =
        Id65AuthoringEditorControlState.CreateRemotePadDefault();
    string parkedBin = Path.Combine(smokeRoot, "parked-locked-base.bin");
    string parkedCue = Path.Combine(smokeRoot, "parked-locked-base.cue");
    File.Move(paths.LockedBaseImagePath, parkedBin);
    File.Move(paths.LockedBaseCuePath, parkedCue);
    Id65AuthoringEditorDocumentWriteResult freshRemote;
    Id65AuthoringEditorDocumentWriteResult freshRemoteUpdate;
    try
    {
        freshRemote =
            await Id65AuthoringEditorDocumentWorkspaceWriter.SaveExactAsync(
                paths,
                manifest,
                freshRemoteState,
                expectedPriorEnvelopeCanonicalSha256: null);
        publications++;
        AssertRemoteResult(freshRemote, freshRemoteState, explicitMigration: false);

        Id65AuthoringEditorControlState freshRemoteUpdatedState =
            Id65AuthoringEditorControlState.CreateRemotePad(
                includeStructuralTerrain: true,
                includeGrassAtT88: false,
                includeGrassAtT107: true,
                Id65RemotePadGrassT107Catalog.WestPositionId);
        freshRemoteUpdate =
            await Id65AuthoringEditorDocumentWorkspaceWriter.SaveExactAsync(
                paths,
                manifest,
                freshRemoteUpdatedState,
                freshRemote.EnvelopeCanonicalSha256);
        publications++;
        AssertRemoteResult(
            freshRemoteUpdate,
            freshRemoteUpdatedState,
            explicitMigration: false);
    }
    finally
    {
        File.Move(parkedBin, paths.LockedBaseImagePath);
        File.Move(parkedCue, paths.LockedBaseCuePath);
    }
    Require(
        freshRemote.CreatedNewDocument && !freshRemote.ReplacedExistingDocument &&
        freshRemote.ControlState.T107RemotePadPositionId ==
            Id65RemotePadGrassT107Catalog.CenterPositionId &&
        !freshRemote.ControlState.IncludeGrassAtT107,
        "A genuinely fresh schema-3 writer create did not preserve its dormant Center default.");
    Require(
        freshRemoteUpdate.ReplacedExistingDocument &&
        freshRemoteUpdate.ControlState.T107RemotePadPositionId ==
            Id65RemotePadGrassT107Catalog.WestPositionId &&
        freshRemoteUpdate.ControlState.IncludeGrassAtT107 &&
        !File.Exists(parkedBin) && !File.Exists(parkedCue),
        "A schema-3 create/update depended on or recreated the parked native BIN/CUE inputs.");
    Require(
        SnapshotFile(paths.LockedBaseImagePath) == labImageBefore &&
        SnapshotFile(paths.LockedBaseCuePath) == labCueBefore &&
        SnapshotFile(paths.ManifestPath) == labManifestBefore,
        "The workspace writer changed its locked BIN, CUE, or manifest.");
    Require(
        !Directory.EnumerateFiles(
                paths.AuthoredEditsDirectoryPath,
                "*.tmp",
                SearchOption.TopDirectoryOnly)
            .Any(),
        "The workspace writer retained a temporary publication file.");

    AssertPublicSurface();
    AssertSourceTopology(repositoryRoot);
    Require(
        SnapshotFile(fixtureImagePath) == fixtureImageBefore &&
        SnapshotFile(fixtureCuePath) == fixtureCueBefore,
        "The writer smoke changed its external locked-base fixture.");

    Console.WriteLine(
        $"ID65_AUTHORING_EDITOR_DOCUMENT_WORKSPACE_WRITER_SMOKE_OK " +
        $"states={cases.Length} publications={publications} rejects={rejects} " +
        $"finalDocument={final.DocumentCanonicalSha256} " +
        $"finalEnvelope={final.EnvelopeCanonicalSha256} " +
        $"remoteFinalEnvelope={remoteFinal.EnvelopeCanonicalSha256} " +
        $"freshRemoteEnvelope={freshRemote.EnvelopeCanonicalSha256} " +
        $"freshRemoteUpdateEnvelope={freshRemoteUpdate.EnvelopeCanonicalSha256} " +
        "legacy bytes pinned; explicit one-way remote migration, native-input-free fresh/remote updates, dormant off/on position retention, stale/reverse CAS refusal, path-free receipts, immutable locked inputs, and no aggregate/App/Create-BIN/runtime/release authority passed.");
}
finally
{
    if (Directory.Exists(smokeRoot) &&
        Path.GetFileName(smokeRoot).StartsWith(
            SmokeRootPrefix,
            StringComparison.Ordinal))
    {
        Directory.Delete(smokeRoot, recursive: true);
    }
    Require(!Directory.Exists(smokeRoot),
        "The writer smoke retained temporary game data.");
}

static Task<Id65AuthoringEditorDocumentWriteResult> SaveDefaultAsync(
    UnusedLevel65BlankLevelLabWorkspacePaths paths,
    UnusedLevel65BlankLevelLabManifest manifest,
    string? expectedPrior) =>
    Id65AuthoringEditorDocumentWorkspaceWriter.SaveExactAsync(
        paths,
        manifest,
        Id65AuthoringEditorControlState.CreateDefault(),
        expectedPrior);

static void AssertResult(
    Id65AuthoringEditorDocumentWriteResult result,
    Id65AuthoringEditorControlState requested,
    Case item,
    bool created)
{
    Require(
        Equivalent(result.ControlState, requested) &&
        result.DocumentCanonicalSha256 == item.ExpectedCanonicalSha256 &&
        result.DocumentContentSha256 == item.ExpectedContentSha256 &&
        result.CreatedNewDocument == created &&
        result.ReplacedExistingDocument != created &&
        !result.ExplicitLegacyToRemotePadMigrationPerformed &&
        !result.ExplicitRemotePadMobyChoiceMigrationPerformed &&
        !result.ExplicitRemotePadTerrainSetMigrationPerformed &&
        !result.ExplicitBlankCycloramaMigrationPerformed &&
        !result.AutomaticMigrationPerformed &&
        result.ExplicitSaveInvocationRequired &&
        result.LegacyTerrainLayerConflictChecked &&
        result.ExactControlStateCompiled &&
        result.ProviderOwnedNativeInputGenerationValidatedDuringWrite &&
        result.LockedSourceRevalidatedDuringWrite &&
        result.CurrentProjectionRevalidatedDuringWrite &&
        !result.StaticAuthoringProjectionValidatedDuringWrite &&
        result.LegacyPriorCompositeProjectionValidatedDuringWrite == !created &&
        result.CompareAndSwapEnforced &&
        result.AtomicPublicationCompleted &&
        result.PersistencePerformed &&
        result.FileSystemMutationPerformed &&
        result.WritesFileSystem &&
        !result.ContainsFilePaths && !result.ContainsGameBytes &&
        !result.ContainsNativePayloadBytes && !result.ContainsPhysicalAddresses &&
        !result.ContainsCompiledAfterimage && !result.CarriesAppOrUiAuthority &&
        !result.WritesDiscImage && !result.WritesCue &&
        !result.AggregateMaterializationEnabled && !result.CreateBinEnabled &&
        !result.NormalCreateBinEnabled && !result.RuntimeCandidateAuthorized &&
        !result.RuntimeProven && !result.ReleaseIntegrated &&
        !result.PromotionAuthorized && !result.Publishable &&
        result.RequiresTrustedExclusiveWorkspaceDirectory &&
        !result.HostileFilesystemRaceSafe && !result.CommonWorkspaceLeaseOwned &&
        !result.Document.CurrentProjectionRevalidated &&
        !result.Document.CurrentLockedSourceReprojected &&
        !result.Document.CurrentOnDiskFreshnessVerified &&
        result.Document.HasCompiledCompositeProjection &&
        !result.Document.HasStaticAuthoringProjection,
        $"The {item.Key} write receipt lost an exact identity or fail-closed boundary.");
}

static void AssertRemoteResult(
    Id65AuthoringEditorDocumentWriteResult result,
    Id65AuthoringEditorControlState requested,
    bool explicitMigration)
{
    Require(
        Equivalent(result.ControlState, requested) &&
        result.Document.SchemaVersion == 3 &&
        result.Document.ReadModelSchemaVersion ==
            Id65AuthoringEditorDocumentReadModel.RemotePadGridReadModelSchemaVersion &&
        result.Document.EnvelopeSchemaVersion == 2 &&
        result.Document.ProjectionContractId ==
            "id65-authoring-editor-document-projector-v2" &&
        result.Document.StaticAuthoringProfileId ==
            "id65-authoring-editor-document-remote-pad-static-projection-v1" &&
        result.Document.StaticAuthoringCatalogSha256 ==
            Id65RemotePadGrassT107Catalog.CanonicalSha256 &&
        result.Document.StaticAuthoringSceneGraphSha256.Length == 64 &&
        result.Document.StaticAuthoringProjectionSha256.Length == 64 &&
        result.Document.CompositeManifestSha256.Length == 0 &&
        result.Document.CompositeOutputRow80Sha256.Length == 0 &&
        result.Document.CompositeDeterministicPlanSha256.Length == 0 &&
        result.Document.HasStaticAuthoringProjection &&
        !result.Document.HasCompiledCompositeProjection &&
        result.ExplicitLegacyToRemotePadMigrationPerformed == explicitMigration &&
        !result.ExplicitRemotePadMobyChoiceMigrationPerformed &&
        !result.ExplicitRemotePadTerrainSetMigrationPerformed &&
        !result.ExplicitBlankCycloramaMigrationPerformed &&
        !result.AutomaticMigrationPerformed &&
        result.ExplicitSaveInvocationRequired &&
        result.LegacyTerrainLayerConflictChecked &&
        result.ExactControlStateCompiled &&
        result.ProviderOwnedNativeInputGenerationValidatedDuringWrite ==
            explicitMigration &&
        result.LockedSourceRevalidatedDuringWrite == explicitMigration &&
        result.CurrentProjectionRevalidatedDuringWrite &&
        result.StaticAuthoringProjectionValidatedDuringWrite &&
        result.LegacyPriorCompositeProjectionValidatedDuringWrite ==
            explicitMigration &&
        result.CompareAndSwapEnforced && result.AtomicPublicationCompleted &&
        result.PersistencePerformed && result.FileSystemMutationPerformed &&
        result.WritesFileSystem && !result.ContainsFilePaths &&
        !result.ContainsGameBytes && !result.ContainsNativePayloadBytes &&
        !result.ContainsPhysicalAddresses && !result.ContainsCompiledAfterimage &&
        !result.CarriesAppOrUiAuthority && !result.WritesDiscImage &&
        !result.WritesCue && !result.AggregateMaterializationEnabled &&
        !result.CreateBinEnabled && !result.NormalCreateBinEnabled &&
        !result.RuntimeCandidateAuthorized && !result.RuntimeProven &&
        !result.ReleaseIntegrated && !result.PromotionAuthorized &&
        !result.Publishable && result.RequiresTrustedExclusiveWorkspaceDirectory &&
        !result.HostileFilesystemRaceSafe && !result.CommonWorkspaceLeaseOwned,
        "A schema-3 writer receipt lost its exact static-authoring identity or fail-closed boundary.");
}

static void AssertPublicSurface()
{
    Type writer = typeof(Id65AuthoringEditorDocumentWorkspaceWriter);
    Type result = typeof(Id65AuthoringEditorDocumentWriteResult);
    Type implementation = writer.GetNestedType(
        "WorkspaceWriteResult",
        BindingFlags.NonPublic)
        ?? throw new InvalidOperationException(
            "The private writer-owned result implementation is missing.");
    Require(
        writer.IsPublic && writer.IsAbstract && writer.IsSealed &&
        writer.GetFields(BindingFlags.Static | BindingFlags.Public |
            BindingFlags.DeclaredOnly).Length == 0 &&
        result.IsPublic && result.IsAbstract &&
        implementation.IsNestedPrivate && implementation.IsSealed &&
        implementation.BaseType == result &&
        result.Assembly.GetTypes()
            .Where(type => type != result && result.IsAssignableFrom(type))
            .SequenceEqual([implementation]),
        "The writer or writer-owned receipt shell lost its sealed topology.");
    ConstructorInfo[] resultConstructors = result.GetConstructors(
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    Require(
        resultConstructors.Length == 1 &&
        resultConstructors[0].IsFamilyAndAssembly,
        "The public write receipt can be directly constructed outside Core.");

    MethodInfo[] methods = writer.GetMethods(
        BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly);
    Type[] exactParameters =
    [
        typeof(UnusedLevel65BlankLevelLabWorkspacePaths),
        typeof(UnusedLevel65BlankLevelLabManifest),
        typeof(Id65AuthoringEditorControlState),
        typeof(string),
        typeof(CancellationToken)
    ];
    MethodInfo? save = methods.SingleOrDefault(method => method.Name == "SaveExactAsync");
    MethodInfo? migrate = methods.SingleOrDefault(method =>
        method.Name == "MigrateLegacyToRemotePadGridExactAsync");
    MethodInfo? migrateMobyChoice = methods.SingleOrDefault(method =>
        method.Name == "MigrateRemotePadGridToMobyChoiceExactAsync");
    MethodInfo? migrateTerrainSet = methods.SingleOrDefault(method =>
        method.Name == "MigrateRemotePadMobyChoiceToTerrainSetExactAsync");
    MethodInfo? migrateBlankCyclorama = methods.SingleOrDefault(method =>
        method.Name ==
            "MigrateRemotePadMobyChoiceTerrainSetToBlankCycloramaExactAsync");
    Require(
        methods.Length == 5 && save != null && migrate != null &&
        migrateMobyChoice != null && migrateTerrainSet != null &&
        migrateBlankCyclorama != null &&
        save.ReturnType == typeof(Task<Id65AuthoringEditorDocumentWriteResult>) &&
        migrate.ReturnType == typeof(Task<Id65AuthoringEditorDocumentWriteResult>) &&
        migrateMobyChoice.ReturnType ==
            typeof(Task<Id65AuthoringEditorDocumentWriteResult>) &&
        migrateTerrainSet.ReturnType ==
            typeof(Task<Id65AuthoringEditorDocumentWriteResult>) &&
        migrateBlankCyclorama.ReturnType ==
            typeof(Task<Id65AuthoringEditorDocumentWriteResult>) &&
        save.GetParameters().Select(parameter => parameter.ParameterType)
            .SequenceEqual(exactParameters) &&
        migrate.GetParameters().Select(parameter => parameter.ParameterType)
            .SequenceEqual(exactParameters) &&
        migrateMobyChoice.GetParameters().Select(parameter => parameter.ParameterType)
            .SequenceEqual(exactParameters) &&
        migrateTerrainSet.GetParameters().Select(parameter => parameter.ParameterType)
            .SequenceEqual(exactParameters) &&
        migrateBlankCyclorama.GetParameters()
            .Select(parameter => parameter.ParameterType)
            .SequenceEqual(exactParameters),
        "The writer lost its normal-save plus four explicit migration surfaces or exposed arbitrary bytes/paths/delegates.");
    Require(
        result.GetProperties(BindingFlags.Instance | BindingFlags.Public).All(property =>
            !(property.PropertyType == typeof(string) &&
              property.Name.Contains("Path", StringComparison.OrdinalIgnoreCase)) &&
            property.PropertyType != typeof(byte[]) &&
            !typeof(Stream).IsAssignableFrom(property.PropertyType)),
        "The public write receipt exposes a path or game/native byte payload.");
}

static void AssertSourceTopology(string repositoryRoot)
{
    string coreRoot = Path.Combine(repositoryRoot, "src", "Spyro.Editor.Core");
    string appRoot = Path.Combine(repositoryRoot, "src", "Spyro.Editor.App");
    string[] productionFiles = new[] { coreRoot, appRoot }
        .SelectMany(root => Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        .Where(path => !HasPathSegment(path, "bin") && !HasPathSegment(path, "obj"))
        .ToArray();
    Regex storeSave = new(
        @"\bId65AuthoringEditorDocumentStore\s*\.\s*SaveAsync\s*\(",
        RegexOptions.CultureInvariant);
    string[] saveOwners = productionFiles
        .Where(path => storeSave.IsMatch(File.ReadAllText(path)))
        .Select(path => Path.GetRelativePath(repositoryRoot, path))
        .Order(StringComparer.Ordinal)
        .ToArray();
    Regex preparedSave = new(
        @"\bId65AuthoringEditorDocumentStore\s*\.\s*SavePreparedAsync\s*\(",
        RegexOptions.CultureInvariant);
    string[] preparedSaveOwners = productionFiles
        .Where(path => preparedSave.IsMatch(File.ReadAllText(path)))
        .Select(path => Path.GetRelativePath(repositoryRoot, path))
        .Order(StringComparer.Ordinal)
        .ToArray();
    Regex migrationSave = new(
        @"\bId65AuthoringEditorDocumentStore\s*\.\s*MigratePreparedAsync\s*\(",
        RegexOptions.CultureInvariant);
    string[] migrationSaveOwners = productionFiles
        .Where(path => migrationSave.IsMatch(File.ReadAllText(path)))
        .Select(path => Path.GetRelativePath(repositoryRoot, path))
        .Order(StringComparer.Ordinal)
        .ToArray();
    string expectedWriter = Path.Combine(
        "src",
        "Spyro.Editor.Core",
        "Persistence",
        "Id65AuthoringEditorDocumentWorkspaceWriter.cs");
    string writerSource = File.ReadAllText(
        Path.Combine(repositoryRoot, expectedWriter));
    string[] preparedSymbolOwners = productionFiles
        .Where(path => File.ReadAllText(path).Contains(
            "SavePreparedAsync",
            StringComparison.Ordinal))
        .Select(path => Path.GetRelativePath(repositoryRoot, path))
        .Order(StringComparer.Ordinal)
        .ToArray();
    string[] migrationSymbolOwners = productionFiles
        .Where(path => File.ReadAllText(path).Contains(
            "MigratePreparedAsync",
            StringComparison.Ordinal))
        .Select(path => Path.GetRelativePath(repositoryRoot, path))
        .Order(StringComparer.Ordinal)
        .ToArray();
    string expectedStore = Path.Combine(
        "src",
        "Spyro.Editor.Core",
        "Persistence",
        "Id65AuthoringEditorDocumentStore.cs");
    string expectedAppOwner = Path.Combine(
        "src",
        "Spyro.Editor.App",
        "Views",
        "MainWindow.Id65BlankLab.cs");
    string[] appWriterOwners = Directory
        .EnumerateFiles(appRoot, "*.cs", SearchOption.AllDirectories)
        .Where(path => !HasPathSegment(path, "bin") && !HasPathSegment(path, "obj"))
        .Where(path => File.ReadAllText(path).Contains(
            "Id65AuthoringEditorDocumentWorkspaceWriter",
            StringComparison.Ordinal))
        .Select(path => Path.GetRelativePath(repositoryRoot, path))
        .Order(StringComparer.Ordinal)
        .ToArray();
    Regex publicWriterCall = new(
        @"\bId65AuthoringEditorDocumentWorkspaceWriter\s*\.\s*SaveExactAsync\s*\(",
        RegexOptions.CultureInvariant);
    Require(
        saveOwners.Length == 0 &&
        preparedSaveOwners.SequenceEqual([expectedWriter]) &&
        migrationSaveOwners.SequenceEqual([expectedWriter]) &&
        preparedSymbolOwners.SequenceEqual(
            new[] { expectedStore, expectedWriter }.Order(StringComparer.Ordinal)) &&
        migrationSymbolOwners.SequenceEqual(
            new[] { expectedStore, expectedWriter }.Order(StringComparer.Ordinal)) &&
        Regex.Matches(
                writerSource,
                @"await\s+RequireNoConflictingLegacyTerrainLayerAsync\s*\(",
                RegexOptions.CultureInvariant)
            .Count == 2 &&
        appWriterOwners.SequenceEqual([expectedAppOwner]) &&
        publicWriterCall.Matches(File.ReadAllText(
            Path.Combine(repositoryRoot, expectedAppOwner))).Count == 6,
        "The prepared Store publication owner, two-phase legacy check, or explicit App writer boundary changed.");
}

static async Task ExpectRejectedAsync(
    string label,
    Func<Task> action,
    string? requiredText = null)
{
    try
    {
        await action();
        throw new InvalidOperationException($"Expected {label} to be rejected.");
    }
    catch (Exception exception) when (
        exception is not InvalidOperationException ||
        !exception.Message.StartsWith("Expected ", StringComparison.Ordinal))
    {
        if (requiredText != null &&
            !exception.ToString().Contains(
                requiredText,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"The {label} rejection did not mention '{requiredText}': {exception}");
        }
    }
}

static async Task ExpectCanceledAsync(string label, Func<Task> action)
{
    try
    {
        await action();
        throw new InvalidOperationException($"Expected {label} to be canceled.");
    }
    catch (OperationCanceledException)
    {
    }
}

static bool Equivalent(
    Id65AuthoringEditorControlState left,
    Id65AuthoringEditorControlState right) =>
    left.IncludeStructuralTerrain == right.IncludeStructuralTerrain &&
    left.IncludeTerrainTile2 == right.IncludeTerrainTile2 &&
    left.IncludeGrassAtT88 == right.IncludeGrassAtT88 &&
    left.IncludeGrassAtT107 == right.IncludeGrassAtT107 &&
    left.MandatoryBaseFaceTextureId == right.MandatoryBaseFaceTextureId &&
    left.T107PlacementSemantics == right.T107PlacementSemantics &&
    left.T107RemotePadPositionId == right.T107RemotePadPositionId &&
    left.RemotePadMobyKind == right.RemotePadMobyKind &&
    left.RemotePadMobyPositionId == right.RemotePadMobyPositionId &&
    left.TerrainSelectionSemantics == right.TerrainSelectionSemantics &&
    left.UsesBlankCyclorama == right.UsesBlankCyclorama &&
    left.BlankCycloramaPresetId == right.BlankCycloramaPresetId &&
    left.BlankCycloramaColorHex == right.BlankCycloramaColorHex;

static bool HasPathSegment(string path, string segment) =>
    path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        .Contains(segment, StringComparer.OrdinalIgnoreCase);

static FileState SnapshotFile(string path)
{
    FileInfo file = new(path);
    using FileStream stream = File.Open(
        path,
        FileMode.Open,
        FileAccess.Read,
        FileShare.Read);
    return new(
        file.Length,
        file.LastWriteTimeUtc,
        Convert.ToHexStringLower(SHA256.HashData(stream)));
}

static string FindRepositoryRoot(string? start)
{
    DirectoryInfo? current = new(Path.GetFullPath(
        start ?? Directory.GetCurrentDirectory()));
    while (current != null)
    {
        if (File.Exists(Path.Combine(current.FullName, "global.json")) &&
            Directory.Exists(Path.Combine(
                current.FullName,
                "src",
                "Spyro.Editor.Core")))
        {
            return current.FullName;
        }
        current = current.Parent;
    }
    throw new DirectoryNotFoundException(
        "Could not locate the Spyro Editor repository root.");
}

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

internal sealed record Case(
    string Key,
    bool Terrain,
    bool T88,
    bool T107,
    string ExpectedCanonicalSha256,
    string ExpectedContentSha256,
    int TextureId = 66);

internal sealed record FileState(
    long ByteLength,
    DateTime LastWriteUtc,
    string Sha256);
