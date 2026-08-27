using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Spyro.Editor.Core.Exporting;

internal sealed record Id65AuthoringAggregateV1RuntimeEvidenceFinalizerStatus(
    string ProfileId,
    int EventualCheckCount,
    string CaptureCoordinatorSha256,
    string FrozenCaptureCoreSha256,
    string CaptureCoordinatorDeepTestSha256,
    string CaptureSourceClosureSha256,
    string AggregateChecklistSha256,
    string ExactPlanSha256,
    string OperationalToolPinsSha256,
    string CaptureRuntimeProfileSha256,
    string ProfileReceiptClosureSha256,
    string CaptureSchemaClosureSha256,
    string WorkflowDeclarationClosureSha256,
    int ProductionCaptureBlockerCount,
    string ProductionCaptureBlockersSha256,
    bool FrozenWorkflowCompatibleWithAggregateScope,
    string FrozenWorkflowSha256,
    bool SaveCapableProfilePinned,
    bool ProfilePinsVerified,
    bool ExactCaptureEngineImplemented,
    bool ProductionCaptureEnabled,
    bool FocusedEvidenceHashPinned,
    bool CaptureStagingDirectoryPresent,
    bool RuntimeProven,
    bool CreateBinAuthorized,
    bool ReleaseAuthorized,
    bool Publishable,
    string BlockingCheckId,
    string ProductionCaptureRequiredGate,
    string BlockingReason);

internal sealed record Id65AuthoringAggregateV1RuntimeEvidenceFinalizerSmokeReport(
    int PositiveCases,
    int RefusalCases,
    int CheckCount,
    string CaptureCoordinatorSha256,
    string CaptureSourceClosureSha256,
    string AggregateChecklistSha256,
    string CaptureRuntimeProfileSha256,
    string FixtureEvidenceSha256,
    bool ProductionInspectRefused,
    bool ProductionFinalizeRefused,
    bool ProductionVerifyRefused,
    bool ExistingIdenticalObserved,
    bool FsyncFailureQuarantined,
    bool TemporaryFilesCleaned);

internal sealed record Id65AuthoringAggregateV1RuntimeEvidenceFinalizationPreview(
    string RunId,
    string RunOriginSha256,
    string CaptureClosureSha256,
    int ArtifactCount,
    int CheckCount,
    byte[] CanonicalAttestationTemplateUtf8);

internal sealed record Id65AuthoringAggregateV1RuntimeEvidenceFinalizationResult(
    string EvidencePath,
    string EvidenceSha256,
    string RunId,
    string CaptureClosureSha256,
    int ArtifactCount,
    int CheckCount,
    bool ExistingIdentical,
    bool RuntimeProven,
    bool CreateBinAuthorized,
    bool ReleaseAuthorized,
    bool Publishable);

internal static partial class Id65AuthoringAggregateV1RuntimeEvidenceFinalizer
{
    private const string FixtureOriginFormat =
        "spyro-editor-id65-authoring-aggregate-v1-finalizer-fixture-origin";
    private const string FixtureJournalFormat =
        "spyro-editor-id65-authoring-aggregate-v1-finalizer-fixture-journal";
    private const string FixtureAttestationFormat =
        "spyro-editor-id65-authoring-aggregate-v1-finalizer-fixture-attestation";
    private const string FixtureEvidenceFormat =
        "spyro-editor-id65-authoring-aggregate-v1-finalizer-fixture-result";
    private const int FixtureFormatVersion = 1;
    private const int MaximumCanonicalFileBytes = 16 * 1024 * 1024;
    private const string FixtureRecordedAtUtc = "2026-08-25T00:00:00.000Z";
    private const string FixtureAttestedAtUtc = "2026-08-25T00:01:00.000Z";
    private const string FixtureStatement =
        "Offline fixture binding only; no game runtime or acceptance was observed.";
    private const string ExpectedCaptureCoordinatorSha256 =
        "de7ca2b3886fc897f876fb9f53727c19596a188d8882979f60cce16fd3e63c2c";
    private const long ExpectedCaptureCoordinatorByteLength = 104_888;
    private const string ExpectedFrozenCaptureCoreSha256 =
        "153871778877a8736a1243ce2e84453e4d0d8e2fb945f9e96fdc01fbf00278ec";
    private const long ExpectedFrozenCaptureCoreByteLength = 473_144;
    private const string ExpectedCaptureCoordinatorDeepTestSha256 =
        "10104ab431260fafed5d215d5c16325c6f5792848dbfcb69c77b98cb4cdbfb72";
    private const long ExpectedCaptureCoordinatorDeepTestByteLength = 77_990;
    private const string ExpectedCaptureSourceClosureSha256 =
        "fa601b2d8aad8a8931ce49e8be9269afdc3bb3105e927d65c70c138e9a832a41";
    private const string ExpectedAggregateChecklistSha256 =
        "3fee9f6a9b8aa5e90a846834b29c4d1b3f17683d19f2b9804126d17527eefeef";
    private const string ExpectedExactPlanSha256 =
        "23838ecb8f05c62f2d4d2df243fd37553eb5f36579e95d137ac7beb6209c1543";
    private const string ExpectedOperationalToolPinsSha256 =
        "ac30470f7741d0273a9d78a63f25fafb0709ce74f430c85256b83173ac7716c7";
    private const string ExpectedCaptureRuntimeProfileSha256 =
        "419b355557a69683e33e254d8609d4dd285ebfeb0529a602b2a22918a8b010f6";
    private const string ExpectedProfileReceiptClosureSha256 =
        "7b43c1c81e4ca40148c99c2c0d908891453e2882c9f93b0d0f9d440c52cb5330";
    private const string ExpectedCaptureSchemaClosureSha256 =
        "3a30359f741bf31d9be46a168662b4d0cf2f38bbbe86806e304a0744808f01f4";
    private const string ExpectedWorkflowDeclarationClosureSha256 =
        "b086b7336c975a59b24cde401d4b5080b260ad9da563c12f2d33518d7096a9c3";
    private const string ExpectedProductionCaptureBlockersSha256 =
        "9069369b35c43da4f6002477b094a8aa5e2be7e0076530c718366ecc95349769";
    private const string ExpectedFrozenWorkflowSha256 =
        "6ae9d047746dba64e0156de1ef547a4c8454654f8aec9f7cd32b0598d6955a69";
    private const string FinalCaptureRunOriginFormat =
        "spyro-id65-authoring-aggregate-exact-v1-capture-run-origin";
    private const string FinalCaptureJournalFormat =
        "spyro-id65-authoring-aggregate-exact-v1-capture-session-journal";

    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly JsonSerializerOptions FixtureJson = CreateFixtureJson();

    private static readonly string[] ExpectedCheckIds =
    [
        "candidate.safe-load-and-spawn",
        "candidate.authored-terrain-visible",
        "candidate.authored-collision-playable",
        "candidate.authored-occlusion-correct",
        "candidate.private-texture-swap-visible",
        "candidate.grass-zero-egg-placement-present",
        "candidate.grass-authored-placement-present",
        "candidate.zero-egg-preserved",
        "candidate.camera-360-stable",
        "candidate.walk-stable",
        "candidate.jump-land-stable",
        "candidate.fall-respawn-stable",
        "candidate.music-long-play-stable",
        "candidate.treasure-total-200",
        "candidate.exit-destination-65",
        "candidate.save-persistence",
        "retail.town-square-preserved"
    ];

    private static readonly string[] FixtureSessionIds =
    [
        "candidate-fixture",
        "retail-town-square-fixture"
    ];

    private sealed record FixtureCapturePins(
        string ContractSourceClosureSha256,
        string ContractRuntimeProfileClosureSha256,
        string ContractToolSetClosureSha256,
        string CaptureCoordinatorSha256,
        string FrozenCaptureCoreSha256,
        string CaptureCoordinatorDeepTestSha256,
        string CaptureSourceClosureSha256,
        string FinalCaptureRunOriginFormat,
        string FinalCaptureJournalFormat,
        string ProfileBuilderSha256,
        string ProfileVerifierSha256,
        string RuntimeWatcherSha256,
        string AggregateChecklistSha256,
        string ExactPlanSha256,
        string OperationalToolPinsSha256,
        string CaptureRuntimeProfileSha256,
        string ProfileReceiptClosureSha256,
        string CaptureSchemaClosureSha256,
        string WorkflowDeclarationClosureSha256,
        int ProductionCaptureBlockerCount,
        string ProductionCaptureBlockersSha256,
        bool FrozenWorkflowCompatibleWithAggregateScope,
        string FrozenWorkflowSha256);

    private sealed record FixtureOrigin(
        string Format,
        int FormatVersion,
        string RunId,
        string RecordedAtUtc,
        bool FixtureOnly,
        bool RuntimeObserved,
        FixtureCapturePins Pins,
        string[] CheckIds);

    private sealed record FixtureArtifact(
        string ArtifactId,
        string Kind,
        string RelativePath,
        long ByteLength,
        string Sha256,
        string CapturedAtUtc);

    private sealed record FixtureObservation(
        string CheckId,
        string SessionId,
        string[] ArtifactIds);

    private sealed record FixtureJournal(
        string Format,
        int FormatVersion,
        string RunId,
        string SessionId,
        int Ordinal,
        string PreviousJournalSha256,
        FixtureArtifact[] Artifacts,
        FixtureObservation[] Observations);

    private sealed record FixtureAttestationCheck(
        string CheckId,
        string SessionId,
        string Status,
        string EvidenceBindingSha256,
        string Statement);

    private sealed record FixtureAttestation(
        string Format,
        int FormatVersion,
        string RunId,
        string RunOriginSha256,
        string CaptureClosureSha256,
        string Operator,
        string AttestedAtUtc,
        bool FixtureOnly,
        bool RuntimeObserved,
        FixtureAttestationCheck[] Checks);

    private sealed record FixtureEvidenceCheck(
        string CheckId,
        string SessionId,
        string EvidenceBindingSha256,
        string[] ArtifactIds);

    private sealed record FixtureEvidence(
        string Format,
        int FormatVersion,
        string RunId,
        string State,
        string RecordedAtUtc,
        bool FixtureOnly,
        bool RuntimeObserved,
        bool RuntimeProven,
        bool CreateBinAuthorized,
        bool ReleaseAuthorized,
        bool Publishable,
        FixtureCapturePins Pins,
        string RunOriginSha256,
        string CaptureClosureSha256,
        string AttestationSha256,
        FixtureArtifact[] ArtifactCatalog,
        FixtureEvidenceCheck[] Checks,
        string PayloadSha256);

    private sealed record FixtureReviewCheck(
        string CheckId,
        string SessionId,
        string EvidenceBindingSha256,
        string[] ArtifactIds);

    private sealed record FixtureMachineRun(
        string StagingRoot,
        FixtureOrigin Origin,
        AggregateStableFileRead OriginRead,
        FixtureArtifact[] Artifacts,
        FixtureReviewCheck[] Checks,
        string CaptureClosureSha256,
        string StableInputIdentitySha256);

    private sealed record FixtureFinalizerHooks(
        Action? BeforeFinalInputReadback,
        Action<string>? BeforeAtomicRename,
        Action? AfterAtomicRenameBeforeDirectoryFsync);

    internal static Id65AuthoringAggregateV1RuntimeEvidenceFinalizerStatus GetStatus(
        string repositoryRoot)
    {
        string root = RequireRepositoryRoot(repositoryRoot);
        Id65AuthoringAggregateV1RuntimeEvidencePackage pending =
            LoadAndValidatePendingContract();
        VerifyPinnedToolFiles(root, pending);
        Id65AuthoringAggregateV1RuntimeEvidenceToolPin coordinator =
            OneTool(pending, "capture-coordinator");
        Id65AuthoringAggregateV1RuntimeEvidenceToolPin captureCore =
            OneTool(pending, "frozen-capture-core");
        Id65AuthoringAggregateV1RuntimeEvidenceToolPin captureDeepTest =
            OneTool(pending, "capture-deep-test");
        RequireExactToolPin(
            coordinator,
            ExpectedCaptureCoordinatorByteLength,
            ExpectedCaptureCoordinatorSha256,
            "0644");
        RequireExactToolPin(
            captureCore,
            ExpectedFrozenCaptureCoreByteLength,
            ExpectedFrozenCaptureCoreSha256,
            "0755");
        RequireExactToolPin(
            captureDeepTest,
            ExpectedCaptureCoordinatorDeepTestByteLength,
            ExpectedCaptureCoordinatorDeepTestSha256,
            "0644");
        if (pending.ScopeBoundary.AggregateChecklistSha256 !=
                ExpectedAggregateChecklistSha256 ||
            pending.ScopeBoundary.ExactPlanSha256 != ExpectedExactPlanSha256 ||
            pending.ScopeBoundary.OperationalToolPinsSha256 !=
                ExpectedOperationalToolPinsSha256 ||
            pending.ScopeBoundary.CaptureRuntimeProfileSha256 !=
                ExpectedCaptureRuntimeProfileSha256 ||
            pending.ScopeBoundary.ProfileReceiptClosureSha256 !=
                ExpectedProfileReceiptClosureSha256 ||
            pending.RuntimeProfile.HandoffReceiptClosureSha256 !=
                ExpectedProfileReceiptClosureSha256 ||
            pending.ScopeBoundary.CaptureSchemaClosureSha256 !=
                ExpectedCaptureSchemaClosureSha256 ||
            pending.ScopeBoundary.CaptureSourceClosureSha256 !=
                ExpectedCaptureSourceClosureSha256 ||
            pending.ScopeBoundary.WorkflowDeclarationClosureSha256 !=
                ExpectedWorkflowDeclarationClosureSha256 ||
            !pending.ScopeBoundary.SaveCapableProfilePinned ||
            pending.ScopeBoundary.ProfilePinsVerified ||
            pending.ScopeBoundary.ExactCaptureEngineImplemented ||
            pending.ScopeBoundary.ProductionCaptureEnabled ||
            pending.ScopeBoundary.ProductionCaptureBlockers is null ||
            pending.ScopeBoundary.ProductionCaptureBlockers.Length != 1 ||
            pending.ScopeBoundary.ProductionCaptureBlockersSha256 !=
                ExpectedProductionCaptureBlockersSha256 ||
            pending.ScopeBoundary.FrozenWorkflowCompatibleWithAggregateScope ||
            pending.ScopeBoundary.FrozenWorkflowSha256 != ExpectedFrozenWorkflowSha256 ||
            pending.ScopeBoundary.ProductionCaptureRequiredGate !=
                "aggregate.exact-three-session-card-aware-capture-engine")
        {
            throw new InvalidDataException("Aggregate final capture schema/profile pins changed.");
        }
        string[] blockerIds =
        [
            "EXACT_CAPTURE_ENGINE_NOT_IMPLEMENTED"
        ];
        string[] blockerGates =
        [
            "aggregate.exact-three-session-card-aware-capture-engine"
        ];
        if (!pending.ScopeBoundary.ProductionCaptureBlockers
                .Select(item => item.BlockerId).SequenceEqual(blockerIds) ||
            !pending.ScopeBoundary.ProductionCaptureBlockers
                .Select(item => item.RequiredGate).SequenceEqual(blockerGates) ||
            pending.ScopeBoundary.ProductionCaptureBlockers.Any(
                item => string.IsNullOrWhiteSpace(item.Reason)))
        {
            throw new InvalidDataException("Aggregate production blocker identities changed.");
        }
        string focusedHash =
            Id65AuthoringAggregateV1RuntimeEvidenceContract.ExpectedFocusedRuntimeEvidenceSha256;
        if (focusedHash != "PENDING")
            RequireSha256(focusedHash, "focused aggregate evidence SHA-256");
        string captureStaging = Path.GetFullPath(
            Path.Combine(root, pending.ScopeBoundary.CaptureStagingRelativePath));
        if (!captureStaging.StartsWith(root + Path.DirectorySeparatorChar,
                StringComparison.Ordinal))
            throw new InvalidDataException("Aggregate capture staging escaped the repository root.");
        bool stagingPresent = Directory.Exists(captureStaging);
        if (stagingPresent)
            RequireOwnedDirectory(captureStaging, 0x1C0, "aggregate real capture staging");
        return new(
            Id65AuthoringAggregateV1RuntimeEvidenceContract.ExpectedProfileId,
            pending.EventualChecks.Length,
            coordinator.Sha256,
            captureCore.Sha256,
            captureDeepTest.Sha256,
            pending.ScopeBoundary.CaptureSourceClosureSha256,
            pending.ScopeBoundary.AggregateChecklistSha256,
            pending.ScopeBoundary.ExactPlanSha256,
            pending.ScopeBoundary.OperationalToolPinsSha256,
            pending.ScopeBoundary.CaptureRuntimeProfileSha256,
            pending.ScopeBoundary.ProfileReceiptClosureSha256,
            pending.ScopeBoundary.CaptureSchemaClosureSha256,
            pending.ScopeBoundary.WorkflowDeclarationClosureSha256,
            pending.ScopeBoundary.ProductionCaptureBlockers.Length,
            pending.ScopeBoundary.ProductionCaptureBlockersSha256,
            pending.ScopeBoundary.FrozenWorkflowCompatibleWithAggregateScope,
            pending.ScopeBoundary.FrozenWorkflowSha256,
            pending.ScopeBoundary.SaveCapableProfilePinned,
            pending.ScopeBoundary.ProfilePinsVerified,
            pending.ScopeBoundary.ExactCaptureEngineImplemented,
            pending.ScopeBoundary.ProductionCaptureEnabled,
            focusedHash != "PENDING",
            stagingPresent,
            pending.DerivedResult.RuntimeProven,
            pending.DerivedResult.CreateBinAuthorized,
            pending.DerivedResult.ReleaseAuthorized,
            pending.DerivedResult.Publishable,
            pending.ScopeBoundary.ProductionCaptureBlockingCheckId,
            pending.ScopeBoundary.ProductionCaptureRequiredGate,
            pending.ScopeBoundary.ProductionCaptureBlockingReason);
    }

    internal static Id65AuthoringAggregateV1RuntimeEvidenceFinalizationPreview Inspect(
        string repositoryRoot,
        string runId)
    {
        RequireProductionGateOpen(repositoryRoot, runId);
        throw new InvalidOperationException(
            "Aggregate V1 production inspection is unavailable in this offline fixture slice.");
    }

    internal static Id65AuthoringAggregateV1RuntimeEvidenceFinalizationResult FinalizeFocusedPass(
        string repositoryRoot,
        string runId,
        string attestationPath,
        string attestationSha256)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(attestationPath);
        RequireSha256(attestationSha256, "attestation SHA-256");
        RequireProductionGateOpen(repositoryRoot, runId);
        throw new InvalidOperationException(
            "Aggregate V1 production finalization is unavailable in this offline fixture slice.");
    }

    internal static Id65AuthoringAggregateV1RuntimeEvidenceFinalizationResult VerifyExisting(
        string repositoryRoot)
    {
        RequireProductionGateOpen(repositoryRoot, runId: null);
        throw new InvalidOperationException(
            "Aggregate V1 production evidence verification is unavailable in this offline fixture slice.");
    }

    internal static Id65AuthoringAggregateV1RuntimeEvidenceFinalizerSmokeReport
        RunOfflineFixtureSmoke(string repositoryRoot)
    {
        string root = RequireRepositoryRoot(repositoryRoot);
        Id65AuthoringAggregateV1RuntimeEvidenceFinalizerStatus status = GetStatus(root);
        if (status.EventualCheckCount != 17 || !status.SaveCapableProfilePinned ||
            status.ProfilePinsVerified || status.ExactCaptureEngineImplemented ||
            status.ProductionCaptureEnabled || status.FocusedEvidenceHashPinned ||
            status.RuntimeProven || status.CreateBinAuthorized || status.ReleaseAuthorized ||
            status.Publishable || status.ProductionCaptureBlockerCount != 1 ||
            status.ProductionCaptureBlockersSha256 !=
                ExpectedProductionCaptureBlockersSha256 ||
            status.FrozenWorkflowCompatibleWithAggregateScope ||
            status.FrozenWorkflowSha256 != ExpectedFrozenWorkflowSha256 ||
            status.CaptureSourceClosureSha256 != ExpectedCaptureSourceClosureSha256 ||
            status.AggregateChecklistSha256 != ExpectedAggregateChecklistSha256 ||
            status.ExactPlanSha256 != ExpectedExactPlanSha256 ||
            status.OperationalToolPinsSha256 != ExpectedOperationalToolPinsSha256 ||
            status.CaptureRuntimeProfileSha256 != ExpectedCaptureRuntimeProfileSha256 ||
            status.ProfileReceiptClosureSha256 != ExpectedProfileReceiptClosureSha256 ||
            status.CaptureSchemaClosureSha256 != ExpectedCaptureSchemaClosureSha256 ||
            status.WorkflowDeclarationClosureSha256 !=
                ExpectedWorkflowDeclarationClosureSha256 ||
            status.BlockingCheckId != "EXACT_CAPTURE_ENGINE_NOT_IMPLEMENTED" ||
            status.ProductionCaptureRequiredGate !=
                "aggregate.exact-three-session-card-aware-capture-engine")
        {
            throw new InvalidDataException("Aggregate production boundary unexpectedly opened.");
        }

        int positive = 0;
        int refusals = 0;
        const string productionFixtureRunId =
            "id65agg1-20260825t000000z-0123456789abcdef";
        const string expectedProductionGateMessage =
            "Aggregate V1 runtime finalization is fail-closed";
        bool inspectRefused = ExpectInvalidOperationContaining(
            () => _ = Inspect(root, productionFixtureRunId),
            expectedProductionGateMessage,
            ref refusals);
        bool finalizeRefused = ExpectInvalidOperationContaining(
            () => _ = FinalizeFocusedPass(
                root,
                productionFixtureRunId,
                "/private/tmp/nonexistent-aggregate-attestation.json",
                new string('0', 64)),
            expectedProductionGateMessage,
            ref refusals);
        bool verifyRefused = ExpectInvalidOperationContaining(
            () => _ = VerifyExisting(root), expectedProductionGateMessage, ref refusals);

        string temporaryParent = NormalizeTemporaryParent(Path.GetTempPath());
        string smokeRoot = Path.Combine(
            temporaryParent, "id65-aggregate-finalizer-smoke-" + Guid.NewGuid().ToString("N"));
        CreateOwnedDirectory(smokeRoot);
        string? fixtureEvidenceSha256 = null;
        bool existingIdentical = false;
        bool quarantined = false;
        bool cleaned = false;
        try
        {
            string staging = Path.Combine(smokeRoot, "staging");
            string external = Path.Combine(smokeRoot, "external");
            string output = Path.Combine(smokeRoot, "output");
            CreateOwnedDirectory(staging);
            CreateOwnedDirectory(external);
            CreateOwnedDirectory(output);
            BuildFixtureCapture(staging);

            Id65AuthoringAggregateV1RuntimeEvidenceFinalizationPreview preview =
                InspectFixture(staging);
            positive++;
            if (preview.CheckCount != ExpectedCheckIds.Length || preview.ArtifactCount != 17)
                throw new InvalidDataException("Fixture inspection count changed.");
            string attestation = Path.Combine(external, "human-attestation.json");
            WriteOwnedFile(attestation, preview.CanonicalAttestationTemplateUtf8);
            AggregateStableFileRead attestationRead = ReadStableOwnedFile(
                attestation, 0x180, MaximumCanonicalFileBytes, "fixture attestation");

            string evidence = Path.Combine(output, "fixture-evidence.json");
            Id65AuthoringAggregateV1RuntimeEvidenceFinalizationResult first = FinalizeFixture(
                staging, attestation, attestationRead.Sha256, evidence, hooks: null);
            positive++;
            fixtureEvidenceSha256 = first.EvidenceSha256;
            if (first.ExistingIdentical || first.RuntimeProven || first.CreateBinAuthorized ||
                first.ReleaseAuthorized || first.Publishable)
            {
                throw new InvalidDataException("Fixture result crossed an authorization boundary.");
            }
            Id65AuthoringAggregateV1RuntimeEvidenceFinalizationResult repeated = FinalizeFixture(
                staging, attestation, attestationRead.Sha256, evidence, hooks: null);
            positive++;
            existingIdentical = repeated.ExistingIdentical;
            if (!existingIdentical || repeated.EvidenceSha256 != first.EvidenceSha256)
                throw new InvalidDataException("Identical fixture publication was not idempotent.");

            string idempotentJournal = Path.Combine(staging, "journals", "journal-000.json");
            bool idempotentRevalidationRefused = ExpectRefusal(
                () => _ = FinalizeFixture(
                    staging,
                    attestation,
                    attestationRead.Sha256,
                    evidence,
                    new(null,
                        _ => File.SetUnixFileMode(
                            idempotentJournal,
                            UnixFileMode.UserRead | UnixFileMode.GroupRead),
                        null)),
                ref refusals);
            File.SetUnixFileMode(
                idempotentJournal, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            if (!idempotentRevalidationRefused ||
                HashBytes(File.ReadAllBytes(evidence)) != first.EvidenceSha256)
            {
                throw new InvalidDataException(
                    "Existing-identical fixture path skipped final input revalidation.");
            }
            positive++;

            ExerciseStableReadRefusal(smokeRoot, ref refusals);
            ExerciseFixtureInputRefusals(staging, attestation, ref refusals);
            ExercisePublicationRefusals(output, first.EvidenceSha256, ref refusals,
                out quarantined, out cleaned);

            string tamperStaging = Path.Combine(smokeRoot, "tamper-staging");
            CreateOwnedDirectory(tamperStaging);
            BuildFixtureCapture(tamperStaging);
            Id65AuthoringAggregateV1RuntimeEvidenceFinalizationPreview tamperPreview =
                InspectFixture(tamperStaging);
            string tamperAttestation = Path.Combine(external, "tamper-attestation.json");
            WriteOwnedFile(tamperAttestation, tamperPreview.CanonicalAttestationTemplateUtf8);
            AggregateStableFileRead tamperAttestationRead = ReadStableOwnedFile(
                tamperAttestation, 0x180, MaximumCanonicalFileBytes, "tamper attestation");
            string tamperDestination = Path.Combine(output, "must-not-publish.json");
            string tamperJournal = Path.Combine(tamperStaging, "journals", "journal-000.json");
            bool finalReadbackRefused = ExpectRefusal(
                () => _ = FinalizeFixture(
                    tamperStaging,
                    tamperAttestation,
                    tamperAttestationRead.Sha256,
                    tamperDestination,
                    new(() => File.SetUnixFileMode(
                            tamperJournal,
                            UnixFileMode.UserRead | UnixFileMode.GroupRead),
                        null,
                        null)),
                ref refusals);
            if (!finalReadbackRefused || File.Exists(tamperDestination))
                throw new InvalidDataException("Final input readback failure published fixture evidence.");
            positive++;
            File.SetUnixFileMode(
                tamperJournal, UnixFileMode.UserRead | UnixFileMode.UserWrite);

            string preRenameDestination = Path.Combine(output, "must-not-publish-pre-rename.json");
            bool preRenameStagingRefused = ExpectRefusal(
                () => _ = FinalizeFixture(
                    tamperStaging,
                    tamperAttestation,
                    tamperAttestationRead.Sha256,
                    preRenameDestination,
                    new(null,
                        _ => File.SetUnixFileMode(
                            tamperJournal,
                            UnixFileMode.UserRead | UnixFileMode.GroupRead),
                        null)),
                ref refusals);
            if (!preRenameStagingRefused || File.Exists(preRenameDestination) ||
                Directory.EnumerateFiles(output, ".id65-aggregate-fixture.tmp-*").Any())
            {
                throw new InvalidDataException(
                    "Immediate pre-rename staging tamper published or leaked fixture evidence.");
            }
            File.SetUnixFileMode(
                tamperJournal, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            positive++;

            byte[] originalTamperAttestation = File.ReadAllBytes(tamperAttestation);
            string preRenameAttestationDestination =
                Path.Combine(output, "must-not-publish-attestation.json");
            bool preRenameAttestationRefused = ExpectRefusal(
                () => _ = FinalizeFixture(
                    tamperStaging,
                    tamperAttestation,
                    tamperAttestationRead.Sha256,
                    preRenameAttestationDestination,
                    new(null,
                        _ => WriteOwnedFile(tamperAttestation, "tampered-attestation"u8),
                        null)),
                ref refusals);
            if (!preRenameAttestationRefused || File.Exists(preRenameAttestationDestination) ||
                Directory.EnumerateFiles(output, ".id65-aggregate-fixture.tmp-*").Any())
            {
                throw new InvalidDataException(
                    "Immediate pre-rename attestation tamper published or leaked fixture evidence.");
            }
            WriteOwnedFile(tamperAttestation, originalTamperAttestation);
            positive++;
        }
        finally
        {
            Directory.Delete(smokeRoot, recursive: true);
        }
        if (Directory.Exists(smokeRoot))
            throw new IOException("Aggregate fixture smoke root was not removed.");
        cleaned &= !Directory.Exists(smokeRoot);
        if (!inspectRefused || !finalizeRefused || !verifyRefused ||
            !existingIdentical || !quarantined || !cleaned || positive != 7 || refusals != 24 ||
            fixtureEvidenceSha256 is null)
        {
            throw new InvalidDataException(
                "Aggregate finalizer smoke did not close every required safety assertion.");
        }

        return new(
            positive,
            refusals,
            ExpectedCheckIds.Length,
            status.CaptureCoordinatorSha256,
            status.CaptureSourceClosureSha256,
            status.AggregateChecklistSha256,
            status.CaptureRuntimeProfileSha256,
            fixtureEvidenceSha256,
            inspectRefused,
            finalizeRefused,
            verifyRefused,
            existingIdentical,
            quarantined,
            cleaned);
    }

    private static Id65AuthoringAggregateV1RuntimeEvidenceFinalizationPreview InspectFixture(
        string stagingRoot)
    {
        FixtureMachineRun machine = LoadFixtureMachineRun(stagingRoot);
        FixtureAttestation template = BuildFixtureAttestation(machine);
        return new(
            machine.Origin.RunId,
            machine.OriginRead.Sha256,
            machine.CaptureClosureSha256,
            machine.Artifacts.Length,
            machine.Checks.Length,
            SerializeCanonical(template));
    }

    private static Id65AuthoringAggregateV1RuntimeEvidenceFinalizationResult FinalizeFixture(
        string stagingRoot,
        string attestationPath,
        string attestationSha256,
        string destination,
        FixtureFinalizerHooks? hooks)
    {
        RequireSha256(attestationSha256, "fixture attestation SHA-256");
        FixtureMachineRun first = LoadFixtureMachineRun(stagingRoot);
        AggregateStableFileRead firstAttestation = ReadAndValidateFixtureAttestation(
            attestationPath, attestationSha256, first);
        hooks?.BeforeFinalInputReadback?.Invoke();
        FixtureMachineRun second = LoadFixtureMachineRun(stagingRoot);
        AggregateStableFileRead secondAttestation = ReadAndValidateFixtureAttestation(
            attestationPath, attestationSha256, second);
        if (first.StableInputIdentitySha256 != second.StableInputIdentitySha256 ||
            StableIdentityManifest(firstAttestation) != StableIdentityManifest(secondAttestation))
        {
            throw new InvalidDataException("Fixture inputs changed across final readback.");
        }

        FixtureEvidence unsigned = BuildFixtureEvidence(second, secondAttestation.Sha256, string.Empty);
        string payloadSha256 = HashBytes(SerializeCanonical(unsigned));
        FixtureEvidence evidence = BuildFixtureEvidence(second, secondAttestation.Sha256, payloadSha256);
        byte[] canonical = SerializeCanonical(evidence);
        FixtureEvidence parsed = ParseCanonical<FixtureEvidence>(canonical, "fixture evidence post-assembly");
        ValidateFixtureEvidence(parsed, second, secondAttestation.Sha256, payloadSha256);

        void RevalidateInputsImmediatelyBeforeRename(string tempPath)
        {
            hooks?.BeforeAtomicRename?.Invoke(tempPath);
            FixtureMachineRun finalMachine = LoadFixtureMachineRun(stagingRoot);
            AggregateStableFileRead finalAttestation = ReadAndValidateFixtureAttestation(
                attestationPath, attestationSha256, finalMachine);
            if (second.StableInputIdentitySha256 != finalMachine.StableInputIdentitySha256 ||
                StableIdentityManifest(secondAttestation) !=
                    StableIdentityManifest(finalAttestation) ||
                finalMachine.CaptureClosureSha256 != second.CaptureClosureSha256)
            {
                throw new InvalidDataException(
                    "Fixture inputs changed immediately before atomic publication.");
            }
        }

        AggregateAtomicPublication publication = AtomicPublishNoReplace(
            Path.GetFullPath(destination),
            canonical,
            RevalidateInputsImmediatelyBeforeRename,
            hooks?.AfterAtomicRenameBeforeDirectoryFsync);
        FixtureEvidence readback = ParseCanonical<FixtureEvidence>(
            publication.Read.Bytes, "published fixture evidence");
        ValidateFixtureEvidence(readback, second, secondAttestation.Sha256, payloadSha256);
        return new(
            publication.Read.FullPath,
            publication.Read.Sha256,
            second.Origin.RunId,
            second.CaptureClosureSha256,
            second.Artifacts.Length,
            second.Checks.Length,
            publication.ExistingIdentical,
            RuntimeProven: false,
            CreateBinAuthorized: false,
            ReleaseAuthorized: false,
            Publishable: false);
    }

    private static FixtureMachineRun LoadFixtureMachineRun(string stagingRoot)
    {
        string staging = Path.GetFullPath(stagingRoot);
        RequireOwnedDirectory(staging, 0x1C0, "fixture staging root");
        string journalsDirectory = Path.Combine(staging, "journals");
        string artifactsDirectory = Path.Combine(staging, "artifacts");
        RequireOwnedDirectory(journalsDirectory, 0x1C0, "fixture journals directory");
        RequireOwnedDirectory(artifactsDirectory, 0x1C0, "fixture artifacts directory");
        RequireExactEntries(staging, ["artifacts", "journals", "run-origin.json"]);
        RequireExactEntries(journalsDirectory, ["journal-000.json", "journal-001.json"]);

        string originPath = Path.Combine(staging, "run-origin.json");
        AggregateStableFileRead originRead = ReadStableOwnedFile(
            originPath, 0x180, MaximumCanonicalFileBytes, "fixture run origin");
        FixtureOrigin origin = ParseCanonical<FixtureOrigin>(originRead.Bytes, "fixture run origin");
        ValidateFixtureOrigin(origin);

        List<FixtureArtifact> artifacts = [];
        List<FixtureObservation> observations = [];
        List<AggregateStableFileRead> stableReads = [originRead];
        string previous = originRead.Sha256;
        for (int ordinal = 0; ordinal < FixtureSessionIds.Length; ordinal++)
        {
            string journalPath = Path.Combine(journalsDirectory, $"journal-{ordinal:000}.json");
            AggregateStableFileRead journalRead = ReadStableOwnedFile(
                journalPath, 0x180, MaximumCanonicalFileBytes, $"fixture journal {ordinal}");
            FixtureJournal journal = ParseCanonical<FixtureJournal>(
                journalRead.Bytes, $"fixture journal {ordinal}");
            if (journal.Format != FixtureJournalFormat ||
                journal.FormatVersion != FixtureFormatVersion ||
                journal.RunId != origin.RunId || journal.Ordinal != ordinal ||
                journal.SessionId != FixtureSessionIds[ordinal] ||
                journal.PreviousJournalSha256 != previous ||
                journal.Artifacts is null || journal.Observations is null)
            {
                throw new InvalidDataException($"Fixture journal {ordinal} schema or chain changed.");
            }
            foreach (FixtureArtifact artifact in journal.Artifacts)
            {
                if (artifact is null || !IsFixtureToken(artifact.ArtifactId) ||
                    artifact.Kind != "fixture-observation" ||
                    artifact.RelativePath != $"artifacts/{artifact.ArtifactId}.bin" ||
                    artifact.CapturedAtUtc != FixtureRecordedAtUtc || artifact.ByteLength <= 0)
                {
                    throw new InvalidDataException("Fixture artifact declaration is malformed.");
                }
                RequireSha256(artifact.Sha256, "fixture artifact SHA-256");
                string artifactPath = Path.Combine(staging, artifact.RelativePath);
                AggregateStableFileRead artifactRead = ReadStableOwnedFile(
                    artifactPath, 0x180, 1024 * 1024, "fixture artifact");
                if (artifactRead.ByteLength != artifact.ByteLength ||
                    artifactRead.Sha256 != artifact.Sha256)
                {
                    throw new InvalidDataException("Fixture artifact bytes changed.");
                }
                artifacts.Add(artifact);
                stableReads.Add(artifactRead);
            }
            HashSet<string> sessionArtifactIds = journal.Artifacts
                .Select(item => item.ArtifactId).ToHashSet(StringComparer.Ordinal);
            HashSet<string> sessionUsedArtifactIds = new(StringComparer.Ordinal);
            foreach (FixtureObservation observation in journal.Observations)
            {
                if (observation is null || observation.SessionId != journal.SessionId ||
                    observation.ArtifactIds is null || observation.ArtifactIds.Length != 1 ||
                    observation.ArtifactIds.Distinct(StringComparer.Ordinal).Count() !=
                        observation.ArtifactIds.Length ||
                    observation.ArtifactIds.Any(id => !sessionArtifactIds.Contains(id)) ||
                    !sessionUsedArtifactIds.Add(observation.ArtifactIds[0]))
                {
                    throw new InvalidDataException("Fixture observation schema changed.");
                }
                observations.Add(observation);
            }
            if (sessionArtifactIds.Count != journal.Artifacts.Length ||
                sessionUsedArtifactIds.Count != sessionArtifactIds.Count ||
                !sessionUsedArtifactIds.SetEquals(sessionArtifactIds) ||
                journal.Observations.Length != journal.Artifacts.Length)
            {
                throw new InvalidDataException(
                    "Fixture session artifact coverage is not exact and one-to-one.");
            }
            stableReads.Add(journalRead);
            previous = journalRead.Sha256;
        }

        if (artifacts.Count != ExpectedCheckIds.Length || observations.Count != ExpectedCheckIds.Length ||
            artifacts.Select(item => item.ArtifactId).Distinct(StringComparer.Ordinal).Count() !=
                artifacts.Count ||
            !observations.Select(item => item.CheckId).SequenceEqual(ExpectedCheckIds) ||
            observations.Select(item => item.CheckId).Distinct(StringComparer.Ordinal).Count() !=
                ExpectedCheckIds.Length)
        {
            throw new InvalidDataException("Fixture check or artifact identities are not exact and unique.");
        }
        string[] expectedArtifactLeaves = artifacts.Select(
            item => Path.GetFileName(item.RelativePath)).Order(StringComparer.Ordinal).ToArray();
        RequireExactEntries(artifactsDirectory, expectedArtifactLeaves);
        HashSet<string> artifactIds = artifacts.Select(item => item.ArtifactId)
            .ToHashSet(StringComparer.Ordinal);
        if (observations.Any(item => item.ArtifactIds.Any(id => !artifactIds.Contains(id))))
            throw new InvalidDataException("Fixture observation references an unknown artifact.");

        string captureClosure = ComputeFixtureCaptureClosure(originRead, stableReads);
        IReadOnlyDictionary<string, FixtureArtifact> artifactTable = artifacts
            .ToDictionary(item => item.ArtifactId, StringComparer.Ordinal);
        FixtureReviewCheck[] checks = observations.Select(observation => new FixtureReviewCheck(
            observation.CheckId,
            observation.SessionId,
            ComputeFixtureEvidenceBinding(captureClosure, observation, artifactTable),
            observation.ArtifactIds)).ToArray();
        string stableIdentity = HashBytes(StrictUtf8.GetBytes(string.Concat(
            stableReads.OrderBy(item => item.FullPath, StringComparer.Ordinal)
                .Select(StableIdentityManifest))));
        return new(staging, origin, originRead, artifacts.ToArray(), checks, captureClosure,
            stableIdentity);
    }

    private static void BuildFixtureCapture(string stagingRoot)
    {
        string staging = Path.GetFullPath(stagingRoot);
        string journals = Path.Combine(staging, "journals");
        string artifacts = Path.Combine(staging, "artifacts");
        CreateOwnedDirectory(journals);
        CreateOwnedDirectory(artifacts);
        string runId = "id65aggregate-fixture-0123456789abcdef";
        FixtureOrigin origin = new(
            FixtureOriginFormat,
            FixtureFormatVersion,
            runId,
            FixtureRecordedAtUtc,
            FixtureOnly: true,
            RuntimeObserved: false,
            BuildExpectedFixturePins(),
            ExpectedCheckIds);
        string originPath = Path.Combine(staging, "run-origin.json");
        WriteOwnedFile(originPath, SerializeCanonical(origin));
        AggregateStableFileRead originRead = ReadStableOwnedFile(
            originPath, 0x180, MaximumCanonicalFileBytes, "built fixture origin");

        List<FixtureArtifact> declarations = [];
        List<FixtureObservation> observations = [];
        for (int index = 0; index < ExpectedCheckIds.Length; index++)
        {
            string artifactId = $"artifact-{index:00}";
            byte[] bytes = StrictUtf8.GetBytes($"offline-fixture:{ExpectedCheckIds[index]}\n");
            string path = Path.Combine(artifacts, artifactId + ".bin");
            WriteOwnedFile(path, bytes);
            FixtureArtifact declaration = new(
                artifactId,
                "fixture-observation",
                $"artifacts/{artifactId}.bin",
                bytes.LongLength,
                HashBytes(bytes),
                FixtureRecordedAtUtc);
            declarations.Add(declaration);
            observations.Add(new(
                ExpectedCheckIds[index],
                index < 16 ? FixtureSessionIds[0] : FixtureSessionIds[1],
                [artifactId]));
        }

        string previous = originRead.Sha256;
        for (int ordinal = 0; ordinal < FixtureSessionIds.Length; ordinal++)
        {
            int start = ordinal == 0 ? 0 : 16;
            int count = ordinal == 0 ? 16 : 1;
            FixtureJournal journal = new(
                FixtureJournalFormat,
                FixtureFormatVersion,
                runId,
                FixtureSessionIds[ordinal],
                ordinal,
                previous,
                declarations.Skip(start).Take(count).ToArray(),
                observations.Skip(start).Take(count).ToArray());
            string path = Path.Combine(journals, $"journal-{ordinal:000}.json");
            WriteOwnedFile(path, SerializeCanonical(journal));
            previous = ReadStableOwnedFile(
                path, 0x180, MaximumCanonicalFileBytes, "built fixture journal").Sha256;
        }
    }

    private static FixtureAttestation BuildFixtureAttestation(FixtureMachineRun machine) => new(
        FixtureAttestationFormat,
        FixtureFormatVersion,
        machine.Origin.RunId,
        machine.OriginRead.Sha256,
        machine.CaptureClosureSha256,
        "offline-fixture",
        FixtureAttestedAtUtc,
        FixtureOnly: true,
        RuntimeObserved: false,
        machine.Checks.Select(check => new FixtureAttestationCheck(
            check.CheckId,
            check.SessionId,
            "fixture-schema-reviewed",
            check.EvidenceBindingSha256,
            FixtureStatement)).ToArray());

    private static AggregateStableFileRead ReadAndValidateFixtureAttestation(
        string path,
        string expectedSha256,
        FixtureMachineRun machine)
    {
        string full = Path.GetFullPath(path);
        if (!Path.IsPathFullyQualified(path) || full != path ||
            full.StartsWith(machine.StagingRoot + Path.DirectorySeparatorChar,
                StringComparison.Ordinal) || full == machine.StagingRoot)
        {
            throw new InvalidDataException("Fixture attestation path is not an external canonical path.");
        }
        AggregateStableFileRead read = ReadStableOwnedFile(
            full, 0x180, MaximumCanonicalFileBytes, "fixture human attestation");
        if (read.Sha256 != expectedSha256)
            throw new InvalidDataException("Fixture attestation hash pin changed.");
        FixtureAttestation attestation = ParseCanonical<FixtureAttestation>(
            read.Bytes, "fixture human attestation");
        FixtureAttestation expected = BuildFixtureAttestation(machine);
        if (!SerializeCanonical(attestation).AsSpan().SequenceEqual(SerializeCanonical(expected)))
            throw new InvalidDataException("Fixture human attestation binding or schema changed.");
        return read;
    }

    private static FixtureEvidence BuildFixtureEvidence(
        FixtureMachineRun machine,
        string attestationSha256,
        string payloadSha256) => new(
            FixtureEvidenceFormat,
            FixtureFormatVersion,
            machine.Origin.RunId,
            "fixture-only-not-runtime-evidence",
            FixtureRecordedAtUtc,
            FixtureOnly: true,
            RuntimeObserved: false,
            RuntimeProven: false,
            CreateBinAuthorized: false,
            ReleaseAuthorized: false,
            Publishable: false,
            machine.Origin.Pins,
            machine.OriginRead.Sha256,
            machine.CaptureClosureSha256,
            attestationSha256,
            machine.Artifacts,
            machine.Checks.Select(check => new FixtureEvidenceCheck(
                check.CheckId, check.SessionId, check.EvidenceBindingSha256, check.ArtifactIds))
                .ToArray(),
            payloadSha256);

    private static void ValidateFixtureEvidence(
        FixtureEvidence evidence,
        FixtureMachineRun machine,
        string attestationSha256,
        string payloadSha256)
    {
        FixtureEvidence expected = BuildFixtureEvidence(machine, attestationSha256, payloadSha256);
        if (!SerializeCanonical(evidence).AsSpan().SequenceEqual(SerializeCanonical(expected)) ||
            evidence.RuntimeObserved || evidence.RuntimeProven || evidence.CreateBinAuthorized ||
            evidence.ReleaseAuthorized || evidence.Publishable)
        {
            throw new InvalidDataException("Fixture evidence schema, closure, or authority changed.");
        }
        FixtureEvidence unsigned = evidence with { PayloadSha256 = string.Empty };
        if (HashBytes(SerializeCanonical(unsigned)) != payloadSha256)
            throw new InvalidDataException("Fixture evidence payload closure changed.");
    }

    private static string ComputeFixtureCaptureClosure(
        AggregateStableFileRead origin,
        IEnumerable<AggregateStableFileRead> reads)
    {
        StringBuilder builder = new("id65-authoring-aggregate-v1-finalizer-fixture-capture-v1\n");
        builder.Append("origin=").Append(origin.Sha256).Append('\n');
        foreach (AggregateStableFileRead read in reads
                     .Where(item => item.FullPath != origin.FullPath)
                     .OrderBy(item => item.FullPath, StringComparer.Ordinal))
        {
            builder.Append(Path.GetFileName(read.FullPath)).Append('=')
                .Append(read.ByteLength).Append(':').Append(read.Sha256).Append('\n');
        }
        return HashBytes(StrictUtf8.GetBytes(builder.ToString()));
    }

    private static string ComputeFixtureEvidenceBinding(
        string captureClosure,
        FixtureObservation observation,
        IReadOnlyDictionary<string, FixtureArtifact> artifactTable)
    {
        StringBuilder builder = new("id65-authoring-aggregate-v1-finalizer-fixture-binding-v1\n");
        builder.Append("captureClosureSha256=").Append(captureClosure).Append('\n')
            .Append("checkId=").Append(observation.CheckId).Append('\n')
            .Append("sessionId=").Append(observation.SessionId).Append('\n');
        foreach (string artifactId in observation.ArtifactIds)
        {
            FixtureArtifact artifact = artifactTable[artifactId];
            builder.Append("artifact=").Append(artifactId).Append(':')
                .Append(artifact.ByteLength).Append(':').Append(artifact.Sha256).Append('\n');
        }
        return HashBytes(StrictUtf8.GetBytes(builder.ToString()));
    }

    private static FixtureCapturePins BuildExpectedFixturePins()
    {
        Id65AuthoringAggregateV1RuntimeEvidencePackage pending =
            LoadAndValidatePendingContract();
        return new(
            pending.Integrity.SourceClosureSha256,
            pending.Integrity.RuntimeProfileClosureSha256,
            pending.Integrity.ToolSetClosureSha256,
            OneTool(pending, "capture-coordinator").Sha256,
            OneTool(pending, "frozen-capture-core").Sha256,
            OneTool(pending, "capture-deep-test").Sha256,
            pending.ScopeBoundary.CaptureSourceClosureSha256,
            FinalCaptureRunOriginFormat,
            FinalCaptureJournalFormat,
            OneTool(pending, "profile-builder").Sha256,
            OneTool(pending, "profile-verifier").Sha256,
            OneTool(pending, "runtime-watcher").Sha256,
            pending.ScopeBoundary.AggregateChecklistSha256,
            pending.ScopeBoundary.ExactPlanSha256,
            pending.ScopeBoundary.OperationalToolPinsSha256,
            pending.ScopeBoundary.CaptureRuntimeProfileSha256,
            pending.ScopeBoundary.ProfileReceiptClosureSha256,
            pending.ScopeBoundary.CaptureSchemaClosureSha256,
            pending.ScopeBoundary.WorkflowDeclarationClosureSha256,
            pending.ScopeBoundary.ProductionCaptureBlockers.Length,
            pending.ScopeBoundary.ProductionCaptureBlockersSha256,
            pending.ScopeBoundary.FrozenWorkflowCompatibleWithAggregateScope,
            pending.ScopeBoundary.FrozenWorkflowSha256);
    }

    private static void ValidateFixtureOrigin(FixtureOrigin origin)
    {
        if (origin.Format != FixtureOriginFormat ||
            origin.FormatVersion != FixtureFormatVersion ||
            !Regex.IsMatch(origin.RunId, "^id65aggregate-fixture-[0-9a-f]{16}$",
                RegexOptions.CultureInvariant) ||
            origin.RecordedAtUtc != FixtureRecordedAtUtc || !origin.FixtureOnly ||
            origin.RuntimeObserved || origin.Pins is null || origin.CheckIds is null ||
            !origin.CheckIds.SequenceEqual(ExpectedCheckIds) ||
            !SerializeCanonical(origin.Pins).AsSpan()
                .SequenceEqual(SerializeCanonical(BuildExpectedFixturePins())))
        {
            throw new InvalidDataException("Fixture run origin schema or production boundary changed.");
        }
    }

    private static Id65AuthoringAggregateV1RuntimeEvidencePackage LoadAndValidatePendingContract()
    {
        Id65AuthoringAggregateV1RuntimeEvidencePackage pending =
            Id65AuthoringAggregateV1RuntimeEvidenceContract.CreatePending();
        byte[] canonical = Id65AuthoringAggregateV1RuntimeEvidenceContract
            .SerializeCanonicalUtf8(pending);
        Id65AuthoringAggregateV1RuntimeEvidenceValidation validation =
            Id65AuthoringAggregateV1RuntimeEvidenceContract.ValidateCanonicalJson(
                canonical,
                Id65AuthoringAggregateV1RuntimeEvidenceValidationMode.StaticOnly);
        if (!validation.StructurallyValid || validation.Package is null ||
            validation.State != Id65AuthoringAggregateV1RuntimeEvidenceState.PendingNoRun ||
            !pending.EventualChecks.Select(item => item.CheckId).SequenceEqual(ExpectedCheckIds) ||
            pending.EventualChecks.Select(item => item.CheckId)
                .Distinct(StringComparer.Ordinal).Count() != ExpectedCheckIds.Length)
        {
            throw new InvalidDataException("Corrected aggregate V1 pending contract did not validate exactly.");
        }
        return validation.Package;
    }

    private static void VerifyPinnedToolFiles(
        string repositoryRoot,
        Id65AuthoringAggregateV1RuntimeEvidencePackage pending)
    {
        foreach (Id65AuthoringAggregateV1RuntimeEvidenceToolPin tool in pending.ToolPins)
        {
            if (tool.ByteLength is not long byteLength || byteLength <= 0 ||
                !Regex.IsMatch(tool.Mode, "^0[0-7]{3}$", RegexOptions.CultureInvariant))
            {
                throw new InvalidDataException($"Aggregate tool pin {tool.Role} is not final.");
            }
            int mode = Convert.ToInt32(tool.Mode, 8);
            string path = Path.GetFullPath(Path.Combine(repositoryRoot, tool.RelativePath));
            string expectedPrefix = repositoryRoot + Path.DirectorySeparatorChar;
            if (!path.StartsWith(expectedPrefix, StringComparison.Ordinal))
                throw new InvalidDataException("Aggregate tool pin escaped the repository root.");
            AggregateStableFileRead read = ReadStableOwnedFile(
                path, mode, byteLength, $"aggregate tool pin {tool.Role}");
            if (read.ByteLength != byteLength || read.Sha256 != tool.Sha256)
                throw new InvalidDataException($"Aggregate tool pin {tool.Role} changed.");
        }
    }

    private static Id65AuthoringAggregateV1RuntimeEvidenceToolPin OneTool(
        Id65AuthoringAggregateV1RuntimeEvidencePackage pending,
        string role)
    {
        Id65AuthoringAggregateV1RuntimeEvidenceToolPin[] matches = pending.ToolPins
            .Where(item => item.Role == role).ToArray();
        if (matches.Length != 1)
            throw new InvalidDataException($"Aggregate contract tool role {role} is not exact.");
        RequireSha256(matches[0].Sha256, $"aggregate tool {role} SHA-256");
        return matches[0];
    }

    private static void RequireExactToolPin(
        Id65AuthoringAggregateV1RuntimeEvidenceToolPin tool,
        long expectedByteLength,
        string expectedSha256,
        string expectedMode)
    {
        if (tool.ByteLength != expectedByteLength || tool.Sha256 != expectedSha256 ||
            tool.Mode != expectedMode)
        {
            throw new InvalidDataException($"Aggregate final tool pin {tool.Role} changed.");
        }
    }

    private static void RequireProductionGateOpen(string repositoryRoot, string? runId)
    {
        Id65AuthoringAggregateV1RuntimeEvidenceFinalizerStatus status = GetStatus(repositoryRoot);
        if (runId is not null && !Regex.IsMatch(
                runId,
                "^id65agg1-[0-9]{8}t[0-9]{6}z-[0-9a-f]{16}$",
                RegexOptions.CultureInvariant))
        {
            throw new ArgumentException(
                "Production run ID does not match the currently pinned capture schema.",
                nameof(runId));
        }
        if (!status.SaveCapableProfilePinned || !status.ProfilePinsVerified ||
            !status.ExactCaptureEngineImplemented || !status.ProductionCaptureEnabled ||
            !status.FocusedEvidenceHashPinned)
        {
            throw new InvalidOperationException(
                "Aggregate V1 runtime finalization is fail-closed: saveCapableProfilePinned=" +
                status.SaveCapableProfilePinned.ToString().ToLowerInvariant() +
                ", profilePinsVerified=" +
                status.ProfilePinsVerified.ToString().ToLowerInvariant() +
                ", exactCaptureEngineImplemented=" +
                status.ExactCaptureEngineImplemented.ToString().ToLowerInvariant() +
                ", productionCaptureEnabled=" +
                status.ProductionCaptureEnabled.ToString().ToLowerInvariant() +
                ", focusedEvidenceHashPinned=" +
                status.FocusedEvidenceHashPinned.ToString().ToLowerInvariant() +
                $", productionCaptureBlockerCount={status.ProductionCaptureBlockerCount}, " +
                $"primaryBlockingCheckId={status.BlockingCheckId}, " +
                $"requiredGate={status.ProductionCaptureRequiredGate}; " +
                $"{status.BlockingReason}.");
        }
        if (status.ProductionCaptureBlockerCount != 0)
        {
            throw new InvalidOperationException(
                "Aggregate V1 production finalization cannot open while any exact capture " +
                "blocker remains.");
        }
        if (!status.CaptureStagingDirectoryPresent)
            throw new DirectoryNotFoundException("Aggregate V1 capture staging directory is absent.");
        if (status.RuntimeProven || status.CreateBinAuthorized || status.ReleaseAuthorized ||
            status.Publishable)
        {
            throw new InvalidDataException("Pending aggregate contract unexpectedly grants authority.");
        }
    }

    private static string RequireRepositoryRoot(string repositoryRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        string full = Path.GetFullPath(repositoryRoot);
        if (!Path.IsPathFullyQualified(repositoryRoot) || repositoryRoot != full ||
            !File.Exists(Path.Combine(full, "Directory.Build.props")) ||
            !File.Exists(Path.Combine(full, "src", "Spyro.Editor.Core", "Spyro.Editor.Core.csproj")))
        {
            throw new InvalidDataException("Aggregate finalizer repository root is not exact.");
        }
        return full;
    }

    private static byte[] SerializeCanonical<T>(T value)
    {
        JsonElement element = JsonSerializer.SerializeToElement(value, FixtureJson);
        using MemoryStream buffer = new();
        using (Utf8JsonWriter writer = new(buffer, new JsonWriterOptions
               {
                   Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                   Indented = false,
                   SkipValidation = false
               }))
        {
            WriteCanonicalElement(writer, element);
        }
        return buffer.ToArray();
    }

    private static void WriteCanonicalElement(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (JsonProperty property in element.EnumerateObject()
                             .OrderBy(item => item.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonicalElement(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (JsonElement item in element.EnumerateArray())
                    WriteCanonicalElement(writer, item);
                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                string text = element.GetString() ?? string.Empty;
                _ = StrictUtf8.GetByteCount(text);
                writer.WriteStringValue(text);
                break;
            case JsonValueKind.Number:
                if (!element.TryGetInt64(out long number) ||
                    number is < -9_007_199_254_740_991 or > 9_007_199_254_740_991)
                {
                    throw new InvalidDataException("Fixture canonical JSON permits only safe integers.");
                }
                writer.WriteNumberValue(number);
                break;
            case JsonValueKind.True: writer.WriteBooleanValue(true); break;
            case JsonValueKind.False: writer.WriteBooleanValue(false); break;
            case JsonValueKind.Null: writer.WriteNullValue(); break;
            default: throw new InvalidDataException("Unsupported fixture JSON token.");
        }
    }

    private static T ParseCanonical<T>(ReadOnlySpan<byte> bytes, string context)
    {
        if (bytes.Length == 0 || bytes.Length > MaximumCanonicalFileBytes ||
            (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF))
        {
            throw new InvalidDataException($"{context} has an invalid canonical byte envelope.");
        }
        _ = StrictUtf8.GetString(bytes);
        using JsonDocument document = JsonDocument.Parse(bytes.ToArray(), new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 64
        });
        RejectDuplicateProperties(document.RootElement, "$", context);
        T value = JsonSerializer.Deserialize<T>(bytes, FixtureJson) ??
            throw new InvalidDataException($"{context} deserialized to null.");
        byte[] canonical = SerializeCanonical(value);
        if (!bytes.SequenceEqual(canonical))
            throw new InvalidDataException($"{context} is not exact canonical UTF-8 JSON.");
        return value;
    }

    private static void RejectDuplicateProperties(JsonElement element, string path, string context)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                    throw new InvalidDataException($"{context} has duplicate property {path}.{property.Name}.");
                RejectDuplicateProperties(property.Value, path + "." + property.Name, context);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            int index = 0;
            foreach (JsonElement item in element.EnumerateArray())
                RejectDuplicateProperties(item, $"{path}[{index++}]", context);
        }
    }

    private static JsonSerializerOptions CreateFixtureJson() => new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
        NumberHandling = JsonNumberHandling.Strict,
        MaxDepth = 64
    };

    private static void RequireExactEntries(string directory, IEnumerable<string> expected)
    {
        string[] actual = Directory.EnumerateFileSystemEntries(directory)
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToArray()!;
        string[] orderedExpected = expected.Order(StringComparer.Ordinal).ToArray();
        if (!actual.SequenceEqual(orderedExpected))
            throw new InvalidDataException($"Fixture directory shape changed: {directory}.");
    }

    private static void RequireSha256(string value, string context)
    {
        if (!Regex.IsMatch(value, "^[0-9a-f]{64}$", RegexOptions.CultureInvariant))
            throw new InvalidDataException($"{context} is not lowercase hexadecimal SHA-256.");
    }

    private static bool IsFixtureToken(string value) =>
        Regex.IsMatch(value, "^[a-z0-9][a-z0-9-]{2,63}$", RegexOptions.CultureInvariant);

    private static string NormalizeTemporaryParent(string value)
    {
        string full = Path.GetFullPath(value);
        if (full == "/tmp" || full.StartsWith("/tmp/", StringComparison.Ordinal))
            full = "/private" + full;
        else if (full == "/var" || full.StartsWith("/var/", StringComparison.Ordinal))
            full = "/private" + full;
        return full.TrimEnd(Path.DirectorySeparatorChar);
    }

    private static bool ExpectRefusal(Action action, ref int count)
    {
        try
        {
            action();
            return false;
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or
                                          InvalidOperationException or UnauthorizedAccessException or
                                          ArgumentException or JsonException)
        {
            count++;
            return true;
        }
    }

    private static bool ExpectInvalidDataContaining(
        Action action,
        string expectedMessage,
        ref int count)
    {
        try
        {
            action();
            return false;
        }
        catch (InvalidDataException exception) when (
            exception.Message.Contains(expectedMessage, StringComparison.Ordinal))
        {
            count++;
            return true;
        }
    }

    private static bool ExpectInvalidOperationContaining(
        Action action,
        string expectedMessage,
        ref int count)
    {
        try
        {
            action();
            return false;
        }
        catch (InvalidOperationException exception) when (
            exception.Message.Contains(expectedMessage, StringComparison.Ordinal))
        {
            count++;
            return true;
        }
    }

    private static void ExerciseStableReadRefusal(string smokeRoot, ref int refusals)
    {
        string path = Path.Combine(smokeRoot, "stable-read.bin");
        string moved = path + ".moved";
        WriteOwnedFile(path, "original"u8);
        bool refused = ExpectRefusal(
            () => _ = ReadStableOwnedFile(
                path,
                0x180,
                1024,
                "stable replacement fixture",
                () =>
                {
                    File.Move(path, moved);
                    WriteOwnedFile(path, "replacement"u8);
                }),
            ref refusals);
        if (!refused)
            throw new InvalidDataException("Stable read accepted a rebound fixture path.");
        File.Delete(path);
        File.Delete(moved);
    }

    private static void ExerciseFixtureInputRefusals(
        string staging,
        string attestation,
        ref int refusals)
    {
        string journal = Path.Combine(staging, "journals", "journal-000.json");
        string secondJournal = Path.Combine(staging, "journals", "journal-001.json");
        byte[] originalJournal = File.ReadAllBytes(journal);
        byte[] originalSecondJournal = File.ReadAllBytes(secondJournal);
        File.SetUnixFileMode(journal, UnixFileMode.UserRead | UnixFileMode.GroupRead);
        if (!ExpectRefusal(() => _ = InspectFixture(staging), ref refusals))
            throw new InvalidDataException("Fixture wrong-mode journal was accepted.");
        File.SetUnixFileMode(journal, UnixFileMode.UserRead | UnixFileMode.UserWrite);

        WriteOwnedFile(journal, [.. originalJournal, (byte)'\n']);
        if (!ExpectRefusal(() => _ = InspectFixture(staging), ref refusals))
            throw new InvalidDataException("Fixture noncanonical journal was accepted.");
        WriteOwnedFile(journal, originalJournal);

        byte[] duplicate = StrictUtf8.GetBytes(
            "{\"format\":\"duplicate\"," + StrictUtf8.GetString(originalJournal)[1..]);
        WriteOwnedFile(journal, duplicate);
        if (!ExpectRefusal(() => _ = InspectFixture(staging), ref refusals))
            throw new InvalidDataException("Fixture duplicate JSON property was accepted.");
        WriteOwnedFile(journal, originalJournal);

        byte[] extra = StrictUtf8.GetBytes(
            "{\"unexpected\":0," + StrictUtf8.GetString(originalJournal)[1..]);
        WriteOwnedFile(journal, extra);
        if (!ExpectRefusal(() => _ = InspectFixture(staging), ref refusals))
            throw new InvalidDataException("Fixture unknown schema member was accepted.");
        WriteOwnedFile(journal, originalJournal);

        FixtureJournal parsed = ParseCanonical<FixtureJournal>(originalJournal, "fixture mutation source");
        FixtureObservation[] duplicateChecks = parsed.Observations.ToArray();
        duplicateChecks[1] = duplicateChecks[1] with
        {
            CheckId = duplicateChecks[0].CheckId
        };
        WriteOwnedFile(journal, SerializeCanonical(parsed with { Observations = duplicateChecks }));
        FixtureJournal parsedSecond = ParseCanonical<FixtureJournal>(
            originalSecondJournal, "fixture second journal mutation source");
        WriteOwnedFile(secondJournal, SerializeCanonical(parsedSecond with
        {
            PreviousJournalSha256 = HashBytes(File.ReadAllBytes(journal))
        }));
        if (!ExpectInvalidDataContaining(
                () => _ = InspectFixture(staging),
                "Fixture check or artifact identities are not exact and unique.",
                ref refusals))
            throw new InvalidDataException("Fixture duplicate check ID was accepted.");
        WriteOwnedFile(journal, originalJournal);
        WriteOwnedFile(secondJournal, originalSecondJournal);

        FixtureObservation[] crossSessionChecks = parsedSecond.Observations.ToArray();
        crossSessionChecks[0] = crossSessionChecks[0] with { ArtifactIds = ["artifact-00"] };
        WriteOwnedFile(secondJournal, SerializeCanonical(parsedSecond with
        {
            Observations = crossSessionChecks
        }));
        if (!ExpectInvalidDataContaining(
                () => _ = InspectFixture(staging),
                "Fixture observation schema changed.",
                ref refusals))
            throw new InvalidDataException("Fixture cross-session artifact binding was accepted.");
        WriteOwnedFile(secondJournal, originalSecondJournal);

        string artifact = Path.Combine(staging, "artifacts", "artifact-00.bin");
        byte[] artifactBytes = File.ReadAllBytes(artifact);
        WriteOwnedFile(artifact, "tampered-fixture"u8);
        if (!ExpectRefusal(() => _ = InspectFixture(staging), ref refusals))
            throw new InvalidDataException("Fixture artifact tamper was accepted.");
        WriteOwnedFile(artifact, artifactBytes);

        string movedArtifact = artifact + ".owned";
        File.Move(artifact, movedArtifact);
        File.CreateSymbolicLink(artifact, movedArtifact);
        if (!ExpectRefusal(() => _ = InspectFixture(staging), ref refusals))
            throw new InvalidDataException("Fixture symlink artifact was accepted.");
        File.Delete(artifact);
        File.Move(movedArtifact, artifact);

        byte[] attestationBytes = File.ReadAllBytes(attestation);
        FixtureAttestation parsedAttestation = ParseCanonical<FixtureAttestation>(
            attestationBytes, "fixture attestation mutation source");
        FixtureAttestationCheck[] checks = parsedAttestation.Checks.ToArray();
        checks[0] = checks[0] with { EvidenceBindingSha256 = new string('0', 64) };
        WriteOwnedFile(attestation, SerializeCanonical(parsedAttestation with { Checks = checks }));
        string tamperedSha = HashBytes(File.ReadAllBytes(attestation));
        if (!ExpectRefusal(
                () => _ = ReadAndValidateFixtureAttestation(
                    attestation, tamperedSha, LoadFixtureMachineRun(staging)), ref refusals))
            throw new InvalidDataException("Fixture attestation binding tamper was accepted.");
        WriteOwnedFile(attestation, attestationBytes);
    }

    private static void ExercisePublicationRefusals(
        string output,
        string expectedPublishedSha256,
        ref int refusals,
        out bool quarantined,
        out bool cleaned)
    {
        byte[] payload = StrictUtf8.GetBytes("fixture-publication-payload");
        string collision = Path.Combine(output, "collision.json");
        WriteOwnedFile(collision, "occupied"u8);
        byte[] occupied = File.ReadAllBytes(collision);
        if (!ExpectRefusal(() => _ = AtomicPublishNoReplace(collision, payload), ref refusals) ||
            !File.ReadAllBytes(collision).AsSpan().SequenceEqual(occupied))
        {
            throw new InvalidDataException("No-replace collision changed its existing destination.");
        }

        string raced = Path.Combine(output, "raced.json");
        if (!ExpectRefusal(
                () => _ = AtomicPublishNoReplace(
                    raced,
                    payload,
                    _ => WriteOwnedFile(raced, "racing-owner"u8),
                    null),
                ref refusals) ||
            StrictUtf8.GetString(File.ReadAllBytes(raced)) != "racing-owner")
        {
            throw new InvalidDataException("No-replace race did not preserve the competing destination.");
        }
        cleaned = !Directory.EnumerateFiles(output, ".id65-aggregate-fixture.tmp-*").Any();

        string? swappedTemp = null;
        string? movedTemp = null;
        string tempSwapDestination = Path.Combine(output, "temp-swap.json");
        if (!ExpectRefusal(
                () => _ = AtomicPublishNoReplace(
                    tempSwapDestination,
                    payload,
                    tempPath =>
                    {
                        swappedTemp = tempPath;
                        movedTemp = tempPath + ".moved";
                        File.Move(tempPath, movedTemp);
                        WriteOwnedFile(tempPath, "replacement-temp"u8);
                    },
                    null),
                ref refusals) || File.Exists(tempSwapDestination) ||
            swappedTemp is null || movedTemp is null)
        {
            throw new InvalidDataException("Anchored publisher accepted a swapped temporary inode.");
        }
        DeleteOwnedFile(swappedTemp);
        DeleteOwnedFile(movedTemp);

        string movedParent = output + ".moved";
        string parentSwapDestination = Path.Combine(output, "parent-swap.json");
        if (!ExpectRefusal(
                () => _ = AtomicPublishNoReplace(
                    parentSwapDestination,
                    payload,
                    _ =>
                    {
                        Directory.Move(output, movedParent);
                        CreateOwnedDirectory(output);
                    },
                    null),
                ref refusals) || File.Exists(parentSwapDestination))
        {
            throw new InvalidDataException("Anchored publisher accepted a rebound parent directory.");
        }
        Directory.Delete(output);
        Directory.Move(movedParent, output);
        RequireOwnedDirectory(output, 0x1C0, "restored fixture output directory");

        string postHookTamper = Path.Combine(output, "post-hook-tamper.json");
        if (!ExpectRefusal(
                () => _ = AtomicPublishNoReplace(
                    postHookTamper,
                    payload,
                    null,
                    () => WriteOwnedFile(postHookTamper, "tampered-after-rename"u8)),
                ref refusals) || File.Exists(postHookTamper))
        {
            throw new InvalidDataException(
                "Nonthrowing post-rename tamper produced a false publication success.");
        }
        string[] tamperQuarantines = Directory
            .EnumerateFiles(output, ".id65-aggregate-fixture.quarantine-*").ToArray();
        bool tamperQuarantined = tamperQuarantines.Length == 1 &&
            HashBytes(File.ReadAllBytes(tamperQuarantines[0])) ==
                HashBytes("tampered-after-rename"u8);
        if (!tamperQuarantined)
            throw new InvalidDataException("Post-rename tamper was not quarantined exactly.");
        DeleteOwnedFile(tamperQuarantines[0]);

        string postRenameReplacement = Path.Combine(output, "post-rename-replacement.json");
        string movedPublished = postRenameReplacement + ".operation-owned";
        if (!ExpectRefusal(
                () => _ = AtomicPublishNoReplace(
                    postRenameReplacement,
                    payload,
                    null,
                    () =>
                    {
                        File.Move(postRenameReplacement, movedPublished);
                        WriteOwnedFile(postRenameReplacement, "unknown-replacement"u8);
                    }),
                ref refusals) ||
            StrictUtf8.GetString(File.ReadAllBytes(postRenameReplacement)) !=
                "unknown-replacement" ||
            HashBytes(File.ReadAllBytes(movedPublished)) != HashBytes(payload))
        {
            throw new InvalidDataException(
                "Post-rename replacement was deleted or accepted as fixture evidence.");
        }
        DeleteOwnedFile(postRenameReplacement);
        DeleteOwnedFile(movedPublished);

        string fsyncFailure = Path.Combine(output, "fsync-failure.json");
        if (!ExpectRefusal(
                () => _ = AtomicPublishNoReplace(
                    fsyncFailure,
                    payload,
                    null,
                    () => throw new IOException("injected pre-directory-fsync failure")),
                ref refusals) || File.Exists(fsyncFailure))
        {
            throw new InvalidDataException("Post-rename failure left the canonical fixture destination.");
        }
        string[] quarantineFiles = Directory
            .EnumerateFiles(output, ".id65-aggregate-fixture.quarantine-*").ToArray();
        quarantined = tamperQuarantined && quarantineFiles.Length == 1 &&
            HashBytes(File.ReadAllBytes(quarantineFiles[0])) == HashBytes(payload);
        if (!quarantined)
            throw new InvalidDataException("Post-rename failure did not quarantine exact fixture bytes.");
        DeleteOwnedFile(quarantineFiles[0]);
        cleaned &= !Directory.EnumerateFiles(output, ".id65-aggregate-fixture.*").Any();

        string existingEvidence = Path.Combine(output, "fixture-evidence.json");
        if (HashBytes(File.ReadAllBytes(existingEvidence)) != expectedPublishedSha256)
            throw new InvalidDataException("Publication refusal tests changed prior fixture evidence.");
    }
}
