using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

const string ExpectedCleanImageSha256 =
    "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37";
const string WriterLeaseFileName =
    ".unusedlevel65blank-authoring-document-v1.json.writer.lock";
const string SmokeRootPrefix =
    "Spyro.Editor.Id65AuthoringEditorDocumentPersistenceSmoke-";

string repositoryRoot = FindRepositoryRoot(args.ElementAtOrDefault(0));
string cleanImagePath = Path.Combine(repositoryRoot, "Spyro the Dragon (USA).bin");
string cleanCuePath = Path.Combine(repositoryRoot, "Spyro the Dragon (USA).cue");
string catalogPath = Path.Combine(repositoryRoot, "spyro-level-catalog.json");

foreach (string requiredPath in new[]
         {
             cleanImagePath,
             cleanCuePath,
             catalogPath
         })
{
    Require(File.Exists(requiredPath), $"Missing persistence-smoke fixture: {requiredPath}");
}

FileState cleanImageBefore = SnapshotFile(cleanImagePath);
FileState cleanCueBefore = SnapshotFile(cleanCuePath);
FileState catalogBefore = SnapshotFile(catalogPath);
Require(
    cleanImageBefore.Sha256 == ExpectedCleanImageSha256,
    "The persistence smoke is not bound to the exact clean USA source image.");

Id65AuthoringEditorDocument fullDocument =
    Id65AuthoringEditorDocument.CreateExact(includeOptionalTerrain: true);
Id65AuthoringEditorDocument noTerrainDocument =
    Id65AuthoringEditorDocument.CreateExact(includeOptionalTerrain: false);
Id65AuthoringEditorDocument residentT25Document =
    Id65AuthoringEditorDocument.CreateExact(mandatoryBaseFaceTextureId: 25);

string smokeRoot = Path.Combine(Path.GetTempPath(), $"{SmokeRootPrefix}{Guid.NewGuid():N}");
string workspaceContainer = Path.Combine(smokeRoot, "workspace-container");
Directory.CreateDirectory(smokeRoot);

try
{
    UnusedLevel65BlankLevelLabBootstrapResult bootstrap =
        await UnusedLevel65BlankLevelLabBootstrapper.BootstrapAsync(new(
            cleanImagePath,
            cleanCuePath,
            workspaceContainer));
    Require(
        bootstrap.AtomicPublicationCompleted &&
        bootstrap.SourceImagePreserved &&
        bootstrap.OwnedTemporaryIntermediatesRemoved &&
        !bootstrap.ReusedExistingLockedBase,
        "The isolated persistence-smoke lab did not bootstrap atomically from the exact source.");

    UnusedLevel65BlankLevelLabWorkspacePaths paths = bootstrap.Paths;
    UnusedLevel65BlankLevelLabManifest manifest = bootstrap.Manifest;
    Id65AuthoringEditorNativeInputProvider.InputSet nativeInputs =
        await Id65AuthoringEditorNativeInputProvider.LoadAsync(paths, manifest);
    Id65AuthoringEditorDocumentProjectionInputs projectionInputs = new(nativeInputs);
    FileState labImageBefore = SnapshotFile(paths.LockedBaseImagePath);
    FileState labCueBefore = SnapshotFile(paths.LockedBaseCuePath);
    FileState labManifestBefore = SnapshotFile(paths.ManifestPath);
    string documentPath = Id65AuthoringEditorDocumentStore.GetPath(paths);
    string authoredRoot = paths.AuthoredEditsDirectoryPath;
    string writerLeasePath = Path.Combine(authoredRoot, WriterLeaseFileName);

    Require(!File.Exists(documentPath), "The new isolated lab unexpectedly contains an editor document.");
    if (Directory.Exists(authoredRoot))
    {
        Require(
            !Directory.EnumerateFileSystemEntries(authoredRoot).Any(),
            "The new isolated lab authored-edits directory is not empty.");
        Directory.Delete(authoredRoot);
    }

    using (CancellationTokenSource canceled = new())
    {
        canceled.Cancel();
        await ExpectCanceledAsync(
            "pre-cancelled missing load",
            async () => _ = await Id65AuthoringEditorDocumentStore.LoadAsync(
                paths,
                manifest,
                projectionInputs,
                canceled.Token));
        await ExpectCanceledAsync(
            "pre-cancelled create",
            async () => _ = await Id65AuthoringEditorDocumentStore.SaveAsync(
                paths,
                manifest,
                fullDocument,
                projectionInputs,
                expectedPriorEnvelopeCanonicalSha256: null,
                canceled.Token));
    }
    Require(
        !Directory.Exists(authoredRoot),
        "A pre-cancelled persistence call created the authored-edits directory.");

    Id65AuthoringEditorDocumentStoreResult? missing =
        await Id65AuthoringEditorDocumentStore.LoadAsync(
            paths,
            manifest,
            projectionInputs);
    Require(missing is null, "A missing editor document did not load as absent.");
    Require(
        !Directory.Exists(authoredRoot),
        "Loading a missing editor document created its authored-edits directory.");
    string[] beforeT25Refusal = Directory.EnumerateFiles(
            paths.RootPath, "*", SearchOption.AllDirectories)
        .Order(StringComparer.Ordinal)
        .Select(path => $"{Path.GetRelativePath(paths.RootPath, path)}:{SnapshotFile(path).Sha256}")
        .ToArray();
    await ExpectRejectedAsync(
        "resident T25 before frozen projection",
        async () => _ = await Id65AuthoringEditorDocumentStore.SaveAsync(
            paths,
            manifest,
            residentT25Document,
            projectionInputs,
            expectedPriorEnvelopeCanonicalSha256: null),
        "cannot be persisted");
    Require(
        Directory.EnumerateFiles(paths.RootPath, "*", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .Select(path => $"{Path.GetRelativePath(paths.RootPath, path)}:{SnapshotFile(path).Sha256}")
            .SequenceEqual(beforeT25Refusal) &&
        !Directory.Exists(authoredRoot),
        "Rejected resident T25 persistence created or mutated workspace bytes.");

    Id65AuthoringEditorNativeInputProvider.InputSet forgedCanonicalInputs =
        ForgeProviderInputSetForNegativeTest(
            nativeInputs.BoundSource,
            nativeInputs.PrivateTextureWitness,
            nativeInputs.GrassBundle,
            new string('0', 64));
    Id65AuthoringEditorDocumentProjectionInputs forgedProjectionInputs =
        new(forgedCanonicalInputs);
    await ExpectRejectedAsync(
        "missing load with forged native-input canonical identity",
        async () => _ = await Id65AuthoringEditorDocumentStore.LoadAsync(
            paths,
            manifest,
            forgedProjectionInputs),
        requiredText: "provider-owned input generation");
    await ExpectRejectedAsync(
        "forged native-input canonical identity",
        async () => _ = await Id65AuthoringEditorDocumentStore.SaveAsync(
            paths,
            manifest,
            fullDocument,
            forgedProjectionInputs,
            expectedPriorEnvelopeCanonicalSha256: null),
        requiredText: "provider-owned input generation");
    Require(
        !Directory.Exists(authoredRoot),
        "A forged native-input identity created the authored-edits directory.");

    Directory.CreateDirectory(authoredRoot);
    byte[] persistentLeaseSentinel = Encoding.UTF8.GetBytes("persistent-crash-residue\n");
    await File.WriteAllBytesAsync(writerLeasePath, persistentLeaseSentinel);
    string unknownReservedPath = Path.Combine(
        authoredRoot,
        $".{Id65AuthoringEditorDocumentStore.FileName}.not-an-owned-identity.tmp");
    byte[] unknownReservedSentinel = Encoding.UTF8.GetBytes("do-not-delete\n");
    await File.WriteAllBytesAsync(unknownReservedPath, unknownReservedSentinel);
    await ExpectRejectedAsync(
        "unknown reserved temp name",
        async () => _ = await Id65AuthoringEditorDocumentStore.SaveAsync(
            paths,
            manifest,
            fullDocument,
            projectionInputs,
            expectedPriorEnvelopeCanonicalSha256: null),
        requiredText: "unknown reserved entry");
    Require(
        File.Exists(unknownReservedPath) &&
        (await File.ReadAllBytesAsync(unknownReservedPath)).SequenceEqual(unknownReservedSentinel) &&
        !File.Exists(documentPath),
        "Unknown reserved persistence residue was deleted or publication continued.");
    File.Delete(unknownReservedPath);

    await ExpectRejectedAsync(
        "null prepared-result callback",
        async () => _ = await Id65AuthoringEditorDocumentStore
            .SavePreparedAsync<object>(
                paths,
                manifest,
                fullDocument,
                projectionInputs,
                preparePublishedResult: null!,
                expectedPriorEnvelopeCanonicalSha256: null),
        requiredText: "preparePublishedResult");
    await ExpectRejectedAsync(
        "null prepared result",
        async () => _ = await Id65AuthoringEditorDocumentStore
            .SavePreparedAsync<object?>(
                paths,
                manifest,
                fullDocument,
                projectionInputs,
                _ => null,
                expectedPriorEnvelopeCanonicalSha256: null),
        requiredText: "returned null");
    int throwingCreatePreparationCount = 0;
    await ExpectRejectedAsync(
        "throwing create preparation",
        async () => _ = await Id65AuthoringEditorDocumentStore
            .SavePreparedAsync<object>(
                paths,
                manifest,
                fullDocument,
                projectionInputs,
                staged =>
                {
                    throwingCreatePreparationCount++;
                    AssertClosedResult(staged, fullDocument);
                    throw new InvalidDataException(
                        "Injected prepared create-result failure.");
                },
                expectedPriorEnvelopeCanonicalSha256: null),
        requiredText: "Injected prepared create-result failure");
    Require(
        throwingCreatePreparationCount == 1 && !File.Exists(documentPath),
        "A throwing create preparation was not invoked exactly once or published a document.");
    AssertNoOrphans(authoredRoot, documentExpected: false);

    string exactStaleTemporaryPath = Path.Combine(
        authoredRoot,
        $".{Id65AuthoringEditorDocumentStore.FileName}.{new string('a', 32)}.tmp");
    await File.WriteAllTextAsync(exactStaleTemporaryPath, "owned-stale-temp\n");
    int successfulPreparationCount = 0;
    Id65AuthoringEditorDocumentStoreResult? stagedFirst = null;
    Id65AuthoringEditorDocumentStoreResult first =
        await Id65AuthoringEditorDocumentStore.SavePreparedAsync(
            paths,
            manifest,
            fullDocument,
            projectionInputs,
            staged =>
            {
                successfulPreparationCount++;
                AssertClosedResult(staged, fullDocument);
                stagedFirst = staged;
                return staged;
            },
            expectedPriorEnvelopeCanonicalSha256: null);
    Require(
        successfulPreparationCount == 1 &&
        ReferenceEquals(first, stagedFirst) &&
        !File.Exists(exactStaleTemporaryPath),
        "The prepared result was not returned exactly once or stale persistence residue survived.");
    Require(
        File.Exists(writerLeasePath) &&
        (await File.ReadAllBytesAsync(writerLeasePath)).SequenceEqual(persistentLeaseSentinel),
        "The reusable persistent writer lease was deleted, truncated, or treated as stale.");
    AssertClosedResult(first, fullDocument);
    AssertNoOrphans(authoredRoot, documentExpected: true);

    byte[] firstEnvelopeBytes = await File.ReadAllBytesAsync(documentPath);
    Require(
        firstEnvelopeBytes.Length > 0 &&
        firstEnvelopeBytes.Length <= Id65AuthoringEditorDocumentStore.MaximumEnvelopeByteCount &&
        Hash(firstEnvelopeBytes) == first.EnvelopeCanonicalSha256,
        "The first persisted envelope bytes do not match their bounded canonical identity.");
    Id65AuthoringEditorDocumentStoreResult loadedFirst =
        await RequireLoadedAsync(paths, manifest, projectionInputs);
    RequireEquivalent(first, loadedFirst, "first create/load");

    int throwingReplacementPreparationCount = 0;
    await ExpectRejectedAsync(
        "throwing replacement preparation",
        async () => _ = await Id65AuthoringEditorDocumentStore
            .SavePreparedAsync<object>(
                paths,
                manifest,
                noTerrainDocument,
                projectionInputs,
                staged =>
                {
                    throwingReplacementPreparationCount++;
                    AssertClosedResult(staged, noTerrainDocument);
                    throw new InvalidDataException(
                        "Injected prepared replacement-result failure.");
                },
                first.EnvelopeCanonicalSha256),
        requiredText: "Injected prepared replacement-result failure");
    Require(
        throwingReplacementPreparationCount == 1 &&
        (await File.ReadAllBytesAsync(documentPath)).SequenceEqual(firstEnvelopeBytes),
        "A throwing replacement preparation was not invoked exactly once or changed the published envelope.");
    AssertNoOrphans(authoredRoot, documentExpected: true);

    Id65AuthoringEditorDocumentStoreResult deterministicReplacement =
        await Id65AuthoringEditorDocumentStore.SaveAsync(
            paths,
            manifest,
            fullDocument,
            projectionInputs,
            first.EnvelopeCanonicalSha256);
    Require(
        (await File.ReadAllBytesAsync(documentPath)).SequenceEqual(firstEnvelopeBytes) &&
        deterministicReplacement.EnvelopeCanonicalSha256 == first.EnvelopeCanonicalSha256,
        "Same-document CAS replacement was not byte-for-byte deterministic.");

    byte[] stableFullEnvelope = await File.ReadAllBytesAsync(documentPath);
    await ExpectRejectedAsync(
        "wrong prior identity",
        async () => _ = await Id65AuthoringEditorDocumentStore.SaveAsync(
            paths,
            manifest,
            noTerrainDocument,
            projectionInputs,
            new string('0', 64)),
        requiredText: "changed after its prior envelope identity");
    Require(
        (await File.ReadAllBytesAsync(documentPath)).SequenceEqual(stableFullEnvelope),
        "A wrong-prior CAS attempt changed the published document.");
    await ExpectRejectedAsync(
        "create-only existing refusal",
        async () => _ = await Id65AuthoringEditorDocumentStore.SaveAsync(
            paths,
            manifest,
            fullDocument,
            projectionInputs,
            expectedPriorEnvelopeCanonicalSha256: null),
        requiredText: "already exists");
    Require(
        (await File.ReadAllBytesAsync(documentPath)).SequenceEqual(stableFullEnvelope),
        "Create-only refusal changed the published document.");

    using (CancellationTokenSource canceled = new())
    {
        canceled.Cancel();
        await ExpectCanceledAsync(
            "pre-cancelled existing load",
            async () => _ = await Id65AuthoringEditorDocumentStore.LoadAsync(
                paths,
                manifest,
                projectionInputs,
                canceled.Token));
        await ExpectCanceledAsync(
            "pre-cancelled replacement",
            async () => _ = await Id65AuthoringEditorDocumentStore.SaveAsync(
                paths,
                manifest,
                noTerrainDocument,
                projectionInputs,
                deterministicReplacement.EnvelopeCanonicalSha256,
                canceled.Token));
    }
    Require(
        (await File.ReadAllBytesAsync(documentPath)).SequenceEqual(stableFullEnvelope),
        "A pre-cancelled existing persistence call changed the published document.");

    await using (FileStream activeLease = new(
                     writerLeasePath,
                     FileMode.Open,
                     FileAccess.ReadWrite,
                     FileShare.None))
    {
        await ExpectRejectedAsync(
            "active writer exclusion",
            async () => _ = await Id65AuthoringEditorDocumentStore.SaveAsync(
                paths,
                manifest,
                fullDocument,
                projectionInputs,
                deterministicReplacement.EnvelopeCanonicalSha256),
            requiredText: "active");
    }
    Id65AuthoringEditorDocumentStoreResult reopenedLeaseReplacement =
        await Id65AuthoringEditorDocumentStore.SaveAsync(
            paths,
            manifest,
            fullDocument,
            projectionInputs,
            deterministicReplacement.EnvelopeCanonicalSha256);
    RequireEquivalent(
        deterministicReplacement,
        reopenedLeaseReplacement,
        "persistent lease reopen after active exclusion");
    AssertNoOrphans(authoredRoot, documentExpected: true);

    Directory.CreateDirectory(paths.OperationsDirectoryPath);
    string bootstrapOperationResidue = Path.Combine(
        paths.OperationsDirectoryPath,
        $"bootstrap-{Guid.NewGuid():N}");
    Directory.CreateDirectory(bootstrapOperationResidue);
    _ = await RequireLoadedAsync(paths, manifest, projectionInputs);
    Require(
        Directory.Exists(bootstrapOperationResidue) &&
        !Directory.EnumerateFileSystemEntries(bootstrapOperationResidue).Any(),
        "Store load triggered bootstrap operation recovery or mutated its residue.");
    Id65AuthoringEditorDocumentStoreResult operationSafeReplacement =
        await Id65AuthoringEditorDocumentStore.SaveAsync(
            paths,
            manifest,
            fullDocument,
            projectionInputs,
            reopenedLeaseReplacement.EnvelopeCanonicalSha256);
    Require(
        Directory.Exists(bootstrapOperationResidue) &&
        !Directory.EnumerateFileSystemEntries(bootstrapOperationResidue).Any(),
        "Store save triggered bootstrap operation recovery or mutated its residue.");

    Id65AuthoringEditorDocumentStoreResult noTerrain =
        await Id65AuthoringEditorDocumentStore.SaveAsync(
            paths,
            manifest,
            noTerrainDocument,
            projectionInputs,
            operationSafeReplacement.EnvelopeCanonicalSha256);
    byte[] noTerrainEnvelopeBytes = await File.ReadAllBytesAsync(documentPath);
    Require(
        noTerrain.EnvelopeCanonicalSha256 != operationSafeReplacement.EnvelopeCanonicalSha256 &&
        noTerrain.DocumentCanonicalSha256 == noTerrainDocument.CanonicalSha256,
        "CAS replacement did not publish the distinct no-terrain document identity.");
    await ExpectRejectedAsync(
        "stale prior identity",
        async () => _ = await Id65AuthoringEditorDocumentStore.SaveAsync(
            paths,
            manifest,
            fullDocument,
            projectionInputs,
            operationSafeReplacement.EnvelopeCanonicalSha256),
        requiredText: "changed after its prior envelope identity");
    Require(
        (await File.ReadAllBytesAsync(documentPath)).SequenceEqual(noTerrainEnvelopeBytes),
        "A stale-prior CAS attempt changed the replacement document.");

    File.Delete(documentPath);
    await ExpectRejectedAsync(
        "missing prior identity",
        async () => _ = await Id65AuthoringEditorDocumentStore.SaveAsync(
            paths,
            manifest,
            fullDocument,
            projectionInputs,
            noTerrain.EnvelopeCanonicalSha256),
        requiredText: "prior ID65 editor-document envelope is missing");
    Require(!File.Exists(documentPath), "A missing-prior refusal unexpectedly created a document.");
    AssertNoOrphans(authoredRoot, documentExpected: false);
    Id65AuthoringEditorDocumentStoreResult recreatedNoTerrain =
        await Id65AuthoringEditorDocumentStore.SaveAsync(
            paths,
            manifest,
            noTerrainDocument,
            projectionInputs,
            expectedPriorEnvelopeCanonicalSha256: null);
    byte[] validEnvelopeBytes = await File.ReadAllBytesAsync(documentPath);
    Require(
        validEnvelopeBytes.SequenceEqual(noTerrainEnvelopeBytes) &&
        recreatedNoTerrain.EnvelopeCanonicalSha256 == noTerrain.EnvelopeCanonicalSha256,
        "Create-after-missing did not reproduce the deterministic no-terrain envelope.");

    await RejectStoredEnvelopeAsync(
        "malformed JSON",
        Encoding.UTF8.GetBytes("{"),
        validEnvelopeBytes,
        documentPath,
        paths,
        manifest,
        projectionInputs);
    await RejectStoredEnvelopeAsync(
        "unknown envelope field",
        RewriteEnvelope(validEnvelopeBytes, root => root["unknownField"] = true),
        validEnvelopeBytes,
        documentPath,
        paths,
        manifest,
        projectionInputs,
        requiredText: "unknown field");
    await RejectStoredEnvelopeAsync(
        "duplicate envelope field",
        AddDuplicateFormat(validEnvelopeBytes),
        validEnvelopeBytes,
        documentPath,
        paths,
        manifest,
        projectionInputs,
        requiredText: "duplicate field");
    await RejectStoredEnvelopeAsync(
        "envelope content hash",
        RewriteEnvelope(validEnvelopeBytes, root =>
            root["envelopeContentSha256"] = new string('0', 64)),
        validEnvelopeBytes,
        documentPath,
        paths,
        manifest,
        projectionInputs);
    await RejectStoredEnvelopeAsync(
        "document canonical hash",
        RewriteEnvelope(validEnvelopeBytes, root =>
            root["documentCanonicalSha256"] = new string('0', 64)),
        validEnvelopeBytes,
        documentPath,
        paths,
        manifest,
        projectionInputs);
    await RejectStoredEnvelopeAsync(
        "embedded document content hash",
        RewriteEnvelope(validEnvelopeBytes, root =>
            RequireObject(root, "document")["contentSha256"] = new string('0', 64)),
        validEnvelopeBytes,
        documentPath,
        paths,
        manifest,
        projectionInputs);
    await RejectStoredEnvelopeAsync(
        "lab profile",
        RewriteEnvelope(validEnvelopeBytes, root => root["labProfileId"] = "forged-profile"),
        validEnvelopeBytes,
        documentPath,
        paths,
        manifest,
        projectionInputs);
    await RejectStoredEnvelopeAsync(
        "workspace layout",
        RewriteEnvelope(validEnvelopeBytes, root => root["workspaceLayoutVersion"] = 2),
        validEnvelopeBytes,
        documentPath,
        paths,
        manifest,
        projectionInputs);
    await RejectStoredEnvelopeAsync(
        "locked base",
        RewriteEnvelope(validEnvelopeBytes, root =>
            root["lockedBaseImageSha256"] = new string('0', 64)),
        validEnvelopeBytes,
        documentPath,
        paths,
        manifest,
        projectionInputs);
    await RejectStoredEnvelopeAsync(
        "projection identity",
        RewriteEnvelope(validEnvelopeBytes, root =>
            root["compositeManifestSha256"] = new string('0', 64)),
        validEnvelopeBytes,
        documentPath,
        paths,
        manifest,
        projectionInputs);
    await RejectStoredEnvelopeAsync(
        "oversized envelope",
        new byte[Id65AuthoringEditorDocumentStore.MaximumEnvelopeByteCount + 1],
        validEnvelopeBytes,
        documentPath,
        paths,
        manifest,
        projectionInputs,
        requiredText: "exceeds the 262144-byte persistence limit");
    Id65AuthoringEditorDocumentStoreResult recoveredAfterTamper =
        await RequireLoadedAsync(paths, manifest, projectionInputs);
    RequireEquivalent(recreatedNoTerrain, recoveredAfterTamper, "tamper restoration");
    AssertNoOrphans(authoredRoot, documentExpected: true);

    string outsideFile = Path.Combine(smokeRoot, "outside-destination-sentinel.json");
    byte[] outsideFileSentinel = Encoding.UTF8.GetBytes("outside-file-must-not-change\n");
    await File.WriteAllBytesAsync(outsideFile, outsideFileSentinel);
    File.Delete(documentPath);
    File.CreateSymbolicLink(documentPath, outsideFile);
    await ExpectRejectedAsync(
        "destination symbolic link",
        async () => _ = await Id65AuthoringEditorDocumentStore.LoadAsync(
            paths,
            manifest,
            projectionInputs),
        requiredText: "escapes its authored-edits root");
    Require(
        (await File.ReadAllBytesAsync(outsideFile)).SequenceEqual(outsideFileSentinel),
        "Destination symlink refusal changed the outside target.");
    File.Delete(documentPath);
    await File.WriteAllBytesAsync(documentPath, validEnvelopeBytes);

    string outsideAuthoredRoot = Path.Combine(smokeRoot, "outside-authored-root");
    Directory.CreateDirectory(outsideAuthoredRoot);
    string outsideAuthoredSentinel = Path.Combine(outsideAuthoredRoot, "outside-sentinel.txt");
    await File.WriteAllTextAsync(outsideAuthoredSentinel, "outside-directory-must-not-change\n");
    UnusedLevel65BlankLevelLabWorkspacePaths escapedPaths = paths with
    {
        AuthoredEditsDirectoryPath = outsideAuthoredRoot
    };
    await ExpectRejectedAsync(
        "authored root lexical escape",
        async () => _ = await Id65AuthoringEditorDocumentStore.LoadAsync(
            escapedPaths,
            manifest,
            projectionInputs));

    File.Delete(documentPath);
    File.Delete(writerLeasePath);
    Directory.Delete(authoredRoot);
    Directory.CreateSymbolicLink(authoredRoot, outsideAuthoredRoot);
    try
    {
        await ExpectRejectedAsync(
            "authored root symbolic link",
            async () => _ = await Id65AuthoringEditorDocumentStore.LoadAsync(
                paths,
                manifest,
                projectionInputs));
        Require(
            File.Exists(outsideAuthoredSentinel) &&
            await File.ReadAllTextAsync(outsideAuthoredSentinel) ==
                "outside-directory-must-not-change\n",
            "Authored-root symlink refusal changed the outside directory.");
    }
    finally
    {
        DirectoryInfo authoredLink = new(authoredRoot);
        if (!string.IsNullOrWhiteSpace(authoredLink.LinkTarget))
            Directory.Delete(authoredRoot);
    }
    Require(
        Directory.Exists(outsideAuthoredRoot) && File.Exists(outsideAuthoredSentinel),
        "Removing the authored-root symlink removed its outside target.");
    Directory.CreateDirectory(authoredRoot);
    await File.WriteAllBytesAsync(documentPath, validEnvelopeBytes);
    Id65AuthoringEditorDocumentStoreResult final =
        await Id65AuthoringEditorDocumentStore.SaveAsync(
            paths,
            manifest,
            noTerrainDocument,
            projectionInputs,
            recreatedNoTerrain.EnvelopeCanonicalSha256);
    RequireEquivalent(recreatedNoTerrain, final, "final symlink-safe replacement");
    AssertNoOrphans(authoredRoot, documentExpected: true);

    AssertClosedResult(final, noTerrainDocument);
    AssertStoreApiBoundary();
    Require(
        Directory.Exists(bootstrapOperationResidue) &&
        !Directory.EnumerateFileSystemEntries(bootstrapOperationResidue).Any(),
        "Persistence calls recovered or mutated the seeded bootstrap operation residue.");
    Require(
        SnapshotFile(paths.LockedBaseImagePath) == labImageBefore &&
        SnapshotFile(paths.LockedBaseCuePath) == labCueBefore &&
        SnapshotFile(paths.ManifestPath) == labManifestBefore,
        "Persistence changed the lab locked BIN, CUE, or manifest.");
    Require(
        SnapshotFile(cleanImagePath) == cleanImageBefore &&
        SnapshotFile(cleanCuePath) == cleanCueBefore &&
        SnapshotFile(catalogPath) == catalogBefore,
        "Persistence changed a clean source or catalog fixture.");

    Console.WriteLine(
        "PASS Id65AuthoringEditorDocumentPersistenceSmoke: isolated exact-lab create/load and deterministic CAS replacement, pre-publication result preparation success/null/throw atomicity, stale/wrong/missing/create-only refusal, strict envelope/document/profile/layout/base/projection tamper rejection, 256-KiB bounded reads, pre-cancellation, symlink/escape refusal, persistent exclusive lease reuse, exact stale-temp recovery, read-only workspace validation, immutable BIN/CUE/manifest/source fixtures, and fail-closed production flags passed. Other deterministic injected mid-stage faults remain unavailable; hostile parent-directory swaps remain explicitly unsafe, the Store remains internal and carries no App/Create-BIN authority, and normal Create BIN stays unwired.");
}
finally
{
    if (Directory.Exists(smokeRoot) &&
        Path.GetFileName(smokeRoot).StartsWith(SmokeRootPrefix, StringComparison.Ordinal))
    {
        Directory.Delete(smokeRoot, recursive: true);
    }
    Require(!Directory.Exists(smokeRoot), "The persistence smoke retained temporary game data.");
}

static async Task<Id65AuthoringEditorDocumentStoreResult> RequireLoadedAsync(
    UnusedLevel65BlankLevelLabWorkspacePaths paths,
    UnusedLevel65BlankLevelLabManifest manifest,
    Id65AuthoringEditorDocumentProjectionInputs projectionInputs)
{
    return await Id65AuthoringEditorDocumentStore.LoadAsync(
        paths,
        manifest,
        projectionInputs) ?? throw new InvalidOperationException(
            "The expected persisted ID65 editor document was missing.");
}

static Id65AuthoringEditorNativeInputProvider.InputSet ForgeProviderInputSetForNegativeTest(
    Id65AuthoringEditorAggregateBoundSource boundSource,
    Id65V2NativeTextureWitness textureWitness,
    Id65MobyDependencyBundleDescriptor grassBundle,
    string canonicalInputSetSha256)
{
    Type implementation = typeof(Id65AuthoringEditorNativeInputProvider).GetNestedType(
            "ProviderInputSet",
            BindingFlags.NonPublic)
        ?? throw new InvalidOperationException(
            "The private provider-owned native-input implementation is missing.");
    ConstructorInfo constructor = implementation.GetConstructors(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
        .Single();
    Require(
        implementation.IsNestedPrivate && implementation.IsSealed &&
        implementation.BaseType == typeof(Id65AuthoringEditorNativeInputProvider.InputSet),
        "The provider-owned negative-test implementation topology changed.");
    return (Id65AuthoringEditorNativeInputProvider.InputSet)(Activator.CreateInstance(
        implementation,
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
        binder: null,
        args:
        [
            boundSource,
            textureWitness,
            grassBundle,
            canonicalInputSetSha256
        ],
        culture: null) ?? throw new InvalidOperationException(
            "The provider-owned negative-test input could not be created."));
}

static async Task RejectStoredEnvelopeAsync(
    string label,
    byte[] forgedBytes,
    byte[] validBytes,
    string documentPath,
    UnusedLevel65BlankLevelLabWorkspacePaths paths,
    UnusedLevel65BlankLevelLabManifest manifest,
    Id65AuthoringEditorDocumentProjectionInputs projectionInputs,
    string? requiredText = null)
{
    await File.WriteAllBytesAsync(documentPath, forgedBytes);
    try
    {
        await ExpectRejectedAsync(
            label,
            async () => _ = await Id65AuthoringEditorDocumentStore.LoadAsync(
                paths,
                manifest,
                projectionInputs),
            requiredText);
    }
    finally
    {
        await File.WriteAllBytesAsync(documentPath, validBytes);
    }
}

static byte[] RewriteEnvelope(byte[] source, Action<JsonObject> mutate)
{
    JsonObject root = JsonNode.Parse(Encoding.UTF8.GetString(source))?.AsObject()
        ?? throw new InvalidDataException("The valid persistence-smoke envelope is not an object.");
    mutate(root);
    return Encoding.UTF8.GetBytes(root.ToJsonString(new JsonSerializerOptions
    {
        WriteIndented = false
    }));
}

static JsonObject RequireObject(JsonObject owner, string name) =>
    owner[name]?.AsObject() ?? throw new InvalidDataException(
        $"The valid persistence-smoke envelope is missing object '{name}'.");

static byte[] AddDuplicateFormat(byte[] source)
{
    string json = Encoding.UTF8.GetString(source);
    Require(json.StartsWith('{'), "The valid persistence-smoke envelope is not a JSON object.");
    return Encoding.UTF8.GetBytes(
        json.Insert(1, $"\"format\":\"{Id65AuthoringEditorDocumentStore.EnvelopeFormat}\","));
}

static void AssertClosedResult(
    Id65AuthoringEditorDocumentStoreResult result,
    Id65AuthoringEditorDocument expectedDocument)
{
    Require(
        result.Document.SerializeUtf8().SequenceEqual(expectedDocument.SerializeUtf8()) &&
        result.DocumentCanonicalSha256 == expectedDocument.CanonicalSha256 &&
        result.ProjectionContractId == Id65AuthoringEditorDocumentStore.ProjectionContractId &&
        !result.ContainsCompositeAfterimage &&
        !result.ContainsDiscImageBytes &&
        !result.ContainsCueBytes &&
        !result.WritesDiscImage &&
        !result.WritesCue &&
        !result.AppIntegrated &&
        !result.CreateBinEnabled &&
        !result.NormalCreateBinEnabled &&
        !result.RuntimeCandidateAuthorized &&
        !result.ReleaseIntegrated &&
        !result.PromotionAuthorized &&
        !result.Publishable &&
        result.RequiresTrustedExclusiveWorkspaceDirectory &&
        !result.HostileFilesystemRaceSafe &&
        !result.CommonWorkspaceLeaseOwned,
        "A store result changed identity, exposed native bytes, or opened a production/safety gate.");
    Require(
        !expectedDocument.PersistenceEnabled &&
        !expectedDocument.WritesFileSystem &&
        !expectedDocument.WritesDiscImage &&
        !expectedDocument.WritesCue &&
        !expectedDocument.AppIntegrated &&
        !expectedDocument.CreateBinEnabled &&
        !expectedDocument.NormalCreateBinEnabled &&
        !expectedDocument.RuntimeCandidateAuthorized &&
        !expectedDocument.ReleaseIntegrated &&
        !expectedDocument.PromotionAuthorized &&
        !expectedDocument.Publishable,
        "The persisted document opened a persistence or production gate.");
}

static void AssertStoreApiBoundary()
{
    Require(
        Id65AuthoringEditorDocumentStore.RequiresTrustedExclusiveWorkspaceDirectory &&
        !Id65AuthoringEditorDocumentStore.HostileFilesystemRaceSafe &&
        !Id65AuthoringEditorDocumentStore.CommonWorkspaceLeaseOwned,
        "The store overclaimed hostile-filesystem or common-workspace lease safety.");
    MethodInfo save = typeof(Id65AuthoringEditorDocumentStore)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Single(method => method.Name == nameof(Id65AuthoringEditorDocumentStore.SaveAsync));
    ParameterInfo[] parameters = save.GetParameters();
    Require(
        parameters.All(parameter =>
            !typeof(Delegate).IsAssignableFrom(parameter.ParameterType) &&
            parameter.ParameterType != typeof(byte[]) &&
            !typeof(Stream).IsAssignableFrom(parameter.ParameterType) &&
            parameter.ParameterType != typeof(Id65CompiledAuthoringComposite) &&
            parameter.ParameterType != typeof(Id65AuthoringEditorProjection)),
        "The store Save API gained a fault hook, raw byte/stream input, or compiled afterimage input.");
    MethodInfo preparedSave = typeof(Id65AuthoringEditorDocumentStore)
        .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
        .Single(method => method.Name == "SavePreparedAsync");
    Require(
        preparedSave.IsAssembly && preparedSave.IsGenericMethodDefinition &&
        preparedSave.GetGenericArguments().Length == 1 &&
        preparedSave.GetParameters().Count(parameter =>
            typeof(Delegate).IsAssignableFrom(parameter.ParameterType)) == 1,
        "The pre-publication result seam is not one internal generic callback boundary.");
    Require(
        typeof(Id65AuthoringEditorDocumentStoreResult)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .All(property =>
                property.PropertyType != typeof(byte[]) &&
                !typeof(Stream).IsAssignableFrom(property.PropertyType) &&
                property.PropertyType != typeof(Id65CompiledAuthoringComposite) &&
                property.PropertyType != typeof(Id65AuthoringEditorProjection)),
        "The store result exposed raw bytes, a stream, or a compiled projection afterimage.");
}

static void AssertNoOrphans(string authoredRoot, bool documentExpected)
{
    Require(Directory.Exists(authoredRoot), "The authored-edits directory is missing.");
    HashSet<string> expected = new(StringComparer.Ordinal)
    {
        WriterLeaseFileName
    };
    if (documentExpected)
        expected.Add(Id65AuthoringEditorDocumentStore.FileName);
    string[] actual = Directory
        .EnumerateFileSystemEntries(authoredRoot, "*", SearchOption.TopDirectoryOnly)
        .Select(Path.GetFileName)
        .Order(StringComparer.Ordinal)
        .ToArray()!;
    Require(
        actual.ToHashSet(StringComparer.Ordinal).SetEquals(expected),
        $"Persistence left unexpected authored-edits entries: {string.Join(", ", actual)}.");
    string leasePath = Path.Combine(authoredRoot, WriterLeaseFileName);
    Require(
        File.Exists(leasePath) &&
        string.IsNullOrWhiteSpace(new FileInfo(leasePath).LinkTarget),
        "The persistent writer lease is missing or is not a regular entry.");
}

static void RequireEquivalent(
    Id65AuthoringEditorDocumentStoreResult expected,
    Id65AuthoringEditorDocumentStoreResult actual,
    string label)
{
    Require(
        expected.Path == actual.Path &&
        expected.Document.SerializeUtf8().SequenceEqual(actual.Document.SerializeUtf8()) &&
        expected.DocumentCanonicalSha256 == actual.DocumentCanonicalSha256 &&
        expected.EnvelopeCanonicalSha256 == actual.EnvelopeCanonicalSha256 &&
        expected.ProjectionContractId == actual.ProjectionContractId &&
        expected.CompositeProfileId == actual.CompositeProfileId &&
        expected.CompositeManifestSha256 == actual.CompositeManifestSha256 &&
        expected.CompositeOutputRow80Sha256 == actual.CompositeOutputRow80Sha256 &&
        expected.CompositeDeterministicPlanSha256 == actual.CompositeDeterministicPlanSha256,
        $"Store result identity changed across {label}.");
}

static async Task ExpectCanceledAsync(string label, Func<Task> action)
{
    bool canceled = false;
    try
    {
        await action();
    }
    catch (OperationCanceledException)
    {
        canceled = true;
    }
    Require(canceled, $"Persistence action '{label}' did not honor pre-cancellation.");
}

static async Task ExpectRejectedAsync(
    string label,
    Func<Task> action,
    string? requiredText = null)
{
    Exception? rejection = null;
    try
    {
        await action();
    }
    catch (Exception exception) when (exception is
        ArgumentNullException or InvalidDataException or InvalidOperationException or
        IOException or UnauthorizedAccessException or NotSupportedException)
    {
        rejection = exception;
    }
    if (rejection is null)
        throw new InvalidOperationException($"Persistence action '{label}' was not rejected.");
    if (requiredText is not null)
    {
        Require(
            rejection.ToString().Contains(requiredText, StringComparison.OrdinalIgnoreCase),
            $"Persistence action '{label}' did not reject with '{requiredText}': {rejection}");
    }
}

static FileState SnapshotFile(string path)
{
    FileInfo info = new(path);
    string? linkTarget = info.LinkTarget;
    string? resolvedTarget = string.IsNullOrWhiteSpace(linkTarget)
        ? null
        : info.ResolveLinkTarget(returnFinalTarget: true)?.FullName;
    using FileStream stream = File.OpenRead(path);
    long contentLength = stream.Length;
    string sha256 = Convert.ToHexStringLower(SHA256.HashData(stream));
    return new(
        contentLength,
        File.GetLastWriteTimeUtc(path),
        sha256,
        linkTarget,
        resolvedTarget);
}

static string Hash(ReadOnlySpan<byte> bytes) =>
    Convert.ToHexStringLower(SHA256.HashData(bytes));

static string FindRepositoryRoot(string? start)
{
    DirectoryInfo? current = new(Path.GetFullPath(start ?? Directory.GetCurrentDirectory()));
    while (current is not null)
    {
        if (File.Exists(Path.Combine(current.FullName, "global.json")) &&
            Directory.Exists(Path.Combine(current.FullName, "src", "Spyro.Editor.Core")))
        {
            return current.FullName;
        }
        current = current.Parent;
    }
    throw new DirectoryNotFoundException("Could not locate the Spyro Editor repository root.");
}

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

internal sealed record FileState(
    long ContentLength,
    DateTime LastWriteUtc,
    string Sha256,
    string? LinkTarget,
    string? ResolvedTarget);
