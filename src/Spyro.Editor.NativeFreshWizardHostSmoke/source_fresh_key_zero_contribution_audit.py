#!/usr/bin/env python3
"""Bounded Original-only key non-award dependency inspection.

No Core, candidate image, native execution, fabricated helper returns, or output
files. Calls have explicit MAY-return continuation edges only for conservative
intra-owner CFG inspection; this is not a transitive non-award certificate.
"""
import argparse
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import struct
import sys

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[2]
READER = "src/Spyro.Editor.NativeWizardFamilyAssetSmoke/source_wizard_family_asset_oracle.py"
DECODE = "src/Spyro.Editor.NativeFreshWizardHostSmoke/source_fresh_chest_continuation_oracle.py"
PINS = {READER: "e92cfc1a4c53611cfe11ccc87931dec71ce6b4c254c34a9d6a74bf56caef2a43",
        DECODE: "4f407c80de5ae78642f94bf727dd113384ba3ad5baa62da308e9b4407a946b8a"}
EXE, TOWN = 0x8000F800, 0x8007AA38
OWNERS = {
    "exe": (None, 53875 * 2048, 0x66000, "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998"),
    "town": (15, 37 * 2048 + 0x118E800, 0xF800, "9bc923cc8d27703537b81b01f51fde351e58aabee87d38a8afc630f1031878a5"),
    "peace-data": (22, 37 * 2048 + 0x1890800, 0x29E800, "c140bd422471c66cd57bd85b29912819f798f5b080779153674714889306c9d3"),
    "pete-header": (None, 37 * 2048 + 0x7B8800, 0x800, "b9a4de193b5cf0bd9592de15c57aa5daa3713c3575026154b45402f3aceef6f1"),
    "pete-copy": (None, 37 * 2048 + 0x7B9000, 0x39C48, "1703ac37e59e480909b6f66dc856d4e5f3ce80551f18cbc8c06129c3b54dd35d"),
}
# Existing bounded owners; unpinned subwindow hashes are measured under the
# exact whole-EXE/Town identities, not claimed to be function extents.
WINDOWS = (
    ("key-complete-direct-owner", "town", 0x80083B4C, 0x274, "c33bdcc3cc509eeaf4dc1a5eb2eaf5f81791bdc1f3a8046fce6a292ba8d18bab"),
    ("XY-metric", "exe", 0x80017990, 0x60, "277c87f0a049389cf5c3e0510eac8b878a516d9f773d44545184f67910815d90"),
    ("sound-source-owner", "exe", 0x80055A78, 0x82C, "d3a63784c8dc509583c63499ccb7ad493ba8bf4af0aaeb4a6bd9fa5aa851a06d"),
    ("row-matrix-update", "exe", 0x800529E4, 0x380, "296b93ed79f3133454db7c42597103ebaa6f4c20c11c9f85e60a286f34394794"),
    ("matrix-transform", "exe", 0x80017048, 0x78, "db85bd66361e446a9d8329616f5f4faa0865f7cb439a5de41488a661a574ea78"),
    ("vector-add", "exe", 0x80017758, 0x34, "a80b10978f8b9cf51fdb19dd9d67b776c3285c209aaf023ccfbccf71913d7398"),
    ("vector-copy", "exe", 0x80017700, 0x1C, "c8e9d6ff8966aaa5fbc6e7ecbab1f9d4b379d15f22e3091881361a770fd433b7"),
    ("sparkle-slot-consumer", "exe", 0x8003AAEC, 0xD4, "b434000b758896dd223eac6654f37deabee10d0e622ea1e96d95afc3a528c2be"),
    ("sparkle-slot-allocator", "exe", 0x8005882C, 0x38, "15fe54d73a09670b02943907cf4846d691f9e714f9bdd94b5f807fcbd44f1d88"),
    ("RNG", "exe", 0x8006272C, 0x30, "8bcf455ef340cfd7846fd7937a88671fbf165aa56526545a4912547c8d6bcfd4"),
    ("particle-dispatch", "town", 0x80088F68, 0x8C, "0f9c96d1d1bd04cd777e56e00b769d81b8e378b0d34f9d6d5bbf63ef21469831"),
    ("particle0C-spawn", "town", 0x800892C0, 0x148, "c0966d0467e8b8c34566ed92d9223cc21cd4e568237d2993762fd9ce7490fce4"),
    ("particle0C-tick", "town", 0x800881E8, 0x74, "5de94cabf94c9014ae69e2241e7703d001ccb063c87951e0a4d1011b49f8b5f2"),
    ("particle-common-tail", "town", 0x80089E38, 0x94, "9470985061e29b5de9254f0555cfd8804cd5d5f4825a29398b54636511599ccc"),
    ("particle-allocator", "exe", 0x80053570, 0x98, "2eefe927534faee54464ea9af9e53a9382ff3fafa0f90d50b6a68982dce61ddb"),
    ("packed-vector", "exe", 0x80017BFC, 0x28, "ef85ebacb8d15ec69efdbdfc9b342ec6ef5007f3216e8f2867baffb9519dd312"),
    ("award-counter-control", "exe", 0x8003B854, 0x180, "333f5d194c140bcf1be0a97c556a1d39ad2efb56cf70871c6da5bba23ed6b426"),
    ("HUD-known-owner", "exe", 0x80054584, 0x1248, "e21414d29b877e50673dac71d5a5a3495dc3e0331aa2e172249360e931099843"),
    ("HUD-initialization", "exe", 0x80054600, 0x388, "7357aeb05eaf0386819f91fd681101cf6aa5f8e0f55f23124f1bda47aef85c8b"),
    ("HUD-update", "exe", 0x80054988, 0xE44, "46b5d648992f88743f84abaeeb9497f382421ff404ca631233267b67565f6307"),
    ("HUD-row-position-helper", "exe", 0x8005445C, 0x4C, "15de0552da1e04a0b72bd2eb02954a72fd3e16592399ab53790f5603ebbe2fc3"),
)


def require(ok, label):
    if not ok: raise AssertionError(label)


def sha(data): return hashlib.sha256(data).hexdigest()
def word(data, at=0): return struct.unpack_from("<I", data, at)[0]
def hx(value): return f"{value:08X}"
def identity(stat): return stat.st_dev, stat.st_ino, stat.st_size, stat.st_mtime_ns, stat.st_ctime_ns


def guard():
    for relative, digest in PINS.items(): require(sha((ROOT / relative).read_bytes()) == digest, "Pinned definition " + relative)


def load(relative, name):
    guard(); spec = importlib.util.spec_from_file_location(name, ROOT / relative)
    module = importlib.util.module_from_spec(spec); spec.loader.exec_module(module); return module


def window(owners, owner, pc, length):
    offset = pc - (EXE if owner == "exe" else TOWN)
    require(pc % 4 == length % 4 == 0 and length > 0 and 0 <= offset <= len(owners[owner]) - length, "Bounded original code owner")
    return owners[owner][offset:offset + length]


def cfg(data, start, decode, branch_outcomes=None):
    """Finite within-window overapproximation, with external transfers exposed."""
    pending, seen, delays, edges = [start], set(), set(), []
    end = start + len(data)
    while pending:
        pc = pending.pop()
        if pc in seen: continue
        require(len(seen) < 4096 and start <= pc < end and pc % 4 == 0, "Finite aligned local CFG budget")
        seen.add(pc); instruction = word(data, pc - start); edge = decode.transfer(pc, instruction)
        if edge is None:
            successors = [pc + 4]
        else:
            kind, target = edge
            require(pc + 4 < end, "Actual complete control delay owner")
            delay = word(data, pc + 4 - start)
            require(decode.transfer(pc + 4, delay) is None, "No assumed nested-delay transfer")
            delays.add(pc + 4)
            op, fn, rs = instruction >> 26, instruction & 63, instruction >> 21 & 31
            call = kind == "call" or op == 0 and fn == 9
            returns = op == 0 and fn == 8 and rs == 31
            successors = [pc + 8] if call else [pc + 8, target] if kind == "branch" else [] if returns or target is None else [target]
            if branch_outcomes and pc in branch_outcomes:
                require(kind == "branch", "Declared constant outcome is a branch")
                successors = [target if branch_outcomes[pc] else pc + 8]
            edges.append(dict(pc=hx(pc), word=hx(instruction), kind=kind, target=hx(target) if target is not None else None,
                              delayPc=hx(pc + 4), delayWord=hx(delay), callMayReturn=call, returnExecutedOrAssumed=False,
                              registerReturn=returns, unresolvedComputedEdge=target is None and not returns))
        for target in successors:
            if start <= target < end: pending.append(target)
            else: edges.append(dict(pc=hx(pc), kind="owner-exit", target=hx(target)))
    all_pcs = sorted(seen | delays)
    return dict(instructionPcs=[hx(pc) for pc in all_pcs], edges=edges, instructionCount=len(all_pcs),
                directCallTargets=sorted({edge["target"] for edge in edges if edge["kind"] == "call"}),
                stores=[decode.decoded(pc, word(data, pc - start)) for pc in all_pcs
                        if word(data, pc - start) >> 26 in (0x28, 0x29, 0x2A, 0x2B, 0x2E, 0x39, 0x3A)],
                conservativeMayReturnGraph=True, helperReturnsExecuted=False, completeRoutine=False)


def inspect(owners, decode):
    reports = []
    for name, owner, pc, length, digest in WINDOWS:
        data = window(owners, owner, pc, length)
        require(digest is None or sha(data) == digest, "Existing exact source window " + name)
        reports.append(dict(id=name, owner=owner, pc=hx(pc), byteLength=length, sha256=sha(data),
                            bytesHex=data.hex(), instructions=[decode.decoded(pc + i, word(data, i)) for i in range(0, length, 4)],
                            cfg=cfg(data, pc, decode)))
    # Literal AD constant references are context candidates, not proof that
    # they are render/update entries or all possible key-related references.
    candidates, key_globals = [], []
    for at in range(0x800, len(owners["exe"]), 4):
        instruction = word(owners["exe"], at)
        if instruction >> 26 not in (8, 9, 10, 11, 12, 13, 14) or instruction & 65535 != 0xAD: continue
        require(len(candidates) < 64, "Bounded literal AD context census")
        lo, hi = max(0x800, at - 32), min(len(owners["exe"]), at + 48)
        data = owners["exe"][lo:hi]
        candidates.append(dict(literalPc=hx(EXE + at), pc=hx(EXE + lo), byteLength=len(data), sha256=sha(data),
                               instructions=[decode.decoded(EXE + lo + i, word(data, i)) for i in range(0, len(data), 4)]))
    for owner, basis in (("exe", EXE), ("town", TOWN)):
        for at in range(0x800 if owner == "exe" else 0, len(owners[owner]), 4):
            instruction = word(owners[owner], at)
            if instruction >> 26 not in (*range(0x20, 0x27), 0x28, 0x29, 0x2B) or instruction & 65535 != 0x5830: continue
            require(len(key_globals) < 64, "Bounded key-global operand-context census")
            lo, hi = max(0, at - 48), min(len(owners[owner]), at + 64)
            data = owners[owner][lo:hi]
            key_globals.append(dict(owner=owner, operandPc=hx(basis + at), pc=hx(basis + lo), byteLength=len(data), sha256=sha(data),
                                    globalAddressProven=False,
                                    instructions=[decode.decoded(basis + lo + i, word(data, i)) for i in range(0, len(data), 4)]))
    key = owners["peace-data"][0x1CAA60 + 78 * 0x58:0x1CAA60 + 79 * 0x58]
    private = owners["peace-data"][0x1C2800 + 0xEA94:0x1C2800 + 0xEA98]
    header = owners["pete-header"]
    require((word(header, 4 + 10 * 8), word(header, 8 + 10 * 8), word(header, 4 + 11 * 8)) == (0x34A8C, 0xAD, 0x34CA8), "Actual adjacent shared key registration")
    model = owners["pete-copy"][0x34A8C - 0x800:0x34CA8 - 0x800]
    require(sha(model) == "72a5a017f8696edfc2d600b093c1b05c29ba3a423b29ad42b838a3385f5298e2", "Complete shared AD simple model")
    # Only these three outcomes are pruned, from literal ANDI-a1 tests before
    # any GTE work or modification of a1. Every later angle/GTE arm remains
    # conservative; numeric transforms and helper returns are not simulated.
    matrix = window(owners, "exe", 0x800529E4, 0x380)
    expected = {0x800529E4: 0x30A10001, 0x800529E8: 0x1020001E,
                0x80052A64: 0x30A10002, 0x80052A68: 0x10200020,
                0x80052AEC: 0x30A10004, 0x80052AF0: 0x1020009A}
    require(all(word(matrix, pc - 0x800529E4) == instruction for pc, instruction in expected.items()), "Literal matrix mode4 guards")
    mode4 = cfg(matrix, 0x800529E4, decode, {0x800529E8: True, 0x80052A68: True, 0x80052AF0: False})
    checks, refusals = source_checks(owners, reports, mode4, decode)
    return dict(marker="FRESH_KEY_ZERO_CONTRIBUTION_DEPENDENCY_INSPECTION_OK", sourceWindows=reports,
                sourceChecks=checks, refusalControls=refusals,
                exactMode4MatrixWriteGraph=mode4,
                literalAdContexts=candidates, keyGlobalOperandContexts=key_globals,
                keySourceRow=dict(sha256=sha(key), bytesHex=key.hex(), selector=key[0x53]),
                keySourcePrivate=dict(sha256=sha(private), bytesHex=private.hex()), keySharedModel=dict(byteLength=len(model), sha256=sha(model)),
                keyContribution=None, zeroInferredFromFF=False, completeKeyNonAwardProven=False,
                currentFreshExecutableRead=False, helperBodiesExecuted=False, fullLifecycleProven=False,
                runtime=False, publication=False, outputsWritten=0)


def source_checks(owners, reports, mode4, decode):
    checks, refusals = [], []
    def check(ok, label): require(ok, label); checks.append(label)
    def refuses(action, phrase):
        try: action()
        except AssertionError as error:
            check(phrase in str(error), "Exact refusal: " + phrase); refusals.append(phrase); return
        raise AssertionError("Missing expected refusal: " + phrase)
    key = reports[0]; graph = key["cfg"]
    check(key["pc"] == "80083B4C" and key["byteLength"] == 0x274 and graph["instructionCount"] == 157,
          "Exact complete key direct owner, including terminal timer-store delay; neighboring emitter excluded")
    check(graph["directCallTargets"] == ["80017048", "80017758", "80017990", "8003AAEC", "800529E4", "80055A78", "8006272C"],
          "Exactly seven ordinary direct callee entries; not transitive helper non-award proof")
    exits = {edge["target"] for edge in graph["edges"] if edge["kind"] == "owner-exit"}
    computed = [edge for edge in graph["edges"] if edge.get("unresolvedComputedEdge")]
    check(exits == {"80087258"} and len(computed) == 1 and computed[0]["pc"] == "80083C54"
          and computed[0]["word"] == "0040F809" and computed[0]["delayWord"] == "26310001",
          "All direct exits are the Town loop; one explicit particle callback retains its real delay")
    data = window(owners, "town", 0x80083B4C, 0x274)
    check(tuple(word(data, at) for at in (0, 4, 8, 12)) == (0x92620049, 0x8E720000, 0x14400DC0, 0x2664000C),
          "Nonzero key state49 skips this whole update via branch and actual delay, with no local store or helper")
    check(tuple((row["rs"], row["signedImmediate"]) for row in graph["stores"]) ==
          ((19,68),(19,69),(18,2),(1,22576),(19,87),(19,80),(19,82),(19,81),(19,28),(19,74),(19,73),(18,3),(18,3),(18,0),(18,0)),
          "Complete15 direct stores are row/private fields plus explicit key-global5830, conditional on valid owners and preserved s2/s3")
    check(all(target not in ("8003ABC0", "8003B854") for target in graph["directCallTargets"]),
          "No direct key emitter/award call; this does not close incoming or outgoing dependencies")
    by_name = {row["id"]: row for row in reports}
    for name in ("XY-metric", "matrix-transform", "vector-add", "vector-copy", "packed-vector", "sparkle-slot-allocator", "row-matrix-update", "RNG"):
        leaf = by_name[name]["cfg"]
        check(not leaf["directCallTargets"] and not any(edge["kind"] == "owner-exit" or edge.get("unresolvedComputedEdge") for edge in leaf["edges"]),
              "Bounded direct leaf graph has no external call/jump: " + name)
    check(not by_name["XY-metric"]["cfg"]["stores"] and not by_name["sparkle-slot-allocator"]["cfg"]["stores"],
          "Metric and finite sparkle-slot allocator have no native memory-store instruction")
    check([row["word"] for row in by_name["matrix-transform"]["cfg"]["stores"]] == ["ACC10000", "ACC20004", "ACC30008"],
          "Complete GTE matrix leaf writes only its supplied12-byte result; numeric GTE results are not executed")
    check(tuple(word(window(owners, "town", pc, 4)) for pc in (0x80083C34, 0x80083CB8)) == (0x24050004, 0x24050004)
          and word(window(owners, "exe", 0x8003AB1C, 4)) == 0x24050004,
          "World-key and sparkle calls supply actual a1=4, not animated collider or bucket-update bits")
    check([(row["rs"], row["signedImmediate"]) for row in mode4["stores"]] == [(15,32),(15,36),(15,40),(15,44),(15,48)]
          and word(window(owners, "exe", 0x80052AF4, 4)) == 0x208F0000 and not mode4["directCallTargets"],
          "Mode4 complete write footprint is exactly row20..33 via t7=a0, independent of unexecuted GTE numeric results")
    changed = bytearray(data); struct.pack_into("<I", changed, 0xDC, 0x0C00EE15)
    refuses(lambda: require(sha(changed) == key["sha256"], "Changed complete key owner"), "Changed complete key owner")
    refuses(lambda: cfg(data[:-4], 0x80083B4C, decode), "Actual complete control delay owner")
    refuses(lambda: window(owners, "exe", EXE + len(owners["exe"]) - 4, 8), "Bounded original code owner")
    # Independent CFG controls: skip unreachable adjacent emitter, retain a
    # real branch-delay store and both conservative branch arms; do not turn
    # a JAL into proof that its callee returned or had no effect.
    packed = lambda words: b"".join(struct.pack("<I", value) for value in words)
    fixture = cfg(packed((0x08000004, 0, 0x0C00EE15, 0, 0x03E00008, 0)), 0, decode)
    check(fixture["directCallTargets"] == [] and fixture["instructionPcs"] == ["00000000", "00000004", "00000010", "00000014"],
          "CFG negative adjacency control does not count an unreachable award call")
    fixture = cfg(packed((0x10800003, 0xAC850000, 0x0C00EE15, 0, 0x03E00008, 0)), 0, decode)
    check(fixture["directCallTargets"] == ["0003B854"] and len(fixture["stores"]) == 1,
          "CFG branch control retains both arms and a real delay-store without executing the award")
    return checks, refusals


def main(path):
    require(__debug__, "Python assertions enabled"); guard()
    raw, decode = load(READER, "key_zero_reader"), load(DECODE, "key_zero_decoder")
    with open(path, "rb") as image:
        initial = identity(os.fstat(image.fileno()))
        def same():
            require(identity(os.fstat(image.fileno())) == initial == identity(os.stat(path)), "Same held and named source identity")
        same(); raw.identity(image)
        try:
            directory = raw.logical(image, 37 * 2048, 0x800)
            require(sha(directory) == "15d3e9c45f07e27a0ddd3f3a49b23a077e2c7750021cc89c55b7b16c551186cd", "Whole Original directory")
            require(struct.unpack_from("<II", directory, 8 * 8) == (0x7B8800, 0x3A000), "Complete PETE WAD entry owner")
            owners = {}
            for name, (entry, at, length, digest) in OWNERS.items():
                if entry is not None:
                    require(struct.unpack_from("<II", directory, entry * 8) == (at - 37 * 2048, length), "Exact source directory " + name)
                data = raw.logical(image, at, length)
                require(len(data) == length and sha(data) == digest, "Complete original owner " + name); owners[name] = data
            report = inspect(owners, decode)
            require(all(sha(owners[name]) == spec[3] for name, spec in OWNERS.items()), "Complete immutable original snapshots")
            report.update(sourceImageSha256=raw.IMAGE_SHA, sourceImageByteLength=raw.IMAGE_LENGTH, helperPins=PINS,
                          sourceOwners={name: dict(logicalOffset=at, byteLength=length, sha256=digest)
                                        for name, (_, at, length, digest) in OWNERS.items()})
        finally:
            raw.identity(image); same(); guard()
    with open(path, "rb") as image:
        require(identity(os.fstat(image.fileno())) == initial, "Reopened source identity"); raw.identity(image); same()
    guard(); report.update(finalHeldSourceAuthenticated=True, finalNamedSourceAuthenticated=True)
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__); parser.add_argument("original_image")
    parser.add_argument("--inspect", action="store_true", required=True)
    main(parser.parse_args().original_image)
