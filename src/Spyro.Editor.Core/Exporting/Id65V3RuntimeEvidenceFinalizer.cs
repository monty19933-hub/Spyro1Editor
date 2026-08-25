using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Spyro.Editor.Core.Exporting;

internal sealed record Id65V3RuntimeEvidenceReviewReference(
    string ArtifactId,
    string RepoRelativePath,
    string Sha256,
    long? StartMilliseconds,
    long? EndMilliseconds);

internal sealed record Id65V3RuntimeEvidenceReviewCheck(
    string CheckId,
    string SessionId,
    string ObservedAtUtc,
    string EvidenceBindingSha256,
    Id65V3RuntimeEvidenceReviewReference[] References);

internal sealed record Id65V3RuntimeEvidenceFinalizationPreview(
    string RunId,
    string RunOriginSha256,
    string FinalTerminalEventSha256,
    string CaptureClosureSha256,
    int ArtifactCount,
    int SessionCount,
    int CheckCount,
    byte[] CanonicalAttestationTemplateUtf8,
    Id65V3RuntimeEvidenceReviewCheck[] Checks);

internal sealed record Id65V3RuntimeEvidenceFinalizationResult(
    string EvidenceRelativePath,
    string EvidenceSha256,
    string RunId,
    string RunOriginSha256,
    string FinalTerminalEventSha256,
    string CaptureClosureSha256,
    string SourceClosureSha256,
    string RuntimeProfileSha256,
    string ArtifactSetSha256,
    string PayloadSha256,
    string ContractSha256,
    int ArtifactCount,
    int SessionCount,
    int CheckCount,
    bool ExistingIdentical,
    string PublisherPinBoundary);

internal sealed record Id65V3RuntimeEvidenceFinalizerSmokeHooks(
    Action? BeforeFinalInputReadback,
    Action<string>? BeforeAtomicRename,
    Action? AfterAtomicRenameBeforeDirectoryFsync,
    Action<string>? AfterAtomicRename);

internal sealed record Id65V3RuntimeHumanSafetyAttestation(
    bool SaveStateLoaded,
    bool SaveStateCreated,
    bool InGameSaveUsed,
    int ActiveCheatCount,
    string Statement);

internal sealed record Id65V3RuntimeHumanSessionAttestation(
    string SessionId,
    bool NewGame,
    bool CardsNone,
    bool CheatsOff,
    bool SaveStateLoaded,
    bool SaveStateCreated,
    bool InGameSaveUsed,
    string Statement);

internal sealed record Id65V3RuntimeHumanCheckAttestation(
    string CheckId,
    string SessionId,
    string Status,
    string EvidenceBindingSha256,
    string Statement);

internal sealed record Id65V3RuntimeHumanAttestation(
    string Format,
    int FormatVersion,
    string RunId,
    string RunOriginSha256,
    string FinalTerminalEventSha256,
    string CaptureClosureSha256,
    string Operator,
    string AttestedAtUtc,
    Id65V3RuntimeHumanSafetyAttestation RuntimeSafety,
    Id65V3RuntimeHumanSessionAttestation[] Sessions,
    Id65V3RuntimeHumanCheckAttestation[] Checks);

internal static partial class Id65V3RuntimeEvidenceFinalizer
{
    internal const string HumanAttestationFormat =
        "spyro-editor-id65-v3-runtime-human-attestation";
    internal const int HumanAttestationFormatVersion = 1;
    internal const string CaptureStagingPrefix =
        "docs/runtime-evidence/assets/unused-level-65-remote-blank-environment-header-repair-v3/" +
        "capture-staging";

    private const string RunOriginFormat =
        "spyro-editor-id65-v3-runtime-capture-run-origin";
    private const int RunOriginFormatVersion = 2;
    private const string JournalFormat =
        "spyro-editor-id65-v3-runtime-capture-event-journal";
    private const int JournalFormatVersion = 3;
    private const string ProfileSnapshotFormat =
        "spyro-editor-id65-v3-runtime-profile-snapshot";
    private const int ProfileSnapshotFormatVersion = 2;
    private const long MaximumIJsonInteger = 9_007_199_254_740_991;
    private const int MaximumCanonicalTextBytes = 64 * 1024 * 1024;
    private const int MaximumAttestationBytes = 1024 * 1024;
    private const long MaximumMediaBytes = 64L * 1024 * 1024 * 1024;
    private const string InspectionAttestation =
        "Machine-derived inspection graph only; no human runtime acceptance is asserted.";

    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly JsonSerializerOptions CamelJson = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private static readonly string[] CapturePinKeys =
    [
        "watcherRelativePath", "watcherSha256",
        "profileBuilderRelativePath", "profileBuilderSha256",
        "profileVerifierRelativePath", "profileVerifierSha256",
        "automationRelativePath", "automationSha256",
        "captureCoordinatorRelativePath", "captureCoordinatorSha256",
        "sourceClosureSha256", "runtimeProfileSha256",
        "profilePackageRelativePath", "profileId",
        "handoffReceiptSha256", "appContentManifestSha256",
        "appOutputTreeManifestSha256", "homeOutputTreeManifestSha256",
        "packageEnvelopeSha256", "externalHomeSettingsSha256",
        "externalHomeBiosSha256", "hostNormalSettingsSentinelSha256"
    ];

    private static readonly string[] ProfileSnapshotKeys =
    [
        "format", "formatVersion", "snapshotId", "phase", "sessionId",
        "processInstanceId", "profileId", "capturedAtUtc",
        "duckStationExecutableSha256", "handoffReceiptSha256",
        "appContentManifestSha256", "appOutputTreeManifestSha256",
        "packageEnvelopeSha256", "externalHomeSettingsSha256",
        "externalHomeBiosSha256", "hostNormalSettingsSentinelId",
        "hostNormalSettingsSentinelSha256", "dataFiles"
    ];

    private static readonly string[] PrelaunchProofKeys =
    [
        "format", "formatVersion", "status", "profileId", "receiptSha256",
        "appContentManifestSha256", "appOutputTreeManifestSha256",
        "homeOutputTreeManifestSha256", "packageEnvelopeSha256", "settingsSha256",
        "biosSha256", "candidateCueSha256", "candidateBinSha256",
        "hostNormalSettingsSentinelSha256", "launchGate", "snapshot"
    ];

    private static readonly string[] OrderedSessionIds =
    [
        "candidate-primary-edge-0",
        "candidate-edge-1",
        "candidate-edge-2",
        "candidate-cold-reset",
        "retail-town-square",
        "retail-gnastys-loot",
        "retail-sunny-flight"
    ];

    private static readonly string[] OrderedCheckIds =
    [
        "candidate.completed-landing-hud4",
        "candidate.hands-off-20-seconds",
        "candidate.lone-pad-visibility",
        "candidate.camera-360-twice",
        "candidate.walk-center-perimeter-both-directions",
        "candidate.jump-land-center-each-corner",
        "candidate.primary-edge-4-to-3-same-pad-yaw",
        "candidate.edge-1-same-pad-yaw",
        "candidate.edge-2-same-pad-yaw",
        "candidate.cold-reset-same-pad-yaw",
        "retail.town-square.terrain",
        "retail.town-square.collision",
        "retail.town-square.mobys",
        "retail.town-square.camera",
        "retail.town-square.death-respawn",
        "retail.town-square.music",
        "retail.town-square.exit",
        "retail.town-square.totals",
        "retail.gnastys-loot.gameplay",
        "retail.gnastys-loot.exit",
        "retail.sunny-flight.gameplay",
        "retail.sunny-flight.timer",
        "retail.sunny-flight.exit"
    ];

    private static readonly IReadOnlyDictionary<string, SessionSpec> SessionSpecs =
        new Dictionary<string, SessionSpec>(StringComparer.Ordinal)
        {
            ["candidate-primary-edge-0"] = new(
                65,
                "Select; then R1, R2, L1, L2, R1, L1, R2, L2; then Left, then Down"),
            ["candidate-edge-1"] = new(
                65,
                "Select; then R1, R2, L1, L2, R1, L1, R2, L2; then Left, then Down"),
            ["candidate-edge-2"] = new(
                65,
                "Select; then R1, R2, L1, L2, R1, L1, R2, L2; then Left, then Down"),
            ["candidate-cold-reset"] = new(
                65,
                "Select; then R1, R2, L1, L2, R1, L1, R2, L2; then Left, then Down"),
            ["retail-town-square"] = new(
                13,
                "Select; then R1, R2, L1, L2, R1, L1, R2, L2; then Cross, then Triangle"),
            ["retail-gnastys-loot"] = new(
                64,
                "Select; then R1, R2, L1, L2, R1, L1, R2, L2; then Left, then Right"),
            ["retail-sunny-flight"] = new(
                15,
                "Select; then R1, R2, L1, L2, R1, L1, R2, L2; then Cross, then Down")
        };

    [GeneratedRegex("^id65v3-[0-9]{8}t[0-9]{6}z-[0-9a-f]{16}$", RegexOptions.CultureInvariant)]
    private static partial Regex RunIdRegex();

    [GeneratedRegex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Regex();

    [GeneratedRegex(
        "^\\d{4}-\\d{2}-\\d{2}T\\d{2}:\\d{2}:\\d{2}\\.\\d{3}Z$",
        RegexOptions.CultureInvariant)]
    private static partial Regex CanonicalUtcRegex();

    private sealed record SessionSpec(int LevelId, string LoadCode);

    private sealed record MachineArtifact(
        Id65V3RuntimeEvidenceArtifact Artifact,
        string? CaptureRole,
        byte[]? Bytes,
        StableFileRead StableRead);

    private sealed record MachineEvent(
        int Sequence,
        string EventType,
        string AtUtc,
        long MonotonicMicroseconds,
        string EventSha256,
        JsonElement Payload,
        JsonElement CanonicalElement);

    private sealed record MachineJournal(
        int Ordinal,
        string SessionId,
        string RunId,
        string BootIdentity,
        string ProcessInstanceId,
        string RunOriginSha256,
        string TerminalEventSha256,
        string JournalSha256,
        string LiveSha256,
        Id65V3RuntimeEvidenceArtifact JournalArtifact,
        MachineEvent[] Events,
        MachineArtifact[] RawArtifacts,
        StableFileRead JournalRead,
        StableFileRead LiveRead,
        byte[] JournalBytes,
        byte[] LiveBytes);

    private sealed record MachineCheck(
        string CheckId,
        string SessionId,
        string ObservedAtUtc,
        Id65V3RuntimeEvidenceSlice[] References,
        bool TelemetryCorroborated,
        string EvidenceBindingSha256);

    private sealed record MachineRun(
        string RepositoryRoot,
        string RunId,
        string RunOriginSha256,
        long RunOriginByteLength,
        string FinalTerminalEventSha256,
        string CaptureClosureSha256,
        string FinalTerminalAtUtc,
        Id65V3RuntimeEvidenceProfile RuntimeProfile,
        Id65V3RuntimeEvidenceArtifact[] Artifacts,
        Id65V3RuntimeEvidenceSession[] Sessions,
        MachineCheck[] Checks,
        MachineJournal[] Journals,
        string StableInputIdentity);

    internal static Id65V3RuntimeEvidenceFinalizationPreview Inspect(
        string repositoryRoot,
        string runId,
        Id65V3RuntimeEvidenceReadbackContext? readbackContext = null)
    {
        string root = RequireRepositoryRootForFinalizer(repositoryRoot);
        RequireRunId(runId);
        string staging = Path.Combine(root, CaptureStagingPrefix);
        using CoordinatorLease lease = new(staging);
        MachineRun machine = LoadMachineRun(root, runId, readbackContext);
        lease.RequireUnchanged();

        Id65V3RuntimeHumanAttestation inspectionAttestation = BuildInspectionAttestation(machine);
        string inspectionAttestationSha256 = HashBytes(
            SerializeCanonicalAttestation(inspectionAttestation));
        Id65V3RuntimeEvidencePackage inspectionPackage = AssemblePackage(
            machine,
            inspectionAttestation,
            inspectionAttestationSha256,
            passOnly: false);
        RequireFullReadback(
            inspectionPackage,
            root,
            readbackContext,
            Id65V3RuntimeEvidenceState.PendingIncomplete,
            "inspection");

        Id65V3RuntimeHumanAttestation template = BuildTemplate(machine);
        byte[] templateBytes = SerializeCanonicalAttestation(template);
        return new(
            runId,
            machine.RunOriginSha256,
            machine.FinalTerminalEventSha256,
            machine.CaptureClosureSha256,
            machine.Artifacts.Length,
            machine.Sessions.Length,
            machine.Checks.Length,
            templateBytes,
            machine.Checks.Select(check => new Id65V3RuntimeEvidenceReviewCheck(
                check.CheckId,
                check.SessionId,
                check.ObservedAtUtc,
                check.EvidenceBindingSha256,
                check.References.Select(reference =>
                {
                    Id65V3RuntimeEvidenceArtifact artifact = machine.Artifacts.Single(item =>
                        item.ArtifactId == reference.ArtifactId);
                    return new Id65V3RuntimeEvidenceReviewReference(
                        artifact.ArtifactId,
                        artifact.RepoRelativePath,
                        artifact.Sha256,
                        reference.StartMilliseconds,
                        reference.EndMilliseconds);
                }).ToArray())).ToArray());
    }

    internal static Id65V3RuntimeEvidenceFinalizationResult FinalizeFocusedPass(
        string repositoryRoot,
        string runId,
        string attestationPath,
        string expectedAttestationSha256,
        Id65V3RuntimeEvidenceReadbackContext? readbackContext = null,
        Id65V3RuntimeEvidenceFinalizerSmokeHooks? smokeHooks = null)
    {
        if (smokeHooks is not null &&
            Assembly.GetEntryAssembly()?.GetName().Name !=
                Id65V3RuntimeEvidenceContract.RequiredSmokeAssemblyName)
        {
            throw new InvalidOperationException(
                "Finalizer fault-injection hooks are restricted to the exact evidence-contract smoke assembly.");
        }
        string root = RequireRepositoryRootForFinalizer(repositoryRoot);
        RequireRunId(runId);
        if (string.IsNullOrWhiteSpace(attestationPath) || !Path.IsPathFullyQualified(attestationPath))
            throw new InvalidDataException("The external human-attestation path must be absolute.");
        if (!Sha256Regex().IsMatch(expectedAttestationSha256))
            throw new InvalidDataException("The expected human-attestation SHA-256 must be lowercase hex64.");

        string staging = Path.Combine(root, CaptureStagingPrefix);
        using CoordinatorLease lease = new(staging);
        MachineRun firstMachine = LoadMachineRun(root, runId, readbackContext);
        (Id65V3RuntimeHumanAttestation attestation, StableFileRead attestationRead) =
            ReadAndValidateAttestation(
                attestationPath,
                expectedAttestationSha256,
                firstMachine,
                requirePass: true);
        Id65V3RuntimeEvidencePackage package = AssemblePackage(
            firstMachine,
            attestation,
            attestationRead.Sha256,
            passOnly: true);
        byte[] canonical = Id65V3RuntimeEvidenceContract.SerializeCanonicalUtf8(package);
        RequireFullReadback(
            package,
            root,
            readbackContext,
            Id65V3RuntimeEvidenceState.FocusedRuntimePass,
            "prewrite finalization");
        lease.RequireUnchanged();

        string destination = Path.Combine(
            root,
            Id65V3RuntimeEvidenceContract.FocusedRuntimeEvidenceRelativePath);
        if (File.Exists(destination) || Directory.Exists(destination))
        {
            if (Directory.Exists(destination))
                throw new InvalidDataException("The fixed canonical evidence destination is a directory.");
            StableFileRead existing = ReadStableOwnedFile(
                destination,
                0x180,
                MaximumCanonicalTextBytes,
                keepBytes: true);
            if (!(existing.Bytes ?? []).AsSpan().SequenceEqual(canonical))
            {
                throw new InvalidDataException(
                    "The fixed canonical evidence destination already contains different bytes; overwrite is forbidden.");
            }
            Id65V3RuntimeEvidenceValidation existingValidation =
                Id65V3RuntimeEvidenceContract.ValidateCanonicalJson(
                    existing.Bytes ?? [],
                    root,
                    Id65V3RuntimeEvidenceValidationMode.FullReadback,
                    readbackContext);
            RequireExactValidation(
                existingValidation,
                Id65V3RuntimeEvidenceState.FocusedRuntimePass,
                "existing identical evidence");
            return BuildResult(existingValidation.Package!, existing.Sha256, firstMachine, existingIdentical: true);
        }

        MachineRun secondMachine = LoadMachineRun(root, runId, readbackContext);
        (Id65V3RuntimeHumanAttestation secondAttestation, StableFileRead secondAttestationRead) =
            ReadAndValidateAttestation(
                attestationPath,
                expectedAttestationSha256,
                secondMachine,
                requirePass: true);
        Id65V3RuntimeEvidencePackage secondPackage = AssemblePackage(
            secondMachine,
            secondAttestation,
            secondAttestationRead.Sha256,
            passOnly: true);
        byte[] secondCanonical = Id65V3RuntimeEvidenceContract.SerializeCanonicalUtf8(secondPackage);
        if (firstMachine.StableInputIdentity != secondMachine.StableInputIdentity ||
            StableReadManifest(attestationRead) != StableReadManifest(secondAttestationRead) ||
            !canonical.AsSpan().SequenceEqual(secondCanonical))
        {
            throw new InvalidDataException(
                "Capture staging or human attestation changed during finalization prewrite readback.");
        }
        RequireFullReadback(
            secondPackage,
            root,
            readbackContext,
            Id65V3RuntimeEvidenceState.FocusedRuntimePass,
            "immediate prepublication");
        lease.RequireUnchanged();

        Id65V3RuntimeEvidencePackage finalPackage = secondPackage;
        Id65V3RuntimeEvidenceValidation? postwrite = null;
        StableFileRead published = AtomicPublishNoReplace(
            destination,
            secondCanonical,
            beforeRename: tempPath =>
            {
                smokeHooks?.BeforeFinalInputReadback?.Invoke();
                smokeHooks?.BeforeAtomicRename?.Invoke(tempPath);
                lease.RequireUnchanged();
                MachineRun finalMachine = LoadMachineRun(root, runId, readbackContext);
                (Id65V3RuntimeHumanAttestation finalAttestationValue,
                    StableFileRead finalAttestation) = ReadAndValidateAttestation(
                    attestationPath,
                    expectedAttestationSha256,
                    finalMachine,
                    requirePass: true);
                Id65V3RuntimeEvidencePackage currentPackage = AssemblePackage(
                    finalMachine,
                    finalAttestationValue,
                    finalAttestation.Sha256,
                    passOnly: true);
                byte[] currentCanonical =
                    Id65V3RuntimeEvidenceContract.SerializeCanonicalUtf8(currentPackage);
                if (finalMachine.StableInputIdentity != secondMachine.StableInputIdentity ||
                    StableReadManifest(finalAttestation) !=
                        StableReadManifest(secondAttestationRead) ||
                    !currentCanonical.AsSpan().SequenceEqual(secondCanonical))
                {
                    throw new InvalidDataException(
                        "Capture staging or human attestation changed immediately before atomic publication.");
                }
                RequireFullReadback(
                    currentPackage,
                    root,
                    readbackContext,
                    Id65V3RuntimeEvidenceState.FocusedRuntimePass,
                    "final hook-free pre-rename readback");
                lease.RequireUnchanged();
                finalPackage = currentPackage;
            },
            afterRenameBeforeDirectoryFsync:
                smokeHooks?.AfterAtomicRenameBeforeDirectoryFsync,
            afterRename: path =>
            {
                smokeHooks?.AfterAtomicRename?.Invoke(path);
                postwrite = Id65V3RuntimeEvidenceContract.ValidateCanonicalJson(
                    Id65V3RuntimeEvidenceContract.SerializeCanonicalUtf8(finalPackage),
                    root,
                    Id65V3RuntimeEvidenceValidationMode.FullReadback,
                    readbackContext);
                RequireExactValidation(
                    postwrite,
                    Id65V3RuntimeEvidenceState.FocusedRuntimePass,
                    "postwrite evidence");
                lease.RequireUnchanged();
            });

        if (postwrite?.Package is null)
            throw new InvalidDataException("Postwrite FullReadback did not return the finalized package.");
        lease.RequireUnchanged();
        return BuildResult(postwrite.Package, published.Sha256, secondMachine, existingIdentical: false);
    }

    internal static Id65V3RuntimeEvidenceFinalizationResult VerifyExisting(
        string repositoryRoot,
        Id65V3RuntimeEvidenceReadbackContext? readbackContext = null)
    {
        string root = RequireRepositoryRootForFinalizer(repositoryRoot);
        string staging = Path.Combine(root, CaptureStagingPrefix);
        using CoordinatorLease lease = new(staging);
        string destination = Path.Combine(
            root,
            Id65V3RuntimeEvidenceContract.FocusedRuntimeEvidenceRelativePath);
        StableFileRead evidence = ReadStableOwnedFile(
            destination,
            0x180,
            MaximumCanonicalTextBytes,
            keepBytes: true);
        Id65V3RuntimeEvidenceValidation validation =
            Id65V3RuntimeEvidenceContract.ValidateCanonicalJson(
                evidence.Bytes ?? [],
                root,
                Id65V3RuntimeEvidenceValidationMode.FullReadback,
                readbackContext);
        RequireExactValidation(
            validation,
            Id65V3RuntimeEvidenceState.FocusedRuntimePass,
            "existing canonical evidence");
        string runId = validation.Package!.Sessions[0].CaptureJournal!.RunId;
        MachineRun machine = LoadMachineRun(root, runId, readbackContext);
        lease.RequireUnchanged();
        if (validation.Package.Sessions[^1].CaptureJournal!.TerminalEventSha256 !=
            machine.FinalTerminalEventSha256)
        {
            throw new InvalidDataException("Existing evidence terminal identity differs from staging.");
        }
        return BuildResult(validation.Package, evidence.Sha256, machine, existingIdentical: true);
    }

    internal static byte[] SerializeCanonicalAttestation(Id65V3RuntimeHumanAttestation attestation) =>
        SerializeCanonicalJsonValue(attestation);

    internal static string RecomputeEmbeddedAttestationSha256(
        Id65V3RuntimeEvidenceOperatorReview review) =>
        HashBytes(SerializeCanonicalAttestation(new(
            HumanAttestationFormat,
            HumanAttestationFormatVersion,
            review.RunId,
            review.RunOriginSha256,
            review.FinalTerminalEventSha256,
            review.CaptureClosureSha256,
            review.Operator,
            review.AttestedAtUtc,
            new(
                review.RuntimeSafety.SaveStateLoaded,
                review.RuntimeSafety.SaveStateCreated,
                review.RuntimeSafety.InGameSaveUsed,
                review.RuntimeSafety.ActiveCheatCount,
                review.RuntimeSafety.Statement),
            review.Sessions.Select(item => new Id65V3RuntimeHumanSessionAttestation(
                item.SessionId,
                item.NewGame,
                item.CardsNone,
                item.CheatsOff,
                item.SaveStateLoaded,
                item.SaveStateCreated,
                item.InGameSaveUsed,
                item.Statement)).ToArray(),
            review.Checks.Select(item => new Id65V3RuntimeHumanCheckAttestation(
                item.CheckId,
                item.SessionId,
                item.Status switch
                {
                    Id65V3RuntimeEvidenceCheckStatus.Passed => "passed",
                    Id65V3RuntimeEvidenceCheckStatus.Failed => "failed",
                    Id65V3RuntimeEvidenceCheckStatus.Inconclusive => "inconclusive",
                    _ => throw new InvalidDataException(
                        $"Operator check review {item.CheckId} has no external-attestation status.")
                },
                item.EvidenceBindingSha256,
                item.Statement)).ToArray())));

    private static MachineRun LoadMachineRun(
        string root,
        string runId,
        Id65V3RuntimeEvidenceReadbackContext? readbackContext)
    {
        Id65V3RuntimeEvidencePackage pending = Id65V3RuntimeEvidenceContract.CreatePending();
        IReadOnlyDictionary<string, string> expectedPins = BuildExpectedCapturePins(pending);
        string staging = Path.Combine(root, CaptureStagingPrefix);
        RequireOwnedPrivateDirectory(staging, 0x1C0, "capture-staging root");
        ValidateStagingRootEntries(staging);
        string runPath = Path.Combine(staging, runId);
        RequireOwnedPrivateDirectory(runPath, 0x1C0, "capture run directory");
        string[] expectedRunEntries =
        [
            "run-origin.json",
            .. OrderedSessionIds.Select((sessionId, ordinal) =>
                ordinal.ToString("D2", CultureInfo.InvariantCulture) + "-" + sessionId)
        ];
        RequireExactDirectoryEntries(runPath, expectedRunEntries, "capture run");

        StableFileRead originRead = ReadStableOwnedFile(
            Path.Combine(runPath, "run-origin.json"),
            0x180,
            MaximumCanonicalTextBytes,
            keepBytes: true);
        JsonElement origin = ParseCanonicalJson(originRead.Bytes ?? [], "run origin");
        ValidateRunOrigin(
            origin,
            root,
            runId,
            expectedPins,
            pending.SourceIdentity,
            pending.RuntimeProfile);

        List<MachineJournal> journals = [];
        List<Id65V3RuntimeEvidenceArtifact> artifacts = [];
        Dictionary<string, MachineArtifact> rawById = new(StringComparer.Ordinal);
        string previousTerminal = new('0', 64);
        for (int ordinal = 0; ordinal < OrderedSessionIds.Length; ordinal++)
        {
            string sessionId = OrderedSessionIds[ordinal];
            string sessionLeaf = ordinal.ToString("D2", CultureInfo.InvariantCulture) + "-" + sessionId;
            string sessionPath = Path.Combine(runPath, sessionLeaf);
            RequireOwnedPrivateDirectory(sessionPath, 0x1C0, $"capture session {sessionId}");
            Dictionary<string, (string Kind, string Leaf, string? Role)> expectedRaw =
                BuildExpectedRawTable(sessionId);
            string[] expectedLeaves =
            [
                "capture-event-journal.json",
                "capture-events-live.ndjson",
                .. expectedRaw.Values.Select(value => value.Leaf)
            ];
            RequireExactDirectoryEntries(sessionPath, expectedLeaves, $"capture session {sessionId}");

            StableFileRead journalRead = ReadStableOwnedFile(
                Path.Combine(sessionPath, "capture-event-journal.json"),
                0x180,
                MaximumCanonicalTextBytes,
                keepBytes: true);
            JsonElement journalRoot = ParseCanonicalJson(
                journalRead.Bytes ?? [],
                $"capture journal {sessionId}");
            MachineEvent[] events = ParseJournalHeaderAndEvents(
                journalRoot,
                runId,
                originRead.Sha256,
                ordinal,
                sessionId,
                previousTerminal,
                expectedPins,
                root,
                readbackContext,
                pending.SourceIdentity,
                pending.RuntimeProfile);
            byte[] expectedLive = BuildLiveProjection(events);
            StableFileRead liveRead = ReadStableOwnedFile(
                Path.Combine(sessionPath, "capture-events-live.ndjson"),
                0x180,
                MaximumCanonicalTextBytes,
                keepBytes: true);
            if (!(liveRead.Bytes ?? []).AsSpan().SequenceEqual(expectedLive))
                throw new InvalidDataException(
                    $"Live NDJSON for {sessionId} differs from the exact LF journal projection.");

            MachineArtifact[] raw = ExtractAndReadRawArtifacts(
                root,
                runId,
                ordinal,
                sessionId,
                events,
                expectedRaw);
            foreach (MachineArtifact item in raw)
            {
                if (!rawById.TryAdd(item.Artifact.ArtifactId, item))
                    throw new InvalidDataException("The capture run repeats a raw artifact ID.");
                artifacts.Add(item.Artifact);
            }
            string terminal = events[^1].EventSha256;
            Id65V3RuntimeEvidenceArtifact journalArtifact = new(
                sessionId + ".capture-journal",
                sessionId,
                "capture-event-journal",
                CaptureStagingPrefix + "/" + runId + "/" + sessionLeaf +
                    "/capture-event-journal.json",
                journalRead.Sha256,
                journalRead.ByteLength,
                events[^1].AtUtc,
                null,
                null);
            artifacts.Add(journalArtifact);
            journals.Add(new(
                ordinal,
                sessionId,
                runId,
                GetString(journalRoot, "bootIdentity", "capture journal"),
                GetString(journalRoot, "processInstanceId", "capture journal"),
                GetString(journalRoot, "runOriginSha256", "capture journal"),
                terminal,
                journalRead.Sha256,
                liveRead.Sha256,
                journalArtifact,
                events,
                raw,
                journalRead,
                liveRead,
                journalRead.Bytes ?? [],
                liveRead.Bytes ?? []));
            previousTerminal = terminal;
        }

        if (artifacts.Count != 43)
            throw new InvalidDataException($"The fixed capture catalog contains {artifacts.Count}, expected 43.");
        if (artifacts.Select(item => item.ArtifactId).Distinct(StringComparer.Ordinal).Count() != 43 ||
            artifacts.Select(item => item.Sha256).Distinct(StringComparer.Ordinal).Count() != 43)
        {
            throw new InvalidDataException("The fixed capture catalog repeats an artifact ID or captured byte hash.");
        }

        Id65V3RuntimeEvidenceProfile profile = BuildObservedProfile(pending.RuntimeProfile, journals);
        Id65V3RuntimeEvidenceSession[] sessions = BuildSessions(journals);
        MachineCheck[] checks = BuildChecks(journals, artifacts);
        string closure = BuildCaptureClosure(runId, originRead.Sha256, journals, artifacts);
        checks = checks.Select(check => check with
        {
            EvidenceBindingSha256 = BuildEvidenceBinding(closure, check, artifacts)
        }).ToArray();
        string stableIdentity = BuildStableInputIdentity(
            originRead,
            journals,
            artifacts,
            closure);
        return new(
            root,
            runId,
            originRead.Sha256,
            originRead.ByteLength,
            journals[^1].TerminalEventSha256,
            closure,
            journals[^1].Events[^1].AtUtc,
            profile,
            artifacts.OrderBy(item => item.ArtifactId, StringComparer.Ordinal).ToArray(),
            sessions,
            checks,
            journals.ToArray(),
            stableIdentity);
    }

    private static IReadOnlyDictionary<string, string> BuildExpectedCapturePins(
        Id65V3RuntimeEvidencePackage package)
    {
        Id65V3RuntimeEvidenceSourceIdentity source = package.SourceIdentity;
        Id65V3RuntimeEvidenceProfile profile = package.RuntimeProfile;
        Dictionary<string, string> pins = new(StringComparer.Ordinal)
        {
            ["watcherRelativePath"] = source.WatcherRelativePath,
            ["watcherSha256"] = source.WatcherSha256,
            ["profileBuilderRelativePath"] = source.ProfileBuilderRelativePath,
            ["profileBuilderSha256"] = source.ProfileBuilderSha256,
            ["profileVerifierRelativePath"] = source.ProfileVerifierRelativePath,
            ["profileVerifierSha256"] = source.ProfileVerifierSha256,
            ["automationRelativePath"] = source.AutomationRelativePath,
            ["automationSha256"] = source.AutomationSha256,
            ["captureCoordinatorRelativePath"] = source.CaptureCoordinatorRelativePath,
            ["captureCoordinatorSha256"] = source.CaptureCoordinatorSha256,
            ["sourceClosureSha256"] = source.SourceClosureSha256,
            ["runtimeProfileSha256"] = profile.ProjectionSha256,
            ["profilePackageRelativePath"] = profile.PackageRelativePath,
            ["profileId"] = profile.ProfileId,
            ["handoffReceiptSha256"] = profile.HandoffReceiptSha256,
            ["appContentManifestSha256"] = profile.AppContentManifestSha256,
            ["appOutputTreeManifestSha256"] = profile.AppOutputTreeManifestSha256,
            ["homeOutputTreeManifestSha256"] = profile.HomeOutputTreeManifestSha256,
            ["packageEnvelopeSha256"] = profile.PackageEnvelopeSha256,
            ["externalHomeSettingsSha256"] = profile.ExternalHomeSettingsSha256,
            ["externalHomeBiosSha256"] = profile.ExternalHomeBiosSha256,
            ["hostNormalSettingsSentinelSha256"] = profile.HostNormalSettingsSha256
        };
        if (pins.Count != CapturePinKeys.Length || CapturePinKeys.Any(key => !pins.ContainsKey(key)))
            throw new InvalidDataException("The finalizer's frozen capture-pin closure changed.");
        return pins;
    }

    private static Id65V3RuntimeEvidencePackage AssemblePackage(
        MachineRun machine,
        Id65V3RuntimeHumanAttestation attestation,
        string attestationSha256,
        bool passOnly)
    {
        Dictionary<string, Id65V3RuntimeHumanSessionAttestation> attestedSessions =
            attestation.Sessions.ToDictionary(item => item.SessionId, StringComparer.Ordinal);
        Dictionary<string, Id65V3RuntimeHumanCheckAttestation> attestedChecks =
            attestation.Checks.ToDictionary(item => item.CheckId, StringComparer.Ordinal);

        Id65V3RuntimeEvidenceSession[] sessions = machine.Sessions.Select(session =>
        {
            Id65V3RuntimeHumanSessionAttestation human = attestedSessions[session.SessionId];
            return session with
            {
                NewGame = human.NewGame,
                CardsNone = human.CardsNone,
                CheatsOff = human.CheatsOff,
                SaveStateLoaded = human.SaveStateLoaded,
                SaveStateCreated = human.SaveStateCreated,
                InGameSaveUsed = human.InGameSaveUsed
            };
        }).ToArray();

        Id65V3RuntimeEvidenceCheck[] checks = machine.Checks.Select(machineCheck =>
        {
            Id65V3RuntimeHumanCheckAttestation human = attestedChecks[machineCheck.CheckId];
            Id65V3RuntimeEvidenceCheckStatus status = human.Status switch
            {
                "passed" => Id65V3RuntimeEvidenceCheckStatus.Passed,
                "failed" => Id65V3RuntimeEvidenceCheckStatus.Failed,
                "inconclusive" => Id65V3RuntimeEvidenceCheckStatus.Inconclusive,
                _ => throw new InvalidDataException(
                    $"Attestation check {human.CheckId} has an unsupported status.")
            };
            return new Id65V3RuntimeEvidenceCheck(
                machineCheck.CheckId,
                machineCheck.SessionId,
                status,
                machineCheck.ObservedAtUtc,
                human.Statement,
                machineCheck.References,
                machineCheck.TelemetryCorroborated);
        }).ToArray();

        Id65V3RuntimeEvidenceCheckStatus coldStatus = checks.Single(item =>
            item.CheckId == "candidate.cold-reset-same-pad-yaw").Status;
        Id65V3RuntimeEvidenceSession edge2 = sessions[2];
        Id65V3RuntimeEvidenceSession cold = sessions[3];
        Id65V3RuntimeEvidenceColdReset coldReset = new(
            coldStatus,
            edge2.ProcessExitedAtUtc,
            cold.ProcessStartedAtUtc,
            "candidate-cold-reset",
            "candidate.cold-reset-same-pad-yaw");
        Id65V3RuntimeEvidenceRetailControl[] retail =
        [
            BuildRetailControl("town-square", 13, SessionSpecs["retail-town-square"].LoadCode,
                checks, "retail.town-square."),
            BuildRetailControl("gnastys-loot", 64, SessionSpecs["retail-gnastys-loot"].LoadCode,
                checks, "retail.gnastys-loot."),
            BuildRetailControl("sunny-flight", 15, SessionSpecs["retail-sunny-flight"].LoadCode,
                checks, "retail.sunny-flight.")
        ];
        bool accepted = checks.All(item => item.Status == Id65V3RuntimeEvidenceCheckStatus.Passed);
        if (passOnly && !accepted)
            throw new InvalidDataException("Focused-pass finalization requires 23 explicit passed attestations.");
        Id65V3RuntimeEvidenceState state = accepted
            ? Id65V3RuntimeEvidenceState.FocusedRuntimePass
            : checks.Any(item => item.Status == Id65V3RuntimeEvidenceCheckStatus.Failed)
                ? Id65V3RuntimeEvidenceState.RuntimeRejected
                : Id65V3RuntimeEvidenceState.PendingIncomplete;
        Id65V3RuntimeEvidenceDerivedResult derived = BuildExpectedDerivedResult(state, accepted);
        Id65V3RuntimeEvidenceCaptureProvenance provenance = new(
            machine.RunId,
            CaptureStagingPrefix + "/" + machine.RunId + "/run-origin.json",
            machine.RunOriginSha256,
            machine.RunOriginByteLength,
            machine.FinalTerminalEventSha256,
            machine.Journals.Select(journal => new Id65V3RuntimeEvidenceLiveProjection(
                journal.Ordinal,
                journal.SessionId,
                CaptureStagingPrefix + "/" + machine.RunId + "/" +
                    journal.Ordinal.ToString("D2", CultureInfo.InvariantCulture) + "-" +
                    journal.SessionId + "/capture-events-live.ndjson",
                journal.LiveSha256,
                journal.LiveBytes.LongLength,
                journal.TerminalEventSha256)).ToArray(),
            machine.CaptureClosureSha256);
        Id65V3RuntimeEvidenceOperatorReview operatorReview = new(
            attestationSha256,
            attestation.Operator,
            attestation.AttestedAtUtc,
            attestation.RunId,
            attestation.RunOriginSha256,
            attestation.FinalTerminalEventSha256,
            attestation.CaptureClosureSha256,
            new(
                attestation.RuntimeSafety.SaveStateLoaded,
                attestation.RuntimeSafety.SaveStateCreated,
                attestation.RuntimeSafety.InGameSaveUsed,
                attestation.RuntimeSafety.ActiveCheatCount,
                attestation.RuntimeSafety.Statement),
            attestation.Sessions.Select(item => new Id65V3RuntimeEvidenceOperatorSessionReview(
                item.SessionId,
                item.NewGame,
                item.CardsNone,
                item.CheatsOff,
                item.SaveStateLoaded,
                item.SaveStateCreated,
                item.InGameSaveUsed,
                item.Statement)).ToArray(),
            attestation.Checks.Select(item => new Id65V3RuntimeEvidenceOperatorCheckReview(
                item.CheckId,
                item.SessionId,
                item.Status switch
                {
                    "passed" => Id65V3RuntimeEvidenceCheckStatus.Passed,
                    "failed" => Id65V3RuntimeEvidenceCheckStatus.Failed,
                    "inconclusive" => Id65V3RuntimeEvidenceCheckStatus.Inconclusive,
                    _ => throw new InvalidDataException(
                        $"Attestation check {item.CheckId} has an unsupported status.")
                },
                item.EvidenceBindingSha256,
                item.Statement)).ToArray());
        Id65V3RuntimeEvidencePackage pending = Id65V3RuntimeEvidenceContract.CreatePending();
        Id65V3RuntimeEvidencePackage package = pending with
        {
            EvidenceId = accepted
                ? "id65-v3-focused-runtime-pass-attestation-" + attestationSha256
                : "id65-v3-runtime-inspection-" + machine.RunId,
            RecordedAtUtc = attestation.AttestedAtUtc,
            DeclaredState = state,
            RuntimeProfile = machine.RuntimeProfile,
            ArtifactCatalog = machine.Artifacts,
            Sessions = sessions,
            VisualChecks = checks,
            ColdReset = coldReset,
            RetailControls = retail,
            CaptureProvenance = provenance,
            OperatorReview = operatorReview,
            DerivedResult = derived
        };
        return Id65V3RuntimeEvidenceContract.WithRecomputedIntegrity(package);
    }

    private static Id65V3RuntimeEvidenceRetailControl BuildRetailControl(
        string id,
        int levelId,
        string loadCode,
        Id65V3RuntimeEvidenceCheck[] checks,
        string prefix)
    {
        Id65V3RuntimeEvidenceCheck[] witnesses = checks.Where(item =>
            item.CheckId.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
        Id65V3RuntimeEvidenceCheckStatus status = witnesses.All(item =>
            item.Status == Id65V3RuntimeEvidenceCheckStatus.Passed)
                ? Id65V3RuntimeEvidenceCheckStatus.Passed
                : witnesses.Any(item => item.Status == Id65V3RuntimeEvidenceCheckStatus.Failed)
                    ? Id65V3RuntimeEvidenceCheckStatus.Failed
                    : Id65V3RuntimeEvidenceCheckStatus.Inconclusive;
        return new(id, levelId, loadCode, status, witnesses.Select(item => item.CheckId).ToArray());
    }

    private static Id65V3RuntimeEvidenceDerivedResult BuildExpectedDerivedResult(
        Id65V3RuntimeEvidenceState state,
        bool accepted) => new(
        state,
        SourceValid: true,
        PinnedRuntimeFilesReadbackValid: true,
        RuntimeProfileValid: true,
        ArtifactReadbackValid: true,
        DiscIdentityValid: true,
        TelemetryValid: true,
        VisualChecklistComplete: accepted,
        ColdResetValid: accepted,
        RetailControlsValid: accepted,
        EnvironmentHeaderRepairV3RuntimeAccepted: accepted,
        RuntimeEvidenceAccepted: accepted,
        FarLowDetailAccepted: false,
        SaveRuntimeAccepted: false,
        Core4RewardRuntimeAccepted: false,
        Total200RuntimeAccepted: false,
        PublisherAuthorized: false,
        AppIntegrated: false,
        NormalCreateBinEnabled: false,
        PromotionAuthorized: false,
        ReleaseIntegrated: false,
        ProcessUseCryptographicallyProven: false);

    private static Id65V3RuntimeEvidenceProfile BuildObservedProfile(
        Id65V3RuntimeEvidenceProfile expected,
        IReadOnlyList<MachineJournal> journals)
    {
        List<string> changed = [];
        Id65V3RuntimeProfileSnapshot? previousAfter = null;
        foreach (MachineJournal journal in journals)
        {
            MachineArtifact beforeArtifact = journal.RawArtifacts.Single(item =>
                item.Artifact.Kind == "runtime-profile-before");
            MachineArtifact afterArtifact = journal.RawArtifacts.Single(item =>
                item.Artifact.Kind == "runtime-profile-after");
            Id65V3RuntimeProfileSnapshot before = ReadProfileSnapshot(
                beforeArtifact,
                expected,
                "before",
                journal.SessionId,
                journal.ProcessInstanceId);
            Id65V3RuntimeProfileSnapshot after = ReadProfileSnapshot(
                afterArtifact,
                expected,
                "after",
                journal.SessionId,
                journal.ProcessInstanceId);
            Dictionary<string, Id65V3RuntimeProfileSnapshotEntry> beforeFiles =
                before.DataFiles.ToDictionary(item => item.RelativePath, StringComparer.Ordinal);
            Dictionary<string, Id65V3RuntimeProfileSnapshotEntry> afterFiles =
                after.DataFiles.ToDictionary(item => item.RelativePath, StringComparer.Ordinal);
            if (previousAfter is not null && !SnapshotFilesEqual(previousAfter.DataFiles, before.DataFiles))
            {
                throw new InvalidDataException(
                    $"Session {journal.SessionId} does not begin with the preceding external-HOME snapshot.");
            }
            foreach (string path in beforeFiles.Keys.Union(afterFiles.Keys, StringComparer.Ordinal))
            {
                bool differs =
                    !beforeFiles.TryGetValue(path, out Id65V3RuntimeProfileSnapshotEntry? left) ||
                    !afterFiles.TryGetValue(path, out Id65V3RuntimeProfileSnapshotEntry? right) ||
                    left.ByteLength != right.ByteLength || left.Sha256 != right.Sha256;
                if (!differs)
                    continue;
                if (!IsAllowedVolatileDataPath(path))
                    throw new InvalidDataException($"Nonvolatile external-HOME profile file changed: {path}");
                changed.Add(path);
            }
            previousAfter = after;
        }
        MachineArtifact firstBeforeArtifact = journals[0].RawArtifacts.Single(item =>
            item.Artifact.Kind == "runtime-profile-before");
        MachineArtifact finalAfterArtifact = journals[^1].RawArtifacts.Single(item =>
            item.Artifact.Kind == "runtime-profile-after");
        Id65V3RuntimeProfileSnapshot firstBefore = ReadProfileSnapshot(
            firstBeforeArtifact,
            expected,
            "before",
            journals[0].SessionId,
            journals[0].ProcessInstanceId);
        Id65V3RuntimeProfileSnapshot finalAfter = ReadProfileSnapshot(
            finalAfterArtifact,
            expected,
            "after",
            journals[^1].SessionId,
            journals[^1].ProcessInstanceId);
        return expected with
        {
            SettingsBeforeSha256 = firstBefore.ExternalHomeSettingsSha256,
            SettingsAfterSha256 = finalAfter.ExternalHomeSettingsSha256,
            NormalUserSettingsBeforeSha256 = firstBefore.HostNormalSettingsSentinelSha256,
            NormalUserSettingsAfterSha256 = finalAfter.HostNormalSettingsSentinelSha256,
            CheatFileCountBefore = CountSnapshotPrefix(firstBefore, "cheats/"),
            CheatFileCountAfter = CountSnapshotPrefix(finalAfter, "cheats/"),
            GameSettingsFileCountBefore = CountSnapshotPrefix(firstBefore, "gamesettings/"),
            GameSettingsFileCountAfter = CountSnapshotPrefix(finalAfter, "gamesettings/"),
            MemoryCardFileCountBefore = CountSnapshotPrefix(firstBefore, "memcards/"),
            MemoryCardFileCountAfter = CountSnapshotPrefix(finalAfter, "memcards/"),
            SaveStateFileCountBefore = CountSnapshotPrefix(firstBefore, "savestates/"),
            SaveStateFileCountAfter = CountSnapshotPrefix(finalAfter, "savestates/"),
            PatchFileCountBefore = CountSnapshotPrefix(firstBefore, "patches/"),
            PatchFileCountAfter = CountSnapshotPrefix(finalAfter, "patches/"),
            ChangedProfilePaths = changed.Distinct(StringComparer.Ordinal)
                .Order(PythonUnicodeComparer.Instance).ToArray(),
            SaveStateLoaded = false,
            SaveStateCreated = false,
            InGameSaveUsed = false,
            ActiveCheatCount = 0
        };
    }

    private static int CountSnapshotPrefix(Id65V3RuntimeProfileSnapshot snapshot, string prefix) =>
        snapshot.DataFiles.Count(item => item.RelativePath.StartsWith(prefix, StringComparison.Ordinal));

    private static bool SnapshotFilesEqual(
        IReadOnlyList<Id65V3RuntimeProfileSnapshotEntry> left,
        IReadOnlyList<Id65V3RuntimeProfileSnapshotEntry> right) =>
        left.Count == right.Count && left.Zip(right).All(pair =>
            pair.First.RelativePath == pair.Second.RelativePath &&
            pair.First.ByteLength == pair.Second.ByteLength &&
            pair.First.Sha256 == pair.Second.Sha256);

    private static Id65V3RuntimeProfileSnapshot ReadProfileSnapshot(
        MachineArtifact artifact,
        Id65V3RuntimeEvidenceProfile expected,
        string phase,
        string sessionId,
        string processInstanceId)
    {
        if (artifact.Bytes is null)
            throw new InvalidDataException($"Profile artifact {artifact.Artifact.ArtifactId} has no bytes.");
        JsonElement element = ParseVerifierCanonicalJson(
            artifact.Bytes,
            $"profile artifact {artifact.Artifact.ArtifactId}");
        return ParseAndValidateProfileSnapshot(
            element,
            expected,
            phase,
            sessionId,
            processInstanceId,
            artifact.Artifact.CapturedAtUtc,
            artifact.Artifact.ArtifactId);
    }

    private static Id65V3RuntimeEvidenceSession[] BuildSessions(IReadOnlyList<MachineJournal> journals)
    {
        List<Id65V3RuntimeEvidenceSession> sessions = [];
        foreach (MachineJournal journal in journals)
        {
            MachineEvent process = OneEvent(journal, "process-start-observed");
            MachineEvent watcher = OneEvent(journal, "watcher-connected");
            MachineEvent resumed = OneEvent(journal, "manually-resumed");
            MachineEvent selector = OneMarker(journal, "selector-started", "selector");
            MachineEvent exit = OneEvent(journal, "process-exit-observed");
            Dictionary<string, MachineEvent> bindings = journal.Events
                .Where(item => item.EventType == "watcher-marker-bound")
                .ToDictionary(
                    item => GetString(item.Payload, "markerRole", "watcher binding"),
                    StringComparer.Ordinal);
            (string? At, int? Ordinal) Bound(string role)
            {
                if (!bindings.TryGetValue(role, out MachineEvent? binding))
                    return (null, null);
                return (
                    GetString(binding.Payload, "sampleAtUtc", "watcher binding"),
                    GetInt32(binding.Payload, "sampleOrdinal", "watcher binding"));
            }
            (string? landingAt, int? landingOrdinal) = Bound("landing-marked");
            (string? holdStartAt, int? holdStartOrdinal) = Bound("hold-started");
            (string? holdEndAt, int? holdEndOrdinal) = Bound("hold-ended");
            (string? fallAt, int? fallOrdinal) = Bound("fall-started");
            (string? respawnAt, int? respawnOrdinal) = Bound("respawn-marked");
            if (landingAt is null || landingOrdinal is null)
                throw new InvalidDataException($"Session {journal.SessionId} lacks its landing watcher binding.");
            SessionSpec spec = SessionSpecs[journal.SessionId];
            Id65V3RuntimeEvidenceSession session = new(
                journal.SessionId,
                journal.ProcessInstanceId,
                spec.LevelId,
                spec.LoadCode,
                ColdBoot: true,
                NewGame: true,
                CardsNone: true,
                CheatsOff: true,
                SaveStateLoaded: false,
                SaveStateCreated: false,
                InGameSaveUsed: false,
                GetString(process.Payload, "processStartedAtUtc", "process start"),
                watcher.AtUtc,
                resumed.AtUtc,
                selector.AtUtc,
                landingAt,
                holdStartAt,
                holdEndAt,
                fallAt,
                respawnAt,
                GetString(exit.Payload, "processExitedAtUtc", "process exit"),
                landingOrdinal,
                holdStartOrdinal,
                holdEndOrdinal,
                fallOrdinal,
                respawnOrdinal,
                journal.RawArtifacts.Single(item => item.Artifact.Kind == "watcher-stdout")
                    .Artifact.ArtifactId,
                new(
                    journal.JournalArtifact.ArtifactId,
                    JournalFormat,
                    JournalFormatVersion,
                    journal.RunId,
                    journal.Ordinal,
                    journal.SessionId,
                    journal.BootIdentity,
                    journal.ProcessInstanceId,
                    journal.TerminalEventSha256),
                journal.RawArtifacts.Single(item => item.Artifact.Kind == "runtime-profile-before")
                    .Artifact.ArtifactId,
                journal.RawArtifacts.Single(item => item.Artifact.Kind == "runtime-profile-after")
                    .Artifact.ArtifactId,
                null);
            MachineArtifact watcherArtifact = journal.RawArtifacts.Single(item =>
                item.Artifact.Kind == "watcher-stdout");
            string watcherText = StrictUtf8.GetString(watcherArtifact.Bytes ??
                throw new InvalidDataException("Watcher bytes are unavailable."));
            Id65V3TelemetryParseResult parsed = Id65V3RuntimeEvidenceContract.ParseTelemetryLog(watcherText);
            session = session with
            {
                SampleSummary = Id65V3RuntimeEvidenceContract.RecomputeTelemetrySummary(parsed, session)
            };
            sessions.Add(session);
        }
        return sessions.ToArray();
    }

    private static MachineCheck[] BuildChecks(
        IReadOnlyList<MachineJournal> journals,
        IReadOnlyList<Id65V3RuntimeEvidenceArtifact> artifacts)
    {
        Dictionary<string, MachineJournal> bySession = journals.ToDictionary(
            item => item.SessionId,
            StringComparer.Ordinal);
        List<MachineCheck> result = [];
        foreach (string checkId in OrderedCheckIds)
        {
            string sessionId = SessionForCheck(checkId);
            MachineJournal journal = bySession[sessionId];
            List<Id65V3RuntimeEvidenceSlice> references = [];
            string observedAt;
            if (checkId == "candidate.cold-reset-same-pad-yaw")
            {
                MachineArtifact before = FrameByRole(
                    bySession["candidate-edge-2"],
                    "candidate.edge-2-same-pad-yaw.post-respawn-and-cold-reset-pre-frame");
                MachineArtifact after = FrameByRole(
                    journal,
                    "candidate.cold-reset-same-pad-yaw.post-reset-frame");
                AddFrameReference(references, before);
                AddFrameReference(references, after);
                AddRecordingSlice(
                    references,
                    journal,
                    OneMarker(journal, "selector-started", "selector").AtUtc,
                    OneMarker(journal, "landing-marked", "landing").AtUtc);
                observedAt = after.Artifact.CapturedAtUtc;
            }
            else if (checkId.Contains("same-pad-yaw", StringComparison.Ordinal))
            {
                MachineArtifact before = FrameByRole(journal, checkId + ".pre-fall-frame");
                string afterRole = sessionId == "candidate-edge-2"
                    ? checkId + ".post-respawn-and-cold-reset-pre-frame"
                    : checkId + ".post-respawn-frame";
                MachineArtifact after = FrameByRole(journal, afterRole);
                AddFrameReference(references, before);
                AddFrameReference(references, after);
                AddRecordingSlice(
                    references,
                    journal,
                    OneMarker(journal, "fall-started", "edge-fall").AtUtc,
                    OneMarker(journal, "respawn-marked", "edge-respawn").AtUtc);
                observedAt = after.Artifact.CapturedAtUtc;
            }
            else if (checkId == "candidate.lone-pad-visibility")
            {
                MachineArtifact frame = FrameByRole(journal, checkId);
                AddFrameReference(references, frame);
                observedAt = frame.Artifact.CapturedAtUtc;
            }
            else
            {
                string startType = checkId == "candidate.hands-off-20-seconds"
                    ? "hold-started"
                    : "check-started";
                string endType = checkId == "candidate.hands-off-20-seconds"
                    ? "hold-ended"
                    : "check-ended";
                string marker = checkId == "candidate.hands-off-20-seconds"
                    ? "stable-hold"
                    : checkId;
                MachineEvent start = OneMarker(journal, startType, marker);
                MachineEvent end = OneMarker(journal, endType, marker);
                AddRecordingSlice(references, journal, start.AtUtc, end.AtUtc);
                observedAt = end.AtUtc;
            }
            bool telemetry = checkId is
                "candidate.completed-landing-hud4" or
                "candidate.hands-off-20-seconds" or
                "candidate.primary-edge-4-to-3-same-pad-yaw" or
                "candidate.edge-1-same-pad-yaw" or
                "candidate.edge-2-same-pad-yaw";
            result.Add(new(
                checkId,
                sessionId,
                observedAt,
                references.ToArray(),
                telemetry,
                string.Empty));
        }
        return result.ToArray();
    }

    private static void AddFrameReference(
        List<Id65V3RuntimeEvidenceSlice> references,
        MachineArtifact frame) =>
        references.Add(new(
            frame.Artifact.ArtifactId,
            null,
            null,
            frame.Artifact.Sha256));

    private static void AddRecordingSlice(
        List<Id65V3RuntimeEvidenceSlice> references,
        MachineJournal journal,
        string startAtUtc,
        string endAtUtc)
    {
        MachineArtifact recording = journal.RawArtifacts.Single(item =>
            item.Artifact.Kind == "screen-recording");
        DateTimeOffset mediaStart = ParseUtc(recording.Artifact.MediaStartAtUtc!, "recording media start");
        long start = RequireIntegralMilliseconds(ParseUtc(startAtUtc, "slice start") - mediaStart);
        long end = RequireIntegralMilliseconds(ParseUtc(endAtUtc, "slice end") - mediaStart);
        references.Add(new(recording.Artifact.ArtifactId, start, end, null));
    }

    private static long RequireIntegralMilliseconds(TimeSpan span)
    {
        double value = span.TotalMilliseconds;
        if (value < 0 || value > long.MaxValue || value != Math.Truncate(value))
            throw new InvalidDataException("A derived recording slice is not a nonnegative integral millisecond.");
        return checked((long)value);
    }

    private static MachineArtifact FrameByRole(MachineJournal journal, string role)
    {
        MachineArtifact[] matches = journal.RawArtifacts.Where(item =>
            item.Artifact.Kind == "frame" && item.CaptureRole == role).ToArray();
        return matches.Length == 1
            ? matches[0]
            : throw new InvalidDataException(
                $"Session {journal.SessionId} lacks one exact frame role {role}.");
    }

    private static string BuildEvidenceBinding(
        string captureClosure,
        MachineCheck check,
        IReadOnlyList<Id65V3RuntimeEvidenceArtifact> artifacts) =>
        RecomputeEvidenceBindingSha256(
            captureClosure,
            new Id65V3RuntimeEvidenceCheck(
                check.CheckId,
                check.SessionId,
                Id65V3RuntimeEvidenceCheckStatus.Inconclusive,
                check.ObservedAtUtc,
                InspectionAttestation,
                check.References,
                check.TelemetryCorroborated),
            artifacts);

    internal static string RecomputeEvidenceBindingSha256(
        string captureClosure,
        Id65V3RuntimeEvidenceCheck check,
        IReadOnlyList<Id65V3RuntimeEvidenceArtifact> artifacts)
    {
        object[] refs = check.ArtifactRefs.Select(reference =>
        {
            Id65V3RuntimeEvidenceArtifact artifact = artifacts.Single(item =>
                item.ArtifactId == reference.ArtifactId);
            return new
            {
                reference.ArtifactId,
                artifact.RepoRelativePath,
                artifact.Sha256,
                reference.StartMilliseconds,
                reference.EndMilliseconds,
                reference.FrameSha256
            };
        }).Cast<object>().ToArray();
        byte[] canonical = SerializeCanonicalJsonValue(new
        {
            format = "spyro-editor-id65-v3-runtime-check-evidence-binding",
            formatVersion = 1,
            captureClosureSha256 = captureClosure,
            check.CheckId,
            check.SessionId,
            observedAtUtc = check.ObservedAtUtc ??
                throw new InvalidDataException($"Check {check.CheckId} lacks an observation time."),
            check.TelemetryCorroborated,
            artifactRefs = refs
        });
        return HashBytes(canonical);
    }

    private static string BuildCaptureClosure(
        string runId,
        string originSha256,
        IReadOnlyList<MachineJournal> journals,
        IReadOnlyList<Id65V3RuntimeEvidenceArtifact> artifacts)
    {
        StringBuilder manifest = new();
        manifest.Append("id65-v3-runtime-capture-closure-v2\n")
            .Append("runId=").Append(runId).Append('\n')
            .Append("runOriginSha256=").Append(originSha256).Append('\n');
        foreach (MachineJournal journal in journals)
        {
            manifest.Append("session=")
                .Append(journal.Ordinal.ToString(CultureInfo.InvariantCulture)).Append('|')
                .Append(journal.SessionId).Append('|')
                .Append(journal.JournalSha256).Append('|')
                .Append(journal.LiveSha256).Append('|')
                .Append(journal.TerminalEventSha256).Append('\n');
        }
        foreach (Id65V3RuntimeEvidenceArtifact artifact in artifacts.OrderBy(
                     item => item.ArtifactId,
                     StringComparer.Ordinal))
        {
            manifest.Append("artifact=").Append(artifact.ArtifactId).Append('|')
                .Append(artifact.Sha256).Append('|')
                .Append(artifact.ByteLength.ToString(CultureInfo.InvariantCulture)).Append('\n');
        }
        return HashBytes(StrictUtf8.GetBytes(manifest.ToString()));
    }

    private static string BuildStableInputIdentity(
        StableFileRead origin,
        IReadOnlyList<MachineJournal> journals,
        IReadOnlyList<Id65V3RuntimeEvidenceArtifact> artifacts,
        string closure)
    {
        StringBuilder manifest = new();
        manifest.Append("id65-v3-runtime-finalizer-stable-input-v1\n")
            .Append("origin=").Append(StableReadManifest(origin)).Append('\n')
            .Append("closure=").Append(closure).Append('\n');
        foreach (MachineJournal journal in journals)
        {
            manifest.Append("journal=").Append(journal.Ordinal).Append('|')
                .Append(StableReadManifest(journal.JournalRead)).Append('\n')
                .Append("live=").Append(journal.Ordinal).Append('|')
                .Append(StableReadManifest(journal.LiveRead)).Append('\n');
            foreach (MachineArtifact raw in journal.RawArtifacts.OrderBy(
                         item => item.Artifact.ArtifactId,
                         StringComparer.Ordinal))
            {
                manifest.Append("raw=").Append(raw.Artifact.ArtifactId).Append('|')
                    .Append(StableReadManifest(raw.StableRead)).Append('\n');
            }
        }
        foreach (Id65V3RuntimeEvidenceArtifact artifact in artifacts.OrderBy(
                     item => item.ArtifactId,
                     StringComparer.Ordinal))
        {
            manifest.Append("artifact=").Append(artifact.ArtifactId).Append('|')
                .Append(artifact.Sha256).Append('|').Append(artifact.ByteLength).Append('\n');
        }
        return HashBytes(StrictUtf8.GetBytes(manifest.ToString()));
    }

    private static string StableReadManifest(StableFileRead read) =>
        $"{read.Sha256}|{read.ByteLength}|{read.Device}|{read.Inode}|" +
        $"{read.Mode}|{read.LinkCount}|{read.UserId}|" +
        $"{read.ModifiedSeconds}|{read.ModifiedNanoseconds}|{read.ChangedSeconds}|{read.ChangedNanoseconds}";

    private static MachineArtifact[] ExtractAndReadRawArtifacts(
        string root,
        string runId,
        int ordinal,
        string sessionId,
        MachineEvent[] events,
        IReadOnlyDictionary<string, (string Kind, string Leaf, string? Role)> expected)
    {
        List<(JsonElement Payload, string Kind)> metadata = [];
        foreach (MachineEvent item in events)
        {
            string? kind = item.EventType switch
            {
                "profile-before-captured" => "runtime-profile-before",
                "profile-after-captured" => "runtime-profile-after",
                "watcher-stopped" => "watcher-stdout",
                "media-artifact-bound" => GetString(item.Payload, "kind", "capture media"),
                _ => null
            };
            if (kind is not null)
                metadata.Add((item.Payload, kind));
        }
        if (metadata.Count != expected.Count)
            throw new InvalidDataException($"Session {sessionId} has the wrong sealed raw-artifact count.");
        List<MachineArtifact> result = [];
        foreach ((JsonElement payload, string eventKind) in metadata)
        {
            string artifactId = GetString(payload, "artifactId", "raw artifact");
            if (!expected.TryGetValue(artifactId, out (string Kind, string Leaf, string? Role) spec))
                throw new InvalidDataException($"Session {sessionId} contains unexpected raw artifact {artifactId}.");
            string kind = GetString(payload, "kind", "raw artifact");
            string relativePath = GetString(payload, "repoRelativePath", "raw artifact");
            string expectedRelative = CaptureStagingPrefix + "/" + runId + "/" +
                ordinal.ToString("D2", CultureInfo.InvariantCulture) + "-" + sessionId + "/" + spec.Leaf;
            string? role = kind is "frame" or "screen-recording"
                ? GetString(payload, "captureRole", "raw media")
                : null;
            if (kind != eventKind || kind != spec.Kind || relativePath != expectedRelative ||
                (spec.Role is not null && role != spec.Role))
            {
                throw new InvalidDataException($"Raw artifact {artifactId} differs from its fixed table.");
            }
            bool text = kind is "runtime-profile-before" or "runtime-profile-after" or "watcher-stdout";
            StableFileRead read = ReadStableOwnedFile(
                Path.Combine(root, relativePath),
                0x180,
                text ? MaximumCanonicalTextBytes : MaximumMediaBytes,
                keepBytes: text);
            string claimedHash = GetSha256(payload, "sha256", "raw artifact");
            long claimedLength = GetInt64(payload, "byteLength", "raw artifact");
            if (read.Sha256 != claimedHash || read.ByteLength != claimedLength)
                throw new InvalidDataException($"Raw artifact {artifactId} hash or length changed.");
            string capturedAt = GetCanonicalUtc(payload, "capturedAtUtc", "raw artifact");
            string? mediaStart = kind == "screen-recording"
                ? GetCanonicalUtc(payload, "mediaStartAtUtc", "recording")
                : null;
            string? mediaEnd = kind == "screen-recording"
                ? GetCanonicalUtc(payload, "mediaEndAtUtc", "recording")
                : null;
            result.Add(new(
                new(
                    artifactId,
                    sessionId,
                    kind,
                    relativePath,
                    read.Sha256,
                    read.ByteLength,
                    capturedAt,
                    mediaStart,
                    mediaEnd),
                role,
                read.Bytes,
                read));
        }
        if (!result.Select(item => item.Artifact.ArtifactId).Order(StringComparer.Ordinal)
                .SequenceEqual(expected.Keys.Order(StringComparer.Ordinal)))
        {
            throw new InvalidDataException($"Session {sessionId} raw artifact set is not exact.");
        }
        return result.ToArray();
    }

    private static MachineEvent[] ParseJournalHeaderAndEvents(
        JsonElement root,
        string runId,
        string runOriginSha256,
        int ordinal,
        string sessionId,
        string previousTerminal,
        IReadOnlyDictionary<string, string> expectedPins,
        string repositoryRoot,
        Id65V3RuntimeEvidenceReadbackContext? readbackContext,
        Id65V3RuntimeEvidenceSourceIdentity expectedSource,
        Id65V3RuntimeEvidenceProfile expectedProfile)
    {
        RequireObjectKeys(
            root,
            [
                "bootIdentity", "events", "format", "formatVersion", "processInstanceId",
                "runOriginSha256", "runId", "sessionId", "sessionOrdinal", .. CapturePinKeys
            ],
            "capture-journal root");
        if (GetString(root, "format", "capture journal") != JournalFormat ||
            GetInt32(root, "formatVersion", "capture journal") != JournalFormatVersion ||
            GetString(root, "runId", "capture journal") != runId ||
            GetString(root, "sessionId", "capture journal") != sessionId ||
            GetInt32(root, "sessionOrdinal", "capture journal") != ordinal ||
            GetSha256(root, "runOriginSha256", "capture journal") != runOriginSha256)
        {
            throw new InvalidDataException($"Capture journal {sessionId} header identity changed.");
        }
        foreach (string pin in CapturePinKeys)
        {
            string value = GetString(root, pin, "capture journal");
            if (!expectedPins.TryGetValue(pin, out string? expected) || value != expected)
                throw new InvalidDataException($"Capture journal {sessionId} pin changed: {pin}.");
        }
        string bootIdentity = GetSha256(root, "bootIdentity", "capture journal");
        string processId = GetString(root, "processInstanceId", "capture journal");
        if (!Regex.IsMatch(processId, "^id65v3-process-[0-9a-f]{32}$", RegexOptions.CultureInvariant))
            throw new InvalidDataException("Capture journal process-instance ID is malformed.");

        JsonElement array = GetProperty(root, "events", "capture journal");
        if (array.ValueKind != JsonValueKind.Array || array.GetArrayLength() is < 1 or > 512)
            throw new InvalidDataException("Capture journal event array is empty or oversized.");
        List<MachineEvent> events = [];
        string priorHash = previousTerminal;
        foreach (JsonElement item in array.EnumerateArray())
        {
            RequireObjectKeys(
                item,
                [
                    "atUtc", "eventSha256", "eventType", "monotonicMicroseconds",
                    "payload", "previousEventSha256", "sequence"
                ],
                "capture event");
            int sequence = GetInt32(item, "sequence", "capture event");
            if (sequence != events.Count)
                throw new InvalidDataException("Capture event sequence is not contiguous from zero.");
            JsonElement payload = GetProperty(item, "payload", "capture event");
            if (payload.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("Capture event payload is not an object.");
            string previousHash = GetSha256(item, "previousEventSha256", "capture event");
            if (previousHash != priorHash)
                throw new InvalidDataException("Capture event previous-hash chain changed.");
            string eventHash = GetSha256(item, "eventSha256", "capture event");
            string expectedEventHash = RecomputeCaptureEventSha256(root, item, payload);
            if (eventHash != expectedEventHash)
                throw new InvalidDataException("Capture event hash differs from the exact v2 manifest.");
            string eventType = GetString(item, "eventType", "capture event");
            string atUtc = GetCanonicalUtc(item, "atUtc", "capture event");
            if (eventType == "process-start-observed")
            {
                ValidateExternalHomeProcessPayload(
                    payload,
                    atUtc,
                    runId,
                    bootIdentity,
                    processId,
                    repositoryRoot,
                    readbackContext,
                    expectedSource,
                    expectedProfile);
            }
            events.Add(new(
                sequence,
                eventType,
                atUtc,
                GetInt64(item, "monotonicMicroseconds", "capture event"),
                eventHash,
                payload.Clone(),
                item.Clone()));
            priorHash = eventHash;
        }
        if (GetSha256(events[0].Payload, "previousSessionTerminalSha256", "session-open") !=
                previousTerminal ||
            GetSha256(events[0].CanonicalElement, "previousEventSha256", "first capture event") !=
                previousTerminal ||
            events[^1].EventType != "session-sealed")
        {
            throw new InvalidDataException($"Capture journal {sessionId} chain boundary changed.");
        }
        return events.ToArray();
    }

    private static string RecomputeCaptureEventSha256(
        JsonElement header,
        JsonElement captureEvent,
        JsonElement payload)
    {
        string payloadSha256 = HashBytes(SerializeCanonicalElement(payload));
        StringBuilder manifest = new("id65-v3-runtime-capture-event-v2\n");
        manifest.Append("format=").Append(GetString(header, "format", "capture journal")).Append('\n')
            .Append("formatVersion=").Append(GetInt32(header, "formatVersion", "capture journal")).Append('\n')
            .Append("runId=").Append(GetString(header, "runId", "capture journal")).Append('\n')
            .Append("sessionId=").Append(GetString(header, "sessionId", "capture journal")).Append('\n')
            .Append("sessionOrdinal=").Append(GetInt32(header, "sessionOrdinal", "capture journal")).Append('\n')
            .Append("processInstanceId=").Append(GetString(header, "processInstanceId", "capture journal")).Append('\n')
            .Append("runOriginSha256=").Append(GetSha256(header, "runOriginSha256", "capture journal")).Append('\n')
            .Append("bootIdentity=").Append(GetSha256(header, "bootIdentity", "capture journal")).Append('\n');
        foreach (string pin in CapturePinKeys)
            manifest.Append(pin).Append('=').Append(GetString(header, pin, "capture journal")).Append('\n');
        manifest.Append("sequence=").Append(GetInt32(captureEvent, "sequence", "capture event")).Append('\n')
            .Append("eventType=").Append(GetString(captureEvent, "eventType", "capture event")).Append('\n')
            .Append("atUtc=").Append(GetCanonicalUtc(captureEvent, "atUtc", "capture event")).Append('\n')
            .Append("monotonicMicroseconds=").Append(GetInt64(captureEvent, "monotonicMicroseconds", "capture event")).Append('\n')
            .Append("previousEventSha256=").Append(GetSha256(captureEvent, "previousEventSha256", "capture event")).Append('\n')
            .Append("payloadSha256=").Append(payloadSha256).Append('\n');
        return HashBytes(StrictUtf8.GetBytes(manifest.ToString()));
    }

    private static void ValidateExternalHomeProcessPayload(
        JsonElement payload,
        string eventAtUtc,
        string runId,
        string bootIdentity,
        string processInstanceId,
        string repositoryRoot,
        Id65V3RuntimeEvidenceReadbackContext? readbackContext,
        Id65V3RuntimeEvidenceSourceIdentity expectedSource,
        Id65V3RuntimeEvidenceProfile expectedProfile)
    {
        RequireObjectKeys(
            payload,
            [
                "argv", "argvNulSha256", "environmentNulSha256", "executableByteLength",
                "executableDevice", "executableInode", "executableRealPath",
                "executableSha256", "externalHomeRealPath", "observedAtUtc", "parentPid",
                "pid", "processStartedAtUtc", "provider", "startMicroseconds", "startSeconds"
            ],
            "process-start-observed payload");
        if (GetString(payload, "provider", "process-start-observed") !=
                "macos-libproc+kern-procargs2-external-home-v2" ||
            GetCanonicalUtc(payload, "observedAtUtc", "process-start-observed") != eventAtUtc)
        {
            throw new InvalidDataException("Process observation provider or event clock changed.");
        }

        long pid = GetInt64(payload, "pid", "process-start-observed");
        _ = GetInt64(payload, "parentPid", "process-start-observed");
        long startSeconds = GetInt64(payload, "startSeconds", "process-start-observed");
        long startMicroseconds = GetInt64(payload, "startMicroseconds", "process-start-observed");
        long executableDevice = GetInt64(payload, "executableDevice", "process-start-observed");
        long executableInode = GetInt64(payload, "executableInode", "process-start-observed");
        long executableByteLength = GetInt64(payload, "executableByteLength", "process-start-observed");
        if (pid <= 0 || startSeconds <= 0 || startMicroseconds is < 0 or >= 1_000_000 ||
            executableDevice <= 0 || executableInode <= 0 || executableByteLength <= 0)
        {
            throw new InvalidDataException("Process observation kernel identity is out of range.");
        }

        string sourceRoot = Path.GetFullPath(
            readbackContext?.SourceRepositoryRootPath ?? repositoryRoot);
        string executablePath = Path.GetFullPath(
            readbackContext?.DuckStationExecutablePath ?? Path.Combine(
                repositoryRoot,
                expectedProfile.DuckStationExecutableRelativePath));
        string externalHomePath = Path.GetFullPath(
            readbackContext?.ExternalHomeRootPath ?? Path.Combine(
                repositoryRoot,
                expectedProfile.ExternalHomeRelativePath));
        string cuePath = Path.GetFullPath(Path.Combine(
            sourceRoot,
            expectedSource.CandidateCueRelativePath));
        if (GetString(payload, "executableRealPath", "process-start-observed") != executablePath ||
            GetString(payload, "externalHomeRealPath", "process-start-observed") != externalHomePath ||
            executableByteLength != expectedProfile.DuckStationExecutableByteLength ||
            GetSha256(payload, "executableSha256", "process-start-observed") !=
                expectedProfile.DuckStationExecutableSha256)
        {
            throw new InvalidDataException(
                "Process observation is not the pinned packaged executable and external HOME.");
        }
        StableFileRead executableIdentity = ReadStableTrustedFile(
            executablePath,
            MaximumMediaBytes,
            keepBytes: false,
            "packaged DuckStation process executable");
        if (executableIdentity.Device != executableDevice ||
            checked((long)executableIdentity.Inode) != executableInode ||
            executableIdentity.ByteLength != executableByteLength ||
            executableIdentity.Sha256 != expectedProfile.DuckStationExecutableSha256)
        {
            throw new InvalidDataException(
                "Process observation executable vnode identity differs from live packaged readback.");
        }

        JsonElement argvElement = GetProperty(payload, "argv", "process-start-observed");
        if (argvElement.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Process observation argv is not an array.");
        string[] argv = argvElement.EnumerateArray().Select(item =>
            item.ValueKind == JsonValueKind.String && item.GetString() is { Length: > 0 } value
                ? value
                : throw new InvalidDataException("Process observation argv contains invalid text.")).ToArray();
        if (!argv.SequenceEqual([executablePath, cuePath], StringComparer.Ordinal))
            throw new InvalidDataException("Process observation argv is not executable plus pinned CUE.");
        byte[] argvNul = StrictUtf8.GetBytes(string.Join('\0', argv) + '\0');
        string argvNulSha256 = GetSha256(payload, "argvNulSha256", "process-start-observed");
        if (HashBytes(argvNul) != argvNulSha256)
            throw new InvalidDataException("Process observation argv NUL projection changed.");
        string environmentNulSha256 = GetSha256(
            payload,
            "environmentNulSha256",
            "process-start-observed");
        string processStartedAtUtc = GetCanonicalUtc(
            payload,
            "processStartedAtUtc",
            "process-start-observed");
        if (processStartedAtUtc != ProcessStartUtc(startSeconds, startMicroseconds))
            throw new InvalidDataException("Process start UTC is not derived from its kernel epoch.");

        string manifest =
            "id65-v3-runtime-process-instance-v2\n" +
            $"runId={runId}\n" +
            $"bootIdentity={bootIdentity}\n" +
            $"pid={pid}\n" +
            $"startSeconds={startSeconds}\n" +
            $"startMicroseconds={startMicroseconds}\n" +
            $"executableDevice={executableDevice}\n" +
            $"executableInode={executableInode}\n" +
            $"executableSha256={expectedProfile.DuckStationExecutableSha256}\n" +
            $"argvNulSha256={argvNulSha256}\n" +
            $"environmentNulSha256={environmentNulSha256}\n" +
            $"externalHomeRealPath={externalHomePath}\n";
        string expectedProcessInstanceId =
            "id65v3-process-" + HashBytes(StrictUtf8.GetBytes(manifest))[..32];
        if (processInstanceId != expectedProcessInstanceId)
            throw new InvalidDataException("Process instance ID is not the exact external-HOME v2 projection.");
    }

    private static string ProcessStartUtc(long startSeconds, long startMicroseconds)
    {
        try
        {
            return DateTimeOffset.FromUnixTimeSeconds(startSeconds)
                .AddTicks((startMicroseconds / 1_000) * TimeSpan.TicksPerMillisecond)
                .ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new InvalidDataException("Process start epoch is outside canonical UTC.", exception);
        }
    }

    private static void ValidatePrelaunchProof(
        JsonElement proof,
        Id65V3RuntimeEvidenceSourceIdentity expectedSource,
        Id65V3RuntimeEvidenceProfile expectedProfile)
    {
        RequireObjectKeys(proof, PrelaunchProofKeys, "prelaunch proof");
        if (GetString(proof, "format", "prelaunch proof") !=
                "spyro-id65-unattended-profile-preflight" ||
            GetInt32(proof, "formatVersion", "prelaunch proof") != 1 ||
            GetString(proof, "status", "prelaunch proof") != "PREFLIGHT_READY" ||
            GetString(proof, "profileId", "prelaunch proof") != expectedProfile.ProfileId ||
            GetSha256(proof, "receiptSha256", "prelaunch proof") != expectedProfile.HandoffReceiptSha256 ||
            GetSha256(proof, "appContentManifestSha256", "prelaunch proof") !=
                expectedProfile.AppContentManifestSha256 ||
            GetSha256(proof, "appOutputTreeManifestSha256", "prelaunch proof") !=
                expectedProfile.AppOutputTreeManifestSha256 ||
            GetSha256(proof, "homeOutputTreeManifestSha256", "prelaunch proof") !=
                expectedProfile.HomeOutputTreeManifestSha256 ||
            GetSha256(proof, "packageEnvelopeSha256", "prelaunch proof") !=
                expectedProfile.PackageEnvelopeSha256 ||
            GetSha256(proof, "settingsSha256", "prelaunch proof") !=
                expectedProfile.ExternalHomeSettingsSha256 ||
            GetSha256(proof, "biosSha256", "prelaunch proof") !=
                expectedProfile.ExternalHomeBiosSha256 ||
            GetSha256(proof, "candidateCueSha256", "prelaunch proof") !=
                expectedSource.CandidateCueSha256 ||
            GetSha256(proof, "candidateBinSha256", "prelaunch proof") !=
                expectedSource.CandidateBinSha256 ||
            GetSha256(proof, "hostNormalSettingsSentinelSha256", "prelaunch proof") !=
                expectedProfile.HostNormalSettingsSha256 ||
            GetString(proof, "launchGate", "prelaunch proof") != expectedProfile.LaunchGate)
        {
            throw new InvalidDataException("Prelaunch proof identity changed.");
        }
        _ = ParseAndValidateProfileSnapshot(
            GetProperty(proof, "snapshot", "prelaunch proof"),
            expectedProfile,
            "preflight",
            "preflight",
            "not-applicable",
            "1970-01-01T00:00:00.000Z",
            "preflight.profile-preflight");
    }

    private static Id65V3RuntimeProfileSnapshot ParseAndValidateProfileSnapshot(
        JsonElement snapshot,
        Id65V3RuntimeEvidenceProfile expected,
        string phase,
        string sessionId,
        string processInstanceId,
        string capturedAtUtc,
        string snapshotId)
    {
        RequireObjectKeys(snapshot, ProfileSnapshotKeys, "runtime profile snapshot");
        string format = GetString(snapshot, "format", "runtime profile snapshot");
        int formatVersion = GetInt32(snapshot, "formatVersion", "runtime profile snapshot");
        string actualSnapshotId = GetString(snapshot, "snapshotId", "runtime profile snapshot");
        string actualPhase = GetString(snapshot, "phase", "runtime profile snapshot");
        string actualSessionId = GetString(snapshot, "sessionId", "runtime profile snapshot");
        string actualProcessInstanceId = GetString(
            snapshot,
            "processInstanceId",
            "runtime profile snapshot");
        string profileId = GetString(snapshot, "profileId", "runtime profile snapshot");
        string actualCapturedAtUtc = GetCanonicalUtc(snapshot, "capturedAtUtc", "runtime profile snapshot");
        string executableSha256 = GetSha256(
            snapshot,
            "duckStationExecutableSha256",
            "runtime profile snapshot");
        string receiptSha256 = GetSha256(snapshot, "handoffReceiptSha256", "runtime profile snapshot");
        string appContentSha256 = GetSha256(
            snapshot,
            "appContentManifestSha256",
            "runtime profile snapshot");
        string appOutputSha256 = GetSha256(
            snapshot,
            "appOutputTreeManifestSha256",
            "runtime profile snapshot");
        string envelopeSha256 = GetSha256(
            snapshot,
            "packageEnvelopeSha256",
            "runtime profile snapshot");
        string settingsSha256 = GetSha256(
            snapshot,
            "externalHomeSettingsSha256",
            "runtime profile snapshot");
        string biosSha256 = GetSha256(
            snapshot,
            "externalHomeBiosSha256",
            "runtime profile snapshot");
        string sentinelId = GetString(
            snapshot,
            "hostNormalSettingsSentinelId",
            "runtime profile snapshot");
        string sentinelSha256 = GetSha256(
            snapshot,
            "hostNormalSettingsSentinelSha256",
            "runtime profile snapshot");
        if (format != ProfileSnapshotFormat || formatVersion != ProfileSnapshotFormatVersion ||
            actualSnapshotId != snapshotId || actualPhase != phase || actualSessionId != sessionId ||
            actualProcessInstanceId != processInstanceId || profileId != expected.ProfileId ||
            actualCapturedAtUtc != capturedAtUtc ||
            executableSha256 != expected.DuckStationExecutableSha256 ||
            receiptSha256 != expected.HandoffReceiptSha256 ||
            appContentSha256 != expected.AppContentManifestSha256 ||
            appOutputSha256 != expected.AppOutputTreeManifestSha256 ||
            envelopeSha256 != expected.PackageEnvelopeSha256 ||
            settingsSha256 != expected.ExternalHomeSettingsSha256 ||
            biosSha256 != expected.ExternalHomeBiosSha256 ||
            sentinelId != expected.HostNormalSettingsSentinelId ||
            sentinelSha256 != expected.HostNormalSettingsSha256)
        {
            throw new InvalidDataException("Runtime profile snapshot identity changed.");
        }

        JsonElement dataFilesElement = GetProperty(snapshot, "dataFiles", "runtime profile snapshot");
        if (dataFilesElement.ValueKind != JsonValueKind.Array ||
            dataFilesElement.GetArrayLength() is < 1 or > 100_000)
            throw new InvalidDataException("Runtime profile snapshot dataFiles is not an array.");
        List<Id65V3RuntimeProfileSnapshotEntry> dataFiles = [];
        string? priorPath = null;
        foreach (JsonElement entry in dataFilesElement.EnumerateArray())
        {
            RequireObjectKeys(entry, ["relativePath", "byteLength", "sha256"], "snapshot data file");
            string relativePath = GetString(entry, "relativePath", "snapshot data file");
            GuardSnapshotDataPath(relativePath);
            if (!IsAllowedSnapshotDataPath(relativePath, expected) ||
                priorPath is not null && ComparePythonUnicode(priorPath, relativePath) >= 0)
            {
                throw new InvalidDataException(
                    "Runtime profile snapshot dataFiles is unapproved, reordered, or duplicated.");
            }
            long byteLength = GetInt64(entry, "byteLength", "snapshot data file");
            if (byteLength < 0)
                throw new InvalidDataException("Runtime profile snapshot data-file length is negative.");
            string sha256 = GetSha256(entry, "sha256", "snapshot data file");
            dataFiles.Add(new(relativePath, byteLength, sha256));
            priorPath = relativePath;
        }
        Id65V3RuntimeProfileSnapshotEntry? settings = dataFiles.SingleOrDefault(item =>
            item.RelativePath == expected.ExternalHomeSettingsRelativePath);
        Id65V3RuntimeProfileSnapshotEntry? bios = dataFiles.SingleOrDefault(item =>
            item.RelativePath == expected.ExternalHomeBiosRelativePath);
        if (settings is null || settings.ByteLength != expected.ExternalHomeSettingsByteLength ||
            settings.Sha256 != expected.ExternalHomeSettingsSha256 ||
            bios is null || bios.ByteLength != expected.ExternalHomeBiosByteLength ||
            bios.Sha256 != expected.ExternalHomeBiosSha256)
        {
            throw new InvalidDataException(
                "Runtime profile snapshot omits pinned external-HOME settings or BIOS bytes.");
        }
        return new(
            format,
            formatVersion,
            actualSnapshotId,
            actualPhase,
            actualSessionId,
            actualProcessInstanceId,
            profileId,
            actualCapturedAtUtc,
            executableSha256,
            receiptSha256,
            appContentSha256,
            appOutputSha256,
            envelopeSha256,
            settingsSha256,
            biosSha256,
            sentinelId,
            sentinelSha256,
            dataFiles.ToArray());
    }

    private static void GuardSnapshotDataPath(string value)
    {
        if (value.Length == 0 || value[0] == '/' || value.Contains('\\') ||
            value.Any(character => character < 0x20 || character == 0x7F))
        {
            throw new InvalidDataException("Runtime profile snapshot path is not canonical POSIX text.");
        }
        string[] segments = value.Split('/');
        if (segments.Any(segment => segment.Length == 0 || segment is "." or ".." ||
                segment.Normalize(NormalizationForm.FormC) != segment))
        {
            throw new InvalidDataException("Runtime profile snapshot path is not segment-safe NFC text.");
        }
    }

    private static bool IsAllowedSnapshotDataPath(
        string relativePath,
        Id65V3RuntimeEvidenceProfile expected) =>
        relativePath == expected.ExternalHomeSettingsRelativePath ||
        relativePath == expected.ExternalHomeBiosRelativePath ||
        IsAllowedVolatileDataPath(relativePath);

    private static bool IsAllowedVolatileDataPath(string relativePath)
    {
        if (relativePath == "playtime.dat")
            return true;
        int separator = relativePath.IndexOf('/');
        if (separator <= 0 || separator == relativePath.Length - 1)
            return false;
        return relativePath[..separator] is "cache" or "screenshots" or "videos";
    }

    private static byte[] BuildLiveProjection(IEnumerable<MachineEvent> events)
    {
        using MemoryStream stream = new();
        foreach (MachineEvent item in events)
        {
            byte[] canonical = SerializeCanonicalElement(item.CanonicalElement);
            stream.Write(canonical);
            stream.WriteByte((byte)'\n');
        }
        return stream.ToArray();
    }

    private static Dictionary<string, (string Kind, string Leaf, string? Role)>
        BuildExpectedRawTable(string sessionId)
    {
        Dictionary<string, (string Kind, string Leaf, string? Role)> result =
            new(StringComparer.Ordinal)
            {
                [$"{sessionId}.profile-before"] =
                    ("runtime-profile-before", "runtime-profile-before.json", null),
                [$"{sessionId}.profile-after"] =
                    ("runtime-profile-after", "runtime-profile-after.json", null),
                [$"{sessionId}.watcher"] =
                    ("watcher-stdout", "watcher-stdout.log", null),
                [$"{sessionId}.screen-recording"] =
                    ("screen-recording", "session-screen-recording.mov",
                        $"{sessionId}.full-window-recording")
            };
        if (sessionId == "candidate-primary-edge-0")
        {
            result[$"{sessionId}.lone-pad"] =
                ("frame", "lone-pad.png", "candidate.lone-pad-visibility");
        }
        if (sessionId is "candidate-primary-edge-0" or "candidate-edge-1" or "candidate-edge-2")
        {
            string checkId = sessionId switch
            {
                "candidate-primary-edge-0" => "candidate.primary-edge-4-to-3-same-pad-yaw",
                "candidate-edge-1" => "candidate.edge-1-same-pad-yaw",
                _ => "candidate.edge-2-same-pad-yaw"
            };
            result[$"{sessionId}.edge-before"] =
                ("frame", "edge-before.png", checkId + ".pre-fall-frame");
            result[$"{sessionId}.edge-after"] =
                ("frame", "edge-after.png",
                    sessionId == "candidate-edge-2"
                        ? checkId + ".post-respawn-and-cold-reset-pre-frame"
                        : checkId + ".post-respawn-frame");
        }
        if (sessionId == "candidate-cold-reset")
        {
            result[$"{sessionId}.cold-reset-after"] =
                ("frame", "cold-reset-after.png",
                    "candidate.cold-reset-same-pad-yaw.post-reset-frame");
        }
        return result;
    }

    private static void ValidateRunOrigin(
        JsonElement origin,
        string root,
        string runId,
        IReadOnlyDictionary<string, string> expectedPins,
        Id65V3RuntimeEvidenceSourceIdentity expectedSource,
        Id65V3RuntimeEvidenceProfile expectedProfile)
    {
        RequireObjectKeys(
            origin,
            [
                "bootIdentity", "captureStagingRepoRelativePath", "createdAtUtc",
                "createdMonotonicMicroseconds", "format", "formatVersion",
                "orderedSessionIds", "prelaunchProof", "prelaunchProofSha256",
                "repositoryRootRealPath", "runId", .. CapturePinKeys
            ],
            "run origin");
        JsonElement ordered = GetProperty(origin, "orderedSessionIds", "run origin");
        if (ordered.ValueKind != JsonValueKind.Array ||
            ordered.GetArrayLength() != OrderedSessionIds.Length ||
            ordered.EnumerateArray().Any(item => item.ValueKind != JsonValueKind.String) ||
            !ordered.EnumerateArray().Select(item => item.GetString()!).SequenceEqual(OrderedSessionIds))
        {
            throw new InvalidDataException("Run origin ordered session IDs changed.");
        }
        if (GetString(origin, "format", "run origin") != RunOriginFormat ||
            GetInt32(origin, "formatVersion", "run origin") != RunOriginFormatVersion ||
            GetString(origin, "runId", "run origin") != runId ||
            GetString(origin, "repositoryRootRealPath", "run origin") != root ||
            GetString(origin, "captureStagingRepoRelativePath", "run origin") !=
                CaptureStagingPrefix + "/" + runId)
        {
            throw new InvalidDataException("Run origin identity or current frozen pins changed.");
        }
        foreach (string pin in CapturePinKeys)
        {
            string value = GetString(origin, pin, "run origin");
            if (!expectedPins.TryGetValue(pin, out string? expected) || value != expected)
                throw new InvalidDataException($"Run origin pin changed: {pin}.");
        }
        _ = GetSha256(origin, "bootIdentity", "run origin");
        _ = GetCanonicalUtc(origin, "createdAtUtc", "run origin");
        if (GetInt64(origin, "createdMonotonicMicroseconds", "run origin") <= 0)
            throw new InvalidDataException("Run origin monotonic clock is not positive.");
        JsonElement proof = GetProperty(origin, "prelaunchProof", "run origin");
        ValidatePrelaunchProof(proof, expectedSource, expectedProfile);
        string proofHash = HashBytes(SerializeVerifierCanonicalLf(proof));
        if (GetSha256(origin, "prelaunchProofSha256", "run origin") != proofHash)
            throw new InvalidDataException("Run origin prelaunch-proof hash changed.");
    }

    private static void ValidateStagingRootEntries(string staging)
    {
        foreach (string path in Directory.EnumerateFileSystemEntries(staging))
        {
            string leaf = Path.GetFileName(path);
            if (leaf == ".capture-coordinator.lock")
            {
                continue;
            }
            if (!RunIdRegex().IsMatch(leaf))
                throw new InvalidDataException("Capture-staging root contains unapproved debris.");
            RequireOwnedPrivateDirectory(path, 0x1C0, $"capture-staging run {leaf}");
        }
    }

    private static void RequireExactDirectoryEntries(
        string directory,
        IEnumerable<string> expected,
        string context)
    {
        string[] actual = Directory.EnumerateFileSystemEntries(directory)
            .Select(path => Path.GetFileName(path) ??
                throw new InvalidDataException($"{context} contains an entry without a leaf name."))
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] wanted = expected.Order(StringComparer.Ordinal).ToArray();
        if (!actual.SequenceEqual(wanted, StringComparer.Ordinal))
            throw new InvalidDataException($"{context} has a missing, extra, or renamed entry.");
    }

    private static (Id65V3RuntimeHumanAttestation Attestation, StableFileRead Read)
        ReadAndValidateAttestation(
            string path,
            string expectedSha256,
            MachineRun machine,
            bool requirePass)
    {
        RequireTrustedExternalAttestationPath(path);
        StableFileRead read = ReadStableOwnedFile(
            path,
            0x180,
            MaximumAttestationBytes,
            keepBytes: true);
        if (read.Sha256 != expectedSha256)
            throw new InvalidDataException(
                $"Human-attestation SHA-256 is {read.Sha256}, expected {expectedSha256}.");
        JsonElement root = ParseCanonicalJson(read.Bytes ?? [], "human attestation");
        Id65V3RuntimeHumanAttestation attestation = ParseAttestation(root);
        ValidateAttestation(attestation, machine, requirePass);
        byte[] roundTrip = SerializeCanonicalAttestation(attestation);
        if (!(read.Bytes ?? []).AsSpan().SequenceEqual(roundTrip))
            throw new InvalidDataException("Human attestation does not round-trip through the exact closed schema.");
        return (attestation, read);
    }

    private static Id65V3RuntimeHumanAttestation ParseAttestation(JsonElement root)
    {
        RequireObjectKeys(
            root,
            [
                "attestedAtUtc", "captureClosureSha256", "checks",
                "finalTerminalEventSha256", "format", "formatVersion", "operator",
                "runId", "runOriginSha256", "runtimeSafety", "sessions"
            ],
            "human attestation");
        JsonElement safety = GetProperty(root, "runtimeSafety", "human attestation");
        RequireObjectKeys(
            safety,
            [
                "activeCheatCount", "inGameSaveUsed", "saveStateCreated",
                "saveStateLoaded", "statement"
            ],
            "runtime safety attestation");
        JsonElement sessionsElement = GetProperty(root, "sessions", "human attestation");
        JsonElement checksElement = GetProperty(root, "checks", "human attestation");
        if (sessionsElement.ValueKind != JsonValueKind.Array || checksElement.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Human-attestation sessions/checks must be arrays.");
        Id65V3RuntimeHumanSessionAttestation[] sessions = sessionsElement.EnumerateArray().Select(item =>
        {
            RequireObjectKeys(
                item,
                [
                    "cardsNone", "cheatsOff", "inGameSaveUsed", "newGame",
                    "saveStateCreated", "saveStateLoaded", "sessionId", "statement"
                ],
                "session attestation");
            return new Id65V3RuntimeHumanSessionAttestation(
                GetString(item, "sessionId", "session attestation"),
                GetBoolean(item, "newGame", "session attestation"),
                GetBoolean(item, "cardsNone", "session attestation"),
                GetBoolean(item, "cheatsOff", "session attestation"),
                GetBoolean(item, "saveStateLoaded", "session attestation"),
                GetBoolean(item, "saveStateCreated", "session attestation"),
                GetBoolean(item, "inGameSaveUsed", "session attestation"),
                GetString(item, "statement", "session attestation"));
        }).ToArray();
        Id65V3RuntimeHumanCheckAttestation[] checks = checksElement.EnumerateArray().Select(item =>
        {
            RequireObjectKeys(
                item,
                [
                    "checkId", "evidenceBindingSha256", "sessionId", "statement", "status"
                ],
                "check attestation");
            return new Id65V3RuntimeHumanCheckAttestation(
                GetString(item, "checkId", "check attestation"),
                GetString(item, "sessionId", "check attestation"),
                GetString(item, "status", "check attestation"),
                GetSha256(item, "evidenceBindingSha256", "check attestation"),
                GetString(item, "statement", "check attestation"));
        }).ToArray();
        return new(
            GetString(root, "format", "human attestation"),
            GetInt32(root, "formatVersion", "human attestation"),
            GetString(root, "runId", "human attestation"),
            GetSha256(root, "runOriginSha256", "human attestation"),
            GetSha256(root, "finalTerminalEventSha256", "human attestation"),
            GetSha256(root, "captureClosureSha256", "human attestation"),
            GetString(root, "operator", "human attestation"),
            GetCanonicalUtc(root, "attestedAtUtc", "human attestation"),
            new(
                GetBoolean(safety, "saveStateLoaded", "runtime safety attestation"),
                GetBoolean(safety, "saveStateCreated", "runtime safety attestation"),
                GetBoolean(safety, "inGameSaveUsed", "runtime safety attestation"),
                GetInt32(safety, "activeCheatCount", "runtime safety attestation"),
                GetString(safety, "statement", "runtime safety attestation")),
            sessions,
            checks);
    }

    private static void ValidateAttestation(
        Id65V3RuntimeHumanAttestation attestation,
        MachineRun machine,
        bool requirePass)
    {
        if (attestation.Format != HumanAttestationFormat ||
            attestation.FormatVersion != HumanAttestationFormatVersion ||
            attestation.RunId != machine.RunId ||
            attestation.RunOriginSha256 != machine.RunOriginSha256 ||
            attestation.FinalTerminalEventSha256 != machine.FinalTerminalEventSha256 ||
            attestation.CaptureClosureSha256 != machine.CaptureClosureSha256)
        {
            throw new InvalidDataException("Human attestation capture identity changed.");
        }
        ValidateHumanText(attestation.Operator, "operator", 3, 128);
        DateTimeOffset attested = ParseUtc(attestation.AttestedAtUtc, "attestation time");
        if (attested < ParseUtc(machine.FinalTerminalAtUtc, "final capture terminal"))
            throw new InvalidDataException("Human attestation predates the final captured boundary.");
        ValidateHumanText(attestation.RuntimeSafety.Statement, "runtime safety statement", 16, 512);
        if (attestation.RuntimeSafety.SaveStateLoaded ||
            attestation.RuntimeSafety.SaveStateCreated ||
            attestation.RuntimeSafety.InGameSaveUsed ||
            attestation.RuntimeSafety.ActiveCheatCount != 0)
        {
            throw new InvalidDataException("Human attestation reports an unsafe runtime-safety observation.");
        }
        if (!attestation.Sessions.Select(item => item.SessionId).SequenceEqual(OrderedSessionIds) ||
            attestation.Sessions.Length != OrderedSessionIds.Length)
        {
            throw new InvalidDataException("Human attestation does not contain seven exact ordered sessions.");
        }
        foreach (Id65V3RuntimeHumanSessionAttestation session in attestation.Sessions)
        {
            ValidateHumanText(session.Statement, $"session {session.SessionId} statement", 16, 512);
            if (!session.Statement.Contains(session.SessionId, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Session attestation {session.SessionId} must name its exact session ID; blanket statements are forbidden.");
            }
            if (!session.NewGame || !session.CardsNone || !session.CheatsOff ||
                session.SaveStateLoaded || session.SaveStateCreated || session.InGameSaveUsed)
            {
                throw new InvalidDataException(
                    $"Session attestation {session.SessionId} does not assert the exact safe cold-run contract.");
            }
        }
        if (!attestation.Checks.Select(item => item.CheckId).SequenceEqual(OrderedCheckIds) ||
            attestation.Checks.Length != OrderedCheckIds.Length)
        {
            throw new InvalidDataException("Human attestation does not contain 23 exact ordered checks.");
        }
        for (int index = 0; index < attestation.Checks.Length; index++)
        {
            Id65V3RuntimeHumanCheckAttestation check = attestation.Checks[index];
            MachineCheck expected = machine.Checks[index];
            if (check.SessionId != expected.SessionId ||
                check.EvidenceBindingSha256 != expected.EvidenceBindingSha256)
            {
                throw new InvalidDataException(
                    $"Check attestation {check.CheckId} differs from its machine evidence binding.");
            }
            if (check.Status is not ("passed" or "failed" or "inconclusive"))
                throw new InvalidDataException($"Check attestation {check.CheckId} has an unsupported status.");
            if (requirePass && check.Status != "passed")
                throw new InvalidDataException(
                    $"Pass-only finalization refuses {check.CheckId} status '{check.Status}'.");
            ValidateHumanText(check.Statement, $"check {check.CheckId} statement", 16, 512);
            if (!check.Statement.Contains(check.CheckId, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Check attestation {check.CheckId} must name its exact check ID; blanket statements are forbidden.");
            }
            string? requiredEdgeToken =
                Id65V3RuntimeEvidenceContract.RequiredEdgeAssignmentToken(check.CheckId);
            if (requiredEdgeToken is not null &&
                !check.Statement.Contains(requiredEdgeToken, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Check attestation {check.CheckId} must include exact edge-assignment token " +
                    $"'{requiredEdgeToken}'.");
            }
        }
    }

    private static void ValidateHumanText(string value, string context, int minimum, int maximum)
    {
        if (value.Length < minimum || value.Length > maximum || value != value.Trim() ||
            value.Any(char.IsControl))
        {
            throw new InvalidDataException($"Human-attestation {context} is not bounded clean text.");
        }
        string normalized = value.ToLowerInvariant();
        string[] placeholders = ["todo", "tbd", "placeholder", "lorem ipsum", "n/a", "not applicable"];
        if (placeholders.Any(item => normalized == item || normalized.StartsWith(item + " ", StringComparison.Ordinal)))
            throw new InvalidDataException($"Human-attestation {context} is placeholder text.");
    }

    private static Id65V3RuntimeHumanAttestation BuildTemplate(MachineRun machine) => new(
        HumanAttestationFormat,
        HumanAttestationFormatVersion,
        machine.RunId,
        machine.RunOriginSha256,
        machine.FinalTerminalEventSha256,
        machine.CaptureClosureSha256,
        string.Empty,
        machine.FinalTerminalAtUtc,
        new(false, false, false, 0, string.Empty),
        OrderedSessionIds.Select(sessionId => new Id65V3RuntimeHumanSessionAttestation(
            sessionId,
            true,
            true,
            true,
            false,
            false,
            false,
            string.Empty)).ToArray(),
        machine.Checks.Select(check => new Id65V3RuntimeHumanCheckAttestation(
            check.CheckId,
            check.SessionId,
            string.Empty,
            check.EvidenceBindingSha256,
            string.Empty)).ToArray());

    private static Id65V3RuntimeHumanAttestation BuildInspectionAttestation(MachineRun machine) => new(
        HumanAttestationFormat,
        HumanAttestationFormatVersion,
        machine.RunId,
        machine.RunOriginSha256,
        machine.FinalTerminalEventSha256,
        machine.CaptureClosureSha256,
        "machine-inspection-only",
        machine.FinalTerminalAtUtc,
        new(false, false, false, 0, InspectionAttestation),
        OrderedSessionIds.Select(sessionId => new Id65V3RuntimeHumanSessionAttestation(
            sessionId,
            true,
            true,
            true,
            false,
            false,
            false,
            InspectionAttestation + " Session " + sessionId + ".")).ToArray(),
        machine.Checks.Select(check => new Id65V3RuntimeHumanCheckAttestation(
            check.CheckId,
            check.SessionId,
            "inconclusive",
            check.EvidenceBindingSha256,
            InspectionAttestation + " Check " + check.CheckId + "." +
                (Id65V3RuntimeEvidenceContract.RequiredEdgeAssignmentToken(check.CheckId) is
                    string edgeToken ? " Required physical assignment " + edgeToken + "." : string.Empty)))
        .ToArray());

    private static MachineEvent OneEvent(MachineJournal journal, string eventType)
    {
        MachineEvent[] matches = journal.Events.Where(item => item.EventType == eventType).ToArray();
        return matches.Length == 1
            ? matches[0]
            : throw new InvalidDataException(
                $"Session {journal.SessionId} does not contain one exact {eventType} event.");
    }

    private static MachineEvent OneMarker(MachineJournal journal, string eventType, string markerId)
    {
        MachineEvent[] matches = journal.Events.Where(item =>
            item.EventType == eventType &&
            GetString(item.Payload, "markerId", eventType) == markerId).ToArray();
        return matches.Length == 1
            ? matches[0]
            : throw new InvalidDataException(
                $"Session {journal.SessionId} does not contain one exact {eventType}/{markerId} marker.");
    }

    private static string SessionForCheck(string checkId)
    {
        if (checkId == "candidate.edge-1-same-pad-yaw")
            return "candidate-edge-1";
        if (checkId == "candidate.edge-2-same-pad-yaw")
            return "candidate-edge-2";
        if (checkId == "candidate.cold-reset-same-pad-yaw")
            return "candidate-cold-reset";
        if (checkId.StartsWith("candidate.", StringComparison.Ordinal))
            return "candidate-primary-edge-0";
        if (checkId.StartsWith("retail.town-square.", StringComparison.Ordinal))
            return "retail-town-square";
        if (checkId.StartsWith("retail.gnastys-loot.", StringComparison.Ordinal))
            return "retail-gnastys-loot";
        if (checkId.StartsWith("retail.sunny-flight.", StringComparison.Ordinal))
            return "retail-sunny-flight";
        throw new InvalidDataException($"Unknown finalizer check {checkId}.");
    }

    private static Id65V3RuntimeEvidenceFinalizationResult BuildResult(
        Id65V3RuntimeEvidencePackage package,
        string evidenceSha256,
        MachineRun machine,
        bool existingIdentical) => new(
        Id65V3RuntimeEvidenceContract.FocusedRuntimeEvidenceRelativePath,
        evidenceSha256,
        machine.RunId,
        machine.RunOriginSha256,
        machine.FinalTerminalEventSha256,
        machine.CaptureClosureSha256,
        package.Integrity.SourceClosureSha256,
        package.Integrity.RuntimeProfileSha256,
        package.Integrity.ArtifactSetSha256,
        package.Integrity.PayloadSha256,
        package.Integrity.ContractSha256,
        package.ArtifactCatalog.Length,
        package.Sessions.Length,
        package.VisualChecks.Length,
        existingIdentical,
        Id65V3RuntimeEvidenceContract.ExpectedFocusedRuntimeEvidenceSha256);

    private static void RequireFullReadback(
        Id65V3RuntimeEvidencePackage package,
        string root,
        Id65V3RuntimeEvidenceReadbackContext? readbackContext,
        Id65V3RuntimeEvidenceState expected,
        string context)
    {
        Id65V3RuntimeEvidenceValidation validation =
            Id65V3RuntimeEvidenceContract.ValidateCanonicalJson(
                Id65V3RuntimeEvidenceContract.SerializeCanonicalUtf8(package),
                root,
                Id65V3RuntimeEvidenceValidationMode.FullReadback,
                readbackContext);
        RequireExactValidation(validation, expected, context);
    }

    private static void RequireExactValidation(
        Id65V3RuntimeEvidenceValidation validation,
        Id65V3RuntimeEvidenceState expected,
        string context)
    {
        if (!validation.StructurallyValid || validation.State != expected ||
            validation.Package is null || validation.Errors.Length != 0)
        {
            string detail = validation.Errors.FirstOrDefault() ?? validation.State.ToString();
            throw new InvalidDataException($"ID65 V3 {context} FullReadback failed: {detail}");
        }
        Id65V3RuntimeEvidenceDerivedResult result = validation.Package.DerivedResult;
        if (result.PublisherAuthorized || result.AppIntegrated || result.NormalCreateBinEnabled ||
            result.PromotionAuthorized || result.ReleaseIntegrated ||
            result.FarLowDetailAccepted || result.SaveRuntimeAccepted ||
            result.Core4RewardRuntimeAccepted || result.Total200RuntimeAccepted ||
            result.ProcessUseCryptographicallyProven)
        {
            throw new InvalidDataException(
                "Finalized evidence unexpectedly opens a downstream authority boundary.");
        }
    }

    private static void RequireRunId(string runId)
    {
        if (string.IsNullOrEmpty(runId) || !RunIdRegex().IsMatch(runId))
            throw new InvalidDataException("Run ID is not the fixed lowercase id65v3 namespace form.");
    }

    private static JsonElement ParseCanonicalJson(ReadOnlySpan<byte> bytes, string context)
    {
        if (bytes.Length == 0 || bytes.Length > MaximumCanonicalTextBytes ||
            (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF))
        {
            throw new InvalidDataException($"{context} is empty, oversized, or contains a UTF-8 BOM.");
        }
        try
        {
            _ = StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException($"{context} is not strict UTF-8.", exception);
        }
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(
                bytes.ToArray(),
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 64
                });
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"{context} is malformed JSON.", exception);
        }
        using (document)
        {
            RejectDuplicateProperties(document.RootElement, "$", context);
            byte[] canonical = SerializeCanonicalElement(document.RootElement);
            if (!bytes.SequenceEqual(canonical))
                throw new InvalidDataException($"{context} is not exact canonical UTF-8 JSON.");
            return document.RootElement.Clone();
        }
    }

    private static JsonElement ParseVerifierCanonicalJson(ReadOnlySpan<byte> bytes, string context)
    {
        if (bytes.Length < 2 || bytes.Length > MaximumCanonicalTextBytes || bytes[^1] != (byte)'\n' ||
            bytes[^2] == (byte)'\n' ||
            (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF))
        {
            throw new InvalidDataException(
                $"{context} is not one exact LF-terminated verifier JSON object.");
        }
        for (int index = 0; index < bytes.Length; index++)
        {
            if (bytes[index] == (byte)'\r')
                throw new InvalidDataException($"{context} contains a carriage return.");
        }
        try
        {
            _ = StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException($"{context} is not strict UTF-8.", exception);
        }
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(
                bytes[..^1].ToArray(),
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 64
                });
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"{context} is malformed verifier JSON.", exception);
        }
        using (document)
        {
            RejectDuplicateProperties(document.RootElement, "$", context);
            byte[] canonical = SerializeVerifierCanonicalLf(document.RootElement);
            if (!bytes.SequenceEqual(canonical))
                throw new InvalidDataException($"{context} differs from the frozen verifier serializer.");
            return document.RootElement.Clone();
        }
    }

    private static void RejectDuplicateProperties(JsonElement value, string path, string context)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (JsonProperty property in value.EnumerateObject())
            {
                if (!names.Add(property.Name))
                    throw new InvalidDataException($"{context} repeats JSON property {path}.{property.Name}.");
                RejectDuplicateProperties(property.Value, path + "." + property.Name, context);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            int index = 0;
            foreach (JsonElement item in value.EnumerateArray())
                RejectDuplicateProperties(item, path + "[" + index++.ToString(CultureInfo.InvariantCulture) + "]", context);
        }
    }

    private static byte[] SerializeCanonicalJsonValue<T>(T value)
    {
        JsonElement element = JsonSerializer.SerializeToElement(value, CamelJson);
        return SerializeCanonicalElement(element);
    }

    private static byte[] SerializeCanonicalElement(JsonElement element)
    {
        StringBuilder builder = new();
        WriteCanonicalElement(builder, element);
        return StrictUtf8.GetBytes(builder.ToString());
    }

    private static byte[] SerializeVerifierCanonicalLf(JsonElement element)
    {
        StringBuilder builder = new();
        WriteJsonElement(builder, element, ensureAscii: true);
        builder.Append('\n');
        return StrictUtf8.GetBytes(builder.ToString());
    }

    private static void WriteCanonicalElement(StringBuilder builder, JsonElement element) =>
        WriteJsonElement(builder, element, ensureAscii: false);

    private static void WriteJsonElement(
        StringBuilder builder,
        JsonElement element,
        bool ensureAscii)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                builder.Append('{');
                bool firstProperty = true;
                foreach (JsonProperty property in element.EnumerateObject()
                             .OrderBy(item => item.Name, PythonUnicodeComparer.Instance))
                {
                    if (!firstProperty)
                        builder.Append(',');
                    firstProperty = false;
                    WriteJsonString(builder, property.Name, ensureAscii);
                    builder.Append(':');
                    WriteJsonElement(builder, property.Value, ensureAscii);
                }
                builder.Append('}');
                return;
            case JsonValueKind.Array:
                builder.Append('[');
                bool firstItem = true;
                foreach (JsonElement item in element.EnumerateArray())
                {
                    if (!firstItem)
                        builder.Append(',');
                    firstItem = false;
                    WriteJsonElement(builder, item, ensureAscii);
                }
                builder.Append(']');
                return;
            case JsonValueKind.String:
                WriteJsonString(
                    builder,
                    element.GetString() ?? throw new InvalidDataException("Canonical JSON string is null."),
                    ensureAscii);
                return;
            case JsonValueKind.Number:
                if (!element.TryGetInt64(out long integer) ||
                    integer is < -MaximumIJsonInteger or > MaximumIJsonInteger)
                {
                    throw new InvalidDataException("Canonical JSON number is not an exact I-JSON integer.");
                }
                builder.Append(integer.ToString(CultureInfo.InvariantCulture));
                return;
            case JsonValueKind.True:
                builder.Append("true");
                return;
            case JsonValueKind.False:
                builder.Append("false");
                return;
            case JsonValueKind.Null:
                builder.Append("null");
                return;
            default:
                throw new InvalidDataException("Unsupported JSON token in canonical serializer.");
        }
    }

    private static void WriteJsonString(StringBuilder builder, string value, bool ensureAscii)
    {
        builder.Append('"');
        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            switch (character)
            {
                case '"':
                    builder.Append("\\\"");
                    continue;
                case '\\':
                    builder.Append("\\\\");
                    continue;
                case '\b':
                    builder.Append("\\b");
                    continue;
                case '\t':
                    builder.Append("\\t");
                    continue;
                case '\n':
                    builder.Append("\\n");
                    continue;
                case '\f':
                    builder.Append("\\f");
                    continue;
                case '\r':
                    builder.Append("\\r");
                    continue;
            }
            if (character < 0x20 || ensureAscii && character >= 0x7F)
            {
                if (char.IsHighSurrogate(character))
                {
                    if (index + 1 >= value.Length || !char.IsLowSurrogate(value[index + 1]))
                        throw new InvalidDataException("Canonical JSON contains an unpaired UTF-16 surrogate.");
                    AppendUnicodeEscape(builder, character);
                    AppendUnicodeEscape(builder, value[++index]);
                    continue;
                }
                if (char.IsLowSurrogate(character))
                    throw new InvalidDataException("Canonical JSON contains an unpaired UTF-16 surrogate.");
                AppendUnicodeEscape(builder, character);
                continue;
            }
            if (char.IsSurrogate(character))
            {
                if (!char.IsHighSurrogate(character) || index + 1 >= value.Length ||
                    !char.IsLowSurrogate(value[index + 1]))
                {
                    throw new InvalidDataException("Canonical JSON contains an unpaired UTF-16 surrogate.");
                }
                builder.Append(character).Append(value[++index]);
                continue;
            }
            builder.Append(character);
        }
        builder.Append('"');
    }

    private static void AppendUnicodeEscape(StringBuilder builder, char value) =>
        builder.Append("\\u").Append(((int)value).ToString("x4", CultureInfo.InvariantCulture));

    private static int ComparePythonUnicode(string left, string right)
    {
        int leftIndex = 0;
        int rightIndex = 0;
        while (leftIndex < left.Length && rightIndex < right.Length)
        {
            int leftScalar = ReadUnicodeScalar(left, ref leftIndex);
            int rightScalar = ReadUnicodeScalar(right, ref rightIndex);
            int comparison = leftScalar.CompareTo(rightScalar);
            if (comparison != 0)
                return comparison;
        }
        return (left.Length - leftIndex).CompareTo(right.Length - rightIndex);
    }

    private static int ReadUnicodeScalar(string value, ref int index)
    {
        char first = value[index++];
        if (!char.IsSurrogate(first))
            return first;
        if (!char.IsHighSurrogate(first) || index >= value.Length || !char.IsLowSurrogate(value[index]))
            throw new InvalidDataException("Canonical JSON contains an unpaired UTF-16 surrogate.");
        return char.ConvertToUtf32(first, value[index++]);
    }

    private sealed class PythonUnicodeComparer : IComparer<string>
    {
        internal static readonly PythonUnicodeComparer Instance = new();

        public int Compare(string? left, string? right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left is null)
                return -1;
            if (right is null)
                return 1;
            return ComparePythonUnicode(left, right);
        }
    }

    private static void RequireObjectKeys(
        JsonElement value,
        IEnumerable<string> expected,
        string context)
    {
        if (value.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException($"{context} is not an object.");
        string[] actual = value.EnumerateObject().Select(item => item.Name)
            .Order(StringComparer.Ordinal).ToArray();
        string[] wanted = expected.Order(StringComparer.Ordinal).ToArray();
        if (!actual.SequenceEqual(wanted, StringComparer.Ordinal))
            throw new InvalidDataException($"{context} has an unknown or missing property.");
    }

    private static JsonElement GetProperty(JsonElement value, string name, string context) =>
        value.TryGetProperty(name, out JsonElement property)
            ? property
            : throw new InvalidDataException($"{context} omits {name}.");

    private static string GetString(JsonElement value, string name, string context)
    {
        JsonElement property = GetProperty(value, name, context);
        if (property.ValueKind != JsonValueKind.String || property.GetString() is not { Length: > 0 } text)
            throw new InvalidDataException($"{context} {name} is not nonempty text.");
        return text;
    }

    private static string GetSha256(JsonElement value, string name, string context)
    {
        string result = GetString(value, name, context);
        if (!Sha256Regex().IsMatch(result))
            throw new InvalidDataException($"{context} {name} is not lowercase SHA-256.");
        return result;
    }

    private static long GetInt64(JsonElement value, string name, string context)
    {
        JsonElement property = GetProperty(value, name, context);
        if (property.ValueKind != JsonValueKind.Number || !property.TryGetInt64(out long result) ||
            result is < -9_007_199_254_740_991 or > 9_007_199_254_740_991)
        {
            throw new InvalidDataException($"{context} {name} is not an exact I-JSON integer.");
        }
        return result;
    }

    private static int GetInt32(JsonElement value, string name, string context)
    {
        long result = GetInt64(value, name, context);
        if (result is < int.MinValue or > int.MaxValue)
            throw new InvalidDataException($"{context} {name} is outside Int32.");
        return checked((int)result);
    }

    private static bool GetBoolean(JsonElement value, string name, string context)
    {
        JsonElement property = GetProperty(value, name, context);
        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw new InvalidDataException($"{context} {name} is not Boolean.")
        };
    }

    private static string GetCanonicalUtc(JsonElement value, string name, string context)
    {
        string text = GetString(value, name, context);
        _ = ParseUtc(text, context + " " + name);
        return text;
    }

    private static DateTimeOffset ParseUtc(string value, string context)
    {
        if (!CanonicalUtcRegex().IsMatch(value) ||
            !DateTimeOffset.TryParseExact(
                value,
                "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTimeOffset result))
        {
            throw new InvalidDataException($"{context} is not canonical millisecond UTC.");
        }
        return result;
    }
}
