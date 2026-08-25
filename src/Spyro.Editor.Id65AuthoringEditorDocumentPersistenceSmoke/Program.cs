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
string lockedImagePath = Path.Combine(
    repositoryRoot,
    "_local",
    "v5-stone-hill-level-replacement",
    "unused-level-65-display-name",
    "Unused-Level-65-Town-Square-independent-storage-with-Town-Square-display-name-RUNTIME-CANDIDATE.bin");
string foundationImagePath = Path.Combine(
    repositoryRoot,
    "_local",
    "v5-stone-hill-level-replacement",
    "unused-level-65-full-authoring-foundation-native-membership",
    "Unused-Level-65-Full-Authoring-Foundation-HP-LP-45deg-NATIVE-MEMBERSHIP-RUNTIME-CANDIDATE.bin");

foreach (string requiredPath in new[]
         {
             cleanImagePath,
             cleanCuePath,
             catalogPath,
             lockedImagePath,
             foundationImagePath
         })
{
    Require(File.Exists(requiredPath), $"Missing persistence-smoke fixture: {requiredPath}");
}

FileState cleanImageBefore = SnapshotFile(cleanImagePath);
FileState cleanCueBefore = SnapshotFile(cleanCuePath);
FileState catalogBefore = SnapshotFile(catalogPath);
FileState lockedImageBefore = SnapshotFile(lockedImagePath);
FileState foundationImageBefore = SnapshotFile(foundationImagePath);
IReadOnlyList<string> lockedDirectoryBefore =
    SnapshotDirectory(Path.GetDirectoryName(lockedImagePath)!);
IReadOnlyList<string> foundationDirectoryBefore =
    SnapshotDirectory(Path.GetDirectoryName(foundationImagePath)!);
Require(
    cleanImageBefore.Sha256 == ExpectedCleanImageSha256,
    "The persistence smoke is not bound to the exact clean USA source image.");

Id65AuthoringEditorDocumentProjectionInputs projectionInputs =
    BuildProjectionInputs(lockedImagePath, foundationImagePath);
Id65AuthoringEditorDocument fullDocument =
    Id65AuthoringEditorDocument.CreateExact(includeOptionalTerrain: true);
Id65AuthoringEditorDocument noTerrainDocument =
    Id65AuthoringEditorDocument.CreateExact(includeOptionalTerrain: false);

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

    string exactStaleTemporaryPath = Path.Combine(
        authoredRoot,
        $".{Id65AuthoringEditorDocumentStore.FileName}.{new string('a', 32)}.tmp");
    await File.WriteAllTextAsync(exactStaleTemporaryPath, "owned-stale-temp\n");
    Id65AuthoringEditorDocumentStoreResult first =
        await Id65AuthoringEditorDocumentStore.SaveAsync(
            paths,
            manifest,
            fullDocument,
            projectionInputs,
            expectedPriorEnvelopeCanonicalSha256: null);
    Require(
        !File.Exists(exactStaleTemporaryPath),
        "An exact owned stale persistence temporary was not recovered under the lease.");
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
        SnapshotFile(catalogPath) == catalogBefore &&
        SnapshotFile(lockedImagePath) == lockedImageBefore &&
        SnapshotFile(foundationImagePath) == foundationImageBefore &&
        SnapshotDirectory(Path.GetDirectoryName(lockedImagePath)!).SequenceEqual(lockedDirectoryBefore) &&
        SnapshotDirectory(Path.GetDirectoryName(foundationImagePath)!).SequenceEqual(foundationDirectoryBefore),
        "Persistence changed a clean source, catalog, locked projection source, donor fixture, or fixture directory.");

    Console.WriteLine(
        "PASS Id65AuthoringEditorDocumentPersistenceSmoke: isolated exact-lab create/load and deterministic CAS replacement, stale/wrong/missing/create-only refusal, strict envelope/document/profile/layout/base/projection tamper rejection, 256-KiB bounded reads, pre-cancellation, symlink/escape refusal, persistent exclusive lease reuse, exact stale-temp recovery, read-only workspace validation, immutable BIN/CUE/manifest/source fixtures, and fail-closed production flags passed. Deterministic injected mid-stage faults remain unavailable; hostile parent-directory swaps remain explicitly unsafe and App/Create BIN stay unwired.");
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

static Id65AuthoringEditorDocumentProjectionInputs BuildProjectionInputs(
    string lockedImagePath,
    string foundationImagePath)
{
    UnusedLevel65RemoteBlankCollisionWindingRepairV2StaticPlan v2 =
        UnusedLevel65RemoteBlankCollisionWindingRepairV2Construction.BuildStaticPlan(lockedImagePath);
    byte[] directRow80 = ReadRootFile(lockedImagePath, "WAD.WAD", 0x6936800, 0x2E2000);
    byte[] targetOverlay = ReadRootFile(lockedImagePath, "WAD.WAD", 0x6927000, 0xF800);
    byte[] globalExecutable = ReadRootFileWhere(
        lockedImagePath,
        name => name.StartsWith("SCUS_", StringComparison.OrdinalIgnoreCase),
        0,
        0x66000);
    Require(
        directRow80.SequenceEqual(v2.SourceData),
        "The persistence smoke locked row-80 source differs from the V2 proof source.");
    Id65AuthoringCompositeLockedSource source =
        Id65AuthoringCompositeCompiler.CaptureLockedSource(
            directRow80,
            targetOverlay,
            globalExecutable);

    UnusedLevel65NativeTextureStaticPlan oldTextureProof =
        UnusedLevel65NativeTextureAuthoringContract.BuildFirstStaticPlan(foundationImagePath);
    Id65V2NativeTextureWitness textureWitness =
        Id65V2NativeTextureCompositionCompiler.ImportPinnedT66Witness(oldTextureProof);
    UnusedLevel65MobyDependencyBundleContract mobyContract =
        UnusedLevel65MobyDependencyBundleFoundation.InspectFirstCrossLevelDonor(
            foundationImagePath);
    byte[] package = ReadRootFile(foundationImagePath, "WAD.WAD", 0x9C1D20, 0x174);
    Id65MobyDependencyBundleDescriptor bundle =
        Id65AuthoringMobyDependencyBundleCompiler.CreateArtisansGrassBundle(
            mobyContract,
            package);
    return new(source, textureWitness, bundle);
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
        InvalidDataException or InvalidOperationException or IOException or UnauthorizedAccessException)
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

static byte[] ReadRootFile(
    string imagePath,
    string exactName,
    long relativeOffset,
    int byteLength) =>
    ReadRootFileWhere(
        imagePath,
        name => string.Equals(name, exactName, StringComparison.OrdinalIgnoreCase),
        relativeOffset,
        byteLength);

static byte[] ReadRootFileWhere(
    string imagePath,
    Predicate<string> predicate,
    long relativeOffset,
    int byteLength)
{
    DiscLayout layout = DiscImage.DetectLayout(imagePath);
    using FileStream image = File.OpenRead(imagePath);
    DiscFileRecord file = DiscImage.FindRootFileRecord(image, layout, predicate);
    if (relativeOffset < 0 || relativeOffset + byteLength > file.Size)
        throw new InvalidDataException("A persistence-smoke source read escaped its root file.");
    return DiscImage.ReadFileBytes(image, layout, file.Lba, relativeOffset, byteLength);
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

static IReadOnlyList<string> SnapshotDirectory(string path) =>
    Directory.GetFileSystemEntries(path)
        .Select(entry =>
        {
            FileSystemInfo info = Directory.Exists(entry)
                ? new DirectoryInfo(entry)
                : new FileInfo(entry);
            return $"{Path.GetFileName(entry)}|{info.LastWriteTimeUtc:O}|{info.LinkTarget}";
        })
        .Order(StringComparer.Ordinal)
        .ToArray();

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
