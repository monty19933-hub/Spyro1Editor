#!/usr/bin/env python3
"""Focused offline/native tests for the external-HOME profile verifier."""

from __future__ import annotations

import contextlib
import dataclasses
import hashlib
import importlib.util
import json
import os
import pathlib
import shutil
import stat
import subprocess
import sys
import tempfile
import types
from collections.abc import Callable, Iterator, Mapping
from typing import Any


SCRIPT_PATH = pathlib.Path(os.path.abspath(__file__))
REPOSITORY_ROOT = SCRIPT_PATH.parent.parent
VERIFIER_PATH = SCRIPT_PATH.parent / "Verify-SpyroId65UnattendedProfile.py"


class TestFailure(RuntimeError):
    pass


def require(condition: object, message: str) -> None:
    if not condition:
        raise TestFailure(message)


def load_verifier() -> types.ModuleType:
    spec = importlib.util.spec_from_file_location("spyro_id65_profile_verifier_tested", VERIFIER_PATH)
    if spec is None or spec.loader is None:
        raise TestFailure("verifier import specification could not be created.")
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    try:
        spec.loader.exec_module(module)
    except Exception:
        sys.modules.pop(spec.name, None)
        raise
    return module


VERIFIER = load_verifier()
BUILDER = VERIFIER._load_frozen_builder(REPOSITORY_ROOT)
VERIFIER._require_builder_contract(BUILDER)
ACTUAL_DUCKSTATION_CALLS = 0


def expect_refusal(call: Callable[[], object], contains: str | None = None) -> str:
    try:
        call()
    except VERIFIER.ProfileVerificationRefusal as exc:
        message = str(exc)
        if contains is not None and contains not in message:
            raise TestFailure(
                f"refusal did not contain {contains!r}: {message!r}"
            ) from exc
        return message
    except Exception as exc:
        raise TestFailure(f"wrong exception type: {type(exc).__name__}: {exc}") from exc
    raise TestFailure("operation unexpectedly succeeded instead of refusing.")


def sha256_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def write_file(path: pathlib.Path, value: bytes, mode: int) -> None:
    path.parent.mkdir(parents=True, exist_ok=True, mode=0o700)
    path.write_bytes(value)
    path.chmod(mode)


class Fixture:
    def __init__(self) -> None:
        local_root = REPOSITORY_ROOT / "_local"
        local_root.mkdir(exist_ok=True)
        self._temporary = tempfile.TemporaryDirectory(
            prefix=".verify-id65-profile-test-", dir=local_root
        )
        self.repository_root = pathlib.Path(self._temporary.name)
        self.package = self.repository_root / "pkg"
        self.app = self.package / "DuckStation-ID65-Unattended-v1.app"
        self.executable = self.app / "Contents/MacOS/DuckStation"
        self.home = self.package / "home"
        self.data = self.home / "Library/Application Support/DuckStation"
        self.settings = self.data / "settings.ini"
        self.bios = self.data / "bios/fixture-bios.bin"
        self.receipt = self.package / "handoff-receipt.json"
        self.host_settings = self.repository_root / "host/settings.ini"
        self.executable_bytes = b"fixture-duckstation-executable\n"
        self.settings_bytes = (
            b"[Hotkeys]\nPowerOff = Keyboard/F12\nTogglePause = Keyboard/Space\n"
        )
        self.bios_bytes = b"fixture-bios-payload\n"
        self.host_bytes = b"fixture-normal-user-settings-sentinel\n"
        self._make_tree()
        self.pins = self._make_pins_and_receipt()

    def __enter__(self) -> "Fixture":
        return self

    def __exit__(self, exc_type: object, exc: object, traceback: object) -> None:
        self._temporary.cleanup()

    def _make_tree(self) -> None:
        self.package.mkdir(parents=True, mode=0o700)
        self.executable.parent.mkdir(parents=True, mode=0o700)
        write_file(self.executable, self.executable_bytes, 0o755)
        self.data.mkdir(parents=True, mode=0o700)
        for directory in (
            self.home,
            self.home / "Library",
            self.home / "Library/Application Support",
            self.data,
        ):
            directory.chmod(0o700)
        write_file(self.settings, self.settings_bytes, 0o600)
        write_file(self.bios, self.bios_bytes, 0o600)
        for name in VERIFIER.SAFETY_DIRECTORIES:
            (self.data / name).mkdir(mode=0o700)
        write_file(self.host_settings, self.host_bytes, 0o600)

    def _app_attestations(self) -> tuple[str, dict[str, object]]:
        app_fd = BUILDER._open_directory_absolute(
            self.app,
            "fixture app",
            owner_required=True,
            xattr_policy="output",
        )
        try:
            manifest = BUILDER._scan_tree_fd(
                app_fd, "fixture app", xattr_policy="output"
            )
            content_hash = sha256_bytes(BUILDER._serialize_app_manifest(manifest))
            output = BUILDER._output_tree_attestation(manifest, "application")
            return content_hash, output
        finally:
            os.close(app_fd)

    def _home_attestation(self) -> dict[str, object]:
        home_fd = BUILDER._open_directory_absolute(
            self.home,
            "fixture HOME",
            owner_required=True,
            xattr_policy="output",
        )
        try:
            manifest = BUILDER._scan_tree_fd(
                home_fd, "fixture HOME", xattr_policy="output"
            )
            return BUILDER._output_tree_attestation(manifest, "home")
        finally:
            os.close(home_fd)

    @staticmethod
    def _directory_envelope_entry(
        fd: int, path: str, *, subtree: Mapping[str, object] | None = None
    ) -> dict[str, object]:
        metadata = os.fstat(fd)
        entry: dict[str, object] = {
            "path": path,
            "kind": "directory",
            "mode": f"{stat.S_IMODE(metadata.st_mode):04o}",
            "linkCount": metadata.st_nlink,
            "size": None,
            "sha256": None,
            "provenanceHex": BUILDER._provenance_hex_fd(fd, f"fixture {path}"),
        }
        if subtree is not None:
            entry["subtreeEntryCount"] = subtree["entryCount"]
            entry["subtreeManifestSha256"] = subtree["manifestSha256"]
        return entry

    def _make_pins_and_receipt(self) -> Any:
        content_hash, app_output = self._app_attestations()
        home_output = self._home_attestation()
        cue_relative = pathlib.PurePosixPath("candidate/fixture.cue")
        bin_relative = pathlib.PurePosixPath("candidate/fixture.bin")
        preliminary = dataclasses.replace(
            VERIFIER.PRODUCTION_PINS,
            package_relative_path=pathlib.PurePosixPath("pkg"),
            executable_size=len(self.executable_bytes),
            executable_sha256=sha256_bytes(self.executable_bytes),
            app_content_manifest_entry_count=app_output["entryCount"],
            app_content_manifest_sha256=content_hash,
            app_output_tree_entry_count=app_output["entryCount"],
            app_output_tree_manifest_sha256=app_output["manifestSha256"],
            home_output_tree_entry_count=home_output["entryCount"],
            home_output_tree_manifest_sha256=home_output["manifestSha256"],
            package_envelope_sha256="0" * 64,
            settings_size=len(self.settings_bytes),
            settings_sha256=sha256_bytes(self.settings_bytes),
            bios_data_relative_path=pathlib.PurePosixPath("bios/fixture-bios.bin"),
            bios_size=len(self.bios_bytes),
            bios_sha256=sha256_bytes(self.bios_bytes),
            candidate_cue_relative_path=cue_relative,
            candidate_cue_size=17,
            candidate_cue_sha256="3" * 64,
            candidate_bin_relative_path=bin_relative,
            candidate_bin_size=23,
            candidate_bin_sha256="4" * 64,
            candidate_manifest_sha256="5" * 64,
            source_app_path=self.app,
            source_bios_path=self.bios,
            host_settings_path=self.host_settings,
            host_settings_size=len(self.host_bytes),
            host_settings_sha256=sha256_bytes(self.host_bytes),
            receipt_size=0,
            receipt_sha256="0" * 64,
            receipt_closure_sha256="0" * 64,
        )
        write_file(self.receipt, b"", 0o600)
        package_fd = BUILDER._open_directory_absolute(
            self.package,
            "fixture package",
            owner_required=True,
            xattr_policy="output",
        )
        app_fd = BUILDER._open_relative_directory(
            package_fd, pathlib.Path(preliminary.app_leaf)
        )
        home_fd = BUILDER._open_relative_directory(package_fd, pathlib.Path("home"))
        receipt_fd = os.open(
            preliminary.receipt_leaf,
            os.O_RDONLY | os.O_NOFOLLOW,
            dir_fd=package_fd,
        )
        try:
            envelope_entries = [
                self._directory_envelope_entry(package_fd, "."),
                self._directory_envelope_entry(
                    app_fd, preliminary.app_leaf, subtree=app_output
                ),
                self._directory_envelope_entry(home_fd, "home", subtree=home_output),
                {
                    "path": preliminary.receipt_leaf,
                    "kind": "file",
                    "mode": "0600",
                    "linkCount": 1,
                    "size": 0,
                    "sha256": "SELF-CANONICAL-RECEIPT-CLOSURE",
                    "provenanceHex": BUILDER._provenance_hex_fd(
                        receipt_fd, "fixture receipt"
                    ),
                },
            ]
        finally:
            os.close(receipt_fd)
            os.close(home_fd)
            os.close(app_fd)
            os.close(package_fd)

        payload: dict[str, object] = {
            "format": preliminary.receipt_format,
            "formatVersion": preliminary.receipt_format_version,
            "trustModel": {
                "class": "cooperative-non-hostile-same-uid",
                "commonLock": ".fixture.lock",
                "hostileSameUidProtectionClaimed": False,
            },
            "application": {
                "relativePath": preliminary.app_leaf,
                "version": preliminary.app_version,
                "executableRelativePath": preliminary.executable_app_relative_path.as_posix(),
                "executableSize": preliminary.executable_size,
                "executableSha256": preliminary.executable_sha256,
                "bundleManifestEntryCount": preliminary.app_content_manifest_entry_count,
                "bundleManifestSha256": preliminary.app_content_manifest_sha256,
                "codesignStatus": "verified",
            },
            "environment": {
                "HOME": os.fspath(self.home),
                "dataRoot": os.fspath(self.data),
                "settingsRelativePath": "home/Library/Application Support/DuckStation/settings.ini",
                "settingsSize": preliminary.settings_size,
                "settingsSha256": preliminary.settings_sha256,
            },
            "bios": {
                "relativePath": (
                    preliminary.data_root_package_relative_path
                    / preliminary.bios_data_relative_path
                ).as_posix(),
                "fileName": preliminary.bios_data_relative_path.name,
                "size": preliminary.bios_size,
                "sha256": preliminary.bios_sha256,
            },
            "candidate": {
                "cue": {
                    "path": os.fspath(self.repository_root / pathlib.Path(cue_relative)),
                    "size": preliminary.candidate_cue_size,
                    "sha256": preliminary.candidate_cue_sha256,
                },
                "bin": {
                    "path": os.fspath(self.repository_root / pathlib.Path(bin_relative)),
                    "size": preliminary.candidate_bin_size,
                    "sha256": preliminary.candidate_bin_sha256,
                },
                "manifest": {"manifestSha256": preliminary.candidate_manifest_sha256},
            },
            "outputMetadata": {
                "policy": {"version": "fixture"},
                "application": app_output,
                "home": home_output,
            },
            "packageEnvelope": {
                "format": preliminary.package_envelope_format,
                "entryCount": preliminary.package_envelope_entry_count,
                "rootTopology": [
                    preliminary.app_leaf,
                    preliminary.external_home_relative_path.as_posix(),
                    preliminary.receipt_leaf,
                ],
                "entries": envelope_entries,
            },
            "safety": {
                "emptyDirectories": [
                    (preliminary.data_root_package_relative_path / name).as_posix()
                    for name in VERIFIER.SAFETY_DIRECTORIES
                ],
                "saveLoadStateHotkeysPresent": False,
                "textureReplacementOrDumpEnabled": False,
            },
            "launchGate": {
                "status": VERIFIER.LAUNCH_GATE,
                "builderLaunchedDuckStation": False,
                "requiredHome": os.fspath(self.home),
                "candidateCue": os.fspath(
                    self.repository_root / pathlib.Path(cue_relative)
                ),
            },
        }
        size_guess = 0
        raw = b""
        closure = ""
        envelope_sha = ""
        for _ in range(16):
            envelope_entries[-1]["size"] = size_guess
            envelope = dict(payload["packageEnvelope"])
            envelope_without_hash = {
                key: value for key, value in envelope.items() if key != "manifestSha256"
            }
            envelope_sha = sha256_bytes(VERIFIER._canonical_json(envelope_without_hash))
            envelope["manifestSha256"] = envelope_sha
            payload["packageEnvelope"] = envelope
            raw, closure = issue_receipt(payload)
            if len(raw) == size_guess:
                break
            size_guess = len(raw)
        else:
            raise TestFailure("fixture receipt did not reach a size fixed point.")
        write_file(self.receipt, raw, 0o600)
        return dataclasses.replace(
            preliminary,
            package_envelope_sha256=envelope_sha,
            receipt_size=len(raw),
            receipt_sha256=sha256_bytes(raw),
            receipt_closure_sha256=closure,
        )

    def snapshot(self, *, builder: object = BUILDER) -> dict[str, object]:
        return VERIFIER._capture_runtime_snapshot_with_builder(
            builder,
            self.repository_root,
            phase="preflight",
            session_id="fixture",
            process_instance_id="not-applicable",
            captured_at_utc=None,
            pins=self.pins,
        )


def issue_receipt(payload: dict[str, object]) -> tuple[bytes, str]:
    body = {key: value for key, value in payload.items() if key != "integrity"}
    closure = sha256_bytes(VERIFIER._canonical_json(body))
    complete = dict(body)
    complete["integrity"] = {
        "algorithm": "sha256",
        "receiptClosureSha256": closure,
    }
    return VERIFIER._canonical_json(complete), closure


def validate_fixture_receipt(
    fixture: Fixture, raw: bytes, *, closure: str | None = None
) -> None:
    pins = dataclasses.replace(
        fixture.pins,
        receipt_size=len(raw),
        receipt_sha256=sha256_bytes(raw),
        receipt_closure_sha256=(
            fixture.pins.receipt_closure_sha256 if closure is None else closure
        ),
    )
    VERIFIER._validate_receipt(raw, fixture.repository_root, fixture.package, pins)


def test_builder_hash_before_import() -> None:
    with tempfile.TemporaryDirectory(
        prefix=".verify-builder-hash-test-", dir=REPOSITORY_ROOT / "_local"
    ) as raw_root:
        root = pathlib.Path(raw_root)
        builder_path = root / pathlib.Path(VERIFIER.BUILDER_RELATIVE_PATH)
        marker = root / "IMPORT_SIDE_EFFECT"
        source = f"open({os.fspath(marker)!r}, 'wb').write(b'executed')\n".encode()
        write_file(builder_path, source, 0o600)
        old_root = VERIFIER.PRODUCTION_REPOSITORY_ROOT
        old_hash = VERIFIER.EXPECTED_BUILDER_SHA256
        VERIFIER.PRODUCTION_REPOSITORY_ROOT = root
        VERIFIER.EXPECTED_BUILDER_SHA256 = "f" * 64
        sys.modules.pop("_spyro_id65_frozen_external_home_builder", None)
        try:
            expect_refusal(lambda: VERIFIER._load_frozen_builder(root), "SHA-256")
            require(not marker.exists(), "untrusted builder executed before its hash pin.")
        finally:
            VERIFIER.PRODUCTION_REPOSITORY_ROOT = old_root
            VERIFIER.EXPECTED_BUILDER_SHA256 = old_hash
            VERIFIER._load_frozen_builder(REPOSITORY_ROOT)


def test_public_api_rejects_unaudited_roots() -> None:
    def snapshot(root: pathlib.Path) -> object:
        return VERIFIER.capture_runtime_snapshot(
            root,
            phase="preflight",
            session_id="root-check",
            process_instance_id="not-applicable",
        )

    expect_refusal(lambda: snapshot(REPOSITORY_ROOT.parent), "does not match")
    noncanonical = REPOSITORY_ROOT / ".." / REPOSITORY_ROOT.name
    expect_refusal(lambda: snapshot(noncanonical), "absolute and normalized")
    with tempfile.TemporaryDirectory(
        prefix=".verify-root-link-test-", dir=REPOSITORY_ROOT / "_local"
    ) as raw_root:
        link = pathlib.Path(raw_root) / "repo-link"
        link.symlink_to(REPOSITORY_ROOT, target_is_directory=True)
        expect_refusal(lambda: snapshot(link), "symbolic link")


def test_receipt_strict_schema() -> None:
    with Fixture() as fixture:
        payload = json.loads(fixture.receipt.read_text(encoding="utf-8"))
        payload.pop("trustModel")
        raw, closure = issue_receipt(payload)
        expect_refusal(
            lambda: validate_fixture_receipt(fixture, raw, closure=closure),
            "top-level schema",
        )


def test_receipt_duplicate_key() -> None:
    with Fixture() as fixture:
        original = fixture.receipt.read_bytes()
        duplicate = b'{"format":"duplicate",' + original[1:]
        expect_refusal(
            lambda: validate_fixture_receipt(fixture, duplicate), "duplicate JSON key"
        )


def test_receipt_canonical_lf() -> None:
    with Fixture() as fixture:
        raw = fixture.receipt.read_bytes()
        require(raw.endswith(b"\n"), "fixture receipt did not begin canonical.")
        expect_refusal(
            lambda: validate_fixture_receipt(fixture, raw[:-1]),
            "serialization is not canonical",
        )


def test_snapshot_exact_key_closure_and_canonical_bytes() -> None:
    with Fixture() as fixture:
        snapshot = fixture.snapshot()
        require(set(snapshot) == VERIFIER.SNAPSHOT_KEYS, "snapshot key closure changed.")
        require(len(snapshot) == 18, "snapshot does not have exactly 18 keys.")
        raw = VERIFIER._canonical_json(snapshot)
        require(raw.endswith(b"\n") and not raw.endswith(b"\n\n"), "snapshot LF is not canonical.")
        require(
            VERIFIER._canonical_json(json.loads(raw)) == raw,
            "snapshot bytes are not canonical JSON.",
        )


def test_timestamp_refusal() -> None:
    with Fixture() as fixture:
        expect_refusal(
            lambda: VERIFIER._capture_runtime_snapshot_with_builder(
                BUILDER,
                fixture.repository_root,
                phase="before",
                session_id="fixture",
                process_instance_id="process",
                captured_at_utc="2026-08-24T12:00:00Z",
                pins=fixture.pins,
            ),
            "capturedAtUtc",
        )


def test_identifier_refusal() -> None:
    with Fixture() as fixture:
        expect_refusal(
            lambda: VERIFIER._capture_runtime_snapshot_with_builder(
                BUILDER,
                fixture.repository_root,
                phase="preflight",
                session_id="fixture/escape",
                process_instance_id="process",
                captured_at_utc=None,
                pins=fixture.pins,
            ),
            "sessionId",
        )


def test_settings_tamper_refusal() -> None:
    with Fixture() as fixture:
        fixture.settings.write_bytes(b"tampered-settings\n")
        expect_refusal(fixture.snapshot, "settings.ini bytes")


def test_bios_tamper_refusal() -> None:
    with Fixture() as fixture:
        fixture.bios.write_bytes(b"tampered-bios\n")
        expect_refusal(fixture.snapshot, "BIOS bytes")


def test_host_sentinel_tamper_refusal() -> None:
    with Fixture() as fixture:
        fixture.host_settings.write_bytes(b"tampered-host-settings\n")
        expect_refusal(fixture.snapshot, "host normal-user settings sentinel pin")


def test_host_sentinel_parent_rebind_refusal() -> None:
    with Fixture() as fixture:
        host_parent = fixture.host_settings.parent
        old_parent = fixture.repository_root / "host-original"

        def rebind_parent() -> None:
            host_parent.rename(old_parent)
            host_parent.mkdir(mode=0o700)
            shutil.copy2(old_parent / fixture.host_settings.name, fixture.host_settings)
            fixture.host_settings.chmod(0o600)

        proxy = ScanMutationProxy(BUILDER, rebind_parent)
        expect_refusal(
            lambda: fixture.snapshot(builder=proxy),
            "host normal-user settings sentinel parent path binding changed",
        )
        require(proxy.mutated, "host-parent rebind hook was not reached.")


def test_receipt_tamper_refusal() -> None:
    with Fixture() as fixture:
        raw = bytearray(fixture.receipt.read_bytes())
        raw[10] ^= 1
        fixture.receipt.write_bytes(raw)
        expect_refusal(fixture.snapshot, "handoff receipt bytes")


def test_app_tamper_refusal() -> None:
    with Fixture() as fixture:
        write_file(fixture.app / "Contents/UNEXPECTED", b"tampered-app\n", 0o600)
        expect_refusal(fixture.snapshot, "app content manifest")


def test_unknown_data_path_refusal() -> None:
    with Fixture() as fixture:
        write_file(fixture.data / "cache-evil/payload", b"unknown\n", 0o600)
        expect_refusal(fixture.snapshot, "unknown data directory")


def test_backslash_data_path_refusal() -> None:
    with Fixture() as fixture:
        write_file(fixture.data / "cache\\escape", b"backslash\n", 0o600)
        expect_refusal(fixture.snapshot, "data path is noncanonical")
        require(
            not VERIFIER._data_path_is_allowed("cache\\escape")
            and not VERIFIER._data_path_is_allowed("cache//escape")
            and not VERIFIER._data_path_is_allowed("."),
            "policy helper accepted a noncanonical volatile path.",
        )


def test_safety_path_refusal() -> None:
    with Fixture() as fixture:
        write_file(fixture.data / "savestates/state.sav", b"unsafe\n", 0o600)
        expect_refusal(fixture.snapshot, "safety directory savestates topology changed")


def test_receipt_bound_directory_metadata_refusal() -> None:
    with Fixture() as fixture:
        fixture.package.chmod(0o755)
        expect_refusal(fixture.snapshot, "profile package root metadata")
    with Fixture() as fixture:
        fixture.bios.parent.chmod(0o755)
        expect_refusal(fixture.snapshot, "BIOS directory metadata")


def test_receipt_bound_file_provenance_refusal() -> None:
    for attribute, expected in (
        ("receipt", "handoff receipt metadata"),
        ("settings", "external-HOME settings.ini metadata"),
        ("bios", "external-HOME BIOS metadata"),
    ):
        with Fixture() as fixture:
            proxy = MetadataXattrProxy(BUILDER, getattr(fixture, attribute))
            expect_refusal(lambda: fixture.snapshot(builder=proxy), expected)
            require(proxy.substitutions > 0, f"{attribute} metadata drift hook was not reached.")


def test_segment_safe_volatile_allowlist() -> None:
    with Fixture() as fixture:
        write_file(fixture.data / "cache/nested/cache.bin", b"cache\n", 0o600)
        write_file(fixture.data / "screenshots/frame.png", b"png\n", 0o600)
        write_file(fixture.data / "videos/capture.mp4", b"video\n", 0o600)
        write_file(fixture.data / "playtime.dat", b"time\n", 0o600)
        snapshot = fixture.snapshot()
        paths = [entry["relativePath"] for entry in snapshot["dataFiles"]]
        require(paths == sorted(set(paths)), "volatile dataFiles are not sorted unique.")
        for expected in (
            "cache/nested/cache.bin",
            "screenshots/frame.png",
            "videos/capture.mp4",
            "playtime.dat",
        ):
            require(expected in paths, f"allowed volatile path was omitted: {expected}.")
        for forbidden in ("cache-evil/a", "screenshots.txt", "savestates/a"):
            require(
                not VERIFIER._data_path_is_allowed(forbidden),
                f"raw-prefix path escaped the segment allowlist: {forbidden}.",
            )


class ScanMutationProxy:
    def __init__(self, base: types.ModuleType, mutation: Callable[[], None]) -> None:
        self.base = base
        self.mutation = mutation
        self.mutated = False

    def __getattr__(self, name: str) -> object:
        return getattr(self.base, name)

    def _scan_tree_fd(self, fd: int, label: str, *, xattr_policy: str) -> object:
        if "runtime data stable rescan" in label and not self.mutated:
            self.mutated = True
            self.mutation()
        return self.base._scan_tree_fd(fd, label, xattr_policy=xattr_policy)


class LabelMutationProxy:
    def __init__(
        self, base: types.ModuleType, label_fragment: str, mutation: Callable[[], None]
    ) -> None:
        self.base = base
        self.label_fragment = label_fragment
        self.mutation = mutation
        self.mutated = False

    def __getattr__(self, name: str) -> object:
        return getattr(self.base, name)

    def _scan_tree_fd(self, fd: int, label: str, *, xattr_policy: str) -> object:
        if self.label_fragment in label and not self.mutated:
            self.mutated = True
            self.mutation()
        return self.base._scan_tree_fd(fd, label, xattr_policy=xattr_policy)


class MetadataXattrProxy:
    def __init__(self, base: types.ModuleType, target: pathlib.Path) -> None:
        self.base = base
        metadata = os.stat(target, follow_symlinks=False)
        self.identity = (metadata.st_dev, metadata.st_ino)
        self.substitutions = 0

    def __getattr__(self, name: str) -> object:
        return getattr(self.base, name)

    def _validate_policy_metadata_fd(
        self,
        fd: int,
        label: str,
        metadata: os.stat_result,
        *,
        xattr_policy: str,
        allow_nonzero_flags: bool = False,
    ) -> tuple[str, ...]:
        observed = self.base._validate_policy_metadata_fd(
            fd,
            label,
            metadata,
            xattr_policy=xattr_policy,
            allow_nonzero_flags=allow_nonzero_flags,
        )
        if (metadata.st_dev, metadata.st_ino) == self.identity:
            self.substitutions += 1
            return ("com.apple.provenance=" + "0102" + ("bb" * 9),)
        return observed


class AfterLabelMutationProxy:
    def __init__(
        self, base: types.ModuleType, label_fragment: str, mutation: Callable[[], None]
    ) -> None:
        self.base = base
        self.label_fragment = label_fragment
        self.mutation = mutation
        self.mutated = False

    def __getattr__(self, name: str) -> object:
        return getattr(self.base, name)

    def _scan_tree_fd(self, fd: int, label: str, *, xattr_policy: str) -> object:
        result = self.base._scan_tree_fd(fd, label, xattr_policy=xattr_policy)
        if self.label_fragment in label and not self.mutated:
            self.mutated = True
            self.mutation()
        return result


def test_double_scan_race_refusal() -> None:
    with Fixture() as fixture:
        proxy = ScanMutationProxy(
            BUILDER,
            lambda: write_file(fixture.data / "cache/race.bin", b"race\n", 0o600),
        )
        expect_refusal(lambda: fixture.snapshot(builder=proxy), "changed across stable scans")
        require(proxy.mutated, "race mutation hook was not reached.")


def test_path_rebind_refusal() -> None:
    with Fixture() as fixture:
        replacement = fixture.repository_root / "replacement-settings.ini"
        backup = fixture.repository_root / "original-settings.ini"
        shutil.copy2(fixture.settings, replacement)
        replacement.chmod(0o600)

        def rebind() -> None:
            os.replace(fixture.settings, backup)
            os.replace(replacement, fixture.settings)
            backup.unlink()

        proxy = ScanMutationProxy(BUILDER, rebind)
        expect_refusal(lambda: fixture.snapshot(builder=proxy), "pathname was rebound")
        require(proxy.mutated, "rebind mutation hook was not reached.")


def test_post_second_scan_app_mutation_refusal() -> None:
    with Fixture() as fixture:
        proxy = LabelMutationProxy(
            BUILDER,
            "packaged app final scan",
            lambda: write_file(
                fixture.app / "Contents/LATE-APP-MUTATION", b"late\n", 0o600
            ),
        )
        expect_refusal(
            lambda: fixture.snapshot(builder=proxy), "changed before its final scan"
        )
        require(proxy.mutated, "post-second app mutation hook was not reached.")


def test_post_second_scan_data_mutation_refusal() -> None:
    with Fixture() as fixture:
        proxy = LabelMutationProxy(
            BUILDER,
            "runtime data final scan",
            lambda: write_file(fixture.data / "cache/late.bin", b"late\n", 0o600),
        )
        expect_refusal(
            lambda: fixture.snapshot(builder=proxy), "changed before its final scan"
        )
        require(proxy.mutated, "post-second data mutation hook was not reached.")


def test_late_package_topology_mutation_refusal() -> None:
    with Fixture() as fixture:
        proxy = LabelMutationProxy(
            BUILDER,
            "packaged app final scan",
            lambda: write_file(fixture.package / "LATE-ROOT-EXTRA", b"late\n", 0o600),
        )
        expect_refusal(lambda: fixture.snapshot(builder=proxy), "profile package topology changed")
        require(proxy.mutated, "late package-topology hook was not reached.")


def test_existing_volatile_post_final_scan_mutation_refusal() -> None:
    with Fixture() as fixture:
        volatile = fixture.data / "cache/item.bin"
        write_file(volatile, b"before", 0o600)
        proxy = AfterLabelMutationProxy(
            BUILDER,
            "runtime data final scan",
            lambda: volatile.write_bytes(b"after!"),
        )
        expect_refusal(
            lambda: fixture.snapshot(builder=proxy), "changed before its closing scan"
        )
        require(proxy.mutated, "post-final volatile mutation hook was not reached.")


@contextlib.contextmanager
def forbid_process_creation() -> Iterator[list[str]]:
    calls: list[str] = []
    targets: list[tuple[object, str]] = []
    for owner, names in (
        (subprocess, ("Popen", "run", "call", "check_call", "check_output")),
        (os, ("system", "popen", "posix_spawn", "posix_spawnp")),
    ):
        for name in names:
            if hasattr(owner, name):
                targets.append((owner, name))
    originals = [(owner, name, getattr(owner, name)) for owner, name in targets]

    def forbidden(*args: object, **kwargs: object) -> object:
        calls.append(repr(args[0]) if args else "unknown")
        raise TestFailure("snapshot attempted process creation.")

    try:
        for owner, name, _ in originals:
            setattr(owner, name, forbidden)
        yield calls
    finally:
        for owner, name, original in originals:
            setattr(owner, name, original)


def test_snapshot_uses_no_subprocess() -> None:
    with forbid_process_creation() as calls:
        snapshot = VERIFIER.capture_runtime_snapshot(
            REPOSITORY_ROOT,
            phase="preflight",
            session_id="no-subprocess",
            process_instance_id="not-applicable",
        )
    require(not calls, f"snapshot attempted process creation: {calls!r}.")
    require(set(snapshot) == VERIFIER.SNAPSHOT_KEYS, "production snapshot key closure changed.")


def test_production_snapshot_read_only() -> None:
    receipt = (
        REPOSITORY_ROOT
        / pathlib.Path(VERIFIER.PRODUCTION_PINS.package_relative_path)
        / VERIFIER.PRODUCTION_PINS.receipt_leaf
    )
    before = receipt.read_bytes()
    snapshot = VERIFIER.capture_runtime_snapshot(
        REPOSITORY_ROOT,
        phase="preflight",
        session_id="production-read-only",
        process_instance_id="not-applicable",
    )
    after = receipt.read_bytes()
    require(before == after, "production snapshot changed the handoff receipt.")
    require(
        snapshot["handoffReceiptSha256"] == VERIFIER.PRODUCTION_PINS.receipt_sha256,
        "production snapshot receipt pin changed.",
    )


@contextlib.contextmanager
def guard_duckstation_launch() -> Iterator[None]:
    global ACTUAL_DUCKSTATION_CALLS
    original_run = subprocess.run
    original_popen = subprocess.Popen

    def command_is_duckstation(command: object) -> bool:
        if isinstance(command, (list, tuple)) and command:
            first = os.fspath(command[0])
        elif isinstance(command, (str, os.PathLike)):
            first = os.fspath(command).split()[0]
        else:
            return False
        return pathlib.Path(first).name == "DuckStation"

    def guarded_run(command: object, *args: object, **kwargs: object) -> object:
        global ACTUAL_DUCKSTATION_CALLS
        if command_is_duckstation(command):
            ACTUAL_DUCKSTATION_CALLS += 1
            raise TestFailure("production prelaunch attempted to run DuckStation.")
        return original_run(command, *args, **kwargs)

    def guarded_popen(command: object, *args: object, **kwargs: object) -> object:
        global ACTUAL_DUCKSTATION_CALLS
        if command_is_duckstation(command):
            ACTUAL_DUCKSTATION_CALLS += 1
            raise TestFailure("production prelaunch attempted to launch DuckStation.")
        return original_popen(command, *args, **kwargs)

    subprocess.run = guarded_run
    subprocess.Popen = guarded_popen
    try:
        yield
    finally:
        subprocess.run = original_run
        subprocess.Popen = original_popen


def test_production_prelaunch_read_only() -> None:
    receipt = (
        REPOSITORY_ROOT
        / pathlib.Path(VERIFIER.PRODUCTION_PINS.package_relative_path)
        / VERIFIER.PRODUCTION_PINS.receipt_leaf
    )
    before = receipt.read_bytes()
    with guard_duckstation_launch():
        result = VERIFIER.verify_prelaunch(REPOSITORY_ROOT)
    after = receipt.read_bytes()
    require(before == after, "production prelaunch changed the handoff receipt.")
    require(result.get("status") == "PREFLIGHT_READY", "production prelaunch was not ready.")
    require(
        result.get("launchGate") == VERIFIER.LAUNCH_GATE,
        "production prelaunch launch gate changed.",
    )


TESTS: tuple[tuple[str, Callable[[], None]], ...] = (
    ("builder_hash_before_import", test_builder_hash_before_import),
    ("public_api_rejects_unaudited_roots", test_public_api_rejects_unaudited_roots),
    ("receipt_strict_schema", test_receipt_strict_schema),
    ("receipt_duplicate_key", test_receipt_duplicate_key),
    ("receipt_canonical_lf", test_receipt_canonical_lf),
    ("snapshot_exact_key_closure_and_canonical_bytes", test_snapshot_exact_key_closure_and_canonical_bytes),
    ("timestamp_refusal", test_timestamp_refusal),
    ("identifier_refusal", test_identifier_refusal),
    ("settings_tamper_refusal", test_settings_tamper_refusal),
    ("bios_tamper_refusal", test_bios_tamper_refusal),
    ("host_sentinel_tamper_refusal", test_host_sentinel_tamper_refusal),
    ("host_sentinel_parent_rebind_refusal", test_host_sentinel_parent_rebind_refusal),
    ("receipt_tamper_refusal", test_receipt_tamper_refusal),
    ("app_tamper_refusal", test_app_tamper_refusal),
    ("unknown_data_path_refusal", test_unknown_data_path_refusal),
    ("backslash_data_path_refusal", test_backslash_data_path_refusal),
    ("safety_path_refusal", test_safety_path_refusal),
    ("receipt_bound_directory_metadata_refusal", test_receipt_bound_directory_metadata_refusal),
    ("receipt_bound_file_provenance_refusal", test_receipt_bound_file_provenance_refusal),
    ("segment_safe_volatile_allowlist", test_segment_safe_volatile_allowlist),
    ("double_scan_race_refusal", test_double_scan_race_refusal),
    ("path_rebind_refusal", test_path_rebind_refusal),
    ("post_second_scan_app_mutation_refusal", test_post_second_scan_app_mutation_refusal),
    ("post_second_scan_data_mutation_refusal", test_post_second_scan_data_mutation_refusal),
    ("late_package_topology_mutation_refusal", test_late_package_topology_mutation_refusal),
    ("existing_volatile_post_final_scan_mutation_refusal", test_existing_volatile_post_final_scan_mutation_refusal),
    ("snapshot_uses_no_subprocess", test_snapshot_uses_no_subprocess),
    ("production_snapshot_read_only", test_production_snapshot_read_only),
    ("production_prelaunch_read_only", test_production_prelaunch_read_only),
)


def main() -> int:
    failures: list[str] = []
    for name, test in TESTS:
        try:
            test()
        except Exception as exc:
            failures.append(f"{name}: {type(exc).__name__}: {exc}")
            print(f"FAIL {name}: {type(exc).__name__}: {exc}", file=sys.stderr)
        else:
            print(f"PASS {name}")
    if failures:
        print(
            f"TEST_VERIFY_PROFILE_FAILED passed={len(TESTS) - len(failures)} "
            f"failed={len(failures)} actualDuckStationCalls={ACTUAL_DUCKSTATION_CALLS}",
            file=sys.stderr,
        )
        return 1
    print(
        f"TEST_VERIFY_PROFILE_OK tests={len(TESTS)} "
        f"actualDuckStationCalls={ACTUAL_DUCKSTATION_CALLS}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
