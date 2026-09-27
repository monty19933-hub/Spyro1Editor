#!/usr/bin/env python3
"""Read-only independent verifier for the fixed USA startup-only experiment.

Expected bytes come from the authenticated original and eleven literal MIPS
words below, never an editor DLL, C# afterimage, receipt or candidate claim.
With only --usa, stream the expected hash without publishing any file. No path
is opened implicitly; candidate BIN and optional CUE must be named explicitly.
"""
from __future__ import annotations

import argparse
import contextlib
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import struct
import sys

sys.dont_write_bytecode = True
_spec = importlib.util.spec_from_file_location("_startup_physical_oracle", Path(__file__).with_name("Verify-SpyroJapaneseEggResearch.py"))
oracle = importlib.util.module_from_spec(_spec)
sys.modules[_spec.name] = oracle
_spec.loader.exec_module(oracle)
require, sha, logical = oracle.require, oracle.sha, oracle.logical
ReadOnlyFile, rebuild_sector = oracle.ReadOnlyFile, oracle.rebuild_sector

RAW, USER = 2352, 2048
USA_SIZE = 661547040
USA_SHA = "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37"
EXE_SHA = "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998"
EXE_LBA, EXE_SIZE = 53875, 0x66000
WAD_LBA, WAD_SIZE = 37, 0x6927000
IMAGE_NAME = "collectible-startup-bootstrap-reservation-r01.bin"
PROFILE = "collectible-startup-bootstrap-reservation-research-v1"
TOUCHED_LBAS = (53875, 54078)
STUB_WORDS = (0x3C02801F, 0x3442F600, 0x3C03801F, 0x3463F800, 0xAC400000,
              0x24420004, 0x0043082B, 0x1420FFFC, 0, 0x08016E38, 0)
EDIT_EXTENTS = ((0x10, 4), (0x65DA4, 4), (0x65E40, 0x2C))
PINS = (
    (0, 0x80, "347f9647eac84734506473cda2d4c3d5e175e517b97a78fa1343e26079333d46"),
    (0x65E40, 0x1C0, "5c55c8f4db4010ba9203d83536d0609856af8c847ac039e37e7dde8fbd574b61"),
    (0x4C0E0, 0x24, "7896dd616818db767dc8a988879948e6011c1b4c1600aff3762ec7dc645c1f48"),
)


def fixed_executable(original):
    """Pure fixed recipe; synthetic tests confer no exact-source authority."""
    require(isinstance(original, bytes) and len(original) == EXE_SIZE and original[:8] == b"PS-X EXE",
            "Requires immutable complete PS-X EXE geometry")
    fields = ((0x10, 0x8005B8E0), (0x14, 0), (0x18, 0x80010000), (0x1C, 0x65800),
              (0x20, 0), (0x24, 0), (0x28, 0), (0x2C, 0), (0x30, 0x801FFFF0), (0x34, 0), (0x65DA4, 0x800))
    require(all(struct.unpack_from("<I", original, at)[0] == value for at, value in fields),
            "Original startup/reserve/header fields differ")
    require(not any(original[0x65E40:]), "Complete loaded-and-cleared original tail is not zero")
    result = bytearray(original)
    struct.pack_into("<I", result, 0x10, 0x80075640)
    struct.pack_into("<I", result, 0x65DA4, 0xA00)
    struct.pack_into("<11I", result, 0x65E40, *STUB_WORDS)
    inverse = bytearray(result)
    for at, length in EDIT_EXTENTS:
        inverse[at:at + length] = original[at:at + length]
    require(inverse == original, "Fixed EXE complement/inverse failed")
    return bytes(result)


def executable_sector(before, lba, original, after):
    require(type(lba) is int and lba in TOUCHED_LBAS and isinstance(before, bytes) and len(before) == RAW,
            "Only the two fixed complete raw sectors are owned")
    require(isinstance(after, bytes) and after == fixed_executable(original), "Unexpected executable afterimage")
    offset = (lba - EXE_LBA) * USER
    require(before[24:2072] == original[offset:offset + USER], "Original raw payload differs from its EXE owner")
    require(rebuild_sector(before, lba) == before, "Original touched-sector EDC/ECC differs")
    result = bytearray(before)
    result[24:2072] = after[offset:offset + USER]
    result = rebuild_sector(result, lba)
    require(result[:24] == before[:24], "Raw header or duplicated subheader changed")
    inverse = bytearray(result)
    for at, length in EDIT_EXTENTS:
        if offset <= at < offset + USER:
            require(at + length <= offset + USER, "An edit unexpectedly crosses sectors")
            inverse[24 + at - offset:24 + at - offset + length] = original[at:at + length]
    require(rebuild_sector(inverse, lba) == before, "Fixed raw-sector field inverse differs")
    return result


def verify_raw_complement(source, candidate, replacements, source_digest):
    """Neutral streaming comparison, also tested on small in-memory images."""
    length = source.initial.st_size
    require(type(length) is int and length > 0 and length % RAW == 0, "Source is not complete raw sectors")
    require(isinstance(replacements, dict) and bool(replacements), "Explicit raw replacements required")
    for lba, raw in replacements.items():
        require(type(lba) is int and 0 <= lba < length // RAW and isinstance(raw, bytes) and len(raw) == RAW,
                "Replacement is outside its raw owner")
        require(rebuild_sector(raw, lba) == raw, "Expected replacement parity differs")
    if candidate is not None:
        require(candidate.initial.st_size == length, "Candidate whole-image extent differs")
    expected_hash, inverse_hash, original_hash = hashlib.sha256(), hashlib.sha256(), hashlib.sha256()
    changed, changed_bytes = [], 0
    for offset in range(0, length, RAW * 128):
        before = source.read(offset, min(RAW * 128, length - offset))
        original_hash.update(before)
        after = bytearray(before)
        owned = [(lba, raw) for lba, raw in replacements.items() if offset <= lba * RAW < offset + len(before)]
        for lba, raw in owned:
            within = lba * RAW - offset
            old = before[within:within + RAW]
            require(rebuild_sector(old, lba) == old and raw[:24] == old[:24], "Original parity or raw header complement differs")
            require(raw != old, "A declared touched sector is unchanged")
            after[within:within + RAW] = raw
            changed.append(lba)
            changed_bytes += sum(a != b for a, b in zip(old, raw))
        expected_hash.update(after)
        actual = after if candidate is None else candidate.read(offset, len(before))
        require(actual == after, f"Candidate raw complement differs at chunk LBA {offset // RAW}")
        inverse = bytearray(actual)
        for lba, _ in owned:
            within = lba * RAW - offset
            inverse[within:within + RAW] = before[within:within + RAW]
        inverse_hash.update(inverse)
    require(original_hash.hexdigest() == source_digest == inverse_hash.hexdigest(), "Complete original/inverse identity differs")
    return {"expectedCandidateSha256": expected_hash.hexdigest(), "inverseOriginalSha256": inverse_hash.hexdigest(),
            "changedLbas": sorted(changed), "changedRawBytes": changed_bytes,
            "candidateVerified": candidate is not None, "completeRawComplementVerified": True,
            "completeOriginalInverseVerified": True}


def require_cue(data):
    require(isinstance(data, bytes) and 0 < len(data) <= 65536, "Invalid bounded CUE")
    try:
        lines = [line.strip() for line in data.decode("utf-8-sig").splitlines() if line.strip()]
    except UnicodeDecodeError as error:
        raise ValueError("CUE is not UTF-8") from error
    match = re.fullmatch(r'FILE\s+"([^"\r\n]+)"\s+BINARY', lines[0], re.IGNORECASE) if lines else None
    require(len(lines) == 3 and match is not None and match.group(1) == IMAGE_NAME and
            lines[1].upper() == "TRACK 01 MODE2/2352" and lines[2].upper() == "INDEX 01 00:00:00",
            "CUE does not bind the exact single-track candidate name/geometry")


def logical_digest(image, lba, length):
    require(length > 0 and length % USER == 0, "Whole logical sector owner required")
    digest = hashlib.sha256()
    for offset in range(0, length, USER * 128):
        size = min(USER * 128, length - offset)
        raw = image.read((lba + offset // USER) * RAW, (size // USER) * RAW)
        for within in range(0, len(raw), RAW):
            digest.update(raw[within + 24:within + 2072])
    return digest.hexdigest()


def verify(usa, candidate=None, cue=None):
    require(usa is not None, "An explicit original USA BIN path is required")
    require(cue is None or candidate is not None, "An optional CUE requires an explicit candidate BIN")
    if candidate is not None:
        require(Path(candidate).name == IMAGE_NAME, "Candidate filename is outside this fixed experiment")
    if cue is not None:
        require(Path(os.path.abspath(cue)).parent == Path(os.path.abspath(candidate)).parent,
                "CUE and candidate must share their explicit directory")
    resolved, alias_stable = oracle.source_alias(usa)
    with contextlib.ExitStack() as stack:
        source = stack.enter_context(ReadOnlyFile(resolved, USA_SIZE))
        require(source.initial.st_size == USA_SIZE and source.digest() == USA_SHA, "Requires the complete exact original USA image")
        root_lba, root, _ = oracle.iso_source(source, (WAD_LBA, WAD_SIZE), (EXE_LBA, EXE_SIZE), "SCUS_942.28")
        original = logical(source, EXE_LBA, EXE_SIZE, 0, EXE_SIZE)
        require(sha(original) == EXE_SHA, "Complete original executable identity differs")
        require(all(sha(original[at:at + size]) == digest for at, size, digest in PINS), "Original bootstrap source pin differs")
        after = fixed_executable(original)
        replacements = {lba: executable_sector(source.read(lba * RAW, RAW), lba, original, after) for lba in TOUCHED_LBAS}
        output = stack.enter_context(ReadOnlyFile(candidate, USA_SIZE)) if candidate is not None else None
        result = verify_raw_complement(source, output, replacements, USA_SHA)
        require(tuple(result["changedLbas"]) == TOUCHED_LBAS, "Fixed two-sector change set differs")
        wad_hash = logical_digest(source, WAD_LBA, WAD_SIZE)
        if output is not None:
            candidate_root_lba, candidate_root, _ = oracle.iso_source(output, (WAD_LBA, WAD_SIZE), (EXE_LBA, EXE_SIZE), "SCUS_942.28")
            require(candidate_root_lba == root_lba and candidate_root == root and
                    logical(output, 16, USER, 0, USER) == logical(source, 16, USER, 0, USER), "ISO root/PVD changed")
            require(logical(output, EXE_LBA, EXE_SIZE, 0, EXE_SIZE) == after, "Candidate complete executable differs")
            require(logical_digest(output, WAD_LBA, WAD_SIZE) == wad_hash, "Complete logical WAD changed")
        cue_hash = None
        if cue is not None:
            cue_file = stack.enter_context(ReadOnlyFile(cue, 65536))
            cue_bytes = cue_file.read(0, cue_file.initial.st_size)
            require_cue(cue_bytes)
            cue_hash = sha(cue_bytes)
            cue_file.stable(cue_hash)
        if output is not None:
            output.stable(result["expectedCandidateSha256"])
        source.stable(USA_SHA)
        alias_stable()
        return dict(result, schemaVersion=1, profile=PROFILE,
                    mode="independent-candidate-verification" if output is not None else "original-only-prediction",
                    sourceUsaSha256=USA_SHA, originalExecutableSha256=EXE_SHA,
                    expectedExecutableSha256=sha(after), wadSha256=wad_hash, rootSha256=sha(root),
                    unchangedWadAndIso=True, cueVerified=cue is not None, cueSha256=cue_hash,
                    originalAndNamedInputsStable=True, editExtents=[list(item) for item in EDIT_EXTENTS],
                    originalPinsVerified=len(PINS), candidateImageName=IMAGE_NAME,
                    runtimeVerified=False, biosLoaderVerified=False, nativeResidenceIssued=False,
                    lifetimeProven=False, readyAdmissionAuthorized=False, saveVerified=False,
                    cardIoApproved=False, normalExportApproved=False, publicationApproved=False,
                    imageWrites=0, cardWrites=0, hostileFilesystemRaceSafe=False)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--usa", required=True, help="Exact original USA BIN, explicitly named")
    parser.add_argument("--candidate", help="Optional explicit fixed-name candidate BIN; omission predicts only")
    parser.add_argument("--cue", help="Optional explicit candidate CUE; requires --candidate")
    args = parser.parse_args()
    print(json.dumps(verify(args.usa, args.candidate, args.cue), indent=2))


if __name__ == "__main__":
    main()
