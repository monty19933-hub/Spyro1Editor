#!/usr/bin/env python3
"""Fail-closed verifier for the ID65 authoring-aggregate DuckStation profile.

The frozen V3 verifier and builder are loaded only from hash-pinned source
bytes.  This adapter supplies aggregate-specific profile and candidate pins; it
never calls the frozen verifier's production-pin prelaunch wrapper, builds a
profile, or launches DuckStation.
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


class AggregateProfileVerificationRefusal(RuntimeError):
    """The aggregate profile identity could not be proven exactly."""


SCRIPT_PATH: Final = pathlib.Path(os.path.abspath(__file__))
PRODUCTION_REPOSITORY_ROOT: Final = SCRIPT_PATH.parent.parent
FROZEN_VERIFIER_RELATIVE_PATH: Final = pathlib.PurePosixPath(
    "tools/Verify-SpyroId65UnattendedProfile.py"
)
EXPECTED_FROZEN_VERIFIER_SHA256: Final = (
    "74f4ed279a3e3f60af6c4c0a0b1fa8ffc0a67886c478d8cc7d1a288b093c3671"
)
FROZEN_BUILDER_RELATIVE_PATH: Final = pathlib.PurePosixPath(
    "tools/Build-SpyroId65UnattendedProfile.py"
)
EXPECTED_FROZEN_BUILDER_SHA256: Final = (
    "0591f1b6bf3f61dac4bfa61d407df65a2a9862569c8d2a7678d216b95929b95c"
)

PROFILE_ID: Final = "duckstation-id65-authoring-aggregate-v1"
PROFILE_DESTINATION_RELATIVE_PATH: Final = pathlib.PurePosixPath(
    "_local/v5-stone-hill-level-replacement/"
    "duckstation-id65-authoring-aggregate-v1"
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

EXPECTED_CANDIDATE_CUE_SIZE: Final = 114
EXPECTED_CANDIDATE_CUE_SHA256: Final = (
    "91c12af236642fc8ad601995cea86d22c414eb1b692a4957cc3f9fa3aedd6de8"
)
EXPECTED_CANDIDATE_BIN_SIZE: Final = 661_547_040
EXPECTED_CANDIDATE_BIN_SHA256: Final = (
    "083dd5b7e258b59010e87c3262274e4da9cb86b1a6a15e7730a8b5eeac001647"
)
EXPECTED_CANDIDATE_RECEIPT_FILE_SIZE: Final = 3_064
EXPECTED_CANDIDATE_RECEIPT_FILE_SHA256: Final = (
    "9f11a3811a54ef4932c5cb4d184877fe957f06eb003680c739d2478c6e7ed03b"
)
EXPECTED_CANDIDATE_RECEIPT_CONTENT_SHA256: Final = (
    "d6c14861cde023e9e100fe2828227da8252c9de37c36e84d33ba4bfbd3b0516f"
)
EXPECTED_DOCUMENT_FILE_SIZE: Final = 3_931
EXPECTED_DOCUMENT_ENVELOPE_SHA256: Final = (
    "5669f5e29010009fa7a698f4eed213dbd7163bd3483ec37beba7732bda321b70"
)
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

PENDING: Final = "PENDING"
# These values were frozen from the first no-launch build of the fixed aggregate
# profile.  The all-or-none validator below prevents a partially updated set
# from being used as production evidence.
EXPECTED_PROFILE_OUTPUT_PINS: Final[dict[str, int | str]] = {
    "receiptSize": 74_510,
    "receiptSha256": (
        "c35c4c2b74f3481206737fb8f310bb5478a6f00df0d53655d9e4ac14230a1fe2"
    ),
    "receiptClosureSha256": (
        "1f826b36b14ee330330cf574e8080a5f744d7b8c5da6e75475d593ec81b19c7f"
    ),
    "appOutputTreeEntryCount": 292,
    "appOutputTreeManifestSha256": (
        "08d0a74450b709ab7e702182d64db55f6bc3395728181b052fec307c4d320ba7"
    ),
    "homeOutputTreeEntryCount": 12,
    "homeOutputTreeManifestSha256": (
        "b00119fd7647647c81c7571d2507dbbce2075bd10cdcd8af8db8ae1e006fc2b0"
    ),
    "packageEnvelopeEntryCount": 4,
    "packageEnvelopeSha256": (
        "31f24a6d8ea38fa19abeae54392fa96e2494b5cfb0a5621d2cc29771a617f866"
    ),
    "candidateManifestSha256": (
        "44f74dcefab535670d4abe0e1b1bcec35b832dec421d9caea8c6c132b860681d"
    ),
}

PREFLIGHT_FORMAT: Final = (
    "spyro-id65-authoring-aggregate-profile-preflight"
)
PREFLIGHT_FORMAT_VERSION: Final = 1
STATUS_FORMAT: Final = "spyro-id65-authoring-aggregate-profile-verifier-status"
STATUS_FORMAT_VERSION: Final = 1
LAUNCH_GATE: Final = "EXTERNAL_RUNTIME_LAUNCH_REQUIRED"
MAX_SOURCE_BYTES: Final = 512 * 1024
READ_CHUNK_SIZE: Final = 1024 * 1024
_SHA256_LENGTH: Final = 64


@dataclasses.dataclass
class _AggregateHandles:
    cue: Any
    binary: Any
    candidate_receipt: Any
    document: Any


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


def _is_sha256(value: object) -> bool:
    return (
        isinstance(value, str)
        and len(value) == _SHA256_LENGTH
        and all(character in "0123456789abcdef" for character in value)
    )


def _require_absolute_normalized(path: pathlib.Path, label: str) -> pathlib.Path:
    if (
        not isinstance(path, pathlib.Path)
        or not path.is_absolute()
        or pathlib.Path(os.path.normpath(os.fspath(path))) != path
    ):
        raise AggregateProfileVerificationRefusal(
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
            raise AggregateProfileVerificationRefusal(
                f"{label} has a missing path segment."
            ) from exc
        if stat.S_ISLNK(metadata.st_mode):
            raise AggregateProfileVerificationRefusal(
                f"{label} traverses a symbolic link."
            )


def _require_repository_root(value: pathlib.Path) -> pathlib.Path:
    root = _require_absolute_normalized(value, "repository root")
    _reject_symlink_segments(root, "repository root")
    if root != PRODUCTION_REPOSITORY_ROOT:
        raise AggregateProfileVerificationRefusal(
            "repository root must be the audited checkout containing this verifier."
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
            raise AggregateProfileVerificationRefusal(f"{label} metadata is unsafe.")
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
        identity_fields = (
            "st_dev",
            "st_ino",
            "st_mode",
            "st_uid",
            "st_gid",
            "st_nlink",
            "st_size",
            "st_mtime_ns",
            "st_ctime_ns",
        )
        if observed != before.st_size or any(
            getattr(before, field) != getattr(after, field)
            for field in identity_fields
        ):
            raise AggregateProfileVerificationRefusal(f"{label} changed while read.")
        observed_sha256 = digest.hexdigest()
        if observed_sha256 != expected_sha256:
            raise AggregateProfileVerificationRefusal(
                f"{label} hash changed: {observed_sha256}."
            )
        return b"".join(chunks)
    finally:
        os.close(fd)


def _load_frozen_verifier(repository_root: pathlib.Path) -> types.ModuleType:
    root = _require_repository_root(repository_root)
    path = root / FROZEN_VERIFIER_RELATIVE_PATH
    source = _stable_read_source(
        path,
        EXPECTED_FROZEN_VERIFIER_SHA256,
        "frozen profile verifier",
    )
    module_name = "_spyro_id65_frozen_verifier_for_authoring_aggregate_v1"
    module = types.ModuleType(module_name)
    module.__file__ = os.fspath(path)
    module.__package__ = ""
    previous = sys.modules.get(module_name)
    sys.modules[module_name] = module
    try:
        exec(
            compile(source, os.fspath(path), "exec", dont_inherit=True),
            module.__dict__,
        )
    except Exception:
        if previous is None:
            sys.modules.pop(module_name, None)
        else:
            sys.modules[module_name] = previous
        raise
    _require_frozen_verifier_contract(module)
    return module


def _require_frozen_verifier_contract(verifier: types.ModuleType) -> None:
    required = (
        "ProfilePins",
        "ProfileVerificationRefusal",
        "PRODUCTION_PINS",
        "EXPECTED_BUILDER_SHA256",
        "BUILDER_RELATIVE_PATH",
        "_load_frozen_builder",
        "_require_builder_contract",
        "_read_receipt_only",
        "_capture_runtime_snapshot_with_builder",
    )
    missing = [name for name in required if not hasattr(verifier, name)]
    if missing:
        raise AggregateProfileVerificationRefusal(
            f"frozen verifier API changed: {','.join(missing)}."
        )
    if (
        verifier.EXPECTED_BUILDER_SHA256 != EXPECTED_FROZEN_BUILDER_SHA256
        or verifier.BUILDER_RELATIVE_PATH != FROZEN_BUILDER_RELATIVE_PATH
    ):
        raise AggregateProfileVerificationRefusal(
            "frozen verifier no longer binds the expected frozen builder."
        )


def _load_frozen_builder(
    verifier: types.ModuleType,
    repository_root: pathlib.Path,
) -> types.ModuleType:
    # The frozen verifier performs its own stable hash-pinned read.  The source
    # hash is also independently fixed above as part of the verifier contract.
    try:
        builder = verifier._load_frozen_builder(repository_root)
        verifier._require_builder_contract(builder)
    except verifier.ProfileVerificationRefusal as exc:
        raise AggregateProfileVerificationRefusal(str(exc)) from exc
    required = (
        "BuildPins",
        "PRODUCTION_PINS",
        "SOURCE_APP_PATH",
        "BIOS_PATH",
        "PACKAGED_APP_LEAF",
        "COMMON_LOCK_LEAF",
        "build_profile",
        "verify_profile",
        "_open_pinned_file",
        "_repin_held_file",
        "_close_pinned_file",
        "_validate_cue_pair",
        "ProfileRefusal",
    )
    missing = [name for name in required if not hasattr(builder, name)]
    if missing:
        raise AggregateProfileVerificationRefusal(
            f"frozen builder API changed: {','.join(missing)}."
        )
    if builder.COMMON_LOCK_LEAF != SHARED_PARENT_LOCK_RELATIVE_PATH.name:
        raise AggregateProfileVerificationRefusal(
            "frozen builder common-lock identity changed."
        )
    return builder


def _pending_output_pin_names() -> list[str]:
    return sorted(
        name
        for name, value in EXPECTED_PROFILE_OUTPUT_PINS.items()
        if value == PENDING
    )


def _validate_output_pin_set(*, allow_pending: bool) -> None:
    expected_keys = {
        "receiptSize",
        "receiptSha256",
        "receiptClosureSha256",
        "appOutputTreeEntryCount",
        "appOutputTreeManifestSha256",
        "homeOutputTreeEntryCount",
        "homeOutputTreeManifestSha256",
        "packageEnvelopeEntryCount",
        "packageEnvelopeSha256",
        "candidateManifestSha256",
    }
    if set(EXPECTED_PROFILE_OUTPUT_PINS) != expected_keys:
        raise AggregateProfileVerificationRefusal(
            "aggregate profile output-pin key closure changed."
        )
    pending = _pending_output_pin_names()
    if pending:
        if len(pending) != len(expected_keys):
            raise AggregateProfileVerificationRefusal(
                "aggregate profile output pins are partially populated; replace the "
                "complete audited set atomically."
            )
        if allow_pending:
            return
        raise AggregateProfileVerificationRefusal(
            "aggregate profile output pins are PENDING: " + ", ".join(pending) + "."
        )
    integer_names = {
        "receiptSize",
        "appOutputTreeEntryCount",
        "homeOutputTreeEntryCount",
        "packageEnvelopeEntryCount",
    }
    for name, value in EXPECTED_PROFILE_OUTPUT_PINS.items():
        if name in integer_names:
            if not isinstance(value, int) or isinstance(value, bool) or value <= 0:
                raise AggregateProfileVerificationRefusal(
                    f"aggregate profile output pin is not a positive integer: {name}."
                )
        elif not _is_sha256(value):
            raise AggregateProfileVerificationRefusal(
                f"aggregate profile output pin is not canonical SHA-256: {name}."
            )


def _ready_output_pin(name: str) -> int | str:
    _validate_output_pin_set(allow_pending=False)
    return EXPECTED_PROFILE_OUTPUT_PINS[name]


def _aggregate_build_pins(builder: types.ModuleType) -> Any:
    pins = dataclasses.replace(
        builder.PRODUCTION_PINS,
        candidate_cue_name=f"{CANDIDATE_STEM}.cue",
        candidate_cue_sha256=EXPECTED_CANDIDATE_CUE_SHA256,
        candidate_cue_size=EXPECTED_CANDIDATE_CUE_SIZE,
        candidate_bin_name=f"{CANDIDATE_STEM}.bin",
        candidate_bin_sha256=EXPECTED_CANDIDATE_BIN_SHA256,
        candidate_bin_size=EXPECTED_CANDIDATE_BIN_SIZE,
    )
    if not isinstance(pins, builder.BuildPins):
        raise AggregateProfileVerificationRefusal(
            "aggregate builder pin type changed."
        )
    return pins


def _profile_pins(
    verifier: types.ModuleType,
    builder: types.ModuleType,
) -> Any:
    _validate_output_pin_set(allow_pending=False)
    base = verifier.PRODUCTION_PINS
    build_pins = builder.PRODUCTION_PINS
    if (
        base.app_leaf != builder.PACKAGED_APP_LEAF
        or base.app_version != build_pins.app_version
        or base.executable_size != build_pins.executable_size
        or base.executable_sha256 != build_pins.executable_sha256
        or base.app_content_manifest_entry_count != build_pins.app_manifest_entry_count
        or base.app_content_manifest_sha256 != build_pins.app_manifest_sha256
        or base.settings_size != build_pins.settings_size
        or base.settings_sha256 != build_pins.settings_sha256
        or base.bios_size != build_pins.bios_size
        or base.bios_sha256 != build_pins.bios_sha256
        or base.source_app_path != builder.SOURCE_APP_PATH
        or base.source_bios_path != builder.BIOS_PATH
    ):
        raise AggregateProfileVerificationRefusal(
            "frozen verifier and builder immutable profile inputs diverged."
        )
    pins = dataclasses.replace(
        base,
        profile_id=PROFILE_ID,
        package_relative_path=PROFILE_DESTINATION_RELATIVE_PATH,
        receipt_size=_ready_output_pin("receiptSize"),
        receipt_sha256=_ready_output_pin("receiptSha256"),
        receipt_closure_sha256=_ready_output_pin("receiptClosureSha256"),
        app_output_tree_entry_count=_ready_output_pin(
            "appOutputTreeEntryCount"
        ),
        app_output_tree_manifest_sha256=_ready_output_pin(
            "appOutputTreeManifestSha256"
        ),
        home_output_tree_entry_count=_ready_output_pin(
            "homeOutputTreeEntryCount"
        ),
        home_output_tree_manifest_sha256=_ready_output_pin(
            "homeOutputTreeManifestSha256"
        ),
        package_envelope_entry_count=_ready_output_pin(
            "packageEnvelopeEntryCount"
        ),
        package_envelope_sha256=_ready_output_pin("packageEnvelopeSha256"),
        candidate_cue_relative_path=CANDIDATE_CUE_RELATIVE_PATH,
        candidate_cue_size=EXPECTED_CANDIDATE_CUE_SIZE,
        candidate_cue_sha256=EXPECTED_CANDIDATE_CUE_SHA256,
        candidate_bin_relative_path=CANDIDATE_BIN_RELATIVE_PATH,
        candidate_bin_size=EXPECTED_CANDIDATE_BIN_SIZE,
        candidate_bin_sha256=EXPECTED_CANDIDATE_BIN_SHA256,
        candidate_manifest_sha256=_ready_output_pin("candidateManifestSha256"),
    )
    if not isinstance(pins, verifier.ProfilePins):
        raise AggregateProfileVerificationRefusal(
            "aggregate verifier pin type changed."
        )
    return pins


def _aggregate_paths(
    repository_root: pathlib.Path,
) -> tuple[pathlib.Path, pathlib.Path, pathlib.Path, pathlib.Path, pathlib.Path]:
    destination = repository_root / PROFILE_DESTINATION_RELATIVE_PATH
    cue = repository_root / CANDIDATE_CUE_RELATIVE_PATH
    binary = repository_root / CANDIDATE_BIN_RELATIVE_PATH
    candidate_receipt = repository_root / CANDIDATE_RECEIPT_RELATIVE_PATH
    document = repository_root / DOCUMENT_RELATIVE_PATH
    expected_parent = repository_root / SHARED_PARENT_LOCK_RELATIVE_PATH.parent
    if destination.parent != expected_parent or destination.name != PROFILE_ID:
        raise AggregateProfileVerificationRefusal(
            "aggregate profile destination identity changed."
        )
    return destination, cue, binary, candidate_receipt, document


def _read_pinned_bytes(handle: Any, expected_size: int, label: str) -> bytes:
    os.lseek(handle.fd, 0, os.SEEK_SET)
    chunks: list[bytes] = []
    remaining = expected_size
    while remaining:
        chunk = os.read(handle.fd, min(remaining, READ_CHUNK_SIZE))
        if not chunk:
            raise AggregateProfileVerificationRefusal(
                f"{label} produced a short read."
            )
        chunks.append(chunk)
        remaining -= len(chunk)
    if os.read(handle.fd, 1):
        raise AggregateProfileVerificationRefusal(f"{label} grew while read.")
    os.lseek(handle.fd, 0, os.SEEK_SET)
    return b"".join(chunks)


def _strict_json_object(raw: bytes, label: str) -> dict[str, object]:
    try:
        text = raw.decode("utf-8")
        payload = json.loads(text)
    except (UnicodeDecodeError, json.JSONDecodeError) as exc:
        raise AggregateProfileVerificationRefusal(
            f"{label} is not strict UTF-8 JSON: {exc}."
        ) from exc
    if not isinstance(payload, dict):
        raise AggregateProfileVerificationRefusal(f"{label} root is not an object.")
    return payload


def _require_aggregate_metadata_fields(
    candidate_receipt: dict[str, object],
    document: dict[str, object],
) -> None:
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
        if candidate_receipt.get(name) != expected:
            raise AggregateProfileVerificationRefusal(
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
        if document.get(name) != expected:
            raise AggregateProfileVerificationRefusal(
                f"aggregate authoring document field changed: {name}."
            )
    nested = document.get("document")
    if (
        not isinstance(nested, dict)
        or nested.get("contentSha256") != EXPECTED_DOCUMENT_CONTENT_SHA256
    ):
        raise AggregateProfileVerificationRefusal(
            "aggregate authoring document content closure changed."
        )


def _open_aggregate_inputs(
    builder: types.ModuleType,
    repository_root: pathlib.Path,
) -> _AggregateHandles:
    _, cue_path, binary_path, receipt_path, document_path = _aggregate_paths(
        repository_root
    )
    opened: list[Any] = []
    try:
        cue = builder._open_pinned_file(
            cue_path,
            "aggregate candidate CUE",
            expected_name=f"{CANDIDATE_STEM}.cue",
            expected_size=EXPECTED_CANDIDATE_CUE_SIZE,
            expected_sha256=EXPECTED_CANDIDATE_CUE_SHA256,
        )
        opened.append(cue)
        binary = builder._open_pinned_file(
            binary_path,
            "aggregate candidate BIN",
            expected_name=f"{CANDIDATE_STEM}.bin",
            expected_size=EXPECTED_CANDIDATE_BIN_SIZE,
            expected_sha256=EXPECTED_CANDIDATE_BIN_SHA256,
        )
        opened.append(binary)
        builder._validate_cue_pair(cue, binary)
        candidate_receipt = builder._open_pinned_file(
            receipt_path,
            "aggregate candidate receipt",
            expected_name=f"{CANDIDATE_STEM}.receipt.json",
            expected_size=EXPECTED_CANDIDATE_RECEIPT_FILE_SIZE,
            expected_sha256=EXPECTED_CANDIDATE_RECEIPT_FILE_SHA256,
        )
        opened.append(candidate_receipt)
        document = builder._open_pinned_file(
            document_path,
            "aggregate authoring document",
            expected_name=DOCUMENT_RELATIVE_PATH.name,
            expected_size=EXPECTED_DOCUMENT_FILE_SIZE,
            expected_sha256=EXPECTED_DOCUMENT_ENVELOPE_SHA256,
        )
        opened.append(document)
        _require_aggregate_metadata_fields(
            _strict_json_object(
                _read_pinned_bytes(
                    candidate_receipt,
                    EXPECTED_CANDIDATE_RECEIPT_FILE_SIZE,
                    "aggregate candidate receipt",
                ),
                "aggregate candidate receipt",
            ),
            _strict_json_object(
                _read_pinned_bytes(
                    document,
                    EXPECTED_DOCUMENT_FILE_SIZE,
                    "aggregate authoring document",
                ),
                "aggregate authoring document",
            ),
        )
        handles = _AggregateHandles(cue, binary, candidate_receipt, document)
        _repin_aggregate_inputs(builder, handles)
        return handles
    except Exception:
        for handle in reversed(opened):
            builder._close_pinned_file(handle)
        raise


def _repin_aggregate_inputs(
    builder: types.ModuleType,
    handles: _AggregateHandles,
) -> None:
    for handle, label in (
        (handles.cue, "aggregate candidate CUE"),
        (handles.binary, "aggregate candidate BIN"),
        (handles.candidate_receipt, "aggregate candidate receipt"),
        (handles.document, "aggregate authoring document"),
    ):
        builder._repin_held_file(handle, label)
    builder._validate_cue_pair(handles.cue, handles.binary)


def _close_aggregate_inputs(
    builder: types.ModuleType,
    handles: _AggregateHandles | None,
) -> None:
    if handles is None:
        return
    for handle in (
        handles.document,
        handles.candidate_receipt,
        handles.binary,
        handles.cue,
    ):
        builder._close_pinned_file(handle)


def _translate_refusal(
    exc: Exception,
    verifier: types.ModuleType,
    builder: types.ModuleType,
) -> AggregateProfileVerificationRefusal:
    if isinstance(exc, AggregateProfileVerificationRefusal):
        return AggregateProfileVerificationRefusal(str(exc))
    if isinstance(exc, verifier.ProfileVerificationRefusal):
        return AggregateProfileVerificationRefusal(str(exc))
    if isinstance(exc, builder.ProfileRefusal):
        return AggregateProfileVerificationRefusal(str(exc))
    return AggregateProfileVerificationRefusal(
        f"unexpected aggregate profile verification failure: "
        f"{type(exc).__name__}: {exc}"
    )


def capture_runtime_snapshot(
    repository_root: pathlib.Path,
    *,
    phase: str,
    session_id: str,
    process_instance_id: str,
    captured_at_utc: str | None = None,
) -> dict[str, object]:
    """Capture a descriptor-relative snapshot without launching DuckStation."""
    root = _require_repository_root(repository_root)
    _validate_output_pin_set(allow_pending=False)
    verifier = _load_frozen_verifier(root)
    builder = _load_frozen_builder(verifier, root)
    pins = _profile_pins(verifier, builder)
    handles: _AggregateHandles | None = None
    try:
        handles = _open_aggregate_inputs(builder, root)
        snapshot = verifier._capture_runtime_snapshot_with_builder(
            builder,
            root,
            phase=phase,
            session_id=session_id,
            process_instance_id=process_instance_id,
            captured_at_utc=captured_at_utc,
            pins=pins,
        )
        _repin_aggregate_inputs(builder, handles)
        return snapshot
    except Exception as exc:
        raise _translate_refusal(exc, verifier, builder) from exc
    finally:
        _close_aggregate_inputs(builder, handles)


def verify_prelaunch(repository_root: pathlib.Path) -> dict[str, object]:
    """Verify the fixed profile with aggregate pins and no emulator launch."""
    root = _require_repository_root(repository_root)
    _validate_output_pin_set(allow_pending=False)
    verifier = _load_frozen_verifier(root)
    builder = _load_frozen_builder(verifier, root)
    pins = _profile_pins(verifier, builder)
    build_pins = _aggregate_build_pins(builder)
    destination, cue, binary, _, _ = _aggregate_paths(root)
    handles: _AggregateHandles | None = None
    try:
        handles = _open_aggregate_inputs(builder, root)
        receipt_before = verifier._read_receipt_only(builder, root, pins)
        result = builder.verify_profile(
            builder.SOURCE_APP_PATH,
            destination,
            cue,
            binary,
            builder.BIOS_PATH,
            build_pins,
        )
        _repin_aggregate_inputs(builder, handles)
        receipt_after = verifier._read_receipt_only(builder, root, pins)
        if receipt_after != receipt_before:
            raise AggregateProfileVerificationRefusal(
                "handoff receipt changed across aggregate builder verification."
            )
        expected_result = {
            "destination": destination,
            "receipt_sha256": pins.receipt_sha256,
            "receipt_schema": (
                f"{pins.receipt_format}@{pins.receipt_format_version}"
            ),
            "app_manifest_sha256": pins.app_content_manifest_sha256,
            "settings_sha256": pins.settings_sha256,
            "launch_gate": LAUNCH_GATE,
        }
        for name, expected in expected_result.items():
            if getattr(result, name, None) != expected:
                raise AggregateProfileVerificationRefusal(
                    f"aggregate builder verification result changed: {name}."
                )
        snapshot = verifier._capture_runtime_snapshot_with_builder(
            builder,
            root,
            phase="preflight",
            session_id="preflight",
            process_instance_id="not-applicable",
            captured_at_utc=None,
            pins=pins,
        )
        _repin_aggregate_inputs(builder, handles)
        return {
            "format": PREFLIGHT_FORMAT,
            "formatVersion": PREFLIGHT_FORMAT_VERSION,
            "status": "PREFLIGHT_READY",
            "profileId": PROFILE_ID,
            "profileDestination": os.fspath(destination),
            "handoffReceiptSha256": pins.receipt_sha256,
            "handoffReceiptClosureSha256": pins.receipt_closure_sha256,
            "appContentManifestSha256": pins.app_content_manifest_sha256,
            "appOutputTreeManifestSha256": pins.app_output_tree_manifest_sha256,
            "homeOutputTreeManifestSha256": pins.home_output_tree_manifest_sha256,
            "packageEnvelopeSha256": pins.package_envelope_sha256,
            "candidateManifestSha256": pins.candidate_manifest_sha256,
            "candidateCueSha256": EXPECTED_CANDIDATE_CUE_SHA256,
            "candidateBinSha256": EXPECTED_CANDIDATE_BIN_SHA256,
            "candidateReceiptFileSha256": (
                EXPECTED_CANDIDATE_RECEIPT_FILE_SHA256
            ),
            "candidateReceiptContentSha256": (
                EXPECTED_CANDIDATE_RECEIPT_CONTENT_SHA256
            ),
            "documentEnvelopeSha256": EXPECTED_DOCUMENT_ENVELOPE_SHA256,
            "documentCanonicalSha256": EXPECTED_DOCUMENT_CANONICAL_SHA256,
            "aggregatePlanSha256": EXPECTED_AGGREGATE_PLAN_SHA256,
            "outputRow80Sha256": EXPECTED_OUTPUT_ROW80_SHA256,
            "launchGate": LAUNCH_GATE,
            "snapshot": snapshot,
            "duckStationLaunched": False,
            "runtimeProven": False,
            "evidenceAccepted": False,
            "createBinAuthorized": False,
            "releaseAuthorized": False,
            "publishable": False,
        }
    except Exception as exc:
        raise _translate_refusal(exc, verifier, builder) from exc
    finally:
        _close_aggregate_inputs(builder, handles)


def verifier_status() -> dict[str, object]:
    pending = _pending_output_pin_names()
    return {
        "format": STATUS_FORMAT,
        "formatVersion": STATUS_FORMAT_VERSION,
        "profileId": PROFILE_ID,
        "profileDestination": os.fspath(
            PRODUCTION_REPOSITORY_ROOT / PROFILE_DESTINATION_RELATIVE_PATH
        ),
        "frozenVerifierSha256": EXPECTED_FROZEN_VERIFIER_SHA256,
        "frozenBuilderSha256": EXPECTED_FROZEN_BUILDER_SHA256,
        "candidateCueSha256": EXPECTED_CANDIDATE_CUE_SHA256,
        "candidateBinSha256": EXPECTED_CANDIDATE_BIN_SHA256,
        "candidateReceiptFileSha256": EXPECTED_CANDIDATE_RECEIPT_FILE_SHA256,
        "candidateReceiptContentSha256": (
            EXPECTED_CANDIDATE_RECEIPT_CONTENT_SHA256
        ),
        "documentEnvelopeSha256": EXPECTED_DOCUMENT_ENVELOPE_SHA256,
        "documentCanonicalSha256": EXPECTED_DOCUMENT_CANONICAL_SHA256,
        "aggregatePlanSha256": EXPECTED_AGGREGATE_PLAN_SHA256,
        "outputRow80Sha256": EXPECTED_OUTPUT_ROW80_SHA256,
        "pendingOutputPins": pending,
        "prelaunchVerificationReady": not pending,
        "launchGate": LAUNCH_GATE,
        "profileBuiltByVerifier": False,
        "duckStationLaunched": False,
        "runtimeProven": False,
        "evidenceAccepted": False,
        "createBinAuthorized": False,
        "releaseAuthorized": False,
        "publishable": False,
    }


def _self_test(repository_root: pathlib.Path) -> None:
    root = _require_repository_root(repository_root)
    _validate_output_pin_set(allow_pending=True)
    verifier = _load_frozen_verifier(root)
    builder = _load_frozen_builder(verifier, root)
    build_pins = _aggregate_build_pins(builder)
    destination, cue, binary, candidate_receipt, document = _aggregate_paths(root)
    expected_cue = (
        f'FILE "{binary.name}" BINARY\n'
        "  TRACK 01 MODE2/2352\n"
        "    INDEX 01 00:00:00\n"
    ).encode("ascii")
    if (
        destination.parent / builder.COMMON_LOCK_LEAF
        != root / SHARED_PARENT_LOCK_RELATIVE_PATH
        or cue.name != build_pins.candidate_cue_name
        or binary.name != build_pins.candidate_bin_name
        or len(expected_cue) != EXPECTED_CANDIDATE_CUE_SIZE
        or _sha256_bytes(expected_cue) != EXPECTED_CANDIDATE_CUE_SHA256
        or build_pins.candidate_bin_size != EXPECTED_CANDIDATE_BIN_SIZE
        or build_pins.candidate_bin_sha256 != EXPECTED_CANDIDATE_BIN_SHA256
        or candidate_receipt.name != f"{CANDIDATE_STEM}.receipt.json"
        or document.name != DOCUMENT_RELATIVE_PATH.name
        or hasattr(builder, "authoring_aggregate_profile_verifier")
    ):
        raise AggregateProfileVerificationRefusal(
            "aggregate profile verifier self-test failed."
        )
    if not _pending_output_pin_names():
        _profile_pins(verifier, builder)


def _build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description=(
            "Verify the no-launch DuckStation profile for the exact ID65 "
            "authoring aggregate."
        )
    )
    parser.add_argument(
        "operation",
        choices=("self-test", "status", "prelaunch", "snapshot-preflight"),
    )
    parser.add_argument(
        "--repository-root",
        required=True,
        type=pathlib.Path,
    )
    return parser


def main(argv: list[str] | None = None) -> int:
    arguments = _build_parser().parse_args(argv)
    try:
        root = _require_repository_root(arguments.repository_root)
        if arguments.operation == "self-test":
            _self_test(root)
            print("ID65_AUTHORING_AGGREGATE_PROFILE_VERIFIER_SELF_TEST_OK")
            return 0
        if arguments.operation == "status":
            _self_test(root)
            print(_canonical_json(verifier_status()))
            return 0
        if arguments.operation == "prelaunch":
            print(_canonical_json(verify_prelaunch(root)))
            return 0
        snapshot = capture_runtime_snapshot(
            root,
            phase="preflight",
            session_id="preflight",
            process_instance_id="not-applicable",
        )
        print(_canonical_json(snapshot))
        return 0
    except AggregateProfileVerificationRefusal as exc:
        print(f"REFUSED: {exc}", file=sys.stderr)
        return 2
    except Exception as exc:
        print(
            f"REFUSED: unexpected aggregate profile verifier failure: "
            f"{type(exc).__name__}: {exc}",
            file=sys.stderr,
        )
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
