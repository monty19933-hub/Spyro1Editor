#!/usr/bin/env python3
"""Adversarial fixture tests for Build-SpyroId65UnattendedProfile.py.

The suite never builds a production profile and never launches DuckStation.
All mutating tests use tiny bundles and candidate files under TemporaryDirectory.
The one production-app test is a read-only manifest and strict-codesign audit.
"""

from __future__ import annotations

import ast
import contextlib
import ctypes
import dataclasses
import fcntl
import hashlib
import importlib.util
import io
import json
import os
import pathlib
import plistlib
import shutil
import sys
import tempfile
import unittest
from unittest import mock


BUILDER_PATH = pathlib.Path(__file__).with_name(
    "Build-SpyroId65UnattendedProfile.py"
).resolve()
SPEC = importlib.util.spec_from_file_location("spyro_id65_profile_builder", BUILDER_PATH)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError("could not load profile builder")
builder = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = builder
SPEC.loader.exec_module(builder)


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


TEST_LIBC = ctypes.CDLL(None, use_errno=True)


def set_xattr(path: pathlib.Path, name: str, value: bytes) -> None:
    function = TEST_LIBC.setxattr
    function.argtypes = [
        ctypes.c_char_p, ctypes.c_char_p, ctypes.c_void_p, ctypes.c_size_t,
        ctypes.c_uint32, ctypes.c_int,
    ]
    function.restype = ctypes.c_int
    buffer = ctypes.create_string_buffer(value)
    if function(os.fsencode(path), name.encode(), buffer, len(value), 0, 0) != 0:
        error_number = ctypes.get_errno()
        raise OSError(error_number, os.strerror(error_number), os.fspath(path))


def get_xattr(path: pathlib.Path, name: str) -> bytes:
    function = TEST_LIBC.getxattr
    function.argtypes = [
        ctypes.c_char_p, ctypes.c_char_p, ctypes.c_void_p, ctypes.c_size_t,
        ctypes.c_uint32, ctypes.c_int,
    ]
    function.restype = ctypes.c_ssize_t
    size = function(os.fsencode(path), name.encode(), None, 0, 0, 0)
    if size < 0:
        error_number = ctypes.get_errno()
        raise OSError(error_number, os.strerror(error_number), os.fspath(path))
    buffer = ctypes.create_string_buffer(size)
    if function(os.fsencode(path), name.encode(), buffer, size, 0, 0) != size:
        error_number = ctypes.get_errno()
        raise OSError(error_number, os.strerror(error_number), os.fspath(path))
    return buffer.raw[:size]


def remove_xattr(path: pathlib.Path, name: str) -> None:
    function = TEST_LIBC.removexattr
    function.argtypes = [ctypes.c_char_p, ctypes.c_char_p, ctypes.c_int]
    function.restype = ctypes.c_int
    if function(os.fsencode(path), name.encode(), 0) != 0:
        error_number = ctypes.get_errno()
        raise OSError(error_number, os.strerror(error_number), os.fspath(path))


class Fixture:
    def __init__(self) -> None:
        self.temporary = tempfile.TemporaryDirectory(
            prefix="spyro-id65-unattended-tests-"
        )
        self.root = pathlib.Path(self.temporary.name).resolve()
        self.source_parent = self.root / "source"
        self.destination_parent = self.root / "output"
        self.candidate_parent = self.root / "candidate"
        self.bios_parent = self.root / "bios-source"
        for directory in (
            self.source_parent,
            self.destination_parent,
            self.candidate_parent,
            self.bios_parent,
        ):
            directory.mkdir(mode=0o700)
            directory.chmod(0o700)

        self.app = self.source_parent / "DuckStation.app"
        macos = self.app / "Contents" / "MacOS"
        resources = self.app / "Contents" / "Resources" / "translations"
        macos.mkdir(parents=True)
        resources.mkdir(parents=True)
        self.executable = macos / "DuckStation"
        self.executable_bytes = b"#!/bin/false\nfixture-duckstation-not-executed\n"
        self.executable.write_bytes(self.executable_bytes)
        self.executable.chmod(0o755)
        info = {
            "CFBundleIdentifier": "fixture.invalid.duckstation",
            "CFBundleShortVersionString": "fixture-0.1",
            "CFBundleExecutable": "DuckStation",
        }
        info_path = self.app / "Contents" / "Info.plist"
        info_path.write_bytes(plistlib.dumps(info, fmt=plistlib.FMT_XML, sort_keys=True))
        info_path.chmod(0o644)
        resource = resources / "fixture.qm"
        resource.write_bytes(b"fixture-resource\n")
        resource.chmod(0o644)
        for directory in sorted(
            (path for path in self.app.rglob("*") if path.is_dir()),
            key=lambda path: len(path.parts),
            reverse=True,
        ):
            directory.chmod(0o755)
        self.app.chmod(0o755)

        self.bin_name = "fixture.bin"
        self.cue_name = "fixture.cue"
        self.binary = self.candidate_parent / self.bin_name
        self.binary_bytes = b"fixture-sector-data" * 17
        self.binary.write_bytes(self.binary_bytes)
        self.binary.chmod(0o600)
        self.cue = self.candidate_parent / self.cue_name
        self.cue_bytes = (
            f'FILE "{self.bin_name}" BINARY\n'
            "  TRACK 01 MODE2/2352\n"
            "    INDEX 01 00:00:00\n"
        ).encode("ascii")
        self.cue.write_bytes(self.cue_bytes)
        self.cue.chmod(0o600)

        self.bios_name = "fixture-bios.bin"
        self.bios = self.bios_parent / self.bios_name
        self.bios_bytes = bytes(range(256)) * 3
        self.bios.write_bytes(self.bios_bytes)
        self.bios.chmod(0o600)

        self.source_manifest = self.scan_app(self.app, "source-app")
        manifest_digest = sha256(builder._serialize_app_manifest(self.source_manifest))
        executable_entry = self.source_manifest[
            builder.SOURCE_APP_EXECUTABLE_RELATIVE_PATH
        ]
        self.pins = dataclasses.replace(
            builder.PRODUCTION_PINS,
            app_version="fixture-0.1",
            executable_sha256=executable_entry.sha256,
            executable_size=executable_entry.size,
            app_manifest_sha256=manifest_digest,
            app_manifest_entry_count=len(self.source_manifest),
            candidate_cue_name=self.cue_name,
            candidate_cue_sha256=sha256(self.cue_bytes),
            candidate_cue_size=len(self.cue_bytes),
            candidate_bin_name=self.bin_name,
            candidate_bin_sha256=sha256(self.binary_bytes),
            candidate_bin_size=len(self.binary_bytes),
            bios_name=self.bios_name,
            bios_sha256=sha256(self.bios_bytes),
            bios_size=len(self.bios_bytes),
        )
        self.codesign_calls: list[pathlib.Path] = []

    def cleanup(self) -> None:
        self.temporary.cleanup()

    @staticmethod
    def scan_app(path: pathlib.Path, xattr_policy: str):
        fd = os.open(path, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW)
        try:
            return builder._scan_tree_fd(fd, "fixture app", xattr_policy=xattr_policy)
        finally:
            os.close(fd)

    @staticmethod
    def scan_package(path: pathlib.Path):
        fd = os.open(path, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW)
        try:
            return builder._scan_tree_fd(fd, "fixture package", xattr_policy="output")
        finally:
            os.close(fd)

    def fake_codesign(self, path: pathlib.Path) -> None:
        path = pathlib.Path(path)
        self.codesign_calls.append(path)
        if not path.is_dir():
            raise builder.ProfileRefusal("fixture codesign target is not a directory")
        if not (path / builder.SOURCE_APP_INFO_PLIST_RELATIVE_PATH).is_file():
            raise builder.ProfileRefusal("fixture codesign target has no Info.plist")

    def destination(self, leaf: str = "fixture-profile") -> pathlib.Path:
        return self.destination_parent / leaf

    def build(
        self,
        destination: pathlib.Path | None = None,
        *,
        hook=None,
        codesign_verifier=None,
        cue: pathlib.Path | None = None,
        binary: pathlib.Path | None = None,
        pins=None,
    ):
        return builder.build_profile(
            self.app,
            destination or self.destination(),
            cue or self.cue,
            binary or self.binary,
            self.bios,
            pins or self.pins,
            codesign_verifier=codesign_verifier or self.fake_codesign,
            hook=hook,
        )

    def verify(self, destination: pathlib.Path | None = None):
        return builder.verify_profile(
            self.app,
            destination or self.destination(),
            self.cue,
            self.binary,
            self.bios,
            self.pins,
            codesign_verifier=self.fake_codesign,
        )


class BuilderTests(unittest.TestCase):
    maxDiff = None

    def setUp(self) -> None:
        self.fixture = Fixture()

    def tearDown(self) -> None:
        self.fixture.cleanup()

    def assert_refused(self, callable_object) -> BaseException:
        with self.assertRaises((builder.ProfileRefusal, OSError)) as caught:
            callable_object()
        return caught.exception

    def assert_no_stage(self) -> None:
        private_prefix = f".{self.fixture.destination().name}.private-"
        self.assertFalse(any(
            path.name.startswith(private_prefix)
            for path in self.fixture.destination_parent.iterdir()
        ))

    def adjusted_settings_pins(self, raw: bytes):
        return dataclasses.replace(
            self.fixture.pins,
            settings_size=len(raw),
            settings_sha256=sha256(raw),
        )

    def test_01_canonical_settings_and_independent_safety_semantics(self) -> None:
        self.assertEqual(len(builder.CANONICAL_SETTINGS), 2143)
        self.assertEqual(
            builder.CANONICAL_SETTINGS_SHA256,
            "469ef98771f26395352bac1d43f90e4be14693fd4ba5316e4d71e0557a7f74f9",
        )
        builder._validate_settings(builder.CANONICAL_SETTINGS, self.fixture.pins)
        sections = builder._parse_ini(builder.CANONICAL_SETTINGS)
        pad1 = dict(sections["Pad1"])
        self.assertEqual(pad1["Circle"], "Keyboard/Z")
        self.assertEqual(pad1["Cross"], "Keyboard/X")
        self.assertEqual(pad1["Start"], "Keyboard/P")
        self.assertEqual(pad1["Select"], "Keyboard/Backspace")
        for ordinal, target in enumerate(
            ("R1", "R2", "L1", "L2", "Left", "Down"), start=1
        ):
            self.assertEqual(pad1[f"Macro{ordinal}"], f"Keyboard/{ordinal}")
            self.assertEqual(pad1[f"Macro{ordinal}Binds"], target)
            self.assertEqual(pad1[f"Macro{ordinal}Toggle"], "true")
        unsafe_replacements = (
            (b"SaveStateOnExit = false", b"SaveStateOnExit = true"),
            (b"LoadDevicesFromSaveStates = false", b"LoadDevicesFromSaveStates = true"),
            (b"StartPaused = false", b"StartPaused = true"),
            (b"EnableTextureReplacements = false", b"EnableTextureReplacements = true"),
            (b"SDL = false", b"SDL = true"),
            (b"TogglePause = Keyboard/Space", b"TogglePause = Keyboard/Escape"),
            (b"Macro1Binds = R1", b"Macro1Binds = AnalogLeftX"),
        )
        for old, new in unsafe_replacements:
            raw = builder.CANONICAL_SETTINGS.replace(old, new)
            with self.subTest(replacement=new):
                with self.assertRaises(builder.ProfileRefusal):
                    builder._validate_settings(raw, self.adjusted_settings_pins(raw))

    def test_02_duplicate_sections_and_keys_fail_closed(self) -> None:
        duplicate_section = builder.CANONICAL_SETTINGS + b"\n[Main]\nX = y\n"
        duplicate_key = builder.CANONICAL_SETTINGS.replace(
            b"ConfirmPowerOff = false\n",
            b"ConfirmPowerOff = false\nConfirmPowerOff = false\n",
        )
        for raw in (duplicate_section, duplicate_key):
            with self.assertRaises(builder.ProfileRefusal):
                builder._validate_settings(raw, self.adjusted_settings_pins(raw))

    def test_03_frozen_production_app_manifest_and_codesign_read_only(self) -> None:
        source = builder.SOURCE_APP_PATH
        self.assertTrue(source.is_dir())
        parent_fd = builder._open_directory_absolute(
            source.parent,
            "production source parent",
            owner_required=False,
            xattr_policy="allowed-system",
            allow_group_write=True,
            allow_nonzero_flags=True,
        )
        app_fd = os.open(
            source.name,
            os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW,
            dir_fd=parent_fd,
        )
        try:
            manifest, digest = builder._validate_source_app(
                app_fd, source, builder.PRODUCTION_PINS, builder._codesign_verify
            )
        finally:
            os.close(app_fd)
            os.close(parent_fd)
        self.assertEqual(len(manifest), 292)
        self.assertEqual(
            digest,
            "95a8142e561d6b37f228728abc8dbf034c774887213eb59bc14518d56c2d222e",
        )

    def test_04_fixture_build_verify_topology_source_preservation_and_determinism(self) -> None:
        source_before = self.fixture.scan_app(self.fixture.app, "source-app")
        result = self.fixture.build()
        destination = self.fixture.destination()
        self.assertEqual(result.destination, destination)
        self.assertEqual(
            result.receipt_schema,
            "spyro-id65-unattended-external-home-handoff@1",
        )
        self.assertEqual(result.launch_gate, "EXTERNAL_RUNTIME_LAUNCH_REQUIRED")
        self.assertEqual(
            {path.name for path in destination.iterdir()},
            {builder.PACKAGED_APP_LEAF, "home", builder.RECEIPT_LEAF},
        )
        data_root = destination / builder.DATA_ROOT_RELATIVE_PATH
        self.assertEqual(
            (data_root / "settings.ini").read_bytes(), builder.CANONICAL_SETTINGS
        )
        self.assertEqual(
            (data_root / "bios" / self.fixture.bios_name).read_bytes(),
            self.fixture.bios_bytes,
        )
        for name in builder.SAFETY_DIRECTORY_NAMES:
            self.assertEqual(list((data_root / name).iterdir()), [])
        clone = destination / builder.PACKAGED_APP_LEAF
        clone_manifest = self.fixture.scan_app(clone, "output")
        self.assertEqual(
            builder._manifest_without_xattrs(clone_manifest),
            builder._manifest_without_xattrs(source_before),
        )
        receipt_raw = (destination / builder.RECEIPT_LEAF).read_bytes()
        receipt = json.loads(receipt_raw)
        self.assertEqual(receipt["environment"]["HOME"], str(destination / "home"))
        self.assertEqual(
            receipt["candidate"]["cue"]["path"], str(self.fixture.cue)
        )
        self.assertFalse(
            receipt["trustModel"]["hostileSameUidProtectionClaimed"]
        )
        self.assertFalse(receipt["launchGate"]["builderLaunchedDuckStation"])
        output_metadata = receipt["outputMetadata"]
        self.assertEqual(
            output_metadata["policy"]["version"],
            builder.OUTPUT_XATTR_POLICY_VERSION,
        )
        for scope in ("application", "home"):
            attestation = output_metadata[scope]
            self.assertEqual(attestation["entryCount"], len(attestation["entries"]))
            self.assertEqual(
                attestation["entryCount"],
                attestation["provenanceEntryCount"] + attestation["noXattrEntryCount"],
            )
            for entry in attestation["entries"]:
                if entry["provenanceHex"] is not None:
                    raw = bytes.fromhex(entry["provenanceHex"])
                    self.assertEqual(len(raw), 11)
                    self.assertTrue(raw.startswith(b"\x01\x02"))
        self.assertEqual(receipt["candidate"]["manifest"]["entryCount"], 2)
        envelope = receipt["packageEnvelope"]
        envelope_without_hash = {
            key: value for key, value in envelope.items() if key != "manifestSha256"
        }
        self.assertEqual(
            envelope["manifestSha256"],
            sha256(builder._canonical_json(envelope_without_hash)),
        )
        envelope_entries = {entry["path"]: entry for entry in envelope["entries"]}
        self.assertEqual(
            set(envelope_entries),
            {".", builder.PACKAGED_APP_LEAF, "home", builder.RECEIPT_LEAF},
        )
        self.assertEqual(envelope_entries["."]["mode"], "0700")
        self.assertEqual(envelope_entries[builder.RECEIPT_LEAF]["mode"], "0600")
        self.assertEqual(
            envelope_entries[builder.RECEIPT_LEAF]["size"], len(receipt_raw)
        )
        self.assertEqual(
            envelope_entries[builder.RECEIPT_LEAF]["sha256"],
            builder.SELF_RECEIPT_SHA256_SENTINEL,
        )
        builder._validate_receipt(receipt_raw, receipt_raw)
        self.fixture.verify()
        first_manifest = self.fixture.scan_package(destination)
        shutil.rmtree(destination)
        self.fixture.build()
        second_manifest = self.fixture.scan_package(destination)
        self.assertEqual(first_manifest, second_manifest)
        self.assertEqual(
            source_before, self.fixture.scan_app(self.fixture.app, "source-app")
        )
        self.assertGreaterEqual(len(self.fixture.codesign_calls), 6)

    def test_05_source_xattrs_preserved_and_clone_forbidden_xattrs_absent(self) -> None:
        value = b"0081;fixture;Safari;"
        set_xattr(self.fixture.app, "com.apple.quarantine", value)
        source_before = get_xattr(self.fixture.app, "com.apple.quarantine")
        self.fixture.source_manifest = self.fixture.scan_app(self.fixture.app, "source-app")
        self.fixture.pins = dataclasses.replace(
            self.fixture.pins,
            app_manifest_sha256=sha256(
                builder._serialize_app_manifest(self.fixture.source_manifest)
            ),
        )
        self.fixture.build()
        self.assertEqual(
            get_xattr(self.fixture.app, "com.apple.quarantine"), source_before
        )
        clone = self.fixture.destination() / builder.PACKAGED_APP_LEAF
        clone_fd = os.open(clone, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW)
        try:
            self.assertNotIn(
                "com.apple.quarantine", builder._read_xattr_names_fd(clone_fd, "clone")
            )
        finally:
            os.close(clone_fd)
        self.fixture.scan_app(clone, "output")

    def test_06_candidate_mutation_and_path_rebind_are_rejected(self) -> None:
        def mutate(context) -> None:
            if context.checkpoint == "after-initial-validation":
                with self.fixture.cue.open("ab") as stream:
                    stream.write(b"tamper")

        self.assert_refused(lambda: self.fixture.build(hook=mutate))
        self.assertFalse(self.fixture.destination().exists())
        self.assert_no_stage()

        second = Fixture()
        try:
            moved = second.cue.with_suffix(".held")

            def rebind(context) -> None:
                if context.checkpoint == "after-initial-validation":
                    second.cue.rename(moved)
                    second.cue.write_bytes(second.cue_bytes)
                    second.cue.chmod(0o600)

            with self.assertRaises(builder.ProfileRefusal):
                second.build(hook=rebind)
            self.assertFalse(second.destination().exists())
        finally:
            second.cleanup()

    def test_07_cue_bin_name_topology_and_handoff_semantics_are_exact(self) -> None:
        wrong_name = self.fixture.candidate_parent / "wrong.cue"
        wrong_name.write_bytes(self.fixture.cue_bytes)
        wrong_name.chmod(0o600)
        self.assert_refused(lambda: self.fixture.build(cue=wrong_name))

        other = self.fixture.root / "other-candidate-parent"
        other.mkdir(mode=0o700)
        other_binary = other / self.fixture.bin_name
        other_binary.write_bytes(self.fixture.binary_bytes)
        other_binary.chmod(0o600)
        self.assert_refused(lambda: self.fixture.build(binary=other_binary))

        wrong_cue_bytes = self.fixture.cue_bytes.replace(
            self.fixture.bin_name.encode("ascii"), b"different.bin"
        )
        self.fixture.cue.write_bytes(wrong_cue_bytes)
        changed_pins = dataclasses.replace(
            self.fixture.pins,
            candidate_cue_size=len(wrong_cue_bytes),
            candidate_cue_sha256=sha256(wrong_cue_bytes),
        )
        self.assert_refused(lambda: self.fixture.build(pins=changed_pins))

    def test_08_mutation_after_stage_validation_is_rejected_and_cleaned(self) -> None:
        def hook(context) -> None:
            if context.checkpoint == "after-stage-validation":
                path = (
                    context.parent_path / context.stage_name
                    / builder.SETTINGS_RELATIVE_PATH
                )
                raw = path.read_bytes().replace(
                    b"ConfirmPowerOff = false", b"ConfirmPowerOff = true "
                )
                path.write_bytes(raw)
                path.chmod(0o600)

        self.assert_refused(lambda: self.fixture.build(hook=hook))
        self.assertFalse(self.fixture.destination().exists())
        self.assert_no_stage()

    def test_09_before_publish_hook_precedes_final_complete_validation(self) -> None:
        def hook(context) -> None:
            if context.checkpoint == "before-publish":
                path = (
                    context.parent_path / context.stage_name
                    / builder.SETTINGS_RELATIVE_PATH
                )
                path.write_bytes(path.read_bytes() + b"#post-validation-tamper\n")
                path.chmod(0o600)

        self.assert_refused(lambda: self.fixture.build(hook=hook))
        self.assertFalse(self.fixture.destination().exists())
        self.assert_no_stage()

    def test_10_wholesale_staged_payload_substitution_is_rejected(self) -> None:
        displaced: pathlib.Path | None = None

        def hook(context) -> None:
            nonlocal displaced
            if context.checkpoint == "after-stage-validation":
                stage = context.parent_path / context.stage_name
                displaced = stage.with_name(stage.name + ".displaced")
                stage.rename(displaced)
                stage.mkdir(mode=0o700)

        self.assert_refused(lambda: self.fixture.build(hook=hook))
        self.assertFalse(self.fixture.destination().exists())
        self.assertIsNotNone(displaced)
        self.assertFalse(displaced.exists())

    def test_11_postpin_executable_replacement_is_rejected(self) -> None:
        mutated = False

        def verifier(path: pathlib.Path) -> None:
            nonlocal mutated
            self.fixture.fake_codesign(path)
            if path.name == builder.PACKAGED_APP_LEAF and not mutated:
                executable = path / builder.SOURCE_APP_EXECUTABLE_RELATIVE_PATH
                executable.unlink()
                executable.write_bytes(b"replacement-after-codesign\n")
                executable.chmod(0o755)
                mutated = True

        self.assert_refused(
            lambda: self.fixture.build(codesign_verifier=verifier)
        )
        self.assertTrue(mutated)
        self.assertEqual(self.fixture.executable.read_bytes(), self.fixture.executable_bytes)
        self.assertFalse(self.fixture.destination().exists())

    def test_12_source_sequential_scan_mutation_is_rejected(self) -> None:
        resource = self.fixture.app / "Contents/Resources/translations/fixture.qm"
        original = resource.read_bytes()
        mutated = False

        def verifier(path: pathlib.Path) -> None:
            nonlocal mutated
            self.fixture.fake_codesign(path)
            if path == self.fixture.app and not mutated:
                resource.write_bytes(original + b"mutation-during-codesign")
                resource.chmod(0o644)
                mutated = True

        try:
            self.assert_refused(
                lambda: self.fixture.build(codesign_verifier=verifier)
            )
        finally:
            resource.write_bytes(original)
            resource.chmod(0o644)
        self.assertTrue(mutated)
        self.assertFalse(self.fixture.destination().exists())

    def test_13_postpublication_tamper_is_quarantined_by_exact_inode(self) -> None:
        published_inode = None

        def hook(context) -> None:
            nonlocal published_inode
            if context.checkpoint == "after-publish":
                published_inode = os.fstat(context.stage_fd).st_ino
                data_fd = builder._open_relative_directory(
                    context.stage_fd, builder.DATA_ROOT_RELATIVE_PATH / "savestates"
                )
                try:
                    builder._write_file_at(data_fd, "forbidden.state", b"x", 0o600)
                finally:
                    os.close(data_fd)

        with self.assertRaises(builder.PublishedProfileQuarantined) as caught:
            self.fixture.build(hook=hook)
        self.assertFalse(self.fixture.destination().exists())
        quarantine = self.fixture.destination_parent / caught.exception.quarantine_leaf
        self.assertTrue(quarantine.is_dir())
        self.assertEqual(os.stat(quarantine).st_ino, published_inode)

    def test_14_postpublication_destination_rebind_is_detected_honestly(self) -> None:
        displaced = self.fixture.destination_parent / "published-displaced"

        def hook(context) -> None:
            if context.checkpoint == "after-publish":
                destination = context.parent_path / context.destination_name
                destination.rename(displaced)
                destination.mkdir(mode=0o700)

        with self.assertRaises(builder.PublishedProfileQuarantineFailed):
            self.fixture.build(hook=hook)
        self.assertTrue(displaced.is_dir())
        self.assertTrue(self.fixture.destination().is_dir())

    def test_15_destination_parent_rebind_before_publish_is_rejected(self) -> None:
        parent = self.fixture.destination_parent
        displaced_parent = parent.with_name(parent.name + "-displaced")

        def hook(context) -> None:
            if context.checkpoint == "before-publish":
                parent.rename(displaced_parent)
                parent.mkdir(mode=0o700)

        self.assert_refused(lambda: self.fixture.build(hook=hook))
        self.assertFalse(self.fixture.destination().exists())
        self.assertTrue(displaced_parent.is_dir())

    def test_16_parent_fsync_failure_is_distinct_published_durability_outcome(self) -> None:
        real_fsync = os.fsync
        parent_identity = os.stat(self.fixture.destination_parent)

        def fsync_with_publication_failure(fd: int) -> None:
            metadata = os.fstat(fd)
            if (
                (metadata.st_dev, metadata.st_ino)
                == (parent_identity.st_dev, parent_identity.st_ino)
                and self.fixture.destination().exists()
            ):
                raise OSError("injected parent fsync failure")
            real_fsync(fd)

        with mock.patch.object(builder.os, "fsync", side_effect=fsync_with_publication_failure):
            with self.assertRaises(builder.PublishedProfileDurabilityUnconfirmed):
                self.fixture.build()
        self.assertTrue(self.fixture.destination().is_dir())
        self.fixture.verify()

    def test_17_verify_rejects_extras_xattrs_and_internal_app_name_changes(self) -> None:
        self.fixture.build()
        safety = (
            self.fixture.destination() / builder.DATA_ROOT_RELATIVE_PATH / "cheats"
            / "unexpected.txt"
        )
        safety.write_bytes(b"unexpected")
        with self.assertRaises(builder.ProfileRefusal):
            self.fixture.verify()
        safety.unlink()

        clone = self.fixture.destination() / builder.PACKAGED_APP_LEAF
        set_xattr(clone, "com.example.fixture", b"tamper")
        with self.assertRaises(builder.ProfileRefusal):
            self.fixture.verify()
        remove_xattr(clone, "com.example.fixture")

        renamed = clone.with_name("Not-An-App")
        clone.rename(renamed)
        with self.assertRaises(builder.ProfileRefusal):
            self.fixture.verify()

    def test_18_existing_destination_symlinks_hardlinks_modes_and_wrong_group_refuse(self) -> None:
        destination = self.fixture.destination()
        destination.mkdir(mode=0o700)
        with self.assertRaises(builder.ProfileRefusal):
            self.fixture.build()
        destination.rmdir()

        hardlink = self.fixture.candidate_parent / "binary-hardlink"
        os.link(self.fixture.binary, hardlink)
        self.assert_refused(lambda: self.fixture.build())
        hardlink.unlink()

        self.fixture.binary.chmod(0o666)
        self.assert_refused(lambda: self.fixture.build())
        self.fixture.binary.chmod(0o600)

        moved_cue = self.fixture.cue.with_suffix(".real")
        self.fixture.cue.rename(moved_cue)
        self.fixture.cue.symlink_to(moved_cue)
        self.assert_refused(lambda: self.fixture.build())
        self.fixture.cue.unlink()
        moved_cue.rename(self.fixture.cue)

        alias = self.fixture.root / "output-alias"
        alias.symlink_to(self.fixture.destination_parent, target_is_directory=True)
        self.assert_refused(lambda: self.fixture.build(destination=alias / "profile"))

        metadata = os.stat(self.fixture.binary)
        with mock.patch.object(builder.os, "getegid", return_value=os.getegid() + 1):
            with self.assertRaises(builder.ProfileRefusal):
                builder._require_safe_metadata(metadata, "wrong-group", "file")

    def test_19_hardlinked_source_app_file_is_rejected_without_mutation(self) -> None:
        outside = self.fixture.root / "held-executable-link"
        before = self.fixture.executable.read_bytes()
        os.link(self.fixture.executable, outside)
        self.assert_refused(lambda: self.fixture.build())
        self.assertEqual(self.fixture.executable.read_bytes(), before)
        self.assertEqual(outside.read_bytes(), before)

    def test_20_unknown_source_xattr_and_simulated_acl_fail_closed(self) -> None:
        set_xattr(self.fixture.app, "com.example.unapproved", b"x")
        self.assert_refused(lambda: self.fixture.build())
        remove_xattr(self.fixture.app, "com.example.unapproved")

        class FakeFunction:
            def __init__(self, result):
                self.result = result
                self.argtypes = None
                self.restype = None

            def __call__(self, *unused):
                return self.result

        getter = FakeFunction(1)
        freer = FakeFunction(0)
        fd = os.open(self.fixture.binary, os.O_RDONLY | os.O_NOFOLLOW)
        try:
            with mock.patch.object(
                builder,
                "_libc_symbol",
                side_effect=lambda name: getter if name == "acl_get_fd_np" else freer,
            ):
                with self.assertRaises(builder.ProfileRefusal):
                    builder._require_no_acl_fd(fd, "simulated ACL")
        finally:
            os.close(fd)

        output_fd = os.open(
            self.fixture.destination_parent,
            os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW,
        )
        try:
            for bad_value in (b"\x01\x03" + b"x" * 9, b"\x01\x02"):
                with self.subTest(provenance=bad_value.hex()):
                    with mock.patch.object(
                        builder, "_read_xattr_names_fd",
                        return_value=(builder.PROVENANCE_XATTR,),
                    ), mock.patch.object(
                        builder, "_read_xattr_value_fd", return_value=bad_value
                    ):
                        with self.assertRaises(builder.ProfileRefusal):
                            builder._validate_policy_metadata_fd(
                                output_fd,
                                "bad provenance",
                                os.fstat(output_fd),
                                xattr_policy="output",
                            )
        finally:
            os.close(output_fd)

        second = Fixture()
        try:
            def mutate_after_manifest(context) -> None:
                if context.checkpoint == "before-publish":
                    stage = context.parent_path / context.stage_name
                    set_xattr(stage / "home", "com.example.postmanifest", b"x")

            with self.assertRaises(builder.ProfileRefusal):
                second.build(hook=mutate_after_manifest)
            self.assertFalse(second.destination().exists())
        finally:
            second.cleanup()

    def test_21_lock_contention_failure_cleanup_and_codesign_failure(self) -> None:
        lock = self.fixture.destination_parent / builder.COMMON_LOCK_LEAF
        lock_fd = os.open(lock, os.O_RDWR | os.O_CREAT | os.O_NOFOLLOW, 0o600)
        try:
            fcntl.flock(lock_fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
            with self.assertRaises(builder.ProfileRefusal):
                self.fixture.build()
        finally:
            os.close(lock_fd)

        def reject_clone(path: pathlib.Path) -> None:
            self.fixture.fake_codesign(path)
            if path.name == builder.PACKAGED_APP_LEAF:
                raise builder.ProfileRefusal("injected strict codesign failure")

        with self.assertRaises(builder.ProfileRefusal):
            self.fixture.build(codesign_verifier=reject_clone)
        self.assertFalse(self.fixture.destination().exists())
        self.assert_no_stage()

    def test_22_ast_process_allowlist_and_ctypes_symbol_closure(self) -> None:
        source = BUILDER_PATH.read_text(encoding="utf-8")
        tree = ast.parse(source)
        process_calls: list[ast.Call] = []
        forbidden_names = {
            "Popen", "call", "check_call", "check_output", "system", "popen",
            "fork", "forkpty", "posix_spawn", "posix_spawnp", "startfile",
        }
        for node in ast.walk(tree):
            if not isinstance(node, ast.Call):
                continue
            function = node.func
            if isinstance(function, ast.Attribute):
                if function.attr == "run" and isinstance(function.value, ast.Name) \
                        and function.value.id == "subprocess":
                    process_calls.append(node)
                if function.attr in forbidden_names or function.attr.startswith("exec") \
                        or function.attr.startswith("spawn"):
                    self.fail(f"forbidden process API: {ast.unparse(function)}")
            elif isinstance(function, ast.Name):
                if function.id in forbidden_names or function.id.startswith("exec") \
                        or function.id.startswith("spawn"):
                    self.fail(f"forbidden process alias: {function.id}")
        self.assertEqual(len(process_calls), 1)
        call = process_calls[0]
        self.assertEqual(
            ast.unparse(call.args[0]),
            "['/usr/bin/codesign', '--verify', '--deep', '--strict', os.fspath(path)]",
        )
        keywords = {item.arg: ast.unparse(item.value) for item in call.keywords}
        self.assertNotIn("shell", keywords)
        self.assertEqual(keywords["check"], "False")
        self.assertEqual(
            builder.LIBC_SYMBOL_ALLOWLIST,
            frozenset({
                "acl_free", "acl_get_fd_np", "flistxattr", "fgetxattr",
                "fremovexattr", "renameatx_np",
            }),
        )
        self.assertNotIn("/usr/bin/open", source)
        self.assertNotIn("open -a", source)

    def test_23_self_test_entrypoint(self) -> None:
        output = io.StringIO()
        with contextlib.redirect_stdout(output):
            self.assertEqual(builder.main(["self-test"]), 0)
        self.assertEqual(output.getvalue(), "SELF_TEST_OK\n")

    def test_24_check_to_rename_substitution_quarantines_uncanonical_publication(self) -> None:
        destination = self.fixture.destination()
        real_rename = builder._rename_exclusive
        triggered = False
        replacement_inode = None
        displaced_leaf = None

        def racing_rename(
            source_parent_fd: int,
            source_name: str,
            destination_parent_fd: int,
            destination_name: str,
        ) -> None:
            nonlocal triggered, replacement_inode, displaced_leaf
            if destination_name == destination.name and not triggered:
                triggered = True
                displaced_leaf = source_name + ".pre-rename-race-original"
                os.rename(
                    source_name,
                    displaced_leaf,
                    src_dir_fd=source_parent_fd,
                    dst_dir_fd=source_parent_fd,
                )
                os.mkdir(source_name, 0o700, dir_fd=source_parent_fd)
                replacement_fd = os.open(
                    source_name,
                    os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW,
                    dir_fd=source_parent_fd,
                )
                try:
                    replacement_inode = os.fstat(replacement_fd).st_ino
                finally:
                    os.close(replacement_fd)
            real_rename(
                source_parent_fd,
                source_name,
                destination_parent_fd,
                destination_name,
            )

        with mock.patch.object(
            builder, "_rename_exclusive", side_effect=racing_rename
        ):
            with self.assertRaises(builder.PublishedProfileQuarantined) as caught:
                self.fixture.build()
        self.assertTrue(triggered)
        self.assertFalse(destination.exists())
        quarantine = self.fixture.destination_parent / caught.exception.quarantine_leaf
        self.assertTrue(quarantine.is_dir())
        self.assertEqual(os.stat(quarantine).st_ino, replacement_inode)
        self.assertIsNotNone(displaced_leaf)
        self.assertFalse((self.fixture.destination_parent / displaced_leaf).exists())

    def test_25_receipt_envelope_rejects_root_and_receipt_metadata_drift(self) -> None:
        self.fixture.build()
        destination = self.fixture.destination()
        receipt_path = destination / builder.RECEIPT_LEAF

        destination.chmod(0o755)
        try:
            with self.assertRaises(builder.ProfileRefusal):
                self.fixture.verify()
        finally:
            destination.chmod(0o700)

        receipt_path.chmod(0o644)
        try:
            with self.assertRaises(builder.ProfileRefusal):
                self.fixture.verify()
        finally:
            receipt_path.chmod(0o600)

        real_read = builder._read_xattr_value_fd
        for target_label in (
            "package envelope/.",
            f"package envelope/{builder.RECEIPT_LEAF}",
        ):
            def drifted_value(fd: int, name: str, label: str, target=target_label):
                raw = real_read(fd, name, label)
                if name == builder.PROVENANCE_XATTR and label == target:
                    self.assertEqual(len(raw), 11)
                    self.assertTrue(raw.startswith(b"\x01\x02"))
                    return raw[:2] + bytes([raw[2] ^ 1]) + raw[3:]
                return raw

            with self.subTest(target=target_label), mock.patch.object(
                builder, "_read_xattr_value_fd", side_effect=drifted_value
            ):
                with self.assertRaises(builder.ProfileRefusal):
                    self.fixture.verify()

    def test_26_quarantine_parent_fsync_failure_reports_known_leaf(self) -> None:
        parent_identity = os.stat(self.fixture.destination_parent)
        real_fsync = os.fsync

        def hook(context) -> None:
            if context.checkpoint == "after-publish":
                data_fd = builder._open_relative_directory(
                    context.stage_fd,
                    builder.DATA_ROOT_RELATIVE_PATH / "savestates",
                )
                try:
                    builder._write_file_at(data_fd, "invalid.state", b"x", 0o600)
                finally:
                    os.close(data_fd)

        def fail_quarantine_fsync(fd: int) -> None:
            metadata = os.fstat(fd)
            if (metadata.st_dev, metadata.st_ino) == (
                parent_identity.st_dev,
                parent_identity.st_ino,
            ) and any(
                name.startswith(f".{self.fixture.destination().name}.quarantine-")
                for name in os.listdir(fd)
            ):
                raise OSError("injected quarantine parent fsync failure")
            real_fsync(fd)

        with mock.patch.object(
            builder.os, "fsync", side_effect=fail_quarantine_fsync
        ):
            with self.assertRaises(
                builder.PublishedProfileQuarantineDurabilityUnconfirmed
            ) as caught:
                self.fixture.build(hook=hook)
        self.assertFalse(self.fixture.destination().exists())
        quarantine = self.fixture.destination_parent / caught.exception.quarantine_leaf
        self.assertTrue(quarantine.is_dir())
        self.assertEqual(os.stat(quarantine).st_ino, caught.exception.inode)

    def test_27_private_stage_cleanup_failure_surfaces_path_and_inode(self) -> None:
        def hook(context) -> None:
            if context.checkpoint == "after-stage-validation":
                settings = (
                    context.parent_path / context.stage_name
                    / builder.SETTINGS_RELATIVE_PATH
                )
                settings.write_bytes(settings.read_bytes() + b"tamper\n")
                settings.chmod(0o600)

        with mock.patch.object(
            builder,
            "_remove_tree_contents_fd",
            side_effect=builder.ProfileRefusal("injected private cleanup failure"),
        ):
            with self.assertRaises(builder.PrivateStageCleanupFailed) as caught:
                self.fixture.build(hook=hook)
        self.assertFalse(self.fixture.destination().exists())
        self.assertTrue(caught.exception.retained_path.is_dir())
        self.assertEqual(
            os.stat(caught.exception.retained_path).st_ino,
            caught.exception.inode,
        )

    def test_28_private_stage_cleanup_fsync_failure_is_distinct(self) -> None:
        parent_identity = os.stat(self.fixture.destination_parent)
        destination = self.fixture.destination()
        private_prefix = f".{destination.name}.private-"
        real_fsync = os.fsync
        injected = False

        def hook(context) -> None:
            if context.checkpoint == "after-stage-validation":
                settings = (
                    context.parent_path / context.stage_name
                    / builder.SETTINGS_RELATIVE_PATH
                )
                settings.write_bytes(settings.read_bytes() + b"tamper\n")
                settings.chmod(0o600)

        def fail_cleanup_fsync(fd: int) -> None:
            nonlocal injected
            metadata = os.fstat(fd)
            if (
                not injected
                and (metadata.st_dev, metadata.st_ino)
                == (parent_identity.st_dev, parent_identity.st_ino)
                and not destination.exists()
                and not any(
                    name.startswith(private_prefix) for name in os.listdir(fd)
                )
            ):
                injected = True
                raise OSError("injected private-stage cleanup parent fsync failure")
            real_fsync(fd)

        with mock.patch.object(builder.os, "fsync", side_effect=fail_cleanup_fsync):
            with self.assertRaises(
                builder.PrivateStageCleanupDurabilityUnconfirmed
            ) as caught:
                self.fixture.build(hook=hook)
        self.assertTrue(injected)
        self.assertFalse(destination.exists())
        self.assertFalse(caught.exception.former_path.exists())
        self.assertGreater(caught.exception.inode, 0)


if __name__ == "__main__":
    unittest.main(verbosity=2)
