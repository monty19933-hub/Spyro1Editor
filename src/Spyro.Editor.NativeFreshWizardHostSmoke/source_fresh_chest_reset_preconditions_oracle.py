#!/usr/bin/env python3
"""Original-only inspection of the still-unproved chest reset preconditions.

No Core, producer, scalar execution, ROM/output write, socket or emulator use.
Exact reward words, seven retained rows/private blocks, and actual PETE/local
registrations are reported without assuming a state arm is unreachable. The
shared-loader successor discovery stops at actual control boundaries; its
lookahead is neither a copied function tail nor an executable certificate.
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
CONTINUATION = "src/Spyro.Editor.NativeFreshWizardHostSmoke/source_fresh_chest_continuation_oracle.py"
CONTINUATION_SHA = "4f407c80de5ae78642f94bf727dd113384ba3ad5baa62da308e9b4407a946b8a"
EXTRA_OWNERS = {
    "peace-data": (37 * 2048 + 0x1890800, 0x29E800,
                   "c140bd422471c66cd57bd85b29912819f798f5b080779153674714889306c9d3"),
    "pete-header": (37 * 2048 + 0x7B8800, 0x800,
                    "b9a4de193b5cf0bd9592de15c57aa5daa3713c3575026154b45402f3aceef6f1"),
    "pete-copy": (37 * 2048 + 0x7B9000, 0x39C48,
                  "1703ac37e59e480909b6f66dc856d4e5f3ce80551f18cbc8c06129c3b54dd35d"),
}
SHARED_ENTRY, SHARED_BYTES = 0x8005B7D8, 0xD4
SHARED_SHA = "f5f4ea14f28a2cfbab256546d7c4a41f9e796b62b152d84252d254b3d6cf1b67"
REWARD_ENTRY, REWARD_BYTES = 0x8007E59C, 0x434
REWARD_SHA = "129d8511c63925af80ab73a02def446efc39e23974255fd4b8d38f51411c2056"


def sha(data): return hashlib.sha256(data).hexdigest()
def hx(value): return f"{value:08X}"
def word(data, at=0): return struct.unpack_from("<I", data, at)[0]
def half(data, at): return struct.unpack_from("<H", data, at)[0]


def require(ok, label):
    if not ok: raise AssertionError(label)


def bounded(data, at, length):
    require(at >= 0 and length > 0 and at % 4 == 0 and length % 4 == 0
            and at <= len(data) - length, "Bounded aligned retained-owner read")
    return data[at:at + length]


def load_continuation():
    require(sha((ROOT / CONTINUATION).read_bytes()) == CONTINUATION_SHA, "Frozen 436 continuation oracle changed")
    spec = importlib.util.spec_from_file_location("chest_reset_retained_continuation", ROOT / CONTINUATION)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def reward_inventory(owners, prior):
    data = prior.extent(owners, "peace", REWARD_ENTRY, REWARD_BYTES)
    require(sha(data) == REWARD_SHA, "Exact original complete reward handler")
    instructions, controls, memory = [], [], []
    for at in range(0, len(data), 4):
        pc, instruction = REWARD_ENTRY + at, word(data, at)
        decoded = prior.decoded(pc, instruction)
        instructions.append(decoded)
        op = instruction >> 26
        if 0x20 <= op <= 0x2F:
            memory.append({**decoded, "direction": "load" if op < 0x28 else "store",
                           "baseRegisterMeaningProven": False})
        edge = prior.transfer(pc, instruction)
        if edge is None: continue
        kind, target = edge
        require(at + 8 <= len(data), "A reward control lost its actual delay-word owner")
        # These are syntactic edges, not an executed CFG. In particular a JAL's
        # return site is only reported; no helper return or reachability is assumed.
        controls.append({"pc": hx(pc), "word": hx(instruction), "kind": kind,
                         "target": hx(target) if target is not None else None,
                         "targetInsideReward": target is not None and REWARD_ENTRY <= target < REWARD_ENTRY + len(data),
                         "delayPc": hx(pc + 4), "delayWord": hx(word(data, at + 4)),
                         "sequentialAfterDelay": hx(pc + 8),
                         "sequentialEdgeIsConditionalOrUnexecutedCallReturn": kind in ("branch", "call"),
                         "computedTransfer": target is None})
    reset_sites = [edge for edge in controls if edge["target"] == "80088CF4"]
    require([edge["pc"] for edge in reset_sites] == ["8007E708", "8007E774"], "Exact two source reward-reset edges")
    return {"owner": "peace", "pc": hx(REWARD_ENTRY), "byteLength": len(data), "sha256": sha(data),
            "instructions": instructions, "syntacticControlEdges": controls, "memoryOperands": memory,
            "resetSites": reset_sites, "registerAliasesProven": False, "reachableStateArmsProven": False,
            "allWritesToPrivateStateProven": False, "helperReturnsAssumed": False, "executed": False}


def family_rows(data):
    # Exact seven source schemas already used by NativeLockedChestFamilySource.
    # Full bytes, not only selected state fields, are retained in the report.
    indices = (78, 79, 118, 119, 120, 121, 122)
    properties = (0xEA94, 0xEA98, 0xEF9C, 0xEFC4, 0xEFEC, 0xF014, 0xF03C)
    lengths = (4, 0x18, 0x28, 0x28, 0x28, 0x28, 0x28)
    scene, source_rows, fixup_at = 0x1C2800, 0x8260, 0x11234
    require((word(data, 0x18), word(data, 0x1C)) == (scene, 0x11800), "Original Peace scene descriptor")
    count = word(data, scene + fixup_at)
    require(0 < count and fixup_at + 4 + count * 4 <= 0x11800, "Actual source fixup bounds")
    fixup_bytes = bounded(data, scene + fixup_at, 4 + count * 4)
    fixups = [word(fixup_bytes, 4 + i * 4) for i in range(count)]
    rows = []
    for ordinal, (index, private_at, length) in enumerate(zip(indices, properties, lengths)):
        row_at = scene + source_rows + index * 0x58
        row, private = bounded(data, row_at, 0x58), bounded(data, scene + private_at, length)
        expected_class = 0xAD if ordinal == 0 else 0xAE if ordinal == 1 else 0xD
        require(half(row, 0x36) == expected_class and word(row) == private_at, "Actual source class/property pointer")
        require(source_rows + index * 0x58 in fixups
                and not any(private_at <= field < private_at + length for field in fixups),
                "Source pointer-fixup versus integer private-field ownership")
        if ordinal: require(word(private) == (78 if ordinal == 1 else 79), "Exact native private integer owner index")
        rows.append({"role": "key" if ordinal == 0 else "chest" if ordinal == 1 else "reward." + str(ordinal - 1),
                     "trueIndex": index, "class": hx(expected_class),
                     "rowDataOffset": hx(row_at), "rowWadOffset": hx(0x1890800 + row_at),
                     "rowByteLength": len(row), "rowSha256": sha(row), "rowHex": row.hex(),
                     "rowFields": {"propertiesSceneRelative": hx(word(row)), "class36": half(row, 0x36),
                                   "byte3A": row[0x3A], "byte48": row[0x48], "byte49": row[0x49],
                                   "byte4A": row[0x4A], "reward53": row[0x53]},
                     "privateSceneOffset": hx(private_at), "privateDataOffset": hx(scene + private_at),
                     "privateWadOffset": hx(0x1890800 + scene + private_at),
                     "privateByteLength": length, "privateSha256": sha(private), "privateHex": private.hex(),
                     "privateWords": [{"offset": hx(at), "word": hx(word(private, at))} for at in range(0, length, 4)],
                     "privateInternalPointerFixups": [], "runtimeInitializationExecuted": False})
    require(sum(row["privateByteLength"] for row in rows) == 0xE4, "Complete seven-row private E4 budget")
    return {"sceneDataOffset": hx(scene), "sceneByteLength": 0x11800, "sourceRowsSceneOffset": hx(source_rows),
            "sourceFixupCount": count, "sourceFixupSha256": sha(fixup_bytes), "rowCount": len(rows),
            "privateByteLength": 0xE4, "rows": rows, "runtimeStateInvariantProven": False}


def package_report(data, at, end, source_wad_base, label):
    package = bounded(data, at, end - at)
    peek_length = min(0x40, len(package))
    first = word(package)
    return {"label": label, "ownerOffset": hx(at), "wadOffset": hx(source_wad_base + at),
            "nextAdjacentOwnerOffset": hx(end), "byteLength": len(package), "sha256": sha(package),
            "headerInspectionByteLength": peek_length,
            "headerWords": [{"offset": hx(i), "word": hx(word(package, i))} for i in range(0, peek_length, 4)],
            "firstWordSignedNegative": bool(first & 0x80000000), "nativeRelocationExecuted": False,
            "animatedResetPointerChainProven": False}


def registrations(owners, raw):
    header, payload = owners["pete-header"], owners["pete-copy"]
    require(word(header) == len(payload), "Exact complete PETE runtime-copy length")
    slots, ended = [], False
    for slot in range(64):
        root, native_class = word(header, 4 + slot * 8), word(header, 8 + slot * 8)
        if root == 0:
            ended = True; require(native_class == 0, "PETE terminator class")
            continue
        require(not ended and root >= 0x800 and root % 4 == 0 and root + 4 <= len(payload) + 0x800
                and native_class < 512 and all(native_class != old[1] for old in slots)
                and (not slots or root > slots[-1][2]), "Actual bounded PETE registration grammar")
        slots.append((slot, native_class, root))
    require(len(slots) == 54 and slots[0] == (0, 0, 0x800), "Existing complete shared registration census")
    selected = []
    for native_class in (0xD, 0xAD, 0x53, 0x54):
        found = [(index, entry) for index, entry in enumerate(slots) if entry[1] == native_class]
        item = {"class": hx(native_class), "present": bool(found)}
        if found:
            index, (slot, _, root) = found[0]
            end = slots[index + 1][2] if index + 1 < len(slots) else 0x800 + len(payload)
            item.update(slot=slot, headerRoot=hx(root), headerRootField=hx(4 + slot * 8),
                        headerClassField=hx(8 + slot * 8), package=package_report(
                            payload, root - 0x800, end - 0x800, 0x7B9000, "Actual adjacent PETE registration interval"))
        selected.append(item)
    # Existing independent key certificate: shared SimpleModel, NOT evidence
    # for an animated +38 pointer chain or permission to enter reset as AD.
    require(slots[10] == (10, 0xAD, 0x34A8C) and slots[11][2] == 0x34CA8, "Existing PETE key registration control")
    key = bounded(payload, 0x34A8C - 0x800, 0x21C)
    require(sha(key) == "72a5a017f8696edfc2d600b093c1b05c29ba3a423b29ad42b838a3385f5298e2"
            and word(key) & 0x80000000 and key[1] == 48
            and tuple(word(key, at) for at in (4, 8, 12)) == (0x10, 0x98, 0x9C), "Exact original AD simple-model control")
    local = raw.roots(owners["peace-data"])
    local_selected = []
    for native_class in (0xD, 0xAD, 0xAE, 0x53, 0x54):
        item = {"class": hx(native_class), "present": native_class in local}
        if native_class in local:
            at, length = local[native_class]
            item["package"] = package_report(owners["peace-data"], at, at + length, 0x1890800,
                                             "Actual adjacent local registration interval")
        local_selected.append(item)
    require(local.get(0xAE) == (0x1B3978, 0x1008), "Existing exact local chest package owner")
    require(sha(bounded(owners["peace-data"], 0x1B3978, 0x1008)) ==
            "8edc9e9ac1e5fb1a92224d2f5f8ca541371940c1ae3f9873c02f6b802d6e22e7", "Exact local AE source package control")
    return {"sharedRegistrations": [{"slot": slot, "class": hx(cls), "root": hx(root)} for slot, cls, root in slots],
            "selectedSharedPackages": selected, "selectedLocalPackages": local_selected,
            "sharedADIsSimpleModel": True, "sharedADAnimatedResetCompatibilityProven": False,
            "actualRuntimeModelTableObserved": False, "sourceIssuedCurrentFreshExecutableCompared": False}


def discover_control_block(owners, prior, entry):
    # Read at most64 instruction positions plus the first control's delay word.
    # No branch/helper target is followed here and no bytes become code owners.
    for index in range(64):
        pc = entry + index * 4
        instruction = word(prior.extent(owners, "exe", pc, 4))
        edge = prior.transfer(pc, instruction)
        if edge is None: continue
        data = prior.extent(owners, "exe", entry, index * 4 + 8)
        target = edge[1]
        return {"entryPc": hx(entry), "byteLength": len(data), "sha256": sha(data),
                "instructions": [prior.decoded(entry + at, word(data, at)) for at in range(0, len(data), 4)],
                "firstControlPc": hx(pc), "firstControlKind": edge[0],
                "firstControlTarget": hx(target) if target is not None else None,
                "targetInsidePreviouslyPinnedPrefix": target is not None and SHARED_ENTRY <= target < SHARED_ENTRY + SHARED_BYTES,
                "sequentialAfterDelay": hx(pc + 8), "discoveryInstructionBound": 64,
                "executed": False, "functionExtentCertified": False}
    raise AssertionError("Shared-loader successor reached finite inspection bound without a control boundary")


def shared_loader(owners, prior):
    prefix = prior.inspect_window(owners, "exe", SHARED_ENTRY, SHARED_BYTES,
                                  "Existing shared-loader witness through first class0 registration", SHARED_SHA)
    first = discover_control_block(owners, prior, SHARED_ENTRY + SHARED_BYTES)
    blocks = [first]
    if first["firstControlKind"] == "branch":
        # Inspect the actual not-taken sequential successor, but neither assume
        # the branch result nor execute a call or fabricate its return state.
        blocks.append(discover_control_block(owners, prior, int(first["sequentialAfterDelay"], 16)))
    return {"previouslyPinnedPrefix": prefix, "newlyInspectedBlocks": blocks, "maximumNewBlocks": 2,
            "branchTargetsFollowed": False, "helperBodiesFollowed": False, "completeSharedLoopExecuted": False,
            "completeTailExtentKnown": False, "currentFreshExecutableCompared": False}


def main(path):
    require(__debug__, "Python assertions must remain enabled")
    prior = load_continuation()
    prior.require_helpers()
    raw = prior.load(next(iter(prior.HELPERS)), "chest_reset_original_reader")
    # Definition-only import checks transitive Host/scalar helper identities.
    # Never call its capture/main or any scalar run method in this inspection.
    host = prior.load(list(prior.HELPERS)[1], "chest_reset_original_host")

    def guards():
        require(sha((ROOT / CONTINUATION).read_bytes()) == CONTINUATION_SHA, "Frozen continuation helper changed during inspection")
        prior.require_helpers(); host.require_helpers()

    guards()
    specs = {**prior.OWNERS, **EXTRA_OWNERS}
    with open(path, "rb") as image:
        raw.identity(image)
        try:
            directory = raw.logical(image, 37 * 2048, 0x800)
            require(sha(directory) == "15d3e9c45f07e27a0ddd3f3a49b23a077e2c7750021cc89c55b7b16c551186cd", "Exact original WAD directory")
            for index, pair in ((8, (0x7B8800, 0x3A000)), (15, (0x118E800, 0xF800)),
                                (21, (0x187E000, 0x12800)), (22, (0x1890800, 0x29E800))):
                require(struct.unpack_from("<II", directory, index * 8) == pair, "Actual original source directory owner")
            owners = {name: raw.logical(image, offset, length) for name, (offset, length, _) in specs.items()}
            for name, (_, length, digest) in specs.items():
                require(len(owners[name]) == length and sha(owners[name]) == digest, "Complete retained source owner: " + name)
            report = {"marker": "FRESH_CHEST_RESET_PRECONDITION_INSPECTION_OK", "sourceImageSha256": raw.IMAGE_SHA,
                      "frozenContinuationOracleSha256": CONTINUATION_SHA,
                      "owners": [{"id": name, "logicalOffset": hx(offset), "byteLength": length, "sha256": digest}
                                 for name, (offset, length, digest) in specs.items()],
                      "reward": reward_inventory(owners, prior), "family": family_rows(owners["peace-data"]),
                      "registrations": registrations(owners, raw), "sharedLoader": shared_loader(owners, prior),
                      "sourceStateReachabilityProven": False, "actualResetModelChainProven": False,
                      "scalarExecuted": False, "currentFreshExecutableRead": False,
                      "freshHostExtensionAdmitted": False, "runtime": False, "publication": False}
        finally:
            raw.identity(image); guards()
    with open(path, "rb") as image: raw.identity(image)
    guards()
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("original_image")
    parser.add_argument("--inspect", action="store_true", required=True,
                        help="Only inspect exact original preconditions; no scalar or production admission")
    arguments = parser.parse_args()
    main(arguments.original_image)
