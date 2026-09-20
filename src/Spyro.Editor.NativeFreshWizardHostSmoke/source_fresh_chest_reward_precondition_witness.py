#!/usr/bin/env python3
"""Independent original-only scalar reward initialization/precondition witness.

Executes retained D-row waiting paths and both inspected vector helpers. The
alternate owner-copy stops at 526A8; its later scan has a separately declared
post-call entry, NOT a simulated initializer return. No model/reset execution,
Core, Fresh admission, output image, native process, socket or emulator use.
"""
import argparse
import copy
import hashlib
import importlib.util
import json
import os
import struct
import sys
from pathlib import Path

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[2]
INSPECTOR = "src/Spyro.Editor.NativeFreshWizardHostSmoke/source_fresh_chest_reward_helpers_inspector.py"
INSPECTOR_SHA = "a2ce7274c41850693deba94b4bc1c338eddbb436c4c920e5387a03fc77526f4c"
PRECONDITIONS_SHA = "8fda30564e5d1ad8f8262a77375dc6bbdc0b846be137ce842e5b90b4d4def6c8"
CONTINUATION_SHA = "4f407c80de5ae78642f94bf727dd113384ba3ad5baa62da308e9b4407a946b8a"
SUB_ENTRY, SUB_BYTES = 0x8001778C, 0x34
SUB_SHA = "6915fb72b182f1d700f2505c7df98ab3bdc03271110b2051c56b21975a4f9e6a"
SUB_WORDS = (0x8CA10000, 0x8CA20004, 0x8CA30008, 0x8CC70000, 0x8CC80004,
             0x8CC90008, 0x00270822, 0x00481022, 0x00691822, 0xAC810000,
             0xAC820004, 0x03E00008, 0xAC830008)
ENTRY, LOOP, RESET = 0x8007E59C, 0x8008A20C, 0x80088CF4
ROWS, PRIVATE, STRIDE, ROW_COUNT = 0x80140000, 0x80150000, 0x58, 123
BASE_GLOBAL, END_GLOBAL = 0x80075828, 0x80075890
CHEST = ROWS + 79 * STRIDE
UNEXECUTED_CALLS = {0x80052568, 0x800526A8, 0x80037F90, 0x80017758,
                    0x8004D5EC, 0x8003ABC0, 0x8003B7C0, 0x80055A78}
STOPS = UNEXECUTED_CALLS | {LOOP, RESET, 0x8008A204, 0x800880FC}


def sha(data): return hashlib.sha256(data).hexdigest()
def hx(value): return f"{value:08X}"
def u32(value): return value & 0xFFFFFFFF
def word(data, at=0): return struct.unpack_from("<I", data, at)[0]


def require(ok, label):
    if not ok: raise AssertionError(label)


def frozen_inspector():
    require(sha((ROOT / INSPECTOR).read_bytes()) == INSPECTOR_SHA, "Frozen 443 inspector")
    spec = importlib.util.spec_from_file_location("reward_witness_frozen_443", ROOT / INSPECTOR)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    require((module.PRECONDITIONS_SHA, module.CONTINUATION_SHA) == (PRECONDITIONS_SHA, CONTINUATION_SHA),
            "Exact 443/441/436 dependency chain")
    return module


class TraceCode(dict):
    """Instruction-fetch tracing only; execution remains the frozen interpreter."""
    def __init__(self, source):
        super().__init__(source)
        self.executed = []

    def __getitem__(self, pc):
        self.executed.append(pc)
        return super().__getitem__(pc)


def source_code(owners, inspected, prior):
    reward = prior.extent(owners, "peace", ENTRY, 0x434)
    require(sha(reward) == "129d8511c63925af80ab73a02def446efc39e23974255fd4b8d38f51411c2056",
            "Complete reward source owner")
    control = prior.extent(owners, "exe", inspected.COPY_ENTRY, inspected.COPY_BYTES)
    require(sha(control) == inspected.COPY_SHA
            and control[:0x1C] == struct.pack("<7I", *inspected.COPY_WORDS), "Exact complete copy control")
    subtract = prior.extent(owners, "exe", SUB_ENTRY, SUB_BYTES)
    require(sha(subtract) == SUB_SHA and subtract == struct.pack("<13I", *SUB_WORDS), "Exact inspected subtraction leaf")
    spans = (("reward", ENTRY, reward), ("copy-leaf-only", inspected.COPY_ENTRY, control[:0x1C]),
             ("subtract-leaf", SUB_ENTRY, subtract))
    code, evidence = {}, []
    for label, pc, data in spans:
        for at in range(0, len(data), 4):
            require(pc + at not in code, "Executable owner overlap")
            code[pc + at] = word(data, at)
        evidence.append({"id": label, "pc": hx(pc), "byteLength": len(data), "sha256": sha(data)})
    return code, evidence


def peek(machine, address, size=4):
    return int.from_bytes(machine.bytes(address, size), "little")


def put_bytes(machine, address, data):
    for i, value in enumerate(data): machine.poke(address + i, value, 1)


def expected_memory(before, writes):
    result = before.copy()
    for address, size, value in writes:
        for i in range(size):
            require(address + i in result, "Expected store lost its declared owner")
            result[address + i] = (value >> (8 * i)) & 255
    return result


def witness(owners, inspected, preconditions, prior, host):
    code, source_windows = source_code(owners, inspected, prior)
    family = preconditions.family_rows(owners["peace-data"])
    retained = {item["trueIndex"]: item for item in family["rows"]}
    checks, refusals, traces = [], [], []

    def check(ok, label):
        require(ok, label); checks.append(label)

    def refuse(action, message, label):
        try: action()
        except AssertionError as error:
            require(message in str(error), label + ": wrong refusal: " + str(error))
            refusals.append(label); return
        raise AssertionError(label + ": unexpectedly accepted")

    def fixture(index=118):
        require(index in range(118, 123) and retained[index]["class"] == "0000000D", "Only actual retained D rows")
        m = host.native.Scalar(TraceCode(code))
        # Non-family gaps are explicitly synthetic inactive rows for the later
        # bounded scan, not a claim to have initialized the entire Peace scene.
        m.map(ROWS - 0x10, ROW_COUNT * STRIDE + 0x20, 0xA5)
        private_start = min(int(item["privateSceneOffset"], 16) for item in family["rows"])
        private_end = max(int(item["privateSceneOffset"], 16) + item["privateByteLength"] for item in family["rows"])
        m.map(PRIVATE + private_start - 4, private_end - private_start + 8, 0xA6)
        for item in family["rows"]:
            row = ROWS + item["trueIndex"] * STRIDE
            props = PRIVATE + int(item["privateSceneOffset"], 16)
            put_bytes(m, row, bytes.fromhex(item["rowHex"]))
            put_bytes(m, props, bytes.fromhex(item["privateHex"]))
            m.poke(row, props)  # Only the source-declared row-pointer relocation.
        for at, value in ((BASE_GLOBAL, ROWS), (END_GLOBAL, ROWS + ROW_COUNT * STRIDE)):
            m.map(at, 4); m.poke(at, value)
        m.map(0x801EFFE0, 0x80, 0x6B)
        m.r = [0] + [u32(0x51000000 + i * 0x010101) for i in range(1, 32)]
        m.r[20], m.r[29] = ROWS + index * STRIDE, 0x801F0000
        return m

    def clear(m):
        m.reads.clear(); m.writes.clear(); m.calls.clear(); m.code.executed.clear(); m.instructions = 0

    def run(m, entry, stops, maximum, label):
        result = m.run(entry, stops, maximum=maximum)
        pcs = m.code.executed
        actual_words = [dict.__getitem__(m.code, pc) for pc in pcs]
        check(m.instructions == len(pcs) and 0 < len(pcs) <= maximum and all(pc in code for pc in pcs),
              label + " finite owned instruction trace")
        # Neither interpreter extensions nor unimplemented HI/LO semantics are
        # needed: no executed instruction reads or writes HI/LO or invokes GTE.
        check(all(not (instruction >> 26 == 0 and (instruction & 63) in range(0x10, 0x1C))
                  and instruction >> 26 not in (0x10, 0x11, 0x12, 0x13) for instruction in actual_words),
              label + " contains no HI/LO or coprocessor operations")
        traces.append({"label": label, "entry": hx(entry), "stop": hx(result), "maximumInstructions": maximum,
                       "instructions": len(pcs), "traceSha256": sha(b"".join(struct.pack("<II", pc, instruction)
                                                                                 for pc, instruction in zip(pcs, actual_words))),
                       "sourceWordsUnchanged": all(instruction == code[pc] for pc, instruction in zip(pcs, actual_words)),
                       "calls": [{"pc": hx(pc), "target": hx(target)} for pc, target in m.calls]})
        return result

    def wait_tick(m, initial, label, gate_reads=()):
        clear(m)
        before, registers = m.memory.copy(), m.r.copy()
        row, props = m.r[20], peek(m, m.r[20])
        xyz = [peek(m, CHEST + 0xC + i * 4) for i in range(3)]
        source_xyz = [peek(m, props + 4 + i * 4) for i in range(3)]
        writes = []
        if initial:
            writes += [(props + 4 + i * 4, 4, u32(source_xyz[i] - xyz[i])) for i in range(3)]
            writes += [(row + 0x52, 1, 0xFF), (props + 0x10, 4, 1)]
        writes += [(row + 0xC + i * 4, 4, xyz[i]) for i in range(3)]
        expected = registers.copy()
        changed = {1: xyz[0], 2: 9, 3: xyz[2], 4: props + 0x1C, 5: CHEST + 0xC,
                   16: CHEST, 17: props, 31: 0x8007E798}
        if initial: changed.update({6: CHEST + 0xC, 7: xyz[0], 8: xyz[1], 9: 1})
        for register, value in changed.items(): expected[register] = value
        reads = [(row, 4), (props, 4), (props + 0x10, 4), (BASE_GLOBAL, 4), (props + 0x18, 4)]
        if initial:
            reads += [(props + 4 + i * 4, 4) for i in range(3)]
            reads += [(CHEST + 0xC + i * 4, 4) for i in range(3)]
            reads += [(props + 0x14, 4), *gate_reads, (props + 0x18, 4)]
        reads += [(CHEST + 0xC + i * 4, 4) for i in range(3)]
        reads += [(props + 0x1C, 4), (row + 0x49, 1), (CHEST + 0x48, 1)]
        calls = ([(0x8007E5E4, SUB_ENTRY)] if initial else []) + [(0x8007E790, 0x80017700)]
        stop = run(m, ENTRY, STOPS, 256, label)
        check(stop == LOOP and RESET not in m.code.executed and 0x8007E660 not in m.code.executed,
              label + " reaches actual donor loop without reset or row replacement")
        check(m.r == expected and m.memory == expected_memory(before, writes) and m.writes == writes
              and m.reads == reads and m.calls == calls, label + " complete registers, memory complement, reads/writes and calls")
        check(peek(m, row + 0x36, 2) == 0xD and peek(m, props + 0x10) == 1
              and peek(m, props + 0x14) == int.from_bytes(bytes(before[props + 0x14 + i] for i in range(4)), "little")
              and peek(m, props + 0x18) == 1 and (0x8007E77C in m.code.executed) == initial,
              label + " exact class/private-state transition and initialization gate")

    # Independent controls for the frozen scalar's two relevant delay rules.
    micro = host.native.Scalar({0x100: 0x8C820000, 0x104: 0x00401821, 0x108: 0x00402821,
                                0x10C: 0x0C000080, 0x110: 0x24060007,
                                0x200: 0x03E00008, 0x204: 0x24C60001})
    micro.map(0x1000, 4); micro.poke(0x1000, 42); micro.r[4], micro.r[2] = 0x1000, 9
    check(micro.run(0x100, {0x114}, maximum=16) == 0x114 and micro.r[3] == 9 and micro.r[5] == 42
          and micro.r[6] == 8 and micro.r[31] == 0x114, "Pinned scalar load/JAL/JR and both control delays")

    first_trace = None
    for index in range(118, 123):
        m = fixture(index)
        wait_tick(m, True, f"D{index} retained initial entry")
        if first_trace is None: first_trace = [hx(pc) for pc in m.code.executed]
        wait_tick(m, False, f"D{index} retained repeat")
        for i, delta in enumerate((17, -9, 5)):
            m.poke(CHEST + 0xC + i * 4, u32(peek(m, CHEST + 0xC + i * 4) + delta))
        wait_tick(m, False, f"D{index} repeated tick after explicit owner motion")

    # Actual source branches, changed inputs: +14 alone does not force reset
    # when the owner is active and its reward high bit is clear.
    blocked = fixture(); blocked.poke(peek(blocked, blocked.r[20]) + 0x14, 1)
    wait_tick(blocked, True, "Changed +14 with inactive gate still false",
              ((CHEST + 0x48, 1), (CHEST + 0x53, 1)))
    initialized = fixture(); wait_tick(initialized, True, "Initialized control before changed +14")
    initialized.poke(peek(initialized, initialized.r[20]) + 0x14, 1)
    wait_tick(initialized, False, "Nonzero +10 bypasses subsequently changed +14")

    no_relative = fixture(); row, props = no_relative.r[20], peek(no_relative, no_relative.r[20])
    no_relative.poke(props + 0x18, 0)
    before, expected = no_relative.memory.copy(), no_relative.r.copy()
    for register, value in {2: 9, 3: ROWS, 4: props + 0x1C, 9: 1, 16: CHEST, 17: props}.items():
        expected[register] = value
    writes = [(row + 0x52, 1, 0x20), (props + 0x10, 4, 1)]
    reads = [(row, 4), (props, 4), (props + 0x10, 4), (BASE_GLOBAL, 4), (props + 0x18, 4),
             (props + 0x14, 4), (props + 0x18, 4), (props + 0x1C, 4), (row + 0x49, 1), (CHEST + 0x48, 1)]
    stop = run(no_relative, ENTRY, STOPS, 128, "Changed +18 zero excludes both vector helpers")
    check(stop == LOOP and no_relative.r == expected and no_relative.memory == expected_memory(before, writes)
          and no_relative.writes == writes and no_relative.reads == reads and no_relative.calls == [],
          "Actual +18 branch changes row52 to20 with exact register/memory complement and no helper")

    for owner_status, owner_selector in ((0x80, 0x54), (0, 0xD4)):
        m = fixture(); row, props = m.r[20], peek(m, m.r[20])
        m.poke(props + 0x14, 1); m.poke(CHEST + 0x48, owner_status, 1); m.poke(CHEST + 0x53, owner_selector, 1)
        before, registers = m.memory.copy(), m.r.copy()
        xyz = [peek(m, CHEST + 0xC + i * 4) for i in range(3)]
        relative = [u32(peek(m, props + 4 + i * 4) - xyz[i]) for i in range(3)]
        selector = peek(m, row + 0x53, 1)
        writes = [(props + 4 + i * 4, 4, relative[i]) for i in range(3)] + [
            (row + 0x52, 1, 0xFF), (CHEST + 0x53, 1, selector)]
        expected = registers.copy()
        for register, value in {1: relative[0], 2: selector, 3: relative[2], 4: row, 5: props + 4,
                                6: CHEST + 0xC, 7: xyz[0], 8: xyz[1], 9: 1,
                                16: CHEST, 17: props, 31: 0x8007E644}.items(): expected[register] = value
        stop = run(m, ENTRY, STOPS, 256, f"Changed alternate gate {owner_status:02X}/{owner_selector:02X}")
        check(stop == 0x80052568 and m.r == expected and m.memory == expected_memory(before, writes)
              and m.writes == writes and m.calls == [(0x8007E5E4, SUB_ENTRY), (0x8007E63C, 0x80052568)]
              and peek(m, props + 0x10) == 0, "Changed gate genuinely reaches delete call with actual delay; no return synthesized")

    # Explicit post-delete cut. It proves the native full-row copy, NOT the
    # effects/return of the two preceding possible delete calls.
    m = fixture(); row, props = m.r[20], peek(m, m.r[20])
    m.r[16], m.r[17] = CHEST, props
    m.poke(props + 0x14, 1); m.poke(CHEST + 0x48, 0x80, 1)
    m.poke(CHEST + 0x53, peek(m, row + 0x53, 1), 1)
    owner_bytes, before, registers = m.bytes(CHEST, STRIDE), m.memory.copy(), m.r.copy()
    writes = [(row + at, 4, word(owner_bytes, at)) for at in range(0, STRIDE, 4)]
    expected = registers.copy()
    for register, value in {2: word(owner_bytes, 0x50), 3: word(owner_bytes, 0x54), 4: row,
                            5: word(owner_bytes, 0x4C), 6: CHEST + 0x50, 7: row + 0x50,
                            8: CHEST + 0x50, 31: 0x8007E6B0}.items(): expected[register] = value
    stop = run(m, 0x8007E660, STOPS, 128, "Explicit post-delete complete owner-copy cut")
    check(stop == 0x800526A8 and m.r == expected and m.memory == expected_memory(before, writes)
          and m.writes == writes and m.reads == [(CHEST + at, 4) for at in range(0, STRIDE, 4)]
          and m.calls == [(0x8007E6A8, 0x800526A8)] and m.bytes(row, STRIDE) == owner_bytes,
          "Exact 58-byte copy including class/private pointer precedes stopped initializer call")
    check(peek(m, row + 0x36, 2) == 0xAE and peek(m, row) == peek(m, CHEST)
          and m.r[17] == props and peek(m, props) == 79,
          "Current row is now AE but s1 still owns the original D properties")
    copied = copy.deepcopy(m)
    alternate_trace = [hx(pc) for pc in m.code.executed]

    # Separate explicit post-526A8 witness: copied class/private pointer and
    # s1/s4 are ENTRY CONDITIONS. No claim that GTE initializer returned these.
    # The empty-list case is a branch-boundary fixture; it does not claim that
    # this current row could belong to an actually empty placed-row interval.
    for empty in (False, True):
        m = copy.deepcopy(copied); clear(m)
        if empty: m.poke(END_GLOBAL, ROWS)
        before, registers = m.memory.copy(), m.r.copy()
        end = peek(m, END_GLOBAL)
        writes = [(row + 0x51, 1, 1)]
        if not empty:
            writes += [(PRIVATE + int(retained[i]["privateSceneOffset"], 16), 4, 118) for i in range(119, 123)]
        expected = registers.copy()
        changes = {2: 0, 3: 118 * 8 if empty else 79, 4: ROWS if empty else PRIVATE + 0xF03C,
                   7: 118, 16: end}
        if not empty: changes.update({5: end + 0x36, 6: end, 8: 0xD})
        for register, value in changes.items(): expected[register] = value
        stop = run(m, 0x8007E6B0, STOPS, 4096, "Explicit post-initializer " + ("empty-list reset" if empty else "bounded owner-link scan"))
        check(stop == RESET and m.r == expected and m.memory == expected_memory(before, writes)
              and m.writes == writes and m.calls == [] and peek(m, row + 0x36, 2) == 0xAE
              and peek(m, row) == peek(m, CHEST) and peek(m, props) == 79,
              "Declared post-call cut reaches reset only with copied AE class and exact scan complement")
        check((0x8007E708 in m.code.executed) and ((0x8007E774 in m.code.executed) != empty)
              and all(address != 0x80076378 + 0xD * 4 for address, _ in m.reads),
              "Both actual reset exits distinguished; no rootless D model read claimed")

    # Refusals test the witness against distinct ownership and delay failures.
    def mutated_tick(pc, value=None, remove=False):
        changed = fixture()
        if remove: del changed.code[pc]
        else: changed.code[pc] = value
        wait_tick(changed, True, "Deliberately changed native code")

    refuse(lambda: mutated_tick(0x800177BC, remove=True), "Unowned native instruction", "Truncated subtract return delay refused")
    refuse(lambda: mutated_tick(0x80017718, 0), "complete registers, memory complement", "Missing copy return-delay store detected")
    refuse(lambda: mutated_tick(0x800177A4, 0x00270821), "complete registers, memory complement", "Changed subtract arithmetic detected")
    refuse(lambda: mutated_tick(0x8007E604, 0x1440005D), "complete registers, memory complement", "Inverted +14 branch gate detected")

    def missing_owner():
        changed = fixture(); del changed.memory[CHEST + 0xC]
        changed.run(ENTRY, STOPS, maximum=256)

    def pointer_owner():
        changed = fixture(); changed.poke(peek(changed, changed.r[20]), CHEST)
        changed.run(ENTRY, STOPS, maximum=256)

    refuse(missing_owner, "Unmapped native read", "Unmapped actual owner vector refused")
    refuse(pointer_owner, "Unmapped native read", "Private owner must be an integer row index, not an address")
    refuse(lambda: fixture().run(ENTRY, STOPS, maximum=1), "instruction budget exhausted", "Finite execution budget enforced")
    refuse(lambda: fixture(78), "Only actual retained D rows", "AD key is not admitted through reward entry")

    return {"marker": "FRESH_CHEST_REWARD_PRECONDITION_WITNESS_OK", "checkCount": len(checks), "checks": checks,
            "refusalCount": len(refusals), "refusals": refusals, "sourceWindows": source_windows,
            "retainedFamily": family, "scalarRuns": traces, "firstRetainedEntryTrace": first_trace,
            "alternateCopyTrace": alternate_trace,
            "executedHelperBodies": [{"pc": "80017700", "byteLength": 0x1C}, {"pc": hx(SUB_ENTRY), "byteLength": SUB_BYTES}],
            "entryConditions": ["Actual seven retained rows/private blocks; only declared row-pointer relocation",
                                "s4 names the selected actual D row; owner index remains79 and owner row is active",
                                "First entry private10=0/private14=0/private18=1; row49=0 and timer1C=0",
                                "Repeated waiting ticks retain private14/18; one explicit owner-position perturbation",
                                "Post-delete E660 and post-initializer E6B0 cuts are separate synthetic entry conditions",
                                "Empty-list post-call cut is a branch boundary, not a valid placed-row membership claim",
                                "Non-family rows in the bounded later scan are synthetic inactive A5 rows"],
            "retainedWaitingPathResetUnreachable": True, "wholeHandlerLifecycleProven": False,
            "arbitraryHelperPrivateStatePreservationProven": False, "wholeAlternatePathExecuted": False,
            "initializer526A8Executed": False, "resetBodyExecuted": False, "actualResetModelChainProven": False,
            "outerClassDispatchExecuted": False, "currentFreshExecutableRead": False,
            "freshHostExtensionAdmitted": False, "runtime": False, "publication": False}


def main(path):
    require(__debug__, "Python assertions must remain enabled")
    inspected = frozen_inspector(); preconditions = inspected.load_preconditions(); prior = preconditions.load_continuation()
    prior.require_helpers()
    raw = prior.load(next(iter(prior.HELPERS)), "reward_witness_original_reader")
    host = prior.load(list(prior.HELPERS)[1], "reward_witness_frozen_host")

    def guards():
        require(sha((ROOT / INSPECTOR).read_bytes()) == INSPECTOR_SHA, "Frozen 443 inspector changed")
        inspected.guard_frozen(); prior.require_helpers(); host.require_helpers()

    guards()
    specs = {**prior.OWNERS, "peace-data": preconditions.EXTRA_OWNERS["peace-data"]}
    with open(path, "rb") as image:
        initial = inspected.file_identity(os.fstat(image.fileno()))

        def held_and_named():
            require(inspected.file_identity(os.fstat(image.fileno())) == initial
                    and inspected.file_identity(os.stat(path)) == initial, "Held and named original identity")

        held_and_named(); raw.identity(image)
        try:
            directory = raw.logical(image, 37 * 2048, 0x800)
            require(sha(directory) == "15d3e9c45f07e27a0ddd3f3a49b23a077e2c7750021cc89c55b7b16c551186cd", "Original WAD directory")
            for index, pair in ((15, (0x118E800, 0xF800)), (21, (0x187E000, 0x12800)), (22, (0x1890800, 0x29E800))):
                require(struct.unpack_from("<II", directory, index * 8) == pair, "Exact retained owner directory entry")
            owners = {name: raw.logical(image, offset, length) for name, (offset, length, _) in specs.items()}
            for name, (_, length, digest) in specs.items():
                require(len(owners[name]) == length and sha(owners[name]) == digest, "Whole retained owner: " + name)
            report = witness(owners, inspected, preconditions, prior, host)
            report.update(sourceImageSha256=raw.IMAGE_SHA, frozenInspectorSha256=INSPECTOR_SHA,
                          frozenPreconditionsSha256=PRECONDITIONS_SHA, frozenContinuationSha256=CONTINUATION_SHA,
                          owners=[{"id": name, "logicalOffset": hx(offset), "byteLength": length, "sha256": digest}
                                  for name, (offset, length, digest) in specs.items()])
        finally:
            raw.identity(image); held_and_named(); guards()
    with open(path, "rb") as image:
        require(inspected.file_identity(os.fstat(image.fileno())) == initial, "Reopened named original identity")
        raw.identity(image)
        require(inspected.file_identity(os.fstat(image.fileno())) == initial
                and inspected.file_identity(os.stat(path)) == initial, "Final held and named original identity")
    guards()
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("original_image")
    parser.add_argument("--witness", action="store_true", required=True)
    arguments = parser.parse_args()
    main(arguments.original_image)
