using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Spyro.Editor.Core.Exporting;

internal enum Id65AuthoringAggregateV1RuntimeEvidenceState
{
    PendingNoRun,
    PendingIncomplete,
    RuntimeRejected,
    Invalid,
    FocusedRuntimePass
}

internal enum Id65AuthoringAggregateV1RuntimeEvidenceCheckStatus
{
    NotObserved,
    Passed,
    Failed,
    Inconclusive
}

internal enum Id65AuthoringAggregateV1RuntimeEvidenceValidationMode
{
    StaticOnly,
    FullReadback
}

internal sealed record Id65AuthoringAggregateV1RuntimeEvidenceSourceIdentity(
    string CandidateProfileId,
    string CandidateDirectoryRelativePath,
    string CandidateBinRelativePath,
    long CandidateBinByteLength,
    string CandidateBinSha256,
    string CandidateCueRelativePath,
    long CandidateCueByteLength,
    string CandidateCueSha256,
    string CandidateReceiptRelativePath,
    long CandidateReceiptByteLength,
    string CandidateReceiptSha256,
    string CandidateReceiptContentSha256,
    string AuthoringDocumentRelativePath,
    long AuthoringDocumentByteLength,
    string AuthoringDocumentEnvelopeSha256,
    string AuthoringDocumentCanonicalSha256,
    string AuthoringDocumentContentSha256,
    string LockedBaseImageSha256,
    string AggregatePlanSha256,
    string CompositeManifestSha256,
    string CompositeOutputRow80Sha256,
    string PhysicalDiffSha256,
    int ChangedRawSectorCount,
    long ChangedPhysicalImageBytes,
    string CandidateScusName,
    int CandidateScusLba,
    int CandidateScusByteLength,
    string CandidateScusSha256,
    bool RetailTownSquarePreserved,
    bool ProtectedComplementVerified,
    bool RuntimeCandidateAuthorized,
    bool RuntimeProven,
    bool CreateBinEnabled,
    bool ReleaseAuthorized,
    bool Publishable);

internal sealed record Id65AuthoringAggregateV1RuntimeEvidenceProfile(
    string ProfileId,
    string PackageRelativePath,
    string AppRelativePath,
    string ExecutableRelativePath,
    string ExternalHomeRelativePath,
    string ExternalHomeDataRelativePath,
    string DuckStationVersion,
    long ExecutableByteLength,
    string ExecutableSha256,
    string HandoffReceiptRelativePath,
    string HandoffReceiptFormat,
    int HandoffReceiptFormatVersion,
    long HandoffReceiptByteLength,
    string HandoffReceiptSha256,
    string HandoffReceiptClosureSha256,
    int CandidateManifestEntryCount,
    string CandidateManifestSha256,
    string AppContentManifestFormat,
    int AppContentManifestEntryCount,
    string AppContentManifestSha256,
    string OutputTreeManifestFormat,
    int AppOutputTreeManifestEntryCount,
    string AppOutputTreeManifestSha256,
    int HomeOutputTreeManifestEntryCount,
    string HomeOutputTreeManifestSha256,
    string PackageEnvelopeFormat,
    int PackageEnvelopeEntryCount,
    string PackageEnvelopeSha256,
    string OutputXattrPolicyVersion,
    string ExternalHomeSettingsRelativePath,
    long ExternalHomeSettingsByteLength,
    string ExternalHomeSettingsSha256,
    string ExternalHomeBiosRelativePath,
    long ExternalHomeBiosByteLength,
    string ExternalHomeBiosSha256,
    string HostNormalSettingsAbsolutePath,
    string HostNormalSettingsSentinelId,
    long HostNormalSettingsByteLength,
    string HostNormalSettingsSha256,
    string HostNormalSettingsMode,
    string LaunchGateStatus,
    bool BuilderLaunchedDuckStation,
    bool SaveLoadStateHotkeysPresent,
    bool TextureReplacementOrDumpEnabled,
    string TrustModel);

internal sealed record Id65AuthoringAggregateV1RuntimeEvidenceToolPin(
    string Role,
    string RelativePath,
    long? ByteLength,
    string Sha256,
    string Mode);

internal sealed record Id65AuthoringAggregateV1RuntimeEvidenceCheck(
    string CheckId,
    string Requirement,
    Id65AuthoringAggregateV1RuntimeEvidenceCheckStatus Status,
    string? SessionId,
    string[] EvidenceArtifactIds);

internal sealed record Id65AuthoringAggregateV1RuntimeEvidenceArtifact(
    string ArtifactId,
    string Kind,
    string RepoRelativePath,
    long ByteLength,
    string Sha256,
    string CapturedAtUtc);

internal sealed record Id65AuthoringAggregateV1RuntimeEvidenceProductionBlocker(
    string BlockerId,
    string RequiredGate,
    string Reason);

internal sealed record Id65AuthoringAggregateV1RuntimeEvidenceScopeBoundary(
    string FocusedEvidenceRelativePath,
    string RuntimeAssetRelativePrefix,
    string CaptureStagingRelativePath,
    string CaptureCoordinatorAuthorityLockRelativePath,
    string AggregateChecklistSha256,
    string ExactPlanSha256,
    string OperationalToolPinsSha256,
    string CaptureRuntimeProfileSha256,
    string ProfileReceiptClosureSha256,
    string CaptureSchemaClosureSha256,
    string CaptureSourceClosureSha256,
    string WorkflowDeclarationClosureSha256,
    bool SaveCapableProfilePinned,
    bool ProfilePinsVerified,
    bool ExactCaptureEngineImplemented,
    bool ProductionCaptureEnabled,
    Id65AuthoringAggregateV1RuntimeEvidenceProductionBlocker[] ProductionCaptureBlockers,
    string ProductionCaptureBlockersSha256,
    string ProductionCaptureBlockingCheckId,
    string ProductionCaptureRequiredGate,
    string ProductionCaptureBlockingReason,
    bool FrozenWorkflowCompatibleWithAggregateScope,
    string FrozenWorkflowSha256,
    bool CandidateOnly,
    bool RetailTownSquareControlRequired,
    bool RuntimeObserved,
    bool EditorIntegrated,
    bool CreateBinAuthorized,
    bool NormalCreateBinEnabled,
    bool ReleaseAuthorized,
    bool PromotionAuthorized,
    bool Publishable);

internal sealed record Id65AuthoringAggregateV1RuntimeEvidenceDerivedResult(
    Id65AuthoringAggregateV1RuntimeEvidenceState State,
    bool SourceClosureValid,
    bool RuntimeProfileClosureValid,
    bool ToolPinsFinalized,
    bool RuntimeEvidencePinned,
    bool AllEventualChecksPassed,
    bool FullReadbackAccepted,
    bool RuntimeProven,
    bool CreateBinAuthorized,
    bool ReleaseAuthorized,
    bool Publishable);

internal sealed record Id65AuthoringAggregateV1RuntimeEvidenceIntegrity(
    string Canonicalization,
    string SourceClosureSha256,
    string RuntimeProfileClosureSha256,
    string ToolSetClosureSha256,
    string ArtifactSetClosureSha256,
    string PayloadSha256,
    string ContractSha256);

internal sealed record Id65AuthoringAggregateV1RuntimeEvidencePackage(
    string Format,
    int FormatVersion,
    string EvidenceId,
    string? RecordedAtUtc,
    Id65AuthoringAggregateV1RuntimeEvidenceState DeclaredState,
    Id65AuthoringAggregateV1RuntimeEvidenceSourceIdentity SourceIdentity,
    Id65AuthoringAggregateV1RuntimeEvidenceProfile RuntimeProfile,
    Id65AuthoringAggregateV1RuntimeEvidenceToolPin[] ToolPins,
    Id65AuthoringAggregateV1RuntimeEvidenceArtifact[] ArtifactCatalog,
    Id65AuthoringAggregateV1RuntimeEvidenceCheck[] EventualChecks,
    Id65AuthoringAggregateV1RuntimeEvidenceDerivedResult DerivedResult,
    Id65AuthoringAggregateV1RuntimeEvidenceScopeBoundary ScopeBoundary,
    Id65AuthoringAggregateV1RuntimeEvidenceIntegrity Integrity);

internal sealed record Id65AuthoringAggregateV1RuntimeEvidenceValidation(
    bool StructurallyValid,
    Id65AuthoringAggregateV1RuntimeEvidenceState State,
    Id65AuthoringAggregateV1RuntimeEvidencePackage? Package,
    string CanonicalJson,
    string[] Errors);

internal static partial class Id65AuthoringAggregateV1RuntimeEvidenceContract
{
    internal const string Format =
        "spyro-editor-id65-authoring-aggregate-v1-runtime-evidence";
    internal const int FormatVersion = 1;
    internal const string RequiredSmokeAssemblyName =
        "Spyro.Editor.Id65AuthoringAggregateV1RuntimeEvidenceContractSmoke";
    internal const string ExpectedProfileId =
        "duckstation-id65-authoring-aggregate-save-v1";
    internal const string FocusedRuntimeEvidenceRelativePath =
        "docs/runtime-evidence/unused-level-65-authoring-aggregate-exact-v1-runtime-evidence.json";
    internal const string RuntimeAssetRelativePrefix =
        "docs/runtime-evidence/assets/unused-level-65-authoring-aggregate-exact-v1/";
    internal const string CaptureStagingRelativePath =
        RuntimeAssetRelativePrefix + "capture-staging";
    internal static readonly string ExpectedFocusedRuntimeEvidenceSha256 = "PENDING";
    internal static readonly string ExpectedCaptureCoordinatorSha256 =
        "83cf8e24455364d6ee6e66fe485ebec62e04518592c36edc0b262e14590445af";
    internal const string ExpectedAggregateChecklistSha256 =
        "3fee9f6a9b8aa5e90a846834b29c4d1b3f17683d19f2b9804126d17527eefeef";
    internal const string ExpectedExactPlanSha256 =
        "a8528be7d66a7944fd50bba03479ed42a839d4c0d2c43d1c9432dc1865499bb2";
    internal const string ExpectedOperationalToolPinsSha256 =
        "ac30470f7741d0273a9d78a63f25fafb0709ce74f430c85256b83173ac7716c7";
    internal const string ExpectedCaptureRuntimeProfileSha256 =
        "419b355557a69683e33e254d8609d4dd285ebfeb0529a602b2a22918a8b010f6";
    internal const string ExpectedProfileReceiptClosureSha256 =
        "7b43c1c81e4ca40148c99c2c0d908891453e2882c9f93b0d0f9d440c52cb5330";
    internal const string ExpectedCaptureSchemaClosureSha256 =
        "f2932cade1dceb5890cfda610cbae06e5be1544de304067c4e0b0a3c7cbc4ba4";
    internal const string ExpectedCaptureSourceClosureSha256 =
        "fa601b2d8aad8a8931ce49e8be9269afdc3bb3105e927d65c70c138e9a832a41";
    internal const string ExpectedWorkflowDeclarationClosureSha256 =
        "e2d4dccc1f365076ca1028252eff08242cfb5284caa3e25ff5a388f042aefa3b";
    internal const string ExpectedProductionCaptureBlockersSha256 =
        "9069369b35c43da4f6002477b094a8aa5e2be7e0076530c718366ecc95349769";
    internal const string ExpectedFrozenWorkflowSha256 =
        "6ae9d047746dba64e0156de1ef547a4c8454654f8aec9f7cd32b0598d6955a69";
    internal const string ExpectedSourceClosureSha256 =
        "5f178ebd345424529d4c5d80f40bd7d72ecbf942b1c6fd5d5096c45125533985";
    internal const string ExpectedRuntimeProfileClosureSha256 =
        "b9b9454b425bb7b743234f8f9056ea5820f26308989a75a1f19bdc4decd8243d";
    internal const string ExpectedToolSetClosureSha256 =
        "b3c7b9e1b529566d6c69142f24ea84d9100f44a91d5cc59d395eb4e1bc85b454";

    private const string Canonicalization = "rfc8785-jcs-utf8-v1";
    private const string EvidenceId =
        "unused-level-65-authoring-aggregate-v1-runtime-evidence-pending";
    private const string CandidateDirectory =
        "_local/id65-authoring-aggregate-workspace/_research/unusedlevel65blank/v1/" +
        "materialized-candidates/aggregate-4955c54b3eb7ea5c1871205e559f5d5dda0766613e55aa0de988b92e896d5c21-" +
        "receipt-d6c14861cde023e9e100fe2828227da8252c9de37c36e84d33ba4bfbd3b0516f";
    private const string CandidateStem = "unusedlevel65blank-authoring-aggregate-candidate-v1";
    private const string AuthoringDocumentRelativePath =
        "_local/id65-authoring-aggregate-workspace/_research/unusedlevel65blank/v1/" +
        "authored-edits/unusedlevel65blank-authoring-document-v1.json";
    private const string ProfilePackageRelativePath =
        "_local/v5-stone-hill-level-replacement/" +
        "duckstation-id65-authoring-aggregate-save-v1";
    private const string AppRelativePath =
        ProfilePackageRelativePath + "/DuckStation-ID65-Unattended-v1.app";
    private const string ExternalHomeRelativePath = ProfilePackageRelativePath + "/home";
    private const string ExternalHomeDataRelativePath =
        ExternalHomeRelativePath + "/Library/Application Support/DuckStation";
    private const string HostNormalSettingsAbsolutePath =
        "/Users/john/Library/Application Support/DuckStation/settings.ini";
    private const long MaximumJcsSafeInteger = 9_007_199_254_740_991;
    private const int MaximumEvidenceBytes = 16 * 1024 * 1024;

    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    internal static Id65AuthoringAggregateV1RuntimeEvidencePackage CreatePending()
    {
        Id65AuthoringAggregateV1RuntimeEvidencePackage package = new(
            Format,
            FormatVersion,
            EvidenceId,
            RecordedAtUtc: null,
            Id65AuthoringAggregateV1RuntimeEvidenceState.PendingNoRun,
            BuildExpectedSourceIdentity(),
            BuildExpectedRuntimeProfile(),
            BuildExpectedToolPins(),
            ArtifactCatalog: [],
            BuildExpectedChecks(),
            BuildExpectedDerivedResult(),
            BuildExpectedScopeBoundary(),
            new(Canonicalization, string.Empty, string.Empty, string.Empty, string.Empty,
                string.Empty, string.Empty));
        return WithRecomputedIntegrity(package);
    }

    internal static string SerializeCanonical(
        Id65AuthoringAggregateV1RuntimeEvidencePackage package) =>
        SerializeCanonicalValue(package);

    internal static byte[] SerializeCanonicalUtf8(
        Id65AuthoringAggregateV1RuntimeEvidencePackage package) =>
        StrictUtf8.GetBytes(SerializeCanonical(package));

    internal static Id65AuthoringAggregateV1RuntimeEvidenceValidation ValidateCanonicalJson(
        ReadOnlySpan<byte> utf8Json,
        Id65AuthoringAggregateV1RuntimeEvidenceValidationMode mode =
            Id65AuthoringAggregateV1RuntimeEvidenceValidationMode.StaticOnly)
    {
        List<string> errors = [];
        Id65AuthoringAggregateV1RuntimeEvidencePackage? package = null;
        string canonical = string.Empty;
        try
        {
            if (utf8Json.Length == 0 || utf8Json.Length > MaximumEvidenceBytes)
                throw new InvalidDataException("Aggregate runtime evidence JSON has an invalid byte length.");
            if (utf8Json.Length >= 3 && utf8Json[0] == 0xEF && utf8Json[1] == 0xBB &&
                utf8Json[2] == 0xBF)
            {
                throw new InvalidDataException("Aggregate runtime evidence JSON must not contain a UTF-8 BOM.");
            }
            _ = StrictUtf8.GetString(utf8Json);

            using JsonDocument document = JsonDocument.Parse(
                utf8Json.ToArray(),
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 64
                });
            RejectDuplicateProperties(document.RootElement, "$", errors);
            if (errors.Count != 0)
                throw new InvalidDataException(errors[0]);

            package = JsonSerializer.Deserialize<Id65AuthoringAggregateV1RuntimeEvidencePackage>(
                utf8Json,
                JsonOptions) ?? throw new InvalidDataException(
                    "Aggregate runtime evidence JSON deserialized to null.");
            canonical = SerializeCanonical(package);
            if (!utf8Json.SequenceEqual(StrictUtf8.GetBytes(canonical)))
            {
                throw new InvalidDataException(
                    "Aggregate runtime evidence JSON is not the exact canonical UTF-8 encoding.");
            }

            ValidateStaticPackage(package, mode);
            ValidateIntegrity(package);
            return new(true, package.DeclaredState, package, canonical, []);
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException or
                                         DecoderFallbackException or EncoderFallbackException or
                                         ArgumentException or OverflowException)
        {
            if (errors.Count == 0 || !errors.Contains(exception.Message, StringComparer.Ordinal))
                errors.Add(exception.Message);
            return new(false, Id65AuthoringAggregateV1RuntimeEvidenceState.Invalid, package,
                canonical, errors.ToArray());
        }
    }

    internal static void RequireFocusedRuntimePass(
        Id65AuthoringAggregateV1RuntimeEvidenceValidation validation)
    {
        if (ExpectedCaptureCoordinatorSha256 == "PENDING" ||
            ExpectedFocusedRuntimeEvidenceSha256 == "PENDING")
        {
            throw new InvalidOperationException(
                "The aggregate V1 capture coordinator or focused runtime evidence is PENDING; " +
                "no runtime-pass capability can be issued.");
        }
        if (!validation.StructurallyValid ||
            validation.State != Id65AuthoringAggregateV1RuntimeEvidenceState.FocusedRuntimePass)
        {
            throw new InvalidDataException("Aggregate V1 evidence is not a focused runtime pass.");
        }
        throw new InvalidOperationException(
            "This pending/static contract slice cannot issue a runtime-pass capability.");
    }

    internal static Id65AuthoringAggregateV1RuntimeEvidencePackage WithRecomputedIntegrity(
        Id65AuthoringAggregateV1RuntimeEvidencePackage package) =>
        package with { Integrity = ComputeIntegrity(package) };

    internal static string RecomputeSourceClosureSha256(
        Id65AuthoringAggregateV1RuntimeEvidenceSourceIdentity source) =>
        HashUtf8(SerializeCanonicalValue(source));

    internal static string RecomputeRuntimeProfileClosureSha256(
        Id65AuthoringAggregateV1RuntimeEvidenceProfile profile) =>
        HashUtf8(SerializeCanonicalValue(profile));

    internal static string RecomputeToolSetClosureSha256(
        Id65AuthoringAggregateV1RuntimeEvidenceToolPin[] tools) =>
        HashUtf8(SerializeCanonicalValue(tools));

    internal static string RecomputeProductionCaptureBlockersSha256(
        Id65AuthoringAggregateV1RuntimeEvidenceProductionBlocker[] blockers) =>
        HashUtf8(SerializeCanonicalValue(blockers));

    private static void ValidateStaticPackage(
        Id65AuthoringAggregateV1RuntimeEvidencePackage package,
        Id65AuthoringAggregateV1RuntimeEvidenceValidationMode mode)
    {
        if (package.SourceIdentity is null || package.RuntimeProfile is null ||
            package.ToolPins is null || package.ArtifactCatalog is null ||
            package.EventualChecks is null || package.DerivedResult is null ||
            package.ScopeBoundary is null || package.Integrity is null ||
            package.ToolPins.Any(item => item is null) ||
            package.ArtifactCatalog.Any(item => item is null) ||
            package.EventualChecks.Any(item => item is null))
        {
            throw new InvalidDataException(
                "Aggregate runtime evidence root graph contains a missing object or array member.");
        }
        if (package.Format != Format || package.FormatVersion != FormatVersion ||
            package.EvidenceId != EvidenceId)
        {
            throw new InvalidDataException("Aggregate runtime evidence format identity changed.");
        }
        if (package.RecordedAtUtc is not null ||
            package.DeclaredState != Id65AuthoringAggregateV1RuntimeEvidenceState.PendingNoRun)
        {
            throw new InvalidDataException(
                "This aggregate V1 contract slice accepts only the canonical pending no-run state.");
        }

        Id65AuthoringAggregateV1RuntimeEvidenceSourceIdentity source =
            BuildExpectedSourceIdentity();
        if (SerializeCanonicalValue(package.SourceIdentity) != SerializeCanonicalValue(source) ||
            RecomputeSourceClosureSha256(package.SourceIdentity) != ExpectedSourceClosureSha256)
        {
            throw new InvalidDataException(
                "Pinned aggregate candidate, document, receipt, or source closure changed.");
        }

        Id65AuthoringAggregateV1RuntimeEvidenceProfile profile = BuildExpectedRuntimeProfile();
        if (SerializeCanonicalValue(package.RuntimeProfile) != SerializeCanonicalValue(profile) ||
            RecomputeRuntimeProfileClosureSha256(package.RuntimeProfile) !=
                ExpectedRuntimeProfileClosureSha256)
        {
            throw new InvalidDataException("Pinned aggregate runtime profile closure changed.");
        }

        Id65AuthoringAggregateV1RuntimeEvidenceToolPin[] tools = BuildExpectedToolPins();
        if (SerializeCanonicalValue(package.ToolPins) != SerializeCanonicalValue(tools) ||
            RecomputeToolSetClosureSha256(package.ToolPins) != ExpectedToolSetClosureSha256)
        {
            throw new InvalidDataException("Pinned aggregate runtime tool closure changed.");
        }
        if (package.ArtifactCatalog.Length != 0 ||
            SerializeCanonicalValue(package.EventualChecks) !=
                SerializeCanonicalValue(BuildExpectedChecks()) ||
            package.EventualChecks.Any(check =>
                check.Status != Id65AuthoringAggregateV1RuntimeEvidenceCheckStatus.NotObserved ||
                check.SessionId is not null || check.EvidenceArtifactIds.Length != 0))
        {
            throw new InvalidDataException(
                "Runtime observations are not accepted while the capture/evidence pins are PENDING.");
        }
        if (SerializeCanonicalValue(package.ScopeBoundary) !=
            SerializeCanonicalValue(BuildExpectedScopeBoundary()) ||
            SerializeCanonicalValue(package.DerivedResult) !=
            SerializeCanonicalValue(BuildExpectedDerivedResult()))
        {
            throw new InvalidDataException("Aggregate pending scope or derived result changed.");
        }
        if (mode == Id65AuthoringAggregateV1RuntimeEvidenceValidationMode.FullReadback &&
            package.DerivedResult.FullReadbackAccepted)
        {
            throw new InvalidDataException(
                "The pending aggregate V1 contract cannot accept a full-runtime readback.");
        }
    }

    private static void ValidateIntegrity(
        Id65AuthoringAggregateV1RuntimeEvidencePackage package)
    {
        Id65AuthoringAggregateV1RuntimeEvidenceIntegrity expected = ComputeIntegrity(package);
        if (SerializeCanonicalValue(package.Integrity) != SerializeCanonicalValue(expected))
            throw new InvalidDataException("Aggregate runtime evidence integrity closure changed.");
    }

    private static Id65AuthoringAggregateV1RuntimeEvidenceIntegrity ComputeIntegrity(
        Id65AuthoringAggregateV1RuntimeEvidencePackage package)
    {
        string source = RecomputeSourceClosureSha256(package.SourceIdentity);
        string profile = RecomputeRuntimeProfileClosureSha256(package.RuntimeProfile);
        string tools = RecomputeToolSetClosureSha256(package.ToolPins);
        string artifacts = HashUtf8(SerializeCanonicalValue(package.ArtifactCatalog));
        string payload = HashUtf8(SerializeCanonicalPayload(package));
        string contract = HashUtf8(
            "id65-authoring-aggregate-v1-runtime-evidence-contract-v1\n" +
            $"sourceClosureSha256={source}\n" +
            $"runtimeProfileClosureSha256={profile}\n" +
            $"toolSetClosureSha256={tools}\n" +
            $"artifactSetClosureSha256={artifacts}\n" +
            $"payloadSha256={payload}\n" +
            $"state={ToKebab(package.DeclaredState)}\n");
        return new(Canonicalization, source, profile, tools, artifacts, payload, contract);
    }

    private static string SerializeCanonicalPayload(
        Id65AuthoringAggregateV1RuntimeEvidencePackage package) =>
        SerializeCanonicalValue(new
        {
            package.Format,
            package.FormatVersion,
            package.EvidenceId,
            package.RecordedAtUtc,
            package.DeclaredState,
            package.SourceIdentity,
            package.RuntimeProfile,
            package.ToolPins,
            package.ArtifactCatalog,
            package.EventualChecks,
            package.DerivedResult,
            package.ScopeBoundary
        });

    private static Id65AuthoringAggregateV1RuntimeEvidenceSourceIdentity
        BuildExpectedSourceIdentity() => new(
            "id65-authoring-editor-aggregate-candidate-darwin-anchored-workspace-v1",
            CandidateDirectory,
            CandidateDirectory + "/" + CandidateStem + ".bin",
            661_547_040,
            "083dd5b7e258b59010e87c3262274e4da9cb86b1a6a15e7730a8b5eeac001647",
            CandidateDirectory + "/" + CandidateStem + ".cue",
            114,
            "91c12af236642fc8ad601995cea86d22c414eb1b692a4957cc3f9fa3aedd6de8",
            CandidateDirectory + "/" + CandidateStem + ".receipt.json",
            3_064,
            "9f11a3811a54ef4932c5cb4d184877fe957f06eb003680c739d2478c6e7ed03b",
            "d6c14861cde023e9e100fe2828227da8252c9de37c36e84d33ba4bfbd3b0516f",
            AuthoringDocumentRelativePath,
            3_931,
            "5669f5e29010009fa7a698f4eed213dbd7163bd3483ec37beba7732bda321b70",
            "8f27bfc3f97238402c0b7dd59a081335ae19df1acfaefa9575d9ca7110dce808",
            "9d853ccfcf18627a46dc730844efc9eeeda28ee9a0d53c70ae3b7144652b4c3b",
            "9e42b43bd1341b40915748432d1b2dc760e22a81c0a320ec09ae6a71ca2efcd8",
            "4955c54b3eb7ea5c1871205e559f5d5dda0766613e55aa0de988b92e896d5c21",
            "62635a61a79a59a59d052f50c3f719b56ea1bcdecf09ae91030063be410cfa34",
            "dad25853f92e4f8300036edd85dae374672d265ba17c7b2682c6b524661b75f3",
            "443bfdeb7727b2a1877eef60027cc1fdb9b1b1224cb1165c4a9e26b33b480211",
            558,
            1_075_276,
            "SCUS_942.28",
            55_382,
            417_792,
            "fa5fc7981188b78fa7d7b78facca64c1f79dadb107515e9146ad178ade39d442",
            RetailTownSquarePreserved: true,
            ProtectedComplementVerified: true,
            RuntimeCandidateAuthorized: false,
            RuntimeProven: false,
            CreateBinEnabled: false,
            ReleaseAuthorized: false,
            Publishable: false);

    private static Id65AuthoringAggregateV1RuntimeEvidenceProfile
        BuildExpectedRuntimeProfile() => new(
            ProfileId: ExpectedProfileId,
            PackageRelativePath: ProfilePackageRelativePath,
            AppRelativePath: AppRelativePath,
            ExecutableRelativePath: AppRelativePath + "/Contents/MacOS/DuckStation",
            ExternalHomeRelativePath: ExternalHomeRelativePath,
            ExternalHomeDataRelativePath: ExternalHomeDataRelativePath,
            DuckStationVersion: "0.1-11580-ge7f2f101c",
            ExecutableByteLength: 22_354_208,
            ExecutableSha256:
                "14c22c6e1f4bbff13c00c24f17424ef8cf7645ee9917f7d65b17cd3fe1dd1645",
            HandoffReceiptRelativePath: ProfilePackageRelativePath + "/handoff-receipt.json",
            HandoffReceiptFormat: "spyro-id65-unattended-external-home-handoff",
            HandoffReceiptFormatVersion: 1,
            HandoffReceiptByteLength: 74_525,
            HandoffReceiptSha256:
                "17ce1f4fd9394bf4890048f08afe28d97a709fd656e7d262e34553f5cd6e58d3",
            HandoffReceiptClosureSha256:
                ExpectedProfileReceiptClosureSha256,
            CandidateManifestEntryCount: 2,
            CandidateManifestSha256:
                "44f74dcefab535670d4abe0e1b1bcec35b832dec421d9caea8c6c132b860681d",
            AppContentManifestFormat: "spyro-id65-duckstation-app-content-manifest-v1",
            AppContentManifestEntryCount: 292,
            AppContentManifestSha256:
                "95a8142e561d6b37f228728abc8dbf034c774887213eb59bc14518d56c2d222e",
            OutputTreeManifestFormat: "spyro-id65-output-tree-manifest-v1",
            AppOutputTreeManifestEntryCount: 292,
            AppOutputTreeManifestSha256:
                "08d0a74450b709ab7e702182d64db55f6bc3395728181b052fec307c4d320ba7",
            HomeOutputTreeManifestEntryCount: 12,
            HomeOutputTreeManifestSha256:
                "5e3bcd2c9c3bd8b457bbfe613244663d545b90dbc072b132e1e4aee1e3e94cb9",
            PackageEnvelopeFormat: "spyro-id65-package-envelope-manifest-v1",
            PackageEnvelopeEntryCount: 4,
            PackageEnvelopeSha256:
                "7544e65f34c99d4d78677321ea38e992310386843ddf933f31fce514e2937872",
            OutputXattrPolicyVersion: "apple-provenance-11-prefix-0102-v1",
            ExternalHomeSettingsRelativePath: ExternalHomeDataRelativePath + "/settings.ini",
            ExternalHomeSettingsByteLength: 2_151,
            ExternalHomeSettingsSha256:
                "4ea5cced616c989e3df2be8a6ddb9507a9c295704c1acd2b586ec19fee28e8cb",
            ExternalHomeBiosRelativePath: ExternalHomeDataRelativePath +
                "/bios/Sony PlayStation SCPH-1001 - DTLH-3000 BIOS v2.2 (1995-12-04)(Sony)(US).bin",
            ExternalHomeBiosByteLength: 524_288,
            ExternalHomeBiosSha256:
                "71af94d1e47a68c11e8fdb9f8368040601514a42a5a399cda48c7d3bff1e99d3",
            HostNormalSettingsAbsolutePath: HostNormalSettingsAbsolutePath,
            HostNormalSettingsSentinelId: "duckstation-normal-user-settings.ini",
            HostNormalSettingsByteLength: 10_058,
            HostNormalSettingsSha256:
                "896daa612142659aa950c16d2435f81cb034aa8b56768b91e60a4ec8f0a5ccc9",
            HostNormalSettingsMode: "0600",
            LaunchGateStatus: "EXTERNAL_RUNTIME_LAUNCH_REQUIRED",
            BuilderLaunchedDuckStation: false,
            SaveLoadStateHotkeysPresent: false,
            TextureReplacementOrDumpEnabled: false,
            TrustModel: "cooperative-non-hostile-same-uid");

    private static Id65AuthoringAggregateV1RuntimeEvidenceToolPin[] BuildExpectedToolPins() =>
    [
        new(
            "profile-builder",
            "tools/Build-SpyroId65AuthoringAggregateSaveV1Profile.py",
            28_304,
            "759ec6a561166fc3e879db58db56781f757735b0975a5db0768248f276aec297",
            "0644"),
        new(
            "profile-adapter-test",
            "tools/Test-SpyroId65AuthoringAggregateSaveV1Profile.py",
            20_994,
            "6eb394181d66261fe7253836ccf866dcbc7b0b07d65f7713f70a11f02bd1e5c1",
            "0644"),
        new(
            "profile-verifier",
            "tools/Verify-SpyroId65AuthoringAggregateSaveV1Profile.py",
            25_873,
            "36913ca29f4d4bdbb2f7ae539df3f6033f5018f5a39a77affce2638c1ca4042b",
            "0644"),
        new(
            "runtime-watcher",
            "tools/Watch-SpyroId65RuntimeState.py",
            21_426,
            "eac3953a0ff7ff8acdefbe43cf987f296d44091ad51619efd225690ba1dc0677",
            "0644"),
        new(
            "frozen-capture-core",
            "tools/Capture-SpyroId65V3RuntimeSession.py",
            473_144,
            "153871778877a8736a1243ce2e84453e4d0d8e2fb945f9e96fdc01fbf00278ec",
            "0755"),
        new(
            "capture-coordinator",
            "tools/Capture-SpyroId65AuthoringAggregateExactV1RuntimeSession.py",
            166_832,
            ExpectedCaptureCoordinatorSha256,
            "0644"),
        new(
            "capture-deep-test",
            "tools/Test-Capture-SpyroId65AuthoringAggregateExactV1RuntimeSession.py",
            134_774,
            "ebad45c21b6c8b7e4c15998f4709256a52038a537a0dada9bdfa11081ce000b2",
            "0644")
    ];

    private static Id65AuthoringAggregateV1RuntimeEvidenceCheck[] BuildExpectedChecks() =>
    [
        PendingCheck("candidate.safe-load-and-spawn",
            "A fresh external-home launch safely loads ID65 and spawns Spyro on the authored pad."),
        PendingCheck("candidate.authored-terrain-visible",
            "The authored terrain tile is visible with the intended geometry."),
        PendingCheck("candidate.authored-collision-playable",
            "The authored collision supports stable traversal without fall-through."),
        PendingCheck("candidate.authored-occlusion-correct",
            "The authored occlusion group neither hides the pad nor reveals invalid geometry."),
        PendingCheck("candidate.private-texture-swap-visible",
            "The private destination texture swap is visibly applied to the authored face."),
        PendingCheck("candidate.grass-zero-egg-placement-present",
            "The row-replaced grass placement at true index 88 is present."),
        PendingCheck("candidate.grass-authored-placement-present",
            "The appended grass placement at true index 107 is present at its authored support."),
        PendingCheck("candidate.zero-egg-preserved",
            "Both grass placements preserve the intended zero-egg state."),
        PendingCheck("candidate.camera-360-stable",
            "A complete camera orbit remains stable around the authored area."),
        PendingCheck("candidate.walk-stable",
            "Walking the authored area remains stable in both directions."),
        PendingCheck("candidate.jump-land-stable",
            "Jumping and landing across the authored area remains stable."),
        PendingCheck("candidate.fall-respawn-stable",
            "An authorized edge fall respawns safely at the ID65 spawn without a crash or softlock."),
        PendingCheck("candidate.music-long-play-stable",
            "The authored music plays correctly through a sustained long-play observation."),
        PendingCheck("candidate.treasure-total-200",
            "The in-level and pause totals report exactly 200 treasure."),
        PendingCheck("candidate.exit-destination-65",
            "The level exit retains destination ID 65 and returns through the intended route."),
        PendingCheck("candidate.save-persistence",
            "An in-game save survives a clean process restart without save-state use."),
        PendingCheck("retail.town-square-preserved",
            "A fresh retail Town Square control run preserves terrain, collision, Mobys, totals, exit, music, and save behavior.")
    ];

    private static Id65AuthoringAggregateV1RuntimeEvidenceCheck PendingCheck(
        string id,
        string requirement) => new(
            id,
            requirement,
            Id65AuthoringAggregateV1RuntimeEvidenceCheckStatus.NotObserved,
            SessionId: null,
            EvidenceArtifactIds: []);

    private static Id65AuthoringAggregateV1RuntimeEvidenceDerivedResult
        BuildExpectedDerivedResult() => new(
            Id65AuthoringAggregateV1RuntimeEvidenceState.PendingNoRun,
            SourceClosureValid: true,
            RuntimeProfileClosureValid: true,
            ToolPinsFinalized: true,
            RuntimeEvidencePinned: false,
            AllEventualChecksPassed: false,
            FullReadbackAccepted: false,
            RuntimeProven: false,
            CreateBinAuthorized: false,
            ReleaseAuthorized: false,
            Publishable: false);

    private static Id65AuthoringAggregateV1RuntimeEvidenceScopeBoundary
        BuildExpectedScopeBoundary() => new(
            FocusedRuntimeEvidenceRelativePath,
            RuntimeAssetRelativePrefix,
            CaptureStagingRelativePath,
            "docs/runtime-evidence/assets/" +
                "unused-level-65-remote-blank-environment-header-repair-v3/" +
                ".capture-coordinator-authority.lock",
            ExpectedAggregateChecklistSha256,
            ExpectedExactPlanSha256,
            ExpectedOperationalToolPinsSha256,
            ExpectedCaptureRuntimeProfileSha256,
            ExpectedProfileReceiptClosureSha256,
            ExpectedCaptureSchemaClosureSha256,
            ExpectedCaptureSourceClosureSha256,
            ExpectedWorkflowDeclarationClosureSha256,
            SaveCapableProfilePinned: true,
            ProfilePinsVerified: false,
            ExactCaptureEngineImplemented: false,
            ProductionCaptureEnabled: false,
            ProductionCaptureBlockers: BuildExpectedProductionCaptureBlockers(),
            ProductionCaptureBlockersSha256: ExpectedProductionCaptureBlockersSha256,
            ProductionCaptureBlockingCheckId: "EXACT_CAPTURE_ENGINE_NOT_IMPLEMENTED",
            ProductionCaptureRequiredGate:
                "aggregate.exact-three-session-card-aware-capture-engine",
            ProductionCaptureBlockingReason:
                "the exact plan/card schema exists, but no live engine, DuckStation launch, " +
                "persistent staging, runtime evidence, or authorization is available",
            FrozenWorkflowCompatibleWithAggregateScope: false,
            FrozenWorkflowSha256: ExpectedFrozenWorkflowSha256,
            CandidateOnly: true,
            RetailTownSquareControlRequired: true,
            RuntimeObserved: false,
            EditorIntegrated: false,
            CreateBinAuthorized: false,
            NormalCreateBinEnabled: false,
            ReleaseAuthorized: false,
            PromotionAuthorized: false,
            Publishable: false);

    private static Id65AuthoringAggregateV1RuntimeEvidenceProductionBlocker[]
        BuildExpectedProductionCaptureBlockers() =>
    [
        new(
            "EXACT_CAPTURE_ENGINE_NOT_IMPLEMENTED",
            "aggregate.exact-three-session-card-aware-capture-engine",
            "the exact plan/card schema exists, but no live engine, DuckStation launch, " +
                "persistent staging, runtime evidence, or authorization is available")
    ];

    private static void RejectDuplicateProperties(
        JsonElement element,
        string path,
        List<string> errors)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                    errors.Add($"Duplicate JSON property at {path}.{property.Name}.");
                RejectDuplicateProperties(property.Value, path + "." + property.Name, errors);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            int index = 0;
            foreach (JsonElement item in element.EnumerateArray())
                RejectDuplicateProperties(item, $"{path}[{index++}]", errors);
        }
    }

    private static string SerializeCanonicalValue<T>(T value)
    {
        JsonElement element = JsonSerializer.SerializeToElement(value, JsonOptions);
        StringBuilder builder = new();
        WriteJcsElement(builder, element);
        return builder.ToString();
    }

    private static void WriteJcsElement(StringBuilder builder, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                builder.Append('{');
                bool firstProperty = true;
                foreach (JsonProperty property in element.EnumerateObject()
                             .OrderBy(item => item.Name, StringComparer.Ordinal))
                {
                    if (!firstProperty)
                        builder.Append(',');
                    firstProperty = false;
                    WriteJcsString(builder, property.Name);
                    builder.Append(':');
                    WriteJcsElement(builder, property.Value);
                }
                builder.Append('}');
                break;
            case JsonValueKind.Array:
                builder.Append('[');
                bool firstItem = true;
                foreach (JsonElement item in element.EnumerateArray())
                {
                    if (!firstItem)
                        builder.Append(',');
                    firstItem = false;
                    WriteJcsElement(builder, item);
                }
                builder.Append(']');
                break;
            case JsonValueKind.String:
                WriteJcsString(builder, element.GetString() ?? string.Empty);
                break;
            case JsonValueKind.Number:
                if (element.TryGetInt64(out long signed) &&
                    signed is >= -MaximumJcsSafeInteger and <= MaximumJcsSafeInteger)
                {
                    builder.Append(signed.ToString(CultureInfo.InvariantCulture));
                }
                else if (element.TryGetUInt64(out ulong unsigned) &&
                         unsigned <= (ulong)MaximumJcsSafeInteger)
                {
                    builder.Append(unsigned.ToString(CultureInfo.InvariantCulture));
                }
                else
                {
                    throw new InvalidDataException(
                        "Aggregate runtime evidence permits only exact I-JSON safe integers.");
                }
                break;
            case JsonValueKind.True:
                builder.Append("true");
                break;
            case JsonValueKind.False:
                builder.Append("false");
                break;
            case JsonValueKind.Null:
                builder.Append("null");
                break;
            default:
                throw new InvalidDataException(
                    $"Unsupported JSON kind {element.ValueKind} in aggregate evidence.");
        }
    }

    private static void WriteJcsString(StringBuilder builder, string value)
    {
        ValidateIJsonString(value);
        builder.Append('"');
        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if (char.IsHighSurrogate(character))
            {
                builder.Append(character).Append(value[++index]);
                continue;
            }
            switch (character)
            {
                case '"': builder.Append("\\\""); break;
                case '\\': builder.Append("\\\\"); break;
                case '\b': builder.Append("\\b"); break;
                case '\t': builder.Append("\\t"); break;
                case '\n': builder.Append("\\n"); break;
                case '\f': builder.Append("\\f"); break;
                case '\r': builder.Append("\\r"); break;
                default:
                    if (character <= 0x1F)
                    {
                        builder.Append("\\u")
                            .Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(character);
                    }
                    break;
            }
        }
        builder.Append('"');
    }

    private static void ValidateIJsonString(string value)
    {
        try
        {
            _ = StrictUtf8.GetByteCount(value);
        }
        catch (EncoderFallbackException exception)
        {
            throw new InvalidDataException(
                "Aggregate runtime evidence contains an unpaired UTF-16 surrogate.", exception);
        }
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        JsonSerializerOptions options = new()
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            WriteIndented = false,
            NumberHandling = JsonNumberHandling.Strict,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            RespectRequiredConstructorParameters = true,
            MaxDepth = 64
        };
        options.Converters.Add(new StrictIJsonStringConverter());
        options.Converters.Add(new JsonStringEnumConverter(
            JsonNamingPolicy.KebabCaseLower,
            allowIntegerValues: false));
        return options;
    }

    private sealed class StrictIJsonStringConverter : JsonConverter<string>
    {
        public override string? Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
                throw new JsonException("Aggregate evidence string member has a non-string token.");
            string? value = reader.GetString();
            if (value is not null)
                ValidateIJsonString(value);
            return value;
        }

        public override void Write(
            Utf8JsonWriter writer,
            string value,
            JsonSerializerOptions options)
        {
            ValidateIJsonString(value);
            writer.WriteStringValue(value);
        }
    }

    private static string HashUtf8(string value) =>
        Convert.ToHexString(SHA256.HashData(StrictUtf8.GetBytes(value))).ToLowerInvariant();

    private static string ToKebab(Id65AuthoringAggregateV1RuntimeEvidenceState state) =>
        state switch
        {
            Id65AuthoringAggregateV1RuntimeEvidenceState.PendingNoRun => "pending-no-run",
            Id65AuthoringAggregateV1RuntimeEvidenceState.PendingIncomplete => "pending-incomplete",
            Id65AuthoringAggregateV1RuntimeEvidenceState.RuntimeRejected => "runtime-rejected",
            Id65AuthoringAggregateV1RuntimeEvidenceState.Invalid => "invalid",
            Id65AuthoringAggregateV1RuntimeEvidenceState.FocusedRuntimePass =>
                "focused-runtime-pass",
            _ => throw new ArgumentOutOfRangeException(nameof(state))
        };
}
