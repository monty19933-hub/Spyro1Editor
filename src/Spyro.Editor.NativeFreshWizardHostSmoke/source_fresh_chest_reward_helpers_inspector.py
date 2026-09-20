#!/usr/bin/env python3
"""Original-only bounded helper inspection for the retained reward precondition.

The frozen 436 and 441 oracles are definition-only dependencies. No scalar
execution, inferred helper return, Core invocation, output image, socket or
emulator operation occurs. A discovered first control plus its delay word is
not a complete-function certificate, and no outgoing target is followed.
"""
import argparse
import hashlib
import importlib.util
import json
import os
import struct
import sys
from pathlib import Path

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[2]
PRECONDITIONS = "src/Spyro.Editor.NativeFreshWizardHostSmoke/source_fresh_chest_reset_preconditions_oracle.py"
PRECONDITIONS_SHA = "8fda30564e5d1ad8f8262a77375dc6bbdc0b846be137ce842e5b90b4d4def6c8"
CONTINUATION = "src/Spyro.Editor.NativeFreshWizardHostSmoke/source_fresh_chest_continuation_oracle.py"
CONTINUATION_SHA = "4f407c80de5ae78642f94bf727dd113384ba3ad5baa62da308e9b4407a946b8a"
COPY_ENTRY, COPY_BYTES = 0x80017700, 0x28
COPY_SHA = "7c1af3c5556925409e78330cd5a9bfa66cdf2a0ef040af9aa3f7d8a062aca56e"
COPY_WORDS = (0x8CA10000, 0x8CA20004, 0x8CA30008, 0xAC810000,
              0xAC820004, 0x03E00008, 0xAC830008)
UNSEEN_ENTRY, DISCOVERY_WORD_BUDGET = 0x8001778C, 64


def sha(data): return hashlib.sha256(data).hexdigest()
def hx(value): return f"{value:08X}"


def require(ok, label):
    if not ok: raise AssertionError(label)


def guard_frozen():
    for relative, digest in ((PRECONDITIONS, PRECONDITIONS_SHA), (CONTINUATION, CONTINUATION_SHA)):
        require(sha((ROOT / relative).read_bytes()) == digest, "Frozen oracle identity: " + relative)


def load_preconditions():
    guard_frozen()
    spec = importlib.util.spec_from_file_location("chest_reward_frozen_preconditions", ROOT / PRECONDITIONS)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    require(module.CONTINUATION == CONTINUATION and module.CONTINUATION_SHA == CONTINUATION_SHA,
            "441 retains the exact frozen 436 dependency")
    return module


def known_copy(owners, prior):
    data = prior.extent(owners, "exe", COPY_ENTRY, COPY_BYTES)
    require(sha(data) == COPY_SHA and data[:len(COPY_WORDS) * 4] == struct.pack("<7I", *COPY_WORDS),
            "Previously independent copy-helper complete-window and literal controls")
    report = prior.inspect_window(owners, "exe", COPY_ENTRY, COPY_BYTES,
                                  "Existing 17700 copy-helper control; all actual window words", COPY_SHA)
    report.update(firstControlPc="80017714", firstControlWord="03E00008", firstControlRegister=31,
                  delayPc="80017718", delayWord="AC830008", firstControlAndDelayByteLength=0x1C,
                  trailingWindowByteLength=COPY_BYTES - 0x1C, trailingWindowExecuted=False,
                  functionExtentCertified=False, scalarExecuted=False, currentFreshExecutableCompared=False)
    return report


def discover_helper(owners, prior):
    # The budget includes the delay word. Never inspect a 65th word, assume a
    # return opcode, read a guessed tail, or follow a discovered target.
    for index in range(DISCOVERY_WORD_BUDGET - 1):
        pc = UNSEEN_ENTRY + index * 4
        instruction = prior.word(prior.extent(owners, "exe", pc, 4))
        edge = prior.transfer(pc, instruction)
        if edge is None: continue
        length = (index + 2) * 4
        report = prior.inspect_window(owners, "exe", UNSEEN_ENTRY, length,
                                      "1778C entry through first observed control plus actual delay")
        delay = prior.word(prior.extent(owners, "exe", pc + 4, 4))
        report.update(firstControlPc=hx(pc), firstControlWord=hx(instruction), firstControlKind=edge[0],
                      firstControlTarget=hx(edge[1]) if edge[1] is not None else None,
                      firstControlRegister=(instruction >> 21) & 31 if instruction >> 26 == 0 else None,
                      delayPc=hx(pc + 4), delayWord=hx(delay), sequentialAfterDelay=hx(pc + 8),
                      discoveryWordBudgetIncludingDelay=DISCOVERY_WORD_BUDGET,
                      targetFollowed=False, helperReturnAssumed=False, functionExtentCertified=False,
                      scalarExecuted=False, currentFreshExecutableCompared=False)
        return report
    raise AssertionError("1778C inspection exhausted its 64-word including-delay budget without a control")


def reward_context(owners, preconditions, prior):
    inventory = preconditions.reward_inventory(owners, prior)
    # These are slices of the already pinned full 434-byte reward owner, not
    # newly guessed function extents or a substitute for the outer class switch.
    windows = (
        ("Entry owner-index derivation and private+10/+18/+14 gates", 0x8007E59C, 0x70),
        ("Alternate full row-copy loop through actual 526A8 call and delay", 0x8007E660, 0x50),
        ("Private+10 store and initialized private+18 copy branch", 0x8007E77C, 0x1C),
        ("Retained row+49 and owner+48 loop decision", 0x8007E8C8, 0x24),
    )
    slices = []
    for label, entry, length in windows:
        require(preconditions.REWARD_ENTRY <= entry
                and entry + length <= preconditions.REWARD_ENTRY + preconditions.REWARD_BYTES,
                "Reward context belongs entirely to the frozen 441 owner")
        slices.append(prior.inspect_window(owners, "peace", entry, length, label))
    calls = [edge for edge in inventory["syntacticControlEdges"] if edge["kind"] == "call"]
    return {"completeRewardPc": inventory["pc"], "completeRewardByteLength": inventory["byteLength"],
            "completeRewardSha256": inventory["sha256"], "necessaryBranchWindows": slices,
            "retainedRewardControlTable": inventory["syntacticControlEdges"],
            "actualCallSites": calls, "actualResetSites": inventory["resetSites"],
            "outerClassDispatchInspected": False, "registerAliasesExecuted": False,
            "firstEntryPathExecuted": False, "postCallStateAssumed": False,
            "privateStateInvariantProven": False, "alternateOwnerCopyExecuted": False}


def file_identity(state):
    return (state.st_dev, state.st_ino, state.st_size, state.st_mtime_ns, state.st_ctime_ns)


def main(path):
    require(__debug__, "Python assertions must remain enabled")
    preconditions = load_preconditions()
    prior = preconditions.load_continuation()
    prior.require_helpers()
    raw = prior.load(next(iter(prior.HELPERS)), "chest_reward_original_reader")
    host = prior.load(list(prior.HELPERS)[1], "chest_reward_original_host_definitions")

    def guards():
        guard_frozen(); prior.require_helpers(); host.require_helpers()

    guards()
    specs = {**prior.OWNERS, "peace-data": preconditions.EXTRA_OWNERS["peace-data"]}
    with open(path, "rb") as image:
        initial = file_identity(os.fstat(image.fileno()))

        def held_and_named_identity():
            require(file_identity(os.fstat(image.fileno())) == initial
                    and file_identity(os.stat(path)) == initial, "Held and named original file identity")

        held_and_named_identity(); raw.identity(image)
        try:
            directory = raw.logical(image, 37 * 2048, 0x800)
            require(sha(directory) == "15d3e9c45f07e27a0ddd3f3a49b23a077e2c7750021cc89c55b7b16c551186cd",
                    "Complete original WAD directory")
            for index, pair in ((15, (0x118E800, 0xF800)), (21, (0x187E000, 0x12800)),
                                (22, (0x1890800, 0x29E800))):
                require(struct.unpack_from("<II", directory, index * 8) == pair, "Actual original source owner")
            owners = {name: raw.logical(image, offset, length) for name, (offset, length, _) in specs.items()}
            for name, (_, length, digest) in specs.items():
                require(len(owners[name]) == length and sha(owners[name]) == digest,
                        "Complete retained source owner: " + name)
            report = {"marker": "FRESH_CHEST_REWARD_HELPERS_INSPECTION_OK", "sourceImageSha256": raw.IMAGE_SHA,
                      "frozenPreconditionsOracleSha256": PRECONDITIONS_SHA,
                      "frozenContinuationOracleSha256": CONTINUATION_SHA,
                      "owners": [{"id": name, "logicalOffset": hx(offset), "byteLength": length, "sha256": digest}
                                 for name, (offset, length, digest) in specs.items()],
                      "knownCopyHelper": known_copy(owners, prior),
                      "newHelperFirstControl": discover_helper(owners, prior),
                      "rewardContext": reward_context(owners, preconditions, prior),
                      "retainedFamily": preconditions.family_rows(owners["peace-data"]),
                      "scalarExecuted": False, "helperBodiesExecuted": False,
                      "sourceStateReachabilityProven": False, "actualResetModelChainProven": False,
                      "currentFreshExecutableRead": False, "freshHostExtensionAdmitted": False,
                      "runtime": False, "publication": False}
        finally:
            raw.identity(image); held_and_named_identity(); guards()
    with open(path, "rb") as image:
        require(file_identity(os.fstat(image.fileno())) == initial, "Reopened named original file identity")
        raw.identity(image)
        require(file_identity(os.fstat(image.fileno())) == initial
                and file_identity(os.stat(path)) == initial, "Final held and named original file identity")
    guards()
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("original_image")
    parser.add_argument("--inspect", action="store_true", required=True,
                        help="Inspect exact original helper boundaries only; no scalar execution or admission")
    arguments = parser.parse_args()
    main(arguments.original_image)
