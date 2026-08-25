#!/usr/bin/env python3
"""Build a no-launch, external-HOME DuckStation profile for ID65 V3.

The builder copies the frozen, codesign-valid application from /Applications
without changing the source.  All mutable state lives under an external HOME
beside that copy.  Publication is atomic and no-replace.

Security boundary: this is a cooperative workflow for processes running under
the same macOS account.  Held descriptors, inode checks, a common advisory
lock, complete rescans, and quarantine close accidental and cooperative races;
they do not claim protection against a hostile same-UID process.
"""

from __future__ import annotations

import argparse
import ctypes
import dataclasses
import errno
import fcntl
import hashlib
import json
import os
import pathlib
import plistlib
import re
import secrets
import stat
import subprocess
import sys
import unicodedata
from collections.abc import Callable, Mapping
from typing import Final


SOURCE_APP_PATH: Final = pathlib.Path("/Applications/DuckStation.app")
SOURCE_APP_VERSION: Final = "0.1-11580-ge7f2f101c"
SOURCE_APP_EXECUTABLE_RELATIVE_PATH: Final = "Contents/MacOS/DuckStation"
SOURCE_APP_INFO_PLIST_RELATIVE_PATH: Final = "Contents/Info.plist"
PACKAGED_APP_LEAF: Final = "DuckStation-ID65-Unattended-v1.app"
EXPECTED_EXECUTABLE_SHA256: Final = (
    "14c22c6e1f4bbff13c00c24f17424ef8cf7645ee9917f7d65b17cd3fe1dd1645"
)
EXPECTED_EXECUTABLE_SIZE: Final = 22_354_208
APP_MANIFEST_FORMAT: Final = "spyro-id65-duckstation-app-content-manifest-v1"
EXPECTED_APP_MANIFEST_ENTRY_COUNT: Final = 292
EXPECTED_APP_MANIFEST_SHA256: Final = (
    "95a8142e561d6b37f228728abc8dbf034c774887213eb59bc14518d56c2d222e"
)

CANDIDATE_DIRECTORY: Final = pathlib.Path(
    "_local/v5-stone-hill-level-replacement/"
    "unused-level-65-remote-blank-environment-header-repair-v3"
)
CANDIDATE_STEM: Final = (
    "Unused-Level-65-Remote-Blank-Environment-Header-Repair-v3-"
    "Sector216-RUNTIME-CANDIDATE"
)
CANDIDATE_CUE_RELATIVE_PATH: Final = CANDIDATE_DIRECTORY / f"{CANDIDATE_STEM}.cue"
CANDIDATE_BIN_RELATIVE_PATH: Final = CANDIDATE_DIRECTORY / f"{CANDIDATE_STEM}.bin"
EXPECTED_CANDIDATE_CUE_SHA256: Final = (
    "ab91adb4ffcbad70bf7a226a2a4a6d0f6efe6b54fbed0b8126e59d1b8ecaf6df"
)
EXPECTED_CANDIDATE_CUE_SIZE: Final = 148
EXPECTED_CANDIDATE_BIN_SHA256: Final = (
    "42e4d5cae98d4786023bef26517d0435b43aff365b797495386813f44ecb0c45"
)
EXPECTED_CANDIDATE_BIN_SIZE: Final = 661_547_040

BIOS_PATH: Final = pathlib.Path(
    "/Users/john/Documents/DuckStation/Bios/"
    "Sony PlayStation SCPH-1001 - DTLH-3000 BIOS v2.2 "
    "(1995-12-04)(Sony)(US).bin"
)
BIOS_LEAF: Final = BIOS_PATH.name
EXPECTED_BIOS_SHA256: Final = (
    "71af94d1e47a68c11e8fdb9f8368040601514a42a5a399cda48c7d3bff1e99d3"
)
EXPECTED_BIOS_SIZE: Final = 524_288

HOME_RELATIVE_PATH: Final = pathlib.Path("home")
DATA_ROOT_RELATIVE_PATH: Final = pathlib.Path(
    "home/Library/Application Support/DuckStation"
)
SETTINGS_RELATIVE_PATH: Final = DATA_ROOT_RELATIVE_PATH / "settings.ini"
PROFILE_BIOS_RELATIVE_PATH: Final = DATA_ROOT_RELATIVE_PATH / "bios" / BIOS_LEAF
RECEIPT_LEAF: Final = "handoff-receipt.json"
RECEIPT_FORMAT: Final = "spyro-id65-unattended-external-home-handoff"
RECEIPT_VERSION: Final = 1
COMMON_LOCK_LEAF: Final = ".spyro-id65-unattended-profile.lock"
SAFETY_DIRECTORY_NAMES: Final = (
    "cheats",
    "gamesettings",
    "memcards",
    "patches",
    "savestates",
)

CANONICAL_SETTINGS: Final = (
    "[Main]\n"
    "ConfirmPowerOff = false\n"
    "CreateSaveStateBackups = false\n"
    "DisableBackgroundInput = true\n"
    "EmulationSpeed = 1\n"
    "LoadDevicesFromSaveStates = false\n"
    "PauseOnControllerDisconnection = false\n"
    "PauseOnFocusLoss = false\n"
    "RewindEnable = false\n"
    "RunaheadFrameCount = 0\n"
    "SaveStateOnExit = false\n"
    "StartPaused = false\n\n"
    "[BIOS]\n"
    "FastForwardBoot = false\n"
    "PatchFastBoot = false\n"
    "SearchDirectory = bios\n\n"
    "[CDROM]\n"
    "LoadImagePatches = false\n"
    "LoadImageToRAM = false\n"
    "ReadSpeedup = 1\n"
    "SeekSpeedup = 1\n\n"
    "[MemoryCards]\n"
    "Card1Type = None\n"
    "Card2Type = None\n"
    "Directory = memcards\n\n"
    "[TextureReplacements]\n"
    "AlwaysTrackUploads = false\n"
    "ConvertCopiesToWrites = false\n"
    "DumpC16Textures = false\n"
    "DumpFullTexturePages = false\n"
    "DumpReplacedTextures = false\n"
    "DumpTextureForceAlphaChannel = false\n"
    "DumpTexturePages = false\n"
    "DumpTextures = false\n"
    "DumpVRAMWriteForceAlphaChannel = false\n"
    "DumpVRAMWrites = false\n"
    "EnableTextureReplacements = false\n"
    "EnableVRAMWriteReplacements = false\n"
    "PreloadTextures = false\n"
    "ReplacementScaleLinearFilter = false\n\n"
    "[InputSources]\n"
    "RawInput = false\n"
    "SDL = false\n"
    "SDLControllerEnhancedMode = false\n"
    "SDLPS5PlayerLED = false\n"
    "XInput = false\n\n"
    "[ControllerPorts]\n"
    "MultitapMode = Disabled\n\n"
    "[Pad1]\n"
    "Circle = Keyboard/Z\n"
    "Cross = Keyboard/X\n"
    "Down = Keyboard/Down\n"
    "L1 = Keyboard/Q\n"
    "L2 = Keyboard/E\n"
    "Left = Keyboard/Left\n"
    "Macro1 = Keyboard/1\n"
    "Macro1Binds = R1\n"
    "Macro1Toggle = true\n"
    "Macro2 = Keyboard/2\n"
    "Macro2Binds = R2\n"
    "Macro2Toggle = true\n"
    "Macro3 = Keyboard/3\n"
    "Macro3Binds = L1\n"
    "Macro3Toggle = true\n"
    "Macro4 = Keyboard/4\n"
    "Macro4Binds = L2\n"
    "Macro4Toggle = true\n"
    "Macro5 = Keyboard/5\n"
    "Macro5Binds = Left\n"
    "Macro5Toggle = true\n"
    "Macro6 = Keyboard/6\n"
    "Macro6Binds = Down\n"
    "Macro6Toggle = true\n"
    "R1 = Keyboard/W\n"
    "R2 = Keyboard/R\n"
    "Right = Keyboard/Right\n"
    "Select = Keyboard/Backspace\n"
    "Square = Keyboard/S\n"
    "Start = Keyboard/P\n"
    "Triangle = Keyboard/A\n"
    "Type = DigitalController\n"
    "Up = Keyboard/Up\n\n"
    + "".join(f"[Pad{ordinal}]\nType = None\n\n" for ordinal in range(2, 9))
    + "[Hotkeys]\n"
    "PowerOff = Keyboard/F12\n"
    "TogglePause = Keyboard/Space\n\n"
    "[PIO]\n"
    "DeviceType = None\n"
    "FlashImageWriteEnable = false\n\n"
    "[PCDrv]\n"
    "EnableWrites = false\n"
    "Enabled = false\n\n"
    "[Debug]\n"
    "EnableGDBServer = true\n"
    "GDBServerPort = 2345\n"
).encode("utf-8")
CANONICAL_SETTINGS_SHA256: Final = hashlib.sha256(CANONICAL_SETTINGS).hexdigest()

SOURCE_APP_ALLOWED_XATTRS: Final = frozenset({
    "com.apple.macl",
    "com.apple.provenance",
    "com.apple.quarantine",
})
SOURCE_REFERENCE_ALLOWED_XATTRS: Final = frozenset({
    *SOURCE_APP_ALLOWED_XATTRS,
    "com.apple.lastuseddate#PS",
})
PROVENANCE_XATTR: Final = "com.apple.provenance"
OUTPUT_XATTR_POLICY_VERSION: Final = "apple-provenance-11-prefix-0102-v1"
OUTPUT_PROVENANCE_SIZE: Final = 11
OUTPUT_PROVENANCE_PREFIX: Final = b"\x01\x02"
OUTPUT_TREE_MANIFEST_FORMAT: Final = "spyro-id65-output-tree-manifest-v1"
CANDIDATE_MANIFEST_FORMAT: Final = "spyro-id65-candidate-handoff-manifest-v1"
PACKAGE_ENVELOPE_FORMAT: Final = "spyro-id65-package-envelope-manifest-v1"
SELF_RECEIPT_SHA256_SENTINEL: Final = "SELF-CANONICAL-RECEIPT-CLOSURE"
MAX_RECEIPT_FIXED_POINT_ITERATIONS: Final = 16
MAX_XATTR_LIST_BYTES: Final = 64 * 1024
MAX_XATTR_VALUE_BYTES: Final = 1024 * 1024
MAX_RECEIPT_BYTES: Final = 256 * 1024
LIBC_SYMBOL_ALLOWLIST: Final = frozenset({
    "acl_free",
    "acl_get_fd_np",
    "flistxattr",
    "fgetxattr",
    "fremovexattr",
    "renameatx_np",
})
INI_SECTION_RE: Final = re.compile(rb"\A\[([A-Za-z0-9_.-]+)\]\n\Z", re.ASCII)
INI_KEY_RE: Final = re.compile(rb"\A([A-Za-z0-9_.-]+) = ([^\r\n]*)\n\Z", re.ASCII)


class ProfileRefusal(RuntimeError):
    """Fail-closed refusal before publication or during read-only verify."""


class PublishedProfileQuarantined(ProfileRefusal):
    """Publication occurred, verification failed, and that exact inode moved."""

    def __init__(self, reason: str, quarantine_leaf: str) -> None:
        super().__init__(f"{reason}; quarantined as {quarantine_leaf}")
        self.quarantine_leaf = quarantine_leaf


class PublishedProfileDurabilityUnconfirmed(ProfileRefusal):
    """Atomic publication occurred, but the parent fsync did not complete."""

    def __init__(self, destination: pathlib.Path, inode: int, reason: str) -> None:
        super().__init__(
            f"published {destination} as inode {inode}, but directory durability "
            f"is unconfirmed: {reason}"
        )
        self.destination = destination
        self.inode = inode


class PublishedProfileQuarantineFailed(ProfileRefusal):
    """Postpublication verification failed and exact quarantine was impossible."""


class PublishedProfileQuarantineDurabilityUnconfirmed(ProfileRefusal):
    """Exact quarantine rename occurred, but its parent fsync failed."""

    def __init__(
        self,
        reason: str,
        quarantine_leaf: str,
        inode: int,
        durability_reason: str,
    ) -> None:
        super().__init__(
            f"{reason}; exact inode {inode} moved to {quarantine_leaf}, but "
            f"quarantine durability is unconfirmed: {durability_reason}"
        )
        self.quarantine_leaf = quarantine_leaf
        self.inode = inode


class PrivateStageCleanupFailed(ProfileRefusal):
    """A failed build retained its exact private staging inode."""

    def __init__(
        self,
        retained_path: pathlib.Path,
        inode: int,
        cleanup_reason: str,
        prior_reason: str,
    ) -> None:
        super().__init__(
            f"private stage retained at {retained_path} as inode {inode}: "
            f"{cleanup_reason}; prior build failure: {prior_reason}"
        )
        self.retained_path = retained_path
        self.inode = inode


class PrivateStageCleanupDurabilityUnconfirmed(ProfileRefusal):
    """Private staging was removed, but its parent fsync failed."""

    def __init__(
        self,
        former_path: pathlib.Path,
        inode: int,
        durability_reason: str,
        prior_reason: str,
    ) -> None:
        super().__init__(
            f"private stage {former_path} inode {inode} was removed, but cleanup "
            f"durability is unconfirmed: {durability_reason}; prior build failure: "
            f"{prior_reason}"
        )
        self.former_path = former_path
        self.inode = inode


@dataclasses.dataclass(frozen=True)
class BuildPins:
    app_version: str
    executable_sha256: str
    executable_size: int
    app_manifest_sha256: str
    app_manifest_entry_count: int
    candidate_cue_name: str
    candidate_cue_sha256: str
    candidate_cue_size: int
    candidate_bin_name: str
    candidate_bin_sha256: str
    candidate_bin_size: int
    bios_name: str
    bios_sha256: str
    bios_size: int
    settings_sha256: str
    settings_size: int


PRODUCTION_PINS: Final = BuildPins(
    app_version=SOURCE_APP_VERSION,
    executable_sha256=EXPECTED_EXECUTABLE_SHA256,
    executable_size=EXPECTED_EXECUTABLE_SIZE,
    app_manifest_sha256=EXPECTED_APP_MANIFEST_SHA256,
    app_manifest_entry_count=EXPECTED_APP_MANIFEST_ENTRY_COUNT,
    candidate_cue_name=f"{CANDIDATE_STEM}.cue",
    candidate_cue_sha256=EXPECTED_CANDIDATE_CUE_SHA256,
    candidate_cue_size=EXPECTED_CANDIDATE_CUE_SIZE,
    candidate_bin_name=f"{CANDIDATE_STEM}.bin",
    candidate_bin_sha256=EXPECTED_CANDIDATE_BIN_SHA256,
    candidate_bin_size=EXPECTED_CANDIDATE_BIN_SIZE,
    bios_name=BIOS_LEAF,
    bios_sha256=EXPECTED_BIOS_SHA256,
    bios_size=EXPECTED_BIOS_SIZE,
    settings_sha256=CANONICAL_SETTINGS_SHA256,
    settings_size=len(CANONICAL_SETTINGS),
)


@dataclasses.dataclass(frozen=True)
class ManifestEntry:
    kind: str
    mode: int
    link_count: int
    size: int | None
    sha256: str | None
    xattrs: tuple[str, ...]


@dataclasses.dataclass(frozen=True)
class PinnedFile:
    absolute_path: pathlib.Path
    parent_path: pathlib.Path
    parent_fd: int
    name: str
    fd: int
    device: int
    inode: int
    size: int
    sha256: str
    mode: int
    provenance_hex: str | None


@dataclasses.dataclass(frozen=True)
class BuildResult:
    destination: pathlib.Path
    receipt_sha256: str
    receipt_schema: str
    app_manifest_sha256: str
    settings_sha256: str
    launch_gate: str


@dataclasses.dataclass
class BuildHookContext:
    checkpoint: str
    parent_fd: int
    parent_path: pathlib.Path
    stage_fd: int | None
    stage_name: str | None
    destination_name: str
    candidate_cue: PinnedFile
    candidate_bin: PinnedFile


Hook = Callable[[BuildHookContext], None]
CodesignVerifier = Callable[[pathlib.Path], None]


_LIBC: Final = ctypes.CDLL(None, use_errno=True)


def _sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def _profile_bios_relative_path(pins: BuildPins) -> pathlib.Path:
    return DATA_ROOT_RELATIVE_PATH / "bios" / pins.bios_name


def _libc_symbol(name: str) -> object:
    if name not in LIBC_SYMBOL_ALLOWLIST:
        raise ProfileRefusal(f"ctypes symbol {name!r} is not allowlisted.")
    symbol = getattr(_LIBC, name, None)
    if symbol is None:
        raise ProfileRefusal(f"required libc symbol {name!r} is unavailable.")
    return symbol


def _require_absolute_normalized(path: pathlib.Path, label: str) -> pathlib.Path:
    if not path.is_absolute():
        raise ProfileRefusal(f"{label} must be absolute.")
    normalized = pathlib.Path(os.path.normpath(os.fspath(path)))
    if normalized != path:
        raise ProfileRefusal(f"{label} must be lexically normalized.")
    return normalized


def _reject_symlink_segments(path: pathlib.Path, label: str, *, include_leaf: bool) -> None:
    parts = path.parts
    current = pathlib.Path(parts[0])
    end = len(parts) if include_leaf else len(parts) - 1
    for part in parts[1:end]:
        current /= part
        try:
            metadata = os.lstat(current)
        except FileNotFoundError as exc:
            raise ProfileRefusal(f"{label} has a missing path segment.") from exc
        if stat.S_ISLNK(metadata.st_mode):
            raise ProfileRefusal(f"{label} traverses a symbolic link.")
        if current != path and not stat.S_ISDIR(metadata.st_mode):
            raise ProfileRefusal(f"{label} traverses a non-directory.")


def _require_safe_metadata(
    metadata: os.stat_result,
    label: str,
    kind: str,
    *,
    owner_required: bool = True,
    allow_group_write: bool = False,
    allow_nonzero_flags: bool = False,
) -> None:
    observed_kind = (
        "file" if stat.S_ISREG(metadata.st_mode)
        else "directory" if stat.S_ISDIR(metadata.st_mode)
        else "other"
    )
    if observed_kind != kind:
        raise ProfileRefusal(f"{label} is not a regular {kind}.")
    if owner_required and (
        metadata.st_uid != os.geteuid() or metadata.st_gid != os.getegid()
    ):
        raise ProfileRefusal(f"{label} has the wrong owner or group.")
    forbidden_write_bits = 0o002 if allow_group_write else 0o022
    if stat.S_IMODE(metadata.st_mode) & forbidden_write_bits:
        raise ProfileRefusal(f"{label} is group- or world-writable.")
    if metadata.st_mode & (stat.S_ISUID | stat.S_ISGID | stat.S_ISVTX):
        raise ProfileRefusal(f"{label} has a forbidden special mode bit.")
    if kind == "file" and metadata.st_nlink != 1:
        raise ProfileRefusal(f"{label} is hard-linked.")
    if not allow_nonzero_flags and getattr(metadata, "st_flags", 0) != 0:
        raise ProfileRefusal(f"{label} has nonzero file flags.")


def _require_no_acl_fd(fd: int, label: str) -> None:
    if sys.platform != "darwin":
        raise ProfileRefusal("ACL validation is available only on the pinned macOS host.")
    function = _libc_symbol("acl_get_fd_np")
    function.argtypes = [ctypes.c_int, ctypes.c_int]
    function.restype = ctypes.c_void_p
    ctypes.set_errno(0)
    acl = function(fd, 0x00000100)
    error_number = ctypes.get_errno()
    if acl:
        free_function = _libc_symbol("acl_free")
        free_function.argtypes = [ctypes.c_void_p]
        free_function.restype = ctypes.c_int
        free_function(acl)
        raise ProfileRefusal(f"{label} has an extended ACL.")
    if error_number != errno.ENOENT:
        raise ProfileRefusal(f"{label} ACL absence could not be proven.")


def _read_xattr_names_fd(fd: int, label: str) -> tuple[str, ...]:
    function = _libc_symbol("flistxattr")
    function.argtypes = [ctypes.c_int, ctypes.c_void_p, ctypes.c_size_t, ctypes.c_int]
    function.restype = ctypes.c_ssize_t
    size = function(fd, None, 0, 0)
    if size < 0 or size > MAX_XATTR_LIST_BYTES:
        raise ProfileRefusal(f"{label} has an unreadable or oversized xattr list.")
    if size == 0:
        return ()
    buffer = ctypes.create_string_buffer(size)
    if function(fd, buffer, size, 0) != size:
        raise ProfileRefusal(f"{label} xattr names changed during read.")
    raw_names = [item for item in buffer.raw[:size].split(b"\0") if item]
    if len(raw_names) != len(set(raw_names)):
        raise ProfileRefusal(f"{label} has duplicate xattr names.")
    try:
        names = tuple(sorted(item.decode("utf-8", "strict") for item in raw_names))
    except UnicodeDecodeError as exc:
        raise ProfileRefusal(f"{label} has a non-UTF-8 xattr name.") from exc
    return names


def _read_xattr_value_fd(fd: int, name: str, label: str) -> bytes:
    function = _libc_symbol("fgetxattr")
    function.argtypes = [
        ctypes.c_int,
        ctypes.c_char_p,
        ctypes.c_void_p,
        ctypes.c_size_t,
        ctypes.c_uint32,
        ctypes.c_int,
    ]
    function.restype = ctypes.c_ssize_t
    encoded_name = name.encode("utf-8")
    size = function(fd, encoded_name, None, 0, 0, 0)
    if size < 0 or size > MAX_XATTR_VALUE_BYTES:
        raise ProfileRefusal(f"{label} xattr {name!r} is unreadable or oversized.")
    if size == 0:
        return b""
    buffer = ctypes.create_string_buffer(size)
    if function(fd, encoded_name, buffer, size, 0, 0) != size:
        raise ProfileRefusal(f"{label} xattr {name!r} changed during read.")
    return buffer.raw[:size]


def _require_portable_provenance(value: bytes, label: str) -> str:
    if (
        len(value) != OUTPUT_PROVENANCE_SIZE
        or not value.startswith(OUTPUT_PROVENANCE_PREFIX)
    ):
        raise ProfileRefusal(
            f"{label} provenance is not 11 bytes with the required 0102 prefix."
        )
    return value.hex()


def _provenance_hex_fd(fd: int, label: str) -> str | None:
    names = _read_xattr_names_fd(fd, label)
    if PROVENANCE_XATTR not in names:
        return None
    return _require_portable_provenance(
        _read_xattr_value_fd(fd, PROVENANCE_XATTR, label), label
    )


def _validate_policy_metadata_fd(
    fd: int,
    label: str,
    metadata: os.stat_result,
    *,
    xattr_policy: str,
    allow_nonzero_flags: bool = False,
) -> tuple[str, ...]:
    if not allow_nonzero_flags and getattr(metadata, "st_flags", 0) != 0:
        raise ProfileRefusal(f"{label} has nonzero file flags.")
    _require_no_acl_fd(fd, label)
    names = _read_xattr_names_fd(fd, label)
    if xattr_policy == "none" and names:
        raise ProfileRefusal(f"{label} retains xattrs under the canonical-none policy.")
    if xattr_policy == "output":
        if any(name != PROVENANCE_XATTR for name in names):
            raise ProfileRefusal(
                f"{label} retains a forbidden output xattr (quarantine and macl included)."
            )
        if not names:
            return ()
        provenance_hex = _require_portable_provenance(
            _read_xattr_value_fd(fd, PROVENANCE_XATTR, label), label
        )
        return (f"{PROVENANCE_XATTR}={provenance_hex}",)
    if xattr_policy == "source-app" and any(
        name not in SOURCE_APP_ALLOWED_XATTRS for name in names
    ):
        raise ProfileRefusal(f"{label} has an unapproved source-app xattr.")
    if xattr_policy not in {"none", "output", "source-app", "allowed-system"}:
        raise ProfileRefusal("unknown xattr policy.")
    if xattr_policy == "allowed-system" and any(
        name not in SOURCE_REFERENCE_ALLOWED_XATTRS for name in names
    ):
        raise ProfileRefusal(f"{label} has an unapproved system xattr.")
    return names


def _strip_all_xattrs_fd(fd: int, label: str) -> None:
    remove_function = _libc_symbol("fremovexattr")
    remove_function.argtypes = [ctypes.c_int, ctypes.c_char_p, ctypes.c_int]
    remove_function.restype = ctypes.c_int
    for name in _read_xattr_names_fd(fd, label):
        if remove_function(fd, name.encode("utf-8"), 0) != 0:
            raise ProfileRefusal(f"{label} xattr {name!r} could not be removed.")
    # APFS may synchronously recreate a process-provenance attribute after a
    # successful removal.  Its portable structural form is the only canonical
    # exception; quarantine, macl, and every other xattr remain forbidden.
    metadata = os.fstat(fd)
    _validate_policy_metadata_fd(fd, label, metadata, xattr_policy="output")


def _open_directory_absolute(
    path: pathlib.Path,
    label: str,
    *,
    owner_required: bool,
    xattr_policy: str,
    allow_group_write: bool = False,
    allow_nonzero_flags: bool = False,
) -> int:
    path = _require_absolute_normalized(path, label)
    _reject_symlink_segments(path, label, include_leaf=True)
    path_metadata = os.lstat(path)
    flags = os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW
    fd = os.open(path, flags)
    try:
        metadata = os.fstat(fd)
        _require_safe_metadata(
            metadata,
            label,
            "directory",
            owner_required=owner_required,
            allow_group_write=allow_group_write,
            allow_nonzero_flags=allow_nonzero_flags,
        )
        if (metadata.st_dev, metadata.st_ino) != (
            path_metadata.st_dev,
            path_metadata.st_ino,
        ):
            raise ProfileRefusal(f"{label} changed while opened.")
        _validate_policy_metadata_fd(
            fd,
            label,
            metadata,
            xattr_policy=xattr_policy,
            allow_nonzero_flags=allow_nonzero_flags,
        )
        return fd
    except Exception:
        os.close(fd)
        raise


def _require_path_matches_fd(path: pathlib.Path, fd: int, label: str) -> None:
    _reject_symlink_segments(path, label, include_leaf=True)
    path_metadata = os.lstat(path)
    held_metadata = os.fstat(fd)
    if (path_metadata.st_dev, path_metadata.st_ino) != (
        held_metadata.st_dev,
        held_metadata.st_ino,
    ):
        raise ProfileRefusal(f"{label} path binding changed.")


def _hash_fd(fd: int, label: str) -> tuple[int, str, os.stat_result]:
    before = os.fstat(fd)
    _require_safe_metadata(before, label, "file")
    _require_no_acl_fd(fd, label)
    os.lseek(fd, 0, os.SEEK_SET)
    digest = hashlib.sha256()
    observed_size = 0
    while True:
        chunk = os.read(fd, 1024 * 1024)
        if not chunk:
            break
        digest.update(chunk)
        observed_size += len(chunk)
    after = os.fstat(fd)
    identity_fields = (
        "st_dev", "st_ino", "st_mode", "st_uid", "st_gid", "st_nlink",
        "st_size", "st_mtime_ns", "st_ctime_ns",
    )
    if any(getattr(before, field) != getattr(after, field) for field in identity_fields):
        raise ProfileRefusal(f"{label} changed while hashed.")
    if observed_size != before.st_size:
        raise ProfileRefusal(f"{label} produced an unstable read.")
    os.lseek(fd, 0, os.SEEK_SET)
    return observed_size, digest.hexdigest(), before


def _open_pinned_file(
    path: pathlib.Path,
    label: str,
    *,
    expected_name: str,
    expected_size: int,
    expected_sha256: str,
) -> PinnedFile:
    path = _require_absolute_normalized(path, label)
    if path.name != expected_name:
        raise ProfileRefusal(f"{label} name is not canonical.")
    parent_fd = _open_directory_absolute(
        path.parent,
        f"{label} parent",
        owner_required=True,
        xattr_policy="allowed-system",
    )
    fd: int | None = None
    try:
        fd = os.open(path.name, os.O_RDONLY | os.O_NOFOLLOW, dir_fd=parent_fd)
        metadata = os.fstat(fd)
        _require_safe_metadata(metadata, label, "file")
        _validate_policy_metadata_fd(
            fd, label, metadata, xattr_policy="allowed-system"
        )
        size, digest, stable = _hash_fd(fd, label)
        if size != expected_size or digest != expected_sha256:
            raise ProfileRefusal(f"{label} bytes do not match the frozen pin.")
        return PinnedFile(
            absolute_path=path,
            parent_path=path.parent,
            parent_fd=parent_fd,
            name=path.name,
            fd=fd,
            device=stable.st_dev,
            inode=stable.st_ino,
            size=size,
            sha256=digest,
            mode=stat.S_IMODE(stable.st_mode),
            provenance_hex=_provenance_hex_fd(fd, label),
        )
    except Exception:
        if fd is not None:
            os.close(fd)
        os.close(parent_fd)
        raise


def _close_pinned_file(handle: PinnedFile) -> None:
    os.close(handle.fd)
    os.close(handle.parent_fd)


def _repin_held_file(handle: PinnedFile, label: str) -> None:
    _require_path_matches_fd(handle.parent_path, handle.parent_fd, f"{label} parent")
    path_metadata = os.stat(handle.name, dir_fd=handle.parent_fd, follow_symlinks=False)
    held_metadata = os.fstat(handle.fd)
    if (path_metadata.st_dev, path_metadata.st_ino) != (
        held_metadata.st_dev,
        held_metadata.st_ino,
    ):
        raise ProfileRefusal(f"{label} pathname no longer binds the held file.")
    size, digest, stable = _hash_fd(handle.fd, label)
    _validate_policy_metadata_fd(
        handle.fd, label, stable, xattr_policy="allowed-system"
    )
    if (
        stable.st_dev != handle.device
        or stable.st_ino != handle.inode
        or size != handle.size
        or digest != handle.sha256
        or stat.S_IMODE(stable.st_mode) != handle.mode
        or _provenance_hex_fd(handle.fd, label) != handle.provenance_hex
    ):
        raise ProfileRefusal(f"{label} changed after its initial pin.")


def _validate_cue_pair(cue: PinnedFile, binary: PinnedFile) -> None:
    if cue.parent_path != binary.parent_path:
        raise ProfileRefusal("candidate CUE and BIN are not canonical siblings.")
    os.lseek(cue.fd, 0, os.SEEK_SET)
    raw = os.read(cue.fd, MAX_RECEIPT_BYTES)
    os.lseek(cue.fd, 0, os.SEEK_SET)
    expected = (
        f'FILE "{binary.name}" BINARY\n'
        "  TRACK 01 MODE2/2352\n"
        "    INDEX 01 00:00:00\n"
    ).encode("ascii")
    if raw != expected:
        raise ProfileRefusal("candidate CUE does not bind the pinned sibling BIN exactly.")


def _scan_tree_fd(
    root_fd: int,
    label: str,
    *,
    xattr_policy: str,
) -> dict[str, ManifestEntry]:
    manifest: dict[str, ManifestEntry] = {}

    def visit(directory_fd: int, relative: str) -> None:
        directory_metadata = os.fstat(directory_fd)
        _require_safe_metadata(directory_metadata, f"{label}/{relative}", "directory")
        xattrs = _validate_policy_metadata_fd(
            directory_fd,
            f"{label}/{relative}",
            directory_metadata,
            xattr_policy=xattr_policy,
        )
        manifest[relative] = ManifestEntry(
            kind="directory",
            mode=stat.S_IMODE(directory_metadata.st_mode),
            link_count=directory_metadata.st_nlink,
            size=None,
            sha256=None,
            xattrs=xattrs,
        )
        names = sorted(os.listdir(directory_fd), key=os.fsencode)
        if len(names) != len(set(names)):
            raise ProfileRefusal(f"{label}/{relative} has duplicate names.")
        for name in names:
            if (
                not name
                or name in {".", ".."}
                or "/" in name
                or "\0" in name
                or unicodedata.normalize("NFC", name) != name
            ):
                raise ProfileRefusal(f"{label} contains a non-canonical name.")
            child_relative = name if relative == "." else f"{relative}/{name}"
            metadata = os.stat(name, dir_fd=directory_fd, follow_symlinks=False)
            if stat.S_ISDIR(metadata.st_mode):
                child_fd = os.open(
                    name,
                    os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW,
                    dir_fd=directory_fd,
                )
                try:
                    visit(child_fd, child_relative)
                finally:
                    os.close(child_fd)
                continue
            if not stat.S_ISREG(metadata.st_mode):
                raise ProfileRefusal(f"{label}/{child_relative} is not regular.")
            child_fd = os.open(name, os.O_RDONLY | os.O_NOFOLLOW, dir_fd=directory_fd)
            try:
                stable = os.fstat(child_fd)
                _require_safe_metadata(stable, f"{label}/{child_relative}", "file")
                child_xattrs = _validate_policy_metadata_fd(
                    child_fd,
                    f"{label}/{child_relative}",
                    stable,
                    xattr_policy=xattr_policy,
                )
                size, digest, after = _hash_fd(child_fd, f"{label}/{child_relative}")
                if (after.st_dev, after.st_ino) != (metadata.st_dev, metadata.st_ino):
                    raise ProfileRefusal(f"{label}/{child_relative} changed while opened.")
                manifest[child_relative] = ManifestEntry(
                    kind="file",
                    mode=stat.S_IMODE(after.st_mode),
                    link_count=after.st_nlink,
                    size=size,
                    sha256=digest,
                    xattrs=child_xattrs,
                )
            finally:
                os.close(child_fd)

    visit(root_fd, ".")
    return manifest


def _serialize_app_manifest(manifest: Mapping[str, ManifestEntry]) -> bytes:
    lines = [
        APP_MANIFEST_FORMAT,
        f"entryCount={len(manifest)}",
        "pathPolicy=NFC-posix-relative-bytewise-order",
        "ownerPolicy=current-effective-uid-gid",
        "fileNlinkPolicy=one",
        "directoryNlinkPolicy=exact",
        "aclPolicy=absent",
        "flagsPolicy=zero",
        "timestampPolicy=excluded",
        "xattrPolicy=source-allowlisted-noncontent-output-attested-separately",
    ]
    for relative in sorted(manifest, key=lambda item: item.encode("utf-8")):
        entry = manifest[relative]
        lines.append("|".join((
            relative,
            entry.kind,
            f"{entry.mode:04o}",
            str(entry.link_count),
            "-" if entry.size is None else str(entry.size),
            "-" if entry.sha256 is None else entry.sha256,
        )))
    return ("\n".join(lines) + "\n").encode("utf-8")


def _manifest_without_xattrs(
    manifest: Mapping[str, ManifestEntry]
) -> dict[str, ManifestEntry]:
    return {
        path: dataclasses.replace(entry, xattrs=())
        for path, entry in manifest.items()
    }


def _output_tree_attestation(
    manifest: Mapping[str, ManifestEntry], scope: str
) -> dict[str, object]:
    entries: list[dict[str, object]] = []
    provenance_count = 0
    for relative in sorted(manifest, key=lambda item: item.encode("utf-8")):
        entry = manifest[relative]
        provenance_hex: str | None = None
        if entry.xattrs:
            if len(entry.xattrs) != 1 or not entry.xattrs[0].startswith(
                f"{PROVENANCE_XATTR}="
            ):
                raise ProfileRefusal(f"{scope} manifest has a noncanonical xattr record.")
            provenance_hex = entry.xattrs[0].split("=", 1)[1]
            _require_portable_provenance(bytes.fromhex(provenance_hex), scope)
            provenance_count += 1
        entries.append({
            "path": relative,
            "kind": entry.kind,
            "mode": f"{entry.mode:04o}",
            "linkCount": entry.link_count,
            "size": entry.size,
            "sha256": entry.sha256,
            "provenanceHex": provenance_hex,
        })
    payload: dict[str, object] = {
        "format": OUTPUT_TREE_MANIFEST_FORMAT,
        "scope": scope,
        "entryCount": len(entries),
        "provenanceEntryCount": provenance_count,
        "noXattrEntryCount": len(entries) - provenance_count,
        "ownerPolicy": "current-effective-uid-gid",
        "aclPolicy": "absent",
        "flagsPolicy": "zero",
        "timestampPolicy": "excluded",
        "xattrPolicyVersion": OUTPUT_XATTR_POLICY_VERSION,
        "entries": entries,
    }
    payload["manifestSha256"] = _sha256_bytes(_canonical_json(payload))
    return payload


def _collect_output_metadata(
    package_fd: int,
    expected_source_manifest: Mapping[str, ManifestEntry],
    pins: BuildPins,
) -> dict[str, object]:
    app_fd = _open_relative_directory(package_fd, pathlib.Path(PACKAGED_APP_LEAF))
    try:
        app_manifest = _scan_tree_fd(app_fd, "packaged app metadata", xattr_policy="output")
    finally:
        os.close(app_fd)
    if _manifest_without_xattrs(app_manifest) != _manifest_without_xattrs(
        expected_source_manifest
    ):
        raise ProfileRefusal("packaged app metadata manifest has unexpected content.")
    _require_app_manifest(app_manifest, pins)
    home_fd = _open_relative_directory(package_fd, HOME_RELATIVE_PATH)
    try:
        home_manifest = _scan_tree_fd(home_fd, "external HOME metadata", xattr_policy="output")
    finally:
        os.close(home_fd)
    return {
        "policy": {
            "version": OUTPUT_XATTR_POLICY_VERSION,
            "allowed": [
                "none",
                "com.apple.provenance:11-bytes-prefix-0102",
            ],
            "forbidden": [
                "com.apple.quarantine",
                "com.apple.macl",
                "all-other-xattrs",
            ],
            "removalAttemptedBeforeAttestation": True,
        },
        "application": _output_tree_attestation(app_manifest, "application"),
        "home": _output_tree_attestation(home_manifest, "home"),
    }


def _candidate_attestation(cue: PinnedFile, binary: PinnedFile) -> dict[str, object]:
    entries = []
    for role, handle in (("cue", cue), ("bin", binary)):
        entries.append({
            "role": role,
            "path": os.fspath(handle.absolute_path),
            "kind": "file",
            "mode": f"{handle.mode:04o}",
            "size": handle.size,
            "sha256": handle.sha256,
            "provenanceHex": handle.provenance_hex,
        })
    payload: dict[str, object] = {
        "format": CANDIDATE_MANIFEST_FORMAT,
        "entryCount": len(entries),
        "provenancePolicy": "null-or-11-bytes-prefix-0102-exact-hex-recorded",
        "entries": entries,
    }
    payload["manifestSha256"] = _sha256_bytes(_canonical_json(payload))
    return payload


def _require_app_manifest(
    manifest: Mapping[str, ManifestEntry], pins: BuildPins
) -> str:
    if len(manifest) != pins.app_manifest_entry_count:
        raise ProfileRefusal("DuckStation app manifest entry count is not frozen.")
    digest = _sha256_bytes(_serialize_app_manifest(manifest))
    if digest != pins.app_manifest_sha256:
        raise ProfileRefusal("DuckStation app content manifest is not frozen.")
    executable = manifest.get(SOURCE_APP_EXECUTABLE_RELATIVE_PATH)
    if (
        executable is None
        or executable.kind != "file"
        or executable.size != pins.executable_size
        or executable.sha256 != pins.executable_sha256
        or executable.mode != 0o755
    ):
        raise ProfileRefusal("DuckStation executable identity is not frozen.")
    return digest


def _open_relative_file(root_fd: int, relative_path: str, label: str) -> int:
    segments = relative_path.split("/")
    if any(segment in {"", ".", ".."} for segment in segments):
        raise ProfileRefusal(f"{label} relative path is not canonical.")
    current_fd = os.dup(root_fd)
    try:
        for segment in segments[:-1]:
            next_fd = os.open(
                segment,
                os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW,
                dir_fd=current_fd,
            )
            os.close(current_fd)
            current_fd = next_fd
        return os.open(
            segments[-1], os.O_RDONLY | os.O_NOFOLLOW, dir_fd=current_fd
        )
    finally:
        os.close(current_fd)


def _hash_relative_file_at(
    root_fd: int, relative_path: str, label: str
) -> tuple[int, str]:
    fd = _open_relative_file(root_fd, relative_path, label)
    try:
        size, digest, _ = _hash_fd(fd, label)
        return size, digest
    finally:
        os.close(fd)


def _read_file_at(
    root_fd: int,
    relative_path: str,
    label: str,
    *,
    maximum_size: int = MAX_RECEIPT_BYTES,
) -> bytes:
    fd = _open_relative_file(root_fd, relative_path, label)
    try:
        size, _, _ = _hash_fd(fd, label)
        if size > maximum_size:
            raise ProfileRefusal(f"{label} is too large.")
        os.lseek(fd, 0, os.SEEK_SET)
        chunks: list[bytes] = []
        remaining = size
        while remaining:
            chunk = os.read(fd, min(remaining, 1024 * 1024))
            if not chunk:
                raise ProfileRefusal(f"{label} ended during its exact read.")
            chunks.append(chunk)
            remaining -= len(chunk)
        return b"".join(chunks)
    finally:
        os.close(fd)


def _require_app_version(app_fd: int, pins: BuildPins) -> None:
    raw = _read_file_at(app_fd, SOURCE_APP_INFO_PLIST_RELATIVE_PATH, "Info.plist")
    try:
        payload = plistlib.loads(raw)
    except Exception as exc:
        raise ProfileRefusal("DuckStation Info.plist is not valid.") from exc
    if payload.get("CFBundleShortVersionString") != pins.app_version:
        raise ProfileRefusal("DuckStation version is not frozen.")


def _codesign_verify(path: pathlib.Path) -> None:
    result = subprocess.run(
        ["/usr/bin/codesign", "--verify", "--deep", "--strict", os.fspath(path)],
        stdin=subprocess.DEVNULL,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        check=False,
        timeout=120,
        env={"LANG": "C", "LC_ALL": "C", "PATH": "/usr/bin:/bin"},
    )
    if result.returncode != 0:
        diagnostic = result.stderr.decode("utf-8", "replace")[:2048]
        raise ProfileRefusal(f"codesign strict verification failed: {diagnostic}")


def _copy_file_fd(
    source_fd: int,
    destination_parent_fd: int,
    destination_name: str,
    mode: int,
    label: str,
) -> None:
    destination_fd = os.open(
        destination_name,
        os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW,
        0o600,
        dir_fd=destination_parent_fd,
    )
    try:
        source_before = os.fstat(source_fd)
        _require_safe_metadata(source_before, f"source {label}", "file")
        os.lseek(source_fd, 0, os.SEEK_SET)
        while True:
            chunk = os.read(source_fd, 1024 * 1024)
            if not chunk:
                break
            view = memoryview(chunk)
            while view:
                written = os.write(destination_fd, view)
                if written <= 0:
                    raise ProfileRefusal(f"copy of {label} made no progress.")
                view = view[written:]
        source_after = os.fstat(source_fd)
        if (
            source_before.st_dev,
            source_before.st_ino,
            source_before.st_size,
            source_before.st_mtime_ns,
            source_before.st_ctime_ns,
        ) != (
            source_after.st_dev,
            source_after.st_ino,
            source_after.st_size,
            source_after.st_mtime_ns,
            source_after.st_ctime_ns,
        ):
            raise ProfileRefusal(f"source {label} changed while copied.")
        os.fchmod(destination_fd, mode)
        _strip_all_xattrs_fd(destination_fd, label)
        os.fsync(destination_fd)
    finally:
        os.close(destination_fd)
        os.lseek(source_fd, 0, os.SEEK_SET)


def _copy_tree_fd(source_fd: int, destination_fd: int, label: str) -> None:
    source_metadata = os.fstat(source_fd)
    _require_safe_metadata(source_metadata, f"source {label}", "directory")
    names = sorted(os.listdir(source_fd), key=os.fsencode)
    for name in names:
        metadata = os.stat(name, dir_fd=source_fd, follow_symlinks=False)
        if stat.S_ISDIR(metadata.st_mode):
            source_child_fd = os.open(
                name,
                os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW,
                dir_fd=source_fd,
            )
            os.mkdir(name, 0o700, dir_fd=destination_fd)
            destination_child_fd = os.open(
                name,
                os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW,
                dir_fd=destination_fd,
            )
            try:
                _copy_tree_fd(source_child_fd, destination_child_fd, f"{label}/{name}")
                os.fchmod(destination_child_fd, stat.S_IMODE(metadata.st_mode))
                _strip_all_xattrs_fd(destination_child_fd, f"{label}/{name}")
                os.fsync(destination_child_fd)
            finally:
                os.close(destination_child_fd)
                os.close(source_child_fd)
            continue
        if not stat.S_ISREG(metadata.st_mode):
            raise ProfileRefusal(f"source {label}/{name} is not regular.")
        source_child_fd = os.open(name, os.O_RDONLY | os.O_NOFOLLOW, dir_fd=source_fd)
        try:
            _copy_file_fd(
                source_child_fd,
                destination_fd,
                name,
                stat.S_IMODE(metadata.st_mode),
                f"{label}/{name}",
            )
        finally:
            os.close(source_child_fd)
    _strip_all_xattrs_fd(destination_fd, label)


def _mkdir_open(parent_fd: int, name: str, mode: int) -> int:
    os.mkdir(name, mode, dir_fd=parent_fd)
    fd: int | None = None
    try:
        fd = os.open(
            name, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW, dir_fd=parent_fd
        )
        os.fchmod(fd, mode)
        _strip_all_xattrs_fd(fd, name)
        return fd
    except Exception as original_exc:
        retained_inode = os.fstat(fd).st_ino if fd is not None else -1
        if fd is not None:
            os.close(fd)
        try:
            os.rmdir(name, dir_fd=parent_fd)
        except OSError as cleanup_exc:
            raise ProfileRefusal(
                f"directory creation failed and retained {name!r} as inode "
                f"{retained_inode}: {cleanup_exc}"
            ) from original_exc
        raise


def _write_file_at(parent_fd: int, name: str, data: bytes, mode: int) -> None:
    fd = os.open(
        name,
        os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW,
        mode,
        dir_fd=parent_fd,
    )
    try:
        view = memoryview(data)
        while view:
            written = os.write(fd, view)
            if written <= 0:
                raise ProfileRefusal(f"write of {name} made no progress.")
            view = view[written:]
        os.fchmod(fd, mode)
        _strip_all_xattrs_fd(fd, name)
        os.fsync(fd)
    finally:
        os.close(fd)


def _create_empty_file_at(parent_fd: int, name: str, mode: int) -> int:
    fd = os.open(
        name,
        os.O_RDWR | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW,
        mode,
        dir_fd=parent_fd,
    )
    try:
        os.fchmod(fd, mode)
        _strip_all_xattrs_fd(fd, name)
        os.fsync(fd)
        return fd
    except Exception:
        os.close(fd)
        try:
            os.unlink(name, dir_fd=parent_fd)
        except OSError as cleanup_exc:
            raise ProfileRefusal(
                f"empty file {name} creation failed and its inode was retained: "
                f"{cleanup_exc}"
            ) from cleanup_exc
        raise


def _rewrite_held_file_at(
    parent_fd: int,
    name: str,
    held_fd: int,
    data: bytes,
    mode: int,
    label: str,
) -> None:
    path_metadata = os.stat(name, dir_fd=parent_fd, follow_symlinks=False)
    before = os.fstat(held_fd)
    _require_safe_metadata(before, label, "file")
    _validate_policy_metadata_fd(
        held_fd, label, before, xattr_policy="output"
    )
    if (path_metadata.st_dev, path_metadata.st_ino) != (before.st_dev, before.st_ino):
        raise ProfileRefusal(f"{label} path does not bind its held inode.")
    if before.st_size != 0 or stat.S_IMODE(before.st_mode) != mode:
        raise ProfileRefusal(f"{label} placeholder metadata is not canonical.")
    immediately_before = os.fstat(held_fd)
    _require_safe_metadata(immediately_before, label, "file")
    if (
        immediately_before.st_dev,
        immediately_before.st_ino,
        immediately_before.st_nlink,
        immediately_before.st_size,
        stat.S_IMODE(immediately_before.st_mode),
    ) != (
        before.st_dev,
        before.st_ino,
        before.st_nlink,
        before.st_size,
        stat.S_IMODE(before.st_mode),
    ):
        raise ProfileRefusal(f"{label} changed before its in-place rewrite.")
    os.ftruncate(held_fd, 0)
    os.lseek(held_fd, 0, os.SEEK_SET)
    view = memoryview(data)
    while view:
        written = os.write(held_fd, view)
        if written <= 0:
            raise ProfileRefusal(f"write of {label} made no progress.")
        view = view[written:]
    os.fchmod(held_fd, mode)
    _strip_all_xattrs_fd(held_fd, label)
    os.fsync(held_fd)
    after = os.fstat(held_fd)
    _require_safe_metadata(after, label, "file")
    if (
        after.st_dev != before.st_dev
        or after.st_ino != before.st_ino
        or after.st_nlink != 1
        or after.st_size != len(data)
        or stat.S_IMODE(after.st_mode) != mode
        or not _name_matches_fd(parent_fd, name, held_fd)
    ):
        raise ProfileRefusal(f"{label} identity changed during its in-place rewrite.")
    size, digest, _ = _hash_fd(held_fd, label)
    if size != len(data) or digest != _sha256_bytes(data):
        raise ProfileRefusal(f"{label} rewrite did not produce the canonical bytes.")


def _open_relative_directory(root_fd: int, relative: pathlib.Path) -> int:
    fd = os.dup(root_fd)
    try:
        for segment in relative.parts:
            next_fd = os.open(
                segment,
                os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW,
                dir_fd=fd,
            )
            os.close(fd)
            fd = next_fd
        return fd
    except Exception:
        os.close(fd)
        raise


def _fsync_tree_fd(root_fd: int) -> None:
    for name in sorted(os.listdir(root_fd), key=os.fsencode):
        metadata = os.stat(name, dir_fd=root_fd, follow_symlinks=False)
        if stat.S_ISDIR(metadata.st_mode):
            child_fd = os.open(
                name,
                os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW,
                dir_fd=root_fd,
            )
            try:
                _fsync_tree_fd(child_fd)
            finally:
                os.close(child_fd)
        elif stat.S_ISREG(metadata.st_mode):
            child_fd = os.open(name, os.O_RDONLY | os.O_NOFOLLOW, dir_fd=root_fd)
            try:
                os.fsync(child_fd)
            finally:
                os.close(child_fd)
        else:
            raise ProfileRefusal("fsync traversal found a non-regular entry.")
    os.fsync(root_fd)


def _remove_tree_contents_fd(root_fd: int) -> None:
    for name in sorted(os.listdir(root_fd), key=os.fsencode, reverse=True):
        metadata = os.stat(name, dir_fd=root_fd, follow_symlinks=False)
        if stat.S_ISDIR(metadata.st_mode):
            child_fd = os.open(
                name,
                os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW,
                dir_fd=root_fd,
            )
            try:
                _remove_tree_contents_fd(child_fd)
            finally:
                os.close(child_fd)
            os.rmdir(name, dir_fd=root_fd)
        elif stat.S_ISREG(metadata.st_mode):
            os.unlink(name, dir_fd=root_fd)
        else:
            raise ProfileRefusal("cleanup refused a non-regular staged entry.")


def _name_matches_fd(parent_fd: int, name: str, held_fd: int) -> bool:
    try:
        path_metadata = os.stat(name, dir_fd=parent_fd, follow_symlinks=False)
    except FileNotFoundError:
        return False
    held_metadata = os.fstat(held_fd)
    return (path_metadata.st_dev, path_metadata.st_ino) == (
        held_metadata.st_dev,
        held_metadata.st_ino,
    )


def _names_matching_fd(parent_fd: int, held_fd: int) -> list[str]:
    held_metadata = os.fstat(held_fd)
    matches: list[str] = []
    for name in sorted(os.listdir(parent_fd), key=os.fsencode):
        try:
            metadata = os.stat(name, dir_fd=parent_fd, follow_symlinks=False)
        except FileNotFoundError:
            continue
        if (metadata.st_dev, metadata.st_ino) == (
            held_metadata.st_dev,
            held_metadata.st_ino,
        ):
            matches.append(name)
    return matches


def _cleanup_private_stage(
    parent_fd: int,
    parent_path: pathlib.Path,
    stage_fd: int,
    prior_reason: str,
) -> None:
    inode = os.fstat(stage_fd).st_ino
    matches = _names_matching_fd(parent_fd, stage_fd)
    if len(matches) != 1:
        retained = parent_path / (
            matches[0] if matches else f"<unbound-in-held-parent-fd-{parent_fd}>"
        )
        raise PrivateStageCleanupFailed(
            retained,
            inode,
            f"expected one exact parent binding, observed {matches!r}",
            prior_reason,
        )
    retained = parent_path / matches[0]
    try:
        _remove_tree_contents_fd(stage_fd)
        os.rmdir(matches[0], dir_fd=parent_fd)
    except Exception as cleanup_exc:
        raise PrivateStageCleanupFailed(
            retained, inode, str(cleanup_exc), prior_reason
        ) from cleanup_exc
    try:
        os.fsync(parent_fd)
    except OSError as durability_exc:
        raise PrivateStageCleanupDurabilityUnconfirmed(
            retained, inode, str(durability_exc), prior_reason
        ) from durability_exc


def _rename_exclusive(
    source_parent_fd: int,
    source_name: str,
    destination_parent_fd: int,
    destination_name: str,
) -> None:
    function = _libc_symbol("renameatx_np")
    function.argtypes = [
        ctypes.c_int,
        ctypes.c_char_p,
        ctypes.c_int,
        ctypes.c_char_p,
        ctypes.c_uint,
    ]
    function.restype = ctypes.c_int
    ctypes.set_errno(0)
    if function(
        source_parent_fd,
        os.fsencode(source_name),
        destination_parent_fd,
        os.fsencode(destination_name),
        0x00000004,
    ) != 0:
        error_number = ctypes.get_errno()
        if error_number in {errno.EEXIST, errno.ENOTEMPTY}:
            raise ProfileRefusal("destination appeared before atomic publication.")
        raise ProfileRefusal(f"atomic publication failed: {os.strerror(error_number)}")


def _quarantine_exact_inode(
    parent_fd: int,
    destination_name: str,
    published_fd: int,
) -> str:
    if not _name_matches_fd(parent_fd, destination_name, published_fd):
        raise ProfileRefusal("published inode is no longer bound; exact quarantine refused.")
    quarantine_name = (
        f".{destination_name}.quarantine-{os.fstat(published_fd).st_ino:x}-"
        f"{secrets.token_hex(4)}"
    )
    _rename_exclusive(parent_fd, destination_name, parent_fd, quarantine_name)
    return quarantine_name


def _acquire_common_lock(parent_fd: int) -> int:
    fd = os.open(
        COMMON_LOCK_LEAF,
        os.O_RDWR | os.O_CREAT | os.O_NOFOLLOW,
        0o600,
        dir_fd=parent_fd,
    )
    try:
        metadata = os.fstat(fd)
        _require_safe_metadata(metadata, "common profile lock", "file")
        os.fchmod(fd, 0o600)
        _validate_policy_metadata_fd(
            fd, "common profile lock", os.fstat(fd), xattr_policy="output"
        )
        try:
            fcntl.flock(fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError as exc:
            raise ProfileRefusal("another cooperative profile operation holds the lock.") from exc
        return fd
    except Exception:
        os.close(fd)
        raise


def _parse_ini(raw: bytes) -> dict[str, tuple[tuple[str, str], ...]]:
    if not raw.endswith(b"\n") or b"\r" in raw or raw.startswith(b"\xef\xbb\xbf"):
        raise ProfileRefusal("settings.ini is not canonical LF UTF-8.")
    raw.decode("utf-8", "strict")
    sections: dict[str, list[tuple[str, str]]] = {}
    folded_sections: set[str] = set()
    folded_keys: dict[str, set[str]] = {}
    current: str | None = None
    for line_number, line in enumerate(raw.splitlines(keepends=True), start=1):
        if line == b"\n":
            continue
        section_match = INI_SECTION_RE.fullmatch(line)
        if section_match:
            current = section_match.group(1).decode("ascii")
            if current.casefold() in folded_sections:
                raise ProfileRefusal("settings.ini has a duplicate section.")
            folded_sections.add(current.casefold())
            sections[current] = []
            folded_keys[current] = set()
            continue
        key_match = INI_KEY_RE.fullmatch(line)
        if key_match is None or current is None:
            raise ProfileRefusal(f"settings.ini line {line_number} is malformed.")
        key = key_match.group(1).decode("ascii")
        if key.casefold() in folded_keys[current]:
            raise ProfileRefusal("settings.ini has a duplicate key.")
        folded_keys[current].add(key.casefold())
        sections[current].append((key, key_match.group(2).decode("utf-8")))
    return {name: tuple(values) for name, values in sections.items()}


def _validate_settings(raw: bytes, pins: BuildPins) -> None:
    if len(raw) != pins.settings_size or _sha256_bytes(raw) != pins.settings_sha256:
        raise ProfileRefusal("settings.ini bytes are not canonical.")
    sections = _parse_ini(raw)
    expected_sections = {
        "Main", "BIOS", "CDROM", "MemoryCards", "TextureReplacements",
        "InputSources", "ControllerPorts", "Pad1", "Hotkeys", "PIO",
        "PCDrv", "Debug", *(f"Pad{ordinal}" for ordinal in range(2, 9)),
    }
    if set(sections) != expected_sections:
        raise ProfileRefusal("settings.ini section topology is not canonical.")
    main = dict(sections.get("Main", ()))
    required_main = {
        "ConfirmPowerOff": "false",
        "CreateSaveStateBackups": "false",
        "DisableBackgroundInput": "true",
        "LoadDevicesFromSaveStates": "false",
        "RewindEnable": "false",
        "RunaheadFrameCount": "0",
        "SaveStateOnExit": "false",
        "StartPaused": "false",
    }
    if any(main.get(key) != value for key, value in required_main.items()):
        raise ProfileRefusal("settings.ini Main safety semantics are not canonical.")
    inputs = dict(sections.get("InputSources", ()))
    if inputs != {
        "RawInput": "false",
        "SDL": "false",
        "SDLControllerEnhancedMode": "false",
        "SDLPS5PlayerLED": "false",
        "XInput": "false",
    }:
        raise ProfileRefusal("settings.ini enables a host controller source.")
    texture = dict(sections.get("TextureReplacements", ()))
    if not texture or any(value != "false" for value in texture.values()):
        raise ProfileRefusal("settings.ini enables texture replacement or dumping.")
    if dict(sections.get("BIOS", ())) != {
        "FastForwardBoot": "false",
        "PatchFastBoot": "false",
        "SearchDirectory": "bios",
    }:
        raise ProfileRefusal("settings.ini BIOS policy is not profile-owned and safe.")
    if dict(sections.get("MemoryCards", ())) != {
        "Card1Type": "None", "Card2Type": "None", "Directory": "memcards"
    }:
        raise ProfileRefusal("settings.ini memory-card policy is not disabled.")
    if dict(sections.get("CDROM", ())) != {
        "LoadImagePatches": "false", "LoadImageToRAM": "false",
        "ReadSpeedup": "1", "SeekSpeedup": "1",
    }:
        raise ProfileRefusal("settings.ini CD-ROM policy is not canonical.")
    expected_hotkeys = (
        ("PowerOff", "Keyboard/F12"),
        ("TogglePause", "Keyboard/Space"),
    )
    if sections.get("Hotkeys") != expected_hotkeys:
        raise ProfileRefusal("settings.ini hotkeys are not the two safe bindings.")
    expected_pad1 = {
        "Circle": "Keyboard/Z", "Cross": "Keyboard/X",
        "Down": "Keyboard/Down", "L1": "Keyboard/Q", "L2": "Keyboard/E",
        "Left": "Keyboard/Left", "R1": "Keyboard/W", "R2": "Keyboard/R",
        "Right": "Keyboard/Right", "Select": "Keyboard/Backspace",
        "Square": "Keyboard/S", "Start": "Keyboard/P",
        "Triangle": "Keyboard/A", "Type": "DigitalController",
        "Up": "Keyboard/Up",
    }
    for ordinal, target in enumerate(
        ("R1", "R2", "L1", "L2", "Left", "Down"), start=1
    ):
        expected_pad1[f"Macro{ordinal}"] = f"Keyboard/{ordinal}"
        expected_pad1[f"Macro{ordinal}Binds"] = target
        expected_pad1[f"Macro{ordinal}Toggle"] = "true"
    if dict(sections.get("Pad1", ())) != expected_pad1:
        raise ProfileRefusal("settings.ini direct keys or automation macros are not canonical.")
    for ordinal in range(2, 9):
        if sections.get(f"Pad{ordinal}") != (("Type", "None"),):
            raise ProfileRefusal("settings.ini does not disable every other pad.")
    if sections.get("ControllerPorts") != (("MultitapMode", "Disabled"),):
        raise ProfileRefusal("settings.ini controller-port topology is unsafe.")
    if dict(sections.get("PIO", ())) != {
        "DeviceType": "None", "FlashImageWriteEnable": "false"
    } or dict(sections.get("PCDrv", ())) != {
        "EnableWrites": "false", "Enabled": "false"
    }:
        raise ProfileRefusal("settings.ini enables a writable expansion device.")
    for section, entries in sections.items():
        for key, value in entries:
            folded = f"{key}={value}".casefold()
            if "keyboard/escape" in folded or value.startswith("SDL-"):
                raise ProfileRefusal("settings.ini retains Escape or SDL binding.")
            if value.startswith("Keyboard/") and any(
                token in key.casefold() for token in ("savestate", "loadstate")
            ):
                raise ProfileRefusal("settings.ini retains save/load-state binding.")


def _canonical_json(payload: object) -> bytes:
    return (json.dumps(payload, sort_keys=True, separators=(",", ":")) + "\n").encode("utf-8")


def _entry_provenance_hex(entry: ManifestEntry, label: str) -> str | None:
    if not entry.xattrs:
        return None
    if len(entry.xattrs) != 1 or not entry.xattrs[0].startswith(
        f"{PROVENANCE_XATTR}="
    ):
        raise ProfileRefusal(f"{label} has a noncanonical xattr attestation.")
    value = entry.xattrs[0].split("=", 1)[1]
    try:
        raw = bytes.fromhex(value)
    except ValueError as exc:
        raise ProfileRefusal(f"{label} provenance hex is malformed.") from exc
    _require_portable_provenance(raw, label)
    return value


def _package_envelope_entry(
    relative_path: str,
    entry: ManifestEntry,
    *,
    size_override: int | None = None,
    sha256_override: str | None = None,
) -> dict[str, object]:
    return {
        "path": relative_path,
        "kind": entry.kind,
        "mode": f"{entry.mode:04o}",
        "linkCount": entry.link_count,
        "size": entry.size if size_override is None else size_override,
        "sha256": entry.sha256 if sha256_override is None else sha256_override,
        "provenanceHex": _entry_provenance_hex(entry, relative_path),
    }


def _collect_package_envelope(
    package_fd: int,
    pins: BuildPins,
    output_metadata: Mapping[str, object],
    *,
    receipt_size_override: int | None = None,
) -> dict[str, object]:
    _require_exact_package_topology(package_fd, pins)
    manifest = _scan_tree_fd(package_fd, "package envelope", xattr_policy="output")
    expected_top = {".", PACKAGED_APP_LEAF, "home", RECEIPT_LEAF}
    if not expected_top.issubset(manifest):
        raise ProfileRefusal("package envelope is missing a canonical root entry.")
    receipt_entry = manifest[RECEIPT_LEAF]
    if receipt_entry.kind != "file":
        raise ProfileRefusal("package envelope receipt entry is not a regular file.")
    if receipt_size_override is not None and not (
        0 <= receipt_size_override <= MAX_RECEIPT_BYTES
    ):
        raise ProfileRefusal("receipt fixed-point size is outside policy.")
    application_metadata = output_metadata.get("application")
    home_metadata = output_metadata.get("home")
    if not isinstance(application_metadata, Mapping) or not isinstance(
        home_metadata, Mapping
    ):
        raise ProfileRefusal("package envelope lacks nested manifest attestations.")
    entries = [
        _package_envelope_entry(".", manifest["."]),
        _package_envelope_entry(PACKAGED_APP_LEAF, manifest[PACKAGED_APP_LEAF]),
        _package_envelope_entry("home", manifest["home"]),
        _package_envelope_entry(
            RECEIPT_LEAF,
            receipt_entry,
            size_override=(
                receipt_entry.size
                if receipt_size_override is None
                else receipt_size_override
            ),
            sha256_override=SELF_RECEIPT_SHA256_SENTINEL,
        ),
    ]
    entries[1]["subtreeManifestSha256"] = application_metadata.get(
        "manifestSha256"
    )
    entries[1]["subtreeEntryCount"] = application_metadata.get("entryCount")
    entries[2]["subtreeManifestSha256"] = home_metadata.get("manifestSha256")
    entries[2]["subtreeEntryCount"] = home_metadata.get("entryCount")
    payload: dict[str, object] = {
        "format": PACKAGE_ENVELOPE_FORMAT,
        "entryCount": len(entries),
        "rootTopology": [PACKAGED_APP_LEAF, "home", RECEIPT_LEAF],
        "receiptContentHashPolicy": (
            "self-sentinel; exact receipt bytes and receipt closure verified separately"
        ),
        "receiptSha256Sentinel": SELF_RECEIPT_SHA256_SENTINEL,
        "ownerPolicy": "current-effective-uid-gid",
        "aclPolicy": "absent",
        "flagsPolicy": "zero",
        "timestampPolicy": "excluded",
        "xattrPolicyVersion": OUTPUT_XATTR_POLICY_VERSION,
        "entries": entries,
    }
    payload["manifestSha256"] = _sha256_bytes(_canonical_json(payload))
    return payload


def _package_envelope_with_receipt_size(
    envelope: Mapping[str, object], receipt_size: int
) -> dict[str, object]:
    if not 0 <= receipt_size <= MAX_RECEIPT_BYTES:
        raise ProfileRefusal("receipt fixed-point size is outside policy.")
    entries_value = envelope.get("entries")
    if not isinstance(entries_value, list):
        raise ProfileRefusal("package envelope entries are malformed.")
    entries: list[dict[str, object]] = []
    found_receipt = False
    for value in entries_value:
        if not isinstance(value, dict):
            raise ProfileRefusal("package envelope entry is malformed.")
        entry = dict(value)
        if entry.get("path") == RECEIPT_LEAF:
            entry["size"] = receipt_size
            entry["sha256"] = SELF_RECEIPT_SHA256_SENTINEL
            found_receipt = True
        entries.append(entry)
    if not found_receipt:
        raise ProfileRefusal("package envelope omits its receipt entry.")
    payload = {
        key: value for key, value in envelope.items() if key != "manifestSha256"
    }
    payload["entries"] = entries
    payload["manifestSha256"] = _sha256_bytes(_canonical_json(payload))
    return payload


def _validate_receipt(observed: bytes, expected: bytes) -> None:
    if observed != expected:
        raise ProfileRefusal("handoff receipt bytes are not canonical.")

    def reject_duplicates(pairs: list[tuple[str, object]]) -> dict[str, object]:
        result: dict[str, object] = {}
        for key, value in pairs:
            if key in result:
                raise ProfileRefusal("handoff receipt has a duplicate JSON key.")
            result[key] = value
        return result

    try:
        payload = json.loads(observed, object_pairs_hook=reject_duplicates)
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise ProfileRefusal("handoff receipt is not canonical JSON.") from exc
    if not isinstance(payload, dict):
        raise ProfileRefusal("handoff receipt root is not an object.")
    integrity = payload.pop("integrity", None)
    if not isinstance(integrity, dict) or set(integrity) != {
        "algorithm", "receiptClosureSha256"
    }:
        raise ProfileRefusal("handoff receipt integrity closure is malformed.")
    if integrity.get("algorithm") != "sha256":
        raise ProfileRefusal("handoff receipt integrity algorithm is not canonical.")
    if integrity.get("receiptClosureSha256") != _sha256_bytes(
        _canonical_json(payload)
    ):
        raise ProfileRefusal("handoff receipt integrity closure does not verify.")
    payload["integrity"] = integrity
    if _canonical_json(payload) != observed:
        raise ProfileRefusal("handoff receipt JSON serialization is not canonical.")


def _build_receipt(
    destination: pathlib.Path,
    pins: BuildPins,
    app_manifest_sha256: str,
    cue: PinnedFile,
    binary: PinnedFile,
    output_metadata: Mapping[str, object],
    package_envelope: Mapping[str, object],
) -> bytes:
    home = destination / HOME_RELATIVE_PATH
    data_root = destination / DATA_ROOT_RELATIVE_PATH
    payload: dict[str, object] = {
        "format": RECEIPT_FORMAT,
        "formatVersion": RECEIPT_VERSION,
        "trustModel": {
            "class": "cooperative-non-hostile-same-uid",
            "commonLock": COMMON_LOCK_LEAF,
            "hostileSameUidProtectionClaimed": False,
        },
        "application": {
            "relativePath": PACKAGED_APP_LEAF,
            "version": pins.app_version,
            "executableRelativePath": SOURCE_APP_EXECUTABLE_RELATIVE_PATH,
            "executableSize": pins.executable_size,
            "executableSha256": pins.executable_sha256,
            "bundleManifestEntryCount": pins.app_manifest_entry_count,
            "bundleManifestSha256": app_manifest_sha256,
            "codesign": ["/usr/bin/codesign", "--verify", "--deep", "--strict"],
            "codesignStatus": "verified",
            "signatureAudit": "ad-hoc-manifest-bound",
            "spctlPolicy": "rejection-expected-not-invoked",
            "xattrPolicy": OUTPUT_XATTR_POLICY_VERSION,
        },
        "environment": {
            "HOME": os.fspath(home),
            "dataRoot": os.fspath(data_root),
            "settingsRelativePath": SETTINGS_RELATIVE_PATH.as_posix(),
            "settingsSize": pins.settings_size,
            "settingsSha256": pins.settings_sha256,
        },
        "bios": {
            "relativePath": _profile_bios_relative_path(pins).as_posix(),
            "fileName": pins.bios_name,
            "size": pins.bios_size,
            "sha256": pins.bios_sha256,
        },
        "candidate": {
            "cue": {
                "path": os.fspath(cue.absolute_path),
                "size": cue.size,
                "sha256": cue.sha256,
                "provenanceHex": cue.provenance_hex,
            },
            "bin": {
                "path": os.fspath(binary.absolute_path),
                "size": binary.size,
                "sha256": binary.sha256,
                "provenanceHex": binary.provenance_hex,
            },
            "manifest": _candidate_attestation(cue, binary),
        },
        "outputMetadata": output_metadata,
        "packageEnvelope": package_envelope,
        "safety": {
            "emptyDirectories": [
                (DATA_ROOT_RELATIVE_PATH / name).as_posix()
                for name in SAFETY_DIRECTORY_NAMES
            ],
            "saveLoadStateHotkeysPresent": False,
            "textureReplacementOrDumpEnabled": False,
        },
        "launchGate": {
            "status": "EXTERNAL_RUNTIME_LAUNCH_REQUIRED",
            "builderLaunchedDuckStation": False,
            "requiredHome": os.fspath(home),
            "candidateCue": os.fspath(cue.absolute_path),
        },
    }
    closure = _sha256_bytes(_canonical_json(payload))
    payload["integrity"] = {
        "algorithm": "sha256",
        "receiptClosureSha256": closure,
    }
    return _canonical_json(payload)


def _build_receipt_fixed_point(
    destination: pathlib.Path,
    pins: BuildPins,
    app_manifest_sha256: str,
    cue: PinnedFile,
    binary: PinnedFile,
    output_metadata: Mapping[str, object],
    base_package_envelope: Mapping[str, object],
) -> tuple[bytes, dict[str, object]]:
    size_guess = 0
    for _ in range(MAX_RECEIPT_FIXED_POINT_ITERATIONS):
        envelope = _package_envelope_with_receipt_size(
            base_package_envelope, size_guess
        )
        receipt = _build_receipt(
            destination,
            pins,
            app_manifest_sha256,
            cue,
            binary,
            output_metadata,
            envelope,
        )
        observed_size = len(receipt)
        if observed_size == size_guess:
            return receipt, envelope
        size_guess = observed_size
    raise ProfileRefusal("canonical receipt size did not reach a fixed point.")


def _require_exact_package_topology(package_fd: int, pins: BuildPins) -> None:
    if set(os.listdir(package_fd)) != {PACKAGED_APP_LEAF, "home", RECEIPT_LEAF}:
        raise ProfileRefusal("package root topology is not exact.")
    home_fd = _open_relative_directory(package_fd, pathlib.Path("home"))
    try:
        if set(os.listdir(home_fd)) != {"Library"}:
            raise ProfileRefusal("HOME topology is not exact.")
    finally:
        os.close(home_fd)
    library_fd = _open_relative_directory(package_fd, pathlib.Path("home/Library"))
    try:
        if set(os.listdir(library_fd)) != {"Application Support"}:
            raise ProfileRefusal("HOME Library topology is not exact.")
    finally:
        os.close(library_fd)
    support_fd = _open_relative_directory(
        package_fd, pathlib.Path("home/Library/Application Support")
    )
    try:
        if set(os.listdir(support_fd)) != {"DuckStation"}:
            raise ProfileRefusal("Application Support topology is not exact.")
    finally:
        os.close(support_fd)
    data_fd = _open_relative_directory(package_fd, DATA_ROOT_RELATIVE_PATH)
    try:
        expected = {"settings.ini", "bios", *SAFETY_DIRECTORY_NAMES}
        if set(os.listdir(data_fd)) != expected:
            raise ProfileRefusal("DuckStation data-root topology is not exact.")
        for name in SAFETY_DIRECTORY_NAMES:
            safety_fd = _open_relative_directory(data_fd, pathlib.Path(name))
            try:
                if os.listdir(safety_fd):
                    raise ProfileRefusal(f"safety directory {name} is not empty.")
            finally:
                os.close(safety_fd)
        bios_fd = _open_relative_directory(data_fd, pathlib.Path("bios"))
        try:
            if set(os.listdir(bios_fd)) != {pins.bios_name}:
                raise ProfileRefusal("profile BIOS topology is not exact.")
        finally:
            os.close(bios_fd)
    finally:
        os.close(data_fd)


def _require_pinned_relative_file(
    root_fd: int,
    relative_path: str,
    label: str,
    expected_size: int,
    expected_sha256: str,
) -> None:
    size, digest = _hash_relative_file_at(root_fd, relative_path, label)
    if size != expected_size or digest != expected_sha256:
        raise ProfileRefusal(f"{label} does not match its direct payload pin.")


def _repin_package_critical_files(
    package_fd: int, pins: BuildPins, receipt: bytes
) -> None:
    _require_pinned_relative_file(
        package_fd,
        f"{PACKAGED_APP_LEAF}/{SOURCE_APP_EXECUTABLE_RELATIVE_PATH}",
        "packaged executable",
        pins.executable_size,
        pins.executable_sha256,
    )
    _require_pinned_relative_file(
        package_fd,
        SETTINGS_RELATIVE_PATH.as_posix(),
        "settings.ini",
        pins.settings_size,
        pins.settings_sha256,
    )
    _require_pinned_relative_file(
        package_fd,
        _profile_bios_relative_path(pins).as_posix(),
        "profile BIOS",
        pins.bios_size,
        pins.bios_sha256,
    )
    _require_pinned_relative_file(
        package_fd,
        RECEIPT_LEAF,
        "handoff receipt",
        len(receipt),
        _sha256_bytes(receipt),
    )


def _verify_package_fd(
    package_fd: int,
    package_path: pathlib.Path,
    pins: BuildPins,
    expected_app_manifest: Mapping[str, ManifestEntry],
    expected_package_manifest: Mapping[str, ManifestEntry] | None,
    receipt: bytes,
    expected_output_metadata: Mapping[str, object],
    expected_package_envelope: Mapping[str, object],
    codesign_verifier: CodesignVerifier,
) -> dict[str, ManifestEntry]:
    _require_path_matches_fd(package_path, package_fd, "package")
    _require_exact_package_topology(package_fd, pins)
    observed_output_metadata = _collect_output_metadata(
        package_fd, expected_app_manifest, pins
    )
    if observed_output_metadata != expected_output_metadata:
        raise ProfileRefusal("app/HOME output metadata differs from its receipt attestation.")
    observed_package_envelope = _collect_package_envelope(
        package_fd, pins, observed_output_metadata
    )
    if observed_package_envelope != expected_package_envelope:
        raise ProfileRefusal(
            "package root or receipt metadata differs from its receipt envelope."
        )
    package_manifest = _scan_tree_fd(package_fd, "package", xattr_policy="output")
    if expected_package_manifest is not None and package_manifest != expected_package_manifest:
        raise ProfileRefusal("postpublication package manifest differs from staging.")
    app_fd = _open_relative_directory(package_fd, pathlib.Path(PACKAGED_APP_LEAF))
    try:
        if not _name_matches_fd(package_fd, PACKAGED_APP_LEAF, app_fd):
            raise ProfileRefusal("packaged app pathname does not bind its held directory.")
        app_manifest = _scan_tree_fd(app_fd, "packaged app", xattr_policy="output")
        if _manifest_without_xattrs(app_manifest) != _manifest_without_xattrs(
            expected_app_manifest
        ):
            raise ProfileRefusal("packaged app differs from the frozen source content.")
        _require_app_manifest(app_manifest, pins)
        _require_app_version(app_fd, pins)
        codesign_verifier(package_path / PACKAGED_APP_LEAF)
        _require_path_matches_fd(package_path, package_fd, "package")
        if not _name_matches_fd(package_fd, PACKAGED_APP_LEAF, app_fd):
            raise ProfileRefusal("packaged app pathname changed during codesign.")
        after_codesign_manifest = _scan_tree_fd(
            app_fd, "packaged app after codesign", xattr_policy="output"
        )
        if after_codesign_manifest != app_manifest:
            raise ProfileRefusal("packaged app changed during codesign verification.")
    finally:
        os.close(app_fd)
    settings = _read_file_at(package_fd, SETTINGS_RELATIVE_PATH.as_posix(), "settings.ini")
    _validate_settings(settings, pins)
    _require_pinned_relative_file(
        package_fd,
        _profile_bios_relative_path(pins).as_posix(),
        "profile BIOS",
        pins.bios_size,
        pins.bios_sha256,
    )
    observed_receipt = _read_file_at(package_fd, RECEIPT_LEAF, "handoff receipt")
    _validate_receipt(observed_receipt, receipt)
    _repin_package_critical_files(package_fd, pins, receipt)
    final_package_manifest = _scan_tree_fd(
        package_fd, "package final sequential scan", xattr_policy="output"
    )
    if final_package_manifest != package_manifest:
        raise ProfileRefusal("package changed between sequential complete scans.")
    _require_path_matches_fd(package_path, package_fd, "package")
    return final_package_manifest


def _invoke_hook(
    hook: Hook | None,
    checkpoint: str,
    parent_fd: int,
    parent_path: pathlib.Path,
    stage_fd: int | None,
    stage_name: str | None,
    destination_name: str,
    cue: PinnedFile,
    binary: PinnedFile,
) -> None:
    if hook is not None:
        hook(BuildHookContext(
            checkpoint=checkpoint,
            parent_fd=parent_fd,
            parent_path=parent_path,
            stage_fd=stage_fd,
            stage_name=stage_name,
            destination_name=destination_name,
            candidate_cue=cue,
            candidate_bin=binary,
        ))


def _validate_source_app(
    app_fd: int,
    app_path: pathlib.Path,
    pins: BuildPins,
    codesign_verifier: CodesignVerifier,
) -> tuple[dict[str, ManifestEntry], str]:
    manifest = _scan_tree_fd(app_fd, "source app", xattr_policy="source-app")
    digest = _require_app_manifest(manifest, pins)
    _require_app_version(app_fd, pins)
    _require_path_matches_fd(app_path, app_fd, "source app")
    codesign_verifier(app_path)
    _require_path_matches_fd(app_path, app_fd, "source app")
    final_manifest = _scan_tree_fd(
        app_fd, "source app after codesign", xattr_policy="source-app"
    )
    if final_manifest != manifest:
        raise ProfileRefusal("source app changed during codesign verification.")
    return final_manifest, digest


def build_profile(
    source_app_path: pathlib.Path,
    destination: pathlib.Path,
    candidate_cue_path: pathlib.Path,
    candidate_bin_path: pathlib.Path,
    bios_path: pathlib.Path,
    pins: BuildPins = PRODUCTION_PINS,
    *,
    codesign_verifier: CodesignVerifier = _codesign_verify,
    hook: Hook | None = None,
) -> BuildResult:
    destination = _require_absolute_normalized(destination, "destination")
    if (
        destination.name in {"", ".", "..", COMMON_LOCK_LEAF}
        or unicodedata.normalize("NFC", destination.name) != destination.name
    ):
        raise ProfileRefusal("destination leaf is invalid.")
    _reject_symlink_segments(destination, "destination", include_leaf=False)
    if os.path.lexists(destination):
        raise ProfileRefusal("destination already exists; overwrite is forbidden.")
    parent_path = destination.parent
    parent_fd = _open_directory_absolute(
        parent_path,
        "destination parent",
        owner_required=True,
        xattr_policy="allowed-system",
    )
    lock_fd = _acquire_common_lock(parent_fd)
    app_parent_fd: int | None = None
    app_fd: int | None = None
    cue: PinnedFile | None = None
    binary: PinnedFile | None = None
    bios: PinnedFile | None = None
    stage_fd: int | None = None
    stage_name: str | None = None
    stage_published = False
    receipt = b""
    app_manifest_digest = ""
    try:
        source_app_path = _require_absolute_normalized(source_app_path, "source app")
        app_parent_fd = _open_directory_absolute(
            source_app_path.parent,
            "source app parent",
            owner_required=False,
            xattr_policy="allowed-system",
            allow_group_write=source_app_path == SOURCE_APP_PATH,
            allow_nonzero_flags=source_app_path == SOURCE_APP_PATH,
        )
        app_fd = os.open(
            source_app_path.name,
            os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW,
            dir_fd=app_parent_fd,
        )
        _require_path_matches_fd(source_app_path, app_fd, "source app")
        source_manifest, app_manifest_digest = _validate_source_app(
            app_fd, source_app_path, pins, codesign_verifier
        )
        cue = _open_pinned_file(
            candidate_cue_path,
            "candidate CUE",
            expected_name=pins.candidate_cue_name,
            expected_size=pins.candidate_cue_size,
            expected_sha256=pins.candidate_cue_sha256,
        )
        binary = _open_pinned_file(
            candidate_bin_path,
            "candidate BIN",
            expected_name=pins.candidate_bin_name,
            expected_size=pins.candidate_bin_size,
            expected_sha256=pins.candidate_bin_sha256,
        )
        _validate_cue_pair(cue, binary)
        bios = _open_pinned_file(
            bios_path,
            "BIOS",
            expected_name=pins.bios_name,
            expected_size=pins.bios_size,
            expected_sha256=pins.bios_sha256,
        )
        _validate_settings(CANONICAL_SETTINGS, pins)
        _invoke_hook(
            hook, "after-initial-validation", parent_fd, parent_path, None, None,
            destination.name, cue, binary
        )

        stage_name = f".{destination.name}.private-{secrets.token_hex(8)}"
        stage_fd = _mkdir_open(parent_fd, stage_name, 0o700)
        app_clone_fd = _mkdir_open(stage_fd, PACKAGED_APP_LEAF, 0o700)
        try:
            _copy_tree_fd(app_fd, app_clone_fd, "packaged app")
            os.fchmod(app_clone_fd, stat.S_IMODE(os.fstat(app_fd).st_mode))
            _strip_all_xattrs_fd(app_clone_fd, "packaged app")
            os.fsync(app_clone_fd)
        finally:
            os.close(app_clone_fd)

        home_fd = _mkdir_open(stage_fd, "home", 0o700)
        library_fd = _mkdir_open(home_fd, "Library", 0o700)
        application_support_fd = _mkdir_open(library_fd, "Application Support", 0o700)
        data_fd = _mkdir_open(application_support_fd, "DuckStation", 0o700)
        try:
            for name in SAFETY_DIRECTORY_NAMES:
                safety_fd = _mkdir_open(data_fd, name, 0o700)
                os.fsync(safety_fd)
                os.close(safety_fd)
            bios_directory_fd = _mkdir_open(data_fd, "bios", 0o700)
            try:
                _copy_file_fd(bios.fd, bios_directory_fd, pins.bios_name, 0o600, "BIOS")
                os.fsync(bios_directory_fd)
            finally:
                os.close(bios_directory_fd)
            _write_file_at(data_fd, "settings.ini", CANONICAL_SETTINGS, 0o600)
            os.fsync(data_fd)
        finally:
            os.close(data_fd)
            os.fsync(application_support_fd)
            os.close(application_support_fd)
            os.fsync(library_fd)
            os.close(library_fd)
            os.fsync(home_fd)
            os.close(home_fd)

        clone_path = parent_path / stage_name / PACKAGED_APP_LEAF
        codesign_verifier(clone_path)
        output_metadata = _collect_output_metadata(stage_fd, source_manifest, pins)
        _repin_held_file(cue, "candidate CUE")
        _repin_held_file(binary, "candidate BIN")
        _repin_held_file(bios, "BIOS")
        receipt_fd = _create_empty_file_at(stage_fd, RECEIPT_LEAF, 0o600)
        try:
            base_package_envelope = _collect_package_envelope(
                stage_fd, pins, output_metadata
            )
            receipt, package_envelope = _build_receipt_fixed_point(
                destination,
                pins,
                app_manifest_digest,
                cue,
                binary,
                output_metadata,
                base_package_envelope,
            )
            _rewrite_held_file_at(
                stage_fd,
                RECEIPT_LEAF,
                receipt_fd,
                receipt,
                0o600,
                "handoff receipt",
            )
            if _collect_package_envelope(
                stage_fd, pins, output_metadata
            ) != package_envelope:
                raise ProfileRefusal(
                    "written receipt metadata differs from its fixed-point envelope."
                )
        finally:
            os.close(receipt_fd)
        expected_package_manifest = _verify_package_fd(
            stage_fd,
            parent_path / stage_name,
            pins,
            source_manifest,
            None,
            receipt,
            output_metadata,
            package_envelope,
            codesign_verifier,
        )
        _invoke_hook(
            hook, "after-stage-validation", parent_fd, parent_path, stage_fd,
            stage_name, destination.name, cue, binary
        )
        # The hook precedes the final complete validation by design.
        expected_package_manifest = _verify_package_fd(
            stage_fd,
            parent_path / stage_name,
            pins,
            source_manifest,
            expected_package_manifest,
            receipt,
            output_metadata,
            package_envelope,
            codesign_verifier,
        )
        final_source_manifest, final_app_digest = _validate_source_app(
            app_fd, source_app_path, pins, codesign_verifier
        )
        if final_source_manifest != source_manifest or final_app_digest != app_manifest_digest:
            raise ProfileRefusal("source app changed between sequential complete scans.")
        _fsync_tree_fd(stage_fd)
        _invoke_hook(
            hook, "before-publish", parent_fd, parent_path, stage_fd,
            stage_name, destination.name, cue, binary
        )
        # Nothing supplied by the hook is trusted.  Its checkpoint is before the
        # final complete FD-relative validation and all direct publication pins.
        _require_path_matches_fd(parent_path, parent_fd, "destination parent")
        if not _name_matches_fd(parent_fd, stage_name, stage_fd):
            raise ProfileRefusal("wholesale staged-payload substitution detected.")
        expected_package_manifest = _verify_package_fd(
            stage_fd,
            parent_path / stage_name,
            pins,
            source_manifest,
            expected_package_manifest,
            receipt,
            output_metadata,
            package_envelope,
            codesign_verifier,
        )
        final_source_manifest, final_app_digest = _validate_source_app(
            app_fd, source_app_path, pins, codesign_verifier
        )
        if final_source_manifest != source_manifest or final_app_digest != app_manifest_digest:
            raise ProfileRefusal("source app changed before atomic publication.")
        _fsync_tree_fd(stage_fd)
        _repin_package_critical_files(stage_fd, pins, receipt)
        _repin_held_file(cue, "candidate CUE")
        _repin_held_file(binary, "candidate BIN")
        _repin_held_file(bios, "BIOS")
        _validate_cue_pair(cue, binary)
        _require_path_matches_fd(source_app_path, app_fd, "source app")
        _require_path_matches_fd(parent_path, parent_fd, "destination parent")
        if not _name_matches_fd(parent_fd, stage_name, stage_fd):
            raise ProfileRefusal("staged payload changed after final validation.")
        _rename_exclusive(parent_fd, stage_name, parent_fd, destination.name)
        published_fd: int | None = None
        try:
            try:
                published_fd = os.open(
                    destination.name,
                    os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW,
                    dir_fd=parent_fd,
                )
                published_metadata = os.fstat(published_fd)
                staged_metadata = os.fstat(stage_fd)
                if (published_metadata.st_dev, published_metadata.st_ino) != (
                    staged_metadata.st_dev, staged_metadata.st_ino
                ):
                    raise ProfileRefusal("postpublication inode differs from staging.")
                stage_published = True
                _invoke_hook(
                    hook, "after-publish", parent_fd, parent_path, published_fd,
                    None, destination.name, cue, binary
                )
                _require_path_matches_fd(parent_path, parent_fd, "destination parent")
                if not _name_matches_fd(parent_fd, destination.name, published_fd):
                    raise ProfileRefusal("published destination pathname rebound.")
                _verify_package_fd(
                    published_fd,
                    destination,
                    pins,
                    source_manifest,
                    expected_package_manifest,
                    receipt,
                    output_metadata,
                    package_envelope,
                    codesign_verifier,
                )
                _repin_package_critical_files(published_fd, pins, receipt)
                _repin_held_file(cue, "candidate CUE")
                _repin_held_file(binary, "candidate BIN")
                _repin_held_file(bios, "BIOS")
                _validate_cue_pair(cue, binary)
                _require_path_matches_fd(source_app_path, app_fd, "source app")
            except Exception as verification_exc:
                if published_fd is None:
                    raise PublishedProfileQuarantineFailed(
                        "postpublication validation could not hold the canonical "
                        f"destination for exact quarantine: {verification_exc}"
                    ) from verification_exc
                published_inode = os.fstat(published_fd).st_ino
                try:
                    quarantine_name = _quarantine_exact_inode(
                        parent_fd, destination.name, published_fd
                    )
                except Exception as quarantine_exc:
                    raise PublishedProfileQuarantineFailed(
                        f"postpublication verification failed: {verification_exc}; "
                        f"exact-inode quarantine also failed: {quarantine_exc}"
                    ) from verification_exc
                try:
                    os.fsync(parent_fd)
                except OSError as durability_exc:
                    raise PublishedProfileQuarantineDurabilityUnconfirmed(
                        str(verification_exc),
                        quarantine_name,
                        published_inode,
                        str(durability_exc),
                    ) from verification_exc
                raise PublishedProfileQuarantined(
                    str(verification_exc), quarantine_name
                ) from verification_exc
            try:
                os.fsync(parent_fd)
            except OSError as durability_exc:
                raise PublishedProfileDurabilityUnconfirmed(
                    destination, os.fstat(published_fd).st_ino, str(durability_exc)
                ) from durability_exc
        finally:
            if published_fd is not None:
                os.close(published_fd)
        return BuildResult(
            destination=destination,
            receipt_sha256=_sha256_bytes(receipt),
            receipt_schema=f"{RECEIPT_FORMAT}@{RECEIPT_VERSION}",
            app_manifest_sha256=app_manifest_digest,
            settings_sha256=pins.settings_sha256,
            launch_gate="EXTERNAL_RUNTIME_LAUNCH_REQUIRED",
        )
    finally:
        prior_exception = sys.exc_info()[1]
        cleanup_failure: (
            PrivateStageCleanupFailed
            | PrivateStageCleanupDurabilityUnconfirmed
            | None
        ) = None
        if stage_fd is not None and not stage_published:
            try:
                _cleanup_private_stage(
                    parent_fd,
                    parent_path,
                    stage_fd,
                    repr(prior_exception) if prior_exception is not None else "none",
                )
            except (
                PrivateStageCleanupFailed,
                PrivateStageCleanupDurabilityUnconfirmed,
            ) as cleanup_exc:
                cleanup_failure = cleanup_exc
            except Exception as cleanup_exc:
                cleanup_failure = PrivateStageCleanupFailed(
                    parent_path / (stage_name or "<unknown-private-stage>"),
                    os.fstat(stage_fd).st_ino,
                    str(cleanup_exc),
                    repr(prior_exception) if prior_exception is not None else "none",
                )
        if stage_fd is not None:
            os.close(stage_fd)
        if bios is not None:
            _close_pinned_file(bios)
        if binary is not None:
            _close_pinned_file(binary)
        if cue is not None:
            _close_pinned_file(cue)
        if app_fd is not None:
            os.close(app_fd)
        if app_parent_fd is not None:
            os.close(app_parent_fd)
        os.close(lock_fd)
        os.close(parent_fd)
        if cleanup_failure is not None:
            raise cleanup_failure


def verify_profile(
    source_app_path: pathlib.Path,
    destination: pathlib.Path,
    candidate_cue_path: pathlib.Path,
    candidate_bin_path: pathlib.Path,
    bios_path: pathlib.Path,
    pins: BuildPins = PRODUCTION_PINS,
    *,
    codesign_verifier: CodesignVerifier = _codesign_verify,
) -> BuildResult:
    destination = _require_absolute_normalized(destination, "destination")
    if (
        destination.name in {"", ".", "..", COMMON_LOCK_LEAF}
        or unicodedata.normalize("NFC", destination.name) != destination.name
    ):
        raise ProfileRefusal("destination leaf is invalid.")
    _reject_symlink_segments(destination, "destination", include_leaf=True)
    parent_fd = _open_directory_absolute(
        destination.parent,
        "destination parent",
        owner_required=True,
        xattr_policy="allowed-system",
    )
    lock_fd = _acquire_common_lock(parent_fd)
    package_fd: int | None = None
    app_parent_fd: int | None = None
    app_fd: int | None = None
    cue: PinnedFile | None = None
    binary: PinnedFile | None = None
    bios: PinnedFile | None = None
    try:
        package_fd = os.open(
            destination.name,
            os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW,
            dir_fd=parent_fd,
        )
        _require_path_matches_fd(destination, package_fd, "destination")
        source_app_path = _require_absolute_normalized(source_app_path, "source app")
        app_parent_fd = _open_directory_absolute(
            source_app_path.parent,
            "source app parent",
            owner_required=False,
            xattr_policy="allowed-system",
            allow_group_write=source_app_path == SOURCE_APP_PATH,
            allow_nonzero_flags=source_app_path == SOURCE_APP_PATH,
        )
        app_fd = os.open(
            source_app_path.name,
            os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW,
            dir_fd=app_parent_fd,
        )
        source_manifest, app_digest = _validate_source_app(
            app_fd, source_app_path, pins, codesign_verifier
        )
        cue = _open_pinned_file(
            candidate_cue_path, "candidate CUE",
            expected_name=pins.candidate_cue_name,
            expected_size=pins.candidate_cue_size,
            expected_sha256=pins.candidate_cue_sha256,
        )
        binary = _open_pinned_file(
            candidate_bin_path, "candidate BIN",
            expected_name=pins.candidate_bin_name,
            expected_size=pins.candidate_bin_size,
            expected_sha256=pins.candidate_bin_sha256,
        )
        bios = _open_pinned_file(
            bios_path, "BIOS",
            expected_name=pins.bios_name,
            expected_size=pins.bios_size,
            expected_sha256=pins.bios_sha256,
        )
        _validate_cue_pair(cue, binary)
        _require_exact_package_topology(package_fd, pins)
        output_metadata = _collect_output_metadata(package_fd, source_manifest, pins)
        package_envelope = _collect_package_envelope(
            package_fd, pins, output_metadata
        )
        receipt = _build_receipt(
            destination,
            pins,
            app_digest,
            cue,
            binary,
            output_metadata,
            package_envelope,
        )
        _verify_package_fd(
            package_fd, destination, pins, source_manifest, None, receipt,
            output_metadata, package_envelope, codesign_verifier
        )
        _repin_held_file(cue, "candidate CUE")
        _repin_held_file(binary, "candidate BIN")
        _repin_held_file(bios, "BIOS")
        _require_path_matches_fd(destination.parent, parent_fd, "destination parent")
        _require_path_matches_fd(destination, package_fd, "destination")
        return BuildResult(
            destination=destination,
            receipt_sha256=_sha256_bytes(receipt),
            receipt_schema=f"{RECEIPT_FORMAT}@{RECEIPT_VERSION}",
            app_manifest_sha256=app_digest,
            settings_sha256=pins.settings_sha256,
            launch_gate="EXTERNAL_RUNTIME_LAUNCH_REQUIRED",
        )
    finally:
        if bios is not None:
            _close_pinned_file(bios)
        if binary is not None:
            _close_pinned_file(binary)
        if cue is not None:
            _close_pinned_file(cue)
        if app_fd is not None:
            os.close(app_fd)
        if app_parent_fd is not None:
            os.close(app_parent_fd)
        if package_fd is not None:
            os.close(package_fd)
        os.close(lock_fd)
        os.close(parent_fd)


def _production_paths(repository_root: pathlib.Path) -> tuple[pathlib.Path, ...]:
    root = _require_absolute_normalized(repository_root, "repository root")
    _reject_symlink_segments(root, "repository root", include_leaf=True)
    return (
        SOURCE_APP_PATH,
        root / CANDIDATE_CUE_RELATIVE_PATH,
        root / CANDIDATE_BIN_RELATIVE_PATH,
        BIOS_PATH,
    )


def _self_test() -> None:
    _validate_settings(CANONICAL_SETTINGS, PRODUCTION_PINS)
    if CANONICAL_SETTINGS.count(b"Macro") != 18:
        raise ProfileRefusal("self-test macro closure is incomplete.")
    if b"Keyboard/Escape" in CANONICAL_SETTINGS or b"SDL-" in CANONICAL_SETTINGS:
        raise ProfileRefusal("self-test found a forbidden binding.")
    unsafe = CANONICAL_SETTINGS.replace(b"SaveStateOnExit = false", b"SaveStateOnExit = true")
    unsafe_pins = dataclasses.replace(
        PRODUCTION_PINS,
        settings_size=len(unsafe),
        settings_sha256=_sha256_bytes(unsafe),
    )
    try:
        _validate_settings(unsafe, unsafe_pins)
    except ProfileRefusal:
        pass
    else:
        raise ProfileRefusal("self-test accepted unsafe settings semantics.")


def _build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Build or verify the no-launch external-HOME ID65 profile."
    )
    commands = parser.add_subparsers(dest="operation", required=True)
    commands.add_parser("self-test")
    for operation in ("build", "verify"):
        command = commands.add_parser(operation)
        command.add_argument("--repository-root", required=True, type=pathlib.Path)
        command.add_argument("--destination", required=True, type=pathlib.Path)
    return parser


def main(argv: list[str] | None = None) -> int:
    arguments = _build_parser().parse_args(argv)
    try:
        if arguments.operation == "self-test":
            _self_test()
            print("SELF_TEST_OK")
            return 0
        source_app, cue, binary, bios = _production_paths(arguments.repository_root)
        operation = build_profile if arguments.operation == "build" else verify_profile
        result = operation(
            source_app,
            arguments.destination,
            cue,
            binary,
            bios,
        )
        print(
            f"{arguments.operation.upper()}_OK "
            f"destination={result.destination} "
            f"receiptSchema={result.receipt_schema} "
            f"receiptSha256={result.receipt_sha256} "
            f"appManifestSha256={result.app_manifest_sha256} "
            f"settingsSha256={result.settings_sha256} "
            f"launchGate={result.launch_gate}"
        )
        return 0
    except PublishedProfileQuarantined as exc:
        print(f"PUBLISHED_PROFILE_QUARANTINED: {exc}", file=sys.stderr)
        return 3
    except PublishedProfileDurabilityUnconfirmed as exc:
        print(f"PUBLISHED_DURABILITY_UNCONFIRMED: {exc}", file=sys.stderr)
        return 4
    except PublishedProfileQuarantineFailed as exc:
        print(f"PUBLISHED_QUARANTINE_FAILED: {exc}", file=sys.stderr)
        return 5
    except PublishedProfileQuarantineDurabilityUnconfirmed as exc:
        print(f"QUARANTINE_DURABILITY_UNCONFIRMED: {exc}", file=sys.stderr)
        return 6
    except PrivateStageCleanupFailed as exc:
        print(f"PRIVATE_STAGE_RETAINED: {exc}", file=sys.stderr)
        return 7
    except PrivateStageCleanupDurabilityUnconfirmed as exc:
        print(f"PRIVATE_STAGE_CLEANUP_DURABILITY_UNCONFIRMED: {exc}", file=sys.stderr)
        return 8
    except (OSError, ProfileRefusal, plistlib.InvalidFileException) as exc:
        print(f"REFUSED: {exc}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
