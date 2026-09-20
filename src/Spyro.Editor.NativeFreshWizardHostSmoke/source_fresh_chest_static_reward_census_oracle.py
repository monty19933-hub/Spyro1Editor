#!/usr/bin/env python3
"""Original-only reward-value witness and bounded P9 census-premise inspector.

The native value chain and original Town class dispatch execute in owned
synthetic RAM. Award, RNG, actor updates, pickup helpers and current Fresh Host
do not execute. Key lookahead is inspection, NOT a complete handler certificate.
The known deterministic selector subtotal is not a certified whole-P9 census.
No Core, producer/candidate input, helper-return substitute or output file.
"""
import argparse
import hashlib
import importlib.util
import json
import struct
import sys
from pathlib import Path

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[2]
HELPERS = {
    "scalar": ("src/Spyro.Editor.NativeMobyPersistentOwnerSmoke/source_owner_lifecycle_oracle.py",
               "4374150efed3d518ae5a6ae0aeae6b8fe05e03b940961049a480759c9f958429"),
    "rows": ("src/Spyro.Editor.NativeFreshWizardHostSmoke/source_fresh_chest_reset_preconditions_oracle.py",
             "8fda30564e5d1ad8f8262a77375dc6bbdc0b846be137ce842e5b90b4d4def6c8"),
    "decode": ("src/Spyro.Editor.NativeFreshWizardHostSmoke/source_fresh_chest_continuation_oracle.py",
               "4f407c80de5ae78642f94bf727dd113384ba3ad5baa62da308e9b4407a946b8a"),
}
OWNERS = {
    "exe": (None, 53875 * 2048, 0x66000,
            "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998"),
    "town": (15, 37 * 2048 + 0x118E800, 0xF800,
             "9bc923cc8d27703537b81b01f51fde351e58aabee87d38a8afc630f1031878a5"),
    "peace-data": (22, 37 * 2048 + 0x1890800, 0x29E800,
                   "c140bd422471c66cd57bd85b29912819f798f5b080779153674714889306c9d3"),
    "wizard-data": (40, 37 * 2048 + 0x2F6B000, 0x28A000,
                    "9973493f865b5e86bc17b32c5af9e3d7596e25615618c007ed28e0379161f2d8"),
    "grass-data": (10, 37 * 2048 + 0x800800, 0x383000,
                   "86cde4f886d97821a3a4b869100b586842dbd4b9fb696558bead274bb547af4f"),
}
EXE, OVERLAY, ACTOR = 0x8000F800, 0x8007AA38, 0x80140000
VALUE, AWARD, EMITTER, RNG = 0x8003BA64, 0x8003B854, 0x8003ABC0, 0x8006272C
DISPATCH, KEY, LOOP = 0x8007DB34, 0x80083B4C, 0x80087258
PINS = (
    ("value-dispatch", "exe", VALUE, 0x58,
     "5a7677129481343250dfaa32337c82b805d64902f154ea5c0af991a460947e48"),
    ("emitter", "exe", EMITTER, 0x51C,
     "979e567dd4bebbdd9cadaf8f884e1110be16f293c6590ba6214941be5be7a4a8"),
    ("original-Town-class-chain", "town", DISPATCH, 0x264,
     "0ffb33d9b0b797e469fb2a1003c1736e28ee380b42738f40f127d8e1a5f00bf4"),
    ("key-pickup-prefix-only", "town", KEY, 0x160,
     "bac4b44c8aaa1e0e382fbd603531810f84c9c916026e655a7a05ff581ea314d4"),
)


def sha(data): return hashlib.sha256(data).hexdigest()
def hx(value): return f"{value:08X}"
def word(data, at=0): return struct.unpack_from("<I", data, at)[0]
def half(data, at): return struct.unpack_from("<H", data, at)[0]


def require(ok, label):
    if not ok: raise AssertionError(label)


def bounded(data, at, length):
    require(at >= 0 and length > 0 and at <= len(data) - length, "Bounded complete original extent")
    return bytes(data[at:at + length])


def guards():
    for relative, digest in HELPERS.values():
        require(sha((ROOT / relative).read_bytes()) == digest, "Frozen helper changed: " + relative)


def load(name):
    relative, _ = HELPERS[name]
    spec = importlib.util.spec_from_file_location("static_census_" + name, ROOT / relative)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)  # Definition-only: never invoke helper main/capture.
    return module


def window(owners, owner, pc, length):
    return bounded(owners[owner], pc - (EXE if owner == "exe" else OVERLAY), length)


class TraceCode(dict):
    def __init__(self, code):
        super().__init__(code)
        self.executed = []

    def __getitem__(self, pc):
        self.executed.append(pc)
        return super().__getitem__(pc)


def trace(machine, entry, stop):
    pcs = machine.code.executed
    require(len(pcs) == machine.instructions and pcs, "Complete bounded scalar fetch trace")
    return {"entry": hx(entry), "stop": hx(stop), "instructionCount": len(pcs),
            "pcs": [hx(pc) for pc in pcs],
            "traceSha256": sha(b"".join(struct.pack("<II", pc, dict.__getitem__(machine.code, pc)) for pc in pcs)),
            "reads": [{"address": hx(at), "byteLength": size} for at, size in machine.reads],
            "writes": [{"address": hx(at), "byteLength": size, "value": hx(value)}
                       for at, size, value in machine.writes],
            "calls": [{"pc": hx(pc), "target": hx(target)} for pc, target in machine.calls]}


def raw_rows(owners, retained):
    family = retained.family_rows(owners["peace-data"])
    rows = []

    def append(role, owner, index, row_at, private_at, length, row_hash=None, private_hash=None):
        row = bounded(owners[owner], row_at, 0x58)
        private = bounded(owners[owner], private_at, length)
        require(row_hash is None or sha(row) == row_hash, "Exact original " + role + " row")
        require(private_hash is None or sha(private) == private_hash, "Exact original " + role + " properties")
        rows.append((row, {"role": role, "sourceOwner": owner, "sourceTrueIndex": index,
                           "rowOwnerOffset": hx(row_at), "rowLogicalOffset": hx(OWNERS[owner][1] + row_at),
                           "rowByteLength": len(row), "rowSha256": sha(row),
                           "selectorOwnerOffset": hx(row_at + 0x53), "selectorByteLength": 1,
                           "classOwnerOffset": hx(row_at + 0x36), "classByteLength": 2,
                           "privateOwnerOffset": hx(private_at), "privateByteLength": length,
                           "privateSha256": sha(private), "nativeClass": hx(half(row, 0x36)),
                           "kind3A": row[0x3A], "selector53": row[0x53]}))

    def source_row_at(data, index):
        scene_at, scene_bytes = word(data, 0x18), word(data, 0x1C)
        scene, cursor = bounded(data, scene_at, scene_bytes), 0x88
        for _ in range(7):
            size = word(bounded(scene, cursor, 8))
            require(size >= 8 and size % 4 == 0, "Native preceding scene-list grammar")
            bounded(scene, cursor, size); cursor += size
        size, count = struct.unpack_from("<II", bounded(scene, cursor, 8))
        require(0 <= index < count and 8 + count * 0x58 <= size, "Actual source placed row count/extent")
        bounded(scene, cursor, size)
        return scene_at + cursor + 8 + index * 0x58

    # The source WAD40/level33/T6 and WAD10/T121 layouts precede every Fresh producer. No
    # authored coordinates, scene placement or output bytes are read here.
    wizard = owners["wizard-data"]
    require((word(wizard, 0x18), word(wizard, 0x1C)) == (0x1CE800, 0x11800), "Wizard original scene descriptor")
    require(source_row_at(wizard, 6) == 0x1D4B88, "Actual source Wizard level33/T6 row-table walk")
    append("wizard", "wizard-data", 6, 0x1D4B88, 0x1DAB50, 0x50,
           "59401eafeab453fd9d26f14eedca58390653dc5f4fcc3d2560f7b3e8fc7afa82",
           "5b3d762f6117ea30e959a21bf2778f3442b03eba93125c9a0e0a642cc58d345b")
    grass = owners["grass-data"]
    require((word(grass, 0x18), word(grass, 0x1C)) == (0x1C9800, 0x14800), "Grass original scene descriptor")
    require(0xA2AC + 121 * 0x58 == 0xCC44, "Source Grass row-table arithmetic")
    require(source_row_at(grass, 121) == 0x1C9800 + 0xCC44, "Actual source Grass T121 row-table walk")
    append("grass", "grass-data", 121, 0x1C9800 + 0xCC44, 0x1C9800 + 0x11ACC, 8,
           "de8450049c0bea92fba8fe4e7f9f9cb749a514d1318dbd83dc9b6b1b18a0b190",
           "9eeeff662fd5b77dbc35de8ed01e0d1fd149cee49126625b69f65553c4b7c20b")
    for item in family["rows"]:
        append(item["role"], "peace-data", item["trueIndex"], int(item["rowDataOffset"], 16),
               int(item["privateDataOffset"], 16), item["privateByteLength"], item["rowSha256"], item["privateSha256"])
    require(tuple(info["selector53"] for _, info in rows) == (0x56, 0xFF, 0xFF, 0x54, 0x54, 0x53, 0x54, 0x53, 0x54),
            "Complete original nine-row selector inventory, including the chest's own reward")
    require(tuple(info["nativeClass"] for _, info in rows) == tuple(map(hx, (0x11B, 0x1F5, 0xAD, 0xAE, 0xD, 0xD, 0xD, 0xD, 0xD))),
            "Complete original nine-row class inventory")
    return tuple(rows)


def inspect_key(owners, decode):
    # This 0x400-byte window is NOT asserted to be a whole function. The known
    # 0x160 prefix and bounded lookahead remain separately labelled. Reporting
    # syntactic edges does not establish reachable state arms.
    data = window(owners, "town", KEY, 0x400)
    instructions, edges, memory = [], [], []
    for at in range(0, len(data), 4):
        pc, instruction = KEY + at, word(data, at)
        decoded = decode.decoded(pc, instruction)
        instructions.append(decoded)
        op = instruction >> 26
        if 0x20 <= op <= 0x2F:
            memory.append({**decoded, "direction": "load" if op < 0x28 else "store",
                           "baseRegisterOwnerProven": False})
        edge = decode.transfer(pc, instruction)
        if edge is None: continue
        kind, target = edge
        # The last control's delay may be outside the printed lookahead, but it
        # still has an actual bounded original owner, never a made-up NOP.
        delay = word(window(owners, "town", pc + 4, 4))
        edges.append({"pc": hx(pc), "word": hx(instruction), "kind": kind,
                      "target": hx(target) if target is not None else None,
                      "delayPc": hx(pc + 4), "delayWord": hx(delay),
                      "targetInLookahead": target is not None and KEY <= target < KEY + len(data),
                      "sequentialAfterDelay": hx(pc + 8), "callReturnExecutedOrAssumed": False})
    calls = sorted({int(edge["target"], 16) for edge in edges if edge["kind"] == "call"})
    helper_entries = []
    for target in calls:
        owner = next((name for name, basis in (("exe", EXE), ("town", OVERLAY))
                      if basis <= target <= basis + len(owners[name]) - 0x10), None)
        if owner is None:
            helper_entries.append({"target": hx(target), "owner": None, "ownerKnown": False,
                                   "completeRoutineExtentKnown": False, "executed": False})
            continue
        peek = window(owners, owner, target, 0x10)
        helper_entries.append({"target": hx(target), "owner": owner, "byteLength": len(peek),
                               "sha256": sha(peek), "instructions": [decode.decoded(target + at, word(peek, at)) for at in range(0, len(peek), 4)],
                               "completeRoutineExtentKnown": False, "executed": False})
    return {"owner": "town", "entry": hx(KEY), "byteLength": len(data), "sha256": sha(data),
            "knownPickupPrefixByteLength": 0x160, "lookaheadIsCompleteHandler": False,
            "instructions": instructions, "syntacticEdges": edges, "memoryOperands": memory,
            "helperEntryLookaheads": helper_entries,
            "directEmitterOrAwardCallInLookahead": any(target in (EMITTER, AWARD) for target in calls),
            "absenceOfDirectCallIsNotTransitiveNonAwardProof": True,
            "zeroContributionProven": False, "executed": False}


def witness(owners, native, retained, decode):
    rows = raw_rows(owners, retained)
    code, certificates, checks, refusals = {}, [], [], []

    def check(ok, label):
        require(ok, label); checks.append(label)

    def refuse(action, phrase, label):
        try: action()
        except AssertionError as error:
            require(phrase in str(error), label + ": wrong refusal: " + str(error))
            refusals.append(label); return
        raise AssertionError(label + ": unexpectedly accepted")

    for name, owner, pc, length, expected in PINS:
        data = window(owners, owner, pc, length)
        check(sha(data) == expected, "Exact independently pinned source " + name)
        bad = bytearray(data); bad[-1] ^= 1
        refuse(lambda: require(sha(bad) == expected, "Changed code pin"), "Changed code pin", "Mutation of " + name)
        certificates.append({"id": name, "owner": owner, "pc": hx(pc), "byteLength": length, "sha256": expected})
        # The unknown key continuation is deliberately NOT executable.
        if name == "key-pickup-prefix-only": continue
        for at in range(0, length, 4):
            require(pc + at not in code, "Disjoint original executable windows")
            code[pc + at] = word(data, at)
    check(word(window(owners, "town", 0x8007DB0C, 4)) == 0x86640036,
          "Original Town s3-row signed class load into a0")

    micro = native.Scalar({0x100: 0x8C820000, 0x104: 0x00401821, 0x108: 0x00402821,
                           0x10C: 0x0C000080, 0x110: 0x24060007,
                           0x200: 0x03E00008, 0x204: 0x24C60001})
    micro.map(0x1000, 4); micro.poke(0x1000, 42); micro.r[4], micro.r[2] = 0x1000, 9
    check(micro.run(0x100, {0x114}, maximum=16) == 0x114 and micro.r[3] == 9 and micro.r[5] == 42
          and micro.r[6] == 8 and micro.r[31] == 0x114, "Frozen scalar load, JAL/JR and delay controls")
    refuse(lambda: native.Scalar({}).run(VALUE, {AWARD}, maximum=1), "Unowned native instruction", "No unowned helper/code execution")

    values, value_traces = {}, []
    for selector, expected in ((0x53, 1), (0x54, 2), (0x55, 5), (0x56, 10), (0x57, 25)):
        machine = native.Scalar(TraceCode(code))
        # Only this two-byte class field is a synthetic collected-gem input.
        # No emitted child, parent copied row or successful collection is claimed.
        machine.map(ACTOR + 0x36, 2); machine.poke(ACTOR + 0x36, selector, 2)
        machine.r[16] = ACTOR
        before = machine.memory.copy()
        stop = machine.run(VALUE, {AWARD}, maximum=64)
        check(stop == AWARD and machine.r[4] == expected and machine.r[5] == ACTOR
              and machine.r[31] == 0x8003BABC, f"Actual native class {selector:02X} selects award argument {expected}")
        check(machine.memory == before and machine.writes == [] and machine.reads
              and all(at == ACTOR + 0x36 and size == 2 for at, size in machine.reads)
              and machine.calls == [(0x8003BAB4, AWARD)], f"Class {selector:02X} exact read-only value chain and real final JAL/delay")
        values[selector] = machine.r[4]
        value_traces.append({"selector": selector, "nativeValue": values[selector], **trace(machine, VALUE, stop)})

    emitter_traces = []
    # Use the full unmodified original Wizard row. The FF control changes only
    # its selector to demonstrate why FF cannot be interpreted as award zero.
    for selector in (0x56, 0xFF):
        row = bytearray(rows[0][0]); row[0x53] = selector
        machine = native.Scalar(TraceCode(code)); machine.map(ACTOR - 4, 0x60, 0xA5)
        for at, value in enumerate(row): machine.poke(ACTOR + at, value, 1)
        machine.r[19], machine.r[7] = ACTOR, 0
        before = machine.memory.copy()
        stop = machine.run(0x8003ABF4, {0x8003AC88, 0x8003AC30, 0x8003B0AC}, maximum=64)
        if selector == 0x56:
            expected_memory = before.copy(); expected_memory[ACTOR + 0x3A] = 0xFF
            check(stop == 0x8003AC88 and machine.r[16] == selector
                  and machine.writes == [(ACTOR + 0x3A, 1, 0xFF)] and machine.memory == expected_memory,
                  "Actual source 7F/56 deterministic selection has exact one-shot write and complete complement")
        else:
            check(stop == 0x8003AC30 and machine.r[16] == 0x7F, "FF masks to 7F and enters actual RNG branch, not zero/no-drop")
            stop = machine.run(stop, {RNG}, maximum=32)
            check(stop == RNG and machine.r[16] == 0x0F and machine.memory == before and machine.writes == []
                  and machine.calls[-1][1] == RNG, "Actual RNG JAL and delay, with RNG body and return unexecuted")
        emitter_traces.append({"selector": selector, "sourceRowUnmodified": selector == 0x56,
                               "emitterCalledByKeyOrGrassProven": False, **trace(machine, 0x8003ABF4, stop)})

    dispatch_traces = []
    for row, info in rows[1:3]:
        machine = native.Scalar(TraceCode(code)); machine.r[4] = half(row, 0x36)
        # Exact post-class-load entry: no visibility-list/helper return is assumed.
        stop = machine.run(DISPATCH, {LOOP, KEY}, maximum=128)
        check(stop == (LOOP if info["role"] == "grass" else KEY)
              and not machine.memory and not machine.reads and not machine.writes and not machine.calls,
              "Original Town " + info["role"] + " class-chain target with no award/helper/property activity")
        dispatch_traces.append({"role": info["role"], "nativeClassFromOriginalRow": half(row, 0x36),
                                "currentFreshHostExecuted": False, **trace(machine, DISPATCH, stop)})

    contributions = []
    for _, info in rows:
        selector = info["selector53"]
        known = selector in values
        contributions.append({**info, "deterministicSelectorValue": values[selector] if known else None,
                              "awardPathInvokedForThisRowProven": False,
                              "zeroInferredFromFF": False})
    subtotal = sum(item["deterministicSelectorValue"] or 0 for item in contributions)
    check(sum(item["deterministicSelectorValue"] is not None for item in contributions) == 7 and subtotal == 20,
          "Actual seven known deterministic selectors total 20: Wizard10 plus chest/rewards10, excluding unresolved FF rows")
    return {"marker": "FRESH_CHEST_STATIC_REWARD_VALUE_WITNESS_AND_INSPECTION_OK",
            "checks": checks, "refusals": refusals, "sourceWindows": certificates,
            "sourceRows": contributions, "valueDispatch": value_traces,
            "ffNotNoDropCounterexample": emitter_traces, "originalTownDispatch": dispatch_traces,
            "keyNonAwardPremiseInspection": inspect_key(owners, decode),
            "knownDeterministicSelectorSubtotal": subtotal,
            "grassOriginalTownDispatchContribution": 0,
            "keyContribution": None, "completeP9StaticTreasureCensus": None,
            "requiredBeforeCompleteCensus": ["Close actual key update/collection and transitive helper non-award behavior",
                                            "Bind passive/key dispatch premises to the actual proposed Fresh Host",
                                            "Bind exact nine source rows to final scene without changing selector/class fields"],
            "fullBranchValueDispatchExecuted": True, "awardBodyExecuted": False,
            "keyHandlerExecuted": False, "currentFreshExecutableRead": False,
            "sourceOnly": True, "bodyIssued": False, "nativeDependencyClosureProven": False,
            "runtimeEmissionOrCollectionProven": False, "runtime": False, "publication": False, "outputsWritten": 0}


def main(path):
    require(__debug__, "Python assertions must remain enabled")
    guards(); native, retained, decode = load("scalar"), load("rows"), load("decode")

    def identity(image):
        native.identity(image)
        require(image.tell() == native.IMAGE_LENGTH and image.read(1) == b"", "Exact original length and true EOF")

    with open(path, "rb") as image:
        identity(image)
        try:
            directory = native.logical(image, 37 * 2048, 0x800)
            require(sha(directory) == "15d3e9c45f07e27a0ddd3f3a49b23a077e2c7750021cc89c55b7b16c551186cd", "Exact original WAD directory")
            owners = {}
            for name, (entry, offset, length, digest) in OWNERS.items():
                if entry is not None:
                    require(struct.unpack_from("<II", directory, entry * 8) == (offset - 37 * 2048, length),
                            "Actual complete WAD directory owner: " + name)
                owners[name] = native.logical(image, offset, length)
                require(len(owners[name]) == length and sha(owners[name]) == digest, "Complete original source owner: " + name)
            snapshots = {name: sha(data) for name, data in owners.items()}
            report = witness(owners, native, retained, decode)
            require(snapshots == {name: sha(data) for name, data in owners.items()}, "Every original immutable owner snapshot retained")
            report.update(sourceImageSha256=native.IMAGE_SHA, sourceImageByteLength=native.IMAGE_LENGTH,
                          helpers=[{"path": relative, "sha256": digest} for relative, digest in HELPERS.values()],
                          sourceOwners=[{"id": name, "wadEntry": entry, "logicalOffset": hx(offset),
                                         "byteLength": length, "sha256": digest}
                                        for name, (entry, offset, length, digest) in OWNERS.items()])
        finally:
            identity(image); guards()
    with open(path, "rb") as reopened: identity(reopened)
    guards()
    report.update(finalHeldSourceAuthenticated=True, finalNamedSourceAuthenticated=True)
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("original_image")
    parser.add_argument("--inspect", action="store_true", required=True,
                        help="Execute only bounded known value/dispatch slices; inspect unresolved key premises")
    args = parser.parse_args()
    main(args.original_image)
