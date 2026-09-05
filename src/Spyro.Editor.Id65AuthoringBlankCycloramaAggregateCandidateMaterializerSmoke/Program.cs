using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

if (!OperatingSystem.IsMacOS())
    throw new PlatformNotSupportedException(
        "The schema-6 Darwin candidate materializer smoke requires macOS.");

string repositoryRoot = FindRepositoryRoot(args.ElementAtOrDefault(0));
string lockedImagePath = Path.Combine(
    repositoryRoot,
    "_local",
    "id65-authoring-aggregate-workspace",
    "_research",
    "unusedlevel65blank",
    "v1",
    "locked-base",
    "unusedlevel65blank-locked-base-v1.bin");
string lockedCuePath = Path.ChangeExtension(lockedImagePath, ".cue");
Require(File.Exists(lockedImagePath) && File.Exists(lockedCuePath),
    "Missing exact schema-6 candidate materializer fixture.");
FileState lockedBefore = SnapshotFile(lockedImagePath);
FileState cueBefore = SnapshotFile(lockedCuePath);
IReadOnlyList<string> lockedDirectoryBefore =
    SnapshotDirectory(Path.GetDirectoryName(lockedImagePath)!);
string smokeRoot = Path.Combine(
    repositoryRoot,
    "_local",
    "Spyro.Editor.Id65AuthoringBlankCycloramaAggregateCandidateMaterializerSmoke-" +
        Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(smokeRoot);

try
{
    UnusedLevel65BlankLevelLabWorkspacePaths paths =
        UnusedLevel65BlankLevelLabProfileRegistry.CreateWorkspacePaths(
            Path.Combine(smokeRoot, "workspace-container"));
    Directory.CreateDirectory(paths.LockedBaseDirectoryPath);
    Directory.CreateDirectory(paths.AuthoredEditsDirectoryPath);
    File.Copy(lockedImagePath, paths.LockedBaseImagePath, overwrite: false);
    await File.WriteAllTextAsync(
        paths.LockedBaseCuePath,
        DiscImage.BuildCueText(
            lockedCuePath,
            Path.GetFileName(paths.LockedBaseImagePath)),
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
            .ValidatePublishedWorkspaceReadOnlyAsync(paths) == manifest,
        "The schema-6 candidate materializer workspace did not validate.");
    FileState workspaceImageBefore = SnapshotFile(paths.LockedBaseImagePath);
    FileState workspaceCueBefore = SnapshotFile(paths.LockedBaseCuePath);
    FileState workspaceManifestBefore = SnapshotFile(paths.ManifestPath);

    int publications = 0;
    int positiveCalls = 0;
    int failClosedRejects = 0;
    int faultRejects = 0;
    int tamperRejects = 0;
    int symlinkRejects = 0;
    int redGemReuseChecks = 0;

    Id65AuthoringEditorControlState schema1State =
        Id65AuthoringEditorControlState.CreateDefault();
    Id65AuthoringEditorDocumentWriteResult schema1Write =
        await Id65AuthoringEditorDocumentWorkspaceWriter.SaveExactAsync(
            paths,
            manifest,
            schema1State,
            expectedPriorEnvelopeCanonicalSha256: null);
    foreach ((string Label, int Version,
              Id65AuthoringEditorDocumentWriteResult Stored) in new[]
             {
                 ("schema-1 document", 1, schema1Write)
             })
    {
        IReadOnlyList<string> beforeReject = SnapshotDirectory(paths.RootPath);
        await ExpectRejectedAsync(
            Label,
            () => Id65AuthoringBlankCycloramaAggregateCandidateWorkspaceMaterializer
                .MaterializeExactAsync(
                    paths,
                    manifest,
                    Stored.EnvelopeCanonicalSha256),
            "native inputs");
        failClosedRejects++;
        Require(Stored.Document.SchemaVersion == Version &&
                SnapshotDirectory(paths.RootPath).SequenceEqual(beforeReject),
            $"A {Label} candidate refusal changed workspace state.");
    }

    Id65AuthoringEditorControlState schema2State = new(
        includeStructuralTerrain: true,
        includeGrassAtT88: true,
        includeGrassAtT107: true,
        mandatoryBaseFaceTextureId:
            Id65AuthoringEditorDocument.ResidentMandatoryBaseFaceTextureId);
    Id65AuthoringEditorDocumentWriteResult schema2Write =
        await Id65AuthoringEditorDocumentWorkspaceWriter.SaveExactAsync(
            paths,
            manifest,
            schema2State,
            schema1Write.EnvelopeCanonicalSha256);
    IReadOnlyList<string> beforeSchema2Reject = SnapshotDirectory(paths.RootPath);
    await ExpectRejectedAsync(
        "schema-2 document",
        () => Id65AuthoringBlankCycloramaAggregateCandidateWorkspaceMaterializer
            .MaterializeExactAsync(
                paths,
                manifest,
                schema2Write.EnvelopeCanonicalSha256),
        "native inputs");
    failClosedRejects++;
    Require(schema2Write.Document.SchemaVersion == 2 &&
            SnapshotDirectory(paths.RootPath).SequenceEqual(beforeSchema2Reject),
        "A schema-2 candidate refusal changed workspace state.");

    Id65AuthoringEditorControlState schema1MigrationState = new(
        includeStructuralTerrain: true,
        includeGrassAtT88: false,
        includeGrassAtT107: true);
    Id65AuthoringEditorDocumentWriteResult schema1RestoredWrite =
        await Id65AuthoringEditorDocumentWorkspaceWriter.SaveExactAsync(
            paths,
            manifest,
            schema1MigrationState,
            schema2Write.EnvelopeCanonicalSha256);
    Id65AuthoringEditorControlState schema3State =
        Id65AuthoringEditorControlState.MigrateLegacyToRemotePadGrid(
            schema1MigrationState,
            Id65RemotePadGrassT107Catalog.CenterPositionId);
    Id65AuthoringEditorDocumentWriteResult schema3Write =
        await Id65AuthoringEditorDocumentWorkspaceWriter
            .MigrateLegacyToRemotePadGridExactAsync(
                paths,
                manifest,
                schema3State,
                schema1RestoredWrite.EnvelopeCanonicalSha256);
    IReadOnlyList<string> beforeSchema3Reject = SnapshotDirectory(paths.RootPath);
    await ExpectRejectedAsync(
        "schema-3 document",
        () => Id65AuthoringBlankCycloramaAggregateCandidateWorkspaceMaterializer
            .MaterializeExactAsync(
                paths,
                manifest,
                schema3Write.EnvelopeCanonicalSha256),
        "schema-6");
    failClosedRejects++;
    Require(schema3Write.Document.SchemaVersion == 3 &&
            SnapshotDirectory(paths.RootPath).SequenceEqual(beforeSchema3Reject),
        "A schema-3 candidate refusal changed workspace state.");

    Id65AuthoringEditorControlState schema4State =
        Id65AuthoringEditorControlState
            .MigrateRemotePadGridToMobyChoice(schema3State);
    Id65AuthoringEditorDocumentWriteResult schema4Write =
        await Id65AuthoringEditorDocumentWorkspaceWriter
            .MigrateRemotePadGridToMobyChoiceExactAsync(
                paths,
                manifest,
                schema4State,
                schema3Write.EnvelopeCanonicalSha256);
    IReadOnlyList<string> beforeSchemaReject = SnapshotDirectory(paths.RootPath);
    await ExpectRejectedAsync(
        "schema-4 document",
        () => Id65AuthoringBlankCycloramaAggregateCandidateWorkspaceMaterializer
            .MaterializeExactAsync(
                paths,
                manifest,
                schema4Write.EnvelopeCanonicalSha256),
        "schema-6");
    failClosedRejects++;
    Require(SnapshotDirectory(paths.RootPath).SequenceEqual(beforeSchemaReject),
        "A schema-4 candidate refusal changed workspace state.");

    Id65AuthoringEditorControlState migratedState =
        Id65AuthoringEditorControlState
            .MigrateRemotePadMobyChoiceToTerrainSet(schema4State);
    Id65AuthoringEditorDocumentWriteResult migratedWrite =
        await Id65AuthoringEditorDocumentWorkspaceWriter
            .MigrateRemotePadMobyChoiceToTerrainSetExactAsync(
                paths,
                manifest,
                migratedState,
                schema4Write.EnvelopeCanonicalSha256);
    Require(migratedWrite.ExplicitRemotePadTerrainSetMigrationPerformed &&
            migratedWrite.Document.SchemaVersion ==
                Id65AuthoringEditorDocument.RemotePadMobyChoiceTerrainSetSchemaVersion,
        "The explicit schema-4-to-5 setup migration changed.");
    IReadOnlyList<string> beforeSchema5Reject = SnapshotDirectory(paths.RootPath);
    await ExpectRejectedAsync(
        "schema-5 document",
        () => Id65AuthoringBlankCycloramaAggregateCandidateWorkspaceMaterializer
            .MaterializeExactAsync(
                paths,
                manifest,
                migratedWrite.EnvelopeCanonicalSha256),
        "schema-6");
    failClosedRejects++;
    Require(SnapshotDirectory(paths.RootPath).SequenceEqual(beforeSchema5Reject),
        "A schema-5 candidate refusal changed workspace state.");

    Id65AuthoringEditorControlState blankCycloramaState =
        Id65AuthoringEditorControlState
            .MigrateRemotePadMobyChoiceTerrainSetToBlankCyclorama(migratedState);
    Id65AuthoringEditorDocumentWriteResult blankCycloramaWrite =
        await Id65AuthoringEditorDocumentWorkspaceWriter
            .MigrateRemotePadMobyChoiceTerrainSetToBlankCycloramaExactAsync(
                paths,
                manifest,
                blankCycloramaState,
                migratedWrite.EnvelopeCanonicalSha256);
    Require(blankCycloramaWrite.ExplicitBlankCycloramaMigrationPerformed &&
            blankCycloramaWrite.Document.SchemaVersion ==
                Id65AuthoringEditorDocument
                    .RemotePadMobyChoiceTerrainSetBlankCycloramaSchemaVersion,
        "The explicit schema-5-to-6 setup migration changed.");
    string priorEnvelope = blankCycloramaWrite.EnvelopeCanonicalSha256;

    (string Key, string[] UnitIds)[] terrainTopologies =
    [
        ("zero", []),
        ("tile1", [Id65AuthoringTerrainSetCatalog.Tile1Id]),
        ("tile2", [Id65AuthoringTerrainSetCatalog.Tile2Id]),
        ("both",
        [
            Id65AuthoringTerrainSetCatalog.Tile1Id,
            Id65AuthoringTerrainSetCatalog.Tile2Id
        ])
    ];
    HashSet<string> planHashes = new(StringComparer.Ordinal);
    HashSet<string> outputHashes = new(StringComparer.Ordinal);
    foreach ((string topologyKey, string[] unitIds) in terrainTopologies)
    {
        foreach (Id65RemotePadMobyKind kind in
                 Enum.GetValues<Id65RemotePadMobyKind>())
        {
            string positionId = topologyKey == "tile2" &&
                kind == Id65RemotePadMobyKind.RedGemT21
                    ? Id65RemotePadGrassT107Catalog.NorthEastPositionId
                    : Id65RemotePadGrassT107Catalog.CenterPositionId;
            Id65AuthoringEditorDocument document =
                Id65AuthoringEditorDocument
                    .CreateRemotePadMobyChoiceTerrainSetBlankCycloramaExact(
                        kind,
                        positionId,
                        unitIds);
            Id65AuthoringEditorDocumentStoreResult stored =
                await SaveSchema6Async(
                    paths,
                    manifest,
                    document,
                    priorEnvelope);
            priorEnvelope = stored.EnvelopeCanonicalSha256;
            Id65AuthoringBlankCycloramaAggregateCandidateMaterializationResult
                result =
                    await Id65AuthoringBlankCycloramaAggregateCandidateMaterializer
                        .MaterializeAsync(new(
                            paths,
                            manifest,
                            priorEnvelope));
            AssertResult(
                result,
                unitIds,
                kind,
                reused: false,
                filesystemMutated: true,
                candidateWritten: true,
                renamedThisCall: true,
                expectedPositionId: positionId);
            Require(planHashes.Add(result.Plan.DeterministicAggregatePlanSha256) &&
                    outputHashes.Add(result.Receipt.OutputImageSha256),
                $"The {topologyKey}/{kind} candidate aliased another schema-6 state.");
            publications++;
            positiveCalls++;
            if (topologyKey == "zero" &&
                kind == Id65RemotePadMobyKind.RedGemT21)
            {
                Id65AuthoringBlankCycloramaAggregateCandidateMaterializationResult
                    redGemReuse =
                        await Id65AuthoringBlankCycloramaAggregateCandidateMaterializer
                            .MaterializeAsync(new(paths, manifest, priorEnvelope));
                AssertResult(
                    redGemReuse,
                    unitIds,
                    kind,
                    reused: true,
                    filesystemMutated: false,
                    candidateWritten: false,
                    renamedThisCall: false,
                    expectedPositionId: positionId);
                Require(redGemReuse.Receipt == result.Receipt &&
                        redGemReuse.Paths == result.Paths,
                    "Immutable Red Gem candidate reuse changed its receipt or paths.");
                positiveCalls++;
                redGemReuseChecks++;
            }
            Directory.Delete(result.Paths.CandidateDirectoryPath, recursive: true);
        }
    }
    Require(publications == 12 && redGemReuseChecks == 1 &&
            planHashes.Count == 12 && outputHashes.Count == 12,
        "The representative four-topology/three-Moby publication matrix changed.");

    string[] defaultTerrainIds =
    [
        Id65AuthoringTerrainSetCatalog.Tile1Id,
        Id65AuthoringTerrainSetCatalog.Tile2Id
    ];
    Id65RemotePadMobyKind defaultKind = Id65RemotePadMobyKind.GrassT107;
    Id65AuthoringEditorDocument defaultDocument =
        Id65AuthoringEditorDocument.CreateRemotePadMobyChoiceTerrainSetBlankCycloramaExact(
            defaultKind,
            Id65RemotePadGrassT107Catalog.CenterPositionId,
            defaultTerrainIds);
    string staleEnvelope = priorEnvelope;
    Id65AuthoringEditorDocumentStoreResult storedDefault =
        await SaveSchema6Async(
            paths,
            manifest,
            defaultDocument,
            priorEnvelope);
    priorEnvelope = storedDefault.EnvelopeCanonicalSha256;
    await ExpectRejectedAsync(
        "stale caller envelope CAS",
        () => Id65AuthoringBlankCycloramaAggregateCandidateWorkspaceMaterializer
            .MaterializeExactAsync(paths, manifest, staleEnvelope),
        "CAS identity");
    failClosedRejects++;
    await ExpectRejectedAsync(
        "noncanonical caller envelope CAS",
        () => Id65AuthoringBlankCycloramaAggregateCandidateWorkspaceMaterializer
            .MaterializeExactAsync(paths, manifest, "ABC"),
        "canonical");
    failClosedRejects++;

    Id65AuthoringBlankCycloramaAggregateCandidateMaterializationRequest request =
        new(paths, manifest, priorEnvelope);
    await ExpectRejectedAsync(
        "smoke-only entrypoint without a fault",
        () => Id65AuthoringBlankCycloramaAggregateCandidateMaterializer
            .MaterializeForSmokeAsync(
                request,
                Id65AuthoringBlankCycloramaAggregateCandidateFaultPoint.None),
        "requires an injected failure point");
    failClosedRejects++;

    string materializedRoot = Path.Combine(
        paths.RootPath,
        "materialized-blank-cyclorama-schema6-candidates");
    string operationsRoot = Path.Combine(
        paths.RootPath,
        ".blank-cyclorama-schema6-candidate-operations");
    string symlinkTarget = Path.Combine(smokeRoot, "symlink-target");
    Directory.CreateDirectory(symlinkTarget);
    Require(!Directory.EnumerateFileSystemEntries(materializedRoot).Any() &&
            !Directory.EnumerateFileSystemEntries(operationsRoot).Any(),
        "The matrix retained a candidate before negative testing.");
    Directory.Delete(materializedRoot);
    Directory.CreateSymbolicLink(materializedRoot, symlinkTarget);
    await ExpectRejectedAsync(
        "candidate-root symlink",
        () => Id65AuthoringBlankCycloramaAggregateCandidateMaterializer
            .MaterializeAsync(request),
        "project root");
    symlinkRejects++;
    Directory.Delete(materializedRoot);
    Directory.CreateDirectory(materializedRoot);
    Directory.Delete(operationsRoot);
    Directory.CreateSymbolicLink(operationsRoot, symlinkTarget);
    await ExpectRejectedAsync(
        "operation-root symlink",
        () => Id65AuthoringBlankCycloramaAggregateCandidateMaterializer
            .MaterializeAsync(request),
        "project root");
    symlinkRejects++;
    Directory.Delete(operationsRoot);
    Directory.CreateDirectory(operationsRoot);
    Require(!Directory.EnumerateFileSystemEntries(symlinkTarget).Any(),
        "A refused symlink boundary mutated its target.");

    Id65AuthoringBlankCycloramaAggregateCandidateFaultPoint[]
        prepublicationFaults =
            Enum.GetValues<
                    Id65AuthoringBlankCycloramaAggregateCandidateFaultPoint>()
                .Where(item =>
                    item is not
                        Id65AuthoringBlankCycloramaAggregateCandidateFaultPoint.None and
                    not Id65AuthoringBlankCycloramaAggregateCandidateFaultPoint
                        .AfterAtomicRenameBeforeCleanup)
                .ToArray();
    Require(prepublicationFaults.Length == 7,
        "The schema-6 prepublication fault matrix changed.");
    foreach (Id65AuthoringBlankCycloramaAggregateCandidateFaultPoint fault in
             prepublicationFaults)
    {
        await ExpectRejectedAsync(
            "injected prepublication fault " + fault,
            () => Id65AuthoringBlankCycloramaAggregateCandidateMaterializer
                .MaterializeForSmokeAsync(request, fault),
            "Injected aggregate candidate materialization fault");
        faultRejects++;
        Require(!Directory.EnumerateDirectories(materializedRoot).Any() &&
                !Directory.EnumerateFileSystemEntries(operationsRoot).Any(),
            "A prepublication fault exposed a candidate or retained owned residue.");
    }

    Id65AuthoringEditorDocumentStoreResult? inCallMutation = null;
    await ExpectRejectedAsync(
        "in-call document mutation",
        () => Id65AuthoringBlankCycloramaAggregateCandidateMaterializer
            .MaterializeForSmokeAsync(
                request,
                Id65AuthoringBlankCycloramaAggregateCandidateFaultPoint
                    .AfterStagedReadbackBeforePrepublishAudit,
                async () =>
                {
                    inCallMutation = await SaveSchema6Async(
                        paths,
                        manifest,
                        Id65AuthoringEditorDocument
                            .CreateRemotePadMobyChoiceTerrainSetBlankCycloramaExact(
                                Id65RemotePadMobyKind.RedGemT21,
                                Id65RemotePadGrassT107Catalog.NorthEastPositionId,
                                [Id65AuthoringTerrainSetCatalog.Tile2Id]),
                        priorEnvelope);
                }),
        "CAS identity");
    failClosedRejects++;
    Require(inCallMutation is not null &&
            !Directory.EnumerateDirectories(materializedRoot).Any() &&
            !Directory.EnumerateFileSystemEntries(operationsRoot).Any(),
        "The in-call CAS mutation was not rejected cleanly.");
    storedDefault = await SaveSchema6Async(
        paths,
        manifest,
        defaultDocument,
        inCallMutation!.EnvelopeCanonicalSha256);
    priorEnvelope = storedDefault.EnvelopeCanonicalSha256;
    request = request with { ExpectedEnvelopeCanonicalSha256 = priorEnvelope };

    Id65AuthoringBlankCycloramaAggregateCandidateMaterializationResult first =
        await Id65AuthoringBlankCycloramaAggregateCandidateMaterializer
            .MaterializeAsync(request);
    positiveCalls++;
    AssertResult(
        first,
        defaultTerrainIds,
        defaultKind,
        reused: false,
        filesystemMutated: true,
        candidateWritten: true,
        renamedThisCall: true);
    FileState imageBeforeReuse = SnapshotFile(first.Paths.CandidateImagePath);
    FileState candidateCueBeforeReuse = SnapshotFile(first.Paths.CandidateCuePath);
    FileState receiptBeforeReuse = SnapshotFile(first.Paths.CandidateReceiptPath);
    Id65AuthoringBlankCycloramaAggregateCandidateMaterializationResult reused =
        await Id65AuthoringBlankCycloramaAggregateCandidateMaterializer
            .MaterializeAsync(request);
    positiveCalls++;
    AssertResult(
        reused,
        defaultTerrainIds,
        defaultKind,
        reused: true,
        filesystemMutated: false,
        candidateWritten: false,
        renamedThisCall: false);
    Require(reused.Receipt == first.Receipt &&
            SnapshotFile(first.Paths.CandidateImagePath) == imageBeforeReuse &&
            SnapshotFile(first.Paths.CandidateCuePath) == candidateCueBeforeReuse &&
            SnapshotFile(first.Paths.CandidateReceiptPath) == receiptBeforeReuse,
        "Immutable content-addressed reuse changed candidate artifacts.");

    byte[] exactReceipt = await File.ReadAllBytesAsync(first.Paths.CandidateReceiptPath);
    byte[] tamperedReceipt = exactReceipt.ToArray();
    tamperedReceipt[^2] ^= 1;
    await File.WriteAllBytesAsync(first.Paths.CandidateReceiptPath, tamperedReceipt);
    await ExpectRejectedAsync(
        "tampered receipt",
        () => Id65AuthoringBlankCycloramaAggregateCandidateMaterializer
            .MaterializeAsync(request),
        "receipt");
    tamperRejects++;
    await File.WriteAllBytesAsync(first.Paths.CandidateReceiptPath, exactReceipt);

    byte[] exactCue = await File.ReadAllBytesAsync(first.Paths.CandidateCuePath);
    byte[] tamperedCue = exactCue.ToArray();
    tamperedCue[^2] ^= 1;
    await File.WriteAllBytesAsync(first.Paths.CandidateCuePath, tamperedCue);
    await ExpectRejectedAsync(
        "tampered cue",
        () => Id65AuthoringBlankCycloramaAggregateCandidateMaterializer
            .MaterializeAsync(request),
        "CUE");
    tamperRejects++;
    await File.WriteAllBytesAsync(first.Paths.CandidateCuePath, exactCue);

    long imageTamperOffset = new FileInfo(first.Paths.CandidateImagePath).Length - 1;
    byte exactImageByte = ToggleByte(first.Paths.CandidateImagePath, imageTamperOffset);
    await ExpectRejectedAsync(
        "tampered image",
        () => Id65AuthoringBlankCycloramaAggregateCandidateMaterializer
            .MaterializeAsync(request),
        "outside WAD80");
    tamperRejects++;
    WriteByte(first.Paths.CandidateImagePath, imageTamperOffset, exactImageByte);
    Id65AuthoringBlankCycloramaAggregateCandidateMaterializationResult
        restoredReuse =
            await Id65AuthoringBlankCycloramaAggregateCandidateMaterializer
                .MaterializeAsync(request);
    positiveCalls++;
    AssertResult(
        restoredReuse,
        defaultTerrainIds,
        defaultKind,
        reused: true,
        filesystemMutated: false,
        candidateWritten: false,
        renamedThisCall: false);

    Directory.Delete(first.Paths.CandidateDirectoryPath, recursive: true);
    await ExpectRejectedAsync(
        "post-rename pre-cleanup fault",
        () => Id65AuthoringBlankCycloramaAggregateCandidateMaterializer
            .MaterializeForSmokeAsync(
                request,
                Id65AuthoringBlankCycloramaAggregateCandidateFaultPoint
                    .AfterAtomicRenameBeforeCleanup),
        "Injected aggregate candidate materialization fault");
    faultRejects++;
    Require(Directory.Exists(first.Paths.CandidateDirectoryPath) &&
            Directory.EnumerateDirectories(operationsRoot).Count() == 1,
        "The post-rename fault did not leave one recoverable journal.");
    Id65AuthoringBlankCycloramaAggregateCandidateMaterializationResult recovered =
        await Id65AuthoringBlankCycloramaAggregateCandidateMaterializer
            .MaterializeAsync(request);
    positiveCalls++;
    AssertResult(
        recovered,
        defaultTerrainIds,
        defaultKind,
        reused: true,
        filesystemMutated: true,
        candidateWritten: false,
        renamedThisCall: false);
    Require(recovered.Receipt == first.Receipt &&
            !Directory.EnumerateFileSystemEntries(operationsRoot).Any(),
        "Recovery changed the immutable candidate or retained its journal.");

    Id65AuthoringBlankCycloramaAggregateCandidateWorkspaceResult publicResult =
        await Id65AuthoringBlankCycloramaAggregateCandidateWorkspaceMaterializer
            .MaterializeExactAsync(
                paths,
                manifest,
                priorEnvelope,
                CancellationToken.None);
    positiveCalls++;
    Require(publicResult.ReusedExistingCandidate &&
            !publicResult.CandidateArtifactsWrittenThisCall &&
            !publicResult.FileSystemMutationPerformed &&
            publicResult.CandidateDirectoryPath == recovered.Paths.CandidateDirectoryPath &&
            publicResult.DocumentEnvelopeSha256 ==
                recovered.Receipt.DocumentEnvelopeSha256 &&
            publicResult.DocumentCanonicalSha256 ==
                recovered.Receipt.DocumentCanonicalSha256 &&
            publicResult.StaticAuthoringProjectionSha256 ==
                recovered.Receipt.StaticAuthoringProjectionSha256 &&
            publicResult.TerrainManifestSha256 ==
                recovered.Receipt.TerrainManifestSha256 &&
            publicResult.ActiveTerrainUnitIds.SequenceEqual(defaultTerrainIds) &&
            publicResult.MobyKind == defaultKind &&
            publicResult.PositionId ==
                Id65RemotePadGrassT107Catalog.CenterPositionId &&
            publicResult.NativeInputSetSha256 ==
                recovered.Receipt.NativeInputSetSha256 &&
            publicResult.AggregatePlanSha256 == recovered.Receipt.AggregatePlanSha256 &&
            publicResult.PresetId == Id65AuthoringBlankCycloramaCatalog.BlankSlatePresetId &&
            publicResult.ColorHex == Id65AuthoringBlankCycloramaCatalog.BlankSlateColorHex &&
            publicResult.CycloramaModelOffset == recovered.Plan.CycloramaModelOffset &&
            publicResult.CycloramaRow80Offset == recovered.Plan.CycloramaRow80Offset &&
            publicResult.CycloramaWadOffset == recovered.Plan.CycloramaWadOffset &&
            publicResult.CycloramaByteLength == recovered.Plan.CycloramaByteLength &&
            publicResult.PaletteWordCount == 727 &&
            publicResult.PaletteOwnedRangeCount == 727 &&
            publicResult.OutputCycloramaSha256 == recovered.Plan.OutputCycloramaSha256 &&
            publicResult.GeometrySha256 == recovered.Plan.GeometrySha256 &&
            publicResult.CommandByteSha256 == recovered.Plan.CommandByteSha256 &&
            publicResult.NonPaletteSha256 == recovered.Plan.NonPaletteSha256 &&
            publicResult.OutputImageSha256 == recovered.Receipt.OutputImageSha256 &&
            publicResult.ReceiptContentSha256 ==
                recovered.Receipt.ReceiptContentSha256 &&
            publicResult.ExactStoreReloadCasVerifiedThreeTimes &&
            publicResult.ExactEnvelopeCasVerifiedThreeTimes &&
            publicResult.ExactStaticAuthoringProjectionReloadVerifiedThreeTimes &&
            publicResult.ExactNativeInputGenerationRecapturedThreeTimes &&
            publicResult.ExactV3TemplateRecapturedThreeTimes &&
            publicResult.ExactLockedBaseRecapturedThreeTimes &&
            publicResult.Schema6CompilerPinsVerified &&
            publicResult.BlankCycloramaIntentReadbackVerified &&
            publicResult.CycloramaLayoutReadbackVerified &&
            publicResult.EveryPaletteWordReadbackVerified &&
            publicResult.CycloramaGeometryReadbackVerified &&
            publicResult.CycloramaCommandBytesReadbackVerified &&
            publicResult.CycloramaNonPaletteReadbackVerified &&
            publicResult.MobyChoicePositionReadbackVerified &&
            publicResult.FourTerrainTopologiesVerified &&
            publicResult.T88ExcludedVerified &&
            publicResult.RepairedT13995SupportVerified &&
            publicResult.ExactRow80ReadbackVerified &&
            publicResult.ExactInverseReadbackVerified &&
            publicResult.ProtectedComplementVerified &&
            publicResult.Wad79Preserved &&
            publicResult.RetailTownSquareWad15Preserved &&
            publicResult.RetailTownSquareWad16Preserved &&
            publicResult.RetailArtisansWad9Preserved &&
            publicResult.RetailArtisansWad10Preserved &&
            publicResult.RetailTownSquareSkyBlockPreserved &&
            publicResult.RetailArtisansLinkedSkyBlockPreserved &&
            publicResult.ExecutablePreserved &&
            publicResult.RawSectorIntegrityVerified &&
            publicResult.PhysicalComplementVerified &&
            publicResult.AtomicNoReplacePublicationVerified &&
            publicResult.OwnedTemporaryResidueRemoved &&
            publicResult.RequiresExactRemotePadSchema6Document &&
            publicResult.RequiresFocusedRuntimeValidation &&
            !publicResult.CarriesAppOrUiAuthority &&
            !publicResult.CarriesCreateBinAuthority &&
            !publicResult.NormalRetailCreateBinComposed &&
            !publicResult.RuntimeCandidateAuthorized && !publicResult.RuntimeProven &&
            !publicResult.ReleaseIntegrated && !publicResult.PromotionAuthorized &&
            !publicResult.Publishable &&
            !publicResult.CurrentOnDiskFreshnessVerified &&
            !publicResult.HostileFilesystemRaceSafe &&
            publicResult.RequiresTrustedExclusiveWorkspaceDirectory &&
            !publicResult.CommonWorkspaceLeaseOwned,
        "The public schema-6 workspace facade crossed its offline boundary.");

    Require(SnapshotFile(paths.LockedBaseImagePath) == workspaceImageBefore &&
            SnapshotFile(paths.LockedBaseCuePath) == workspaceCueBefore &&
            SnapshotFile(paths.ManifestPath) == workspaceManifestBefore,
        "The schema-6 candidate materializer changed a locked workspace source.");
    VerifyApiAndTopology(repositoryRoot);
    Console.WriteLine(
        "ID65_AUTHORING_BLANK_CYCLORAMA_AGGREGATE_CANDIDATE_MATERIALIZER_SMOKE_OK " +
        $"publications={publications} positive={positiveCalls} " +
        $"redGemReuse={redGemReuseChecks} " +
        $"rejects={failClosedRejects} faultRejects={faultRejects} " +
        $"tamperRejects={tamperRejects} symlinkRejects={symlinkRejects} " +
        $"image={first.Receipt.OutputImageSha256} " +
        $"physical={first.Receipt.ChangedPhysicalImageBytes} " +
        $"changedSectors={first.Receipt.ChangedRawSectorCount} " +
        $"receipt={first.Receipt.ReceiptContentSha256} " +
        "runtime=0 createBin=0 release=0");
}
finally
{
    if (Directory.Exists(smokeRoot))
        Directory.Delete(smokeRoot, recursive: true);
}

Require(SnapshotFile(lockedImagePath) == lockedBefore &&
        SnapshotFile(lockedCuePath) == cueBefore &&
        SnapshotDirectory(Path.GetDirectoryName(lockedImagePath)!)
            .SequenceEqual(lockedDirectoryBefore),
    "The schema-6 candidate materializer smoke changed its locked fixture.");

static Task<Id65AuthoringEditorDocumentStoreResult> SaveSchema6Async(
    UnusedLevel65BlankLevelLabWorkspacePaths paths,
    UnusedLevel65BlankLevelLabManifest manifest,
    Id65AuthoringEditorDocument document,
    string? expectedEnvelopeCanonicalSha256) =>
    Id65AuthoringEditorDocumentStore.SaveAsync(
        paths,
        manifest,
        document,
        new Id65AuthoringEditorDocumentProjectionInputs(null),
        expectedEnvelopeCanonicalSha256);

static void AssertResult(
    Id65AuthoringBlankCycloramaAggregateCandidateMaterializationResult result,
    IReadOnlyList<string> activeTerrainUnitIds,
    Id65RemotePadMobyKind kind,
    bool reused,
    bool filesystemMutated,
    bool candidateWritten,
    bool renamedThisCall,
    string? expectedPositionId = null)
{
    expectedPositionId ??= Id65RemotePadGrassT107Catalog.CenterPositionId;
    Id65AuthoringTerrainSetManifest terrainManifest =
        Id65AuthoringTerrainSetCatalog.Create(activeTerrainUnitIds);
    Require(result.Plan.Document.SchemaVersion ==
                Id65AuthoringEditorDocument.RemotePadMobyChoiceTerrainSetBlankCycloramaSchemaVersion &&
            result.Plan.ActiveTerrainUnitIds.SequenceEqual(activeTerrainUnitIds) &&
            result.Plan.TerrainUnitCount == activeTerrainUnitIds.Count &&
            result.Plan.TerrainManifestSha256 == terrainManifest.CanonicalSha256 &&
            result.Plan.PresetId ==
                Id65AuthoringBlankCycloramaCatalog.BlankSlatePresetId &&
            result.Plan.ColorHex ==
                Id65AuthoringBlankCycloramaCatalog.BlankSlateColorHex &&
            result.Plan.PaletteWordCount == 727 &&
            result.Plan.PaletteOwnedRangeCount == 727 &&
            result.Plan.MobyKind == kind &&
            result.Plan.MobyPositionId ==
                expectedPositionId &&
            result.Receipt.ActiveTerrainUnitIdsCanonical ==
                string.Join('\n', activeTerrainUnitIds) &&
            result.Receipt.TerrainManifestSha256 == terrainManifest.CanonicalSha256 &&
            result.Receipt.MobyKindId == Id65RemotePadMobyChoiceCatalog.GetKindId(kind) &&
            result.Receipt.SelectedExactRowSha256 ==
                Id65RemotePadMobyChoiceCatalog.GetSelectedExactRowSha256(
                    kind,
                    result.Plan.MobyPositionId) &&
            result.Receipt.OutputRow80Sha256 == result.Plan.OutputRow80Sha256 &&
            result.Receipt.ChangedLogicalWadBytes == result.Plan.ChangedByteCount &&
            result.Receipt.ChangedRawSectorCount is > 0 and <=
                Id65AuthoringBlankCycloramaAggregateCandidateMaterializer
                    .RebuiltRawSectorCount &&
            result.ReusedExistingCandidate == reused &&
            result.CandidateArtifactsWrittenThisCall == candidateWritten &&
            result.FileSystemMutationPerformed == filesystemMutated &&
            result.WritesDiscImage == candidateWritten &&
            result.WritesBin == candidateWritten && result.WritesCue == candidateWritten &&
            result.AtomicNoReplaceRenameCompletedThisCall == renamedThisCall &&
            result.VerifiedAtomicNoReplacePublicationProvenance &&
            result.OwnedTemporaryResidueRemoved &&
            !result.FinderHandoffPerformed && !result.AppIntegrated &&
            !result.UiIntegrated && !result.CreateBinEnabled &&
            !result.NormalCreateBinEnabled &&
            !result.RuntimeCandidateAuthorized && !result.RuntimeProven &&
            !result.ReleaseIntegrated && !result.PromotionAuthorized &&
            !result.Publishable && !result.CurrentOnDiskFreshnessVerified &&
            !result.HostileFilesystemRaceSafe &&
            result.RequiresTrustedExclusiveWorkspaceDirectory &&
            !result.CommonWorkspaceLeaseOwned &&
            File.Exists(result.Paths.CandidateImagePath) &&
            File.Exists(result.Paths.CandidateCuePath) &&
            File.Exists(result.Paths.CandidateReceiptPath) &&
            new FileInfo(result.Paths.CandidateImagePath).Length ==
                Id65AuthoringBlankCycloramaAggregateCandidateMaterializer
                    .ExpectedSourceImageByteLength &&
            result.Plan.Destination.SourceImageSha256 ==
                Id65RewardCensusContract.LockedSourceImageSha256,
        "The schema-6 candidate result changed its exact physical boundary.");
    AssertResidentT21Candidate(result, kind);
    AssertCycloramaCandidate(result, activeTerrainUnitIds.Count);

    Id65AuthoringBlankCycloramaAggregateCandidateReceipt receipt = result.Receipt;
    Require(receipt.ExactStoreReloadCasVerifiedThreeTimes &&
            receipt.ExactStaticAuthoringProjectionReloadVerifiedThreeTimes &&
            receipt.ExactNativeInputGenerationRecapturedThreeTimes &&
            receipt.ExactV3TemplateRecapturedThreeTimes &&
            receipt.ExactLockedBaseRecapturedThreeTimes &&
            receipt.Schema6CompilerPinsVerified &&
            receipt.BlankCycloramaIntentReadbackVerified &&
            receipt.CycloramaLayoutReadbackVerified &&
            receipt.EveryPaletteWordReadbackVerified &&
            receipt.CycloramaGeometryReadbackVerified &&
            receipt.CycloramaCommandBytesReadbackVerified &&
            receipt.CycloramaNonPaletteReadbackVerified &&
            receipt.MobyChoicePositionReadbackVerified &&
            receipt.FourTerrainTopologiesVerified && receipt.T88ExcludedVerified &&
            receipt.RepairedT13995SupportVerified &&
            receipt.ExactRow80ReadbackVerified &&
            receipt.ExactInverseReadbackVerified &&
            receipt.ProtectedComplementVerified && receipt.Wad79Preserved &&
            receipt.RetailTownSquareWad15Preserved &&
            receipt.RetailTownSquareWad16Preserved &&
            receipt.RetailArtisansWad9Preserved &&
            receipt.RetailArtisansWad10Preserved &&
            receipt.RetailTownSquareSkyBlockPreserved &&
            receipt.RetailArtisansLinkedSkyBlockPreserved &&
            receipt.ExecutablePreserved && receipt.RawSectorIntegrityVerified &&
            receipt.PhysicalComplementVerified && receipt.SourceLockedBasePreserved &&
            receipt.AtomicNoReplaceRenameCompleted && receipt.ContainsFullDiscImage &&
            receipt.ContainsCue && receipt.WorkspaceCandidateWriterAuthorized &&
            !receipt.RuntimeCandidateAuthorized && !receipt.RuntimeProven &&
            !receipt.SaveAuthorized && !receipt.SavePersistenceProven &&
            !receipt.TreasureTotal200Proven && !receipt.MusicLongPlayProven &&
            !receipt.ExitDestination65Proven && !receipt.AppIntegrated &&
            !receipt.UiIntegrated && !receipt.CreateBinEnabled &&
            !receipt.NormalCreateBinEnabled &&
            !receipt.RetailLevelMutationAuthorized && !receipt.ReleaseIntegrated &&
            !receipt.ReleaseAuthorized && !receipt.PromotionAuthorized &&
            !receipt.Publishable && !receipt.CurrentOnDiskFreshnessVerified &&
            !receipt.HostileFilesystemRaceSafe &&
            receipt.RequiresTrustedExclusiveWorkspaceDirectory &&
            !receipt.CommonWorkspaceLeaseOwned,
        "The schema-6 receipt overclaimed an unproven capability.");
    Require(result.Plan.PureInMemory &&
            result.Plan.FullExactSchema6DocumentRequired &&
            result.Plan.CompilesBlankCycloramaFromLockedInputs &&
            result.Plan.BuildsSchema5BaselineInternally &&
            result.Plan.EveryPaletteWordOwnedExactlyOnce &&
            result.Plan.PaletteCommandBytesPreserved &&
            result.Plan.FrozenCycloramaGeometryVerified &&
            result.Plan.Wad80SoleWritableDestination &&
            result.Plan.Wad79Protected &&
            result.Plan.RetailTownSquareWad15And16Protected &&
            result.Plan.RetailArtisansWad9And10Protected &&
            result.Plan.ExecutableProtected &&
            !result.Plan.CallsNativeSkyPatchExporter &&
            !result.Plan.LinkedPrimarySkyCopiesTouched &&
            !result.Plan.MaterializerIntegrated &&
            !result.Plan.WritesFileSystem && !result.Plan.WritesDiscImage &&
            !result.Plan.WritesBin && !result.Plan.WritesCue &&
            !result.Plan.AppIntegrated && !result.Plan.UiIntegrated &&
            !result.Plan.CreateBinEnabled && !result.Plan.NormalCreateBinEnabled &&
            !result.Plan.RuntimeCandidateAuthorized && !result.Plan.RuntimeProven &&
            !result.Plan.ReleaseIntegrated &&
            !result.Plan.PromotionAuthorized && !result.Plan.Publishable &&
            !result.Plan.CurrentOnDiskFreshnessVerified &&
            !result.Plan.HostileFilesystemRaceSafe &&
            result.Plan.RequiresTrustedExclusiveWorkspaceDirectory &&
            !result.Plan.CommonWorkspaceLeaseOwned,
        "The consumed in-memory aggregate plan gained materialization authority.");
}

static void AssertCycloramaCandidate(
    Id65AuthoringBlankCycloramaAggregateCandidateMaterializationResult result,
    int activeTerrainUnitCount)
{
    byte[] outputRow = ReadWad(
        result.Paths.CandidateImagePath,
        Id65AuthoringBlankCycloramaAggregateCandidateMaterializer
            .TargetDataWadOffset,
        Id65AuthoringBlankCycloramaAggregateCandidateMaterializer
            .TargetDataByteLength);
    byte[] lockedSourceRow =
        Id65AuthoringBlankCycloramaAggregatePlanProjector.ApplyExact(
            result.Plan,
            outputRow,
            reverse: true);
    Spyro1SkyBlockLayout layout = result.Plan.OutputCycloramaLayout;
    int[] paletteOffsets = new[]
        {
            checked(result.Plan.CycloramaRow80Offset +
                layout.BackgroundColorOffset)
        }
        .Concat(layout.Parts.SelectMany(part =>
            Enumerable.Range(0, part.ColorCount).Select(index => checked(
                result.Plan.CycloramaRow80Offset +
                part.ColorTableOffset + index * 4))))
        .Order()
        .ToArray();
    byte[] cyclorama = outputRow.AsSpan(
        result.Plan.CycloramaRow80Offset,
        result.Plan.CycloramaByteLength).ToArray();
    Spyro1SkyBlockLayout reparsed =
        Spyro1SkyBlockAnalyzer.AnalyzeStandaloneSkyBlock(cyclorama);
    string outputNonPalette = HashOutsidePaletteRgb(outputRow, paletteOffsets);
    string lockedSourceNonPalette =
        HashOutsidePaletteRgb(lockedSourceRow, paletteOffsets);
    string outputCommands = HashPaletteCommands(outputRow, paletteOffsets);
    string lockedSourceCommands =
        HashPaletteCommands(lockedSourceRow, paletteOffsets);
    string lockedSliceSha256 = Convert.ToHexString(
        SHA256.HashData(lockedSourceRow.AsSpan(
            result.Plan.CycloramaRow80Offset,
            result.Plan.CycloramaByteLength)));

    Require(
        paletteOffsets.Length == 727 &&
        reparsed.Sha256 == result.Plan.OutputCycloramaSha256 &&
        reparsed.GeometrySha256 == result.Plan.GeometrySha256 &&
        outputNonPalette == result.Plan.NonPaletteSha256 &&
        outputCommands == result.Plan.CommandByteSha256 &&
        lockedSourceNonPalette != result.Plan.NonPaletteSha256 &&
        lockedSourceCommands != result.Plan.CommandByteSha256 &&
        (activeTerrainUnitCount > 1 ||
            lockedSliceSha256 != result.Plan.BaselineCycloramaSha256),
        "The candidate smoke conflated the locked V3 row with the internally derived schema-5 cyclorama baseline.");
}

static string HashOutsidePaletteRgb(
    ReadOnlySpan<byte> bytes,
    IReadOnlyList<int> paletteOffsets)
{
    using IncrementalHash hash =
        IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    int cursor = 0;
    foreach (int offset in paletteOffsets)
    {
        if (offset > cursor)
            hash.AppendData(bytes.Slice(cursor, offset - cursor));
        cursor = offset + 3;
    }
    if (cursor < bytes.Length)
        hash.AppendData(bytes[cursor..]);
    return Convert.ToHexStringLower(hash.GetHashAndReset());
}

static string HashPaletteCommands(
    ReadOnlySpan<byte> bytes,
    IReadOnlyList<int> paletteOffsets)
{
    byte[] commands = new byte[paletteOffsets.Count];
    for (int index = 0; index < paletteOffsets.Count; index++)
        commands[index] = bytes[paletteOffsets[index] + 3];
    return Convert.ToHexStringLower(SHA256.HashData(commands));
}

static void AssertResidentT21Candidate(
    Id65AuthoringBlankCycloramaAggregateCandidateMaterializationResult result,
    Id65RemotePadMobyKind kind)
{
    int t21RowOffset = Id65AuthoringRemotePadMobyChoicePhysicalCompiler
        .ResidentT21RowOffset;
    int t21RowLength = Id65RemotePadRedGemT21Catalog.RowByteLength;
    int t21RowEnd = checked(t21RowOffset + t21RowLength);
    int xyzOffset = Id65AuthoringRemotePadMobyChoicePhysicalCompiler
        .ResidentT21CoordinateOffset;
    int xyzLength = Id65AuthoringRemotePadMobyChoicePhysicalCompiler
        .ResidentT21CoordinateByteLength;

    bool Related(Id65AuthoringBlankCycloramaAggregateOwnedRange range)
    {
        long end = range.DataRelativeOffset + (long)range.ByteLength;
        return range.DataRelativeOffset < t21RowEnd && end > t21RowOffset ||
            range.StableId ==
                Id65AuthoringRemotePadTerrainSetPhysicalCompiler
                    .ResidentT21XyzOwnedRangeStableId ||
            range.OwnerId == Id65RemotePadRedGemT21Catalog.PlacementId ||
            range.Domain ==
                Id65AuthoringBlankCycloramaAggregateRangeDomain.RedGem;
    }

    bool Exact(Id65AuthoringBlankCycloramaAggregateOwnedRange range) =>
        range.StableId ==
            Id65AuthoringRemotePadTerrainSetPhysicalCompiler
                .ResidentT21XyzOwnedRangeStableId &&
        range.OwnerId == Id65RemotePadRedGemT21Catalog.PlacementId &&
        range.Domain ==
            Id65AuthoringBlankCycloramaAggregateRangeDomain.RedGem &&
        range.TargetWadEntry ==
            Id65AuthoringBlankCycloramaAggregateCandidateMaterializer
                .TargetDataWadEntry &&
        range.DataRelativeOffset == xyzOffset &&
        range.WadOffset ==
            Id65AuthoringBlankCycloramaAggregateCandidateMaterializer
                .TargetDataWadOffset + xyzOffset &&
        range.ByteLength == xyzLength;

    Id65AuthoringBlankCycloramaAggregateOwnedRange[] owned =
        result.Plan.OwnedRanges.Where(Related).ToArray();
    Id65AuthoringBlankCycloramaAggregateOwnedRange[] inverse =
        result.Plan.InverseRanges.Where(Related).ToArray();
    byte[] exactSourceRow = Convert.FromHexString(
        Id65RemotePadRedGemT21Catalog.SourceRowExactHex);
    byte[] expectedOutputRow = kind == Id65RemotePadMobyKind.RedGemT21
        ? Id65RemotePadRedGemT21Catalog.CreateExactRowBytes(
            result.Plan.MobyPositionId)
        : exactSourceRow;
    byte[] candidateRow = ReadWad(
        result.Paths.CandidateImagePath,
        Id65AuthoringBlankCycloramaAggregateCandidateMaterializer
            .TargetDataWadOffset + t21RowOffset,
        t21RowLength);

    bool exactLedger = kind == Id65RemotePadMobyKind.RedGemT21
        ? owned.Length == 1 && inverse.Length == 1 &&
            Exact(owned[0]) && Exact(inverse[0]) &&
            owned[0].CopyBefore().AsSpan().SequenceEqual(exactSourceRow.AsSpan(
                Id65RemotePadRedGemT21Catalog.RowCoordinateOffset,
                xyzLength)) &&
            owned[0].CopyAfter().AsSpan().SequenceEqual(expectedOutputRow.AsSpan(
                Id65RemotePadRedGemT21Catalog.RowCoordinateOffset,
                xyzLength)) &&
            inverse[0].CopyBefore().SequenceEqual(owned[0].CopyAfter()) &&
            inverse[0].CopyAfter().SequenceEqual(owned[0].CopyBefore())
        : owned.Length == 0 && inverse.Length == 0;
    Require(
        Convert.ToHexStringLower(SHA256.HashData(exactSourceRow)) ==
            Id65RemotePadRedGemT21Catalog.SourceRowSha256 &&
        exactLedger && candidateRow.SequenceEqual(expectedOutputRow),
        "The materialized schema-6 candidate lost exact full-row resident-T21 readback.");
}

static byte[] ReadWad(string imagePath, long wadOffset, int byteLength)
{
    DiscLayout layout = DiscImage.DetectLayout(imagePath);
    using FileStream image = File.OpenRead(imagePath);
    DiscFileRecord wad = DiscImage.FindRootFileRecord(
        image,
        layout,
        name => string.Equals(name, "WAD.WAD", StringComparison.OrdinalIgnoreCase));
    Require(wad.Lba ==
                Id65AuthoringBlankCycloramaAggregateCandidateMaterializer.WadLba &&
            wad.Size ==
                Id65AuthoringBlankCycloramaAggregateCandidateMaterializer
                    .ExpectedWadByteLength,
        "The schema-6 candidate WAD.WAD layout changed.");
    return DiscImage.ReadFileBytes(image, layout, wad.Lba, wadOffset, byteLength);
}

static void VerifyApiAndTopology(string repositoryRoot)
{
    MethodInfo[] internalMaterializerMethods =
        typeof(Id65AuthoringBlankCycloramaAggregateCandidateMaterializer)
            .GetMethods(BindingFlags.Public | BindingFlags.Static |
                BindingFlags.DeclaredOnly);
    Require(internalMaterializerMethods.Length == 1 &&
            internalMaterializerMethods[0].Name == "MaterializeAsync" &&
            internalMaterializerMethods[0].GetParameters()
                .Select(item => item.ParameterType)
                .SequenceEqual(new[]
                {
                    typeof(
                        Id65AuthoringBlankCycloramaAggregateCandidateMaterializationRequest),
                    typeof(CancellationToken)
                }),
        "The schema-6 internal materializer API surface changed.");
    PropertyInfo[] requestProperties =
        typeof(Id65AuthoringBlankCycloramaAggregateCandidateMaterializationRequest)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance);
    Require(requestProperties.Select(item => item.Name).SequenceEqual(new[]
            {
                "Paths",
                "ExpectedManifest",
                "ExpectedEnvelopeCanonicalSha256"
            }) &&
            requestProperties.All(item =>
                item.PropertyType != typeof(byte[]) &&
                item.PropertyType !=
                    typeof(Id65AuthoringBlankCycloramaAggregatePlan)),
        "The schema-6 request stopped accepting only workspace/CAS identity.");
    MethodInfo[] facadeMethods =
        typeof(
                Id65AuthoringBlankCycloramaAggregateCandidateWorkspaceMaterializer)
            .GetMethods(BindingFlags.Public | BindingFlags.Static |
                BindingFlags.DeclaredOnly);
    Require(facadeMethods.Length == 1 &&
            facadeMethods[0].Name == "MaterializeExactAsync" &&
            facadeMethods[0].GetParameters()
                .Select(item => item.ParameterType)
                .SequenceEqual(new[]
                {
                    typeof(UnusedLevel65BlankLevelLabWorkspacePaths),
                    typeof(UnusedLevel65BlankLevelLabManifest),
                    typeof(string),
                    typeof(CancellationToken)
                }),
        "The public schema-6 facade signature changed.");

    string sourceRoot = Path.Combine(repositoryRoot, "src");
    string materializerRelative = Path.Combine(
        "src",
        "Spyro.Editor.Core",
        "Persistence",
        "Id65AuthoringBlankCycloramaAggregateCandidateMaterializer.cs");
    string facadeRelative = Path.Combine(
        "src",
        "Spyro.Editor.Core",
        "Persistence",
        "Id65AuthoringBlankCycloramaAggregateCandidateWorkspaceMaterializer.cs");
    string smokeRelative = Path.Combine(
        "src",
        "Spyro.Editor.Id65AuthoringBlankCycloramaAggregateCandidateMaterializerSmoke",
        "Program.cs");
    List<(string Relative, string Text)> sources = Directory
        .EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
        .Where(path => !path.Split(Path.DirectorySeparatorChar)
            .Any(segment => segment is "bin" or "obj"))
        .Select(path => (
            Path.GetRelativePath(repositoryRoot, path),
            File.ReadAllText(path)))
        .ToList();
    Regex declaration = new(
        @"\binternal\s+static\s+class\s+Id65AuthoringBlankCycloramaAggregateCandidateMaterializer\b",
        RegexOptions.CultureInvariant);
    Require(sources.Sum(item => declaration.Matches(item.Text).Count) == 1 &&
            declaration.IsMatch(sources.Single(item =>
                item.Relative == materializerRelative).Text),
        "The isolated schema-6 materializer declaration topology changed.");
    Regex internalCall = new(
        @"\bId65AuthoringBlankCycloramaAggregateCandidateMaterializer\s*\.\s*Materialize(?:ForSmoke)?Async\s*\(",
        RegexOptions.CultureInvariant);
    string[] internalCallers = sources
        .Where(item => internalCall.IsMatch(item.Text))
        .Select(item => item.Relative)
        .Order(StringComparer.Ordinal)
        .ToArray();
    Require(internalCallers.SequenceEqual(new[] { facadeRelative, smokeRelative }),
        "The schema-6 candidate writer gained an unauthorized caller: " +
        string.Join(", ", internalCallers));
    Regex facadeCall = new(
        @"\bId65AuthoringBlankCycloramaAggregateCandidateWorkspaceMaterializer\s*\.\s*MaterializeExactAsync\s*\(",
        RegexOptions.CultureInvariant);
    string[] facadeCallers = sources
        .Where(item => facadeCall.IsMatch(item.Text))
        .Select(item => item.Relative)
        .Order(StringComparer.Ordinal)
        .ToArray();
    string appFacadeRelative = Path.Combine(
        "src",
        "Spyro.Editor.App",
        "Views",
        "MainWindow.Id65BlankLab.cs");
    Require(facadeCallers.SequenceEqual(
            new[] { appFacadeRelative, smokeRelative }.Order(StringComparer.Ordinal)),
        "The public schema-6 candidate facade gained an unauthorized caller: " +
        string.Join(", ", facadeCallers));
    string[] appCandidateOwners = sources.Where(item =>
            item.Relative.StartsWith(
                Path.Combine("src", "Spyro.Editor.App") +
                    Path.DirectorySeparatorChar,
                StringComparison.Ordinal) &&
            item.Text.Contains(
                "Id65AuthoringBlankCycloramaAggregateCandidate",
                StringComparison.Ordinal))
        .Select(item => item.Relative)
        .Order(StringComparer.Ordinal)
        .ToArray();
    Require(appCandidateOwners.SequenceEqual(new[] { appFacadeRelative }),
        "The schema-6 candidate facade leaked beyond its one explicit App/UI route: " +
        string.Join(", ", appCandidateOwners));
}

static async Task ExpectRejectedAsync(
    string label,
    Func<Task> action,
    string requiredText)
{
    Exception? captured = null;
    try
    {
        await action();
    }
    catch (Exception exception) when (
        exception is InvalidDataException or InvalidOperationException or
            IOException or NotSupportedException)
    {
        captured = exception;
    }
    Require(captured is not null &&
            captured.ToString().Contains(
                requiredText,
                StringComparison.OrdinalIgnoreCase),
        $"The negative case '{label}' was accepted or returned the wrong refusal: " +
        captured);
}

static byte ToggleByte(string path, long offset)
{
    using FileStream stream = new(
        path,
        FileMode.Open,
        FileAccess.ReadWrite,
        FileShare.None);
    stream.Position = offset;
    int value = stream.ReadByte();
    Require(value >= 0, "The candidate image tamper probe reached end of file.");
    stream.Position = offset;
    stream.WriteByte((byte)(value ^ 1));
    stream.Flush(flushToDisk: true);
    return (byte)value;
}

static void WriteByte(string path, long offset, byte value)
{
    using FileStream stream = new(
        path,
        FileMode.Open,
        FileAccess.Write,
        FileShare.None);
    stream.Position = offset;
    stream.WriteByte(value);
    stream.Flush(flushToDisk: true);
}

static string FindRepositoryRoot(string? supplied)
{
    string currentPath = Path.GetFullPath(supplied ?? Directory.GetCurrentDirectory());
    DirectoryInfo? current = new(currentPath);
    while (current is not null)
    {
        if (Directory.Exists(Path.Combine(current.FullName, ".git")) &&
            Directory.Exists(Path.Combine(current.FullName, "src", "Spyro.Editor.Core")))
        {
            return current.FullName;
        }
        current = current.Parent;
    }
    throw new DirectoryNotFoundException("Could not locate the Spyro Editor repository.");
}

static FileState SnapshotFile(string path)
{
    FileInfo file = new(path);
    using FileStream stream = File.OpenRead(path);
    return new(
        file.Length,
        file.LastWriteTimeUtc.Ticks,
        Convert.ToHexStringLower(SHA256.HashData(stream)));
}

static IReadOnlyList<string> SnapshotDirectory(string path) =>
    Directory.EnumerateFileSystemEntries(path)
        .Select(Path.GetFileName)
        .Where(name => name is not null)
        .Select(name => name!)
        .Order(StringComparer.Ordinal)
        .ToArray();

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

internal sealed record FileState(
    long ByteLength,
    long LastWriteUtcTicks,
    string Sha256);
