#!/usr/bin/env python3
"""Original-only chest continuation inspection and bounded scalar witness.

No Core invocation, output image, emulator, socket, injected call or helper
substitute. --inspect reports bounded original instruction windows; reported
lookahead extents are not certified basic blocks or current-Fresh-EXE owners.
--witness executes source words and explicit test-only continuation recipes on
synthetic owned RAM. Whole handlers and native helper bodies are NOT executed.
Recipe PCs are NOT an approved Fresh overlay allocation or runtime acceptance.
"""
import argparse
import copy
import hashlib
import importlib.util
import json
import struct
import sys
from pathlib import Path

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[2]
BASE, EXE_BASE = 0x8007AA38, 0x8000F800
HELPERS = {
    "src/Spyro.Editor.NativeWizardFamilyAssetSmoke/source_wizard_family_asset_oracle.py":
        "e92cfc1a4c53611cfe11ccc87931dec71ce6b4c254c34a9d6a74bf56caef2a43",
    "src/Spyro.Editor.NativeFreshWizardHostSmoke/source_fresh_wizard_host_abi_oracle.py":
        "e6032f4ff386cd060c056919cbed52629fadfe6efbd9257a72a77be6dae41800",
}
OWNERS = {
    "town": (37 * 2048 + 0x118E800, 0xF800, "9bc923cc8d27703537b81b01f51fde351e58aabee87d38a8afc630f1031878a5"),
    "peace": (37 * 2048 + 0x187E000, 0x12800, "299f4822489c48736c9f46bb9556e82952fc1693f5f9f42bc2e6415c1908aebc"),
    "exe": (53875 * 2048, 0x66000, "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998"),
}
HANDLERS = (
    ("setup", 0x80083BD4, 0x30, "9343243adf185b81f638f37a41be77c61d27508c45158fed1ebe0b9fc6dd97ad"),
    ("state", 0x80083D68, 0x70C, "83c507315fcf747b0c94c6bd47c7622e79f38c68ff746421fd9b2021314a479f"),
    ("reward", 0x8007E59C, 0x434, "129d8511c63925af80ab73a02def446efc39e23974255fd4b8d38f51411c2056"),
)
RESET_HASH = "23aabbf138f06867c52f9a0c903236b5acf57bc39db7764c174b7d3d97c35dff"
EXTERNAL_ROLES = {0x8008A20C: "loop", 0x80088CF4: "reset", 0x8008A204: "delete", 0x800880FC: "sound"}
TOWN_RESET, DONOR_RESET = 0x800858F8, 0x80088CF4
TOWN_LOOP, DONOR_LOOP = 0x80087258, 0x8008A20C
# Exact entry blocks discovered in original-only inspection 421, NOT extents
# inferred from the historical overlapping 0x40-byte reset guard.
RESET_PINS = {
    "town": "92110f34196989ab6d65d9c8297881cd21b802c95d7620d230d81aafa91421a7",
    "peace": "d7207348e071b530b2c6bbd3ed59166d5d9c45f27e8565da8bacbb23d7786990",
}
TOWN_RESET_WORDS = (
    0x86620036, 0x3C098007, 0x25296378, 0xA2600048, 0xA2600040,
    0x00021080, 0x00491021, 0x8C420000, 0, 0x8C420038, 0, 0x9043000C,
    0x24020001, 0xA260003C, 0xA260003D, 0xA260003E, 0xA262003F,
    0x08021C96, 0xA2630041,
)
DONOR_RESET_WORDS = (
    0x86820036, 0x3C098007, 0x25296378, 0xA2800048, 0xA2800040,
    0x00021080, 0x00491021, 0x8C420000, 0, 0x8C420038, 0, 0x9043000C,
    0x24020001, 0xA280003C, 0xA280003D, 0xA280003E, 0xA282003F,
    0x08022883, 0xA2830041,
)


def sha(data): return hashlib.sha256(data).hexdigest()
def hx(value): return f"{value:08X}"
def word(data, at=0): return struct.unpack_from("<I", data, at)[0]
def signed16(value): return (value & 65535) - (65536 if value & 32768 else 0)


def require(ok, label):
    if not ok: raise AssertionError(label)


def require_helpers():
    for relative, digest in HELPERS.items():
        require(sha((ROOT / relative).read_bytes()) == digest, "Pinned original-only helper: " + relative)


def load(relative, name):
    spec = importlib.util.spec_from_file_location(name, ROOT / relative)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def extent(owners, owner, pc, length):
    basis = EXE_BASE if owner == "exe" else BASE
    at = pc - basis
    require(length > 0 and length % 4 == 0 and pc % 4 == 0 and 0 <= at <= len(owners[owner]) - length,
            "Bounded original instruction window")
    return owners[owner][at:at + length]


def transfer(pc, instruction):
    op = instruction >> 26
    if op in (2, 3):
        return ("call" if op == 3 else "jump", ((pc + 4) & 0xF0000000) | ((instruction & 0x3FFFFFF) << 2))
    if op in (1, 4, 5, 6, 7, 0x14, 0x15, 0x16, 0x17):
        return "branch", (pc + 4 + signed16(instruction) * 4) & 0xFFFFFFFF
    if op == 0 and instruction & 63 in (8, 9):
        return "register-return-or-call", None
    return None


def decoded(pc, instruction):
    result = {"pc": hx(pc), "word": hx(instruction), "op": instruction >> 26,
              "rs": instruction >> 21 & 31, "rt": instruction >> 16 & 31,
              "rd": instruction >> 11 & 31, "signedImmediate": signed16(instruction)}
    control = transfer(pc, instruction)
    if control: result.update(kind=control[0], target=hx(control[1]) if control[1] is not None else None)
    return result


def inspect_window(owners, owner, pc, length, label, expected=None, instruction_limit=None):
    data = extent(owners, owner, pc, length)
    if expected is not None: require(sha(data) == expected, "Existing source window: " + label)
    printed = length if instruction_limit is None else min(length, instruction_limit)
    return {"label": label, "owner": owner, "pc": hx(pc), "byteLength": length, "sha256": sha(data),
            "previouslyPinned": expected is not None, "instructionBytesReported": printed,
            "instructions": [decoded(pc + at, word(data, at)) for at in range(0, printed, 4)]}


def inspect_reset_block(owners, owner, entry):
    # The first inspection ended in a pending LBU, not a complete continuation.
    # Discover only the linear entry block through its FIRST actual control and
    # delay word; neither a guessed span nor its outgoing destination is executed.
    for at in range(0, 0x100, 4):
        instruction = word(extent(owners, owner, entry + at, 4))
        edge = transfer(entry + at, instruction)
        if edge is not None:
            result = inspect_window(owners, owner, entry, at + 8, "Reset entry through first actual control and delay")
            result.update(firstControlKind=edge[0], firstControlTarget=hx(edge[1]) if edge[1] is not None else None,
                          targetExecuted=False, discoveryInstructionBound=64)
            return result
    raise AssertionError("Reset inspection reached its finite bound without a native control boundary")


def inspection(owners, host):
    controls = []
    for label, owner, pc, length, digest in host.SPANS:
        if owner not in ("town", "town-data"): continue
        actual = extent(owners, "town", pc, length)
        require(sha(actual) == digest, "Unchanged original Host control: " + label)
        controls.append({"label": label, "pc": hx(pc), "byteLength": length, "sha256": digest})
    windows = [
        inspect_window(owners, "town", 0x800858E8, 0x40, "Town reset historical guard", RESET_HASH),
        inspect_window(owners, "town", 0x80087258, 0x4C, "Town loop and complete outer epilogue",
                       "829a0bc3b1a7a93ee100386cb5d7f16eef6fc2737e1044249ef9fba760abfafd"),
        # These are inspection lookaheads, NOT guessed executable block lengths.
        inspect_window(owners, "peace", 0x80088CE4, 0x40, "Donor reset contextual lookahead"),
        inspect_window(owners, "peace", 0x8008A204, 0x10, "Donor delete and loop entry lookahead"),
        inspect_window(owners, "peace", 0x800880FC, 0x10, "Donor sound entry lookahead"),
        inspect_window(owners, "exe", 0x80052568, 0x64, "Placed-row delete existing original witness",
                       "602fc31b1351212e5d5c72c39ac59cc86a9afd0226a9a0549192855588498505"),
        inspect_window(owners, "exe", 0x80055A78, 0x82C, "PlaySound retained source extent, body not executed",
                       instruction_limit=0x20),
    ]
    exits, calls, stack, handler_reports, unclassified = [], {}, [], [], []
    for name, pc, length, digest in HANDLERS:
        data = extent(owners, "peace", pc, length)
        require(sha(data) == digest, "Exact retained chest handler: " + name)
        handler_reports.append({"id": name, "pc": hx(pc), "byteLength": length, "sha256": digest})
        for at in range(0, length, 4):
            instruction = word(data, at); site = pc + at
            op, rs, rt = instruction >> 26, instruction >> 21 & 31, instruction >> 16 & 31
            if rs == 29 and (op >= 0x20 or op == 9 and rt == 29):
                stack.append({"handler": name, **decoded(site, instruction)})
            control = transfer(site, instruction)
            if control is None: continue
            kind, target = control
            delay = hx(word(data, at + 4)) if at + 4 < length else None
            if target in EXTERNAL_ROLES:
                # Capture actual producer arguments immediately before transfer;
                # no assertion yet that these two preceding words are a CFG root.
                start = max(0, at - 8)
                exits.append({"handler": name, "role": EXTERNAL_ROLES[target], "pc": hx(site),
                              "kind": kind, "target": hx(target), "delayWord": delay,
                              "context": [decoded(pc + k, word(data, k)) for k in range(start, min(length, at + 8), 4)]})
            elif kind == "call" and target is not None and not any(p <= target < p + n for _, p, n, _ in HANDLERS):
                require(EXE_BASE <= target < EXE_BASE + len(owners["exe"]), "Unclassified donor call outside original EXE")
                calls.setdefault(target, []).append({"handler": name, "pc": hx(site), "delayWord": delay})
            elif target is None or not any(p <= target < p + n for _, p, n, _ in HANDLERS):
                unclassified.append({"handler": name, "pc": hx(site), "kind": kind,
                                     "target": hx(target) if target is not None else None, "delayWord": delay})
    return {"marker": "FRESH_CHEST_CONTINUATION_INSPECTION_OK", "sourceImageSha256":
            "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37",
            "originalHostControls": controls, "windows": windows, "handlers": handler_reports,
            "resetEntryBlocksPendingScalarReview": [inspect_reset_block(owners, "town", 0x800858F8),
                                                    inspect_reset_block(owners, "peace", 0x80088CF4)],
            "declaredContinuationSites": exits, "nativeStackOperands": stack,
            "unclassifiedOutgoingEdgesNotExecuted": unclassified,
            "retainedOriginalCallsRequiringCurrentExecutableBinding": [
                {"target": hx(target), "sites": sites, "currentExecutableMatched": False,
                 "entryInspection": inspect_window(owners, "exe", target, 0x10, "Entry lookahead, not a routine certificate")}
                for target, sites in sorted(calls.items())],
            "resetScalarWitnessEstablished": False, "currentFreshExecutableRead": False,
            "freshOverlayEmitted": False, "runtime": False, "publication": False}


def scalar_witness(owners, host, inspected):
    checks, refusals, windows, code = [], [], [], {}

    def check(ok, label):
        require(ok, label)
        checks.append(label)

    def refuse(action, expected, label):
        try:
            action()
        except AssertionError as error:
            require(expected in str(error), label + ": wrong failure: " + str(error))
            refusals.append(label)
            return
        raise AssertionError(label + ": negative control was accepted")

    def source(owner, pc, length, label, literal=None, digest=None):
        data = extent(owners, owner, pc, length)
        if literal is not None: require(data == host.pack(literal), "Inspected native words: " + label)
        if digest is not None: require(sha(data) == digest, "Inspected native span: " + label)
        added = host.words(data, pc)
        require(not set(added).intersection(code), "Executable source-window overlap")
        code.update(added)
        windows.append({"label": label, "owner": owner, "pc": hx(pc), "byteLength": length,
                        "sha256": sha(data), "wholeOwnerPinned": True})
        return data

    # Only the real prologue/list setup and loop are executable Town controls.
    # town_main stops before the unexecuted visibility helper, then uses a
    # separately declared post-visibility entry with the pinned helper's list.
    source("town", 0x8007DA54, 0x178, "Town prologue and actor-list setup",
           digest="3390151d01a3c026f6115a51b16b92414bf7dc3af8dbbf8e38717eaac3942f7c")
    source("town", TOWN_LOOP, 0x4C, "Town next actor and complete outer return",
           digest="829a0bc3b1a7a93ee100386cb5d7f16eef6fc2737e1044249ef9fba760abfafd")
    town_reset = source("town", TOWN_RESET, 0x4C, "Town complete reset entry", TOWN_RESET_WORDS, RESET_PINS["town"])
    donor_reset = source("peace", DONOR_RESET, 0x4C, "Donor complete reset entry", DONOR_RESET_WORDS, RESET_PINS["peace"])
    source("peace", 0x8008A204, 0x10, "Delete call and donor cursor load only",
           (0x0C01495A, 0, 0x8FA901E8, 0), "d906259214dda675acd54ca4f753d68ccb4d2bb2a323ea19774cc4e0f65aaf04")
    source("peace", 0x800880FC, 0x10, "Sound call and donor continuation",
           (0x0C01569E, 0x24A70054, 0x08022883, 0), "198beb4de171f0d2e8356c4662ffe4fa96f59d0e1a5bb8db405b32faf1437187")
    source("peace", 0x8007E704, 0xC, "Reset unsigned comparison and branch delay",
           (0x0204102B, 0x1040297A, 0x000338C3))
    source("peace", 0x8007E76C, 0x10, "Reset list-loop branch and unconditional exit",
           (0x1440FFEB, 0x24A50058, 0x0802233D, 0))
    source("peace", 0x8007E7B0, 8, "Reward result loop branch only", (0x10402E96, 0))
    source("peace", 0x8007E8B8, 0x10, "Reward direct sound call and delete producer",
           (0x0C01569E, 0x26870054, 0x08022881, 0x02802021))
    source("peace", 0x8007E8E0, 0xC, "Reward range comparison and loop delay",
           (0x2C420080, 0x14402E49, 0x24020009))
    source("peace", 0x8007E978, 0x10, "Reward property load and loop delay",
           (0x8E220020, 0x24090001, 0x14492E22, 0x24090002))
    source("peace", 0x8007E9C4, 0xC, "Reward sound index and arguments",
           (0x90840037, 0x0802203F, 0x24060008))
    source("peace", 0x80084464, 0x10, "State terminal stores and loop delay",
           (0x02802021, 0xAE420014, 0x08022883, 0xAE800018))

    # No bytes are installed in an output overlay. These PCs are independent
    # scalar-test owners and deliberately do not assert a Fresh arena address.
    shim_pc, reset_pc, delete_pc, sound_pc, wrong_leave_pc = (
        0x80190000, 0x80190100, 0x80190200, 0x80190220, 0x80190300)
    recipes = {
        shim_pc: host.pack((0x0260A021,)),  # s4 = Town s3; no private frame.
        reset_pc: donor_reset[:0x44] + host.pack((0x08021C96,)) + donor_reset[0x48:],
        delete_pc: host.pack((0x0C01495A, 0x02602021, 0x08021C96, 0)),
        sound_pc: host.pack((0x0C01569E, 0x24A70054, 0x08021C96, 0)),
    }
    for pc, data in recipes.items():
        require(not set(host.words(data, pc)).intersection(code), "Test recipe owner overlap")
        code.update(host.words(data, pc))
    code.update(host.words(host.leave(TOWN_LOOP), wrong_leave_pc))

    def town():
        machine, original = host.town_main(code)
        machine.writes.clear(); machine.reads.clear(); machine.calls.clear()
        return machine, original

    def unchanged(machine, before, changed, label):
        check(all(machine.r[r] == before[r] for r in range(32) if r not in changed), label)

    # Interpreter controls apply to both pinned engines used below. The smaller
    # engine executes the real SLTU producer; it is not extended or stubbed.
    for engine in (host.Machine, host.native.Scalar):
        micro = engine({0x100: 0x8C820000, 0x104: 0x00401821, 0x108: 0x00402821,
                        0x10C: 0x10000002, 0x110: 0x24060007, 0x114: 0x24060063})
        micro.map(0x1000, 4); micro.poke(0x1000, 42)
        micro.r[4], micro.r[2] = 0x1000, 9
        check(micro.run(0x100, {0x118}) == 0x118 and micro.r[2:4] == [42, 9]
              and micro.r[5:7] == [42, 7], engine.__name__ + " load and taken-branch delay control")

    m, original = town()
    before, memory, hilo = m.r.copy(), m.memory.copy(), (m.hi, m.lo)
    m.run(shim_pc, {shim_pc + 4})
    check(m.r[20] == host.ROW and m.memory == memory and (m.hi, m.lo) == hilo,
          "Chest row shim owns only s4, with no Wizard private frame or memory write")
    unchanged(m, before, {20}, "Chest row shim preserves every other register including sp and ra")
    cursor = m.r[29] + 0x180
    expected = memory.copy()
    for i, value in enumerate(struct.pack("<I", 0x800700FC)): expected[cursor + i] = value
    check(m.run(TOWN_LOOP, {0x8007DAEC, host.RETURN}) == 0x8007DAEC
          and m.r[19] == host.ROW + 0x58 and m.r[29] == host.STACK - 0x1D0
          and m.memory == expected and m.writes == [(cursor, 4, 0x800700FC)],
          "Town taken loop advances exactly its sp+180 cursor and keeps the outer frame")
    m.run(0x8007DAEC, {0x8007DB34})
    check(m.r[19] == host.ROW + 0x58 and m.r[4] == 0x78,
          "Actual Town next-actor setup reaches the existing class dispatch")
    before_memory = m.memory.copy(); m.writes.clear()
    check(m.run(TOWN_LOOP, {host.RETURN}) == host.RETURN
          and all(m.r[r] == original[r] for r in list(range(16, 24)) + [29, 30, 31])
          and (m.hi, m.lo) == hilo, "Town null-list path restores the complete saved register set and 1D0 frame")
    for i, value in enumerate(struct.pack("<I", 0x80070100)): before_memory[cursor + i] = value
    check(m.memory == before_memory and m.writes == [(cursor, 4, 0x80070100)],
          "Town terminal loop preserves all memory except its one cursor store")

    m, _ = town()
    m.poke(m.r[29] + 0x1E8, 0x80170000)
    donor_cursor = copy.deepcopy(m)
    donor_cursor.run(DONOR_LOOP, {DONOR_LOOP + 8})
    m.run(TOWN_LOOP, {TOWN_LOOP + 8})
    check(donor_cursor.r[9] == 0x80170000 and m.r[9] == 0x800700F8,
          "Negative control: donor sp+1E8 cursor is not the Town sp+180 cursor")
    bad_frame, _ = town()
    refuse(lambda: bad_frame.run(wrong_leave_pc, {TOWN_LOOP}), "Unmapped native read",
           "Wizard 2B0-frame restore cannot be borrowed by an unframed chest continuation")

    # The literal blocks differ in eight row operands plus their terminal J.
    # Normalization is an instruction identity check, not a byte-equality claim.
    normalized = list(DONOR_RESET_WORDS)
    for index in (0, 3, 4, 13, 14, 15, 16, 18):
        normalized[index] = (normalized[index] & ~(31 << 21)) | (19 << 21)
    normalized[17] = 0x08021C96
    check(town_reset != donor_reset and host.pack(normalized) == town_reset,
          "Complete reset blocks differ only in explicit s3/s4 operands and owned continuation")

    table, model, animation = 0x80076378, 0x80150000, 0x80150100

    def reset_machine(animation_byte=7):
        machine, _ = town()
        machine.r[19] = machine.r[20] = host.ROW
        for at in (host.ROW, host.ROW + 0x58):
            for offset in range(0x58): machine.poke(at + offset, 0x6B, 1)
            machine.poke(at + 0x36, 0xAE, 2)
        machine.map(table + 0xAE * 4, 4); machine.poke(table + 0xAE * 4, model)
        machine.map(model, 0x3C); machine.poke(model + 0x38, animation)
        machine.map(animation, 0xD); machine.poke(animation + 0xC, animation_byte, 1)
        return machine

    for value in (0, 7, 255):
        initial = reset_machine(value)
        results = []
        writes = [(host.ROW + offset, 1, byte) for offset, byte in
                  ((0x48, 0), (0x40, 0), (0x3C, 0), (0x3D, 0), (0x3E, 0), (0x3F, 1), (0x41, value))]
        expected_memory = initial.memory.copy()
        for at, _, byte in writes: expected_memory[at] = byte
        expected_registers = initial.r.copy()
        expected_registers[2], expected_registers[3], expected_registers[9] = 1, value, table
        for label, entry, stop in (("Town", TOWN_RESET, TOWN_LOOP), ("Donor", DONOR_RESET, DONOR_LOOP),
                                   ("Chest-specific recipe", reset_pc, TOWN_LOOP)):
            machine = copy.deepcopy(initial)
            check(machine.run(entry, {stop}) == stop and machine.r == expected_registers
                  and machine.memory == expected_memory and machine.writes == writes
                  and machine.reads == [(host.ROW + 0x36, 2), (table + 0xAE * 4, 4), (model + 0x38, 4), (animation + 0xC, 1)]
                  and machine.calls == [] and (machine.hi, machine.lo) == (initial.hi, initial.lo),
                  label + " complete reset effects, read owners, register/stack/complement at byte " + str(value))
            results.append(machine)
        check(results[0].r == results[1].r == results[2].r and results[0].memory == results[1].memory == results[2].memory,
              "Reset native and rebased-recipe equivalence requires s3=s4, byte " + str(value))

    initial = reset_machine(); initial.r[20] += 0x58
    town_only, donor_only = copy.deepcopy(initial), copy.deepcopy(initial)
    town_only.run(TOWN_RESET, {TOWN_LOOP}); donor_only.run(DONOR_RESET, {DONOR_LOOP})
    check(town_only.memory != donor_only.memory and town_only.bytes(host.ROW + 0x58, 0x58) == initial.bytes(host.ROW + 0x58, 0x58)
          and donor_only.bytes(host.ROW, 0x58) == initial.bytes(host.ROW, 0x58),
          "Negative control: unequal s3/s4 resets different rows, not an interchangeable source target")
    missing_model = reset_machine(); missing_model.poke(table + 0xAE * 4, 0)
    refuse(lambda: missing_model.run(TOWN_RESET, {TOWN_LOOP}), "Unmapped native read 00000038",
           "Reset does not certify a rootless or absent model/animation owner")
    missing_delay = reset_machine(); del missing_delay.code[TOWN_RESET + 0x48]
    refuse(lambda: missing_delay.run(TOWN_RESET, {TOWN_LOOP}), "Unowned instruction",
           "Reset continuation cannot omit the native row+41 branch-delay store")
    bad_load = reset_machine(); bad_load.code[TOWN_RESET + 0x28] = 0x24020000
    refuse(lambda: bad_load.run(TOWN_RESET, {TOWN_LOOP}), "Unmapped native read 0000000C",
           "Reset load-delay overwrite cancels the animation pointer and cannot pass")

    # Every external continuation site has its own explicit entry-register
    # witness. No synthetic return value is attributed to the preceding helper.
    for left, right, signed_value in ((5, 5, 0xFFFFFFF8), (4, 5, 0x20), (0xFFFFFFFF, 1, 0x80000000)):
        seed, _ = town()
        machine = host.native.Scalar(dict(code)); machine.r = seed.r.copy(); machine.memory = seed.memory.copy()
        machine.r[16], machine.r[4], machine.r[3] = left, right, signed_value
        before = machine.r.copy(); memory = machine.memory.copy()
        stop = DONOR_RESET if left >= right else 0x8007E710
        check(machine.run(0x8007E704, {DONOR_RESET, 0x8007E710}) == stop
              and machine.r[2] == int(left < right)
              and machine.r[7] == (host.native.signed(signed_value) >> 3) & 0xFFFFFFFF
              and machine.memory == memory and not machine.calls,
              "Reset SLTU branch uses unsigned inputs and executes signed-shift delay " + str((left, right)))
        unchanged(machine, before, {2, 7}, "Reset comparison preserves all other inputs " + str((left, right)))
    for condition in (0, 1):
        machine, _ = town(); machine.r[2], machine.r[5] = condition, host.ROW
        before, memory = machine.r.copy(), machine.memory.copy()
        stop = DONOR_RESET if condition == 0 else 0x8007E71C
        check(machine.run(0x8007E76C, {DONOR_RESET, 0x8007E71C}) == stop
              and machine.r[5] == host.ROW + 0x58 and machine.memory == memory and not machine.calls,
              "Reset list exit/continue executes row-stride delay " + str(condition))
        unchanged(machine, before, {5}, "Reset list branch keeps all non-cursor registers " + str(condition))
    for condition in (0, 1):
        machine, _ = town(); machine.r[2] = condition
        before, memory = machine.r.copy(), machine.memory.copy()
        check(machine.run(0x8007E7B0, {DONOR_LOOP, 0x8007E7B8}) == (DONOR_LOOP if condition == 0 else 0x8007E7B8)
              and machine.r == before and machine.memory == memory and not machine.calls,
              "Reward result loop branch with explicit, not synthesized helper result " + str(condition))
    for distance in (0x7F, 0x80, 0xFFFFFFFF):
        machine, _ = town(); machine.r[2] = distance
        before, memory = machine.r.copy(), machine.memory.copy()
        check(machine.run(0x8007E8E0, {DONOR_LOOP, 0x8007E8EC}) == (DONOR_LOOP if distance < 0x80 else 0x8007E8EC)
              and machine.r[2] == 9 and machine.memory == memory and not machine.calls,
              "Range branch decides before its v0=9 delay " + hx(distance))
        unchanged(machine, before, {2}, "Range branch complete unchanged-register complement " + hx(distance))
    for prop in (0, 1, 2):
        machine, _ = town(); machine.r[17] = host.ROW; machine.poke(host.ROW + 0x20, prop)
        before, memory = machine.r.copy(), machine.memory.copy()
        check(machine.run(0x8007E978, {DONOR_LOOP, 0x8007E988}) == (DONOR_LOOP if prop != 1 else 0x8007E988)
              and (machine.r[2], machine.r[9]) == (prop, 2) and machine.memory == memory and not machine.calls,
              "Property load retires before comparison; t1=2 is a delay, not branch input " + str(prop))
        unchanged(machine, before, {2, 9}, "Property branch complete unchanged-register complement " + str(prop))
    machine, _ = town(); machine.r[20], machine.r[18], machine.r[2] = host.ROW, host.ROW + 0x58, 0x12345678
    before, memory = machine.r.copy(), machine.memory.copy()
    for at, value in ((host.ROW + 0x58 + 0x14, 0x12345678), (host.ROW + 0x18, 0)):
        for i, byte in enumerate(struct.pack("<I", value)): memory[at + i] = byte
    check(machine.run(0x80084464, {DONOR_LOOP}) == DONOR_LOOP and machine.r[4] == host.ROW and machine.memory == memory
          and machine.writes == [(host.ROW + 0x58 + 0x14, 4, 0x12345678), (host.ROW + 0x18, 4, 0)],
          "State loop transfer retains both native stores including the row+18 delay")
    unchanged(machine, before, {4}, "State terminal stores preserve frame, return and all other registers")

    # Calls stop BEFORE executing the EXE routine. These assert the real call
    # arguments, link address and delay word, not audio/deletion/helper semantics.
    direct, _ = town(); direct.r[20] = host.ROW
    before, memory = direct.r.copy(), direct.memory.copy()
    check(direct.run(0x8007E8B8, {0x80055A78}) == 0x80055A78 and direct.r[7] == host.ROW + 0x54
          and direct.r[31] == 0x8007E8C0 and direct.calls == [(0x8007E8B8, 0x80055A78)] and direct.memory == memory,
          "Reward direct sound call uses s4+54 in the actual JAL delay")
    unchanged(direct, before, {7, 31}, "Direct sound entry preserves all non-a3/ra registers")
    initial, _ = town(); initial.r[19] = initial.r[20] = host.ROW
    native_delete, recipe_delete = copy.deepcopy(initial), copy.deepcopy(initial)
    native_delete.run(0x8007E8C0, {0x80052568}); recipe_delete.run(delete_pc, {0x80052568})
    check(native_delete.r[4] == recipe_delete.r[4] == host.ROW and native_delete.r[31] == DONOR_LOOP
          and recipe_delete.r[31] == delete_pc + 8
          and native_delete.calls == [(0x8008A204, 0x80052568)] and recipe_delete.calls == [(delete_pc, 0x80052568)]
          and native_delete.memory == recipe_delete.memory == initial.memory,
          "Delete producer and chest-specific thunk reach the real EXE entry with actual row argument/link")
    unchanged(native_delete, initial.r, {4, 31}, "Native delete argument/return are the only register changes")
    unchanged(recipe_delete, initial.r, {4, 31}, "Chest delete thunk does not consume a Wizard save frame")
    wrong_row = copy.deepcopy(initial); wrong_row.r[20] += 0x58
    wrong_native, wrong_recipe = copy.deepcopy(wrong_row), copy.deepcopy(wrong_row)
    wrong_native.run(0x8007E8C0, {0x80052568}); wrong_recipe.run(delete_pc, {0x80052568})
    check(wrong_native.r[4] != wrong_recipe.r[4], "Negative control: native delete s4 and thunk s3 require the declared row mapping")

    for index in (0, 54, 255):
        initial, _ = town(); initial.r[4], initial.r[5] = host.ROW, host.ROW + 0x58
        initial.poke(host.ROW + 0x37, index, 1)
        native_sound, recipe_sound = copy.deepcopy(initial), copy.deepcopy(initial)
        # Retarget only the actual producer J to the test-only thunk. Its LBU,
        # argument delay and all of the original sound JAL/delay remain exact.
        recipe_sound.code[0x8007E9C8] = host.jump(sound_pc)
        native_sound.run(0x8007E9C4, {0x80055A78}); recipe_sound.run(0x8007E9C4, {0x80055A78})
        check(native_sound.r[4:8] == recipe_sound.r[4:8] == [index, host.ROW + 0x58, 8, host.ROW + 0xAC]
              and native_sound.r[31] == 0x80088104 and recipe_sound.r[31] == sound_pc + 8
              and native_sound.calls == [(0x800880FC, 0x80055A78)] and recipe_sound.calls == [(sound_pc, 0x80055A78)]
              and native_sound.memory == recipe_sound.memory == initial.memory,
              "Sound producer load, a2 delay and a1+54 JAL delay agree at actual EXE entry " + str(index))
        unchanged(native_sound, initial.r, {4, 6, 7, 31}, "Native sound preserves every other register " + str(index))
        unchanged(recipe_sound, initial.r, {4, 6, 7, 31}, "Chest sound thunk preserves every other register " + str(index))
    for pc in (delete_pc + 8, sound_pc + 8):
        machine, _ = town(); before, memory = machine.r.copy(), machine.memory.copy()
        check(machine.run(pc, {TOWN_LOOP}) == TOWN_LOOP and machine.r == before and machine.memory == memory and not machine.calls,
              "Separate post-call-entry witness returns to Town without stack restoration " + hx(pc))
    native_tail, _ = town(); before, memory = native_tail.r.copy(), native_tail.memory.copy()
    check(native_tail.run(0x80088104, {DONOR_LOOP}) == DONOR_LOOP and native_tail.r == before and native_tail.memory == memory,
          "Original sound post-call entry goes to donor loop, not Town; helper return remains unexecuted")

    direct_stack = [(int(row["pc"], 16), int(row["word"], 16)) for row in inspected["nativeStackOperands"]]
    check(direct_stack == [(0x80084020, 0xAFA20100), (0x80084028, 0xAFA00104), (0x80084030, 0xAFA20108),
          (0x8008405C, 0x8FA20108), (0x8008406C, 0xAFA20108), (0x800840A4, 0x8FA60108),
          (0x800840B0, 0xAFA60108), (0x800840FC, 0xAFA60108), (0x80084228, 0xAFA20118),
          (0x80084230, 0xAFA00114), (0x80084244, 0xAFA20110)],
          "Exact direct-sp scratch operands are below Town cursor, not proof of helper or aliased scratch ownership")
    check([(row["pc"], row["role"]) for row in inspected["declaredContinuationSites"]] == [
          ("8008446C", "loop"), ("8007E708", "reset"), ("8007E774", "reset"), ("8007E7B0", "loop"),
          ("8007E8C0", "delete"), ("8007E8E4", "loop"), ("8007E980", "loop"), ("8007E9C8", "sound")],
          "Every declared continuation producer has a bounded native scalar case")
    requirements = []
    for role, pc, length, digest in (
        ("delete", 0x80052568, 0x64, "602fc31b1351212e5d5c72c39ac59cc86a9afd0226a9a0549192855588498505"),
        ("sound", 0x80055A78, 0x82C, "d3a63784c8dc509583c63499ccb7ad493ba8bf4af0aaeb4a6bd9fa5aa851a06d"),
    ):
        require(sha(extent(owners, "exe", pc, length)) == digest, "Exact original EXE continuation consumer: " + role)
        requirements.append({"role": role, "pc": hx(pc), "exeFileOffset": hx(pc - EXE_BASE),
                             "byteLength": length, "sha256": digest, "bodyExecuted": False,
                             "currentFreshExecutableMatched": False, "outgoingHelperClosureCertified": False})
    return {
        "marker": "FRESH_CHEST_CONTINUATION_SOURCE_OK", "sourceImageSha256": inspected["sourceImageSha256"],
        "checkCount": len(checks), "refusalCount": len(refusals),
        "checks": checks, "refusals": refusals, "scalarSourceWindows": windows,
        "originalHostControls": inspected["originalHostControls"], "handlers": inspected["handlers"],
        "testOnlyRecipes": [{"pc": hx(pc), "byteLength": len(data), "sha256": sha(data),
                             "words": [hx(word(data, at)) for at in range(0, len(data), 4)]} for pc, data in recipes.items()],
        "entryConditions": {"townFrameBytes": 0x1D0, "townCursorOffset": 0x180, "donorCursorOffset": 0x1E8,
                            "chestRowMapping": "s4=s3", "wizardPrivateFrameAllocated": False,
                            "resetClass": 0xAE, "syntheticModelTableSlot": hx(table + 0xAE * 4),
                            "syntheticModel": hx(model), "syntheticAnimation": hx(animation),
                            "actualRootlessKeyModelReadinessEstablished": False,
                            "postVisibilityAndPostCallEntriesAreSeparateWitnesses": True},
        "declaredContinuationSites": inspected["declaredContinuationSites"],
        "nativeDirectStackOperandsNotWholeStackProof": inspected["nativeStackOperands"],
        "unclassifiedOutgoingEdgesNotExecuted": inspected["unclassifiedOutgoingEdgesNotExecuted"],
        "retainedOriginalCallsRequiringCurrentExecutableBinding": inspected["retainedOriginalCallsRequiringCurrentExecutableBinding"],
        "narrowOriginalExecutableConsumersRequiringCurrentBinding": requirements,
        "pending": ["Actual current Fresh EXE bytes and all retained outgoing helper/callback owners",
                    "Whole chest/reward handler reachability, singleton/private state, aliased stack scratch and helper depth",
                    "Actual model/animation validity for every class using reset, especially rootless special-renderer classes",
                    "Source-issued Fresh append allocation after complete 0x11000 host and exact hook preimages",
                    "Seven-row family placement/initialization, persistence, renderer, audio and runtime acceptance"],
        "resetScalarWitnessEstablished": True, "wholeHandlerExecuted": False, "nativeHelperBodiesExecuted": False,
        "currentFreshExecutableRead": False, "freshOverlayEmitted": False, "runtime": False, "publication": False,
    }


def main(path, witness):
    require(__debug__, "Python assertions must remain enabled")
    require_helpers()
    raw = load(next(iter(HELPERS)), "chest_original_reader")
    # Import definitions and pins only. Never call host.capture/main: those run
    # a separate legacy candidate oracle unnecessary for this source inspection.
    host = load(list(HELPERS)[1], "chest_original_host_controls")
    host.require_helpers()
    with open(path, "rb") as image:
        raw.identity(image)
        try:
            directory = raw.logical(image, 37 * 2048, 0x800)
            require(sha(directory) == "15d3e9c45f07e27a0ddd3f3a49b23a077e2c7750021cc89c55b7b16c551186cd", "Complete original WAD directory")
            for index, pair in ((15, (0x118E800, 0xF800)), (21, (0x187E000, 0x12800))):
                require(struct.unpack_from("<II", directory, index * 8) == pair, "Original continuation overlay directory owner")
            owners = {name: raw.logical(image, offset, length) for name, (offset, length, _) in OWNERS.items()}
            for name, (_, _, digest) in OWNERS.items(): require(sha(owners[name]) == digest, "Complete original owner: " + name)
            report = inspection(owners, host)
            if witness: report = scalar_witness(owners, host, report)
        finally:
            raw.identity(image)
            require_helpers()
            host.require_helpers()
    # Reopen the path as well as rehashing the held descriptor. Identity must
    # still name the same exact original bytes after all bounded reads.
    with open(path, "rb") as image: raw.identity(image)
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("original_image")
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument("--inspect", action="store_true", help="Inspect bounded original continuation words only")
    mode.add_argument("--witness", action="store_true", help="Original scalar continuation cases; no current Fresh or runtime acceptance")
    arguments = parser.parse_args()
    main(arguments.original_image, arguments.witness)
