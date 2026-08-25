#!/usr/bin/env python3
"""Manual-only runtime capture for the ID65 authoring aggregate V1.

This adapter hash-pins and loads the frozen V3 capture engine without changing
that file.  It replaces only the candidate/profile/evidence namespaces and
their immutable pins.  DuckStation is never launched, resumed, controlled, or
quit by this program: the operator performs those actions at the frozen
engine's explicit Terminal checkpoints.  The old automation program is not a
dependency, is not part of the source closure, and is never loaded.

Capture output is raw append-only staging, not accepted runtime evidence.  The
separate final evidence document remains unavailable until the complete staged
run is independently finalized and attested.  Live production modes currently
fail closed at two independent boundaries: the pinned no-memory-card profile
cannot satisfy clean-restart in-game save persistence, and the frozen engine's
seven-session/lone-pad workflow is not the exact aggregate 17-check / Town
Square-only acceptance scope.  Static status and self-test bind both blockers
without opening the profile or candidate.
"""

from __future__ import annotations

import argparse
import contextlib
import dataclasses
import fcntl
import hashlib
import json
import os
import pathlib
import re
import stat
import sys
import tempfile
import types
from collections.abc import Mapping, Sequence
from typing import Any, Final


class AggregateCaptureRefusal(RuntimeError):
    """The aggregate capture boundary was not satisfied exactly."""


SCRIPT_PATH: Final = pathlib.Path(os.path.abspath(__file__))
PRODUCTION_REPOSITORY_ROOT: Final = SCRIPT_PATH.parent.parent

CAPTURE_COORDINATOR_RELATIVE_PATH: Final = pathlib.PurePosixPath(
    "tools/Capture-SpyroId65AuthoringAggregateV1RuntimeSession.py"
)
FROZEN_CAPTURE_RELATIVE_PATH: Final = pathlib.PurePosixPath(
    "tools/Capture-SpyroId65V3RuntimeSession.py"
)
EXPECTED_FROZEN_CAPTURE_SHA256: Final = (
    "153871778877a8736a1243ce2e84453e4d0d8e2fb945f9e96fdc01fbf00278ec"
)
EXPECTED_FROZEN_CAPTURE_SIZE: Final = 473_144
WATCHER_RELATIVE_PATH: Final = pathlib.PurePosixPath(
    "tools/Watch-SpyroId65RuntimeState.py"
)
EXPECTED_WATCHER_SHA256: Final = (
    "eac3953a0ff7ff8acdefbe43cf987f296d44091ad51619efd225690ba1dc0677"
)
PROFILE_BUILDER_RELATIVE_PATH: Final = pathlib.PurePosixPath(
    "tools/Build-SpyroId65AuthoringAggregateV1Profile.py"
)
EXPECTED_PROFILE_BUILDER_SHA256: Final = (
    "5e344a097b1cd79c50cdd5d07b0a3c818b457f35f33b46d1677fa55487aa85a2"
)
PROFILE_VERIFIER_RELATIVE_PATH: Final = pathlib.PurePosixPath(
    "tools/Verify-SpyroId65AuthoringAggregateV1Profile.py"
)
EXPECTED_PROFILE_VERIFIER_SHA256: Final = (
    "ba1b461193c7a67fd8522c72fd9141e725ec3063791d44d2a82d96e0a01a344c"
)
FORBIDDEN_AUTOMATION_RELATIVE_PATH: Final = pathlib.PurePosixPath(
    "tools/Automate-SpyroId65V3RuntimeSession.py"
)

PROFILE_ID: Final = "duckstation-id65-authoring-aggregate-v1"
PROFILE_PACKAGE_RELATIVE_PATH: Final = pathlib.PurePosixPath(
    "_local/v5-stone-hill-level-replacement/"
    "duckstation-id65-authoring-aggregate-v1"
)
PACKAGED_APP_LEAF: Final = "DuckStation-ID65-Unattended-v1.app"
PACKAGED_APP_RELATIVE_PATH: Final = (
    PROFILE_PACKAGE_RELATIVE_PATH / PACKAGED_APP_LEAF
)
DUCKSTATION_EXECUTABLE_RELATIVE_PATH: Final = (
    PACKAGED_APP_RELATIVE_PATH / "Contents/MacOS/DuckStation"
)
EXTERNAL_HOME_RELATIVE_PATH: Final = PROFILE_PACKAGE_RELATIVE_PATH / "home"
EXTERNAL_HOME_DATA_RELATIVE_PATH: Final = (
    EXTERNAL_HOME_RELATIVE_PATH / "Library/Application Support/DuckStation"
)
HANDOFF_RECEIPT_RELATIVE_PATH: Final = (
    PROFILE_PACKAGE_RELATIVE_PATH / "handoff-receipt.json"
)

CANDIDATE_DIRECTORY_RELATIVE_PATH: Final = pathlib.PurePosixPath(
    "_local/id65-authoring-aggregate-workspace/_research/"
    "unusedlevel65blank/v1/materialized-candidates/"
    "aggregate-4955c54b3eb7ea5c1871205e559f5d5dda0766613e55aa0de988b92e896d5c21-"
    "receipt-d6c14861cde023e9e100fe2828227da8252c9de37c36e84d33ba4bfbd3b0516f"
)
CANDIDATE_STEM: Final = "unusedlevel65blank-authoring-aggregate-candidate-v1"
CANDIDATE_CUE_RELATIVE_PATH: Final = (
    CANDIDATE_DIRECTORY_RELATIVE_PATH / f"{CANDIDATE_STEM}.cue"
)
CANDIDATE_BIN_RELATIVE_PATH: Final = (
    CANDIDATE_DIRECTORY_RELATIVE_PATH / f"{CANDIDATE_STEM}.bin"
)
CANDIDATE_RECEIPT_RELATIVE_PATH: Final = (
    CANDIDATE_DIRECTORY_RELATIVE_PATH / f"{CANDIDATE_STEM}.receipt.json"
)
DOCUMENT_RELATIVE_PATH: Final = pathlib.PurePosixPath(
    "_local/id65-authoring-aggregate-workspace/_research/unusedlevel65blank/"
    "v1/authored-edits/unusedlevel65blank-authoring-document-v1.json"
)

EXPECTED_CANDIDATE_CUE_SIZE: Final = 114
EXPECTED_CANDIDATE_CUE_SHA256: Final = (
    "91c12af236642fc8ad601995cea86d22c414eb1b692a4957cc3f9fa3aedd6de8"
)
EXPECTED_CANDIDATE_BIN_SIZE: Final = 661_547_040
EXPECTED_CANDIDATE_BIN_SHA256: Final = (
    "083dd5b7e258b59010e87c3262274e4da9cb86b1a6a15e7730a8b5eeac001647"
)
EXPECTED_CANDIDATE_RECEIPT_FILE_SHA256: Final = (
    "9f11a3811a54ef4932c5cb4d184877fe957f06eb003680c739d2478c6e7ed03b"
)
EXPECTED_CANDIDATE_RECEIPT_CONTENT_SHA256: Final = (
    "d6c14861cde023e9e100fe2828227da8252c9de37c36e84d33ba4bfbd3b0516f"
)
EXPECTED_DOCUMENT_ENVELOPE_SHA256: Final = (
    "5669f5e29010009fa7a698f4eed213dbd7163bd3483ec37beba7732bda321b70"
)
EXPECTED_DOCUMENT_CANONICAL_SHA256: Final = (
    "8f27bfc3f97238402c0b7dd59a081335ae19df1acfaefa9575d9ca7110dce808"
)
EXPECTED_AGGREGATE_PLAN_SHA256: Final = (
    "4955c54b3eb7ea5c1871205e559f5d5dda0766613e55aa0de988b92e896d5c21"
)
EXPECTED_OUTPUT_ROW80_SHA256: Final = (
    "dad25853f92e4f8300036edd85dae374672d265ba17c7b2682c6b524661b75f3"
)

EXPECTED_DUCKSTATION_SHA256: Final = (
    "14c22c6e1f4bbff13c00c24f17424ef8cf7645ee9917f7d65b17cd3fe1dd1645"
)
EXPECTED_DUCKSTATION_SIZE: Final = 22_354_208
EXPECTED_SETTINGS_SHA256: Final = (
    "469ef98771f26395352bac1d43f90e4be14693fd4ba5316e4d71e0557a7f74f9"
)
EXPECTED_SETTINGS_SIZE: Final = 2_143
EXPECTED_BIOS_SHA256: Final = (
    "71af94d1e47a68c11e8fdb9f8368040601514a42a5a399cda48c7d3bff1e99d3"
)
EXPECTED_BIOS_SIZE: Final = 524_288
EXPECTED_HANDOFF_RECEIPT_SHA256: Final = (
    "c35c4c2b74f3481206737fb8f310bb5478a6f00df0d53655d9e4ac14230a1fe2"
)
EXPECTED_HANDOFF_RECEIPT_SIZE: Final = 74_510
EXPECTED_RECEIPT_CLOSURE_SHA256: Final = (
    "1f826b36b14ee330330cf574e8080a5f744d7b8c5da6e75475d593ec81b19c7f"
)
EXPECTED_APP_CONTENT_MANIFEST_SHA256: Final = (
    "95a8142e561d6b37f228728abc8dbf034c774887213eb59bc14518d56c2d222e"
)
EXPECTED_APP_OUTPUT_TREE_MANIFEST_SHA256: Final = (
    "08d0a74450b709ab7e702182d64db55f6bc3395728181b052fec307c4d320ba7"
)
EXPECTED_HOME_OUTPUT_TREE_MANIFEST_SHA256: Final = (
    "b00119fd7647647c81c7571d2507dbbce2075bd10cdcd8af8db8ae1e006fc2b0"
)
EXPECTED_PACKAGE_ENVELOPE_SHA256: Final = (
    "31f24a6d8ea38fa19abeae54392fa96e2494b5cfb0a5621d2cc29771a617f866"
)
EXPECTED_CANDIDATE_MANIFEST_SHA256: Final = (
    "44f74dcefab535670d4abe0e1b1bcec35b832dec421d9caea8c6c132b860681d"
)
EXPECTED_NORMAL_SETTINGS_SHA256: Final = (
    "896daa612142659aa950c16d2435f81cb034aa8b56768b91e60a4ec8f0a5ccc9"
)

CAPTURE_STAGING_PREFIX: Final = pathlib.PurePosixPath(
    "docs/runtime-evidence/assets/"
    "unused-level-65-authoring-aggregate-v1/capture-staging"
)
FINAL_EVIDENCE_RELATIVE_PATH: Final = pathlib.PurePosixPath(
    "docs/runtime-evidence/"
    "unused-level-65-authoring-aggregate-v1-runtime-evidence.json"
)
GLOBAL_AUTHORITY_DIRECTORY: Final = pathlib.PurePosixPath(
    "docs/runtime-evidence/assets/"
    "unused-level-65-remote-blank-environment-header-repair-v3"
)
GLOBAL_AUTHORITY_LOCK_NAME: Final = ".capture-coordinator-authority.lock"
AGGREGATE_STAGING_LOCK_NAME: Final = ".capture-coordinator.lock"

PROFILE_FORMAT: Final = "spyro-editor-id65-v3-runtime-profile-snapshot"
PROFILE_FORMAT_VERSION: Final = 2
PREFLIGHT_FORMAT: Final = "spyro-id65-authoring-aggregate-profile-preflight"
PREFLIGHT_FORMAT_VERSION: Final = 1
CAPTURE_FORMAT: Final = (
    "spyro-editor-id65-authoring-aggregate-v1-runtime-capture-event-journal"
)
RUN_FORMAT: Final = (
    "spyro-editor-id65-authoring-aggregate-v1-runtime-capture-run-origin"
)
LAUNCH_GATE: Final = "EXTERNAL_RUNTIME_LAUNCH_REQUIRED"

# The focused contract requires all of these checks.  The frozen engine still
# supplies its strong journal, process, watcher, media, and profile bookends;
# these additions define the aggregate-specific operator checklist layered on
# top of those primitives.  The old V3 checks remain useful regression data but
# are not treated as substitutes for these exact check identities.
EXPECTED_AGGREGATE_CHECK_IDS: Final = (
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

AGGREGATE_ADDITIONAL_MARKER_PLANS: Final[
    Mapping[str, tuple[tuple[str, int], ...]]
] = {
    "candidate-edge-1": (
        ("candidate.authored-terrain-visible", 5_500_000),
        ("candidate.authored-collision-playable", 8_500_000),
        ("candidate.authored-occlusion-correct", 6_500_000),
        ("candidate.private-texture-swap-visible", 5_500_000),
        ("candidate.grass-zero-egg-placement-present", 5_500_000),
        ("candidate.grass-authored-placement-present", 5_500_000),
        ("candidate.zero-egg-preserved", 5_500_000),
        ("candidate.camera-360-stable", 6_500_000),
        ("candidate.walk-stable", 8_500_000),
        ("candidate.jump-land-stable", 8_500_000),
        ("candidate.music-long-play-stable", 120_500_000),
    ),
    "candidate-edge-2": (
        ("candidate.treasure-total-200", 5_500_000),
        ("candidate.save-created-for-persistence", 8_500_000),
    ),
    "candidate-cold-reset": (
        ("candidate.save-persistence", 8_500_000),
        ("candidate.exit-destination-65", 8_500_000),
    ),
    "retail-town-square": (
        ("retail.town-square-preserved", 60_500_000),
    ),
}

AGGREGATE_MARKER_INSTRUCTIONS: Final[Mapping[str, str]] = {
    "candidate.authored-terrain-visible": (
        "Inspect the authored terrain tile from multiple angles and confirm its "
        "intended geometry is visible without inherited test-pad geometry."
    ),
    "candidate.authored-collision-playable": (
        "Walk, charge, and jump across the authored terrain and its boundaries; "
        "confirm stable support and no pass-through, snag, hover, or invisible wall."
    ),
    "candidate.authored-occlusion-correct": (
        "Move the player and rotate the camera around the authored tile; confirm "
        "faces appear and disappear only when physically occluded."
    ),
    "candidate.private-texture-swap-visible": (
        "Hold the authored destination face in view and confirm the intended private "
        "texture swap is visibly applied only to that destination."
    ),
    "candidate.grass-zero-egg-placement-present": (
        "Locate the row-replaced grass placement at true index 88 and keep it clearly "
        "visible at its intended support."
    ),
    "candidate.grass-authored-placement-present": (
        "Locate the appended grass placement at true index 107 and keep it clearly "
        "visible at its authored support."
    ),
    "candidate.zero-egg-preserved": (
        "Inspect both authored grass placements and confirm neither produces an egg "
        "or changes the intended zero-egg state."
    ),
    "candidate.camera-360-stable": (
        "Rotate the camera through a complete circle in both directions around the "
        "authored area and return to a stable view."
    ),
    "candidate.walk-stable": (
        "Walk the authored center and complete perimeter in both directions and "
        "confirm continuous stable support."
    ),
    "candidate.jump-land-stable": (
        "Jump and land at the authored center and near each boundary, remaining on "
        "the intended visible surface after every landing."
    ),
    "candidate.music-long-play-stable": (
        "Remain in controllable authored-level gameplay for the full minimum while "
        "confirming the intended music remains correct and uninterrupted."
    ),
    "candidate.treasure-total-200": (
        "Open both the in-level and pause totals displays and confirm each reports "
        "exactly 200 treasure for the authored level."
    ),
    "candidate.save-created-for-persistence": (
        "Create an ordinary in-game save using the normal game route, without a "
        "save-state, and return to stable controllable gameplay before process exit."
    ),
    "candidate.save-persistence": (
        "After this clean process restart, load the ordinary in-game save and confirm "
        "the authored-level progress created in the prior session persisted; do not "
        "use a save-state."
    ),
    "candidate.exit-destination-65": (
        "Use the authored level's normal exit, confirm destination ID 65 returns "
        "through the intended route, then reload the authored level and stabilize."
    ),
    "retail.town-square-preserved": (
        "Exercise Town Square terrain, collision, Mobys, totals, exit, music, and "
        "ordinary in-game save behavior; finish in stable controllable gameplay."
    ),
}

# Live production is closed by two independent boundaries.  The current profile
# cannot preserve an ordinary in-game save, and the frozen capture engine still
# mandates its old seven-session/lone-pad workflow.  The latter is useful as
# frozen implementation lineage but is not the exact aggregate 17-check / Town
# Square-only acceptance scope.  Pinning a save-capable profile alone therefore
# must never enable live capture.
PRODUCTION_CAPTURE_ENABLED: Final = False
PRODUCTION_CAPTURE_BLOCKERS: Final = (
    (
        "profile.no-memory-card-save-persistence",
        "candidate.save-persistence",
        "the pinned aggregate profile disables both memory-card slots, so an "
        "ordinary in-game save cannot survive a clean process restart without "
        "save-state use",
    ),
    (
        "workflow.frozen-v3-seven-session-lone-pad-incompatible",
        "aggregate.exact-17-check-town-square-workflow",
        "the frozen capture engine still mandates seven sessions, the legacy "
        "lone-pad visibility and triangular E0/E1/E2 workflow, and Gnasty's Loot "
        "and Sunny Flight controls; that workflow is not the exact 17-check "
        "aggregate and Town Square-only acceptance scope",
    ),
)
PRODUCTION_CAPTURE_BLOCKING_CHECK_ID: Final = "candidate.save-persistence"
PRODUCTION_CAPTURE_BLOCKING_REASON: Final = PRODUCTION_CAPTURE_BLOCKERS[0][2]

FROZEN_WORKFLOW_COMPATIBLE_WITH_AGGREGATE_SCOPE: Final = False
FROZEN_WORKFLOW_RUN_ID_PATTERN: Final = (
    r"\Aid65v3-[0-9]{8}t[0-9]{6}z-[0-9a-f]{16}\Z"
)
FROZEN_WORKFLOW_SESSION_IDS: Final = (
    "candidate-primary-edge-0",
    "candidate-edge-1",
    "candidate-edge-2",
    "candidate-cold-reset",
    "retail-town-square",
    "retail-gnastys-loot",
    "retail-sunny-flight",
)
FROZEN_WORKFLOW_PRIMARY_MARKER_IDS: Final = (
    "candidate.completed-landing-hud4",
    "candidate.hands-off-20-seconds",
    "candidate.camera-360-twice",
    "candidate.walk-center-perimeter-both-directions",
    "candidate.jump-land-center-each-corner",
)
FROZEN_WORKFLOW_EDGE_ASSIGNMENTS: Final = (
    (
        "candidate-primary-edge-0", "E0", "A-B", "rear/base edge",
        "-Y, opposite spawn yaw", "(384,272,512)",
    ),
    (
        "candidate-edge-1", "E1", "B-C", "right-hand/east slope",
        "+X", "(440,384,512)",
    ),
    (
        "candidate-edge-2", "E2", "C-A", "left-hand/west slope",
        "-X", "(328,384,512)",
    ),
)

AGGREGATE_CHECK_BINDINGS: Final[Mapping[str, tuple[str, ...]]] = {
    "candidate.safe-load-and-spawn": (
        "candidate-primary-edge-0:check:candidate.completed-landing-hud4",
        "candidate-primary-edge-0:event:landing-marked",
    ),
    "candidate.authored-terrain-visible": (
        "candidate-edge-1:check:candidate.authored-terrain-visible",
    ),
    "candidate.authored-collision-playable": (
        "candidate-edge-1:check:candidate.authored-collision-playable",
    ),
    "candidate.authored-occlusion-correct": (
        "candidate-edge-1:check:candidate.authored-occlusion-correct",
    ),
    "candidate.private-texture-swap-visible": (
        "candidate-edge-1:check:candidate.private-texture-swap-visible",
    ),
    "candidate.grass-zero-egg-placement-present": (
        "candidate-edge-1:check:candidate.grass-zero-egg-placement-present",
    ),
    "candidate.grass-authored-placement-present": (
        "candidate-edge-1:check:candidate.grass-authored-placement-present",
    ),
    "candidate.zero-egg-preserved": (
        "candidate-edge-1:check:candidate.zero-egg-preserved",
    ),
    "candidate.camera-360-stable": (
        "candidate-edge-1:check:candidate.camera-360-stable",
    ),
    "candidate.walk-stable": (
        "candidate-edge-1:check:candidate.walk-stable",
    ),
    "candidate.jump-land-stable": (
        "candidate-edge-1:check:candidate.jump-land-stable",
    ),
    "candidate.fall-respawn-stable": (
        "candidate-primary-edge-0:event:fall-started",
        "candidate-primary-edge-0:event:respawn-marked",
        "candidate-primary-edge-0:watcher:fall-respawn",
    ),
    "candidate.music-long-play-stable": (
        "candidate-edge-1:check:candidate.music-long-play-stable",
    ),
    "candidate.treasure-total-200": (
        "candidate-edge-2:check:candidate.treasure-total-200",
    ),
    "candidate.exit-destination-65": (
        "candidate-cold-reset:check:candidate.exit-destination-65",
    ),
    "candidate.save-persistence": (
        "candidate-edge-2:check:candidate.save-created-for-persistence",
        "candidate-cold-reset:check:candidate.save-persistence",
    ),
    "retail.town-square-preserved": (
        "retail-town-square:check:retail.town-square-preserved",
    ),
}


def _production_capture_blocker_payload() -> list[dict[str, str]]:
    return [
        {
            "blockerId": blocker_id,
            "requiredGate": required_gate,
            "reason": reason,
        }
        for blocker_id, required_gate, reason in PRODUCTION_CAPTURE_BLOCKERS
    ]


def _frozen_workflow_payload() -> dict[str, object]:
    return {
        "compatibleWithAggregateScope": (
            FROZEN_WORKFLOW_COMPATIBLE_WITH_AGGREGATE_SCOPE
        ),
        "edgeAssignments": [list(item) for item in FROZEN_WORKFLOW_EDGE_ASSIGNMENTS],
        "legacyLonePadFrameRole": "candidate.lone-pad-visibility",
        "primaryMarkerIds": list(FROZEN_WORKFLOW_PRIMARY_MARKER_IDS),
        "runIdPattern": FROZEN_WORKFLOW_RUN_ID_PATTERN,
        "sessionIds": list(FROZEN_WORKFLOW_SESSION_IDS),
    }


def _production_capture_blockers_sha256() -> str:
    return hashlib.sha256(
        json.dumps(
            _production_capture_blocker_payload(),
            allow_nan=False,
            ensure_ascii=False,
            sort_keys=True,
            separators=(",", ":"),
        ).encode("utf-8", "strict")
    ).hexdigest()


def _frozen_workflow_sha256() -> str:
    return hashlib.sha256(
        json.dumps(
            _frozen_workflow_payload(),
            allow_nan=False,
            ensure_ascii=False,
            sort_keys=True,
            separators=(",", ":"),
        ).encode("utf-8", "strict")
    ).hexdigest()


EXPECTED_PRODUCTION_CAPTURE_BLOCKERS_SHA256: Final = (
    _production_capture_blockers_sha256()
)
EXPECTED_FROZEN_WORKFLOW_SHA256: Final = _frozen_workflow_sha256()


def _aggregate_checklist_sha256() -> str:
    value = {
        "bindings": dict(AGGREGATE_CHECK_BINDINGS),
        "checkIds": list(EXPECTED_AGGREGATE_CHECK_IDS),
        "frozenWorkflow": _frozen_workflow_payload(),
        "instructions": dict(AGGREGATE_MARKER_INSTRUCTIONS),
        "plans": {
            key: [list(item) for item in value]
            for key, value in AGGREGATE_ADDITIONAL_MARKER_PLANS.items()
        },
        "productionCaptureBlockers": _production_capture_blocker_payload(),
    }
    canonical = json.dumps(
        value,
        allow_nan=False,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8", "strict")
    return hashlib.sha256(canonical).hexdigest()


EXPECTED_AGGREGATE_CHECKLIST_SHA256: Final = _aggregate_checklist_sha256()

CAPTURE_PIN_KEYS: Final = (
    "watcherRelativePath",
    "watcherSha256",
    "captureCoreRelativePath",
    "captureCoreSha256",
    "profileBuilderRelativePath",
    "profileBuilderSha256",
    "profileVerifierRelativePath",
    "profileVerifierSha256",
    "captureCoordinatorRelativePath",
    "captureCoordinatorSha256",
    "sourceClosureSha256",
    "runtimeProfileSha256",
    "profilePackageRelativePath",
    "profileId",
    "handoffReceiptSha256",
    "handoffReceiptClosureSha256",
    "appContentManifestSha256",
    "appOutputTreeManifestSha256",
    "homeOutputTreeManifestSha256",
    "packageEnvelopeSha256",
    "externalHomeSettingsSha256",
    "externalHomeBiosSha256",
    "hostNormalSettingsSentinelSha256",
    "candidateCueSha256",
    "candidateBinSha256",
    "candidateManifestSha256",
    "candidateReceiptFileSha256",
    "candidateReceiptContentSha256",
    "documentEnvelopeSha256",
    "documentCanonicalSha256",
    "aggregatePlanSha256",
    "outputRow80Sha256",
    "aggregateChecklistSha256",
    "productionCaptureBlockersSha256",
    "frozenWorkflowSha256",
    "frozenWorkflowCompatible",
    "captureStagingRelativePath",
    "finalEvidenceRelativePath",
)

_EXPECTED_SOURCE_MODES: Final[Mapping[pathlib.PurePosixPath, int]] = {
    FROZEN_CAPTURE_RELATIVE_PATH: 0o755,
    WATCHER_RELATIVE_PATH: 0o644,
    PROFILE_BUILDER_RELATIVE_PATH: 0o644,
    PROFILE_VERIFIER_RELATIVE_PATH: 0o644,
}
_EXPECTED_SOURCE_HASHES: Final[Mapping[pathlib.PurePosixPath, str]] = {
    FROZEN_CAPTURE_RELATIVE_PATH: EXPECTED_FROZEN_CAPTURE_SHA256,
    WATCHER_RELATIVE_PATH: EXPECTED_WATCHER_SHA256,
    PROFILE_BUILDER_RELATIVE_PATH: EXPECTED_PROFILE_BUILDER_SHA256,
    PROFILE_VERIFIER_RELATIVE_PATH: EXPECTED_PROFILE_VERIFIER_SHA256,
}
_MAX_SOURCE_BYTES: Final = 1024 * 1024
_READ_CHUNK_BYTES: Final = 1024 * 1024
_SHA256_RE: Final = re.compile(r"\A[0-9a-f]{64}\Z", re.ASCII)


def _sha256_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def _canonical_json_bytes(value: object) -> bytes:
    return json.dumps(
        value,
        allow_nan=False,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8", "strict")


def _reject_symlink_segments(path: pathlib.Path, label: str) -> None:
    if not path.is_absolute() or pathlib.Path(os.path.normpath(os.fspath(path))) != path:
        raise AggregateCaptureRefusal(f"{label} is not absolute and normalized.")
    current = pathlib.Path(path.anchor)
    for part in path.parts[1:]:
        current /= part
        try:
            info = os.lstat(current)
        except FileNotFoundError as exc:
            raise AggregateCaptureRefusal(f"{label} has a missing segment.") from exc
        if stat.S_ISLNK(info.st_mode):
            raise AggregateCaptureRefusal(f"{label} traverses a symbolic link.")


def _require_repository_root(value: pathlib.Path) -> pathlib.Path:
    if not isinstance(value, pathlib.Path):
        raise AggregateCaptureRefusal("repository root must be a pathlib.Path.")
    _reject_symlink_segments(value, "repository root")
    if value != PRODUCTION_REPOSITORY_ROOT:
        raise AggregateCaptureRefusal(
            "repository root must be the audited checkout containing this coordinator."
        )
    return value


def _stable_read_source(
    path: pathlib.Path,
    *,
    expected_sha256: str | None,
    expected_mode: int,
    label: str,
) -> bytes:
    _reject_symlink_segments(path, label)
    flags = os.O_RDONLY | getattr(os, "O_CLOEXEC", 0) | getattr(os, "O_NOFOLLOW", 0)
    fd = os.open(path, flags)
    try:
        before = os.fstat(fd)
        if (
            not stat.S_ISREG(before.st_mode)
            or before.st_nlink != 1
            or before.st_uid != os.geteuid()
            or stat.S_IMODE(before.st_mode) != expected_mode
            or before.st_size > _MAX_SOURCE_BYTES
        ):
            raise AggregateCaptureRefusal(f"{label} metadata is unsafe.")
        digest = hashlib.sha256()
        chunks: list[bytes] = []
        observed = 0
        while True:
            chunk = os.read(fd, _READ_CHUNK_BYTES)
            if not chunk:
                break
            digest.update(chunk)
            chunks.append(chunk)
            observed += len(chunk)
        after = os.fstat(fd)
        stable_fields = (
            "st_dev", "st_ino", "st_mode", "st_uid", "st_gid", "st_nlink",
            "st_size", "st_mtime_ns", "st_ctime_ns",
        )
        if observed != before.st_size or any(
            getattr(before, name) != getattr(after, name) for name in stable_fields
        ):
            raise AggregateCaptureRefusal(f"{label} changed while held open.")
        observed_sha256 = digest.hexdigest()
        if expected_sha256 is not None and observed_sha256 != expected_sha256:
            raise AggregateCaptureRefusal(
                f"{label} hash changed: {observed_sha256}."
            )
        return b"".join(chunks)
    finally:
        os.close(fd)


def _load_module_from_pinned_source(
    repository_root: pathlib.Path,
    relative_path: pathlib.PurePosixPath,
    expected_sha256: str,
    expected_mode: int,
    module_name: str,
) -> types.ModuleType:
    source_path = repository_root / relative_path
    source = _stable_read_source(
        source_path,
        expected_sha256=expected_sha256,
        expected_mode=expected_mode,
        label=os.fspath(relative_path),
    )
    module = types.ModuleType(module_name)
    module.__file__ = os.fspath(source_path)
    module.__package__ = ""
    previous = sys.modules.get(module_name)
    sys.modules[module_name] = module
    try:
        code = compile(source, os.fspath(source_path), "exec", dont_inherit=True)
        exec(code, module.__dict__)
    except BaseException:
        if previous is None:
            sys.modules.pop(module_name, None)
        else:
            sys.modules[module_name] = previous
        raise
    return module


def _runtime_profile_sha256() -> str:
    fields = (
        ("profileId", PROFILE_ID),
        ("profilePackageRelativePath", PROFILE_PACKAGE_RELATIVE_PATH.as_posix()),
        ("packagedAppRelativePath", PACKAGED_APP_RELATIVE_PATH.as_posix()),
        ("duckStationExecutableRelativePath", DUCKSTATION_EXECUTABLE_RELATIVE_PATH.as_posix()),
        ("duckStationExecutableSha256", EXPECTED_DUCKSTATION_SHA256),
        ("handoffReceiptSha256", EXPECTED_HANDOFF_RECEIPT_SHA256),
        ("handoffReceiptClosureSha256", EXPECTED_RECEIPT_CLOSURE_SHA256),
        ("appContentManifestSha256", EXPECTED_APP_CONTENT_MANIFEST_SHA256),
        ("appOutputTreeManifestSha256", EXPECTED_APP_OUTPUT_TREE_MANIFEST_SHA256),
        ("homeOutputTreeManifestSha256", EXPECTED_HOME_OUTPUT_TREE_MANIFEST_SHA256),
        ("packageEnvelopeSha256", EXPECTED_PACKAGE_ENVELOPE_SHA256),
        ("candidateCueSha256", EXPECTED_CANDIDATE_CUE_SHA256),
        ("candidateBinSha256", EXPECTED_CANDIDATE_BIN_SHA256),
        ("candidateManifestSha256", EXPECTED_CANDIDATE_MANIFEST_SHA256),
        ("aggregateChecklistSha256", EXPECTED_AGGREGATE_CHECKLIST_SHA256),
        (
            "productionCaptureBlockersSha256",
            EXPECTED_PRODUCTION_CAPTURE_BLOCKERS_SHA256,
        ),
        ("frozenWorkflowSha256", EXPECTED_FROZEN_WORKFLOW_SHA256),
        (
            "frozenWorkflowCompatible",
            str(FROZEN_WORKFLOW_COMPATIBLE_WITH_AGGREGATE_SCOPE).lower(),
        ),
        ("aggregateEventualCheckCount", len(EXPECTED_AGGREGATE_CHECK_IDS)),
        ("launchGate", LAUNCH_GATE),
        ("productionCaptureEnabled", str(PRODUCTION_CAPTURE_ENABLED).lower()),
        (
            "productionCaptureBlockingCheckId",
            PRODUCTION_CAPTURE_BLOCKING_CHECK_ID,
        ),
        ("runtimeAutomation", "forbidden"),
    )
    manifest = "id65-authoring-aggregate-v1-runtime-profile\n" + "".join(
        f"{name}={value}\n" for name, value in fields
    )
    return _sha256_bytes(manifest.encode("utf-8", "strict"))


EXPECTED_RUNTIME_PROFILE_SHA256: Final = _runtime_profile_sha256()


def _require_core_contract(core: types.ModuleType) -> None:
    required = (
        "CaptureRefusal",
        "CaptureCoordinator",
        "CoordinatorLease",
        "AnchoredStaging",
        "RealMacBoundary",
        "FileIdentity",
        "read_regular_file",
        "_validate_profile_snapshot",
        "PREFLIGHT_PROOF_KEYS",
        "SESSION_IDS",
    )
    missing = [name for name in required if not hasattr(core, name)]
    if missing:
        raise AggregateCaptureRefusal(
            f"frozen capture API changed: {','.join(missing)}"
        )
    if any(name.lower().startswith("automation") for name in CAPTURE_PIN_KEYS):
        raise AggregateCaptureRefusal("automation entered the aggregate pin closure.")


def _install_authority_first_coordinator_lease(core: types.ModuleType) -> None:
    """Replace the frozen lease with an authority-first aggregate lease.

    The frozen implementation prepares its staging prefix before acquiring the
    shared authority lock.  That is safe for its frozen namespace but is not the
    aggregate contract: no aggregate staging directory may even be opened or
    created until the cross-tool authority lock is held.
    """

    frozen_lease = core.CoordinatorLease

    class AuthorityFirstAggregateCoordinatorLease(frozen_lease):
        def __enter__(self) -> Any:
            if self.authority_fd >= 0 or self.fd >= 0:
                raise core.CaptureRefusal("Coordinator lease is already entered.")
            self.authority_parent_fd = core._open_secure_directory(
                self.repository_root / core.CAPTURE_COORDINATOR_AUTHORITY_DIRECTORY
            )
            try:
                authority_parent = core.os.fstat(self.authority_parent_fd)
                self._require_private_parent(authority_parent)
                self.authority_parent_identity = self._directory_identity(
                    authority_parent
                )
                self.authority_fd = core.os.open(
                    core.CAPTURE_COORDINATOR_AUTHORITY_NAME,
                    core.os.O_RDWR
                    | core.os.O_CREAT
                    | getattr(core.os, "O_CLOEXEC", 0)
                    | getattr(core.os, "O_NOFOLLOW", 0),
                    0o600,
                    dir_fd=self.authority_parent_fd,
                )
                authority = core.os.fstat(self.authority_fd)
                self._require_lock_inode(
                    authority, "Capture coordinator stable authority"
                )
                try:
                    core.fcntl.flock(
                        self.authority_fd,
                        core.fcntl.LOCK_EX | core.fcntl.LOCK_NB,
                    )
                except BlockingIOError:
                    raise core.CaptureRefusal(
                        "Another capture coordinator already owns the lease."
                    ) from None
                self.authority_locked = True
                self.authority_identity = self._file_identity(authority)
                self._assert_stable_authority_current()

                # This is deliberately the first aggregate-staging access.
                self.staging.ensure_prefix()
                self.prefix_fd = core._open_secure_directory(self.staging.prefix)
                prefix = core.os.fstat(self.prefix_fd)
                if (
                    prefix.st_uid != core.os.getuid()
                    or core.stat.S_IMODE(prefix.st_mode) != 0o700
                ):
                    raise core.CaptureRefusal(
                        "Capture-staging root is not owned mode 0700."
                    )
                self.prefix_identity = self._directory_identity(prefix)
                self.fd = core.os.open(
                    core.CAPTURE_COORDINATOR_STAGING_LOCK_NAME,
                    core.os.O_RDWR
                    | core.os.O_CREAT
                    | getattr(core.os, "O_CLOEXEC", 0)
                    | getattr(core.os, "O_NOFOLLOW", 0),
                    0o600,
                    dir_fd=self.prefix_fd,
                )
                info = core.os.fstat(self.fd)
                self._require_lock_inode(info, "Coordinator lease")
                try:
                    core.fcntl.flock(
                        self.fd,
                        core.fcntl.LOCK_EX | core.fcntl.LOCK_NB,
                    )
                except BlockingIOError:
                    raise core.CaptureRefusal(
                        "Another capture coordinator already owns the lease."
                    ) from None
                self.staging_locked = True
                self.lock_identity = self._file_identity(info)
                self.assert_current()
                return self
            except BaseException:
                self._close_all(validate=False)
                raise

    AuthorityFirstAggregateCoordinatorLease.__name__ = (
        "AuthorityFirstAggregateCoordinatorLease"
    )
    AuthorityFirstAggregateCoordinatorLease.__qualname__ = (
        "AuthorityFirstAggregateCoordinatorLease"
    )
    core.CoordinatorLease = AuthorityFirstAggregateCoordinatorLease


def _install_authority_first_capture_coordinator(core: types.ModuleType) -> None:
    """Hold the authority-first lease around every frozen staging access."""

    frozen_coordinator = core.CaptureCoordinator

    class AuthorityFirstAggregateCaptureCoordinator(frozen_coordinator):
        def __init__(self, *args: Any, **kwargs: Any) -> None:
            super().__init__(*args, **kwargs)
            self._aggregate_authority_depth = 0

        def _under_aggregate_authority(self, operation: Any) -> Any:
            if self._aggregate_authority_depth != 0:
                return operation()
            lease = self._lease_factory()
            with lease:
                original_factory = self._lease_factory
                self._lease_factory = lambda: contextlib.nullcontext()
                self._aggregate_authority_depth = 1
                try:
                    return operation()
                finally:
                    self._aggregate_authority_depth = 0
                    self._lease_factory = original_factory

        def preflight(self) -> None:
            return self._under_aggregate_authority(
                lambda: frozen_coordinator.preflight(self)
            )

        def capture_session(self, session_id: str) -> str:
            return self._under_aggregate_authority(
                lambda: frozen_coordinator.capture_session(self, session_id)
            )

        def verify_staging(self, run_id: str) -> None:
            return self._under_aggregate_authority(
                lambda: frozen_coordinator.verify_staging(self, run_id)
            )

    AuthorityFirstAggregateCaptureCoordinator.__name__ = (
        "AuthorityFirstAggregateCaptureCoordinator"
    )
    AuthorityFirstAggregateCaptureCoordinator.__qualname__ = (
        "AuthorityFirstAggregateCaptureCoordinator"
    )
    core.CaptureCoordinator = AuthorityFirstAggregateCaptureCoordinator


def _aggregate_expected_marker_sequence(
    core: types.ModuleType, session_id: str
) -> list[tuple[str, str | None]]:
    result: list[tuple[str, str | None]] = []
    if session_id == "candidate-primary-edge-0":
        result.extend(
            [
                ("hold-started", "stable-hold"),
                ("hold-ended", "stable-hold"),
                ("frame-captured", "candidate.lone-pad-visibility"),
            ]
        )
        marker_plan = core.CHECK_MARKER_PLANS[session_id][2:]
    else:
        marker_plan = core.CHECK_MARKER_PLANS.get(session_id, ())
    for check_id, _ in marker_plan:
        result.extend(
            [("check-started", check_id), ("check-ended", check_id)]
        )
    if session_id in {
        "candidate-primary-edge-0",
        "candidate-edge-1",
        "candidate-edge-2",
    }:
        result.extend(
            [
                (
                    "frame-captured",
                    core.CaptureCoordinator._edge_frame_role(
                        session_id, before=True
                    ),
                ),
                ("fall-started", "edge-fall"),
                ("respawn-marked", "edge-respawn"),
                (
                    "frame-captured",
                    core.CaptureCoordinator._edge_frame_role(
                        session_id, before=False
                    ),
                ),
            ]
        )
    elif session_id == "candidate-cold-reset":
        result.append(
            (
                "frame-captured",
                "candidate.cold-reset-same-pad-yaw.post-reset-frame",
            )
        )
    return result


def _configure_aggregate_checklist(core: types.ModuleType) -> None:
    plans = {
        session_id: tuple(items)
        for session_id, items in core.CHECK_MARKER_PLANS.items()
    }
    for session_id, additions in AGGREGATE_ADDITIONAL_MARKER_PLANS.items():
        plans[session_id] = plans.get(session_id, ()) + tuple(additions)
    instructions = dict(core.MARKER_INSTRUCTIONS)
    instructions.update(AGGREGATE_MARKER_INSTRUCTIONS)
    core.CHECK_MARKER_PLANS = plans
    core.MARKER_INSTRUCTIONS = types.MappingProxyType(instructions)
    core._expected_marker_sequence = lambda session_id: (
        _aggregate_expected_marker_sequence(core, session_id)
    )


def _require_aggregate_checklist(core: types.ModuleType) -> None:
    observed_edges = tuple(
        (
            session_id,
            assignment.edge_id,
            assignment.segment,
            assignment.description,
            assignment.direction,
            assignment.midpoint,
        )
        for session_id, assignment in core.EDGE_ASSIGNMENTS.items()
    )
    observed_primary_markers = tuple(
        marker_id
        for marker_id, _ in core.CHECK_MARKER_PLANS.get(
            "candidate-primary-edge-0", ()
        )[: len(FROZEN_WORKFLOW_PRIMARY_MARKER_IDS)]
    )
    if (
        len(EXPECTED_AGGREGATE_CHECK_IDS) != 17
        or len(set(EXPECTED_AGGREGATE_CHECK_IDS)) != 17
        or tuple(AGGREGATE_CHECK_BINDINGS) != EXPECTED_AGGREGATE_CHECK_IDS
        or len(PRODUCTION_CAPTURE_BLOCKERS) != 2
        or len({item[0] for item in PRODUCTION_CAPTURE_BLOCKERS}) != 2
        or PRODUCTION_CAPTURE_ENABLED
        or FROZEN_WORKFLOW_COMPATIBLE_WITH_AGGREGATE_SCOPE
        or tuple(core.SESSION_IDS) != FROZEN_WORKFLOW_SESSION_IDS
        or core.RUN_ID_RE.pattern != FROZEN_WORKFLOW_RUN_ID_PATTERN
        or observed_primary_markers != FROZEN_WORKFLOW_PRIMARY_MARKER_IDS
        or observed_edges != FROZEN_WORKFLOW_EDGE_ASSIGNMENTS
        or (
            "frame-captured",
            "candidate.lone-pad-visibility",
        )
        not in _aggregate_expected_marker_sequence(
            core, "candidate-primary-edge-0"
        )
        or not re.fullmatch(
            r"[0-9a-f]{64}", EXPECTED_AGGREGATE_CHECKLIST_SHA256
        )
        or not _SHA256_RE.fullmatch(
            EXPECTED_PRODUCTION_CAPTURE_BLOCKERS_SHA256
        )
        or not _SHA256_RE.fullmatch(EXPECTED_FROZEN_WORKFLOW_SHA256)
    ):
        raise AggregateCaptureRefusal(
            "aggregate focused-check identity changed."
        )
    session_ids = set(core.SESSION_IDS)
    all_planned: dict[str, set[str]] = {
        session_id: {marker_id for marker_id, _ in marker_plan}
        for session_id, marker_plan in core.CHECK_MARKER_PLANS.items()
    }
    for session_id, additions in AGGREGATE_ADDITIONAL_MARKER_PLANS.items():
        if session_id not in session_ids:
            raise AggregateCaptureRefusal(
                f"aggregate checklist session is unavailable: {session_id}."
            )
        planned = core.CHECK_MARKER_PLANS.get(session_id, ())
        marker_ids = [marker_id for marker_id, _ in planned]
        if len(marker_ids) != len(set(marker_ids)):
            raise AggregateCaptureRefusal(
                f"aggregate checklist repeats a marker: {session_id}."
            )
        for marker_id, minimum_microseconds in additions:
            if (
                marker_id not in all_planned.get(session_id, set())
                or core.MARKER_INSTRUCTIONS.get(marker_id)
                != AGGREGATE_MARKER_INSTRUCTIONS[marker_id]
                or not isinstance(minimum_microseconds, int)
                or minimum_microseconds <= 0
            ):
                raise AggregateCaptureRefusal(
                    f"aggregate checklist marker is not closed: {marker_id}."
                )
        if sum(duration for _, duration in planned) >= (
            core.MAX_RECORDING_DURATION_MICROSECONDS
        ):
            raise AggregateCaptureRefusal(
                f"aggregate checklist exceeds one recording bound: {session_id}."
            )
    allowed_derived = {
        (
            "candidate-primary-edge-0",
            "check",
            "candidate.completed-landing-hud4",
        ),
        ("candidate-primary-edge-0", "event", "landing-marked"),
        ("candidate-primary-edge-0", "event", "fall-started"),
        ("candidate-primary-edge-0", "event", "respawn-marked"),
        ("candidate-primary-edge-0", "watcher", "fall-respawn"),
    }
    for check_id, bindings in AGGREGATE_CHECK_BINDINGS.items():
        if not bindings:
            raise AggregateCaptureRefusal(
                f"aggregate checklist check is unbound: {check_id}."
            )
        for binding in bindings:
            parts = binding.split(":", 2)
            if len(parts) != 3:
                raise AggregateCaptureRefusal(
                    f"aggregate checklist binding is malformed: {binding}."
                )
            session_id, kind, role = parts
            if session_id not in session_ids:
                raise AggregateCaptureRefusal(
                    f"aggregate checklist binding session changed: {binding}."
                )
            if kind == "check" and role in all_planned.get(session_id, set()):
                continue
            if (session_id, kind, role) not in allowed_derived:
                raise AggregateCaptureRefusal(
                    f"aggregate checklist binding has no capture primitive: {binding}."
                )
    blocker_payload = _production_capture_blocker_payload()
    if (
        PRODUCTION_CAPTURE_BLOCKING_CHECK_ID
        not in EXPECTED_AGGREGATE_CHECK_IDS
        or "memory-card" not in blocker_payload[0]["reason"]
        or blocker_payload[1]["requiredGate"]
        != "aggregate.exact-17-check-town-square-workflow"
        or "seven sessions" not in blocker_payload[1]["reason"]
        or "lone-pad" not in blocker_payload[1]["reason"]
        or "Town Square-only" not in blocker_payload[1]["reason"]
    ):
        raise AggregateCaptureRefusal(
            "aggregate production blocker closure changed."
        )


def _configure_core(core: types.ModuleType) -> types.ModuleType:
    _require_core_contract(core)
    if getattr(core, "_AUTHORING_AGGREGATE_V1_CONFIGURED", False):
        return core

    assignments: dict[str, object] = {
        "FORMAT": CAPTURE_FORMAT,
        "RUN_FORMAT": RUN_FORMAT,
        "PROFILE_FORMAT": PROFILE_FORMAT,
        "PROFILE_FORMAT_VERSION": PROFILE_FORMAT_VERSION,
        "CAPTURE_COORDINATOR_RELATIVE_PATH": CAPTURE_COORDINATOR_RELATIVE_PATH.as_posix(),
        "CAPTURE_STAGING_PREFIX": CAPTURE_STAGING_PREFIX.as_posix(),
        "CAPTURE_COORDINATOR_AUTHORITY_DIRECTORY": GLOBAL_AUTHORITY_DIRECTORY.as_posix(),
        "CAPTURE_COORDINATOR_AUTHORITY_NAME": GLOBAL_AUTHORITY_LOCK_NAME,
        "CAPTURE_COORDINATOR_STAGING_LOCK_NAME": AGGREGATE_STAGING_LOCK_NAME,
        "WATCHER_RELATIVE_PATH": WATCHER_RELATIVE_PATH.as_posix(),
        "EXPECTED_WATCHER_SHA256": EXPECTED_WATCHER_SHA256,
        "PROFILE_BUILDER_RELATIVE_PATH": PROFILE_BUILDER_RELATIVE_PATH.as_posix(),
        "EXPECTED_PROFILE_BUILDER_SHA256": EXPECTED_PROFILE_BUILDER_SHA256,
        "PROFILE_VERIFIER_RELATIVE_PATH": PROFILE_VERIFIER_RELATIVE_PATH.as_posix(),
        "EXPECTED_PROFILE_VERIFIER_SHA256": EXPECTED_PROFILE_VERIFIER_SHA256,
        "PROFILE_ID": PROFILE_ID,
        "PROFILE_PACKAGE_RELATIVE_PATH": PROFILE_PACKAGE_RELATIVE_PATH.as_posix(),
        "PACKAGED_APP_RELATIVE_PATH": PACKAGED_APP_RELATIVE_PATH.as_posix(),
        "DUCKSTATION_EXECUTABLE_RELATIVE_PATH": DUCKSTATION_EXECUTABLE_RELATIVE_PATH.as_posix(),
        "EXTERNAL_HOME_RELATIVE_PATH": EXTERNAL_HOME_RELATIVE_PATH.as_posix(),
        "EXTERNAL_HOME_DATA_RELATIVE_PATH": EXTERNAL_HOME_DATA_RELATIVE_PATH.as_posix(),
        "PROFILE_ROOT_RELATIVE_PATH": EXTERNAL_HOME_DATA_RELATIVE_PATH.as_posix(),
        "HANDOFF_RECEIPT_RELATIVE_PATH": HANDOFF_RECEIPT_RELATIVE_PATH.as_posix(),
        "CANDIDATE_DIRECTORY": CANDIDATE_DIRECTORY_RELATIVE_PATH.as_posix(),
        "CANDIDATE_STEM": CANDIDATE_STEM,
        "CANDIDATE_CUE_RELATIVE_PATH": CANDIDATE_CUE_RELATIVE_PATH.as_posix(),
        "CANDIDATE_BIN_RELATIVE_PATH": CANDIDATE_BIN_RELATIVE_PATH.as_posix(),
        "EXPECTED_CANDIDATE_CUE_SHA256": EXPECTED_CANDIDATE_CUE_SHA256,
        "EXPECTED_CANDIDATE_CUE_SIZE": EXPECTED_CANDIDATE_CUE_SIZE,
        "EXPECTED_CANDIDATE_BIN_SHA256": EXPECTED_CANDIDATE_BIN_SHA256,
        "EXPECTED_CANDIDATE_BIN_SIZE": EXPECTED_CANDIDATE_BIN_SIZE,
        "EXPECTED_DUCKSTATION_SHA256": EXPECTED_DUCKSTATION_SHA256,
        "EXPECTED_EXECUTABLE_SIZE": EXPECTED_DUCKSTATION_SIZE,
        "EXPECTED_SETTINGS_SHA256": EXPECTED_SETTINGS_SHA256,
        "EXPECTED_SETTINGS_SIZE": EXPECTED_SETTINGS_SIZE,
        "EXPECTED_BIOS_SHA256": EXPECTED_BIOS_SHA256,
        "EXPECTED_BIOS_SIZE": EXPECTED_BIOS_SIZE,
        "EXPECTED_HANDOFF_RECEIPT_SHA256": EXPECTED_HANDOFF_RECEIPT_SHA256,
        "EXPECTED_RECEIPT_SIZE": EXPECTED_HANDOFF_RECEIPT_SIZE,
        "EXPECTED_RECEIPT_CLOSURE_SHA256": EXPECTED_RECEIPT_CLOSURE_SHA256,
        "EXPECTED_APP_CONTENT_MANIFEST_SHA256": EXPECTED_APP_CONTENT_MANIFEST_SHA256,
        "EXPECTED_APP_OUTPUT_TREE_MANIFEST_SHA256": EXPECTED_APP_OUTPUT_TREE_MANIFEST_SHA256,
        "EXPECTED_HOME_OUTPUT_TREE_MANIFEST_SHA256": EXPECTED_HOME_OUTPUT_TREE_MANIFEST_SHA256,
        "EXPECTED_PACKAGE_ENVELOPE_SHA256": EXPECTED_PACKAGE_ENVELOPE_SHA256,
        "EXPECTED_CANDIDATE_MANIFEST_SHA256": EXPECTED_CANDIDATE_MANIFEST_SHA256,
        "EXPECTED_RUNTIME_PROFILE_SHA256": EXPECTED_RUNTIME_PROFILE_SHA256,
        "CAPTURE_PIN_KEYS": CAPTURE_PIN_KEYS,
        "RUNTIME_ENVIRONMENT_SENTINEL_VALUE": PROFILE_ID,
        "RUNTIME_ENVIRONMENT_FIXED_ASSIGNMENTS": (
            "LANG=C",
            "LC_ALL=C",
            "PATH=/usr/bin:/bin:/usr/sbin:/sbin",
            f"SPYRO_ID65_CAPTURE_ENVIRONMENT_END_V1={PROFILE_ID}",
        ),
    }
    for name, value in assignments.items():
        setattr(core, name, value)
    _install_authority_first_coordinator_lease(core)
    _install_authority_first_capture_coordinator(core)
    _configure_aggregate_checklist(core)
    core._validate_prelaunch_proof = lambda proof: _validate_compact_prelaunch_proof(
        core, proof
    )
    _require_aggregate_checklist(core)
    core._AUTHORING_AGGREGATE_V1_CONFIGURED = True
    return core


def _load_configured_core(repository_root: pathlib.Path) -> types.ModuleType:
    core = _load_module_from_pinned_source(
        repository_root,
        FROZEN_CAPTURE_RELATIVE_PATH,
        EXPECTED_FROZEN_CAPTURE_SHA256,
        0o755,
        "_spyro_id65_frozen_capture_for_authoring_aggregate_v1",
    )
    if len(_stable_read_source(
        repository_root / FROZEN_CAPTURE_RELATIVE_PATH,
        expected_sha256=EXPECTED_FROZEN_CAPTURE_SHA256,
        expected_mode=0o755,
        label="frozen capture size check",
    )) != EXPECTED_FROZEN_CAPTURE_SIZE:
        raise AggregateCaptureRefusal("frozen capture byte length changed.")
    return _configure_core(core)


def _validate_compact_prelaunch_proof(
    core: types.ModuleType, proof: object
) -> dict[str, Any]:
    if type(proof) is not dict or set(proof) != set(core.PREFLIGHT_PROOF_KEYS):
        raise core.CaptureRefusal(
            "Aggregate prelaunch proof is not the exact compact schema."
        )
    expected = {
        "format": PREFLIGHT_FORMAT,
        "formatVersion": PREFLIGHT_FORMAT_VERSION,
        "status": "PREFLIGHT_READY",
        "profileId": PROFILE_ID,
        "receiptSha256": EXPECTED_HANDOFF_RECEIPT_SHA256,
        "appContentManifestSha256": EXPECTED_APP_CONTENT_MANIFEST_SHA256,
        "appOutputTreeManifestSha256": EXPECTED_APP_OUTPUT_TREE_MANIFEST_SHA256,
        "homeOutputTreeManifestSha256": EXPECTED_HOME_OUTPUT_TREE_MANIFEST_SHA256,
        "packageEnvelopeSha256": EXPECTED_PACKAGE_ENVELOPE_SHA256,
        "settingsSha256": EXPECTED_SETTINGS_SHA256,
        "biosSha256": EXPECTED_BIOS_SHA256,
        "candidateCueSha256": EXPECTED_CANDIDATE_CUE_SHA256,
        "candidateBinSha256": EXPECTED_CANDIDATE_BIN_SHA256,
        "hostNormalSettingsSentinelSha256": EXPECTED_NORMAL_SETTINGS_SHA256,
        "launchGate": LAUNCH_GATE,
    }
    for name, value in expected.items():
        if proof.get(name) != value or type(proof.get(name)) is not type(value):
            raise core.CaptureRefusal(
                f"Aggregate prelaunch proof identity changed: {name}."
            )
    core._validate_profile_snapshot(
        proof["snapshot"],
        phase="preflight",
        session_id="preflight",
        process_instance_id="not-applicable",
        captured_at_utc="1970-01-01T00:00:00.000Z",
    )
    return proof


@dataclasses.dataclass(frozen=True)
class _SourceIdentity:
    relative_path: str
    sha256: str


class AggregateSourceGuard:
    """Bind the frozen engine to the exact aggregate profile and verifier."""

    def __init__(self, repository_root: pathlib.Path, core: types.ModuleType) -> None:
        self.repository_root = _require_repository_root(repository_root)
        self.core = _configure_core(core)
        self.package_root = self.repository_root / PROFILE_PACKAGE_RELATIVE_PATH
        self.external_home_root = self.repository_root / EXTERNAL_HOME_RELATIVE_PATH
        self.profile_root = self.repository_root / EXTERNAL_HOME_DATA_RELATIVE_PATH
        self.executable_path = self.repository_root / DUCKSTATION_EXECUTABLE_RELATIVE_PATH
        self.receipt_path = self.repository_root / HANDOFF_RECEIPT_RELATIVE_PATH
        self.cue_path = self.repository_root / CANDIDATE_CUE_RELATIVE_PATH
        self.bin_path = self.repository_root / CANDIDATE_BIN_RELATIVE_PATH
        self._verifier: types.ModuleType | None = None
        self.prelaunch_call_count = 0
        self.snapshot_call_count = 0

    def _load_verifier(self) -> types.ModuleType:
        if self._verifier is None:
            verifier = _load_module_from_pinned_source(
                self.repository_root,
                PROFILE_VERIFIER_RELATIVE_PATH,
                EXPECTED_PROFILE_VERIFIER_SHA256,
                0o644,
                "_spyro_id65_authoring_aggregate_v1_verifier_for_capture",
            )
            required = (
                "capture_runtime_snapshot",
                "verify_prelaunch",
                "verifier_status",
                "EXPECTED_PROFILE_OUTPUT_PINS",
            )
            if any(not hasattr(verifier, name) for name in required):
                raise self.core.CaptureRefusal(
                    "Aggregate profile verifier API changed."
                )
            if (
                verifier.PRODUCTION_REPOSITORY_ROOT != self.repository_root
                or verifier.PROFILE_ID != PROFILE_ID
                or verifier.PROFILE_DESTINATION_RELATIVE_PATH
                != PROFILE_PACKAGE_RELATIVE_PATH
                or verifier.EXPECTED_CANDIDATE_CUE_SHA256
                != EXPECTED_CANDIDATE_CUE_SHA256
                or verifier.EXPECTED_CANDIDATE_BIN_SHA256
                != EXPECTED_CANDIDATE_BIN_SHA256
                or verifier.EXPECTED_PROFILE_OUTPUT_PINS.get("receiptSha256")
                != EXPECTED_HANDOFF_RECEIPT_SHA256
                or verifier.EXPECTED_PROFILE_OUTPUT_PINS.get("packageEnvelopeSha256")
                != EXPECTED_PACKAGE_ENVELOPE_SHA256
            ):
                raise self.core.CaptureRefusal(
                    "Aggregate profile verifier production contract changed."
                )
            self._verifier = verifier
        return self._verifier

    def _source_identities(self) -> tuple[_SourceIdentity, ...]:
        identities: list[_SourceIdentity] = []
        for relative_path, expected_hash in _EXPECTED_SOURCE_HASHES.items():
            data = _stable_read_source(
                self.repository_root / relative_path,
                expected_sha256=expected_hash,
                expected_mode=_EXPECTED_SOURCE_MODES[relative_path],
                label=relative_path.as_posix(),
            )
            identities.append(
                _SourceIdentity(relative_path.as_posix(), _sha256_bytes(data))
            )
        coordinator = _stable_read_source(
            self.repository_root / CAPTURE_COORDINATOR_RELATIVE_PATH,
            expected_sha256=None,
            expected_mode=0o644,
            label=CAPTURE_COORDINATOR_RELATIVE_PATH.as_posix(),
        )
        identities.append(
            _SourceIdentity(
                CAPTURE_COORDINATOR_RELATIVE_PATH.as_posix(),
                _sha256_bytes(coordinator),
            )
        )
        return tuple(identities)

    def validate_all(self) -> dict[str, str]:
        self._load_verifier()
        identities = {item.relative_path: item.sha256 for item in self._source_identities()}
        base = {
            "watcherRelativePath": WATCHER_RELATIVE_PATH.as_posix(),
            "watcherSha256": EXPECTED_WATCHER_SHA256,
            "captureCoreRelativePath": FROZEN_CAPTURE_RELATIVE_PATH.as_posix(),
            "captureCoreSha256": EXPECTED_FROZEN_CAPTURE_SHA256,
            "profileBuilderRelativePath": PROFILE_BUILDER_RELATIVE_PATH.as_posix(),
            "profileBuilderSha256": EXPECTED_PROFILE_BUILDER_SHA256,
            "profileVerifierRelativePath": PROFILE_VERIFIER_RELATIVE_PATH.as_posix(),
            "profileVerifierSha256": EXPECTED_PROFILE_VERIFIER_SHA256,
            "captureCoordinatorRelativePath": CAPTURE_COORDINATOR_RELATIVE_PATH.as_posix(),
            "captureCoordinatorSha256": identities[CAPTURE_COORDINATOR_RELATIVE_PATH.as_posix()],
            "runtimeProfileSha256": EXPECTED_RUNTIME_PROFILE_SHA256,
            "profilePackageRelativePath": PROFILE_PACKAGE_RELATIVE_PATH.as_posix(),
            "profileId": PROFILE_ID,
            "handoffReceiptSha256": EXPECTED_HANDOFF_RECEIPT_SHA256,
            "handoffReceiptClosureSha256": EXPECTED_RECEIPT_CLOSURE_SHA256,
            "appContentManifestSha256": EXPECTED_APP_CONTENT_MANIFEST_SHA256,
            "appOutputTreeManifestSha256": EXPECTED_APP_OUTPUT_TREE_MANIFEST_SHA256,
            "homeOutputTreeManifestSha256": EXPECTED_HOME_OUTPUT_TREE_MANIFEST_SHA256,
            "packageEnvelopeSha256": EXPECTED_PACKAGE_ENVELOPE_SHA256,
            "externalHomeSettingsSha256": EXPECTED_SETTINGS_SHA256,
            "externalHomeBiosSha256": EXPECTED_BIOS_SHA256,
            "hostNormalSettingsSentinelSha256": EXPECTED_NORMAL_SETTINGS_SHA256,
            "candidateCueSha256": EXPECTED_CANDIDATE_CUE_SHA256,
            "candidateBinSha256": EXPECTED_CANDIDATE_BIN_SHA256,
            "candidateManifestSha256": EXPECTED_CANDIDATE_MANIFEST_SHA256,
            "candidateReceiptFileSha256": EXPECTED_CANDIDATE_RECEIPT_FILE_SHA256,
            "candidateReceiptContentSha256": EXPECTED_CANDIDATE_RECEIPT_CONTENT_SHA256,
            "documentEnvelopeSha256": EXPECTED_DOCUMENT_ENVELOPE_SHA256,
            "documentCanonicalSha256": EXPECTED_DOCUMENT_CANONICAL_SHA256,
            "aggregatePlanSha256": EXPECTED_AGGREGATE_PLAN_SHA256,
            "outputRow80Sha256": EXPECTED_OUTPUT_ROW80_SHA256,
            "aggregateChecklistSha256": EXPECTED_AGGREGATE_CHECKLIST_SHA256,
            "productionCaptureBlockersSha256": (
                EXPECTED_PRODUCTION_CAPTURE_BLOCKERS_SHA256
            ),
            "frozenWorkflowSha256": EXPECTED_FROZEN_WORKFLOW_SHA256,
            "frozenWorkflowCompatible": str(
                FROZEN_WORKFLOW_COMPATIBLE_WITH_AGGREGATE_SCOPE
            ).lower(),
            "captureStagingRelativePath": CAPTURE_STAGING_PREFIX.as_posix(),
            "finalEvidenceRelativePath": FINAL_EVIDENCE_RELATIVE_PATH.as_posix(),
        }
        closure_lines = ["id65-authoring-aggregate-v1-capture-source-closure"]
        closure_lines.extend(f"{name}={base[name]}" for name in sorted(base))
        base["sourceClosureSha256"] = _sha256_bytes(
            ("\n".join(closure_lines) + "\n").encode("utf-8", "strict")
        )
        if set(base) != set(CAPTURE_PIN_KEYS):
            raise self.core.CaptureRefusal(
                "Aggregate capture pin closure changed shape."
            )
        if any(not isinstance(value, str) or not value for value in base.values()):
            raise self.core.CaptureRefusal("Aggregate capture pin is malformed.")
        return base

    def _identity(
        self, path: pathlib.Path, expected_size: int, expected_sha256: str
    ) -> Any:
        identity = self.core.read_regular_file(
            path,
            expected_owner=os.getuid(),
            expected_mode=0o644,
        )
        if identity.byte_length != expected_size or identity.sha256 != expected_sha256:
            raise self.core.CaptureRefusal("Aggregate candidate identity changed.")
        return identity

    def candidate_identities(self) -> tuple[Any, Any]:
        cue = self._identity(
            self.cue_path, EXPECTED_CANDIDATE_CUE_SIZE, EXPECTED_CANDIDATE_CUE_SHA256
        )
        binary = self._identity(
            self.bin_path, EXPECTED_CANDIDATE_BIN_SIZE, EXPECTED_CANDIDATE_BIN_SHA256
        )
        expected_cue = (
            f'FILE "{CANDIDATE_STEM}.bin" BINARY\n'
            "  TRACK 01 MODE2/2352\n"
            "    INDEX 01 00:00:00\n"
        ).encode("ascii")
        cue_bytes = self.core.read_regular_file(
            self.cue_path,
            include_bytes=True,
            expected_owner=os.getuid(),
            expected_mode=0o644,
            maximum_bytes=EXPECTED_CANDIDATE_CUE_SIZE,
        ).bytes_value
        if cue_bytes != expected_cue:
            raise self.core.CaptureRefusal("Aggregate candidate CUE bytes changed.")
        return cue, binary

    def executable_identity(self) -> Any:
        identity = self.core.read_regular_file(self.executable_path)
        if (
            identity.byte_length != EXPECTED_DUCKSTATION_SIZE
            or identity.sha256 != EXPECTED_DUCKSTATION_SHA256
        ):
            raise self.core.CaptureRefusal(
                "Aggregate profile DuckStation executable changed."
            )
        return identity

    def capture_profile(
        self,
        *,
        phase: str,
        session_id: str,
        process_instance_id: str,
        captured_at_utc: str | None = None,
    ) -> dict[str, Any]:
        self.snapshot_call_count += 1
        try:
            snapshot = self._load_verifier().capture_runtime_snapshot(
                self.repository_root,
                phase=phase,
                session_id=session_id,
                process_instance_id=process_instance_id,
                captured_at_utc=captured_at_utc,
            )
        except Exception as exc:
            raise self.core.CaptureRefusal(
                "Aggregate runtime profile snapshot was refused."
            ) from exc
        expected_time = captured_at_utc
        if phase == "preflight" and expected_time is None:
            expected_time = "1970-01-01T00:00:00.000Z"
        if expected_time is None:
            raise self.core.CaptureRefusal(
                "Aggregate runtime snapshot requires a capture time."
            )
        return self.core._validate_profile_snapshot(
            snapshot,
            phase=phase,
            session_id=session_id,
            process_instance_id=process_instance_id,
            captured_at_utc=expected_time,
        )

    def verify_prelaunch_once(self) -> dict[str, Any]:
        if self.prelaunch_call_count != 0:
            raise self.core.CaptureRefusal(
                "Aggregate full prelaunch proof may run only once."
            )
        self.prelaunch_call_count += 1
        try:
            raw = self._load_verifier().verify_prelaunch(self.repository_root)
        except Exception as exc:
            raise self.core.CaptureRefusal(
                "Aggregate profile prelaunch verification was refused."
            ) from exc
        return _compact_verifier_prelaunch(self.core, raw)


def _compact_verifier_prelaunch(
    core: types.ModuleType, raw: object
) -> dict[str, Any]:
    expected_keys = {
        "format", "formatVersion", "status", "profileId", "profileDestination",
        "handoffReceiptSha256", "handoffReceiptClosureSha256",
        "appContentManifestSha256", "appOutputTreeManifestSha256",
        "homeOutputTreeManifestSha256", "packageEnvelopeSha256",
        "candidateManifestSha256", "candidateCueSha256", "candidateBinSha256",
        "candidateReceiptFileSha256", "candidateReceiptContentSha256",
        "documentEnvelopeSha256", "documentCanonicalSha256",
        "aggregatePlanSha256", "outputRow80Sha256", "launchGate", "snapshot",
        "duckStationLaunched", "runtimeProven", "evidenceAccepted",
        "createBinAuthorized", "releaseAuthorized", "publishable",
    }
    if type(raw) is not dict or set(raw) != expected_keys:
        raise core.CaptureRefusal(
            "Aggregate verifier prelaunch result changed schema."
        )
    expected = {
        "format": PREFLIGHT_FORMAT,
        "formatVersion": PREFLIGHT_FORMAT_VERSION,
        "status": "PREFLIGHT_READY",
        "profileId": PROFILE_ID,
        "profileDestination": os.fspath(
            PRODUCTION_REPOSITORY_ROOT / PROFILE_PACKAGE_RELATIVE_PATH
        ),
        "handoffReceiptSha256": EXPECTED_HANDOFF_RECEIPT_SHA256,
        "handoffReceiptClosureSha256": EXPECTED_RECEIPT_CLOSURE_SHA256,
        "appContentManifestSha256": EXPECTED_APP_CONTENT_MANIFEST_SHA256,
        "appOutputTreeManifestSha256": EXPECTED_APP_OUTPUT_TREE_MANIFEST_SHA256,
        "homeOutputTreeManifestSha256": EXPECTED_HOME_OUTPUT_TREE_MANIFEST_SHA256,
        "packageEnvelopeSha256": EXPECTED_PACKAGE_ENVELOPE_SHA256,
        "candidateManifestSha256": EXPECTED_CANDIDATE_MANIFEST_SHA256,
        "candidateCueSha256": EXPECTED_CANDIDATE_CUE_SHA256,
        "candidateBinSha256": EXPECTED_CANDIDATE_BIN_SHA256,
        "candidateReceiptFileSha256": EXPECTED_CANDIDATE_RECEIPT_FILE_SHA256,
        "candidateReceiptContentSha256": EXPECTED_CANDIDATE_RECEIPT_CONTENT_SHA256,
        "documentEnvelopeSha256": EXPECTED_DOCUMENT_ENVELOPE_SHA256,
        "documentCanonicalSha256": EXPECTED_DOCUMENT_CANONICAL_SHA256,
        "aggregatePlanSha256": EXPECTED_AGGREGATE_PLAN_SHA256,
        "outputRow80Sha256": EXPECTED_OUTPUT_ROW80_SHA256,
        "launchGate": LAUNCH_GATE,
        "duckStationLaunched": False,
        "runtimeProven": False,
        "evidenceAccepted": False,
        "createBinAuthorized": False,
        "releaseAuthorized": False,
        "publishable": False,
    }
    for name, value in expected.items():
        if raw.get(name) != value or type(raw.get(name)) is not type(value):
            raise core.CaptureRefusal(
                f"Aggregate verifier prelaunch identity changed: {name}."
            )
    snapshot = raw["snapshot"]
    core._validate_profile_snapshot(
        snapshot,
        phase="preflight",
        session_id="preflight",
        process_instance_id="not-applicable",
        captured_at_utc="1970-01-01T00:00:00.000Z",
    )
    compact = {
        "format": PREFLIGHT_FORMAT,
        "formatVersion": PREFLIGHT_FORMAT_VERSION,
        "status": "PREFLIGHT_READY",
        "profileId": PROFILE_ID,
        "receiptSha256": EXPECTED_HANDOFF_RECEIPT_SHA256,
        "appContentManifestSha256": EXPECTED_APP_CONTENT_MANIFEST_SHA256,
        "appOutputTreeManifestSha256": EXPECTED_APP_OUTPUT_TREE_MANIFEST_SHA256,
        "homeOutputTreeManifestSha256": EXPECTED_HOME_OUTPUT_TREE_MANIFEST_SHA256,
        "packageEnvelopeSha256": EXPECTED_PACKAGE_ENVELOPE_SHA256,
        "settingsSha256": EXPECTED_SETTINGS_SHA256,
        "biosSha256": EXPECTED_BIOS_SHA256,
        "candidateCueSha256": EXPECTED_CANDIDATE_CUE_SHA256,
        "candidateBinSha256": EXPECTED_CANDIDATE_BIN_SHA256,
        "hostNormalSettingsSentinelSha256": EXPECTED_NORMAL_SETTINGS_SHA256,
        "launchGate": LAUNCH_GATE,
        "snapshot": snapshot,
    }
    return _validate_compact_prelaunch_proof(core, compact)


def _static_status(repository_root: pathlib.Path) -> dict[str, object]:
    core = _load_configured_core(repository_root)
    guard = AggregateSourceGuard(repository_root, core)
    pins = guard.validate_all()
    return {
        "format": "spyro-id65-authoring-aggregate-v1-capture-status",
        "formatVersion": 1,
        "profileId": PROFILE_ID,
        "captureStagingRelativePath": CAPTURE_STAGING_PREFIX.as_posix(),
        "finalEvidenceRelativePath": FINAL_EVIDENCE_RELATIVE_PATH.as_posix(),
        "globalAuthorityRelativePath": (
            GLOBAL_AUTHORITY_DIRECTORY / GLOBAL_AUTHORITY_LOCK_NAME
        ).as_posix(),
        "aggregateStagingLockRelativePath": (
            CAPTURE_STAGING_PREFIX / AGGREGATE_STAGING_LOCK_NAME
        ).as_posix(),
        "manualCaptureOnly": True,
        "automationAllowed": False,
        "productionCaptureEnabled": PRODUCTION_CAPTURE_ENABLED,
        "productionCaptureBlockerCount": len(PRODUCTION_CAPTURE_BLOCKERS),
        "productionCaptureBlockers": _production_capture_blocker_payload(),
        "productionCaptureBlockersSha256": (
            EXPECTED_PRODUCTION_CAPTURE_BLOCKERS_SHA256
        ),
        "productionCaptureBlockingCheckId": (
            PRODUCTION_CAPTURE_BLOCKING_CHECK_ID
        ),
        "productionCaptureBlockingReason": PRODUCTION_CAPTURE_BLOCKING_REASON,
        "frozenWorkflowCompatibleWithAggregateScope": (
            FROZEN_WORKFLOW_COMPATIBLE_WITH_AGGREGATE_SCOPE
        ),
        "frozenWorkflowSha256": EXPECTED_FROZEN_WORKFLOW_SHA256,
        "frozenWorkflow": _frozen_workflow_payload(),
        "aggregateChecklistSha256": EXPECTED_AGGREGATE_CHECKLIST_SHA256,
        "eventualCheckCount": len(EXPECTED_AGGREGATE_CHECK_IDS),
        "eventualCheckIds": list(EXPECTED_AGGREGATE_CHECK_IDS),
        "duckStationLaunched": False,
        "liveProfilePreflightRun": False,
        "runtimeProven": False,
        "evidenceAccepted": False,
        "saveCapableProfilePinned": False,
        "captureCoordinatorSha256": pins["captureCoordinatorSha256"],
        "captureCoreSha256": EXPECTED_FROZEN_CAPTURE_SHA256,
        "profileVerifierSha256": EXPECTED_PROFILE_VERIFIER_SHA256,
        "sourceClosureSha256": pins["sourceClosureSha256"],
        "runtimeProfileSha256": EXPECTED_RUNTIME_PROFILE_SHA256,
    }


def _require_production_capture_enabled() -> None:
    if not PRODUCTION_CAPTURE_ENABLED:
        blockers = "; ".join(
            f"{blocker_id} [{required_gate}]: {reason}"
            for blocker_id, required_gate, reason in PRODUCTION_CAPTURE_BLOCKERS
        )
        raise AggregateCaptureRefusal(
            "aggregate live capture is fail-closed at all production blockers: "
            f"{blockers}; both a separately pinned save-capable aggregate profile "
            "and an audited exact 17-check aggregate/Town Square capture workflow "
            "are required before enabling production modes."
        )


def _exercise_temporary_lock_order(core: types.ModuleType) -> None:
    temporary_parent = pathlib.Path(tempfile.gettempdir()).resolve(strict=True)
    with tempfile.TemporaryDirectory(
        prefix="id65-authoring-aggregate-capture-lock-",
        dir=temporary_parent,
    ) as temporary:
        root = pathlib.Path(temporary)
        authority = root / GLOBAL_AUTHORITY_DIRECTORY
        authority.mkdir(parents=True)
        authority.chmod(0o700)
        events: list[tuple[str, int | str]] = []
        original_flock = core.fcntl.flock
        original_open = core.os.open
        original_mkdir = core.os.mkdir
        aggregate_leaves = {
            CAPTURE_STAGING_PREFIX.parts[-2],
            CAPTURE_STAGING_PREFIX.parts[-1],
            AGGREGATE_STAGING_LOCK_NAME,
        }

        def is_aggregate_staging_target(path: Any) -> bool:
            try:
                value = os.fsdecode(path)
            except (TypeError, ValueError):
                return False
            if value in aggregate_leaves:
                return True
            if not os.path.isabs(value):
                return False
            normalized = pathlib.Path(os.path.normpath(value))
            prefix = root / CAPTURE_STAGING_PREFIX
            return normalized == prefix or prefix in normalized.parents

        def observed_flock(fd: int, operation: int) -> Any:
            if operation & fcntl.LOCK_EX:
                events.append(("exclusive-flock", fd))
            return original_flock(fd, operation)

        def observed_open(path: Any, *args: Any, **kwargs: Any) -> int:
            if is_aggregate_staging_target(path):
                events.append(("aggregate-staging-open", os.fsdecode(path)))
            return original_open(path, *args, **kwargs)

        def observed_mkdir(path: Any, *args: Any, **kwargs: Any) -> None:
            if is_aggregate_staging_target(path):
                events.append(("aggregate-staging-mkdir", os.fsdecode(path)))
            original_mkdir(path, *args, **kwargs)

        core.fcntl.flock = observed_flock
        core.os.open = observed_open
        core.os.mkdir = observed_mkdir
        try:
            with core.CoordinatorLease(root) as lease:
                exclusive = [
                    (index, value)
                    for index, (kind, value) in enumerate(events)
                    if kind == "exclusive-flock"
                ]
                if len(exclusive) != 2:
                    raise AggregateCaptureRefusal(
                        "temporary coordinator did not acquire exactly two locks."
                    )
                if exclusive[0][1] != lease.authority_fd:
                    raise AggregateCaptureRefusal(
                        "global capture authority was not acquired first."
                    )
                if exclusive[1][1] != lease.fd:
                    raise AggregateCaptureRefusal(
                        "aggregate staging lock was not acquired second."
                    )
                staging_event_indexes = [
                    index
                    for index, (kind, _) in enumerate(events)
                    if kind.startswith("aggregate-staging-")
                ]
                if (
                    not staging_event_indexes
                    or exclusive[0][0] >= min(staging_event_indexes)
                    or exclusive[1][0] <= min(staging_event_indexes)
                ):
                    raise AggregateCaptureRefusal(
                        "aggregate staging was accessed before global authority."
                    )
                staging_events_before_contention = len(staging_event_indexes)
                try:
                    with core.CoordinatorLease(root):
                        raise AggregateCaptureRefusal(
                            "concurrent temporary coordinator unexpectedly acquired locks."
                        )
                except core.CaptureRefusal:
                    pass
                staging_events_after_contention = sum(
                    1
                    for kind, _ in events
                    if kind.startswith("aggregate-staging-")
                )
                if staging_events_after_contention != staging_events_before_contention:
                    raise AggregateCaptureRefusal(
                        "a refused competing lease accessed aggregate staging."
                    )
        finally:
            core.fcntl.flock = original_flock
            core.os.open = original_open
            core.os.mkdir = original_mkdir


def run_offline_self_test(repository_root: pathlib.Path) -> None:
    root = _require_repository_root(repository_root)
    core = _load_configured_core(root)
    guard = AggregateSourceGuard(root, core)
    pins = guard.validate_all()
    if (
        set(pins) != set(CAPTURE_PIN_KEYS)
        or "automationRelativePath" in pins
        or "automationSha256" in pins
        or core.CAPTURE_STAGING_PREFIX != CAPTURE_STAGING_PREFIX.as_posix()
        or core.CAPTURE_COORDINATOR_AUTHORITY_DIRECTORY
        != GLOBAL_AUTHORITY_DIRECTORY.as_posix()
        or core.CAPTURE_COORDINATOR_AUTHORITY_NAME != GLOBAL_AUTHORITY_LOCK_NAME
        or core.CAPTURE_COORDINATOR_STAGING_LOCK_NAME
        != AGGREGATE_STAGING_LOCK_NAME
        or not _SHA256_RE.fullmatch(pins["sourceClosureSha256"])
        or not _SHA256_RE.fullmatch(EXPECTED_RUNTIME_PROFILE_SHA256)
        or pins["aggregateChecklistSha256"]
        != EXPECTED_AGGREGATE_CHECKLIST_SHA256
        or pins["productionCaptureBlockersSha256"]
        != EXPECTED_PRODUCTION_CAPTURE_BLOCKERS_SHA256
        or pins["frozenWorkflowSha256"] != EXPECTED_FROZEN_WORKFLOW_SHA256
        or pins["frozenWorkflowCompatible"] != "false"
        or len(PRODUCTION_CAPTURE_BLOCKERS) != 2
        or FROZEN_WORKFLOW_COMPATIBLE_WITH_AGGREGATE_SCOPE
        or tuple(core.SESSION_IDS) != FROZEN_WORKFLOW_SESSION_IDS
        or core.CoordinatorLease.__name__
        != "AuthorityFirstAggregateCoordinatorLease"
        or core.CaptureCoordinator.__name__
        != "AuthorityFirstAggregateCaptureCoordinator"
        or PRODUCTION_CAPTURE_ENABLED
    ):
        raise AggregateCaptureRefusal(
            "aggregate capture source or namespace self-test failed."
        )
    expected_cue = (
        f'FILE "{CANDIDATE_STEM}.bin" BINARY\n'
        "  TRACK 01 MODE2/2352\n"
        "    INDEX 01 00:00:00\n"
    ).encode("ascii")
    if (
        len(expected_cue) != EXPECTED_CANDIDATE_CUE_SIZE
        or _sha256_bytes(expected_cue) != EXPECTED_CANDIDATE_CUE_SHA256
    ):
        raise AggregateCaptureRefusal("aggregate CUE projection self-test failed.")
    _exercise_temporary_lock_order(core)


def _build_parser(core: types.ModuleType) -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    subparsers = parser.add_subparsers(dest="operation", required=True)
    subparsers.add_parser("self-test")
    subparsers.add_parser("status")
    subparsers.add_parser("preflight")
    capture = subparsers.add_parser("capture-session")
    capture.add_argument("session_id", choices=core.SESSION_IDS)
    verify = subparsers.add_parser("verify-staging")
    verify.add_argument("run_id")
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    root: pathlib.Path | None = None
    core: types.ModuleType | None = None
    args: argparse.Namespace | None = None
    committed_run_id: str | None = None
    coordinator: Any | None = None
    try:
        root = _require_repository_root(PRODUCTION_REPOSITORY_ROOT)
        core = _load_configured_core(root)
        args = _build_parser(core).parse_args(argv)
        if args.operation == "self-test":
            run_offline_self_test(root)
            print("ID65_AUTHORING_AGGREGATE_CAPTURE_SELF_TEST_OK")
            return 0
        if args.operation == "status":
            print(_canonical_json_bytes(_static_status(root)).decode("utf-8"))
            return 0

        _require_production_capture_enabled()
        guard = AggregateSourceGuard(root, core)
        boundary = core.RealMacBoundary(root)
        coordinator = core.CaptureCoordinator(
            boundary,
            source_guard=guard,
            staging=core.AnchoredStaging(root),
            lease_factory=lambda: core.CoordinatorLease(root),
        )
        if args.operation == "preflight":
            coordinator.preflight()
            print("ID65_AUTHORING_AGGREGATE_CAPTURE_PREFLIGHT_READY")
        elif args.operation == "capture-session":
            committed_run_id = coordinator.capture_session(args.session_id)
            print(
                "ID65_AUTHORING_AGGREGATE_CAPTURE_SESSION_SEALED "
                f"run-id={committed_run_id}",
                flush=True,
            )
        elif args.operation == "verify-staging":
            coordinator.verify_staging(args.run_id)
            print(
                "ID65_AUTHORING_AGGREGATE_RAW_STAGING_CHECKLIST_VERIFIED "
                f"sessions={len(core.SESSION_IDS)} "
                f"checks={len(EXPECTED_AGGREGATE_CHECK_IDS)} "
                "evidence-accepted=false"
            )
        else:
            raise AggregateCaptureRefusal("unknown aggregate capture operation.")
        return 0
    except BaseException as exc:
        if committed_run_id is not None or (
            coordinator is not None
            and coordinator.last_committed_run_id is not None
        ):
            return 0
        refused_types: tuple[type[BaseException], ...] = (
            AggregateCaptureRefusal,
            OSError,
            ValueError,
            KeyboardInterrupt,
        )
        if core is not None:
            refused_types += (core.CaptureRefusal,)
        if not isinstance(exc, refused_types):
            raise
        print(f"REFUSED: {exc}", file=sys.stderr)
        if args is not None and args.operation == "capture-session":
            print(
                "Capture did not complete. If DuckStation was manually launched for "
                "this attempt, quit it before rerunning.",
                file=sys.stderr,
            )
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
