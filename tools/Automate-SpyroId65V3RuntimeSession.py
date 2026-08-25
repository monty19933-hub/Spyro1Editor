#!/usr/bin/env python3
"""Fail-closed input transport foundation for an existing pinned DuckStation.

This tool's production CLI does not launch DuckStation, connect to its GDB
server, capture media, write evidence, or finalize runtime acceptance.  It
provides the narrow pieces needed for a later unattended ID65 V3 capture
coordinator:

* exact PID/start-time/UID/path and mapped-text-vnode process pinning;
* a canonical, replay-resistant input-plan format;
* PID-targeted macOS keyboard events behind the existing Accessibility grant;
* a fixed action allowlist with save/load-state keys intentionally absent;
* exception-safe release of every host key that might have been pressed; and
* a read-only pad-word confirmation interface;
* an offline-only, production-unwired one-connection GDB lifecycle; and
* an offline-only adapter that orders one complete host pulse around that
  lifecycle without exposing either capability through the production CLI; and
* the manual V3 capture coordinator's persistent cross-tool advisory lease.

The frozen external-HOME profile binds PowerOff only to F12 and TogglePause
only to Space.  Those actions are enabled only after the complete hash-pinned
profile-verifier/settings/receipt/app/package chain succeeds.  Escape and all
save/load-state transports remain forbidden.  PAD_ANALOG/M remains parseable
for plan-format continuity but is unavailable because the frozen settings do
not bind it.

Mode-specific results are deliberately narrow: SELF_TEST_OK, PREFLIGHT_READY,
or INPUT_POSTED_PENDING_RUNTIME_CONFIRMATION.  None is media capture, gameplay
acceptance, evidence finalization, or release acceptance.
"""

from __future__ import annotations

import argparse
import configparser
import contextlib
import ctypes
import dataclasses
import datetime as dt
import enum
import fcntl
import hashlib
import io
import json
import math
import os
import pathlib
import re
import socket
import stat
import subprocess
import sys
import threading
import time
from collections.abc import Callable, Mapping, Sequence
from types import MappingProxyType, ModuleType
from typing import Any, Final, Protocol


PLAN_FORMAT: Final = "spyro-editor-id65-v3-unattended-input-plan"
PLAN_FORMAT_VERSION: Final = 2
PROFILE_ID: Final = "duckstation-id65-unattended-v1"
SELF_TEST_STATUS: Final = "SELF_TEST_OK"
PREFLIGHT_STATUS: Final = "PREFLIGHT_READY"
INPUT_STATUS: Final = "INPUT_POSTED_PENDING_RUNTIME_CONFIRMATION"
UNATTENDED_STATE_RELATIVE_PATH: Final = (
    "_local/v5-stone-hill-level-replacement/id65-v3-unattended-private-state"
)
CAPTURE_STAGING_PREFIX: Final = (
    "docs/runtime-evidence/assets/"
    "unused-level-65-remote-blank-environment-header-repair-v3/"
    "capture-staging"
)
CAPTURE_COORDINATOR_AUTHORITY_DIRECTORY: Final = (
    "docs/runtime-evidence/assets/"
    "unused-level-65-remote-blank-environment-header-repair-v3"
)
CAPTURE_COORDINATOR_AUTHORITY_NAME: Final = (
    ".capture-coordinator-authority.lock"
)
CAPTURE_COORDINATOR_STAGING_LOCK_NAME: Final = ".capture-coordinator.lock"
MAX_PLAN_BYTES: Final = 256 * 1024
MAX_PLAN_EVENTS: Final = 256
KEY_PULSE_SECONDS: Final = 0.035
PAD_CONFIRMATION_TIMEOUT_SECONDS: Final = 1.5
PAD_CONFIRMATION_INTERVAL_SECONDS: Final = 0.01
PAD_CONFIRMATION_STABLE_SAMPLES: Final = 2

# These pre-framed packets are the session's entire GDB command surface.  The
# two memory reads remain available through narrow read methods.  The one
# continue packet is owned only by the connect/resume lifecycle below; there is
# intentionally no generic address, packet, pause, step, or breakpoint API.
GDB_HOST: Final = "127.0.0.1"
GDB_PORT: Final = 2345
GDB_SOURCE_COMMIT: Final = "e7f2f101c2489e68446deff6dae9083e7a2114ea"
GDB_SOCKET_TIMEOUT_SECONDS: Final = 2.0
GDB_MAX_RSP_PAYLOAD_BYTES: Final = 4096
GDB_MAX_RSP_EVENTS_PER_READ: Final = 32
# This is an evidence-acceptance deadline, not a promise that every native
# subprocess returns within 250 ms.  The clock is checked after every blocking
# sample so late evidence is always refused.  Native socket/lsof calls retain
# their own fail-closed timeouts.
GDB_VBLANK_PROGRESS_TIMEOUT_SECONDS: Final = 0.25
GDB_VBLANK_PROGRESS_INTERVAL_SECONDS: Final = 0.01
GDB_VBLANK_PROGRESS_MAX_SAMPLES: Final = 32
GDB_PAUSED_CONFIRMATION_MIN_SECONDS: Final = 0.05
# This outer pulse-acceptance deadline begins before a planned transition is
# armed.  Expiry can never veto an already-owed physical key-up; the transition
# is consumed and classified after release, then the late pulse is refused.
# Production remains unwired until every native boundary shares one absolute
# operation/cancellation budget rather than this offline adapter's clock.
GDB_SPACE_PULSE_ACCEPTANCE_TIMEOUT_SECONDS: Final = 2.0
GDB_PULSE_RELEASE_CLEANUP_ATTEMPTS: Final = 2
GDB_PAD_READ_PACKET: Final = b"$m800786a0,4#9b"
GDB_VBLANK_READ_PACKET: Final = b"$m800758c8,4#a4"
GDB_CONTINUE_PACKET: Final = b"$c#63"
GDB_READ_ONLY_OUTBOUND: Final = frozenset({
    GDB_PAD_READ_PACKET,
    GDB_VBLANK_READ_PACKET,
    b"+",
    b"-",
})
GDB_SESSION_OUTBOUND: Final = frozenset({
    *GDB_READ_ONLY_OUTBOUND,
    GDB_CONTINUE_PACKET,
})
GDB_LSOF_COMMAND: Final = (
    "/usr/sbin/lsof",
    "-nP",
    "-a",
    "-iTCP:2345",
    "-sTCP:LISTEN",
    "-F0pfadDikstnPT",
)
GDB_CONNECTION_SET_LSOF_COMMAND: Final = (
    "/usr/sbin/lsof",
    "-nP",
    "-Tqs",
    "-a",
    "-iTCP:2345",
    "-F0pfadDikstnPT",
)

PROFILE_PACKAGE_RELATIVE_PATH: Final = (
    "_local/v5-stone-hill-level-replacement/duckstation-id65-unattended-v1"
)
PACKAGED_APP_RELATIVE_PATH: Final = (
    f"{PROFILE_PACKAGE_RELATIVE_PATH}/DuckStation-ID65-Unattended-v1.app"
)
DUCKSTATION_EXECUTABLE_RELATIVE_PATH: Final = (
    f"{PACKAGED_APP_RELATIVE_PATH}/Contents/MacOS/DuckStation"
)
EXTERNAL_HOME_DATA_RELATIVE_PATH: Final = (
    f"{PROFILE_PACKAGE_RELATIVE_PATH}/home/Library/Application Support/DuckStation"
)
SETTINGS_RELATIVE_PATH: Final = f"{EXTERNAL_HOME_DATA_RELATIVE_PATH}/settings.ini"
RECEIPT_RELATIVE_PATH: Final = (
    f"{PROFILE_PACKAGE_RELATIVE_PATH}/handoff-receipt.json"
)
PROFILE_VERIFIER_RELATIVE_PATH: Final = "tools/Verify-SpyroId65UnattendedProfile.py"
CANDIDATE_DIRECTORY: Final = (
    "_local/v5-stone-hill-level-replacement/"
    "unused-level-65-remote-blank-environment-header-repair-v3"
)
CANDIDATE_STEM: Final = (
    "Unused-Level-65-Remote-Blank-Environment-Header-Repair-v3-"
    "Sector216-RUNTIME-CANDIDATE"
)
CANDIDATE_CUE_RELATIVE_PATH: Final = (
    f"{CANDIDATE_DIRECTORY}/{CANDIDATE_STEM}.cue"
)
CANDIDATE_BIN_RELATIVE_PATH: Final = (
    f"{CANDIDATE_DIRECTORY}/{CANDIDATE_STEM}.bin"
)
EXPECTED_DUCKSTATION_SHA256: Final = (
    "14c22c6e1f4bbff13c00c24f17424ef8cf7645ee9917f7d65b17cd3fe1dd1645"
)
EXPECTED_SETTINGS_SHA256: Final = (
    "469ef98771f26395352bac1d43f90e4be14693fd4ba5316e4d71e0557a7f74f9"
)
EXPECTED_PROFILE_VERIFIER_SHA256: Final = (
    "74f4ed279a3e3f60af6c4c0a0b1fa8ffc0a67886c478d8cc7d1a288b093c3671"
)
EXPECTED_HANDOFF_RECEIPT_SHA256: Final = (
    "a748153bdd554187fb30ee79e75d25d36c606663bd4aee8444826b7612aa27d5"
)
EXPECTED_APP_CONTENT_MANIFEST_SHA256: Final = (
    "95a8142e561d6b37f228728abc8dbf034c774887213eb59bc14518d56c2d222e"
)
EXPECTED_APP_OUTPUT_TREE_MANIFEST_SHA256: Final = (
    "08d0a74450b709ab7e702182d64db55f6bc3395728181b052fec307c4d320ba7"
)
EXPECTED_PACKAGE_ENVELOPE_SHA256: Final = (
    "03e6f465647c04ee7d86add2550e1ba6818972233be419ab98290f05158ffff3"
)
EXPECTED_EXTERNAL_HOME_BIOS_SHA256: Final = (
    "71af94d1e47a68c11e8fdb9f8368040601514a42a5a399cda48c7d3bff1e99d3"
)
EXPECTED_HOST_SETTINGS_SENTINEL_SHA256: Final = (
    "896daa612142659aa950c16d2435f81cb034aa8b56768b91e60a4ec8f0a5ccc9"
)
HOST_SETTINGS_SENTINEL_ID: Final = "duckstation-normal-user-settings.ini"
EXPECTED_RECEIPT_CLOSURE_SHA256: Final = (
    "722a7ac322fa88749f17762185256281a4d6a930a290e35cdb955937fe57aead"
)
EXPECTED_HOME_OUTPUT_TREE_MANIFEST_SHA256: Final = (
    "b00119fd7647647c81c7571d2507dbbce2075bd10cdcd8af8db8ae1e006fc2b0"
)
EXPECTED_CANDIDATE_MANIFEST_SHA256: Final = (
    "1c6824d78e8cb43f791b1b184b5cba2746352073114be120fc2a4e25f25e2e97"
)
EXPECTED_APP_VERSION: Final = "0.1-11580-ge7f2f101c"
EXPECTED_RECEIPT_FORMAT: Final = "spyro-id65-unattended-external-home-handoff"
EXPECTED_RECEIPT_FORMAT_VERSION: Final = 1
EXPECTED_APP_CONTENT_MANIFEST_FORMAT: Final = (
    "spyro-id65-duckstation-app-content-manifest-v1"
)
EXPECTED_OUTPUT_TREE_MANIFEST_FORMAT: Final = (
    "spyro-id65-output-tree-manifest-v1"
)
EXPECTED_PACKAGE_ENVELOPE_FORMAT: Final = (
    "spyro-id65-package-envelope-manifest-v1"
)
EXPECTED_RECEIPT_SIZE: Final = 73_908
EXPECTED_EXECUTABLE_SIZE: Final = 22_354_208
EXPECTED_SETTINGS_SIZE: Final = 2_143
EXPECTED_BIOS_SIZE: Final = 524_288
EXPECTED_CANDIDATE_CUE_SIZE: Final = 148
EXPECTED_CANDIDATE_BIN_SIZE: Final = 661_547_040
EXPECTED_APP_CONTENT_ENTRY_COUNT: Final = 292
EXPECTED_APP_OUTPUT_TREE_ENTRY_COUNT: Final = 292
EXPECTED_HOME_OUTPUT_TREE_ENTRY_COUNT: Final = 12
EXPECTED_PACKAGE_ENVELOPE_ENTRY_COUNT: Final = 4
EXPECTED_HOST_SETTINGS_SIZE: Final = 10_058
EXPECTED_HOST_SETTINGS_MODE: Final = 0o600
BIOS_DATA_RELATIVE_PATH: Final = (
    "bios/Sony PlayStation SCPH-1001 - DTLH-3000 BIOS v2.2 "
    "(1995-12-04)(Sony)(US).bin"
)
EXPECTED_HOST_SETTINGS_PATH: Final = pathlib.Path(
    "/Users/john/Library/Application Support/DuckStation/settings.ini"
)
PROFILE_SNAPSHOT_FORMAT: Final = "spyro-editor-id65-v3-runtime-profile-snapshot"
PROFILE_SNAPSHOT_VERSION: Final = 2
PROFILE_SNAPSHOT_SESSION_ID: Final = "id65-v3-unattended-automation"
PROFILE_SNAPSHOT_PROCESS_INSTANCE_ID: Final = "runtime-attestation-pending"
PROFILE_SNAPSHOT_KEYS: Final = frozenset({
    "format",
    "formatVersion",
    "snapshotId",
    "phase",
    "sessionId",
    "processInstanceId",
    "profileId",
    "capturedAtUtc",
    "duckStationExecutableSha256",
    "handoffReceiptSha256",
    "appContentManifestSha256",
    "appOutputTreeManifestSha256",
    "packageEnvelopeSha256",
    "externalHomeSettingsSha256",
    "externalHomeBiosSha256",
    "hostNormalSettingsSentinelId",
    "hostNormalSettingsSentinelSha256",
    "dataFiles",
})
AUTOMATION_REPOSITORY_ROOT: Final = pathlib.Path(
    os.path.abspath(__file__)
).parent.parent
EXPECTED_CANDIDATE_CUE_SHA256: Final = (
    "ab91adb4ffcbad70bf7a226a2a4a6d0f6efe6b54fbed0b8126e59d1b8ecaf6df"
)
EXPECTED_CANDIDATE_BIN_SHA256: Final = (
    "42e4d5cae98d4786023bef26517d0435b43aff365b797495386813f44ecb0c45"
)

PLAN_NONCE_RE: Final = re.compile(r"\A[0-9a-f]{32}\Z", re.ASCII)
SHA256_RE: Final = re.compile(r"\A[0-9a-f]{64}\Z", re.ASCII)
CANONICAL_UTC_RE: Final = re.compile(
    r"\A\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z\Z", re.ASCII
)
FORBIDDEN_ACTION_TOKENS: Final = frozenset({
    "ESCAPE", "F1", "F2", "F3", "F4", "LOAD", "SAVE", "SLOT", "STATE"
})


class AutomationRefusal(RuntimeError):
    """A fail-closed refusal that must not be converted into acceptance."""


class _GdbUnexpectedStopRefusal(AutomationRefusal):
    """A checksum-valid S/T notification proved that emulation stopped."""


class _GdbTopologyDriftRefusal(AutomationRefusal):
    """The exact process/listener/client topology became unprovable."""


class _GdbPauseContradictionRefusal(AutomationRefusal):
    """Guest progress directly contradicted a claimed stable paused state."""


class ReleasedWithEnvironmentDrift(AutomationRefusal):
    """The mandatory key-up posted, but non-vetoing runtime state drifted."""


class KeyDownNotPostedRefusal(AutomationRefusal):
    """A proven pre-post key-down refusal that creates no release obligation."""


class KeyAction(str, enum.Enum):
    PAD_ANALOG = "PAD_ANALOG"
    PAD_R1_TOGGLE = "PAD_R1_TOGGLE"
    PAD_R2_TOGGLE = "PAD_R2_TOGGLE"
    PAD_L1_TOGGLE = "PAD_L1_TOGGLE"
    PAD_L2_TOGGLE = "PAD_L2_TOGGLE"
    PAD_LEFT_TOGGLE = "PAD_LEFT_TOGGLE"
    PAD_DOWN_TOGGLE = "PAD_DOWN_TOGGLE"
    TOGGLE_PAUSE = "TOGGLE_PAUSE"
    POWER_OFF = "POWER_OFF"


class PadState(str, enum.Enum):
    HELD = "HELD"
    RELEASED = "RELEASED"
    NOT_APPLICABLE = "NOT_APPLICABLE"


@dataclasses.dataclass(frozen=True)
class ActionSpec:
    key_code: int
    profile_binding: tuple[str, str, str]
    pad_bit: int | None


# macOS virtual key codes.  Escape (53) is deliberately absent.
ACTION_SPECS: Final[Mapping[KeyAction, ActionSpec]] = MappingProxyType({
    KeyAction.PAD_ANALOG: ActionSpec(46, ("Pad1", "Analog", "Keyboard/M"), None),
    KeyAction.PAD_R1_TOGGLE: ActionSpec(
        18, ("Pad1", "Macro1", "Keyboard/1"), 11
    ),
    KeyAction.PAD_R2_TOGGLE: ActionSpec(
        19, ("Pad1", "Macro2", "Keyboard/2"), 9
    ),
    KeyAction.PAD_L1_TOGGLE: ActionSpec(
        20, ("Pad1", "Macro3", "Keyboard/3"), 10
    ),
    KeyAction.PAD_L2_TOGGLE: ActionSpec(
        21, ("Pad1", "Macro4", "Keyboard/4"), 8
    ),
    KeyAction.PAD_LEFT_TOGGLE: ActionSpec(
        23, ("Pad1", "Macro5", "Keyboard/5"), 7
    ),
    KeyAction.PAD_DOWN_TOGGLE: ActionSpec(
        22, ("Pad1", "Macro6", "Keyboard/6"), 6
    ),
    KeyAction.TOGGLE_PAUSE: ActionSpec(
        49, ("Hotkeys", "TogglePause", "Keyboard/Space"), None
    ),
    # Available only through the complete frozen external-HOME verifier chain.
    KeyAction.POWER_OFF: ActionSpec(
        111, ("Hotkeys", "PowerOff", "Keyboard/F12"), None
    ),
})


@dataclasses.dataclass(frozen=True)
class InputEvent:
    sequence: int
    nonce: str
    action: KeyAction
    expected_pad_state: PadState


@dataclasses.dataclass(frozen=True)
class InputPlan:
    profile_id: str
    plan_nonce: str
    events: tuple[InputEvent, ...]
    sha256: str

    @staticmethod
    def parse(raw: bytes) -> "InputPlan":
        if type(raw) is not bytes or not raw or len(raw) > MAX_PLAN_BYTES:
            raise AutomationRefusal("Input plan has an invalid byte length.")
        if raw.startswith(b"\xef\xbb\xbf"):
            raise AutomationRefusal("Input plan may not contain a UTF-8 BOM.")
        try:
            text = raw.decode("utf-8", "strict")
            payload = json.loads(text, object_pairs_hook=_unique_object)
        except (UnicodeDecodeError, json.JSONDecodeError) as exc:
            raise AutomationRefusal("Input plan is not strict UTF-8 JSON.") from exc
        if type(payload) is not dict:
            raise AutomationRefusal("Input plan root must be an object.")
        _require_exact_keys(
            payload,
            {"events", "format", "formatVersion", "planNonce", "profileId"},
            "input plan",
        )
        if payload["format"] != PLAN_FORMAT:
            raise AutomationRefusal("Input plan format is not pinned.")
        if (
            type(payload["formatVersion"]) is not int
            or payload["formatVersion"] != PLAN_FORMAT_VERSION
        ):
            raise AutomationRefusal("Input plan format version is not pinned.")
        if payload["profileId"] != PROFILE_ID:
            raise AutomationRefusal("Input plan profile is not the frozen ID65 profile.")
        plan_nonce = payload["planNonce"]
        if type(plan_nonce) is not str or PLAN_NONCE_RE.fullmatch(plan_nonce) is None:
            raise AutomationRefusal("Input plan nonce is not 32 lowercase hex characters.")
        raw_events = payload["events"]
        if (
            type(raw_events) is not list
            or not raw_events
            or len(raw_events) > MAX_PLAN_EVENTS
        ):
            raise AutomationRefusal("Input plan must contain 1..256 events.")
        parsed: list[InputEvent] = []
        for index, value in enumerate(raw_events):
            if type(value) is not dict:
                raise AutomationRefusal(f"Input event {index} is not an object.")
            _require_exact_keys(
                value,
                {"action", "expectedPadState", "nonce", "sequence"},
                f"input event {index}",
            )
            if type(value["sequence"]) is not int or value["sequence"] != index:
                raise AutomationRefusal("Input event sequence is reordered or non-monotonic.")
            expected_nonce = f"{plan_nonce}-{index:08x}"
            if value["nonce"] != expected_nonce:
                raise AutomationRefusal("Input event nonce is reordered, replayed, or non-canonical.")
            action_text = value["action"]
            if type(action_text) is not str:
                raise AutomationRefusal("Input event action must be a fixed string.")
            upper_tokens = set(filter(None, re.split(r"[^A-Z0-9]+", action_text.upper())))
            if upper_tokens & FORBIDDEN_ACTION_TOKENS:
                raise AutomationRefusal("Save/load-state, Escape, and slot actions are forbidden.")
            try:
                action = KeyAction(action_text)
            except ValueError as exc:
                raise AutomationRefusal("Input event action is outside the fixed allowlist.") from exc
            try:
                expected_pad_state = PadState(value["expectedPadState"])
            except (TypeError, ValueError) as exc:
                raise AutomationRefusal("Input event pad state is invalid.") from exc
            pad_bit = ACTION_SPECS[action].pad_bit
            if pad_bit is None and expected_pad_state is not PadState.NOT_APPLICABLE:
                raise AutomationRefusal("Non-pad action cannot claim a pad held/released state.")
            if pad_bit is not None and expected_pad_state is PadState.NOT_APPLICABLE:
                raise AutomationRefusal("Mapped pad action requires held/released confirmation.")
            parsed.append(InputEvent(index, expected_nonce, action, expected_pad_state))
        power_off_events = [
            event for event in parsed if event.action is KeyAction.POWER_OFF
        ]
        if len(power_off_events) > 1:
            raise AutomationRefusal("PowerOff may appear at most once in an input plan.")
        if power_off_events and power_off_events[0].sequence != len(parsed) - 1:
            raise AutomationRefusal("PowerOff is terminal and must be the final input event.")
        for action, spec in ACTION_SPECS.items():
            if spec.pad_bit is None:
                continue
            action_events = [event for event in parsed if event.action is action]
            if not action_events:
                continue
            required = PadState.HELD
            for event in action_events:
                if event.expected_pad_state is not required:
                    raise AutomationRefusal(
                        f"Macro-toggle {action.value} must start HELD and alternate "
                        "HELD/RELEASED exactly."
                    )
                required = (
                    PadState.RELEASED
                    if required is PadState.HELD
                    else PadState.HELD
                )
            if action_events[-1].expected_pad_state is not PadState.RELEASED:
                raise AutomationRefusal(
                    f"Macro-toggle {action.value} plan ends HELD; terminal state must be RELEASED."
                )
        canonical = canonical_json(payload)
        if raw != canonical:
            raise AutomationRefusal("Input plan is not canonical JSON with one trailing newline.")
        return InputPlan(
            profile_id=PROFILE_ID,
            plan_nonce=plan_nonce,
            events=tuple(parsed),
            sha256=hashlib.sha256(raw).hexdigest(),
        )


class PlannedEmulationState(str, enum.Enum):
    RUNNING = "RUNNING"
    PAUSED = "PAUSED"


@dataclasses.dataclass(frozen=True)
class GdbSessionInputLifecycle:
    """Pure plan result relative to the post-continue RUNNING baseline."""

    states_after_events: tuple[PlannedEmulationState, ...]
    toggle_pause_count: int
    terminal_state: PlannedEmulationState


def validate_gdb_session_input_lifecycle(
    plan: InputPlan,
) -> GdbSessionInputLifecycle:
    """Validate only the future one-shot GDB execution policy.

    The existing production foundation remains unwired.  A later coordinator
    must call this pure function before any listener connection or host event.
    PowerOff is deliberately excluded until process exit has its own terminal
    evidence contract.
    """

    if type(plan) is not InputPlan or not plan.events:
        raise AutomationRefusal("GDB session input lifecycle plan is malformed.")
    state = PlannedEmulationState.RUNNING
    states: list[PlannedEmulationState] = []
    toggle_count = 0
    for event in plan.events:
        if event.action is KeyAction.POWER_OFF:
            raise AutomationRefusal(
                "PowerOff is unavailable in the one-shot GDB session lifecycle."
            )
        if state is PlannedEmulationState.PAUSED:
            if event.action is not KeyAction.TOGGLE_PAUSE:
                raise AutomationRefusal(
                    "Only the matching TogglePause resume may follow a planned pause."
                )
            state = PlannedEmulationState.RUNNING
            toggle_count += 1
        elif event.action is KeyAction.TOGGLE_PAUSE:
            state = PlannedEmulationState.PAUSED
            toggle_count += 1
        states.append(state)
    if state is not PlannedEmulationState.RUNNING or toggle_count % 2 != 0:
        raise AutomationRefusal(
            "TogglePause lifecycle must contain pairs and terminate RUNNING."
        )
    return GdbSessionInputLifecycle(tuple(states), toggle_count, state)


def _unique_object(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for key, value in pairs:
        if key in result:
            raise AutomationRefusal(f"Duplicate JSON member {key!r} is forbidden.")
        result[key] = value
    return result


def _require_exact_keys(
    value: Mapping[str, Any], expected: set[str], label: str
) -> None:
    if set(value) != expected:
        raise AutomationRefusal(f"{label} members are not the exact pinned schema.")


def canonical_json(value: Any) -> bytes:
    return (
        json.dumps(
            value,
            ensure_ascii=True,
            allow_nan=False,
            sort_keys=True,
            separators=(",", ":"),
        )
        + "\n"
    ).encode("ascii")


@dataclasses.dataclass(frozen=True)
class FileIdentity:
    real_path: str
    device: int
    inode: int
    byte_length: int
    owner_uid: int
    mode: int
    link_count: int
    sha256: str


@dataclasses.dataclass(frozen=True)
class ProcessIdentity:
    pid: int
    effective_uid: int
    real_uid: int
    saved_uid: int
    start_seconds: int
    start_microseconds: int
    executable: FileIdentity


@dataclasses.dataclass(frozen=True)
class VerifiedProfile:
    profile_id: str
    effective_uid: int
    executable: FileIdentity
    settings: FileIdentity
    candidate_cue: FileIdentity
    candidate_bin: FileIdentity
    handoff_receipt_sha256: str
    app_content_manifest_sha256: str
    app_output_tree_manifest_sha256: str
    package_envelope_sha256: str
    external_home_bios_sha256: str
    host_normal_settings_sentinel_sha256: str
    enabled_actions: frozenset[KeyAction]


class ProcessObserver(Protocol):
    def observe(self, pid: int) -> ProcessIdentity: ...
    def observe_release_identity(
        self, pid: int, baseline: ProcessIdentity
    ) -> ProcessIdentity: ...


class ProfileVerifier(Protocol):
    def verify(self) -> VerifiedProfile: ...


def read_pinned_file(
    path: pathlib.Path,
    expected_sha256: str,
    *,
    maximum_bytes: int,
    expected_owner_uid: int,
) -> FileIdentity:
    identity, _ = _read_stable_file(
        path,
        expected_sha256=expected_sha256,
        maximum_bytes=maximum_bytes,
        expected_owner_uid=expected_owner_uid,
        retain_bytes=False,
        label="Pinned resource",
    )
    return identity


def read_pinned_bytes(
    path: pathlib.Path,
    expected_sha256: str,
    *,
    maximum_bytes: int,
    expected_owner_uid: int,
) -> tuple[FileIdentity, bytes]:
    identity, raw = _read_stable_file(
        path,
        expected_sha256=expected_sha256,
        maximum_bytes=maximum_bytes,
        expected_owner_uid=expected_owner_uid,
        retain_bytes=True,
        label="Pinned resource",
    )
    if raw is None:
        raise AutomationRefusal("Pinned byte reader lost its retained bytes.")
    return identity, raw


def _read_stable_file(
    path: pathlib.Path,
    *,
    expected_sha256: str | None,
    maximum_bytes: int,
    expected_owner_uid: int,
    retain_bytes: bool,
    label: str,
) -> tuple[FileIdentity, bytes | None]:
    if maximum_bytes < 0:
        raise AutomationRefusal(f"{label} size bound is invalid.")
    if expected_sha256 is not None and SHA256_RE.fullmatch(expected_sha256) is None:
        raise AutomationRefusal("Expected resource SHA-256 is malformed.")
    absolute = pathlib.Path(os.path.abspath(path))
    if str(absolute) != os.path.realpath(absolute):
        raise AutomationRefusal(f"{label} path is a symlink: {absolute}")
    flags = os.O_RDONLY | getattr(os, "O_CLOEXEC", 0) | getattr(os, "O_NOFOLLOW", 0)
    try:
        descriptor = os.open(absolute, flags)
    except OSError as exc:
        raise AutomationRefusal(f"{label} is unavailable: {absolute}") from exc
    try:
        before = os.fstat(descriptor)
        if (
            not stat.S_ISREG(before.st_mode)
            or before.st_uid != expected_owner_uid
            or before.st_nlink != 1
            or before.st_size < 0
            or before.st_size > maximum_bytes
        ):
            raise AutomationRefusal(f"{label} metadata is unsafe: {absolute}")
        digest = hashlib.sha256()
        retained: list[bytes] | None = [] if retain_bytes else None
        total = 0
        while True:
            chunk = os.read(descriptor, 1024 * 1024)
            if not chunk:
                break
            total += len(chunk)
            if total > maximum_bytes:
                raise AutomationRefusal(f"{label} exceeded its size bound: {absolute}")
            digest.update(chunk)
            if retained is not None:
                retained.append(chunk)
        after = os.fstat(descriptor)
        try:
            path_after = os.stat(absolute, follow_symlinks=False)
        except OSError as exc:
            raise AutomationRefusal(f"{label} path changed while reading: {absolute}") from exc
    finally:
        os.close(descriptor)
    stable_fields = (
        "st_dev", "st_ino", "st_mode", "st_size", "st_uid", "st_nlink",
        "st_mtime_ns", "st_ctime_ns",
    )
    if any(getattr(before, field) != getattr(after, field) for field in stable_fields):
        raise AutomationRefusal(f"{label} changed while reading: {absolute}")
    if any(getattr(before, field) != getattr(path_after, field) for field in stable_fields):
        raise AutomationRefusal(f"{label} path was swapped while reading: {absolute}")
    observed_hash = digest.hexdigest()
    if expected_sha256 is not None and observed_hash != expected_sha256:
        raise AutomationRefusal(f"{label} SHA-256 changed: {absolute}")
    identity = FileIdentity(
        real_path=str(absolute),
        device=before.st_dev,
        inode=before.st_ino,
        byte_length=before.st_size,
        owner_uid=before.st_uid,
        mode=stat.S_IMODE(before.st_mode),
        link_count=before.st_nlink,
        sha256=observed_hash,
    )
    return identity, b"".join(retained) if retained is not None else None


def _require_audited_repository_root(repository_root: pathlib.Path) -> pathlib.Path:
    if not isinstance(repository_root, pathlib.Path):
        raise AutomationRefusal("Repository root must be an explicit pathlib.Path.")
    if (
        not repository_root.is_absolute()
        or pathlib.Path(os.path.normpath(os.fspath(repository_root))) != repository_root
    ):
        raise AutomationRefusal("Repository root must be absolute and normalized.")
    current = pathlib.Path(repository_root.anchor)
    for segment in repository_root.parts[1:]:
        current /= segment
        try:
            metadata = os.lstat(current)
        except OSError as exc:
            raise AutomationRefusal(
                f"Repository root segment is unavailable: {current}"
            ) from exc
        if stat.S_ISLNK(metadata.st_mode):
            raise AutomationRefusal("Repository root may not contain symlink segments.")
    if pathlib.Path(os.path.realpath(repository_root)) != repository_root:
        raise AutomationRefusal("Repository root is not the canonical physical path.")
    if repository_root != AUTOMATION_REPOSITORY_ROOT:
        raise AutomationRefusal(
            "Repository root does not match the audited Automation checkout."
        )
    return repository_root


def _require_external_home_verifier_contract(
    verifier: ModuleType, repository_root: pathlib.Path
) -> None:
    if getattr(verifier, "PRODUCTION_REPOSITORY_ROOT", None) != repository_root:
        raise AutomationRefusal("Frozen profile verifier repository identity changed.")
    if (
        getattr(verifier, "PROFILE_FORMAT", None) != PROFILE_SNAPSHOT_FORMAT
        or getattr(verifier, "PROFILE_FORMAT_VERSION", None)
        != PROFILE_SNAPSHOT_VERSION
    ):
        raise AutomationRefusal("Frozen profile verifier snapshot contract changed.")
    if not callable(getattr(verifier, "capture_runtime_snapshot", None)):
        raise AutomationRefusal("Frozen profile verifier snapshot API is unavailable.")
    pins = getattr(verifier, "PRODUCTION_PINS", None)
    expected = {
        "profile_id": PROFILE_ID,
        "package_relative_path": pathlib.PurePosixPath(PROFILE_PACKAGE_RELATIVE_PATH),
        "app_leaf": "DuckStation-ID65-Unattended-v1.app",
        "external_home_relative_path": pathlib.PurePosixPath("home"),
        "data_root_package_relative_path": pathlib.PurePosixPath(
            "home/Library/Application Support/DuckStation"
        ),
        "receipt_leaf": "handoff-receipt.json",
        "receipt_format": EXPECTED_RECEIPT_FORMAT,
        "receipt_format_version": EXPECTED_RECEIPT_FORMAT_VERSION,
        "receipt_size": EXPECTED_RECEIPT_SIZE,
        "receipt_sha256": EXPECTED_HANDOFF_RECEIPT_SHA256,
        "receipt_closure_sha256": EXPECTED_RECEIPT_CLOSURE_SHA256,
        "app_version": EXPECTED_APP_VERSION,
        "executable_app_relative_path": pathlib.PurePosixPath(
            "Contents/MacOS/DuckStation"
        ),
        "executable_size": EXPECTED_EXECUTABLE_SIZE,
        "executable_sha256": EXPECTED_DUCKSTATION_SHA256,
        "app_content_manifest_format": EXPECTED_APP_CONTENT_MANIFEST_FORMAT,
        "app_content_manifest_entry_count": EXPECTED_APP_CONTENT_ENTRY_COUNT,
        "app_content_manifest_sha256": EXPECTED_APP_CONTENT_MANIFEST_SHA256,
        "app_output_tree_entry_count": EXPECTED_APP_OUTPUT_TREE_ENTRY_COUNT,
        "app_output_tree_manifest_sha256": EXPECTED_APP_OUTPUT_TREE_MANIFEST_SHA256,
        "output_tree_manifest_format": EXPECTED_OUTPUT_TREE_MANIFEST_FORMAT,
        "home_output_tree_entry_count": EXPECTED_HOME_OUTPUT_TREE_ENTRY_COUNT,
        "home_output_tree_manifest_sha256": EXPECTED_HOME_OUTPUT_TREE_MANIFEST_SHA256,
        "package_envelope_format": EXPECTED_PACKAGE_ENVELOPE_FORMAT,
        "package_envelope_entry_count": EXPECTED_PACKAGE_ENVELOPE_ENTRY_COUNT,
        "package_envelope_sha256": EXPECTED_PACKAGE_ENVELOPE_SHA256,
        "settings_data_relative_path": pathlib.PurePosixPath("settings.ini"),
        "settings_size": EXPECTED_SETTINGS_SIZE,
        "settings_sha256": EXPECTED_SETTINGS_SHA256,
        "bios_data_relative_path": pathlib.PurePosixPath(BIOS_DATA_RELATIVE_PATH),
        "bios_size": EXPECTED_BIOS_SIZE,
        "bios_sha256": EXPECTED_EXTERNAL_HOME_BIOS_SHA256,
        "candidate_cue_relative_path": pathlib.PurePosixPath(
            CANDIDATE_CUE_RELATIVE_PATH
        ),
        "candidate_cue_size": EXPECTED_CANDIDATE_CUE_SIZE,
        "candidate_cue_sha256": EXPECTED_CANDIDATE_CUE_SHA256,
        "candidate_bin_relative_path": pathlib.PurePosixPath(
            CANDIDATE_BIN_RELATIVE_PATH
        ),
        "candidate_bin_size": EXPECTED_CANDIDATE_BIN_SIZE,
        "candidate_bin_sha256": EXPECTED_CANDIDATE_BIN_SHA256,
        "candidate_manifest_sha256": EXPECTED_CANDIDATE_MANIFEST_SHA256,
        "host_settings_path": EXPECTED_HOST_SETTINGS_PATH,
        "host_settings_sentinel_id": HOST_SETTINGS_SENTINEL_ID,
        "host_settings_size": EXPECTED_HOST_SETTINGS_SIZE,
        "host_settings_sha256": EXPECTED_HOST_SETTINGS_SENTINEL_SHA256,
        "host_settings_mode": EXPECTED_HOST_SETTINGS_MODE,
    }
    if pins is None:
        raise AutomationRefusal("Frozen profile verifier production pins are unavailable.")
    for name, value in expected.items():
        if getattr(pins, name, None) != value:
            raise AutomationRefusal(
                f"Frozen profile verifier production pin changed: {name}."
            )


def _load_external_home_verifier(repository_root: pathlib.Path) -> ModuleType:
    repository_root = _require_audited_repository_root(repository_root)
    source_path = repository_root / PROFILE_VERIFIER_RELATIVE_PATH
    identity, source = read_pinned_bytes(
        source_path,
        EXPECTED_PROFILE_VERIFIER_SHA256,
        maximum_bytes=1024 * 1024,
        expected_owner_uid=os.geteuid(),
    )
    if identity.mode & 0o022:
        raise AutomationRefusal("Frozen profile verifier source mode is writable by others.")
    module_name = "_spyro_id65_frozen_external_home_verifier_for_automation"
    module = ModuleType(module_name)
    module.__file__ = os.fspath(source_path)
    module.__package__ = ""
    previous = sys.modules.get(module_name)
    sys.modules[module_name] = module
    try:
        code = compile(source, os.fspath(source_path), "exec", dont_inherit=True)
        exec(code, module.__dict__)
        _require_external_home_verifier_contract(module, repository_root)
    except AutomationRefusal:
        if previous is None:
            sys.modules.pop(module_name, None)
        else:
            sys.modules[module_name] = previous
        raise
    except Exception as exc:
        if previous is None:
            sys.modules.pop(module_name, None)
        else:
            sys.modules[module_name] = previous
        raise AutomationRefusal("Frozen profile verifier could not be loaded safely.") from exc
    return module


def _canonical_utc_now() -> str:
    return (
        dt.datetime.now(dt.timezone.utc)
        .isoformat(timespec="milliseconds")
        .replace("+00:00", "Z")
    )


def _canonical_data_relative_path(value: object) -> str:
    if type(value) is not str or not value or "\\" in value:
        raise AutomationRefusal("Profile snapshot data path is not canonical POSIX text.")
    if any(ord(character) < 0x20 or ord(character) == 0x7f for character in value):
        raise AutomationRefusal("Profile snapshot data path contains control characters.")
    path = pathlib.PurePosixPath(value)
    if (
        path.is_absolute()
        or path.as_posix() != value
        or value in {".", ".."}
        or any(segment in {"", ".", ".."} for segment in value.split("/"))
    ):
        raise AutomationRefusal("Profile snapshot data path is not segment-safe.")
    return value


def _require_external_home_snapshot(
    snapshot: object, *, captured_at_utc: str
) -> None:
    if type(snapshot) is not dict or set(snapshot) != PROFILE_SNAPSHOT_KEYS:
        raise AutomationRefusal("Profile snapshot is not the exact v2 schema.")
    expected = {
        "format": PROFILE_SNAPSHOT_FORMAT,
        "formatVersion": PROFILE_SNAPSHOT_VERSION,
        "snapshotId": f"{PROFILE_SNAPSHOT_SESSION_ID}.profile-before",
        "phase": "before",
        "sessionId": PROFILE_SNAPSHOT_SESSION_ID,
        "processInstanceId": PROFILE_SNAPSHOT_PROCESS_INSTANCE_ID,
        "profileId": PROFILE_ID,
        "capturedAtUtc": captured_at_utc,
        "duckStationExecutableSha256": EXPECTED_DUCKSTATION_SHA256,
        "handoffReceiptSha256": EXPECTED_HANDOFF_RECEIPT_SHA256,
        "appContentManifestSha256": EXPECTED_APP_CONTENT_MANIFEST_SHA256,
        "appOutputTreeManifestSha256": EXPECTED_APP_OUTPUT_TREE_MANIFEST_SHA256,
        "packageEnvelopeSha256": EXPECTED_PACKAGE_ENVELOPE_SHA256,
        "externalHomeSettingsSha256": EXPECTED_SETTINGS_SHA256,
        "externalHomeBiosSha256": EXPECTED_EXTERNAL_HOME_BIOS_SHA256,
        "hostNormalSettingsSentinelId": HOST_SETTINGS_SENTINEL_ID,
        "hostNormalSettingsSentinelSha256": EXPECTED_HOST_SETTINGS_SENTINEL_SHA256,
    }
    for name, value in expected.items():
        if snapshot.get(name) != value or type(snapshot.get(name)) is not type(value):
            raise AutomationRefusal(f"Profile snapshot identity changed: {name}.")
    if CANONICAL_UTC_RE.fullmatch(captured_at_utc) is None:
        raise AutomationRefusal("Profile snapshot timestamp is not canonical UTC.")
    data_files = snapshot["dataFiles"]
    if type(data_files) is not list:
        raise AutomationRefusal("Profile snapshot dataFiles is not a list.")
    observed_paths: list[str] = []
    pinned_files: dict[str, tuple[int, str]] = {}
    for entry in data_files:
        if type(entry) is not dict or set(entry) != {
            "relativePath", "byteLength", "sha256"
        }:
            raise AutomationRefusal("Profile snapshot data-file schema changed.")
        relative_path = _canonical_data_relative_path(entry["relativePath"])
        byte_length = entry["byteLength"]
        digest = entry["sha256"]
        if type(byte_length) is not int or byte_length < 0:
            raise AutomationRefusal("Profile snapshot data-file size is invalid.")
        if type(digest) is not str or SHA256_RE.fullmatch(digest) is None:
            raise AutomationRefusal("Profile snapshot data-file SHA-256 is invalid.")
        observed_paths.append(relative_path)
        pinned_files[relative_path] = (byte_length, digest)
    if observed_paths != sorted(set(observed_paths)):
        raise AutomationRefusal("Profile snapshot dataFiles is reordered or duplicated.")
    if pinned_files.get("settings.ini") != (
        EXPECTED_SETTINGS_SIZE,
        EXPECTED_SETTINGS_SHA256,
    ):
        raise AutomationRefusal("Profile snapshot does not bind external-HOME settings.ini.")
    if pinned_files.get(BIOS_DATA_RELATIVE_PATH) != (
        EXPECTED_BIOS_SIZE,
        EXPECTED_EXTERNAL_HOME_BIOS_SHA256,
    ):
        raise AutomationRefusal("Profile snapshot does not bind the external-HOME BIOS.")


class FrozenProfileVerifier:
    """Read-only external-HOME snapshot and runtime-resource verification."""

    def __init__(self, repository_root: pathlib.Path) -> None:
        self.repository_root = _require_audited_repository_root(repository_root)

    def verify(self) -> VerifiedProfile:
        uid = os.geteuid()
        if uid == 0 or os.getuid() != uid:
            raise AutomationRefusal("Automation requires one non-root real/effective UID.")
        verifier = _load_external_home_verifier(self.repository_root)
        captured_at_utc = _canonical_utc_now()
        try:
            snapshot = verifier.capture_runtime_snapshot(
                self.repository_root,
                phase="before",
                session_id=PROFILE_SNAPSHOT_SESSION_ID,
                process_instance_id=PROFILE_SNAPSHOT_PROCESS_INSTANCE_ID,
                captured_at_utc=captured_at_utc,
            )
        except Exception as exc:
            raise AutomationRefusal(
                "Frozen external-HOME runtime snapshot was refused."
            ) from exc
        _require_external_home_snapshot(snapshot, captured_at_utc=captured_at_utc)
        executable = read_pinned_file(
            self.repository_root / DUCKSTATION_EXECUTABLE_RELATIVE_PATH,
            EXPECTED_DUCKSTATION_SHA256,
            maximum_bytes=256 * 1024 * 1024,
            expected_owner_uid=uid,
        )
        settings, settings_bytes = read_pinned_bytes(
            self.repository_root / SETTINGS_RELATIVE_PATH,
            EXPECTED_SETTINGS_SHA256,
            maximum_bytes=4 * 1024 * 1024,
            expected_owner_uid=uid,
        )
        cue = read_pinned_file(
            self.repository_root / CANDIDATE_CUE_RELATIVE_PATH,
            EXPECTED_CANDIDATE_CUE_SHA256,
            maximum_bytes=1024 * 1024,
            expected_owner_uid=uid,
        )
        candidate_bin = read_pinned_file(
            self.repository_root / CANDIDATE_BIN_RELATIVE_PATH,
            EXPECTED_CANDIDATE_BIN_SHA256,
            maximum_bytes=1024 * 1024 * 1024,
            expected_owner_uid=uid,
        )
        parser = configparser.ConfigParser(
            interpolation=None,
            strict=True,
            delimiters=("=",),
            comment_prefixes=("#", ";"),
            inline_comment_prefixes=None,
        )
        parser.optionxform = str
        try:
            settings_text = settings_bytes.decode("utf-8", "strict")
            parser.read_file(io.StringIO(settings_text, newline=None))
        except (UnicodeError, configparser.Error) as exc:
            raise AutomationRefusal("Pinned settings.ini cannot be decoded strictly.") from exc
        enabled: set[KeyAction] = set()
        for action, spec in ACTION_SPECS.items():
            section, key, expected = spec.profile_binding
            actual = parser.get(section, key, fallback=None)
            if actual == expected:
                enabled.add(action)
            elif action is KeyAction.PAD_ANALOG and actual is None:
                # The frozen external-HOME settings intentionally do not bind M.
                # Keeping the logical action in the parser but out of the verified
                # action set makes any plan that requests it refuse before input.
                continue
            else:
                raise AutomationRefusal(
                    f"Pinned controller/hotkey binding changed for {action.value}."
                )
        return VerifiedProfile(
            profile_id=PROFILE_ID,
            effective_uid=uid,
            executable=executable,
            settings=settings,
            candidate_cue=cue,
            candidate_bin=candidate_bin,
            handoff_receipt_sha256=EXPECTED_HANDOFF_RECEIPT_SHA256,
            app_content_manifest_sha256=EXPECTED_APP_CONTENT_MANIFEST_SHA256,
            app_output_tree_manifest_sha256=EXPECTED_APP_OUTPUT_TREE_MANIFEST_SHA256,
            package_envelope_sha256=EXPECTED_PACKAGE_ENVELOPE_SHA256,
            external_home_bios_sha256=EXPECTED_EXTERNAL_HOME_BIOS_SHA256,
            host_normal_settings_sentinel_sha256=(
                EXPECTED_HOST_SETTINGS_SENTINEL_SHA256
            ),
            enabled_actions=frozenset(enabled),
        )


class _ProcBsdInfo(ctypes.Structure):
    _fields_ = (
        [(name, ctypes.c_uint32) for name in (
            "flags", "status", "xstatus", "pid", "ppid", "uid", "gid",
            "ruid", "rgid", "svuid", "svgid", "reserved"
        )]
        + [("comm", ctypes.c_char * 16), ("name", ctypes.c_char * 32)]
        + [(name, ctypes.c_uint32) for name in (
            "nfiles", "pgid", "pjobc", "e_tdev", "e_tpgid"
        )]
        + [("nice", ctypes.c_int32), ("start_sec", ctypes.c_uint64),
           ("start_usec", ctypes.c_uint64)]
    )


class _ProcRegionInfo(ctypes.Structure):
    _fields_ = (
        [(name, ctypes.c_uint32) for name in (
            "protection", "max_protection", "inheritance", "flags"
        )]
        + [("offset", ctypes.c_uint64)]
        + [(name, ctypes.c_uint32) for name in (
            "behavior", "user_wired_count", "user_tag", "pages_resident",
            "pages_shared_now_private", "pages_swapped_out", "pages_dirtied",
            "ref_count", "shadow_depth", "share_mode", "private_pages_resident",
            "shared_pages_resident", "obj_id", "depth",
        )]
        + [("address", ctypes.c_uint64), ("size", ctypes.c_uint64)]
    )


class _VinfoStat(ctypes.Structure):
    _fields_ = (
        ("device", ctypes.c_uint32),
        ("mode", ctypes.c_uint16),
        ("link_count", ctypes.c_uint16),
        ("inode", ctypes.c_uint64),
        ("uid", ctypes.c_uint32),
        ("gid", ctypes.c_uint32),
        ("atime", ctypes.c_int64),
        ("atimensec", ctypes.c_int64),
        ("mtime", ctypes.c_int64),
        ("mtimensec", ctypes.c_int64),
        ("ctime", ctypes.c_int64),
        ("ctimensec", ctypes.c_int64),
        ("birthtime", ctypes.c_int64),
        ("birthtimensec", ctypes.c_int64),
        ("byte_length", ctypes.c_int64),
        ("blocks", ctypes.c_int64),
        ("block_size", ctypes.c_int32),
        ("user_flags", ctypes.c_uint32),
        ("generation", ctypes.c_uint32),
        ("raw_device", ctypes.c_uint32),
        ("spare", ctypes.c_int64 * 2),
    )


class _Fsid(ctypes.Structure):
    _fields_ = (("value", ctypes.c_int32 * 2),)


class _VnodeInfo(ctypes.Structure):
    _fields_ = (
        ("stat", _VinfoStat),
        ("vnode_type", ctypes.c_int32),
        ("padding", ctypes.c_int32),
        ("filesystem_id", _Fsid),
    )


class _VnodeInfoPath(ctypes.Structure):
    _fields_ = (("info", _VnodeInfo), ("path", ctypes.c_char * 1024))


class _ProcRegionWithPathInfo(ctypes.Structure):
    _fields_ = (("region", _ProcRegionInfo), ("vnode", _VnodeInfoPath))


@dataclasses.dataclass(frozen=True)
class MappedExecutableVnode:
    """One lsof-observed executable text vnode for an exact process."""

    device: int
    inode: int
    byte_length: int
    link_count: int
    real_path: str

    def __post_init__(self) -> None:
        if (
            type(self.device) is not int
            or self.device < 0
            or type(self.inode) is not int
            or self.inode <= 0
            or type(self.byte_length) is not int
            or self.byte_length <= 0
            or type(self.link_count) is not int
            or self.link_count < 0
            or not self.real_path.startswith("/")
        ):
            raise AutomationRefusal("Mapped executable text vnode is malformed.")


def _parse_text_vnodes(raw: bytes, *, pid: int) -> tuple[MappedExecutableVnode, ...]:
    """Parse all regular ``txt`` vnodes for one exact lsof process record."""

    if type(raw) is not bytes or type(pid) is not int or pid <= 1:
        raise AutomationRefusal("Mapped executable observation input is invalid.")
    process_records = 0
    observed: list[MappedExecutableVnode] = []
    for record in raw.split(b"\x00\n"):
        if not record:
            continue
        try:
            fields = [
                field.decode("utf-8", "strict")
                for field in record.split(b"\x00")
                if field
            ]
        except UnicodeDecodeError as exc:
            raise AutomationRefusal("lsof text-vnode output is not strict UTF-8.") from exc
        if not fields:
            continue
        if fields[0].startswith("p"):
            if fields[0] != f"p{pid}":
                raise AutomationRefusal("lsof returned a different process record.")
            process_records += 1
            continue
        if not fields[0].startswith("f"):
            raise AutomationRefusal("lsof returned an unknown text-vnode record kind.")
        values = {field[0]: field[1:] for field in fields if len(field) >= 2}
        if values.get("f") != "txt":
            continue
        if values.get("t") != "REG":
            raise AutomationRefusal("Executable text vnode is not a regular file.")
        try:
            observed_path = values["n"]
            if not observed_path.startswith("/"):
                raise AutomationRefusal(
                    "lsof executable text-vnode path is not absolute."
                )
            item = MappedExecutableVnode(
                device=int(values["D"], 0),
                inode=int(values["i"], 10),
                byte_length=int(values["s"], 10),
                link_count=int(values["k"], 10),
                real_path=os.path.realpath(observed_path),
            )
        except (KeyError, ValueError) as exc:
            raise AutomationRefusal("lsof executable text-vnode record is incomplete.") from exc
        observed.append(item)
    if process_records != 1:
        raise AutomationRefusal("lsof did not return one exact process record.")
    if not observed:
        raise AutomationRefusal("lsof omitted executable text vnodes.")
    return tuple(observed)


def _parse_mapped_text_vnode(
    raw: bytes, *, pid: int, executable_real_path: str
) -> MappedExecutableVnode:
    matches = tuple(
        item for item in _parse_text_vnodes(raw, pid=pid)
        if item.real_path == executable_real_path
    )
    if len(matches) != 1:
        raise AutomationRefusal(
            "lsof did not bind one exact mapped executable text vnode to DuckStation."
        )
    return matches[0]


class MacProcessObserver:
    PROC_PIDTBSDINFO = 3
    PROC_PIDREGIONPATHINFO = 8
    PROC_PIDPATHINFO_MAXSIZE = 4096
    VM_PROT_EXECUTE = 0x04

    def __init__(self) -> None:
        if sys.platform != "darwin":
            raise AutomationRefusal("Native process identity inspection is macOS-only.")
        self.libproc = ctypes.CDLL("/usr/lib/libproc.dylib", use_errno=True)
        self.libproc.proc_pidinfo.argtypes = (
            ctypes.c_int, ctypes.c_int, ctypes.c_uint64, ctypes.c_void_p, ctypes.c_int
        )
        self.libproc.proc_pidinfo.restype = ctypes.c_int
        self.libproc.proc_pidpath.argtypes = (
            ctypes.c_int, ctypes.c_void_p, ctypes.c_uint32
        )
        self.libproc.proc_pidpath.restype = ctypes.c_int

    def _bsd_info(self, pid: int) -> _ProcBsdInfo:
        bsd = _ProcBsdInfo()
        size = ctypes.sizeof(bsd)
        if self.libproc.proc_pidinfo(
            pid, self.PROC_PIDTBSDINFO, 0, ctypes.byref(bsd), size
        ) != size:
            raise AutomationRefusal("Exact DuckStation PID could not be inspected.")
        return bsd

    def observe(self, pid: int) -> ProcessIdentity:
        if type(pid) is not int or pid <= 1:
            raise AutomationRefusal("DuckStation PID must be an exact positive non-system PID.")
        bsd = self._bsd_info(pid)
        path_buffer = ctypes.create_string_buffer(self.PROC_PIDPATHINFO_MAXSIZE)
        path_length = self.libproc.proc_pidpath(
            pid, path_buffer, self.PROC_PIDPATHINFO_MAXSIZE
        )
        if path_length <= 0:
            raise AutomationRefusal("Exact DuckStation executable path is unavailable.")
        try:
            path = path_buffer.value.decode("utf-8", "strict")
        except UnicodeDecodeError as exc:
            raise AutomationRefusal("DuckStation executable path is not strict UTF-8.") from exc
        executable_real_path = os.path.realpath(path)
        mapped_vnode = self._mapped_text_vnode(
            pid, executable_real_path=executable_real_path
        )
        file_identity = _read_observed_executable(path, mapped_vnode=mapped_vnode)
        bsd_after = self._bsd_info(pid)
        stable_process_fields = (
            "pid", "uid", "ruid", "svuid", "start_sec", "start_usec"
        )
        if any(
            getattr(bsd, field) != getattr(bsd_after, field)
            for field in stable_process_fields
        ):
            raise AutomationRefusal("DuckStation identity changed during inspection.")
        path_after_buffer = ctypes.create_string_buffer(self.PROC_PIDPATHINFO_MAXSIZE)
        if self.libproc.proc_pidpath(
            pid, path_after_buffer, self.PROC_PIDPATHINFO_MAXSIZE
        ) <= 0:
            raise AutomationRefusal(
                "DuckStation executable path vanished during inspection."
            )
        try:
            path_after = path_after_buffer.value.decode("utf-8", "strict")
        except UnicodeDecodeError as exc:
            raise AutomationRefusal(
                "DuckStation final executable path is not strict UTF-8."
            ) from exc
        if path_after != path:
            raise AutomationRefusal("DuckStation executable path changed during inspection.")
        mapped_after = self._mapped_text_vnode(
            pid, executable_real_path=executable_real_path
        )
        if mapped_after != mapped_vnode:
            raise AutomationRefusal(
                "DuckStation mapped executable text vnode changed during inspection."
            )
        return ProcessIdentity(
            pid=int(bsd.pid),
            effective_uid=int(bsd.uid),
            real_uid=int(bsd.ruid),
            saved_uid=int(bsd.svuid),
            start_seconds=int(bsd.start_sec),
            start_microseconds=int(bsd.start_usec),
            executable=file_identity,
        )

    def observe_release_identity(
        self, pid: int, baseline: ProcessIdentity
    ) -> ProcessIdentity:
        """Observe release-safe identity without reopening proc_pidpath.

        A pathname replacement must not veto a key-up owed to the process that
        still maps the original executable vnode.  The baseline descriptor hash
        remains authoritative while libproc VM-region inspection proves that
        exact device/inode/size is still an executable process mapping.
        """

        if type(pid) is not int or pid <= 1 or baseline.pid != pid:
            raise AutomationRefusal("Release-only process observation is invalid.")
        before = self._bsd_info(pid)
        regions = self._mapped_executable_regions(pid, baseline.executable)
        after = self._bsd_info(pid)
        regions_after = self._mapped_executable_regions(pid, baseline.executable)
        if regions_after != regions:
            raise AutomationRefusal(
                "Release-only mapped executable vnode changed during inspection."
            )
        stable_process_fields = (
            "pid", "uid", "ruid", "svuid", "start_sec", "start_usec"
        )
        if any(
            getattr(before, field) != getattr(after, field)
            for field in stable_process_fields
        ):
            raise AutomationRefusal(
                "DuckStation identity changed during release-only inspection."
            )
        return ProcessIdentity(
            pid=int(before.pid),
            effective_uid=int(before.uid),
            real_uid=int(before.ruid),
            saved_uid=int(before.svuid),
            start_seconds=int(before.start_sec),
            start_microseconds=int(before.start_usec),
            executable=baseline.executable,
        )

    def _mapped_executable_regions(
        self, pid: int, executable: FileIdentity
    ) -> tuple[tuple[int, ...], ...]:
        structure_size = ctypes.sizeof(_ProcRegionWithPathInfo)
        if structure_size != 1272:
            raise AutomationRefusal("Native process-region structure layout is invalid.")
        cursor = 0
        matches: list[tuple[int, ...]] = []
        for _ in range(65536):
            observed = _ProcRegionWithPathInfo()
            result = self.libproc.proc_pidinfo(
                pid,
                self.PROC_PIDREGIONPATHINFO,
                cursor,
                ctypes.byref(observed),
                structure_size,
            )
            if result == 0:
                break
            if result != structure_size:
                raise AutomationRefusal(
                    "Native mapped executable region inspection was incomplete."
                )
            region = observed.region
            vnode = observed.vnode.info.stat
            next_cursor = int(region.address) + int(region.size)
            if (
                region.size <= 0
                or region.address < cursor
                or next_cursor <= cursor
                or next_cursor > (1 << 64) - 1
            ):
                raise AutomationRefusal("Native process region cursor did not advance.")
            if (
                int(vnode.device) == executable.device
                and int(vnode.inode) == executable.inode
                and int(vnode.byte_length) == executable.byte_length
                and int(vnode.uid) == executable.owner_uid
                and stat.S_IMODE(int(vnode.mode)) == executable.mode
                and stat.S_ISREG(int(vnode.mode))
                and int(region.max_protection) & self.VM_PROT_EXECUTE
            ):
                matches.append((
                    int(region.address),
                    int(region.size),
                    int(region.offset),
                    int(region.protection),
                    int(region.max_protection),
                    int(vnode.device),
                    int(vnode.inode),
                    int(vnode.byte_length),
                    int(vnode.uid),
                    stat.S_IMODE(int(vnode.mode)),
                ))
            cursor = next_cursor
        else:
            raise AutomationRefusal("Native process region enumeration exceeded its bound.")
        if not matches:
            raise AutomationRefusal(
                "Release-only observation lost the original executable VM mapping."
            )
        return tuple(matches)

    @staticmethod
    def _text_vnodes(pid: int) -> tuple[MappedExecutableVnode, ...]:
        try:
            completed = subprocess.run(
                [
                    "/usr/sbin/lsof", "-n", "-F0pfDikstn", "-a", "-p", str(pid),
                    "-d", "txt",
                ],
                stdin=subprocess.DEVNULL,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                check=False,
                close_fds=True,
                timeout=10,
                env={
                    "LC_ALL": "C",
                    "PATH": "/usr/bin:/bin:/usr/sbin:/sbin",
                },
            )
        except (OSError, subprocess.SubprocessError) as exc:
            raise AutomationRefusal("Cannot inspect the mapped executable text vnode.") from exc
        if (
            completed.returncode not in {0, 1}
            or len(completed.stdout) > 16 * 1024 * 1024
            or len(completed.stderr) > 1024 * 1024
        ):
            raise AutomationRefusal("lsof executable text-vnode inspection failed closed.")
        return _parse_text_vnodes(completed.stdout, pid=pid)

    @classmethod
    def _mapped_text_vnode(
        cls, pid: int, *, executable_real_path: str
    ) -> MappedExecutableVnode:
        matches = tuple(
            item for item in cls._text_vnodes(pid)
            if item.real_path == executable_real_path
        )
        if len(matches) != 1:
            raise AutomationRefusal(
                "lsof did not bind one exact mapped executable text vnode to DuckStation."
            )
        return matches[0]


def _read_observed_executable(
    path: str, *, mapped_vnode: MappedExecutableVnode
) -> FileIdentity:
    absolute = pathlib.Path(os.path.abspath(path))
    if str(absolute) != os.path.realpath(absolute):
        raise AutomationRefusal("Observed process executable resolves through a symlink.")
    if mapped_vnode.real_path != str(absolute):
        raise AutomationRefusal("Mapped executable text vnode path differs from proc_pidpath.")
    digest = hashlib.sha256()
    flags = os.O_RDONLY | getattr(os, "O_CLOEXEC", 0) | getattr(os, "O_NOFOLLOW", 0)
    try:
        descriptor = os.open(absolute, flags)
    except OSError as exc:
        raise AutomationRefusal("Observed process executable cannot be opened safely.") from exc
    try:
        before = os.fstat(descriptor)
        if (
            not stat.S_ISREG(before.st_mode)
            or before.st_nlink != 1
            or before.st_size <= 0
            or before.st_size > 1024 * 1024 * 1024
        ):
            raise AutomationRefusal("Observed process executable metadata is unsafe.")
        if (
            before.st_dev,
            before.st_ino,
            before.st_size,
            before.st_nlink,
        ) != (
            mapped_vnode.device,
            mapped_vnode.inode,
            mapped_vnode.byte_length,
            mapped_vnode.link_count,
        ):
            raise AutomationRefusal(
                "Executable pathname descriptor differs from the mapped text vnode."
            )
        while True:
            chunk = os.read(descriptor, 1024 * 1024)
            if not chunk:
                break
            digest.update(chunk)
        after = os.fstat(descriptor)
        try:
            path_after = os.stat(absolute, follow_symlinks=False)
        except OSError as exc:
            raise AutomationRefusal(
                "Observed process executable path changed while hashing."
            ) from exc
    finally:
        os.close(descriptor)
    stable_fields = (
        "st_dev", "st_ino", "st_mode", "st_size", "st_uid", "st_nlink",
        "st_mtime_ns", "st_ctime_ns",
    )
    if any(
        getattr(before, field) != getattr(after, field)
        or getattr(before, field) != getattr(path_after, field)
        for field in stable_fields
    ):
        raise AutomationRefusal("Observed process executable changed while hashing.")
    return FileIdentity(
        real_path=str(absolute),
        device=before.st_dev,
        inode=before.st_ino,
        byte_length=before.st_size,
        owner_uid=before.st_uid,
        mode=stat.S_IMODE(before.st_mode),
        link_count=before.st_nlink,
        sha256=digest.hexdigest(),
    )


def verify_exact_process(
    observed: ProcessIdentity,
    *,
    expected_pid: int,
    profile: VerifiedProfile,
) -> None:
    if observed.pid != expected_pid:
        raise AutomationRefusal("Observed process PID differs from the exact requested PID.")
    if (
        observed.effective_uid != profile.effective_uid
        or observed.real_uid != profile.effective_uid
        or observed.saved_uid != profile.effective_uid
    ):
        raise AutomationRefusal("DuckStation effective, real, or saved UID is not pinned.")
    if observed.start_seconds <= 0 or not 0 <= observed.start_microseconds < 1_000_000:
        raise AutomationRefusal("DuckStation start identity is invalid.")
    expected = profile.executable
    actual = observed.executable
    if (
        actual.real_path != expected.real_path
        or actual.device != expected.device
        or actual.inode != expected.inode
        or actual.byte_length != expected.byte_length
        or actual.owner_uid != expected.owner_uid
        or actual.mode != expected.mode
        or actual.link_count != expected.link_count
        or actual.sha256 != expected.sha256
    ):
        raise AutomationRefusal(
            "DuckStation path, inode, owner, mode, size, or hash is not pinned."
        )


class EventBackend(Protocol):
    def preflight_access(self) -> bool: ...
    def require_frontmost_pid(self, expected_pid: int) -> None: ...
    def post_key_down(self, pid: int, key_code: int) -> None: ...
    def force_compensating_key_down(
        self,
        pid: int,
        key_code: int,
        *,
        identity_validator: Callable[[], None],
    ) -> "ReleaseEnvironmentObservation": ...
    def force_key_up(
        self,
        pid: int,
        key_code: int,
        *,
        identity_validator: Callable[[], None],
    ) -> "ReleaseEnvironmentObservation": ...


@dataclasses.dataclass(frozen=True)
class ReleaseEnvironmentObservation:
    before_pid: int | None
    after_pid: int | None
    before_error: str | None
    after_error: str | None
    access_before: bool | None
    access_after: bool | None
    access_before_error: str | None
    access_after_error: str | None

    def __post_init__(self) -> None:
        for value in (self.before_pid, self.after_pid):
            if value is not None and (type(value) is not int or value <= 1):
                raise AutomationRefusal("Release environment observation has an invalid PID.")
        for value in (self.access_before, self.access_after):
            if value is not None and type(value) is not bool:
                raise AutomationRefusal(
                    "Release environment observation has an invalid Accessibility state."
                )

    def drifted_from(self, expected_pid: int) -> bool:
        return (
            self.before_error is not None
            or self.after_error is not None
            or self.before_pid != expected_pid
            or self.after_pid != expected_pid
            or self.access_before_error is not None
            or self.access_after_error is not None
            or self.access_before is not True
            or self.access_after is not True
        )

    def describe_drift(self, expected_pid: int) -> tuple[str, ...]:
        issues: list[str] = []
        if self.before_error is not None:
            issues.append(f"frontmost-before-error={self.before_error}")
        elif self.before_pid != expected_pid:
            issues.append(f"frontmost-before={self.before_pid}")
        if self.after_error is not None:
            issues.append(f"frontmost-after-error={self.after_error}")
        elif self.after_pid != expected_pid:
            issues.append(f"frontmost-after={self.after_pid}")
        if self.access_before_error is not None:
            issues.append(f"access-before-error={self.access_before_error}")
        elif self.access_before is not True:
            issues.append(f"access-before={self.access_before}")
        if self.access_after_error is not None:
            issues.append(f"access-after-error={self.access_after_error}")
        elif self.access_after is not True:
            issues.append(f"access-after={self.access_after}")
        return tuple(issues)


class _ProcessSerialNumber(ctypes.Structure):
    _fields_ = (
        ("high_long", ctypes.c_uint32),
        ("low_long", ctypes.c_uint32),
    )


class MacFrontmostProcessObserver:
    """Read the foreground application PID through the native Process Manager."""

    def __init__(self) -> None:
        if sys.platform != "darwin":
            raise AutomationRefusal("Frontmost application inspection is macOS-only.")
        self.application_services = ctypes.CDLL(
            "/System/Library/Frameworks/ApplicationServices.framework/"
            "ApplicationServices"
        )
        try:
            self.application_services.GetFrontProcess.argtypes = (
                ctypes.POINTER(_ProcessSerialNumber),
            )
            self.application_services.GetFrontProcess.restype = ctypes.c_int32
            self.application_services.GetProcessPID.argtypes = (
                ctypes.POINTER(_ProcessSerialNumber), ctypes.POINTER(ctypes.c_int32)
            )
            self.application_services.GetProcessPID.restype = ctypes.c_int32
        except AttributeError as exc:
            raise AutomationRefusal("Native frontmost-process APIs are unavailable.") from exc

    def frontmost_pid(self) -> int:
        serial = _ProcessSerialNumber()
        if self.application_services.GetFrontProcess(ctypes.byref(serial)) != 0:
            raise AutomationRefusal("Native frontmost application lookup failed.")
        pid = ctypes.c_int32()
        if self.application_services.GetProcessPID(
            ctypes.byref(serial), ctypes.byref(pid)
        ) != 0:
            raise AutomationRefusal("Native frontmost application PID lookup failed.")
        if pid.value <= 1:
            raise AutomationRefusal("Native frontmost application PID is invalid.")
        return int(pid.value)


class MacCGEventBackend:
    """CGEventPostToPid transport; it never requests a new permission grant."""

    def __init__(self) -> None:
        if sys.platform != "darwin":
            raise AutomationRefusal("PID-targeted keyboard event transport is macOS-only.")
        self.core_graphics = ctypes.CDLL(
            "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics"
        )
        self.core_foundation = ctypes.CDLL(
            "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation"
        )
        try:
            self.core_graphics.CGPreflightPostEventAccess.argtypes = ()
            self.core_graphics.CGPreflightPostEventAccess.restype = ctypes.c_bool
        except AttributeError as exc:
            raise AutomationRefusal("CGPreflightPostEventAccess is unavailable.") from exc
        self.core_graphics.CGEventCreateKeyboardEvent.argtypes = (
            ctypes.c_void_p, ctypes.c_uint16, ctypes.c_bool
        )
        self.core_graphics.CGEventCreateKeyboardEvent.restype = ctypes.c_void_p
        self.core_graphics.CGEventPostToPid.argtypes = (
            ctypes.c_int, ctypes.c_void_p
        )
        self.core_graphics.CGEventPostToPid.restype = None
        self.core_foundation.CFRelease.argtypes = (ctypes.c_void_p,)
        self.core_foundation.CFRelease.restype = None
        self.frontmost = MacFrontmostProcessObserver()

    def preflight_access(self) -> bool:
        return bool(self.core_graphics.CGPreflightPostEventAccess())

    def require_frontmost_pid(self, expected_pid: int) -> None:
        if type(expected_pid) is not int or expected_pid <= 1:
            raise AutomationRefusal("Expected frontmost application PID is invalid.")
        observed_pid = self.frontmost.frontmost_pid()
        if observed_pid != expected_pid:
            raise AutomationRefusal(
                "Exact DuckStation PID is not the frontmost application; no event was posted."
            )

    @staticmethod
    def _validate_post_arguments(pid: int, key_code: int) -> None:
        if type(pid) is not int or pid <= 1:
            raise AutomationRefusal("Refusing to post an event to an invalid PID.")
        if key_code not in {spec.key_code for spec in ACTION_SPECS.values()}:
            raise AutomationRefusal("Refusing a key code outside the fixed allowlist.")

    def _require_new_key_arguments(self, pid: int, key_code: int) -> None:
        if not self.preflight_access():
            raise AutomationRefusal("Accessibility post-event permission is not already granted.")
        self._validate_post_arguments(pid, key_code)

    def _create_keyboard_event(self, key_code: int, key_down: bool) -> int:
        event = self.core_graphics.CGEventCreateKeyboardEvent(
            None, key_code, bool(key_down)
        )
        if not event:
            raise AutomationRefusal("CoreGraphics could not create the pinned keyboard event.")
        return int(event)

    def post_key_down(self, pid: int, key_code: int) -> None:
        try:
            self._require_new_key_arguments(pid, key_code)
            event = self._create_keyboard_event(key_code, True)
        except BaseException as exc:
            raise KeyDownNotPostedRefusal(
                "Key-down was refused before an event could be posted."
            ) from exc
        try:
            # This is intentionally the last operation before the PID-targeted
            # key-down.  A prior foreground observation is never reused.
            try:
                self.require_frontmost_pid(pid)
            except BaseException as exc:
                raise KeyDownNotPostedRefusal(
                    "Key-down was not posted because DuckStation was not the frontmost PID."
                ) from exc
            self.core_graphics.CGEventPostToPid(pid, event)
        finally:
            self.core_foundation.CFRelease(event)

    def _observe_release_focus(self) -> tuple[int | None, str | None]:
        try:
            return self.frontmost.frontmost_pid(), None
        except BaseException as exc:
            return None, f"{type(exc).__name__}:{exc}"

    def _observe_release_access(self) -> tuple[bool | None, str | None]:
        try:
            return self.preflight_access(), None
        except BaseException as exc:
            return None, f"{type(exc).__name__}:{exc}"

    def force_key_up(
        self,
        pid: int,
        key_code: int,
        *,
        identity_validator: Callable[[], None],
    ) -> ReleaseEnvironmentObservation:
        """Post the owed release after the caller revalidates process identity.

        Foreground drift is observed immediately before and after the post but
        deliberately cannot veto an owed key-up.  The caller fails the run only
        after this method returns and the host key is no longer held.
        """

        if not callable(identity_validator):
            raise AutomationRefusal("Forced key-up identity validator is unavailable.")
        # Accessibility may have drifted since key-down.  It is observed below,
        # but must never veto the release already owed to this exact PID.
        self._validate_post_arguments(pid, key_code)
        event = self._create_keyboard_event(key_code, False)
        try:
            before_pid, before_error = self._observe_release_focus()
            access_before, access_before_error = self._observe_release_access()
            # This exact original-process check is the sole dynamic release
            # gate and is the last potentially-vetoing operation before post.
            identity_validator()
            self.core_graphics.CGEventPostToPid(pid, event)
            after_pid, after_error = self._observe_release_focus()
            access_after, access_after_error = self._observe_release_access()
        finally:
            self.core_foundation.CFRelease(event)
        return ReleaseEnvironmentObservation(
            before_pid=before_pid,
            after_pid=after_pid,
            before_error=before_error,
            after_error=after_error,
            access_before=access_before,
            access_after=access_after,
            access_before_error=access_before_error,
            access_after_error=access_after_error,
        )

    def force_compensating_key_down(
        self,
        pid: int,
        key_code: int,
        *,
        identity_validator: Callable[[], None],
    ) -> ReleaseEnvironmentObservation:
        """Post one identity-pinned cleanup toggle regardless of app focus."""

        try:
            if not callable(identity_validator):
                raise AutomationRefusal(
                    "Compensating key-down identity validator is unavailable."
                )
            self._require_new_key_arguments(pid, key_code)
            event = self._create_keyboard_event(key_code, True)
        except BaseException as exc:
            raise KeyDownNotPostedRefusal(
                "Compensating key-down was refused before an event could be posted."
            ) from exc
        try:
            before_pid, before_error = self._observe_release_focus()
            access_before, access_before_error = self._observe_release_access()
            try:
                identity_validator()
            except BaseException as exc:
                raise KeyDownNotPostedRefusal(
                    "Compensating key-down process identity was not pinned."
                ) from exc
            self.core_graphics.CGEventPostToPid(pid, event)
            after_pid, after_error = self._observe_release_focus()
            access_after, access_after_error = self._observe_release_access()
        finally:
            self.core_foundation.CFRelease(event)
        return ReleaseEnvironmentObservation(
            before_pid=before_pid,
            after_pid=after_pid,
            before_error=before_error,
            after_error=after_error,
            access_before=access_before,
            access_after=access_after,
            access_before_error=access_before_error,
            access_after_error=access_after_error,
        )


@dataclasses.dataclass(frozen=True)
class _PendingRelease:
    key_code: int
    identity_validator: Callable[[], None]
    pulse_ownership_validator: Callable[[], None]
    post_release_drift_observer: Callable[[], None]


@dataclasses.dataclass(frozen=True)
class TargetedKeyTransportStatus:
    """One atomic, read-only snapshot of accepted transport mutation state."""

    exclusive_claimed: bool
    raw_mutation_attempts: int
    posted_down_count: int
    possibly_held: tuple[tuple[int, KeyAction], ...]

    def __post_init__(self) -> None:
        if (
            type(self.exclusive_claimed) is not bool
            or type(self.raw_mutation_attempts) is not int
            or self.raw_mutation_attempts < 0
            or type(self.posted_down_count) is not int
            or self.posted_down_count < 0
            or type(self.possibly_held) is not tuple
            or any(
                type(token) is not tuple
                or len(token) != 2
                or type(token[0]) is not int
                or token[0] <= 1
                or type(token[1]) is not KeyAction
                for token in self.possibly_held
            )
        ):
            raise AutomationRefusal("Key-transport status is malformed.")


class _ExclusiveTargetedKeyTransportCapability:
    """Single-use mutation authority for one already-pristine key transport.

    This is an offline ownership primitive, not a production-ready security
    boundary.  It prevents a retained :class:`TargetedKeyTransport` alias from
    posting or releasing through that transport after a lifecycle coordinator
    claims it.  A caller that retained the original EventBackend can still
    bypass this Python object, so production promotion remains blocked on an
    outer owner that constructs and contains the backend itself.
    """

    __slots__ = ("__owner", "__token")

    def __init__(self, owner: "TargetedKeyTransport", token: object) -> None:
        self.__owner = owner
        self.__token = token

    def require_target(self, pid: int) -> None:
        self.__owner._exclusive_require_target(self.__token, pid)

    def tap(
        self,
        pid: int,
        action: KeyAction,
        *,
        release_identity_validator: Callable[[], None],
        post_release_drift_observer: Callable[[], None],
        pre_key_down_validator: Callable[[], None],
    ) -> None:
        self.__owner._exclusive_tap(
            self.__token,
            pid,
            action,
            release_identity_validator=release_identity_validator,
            post_release_drift_observer=post_release_drift_observer,
            pre_key_down_validator=pre_key_down_validator,
        )

    def release_all(self) -> tuple[str, ...]:
        return self.__owner._exclusive_release_all(self.__token)

    @property
    def possibly_held(self) -> tuple[tuple[int, KeyAction], ...]:
        return self.__owner._exclusive_possibly_held(self.__token)


class TargetedKeyTransport:
    def __init__(
        self,
        backend: EventBackend,
        *,
        sleeper: Callable[[float], None] = time.sleep,
    ) -> None:
        self._backend = backend
        self._sleeper = sleeper
        self._possibly_held: dict[tuple[int, KeyAction], _PendingRelease] = {}
        self._posted_down_count = 0
        self._operation_lock = threading.RLock()
        self._exclusive_token: object | None = None
        self._raw_mutation_attempts = 0

    def _begin_raw_mutation_attempt(self) -> None:
        self._require_direct_access()
        self._raw_mutation_attempts += 1

    def _require_direct_access(self) -> None:
        if self._exclusive_token is not None:
            raise AutomationRefusal(
                "Direct key-transport mutation is forbidden after exclusive claim."
            )

    def _require_exclusive_access(self, token: object) -> None:
        if self._exclusive_token is None or self._exclusive_token is not token:
            raise AutomationRefusal(
                "Exclusive key-transport capability is absent or stale."
            )

    def claim_exclusive_lifecycle(self) -> _ExclusiveTargetedKeyTransportCapability:
        """Consume the pristine transport's only lifecycle mutation capability."""

        with self._operation_lock:
            if type(self) is not TargetedKeyTransport:
                raise AutomationRefusal(
                    "Exclusive key transport requires the exact reviewed type."
                )
            if self._exclusive_token is not None:
                raise AutomationRefusal(
                    "Key transport already has an exclusive lifecycle owner."
                )
            if (
                self._raw_mutation_attempts != 0
                or self._possibly_held
                or self._posted_down_count != 0
            ):
                raise AutomationRefusal(
                    "Exclusive key transport must be claimed before any pulse work."
                )
            token = object()
            capability = _ExclusiveTargetedKeyTransportCapability(self, token)
            self._exclusive_token = token
            return capability

    def require_target(self, pid: int) -> None:
        with self._operation_lock:
            self._begin_raw_mutation_attempt()
            self._require_target_owned(pid)

    def _exclusive_require_target(self, token: object, pid: int) -> None:
        with self._operation_lock:
            self._require_exclusive_access(token)
            self._require_target_owned(pid)

    def _require_target_owned(self, pid: int) -> None:
        if not self._backend.preflight_access():
            raise AutomationRefusal("Accessibility post-event permission is not already granted.")
        self._backend.require_frontmost_pid(pid)

    def tap(
        self,
        pid: int,
        action: KeyAction,
        *,
        release_identity_validator: Callable[[], None],
        post_release_drift_observer: Callable[[], None],
        pre_key_down_validator: Callable[[], None],
    ) -> None:
        with self._operation_lock:
            self._begin_raw_mutation_attempt()
            self._tap_owned(
                pid,
                action,
                release_identity_validator=release_identity_validator,
                post_release_drift_observer=post_release_drift_observer,
                pre_key_down_validator=pre_key_down_validator,
            )

    def _exclusive_tap(
        self,
        token: object,
        pid: int,
        action: KeyAction,
        *,
        release_identity_validator: Callable[[], None],
        post_release_drift_observer: Callable[[], None],
        pre_key_down_validator: Callable[[], None],
    ) -> None:
        with self._operation_lock:
            self._require_exclusive_access(token)
            self._tap_owned(
                pid,
                action,
                release_identity_validator=release_identity_validator,
                post_release_drift_observer=post_release_drift_observer,
                pre_key_down_validator=pre_key_down_validator,
            )

    def _tap_owned(
        self,
        pid: int,
        action: KeyAction,
        *,
        release_identity_validator: Callable[[], None],
        post_release_drift_observer: Callable[[], None],
        pre_key_down_validator: Callable[[], None],
    ) -> None:
        if type(action) is not KeyAction:
            raise AutomationRefusal("Transport action is outside the fixed enum allowlist.")
        if not callable(release_identity_validator):
            raise AutomationRefusal("Mandatory release identity validator is unavailable.")
        if not callable(post_release_drift_observer):
            raise AutomationRefusal("Post-release drift observer is unavailable.")
        if not callable(pre_key_down_validator):
            raise AutomationRefusal("Pre-key-down ownership validator is unavailable.")
        spec = ACTION_SPECS[action]
        token = (pid, action)
        # Install the cleanup obligation before crossing the native post call.
        # A proven pre-post refusal removes it below; an asynchronous exception
        # at or after CGEventPostToPid conservatively retains it for release.
        self._possibly_held[token] = _PendingRelease(
            spec.key_code,
            release_identity_validator,
            pre_key_down_validator,
            post_release_drift_observer,
        )
        primary: BaseException | None = None
        self._posted_down_count += 1
        try:
            try:
                pre_key_down_validator()
            except BaseException as exc:
                raise KeyDownNotPostedRefusal(
                    "Key-down ownership was refused before an event could be posted."
                ) from exc
            self._backend.post_key_down(pid, spec.key_code)
            self._sleeper(KEY_PULSE_SECONDS)
        except KeyDownNotPostedRefusal as exc:
            self._posted_down_count -= 1
            self._possibly_held.pop(token, None)
            primary = exc
        except BaseException as exc:
            primary = exc
        release_error: BaseException | None = None
        if token in self._possibly_held:
            try:
                self._release_one(token)
            except BaseException as exc:
                release_error = exc
        if release_error is not None:
            if primary is not None:
                if isinstance(release_error, ReleasedWithEnvironmentDrift):
                    raise AutomationRefusal(
                        "Key-down path failed; the key was released under focus "
                        "or runtime-environment drift and the run is refused."
                    ) from release_error
                raise AutomationRefusal(
                    "Key-down path failed and the mandatory key-up also failed."
                ) from release_error
            if isinstance(release_error, ReleasedWithEnvironmentDrift):
                raise release_error
            raise AutomationRefusal(
                "Mandatory key-up failed; input state is uncertain."
            ) from release_error
        if primary is not None:
            if isinstance(primary, AutomationRefusal):
                raise primary
            raise AutomationRefusal("Targeted key pulse failed before release.") from primary

    def compensating_toggle(
        self,
        pid: int,
        action: KeyAction,
        *,
        identity_validator: Callable[[], None],
        post_release_drift_observer: Callable[[], None],
        pre_key_down_validator: Callable[[], None],
    ) -> bool:
        with self._operation_lock:
            self._begin_raw_mutation_attempt()
            return self._compensating_toggle_owned(
                pid,
                action,
                identity_validator=identity_validator,
                post_release_drift_observer=post_release_drift_observer,
                pre_key_down_validator=pre_key_down_validator,
            )

    def _compensating_toggle_owned(
        self,
        pid: int,
        action: KeyAction,
        *,
        identity_validator: Callable[[], None],
        post_release_drift_observer: Callable[[], None],
        pre_key_down_validator: Callable[[], None],
    ) -> bool:
        """Force one exact logical untoggle and release its physical host key.

        Returns whether focus drift was observed.  Drift never blocks cleanup,
        but the caller must include it in the terminal refusal after confirming
        the pad bit returned to RELEASED.
        """

        spec = ACTION_SPECS[action]
        if (
            spec.pad_bit is None
            or not callable(identity_validator)
            or not callable(post_release_drift_observer)
            or not callable(pre_key_down_validator)
        ):
            raise AutomationRefusal("Compensating untoggle request is invalid.")
        token = (pid, action)
        if token in self._possibly_held:
            raise AutomationRefusal("Cannot untoggle while the host key release is uncertain.")
        self._possibly_held[token] = _PendingRelease(
            spec.key_code,
            identity_validator,
            pre_key_down_validator,
            post_release_drift_observer,
        )
        self._posted_down_count += 1
        try:
            try:
                pre_key_down_validator()
            except BaseException as exc:
                raise KeyDownNotPostedRefusal(
                    "Compensating key-down ownership was refused before posting."
                ) from exc
            down_focus = self._backend.force_compensating_key_down(
                pid,
                spec.key_code,
                identity_validator=identity_validator,
            )
        except KeyDownNotPostedRefusal:
            self._posted_down_count -= 1
            self._possibly_held.pop(token, None)
            raise
        release_drift = False
        try:
            self._release_one(token)
        except ReleasedWithEnvironmentDrift:
            release_drift = True
        return down_focus.drifted_from(pid) or release_drift

    def _release_one(self, token: tuple[int, KeyAction]) -> None:
        pending = self._possibly_held[token]
        pid, action = token
        ownership_drift: BaseException | None = None
        try:
            # The shared manual-coordinator inode and its staging-directory
            # anchor remain open for the whole pulse.  Revalidate both as close
            # as possible to key-up, but never let namespace drift veto an
            # already-owed release to the still-pinned process.
            pending.pulse_ownership_validator()
        except BaseException as exc:
            ownership_drift = exc
        # The backend invokes this validator immediately before its forced
        # release.  If PID identity changed, posting to a recycled PID is less
        # safe than retaining the explicit uncertain-state refusal.
        environment = self._backend.force_key_up(
            pid,
            pending.key_code,
            identity_validator=pending.identity_validator,
        )
        # The native post returned: this key is no longer conservatively held.
        # No focus, Accessibility, or runtime-attestation drift reported after
        # this point may recreate the physical key-up obligation.
        self._possibly_held.pop(token, None)
        drift: list[str] = []
        if ownership_drift is not None:
            drift.append(
                "pre-key-up-ownership="
                f"{type(ownership_drift).__name__}:{ownership_drift}"
            )
        drift.extend(environment.describe_drift(pid))
        try:
            result = pending.post_release_drift_observer()
            if result is not None:
                raise AutomationRefusal(
                    "Post-release drift observer returned an unexpected value."
                )
        except BaseException as exc:
            drift.append(f"post-release={type(exc).__name__}:{exc}")
        if drift:
            raise ReleasedWithEnvironmentDrift(
                f"Environment drifted during mandatory {action.value} key-up "
                f"({';'.join(drift)}); the key was released and the run is refused."
            )

    def release_all(self) -> tuple[str, ...]:
        with self._operation_lock:
            self._begin_raw_mutation_attempt()
            return self._release_all_owned()

    def _exclusive_release_all(self, token: object) -> tuple[str, ...]:
        with self._operation_lock:
            self._require_exclusive_access(token)
            return self._release_all_owned()

    def _release_all_owned(self) -> tuple[str, ...]:
        errors: list[str] = []
        for token, _pending in sorted(
            tuple(self._possibly_held.items()), key=lambda item: (item[0][0], item[0][1].value)
        ):
            pid, action = token
            try:
                self._release_one(token)
            except BaseException as exc:
                errors.append(f"{pid}:{action.value}:{type(exc).__name__}")
        return tuple(errors)

    @property
    def possibly_held(self) -> tuple[tuple[int, KeyAction], ...]:
        return self.status().possibly_held

    def _exclusive_possibly_held(
        self, token: object
    ) -> tuple[tuple[int, KeyAction], ...]:
        with self._operation_lock:
            self._require_exclusive_access(token)
            return tuple(
                sorted(self._possibly_held, key=lambda item: (item[0], item[1].value))
            )

    @property
    def posted_down_count(self) -> int:
        return self.status().posted_down_count

    @property
    def exclusive_claimed(self) -> bool:
        return self.status().exclusive_claimed

    def status(self) -> TargetedKeyTransportStatus:
        with self._operation_lock:
            return TargetedKeyTransportStatus(
                self._exclusive_token is not None,
                self._raw_mutation_attempts,
                self._posted_down_count,
                tuple(
                    sorted(
                        self._possibly_held,
                        key=lambda item: (item[0], item[1].value),
                    )
                ),
            )


class PadWordReader(Protocol):
    """Read one active-low PS1 pad word without sending any GDB control packet."""

    def read_pad_word(self, process: ProcessIdentity) -> int: ...


@dataclasses.dataclass(frozen=True)
class GdbListenerIdentity:
    """One exact IPv4 loopback lsof listener record owned by DuckStation."""

    pid: int
    descriptor: int
    access: str
    socket_type: str
    device_token: str
    protocol: str
    endpoint: str
    tcp_state: str
    receive_queue: int
    send_queue: int

    def __post_init__(self) -> None:
        if (
            type(self.pid) is not int
            or self.pid <= 1
            or type(self.descriptor) is not int
            or self.descriptor < 0
            or self.access != "u"
            or self.socket_type != "IPv4"
            or re.fullmatch(r"0x[0-9a-f]+", self.device_token, re.ASCII) is None
            or self.protocol != "TCP"
            or self.endpoint != f"{GDB_HOST}:{GDB_PORT}"
            or self.tcp_state != "LISTEN"
            or type(self.receive_queue) is not int
            or self.receive_queue != 0
            or type(self.send_queue) is not int
            or self.send_queue != 0
        ):
            raise AutomationRefusal("DuckStation GDB listener identity is malformed.")


@dataclasses.dataclass(frozen=True)
class GdbClientEndpointIdentity:
    """The exact local endpoint created by this automation process."""

    pid: int
    descriptor: int
    address: str
    port: int

    def __post_init__(self) -> None:
        if (
            type(self.pid) is not int
            or self.pid <= 1
            or self.pid > 2_147_483_647
            or type(self.descriptor) is not int
            or self.descriptor < 0
            or self.descriptor > 2_147_483_647
            or self.address != GDB_HOST
            or type(self.port) is not int
            or not 1 <= self.port <= 65_535
            or self.port == GDB_PORT
        ):
            raise AutomationRefusal("DuckStation GDB client endpoint is malformed.")


@dataclasses.dataclass(frozen=True)
class GdbObservedConnectionSet:
    """One lsof observation of the listener and optional sole client pair.

    This excludes stable or accidental competing clients at every observation.
    Like any userspace snapshot, it is not an OS-enforced defence against a
    hostile client that connects, acts, and disconnects between observations.
    The production coordinator remains disabled while that residual exists.
    """

    listener: GdbListenerIdentity
    client_endpoint: GdbClientEndpointIdentity | None
    server_descriptor: int | None
    server_device_token: str | None
    client_descriptor: int | None
    client_device_token: str | None

    def __post_init__(self) -> None:
        if type(self.listener) is not GdbListenerIdentity:
            raise AutomationRefusal("DuckStation GDB connection set listener is malformed.")
        optional_values = (
            self.server_descriptor,
            self.server_device_token,
            self.client_descriptor,
            self.client_device_token,
        )
        if self.client_endpoint is None:
            if any(value is not None for value in optional_values):
                raise AutomationRefusal(
                    "Unconnected DuckStation GDB set contains client socket fields."
                )
            return
        if (
            type(self.client_endpoint) is not GdbClientEndpointIdentity
            or self.client_endpoint.pid == self.listener.pid
            or type(self.server_descriptor) is not int
            or self.server_descriptor < 0
            or self.server_descriptor > 2_147_483_647
            or self.server_descriptor == self.listener.descriptor
            or type(self.client_descriptor) is not int
            or self.client_descriptor < 0
            or self.client_descriptor > 2_147_483_647
            or self.client_descriptor != self.client_endpoint.descriptor
            or re.fullmatch(
                r"0x[0-9a-f]+", self.server_device_token or "", re.ASCII
            )
            is None
            or re.fullmatch(
                r"0x[0-9a-f]+", self.client_device_token or "", re.ASCII
            )
            is None
            or self.server_device_token == self.listener.device_token
            or self.client_device_token == self.listener.device_token
            or self.server_device_token == self.client_device_token
        ):
            raise AutomationRefusal("DuckStation GDB established socket pair is malformed.")


def _parse_gdb_connection_set_lsof(
    raw: bytes,
    duckstation_pid: int,
    expected_client: GdbClientEndpointIdentity | None,
) -> GdbObservedConnectionSet:
    """Parse a global macOS lsof snapshot and require the exact whole set."""

    if (
        type(raw) is not bytes
        or not raw
        or len(raw) > 64 * 1024
        or not raw.endswith(b"\x00\n")
        or raw.startswith(b"\xef\xbb\xbf")
        or type(duckstation_pid) is not int
        or duckstation_pid <= 1
        or duckstation_pid > 2_147_483_647
        or (
            expected_client is not None
            and type(expected_client) is not GdbClientEndpointIdentity
        )
    ):
        raise AutomationRefusal("GDB connection-set lsof framing is invalid.")
    try:
        decoded = raw.decode("utf-8", "strict")
    except UnicodeDecodeError as exc:
        raise AutomationRefusal(
            "GDB connection-set lsof output is not strict UTF-8."
        ) from exc
    records = decoded.split("\x00\n")
    if records[-1] != "":
        raise AutomationRefusal("GDB connection-set lsof output is unterminated.")

    groups: dict[int, list[tuple[str, ...]]] = {}
    current_pid: int | None = None
    for record in records[:-1]:
        fields = tuple(record.split("\x00"))
        if any(
            not field or "\x00" in field or "\r" in field or "\n" in field
            for field in fields
        ):
            raise AutomationRefusal(
                "GDB connection-set lsof fields contain control text."
            )
        if len(fields) == 1 and re.fullmatch(
            r"p[1-9][0-9]{0,9}", fields[0], re.ASCII
        ):
            current_pid = int(fields[0][1:], 10)
            if current_pid > 2_147_483_647 or current_pid in groups:
                raise AutomationRefusal(
                    "GDB connection-set lsof process record is duplicated or oversized."
                )
            groups[current_pid] = []
            continue
        if current_pid is None or len(fields) != 9:
            raise AutomationRefusal(
                "GDB connection-set lsof socket record is misplaced or incomplete."
            )
        expected_prefixes = ("f", "a", "t", "d", "P", "n", "T", "T", "T")
        if tuple(field[:1] for field in fields) != expected_prefixes:
            raise AutomationRefusal(
                "GDB connection-set lsof fields are reordered, duplicated, or unknown."
            )
        descriptor_text = fields[0][1:]
        if (
            re.fullmatch(r"0|[1-9][0-9]{0,9}", descriptor_text, re.ASCII) is None
            or int(descriptor_text, 10) > 2_147_483_647
            or fields[1] != "au"
            or fields[2] != "tIPv4"
            or re.fullmatch(r"d0x[0-9a-f]+", fields[3], re.ASCII) is None
            or fields[4] != "PTCP"
            or fields[6] not in {"TST=LISTEN", "TST=ESTABLISHED"}
            or re.fullmatch(r"TQR=(0|[1-9][0-9]{0,9})", fields[7], re.ASCII)
            is None
            or re.fullmatch(r"TQS=(0|[1-9][0-9]{0,9})", fields[8], re.ASCII)
            is None
            or int(fields[7][4:], 10) > 2_147_483_647
            or int(fields[8][4:], 10) > 2_147_483_647
        ):
            raise AutomationRefusal(
                "GDB connection-set lsof socket identity is not pinned."
            )
        groups[current_pid].append(fields)

    expected_pids = {duckstation_pid}
    if expected_client is not None:
        expected_pids.add(expected_client.pid)
    if set(groups) != expected_pids or any(not sockets for sockets in groups.values()):
        if expected_client is None and duckstation_pid in groups:
            raise AutomationRefusal(
                "A GDB client was already established or foreign before ownership."
            )
        raise AutomationRefusal(
            "GDB connection-set lsof contains a missing or foreign process."
        )

    duckstation_sockets = groups[duckstation_pid]
    listener_sockets = [
        fields
        for fields in duckstation_sockets
        if fields[5] == f"n{GDB_HOST}:{GDB_PORT}"
        and fields[6] == "TST=LISTEN"
    ]
    if len(listener_sockets) != 1:
        raise AutomationRefusal(
            "GDB connection-set lsof does not contain one exact listener."
        )
    listener_fields = listener_sockets[0]
    if listener_fields[7:] != ("TQR=0", "TQS=0"):
        raise AutomationRefusal("DuckStation GDB listener queues are not empty.")
    listener = GdbListenerIdentity(
        pid=duckstation_pid,
        descriptor=int(listener_fields[0][1:], 10),
        access="u",
        socket_type="IPv4",
        device_token=listener_fields[3][1:],
        protocol="TCP",
        endpoint=f"{GDB_HOST}:{GDB_PORT}",
        tcp_state="LISTEN",
        receive_queue=0,
        send_queue=0,
    )
    if expected_client is None:
        if len(duckstation_sockets) != 1:
            raise AutomationRefusal(
                "A GDB client was already established before ownership."
            )
        return GdbObservedConnectionSet(listener, None, None, None, None, None)

    server_endpoint = (
        f"n{GDB_HOST}:{GDB_PORT}->{expected_client.address}:{expected_client.port}"
    )
    client_endpoint = (
        f"n{expected_client.address}:{expected_client.port}->{GDB_HOST}:{GDB_PORT}"
    )
    server_sockets = [
        fields
        for fields in duckstation_sockets
        if fields[5] == server_endpoint and fields[6] == "TST=ESTABLISHED"
    ]
    client_sockets = [
        fields
        for fields in groups[expected_client.pid]
        if fields[5] == client_endpoint and fields[6] == "TST=ESTABLISHED"
    ]
    if (
        len(duckstation_sockets) != 2
        or len(groups[expected_client.pid]) != 1
        or len(server_sockets) != 1
        or len(client_sockets) != 1
        or int(client_sockets[0][0][1:], 10) != expected_client.descriptor
    ):
        raise AutomationRefusal(
            "GDB connection-set lsof is not one exact reversed socket pair."
        )
    server = server_sockets[0]
    client = client_sockets[0]
    return GdbObservedConnectionSet(
        listener,
        expected_client,
        int(server[0][1:], 10),
        server[3][1:],
        int(client[0][1:], 10),
        client[3][1:],
    )


def _parse_gdb_listener_lsof(raw: bytes) -> GdbListenerIdentity:
    """Parse the empirically pinned macOS ``lsof -F0`` listener schema.

    The query is intentionally global rather than PID-filtered.  Requiring one
    process record and one socket record therefore rejects extra listeners as
    well as a listener owned by the wrong process.
    """

    if (
        type(raw) is not bytes
        or not raw
        or len(raw) > 64 * 1024
        or not raw.endswith(b"\x00\n")
        or raw.startswith(b"\xef\xbb\xbf")
    ):
        raise AutomationRefusal("GDB listener lsof output framing is invalid.")
    try:
        decoded = raw.decode("utf-8", "strict")
    except UnicodeDecodeError as exc:
        raise AutomationRefusal(
            "GDB listener lsof output is not strict UTF-8."
        ) from exc
    records = decoded.split("\x00\n")
    if records[-1] != "" or len(records) != 3:
        raise AutomationRefusal(
            "lsof did not return exactly one GDB process and listener record."
        )
    process_fields = records[0].split("\x00")
    listener_fields = records[1].split("\x00")
    if (
        len(process_fields) != 1
        or re.fullmatch(r"p[1-9][0-9]{0,9}", process_fields[0], re.ASCII) is None
    ):
        raise AutomationRefusal("GDB listener lsof process record is malformed.")
    if len(listener_fields) != 9:
        raise AutomationRefusal("GDB listener lsof socket record is incomplete.")
    expected_prefixes = ("f", "a", "t", "d", "P", "n", "T", "T", "T")
    if tuple(field[:1] for field in listener_fields) != expected_prefixes:
        raise AutomationRefusal(
            "GDB listener lsof fields are reordered, duplicated, or unknown."
        )
    if any(
        not field
        or "\x00" in field
        or "\r" in field
        or "\n" in field
        for field in process_fields + listener_fields
    ):
        raise AutomationRefusal("GDB listener lsof fields contain control text.")
    pid_text = process_fields[0][1:]
    descriptor_text = listener_fields[0][1:]
    if (
        re.fullmatch(r"0|[1-9][0-9]{0,9}", descriptor_text, re.ASCII) is None
        or listener_fields[1] != "au"
        or listener_fields[2] != "tIPv4"
        or re.fullmatch(r"d0x[0-9a-f]+", listener_fields[3], re.ASCII) is None
        or listener_fields[4] != "PTCP"
        or listener_fields[5] != f"n{GDB_HOST}:{GDB_PORT}"
        or listener_fields[6:] != ["TST=LISTEN", "TQR=0", "TQS=0"]
    ):
        raise AutomationRefusal("GDB listener lsof socket identity is not pinned.")
    pid = int(pid_text, 10)
    descriptor = int(descriptor_text, 10)
    if pid > 2_147_483_647 or descriptor > 2_147_483_647:
        raise AutomationRefusal("GDB listener PID or descriptor exceeds its native bound.")
    return GdbListenerIdentity(
        pid=pid,
        descriptor=descriptor,
        access="u",
        socket_type="IPv4",
        device_token=listener_fields[3][1:],
        protocol="TCP",
        endpoint=f"{GDB_HOST}:{GDB_PORT}",
        tcp_state="LISTEN",
        receive_queue=0,
        send_queue=0,
    )


class GdbEndpointAttestor(Protocol):
    def observe(self, process: ProcessIdentity) -> GdbListenerIdentity: ...

    def observe_connection_set(
        self,
        process: ProcessIdentity,
        expected_client: GdbClientEndpointIdentity | None,
    ) -> GdbObservedConnectionSet: ...


class MacGdbEndpointAttestor:
    """Bind the fixed endpoint to its listener and observed whole client set."""

    def __init__(
        self,
        runner: Callable[
            [Sequence[str]], subprocess.CompletedProcess[bytes]
        ] | None = None,
    ) -> None:
        if runner is None:
            if sys.platform != "darwin":
                raise AutomationRefusal("Native GDB listener inspection is macOS-only.")
            runner = self._run_lsof
        self._runner = runner

    @staticmethod
    def _run_lsof(command: Sequence[str]) -> subprocess.CompletedProcess[bytes]:
        return subprocess.run(
            list(command),
            stdin=subprocess.DEVNULL,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            check=False,
            close_fds=True,
            timeout=10,
            env={
                "LC_ALL": "C",
                "PATH": "/usr/bin:/bin:/usr/sbin:/sbin",
            },
        )

    def observe(self, process: ProcessIdentity) -> GdbListenerIdentity:
        if type(process) is not ProcessIdentity or process.pid <= 1:
            raise AutomationRefusal("GDB listener process binding is invalid.")
        try:
            completed = self._runner(GDB_LSOF_COMMAND)
        except (OSError, subprocess.SubprocessError) as exc:
            raise AutomationRefusal("Cannot inspect the DuckStation GDB listener.") from exc
        if (
            type(completed.returncode) is not int
            or completed.returncode != 0
            or type(completed.stdout) is not bytes
            or type(completed.stderr) is not bytes
            or completed.stderr
        ):
            raise AutomationRefusal("DuckStation GDB listener inspection failed closed.")
        listener = _parse_gdb_listener_lsof(completed.stdout)
        if listener.pid != process.pid:
            raise AutomationRefusal(
                "The fixed GDB listener is not owned by the exact DuckStation process."
            )
        return listener

    def observe_connection_set(
        self,
        process: ProcessIdentity,
        expected_client: GdbClientEndpointIdentity | None,
    ) -> GdbObservedConnectionSet:
        if type(process) is not ProcessIdentity or process.pid <= 1:
            raise AutomationRefusal("GDB connection-set process binding is invalid.")
        if (
            expected_client is not None
            and type(expected_client) is not GdbClientEndpointIdentity
        ):
            raise AutomationRefusal("GDB expected client endpoint is malformed.")
        try:
            completed = self._runner(GDB_CONNECTION_SET_LSOF_COMMAND)
        except (OSError, subprocess.SubprocessError) as exc:
            raise AutomationRefusal(
                "Cannot inspect the DuckStation GDB connection set."
            ) from exc
        if (
            type(completed.returncode) is not int
            or completed.returncode != 0
            or type(completed.stdout) is not bytes
            or type(completed.stderr) is not bytes
            or completed.stderr
        ):
            raise AutomationRefusal(
                "DuckStation GDB connection-set inspection failed closed."
            )
        observed = _parse_gdb_connection_set_lsof(
            completed.stdout, process.pid, expected_client
        )
        if observed.listener.pid != process.pid:
            raise AutomationRefusal(
                "The GDB connection set is not owned by the exact DuckStation process."
            )
        return observed


class _GdbSocket(Protocol):
    def settimeout(self, value: float | None) -> None: ...
    def sendall(self, data: bytes) -> None: ...
    def recv(self, maximum_bytes: int) -> bytes: ...
    def getpeername(self) -> Any: ...
    def getsockname(self) -> Any: ...
    def fileno(self) -> int: ...
    def close(self) -> None: ...


class _GdbReaderState(enum.Enum):
    NEW = enum.auto()
    CONNECTING = enum.auto()
    CONNECTED = enum.auto()
    POISONED = enum.auto()
    CLOSED = enum.auto()


@dataclasses.dataclass(frozen=True)
class _GdbTransportStatus:
    """Side-effect-free lifecycle snapshot for the internal RSP transport."""

    state: str
    bound_process: ProcessIdentity | None
    bound_listener: GdbListenerIdentity | None
    client_endpoint: GdbClientEndpointIdentity | None
    connection_attempts: int

    def __post_init__(self) -> None:
        if (
            self.state not in {state.name for state in _GdbReaderState}
            or (
                self.bound_process is not None
                and type(self.bound_process) is not ProcessIdentity
            )
            or (
                self.bound_listener is not None
                and type(self.bound_listener) is not GdbListenerIdentity
            )
            or (
                self.client_endpoint is not None
                and type(self.client_endpoint) is not GdbClientEndpointIdentity
            )
            or type(self.connection_attempts) is not int
            or not 0 <= self.connection_attempts <= 2_147_483_647
        ):
            raise AutomationRefusal("DuckStation GDB transport status is malformed.")


class _PersistentDuckStationGdbTransport:
    """Internal one-connection RSP transport with a fixed packet surface.

    The public session below is the sole owner of the exact continue packet.
    Keeping this transport private prevents the read-only confirmation policy
    from acquiring a generic GDB control surface.
    """

    def __init__(
        self,
        observer: ProcessObserver,
        endpoint_attestor: GdbEndpointAttestor,
        connector: Callable[[tuple[str, int], float], _GdbSocket] | None = None,
    ) -> None:
        if observer is None or endpoint_attestor is None:
            raise AutomationRefusal("GDB reader dependencies are unavailable.")
        if connector is None:
            connector = socket.create_connection
        self._observer = observer
        self._endpoint_attestor = endpoint_attestor
        self._connector = connector
        self._state = _GdbReaderState.NEW
        self._socket: _GdbSocket | None = None
        self._process: ProcessIdentity | None = None
        self._listener: GdbListenerIdentity | None = None
        self._client_endpoint: GdbClientEndpointIdentity | None = None
        self._connection_attempts = 0
        self._state_guard = threading.Lock()
        self._operation_lock = threading.Lock()

    def status(self) -> _GdbTransportStatus:
        """Return state only; never inspect lsof, open a socket, or send RSP."""

        with self._state_guard:
            return _GdbTransportStatus(
                self._state.name,
                self._process,
                self._listener,
                self._client_endpoint,
                self._connection_attempts,
            )

    def require_existing_connection(self, process: ProcessIdentity) -> None:
        """Require one already-open exact connection without creating one."""

        if type(process) is not ProcessIdentity:
            raise AutomationRefusal(
                "Existing DuckStation GDB process identity is malformed."
            )
        with self._state_guard:
            if (
                self._state is not _GdbReaderState.CONNECTED
                or self._socket is None
                or self._listener is None
                or self._client_endpoint is None
                or self._process != process
                or self._connection_attempts != 1
            ):
                raise AutomationRefusal(
                    "DuckStation GDB reader is not one exact existing connection."
                )

    def connect_once(self, process: ProcessIdentity) -> None:
        """Create the sole socket without sending any RSP bytes."""

        if not self._operation_lock.acquire(blocking=False):
            raise AutomationRefusal("Concurrent DuckStation GDB operations are forbidden.")
        try:
            try:
                if type(process) is not ProcessIdentity:
                    raise AutomationRefusal(
                        "DuckStation GDB process identity is malformed."
                    )
                self._connect(process)
            except BaseException as exc:
                self._poison_and_raise(exc)
        finally:
            self._operation_lock.release()

    def invalidate(self) -> None:
        """Poison and close immediately, including during a concurrent operation."""

        with self._state_guard:
            if self._state is _GdbReaderState.CLOSED:
                return
            self._state = _GdbReaderState.POISONED
            active = self._socket
            self._socket = None
        if active is not None:
            try:
                active.close()
            except BaseException:
                pass

    def read_pad_word(self, process: ProcessIdentity) -> int:
        return self._read_pad_word(process, strict_running=False)

    def read_pad_word_strict_running(self, process: ProcessIdentity) -> int:
        return self._read_pad_word(process, strict_running=True)

    def _read_pad_word(
        self, process: ProcessIdentity, *, strict_running: bool
    ) -> int:
        if not self._operation_lock.acquire(blocking=False):
            raise AutomationRefusal("Concurrent DuckStation GDB reads are forbidden.")
        try:
            try:
                raw = self._read_fixed_packet(
                    process,
                    GDB_PAD_READ_PACKET,
                    strict_ack=strict_running,
                    allow_stop_notifications=not strict_running,
                )
                if raw[0] != 0 or raw[1] != 0x41:
                    raise AutomationRefusal(
                        "DuckStation pad reply is not the pinned digital-pad frame."
                    )
                return raw[2] | (raw[3] << 8)
            except BaseException as exc:
                self._poison_and_raise(exc)
        finally:
            self._operation_lock.release()

    def read_vblank_tick(self, process: ProcessIdentity) -> int:
        return self._read_vblank_tick(process, strict_running=False)

    def read_vblank_tick_strict_running(self, process: ProcessIdentity) -> int:
        return self._read_vblank_tick(process, strict_running=True)

    def _read_vblank_tick(
        self, process: ProcessIdentity, *, strict_running: bool
    ) -> int:
        if not self._operation_lock.acquire(blocking=False):
            raise AutomationRefusal("Concurrent DuckStation GDB reads are forbidden.")
        try:
            try:
                raw = self._read_fixed_packet(
                    process,
                    GDB_VBLANK_READ_PACKET,
                    strict_ack=strict_running,
                    allow_stop_notifications=not strict_running,
                )
                return int.from_bytes(raw, byteorder="little", signed=False)
            except BaseException as exc:
                self._poison_and_raise(exc)
        finally:
            self._operation_lock.release()

    def continue_from_connection_pause(
        self,
        process: ProcessIdentity,
        *,
        before_send: Callable[[], None],
    ) -> None:
        """Send the one literal source-pinned continue and require its raw ACK."""

        if not callable(before_send):
            raise AutomationRefusal("DuckStation GDB continue owner is unavailable.")
        if not self._operation_lock.acquire(blocking=False):
            raise AutomationRefusal("Concurrent DuckStation GDB operations are forbidden.")
        try:
            try:
                self.require_existing_connection(process)
                self._attest_bound_endpoint()
                if before_send() is not None:
                    raise AutomationRefusal(
                        "DuckStation GDB continue owner returned an unexpected value."
                    )
                self._send_outbound(GDB_CONTINUE_PACKET)
                # At the pinned DuckStation commit, literal $c#63 is handled
                # before normal packet dispatch.  OnSystemResumed emits exactly
                # one raw '+'; there is no ACK-framed payload for this command.
                if self._read_byte() != ord("+"):
                    raise AutomationRefusal(
                        "DuckStation GDB continue acknowledgement is not exact."
                    )
                self._attest_bound_endpoint()
            except BaseException as exc:
                self._poison_and_raise(exc)
        finally:
            self._operation_lock.release()

    def consume_planned_pause_notification(
        self, process: ProcessIdentity
    ) -> None:
        """Consume exact ``+$S05#b8`` and ACK only the framed stop reply."""

        if not self._operation_lock.acquire(blocking=False):
            raise AutomationRefusal("Concurrent DuckStation GDB operations are forbidden.")
        try:
            try:
                self.require_existing_connection(process)
                self._attest_bound_endpoint()
                if self._read_byte() != ord("+") or self._read_byte() != ord("$"):
                    raise AutomationRefusal(
                        "DuckStation planned pause notification prefix is not exact."
                    )
                payload, checksum_text = self._read_packet_payload_with_checksum()
                self._send_outbound(b"+")
                if payload != b"S05" or checksum_text != b"b8":
                    refusal_type = (
                        _GdbUnexpectedStopRefusal
                        if self._is_stop_notification(payload)
                        else AutomationRefusal
                    )
                    raise refusal_type(
                        "DuckStation planned pause notification is not exact S05#b8."
                    )
                self._attest_bound_endpoint()
            except BaseException as exc:
                self._poison_and_raise(exc)
        finally:
            self._operation_lock.release()

    def consume_planned_resume_notification(
        self, process: ProcessIdentity
    ) -> None:
        """Consume the one raw resume ``+`` without acknowledging it."""

        if not self._operation_lock.acquire(blocking=False):
            raise AutomationRefusal("Concurrent DuckStation GDB operations are forbidden.")
        try:
            try:
                self.require_existing_connection(process)
                self._attest_bound_endpoint()
                if self._read_byte() != ord("+"):
                    raise AutomationRefusal(
                        "DuckStation planned resume notification is not exact."
                    )
                self._attest_bound_endpoint()
            except BaseException as exc:
                self._poison_and_raise(exc)
        finally:
            self._operation_lock.release()

    def close(self) -> None:
        if not self._operation_lock.acquire(blocking=False):
            raise AutomationRefusal("Concurrent DuckStation GDB close is forbidden.")
        try:
            with self._state_guard:
                if self._state in {
                    _GdbReaderState.CLOSED, _GdbReaderState.POISONED
                }:
                    return
                active = self._socket
                self._socket = None
                self._state = _GdbReaderState.CLOSED
            if active is not None:
                try:
                    active.close()
                except BaseException as exc:
                    with self._state_guard:
                        self._state = _GdbReaderState.POISONED
                    raise AutomationRefusal(
                        "DuckStation GDB socket close failed."
                    ) from exc
        finally:
            self._operation_lock.release()

    def _poison_and_raise(self, exc: BaseException) -> None:
        self.invalidate()
        if isinstance(exc, AutomationRefusal):
            raise exc
        raise AutomationRefusal("DuckStation GDB reader failed closed.") from exc

    def _read_fixed_packet(
        self,
        process: ProcessIdentity,
        packet: bytes,
        *,
        strict_ack: bool = False,
        allow_stop_notifications: bool = True,
    ) -> bytes:
        with self._state_guard:
            state = self._state
        if state is _GdbReaderState.CLOSED:
            raise AutomationRefusal("DuckStation GDB reader is closed.")
        if state is _GdbReaderState.POISONED:
            raise AutomationRefusal("DuckStation GDB reader is poisoned.")
        if state not in {_GdbReaderState.NEW, _GdbReaderState.CONNECTED}:
            raise AutomationRefusal("DuckStation GDB reader state is not readable.")
        if type(process) is not ProcessIdentity:
            raise AutomationRefusal("DuckStation GDB process identity is malformed.")
        if packet not in {GDB_PAD_READ_PACKET, GDB_VBLANK_READ_PACKET}:
            raise AutomationRefusal("DuckStation GDB read packet is outside the allowlist.")
        if state is _GdbReaderState.NEW:
            self._connect(process)
        if self._process != process:
            raise AutomationRefusal(
                "DuckStation GDB reader cannot be rebound to another process."
            )
        self._attest_bound_endpoint()
        self._send_outbound(packet)
        reply = self._read_data_reply(
            strict_ack=strict_ack,
            allow_stop_notifications=allow_stop_notifications,
        )
        self._attest_bound_endpoint()
        return reply

    def _connect(self, process: ProcessIdentity) -> None:
        with self._state_guard:
            if self._state is not _GdbReaderState.NEW:
                raise AutomationRefusal("DuckStation GDB connection state is not new.")
            self._state = _GdbReaderState.CONNECTING
        observed_before = self._observer.observe(process.pid)
        self._require_connecting()
        if type(observed_before) is not ProcessIdentity or observed_before != process:
            raise AutomationRefusal(
                "DuckStation process identity changed before GDB connection."
            )
        listener_before = self._endpoint_attestor.observe(observed_before)
        self._require_connecting()
        if (
            type(listener_before) is not GdbListenerIdentity
            or listener_before.pid != observed_before.pid
        ):
            raise AutomationRefusal(
                "DuckStation GDB listener is not bound to the exact process."
            )
        with self._state_guard:
            if (
                self._state is not _GdbReaderState.CONNECTING
                or self._connection_attempts != 0
            ):
                raise AutomationRefusal(
                    "DuckStation GDB connector attempt is not singular."
                )
            # This counter crosses the native boundary with the attempt.  A
            # connector exception still consumes the sole allowed attempt.
            self._connection_attempts = 1
        active = self._connector(
            (GDB_HOST, GDB_PORT), GDB_SOCKET_TIMEOUT_SECONDS
        )
        with self._state_guard:
            connection_is_live = self._state is _GdbReaderState.CONNECTING
            if connection_is_live:
                self._socket = active
        if not connection_is_live:
            try:
                active.close()
            except BaseException:
                pass
            raise AutomationRefusal(
                "DuckStation GDB connection was poisoned while opening."
            )
        if active.settimeout(GDB_SOCKET_TIMEOUT_SECONDS) is not None:
            raise AutomationRefusal("DuckStation GDB socket timeout setup was malformed.")
        self._require_connecting()
        peer = active.getpeername()
        self._require_connecting()
        if (
            type(peer) is not tuple
            or len(peer) != 2
            or peer[0] != GDB_HOST
            or type(peer[1]) is not int
            or peer[1] != GDB_PORT
        ):
            raise AutomationRefusal("DuckStation GDB socket peer is not exact loopback.")
        local = active.getsockname()
        self._require_connecting()
        if (
            type(local) is not tuple
            or len(local) != 2
            or local[0] != GDB_HOST
            or type(local[1]) is not int
            or isinstance(local[1], bool)
        ):
            raise AutomationRefusal("DuckStation GDB local socket endpoint is not exact.")
        descriptor = active.fileno()
        self._require_connecting()
        if (
            type(descriptor) is not int
            or isinstance(descriptor, bool)
            or descriptor < 0
            or descriptor > 2_147_483_647
        ):
            raise AutomationRefusal("DuckStation GDB client descriptor is not exact.")
        client_endpoint = GdbClientEndpointIdentity(
            os.getpid(), descriptor, local[0], local[1]
        )
        observed_after = self._observer.observe(process.pid)
        self._require_connecting()
        if (
            type(observed_after) is not ProcessIdentity
            or observed_after != observed_before
        ):
            raise AutomationRefusal(
                "DuckStation process identity changed during GDB connection."
            )
        listener_after = self._endpoint_attestor.observe(observed_after)
        self._require_connecting()
        if (
            type(listener_after) is not GdbListenerIdentity
            or listener_after.pid != observed_after.pid
            or listener_after != listener_before
        ):
            raise AutomationRefusal(
                "DuckStation GDB listener was replaced during connection."
            )
        with self._state_guard:
            if self._state is not _GdbReaderState.CONNECTING:
                raise AutomationRefusal(
                    "DuckStation GDB connection was poisoned during setup."
                )
            self._process = process
            self._listener = listener_before
            self._client_endpoint = client_endpoint
            self._state = _GdbReaderState.CONNECTED

    def _require_connecting(self) -> None:
        with self._state_guard:
            connecting = self._state is _GdbReaderState.CONNECTING
        if not connecting:
            raise AutomationRefusal(
                "DuckStation GDB connection was poisoned during setup."
            )

    def _attest_bound_endpoint(self) -> None:
        with self._state_guard:
            connected = self._state is _GdbReaderState.CONNECTED
            active = self._socket
            process = self._process
            listener = self._listener
        if not connected or active is None or process is None or listener is None:
            raise _GdbTopologyDriftRefusal(
                "DuckStation GDB connection is not fully bound."
            )
        try:
            observed = self._observer.observe(process.pid)
        except BaseException as exc:
            raise _GdbTopologyDriftRefusal(
                "DuckStation process identity became unobservable around GDB work."
            ) from exc
        if type(observed) is not ProcessIdentity or observed != process:
            raise _GdbTopologyDriftRefusal(
                "DuckStation process identity changed around a GDB read."
            )
        try:
            observed_listener = self._endpoint_attestor.observe(observed)
        except BaseException as exc:
            raise _GdbTopologyDriftRefusal(
                "DuckStation GDB listener became unobservable around GDB work."
            ) from exc
        if (
            type(observed_listener) is not GdbListenerIdentity
            or observed_listener.pid != observed.pid
            or observed_listener != listener
        ):
            raise _GdbTopologyDriftRefusal(
                "DuckStation GDB listener identity changed around a read."
            )

    def _send_outbound(self, data: bytes) -> None:
        if type(data) is not bytes or data not in GDB_SESSION_OUTBOUND:
            raise AutomationRefusal("DuckStation GDB outbound packet is forbidden.")
        with self._state_guard:
            active = self._socket
        if active is None:
            raise AutomationRefusal("DuckStation GDB socket is unavailable.")
        if active.sendall(data) is not None:
            raise AutomationRefusal("DuckStation GDB outbound write was malformed.")

    def _read_byte(self) -> int:
        with self._state_guard:
            active = self._socket
        if active is None:
            raise AutomationRefusal("DuckStation GDB socket is unavailable.")
        # Avoid read-ahead: a byte received before a later fixed request must
        # never be buffered and misattributed to that later transaction.
        received = active.recv(1)
        if type(received) is not bytes or len(received) != 1:
            raise AutomationRefusal("DuckStation GDB reply ended or was malformed.")
        return received[0]

    @staticmethod
    def _is_hex_byte(value: int) -> bool:
        return (
            ord("0") <= value <= ord("9")
            or ord("a") <= value <= ord("f")
            or ord("A") <= value <= ord("F")
        )

    def _reject_bad_packet(self, reason: str) -> None:
        self._send_outbound(b"-")
        raise AutomationRefusal(reason)

    def _read_packet_payload_with_checksum(self) -> tuple[bytes, bytes]:
        payload = bytearray()
        while True:
            value = self._read_byte()
            if value == ord("#"):
                break
            payload.append(value)
            if len(payload) > GDB_MAX_RSP_PAYLOAD_BYTES:
                self._reject_bad_packet("DuckStation GDB packet payload is oversized.")
        checksum_text = bytes((self._read_byte(), self._read_byte()))
        if not all(self._is_hex_byte(value) for value in checksum_text):
            self._reject_bad_packet("DuckStation GDB packet checksum is malformed.")
        expected_checksum = int(checksum_text.decode("ascii"), 16)
        if sum(payload) & 0xFF != expected_checksum:
            self._reject_bad_packet("DuckStation GDB packet checksum does not match.")
        return bytes(payload), checksum_text

    def _read_packet_payload(self) -> bytes:
        payload, _ = self._read_packet_payload_with_checksum()
        return payload

    @classmethod
    def _is_stop_notification(cls, payload: bytes) -> bool:
        if (
            len(payload) < 3
            or payload[:1] not in {b"S", b"T"}
            or not cls._is_hex_byte(payload[1])
            or not cls._is_hex_byte(payload[2])
        ):
            return False
        if payload[:1] == b"S":
            return len(payload) == 3
        return re.fullmatch(
            rb"T[0-9A-Fa-f]{2}(?:[0-9A-Za-z_.-]+:[0-9A-Za-z_.-]+;)+",
            payload,
        ) is not None

    def _read_data_reply(
        self, *, strict_ack: bool = False, allow_stop_notifications: bool = True
    ) -> bytes:
        server_acknowledged = False
        for _ in range(GDB_MAX_RSP_EVENTS_PER_READ):
            marker = self._read_byte()
            if marker == ord("+"):
                if strict_ack and server_acknowledged:
                    raise AutomationRefusal(
                        "DuckStation GDB server sent a duplicate request acknowledgement."
                    )
                server_acknowledged = True
                continue
            if marker == ord("-"):
                raise AutomationRefusal(
                    "DuckStation GDB server negatively acknowledged a fixed read."
                )
            if marker != ord("$"):
                raise AutomationRefusal("DuckStation GDB reply marker is unexpected.")
            payload = self._read_packet_payload()
            self._send_outbound(b"+")
            if self._is_stop_notification(payload):
                if not allow_stop_notifications:
                    raise _GdbUnexpectedStopRefusal(
                        "DuckStation GDB stop notification interrupted RUNNING state."
                    )
                continue
            if not server_acknowledged:
                raise AutomationRefusal(
                    "DuckStation GDB server omitted the required request acknowledgement."
                )
            if (
                len(payload) == 3
                and payload[:1] == b"E"
                and self._is_hex_byte(payload[1])
                and self._is_hex_byte(payload[2])
            ):
                raise AutomationRefusal("DuckStation GDB memory read returned an error.")
            if len(payload) != 8 or not all(
                self._is_hex_byte(value) for value in payload
            ):
                raise AutomationRefusal(
                    "DuckStation GDB memory reply is not exactly four hex bytes."
                )
            return bytes.fromhex(payload.decode("ascii"))
        raise AutomationRefusal("DuckStation GDB reply exceeded its event bound.")


class _GdbSessionState(enum.Enum):
    NEW = enum.auto()
    CONNECTING = enum.auto()
    CONNECTED_PAUSED = enum.auto()
    CONTINUE_IN_FLIGHT = enum.auto()
    RUNNING = enum.auto()
    EXPECTING_PLAN_PAUSE = enum.auto()
    PAUSED_BY_PLAN = enum.auto()
    EXPECTING_PLAN_RESUME = enum.auto()
    POISONED = enum.auto()
    CLOSED = enum.auto()


@dataclasses.dataclass(frozen=True)
class GdbSessionStatus:
    """Non-I/O status for the one owner of pause, resume, and fixed reads."""

    state: str
    bound_process: ProcessIdentity | None
    bound_listener: GdbListenerIdentity | None
    connection_attempts: int
    continue_attempts: int
    pause_state_certain: bool
    single_client_observed: bool

    def __post_init__(self) -> None:
        if (
            self.state not in {state.name for state in _GdbSessionState}
            or (
                self.bound_process is not None
                and type(self.bound_process) is not ProcessIdentity
            )
            or (
                self.bound_listener is not None
                and type(self.bound_listener) is not GdbListenerIdentity
            )
            or type(self.connection_attempts) is not int
            or not 0 <= self.connection_attempts <= 2_147_483_647
            or type(self.continue_attempts) is not int
            or not 0 <= self.continue_attempts <= 2_147_483_647
            or type(self.pause_state_certain) is not bool
            or type(self.single_client_observed) is not bool
        ):
            raise AutomationRefusal("DuckStation GDB session status is malformed.")
        bound = self.bound_process is not None and self.bound_listener is not None
        if (self.bound_process is None) != (self.bound_listener is None):
            raise AutomationRefusal("DuckStation GDB status binding is incomplete.")
        exact_active = (
            bound
            and self.connection_attempts == 1
            and self.continue_attempts == 1
            and self.single_client_observed
        )
        if self.state == "NEW" and (
            bound
            or self.connection_attempts != 0
            or self.continue_attempts != 0
            or self.pause_state_certain
            or self.single_client_observed
        ):
            raise AutomationRefusal("Fresh DuckStation GDB session status is inconsistent.")
        if self.state in {"RUNNING", "PAUSED_BY_PLAN"} and (
            not exact_active or not self.pause_state_certain
        ):
            raise AutomationRefusal("Confirmed DuckStation GDB session status is inconsistent.")
        if self.state in {"EXPECTING_PLAN_PAUSE", "EXPECTING_PLAN_RESUME"} and (
            not exact_active or self.pause_state_certain
        ):
            raise AutomationRefusal("Armed DuckStation GDB session status is inconsistent.")


class PersistentDuckStationGdbSession:
    """One fixed DuckStation connection with one exact resume capability.

    Construction and :meth:`status` are side-effect free.  Only
    :meth:`connect_resume_once` may create the socket.  It first proves the
    connection-induced pause with a stable guest VBlank tick, sends literal
    ``$c#63`` once, and requires forward VBlank progress before exposing pad or
    VBlank reads.  This class is deliberately absent from :func:`main`.
    """

    def __init__(
        self,
        observer: ProcessObserver,
        endpoint_attestor: GdbEndpointAttestor,
        connector: Callable[[tuple[str, int], float], _GdbSocket] | None = None,
        *,
        clock: Callable[[], float] = time.monotonic,
        sleeper: Callable[[float], None] = time.sleep,
        timeout_seconds: float = GDB_VBLANK_PROGRESS_TIMEOUT_SECONDS,
        interval_seconds: float = GDB_VBLANK_PROGRESS_INTERVAL_SECONDS,
        paused_minimum_seconds: float = GDB_PAUSED_CONFIRMATION_MIN_SECONDS,
        maximum_samples: int = GDB_VBLANK_PROGRESS_MAX_SAMPLES,
    ) -> None:
        numeric_policy = (timeout_seconds, interval_seconds, paused_minimum_seconds)
        if (
            not callable(clock)
            or not callable(sleeper)
            or any(
                type(value) not in {int, float}
                or not math.isfinite(float(value))
                or value <= 0
                for value in numeric_policy
            )
            or paused_minimum_seconds >= timeout_seconds
            or type(maximum_samples) is not int
            or maximum_samples < 2
            or maximum_samples > 4096
        ):
            raise AutomationRefusal("DuckStation GDB session timing policy is invalid.")
        if not callable(getattr(endpoint_attestor, "observe_connection_set", None)):
            raise AutomationRefusal(
                "DuckStation GDB whole connection-set attestor is unavailable."
            )
        self._transport = _PersistentDuckStationGdbTransport(
            observer, endpoint_attestor, connector=connector
        )
        self._connection_set_attestor = endpoint_attestor
        self._clock = clock
        self._sleeper = sleeper
        self._timeout_seconds = float(timeout_seconds)
        self._interval_seconds = float(interval_seconds)
        self._paused_minimum_seconds = float(paused_minimum_seconds)
        self._maximum_samples = maximum_samples
        self._state = _GdbSessionState.NEW
        self._bound_process: ProcessIdentity | None = None
        self._bound_listener: GdbListenerIdentity | None = None
        self._observed_connection_set: GdbObservedConnectionSet | None = None
        self._continue_attempts = 0
        self._pause_state_certain = False
        self._single_client_observed = False
        self._planned_paused_tick: int | None = None
        self._state_guard = threading.Lock()
        self._operation_lock = threading.Lock()

    def status(self) -> GdbSessionStatus:
        """Return session state without observer, lsof, socket, or RSP work."""

        # Serialize the two internal snapshots with every public mutation.  A
        # status caller may wait for a bounded operation, but can never observe
        # RUNNING paired with a pre-connect or poisoned transport snapshot.
        with self._operation_lock:
            transport = self._transport.status()
            with self._state_guard:
                if self._state in {
                    _GdbSessionState.CONNECTED_PAUSED,
                    _GdbSessionState.CONTINUE_IN_FLIGHT,
                    _GdbSessionState.RUNNING,
                    _GdbSessionState.EXPECTING_PLAN_PAUSE,
                    _GdbSessionState.PAUSED_BY_PLAN,
                    _GdbSessionState.EXPECTING_PLAN_RESUME,
                } and (
                    transport.state != "CONNECTED"
                    or transport.bound_process != self._bound_process
                    or transport.bound_listener != self._bound_listener
                    or transport.client_endpoint is None
                    or transport.connection_attempts != 1
                ):
                    raise AutomationRefusal(
                        "DuckStation GDB session/transport status is inconsistent."
                    )
                return GdbSessionStatus(
                    self._state.name,
                    self._bound_process,
                    self._bound_listener,
                    transport.connection_attempts,
                    self._continue_attempts,
                    self._pause_state_certain,
                    self._single_client_observed,
                )

    @staticmethod
    def _require_tick(value: int) -> int:
        if type(value) is not int or not 0 <= value <= 0xFFFFFFFF:
            raise AutomationRefusal("DuckStation VBlank tick is malformed.")
        return value

    def _clock_value(self) -> float:
        value = self._clock()
        if (
            type(value) not in {int, float}
            or not math.isfinite(float(value))
            or value < 0
        ):
            raise AutomationRefusal("DuckStation GDB session clock is malformed.")
        return float(value)

    @staticmethod
    def _certainty_after_failure(
        exc: BaseException, current: bool
    ) -> bool:
        if isinstance(exc, _GdbTopologyDriftRefusal):
            return False
        if isinstance(exc, _GdbPauseContradictionRefusal):
            return False
        if isinstance(exc, _GdbUnexpectedStopRefusal):
            return True
        return current

    def _sleep_once(self) -> None:
        if self._sleeper(self._interval_seconds) is not None:
            raise AutomationRefusal("DuckStation GDB session sleeper is malformed.")

    def _observe_connection_set(
        self,
        process: ProcessIdentity,
        expected_client: GdbClientEndpointIdentity | None,
    ) -> GdbObservedConnectionSet:
        observed = self._connection_set_attestor.observe_connection_set(
            process, expected_client
        )
        if (
            type(observed) is not GdbObservedConnectionSet
            or observed.listener.pid != process.pid
            or observed.client_endpoint != expected_client
        ):
            raise AutomationRefusal(
                "DuckStation GDB whole connection-set evidence is malformed."
            )
        return observed

    def _attest_no_existing_client(
        self, process: ProcessIdentity
    ) -> GdbObservedConnectionSet:
        observed = self._observe_connection_set(process, None)
        if observed.client_endpoint is not None:
            raise AutomationRefusal(
                "DuckStation GDB already has an established client."
            )
        return observed

    def _attest_single_client(
        self, process: ProcessIdentity, *, bind: bool = False
    ) -> GdbObservedConnectionSet:
        transport = self._transport.status()
        if (
            transport.state != "CONNECTED"
            or transport.bound_process != process
            or transport.bound_listener is None
            or transport.client_endpoint is None
            or transport.connection_attempts != 1
        ):
            raise _GdbTopologyDriftRefusal(
                "DuckStation GDB transport cannot attest one existing client."
            )
        try:
            observed = self._observe_connection_set(
                process, transport.client_endpoint
            )
        except BaseException as exc:
            raise _GdbTopologyDriftRefusal(
                "DuckStation GDB whole connection set became unobservable: "
                f"{exc}"
            ) from exc
        if observed.listener != transport.bound_listener:
            raise _GdbTopologyDriftRefusal(
                "DuckStation GDB listener changed in the whole connection set."
            )
        with self._state_guard:
            expected = self._observed_connection_set
        if bind:
            if expected is not None:
                raise AutomationRefusal(
                    "DuckStation GDB client socket set was already bound."
                )
            with self._state_guard:
                self._observed_connection_set = observed
                self._single_client_observed = True
        elif expected is None or observed != expected:
            raise _GdbTopologyDriftRefusal(
                "DuckStation GDB established client set changed around an operation."
            )
        return observed

    def _read_vblank_owned(self, process: ProcessIdentity) -> int:
        self._attest_single_client(process)
        value = self._require_tick(
            self._transport.read_vblank_tick_strict_running(process)
        )
        self._attest_single_client(process)
        return value

    @staticmethod
    def _forward_delta(before: int, after: int) -> int:
        return (after - before) & 0xFFFFFFFF

    def _confirm_stable_pause(
        self,
        process: ProcessIdentity,
        baseline_tick: int,
        *,
        label: str,
        start_clock: float,
    ) -> tuple[int, float]:
        if label not in {"connection-induced", "planned"}:
            raise AutomationRefusal("DuckStation GDB pause proof label is malformed.")
        start = start_clock
        previous_clock = self._clock_value()
        if previous_clock < start:
            raise AutomationRefusal("DuckStation GDB session clock moved backward.")
        if previous_clock - start > self._timeout_seconds:
            raise AutomationRefusal(
                f"DuckStation {label} pause confirmation timed out."
            )
        for _ in range(self._maximum_samples):
            self._sleep_once()
            observed_clock = self._clock_value()
            if observed_clock < previous_clock:
                raise AutomationRefusal("DuckStation GDB session clock moved backward.")
            previous_clock = observed_clock
            elapsed = observed_clock - start
            if elapsed > self._timeout_seconds:
                break
            observed_tick = self._require_tick(
                self._read_vblank_owned(process)
            )
            after_read_clock = self._clock_value()
            if after_read_clock < observed_clock:
                raise AutomationRefusal("DuckStation GDB session clock moved backward.")
            previous_clock = after_read_clock
            elapsed = after_read_clock - start
            if observed_tick != baseline_tick:
                raise _GdbPauseContradictionRefusal(
                    f"DuckStation did not remain at the {label} pause."
                )
            if elapsed > self._timeout_seconds:
                break
            if elapsed >= self._paused_minimum_seconds:
                return observed_tick, after_read_clock
        raise AutomationRefusal(
            f"DuckStation {label} pause confirmation timed out."
        )

    def _confirm_running(
        self, process: ProcessIdentity, paused_tick: int, start_clock: float
    ) -> tuple[int, float]:
        previous_clock = start_clock
        for _ in range(self._maximum_samples):
            self._sleep_once()
            observed_clock = self._clock_value()
            if observed_clock < previous_clock:
                raise AutomationRefusal("DuckStation GDB session clock moved backward.")
            previous_clock = observed_clock
            if observed_clock - start_clock > self._timeout_seconds:
                break
            observed_tick = self._require_tick(
                self._read_vblank_owned(process)
            )
            after_read_clock = self._clock_value()
            if after_read_clock < observed_clock:
                raise AutomationRefusal("DuckStation GDB session clock moved backward.")
            previous_clock = after_read_clock
            if after_read_clock - start_clock > self._timeout_seconds:
                break
            delta = self._forward_delta(paused_tick, observed_tick)
            if delta == 0:
                continue
            if delta >= 0x80000000:
                raise AutomationRefusal(
                    "DuckStation VBlank tick moved backward or reset after resume."
                )
            return observed_tick, after_read_clock
        raise AutomationRefusal("DuckStation did not prove RUNNING after GDB resume.")

    def connect_resume_once(self, process: ProcessIdentity) -> None:
        """Own the sole connection pause and literal one-shot continue."""

        if not self._operation_lock.acquire(blocking=False):
            self._poison_and_raise(
                AutomationRefusal("Concurrent DuckStation GDB session work is forbidden."),
                pause_state_certain=False,
            )
        try:
            try:
                if type(process) is not ProcessIdentity:
                    raise AutomationRefusal(
                        "DuckStation GDB session process identity is malformed."
                    )
                with self._state_guard:
                    if self._state is not _GdbSessionState.NEW:
                        raise AutomationRefusal(
                            "DuckStation GDB session can connect and resume only once."
                        )
                    self._state = _GdbSessionState.CONNECTING
                preconnect_set = self._attest_no_existing_client(process)
                self._transport.connect_once(process)
                transport = self._transport.status()
                if (
                    transport.state != "CONNECTED"
                    or transport.bound_process != process
                    or transport.bound_listener is None
                    or transport.bound_listener.pid != process.pid
                    or transport.bound_listener != preconnect_set.listener
                    or transport.client_endpoint is None
                    or transport.connection_attempts != 1
                ):
                    raise AutomationRefusal(
                        "DuckStation GDB session transport binding is not exact."
                    )
                with self._state_guard:
                    self._bound_process = process
                    self._bound_listener = transport.bound_listener
                    self._state = _GdbSessionState.CONNECTED_PAUSED
                    self._pause_state_certain = False
                self._attest_single_client(process, bind=True)
                pause_proof_clock = self._clock_value()
                paused_tick = self._read_vblank_owned(process)
                paused_tick, paused_clock = self._confirm_stable_pause(
                    process,
                    paused_tick,
                    label="connection-induced",
                    start_clock=pause_proof_clock,
                )
                with self._state_guard:
                    if self._state is not _GdbSessionState.CONNECTED_PAUSED:
                        raise AutomationRefusal(
                            "DuckStation GDB paused-session ownership changed."
                        )
                    self._pause_state_certain = True

                def own_continue_boundary() -> None:
                    self._attest_single_client(process)
                    with self._state_guard:
                        if (
                            self._state is not _GdbSessionState.CONNECTED_PAUSED
                            or not self._pause_state_certain
                            or self._continue_attempts != 0
                        ):
                            raise AutomationRefusal(
                                "DuckStation GDB continue attempt is not singular."
                            )
                        # Sending can fail after partial delivery.  From this
                        # exact callback until VBlank advances, pause state is
                        # deliberately unknown.
                        self._continue_attempts = 1
                        self._state = _GdbSessionState.CONTINUE_IN_FLIGHT
                        self._pause_state_certain = False

                self._transport.continue_from_connection_pause(
                    process, before_send=own_continue_boundary
                )
                self._attest_single_client(process)
                self._confirm_running(process, paused_tick, paused_clock)
                self._transport.require_existing_connection(process)
                with self._state_guard:
                    if self._state is not _GdbSessionState.CONTINUE_IN_FLIGHT:
                        raise AutomationRefusal(
                            "DuckStation GDB resume ownership changed."
                        )
                    self._state = _GdbSessionState.RUNNING
                    self._pause_state_certain = True
            except BaseException as exc:
                with self._state_guard:
                    certain = self._certainty_after_failure(
                        exc, self._pause_state_certain
                    )
                self._poison_and_raise(exc, pause_state_certain=certain)
        finally:
            self._operation_lock.release()

    def arm_planned_pause(self, process: ProcessIdentity) -> None:
        """Arm RUNNING -> PAUSED before the caller begins one Space pulse.

        The future coordinator must finish and resolve the owed key-up before
        calling :meth:`confirm_planned_pause`.  This offline session never
        posts the host key itself and remains absent from production ``main``.
        """

        self._arm_planned_transition(
            process,
            required=_GdbSessionState.RUNNING,
            armed=_GdbSessionState.EXPECTING_PLAN_PAUSE,
        )

    def arm_planned_resume(self, process: ProcessIdentity) -> None:
        """Arm PAUSED -> RUNNING before the caller begins one Space pulse."""

        self._arm_planned_transition(
            process,
            required=_GdbSessionState.PAUSED_BY_PLAN,
            armed=_GdbSessionState.EXPECTING_PLAN_RESUME,
        )

    def _arm_planned_transition(
        self,
        process: ProcessIdentity,
        *,
        required: _GdbSessionState,
        armed: _GdbSessionState,
    ) -> None:
        if not self._operation_lock.acquire(blocking=False):
            self._poison_and_raise(
                AutomationRefusal("Concurrent DuckStation GDB transition is forbidden."),
                pause_state_certain=False,
            )
        try:
            try:
                if type(process) is not ProcessIdentity:
                    raise AutomationRefusal(
                        "DuckStation GDB transition process identity is malformed."
                    )
                with self._state_guard:
                    if (
                        self._state is not required
                        or not self._pause_state_certain
                        or self._bound_process != process
                        or not self._single_client_observed
                    ):
                        raise AutomationRefusal(
                            "DuckStation GDB planned transition baseline is not exact."
                        )
                self._attest_single_client(process)
                with self._state_guard:
                    if self._state is not required:
                        raise AutomationRefusal(
                            "DuckStation GDB planned transition state changed while arming."
                        )
                    self._state = armed
                    # Once a release is armed, an external/UI race can make the
                    # resulting emulation state unknowable until its exact RSP
                    # notification and VBlank semantics are consumed.
                    self._pause_state_certain = False
            except BaseException as exc:
                with self._state_guard:
                    certain = self._certainty_after_failure(
                        exc, self._pause_state_certain
                    )
                self._poison_and_raise(exc, pause_state_certain=certain)
        finally:
            self._operation_lock.release()

    def confirm_planned_pause(self, process: ProcessIdentity) -> None:
        """After resolved Space key-up, consume stop evidence and prove PAUSED."""

        if not self._operation_lock.acquire(blocking=False):
            self._poison_and_raise(
                AutomationRefusal("Concurrent DuckStation GDB transition is forbidden."),
                pause_state_certain=False,
            )
        try:
            try:
                with self._state_guard:
                    if (
                        self._state is not _GdbSessionState.EXPECTING_PLAN_PAUSE
                        or self._bound_process != process
                        or self._pause_state_certain
                    ):
                        raise AutomationRefusal(
                            "DuckStation GDB planned pause was not armed exactly."
                        )
                # Start the evidence deadline before either lsof attestation or
                # the blocking stop-frame read.  A late exact S05 is consumed
                # and classified, but cannot reset the acceptance window.
                pause_proof_clock = self._clock_value()
                self._attest_single_client(process)
                self._transport.consume_planned_pause_notification(process)
                self._attest_single_client(process)
                # Exact S05 establishes PAUSED even if the stronger stable-tick
                # proof later times out; preserve that fact in poisoned status.
                with self._state_guard:
                    self._pause_state_certain = True
                after_notification_clock = self._clock_value()
                if after_notification_clock < pause_proof_clock:
                    raise AutomationRefusal(
                        "DuckStation GDB session clock moved backward."
                    )
                if (
                    after_notification_clock - pause_proof_clock
                    > self._timeout_seconds
                ):
                    raise AutomationRefusal(
                        "DuckStation planned pause confirmation timed out."
                    )
                paused_tick = self._read_vblank_owned(process)
                paused_tick, _ = self._confirm_stable_pause(
                    process,
                    paused_tick,
                    label="planned",
                    start_clock=pause_proof_clock,
                )
                with self._state_guard:
                    if self._state is not _GdbSessionState.EXPECTING_PLAN_PAUSE:
                        raise AutomationRefusal(
                            "DuckStation GDB planned pause ownership changed."
                        )
                    self._planned_paused_tick = paused_tick
                    self._state = _GdbSessionState.PAUSED_BY_PLAN
                    self._pause_state_certain = True
            except BaseException as exc:
                with self._state_guard:
                    certain = self._certainty_after_failure(
                        exc, self._pause_state_certain
                    )
                self._poison_and_raise(exc, pause_state_certain=certain)
        finally:
            self._operation_lock.release()

    def confirm_planned_resume(self, process: ProcessIdentity) -> None:
        """After resolved Space key-up, consume raw resume and prove RUNNING."""

        if not self._operation_lock.acquire(blocking=False):
            self._poison_and_raise(
                AutomationRefusal("Concurrent DuckStation GDB transition is forbidden."),
                pause_state_certain=False,
            )
        try:
            try:
                with self._state_guard:
                    paused_tick = self._planned_paused_tick
                    if (
                        self._state is not _GdbSessionState.EXPECTING_PLAN_RESUME
                        or self._bound_process != process
                        or self._pause_state_certain
                        or type(paused_tick) is not int
                    ):
                        raise AutomationRefusal(
                            "DuckStation GDB planned resume was not armed exactly."
                        )
                resume_clock = self._clock_value()
                self._attest_single_client(process)
                self._transport.consume_planned_resume_notification(process)
                self._attest_single_client(process)
                self._confirm_running(process, paused_tick, resume_clock)
                with self._state_guard:
                    if self._state is not _GdbSessionState.EXPECTING_PLAN_RESUME:
                        raise AutomationRefusal(
                            "DuckStation GDB planned resume ownership changed."
                        )
                    self._planned_paused_tick = None
                    self._state = _GdbSessionState.RUNNING
                    self._pause_state_certain = True
            except BaseException as exc:
                with self._state_guard:
                    certain = self._certainty_after_failure(
                        exc, self._pause_state_certain
                    )
                self._poison_and_raise(exc, pause_state_certain=certain)
        finally:
            self._operation_lock.release()

    def _require_owned_state(
        self,
        process: ProcessIdentity,
        *,
        expected: _GdbSessionState,
        pause_state_certain: bool,
        require_paused_tick: bool,
    ) -> None:
        """Reattest the sole-client topology without sending an RSP packet."""

        if not self._operation_lock.acquire(blocking=False):
            self._poison_and_raise(
                AutomationRefusal("Concurrent DuckStation GDB ownership check is forbidden."),
                pause_state_certain=False,
            )
        try:
            try:
                if type(process) is not ProcessIdentity:
                    raise AutomationRefusal(
                        "DuckStation GDB ownership process identity is malformed."
                    )

                def state_is_exact() -> bool:
                    return (
                        self._state is expected
                        and self._pause_state_certain is pause_state_certain
                        and self._bound_process == process
                        and self._bound_listener is not None
                        and self._continue_attempts == 1
                        and self._single_client_observed
                        and (
                            not require_paused_tick
                            or type(self._planned_paused_tick) is int
                        )
                    )

                with self._state_guard:
                    if not state_is_exact():
                        raise AutomationRefusal(
                            "DuckStation GDB owned state is not exact before pulse work."
                        )
                self._attest_single_client(process)
                self._transport.require_existing_connection(process)
                with self._state_guard:
                    if not state_is_exact():
                        raise AutomationRefusal(
                            "DuckStation GDB owned state changed during pulse work."
                        )
            except BaseException as exc:
                with self._state_guard:
                    certain = self._certainty_after_failure(
                        exc, self._pause_state_certain
                    )
                self._poison_and_raise(exc, pause_state_certain=certain)
        finally:
            self._operation_lock.release()

    def require_running_owned(self, process: ProcessIdentity) -> None:
        """Require RUNNING and reattest the one exact connection set."""

        self._require_owned_state(
            process,
            expected=_GdbSessionState.RUNNING,
            pause_state_certain=True,
            require_paused_tick=False,
        )

    def require_paused_owned(self, process: ProcessIdentity) -> None:
        """Require planned PAUSED and reattest the one exact connection set."""

        self._require_owned_state(
            process,
            expected=_GdbSessionState.PAUSED_BY_PLAN,
            pause_state_certain=True,
            require_paused_tick=True,
        )

    def require_planned_pause_armed(self, process: ProcessIdentity) -> None:
        """Require the exact uncertain RUNNING -> PAUSED armed state."""

        self._require_owned_state(
            process,
            expected=_GdbSessionState.EXPECTING_PLAN_PAUSE,
            pause_state_certain=False,
            require_paused_tick=False,
        )

    def require_planned_resume_armed(self, process: ProcessIdentity) -> None:
        """Require the exact uncertain PAUSED -> RUNNING armed state."""

        self._require_owned_state(
            process,
            expected=_GdbSessionState.EXPECTING_PLAN_RESUME,
            pause_state_certain=False,
            require_paused_tick=True,
        )

    def require_paused(self, process: ProcessIdentity) -> None:
        """Require confirmed planned PAUSED state without native I/O."""

        with self._state_guard:
            valid = (
                self._state is _GdbSessionState.PAUSED_BY_PLAN
                and self._pause_state_certain
                and self._bound_process == process
                and self._bound_listener is not None
                and self._continue_attempts == 1
                and self._single_client_observed
                and type(self._planned_paused_tick) is int
            )
        if not valid:
            raise AutomationRefusal(
                "DuckStation GDB session is not exact confirmed PAUSED state."
            )
        self._transport.require_existing_connection(process)

    def require_running(self, process: ProcessIdentity) -> None:
        """Require the already-confirmed RUNNING binding without native I/O."""

        with self._state_guard:
            valid = (
                self._state is _GdbSessionState.RUNNING
                and self._pause_state_certain
                and self._bound_process == process
                and self._bound_listener is not None
                and self._continue_attempts == 1
                and self._single_client_observed
            )
        if not valid:
            raise AutomationRefusal(
                "DuckStation GDB session is not exact confirmed RUNNING state."
            )
        self._transport.require_existing_connection(process)

    def _read_while_running(
        self, process: ProcessIdentity, reader: Callable[[ProcessIdentity], int]
    ) -> int:
        if not self._operation_lock.acquire(blocking=False):
            self._poison_and_raise(
                AutomationRefusal("Concurrent DuckStation GDB session reads are forbidden."),
                pause_state_certain=False,
            )
        try:
            try:
                self.require_running(process)
                self._attest_single_client(process)
                value = reader(process)
                self._attest_single_client(process)
                self.require_running(process)
                return value
            except BaseException as exc:
                self._poison_and_raise(
                    exc,
                    pause_state_certain=self._certainty_after_failure(exc, False),
                )
        finally:
            self._operation_lock.release()

    def read_pad_word(self, process: ProcessIdentity) -> int:
        return self._read_while_running(
            process, self._transport.read_pad_word_strict_running
        )

    def read_vblank_tick(self, process: ProcessIdentity) -> int:
        return self._require_tick(
            self._read_while_running(
                process, self._transport.read_vblank_tick_strict_running
            )
        )

    def close(self) -> None:
        if not self._operation_lock.acquire(blocking=False):
            self._poison_and_raise(
                AutomationRefusal("Concurrent DuckStation GDB session close is forbidden."),
                pause_state_certain=False,
            )
        try:
            try:
                with self._state_guard:
                    if self._state is _GdbSessionState.CLOSED:
                        return
                    if self._state is not _GdbSessionState.POISONED:
                        self._state = _GdbSessionState.CLOSED
                self._transport.close()
                with self._state_guard:
                    self._single_client_observed = False
            except BaseException as exc:
                self._poison_and_raise(exc, pause_state_certain=False)
        finally:
            self._operation_lock.release()

    def _poison_and_raise(
        self, exc: BaseException, *, pause_state_certain: bool
    ) -> None:
        with self._state_guard:
            self._state = _GdbSessionState.POISONED
            self._pause_state_certain = pause_state_certain
            self._single_client_observed = False
        self._transport.invalidate()
        if isinstance(exc, AutomationRefusal):
            raise exc
        raise AutomationRefusal("DuckStation GDB session failed closed.") from exc


class _GdbInputPulseCoordinatorState(enum.Enum):
    NEW = enum.auto()
    STARTED = enum.auto()
    PULSE_IN_FLIGHT = enum.auto()
    COMPLETE = enum.auto()
    POISONED = enum.auto()
    CLOSED = enum.auto()


@dataclasses.dataclass(frozen=True)
class GdbInputPulseCoordinatorStatus:
    """Non-I/O truth for the offline event-aware GDB/pulse adapter."""

    state: str
    bound_process: ProcessIdentity | None
    next_event_index: int
    planned_count: int
    settled_event_count: int
    emulation_state: PlannedEmulationState | None
    possibly_held: tuple[tuple[int, KeyAction], ...]
    session_state: str
    recovery_required: bool

    def __post_init__(self) -> None:
        if (
            self.state
            not in {state.name for state in _GdbInputPulseCoordinatorState}
            or (
                self.bound_process is not None
                and type(self.bound_process) is not ProcessIdentity
            )
            or type(self.next_event_index) is not int
            or type(self.planned_count) is not int
            or type(self.settled_event_count) is not int
            or not 0 <= self.next_event_index <= self.planned_count
            or self.planned_count < 1
            or self.settled_event_count != self.next_event_index
            or (
                self.emulation_state is not None
                and type(self.emulation_state) is not PlannedEmulationState
            )
            or type(self.possibly_held) is not tuple
            or any(
                type(token) is not tuple
                or len(token) != 2
                or type(token[0]) is not int
                or token[0] <= 1
                or type(token[1]) is not KeyAction
                for token in self.possibly_held
            )
            or self.session_state not in {state.name for state in _GdbSessionState}
            or type(self.recovery_required) is not bool
        ):
            raise AutomationRefusal("GDB input-pulse coordinator status is malformed.")
        if self.state == "NEW" and (
            self.bound_process is not None
            or self.next_event_index != 0
            or self.emulation_state is not None
            or self.possibly_held
            or self.session_state != "NEW"
            or self.recovery_required
        ):
            raise AutomationRefusal("Fresh GDB input-pulse status is inconsistent.")
        if self.state in {"STARTED", "COMPLETE", "CLOSED"} and (
            self.bound_process is None or self.possibly_held
        ):
            raise AutomationRefusal("Settled GDB input-pulse status is inconsistent.")
        if self.state in {"COMPLETE", "CLOSED"} and (
            self.next_event_index != self.planned_count
            or self.emulation_state is not PlannedEmulationState.RUNNING
        ):
            raise AutomationRefusal("Terminal GDB input-pulse status is inconsistent.")
        if self.recovery_required != (self.state == "POISONED"):
            raise AutomationRefusal(
                "GDB input-pulse recovery requirement is inconsistent."
            )
        expected_session_state = {
            PlannedEmulationState.RUNNING: "RUNNING",
            PlannedEmulationState.PAUSED: "PAUSED_BY_PLAN",
        }.get(self.emulation_state)
        if self.state in {"STARTED", "COMPLETE"} and (
            expected_session_state is None
            or self.session_state != expected_session_state
        ):
            raise AutomationRefusal(
                "Settled GDB input-pulse/session states are inconsistent."
            )
        if self.state == "CLOSED" and self.session_state != "CLOSED":
            raise AutomationRefusal("Closed GDB input-pulse status is inconsistent.")


class GdbInputPulseLifecycleCoordinator:
    """Offline-only owner of arm -> host pulse -> GDB confirmation ordering.

    Construction performs pure dependency/state validation followed by one
    in-memory capability claim; it performs no process or native I/O.  This
    adapter deliberately is not referenced by :func:`main` or
    :class:`UnattendedFoundation`.  It exists so the cross-domain lifecycle can
    be proved with fake sockets and a fake event backend before any production
    coordinator is permitted to use it.

    A poisoned status deliberately has no general close/cancel operation.  If
    its explicit emulation state is PAUSED or UNKNOWN, disconnecting could
    strand DuckStation in the wrong state.  The session state and
    ``recovery_required`` bit preserve that promotion blocker for a later
    recovery-capable outer coordinator.  Construction consumes the pristine
    key transport's single mutation capability, so retained aliases of that
    transport refuse.  A retained alias of the underlying EventBackend remains
    an explicit production blocker until an outer owner constructs it privately.
    Recovery/cancellation and one native-boundary-aware absolute deadline are
    also deliberately absent, so this adapter remains offline-only.
    """

    def __init__(
        self,
        plan: InputPlan,
        session: PersistentDuckStationGdbSession,
        transport: TargetedKeyTransport,
        *,
        clock: Callable[[], float] = time.monotonic,
        pulse_timeout_seconds: float = GDB_SPACE_PULSE_ACCEPTANCE_TIMEOUT_SECONDS,
    ) -> None:
        lifecycle = validate_gdb_session_input_lifecycle(plan)
        if (
            type(session) is not PersistentDuckStationGdbSession
            or type(transport) is not TargetedKeyTransport
            or not callable(clock)
            or type(pulse_timeout_seconds) not in {int, float}
            or not math.isfinite(float(pulse_timeout_seconds))
            or pulse_timeout_seconds <= 0
        ):
            raise AutomationRefusal(
                "GDB input-pulse coordinator dependencies are not exact."
            )
        transport_status = transport.status()
        if transport_status != TargetedKeyTransportStatus(False, 0, 0, ()):
            raise AutomationRefusal(
                "GDB input-pulse coordinator requires one pristine key transport."
            )
        session_status = session.status()
        if session_status.state != "NEW":
            raise AutomationRefusal(
                "GDB input-pulse coordinator requires one fresh NEW session."
            )
        self._plan = plan
        self._lifecycle = lifecycle
        self._session = session
        self._clock = clock
        self._pulse_timeout_seconds = float(pulse_timeout_seconds)
        self._state = _GdbInputPulseCoordinatorState.NEW
        self._bound_process: ProcessIdentity | None = None
        self._next_event_index = 0
        self._settled_event_count = 0
        self._emulation_state: PlannedEmulationState | None = None
        self._state_guard = threading.Lock()
        # Caller-supplied validators/observers may inspect status.  An RLock
        # permits that same-thread read without allowing a second pulse to pass
        # the explicit PULSE_IN_FLIGHT state checks.  Cross-thread status still
        # waits for one atomic settled snapshot.
        self._operation_lock = threading.RLock()
        self._mutation_active = False
        # Claim last: every constructor refusal above leaves the caller's
        # transport pristine and directly usable.  A successful coordinator
        # owns the only transport mutation route for its full lifetime,
        # including CLOSED and POISONED terminal states.
        self._transport_capability = transport.claim_exclusive_lifecycle()

    def status(self) -> GdbInputPulseCoordinatorStatus:
        """Return one atomic status without process, lsof, socket, or post work."""

        with self._operation_lock:
            session_status = self._session.status()
            with self._state_guard:
                return GdbInputPulseCoordinatorStatus(
                    self._state.name,
                    self._bound_process,
                    self._next_event_index,
                    len(self._plan.events),
                    self._settled_event_count,
                    self._emulation_state,
                    self._transport_capability.possibly_held,
                    session_status.state,
                    self._state is _GdbInputPulseCoordinatorState.POISONED,
                )

    def _begin_mutation(self, label: str) -> None:
        if label not in {"start", "event", "completion", "close"}:
            raise AutomationRefusal("GDB input-pulse mutation label is malformed.")
        if not self._operation_lock.acquire(blocking=False):
            raise AutomationRefusal(
                f"Concurrent GDB input-pulse {label} is forbidden."
            )
        with self._state_guard:
            if self._mutation_active:
                self._operation_lock.release()
                raise AutomationRefusal(
                    f"Reentrant GDB input-pulse {label} is forbidden."
                )
            self._mutation_active = True

    def _end_mutation(self) -> None:
        with self._state_guard:
            if not self._mutation_active:
                self._operation_lock.release()
                raise AutomationRefusal(
                    "GDB input-pulse mutation ownership was lost."
                )
            self._mutation_active = False
        self._operation_lock.release()

    def _clock_value(self) -> float:
        value = self._clock()
        if (
            type(value) not in {int, float}
            or not math.isfinite(float(value))
            or value < 0
        ):
            raise AutomationRefusal("GDB input-pulse coordinator clock is malformed.")
        return float(value)

    def _require_pulse_deadline(self, started_at: float) -> None:
        observed = self._clock_value()
        if observed < started_at:
            raise AutomationRefusal("GDB input-pulse coordinator clock moved backward.")
        if observed - started_at > self._pulse_timeout_seconds:
            raise AutomationRefusal(
                "GDB input-pulse transition exceeded its absolute acceptance deadline."
            )

    def _set_poisoned(self) -> None:
        with self._state_guard:
            self._state = _GdbInputPulseCoordinatorState.POISONED

    def _raise_poisoned(
        self,
        exc: BaseException,
        *,
        cleanup_errors: Sequence[str] = (),
    ) -> None:
        self._set_poisoned()
        held = self._transport_capability.possibly_held
        if held:
            details = ",".join(cleanup_errors) if cleanup_errors else "none"
            raise AutomationRefusal(
                "GDB input-pulse work failed and mandatory key-up cleanup "
                f"remains incomplete (errors={details};held={held!r})."
            ) from exc
        # Historical cleanup errors are resolved if a later bounded attempt
        # removed the token.  Preserve the original refusal without falsely
        # claiming that a physical key remains held.
        if isinstance(exc, AutomationRefusal):
            raise exc
        raise AutomationRefusal("GDB input-pulse coordinator failed closed.") from exc

    def _release_owed_keys(self) -> tuple[str, ...]:
        errors: list[str] = []
        for _ in range(GDB_PULSE_RELEASE_CLEANUP_ATTEMPTS):
            if not self._transport_capability.possibly_held:
                break
            errors.extend(self._transport_capability.release_all())
        return tuple(errors)

    def start_execute(self, process: ProcessIdentity) -> None:
        """Create/resume the one GDB session before any host pulse."""

        self._begin_mutation("start")
        try:
            try:
                with self._state_guard:
                    if (
                        type(process) is not ProcessIdentity
                        or self._state is not _GdbInputPulseCoordinatorState.NEW
                        or self._bound_process is not None
                        or self._next_event_index != 0
                        or self._transport_capability.possibly_held
                    ):
                        raise AutomationRefusal(
                            "GDB input-pulse coordinator can start exactly once."
                        )
                # Accessibility and exact frontmost-PID proof must fail before
                # the GDB connection can induce any pause.
                self._transport_capability.require_target(process.pid)
                self._session.connect_resume_once(process)
                self._session.require_running_owned(process)
                with self._state_guard:
                    if self._state is not _GdbInputPulseCoordinatorState.NEW:
                        raise AutomationRefusal(
                            "GDB input-pulse coordinator start ownership changed."
                        )
                    self._bound_process = process
                    self._emulation_state = PlannedEmulationState.RUNNING
                    self._state = _GdbInputPulseCoordinatorState.STARTED
            except BaseException as exc:
                self._raise_poisoned(exc)
        finally:
            self._end_mutation()

    def execute_next(
        self,
        event: InputEvent,
        process: ProcessIdentity,
        *,
        release_identity_validator: Callable[[], None],
        pulse_ownership_validator: Callable[[], None],
        post_release_drift_observer: Callable[[], None],
    ) -> None:
        """Own one event; settle its GDB state only after resolved key-up.

        Non-Space events prove only that the shared session remained RUNNING.
        Pad-bit confirmation and macro compensation remain unavailable in the
        production foundation and are not claimed by this counter.
        """

        self._begin_mutation("event")
        try:
            index: int | None = None
            session_boundary_crossed = False
            try:
                if (
                    type(event) is not InputEvent
                    or type(process) is not ProcessIdentity
                    or not callable(release_identity_validator)
                    or not callable(pulse_ownership_validator)
                    or not callable(post_release_drift_observer)
                ):
                    raise AutomationRefusal(
                        "GDB input-pulse event or callback contract is malformed."
                    )
                with self._state_guard:
                    index = self._next_event_index
                    if (
                        self._state is not _GdbInputPulseCoordinatorState.STARTED
                        or self._bound_process != process
                        or index >= len(self._plan.events)
                        or self._plan.events[index] != event
                        or self._settled_event_count != index
                        or self._transport_capability.possibly_held
                    ):
                        raise AutomationRefusal(
                            "GDB input-pulse event order or ownership is not exact."
                        )
                    before_state = (
                        PlannedEmulationState.RUNNING
                        if index == 0
                        else self._lifecycle.states_after_events[index - 1]
                    )
                    after_state = self._lifecycle.states_after_events[index]
                    if self._emulation_state is not before_state:
                        raise AutomationRefusal(
                            "GDB input-pulse emulation baseline is not exact."
                        )

                pulse_started_at = self._clock_value()
                self._require_pulse_deadline(pulse_started_at)

                if event.action is KeyAction.TOGGLE_PAUSE:
                    if (
                        before_state is PlannedEmulationState.RUNNING
                        and after_state is PlannedEmulationState.PAUSED
                    ):
                        arm_transition = self._session.arm_planned_pause
                        require_armed = self._session.require_planned_pause_armed
                        confirm_transition = self._session.confirm_planned_pause
                        require_after = self._session.require_paused_owned
                    elif (
                        before_state is PlannedEmulationState.PAUSED
                        and after_state is PlannedEmulationState.RUNNING
                    ):
                        arm_transition = self._session.arm_planned_resume
                        require_armed = self._session.require_planned_resume_armed
                        confirm_transition = self._session.confirm_planned_resume
                        require_after = self._session.require_running_owned
                    else:
                        raise AutomationRefusal(
                            "GDB input-pulse TogglePause transition is malformed."
                        )
                    with self._state_guard:
                        self._emulation_state = None
                    session_boundary_crossed = True
                    arm_transition(process)
                else:
                    if (
                        before_state is not PlannedEmulationState.RUNNING
                        or after_state is not PlannedEmulationState.RUNNING
                    ):
                        raise AutomationRefusal(
                            "Non-toggle input cannot execute while DuckStation is paused."
                        )
                    with self._state_guard:
                        self._emulation_state = None
                    session_boundary_crossed = True
                    self._session.require_running_owned(process)
                    with self._state_guard:
                        if self._state is not _GdbInputPulseCoordinatorState.STARTED:
                            raise AutomationRefusal(
                                "GDB input-pulse ownership changed during RUNNING proof."
                            )
                        self._emulation_state = PlannedEmulationState.RUNNING
                    require_armed = self._session.require_running_owned
                    confirm_transition = self._session.require_running_owned
                    require_after = self._session.require_running_owned

                with self._state_guard:
                    if self._state is not _GdbInputPulseCoordinatorState.STARTED:
                        raise AutomationRefusal(
                            "GDB input-pulse ownership changed before host input."
                        )
                    self._state = _GdbInputPulseCoordinatorState.PULSE_IN_FLIGHT

                def validate_pulse_ownership() -> None:
                    pulse_ownership_validator()
                    require_armed(process)
                    self._require_pulse_deadline(pulse_started_at)

                def confirm_after_resolved_key_up() -> None:
                    with self._state_guard:
                        if (
                            self._state
                            is not _GdbInputPulseCoordinatorState.PULSE_IN_FLIGHT
                            or self._next_event_index != index
                            or self._settled_event_count != index
                        ):
                            raise AutomationRefusal(
                                "GDB input-pulse confirmation ownership changed."
                            )
                    confirm_transition(process)
                    require_after(process)
                    with self._state_guard:
                        if (
                            self._state
                            is not _GdbInputPulseCoordinatorState.PULSE_IN_FLIGHT
                            or self._next_event_index != index
                            or self._settled_event_count != index
                        ):
                            raise AutomationRefusal(
                                "GDB input-pulse confirmation was duplicated."
                            )
                        self._next_event_index = index + 1
                        self._settled_event_count = index + 1
                        self._emulation_state = after_state
                    deadline_error: BaseException | None = None
                    try:
                        self._require_pulse_deadline(pulse_started_at)
                    except BaseException as exc:
                        deadline_error = exc
                    try:
                        result = post_release_drift_observer()
                        if result is not None:
                            raise AutomationRefusal(
                                "GDB post-release drift observer returned a value."
                            )
                    except BaseException as exc:
                        if deadline_error is not None:
                            raise AutomationRefusal(
                                "GDB input pulse was late and post-release drifted."
                            ) from exc
                        raise
                    if deadline_error is not None:
                        raise deadline_error

                self._transport_capability.tap(
                    process.pid,
                    event.action,
                    release_identity_validator=release_identity_validator,
                    post_release_drift_observer=confirm_after_resolved_key_up,
                    pre_key_down_validator=validate_pulse_ownership,
                )
                self._require_pulse_deadline(pulse_started_at)
                with self._state_guard:
                    if (
                        self._state
                        is not _GdbInputPulseCoordinatorState.PULSE_IN_FLIGHT
                        or self._next_event_index != index + 1
                        or self._settled_event_count != index + 1
                        or self._emulation_state is not after_state
                        or self._transport_capability.possibly_held
                    ):
                        raise AutomationRefusal(
                            "GDB input-pulse event did not settle exactly once."
                        )
                    self._state = (
                        _GdbInputPulseCoordinatorState.COMPLETE
                        if self._next_event_index == len(self._plan.events)
                        else _GdbInputPulseCoordinatorState.STARTED
                    )
            except BaseException as exc:
                cleanup_errors = self._release_owed_keys()
                with self._state_guard:
                    if (
                        session_boundary_crossed
                        and index is not None
                        and self._settled_event_count == index
                    ):
                        self._emulation_state = None
                self._raise_poisoned(exc, cleanup_errors=cleanup_errors)
        finally:
            self._end_mutation()

    def _require_complete_locked(self, process: ProcessIdentity) -> None:
        with self._state_guard:
            if (
                type(process) is not ProcessIdentity
                or self._state is not _GdbInputPulseCoordinatorState.COMPLETE
                or self._bound_process != process
                or self._next_event_index != len(self._plan.events)
                or self._settled_event_count != len(self._plan.events)
                or self._emulation_state is not PlannedEmulationState.RUNNING
                or self._transport_capability.possibly_held
            ):
                raise AutomationRefusal(
                    "GDB input-pulse coordinator is not terminal RUNNING."
                )
            # Reattestation can poison the underlying session.  Clear the
            # coordinator's claim until the check returns successfully.
            self._emulation_state = None
        self._session.require_running_owned(process)
        with self._state_guard:
            if (
                self._state is not _GdbInputPulseCoordinatorState.COMPLETE
                or self._bound_process != process
                or self._next_event_index != len(self._plan.events)
                or self._settled_event_count != len(self._plan.events)
                or self._transport_capability.possibly_held
            ):
                raise AutomationRefusal(
                    "GDB input-pulse completion ownership changed."
                )
            self._emulation_state = PlannedEmulationState.RUNNING

    def require_complete(self, process: ProcessIdentity) -> None:
        """Reattest exact terminal RUNNING state after every event settled."""

        self._begin_mutation("completion")
        try:
            try:
                self._require_complete_locked(process)
            except BaseException as exc:
                self._raise_poisoned(exc)
        finally:
            self._end_mutation()

    def close(self, process: ProcessIdentity) -> None:
        """Close only after exact terminal RUNNING; paused/armed close is absent."""

        self._begin_mutation("close")
        try:
            with self._state_guard:
                if self._state is _GdbInputPulseCoordinatorState.CLOSED:
                    if self._bound_process != process:
                        raise AutomationRefusal(
                            "Closed GDB input-pulse process binding changed."
                        )
                    return
            try:
                self._require_complete_locked(process)
                with self._state_guard:
                    self._emulation_state = None
                self._session.close()
                with self._state_guard:
                    if self._state is not _GdbInputPulseCoordinatorState.COMPLETE:
                        raise AutomationRefusal(
                            "GDB input-pulse close ownership changed."
                        )
                    self._emulation_state = PlannedEmulationState.RUNNING
                    self._state = _GdbInputPulseCoordinatorState.CLOSED
            except BaseException as exc:
                self._raise_poisoned(exc)
        finally:
            self._end_mutation()


@dataclasses.dataclass(frozen=True)
class PadConfirmationReadiness:
    source_id: str
    ready: bool
    refusal_reason: str | None

    def __post_init__(self) -> None:
        if not self.source_id or type(self.ready) is not bool:
            raise AutomationRefusal("Pad confirmation readiness is malformed.")
        if self.ready and self.refusal_reason is not None:
            raise AutomationRefusal("Ready pad confirmation cannot carry a refusal reason.")
        if not self.ready and not self.refusal_reason:
            raise AutomationRefusal("Unavailable pad confirmation requires a refusal reason.")


class PadConfirmer(Protocol):
    def readiness(self) -> PadConfirmationReadiness: ...
    def preflight(
        self, events: Sequence[InputEvent], process: ProcessIdentity
    ) -> None: ...
    def confirm_initial_released(
        self, actions: Sequence[KeyAction], process: ProcessIdentity
    ) -> None: ...
    def observe_state(
        self, action: KeyAction, process: ProcessIdentity
    ) -> PadState: ...
    def confirm(self, event: InputEvent, process: ProcessIdentity) -> None: ...


class PollingReadOnlyPadConfirmer:
    """Confirmation policy over a caller-supplied read-only GDB word reader."""

    def __init__(
        self,
        reader: PadWordReader,
        *,
        clock: Callable[[], float] = time.monotonic,
        sleeper: Callable[[float], None] = time.sleep,
        timeout_seconds: float = PAD_CONFIRMATION_TIMEOUT_SECONDS,
        interval_seconds: float = PAD_CONFIRMATION_INTERVAL_SECONDS,
        stable_samples: int = PAD_CONFIRMATION_STABLE_SAMPLES,
    ) -> None:
        if timeout_seconds <= 0 or interval_seconds <= 0 or stable_samples < 1:
            raise AutomationRefusal("Pad confirmation timing policy is invalid.")
        self.reader = reader
        self.clock = clock
        self.sleeper = sleeper
        self.timeout_seconds = timeout_seconds
        self.interval_seconds = interval_seconds
        self.stable_samples = stable_samples

    def readiness(self) -> PadConfirmationReadiness:
        return PadConfirmationReadiness(
            "read-only-gdb-pad-word-reader", True, None
        )

    def _read_word_once(self, process: ProcessIdentity, context: str) -> int:
        try:
            word = self.reader.read_pad_word(process)
        except BaseException as exc:
            raise AutomationRefusal(
                f"Read-only GDB pad reader failed during {context}."
            ) from exc
        if type(word) is not int or not 0 <= word <= 0xFFFF:
            raise AutomationRefusal("Read-only GDB pad reader returned an invalid word.")
        return word

    def preflight(
        self, events: Sequence[InputEvent], process: ProcessIdentity
    ) -> None:
        if not events:
            raise AutomationRefusal("Pad-reader preflight received an empty input plan.")
        # One exact process-bound read proves the ready reader is live for every
        # plan, including plans made entirely of non-pad/hotkey actions.
        self._read_word_once(process, "process-bound preflight probe")

    def confirm_initial_released(
        self, actions: Sequence[KeyAction], process: ProcessIdentity
    ) -> None:
        for action in actions:
            if self.observe_state(action, process) is not PadState.RELEASED:
                raise AutomationRefusal(
                    f"Macro-toggle {action.value} initial pad baseline is not RELEASED."
                )

    def observe_state(
        self, action: KeyAction, process: ProcessIdentity
    ) -> PadState:
        bit = ACTION_SPECS[action].pad_bit
        if bit is None:
            raise AutomationRefusal("Cannot observe pad state for a non-pad action.")
        deadline = self.clock() + self.timeout_seconds
        stable_state: PadState | None = None
        stable = 0
        while self.clock() < deadline:
            word = self._read_word_once(process, "pad state observation")
            observed = (
                PadState.HELD if (word & (1 << bit)) == 0 else PadState.RELEASED
            )
            if observed is stable_state:
                stable += 1
            else:
                stable_state = observed
                stable = 1
            if stable >= self.stable_samples:
                return observed
            self.sleeper(self.interval_seconds)
        raise AutomationRefusal("Read-only GDB pad state observation timed out.")

    def confirm(self, event: InputEvent, process: ProcessIdentity) -> None:
        bit = ACTION_SPECS[event.action].pad_bit
        if bit is None:
            if event.expected_pad_state is not PadState.NOT_APPLICABLE:
                raise AutomationRefusal("Non-pad action reached pad confirmation.")
            return
        desired_held = event.expected_pad_state is PadState.HELD
        deadline = self.clock() + self.timeout_seconds
        stable = 0
        while self.clock() < deadline:
            word = self._read_word_once(process, "held/released confirmation")
            observed_held = (word & (1 << bit)) == 0
            if observed_held == desired_held:
                stable += 1
                if stable >= self.stable_samples:
                    return
            else:
                stable = 0
            self.sleeper(self.interval_seconds)
        raise AutomationRefusal("Read-only GDB pad held/released confirmation timed out.")


class UnavailableNativePadConfirmer:
    """Production refusal until the candidate-native pad evidence is pinned."""

    REFUSAL_REASON: Final = (
        "Native read-only GDB confirmation is not production-wired because "
        "candidate provenance for the pad-frame address, update timing, and bit "
        "mapping plus shared-session deadlines and macro compensation is not "
        "pinned; the entire input plan is refused before any host key event."
    )

    def readiness(self) -> PadConfirmationReadiness:
        return PadConfirmationReadiness(
            "unavailable-native-gdb-pad-word-reader", False, self.REFUSAL_REASON
        )

    def preflight(
        self, events: Sequence[InputEvent], process: ProcessIdentity
    ) -> None:
        del events, process
        raise AutomationRefusal(self.REFUSAL_REASON)

    def confirm_initial_released(
        self, actions: Sequence[KeyAction], process: ProcessIdentity
    ) -> None:
        del actions, process
        raise AutomationRefusal(self.REFUSAL_REASON)

    def observe_state(
        self, action: KeyAction, process: ProcessIdentity
    ) -> PadState:
        del action, process
        raise AutomationRefusal(self.REFUSAL_REASON)

    def confirm(self, event: InputEvent, process: ProcessIdentity) -> None:
        del event, process
        raise AutomationRefusal("Native read-only GDB pad confirmation is unavailable.")


@dataclasses.dataclass(frozen=True)
class RuntimeSessionAttestationReadiness:
    attestor_id: str
    ready: bool
    refusal_reason: str | None

    def __post_init__(self) -> None:
        if not self.attestor_id or type(self.ready) is not bool:
            raise AutomationRefusal("Runtime-session attestor readiness is malformed.")
        if self.ready and self.refusal_reason is not None:
            raise AutomationRefusal("Ready runtime attestor cannot carry a refusal reason.")
        if not self.ready and not self.refusal_reason:
            raise AutomationRefusal("Unavailable runtime attestor requires a refusal reason.")


class RuntimeSessionAttestor(Protocol):
    def readiness(self) -> RuntimeSessionAttestationReadiness: ...
    def attest(self, profile: VerifiedProfile, process: ProcessIdentity) -> None: ...


class UnavailableRuntimeSessionAttestor:
    """Production refusal until the full live session closure is implemented."""

    REFUSAL_REASON: Final = (
        "Runtime session attestation is unavailable: exact argv, open candidate "
        "CUE/BIN descriptors, and owned GDB sentinels are not implemented."
    )

    def readiness(self) -> RuntimeSessionAttestationReadiness:
        return RuntimeSessionAttestationReadiness(
            "unavailable-runtime-session-attestor", False, self.REFUSAL_REASON
        )

    def attest(self, profile: VerifiedProfile, process: ProcessIdentity) -> None:
        del profile, process
        raise AutomationRefusal(self.REFUSAL_REASON)


def _open_secure_directory(path: pathlib.Path) -> int:
    """Open every absolute directory component with O_NOFOLLOW."""

    absolute = pathlib.Path(os.path.abspath(path))
    descriptor = os.open(
        absolute.anchor,
        os.O_RDONLY
        | getattr(os, "O_CLOEXEC", 0)
        | getattr(os, "O_DIRECTORY", 0)
        | getattr(os, "O_NOFOLLOW", 0),
    )
    try:
        for part in absolute.parts[1:]:
            child = os.open(
                part,
                os.O_RDONLY
                | getattr(os, "O_CLOEXEC", 0)
                | getattr(os, "O_DIRECTORY", 0)
                | getattr(os, "O_NOFOLLOW", 0),
                dir_fd=descriptor,
            )
            info = os.fstat(child)
            if not stat.S_ISDIR(info.st_mode):
                os.close(child)
                raise AutomationRefusal("Secure directory path changed type.")
            os.close(descriptor)
            descriptor = child
        return descriptor
    except BaseException:
        os.close(descriptor)
        raise


class SharedCaptureCoordinatorLease:
    """The manual V3 coordinator's stable cross-tool authority boundary."""

    def __init__(
        self, repository_root: pathlib.Path, *, expected_uid: int | None = None
    ) -> None:
        self.repository_root = pathlib.Path(os.path.abspath(repository_root))
        self.expected_uid = os.getuid() if expected_uid is None else expected_uid
        if type(self.expected_uid) is not int or self.expected_uid < 0:
            raise AutomationRefusal("Capture coordinator lease UID is invalid.")
        self.authority_parent_fd = -1
        self.authority_fd = -1
        self.authority_locked = False
        self.authority_parent_identity: tuple[int, int, int, int] | None = None
        self.authority_identity: tuple[int, int, int, int, int] | None = None
        self.prefix_fd = -1
        self.fd = -1
        self.staging_locked = False
        self.prefix_identity: tuple[int, int, int, int] | None = None
        self.lock_identity: tuple[int, int, int, int, int] | None = None

    @staticmethod
    def _directory_identity(info: os.stat_result) -> tuple[int, int, int, int]:
        return info.st_dev, info.st_ino, info.st_mode, info.st_uid

    @staticmethod
    def _file_identity(info: os.stat_result) -> tuple[int, int, int, int, int]:
        return (
            info.st_dev, info.st_ino, info.st_mode, info.st_uid, info.st_nlink
        )

    def _require_private_parent(self, info: os.stat_result) -> None:
        if (
            not stat.S_ISDIR(info.st_mode)
            or info.st_uid != self.expected_uid
            or stat.S_IMODE(info.st_mode) != 0o700
        ):
            raise AutomationRefusal(
                "Capture coordinator authority parent is not owned mode 0700."
            )

    def _require_lock_inode(self, info: os.stat_result, label: str) -> None:
        if (
            not stat.S_ISREG(info.st_mode)
            or info.st_nlink != 1
            or info.st_uid != self.expected_uid
            or stat.S_IMODE(info.st_mode) != 0o600
        ):
            raise AutomationRefusal(f"{label} inode is unsafe.")

    def _ensure_prefix(self) -> int:
        descriptor = _open_secure_directory(self.repository_root)
        try:
            for index, part in enumerate(CAPTURE_STAGING_PREFIX.split("/")):
                if not part or part in {".", ".."}:
                    raise AutomationRefusal("Capture-staging prefix is malformed.")
                try:
                    child = os.open(
                        part,
                        os.O_RDONLY
                        | getattr(os, "O_CLOEXEC", 0)
                        | getattr(os, "O_DIRECTORY", 0)
                        | getattr(os, "O_NOFOLLOW", 0),
                        dir_fd=descriptor,
                    )
                except FileNotFoundError:
                    os.mkdir(part, 0o700, dir_fd=descriptor)
                    child = os.open(
                        part,
                        os.O_RDONLY
                        | getattr(os, "O_CLOEXEC", 0)
                        | getattr(os, "O_DIRECTORY", 0)
                        | getattr(os, "O_NOFOLLOW", 0),
                        dir_fd=descriptor,
                    )
                info = os.fstat(child)
                if not stat.S_ISDIR(info.st_mode):
                    os.close(child)
                    raise AutomationRefusal("Capture-staging ancestry changed type.")
                if index == len(CAPTURE_STAGING_PREFIX.split("/")) - 1 and (
                    info.st_uid != self.expected_uid
                    or stat.S_IMODE(info.st_mode) != 0o700
                ):
                    os.close(child)
                    raise AutomationRefusal(
                        "Capture-staging root is not owned mode 0700."
                    )
                os.close(descriptor)
                descriptor = child
            return descriptor
        except BaseException:
            os.close(descriptor)
            raise

    def __enter__(self) -> "SharedCaptureCoordinatorLease":
        if self.authority_fd >= 0 or self.fd >= 0:
            raise AutomationRefusal("Capture coordinator lease is already entered.")
        self.prefix_fd = self._ensure_prefix()
        try:
            self.authority_parent_fd = _open_secure_directory(
                self.repository_root / CAPTURE_COORDINATOR_AUTHORITY_DIRECTORY
            )
            authority_parent = os.fstat(self.authority_parent_fd)
            self._require_private_parent(authority_parent)
            self.authority_parent_identity = self._directory_identity(
                authority_parent
            )
            self.authority_fd = os.open(
                CAPTURE_COORDINATOR_AUTHORITY_NAME,
                os.O_RDWR
                | os.O_CREAT
                | getattr(os, "O_CLOEXEC", 0)
                | getattr(os, "O_NOFOLLOW", 0),
                0o600,
                dir_fd=self.authority_parent_fd,
            )
            authority = os.fstat(self.authority_fd)
            self._require_lock_inode(
                authority, "Capture coordinator stable authority"
            )
            try:
                fcntl.flock(
                    self.authority_fd, fcntl.LOCK_EX | fcntl.LOCK_NB
                )
            except BlockingIOError:
                raise AutomationRefusal(
                    "Another capture coordinator already owns the lease."
                ) from None
            self.authority_locked = True
            self.authority_identity = self._file_identity(authority)
            self._assert_stable_authority_current()

            self.fd = os.open(
                CAPTURE_COORDINATOR_STAGING_LOCK_NAME,
                os.O_RDWR
                | os.O_CREAT
                | getattr(os, "O_CLOEXEC", 0)
                | getattr(os, "O_NOFOLLOW", 0),
                0o600,
                dir_fd=self.prefix_fd,
            )
            info = os.fstat(self.fd)
            self._require_lock_inode(info, "Capture coordinator lease")
            try:
                fcntl.flock(self.fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
            except BlockingIOError:
                raise AutomationRefusal(
                    "Another capture coordinator already owns the lease."
                ) from None
            self.staging_locked = True
            prefix = os.fstat(self.prefix_fd)
            self.prefix_identity = self._directory_identity(prefix)
            self.lock_identity = self._file_identity(info)
            self.assert_current()
            return self
        except BaseException as exc:
            try:
                self._close_all(validate=False)
            except BaseException as cleanup:
                raise AutomationRefusal(
                    "Capture coordinator lease setup cleanup failed."
                ) from cleanup
            if isinstance(exc, AutomationRefusal):
                raise
            raise AutomationRefusal(
                "Capture coordinator lease cannot be opened safely."
            ) from exc

    def _assert_stable_authority_current(self) -> None:
        if (
            self.authority_parent_fd < 0
            or self.authority_fd < 0
            or self.authority_parent_identity is None
            or self.authority_identity is None
        ):
            raise AutomationRefusal(
                "Capture coordinator stable authority is not active."
            )
        retained_parent = os.fstat(self.authority_parent_fd)
        self._require_private_parent(retained_parent)
        current_parent_fd = _open_secure_directory(
            self.repository_root / CAPTURE_COORDINATOR_AUTHORITY_DIRECTORY
        )
        try:
            current_parent = os.fstat(current_parent_fd)
            self._require_private_parent(current_parent)
        finally:
            os.close(current_parent_fd)
        if (
            self._directory_identity(retained_parent)
            != self.authority_parent_identity
            or self._directory_identity(current_parent)
            != self.authority_parent_identity
        ):
            raise AutomationRefusal(
                "Capture coordinator stable authority parent was rebound."
            )
        retained = os.fstat(self.authority_fd)
        current = os.stat(
            CAPTURE_COORDINATOR_AUTHORITY_NAME,
            dir_fd=self.authority_parent_fd,
            follow_symlinks=False,
        )
        self._require_lock_inode(retained, "Capture coordinator stable authority")
        self._require_lock_inode(current, "Capture coordinator stable authority")
        if (
            self._file_identity(retained) != self.authority_identity
            or self._file_identity(current) != self.authority_identity
        ):
            raise AutomationRefusal(
                "Capture coordinator stable authority path was rebound."
            )

    def assert_current(self) -> None:
        self._assert_stable_authority_current()
        if (
            self.fd < 0
            or self.prefix_fd < 0
            or self.prefix_identity is None
            or self.lock_identity is None
        ):
            raise AutomationRefusal("Capture coordinator lease is not active.")
        prefix = os.fstat(self.prefix_fd)
        current_fd = _open_secure_directory(
            self.repository_root / CAPTURE_STAGING_PREFIX
        )
        try:
            current_prefix = os.fstat(current_fd)
        finally:
            os.close(current_fd)
        if (
            self._directory_identity(prefix) != self.prefix_identity
            or self._directory_identity(current_prefix) != self.prefix_identity
        ):
            raise AutomationRefusal("Capture-staging lease directory was rebound.")
        lock = os.fstat(self.fd)
        lock_path = os.stat(
            CAPTURE_COORDINATOR_STAGING_LOCK_NAME,
            dir_fd=self.prefix_fd,
            follow_symlinks=False,
        )
        self._require_lock_inode(lock, "Capture coordinator lease")
        self._require_lock_inode(lock_path, "Capture coordinator lease")
        if (
            self._file_identity(lock) != self.lock_identity
            or self._file_identity(lock_path) != self.lock_identity
        ):
            raise AutomationRefusal("Capture coordinator lease path was rebound.")

    def _close_all(self, *, validate: bool) -> None:
        first_error: BaseException | None = None
        if validate and self.authority_fd >= 0:
            try:
                self.assert_current()
            except BaseException as error:
                first_error = error
        for descriptor, locked in (
            (self.fd, self.staging_locked),
            (self.authority_fd, self.authority_locked),
        ):
            if descriptor < 0 or not locked:
                continue
            try:
                fcntl.flock(descriptor, fcntl.LOCK_UN)
            except BaseException as error:
                if first_error is None:
                    first_error = error
        for descriptor in (
            self.fd,
            self.prefix_fd,
            self.authority_fd,
            self.authority_parent_fd,
        ):
            if descriptor < 0:
                continue
            try:
                os.close(descriptor)
            except BaseException as error:
                if first_error is None:
                    first_error = error
        self.fd = -1
        self.prefix_fd = -1
        self.authority_fd = -1
        self.authority_parent_fd = -1
        self.staging_locked = False
        self.authority_locked = False
        if first_error is not None:
            raise first_error

    def __exit__(self, exc_type: Any, exc: Any, traceback: Any) -> None:
        del exc_type, exc, traceback
        self._close_all(validate=True)


class _DurableNamespaceAuthority:
    """Persistent parent-anchored identity and flock for one state-root inode."""

    def __init__(self, state_root: pathlib.Path, expected_uid: int) -> None:
        self.state_root = state_root
        self.expected_uid = expected_uid
        suffix = hashlib.sha256(str(state_root).encode("utf-8", "strict")).hexdigest()[:32]
        self.name = f".id65-v3-unattended-authority-{suffix}.lock"
        self.parent_fd = -1
        self.fd = -1
        self.file_identity: tuple[int, int, int, int, int] | None = None
        self.root_identity: tuple[int, int, int, int] | None = None

    def __enter__(self) -> "_DurableNamespaceAuthority":
        self.parent_fd = _open_secure_directory(self.state_root.parent)
        try:
            try:
                fcntl.flock(self.parent_fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
            except BlockingIOError:
                raise AutomationRefusal(
                    "Another unattended namespace authority is active."
                ) from None
            self.fd = os.open(
                self.name,
                os.O_RDWR | os.O_CREAT | getattr(os, "O_CLOEXEC", 0)
                | getattr(os, "O_NOFOLLOW", 0),
                0o600,
                dir_fd=self.parent_fd,
            )
            info = os.fstat(self.fd)
            self._require_file(info)
            try:
                fcntl.flock(self.fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
            except BlockingIOError:
                raise AutomationRefusal(
                    "Another unattended namespace authority is active."
                ) from None
            path_info = os.stat(
                self.name, dir_fd=self.parent_fd, follow_symlinks=False
            )
            self._require_file(path_info)
            self.file_identity = (
                info.st_dev, info.st_ino, info.st_mode, info.st_uid, info.st_nlink
            )
            if self.file_identity != (
                path_info.st_dev, path_info.st_ino, path_info.st_mode,
                path_info.st_uid, path_info.st_nlink,
            ):
                raise AutomationRefusal("Unattended namespace authority path was rebound.")
            return self
        except BaseException:
            if self.fd >= 0:
                os.close(self.fd)
                self.fd = -1
            if self.parent_fd >= 0:
                os.close(self.parent_fd)
                self.parent_fd = -1
            raise

    def bind_or_validate(self, root_fd: int) -> None:
        root = os.fstat(root_fd)
        self._require_root(root)
        current = os.stat(
            self.state_root.name, dir_fd=self.parent_fd, follow_symlinks=False
        )
        self._require_root(current)
        identity = (root.st_dev, root.st_ino, root.st_mode, root.st_uid)
        if identity != (current.st_dev, current.st_ino, current.st_mode, current.st_uid):
            raise AutomationRefusal("Unattended namespace root path was rebound.")
        payload = canonical_json({
            "device": root.st_dev,
            "format": "spyro-editor-id65-v3-unattended-namespace-authority",
            "formatVersion": 1,
            "inode": root.st_ino,
            "mode": stat.S_IMODE(root.st_mode),
            "stateRoot": str(self.state_root),
            "uid": self.expected_uid,
        })
        os.lseek(self.fd, 0, os.SEEK_SET)
        before = os.fstat(self.fd)
        self._require_file(before)
        if before.st_size == 0:
            _write_all(self.fd, payload)
            os.fsync(self.fd)
            os.fsync(self.parent_fd)
        elif before.st_size > 4096:
            raise AutomationRefusal("Unattended namespace authority is oversized.")
        else:
            observed = os.read(self.fd, before.st_size + 1)
            after = os.fstat(self.fd)
            self._require_file(after)
            if (
                observed != payload
                or before.st_size != after.st_size
                or before.st_mtime_ns != after.st_mtime_ns
                or before.st_ctime_ns != after.st_ctime_ns
            ):
                raise AutomationRefusal(
                    "Unattended namespace authority identity was replaced or tampered."
                )
        self.root_identity = identity
        self.assert_current(root_fd)

    def assert_current(self, root_fd: int) -> None:
        if self.root_identity is None or self.file_identity is None:
            raise AutomationRefusal("Unattended namespace authority is not bound.")
        root = os.fstat(root_fd)
        current = os.stat(
            self.state_root.name, dir_fd=self.parent_fd, follow_symlinks=False
        )
        observed_root = (root.st_dev, root.st_ino, root.st_mode, root.st_uid)
        path_root = (
            current.st_dev, current.st_ino, current.st_mode, current.st_uid
        )
        if observed_root != self.root_identity or path_root != self.root_identity:
            raise AutomationRefusal("Unattended namespace root path was rebound.")
        anchor = os.fstat(self.fd)
        anchor_path = os.stat(
            self.name, dir_fd=self.parent_fd, follow_symlinks=False
        )
        if self.file_identity != (
            anchor.st_dev, anchor.st_ino, anchor.st_mode, anchor.st_uid, anchor.st_nlink
        ) or self.file_identity != (
            anchor_path.st_dev, anchor_path.st_ino, anchor_path.st_mode,
            anchor_path.st_uid, anchor_path.st_nlink,
        ):
            raise AutomationRefusal("Unattended namespace authority path was rebound.")

    def _require_file(self, info: os.stat_result) -> None:
        if (
            not stat.S_ISREG(info.st_mode)
            or stat.S_IMODE(info.st_mode) != 0o600
            or info.st_uid != self.expected_uid
            or info.st_nlink != 1
        ):
            raise AutomationRefusal("Unattended namespace authority inode is unsafe.")

    def _require_root(self, info: os.stat_result) -> None:
        if (
            not stat.S_ISDIR(info.st_mode)
            or stat.S_IMODE(info.st_mode) != 0o700
            or info.st_uid != self.expected_uid
        ):
            raise AutomationRefusal("Unattended namespace root inode is unsafe.")

    def __exit__(self, exc_type: Any, exc: Any, traceback: Any) -> None:
        del exc_type, exc, traceback
        first_error: BaseException | None = None
        if self.fd >= 0:
            try:
                fcntl.flock(self.fd, fcntl.LOCK_UN)
            except BaseException as error:
                first_error = error
            try:
                os.close(self.fd)
            except BaseException as error:
                if first_error is None:
                    first_error = error
            self.fd = -1
        if self.parent_fd >= 0:
            try:
                fcntl.flock(self.parent_fd, fcntl.LOCK_UN)
            except BaseException as error:
                if first_error is None:
                    first_error = error
            try:
                os.close(self.parent_fd)
            except BaseException as error:
                if first_error is None:
                    first_error = error
            self.parent_fd = -1
        if first_error is not None:
            raise first_error


class DurableRunGuard:
    """Private O_EXCL run lock plus durable nonce and plan-hash ledgers."""

    def __init__(
        self,
        state_root: pathlib.Path,
        *,
        expected_uid: int | None = None,
    ) -> None:
        self.state_root = pathlib.Path(os.path.abspath(state_root))
        self.expected_uid = os.geteuid() if expected_uid is None else expected_uid
        if type(self.expected_uid) is not int or self.expected_uid < 0:
            raise AutomationRefusal("Durable replay namespace UID is invalid.")

    @contextlib.contextmanager
    def execution(self, plan: InputPlan) -> Any:
        if (
            type(plan) is not InputPlan
            or PLAN_NONCE_RE.fullmatch(plan.plan_nonce) is None
            or SHA256_RE.fullmatch(plan.sha256) is None
        ):
            raise AutomationRefusal("Durable run guard received an invalid plan identity.")
        with _DurableNamespaceAuthority(
            self.state_root, self.expected_uid
        ) as authority:
            with self._execution_in_namespace(plan, authority):
                yield

    @contextlib.contextmanager
    def _execution_in_namespace(
        self, plan: InputPlan, authority: _DurableNamespaceAuthority
    ) -> Any:
        root_fd = self._open_root()
        authority.bind_or_validate(root_fd)
        lock_fd: int | None = None
        lock_identity: tuple[int, int, int, int, int] | None = None
        try:
            lock_payload = canonical_json({
                "format": "spyro-editor-id65-v3-unattended-run-lock",
                "formatVersion": 1,
                "pid": os.getpid(),
                "planNonce": plan.plan_nonce,
                "planSha256": plan.sha256,
                "uid": self.expected_uid,
            })
            lock_fd = self._create_exclusive_file(
                root_fd, "active-run.lock", lock_payload, "active run lock"
            )
            lock_info = os.fstat(lock_fd)
            lock_identity = (
                lock_info.st_dev,
                lock_info.st_ino,
                lock_info.st_size,
                lock_info.st_mtime_ns,
                lock_info.st_ctime_ns,
            )
            nonce_fd: int | None = None
            hash_fd: int | None = None
            try:
                nonce_fd = self._open_child_directory(root_fd, "nonces")
                hash_fd = self._open_child_directory(root_fd, "plan-hashes")
                nonce_payload = canonical_json({
                    "format": "spyro-editor-id65-v3-unattended-nonce-claim",
                    "formatVersion": 1,
                    "planNonce": plan.plan_nonce,
                    "planSha256": plan.sha256,
                })
                plan_payload = canonical_json({
                    "format": "spyro-editor-id65-v3-unattended-plan-hash-claim",
                    "formatVersion": 1,
                    "planNonce": plan.plan_nonce,
                    "planSha256": plan.sha256,
                })
                self._claim_ledger_file(
                    nonce_fd, f"{plan.plan_nonce}.claim", nonce_payload, "plan nonce"
                )
                self._claim_ledger_file(
                    hash_fd, f"{plan.sha256}.claim", plan_payload, "plan hash"
                )
            finally:
                if nonce_fd is not None:
                    os.close(nonce_fd)
                if hash_fd is not None:
                    os.close(hash_fd)
            yield
        finally:
            try:
                if lock_fd is not None and lock_identity is not None:
                    try:
                        current = os.stat(
                            "active-run.lock", dir_fd=root_fd, follow_symlinks=False
                        )
                        if (
                            not stat.S_ISREG(current.st_mode)
                            or stat.S_IMODE(current.st_mode) != 0o600
                            or current.st_uid != self.expected_uid
                            or current.st_nlink != 1
                            or (
                                current.st_dev,
                                current.st_ino,
                                current.st_size,
                                current.st_mtime_ns,
                                current.st_ctime_ns,
                            ) != lock_identity
                        ):
                            raise AutomationRefusal(
                                "Active unattended run lock was replaced or tampered."
                            )
                        os.unlink("active-run.lock", dir_fd=root_fd)
                        os.fsync(root_fd)
                    finally:
                        os.close(lock_fd)
            finally:
                try:
                    authority.assert_current(root_fd)
                finally:
                    os.close(root_fd)

    def _open_root(self) -> int:
        if str(self.state_root) != os.path.realpath(self.state_root):
            raise AutomationRefusal("Unattended private namespace resolves through a symlink.")
        parent = self.state_root.parent
        if not parent.is_dir() or str(parent) != os.path.realpath(parent):
            raise AutomationRefusal("Unattended private namespace parent is unsafe.")
        try:
            os.mkdir(self.state_root, 0o700)
        except FileExistsError:
            pass
        except OSError as exc:
            raise AutomationRefusal("Cannot create unattended private namespace.") from exc
        flags = (
            os.O_RDONLY
            | getattr(os, "O_CLOEXEC", 0)
            | getattr(os, "O_DIRECTORY", 0)
            | getattr(os, "O_NOFOLLOW", 0)
        )
        try:
            descriptor = os.open(self.state_root, flags)
        except OSError as exc:
            raise AutomationRefusal("Cannot open unattended private namespace safely.") from exc
        try:
            self._require_private_directory(os.fstat(descriptor), "private namespace")
        except BaseException:
            os.close(descriptor)
            raise
        return descriptor

    def _open_child_directory(self, parent_fd: int, name: str) -> int:
        try:
            os.mkdir(name, 0o700, dir_fd=parent_fd)
            os.fsync(parent_fd)
        except FileExistsError:
            pass
        except OSError as exc:
            raise AutomationRefusal(f"Cannot create private {name} ledger.") from exc
        flags = (
            os.O_RDONLY
            | getattr(os, "O_CLOEXEC", 0)
            | getattr(os, "O_DIRECTORY", 0)
            | getattr(os, "O_NOFOLLOW", 0)
        )
        try:
            descriptor = os.open(name, flags, dir_fd=parent_fd)
        except OSError as exc:
            raise AutomationRefusal(f"Cannot open private {name} ledger safely.") from exc
        try:
            self._require_private_directory(os.fstat(descriptor), f"{name} ledger")
        except BaseException:
            os.close(descriptor)
            raise
        return descriptor

    def _require_private_directory(self, info: os.stat_result, label: str) -> None:
        if (
            not stat.S_ISDIR(info.st_mode)
            or stat.S_IMODE(info.st_mode) != 0o700
            or info.st_uid != self.expected_uid
        ):
            raise AutomationRefusal(f"Unattended {label} owner or mode is unsafe.")

    def _create_exclusive_file(
        self, directory_fd: int, name: str, payload: bytes, label: str
    ) -> int:
        flags = (
            os.O_WRONLY
            | os.O_CREAT
            | os.O_EXCL
            | getattr(os, "O_CLOEXEC", 0)
            | getattr(os, "O_NOFOLLOW", 0)
        )
        try:
            descriptor = os.open(name, flags, 0o600, dir_fd=directory_fd)
        except FileExistsError as exc:
            self._inspect_existing_file(directory_fd, name, None, label)
            raise AutomationRefusal(f"Unattended {label} already exists.") from exc
        except OSError as exc:
            raise AutomationRefusal(f"Cannot create unattended {label}.") from exc
        try:
            info = os.fstat(descriptor)
            self._require_private_file(info, label)
            _write_all(descriptor, payload)
            os.fsync(descriptor)
            after = os.fstat(descriptor)
            self._require_private_file(after, label)
            path_after = os.stat(name, dir_fd=directory_fd, follow_symlinks=False)
            self._require_private_file(path_after, label)
            if (
                (info.st_dev, info.st_ino, info.st_uid, info.st_mode)
                != (after.st_dev, after.st_ino, after.st_uid, after.st_mode)
                or after.st_size != len(payload)
                or (
                    path_after.st_dev,
                    path_after.st_ino,
                    path_after.st_size,
                    path_after.st_uid,
                    path_after.st_mode,
                ) != (
                    after.st_dev,
                    after.st_ino,
                    after.st_size,
                    after.st_uid,
                    after.st_mode,
                )
            ):
                raise AutomationRefusal(f"Unattended {label} changed while writing.")
            os.fsync(directory_fd)
        except BaseException:
            os.close(descriptor)
            raise
        return descriptor

    def _claim_ledger_file(
        self, directory_fd: int, name: str, payload: bytes, label: str
    ) -> None:
        try:
            descriptor = self._create_exclusive_file(
                directory_fd, name, payload, f"{label} claim"
            )
        except AutomationRefusal as exc:
            self._inspect_existing_file(directory_fd, name, payload, f"{label} claim")
            raise AutomationRefusal(f"Durable {label} replay or tamper was refused.") from exc
        else:
            os.close(descriptor)

    def _inspect_existing_file(
        self,
        directory_fd: int,
        name: str,
        expected_payload: bytes | None,
        label: str,
    ) -> None:
        flags = os.O_RDONLY | getattr(os, "O_CLOEXEC", 0) | getattr(os, "O_NOFOLLOW", 0)
        try:
            descriptor = os.open(name, flags, dir_fd=directory_fd)
        except OSError as exc:
            raise AutomationRefusal(f"Existing unattended {label} is unsafe.") from exc
        try:
            before = os.fstat(descriptor)
            self._require_private_file(before, label)
            if before.st_size > 4096:
                raise AutomationRefusal(f"Existing unattended {label} is oversized.")
            chunks: list[bytes] = []
            while True:
                chunk = os.read(descriptor, 4096)
                if not chunk:
                    break
                chunks.append(chunk)
            after = os.fstat(descriptor)
            self._require_private_file(after, label)
            path_after = os.stat(name, dir_fd=directory_fd, follow_symlinks=False)
            self._require_private_file(path_after, label)
            stable_fields = (
                "st_dev", "st_ino", "st_mode", "st_size", "st_uid", "st_nlink",
                "st_mtime_ns", "st_ctime_ns",
            )
            if any(
                getattr(before, field) != getattr(after, field)
                or getattr(before, field) != getattr(path_after, field)
                for field in stable_fields
            ):
                raise AutomationRefusal(f"Existing unattended {label} changed while reading.")
            observed = b"".join(chunks)
            if expected_payload is not None and observed != expected_payload:
                raise AutomationRefusal(f"Existing unattended {label} content was tampered.")
        finally:
            os.close(descriptor)

    def _require_private_file(self, info: os.stat_result, label: str) -> None:
        if (
            not stat.S_ISREG(info.st_mode)
            or stat.S_IMODE(info.st_mode) != 0o600
            or info.st_uid != self.expected_uid
            or info.st_nlink != 1
        ):
            raise AutomationRefusal(f"Unattended {label} owner, mode, or links are unsafe.")


def _write_all(descriptor: int, payload: bytes) -> None:
    offset = 0
    while offset < len(payload):
        written = os.write(descriptor, payload[offset:])
        if written <= 0:
            raise AutomationRefusal("Atomic unattended state write made no progress.")
        offset += written


@dataclasses.dataclass(frozen=True)
class AutomationResult:
    status: str
    mode: str
    profile_id: str
    process_pid: int | None
    plan_sha256: str | None
    planned_count: int
    posted_count: int
    confirmed_count: int
    machine_finalized: bool

    def __post_init__(self) -> None:
        expected_status = {
            "self-test": SELF_TEST_STATUS,
            "preflight": PREFLIGHT_STATUS,
            "execute": INPUT_STATUS,
        }.get(self.mode)
        if expected_status is None or self.status != expected_status or self.machine_finalized:
            raise AutomationRefusal("Machine result attempted to claim final acceptance.")
        if any(
            type(value) is not int or value < 0
            for value in (self.planned_count, self.posted_count, self.confirmed_count)
        ):
            raise AutomationRefusal("Automation result counts are invalid.")
        if self.confirmed_count > self.posted_count:
            raise AutomationRefusal("Automation result confirms more input than was posted.")
        if self.mode in {"self-test", "preflight"} and (
            self.posted_count != 0 or self.confirmed_count != 0
        ):
            raise AutomationRefusal("Non-execution result claims posted or confirmed input.")
        if self.mode == "execute" and (
            self.planned_count == 0 or self.posted_count != self.planned_count
        ):
            raise AutomationRefusal(
                "Execution result does not bind one posted pulse to every planned event."
            )

    def to_payload(self) -> dict[str, Any]:
        return {
            "confirmedCount": self.confirmed_count,
            "machineFinalized": False,
            "mode": self.mode,
            "planSha256": self.plan_sha256,
            "plannedCount": self.planned_count,
            "postedCount": self.posted_count,
            "processPid": self.process_pid,
            "profileId": self.profile_id,
            "status": self.status,
        }


class UnattendedFoundation:
    def __init__(
        self,
        *,
        verifier: ProfileVerifier,
        observer: ProcessObserver,
        transport: TargetedKeyTransport,
        pad_confirmer: PadConfirmer,
        runtime_attestor: RuntimeSessionAttestor,
        run_guard: DurableRunGuard,
        coordinator_lease_factory: Callable[[], Any],
    ) -> None:
        self.verifier = verifier
        self.observer = observer
        self.transport = transport
        self.pad_confirmer = pad_confirmer
        self.runtime_attestor = runtime_attestor
        self.run_guard = run_guard
        if not callable(coordinator_lease_factory):
            raise AutomationRefusal("Shared capture coordinator lease is unavailable.")
        self.coordinator_lease_factory = coordinator_lease_factory

    def run(self, plan: InputPlan, pid: int, *, execute: bool) -> AutomationResult:
        with self.coordinator_lease_factory() as lease:
            validator = getattr(lease, "assert_current", None)
            if not callable(validator):
                raise AutomationRefusal(
                    "Shared capture coordinator lease cannot prove path ownership."
                )
            validator()
            return self._run_with_coordinator_lease(
                plan, pid, execute=execute, lease_validator=validator
            )

    def _run_with_coordinator_lease(
        self,
        plan: InputPlan,
        pid: int,
        *,
        execute: bool,
        lease_validator: Callable[[], None],
    ) -> AutomationResult:
        profile = self.verifier.verify()
        if profile.profile_id != plan.profile_id:
            raise AutomationRefusal("Verified profile does not own this input plan.")
        unavailable = [event.action.value for event in plan.events if event.action not in profile.enabled_actions]
        if unavailable:
            raise AutomationRefusal(
                "Input plan requests actions absent from the verified profile: "
                + ",".join(sorted(set(unavailable)))
            )
        readiness = self.pad_confirmer.readiness()
        if type(readiness) is not PadConfirmationReadiness:
            raise AutomationRefusal("Pad confirmation readiness contract is invalid.")
        if not readiness.ready:
            raise AutomationRefusal(readiness.refusal_reason or "Pad confirmation is unavailable.")
        session_readiness = self.runtime_attestor.readiness()
        if type(session_readiness) is not RuntimeSessionAttestationReadiness:
            raise AutomationRefusal("Runtime-session attestor readiness contract is invalid.")
        if not session_readiness.ready:
            raise AutomationRefusal(
                session_readiness.refusal_reason or "Runtime session attestation is unavailable."
            )
        baseline = self.observer.observe(pid)
        verify_exact_process(baseline, expected_pid=pid, profile=profile)
        self.runtime_attestor.attest(profile, baseline)
        self.pad_confirmer.preflight(plan.events, baseline)
        macro_actions = tuple(sorted(
            {
                event.action for event in plan.events
                if ACTION_SPECS[event.action].pad_bit is not None
            },
            key=lambda action: action.value,
        ))
        self.pad_confirmer.confirm_initial_released(macro_actions, baseline)
        lease_validator()
        self.transport.require_target(pid)
        if not execute:
            return AutomationResult(
                PREFLIGHT_STATUS, "preflight", profile.profile_id, pid,
                plan.sha256, len(plan.events), 0, 0, False
            )
        with self.run_guard.execution(plan):
            return self._execute(
                plan, pid, profile, baseline, macro_actions, lease_validator
            )

    def _execute(
        self,
        plan: InputPlan,
        pid: int,
        profile: VerifiedProfile,
        baseline: ProcessIdentity,
        macro_actions: Sequence[KeyAction],
        lease_validator: Callable[[], None],
    ) -> AutomationResult:
        posted_count = 0
        confirmed_count = 0
        macro_states = {action: PadState.RELEASED for action in macro_actions}

        def revalidate_release_identity() -> None:
            release_process = self.observer.observe_release_identity(pid, baseline)
            verify_exact_process(
                release_process, expected_pid=pid, profile=profile
            )
            if release_process != baseline:
                raise AutomationRefusal(
                    "DuckStation process identity changed before mandatory key-up."
                )

        def observe_post_release_drift() -> None:
            lease_validator()
            release_process = self.observer.observe(pid)
            verify_exact_process(
                release_process, expected_pid=pid, profile=profile
            )
            if release_process != baseline:
                raise AutomationRefusal(
                    "DuckStation process identity changed after mandatory key-up."
                )
            self.runtime_attestor.attest(profile, release_process)

        primary: BaseException | None = None
        try:
            for event in plan.events:
                before = self.observer.observe(pid)
                verify_exact_process(before, expected_pid=pid, profile=profile)
                if before != baseline:
                    raise AutomationRefusal("DuckStation process identity changed before input.")
                self.runtime_attestor.attest(profile, before)
                posted_before = self.transport.posted_down_count
                try:
                    self.transport.tap(
                        pid,
                        event.action,
                        release_identity_validator=revalidate_release_identity,
                        post_release_drift_observer=observe_post_release_drift,
                        pre_key_down_validator=lease_validator,
                    )
                finally:
                    posted_delta = self.transport.posted_down_count - posted_before
                    if posted_delta not in {0, 1}:
                        raise AutomationRefusal("Input transport posted an invalid pulse count.")
                    posted_count += posted_delta
                    if posted_delta == 1 and event.action in macro_states:
                        macro_states[event.action] = (
                            PadState.RELEASED
                            if macro_states[event.action] is PadState.HELD
                            else PadState.HELD
                        )
                after_key = self.observer.observe(pid)
                verify_exact_process(after_key, expected_pid=pid, profile=profile)
                if after_key != baseline:
                    raise AutomationRefusal("DuckStation process identity changed after input.")
                if ACTION_SPECS[event.action].pad_bit is not None:
                    self.pad_confirmer.confirm(event, after_key)
                    confirmed_count += 1
                    macro_states[event.action] = event.expected_pad_state
                after_confirmation = self.observer.observe(pid)
                verify_exact_process(after_confirmation, expected_pid=pid, profile=profile)
                if after_confirmation != baseline:
                    raise AutomationRefusal(
                        "DuckStation process identity changed during pad confirmation."
                    )
                self.runtime_attestor.attest(profile, after_confirmation)
            final_profile = self.verifier.verify()
            if final_profile != profile:
                raise AutomationRefusal("Pinned profile/resource identity changed during input.")
            final_process = self.observer.observe(pid)
            verify_exact_process(final_process, expected_pid=pid, profile=profile)
            if final_process != baseline:
                raise AutomationRefusal("DuckStation process identity changed at final readback.")
            self.runtime_attestor.attest(profile, final_process)
            if any(state is not PadState.RELEASED for state in macro_states.values()):
                raise AutomationRefusal("Macro-toggle execution did not end RELEASED.")
        except BaseException as exc:
            primary = exc
        release_errors = self.transport.release_all()
        cleanup_issues = list(release_errors)
        if self.transport.possibly_held:
            cleanup_issues.extend(self.transport.release_all())
        if primary is not None and not self.transport.possibly_held:
            for action in macro_actions:
                try:
                    process = self.observer.observe(pid)
                    verify_exact_process(process, expected_pid=pid, profile=profile)
                    if process != baseline:
                        raise AutomationRefusal(
                            "DuckStation identity changed before macro cleanup."
                        )
                    self.runtime_attestor.attest(profile, process)
                    observed_state = self.pad_confirmer.observe_state(action, process)
                    if observed_state is PadState.HELD:
                        focus_drift = self.transport.compensating_toggle(
                            pid,
                            action,
                            identity_validator=revalidate_release_identity,
                            post_release_drift_observer=observe_post_release_drift,
                            pre_key_down_validator=lease_validator,
                        )
                        process_after = self.observer.observe(pid)
                        verify_exact_process(
                            process_after, expected_pid=pid, profile=profile
                        )
                        if process_after != baseline:
                            raise AutomationRefusal(
                                "DuckStation identity changed after macro cleanup."
                            )
                        self.runtime_attestor.attest(profile, process_after)
                        if (
                            self.pad_confirmer.observe_state(action, process_after)
                            is not PadState.RELEASED
                        ):
                            raise AutomationRefusal(
                                f"Compensating {action.value} untoggle was not RELEASED."
                            )
                        macro_states[action] = PadState.RELEASED
                        if focus_drift:
                            cleanup_issues.append(
                                f"{pid}:{action.value}:compensated-with-focus-drift"
                            )
                    else:
                        macro_states[action] = PadState.RELEASED
                except BaseException as exc:
                    cleanup_issues.append(
                        f"{pid}:{action.value}:macro-cleanup-{type(exc).__name__}"
                    )
        cleanup_issues.extend(self.transport.release_all())
        if cleanup_issues:
            raise AutomationRefusal(
                "Mandatory release or macro cleanup failed closed: "
                + ",".join(cleanup_issues)
            ) from primary
        if primary is not None:
            if isinstance(primary, AutomationRefusal):
                raise primary
            raise AutomationRefusal("Unattended input execution failed closed.") from primary
        return AutomationResult(
            INPUT_STATUS, "execute", profile.profile_id, pid,
            plan.sha256, len(plan.events), posted_count, confirmed_count, False
        )


def _read_plan(path: pathlib.Path) -> InputPlan:
    _identity, raw = _read_stable_file(
        path,
        expected_sha256=None,
        maximum_bytes=MAX_PLAN_BYTES,
        expected_owner_uid=os.getuid(),
        retain_bytes=True,
        label="Input plan",
    )
    if raw is None:
        raise AutomationRefusal("Input-plan byte read was incomplete.")
    return InputPlan.parse(raw)


def _example_plan_bytes() -> bytes:
    nonce = "0123456789abcdef0123456789abcdef"
    return canonical_json({
        "events": [{
            "action": "TOGGLE_PAUSE",
            "expectedPadState": "NOT_APPLICABLE",
            "nonce": f"{nonce}-00000000",
            "sequence": 0,
        }],
        "format": PLAN_FORMAT,
        "formatVersion": PLAN_FORMAT_VERSION,
        "planNonce": nonce,
        "profileId": PROFILE_ID,
    })


def run_self_test() -> AutomationResult:
    plan = InputPlan.parse(_example_plan_bytes())
    if plan.events[0].action is not KeyAction.TOGGLE_PAUSE:
        raise AutomationRefusal("Self-test allowlist parse changed.")
    noncanonical = _example_plan_bytes().replace(b'"events":', b'"events" :')
    try:
        InputPlan.parse(noncanonical)
    except AutomationRefusal:
        pass
    else:
        raise AutomationRefusal("Self-test accepted non-canonical JSON.")
    if 53 in {spec.key_code for spec in ACTION_SPECS.values()}:
        raise AutomationRefusal("Self-test found forbidden Escape in the allowlist.")
    if ACTION_SPECS[KeyAction.POWER_OFF].key_code != 111:
        raise AutomationRefusal("Self-test PowerOff binding is not exact F12.")
    if ACTION_SPECS[KeyAction.TOGGLE_PAUSE].key_code != 49:
        raise AutomationRefusal("Self-test TogglePause binding is not exact Space.")
    result = AutomationResult(
        SELF_TEST_STATUS, "self-test", PROFILE_ID, None,
        plan.sha256, 1, 0, 0, False
    )
    return result


class _RefusingArgumentParser(argparse.ArgumentParser):
    def error(self, message: str) -> None:
        raise AutomationRefusal(f"CLI arguments refused: {message}")


def _build_parser() -> argparse.ArgumentParser:
    parser = _RefusingArgumentParser(
        description="Fail-closed ID65 V3 unattended-input foundation."
    )
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument("--self-test", action="store_true")
    mode.add_argument("--preflight", action="store_true")
    mode.add_argument("--execute", action="store_true")
    parser.add_argument("--repository-root", type=pathlib.Path)
    parser.add_argument("--pid", type=int)
    parser.add_argument("--input-plan", type=pathlib.Path)
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    try:
        parser = _build_parser()
        arguments = parser.parse_args(argv)
        if arguments.self_test:
            if any(value is not None for value in (
                arguments.repository_root, arguments.pid, arguments.input_plan
            )):
                raise AutomationRefusal("Self-test accepts no live-process arguments.")
            result = run_self_test()
        else:
            if (
                arguments.repository_root is None
                or arguments.pid is None
                or arguments.input_plan is None
            ):
                raise AutomationRefusal(
                    "Preflight/execute requires repository root, exact PID, and input plan."
                )
            plan = _read_plan(arguments.input_plan)
            foundation = UnattendedFoundation(
                verifier=FrozenProfileVerifier(arguments.repository_root),
                observer=MacProcessObserver(),
                transport=TargetedKeyTransport(MacCGEventBackend()),
                pad_confirmer=UnavailableNativePadConfirmer(),
                runtime_attestor=UnavailableRuntimeSessionAttestor(),
                run_guard=DurableRunGuard(
                    arguments.repository_root / UNATTENDED_STATE_RELATIVE_PATH
                ),
                coordinator_lease_factory=lambda: SharedCaptureCoordinatorLease(
                    arguments.repository_root
                ),
            )
            result = foundation.run(plan, arguments.pid, execute=arguments.execute)
        sys.stdout.buffer.write(canonical_json(result.to_payload()))
        return 0
    except (AutomationRefusal, OSError, ValueError, KeyboardInterrupt) as exc:
        print(f"REFUSED: {exc}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
