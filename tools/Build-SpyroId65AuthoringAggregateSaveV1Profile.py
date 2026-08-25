#!/usr/bin/env python3
"""Build or verify the isolated save-capable ID65 aggregate profile.

This no-launch adapter executes a stable, hash-pinned copy of the frozen V3
profile builder.  Its settings are the frozen no-card settings with exactly one
replacement: Card1Type=None becomes Card1Type=PerGameTitle.  Every other byte
is reverse-projected through the frozen validator before the customized builder
is allowed to write or verify a profile.
"""

from __future__ import annotations

import argparse
import dataclasses
import hashlib
import json
import os
import pathlib
import stat
import sys
import types
from typing import Any, Final


class AggregateSaveProfileRefusal(RuntimeError):
    """The isolated save-capable profile could not be handled exactly."""


class AggregateSaveProfileOperationRefusal(AggregateSaveProfileRefusal):
    """The frozen builder refused with a stable operation-specific exit."""

    def __init__(self, message: str, exit_code: int) -> None:
        super().__init__(message)
        self.exit_code = exit_code


class AggregateSaveProfilePublishedBindingUnconfirmed(
    AggregateSaveProfileOperationRefusal
):
    """The profile published, but a wrapper-owned binding was not reconfirmed."""

    def __init__(self, destination: pathlib.Path, reason: str) -> None:
        super().__init__(
            f"profile published at {destination}, but save-profile input binding "
            f"could not be reconfirmed after publication: {reason}",
            9,
        )


SCRIPT_PATH: Final = pathlib.Path(os.path.abspath(__file__))
PRODUCTION_REPOSITORY_ROOT: Final = SCRIPT_PATH.parent.parent
FROZEN_BUILDER_RELATIVE_PATH: Final = pathlib.PurePosixPath(
    "tools/Build-SpyroId65UnattendedProfile.py"
)
EXPECTED_FROZEN_BUILDER_SHA256: Final = (
    "0591f1b6bf3f61dac4bfa61d407df65a2a9862569c8d2a7678d216b95929b95c"
)
MAX_SOURCE_BYTES: Final = 512 * 1024
READ_CHUNK_SIZE: Final = 1024 * 1024

PROFILE_ID: Final = "duckstation-id65-authoring-aggregate-save-v1"
PROFILE_DESTINATION_RELATIVE_PATH: Final = pathlib.PurePosixPath(
    "_local/v5-stone-hill-level-replacement/"
    "duckstation-id65-authoring-aggregate-save-v1"
)
SHARED_PARENT_LOCK_RELATIVE_PATH: Final = pathlib.PurePosixPath(
    "_local/v5-stone-hill-level-replacement/"
    ".spyro-id65-unattended-profile.lock"
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

EXPECTED_CANDIDATE_CUE_SHA256: Final = (
    "91c12af236642fc8ad601995cea86d22c414eb1b692a4957cc3f9fa3aedd6de8"
)
EXPECTED_CANDIDATE_CUE_SIZE: Final = 114
EXPECTED_CANDIDATE_BIN_SHA256: Final = (
    "083dd5b7e258b59010e87c3262274e4da9cb86b1a6a15e7730a8b5eeac001647"
)
EXPECTED_CANDIDATE_BIN_SIZE: Final = 661_547_040
EXPECTED_CANDIDATE_RECEIPT_FILE_SHA256: Final = (
    "9f11a3811a54ef4932c5cb4d184877fe957f06eb003680c739d2478c6e7ed03b"
)
EXPECTED_CANDIDATE_RECEIPT_FILE_SIZE: Final = 3_064
EXPECTED_CANDIDATE_RECEIPT_CONTENT_SHA256: Final = (
    "d6c14861cde023e9e100fe2828227da8252c9de37c36e84d33ba4bfbd3b0516f"
)
EXPECTED_DOCUMENT_ENVELOPE_SHA256: Final = (
    "5669f5e29010009fa7a698f4eed213dbd7163bd3483ec37beba7732bda321b70"
)
EXPECTED_DOCUMENT_FILE_SIZE: Final = 3_931
EXPECTED_DOCUMENT_CANONICAL_SHA256: Final = (
    "8f27bfc3f97238402c0b7dd59a081335ae19df1acfaefa9575d9ca7110dce808"
)
EXPECTED_DOCUMENT_CONTENT_SHA256: Final = (
    "9d853ccfcf18627a46dc730844efc9eeeda28ee9a0d53c70ae3b7144652b4c3b"
)
EXPECTED_AGGREGATE_PLAN_SHA256: Final = (
    "4955c54b3eb7ea5c1871205e559f5d5dda0766613e55aa0de988b92e896d5c21"
)
EXPECTED_COMPOSITE_MANIFEST_SHA256: Final = (
    "62635a61a79a59a59d052f50c3f719b56ea1bcdecf09ae91030063be410cfa34"
)
EXPECTED_OUTPUT_ROW80_SHA256: Final = (
    "dad25853f92e4f8300036edd85dae374672d265ba17c7b2682c6b524661b75f3"
)

EXPECTED_NO_CARD_SETTINGS_SIZE: Final = 2_143
EXPECTED_NO_CARD_SETTINGS_SHA256: Final = (
    "469ef98771f26395352bac1d43f90e4be14693fd4ba5316e4d71e0557a7f74f9"
)
NO_CARD_LINE: Final = b"Card1Type = None\n"
SAVE_CARD_LINE: Final = b"Card1Type = PerGameTitle\n"
EXPECTED_SAVE_SETTINGS_SIZE: Final = 2_151
EXPECTED_SAVE_SETTINGS_SHA256: Final = (
    "4ea5cced616c989e3df2be8a6ddb9507a9c295704c1acd2b586ec19fee28e8cb"
)
LAUNCH_GATE: Final = "EXTERNAL_RUNTIME_LAUNCH_REQUIRED"
BUILD_HOOK_CHECKPOINTS: Final = (
    "after-initial-validation",
    "after-stage-validation",
    "before-publish",
    "after-publish",
)


def _sha256_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def _canonical_json(value: object) -> str:
    return json.dumps(
        value,
        allow_nan=False,
        ensure_ascii=False,
        sort_keys=True,
        separators=(",", ":"),
    )


def _require_absolute_normalized(path: pathlib.Path, label: str) -> pathlib.Path:
    if (
        not isinstance(path, pathlib.Path)
        or not path.is_absolute()
        or pathlib.Path(os.path.normpath(os.fspath(path))) != path
    ):
        raise AggregateSaveProfileRefusal(
            f"{label} must be an explicit absolute normalized pathlib.Path."
        )
    return path


def _reject_symlink_segments(path: pathlib.Path, label: str) -> None:
    _require_absolute_normalized(path, label)
    current = pathlib.Path(path.anchor)
    for segment in path.parts[1:]:
        current /= segment
        try:
            metadata = os.lstat(current)
        except FileNotFoundError as exc:
            raise AggregateSaveProfileRefusal(
                f"{label} has a missing path segment."
            ) from exc
        if stat.S_ISLNK(metadata.st_mode):
            raise AggregateSaveProfileRefusal(
                f"{label} traverses a symbolic link."
            )


def _require_repository_root(value: pathlib.Path) -> pathlib.Path:
    root = _require_absolute_normalized(value, "repository root")
    _reject_symlink_segments(root, "repository root")
    try:
        expected = PRODUCTION_REPOSITORY_ROOT.resolve(strict=True)
    except OSError as exc:
        raise AggregateSaveProfileRefusal(
            f"audited repository root cannot be resolved: {exc}"
        ) from exc
    if root != expected:
        raise AggregateSaveProfileRefusal(
            "repository root must be the real checkout containing this adapter."
        )
    return root


def _stable_read_source(
    path: pathlib.Path,
    expected_sha256: str,
    label: str,
) -> bytes:
    _reject_symlink_segments(path, label)
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
            raise AggregateSaveProfileRefusal(f"{label} metadata is unsafe.")
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
            getattr(before, name) != getattr(after, name) for name in fields
        ):
            raise AggregateSaveProfileRefusal(f"{label} changed while read.")
        if digest.hexdigest() != expected_sha256:
            raise AggregateSaveProfileRefusal(
                f"{label} hash changed: {digest.hexdigest()}."
            )
        return b"".join(chunks)
    finally:
        os.close(fd)


def _load_frozen_builder(repository_root: pathlib.Path) -> types.ModuleType:
    root = _require_repository_root(repository_root)
    path = root / FROZEN_BUILDER_RELATIVE_PATH
    source = _stable_read_source(
        path, EXPECTED_FROZEN_BUILDER_SHA256, "frozen profile builder"
    )
    name = "_spyro_id65_frozen_builder_for_authoring_aggregate_save_v1"
    module = types.ModuleType(name)
    module.__file__ = os.fspath(path)
    module.__package__ = None
    previous = sys.modules.get(name)
    sys.modules[name] = module
    try:
        exec(compile(source, os.fspath(path), "exec", dont_inherit=True), module.__dict__)
    except Exception:
        if previous is None:
            sys.modules.pop(name, None)
        else:
            sys.modules[name] = previous
        raise
    _require_builder_contract(module)
    return module


def _require_builder_contract(builder: types.ModuleType) -> None:
    required = (
        "BuildPins", "PRODUCTION_PINS", "CANONICAL_SETTINGS",
        "CANONICAL_SETTINGS_SHA256", "SOURCE_APP_PATH", "BIOS_PATH",
        "COMMON_LOCK_LEAF", "build_profile", "verify_profile",
        "_parse_ini", "_validate_settings", "_open_pinned_file",
        "_repin_held_file", "_close_pinned_file", "ProfileRefusal",
        "PublishedProfileQuarantined", "PublishedProfileDurabilityUnconfirmed",
        "PublishedProfileQuarantineFailed",
        "PublishedProfileQuarantineDurabilityUnconfirmed",
        "PrivateStageCleanupFailed", "PrivateStageCleanupDurabilityUnconfirmed",
    )
    missing = [name for name in required if not hasattr(builder, name)]
    if missing:
        raise AggregateSaveProfileRefusal(
            f"frozen profile builder API changed: {','.join(missing)}."
        )
    if builder.COMMON_LOCK_LEAF != SHARED_PARENT_LOCK_RELATIVE_PATH.name:
        raise AggregateSaveProfileRefusal(
            "frozen common profile lock identity changed."
        )


def _configure_save_settings(builder: types.ModuleType) -> bytes:
    original = builder.CANONICAL_SETTINGS
    if (
        not isinstance(original, bytes)
        or len(original) != EXPECTED_NO_CARD_SETTINGS_SIZE
        or _sha256_bytes(original) != EXPECTED_NO_CARD_SETTINGS_SHA256
        or builder.CANONICAL_SETTINGS_SHA256 != EXPECTED_NO_CARD_SETTINGS_SHA256
        or builder.PRODUCTION_PINS.settings_size != EXPECTED_NO_CARD_SETTINGS_SIZE
        or builder.PRODUCTION_PINS.settings_sha256
        != EXPECTED_NO_CARD_SETTINGS_SHA256
    ):
        raise AggregateSaveProfileRefusal(
            "frozen no-card canonical settings identity changed."
        )
    if original.count(NO_CARD_LINE) != 1 or original.count(SAVE_CARD_LINE) != 0:
        raise AggregateSaveProfileRefusal(
            "no-card settings do not contain one exact Card1 replacement site."
        )
    replacement_offset = original.index(NO_CARD_LINE)
    save_settings = (
        original[:replacement_offset]
        + SAVE_CARD_LINE
        + original[replacement_offset + len(NO_CARD_LINE):]
    )
    if (
        len(save_settings) != EXPECTED_SAVE_SETTINGS_SIZE
        or _sha256_bytes(save_settings) != EXPECTED_SAVE_SETTINGS_SHA256
        or save_settings.count(SAVE_CARD_LINE) != 1
        or save_settings.count(NO_CARD_LINE) != 0
    ):
        raise AggregateSaveProfileRefusal(
            "save-capable settings exact replacement projection changed."
        )
    reverse = save_settings.replace(SAVE_CARD_LINE, NO_CARD_LINE, 1)
    if reverse != original:
        raise AggregateSaveProfileRefusal(
            "save-capable settings do not reverse-project to the frozen settings."
        )

    frozen_validator = builder._validate_settings
    frozen_validator(
        reverse,
        dataclasses.replace(
            builder.PRODUCTION_PINS,
            settings_size=EXPECTED_NO_CARD_SETTINGS_SIZE,
            settings_sha256=EXPECTED_NO_CARD_SETTINGS_SHA256,
        ),
    )

    def validate_save_settings(raw: bytes, pins: Any) -> None:
        if (
            not isinstance(raw, bytes)
            or len(raw) != EXPECTED_SAVE_SETTINGS_SIZE
            or _sha256_bytes(raw) != EXPECTED_SAVE_SETTINGS_SHA256
            or pins.settings_size != EXPECTED_SAVE_SETTINGS_SIZE
            or pins.settings_sha256 != EXPECTED_SAVE_SETTINGS_SHA256
            or raw.count(SAVE_CARD_LINE) != 1
            or raw.count(NO_CARD_LINE) != 0
        ):
            raise AggregateSaveProfileRefusal(
                "settings.ini bytes are not the exact save-capable projection."
            )
        projected = raw.replace(SAVE_CARD_LINE, NO_CARD_LINE, 1)
        if projected != original:
            raise AggregateSaveProfileRefusal(
                "settings.ini differs outside the one authorized Card1 line."
            )
        frozen_validator(
            projected,
            dataclasses.replace(
                pins,
                settings_size=EXPECTED_NO_CARD_SETTINGS_SIZE,
                settings_sha256=EXPECTED_NO_CARD_SETTINGS_SHA256,
            ),
        )
        memory_cards = dict(builder._parse_ini(raw).get("MemoryCards", ()))
        if memory_cards != {
            "Card1Type": "PerGameTitle",
            "Card2Type": "None",
            "Directory": "memcards",
        }:
            raise AggregateSaveProfileRefusal(
                "settings.ini memory-card policy is not the exact save profile."
            )

    builder.CANONICAL_SETTINGS = save_settings
    builder.CANONICAL_SETTINGS_SHA256 = EXPECTED_SAVE_SETTINGS_SHA256
    builder._validate_settings = validate_save_settings
    return save_settings


def _save_build_pins(builder: types.ModuleType) -> Any:
    _configure_save_settings(builder)
    pins = dataclasses.replace(
        builder.PRODUCTION_PINS,
        candidate_cue_name=f"{CANDIDATE_STEM}.cue",
        candidate_cue_sha256=EXPECTED_CANDIDATE_CUE_SHA256,
        candidate_cue_size=EXPECTED_CANDIDATE_CUE_SIZE,
        candidate_bin_name=f"{CANDIDATE_STEM}.bin",
        candidate_bin_sha256=EXPECTED_CANDIDATE_BIN_SHA256,
        candidate_bin_size=EXPECTED_CANDIDATE_BIN_SIZE,
        settings_sha256=EXPECTED_SAVE_SETTINGS_SHA256,
        settings_size=EXPECTED_SAVE_SETTINGS_SIZE,
    )
    if not isinstance(pins, builder.BuildPins):
        raise AggregateSaveProfileRefusal("save profile pins changed type.")
    builder._validate_settings(builder.CANONICAL_SETTINGS, pins)
    return pins


def _paths(
    repository_root: pathlib.Path,
) -> tuple[pathlib.Path, pathlib.Path, pathlib.Path, pathlib.Path, pathlib.Path]:
    destination = repository_root / PROFILE_DESTINATION_RELATIVE_PATH
    cue = repository_root / CANDIDATE_CUE_RELATIVE_PATH
    binary = repository_root / CANDIDATE_BIN_RELATIVE_PATH
    receipt = repository_root / CANDIDATE_RECEIPT_RELATIVE_PATH
    document = repository_root / DOCUMENT_RELATIVE_PATH
    expected_parent = repository_root / SHARED_PARENT_LOCK_RELATIVE_PATH.parent
    if destination.parent != expected_parent or destination.name != PROFILE_ID:
        raise AggregateSaveProfileRefusal("save profile destination identity changed.")
    return destination, cue, binary, receipt, document


def _read_held_json(handle: Any, expected_size: int, label: str) -> dict[str, object]:
    os.lseek(handle.fd, 0, os.SEEK_SET)
    raw = os.read(handle.fd, expected_size + 1)
    os.lseek(handle.fd, 0, os.SEEK_SET)
    if len(raw) != expected_size:
        raise AggregateSaveProfileRefusal(f"{label} byte length changed while held.")
    try:
        parsed = json.loads(raw)
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise AggregateSaveProfileRefusal(f"{label} is not strict JSON: {exc}") from exc
    if not isinstance(parsed, dict):
        raise AggregateSaveProfileRefusal(f"{label} root must be an object.")
    return parsed


def _require_aggregate_metadata(
    builder: types.ModuleType,
    receipt_path: pathlib.Path,
    document_path: pathlib.Path,
) -> tuple[Any, Any]:
    receipt = builder._open_pinned_file(
        receipt_path,
        "aggregate candidate receipt",
        expected_name=f"{CANDIDATE_STEM}.receipt.json",
        expected_size=EXPECTED_CANDIDATE_RECEIPT_FILE_SIZE,
        expected_sha256=EXPECTED_CANDIDATE_RECEIPT_FILE_SHA256,
    )
    document: Any | None = None
    try:
        document = builder._open_pinned_file(
            document_path,
            "aggregate authoring document",
            expected_name=DOCUMENT_RELATIVE_PATH.name,
            expected_size=EXPECTED_DOCUMENT_FILE_SIZE,
            expected_sha256=EXPECTED_DOCUMENT_ENVELOPE_SHA256,
        )
        receipt_json = _read_held_json(
            receipt, EXPECTED_CANDIDATE_RECEIPT_FILE_SIZE,
            "aggregate candidate receipt",
        )
        document_json = _read_held_json(
            document, EXPECTED_DOCUMENT_FILE_SIZE,
            "aggregate authoring document",
        )
        expected_receipt = {
            "format": "spyro-editor-id65-authoring-editor-aggregate-candidate-receipt",
            "schemaVersion": 1,
            "documentEnvelopeSha256": EXPECTED_DOCUMENT_ENVELOPE_SHA256,
            "documentCanonicalSha256": EXPECTED_DOCUMENT_CANONICAL_SHA256,
            "aggregatePlanSha256": EXPECTED_AGGREGATE_PLAN_SHA256,
            "outputRow80Sha256": EXPECTED_OUTPUT_ROW80_SHA256,
            "outputImageSha256": EXPECTED_CANDIDATE_BIN_SHA256,
            "candidateCueSha256": EXPECTED_CANDIDATE_CUE_SHA256,
            "receiptContentSha256": EXPECTED_CANDIDATE_RECEIPT_CONTENT_SHA256,
            "runtimeCandidateAuthorized": False,
            "runtimeProven": False,
            "createBinEnabled": False,
            "normalCreateBinEnabled": False,
            "releaseAuthorized": False,
            "publishable": False,
        }
        for name, expected in expected_receipt.items():
            if receipt_json.get(name) != expected:
                raise AggregateSaveProfileRefusal(
                    f"aggregate candidate receipt field changed: {name}."
                )
        expected_document = {
            "format": "spyro-editor-id65-authoring-editor-document-envelope",
            "schemaVersion": 1,
            "documentCanonicalSha256": EXPECTED_DOCUMENT_CANONICAL_SHA256,
            "compositeManifestSha256": EXPECTED_COMPOSITE_MANIFEST_SHA256,
            "compositeOutputRow80Sha256": EXPECTED_OUTPUT_ROW80_SHA256,
        }
        for name, expected in expected_document.items():
            if document_json.get(name) != expected:
                raise AggregateSaveProfileRefusal(
                    f"aggregate authoring document field changed: {name}."
                )
        nested = document_json.get("document")
        if (
            not isinstance(nested, dict)
            or nested.get("contentSha256") != EXPECTED_DOCUMENT_CONTENT_SHA256
        ):
            raise AggregateSaveProfileRefusal(
                "aggregate authoring document content closure changed."
            )
        return receipt, document
    except Exception:
        if document is not None:
            builder._close_pinned_file(document)
        builder._close_pinned_file(receipt)
        raise


def _translate_builder_refusal(builder: types.ModuleType, exc: Exception) -> None:
    mappings = (
        (builder.PublishedProfileQuarantined, 3),
        (builder.PublishedProfileDurabilityUnconfirmed, 4),
        (builder.PublishedProfileQuarantineFailed, 5),
        (builder.PublishedProfileQuarantineDurabilityUnconfirmed, 6),
        (builder.PrivateStageCleanupFailed, 7),
        (builder.PrivateStageCleanupDurabilityUnconfirmed, 8),
    )
    for exception_type, exit_code in mappings:
        if isinstance(exc, exception_type):
            raise AggregateSaveProfileOperationRefusal(str(exc), exit_code) from exc
    if isinstance(exc, builder.ProfileRefusal):
        raise AggregateSaveProfileOperationRefusal(str(exc), 2) from exc
    raise exc


def _run_profile_operation(
    operation: str,
    repository_root: pathlib.Path,
) -> dict[str, object]:
    if operation not in {"build", "verify"}:
        raise AggregateSaveProfileRefusal("profile operation is not build or verify.")
    root = _require_repository_root(repository_root)
    builder = _load_frozen_builder(root)
    pins = _save_build_pins(builder)
    destination, cue, binary, receipt, document = _paths(root)
    receipt_handle: Any | None = None
    document_handle: Any | None = None
    observed_checkpoints: list[str] = []
    try:
        receipt_handle, document_handle = _require_aggregate_metadata(
            builder, receipt, document
        )

        def repin_wrapper_inputs(context: Any) -> None:
            index = len(observed_checkpoints)
            if (
                index >= len(BUILD_HOOK_CHECKPOINTS)
                or context.checkpoint != BUILD_HOOK_CHECKPOINTS[index]
            ):
                raise AggregateSaveProfileRefusal(
                    "frozen builder save-profile hook checkpoint changed."
                )
            observed_checkpoints.append(context.checkpoint)
            builder._repin_held_file(receipt_handle, "aggregate candidate receipt")
            builder._repin_held_file(document_handle, "aggregate authoring document")
            builder._validate_settings(builder.CANONICAL_SETTINGS, pins)

        try:
            if operation == "build":
                result = builder.build_profile(
                    builder.SOURCE_APP_PATH,
                    destination,
                    cue,
                    binary,
                    builder.BIOS_PATH,
                    pins,
                    hook=repin_wrapper_inputs,
                )
            else:
                result = builder.verify_profile(
                    builder.SOURCE_APP_PATH,
                    destination,
                    cue,
                    binary,
                    builder.BIOS_PATH,
                    pins,
                )
        except Exception as exc:
            _translate_builder_refusal(builder, exc)
            raise AssertionError("unreachable")

        try:
            builder._repin_held_file(receipt_handle, "aggregate candidate receipt")
            builder._repin_held_file(document_handle, "aggregate authoring document")
            builder._validate_settings(builder.CANONICAL_SETTINGS, pins)
            if result.destination != destination or result.launch_gate != LAUNCH_GATE:
                raise AggregateSaveProfileRefusal(
                    "frozen profile result identity changed."
                )
            if operation == "build" and tuple(observed_checkpoints) != (
                BUILD_HOOK_CHECKPOINTS
            ):
                raise AggregateSaveProfileRefusal(
                    "frozen builder save-profile hook closure is incomplete."
                )
        except Exception as exc:
            if operation == "build":
                raise AggregateSaveProfilePublishedBindingUnconfirmed(
                    destination, str(exc)
                ) from exc
            raise
    finally:
        if document_handle is not None:
            builder._close_pinned_file(document_handle)
        if receipt_handle is not None:
            builder._close_pinned_file(receipt_handle)

    return {
        "format": "spyro-id65-authoring-aggregate-save-profile-result",
        "formatVersion": 1,
        "operation": operation,
        "profileId": PROFILE_ID,
        "destination": os.fspath(destination),
        "candidateCueSha256": EXPECTED_CANDIDATE_CUE_SHA256,
        "candidateBinSha256": EXPECTED_CANDIDATE_BIN_SHA256,
        "candidateReceiptContentSha256": EXPECTED_CANDIDATE_RECEIPT_CONTENT_SHA256,
        "documentCanonicalSha256": EXPECTED_DOCUMENT_CANONICAL_SHA256,
        "aggregatePlanSha256": EXPECTED_AGGREGATE_PLAN_SHA256,
        "outputRow80Sha256": EXPECTED_OUTPUT_ROW80_SHA256,
        "noCardSettingsSha256": EXPECTED_NO_CARD_SETTINGS_SHA256,
        "settingsSha256": EXPECTED_SAVE_SETTINGS_SHA256,
        "memoryCard1Type": "PerGameTitle",
        "memoryCard2Type": "None",
        "memoryCardDirectory": "memcards",
        "handoffReceiptSha256": result.receipt_sha256,
        "handoffReceiptSchema": result.receipt_schema,
        "appContentManifestSha256": result.app_manifest_sha256,
        "sharedParentLock": os.fspath(root / SHARED_PARENT_LOCK_RELATIVE_PATH),
        "launchGate": result.launch_gate,
        "profileBuilt": operation == "build",
        "profileVerified": True,
        "duckStationLaunched": False,
        "runtimeSaveProven": False,
        "postRunSnapshotRequired": True,
        "postRunFinalizerRequired": True,
        "evidenceAccepted": False,
        "createBinAuthorized": False,
        "releaseAuthorized": False,
        "publishable": False,
    }


def _self_test(repository_root: pathlib.Path) -> None:
    root = _require_repository_root(repository_root)
    builder = _load_frozen_builder(root)
    pins = _save_build_pins(builder)
    destination, cue, binary, receipt, document = _paths(root)
    expected_cue = (
        f'FILE "{binary.name}" BINARY\n'
        "  TRACK 01 MODE2/2352\n"
        "    INDEX 01 00:00:00\n"
    ).encode("ascii")
    if (
        destination.parent / builder.COMMON_LOCK_LEAF
        != root / SHARED_PARENT_LOCK_RELATIVE_PATH
        or cue.name != pins.candidate_cue_name
        or binary.name != pins.candidate_bin_name
        or len(expected_cue) != pins.candidate_cue_size
        or _sha256_bytes(expected_cue) != pins.candidate_cue_sha256
        or pins.candidate_bin_size != EXPECTED_CANDIDATE_BIN_SIZE
        or pins.candidate_bin_sha256 != EXPECTED_CANDIDATE_BIN_SHA256
        or pins.settings_size != EXPECTED_SAVE_SETTINGS_SIZE
        or pins.settings_sha256 != EXPECTED_SAVE_SETTINGS_SHA256
        or receipt.name != f"{CANDIDATE_STEM}.receipt.json"
        or document.name != DOCUMENT_RELATIVE_PATH.name
    ):
        raise AggregateSaveProfileRefusal("save profile adapter self-test failed.")
    receipt_handle, document_handle = _require_aggregate_metadata(
        builder, receipt, document
    )
    builder._close_pinned_file(document_handle)
    builder._close_pinned_file(receipt_handle)


def _build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description=(
            "Build or verify the no-launch DuckStation profile for the exact ID65 "
            "aggregate with one per-game memory card."
        )
    )
    parser.add_argument("operation", choices=("self-test", "build", "verify"))
    parser.add_argument("--repository-root", required=True, type=pathlib.Path)
    return parser


def main(argv: list[str] | None = None) -> int:
    arguments = _build_parser().parse_args(argv)
    try:
        if arguments.operation == "self-test":
            _self_test(arguments.repository_root)
            print("ID65_AUTHORING_AGGREGATE_SAVE_PROFILE_ADAPTER_SELF_TEST_OK")
            return 0
        print(_canonical_json(_run_profile_operation(
            arguments.operation, arguments.repository_root
        )))
        return 0
    except AggregateSaveProfileOperationRefusal as exc:
        print(f"REFUSED: {exc}", file=sys.stderr)
        return exc.exit_code
    except AggregateSaveProfileRefusal as exc:
        print(f"REFUSED: {exc}", file=sys.stderr)
        return 2
    except Exception as exc:
        print(
            f"REFUSED: unexpected save profile failure: {type(exc).__name__}: {exc}",
            file=sys.stderr,
        )
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
