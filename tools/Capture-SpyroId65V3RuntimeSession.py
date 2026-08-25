#!/usr/bin/env python3
"""Fail-closed capture-only coordinator for the frozen Spyro ID65 V3 candidate.

This program never launches, signals, resumes, quits, or injects input into
DuckStation.  The one approved emulator side effect is the pinned read-only
watcher's connection-induced pause; the operator alone manually resumes after
that connection.  The only children owned here are that watcher and macOS
``screencapture``.  Output is limited to raw, append-only capture provenance
under the fixed capture-staging subtree; this is not an evidence-package or
release writer.

Process/window checks are exact checkpoints around operator prompts, not a
continuous monitor.  The macOS diagnostic and capture executables are invoked
by absolute system paths but are not cryptographically pinned.  Process-use
provenance is OS-observed and hash-chained, not cryptographically authenticated.
"""

from __future__ import annotations

import argparse
import contextlib
import ctypes
import ctypes.util
import dataclasses
import datetime as dt
import errno
import fcntl
import hashlib
import json
import math
import os
import pathlib
import plistlib
import pwd
import re
import secrets
import select
import shlex
import signal
import stat
import struct
import subprocess
import sys
import tempfile
import time
import unicodedata
from types import MappingProxyType, ModuleType
import zlib
from collections.abc import Callable, Iterable, Mapping, Sequence
from typing import Any, BinaryIO, Final, Protocol, TextIO


FORMAT: Final = "spyro-editor-id65-v3-runtime-capture-event-journal"
JOURNAL_FORMAT_VERSION: Final = 3
RUN_FORMAT: Final = "spyro-editor-id65-v3-runtime-capture-run-origin"
RUN_FORMAT_VERSION: Final = 2
PROFILE_FORMAT: Final = "spyro-editor-id65-v3-runtime-profile-snapshot"
PROFILE_FORMAT_VERSION: Final = 2
CAPTURE_COORDINATOR_RELATIVE_PATH: Final = (
    "tools/Capture-SpyroId65V3RuntimeSession.py"
)
WATCHER_RELATIVE_PATH: Final = "tools/Watch-SpyroId65RuntimeState.py"
CONTRACT_RELATIVE_PATH: Final = (
    "src/Spyro.Editor.Core/Exporting/Id65V3RuntimeEvidenceContract.cs"
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

SESSION_IDS: Final = (
    "candidate-primary-edge-0",
    "candidate-edge-1",
    "candidate-edge-2",
    "candidate-cold-reset",
    "retail-town-square",
    "retail-gnastys-loot",
    "retail-sunny-flight",
)


@dataclasses.dataclass(frozen=True)
class EdgeAssignment:
    edge_id: str
    segment: str
    description: str
    direction: str
    midpoint: str


EDGE_ASSIGNMENTS: Final[Mapping[str, EdgeAssignment]] = MappingProxyType({
    "candidate-primary-edge-0": EdgeAssignment(
        "E0", "A-B", "rear/base edge", "-Y, opposite spawn yaw", "(384,272,512)"
    ),
    "candidate-edge-1": EdgeAssignment(
        "E1", "B-C", "right-hand/east slope", "+X", "(440,384,512)"
    ),
    "candidate-edge-2": EdgeAssignment(
        "E2", "C-A", "left-hand/west slope", "-X", "(328,384,512)"
    ),
})


def _require_closed_edge_assignments() -> None:
    expected = (
        ("candidate-primary-edge-0", "E0", "A-B", "rear/base edge",
         "-Y, opposite spawn yaw", "(384,272,512)"),
        ("candidate-edge-1", "E1", "B-C", "right-hand/east slope",
         "+X", "(440,384,512)"),
        ("candidate-edge-2", "E2", "C-A", "left-hand/west slope",
         "-X", "(328,384,512)"),
    )
    actual = tuple(
        (session_id, assignment.edge_id, assignment.segment,
         assignment.description, assignment.direction, assignment.midpoint)
        for session_id, assignment in EDGE_ASSIGNMENTS.items()
    )
    if actual != expected or tuple(EDGE_ASSIGNMENTS) != SESSION_IDS[:3]:
        raise RuntimeError("Closed ID65 physical-edge assignments changed.")


_require_closed_edge_assignments()
RUN_ID_RE: Final = re.compile(
    r"\Aid65v3-[0-9]{8}t[0-9]{6}z-[0-9a-f]{16}\Z", re.ASCII
)
SHA256_RE: Final = re.compile(r"\A[0-9a-f]{64}\Z", re.ASCII)
CANONICAL_UTC_RE: Final = re.compile(
    r"\A[0-9]{4}-[0-9]{2}-[0-9]{2}T"
    r"[0-9]{2}:[0-9]{2}:[0-9]{2}\.[0-9]{3}Z\Z",
    re.ASCII,
)
CANONICAL_UPPER_UUID_RE: Final = re.compile(
    r"\A[0-9A-F]{8}-[0-9A-F]{4}-[0-9A-F]{4}-"
    r"[0-9A-F]{4}-[0-9A-F]{12}\Z",
    re.ASCII,
)
PLATFORM_SERIAL_RE: Final = re.compile(r"\A[A-Z0-9]{8,24}\Z", re.ASCII)
ZERO_UUID: Final = "00000000-0000-0000-0000-000000000000"
PLATFORM_SERIAL_PLACEHOLDERS: Final = frozenset({
    "00000000",
    "0123456789",
    "12345678",
    "DEFAULTSTRING",
    "FFFFFFFF",
    "NOTAVAILABLE",
    "SERIALNUMBER",
    "SYSTEMSERIALNUMBER",
    "UNKNOWN00",
})
BOOT_PLATFORM_IOREG_ARGV: Final = (
    "/usr/sbin/ioreg", "-rd1", "-c", "IOPlatformExpertDevice", "-a"
)
MAX_SAFE_INTEGER: Final = 9_007_199_254_740_991
MAX_TEXT_BYTES: Final = 64 * 1024 * 1024
MAX_MEDIA_BYTES: Final = 64 * 1024 * 1024 * 1024
MAX_JOURNAL_EVENTS: Final = 512
MAX_RECORDING_DURATION_MICROSECONDS: Final = 30 * 60 * 1_000_000
MAX_FALL_AUTHORIZATION_BINDING_MILLISECONDS: Final = 30_000
MAX_POST_DEATH_RECOVERY_MILLISECONDS: Final = 15_000
ASSISTED_READINESS_MINIMUM_MILLISECONDS: Final = 2_000
ASSISTED_READINESS_MINIMUM_SAMPLES: Final = 19
ASSISTED_PRIMARY_DEPARTURE_MAXIMUM_MILLISECONDS: Final = 300_000
ASSISTED_PRIMARY_LANDING_MAXIMUM_MILLISECONDS: Final = 30_000
ZERO_SHA256: Final = "0" * 64
GDB_HOST: Final = "127.0.0.1"
GDB_PORT: Final = 2345
RUNTIME_ENVIRONMENT_SENTINEL_NAME: Final = (
    "SPYRO_ID65_CAPTURE_ENVIRONMENT_END_V1"
)
RUNTIME_ENVIRONMENT_SENTINEL_VALUE: Final = (
    "duckstation-id65-unattended-v1"
)
RUNTIME_ENVIRONMENT_FIXED_ASSIGNMENTS: Final = (
    "LANG=C",
    "LC_ALL=C",
    "PATH=/usr/bin:/bin:/usr/sbin:/sbin",
    f"{RUNTIME_ENVIRONMENT_SENTINEL_NAME}="
    f"{RUNTIME_ENVIRONMENT_SENTINEL_VALUE}",
)
APPLE_AUXILIARY_VISIBLE_KEY_ORDER: Final = (
    "ptr_munge",
    "main_stack",
    "executable_file",
    "dyld_file",
    "executable_cdhash",
    "executable_boothash",
    "th_port",
    "security_config",
)
APPLE_AUXILIARY_REDACTED_VALUE_KEYS: Final = frozenset({
    "ptr_munge",
    "main_stack",
    "th_port",
})

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
EXPECTED_CANDIDATE_CUE_SHA256: Final = (
    "ab91adb4ffcbad70bf7a226a2a4a6d0f6efe6b54fbed0b8126e59d1b8ecaf6df"
)
EXPECTED_CANDIDATE_BIN_SHA256: Final = (
    "42e4d5cae98d4786023bef26517d0435b43aff365b797495386813f44ecb0c45"
)
FAILED_V1_BIN_SHA256: Final = (
    "8020947d4ab5e7e4b0eac4bc6409ff3d6f11212b0e118bbf007d870812014a8e"
)
SUPERSEDED_V2_BIN_SHA256: Final = (
    "23ce483a29187996827148d986fb12f483fc2caa82fc953106882568ea9254a6"
)
EXPECTED_WATCHER_SHA256: Final = (
    "eac3953a0ff7ff8acdefbe43cf987f296d44091ad51619efd225690ba1dc0677"
)
PROFILE_BUILDER_RELATIVE_PATH: Final = "tools/Build-SpyroId65UnattendedProfile.py"
PROFILE_VERIFIER_RELATIVE_PATH: Final = "tools/Verify-SpyroId65UnattendedProfile.py"
AUTOMATION_RELATIVE_PATH: Final = "tools/Automate-SpyroId65V3RuntimeSession.py"
EXPECTED_PROFILE_BUILDER_SHA256: Final = (
    "0591f1b6bf3f61dac4bfa61d407df65a2a9862569c8d2a7678d216b95929b95c"
)
EXPECTED_PROFILE_VERIFIER_SHA256: Final = (
    "74f4ed279a3e3f60af6c4c0a0b1fa8ffc0a67886c478d8cc7d1a288b093c3671"
)
EXPECTED_AUTOMATION_SHA256: Final = (
    "049b4fbeecb4b176d415614165589162c10d8322f349af9c1ef6e93c6e1c2899"
)

SOURCE_PROFILE_ID: Final = (
    "unused-level-65-remote-blank-environment-header-repair-"
    "sector216-clean-usa-disposable-v3"
)
PROFILE_ID: Final = "duckstation-id65-unattended-v1"
PROFILE_PACKAGE_RELATIVE_PATH: Final = (
    "_local/v5-stone-hill-level-replacement/duckstation-id65-unattended-v1"
)
PACKAGED_APP_RELATIVE_PATH: Final = (
    f"{PROFILE_PACKAGE_RELATIVE_PATH}/DuckStation-ID65-Unattended-v1.app"
)
DUCKSTATION_EXECUTABLE_RELATIVE_PATH: Final = (
    f"{PACKAGED_APP_RELATIVE_PATH}/Contents/MacOS/DuckStation"
)
EXTERNAL_HOME_RELATIVE_PATH: Final = f"{PROFILE_PACKAGE_RELATIVE_PATH}/home"
EXTERNAL_HOME_DATA_RELATIVE_PATH: Final = (
    f"{EXTERNAL_HOME_RELATIVE_PATH}/Library/Application Support/DuckStation"
)
HANDOFF_RECEIPT_RELATIVE_PATH: Final = (
    f"{PROFILE_PACKAGE_RELATIVE_PATH}/handoff-receipt.json"
)
PROFILE_ROOT_RELATIVE_PATH: Final = EXTERNAL_HOME_DATA_RELATIVE_PATH
DUCKSTATION_VERSION: Final = "0.1-11580-ge7f2f101c"
EXPECTED_DUCKSTATION_SHA256: Final = (
    "14c22c6e1f4bbff13c00c24f17424ef8cf7645ee9917f7d65b17cd3fe1dd1645"
)
EXPECTED_SETTINGS_SHA256: Final = (
    "469ef98771f26395352bac1d43f90e4be14693fd4ba5316e4d71e0557a7f74f9"
)
EXPECTED_HANDOFF_RECEIPT_SHA256: Final = (
    "a748153bdd554187fb30ee79e75d25d36c606663bd4aee8444826b7612aa27d5"
)
EXPECTED_RECEIPT_CLOSURE_SHA256: Final = (
    "722a7ac322fa88749f17762185256281a4d6a930a290e35cdb955937fe57aead"
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
    "03e6f465647c04ee7d86add2550e1ba6818972233be419ab98290f05158ffff3"
)
EXPECTED_CANDIDATE_MANIFEST_SHA256: Final = (
    "1c6824d78e8cb43f791b1b184b5cba2746352073114be120fc2a4e25f25e2e97"
)
EXPECTED_RECEIPT_SIZE: Final = 73_908
EXPECTED_EXECUTABLE_SIZE: Final = 22_354_208
EXPECTED_SETTINGS_SIZE: Final = 2_143
EXPECTED_BIOS_SIZE: Final = 524_288
EXPECTED_CANDIDATE_CUE_SIZE: Final = 148
EXPECTED_CANDIDATE_BIN_SIZE: Final = 661_547_040
NORMAL_SETTINGS_PATH: Final = pathlib.Path(
    "/Users/john/Library/Application Support/DuckStation/settings.ini"
)
NORMAL_SETTINGS_RESOURCE_ID: Final = "duckstation-normal-user-settings.ini"
EXPECTED_NORMAL_SETTINGS_SHA256: Final = (
    "896daa612142659aa950c16d2435f81cb034aa8b56768b91e60a4ec8f0a5ccc9"
)
BIOS_MANIFEST_PATH: Final = (
    "bios/Sony PlayStation SCPH-1001 - DTLH-3000 BIOS v2.2 "
    "(1995-12-04)(Sony)(US).bin"
)
EXPECTED_BIOS_SHA256: Final = (
    "71af94d1e47a68c11e8fdb9f8368040601514a42a5a399cda48c7d3bff1e99d3"
)
BIOS_RELATIVE_PATH: Final = f"{EXTERNAL_HOME_DATA_RELATIVE_PATH}/{BIOS_MANIFEST_PATH}"
SAFETY_DIRECTORIES: Final = (
    "cheats",
    "gamesettings",
    "memcards",
    "patches",
    "savestates",
)
ALLOWED_VOLATILE_PREFIXES: Final = ("cache/", "screenshots/", "videos/")
ALLOWED_PROFILE_ROOT_FILES: Final = frozenset({"settings.ini", "playtime.dat"})
ALLOWED_PROCESS_CACHE_BIN_RELATIVE_PATH: Final = "cache/vulkan_shaders.bin"
MAX_PROCESS_CACHE_BIN_BYTES: Final = 64 * 1024 * 1024

RUNTIME_PROFILE_V2_DOMAIN: Final = "id65-v3-runtime-profile-v2"
RUNTIME_PROFILE_V2_FIELDS: Final = (
    ("profileId", PROFILE_ID),
    ("packageRelativePath", PROFILE_PACKAGE_RELATIVE_PATH),
    ("appRelativePath", PACKAGED_APP_RELATIVE_PATH),
    ("duckStationExecutableRelativePath", DUCKSTATION_EXECUTABLE_RELATIVE_PATH),
    ("externalHomeRelativePath", EXTERNAL_HOME_RELATIVE_PATH),
    ("externalHomeDataRelativePath", EXTERNAL_HOME_DATA_RELATIVE_PATH),
    ("handoffReceiptRelativePath", HANDOFF_RECEIPT_RELATIVE_PATH),
    ("duckStationVersion", DUCKSTATION_VERSION),
    ("duckStationExecutableByteLength", EXPECTED_EXECUTABLE_SIZE),
    ("duckStationExecutableSha256", EXPECTED_DUCKSTATION_SHA256),
    ("handoffReceiptFormat", "spyro-id65-unattended-external-home-handoff"),
    ("handoffReceiptFormatVersion", 1),
    ("handoffReceiptByteLength", EXPECTED_RECEIPT_SIZE),
    ("handoffReceiptSha256", EXPECTED_HANDOFF_RECEIPT_SHA256),
    ("handoffReceiptClosureSha256", EXPECTED_RECEIPT_CLOSURE_SHA256),
    ("appContentManifestFormat", "spyro-id65-duckstation-app-content-manifest-v1"),
    ("appContentManifestEntryCount", 292),
    ("appContentManifestSha256", EXPECTED_APP_CONTENT_MANIFEST_SHA256),
    ("outputTreeManifestFormat", "spyro-id65-output-tree-manifest-v1"),
    ("appOutputTreeManifestEntryCount", 292),
    ("appOutputTreeManifestSha256", EXPECTED_APP_OUTPUT_TREE_MANIFEST_SHA256),
    ("homeOutputTreeManifestEntryCount", 12),
    ("homeOutputTreeManifestSha256", EXPECTED_HOME_OUTPUT_TREE_MANIFEST_SHA256),
    ("packageEnvelopeFormat", "spyro-id65-package-envelope-manifest-v1"),
    ("packageEnvelopeEntryCount", 4),
    ("packageEnvelopeSha256", EXPECTED_PACKAGE_ENVELOPE_SHA256),
    ("outputXattrPolicyVersion", "apple-provenance-11-prefix-0102-v1"),
    ("externalHomeSettingsRelativePath", "settings.ini"),
    ("externalHomeSettingsByteLength", EXPECTED_SETTINGS_SIZE),
    ("externalHomeSettingsSha256", EXPECTED_SETTINGS_SHA256),
    ("externalHomeBiosRelativePath", BIOS_MANIFEST_PATH),
    ("externalHomeBiosByteLength", EXPECTED_BIOS_SIZE),
    ("externalHomeBiosSha256", EXPECTED_BIOS_SHA256),
    ("hostNormalSettingsSentinelId", NORMAL_SETTINGS_RESOURCE_ID),
    ("hostNormalSettingsByteLength", 10_058),
    ("hostNormalSettingsSha256", EXPECTED_NORMAL_SETTINGS_SHA256),
    ("hostNormalSettingsMode", "0600"),
    ("safetyDirectories", ",".join(SAFETY_DIRECTORIES)),
    ("allowedVolatilePolicy", "playtime.dat,cache/**,screenshots/**,videos/**"),
    ("trustModel", "cooperative-non-hostile-same-uid"),
    ("launchGate", "EXTERNAL_RUNTIME_LAUNCH_REQUIRED"),
    ("powerOffBinding", "Keyboard/F12"),
    ("togglePauseBinding", "Keyboard/Space"),
    ("emulationSpeed", 1),
    ("loadDevicesFromSaveStates", "false"),
    ("rewindEnabled", "false"),
    ("runaheadFrameCount", 0),
    ("saveStateOnExit", "false"),
    ("startPaused", "false"),
    ("fastForwardBoot", "false"),
    ("patchFastBoot", "false"),
    ("loadImagePatches", "false"),
    ("loadImageToRam", "false"),
    ("cdReadSpeedup", 1),
    ("cdSeekSpeedup", 1),
    ("card1Type", "None"),
    ("card2Type", "None"),
    ("multitapMode", "Disabled"),
    ("pad1Type", "DigitalController"),
    ("otherPadTypes", "None,None,None,None,None,None,None"),
    ("pioDeviceType", "None"),
    ("pioFlashWriteEnabled", "false"),
    ("pcDriveEnabled", "false"),
    ("pcDriveWritesEnabled", "false"),
    ("gdbServerEnabled", "true"),
    ("gdbServerPort", 2345),
    ("expectedCheatFileCount", 0),
    ("expectedGameSettingsFileCount", 0),
    ("expectedMemoryCardFileCount", 0),
    ("expectedSaveStateFileCount", 0),
    ("expectedPatchFileCount", 0),
    ("saveLoadStateHotkeysPresent", "false"),
    ("textureReplacementOrDumpEnabled", "false"),
)


def recompute_runtime_profile_v2_sha256() -> str:
    labels = tuple(name for name, _ in RUNTIME_PROFILE_V2_FIELDS)
    if len(labels) != len(set(labels)):
        raise CaptureRefusal("Runtime-profile-v2 manifest labels are duplicated.")
    text = "\n".join(
        (RUNTIME_PROFILE_V2_DOMAIN, *(f"{name}={value}" for name, value in RUNTIME_PROFILE_V2_FIELDS))
    ) + "\n"
    return hashlib.sha256(text.encode("utf-8", "strict")).hexdigest()


# Frozen for cross-language C# parity; self-test recomputes this exact manifest.
EXPECTED_RUNTIME_PROFILE_SHA256: Final = (
    "29d01f1b791e4dc08ebfc4d62cf5a1b91600a5c55ec175ac6da45b13123100ad"
)

CAPTURE_PIN_KEYS: Final = (
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
    "externalHomeBiosSha256", "hostNormalSettingsSentinelSha256",
)
MAX_PINNED_WATCHER_BYTES: Final = 1024 * 1024
MAX_WATCHER_STARTUP_DIAGNOSTIC_BYTES: Final = 4096
SCREEN_RECORDING_LEAF: Final = "session-screen-recording.mov"
CAPTURE_FRAME_LEAVES: Final = frozenset({
    "lone-pad.png",
    "edge-before.png",
    "edge-after.png",
    "cold-reset-after.png",
})
PINNED_WATCHER_BOOTSTRAP: Final = "\n".join((
    "import hashlib, os, signal, stat, sys",
    "signal.pthread_sigmask(signal.SIG_UNBLOCK, (signal.SIGINT, signal.SIGTERM))",
    "if len(sys.argv) < 6:",
    "    raise SystemExit('Pinned watcher bootstrap refused malformed arguments.')",
    "fd_text, expected_sha256, maximum_text, script_label = sys.argv[1:5]",
    "watcher_args = sys.argv[5:]",
    "if watcher_args not in (['--period', '0.1'], ['--self-test']):",
    "    raise SystemExit('Pinned watcher bootstrap refused unapproved arguments.')",
    "try:",
    "    fd = int(fd_text, 10)",
    "    maximum = int(maximum_text, 10)",
    "except ValueError:",
    "    raise SystemExit('Pinned watcher bootstrap refused a malformed bound.')",
    "if fd < 3 or maximum != 1048576:",
    "    raise SystemExit('Pinned watcher bootstrap refused an unsafe bound.')",
    "before = os.fstat(fd)",
    "def identity(value):",
    "    return (value.st_dev, value.st_ino, value.st_mode, value.st_nlink,",
    "            value.st_uid, value.st_gid, value.st_size,",
    "            value.st_mtime_ns, value.st_ctime_ns)",
    "if (not stat.S_ISREG(before.st_mode) or before.st_nlink != 1 or",
    "        before.st_uid != os.getuid() or stat.S_IMODE(before.st_mode) != 0o644 or",
    "        before.st_size <= 0 or before.st_size > maximum):",
    "    raise SystemExit('Pinned watcher bootstrap refused an unsafe inode.')",
    "os.lseek(fd, 0, os.SEEK_SET)",
    "source = bytearray()",
    "while len(source) <= maximum:",
    "    request = min(65536, maximum + 1 - len(source))",
    "    if request <= 0:",
    "        break",
    "    chunk = os.read(fd, request)",
    "    if not chunk:",
    "        break",
    "    source.extend(chunk)",
    "after = os.fstat(fd)",
    "if len(source) > maximum or len(source) != before.st_size:",
    "    raise SystemExit('Pinned watcher bootstrap refused a changed length.')",
    "if identity(after) != identity(before):",
    "    raise SystemExit('Pinned watcher bootstrap refused changed identity.')",
    "if hashlib.sha256(source).hexdigest() != expected_sha256:",
    "    raise SystemExit('Pinned watcher bootstrap refused changed bytes.')",
    "code = compile(bytes(source), script_label, 'exec',",
    "               flags=0, dont_inherit=True, optimize=0)",
    "os.close(fd)",
    "sys.argv = [script_label, *watcher_args]",
    "globals()['__file__'] = script_label",
    "globals()['__package__'] = None",
    "globals()['__spec__'] = None",
    "exec(code, globals(), globals())",
))
PINNED_SCREENCAPTURE_BOOTSTRAP: Final = "\n".join((
    "import os, signal, stat, sys",
    "signal.pthread_sigmask(signal.SIG_UNBLOCK, (signal.SIGINT, signal.SIGTERM))",
    "if len(sys.argv) != 8:",
    "    raise SystemExit('Pinned screencapture bootstrap refused malformed arguments.')",
    "fd_text, device_text, inode_text, leaf, window_text, kind, root_text = sys.argv[1:]",
    "try:",
    "    fd = int(fd_text, 10)",
    "    expected_device = int(device_text, 10)",
    "    expected_inode = int(inode_text, 10)",
    "    window_id = int(window_text, 10)",
    "except ValueError:",
    "    raise SystemExit('Pinned screencapture bootstrap refused malformed identity.')",
    "frame_leaves = {'lone-pad.png', 'edge-before.png', 'edge-after.png', 'cold-reset-after.png'}",
    "if (fd < 3 or expected_device < 0 or expected_inode <= 0 or window_id <= 0 or",
    "        root_text != '/' or",
    "        not ((kind == 'video' and leaf == 'session-screen-recording.mov') or",
    "             (kind == 'frame' and leaf in frame_leaves))):",
    "    raise SystemExit('Pinned screencapture bootstrap refused an unsafe target.')",
    "before = os.fstat(fd)",
    "if (not stat.S_ISDIR(before.st_mode) or before.st_dev != expected_device or",
    "        before.st_ino != expected_inode or before.st_uid != os.getuid() or",
    "        stat.S_IMODE(before.st_mode) != 0o700):",
    "    raise SystemExit('Pinned screencapture bootstrap refused an unsafe directory.')",
    "os.fchdir(fd)",
    "dot = os.stat('.', follow_symlinks=False)",
    "if ((dot.st_dev, dot.st_ino, dot.st_mode, dot.st_uid) !=",
    "        (before.st_dev, before.st_ino, before.st_mode, before.st_uid)):",
    "    raise SystemExit('Pinned screencapture bootstrap refused a rebound directory.')",
    "try:",
    "    os.lstat(leaf)",
    "except FileNotFoundError:",
    "    pass",
    "else:",
    "    raise SystemExit('Pinned screencapture bootstrap refused an existing target.')",
    "os.umask(0o077)",
    "os.close(fd)",
    "arguments = ['/usr/sbin/screencapture']",
    "if kind == 'video':",
    "    arguments.append('-v')",
    "arguments.extend(('-x', '-l' + str(window_id), leaf))",
    "os.execve('/usr/sbin/screencapture', arguments,",
    "          {'LANG': 'C', 'LC_ALL': 'C', 'PATH': '/usr/bin:/bin'})",
))

PINNED_SOURCE_FILES: Final = {
    CANDIDATE_CUE_RELATIVE_PATH: EXPECTED_CANDIDATE_CUE_SHA256,
    CANDIDATE_BIN_RELATIVE_PATH: EXPECTED_CANDIDATE_BIN_SHA256,
    (
        f"{CANDIDATE_DIRECTORY}/{CANDIDATE_STEM}-construction-plan.json"
    ): "e3054b63b4753399203a5eed0ef5b42f7d219957750d186532c5713c6aa906ae",
    (
        f"{CANDIDATE_DIRECTORY}/{CANDIDATE_STEM}-static-readback-receipt.json"
    ): "e4494a9364165951f6a7e54676574c085495a3f800b65e616da2193fa0d01be4",
    (
        f"{CANDIDATE_DIRECTORY}/{CANDIDATE_STEM}-runtime-checklist.md"
    ): "7023eb070d6ee1c840bd52013fb851ab53f8de52d2bc68f80a79323a0392363b",
    (
        f"{CANDIDATE_DIRECTORY}/{CANDIDATE_STEM}-location-guide.svg"
    ): "bab8c2851ca8743c43c91d70a9ec218796706e68c6fdfa50d977d013520c99a7",
    (
        f"{CANDIDATE_DIRECTORY}/{CANDIDATE_STEM}-Reveal-in-Finder.command"
    ): "e5e5c9485666e36aed50e6b0e9f43158f58fcf91f1fbb07f3207f8ccdc0be52a",
    WATCHER_RELATIVE_PATH: EXPECTED_WATCHER_SHA256,
}

FAILED_V1_BIN_RELATIVE_PATH: Final = (
    "_local/v5-stone-hill-level-replacement/unused-level-65-remote-blank-isolation/"
    "Unused-Level-65-Remote-Blank-Isolation-Sector216-RUNTIME-CANDIDATE.bin"
)
SUPERSEDED_V2_BIN_RELATIVE_PATH: Final = (
    "_local/v5-stone-hill-level-replacement/"
    "unused-level-65-remote-blank-collision-winding-repair-v2/"
    "Unused-Level-65-Remote-Blank-Collision-Winding-Repair-v2-"
    "Sector216-RUNTIME-CANDIDATE.bin"
)

REPAIR_LINEAGE_PREFIX_FIELDS: Final = (
    ("profileId", SOURCE_PROFILE_ID),
    ("baseBinSha256", "9e42b43bd1341b40915748432d1b2dc760e22a81c0a320ec09ae6a71ca2efcd8"),
    ("baseCueSha256", "3c8e28a8dac7b6a4621a5e72ba305047b5267e78720331893b9af80b8940dfa2"),
    ("candidateBinRelativePath", CANDIDATE_BIN_RELATIVE_PATH),
    ("candidateBinSha256", EXPECTED_CANDIDATE_BIN_SHA256),
    ("candidateCueRelativePath", CANDIDATE_CUE_RELATIVE_PATH),
    ("candidateCueSha256", EXPECTED_CANDIDATE_CUE_SHA256),
    ("lockedBaseRow80DataSha256", "7ddbf6d9a6ee6c0f64c8564a89e374ca0ca234ef608ec812aae68e8176de1dc0"),
    ("lockedBaseModelSha256", "1aa6950fe78e71ef4506d823fd33806c13a32838cdb00aadc4e35daffcf17f47"),
    ("environmentHeaderRepairV3BeforeRow80DataSha256", "a21e3ace8f16e981dc1d75c4d2210e96ba2833200bd4e3054b2a1c0bb540ade9"),
    ("environmentHeaderRepairV3AfterRow80DataSha256", "a759fba359722aedde2e5af4559f8bfe94a83cdeb263c364ebab5a8af7b92dcb"),
    ("environmentHeaderRepairV3BeforeModelSha256", "2ea39a0c19f51f16abfd3cae73d13df01191e16d70f31644ae8c14c977581b79"),
    ("environmentHeaderRepairV3AfterModelSha256", "00c88deb64eed8d829b504422e1dd21b1d6b4449c75126bdb2c0821d077c17b1"),
    ("outputCollisionTreeSha256", "c77618c5a80fce590c802b197945348d0b4fc04ab666e9b0b911e3a6ff9b2cb9"),
    ("outputCollisionBlocksSha256", "2d5838ffd9c991d017733f48cadbb9739131fbd6cec75f69a831c03442c1f34a"),
    ("staticDiffManifestSha256", "c37a00572bd023b9457d2a24944a4864c639c8e160958af135ac4c4f2e0b6a78"),
    ("deterministicStaticPlanSha256", "ea78191299f6f59c41a3bd09858b9ad6bc18dc5c783baca6455f6c5de67ae9a4"),
    ("rawSectorDiffSha256", "902a27be55ddb0cdfea96bde6b5c309c8e6fa4f7b587c27d45cf4afab3642c91"),
    ("constructionPlanRelativePath", f"{CANDIDATE_DIRECTORY}/{CANDIDATE_STEM}-construction-plan.json"),
    ("constructionPlanSha256", "e3054b63b4753399203a5eed0ef5b42f7d219957750d186532c5713c6aa906ae"),
    ("staticReadbackReceiptRelativePath", f"{CANDIDATE_DIRECTORY}/{CANDIDATE_STEM}-static-readback-receipt.json"),
    ("staticReadbackReceiptSha256", "e4494a9364165951f6a7e54676574c085495a3f800b65e616da2193fa0d01be4"),
    ("runtimeChecklistRelativePath", f"{CANDIDATE_DIRECTORY}/{CANDIDATE_STEM}-runtime-checklist.md"),
    ("runtimeChecklistSha256", "7023eb070d6ee1c840bd52013fb851ab53f8de52d2bc68f80a79323a0392363b"),
    ("locationGuideRelativePath", f"{CANDIDATE_DIRECTORY}/{CANDIDATE_STEM}-location-guide.svg"),
    ("locationGuideSha256", "bab8c2851ca8743c43c91d70a9ec218796706e68c6fdfa50d977d013520c99a7"),
    ("revealCommandRelativePath", f"{CANDIDATE_DIRECTORY}/{CANDIDATE_STEM}-Reveal-in-Finder.command"),
    ("revealCommandSha256", "e5e5c9485666e36aed50e6b0e9f43158f58fcf91f1fbb07f3207f8ccdc0be52a"),
)

REPAIR_LINEAGE_SUFFIX_FIELDS: Final = (
    ("failedV1ProfileId", "unused-level-65-remote-blank-isolation-sector216-static-clean-usa-v1"),
    ("failedV1BinRelativePath", FAILED_V1_BIN_RELATIVE_PATH),
    ("failedV1BinSha256", FAILED_V1_BIN_SHA256),
    ("failedV1OutputDataSha256", "8d10aa62b134414aec80eec10d1fcede13bd55806ee13f6eb7c752f0fa1a9061"),
    ("failedV1OutputModelSha256", "72c3fb268f63952ad37707e33be4de4039d4bb73af42e25b52e618e7ec9a5820"),
    ("collisionWindingV2ProfileId", "unused-level-65-remote-blank-collision-winding-repair-sector216-static-clean-usa-v2"),
    ("collisionWindingV2BinRelativePath", SUPERSEDED_V2_BIN_RELATIVE_PATH),
    ("collisionWindingV2BinSha256", SUPERSEDED_V2_BIN_SHA256),
    ("collisionWindingV2OutputDataSha256", "a21e3ace8f16e981dc1d75c4d2210e96ba2833200bd4e3054b2a1c0bb540ade9"),
    ("collisionWindingV2OutputModelSha256", "2ea39a0c19f51f16abfd3cae73d13df01191e16d70f31644ae8c14c977581b79"),
    ("collisionWindingV2RepairDiffManifestSha256", "0fae1b1035fa0bae3faf82e1b4d172a39c51d1cd5ce425a49321e3f01cf66e25"),
    ("collisionWindingV2ChangedByteCount", 4),
    ("collisionWindingV2RangeCount", 2),
    ("collisionWindingV2RawSectorLba", 54_581),
    ("collisionWindingV2InverseVerified", "true"),
    ("failedV1RetestForbidden", "true"),
    ("supersededV2SectorSha256", "08f1de7187887ee4bc05bb0dbb0d9fd0c819ad015a39b6f1ae83a1e745ddc07a"),
    ("environmentHeaderRepairV3AfterSectorSha256", "7d71ebc9558c10896b83d1d8a79e6224cd6a48630b7a335d25b46935a572759e"),
    ("environmentHeaderRepairV3AfterEnvironmentSha256", "c4097bfdc54cf9a29864f5195e52c44151478d67e515fdb27f7c4c4f24d1ff06"),
    ("environmentHeaderRepairV3DiffManifestSha256", "fe85cd987e9d39c3ca10b944371c76fd7f7247e11db15e9eb6baed0d1925d875"),
    ("environmentHeaderRepairV3ChangedByteCount", 1),
    ("environmentHeaderRepairV3RangeCount", 1),
    ("environmentHeaderRepairV3RawSectorLba", 54_437),
    ("environmentHeaderRepairV3BeforeByte", 9),
    ("environmentHeaderRepairV3AfterByte", 8),
    ("environmentHeaderRepairV3InverseVerified", "true"),
    ("supersededV2RetestForbidden", "true"),
)

SOURCE_CLOSURE_PREFIX_FIELDS: Final = (
    ("watcherRelativePath", WATCHER_RELATIVE_PATH),
    ("watcherSha256", EXPECTED_WATCHER_SHA256),
    ("watcherMode", "0644"),
    ("profileBuilderRelativePath", PROFILE_BUILDER_RELATIVE_PATH),
    ("profileBuilderSha256", EXPECTED_PROFILE_BUILDER_SHA256),
    ("profileBuilderMode", "0644"),
    ("profileVerifierRelativePath", PROFILE_VERIFIER_RELATIVE_PATH),
    ("profileVerifierSha256", EXPECTED_PROFILE_VERIFIER_SHA256),
    ("profileVerifierMode", "0644"),
    ("automationRelativePath", AUTOMATION_RELATIVE_PATH),
    ("automationSha256", EXPECTED_AUTOMATION_SHA256),
    ("automationMode", "0755"),
    ("captureCoordinatorRelativePath", CAPTURE_COORDINATOR_RELATIVE_PATH),
    ("captureCoordinatorMode", "0755"),
)

SOURCE_CLOSURE_SUFFIX_FIELDS: Final = (
    ("runtimeProfileManifestDomain", RUNTIME_PROFILE_V2_DOMAIN),
    ("runtimeProfileSha256", EXPECTED_RUNTIME_PROFILE_SHA256),
    *REPAIR_LINEAGE_PREFIX_FIELDS,
    *REPAIR_LINEAGE_SUFFIX_FIELDS,
)

EXPECTED_SOURCE_CLOSURE_PREFIX_LABELS: Final = (
    "watcherRelativePath",
    "watcherSha256",
    "watcherMode",
    "profileBuilderRelativePath",
    "profileBuilderSha256",
    "profileBuilderMode",
    "profileVerifierRelativePath",
    "profileVerifierSha256",
    "profileVerifierMode",
    "automationRelativePath",
    "automationSha256",
    "automationMode",
    "captureCoordinatorRelativePath",
    "captureCoordinatorMode",
)
EXPECTED_SOURCE_CLOSURE_SUFFIX_LABELS: Final = (
    "runtimeProfileManifestDomain",
    "runtimeProfileSha256",
    *(name for name, _ in REPAIR_LINEAGE_PREFIX_FIELDS),
    *(name for name, _ in REPAIR_LINEAGE_SUFFIX_FIELDS),
)

CHECK_MARKER_PLANS: Final = {
    "candidate-primary-edge-0": (
        ("candidate.completed-landing-hud4", 3_500_000),
        ("candidate.hands-off-20-seconds", 20_500_000),
        ("candidate.camera-360-twice", 6_500_000),
        ("candidate.walk-center-perimeter-both-directions", 8_500_000),
        ("candidate.jump-land-center-each-corner", 8_500_000),
    ),
    "retail-town-square": (
        ("retail.town-square.terrain", 3_500_000),
        ("retail.town-square.collision", 5_500_000),
        ("retail.town-square.mobys", 5_500_000),
        ("retail.town-square.camera", 5_500_000),
        ("retail.town-square.death-respawn", 5_500_000),
        ("retail.town-square.music", 10_500_000),
        ("retail.town-square.exit", 3_500_000),
        ("retail.town-square.totals", 2_500_000),
    ),
    "retail-gnastys-loot": (
        ("retail.gnastys-loot.gameplay", 5_500_000),
        ("retail.gnastys-loot.exit", 3_500_000),
    ),
    "retail-sunny-flight": (
        ("retail.sunny-flight.gameplay", 5_500_000),
        ("retail.sunny-flight.timer", 3_500_000),
        ("retail.sunny-flight.exit", 3_500_000),
    ),
}

LOAD_SEQUENCE_INSTRUCTIONS: Final[Mapping[str, str]] = MappingProxyType({
    "candidate-primary-edge-0": "Select; R1, R2, L1, L2, R1, L1, R2, L2; Left; Down",
    "candidate-edge-1": "Select; R1, R2, L1, L2, R1, L1, R2, L2; Left; Down",
    "candidate-edge-2": "Select; R1, R2, L1, L2, R1, L1, R2, L2; Left; Down",
    "candidate-cold-reset": "Select; R1, R2, L1, L2, R1, L1, R2, L2; Left; Down",
    "retail-town-square": "Select; R1, R2, L1, L2, R1, L1, R2, L2; Cross; Triangle",
    "retail-gnastys-loot": "Select; R1, R2, L1, L2, R1, L1, R2, L2; Left; Right",
    "retail-sunny-flight": "Select; R1, R2, L1, L2, R1, L1, R2, L2; Cross; Down",
})

MARKER_INSTRUCTIONS: Final[Mapping[str, str]] = MappingProxyType({
    "candidate.camera-360-twice": (
        "Rotate the camera one full circle clockwise and one full circle counterclockwise; "
        "keep the lone pad visible and return to the original spawn view."
    ),
    "candidate.walk-center-perimeter-both-directions": (
        "Walk across the pad center, then traverse the complete perimeter in both directions; "
        "verify there is no pass-through, snag, hover, or hidden support."
    ),
    "candidate.jump-land-center-each-corner": (
        "Jump and land once at the center and once near each of the three corners; remain on "
        "the visible surface after every landing."
    ),
    "retail.town-square.terrain": "Move through Town Square and inspect its normal visible terrain.",
    "retail.town-square.collision": "Walk, charge, and jump against Town Square terrain to confirm normal collision.",
    "retail.town-square.mobys": "Approach and interact with normal Town Square characters, enemies, and objects.",
    "retail.town-square.camera": "Rotate and recenter the camera during normal Town Square gameplay.",
    "retail.town-square.death-respawn": "Deliberately die once in Town Square and wait for normal controllable respawn.",
    "retail.town-square.music": "Remain in controllable Town Square gameplay and listen for uninterrupted normal music.",
    "retail.town-square.exit": "Use Town Square's normal exit and observe the expected transition.",
    "retail.town-square.totals": "Open the relevant totals display and verify Town Square totals remain normal.",
    "retail.gnastys-loot.gameplay": "Move, jump, charge, and use the camera during normal Gnasty's Loot gameplay.",
    "retail.gnastys-loot.exit": "Use Gnasty's Loot's normal exit and observe the expected transition.",
    "retail.sunny-flight.gameplay": "Fly and use normal controls during Sunny Flight gameplay.",
    "retail.sunny-flight.timer": "Observe the Sunny Flight timer counting normally during active gameplay.",
    "retail.sunny-flight.exit": "Exit Sunny Flight normally and observe the expected transition.",
})

WATCHER_LIVE_PREAMBLE: Final = (
    "WARNING: connecting to this DuckStation GDB server pauses a running game exactly once.\n"
    "This watcher sends only the three approved m reads and protocol ACKs; it cannot resume the game.\n"
    "Connected once to 127.0.0.1:2345. DuckStation is now paused.\n"
    "Manually Resume in DuckStation, leave this watcher connected, then press Return here to begin the 0.1-second-period watch: "
    "Watching one numbered near-contemporaneous tuple per period with monotonic elapsed milliseconds; press Ctrl-C to stop cleanly.\n"
)
WATCHER_CLEAN_TRAILER: Final = (
    "\n\nStopped cleanly; the socket is closed and no control packet was sent. "
    "If DuckStation was not manually resumed, it remains paused.\n"
)
WATCHER_SAMPLE_RE: Final = re.compile(
    r"^(?P<at>[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:"
    r"[0-9]{2}\.[0-9]{3}Z) sample=(?P<ordinal>0|[1-9][0-9]*) "
    r"elapsed-ms=(?P<elapsed>0|[1-9][0-9]*) lives=(?P<lives>0|[1-9][0-9]*) "
    r"death-state=(?P<state>0|[1-9][0-9]*)\((?P<label>[^)]+)\) "
    r"player-Z=(?P<z>-?(?:0|[1-9][0-9]*)) "
    r"\[Z<0x400:(?P<marker>YES|no)\] raw=(?P<livesRaw>[0-9a-f]{8})/"
    r"(?P<stateRaw>[0-9a-f]{8})/(?P<zRaw>[0-9a-f]{8})$",
    re.ASCII,
)


class CaptureRefusal(RuntimeError):
    """A fail-closed capture boundary was not satisfied."""


class DeferredTerminationSignals:
    """Defer main-thread termination until an owned-child handoff is complete."""

    def __init__(self) -> None:
        self._old_handlers: dict[int, Any] = {}
        self._old_mask: set[signal.Signals] | None = None
        self._pending_signal: int | None = None

    def __enter__(self) -> "DeferredTerminationSignals":
        def defer(signum: int, frame: Any) -> None:
            del frame
            if self._pending_signal is None:
                self._pending_signal = signum

        try:
            self._old_mask = signal.pthread_sigmask(
                signal.SIG_BLOCK, (signal.SIGINT, signal.SIGTERM)
            )
            for signum in (signal.SIGINT, signal.SIGTERM):
                self._old_handlers[signum] = signal.getsignal(signum)
                signal.signal(signum, defer)
        except BaseException:
            for signum, handler in self._old_handlers.items():
                signal.signal(signum, handler)
            self._old_handlers.clear()
            if self._old_mask is not None:
                signal.pthread_sigmask(signal.SIG_SETMASK, self._old_mask)
                self._old_mask = None
            raise
        return self

    def __exit__(self, exc_type: Any, exc: Any, traceback: Any) -> None:
        del exc, traceback
        first_error: BaseException | None = None
        if self._old_mask is not None:
            try:
                signal.pthread_sigmask(signal.SIG_SETMASK, self._old_mask)
            except BaseException as error:
                first_error = error
            self._old_mask = None
        for signum, handler in self._old_handlers.items():
            try:
                signal.signal(signum, handler)
            except BaseException as error:
                if first_error is None:
                    first_error = error
        self._old_handlers.clear()
        if exc_type is not None:
            return
        if first_error is not None:
            raise first_error
        if self._pending_signal is not None:
            raise KeyboardInterrupt(
                f"Deferred termination signal {self._pending_signal} after child ownership handoff."
            )


class CoordinatorLease:
    """Stable cross-tool authority plus the legacy staging-inode lease."""

    def __init__(self, repository_root: pathlib.Path) -> None:
        self.staging = AnchoredStaging(repository_root)
        self.repository_root = pathlib.Path(os.path.abspath(repository_root))
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
    def _require_private_parent(info: os.stat_result) -> None:
        if (
            not stat.S_ISDIR(info.st_mode)
            or info.st_uid != os.getuid()
            or stat.S_IMODE(info.st_mode) != 0o700
        ):
            raise CaptureRefusal(
                "Capture coordinator authority parent is not owned mode 0700."
            )

    @staticmethod
    def _require_lock_inode(info: os.stat_result, label: str) -> None:
        if (
            not stat.S_ISREG(info.st_mode)
            or info.st_nlink != 1
            or info.st_uid != os.getuid()
            or stat.S_IMODE(info.st_mode) != 0o600
        ):
            raise CaptureRefusal(f"{label} inode is unsafe.")

    @staticmethod
    def _file_identity(info: os.stat_result) -> tuple[int, int, int, int, int]:
        return (
            info.st_dev, info.st_ino, info.st_mode, info.st_uid, info.st_nlink
        )

    @staticmethod
    def _directory_identity(info: os.stat_result) -> tuple[int, int, int, int]:
        return info.st_dev, info.st_ino, info.st_mode, info.st_uid

    def __enter__(self) -> "CoordinatorLease":
        if self.authority_fd >= 0 or self.fd >= 0:
            raise CaptureRefusal("Coordinator lease is already entered.")
        self.staging.ensure_prefix()
        self.authority_parent_fd = _open_secure_directory(
            self.repository_root / CAPTURE_COORDINATOR_AUTHORITY_DIRECTORY
        )
        try:
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
                raise CaptureRefusal(
                    "Another capture coordinator already owns the lease."
                ) from None
            self.authority_locked = True
            self.authority_identity = self._file_identity(authority)
            self._assert_stable_authority_current()

            self.prefix_fd = _open_secure_directory(self.staging.prefix)
            prefix = os.fstat(self.prefix_fd)
            if (
                prefix.st_uid != os.getuid()
                or stat.S_IMODE(prefix.st_mode) != 0o700
            ):
                raise CaptureRefusal(
                    "Capture-staging root is not owned mode 0700."
                )
            self.prefix_identity = self._directory_identity(prefix)
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
            self._require_lock_inode(info, "Coordinator lease")
            try:
                fcntl.flock(self.fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
            except BlockingIOError:
                raise CaptureRefusal(
                    "Another capture coordinator already owns the lease."
                ) from None
            self.staging_locked = True
            self.lock_identity = self._file_identity(info)
            self.assert_current()
            return self
        except BaseException:
            self._close_all(validate=False)
            raise

    def _assert_stable_authority_current(self) -> None:
        if (
            self.authority_parent_fd < 0
            or self.authority_fd < 0
            or self.authority_parent_identity is None
            or self.authority_identity is None
        ):
            raise CaptureRefusal(
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
            raise CaptureRefusal(
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
            raise CaptureRefusal(
                "Capture coordinator stable authority path was rebound."
            )

    def assert_current(self) -> None:
        self._assert_stable_authority_current()
        if (
            self.prefix_fd < 0
            or self.fd < 0
            or self.prefix_identity is None
            or self.lock_identity is None
        ):
            raise CaptureRefusal("Capture coordinator staging lease is not active.")
        retained_prefix = os.fstat(self.prefix_fd)
        current_prefix_fd = _open_secure_directory(self.staging.prefix)
        try:
            current_prefix = os.fstat(current_prefix_fd)
        finally:
            os.close(current_prefix_fd)
        if (
            self._directory_identity(retained_prefix) != self.prefix_identity
            or self._directory_identity(current_prefix) != self.prefix_identity
        ):
            raise CaptureRefusal("Capture-staging lease directory was rebound.")
        retained_lock = os.fstat(self.fd)
        current_lock = os.stat(
            CAPTURE_COORDINATOR_STAGING_LOCK_NAME,
            dir_fd=self.prefix_fd,
            follow_symlinks=False,
        )
        self._require_lock_inode(retained_lock, "Coordinator lease")
        self._require_lock_inode(current_lock, "Coordinator lease")
        if (
            self._file_identity(retained_lock) != self.lock_identity
            or self._file_identity(current_lock) != self.lock_identity
        ):
            raise CaptureRefusal("Capture coordinator staging lease was rebound.")

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


@dataclasses.dataclass(frozen=True)
class ClockSample:
    at_utc: str
    monotonic_microseconds: int


@dataclasses.dataclass(frozen=True)
class CaptureRunState:
    run_id: str
    run_path: pathlib.Path
    session_count: int
    previous_hash: str
    previous_clock: ClockSample | None
    previous_after_profile: dict[str, Any] | None
    seen_raw_hashes: frozenset[str]
    seen_process_fingerprints: frozenset[str]


@dataclasses.dataclass(frozen=True)
class FileIdentity:
    real_path: str
    device: int
    inode: int
    byte_length: int
    sha256: str
    bytes_value: bytes | None = None


@dataclasses.dataclass(frozen=True)
class OpenFileObservation:
    fd: str
    device: int
    inode: int
    byte_length: int
    link_count: int
    real_path: str


@dataclasses.dataclass(frozen=True)
class ProcessObservation:
    pid: int
    parent_pid: int
    start_seconds: int
    start_microseconds: int
    process_started_at_utc: str
    executable: FileIdentity
    argv: tuple[str, ...]
    argv_nul_sha256: str
    environment_nul_sha256: str
    external_home_real_path: str
    open_files: tuple[OpenFileObservation, ...]

    @property
    def open_paths(self) -> frozenset[str]:
        return frozenset(item.real_path for item in self.open_files)


def _runtime_environment_assignments(expected_external_home: str) -> tuple[str, ...]:
    if (
        type(expected_external_home) is not str
        or not os.path.isabs(expected_external_home)
        or os.path.normpath(expected_external_home) != expected_external_home
        or os.path.realpath(expected_external_home) != expected_external_home
    ):
        raise CaptureRefusal("Pinned runtime HOME is not one canonical real path.")
    assignments = (
        f"HOME={expected_external_home}",
        *RUNTIME_ENVIRONMENT_FIXED_ASSIGNMENTS,
    )
    if (
        assignments[-1]
        != f"{RUNTIME_ENVIRONMENT_SENTINEL_NAME}="
        f"{RUNTIME_ENVIRONMENT_SENTINEL_VALUE}"
    ):
        raise CaptureRefusal("Pinned runtime environment sentinel is not terminal.")
    return assignments


def _runtime_environment_nul_sha256(expected_external_home: str) -> str:
    return sha256_bytes(
        b"\0".join(
            assignment.encode("utf-8", "strict")
            for assignment in _runtime_environment_assignments(
                expected_external_home
            )
        )
        + b"\0"
    )


def _decode_environment_assignment(raw: bytes, *, label: str) -> tuple[str, str]:
    if type(raw) is not bytes or not raw or b"\0" in raw:
        raise CaptureRefusal(f"{label} environment item is empty or contains NUL.")
    try:
        text = raw.decode("utf-8", "strict")
    except UnicodeDecodeError as exc:
        raise CaptureRefusal(f"{label} environment is not UTF-8.") from exc
    if "=" not in text:
        raise CaptureRefusal(f"{label} environment item has no assignment.")
    name, value = text.split("=", 1)
    if (
        not name
        or any(ord(character) < 0x21 or ord(character) == 0x7f for character in name)
        or "=" in name
    ):
        raise CaptureRefusal(f"{label} environment key is malformed.")
    return name, value


def _expected_environment_bytes(
    expected_external_home: str,
    expected_environment: tuple[bytes, ...] | None,
) -> tuple[tuple[bytes, ...], dict[str, str]]:
    if expected_environment is None:
        expected_environment = tuple(
            item.encode("utf-8", "strict")
            for item in _runtime_environment_assignments(expected_external_home)
        )
    if (
        type(expected_environment) is not tuple
        or not expected_environment
        or len(expected_environment) > 4096
    ):
        raise CaptureRefusal("Expected process environment manifest is invalid.")
    parsed: dict[str, str] = {}
    for raw in expected_environment:
        name, value = _decode_environment_assignment(raw, label="Expected process")
        if name in parsed:
            raise CaptureRefusal("Expected process environment key is duplicated.")
        parsed[name] = value
    if parsed.get("HOME") != expected_external_home:
        raise CaptureRefusal("Expected process environment does not bind pinned HOME.")
    return expected_environment, parsed


def _require_apple_auxiliary_tail(data: bytes, cursor: int) -> None:
    visible: list[tuple[str, str]] = []
    while cursor < len(data):
        while cursor < len(data) and data[cursor] == 0:
            cursor += 1
        if cursor == len(data):
            break
        end = data.find(b"\0", cursor)
        if end < 0:
            raise CaptureRefusal("KERN_PROCARGS2 Apple auxiliary tail is unterminated.")
        visible.append(_decode_environment_assignment(
            data[cursor:end], label="KERN_PROCARGS2 Apple auxiliary"
        ))
        cursor = end + 1
    names = tuple(name for name, _value in visible)
    if names != APPLE_AUXILIARY_VISIBLE_KEY_ORDER:
        raise CaptureRefusal(
            "KERN_PROCARGS2 has an unexpected item after the exact launch environment."
        )
    for name, value in visible:
        if name in APPLE_AUXILIARY_REDACTED_VALUE_KEYS:
            valid = value == ""
        elif name in {"executable_file", "dyld_file"}:
            valid = re.fullmatch(r"0x[0-9a-f]+,0x[0-9a-f]+", value) is not None
        elif name in {"executable_cdhash", "executable_boothash"}:
            valid = re.fullmatch(r"[0-9a-f]{40}", value) is not None
        elif name == "security_config":
            valid = re.fullmatch(r"0x[0-9a-f]+", value) is not None
        else:
            valid = False
        if not valid:
            raise CaptureRefusal(
                "KERN_PROCARGS2 Apple auxiliary value grammar changed."
            )


def _parse_kern_procargs2(
    data: bytes,
    expected_external_home: str,
    *,
    expected_environment: tuple[bytes, ...] | None = None,
) -> tuple[tuple[str, ...], str, str]:
    """Parse exact argv and a caller-owned environment manifest from PROCARGS2."""
    if (
        type(data) is not bytes
        or not 4 <= len(data) <= 4 * 1024 * 1024
        or type(expected_external_home) is not str
        or not os.path.isabs(expected_external_home)
        or os.path.normpath(expected_external_home) != expected_external_home
    ):
        raise CaptureRefusal("KERN_PROCARGS2 input or expected HOME is invalid.")
    argc = int.from_bytes(data[:4], sys.byteorder, signed=True)
    if not 0 < argc <= 4096:
        raise CaptureRefusal("KERN_PROCARGS2 argc is invalid.")
    cursor = 4
    executable_end = data.find(b"\0", cursor)
    if executable_end <= cursor:
        raise CaptureRefusal("KERN_PROCARGS2 executable path is missing.")
    try:
        data[cursor:executable_end].decode("utf-8", "strict")
    except UnicodeDecodeError as exc:
        raise CaptureRefusal("KERN_PROCARGS2 executable is not UTF-8.") from exc
    cursor = executable_end
    while cursor < len(data) and data[cursor] == 0:
        cursor += 1
    argv: list[str] = []
    while len(argv) < argc:
        if cursor >= len(data):
            raise CaptureRefusal("KERN_PROCARGS2 argv is truncated.")
        end = data.find(b"\0", cursor)
        if end <= cursor:
            raise CaptureRefusal("KERN_PROCARGS2 argv contains an empty or unterminated item.")
        try:
            argv.append(data[cursor:end].decode("utf-8", "strict"))
        except UnicodeDecodeError as exc:
            raise CaptureRefusal("KERN_PROCARGS2 argv is not UTF-8.") from exc
        cursor = end + 1
    expected_raw, expected_values = _expected_environment_bytes(
        expected_external_home, expected_environment
    )
    environment_raw: list[bytes] = []
    environment: dict[str, str] = {}
    for expected_item in expected_raw:
        if cursor >= len(data) or data[cursor] == 0:
            raise CaptureRefusal(
                "Observed process omits the exact pinned launch environment."
            )
        end = data.find(b"\0", cursor)
        if end < 0:
            raise CaptureRefusal("KERN_PROCARGS2 environment is unterminated.")
        raw = data[cursor:end]
        name, value = _decode_environment_assignment(
            raw, label="KERN_PROCARGS2"
        )
        if name in environment:
            raise CaptureRefusal("KERN_PROCARGS2 environment key is duplicated.")
        if raw != expected_item:
            raise CaptureRefusal(
                "Observed process environment differs from the exact pinned launch manifest."
            )
        environment[name] = value
        environment_raw.append(raw)
        cursor = end + 1
    if environment != expected_values:
        raise CaptureRefusal("Observed process environment manifest is reordered.")
    _require_apple_auxiliary_tail(data, cursor)
    home = environment.get("HOME")
    if (
        home != expected_external_home
        or not os.path.isabs(home)
        or os.path.normpath(home) != home
        or os.path.realpath(home) != home
    ):
        raise CaptureRefusal("Observed DuckStation HOME is not the pinned external HOME.")
    environment_nul = b"\0".join(environment_raw) + b"\0"
    return tuple(argv), sha256_bytes(environment_nul), home


def _candidate_process_lifecycle_ready(
    process: ProcessObservation,
    cue_real_path: str,
    bin_real_path: str,
) -> bool:
    return (
        process.argv == (process.executable.real_path, cue_real_path)
        and bin_real_path in process.open_paths
        and cue_real_path not in process.open_paths
    )


@dataclasses.dataclass(frozen=True)
class WindowObservation:
    window_id: int
    owner_pid: int
    x: int
    y: int
    width: int
    height: int


@dataclasses.dataclass(frozen=True)
class ChildStop:
    requested: ClockSample
    observed: ClockSample


class OwnedChild(Protocol):
    pid: int

    def poll(self) -> int | None: ...

    def wait(self, timeout: float | None = None) -> int: ...

    def send_signal(self, sig: int) -> None: ...


class Boundary(Protocol):
    repository_root: pathlib.Path

    def clock(self) -> ClockSample: ...

    def boot_identity(self) -> str: ...

    def require_capture_environment(self) -> None: ...

    def duckstation_processes(self) -> list[ProcessObservation]: ...

    def listener_pids(self) -> set[int]: ...

    def wait_for_duckstation(self, cue_real_path: str) -> ProcessObservation: ...

    def wait_for_process_exit(self, process: ProcessObservation) -> ClockSample: ...

    def observe_window(self, pid: int) -> WindowObservation: ...

    def confirm(self, prompt: str) -> ClockSample: ...

    def confirm_before_deadline(
        self, prompt: str, deadline_monotonic_microseconds: int
    ) -> ClockSample: ...

    def fresh_confirm_before_deadline(
        self, prompt: str, deadline_monotonic_microseconds: int
    ) -> ClockSample: ...

    def announce(self, message: str) -> None: ...

    def wait_for_fresh_return_after(
        self,
        started: ClockSample,
        minimum_microseconds: int,
        eligible_prompt: str,
        deadline_monotonic_microseconds: int | None = None,
    ) -> ClockSample: ...

    def start_watcher(
        self,
        stdout: BinaryIO,
        owned_children: list[tuple[str, OwnedChild]],
    ) -> tuple[OwnedChild, BinaryIO]: ...

    def wait_for_watcher_connection(
        self, child: OwnedChild, watcher_path: pathlib.Path
    ) -> ClockSample: ...

    def start_recording(
        self,
        window: WindowObservation,
        session_path: pathlib.Path,
        destination_leaf: str,
        owned_children: list[tuple[str, OwnedChild]],
    ) -> OwnedChild: ...

    def capture_frame(
        self,
        window: WindowObservation,
        session_path: pathlib.Path,
        destination_leaf: str,
        deadline_monotonic_microseconds: int,
    ) -> None: ...

    def stop_owned_child(
        self,
        child: OwnedChild,
        label: str,
        deadline_monotonic_microseconds: int | None = None,
    ) -> ChildStop: ...

    def ensure_owned_child_reaped(
        self, child: OwnedChild, label: str
    ) -> BaseException | None: ...


def _validate_i_json(value: Any) -> None:
    if value is None or isinstance(value, (str, bool)):
        if isinstance(value, str):
            value.encode("utf-8", "strict")
        return
    if isinstance(value, int) and not isinstance(value, bool):
        if not -MAX_SAFE_INTEGER <= value <= MAX_SAFE_INTEGER:
            raise CaptureRefusal("Canonical JSON integer exceeds the I-JSON range.")
        return
    if isinstance(value, list):
        for item in value:
            _validate_i_json(item)
        return
    if isinstance(value, dict):
        for key, item in value.items():
            if not isinstance(key, str):
                raise CaptureRefusal("Canonical JSON object key is not text.")
            key.encode("utf-8", "strict")
            _validate_i_json(item)
        return
    raise CaptureRefusal("Canonical JSON permits only objects, arrays, strings, integers, booleans, and null.")


def canonical_bytes(value: Any) -> bytes:
    _validate_i_json(value)
    return json.dumps(
        value,
        ensure_ascii=False,
        allow_nan=False,
        sort_keys=True,
        separators=(",", ":"),
    ).encode("utf-8", "strict")


def sha256_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def _parse_boot_session_uuid(raw: bytes) -> str:
    if type(raw) is not bytes or len(raw) != 37 or raw[-1:] != b"\0":
        raise CaptureRefusal("Boot-session UUID sysctl bytes are not exact.")
    try:
        value = raw[:-1].decode("ascii", "strict")
    except UnicodeDecodeError as exc:
        raise CaptureRefusal("Boot-session UUID is not canonical ASCII.") from exc
    if not CANONICAL_UPPER_UUID_RE.fullmatch(value) or value == ZERO_UUID:
        raise CaptureRefusal("Boot-session UUID is not canonical nonzero uppercase UUID text.")
    return value


def _parse_boot_time(raw: bytes, *, now_seconds: float | None = None) -> tuple[int, int]:
    if type(raw) is not bytes or len(raw) != 16:
        raise CaptureRefusal("Boot-time sysctl bytes are not one exact native signed64 pair.")
    seconds, microseconds = struct.unpack("=qq", raw)
    if seconds <= 0 or not 0 <= microseconds <= 999_999:
        raise CaptureRefusal("Boot time has an invalid seconds or microseconds field.")
    current = time.time() if now_seconds is None else now_seconds
    if (
        type(current) not in {int, float}
        or not math.isfinite(current)
        or current <= 0
    ):
        raise CaptureRefusal("Current time for boot validation is invalid.")
    if seconds + microseconds / 1_000_000 > current:
        raise CaptureRefusal("Boot time is in the future.")
    return seconds, microseconds


def _parse_platform_identity(raw: bytes) -> tuple[str, str]:
    if type(raw) is not bytes or not raw or len(raw) > 4 * 1024 * 1024:
        raise CaptureRefusal("Platform-identity plist bytes are empty or oversized.")
    try:
        value = plistlib.loads(raw)
    except Exception as exc:
        raise CaptureRefusal("Platform-identity plist is malformed.") from exc
    if type(value) is not list or len(value) != 1 or type(value[0]) is not dict:
        raise CaptureRefusal("Platform-identity plist is not one exact dictionary entry.")
    platform_uuid = value[0].get("IOPlatformUUID")
    serial = value[0].get("IOPlatformSerialNumber")
    if (
        type(platform_uuid) is not str
        or not CANONICAL_UPPER_UUID_RE.fullmatch(platform_uuid)
        or platform_uuid == ZERO_UUID
    ):
        raise CaptureRefusal("IOPlatformUUID is not canonical nonzero uppercase UUID text.")
    if (
        type(serial) is not str
        or not PLATFORM_SERIAL_RE.fullmatch(serial)
        or serial in PLATFORM_SERIAL_PLACEHOLDERS
        or len(set(serial)) == 1
    ):
        raise CaptureRefusal("IOPlatformSerialNumber is not a non-placeholder uppercase serial.")
    return platform_uuid, serial


def _boot_machine_identity_sha256(
    boot_session_uuid: str,
    boot_seconds: int,
    boot_microseconds: int,
    platform_uuid: str,
    platform_serial: str,
) -> str:
    if (
        type(boot_session_uuid) is not str
        or not CANONICAL_UPPER_UUID_RE.fullmatch(boot_session_uuid)
        or boot_session_uuid == ZERO_UUID
        or type(boot_seconds) is not int
        or boot_seconds <= 0
        or type(boot_microseconds) is not int
        or not 0 <= boot_microseconds <= 999_999
        or type(platform_uuid) is not str
        or not CANONICAL_UPPER_UUID_RE.fullmatch(platform_uuid)
        or platform_uuid == ZERO_UUID
        or type(platform_serial) is not str
        or not PLATFORM_SERIAL_RE.fullmatch(platform_serial)
        or platform_serial in PLATFORM_SERIAL_PLACEHOLDERS
        or len(set(platform_serial)) == 1
    ):
        raise CaptureRefusal("Boot-machine identity fields are not canonical.")
    manifest = (
        "macos-boot-machine-identity-v2\n"
        f"bootSessionUuid={boot_session_uuid}\n"
        f"bootTimeSeconds={boot_seconds}\n"
        f"bootTimeMicroseconds={boot_microseconds:06d}\n"
        f"ioPlatformUuid={platform_uuid}\n"
        f"ioPlatformSerialNumber={platform_serial}\n"
    )
    return sha256_bytes(manifest.encode("ascii", "strict"))


def recompute_source_closure(capture_coordinator_sha256: str) -> str:
    if not SHA256_RE.fullmatch(capture_coordinator_sha256):
        raise CaptureRefusal("Capture coordinator hash is not lowercase SHA-256.")
    prefix_labels = tuple(name for name, _ in SOURCE_CLOSURE_PREFIX_FIELDS)
    suffix_labels = tuple(name for name, _ in SOURCE_CLOSURE_SUFFIX_FIELDS)
    if (
        prefix_labels != EXPECTED_SOURCE_CLOSURE_PREFIX_LABELS
        or suffix_labels != EXPECTED_SOURCE_CLOSURE_SUFFIX_LABELS
        or len(set((*prefix_labels, "captureCoordinatorSha256", *suffix_labels)))
        != len(prefix_labels) + len(suffix_labels) + 1
    ):
        raise CaptureRefusal(
            "V3 source-closure labels are reordered, missing, duplicated, or unknown."
        )
    values = dict((*SOURCE_CLOSURE_PREFIX_FIELDS, *SOURCE_CLOSURE_SUFFIX_FIELDS))
    if (
        values["environmentHeaderRepairV3BeforeRow80DataSha256"]
        != values["collisionWindingV2OutputDataSha256"]
        or values["environmentHeaderRepairV3BeforeModelSha256"]
        != values["collisionWindingV2OutputModelSha256"]
        or values["environmentHeaderRepairV3BeforeByte"] != 9
        or values["environmentHeaderRepairV3AfterByte"] != 8
        or values["environmentHeaderRepairV3ChangedByteCount"] != 1
        or values["environmentHeaderRepairV3RangeCount"] != 1
        or values["environmentHeaderRepairV3RawSectorLba"] != 54_437
        or values["failedV1RetestForbidden"] != "true"
        or values["supersededV2RetestForbidden"] != "true"
    ):
        raise CaptureRefusal("V3 source-closure repair lineage is internally mixed.")
    lines = ["id65-v3-runtime-source-closure-v2"]
    lines.extend(
        f"{name}={value}" for name, value in SOURCE_CLOSURE_PREFIX_FIELDS
    )
    lines.append(f"captureCoordinatorSha256={capture_coordinator_sha256}")
    lines.extend(
        f"{name}={value}" for name, value in SOURCE_CLOSURE_SUFFIX_FIELDS
    )
    return sha256_bytes(("\n".join(lines) + "\n").encode("utf-8", "strict"))


def format_utc(value: dt.datetime) -> str:
    value = value.astimezone(dt.timezone.utc)
    milliseconds = value.microsecond // 1000
    return value.strftime("%Y-%m-%dT%H:%M:%S.") + f"{milliseconds:03d}Z"


def parse_utc(value: str) -> dt.datetime:
    if not CANONICAL_UTC_RE.fullmatch(value):
        raise CaptureRefusal(f"Noncanonical UTC timestamp: {value!r}.")
    try:
        return dt.datetime.strptime(value, "%Y-%m-%dT%H:%M:%S.%fZ").replace(
            tzinfo=dt.timezone.utc
        )
    except ValueError as exc:
        raise CaptureRefusal(f"Invalid UTC timestamp: {value!r}.") from exc


def _within_session_clock_step_is_accepted(
    previous: ClockSample,
    current: ClockSample,
) -> bool:
    utc_delta = int(
        (parse_utc(current.at_utc) - parse_utc(previous.at_utc))
        .total_seconds()
        * 1_000_000
    )
    monotonic_delta = (
        current.monotonic_microseconds - previous.monotonic_microseconds
    )
    return (
        utc_delta >= 0
        and monotonic_delta > 0
        and abs(utc_delta - monotonic_delta) <= 250_000
    )


def _cross_session_clock_step_is_accepted(
    previous: ClockSample,
    current: ClockSample,
) -> bool:
    utc_delta = int(
        (parse_utc(current.at_utc) - parse_utc(previous.at_utc))
        .total_seconds()
        * 1_000_000
    )
    monotonic_delta = (
        current.monotonic_microseconds - previous.monotonic_microseconds
    )
    return (
        utc_delta > 0
        and monotonic_delta > 0
        and abs(utc_delta - monotonic_delta) <= 250_000
    )


def _require_minimum_interval(
    started: ClockSample, ended: ClockSample, minimum_microseconds: int, label: str
) -> None:
    if (
        isinstance(minimum_microseconds, bool)
        or not isinstance(minimum_microseconds, int)
        or minimum_microseconds <= 0
        or ended.monotonic_microseconds - started.monotonic_microseconds
        < minimum_microseconds
    ):
        raise CaptureRefusal(f"{label} is shorter than its fixed minimum interval.")


def _require_recording_duration(
    started_monotonic_microseconds: int,
    ended_monotonic_microseconds: int,
) -> None:
    duration = ended_monotonic_microseconds - started_monotonic_microseconds
    if (
        type(started_monotonic_microseconds) is not int
        or type(ended_monotonic_microseconds) is not int
        or started_monotonic_microseconds < 0
        or ended_monotonic_microseconds < 0
        or duration <= 0
        or duration > MAX_RECORDING_DURATION_MICROSECONDS
    ):
        raise CaptureRefusal("Direct-window recording duration is outside its bound.")


def _decode_watcher_sample_lines(lines: Sequence[str]) -> list[dict[str, Any]]:
    samples: list[dict[str, Any]] = []
    for line in lines:
        match = WATCHER_SAMPLE_RE.fullmatch(line)
        if match is None:
            raise CaptureRefusal("Watcher stdout contains an unapproved interior line.")
        lives = int(match.group("lives"))
        state = int(match.group("state"))
        z_value = int(match.group("z"))
        label = (
            "gameplay/non-death" if state == 0 else
            "death-respawn" if state == 4 else
            "game-over" if state == 5 else f"other-{state}"
        )
        if (
            match.group("label") != label
            or match.group("marker") != ("YES" if z_value < 0x400 else "no")
            or int.from_bytes(bytes.fromhex(match.group("livesRaw")), "little") != lives
            or int.from_bytes(bytes.fromhex(match.group("stateRaw")), "little") != state
            or int.from_bytes(
                bytes.fromhex(match.group("zRaw")), "little", signed=True
            ) != z_value
        ):
            raise CaptureRefusal(
                "Watcher decoded values disagree with raw words or labels."
            )
        sample = {
            "atUtc": match.group("at"),
            "elapsedMilliseconds": int(match.group("elapsed")),
            "lives": lives,
            "ordinal": int(match.group("ordinal")),
            "playerZ": z_value,
            "state": state,
        }
        if sample["ordinal"] != len(samples):
            raise CaptureRefusal("Watcher sample ordinals are not contiguous.")
        parse_utc(sample["atUtc"])
        if samples:
            utc_gap = int(
                (parse_utc(sample["atUtc"]) - parse_utc(samples[-1]["atUtc"]))
                .total_seconds()
                * 1000
            )
            elapsed_gap = (
                sample["elapsedMilliseconds"]
                - samples[-1]["elapsedMilliseconds"]
            )
            if (
                elapsed_gap <= 0
                or elapsed_gap > 250
                or utc_gap <= 0
                or abs(utc_gap - elapsed_gap) > 250
            ):
                raise CaptureRefusal("Watcher sample clocks are discontinuous.")
        samples.append(sample)
    if samples:
        total_utc = int(
            (parse_utc(samples[-1]["atUtc"]) - parse_utc(samples[0]["atUtc"]))
            .total_seconds()
            * 1000
        )
        total_elapsed = (
            samples[-1]["elapsedMilliseconds"]
            - samples[0]["elapsedMilliseconds"]
        )
        if abs(total_utc - total_elapsed) > 250:
            raise CaptureRefusal("Watcher UTC and monotonic clocks diverged in total.")
    return samples


def _parse_live_watcher_bytes(data: bytes) -> list[dict[str, Any]]:
    try:
        text = data.decode("utf-8", "strict")
    except UnicodeDecodeError as exc:
        raise CaptureRefusal("Watcher stdout is not strict UTF-8.") from exc
    if "\r" in text:
        raise CaptureRefusal("Watcher stdout contains a CR line ending.")
    if not text.startswith(WATCHER_LIVE_PREAMBLE):
        raise CaptureRefusal("Live watcher stdout preamble changed or is incomplete.")
    if WATCHER_CLEAN_TRAILER in text:
        raise CaptureRefusal("Live watcher stopped before the capture gate completed.")
    body = text[len(WATCHER_LIVE_PREAMBLE):]
    complete, separator, _tail = body.rpartition("\n")
    if not separator or not complete:
        return []
    return _decode_watcher_sample_lines(complete.split("\n"))


def _watcher_sample_safe(sample: Mapping[str, Any]) -> bool:
    return (
        sample.get("lives") == 4
        and sample.get("state") == 0
        and isinstance(sample.get("playerZ"), int)
        and sample["playerZ"] >= 0x400
    )


def _fall_binding_is_accepted(
    samples: Sequence[Mapping[str, Any]],
    marker_time: dt.datetime,
    sample_ordinal: int,
) -> bool:
    if not 0 < sample_ordinal < len(samples):
        return False
    selected = samples[sample_ordinal]
    prior = samples[sample_ordinal - 1]
    selected_time = parse_utc(str(selected["atUtc"]))
    delta_ms = int((selected_time - marker_time).total_seconds() * 1000)
    return (
        0 <= delta_ms <= MAX_FALL_AUTHORIZATION_BINDING_MILLISECONDS
        and _watcher_sample_safe(prior)
        and selected.get("lives") == 3
        and selected.get("state") == 4
        and isinstance(selected.get("playerZ"), int)
        and selected["playerZ"] < 0x400
        and all(
            _watcher_sample_safe(item)
            for item in samples[:sample_ordinal]
            if parse_utc(str(item["atUtc"])) >= marker_time
        )
    )


def _respawn_binding_is_accepted(
    samples: Sequence[Mapping[str, Any]],
    fall_ordinal: int,
    marker_time: dt.datetime,
    bound_ordinal: int,
) -> bool:
    if not 0 <= fall_ordinal < bound_ordinal < len(samples):
        return False
    first_directional = next(
        (
            item for item in samples
            if parse_utc(str(item["atUtc"])) >= marker_time
        ),
        None,
    )
    first_recovery = next(
        (
            item for item in samples[fall_ordinal + 1 :]
            if item.get("lives") == 3
            and item.get("state") == 0
            and isinstance(item.get("playerZ"), int)
            and item["playerZ"] >= 0x400
        ),
        None,
    )
    selected = samples[bound_ordinal]
    return (
        first_directional is not None
        and int(first_directional["ordinal"]) == bound_ordinal
        and first_recovery is not None
        and int(first_recovery["ordinal"]) <= bound_ordinal
        and parse_utc(str(first_recovery["atUtc"])) <= marker_time
        and int(first_recovery["elapsedMilliseconds"])
        - int(samples[fall_ordinal]["elapsedMilliseconds"])
        <= MAX_POST_DEATH_RECOVERY_MILLISECONDS
        and selected.get("lives") == 3
        and selected.get("state") == 0
        and isinstance(selected.get("playerZ"), int)
        and selected["playerZ"] >= 0x400
        and all(
            item.get("lives") == 3
            and item.get("state") == 0
            and isinstance(item.get("playerZ"), int)
            and item["playerZ"] >= 0x400
            for item in samples[int(first_recovery["ordinal"]) :]
        )
    )


def _select_first_watcher_sample_after(
    samples: Sequence[dict[str, Any]], confirmed: ClockSample
) -> dict[str, Any] | None:
    confirmed_utc = parse_utc(confirmed.at_utc)
    selected = next(
        (item for item in samples if parse_utc(item["atUtc"]) >= confirmed_utc),
        None,
    )
    if selected is None:
        return None
    lag_ms = int(
        (parse_utc(selected["atUtc"]) - confirmed_utc).total_seconds() * 1000
    )
    if not 0 <= lag_ms <= 250:
        raise CaptureRefusal(
            "Watcher has no fresh post-confirmation sample within 250ms."
        )
    return selected


def _candidate_primary_landing_transition(
    samples: Sequence[dict[str, Any]],
    baseline_ordinal: int,
    armed_at: dt.datetime,
) -> tuple[
    dict[str, Any] | None,
    dict[str, Any] | None,
    dict[str, Any] | None,
]:
    """Select the exact V3 load departure, landing, and safe-suffix endpoint."""
    if (
        isinstance(baseline_ordinal, bool)
        or not isinstance(baseline_ordinal, int)
        or baseline_ordinal < 0
        or baseline_ordinal >= len(samples)
        or not isinstance(armed_at, dt.datetime)
        or armed_at.tzinfo is None
        or not _watcher_sample_safe(samples[baseline_ordinal])
    ):
        raise CaptureRefusal("Assisted landing detector lacks its safe armed baseline.")

    departure: dict[str, Any] | None = None
    landing: dict[str, Any] | None = None
    phase = "armed"
    for ordinal in range(baseline_ordinal + 1, len(samples)):
        sample = samples[ordinal]
        if sample.get("ordinal") != ordinal:
            raise CaptureRefusal("Assisted landing detector found a noncanonical ordinal.")
        lives = sample.get("lives")
        state = sample.get("state")
        z_value = sample.get("playerZ")
        high = isinstance(z_value, int) and z_value >= 0x400
        if phase == "armed":
            departure_lag_ms = int(
                (
                    parse_utc(str(sample["atUtc"])) - armed_at
                ).total_seconds()
                * 1_000
            )
            if departure_lag_ms > ASSISTED_PRIMARY_DEPARTURE_MAXIMUM_MILLISECONDS:
                raise CaptureRefusal(
                    "Candidate load did not depart within its 5-minute selector bound."
                )
            if _watcher_sample_safe(sample):
                continue
            if lives == 4 and state == 3 and high:
                if (
                    departure_lag_ms <= 0
                    or not _watcher_sample_safe(samples[ordinal - 1])
                ):
                    raise CaptureRefusal(
                        "Candidate load departure predates its armed safe baseline."
                    )
                departure = sample
                phase = "state-3"
                continue
            raise CaptureRefusal(
                "Candidate load did not begin with the exact safe-to-state-3 transition."
            )
        if lives != 4 or not high:
            raise CaptureRefusal(
                "Candidate fly-in changed lives or crossed the safe Z plane."
            )
        if phase == "state-3":
            if state == 3:
                continue
            if state == 1:
                phase = "state-1"
                continue
            raise CaptureRefusal(
                "Candidate fly-in skipped or left its exact state-3/state-1 sequence."
            )
        if phase == "state-1":
            if state == 1:
                continue
            if state == 0:
                landing = sample
                phase = "landed"
                continue
            raise CaptureRefusal(
                "Candidate fly-in left state 1 without an exact safe landing."
            )
        if not _watcher_sample_safe(sample):
            raise CaptureRefusal(
                "Watcher observed unsafe gameplay after the automated landing."
            )

    if departure is None or landing is None:
        if (
            departure is not None
            and samples
            and int(samples[-1]["elapsedMilliseconds"])
            - int(departure["elapsedMilliseconds"])
            > ASSISTED_PRIMARY_LANDING_MAXIMUM_MILLISECONDS
        ):
            raise CaptureRefusal(
                "Candidate fly-in did not land within its 30-second assisted bound."
            )
        return departure, landing, None
    if (
        int(landing["elapsedMilliseconds"])
        - int(departure["elapsedMilliseconds"])
        > ASSISTED_PRIMARY_LANDING_MAXIMUM_MILLISECONDS
    ):
        raise CaptureRefusal(
            "Candidate fly-in exceeded its 30-second assisted landing bound."
        )
    safe_tail = samples[int(landing["ordinal"]) :]
    if (
        len(safe_tail) < ASSISTED_READINESS_MINIMUM_SAMPLES
        or int(safe_tail[-1]["elapsedMilliseconds"])
        - int(safe_tail[0]["elapsedMilliseconds"])
        < ASSISTED_READINESS_MINIMUM_MILLISECONDS
    ):
        return departure, landing, None
    if any(not _watcher_sample_safe(item) for item in safe_tail):
        raise CaptureRefusal(
            "Watcher observed unsafe gameplay in the automated landing suffix."
        )
    return departure, landing, safe_tail[-1]


def _assisted_armed_baseline_ordinal(
    samples: Sequence[dict[str, Any]],
    selector_at: dt.datetime,
) -> int:
    if not samples or selector_at.tzinfo is None:
        raise CaptureRefusal("Assisted landing selector boundary is invalid.")
    baseline = next(
        (
            item
            for item in reversed(samples)
            if parse_utc(str(item.get("atUtc"))) <= selector_at
        ),
        None,
    )
    if baseline is None or not _watcher_sample_safe(baseline):
        raise CaptureRefusal(
            "Assisted landing lacks a safe watcher baseline at selector acceptance."
        )
    baseline_ordinal = int(baseline["ordinal"])
    baseline_delta_ms = int(
        (selector_at - parse_utc(str(baseline["atUtc"]))).total_seconds()
        * 1_000
    )
    if not 0 <= baseline_delta_ms <= 250:
        raise CaptureRefusal(
            "Assisted landing lacks a fresh watcher baseline at selector acceptance."
        )
    CaptureCoordinator._require_gameplay_suffix(samples, baseline)
    return baseline_ordinal


def _watcher_sample_clock(
    sample: Mapping[str, Any],
    anchor: ClockSample,
) -> ClockSample:
    sample_at = parse_utc(str(sample.get("atUtc")))
    anchor_at = parse_utc(anchor.at_utc)
    delta_microseconds = int((sample_at - anchor_at).total_seconds() * 1_000_000)
    monotonic = anchor.monotonic_microseconds + delta_microseconds
    if delta_microseconds <= 0 or not 0 <= monotonic <= MAX_SAFE_INTEGER:
        raise CaptureRefusal(
            "Watcher-derived assisted marker does not follow its coordinator arm."
        )
    return ClockSample(str(sample["atUtc"]), monotonic)


def _candidate_primary_landing_evidence_is_accepted(
    samples: Sequence[dict[str, Any]],
    selector_at_utc: str,
    selector_monotonic_microseconds: int,
    completed_start_at_utc: str,
    completed_start_monotonic_microseconds: int,
    landing_bound_ordinal: int,
) -> bool:
    if (
        isinstance(landing_bound_ordinal, bool)
        or not isinstance(landing_bound_ordinal, int)
        or not 0 < landing_bound_ordinal < len(samples)
        or isinstance(selector_monotonic_microseconds, bool)
        or not isinstance(selector_monotonic_microseconds, int)
        or isinstance(completed_start_monotonic_microseconds, bool)
        or not isinstance(completed_start_monotonic_microseconds, int)
    ):
        return False
    selector_at = parse_utc(selector_at_utc)
    try:
        baseline_ordinal = _assisted_armed_baseline_ordinal(
            samples[: landing_bound_ordinal + 1], selector_at
        )
    except CaptureRefusal:
        return False
    first_non_safe = next(
        (
            item
            for item in samples[baseline_ordinal + 1 : landing_bound_ordinal + 1]
            if not _watcher_sample_safe(item)
        ),
        None,
    )
    if (
        first_non_safe is None
        or first_non_safe.get("atUtc") != completed_start_at_utc
        or first_non_safe.get("lives") != 4
        or first_non_safe.get("state") != 3
        or not isinstance(first_non_safe.get("playerZ"), int)
        or first_non_safe["playerZ"] < 0x400
    ):
        return False
    try:
        derived_start = _watcher_sample_clock(
            first_non_safe,
            ClockSample(selector_at_utc, selector_monotonic_microseconds),
        )
    except CaptureRefusal:
        return False
    if (
        derived_start.at_utc != completed_start_at_utc
        or derived_start.monotonic_microseconds
        != completed_start_monotonic_microseconds
    ):
        return False
    try:
        selected_departure, landing, endpoint = (
            _candidate_primary_landing_transition(
                samples[: landing_bound_ordinal + 1],
                baseline_ordinal,
                selector_at,
            )
        )
    except CaptureRefusal:
        return False
    return (
        selected_departure is not None
        and selected_departure["ordinal"] == first_non_safe["ordinal"]
        and landing is not None
        and int(landing["elapsedMilliseconds"])
        - int(selected_departure["elapsedMilliseconds"])
        <= ASSISTED_PRIMARY_LANDING_MAXIMUM_MILLISECONDS
        and endpoint is not None
        and int(endpoint["ordinal"]) == landing_bound_ordinal
    )


def _require_safe_hold_samples(samples: Sequence[dict[str, Any]]) -> None:
    if (
        len(samples) < 190
        or not samples
        or samples[-1]["elapsedMilliseconds"]
        - samples[0]["elapsedMilliseconds"]
        < 20_000
        or any(not _watcher_sample_safe(item) for item in samples)
    ):
        raise CaptureRefusal(
            "Watcher does not prove a continuous safe 20-second hands-off hold."
        )


def process_start_utc(start_seconds: int, start_microseconds: int) -> str:
    if (
        isinstance(start_seconds, bool)
        or not isinstance(start_seconds, int)
        or start_seconds <= 0
        or isinstance(start_microseconds, bool)
        or not isinstance(start_microseconds, int)
        or not 0 <= start_microseconds < 1_000_000
    ):
        raise CaptureRefusal("Process start epoch fields are invalid.")
    try:
        started = dt.datetime.fromtimestamp(
            start_seconds,
            tz=dt.timezone.utc,
        ) + dt.timedelta(microseconds=start_microseconds)
    except (OverflowError, OSError, ValueError) as exc:
        raise CaptureRefusal("Process start epoch is outside the canonical UTC range.") from exc
    return format_utc(started)


def guard_relative_path(value: str) -> tuple[str, ...]:
    if (
        not value
        or value.startswith("/")
        or "\\" in value
        or "\x00" in value
        or "|" in value
        or any(ord(character) < 0x20 for character in value)
    ):
        raise CaptureRefusal("Path is not a strict POSIX relative path.")
    pieces = tuple(value.split("/"))
    if any(piece in {"", ".", ".."} for piece in pieces):
        raise CaptureRefusal("Path contains an empty, current, or parent segment.")
    return pieces


def _require_no_symlink_segments(path: pathlib.Path) -> None:
    absolute = pathlib.Path(os.path.abspath(path))
    current = pathlib.Path(absolute.anchor)
    for part in absolute.parts[1:]:
        current = current / part
        try:
            mode = os.lstat(current).st_mode
        except FileNotFoundError:
            raise CaptureRefusal(f"Pinned path is missing: {current}") from None
        if stat.S_ISLNK(mode):
            raise CaptureRefusal(f"Pinned path traverses a symbolic link: {current}")


def _open_secure_directory(path: pathlib.Path) -> int:
    absolute = pathlib.Path(os.path.abspath(path))
    fd = os.open(
        absolute.anchor,
        os.O_RDONLY
        | getattr(os, "O_DIRECTORY", 0)
        | getattr(os, "O_NOFOLLOW", 0),
    )
    try:
        for part in absolute.parts[1:]:
            child = os.open(
                part,
                os.O_RDONLY
                | getattr(os, "O_DIRECTORY", 0)
                | getattr(os, "O_NOFOLLOW", 0),
                dir_fd=fd,
            )
            info = os.fstat(child)
            if not stat.S_ISDIR(info.st_mode):
                os.close(child)
                raise CaptureRefusal(f"Directory path changed type: {absolute}")
            os.close(fd)
            fd = child
        return fd
    except BaseException:
        os.close(fd)
        raise


def _require_owned_private_directory(path: pathlib.Path) -> None:
    fd = _open_secure_directory(path)
    try:
        info = os.fstat(fd)
        if info.st_uid != os.getuid() or stat.S_IMODE(info.st_mode) != 0o700:
            raise CaptureRefusal(f"Capture directory is not owned mode 0700: {path}")
    finally:
        os.close(fd)


def _private_directory_identity(path: pathlib.Path) -> tuple[int, int, int, int]:
    fd = _open_secure_directory(path)
    try:
        info = os.fstat(fd)
        if (
            not stat.S_ISDIR(info.st_mode)
            or info.st_uid != os.getuid()
            or stat.S_IMODE(info.st_mode) != 0o700
        ):
            raise CaptureRefusal(f"Capture directory is not owned mode 0700: {path}")
        return (info.st_dev, info.st_ino, info.st_mode, info.st_uid)
    finally:
        os.close(fd)


def read_regular_file(
    path: pathlib.Path,
    *,
    include_bytes: bool = False,
    expected_owner: int | None = None,
    maximum_bytes: int | None = None,
    validator: Callable[[int, int], None] | None = None,
    expected_mode: int | None = None,
    expected_parent_identity: tuple[int, int, int, int] | None = None,
) -> FileIdentity:
    absolute = pathlib.Path(os.path.abspath(path))
    parent_fd = _open_secure_directory(absolute.parent)
    flags = os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0)
    try:
        if expected_parent_identity is not None:
            parent_info = os.fstat(parent_fd)
            parent_identity = (
                parent_info.st_dev,
                parent_info.st_ino,
                parent_info.st_mode,
                parent_info.st_uid,
            )
            if parent_identity != expected_parent_identity:
                raise CaptureRefusal("Capture output directory identity changed.")
        fd = os.open(absolute.name, flags, dir_fd=parent_fd)
    finally:
        os.close(parent_fd)
    try:
        before = os.fstat(fd)
        if not stat.S_ISREG(before.st_mode) or before.st_nlink != 1:
            raise CaptureRefusal(f"Pinned file is not a single-link regular file: {absolute}")
        if expected_owner is not None and before.st_uid != expected_owner:
            raise CaptureRefusal(f"Pinned file has the wrong owner: {absolute}")
        if expected_mode is not None and stat.S_IMODE(before.st_mode) != expected_mode:
            raise CaptureRefusal(f"Pinned file has the wrong mode: {absolute}")
        if maximum_bytes is not None:
            _require_maximum_file_bytes(before.st_size, maximum_bytes, absolute)
        digest = hashlib.sha256()
        chunks: list[bytes] | None = [] if include_bytes else None
        total = 0
        while True:
            read_size = 1024 * 1024
            if maximum_bytes is not None:
                read_size = min(read_size, maximum_bytes - total + 1)
            chunk = os.read(fd, read_size)
            if not chunk:
                break
            digest.update(chunk)
            total += len(chunk)
            if maximum_bytes is not None and total > maximum_bytes:
                raise CaptureRefusal(f"Pinned file exceeds its bounded size: {absolute}")
            if chunks is not None:
                chunks.append(chunk)
        if validator is not None:
            os.lseek(fd, 0, os.SEEK_SET)
            validator(fd, before.st_size)
        after = os.fstat(fd)
        stable = (
            before.st_dev,
            before.st_ino,
            before.st_mode,
            before.st_nlink,
            before.st_uid,
            before.st_size,
            before.st_mtime_ns,
            before.st_ctime_ns,
        ) == (
            after.st_dev,
            after.st_ino,
            after.st_mode,
            after.st_nlink,
            after.st_uid,
            after.st_size,
            after.st_mtime_ns,
            after.st_ctime_ns,
        )
        if not stable or total != before.st_size:
            raise CaptureRefusal(f"Pinned file changed while it was read: {absolute}")
        return FileIdentity(
            real_path=os.path.realpath(absolute),
            device=before.st_dev,
            inode=before.st_ino,
            byte_length=before.st_size,
            sha256=digest.hexdigest(),
            bytes_value=b"".join(chunks) if chunks is not None else None,
        )
    finally:
        os.close(fd)


def _require_maximum_file_bytes(
    byte_length: int,
    maximum_bytes: int,
    path: pathlib.Path,
) -> None:
    if (
        type(byte_length) is not int
        or byte_length < 0
        or type(maximum_bytes) is not int
        or maximum_bytes <= 0
        or byte_length > maximum_bytes
    ):
        raise CaptureRefusal(f"Pinned file exceeds its bounded size: {path}")


def read_regular_file_at(
    directory_fd: int,
    name: str,
    display_path: pathlib.Path,
    *,
    expected_owner: int,
) -> FileIdentity:
    if "/" in name or name in {"", ".", ".."}:
        raise CaptureRefusal("Open-at filename is not one leaf.")
    fd = os.open(
        name,
        os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0),
        dir_fd=directory_fd,
    )
    try:
        before = os.fstat(fd)
        if (
            not stat.S_ISREG(before.st_mode)
            or before.st_nlink != 1
            or before.st_uid != expected_owner
        ):
            raise CaptureRefusal(f"Profile file identity is unsafe: {display_path}")
        digest = hashlib.sha256()
        total = 0
        while True:
            chunk = os.read(fd, 1024 * 1024)
            if not chunk:
                break
            total += len(chunk)
            digest.update(chunk)
        after = os.fstat(fd)
        fingerprint = lambda item: (
            item.st_dev,
            item.st_ino,
            item.st_mode,
            item.st_nlink,
            item.st_uid,
            item.st_size,
            item.st_mtime_ns,
            item.st_ctime_ns,
        )
        if fingerprint(before) != fingerprint(after) or total != before.st_size:
            raise CaptureRefusal(f"Profile file changed while read: {display_path}")
        return FileIdentity(
            os.path.realpath(display_path),
            before.st_dev,
            before.st_ino,
            before.st_size,
            digest.hexdigest(),
        )
    finally:
        os.close(fd)


class IncrementalWatcherReader:
    """Anchored, prefix-only reader for the actively growing watcher log."""

    def __init__(self, session_path: pathlib.Path, writer_fd: int) -> None:
        self._directory_fd = _open_secure_directory(session_path)
        directory = os.fstat(self._directory_fd)
        if (
            not stat.S_ISDIR(directory.st_mode)
            or directory.st_uid != os.getuid()
            or stat.S_IMODE(directory.st_mode) != 0o700
        ):
            os.close(self._directory_fd)
            raise CaptureRefusal("Live watcher directory is not owned mode 0700.")
        self._directory_identity = (
            directory.st_dev,
            directory.st_ino,
            directory.st_mode,
            directory.st_uid,
        )
        try:
            self._fd = os.open(
                "watcher-stdout.log",
                os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0),
                dir_fd=self._directory_fd,
            )
        except BaseException:
            os.close(self._directory_fd)
            raise
        self._identity = self._require_identity(os.fstat(self._fd))
        if self._require_identity(os.fstat(writer_fd)) != self._identity:
            os.close(self._fd)
            os.close(self._directory_fd)
            raise CaptureRefusal(
                "Live watcher reader is not bound to the writer inode."
            )
        self._accepted = b""
        self._accepted_dynamic: tuple[int, int, int] | None = None
        self._closed = False

    @staticmethod
    def _require_identity(info: os.stat_result) -> tuple[int, int, int, int, int]:
        if (
            not stat.S_ISREG(info.st_mode)
            or info.st_nlink != 1
            or info.st_uid != os.getuid()
            or stat.S_IMODE(info.st_mode) != 0o600
        ):
            raise CaptureRefusal("Live watcher log inode is unsafe.")
        return (
            info.st_dev,
            info.st_ino,
            info.st_mode,
            info.st_nlink,
            info.st_uid,
        )

    def close(self) -> None:
        if not self._closed:
            self._closed = True
            os.close(self._fd)
            os.close(self._directory_fd)

    def __enter__(self) -> "IncrementalWatcherReader":
        return self

    def __exit__(self, *args: Any) -> None:
        self.close()

    def snapshot(
        self,
        deadline_monotonic_microseconds: int | None = None,
        monotonic_clock: Callable[[], int] | None = None,
    ) -> list[dict[str, Any]]:
        if self._closed:
            raise CaptureRefusal("Live watcher reader is already closed.")
        clock = monotonic_clock or (lambda: time.monotonic_ns() // 1000)
        local_deadline = time.monotonic() + 1.0
        while True:
            now = clock()
            if (
                deadline_monotonic_microseconds is not None
                and now >= deadline_monotonic_microseconds
            ):
                raise CaptureRefusal(
                    "Direct-window recording reached its 30-minute deadline while "
                    "reading live watcher telemetry."
                )
            directory = os.fstat(self._directory_fd)
            if (
                directory.st_dev,
                directory.st_ino,
                directory.st_mode,
                directory.st_uid,
            ) != self._directory_identity:
                raise CaptureRefusal("Live watcher directory identity changed.")
            try:
                path_before = os.stat(
                    "watcher-stdout.log",
                    dir_fd=self._directory_fd,
                    follow_symlinks=False,
                )
            except OSError as exc:
                raise CaptureRefusal("Live watcher log path disappeared.") from exc
            before = os.fstat(self._fd)
            if (
                self._require_identity(path_before) != self._identity
                or self._require_identity(before) != self._identity
            ):
                raise CaptureRefusal("Live watcher log path or inode changed.")
            if before.st_size > MAX_TEXT_BYTES:
                raise CaptureRefusal("Live watcher log exceeds its bounded size.")
            if before.st_size < len(self._accepted):
                raise CaptureRefusal("Live watcher log shrank after an accepted prefix.")
            data = os.pread(self._fd, before.st_size, 0)
            after = os.fstat(self._fd)
            path_after = os.stat(
                "watcher-stdout.log",
                dir_fd=self._directory_fd,
                follow_symlinks=False,
            )
            if (
                self._require_identity(after) != self._identity
                or self._require_identity(path_after) != self._identity
            ):
                raise CaptureRefusal("Live watcher log identity changed while read.")
            before_dynamic = (
                before.st_size,
                before.st_mtime_ns,
                before.st_ctime_ns,
            )
            after_dynamic = (
                after.st_size,
                after.st_mtime_ns,
                after.st_ctime_ns,
            )
            if (
                before_dynamic != after_dynamic
                or len(data) != before.st_size
                or (
                    path_after.st_size,
                    path_after.st_mtime_ns,
                    path_after.st_ctime_ns,
                ) != after_dynamic
            ):
                now = clock()
                if (
                    deadline_monotonic_microseconds is not None
                    and now >= deadline_monotonic_microseconds
                ):
                    raise CaptureRefusal(
                        "Direct-window recording reached its 30-minute deadline while "
                        "reading live watcher telemetry."
                    )
                if time.monotonic() >= local_deadline:
                    raise CaptureRefusal(
                        "Live watcher log did not stabilize during bounded growth."
                    )
                sleep_seconds = 0.05
                if deadline_monotonic_microseconds is not None:
                    sleep_seconds = min(
                        sleep_seconds,
                        max(
                            0.0,
                            (deadline_monotonic_microseconds - now) / 1_000_000,
                        ),
                    )
                time.sleep(sleep_seconds)
                continue
            if not data.startswith(self._accepted):
                raise CaptureRefusal("Live watcher log rewrote an accepted byte prefix.")
            if (
                self._accepted_dynamic is not None
                and before.st_size == len(self._accepted)
                and before_dynamic != self._accepted_dynamic
            ):
                raise CaptureRefusal(
                    "Live watcher log rewrote an accepted byte prefix."
                )
            samples = _parse_live_watcher_bytes(data)
            now = clock()
            if (
                deadline_monotonic_microseconds is not None
                and now > deadline_monotonic_microseconds
            ):
                raise CaptureRefusal(
                    "Direct-window recording reached its 30-minute deadline while "
                    "reading live watcher telemetry."
                )
            self._accepted = data
            self._accepted_dynamic = after_dynamic
            return samples


def _parse_contract_pins(contract_bytes: bytes) -> dict[str, str]:
    try:
        text = contract_bytes.decode("utf-8", "strict")
    except UnicodeDecodeError as exc:
        raise CaptureRefusal("The frozen contract source is not strict UTF-8.") from exc
    if re.search(r"(?m)^[^\S\r\n]*#", text):
        raise CaptureRefusal(
            "Frozen contract contains an unapproved C# preprocessor directive."
        )
    if any(opener in text for opener in ('\"\"\"', '@\"', '$@\"', '@$\"')):
        raise CaptureRefusal(
            "Frozen contract contains an unapproved multiline C# string opener."
        )
    names = (
        "ExpectedSourceClosureSha256",
        "ExpectedRuntimeProfileSha256",
        "ExpectedWatcherSha256",
        "ExpectedCaptureCoordinatorSha256",
    )
    result: dict[str, str] = {}
    for name in names:
        matches = [
            match.group(1)
            for match in re.finditer(
            rf'(?m)^[ \t]*internal const string {name}[ \t]*=[ \t]*\n'
            rf'[ \t]*"([0-9a-f]{{64}})";[ \t]*$',
            text,
            )
            if text.rfind("/*", 0, match.start())
            <= text.rfind("*/", 0, match.start())
        ]
        if len(matches) != 1:
            raise CaptureRefusal(
                f"Frozen contract omits the unique anchored {name} declaration."
            )
        result[name] = matches[0]
    return result


def _artifact_metadata(
    artifact_id: str,
    kind: str,
    relative_path: str,
    captured_at_utc: str,
    identity: FileIdentity,
) -> dict[str, Any]:
    return {
        "artifactId": artifact_id,
        "byteLength": identity.byte_length,
        "capturedAtUtc": captured_at_utc,
        "kind": kind,
        "repoRelativePath": relative_path,
        "sha256": identity.sha256,
    }


PROFILE_SNAPSHOT_KEYS: Final = frozenset({
    "format", "formatVersion", "snapshotId", "phase", "sessionId",
    "processInstanceId", "profileId", "capturedAtUtc",
    "duckStationExecutableSha256", "handoffReceiptSha256",
    "appContentManifestSha256", "appOutputTreeManifestSha256",
    "packageEnvelopeSha256", "externalHomeSettingsSha256",
    "externalHomeBiosSha256", "hostNormalSettingsSentinelId",
    "hostNormalSettingsSentinelSha256", "dataFiles",
})

PREFLIGHT_PROOF_KEYS: Final = frozenset({
    "format", "formatVersion", "status", "profileId", "receiptSha256",
    "appContentManifestSha256", "appOutputTreeManifestSha256",
    "homeOutputTreeManifestSha256", "packageEnvelopeSha256", "settingsSha256",
    "biosSha256", "candidateCueSha256", "candidateBinSha256",
    "hostNormalSettingsSentinelSha256", "launchGate", "snapshot",
})


def canonical_lf_bytes(value: Any) -> bytes:
    """Canonical JSON used by the frozen verifier and snapshot artifacts."""
    _validate_i_json(value)
    return (
        json.dumps(
            value,
            allow_nan=False,
            sort_keys=True,
            separators=(",", ":"),
        )
        + "\n"
    ).encode("utf-8", "strict")


def _canonical_data_relative_path(value: object) -> str:
    if type(value) is not str or not value or "\\" in value:
        raise CaptureRefusal("Runtime snapshot path is not canonical POSIX text.")
    if any(ord(character) < 0x20 or ord(character) == 0x7f for character in value):
        raise CaptureRefusal("Runtime snapshot path contains a control character.")
    path = pathlib.PurePosixPath(value)
    if (
        path.is_absolute()
        or path.as_posix() != value
        or value in {".", ".."}
        or any(
            segment in {"", ".", ".."}
            or unicodedata.normalize("NFC", segment) != segment
            for segment in value.split("/")
        )
    ):
        raise CaptureRefusal("Runtime snapshot path is not segment-safe.")
    return value


def _runtime_data_path_is_allowed(relative: str) -> bool:
    relative = _canonical_data_relative_path(relative)
    if relative in {"settings.ini", BIOS_MANIFEST_PATH, "playtime.dat"}:
        return True
    prefix, separator, suffix = relative.partition("/")
    return (
        separator == "/"
        and bool(suffix)
        and prefix in {"cache", "screenshots", "videos"}
    )


def _validate_profile_snapshot(
    snapshot: object,
    *,
    phase: str,
    session_id: str,
    process_instance_id: str,
    captured_at_utc: str,
) -> dict[str, Any]:
    if type(snapshot) is not dict or set(snapshot) != PROFILE_SNAPSHOT_KEYS:
        raise CaptureRefusal("Runtime profile snapshot is not the exact 18-key v2 schema.")
    expected = {
        "format": PROFILE_FORMAT,
        "formatVersion": PROFILE_FORMAT_VERSION,
        "snapshotId": f"{session_id}.profile-{phase}",
        "phase": phase,
        "sessionId": session_id,
        "processInstanceId": process_instance_id,
        "profileId": PROFILE_ID,
        "capturedAtUtc": captured_at_utc,
        "duckStationExecutableSha256": EXPECTED_DUCKSTATION_SHA256,
        "handoffReceiptSha256": EXPECTED_HANDOFF_RECEIPT_SHA256,
        "appContentManifestSha256": EXPECTED_APP_CONTENT_MANIFEST_SHA256,
        "appOutputTreeManifestSha256": EXPECTED_APP_OUTPUT_TREE_MANIFEST_SHA256,
        "packageEnvelopeSha256": EXPECTED_PACKAGE_ENVELOPE_SHA256,
        "externalHomeSettingsSha256": EXPECTED_SETTINGS_SHA256,
        "externalHomeBiosSha256": EXPECTED_BIOS_SHA256,
        "hostNormalSettingsSentinelId": NORMAL_SETTINGS_RESOURCE_ID,
        "hostNormalSettingsSentinelSha256": EXPECTED_NORMAL_SETTINGS_SHA256,
    }
    for name, value in expected.items():
        if snapshot.get(name) != value or type(snapshot.get(name)) is not type(value):
            raise CaptureRefusal(f"Runtime profile snapshot identity changed: {name}.")
    parse_utc(captured_at_utc)
    data_files = snapshot["dataFiles"]
    if type(data_files) is not list:
        raise CaptureRefusal("Runtime profile dataFiles is not a list.")
    paths: list[str] = []
    pinned: dict[str, tuple[int, str]] = {}
    for entry in data_files:
        if type(entry) is not dict or set(entry) != {"relativePath", "byteLength", "sha256"}:
            raise CaptureRefusal("Runtime profile data-file schema changed.")
        relative = _canonical_data_relative_path(entry["relativePath"])
        length = entry["byteLength"]
        digest = entry["sha256"]
        if (
            not _runtime_data_path_is_allowed(relative)
            or type(length) is not int
            or length < 0
            or length > MAX_SAFE_INTEGER
            or type(digest) is not str
            or SHA256_RE.fullmatch(digest) is None
        ):
            raise CaptureRefusal("Runtime profile data-file entry is unapproved.")
        paths.append(relative)
        pinned[relative] = (length, digest)
    if paths != sorted(set(paths)):
        raise CaptureRefusal("Runtime profile dataFiles is reordered or duplicated.")
    if pinned.get("settings.ini") != (EXPECTED_SETTINGS_SIZE, EXPECTED_SETTINGS_SHA256):
        raise CaptureRefusal("Runtime profile omits pinned external-HOME settings.ini.")
    if pinned.get(BIOS_MANIFEST_PATH) != (EXPECTED_BIOS_SIZE, EXPECTED_BIOS_SHA256):
        raise CaptureRefusal("Runtime profile omits the pinned external-HOME BIOS.")
    return snapshot


def _validate_prelaunch_proof(proof: object) -> dict[str, Any]:
    if type(proof) is not dict or set(proof) != PREFLIGHT_PROOF_KEYS:
        raise CaptureRefusal("Prelaunch proof is not the exact frozen schema.")
    expected = {
        "format": "spyro-id65-unattended-profile-preflight",
        "formatVersion": 1,
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
        "launchGate": "EXTERNAL_RUNTIME_LAUNCH_REQUIRED",
    }
    for name, value in expected.items():
        if proof.get(name) != value or type(proof.get(name)) is not type(value):
            raise CaptureRefusal(f"Prelaunch proof identity changed: {name}.")
    _validate_profile_snapshot(
        proof["snapshot"],
        phase="preflight",
        session_id="preflight",
        process_instance_id="not-applicable",
        captured_at_utc="1970-01-01T00:00:00.000Z",
    )
    return proof


class SourceGuard:
    def __init__(self, repository_root: pathlib.Path) -> None:
        root = pathlib.Path(repository_root)
        expected_root = pathlib.Path(os.path.abspath(__file__)).parent.parent
        if (
            not root.is_absolute()
            or pathlib.Path(os.path.normpath(os.fspath(root))) != root
            or pathlib.Path(os.path.realpath(root)) != root
            or root != expected_root
        ):
            raise CaptureRefusal("Repository root is not the exact audited Capture checkout.")
        _require_no_symlink_segments(root)
        self.repository_root = root
        self.package_root = root / PROFILE_PACKAGE_RELATIVE_PATH
        self.external_home_root = root / EXTERNAL_HOME_RELATIVE_PATH
        self.profile_root = root / EXTERNAL_HOME_DATA_RELATIVE_PATH
        self.executable_path = root / DUCKSTATION_EXECUTABLE_RELATIVE_PATH
        self.receipt_path = root / HANDOFF_RECEIPT_RELATIVE_PATH
        self.cue_path = root / CANDIDATE_CUE_RELATIVE_PATH
        self.bin_path = root / CANDIDATE_BIN_RELATIVE_PATH
        self._verifier: ModuleType | None = None
        self.prelaunch_call_count = 0
        self.snapshot_call_count = 0

    def _require_verifier_contract(self, verifier: ModuleType) -> None:
        if getattr(verifier, "PRODUCTION_REPOSITORY_ROOT", None) != self.repository_root:
            raise CaptureRefusal("Frozen verifier repository identity changed.")
        if (
            getattr(verifier, "PROFILE_FORMAT", None) != PROFILE_FORMAT
            or getattr(verifier, "PROFILE_FORMAT_VERSION", None) != PROFILE_FORMAT_VERSION
            or not callable(getattr(verifier, "verify_prelaunch", None))
            or not callable(getattr(verifier, "capture_runtime_snapshot", None))
        ):
            raise CaptureRefusal("Frozen verifier API contract changed.")
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
            "receipt_sha256": EXPECTED_HANDOFF_RECEIPT_SHA256,
            "receipt_closure_sha256": EXPECTED_RECEIPT_CLOSURE_SHA256,
            "executable_size": EXPECTED_EXECUTABLE_SIZE,
            "executable_sha256": EXPECTED_DUCKSTATION_SHA256,
            "app_content_manifest_sha256": EXPECTED_APP_CONTENT_MANIFEST_SHA256,
            "app_output_tree_manifest_sha256": EXPECTED_APP_OUTPUT_TREE_MANIFEST_SHA256,
            "home_output_tree_manifest_sha256": EXPECTED_HOME_OUTPUT_TREE_MANIFEST_SHA256,
            "package_envelope_sha256": EXPECTED_PACKAGE_ENVELOPE_SHA256,
            "settings_data_relative_path": pathlib.PurePosixPath("settings.ini"),
            "settings_size": EXPECTED_SETTINGS_SIZE,
            "settings_sha256": EXPECTED_SETTINGS_SHA256,
            "bios_data_relative_path": pathlib.PurePosixPath(BIOS_MANIFEST_PATH),
            "bios_size": EXPECTED_BIOS_SIZE,
            "bios_sha256": EXPECTED_BIOS_SHA256,
            "candidate_cue_relative_path": pathlib.PurePosixPath(CANDIDATE_CUE_RELATIVE_PATH),
            "candidate_cue_size": EXPECTED_CANDIDATE_CUE_SIZE,
            "candidate_cue_sha256": EXPECTED_CANDIDATE_CUE_SHA256,
            "candidate_bin_relative_path": pathlib.PurePosixPath(CANDIDATE_BIN_RELATIVE_PATH),
            "candidate_bin_size": EXPECTED_CANDIDATE_BIN_SIZE,
            "candidate_bin_sha256": EXPECTED_CANDIDATE_BIN_SHA256,
            "candidate_manifest_sha256": EXPECTED_CANDIDATE_MANIFEST_SHA256,
            "host_settings_path": NORMAL_SETTINGS_PATH,
            "host_settings_sentinel_id": NORMAL_SETTINGS_RESOURCE_ID,
            "host_settings_size": 10_058,
            "host_settings_sha256": EXPECTED_NORMAL_SETTINGS_SHA256,
            "host_settings_mode": 0o600,
        }
        if pins is None:
            raise CaptureRefusal("Frozen verifier production pins are unavailable.")
        for name, value in expected.items():
            if getattr(pins, name, None) != value:
                raise CaptureRefusal(f"Frozen verifier production pin changed: {name}.")

    def _load_verifier(self) -> ModuleType:
        if self._verifier is not None:
            self._require_verifier_contract(self._verifier)
            return self._verifier
        identity = read_regular_file(
            self.repository_root / PROFILE_VERIFIER_RELATIVE_PATH,
            include_bytes=True,
            expected_owner=os.getuid(),
            expected_mode=0o644,
            maximum_bytes=1024 * 1024,
        )
        if identity.sha256 != EXPECTED_PROFILE_VERIFIER_SHA256:
            raise CaptureRefusal("Frozen verifier hash changed before dynamic import.")
        module_name = "_spyro_id65_frozen_external_home_verifier_for_capture"
        module = ModuleType(module_name)
        module.__file__ = os.fspath(self.repository_root / PROFILE_VERIFIER_RELATIVE_PATH)
        module.__package__ = ""
        previous = sys.modules.get(module_name)
        sys.modules[module_name] = module
        try:
            code = compile(
                identity.bytes_value or b"", module.__file__, "exec",
                flags=0, dont_inherit=True, optimize=0,
            )
            exec(code, module.__dict__)
            self._require_verifier_contract(module)
        except CaptureRefusal:
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
            raise CaptureRefusal("Frozen verifier could not be loaded safely.") from exc
        self._verifier = module
        return module

    def validate_all(self) -> dict[str, str]:
        uid = os.getuid()
        if recompute_runtime_profile_v2_sha256() != EXPECTED_RUNTIME_PROFILE_SHA256:
            raise CaptureRefusal("Frozen runtime-profile-v2 projection changed.")
        expected_names = {
            pathlib.PurePosixPath(relative_path).name
            for relative_path in PINNED_SOURCE_FILES
            if relative_path.startswith(CANDIDATE_DIRECTORY + "/")
        }
        candidate_directory = self.repository_root / CANDIDATE_DIRECTORY
        candidate_fd = _open_secure_directory(candidate_directory)
        try:
            directory_info = os.fstat(candidate_fd)
            if directory_info.st_uid != uid or stat.S_IMODE(directory_info.st_mode) != 0o755:
                raise CaptureRefusal("Candidate directory is not exact current-user mode 0755.")
            candidate_names = set(os.listdir(candidate_fd))
            for name in candidate_names:
                info = os.stat(name, dir_fd=candidate_fd, follow_symlinks=False)
                expected_mode = 0o755 if name.endswith("-Reveal-in-Finder.command") else 0o644
                if (
                    not stat.S_ISREG(info.st_mode) or info.st_nlink != 1
                    or info.st_uid != uid or stat.S_IMODE(info.st_mode) != expected_mode
                ):
                    raise CaptureRefusal("Candidate directory contains an unsafe leaf.")
        finally:
            os.close(candidate_fd)
        if candidate_names != expected_names:
            raise CaptureRefusal("Candidate directory is not the exact seven-file set.")
        source_files = dict(PINNED_SOURCE_FILES)
        source_files.update({
            PROFILE_BUILDER_RELATIVE_PATH: EXPECTED_PROFILE_BUILDER_SHA256,
            PROFILE_VERIFIER_RELATIVE_PATH: EXPECTED_PROFILE_VERIFIER_SHA256,
            AUTOMATION_RELATIVE_PATH: EXPECTED_AUTOMATION_SHA256,
        })
        for relative_path, expected_hash in source_files.items():
            expected_mode = (
                0o755 if relative_path in {AUTOMATION_RELATIVE_PATH}
                or relative_path.endswith("-Reveal-in-Finder.command") else 0o644
            )
            identity = read_regular_file(
                self.repository_root / relative_path,
                expected_owner=uid,
                expected_mode=expected_mode,
                include_bytes=relative_path == CANDIDATE_CUE_RELATIVE_PATH,
            )
            if identity.sha256 != expected_hash:
                raise CaptureRefusal(f"Pinned source hash changed: {relative_path}")
            if relative_path == CANDIDATE_CUE_RELATIVE_PATH:
                self._validate_cue(identity.bytes_value or b"")
        coordinator = read_regular_file(
            self.repository_root / CAPTURE_COORDINATOR_RELATIVE_PATH,
            expected_owner=uid, expected_mode=0o755,
        )
        recomputed_closure = recompute_source_closure(coordinator.sha256)
        contract = read_regular_file(
            self.repository_root / CONTRACT_RELATIVE_PATH,
            include_bytes=True, expected_owner=uid, expected_mode=0o644,
            maximum_bytes=4 * 1024 * 1024,
        )
        pins = _parse_contract_pins(contract.bytes_value or b"")
        expected_contract = {
            "ExpectedCaptureCoordinatorSha256": coordinator.sha256,
            "ExpectedWatcherSha256": EXPECTED_WATCHER_SHA256,
            "ExpectedRuntimeProfileSha256": EXPECTED_RUNTIME_PROFILE_SHA256,
            "ExpectedSourceClosureSha256": recomputed_closure,
        }
        for name, expected in expected_contract.items():
            if pins[name] != expected:
                raise CaptureRefusal(f"Frozen C# contract pin is stale: {name}.")
        self._load_verifier()
        result = {
            "automationRelativePath": AUTOMATION_RELATIVE_PATH,
            "automationSha256": EXPECTED_AUTOMATION_SHA256,
            "captureCoordinatorRelativePath": CAPTURE_COORDINATOR_RELATIVE_PATH,
            "captureCoordinatorSha256": coordinator.sha256,
            "profileBuilderRelativePath": PROFILE_BUILDER_RELATIVE_PATH,
            "profileBuilderSha256": EXPECTED_PROFILE_BUILDER_SHA256,
            "profilePackageRelativePath": PROFILE_PACKAGE_RELATIVE_PATH,
            "profileId": PROFILE_ID,
            "profileVerifierRelativePath": PROFILE_VERIFIER_RELATIVE_PATH,
            "profileVerifierSha256": EXPECTED_PROFILE_VERIFIER_SHA256,
            "runtimeProfileSha256": EXPECTED_RUNTIME_PROFILE_SHA256,
            "sourceClosureSha256": recomputed_closure,
            "watcherRelativePath": WATCHER_RELATIVE_PATH,
            "watcherSha256": EXPECTED_WATCHER_SHA256,
            "handoffReceiptSha256": EXPECTED_HANDOFF_RECEIPT_SHA256,
            "appContentManifestSha256": EXPECTED_APP_CONTENT_MANIFEST_SHA256,
            "appOutputTreeManifestSha256": EXPECTED_APP_OUTPUT_TREE_MANIFEST_SHA256,
            "homeOutputTreeManifestSha256": EXPECTED_HOME_OUTPUT_TREE_MANIFEST_SHA256,
            "packageEnvelopeSha256": EXPECTED_PACKAGE_ENVELOPE_SHA256,
            "externalHomeSettingsSha256": EXPECTED_SETTINGS_SHA256,
            "externalHomeBiosSha256": EXPECTED_BIOS_SHA256,
            "hostNormalSettingsSentinelSha256": EXPECTED_NORMAL_SETTINGS_SHA256,
        }
        if set(result) != set(CAPTURE_PIN_KEYS):
            raise CaptureRefusal("Internal Capture pin-set closure changed.")
        return result

    def _validate_cue(self, data: bytes) -> None:
        expected = (
            f'FILE "{CANDIDATE_STEM}.bin" BINARY\n'
            "  TRACK 01 MODE2/2352\n"
            "    INDEX 01 00:00:00\n"
        ).encode("ascii")
        if data != expected:
            raise CaptureRefusal("Candidate CUE bytes or referenced BIN name changed.")
        bin_identity = read_regular_file(
            self.bin_path, expected_owner=os.getuid(), expected_mode=0o644
        )
        if (
            bin_identity.sha256 != EXPECTED_CANDIDATE_BIN_SHA256
            or bin_identity.sha256 == FAILED_V1_BIN_SHA256
            or bin_identity.sha256 == SUPERSEDED_V2_BIN_SHA256
        ):
            raise CaptureRefusal("Candidate CUE does not bind the exact V3 BIN.")

    def candidate_identities(self) -> tuple[FileIdentity, FileIdentity]:
        cue = read_regular_file(
            self.cue_path, expected_owner=os.getuid(), expected_mode=0o644
        )
        bin_file = read_regular_file(
            self.bin_path, expected_owner=os.getuid(), expected_mode=0o644
        )
        if (
            cue.sha256 != EXPECTED_CANDIDATE_CUE_SHA256
            or bin_file.sha256 != EXPECTED_CANDIDATE_BIN_SHA256
            or bin_file.sha256 == FAILED_V1_BIN_SHA256
            or bin_file.sha256 == SUPERSEDED_V2_BIN_SHA256
        ):
            raise CaptureRefusal("Candidate disc pins changed during capture.")
        return cue, bin_file

    def executable_identity(self) -> FileIdentity:
        identity = read_regular_file(
            self.executable_path,
            expected_owner=os.getuid(),
        )
        if identity.sha256 != EXPECTED_DUCKSTATION_SHA256:
            raise CaptureRefusal("Pinned DuckStation executable changed during capture.")
        return identity

    def capture_profile(
        self,
        *,
        phase: str,
        session_id: str,
        process_instance_id: str,
        captured_at_utc: str | None = None,
    ) -> dict[str, Any]:
        if phase not in {"preflight", "before", "after"}:
            raise CaptureRefusal("Unknown profile snapshot phase.")
        if phase == "preflight":
            captured_at_utc = captured_at_utc or "1970-01-01T00:00:00.000Z"
        elif captured_at_utc is None:
            raise CaptureRefusal("A tool-derived profile capture time is required.")
        verifier = self._load_verifier()
        self.snapshot_call_count += 1
        try:
            snapshot = verifier.capture_runtime_snapshot(
                self.repository_root,
                phase=phase,
                session_id=session_id,
                process_instance_id=process_instance_id,
                captured_at_utc=captured_at_utc,
            )
        except Exception as exc:
            raise CaptureRefusal("Frozen external-HOME runtime snapshot was refused.") from exc
        return _validate_profile_snapshot(
            snapshot,
            phase=phase,
            session_id=session_id,
            process_instance_id=process_instance_id,
            captured_at_utc=captured_at_utc,
        )

    def verify_prelaunch_once(self) -> dict[str, Any]:
        if self.prelaunch_call_count != 0:
            raise CaptureRefusal("Full external-HOME prelaunch proof may run only once.")
        verifier = self._load_verifier()
        self.prelaunch_call_count += 1
        try:
            proof = verifier.verify_prelaunch(self.repository_root)
        except Exception as exc:
            raise CaptureRefusal("Frozen external-HOME prelaunch verification was refused.") from exc
        return _validate_prelaunch_proof(proof)


class AnchoredStaging:
    def __init__(self, repository_root: pathlib.Path) -> None:
        self.repository_root = pathlib.Path(os.path.abspath(repository_root))
        self.prefix = self.repository_root / CAPTURE_STAGING_PREFIX

    def inspect_prefix(self) -> None:
        fd = _open_secure_directory(self.repository_root)
        try:
            for part in guard_relative_path(CAPTURE_STAGING_PREFIX):
                try:
                    child = os.open(
                        part,
                        os.O_RDONLY
                        | getattr(os, "O_DIRECTORY", 0)
                        | getattr(os, "O_NOFOLLOW", 0),
                        dir_fd=fd,
                    )
                except FileNotFoundError:
                    return
                except OSError as exc:
                    raise CaptureRefusal(
                        "Capture-staging ancestry is not a real directory."
                    ) from exc
                os.close(fd)
                fd = child
            info = os.fstat(fd)
            if info.st_uid != os.getuid() or stat.S_IMODE(info.st_mode) != 0o700:
                raise CaptureRefusal("Capture-staging root is not owned mode 0700.")
            for name in os.listdir(fd):
                item = os.stat(name, dir_fd=fd, follow_symlinks=False)
                if name == CAPTURE_COORDINATOR_STAGING_LOCK_NAME:
                    if (
                        not stat.S_ISREG(item.st_mode)
                        or item.st_nlink != 1
                        or item.st_uid != os.getuid()
                        or stat.S_IMODE(item.st_mode) != 0o600
                    ):
                        raise CaptureRefusal("Capture coordinator lease inode is unsafe.")
                    continue
                if (
                    stat.S_ISLNK(item.st_mode)
                    or not stat.S_ISDIR(item.st_mode)
                    or item.st_uid != os.getuid()
                    or stat.S_IMODE(item.st_mode) != 0o700
                    or not RUN_ID_RE.fullmatch(name)
                ):
                    raise CaptureRefusal(
                        "Capture staging contains symlink or unapproved debris."
                    )
        finally:
            os.close(fd)

    def ensure_prefix(self) -> None:
        fd = _open_secure_directory(self.repository_root)
        try:
            parts = guard_relative_path(CAPTURE_STAGING_PREFIX)
            for index, part in enumerate(parts):
                try:
                    child = os.open(
                        part,
                        os.O_RDONLY
                        | getattr(os, "O_DIRECTORY", 0)
                        | getattr(os, "O_NOFOLLOW", 0),
                        dir_fd=fd,
                    )
                except FileNotFoundError:
                    os.mkdir(part, 0o700, dir_fd=fd)
                    child = os.open(
                        part,
                        os.O_RDONLY
                        | getattr(os, "O_DIRECTORY", 0)
                        | getattr(os, "O_NOFOLLOW", 0),
                        dir_fd=fd,
                    )
                info = os.fstat(child)
                if not stat.S_ISDIR(info.st_mode):
                    os.close(child)
                    raise CaptureRefusal("Capture-staging ancestry changed type.")
                if index == len(parts) - 1 and (
                    info.st_uid != os.getuid()
                    or stat.S_IMODE(info.st_mode) != 0o700
                ):
                    os.close(child)
                    raise CaptureRefusal("Capture-staging root is not owned mode 0700.")
                os.close(fd)
                fd = child
        finally:
            os.close(fd)
        self.inspect_prefix()

    def create_run_directory(self, run_id: str) -> pathlib.Path:
        if not RUN_ID_RE.fullmatch(run_id):
            raise CaptureRefusal("Run ID is not the fixed lowercase namespace format.")
        self.ensure_prefix()
        prefix_fd = _open_secure_directory(self.prefix)
        try:
            os.mkdir(run_id, 0o700, dir_fd=prefix_fd)
        except FileExistsError:
            raise CaptureRefusal("Capture run namespace already exists.") from None
        finally:
            os.close(prefix_fd)
        run_path = self.prefix / run_id
        _require_owned_private_directory(run_path)
        return run_path

    def create_session_directory(
        self, run_path: pathlib.Path, ordinal: int, session_id: str
    ) -> pathlib.Path:
        expected = SESSION_IDS[ordinal]
        if session_id != expected:
            raise CaptureRefusal("Session is not the exact next fixed session.")
        _require_owned_private_directory(run_path)
        name = f"{ordinal:02d}-{session_id}"
        run_fd = _open_secure_directory(run_path)
        try:
            os.mkdir(name, 0o700, dir_fd=run_fd)
        except FileExistsError:
            raise CaptureRefusal("Capture session directory already exists.") from None
        finally:
            os.close(run_fd)
        session_path = run_path / name
        _require_owned_private_directory(session_path)
        return session_path

    @staticmethod
    def create_new_file(
        directory: pathlib.Path, name: str, *, mode: int = 0o600
    ) -> BinaryIO:
        if "/" in name or name in {"", ".", ".."}:
            raise CaptureRefusal("Capture filename is not one fixed leaf name.")
        directory_fd = _open_secure_directory(directory)
        try:
            directory_info = os.fstat(directory_fd)
            if (
                directory_info.st_uid != os.getuid()
                or stat.S_IMODE(directory_info.st_mode) != 0o700
            ):
                raise CaptureRefusal("Capture output directory is not owned mode 0700.")
            flags = (
                os.O_WRONLY
                | os.O_CREAT
                | os.O_EXCL
                | getattr(os, "O_NOFOLLOW", 0)
            )
            try:
                fd = os.open(name, flags, mode, dir_fd=directory_fd)
            except FileExistsError:
                raise CaptureRefusal(
                    f"Create-new capture file already exists: {name}"
                ) from None
            info = os.fstat(fd)
            if (
                not stat.S_ISREG(info.st_mode)
                or info.st_nlink != 1
                or info.st_uid != os.getuid()
                or stat.S_IMODE(info.st_mode) != mode
            ):
                os.close(fd)
                raise CaptureRefusal("Create-new capture file identity is unsafe.")
        finally:
            os.close(directory_fd)
        return os.fdopen(fd, "wb", buffering=0)

    @staticmethod
    def write_new_json(
        directory: pathlib.Path,
        name: str,
        value: Any,
        *,
        verifier_canonical_lf: bool = False,
    ) -> FileIdentity:
        data = canonical_lf_bytes(value) if verifier_canonical_lf else canonical_bytes(value)
        with AnchoredStaging.create_new_file(directory, name) as stream:
            written = stream.write(data)
            if written != len(data):
                raise CaptureRefusal("Create-new JSON write was incomplete.")
            stream.flush()
            os.fsync(stream.fileno())
            info = os.fstat(stream.fileno())
            if (
                not stat.S_ISREG(info.st_mode)
                or info.st_nlink != 1
                or info.st_uid != os.getuid()
                or stat.S_IMODE(info.st_mode) != 0o600
                or info.st_size != len(data)
            ):
                raise CaptureRefusal("Create-new JSON identity changed while writing.")
            return FileIdentity(
                real_path=os.path.realpath(directory / name),
                device=info.st_dev,
                inode=info.st_ino,
                byte_length=info.st_size,
                sha256=sha256_bytes(data),
                bytes_value=data,
            )


def _event_manifest(header: Mapping[str, Any], event: Mapping[str, Any]) -> bytes:
    payload_sha = sha256_bytes(canonical_bytes(event["payload"]))
    text = (
        "id65-v3-runtime-capture-event-v2\n"
        f"format={header['format']}\n"
        f"formatVersion={header['formatVersion']}\n"
        f"runId={header['runId']}\n"
        f"sessionId={header['sessionId']}\n"
        f"sessionOrdinal={header['sessionOrdinal']}\n"
        f"processInstanceId={header['processInstanceId']}\n"
        f"runOriginSha256={header['runOriginSha256']}\n"
        f"bootIdentity={header['bootIdentity']}\n"
    )
    text += "".join(f"{name}={header[name]}\n" for name in CAPTURE_PIN_KEYS)
    text += (
        f"sequence={event['sequence']}\n"
        f"eventType={event['eventType']}\n"
        f"atUtc={event['atUtc']}\n"
        f"monotonicMicroseconds={event['monotonicMicroseconds']}\n"
        f"previousEventSha256={event['previousEventSha256']}\n"
        f"payloadSha256={payload_sha}\n"
    )
    return text.encode("utf-8", "strict")


class JournalWriter:
    _SEALED_LEAF = "capture-event-journal.json"

    def __init__(
        self,
        directory: pathlib.Path,
        header: dict[str, Any],
        previous_hash: str,
        clock: Callable[[], ClockSample],
        on_logical_commit: Callable[[], None] | None = None,
    ) -> None:
        self.directory = directory
        self.header = header
        self.previous_hash = previous_hash
        self.clock = clock
        self._on_logical_commit = on_logical_commit
        self.events: list[dict[str, Any]] = []
        self._last_clock: ClockSample | None = None
        self._provisional_directory_fd: int | None = None
        self._provisional_file_fd: int | None = None
        self._provisional_identity: tuple[int, int] | None = None
        self._projected_seal_bytes: bytes | None = None
        self._projected_live_bytes: bytes | None = None
        self._live_identity: tuple[int, int] | None = None
        self._seal_file_fsync_complete = False
        self._seal_directory_fsync_complete = False
        self._seal_readback_verified = False
        self._terminal_fsync_complete = False
        self._terminal_readback_verified = False
        self._terminal_tail_cleanup_failed = False
        self._logical_commit_complete = False
        self._live = AnchoredStaging.create_new_file(
            directory, "capture-events-live.ndjson"
        )

    def append(
        self,
        event_type: str,
        payload: dict[str, Any],
        sample: ClockSample | None = None,
    ) -> dict[str, Any]:
        event, sample = self._project_event(event_type, payload, sample)
        line = canonical_bytes(event) + b"\n"
        self._live.write(line)
        self._live.flush()
        os.fsync(self._live.fileno())
        self.events.append(event)
        self.previous_hash = event["eventSha256"]
        self._last_clock = sample
        return event

    def _project_event(
        self,
        event_type: str,
        payload: dict[str, Any],
        sample: ClockSample | None,
    ) -> tuple[dict[str, Any], ClockSample]:
        if len(self.events) >= MAX_JOURNAL_EVENTS:
            raise CaptureRefusal("Capture journal exceeds its fixed event bound.")
        sample = sample or self.clock()
        parse_utc(sample.at_utc)
        if not 0 <= sample.monotonic_microseconds <= MAX_SAFE_INTEGER:
            raise CaptureRefusal("Monotonic clock is outside the I-JSON range.")
        if self._last_clock is not None:
            if not _within_session_clock_step_is_accepted(
                self._last_clock, sample
            ):
                raise CaptureRefusal("UTC and monotonic capture clocks diverged.")
        event: dict[str, Any] = {
            "atUtc": sample.at_utc,
            "eventType": event_type,
            "monotonicMicroseconds": sample.monotonic_microseconds,
            "payload": payload,
            "previousEventSha256": self.previous_hash,
            "sequence": len(self.events),
        }
        event["eventSha256"] = sha256_bytes(_event_manifest(self.header, event))
        return event, sample

    def project_terminal(
        self,
        payload: dict[str, Any],
        sample: ClockSample,
    ) -> dict[str, Any]:
        if self._live.closed:
            raise CaptureRefusal("Capture journal is already closed.")
        event, _ = self._project_event("session-sealed", payload, sample)
        journal = dict(self.header)
        journal["events"] = [*self.events, event]
        return journal

    def commit_projected_seal(self, journal: dict[str, Any]) -> FileIdentity:
        if self._live.closed:
            raise CaptureRefusal("Capture journal closed before its sealed projection commit.")
        if self._provisional_directory_fd is not None:
            raise CaptureRefusal("Capture journal already owns a provisional seal.")
        data = canonical_bytes(journal)
        self._projected_seal_bytes = data
        directory_fd = _open_secure_directory(self.directory)
        self._provisional_directory_fd = directory_fd
        try:
            directory_info = os.fstat(directory_fd)
            if (
                not stat.S_ISDIR(directory_info.st_mode)
                or directory_info.st_uid != os.getuid()
                or stat.S_IMODE(directory_info.st_mode) != 0o700
            ):
                raise CaptureRefusal(
                    "Provisional sealed journal directory is not owned mode 0700."
                )
            file_fd = self._open_provisional_seal(directory_fd)
            self._provisional_file_fd = file_fd
            created = os.fstat(file_fd)
            if (
                not stat.S_ISREG(created.st_mode)
                or created.st_nlink != 1
                or created.st_uid != os.getuid()
                or stat.S_IMODE(created.st_mode) != 0o600
            ):
                raise CaptureRefusal(
                    "Provisional sealed journal create-new identity is unsafe."
                )
            self._provisional_identity = (created.st_dev, created.st_ino)
            self._write_provisional_seal(file_fd, data)
            self._fsync_provisional_seal(file_fd)
            self._seal_file_fsync_complete = True
            self._fsync_provisional_directory(directory_fd)
            self._seal_directory_fsync_complete = True
            info = os.fstat(file_fd)
            if (
                not stat.S_ISREG(info.st_mode)
                or info.st_nlink != 1
                or info.st_uid != os.getuid()
                or stat.S_IMODE(info.st_mode) != 0o600
                or (info.st_dev, info.st_ino) != self._provisional_identity
                or info.st_size != len(data)
            ):
                raise CaptureRefusal(
                    "Provisional sealed journal identity changed while writing."
                )
            return FileIdentity(
                real_path=os.path.realpath(self.directory / self._SEALED_LEAF),
                device=info.st_dev,
                inode=info.st_ino,
                byte_length=info.st_size,
                sha256=sha256_bytes(data),
                bytes_value=data,
            )
        except OSError as exc:
            raise CaptureRefusal("Provisional sealed journal commit failed.") from exc

    def confirm_projected_seal_readback(
        self,
        identity: FileIdentity,
        projected_journal: dict[str, Any],
    ) -> None:
        expected = canonical_bytes(projected_journal)
        if (
            self._projected_seal_bytes != expected
            or identity.bytes_value != expected
            or identity.sha256 != sha256_bytes(expected)
            or self._provisional_file_fd is None
            or self._provisional_identity is None
            or (identity.device, identity.inode) != self._provisional_identity
            or not self._seal_file_fsync_complete
            or not self._seal_directory_fsync_complete
        ):
            raise CaptureRefusal(
                "Provisional sealed journal readback did not confirm its durable projection."
            )
        self._seal_readback_verified = True

    def _open_provisional_seal(self, directory_fd: int) -> int:
        try:
            return os.open(
                self._SEALED_LEAF,
                os.O_RDWR
                | os.O_CREAT
                | os.O_EXCL
                | getattr(os, "O_NOFOLLOW", 0),
                0o600,
                dir_fd=directory_fd,
            )
        except FileExistsError:
            raise CaptureRefusal(
                "Create-new capture file already exists: " + self._SEALED_LEAF
            ) from None

    @staticmethod
    def _write_provisional_seal(file_fd: int, data: bytes) -> None:
        offset = 0
        while offset < len(data):
            written = os.write(file_fd, data[offset:])
            if written <= 0:
                raise CaptureRefusal("Provisional sealed journal write was incomplete.")
            offset += written

    @staticmethod
    def _fsync_provisional_seal(file_fd: int) -> None:
        os.fsync(file_fd)

    @staticmethod
    def _fsync_provisional_directory(directory_fd: int) -> None:
        os.fsync(directory_fd)

    def commit_projected_terminal(
        self,
        projected_journal: dict[str, Any],
        sample: ClockSample,
    ) -> None:
        if self._live.closed:
            raise CaptureRefusal("Capture journal closed before terminal commit.")
        if (
            self._provisional_directory_fd is None
            or self._provisional_file_fd is None
            or self._provisional_identity is None
        ):
            raise CaptureRefusal("Capture journal has no verified provisional seal.")
        projected_events = projected_journal.get("events")
        if (
            not isinstance(projected_events, list)
            or projected_journal != {**self.header, "events": projected_events}
            or len(projected_events) != len(self.events) + 1
            or projected_events[:-1] != self.events
        ):
            raise CaptureRefusal("Projected terminal journal changed before commit.")
        projected_event = projected_events[-1]
        if (
            not isinstance(projected_event, dict)
            or projected_event.get("eventType") != "session-sealed"
        ):
            raise CaptureRefusal("Projected terminal event is not session-sealed.")
        regenerated, regenerated_sample = self._project_event(
            "session-sealed", projected_event.get("payload"), sample
        )
        if regenerated != projected_event:
            raise CaptureRefusal("Projected terminal event changed before commit.")
        terminal_line = canonical_bytes(projected_event) + b"\n"
        live_fd = self._live.fileno()
        before = os.fstat(live_fd)
        precommit_size = before.st_size
        expected_live = b"".join(
            canonical_bytes(event) + b"\n" for event in projected_events
        )
        self._projected_live_bytes = expected_live
        self._live_identity = (before.st_dev, before.st_ino)
        try:
            with DeferredTerminationSignals():
                self._before_terminal_append(live_fd, terminal_line)
                self._write_terminal_line(live_fd, terminal_line)
                self._fsync_terminal_line(live_fd)
                self._terminal_fsync_complete = True
                after = os.fstat(live_fd)
                if (
                    after.st_dev != before.st_dev
                    or after.st_ino != before.st_ino
                    or after.st_mode != before.st_mode
                    or after.st_nlink != before.st_nlink
                    or after.st_uid != before.st_uid
                    or after.st_size != precommit_size + len(terminal_line)
                ):
                    raise CaptureRefusal(
                        "Live terminal journal identity changed during commit."
                    )
                live_readback = read_regular_file(
                    self.directory / "capture-events-live.ndjson",
                    include_bytes=True,
                    expected_owner=os.getuid(),
                    expected_mode=0o600,
                    maximum_bytes=MAX_TEXT_BYTES,
                )
                if live_readback.bytes_value != expected_live:
                    raise CaptureRefusal(
                        "Live terminal journal failed exact immediate readback."
                    )
                self._terminal_readback_verified = True
                self._before_terminal_logical_commit()
                if not self._durably_resolve_terminal_projection():
                    raise CaptureRefusal(
                        "Terminal journal files were not durably exact before logical commit."
                    )
        except BaseException as exc:
            if self.resolve_terminal_outcome():
                return
            try:
                self._rollback_terminal_tail(live_fd, precommit_size)
            except BaseException as cleanup_exc:
                self._terminal_tail_cleanup_failed = True
                raise CaptureRefusal(
                    "Live terminal tail rollback did not complete."
                ) from cleanup_exc
            raise exc
        try:
            self._close_live_stream()
        except BaseException:
            pass
        try:
            self._close_provisional_handles()
        except BaseException:
            pass

    @staticmethod
    def _before_terminal_append(file_fd: int, data: bytes) -> None:
        del file_fd, data

    @staticmethod
    def _write_terminal_line(file_fd: int, data: bytes) -> None:
        offset = 0
        while offset < len(data):
            written = os.write(file_fd, data[offset:])
            if written <= 0:
                raise CaptureRefusal("Live terminal journal write was incomplete.")
            offset += written

    @staticmethod
    def _fsync_terminal_line(file_fd: int) -> None:
        os.fsync(file_fd)

    def _before_terminal_logical_commit(self) -> None:
        pass

    @staticmethod
    def _rollback_terminal_tail(live_fd: int, precommit_size: int) -> None:
        os.ftruncate(live_fd, precommit_size)
        os.lseek(live_fd, precommit_size, os.SEEK_SET)
        os.fsync(live_fd)
        if os.fstat(live_fd).st_size != precommit_size:
            raise CaptureRefusal("Live terminal tail remained after rollback.")

    def resolve_terminal_outcome(self) -> bool:
        if self._logical_commit_complete:
            return True
        if not (
            self._seal_file_fsync_complete
            and self._seal_directory_fsync_complete
            and self._seal_readback_verified
            and self._terminal_fsync_complete
            and self._terminal_readback_verified
        ):
            return False
        try:
            with DeferredTerminationSignals():
                if not self._durably_resolve_terminal_projection():
                    return False
        except BaseException:
            if not self._logical_commit_complete:
                return False
        try:
            self._close_live_stream()
        except BaseException:
            pass
        try:
            self._close_provisional_handles()
        except BaseException:
            pass
        return True

    def _durably_resolve_terminal_projection(self) -> bool:
        if not self._terminal_files_are_current_exact_projection():
            return False
        if (
            self._provisional_file_fd is None
            or self._provisional_directory_fd is None
            or self._live.closed
        ):
            return False
        self._fsync_resolved_seal(self._provisional_file_fd)
        self._fsync_resolved_live(self._live.fileno())
        self._fsync_resolved_directory(self._provisional_directory_fd)
        if not self._terminal_files_are_current_exact_projection():
            return False
        self._mark_logical_commit()
        return True

    def _terminal_files_are_current_exact_projection(self) -> bool:
        directory_fd = self._provisional_directory_fd
        seal_fd = self._provisional_file_fd
        if (
            directory_fd is None
            or seal_fd is None
            or self._provisional_identity is None
            or self._live_identity is None
            or self._projected_seal_bytes is None
            or self._projected_live_bytes is None
            or self._live.closed
        ):
            return False
        try:
            directory_before = os.fstat(directory_fd)
            seal_before = os.fstat(seal_fd)
            seal_path_before = os.stat(
                self._SEALED_LEAF,
                dir_fd=directory_fd,
                follow_symlinks=False,
            )
            live_fd = self._live.fileno()
            live_before = os.fstat(live_fd)
            live_path_before = os.stat(
                "capture-events-live.ndjson",
                dir_fd=directory_fd,
                follow_symlinks=False,
            )
            if not self._terminal_identity_is_safe(
                seal_before,
                seal_path_before,
                self._provisional_identity,
                len(self._projected_seal_bytes),
            ) or not self._terminal_identity_is_safe(
                live_before,
                live_path_before,
                self._live_identity,
                len(self._projected_live_bytes),
            ):
                return False
            seal_bytes = self._pread_exact_projection(
                seal_fd, len(self._projected_seal_bytes)
            )
            live_projection = read_regular_file(
                self.directory / "capture-events-live.ndjson",
                include_bytes=True,
                expected_owner=os.getuid(),
                expected_mode=0o600,
                maximum_bytes=len(self._projected_live_bytes),
            )
            live_bytes = live_projection.bytes_value
            if (
                live_projection.device,
                live_projection.inode,
            ) != self._live_identity:
                return False
            directory_after = os.fstat(directory_fd)
            seal_after = os.fstat(seal_fd)
            seal_path_after = os.stat(
                self._SEALED_LEAF,
                dir_fd=directory_fd,
                follow_symlinks=False,
            )
            live_after = os.fstat(live_fd)
            live_path_after = os.stat(
                "capture-events-live.ndjson",
                dir_fd=directory_fd,
                follow_symlinks=False,
            )
        except (CaptureRefusal, OSError, ValueError):
            return False
        if (
            not stat.S_ISDIR(directory_before.st_mode)
            or directory_before.st_uid != os.getuid()
            or stat.S_IMODE(directory_before.st_mode) != 0o700
            or self._full_stat_identity(directory_after)
            != self._full_stat_identity(directory_before)
            or self._full_stat_identity(seal_after)
            != self._full_stat_identity(seal_before)
            or self._full_stat_identity(seal_path_after)
            != self._full_stat_identity(seal_path_before)
            or self._full_stat_identity(live_after)
            != self._full_stat_identity(live_before)
            or self._full_stat_identity(live_path_after)
            != self._full_stat_identity(live_path_before)
        ):
            return False
        return (
            seal_bytes == self._projected_seal_bytes
            and live_bytes == self._projected_live_bytes
        )

    @staticmethod
    def _full_stat_identity(value: os.stat_result) -> tuple[int, ...]:
        return (
            value.st_dev,
            value.st_ino,
            value.st_mode,
            value.st_nlink,
            value.st_uid,
            value.st_gid,
            value.st_size,
            value.st_mtime_ns,
            value.st_ctime_ns,
        )

    @staticmethod
    def _terminal_identity_is_safe(
        fd_info: os.stat_result,
        path_info: os.stat_result,
        expected_identity: tuple[int, int],
        expected_length: int,
    ) -> bool:
        return (
            stat.S_ISREG(fd_info.st_mode)
            and fd_info.st_nlink == 1
            and fd_info.st_uid == os.getuid()
            and stat.S_IMODE(fd_info.st_mode) == 0o600
            and (fd_info.st_dev, fd_info.st_ino) == expected_identity
            and fd_info.st_size == expected_length
            and stat.S_ISREG(path_info.st_mode)
            and path_info.st_nlink == 1
            and path_info.st_uid == os.getuid()
            and stat.S_IMODE(path_info.st_mode) == 0o600
            and (path_info.st_dev, path_info.st_ino) == expected_identity
            and path_info.st_size == expected_length
        )

    @staticmethod
    def _pread_exact_projection(fd: int, length: int) -> bytes:
        chunks: list[bytes] = []
        offset = 0
        while offset <= length:
            chunk = os.pread(fd, min(64 * 1024, length + 1 - offset), offset)
            if not chunk:
                break
            chunks.append(chunk)
            offset += len(chunk)
            if offset > length:
                break
        return b"".join(chunks)

    def _mark_logical_commit(self) -> None:
        if self._on_logical_commit is not None:
            self._on_logical_commit()
        self._logical_commit_complete = True

    @staticmethod
    def _fsync_resolved_seal(file_fd: int) -> None:
        os.fsync(file_fd)

    @staticmethod
    def _fsync_resolved_live(live_fd: int) -> None:
        os.fsync(live_fd)

    @staticmethod
    def _fsync_resolved_directory(directory_fd: int) -> None:
        os.fsync(directory_fd)

    @property
    def logical_commit_complete(self) -> bool:
        return self._logical_commit_complete

    @property
    def terminal_tail_cleanup_failed(self) -> bool:
        return self._terminal_tail_cleanup_failed

    @property
    def has_provisional_seal(self) -> bool:
        return self._provisional_directory_fd is not None

    def rollback_provisional_seal(self) -> None:
        if self._live.closed:
            raise CaptureRefusal("Cannot roll back a seal after closing its live journal.")
        directory_fd = self._provisional_directory_fd
        if directory_fd is None:
            return
        try:
            if self._provisional_file_fd is not None:
                info = os.fstat(self._provisional_file_fd)
                path_info = os.stat(
                    self._SEALED_LEAF,
                    dir_fd=directory_fd,
                    follow_symlinks=False,
                )
                if (
                    not stat.S_ISREG(info.st_mode)
                    or info.st_nlink != 1
                    or info.st_uid != os.getuid()
                    or stat.S_IMODE(info.st_mode) != 0o600
                    or self._provisional_identity is None
                    or (info.st_dev, info.st_ino) != self._provisional_identity
                    or not stat.S_ISREG(path_info.st_mode)
                    or path_info.st_nlink != 1
                    or path_info.st_uid != os.getuid()
                    or stat.S_IMODE(path_info.st_mode) != 0o600
                    or (path_info.st_dev, path_info.st_ino)
                    != self._provisional_identity
                ):
                    raise CaptureRefusal("Provisional sealed journal identity changed.")
                self._unlink_provisional_seal(directory_fd)
                try:
                    os.stat(
                        self._SEALED_LEAF,
                        dir_fd=directory_fd,
                        follow_symlinks=False,
                    )
                except FileNotFoundError:
                    pass
                else:
                    raise CaptureRefusal(
                        "Provisional sealed journal remained after rollback."
                    )
        except OSError as exc:
            raise CaptureRefusal("Provisional sealed journal rollback failed.") from exc
        else:
            self._close_provisional_handles()

    def _unlink_provisional_seal(self, directory_fd: int) -> None:
        os.unlink(self._SEALED_LEAF, dir_fd=directory_fd)
        os.fsync(directory_fd)

    def _close_provisional_handles(self) -> None:
        file_fd = self._provisional_file_fd
        directory_fd = self._provisional_directory_fd
        self._provisional_file_fd = None
        self._provisional_directory_fd = None
        self._provisional_identity = None
        first_error: BaseException | None = None
        if file_fd is not None:
            try:
                self._close_provisional_file(file_fd)
            except BaseException as error:
                first_error = error
        if directory_fd is not None:
            try:
                self._close_provisional_directory(directory_fd)
            except BaseException as error:
                if first_error is None:
                    first_error = error
        if first_error is not None:
            raise first_error

    def _close_live_stream(self) -> None:
        self._live.close()

    @staticmethod
    def _close_provisional_file(file_fd: int) -> None:
        os.close(file_fd)

    @staticmethod
    def _close_provisional_directory(directory_fd: int) -> None:
        os.close(directory_fd)

    def close_after_failed_cleanup(self) -> None:
        first_error: BaseException | None = None
        if not self._live.closed:
            try:
                self._close_live_stream()
            except BaseException as error:
                first_error = error
        try:
            self._close_provisional_handles()
        except BaseException as error:
            if first_error is None:
                first_error = error
        if first_error is not None:
            raise first_error

    def _require_sealed_leaf_absent(self) -> None:
        directory_fd = _open_secure_directory(self.directory)
        try:
            try:
                os.stat(
                    self._SEALED_LEAF,
                    dir_fd=directory_fd,
                    follow_symlinks=False,
                )
            except FileNotFoundError:
                return
            raise CaptureRefusal(
                "Capture abort cannot coexist with a sealed journal leaf."
            )
        finally:
            os.close(directory_fd)

    def abort(self, reason_code: str) -> None:
        if self._live.closed:
            return
        try:
            self._require_sealed_leaf_absent()
            self.append("capture-aborted", {"reasonCode": reason_code})
        finally:
            self._live.close()


def process_instance_id(
    run_id: str,
    boot_identity: str,
    process: ProcessObservation,
) -> str:
    manifest = (
        "id65-v3-runtime-process-instance-v2\n"
        f"runId={run_id}\n"
        f"bootIdentity={boot_identity}\n"
        f"pid={process.pid}\n"
        f"startSeconds={process.start_seconds}\n"
        f"startMicroseconds={process.start_microseconds}\n"
        f"executableDevice={process.executable.device}\n"
        f"executableInode={process.executable.inode}\n"
        f"executableSha256={process.executable.sha256}\n"
        f"argvNulSha256={process.argv_nul_sha256}\n"
        f"environmentNulSha256={process.environment_nul_sha256}\n"
        f"externalHomeRealPath={process.external_home_real_path}\n"
    ).encode("utf-8", "strict")
    return "id65v3-process-" + sha256_bytes(manifest)[:32]


def process_payload(process: ProcessObservation, observed: ClockSample) -> dict[str, Any]:
    return {
        "argv": list(process.argv),
        "argvNulSha256": process.argv_nul_sha256,
        "environmentNulSha256": process.environment_nul_sha256,
        "executableByteLength": process.executable.byte_length,
        "executableDevice": process.executable.device,
        "executableInode": process.executable.inode,
        "executableRealPath": process.executable.real_path,
        "executableSha256": process.executable.sha256,
        "externalHomeRealPath": process.external_home_real_path,
        "observedAtUtc": observed.at_utc,
        "parentPid": process.parent_pid,
        "pid": process.pid,
        "processStartedAtUtc": process.process_started_at_utc,
        "provider": "macos-libproc+kern-procargs2-external-home-v2",
        "startMicroseconds": process.start_microseconds,
        "startSeconds": process.start_seconds,
    }


def process_os_fingerprint_from_payload(payload: Mapping[str, Any]) -> str:
    fields = {
        name: payload[name]
        for name in (
            "argv", "argvNulSha256", "environmentNulSha256",
            "executableByteLength", "executableDevice",
            "executableInode", "executableRealPath", "executableSha256", "parentPid",
            "externalHomeRealPath", "pid", "processStartedAtUtc",
            "startMicroseconds", "startSeconds",
        )
    }
    return sha256_bytes(
        b"id65-v3-runtime-raw-os-process-fingerprint-v2\n" + canonical_bytes(fields)
    )


def process_os_fingerprint(process: ProcessObservation) -> str:
    return process_os_fingerprint_from_payload(
        process_payload(process, ClockSample("1970-01-01T00:00:00.000Z", 0))
    )


def disc_payload(
    process: ProcessObservation,
    cue: FileIdentity,
    bin_file: FileIdentity,
    bin_open: OpenFileObservation,
    observed: ClockSample,
) -> dict[str, Any]:
    return {
        "bindingMode": "exact-argv-cue+pinned-source-cue+read-only-open-bin-v1",
        "binByteLength": bin_file.byte_length,
        "binDevice": bin_file.device,
        "binFd": bin_open.fd,
        "binInode": bin_file.inode,
        "binLinkCount": bin_open.link_count,
        "binOpen": True,
        "binRealPath": bin_file.real_path,
        "binRepoRelativePath": CANDIDATE_BIN_RELATIVE_PATH,
        "binSha256": bin_file.sha256,
        "cueByteLength": cue.byte_length,
        "cueDevice": cue.device,
        "cueInode": cue.inode,
        "cueRealPath": cue.real_path,
        "cueRepoRelativePath": CANDIDATE_CUE_RELATIVE_PATH,
        "cueSha256": cue.sha256,
        "cueSourceLinkCount": 1,
        "failedV1Excluded": True,
        "gdbListenerPid": process.pid,
        "observedAtUtc": observed.at_utc,
        "pid": process.pid,
        "provider": "macos-libproc-argv-plus-open-bin-v1",
        "startMicroseconds": process.start_microseconds,
        "startSeconds": process.start_seconds,
    }


class CaptureCoordinator:
    def __init__(
        self,
        boundary: Boundary,
        *,
        source_guard: SourceGuard | Any | None = None,
        staging: AnchoredStaging | None = None,
        lease_factory: Callable[[], Any] | None = None,
        run_id_factory: Callable[[ClockSample], str] | None = None,
    ) -> None:
        self.boundary = boundary
        self.source = source_guard or SourceGuard(boundary.repository_root)
        self.staging = staging or AnchoredStaging(boundary.repository_root)
        self._lease_factory = lease_factory or (
            lambda: CoordinatorLease(boundary.repository_root)
        )
        self._run_id_factory = run_id_factory or self._generate_run_id
        self.last_committed_run_id: str | None = None

    def _record_logical_commit(self, run_id: str) -> None:
        self.last_committed_run_id = run_id

    def preflight(self) -> None:
        self.staging.inspect_prefix()
        self.boundary.require_capture_environment()
        if self.boundary.duckstation_processes():
            raise CaptureRefusal("DuckStation is already running.")
        if self.boundary.listener_pids():
            raise CaptureRefusal("The pinned GDB listener is already active.")
        self.source.validate_all()
        self.source.capture_profile(
            phase="preflight",
            session_id="preflight",
            process_instance_id="not-applicable",
            captured_at_utc="1970-01-01T00:00:00.000Z",
        )

    def _load_run(
        self, run_id: str, current_pins: Mapping[str, str] | None = None
    ) -> CaptureRunState:
        if not RUN_ID_RE.fullmatch(run_id):
            raise CaptureRefusal("Run ID is not the fixed lowercase namespace format.")
        self.staging.inspect_prefix()
        run_path = self.staging.prefix / run_id
        try:
            _require_owned_private_directory(run_path)
        except FileNotFoundError:
            return CaptureRunState(
                run_id,
                run_path,
                0,
                ZERO_SHA256,
                None,
                None,
                frozenset(),
                frozenset(),
            )
        origin_path = run_path / "run-origin.json"
        origin = self._read_canonical_json(origin_path)
        self._validate_origin(origin, run_id)
        run_origin_sha256 = sha256_bytes(canonical_bytes(origin))
        if current_pins is not None:
            for key, expected in current_pins.items():
                if origin.get(key) != expected:
                    raise CaptureRefusal("Run origin differs from current frozen source pins.")
        run_fd = _open_secure_directory(run_path)
        try:
            run_names = sorted(os.listdir(run_fd))
            for name in run_names:
                info = os.stat(name, dir_fd=run_fd, follow_symlinks=False)
                if name == "run-origin.json":
                    if (
                        not stat.S_ISREG(info.st_mode)
                        or info.st_nlink != 1
                        or info.st_uid != os.getuid()
                        or stat.S_IMODE(info.st_mode) != 0o600
                    ):
                        raise CaptureRefusal("Run origin inode is unsafe.")
                elif (
                    not stat.S_ISDIR(info.st_mode)
                    or info.st_uid != os.getuid()
                    or stat.S_IMODE(info.st_mode) != 0o700
                ):
                    raise CaptureRefusal("Capture run contains a nonprivate entry.")
        finally:
            os.close(run_fd)
        session_names = [name for name in run_names if name != "run-origin.json"]
        if len(run_names) != len(session_names) + 1:
            raise CaptureRefusal("Capture run has an extra or missing run-origin entry.")
        if len(session_names) > len(SESSION_IDS):
            raise CaptureRefusal("Capture run has too many session directories.")
        previous = ZERO_SHA256
        previous_clock: ClockSample | None = ClockSample(
            origin["createdAtUtc"], origin["createdMonotonicMicroseconds"]
        )
        previous_after: dict[str, Any] | None = None
        seen_raw_hashes: set[str] = set()
        seen_process_fingerprints: set[str] = set()
        for ordinal, session_id in enumerate(SESSION_IDS[: len(session_names)]):
            expected_name = f"{ordinal:02d}-{session_id}"
            if session_names[ordinal] != expected_name:
                raise CaptureRefusal("Capture run is not the exact fixed session prefix.")
            directory = run_path / session_names[ordinal]
            _require_owned_private_directory(directory)
            journal_identity = read_regular_file(
                directory / "capture-event-journal.json",
                include_bytes=True,
                expected_owner=os.getuid(),
                expected_mode=0o600,
                maximum_bytes=MAX_TEXT_BYTES,
            )
            journal = _decode_canonical_json_bytes(
                journal_identity.bytes_value or b"", "sealed capture journal"
            )
            verified = verify_journal(
                journal,
                expected_run_id=run_id,
                expected_session_id=session_id,
                expected_ordinal=ordinal,
                expected_previous_hash=previous,
                expected_run_origin_sha256=run_origin_sha256,
                expected_pins={name: origin[name] for name in CAPTURE_PIN_KEYS},
                expected_boot_identity=origin["bootIdentity"],
                previous_terminal_clock=previous_clock,
            )
            process_fingerprint = process_os_fingerprint_from_payload(
                verified.process_payload
            )
            if process_fingerprint in seen_process_fingerprints:
                raise CaptureRefusal(
                    "Capture run reused one raw OS process fingerprint across sessions."
                )
            seen_process_fingerprints.add(process_fingerprint)
            previous_after = self._verify_session_staging(
                run_id,
                ordinal,
                session_id,
                directory,
                journal,
                verified,
                previous_after,
                seen_raw_hashes,
            )
            previous = verified.terminal_hash
            previous_clock = verified.terminal_clock
        return CaptureRunState(
            run_id,
            run_path,
            len(session_names),
            previous,
            previous_clock,
            previous_after,
            frozenset(seen_raw_hashes),
            frozenset(seen_process_fingerprints),
        )

    def _verify_session_staging(
        self,
        run_id: str,
        ordinal: int,
        session_id: str,
        directory: pathlib.Path,
        journal: dict[str, Any],
        verified: JournalVerification,
        previous_after: dict[str, Any] | None,
        seen_raw_hashes: set[str],
        *,
        sealed_journal_present: bool = True,
        live_excludes_projected_terminal: bool = False,
    ) -> dict[str, Any]:
        _require_owned_private_directory(directory)
        table = _expected_raw_artifact_table(run_id, ordinal, session_id)
        metadata = {item["artifactId"]: item for item in verified.artifact_metadata}
        if set(metadata) != set(table) or len(metadata) != len(verified.artifact_metadata):
            raise CaptureRefusal("Session raw artifact set differs from the fixed table.")
        expected_files = {
            "capture-events-live.ndjson",
            *(leaf for _, leaf, _ in table.values()),
        }
        if sealed_journal_present:
            expected_files.add("capture-event-journal.json")
        directory_fd = _open_secure_directory(directory)
        try:
            actual_files = set(os.listdir(directory_fd))
        finally:
            os.close(directory_fd)
        if actual_files != expected_files:
            raise CaptureRefusal("Session has missing, extra, or renamed raw assets.")

        live_identity = read_regular_file(
            directory / "capture-events-live.ndjson",
            include_bytes=True,
            expected_owner=os.getuid(),
            expected_mode=0o600,
            maximum_bytes=MAX_TEXT_BYTES,
        )
        live_bytes = live_identity.bytes_value or b""
        if not live_bytes.endswith(b"\n") or b"\r" in live_bytes:
            raise CaptureRefusal("Live journal is not exact LF-terminated NDJSON.")
        live_events = [
            _decode_canonical_json_bytes(line, "live capture event")
            for line in live_bytes[:-1].split(b"\n")
        ]
        expected_live_events = (
            journal["events"][:-1]
            if live_excludes_projected_terminal
            else journal["events"]
        )
        if live_events != expected_live_events:
            raise CaptureRefusal("Live NDJSON differs from the sealed journal projection.")

        profiles: dict[str, dict[str, Any]] = {}
        watcher_samples: list[dict[str, Any]] | None = None
        session_hashes: set[str] = set()
        for artifact_id, (kind, leaf, role) in table.items():
            item = metadata[artifact_id]
            expected_relative = (
                f"{CAPTURE_STAGING_PREFIX}/{run_id}/{ordinal:02d}-{session_id}/{leaf}"
            )
            if (
                item["kind"] != kind
                or item["repoRelativePath"] != expected_relative
                or (role is not None and item.get("captureRole") != role)
            ):
                raise CaptureRefusal("Raw artifact metadata differs from its fixed table.")
            path = directory / leaf
            if kind == "screen-recording":
                identity = read_regular_file(
                    path,
                    expected_owner=os.getuid(),
                    expected_mode=0o600,
                    maximum_bytes=MAX_MEDIA_BYTES,
                    validator=_validate_iso_bmff_fd,
                )
                data = None
            else:
                identity = read_regular_file(
                    path,
                    include_bytes=True,
                    expected_owner=os.getuid(),
                    expected_mode=0o600,
                    maximum_bytes=MAX_TEXT_BYTES,
                )
                data = identity.bytes_value or b""
            if identity.sha256 != item["sha256"] or identity.byte_length != item["byteLength"]:
                raise CaptureRefusal("Raw artifact hash or length changed.")
            if identity.sha256 in seen_raw_hashes:
                raise CaptureRefusal("Raw artifact bytes were reused across sessions.")
            if identity.sha256 in session_hashes:
                raise CaptureRefusal("Raw artifact bytes were reused across session roles.")
            session_hashes.add(identity.sha256)
            if kind.startswith("runtime-profile-"):
                profile = _decode_verifier_canonical_json_bytes(
                    data or b"", f"profile {artifact_id}"
                )
                self._validate_staged_profile(
                    profile, item, session_id, journal["processInstanceId"]
                )
                profiles[kind] = profile
            elif kind == "watcher-stdout":
                watcher_samples = self._parse_watcher_samples(data or b"")
            elif kind == "frame":
                _validate_png_bytes(data or b"")

        before = profiles.get("runtime-profile-before")
        after = profiles.get("runtime-profile-after")
        if before is None or after is None:
            raise CaptureRefusal("Session omits one profile bookend.")
        if previous_after is not None:
            for field in (
                "format", "formatVersion", "profileId",
                "duckStationExecutableSha256", "handoffReceiptSha256",
                "appContentManifestSha256", "appOutputTreeManifestSha256",
                "packageEnvelopeSha256", "externalHomeSettingsSha256",
                "externalHomeBiosSha256", "hostNormalSettingsSentinelId",
                "hostNormalSettingsSentinelSha256",
            ):
                if before[field] != previous_after[field]:
                    raise CaptureRefusal(
                        f"Adjacent session immutable profile field changed: {field}."
                    )
            if parse_utc(before["capturedAtUtc"]) <= parse_utc(
                previous_after["capturedAtUtc"]
            ):
                raise CaptureRefusal(
                    "Adjacent session profile snapshot clocks are not strictly ordered."
                )
        self._validate_profile_transition(before, after)
        if watcher_samples is None:
            raise CaptureRefusal("Session watcher bytes were not parsed.")
        self._validate_journal_cross_bindings(
            journal, verified, metadata, watcher_samples
        )
        seen_raw_hashes.update(session_hashes)
        return after

    @staticmethod
    def _validate_staged_profile(
        profile: dict[str, Any],
        artifact: dict[str, Any],
        session_id: str,
        process_instance: str,
    ) -> None:
        expected_phase = "before" if artifact["kind"].endswith("before") else "after"
        _validate_profile_snapshot(
            profile,
            phase=expected_phase,
            session_id=session_id,
            process_instance_id=process_instance,
            captured_at_utc=artifact["capturedAtUtc"],
        )
        if profile["snapshotId"] != artifact["artifactId"]:
            raise CaptureRefusal("Staged profile snapshot ID differs from its artifact ID.")

    def _validate_journal_cross_bindings(
        self,
        journal: dict[str, Any],
        verified: JournalVerification,
        metadata: Mapping[str, dict[str, Any]],
        samples: Sequence[dict[str, Any]],
    ) -> None:
        events = journal["events"]
        by_type: dict[str, list[dict[str, Any]]] = {}
        for event in events:
            by_type.setdefault(event["eventType"], []).append(event)
        process = verified.process_payload
        executable = self.source.executable_identity()
        expected_cue_path = os.path.realpath(self.source.cue_path)
        expected_disc_arguments = [
            argument
            for argument in process["argv"]
            if argument.lower().endswith((".cue", ".bin"))
        ]
        if (
            process["executableSha256"] != EXPECTED_DUCKSTATION_SHA256
            or process["executableRealPath"] != os.path.realpath(self.source.executable_path)
            or process["executableDevice"] != executable.device
            or process["executableInode"] != executable.inode
            or process["executableByteLength"] != executable.byte_length
            or process["argv"]
            != [os.path.realpath(self.source.executable_path), expected_cue_path]
            or process["externalHomeRealPath"]
            != os.path.realpath(self.source.external_home_root)
            or expected_disc_arguments != [expected_cue_path]
        ):
            raise CaptureRefusal(
                "Journal process or argv differs from the pinned executable and CUE."
            )
        disc = by_type["disc-open-observed"][0]["payload"]
        cue, bin_file = self.source.candidate_identities()
        for prefix, identity in (("cue", cue), ("bin", bin_file)):
            if any(
                disc[f"{prefix}{suffix}"] != expected
                for suffix, expected in (
                    ("RealPath", identity.real_path), ("Device", identity.device),
                    ("Inode", identity.inode), ("ByteLength", identity.byte_length),
                    ("Sha256", identity.sha256),
                )
            ):
                raise CaptureRefusal("Journal disc observation differs from current pinned inode.")
        if (
            disc["bindingMode"]
            != "exact-argv-cue+pinned-source-cue+read-only-open-bin-v1"
            or disc["cueSourceLinkCount"] != 1
            or disc["binOpen"] is not True
            or disc["binLinkCount"] != 1
            or re.fullmatch(r"[0-9]+r", disc["binFd"]) is None
        ):
            raise CaptureRefusal(
                "Journal CUE argv/source binding or open BIN descriptor changed."
            )
        if (
            by_type["watcher-connected"][0]["payload"]["ownedWatcherPid"]
            != by_type["watcher-stopped"][0]["payload"]["ownedWatcherPid"]
        ):
            raise CaptureRefusal("Watcher child PID changed across its lifecycle.")
        exit_payload = by_type["process-exit-observed"][0]["payload"]
        if any(exit_payload[name] != process[name] for name in ("pid", "startSeconds", "startMicroseconds")):
            raise CaptureRefusal("Process exit is not bound to the observed process.")
        sample_by_ordinal = {item["ordinal"]: item for item in samples}
        resumed_at = parse_utc(by_type["manually-resumed"][0]["atUtc"])
        stopped_at = parse_utc(by_type["watcher-stopped"][0]["atUtc"])
        if (
            parse_utc(samples[0]["atUtc"]) < resumed_at
            or parse_utc(samples[-1]["atUtc"]) > stopped_at
        ):
            raise CaptureRefusal("Watcher samples fall outside its resumed process window.")
        bound_samples: dict[str, dict[str, Any]] = {}
        for binding in by_type.get("watcher-marker-bound", []):
            payload = binding["payload"]
            sample = sample_by_ordinal.get(payload["sampleOrdinal"])
            if sample is None or (
                sample["atUtc"] != payload["sampleAtUtc"]
                or sample["elapsedMilliseconds"] != payload["sampleElapsedMilliseconds"]
            ):
                raise CaptureRefusal("Watcher marker binding differs from raw sample bytes.")
            sequence = payload["operatorEventSequence"]
            if not 0 <= sequence < len(events) or events[sequence]["eventType"] != payload["markerRole"]:
                raise CaptureRefusal("Watcher marker targets the wrong operator event.")
            role = payload["markerRole"]
            bound_samples[role] = sample
            if role in {"landing-marked", "hold-started", "hold-ended"} and not (
                sample["lives"] == 4
                and sample["state"] == 0
                and sample["playerZ"] >= 0x400
            ):
                raise CaptureRefusal("Watcher stable marker has an unsafe tuple.")
            if role == "fall-started":
                marker_time = parse_utc(events[sequence]["atUtc"])
                if not _fall_binding_is_accepted(
                    samples,
                    marker_time,
                    sample["ordinal"],
                ):
                    raise CaptureRefusal(
                        "Watcher fall marker lacks its exact safe authorization-to-death edge."
                    )
            if role == "respawn-marked" and not (
                sample["lives"] == 3
                and sample["state"] == 0
                and sample["playerZ"] >= 0x400
                and sample["ordinal"] > 0
            ):
                raise CaptureRefusal("Watcher respawn marker lacks its lives/state/stable recovery.")
            if role == "respawn-marked":
                marker_time = parse_utc(events[sequence]["atUtc"])
                fall_binding = next(
                    (
                        item for item in by_type.get("watcher-marker-bound", [])
                        if item["payload"]["markerRole"] == "fall-started"
                    ),
                    None,
                )
                if fall_binding is None or not _respawn_binding_is_accepted(
                    samples,
                    fall_binding["payload"]["sampleOrdinal"],
                    marker_time,
                    sample["ordinal"],
                ):
                    raise CaptureRefusal(
                        "Watcher respawn binding is not the first raw sample after its marker and fall."
                    )
        landing_sample = bound_samples.get("landing-marked")
        if landing_sample is None:
            raise CaptureRefusal("Watcher landing binding is missing.")
        if journal["sessionId"] == "candidate-primary-edge-0":
            completed_start = next(
                (
                    item
                    for item in by_type.get("check-started", [])
                    if item["payload"].get("markerId")
                    == "candidate.completed-landing-hud4"
                ),
                None,
            )
            selector_event = next(
                (
                    item
                    for item in by_type.get("selector-started", [])
                    if item["payload"].get("markerId") == "selector"
                ),
                None,
            )
            if completed_start is None or selector_event is None or not (
                _candidate_primary_landing_evidence_is_accepted(
                    samples,
                    selector_event["atUtc"],
                    selector_event["monotonicMicroseconds"],
                    completed_start["atUtc"],
                    completed_start["monotonicMicroseconds"],
                    int(landing_sample["ordinal"]),
                )
            ):
                raise CaptureRefusal(
                    "Primary landing evidence lacks the exact automated "
                    "state-0/state-3/state-1/state-0 transition and safe suffix."
                )
        if journal["sessionId"] in {
            "candidate-primary-edge-0",
            "candidate-edge-1",
            "candidate-edge-2",
        }:
            fall_sample = bound_samples.get("fall-started")
            respawn_sample = bound_samples.get("respawn-marked")
            if fall_sample is None or respawn_sample is None:
                raise CaptureRefusal("Watcher death episode binding is incomplete.")
            landing_ordinal = landing_sample["ordinal"]
            fall_ordinal = fall_sample["ordinal"]
            respawn_ordinal = respawn_sample["ordinal"]
            first_recovery = next(
                (
                    item for item in samples[fall_ordinal + 1 :]
                    if item["lives"] == 3
                    and item["state"] == 0
                    and item["playerZ"] >= 0x400
                ),
                None,
            )
            transitions = [
                f"{before['lives']}->{after['lives']}"
                for before, after in zip(samples[landing_ordinal:], samples[landing_ordinal + 1 :])
                if before["lives"] != after["lives"]
            ]
            if not (
                0 <= landing_ordinal < fall_ordinal < respawn_ordinal < len(samples)
                and first_recovery is not None
                and fall_ordinal < first_recovery["ordinal"] <= respawn_ordinal
                and all(
                    _watcher_sample_safe(item)
                    for item in samples[landing_ordinal:fall_ordinal]
                )
                and all(
                    item["lives"] == 3 and item["state"] in {0, 4}
                    for item in samples[fall_ordinal:first_recovery["ordinal"]]
                )
                and (
                    first_recovery["elapsedMilliseconds"]
                    - fall_sample["elapsedMilliseconds"]
                    <= MAX_POST_DEATH_RECOVERY_MILLISECONDS
                )
                and all(
                    item["lives"] == 3
                    and item["state"] == 0
                    and item["playerZ"] >= 0x400
                    for item in samples[first_recovery["ordinal"] :]
                )
                and transitions == ["4->3"]
            ):
                raise CaptureRefusal("Watcher death episode is not exact after landing.")
        elif journal["sessionId"] == "candidate-cold-reset" and not all(
            _watcher_sample_safe(item)
            for item in samples[landing_sample["ordinal"] :]
        ):
            raise CaptureRefusal("Watcher cold-reset session became unsafe after landing.")
        frame_events = {
            item["payload"]["artifactId"]: item["payload"]["captureRole"]
            for item in by_type.get("frame-captured", [])
        }
        bound_frames = {
            artifact_id: item.get("captureRole")
            for artifact_id, item in metadata.items() if item["kind"] == "frame"
        }
        if frame_events != bound_frames:
            raise CaptureRefusal("Frame events do not equal bound frame artifacts.")
        recording_start = by_type["recording-started"][0]["payload"]
        recording_stop = by_type["recording-stopped"][0]["payload"]
        recording = next(item for item in metadata.values() if item["kind"] == "screen-recording")
        for name in (
            "artifactId", "backend", "boundsHeight", "boundsWidth", "boundsX", "boundsY",
            "captureRole", "mediaStartAtUtc", "mediaStartMonotonicMicroseconds",
            "ownedRecorderPid", "ownerPid", "ownerStartMicroseconds", "ownerStartSeconds",
            "windowId", "windowOwnerPid",
        ):
            if recording_start[name] != recording[name]:
                raise CaptureRefusal("Recording start differs from bound media.")
        for name in (
            "artifactId", "mediaEndAtUtc", "mediaEndMonotonicMicroseconds",
            "ownedRecorderPid",
        ):
            if recording_stop[name] != recording[name]:
                raise CaptureRefusal("Recording stop differs from bound media.")

    def capture_session(self, session_id: str) -> str:
        # Refused environmental/source preflight must be filesystem side-effect free.
        self.last_committed_run_id = None
        self.preflight()
        pins = self.source.validate_all()
        planned = self._select_run(session_id, pins)
        if not planned.run_path.exists():
            if session_id != SESSION_IDS[0]:
                raise CaptureRefusal("Only the first fixed session may create a capture run.")
        lease = self._lease_factory()
        lease.__enter__()
        result: str | None = None
        primary_error: BaseException | None = None
        try:
            result = self._capture_session_owned(
                session_id,
                planned_run_id=planned.run_id,
                preflight_pins=pins,
            )
        except BaseException as exc:
            primary_error = exc
            if self.last_committed_run_id is not None:
                result = self.last_committed_run_id
                primary_error = None
        try:
            lease.__exit__(
                type(primary_error) if primary_error is not None else None,
                primary_error,
                primary_error.__traceback__ if primary_error is not None else None,
            )
        except BaseException as lease_error:
            if result is not None:
                return result
            if primary_error is not None:
                raise CaptureRefusal(
                    "Capture failed and its coordinator lease did not release cleanly."
                ) from lease_error
            raise
        if primary_error is not None:
            raise primary_error
        if result is None:
            raise CaptureRefusal("Capture returned without a committed run identifier.")
        return result

    def _capture_session_owned(
        self,
        session_id: str,
        *,
        planned_run_id: str,
        preflight_pins: Mapping[str, str],
    ) -> str:
        self.staging.inspect_prefix()
        self.boundary.require_capture_environment()
        if self.boundary.duckstation_processes() or self.boundary.listener_pids():
            raise CaptureRefusal("DuckStation or its listener appeared before run ownership.")
        pins = self.source.validate_all()
        if dict(pins) != dict(preflight_pins):
            raise CaptureRefusal("Frozen source pins changed across lease acquisition.")
        run_state = self._select_run(
            session_id, pins, proposed_run_id=planned_run_id
        )
        if run_state.run_id != planned_run_id:
            raise CaptureRefusal("Capture run selection changed across lease acquisition.")
        run_id = run_state.run_id
        run_path = run_state.run_path
        ordinal = run_state.session_count
        previous_hash = run_state.previous_hash
        if ordinal >= len(SESSION_IDS) or session_id != SESSION_IDS[ordinal]:
            raise CaptureRefusal("Requested session is not the exact next fixed session.")

        boot_identity = self.boundary.boot_identity()
        new_run = not run_path.exists()
        if new_run:
            if session_id != SESSION_IDS[0]:
                raise CaptureRefusal("Only the first fixed session may create a capture run.")
            proof = _validate_prelaunch_proof(
                self.source.verify_prelaunch_once()
            )
            run_path = self.staging.create_run_directory(run_id)
            origin_clock = self.boundary.clock()
            origin = {
                "bootIdentity": boot_identity,
                "captureStagingRepoRelativePath": f"{CAPTURE_STAGING_PREFIX}/{run_id}",
                "createdAtUtc": origin_clock.at_utc,
                "createdMonotonicMicroseconds": origin_clock.monotonic_microseconds,
                "format": RUN_FORMAT,
                "formatVersion": RUN_FORMAT_VERSION,
                "orderedSessionIds": list(SESSION_IDS),
                "prelaunchProof": proof,
                "prelaunchProofSha256": sha256_bytes(canonical_lf_bytes(proof)),
                "repositoryRootRealPath": os.path.realpath(self.boundary.repository_root),
                "runId": run_id,
                **{name: pins[name] for name in CAPTURE_PIN_KEYS},
            }
            origin_identity = AnchoredStaging.write_new_json(
                run_path, "run-origin.json", origin
            )
            run_origin_sha256 = origin_identity.sha256
        else:
            origin = self._read_canonical_json(run_path / "run-origin.json")
            self._validate_origin(origin, run_id)
            _validate_prelaunch_proof(origin["prelaunchProof"])
            run_origin_sha256 = sha256_bytes(canonical_bytes(origin))
            if origin["bootIdentity"] != boot_identity:
                raise CaptureRefusal("Capture run cannot cross a machine boot boundary.")
            for key, value in pins.items():
                if origin[key] != value:
                    raise CaptureRefusal("Capture run pin differs from current frozen source.")

        launch_not_before = self.boundary.clock()
        candidate_cue_real_path = os.path.realpath(self.source.cue_path)
        external_home_real_path = os.path.realpath(self.source.external_home_root)
        executable_real_path = os.path.realpath(self.source.executable_path)
        launch_argv = (
            "/usr/bin/env",
            "-i",
            *_runtime_environment_assignments(external_home_real_path),
            executable_real_path,
            candidate_cue_real_path,
        )
        launch_command = " ".join(shlex.quote(item) for item in launch_argv)
        self.boundary.confirm(
            "Manually launch the pinned external-HOME DuckStation from Terminal with "
            "this exact empty-environment command (do not use Finder or the normal HOME):\n"
            f"{launch_command}\n"
            "This coordinator will only observe it. Press Return when launched."
        )
        process = self.boundary.wait_for_duckstation(candidate_cue_real_path)
        self._validate_process(process)
        if parse_utc(process.process_started_at_utc) <= parse_utc(launch_not_before.at_utc):
            raise CaptureRefusal("Observed DuckStation did not start after the launch gate.")
        if self.boundary.listener_pids() != {process.pid}:
            raise CaptureRefusal("Port 2345 listener is not owned by exact DuckStation.")
        window = self.boundary.observe_window(process.pid)
        instance_id = process_instance_id(
            run_id, boot_identity, process
        )
        if process_os_fingerprint(process) in run_state.seen_process_fingerprints:
            raise CaptureRefusal(
                "A prior session already used this raw OS process fingerprint."
            )
        self._revalidate_process(process)
        before_clock = self.boundary.clock()
        before = self.source.capture_profile(
            phase="before",
            session_id=session_id,
            process_instance_id=instance_id,
            captured_at_utc=before_clock.at_utc,
        )
        self._revalidate_process(process)

        session_path = self.staging.create_session_directory(
            run_path, ordinal, session_id
        )
        header = {
            "bootIdentity": boot_identity,
            "format": FORMAT,
            "formatVersion": JOURNAL_FORMAT_VERSION,
            "processInstanceId": instance_id,
            "runOriginSha256": run_origin_sha256,
            "runId": run_id,
            "sessionId": session_id,
            "sessionOrdinal": ordinal,
            **{name: pins[name] for name in CAPTURE_PIN_KEYS},
        }
        journal = JournalWriter(
            session_path,
            header,
            previous_hash,
            self.boundary.clock,
            on_logical_commit=lambda: self._record_logical_commit(run_id),
        )
        owned_children: list[tuple[str, OwnedChild]] = []
        raw_artifacts: list[dict[str, Any]] = []
        watcher_reader: IncrementalWatcherReader | None = None
        recording_deadline: int | None = None
        try:
            journal.append(
                "session-open",
                {"previousSessionTerminalSha256": previous_hash},
            )
            before_identity = AnchoredStaging.write_new_json(
                session_path, "runtime-profile-before.json", before,
                verifier_canonical_lf=True,
            )
            before_relative = self._session_relative(
                run_id, ordinal, session_id, "runtime-profile-before.json"
            )
            before_meta = _artifact_metadata(
                f"{session_id}.profile-before",
                "runtime-profile-before",
                before_relative,
                before_clock.at_utc,
                before_identity,
            )
            raw_artifacts.append(before_meta)
            journal.append("profile-before-captured", before_meta)

            observed = self.boundary.clock()
            journal.append(
                "process-start-observed", process_payload(process, observed), observed
            )
            current_process, cue, bin_file = self._revalidate_process(process)
            bin_open = self._require_open_identity(current_process, bin_file)
            disc_observed = self.boundary.clock()
            journal.append(
                "disc-open-observed",
                disc_payload(
                    current_process,
                    cue,
                    bin_file,
                    bin_open,
                    disc_observed,
                ),
                disc_observed,
            )

            watcher_stream = AnchoredStaging.create_new_file(
                session_path, "watcher-stdout.log"
            )
            watcher_reader = IncrementalWatcherReader(
                session_path, watcher_stream.fileno()
            )
            watcher_child, watcher_stdin = self.boundary.start_watcher(
                watcher_stream, owned_children
            )
            watcher_connected = self.boundary.wait_for_watcher_connection(
                watcher_child, session_path / "watcher-stdout.log"
            )
            journal.append(
                "watcher-connected",
                {
                    "ownedWatcherPid": watcher_child.pid,
                    "provider": "pinned-watcher-subprocess-v1",
                },
                watcher_connected,
            )
            resumed = self._confirm_process(
                process,
                "LOCAL RESUME — activate the exact DuckStation GAMEPLAY window "
                "without moving or resizing it, then use the pinned TogglePause hotkey "
                "Space exactly once to Resume (never Escape/Start; do not open Pause, "
                "Settings, or any menu), verify controllable base gameplay with 4 "
                "lives, leave Spyro hands off, then return to this Terminal and press "
                "Return once. This one local confirmation starts the watcher; do not "
                "enter any load code yet."
            )
            watcher_stdin.write(b"\n")
            watcher_stdin.flush()
            journal.append("manually-resumed", {"operatorConfirmed": True}, resumed)

            self._revalidate_process(process)
            if self.boundary.observe_window(process.pid) != window:
                raise CaptureRefusal("DuckStation window changed before recording startup.")
            recording_directory_identity = _private_directory_identity(session_path)
            recording_child = self.boundary.start_recording(
                window,
                session_path,
                SCREEN_RECORDING_LEAF,
                owned_children,
            )
            if recording_child.poll() is not None:
                raise CaptureRefusal("Owned screen recorder exited during startup.")
            recording_started = self.boundary.clock()
            recording_deadline = (
                recording_started.monotonic_microseconds
                + MAX_RECORDING_DURATION_MICROSECONDS
            )
            self._revalidate_process(process)
            if self.boundary.observe_window(process.pid) != window:
                raise CaptureRefusal("DuckStation window changed during recording startup.")
            recording_artifact_id = f"{session_id}.screen-recording"
            recording_role = f"{session_id}.full-window-recording"
            journal.append(
                "recording-started",
                {
                    "artifactId": recording_artifact_id,
                    "backend": "macos-screencapture-window-v1",
                    "boundsHeight": window.height,
                    "boundsWidth": window.width,
                    "boundsX": window.x,
                    "boundsY": window.y,
                    "captureRole": recording_role,
                    "mediaStartAtUtc": recording_started.at_utc,
                    "mediaStartMonotonicMicroseconds": recording_started.monotonic_microseconds,
                    "ownedRecorderPid": recording_child.pid,
                    "ownerPid": process.pid,
                    "ownerStartMicroseconds": process.start_microseconds,
                    "ownerStartSeconds": process.start_seconds,
                    "windowId": window.window_id,
                    "windowOwnerPid": window.owner_pid,
                },
                recording_started,
            )
            if watcher_reader is None:
                raise CaptureRefusal("Live watcher reader was not retained.")
            resume_samples = self._live_watcher_snapshot(
                process,
                window,
                watcher_child,
                recording_child,
                watcher_reader,
                recording_deadline,
            )
            resume_ordinal = (
                int(resume_samples[-1]["ordinal"]) if resume_samples else -1
            )
            selector, _selector_sample_ordinal = self._wait_for_gameplay_ready(
                process,
                window,
                watcher_child,
                recording_child,
                watcher_reader,
                (
                    "AUTOMATIC READINESS ARMED — DO NOT ENTER ANY LOAD CODE and do "
                    "not touch the controller. The coordinator is now waiting for "
                    f"the exact 2.000-second/19-sample safe base-gameplay suffix for "
                    f"{session_id}; no Return or chat reply is needed."
                ),
                not_before_ordinal=resume_ordinal,
                recording_deadline_monotonic_microseconds=recording_deadline,
            )
            journal.append("selector-started", {"markerId": "selector"}, selector)
            self._require_live_gate_boundary(
                process, window, watcher_child, recording_child
            )
            self.boundary.announce(
                "READINESS ACCEPTED — ENTER CODE NOW for "
                f"{session_id}: {LOAD_SEQUENCE_INSTRUCTIONS[session_id]}. "
                "Enter that exact sequence once, wait for the destination landing, "
                "and keep the controller untouched until the next coordinator prompt. "
                "No chat reply is needed."
            )
            self._require_live_gate_boundary(
                process, window, watcher_child, recording_child
            )
            if session_id == "candidate-primary-edge-0":
                landing_check_start, automated_safe_ordinal = (
                    self._wait_for_candidate_primary_landing(
                        process,
                        window,
                        watcher_child,
                        recording_child,
                        watcher_reader,
                        selector,
                        journal,
                        recording_deadline,
                    )
                )
            if session_id in EDGE_ASSIGNMENTS:
                landing_prompt = (
                    "AUTOMATED LANDING TELEMETRY ACCEPTED. Before any movement, "
                    "visually confirm the destination rendered as one clean triangular "
                    "pad, the HUD shows exactly 4 lives, and untouched controllable "
                    "Spyro's BODY yaw 0x40 points toward visible apex C (+Y / 90-degree "
                    "source reference). If any check fails, stop; do not remap from "
                    "camera left/right and do not continue. Camera alignment is not "
                    "evidence of world direction. Press Return once only after this "
                    "unhurried visual confirmation."
                ) if session_id == "candidate-primary-edge-0" else (
                    "On the first completed landing with normal control, before any "
                    "movement, confirm that untouched controllable Spyro's BODY yaw "
                    "0x40 points toward visible apex C (+Y / 90-degree source "
                    "reference). If it does not, stop; do not remap from camera "
                    "left/right and do not continue. Camera alignment is not evidence "
                    "of world direction. Press Return only after that confirmation."
                )
            else:
                landing_prompt = (
                    "Wait for the first completed landing with normal control and no "
                    "movement input. Confirm the character is controllable, then press Return."
                )
            if session_id == "candidate-cold-reset":
                cold_eligible = self.boundary.wait_for_fresh_return_after(
                    selector,
                    5_500_000,
                    "Cold-reset load interval is eligible. Press a fresh Return, then "
                    "complete the live landing-readiness confirmation.",
                    recording_deadline,
                )
                _require_minimum_interval(
                    selector,
                    cold_eligible,
                    5_500_000,
                    "Cold-reset selector-to-landing countdown",
                )
            landing, safe_watcher_ordinal = self._confirm_gameplay_ready(
                process,
                window,
                watcher_child,
                recording_child,
                watcher_reader,
                landing_prompt,
                require_fresh_input=(session_id == "candidate-primary-edge-0"),
                recording_deadline_monotonic_microseconds=recording_deadline,
            )
            if session_id == "candidate-primary-edge-0":
                safe_watcher_ordinal = self._require_safe_growth(
                    process,
                    window,
                    watcher_child,
                    recording_child,
                    watcher_reader,
                    automated_safe_ordinal,
                    "automated landing visual confirmation",
                    recording_deadline,
                )
            journal.append("landing-marked", {"markerId": "landing"}, landing)
            if session_id == "candidate-primary-edge-0":
                self._require_live_gate_boundary(
                    process, window, watcher_child, recording_child
                )
                landing_check_end = self.boundary.clock()
                _require_minimum_interval(
                    landing_check_start,
                    landing_check_end,
                    3_500_000,
                    "Completed-landing interval",
                )
                journal.append(
                    "check-ended",
                    {"markerId": "candidate.completed-landing-hud4"},
                    landing_check_end,
                )
            self._capture_required_markers(
                session_id, process, window, session_path, run_id, ordinal,
                journal, raw_artifacts, watcher_child, recording_child,
                watcher_reader, safe_watcher_ordinal, recording_deadline
            )

            self._revalidate_process(process)
            if self.boundary.observe_window(process.pid) != window:
                raise CaptureRefusal("DuckStation window changed before recording stop.")
            recording_stop = self.boundary.stop_owned_child(
                recording_child, "screen recorder", recording_deadline
            )
            _require_recording_duration(
                recording_started.monotonic_microseconds,
                recording_stop.requested.monotonic_microseconds,
            )
            owned_children = [
                item for item in owned_children if item[1] is not recording_child
            ]
            recording_identity = read_regular_file(
                session_path / SCREEN_RECORDING_LEAF,
                expected_owner=os.getuid(),
                expected_mode=0o600,
                maximum_bytes=MAX_MEDIA_BYTES,
                validator=_validate_iso_bmff_fd,
                expected_parent_identity=recording_directory_identity,
            )
            journal.append(
                "recording-stopped",
                {
                    "artifactId": recording_artifact_id,
                    "mediaEndAtUtc": recording_stop.requested.at_utc,
                    "mediaEndMonotonicMicroseconds": recording_stop.requested.monotonic_microseconds,
                    "ownedRecorderPid": recording_child.pid,
                },
                recording_stop.observed,
            )
            watcher_stop = self.boundary.stop_owned_child(
                watcher_child, "pinned watcher", recording_deadline
            )
            owned_children = [
                item for item in owned_children if item[1] is not watcher_child
            ]
            watcher_stdin.close()
            watcher_reader.close()
            watcher_reader = None
            watcher_stream.close()
            watcher_identity = read_regular_file(
                session_path / "watcher-stdout.log", expected_owner=os.getuid()
            )
            watcher_meta = _artifact_metadata(
                f"{session_id}.watcher",
                "watcher-stdout",
                self._session_relative(
                    run_id, ordinal, session_id, "watcher-stdout.log"
                ),
                watcher_stop.observed.at_utc,
                watcher_identity,
            )
            raw_artifacts.append(watcher_meta)
            watcher_payload = dict(watcher_meta)
            watcher_payload["ownedWatcherPid"] = watcher_child.pid
            journal.append("watcher-stopped", watcher_payload, watcher_stop.observed)
            watcher_samples = self._parse_watcher_samples(
                session_path / "watcher-stdout.log"
            )
            self._append_watcher_marker_bindings(
                journal, session_id, watcher_samples
            )

            recording_meta = _artifact_metadata(
                recording_artifact_id,
                "screen-recording",
                self._session_relative(
                    run_id, ordinal, session_id, SCREEN_RECORDING_LEAF
                ),
                recording_stop.observed.at_utc,
                recording_identity,
            )
            recording_meta.update(
                {
                    "backend": "macos-screencapture-window-v1",
                    "boundsHeight": window.height,
                    "boundsWidth": window.width,
                    "boundsX": window.x,
                    "boundsY": window.y,
                    "captureRole": recording_role,
                    "mediaEndAtUtc": recording_stop.requested.at_utc,
                    "mediaEndMonotonicMicroseconds": (
                        recording_stop.requested.monotonic_microseconds
                    ),
                    "mediaStartAtUtc": recording_started.at_utc,
                    "mediaStartMonotonicMicroseconds": (
                        recording_started.monotonic_microseconds
                    ),
                    "ownedRecorderPid": recording_child.pid,
                    "ownerPid": process.pid,
                    "ownerStartMicroseconds": process.start_microseconds,
                    "ownerStartSeconds": process.start_seconds,
                    "windowId": window.window_id,
                    "windowOwnerPid": window.owner_pid,
                }
            )
            raw_artifacts.append(recording_meta)

            self.boundary.confirm(
                "Manually quit DuckStation now; this coordinator will only observe exit."
            )
            process_exit = self.boundary.wait_for_process_exit(process)
            journal.append(
                "process-exit-observed",
                {
                    "pid": process.pid,
                    "processExitedAtUtc": process_exit.at_utc,
                    "startMicroseconds": process.start_microseconds,
                    "startSeconds": process.start_seconds,
                },
                process_exit,
            )
            if self.boundary.listener_pids():
                raise CaptureRefusal("GDB listener remains after observed process exit.")
            after_clock = self.boundary.clock()
            after = self.source.capture_profile(
                phase="after",
                session_id=session_id,
                process_instance_id=instance_id,
                captured_at_utc=after_clock.at_utc,
            )
            self._validate_profile_transition(before, after)
            after_identity = AnchoredStaging.write_new_json(
                session_path, "runtime-profile-after.json", after,
                verifier_canonical_lf=True,
            )
            after_meta = _artifact_metadata(
                f"{session_id}.profile-after",
                "runtime-profile-after",
                self._session_relative(
                    run_id, ordinal, session_id, "runtime-profile-after.json"
                ),
                after_clock.at_utc,
                after_identity,
            )
            raw_artifacts.append(after_meta)
            journal.append("profile-after-captured", after_meta, after_clock)
            for media in sorted(
                (
                    item
                    for item in raw_artifacts
                    if item["kind"] in {"frame", "screen-recording"}
                ),
                key=lambda item: item["artifactId"],
            ):
                journal.append("media-artifact-bound", media)
            seal_ids = sorted(item["artifactId"] for item in raw_artifacts)
            self.source.validate_all()
            self.boundary.require_capture_environment()
            if self.boundary.duckstation_processes() or self.boundary.listener_pids():
                raise CaptureRefusal(
                    "Process or listener reappeared before terminal capture seal."
                )
            self._verify_raw_metadata(session_path, raw_artifacts)
            seal_payload = {"rawArtifactIds": seal_ids}
            seal_clock = self.boundary.clock()
            projected_journal = journal.project_terminal(seal_payload, seal_clock)
            verified = verify_journal(
                projected_journal,
                expected_run_id=run_id,
                expected_session_id=session_id,
                expected_ordinal=ordinal,
                expected_previous_hash=previous_hash,
                expected_run_origin_sha256=run_origin_sha256,
                expected_pins={name: pins[name] for name in CAPTURE_PIN_KEYS},
                expected_boot_identity=boot_identity,
                previous_terminal_clock=run_state.previous_clock
                or ClockSample(
                    origin["createdAtUtc"],
                    origin["createdMonotonicMicroseconds"],
                ),
            )
            verified_after = self._verify_session_staging(
                run_id,
                ordinal,
                session_id,
                session_path,
                projected_journal,
                verified,
                run_state.previous_after_profile,
                set(run_state.seen_raw_hashes),
                sealed_journal_present=False,
                live_excludes_projected_terminal=True,
            )
            if canonical_bytes(verified_after) != canonical_bytes(after):
                raise CaptureRefusal(
                    "Pre-seal profile self-verification changed the after bookend."
                )
            committed_journal = projected_journal
            sealed_identity = journal.commit_projected_seal(committed_journal)
            sealed_readback = read_regular_file(
                session_path / "capture-event-journal.json",
                include_bytes=True,
                expected_owner=os.getuid(),
                expected_mode=0o600,
                maximum_bytes=MAX_TEXT_BYTES,
            )
            if (
                sealed_readback.sha256 != sealed_identity.sha256
                or sealed_readback.bytes_value != canonical_bytes(committed_journal)
            ):
                raise CaptureRefusal("Sealed journal failed exact immediate readback.")
            journal.confirm_projected_seal_readback(
                sealed_readback, committed_journal
            )
            journal.commit_projected_terminal(projected_journal, seal_clock)
            return run_id
        except BaseException as exc:
            if journal.resolve_terminal_outcome():
                return run_id
            cleanup_errors: list[BaseException] = []
            seal_cleanup_failed = False
            unreaped_child = False
            if journal.has_provisional_seal:
                try:
                    journal.rollback_provisional_seal()
                except BaseException as cleanup_exc:
                    cleanup_errors.append(cleanup_exc)
                    seal_cleanup_failed = True
            if watcher_reader is not None:
                try:
                    watcher_reader.close()
                except BaseException as cleanup_exc:
                    cleanup_errors.append(cleanup_exc)
            for label, child in reversed(owned_children):
                try:
                    cleanup_exc = self.boundary.ensure_owned_child_reaped(
                        child, label
                    )
                except BaseException as cleanup_exc:
                    cleanup_errors.append(cleanup_exc)
                    unreaped_child = True
                else:
                    if cleanup_exc is not None:
                        cleanup_errors.append(cleanup_exc)
            if (
                not seal_cleanup_failed
                and not unreaped_child
                and not journal.terminal_tail_cleanup_failed
            ):
                try:
                    journal.abort(type(exc).__name__.lower())
                except BaseException as cleanup_exc:
                    cleanup_errors.append(cleanup_exc)
            if cleanup_errors:
                try:
                    journal.close_after_failed_cleanup()
                except BaseException as cleanup_exc:
                    cleanup_errors.append(cleanup_exc)
                cleanup_types = ",".join(
                    type(item).__name__ for item in cleanup_errors
                )
                raise CaptureRefusal(
                    "Capture failed and exact cleanup did not complete: "
                    f"primary={type(exc).__name__}; cleanup={cleanup_types}."
                ) from cleanup_errors[0]
            raise

    def _select_run(
        self,
        session_id: str,
        pins: Mapping[str, str],
        *,
        proposed_run_id: str | None = None,
    ) -> CaptureRunState:
        if session_id not in SESSION_IDS:
            raise CaptureRefusal("Session is not one of the seven fixed sessions.")
        self.staging.inspect_prefix()
        active: list[CaptureRunState] = []
        try:
            prefix_fd = _open_secure_directory(self.staging.prefix)
        except FileNotFoundError:
            names: list[str] = []
        else:
            try:
                names = sorted(os.listdir(prefix_fd))
            finally:
                os.close(prefix_fd)
        for name in names:
            if name == CAPTURE_COORDINATOR_STAGING_LOCK_NAME:
                continue
            state = self._load_run(name, pins)
            if state.session_count < len(SESSION_IDS):
                active.append(state)
        requested_ordinal = SESSION_IDS.index(session_id)
        if len(active) > 1:
            raise CaptureRefusal("More than one incomplete capture run exists.")
        if requested_ordinal == 0:
            if active:
                if len(active) == 1 and active[0].session_count == 0:
                    if proposed_run_id is not None and active[0].run_id != proposed_run_id:
                        raise CaptureRefusal("Stored zero-session run differs from the planned run.")
                    return active[0]
                raise CaptureRefusal("An incomplete capture run already owns the next session.")
            run_id = proposed_run_id or self._run_id_factory(self.boundary.clock())
            if not RUN_ID_RE.fullmatch(run_id):
                raise CaptureRefusal("Internally generated run ID is malformed.")
            return CaptureRunState(
                run_id,
                self.staging.prefix / run_id,
                0,
                ZERO_SHA256,
                None,
                None,
                frozenset(),
                frozenset(),
            )
        matches = [
            item for item in active if item.session_count == requested_ordinal
        ]
        if len(matches) != 1:
            raise CaptureRefusal("There is not one exact active run for this next session.")
        return matches[0]

    @staticmethod
    def _generate_run_id(sample: ClockSample) -> str:
        timestamp = parse_utc(sample.at_utc).strftime("%Y%m%dt%H%M%Sz").lower()
        return f"id65v3-{timestamp}-{secrets.token_hex(8)}"

    def verify_staging(self, run_id: str) -> None:
        self.preflight()
        with self._lease_factory():
            self.preflight()
            pins = self.source.validate_all()
            state = self._load_run(run_id, pins)
            if state.session_count != len(SESSION_IDS) or not state.run_path.exists():
                raise CaptureRefusal(
                    "Capture staging is not one complete exact seven-session run."
                )

    def _capture_required_markers(
        self,
        session_id: str,
        process: ProcessObservation,
        window: WindowObservation,
        session_path: pathlib.Path,
        run_id: str,
        ordinal: int,
        journal: JournalWriter,
        raw_artifacts: list[dict[str, Any]],
        watcher_child: OwnedChild,
        recorder_child: OwnedChild,
        watcher_reader: IncrementalWatcherReader,
        safe_watcher_ordinal: int,
        recording_deadline_monotonic_microseconds: int,
    ) -> None:
        if session_id == "candidate-primary-edge-0":
            hold_start, safe_watcher_ordinal = self._confirm_safe_growth(
                process,
                window,
                watcher_child,
                recorder_child,
                watcher_reader,
                safe_watcher_ordinal,
                "TASK 2/6 — 20-SECOND HANDS-OFF STABILITY. READY — do not "
                "start yet. Put the controller down, then press Return to start; "
                "provide no input until the coordinator says MINIMUM MET.",
                "hands-off hold start",
                recording_deadline_monotonic_microseconds,
            )
            journal.append("hold-started", {"markerId": "stable-hold"}, hold_start)
            hold_end = self.boundary.wait_for_fresh_return_after(
                hold_start,
                20_500_000,
                "The 20.5-second hands-off minimum is complete. Verify 4 lives, "
                "normal control, and no fall/death/respawn/Game Over, then press "
                "a fresh Return.",
                recording_deadline_monotonic_microseconds,
            )
            _require_minimum_interval(
                hold_start, hold_end, 20_500_000, "Hands-off hold"
            )
            safe_watcher_ordinal = self._require_safe_hold_window(
                process,
                window,
                watcher_child,
                recorder_child,
                watcher_reader,
                hold_start,
                hold_end,
                recording_deadline_monotonic_microseconds,
            )
            journal.append("hold-ended", {"markerId": "stable-hold"}, hold_end)
            _, safe_watcher_ordinal = self._confirm_safe_growth(
                process,
                window,
                watcher_child,
                recorder_child,
                watcher_reader,
                safe_watcher_ordinal,
                "TASK 3/6 — LONE-PAD VISIBILITY. Confirm one small triangular "
                "pad is visible and no inherited terrain or Mobys connect to it; "
                "press Return while stationary for the automatic still frame.",
                "lone-pad visibility",
                recording_deadline_monotonic_microseconds,
            )
            self._capture_frame(
                process,
                window,
                session_path,
                run_id,
                ordinal,
                session_id,
                "lone-pad",
                "candidate.lone-pad-visibility",
                journal,
                raw_artifacts,
                recording_deadline_monotonic_microseconds,
            )

        marker_plan = CHECK_MARKER_PLANS.get(session_id, ())
        if session_id == "candidate-primary-edge-0":
            marker_plan = tuple(
                item for item in marker_plan
                if item[0] not in {
                    "candidate.completed-landing-hud4",
                    "candidate.hands-off-20-seconds",
                }
            )
        retail_expected_lives = 4
        candidate_task_numbers = {
            "candidate.camera-360-twice": 4,
            "candidate.walk-center-perimeter-both-directions": 5,
            "candidate.jump-land-center-each-corner": 6,
        }
        for marker_index, (marker_id, minimum_us) in enumerate(marker_plan, 1):
            instruction = MARKER_INSTRUCTIONS[marker_id]
            if session_id == "candidate-primary-edge-0":
                task_text = f"TASK {candidate_task_numbers[marker_id]}/6"
                start, safe_watcher_ordinal = self._confirm_safe_growth(
                    process,
                    window,
                    watcher_child,
                    recorder_child,
                    watcher_reader,
                    safe_watcher_ordinal,
                    f"{task_text} — {instruction} READY — do not start yet. "
                    f"Press Return to start; continue for at least "
                    f"{minimum_us / 1_000_000:g}s.",
                    marker_id + " start",
                    recording_deadline_monotonic_microseconds,
                )
            else:
                start, _ = self._confirm_gameplay_ready(
                    process,
                    window,
                    watcher_child,
                    recorder_child,
                    watcher_reader,
                    f"TASK {marker_index}/{len(marker_plan)} — {instruction} "
                    "READY — do not start yet. Press Return to start.",
                    expected_lives=retail_expected_lives,
                    recording_deadline_monotonic_microseconds=(
                        recording_deadline_monotonic_microseconds
                    ),
                )
            journal.append("check-started", {"markerId": marker_id}, start)
            end = self.boundary.wait_for_fresh_return_after(
                start,
                minimum_us,
                f"{instruction} Task minimum {minimum_us / 1_000_000:g}s is complete. "
                "Finish the stated action, return to stable controllable gameplay "
                "where applicable, and press a fresh Return.",
                recording_deadline_monotonic_microseconds,
            )
            _require_minimum_interval(start, end, minimum_us, marker_id)
            if session_id == "candidate-primary-edge-0":
                safe_watcher_ordinal = self._require_safe_growth(
                    process,
                    window,
                    watcher_child,
                    recorder_child,
                    watcher_reader,
                    safe_watcher_ordinal,
                    marker_id + " completion",
                    recording_deadline_monotonic_microseconds,
                )
            else:
                expected_end_lives = (
                    3 if marker_id == "retail.town-square.death-respawn"
                    else retail_expected_lives
                )
                samples, selected = self._first_watcher_sample_after(
                    end,
                    process,
                    window,
                    watcher_child,
                    recorder_child,
                    watcher_reader,
                    recording_deadline_monotonic_microseconds,
                )
                self._require_gameplay_suffix(
                    samples, selected, expected_lives=expected_end_lives
                )
                retail_expected_lives = expected_end_lives
            journal.append("check-ended", {"markerId": marker_id}, end)

        if session_id in {
            "candidate-primary-edge-0",
            "candidate-edge-1",
            "candidate-edge-2",
        }:
            assignment = EDGE_ASSIGNMENTS.get(session_id)
            if assignment is None:
                raise CaptureRefusal("Edge session lacks one closed physical-edge assignment.")
            movement_note = (
                "Turn around from body-facing C. "
                if assignment.edge_id == "E0"
                else ""
            )
            _, safe_watcher_ordinal = self._confirm_safe_growth(
                process,
                window,
                watcher_child,
                recorder_child,
                watcher_reader,
                safe_watcher_ordinal,
                "Restore/face the already-confirmed BODY yaw 0x40 reference toward visible "
                "apex C (+Y / 90 degrees); if it cannot be restored, stop, do not "
                "remap, and do not continue. This is a body-facing/top-down reference, "
                "not a camera-view direction. E1 right-hand/east and E2 left-hand/west "
                "are relative to the body facing C regardless of camera. "
                f"{movement_note}"
                f"Move to the stable inside lip of {assignment.edge_id}={assignment.segment} "
                f"({assignment.description}, {assignment.direction}) near midpoint "
                f"{assignment.midpoint}. Press Return when stationary; the coordinator "
                "will then capture the pre-fall frame.",
                "edge pre-fall positioning",
                recording_deadline_monotonic_microseconds,
            )
            self._capture_frame(
                process, window, session_path, run_id, ordinal, session_id,
                "edge-before", self._edge_frame_role(session_id, before=True),
                journal, raw_artifacts, recording_deadline_monotonic_microseconds
            )
            safe_watcher_ordinal = self._require_safe_growth(
                process,
                window,
                watcher_child,
                recorder_child,
                watcher_reader,
                safe_watcher_ordinal,
                "edge fall readiness",
                recording_deadline_monotonic_microseconds,
            )
            self._require_live_gate_boundary(
                process, window, watcher_child, recorder_child
            )
            fall = self.boundary.confirm_before_deadline(
                f"Two-operator fall authorization for {assignment.edge_id}={assignment.segment} "
                f"({assignment.description}, {assignment.direction}) near midpoint "
                f"{assignment.midpoint}, after the pre-fall frame. CONTROLLER OPERATOR: "
                "remain stationary until the external cue, then begin one committed "
                "outward departure. OBSERVING TERMINAL OPERATOR: press Return exactly "
                "once to record the authorization cue; the controller departure must "
                "then begin and reach the death transition within 30 seconds.",
                recording_deadline_monotonic_microseconds,
            )
            self._require_live_gate_boundary(
                process, window, watcher_child, recorder_child
            )
            journal.append("fall-started", {"markerId": "edge-fall"}, fall)
            respawn = self.boundary.wait_for_fresh_return_after(
                fall,
                5_500_000,
                "The 5.5-second fall/respawn minimum is complete. After one death, "
                "wait for stable controllable same-pad respawn with exactly 3 lives, "
                "then press a fresh Return.",
                recording_deadline_monotonic_microseconds,
            )
            _require_minimum_interval(
                fall, respawn, 5_500_000, "Fall-to-respawn interval"
            )
            samples, selected = self._first_watcher_sample_after(
                respawn,
                process,
                window,
                watcher_child,
                recorder_child,
                watcher_reader,
                recording_deadline_monotonic_microseconds,
            )
            fall_marker_time = parse_utc(fall.at_utc)
            fall_sample = next(
                (
                    item for item in samples
                    if item["ordinal"] > 0
                    and parse_utc(item["atUtc"]) >= fall_marker_time
                    and _fall_binding_is_accepted(
                        samples,
                        fall_marker_time,
                        item["ordinal"],
                    )
                ),
                None,
            )
            if fall_sample is None or not _respawn_binding_is_accepted(
                samples,
                fall_sample["ordinal"],
                parse_utc(respawn.at_utc),
                selected["ordinal"],
            ):
                raise CaptureRefusal(
                    "Watcher does not show exact continuous recovery before the respawn marker."
                )
            journal.append("respawn-marked", {"markerId": "edge-respawn"}, respawn)
            self._capture_frame(
                process, window, session_path, run_id, ordinal, session_id,
                "edge-after", self._edge_frame_role(session_id, before=False), journal,
                raw_artifacts, recording_deadline_monotonic_microseconds
            )
        elif session_id == "candidate-cold-reset":
            safe_watcher_ordinal = self._require_safe_growth(
                process,
                window,
                watcher_child,
                recorder_child,
                watcher_reader,
                safe_watcher_ordinal,
                "cold-reset post-landing frame",
                recording_deadline_monotonic_microseconds,
            )
            self._capture_frame(
                process, window, session_path, run_id, ordinal, session_id,
                "cold-reset-after", "candidate.cold-reset-same-pad-yaw.post-reset-frame", journal,
                raw_artifacts, recording_deadline_monotonic_microseconds
            )

    def _capture_frame(
        self,
        process: ProcessObservation,
        window: WindowObservation,
        session_path: pathlib.Path,
        run_id: str,
        ordinal: int,
        session_id: str,
        leaf_stem: str,
        intended_check_id: str,
        journal: JournalWriter,
        raw_artifacts: list[dict[str, Any]],
        recording_deadline_monotonic_microseconds: int,
    ) -> None:
        before_capture = self.boundary.clock()
        if (
            before_capture.monotonic_microseconds
            >= recording_deadline_monotonic_microseconds
        ):
            raise CaptureRefusal(
                "Direct-window recording reached its 30-minute deadline before "
                "frame capture."
            )
        self.boundary.require_capture_environment()
        self._revalidate_process(process)
        if self.boundary.observe_window(process.pid) != window:
            raise CaptureRefusal("DuckStation window changed before frame capture.")
        filename = leaf_stem + ".png"
        if filename not in CAPTURE_FRAME_LEAVES:
            raise CaptureRefusal("Frame capture requested an unapproved destination leaf.")
        capture_directory_identity = _private_directory_identity(session_path)
        self.boundary.capture_frame(
            window,
            session_path,
            filename,
            recording_deadline_monotonic_microseconds,
        )
        captured = self.boundary.clock()
        if captured.monotonic_microseconds > recording_deadline_monotonic_microseconds:
            raise CaptureRefusal(
                "Direct-window recording reached its 30-minute deadline during "
                "frame capture."
            )
        identity = read_regular_file(
            session_path / filename,
            include_bytes=True,
            expected_owner=os.getuid(),
            expected_mode=0o600,
            maximum_bytes=MAX_TEXT_BYTES,
            expected_parent_identity=capture_directory_identity,
        )
        _validate_png_bytes(identity.bytes_value or b"")
        self._revalidate_process(process)
        if self.boundary.observe_window(process.pid) != window:
            raise CaptureRefusal("DuckStation window changed after frame capture.")
        meta = _artifact_metadata(
            f"{session_id}.{leaf_stem}",
            "frame",
            self._session_relative(
                run_id, ordinal, session_id, filename
            ),
            captured.at_utc,
            identity,
        )
        meta.update(
            {
                "backend": "macos-screencapture-window-v1",
                "boundsHeight": window.height,
                "boundsWidth": window.width,
                "boundsX": window.x,
                "boundsY": window.y,
                "captureRole": intended_check_id,
                "ownerPid": process.pid,
                "ownerStartMicroseconds": process.start_microseconds,
                "ownerStartSeconds": process.start_seconds,
                "windowId": window.window_id,
                "windowOwnerPid": window.owner_pid,
            }
        )
        raw_artifacts.append(meta)
        journal.append(
            "frame-captured",
            {"artifactId": meta["artifactId"], "captureRole": intended_check_id},
            captured,
        )

    @staticmethod
    def _edge_frame_role(session_id: str, *, before: bool) -> str:
        check_id = {
            "candidate-primary-edge-0": "candidate.primary-edge-4-to-3-same-pad-yaw",
            "candidate-edge-1": "candidate.edge-1-same-pad-yaw",
            "candidate-edge-2": "candidate.edge-2-same-pad-yaw",
        }.get(session_id)
        if check_id is None:
            raise CaptureRefusal("Edge frame requested for a non-edge session.")
        if before:
            return check_id + ".pre-fall-frame"
        if session_id == "candidate-edge-2":
            return check_id + ".post-respawn-and-cold-reset-pre-frame"
        return check_id + ".post-respawn-frame"

    def _validate_process(
        self, process: ProcessObservation
    ) -> tuple[FileIdentity, FileIdentity]:
        expected_executable = os.path.realpath(self.source.executable_path)
        expected_cue = os.path.realpath(self.source.cue_path)
        expected_bin = os.path.realpath(self.source.bin_path)
        expected_home = os.path.realpath(self.source.external_home_root)
        cue, bin_file = self.source.candidate_identities()
        if (
            process.executable.real_path != expected_executable
            or process.executable.sha256 != EXPECTED_DUCKSTATION_SHA256
            or process.pid <= 1
            or process.start_seconds <= 0
            or not 0 <= process.start_microseconds < 1_000_000
            or process.process_started_at_utc
            != process_start_utc(process.start_seconds, process.start_microseconds)
            or process.argv != (expected_executable, expected_cue)
            or process.argv_nul_sha256 != sha256_bytes(
                b"\0".join(arg.encode("utf-8", "strict") for arg in process.argv)
                + b"\0"
            )
            or process.environment_nul_sha256
            != _runtime_environment_nul_sha256(expected_home)
            or process.external_home_real_path != expected_home
        ):
            raise CaptureRefusal(
                "Observed process is not the exact pinned external-HOME DuckStation."
            )
        if (
            cue.real_path != expected_cue
            or cue.sha256 != EXPECTED_CANDIDATE_CUE_SHA256
            or bin_file.real_path != expected_bin
            or bin_file.sha256 != EXPECTED_CANDIDATE_BIN_SHA256
        ):
            raise CaptureRefusal("Current candidate CUE/BIN source identity changed.")
        disc_args = [arg for arg in process.argv if arg.lower().endswith((".cue", ".bin"))]
        if disc_args != [expected_cue]:
            raise CaptureRefusal("DuckStation argv contains an unapproved disc path.")
        self._validate_open_disc_files(process, expected_cue, bin_file)
        return cue, bin_file

    def _validate_open_disc_files(
        self,
        process: ProcessObservation,
        expected_cue: str,
        bin_file: FileIdentity,
    ) -> None:
        self._require_open_identity(process, bin_file)
        if expected_cue in process.open_paths:
            raise CaptureRefusal("DuckStation still has the parsed CUE open instead of the stable BIN-only lifecycle.")
        allowed_cache_path = self._validate_process_cache_bin(process)
        for opened in process.open_files:
            if not opened.real_path.lower().endswith((".cue", ".bin")):
                continue
            if opened.real_path == bin_file.real_path:
                continue
            if allowed_cache_path is not None and opened.real_path == allowed_cache_path:
                continue
            raise CaptureRefusal("DuckStation has another CUE or BIN open.")

    def _validate_process_cache_bin(
        self, process: ProcessObservation
    ) -> str | None:
        expected_path = pathlib.Path(os.path.abspath(
            self.source.profile_root / ALLOWED_PROCESS_CACHE_BIN_RELATIVE_PATH
        ))
        expected_real_path = os.path.realpath(expected_path)
        matches = [
            item for item in process.open_files
            if item.real_path == expected_real_path
        ]
        if len(matches) > 1:
            raise CaptureRefusal(
                "DuckStation has duplicate allowed shader-cache descriptors."
            )
        if not matches:
            return None
        _require_no_symlink_segments(expected_path)
        opened = matches[0]
        if re.fullmatch(r"[0-9]+u", opened.fd) is None or opened.link_count != 1:
            raise CaptureRefusal(
                "DuckStation shader-cache descriptor is not one update-mode single-link file."
            )
        identity = read_regular_file(
            expected_path,
            expected_owner=os.getuid(),
            expected_mode=0o644,
            maximum_bytes=MAX_PROCESS_CACHE_BIN_BYTES,
        )
        if (
            identity.real_path != expected_real_path
            or opened.device != identity.device
            or opened.inode != identity.inode
            or opened.byte_length != identity.byte_length
        ):
            raise CaptureRefusal(
                "DuckStation shader-cache descriptor differs from its stable current file."
            )
        return expected_real_path

    def _revalidate_process(
        self, expected: ProcessObservation
    ) -> tuple[ProcessObservation, FileIdentity, FileIdentity]:
        current = self.boundary.duckstation_processes()
        if len(current) != 1:
            raise CaptureRefusal("The one exact DuckStation process is no longer exclusive.")
        observed = current[0]
        cue, bin_file = self._validate_process(observed)
        if (
            observed.pid != expected.pid
            or observed.start_seconds != expected.start_seconds
            or observed.start_microseconds != expected.start_microseconds
            or observed.executable.real_path != expected.executable.real_path
            or observed.executable.device != expected.executable.device
            or observed.executable.inode != expected.executable.inode
            or observed.executable.sha256 != expected.executable.sha256
            or observed.argv != expected.argv
            or observed.argv_nul_sha256 != expected.argv_nul_sha256
            or observed.environment_nul_sha256 != expected.environment_nul_sha256
            or observed.external_home_real_path != expected.external_home_real_path
        ):
            raise CaptureRefusal("DuckStation process fingerprint changed during capture.")
        if self._require_open_identity(observed, bin_file) != self._require_open_identity(
            expected, bin_file
        ):
            raise CaptureRefusal(
                "DuckStation BIN file descriptor identity changed during capture."
            )
        if self.boundary.listener_pids() != {expected.pid}:
            raise CaptureRefusal("Port 2345 listener is not owned by the exact DuckStation PID.")
        return observed, cue, bin_file

    @staticmethod
    def _require_open_identity(
        process: ProcessObservation, identity: FileIdentity
    ) -> OpenFileObservation:
        matches = [
            item for item in process.open_files
            if item.real_path == identity.real_path
        ]
        if len(matches) != 1:
            raise CaptureRefusal("Process does not hold one exact pinned disc file descriptor.")
        opened = matches[0]
        if (
            opened.device != identity.device
            or opened.inode != identity.inode
            or opened.byte_length != identity.byte_length
            or opened.link_count != 1
            or re.fullmatch(r"[0-9]+r", opened.fd) is None
        ):
            raise CaptureRefusal(
                "Process-open disc inode or read-only descriptor differs from current pinned bytes."
            )
        return opened

    def _confirm_process(
        self, process: ProcessObservation, prompt: str
    ) -> ClockSample:
        self._revalidate_process(process)
        sample = self.boundary.confirm(prompt)
        self._revalidate_process(process)
        return sample

    def _require_live_gate_boundary(
        self,
        process: ProcessObservation,
        window: WindowObservation,
        watcher_child: OwnedChild,
        recorder_child: OwnedChild,
    ) -> None:
        self._revalidate_process(process)
        if self.boundary.observe_window(process.pid) != window:
            raise CaptureRefusal("DuckStation window changed during a live gameplay gate.")
        if watcher_child.poll() is not None:
            raise CaptureRefusal("Pinned watcher exited during a live gameplay gate.")
        if recorder_child.poll() is not None:
            raise CaptureRefusal("Owned screen recorder exited during a live gameplay gate.")

    def _live_watcher_snapshot(
        self,
        process: ProcessObservation,
        window: WindowObservation,
        watcher_child: OwnedChild,
        recorder_child: OwnedChild,
        reader: IncrementalWatcherReader,
        recording_deadline_monotonic_microseconds: int,
    ) -> list[dict[str, Any]]:
        self._require_live_gate_boundary(
            process, window, watcher_child, recorder_child
        )
        samples = reader.snapshot(
            recording_deadline_monotonic_microseconds,
            lambda: self.boundary.clock().monotonic_microseconds,
        )
        self._require_live_gate_boundary(
            process, window, watcher_child, recorder_child
        )
        return samples

    def _first_watcher_sample_after(
        self,
        confirmed: ClockSample,
        process: ProcessObservation,
        window: WindowObservation,
        watcher_child: OwnedChild,
        recorder_child: OwnedChild,
        reader: IncrementalWatcherReader,
        recording_deadline_monotonic_microseconds: int,
    ) -> tuple[list[dict[str, Any]], dict[str, Any]]:
        local_deadline = time.monotonic() + 0.35
        while True:
            now = self.boundary.clock()
            if now.monotonic_microseconds >= recording_deadline_monotonic_microseconds:
                raise CaptureRefusal(
                    "Direct-window recording reached its 30-minute deadline while "
                    "waiting for a fresh watcher sample."
                )
            samples = self._live_watcher_snapshot(
                process,
                window,
                watcher_child,
                recorder_child,
                reader,
                recording_deadline_monotonic_microseconds,
            )
            selected = _select_first_watcher_sample_after(samples, confirmed)
            if selected is not None:
                return samples, selected
            remaining_seconds = (
                recording_deadline_monotonic_microseconds
                - now.monotonic_microseconds
            ) / 1_000_000
            if time.monotonic() >= local_deadline:
                raise CaptureRefusal(
                    "Watcher has no fresh post-confirmation sample within 250ms."
                )
            time.sleep(min(0.025, max(0.0, remaining_seconds)))

    @staticmethod
    def _require_gameplay_suffix(
        samples: Sequence[dict[str, Any]],
        selected: Mapping[str, Any],
        *,
        expected_lives: int = 4,
        strictly_after_ordinal: int = -1,
    ) -> int:
        selected_ordinal = int(selected["ordinal"])
        selected_elapsed = int(selected["elapsedMilliseconds"])
        suffix_start_ordinal = selected_ordinal
        while (
            suffix_start_ordinal > 0
            and selected_elapsed
            - int(samples[suffix_start_ordinal]["elapsedMilliseconds"])
            < ASSISTED_READINESS_MINIMUM_MILLISECONDS
        ):
            suffix_start_ordinal -= 1
        suffix = list(samples[suffix_start_ordinal : selected_ordinal + 1])
        if (
            len(suffix) < ASSISTED_READINESS_MINIMUM_SAMPLES
            or not suffix
            or int(suffix[-1]["elapsedMilliseconds"])
            - int(suffix[0]["elapsedMilliseconds"])
            < ASSISTED_READINESS_MINIMUM_MILLISECONDS
            or any(
                item["lives"] != expected_lives
                or item["state"] != 0
                or item["playerZ"] < 0x400
                for item in suffix
            )
        ):
            raise CaptureRefusal(
                "Watcher lacks a 2.000-second, 19-sample safe gameplay suffix."
            )
        if suffix_start_ordinal <= strictly_after_ordinal:
            raise CaptureRefusal(
                "Watcher readiness suffix does not begin after recording startup."
            )
        return suffix_start_ordinal

    def _wait_for_gameplay_ready(
        self,
        process: ProcessObservation,
        window: WindowObservation,
        watcher_child: OwnedChild,
        recorder_child: OwnedChild,
        reader: IncrementalWatcherReader,
        prompt: str,
        *,
        not_before_ordinal: int = -1,
        expected_lives: int = 4,
        recording_deadline_monotonic_microseconds: int,
    ) -> tuple[ClockSample, int]:
        self.boundary.announce(prompt)
        while True:
            now = self.boundary.clock()
            if now.monotonic_microseconds >= recording_deadline_monotonic_microseconds:
                raise CaptureRefusal(
                    "Direct-window recording reached its 30-minute deadline while "
                    "waiting for automatic gameplay readiness."
                )
            samples = self._live_watcher_snapshot(
                process,
                window,
                watcher_child,
                recorder_child,
                reader,
                recording_deadline_monotonic_microseconds,
            )
            if samples:
                selected = samples[-1]
                if int(selected["ordinal"]) <= not_before_ordinal:
                    remaining_seconds = (
                        recording_deadline_monotonic_microseconds
                        - now.monotonic_microseconds
                    ) / 1_000_000
                    time.sleep(min(0.025, max(0.0, remaining_seconds)))
                    continue
                try:
                    self._require_gameplay_suffix(
                        samples,
                        selected,
                        expected_lives=expected_lives,
                        strictly_after_ordinal=not_before_ordinal,
                    )
                except CaptureRefusal:
                    pass
                else:
                    self._require_live_gate_boundary(
                        process, window, watcher_child, recorder_child
                    )
                    accepted = self.boundary.clock()
                    if (
                        accepted.monotonic_microseconds
                        >= recording_deadline_monotonic_microseconds
                    ):
                        raise CaptureRefusal(
                            "Direct-window recording reached its 30-minute deadline "
                            "at automatic gameplay readiness."
                        )
                    return accepted, int(selected["ordinal"])
            remaining_seconds = (
                recording_deadline_monotonic_microseconds
                - now.monotonic_microseconds
            ) / 1_000_000
            time.sleep(min(0.025, max(0.0, remaining_seconds)))

    def _wait_for_candidate_primary_landing(
        self,
        process: ProcessObservation,
        window: WindowObservation,
        watcher_child: OwnedChild,
        recorder_child: OwnedChild,
        reader: IncrementalWatcherReader,
        selector: ClockSample,
        journal: JournalWriter,
        recording_deadline_monotonic_microseconds: int,
    ) -> tuple[ClockSample, int]:
        check_start: ClockSample | None = None
        baseline_ordinal: int | None = None
        selector_at = parse_utc(selector.at_utc)
        armed_at = selector_at
        while True:
            now = self.boundary.clock()
            if now.monotonic_microseconds >= recording_deadline_monotonic_microseconds:
                raise CaptureRefusal(
                    "Direct-window recording reached its 30-minute deadline while "
                    "waiting for the armed candidate landing."
                )
            samples = self._live_watcher_snapshot(
                process,
                window,
                watcher_child,
                recorder_child,
                reader,
                recording_deadline_monotonic_microseconds,
            )
            if baseline_ordinal is None:
                if not any(
                    parse_utc(str(item["atUtc"])) <= selector_at
                    for item in samples
                ):
                    remaining_seconds = (
                        recording_deadline_monotonic_microseconds
                        - now.monotonic_microseconds
                    ) / 1_000_000
                    time.sleep(min(0.025, max(0.0, remaining_seconds)))
                    continue
                baseline_ordinal = _assisted_armed_baseline_ordinal(
                    samples, selector_at
                )
            departure, _landing, safe_endpoint = (
                _candidate_primary_landing_transition(
                    samples,
                    baseline_ordinal,
                    armed_at,
                )
            )
            if departure is not None and check_start is None:
                check_start = _watcher_sample_clock(departure, selector)
                journal.append(
                    "check-started",
                    {"markerId": "candidate.completed-landing-hud4"},
                    check_start,
                )
                self.boundary.announce(
                    "TASK 1/6 AUTO-MARKED — exact V3 fly-in departure detected. "
                    "Keep the controller untouched while landing telemetry settles."
                )
            if safe_endpoint is not None:
                if check_start is None:
                    raise CaptureRefusal(
                        "Automated landing completed without its departure marker."
                    )
                self._require_live_gate_boundary(
                    process, window, watcher_child, recorder_child
                )
                return check_start, int(safe_endpoint["ordinal"])
            remaining_seconds = (
                recording_deadline_monotonic_microseconds
                - now.monotonic_microseconds
            ) / 1_000_000
            time.sleep(min(0.025, max(0.0, remaining_seconds)))

    @staticmethod
    def _latest_tuple_text(samples: Sequence[Mapping[str, Any]]) -> str:
        if not samples:
            return "no complete watcher sample"
        latest = samples[-1]
        return (
            f"lives={latest['lives']}, death-state={latest['state']}, "
            f"player-Z={latest['playerZ']}, sample={latest['ordinal']}"
        )

    def _confirm_gameplay_ready(
        self,
        process: ProcessObservation,
        window: WindowObservation,
        watcher_child: OwnedChild,
        recorder_child: OwnedChild,
        reader: IncrementalWatcherReader,
        prompt: str,
        *,
        expected_lives: int = 4,
        attempts: int = 3,
        require_fresh_input: bool = False,
        recording_deadline_monotonic_microseconds: int,
    ) -> tuple[ClockSample, int]:
        latest_text = "not sampled yet"
        for attempt in range(1, attempts + 1):
            self._require_live_gate_boundary(
                process, window, watcher_child, recorder_child
            )
            confirm_action = (
                self.boundary.fresh_confirm_before_deadline
                if require_fresh_input
                else self.boundary.confirm_before_deadline
            )
            confirmed = confirm_action(
                f"GAMEPLAY READINESS {attempt}/{attempts}. {prompt} "
                f"Latest result: {latest_text}.",
                recording_deadline_monotonic_microseconds,
            )
            self._require_live_gate_boundary(
                process, window, watcher_child, recorder_child
            )
            samples, selected = self._first_watcher_sample_after(
                confirmed,
                process,
                window,
                watcher_child,
                recorder_child,
                reader,
                recording_deadline_monotonic_microseconds,
            )
            try:
                self._require_gameplay_suffix(
                    samples, selected, expected_lives=expected_lives
                )
            except CaptureRefusal:
                latest_text = self._latest_tuple_text(samples)
                if attempt == attempts:
                    raise CaptureRefusal(
                        "Gameplay readiness failed three operator confirmations; "
                        f"latest tuple: {latest_text}."
                    ) from None
                continue
            self._require_live_gate_boundary(
                process, window, watcher_child, recorder_child
            )
            return confirmed, int(selected["ordinal"])
        raise AssertionError("unreachable gameplay readiness attempt loop")

    def _require_safe_growth(
        self,
        process: ProcessObservation,
        window: WindowObservation,
        watcher_child: OwnedChild,
        recorder_child: OwnedChild,
        reader: IncrementalWatcherReader,
        after_ordinal: int,
        label: str,
        recording_deadline_monotonic_microseconds: int,
    ) -> int:
        samples = self._live_watcher_snapshot(
            process,
            window,
            watcher_child,
            recorder_child,
            reader,
            recording_deadline_monotonic_microseconds,
        )
        growth = [item for item in samples if item["ordinal"] > after_ordinal]
        if any(not _watcher_sample_safe(item) for item in growth):
            raise CaptureRefusal(
                f"Watcher observed unsafe gameplay after accepted landing during {label}."
            )
        return int(samples[-1]["ordinal"]) if samples else after_ordinal

    def _confirm_safe_growth(
        self,
        process: ProcessObservation,
        window: WindowObservation,
        watcher_child: OwnedChild,
        recorder_child: OwnedChild,
        reader: IncrementalWatcherReader,
        after_ordinal: int,
        prompt: str,
        label: str,
        recording_deadline_monotonic_microseconds: int,
    ) -> tuple[ClockSample, int]:
        after_ordinal = self._require_safe_growth(
            process,
            window,
            watcher_child,
            recorder_child,
            reader,
            after_ordinal,
            label,
            recording_deadline_monotonic_microseconds,
        )
        self._require_live_gate_boundary(
            process, window, watcher_child, recorder_child
        )
        confirmed = self.boundary.confirm_before_deadline(
            prompt, recording_deadline_monotonic_microseconds
        )
        self._require_live_gate_boundary(
            process, window, watcher_child, recorder_child
        )
        samples, selected = self._first_watcher_sample_after(
            confirmed,
            process,
            window,
            watcher_child,
            recorder_child,
            reader,
            recording_deadline_monotonic_microseconds,
        )
        if any(
            not _watcher_sample_safe(item)
            for item in samples
            if after_ordinal < item["ordinal"] <= selected["ordinal"]
        ):
            raise CaptureRefusal(
                f"Watcher observed unsafe gameplay after accepted landing during {label}."
            )
        self._require_live_gate_boundary(
            process, window, watcher_child, recorder_child
        )
        return confirmed, int(selected["ordinal"])

    def _require_safe_hold_window(
        self,
        process: ProcessObservation,
        window: WindowObservation,
        watcher_child: OwnedChild,
        recorder_child: OwnedChild,
        reader: IncrementalWatcherReader,
        started: ClockSample,
        ended: ClockSample,
        recording_deadline_monotonic_microseconds: int,
    ) -> int:
        samples = self._live_watcher_snapshot(
            process,
            window,
            watcher_child,
            recorder_child,
            reader,
            recording_deadline_monotonic_microseconds,
        )
        start_utc = parse_utc(started.at_utc)
        end_utc = parse_utc(ended.at_utc)
        window_samples = [
            item for item in samples
            if start_utc <= parse_utc(item["atUtc"]) <= end_utc
        ]
        _require_safe_hold_samples(window_samples)
        return int(window_samples[-1]["ordinal"])

    @staticmethod
    def _validate_profile_transition(before: dict[str, Any], after: dict[str, Any]) -> None:
        before_files = {item["relativePath"]: item for item in before["dataFiles"]}
        after_files = {item["relativePath"]: item for item in after["dataFiles"]}
        for path in sorted(before_files.keys() | after_files.keys()):
            if before_files.get(path) == after_files.get(path):
                continue
            if path != "playtime.dat" and path.split("/", 1)[0] not in {
                "cache", "screenshots", "videos"
            }:
                raise CaptureRefusal(f"Nonvolatile profile file changed: {path}")
        for field in (
            "format", "formatVersion",
            "duckStationExecutableSha256",
            "handoffReceiptSha256",
            "appContentManifestSha256",
            "appOutputTreeManifestSha256",
            "packageEnvelopeSha256",
            "externalHomeSettingsSha256",
            "externalHomeBiosSha256",
            "hostNormalSettingsSentinelId",
            "hostNormalSettingsSentinelSha256",
            "profileId",
            "processInstanceId",
            "sessionId",
        ):
            if before[field] != after[field]:
                raise CaptureRefusal(f"Profile bookend field changed: {field}")

    def _verify_raw_metadata(
        self, session_path: pathlib.Path, metadata: Sequence[dict[str, Any]]
    ) -> None:
        seen_ids: set[str] = set()
        seen_paths: set[str] = set()
        for item in metadata:
            artifact_id = item.get("artifactId")
            relative = item.get("repoRelativePath")
            if (
                not isinstance(artifact_id, str)
                or artifact_id in seen_ids
                or not isinstance(relative, str)
                or relative in seen_paths
            ):
                raise CaptureRefusal("Raw artifact identity is missing or reused.")
            seen_ids.add(artifact_id)
            seen_paths.add(relative)
            expected_prefix = session_path.relative_to(
                self.boundary.repository_root
            ).as_posix() + "/"
            if not relative.startswith(expected_prefix):
                raise CaptureRefusal("Raw artifact escaped its fixed session directory.")
            path = self.boundary.repository_root / relative
            kind = item.get("kind")
            if kind == "screen-recording":
                identity = read_regular_file(
                    path,
                    expected_owner=os.getuid(),
                    expected_mode=0o600,
                    maximum_bytes=MAX_MEDIA_BYTES,
                    validator=_validate_iso_bmff_fd,
                )
                data = None
            else:
                identity = read_regular_file(
                    path,
                    include_bytes=True,
                    expected_owner=os.getuid(),
                    expected_mode=0o600,
                    maximum_bytes=MAX_TEXT_BYTES,
                )
                data = identity.bytes_value or b""
            if (
                identity.sha256 != item.get("sha256")
                or identity.byte_length != item.get("byteLength")
            ):
                raise CaptureRefusal("Raw artifact bytes differ from captured metadata.")
            if kind in {"runtime-profile-before", "runtime-profile-after"}:
                profile = _decode_verifier_canonical_json_bytes(
                    data or b"", f"profile {artifact_id}"
                )
                if (
                    profile.get("snapshotId") != artifact_id
                    or profile.get("capturedAtUtc") != item.get("capturedAtUtc")
                    or profile.get("profileId") != PROFILE_ID
                ):
                    raise CaptureRefusal("Profile artifact does not match its raw metadata.")
            elif kind == "watcher-stdout":
                self._parse_watcher_samples(data or b"")
            elif kind == "frame":
                _validate_png_bytes(data or b"")
            elif kind == "screen-recording":
                pass
            else:
                raise CaptureRefusal("Raw artifact has an unapproved kind.")

    @staticmethod
    def _append_watcher_marker_bindings(
        journal: JournalWriter,
        session_id: str,
        samples: Sequence[dict[str, Any]],
    ) -> None:
        wanted = ["landing-marked"]
        if session_id == "candidate-primary-edge-0":
            wanted.extend(["hold-started", "hold-ended"])
        if session_id in {
            "candidate-primary-edge-0",
            "candidate-edge-1",
            "candidate-edge-2",
        }:
            wanted.extend(["fall-started", "respawn-marked"])
        events_by_type = {
            item["eventType"]: item
            for item in journal.events
            if item["eventType"] in wanted
        }
        if set(events_by_type) != set(wanted):
            raise CaptureRefusal("Required watcher-bound marker is missing.")
        selected_by_role: dict[str, dict[str, Any]] = {}
        for event_type in wanted:
            marker = events_by_type[event_type]
            marker_time = parse_utc(marker["atUtc"])
            if event_type == "respawn-marked":
                chosen = next(
                    (
                        item for item in samples
                        if parse_utc(item["atUtc"]) >= marker_time
                    ),
                    None,
                )
                fall_sample = selected_by_role.get("fall-started")
                if chosen is None or fall_sample is None or not (
                    chosen["ordinal"] > fall_sample["ordinal"]
                    and chosen["lives"] == 3
                    and chosen["state"] == 0
                    and chosen["playerZ"] >= 0x400
                ):
                    raise CaptureRefusal(
                        "Watcher's first post-marker respawn sample is not stable recovery."
                    )
                first_recovery = next(
                    (
                        item for item in samples[fall_sample["ordinal"] + 1 :]
                        if item["lives"] == 3
                        and item["state"] == 0
                        and item["playerZ"] >= 0x400
                    ),
                    None,
                )
                if (
                    first_recovery is None
                    or not _respawn_binding_is_accepted(
                        samples,
                        fall_sample["ordinal"],
                        marker_time,
                        chosen["ordinal"],
                    )
                ):
                    raise CaptureRefusal(
                        "Watcher recovery is not continuously stable through clean stop."
                    )
            elif event_type == "fall-started":
                chosen = next(
                    (
                        item for item in samples
                        if item["ordinal"] > 0
                        and parse_utc(item["atUtc"]) >= marker_time
                        and samples[item["ordinal"] - 1]["lives"] == 4
                        and samples[item["ordinal"] - 1]["state"] == 0
                        and samples[item["ordinal"] - 1]["playerZ"] >= 0x400
                        and item["lives"] == 3
                        and item["state"] == 4
                        and item["playerZ"] < 0x400
                    ),
                    None,
                )
                if chosen is None:
                    raise CaptureRefusal(
                        "Watcher has no post-cue safe-to-death fall edge."
                    )
            else:
                directional = [
                    item for item in samples
                    if (
                        parse_utc(item["atUtc"]) <= marker_time
                        if event_type == "hold-ended"
                        else parse_utc(item["atUtc"]) >= marker_time
                    )
                ]
                if not directional:
                    raise CaptureRefusal(f"Watcher has no directional sample for {event_type}.")
                chosen = directional[-1] if event_type == "hold-ended" else directional[0]
            signed_distance_ms = int(
                (parse_utc(chosen["atUtc"]) - marker_time).total_seconds()
                * 1000
            )
            if event_type == "fall-started":
                in_tolerance = (
                    0 <= signed_distance_ms
                    <= MAX_FALL_AUTHORIZATION_BINDING_MILLISECONDS
                )
            else:
                in_tolerance = abs(signed_distance_ms) <= 250
            if not in_tolerance:
                raise CaptureRefusal(
                    f"Watcher has no in-tolerance sample for {event_type}."
                )
            if event_type in {"landing-marked", "hold-started", "hold-ended"} and not (
                chosen["lives"] == 4
                and chosen["state"] == 0
                and chosen["playerZ"] >= 0x400
            ):
                raise CaptureRefusal(f"Watcher semantic tuple is unsafe for {event_type}.")
            if event_type == "fall-started":
                if not _fall_binding_is_accepted(
                    samples,
                    marker_time,
                    chosen["ordinal"],
                ):
                    raise CaptureRefusal(
                        "Fall marker lacks its safe authorization-to-death watcher edge."
                    )
            selected_by_role[event_type] = chosen
            journal.append(
                "watcher-marker-bound",
                {
                    "markerRole": event_type,
                    "operatorEventSequence": marker["sequence"],
                    "sampleAtUtc": chosen["atUtc"],
                    "sampleElapsedMilliseconds": chosen["elapsedMilliseconds"],
                    "sampleOrdinal": chosen["ordinal"],
                },
            )

    @staticmethod
    def _parse_watcher_samples(
        source: pathlib.Path | bytes
    ) -> list[dict[str, Any]]:
        if isinstance(source, bytes):
            data = source
        else:
            identity = read_regular_file(
                source,
                include_bytes=True,
                expected_owner=os.getuid(),
                maximum_bytes=MAX_TEXT_BYTES,
            )
            data = identity.bytes_value or b""
        try:
            text = data.decode("utf-8", "strict")
        except UnicodeDecodeError as exc:
            raise CaptureRefusal("Watcher stdout is not strict UTF-8.") from exc
        if "\r" in text:
            raise CaptureRefusal("Watcher stdout contains a CR line ending.")
        if (
            not text.startswith(WATCHER_LIVE_PREAMBLE)
            or not text.endswith(WATCHER_CLEAN_TRAILER)
        ):
            raise CaptureRefusal("Watcher stdout preamble or clean terminal trailer changed.")
        sample_block = text[
            len(WATCHER_LIVE_PREAMBLE) : -len(WATCHER_CLEAN_TRAILER)
        ]
        if not sample_block or sample_block.endswith("\n"):
            raise CaptureRefusal("Watcher sample block has an invalid boundary.")
        samples = _decode_watcher_sample_lines(sample_block.split("\n"))
        if not samples:
            raise CaptureRefusal("Watcher stdout contains no samples.")
        return samples

    @staticmethod
    def _session_relative(
        run_id: str, ordinal: int, session_id: str, filename: str
    ) -> str:
        return (
            f"{CAPTURE_STAGING_PREFIX}/{run_id}/"
            f"{ordinal:02d}-{session_id}/{filename}"
        )

    @staticmethod
    def _read_canonical_json(path: pathlib.Path) -> dict[str, Any]:
        identity = read_regular_file(
            path,
            include_bytes=True,
            expected_owner=os.getuid(),
            expected_mode=0o600,
            maximum_bytes=MAX_TEXT_BYTES,
        )
        data = identity.bytes_value or b""
        return _decode_canonical_json_bytes(data, f"capture JSON {path}")

    def _validate_origin(self, origin: dict[str, Any], run_id: str) -> None:
        expected_keys = {
            "bootIdentity",
            "captureStagingRepoRelativePath",
            "createdAtUtc",
            "createdMonotonicMicroseconds",
            "format",
            "formatVersion",
            "orderedSessionIds",
            "repositoryRootRealPath",
            "runId",
            "prelaunchProof",
            "prelaunchProofSha256",
        } | set(CAPTURE_PIN_KEYS)
        if set(origin) != expected_keys:
            raise CaptureRefusal("Run origin has an unknown or missing property.")
        if (
            origin["format"] != RUN_FORMAT
            or origin["formatVersion"] != RUN_FORMAT_VERSION
            or origin["runId"] != run_id
            or origin["orderedSessionIds"] != list(SESSION_IDS)
            or origin["captureCoordinatorRelativePath"]
            != CAPTURE_COORDINATOR_RELATIVE_PATH
            or origin["watcherRelativePath"] != WATCHER_RELATIVE_PATH
            or origin["repositoryRootRealPath"]
            != os.path.realpath(self.boundary.repository_root)
            or origin["captureStagingRepoRelativePath"]
            != f"{CAPTURE_STAGING_PREFIX}/{run_id}"
            or not SHA256_RE.fullmatch(origin["bootIdentity"])
        ):
            raise CaptureRefusal("Run origin identity changed.")
        parse_utc(origin["createdAtUtc"])
        _require_int(
            origin["createdMonotonicMicroseconds"],
            "run-origin monotonic clock",
            positive=True,
        )
        proof = _validate_prelaunch_proof(origin["prelaunchProof"])
        if origin["prelaunchProofSha256"] != sha256_bytes(canonical_lf_bytes(proof)):
            raise CaptureRefusal("Run origin prelaunch proof hash changed.")
        for name in CAPTURE_PIN_KEYS:
            value = origin[name]
            if not isinstance(value, str) or not value:
                raise CaptureRefusal(f"Run origin pin is malformed: {name}.")


def _pairs_no_duplicates(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for key, value in pairs:
        if key in result:
            raise CaptureRefusal(f"Duplicate JSON property: {key}")
        result[key] = value
    return result


def _decode_canonical_json_bytes(data: bytes, context: str) -> dict[str, Any]:
    if not data or data.startswith(b"\xef\xbb\xbf"):
        raise CaptureRefusal(f"{context} is empty or has a UTF-8 BOM.")
    try:
        value = json.loads(
            data.decode("utf-8", "strict"), object_pairs_hook=_pairs_no_duplicates
        )
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise CaptureRefusal(f"{context} cannot be decoded.") from exc
    if not isinstance(value, dict) or canonical_bytes(value) != data:
        raise CaptureRefusal(f"{context} is not exact canonical UTF-8 JSON.")
    return value


def _decode_verifier_canonical_json_bytes(
    data: bytes, context: str
) -> dict[str, Any]:
    if not data.endswith(b"\n") or data.endswith(b"\n\n") or b"\r" in data:
        raise CaptureRefusal(f"{context} is not one exact LF-terminated verifier JSON object.")
    if data.startswith(b"\xef\xbb\xbf"):
        raise CaptureRefusal(f"{context} has a UTF-8 BOM.")
    try:
        value = json.loads(
            data[:-1].decode("utf-8", "strict"),
            object_pairs_hook=_pairs_no_duplicates,
        )
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise CaptureRefusal(f"{context} cannot be decoded.") from exc
    if not isinstance(value, dict) or canonical_lf_bytes(value) != data:
        raise CaptureRefusal(f"{context} differs from the frozen verifier serializer.")
    return value


def _validate_png_bytes(data: bytes) -> None:
    if not data.startswith(b"\x89PNG\r\n\x1a\n"):
        raise CaptureRefusal("Captured frame lacks the PNG signature.")
    cursor = 8
    saw_ihdr = False
    saw_idat = False
    saw_iend = False
    chunk_count = 0
    while cursor < len(data):
        chunk_count += 1
        if chunk_count > 100_000 or cursor + 12 > len(data):
            raise CaptureRefusal("Captured frame has an invalid or excessive PNG chunk table.")
        length = int.from_bytes(data[cursor : cursor + 4], "big")
        kind = data[cursor + 4 : cursor + 8]
        end = cursor + 12 + length
        if length > 64 * 1024 * 1024 or end > len(data):
            raise CaptureRefusal("Captured frame has an out-of-bounds PNG chunk.")
        if chunk_count == 1 and (kind != b"IHDR" or length != 13):
            raise CaptureRefusal("Captured frame does not start with a 13-byte PNG IHDR.")
        if kind == b"IHDR" and (saw_ihdr or chunk_count != 1 or length != 13):
            raise CaptureRefusal("Captured frame has a duplicate or malformed PNG IHDR.")
        if kind == b"IEND" and length != 0:
            raise CaptureRefusal("Captured frame has a nonempty PNG IEND.")
        payload = data[cursor + 8 : cursor + 8 + length]
        stored_crc = int.from_bytes(data[cursor + 8 + length : end], "big")
        computed_crc = zlib.crc32(kind + payload) & 0xFFFFFFFF
        if stored_crc != computed_crc:
            raise CaptureRefusal("Captured frame has a PNG chunk CRC mismatch.")
        if kind == b"IHDR":
            saw_ihdr = True
            width = int.from_bytes(payload[0:4], "big")
            height = int.from_bytes(payload[4:8], "big")
            if width == 0 or height == 0:
                raise CaptureRefusal("Captured frame has zero PNG dimensions.")
        elif kind == b"IDAT":
            if not saw_ihdr or length == 0:
                raise CaptureRefusal("Captured frame has an empty or misplaced PNG IDAT.")
            saw_idat = True
        elif kind == b"IEND":
            saw_iend = True
            if end != len(data):
                raise CaptureRefusal("Captured frame has bytes after final PNG IEND.")
        cursor = end
        if saw_iend:
            break
    if not saw_ihdr or not saw_idat or not saw_iend:
        raise CaptureRefusal("Captured frame lacks required PNG IHDR, IDAT, or final IEND.")


def _validate_iso_bmff_fd(fd: int, length: int) -> None:
    if length < 16:
        raise CaptureRefusal("Captured recording is shorter than ISO-BMFF.")
    cursor = 0
    kinds: list[bytes] = []
    recognized = False
    while cursor < length:
        if len(kinds) >= 1_000_000 or cursor + 8 > length:
            raise CaptureRefusal("Captured recording has a truncated ISO-BMFF box.")
        os.lseek(fd, cursor, os.SEEK_SET)
        header_bytes = os.read(fd, 8)
        if len(header_bytes) != 8:
            raise CaptureRefusal("Captured recording box header changed while read.")
        size = int.from_bytes(header_bytes[:4], "big")
        kind = header_bytes[4:8]
        header = 8
        if size == 1:
            extended = os.read(fd, 8)
            if cursor + 16 > length or len(extended) != 8:
                raise CaptureRefusal("Captured recording has a truncated 64-bit box.")
            size = int.from_bytes(extended, "big")
            header = 16
        elif size == 0:
            size = length - cursor
        if size < header or cursor + size > length:
            raise CaptureRefusal("Captured recording has an out-of-bounds ISO-BMFF box.")
        if not kinds:
            payload_length = size - header
            if kind != b"ftyp" or payload_length < 8 or payload_length > 1_048_576 or (payload_length - 8) % 4:
                raise CaptureRefusal("Captured recording does not begin with ftyp.")
            os.lseek(fd, cursor + header, os.SEEK_SET)
            brands = os.read(fd, payload_length)
            approved = {b"isom", b"iso2", b"iso3", b"iso4", b"iso5", b"iso6", b"mp41", b"mp42", b"avc1", b"M4V ", b"qt  "}
            recognized = brands[:4] in approved or any(
                brands[index : index + 4] in approved
                for index in range(8, len(brands), 4)
            )
        elif kind in {b"moov", b"mdat"} and size == header:
            raise CaptureRefusal("Captured recording has an empty moov or mdat box.")
        kinds.append(kind)
        cursor += size
    if not recognized or b"moov" not in kinds or b"mdat" not in kinds:
        raise CaptureRefusal("Captured recording lacks recognized ftyp/moov/mdat boxes.")


@dataclasses.dataclass(frozen=True)
class JournalVerification:
    terminal_hash: str
    first_clock: ClockSample
    terminal_clock: ClockSample
    process_payload: dict[str, Any]
    artifact_metadata: tuple[dict[str, Any], ...]


def _exact_keys(value: Any, keys: set[str], context: str) -> dict[str, Any]:
    if not isinstance(value, dict) or set(value) != keys:
        raise CaptureRefusal(f"{context} has an unknown or missing property.")
    return value


def _require_text(value: Any, context: str) -> str:
    if not isinstance(value, str) or not value:
        raise CaptureRefusal(f"{context} is not nonempty text.")
    return value


def _require_int(value: Any, context: str, *, positive: bool = False) -> int:
    if isinstance(value, bool) or not isinstance(value, int):
        raise CaptureRefusal(f"{context} is not an integer.")
    if not -MAX_SAFE_INTEGER <= value <= MAX_SAFE_INTEGER or (positive and value <= 0):
        raise CaptureRefusal(f"{context} integer is outside its bound.")
    return value


ARTIFACT_PAYLOAD_KEYS: Final = {
    "artifactId", "byteLength", "capturedAtUtc", "kind", "repoRelativePath", "sha256"
}
PROCESS_PAYLOAD_KEYS: Final = {
    "argv", "argvNulSha256", "environmentNulSha256", "executableByteLength", "executableDevice",
    "executableInode", "executableRealPath", "executableSha256", "observedAtUtc",
    "externalHomeRealPath", "parentPid", "pid", "processStartedAtUtc", "provider", "startMicroseconds",
    "startSeconds",
}
DISC_PAYLOAD_KEYS: Final = {
    "bindingMode", "binByteLength", "binDevice", "binFd", "binInode", "binLinkCount",
    "binOpen", "binRealPath", "binRepoRelativePath", "binSha256", "cueByteLength",
    "cueDevice", "cueInode", "cueRealPath", "cueRepoRelativePath", "cueSha256",
    "cueSourceLinkCount", "failedV1Excluded", "gdbListenerPid",
    "observedAtUtc", "pid", "provider", "startMicroseconds", "startSeconds",
}
MEDIA_COMMON_KEYS: Final = ARTIFACT_PAYLOAD_KEYS | {
    "backend", "boundsHeight", "boundsWidth", "boundsX", "boundsY", "captureRole",
    "ownerPid", "ownerStartMicroseconds", "ownerStartSeconds", "windowId",
    "windowOwnerPid",
}
RECORDING_MEDIA_KEYS: Final = MEDIA_COMMON_KEYS | {
    "mediaEndAtUtc", "mediaEndMonotonicMicroseconds", "mediaStartAtUtc",
    "mediaStartMonotonicMicroseconds", "ownedRecorderPid",
}


def _validate_artifact_payload(
    payload: dict[str, Any], *, expected_kind: str | None = None
) -> None:
    _require_text(payload["artifactId"], "artifactId")
    _require_int(payload["byteLength"], "artifact byte length", positive=True)
    parse_utc(_require_text(payload["capturedAtUtc"], "artifact capture time"))
    kind = _require_text(payload["kind"], "artifact kind")
    if expected_kind is not None and kind != expected_kind:
        raise CaptureRefusal("Artifact payload has the wrong kind.")
    relative = _require_text(payload["repoRelativePath"], "artifact path")
    guard_relative_path(relative)
    if not relative.startswith(CAPTURE_STAGING_PREFIX + "/"):
        raise CaptureRefusal("Artifact payload escaped capture staging.")
    if not SHA256_RE.fullmatch(_require_text(payload["sha256"], "artifact hash")):
        raise CaptureRefusal("Artifact payload hash is malformed.")


def _validate_journal_semantics(
    journal: dict[str, Any],
    process: dict[str, Any],
    artifacts: Sequence[dict[str, Any]],
) -> None:
    events = journal["events"]
    by_type: dict[str, list[dict[str, Any]]] = {}
    for event in events:
        by_type.setdefault(event["eventType"], []).append(event)

    def only(event_type: str) -> dict[str, Any]:
        matches = by_type.get(event_type, [])
        if len(matches) != 1:
            raise CaptureRefusal(
                f"Capture journal does not contain one exact {event_type} event."
            )
        return matches[0]

    def utc(event: dict[str, Any]) -> dt.datetime:
        return parse_utc(event["atUtc"])

    session_open = only("session-open")
    profile_before = only("profile-before-captured")
    process_event = only("process-start-observed")
    disc_event = only("disc-open-observed")
    watcher_connected = only("watcher-connected")
    resumed = only("manually-resumed")
    recording_start = only("recording-started")
    selector = only("selector-started")
    landing = only("landing-marked")
    recording_stop = only("recording-stopped")
    watcher_stop = only("watcher-stopped")
    process_exit = only("process-exit-observed")
    profile_after = only("profile-after-captured")
    session_sealed = only("session-sealed")

    process_started = parse_utc(process["processStartedAtUtc"])
    process_exited = parse_utc(process_exit["payload"]["processExitedAtUtc"])
    lifecycle = (
        session_open,
        profile_before,
        process_event,
        disc_event,
        watcher_connected,
        resumed,
        recording_start,
        selector,
        landing,
        recording_stop,
        watcher_stop,
        process_exit,
        profile_after,
        session_sealed,
    )
    if (
        process_started >= utc(session_open)
        or any(
            later["sequence"] <= earlier["sequence"]
            or utc(later) < utc(earlier)
            or later["monotonicMicroseconds"] <= earlier["monotonicMicroseconds"]
            for earlier, later in zip(lifecycle, lifecycle[1:])
        )
    ):
        raise CaptureRefusal("Capture journal process, profile, and media lifecycle reordered.")
    if process_exited != utc(process_exit):
        raise CaptureRefusal("Process-exit payload and event clock differ.")

    before_payload = profile_before["payload"]
    after_payload = profile_after["payload"]
    if not (
        process_started < parse_utc(before_payload["capturedAtUtc"])
        and parse_utc(before_payload["capturedAtUtc"]) <= utc(profile_before)
        and after_payload["capturedAtUtc"] == profile_after["atUtc"]
    ):
        raise CaptureRefusal("Profile snapshot clocks do not follow the attested process lifecycle.")

    disc = disc_event["payload"]
    if (
        re.fullmatch(r"[0-9]+r", disc["binFd"]) is None
        or disc["cueRealPath"] == disc["binRealPath"]
    ):
        raise CaptureRefusal("Open BIN descriptor or argv-bound CUE path is malformed.")

    start_payload = recording_start["payload"]
    stop_payload = recording_stop["payload"]
    start_utc = parse_utc(start_payload["mediaStartAtUtc"])
    end_utc = parse_utc(stop_payload["mediaEndAtUtc"])
    start_mono = start_payload["mediaStartMonotonicMicroseconds"]
    end_mono = stop_payload["mediaEndMonotonicMicroseconds"]
    _require_recording_duration(start_mono, end_mono)
    if (
        start_payload["artifactId"] != stop_payload["artifactId"]
        or start_payload["mediaStartAtUtc"] != recording_start["atUtc"]
        or start_mono != recording_start["monotonicMicroseconds"]
        or not start_utc < end_utc <= utc(recording_stop)
        or not start_mono < end_mono <= recording_stop["monotonicMicroseconds"]
        or start_payload["ownedRecorderPid"] != stop_payload["ownedRecorderPid"]
        or start_payload["ownerPid"] != process["pid"]
        or start_payload["windowOwnerPid"] != process["pid"]
        or start_payload["ownerStartSeconds"] != process["startSeconds"]
        or start_payload["ownerStartMicroseconds"] != process["startMicroseconds"]
    ):
        raise CaptureRefusal("Screen-recording lifecycle is not bound to the process window.")

    marker_events: dict[tuple[str, str], dict[str, Any]] = {}
    for event_type in (
        "selector-started",
        "landing-marked",
        "hold-started",
        "hold-ended",
        "fall-started",
        "respawn-marked",
        "check-started",
        "check-ended",
    ):
        for event in by_type.get(event_type, []):
            key = (event_type, event["payload"]["markerId"])
            if key in marker_events:
                raise CaptureRefusal("Capture journal reuses a typed marker event.")
            marker_events[key] = event

    session_id = journal["sessionId"]
    if session_id == "candidate-primary-edge-0":
        completed_start = marker_events[(
            "check-started", "candidate.completed-landing-hud4"
        )]
        completed_end = marker_events[(
            "check-ended", "candidate.completed-landing-hud4"
        )]
        if not (
            completed_start["monotonicMicroseconds"]
            < landing["monotonicMicroseconds"]
            < completed_end["monotonicMicroseconds"]
            and completed_end["monotonicMicroseconds"]
            - completed_start["monotonicMicroseconds"]
            >= 3_500_000
        ):
            raise CaptureRefusal("Completed-landing recording interval is too short or reordered.")
        hold_start = marker_events[("hold-started", "stable-hold")]
        hold_end = marker_events[("hold-ended", "stable-hold")]
        if (
            hold_end["monotonicMicroseconds"]
            - hold_start["monotonicMicroseconds"]
            < 20_500_000
        ):
            raise CaptureRefusal("Hands-off hold is shorter than its fixed capture bound.")
    elif session_id == "candidate-cold-reset" and (
        landing["monotonicMicroseconds"]
        - selector["monotonicMicroseconds"]
        < 5_500_000
    ):
        raise CaptureRefusal(
            "Cold-reset selector-to-landing interval is shorter than 5.5 seconds."
        )

    for marker_id, minimum_microseconds in CHECK_MARKER_PLANS.get(session_id, ()):
        if marker_id in {
            "candidate.completed-landing-hud4",
            "candidate.hands-off-20-seconds",
        }:
            continue
        started = marker_events[("check-started", marker_id)]
        ended = marker_events[("check-ended", marker_id)]
        if (
            ended["monotonicMicroseconds"]
            - started["monotonicMicroseconds"]
            < minimum_microseconds
        ):
            raise CaptureRefusal(f"Capture check interval is too short: {marker_id}")

    if session_id in {
        "candidate-primary-edge-0",
        "candidate-edge-1",
        "candidate-edge-2",
    }:
        fall = marker_events[("fall-started", "edge-fall")]
        respawn = marker_events[("respawn-marked", "edge-respawn")]
        if (
            respawn["monotonicMicroseconds"]
            - fall["monotonicMicroseconds"]
            < 5_500_000
        ):
            raise CaptureRefusal("Death-respawn interval is shorter than 5.5 seconds.")

    metadata = {item["artifactId"]: item for item in artifacts}
    if len(metadata) != len(artifacts):
        raise CaptureRefusal("Capture journal repeats raw artifact metadata.")
    frame_events = {
        event["payload"]["artifactId"]: event
        for event in by_type.get("frame-captured", [])
    }
    frame_metadata = {
        artifact_id: item
        for artifact_id, item in metadata.items()
        if item["kind"] == "frame"
    }
    if set(frame_events) != set(frame_metadata):
        raise CaptureRefusal("Captured frame events and bound frame metadata differ.")
    for artifact_id, item in frame_metadata.items():
        event = frame_events[artifact_id]
        for name in (
            "boundsHeight",
            "boundsWidth",
            "ownerPid",
            "ownerStartSeconds",
            "windowId",
            "windowOwnerPid",
        ):
            _require_int(item[name], f"frame {name}", positive=True)
        for name in ("boundsX", "boundsY", "ownerStartMicroseconds"):
            _require_int(item[name], f"frame {name}")
        if (
            item["captureRole"] != event["payload"]["captureRole"]
            or item["capturedAtUtc"] != event["atUtc"]
            or item["ownerPid"] != process["pid"]
            or item["windowOwnerPid"] != process["pid"]
            or item["ownerStartSeconds"] != process["startSeconds"]
            or item["ownerStartMicroseconds"] != process["startMicroseconds"]
            or item["windowId"] != start_payload["windowId"]
            or any(
                item[name] != start_payload[name]
                for name in (
                    "backend",
                    "boundsHeight",
                    "boundsWidth",
                    "boundsX",
                    "boundsY",
                )
            )
            or not start_utc <= parse_utc(item["capturedAtUtc"]) <= end_utc
        ):
            raise CaptureRefusal("Frame metadata is not bound to its exact process window event.")

    recordings = [item for item in artifacts if item["kind"] == "screen-recording"]
    if len(recordings) != 1:
        raise CaptureRefusal("Capture journal does not bind one exact session recording.")
    recording = recordings[0]
    if (
        recording["artifactId"] != start_payload["artifactId"]
        or recording["capturedAtUtc"] != recording_stop["atUtc"]
        or any(recording[name] != start_payload[name] for name in start_payload)
        or any(recording[name] != stop_payload[name] for name in stop_payload)
    ):
        raise CaptureRefusal("Bound recording metadata differs from its lifecycle events.")
    watcher = watcher_stop["payload"]
    if watcher["capturedAtUtc"] != watcher_stop["atUtc"]:
        raise CaptureRefusal("Watcher artifact capture time differs from its clean stop.")

    for binding in by_type.get("watcher-marker-bound", []):
        payload = binding["payload"]
        sequence = payload["operatorEventSequence"]
        if not 0 <= sequence < len(events):
            raise CaptureRefusal("Watcher binding targets an out-of-range operator event.")
        operator = events[sequence]
        sample_at = parse_utc(payload["sampleAtUtc"])
        operator_at = parse_utc(operator["atUtc"])
        delta_ms = int((sample_at - operator_at).total_seconds() * 1000)
        within_tolerance = (
            0 <= delta_ms <= MAX_FALL_AUTHORIZATION_BINDING_MILLISECONDS
            if payload["markerRole"] == "fall-started"
            else abs(delta_ms) <= 250
        )
        if (
            operator["eventType"] != payload["markerRole"]
            or not within_tolerance
            or not parse_utc(resumed["atUtc"]) <= sample_at <= utc(watcher_stop)
        ):
            raise CaptureRefusal(
                "Watcher marker is outside its role-specific operator binding window."
            )
        if payload["markerRole"] == "hold-ended" and sample_at > operator_at:
            raise CaptureRefusal("Hold-end watcher sample is after its operator boundary.")
        if payload["markerRole"] != "hold-ended" and sample_at < operator_at:
            raise CaptureRefusal("Watcher sample predates its operator boundary.")

    raw_ids = session_sealed["payload"]["rawArtifactIds"]
    if raw_ids != sorted(metadata):
        raise CaptureRefusal("Terminal raw artifact IDs differ from typed metadata.")


def _expected_marker_sequence(session_id: str) -> list[tuple[str, str | None]]:
    result: list[tuple[str, str | None]] = []
    if session_id == "candidate-primary-edge-0":
        result.extend([
            ("hold-started", "stable-hold"),
            ("hold-ended", "stable-hold"),
            ("frame-captured", "candidate.lone-pad-visibility"),
        ])
        for check_id, _ in CHECK_MARKER_PLANS[session_id][2:]:
            result.extend([("check-started", check_id), ("check-ended", check_id)])
    elif session_id.startswith("retail-"):
        for check_id, _ in CHECK_MARKER_PLANS[session_id]:
            result.extend([("check-started", check_id), ("check-ended", check_id)])
    if session_id in {
        "candidate-primary-edge-0", "candidate-edge-1", "candidate-edge-2"
    }:
        result.extend([
            ("frame-captured", CaptureCoordinator._edge_frame_role(session_id, before=True)),
            ("fall-started", "edge-fall"),
            ("respawn-marked", "edge-respawn"),
            ("frame-captured", CaptureCoordinator._edge_frame_role(session_id, before=False)),
        ])
    elif session_id == "candidate-cold-reset":
        result.append(("frame-captured", "candidate.cold-reset-same-pad-yaw.post-reset-frame"))
    return result


def _expected_watcher_roles(session_id: str) -> list[str]:
    roles = ["landing-marked"]
    if session_id == "candidate-primary-edge-0":
        roles.extend(["hold-started", "hold-ended"])
    if session_id in {
        "candidate-primary-edge-0", "candidate-edge-1", "candidate-edge-2"
    }:
        roles.extend(["fall-started", "respawn-marked"])
    return roles


def _expected_raw_artifact_table(
    run_id: str, ordinal: int, session_id: str
) -> dict[str, tuple[str, str, str | None]]:
    result = {
        f"{session_id}.profile-before": (
            "runtime-profile-before", "runtime-profile-before.json", None
        ),
        f"{session_id}.profile-after": (
            "runtime-profile-after", "runtime-profile-after.json", None
        ),
        f"{session_id}.watcher": ("watcher-stdout", "watcher-stdout.log", None),
        f"{session_id}.screen-recording": (
            "screen-recording",
            "session-screen-recording.mov",
            f"{session_id}.full-window-recording",
        ),
    }
    if session_id == "candidate-primary-edge-0":
        result[f"{session_id}.lone-pad"] = (
            "frame", "lone-pad.png", "candidate.lone-pad-visibility"
        )
    if session_id in {
        "candidate-primary-edge-0", "candidate-edge-1", "candidate-edge-2"
    }:
        result[f"{session_id}.edge-before"] = (
            "frame", "edge-before.png",
            CaptureCoordinator._edge_frame_role(session_id, before=True),
        )
        result[f"{session_id}.edge-after"] = (
            "frame", "edge-after.png",
            CaptureCoordinator._edge_frame_role(session_id, before=False),
        )
    if session_id == "candidate-cold-reset":
        result[f"{session_id}.cold-reset-after"] = (
            "frame", "cold-reset-after.png",
            "candidate.cold-reset-same-pad-yaw.post-reset-frame",
        )
    return result


def verify_journal(
    journal: dict[str, Any],
    *,
    expected_run_id: str,
    expected_session_id: str,
    expected_ordinal: int,
    expected_previous_hash: str,
    expected_run_origin_sha256: str,
    expected_pins: Mapping[str, str],
    expected_boot_identity: str | None = None,
    previous_terminal_clock: ClockSample | None = None,
) -> JournalVerification:
    if set(expected_pins) != set(CAPTURE_PIN_KEYS):
        raise CaptureRefusal(
            "Capture journal verification lacks the exact frozen pin closure."
        )
    header_keys = {
        "bootIdentity",
        "events",
        "format",
        "formatVersion",
        "processInstanceId",
        "runOriginSha256",
        "runId",
        "sessionId",
        "sessionOrdinal",
    } | set(CAPTURE_PIN_KEYS)
    if set(journal) != header_keys:
        raise CaptureRefusal("Capture journal has an unknown or missing header property.")
    if (
        journal["format"] != FORMAT
        or journal["formatVersion"] != JOURNAL_FORMAT_VERSION
        or journal["runId"] != expected_run_id
        or journal["sessionId"] != expected_session_id
        or journal["sessionOrdinal"] != expected_ordinal
        or journal["captureCoordinatorRelativePath"]
        != CAPTURE_COORDINATOR_RELATIVE_PATH
        or journal["runOriginSha256"] != expected_run_origin_sha256
        or not SHA256_RE.fullmatch(journal["bootIdentity"])
        or not re.fullmatch(r"id65v3-process-[0-9a-f]{32}", journal["processInstanceId"])
    ):
        raise CaptureRefusal("Capture journal header identity changed.")
    if expected_boot_identity is not None and journal["bootIdentity"] != expected_boot_identity:
        raise CaptureRefusal("Capture journal crossed its run boot identity.")
    for key, expected in expected_pins.items():
        if journal.get(key) != expected:
            raise CaptureRefusal(f"Capture journal pin changed: {key}")
    events = journal["events"]
    if not isinstance(events, list) or not 1 <= len(events) <= MAX_JOURNAL_EVENTS:
        raise CaptureRefusal("Capture journal has an invalid event count.")
    header = {key: value for key, value in journal.items() if key != "events"}
    previous = expected_previous_hash
    previous_clock: ClockSample | None = None
    event_keys = {
        "atUtc",
        "eventSha256",
        "eventType",
        "monotonicMicroseconds",
        "payload",
        "previousEventSha256",
        "sequence",
    }
    terminal_seen = False
    process_event_payload: dict[str, Any] | None = None
    artifact_metadata: list[dict[str, Any]] = []
    for sequence, event in enumerate(events):
        if not isinstance(event, dict) or set(event) != event_keys:
            raise CaptureRefusal("Capture journal event has an unknown or missing property.")
        if event["sequence"] != sequence or event["previousEventSha256"] != previous:
            raise CaptureRefusal("Capture journal sequence or previous hash changed.")
        if not isinstance(event["eventType"], str):
            raise CaptureRefusal("Capture journal event type is not text.")
        payload = event["payload"]
        if not isinstance(payload, dict):
            raise CaptureRefusal("Capture journal payload is not an object.")
        expected_hash = sha256_bytes(_event_manifest(header, event))
        if event["eventSha256"] != expected_hash:
            raise CaptureRefusal("Capture journal event hash changed.")
        _require_int(event["sequence"], "event sequence")
        _require_int(event["monotonicMicroseconds"], "event monotonic clock")
        sample = ClockSample(event["atUtc"], event["monotonicMicroseconds"])
        parse_utc(sample.at_utc)
        if previous_clock is not None:
            if not _within_session_clock_step_is_accepted(
                previous_clock, sample
            ):
                raise CaptureRefusal("Capture journal clocks diverged.")
        elif previous_terminal_clock is not None:
            utc_delta = int(
                (
                    parse_utc(sample.at_utc)
                    - parse_utc(previous_terminal_clock.at_utc)
                ).total_seconds()
                * 1_000_000
            )
            mono_delta = (
                sample.monotonic_microseconds
                - previous_terminal_clock.monotonic_microseconds
            )
            if utc_delta <= 0 or mono_delta <= 0:
                raise CaptureRefusal("Capture journal overlaps the preceding session clock.")
            if not _cross_session_clock_step_is_accepted(
                previous_terminal_clock, sample
            ):
                raise CaptureRefusal("Cross-session capture clocks diverged.")
        event_type = event["eventType"]
        if event_type == "session-open":
            _exact_keys(payload, {"previousSessionTerminalSha256"}, event_type)
            if payload["previousSessionTerminalSha256"] != expected_previous_hash:
                raise CaptureRefusal("Session-open previous terminal hash changed.")
        elif event_type in {"profile-before-captured", "profile-after-captured"}:
            _exact_keys(payload, ARTIFACT_PAYLOAD_KEYS, event_type)
            _validate_artifact_payload(
                payload,
                expected_kind=(
                    "runtime-profile-before" if event_type == "profile-before-captured"
                    else "runtime-profile-after"
                ),
            )
            artifact_metadata.append(payload)
        elif event_type == "process-start-observed":
            _exact_keys(payload, PROCESS_PAYLOAD_KEYS, event_type)
            if payload["provider"] != "macos-libproc+kern-procargs2-external-home-v2":
                raise CaptureRefusal("Process observation provider changed.")
            for name in ("pid", "startSeconds", "executableDevice", "executableInode", "executableByteLength"):
                _require_int(payload[name], f"process {name}", positive=True)
            _require_int(payload["parentPid"], "process parent PID")
            start_usec = _require_int(payload["startMicroseconds"], "process start microseconds")
            if not 0 <= start_usec < 1_000_000:
                raise CaptureRefusal("Process start microseconds are out of range.")
            if not isinstance(payload["argv"], list) or not payload["argv"] or not all(
                isinstance(item, str) and item for item in payload["argv"]
            ):
                raise CaptureRefusal("Process argv is invalid.")
            for name in ("argvNulSha256", "environmentNulSha256", "executableSha256"):
                if not SHA256_RE.fullmatch(_require_text(payload[name], name)):
                    raise CaptureRefusal("Process SHA-256 field is malformed.")
            parse_utc(payload["observedAtUtc"])
            parse_utc(payload["processStartedAtUtc"])
            if (
                payload["observedAtUtc"] != event["atUtc"]
                or payload["processStartedAtUtc"]
                != process_start_utc(payload["startSeconds"], start_usec)
            ):
                raise CaptureRefusal(
                    "Process observation or epoch-derived start time changed."
                )
            expected_argv_hash = sha256_bytes(
                b"\0".join(item.encode("utf-8", "strict") for item in payload["argv"]) + b"\0"
            )
            if payload["argvNulSha256"] != expected_argv_hash:
                raise CaptureRefusal("Process argv byte hash changed.")
            if payload["externalHomeRealPath"] != os.path.realpath(
                pathlib.Path(payload["executableRealPath"]).parents[3] / "home"
            ):
                raise CaptureRefusal("Process payload does not bind its packaged external HOME.")
            reconstructed = ProcessObservation(
                payload["pid"], payload["parentPid"], payload["startSeconds"],
                payload["startMicroseconds"], payload["processStartedAtUtc"],
                FileIdentity(
                    payload["executableRealPath"], payload["executableDevice"],
                    payload["executableInode"], payload["executableByteLength"],
                    payload["executableSha256"],
                ),
                tuple(payload["argv"]), payload["argvNulSha256"],
                payload["environmentNulSha256"], payload["externalHomeRealPath"],
                tuple(),
            )
            if process_instance_id(
                expected_run_id, journal["bootIdentity"], reconstructed
            ) != journal["processInstanceId"]:
                raise CaptureRefusal("Process instance ID is not derived from the OS observation.")
            process_event_payload = payload
        elif event_type == "disc-open-observed":
            _exact_keys(payload, DISC_PAYLOAD_KEYS, event_type)
            if (
                payload["provider"] != "macos-libproc-argv-plus-open-bin-v1"
                or payload["bindingMode"]
                != "exact-argv-cue+pinned-source-cue+read-only-open-bin-v1"
                or payload["observedAtUtc"] != event["atUtc"]
                or payload["cueRepoRelativePath"] != CANDIDATE_CUE_RELATIVE_PATH
                or payload["binRepoRelativePath"] != CANDIDATE_BIN_RELATIVE_PATH
                or payload["cueSha256"] != EXPECTED_CANDIDATE_CUE_SHA256
                or payload["binSha256"] != EXPECTED_CANDIDATE_BIN_SHA256
                or payload["binOpen"] is not True
                or payload["failedV1Excluded"] is not True
            ):
                raise CaptureRefusal("Disc observation changed its exact pinned identity.")
            for name in ("pid", "startSeconds", "cueDevice", "cueInode", "cueByteLength", "cueSourceLinkCount", "binDevice", "binInode", "binByteLength", "binLinkCount"):
                _require_int(payload[name], f"disc {name}", positive=True)
            _require_int(payload["gdbListenerPid"], "disc GDB listener PID", positive=True)
            if payload["cueSourceLinkCount"] != 1 or payload["binLinkCount"] != 1:
                raise CaptureRefusal("Process-open disc link count changed.")
            _require_text(payload["binFd"], "disc BIN fd")
            _require_int(payload["startMicroseconds"], "disc start microseconds")
            if process_event_payload is None or any(
                payload[name] != process_event_payload[name]
                for name in ("pid", "startSeconds", "startMicroseconds")
            ):
                raise CaptureRefusal("Disc observation is not bound to the process observation.")
            if payload["gdbListenerPid"] != payload["pid"]:
                raise CaptureRefusal("GDB listener is not bound to the exact process PID.")
        elif event_type == "watcher-connected":
            _exact_keys(payload, {"ownedWatcherPid", "provider"}, event_type)
            _require_int(payload["ownedWatcherPid"], "owned watcher PID", positive=True)
            if payload["provider"] != "pinned-watcher-subprocess-v1":
                raise CaptureRefusal("Watcher provider changed.")
        elif event_type == "manually-resumed":
            _exact_keys(payload, {"operatorConfirmed"}, event_type)
            if payload["operatorConfirmed"] is not True:
                raise CaptureRefusal("Manual-resume confirmation changed.")
        elif event_type in {
            "selector-started", "landing-marked", "hold-started", "hold-ended",
            "fall-started", "respawn-marked", "check-started", "check-ended"
        }:
            _exact_keys(payload, {"markerId"}, event_type)
            _require_text(payload["markerId"], "marker ID")
        elif event_type == "frame-captured":
            _exact_keys(payload, {"artifactId", "captureRole"}, event_type)
            _require_text(payload["artifactId"], "frame artifact ID")
            _require_text(payload["captureRole"], "frame capture role")
        elif event_type == "recording-started":
            keys = {
                "artifactId", "backend", "boundsHeight", "boundsWidth", "boundsX", "boundsY",
                "captureRole", "mediaStartAtUtc", "mediaStartMonotonicMicroseconds",
                "ownedRecorderPid", "ownerPid", "ownerStartMicroseconds", "ownerStartSeconds",
                "windowId", "windowOwnerPid",
            }
            _exact_keys(payload, keys, event_type)
            if payload["backend"] != "macos-screencapture-window-v1":
                raise CaptureRefusal("Recording backend changed.")
            for name in (
                "boundsHeight", "boundsWidth", "ownedRecorderPid", "ownerPid",
                "ownerStartSeconds", "windowId", "windowOwnerPid",
            ):
                _require_int(payload[name], f"recording {name}", positive=True)
            for name in ("boundsX", "boundsY", "ownerStartMicroseconds", "mediaStartMonotonicMicroseconds"):
                _require_int(payload[name], f"recording {name}")
            parse_utc(payload["mediaStartAtUtc"])
        elif event_type == "recording-stopped":
            _exact_keys(
                payload,
                {
                    "artifactId", "mediaEndAtUtc", "mediaEndMonotonicMicroseconds",
                    "ownedRecorderPid",
                },
                event_type,
            )
            parse_utc(payload["mediaEndAtUtc"])
            _require_int(payload["mediaEndMonotonicMicroseconds"], "recording end monotonic")
            _require_int(payload["ownedRecorderPid"], "owned recorder PID", positive=True)
        elif event_type == "watcher-stopped":
            _exact_keys(payload, ARTIFACT_PAYLOAD_KEYS | {"ownedWatcherPid"}, event_type)
            _validate_artifact_payload(payload, expected_kind="watcher-stdout")
            _require_int(payload["ownedWatcherPid"], "owned watcher PID", positive=True)
            artifact_metadata.append(payload)
        elif event_type == "watcher-marker-bound":
            keys = {"markerRole", "operatorEventSequence", "sampleAtUtc", "sampleElapsedMilliseconds", "sampleOrdinal"}
            _exact_keys(payload, keys, event_type)
            _require_text(payload["markerRole"], "watcher marker role")
            for name in ("operatorEventSequence", "sampleElapsedMilliseconds", "sampleOrdinal"):
                _require_int(payload[name], f"watcher marker {name}")
            parse_utc(payload["sampleAtUtc"])
        elif event_type == "process-exit-observed":
            _exact_keys(payload, {"pid", "processExitedAtUtc", "startMicroseconds", "startSeconds"}, event_type)
            for name in ("pid", "startSeconds"):
                _require_int(payload[name], f"process exit {name}", positive=True)
            _require_int(payload["startMicroseconds"], "process exit start microseconds")
            if payload["processExitedAtUtc"] != event["atUtc"]:
                raise CaptureRefusal("Process exit time differs from event time.")
        elif event_type == "media-artifact-bound":
            kind = payload.get("kind")
            keys = RECORDING_MEDIA_KEYS if kind == "screen-recording" else MEDIA_COMMON_KEYS
            _exact_keys(payload, keys, event_type)
            _validate_artifact_payload(payload, expected_kind=kind)
            if kind not in {"frame", "screen-recording"}:
                raise CaptureRefusal("Bound media kind is invalid.")
            if payload["backend"] != "macos-screencapture-window-v1":
                raise CaptureRefusal("Bound media backend changed.")
            for name in (
                "boundsHeight", "boundsWidth", "ownerPid", "ownerStartSeconds",
                "windowId", "windowOwnerPid",
            ):
                _require_int(payload[name], f"media {name}", positive=True)
            if kind == "screen-recording":
                _require_int(payload["ownedRecorderPid"], "owned recorder PID", positive=True)
            artifact_metadata.append(payload)
        elif event_type == "session-sealed":
            _exact_keys(payload, {"rawArtifactIds"}, event_type)
            ids = payload["rawArtifactIds"]
            if not isinstance(ids, list) or not all(isinstance(item, str) and item for item in ids):
                raise CaptureRefusal("Session seal raw artifact IDs are invalid.")
            if ids != sorted(set(ids)):
                raise CaptureRefusal("Session seal raw artifact IDs are not sorted unique.")
        elif event_type == "capture-aborted":
            _exact_keys(payload, {"reasonCode"}, event_type)
            _require_text(payload["reasonCode"], "capture abort reason")
        else:
            raise CaptureRefusal(f"Capture journal contains unknown event type {event_type!r}.")
        if terminal_seen:
            raise CaptureRefusal("Capture journal has an event after its terminal event.")
        if event["eventType"] in {"session-sealed", "capture-aborted"}:
            terminal_seen = True
        previous = expected_hash
        previous_clock = sample
    if events[-1]["eventType"] != "session-sealed":
        raise CaptureRefusal("Capture journal is not sealed.")
    expected: list[tuple[str, str | None]] = [
        ("session-open", None), ("profile-before-captured", None),
        ("process-start-observed", None), ("disc-open-observed", None),
        ("watcher-connected", None), ("manually-resumed", None),
        ("recording-started", None), ("selector-started", "selector"),
    ]
    if expected_session_id == "candidate-primary-edge-0":
        expected.extend([
            ("check-started", "candidate.completed-landing-hud4"),
            ("landing-marked", "landing"),
            ("check-ended", "candidate.completed-landing-hud4"),
        ])
    else:
        expected.append(("landing-marked", "landing"))
    expected.extend(_expected_marker_sequence(expected_session_id))
    expected.extend([("recording-stopped", None), ("watcher-stopped", None)])
    expected.extend(("watcher-marker-bound", role) for role in _expected_watcher_roles(expected_session_id))
    expected.extend([
        ("process-exit-observed", None), ("profile-after-captured", None)
    ])
    media_events = [item for item in events if item["eventType"] == "media-artifact-bound"]
    media_ids = [item["payload"]["artifactId"] for item in media_events]
    if media_ids != sorted(set(media_ids)):
        raise CaptureRefusal("Bound media artifacts are not sorted unique.")
    expected.extend(("media-artifact-bound", artifact_id) for artifact_id in media_ids)
    expected.append(("session-sealed", None))
    actual: list[tuple[str, str | None]] = []
    for item in events:
        event_type = item["eventType"]
        payload = item["payload"]
        discriminator = None
        if event_type in {"selector-started", "landing-marked", "hold-started", "hold-ended", "fall-started", "respawn-marked", "check-started", "check-ended"}:
            discriminator = payload["markerId"]
        elif event_type == "frame-captured":
            discriminator = payload["captureRole"]
        elif event_type == "watcher-marker-bound":
            discriminator = payload["markerRole"]
        elif event_type == "media-artifact-bound":
            discriminator = payload["artifactId"]
        actual.append((event_type, discriminator))
    if actual != expected:
        raise CaptureRefusal("Capture journal does not match the exact per-session FSM.")
    process_payload_value = process_event_payload
    if process_payload_value is None:
        raise CaptureRefusal("Capture journal omits its process observation.")
    sealed_ids = events[-1]["payload"]["rawArtifactIds"]
    metadata_ids = sorted(item["artifactId"] for item in artifact_metadata)
    if sealed_ids != metadata_ids or len(metadata_ids) != len(set(metadata_ids)):
        raise CaptureRefusal("Session seal does not equal the exact non-journal raw artifact set.")
    _validate_journal_semantics(journal, process_payload_value, artifact_metadata)
    return JournalVerification(
        previous,
        ClockSample(events[0]["atUtc"], events[0]["monotonicMicroseconds"]),
        ClockSample(events[-1]["atUtc"], events[-1]["monotonicMicroseconds"]),
        process_payload_value,
        tuple(artifact_metadata),
    )


class _OfflineBoundary:
    def __init__(self, repository_root: pathlib.Path) -> None:
        self.repository_root = repository_root


class _OfflineSourceGuard:
    def __init__(self, repository_root: pathlib.Path) -> None:
        self.repository_root = repository_root
        self.package_root = repository_root / "offline-source"
        self.external_home_root = self.package_root / "home"
        self.profile_root = (
            self.external_home_root / "Library/Application Support/DuckStation"
        )
        self.executable_path = (
            self.package_root
            / "DuckStation-ID65-Unattended-v1.app/Contents/MacOS/DuckStation"
        )
        self.cue_path = repository_root / "offline-source" / "candidate.cue"
        self.bin_path = repository_root / "offline-source" / "candidate.bin"
        self._executable = FileIdentity(
            os.path.realpath(self.executable_path),
            501,
            601,
            42_000_000,
            EXPECTED_DUCKSTATION_SHA256,
        )
        self._cue = FileIdentity(
            os.path.realpath(self.cue_path),
            502,
            602,
            147,
            EXPECTED_CANDIDATE_CUE_SHA256,
        )
        self._bin = FileIdentity(
            os.path.realpath(self.bin_path),
            503,
            603,
            661_547_040,
            EXPECTED_CANDIDATE_BIN_SHA256,
        )

    def executable_identity(self) -> FileIdentity:
        return self._executable

    def candidate_identities(self) -> tuple[FileIdentity, FileIdentity]:
        return self._cue, self._bin


def _offline_png(seed: str) -> bytes:
    color = hashlib.sha256(seed.encode("utf-8", "strict")).digest()[:3]
    payload = zlib.compress(b"\x00" + color + b"\xff")

    def chunk(kind: bytes, data: bytes) -> bytes:
        return (
            len(data).to_bytes(4, "big")
            + kind
            + data
            + (zlib.crc32(kind + data) & 0xFFFFFFFF).to_bytes(4, "big")
        )

    ihdr = (
        (1).to_bytes(4, "big")
        + (1).to_bytes(4, "big")
        + bytes((8, 6, 0, 0, 0))
    )
    return b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", ihdr) + chunk(
        b"IDAT", payload
    ) + chunk(b"IEND", b"")


def _offline_movie(seed: str) -> bytes:
    def box(kind: bytes, payload: bytes) -> bytes:
        return (len(payload) + 8).to_bytes(4, "big") + kind + payload

    ftyp = box(b"ftyp", b"qt  \x00\x00\x00\x00qt  isom")
    moov = box(b"moov", box(b"free", ("moov:" + seed).encode("utf-8")))
    mdat = box(b"mdat", ("media:" + seed).encode("utf-8"))
    return ftyp + moov + mdat


def _offline_watcher_log(samples: Sequence[dict[str, Any]]) -> bytes:
    preamble = (
        "WARNING: connecting to this DuckStation GDB server pauses a running game exactly once.\n"
        "This watcher sends only the three approved m reads and protocol ACKs; it cannot resume the game.\n"
        "Connected once to 127.0.0.1:2345. DuckStation is now paused.\n"
        "Manually Resume in DuckStation, leave this watcher connected, then press Return here to begin the 0.1-second-period watch: "
        "Watching one numbered near-contemporaneous tuple per period with monotonic elapsed milliseconds; press Ctrl-C to stop cleanly.\n"
    )
    lines: list[str] = []
    for sample in samples:
        state = sample["state"]
        label = (
            "gameplay/non-death"
            if state == 0
            else "death-respawn"
            if state == 4
            else "game-over"
            if state == 5
            else f"other-{state}"
        )
        z_value = sample["playerZ"]
        lines.append(
            f"{sample['atUtc']} sample={sample['ordinal']} "
            f"elapsed-ms={sample['elapsedMilliseconds']} lives={sample['lives']} "
            f"death-state={state}({label}) player-Z={z_value} "
            f"[Z<0x400:{'YES' if z_value < 0x400 else 'no'}] "
            f"raw={sample['lives'].to_bytes(4, 'little').hex()}/"
            f"{state.to_bytes(4, 'little').hex()}/"
            f"{z_value.to_bytes(4, 'little', signed=True).hex()}"
        )
    trailer = (
        "\n\nStopped cleanly; the socket is closed and no control packet was sent. "
        "If DuckStation was not manually resumed, it remains paused.\n"
    )
    return (preamble + "\n".join(lines) + trailer).encode("utf-8", "strict")


def _offline_write_new(path: pathlib.Path, data: bytes) -> FileIdentity:
    with AnchoredStaging.create_new_file(path.parent, path.name) as stream:
        if stream.write(data) != len(data):
            raise CaptureRefusal("Offline fixture write was incomplete.")
        stream.flush()
        os.fsync(stream.fileno())
    return read_regular_file(
        path,
        include_bytes=True,
        expected_owner=os.getuid(),
        expected_mode=0o600,
        maximum_bytes=MAX_TEXT_BYTES if len(data) <= MAX_TEXT_BYTES else None,
    )


def _offline_rechain(journal: dict[str, Any], previous_hash: str) -> None:
    header = {key: value for key, value in journal.items() if key != "events"}
    previous = previous_hash
    for sequence, event in enumerate(journal["events"]):
        event["sequence"] = sequence
        event["previousEventSha256"] = previous
        event["eventSha256"] = sha256_bytes(_event_manifest(header, event))
        previous = event["eventSha256"]


def _offline_write_existing_journal(
    journal_path: pathlib.Path,
    live_path: pathlib.Path,
    journal: dict[str, Any],
) -> None:
    journal_bytes = canonical_bytes(journal)
    live_bytes = b"".join(canonical_bytes(event) + b"\n" for event in journal["events"])
    for path, data in ((journal_path, journal_bytes), (live_path, live_bytes)):
        with open(path, "r+b", buffering=0) as stream:
            stream.truncate(0)
            if stream.write(data) != len(data):
                raise CaptureRefusal("Offline fixture rewrite was incomplete.")
            stream.flush()
            os.fsync(stream.fileno())


def _offline_profile(
    phase: str,
    session_id: str,
    process_id: str,
    captured_at_utc: str,
) -> dict[str, Any]:
    files = [
        {
            "byteLength": EXPECTED_BIOS_SIZE,
            "relativePath": BIOS_MANIFEST_PATH,
            "sha256": EXPECTED_BIOS_SHA256,
        },
        {
            "byteLength": 64,
            "relativePath": "playtime.dat",
            "sha256": sha256_bytes(b"offline-stable-playtime"),
        },
        {
            "byteLength": EXPECTED_SETTINGS_SIZE,
            "relativePath": "settings.ini",
            "sha256": EXPECTED_SETTINGS_SHA256,
        },
    ]
    files.sort(key=lambda item: item["relativePath"])
    return {
        "capturedAtUtc": captured_at_utc,
        "duckStationExecutableSha256": EXPECTED_DUCKSTATION_SHA256,
        "handoffReceiptSha256": EXPECTED_HANDOFF_RECEIPT_SHA256,
        "appContentManifestSha256": EXPECTED_APP_CONTENT_MANIFEST_SHA256,
        "appOutputTreeManifestSha256": EXPECTED_APP_OUTPUT_TREE_MANIFEST_SHA256,
        "packageEnvelopeSha256": EXPECTED_PACKAGE_ENVELOPE_SHA256,
        "externalHomeSettingsSha256": EXPECTED_SETTINGS_SHA256,
        "externalHomeBiosSha256": EXPECTED_BIOS_SHA256,
        "hostNormalSettingsSentinelId": NORMAL_SETTINGS_RESOURCE_ID,
        "hostNormalSettingsSentinelSha256": EXPECTED_NORMAL_SETTINGS_SHA256,
        "dataFiles": files,
        "format": PROFILE_FORMAT,
        "formatVersion": PROFILE_FORMAT_VERSION,
        "phase": phase,
        "processInstanceId": process_id,
        "profileId": PROFILE_ID,
        "sessionId": session_id,
        "snapshotId": f"{session_id}.profile-{phase}",
    }


def _offline_prelaunch_proof() -> dict[str, Any]:
    return {
        "format": "spyro-id65-unattended-profile-preflight",
        "formatVersion": 1,
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
        "launchGate": "EXTERNAL_RUNTIME_LAUNCH_REQUIRED",
        "snapshot": _offline_profile(
            "preflight", "preflight", "not-applicable",
            "1970-01-01T00:00:00.000Z",
        ),
    }


def _offline_capture_pins(coordinator_sha256: str) -> dict[str, str]:
    pins = {
        "watcherRelativePath": WATCHER_RELATIVE_PATH,
        "watcherSha256": EXPECTED_WATCHER_SHA256,
        "profileBuilderRelativePath": PROFILE_BUILDER_RELATIVE_PATH,
        "profileBuilderSha256": EXPECTED_PROFILE_BUILDER_SHA256,
        "profileVerifierRelativePath": PROFILE_VERIFIER_RELATIVE_PATH,
        "profileVerifierSha256": EXPECTED_PROFILE_VERIFIER_SHA256,
        "automationRelativePath": AUTOMATION_RELATIVE_PATH,
        "automationSha256": EXPECTED_AUTOMATION_SHA256,
        "captureCoordinatorRelativePath": CAPTURE_COORDINATOR_RELATIVE_PATH,
        "captureCoordinatorSha256": coordinator_sha256,
        "sourceClosureSha256": recompute_source_closure(coordinator_sha256),
        "runtimeProfileSha256": EXPECTED_RUNTIME_PROFILE_SHA256,
        "profilePackageRelativePath": PROFILE_PACKAGE_RELATIVE_PATH,
        "profileId": PROFILE_ID,
        "handoffReceiptSha256": EXPECTED_HANDOFF_RECEIPT_SHA256,
        "appContentManifestSha256": EXPECTED_APP_CONTENT_MANIFEST_SHA256,
        "appOutputTreeManifestSha256": EXPECTED_APP_OUTPUT_TREE_MANIFEST_SHA256,
        "homeOutputTreeManifestSha256": EXPECTED_HOME_OUTPUT_TREE_MANIFEST_SHA256,
        "packageEnvelopeSha256": EXPECTED_PACKAGE_ENVELOPE_SHA256,
        "externalHomeSettingsSha256": EXPECTED_SETTINGS_SHA256,
        "externalHomeBiosSha256": EXPECTED_BIOS_SHA256,
        "hostNormalSettingsSentinelSha256": EXPECTED_NORMAL_SETTINGS_SHA256,
    }
    if set(pins) != set(CAPTURE_PIN_KEYS):
        raise CaptureRefusal("Offline pin-set closure changed.")
    return pins


def _build_offline_capture_run(
    root: pathlib.Path,
    coordinator_sha256: str,
    *,
    reuse_first_process_fingerprint: bool = False,
    exercise_within_session_utc_ties: bool = False,
) -> tuple[CaptureCoordinator, str, dict[str, pathlib.Path]]:
    staging = AnchoredStaging(root)
    staging.ensure_prefix()
    run_id = "id65v3-20260810t190000z-0123456789abcdef"
    run_path = staging.create_run_directory(run_id)
    boot_identity = sha256_bytes(b"offline-boot-identity")
    pins = _offline_capture_pins(coordinator_sha256)
    prelaunch_proof = _offline_prelaunch_proof()
    origin = {
        "bootIdentity": boot_identity,
        "captureStagingRepoRelativePath": f"{CAPTURE_STAGING_PREFIX}/{run_id}",
        "createdAtUtc": "2026-08-10T18:59:55.000Z",
        "createdMonotonicMicroseconds": 995_000_000,
        "format": RUN_FORMAT,
        "formatVersion": RUN_FORMAT_VERSION,
        "orderedSessionIds": list(SESSION_IDS),
        "prelaunchProof": prelaunch_proof,
        "prelaunchProofSha256": sha256_bytes(canonical_lf_bytes(prelaunch_proof)),
        "repositoryRootRealPath": os.path.realpath(root),
        "runId": run_id,
        **pins,
    }
    origin_identity = AnchoredStaging.write_new_json(
        run_path, "run-origin.json", origin
    )
    source = _OfflineSourceGuard(root)
    coordinator = CaptureCoordinator(
        _OfflineBoundary(root), source_guard=source, staging=staging
    )
    previous_hash = ZERO_SHA256
    paths: dict[str, pathlib.Path] = {
        "run": run_path,
        "origin": run_path / "run-origin.json",
    }
    epoch = dt.datetime(2026, 8, 10, 19, 0, 0, tzinfo=dt.timezone.utc)

    for ordinal, session_id in enumerate(SESSION_IDS):
        session_epoch = epoch + dt.timedelta(seconds=ordinal * 300)
        mono_base = 1_000_000_000 + ordinal * 300_000_000
        process_ordinal = 0 if reuse_first_process_fingerprint and ordinal == 1 else ordinal
        process_epoch = epoch + dt.timedelta(seconds=process_ordinal * 300)

        def clock_at(milliseconds: int) -> ClockSample:
            return ClockSample(
                format_utc(session_epoch + dt.timedelta(milliseconds=milliseconds)),
                mono_base + milliseconds * 1000,
            )

        process = ProcessObservation(
            pid=10_000 + process_ordinal,
            parent_pid=1,
            start_seconds=int(process_epoch.timestamp()),
            start_microseconds=100_000 + process_ordinal,
            process_started_at_utc=process_start_utc(
                int(process_epoch.timestamp()), 100_000 + process_ordinal
            ),
            executable=source.executable_identity(),
            argv=(
                source.executable_identity().real_path,
                source.candidate_identities()[0].real_path,
            ),
            argv_nul_sha256="",
            environment_nul_sha256=sha256_bytes(
                b"\0".join(
                    item.encode("utf-8", "strict")
                    for item in _runtime_environment_assignments(
                        os.path.realpath(source.external_home_root)
                    )
                )
                + b"\0"
            ),
            external_home_real_path=os.path.realpath(source.external_home_root),
            open_files=(),
        )
        process = dataclasses.replace(
            process,
            argv_nul_sha256=sha256_bytes(
                b"\0".join(item.encode("utf-8") for item in process.argv) + b"\0"
            ),
        )
        instance_id = process_instance_id(
            run_id, boot_identity, process
        )
        session_path = staging.create_session_directory(
            run_path, ordinal, session_id
        )
        paths[f"session:{session_id}"] = session_path
        header = {
            "bootIdentity": boot_identity,
            "format": FORMAT,
            "formatVersion": JOURNAL_FORMAT_VERSION,
            "processInstanceId": instance_id,
            "runOriginSha256": origin_identity.sha256,
            "runId": run_id,
            "sessionId": session_id,
            "sessionOrdinal": ordinal,
            **pins,
        }

        scheduled: list[tuple[str, dict[str, Any], int]] = []

        def schedule(event_type: str, payload: dict[str, Any], at_ms: int) -> None:
            if scheduled and at_ms <= scheduled[-1][2]:
                raise CaptureRefusal("Offline event schedule is not strictly ordered.")
            scheduled.append((event_type, payload, at_ms))

        before = _offline_profile(
            "before", session_id, instance_id, clock_at(500).at_utc
        )
        before_identity = _offline_write_new(
            session_path / "runtime-profile-before.json", canonical_lf_bytes(before)
        )
        before_meta = _artifact_metadata(
            f"{session_id}.profile-before",
            "runtime-profile-before",
            CaptureCoordinator._session_relative(
                run_id, ordinal, session_id, "runtime-profile-before.json"
            ),
            before["capturedAtUtc"],
            before_identity,
        )
        raw_artifacts: list[dict[str, Any]] = [before_meta]
        schedule(
            "session-open",
            {"previousSessionTerminalSha256": previous_hash},
            1000,
        )
        schedule("profile-before-captured", before_meta, 1100)
        schedule("process-start-observed", process_payload(process, clock_at(1200)), 1200)
        cue, bin_file = source.candidate_identities()
        bin_open = OpenFileObservation("13r", bin_file.device, bin_file.inode, bin_file.byte_length, 1, bin_file.real_path)
        schedule(
            "disc-open-observed",
            disc_payload(process, cue, bin_file, bin_open, clock_at(1300)),
            1300,
        )
        watcher_pid = 20_000 + ordinal
        schedule(
            "watcher-connected",
            {
                "ownedWatcherPid": watcher_pid,
                "provider": "pinned-watcher-subprocess-v1",
            },
            2000,
        )
        schedule("manually-resumed", {"operatorConfirmed": True}, 3000)

        recording_id = f"{session_id}.screen-recording"
        recording_role = f"{session_id}.full-window-recording"
        recorder_pid = 40_000 + ordinal
        recording_start_payload = {
            "artifactId": recording_id,
            "backend": "macos-screencapture-window-v1",
            "boundsHeight": 720,
            "boundsWidth": 960,
            "boundsX": 20,
            "boundsY": 30,
            "captureRole": recording_role,
            "mediaStartAtUtc": clock_at(3500).at_utc,
            "mediaStartMonotonicMicroseconds": clock_at(3500).monotonic_microseconds,
            "ownedRecorderPid": recorder_pid,
            "ownerPid": process.pid,
            "ownerStartMicroseconds": process.start_microseconds,
            "ownerStartSeconds": process.start_seconds,
            "windowId": 30_000 + ordinal,
            "windowOwnerPid": process.pid,
        }
        schedule("recording-started", recording_start_payload, 3500)
        selector_ms = 6000 if session_id == "candidate-primary-edge-0" else 4000
        schedule("selector-started", {"markerId": "selector"}, selector_ms)
        frame_specs: list[tuple[str, str, int]] = []
        fall_transition_ms: int | None = None
        respawn_transition_ms: int | None = None

        if session_id == "candidate-primary-edge-0":
            schedule(
                "check-started",
                {"markerId": "candidate.completed-landing-hud4"},
                7000,
            )
            schedule("landing-marked", {"markerId": "landing"}, 18300)
            schedule(
                "check-ended",
                {"markerId": "candidate.completed-landing-hud4"},
                18800,
            )
            schedule("hold-started", {"markerId": "stable-hold"}, 19300)
            schedule("hold-ended", {"markerId": "stable-hold"}, 39800)
            frame_specs.append(
                ("lone-pad", "candidate.lone-pad-visibility", 40300)
            )
            cursor = 40800
            for marker_id, minimum_us in CHECK_MARKER_PLANS[session_id][2:]:
                schedule("check-started", {"markerId": marker_id}, cursor)
                cursor += minimum_us // 1000
                schedule("check-ended", {"markerId": marker_id}, cursor)
                cursor += 500
            frame_specs.append(
                (
                    "edge-before",
                    CaptureCoordinator._edge_frame_role(session_id, before=True),
                    cursor,
                )
            )
            fall_ms = cursor + 500
            fall_transition_ms = fall_ms + 100
            schedule("fall-started", {"markerId": "edge-fall"}, fall_ms)
            respawn_ms = fall_ms + 5500
            respawn_transition_ms = fall_transition_ms + 1_000
            schedule("respawn-marked", {"markerId": "edge-respawn"}, respawn_ms)
            frame_specs.append(
                (
                    "edge-after",
                    CaptureCoordinator._edge_frame_role(session_id, before=False),
                    respawn_ms + 500,
                )
            )
        elif session_id in {"candidate-edge-1", "candidate-edge-2"}:
            schedule("landing-marked", {"markerId": "landing"}, 5000)
            frame_specs.append(
                (
                    "edge-before",
                    CaptureCoordinator._edge_frame_role(session_id, before=True),
                    5500,
                )
            )
            fall_ms = 6000
            fall_transition_ms = fall_ms + 100
            schedule("fall-started", {"markerId": "edge-fall"}, fall_ms)
            respawn_ms = 11500
            respawn_transition_ms = fall_transition_ms + 1_000
            schedule("respawn-marked", {"markerId": "edge-respawn"}, respawn_ms)
            frame_specs.append(
                (
                    "edge-after",
                    CaptureCoordinator._edge_frame_role(session_id, before=False),
                    12000,
                )
            )
        elif session_id == "candidate-cold-reset":
            fall_ms = None
            respawn_ms = None
            schedule("landing-marked", {"markerId": "landing"}, 9500)
            frame_specs.append(
                (
                    "cold-reset-after",
                    "candidate.cold-reset-same-pad-yaw.post-reset-frame",
                    10000,
                )
            )
        else:
            fall_ms = None
            respawn_ms = None
            schedule("landing-marked", {"markerId": "landing"}, 5000)
            cursor = 5500
            for marker_id, minimum_us in CHECK_MARKER_PLANS[session_id]:
                schedule("check-started", {"markerId": marker_id}, cursor)
                cursor += minimum_us // 1000
                schedule("check-ended", {"markerId": marker_id}, cursor)
                cursor += 500

        for leaf, role, at_ms in sorted(frame_specs, key=lambda item: item[2]):
            frame_identity = _offline_write_new(
                session_path / f"{leaf}.png",
                _offline_png(f"{session_id}:{leaf}"),
            )
            frame_meta = _artifact_metadata(
                f"{session_id}.{leaf}",
                "frame",
                CaptureCoordinator._session_relative(
                    run_id, ordinal, session_id, f"{leaf}.png"
                ),
                clock_at(at_ms).at_utc,
                frame_identity,
            )
            frame_meta.update(
                {
                    "backend": "macos-screencapture-window-v1",
                    "boundsHeight": 720,
                    "boundsWidth": 960,
                    "boundsX": 20,
                    "boundsY": 30,
                    "captureRole": role,
                    "ownerPid": process.pid,
                    "ownerStartMicroseconds": process.start_microseconds,
                    "ownerStartSeconds": process.start_seconds,
                    "windowId": 30_000 + ordinal,
                    "windowOwnerPid": process.pid,
                }
            )
            raw_artifacts.append(frame_meta)
            scheduled.append(
                (
                    "frame-captured",
                    {"artifactId": frame_meta["artifactId"], "captureRole": role},
                    at_ms,
                )
            )
            scheduled.sort(key=lambda item: item[2])
            if any(
                right[2] <= left[2]
                for left, right in zip(scheduled, scheduled[1:])
            ):
                raise CaptureRefusal("Offline frame schedule overlaps another event.")

        last_capture_ms = max(item[2] for item in scheduled)
        recording_stop_ms = last_capture_ms + 500
        watcher_stop_ms = recording_stop_ms + 500
        recording_identity = _offline_write_new(
            session_path / "session-screen-recording.mov",
            _offline_movie(session_id),
        )
        recording_meta = _artifact_metadata(
            recording_id,
            "screen-recording",
            CaptureCoordinator._session_relative(
                run_id, ordinal, session_id, "session-screen-recording.mov"
            ),
            clock_at(recording_stop_ms).at_utc,
            recording_identity,
        )
        recording_meta.update(
            recording_start_payload
            | {
                "mediaEndAtUtc": clock_at(recording_stop_ms).at_utc,
                "mediaEndMonotonicMicroseconds": clock_at(
                    recording_stop_ms
                ).monotonic_microseconds,
            }
        )
        raw_artifacts.append(recording_meta)
        schedule(
            "recording-stopped",
            {
                "artifactId": recording_id,
                "mediaEndAtUtc": clock_at(recording_stop_ms).at_utc,
                "mediaEndMonotonicMicroseconds": clock_at(
                    recording_stop_ms
                ).monotonic_microseconds,
                "ownedRecorderPid": recorder_pid,
            },
            recording_stop_ms,
        )

        sample_rows: list[dict[str, Any]] = []
        for sample_ms in range(3100, watcher_stop_ms, 100):
            lives = 4
            state = 0
            player_z = 1200
            if sample_ms < 3500:
                lives = 0
                state = 13
                player_z = 0
            elif sample_ms < 4000:
                lives = 4
                state = 1
            elif session_id == "candidate-primary-edge-0" and 7000 <= sample_ms < 11000:
                state = 3
            elif session_id == "candidate-primary-edge-0" and 11000 <= sample_ms < 16000:
                state = 1
            elif fall_transition_ms is not None and sample_ms >= fall_transition_ms:
                lives = 3
                state = 4
                player_z = 900
                if (
                    respawn_transition_ms is not None
                    and sample_ms >= respawn_transition_ms
                ):
                    lives = 3
                    state = 0
                    player_z = 1200
            sample_rows.append(
                {
                    "atUtc": clock_at(sample_ms).at_utc,
                    "elapsedMilliseconds": sample_ms - 3100,
                    "lives": lives,
                    "ordinal": len(sample_rows),
                    "playerZ": player_z,
                    "state": state,
                }
            )
        watcher_identity = _offline_write_new(
            session_path / "watcher-stdout.log",
            _offline_watcher_log(sample_rows),
        )
        watcher_meta = _artifact_metadata(
            f"{session_id}.watcher",
            "watcher-stdout",
            CaptureCoordinator._session_relative(
                run_id, ordinal, session_id, "watcher-stdout.log"
            ),
            clock_at(watcher_stop_ms).at_utc,
            watcher_identity,
        )
        raw_artifacts.append(watcher_meta)
        watcher_payload = dict(watcher_meta)
        watcher_payload["ownedWatcherPid"] = watcher_pid
        schedule("watcher-stopped", watcher_payload, watcher_stop_ms)
        scheduled.sort(key=lambda item: item[2])

        events: list[dict[str, Any]] = []
        current_hash = previous_hash
        first_media_event_at_utc: str | None = None

        def add_scheduled(
            event_type: str, payload: dict[str, Any], at_ms: int
        ) -> dict[str, Any]:
            nonlocal current_hash, first_media_event_at_utc
            sample = clock_at(at_ms)
            event_at_utc = sample.at_utc
            if exercise_within_session_utc_ties and ordinal == 0:
                if event_type in {
                    "session-open",
                    "profile-before-captured",
                    "process-start-observed",
                }:
                    event_at_utc = clock_at(1_200).at_utc
                elif event_type == "media-artifact-bound":
                    first_media_event_at_utc = (
                        first_media_event_at_utc or event_at_utc
                    )
                    event_at_utc = first_media_event_at_utc
            event: dict[str, Any] = {
                "atUtc": event_at_utc,
                "eventType": event_type,
                "monotonicMicroseconds": sample.monotonic_microseconds,
                "payload": payload,
                "previousEventSha256": current_hash,
                "sequence": len(events),
            }
            event["eventSha256"] = sha256_bytes(_event_manifest(header, event))
            events.append(event)
            current_hash = event["eventSha256"]
            return event

        for event_type, payload, at_ms in scheduled:
            add_scheduled(event_type, payload, at_ms)

        marker_by_role = {
            event["eventType"]: event
            for event in events
            if event["eventType"] in _expected_watcher_roles(session_id)
        }
        binding_ms = watcher_stop_ms + 10
        sample_by_utc = {row["atUtc"]: row for row in sample_rows}
        for role in _expected_watcher_roles(session_id):
            marker = marker_by_role[role]
            offset_ms = (
                -100 if role == "hold-ended"
                else 0 if role == "respawn-marked"
                else 100
            )
            chosen_at = format_utc(
                parse_utc(marker["atUtc"]) + dt.timedelta(milliseconds=offset_ms)
            )
            chosen = sample_by_utc.get(chosen_at)
            if chosen is None:
                raise CaptureRefusal(
                    "Offline marker lacks its directional watcher sample."
                )
            add_scheduled(
                "watcher-marker-bound",
                {
                    "markerRole": role,
                    "operatorEventSequence": marker["sequence"],
                    "sampleAtUtc": chosen["atUtc"],
                    "sampleElapsedMilliseconds": chosen["elapsedMilliseconds"],
                    "sampleOrdinal": chosen["ordinal"],
                },
                binding_ms,
            )
            binding_ms += 10

        process_exit_ms = binding_ms + 500
        add_scheduled(
            "process-exit-observed",
            {
                "pid": process.pid,
                "processExitedAtUtc": clock_at(process_exit_ms).at_utc,
                "startMicroseconds": process.start_microseconds,
                "startSeconds": process.start_seconds,
            },
            process_exit_ms,
        )
        after_ms = process_exit_ms + 500
        after = _offline_profile(
            "after", session_id, instance_id, clock_at(after_ms).at_utc
        )
        after_identity = _offline_write_new(
            session_path / "runtime-profile-after.json", canonical_lf_bytes(after)
        )
        after_meta = _artifact_metadata(
            f"{session_id}.profile-after",
            "runtime-profile-after",
            CaptureCoordinator._session_relative(
                run_id, ordinal, session_id, "runtime-profile-after.json"
            ),
            after["capturedAtUtc"],
            after_identity,
        )
        raw_artifacts.append(after_meta)
        add_scheduled("profile-after-captured", after_meta, after_ms)
        media_ms = after_ms + 10
        for media in sorted(
            (
                item
                for item in raw_artifacts
                if item["kind"] in {"frame", "screen-recording"}
            ),
            key=lambda item: item["artifactId"],
        ):
            add_scheduled("media-artifact-bound", media, media_ms)
            media_ms += 10
        add_scheduled(
            "session-sealed",
            {
                "rawArtifactIds": sorted(
                    item["artifactId"] for item in raw_artifacts
                )
            },
            media_ms,
        )
        journal = dict(header)
        journal["events"] = events
        _offline_write_new(
            session_path / "capture-events-live.ndjson",
            b"".join(canonical_bytes(event) + b"\n" for event in events),
        )
        _offline_write_new(
            session_path / "capture-event-journal.json", canonical_bytes(journal)
        )
        paths[f"journal:{session_id}"] = session_path / "capture-event-journal.json"
        paths[f"live:{session_id}"] = session_path / "capture-events-live.ndjson"
        paths[f"recording:{session_id}"] = session_path / "session-screen-recording.mov"
        paths[f"watcher:{session_id}"] = session_path / "watcher-stdout.log"
        if frame_specs:
            paths[f"frame:{session_id}"] = session_path / f"{frame_specs[0][0]}.png"
        previous_hash = events[-1]["eventSha256"]

    return coordinator, run_id, paths


def run_offline_self_test(repository_root: pathlib.Path) -> None:
    coordinator_identity = read_regular_file(
        repository_root / CAPTURE_COORDINATOR_RELATIVE_PATH,
        expected_owner=os.getuid(),
        expected_mode=0o755,
    )
    contract_identity = read_regular_file(
        repository_root / CONTRACT_RELATIVE_PATH,
        include_bytes=True,
        expected_owner=os.getuid(),
        maximum_bytes=4 * 1024 * 1024,
    )
    pins = _parse_contract_pins(contract_identity.bytes_value or b"")
    if (
        any(SHA256_RE.fullmatch(value) is None for value in pins.values())
        or recompute_runtime_profile_v2_sha256() != EXPECTED_RUNTIME_PROFILE_SHA256
        or not SHA256_RE.fullmatch(
            recompute_source_closure(coordinator_identity.sha256)
        )
    ):
        raise CaptureRefusal("Offline source/profile manifest self-check failed.")

    same_millisecond = ClockSample("2026-08-10T17:59:59.000Z", 1_000_000)
    if not (
        _within_session_clock_step_is_accepted(
            same_millisecond,
            ClockSample(same_millisecond.at_utc, 1_250_000),
        )
        and not _within_session_clock_step_is_accepted(
            same_millisecond,
            ClockSample(same_millisecond.at_utc, 1_250_001),
        )
        and not _within_session_clock_step_is_accepted(
            same_millisecond,
            ClockSample("2026-08-10T17:59:58.999Z", 1_001_000),
        )
        and not _within_session_clock_step_is_accepted(
            same_millisecond,
            ClockSample(same_millisecond.at_utc, 1_000_000),
        )
        and not _within_session_clock_step_is_accepted(
            same_millisecond,
            ClockSample(same_millisecond.at_utc, 999_999),
        )
    ):
        raise CaptureRefusal("Within-session dual-clock boundary changed.")

    cross_session_previous = ClockSample(
        "2026-08-10T17:59:59.000Z", 1_000_000
    )
    if not (
        _cross_session_clock_step_is_accepted(
            cross_session_previous,
            ClockSample("2026-08-10T17:59:59.001Z", 1_251_000),
        )
        and not _cross_session_clock_step_is_accepted(
            cross_session_previous,
            ClockSample("2026-08-10T17:59:59.001Z", 1_251_001),
        )
        and not _cross_session_clock_step_is_accepted(
            cross_session_previous,
            ClockSample("2026-08-10T17:59:59.000Z", 1_000_001),
        )
        and not _cross_session_clock_step_is_accepted(
            cross_session_previous,
            ClockSample("2026-08-10T17:59:59.001Z", 1_000_000),
        )
        and not _cross_session_clock_step_is_accepted(
            cross_session_previous,
            ClockSample("2026-08-10T17:59:59.001Z", 999_999),
        )
    ):
        raise CaptureRefusal("Cross-session dual-clock boundary changed.")

    _require_recording_duration(0, 1)
    _require_recording_duration(
        1_000_000,
        1_000_000 + MAX_RECORDING_DURATION_MICROSECONDS,
    )
    recording_duration_refusals = 0
    for end in (
        1_000_000,
        1_000_000 + MAX_RECORDING_DURATION_MICROSECONDS + 1,
    ):
        try:
            _require_recording_duration(1_000_000, end)
        except CaptureRefusal:
            recording_duration_refusals += 1
        else:
            raise CaptureRefusal("Recording-duration boundary tamper was accepted.")
    for start, end in ((-1, 0), (2, 1)):
        try:
            _require_recording_duration(start, end)
        except CaptureRefusal:
            recording_duration_refusals += 1
        else:
            raise CaptureRefusal("Recording-duration clock tamper was accepted.")

    assisted_epoch = dt.datetime(2026, 8, 10, 18, 0, tzinfo=dt.timezone.utc)
    assisted_samples: list[dict[str, Any]] = []
    for ordinal in range(123):
        if ordinal <= 20 or ordinal >= 101:
            state = 0
        elif ordinal <= 60:
            state = 3
        else:
            state = 1
        assisted_samples.append({
            "atUtc": format_utc(
                assisted_epoch + dt.timedelta(milliseconds=ordinal * 100)
            ),
            "elapsedMilliseconds": ordinal * 100,
            "lives": 4,
            "ordinal": ordinal,
            "playerZ": 8_548,
            "state": state,
        })
    assisted_arm_at = parse_utc(assisted_samples[20]["atUtc"])
    departure, landing_transition, safe_endpoint = (
        _candidate_primary_landing_transition(
            assisted_samples,
            20,
            assisted_arm_at,
        )
    )
    if not (
        departure is not None
        and departure["ordinal"] == 21
        and landing_transition is not None
        and landing_transition["ordinal"] == 101
        and safe_endpoint is not None
        and safe_endpoint["ordinal"] == 122
        and _candidate_primary_landing_evidence_is_accepted(
            assisted_samples,
            assisted_samples[20]["atUtc"],
            5_000_000,
            assisted_samples[21]["atUtc"],
            5_100_000,
            122,
        )
        and not _candidate_primary_landing_evidence_is_accepted(
            assisted_samples,
            assisted_samples[20]["atUtc"],
            5_000_000,
            assisted_samples[22]["atUtc"],
            5_200_000,
            122,
        )
        and not _candidate_primary_landing_evidence_is_accepted(
            assisted_samples,
            assisted_samples[20]["atUtc"],
            5_000_000,
            assisted_samples[21]["atUtc"],
            5_100_001,
            122,
        )
    ):
        raise CaptureRefusal("Assisted landing positive fixture was not accepted exactly.")
    derived_departure_clock = _watcher_sample_clock(
        assisted_samples[21],
        ClockSample(assisted_samples[20]["atUtc"], 5_000_000),
    )
    if (
        derived_departure_clock.at_utc != assisted_samples[21]["atUtc"]
        or derived_departure_clock.monotonic_microseconds != 5_100_000
    ):
        raise CaptureRefusal("Assisted watcher marker dual-clock derivation changed.")
    short_departure, short_landing, short_endpoint = (
        _candidate_primary_landing_transition(
            assisted_samples[:119],
            20,
            assisted_arm_at,
        )
    )
    if short_departure is None or short_landing is None or short_endpoint is not None:
        raise CaptureRefusal("Assisted landing accepted a suffix shorter than 2 seconds.")

    stale_selector_suffix = [dict(item) for item in assisted_samples]
    stale_selector_suffix[5]["state"] = 13
    if (
        _candidate_primary_landing_evidence_is_accepted(
            stale_selector_suffix,
            stale_selector_suffix[20]["atUtc"],
            5_000_000,
            stale_selector_suffix[21]["atUtc"],
            5_100_000,
            122,
        )
        or _candidate_primary_landing_evidence_is_accepted(
            assisted_samples,
            assisted_samples[21]["atUtc"],
            5_100_000,
            assisted_samples[21]["atUtc"],
            5_100_000,
            122,
        )
    ):
        raise CaptureRefusal(
            "Assisted landing accepted a stale suffix or post-departure selector."
        )

    boundary_samples: list[dict[str, Any]] = []
    for ordinal in range(342):
        if ordinal <= 20 or ordinal >= 321:
            state = 0
        elif ordinal <= 120:
            state = 3
        else:
            state = 1
        boundary_samples.append({
            "atUtc": format_utc(
                assisted_epoch + dt.timedelta(milliseconds=ordinal * 100)
            ),
            "elapsedMilliseconds": ordinal * 100,
            "lives": 4,
            "ordinal": ordinal,
            "playerZ": 8_548,
            "state": state,
        })
    exact_departure, exact_landing, exact_endpoint = (
        _candidate_primary_landing_transition(
            boundary_samples,
            20,
            parse_utc(boundary_samples[20]["atUtc"]),
        )
    )
    if not (
        exact_departure is not None
        and exact_landing is not None
        and exact_endpoint is not None
        and int(exact_landing["elapsedMilliseconds"])
        - int(exact_departure["elapsedMilliseconds"])
        == ASSISTED_PRIMARY_LANDING_MAXIMUM_MILLISECONDS
    ):
        raise CaptureRefusal("Assisted landing rejected its exact 30-second boundary.")
    over_boundary = [dict(item) for item in boundary_samples]
    over_boundary[321]["elapsedMilliseconds"] += 1
    try:
        _candidate_primary_landing_transition(
            over_boundary,
            20,
            parse_utc(over_boundary[20]["atUtc"]),
        )
    except CaptureRefusal:
        pass
    else:
        raise CaptureRefusal("Assisted landing accepted 30,001 milliseconds.")

    delayed_departure_samples: list[dict[str, Any]] = []
    for ordinal in range(3021):
        delayed_departure_samples.append({
            "atUtc": format_utc(
                assisted_epoch + dt.timedelta(milliseconds=ordinal * 100)
            ),
            "elapsedMilliseconds": ordinal * 100,
            "lives": 4,
            "ordinal": ordinal,
            "playerZ": 8_548,
            "state": 3 if ordinal == 3020 else 0,
        })
    delayed_departure, _, _ = _candidate_primary_landing_transition(
        delayed_departure_samples,
        20,
        parse_utc(delayed_departure_samples[20]["atUtc"]),
    )
    if delayed_departure is None or delayed_departure["ordinal"] != 3020:
        raise CaptureRefusal("Assisted departure rejected its exact 5-minute bound.")
    over_departure_samples = [dict(item) for item in delayed_departure_samples]
    over_departure_samples[3020]["state"] = 0
    over_departure_samples.append({
        "atUtc": format_utc(
            assisted_epoch + dt.timedelta(milliseconds=302_100)
        ),
        "elapsedMilliseconds": 302_100,
        "lives": 4,
        "ordinal": 3021,
        "playerZ": 8_548,
        "state": 3,
    })
    try:
        _candidate_primary_landing_transition(
            over_departure_samples,
            20,
            parse_utc(over_departure_samples[20]["atUtc"]),
        )
    except CaptureRefusal:
        pass
    else:
        raise CaptureRefusal("Assisted departure accepted 5 minutes plus 100ms.")

    no_landing_exact = [dict(item) for item in assisted_samples[:22]]
    for ordinal in range(22, 322):
        no_landing_exact.append({
            "atUtc": format_utc(
                assisted_epoch + dt.timedelta(milliseconds=ordinal * 100)
            ),
            "elapsedMilliseconds": ordinal * 100,
            "lives": 4,
            "ordinal": ordinal,
            "playerZ": 8_548,
            "state": 3,
        })
    exact_pending_departure, exact_pending_landing, _ = (
        _candidate_primary_landing_transition(
            no_landing_exact,
            20,
            parse_utc(no_landing_exact[20]["atUtc"]),
        )
    )
    if exact_pending_departure is None or exact_pending_landing is not None:
        raise CaptureRefusal("Assisted no-landing exact boundary changed.")
    no_landing_over = [dict(item) for item in no_landing_exact]
    no_landing_over.append({
        "atUtc": format_utc(
            assisted_epoch + dt.timedelta(milliseconds=32_200)
        ),
        "elapsedMilliseconds": 32_200,
        "lives": 4,
        "ordinal": 322,
        "playerZ": 8_548,
        "state": 3,
    })
    try:
        _candidate_primary_landing_transition(
            no_landing_over,
            20,
            parse_utc(no_landing_over[20]["atUtc"]),
        )
    except CaptureRefusal:
        pass
    else:
        raise CaptureRefusal("Assisted fly-in without landing exceeded 30 seconds.")

    assisted_landing_refusals = 0
    assisted_mutations: list[tuple[str, list[dict[str, Any]], dt.datetime]] = []
    for label, ordinal, changes in (
        ("skip state 3", 21, {"state": 1}),
        ("state regression", 70, {"state": 3}),
        ("life drift", 80, {"lives": 3}),
        ("low Z", 80, {"playerZ": 900}),
        ("unsafe postlanding", 110, {"state": 4}),
    ):
        changed = [dict(item) for item in assisted_samples]
        changed[ordinal].update(changes)
        assisted_mutations.append((label, changed, assisted_arm_at))
    assisted_mutations.append((
        "departure before arm",
        [dict(item) for item in assisted_samples],
        parse_utc(assisted_samples[22]["atUtc"]),
    ))
    for label, changed, armed_at in assisted_mutations:
        try:
            _candidate_primary_landing_transition(changed, 20, armed_at)
        except CaptureRefusal:
            assisted_landing_refusals += 1
        else:
            raise CaptureRefusal(
                f"Assisted landing transition tamper was accepted: {label}."
            )

    boundary_refusals = 0

    def expect_boundary_refusal(label: str, action: Callable[[], None]) -> None:
        nonlocal boundary_refusals
        try:
            action()
        except CaptureRefusal:
            boundary_refusals += 1
            return
        raise CaptureRefusal(f"Offline macOS boundary tamper was accepted: {label}")

    absent = object()

    def console_fixture(
        *,
        root_lock: Any = False,
        users_value: Any = absent,
        on_console: Any = True,
        login_done: Any = True,
        user_id: Any = 501,
        user_name: Any = "john",
        user_lock: Any = absent,
    ) -> dict[str, Any]:
        session: dict[str, Any] = {
            "kCGSSessionOnConsoleKey": on_console,
            "kCGSessionLoginDoneKey": login_done,
            "kCGSSessionUserIDKey": user_id,
            "kCGSSessionUserNameKey": user_name,
        }
        if user_lock is not absent:
            session["CGSSessionScreenIsLocked"] = user_lock
        return {
            "IOConsoleLocked": root_lock,
            "IOConsoleUsers": [session] if users_value is absent else users_value,
        }

    _require_exact_console_session(
        console_fixture(), expected_uid=501, expected_name="john"
    )
    _require_exact_console_session(
        [console_fixture(user_lock=False)], expected_uid=501, expected_name="john"
    )
    console_refusals: tuple[tuple[str, Callable[[], None]], ...] = (
        ("root type", lambda: _require_exact_console_session(
            "root", expected_uid=501, expected_name="john")),
        ("empty root array", lambda: _require_exact_console_session(
            [], expected_uid=501, expected_name="john")),
        ("duplicate root array", lambda: _require_exact_console_session(
            [console_fixture(), console_fixture()], expected_uid=501,
            expected_name="john")),
        ("nondictionary root array", lambda: _require_exact_console_session(
            [False], expected_uid=501, expected_name="john")),
        ("missing root lock", lambda: _require_exact_console_session(
            {"IOConsoleUsers": []}, expected_uid=501, expected_name="john")),
        ("wrong root lock type", lambda: _require_exact_console_session(
            console_fixture(root_lock=0), expected_uid=501, expected_name="john")),
        ("locked root", lambda: _require_exact_console_session(
            console_fixture(root_lock=True), expected_uid=501, expected_name="john")),
        ("wrong users type", lambda: _require_exact_console_session(
            console_fixture(users_value={}), expected_uid=501, expected_name="john")),
        ("wrong user entry type", lambda: _require_exact_console_session(
            console_fixture(users_value=[False]), expected_uid=501,
            expected_name="john")),
        ("wrong on-console type", lambda: _require_exact_console_session(
            console_fixture(on_console=1), expected_uid=501, expected_name="john")),
        ("no on-console session", lambda: _require_exact_console_session(
            console_fixture(on_console=False), expected_uid=501, expected_name="john")),
        ("duplicate on-console sessions", lambda: _require_exact_console_session(
            console_fixture(users_value=[
                console_fixture()["IOConsoleUsers"][0],
                console_fixture()["IOConsoleUsers"][0],
            ]), expected_uid=501, expected_name="john")),
        ("login false", lambda: _require_exact_console_session(
            console_fixture(login_done=False), expected_uid=501, expected_name="john")),
        ("wrong login type", lambda: _require_exact_console_session(
            console_fixture(login_done=1), expected_uid=501, expected_name="john")),
        ("wrong user ID", lambda: _require_exact_console_session(
            console_fixture(user_id=502), expected_uid=501, expected_name="john")),
        ("wrong user ID type", lambda: _require_exact_console_session(
            console_fixture(user_id=True), expected_uid=501, expected_name="john")),
        ("wrong user name", lambda: _require_exact_console_session(
            console_fixture(user_name="other"), expected_uid=501,
            expected_name="john")),
        ("wrong user name type", lambda: _require_exact_console_session(
            console_fixture(user_name=501), expected_uid=501, expected_name="john")),
        ("locked user", lambda: _require_exact_console_session(
            console_fixture(user_lock=True), expected_uid=501, expected_name="john")),
        ("wrong user lock type", lambda: _require_exact_console_session(
            console_fixture(user_lock=0), expected_uid=501, expected_name="john")),
        ("wrong expected UID type", lambda: _require_exact_console_session(
            console_fixture(), expected_uid=True, expected_name="john")),
        ("wrong expected name type", lambda: _require_exact_console_session(
            console_fixture(), expected_uid=501, expected_name="")),
    )
    for label, action in console_refusals:
        expect_boundary_refusal(label, action)

    pid_bytes = ctypes.sizeof(ctypes.c_int)
    if _decode_exact_uid_pid_list(
        query_byte_count=3 * pid_bytes,
        returned_byte_count=2 * pid_bytes,
        buffer_byte_count=67 * pid_bytes,
        raw_pids=[77, 42],
        coordinator_pid=42,
    ) != [42, 77]:
        raise CaptureRefusal("Offline UID process ordering regression failed.")

    pid_refusals: tuple[tuple[str, Callable[[], None]], ...] = (
        ("zero query bytes", lambda: _decode_exact_uid_pid_list(
            query_byte_count=0, returned_byte_count=pid_bytes,
            buffer_byte_count=65 * pid_bytes, raw_pids=[42], coordinator_pid=42)),
        ("misaligned query bytes", lambda: _decode_exact_uid_pid_list(
            query_byte_count=pid_bytes + 1, returned_byte_count=pid_bytes,
            buffer_byte_count=65 * pid_bytes, raw_pids=[42], coordinator_pid=42)),
        ("buffer without headroom", lambda: _decode_exact_uid_pid_list(
            query_byte_count=pid_bytes, returned_byte_count=pid_bytes,
            buffer_byte_count=pid_bytes, raw_pids=[42], coordinator_pid=42)),
        ("zero returned bytes", lambda: _decode_exact_uid_pid_list(
            query_byte_count=pid_bytes, returned_byte_count=0,
            buffer_byte_count=65 * pid_bytes, raw_pids=[], coordinator_pid=42)),
        ("misaligned returned bytes", lambda: _decode_exact_uid_pid_list(
            query_byte_count=pid_bytes, returned_byte_count=pid_bytes + 1,
            buffer_byte_count=65 * pid_bytes, raw_pids=[42], coordinator_pid=42)),
        ("saturated PID buffer", lambda: _decode_exact_uid_pid_list(
            query_byte_count=pid_bytes, returned_byte_count=65 * pid_bytes,
            buffer_byte_count=65 * pid_bytes, raw_pids=list(range(1, 66)),
            coordinator_pid=42)),
        ("oversized returned bytes", lambda: _decode_exact_uid_pid_list(
            query_byte_count=pid_bytes, returned_byte_count=66 * pid_bytes,
            buffer_byte_count=65 * pid_bytes, raw_pids=list(range(1, 67)),
            coordinator_pid=42)),
        ("partial PID vector", lambda: _decode_exact_uid_pid_list(
            query_byte_count=2 * pid_bytes, returned_byte_count=2 * pid_bytes,
            buffer_byte_count=66 * pid_bytes, raw_pids=[42], coordinator_pid=42)),
        ("zero PID", lambda: _decode_exact_uid_pid_list(
            query_byte_count=2 * pid_bytes, returned_byte_count=2 * pid_bytes,
            buffer_byte_count=66 * pid_bytes, raw_pids=[42, 0], coordinator_pid=42)),
        ("duplicate PID", lambda: _decode_exact_uid_pid_list(
            query_byte_count=2 * pid_bytes, returned_byte_count=2 * pid_bytes,
            buffer_byte_count=66 * pid_bytes, raw_pids=[42, 42], coordinator_pid=42)),
        ("missing coordinator PID", lambda: _decode_exact_uid_pid_list(
            query_byte_count=2 * pid_bytes, returned_byte_count=2 * pid_bytes,
            buffer_byte_count=66 * pid_bytes, raw_pids=[41, 43], coordinator_pid=42)),
    )
    for label, action in pid_refusals:
        expect_boundary_refusal(label, action)

    def bsd_fixture(
        *, pid: int = 42, uid: int = 501, ruid: int = 501, svuid: int = 501
    ) -> _ProcBsdInfo:
        value = _ProcBsdInfo()
        value.pid = pid
        value.uid = uid
        value.ruid = ruid
        value.svuid = svuid
        return value

    _require_exact_bsd_identity(
        bsd_fixture(), expected_pid=42, expected_uid=501,
        require_real_and_saved_uid=True,
    )
    credential_refusals: tuple[tuple[str, Callable[[], None]], ...] = (
        ("mismatched returned PID", lambda: _require_exact_bsd_identity(
            bsd_fixture(pid=43), expected_pid=42, expected_uid=501,
            require_real_and_saved_uid=False)),
        ("mismatched effective UID", lambda: _require_exact_bsd_identity(
            bsd_fixture(uid=502), expected_pid=42, expected_uid=501,
            require_real_and_saved_uid=False)),
        ("mismatched real UID", lambda: _require_exact_bsd_identity(
            bsd_fixture(ruid=502), expected_pid=42, expected_uid=501,
            require_real_and_saved_uid=True)),
        ("mismatched saved UID", lambda: _require_exact_bsd_identity(
            bsd_fixture(svuid=502), expected_pid=42, expected_uid=501,
            require_real_and_saved_uid=True)),
    )
    for label, action in credential_refusals:
        expect_boundary_refusal(label, action)

    native_window_regressions = 0
    if sys.platform == "darwin":
        regression_source = """
import importlib.util
import pathlib
import sys
tool = pathlib.Path(sys.argv[1])
spec = importlib.util.spec_from_file_location("id65v3_native_window_regression", tool)
module = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = module
spec.loader.exec_module(module)
try:
    module._copy_exact_process_window(2147483647)
except module.CaptureRefusal:
    print("ID65_V3_NATIVE_WINDOW_REFUSAL_OK")
else:
    raise SystemExit("nonmatching PID unexpectedly owned an eligible window")
"""
        try:
            completed = subprocess.run(
                [
                    sys.executable,
                    "-I",
                    "-B",
                    "-c",
                    regression_source,
                    str(repository_root / CAPTURE_COORDINATOR_RELATIVE_PATH),
                ],
                stdin=subprocess.DEVNULL,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                env={"LANG": "C", "LC_ALL": "C", "PATH": "/usr/bin:/bin"},
                check=False,
                close_fds=True,
                timeout=15.0,
            )
        except subprocess.TimeoutExpired as exc:
            raise CaptureRefusal("Native window regression timed out.") from exc
        if (
            completed.returncode != 0
            or completed.stdout.strip() != b"ID65_V3_NATIVE_WINDOW_REFUSAL_OK"
        ):
            raise CaptureRefusal(
                "Native window regression did not exit through clean CaptureRefusal."
            )
        native_window_regressions = 1

    with tempfile.TemporaryDirectory(prefix="id65v3-capture-smoke-") as temporary:
        root = pathlib.Path(os.path.realpath(temporary))
        os.chmod(root, 0o700)
        coordinator, run_id, paths = _build_offline_capture_run(
            root, coordinator_identity.sha256
        )
        state = coordinator._load_run(run_id)
        if state.session_count != len(SESSION_IDS):
            raise CaptureRefusal("Offline seven-session run did not verify completely.")

        tied_root = root / "within-session-utc-ties"
        tied_root.mkdir(mode=0o700)
        tied_coordinator, tied_run_id, _ = _build_offline_capture_run(
            tied_root,
            coordinator_identity.sha256,
            exercise_within_session_utc_ties=True,
        )
        tied_state = tied_coordinator._load_run(tied_run_id)
        if tied_state.session_count != len(SESSION_IDS):
            raise CaptureRefusal(
                "Equal-millisecond within-session UTC fixture was not accepted."
            )

        writer_clock_root = root / "writer-clock-regression"
        writer_clock_root.mkdir(mode=0o700)
        writer_clocks = iter((
            ClockSample("2026-08-10T18:58:00.000Z", 1_000_000),
            ClockSample("2026-08-10T18:58:00.000Z", 1_000_001),
        ))
        writer = JournalWriter(
            writer_clock_root,
            {
                "bootIdentity": sha256_bytes(b"writer-clock-boot"),
                "format": FORMAT,
                "formatVersion": JOURNAL_FORMAT_VERSION,
                "processInstanceId": "id65v3-process-" + "1" * 32,
                "runOriginSha256": sha256_bytes(b"writer-clock-origin"),
                "runId": "id65v3-20260810t205800z-1111111111111111",
                "sessionId": SESSION_IDS[0],
                "sessionOrdinal": 0,
                **_offline_capture_pins(coordinator_identity.sha256),
            },
            ZERO_SHA256,
            lambda: next(writer_clocks),
        )
        writer.append("synthetic", {})
        before_events = tuple(writer.events)
        before_hash = writer.previous_hash
        before_live = (
            writer_clock_root / "capture-events-live.ndjson"
        ).read_bytes()
        try:
            writer.append(
                "synthetic",
                {},
                ClockSample("2026-08-10T18:57:59.999Z", 1_000_001),
            )
        except CaptureRefusal:
            pass
        else:
            raise CaptureRefusal("Journal writer accepted a UTC regression.")
        if (
            tuple(writer.events) != before_events
            or writer.previous_hash != before_hash
            or (writer_clock_root / "capture-events-live.ndjson").read_bytes()
            != before_live
        ):
            raise CaptureRefusal("Journal writer mutated state after a clock refusal.")
        writer.abort("clock-regression")

        refusal_count = 0

        def expect_refusal(
            label: str,
            action: Callable[[], None],
            expected_fragment: str | None = None,
        ) -> None:
            nonlocal refusal_count
            try:
                action()
            except CaptureRefusal as exc:
                if expected_fragment is not None and expected_fragment not in str(exc):
                    raise CaptureRefusal(
                        f"Offline tamper {label!r} reached the wrong refusal: {exc}"
                    ) from exc
                refusal_count += 1
                return
            raise CaptureRefusal(f"Offline tamper was accepted: {label}")

        first_session = SESSION_IDS[0]
        second_session = SESSION_IDS[1]
        first_journal_path = paths[f"journal:{first_session}"]
        first_live_path = paths[f"live:{first_session}"]
        original_journal = first_journal_path.read_bytes()
        original_live = first_live_path.read_bytes()

        def journal_tamper(
            label: str,
            mutate: Callable[[dict[str, Any]], None],
            expected_fragment: str | None = None,
        ) -> None:
            journal = _decode_canonical_json_bytes(original_journal, "offline journal")
            mutate(journal)
            _offline_rechain(journal, ZERO_SHA256)
            _offline_write_existing_journal(first_journal_path, first_live_path, journal)
            try:
                expect_refusal(
                    label,
                    lambda: coordinator._load_run(run_id),
                    expected_fragment,
                )
            finally:
                first_journal_path.write_bytes(original_journal)
                first_live_path.write_bytes(original_live)
                os.chmod(first_journal_path, 0o600)
                os.chmod(first_live_path, 0o600)

        journal_tamper(
            "legacy v1 journal format",
            lambda journal: journal.update({"formatVersion": 1}),
            "Capture journal header identity changed",
        )
        journal_tamper(
            "failed v1 exclusion false",
            lambda journal: next(
                event
                for event in journal["events"]
                if event["eventType"] == "disc-open-observed"
            )["payload"].update({"failedV1Excluded": False}),
            "Disc observation changed its exact pinned identity",
        )
        journal_tamper(
            "CUE repo-relative path drift",
            lambda journal: next(
                event
                for event in journal["events"]
                if event["eventType"] == "disc-open-observed"
            )["payload"].update({"cueRepoRelativePath": "wrong/candidate.cue"}),
            "Disc observation changed its exact pinned identity",
        )
        journal_tamper(
            "BIN repo-relative path drift",
            lambda journal: next(
                event
                for event in journal["events"]
                if event["eventType"] == "disc-open-observed"
            )["payload"].update({"binRepoRelativePath": "wrong/candidate.bin"}),
            "Disc observation changed its exact pinned identity",
        )

        def reorder_process_and_disc_events(journal: dict[str, Any]) -> None:
            events = journal["events"]
            process_index = next(
                index for index, event in enumerate(events)
                if event["eventType"] == "process-start-observed"
            )
            disc_index = next(
                index for index, event in enumerate(events)
                if event["eventType"] == "disc-open-observed"
            )
            events[process_index], events[disc_index] = (
                events[disc_index], events[process_index]
            )

        journal_tamper(
            "fully rechained event-array reorder",
            reorder_process_and_disc_events,
            "Disc observation is not bound to the process observation",
        )

        def second_media_event(journal: dict[str, Any]) -> dict[str, Any]:
            return [
                event for event in journal["events"]
                if event["eventType"] == "media-artifact-bound"
            ][1]

        def first_media_event(journal: dict[str, Any]) -> dict[str, Any]:
            return [
                event for event in journal["events"]
                if event["eventType"] == "media-artifact-bound"
            ][0]

        journal_tamper(
            "within-session UTC regression",
            lambda journal: second_media_event(journal).update({
                "atUtc": format_utc(
                    parse_utc(first_media_event(journal)["atUtc"])
                    - dt.timedelta(milliseconds=1)
                )
            }),
            "Capture journal clocks diverged",
        )
        journal_tamper(
            "within-session monotonic equality",
            lambda journal: second_media_event(journal).update({
                "monotonicMicroseconds": first_media_event(journal)[
                    "monotonicMicroseconds"
                ]
            }),
            "Capture journal clocks diverged",
        )
        journal_tamper(
            "within-session monotonic regression",
            lambda journal: second_media_event(journal).update({
                "monotonicMicroseconds": first_media_event(journal)[
                    "monotonicMicroseconds"
                ] - 1
            }),
            "Capture journal clocks diverged",
        )

        def swap_watcher_and_resume_bodies(journal: dict[str, Any]) -> None:
            watcher = next(
                event for event in journal["events"]
                if event["eventType"] == "watcher-connected"
            )
            resumed_event = next(
                event for event in journal["events"]
                if event["eventType"] == "manually-resumed"
            )
            watcher["eventType"], resumed_event["eventType"] = (
                resumed_event["eventType"], watcher["eventType"]
            )
            watcher["payload"], resumed_event["payload"] = (
                resumed_event["payload"], watcher["payload"]
            )

        journal_tamper(
            "fully rechained tied-clock FSM reorder",
            swap_watcher_and_resume_bodies,
            "exact per-session FSM",
        )

        journal_tamper(
            "unknown event type",
            lambda journal: journal["events"][7].update(
                {"eventType": "selector-started-unknown"}
            ),
        )
        journal_tamper(
            "wrong source pin",
            lambda journal: journal.update({"sourceClosureSha256": ZERO_SHA256}),
        )
        journal_tamper(
            "wrong marker FSM",
            lambda journal: journal["events"][8]["payload"].update(
                {"markerId": "candidate.wrong-landing"}
            ),
        )
        journal_tamper(
            "broken watcher marker reference",
            lambda journal: next(
                event
                for event in journal["events"]
                if event["eventType"] == "watcher-marker-bound"
            )["payload"].update({"sampleOrdinal": 0}),
        )
        journal_tamper(
            "recorder PID lifecycle",
            lambda journal: next(
                event
                for event in journal["events"]
                if event["eventType"] == "recording-stopped"
            )["payload"].update({"ownedRecorderPid": 99_999}),
        )
        journal_tamper(
            "process start epoch projection",
            lambda journal: next(
                event
                for event in journal["events"]
                if event["eventType"] == "process-start-observed"
            )["payload"].update({"processStartedAtUtc": "2026-08-10T19:00:00.101Z"}),
        )
        journal_tamper(
            "false CUE-open claim",
            lambda journal: next(
                event
                for event in journal["events"]
                if event["eventType"] == "disc-open-observed"
            )["payload"].update({"cueOpen": True}),
        )
        journal_tamper(
            "write-only disc descriptor",
            lambda journal: next(
                event
                for event in journal["events"]
                if event["eventType"] == "disc-open-observed"
            )["payload"].update({"binFd": "13w"}),
        )

        def move_operator_marker(
            journal: dict[str, Any], event_type: str, milliseconds: int
        ) -> None:
            event = next(
                item for item in journal["events"]
                if item["eventType"] == event_type
            )
            event["atUtc"] = format_utc(
                parse_utc(event["atUtc"]) + dt.timedelta(milliseconds=milliseconds)
            )
            event["monotonicMicroseconds"] += milliseconds * 1000

        def move_fall_and_respawn_markers(journal: dict[str, Any]) -> None:
            move_operator_marker(journal, "fall-started", 200)
            move_operator_marker(journal, "respawn-marked", 200)

        journal_tamper(
            "fall watcher wrong direction",
            move_fall_and_respawn_markers,
        )
        journal_tamper(
            "respawn watcher outside reaction tolerance",
            lambda journal: move_operator_marker(journal, "respawn-marked", 300),
        )

        def bind_respawn_to_later_safe_sample(journal: dict[str, Any]) -> None:
            binding = next(
                event["payload"]
                for event in journal["events"]
                if event["eventType"] == "watcher-marker-bound"
                and event["payload"]["markerRole"] == "respawn-marked"
            )
            binding["sampleAtUtc"] = format_utc(
                parse_utc(binding["sampleAtUtc"]) + dt.timedelta(milliseconds=100)
            )
            binding["sampleElapsedMilliseconds"] += 100
            binding["sampleOrdinal"] += 1

        journal_tamper(
            "respawn watcher skipped first directional sample",
            bind_respawn_to_later_safe_sample,
        )

        coordinator_declaration = (
            b"internal const string ExpectedCaptureCoordinatorSha256"
        )
        coordinator_pin_statement = (
            coordinator_declaration
            + b' =\n        "'
            + pins["ExpectedCaptureCoordinatorSha256"].encode("ascii")
            + b'";\n'
        )
        renamed_contract = (contract_identity.bytes_value or b"").replace(
            coordinator_declaration,
            coordinator_declaration + b"Renamed",
            1,
        )
        commented_contract_pin = (
            b'// internal const string ExpectedCaptureCoordinatorSha256 = "'
            + pins["ExpectedCaptureCoordinatorSha256"].encode("ascii")
            + b'";\n'
            + renamed_contract
        )
        expect_refusal(
            "commented contract pin",
            lambda: _parse_contract_pins(commented_contract_pin),
        )
        duplicate_contract_pin = (
            coordinator_pin_statement + (contract_identity.bytes_value or b"")
        )
        expect_refusal(
            "duplicate active contract pin",
            lambda: _parse_contract_pins(duplicate_contract_pin),
        )
        inactive_contract_pin = (
            b"#if false\n"
            + coordinator_pin_statement
            + b"#endif\n"
            + renamed_contract
        )
        expect_refusal(
            "inactive preprocessor contract pin",
            lambda: _parse_contract_pins(inactive_contract_pin),
        )
        raw_string_contract_pin = (
            b'private const string Decoy = """\n'
            + coordinator_pin_statement
            + b'""";\n'
            + renamed_contract
        )
        expect_refusal(
            "raw-string contract pin",
            lambda: _parse_contract_pins(raw_string_contract_pin),
        )

        second_journal_path = paths[f"journal:{second_session}"]
        second_live_path = paths[f"live:{second_session}"]
        second_original_journal = second_journal_path.read_bytes()
        second_original_live = second_live_path.read_bytes()
        second_journal = _decode_canonical_json_bytes(
            second_original_journal, "second offline journal"
        )
        prior_terminal = _decode_canonical_json_bytes(
            original_journal, "first offline journal"
        )["events"][-1]
        second_journal["events"][0]["atUtc"] = prior_terminal["atUtc"]
        second_journal["events"][0]["monotonicMicroseconds"] = prior_terminal[
            "monotonicMicroseconds"
        ]
        _offline_rechain(second_journal, prior_terminal["eventSha256"])
        _offline_write_existing_journal(
            second_journal_path, second_live_path, second_journal
        )
        try:
            expect_refusal(
                "cross-session clock overlap", lambda: coordinator._load_run(run_id)
            )
        finally:
            second_journal_path.write_bytes(second_original_journal)
            second_live_path.write_bytes(second_original_live)
            os.chmod(second_journal_path, 0o600)
            os.chmod(second_live_path, 0o600)

        second_before_path = (
            paths[f"session:{second_session}"] / "runtime-profile-before.json"
        )
        second_before_original = second_before_path.read_bytes()
        downstream_originals = {
            session_id: (
                paths[f"journal:{session_id}"].read_bytes(),
                paths[f"live:{session_id}"].read_bytes(),
            )
            for session_id in SESSION_IDS[1:]
        }
        first_after = _decode_verifier_canonical_json_bytes(
            (
                paths[f"session:{first_session}"]
                / "runtime-profile-after.json"
            ).read_bytes(),
            "first offline after profile",
        )
        equal_clock_before = _decode_verifier_canonical_json_bytes(
            second_before_original,
            "second offline before profile",
        )
        equal_clock_before["capturedAtUtc"] = first_after["capturedAtUtc"]
        equal_clock_before_bytes = canonical_lf_bytes(equal_clock_before)
        second_before_path.write_bytes(equal_clock_before_bytes)
        os.chmod(second_before_path, 0o600)
        previous_terminal_hash = _decode_canonical_json_bytes(
            original_journal,
            "first offline journal",
        )["events"][-1]["eventSha256"]
        try:
            for downstream_session in SESSION_IDS[1:]:
                downstream_journal_path = paths[f"journal:{downstream_session}"]
                downstream_live_path = paths[f"live:{downstream_session}"]
                downstream_journal = _decode_canonical_json_bytes(
                    downstream_originals[downstream_session][0],
                    "downstream offline journal",
                )
                downstream_journal["events"][0]["payload"][
                    "previousSessionTerminalSha256"
                ] = previous_terminal_hash
                if downstream_session == second_session:
                    before_event = next(
                        item for item in downstream_journal["events"]
                        if item["eventType"] == "profile-before-captured"
                    )
                    before_event["payload"].update(
                        {
                            "byteLength": len(equal_clock_before_bytes),
                            "capturedAtUtc": equal_clock_before["capturedAtUtc"],
                            "sha256": sha256_bytes(equal_clock_before_bytes),
                        }
                    )
                _offline_rechain(downstream_journal, previous_terminal_hash)
                _offline_write_existing_journal(
                    downstream_journal_path,
                    downstream_live_path,
                    downstream_journal,
                )
                previous_terminal_hash = downstream_journal["events"][-1][
                    "eventSha256"
                ]
            expect_refusal(
                "adjacent profile timestamp equality",
                lambda: coordinator._load_run(run_id),
                "snapshot clocks",
            )
        finally:
            second_before_path.write_bytes(second_before_original)
            os.chmod(second_before_path, 0o600)
            for downstream_session, (journal_bytes, live_bytes) in (
                downstream_originals.items()
            ):
                downstream_journal_path = paths[f"journal:{downstream_session}"]
                downstream_live_path = paths[f"live:{downstream_session}"]
                downstream_journal_path.write_bytes(journal_bytes)
                downstream_live_path.write_bytes(live_bytes)
                os.chmod(downstream_journal_path, 0o600)
                os.chmod(downstream_live_path, 0o600)

        extra = paths[f"session:{first_session}"] / "unexpected.bin"
        extra.write_bytes(b"debris")
        os.chmod(extra, 0o600)
        try:
            expect_refusal("raw allowlist debris", lambda: coordinator._load_run(run_id))
        finally:
            extra.unlink()

        recording = paths[f"recording:{first_session}"]
        os.chmod(recording, 0o644)
        try:
            expect_refusal("raw artifact mode", lambda: coordinator._load_run(run_id))
        finally:
            os.chmod(recording, 0o600)

        frame = paths[f"frame:{first_session}"]
        frame_original = frame.read_bytes()
        frame.write_bytes(frame_original[:-1])
        try:
            expect_refusal("truncated frame", lambda: coordinator._load_run(run_id))
        finally:
            frame.write_bytes(frame_original)
            os.chmod(frame, 0o600)

        watcher = paths[f"watcher:{first_session}"]
        watcher_original = watcher.read_bytes()
        watcher.write_bytes(watcher_original.replace(b"lives=4", b"lives=9", 1))
        try:
            expect_refusal("watcher raw tuple", lambda: coordinator._load_run(run_id))
        finally:
            watcher.write_bytes(watcher_original)
            os.chmod(watcher, 0o600)

        first_session_path = paths[f"session:{first_session}"]
        edge_before = first_session_path / "edge-before.png"
        edge_after = first_session_path / "edge-after.png"
        edge_after_original = edge_after.read_bytes()
        duplicate_frame_bytes = edge_before.read_bytes()
        duplicate_frame_hash = sha256_bytes(duplicate_frame_bytes)
        duplicated_journal = _decode_canonical_json_bytes(
            original_journal, "same-session duplicate journal"
        )
        edge_after.write_bytes(duplicate_frame_bytes)
        for event in duplicated_journal["events"]:
            payload = event["payload"]
            if (
                event["eventType"] == "media-artifact-bound"
                and payload.get("artifactId") == f"{first_session}.edge-after"
            ):
                payload["sha256"] = duplicate_frame_hash
                payload["byteLength"] = len(duplicate_frame_bytes)
        _offline_rechain(duplicated_journal, ZERO_SHA256)
        _offline_write_existing_journal(
            first_journal_path, first_live_path, duplicated_journal
        )
        try:
            expect_refusal(
                "same-session copied frame bytes", lambda: coordinator._load_run(run_id)
            )
        finally:
            edge_after.write_bytes(edge_after_original)
            first_journal_path.write_bytes(original_journal)
            first_live_path.write_bytes(original_live)
            for path in (edge_after, first_journal_path, first_live_path):
                os.chmod(path, 0o600)

        first_recording = paths[f"recording:{first_session}"]
        second_recording = paths[f"recording:{second_session}"]
        second_recording_original = second_recording.read_bytes()
        second_journal = _decode_canonical_json_bytes(
            second_original_journal, "second offline journal"
        )
        copied = first_recording.read_bytes()
        second_recording.write_bytes(copied)
        copied_hash = sha256_bytes(copied)
        for event in second_journal["events"]:
            payload = event["payload"]
            if (
                event["eventType"] == "media-artifact-bound"
                and payload.get("kind") == "screen-recording"
            ):
                payload["sha256"] = copied_hash
                payload["byteLength"] = len(copied)
        _offline_rechain(second_journal, prior_terminal["eventSha256"])
        _offline_write_existing_journal(
            second_journal_path, second_live_path, second_journal
        )
        try:
            expect_refusal(
                "cross-session copied raw bytes", lambda: coordinator._load_run(run_id)
            )
        finally:
            second_recording.write_bytes(second_recording_original)
            second_journal_path.write_bytes(second_original_journal)
            second_live_path.write_bytes(second_original_live)
            for path in (second_recording, second_journal_path, second_live_path):
                os.chmod(path, 0o600)

        os.chmod(first_journal_path, 0o644)
        try:
            expect_refusal("sealed journal mode", lambda: coordinator._load_run(run_id))
        finally:
            os.chmod(first_journal_path, 0o600)

        origin_original = paths["origin"].read_bytes()
        origin_value = _decode_canonical_json_bytes(origin_original, "offline origin")
        origin_value["unexpected"] = True
        paths["origin"].write_bytes(canonical_bytes(origin_value))
        try:
            expect_refusal("run-origin closed schema", lambda: coordinator._load_run(run_id))
        finally:
            paths["origin"].write_bytes(origin_original)
            os.chmod(paths["origin"], 0o600)

        duplicate_root = root / "duplicate-process-fixture"
        duplicate_root.mkdir(mode=0o700)
        duplicate_coordinator, duplicate_run_id, _ = _build_offline_capture_run(
            duplicate_root,
            coordinator_identity.sha256,
            reuse_first_process_fingerprint=True,
        )
        expect_refusal(
            "cross-session raw OS process fingerprint reuse",
            lambda: duplicate_coordinator._load_run(duplicate_run_id),
        )

        projected_root = root / "projected-terminal-fixture"
        projected_root.mkdir(mode=0o700)
        projected_header = {
            "bootIdentity": sha256_bytes(b"projected-terminal-boot"),
            "format": FORMAT,
            "formatVersion": JOURNAL_FORMAT_VERSION,
            "processInstanceId": "id65v3-process-" + "1" * 32,
            "runOriginSha256": sha256_bytes(b"projected-terminal-origin"),
            "runId": "id65v3-20260810t210000z-1111111111111111",
            "sessionId": SESSION_IDS[0],
            "sessionOrdinal": 0,
            **_offline_capture_pins(coordinator_identity.sha256),
        }
        projected_clock = iter(
            (
                ClockSample("2026-08-10T21:00:00.000Z", 1_000_000),
                ClockSample("2026-08-10T21:00:00.100Z", 1_100_000),
                ClockSample("2026-08-10T21:00:00.200Z", 1_200_000),
            )
        )
        projected_writer = JournalWriter(
            projected_root,
            projected_header,
            ZERO_SHA256,
            lambda: next(projected_clock),
        )
        projected_writer.append(
            "session-open", {"previousSessionTerminalSha256": ZERO_SHA256}
        )
        _ = projected_writer.project_terminal(
            {"rawArtifactIds": []},
            ClockSample("2026-08-10T21:00:00.100Z", 1_100_000),
        )
        projected_writer.abort("simulated-preseal-validation")
        projected_live = (
            projected_root / "capture-events-live.ndjson"
        ).read_bytes()
        if (
            b'"eventType":"session-sealed"' in projected_live
            or b'"eventType":"capture-aborted"' not in projected_live
            or (projected_root / "capture-event-journal.json").exists()
        ):
            raise CaptureRefusal(
                "Projected terminal failure did not remain an unsealed abort."
            )

        locked_root = root / "locked-preflight"
        locked_root.mkdir(mode=0o700)

        class RefusingBoundary:
            repository_root = locked_root

            @staticmethod
            def require_capture_environment() -> None:
                raise CaptureRefusal("simulated locked display")

        locked = CaptureCoordinator(
            RefusingBoundary(),
            source_guard=object(),
            staging=AnchoredStaging(locked_root),
        )
        before_names = tuple(locked_root.iterdir())
        expect_refusal(
            "side-effect-free locked preflight",
            lambda: locked.capture_session(SESSION_IDS[0]),
        )
        if tuple(locked_root.iterdir()) != before_names:
            raise CaptureRefusal("Locked preflight created filesystem state.")

        lease_root = root / "lease"
        lease_root.mkdir(mode=0o700)
        with CoordinatorLease(lease_root):
            expect_refusal(
                "exclusive coordinator lease",
                lambda: CoordinatorLease(lease_root).__enter__(),
            )

        if refusal_count != 37:
            raise CaptureRefusal(
                f"Offline refusal matrix was incomplete: {refusal_count}."
            )

    print(
        "ID65_V3_CAPTURE_COORDINATOR_SELF_TEST_OK "
        f"sessions={len(SESSION_IDS)} refusals={refusal_count} "
        f"recording-duration-refusals={recording_duration_refusals} "
        f"assisted-landing-refusals={assisted_landing_refusals} "
        f"mac-boundary-refusals={boundary_refusals} "
        f"native-window={native_window_regressions} "
        f"coordinator={coordinator_identity.sha256} "
        f"source={pins['ExpectedSourceClosureSha256']}"
    )


def _require_exact_console_session(
    plist_value: Any, *, expected_uid: int, expected_name: str
) -> None:
    """Accept only one exact unlocked, logged-in, on-console user session."""
    if (
        type(expected_uid) is not int
        or expected_uid <= 0
        or type(expected_name) is not str
        or not expected_name
    ):
        raise CaptureRefusal("The expected console-user identity is invalid.")
    if type(plist_value) is list:
        if len(plist_value) != 1 or type(plist_value[0]) is not dict:
            raise CaptureRefusal("The console-session root array is ambiguous.")
        root = plist_value[0]
    elif type(plist_value) is dict:
        root = plist_value
    else:
        raise CaptureRefusal("The console-session root has the wrong type.")
    if root.get("IOConsoleLocked") is not False:
        raise CaptureRefusal("The console display is locked.")
    users = root.get("IOConsoleUsers")
    if type(users) is not list:
        raise CaptureRefusal("The active console-user list is unavailable.")
    on_console: list[dict[str, Any]] = []
    for item in users:
        if type(item) is not dict:
            raise CaptureRefusal("A console-user entry has the wrong type.")
        value = item.get("kCGSSessionOnConsoleKey")
        if value is not True and value is not False:
            raise CaptureRefusal("A console-user entry has an invalid on-console flag.")
        if value is True:
            on_console.append(item)
    if len(on_console) != 1:
        raise CaptureRefusal("There is not one exact on-console session.")
    active = on_console[0]
    if active.get("kCGSessionLoginDoneKey") is not True:
        raise CaptureRefusal("The on-console session is not exactly logged in.")
    session_uid = active.get("kCGSSessionUserIDKey")
    if type(session_uid) is not int or session_uid != expected_uid:
        raise CaptureRefusal("The on-console session has the wrong user ID.")
    session_name = active.get("kCGSSessionUserNameKey")
    if type(session_name) is not str or session_name != expected_name:
        raise CaptureRefusal("The on-console session has the wrong user name.")
    if (
        "CGSSessionScreenIsLocked" in active
        and active["CGSSessionScreenIsLocked"] is not False
    ):
        raise CaptureRefusal("The on-console user session is locked.")


def _decode_exact_uid_pid_list(
    *,
    query_byte_count: int,
    returned_byte_count: int,
    buffer_byte_count: int,
    raw_pids: Sequence[int],
    coordinator_pid: int,
) -> list[int]:
    pid_bytes = ctypes.sizeof(ctypes.c_int)
    if (
        type(query_byte_count) is not int
        or query_byte_count <= 0
        or query_byte_count % pid_bytes != 0
    ):
        raise CaptureRefusal("UID process enumeration returned an invalid query byte count.")
    if (
        type(buffer_byte_count) is not int
        or buffer_byte_count <= query_byte_count
        or buffer_byte_count % pid_bytes != 0
    ):
        raise CaptureRefusal("UID process enumeration has an invalid buffer byte count.")
    if (
        type(returned_byte_count) is not int
        or returned_byte_count <= 0
        or returned_byte_count % pid_bytes != 0
        or returned_byte_count > buffer_byte_count
    ):
        raise CaptureRefusal("UID process enumeration returned an invalid result byte count.")
    if returned_byte_count == buffer_byte_count:
        raise CaptureRefusal("UID process enumeration saturated its buffer.")
    expected_count = returned_byte_count // pid_bytes
    if len(raw_pids) != expected_count:
        raise CaptureRefusal("UID process enumeration returned a partial PID vector.")
    if any(type(pid) is not int or pid <= 0 for pid in raw_pids):
        raise CaptureRefusal("UID process enumeration returned an invalid PID.")
    if len(set(raw_pids)) != len(raw_pids):
        raise CaptureRefusal("UID process enumeration returned duplicate PIDs.")
    if type(coordinator_pid) is not int or raw_pids.count(coordinator_pid) != 1:
        raise CaptureRefusal("UID process enumeration omitted the capture coordinator.")
    return sorted(raw_pids)


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


def _require_exact_bsd_identity(
    value: _ProcBsdInfo,
    *,
    expected_pid: int,
    expected_uid: int,
    require_real_and_saved_uid: bool,
) -> None:
    if int(value.pid) != expected_pid:
        raise CaptureRefusal("libproc returned a mismatched process ID.")
    if int(value.uid) != expected_uid:
        raise CaptureRefusal("libproc returned a process outside the effective UID.")
    if require_real_and_saved_uid and (
        int(value.ruid) != expected_uid or int(value.svuid) != expected_uid
    ):
        raise CaptureRefusal("DuckStation has mismatched effective, real, or saved user IDs.")


class RealMacBoundary:
    PROC_UID_ONLY = 4
    PROC_PIDTBSDINFO = 3
    CTL_KERN = 1
    KERN_PROCARGS2 = 49

    def __init__(self, repository_root: pathlib.Path) -> None:
        if sys.platform != "darwin":
            raise CaptureRefusal("The frozen capture coordinator is macOS-only.")
        self.repository_root = pathlib.Path(os.path.abspath(repository_root))
        self._libproc = ctypes.CDLL("/usr/lib/libproc.dylib", use_errno=True)
        self._libc = ctypes.CDLL("/usr/lib/libSystem.B.dylib", use_errno=True)
        self._libproc.proc_listpids.argtypes = (
            ctypes.c_uint32, ctypes.c_uint32, ctypes.c_void_p, ctypes.c_int
        )
        self._libproc.proc_listpids.restype = ctypes.c_int
        self._libproc.proc_pidinfo.argtypes = (
            ctypes.c_int, ctypes.c_int, ctypes.c_uint64, ctypes.c_void_p,
            ctypes.c_int,
        )
        self._libproc.proc_pidinfo.restype = ctypes.c_int
        self._last_monotonic = -1

    def clock(self) -> ClockSample:
        monotonic = time.monotonic_ns() // 1000
        if monotonic <= self._last_monotonic:
            monotonic = self._last_monotonic + 1
        self._last_monotonic = monotonic
        return ClockSample(format_utc(dt.datetime.now(dt.timezone.utc)), monotonic)

    def boot_identity(self) -> str:
        session_a_raw = self._sysctlbyname_bytes("kern.bootsessionuuid")
        boot_time_a_raw = self._sysctlbyname_bytes("kern.boottime")
        platform_a_raw = self._run_fixed(
            list(BOOT_PLATFORM_IOREG_ARGV), maximum_stdout=4 * 1024 * 1024
        ).stdout
        platform_b_raw = self._run_fixed(
            list(BOOT_PLATFORM_IOREG_ARGV), maximum_stdout=4 * 1024 * 1024
        ).stdout
        boot_time_b_raw = self._sysctlbyname_bytes("kern.boottime")
        session_b_raw = self._sysctlbyname_bytes("kern.bootsessionuuid")

        session_a = _parse_boot_session_uuid(session_a_raw)
        boot_time_a = _parse_boot_time(boot_time_a_raw)
        platform_a = _parse_platform_identity(platform_a_raw)
        platform_b = _parse_platform_identity(platform_b_raw)
        boot_time_b = _parse_boot_time(boot_time_b_raw)
        session_b = _parse_boot_session_uuid(session_b_raw)
        if (
            session_a != session_b
            or boot_time_a != boot_time_b
            or platform_a != platform_b
        ):
            raise CaptureRefusal(
                "Boot or platform identity changed across the mirrored read."
            )
        return _boot_machine_identity_sha256(
            session_a,
            boot_time_a[0],
            boot_time_a[1],
            platform_a[0],
            platform_a[1],
        )

    def require_capture_environment(self) -> None:
        effective_uid = os.geteuid()
        if effective_uid == 0:
            raise CaptureRefusal("Capture may not run as root.")
        if os.getuid() != effective_uid:
            raise CaptureRefusal("Capture requires matching real and effective user IDs.")
        if any(name in os.environ for name in ("SSH_CONNECTION", "SSH_CLIENT", "SSH_TTY")):
            raise CaptureRefusal("Capture requires the local console, not SSH.")
        completed = self._run_fixed(
            ["/usr/sbin/ioreg", "-n", "Root", "-d1", "-a"],
            maximum_stdout=4 * 1024 * 1024,
        )
        try:
            plist = plistlib.loads(completed.stdout)
        except Exception as exc:
            raise CaptureRefusal("Cannot decode the console-session state.") from exc
        try:
            account = pwd.getpwuid(effective_uid)
        except KeyError as exc:
            raise CaptureRefusal("The effective console-user account is unavailable.") from exc
        if account.pw_uid != effective_uid or not account.pw_name:
            raise CaptureRefusal("The effective console-user account is ambiguous.")
        _require_exact_console_session(
            plist,
            expected_uid=effective_uid,
            expected_name=account.pw_name,
        )
        core_graphics = ctypes.CDLL(
            "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics"
        )
        core_graphics.CGPreflightScreenCaptureAccess.restype = ctypes.c_bool
        if not core_graphics.CGPreflightScreenCaptureAccess():
            raise CaptureRefusal("Screen Recording permission is not already granted.")
        display_ids = (ctypes.c_uint32 * 16)()
        count = ctypes.c_uint32()
        core_graphics.CGGetActiveDisplayList(
            16, display_ids, ctypes.byref(count)
        )
        if count.value == 0:
            raise CaptureRefusal("No active display is available.")
        core_graphics.CGDisplayIsAsleep.restype = ctypes.c_bool
        if any(core_graphics.CGDisplayIsAsleep(display_ids[index]) for index in range(count.value)):
            raise CaptureRefusal("An active display is asleep.")
        self._require_foreground_local_tty()

    @staticmethod
    def _require_foreground_local_tty() -> None:
        for stream, label in (
            (sys.stdin, "stdin"),
            (sys.stdout, "stdout"),
            (sys.stderr, "stderr"),
        ):
            if not stream.isatty():
                raise CaptureRefusal(f"Operator {label} is not a local TTY.")
            info = os.fstat(stream.fileno())
            if info.st_uid != os.getuid():
                raise CaptureRefusal(f"Operator {label} has the wrong owner.")
        if os.tcgetpgrp(sys.stdin.fileno()) != os.getpgrp():
            raise CaptureRefusal(
                "Capture coordinator is not the foreground TTY process group."
            )

    def duckstation_processes(self) -> list[ProcessObservation]:
        result = []
        for pid in self._all_pids():
            try:
                bsd = self._bsd_info(pid)
            except ProcessLookupError:
                continue
            bsd_name = bytes(bsd.name).split(b"\0", 1)[0].decode("utf-8", "replace")
            named_duckstation = "DuckStation" in bsd_name
            if named_duckstation:
                _require_exact_bsd_identity(
                    bsd,
                    expected_pid=pid,
                    expected_uid=os.geteuid(),
                    require_real_and_saved_uid=True,
                )
            try:
                path = self._pid_path(pid)
            except ProcessLookupError:
                if named_duckstation:
                    raise CaptureRefusal("A DuckStation process path could not be inspected.")
                continue
            path_is_duckstation = pathlib.Path(path).name == "DuckStation"
            if not path_is_duckstation and not named_duckstation:
                continue
            _require_exact_bsd_identity(
                bsd,
                expected_pid=pid,
                expected_uid=os.geteuid(),
                require_real_and_saved_uid=True,
            )
            result.append(self._observe_process(pid, path, bsd))
        return result

    def _observe_process(
        self, pid: int, path: str | None = None, bsd: _ProcBsdInfo | None = None
    ) -> ProcessObservation:
        bsd = bsd or self._bsd_info(pid)
        _require_exact_bsd_identity(
            bsd,
            expected_pid=pid,
            expected_uid=os.geteuid(),
            require_real_and_saved_uid=True,
        )
        path = path or self._pid_path(pid)
        expected_home = os.path.realpath(
            self.repository_root / EXTERNAL_HOME_RELATIVE_PATH
        )
        argv, environment_nul_sha256, external_home_real_path = (
            self._pid_argv_environment(pid, expected_home)
        )
        open_files = tuple(self._open_files(pid))
        identity = read_regular_file(path)
        executable_path = os.path.realpath(path)
        text_vnodes = [
            item for item in open_files
            if item.fd == "txt" and item.real_path == executable_path
        ]
        if len(text_vnodes) != 1:
            raise CaptureRefusal(
                "lsof does not bind one exact executable text vnode to DuckStation."
            )
        text_vnode = text_vnodes[0]
        if (
            text_vnode.device != identity.device
            or text_vnode.inode != identity.inode
            or text_vnode.byte_length != identity.byte_length
            or text_vnode.link_count != 1
        ):
            raise CaptureRefusal(
                "DuckStation executable path bytes differ from its open text vnode."
            )
        return ProcessObservation(
            pid=pid,
            parent_pid=bsd.ppid,
            start_seconds=bsd.start_sec,
            start_microseconds=bsd.start_usec,
            process_started_at_utc=process_start_utc(
                bsd.start_sec, bsd.start_usec
            ),
            executable=identity,
            argv=argv,
            argv_nul_sha256=sha256_bytes(
                b"\0".join(arg.encode("utf-8", "strict") for arg in argv) + b"\0"
            ),
            environment_nul_sha256=environment_nul_sha256,
            external_home_real_path=external_home_real_path,
            open_files=open_files,
        )

    def listener_pids(self) -> set[int]:
        completed = self._run_fixed(
            [
                "/usr/sbin/lsof", "-nP", f"-iTCP:{GDB_PORT}",
                "-sTCP:LISTEN", "-Fp"
            ],
            allowed_codes={0, 1},
            maximum_stdout=1024 * 1024,
        )
        return {
            int(line[1:]) for line in completed.stdout.decode("utf-8", "strict").splitlines()
            if re.fullmatch(r"p[1-9][0-9]*", line)
        }

    def wait_for_duckstation(self, cue_real_path: str) -> ProcessObservation:
        deadline = time.monotonic() + 180.0
        while time.monotonic() < deadline:
            self.require_capture_environment()
            processes = self.duckstation_processes()
            if len(processes) > 1:
                raise CaptureRefusal("More than one DuckStation process appeared.")
            if len(processes) == 1:
                process = processes[0]
                expected_bin = os.path.realpath(
                    self.repository_root / CANDIDATE_BIN_RELATIVE_PATH
                )
                if _candidate_process_lifecycle_ready(
                    process, cue_real_path, expected_bin
                ):
                    return process
            time.sleep(0.1)
        raise CaptureRefusal(
            "Exact DuckStation process with argv-bound CUE and read-only open BIN "
            "did not appear within 180 seconds."
        )

    def wait_for_process_exit(self, process: ProcessObservation) -> ClockSample:
        deadline = time.monotonic() + 180.0
        while time.monotonic() < deadline:
            self.require_capture_environment()
            try:
                observed = self._observe_process(process.pid)
            except ProcessLookupError:
                current = self.duckstation_processes()
                if current:
                    raise CaptureRefusal("Another DuckStation process appeared during exit.")
                return self.clock()
            if (
                observed.start_seconds != process.start_seconds
                or observed.start_microseconds != process.start_microseconds
                or observed.executable.sha256 != process.executable.sha256
                or observed.argv_nul_sha256 != process.argv_nul_sha256
                or observed.environment_nul_sha256 != process.environment_nul_sha256
                or observed.external_home_real_path != process.external_home_real_path
            ):
                raise CaptureRefusal("DuckStation PID identity changed during capture.")
            time.sleep(0.1)
        raise CaptureRefusal("DuckStation did not exit after the operator prompt.")

    def observe_window(self, pid: int) -> WindowObservation:
        # The fixed AppleScript-free window query is kept in one ctypes helper.
        return _copy_exact_process_window(pid)

    def confirm(self, prompt: str) -> ClockSample:
        self.require_capture_environment()
        stdin_fd = self._foreground_tty_fd("Operator confirmation")
        drained = 0
        while select.select([stdin_fd], [], [], 0.0)[0]:
            try:
                value = os.read(stdin_fd, 1)
            except OSError as exc:
                raise CaptureRefusal(
                    "Operator confirmation could not drain its TTY."
                ) from exc
            if value == b"":
                raise CaptureRefusal("Operator confirmation reached EOF while draining.")
            if value != b"\n":
                raise CaptureRefusal(
                    "Operator confirmation found queued input other than Return."
                )
            drained += 1
        if drained:
            print(
                f"QUEUED RETURNS DRAINED ({drained}) — none counted as this "
                "operator confirmation.",
                flush=True,
            )
        print(prompt, end=" ", flush=True)
        while True:
            readable, _, _ = select.select([stdin_fd], [], [], None)
            if not readable:
                continue
            try:
                value = os.read(stdin_fd, 1)
            except OSError as exc:
                raise CaptureRefusal(
                    "Operator confirmation could not read its TTY."
                ) from exc
            if value == b"":
                raise CaptureRefusal("Operator confirmation reached EOF.")
            if value != b"\n":
                raise CaptureRefusal("Operator confirmation requires an empty Return.")
            break
        self.require_capture_environment()
        return self.clock()

    def confirm_before_deadline(
        self, prompt: str, deadline_monotonic_microseconds: int
    ) -> ClockSample:
        self.require_capture_environment()
        stdin_fd = self._foreground_tty_fd("Recording confirmation")
        print(prompt, end=" ", flush=True)
        self._read_empty_return_before_deadline(
            stdin_fd,
            deadline_monotonic_microseconds,
            "Direct-window recording reached its 30-minute deadline before "
            "operator confirmation.",
        )
        self.require_capture_environment()
        confirmed = self.clock()
        if confirmed.monotonic_microseconds > deadline_monotonic_microseconds:
            raise CaptureRefusal(
                "Direct-window recording reached its 30-minute deadline before "
                "operator confirmation."
            )
        return confirmed

    def fresh_confirm_before_deadline(
        self, prompt: str, deadline_monotonic_microseconds: int
    ) -> ClockSample:
        self.require_capture_environment()
        stdin_fd = self._foreground_tty_fd("Fresh visual confirmation")
        drained = 0
        while select.select([stdin_fd], [], [], 0.0)[0]:
            try:
                value = os.read(stdin_fd, 1)
            except OSError as exc:
                raise CaptureRefusal(
                    "Fresh visual confirmation could not drain its TTY."
                ) from exc
            if value == b"":
                raise CaptureRefusal("Fresh visual confirmation reached EOF while draining.")
            if value != b"\n":
                raise CaptureRefusal(
                    "Fresh visual confirmation found queued input other than Return."
                )
            drained += 1
        if drained:
            print(
                f"QUEUED RETURNS DRAINED ({drained}) — none counted as the visual "
                "confirmation.",
                flush=True,
            )
        print(prompt, end=" ", flush=True)
        self._read_empty_return_before_deadline(
            stdin_fd,
            deadline_monotonic_microseconds,
            "Direct-window recording reached its 30-minute deadline before the "
            "fresh visual confirmation.",
        )
        self.require_capture_environment()
        confirmed = self.clock()
        if confirmed.monotonic_microseconds > deadline_monotonic_microseconds:
            raise CaptureRefusal(
                "Direct-window recording reached its 30-minute deadline before the "
                "fresh visual confirmation."
            )
        return confirmed

    def announce(self, message: str) -> None:
        if not isinstance(message, str) or not message:
            raise CaptureRefusal("Coordinator announcement is empty.")
        self.require_capture_environment()
        print(message, flush=True)
        self.require_capture_environment()

    def wait_for_fresh_return_after(
        self,
        started: ClockSample,
        minimum_microseconds: int,
        eligible_prompt: str,
        deadline_monotonic_microseconds: int | None = None,
    ) -> ClockSample:
        if minimum_microseconds <= 0:
            raise CaptureRefusal("Timed confirmation minimum is invalid.")
        self.require_capture_environment()
        stdin_fd = self._foreground_tty_fd("Timed confirmation")

        def read_empty_return(error_message: str) -> None:
            # Keep select and consumption on the same unbuffered descriptor.
            # BufferedReader.readline() may prefetch several Returns, hiding the
            # extras from select() and allowing an early Return to satisfy the
            # later eligible prompt.
            try:
                value = os.read(stdin_fd, 1)
            except OSError as exc:
                raise CaptureRefusal("Timed confirmation could not read its TTY.") from exc
            if value == b"":
                raise CaptureRefusal("Timed confirmation reached EOF.")
            if value != b"\n":
                raise CaptureRefusal(error_message)

        deadline = started.monotonic_microseconds + minimum_microseconds
        print(
            f"TIMER RUNNING — do not press Return yet; minimum "
            f"{minimum_microseconds / 1_000_000:g}s.",
            flush=True,
        )
        while True:
            now = time.monotonic_ns() // 1000
            if (
                deadline_monotonic_microseconds is not None
                and now >= deadline_monotonic_microseconds
            ):
                raise CaptureRefusal(
                    "Direct-window recording reached its 30-minute deadline during "
                    "a timed operator action."
                )
            remaining = deadline - now
            if remaining <= 0:
                break
            wait_microseconds = remaining
            if deadline_monotonic_microseconds is not None:
                wait_microseconds = min(
                    wait_microseconds,
                    deadline_monotonic_microseconds - now,
                )
            readable, _, _ = select.select(
                [stdin_fd], [], [], min(wait_microseconds / 1_000_000, 0.25)
            )
            if readable:
                read_empty_return(
                    "Timed confirmation received input other than Return."
                )
                elapsed_seconds = max(
                    0.0,
                    (time.monotonic_ns() // 1000
                     - started.monotonic_microseconds) / 1_000_000,
                )
                print(
                    f"Too early: {elapsed_seconds:.2f}/"
                    f"{minimum_microseconds / 1_000_000:.2f}s — keep going; "
                    "nothing was recorded.",
                    flush=True,
                )
        while select.select([stdin_fd], [], [], 0.0)[0]:
            read_empty_return(
                "Timed confirmation received input other than Return."
            )
            print("QUEUED RETURN DRAINED — wait for the fresh prompt.", flush=True)
        print(
            "MINIMUM MET — finish the action, then press Return once. "
            + eligible_prompt,
            end=" ",
            flush=True,
        )
        if deadline_monotonic_microseconds is None:
            read_empty_return("Timed confirmation requires a fresh empty Return.")
        else:
            self._read_empty_return_before_deadline(
                stdin_fd,
                deadline_monotonic_microseconds,
                "Direct-window recording reached its 30-minute deadline during "
                "a timed operator action.",
            )
        self.require_capture_environment()
        ended = self.clock()
        if (
            deadline_monotonic_microseconds is not None
            and ended.monotonic_microseconds > deadline_monotonic_microseconds
        ):
            raise CaptureRefusal(
                "Direct-window recording reached its 30-minute deadline during "
                "a timed operator action."
            )
        _require_minimum_interval(
            started, ended, minimum_microseconds, "Timed confirmation"
        )
        return ended

    @staticmethod
    def _foreground_tty_fd(context: str) -> int:
        try:
            stdin_fd = sys.stdin.fileno()
        except (AttributeError, OSError, ValueError) as exc:
            raise CaptureRefusal(f"{context} requires a foreground TTY.") from exc
        if not os.isatty(stdin_fd) or os.tcgetpgrp(stdin_fd) != os.getpgrp():
            raise CaptureRefusal(f"{context} requires the foreground TTY.")
        return stdin_fd

    @staticmethod
    def _read_empty_return_before_deadline(
        stdin_fd: int,
        deadline_monotonic_microseconds: int,
        timeout_message: str,
    ) -> None:
        while True:
            remaining = deadline_monotonic_microseconds - time.monotonic_ns() // 1000
            if remaining <= 0:
                raise CaptureRefusal(timeout_message)
            readable, _, _ = select.select([stdin_fd], [], [], remaining / 1_000_000)
            if not readable:
                raise CaptureRefusal(timeout_message)
            try:
                value = os.read(stdin_fd, 1)
            except OSError as exc:
                raise CaptureRefusal("Recording confirmation could not read its TTY.") from exc
            if value == b"":
                raise CaptureRefusal("Recording confirmation reached EOF.")
            if value != b"\n":
                raise CaptureRefusal("Recording confirmation requires an empty Return.")
            return

    def _start_pinned_watcher(
        self,
        stdout: BinaryIO,
        watcher_args: tuple[str, ...],
        owned_children: list[tuple[str, OwnedChild]],
    ) -> tuple[OwnedChild, BinaryIO]:
        if watcher_args not in (("--period", "0.1"), ("--self-test",)):
            raise CaptureRefusal("Pinned watcher arguments are not approved.")
        watcher = self.repository_root / WATCHER_RELATIVE_PATH
        _require_no_symlink_segments(watcher)
        watcher_fd = os.open(
            watcher, os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0)
        )
        try:
            info = os.fstat(watcher_fd)
            if (
                not stat.S_ISREG(info.st_mode)
                or info.st_nlink != 1
                or info.st_uid != os.getuid()
                or stat.S_IMODE(info.st_mode) != 0o644
                or info.st_size <= 0
                or info.st_size > MAX_PINNED_WATCHER_BYTES
            ):
                raise CaptureRefusal("Pinned watcher inode is unsafe at execution.")
            digest = hashlib.sha256()
            byte_length = 0
            while True:
                chunk = os.read(watcher_fd, 64 * 1024)
                if not chunk:
                    break
                byte_length += len(chunk)
                if byte_length > MAX_PINNED_WATCHER_BYTES:
                    raise CaptureRefusal("Pinned watcher exceeds its execution bound.")
                digest.update(chunk)
            after = os.fstat(watcher_fd)
            if (
                byte_length != info.st_size
                or (
                    after.st_dev,
                    after.st_ino,
                    after.st_mode,
                    after.st_nlink,
                    after.st_uid,
                    after.st_gid,
                    after.st_size,
                    after.st_mtime_ns,
                    after.st_ctime_ns,
                )
                != (
                    info.st_dev,
                    info.st_ino,
                    info.st_mode,
                    info.st_nlink,
                    info.st_uid,
                    info.st_gid,
                    info.st_size,
                    info.st_mtime_ns,
                    info.st_ctime_ns,
                )
                or digest.hexdigest() != EXPECTED_WATCHER_SHA256
            ):
                raise CaptureRefusal("Pinned watcher bytes changed at execution.")
            os.lseek(watcher_fd, 0, os.SEEK_SET)
            child = self._spawn_and_register_owned_child(
                lambda: subprocess.Popen(
                    [
                        "/usr/bin/python3",
                        "-I",
                        "-B",
                        "-c",
                        PINNED_WATCHER_BOOTSTRAP,
                        str(watcher_fd),
                        EXPECTED_WATCHER_SHA256,
                        str(MAX_PINNED_WATCHER_BYTES),
                        WATCHER_RELATIVE_PATH,
                        *watcher_args,
                    ],
                    stdin=subprocess.PIPE,
                    stdout=stdout,
                    stderr=subprocess.STDOUT,
                    close_fds=True,
                    pass_fds=(watcher_fd,),
                    env={
                        "LANG": "C",
                        "LC_ALL": "C",
                        "PATH": "/usr/bin:/bin",
                        "PYTHONHASHSEED": "0",
                    },
                ),
                "watcher",
                owned_children,
            )
        finally:
            os.close(watcher_fd)
        if child.stdin is None:
            raise CaptureRefusal("Pinned watcher stdin was not created.")
        return child, child.stdin

    def start_watcher(
        self,
        stdout: BinaryIO,
        owned_children: list[tuple[str, OwnedChild]],
    ) -> tuple[OwnedChild, BinaryIO]:
        return self._start_pinned_watcher(
            stdout, ("--period", "0.1"), owned_children
        )

    def start_watcher_self_test(
        self,
        stdout: BinaryIO,
        owned_children: list[tuple[str, OwnedChild]],
    ) -> tuple[OwnedChild, BinaryIO]:
        return self._start_pinned_watcher(
            stdout, ("--self-test",), owned_children
        )

    def wait_for_watcher_connection(
        self, child: OwnedChild, watcher_path: pathlib.Path
    ) -> ClockSample:
        expected = b"Connected once to 127.0.0.1:2345. DuckStation is now paused."
        deadline = time.monotonic() + 30.0
        while time.monotonic() < deadline:
            return_code = child.poll()
            if return_code is not None:
                try:
                    diagnostic_identity = read_regular_file(
                        watcher_path,
                        include_bytes=True,
                        expected_owner=os.getuid(),
                        maximum_bytes=1024 * 1024,
                    )
                    diagnostic = (
                        diagnostic_identity.bytes_value or b""
                    )[:MAX_WATCHER_STARTUP_DIAGNOSTIC_BYTES]
                    truncated = (
                        diagnostic_identity.byte_length
                        > MAX_WATCHER_STARTUP_DIAGNOSTIC_BYTES
                    )
                    diagnostic_text = repr(diagnostic)
                    if truncated:
                        diagnostic_text += " (truncated)"
                except CaptureRefusal:
                    diagnostic_text = "<unavailable>"
                raise CaptureRefusal(
                    "Pinned watcher exited before connection "
                    f"(exit status {return_code}; output prefix {diagnostic_text})."
                )
            try:
                identity = read_regular_file(
                    watcher_path,
                    include_bytes=True,
                    expected_owner=os.getuid(),
                    maximum_bytes=1024 * 1024,
                )
            except CaptureRefusal:
                time.sleep(0.05)
                continue
            if expected in (identity.bytes_value or b""):
                return self.clock()
            time.sleep(0.05)
        raise CaptureRefusal("Pinned watcher did not reach its connection prompt.")

    def _open_capture_session_directory(
        self, session_path: pathlib.Path
    ) -> tuple[int, os.stat_result]:
        absolute = pathlib.Path(os.path.abspath(session_path))
        try:
            relative = absolute.relative_to(self.repository_root).as_posix()
        except ValueError:
            raise CaptureRefusal(
                "Screencapture destination is outside the authoritative repository."
            ) from None
        parts = guard_relative_path(relative)
        prefix_parts = guard_relative_path(CAPTURE_STAGING_PREFIX)
        if len(parts) != len(prefix_parts) + 2 or parts[: len(prefix_parts)] != prefix_parts:
            raise CaptureRefusal(
                "Screencapture destination is not one exact capture session directory."
            )
        run_id, session_leaf = parts[-2:]
        if not RUN_ID_RE.fullmatch(run_id):
            raise CaptureRefusal("Screencapture destination has an invalid run ID.")
        if len(session_leaf) < 4 or session_leaf[2] != "-" or not session_leaf[:2].isdigit():
            raise CaptureRefusal("Screencapture destination has an invalid session leaf.")
        ordinal = int(session_leaf[:2], 10)
        if (
            ordinal >= len(SESSION_IDS)
            or session_leaf != f"{ordinal:02d}-{SESSION_IDS[ordinal]}"
        ):
            raise CaptureRefusal("Screencapture destination is not a fixed session.")
        directory_fd = _open_secure_directory(absolute)
        info = os.fstat(directory_fd)
        if (
            not stat.S_ISDIR(info.st_mode)
            or info.st_uid != os.getuid()
            or stat.S_IMODE(info.st_mode) != 0o700
        ):
            os.close(directory_fd)
            raise CaptureRefusal("Screencapture directory is not owned mode 0700.")
        return directory_fd, info

    def _start_screencapture(
        self,
        window: WindowObservation,
        session_path: pathlib.Path,
        destination_leaf: str,
        kind: str,
        owned_children: list[tuple[str, OwnedChild]],
        owned_label: str,
    ) -> subprocess.Popen[bytes]:
        if window.window_id <= 0:
            raise CaptureRefusal("Screencapture window ID is outside its bound.")
        if not (
            (kind == "video" and destination_leaf == SCREEN_RECORDING_LEAF)
            or (kind == "frame" and destination_leaf in CAPTURE_FRAME_LEAVES)
        ):
            raise CaptureRefusal("Screencapture destination leaf is not approved.")
        directory_fd, directory_info = self._open_capture_session_directory(
            session_path
        )
        try:
            try:
                os.stat(destination_leaf, dir_fd=directory_fd, follow_symlinks=False)
            except FileNotFoundError:
                pass
            else:
                raise CaptureRefusal(
                    "Screencapture destination already exists."
                )
            child = self._spawn_and_register_owned_child(
                lambda: subprocess.Popen(
                    [
                        "/usr/bin/python3",
                        "-I",
                        "-B",
                        "-c",
                        PINNED_SCREENCAPTURE_BOOTSTRAP,
                        str(directory_fd),
                        str(directory_info.st_dev),
                        str(directory_info.st_ino),
                        destination_leaf,
                        str(window.window_id),
                        kind,
                        "/",
                    ],
                    stdin=subprocess.DEVNULL,
                    stdout=subprocess.DEVNULL,
                    stderr=subprocess.DEVNULL,
                    cwd="/",
                    env={"LANG": "C", "LC_ALL": "C", "PATH": "/usr/bin:/bin"},
                    close_fds=True,
                    pass_fds=(directory_fd,),
                ),
                owned_label,
                owned_children,
            )
            return child
        finally:
            os.close(directory_fd)

    def start_recording(
        self,
        window: WindowObservation,
        session_path: pathlib.Path,
        destination_leaf: str,
        owned_children: list[tuple[str, OwnedChild]],
    ) -> OwnedChild:
        return self._start_screencapture(
            window,
            session_path,
            destination_leaf,
            "video",
            owned_children,
            "recorder",
        )

    def capture_frame(
        self,
        window: WindowObservation,
        session_path: pathlib.Path,
        destination_leaf: str,
        deadline_monotonic_microseconds: int,
    ) -> None:
        owned_frame: list[tuple[str, OwnedChild]] = []
        try:
            self._start_screencapture(
                window,
                session_path,
                destination_leaf,
                "frame",
                owned_frame,
                "frame capture",
            )
            if len(owned_frame) != 1:
                raise CaptureRefusal(
                    "Exact-window frame child ownership handoff is not singular."
                )
            child = owned_frame[0][1]
            remaining_microseconds = (
                deadline_monotonic_microseconds - time.monotonic_ns() // 1000
            )
            if remaining_microseconds <= 0:
                raise CaptureRefusal(
                    "Direct-window recording reached its 30-minute deadline before "
                    "frame capture."
                )
            wait_seconds = min(30.0, remaining_microseconds / 1_000_000)
            code = child.wait(timeout=wait_seconds)
            if code != 0:
                raise CaptureRefusal("Exact-window frame capture failed.")
            if time.monotonic_ns() // 1000 > deadline_monotonic_microseconds:
                raise CaptureRefusal(
                    "Direct-window recording reached its 30-minute deadline during "
                    "frame capture."
                )
        except BaseException as exc:
            cleanup_issue: BaseException | None = None
            if owned_frame:
                try:
                    cleanup_issue = self.ensure_owned_child_reaped(
                        owned_frame[0][1], "frame capture"
                    )
                except BaseException as reap_exc:
                    raise CaptureRefusal(
                        "Exact-window frame capture child could not be reaped."
                    ) from reap_exc
            if cleanup_issue is not None:
                raise CaptureRefusal(
                    "Exact-window frame capture encountered a cleanup error after "
                    "verified reap."
                ) from cleanup_issue
            if isinstance(exc, subprocess.TimeoutExpired):
                if time.monotonic_ns() // 1000 >= deadline_monotonic_microseconds:
                    raise CaptureRefusal(
                        "Direct-window recording reached its 30-minute deadline during "
                        "frame capture."
                    ) from exc
                raise CaptureRefusal("Exact-window frame capture timed out.") from exc
            raise

    @staticmethod
    def _reap_exact_child(
        child: OwnedChild,
        label: str,
        sigint_timeout: float,
        sigterm_timeout: float,
    ) -> tuple[int, bool, BaseException | None]:
        first_error: BaseException | None = None

        def remember(error: BaseException) -> None:
            nonlocal first_error
            if first_error is None:
                first_error = error

        def safe_poll() -> int | None:
            try:
                return child.poll()
            except BaseException as error:
                remember(error)
                return None

        initial = safe_poll()
        if initial is not None:
            return initial, False, first_error
        stages = (
            (signal.SIGINT, max(0.0, sigint_timeout)),
            (signal.SIGTERM, max(0.0, sigterm_timeout)),
            (signal.SIGKILL, 2.0),
        )
        for index, (requested_signal, timeout) in enumerate(stages):
            status = safe_poll()
            if status is not None:
                return status, index > 0, first_error
            try:
                child.send_signal(requested_signal)
            except BaseException as error:
                remember(error)
            try:
                child.wait(timeout=timeout)
            except subprocess.TimeoutExpired:
                pass
            except BaseException as error:
                remember(error)
            status = safe_poll()
            if status is not None:
                return status, index > 0, first_error
        raise CaptureRefusal(f"Owned {label} could not be reaped after SIGKILL.") from first_error

    def _register_owned_child(
        self,
        child: OwnedChild,
        label: str,
        owned_children: list[tuple[str, OwnedChild]],
    ) -> None:
        try:
            owned_children.append((label, child))
        except BaseException as handoff_exc:
            try:
                self._reap_exact_child(
                    child, label + " ownership handoff", 0.25, 0.25
                )
            except BaseException as reap_exc:
                raise CaptureRefusal(
                    f"Owned {label} handoff failed and its child could not be reaped."
                ) from reap_exc
            raise handoff_exc

    @staticmethod
    def _after_owned_child_spawn(child: OwnedChild, label: str) -> None:
        del child, label

    def _spawn_and_register_owned_child(
        self,
        spawn: Callable[[], subprocess.Popen[bytes]],
        label: str,
        owned_children: list[tuple[str, OwnedChild]],
    ) -> subprocess.Popen[bytes]:
        child: subprocess.Popen[bytes] | None = None
        try:
            with DeferredTerminationSignals():
                child = spawn()
                self._after_owned_child_spawn(child, label)
                self._register_owned_child(child, label, owned_children)
        except BaseException as handoff_error:
            if child is not None:
                try:
                    _, _, lifecycle_error = self._reap_exact_child(
                        child, label + " pre-registration handoff", 0.25, 0.25
                    )
                except BaseException as reap_error:
                    raise CaptureRefusal(
                        f"Owned {label} failed before registration and could not be reaped."
                    ) from reap_error
                if lifecycle_error is not None:
                    raise CaptureRefusal(
                        f"Owned {label} failed before registration with a lifecycle API error."
                    ) from lifecycle_error
            raise handoff_error
        if child is None:
            raise CaptureRefusal(f"Owned {label} spawn returned no child.")
        return child

    def ensure_owned_child_reaped(
        self, child: OwnedChild, label: str
    ) -> BaseException | None:
        _, _, first_error = self._reap_exact_child(
            child, label, 0.25, 0.25
        )
        return first_error

    def stop_owned_child(
        self,
        child: OwnedChild,
        label: str,
        deadline_monotonic_microseconds: int | None = None,
    ) -> ChildStop:
        initial_error: BaseException | None = None
        try:
            initial = child.poll()
        except BaseException as error:
            initial_error = error
            initial = None
        if initial is not None:
            raise CaptureRefusal(f"Owned {label} exited before the requested stop.")
        requested = self.clock()
        sigint_timeout = 15.0
        sigterm_timeout = 2.0
        if deadline_monotonic_microseconds is not None:
            remaining = max(
                0.0,
                (
                    deadline_monotonic_microseconds
                    - time.monotonic_ns() // 1000
                )
                / 1_000_000,
            )
            sigint_timeout = min(sigint_timeout, remaining)
            sigterm_timeout = min(sigterm_timeout, remaining)
        code, forced, first_error = self._reap_exact_child(
            child, label, sigint_timeout, sigterm_timeout
        )
        lifecycle_error = (
            initial_error if initial_error is not None else first_error
        )
        if lifecycle_error is not None:
            raise CaptureRefusal(
                f"Owned {label} encountered a lifecycle API error after verified reap."
            ) from lifecycle_error
        if forced:
            raise CaptureRefusal(
                f"Owned {label} required forced termination after SIGINT."
            )
        if code != 0:
            raise CaptureRefusal(f"Owned {label} exited with code {code}.")
        return ChildStop(requested, self.clock())

    def _all_pids(self) -> list[int]:
        effective_uid = os.geteuid()
        query_bytes = self._libproc.proc_listpids(
            self.PROC_UID_ONLY, effective_uid, None, 0
        )
        pid_bytes = ctypes.sizeof(ctypes.c_int)
        if (
            type(query_bytes) is not int
            or query_bytes <= 0
            or query_bytes % pid_bytes != 0
        ):
            raise CaptureRefusal(
                "UID process enumeration returned an invalid query byte count."
            )
        array = (ctypes.c_int * (query_bytes // pid_bytes + 64))()
        buffer_bytes = ctypes.sizeof(array)
        returned_bytes = self._libproc.proc_listpids(
            self.PROC_UID_ONLY, effective_uid, array, buffer_bytes
        )
        raw_pids = list(array[: max(returned_bytes, 0) // pid_bytes])
        return _decode_exact_uid_pid_list(
            query_byte_count=query_bytes,
            returned_byte_count=returned_bytes,
            buffer_byte_count=buffer_bytes,
            raw_pids=raw_pids,
            coordinator_pid=os.getpid(),
        )

    def _bsd_info(self, pid: int) -> _ProcBsdInfo:
        value = _ProcBsdInfo()
        size = self._libproc.proc_pidinfo(
            pid, self.PROC_PIDTBSDINFO, 0, ctypes.byref(value), ctypes.sizeof(value)
        )
        if size != ctypes.sizeof(value):
            error = ctypes.get_errno()
            if error in {0, errno.ESRCH}:
                raise ProcessLookupError(pid)
            raise CaptureRefusal(f"Cannot inspect process {pid}: errno {error}.")
        _require_exact_bsd_identity(
            value,
            expected_pid=pid,
            expected_uid=os.geteuid(),
            require_real_and_saved_uid=False,
        )
        return value

    def _pid_path(self, pid: int) -> str:
        buffer = ctypes.create_string_buffer(4096)
        size = self._libproc.proc_pidpath(pid, buffer, len(buffer))
        if size <= 0:
            error = ctypes.get_errno()
            if error in {0, errno.ENOENT, errno.ESRCH}:
                raise ProcessLookupError(pid)
            raise CaptureRefusal(f"Cannot inspect process path {pid}: errno {error}.")
        return buffer.value.decode("utf-8", "strict")

    def _pid_argv_environment(
        self,
        pid: int,
        expected_external_home: str,
        *,
        expected_environment: tuple[bytes, ...] | None = None,
    ) -> tuple[tuple[str, ...], str, str]:
        def read_once() -> bytes:
            mib = (ctypes.c_int * 3)(self.CTL_KERN, self.KERN_PROCARGS2, pid)
            size = ctypes.c_size_t()
            if self._libc.sysctl(mib, 3, None, ctypes.byref(size), None, 0) != 0:
                error = ctypes.get_errno()
                if error == errno.ESRCH:
                    raise ProcessLookupError(pid)
                raise CaptureRefusal(
                    f"Cannot inspect process argv/environment {pid}: errno {error}."
                )
            if not 4 <= size.value <= 4 * 1024 * 1024:
                raise CaptureRefusal("KERN_PROCARGS2 record has an unsafe length.")
            requested = size.value
            buffer = ctypes.create_string_buffer(requested)
            if self._libc.sysctl(mib, 3, buffer, ctypes.byref(size), None, 0) != 0:
                error = ctypes.get_errno()
                if error == errno.ESRCH:
                    raise ProcessLookupError(pid)
                raise CaptureRefusal(
                    f"Cannot read process argv/environment {pid}: errno {error}."
                )
            if not 4 <= size.value <= requested:
                raise CaptureRefusal("KERN_PROCARGS2 record changed to an unsafe length.")
            return bytes(buffer.raw[: size.value])

        first = read_once()
        second = read_once()
        if first != second:
            raise CaptureRefusal("KERN_PROCARGS2 changed across its stable mirrored read.")
        return _parse_kern_procargs2(
            first,
            expected_external_home,
            expected_environment=expected_environment,
        )

    def _open_files(self, pid: int) -> list[OpenFileObservation]:
        completed = self._run_fixed(
            ["/usr/sbin/lsof", "-n", "-F0pfadDikstn", "-a", "-p", str(pid)],
            allowed_codes={0, 1},
            maximum_stdout=16 * 1024 * 1024,
        )
        result: list[OpenFileObservation] = []
        process_seen = False
        for record in completed.stdout.split(b"\x00\n"):
            if not record:
                continue
            fields = [field for field in record.split(b"\x00") if field]
            if not fields:
                continue
            decoded = [field.decode("utf-8", "strict") for field in fields]
            if decoded[0].startswith("p"):
                if decoded[0] != f"p{pid}":
                    raise CaptureRefusal("lsof returned a different process record.")
                process_seen = True
                continue
            if not decoded[0].startswith("f"):
                raise CaptureRefusal("lsof returned an unknown record kind.")
            values = {field[0]: field[1:] for field in decoded if len(field) >= 2}
            path = values.get("n")
            if values.get("t") != "REG" or path is None or not path.startswith("/"):
                continue
            try:
                descriptor = values["f"]
                if descriptor.isdecimal():
                    access = values.get("a")
                    if access not in {"r", "w", "u"}:
                        raise CaptureRefusal(
                            "lsof numeric regular-file descriptor omits its access mode."
                        )
                    descriptor += access
                observation = OpenFileObservation(
                    descriptor,
                    int(values["D"], 0),
                    int(values["i"], 10),
                    int(values["s"], 10),
                    int(values["k"], 10),
                    os.path.realpath(path),
                )
            except (KeyError, ValueError) as exc:
                raise CaptureRefusal("lsof regular-file record is incomplete.") from exc
            result.append(observation)
        if not process_seen:
            raise CaptureRefusal("lsof omitted the requested process identity.")
        result.sort(key=lambda item: (item.real_path, item.fd))
        return result

    def _sysctlbyname_bytes(self, name: str) -> bytes:
        if name not in {"kern.bootsessionuuid", "kern.boottime"}:
            raise CaptureRefusal("Unapproved boot-identity sysctl name.")
        size = ctypes.c_size_t()
        encoded = name.encode("ascii")
        if self._libc.sysctlbyname(encoded, None, ctypes.byref(size), None, 0) != 0:
            raise CaptureRefusal(f"Cannot read {name}.")
        queried_size = size.value
        if queried_size <= 0 or queried_size > 64:
            raise CaptureRefusal(f"Invalid byte count for {name}.")
        buffer = ctypes.create_string_buffer(queried_size)
        if self._libc.sysctlbyname(
            encoded, buffer, ctypes.byref(size), None, 0
        ) != 0:
            raise CaptureRefusal(f"Cannot read {name}.")
        if size.value != queried_size:
            raise CaptureRefusal(f"Byte count changed while reading {name}.")
        return buffer.raw[:queried_size]

    @staticmethod
    def _run_fixed(
        argv: list[str],
        *,
        allowed_codes: set[int] = {0},
        maximum_stdout: int,
    ) -> subprocess.CompletedProcess[bytes]:
        allowed_programs = {"/usr/sbin/ioreg", "/usr/sbin/lsof"}
        if not argv or argv[0] not in allowed_programs:
            raise CaptureRefusal("Unapproved fixed diagnostic subprocess.")
        try:
            completed = subprocess.run(
                argv,
                stdin=subprocess.DEVNULL,
                stdout=subprocess.PIPE,
                stderr=subprocess.PIPE,
                check=False,
                close_fds=True,
                timeout=10.0,
                env={"LANG": "C", "LC_ALL": "C", "PATH": "/usr/bin:/bin"},
            )
        except subprocess.TimeoutExpired as exc:
            raise CaptureRefusal(f"Fixed diagnostic timed out: {argv[0]}") from exc
        if completed.returncode not in allowed_codes:
            raise CaptureRefusal(f"Fixed diagnostic failed: {argv[0]}")
        if len(completed.stdout) > maximum_stdout:
            raise CaptureRefusal(f"Fixed diagnostic output is oversized: {argv[0]}")
        return completed


def _copy_exact_process_window(pid: int) -> WindowObservation:
    # This helper intentionally contains no window-control API.  It reads the
    # on-screen window list and accepts one unique largest layer-zero window.
    app = ctypes.CDLL(
        "/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices"
    )
    core = ctypes.CDLL(
        "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation"
    )
    app.CGWindowListCopyWindowInfo.restype = ctypes.c_void_p
    array = app.CGWindowListCopyWindowInfo(1, 0)
    if not array:
        raise CaptureRefusal("Cannot read the on-screen window list.")
    core.CFArrayGetCount.argtypes = [ctypes.c_void_p]
    core.CFArrayGetCount.restype = ctypes.c_long
    core.CFArrayGetValueAtIndex.argtypes = [ctypes.c_void_p, ctypes.c_long]
    core.CFArrayGetValueAtIndex.restype = ctypes.c_void_p
    core.CFDictionaryGetValue.argtypes = [ctypes.c_void_p, ctypes.c_void_p]
    core.CFDictionaryGetValue.restype = ctypes.c_void_p
    core.CFNumberGetValue.argtypes = [ctypes.c_void_p, ctypes.c_int, ctypes.c_void_p]
    core.CFNumberGetValue.restype = ctypes.c_bool
    core.CFRelease.argtypes = [ctypes.c_void_p]
    core.CFRelease.restype = None

    def key(name: str) -> int:
        return ctypes.c_void_p.in_dll(app, name).value or 0

    def number(dictionary: int, key_name: str) -> int:
        value = core.CFDictionaryGetValue(dictionary, key(key_name))
        target = ctypes.c_int64()
        if not value or not core.CFNumberGetValue(value, 4, ctypes.byref(target)):
            raise CaptureRefusal(f"Window metadata omits {key_name}.")
        return int(target.value)

    class CGRect(ctypes.Structure):
        _fields_ = [
            ("x", ctypes.c_double), ("y", ctypes.c_double),
            ("width", ctypes.c_double), ("height", ctypes.c_double)
        ]

    app.CGRectMakeWithDictionaryRepresentation.argtypes = [ctypes.c_void_p, ctypes.POINTER(CGRect)]
    app.CGRectMakeWithDictionaryRepresentation.restype = ctypes.c_bool
    candidates: list[WindowObservation] = []
    try:
        for index in range(core.CFArrayGetCount(array)):
            dictionary = core.CFArrayGetValueAtIndex(array, index)
            try:
                owner = number(dictionary, "kCGWindowOwnerPID")
                layer = number(dictionary, "kCGWindowLayer")
                window_id = number(dictionary, "kCGWindowNumber")
            except CaptureRefusal:
                continue
            if owner != pid or layer != 0:
                continue
            bounds_value = core.CFDictionaryGetValue(
                dictionary, key("kCGWindowBounds")
            )
            bounds = CGRect()
            if not bounds_value or not app.CGRectMakeWithDictionaryRepresentation(
                bounds_value, ctypes.byref(bounds)
            ):
                continue
            width = int(round(bounds.width))
            height = int(round(bounds.height))
            if width < 640 or height < 480:
                continue
            candidates.append(
                WindowObservation(
                    window_id, owner, int(round(bounds.x)), int(round(bounds.y)),
                    width, height
                )
            )
    finally:
        core.CFRelease(array)
    if not candidates:
        raise CaptureRefusal("Exact DuckStation window is not visible.")
    candidates.sort(key=lambda item: item.width * item.height, reverse=True)
    if len(candidates) > 1 and (
        candidates[0].width * candidates[0].height
        == candidates[1].width * candidates[1].height
    ):
        raise CaptureRefusal("DuckStation window identity is ambiguous.")
    return candidates[0]


def _repository_root_from_script() -> pathlib.Path:
    script = pathlib.Path(__file__)
    if stat.S_ISLNK(os.lstat(script).st_mode):
        raise CaptureRefusal("Capture coordinator may not run through a symlink.")
    root = script.parent.parent
    expected = root / CAPTURE_COORDINATOR_RELATIVE_PATH
    if os.path.abspath(script) != os.path.abspath(expected):
        raise CaptureRefusal("Capture coordinator is outside its fixed repository path.")
    _require_no_symlink_segments(root)
    return pathlib.Path(os.path.abspath(root))


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    subparsers = parser.add_subparsers(dest="operation", required=True)
    subparsers.add_parser("self-test")
    subparsers.add_parser("preflight")
    capture = subparsers.add_parser("capture-session")
    capture.add_argument("session_id", choices=SESSION_IDS)
    verify = subparsers.add_parser("verify-staging")
    verify.add_argument("run_id")
    return parser


def main(argv: Sequence[str] | None = None) -> int:
    args = build_parser().parse_args(argv)
    committed_capture_run_id: str | None = None
    coordinator: CaptureCoordinator | None = None
    try:
        repository_root = _repository_root_from_script()
        if args.operation == "self-test":
            run_offline_self_test(repository_root)
            return 0
        coordinator = CaptureCoordinator(
            RealMacBoundary(repository_root)
        )
        if args.operation == "preflight":
            coordinator.preflight()
            print("ID65_V3_CAPTURE_PREFLIGHT_READY")
        elif args.operation == "capture-session":
            run_id = coordinator.capture_session(args.session_id)
            committed_capture_run_id = run_id
            print(
                f"ID65_V3_CAPTURE_SESSION_SEALED run-id={run_id}",
                flush=True,
            )
        elif args.operation == "verify-staging":
            coordinator.verify_staging(args.run_id)
            print(
                "ID65_V3_CAPTURE_STAGING_COMPLETE_VERIFIED "
                f"sessions={len(SESSION_IDS)}"
            )
        else:
            raise CaptureRefusal("Unknown capture-only operation.")
        return 0
    except BaseException as exc:
        if (
            committed_capture_run_id is not None
            or (
                coordinator is not None
                and coordinator.last_committed_run_id is not None
            )
        ):
            return 0
        if not isinstance(
            exc, (CaptureRefusal, OSError, ValueError, KeyboardInterrupt)
        ):
            raise
        print(f"REFUSED: {exc}", file=sys.stderr)
        if args.operation == "capture-session":
            print(
                "This session is unusable. Manually quit DuckStation before rerunning.",
                file=sys.stderr,
            )
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
