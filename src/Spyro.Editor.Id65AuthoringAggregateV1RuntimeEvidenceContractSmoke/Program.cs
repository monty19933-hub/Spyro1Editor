using System.Text;
using Spyro.Editor.Core.Exporting;

Id65AuthoringAggregateV1RuntimeEvidencePackage pending =
    Id65AuthoringAggregateV1RuntimeEvidenceContract.CreatePending();

if (args.Contains("--print-closures", StringComparer.Ordinal))
{
    Console.WriteLine(
        Id65AuthoringAggregateV1RuntimeEvidenceContract.RecomputeSourceClosureSha256(
            pending.SourceIdentity));
    Console.WriteLine(
        Id65AuthoringAggregateV1RuntimeEvidenceContract.RecomputeRuntimeProfileClosureSha256(
            pending.RuntimeProfile));
    Console.WriteLine(
        Id65AuthoringAggregateV1RuntimeEvidenceContract.RecomputeToolSetClosureSha256(
            pending.ToolPins));
    return;
}

byte[] canonical =
    Id65AuthoringAggregateV1RuntimeEvidenceContract.SerializeCanonicalUtf8(pending);
Id65AuthoringAggregateV1RuntimeEvidenceValidation validation =
    Id65AuthoringAggregateV1RuntimeEvidenceContract.ValidateCanonicalJson(canonical);
Require(validation.StructurallyValid, "canonical pending evidence must validate");
Require(validation.State == Id65AuthoringAggregateV1RuntimeEvidenceState.PendingNoRun,
    "canonical pending evidence must stay pending-no-run");
Require(validation.Package is not null, "canonical pending evidence must deserialize");
Require(validation.CanonicalJson == Encoding.UTF8.GetString(canonical),
    "canonical validator must return exact canonical JSON");

Id65AuthoringAggregateV1RuntimeEvidenceValidation fullReadback =
    Id65AuthoringAggregateV1RuntimeEvidenceContract.ValidateCanonicalJson(
        canonical,
        Id65AuthoringAggregateV1RuntimeEvidenceValidationMode.FullReadback);
Require(fullReadback.StructurallyValid, "pending package must remain statically valid in full mode");
Require(fullReadback.State == Id65AuthoringAggregateV1RuntimeEvidenceState.PendingNoRun,
    "full mode must not promote pending evidence");
Require(fullReadback.Package!.DerivedResult is
    {
        FullReadbackAccepted: false,
        RuntimeProven: false,
        CreateBinAuthorized: false,
        ReleaseAuthorized: false,
        Publishable: false
    }, "pending derived gates must stay false");
Require(pending.SourceIdentity is
    {
        RuntimeCandidateAuthorized: false,
        RuntimeProven: false,
        CreateBinEnabled: false,
        ReleaseAuthorized: false,
        Publishable: false
    }, "pending source authorization gates must stay false");
Require(pending.RuntimeProfile is
    {
        ProfileId: "duckstation-id65-authoring-aggregate-save-v1",
        HandoffReceiptByteLength: 74_525,
        HandoffReceiptSha256:
            "17ce1f4fd9394bf4890048f08afe28d97a709fd656e7d262e34553f5cd6e58d3",
        HandoffReceiptClosureSha256:
            "7b43c1c81e4ca40148c99c2c0d908891453e2882c9f93b0d0f9d440c52cb5330",
        ExternalHomeSettingsByteLength: 2_151,
        ExternalHomeSettingsSha256:
            "4ea5cced616c989e3df2be8a6ddb9507a9c295704c1acd2b586ec19fee28e8cb",
        ExternalHomeBiosByteLength: 524_288,
        ExternalHomeBiosSha256:
            "71af94d1e47a68c11e8fdb9f8368040601514a42a5a399cda48c7d3bff1e99d3",
        HostNormalSettingsSentinelId: "duckstation-normal-user-settings.ini",
        HostNormalSettingsByteLength: 10_058,
        HostNormalSettingsSha256:
            "896daa612142659aa950c16d2435f81cb034aa8b56768b91e60a4ec8f0a5ccc9",
        HostNormalSettingsMode: "0600",
        BuilderLaunchedDuckStation: false,
        SaveLoadStateHotkeysPresent: false,
        TextureReplacementOrDumpEnabled: false
    }, "runtime profile identity, isolation sentinels, and launch gates changed");
Require(pending.RuntimeProfile.HandoffReceiptFormat ==
        "spyro-id65-unattended-external-home-handoff" &&
    pending.RuntimeProfile.HandoffReceiptFormatVersion == 1 &&
    pending.RuntimeProfile.AppContentManifestFormat ==
        "spyro-id65-duckstation-app-content-manifest-v1" &&
    pending.RuntimeProfile.OutputTreeManifestFormat ==
        "spyro-id65-output-tree-manifest-v1" &&
    pending.RuntimeProfile.PackageEnvelopeFormat ==
        "spyro-id65-package-envelope-manifest-v1" &&
    pending.RuntimeProfile.OutputXattrPolicyVersion ==
        "apple-provenance-11-prefix-0102-v1",
    "runtime profile manifest formats or xattr policy changed");
Require(pending.ScopeBoundary is
    {
        FocusedEvidenceRelativePath:
            "docs/runtime-evidence/unused-level-65-authoring-aggregate-exact-v1-runtime-evidence.json",
        RuntimeAssetRelativePrefix:
            "docs/runtime-evidence/assets/unused-level-65-authoring-aggregate-exact-v1/",
        CaptureStagingRelativePath:
            "docs/runtime-evidence/assets/unused-level-65-authoring-aggregate-exact-v1/capture-staging",
        AggregateChecklistSha256:
            "3fee9f6a9b8aa5e90a846834b29c4d1b3f17683d19f2b9804126d17527eefeef",
        ExactPlanSha256:
            "23838ecb8f05c62f2d4d2df243fd37553eb5f36579e95d137ac7beb6209c1543",
        OperationalToolPinsSha256:
            "ac30470f7741d0273a9d78a63f25fafb0709ce74f430c85256b83173ac7716c7",
        CaptureRuntimeProfileSha256:
            "419b355557a69683e33e254d8609d4dd285ebfeb0529a602b2a22918a8b010f6",
        ProfileReceiptClosureSha256:
            "7b43c1c81e4ca40148c99c2c0d908891453e2882c9f93b0d0f9d440c52cb5330",
        CaptureSchemaClosureSha256:
            "3a30359f741bf31d9be46a168662b4d0cf2f38bbbe86806e304a0744808f01f4",
        CaptureSourceClosureSha256:
            "fa601b2d8aad8a8931ce49e8be9269afdc3bb3105e927d65c70c138e9a832a41",
        WorkflowDeclarationClosureSha256:
            "b086b7336c975a59b24cde401d4b5080b260ad9da563c12f2d33518d7096a9c3",
        SaveCapableProfilePinned: true,
        ProfilePinsVerified: false,
        ExactCaptureEngineImplemented: false,
        ProductionCaptureEnabled: false,
        ProductionCaptureBlockingCheckId: "EXACT_CAPTURE_ENGINE_NOT_IMPLEMENTED",
        ProductionCaptureRequiredGate:
            "aggregate.exact-three-session-card-aware-capture-engine",
        ProductionCaptureBlockersSha256:
            "9069369b35c43da4f6002477b094a8aa5e2be7e0076530c718366ecc95349769",
        FrozenWorkflowCompatibleWithAggregateScope: false,
        FrozenWorkflowSha256:
            "6ae9d047746dba64e0156de1ef547a4c8454654f8aec9f7cd32b0598d6955a69",
        RuntimeObserved: false,
        EditorIntegrated: false,
        CreateBinAuthorized: false,
        NormalCreateBinEnabled: false,
        ReleaseAuthorized: false,
        PromotionAuthorized: false,
        Publishable: false
    }, "pending scope authorization gates must stay false");
Require(pending.ScopeBoundary.ProductionCaptureBlockingReason ==
    "the exact plan/card schema exists, but no live engine, DuckStation launch, " +
    "persistent staging, runtime evidence, or authorization is available",
    "production capture blocker changed");
Require(pending.ScopeBoundary.ProductionCaptureBlockers is
    [
        {
            BlockerId: "EXACT_CAPTURE_ENGINE_NOT_IMPLEMENTED",
            RequiredGate: "aggregate.exact-three-session-card-aware-capture-engine"
        }
    ], "the exact-engine production blocker changed");
Require(Id65AuthoringAggregateV1RuntimeEvidenceContract
        .RecomputeProductionCaptureBlockersSha256(
            pending.ScopeBoundary.ProductionCaptureBlockers) ==
    Id65AuthoringAggregateV1RuntimeEvidenceContract
        .ExpectedProductionCaptureBlockersSha256,
    "production capture blocker closure changed");

RequireThrows<InvalidOperationException>(() =>
    Id65AuthoringAggregateV1RuntimeEvidenceContract.RequireFocusedRuntimePass(fullReadback),
    "PENDING capture/evidence pins must forbid a runtime-pass capability");

string json = Encoding.UTF8.GetString(canonical);
RequireRejected(
    Encoding.UTF8.GetBytes(json.Replace(
        "{",
        "{\"format\":\"spyro-editor-id65-authoring-aggregate-v1-runtime-evidence\",",
        StringComparison.Ordinal)),
    "duplicate properties must be rejected");
RequireRejected(
    Encoding.UTF8.GetBytes(json.Replace(
        "\"formatVersion\":1",
        "\"formatVersion\":NaN",
        StringComparison.Ordinal)),
    "non-finite numbers must be rejected");
RequireRejected(
    Encoding.UTF8.GetBytes(json.Replace(
        "\"formatVersion\":1",
        "\"formatVersion\":1,\"unknownSchemaMember\":0",
        StringComparison.Ordinal)),
    "unknown schema members must be rejected");
RequireRejected(
    Encoding.UTF8.GetBytes(" \n" + json),
    "non-canonical whitespace must be rejected");
RequireRejected(
    [0x7B, 0x22, 0x78, 0x22, 0x3A, 0x22, 0xC3, 0x28, 0x22, 0x7D],
    "invalid UTF-8 must be rejected");
RequireRejected(
    [0xEF, 0xBB, 0xBF, .. canonical],
    "a UTF-8 BOM must be rejected");

Id65AuthoringAggregateV1RuntimeEvidencePackage sourceTampered = pending with
{
    SourceIdentity = pending.SourceIdentity with { ChangedRawSectorCount = 559 }
};
RequireRejected(
    Id65AuthoringAggregateV1RuntimeEvidenceContract.SerializeCanonicalUtf8(
        Id65AuthoringAggregateV1RuntimeEvidenceContract.WithRecomputedIntegrity(sourceTampered)),
    "source pin and closure tampering must be rejected even after integrity recomputation");

Id65AuthoringAggregateV1RuntimeEvidencePackage profileTampered = pending with
{
    RuntimeProfile = pending.RuntimeProfile with
    {
        HostNormalSettingsSha256 = new string('1', 64)
    }
};
RequireRejected(
    Id65AuthoringAggregateV1RuntimeEvidenceContract.SerializeCanonicalUtf8(
        Id65AuthoringAggregateV1RuntimeEvidenceContract.WithRecomputedIntegrity(profileTampered)),
    "runtime profile pin and closure tampering must be rejected after integrity recomputation");

Id65AuthoringAggregateV1RuntimeEvidencePackage exactPlanTampered = pending with
{
    ScopeBoundary = pending.ScopeBoundary with { ExactPlanSha256 = new string('3', 64) }
};
RequireRejected(
    Id65AuthoringAggregateV1RuntimeEvidenceContract.SerializeCanonicalUtf8(
        Id65AuthoringAggregateV1RuntimeEvidenceContract.WithRecomputedIntegrity(
            exactPlanTampered)),
    "exact plan pin tampering must be rejected after integrity recomputation");

Id65AuthoringAggregateV1RuntimeEvidencePackage exactSchemaTampered = pending with
{
    ScopeBoundary = pending.ScopeBoundary with
    {
        CaptureSchemaClosureSha256 = new string('4', 64)
    }
};
RequireRejected(
    Id65AuthoringAggregateV1RuntimeEvidenceContract.SerializeCanonicalUtf8(
        Id65AuthoringAggregateV1RuntimeEvidenceContract.WithRecomputedIntegrity(
            exactSchemaTampered)),
    "exact schema pin tampering must be rejected after integrity recomputation");

Id65AuthoringAggregateV1RuntimeEvidencePackage exactProfilePinTampered = pending with
{
    ScopeBoundary = pending.ScopeBoundary with
    {
        CaptureRuntimeProfileSha256 = new string('5', 64)
    }
};
RequireRejected(
    Id65AuthoringAggregateV1RuntimeEvidenceContract.SerializeCanonicalUtf8(
        Id65AuthoringAggregateV1RuntimeEvidenceContract.WithRecomputedIntegrity(
            exactProfilePinTampered)),
    "exact profile pin-set tampering must be rejected after integrity recomputation");

Id65AuthoringAggregateV1RuntimeEvidenceToolPin[] toolTampered = pending.ToolPins.ToArray();
toolTampered[0] = toolTampered[0] with { ByteLength = toolTampered[0].ByteLength + 1 };
Id65AuthoringAggregateV1RuntimeEvidencePackage toolPackageTampered = pending with
{
    ToolPins = toolTampered
};
RequireRejected(
    Id65AuthoringAggregateV1RuntimeEvidenceContract.SerializeCanonicalUtf8(
        Id65AuthoringAggregateV1RuntimeEvidenceContract.WithRecomputedIntegrity(
            toolPackageTampered)),
    "tool pin and closure tampering must be rejected after integrity recomputation");

Id65AuthoringAggregateV1RuntimeEvidencePackage derivedGateTampered = pending with
{
    DerivedResult = pending.DerivedResult with { CreateBinAuthorized = true }
};
RequireRejected(
    Id65AuthoringAggregateV1RuntimeEvidenceContract.SerializeCanonicalUtf8(
        Id65AuthoringAggregateV1RuntimeEvidenceContract.WithRecomputedIntegrity(
            derivedGateTampered)),
    "a forged pending derived authorization must be rejected after integrity recomputation");

Id65AuthoringAggregateV1RuntimeEvidencePackage scopeGateTampered = pending with
{
    ScopeBoundary = pending.ScopeBoundary with
    {
        ProfilePinsVerified = true,
        ExactCaptureEngineImplemented = true,
        ProductionCaptureEnabled = true,
        PromotionAuthorized = true
    }
};
RequireRejected(
    Id65AuthoringAggregateV1RuntimeEvidenceContract.SerializeCanonicalUtf8(
        Id65AuthoringAggregateV1RuntimeEvidenceContract.WithRecomputedIntegrity(
            scopeGateTampered)),
    "a forged pending scope authorization must be rejected after integrity recomputation");

Id65AuthoringAggregateV1RuntimeEvidenceProductionBlocker[] blockerTampered =
    pending.ScopeBoundary.ProductionCaptureBlockers.ToArray();
blockerTampered[0] = blockerTampered[0] with { RequiredGate = "forged-workflow-gate" };
Id65AuthoringAggregateV1RuntimeEvidencePackage blockerPackageTampered = pending with
{
    ScopeBoundary = pending.ScopeBoundary with
    {
        ProductionCaptureBlockers = blockerTampered,
        ProductionCaptureBlockersSha256 =
            Id65AuthoringAggregateV1RuntimeEvidenceContract
                .RecomputeProductionCaptureBlockersSha256(blockerTampered)
    }
};
RequireRejected(
    Id65AuthoringAggregateV1RuntimeEvidenceContract.SerializeCanonicalUtf8(
        Id65AuthoringAggregateV1RuntimeEvidenceContract.WithRecomputedIntegrity(
            blockerPackageTampered)),
    "a forged production blocker must be rejected after closure recomputation");

Id65AuthoringAggregateV1RuntimeEvidencePackage integrityTampered = pending with
{
    Integrity = pending.Integrity with { PayloadSha256 = new string('0', 64) }
};
RequireRejected(
    Id65AuthoringAggregateV1RuntimeEvidenceContract.SerializeCanonicalUtf8(integrityTampered),
    "integrity tampering must be rejected");

Id65AuthoringAggregateV1RuntimeEvidenceCheck[] observedChecks =
    pending.EventualChecks.Select(check => check with
    {
        Status = Id65AuthoringAggregateV1RuntimeEvidenceCheckStatus.Passed,
        SessionId = "forbidden-unpinned-run",
        EvidenceArtifactIds = ["forged-runtime-artifact"]
    }).ToArray();
Id65AuthoringAggregateV1RuntimeEvidencePackage observed = pending with
{
    RecordedAtUtc = "2026-08-25T00:00:00.000Z",
    DeclaredState = Id65AuthoringAggregateV1RuntimeEvidenceState.FocusedRuntimePass,
    ArtifactCatalog =
    [
        new(
            "forged-runtime-artifact",
            "video",
            Id65AuthoringAggregateV1RuntimeEvidenceContract.RuntimeAssetRelativePrefix +
                "forged-runtime-artifact.mp4",
            1,
            new string('2', 64),
            "2026-08-25T00:00:00.000Z")
    ],
    EventualChecks = observedChecks,
    DerivedResult = pending.DerivedResult with
    {
        State = Id65AuthoringAggregateV1RuntimeEvidenceState.FocusedRuntimePass,
        ToolPinsFinalized = true,
        RuntimeEvidencePinned = true,
        AllEventualChecksPassed = true,
        FullReadbackAccepted = true,
        RuntimeProven = true,
        CreateBinAuthorized = true,
        ReleaseAuthorized = true,
        Publishable = true
    },
    ScopeBoundary = pending.ScopeBoundary with
    {
        SaveCapableProfilePinned = true,
        ProfilePinsVerified = true,
        ExactCaptureEngineImplemented = true,
        ProductionCaptureEnabled = true,
        RuntimeObserved = true,
        EditorIntegrated = true,
        CreateBinAuthorized = true,
        NormalCreateBinEnabled = true,
        ReleaseAuthorized = true,
        PromotionAuthorized = true,
        Publishable = true
    }
};
RequireRejected(
    Id65AuthoringAggregateV1RuntimeEvidenceContract.SerializeCanonicalUtf8(
        Id65AuthoringAggregateV1RuntimeEvidenceContract.WithRecomputedIntegrity(observed)),
    "a fully forged runtime pass must be rejected by the pending/static slice");

string[] expectedCheckIds =
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
Require(pending.EventualChecks.Select(check => check.CheckId).SequenceEqual(expectedCheckIds),
    "eventual runtime checklist identity changed");
Require(pending.ToolPins.Single(pin => pin.Role == "capture-coordinator") is
    {
        ByteLength: 104_888,
        Sha256: "de7ca2b3886fc897f876fb9f53727c19596a188d8882979f60cce16fd3e63c2c",
        Mode: "0644"
    }, "capture coordinator pin must stay finalized");
Require(pending.ToolPins.Single(pin => pin.Role == "frozen-capture-core") is
    {
        ByteLength: 473_144,
        Sha256: "153871778877a8736a1243ce2e84453e4d0d8e2fb945f9e96fdc01fbf00278ec",
        Mode: "0755"
    }, "frozen capture core pin must stay finalized");
Require(pending.ToolPins.Single(pin => pin.Role == "capture-deep-test") is
    {
        ByteLength: 77_990,
        Sha256: "10104ab431260fafed5d215d5c16325c6f5792848dbfcb69c77b98cb4cdbfb72",
        Mode: "0644"
    }, "capture deep-test pin must stay finalized");
Require(pending.DerivedResult.ToolPinsFinalized,
    "the frozen aggregate tool set must remain finalized");
Require(Id65AuthoringAggregateV1RuntimeEvidenceContract.ExpectedFocusedRuntimeEvidenceSha256 ==
    "PENDING", "focused runtime evidence must remain explicitly PENDING");

Console.WriteLine(
    "ID65_AUTHORING_AGGREGATE_V1_RUNTIME_EVIDENCE_CONTRACT_SMOKE_OK " +
    $"checks={pending.EventualChecks.Length} state=pending-no-run " +
    $"source={pending.Integrity.SourceClosureSha256} " +
    $"profile={pending.Integrity.RuntimeProfileClosureSha256} " +
    $"tools={pending.Integrity.ToolSetClosureSha256} " +
    $"payload={pending.Integrity.PayloadSha256} " +
    $"contract={pending.Integrity.ContractSha256}");

static void RequireRejected(byte[] json, string message)
{
    Id65AuthoringAggregateV1RuntimeEvidenceValidation rejected =
        Id65AuthoringAggregateV1RuntimeEvidenceContract.ValidateCanonicalJson(json);
    Require(!rejected.StructurallyValid &&
        rejected.State == Id65AuthoringAggregateV1RuntimeEvidenceState.Invalid,
        message);
}

static void RequireThrows<TException>(Action action, string message)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }
    throw new InvalidOperationException(message);
}

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
