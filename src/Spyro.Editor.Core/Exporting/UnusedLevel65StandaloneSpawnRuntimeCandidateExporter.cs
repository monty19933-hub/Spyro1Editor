using System.Buffers.Binary;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Spyro.Editor.Core.Exporting;

internal sealed record UnusedLevel65StandaloneSpawnRuntimeCandidateRequest(
    string WorkspaceRoot,
    string LockedBaseImagePath,
    string FoundationImagePath,
    string FoundationCuePath,
    string OutputDirectoryPath,
    bool ReplaceExistingCandidate = false,
    Action<string>? TestStageHook = null);

internal sealed record UnusedLevel65StandaloneSpawnRuntimeCandidatePaths(
    string PublicationParentPath,
    string OutputDirectoryPath,
    string OperationsDirectoryPath,
    string WriterLeasePath,
    string ImagePath,
    string CuePath,
    string PlanPath,
    string ReceiptPath,
    string ChecklistPath,
    string GuidePath,
    string RevealHelperPath);

internal sealed record UnusedLevel65StandaloneSpawnPatch(
    string Name,
    long WadOffset,
    string BeforeHex,
    string AfterHex,
    int ChangedByteCount);

internal readonly record struct UnusedLevel65StandaloneSpawnPoint(int X, int Y, int Z);

internal readonly record struct UnusedLevel65StandaloneSpawnCollisionCell(int X, int Y, int Z);

internal sealed record UnusedLevel65StandaloneSpawnSupportProof(
    int TerrainSectorIndex,
    int TerrainFaceIndex,
    IReadOnlyList<UnusedLevel65StandaloneSpawnPoint> NativeFacePoints,
    int CollisionTriangleIndex,
    string CollisionTriangleHex,
    IReadOnlyList<UnusedLevel65StandaloneSpawnPoint> CollisionPoints,
    int CollisionAssignment,
    int CollisionFlags,
    long CollisionNormalZ,
    UnusedLevel65StandaloneSpawnCollisionCell DestinationCell,
    IReadOnlyList<long> CollisionLookupWadOffsets,
    int NativeLeftEdgeMarginRaw,
    int NativeDiagonalMarginRaw,
    int FoundationTriangleSeparationRaw,
    int NativeGroundRawZ,
    int LandingClearanceRaw,
    int PlayerAnchorClearanceRaw,
    bool DestinationStrictlyInsideNativeCollision,
    bool DestinationStrictlyOutsideFoundationTriangle,
    bool SupportIsTopmostAtDestination,
    bool TerrainFaceReadbackVerified,
    bool RuntimeEvidenceBoundToExactNativeFan,
    string RuntimeEvidenceSha256);

internal sealed record UnusedLevel65StandaloneSpawnRuntimeCandidatePlan(
    int SchemaVersion,
    string ProfileId,
    string FoundationImageSha256,
    string LockedBaseImageSha256,
    int LevelId,
    int ContinuousLevelIndex,
    IReadOnlyList<UnusedLevel65StandaloneSpawnPatch> Patches,
    int BeforeLandingRawX,
    int BeforeLandingRawY,
    int BeforeLandingRawZ,
    int BeforePlayerRawX,
    int BeforePlayerRawY,
    int BeforePlayerRawZ,
    int AuthoredLandingRawX,
    int AuthoredLandingRawY,
    int AuthoredLandingRawZ,
    int AuthoredPlayerRawX,
    int AuthoredPlayerRawY,
    int AuthoredPlayerRawZ,
    int YawByte,
    int DeltaRawX,
    int DeltaRawY,
    int DeltaRawZ,
    UnusedLevel65StandaloneSpawnSupportProof Support,
    IReadOnlyList<int> AffectedRawSectorLbas,
    IReadOnlyList<RuntimeCandidateLoadCode> LoadCodes,
    bool LandingAndPlayerAnchorCoupledAtomically,
    bool MusicPreserved,
    bool TotalsPreserved,
    bool ExitPreserved,
    bool SaveCodePreserved,
    bool RetailLevelsPreserved,
    bool RequiresDuckStationRuntimeProof,
    bool RuntimePassed,
    bool NormalCreateBinEnabled,
    bool PromotionAuthorized);

internal sealed record UnusedLevel65StandaloneSpawnRawSectorDiff(
    int RawSectorLba,
    int HeaderChangedBytes,
    int SubheaderChangedBytes,
    int PayloadChangedBytes,
    int EdcChangedBytes,
    int ReservedChangedBytes,
    int EccPChangedBytes,
    int EccQChangedBytes,
    int TotalChangedBytes);

internal sealed record UnusedLevel65StandaloneSpawnRuntimeCandidateReceipt(
    int SchemaVersion,
    string ProfileId,
    string LockedBaseImagePath,
    string FoundationImagePath,
    string FoundationCuePath,
    string OutputDirectoryPath,
    string OutputImagePath,
    string OutputCuePath,
    string PlanPath,
    string ReceiptPath,
    string ChecklistPath,
    string GuidePath,
    string? FinderHelperPath,
    string LockedBaseImageSha256,
    string FoundationImageSha256,
    string OutputImageSha256,
    string OutputId65DataSha256,
    string OutputCueSha256,
    string PlanSha256,
    string ChecklistSha256,
    string GuideSha256,
    string? FinderHelperSha256,
    IReadOnlyList<RuntimeCandidateLoadCode> LoadCodes,
    long ChangedLogicalWadBytes,
    long ChangedPhysicalImageBytes,
    IReadOnlyList<UnusedLevel65StandaloneSpawnRawSectorDiff> RawSectorDiffs,
    string RawSectorDiffSha256,
    bool ExactPatchReadbackVerified,
    bool CoupledLandingAndPlayerAnchorVerified,
    bool DestinationSupportVerified,
    bool FoundationPreserved,
    bool ExecutablePreserved,
    bool RetailTownSquarePreserved,
    bool MusicTotalsExitSavePreserved,
    bool RawSectorIntegrityVerified,
    bool AtomicDirectoryPublicationCompleted,
    bool RollbackGuardsEnabled,
    bool StartupRecoveryPerformed,
    bool RuntimePending,
    bool NormalCreateBinEnabled,
    bool PromotionAuthorized);

internal sealed record UnusedLevel65StandaloneSpawnPublishedArtifactHashes(
    string CueSha256,
    string PlanSha256,
    string ReceiptSha256,
    string ChecklistSha256,
    string GuideSha256,
    string? FinderHelperSha256);

internal sealed record UnusedLevel65StandaloneSpawnRuntimeCandidateResult(
    UnusedLevel65StandaloneSpawnRuntimeCandidatePaths Paths,
    UnusedLevel65StandaloneSpawnRuntimeCandidatePlan Plan,
    UnusedLevel65StandaloneSpawnRuntimeCandidateReceipt Receipt,
    UnusedLevel65StandaloneSpawnPublishedArtifactHashes ArtifactHashes);

/// <summary>
/// Disposable first ownership gate for ID65's landing and T92 player anchor.
/// It moves both records as one transaction to a statically exposed point on
/// the exact entrance collision fan that already has focused runtime solidity
/// evidence. The destination is deliberately outside the new, runtime-pending
/// foundation triangle, and every other behavior-owning byte remains unchanged.
/// </summary>
internal static class UnusedLevel65StandaloneSpawnRuntimeCandidateExporter
{
    public static bool Retired => true;
    public static string RetirementReason =>
        "RETIRED: this candidate is stacked on the positive-wound ID65 foundation collision convention, which is not runtime-proven and matches the failed remote-blank v1 sign. Preserve frozen artifacts only as historical evidence; do not republish or load them. Use the isolated collision-winding-repair v2 discriminator until DuckStation establishes the playable convention.";

    public const string ProfileId =
        "unused-level-65-standalone-spawn-ownership-native-apron-clean-usa-disposable-v1";
    public const string OutputDirectoryName = "unused-level-65-standalone-spawn-ownership";
    public const string OutputPrefix =
        "Unused-Level-65-Standalone-Spawn-Owned-Native-Apron-RUNTIME-CANDIDATE";

    public const string FoundationImageSha256 =
        UnusedLevel65StandaloneBehaviorOwnershipInspector.FoundationImageSha256;
    public const string LockedBaseImageSha256 =
        UnusedLevel65StandaloneBehaviorOwnershipInspector.LockedBaseImageSha256;

    public const string ExpectedOutputImageSha256 =
        "f76765081433a8ce4e68eaffa833ede6970684431e2a430c1e8eb18be9752f0d";
    public const string ExpectedOutputId65DataSha256 =
        "782672262a9961ea114ba676f691ed8d6d1f29dcbbfcd9f9d1071ed3836bd96e";
    public const string ExpectedRawSectorDiffSha256 =
        "ae1a93b94dc6485a312f29dce23d7981e3d164d8678dd68affc3998fb2ca6607";

    private const int PlanSchemaVersion = 1;
    private const int ReceiptSchemaVersion = 1;
    private const int JournalSchemaVersion = 2;
    private const int WadLba = 37;
    private const int WadByteLength = 0x6C18800;
    private const int RawSectorByteLength = 2352;
    private const int LogicalSectorByteLength = 2048;
    private const int UserDataOffset = 24;
    private const byte XaDataSubmode = 0x08;
    private const long Id65DataWadOffset = 0x6936800;
    private const int Id65DataByteLength = 0x2E2000;
    private const long Id65OverlayWadOffset = 0x6927000;
    private const int Id65OverlayByteLength = 0xF800;
    private const long RetailTownSquareOverlayWadOffset = 0x118E800;
    private const int RetailTownSquareOverlayByteLength = 0xF800;
    private const long RetailTownSquareDataWadOffset = 0x119E000;
    private const int RetailTownSquareDataByteLength = 0x2E2000;
    private const int ExecutableLba = 55_382;
    private const int ExecutableByteLength = 0x66000;
    private const string ExecutableSha256 =
        UnusedLevel65StandaloneBehaviorOwnershipInspector.ExecutableSha256;

    private const long LandingWadOffset = 0x6B06800;
    private const long PlayerAnchorWadOffset = 0x6B08910;
    private const int PlayerAnchorByteLength = 0x58;
    private const int PlayerAnchorTrueIndex = 92;
    private const string BeforeLandingHex = "29E901009A8801006621000000004000";
    private const string BeforePlayerAnchorSha256 =
        "4987c539f3178c555da26e4ec2f6cdc25094a27382b9465c9845846a39679bb2";
    private const int BeforeRawX = 125_225;
    private const int BeforeRawY = 100_506;
    private const int LandingRawZ = 8_550;
    private const int PlayerRawZ = 8_704;
    private const int YawByte = 0x40;

    // Scene point (7786.5625, 6434.625). Keeping the native fractional
    // residues makes the authored pair exact in raw coordinates.
    private const int AuthoredRawX = 124_585;
    private const int AuthoredRawY = 102_954;
    private const int NativeGroundRawZ = 8_192;
    private const int CollisionTriangleIndex = 1_353;
    private const int FoundationCollisionTriangleIndex = 13_995;
    private const long FoundationCollisionComponentWadOffset = 0x6A40DF0;
    private const int CollisionComponentByteLength = 0x5FAE8;
    private const int CollisionTreeRelativeOffset = 4 + 0x1C;
    private const int CollisionTreeByteLength = 0x6A60;
    private const int CollisionBlocksRelativeOffset = 4 + 0x6A7C;
    private const int CollisionBlocksByteLength = 0x17984;
    private const int CollisionTrianglesRelativeOffset = 4 + 0x1E400;
    private const int CollisionAssignmentsRelativeOffset = 4 + 0x58480;
    private const int CollisionFlagsRelativeOffset = 4 + 0x5D1E0;
    private const int CollisionTriangleCount = 19_808;
    private const string ExpectedSupportTriangleHex = "521E20004A1960C000020000";
    private const string ExpectedFoundationTriangleHex = "521E2020CA18004000020080";
    private const string FoundationCollisionSha256 =
        "5e7b4430c9bfbd2793df1d9833d8d3af005924d7c110b66f0b0e1f8bc818c056";
    private const string FoundationCollisionTreeSha256 =
        "0318210317487c34a7c25cf487805a203dbe4b03ee1250141cab05f624ab09dd";
    private const string FoundationCollisionBlocksSha256 =
        "7c414761af050eabc226b81cb4f57e248f2f3b2a43fc28b0fe9bc591532656d4";
    private const string RuntimeEvidenceRelativePath =
        "docs/runtime-evidence/unused-level-65-town-square-authored-terrain-solid-entry-ramp-control-focused-pass-2026-08-09.json";
    private const string RuntimeEvidenceSha256 =
        "3ddfb471fd316c68459c6c09f5d3caa85ab0b134b9720e2f86025aa398b1ea87";

    private const string OperationsDirectoryName = ".unused-level-65-standalone-spawn-operations";
    private const string WriterLeaseFileName = ".unused-level-65-standalone-spawn-writer.lease";
    private const string JournalFileName = "operation-journal.json";
    private const string JournalOperationKind = "unused-level-65-standalone-spawn-publication";
    private const string StagePrefix = "spawn-stage-";
    private const string BackupPrefix = "spawn-backup-";
    private const string PhaseStaged = "staged";
    private const string PhaseBackupIntent = "backup-intent";
    private const string PhasePreviousBackedUp = "previous-backed-up";
    private const string PhasePublishIntent = "publish-intent";
    private const string PhasePublished = "published";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static UnusedLevel65StandaloneSpawnRuntimeCandidatePaths CreatePaths(string outputDirectoryPath)
    {
        if (string.IsNullOrWhiteSpace(outputDirectoryPath))
            throw new ArgumentException("The standalone-spawn output directory is missing.", nameof(outputDirectoryPath));
        string output = Path.GetFullPath(outputDirectoryPath);
        if (!string.Equals(Path.GetFileName(output), OutputDirectoryName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"The standalone-spawn writer owns only a directory named '{OutputDirectoryName}'.");
        }
        string parent = Path.GetDirectoryName(output)
            ?? throw new InvalidOperationException("The standalone-spawn publication parent is missing.");
        return new(
            parent,
            output,
            Path.Combine(parent, OperationsDirectoryName),
            Path.Combine(parent, WriterLeaseFileName),
            Path.Combine(output, OutputPrefix + ".bin"),
            Path.Combine(output, OutputPrefix + ".cue"),
            Path.Combine(output, OutputPrefix + "-spawn-plan.json"),
            Path.Combine(output, OutputPrefix + "-static-readback-receipt.json"),
            Path.Combine(output, OutputPrefix + "-runtime-checklist.md"),
            Path.Combine(output, OutputPrefix + "-location-guide.svg"),
            Path.Combine(output, OutputPrefix + "-Reveal-in-Finder.command"));
    }

    public static async Task<UnusedLevel65StandaloneSpawnRuntimeCandidateResult> CreateAsync(
        UnusedLevel65StandaloneSpawnRuntimeCandidateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (Retired)
            throw new InvalidOperationException(RetirementReason);

        ArgumentNullException.ThrowIfNull(request);
        string root = RequireDirectory(request.WorkspaceRoot, "Spyro Editor workspace");
        string lockedBase = RequireFile(request.LockedBaseImagePath, "exact locked ID65 base BIN");
        string foundation = RequireFile(request.FoundationImagePath, "exact ID65 foundation BIN");
        string foundationCue = RequireFile(request.FoundationCuePath, "exact ID65 foundation CUE");
        UnusedLevel65StandaloneSpawnRuntimeCandidatePaths paths = CreatePaths(request.OutputDirectoryPath);
        RequireSafeRoles(lockedBase, foundation, foundationCue, paths);
        Directory.CreateDirectory(paths.PublicationParentPath);

        using FileStream writerLease = AcquireWriterLease(paths.WriterLeasePath);
        request.TestStageHook?.Invoke("after-global-writer-lease-acquired");
        bool rollbackRecoveryVerified = RecoverOwnedOperations(paths);
        request.TestStageHook?.Invoke("after-startup-recovery");
        if (Directory.Exists(paths.OutputDirectoryPath) && !request.ReplaceExistingCandidate)
        {
            throw new InvalidOperationException(
                "The standalone-spawn runtime candidate already exists. Set ReplaceExistingCandidate only for an intentional deterministic rebuild.");
        }

        NativeLevelReplacementBaselineExporter.ValidateCue(foundationCue, foundation, "MODE2/2352");
        await RequireFileHashAsync(lockedBase, LockedBaseImageSha256, cancellationToken);
        await RequireFileHashAsync(foundation, FoundationImageSha256, cancellationToken);
        UnusedLevel65StandaloneBehaviorOwnershipContract ownership =
            await UnusedLevel65StandaloneBehaviorOwnershipInspector.InspectAsync(
                root,
                lockedBase,
                foundation,
                cancellationToken);
        if (!ownership.StaticReadbackOnly || ownership.PromotionAuthorized ||
            ownership.Spawn.PlayerAnchorTrueIndex != PlayerAnchorTrueIndex)
        {
            throw new InvalidDataException("The committed standalone behavior ownership boundary changed.");
        }

        Prepared prepared = await PrepareAsync(root, lockedBase, foundation, foundationCue, ownership, cancellationToken);
        string operationId = Guid.NewGuid().ToString("N");
        string stage = Path.Combine(paths.OperationsDirectoryPath, StagePrefix + operationId);
        string backup = Path.Combine(paths.OperationsDirectoryPath, BackupPrefix + operationId);
        Journal journal = new(
            JournalSchemaVersion,
            JournalOperationKind,
            operationId,
            paths.OutputDirectoryPath,
            PhaseStaged,
            stage,
            backup,
            Directory.Exists(paths.OutputDirectoryPath));
        bool priorBackedUp = false;
        bool candidatePublished = false;
        try
        {
            Directory.CreateDirectory(stage);
            await BuildStageAsync(
                prepared,
                foundation,
                foundationCue,
                paths,
                stage,
                rollbackRecoveryVerified,
                cancellationToken);
            WriteJournal(paths, journal);
            request.TestStageHook?.Invoke("after-stage-complete");

            if (journal.HadPreviousCandidate)
            {
                journal = journal with { Phase = PhaseBackupIntent };
                WriteJournal(paths, journal);
                Directory.Move(paths.OutputDirectoryPath, backup);
                priorBackedUp = true;
                journal = journal with { Phase = PhasePreviousBackedUp };
                WriteJournal(paths, journal);
                request.TestStageHook?.Invoke("after-previous-candidate-backup");
            }

            journal = journal with { Phase = PhasePublishIntent };
            WriteJournal(paths, journal);
            Directory.Move(stage, paths.OutputDirectoryPath);
            candidatePublished = true;
            journal = journal with { Phase = PhasePublished };
            WriteJournal(paths, journal);
            request.TestStageHook?.Invoke("after-candidate-publication");

            UnusedLevel65StandaloneSpawnRuntimeCandidateResult result =
                await ReadPublishedResultAsync(paths, cancellationToken);
            if (priorBackedUp)
            {
                DeleteOwnedDirectory(backup, paths.OperationsDirectoryPath, BackupPrefix);
                priorBackedUp = false;
            }
            DeleteJournal(paths);
            return result;
        }
        catch (Exception failure)
        {
            List<Exception> recoveryFailures = [];
            try
            {
                if (candidatePublished && Directory.Exists(paths.OutputDirectoryPath))
                    DeleteOwnedDirectory(paths.OutputDirectoryPath, paths.PublicationParentPath, OutputDirectoryName);
            }
            catch (Exception ex)
            {
                recoveryFailures.Add(ex);
            }
            try
            {
                if (priorBackedUp && Directory.Exists(backup) && !Directory.Exists(paths.OutputDirectoryPath))
                {
                    Directory.Move(backup, paths.OutputDirectoryPath);
                    priorBackedUp = false;
                }
            }
            catch (Exception ex)
            {
                recoveryFailures.Add(ex);
            }
            try
            {
                if (Directory.Exists(stage))
                    DeleteOwnedDirectory(stage, paths.OperationsDirectoryPath, StagePrefix);
            }
            catch (Exception ex)
            {
                recoveryFailures.Add(ex);
            }
            if (recoveryFailures.Count == 0)
                DeleteJournal(paths);
            if (recoveryFailures.Count > 0)
            {
                throw new IOException(
                    "Standalone-spawn publication failed and exact prior-candidate rollback was incomplete.",
                    new AggregateException([failure, .. recoveryFailures]));
            }
            throw;
        }
    }

    private static async Task<Prepared> PrepareAsync(
        string workspaceRoot,
        string lockedBase,
        string foundation,
        string foundationCue,
        UnusedLevel65StandaloneBehaviorOwnershipContract ownership,
        CancellationToken cancellationToken)
    {
        string evidencePath = Path.Combine(workspaceRoot, RuntimeEvidenceRelativePath);
        await RequireFileHashAsync(
            RequireFile(evidencePath, "focused entrance-fan runtime evidence"),
            RuntimeEvidenceSha256,
            cancellationToken);
        using (JsonDocument evidence = JsonDocument.Parse(await File.ReadAllTextAsync(evidencePath, cancellationToken)))
        {
            JsonElement root = evidence.RootElement;
            if (root.GetProperty("evidenceStatus").GetString() != "focused-runtime-pass" ||
                root.GetProperty("evidenceScope").GetString() != "complete-edited-surface-solidity-only" ||
                root.GetProperty("profileId").GetString() !=
                    UnusedLevel65AuthoredTerrainSolidEntryRampControlCandidateExporter.ProfileId ||
                root.GetProperty("promotionAuthorized").GetBoolean() ||
                root.GetProperty("normalCreateBinIntegrationAuthorized").GetBoolean())
            {
                throw new InvalidDataException("The exact focused entrance-fan runtime evidence boundary changed.");
            }
            int[] central = root.GetProperty("exactCandidateStaticBinding")
                .GetProperty("centralCollisionFootprintTriangleIndexes")
                .EnumerateArray()
                .Select(value => value.GetInt32())
                .ToArray();
            if (!central.SequenceEqual(new[] { 1_353, 1_354 }))
                throw new InvalidDataException("The runtime evidence is no longer bound to native collision triangles 1353/1354.");
        }

        UnusedLevel65ConstructionCollidableTerrainPlan construction =
            await UnusedLevel65FullAuthoringConstructionTemplate.BuildFirstCollidableTerrainPlanAsync(
                lockedBase,
                cancellationToken);
        if (!construction.CollisionComposed || !construction.OcclusionOwnershipVerified ||
            construction.CollisionBinding.ReusedTriangleIndex != FoundationCollisionTriangleIndex ||
            construction.PromotionAuthorized || construction.NormalCreateBinEnabled)
        {
            throw new InvalidDataException("The exact static-only foundation composition contract changed.");
        }

        DiscLayout layout = DiscImage.DetectLayout(foundation);
        RequireMode2(layout, "foundation");
        using FileStream image = File.OpenRead(foundation);
        DiscFileRecord wad = RequireFileRecord(image, layout, "WAD.WAD", WadLba, WadByteLength);
        DiscFileRecord executable = RequireFileRecord(
            image,
            layout,
            "SCUS_942.28",
            ExecutableLba,
            ExecutableByteLength);
        _ = wad;
        byte[] executableBytes = DiscImage.ReadFileBytes(image, layout, executable.Lba, 0, executable.Size);
        RequireHash(executableBytes, ExecutableSha256, "foundation executable");

        byte[] model = DiscImage.ReadFileBytes(
            image,
            layout,
            WadLba,
            UnusedLevel65FullAuthoringConstructionTemplate.ModelSubfileWadOffset,
            UnusedLevel65FullAuthoringConstructionTemplate.ModelSubfileByteLength);
        if (!model.SequenceEqual(construction.AfterModelBytes))
            throw new InvalidDataException("The exact foundation model does not equal the committed construction composition.");
        const long sector213WadOffset = 0x6A3E8B4;
        const long authoredHpVertexTableWadOffset = 0x6A3EAD8;
        const long authoredHpFace37WadOffset = 0x6A3F52C;
        int sectorRelative = checked((int)(sector213WadOffset -
            UnusedLevel65FullAuthoringConstructionTemplate.ModelSubfileWadOffset));
        int vertexRelative = checked((int)(authoredHpVertexTableWadOffset -
            UnusedLevel65FullAuthoringConstructionTemplate.ModelSubfileWadOffset));
        int faceRelative = checked((int)(authoredHpFace37WadOffset -
            UnusedLevel65FullAuthoringConstructionTemplate.ModelSubfileWadOffset));
        byte[] sectorHeader = model.AsSpan(sectorRelative, 28).ToArray();
        byte[] face37 = model.AsSpan(faceRelative, 16).ToArray();
        RequireHex(face37, "283930272D3D352D1C0680FF08F4CFFF", "foundation HP face 213:37");
        if (sectorHeader[20] != 143 || sectorHeader[22] != 114 ||
            !face37.AsSpan(0, 4).SequenceEqual(new byte[] { 40, 57, 48, 39 }))
        {
            throw new InvalidDataException("The foundation sector-213 HP counts/face-37 slots changed.");
        }
        UnusedLevel65StandaloneSpawnPoint[] nativeFacePoints = face37.AsSpan(0, 4)
            .ToArray()
            .Select(vertexIndex => DecodeSceneVertex(
                model.AsSpan(vertexRelative + (vertexIndex * 4), 4),
                sectorHeader))
            .ToArray();
        UnusedLevel65StandaloneSpawnPoint[] expectedNativeFacePoints =
        [
            new(7_762, 6_346, 512),
            new(7_890, 6_346, 512),
            new(7_890, 6_474, 512),
            new(7_762, 6_474, 512)
        ];
        if (!nativeFacePoints.SequenceEqual(expectedNativeFacePoints))
            throw new InvalidDataException("The exact foundation HP face 213:37 geometry changed.");
        byte[] collision = DiscImage.ReadFileBytes(
            image,
            layout,
            WadLba,
            FoundationCollisionComponentWadOffset,
            CollisionComponentByteLength);
        RequireHash(collision, FoundationCollisionSha256, "foundation collision component");
        if (BinaryPrimitives.ReadInt32LittleEndian(collision.AsSpan(0, 4)) != CollisionComponentByteLength ||
            BinaryPrimitives.ReadInt32LittleEndian(collision.AsSpan(4, 4)) != CollisionTriangleCount)
        {
            throw new InvalidDataException("The foundation collision header changed.");
        }
        RequireHash(
            collision.AsSpan(CollisionTreeRelativeOffset, CollisionTreeByteLength),
            FoundationCollisionTreeSha256,
            "foundation collision tree");
        RequireHash(
            collision.AsSpan(CollisionBlocksRelativeOffset, CollisionBlocksByteLength),
            FoundationCollisionBlocksSha256,
            "foundation collision blocks");

        byte[] supportTriangleBytes = collision.AsSpan(
            CollisionTrianglesRelativeOffset + (CollisionTriangleIndex * 12),
            12).ToArray();
        byte[] foundationTriangleBytes = collision.AsSpan(
            CollisionTrianglesRelativeOffset + (FoundationCollisionTriangleIndex * 12),
            12).ToArray();
        RequireHex(supportTriangleBytes, ExpectedSupportTriangleHex, "native support triangle 1353");
        RequireHex(foundationTriangleBytes, ExpectedFoundationTriangleHex, "foundation triangle 13995");
        IReadOnlyList<UnusedLevel65StandaloneSpawnPoint> supportPoints = DecodeCollisionTriangle(supportTriangleBytes);
        IReadOnlyList<UnusedLevel65StandaloneSpawnPoint> expectedSupportPoints =
        [
            new(7_762, 6_474, 512),
            new(7_890, 6_346, 512),
            new(7_762, 6_346, 512)
        ];
        IReadOnlyList<UnusedLevel65StandaloneSpawnPoint> foundationPoints = DecodeCollisionTriangle(foundationTriangleBytes);
        IReadOnlyList<UnusedLevel65StandaloneSpawnPoint> expectedFoundationPoints =
        [
            new(7_762, 6_346, 512),
            new(7_890, 6_346, 512),
            new(7_826, 6_474, 640)
        ];
        if (!supportPoints.SequenceEqual(expectedSupportPoints) ||
            !foundationPoints.SequenceEqual(expectedFoundationPoints))
        {
            throw new InvalidDataException("The exact native/foundation collision geometry changed.");
        }

        int assignment = collision[CollisionAssignmentsRelativeOffset + CollisionTriangleIndex];
        uint supportZWord = BinaryPrimitives.ReadUInt32LittleEndian(supportTriangleBytes.AsSpan(8, 4));
        int flags = checked((int)(supportZWord & 0xC000u));
        if (assignment != 0 || flags != 0)
            throw new InvalidDataException("The destination support triangle lost ordinary assignment/flags zero.");

        CollisionIndex index = DecodeCollisionIndex(
            collision.AsSpan(CollisionTreeRelativeOffset, CollisionTreeByteLength),
            collision.AsSpan(CollisionBlocksRelativeOffset, CollisionBlocksByteLength),
            construction.CollisionBlocksUsedBytes,
            CollisionTriangleCount);
        UnusedLevel65StandaloneSpawnCollisionCell destinationCell = new(30, 25, 2);
        if (!index.Cells.TryGetValue(destinationCell, out CollisionCell? cell) ||
            !cell.TriangleIndexes.Contains(CollisionTriangleIndex) ||
            !cell.TriangleIndexes.Contains(FoundationCollisionTriangleIndex))
        {
            throw new InvalidDataException("The exact destination collision cell lost its native/foundation memberships.");
        }
        int supportPosition = Array.IndexOf(cell.TriangleIndexes, CollisionTriangleIndex);
        if (supportPosition < 0)
            throw new InvalidDataException("The native support triangle is missing from the exact destination group.");
        long supportLookupWadOffset = FoundationCollisionComponentWadOffset +
                                      CollisionBlocksRelativeOffset +
                                      ((cell.BlockWordOffset + supportPosition) * 2L);

        const int nativeLeftMarginRaw = AuthoredRawX - (7_762 * 16);
        const int nativeDiagonalMarginRaw = (128 * 16) -
                                            ((AuthoredRawX - (7_762 * 16)) +
                                             (AuthoredRawY - (6_346 * 16)));
        int foundationLeftBoundaryRaw = (7_762 * 16) +
                                        ((AuthoredRawY - (6_346 * 16)) / 2);
        int foundationSeparationRaw = foundationLeftBoundaryRaw - AuthoredRawX;
        if (nativeLeftMarginRaw != 393 || nativeDiagonalMarginRaw != 237 ||
            foundationSeparationRaw != 316 ||
            !PointInsideTriangleStrictRaw16(supportPoints, AuthoredRawX, AuthoredRawY) ||
            PointInsideTriangleInclusiveRaw16(foundationPoints, AuthoredRawX, AuthoredRawY))
        {
            throw new InvalidDataException("The authored landing is not strictly inside native T1353 and outside foundation T13995.");
        }

        List<CollisionSurfaceHit> hits = [];
        for (int triangleIndex = 0; triangleIndex < CollisionTriangleCount; triangleIndex++)
        {
            byte[] bytes = collision.AsSpan(CollisionTrianglesRelativeOffset + (triangleIndex * 12), 12).ToArray();
            IReadOnlyList<UnusedLevel65StandaloneSpawnPoint> points = DecodeCollisionTriangle(bytes);
            if (!PointInsideTriangleInclusiveRaw16(points, AuthoredRawX, AuthoredRawY))
                continue;
            if (!TryInterpolateRawZ(points, AuthoredRawX, AuthoredRawY, out int rawZ, out long normalZ))
                continue;
            hits.Add(new(triangleIndex, rawZ, normalZ));
        }
        CollisionSurfaceHit[] topOrHigher = hits
            .Where(hit => hit.RawZ >= NativeGroundRawZ)
            .OrderByDescending(hit => hit.RawZ)
            .ThenBy(hit => hit.TriangleIndex)
            .ToArray();
        if (topOrHigher.Length == 0 || topOrHigher[0].RawZ != NativeGroundRawZ ||
            topOrHigher.Any(hit => hit.RawZ > NativeGroundRawZ) ||
            !topOrHigher.Any(hit => hit.TriangleIndex == CollisionTriangleIndex && hit.NormalZ == -16_384))
        {
            throw new InvalidDataException(
                "The destination does not have exact triangle 1353 as an exposed topmost native support surface: " +
                string.Join(",", topOrHigher.Select(hit => $"T{hit.TriangleIndex}:Z{hit.RawZ}:N{hit.NormalZ}")));
        }

        byte[] beforeLanding = DiscImage.ReadFileBytes(image, layout, WadLba, LandingWadOffset, 0x10);
        byte[] beforePlayerAnchor = DiscImage.ReadFileBytes(
            image,
            layout,
            WadLba,
            PlayerAnchorWadOffset,
            PlayerAnchorByteLength);
        RequireHex(beforeLanding, BeforeLandingHex, "foundation landing");
        RequireHash(beforePlayerAnchor, BeforePlayerAnchorSha256, "foundation T92 player anchor");
        int playerX = BinaryPrimitives.ReadInt32LittleEndian(beforePlayerAnchor.AsSpan(0x0C, 4));
        int playerY = BinaryPrimitives.ReadInt32LittleEndian(beforePlayerAnchor.AsSpan(0x10, 4));
        int playerZ = BinaryPrimitives.ReadInt32LittleEndian(beforePlayerAnchor.AsSpan(0x14, 4));
        if (playerX != BeforeRawX || playerY != BeforeRawY || playerZ != PlayerRawZ)
            throw new InvalidDataException("The foundation T92 coordinate preimage changed.");

        byte[] afterLanding = beforeLanding.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(afterLanding.AsSpan(0, 4), AuthoredRawX);
        BinaryPrimitives.WriteInt32LittleEndian(afterLanding.AsSpan(4, 4), AuthoredRawY);
        byte[] beforePlayerCoordinates = beforePlayerAnchor.AsSpan(0x0C, 12).ToArray();
        byte[] afterPlayerCoordinates = beforePlayerCoordinates.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(afterPlayerCoordinates.AsSpan(0, 4), AuthoredRawX);
        BinaryPrimitives.WriteInt32LittleEndian(afterPlayerCoordinates.AsSpan(4, 4), AuthoredRawY);

        UnusedLevel65StandaloneSpawnPatch[] patches =
        [
            new(
                "ID65 fly-in landing XYZ/yaw record",
                LandingWadOffset,
                Convert.ToHexString(beforeLanding),
                Convert.ToHexString(afterLanding),
                CountDifferentBytes(beforeLanding, afterLanding)),
            new(
                "ID65 T92 player-anchor XYZ",
                PlayerAnchorWadOffset + 0x0C,
                Convert.ToHexString(beforePlayerCoordinates),
                Convert.ToHexString(afterPlayerCoordinates),
                CountDifferentBytes(beforePlayerCoordinates, afterPlayerCoordinates))
        ];
        if (patches.Sum(patch => patch.ChangedByteCount) != 8 ||
            afterLanding[0x0E] != YawByte ||
            BinaryPrimitives.ReadInt32LittleEndian(afterLanding.AsSpan(8, 4)) != LandingRawZ ||
            BinaryPrimitives.ReadInt32LittleEndian(afterPlayerCoordinates.AsSpan(8, 4)) != PlayerRawZ)
        {
            throw new InvalidDataException("The atomic landing/T92 patch changed more than exact X/Y ownership.");
        }

        int[] affectedLbas = patches
            .SelectMany(patch => RawSectorLbasForWadRange(layout, patch.WadOffset, Convert.FromHexString(patch.AfterHex).Length))
            .Distinct()
            .Order()
            .ToArray();
        if (!affectedLbas.SequenceEqual(new[] { 54_834, 54_838 }))
            throw new InvalidDataException($"The spawn patch escaped its exact two raw sectors: [{string.Join(',', affectedLbas)}].");
        IReadOnlyList<RuntimeCandidateLoadCode> loadCodes =
            RuntimeCandidateTestHandoff.Id65ComparisonLoadCodes;
        VerifyLoadCodes(loadCodes);

        UnusedLevel65StandaloneSpawnSupportProof support = new(
            213,
            37,
            nativeFacePoints,
            CollisionTriangleIndex,
            Convert.ToHexString(supportTriangleBytes),
            supportPoints,
            assignment,
            flags,
            CollisionNormalZ: -16_384,
            destinationCell,
            [supportLookupWadOffset],
            nativeLeftMarginRaw,
            nativeDiagonalMarginRaw,
            foundationSeparationRaw,
            NativeGroundRawZ,
            LandingRawZ - NativeGroundRawZ,
            PlayerRawZ - NativeGroundRawZ,
            DestinationStrictlyInsideNativeCollision: true,
            DestinationStrictlyOutsideFoundationTriangle: true,
            SupportIsTopmostAtDestination: true,
            TerrainFaceReadbackVerified: true,
            RuntimeEvidenceBoundToExactNativeFan: true,
            RuntimeEvidenceSha256);
        UnusedLevel65StandaloneSpawnRuntimeCandidatePlan plan = new(
            PlanSchemaVersion,
            ProfileId,
            FoundationImageSha256,
            LockedBaseImageSha256,
            65,
            35,
            patches,
            BeforeRawX,
            BeforeRawY,
            LandingRawZ,
            BeforeRawX,
            BeforeRawY,
            PlayerRawZ,
            AuthoredRawX,
            AuthoredRawY,
            LandingRawZ,
            AuthoredRawX,
            AuthoredRawY,
            PlayerRawZ,
            YawByte,
            AuthoredRawX - BeforeRawX,
            AuthoredRawY - BeforeRawY,
            0,
            support,
            affectedLbas,
            loadCodes,
            LandingAndPlayerAnchorCoupledAtomically: true,
            MusicPreserved: true,
            TotalsPreserved: true,
            ExitPreserved: true,
            SaveCodePreserved: true,
            RetailLevelsPreserved: true,
            RequiresDuckStationRuntimeProof: true,
            RuntimePassed: false,
            NormalCreateBinEnabled: false,
            PromotionAuthorized: false);
        return new(
            layout,
            plan,
            ownership,
            executableBytes,
            beforeLanding,
            beforePlayerAnchor,
            lockedBase,
            foundation,
            foundationCue);
    }

    private static async Task BuildStageAsync(
        Prepared prepared,
        string foundation,
        string foundationCue,
        UnusedLevel65StandaloneSpawnRuntimeCandidatePaths finalPaths,
        string stageDirectory,
        bool rollbackRecoveryVerified,
        CancellationToken cancellationToken)
    {
        string imagePath = Path.Combine(stageDirectory, OutputPrefix + ".bin");
        string cuePath = Path.Combine(stageDirectory, OutputPrefix + ".cue");
        string planPath = Path.Combine(stageDirectory, OutputPrefix + "-spawn-plan.json");
        string receiptPath = Path.Combine(stageDirectory, OutputPrefix + "-static-readback-receipt.json");
        string checklistPath = Path.Combine(stageDirectory, OutputPrefix + "-runtime-checklist.md");
        string guidePath = Path.Combine(stageDirectory, OutputPrefix + "-location-guide.svg");
        string revealPath = Path.Combine(stageDirectory, OutputPrefix + "-Reveal-in-Finder.command");

        await DiscImageWorkingCopy.StageAsync(foundation, imagePath, false, cancellationToken);
        DiscLayout layout = DiscImage.DetectLayout(imagePath);
        RequireMode2(layout, "staged standalone-spawn candidate");
        int rebuilt;
        await using (FileStream output = new(
                         imagePath,
                         FileMode.Open,
                         FileAccess.ReadWrite,
                         FileShare.None,
                         bufferSize: 1 << 20,
                         FileOptions.Asynchronous))
        {
            foreach (UnusedLevel65StandaloneSpawnPatch patch in prepared.Plan.Patches)
            {
                byte[] before = Convert.FromHexString(patch.BeforeHex);
                byte[] after = Convert.FromHexString(patch.AfterHex);
                RequireEqual(
                    DiscImage.ReadFileBytes(output, layout, WadLba, patch.WadOffset, before.Length),
                    before,
                    $"staged {patch.Name} preimage");
                DiscImage.WriteFileBytes(output, layout, WadLba, patch.WadOffset, after);
            }
            rebuilt = RawMode2Form1SectorIntegrity.RebuildFileRanges(
                output,
                layout,
                WadLba,
                prepared.Plan.Patches
                    .Select(patch => (patch.WadOffset, Convert.FromHexString(patch.AfterHex).Length))
                    .ToArray());
            if (rebuilt != prepared.Plan.AffectedRawSectorLbas.Count)
                throw new InvalidDataException("The spawn writer did not rebuild exactly its two owned raw sectors.");
            VerifyRawSectors(output, layout, prepared.Plan.AffectedRawSectorLbas);
            await output.FlushAsync(cancellationToken);
            output.Flush(flushToDisk: true);
        }

        Readback readback = await VerifyOutputAsync(
            prepared,
            foundation,
            imagePath,
            cancellationToken);
        if (!IsPendingHash(ExpectedOutputImageSha256))
            RequireTextHash(readback.OutputImageSha256, ExpectedOutputImageSha256, "standalone-spawn output BIN");
        if (!IsPendingHash(ExpectedOutputId65DataSha256))
            RequireTextHash(readback.OutputId65DataSha256, ExpectedOutputId65DataSha256, "standalone-spawn ID65 data");
        if (!IsPendingHash(ExpectedRawSectorDiffSha256))
            RequireTextHash(readback.RawSectorDiffSha256, ExpectedRawSectorDiffSha256, "standalone-spawn raw diff");

        string cueText = DiscImage.BuildCueText(foundationCue, Path.GetFileName(imagePath));
        await WriteTextAsync(cuePath, cueText, Encoding.ASCII, cancellationToken);
        NativeLevelReplacementBaselineExporter.ValidateCue(cuePath, imagePath, "MODE2/2352");

        await WriteJsonAsync(planPath, prepared.Plan, cancellationToken);
        await WriteTextAsync(
            checklistPath,
            BuildChecklist(prepared.Plan, readback, finalPaths),
            new UTF8Encoding(false),
            cancellationToken);
        await WriteTextAsync(
            guidePath,
            BuildGuide(prepared.Plan),
            new UTF8Encoding(false),
            cancellationToken);
        string cueName = Path.GetFileName(finalPaths.CuePath);
        string reveal =
            "#!/bin/zsh\n" +
            "set -euo pipefail\n\n" +
            $"cue_name={ShellSingleQuote(cueName)}\n" +
            "cue_path=\"${0:A:h}/${cue_name}\"\n" +
            "if [[ ! -f \"$cue_path\" ]]; then\n" +
            "  print -u2 -- \"Missing runtime candidate CUE: $cue_path\"\n" +
            "  exit 1\n" +
            "fi\n" +
            "/usr/bin/open -R \"$cue_path\"\n";
        await WriteTextAsync(revealPath, reveal, new UTF8Encoding(false), cancellationToken);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                revealPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }

        string cueSha256 = await HashFileAsync(cuePath, cancellationToken);
        string planSha256 = await HashFileAsync(planPath, cancellationToken);
        string checklistSha256 = await HashFileAsync(checklistPath, cancellationToken);
        string guideSha256 = await HashFileAsync(guidePath, cancellationToken);
        string helperSha256 = await HashFileAsync(revealPath, cancellationToken);

        UnusedLevel65StandaloneSpawnRuntimeCandidateReceipt receipt = new(
            ReceiptSchemaVersion,
            ProfileId,
            prepared.LockedBaseImagePath,
            prepared.FoundationImagePath,
            prepared.FoundationCuePath,
            finalPaths.OutputDirectoryPath,
            finalPaths.ImagePath,
            finalPaths.CuePath,
            finalPaths.PlanPath,
            finalPaths.ReceiptPath,
            finalPaths.ChecklistPath,
            finalPaths.GuidePath,
            finalPaths.RevealHelperPath,
            LockedBaseImageSha256,
            FoundationImageSha256,
            readback.OutputImageSha256,
            readback.OutputId65DataSha256,
            cueSha256,
            planSha256,
            checklistSha256,
            guideSha256,
            helperSha256,
            prepared.Plan.LoadCodes,
            readback.ChangedLogicalWadBytes,
            readback.ChangedPhysicalImageBytes,
            readback.RawSectorDiffs,
            readback.RawSectorDiffSha256,
            ExactPatchReadbackVerified: true,
            CoupledLandingAndPlayerAnchorVerified: true,
            DestinationSupportVerified: true,
            FoundationPreserved: true,
            ExecutablePreserved: true,
            RetailTownSquarePreserved: true,
            MusicTotalsExitSavePreserved: true,
            RawSectorIntegrityVerified: true,
            AtomicDirectoryPublicationCompleted: true,
            RollbackGuardsEnabled: true,
            StartupRecoveryPerformed: rollbackRecoveryVerified,
            RuntimePending: true,
            NormalCreateBinEnabled: false,
            PromotionAuthorized: false);
        await WriteJsonAsync(receiptPath, receipt, cancellationToken);
        await VerifyHandoffArtifactsAsync(
            prepared.Plan,
            receipt,
            imagePath,
            cuePath,
            planPath,
            receiptPath,
            checklistPath,
            guidePath,
            revealPath,
            cancellationToken);
    }

    private static async Task<Readback> VerifyOutputAsync(
        Prepared prepared,
        string foundation,
        string outputPath,
        CancellationToken cancellationToken)
    {
        DiscLayout outputLayout = DiscImage.DetectLayout(outputPath);
        if (outputLayout != prepared.Layout)
            throw new InvalidDataException("The standalone-spawn writer changed the MODE2 disc layout.");
        await using FileStream before = File.OpenRead(foundation);
        await using FileStream after = File.OpenRead(outputPath);
        if (before.Length != after.Length)
            throw new InvalidDataException("The standalone-spawn writer changed the disc image length.");
        DiscFileRecord beforeWad = RequireFileRecord(before, prepared.Layout, "WAD.WAD", WadLba, WadByteLength);
        DiscFileRecord afterWad = RequireFileRecord(after, outputLayout, "WAD.WAD", WadLba, WadByteLength);
        DiscFileRecord afterExecutable = RequireFileRecord(
            after,
            outputLayout,
            "SCUS_942.28",
            ExecutableLba,
            ExecutableByteLength);
        if (beforeWad != afterWad)
            throw new InvalidDataException("The standalone-spawn writer moved WAD.WAD.");

        foreach (UnusedLevel65StandaloneSpawnPatch patch in prepared.Plan.Patches)
        {
            RequireEqual(
                DiscImage.ReadFileBytes(
                    after,
                    outputLayout,
                    WadLba,
                    patch.WadOffset,
                    Convert.FromHexString(patch.AfterHex).Length),
                Convert.FromHexString(patch.AfterHex),
                $"authored {patch.Name} readback");
        }
        byte[] landing = DiscImage.ReadFileBytes(after, outputLayout, WadLba, LandingWadOffset, 0x10);
        byte[] player = DiscImage.ReadFileBytes(after, outputLayout, WadLba, PlayerAnchorWadOffset, PlayerAnchorByteLength);
        if (BinaryPrimitives.ReadInt32LittleEndian(landing.AsSpan(0, 4)) != AuthoredRawX ||
            BinaryPrimitives.ReadInt32LittleEndian(landing.AsSpan(4, 4)) != AuthoredRawY ||
            BinaryPrimitives.ReadInt32LittleEndian(landing.AsSpan(8, 4)) != LandingRawZ ||
            landing[0x0E] != YawByte ||
            BinaryPrimitives.ReadInt32LittleEndian(player.AsSpan(0x0C, 4)) != AuthoredRawX ||
            BinaryPrimitives.ReadInt32LittleEndian(player.AsSpan(0x10, 4)) != AuthoredRawY ||
            BinaryPrimitives.ReadInt32LittleEndian(player.AsSpan(0x14, 4)) != PlayerRawZ)
        {
            throw new InvalidDataException("The coupled authored landing/T92 readback changed.");
        }
        RequireHash(
            DiscImage.ReadFileBytes(after, outputLayout, afterExecutable.Lba, 0, afterExecutable.Size),
            ExecutableSha256,
            "standalone-spawn executable");
        RequireEqual(
            DiscImage.ReadFileBytes(before, prepared.Layout, WadLba, RetailTownSquareOverlayWadOffset, RetailTownSquareOverlayByteLength),
            DiscImage.ReadFileBytes(after, outputLayout, WadLba, RetailTownSquareOverlayWadOffset, RetailTownSquareOverlayByteLength),
            "retail Town Square overlay");
        RequireEqual(
            DiscImage.ReadFileBytes(before, prepared.Layout, WadLba, RetailTownSquareDataWadOffset, RetailTownSquareDataByteLength),
            DiscImage.ReadFileBytes(after, outputLayout, WadLba, RetailTownSquareDataWadOffset, RetailTownSquareDataByteLength),
            "retail Town Square data");
        RequireEqual(
            DiscImage.ReadFileBytes(before, prepared.Layout, WadLba, Id65OverlayWadOffset, Id65OverlayByteLength),
            DiscImage.ReadFileBytes(after, outputLayout, WadLba, Id65OverlayWadOffset, Id65OverlayByteLength),
            "ID65 overlay");

        LogicalDiff logical = CompareLogicalWad(
            before,
            after,
            prepared.Layout,
            beforeWad,
            prepared.Plan.Patches,
            cancellationToken);
        if (logical.ChangedBytes != 8 || logical.OutsideAllowedBytes != 0)
        {
            throw new InvalidDataException(
                $"The standalone-spawn logical diff changed {logical.ChangedBytes} bytes, including {logical.OutsideAllowedBytes} outside its two exact records.");
        }
        PhysicalDiff physical = ComparePhysicalImages(
            before,
            after,
            prepared.Plan.AffectedRawSectorLbas,
            cancellationToken);
        VerifyRawSectors(after, outputLayout, prepared.Plan.AffectedRawSectorLbas);
        string imageHash = await HashFileAsync(outputPath, cancellationToken);
        string dataHash = Hash(DiscImage.ReadFileBytes(
            after,
            outputLayout,
            WadLba,
            Id65DataWadOffset,
            Id65DataByteLength));
        string rawDiffHash = Hash(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(physical.RawSectorDiffs, JsonOptions)));
        return new(
            imageHash,
            dataHash,
            logical.ChangedBytes,
            physical.ChangedBytes,
            physical.RawSectorDiffs,
            rawDiffHash);
    }

    internal static async Task<UnusedLevel65StandaloneSpawnRuntimeCandidateResult> VerifyPublishedAsync(
        string outputDirectoryPath,
        CancellationToken cancellationToken = default) =>
        await ReadPublishedResultAsync(CreatePaths(outputDirectoryPath), cancellationToken);

    private static async Task<UnusedLevel65StandaloneSpawnRuntimeCandidateResult> ReadPublishedResultAsync(
        UnusedLevel65StandaloneSpawnRuntimeCandidatePaths paths,
        CancellationToken cancellationToken)
    {
        string[] required =
        [
            paths.ImagePath,
            paths.CuePath,
            paths.PlanPath,
            paths.ReceiptPath,
            paths.ChecklistPath,
            paths.GuidePath,
            paths.RevealHelperPath
        ];
        if (required.Any(path => !File.Exists(path)))
            throw new InvalidDataException("The published standalone-spawn directory is incomplete.");
        NativeLevelReplacementBaselineExporter.ValidateCue(paths.CuePath, paths.ImagePath, "MODE2/2352");
        UnusedLevel65StandaloneSpawnRuntimeCandidatePlan plan =
            JsonSerializer.Deserialize<UnusedLevel65StandaloneSpawnRuntimeCandidatePlan>(
                await File.ReadAllTextAsync(paths.PlanPath, cancellationToken),
                JsonOptions)
            ?? throw new InvalidDataException("The published standalone-spawn plan is invalid.");
        UnusedLevel65StandaloneSpawnRuntimeCandidateReceipt receipt =
            JsonSerializer.Deserialize<UnusedLevel65StandaloneSpawnRuntimeCandidateReceipt>(
                await File.ReadAllTextAsync(paths.ReceiptPath, cancellationToken),
                JsonOptions)
            ?? throw new InvalidDataException("The published standalone-spawn receipt is invalid.");
        VerifyReceiptPaths(receipt, paths);
        await VerifyHandoffArtifactsAsync(
            plan,
            receipt,
            paths.ImagePath,
            paths.CuePath,
            paths.PlanPath,
            paths.ReceiptPath,
            paths.ChecklistPath,
            paths.GuidePath,
            paths.RevealHelperPath,
            cancellationToken);
        string actualHash = await HashFileAsync(paths.ImagePath, cancellationToken);
        if (plan.ProfileId != ProfileId || receipt.ProfileId != ProfileId ||
            !string.Equals(actualHash, receipt.OutputImageSha256, StringComparison.OrdinalIgnoreCase) ||
            !receipt.RuntimePending || receipt.NormalCreateBinEnabled || receipt.PromotionAuthorized ||
            plan.RuntimePassed || plan.NormalCreateBinEnabled || plan.PromotionAuthorized)
        {
            throw new InvalidDataException("The published standalone-spawn runtime-pending boundary changed.");
        }
        UnusedLevel65StandaloneSpawnPublishedArtifactHashes hashes = new(
            await HashFileAsync(paths.CuePath, cancellationToken),
            await HashFileAsync(paths.PlanPath, cancellationToken),
            await HashFileAsync(paths.ReceiptPath, cancellationToken),
            await HashFileAsync(paths.ChecklistPath, cancellationToken),
            await HashFileAsync(paths.GuidePath, cancellationToken),
            await HashFileAsync(paths.RevealHelperPath, cancellationToken));
        return new(paths, plan, receipt, hashes);
    }

    private static async Task VerifyHandoffArtifactsAsync(
        UnusedLevel65StandaloneSpawnRuntimeCandidatePlan plan,
        UnusedLevel65StandaloneSpawnRuntimeCandidateReceipt receipt,
        string imagePath,
        string cuePath,
        string planPath,
        string receiptPath,
        string checklistPath,
        string guidePath,
        string helperPath,
        CancellationToken cancellationToken)
    {
        string expectedPlan = JsonSerializer.Serialize(plan, JsonOptions) + "\n";
        string expectedReceipt = JsonSerializer.Serialize(receipt, JsonOptions) + "\n";
        if (await File.ReadAllTextAsync(planPath, cancellationToken) != expectedPlan ||
            await File.ReadAllTextAsync(receiptPath, cancellationToken) != expectedReceipt)
        {
            throw new InvalidDataException("The standalone-spawn plan or receipt failed exact serialized readback.");
        }
        RequireTextHash(await HashFileAsync(imagePath, cancellationToken), receipt.OutputImageSha256, "handoff BIN");
        RequireTextHash(await HashFileAsync(cuePath, cancellationToken), receipt.OutputCueSha256, "handoff CUE");
        RequireTextHash(await HashFileAsync(planPath, cancellationToken), receipt.PlanSha256, "handoff plan");
        RequireTextHash(await HashFileAsync(checklistPath, cancellationToken), receipt.ChecklistSha256, "handoff checklist");
        RequireTextHash(await HashFileAsync(guidePath, cancellationToken), receipt.GuideSha256, "handoff guide");
        if (string.IsNullOrWhiteSpace(receipt.FinderHelperSha256) ||
            string.IsNullOrWhiteSpace(receipt.FinderHelperPath))
        {
            throw new InvalidDataException("The standalone-spawn handoff lost its requested Finder helper identity.");
        }
        RequireTextHash(await HashFileAsync(helperPath, cancellationToken), receipt.FinderHelperSha256, "handoff Finder helper");
        NativeLevelReplacementBaselineExporter.ValidateCue(cuePath, imagePath, "MODE2/2352");
        VerifyLoadCodes(plan.LoadCodes);
        if (!plan.LoadCodes.SequenceEqual(receipt.LoadCodes))
            throw new InvalidDataException("The standalone-spawn plan/receipt load codes differ.");

        string checklist = await File.ReadAllTextAsync(checklistPath, cancellationToken);
        string guide = await File.ReadAllTextAsync(guidePath, cancellationToken);
        string helper = await File.ReadAllTextAsync(helperPath, cancellationToken);
        string cueName = Path.GetFileName(receipt.OutputCuePath);
        string[] commonIdentity = [ProfileId, cueName, receipt.OutputImageSha256, "RUNTIME PENDING", "UNPROMOTED"];
        foreach (string identity in commonIdentity)
        {
            if (!guide.Contains(identity, StringComparison.Ordinal))
                throw new InvalidDataException($"The standalone-spawn guide omitted '{identity}'.");
        }
        if (!checklist.Contains(ProfileId, StringComparison.Ordinal) ||
            !checklist.Contains(cueName, StringComparison.Ordinal) ||
            !checklist.Contains(receipt.OutputImageSha256, StringComparison.Ordinal) ||
            !checklist.Contains("Runtime status: **pending / unpromoted**", StringComparison.Ordinal))
        {
            throw new InvalidDataException("The standalone-spawn checklist lost its exact identity/pending stamp.");
        }
        foreach (RuntimeCandidateLoadCode code in plan.LoadCodes)
        {
            if (!checklist.Contains(code.TestName, StringComparison.Ordinal) ||
                !checklist.Contains(code.InputCode, StringComparison.Ordinal) ||
                !guide.Contains(XmlEscape(code.TestName), StringComparison.Ordinal) ||
                !guide.Contains(XmlEscape(code.InputCode), StringComparison.Ordinal))
            {
                throw new InvalidDataException($"The standalone-spawn handoff omitted the full {code.TestName} load code.");
            }
        }
        if (!helper.Contains($"cue_name={ShellSingleQuote(cueName)}", StringComparison.Ordinal) ||
            !helper.Contains("/usr/bin/open -R \"$cue_path\"", StringComparison.Ordinal))
        {
            throw new InvalidDataException("The standalone-spawn Finder helper lost exact relative CUE targeting.");
        }
        if (!OperatingSystem.IsWindows() &&
            File.GetUnixFileMode(helperPath) !=
            (UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
             UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
             UnixFileMode.OtherRead | UnixFileMode.OtherExecute))
        {
            throw new InvalidDataException("The standalone-spawn Finder helper is not exact 0755.");
        }
    }

    private static void VerifyReceiptPaths(
        UnusedLevel65StandaloneSpawnRuntimeCandidateReceipt receipt,
        UnusedLevel65StandaloneSpawnRuntimeCandidatePaths paths)
    {
        string[] actual =
        [
            receipt.OutputDirectoryPath,
            receipt.OutputImagePath,
            receipt.OutputCuePath,
            receipt.PlanPath,
            receipt.ReceiptPath,
            receipt.ChecklistPath,
            receipt.GuidePath,
            receipt.FinderHelperPath ?? ""
        ];
        string[] expected =
        [
            paths.OutputDirectoryPath,
            paths.ImagePath,
            paths.CuePath,
            paths.PlanPath,
            paths.ReceiptPath,
            paths.ChecklistPath,
            paths.GuidePath,
            paths.RevealHelperPath
        ];
        if (!actual.Zip(expected).All(pair => PathEquals(pair.First, pair.Second)))
            throw new InvalidDataException("The standalone-spawn receipt artifact paths changed.");
    }

    private static UnusedLevel65StandaloneSpawnPoint DecodeSceneVertex(
        ReadOnlySpan<byte> vertexWord,
        ReadOnlySpan<byte> sectorHeader)
    {
        uint word = BinaryPrimitives.ReadUInt32LittleEndian(vertexWord);
        uint xyPos = BinaryPrimitives.ReadUInt32LittleEndian(sectorHeader.Slice(8, 4));
        uint zPos = BinaryPrimitives.ReadUInt32LittleEndian(sectorHeader.Slice(12, 4));
        int sectorX = (int)(xyPos >> 16);
        int sectorY = (int)(xyPos & 0xFFFF);
        int sectorZ = (int)((zPos >> 14) & 0xFFFF) >> 2;
        int x = sectorX + (int)(((word >> 19) & 0x1FFC) >> 2);
        int y = sectorY + (int)(((word >> 8) & 0x1FFC) >> 2);
        int z = sectorZ + (int)(((word << 3) & 0x1FFC) >> 3);
        if (((BinaryPrimitives.ReadUInt16LittleEndian(sectorHeader.Slice(4, 2)) >> 12) & 1) == 1)
            z >>= 3;
        return new(x, y, z);
    }

    private static IReadOnlyList<UnusedLevel65StandaloneSpawnPoint> DecodeCollisionTriangle(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != 12)
            throw new InvalidDataException("A standalone-spawn collision triangle is truncated.");
        uint xWord = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(0, 4));
        uint yWord = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(4, 4));
        uint zWord = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(8, 4));
        UnusedLevel65StandaloneSpawnPoint p1 = new(
            (int)(xWord & 0x3FFF),
            (int)(yWord & 0x3FFF),
            (int)(zWord & 0x3FFF));
        UnusedLevel65StandaloneSpawnPoint p2 = new(
            p1.X + SignedBits((int)((xWord >> 14) & 0x1FF), 9),
            p1.Y + SignedBits((int)((yWord >> 14) & 0x1FF), 9),
            p1.Z + (int)((zWord >> 16) & 0xFF));
        UnusedLevel65StandaloneSpawnPoint p3 = new(
            p1.X + SignedBits((int)((xWord >> 23) & 0x1FF), 9),
            p1.Y + SignedBits((int)((yWord >> 23) & 0x1FF), 9),
            p1.Z + (int)((zWord >> 24) & 0xFF));
        return [p1, p2, p3];
    }

    private static int SignedBits(int value, int bits)
    {
        int sign = 1 << (bits - 1);
        return (value ^ sign) - sign;
    }

    private static bool PointInsideTriangleStrictRaw16(
        IReadOnlyList<UnusedLevel65StandaloneSpawnPoint> points,
        int rawX,
        int rawY) =>
        PointInsideTriangleRaw16(points, rawX, rawY, strict: true);

    private static bool PointInsideTriangleInclusiveRaw16(
        IReadOnlyList<UnusedLevel65StandaloneSpawnPoint> points,
        int rawX,
        int rawY) =>
        PointInsideTriangleRaw16(points, rawX, rawY, strict: false);

    private static bool PointInsideTriangleRaw16(
        IReadOnlyList<UnusedLevel65StandaloneSpawnPoint> points,
        int rawX,
        int rawY,
        bool strict)
    {
        if (points.Count != 3)
            return false;
        long Cross(UnusedLevel65StandaloneSpawnPoint a, UnusedLevel65StandaloneSpawnPoint b) =>
            ((long)((b.X * 16) - (a.X * 16)) * (rawY - (a.Y * 16))) -
            ((long)((b.Y * 16) - (a.Y * 16)) * (rawX - (a.X * 16)));
        long c1 = Cross(points[0], points[1]);
        long c2 = Cross(points[1], points[2]);
        long c3 = Cross(points[2], points[0]);
        if (strict)
            return (c1 > 0 && c2 > 0 && c3 > 0) || (c1 < 0 && c2 < 0 && c3 < 0);
        return (c1 >= 0 && c2 >= 0 && c3 >= 0) || (c1 <= 0 && c2 <= 0 && c3 <= 0);
    }

    private static bool TryInterpolateRawZ(
        IReadOnlyList<UnusedLevel65StandaloneSpawnPoint> points,
        int rawX,
        int rawY,
        out int rawZ,
        out long normalZ)
    {
        UnusedLevel65StandaloneSpawnPoint a = points[0];
        UnusedLevel65StandaloneSpawnPoint b = points[1];
        UnusedLevel65StandaloneSpawnPoint c = points[2];
        long abX = b.X - a.X;
        long abY = b.Y - a.Y;
        long abZ = b.Z - a.Z;
        long acX = c.X - a.X;
        long acY = c.Y - a.Y;
        long acZ = c.Z - a.Z;
        long normalX = (abY * acZ) - (abZ * acY);
        long normalY = (abZ * acX) - (abX * acZ);
        normalZ = (abX * acY) - (abY * acX);
        if (normalZ == 0)
        {
            rawZ = 0;
            return false;
        }
        long numerator = ((long)a.Z * 16 * normalZ) -
                         (normalX * (rawX - (a.X * 16L))) -
                         (normalY * (rawY - (a.Y * 16L)));
        rawZ = checked((int)Math.Round((double)numerator / normalZ, MidpointRounding.AwayFromZero));
        return true;
    }

    private static CollisionIndex DecodeCollisionIndex(
        ReadOnlySpan<byte> treeCapacity,
        ReadOnlySpan<byte> blockCapacity,
        int usedBlockBytes,
        int triangleCount)
    {
        if ((treeCapacity.Length & 1) != 0 || (blockCapacity.Length & 1) != 0 ||
            usedBlockBytes <= 0 || (usedBlockBytes & 1) != 0 || usedBlockBytes > blockCapacity.Length)
        {
            throw new InvalidDataException("The standalone-spawn collision index capacities are invalid.");
        }
        byte[] tree = treeCapacity.ToArray();
        byte[] blocks = blockCapacity.ToArray();
        int usedWords = usedBlockBytes / 2;
        List<int> starts = [];
        for (int wordOffset = 0; wordOffset < usedWords; wordOffset++)
        {
            ushort word = BinaryPrimitives.ReadUInt16LittleEndian(blocks.AsSpan(wordOffset * 2, 2));
            if ((word & 0x8000) != 0)
                starts.Add(wordOffset);
        }
        if (starts.Count < 2 || starts[^1] != usedWords - 1 ||
            BinaryPrimitives.ReadUInt16LittleEndian(blocks.AsSpan(starts[^1] * 2, 2)) != 0x8000)
        {
            throw new InvalidDataException("The standalone-spawn collision blocks lost their terminal sentinel.");
        }
        Dictionary<int, int[]> groups = [];
        for (int group = 0; group < starts.Count - 1; group++)
        {
            int start = starts[group];
            int end = starts[group + 1];
            int[] indexes = new int[end - start];
            for (int index = 0; index < indexes.Length; index++)
            {
                ushort word = BinaryPrimitives.ReadUInt16LittleEndian(blocks.AsSpan((start + index) * 2, 2));
                if ((index == 0) != ((word & 0x8000) != 0))
                    throw new InvalidDataException("A collision group has an invalid start marker.");
                indexes[index] = word & 0x7FFF;
                if (indexes[index] >= triangleCount)
                    throw new InvalidDataException("A collision group references outside the triangle table.");
            }
            groups.Add(start, indexes);
        }

        int ReadWord(int offset)
        {
            if (offset < 0 || (offset & 1) != 0 || offset + 2 > tree.Length)
                throw new InvalidDataException("A collision tree pointer escaped its capacity.");
            return BinaryPrimitives.ReadUInt16LittleEndian(tree.AsSpan(offset, 2));
        }
        int ReadLength(int offset)
        {
            int length = ReadWord(offset);
            if (length > 256 || offset + ((length + 1) * 2L) > tree.Length)
                throw new InvalidDataException("A collision tree segment length is invalid.");
            return length;
        }

        Dictionary<UnusedLevel65StandaloneSpawnCollisionCell, CollisionCell> cells = [];
        int zLength = ReadLength(0);
        for (int z = 0; z < zLength; z++)
        {
            int yOffset = ReadWord((z + 1) * 2);
            if (yOffset == 0xFFFF)
                continue;
            int yLength = ReadLength(yOffset);
            for (int y = 0; y < yLength; y++)
            {
                int xOffset = ReadWord(yOffset + ((y + 1) * 2));
                if (xOffset == 0xFFFF)
                    continue;
                int xLength = ReadLength(xOffset);
                for (int x = 0; x < xLength; x++)
                {
                    int blockWordOffset = ReadWord(xOffset + ((x + 1) * 2));
                    if (blockWordOffset == 0xFFFF)
                        continue;
                    if (!groups.TryGetValue(blockWordOffset, out int[]? indexes))
                        throw new InvalidDataException("A collision tree cell points outside an exact group start.");
                    UnusedLevel65StandaloneSpawnCollisionCell key = new(x, y, z);
                    if (!cells.TryAdd(key, new(blockWordOffset, indexes)))
                        throw new InvalidDataException("A collision cell is duplicated in the tree.");
                }
            }
        }
        if (!cells.Values.Select(value => value.BlockWordOffset).Distinct().Order().SequenceEqual(groups.Keys.Order()))
            throw new InvalidDataException("The collision index has an unreferenced group.");
        return new(cells);
    }

    private static LogicalDiff CompareLogicalWad(
        FileStream before,
        FileStream after,
        DiscLayout layout,
        DiscFileRecord wad,
        IReadOnlyList<UnusedLevel65StandaloneSpawnPatch> patches,
        CancellationToken cancellationToken)
    {
        long changed = 0;
        long outside = 0;
        const int chunkBytes = 1 << 20;
        for (long offset = 0; offset < wad.Size; offset += chunkBytes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int length = checked((int)Math.Min(chunkBytes, wad.Size - offset));
            byte[] left = DiscImage.ReadFileBytes(before, layout, wad.Lba, offset, length);
            byte[] right = DiscImage.ReadFileBytes(after, layout, wad.Lba, offset, length);
            for (int index = 0; index < length; index++)
            {
                if (left[index] == right[index])
                    continue;
                changed++;
                long wadOffset = offset + index;
                bool allowed = patches.Any(patch =>
                    wadOffset >= patch.WadOffset &&
                    wadOffset < patch.WadOffset + Convert.FromHexString(patch.AfterHex).Length);
                if (!allowed)
                    outside++;
            }
        }
        return new(changed, outside);
    }

    private static PhysicalDiff ComparePhysicalImages(
        FileStream before,
        FileStream after,
        IReadOnlyList<int> allowedRawSectorLbas,
        CancellationToken cancellationToken)
    {
        HashSet<int> allowed = allowedRawSectorLbas.ToHashSet();
        byte[] left = new byte[RawSectorByteLength];
        byte[] right = new byte[RawSectorByteLength];
        List<UnusedLevel65StandaloneSpawnRawSectorDiff> diffs = [];
        long changedBytes = 0;
        int sectorCount = checked((int)(before.Length / RawSectorByteLength));
        before.Position = 0;
        after.Position = 0;
        for (int lba = 0; lba < sectorCount; lba++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadExactly(before, left);
            ReadExactly(after, right);
            if (left.AsSpan().SequenceEqual(right))
                continue;
            if (!allowed.Contains(lba))
                throw new InvalidDataException($"The standalone-spawn physical diff escaped to raw sector {lba}.");
            int header = CountDiff(left, right, 0, 16);
            int subheader = CountDiff(left, right, 16, 8);
            int payload = CountDiff(left, right, 24, 2048);
            int edc = CountDiff(left, right, 2072, 4);
            const int reserved = 0;
            int eccP = CountDiff(left, right, 2076, 172);
            int eccQ = CountDiff(left, right, 2248, 104);
            int total = header + subheader + payload + edc + reserved + eccP + eccQ;
            int actualTotal = CountDiff(left, right, 0, RawSectorByteLength);
            if (header != 0 || subheader != 0 || reserved != 0)
            {
                throw new InvalidDataException(
                    $"Standalone-spawn sector {lba} changed forbidden MODE2 header/subheader/reserved bytes.");
            }
            if (total != actualTotal)
                throw new InvalidDataException($"Standalone-spawn sector {lba} raw regions do not partition all changed bytes.");
            changedBytes += total;
            diffs.Add(new(lba, header, subheader, payload, edc, reserved, eccP, eccQ, total));
        }
        if (!diffs.Select(diff => diff.RawSectorLba).SequenceEqual(allowedRawSectorLbas))
            throw new InvalidDataException("The standalone-spawn writer did not change both exact owned raw sectors.");
        return new(changedBytes, diffs);
    }

    private static void VerifyRawSectors(
        FileStream stream,
        DiscLayout layout,
        IReadOnlyList<int> rawSectorLbas)
    {
        int verified = RawMode2Form1SectorIntegrity.VerifyAbsoluteSectors(
            stream,
            layout,
            rawSectorLbas.Select(lba => (lba, 1)).ToArray());
        if (verified != rawSectorLbas.Count)
            throw new InvalidDataException("The standalone-spawn raw-sector verification count changed.");
        foreach (int lba in rawSectorLbas)
            RawMode2Form1SectorIntegrity.VerifyDuplicatedSubmode(stream, layout, lba, XaDataSubmode);
    }

    private static int[] RawSectorLbasForWadRange(DiscLayout layout, long wadOffset, int byteLength)
    {
        long firstLogical = wadOffset / LogicalSectorByteLength;
        long lastLogical = (wadOffset + byteLength - 1) / LogicalSectorByteLength;
        return Enumerable.Range(
                checked(WadLba + (int)firstLogical),
                checked((int)(lastLogical - firstLogical + 1)))
            .ToArray();
    }

    private static string BuildChecklist(
        UnusedLevel65StandaloneSpawnRuntimeCandidatePlan plan,
        Readback readback,
        UnusedLevel65StandaloneSpawnRuntimeCandidatePaths paths)
    {
        VerifyLoadCodes(plan.LoadCodes);
        StringBuilder text = new();
        text.AppendLine("# ID65 Standalone Spawn Ownership - Disposable Runtime Checklist");
        text.AppendLine();
        text.AppendLine("This candidate changes only ID65's landing and T92 player-anchor X/Y fields. Music, totals, exit, save code, the executable, retail levels, and the authored foundation model are unchanged.");
        text.AppendLine();
        text.AppendLine($"- Profile: `{ProfileId}`");
        text.AppendLine($"- CUE: `{Path.GetFileName(paths.CuePath)}` (load the CUE, not the BIN)");
        text.AppendLine($"- Finder helper: `{Path.GetFileName(paths.RevealHelperPath)}`");
        text.AppendLine($"- BIN SHA-256: `{readback.OutputImageSha256}`");
        text.AppendLine($"- ID65 data SHA-256: `{readback.OutputId65DataSha256}`");
        text.AppendLine($"- Authored landing: raw ({plan.AuthoredLandingRawX}, {plan.AuthoredLandingRawY}, {plan.AuthoredLandingRawZ}), yaw `0x{plan.YawByte:X2}`");
        text.AppendLine($"- Authored T92: raw ({plan.AuthoredPlayerRawX}, {plan.AuthoredPlayerRawY}, {plan.AuthoredPlayerRawZ})");
        text.AppendLine("- Runtime status: **pending / unpromoted**. This is not editor/Create BIN/release integration.");
        text.AppendLine();
        RuntimeCandidateTestHandoff.AppendLoadCodeTable(text, plan.LoadCodes);
        text.AppendLine("## Safety setup");
        text.AppendLine();
        text.AppendLine("1. Disable DuckStation cheats, save states, and both memory-card slots.");
        text.AppendLine("2. Boot this exact CUE. Reach controllable gameplay normally.");
        text.AppendLine("3. Load ID65: Select; R1, R2, L1, L2, R1, L1, R2, L2; Left; Down.");
        text.AppendLine();
        text.AppendLine("## Focused gate");
        text.AppendLine();
        text.AppendLine("- [ ] The fly-in ends on the left native apron marked START in the guide, not at the old spawn 153 scene units behind it along Y.");
        text.AppendLine("- [ ] Spyro is standing normally on the flat visible surface; no sinking, pop-up, hovering, snag, pass-through, or immediate death occurs.");
        text.AppendLine("- [ ] The new 45-degree foundation triangle is visible to Spyro's right/front, proving the authored spawn is outside it rather than supported by its still-pending collision.");
        text.AppendLine("- [ ] Walk a small circle on the native apron, then cross onto and back off the foundation only if the separate foundation gate has already passed in this session.");
        text.AppendLine("- [ ] Cause one normal in-level death without Return Home, Exit Level, saving, or a memory card. Respawn must return to the same authored left-apron location and yaw.");
        text.AppendLine("- [ ] Reset/cold boot the same CUE, re-enter ID65, and confirm the same authored location and yaw return without a save state.");
        text.AppendLine("- [ ] Open pause/Inventory only to confirm normal responsiveness and TOWN SQUARE identity; do not save or exit the level.");
        text.AppendLine();
        text.AppendLine("## Isolation controls");
        text.AppendLine();
        text.AppendLine("- [ ] Retail Town Square loads at its normal landing.");
        text.AppendLine("- [ ] Gnasty's Loot and Sunny Flight still load normally.");
        text.AppendLine("- [ ] No music, total, portal/exit, or save-ownership claim is made by this gate.");
        text.AppendLine();
        text.AppendLine("PASS requires authored cold entry, death respawn, and reset/cold-boot restoration at the marked native apron, plus all three isolation controls. Any unsafe landing or different death/reset destination is a FAIL and leaves standalone spawn ownership unpromoted.");
        return text.ToString();
    }

    private static void VerifyLoadCodes(IReadOnlyList<RuntimeCandidateLoadCode> loadCodes)
    {
        if (loadCodes.Count != 4 ||
            loadCodes[0].TestName != "ID65 candidate" || loadCodes[0].LevelId != 65 ||
            loadCodes[1].TestName != "Retail Town Square" || loadCodes[1].LevelId != 13 ||
            loadCodes[2].TestName != "Gnasty's Loot" || loadCodes[2].LevelId != 64 ||
            loadCodes[3].TestName != "Sunny Flight" || loadCodes[3].LevelId != 15 ||
            loadCodes.Any(code => string.IsNullOrWhiteSpace(code.InputCode)))
        {
            throw new InvalidDataException("The exact four standalone-spawn comparison load codes changed.");
        }
    }

    private static string BuildGuide(UnusedLevel65StandaloneSpawnRuntimeCandidatePlan plan)
    {
        VerifyLoadCodes(plan.LoadCodes);
        string cueName = OutputPrefix + ".cue";
        StringBuilder svg = new();
        svg.AppendLine("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"1200\" height=\"1000\" viewBox=\"0 0 1200 1000\">");
        svg.AppendLine("<rect width=\"1200\" height=\"1000\" fill=\"#10172a\"/>");
        svg.AppendLine("<text x=\"60\" y=\"58\" fill=\"#ffffff\" font-family=\"sans-serif\" font-size=\"32\" font-weight=\"700\">ID65 Standalone Spawn Ownership</text>");
        svg.AppendLine("<rect x=\"770\" y=\"25\" width=\"370\" height=\"48\" rx=\"9\" fill=\"#6f2433\" stroke=\"#ff9cac\" stroke-width=\"2\"/>");
        svg.AppendLine("<text x=\"955\" y=\"55\" text-anchor=\"middle\" fill=\"#ffffff\" font-family=\"sans-serif\" font-size=\"14\" font-weight=\"700\">RUNTIME PENDING / UNPROMOTED</text>");
        svg.AppendLine("<text x=\"60\" y=\"92\" fill=\"#b9c7e8\" font-family=\"sans-serif\" font-size=\"17\">Top-down native entrance apron; yaw 0x40 faces upward</text>");

        svg.AppendLine("<rect x=\"120\" y=\"120\" width=\"960\" height=\"365\" rx=\"12\" fill=\"#33445b\" stroke=\"#8ea2c8\" stroke-width=\"4\"/>");
        svg.AppendLine("<polygon points=\"430,485 970,485 700,120\" fill=\"#7b4bc4\" fill-opacity=\"0.72\" stroke=\"#d5b8ff\" stroke-width=\"4\"/>");
        svg.AppendLine("<circle cx=\"335\" cy=\"285\" r=\"30\" fill=\"#5cffad\" stroke=\"#ffffff\" stroke-width=\"5\"/>");
        svg.AppendLine("<path d=\"M335 285 L335 195\" stroke=\"#5cffad\" stroke-width=\"8\" marker-end=\"url(#arrow)\"/>");
        svg.AppendLine("<defs><marker id=\"arrow\" markerWidth=\"10\" markerHeight=\"10\" refX=\"8\" refY=\"3\" orient=\"auto\"><path d=\"M0,0 L0,6 L9,3 z\" fill=\"#5cffad\"/></marker></defs>");
        svg.AppendLine("<text x=\"385\" y=\"278\" fill=\"#5cffad\" font-family=\"sans-serif\" font-size=\"26\" font-weight=\"700\">AUTHORED START</text>");
        svg.AppendLine("<text x=\"385\" y=\"309\" fill=\"#dcecff\" font-family=\"sans-serif\" font-size=\"17\">native T1353 support, outside purple foundation</text>");
        svg.AppendLine("<circle cx=\"300\" cy=\"440\" r=\"18\" fill=\"#ff7f7f\"/>");
        svg.AppendLine("<path d=\"M300 417 L328 321\" stroke=\"#ff7f7f\" stroke-width=\"4\" stroke-dasharray=\"12 9\"/>");
        svg.AppendLine("<text x=\"330\" y=\"447\" fill=\"#ffb1b1\" font-family=\"sans-serif\" font-size=\"17\">OLD START (153 scene units behind)</text>");
        svg.AppendLine($"<text x=\"720\" y=\"428\" fill=\"#f0dcff\" font-family=\"sans-serif\" font-size=\"14\">Raw ({plan.AuthoredLandingRawX}, {plan.AuthoredLandingRawY})</text>");
        svg.AppendLine("<text x=\"720\" y=\"452\" fill=\"#f0dcff\" font-family=\"sans-serif\" font-size=\"14\">19.75 scene units outside foundation triangle</text>");

        svg.AppendLine("<rect x=\"60\" y=\"510\" width=\"1080\" height=\"128\" rx=\"10\" fill=\"#18243d\" stroke=\"#425a82\" stroke-width=\"2\"/>");
        svg.AppendLine($"<text x=\"82\" y=\"540\" fill=\"#8fe1ff\" font-family=\"monospace\" font-size=\"13\">PROFILE: {XmlEscape(ProfileId)}</text>");
        svg.AppendLine($"<text x=\"82\" y=\"570\" fill=\"#ffffff\" font-family=\"monospace\" font-size=\"13\">CUE: {XmlEscape(cueName)}</text>");
        svg.AppendLine($"<text x=\"82\" y=\"600\" fill=\"#ffffff\" font-family=\"monospace\" font-size=\"13\">BIN SHA-256: {XmlEscape(ExpectedOutputImageSha256)}</text>");
        svg.AppendLine("<text x=\"82\" y=\"625\" fill=\"#ffcf70\" font-family=\"sans-serif\" font-size=\"13\">Load the exact CUE, never the BIN. Cheats, save states, and memory cards off.</text>");

        svg.AppendLine("<text x=\"60\" y=\"674\" fill=\"#ffffff\" font-family=\"sans-serif\" font-size=\"21\" font-weight=\"700\">Exact comparison load codes</text>");
        int codeY = 706;
        foreach (RuntimeCandidateLoadCode code in plan.LoadCodes)
        {
            string label = $"{code.TestName} (ID{code.LevelId}): {code.InputCode}";
            svg.AppendLine($"<text x=\"72\" y=\"{codeY}\" fill=\"#dcecff\" font-family=\"monospace\" font-size=\"12\">{XmlEscape(label)}</text>");
            codeY += 28;
        }

        svg.AppendLine("<rect x=\"60\" y=\"826\" width=\"1080\" height=\"130\" rx=\"10\" fill=\"#192f2c\" stroke=\"#55b99b\" stroke-width=\"2\"/>");
        svg.AppendLine("<text x=\"82\" y=\"858\" fill=\"#8fffd4\" font-family=\"sans-serif\" font-size=\"18\" font-weight=\"700\">Runtime pass boundary</text>");
        svg.AppendLine("<text x=\"82\" y=\"885\" fill=\"#e6fff6\" font-family=\"sans-serif\" font-size=\"14\">Cold entry, normal standing and movement, one in-level death respawn, then reset/cold-boot restoration</text>");
        svg.AppendLine("<text x=\"82\" y=\"910\" fill=\"#e6fff6\" font-family=\"sans-serif\" font-size=\"14\">must all return to AUTHORED START. Then load all three retail controls with the exact codes above.</text>");
        svg.AppendLine("<text x=\"82\" y=\"937\" fill=\"#ffcf70\" font-family=\"sans-serif\" font-size=\"13\">No music, totals, exit, save, normal Create BIN, editor, or release promotion is claimed by this artifact.</text>");
        svg.AppendLine("</svg>");
        return svg.ToString();
    }

    private static FileStream AcquireWriterLease(string leasePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(leasePath) ?? ".");
        FileStream lease;
        try
        {
            lease = new(
                leasePath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.ReadWrite,
                bufferSize: 1,
                FileOptions.None);
        }
        catch (IOException ex) when (!OperatingSystem.IsWindows() &&
                                     (ex.HResult & 0xFFFF) is 11 or 35)
        {
            throw new IOException("Another standalone-spawn writer is active; no operation or output move was started.", ex);
        }
        try
        {
            if (OperatingSystem.IsWindows())
            {
                try
                {
                    lease.Lock(0, 1);
                }
                catch (IOException ex) when ((ex.HResult & 0xFFFF) == 33)
                {
                    throw new IOException("Another standalone-spawn writer is active; no operation or output move was started.", ex);
                }
                return lease;
            }
            int fd = checked((int)lease.SafeFileHandle.DangerousGetHandle());
            if (NativeFlock(fd, 2 | 4) != 0)
            {
                int error = Marshal.GetLastPInvokeError();
                Win32Exception nativeError = new(error);
                if (error is 11 or 35)
                    throw new IOException("Another standalone-spawn writer is active; no operation or output move was started.", nativeError);
                throw new IOException($"The standalone-spawn OS lease failed (errno {error}: {nativeError.Message}).", nativeError);
            }
            return lease;
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    [DllImport("libc", EntryPoint = "flock", SetLastError = true)]
    private static extern int NativeFlock(int fileDescriptor, int operation);

    private static bool RecoverOwnedOperations(UnusedLevel65StandaloneSpawnRuntimeCandidatePaths paths)
    {
        string journalPath = Path.Combine(paths.OperationsDirectoryPath, JournalFileName);
        if (!File.Exists(journalPath))
            return false;
        RejectReparsePoint(journalPath, "standalone-spawn operation journal");
        Journal journal = JsonSerializer.Deserialize<Journal>(File.ReadAllText(journalPath), JsonOptions)
            ?? throw new InvalidDataException("The standalone-spawn recovery journal is invalid.");
        string expectedStage = Path.Combine(paths.OperationsDirectoryPath, StagePrefix + journal.OperationId);
        string expectedBackup = Path.Combine(paths.OperationsDirectoryPath, BackupPrefix + journal.OperationId);
        if (journal.SchemaVersion != JournalSchemaVersion ||
            journal.OperationKind != JournalOperationKind ||
            !Guid.TryParseExact(journal.OperationId, "N", out _) ||
            !PathEquals(journal.OutputDirectoryPath, paths.OutputDirectoryPath) ||
            !PathEquals(journal.StageDirectoryPath, expectedStage) ||
            !PathEquals(journal.BackupDirectoryPath, expectedBackup) ||
            !IsOwnedChild(journal.StageDirectoryPath, paths.OperationsDirectoryPath, StagePrefix) ||
            !IsOwnedChild(journal.BackupDirectoryPath, paths.OperationsDirectoryPath, BackupPrefix) ||
            journal.Phase is not (PhaseStaged or PhaseBackupIntent or PhasePreviousBackedUp or PhasePublishIntent or PhasePublished))
        {
            throw new InvalidDataException("The standalone-spawn recovery journal escaped its owned operation paths.");
        }

        bool outputExists = Directory.Exists(paths.OutputDirectoryPath);
        bool stageExists = Directory.Exists(journal.StageDirectoryPath);
        bool backupExists = Directory.Exists(journal.BackupDirectoryPath);
        if (outputExists)
            RejectReparsePoint(paths.OutputDirectoryPath, "published standalone-spawn candidate");
        if (stageExists)
            RejectReparsePoint(journal.StageDirectoryPath, "standalone-spawn stage");
        if (backupExists)
            RejectReparsePoint(journal.BackupDirectoryPath, "standalone-spawn backup");

        RecoveryAction action = journal.HadPreviousCandidate
            ? (journal.Phase, outputExists, stageExists, backupExists) switch
            {
                (PhaseStaged, true, true, false) => RecoveryAction.KeepOutputDeleteStage,
                (PhaseBackupIntent, true, true, false) => RecoveryAction.KeepOutputDeleteStage,
                (PhaseBackupIntent, false, true, true) => RecoveryAction.RestoreBackupDeleteStage,
                (PhasePreviousBackedUp, false, true, true) => RecoveryAction.RestoreBackupDeleteStage,
                (PhasePublishIntent, false, true, true) => RecoveryAction.RestoreBackupDeleteStage,
                (PhasePublishIntent, true, false, true) => RecoveryAction.DeleteOutputRestoreBackup,
                (PhasePublished, true, false, true) => RecoveryAction.DeleteOutputRestoreBackup,
                (PhasePublished, true, false, false) => RecoveryAction.KeepCommittedOutput,
                _ => RecoveryAction.FailClosed
            }
            : (journal.Phase, outputExists, stageExists, backupExists) switch
            {
                (PhaseStaged, false, true, false) => RecoveryAction.DeleteStage,
                (PhasePublishIntent, false, true, false) => RecoveryAction.DeleteStage,
                (PhasePublishIntent, true, false, false) => RecoveryAction.DeleteOutput,
                (PhasePublished, true, false, false) => RecoveryAction.DeleteOutput,
                _ => RecoveryAction.FailClosed
            };

        if (action == RecoveryAction.FailClosed)
        {
            throw new IOException(
                $"Standalone-spawn recovery found an ambiguous state: phase={journal.Phase}, " +
                $"hadPrevious={journal.HadPreviousCandidate}, output={outputExists}, stage={stageExists}, backup={backupExists}.");
        }

        if (action == RecoveryAction.DeleteOutputRestoreBackup)
        {
            DeleteOwnedDirectory(paths.OutputDirectoryPath, paths.PublicationParentPath, OutputDirectoryName);
            Directory.Move(journal.BackupDirectoryPath, paths.OutputDirectoryPath);
        }
        else if (action == RecoveryAction.RestoreBackupDeleteStage)
        {
            Directory.Move(journal.BackupDirectoryPath, paths.OutputDirectoryPath);
        }
        else if (action == RecoveryAction.DeleteOutput)
        {
            DeleteOwnedDirectory(paths.OutputDirectoryPath, paths.PublicationParentPath, OutputDirectoryName);
        }
        if (action is RecoveryAction.KeepOutputDeleteStage or
            RecoveryAction.RestoreBackupDeleteStage or
            RecoveryAction.DeleteStage)
        {
            DeleteOwnedDirectory(journal.StageDirectoryPath, paths.OperationsDirectoryPath, StagePrefix);
        }

        if (Directory.Exists(journal.StageDirectoryPath) || Directory.Exists(journal.BackupDirectoryPath))
            throw new IOException("Standalone-spawn recovery left an owned stage or backup directory.");
        File.Delete(journalPath);
        return true;
    }

    private static void WriteJournal(
        UnusedLevel65StandaloneSpawnRuntimeCandidatePaths paths,
        Journal journal)
    {
        Directory.CreateDirectory(paths.OperationsDirectoryPath);
        string journalPath = Path.Combine(paths.OperationsDirectoryPath, JournalFileName);
        string temporary = journalPath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(journal, JsonOptions) + "\n", new UTF8Encoding(false));
        using (FileStream stream = new(temporary, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
            stream.Flush(flushToDisk: true);
        File.Move(temporary, journalPath, overwrite: true);
    }

    private static void DeleteJournal(UnusedLevel65StandaloneSpawnRuntimeCandidatePaths paths)
    {
        string journalPath = Path.Combine(paths.OperationsDirectoryPath, JournalFileName);
        if (File.Exists(journalPath))
            File.Delete(journalPath);
    }

    private static void DeleteOwnedDirectory(string path, string expectedParent, string expectedNameOrPrefix)
    {
        string full = Path.GetFullPath(path);
        string parent = Path.GetDirectoryName(full) ?? "";
        string name = Path.GetFileName(full);
        bool exactOutput = string.Equals(expectedNameOrPrefix, OutputDirectoryName, StringComparison.Ordinal) &&
                           string.Equals(name, OutputDirectoryName, StringComparison.Ordinal);
        bool operation = !string.Equals(expectedNameOrPrefix, OutputDirectoryName, StringComparison.Ordinal) &&
                         name.StartsWith(expectedNameOrPrefix, StringComparison.Ordinal) &&
                         name.Length > expectedNameOrPrefix.Length;
        if (!PathEquals(parent, expectedParent) || (!exactOutput && !operation))
            throw new InvalidOperationException("Refused to delete a directory outside standalone-spawn ownership.");
        RejectReparsePoint(full, "owned standalone-spawn directory");
        Directory.Delete(full, recursive: true);
    }

    private static bool IsOwnedChild(string path, string parent, string prefix)
    {
        string full = Path.GetFullPath(path);
        return PathEquals(Path.GetDirectoryName(full), parent) &&
               Path.GetFileName(full).StartsWith(prefix, StringComparison.Ordinal) &&
               Path.GetFileName(full).Length > prefix.Length;
    }

    private static void RequireSafeRoles(
        string lockedBase,
        string foundation,
        string foundationCue,
        UnusedLevel65StandaloneSpawnRuntimeCandidatePaths paths)
    {
        foreach (string source in new[] { lockedBase, foundation, foundationCue })
        {
            if (IsDescendantOrEqual(source, paths.OutputDirectoryPath) ||
                IsDescendantOrEqual(paths.OutputDirectoryPath, source))
            {
                throw new InvalidOperationException("Standalone-spawn source and output roles overlap.");
            }
        }
        foreach (string artifact in new[]
                 {
                     paths.ImagePath, paths.CuePath, paths.PlanPath, paths.ReceiptPath,
                     paths.ChecklistPath, paths.GuidePath, paths.RevealHelperPath
                 })
        {
            if (!IsDescendantOrEqual(artifact, paths.OutputDirectoryPath) || PathEquals(artifact, paths.OutputDirectoryPath))
                throw new InvalidOperationException("A standalone-spawn artifact escaped its output directory.");
        }
    }

    private static bool IsDescendantOrEqual(string candidate, string ancestor)
    {
        string child = Path.GetFullPath(candidate).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string root = Path.GetFullPath(ancestor).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return child.StartsWith(root, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) ||
               PathEquals(candidate, ancestor);
    }

    private static bool PathEquals(string? left, string? right) =>
        string.Equals(
            Path.GetFullPath(left ?? ""),
            Path.GetFullPath(right ?? ""),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static void RejectReparsePoint(string path, string label)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException($"The {label} is a reparse point.");
    }

    private static string RequireFile(string path, string label)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            throw new FileNotFoundException($"The {label} is missing.", path);
        string full = Path.GetFullPath(path);
        RejectReparsePoint(full, label);
        return full;
    }

    private static string RequireDirectory(string path, string label)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            throw new DirectoryNotFoundException($"The {label} is missing: {path}");
        string full = Path.GetFullPath(path);
        RejectReparsePoint(full, label);
        return full;
    }

    private static DiscFileRecord RequireFileRecord(
        FileStream image,
        DiscLayout layout,
        string name,
        int expectedLba,
        int expectedSize)
    {
        DiscFileRecord record = DiscImage.FindRootFileRecord(
            image,
            layout,
            candidate => string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase));
        if (record.Lba != expectedLba || record.Size != expectedSize)
            throw new InvalidDataException($"{name} resolved to LBA {record.Lba}/0x{record.Size:X}.");
        return record;
    }

    private static void RequireMode2(DiscLayout layout, string label)
    {
        if (layout.SectorSize != RawSectorByteLength || layout.UserOffset != UserDataOffset)
            throw new InvalidDataException($"The {label} is not exact MODE2/2352.");
    }

    private static async Task RequireFileHashAsync(string path, string expected, CancellationToken cancellationToken) =>
        RequireTextHash(await HashFileAsync(path, cancellationToken), expected, Path.GetFileName(path));

    private static async Task<string> HashFileAsync(string path, CancellationToken cancellationToken)
    {
        await using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken)).ToLowerInvariant();
    }

    private static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static void RequireHash(ReadOnlySpan<byte> bytes, string expected, string label) =>
        RequireTextHash(Hash(bytes), expected, label);

    private static void RequireTextHash(string actual, string expected, string label)
    {
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"The {label} SHA-256 was {actual}, expected {expected}.");
    }

    private static void RequireHex(ReadOnlySpan<byte> bytes, string expected, string label)
    {
        if (!bytes.SequenceEqual(Convert.FromHexString(expected)))
            throw new InvalidDataException($"The exact {label} bytes changed.");
    }

    private static void RequireEqual(ReadOnlySpan<byte> actual, ReadOnlySpan<byte> expected, string label)
    {
        if (!actual.SequenceEqual(expected))
            throw new InvalidDataException($"The exact {label} bytes changed.");
    }

    private static int CountDifferentBytes(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        if (left.Length != right.Length)
            throw new ArgumentException("Byte arrays must have equal length.");
        int count = 0;
        for (int index = 0; index < left.Length; index++)
            count += left[index] == right[index] ? 0 : 1;
        return count;
    }

    private static int CountDiff(byte[] left, byte[] right, int offset, int length)
    {
        int count = 0;
        for (int index = offset; index < offset + length; index++)
            count += left[index] == right[index] ? 0 : 1;
        return count;
    }

    private static void ReadExactly(Stream stream, byte[] buffer)
    {
        int total = 0;
        while (total < buffer.Length)
        {
            int read = stream.Read(buffer, total, buffer.Length - total);
            if (read == 0)
                throw new EndOfStreamException("The raw disc image ended early.");
            total += read;
        }
    }

    private static bool IsPendingHash(string hash) => hash.StartsWith("PENDING-", StringComparison.Ordinal);

    private static async Task WriteJsonAsync<T>(string path, T value, CancellationToken cancellationToken) =>
        await WriteTextAsync(
            path,
            JsonSerializer.Serialize(value, JsonOptions) + "\n",
            new UTF8Encoding(false),
            cancellationToken);

    private static async Task WriteTextAsync(
        string path,
        string content,
        Encoding encoding,
        CancellationToken cancellationToken)
    {
        await File.WriteAllTextAsync(path, content, encoding, cancellationToken);
        await using FileStream stream = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
        await stream.FlushAsync(cancellationToken);
        stream.Flush(flushToDisk: true);
    }

    private static string ShellSingleQuote(string value) => "'" + value.Replace("'", "'\\''", StringComparison.Ordinal) + "'";

    private static string XmlEscape(string value) => value
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal)
        .Replace("\"", "&quot;", StringComparison.Ordinal)
        .Replace("'", "&apos;", StringComparison.Ordinal);

    private sealed record Prepared(
        DiscLayout Layout,
        UnusedLevel65StandaloneSpawnRuntimeCandidatePlan Plan,
        UnusedLevel65StandaloneBehaviorOwnershipContract Ownership,
        byte[] ExecutableBytes,
        byte[] BeforeLanding,
        byte[] BeforePlayerAnchor,
        string LockedBaseImagePath,
        string FoundationImagePath,
        string FoundationCuePath);

    private sealed record Journal(
        int SchemaVersion,
        string OperationKind,
        string OperationId,
        string OutputDirectoryPath,
        string Phase,
        string StageDirectoryPath,
        string BackupDirectoryPath,
        bool HadPreviousCandidate);

    private enum RecoveryAction
    {
        FailClosed,
        KeepOutputDeleteStage,
        RestoreBackupDeleteStage,
        DeleteOutputRestoreBackup,
        DeleteStage,
        DeleteOutput,
        KeepCommittedOutput
    }

    private sealed record CollisionCell(int BlockWordOffset, int[] TriangleIndexes);
    private sealed record CollisionIndex(IReadOnlyDictionary<UnusedLevel65StandaloneSpawnCollisionCell, CollisionCell> Cells);
    private sealed record CollisionSurfaceHit(int TriangleIndex, int RawZ, long NormalZ);
    private sealed record LogicalDiff(long ChangedBytes, long OutsideAllowedBytes);
    private sealed record PhysicalDiff(
        long ChangedBytes,
        IReadOnlyList<UnusedLevel65StandaloneSpawnRawSectorDiff> RawSectorDiffs);
    private sealed record Readback(
        string OutputImageSha256,
        string OutputId65DataSha256,
        long ChangedLogicalWadBytes,
        long ChangedPhysicalImageBytes,
        IReadOnlyList<UnusedLevel65StandaloneSpawnRawSectorDiff> RawSectorDiffs,
        string RawSectorDiffSha256);
}
