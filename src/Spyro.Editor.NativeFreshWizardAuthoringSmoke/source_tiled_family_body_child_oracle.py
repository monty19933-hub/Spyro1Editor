#!/usr/bin/env python3
"""Independent complete tiled-family Body reference from the verified R07 child.

Reads only the explicit exact child BIN/CUE and pinned Python definitions. The
Body is an in-memory expected byte array, not a typed source/Body issuer. No
Core output, Original image, native execution, payload or image writes occur.
"""
import contextlib
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import struct
import sys

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[2]
CHILD = "src/Spyro.Editor.NativeFreshWizardAuthoringSmoke/source_tiled_family_data_child_oracle.py"
NEUTRAL = "src/Spyro.Editor.NativeFreshWizardAuthoringSmoke/source_tiled_family_surface_oracle.py"
READER = "src/Spyro.Editor.NativeId65FreshWizardBodySmoke/source_fresh_chest_spawn_collider_diagnostic.py"
HELPERS = {
    CHILD: "4f0ddbd810ae40ee10bd96c58d92fd4c46e8af1e094a6c309bc43c6e5e6d7ede",
    NEUTRAL: "e3bfb4fa6861fc2230b8b3ba22cc78231bf03808b7cd9575e15874f23e068a7f",
    READER: "a66004ad9fd42b5c20580adb3d8be2783f6e65b65f32e5bcaf94a47e6c057b3b",
    "src/Spyro.Editor.NativeId65FreshDocumentSmoke/source_tiled_surface_oracle.py":
        "4aa6bc207bed327c8fb2f9832b8bd1bc098b26b0f50a4817ba55183373be0ec3",
}
OLD_BODY_SHA = "2d8c1bf1af35c8e5522b239c564d36c79c828539d133278c461b6178b53bb504"
USED_SHA = "fe754f8e5e4686fdfe03d7e516dab8f1b52ad605b1541615e98d3f0511e4835e"
PADDED_SHA = "83baa79d7b1f9f27eadecb721f1026b0d0aea57cc65f0758b5353eaa0fc36fb6"
# Frozen from the first independent child-only measurement, before Core Body
# implementation. --derive is reporting only and cannot bypass these pins.
PINS = {
    "bodyByteLength": 0x20F000,
    "oldBodySha256": OLD_BODY_SHA,
    "newBodySha256": "c8c85e71b633e29ce573dddccb04a4a0adcd6696d292658304328e435dfaa9ca",
    "headerSha256": "79a8467e3c33d6924bde2253e2f165cf8423cc1d719730c09aa01dbe042a467f",
    "retainedComplementSha256": "1216590804168647145c2ee8ddbe8a1c38c2ec80dc86363b2f7edf0b40146a6a",
    "dataBodyOffset": 0xE6000, "dataAllocationByteLength": 0x1000,
    "newUsedByteLength": 0xCB8, "newUsedSha256": USED_SHA, "newPaddingByteLength": 0x348,
    "physicalGrowth": 0, "changedByteCount": 1413,
    "firstChangedBodyOffset": 942456, "lastChangedBodyOffset": 945327,
    "subfileSha256": [
        "6e20736d79a86035119d264af1f8acb307f6a08e573295afcd89097b4330e014",
        PADDED_SHA,
        "b3fdc6639588149657de506300930e5f96cb69dc3b02a4362fac52a7c3f764bf",
        "4f560dcbfa2d997a3f02554cf297d95c1778f67fc5d8fcf8ff47f64d609a89e8",
        "bdf27988f3d2b0fd4c4b905636b6980aff49ab1475d85edb4ce8aa2cc469351f",
        "23968146e46b3263997e16692a192bbdb049966b010673d17611f2ab6903cef0",
        "402bb40e21bf84922cbc35312b0e49b95292a2f96326636aba05685443686991",
        "eabadb4ea663043665bc4ba2d2e00c1c7c104688906b56bac2d7aba7c6cae8da",
    ],
    "completeHeaderUnchanged": True, "completeNonDataComplementUnchanged": True,
    "completePlacedSceneUnchanged": True, "completeImmediateBodyInverse": True,
    "negativeControls": 47,
}


def require(ok, message):
    if not ok:
        raise AssertionError(message)


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def definitions(before):
    require(Path(__file__).read_bytes() == before, "Complete own definition unchanged")
    for relative, expected in HELPERS.items():
        require(sha((ROOT / relative).read_bytes()) == expected, "Pinned complete helper: " + relative)


def load(relative, name):
    path = ROOT / relative
    require(sha(path.read_bytes()) == HELPERS[relative], "Helper pinned immediately before import")
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def descriptors(body):
    require(type(body) is bytes and len(body) == 0x20F000, "Complete exact Body extent")
    result, cursor, ended = [], 0x800, False
    for index in range(10):
        at, length = struct.unpack_from("<II", body, index * 8)
        if (at, length) == (0, 0):
            ended = True
            continue
        require(not ended and at == cursor and length > 0 and length % 0x800 == 0 and
                at <= len(body) - length, "All packed physical descriptors partition the actual Body")
        result.append((at, length))
        cursor += length
    require(len(result) == 8 and cursor == len(body) and body[0x1D0:0x800] == bytes(0x630),
            "Complete eight-subfile Body and reserved header complement")
    require(result[:4] == [(0x800, 0xE5800), (0xE6000, 0x1000), (0xE7000, 0x1B000),
                           (0x102000, 0x3800)], "Current chest upload/data/model/P9 Scene geometry")
    return result


def assemble(prior, used):
    require(type(prior) is bytes and sha(prior) == OLD_BODY_SHA, "Actual complete retained child Body preimage")
    parts = descriptors(prior)
    require(type(used) is bytes and len(used) == 0xCB8 and sha(used) == USED_SHA,
            "Complete independently frozen CB8 used data")
    padded = used + bytes(0x348)
    require(len(padded) == 0x1000 and sha(padded) == PADDED_SHA, "Exact complete replacement allocation")
    at, length = parts[1]
    result = prior[:at] + padded + prior[at + length:]
    require(descriptors(result) == parts and result[:0x800] == prior[:0x800] and
            result[:at] == prior[:at] and result[at + length:] == prior[at + length:],
            "Unchanged descriptors/header and every byte outside complete data allocation")
    for index, (start, size) in enumerate(parts):
        if index != 1:
            require(result[start:start + size] == prior[start:start + size], "Complete retained physical owner")
    return result


def inverse(supplied, prior, used, child):
    expected = assemble(prior, used)
    require(type(supplied) is bytes and supplied == expected, "Exact entire new Body inverse preimage")
    at, length = descriptors(prior)[1]
    allocation = supplied[at:at + length]
    require(allocation[0xCB8:] == bytes(0x348), "Complete new data-allocation padding")
    old_allocation = child.inverse(allocation[:0xCB8], used, prior[at:at + length])
    restored = supplied[:at] + old_allocation + supplied[at + length:]
    require(restored == prior and sha(restored) == OLD_BODY_SHA, "Complete immediate child-relative Body inverse")
    return restored


def reference(prior, used, child):
    result = assemble(prior, used)
    parts = descriptors(prior)
    require(inverse(result, prior, used, child) == prior and inverse(result, prior, used, child) == prior,
            "Repeat complete immediate inverse")
    at, size = parts[1]
    complement = prior[:at] + prior[at + size:]
    require(result[:at] + result[at + size:] == complement, "Full retained complement")
    refusals = []

    def reject(label, action):
        try:
            action()
        except AssertionError:
            refusals.append(label)
            return
        raise AssertionError("Negative control accepted: " + label)

    # Header descriptors, root/class fields, each complete subfile's endpoints,
    # material/surface/tail boundaries, and both ends of the new zero padding.
    probes = {0, 12, 0x50, 0x150, 0x1D0, 0x7FF}
    for start, length in parts:
        probes.update((start, start + length - 1))
    probes.update(at + relative for relative in (0x177, 0x178, 0x507, 0x508, 0x50C,
                                                0x598, 0x69C, 0xCB7, 0xCB8, 0xFFF))
    for offset in sorted(probes):
        corrupt = bytearray(result)
        corrupt[offset] ^= 1
        reject("changed-or-complement-" + hex(offset),
               lambda corrupt=corrupt: inverse(bytes(corrupt), prior, used, child))
    for bad in (result[:-1], result + bytes(1), result[:-0x800], result + bytes(0x800), prior):
        reject("truncated-appended-or-old-body", lambda bad=bad: inverse(bad, prior, used, child))
    for offset in (0, at, at + 0xA6C, parts[2][0], parts[3][0], len(prior) - 1):
        foreign = bytearray(prior)
        foreign[offset] ^= 1
        reject("foreign-child-" + hex(offset), lambda foreign=foreign: assemble(bytes(foreign), used))
    reject("short-prior", lambda: assemble(prior[:-1], used))
    reject("appended-prior", lambda: assemble(prior + bytes(1), used))
    reject("old-data-as-new", lambda: assemble(prior, prior[at:at + 0xCB8]))
    reject("padded-data-as-used", lambda: assemble(prior, used + bytes(0x348)))
    reject("short-used", lambda: assemble(prior, used[:-1]))
    changed = [i for i, (old, new) in enumerate(zip(prior, result)) if old != new]
    require(changed and all(at <= offset < at + size for offset in changed), "Only data allocation changed")
    return dict(bodyByteLength=len(result), oldBodySha256=sha(prior), newBodySha256=sha(result),
                headerSha256=sha(result[:0x800]), retainedComplementSha256=sha(complement),
                dataBodyOffset=at, dataAllocationByteLength=size, newUsedByteLength=len(used),
                newUsedSha256=sha(used), newPaddingByteLength=0x348,
                physicalGrowth=0, changedByteCount=len(changed), firstChangedBodyOffset=changed[0],
                lastChangedBodyOffset=changed[-1],
                subfileSha256=[sha(result[start:start + length]) for start, length in parts],
                subfiles=[dict(index=i, bodyOffset=start, byteLength=length,
                               oldSha256=sha(prior[start:start + length]),
                               newSha256=sha(result[start:start + length]), unchanged=i != 1)
                          for i, (start, length) in enumerate(parts)],
                completeHeaderUnchanged=True, completeNonDataComplementUnchanged=True,
                completePlacedSceneUnchanged=True, completeImmediateBodyInverse=True,
                negativeControls=len(refusals))


def main(argv=None):
    args = list(sys.argv[1:] if argv is None else argv)
    require(len(args) == 3 and args[0] in ("--derive", "--child-reference"),
            "Only --derive/--child-reference and explicit absolute R07 BIN/CUE; no output paths")
    mode, bin_name, cue_name = args
    bin_path, cue_path = Path(bin_name), Path(cue_name)
    require(bin_path.is_absolute() and cue_path.is_absolute() and bin_path.parent == cue_path.parent and
            bin_path.name == "native-fresh-saved-level-r01.bin" and cue_path.name == "native-fresh-saved-level-r01.cue",
            "Explicit paired R07 names; no discovery, Original source or output")
    before = Path(__file__).read_bytes()
    definitions(before)
    child, neutral, reader = (load(path, "body_child_" + label) for path, label in
                              ((CHILD, "data"), (NEUTRAL, "neutral"), (READER, "reader")))
    require(child.HELPERS == {k: v for k, v in HELPERS.items() if k != CHILD}, "Complete transitive helper inventory")
    neutral.preserved_control()
    neutral.validate_document(neutral.CANONICAL)
    require(sha(neutral.CANONICAL) == "2bc9a53da03fdc5d5c8bfe6bc11b1c46fbbdd1db7ae089c1df8a664c20e5b375",
            "Exact complete neutral document, not a Core serialization")
    held, failures, report = [], [], None
    with contextlib.ExitStack() as stack:
        try:
            for path, length, digest in ((bin_path, child.IMAGE_LENGTH, child.IMAGE_SHA),
                                         (cue_path, len(child.CUE_BYTES), sha(child.CUE_BYTES))):
                stream = stack.enter_context(path.open("rb"))
                initial = child.identity(os.fstat(stream.fileno()))
                held.append((stream, path, initial, length, digest))
                child.authenticate(stream, path, initial, length, digest)
            allocated, extraction = child.extract(held[0][0], reader)
            data_report = child.reference(allocated, neutral)
            require(all(data_report[key] == value for key, value in child.PINS.items()), "All frozen child-data pins")
            used, _, _ = child.compose(allocated, neutral)
            prior = reader.logical(held[0][0], extraction["wadLba"] * 2048 + extraction["bodyWadOffset"],
                                   extraction["bodyByteLength"])
            require(prior[0xE6000:0xE7000] == allocated, "Same complete extracted data owner")
            report = reference(prior, used, child)
            require(PINS and all(report[key] == value for key, value in PINS.items()), "Frozen independent Body pins")
        except BaseException as error:
            failures.append(error)
        finally:
            for stream, path, initial, length, digest in held:
                for reopened in (False, True):
                    try:
                        if reopened:
                            with path.open("rb") as current:
                                child.authenticate(current, path, initial, length, digest)
                        else:
                            child.authenticate(stream, path, initial, length, digest)
                    except BaseException as error:
                        failures.append(error)
            try:
                definitions(before)
            except BaseException as error:
                failures.append(error)
            if failures:
                raise BaseExceptionGroup("Body reference and complete child/CUE/definition guards", failures)
    print(json.dumps(dict(evidenceKind="child-relative-complete-body-byte-reference", oracleSha256=sha(before),
        helpers=HELPERS, inputBinSha256=child.IMAGE_SHA, inputCueSha256=sha(child.CUE_BYTES),
        canonicalDocumentSha256=sha(neutral.CANONICAL), extraction=extraction, reference=report,
        completeBeforeAfterReopenedInputGuards=True, originalOpened=False, coreOutputsRead=False,
        sourceCertified=False, bodyIssued=False, domainIssued=False, executableIssued=False, discIssued=False,
        nativeLoaderExecuted=False, modelOrSceneLoaderExecuted=False, runtimeVerified=False,
        publicationApproved=False, completeNativeDependencyClosureProven=False,
        scope="In-memory byte reference and immediate inverse only. Native storage graph, loader/RAM, EXE, disc and runtime proofs remain separate."), indent=2))
    print("TILED_FAMILY_BODY_CHILD_REFERENCE_OK")


if __name__ == "__main__":
    main()
