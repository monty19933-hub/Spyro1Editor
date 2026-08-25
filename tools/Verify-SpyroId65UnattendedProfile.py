#!/usr/bin/env python3
"""Fail-closed verifier for the frozen Spyro ID65 external-HOME profile.

The builder remains the authority for an exact prelaunch package verification.
Runtime snapshots deliberately use only descriptor-relative, read-only checks and
never invoke codesign, DuckStation, a shell, or any subprocess.
"""

from __future__ import annotations

import argparse
import dataclasses
import datetime as dt
import hashlib
import json
import os
import pathlib
import re
import stat
import sys
import types
import unicodedata
from collections.abc import Mapping
from typing import Any, Final


class ProfileVerificationRefusal(RuntimeError):
    """The frozen external-HOME identity could not be proven exactly."""


@dataclasses.dataclass(frozen=True)
class ProfilePins:
    profile_id: str
    package_relative_path: pathlib.PurePosixPath
    app_leaf: str
    external_home_relative_path: pathlib.PurePosixPath
    data_root_package_relative_path: pathlib.PurePosixPath
    receipt_leaf: str
    receipt_format: str
    receipt_format_version: int
    receipt_size: int
    receipt_sha256: str
    receipt_closure_sha256: str
    app_version: str
    executable_app_relative_path: pathlib.PurePosixPath
    executable_size: int
    executable_sha256: str
    app_content_manifest_format: str
    app_content_manifest_entry_count: int
    app_content_manifest_sha256: str
    output_tree_manifest_format: str
    app_output_tree_entry_count: int
    app_output_tree_manifest_sha256: str
    home_output_tree_entry_count: int
    home_output_tree_manifest_sha256: str
    package_envelope_format: str
    package_envelope_entry_count: int
    package_envelope_sha256: str
    settings_data_relative_path: pathlib.PurePosixPath
    settings_size: int
    settings_sha256: str
    bios_data_relative_path: pathlib.PurePosixPath
    bios_size: int
    bios_sha256: str
    candidate_cue_relative_path: pathlib.PurePosixPath
    candidate_cue_size: int
    candidate_cue_sha256: str
    candidate_bin_relative_path: pathlib.PurePosixPath
    candidate_bin_size: int
    candidate_bin_sha256: str
    candidate_manifest_sha256: str
    source_app_path: pathlib.Path
    source_bios_path: pathlib.Path
    host_settings_path: pathlib.Path
    host_settings_sentinel_id: str
    host_settings_size: int
    host_settings_sha256: str
    host_settings_mode: int


SCRIPT_PATH: Final = pathlib.Path(os.path.abspath(__file__))
PRODUCTION_REPOSITORY_ROOT: Final = SCRIPT_PATH.parent.parent
BUILDER_RELATIVE_PATH: Final = pathlib.PurePosixPath(
    "tools/Build-SpyroId65UnattendedProfile.py"
)
EXPECTED_BUILDER_SHA256: Final = (
    "0591f1b6bf3f61dac4bfa61d407df65a2a9862569c8d2a7678d216b95929b95c"
)

PROFILE_FORMAT: Final = "spyro-editor-id65-v3-runtime-profile-snapshot"
PROFILE_FORMAT_VERSION: Final = 2
PREFLIGHT_FORMAT: Final = "spyro-id65-unattended-profile-preflight"
PREFLIGHT_FORMAT_VERSION: Final = 1
PREFLIGHT_TIMESTAMP: Final = "1970-01-01T00:00:00.000Z"
LAUNCH_GATE: Final = "EXTERNAL_RUNTIME_LAUNCH_REQUIRED"
MAX_SOURCE_BYTES: Final = 512 * 1024
MAX_RECEIPT_BYTES: Final = 256 * 1024
READ_CHUNK_SIZE: Final = 1024 * 1024

SAFETY_DIRECTORIES: Final = (
    "cheats",
    "gamesettings",
    "memcards",
    "patches",
    "savestates",
)
VOLATILE_DIRECTORY_ROOTS: Final = frozenset({"cache", "screenshots", "videos"})
VOLATILE_ROOT_FILES: Final = frozenset({"playtime.dat"})
SNAPSHOT_KEYS: Final = frozenset({
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

_CANDIDATE_DIRECTORY: Final = pathlib.PurePosixPath(
    "_local/v5-stone-hill-level-replacement/"
    "unused-level-65-remote-blank-environment-header-repair-v3"
)
_CANDIDATE_STEM: Final = (
    "Unused-Level-65-Remote-Blank-Environment-Header-Repair-v3-"
    "Sector216-RUNTIME-CANDIDATE"
)
_BIOS_LEAF: Final = (
    "Sony PlayStation SCPH-1001 - DTLH-3000 BIOS v2.2 "
    "(1995-12-04)(Sony)(US).bin"
)

PRODUCTION_PINS: Final = ProfilePins(
    profile_id="duckstation-id65-unattended-v1",
    package_relative_path=pathlib.PurePosixPath(
        "_local/v5-stone-hill-level-replacement/duckstation-id65-unattended-v1"
    ),
    app_leaf="DuckStation-ID65-Unattended-v1.app",
    external_home_relative_path=pathlib.PurePosixPath("home"),
    data_root_package_relative_path=pathlib.PurePosixPath(
        "home/Library/Application Support/DuckStation"
    ),
    receipt_leaf="handoff-receipt.json",
    receipt_format="spyro-id65-unattended-external-home-handoff",
    receipt_format_version=1,
    receipt_size=73_908,
    receipt_sha256="a748153bdd554187fb30ee79e75d25d36c606663bd4aee8444826b7612aa27d5",
    receipt_closure_sha256=(
        "722a7ac322fa88749f17762185256281a4d6a930a290e35cdb955937fe57aead"
    ),
    app_version="0.1-11580-ge7f2f101c",
    executable_app_relative_path=pathlib.PurePosixPath("Contents/MacOS/DuckStation"),
    executable_size=22_354_208,
    executable_sha256=(
        "14c22c6e1f4bbff13c00c24f17424ef8cf7645ee9917f7d65b17cd3fe1dd1645"
    ),
    app_content_manifest_format="spyro-id65-duckstation-app-content-manifest-v1",
    app_content_manifest_entry_count=292,
    app_content_manifest_sha256=(
        "95a8142e561d6b37f228728abc8dbf034c774887213eb59bc14518d56c2d222e"
    ),
    output_tree_manifest_format="spyro-id65-output-tree-manifest-v1",
    app_output_tree_entry_count=292,
    app_output_tree_manifest_sha256=(
        "08d0a74450b709ab7e702182d64db55f6bc3395728181b052fec307c4d320ba7"
    ),
    home_output_tree_entry_count=12,
    home_output_tree_manifest_sha256=(
        "b00119fd7647647c81c7571d2507dbbce2075bd10cdcd8af8db8ae1e006fc2b0"
    ),
    package_envelope_format="spyro-id65-package-envelope-manifest-v1",
    package_envelope_entry_count=4,
    package_envelope_sha256=(
        "03e6f465647c04ee7d86add2550e1ba6818972233be419ab98290f05158ffff3"
    ),
    settings_data_relative_path=pathlib.PurePosixPath("settings.ini"),
    settings_size=2_143,
    settings_sha256="469ef98771f26395352bac1d43f90e4be14693fd4ba5316e4d71e0557a7f74f9",
    bios_data_relative_path=pathlib.PurePosixPath("bios") / _BIOS_LEAF,
    bios_size=524_288,
    bios_sha256="71af94d1e47a68c11e8fdb9f8368040601514a42a5a399cda48c7d3bff1e99d3",
    candidate_cue_relative_path=_CANDIDATE_DIRECTORY / f"{_CANDIDATE_STEM}.cue",
    candidate_cue_size=148,
    candidate_cue_sha256=(
        "ab91adb4ffcbad70bf7a226a2a4a6d0f6efe6b54fbed0b8126e59d1b8ecaf6df"
    ),
    candidate_bin_relative_path=_CANDIDATE_DIRECTORY / f"{_CANDIDATE_STEM}.bin",
    candidate_bin_size=661_547_040,
    candidate_bin_sha256=(
        "42e4d5cae98d4786023bef26517d0435b43aff365b797495386813f44ecb0c45"
    ),
    candidate_manifest_sha256=(
        "1c6824d78e8cb43f791b1b184b5cba2746352073114be120fc2a4e25f25e2e97"
    ),
    source_app_path=pathlib.Path("/Applications/DuckStation.app"),
    source_bios_path=pathlib.Path("/Users/john/Documents/DuckStation/Bios") / _BIOS_LEAF,
    host_settings_path=pathlib.Path(
        "/Users/john/Library/Application Support/DuckStation/settings.ini"
    ),
    host_settings_sentinel_id="duckstation-normal-user-settings.ini",
    host_settings_size=10_058,
    host_settings_sha256=(
        "896daa612142659aa950c16d2435f81cb034aa8b56768b91e60a4ec8f0a5ccc9"
    ),
    host_settings_mode=0o600,
)


@dataclasses.dataclass
class _HeldFile:
    parent_fd: int
    name: str
    fd: int
    device: int
    inode: int
    mode: int
    uid: int
    gid: int
    link_count: int
    size: int
    sha256: str
    xattrs: tuple[str, ...]


def _canonical_json(payload: object) -> bytes:
    return (json.dumps(payload, sort_keys=True, separators=(",", ":")) + "\n").encode(
        "utf-8"
    )


def _require_absolute_normalized(path: pathlib.Path, label: str) -> pathlib.Path:
    if not path.is_absolute() or pathlib.Path(os.path.normpath(os.fspath(path))) != path:
        raise ProfileVerificationRefusal(f"{label} must be absolute and normalized.")
    return path


def _reject_symlink_segments(path: pathlib.Path, label: str) -> None:
    _require_absolute_normalized(path, label)
    current = pathlib.Path(path.anchor)
    for segment in path.parts[1:]:
        current /= segment
        try:
            metadata = os.lstat(current)
        except FileNotFoundError as exc:
            raise ProfileVerificationRefusal(f"{label} has a missing path segment.") from exc
        if stat.S_ISLNK(metadata.st_mode):
            raise ProfileVerificationRefusal(f"{label} traverses a symbolic link.")


def _stable_read_source(path: pathlib.Path, expected_sha256: str) -> bytes:
    _reject_symlink_segments(path, "profile builder")
    fd = os.open(path, os.O_RDONLY | os.O_NOFOLLOW)
    try:
        before = os.fstat(fd)
        if (
            not stat.S_ISREG(before.st_mode)
            or before.st_uid != os.geteuid()
            or before.st_gid != os.getegid()
            or stat.S_IMODE(before.st_mode) & 0o022
            or before.st_nlink != 1
            or before.st_size > MAX_SOURCE_BYTES
        ):
            raise ProfileVerificationRefusal("profile builder metadata is unsafe.")
        chunks: list[bytes] = []
        digest = hashlib.sha256()
        observed = 0
        while True:
            chunk = os.read(fd, READ_CHUNK_SIZE)
            if not chunk:
                break
            chunks.append(chunk)
            digest.update(chunk)
            observed += len(chunk)
        after = os.fstat(fd)
        fields = (
            "st_dev", "st_ino", "st_mode", "st_uid", "st_gid", "st_nlink",
            "st_size", "st_mtime_ns", "st_ctime_ns",
        )
        if observed != before.st_size or any(
            getattr(before, field) != getattr(after, field) for field in fields
        ):
            raise ProfileVerificationRefusal("profile builder changed while read.")
        if digest.hexdigest() != expected_sha256:
            raise ProfileVerificationRefusal("profile builder SHA-256 is not frozen.")
        return b"".join(chunks)
    finally:
        os.close(fd)


def _require_audited_repository_root(repository_root: pathlib.Path) -> pathlib.Path:
    if not isinstance(repository_root, pathlib.Path):
        raise ProfileVerificationRefusal("repository root must be an explicit pathlib.Path.")
    repository_root = _require_absolute_normalized(repository_root, "repository root")
    _reject_symlink_segments(repository_root, "repository root")
    if repository_root != PRODUCTION_REPOSITORY_ROOT:
        raise ProfileVerificationRefusal(
            "repository root does not match the audited verifier checkout."
        )
    return repository_root


def _load_frozen_builder(repository_root: pathlib.Path) -> types.ModuleType:
    repository_root = _require_audited_repository_root(repository_root)
    path = repository_root / pathlib.Path(BUILDER_RELATIVE_PATH)
    source = _stable_read_source(path, EXPECTED_BUILDER_SHA256)
    module_name = "_spyro_id65_frozen_external_home_builder"
    module = types.ModuleType(module_name)
    module.__file__ = os.fspath(path)
    module.__package__ = ""
    previous = sys.modules.get(module_name)
    sys.modules[module_name] = module
    try:
        code = compile(source, os.fspath(path), "exec", dont_inherit=True)
        exec(code, module.__dict__)
    except Exception:
        if previous is None:
            sys.modules.pop(module_name, None)
        else:
            sys.modules[module_name] = previous
        raise
    return module


def _require_builder_contract(builder: types.ModuleType) -> None:
    expected = {
        "PACKAGED_APP_LEAF": PRODUCTION_PINS.app_leaf,
        "SOURCE_APP_EXECUTABLE_RELATIVE_PATH": (
            PRODUCTION_PINS.executable_app_relative_path.as_posix()
        ),
        "EXPECTED_EXECUTABLE_SHA256": PRODUCTION_PINS.executable_sha256,
        "EXPECTED_APP_MANIFEST_SHA256": PRODUCTION_PINS.app_content_manifest_sha256,
        "RECEIPT_FORMAT": PRODUCTION_PINS.receipt_format,
        "RECEIPT_VERSION": PRODUCTION_PINS.receipt_format_version,
        "EXPECTED_CANDIDATE_CUE_SHA256": PRODUCTION_PINS.candidate_cue_sha256,
        "EXPECTED_CANDIDATE_BIN_SHA256": PRODUCTION_PINS.candidate_bin_sha256,
        "EXPECTED_BIOS_SHA256": PRODUCTION_PINS.bios_sha256,
        "CANONICAL_SETTINGS_SHA256": PRODUCTION_PINS.settings_sha256,
        "PACKAGE_ENVELOPE_FORMAT": PRODUCTION_PINS.package_envelope_format,
    }
    for name, value in expected.items():
        if getattr(builder, name, None) != value:
            raise ProfileVerificationRefusal(f"frozen builder contract changed: {name}.")
    for name in (
        "verify_profile", "_open_directory_absolute", "_open_relative_directory",
        "_require_safe_metadata", "_validate_policy_metadata_fd", "_hash_fd",
        "_scan_tree_fd", "_serialize_app_manifest", "_output_tree_attestation",
        "_name_matches_fd", "_require_path_matches_fd", "_open_pinned_file",
        "_repin_held_file", "_close_pinned_file", "_provenance_hex_fd",
    ):
        if not callable(getattr(builder, name, None)):
            raise ProfileVerificationRefusal(f"frozen builder helper is missing: {name}.")


def _strict_json(raw: bytes) -> dict[str, Any]:
    def reject_duplicates(pairs: list[tuple[str, object]]) -> dict[str, object]:
        result: dict[str, object] = {}
        for key, value in pairs:
            if key in result:
                raise ProfileVerificationRefusal("handoff receipt has a duplicate JSON key.")
            result[key] = value
        return result

    def reject_constant(value: str) -> object:
        raise ProfileVerificationRefusal(f"handoff receipt uses non-finite JSON: {value}.")

    try:
        value = json.loads(
            raw.decode("utf-8", "strict"),
            object_pairs_hook=reject_duplicates,
            parse_constant=reject_constant,
        )
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise ProfileVerificationRefusal("handoff receipt is not strict JSON.") from exc
    if not isinstance(value, dict):
        raise ProfileVerificationRefusal("handoff receipt root is not an object.")
    return value


def _mapping(value: object, label: str) -> Mapping[str, Any]:
    if not isinstance(value, Mapping):
        raise ProfileVerificationRefusal(f"handoff receipt {label} is not an object.")
    return value


def _require_equal(observed: object, expected: object, label: str) -> None:
    if observed != expected:
        raise ProfileVerificationRefusal(f"handoff receipt {label} changed.")


def _validate_receipt(
    raw: bytes,
    repository_root: pathlib.Path,
    package_path: pathlib.Path,
    pins: ProfilePins,
) -> dict[str, Any]:
    if len(raw) != pins.receipt_size or hashlib.sha256(raw).hexdigest() != pins.receipt_sha256:
        raise ProfileVerificationRefusal("handoff receipt bytes do not match the frozen pin.")
    payload = _strict_json(raw)
    expected_top = {
        "format", "formatVersion", "trustModel", "application", "environment",
        "bios", "candidate", "outputMetadata", "packageEnvelope", "safety",
        "launchGate", "integrity",
    }
    if set(payload) != expected_top:
        raise ProfileVerificationRefusal("handoff receipt top-level schema changed.")
    integrity = _mapping(payload["integrity"], "integrity")
    if set(integrity) != {"algorithm", "receiptClosureSha256"}:
        raise ProfileVerificationRefusal("handoff receipt integrity schema changed.")
    _require_equal(integrity["algorithm"], "sha256", "integrity algorithm")
    _require_equal(
        integrity["receiptClosureSha256"],
        pins.receipt_closure_sha256,
        "integrity closure pin",
    )
    without_integrity = {key: value for key, value in payload.items() if key != "integrity"}
    closure = hashlib.sha256(_canonical_json(without_integrity)).hexdigest()
    _require_equal(closure, pins.receipt_closure_sha256, "integrity closure")
    if _canonical_json(payload) != raw:
        raise ProfileVerificationRefusal("handoff receipt serialization is not canonical.")

    _require_equal(payload["format"], pins.receipt_format, "format")
    _require_equal(payload["formatVersion"], pins.receipt_format_version, "format version")
    application = _mapping(payload["application"], "application")
    for key, expected in {
        "relativePath": pins.app_leaf,
        "version": pins.app_version,
        "executableRelativePath": pins.executable_app_relative_path.as_posix(),
        "executableSize": pins.executable_size,
        "executableSha256": pins.executable_sha256,
        "bundleManifestEntryCount": pins.app_content_manifest_entry_count,
        "bundleManifestSha256": pins.app_content_manifest_sha256,
        "codesignStatus": "verified",
    }.items():
        _require_equal(application.get(key), expected, f"application.{key}")

    home_path = package_path / pathlib.Path(pins.external_home_relative_path)
    data_path = package_path / pathlib.Path(pins.data_root_package_relative_path)
    environment = _mapping(payload["environment"], "environment")
    for key, expected in {
        "HOME": os.fspath(home_path),
        "dataRoot": os.fspath(data_path),
        "settingsRelativePath": (
            pins.data_root_package_relative_path / pins.settings_data_relative_path
        ).as_posix(),
        "settingsSize": pins.settings_size,
        "settingsSha256": pins.settings_sha256,
    }.items():
        _require_equal(environment.get(key), expected, f"environment.{key}")

    bios = _mapping(payload["bios"], "bios")
    for key, expected in {
        "relativePath": (
            pins.data_root_package_relative_path / pins.bios_data_relative_path
        ).as_posix(),
        "fileName": pins.bios_data_relative_path.name,
        "size": pins.bios_size,
        "sha256": pins.bios_sha256,
    }.items():
        _require_equal(bios.get(key), expected, f"bios.{key}")

    candidate = _mapping(payload["candidate"], "candidate")
    for role, relative, size, digest in (
        ("cue", pins.candidate_cue_relative_path, pins.candidate_cue_size, pins.candidate_cue_sha256),
        ("bin", pins.candidate_bin_relative_path, pins.candidate_bin_size, pins.candidate_bin_sha256),
    ):
        item = _mapping(candidate.get(role), f"candidate.{role}")
        _require_equal(item.get("path"), os.fspath(repository_root / pathlib.Path(relative)), f"candidate.{role}.path")
        _require_equal(item.get("size"), size, f"candidate.{role}.size")
        _require_equal(item.get("sha256"), digest, f"candidate.{role}.sha256")
    candidate_manifest = _mapping(candidate.get("manifest"), "candidate.manifest")
    _require_equal(
        candidate_manifest.get("manifestSha256"),
        pins.candidate_manifest_sha256,
        "candidate manifest pin",
    )

    output = _mapping(payload["outputMetadata"], "outputMetadata")
    for scope, count, digest in (
        ("application", pins.app_output_tree_entry_count, pins.app_output_tree_manifest_sha256),
        ("home", pins.home_output_tree_entry_count, pins.home_output_tree_manifest_sha256),
    ):
        manifest = _mapping(output.get(scope), f"outputMetadata.{scope}")
        _require_equal(manifest.get("format"), pins.output_tree_manifest_format, f"{scope} output format")
        _require_equal(manifest.get("entryCount"), count, f"{scope} output count")
        _require_equal(manifest.get("manifestSha256"), digest, f"{scope} output manifest")

    envelope = _mapping(payload["packageEnvelope"], "packageEnvelope")
    _require_equal(envelope.get("format"), pins.package_envelope_format, "package envelope format")
    _require_equal(envelope.get("entryCount"), pins.package_envelope_entry_count, "package envelope count")
    _require_equal(envelope.get("manifestSha256"), pins.package_envelope_sha256, "package envelope pin")
    _require_equal(
        envelope.get("rootTopology"),
        [pins.app_leaf, pins.external_home_relative_path.as_posix(), pins.receipt_leaf],
        "package root topology",
    )
    envelope_entries = envelope.get("entries")
    if not isinstance(envelope_entries, list) or len(envelope_entries) != pins.package_envelope_entry_count:
        raise ProfileVerificationRefusal("handoff receipt package envelope entries changed.")
    envelope_paths = [
        _mapping(entry, "packageEnvelope entry").get("path")
        for entry in envelope_entries
    ]
    if envelope_paths != [".", pins.app_leaf, pins.external_home_relative_path.as_posix(), pins.receipt_leaf]:
        raise ProfileVerificationRefusal("handoff receipt package envelope paths changed.")

    safety = _mapping(payload["safety"], "safety")
    expected_empty = [
        (pins.data_root_package_relative_path / name).as_posix()
        for name in SAFETY_DIRECTORIES
    ]
    _require_equal(safety.get("emptyDirectories"), expected_empty, "safety directories")
    _require_equal(safety.get("saveLoadStateHotkeysPresent"), False, "save/load hotkey policy")
    _require_equal(safety.get("textureReplacementOrDumpEnabled"), False, "texture policy")
    launch = _mapping(payload["launchGate"], "launchGate")
    _require_equal(launch.get("status"), LAUNCH_GATE, "launch gate")
    _require_equal(launch.get("builderLaunchedDuckStation"), False, "builder launch claim")
    _require_equal(launch.get("requiredHome"), os.fspath(home_path), "required HOME")
    _require_equal(
        launch.get("candidateCue"),
        os.fspath(repository_root / pathlib.Path(pins.candidate_cue_relative_path)),
        "launch candidate CUE",
    )
    return payload


def _open_child_directory(parent_fd: int, name: str, label: str, builder: types.ModuleType) -> int:
    if not name or name in {".", ".."} or "/" in name or unicodedata.normalize("NFC", name) != name:
        raise ProfileVerificationRefusal(f"{label} has a noncanonical name.")
    before = os.stat(name, dir_fd=parent_fd, follow_symlinks=False)
    fd = os.open(name, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW, dir_fd=parent_fd)
    try:
        held = os.fstat(fd)
        if (before.st_dev, before.st_ino) != (held.st_dev, held.st_ino):
            raise ProfileVerificationRefusal(f"{label} changed while opened.")
        builder._require_safe_metadata(held, label, "directory")
        builder._validate_policy_metadata_fd(fd, label, held, xattr_policy="output")
        return fd
    except Exception:
        os.close(fd)
        raise


def _open_directory_chain(root_fd: int, relative: pathlib.PurePosixPath, label: str, builder: types.ModuleType) -> int:
    fd = os.dup(root_fd)
    try:
        for segment in relative.parts:
            next_fd = _open_child_directory(fd, segment, f"{label}/{segment}", builder)
            os.close(fd)
            fd = next_fd
        return fd
    except Exception:
        os.close(fd)
        raise


def _open_held_file(
    root_fd: int,
    relative: pathlib.PurePosixPath,
    label: str,
    expected_size: int,
    expected_sha256: str,
    expected_mode: int,
    builder: types.ModuleType,
) -> _HeldFile:
    parent_relative = relative.parent
    parent_fd = os.dup(root_fd) if parent_relative == pathlib.PurePosixPath(".") else _open_directory_chain(root_fd, parent_relative, label, builder)
    name = relative.name
    fd: int | None = None
    try:
        before = os.stat(name, dir_fd=parent_fd, follow_symlinks=False)
        fd = os.open(name, os.O_RDONLY | os.O_NOFOLLOW, dir_fd=parent_fd)
        held = os.fstat(fd)
        if (before.st_dev, before.st_ino) != (held.st_dev, held.st_ino):
            raise ProfileVerificationRefusal(f"{label} changed while opened.")
        builder._require_safe_metadata(held, label, "file")
        xattrs = builder._validate_policy_metadata_fd(fd, label, held, xattr_policy="output")
        size, digest, stable = builder._hash_fd(fd, label)
        if size != expected_size or digest != expected_sha256:
            raise ProfileVerificationRefusal(f"{label} bytes do not match the frozen pin.")
        mode = stat.S_IMODE(stable.st_mode)
        if mode != expected_mode:
            raise ProfileVerificationRefusal(f"{label} mode is not frozen.")
        return _HeldFile(
            parent_fd, name, fd, stable.st_dev, stable.st_ino, mode,
            stable.st_uid, stable.st_gid, stable.st_nlink, size, digest, xattrs,
        )
    except Exception:
        if fd is not None:
            os.close(fd)
        os.close(parent_fd)
        raise


def _repin_held_file(
    handle: _HeldFile,
    label: str,
    builder: types.ModuleType,
    *,
    xattr_policy: str = "output",
) -> None:
    path_metadata = os.stat(handle.name, dir_fd=handle.parent_fd, follow_symlinks=False)
    stable = os.fstat(handle.fd)
    if (path_metadata.st_dev, path_metadata.st_ino) != (handle.device, handle.inode):
        raise ProfileVerificationRefusal(f"{label} pathname was rebound.")
    xattrs = builder._validate_policy_metadata_fd(
        handle.fd, label, stable, xattr_policy=xattr_policy
    )
    size, digest, after = builder._hash_fd(handle.fd, label)
    identity = (
        after.st_dev, after.st_ino, stat.S_IMODE(after.st_mode), after.st_uid,
        after.st_gid, after.st_nlink, size, digest, xattrs,
    )
    expected = (
        handle.device, handle.inode, handle.mode, handle.uid, handle.gid,
        handle.link_count, handle.size, handle.sha256, handle.xattrs,
    )
    if identity != expected:
        raise ProfileVerificationRefusal(f"{label} changed after its initial pin.")


def _read_held_file(handle: _HeldFile, label: str, builder: types.ModuleType) -> bytes:
    if handle.size > MAX_RECEIPT_BYTES:
        raise ProfileVerificationRefusal(f"{label} is oversized.")
    os.lseek(handle.fd, 0, os.SEEK_SET)
    chunks: list[bytes] = []
    remaining = handle.size
    while remaining:
        chunk = os.read(handle.fd, min(remaining, READ_CHUNK_SIZE))
        if not chunk:
            raise ProfileVerificationRefusal(f"{label} produced a short read.")
        chunks.append(chunk)
        remaining -= len(chunk)
    if os.read(handle.fd, 1):
        raise ProfileVerificationRefusal(f"{label} grew while read.")
    _repin_held_file(handle, label, builder)
    return b"".join(chunks)


def _open_host_sentinel(pins: ProfilePins, builder: types.ModuleType) -> _HeldFile:
    path = _require_absolute_normalized(
        pins.host_settings_path, "host normal-user settings sentinel"
    )
    _reject_symlink_segments(path, "host normal-user settings sentinel")
    parent_fd = builder._open_directory_absolute(
        path.parent,
        "host normal-user settings sentinel parent",
        owner_required=True,
        xattr_policy="allowed-system",
    )
    fd: int | None = None
    try:
        before = os.stat(path.name, dir_fd=parent_fd, follow_symlinks=False)
        fd = os.open(path.name, os.O_RDONLY | os.O_NOFOLLOW, dir_fd=parent_fd)
        held = os.fstat(fd)
        if (before.st_dev, before.st_ino) != (held.st_dev, held.st_ino):
            raise ProfileVerificationRefusal(
                "host normal-user settings sentinel changed while opened."
            )
        builder._require_safe_metadata(
            held, "host normal-user settings sentinel", "file"
        )
        xattrs = builder._validate_policy_metadata_fd(
            fd,
            "host normal-user settings sentinel",
            held,
            xattr_policy="allowed-system",
        )
        size, digest, stable = builder._hash_fd(
            fd, "host normal-user settings sentinel"
        )
        mode = stat.S_IMODE(stable.st_mode)
        if (
            size != pins.host_settings_size
            or digest != pins.host_settings_sha256
            or mode != pins.host_settings_mode
        ):
            raise ProfileVerificationRefusal(
                "host normal-user settings sentinel pin changed."
            )
        return _HeldFile(
            parent_fd,
            path.name,
            fd,
            stable.st_dev,
            stable.st_ino,
            mode,
            stable.st_uid,
            stable.st_gid,
            stable.st_nlink,
            size,
            digest,
            xattrs,
        )
    except Exception:
        if fd is not None:
            os.close(fd)
        os.close(parent_fd)
        raise


def _repin_host_sentinel(
    handle: _HeldFile, pins: ProfilePins, builder: types.ModuleType
) -> None:
    _repin_held_file(
        handle,
        "host normal-user settings sentinel",
        builder,
        xattr_policy="allowed-system",
    )
    builder._require_path_matches_fd(
        pins.host_settings_path.parent,
        handle.parent_fd,
        "host normal-user settings sentinel parent",
    )
    if not builder._name_matches_fd(handle.parent_fd, handle.name, handle.fd):
        raise ProfileVerificationRefusal(
            "host normal-user settings sentinel pathname was rebound."
        )


def _close_held_file(handle: _HeldFile | None) -> None:
    if handle is not None:
        os.close(handle.fd)
        os.close(handle.parent_fd)


def _require_exact_names(fd: int, expected: set[str], label: str) -> None:
    observed = set(os.listdir(fd))
    if observed != expected:
        raise ProfileVerificationRefusal(f"{label} topology changed: {sorted(observed)!r}.")


def _require_current_directory_binding(
    root_fd: int,
    relative: pathlib.PurePosixPath,
    held_fd: int,
    label: str,
    builder: types.ModuleType,
) -> None:
    current = _open_directory_chain(root_fd, relative, label, builder)
    try:
        left = os.fstat(current)
        right = os.fstat(held_fd)
        if (left.st_dev, left.st_ino) != (right.st_dev, right.st_ino):
            raise ProfileVerificationRefusal(f"{label} pathname was rebound.")
    finally:
        os.close(current)


def _canonical_data_relative_path(relative: str) -> str:
    if (
        not isinstance(relative, str)
        or not relative
        or "\\" in relative
        or any(ord(character) < 0x20 or ord(character) == 0x7F for character in relative)
    ):
        raise ProfileVerificationRefusal(f"data path is noncanonical: {relative!r}.")
    if relative == ".":
        return relative
    path = pathlib.PurePosixPath(relative)
    if (
        path.is_absolute()
        or path.as_posix() != relative
        or any(
            segment in {"", ".", ".."}
            or unicodedata.normalize("NFC", segment) != segment
            for segment in path.parts
        )
    ):
        raise ProfileVerificationRefusal(f"data path is noncanonical: {relative!r}.")
    return relative


def _entries_by_path(value: object, label: str) -> dict[str, Mapping[str, Any]]:
    if not isinstance(value, list):
        raise ProfileVerificationRefusal(f"handoff receipt {label} entries are malformed.")
    result: dict[str, Mapping[str, Any]] = {}
    for raw_entry in value:
        entry = _mapping(raw_entry, f"{label} entry")
        path = entry.get("path")
        if not isinstance(path, str) or path in result:
            raise ProfileVerificationRefusal(f"handoff receipt {label} paths are malformed.")
        result[path] = entry
    return result


def _require_pinned_directory_metadata(
    fd: int,
    receipt_entry: Mapping[str, Any],
    label: str,
    builder: types.ModuleType,
    *,
    link_growth: int = 0,
) -> None:
    metadata = os.fstat(fd)
    builder._require_safe_metadata(metadata, label, "directory")
    builder._validate_policy_metadata_fd(fd, label, metadata, xattr_policy="output")
    if (
        receipt_entry.get("kind") != "directory"
        or receipt_entry.get("mode") != "0700"
        or receipt_entry.get("size") is not None
        or receipt_entry.get("sha256") is not None
        or not isinstance(receipt_entry.get("linkCount"), int)
        or link_growth < 0
    ):
        raise ProfileVerificationRefusal(f"handoff receipt {label} metadata is not frozen.")
    live = {
        "mode": f"{stat.S_IMODE(metadata.st_mode):04o}",
        "linkCount": metadata.st_nlink,
        "provenanceHex": builder._provenance_hex_fd(fd, label),
    }
    expected = {
        "mode": receipt_entry["mode"],
        "linkCount": receipt_entry["linkCount"] + link_growth,
        "provenanceHex": receipt_entry.get("provenanceHex"),
    }
    if live != expected:
        raise ProfileVerificationRefusal(f"live {label} metadata changed from its receipt pin.")


def _held_provenance_hex(handle: _HeldFile, label: str) -> str | None:
    if not handle.xattrs:
        return None
    if len(handle.xattrs) != 1 or not handle.xattrs[0].startswith(
        "com.apple.provenance="
    ):
        raise ProfileVerificationRefusal(f"live {label} xattr record is noncanonical.")
    return handle.xattrs[0].split("=", 1)[1]


def _require_pinned_file_metadata(
    handle: _HeldFile,
    receipt_entry: Mapping[str, Any],
    label: str,
    *,
    expected_sha256: str | None,
) -> None:
    expected = {
        "kind": "file",
        "mode": f"{handle.mode:04o}",
        "linkCount": handle.link_count,
        "size": handle.size,
        "provenanceHex": _held_provenance_hex(handle, label),
    }
    observed = {
        "kind": receipt_entry.get("kind"),
        "mode": receipt_entry.get("mode"),
        "linkCount": receipt_entry.get("linkCount"),
        "size": receipt_entry.get("size"),
        "provenanceHex": receipt_entry.get("provenanceHex"),
    }
    if observed != expected:
        raise ProfileVerificationRefusal(f"live {label} metadata changed from its receipt pin.")
    if expected_sha256 is not None and receipt_entry.get("sha256") != expected_sha256:
        raise ProfileVerificationRefusal(f"handoff receipt {label} content pin changed.")


def _require_live_directory_metadata(
    receipt_payload: Mapping[str, Any],
    builder: types.ModuleType,
    *,
    package_fd: int,
    home_fd: int,
    library_fd: int,
    support_fd: int,
    data_fd: int,
    bios_directory_fd: int,
    safety_fds: Mapping[str, int],
    data_manifest: Mapping[str, Any],
    receipt_file: _HeldFile,
    settings_file: _HeldFile,
    bios_file: _HeldFile,
    pins: ProfilePins,
) -> None:
    envelope = _mapping(receipt_payload["packageEnvelope"], "packageEnvelope")
    envelope_entries = _entries_by_path(envelope.get("entries"), "packageEnvelope")
    if set(envelope_entries) != {
        ".", pins.app_leaf, pins.external_home_relative_path.as_posix(), pins.receipt_leaf
    }:
        raise ProfileVerificationRefusal("handoff receipt package envelope path set changed.")
    _require_pinned_directory_metadata(
        package_fd, envelope_entries["."], "profile package root", builder
    )
    _require_pinned_file_metadata(
        receipt_file,
        envelope_entries[pins.receipt_leaf],
        "handoff receipt",
        expected_sha256="SELF-CANONICAL-RECEIPT-CLOSURE",
    )

    output = _mapping(receipt_payload["outputMetadata"], "outputMetadata")
    home_manifest = _mapping(output.get("home"), "outputMetadata.home")
    home_entries = _entries_by_path(home_manifest.get("entries"), "outputMetadata.home")
    data_prefix = "Library/Application Support/DuckStation"
    required = {
        ".": (home_fd, "external HOME"),
        "Library": (library_fd, "external HOME Library"),
        "Library/Application Support": (support_fd, "external HOME Application Support"),
        data_prefix: (data_fd, "DuckStation data root"),
        f"{data_prefix}/bios": (bios_directory_fd, "DuckStation BIOS directory"),
    }
    for name in SAFETY_DIRECTORIES:
        required[f"{data_prefix}/{name}"] = (
            safety_fds[name], f"DuckStation safety directory {name}"
        )
    if any(path not in home_entries for path in required):
        raise ProfileVerificationRefusal(
            "handoff receipt omits fixed external-HOME directory metadata."
        )
    volatile_top_level_entries = {
        path for path, entry in data_manifest.items()
        if path != "."
        and len(pathlib.PurePosixPath(path).parts) == 1
        and (
            (entry.kind == "directory" and path in VOLATILE_DIRECTORY_ROOTS)
            or (entry.kind == "file" and path in VOLATILE_ROOT_FILES)
        )
    }
    for path, (fd, label) in required.items():
        # On the pinned APFS host st_nlink grows for each immediate child,
        # including the approved playtime.dat regular file, but not for nested
        # descendants below cache/screenshots/videos.
        growth = len(volatile_top_level_entries) if path == data_prefix else 0
        _require_pinned_directory_metadata(
            fd, home_entries[path], label, builder, link_growth=growth
        )
    settings_home_path = (
        pathlib.PurePosixPath(data_prefix) / pins.settings_data_relative_path
    ).as_posix()
    bios_home_path = (
        pathlib.PurePosixPath(data_prefix) / pins.bios_data_relative_path
    ).as_posix()
    if settings_home_path not in home_entries or bios_home_path not in home_entries:
        raise ProfileVerificationRefusal(
            "handoff receipt omits immutable external-HOME file metadata."
        )
    _require_pinned_file_metadata(
        settings_file,
        home_entries[settings_home_path],
        "external-HOME settings.ini",
        expected_sha256=pins.settings_sha256,
    )
    _require_pinned_file_metadata(
        bios_file,
        home_entries[bios_home_path],
        "external-HOME BIOS",
        expected_sha256=pins.bios_sha256,
    )


def _data_files_from_manifest(
    manifest: Mapping[str, Any], pins: ProfilePins
) -> list[dict[str, object]]:
    required_files = {
        pins.settings_data_relative_path.as_posix(): (pins.settings_size, pins.settings_sha256),
        pins.bios_data_relative_path.as_posix(): (pins.bios_size, pins.bios_sha256),
    }
    for safety in SAFETY_DIRECTORIES:
        entry = manifest.get(safety)
        if entry is None or entry.kind != "directory":
            raise ProfileVerificationRefusal(f"safety directory is missing: {safety}.")
        if any(path.startswith(f"{safety}/") for path in manifest):
            raise ProfileVerificationRefusal(f"safety directory is not empty: {safety}.")

    data_files: list[dict[str, object]] = []
    for relative in sorted(manifest, key=lambda value: value.encode("utf-8")):
        _canonical_data_relative_path(relative)
        entry = manifest[relative]
        if relative == ".":
            if entry.kind != "directory":
                raise ProfileVerificationRefusal("data root manifest is malformed.")
            continue
        parts = pathlib.PurePosixPath(relative).parts
        first = parts[0]
        if entry.kind == "directory":
            allowed = (
                relative == "bios"
                or relative in SAFETY_DIRECTORIES
                or first in VOLATILE_DIRECTORY_ROOTS
            )
            if not allowed:
                raise ProfileVerificationRefusal(f"unknown data directory: {relative}.")
            if relative.startswith("bios/"):
                raise ProfileVerificationRefusal(f"unknown BIOS directory: {relative}.")
            continue
        if entry.kind != "file" or entry.size is None or entry.sha256 is None:
            raise ProfileVerificationRefusal(f"data entry is not a regular file: {relative}.")
        allowed_file = (
            relative in required_files
            or relative in VOLATILE_ROOT_FILES
            or (first in VOLATILE_DIRECTORY_ROOTS and len(parts) > 1)
        )
        if not allowed_file:
            raise ProfileVerificationRefusal(f"unknown data file: {relative}.")
        if relative in required_files and (entry.size, entry.sha256) != required_files[relative]:
            raise ProfileVerificationRefusal(f"immutable data pin changed: {relative}.")
        data_files.append({
            "relativePath": relative,
            "byteLength": entry.size,
            "sha256": entry.sha256,
        })
    observed_required = {entry["relativePath"] for entry in data_files} & set(required_files)
    if observed_required != set(required_files):
        raise ProfileVerificationRefusal("runtime data omits settings.ini or the pinned BIOS.")
    return data_files


_IDENTIFIER_RE: Final = re.compile(r"[A-Za-z0-9][A-Za-z0-9._:-]{0,127}")
_UTC_RE: Final = re.compile(r"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z")


def _identifier(value: str, label: str) -> str:
    if not isinstance(value, str) or _IDENTIFIER_RE.fullmatch(value) is None:
        raise ProfileVerificationRefusal(f"{label} is not canonical.")
    return value


def _timestamp(value: str) -> str:
    if _UTC_RE.fullmatch(value) is None:
        raise ProfileVerificationRefusal("capturedAtUtc is not canonical UTC.")
    try:
        parsed = dt.datetime.strptime(value, "%Y-%m-%dT%H:%M:%S.%fZ")
    except ValueError as exc:
        raise ProfileVerificationRefusal("capturedAtUtc is not a real UTC instant.") from exc
    if parsed.strftime("%Y-%m-%dT%H:%M:%S.%f")[:-3] + "Z" != value:
        raise ProfileVerificationRefusal("capturedAtUtc is not millisecond canonical.")
    return value


def _read_receipt_only(
    builder: types.ModuleType,
    repository_root: pathlib.Path,
    pins: ProfilePins,
) -> bytes:
    package_path = repository_root / pathlib.Path(pins.package_relative_path)
    package_fd = builder._open_directory_absolute(
        package_path, "profile package", owner_required=True, xattr_policy="output"
    )
    receipt: _HeldFile | None = None
    try:
        _require_exact_names(
            package_fd,
            {pins.app_leaf, pins.external_home_relative_path.as_posix(), pins.receipt_leaf},
            "profile package",
        )
        receipt = _open_held_file(
            package_fd, pathlib.PurePosixPath(pins.receipt_leaf), "handoff receipt",
            pins.receipt_size, pins.receipt_sha256, 0o600, builder,
        )
        raw = _read_held_file(receipt, "handoff receipt", builder)
        _validate_receipt(raw, repository_root, package_path, pins)
        builder._require_path_matches_fd(package_path, package_fd, "profile package")
        return raw
    finally:
        _close_held_file(receipt)
        os.close(package_fd)


def _capture_runtime_snapshot_with_builder(
    builder: types.ModuleType,
    repository_root: pathlib.Path,
    *,
    phase: str,
    session_id: str,
    process_instance_id: str,
    captured_at_utc: str | None,
    pins: ProfilePins,
) -> dict[str, object]:
    repository_root = _require_absolute_normalized(repository_root, "repository root")
    if phase not in {"preflight", "before", "after"}:
        raise ProfileVerificationRefusal("profile snapshot phase is unknown.")
    session_id = _identifier(session_id, "sessionId")
    process_instance_id = _identifier(process_instance_id, "processInstanceId")
    if captured_at_utc is None:
        if phase != "preflight":
            raise ProfileVerificationRefusal("before/after snapshots require capturedAtUtc.")
        captured_at_utc = PREFLIGHT_TIMESTAMP
    captured_at_utc = _timestamp(captured_at_utc)
    snapshot_id = f"{session_id}.profile-{phase}"
    _identifier(snapshot_id, "snapshotId")

    package_path = repository_root / pathlib.Path(pins.package_relative_path)
    package_fd = builder._open_directory_absolute(
        package_path, "profile package", owner_required=True, xattr_policy="output"
    )
    app_fd: int | None = None
    home_fd: int | None = None
    library_fd: int | None = None
    support_fd: int | None = None
    data_fd: int | None = None
    bios_directory_fd: int | None = None
    safety_fds: dict[str, int] = {}
    receipt: _HeldFile | None = None
    executable: _HeldFile | None = None
    settings: _HeldFile | None = None
    bios: _HeldFile | None = None
    host = None
    try:
        _require_exact_names(
            package_fd,
            {pins.app_leaf, pins.external_home_relative_path.as_posix(), pins.receipt_leaf},
            "profile package",
        )
        app_fd = _open_child_directory(package_fd, pins.app_leaf, "packaged app", builder)
        home_fd = _open_child_directory(
            package_fd, pins.external_home_relative_path.as_posix(), "external HOME", builder
        )
        _require_exact_names(home_fd, {"Library"}, "external HOME")
        library_fd = _open_child_directory(home_fd, "Library", "external HOME/Library", builder)
        _require_exact_names(library_fd, {"Application Support"}, "external HOME/Library")
        support_fd = _open_child_directory(
            library_fd, "Application Support", "external HOME/Application Support", builder
        )
        _require_exact_names(support_fd, {"DuckStation"}, "external HOME/Application Support")
        data_fd = _open_child_directory(
            support_fd, "DuckStation", "DuckStation data root", builder
        )
        bios_directory_fd = _open_child_directory(
            data_fd, "bios", "DuckStation BIOS directory", builder
        )
        _require_exact_names(
            bios_directory_fd, {pins.bios_data_relative_path.name},
            "DuckStation BIOS directory",
        )
        for name in SAFETY_DIRECTORIES:
            safety_fd = _open_child_directory(
                data_fd, name, f"DuckStation safety directory {name}", builder
            )
            safety_fds[name] = safety_fd
            _require_exact_names(
                safety_fd, set(), f"DuckStation safety directory {name}"
            )

        receipt = _open_held_file(
            package_fd, pathlib.PurePosixPath(pins.receipt_leaf), "handoff receipt",
            pins.receipt_size, pins.receipt_sha256, 0o600, builder,
        )
        receipt_raw = _read_held_file(receipt, "handoff receipt", builder)
        receipt_payload = _validate_receipt(receipt_raw, repository_root, package_path, pins)
        executable = _open_held_file(
            app_fd, pins.executable_app_relative_path, "packaged executable",
            pins.executable_size, pins.executable_sha256, 0o755, builder,
        )
        settings = _open_held_file(
            data_fd, pins.settings_data_relative_path, "external-HOME settings.ini",
            pins.settings_size, pins.settings_sha256, 0o600, builder,
        )
        bios = _open_held_file(
            data_fd, pins.bios_data_relative_path, "external-HOME BIOS",
            pins.bios_size, pins.bios_sha256, 0o600, builder,
        )
        host = _open_host_sentinel(pins, builder)

        app_manifest_first = builder._scan_tree_fd(
            app_fd, "packaged app", xattr_policy="output"
        )
        app_content_sha = hashlib.sha256(
            builder._serialize_app_manifest(app_manifest_first)
        ).hexdigest()
        if (
            len(app_manifest_first) != pins.app_content_manifest_entry_count
            or app_content_sha != pins.app_content_manifest_sha256
        ):
            raise ProfileVerificationRefusal("packaged app content manifest changed.")
        app_output = builder._output_tree_attestation(app_manifest_first, "application")
        expected_app_output = _mapping(
            _mapping(receipt_payload["outputMetadata"], "outputMetadata").get("application"),
            "outputMetadata.application",
        )
        if app_output != expected_app_output:
            raise ProfileVerificationRefusal("packaged app output-tree attestation changed.")

        data_manifest_first = builder._scan_tree_fd(
            data_fd, "DuckStation runtime data", xattr_policy="output"
        )
        _data_files_from_manifest(data_manifest_first, pins)

        app_manifest_second = builder._scan_tree_fd(
            app_fd, "packaged app stable rescan", xattr_policy="output"
        )
        data_manifest_second = builder._scan_tree_fd(
            data_fd, "DuckStation runtime data stable rescan", xattr_policy="output"
        )
        if app_manifest_second != app_manifest_first:
            raise ProfileVerificationRefusal("packaged app changed across stable scans.")
        if data_manifest_second != data_manifest_first:
            raise ProfileVerificationRefusal("DuckStation runtime data changed across stable scans.")

        def repin_critical_and_bindings() -> None:
            _require_exact_names(
                package_fd,
                {pins.app_leaf, pins.external_home_relative_path.as_posix(), pins.receipt_leaf},
                "profile package",
            )
            for handle, label in (
                (receipt, "handoff receipt"),
                (executable, "packaged executable"),
                (settings, "external-HOME settings.ini"),
                (bios, "external-HOME BIOS"),
            ):
                _repin_held_file(handle, label, builder)
            _repin_host_sentinel(host, pins, builder)
            builder._require_path_matches_fd(package_path, package_fd, "profile package")
            if not builder._name_matches_fd(package_fd, pins.app_leaf, app_fd):
                raise ProfileVerificationRefusal("packaged app pathname was rebound.")
            if not builder._name_matches_fd(
                package_fd, pins.external_home_relative_path.as_posix(), home_fd
            ):
                raise ProfileVerificationRefusal("external HOME pathname was rebound.")
            bindings = {
                pins.external_home_relative_path: (home_fd, "external HOME"),
                pins.external_home_relative_path / "Library": (
                    library_fd, "external HOME Library"
                ),
                pins.external_home_relative_path / "Library/Application Support": (
                    support_fd, "external HOME Application Support"
                ),
                pins.data_root_package_relative_path: (data_fd, "DuckStation data root"),
                pins.data_root_package_relative_path / "bios": (
                    bios_directory_fd, "DuckStation BIOS directory"
                ),
            }
            for name, safety_fd in safety_fds.items():
                bindings[pins.data_root_package_relative_path / name] = (
                    safety_fd, f"DuckStation safety directory {name}"
                )
            for relative, (held_fd, label) in bindings.items():
                _require_current_directory_binding(
                    package_fd, relative, held_fd, label, builder
                )
            _require_exact_names(home_fd, {"Library"}, "external HOME")
            _require_exact_names(
                library_fd, {"Application Support"}, "external HOME Library"
            )
            _require_exact_names(
                support_fd, {"DuckStation"}, "external HOME Application Support"
            )
            _require_exact_names(
                bios_directory_fd, {pins.bios_data_relative_path.name},
                "DuckStation BIOS directory",
            )
            for name, safety_fd in safety_fds.items():
                _require_exact_names(
                    safety_fd, set(), f"DuckStation safety directory {name}"
                )

        _require_live_directory_metadata(
            receipt_payload,
            builder,
            package_fd=package_fd,
            home_fd=home_fd,
            library_fd=library_fd,
            support_fd=support_fd,
            data_fd=data_fd,
            bios_directory_fd=bios_directory_fd,
            safety_fds=safety_fds,
            data_manifest=data_manifest_second,
            receipt_file=receipt,
            settings_file=settings,
            bios_file=bios,
            pins=pins,
        )
        repin_critical_and_bindings()

        app_manifest_final = builder._scan_tree_fd(
            app_fd, "packaged app final scan", xattr_policy="output"
        )
        data_manifest_final = builder._scan_tree_fd(
            data_fd, "DuckStation runtime data final scan", xattr_policy="output"
        )
        if app_manifest_final != app_manifest_first:
            raise ProfileVerificationRefusal("packaged app changed before its final scan.")
        if data_manifest_final != data_manifest_first:
            raise ProfileVerificationRefusal(
                "DuckStation runtime data changed before its final scan."
            )
        repin_critical_and_bindings()
        _require_live_directory_metadata(
            receipt_payload,
            builder,
            package_fd=package_fd,
            home_fd=home_fd,
            library_fd=library_fd,
            support_fd=support_fd,
            data_fd=data_fd,
            bios_directory_fd=bios_directory_fd,
            safety_fds=safety_fds,
            data_manifest=data_manifest_final,
            receipt_file=receipt,
            settings_file=settings,
            bios_file=bios,
            pins=pins,
        )
        # These closing complete scans are deliberately the final filesystem
        # integrity reads.  Snapshot construction below is pure in-memory work.
        # This is cooperative, non-hostile same-UID evidence, not an atomic
        # adversarial snapshot claim.
        app_manifest_closing = builder._scan_tree_fd(
            app_fd, "packaged app closing scan", xattr_policy="output"
        )
        data_manifest_closing = builder._scan_tree_fd(
            data_fd, "DuckStation runtime data closing scan", xattr_policy="output"
        )
        if app_manifest_closing != app_manifest_first:
            raise ProfileVerificationRefusal("packaged app changed before its closing scan.")
        if data_manifest_closing != data_manifest_first:
            raise ProfileVerificationRefusal(
                "DuckStation runtime data changed before its closing scan."
            )
        data_files = _data_files_from_manifest(data_manifest_closing, pins)

        snapshot: dict[str, object] = {
            "format": PROFILE_FORMAT,
            "formatVersion": PROFILE_FORMAT_VERSION,
            "snapshotId": snapshot_id,
            "phase": phase,
            "sessionId": session_id,
            "processInstanceId": process_instance_id,
            "profileId": pins.profile_id,
            "capturedAtUtc": captured_at_utc,
            "duckStationExecutableSha256": pins.executable_sha256,
            "handoffReceiptSha256": pins.receipt_sha256,
            "appContentManifestSha256": pins.app_content_manifest_sha256,
            "appOutputTreeManifestSha256": pins.app_output_tree_manifest_sha256,
            "packageEnvelopeSha256": pins.package_envelope_sha256,
            "externalHomeSettingsSha256": pins.settings_sha256,
            "externalHomeBiosSha256": pins.bios_sha256,
            "hostNormalSettingsSentinelId": pins.host_settings_sentinel_id,
            "hostNormalSettingsSentinelSha256": pins.host_settings_sha256,
            "dataFiles": data_files,
        }
        if set(snapshot) != SNAPSHOT_KEYS:
            raise ProfileVerificationRefusal("internal snapshot-v2 key closure failed.")
        return snapshot
    except ProfileVerificationRefusal:
        raise
    except Exception as exc:
        if isinstance(exc, getattr(builder, "ProfileRefusal", ())):
            raise ProfileVerificationRefusal(str(exc)) from exc
        raise
    finally:
        if host is not None:
            _close_held_file(host)
        _close_held_file(bios)
        _close_held_file(settings)
        _close_held_file(executable)
        _close_held_file(receipt)
        if data_fd is not None:
            os.close(data_fd)
        for safety_fd in safety_fds.values():
            os.close(safety_fd)
        if bios_directory_fd is not None:
            os.close(bios_directory_fd)
        if support_fd is not None:
            os.close(support_fd)
        if library_fd is not None:
            os.close(library_fd)
        if home_fd is not None:
            os.close(home_fd)
        if app_fd is not None:
            os.close(app_fd)
        os.close(package_fd)


def capture_runtime_snapshot(
    repository_root: pathlib.Path,
    *,
    phase: str,
    session_id: str,
    process_instance_id: str,
    captured_at_utc: str | None = None,
) -> dict[str, object]:
    """Return an exact canonical snapshot-v2 object without using subprocesses."""
    repository_root = _require_audited_repository_root(repository_root)
    builder = _load_frozen_builder(repository_root)
    _require_builder_contract(builder)
    return _capture_runtime_snapshot_with_builder(
        builder,
        repository_root,
        phase=phase,
        session_id=session_id,
        process_instance_id=process_instance_id,
        captured_at_utc=captured_at_utc,
        pins=PRODUCTION_PINS,
    )


def _verify_prelaunch_with_builder(
    builder: types.ModuleType,
    repository_root: pathlib.Path,
    pins: ProfilePins,
) -> dict[str, object]:
    package_path = repository_root / pathlib.Path(pins.package_relative_path)
    receipt_before = _read_receipt_only(builder, repository_root, pins)
    try:
        result = builder.verify_profile(
            pins.source_app_path,
            package_path,
            repository_root / pathlib.Path(pins.candidate_cue_relative_path),
            repository_root / pathlib.Path(pins.candidate_bin_relative_path),
            pins.source_bios_path,
            builder.PRODUCTION_PINS,
        )
    except getattr(builder, "ProfileRefusal", Exception) as exc:
        raise ProfileVerificationRefusal(str(exc)) from exc
    receipt_after = _read_receipt_only(builder, repository_root, pins)
    if receipt_after != receipt_before:
        raise ProfileVerificationRefusal("handoff receipt changed across builder verification.")
    expected_result = {
        "destination": package_path,
        "receipt_sha256": pins.receipt_sha256,
        "receipt_schema": f"{pins.receipt_format}@{pins.receipt_format_version}",
        "app_manifest_sha256": pins.app_content_manifest_sha256,
        "settings_sha256": pins.settings_sha256,
        "launch_gate": LAUNCH_GATE,
    }
    for name, expected in expected_result.items():
        if getattr(result, name, None) != expected:
            raise ProfileVerificationRefusal(f"builder verification result changed: {name}.")
    snapshot = _capture_runtime_snapshot_with_builder(
        builder,
        repository_root,
        phase="preflight",
        session_id="preflight",
        process_instance_id="not-applicable",
        captured_at_utc=None,
        pins=pins,
    )
    return {
        "format": PREFLIGHT_FORMAT,
        "formatVersion": PREFLIGHT_FORMAT_VERSION,
        "status": "PREFLIGHT_READY",
        "profileId": pins.profile_id,
        "receiptSha256": pins.receipt_sha256,
        "appContentManifestSha256": pins.app_content_manifest_sha256,
        "appOutputTreeManifestSha256": pins.app_output_tree_manifest_sha256,
        "homeOutputTreeManifestSha256": pins.home_output_tree_manifest_sha256,
        "packageEnvelopeSha256": pins.package_envelope_sha256,
        "settingsSha256": pins.settings_sha256,
        "biosSha256": pins.bios_sha256,
        "candidateCueSha256": pins.candidate_cue_sha256,
        "candidateBinSha256": pins.candidate_bin_sha256,
        "hostNormalSettingsSentinelSha256": pins.host_settings_sha256,
        "launchGate": LAUNCH_GATE,
        "snapshot": snapshot,
    }


def verify_prelaunch(repository_root: pathlib.Path) -> dict[str, object]:
    """Delegate exact package verification to the hash-pinned frozen builder."""
    repository_root = _require_audited_repository_root(repository_root)
    builder = _load_frozen_builder(repository_root)
    _require_builder_contract(builder)
    return _verify_prelaunch_with_builder(builder, repository_root, PRODUCTION_PINS)


def _self_test() -> None:
    builder = _load_frozen_builder(PRODUCTION_REPOSITORY_ROOT)
    _require_builder_contract(builder)
    if len(SNAPSHOT_KEYS) != 18:
        raise ProfileVerificationRefusal("snapshot-v2 key count changed.")
    if not (
        _data_path_is_allowed("cache/a/b")
        and _data_path_is_allowed("screenshots/a.png")
        and _data_path_is_allowed("videos/a.mp4")
        and _data_path_is_allowed("playtime.dat")
        and not _data_path_is_allowed("cache-evil/a")
        and not _data_path_is_allowed("cache\\a")
        and not _data_path_is_allowed("cache//a")
        and not _data_path_is_allowed(".")
        and not _data_path_is_allowed("savestates/a")
    ):
        raise ProfileVerificationRefusal("segment-safe volatile allowlist self-test failed.")
    receipt = _read_receipt_only(builder, PRODUCTION_REPOSITORY_ROOT, PRODUCTION_PINS)
    if hashlib.sha256(receipt).hexdigest() != PRODUCTION_PINS.receipt_sha256:
        raise ProfileVerificationRefusal("receipt self-test pin failed.")


def _data_path_is_allowed(relative: str) -> bool:
    if relative == ".":
        return False
    try:
        _canonical_data_relative_path(relative)
    except ProfileVerificationRefusal:
        return False
    path = pathlib.PurePosixPath(relative)
    return (
        relative in VOLATILE_ROOT_FILES
        or (path.parts[0] in VOLATILE_DIRECTORY_ROOTS and len(path.parts) > 1)
    )


def _build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Verify the frozen external-HOME DuckStation ID65 profile."
    )
    commands = parser.add_subparsers(dest="operation", required=True)
    commands.add_parser("self-test")
    commands.add_parser("prelaunch")
    commands.add_parser("snapshot-preflight")
    return parser


def main(argv: list[str] | None = None) -> int:
    arguments = _build_parser().parse_args(argv)
    try:
        if arguments.operation == "self-test":
            _self_test()
            print("SELF_TEST_OK")
            return 0
        if arguments.operation == "prelaunch":
            print(
                _canonical_json(verify_prelaunch(PRODUCTION_REPOSITORY_ROOT)).decode("utf-8"),
                end="",
            )
            return 0
        if arguments.operation == "snapshot-preflight":
            snapshot = capture_runtime_snapshot(
                PRODUCTION_REPOSITORY_ROOT,
                phase="preflight",
                session_id="preflight",
                process_instance_id="not-applicable",
            )
            print(_canonical_json(snapshot).decode("utf-8"), end="")
            return 0
        raise ProfileVerificationRefusal("unknown operation.")
    except ProfileVerificationRefusal as exc:
        print(f"REFUSED: {exc}", file=sys.stderr)
        return 2
    except Exception as exc:
        print(f"REFUSED: unexpected verifier failure: {exc}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
