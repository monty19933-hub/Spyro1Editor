#!/usr/bin/env python3
"""Offline contract for the exact three-session ID65 aggregate capture.

This file deliberately does not implement or enable live capture.  It freezes
the shorter three-session plan, the exact 17-check binding, the isolated
save-card chain, and an authority-before-staging lease so those boundaries can
be adversarially tested before any runtime engine is added.  It never launches,
resumes, controls, or quits DuckStation and never imports the old aggregate V1
wrapper, its high-level frozen CaptureCoordinator, or input automation.
"""

from __future__ import annotations

import argparse
import dataclasses
import datetime
import fcntl
import hashlib
import json
import os
import pathlib
import re
import stat
import sys
import tempfile
from collections.abc import Mapping, Sequence
from typing import Any, Final


class ExactCaptureRefusal(RuntimeError):
    """The exact aggregate capture boundary was not satisfied."""


SCRIPT_PATH: Final = pathlib.Path(os.path.abspath(__file__))
PRODUCTION_REPOSITORY_ROOT: Final = SCRIPT_PATH.parent.parent

PROFILE_ID: Final = "duckstation-id65-authoring-aggregate-save-v1"
PROFILE_RELATIVE_PATH: Final = pathlib.PurePosixPath(
    "_local/v5-stone-hill-level-replacement/"
    "duckstation-id65-authoring-aggregate-save-v1"
)
PROFILE_RECEIPT_RELATIVE_PATH: Final = (
    PROFILE_RELATIVE_PATH / "handoff-receipt.json"
)
PROFILE_SETTINGS_RELATIVE_PATH: Final = PROFILE_RELATIVE_PATH / (
    "home/Library/Application Support/DuckStation/settings.ini"
)
PROFILE_MEMCARDS_RELATIVE_PATH: Final = PROFILE_RELATIVE_PATH / (
    "home/Library/Application Support/DuckStation/memcards"
)
EXPECTED_PROFILE_RECEIPT_SIZE: Final = 74_525
EXPECTED_PROFILE_RECEIPT_SHA256: Final = (
    "17ce1f4fd9394bf4890048f08afe28d97a709fd656e7d262e34553f5cd6e58d3"
)
EXPECTED_PROFILE_RECEIPT_CLOSURE_SHA256: Final = (
    "7b43c1c81e4ca40148c99c2c0d908891453e2882c9f93b0d0f9d440c52cb5330"
)
EXPECTED_SETTINGS_SIZE: Final = 2_151
EXPECTED_SETTINGS_SHA256: Final = (
    "4ea5cced616c989e3df2be8a6ddb9507a9c295704c1acd2b586ec19fee28e8cb"
)
EXPECTED_APP_CONTENT_MANIFEST_SHA256: Final = (
    "95a8142e561d6b37f228728abc8dbf034c774887213eb59bc14518d56c2d222e"
)
EXPECTED_APP_OUTPUT_TREE_MANIFEST_SHA256: Final = (
    "08d0a74450b709ab7e702182d64db55f6bc3395728181b052fec307c4d320ba7"
)
EXPECTED_HOME_BASELINE_TREE_MANIFEST_SHA256: Final = (
    "5e3bcd2c9c3bd8b457bbfe613244663d545b90dbc072b132e1e4aee1e3e94cb9"
)
EXPECTED_PACKAGE_ENVELOPE_SHA256: Final = (
    "7544e65f34c99d4d78677321ea38e992310386843ddf933f31fce514e2937872"
)
EXPECTED_EXECUTABLE_SHA256: Final = (
    "14c22c6e1f4bbff13c00c24f17424ef8cf7645ee9917f7d65b17cd3fe1dd1645"
)
EXPECTED_APPLICATION_RELATIVE_PATH: Final = (
    "DuckStation-ID65-Unattended-v1.app"
)
EXPECTED_EXECUTABLE_RELATIVE_PATH: Final = "Contents/MacOS/DuckStation"
EXPECTED_EXECUTABLE_PROFILE_RELATIVE_PATH: Final = (
    f"{EXPECTED_APPLICATION_RELATIVE_PATH}/{EXPECTED_EXECUTABLE_RELATIVE_PATH}"
)
EXPECTED_BIOS_SHA256: Final = (
    "71af94d1e47a68c11e8fdb9f8368040601514a42a5a399cda48c7d3bff1e99d3"
)
EXPECTED_CANDIDATE_CUE_SHA256: Final = (
    "91c12af236642fc8ad601995cea86d22c414eb1b692a4957cc3f9fa3aedd6de8"
)
EXPECTED_CANDIDATE_BIN_SHA256: Final = (
    "083dd5b7e258b59010e87c3262274e4da9cb86b1a6a15e7730a8b5eeac001647"
)
HOST_SETTINGS_SENTINEL_PATH: Final = pathlib.Path(
    "/Users/john/Library/Application Support/DuckStation/settings.ini"
)
EXPECTED_HOST_SETTINGS_SIZE: Final = 10_058
EXPECTED_HOST_SETTINGS_SHA256: Final = (
    "896daa612142659aa950c16d2435f81cb034aa8b56768b91e60a4ec8f0a5ccc9"
)

GLOBAL_AUTHORITY_DIRECTORY: Final = pathlib.PurePosixPath(
    "docs/runtime-evidence/assets/"
    "unused-level-65-remote-blank-environment-header-repair-v3"
)
GLOBAL_AUTHORITY_LOCK_LEAF: Final = ".capture-coordinator-authority.lock"
EXACT_STAGING_PREFIX: Final = pathlib.PurePosixPath(
    "docs/runtime-evidence/assets/"
    "unused-level-65-authoring-aggregate-exact-v1/capture-staging"
)
EXACT_STAGING_LOCK_LEAF: Final = ".capture-coordinator.lock"
FOCUSED_EVIDENCE_RELATIVE_PATH: Final = pathlib.PurePosixPath(
    "docs/runtime-evidence/"
    "unused-level-65-authoring-aggregate-exact-v1-runtime-evidence.json"
)

CARD_SNAPSHOT_FORMAT: Final = (
    "spyro-id65-authoring-aggregate-save-card-snapshot"
)
CARD_SNAPSHOT_FORMAT_VERSION: Final = 1
CARD_LEAF: Final = "Spyro the Dragon (USA)_1.mcd"
CARD_SIZE: Final = 131_072
CARD_MODE: Final = 0o600
MEMCARDS_MODE: Final = 0o700
CARD_LEAF_SIZE_PROVENANCE: Final = (
    "observed-host-duckstation-per-game-title-convention"
)
CARD_BYTES_AUTHORITY: Final = "isolated-run-derived-only"
HISTORICAL_HOST_CARD_SHA256_FORBIDDEN: Final = (
    "d69b43f8be2585f7b5e7fd1b07f85217ba74c01f102c01d7a22928ee0e9e8ae2"
)

SESSION_IDS: Final = (
    "candidate-authoring-save",
    "candidate-clean-restart",
    "retail-town-square-control",
)

EVENTUAL_CHECK_IDS: Final = (
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
    "retail.town-square-preserved",
)


@dataclasses.dataclass(frozen=True)
class ExactEvidenceRoleBinding:
    role_id: str
    session_id: str


def _evidence_roles(
    session_id: str,
    *role_ids: str,
) -> tuple[ExactEvidenceRoleBinding, ...]:
    return tuple(ExactEvidenceRoleBinding(role_id, session_id) for role_id in role_ids)


@dataclasses.dataclass(frozen=True)
class ExactCheckBinding:
    check_id: str
    session_ids: tuple[str, ...]
    evidence_roles: tuple[ExactEvidenceRoleBinding, ...]


CHECK_BINDINGS: Final = (
    ExactCheckBinding(
        "candidate.safe-load-and-spawn",
        (SESSION_IDS[0],),
        _evidence_roles(
            SESSION_IDS[0],
            "candidate-load-armed", "candidate-spawn-frame", "landing-watcher",
        ),
    ),
    ExactCheckBinding(
        "candidate.authored-terrain-visible",
        (SESSION_IDS[0],),
        _evidence_roles(SESSION_IDS[0], "authored-face-view-frame"),
    ),
    ExactCheckBinding(
        "candidate.authored-collision-playable",
        (SESSION_IDS[0],),
        _evidence_roles(
            SESSION_IDS[0],
            "authored-movement-interval", "movement-safe-watcher-suffix",
        ),
    ),
    ExactCheckBinding(
        "candidate.authored-occlusion-correct",
        (SESSION_IDS[0],),
        _evidence_roles(
            SESSION_IDS[0], "camera-occlusion-interval", "camera-stable-endpoint"
        ),
    ),
    ExactCheckBinding(
        "candidate.private-texture-swap-visible",
        (SESSION_IDS[0],),
        _evidence_roles(SESSION_IDS[0], "authored-face-view-frame"),
    ),
    ExactCheckBinding(
        "candidate.grass-zero-egg-placement-present",
        (SESSION_IDS[0],),
        _evidence_roles(SESSION_IDS[0], "grass-index-88-frame"),
    ),
    ExactCheckBinding(
        "candidate.grass-authored-placement-present",
        (SESSION_IDS[0],),
        _evidence_roles(SESSION_IDS[0], "grass-index-107-frame"),
    ),
    ExactCheckBinding(
        "candidate.zero-egg-preserved",
        (SESSION_IDS[0],),
        _evidence_roles(
            SESSION_IDS[0],
            "grass-index-88-frame", "grass-index-107-frame", "pause-zero-egg-token",
        ),
    ),
    ExactCheckBinding(
        "candidate.camera-360-stable",
        (SESSION_IDS[0],),
        _evidence_roles(
            SESSION_IDS[0], "camera-occlusion-interval", "camera-stable-endpoint"
        ),
    ),
    ExactCheckBinding(
        "candidate.walk-stable",
        (SESSION_IDS[0],),
        _evidence_roles(SESSION_IDS[0], "authored-movement-interval"),
    ),
    ExactCheckBinding(
        "candidate.jump-land-stable",
        (SESSION_IDS[0],),
        _evidence_roles(
            SESSION_IDS[0],
            "jump-start-frame", "landing-watcher", "stable-post-landing-frame",
        ),
    ),
    ExactCheckBinding(
        "candidate.fall-respawn-stable",
        (SESSION_IDS[1],),
        _evidence_roles(
            SESSION_IDS[1],
            "fall-authorized-frame", "fall-respawn-watcher", "post-respawn-frame",
        ),
    ),
    ExactCheckBinding(
        "candidate.music-long-play-stable",
        (SESSION_IDS[0],),
        _evidence_roles(
            SESSION_IDS[0],
            "music-observation-120500ms", "live-music-operator-attestation",
        ),
    ),
    ExactCheckBinding(
        "candidate.treasure-total-200",
        (SESSION_IDS[0],),
        _evidence_roles(
            SESSION_IDS[0], "in-level-total-200-frame", "pause-total-200-frame"
        ),
    ),
    ExactCheckBinding(
        "candidate.exit-destination-65",
        (SESSION_IDS[1],),
        _evidence_roles(
            SESSION_IDS[1],
            "normal-exit-started", "destination-65-frame", "candidate-reentry-watcher",
        ),
    ),
    ExactCheckBinding(
        "candidate.save-persistence",
        (SESSION_IDS[0], SESSION_IDS[1]),
        (
            *_evidence_roles(
                SESSION_IDS[0],
                "ordinary-save-completed-frame",
                "save-progress-token",
                "session0-post-card",
            ),
            *_evidence_roles(
                SESSION_IDS[1],
                "session1-pre-card",
                "ordinary-load-completed-frame",
                "load-progress-token",
                "distinct-process-fingerprints",
            ),
        ),
    ),
    ExactCheckBinding(
        "retail.town-square-preserved",
        (SESSION_IDS[2],),
        _evidence_roles(
            SESSION_IDS[2],
            "town-square-terrain-visible-frame",
            "town-square-collision-route-60500ms",
            "town-square-collision-watcher",
            "town-square-moby-visibility-frame",
            "town-square-totals-frame",
            "town-square-exit-frame",
            "town-square-save-transition",
            "town-square-music-observation-60500ms",
            "town-square-live-music-operator-attestation",
        ),
    ),
)

OPERATOR_CHECKPOINTS: Final[Mapping[str, tuple[str, ...]]] = {
    SESSION_IDS[0]: (
        "resume-fresh-candidate",
        "face-texture-complete",
        "grass-index-88-framed",
        "grass-index-107-framed",
        "movement-group-complete",
        "camera-group-complete",
        "in-level-total-framed",
        "pause-total-zero-egg-framed",
        "ordinary-save-complete",
    ),
    SESSION_IDS[1]: (
        "resume-clean-restart",
        "loaded-save-confirmed",
        "exit-destination-confirmed",
        "fall-authorized",
    ),
    SESSION_IDS[2]: (
        "resume-fresh-town-square",
        "retail-route-complete",
        "retail-totals-framed",
        "retail-save-complete",
        "retail-exit-complete",
    ),
}

CHECK_BINDING_OWNER_SESSION: Final[Mapping[str, str]] = {
    binding.check_id: (
        SESSION_IDS[1]
        if binding.check_id == "candidate.save-persistence"
        else binding.session_ids[0]
    )
    for binding in CHECK_BINDINGS
}

FORBIDDEN_LEGACY_TOKENS: Final = (
    "retail-gnastys-loot",
    "retail-sunny-flight",
    "candidate-primary-edge-0",
    "candidate-edge-1",
    "candidate-edge-2",
    "candidate-cold-reset",
    "candidate.lone-pad-visibility",
    "triangular-yaw",
    "E0",
    "E1",
    "E2",
)


@dataclasses.dataclass(frozen=True)
class OperationalToolPin:
    role: str
    relative_path: str
    byte_length: int
    sha256: str
    mode: str


OPERATIONAL_TOOL_PINS: Final = (
    OperationalToolPin(
        "frozen-profile-builder",
        "tools/Build-SpyroId65UnattendedProfile.py",
        100_781,
        "0591f1b6bf3f61dac4bfa61d407df65a2a9862569c8d2a7678d216b95929b95c",
        "0644",
    ),
    OperationalToolPin(
        "frozen-profile-verifier",
        "tools/Verify-SpyroId65UnattendedProfile.py",
        62_959,
        "74f4ed279a3e3f60af6c4c0a0b1fa8ffc0a67886c478d8cc7d1a288b093c3671",
        "0644",
    ),
    OperationalToolPin(
        "save-profile-builder",
        "tools/Build-SpyroId65AuthoringAggregateSaveV1Profile.py",
        28_304,
        "759ec6a561166fc3e879db58db56781f757735b0975a5db0768248f276aec297",
        "0644",
    ),
    OperationalToolPin(
        "save-profile-verifier",
        "tools/Verify-SpyroId65AuthoringAggregateSaveV1Profile.py",
        25_873,
        "36913ca29f4d4bdbb2f7ae539df3f6033f5018f5a39a77affce2638c1ca4042b",
        "0644",
    ),
    OperationalToolPin(
        "frozen-capture-primitives",
        "tools/Capture-SpyroId65V3RuntimeSession.py",
        473_144,
        "153871778877a8736a1243ce2e84453e4d0d8e2fb945f9e96fdc01fbf00278ec",
        "0755",
    ),
    OperationalToolPin(
        "runtime-watcher",
        "tools/Watch-SpyroId65RuntimeState.py",
        21_426,
        "eac3953a0ff7ff8acdefbe43cf987f296d44091ad51619efd225690ba1dc0677",
        "0644",
    ),
)

EXACT_CAPTURE_ENGINE_IMPLEMENTED: Final = False
EXACT_CAPTURE_ENGINE_BLOCKER_ID: Final = "EXACT_CAPTURE_ENGINE_NOT_IMPLEMENTED"
EXACT_CAPTURE_ENGINE_REQUIRED_GATE: Final = (
    "aggregate.exact-three-session-card-aware-capture-engine"
)
MUSIC_AUDIO_TRACK_VALIDATED: Final = False
MUSIC_REQUIRES_LIVE_OPERATOR_ATTESTATION: Final = True

_SHA256_RE: Final = re.compile(r"\A[0-9a-f]{64}\Z")
ZERO_SHA256: Final = "0" * 64
RUN_ID_RE: Final = re.compile(
    r"\Aid65agg1-[0-9]{8}t[0-9]{6}z-[0-9a-f]{16}\Z"
)
OFFLINE_FIXTURE_RUN_ID: Final = (
    "id65agg1-20260825t120000z-0123456789abcdef"
)
RUN_ORIGIN_FORMAT: Final = (
    "spyro-id65-authoring-aggregate-exact-v1-capture-run-origin"
)
SESSION_JOURNAL_FORMAT: Final = (
    "spyro-id65-authoring-aggregate-exact-v1-capture-session-journal"
)
PROCESS_FINGERPRINT_FORMAT: Final = "duckstation-process-fingerprint"
PROCESS_FINGERPRINT_FORMAT_VERSION: Final = 1
PROCESS_INSTANCE_ID_RE: Final = re.compile(r"\Adsproc-[0-9a-f]{64}\Z")
LAUNCH_NONCE_RE: Final = re.compile(r"\A[0-9a-f]{32}\Z")
UTC_MILLISECONDS_RE: Final = re.compile(
    r"\A[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:"
    r"[0-9]{2}:[0-9]{2}\.[0-9]{3}Z\Z"
)
CARD_CHAIN_LAYOUT: Final = (
    ("prelaunch-empty", "preflight"),
    ("post-session", SESSION_IDS[0]),
    ("pre-session", SESSION_IDS[1]),
    ("post-session", SESSION_IDS[1]),
    ("pre-session", SESSION_IDS[2]),
    ("post-session", SESSION_IDS[2]),
)


def _canonical_json(value: object) -> str:
    return json.dumps(
        value,
        allow_nan=False,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
    )


def _sha256_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def _sha256_canonical(value: object) -> str:
    return _sha256_bytes(_canonical_json(value).encode("utf-8"))


def _binding_payload() -> list[dict[str, object]]:
    return [
        {
            "checkId": binding.check_id,
            "sessionIds": list(binding.session_ids),
            "evidenceRoles": [
                {"roleId": role.role_id, "sessionId": role.session_id}
                for role in binding.evidence_roles
            ],
        }
        for binding in CHECK_BINDINGS
    ]


def _plan_payload() -> dict[str, object]:
    return {
        "format": "spyro-id65-authoring-aggregate-exact-v1-capture-plan",
        "formatVersion": 1,
        "sessionIds": list(SESSION_IDS),
        "eventualCheckIds": list(EVENTUAL_CHECK_IDS),
        "checkBindings": _binding_payload(),
        "checkBindingOwnerSession": {
            check_id: CHECK_BINDING_OWNER_SESSION[check_id]
            for check_id in EVENTUAL_CHECK_IDS
        },
        "operatorCheckpoints": {
            session: list(OPERATOR_CHECKPOINTS[session]) for session in SESSION_IDS
        },
        "operatorCheckpointCount": sum(map(len, OPERATOR_CHECKPOINTS.values())),
        "cardSnapshotFormat": CARD_SNAPSHOT_FORMAT,
        "cardSnapshotFormatVersion": CARD_SNAPSHOT_FORMAT_VERSION,
        "cardLeaf": CARD_LEAF,
        "cardByteLength": CARD_SIZE,
        "cardLeafSizeProvenance": CARD_LEAF_SIZE_PROVENANCE,
        "cardBytesAuthority": CARD_BYTES_AUTHORITY,
        "historicalHostCardBytesForbidden": True,
        "musicAudioTrackValidated": MUSIC_AUDIO_TRACK_VALIDATED,
        "musicRequiresLiveOperatorAttestation": (
            MUSIC_REQUIRES_LIVE_OPERATOR_ATTESTATION
        ),
    }


def _tool_pin_payload() -> list[dict[str, object]]:
    return [dataclasses.asdict(pin) for pin in OPERATIONAL_TOOL_PINS]


def _profile_pin_payload() -> dict[str, object]:
    return {
        "profileId": PROFILE_ID,
        "profileRelativePath": PROFILE_RELATIVE_PATH.as_posix(),
        "receiptRelativePath": PROFILE_RECEIPT_RELATIVE_PATH.as_posix(),
        "receiptByteLength": EXPECTED_PROFILE_RECEIPT_SIZE,
        "receiptSha256": EXPECTED_PROFILE_RECEIPT_SHA256,
        "receiptClosureSha256": EXPECTED_PROFILE_RECEIPT_CLOSURE_SHA256,
        "settingsRelativePath": PROFILE_SETTINGS_RELATIVE_PATH.as_posix(),
        "settingsByteLength": EXPECTED_SETTINGS_SIZE,
        "settingsSha256": EXPECTED_SETTINGS_SHA256,
        "appContentManifestSha256": EXPECTED_APP_CONTENT_MANIFEST_SHA256,
        "appOutputTreeManifestSha256": EXPECTED_APP_OUTPUT_TREE_MANIFEST_SHA256,
        "homeBaselineTreeManifestSha256": (
            EXPECTED_HOME_BASELINE_TREE_MANIFEST_SHA256
        ),
        "packageEnvelopeSha256": EXPECTED_PACKAGE_ENVELOPE_SHA256,
        "executableSha256": EXPECTED_EXECUTABLE_SHA256,
        "applicationRelativePath": EXPECTED_APPLICATION_RELATIVE_PATH,
        "executableRelativePath": EXPECTED_EXECUTABLE_RELATIVE_PATH,
        "executableProfileRelativePath": (
            EXPECTED_EXECUTABLE_PROFILE_RELATIVE_PATH
        ),
        "biosSha256": EXPECTED_BIOS_SHA256,
        "candidateCueSha256": EXPECTED_CANDIDATE_CUE_SHA256,
        "candidateBinSha256": EXPECTED_CANDIDATE_BIN_SHA256,
        "hostSettingsSentinelPath": os.fspath(HOST_SETTINGS_SENTINEL_PATH),
        "hostSettingsSentinelByteLength": EXPECTED_HOST_SETTINGS_SIZE,
        "hostSettingsSentinelSha256": EXPECTED_HOST_SETTINGS_SHA256,
        "cardLeaf": CARD_LEAF,
        "cardByteLength": CARD_SIZE,
        "cardLeafSizeProvenance": CARD_LEAF_SIZE_PROVENANCE,
        "cardBytesAuthority": CARD_BYTES_AUTHORITY,
        "cardMode": "0600",
        "memcardsDirectoryMode": "0700",
        "historicalHostCardSha256Forbidden": (
            HISTORICAL_HOST_CARD_SHA256_FORBIDDEN
        ),
    }


def _schema_closure_payload() -> dict[str, object]:
    return {
        "format": "spyro-id65-authoring-aggregate-exact-v1-schema-closure",
        "formatVersion": 1,
        "runOriginFormat": RUN_ORIGIN_FORMAT,
        "sessionJournalFormat": SESSION_JOURNAL_FORMAT,
        "cardSnapshotFormat": CARD_SNAPSHOT_FORMAT,
        "cardSnapshotFormatVersion": CARD_SNAPSHOT_FORMAT_VERSION,
        "processFingerprintFormat": PROCESS_FINGERPRINT_FORMAT,
        "processFingerprintFormatVersion": PROCESS_FINGERPRINT_FORMAT_VERSION,
        "processInstanceIdDerivation": "sha256-canonical-process-identity",
        "osProcessIdentityDerivation": (
            "sha256-pid-start-executable-path-and-bytes-without-launch-nonce"
        ),
        "processLifecycleSemantics": "pre-running-post-clean-exit",
        "cleanRestartOrdering": "next-start-strictly-after-prior-clean-exit",
        "utcTimestampSemantics": "parsed-real-utc-milliseconds",
        "evidenceRoleOwnership": "explicit-owning-session-per-role",
        "cardChainLayout": [list(item) for item in CARD_CHAIN_LAYOUT],
        "journalEventTypes": [
            "session-open",
            "card-snapshot-bound",
            "operator-checkpoint",
            "check-bound",
            "session-sealed",
        ],
        "cardLeaf": CARD_LEAF,
        "cardByteLength": CARD_SIZE,
        "cardLeafSizeProvenance": CARD_LEAF_SIZE_PROVENANCE,
        "cardBytesAuthority": CARD_BYTES_AUTHORITY,
        "cardMode": "0600",
        "memcardsDirectoryMode": "0700",
    }


def _source_declaration_closure_payload(
    operational_tool_pins_sha256: str,
    profile_pin_set_sha256: str,
) -> dict[str, object]:
    return {
        "format": (
            "spyro-id65-authoring-aggregate-exact-v1-source-declaration-closure"
        ),
        "formatVersion": 1,
        "operationalToolPinsSha256": operational_tool_pins_sha256,
        "profilePinSetSha256": profile_pin_set_sha256,
    }


def _workflow_declaration_closure_payload(
    *,
    exact_plan_sha256: str,
    exact_checklist_sha256: str,
    schema_closure_sha256: str,
    source_declaration_closure_sha256: str,
    profile_pin_set_sha256: str,
) -> dict[str, object]:
    return {
        "format": (
            "spyro-id65-authoring-aggregate-exact-v1-workflow-declaration-closure"
        ),
        "formatVersion": 1,
        "exactPlanSha256": exact_plan_sha256,
        "exactChecklistSha256": exact_checklist_sha256,
        "schemaClosureSha256": schema_closure_sha256,
        "sourceDeclarationClosureSha256": source_declaration_closure_sha256,
        "profilePinSetSha256": profile_pin_set_sha256,
        "profileReceiptClosureSha256": EXPECTED_PROFILE_RECEIPT_CLOSURE_SHA256,
        "productionBlockerId": EXACT_CAPTURE_ENGINE_BLOCKER_ID,
    }


EXACT_PLAN_SHA256: Final = _sha256_canonical(_plan_payload())
EXACT_CHECKLIST_SHA256: Final = _sha256_canonical(
    {"checkIds": list(EVENTUAL_CHECK_IDS), "bindings": _binding_payload()}
)
OPERATIONAL_TOOL_PINS_SHA256: Final = _sha256_canonical(_tool_pin_payload())
PROFILE_PIN_SET_SHA256: Final = _sha256_canonical(_profile_pin_payload())
SCHEMA_CLOSURE_SHA256: Final = _sha256_canonical(_schema_closure_payload())
SOURCE_DECLARATION_CLOSURE_SHA256: Final = _sha256_canonical(
    _source_declaration_closure_payload(
        OPERATIONAL_TOOL_PINS_SHA256,
        PROFILE_PIN_SET_SHA256,
    )
)
WORKFLOW_DECLARATION_CLOSURE_SHA256: Final = _sha256_canonical(
    _workflow_declaration_closure_payload(
        exact_plan_sha256=EXACT_PLAN_SHA256,
        exact_checklist_sha256=EXACT_CHECKLIST_SHA256,
        schema_closure_sha256=SCHEMA_CLOSURE_SHA256,
        source_declaration_closure_sha256=SOURCE_DECLARATION_CLOSURE_SHA256,
        profile_pin_set_sha256=PROFILE_PIN_SET_SHA256,
    )
)


def _require_repository_root(value: pathlib.Path) -> pathlib.Path:
    if not isinstance(value, pathlib.Path) or not value.is_absolute():
        raise ExactCaptureRefusal("repository root must be an absolute pathlib.Path.")
    normalized = pathlib.Path(os.path.normpath(os.fspath(value)))
    if normalized != value:
        raise ExactCaptureRefusal("repository root must be normalized.")
    expected = PRODUCTION_REPOSITORY_ROOT.resolve(strict=True)
    if value != expected:
        raise ExactCaptureRefusal("repository root is not the audited checkout.")
    current = pathlib.Path(value.anchor)
    for segment in value.parts[1:]:
        current /= segment
        info = os.lstat(current)
        if stat.S_ISLNK(info.st_mode):
            raise ExactCaptureRefusal("repository root traverses a symbolic link.")
    return value


def _verify_tool_pin(root: pathlib.Path, pin: OperationalToolPin) -> None:
    path = root / pin.relative_path
    fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW | getattr(os, "O_CLOEXEC", 0))
    try:
        before = os.fstat(fd)
        expected_mode = int(pin.mode, 8)
        if (
            not stat.S_ISREG(before.st_mode)
            or before.st_uid != os.geteuid()
            or before.st_gid != os.getegid()
            or stat.S_IMODE(before.st_mode) != expected_mode
            or before.st_nlink != 1
            or before.st_size != pin.byte_length
        ):
            raise ExactCaptureRefusal(f"operational tool metadata changed: {pin.role}.")
        digest = hashlib.sha256()
        observed = 0
        while True:
            chunk = os.read(fd, 1024 * 1024)
            if not chunk:
                break
            observed += len(chunk)
            digest.update(chunk)
        after = os.fstat(fd)
        fields = (
            "st_dev", "st_ino", "st_mode", "st_uid", "st_gid", "st_nlink",
            "st_size", "st_mtime_ns", "st_ctime_ns",
        )
        if observed != pin.byte_length or any(
            getattr(before, field) != getattr(after, field) for field in fields
        ):
            raise ExactCaptureRefusal(f"operational tool changed while read: {pin.role}.")
        if digest.hexdigest() != pin.sha256:
            raise ExactCaptureRefusal(f"operational tool hash changed: {pin.role}.")
    finally:
        os.close(fd)


def _validate_profile_pin_declarations() -> None:
    profile_hashes = (
        EXPECTED_PROFILE_RECEIPT_SHA256,
        EXPECTED_PROFILE_RECEIPT_CLOSURE_SHA256,
        EXPECTED_SETTINGS_SHA256,
        EXPECTED_APP_CONTENT_MANIFEST_SHA256,
        EXPECTED_APP_OUTPUT_TREE_MANIFEST_SHA256,
        EXPECTED_HOME_BASELINE_TREE_MANIFEST_SHA256,
        EXPECTED_PACKAGE_ENVELOPE_SHA256,
        EXPECTED_EXECUTABLE_SHA256,
        EXPECTED_BIOS_SHA256,
        EXPECTED_CANDIDATE_CUE_SHA256,
        EXPECTED_CANDIDATE_BIN_SHA256,
        EXPECTED_HOST_SETTINGS_SHA256,
        HISTORICAL_HOST_CARD_SHA256_FORBIDDEN,
    )
    if (
        not all(_SHA256_RE.fullmatch(value) for value in profile_hashes)
        or EXPECTED_PROFILE_RECEIPT_SIZE <= 0
        or EXPECTED_SETTINGS_SIZE <= 0
        or EXPECTED_HOST_SETTINGS_SIZE <= 0
        or PROFILE_RECEIPT_RELATIVE_PATH.parts[:len(PROFILE_RELATIVE_PATH.parts)]
        != PROFILE_RELATIVE_PATH.parts
        or PROFILE_SETTINGS_RELATIVE_PATH.parts[:len(PROFILE_RELATIVE_PATH.parts)]
        != PROFILE_RELATIVE_PATH.parts
        or PROFILE_MEMCARDS_RELATIVE_PATH.parts[:len(PROFILE_RELATIVE_PATH.parts)]
        != PROFILE_RELATIVE_PATH.parts
        or ".." in PROFILE_RELATIVE_PATH.parts
        or not HOST_SETTINGS_SENTINEL_PATH.is_absolute()
        or CARD_SIZE != 131_072
        or CARD_MODE != 0o600
        or MEMCARDS_MODE != 0o700
        or CARD_LEAF != "Spyro the Dragon (USA)_1.mcd"
        or CARD_LEAF_SIZE_PROVENANCE
        != "observed-host-duckstation-per-game-title-convention"
        or CARD_BYTES_AUTHORITY != "isolated-run-derived-only"
        or EXPECTED_APPLICATION_RELATIVE_PATH
        != "DuckStation-ID65-Unattended-v1.app"
        or EXPECTED_EXECUTABLE_RELATIVE_PATH != "Contents/MacOS/DuckStation"
        or EXPECTED_EXECUTABLE_PROFILE_RELATIVE_PATH
        != "DuckStation-ID65-Unattended-v1.app/Contents/MacOS/DuckStation"
    ):
        raise ExactCaptureRefusal("save-profile pin declarations changed.")


def _validate_plan() -> None:
    _validate_profile_pin_declarations()
    expected_plan_sha256 = _sha256_canonical(_plan_payload())
    expected_checklist_sha256 = _sha256_canonical({
        "checkIds": list(EVENTUAL_CHECK_IDS),
        "bindings": _binding_payload(),
    })
    expected_tool_pins_sha256 = _sha256_canonical(_tool_pin_payload())
    expected_profile_pin_set_sha256 = _sha256_canonical(_profile_pin_payload())
    expected_schema_closure_sha256 = _sha256_canonical(
        _schema_closure_payload()
    )
    expected_source_declaration_closure_sha256 = _sha256_canonical(
        _source_declaration_closure_payload(
            expected_tool_pins_sha256,
            expected_profile_pin_set_sha256,
        )
    )
    expected_workflow_declaration_closure_sha256 = _sha256_canonical(
        _workflow_declaration_closure_payload(
            exact_plan_sha256=expected_plan_sha256,
            exact_checklist_sha256=expected_checklist_sha256,
            schema_closure_sha256=expected_schema_closure_sha256,
            source_declaration_closure_sha256=(
                expected_source_declaration_closure_sha256
            ),
            profile_pin_set_sha256=expected_profile_pin_set_sha256,
        )
    )
    if (
        EXACT_PLAN_SHA256 != expected_plan_sha256
        or EXACT_CHECKLIST_SHA256 != expected_checklist_sha256
        or OPERATIONAL_TOOL_PINS_SHA256 != expected_tool_pins_sha256
        or PROFILE_PIN_SET_SHA256 != expected_profile_pin_set_sha256
        or SCHEMA_CLOSURE_SHA256 != expected_schema_closure_sha256
        or SOURCE_DECLARATION_CLOSURE_SHA256
        != expected_source_declaration_closure_sha256
        or WORKFLOW_DECLARATION_CLOSURE_SHA256
        != expected_workflow_declaration_closure_sha256
    ):
        raise ExactCaptureRefusal("exact plan/source/schema/workflow closure drifted.")
    if (
        SESSION_IDS != (
            "candidate-authoring-save",
            "candidate-clean-restart",
            "retail-town-square-control",
        )
        or tuple(binding.check_id for binding in CHECK_BINDINGS)
        != EVENTUAL_CHECK_IDS
        or len(set(EVENTUAL_CHECK_IDS)) != 17
        or set(OPERATOR_CHECKPOINTS) != set(SESSION_IDS)
        or [len(OPERATOR_CHECKPOINTS[session]) for session in SESSION_IDS]
        != [9, 4, 5]
        or sum(map(len, OPERATOR_CHECKPOINTS.values())) != 18
        or set(CHECK_BINDING_OWNER_SESSION) != set(EVENTUAL_CHECK_IDS)
    ):
        raise ExactCaptureRefusal("exact session/check/checkpoint plan changed.")
    save = CHECK_BINDINGS[EVENTUAL_CHECK_IDS.index("candidate.save-persistence")]
    retail = CHECK_BINDINGS[EVENTUAL_CHECK_IDS.index("retail.town-square-preserved")]
    if save.session_ids != SESSION_IDS[:2] or retail.session_ids != (SESSION_IDS[2],):
        raise ExactCaptureRefusal("cross-session save or fresh retail binding changed.")
    if (
        CHECK_BINDING_OWNER_SESSION["candidate.save-persistence"] != SESSION_IDS[1]
        or any(
            CHECK_BINDING_OWNER_SESSION[binding.check_id]
            not in binding.session_ids
            for binding in CHECK_BINDINGS
        )
    ):
        raise ExactCaptureRefusal("deterministic check-binding ownership changed.")
    if any(
        not binding.evidence_roles
        or len({role.role_id for role in binding.evidence_roles})
        != len(binding.evidence_roles)
        or any(
            not isinstance(role, ExactEvidenceRoleBinding)
            or not role.role_id
            or role.session_id not in binding.session_ids
            for role in binding.evidence_roles
        )
        for binding in CHECK_BINDINGS
    ):
        raise ExactCaptureRefusal("evidence-role session ownership changed.")
    expected_save_role_owners = {
        "ordinary-save-completed-frame": SESSION_IDS[0],
        "save-progress-token": SESSION_IDS[0],
        "session0-post-card": SESSION_IDS[0],
        "session1-pre-card": SESSION_IDS[1],
        "ordinary-load-completed-frame": SESSION_IDS[1],
        "load-progress-token": SESSION_IDS[1],
        "distinct-process-fingerprints": SESSION_IDS[1],
    }
    if {
        role.role_id: role.session_id for role in save.evidence_roles
    } != expected_save_role_owners:
        raise ExactCaptureRefusal("cross-session save evidence ownership changed.")
    serialized = _canonical_json(_plan_payload())
    if any(token in serialized for token in FORBIDDEN_LEGACY_TOKENS):
        raise ExactCaptureRefusal("legacy seven-session workflow entered exact plan.")
    if not all(
        _SHA256_RE.fullmatch(value)
        for value in (
            EXACT_PLAN_SHA256,
            EXACT_CHECKLIST_SHA256,
            OPERATIONAL_TOOL_PINS_SHA256,
            PROFILE_PIN_SET_SHA256,
            SCHEMA_CLOSURE_SHA256,
            SOURCE_DECLARATION_CLOSURE_SHA256,
            WORKFLOW_DECLARATION_CLOSURE_SHA256,
        )
    ):
        raise ExactCaptureRefusal("exact plan closure is malformed.")


def _require_snapshot_integer(value: object, label: str, minimum: int = 0) -> int:
    if not isinstance(value, int) or isinstance(value, bool) or value < minimum:
        raise ExactCaptureRefusal(f"card snapshot {label} is invalid.")
    return value


def _parse_utc_milliseconds(value: object, label: str) -> datetime.datetime:
    if not isinstance(value, str) or UTC_MILLISECONDS_RE.fullmatch(value) is None:
        raise ExactCaptureRefusal(f"{label} is not exact UTC milliseconds.")
    try:
        parsed = datetime.datetime.strptime(
            value, "%Y-%m-%dT%H:%M:%S.%fZ"
        ).replace(tzinfo=datetime.timezone.utc)
    except ValueError:
        raise ExactCaptureRefusal(f"{label} is not a real UTC instant.") from None
    return parsed


def _validate_process_identity(identity: object) -> Mapping[str, object]:
    if not isinstance(identity, dict) or set(identity) != {
        "pid", "processStartUtc", "executableProfileRelativePath",
        "executableSha256", "launchNonce",
    }:
        raise ExactCaptureRefusal("process fingerprint identity schema changed.")
    _require_snapshot_integer(identity["pid"], "process.pid", minimum=1)
    _parse_utc_milliseconds(identity["processStartUtc"], "process start UTC")
    if (
        identity["executableProfileRelativePath"]
        != EXPECTED_EXECUTABLE_PROFILE_RELATIVE_PATH
        or identity["executableSha256"] != EXPECTED_EXECUTABLE_SHA256
        or not isinstance(identity["launchNonce"], str)
        or LAUNCH_NONCE_RE.fullmatch(identity["launchNonce"]) is None
    ):
        raise ExactCaptureRefusal("process executable or launch identity changed.")
    return identity


def process_instance_id_from_fingerprint(fingerprint: object) -> str:
    if not isinstance(fingerprint, dict) or set(fingerprint) != {
        "format", "formatVersion", "identity", "termination"
    }:
        raise ExactCaptureRefusal("process fingerprint root schema changed.")
    if (
        fingerprint["format"] != PROCESS_FINGERPRINT_FORMAT
        or fingerprint["formatVersion"] != PROCESS_FINGERPRINT_FORMAT_VERSION
    ):
        raise ExactCaptureRefusal("process fingerprint format changed.")
    identity = _validate_process_identity(fingerprint["identity"])
    return "dsproc-" + _sha256_canonical(identity)


def os_process_identity_sha256_from_fingerprint(fingerprint: object) -> str:
    process_instance_id_from_fingerprint(fingerprint)
    assert isinstance(fingerprint, dict)
    identity = fingerprint["identity"]
    assert isinstance(identity, dict)
    return _sha256_canonical({
        "pid": identity["pid"],
        "processStartUtc": identity["processStartUtc"],
        "executableProfileRelativePath": identity[
            "executableProfileRelativePath"
        ],
        "executableSha256": identity["executableSha256"],
    })


def _validate_process_fingerprint(
    fingerprint: object,
    *,
    expected_phase: str,
    snapshot_captured_at: datetime.datetime,
) -> str:
    process_instance_id = process_instance_id_from_fingerprint(fingerprint)
    assert isinstance(fingerprint, dict)
    identity = fingerprint["identity"]
    assert isinstance(identity, dict)
    start = _parse_utc_milliseconds(
        identity["processStartUtc"], "process start UTC"
    )
    if start >= snapshot_captured_at:
        raise ExactCaptureRefusal("process start is not before card snapshot.")
    termination = fingerprint["termination"]
    if not isinstance(termination, dict) or set(termination) != {
        "observed", "capturedAtUtc", "exitCode", "signal"
    }:
        raise ExactCaptureRefusal("process termination schema changed.")
    if expected_phase == "pre-session":
        if termination != {
            "observed": False,
            "capturedAtUtc": None,
            "exitCode": None,
            "signal": None,
        }:
            raise ExactCaptureRefusal("pre-session process claims termination.")
    elif expected_phase == "post-session":
        if (
            termination["observed"] is not True
            or termination["exitCode"] != 0
            or termination["signal"] is not None
        ):
            raise ExactCaptureRefusal("post-session process lacks a clean exit.")
        terminated = _parse_utc_milliseconds(
            termination["capturedAtUtc"], "process termination UTC"
        )
        if terminated < start or terminated > snapshot_captured_at:
            raise ExactCaptureRefusal("process termination chronology changed.")
    else:
        raise ExactCaptureRefusal("process fingerprint phase is invalid.")
    return process_instance_id


def _validate_card_entry(
    entry: object,
    *,
    expected_run_id: str,
    expected_phase: str,
    expected_session_id: str,
) -> Mapping[str, object]:
    if not isinstance(entry, dict):
        raise ExactCaptureRefusal("card snapshot entry is not an object.")
    expected_keys = {
        "leaf", "byteLength", "sha256", "dev", "ino", "mode", "uid", "gid",
        "nlink", "mtimeNs", "ctimeNs", "archiveArtifactId",
        "archiveRepoRelativePath", "archiveSha256",
    }
    if set(entry) != expected_keys:
        raise ExactCaptureRefusal("card snapshot entry schema changed.")
    if entry["leaf"] != CARD_LEAF or entry["byteLength"] != CARD_SIZE:
        raise ExactCaptureRefusal("card leaf or byte length changed.")
    if entry["mode"] != "0600" or entry["nlink"] != 1:
        raise ExactCaptureRefusal("card mode or link count changed.")
    for key in ("dev", "ino"):
        _require_snapshot_integer(entry[key], key, minimum=1)
    for key in ("uid", "gid", "mtimeNs", "ctimeNs"):
        _require_snapshot_integer(entry[key], key)
    if entry["uid"] != os.geteuid() or entry["gid"] != os.getegid():
        raise ExactCaptureRefusal("card owner identity changed.")
    for key in ("sha256", "archiveSha256"):
        if not isinstance(entry[key], str) or not _SHA256_RE.fullmatch(entry[key]):
            raise ExactCaptureRefusal(f"card snapshot {key} is not SHA-256.")
    if entry["sha256"] != entry["archiveSha256"]:
        raise ExactCaptureRefusal("card source and archive readback hashes differ.")
    if entry["sha256"] == HISTORICAL_HOST_CARD_SHA256_FORBIDDEN:
        raise ExactCaptureRefusal("historical host memory-card bytes are forbidden.")
    if entry["sha256"] == ZERO_SHA256:
        raise ExactCaptureRefusal("zero memory-card hash is forbidden.")
    artifact_id = entry["archiveArtifactId"]
    relative = entry["archiveRepoRelativePath"]
    phase_leaf = "pre" if expected_phase == "pre-session" else "post"
    expected_artifact_id = (
        f"card.{expected_run_id}.{expected_session_id}.{phase_leaf}"
    )
    expected_relative = (
        EXACT_STAGING_PREFIX
        / expected_run_id
        / "cards"
        / f"{expected_artifact_id}.mcd"
    ).as_posix()
    if artifact_id != expected_artifact_id:
        raise ExactCaptureRefusal("card archive artifact ID is invalid.")
    if relative != expected_relative:
        raise ExactCaptureRefusal("card archive path escaped exact staging.")
    return entry


def validate_card_snapshot(
    snapshot: object,
    *,
    expected_phase: str,
    expected_session_id: str,
    expected_run_id: str | None = None,
    prior_snapshot: Mapping[str, object] | None = None,
) -> None:
    if not isinstance(snapshot, dict):
        raise ExactCaptureRefusal("card snapshot root is not an object.")
    expected_keys = {
        "format", "formatVersion", "runId", "snapshotId", "phase", "sessionId",
        "processInstanceId", "processFingerprint", "capturedAtUtc",
        "memcardsDirectory", "entries",
        "priorSnapshotSha256", "sourceArchiveReadbackSha256",
    }
    if set(snapshot) != expected_keys:
        raise ExactCaptureRefusal("card snapshot root schema changed.")
    run_id = snapshot["runId"]
    if (
        snapshot["format"] != CARD_SNAPSHOT_FORMAT
        or snapshot["formatVersion"] != CARD_SNAPSHOT_FORMAT_VERSION
        or not isinstance(run_id, str)
        or RUN_ID_RE.fullmatch(run_id) is None
        or (expected_run_id is not None and run_id != expected_run_id)
        or snapshot["phase"] != expected_phase
        or snapshot["sessionId"] != expected_session_id
        or expected_session_id not in ("preflight",) + SESSION_IDS
    ):
        raise ExactCaptureRefusal("card snapshot identity changed.")
    for key in ("snapshotId", "processInstanceId", "capturedAtUtc"):
        if not isinstance(snapshot[key], str) or not snapshot[key]:
            raise ExactCaptureRefusal(f"card snapshot {key} is empty.")
    if snapshot["snapshotId"] != (
        f"card.{run_id}.{expected_session_id}.{expected_phase}"
    ):
        raise ExactCaptureRefusal("card snapshot ID is not run/session/phase exact.")
    snapshot_captured_at = _parse_utc_milliseconds(
        snapshot["capturedAtUtc"], "card snapshot UTC"
    )
    directory = snapshot["memcardsDirectory"]
    if not isinstance(directory, dict) or set(directory) != {
        "dev", "ino", "mode", "uid", "gid"
    }:
        raise ExactCaptureRefusal("memcards directory identity is malformed.")
    if directory["mode"] != "0700":
        raise ExactCaptureRefusal("memcards directory mode changed.")
    for key in ("dev", "ino"):
        _require_snapshot_integer(directory[key], f"memcards.{key}", minimum=1)
    for key in ("uid", "gid"):
        _require_snapshot_integer(directory[key], f"memcards.{key}")
    if directory["uid"] != os.geteuid() or directory["gid"] != os.getegid():
        raise ExactCaptureRefusal("memcards directory owner identity changed.")
    entries = snapshot["entries"]
    if not isinstance(entries, list):
        raise ExactCaptureRefusal("card snapshot entries is not an array.")
    if expected_phase == "prelaunch-empty":
        if entries or prior_snapshot is not None:
            raise ExactCaptureRefusal("prelaunch memory-card directory is not empty.")
        if snapshot["priorSnapshotSha256"] is not None:
            raise ExactCaptureRefusal("empty prelaunch snapshot has a prior link.")
        if snapshot["sourceArchiveReadbackSha256"] is not None:
            raise ExactCaptureRefusal("empty prelaunch snapshot has card bytes.")
        if snapshot["processInstanceId"] != "not-applicable":
            raise ExactCaptureRefusal("prelaunch snapshot declares a process instance.")
        if snapshot["processFingerprint"] is not None:
            raise ExactCaptureRefusal("prelaunch snapshot declares a process fingerprint.")
        return
    if expected_phase not in {"pre-session", "post-session"} or len(entries) != 1:
        raise ExactCaptureRefusal("runtime card snapshot must contain exactly one card.")
    if PROCESS_INSTANCE_ID_RE.fullmatch(snapshot["processInstanceId"]) is None:
        raise ExactCaptureRefusal("runtime card process instance ID is malformed.")
    derived_process_instance_id = _validate_process_fingerprint(
        snapshot["processFingerprint"],
        expected_phase=expected_phase,
        snapshot_captured_at=snapshot_captured_at,
    )
    if snapshot["processInstanceId"] != derived_process_instance_id:
        raise ExactCaptureRefusal("process instance ID is not fingerprint-derived.")
    entry = _validate_card_entry(
        entries[0],
        expected_run_id=run_id,
        expected_phase=expected_phase,
        expected_session_id=expected_session_id,
    )
    if snapshot["sourceArchiveReadbackSha256"] != entry["sha256"]:
        raise ExactCaptureRefusal("card snapshot readback closure changed.")
    prior_hash = snapshot["priorSnapshotSha256"]
    if prior_snapshot is None:
        raise ExactCaptureRefusal("runtime card snapshot lacks its prior chain link.")
    expected_prior_hash = _sha256_canonical(prior_snapshot)
    if prior_hash != expected_prior_hash:
        raise ExactCaptureRefusal("card snapshot chain hash changed.")
    if prior_snapshot.get("runId") != run_id:
        raise ExactCaptureRefusal("card snapshot prior link crossed run identity.")
    prior_entries = prior_snapshot.get("entries")
    if not isinstance(prior_entries, list):
        raise ExactCaptureRefusal("prior card snapshot entries is malformed.")
    if expected_phase == "pre-session":
        if len(prior_entries) != 1:
            raise ExactCaptureRefusal("prior card snapshot does not contain one card.")
        prior_entry = _validate_card_entry(
            prior_entries[0],
            expected_run_id=run_id,
            expected_phase=str(prior_snapshot.get("phase")),
            expected_session_id=str(prior_snapshot.get("sessionId")),
        )
        identity_keys = (
            "leaf", "byteLength", "sha256", "dev", "ino", "mode", "uid",
            "gid", "nlink",
        )
        if any(entry[key] != prior_entry[key] for key in identity_keys):
            raise ExactCaptureRefusal("adjacent post/pre card identity is not exact.")
    elif expected_session_id == SESSION_IDS[0]:
        if prior_snapshot.get("phase") != "prelaunch-empty" or prior_entries:
            raise ExactCaptureRefusal("session0 post card did not follow empty preflight.")
    elif len(prior_entries) != 1:
        raise ExactCaptureRefusal("post-session prior card snapshot is malformed.")


def card_snapshot_sha256(snapshot: Mapping[str, object]) -> str:
    """Return the exact canonical snapshot identity used by adjacent links."""

    return _sha256_canonical(snapshot)


def validate_card_chain(
    snapshots: Sequence[Mapping[str, object]],
) -> tuple[Mapping[str, object], ...]:
    """Validate empty preflight plus post/pre pairs for three fresh processes."""

    if not isinstance(snapshots, (list, tuple)) or len(snapshots) != 6:
        raise ExactCaptureRefusal("card chain must contain six exact snapshots.")
    values = tuple(snapshots)
    first = values[0]
    if not isinstance(first, dict):
        raise ExactCaptureRefusal("card chain first snapshot is not an object.")
    run_id = first.get("runId")
    if not isinstance(run_id, str) or RUN_ID_RE.fullmatch(run_id) is None:
        raise ExactCaptureRefusal("card chain run identity is malformed.")
    for index, (phase, session_id) in enumerate(CARD_CHAIN_LAYOUT):
        prior = values[index - 1] if index > 0 else None
        validate_card_snapshot(
            values[index],
            expected_phase=phase,
            expected_session_id=session_id,
            expected_run_id=run_id,
            prior_snapshot=prior,
        )
    timestamps = [
        _parse_utc_milliseconds(snapshot["capturedAtUtc"], "card chain UTC")
        for snapshot in values
    ]
    if any(left >= right for left, right in zip(timestamps, timestamps[1:])):
        raise ExactCaptureRefusal("card snapshot chronology is not strictly ordered.")
    directories = [snapshot["memcardsDirectory"] for snapshot in values]
    if any(directory != directories[0] for directory in directories[1:]):
        raise ExactCaptureRefusal("memcards directory identity changed during run.")
    session0_post = values[1]
    session1_pre = values[2]
    session1_post = values[3]
    session2_pre = values[4]
    session2_post = values[5]
    for post, pre in ((session0_post, session1_pre), (session1_post, session2_pre)):
        post_entry = post["entries"][0]
        pre_entry = pre["entries"][0]
        for key in (
            "leaf", "byteLength", "sha256", "dev", "ino", "mode", "uid",
            "gid", "nlink",
        ):
            if post_entry[key] != pre_entry[key]:
                raise ExactCaptureRefusal("adjacent card post/pre identity changed.")
    if (
        session1_pre["processInstanceId"]
        != session1_post["processInstanceId"]
        or session2_pre["processInstanceId"]
        != session2_post["processInstanceId"]
        or session1_pre["processFingerprint"]["identity"]
        != session1_post["processFingerprint"]["identity"]
        or session2_pre["processFingerprint"]["identity"]
        != session2_post["processFingerprint"]["identity"]
    ):
        raise ExactCaptureRefusal("within-session DuckStation process identity changed.")
    process_ids = (
        session0_post["processInstanceId"],
        session1_pre["processInstanceId"],
        session2_pre["processInstanceId"],
    )
    if len(set(process_ids)) != 3:
        raise ExactCaptureRefusal("card chain reused a DuckStation process instance.")
    os_process_ids = tuple(
        os_process_identity_sha256_from_fingerprint(snapshot["processFingerprint"])
        for snapshot in (session0_post, session1_pre, session2_pre)
    )
    if len(set(os_process_ids)) != 3:
        raise ExactCaptureRefusal(
            "card chain reused an OS process identity behind a new launch nonce."
        )
    prior_post_and_next_pre = (
        (session0_post, session1_pre),
        (session1_post, session2_pre),
    )
    for prior_post, next_pre in prior_post_and_next_pre:
        prior_termination = _parse_utc_milliseconds(
            prior_post["processFingerprint"]["termination"]["capturedAtUtc"],
            "prior process termination UTC",
        )
        next_start = _parse_utc_milliseconds(
            next_pre["processFingerprint"]["identity"]["processStartUtc"],
            "next process start UTC",
        )
        if next_start <= prior_termination:
            raise ExactCaptureRefusal(
                "clean-restart process began before the prior clean termination."
            )
    return values


class AppendOnlyJournal:
    """Deterministic append-only hash chain for offline capture fixtures."""

    def __init__(self, run_id: str, session_id: str, ordinal: int) -> None:
        if (
            not isinstance(run_id, str)
            or RUN_ID_RE.fullmatch(run_id) is None
            or session_id not in SESSION_IDS
            or ordinal != SESSION_IDS.index(session_id)
        ):
            raise ExactCaptureRefusal("append-only journal identity is invalid.")
        self.run_id = run_id
        self.session_id = session_id
        self.ordinal = ordinal
        self.events: list[dict[str, object]] = []
        self.previous_sha256 = ZERO_SHA256
        self.last_monotonic_microseconds = -1
        self.sealed = False

    def append(
        self,
        event_type: str,
        payload: Mapping[str, object],
        monotonic_microseconds: int,
    ) -> None:
        if self.sealed:
            raise ExactCaptureRefusal("append attempted after journal seal.")
        if (
            not isinstance(event_type, str)
            or not event_type
            or not isinstance(payload, dict)
            or not isinstance(monotonic_microseconds, int)
            or isinstance(monotonic_microseconds, bool)
            or monotonic_microseconds <= self.last_monotonic_microseconds
        ):
            raise ExactCaptureRefusal("append-only journal event is invalid.")
        material = {
            "sequence": len(self.events),
            "eventType": event_type,
            "monotonicMicroseconds": monotonic_microseconds,
            "previousEventSha256": self.previous_sha256,
            "payload": dict(payload),
        }
        event = dict(material)
        event["eventSha256"] = _sha256_canonical(material)
        self.events.append(event)
        self.previous_sha256 = str(event["eventSha256"])
        self.last_monotonic_microseconds = monotonic_microseconds
        if event_type == "session-sealed":
            self.sealed = True

    def document(self) -> dict[str, object]:
        return {
            "format": SESSION_JOURNAL_FORMAT,
            "formatVersion": 1,
            "runId": self.run_id,
            "sessionId": self.session_id,
            "sessionOrdinal": self.ordinal,
            "events": list(self.events),
            "terminalEventSha256": self.previous_sha256,
            "sealed": self.sealed,
        }


def validate_journal(journal: object) -> Mapping[str, object]:
    if not isinstance(journal, dict) or set(journal) != {
        "format", "formatVersion", "runId", "sessionId", "sessionOrdinal",
        "events", "terminalEventSha256", "sealed",
    }:
        raise ExactCaptureRefusal("append-only journal schema changed.")
    session_id = journal["sessionId"]
    if (
        journal["format"] != SESSION_JOURNAL_FORMAT
        or journal["formatVersion"] != 1
        or not isinstance(journal["runId"], str)
        or RUN_ID_RE.fullmatch(journal["runId"]) is None
        or session_id not in SESSION_IDS
        or journal["sessionOrdinal"] != SESSION_IDS.index(session_id)
        or journal["sealed"] is not True
        or not isinstance(journal["events"], list)
        or not journal["events"]
    ):
        raise ExactCaptureRefusal("append-only journal identity changed.")
    previous = ZERO_SHA256
    last_clock = -1
    allowed_types = {
        "session-open", "card-snapshot-bound", "operator-checkpoint",
        "check-bound", "session-sealed",
    }
    for sequence, event in enumerate(journal["events"]):
        if not isinstance(event, dict) or set(event) != {
            "sequence", "eventType", "monotonicMicroseconds",
            "previousEventSha256", "payload", "eventSha256",
        }:
            raise ExactCaptureRefusal("append-only journal event schema changed.")
        material = {
            key: event[key]
            for key in (
                "sequence", "eventType", "monotonicMicroseconds",
                "previousEventSha256", "payload",
            )
        }
        event_type = event["eventType"]
        payload = event["payload"]
        if (
            event["sequence"] != sequence
            or event["previousEventSha256"] != previous
            or not isinstance(event_type, str)
            or event_type not in allowed_types
            or not isinstance(payload, dict)
            or not isinstance(event["monotonicMicroseconds"], int)
            or isinstance(event["monotonicMicroseconds"], bool)
            or event["monotonicMicroseconds"] <= last_clock
            or event["eventSha256"] != _sha256_canonical(material)
        ):
            raise ExactCaptureRefusal("append-only journal chain changed.")
        previous = str(event["eventSha256"])
        last_clock = int(event["monotonicMicroseconds"])
        if sequence == 0:
            if event_type != "session-open" or set(payload) != {
                "sessionId", "preCardSnapshotSha256"
            } or payload["sessionId"] != session_id:
                raise ExactCaptureRefusal("journal session-open event changed.")
            pre_hash = payload["preCardSnapshotSha256"]
            if pre_hash is not None and (
                not isinstance(pre_hash, str) or _SHA256_RE.fullmatch(pre_hash) is None
            ):
                raise ExactCaptureRefusal("journal pre-card closure is malformed.")
        elif sequence == len(journal["events"]) - 1:
            if event_type != "session-sealed" or set(payload) != {
                "postCardSnapshotSha256"
            } or not isinstance(payload["postCardSnapshotSha256"], str) or (
                _SHA256_RE.fullmatch(payload["postCardSnapshotSha256"]) is None
            ):
                raise ExactCaptureRefusal("journal session-sealed event changed.")
        elif event_type in {"session-open", "session-sealed"}:
            raise ExactCaptureRefusal("journal has an interior session boundary event.")
        elif event_type == "card-snapshot-bound":
            if set(payload) != {"role", "snapshotSha256"} or payload["role"] not in {
                "session-pre", "session-post"
            } or not isinstance(payload["snapshotSha256"], str) or (
                _SHA256_RE.fullmatch(payload["snapshotSha256"]) is None
            ):
                raise ExactCaptureRefusal("journal card binding event changed.")
        elif event_type == "operator-checkpoint":
            if set(payload) != {"checkpointId", "ordinal"} or not isinstance(
                payload["checkpointId"], str
            ) or not payload["checkpointId"] or not isinstance(
                payload["ordinal"], int
            ) or isinstance(payload["ordinal"], bool) or payload["ordinal"] < 0:
                raise ExactCaptureRefusal("journal operator checkpoint event changed.")
        elif event_type == "check-bound" and set(payload) != {
            "checkId", "sessionIds", "evidenceRoles"
        }:
            raise ExactCaptureRefusal("journal check-binding event changed.")
    if (
        journal["events"][0]["eventType"] != "session-open"
        or journal["events"][-1]["eventType"] != "session-sealed"
        or journal["terminalEventSha256"] != previous
    ):
        raise ExactCaptureRefusal("append-only journal terminal closure changed.")
    return journal


def validate_run_bundle(bundle: object) -> Mapping[str, object]:
    _validate_plan()
    if not isinstance(bundle, dict) or set(bundle) != {
        "origin", "journals", "cardSnapshots"
    }:
        raise ExactCaptureRefusal("exact run bundle schema changed.")
    origin = bundle["origin"]
    if not isinstance(origin, dict) or set(origin) != {
        "format", "formatVersion", "runId", "orderedSessionIds", "checkIds",
        "exactPlanSha256", "exactChecklistSha256", "profileId",
        "operationalToolPinsSha256", "profilePinSetSha256",
        "profileReceiptClosureSha256", "schemaClosureSha256",
        "sourceDeclarationClosureSha256", "workflowDeclarationClosureSha256",
        "declarationClosuresBindCoordinatorBytes",
        "profilePinsVerified", "saveProfilePreflightRun",
        "runtimeObserved", "productionCaptureEnabled",
        "musicAudioTrackValidated", "musicRequiresLiveOperatorAttestation",
    }:
        raise ExactCaptureRefusal("exact run origin schema changed.")
    if (
        origin["format"] != RUN_ORIGIN_FORMAT
        or origin["formatVersion"] != 1
        or not isinstance(origin["runId"], str)
        or RUN_ID_RE.fullmatch(origin["runId"]) is None
        or origin["orderedSessionIds"] != list(SESSION_IDS)
        or origin["checkIds"] != list(EVENTUAL_CHECK_IDS)
        or origin["exactPlanSha256"] != EXACT_PLAN_SHA256
        or origin["exactChecklistSha256"] != EXACT_CHECKLIST_SHA256
        or origin["profileId"] != PROFILE_ID
        or origin["operationalToolPinsSha256"] != OPERATIONAL_TOOL_PINS_SHA256
        or origin["profilePinSetSha256"] != PROFILE_PIN_SET_SHA256
        or origin["profileReceiptClosureSha256"]
        != EXPECTED_PROFILE_RECEIPT_CLOSURE_SHA256
        or origin["schemaClosureSha256"] != SCHEMA_CLOSURE_SHA256
        or origin["sourceDeclarationClosureSha256"]
        != SOURCE_DECLARATION_CLOSURE_SHA256
        or origin["workflowDeclarationClosureSha256"]
        != WORKFLOW_DECLARATION_CLOSURE_SHA256
        or origin["declarationClosuresBindCoordinatorBytes"] is not False
        or origin["profilePinsVerified"] is not False
        or origin["saveProfilePreflightRun"] is not False
        or origin["runtimeObserved"] is not False
        or origin["productionCaptureEnabled"] is not False
        or origin["musicAudioTrackValidated"] is not False
        or origin["musicRequiresLiveOperatorAttestation"] is not True
    ):
        raise ExactCaptureRefusal("exact run origin identity changed.")
    snapshots = validate_card_chain(bundle["cardSnapshots"])
    if snapshots[0]["runId"] != origin["runId"]:
        raise ExactCaptureRefusal("exact run substituted a foreign card chain.")
    snapshot_hashes = tuple(card_snapshot_sha256(snapshot) for snapshot in snapshots)
    expected_pre_hashes: tuple[str | None, ...] = (
        None,
        snapshot_hashes[2],
        snapshot_hashes[4],
    )
    expected_post_hashes = (
        snapshot_hashes[1],
        snapshot_hashes[3],
        snapshot_hashes[5],
    )
    journals = bundle["journals"]
    if not isinstance(journals, list) or len(journals) != 3:
        raise ExactCaptureRefusal("exact run does not contain three journals.")
    observed: list[str] = []
    for ordinal, raw in enumerate(journals):
        journal = validate_journal(raw)
        if (
            journal["runId"] != origin["runId"]
            or journal["sessionId"] != SESSION_IDS[ordinal]
        ):
            raise ExactCaptureRefusal("exact run journal order changed.")
        session_id = SESSION_IDS[ordinal]
        owned_bindings = [
            binding for binding in CHECK_BINDINGS
            if CHECK_BINDING_OWNER_SESSION[binding.check_id] == session_id
        ]
        semantic_events: list[tuple[str, dict[str, object]]] = [
            (
                "session-open",
                {
                    "sessionId": session_id,
                    "preCardSnapshotSha256": expected_pre_hashes[ordinal],
                },
            )
        ]
        if expected_pre_hashes[ordinal] is not None:
            semantic_events.append((
                "card-snapshot-bound",
                {
                    "role": "session-pre",
                    "snapshotSha256": expected_pre_hashes[ordinal],
                },
            ))
        semantic_events.extend(
            (
                "operator-checkpoint",
                {"checkpointId": checkpoint_id, "ordinal": checkpoint_ordinal},
            )
            for checkpoint_ordinal, checkpoint_id in enumerate(
                OPERATOR_CHECKPOINTS[session_id]
            )
        )
        semantic_events.extend(
            (
                "check-bound",
                {
                    "checkId": binding.check_id,
                    "sessionIds": list(binding.session_ids),
                    "evidenceRoles": [
                        {"roleId": role.role_id, "sessionId": role.session_id}
                        for role in binding.evidence_roles
                    ],
                },
            )
            for binding in owned_bindings
        )
        semantic_events.extend((
            (
                "card-snapshot-bound",
                {
                    "role": "session-post",
                    "snapshotSha256": expected_post_hashes[ordinal],
                },
            ),
            (
                "session-sealed",
                {"postCardSnapshotSha256": expected_post_hashes[ordinal]},
            ),
        ))
        observed_events = [
            (event["eventType"], event["payload"])
            for event in journal["events"]
        ]
        if observed_events != semantic_events:
            raise ExactCaptureRefusal(
                "journal session/card/check/checkpoint binding order changed."
            )
        for binding in owned_bindings:
            if session_id not in binding.session_ids:
                raise ExactCaptureRefusal("check was bound outside its declared session.")
            observed.append(binding.check_id)
    if len(observed) != 17 or set(observed) != set(EVENTUAL_CHECK_IDS):
        raise ExactCaptureRefusal("exact run does not bind each of 17 checks once.")
    return bundle


_DIRECTORY_OPEN_FLAGS: Final = (
    os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW | getattr(os, "O_CLOEXEC", 0)
)
_LOCK_OPEN_FLAGS: Final = (
    os.O_RDWR | os.O_CREAT | os.O_NOFOLLOW | getattr(os, "O_CLOEXEC", 0)
)


def _same_file_identity(left: os.stat_result, right: os.stat_result) -> bool:
    return left.st_dev == right.st_dev and left.st_ino == right.st_ino


def _validate_lease_relative(
    value: pathlib.PurePosixPath,
    label: str,
) -> pathlib.PurePosixPath:
    if (
        not isinstance(value, pathlib.PurePosixPath)
        or value.is_absolute()
        or not value.parts
        or any(part in {"", ".", ".."} for part in value.parts)
        or pathlib.PurePosixPath(*value.parts) != value
    ):
        raise ExactCaptureRefusal(f"{label} must be a normalized safe relative path.")
    return value


def _require_custom_lease_root(value: pathlib.Path) -> pathlib.Path:
    if not isinstance(value, pathlib.Path) or not value.is_absolute():
        raise ExactCaptureRefusal("temporary lease root must be an absolute pathlib.Path.")
    normalized = pathlib.Path(os.path.normpath(os.fspath(value)))
    if normalized != value:
        raise ExactCaptureRefusal("temporary lease root must be normalized.")
    temporary_parent = pathlib.Path(tempfile.gettempdir()).resolve(strict=True)
    if value == temporary_parent or temporary_parent not in value.parents:
        raise ExactCaptureRefusal("custom lease root must be an isolated temp child.")
    current = pathlib.Path(value.anchor)
    for segment in value.parts[1:]:
        current /= segment
        info = os.lstat(current)
        if stat.S_ISLNK(info.st_mode):
            raise ExactCaptureRefusal("temporary lease root traverses a symbolic link.")
    info = os.lstat(value)
    if (
        not stat.S_ISDIR(info.st_mode)
        or info.st_uid != os.geteuid()
        or info.st_gid != os.getegid()
        or stat.S_IMODE(info.st_mode) != 0o700
    ):
        raise ExactCaptureRefusal(
            "temporary lease root must be current-user mode 0700."
        )
    return value


def _revalidate_absolute_directory_fd(
    path: pathlib.Path,
    opened_fd: int,
    label: str,
    *,
    private: bool,
) -> None:
    try:
        named = os.lstat(path)
        opened = os.fstat(opened_fd)
    except OSError as exc:
        raise ExactCaptureRefusal(f"{label} root revalidation failed: {exc}.") from None
    if (
        not stat.S_ISDIR(named.st_mode)
        or stat.S_ISLNK(named.st_mode)
        or not _same_file_identity(named, opened)
    ):
        raise ExactCaptureRefusal(f"{label} root path/inode identity changed.")
    _require_open_directory_metadata(opened, label, private=private)


def _require_open_directory_metadata(
    info: os.stat_result,
    label: str,
    *,
    private: bool,
) -> None:
    mode = stat.S_IMODE(info.st_mode)
    if (
        not stat.S_ISDIR(info.st_mode)
        or info.st_uid != os.geteuid()
        or info.st_gid != os.getegid()
        or mode & 0o022
        or (private and mode != 0o700)
    ):
        suffix = "current-user mode 0700" if private else "owner-only writable"
        raise ExactCaptureRefusal(f"{label} is not a safe {suffix} directory.")


def _revalidate_named_fd(
    parent_fd: int,
    leaf: str,
    opened_fd: int,
    label: str,
    *,
    require_directory: bool,
) -> os.stat_result:
    try:
        named = os.stat(leaf, dir_fd=parent_fd, follow_symlinks=False)
        opened = os.fstat(opened_fd)
    except OSError as exc:
        raise ExactCaptureRefusal(f"{label} could not be revalidated: {exc}.") from None
    expected_type = stat.S_ISDIR if require_directory else stat.S_ISREG
    if not expected_type(named.st_mode) or not _same_file_identity(named, opened):
        raise ExactCaptureRefusal(f"{label} name/inode identity changed.")
    return opened


def _secure_open_directory_chain(
    root_fd: int,
    relative: pathlib.PurePosixPath,
    *,
    create: bool,
    final_private: bool,
    label: str,
) -> int:
    current_fd = os.dup(root_fd)
    try:
        for index, part in enumerate(relative.parts):
            if create:
                try:
                    os.mkdir(part, mode=0o700, dir_fd=current_fd)
                except FileExistsError:
                    pass
                except OSError as exc:
                    raise ExactCaptureRefusal(
                        f"{label} directory creation failed: {exc}."
                    ) from None
            try:
                before = os.stat(part, dir_fd=current_fd, follow_symlinks=False)
                if not stat.S_ISDIR(before.st_mode):
                    raise ExactCaptureRefusal(f"{label} traverses a non-directory.")
                child_fd = os.open(part, _DIRECTORY_OPEN_FLAGS, dir_fd=current_fd)
            except ExactCaptureRefusal:
                raise
            except OSError as exc:
                raise ExactCaptureRefusal(
                    f"{label} secure traversal failed: {exc}."
                ) from None
            try:
                opened = os.fstat(child_fd)
                if not _same_file_identity(before, opened):
                    raise ExactCaptureRefusal(
                        f"{label} directory changed while opened."
                    )
                _revalidate_named_fd(
                    current_fd,
                    part,
                    child_fd,
                    label,
                    require_directory=True,
                )
                _require_open_directory_metadata(
                    opened,
                    label,
                    private=final_private and index == len(relative.parts) - 1,
                )
            except BaseException:
                os.close(child_fd)
                raise
            os.close(current_fd)
            current_fd = child_fd
        return current_fd
    except BaseException:
        os.close(current_fd)
        raise


def _reopen_chain_and_compare(
    root_fd: int,
    relative: pathlib.PurePosixPath,
    expected_fd: int,
    label: str,
) -> None:
    reopened_fd = _secure_open_directory_chain(
        root_fd,
        relative,
        create=False,
        final_private=True,
        label=label,
    )
    try:
        if not _same_file_identity(os.fstat(reopened_fd), os.fstat(expected_fd)):
            raise ExactCaptureRefusal(f"{label} was rebound after lock acquisition.")
    finally:
        os.close(reopened_fd)


def _open_and_lock_at(directory_fd: int, leaf: str, label: str) -> int:
    if (
        not isinstance(leaf, str)
        or not leaf
        or pathlib.PurePosixPath(leaf).parts != (leaf,)
        or leaf in {".", ".."}
    ):
        raise ExactCaptureRefusal(f"{label} lock leaf is unsafe.")
    try:
        lock_fd = os.open(leaf, _LOCK_OPEN_FLAGS, 0o600, dir_fd=directory_fd)
    except OSError as exc:
        raise ExactCaptureRefusal(f"{label} lock open failed: {exc}.") from None
    try:
        info = _revalidate_named_fd(
            directory_fd,
            leaf,
            lock_fd,
            f"{label} lock",
            require_directory=False,
        )
        if (
            not stat.S_ISREG(info.st_mode)
            or info.st_uid != os.geteuid()
            or info.st_gid != os.getegid()
            or stat.S_IMODE(info.st_mode) != 0o600
            or info.st_nlink != 1
        ):
            raise ExactCaptureRefusal(f"{label} lock metadata is unsafe.")
        try:
            fcntl.flock(lock_fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError:
            raise ExactCaptureRefusal(
                f"another capture coordinator owns {label}."
            ) from None
        _revalidate_named_fd(
            directory_fd,
            leaf,
            lock_fd,
            f"{label} lock post-flock",
            require_directory=False,
        )
        if os.fstat(lock_fd).st_nlink != 1:
            raise ExactCaptureRefusal(f"{label} lock was unlinked after flock.")
        return lock_fd
    except BaseException:
        os.close(lock_fd)
        raise


class AuthorityFirstExactLease:
    """Two-lock lease whose first exact-staging access follows global flock."""

    def __init__(
        self,
        repository_root: pathlib.Path,
        *,
        authority_directory: pathlib.PurePosixPath = GLOBAL_AUTHORITY_DIRECTORY,
        staging_prefix: pathlib.PurePosixPath = EXACT_STAGING_PREFIX,
    ) -> None:
        self.root = repository_root
        self.authority_directory = _validate_lease_relative(
            authority_directory, "authority directory"
        )
        self.staging_prefix = _validate_lease_relative(
            staging_prefix, "staging prefix"
        )
        if (
            self.authority_directory == self.staging_prefix
            or self.authority_directory in self.staging_prefix.parents
            or self.staging_prefix in self.authority_directory.parents
        ):
            raise ExactCaptureRefusal(
                "authority and staging directory chains must not overlap."
            )
        defaults = (
            self.authority_directory == GLOBAL_AUTHORITY_DIRECTORY,
            self.staging_prefix == EXACT_STAGING_PREFIX,
        )
        if defaults[0] != defaults[1]:
            raise ExactCaptureRefusal("production lease paths cannot be partially replaced.")
        self.production_defaults = all(defaults)
        self.authority_fd = -1
        self.authority_lock_fd = -1
        self.staging_fd = -1
        self.staging_lock_fd = -1
        self.root_fd = -1

    def __enter__(self) -> "AuthorityFirstExactLease":
        if any(fd >= 0 for fd in (
            self.authority_fd,
            self.authority_lock_fd,
            self.staging_fd,
            self.staging_lock_fd,
            self.root_fd,
        )):
            raise ExactCaptureRefusal("exact coordinator lease is already entered.")
        if self.production_defaults:
            root = _require_repository_root(self.root)
            if not EXACT_CAPTURE_ENGINE_IMPLEMENTED:
                raise ExactCaptureRefusal(
                    f"{EXACT_CAPTURE_ENGINE_BLOCKER_ID}: production lease is disabled."
                )
        else:
            if self.root == PRODUCTION_REPOSITORY_ROOT:
                raise ExactCaptureRefusal(
                    "audited production root requires exact production lease paths."
                )
            root = _require_custom_lease_root(self.root)
        try:
            root_before = os.lstat(root)
            self.root_fd = os.open(root, _DIRECTORY_OPEN_FLAGS)
            root_opened = os.fstat(self.root_fd)
        except OSError as exc:
            self._close()
            raise ExactCaptureRefusal(f"lease root secure open failed: {exc}.") from None
        if not _same_file_identity(root_before, root_opened):
            self._close()
            raise ExactCaptureRefusal("lease root changed while opened.")
        try:
            _require_open_directory_metadata(
                root_opened,
                "lease root",
                private=not self.production_defaults,
            )
        except BaseException:
            self._close()
            raise
        try:
            self.authority_fd = _secure_open_directory_chain(
                self.root_fd,
                self.authority_directory,
                create=False,
                final_private=True,
                label="global authority directory",
            )
            self.authority_lock_fd = _open_and_lock_at(
                self.authority_fd,
                GLOBAL_AUTHORITY_LOCK_LEAF,
                "global authority",
            )
            _reopen_chain_and_compare(
                self.root_fd,
                self.authority_directory,
                self.authority_fd,
                "global authority directory post-flock",
            )
            _revalidate_absolute_directory_fd(
                root,
                self.root_fd,
                "lease post-authority-flock",
                private=not self.production_defaults,
            )

            # No exact-staging path is evaluated before the global EX flock.
            self.staging_fd = _secure_open_directory_chain(
                self.root_fd,
                self.staging_prefix,
                create=True,
                final_private=True,
                label="exact staging prefix",
            )
            self.staging_lock_fd = _open_and_lock_at(
                self.staging_fd,
                EXACT_STAGING_LOCK_LEAF,
                "exact staging lease",
            )
            _reopen_chain_and_compare(
                self.root_fd,
                self.staging_prefix,
                self.staging_fd,
                "exact staging prefix post-flock",
            )
            _reopen_chain_and_compare(
                self.root_fd,
                self.authority_directory,
                self.authority_fd,
                "global authority directory final",
            )
            _revalidate_named_fd(
                self.authority_fd,
                GLOBAL_AUTHORITY_LOCK_LEAF,
                self.authority_lock_fd,
                "global authority lock final",
                require_directory=False,
            )
            authority_lock_final = os.fstat(self.authority_lock_fd)
            staging_lock_final = _revalidate_named_fd(
                self.staging_fd,
                EXACT_STAGING_LOCK_LEAF,
                self.staging_lock_fd,
                "exact staging lock final",
                require_directory=False,
            )
            if (
                authority_lock_final.st_nlink != 1
                or stat.S_IMODE(authority_lock_final.st_mode) != 0o600
                or staging_lock_final.st_nlink != 1
                or stat.S_IMODE(staging_lock_final.st_mode) != 0o600
            ):
                raise ExactCaptureRefusal("final lock metadata changed after flock.")
            _revalidate_absolute_directory_fd(
                root,
                self.root_fd,
                "lease final",
                private=not self.production_defaults,
            )
            return self
        except BaseException:
            self._close()
            raise

    def __exit__(self, exc_type: object, exc: object, traceback: object) -> None:
        self._close()

    def _close(self) -> None:
        for name in (
            "staging_lock_fd", "staging_fd", "authority_lock_fd", "authority_fd",
            "root_fd",
        ):
            fd = getattr(self, name)
            if fd >= 0:
                try:
                    os.close(fd)
                finally:
                    setattr(self, name, -1)


def _synthetic_process_fingerprint(
    phase: str,
    session_id: str,
) -> dict[str, object]:
    process_ordinal = SESSION_IDS.index(session_id) + 1
    start_times = {
        SESSION_IDS[0]: "1970-01-01T00:00:00.001Z",
        SESSION_IDS[1]: "1970-01-01T00:00:00.011Z",
        SESSION_IDS[2]: "1970-01-01T00:00:00.031Z",
    }
    termination_times = {
        SESSION_IDS[0]: "1970-01-01T00:00:00.009Z",
        SESSION_IDS[1]: "1970-01-01T00:00:00.029Z",
        SESSION_IDS[2]: "1970-01-01T00:00:00.049Z",
    }
    terminated = phase == "post-session"
    return {
        "format": PROCESS_FINGERPRINT_FORMAT,
        "formatVersion": PROCESS_FINGERPRINT_FORMAT_VERSION,
        "identity": {
            "pid": 1_000 + process_ordinal,
            "processStartUtc": start_times[session_id],
            "executableProfileRelativePath": (
                EXPECTED_EXECUTABLE_PROFILE_RELATIVE_PATH
            ),
            "executableSha256": EXPECTED_EXECUTABLE_SHA256,
            "launchNonce": f"{process_ordinal:032x}",
        },
        "termination": {
            "observed": terminated,
            "capturedAtUtc": termination_times[session_id] if terminated else None,
            "exitCode": 0 if terminated else None,
            "signal": None,
        },
    }


def _synthetic_snapshot(
    phase: str,
    session_id: str,
    *,
    prior: Mapping[str, object] | None = None,
    empty: bool = False,
    card_sha256: str = "1" * 64,
    card_inode: int = 2,
    run_id: str = OFFLINE_FIXTURE_RUN_ID,
    captured_at_utc: str = "1970-01-01T00:00:00.000Z",
) -> dict[str, object]:
    if (
        not _SHA256_RE.fullmatch(card_sha256)
        or RUN_ID_RE.fullmatch(run_id) is None
        or UTC_MILLISECONDS_RE.fullmatch(captured_at_utc) is None
    ):
        raise ExactCaptureRefusal("synthetic card SHA-256 is malformed.")
    phase_leaf = "pre" if phase == "pre-session" else "post"
    artifact_id = f"card.{run_id}.{session_id}.{phase_leaf}"
    entry = {
        "leaf": CARD_LEAF,
        "byteLength": CARD_SIZE,
        "sha256": card_sha256,
        "dev": 1,
        "ino": card_inode,
        "mode": "0600",
        "uid": os.geteuid(),
        "gid": os.getegid(),
        "nlink": 1,
        "mtimeNs": 3,
        "ctimeNs": 4,
        "archiveArtifactId": artifact_id,
        "archiveRepoRelativePath": (
            EXACT_STAGING_PREFIX.as_posix()
            + f"/{run_id}/cards/{artifact_id}.mcd"
        ),
        "archiveSha256": card_sha256,
    }
    process_fingerprint = (
        None if empty else _synthetic_process_fingerprint(phase, session_id)
    )
    return {
        "format": CARD_SNAPSHOT_FORMAT,
        "formatVersion": CARD_SNAPSHOT_FORMAT_VERSION,
        "runId": run_id,
        "snapshotId": f"card.{run_id}.{session_id}.{phase}",
        "phase": phase,
        "sessionId": session_id,
        "processInstanceId": (
            "not-applicable" if empty else
            process_instance_id_from_fingerprint(process_fingerprint)
        ),
        "processFingerprint": process_fingerprint,
        "capturedAtUtc": captured_at_utc,
        "memcardsDirectory": {
            "dev": 1,
            "ino": 5,
            "mode": "0700",
            "uid": os.geteuid(),
            "gid": os.getegid(),
        },
        "entries": [] if empty else [entry],
        "priorSnapshotSha256": None if prior is None else _sha256_canonical(prior),
        "sourceArchiveReadbackSha256": None if empty else card_sha256,
    }


def build_offline_fixture_bundle() -> dict[str, object]:
    """Build one deterministic no-runtime bundle for adversarial validation."""

    _validate_plan()
    run_id = OFFLINE_FIXTURE_RUN_ID
    snapshots: list[Mapping[str, object]] = []
    empty = _synthetic_snapshot(
        "prelaunch-empty", "preflight", empty=True,
        run_id=run_id, captured_at_utc="1970-01-01T00:00:00.000Z",
    )
    snapshots.append(empty)
    session0_post = _synthetic_snapshot(
        "post-session",
        SESSION_IDS[0],
        prior=empty,
        card_sha256="1" * 64,
        run_id=run_id,
        captured_at_utc="1970-01-01T00:00:00.010Z",
    )
    snapshots.append(session0_post)
    session1_pre = _synthetic_snapshot(
        "pre-session",
        SESSION_IDS[1],
        prior=session0_post,
        card_sha256="1" * 64,
        run_id=run_id,
        captured_at_utc="1970-01-01T00:00:00.020Z",
    )
    snapshots.append(session1_pre)
    session1_post = _synthetic_snapshot(
        "post-session",
        SESSION_IDS[1],
        prior=session1_pre,
        card_sha256="2" * 64,
        run_id=run_id,
        captured_at_utc="1970-01-01T00:00:00.030Z",
    )
    snapshots.append(session1_post)
    session2_pre = _synthetic_snapshot(
        "pre-session",
        SESSION_IDS[2],
        prior=session1_post,
        card_sha256="2" * 64,
        run_id=run_id,
        captured_at_utc="1970-01-01T00:00:00.040Z",
    )
    snapshots.append(session2_pre)
    session2_post = _synthetic_snapshot(
        "post-session",
        SESSION_IDS[2],
        prior=session2_pre,
        card_sha256="3" * 64,
        run_id=run_id,
        captured_at_utc="1970-01-01T00:00:00.050Z",
    )
    snapshots.append(session2_post)

    snapshot_hashes = tuple(card_snapshot_sha256(snapshot) for snapshot in snapshots)
    pre_hashes: tuple[str | None, ...] = (
        None,
        snapshot_hashes[2],
        snapshot_hashes[4],
    )
    post_hashes = (snapshot_hashes[1], snapshot_hashes[3], snapshot_hashes[5])
    journals: list[dict[str, object]] = []
    for ordinal, session_id in enumerate(SESSION_IDS):
        writer = AppendOnlyJournal(run_id, session_id, ordinal)
        clock = 1_000
        writer.append(
            "session-open",
            {
                "sessionId": session_id,
                "preCardSnapshotSha256": pre_hashes[ordinal],
            },
            clock,
        )
        if pre_hashes[ordinal] is not None:
            clock += 1_000
            writer.append(
                "card-snapshot-bound",
                {
                    "role": "session-pre",
                    "snapshotSha256": pre_hashes[ordinal],
                },
                clock,
            )
        for checkpoint_ordinal, checkpoint_id in enumerate(
            OPERATOR_CHECKPOINTS[session_id]
        ):
            clock += 1_000
            writer.append(
                "operator-checkpoint",
                {"checkpointId": checkpoint_id, "ordinal": checkpoint_ordinal},
                clock,
            )
        for binding in CHECK_BINDINGS:
            if CHECK_BINDING_OWNER_SESSION[binding.check_id] != session_id:
                continue
            clock += 1_000
            writer.append(
                "check-bound",
                {
                    "checkId": binding.check_id,
                    "sessionIds": list(binding.session_ids),
                    "evidenceRoles": [
                        {"roleId": role.role_id, "sessionId": role.session_id}
                        for role in binding.evidence_roles
                    ],
                },
                clock,
            )
        clock += 1_000
        writer.append(
            "card-snapshot-bound",
            {
                "role": "session-post",
                "snapshotSha256": post_hashes[ordinal],
            },
            clock,
        )
        clock += 1_000
        writer.append(
            "session-sealed",
            {"postCardSnapshotSha256": post_hashes[ordinal]},
            clock,
        )
        journals.append(writer.document())

    return {
        "origin": {
            "format": RUN_ORIGIN_FORMAT,
            "formatVersion": 1,
            "runId": run_id,
            "orderedSessionIds": list(SESSION_IDS),
            "checkIds": list(EVENTUAL_CHECK_IDS),
            "exactPlanSha256": EXACT_PLAN_SHA256,
            "exactChecklistSha256": EXACT_CHECKLIST_SHA256,
            "profileId": PROFILE_ID,
            "operationalToolPinsSha256": OPERATIONAL_TOOL_PINS_SHA256,
            "profilePinSetSha256": PROFILE_PIN_SET_SHA256,
            "profileReceiptClosureSha256": (
                EXPECTED_PROFILE_RECEIPT_CLOSURE_SHA256
            ),
            "schemaClosureSha256": SCHEMA_CLOSURE_SHA256,
            "sourceDeclarationClosureSha256": (
                SOURCE_DECLARATION_CLOSURE_SHA256
            ),
            "workflowDeclarationClosureSha256": (
                WORKFLOW_DECLARATION_CLOSURE_SHA256
            ),
            "declarationClosuresBindCoordinatorBytes": False,
            "profilePinsVerified": False,
            "saveProfilePreflightRun": False,
            "runtimeObserved": False,
            "productionCaptureEnabled": False,
            "musicAudioTrackValidated": MUSIC_AUDIO_TRACK_VALIDATED,
            "musicRequiresLiveOperatorAttestation": (
                MUSIC_REQUIRES_LIVE_OPERATOR_ATTESTATION
            ),
        },
        "journals": journals,
        "cardSnapshots": snapshots,
    }


def run_offline_self_test(repository_root: pathlib.Path) -> None:
    root = _require_repository_root(repository_root)
    _validate_plan()
    for pin in OPERATIONAL_TOOL_PINS:
        _verify_tool_pin(root, pin)
    empty = _synthetic_snapshot("prelaunch-empty", "preflight", empty=True)
    validate_card_snapshot(
        empty,
        expected_phase="prelaunch-empty",
        expected_session_id="preflight",
    )
    post = _synthetic_snapshot(
        "post-session", SESSION_IDS[0], prior=empty,
        captured_at_utc="1970-01-01T00:00:00.010Z",
    )
    validate_card_snapshot(
        post,
        expected_phase="post-session",
        expected_session_id=SESSION_IDS[0],
        prior_snapshot=empty,
    )
    pre = _synthetic_snapshot(
        "pre-session", SESSION_IDS[1], prior=post,
        captured_at_utc="1970-01-01T00:00:00.020Z",
    )
    validate_card_snapshot(
        pre,
        expected_phase="pre-session",
        expected_session_id=SESSION_IDS[1],
        prior_snapshot=post,
    )
    fixture_bundle = build_offline_fixture_bundle()
    validate_run_bundle(fixture_bundle)
    premature_restart = json.loads(_canonical_json(fixture_bundle["cardSnapshots"]))
    for snapshot_index in (2, 3):
        identity = premature_restart[snapshot_index]["processFingerprint"]["identity"]
        identity["processStartUtc"] = "1970-01-01T00:00:00.008Z"
        premature_restart[snapshot_index]["processInstanceId"] = (
            process_instance_id_from_fingerprint(
                premature_restart[snapshot_index]["processFingerprint"]
            )
        )
    for snapshot_index in range(1, len(premature_restart)):
        premature_restart[snapshot_index]["priorSnapshotSha256"] = (
            card_snapshot_sha256(premature_restart[snapshot_index - 1])
        )
    try:
        validate_card_chain(premature_restart)
        raise ExactCaptureRefusal("premature clean restart unexpectedly passed.")
    except ExactCaptureRefusal as exc:
        if "before the prior clean termination" not in str(exc):
            raise
    nonce_variant = json.loads(
        _canonical_json(fixture_bundle["cardSnapshots"][2]["processFingerprint"])
    )
    nonce_variant["identity"]["launchNonce"] = "f" * 32
    if (
        process_instance_id_from_fingerprint(nonce_variant)
        == fixture_bundle["cardSnapshots"][2]["processInstanceId"]
        or os_process_identity_sha256_from_fingerprint(nonce_variant)
        != os_process_identity_sha256_from_fingerprint(
            fixture_bundle["cardSnapshots"][2]["processFingerprint"]
        )
    ):
        raise ExactCaptureRefusal(
            "launch nonce did not separate instance ID from OS process identity."
        )
    invalid_date = json.loads(_canonical_json(post))
    invalid_date["capturedAtUtc"] = "1970-02-30T00:00:00.010Z"
    try:
        validate_card_snapshot(
            invalid_date,
            expected_phase="post-session",
            expected_session_id=SESSION_IDS[0],
            prior_snapshot=empty,
        )
        raise ExactCaptureRefusal("nonexistent UTC date unexpectedly passed.")
    except ExactCaptureRefusal as exc:
        if "real UTC" not in str(exc):
            raise
    unclean_exit = json.loads(_canonical_json(post))
    unclean_exit["processFingerprint"]["termination"]["exitCode"] = 1
    try:
        validate_card_snapshot(
            unclean_exit,
            expected_phase="post-session",
            expected_session_id=SESSION_IDS[0],
            prior_snapshot=empty,
        )
        raise ExactCaptureRefusal("unclean process exit unexpectedly passed.")
    except ExactCaptureRefusal as exc:
        if "clean exit" not in str(exc):
            raise
    forged_process_id = json.loads(_canonical_json(post))
    forged_process_id["processInstanceId"] = "dsproc-" + "f" * 64
    try:
        validate_card_snapshot(
            forged_process_id,
            expected_phase="post-session",
            expected_session_id=SESSION_IDS[0],
            prior_snapshot=empty,
        )
        raise ExactCaptureRefusal("forged process instance ID unexpectedly passed.")
    except ExactCaptureRefusal as exc:
        if "fingerprint-derived" not in str(exc):
            raise

    try:
        with AuthorityFirstExactLease(root):
            raise ExactCaptureRefusal("disabled production lease unexpectedly passed.")
    except ExactCaptureRefusal as exc:
        if EXACT_CAPTURE_ENGINE_BLOCKER_ID not in str(exc):
            raise

    temporary_parent = pathlib.Path(tempfile.gettempdir()).resolve(strict=True)
    with tempfile.TemporaryDirectory(
        prefix="id65-authoring-aggregate-exact-v1-lease-",
        dir=temporary_parent,
    ) as temporary:
        temporary_root = pathlib.Path(temporary)
        temporary_root.chmod(0o700)
        authority_relative = pathlib.PurePosixPath("authority")
        authority = temporary_root / authority_relative
        authority.mkdir(mode=0o700)
        staging_relative = pathlib.PurePosixPath("exact/capture-staging")
        with AuthorityFirstExactLease(
            temporary_root,
            authority_directory=authority_relative,
            staging_prefix=staging_relative,
        ) as lease:
            if lease.authority_lock_fd < 0 or lease.staging_lock_fd < 0:
                raise ExactCaptureRefusal("temporary exact lease did not hold both locks.")
            try:
                with AuthorityFirstExactLease(
                    temporary_root,
                    authority_directory=authority_relative,
                    staging_prefix=staging_relative,
                ):
                    raise ExactCaptureRefusal("contending exact lease unexpectedly succeeded.")
            except ExactCaptureRefusal as exc:
                if "global authority" not in str(exc):
                    raise

        for unsafe_relative in (
            pathlib.PurePosixPath("../escaped"),
            pathlib.PurePosixPath("/absolute/escaped"),
        ):
            try:
                AuthorityFirstExactLease(
                    temporary_root,
                    authority_directory=unsafe_relative,
                    staging_prefix=staging_relative,
                )
                raise ExactCaptureRefusal("unsafe lease relative unexpectedly passed.")
            except ExactCaptureRefusal as exc:
                if "safe relative" not in str(exc):
                    raise

        symlink_target = temporary_root / "symlink-target"
        symlink_target.mkdir(mode=0o700)
        symlink_authority = temporary_root / "symlink-authority"
        os.symlink(symlink_target, symlink_authority)
        try:
            with AuthorityFirstExactLease(
                temporary_root,
                authority_directory=pathlib.PurePosixPath("symlink-authority"),
                staging_prefix=pathlib.PurePosixPath("symlink-staging"),
            ):
                raise ExactCaptureRefusal("symlink authority unexpectedly passed.")
        except ExactCaptureRefusal as exc:
            if "non-directory" not in str(exc) and "traversal" not in str(exc):
                raise

        authority_lock = authority / GLOBAL_AUTHORITY_LOCK_LEAF
        os.link(authority_lock, authority / "split-lock-alias")
        try:
            with AuthorityFirstExactLease(
                temporary_root,
                authority_directory=authority_relative,
                staging_prefix=staging_relative,
            ):
                raise ExactCaptureRefusal("split authority lock unexpectedly passed.")
        except ExactCaptureRefusal as exc:
            if "metadata" not in str(exc):
                raise

    with tempfile.TemporaryDirectory(
        prefix="id65-authoring-aggregate-exact-v1-rebind-",
        dir=temporary_parent,
    ) as temporary:
        temporary_root = pathlib.Path(temporary)
        temporary_root.chmod(0o700)
        authority_relative = pathlib.PurePosixPath("authority")
        authority = temporary_root / authority_relative
        authority.mkdir(mode=0o700)
        staging_relative = pathlib.PurePosixPath("staging")
        original_flock = fcntl.flock
        rebound = False

        def rebind_after_authority_flock(fd: int, operation: int) -> object:
            nonlocal rebound
            result = original_flock(fd, operation)
            if operation & fcntl.LOCK_EX and not rebound:
                rebound = True
                os.rename(authority, temporary_root / "authority-rebound-old")
                authority.mkdir(mode=0o700)
            return result

        fcntl.flock = rebind_after_authority_flock
        try:
            try:
                with AuthorityFirstExactLease(
                    temporary_root,
                    authority_directory=authority_relative,
                    staging_prefix=staging_relative,
                ):
                    raise ExactCaptureRefusal(
                        "authority directory rebind unexpectedly passed."
                    )
            except ExactCaptureRefusal as exc:
                if "rebound" not in str(exc) and "identity" not in str(exc):
                    raise
        finally:
            fcntl.flock = original_flock

    with tempfile.TemporaryDirectory(
        prefix="id65-authoring-aggregate-exact-v1-root-mode-",
        dir=temporary_parent,
    ) as temporary:
        temporary_root = pathlib.Path(temporary)
        temporary_root.chmod(0o700)
        authority_relative = pathlib.PurePosixPath("authority")
        authority = temporary_root / authority_relative
        authority.mkdir(mode=0o700)
        staging_relative = pathlib.PurePosixPath("staging")
        original_flock = fcntl.flock
        loosened = False

        def loosen_root_after_authority_flock(fd: int, operation: int) -> object:
            nonlocal loosened
            result = original_flock(fd, operation)
            if operation & fcntl.LOCK_EX and not loosened:
                loosened = True
                temporary_root.chmod(0o777)
            return result

        fcntl.flock = loosen_root_after_authority_flock
        try:
            try:
                with AuthorityFirstExactLease(
                    temporary_root,
                    authority_directory=authority_relative,
                    staging_prefix=staging_relative,
                ):
                    raise ExactCaptureRefusal("unsafe root mode unexpectedly passed.")
            except ExactCaptureRefusal as exc:
                if "safe current-user mode 0700" not in str(exc):
                    raise
        finally:
            fcntl.flock = original_flock

    with tempfile.TemporaryDirectory(
        prefix="id65-authoring-aggregate-exact-v1-split-lock-",
        dir=temporary_parent,
    ) as temporary:
        temporary_root = pathlib.Path(temporary)
        temporary_root.chmod(0o700)
        authority_relative = pathlib.PurePosixPath("authority")
        authority = temporary_root / authority_relative
        authority.mkdir(mode=0o700)
        staging_relative = pathlib.PurePosixPath("staging")
        original_flock = fcntl.flock
        split = False

        def split_after_authority_flock(fd: int, operation: int) -> object:
            nonlocal split
            result = original_flock(fd, operation)
            if operation & fcntl.LOCK_EX and not split:
                split = True
                lock_path = authority / GLOBAL_AUTHORITY_LOCK_LEAF
                os.unlink(lock_path)
                replacement_fd = os.open(
                    lock_path,
                    os.O_RDWR | os.O_CREAT | os.O_EXCL,
                    0o600,
                )
                os.close(replacement_fd)
            return result

        fcntl.flock = split_after_authority_flock
        try:
            try:
                with AuthorityFirstExactLease(
                    temporary_root,
                    authority_directory=authority_relative,
                    staging_prefix=staging_relative,
                ):
                    raise ExactCaptureRefusal("split lock unexpectedly passed.")
            except ExactCaptureRefusal as exc:
                if "identity" not in str(exc) and "unlinked" not in str(exc):
                    raise
        finally:
            fcntl.flock = original_flock


def _static_status() -> dict[str, object]:
    _validate_plan()
    return {
        "format": "spyro-id65-authoring-aggregate-exact-v1-capture-status",
        "formatVersion": 1,
        "profileId": PROFILE_ID,
        "profileRelativePath": PROFILE_RELATIVE_PATH.as_posix(),
        "sessionIds": list(SESSION_IDS),
        "sessionCount": len(SESSION_IDS),
        "eventualCheckIds": list(EVENTUAL_CHECK_IDS),
        "eventualCheckCount": len(EVENTUAL_CHECK_IDS),
        "operatorCheckpointCount": sum(map(len, OPERATOR_CHECKPOINTS.values())),
        "exactPlanSha256": EXACT_PLAN_SHA256,
        "exactChecklistSha256": EXACT_CHECKLIST_SHA256,
        "operationalToolPinsSha256": OPERATIONAL_TOOL_PINS_SHA256,
        "profilePinSetSha256": PROFILE_PIN_SET_SHA256,
        "profileReceiptClosureSha256": EXPECTED_PROFILE_RECEIPT_CLOSURE_SHA256,
        "schemaClosureSha256": SCHEMA_CLOSURE_SHA256,
        "sourceDeclarationClosureSha256": SOURCE_DECLARATION_CLOSURE_SHA256,
        "workflowDeclarationClosureSha256": (
            WORKFLOW_DECLARATION_CLOSURE_SHA256
        ),
        "declarationClosureScope": (
            "declared-plan-schema-profile-and-tool-pins-only"
        ),
        "coordinatorBytePinExternallyValidated": False,
        "saveCapableProfilePinsDeclared": True,
        "profilePinDeclarationValidatorImplemented": True,
        "profilePinsVerified": False,
        "saveProfilePreflightRun": False,
        "cardSnapshotSchemaImplemented": True,
        "snapshotRunBindingImplemented": True,
        "cardChainValidatorImplemented": True,
        "appendOnlyJournalSchemaImplemented": True,
        "journalCardBindingsImplemented": True,
        "operatorCheckpointEventValidationImplemented": True,
        "runBundleValidatorImplemented": True,
        "evidenceRoleArtifactsValidated": False,
        "evidenceRoleSessionOwnershipImplemented": True,
        "candidateJumpLandingRoleCoverageDeclared": True,
        "retailTownSquareTerrainCollisionMobySaveExitCoverageDeclared": True,
        "retailTownSquareMusic60500msLiveAttestationDeclared": True,
        "authorityFirstLeaseImplemented": True,
        "productionLeaseApiEnabled": False,
        "leaseSymlinkParentRaceHardeningImplemented": True,
        "leaseFdRelativeNoFollowTraversalImplemented": True,
        "leasePostFlockIdentityRevalidationImplemented": True,
        "lockedConsoleLiveProbeImplemented": False,
        "runtimeCardCaptured": False,
        "structuredProcessFingerprintSchemaImplemented": True,
        "processInstanceIdFingerprintDerived": True,
        "osProcessIdentityLaunchNonceIndependent": True,
        "nextProcessStartsAfterPriorCleanExitEnforced": True,
        "cleanExitCardChainSemanticsImplemented": True,
        "runtimeProcessFingerprintObserved": False,
        "semanticUtcMillisecondsValidationImplemented": True,
        "cardLeafSizeProvenance": CARD_LEAF_SIZE_PROVENANCE,
        "cardBytesAuthority": CARD_BYTES_AUTHORITY,
        "historicalHostCardBytesForbidden": True,
        "musicAudioTrackValidated": MUSIC_AUDIO_TRACK_VALIDATED,
        "musicRequiresLiveOperatorAttestation": (
            MUSIC_REQUIRES_LIVE_OPERATOR_ATTESTATION
        ),
        "exactCaptureEngineImplemented": EXACT_CAPTURE_ENGINE_IMPLEMENTED,
        "productionCaptureEnabled": False,
        "productionBlockerId": EXACT_CAPTURE_ENGINE_BLOCKER_ID,
        "productionRequiredGate": EXACT_CAPTURE_ENGINE_REQUIRED_GATE,
        "duckStationLaunched": False,
        "runtimeObserved": False,
        "runtimeProven": False,
        "evidenceAccepted": False,
        "createBinAuthorized": False,
        "releaseAuthorized": False,
        "publishable": False,
        "captureStagingRelativePath": EXACT_STAGING_PREFIX.as_posix(),
        "focusedEvidenceRelativePath": FOCUSED_EVIDENCE_RELATIVE_PATH.as_posix(),
    }


def _refuse_live_operation(operation: str) -> None:
    raise ExactCaptureRefusal(
        f"{EXACT_CAPTURE_ENGINE_BLOCKER_ID}: {operation} is fail-closed until "
        f"{EXACT_CAPTURE_ENGINE_REQUIRED_GATE} is implemented and independently audited; "
        "the exact plan/card schema exists, but no live engine, DuckStation launch, "
        "persistent staging, runtime evidence, or authorization is available."
    )


def _build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    subparsers = parser.add_subparsers(dest="operation", required=True)
    subparsers.add_parser("self-test")
    subparsers.add_parser("status")
    subparsers.add_parser("preflight")
    subparsers.add_parser("capture-run")
    resume = subparsers.add_parser("resume-run")
    resume.add_argument("run_id")
    verify = subparsers.add_parser("verify-staging")
    verify.add_argument("run_id")
    quarantine = subparsers.add_parser("quarantine-abort")
    quarantine.add_argument("run_id")
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    arguments = _build_parser().parse_args(argv)
    try:
        if arguments.operation == "self-test":
            run_offline_self_test(PRODUCTION_REPOSITORY_ROOT)
            print("ID65_AUTHORING_AGGREGATE_EXACT_V1_CAPTURE_SELF_TEST_OK")
            return 0
        if arguments.operation == "status":
            print(_canonical_json(_static_status()))
            return 0
        _refuse_live_operation(arguments.operation)
        raise AssertionError("unreachable")
    except ExactCaptureRefusal as exc:
        print(f"REFUSED: {exc}", file=sys.stderr)
        return 2
    except Exception as exc:
        print(
            f"REFUSED: unexpected exact capture failure: {type(exc).__name__}: {exc}",
            file=sys.stderr,
        )
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
