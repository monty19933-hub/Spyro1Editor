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
        "The schema-5 Darwin candidate materializer smoke requires macOS.");

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
    "Missing exact schema-5 candidate materializer fixture.");
FileState lockedBefore = SnapshotFile(lockedImagePath);
FileState cueBefore = SnapshotFile(lockedCuePath);
IReadOnlyList<string> lockedDirectoryBefore =
    SnapshotDirectory(Path.GetDirectoryName(lockedImagePath)!);
string smokeRoot = Path.Combine(
    repositoryRoot,
    "_local",
    "Spyro.Editor.Id65AuthoringRemotePadTerrainSetAggregateCandidateMaterializerSmoke-" +
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
        "The schema-5 candidate materializer workspace did not validate.");
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

    Id65AuthoringEditorControlState schema4State =
        Id65AuthoringEditorControlState.CreateRemotePadMobyChoice(
            includeStructuralTerrain: true,
            Id65RemotePadMobyKind.None,
            Id65RemotePadGrassT107Catalog.CenterPositionId);
    Id65AuthoringEditorDocumentWriteResult schema4Write =
        await Id65AuthoringEditorDocumentWorkspaceWriter.SaveExactAsync(
            paths,
            manifest,
            schema4State,
            expectedPriorEnvelopeCanonicalSha256: null);
    IReadOnlyList<string> beforeSchemaReject = SnapshotDirectory(paths.RootPath);
    await ExpectRejectedAsync(
        "schema-4 document",
        () => Id65AuthoringRemotePadTerrainSetAggregateCandidateWorkspaceMaterializer
            .MaterializeExactAsync(
                paths,
                manifest,
                schema4Write.EnvelopeCanonicalSha256),
        "schema-5");
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
    string priorEnvelope = migratedWrite.EnvelopeCanonicalSha256;

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
                    .CreateRemotePadMobyChoiceTerrainSetExact(
                        kind,
                        positionId,
                        unitIds);
            Id65AuthoringEditorDocumentStoreResult stored =
                await SaveSchema5Async(
                    paths,
                    manifest,
                    document,
                    priorEnvelope);
            priorEnvelope = stored.EnvelopeCanonicalSha256;
            Id65AuthoringRemotePadTerrainSetAggregateCandidateMaterializationResult
                result =
                    await Id65AuthoringRemotePadTerrainSetAggregateCandidateMaterializer
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
                $"The {topologyKey}/{kind} candidate aliased another schema-5 state.");
            publications++;
            positiveCalls++;
            if (topologyKey == "zero" &&
                kind == Id65RemotePadMobyKind.RedGemT21)
            {
                Id65AuthoringRemotePadTerrainSetAggregateCandidateMaterializationResult
                    redGemReuse =
                        await Id65AuthoringRemotePadTerrainSetAggregateCandidateMaterializer
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
        Id65AuthoringEditorDocument.CreateRemotePadMobyChoiceTerrainSetExact(
            defaultKind,
            Id65RemotePadGrassT107Catalog.CenterPositionId,
            defaultTerrainIds);
    string staleEnvelope = priorEnvelope;
    Id65AuthoringEditorDocumentStoreResult storedDefault =
        await SaveSchema5Async(
            paths,
            manifest,
            defaultDocument,
            priorEnvelope);
    priorEnvelope = storedDefault.EnvelopeCanonicalSha256;
    await ExpectRejectedAsync(
        "stale caller envelope CAS",
        () => Id65AuthoringRemotePadTerrainSetAggregateCandidateWorkspaceMaterializer
            .MaterializeExactAsync(paths, manifest, staleEnvelope),
        "CAS identity");
    failClosedRejects++;
    await ExpectRejectedAsync(
        "noncanonical caller envelope CAS",
        () => Id65AuthoringRemotePadTerrainSetAggregateCandidateWorkspaceMaterializer
            .MaterializeExactAsync(paths, manifest, "ABC"),
        "canonical");
    failClosedRejects++;

    Id65AuthoringRemotePadTerrainSetAggregateCandidateMaterializationRequest request =
        new(paths, manifest, priorEnvelope);
    await ExpectRejectedAsync(
        "smoke-only entrypoint without a fault",
        () => Id65AuthoringRemotePadTerrainSetAggregateCandidateMaterializer
            .MaterializeForSmokeAsync(
                request,
                Id65AuthoringRemotePadTerrainSetAggregateCandidateFaultPoint.None),
        "requires an injected failure point");
    failClosedRejects++;

    string materializedRoot = Path.Combine(
        paths.RootPath,
        "materialized-remote-pad-terrain-set-schema5-candidates");
    string operationsRoot = Path.Combine(
        paths.RootPath,
        ".remote-pad-terrain-set-schema5-candidate-operations");
    string symlinkTarget = Path.Combine(smokeRoot, "symlink-target");
    Directory.CreateDirectory(symlinkTarget);
    Require(!Directory.EnumerateFileSystemEntries(materializedRoot).Any() &&
            !Directory.EnumerateFileSystemEntries(operationsRoot).Any(),
        "The matrix retained a candidate before negative testing.");
    Directory.Delete(materializedRoot);
    Directory.CreateSymbolicLink(materializedRoot, symlinkTarget);
    await ExpectRejectedAsync(
        "candidate-root symlink",
        () => Id65AuthoringRemotePadTerrainSetAggregateCandidateMaterializer
            .MaterializeAsync(request),
        "project root");
    symlinkRejects++;
    Directory.Delete(materializedRoot);
    Directory.CreateDirectory(materializedRoot);
    Directory.Delete(operationsRoot);
    Directory.CreateSymbolicLink(operationsRoot, symlinkTarget);
    await ExpectRejectedAsync(
        "operation-root symlink",
        () => Id65AuthoringRemotePadTerrainSetAggregateCandidateMaterializer
            .MaterializeAsync(request),
        "project root");
    symlinkRejects++;
    Directory.Delete(operationsRoot);
    Directory.CreateDirectory(operationsRoot);
    Require(!Directory.EnumerateFileSystemEntries(symlinkTarget).Any(),
        "A refused symlink boundary mutated its target.");

    Id65AuthoringRemotePadTerrainSetAggregateCandidateFaultPoint[]
        prepublicationFaults =
            Enum.GetValues<
                    Id65AuthoringRemotePadTerrainSetAggregateCandidateFaultPoint>()
                .Where(item =>
                    item is not
                        Id65AuthoringRemotePadTerrainSetAggregateCandidateFaultPoint.None and
                    not Id65AuthoringRemotePadTerrainSetAggregateCandidateFaultPoint
                        .AfterAtomicRenameBeforeCleanup)
                .ToArray();
    Require(prepublicationFaults.Length == 7,
        "The schema-5 prepublication fault matrix changed.");
    foreach (Id65AuthoringRemotePadTerrainSetAggregateCandidateFaultPoint fault in
             prepublicationFaults)
    {
        await ExpectRejectedAsync(
            "injected prepublication fault " + fault,
            () => Id65AuthoringRemotePadTerrainSetAggregateCandidateMaterializer
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
        () => Id65AuthoringRemotePadTerrainSetAggregateCandidateMaterializer
            .MaterializeForSmokeAsync(
                request,
                Id65AuthoringRemotePadTerrainSetAggregateCandidateFaultPoint
                    .AfterStagedReadbackBeforePrepublishAudit,
                async () =>
                {
                    inCallMutation = await SaveSchema5Async(
                        paths,
                        manifest,
                        Id65AuthoringEditorDocument
                            .CreateRemotePadMobyChoiceTerrainSetExact(
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
    storedDefault = await SaveSchema5Async(
        paths,
        manifest,
        defaultDocument,
        inCallMutation!.EnvelopeCanonicalSha256);
    priorEnvelope = storedDefault.EnvelopeCanonicalSha256;
    request = request with { ExpectedEnvelopeCanonicalSha256 = priorEnvelope };

    Id65AuthoringRemotePadTerrainSetAggregateCandidateMaterializationResult first =
        await Id65AuthoringRemotePadTerrainSetAggregateCandidateMaterializer
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
    Id65AuthoringRemotePadTerrainSetAggregateCandidateMaterializationResult reused =
        await Id65AuthoringRemotePadTerrainSetAggregateCandidateMaterializer
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
        () => Id65AuthoringRemotePadTerrainSetAggregateCandidateMaterializer
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
        () => Id65AuthoringRemotePadTerrainSetAggregateCandidateMaterializer
            .MaterializeAsync(request),
        "CUE");
    tamperRejects++;
    await File.WriteAllBytesAsync(first.Paths.CandidateCuePath, exactCue);

    long imageTamperOffset = new FileInfo(first.Paths.CandidateImagePath).Length - 1;
    byte exactImageByte = ToggleByte(first.Paths.CandidateImagePath, imageTamperOffset);
    await ExpectRejectedAsync(
        "tampered image",
        () => Id65AuthoringRemotePadTerrainSetAggregateCandidateMaterializer
            .MaterializeAsync(request),
        "outside WAD80");
    tamperRejects++;
    WriteByte(first.Paths.CandidateImagePath, imageTamperOffset, exactImageByte);
    Id65AuthoringRemotePadTerrainSetAggregateCandidateMaterializationResult
        restoredReuse =
            await Id65AuthoringRemotePadTerrainSetAggregateCandidateMaterializer
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
        () => Id65AuthoringRemotePadTerrainSetAggregateCandidateMaterializer
            .MaterializeForSmokeAsync(
                request,
                Id65AuthoringRemotePadTerrainSetAggregateCandidateFaultPoint
                    .AfterAtomicRenameBeforeCleanup),
        "Injected aggregate candidate materialization fault");
    faultRejects++;
    Require(Directory.Exists(first.Paths.CandidateDirectoryPath) &&
            Directory.EnumerateDirectories(operationsRoot).Count() == 1,
        "The post-rename fault did not leave one recoverable journal.");
    Id65AuthoringRemotePadTerrainSetAggregateCandidateMaterializationResult recovered =
        await Id65AuthoringRemotePadTerrainSetAggregateCandidateMaterializer
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

    Id65AuthoringRemotePadTerrainSetAggregateCandidateWorkspaceResult publicResult =
        await Id65AuthoringRemotePadTerrainSetAggregateCandidateWorkspaceMaterializer
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
            publicResult.OutputImageSha256 == recovered.Receipt.OutputImageSha256 &&
            publicResult.ReceiptContentSha256 ==
                recovered.Receipt.ReceiptContentSha256 &&
            publicResult.ExactStoreReloadCasVerifiedThreeTimes &&
            publicResult.ExactEnvelopeCasVerifiedThreeTimes &&
            publicResult.ExactStaticAuthoringProjectionReloadVerifiedThreeTimes &&
            publicResult.ExactNativeInputGenerationRecapturedThreeTimes &&
            publicResult.ExactV3TemplateRecapturedThreeTimes &&
            publicResult.ExactLockedBaseRecapturedThreeTimes &&
            publicResult.Schema5CompilerPinsVerified &&
            publicResult.MobyChoicePositionReadbackVerified &&
            publicResult.FourTerrainTopologiesVerified &&
            publicResult.T88ExcludedVerified &&
            publicResult.RepairedT13995SupportVerified &&
            publicResult.ExactRow80ReadbackVerified &&
            publicResult.ExactInverseReadbackVerified &&
            publicResult.ProtectedComplementVerified &&
            publicResult.RetailTownSquarePreserved &&
            publicResult.TargetOverlayPreserved && publicResult.ExecutablePreserved &&
            publicResult.RawSectorIntegrityVerified &&
            publicResult.PhysicalComplementVerified &&
            publicResult.AtomicNoReplacePublicationVerified &&
            publicResult.OwnedTemporaryResidueRemoved &&
            publicResult.RequiresExactRemotePadSchema5Document &&
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
        "The public schema-5 workspace facade crossed its offline boundary.");

    Require(SnapshotFile(paths.LockedBaseImagePath) == workspaceImageBefore &&
            SnapshotFile(paths.LockedBaseCuePath) == workspaceCueBefore &&
            SnapshotFile(paths.ManifestPath) == workspaceManifestBefore,
        "The schema-5 candidate materializer changed a locked workspace source.");
    VerifyApiAndTopology(repositoryRoot);
    Console.WriteLine(
        "ID65_AUTHORING_REMOTE_PAD_TERRAIN_SET_AGGREGATE_CANDIDATE_MATERIALIZER_SMOKE_OK " +
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
    "The schema-5 candidate materializer smoke changed its locked fixture.");

static Task<Id65AuthoringEditorDocumentStoreResult> SaveSchema5Async(
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
    Id65AuthoringRemotePadTerrainSetAggregateCandidateMaterializationResult result,
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
                Id65AuthoringEditorDocument.RemotePadMobyChoiceTerrainSetSchemaVersion &&
            result.Plan.ActiveTerrainUnitIds.SequenceEqual(activeTerrainUnitIds) &&
            result.Plan.TerrainUnitCount == activeTerrainUnitIds.Count &&
            result.Plan.TerrainManifestSha256 == terrainManifest.CanonicalSha256 &&
            result.Plan.MobyKind == kind && result.Plan.Placement.Kind == kind &&
            result.Plan.Placement.PositionId ==
                expectedPositionId &&
            result.Receipt.ActiveTerrainUnitIdsCanonical ==
                string.Join('\n', activeTerrainUnitIds) &&
            result.Receipt.TerrainManifestSha256 == terrainManifest.CanonicalSha256 &&
            result.Receipt.MobyKindId == Id65RemotePadMobyChoiceCatalog.GetKindId(kind) &&
            result.Receipt.SelectedExactRowSha256 ==
                Id65RemotePadMobyChoiceCatalog.GetSelectedExactRowSha256(
                    kind,
                    result.Plan.Placement.PositionId) &&
            result.Receipt.OutputRow80Sha256 == result.Plan.OutputRow80Sha256 &&
            result.Receipt.ChangedLogicalWadBytes == result.Plan.ChangedByteCount &&
            result.Receipt.ChangedRawSectorCount is > 0 and <=
                Id65AuthoringRemotePadTerrainSetAggregateCandidateMaterializer
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
                Id65AuthoringRemotePadTerrainSetAggregateCandidateMaterializer
                    .ExpectedSourceImageByteLength &&
            result.Plan.Destination.SourceImageSha256 ==
                Id65RewardCensusContract.LockedSourceImageSha256,
        "The schema-5 candidate result changed its exact physical boundary.");
    AssertResidentT21Candidate(result, kind);

    Id65AuthoringRemotePadTerrainSetAggregateCandidateReceipt receipt = result.Receipt;
    Require(receipt.ExactStoreReloadCasVerifiedThreeTimes &&
            receipt.ExactStaticAuthoringProjectionReloadVerifiedThreeTimes &&
            receipt.ExactNativeInputGenerationRecapturedThreeTimes &&
            receipt.ExactV3TemplateRecapturedThreeTimes &&
            receipt.ExactLockedBaseRecapturedThreeTimes &&
            receipt.Schema5CompilerPinsVerified &&
            receipt.MobyChoicePositionReadbackVerified &&
            receipt.FourTerrainTopologiesVerified && receipt.T88ExcludedVerified &&
            receipt.RepairedT13995SupportVerified &&
            receipt.ExactRow80ReadbackVerified &&
            receipt.ExactInverseReadbackVerified &&
            receipt.ProtectedComplementVerified &&
            receipt.RetailTownSquarePreserved && receipt.TargetOverlayPreserved &&
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
        "The schema-5 receipt overclaimed an unproven capability.");
    Require(!result.Plan.AggregateMaterializerEnabled &&
            !result.Plan.CandidateMaterializerEnabled &&
            !result.Plan.PhysicalMaterializationEnabled &&
            !result.Plan.WritesFileSystem && !result.Plan.WritesDiscImage &&
            !result.Plan.WritesBin && !result.Plan.WritesCue &&
            !result.Plan.AppIntegrated && !result.Plan.UiIntegrated &&
            !result.Plan.CreateBinEnabled && !result.Plan.NormalCreateBinEnabled &&
            !result.Plan.RuntimeCandidateAuthorized && !result.Plan.RuntimeProven &&
            !result.Plan.ReleaseIntegrated && !result.Plan.ReleaseAuthorized &&
            !result.Plan.PromotionAuthorized && !result.Plan.Publishable &&
            !result.Plan.CurrentOnDiskFreshnessVerified &&
            !result.Plan.HostileFilesystemRaceSafe &&
            result.Plan.RequiresTrustedExclusiveWorkspaceDirectory &&
            !result.Plan.CommonWorkspaceLeaseOwned,
        "The consumed in-memory aggregate plan gained materialization authority.");
}

static void AssertResidentT21Candidate(
    Id65AuthoringRemotePadTerrainSetAggregateCandidateMaterializationResult result,
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

    bool Related(Id65AuthoringRemotePadTerrainSetAggregateOwnedRange range)
    {
        long end = range.DataRelativeOffset + (long)range.ByteLength;
        return range.DataRelativeOffset < t21RowEnd && end > t21RowOffset ||
            range.StableId ==
                Id65AuthoringRemotePadTerrainSetPhysicalCompiler
                    .ResidentT21XyzOwnedRangeStableId ||
            range.OwnerId == Id65RemotePadRedGemT21Catalog.PlacementId ||
            range.Domain ==
                Id65AuthoringRemotePadTerrainSetAggregateRangeDomain.RedGem;
    }

    bool Exact(Id65AuthoringRemotePadTerrainSetAggregateOwnedRange range) =>
        range.StableId ==
            Id65AuthoringRemotePadTerrainSetPhysicalCompiler
                .ResidentT21XyzOwnedRangeStableId &&
        range.OwnerId == Id65RemotePadRedGemT21Catalog.PlacementId &&
        range.Domain ==
            Id65AuthoringRemotePadTerrainSetAggregateRangeDomain.RedGem &&
        range.TargetWadEntry ==
            Id65AuthoringRemotePadTerrainSetAggregateCandidateMaterializer
                .TargetDataWadEntry &&
        range.DataRelativeOffset == xyzOffset &&
        range.WadOffset ==
            Id65AuthoringRemotePadTerrainSetAggregateCandidateMaterializer
                .TargetDataWadOffset + xyzOffset &&
        range.ByteLength == xyzLength;

    Id65AuthoringRemotePadTerrainSetAggregateOwnedRange[] owned =
        result.Plan.OwnedRanges.Where(Related).ToArray();
    Id65AuthoringRemotePadTerrainSetAggregateOwnedRange[] inverse =
        result.Plan.InverseRanges.Where(Related).ToArray();
    byte[] exactSourceRow = Convert.FromHexString(
        Id65RemotePadRedGemT21Catalog.SourceRowExactHex);
    byte[] expectedOutputRow = kind == Id65RemotePadMobyKind.RedGemT21
        ? Id65RemotePadRedGemT21Catalog.CreateExactRowBytes(
            result.Plan.Placement.PositionId)
        : exactSourceRow;
    byte[] candidateRow = ReadWad(
        result.Paths.CandidateImagePath,
        Id65AuthoringRemotePadTerrainSetAggregateCandidateMaterializer
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
        "The materialized schema-5 candidate lost exact full-row resident-T21 readback.");
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
                Id65AuthoringRemotePadTerrainSetAggregateCandidateMaterializer.WadLba &&
            wad.Size ==
                Id65AuthoringRemotePadTerrainSetAggregateCandidateMaterializer
                    .ExpectedWadByteLength,
        "The schema-5 candidate WAD.WAD layout changed.");
    return DiscImage.ReadFileBytes(image, layout, wad.Lba, wadOffset, byteLength);
}

static void VerifyApiAndTopology(string repositoryRoot)
{
    MethodInfo[] internalMaterializerMethods =
        typeof(Id65AuthoringRemotePadTerrainSetAggregateCandidateMaterializer)
            .GetMethods(BindingFlags.Public | BindingFlags.Static |
                BindingFlags.DeclaredOnly);
    Require(internalMaterializerMethods.Length == 1 &&
            internalMaterializerMethods[0].Name == "MaterializeAsync" &&
            internalMaterializerMethods[0].GetParameters()
                .Select(item => item.ParameterType)
                .SequenceEqual(new[]
                {
                    typeof(
                        Id65AuthoringRemotePadTerrainSetAggregateCandidateMaterializationRequest),
                    typeof(CancellationToken)
                }),
        "The schema-5 internal materializer API surface changed.");
    PropertyInfo[] requestProperties =
        typeof(Id65AuthoringRemotePadTerrainSetAggregateCandidateMaterializationRequest)
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
                    typeof(Id65AuthoringRemotePadTerrainSetAggregatePlan)),
        "The schema-5 request stopped accepting only workspace/CAS identity.");
    MethodInfo[] facadeMethods =
        typeof(
                Id65AuthoringRemotePadTerrainSetAggregateCandidateWorkspaceMaterializer)
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
        "The public schema-5 facade signature changed.");

    string sourceRoot = Path.Combine(repositoryRoot, "src");
    string materializerRelative = Path.Combine(
        "src",
        "Spyro.Editor.Core",
        "Persistence",
        "Id65AuthoringRemotePadTerrainSetAggregateCandidateMaterializer.cs");
    string facadeRelative = Path.Combine(
        "src",
        "Spyro.Editor.Core",
        "Persistence",
        "Id65AuthoringRemotePadTerrainSetAggregateCandidateWorkspaceMaterializer.cs");
    string smokeRelative = Path.Combine(
        "src",
        "Spyro.Editor.Id65AuthoringRemotePadTerrainSetAggregateCandidateMaterializerSmoke",
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
        @"\binternal\s+static\s+class\s+Id65AuthoringRemotePadTerrainSetAggregateCandidateMaterializer\b",
        RegexOptions.CultureInvariant);
    Require(sources.Sum(item => declaration.Matches(item.Text).Count) == 1 &&
            declaration.IsMatch(sources.Single(item =>
                item.Relative == materializerRelative).Text),
        "The isolated schema-5 materializer declaration topology changed.");
    Regex internalCall = new(
        @"\bId65AuthoringRemotePadTerrainSetAggregateCandidateMaterializer\s*\.\s*Materialize(?:ForSmoke)?Async\s*\(",
        RegexOptions.CultureInvariant);
    string[] internalCallers = sources
        .Where(item => internalCall.IsMatch(item.Text))
        .Select(item => item.Relative)
        .Order(StringComparer.Ordinal)
        .ToArray();
    Require(internalCallers.SequenceEqual(new[] { facadeRelative, smokeRelative }),
        "The schema-5 candidate writer gained an unauthorized caller: " +
        string.Join(", ", internalCallers));
    Regex facadeCall = new(
        @"\bId65AuthoringRemotePadTerrainSetAggregateCandidateWorkspaceMaterializer\s*\.\s*MaterializeExactAsync\s*\(",
        RegexOptions.CultureInvariant);
    string[] facadeCallers = sources
        .Where(item => facadeCall.IsMatch(item.Text))
        .Select(item => item.Relative)
        .Order(StringComparer.Ordinal)
        .ToArray();
    Require(facadeCallers.SequenceEqual(new[] { smokeRelative }),
        "The public schema-5 candidate facade gained an unauthorized caller: " +
        string.Join(", ", facadeCallers));
    Require(!sources.Any(item =>
            item.Relative.StartsWith(
                Path.Combine("src", "Spyro.Editor.App") +
                    Path.DirectorySeparatorChar,
                StringComparison.Ordinal) &&
            item.Text.Contains(
                "Id65AuthoringRemotePadTerrainSetAggregateCandidate",
                StringComparison.Ordinal)),
        "The schema-5 candidate materializer leaked into App/UI routing.");
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
