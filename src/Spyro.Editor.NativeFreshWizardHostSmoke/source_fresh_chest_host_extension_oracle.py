#!/usr/bin/env python3
"""Independent original-only detached Fresh chest Host reference.

Reconstructs the frozen complete Wizard Host/current Family EXE, appends the
retained chest behavior, and exercises bounded scalar paths. No Core producer,
candidate input, output file, model/row issuance, emulator or publication.
Unexecuted calls and synthetic post-call/reset-model entries remain explicit.
"""
import argparse
import copy
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import struct
import sys

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[2]
BASE, EXE = 0x8007AA38, 0x8000F800
START, USED, END = 0x11000, 0x11BC0, 0x12000
PAYLOAD, LOOP, RESET = BASE + START, 0x80087258, 0x800858F8
LINKED = "_local/research/dark-wizard-native-closure-20260906/fresh-town-overlay-link-r01.5HRJ13/source_town_overlay_link.py"
SELECTOR = "_local/research/dark-wizard-native-closure-20260906/town-resident-copy-selector-r01.XzDUh7/source_copy_selector.py"
DISC = "src/Spyro.Editor.NativeId65FreshDiscSmoke/source_fresh_disc_oracle.py"
DISC_BODY = "src/Spyro.Editor.NativeId65FreshDiscSmoke/source_fresh_disc_body.py"
REWARD = "src/Spyro.Editor.NativeFreshWizardHostSmoke/source_fresh_chest_reward_precondition_witness.py"
MACHINE = "src/Spyro.Editor.NativeId65FreshDocumentSmoke/source_variable_native_loader_oracle.py"
PINS = {
    MACHINE: "75f6762822c79520176bb3127e9b426822fb7b007b2ba6da931e4822cb43f283",
    LINKED: "3a66f4e6cfaa00c22c551c6ba713ec077473ab2b18d42486e099e0b53799c2e2",
    SELECTOR: "02d104d41ca33f5616ffa3097b9c431954ba1092a3c37b388fd09e4f9ec557a5",
    DISC: "f93780c582df180f378a4121a824d01dec6f13e3c5da68a61af571772e732608",
    DISC_BODY: "e69619f391814c270c9c28bc588f432f79f8a6cd3f61301e3a1c483a8645ad07",
    REWARD: "0868d7ce9cbab4abcb29b768490f2bfaded357bb7d57abbdc96c471509620405",
}
HOST_SHA = "ad6d952cbf70ab8b1eab53f7637ffd0235271b7b20696956c9406f36f714fa7e"
CURRENT_EXE_SHA = "a0eb46c2735355c05d66cc33492ed70c6c2174e77e8e8eb6c2ca8fae0f3fc8dc"
CURRENT_INIT_SHA = "d7f0e7c9ed86467f0749db689b2bf1f1c4df27587eb357821ef8947d7f70466b"
# Independent complete 38-word literal: original nine callbacks and tail,
# level65-only anchor8008CA38. Not read from any new producer/output.
INITIALIZER = bytes.fromhex(
    "0780013c6c59228c0880013cbfff4224040040140980023c0980023c02000010"
    "38ca4224cc9e4224d88522ac0780013c0880023ca4724224cc5822ac0880023c"
    "54da4224345722ac0980023c688f4224e45822ac0880023c207e4224bc5622ac"
    "0880023c2caf4224a05722ac0880023c68b04224a85822ac0880023cacb04224"
    "4c5722ac0880023c4cd04224d85822acb86d010800000000")


def sha(data): return hashlib.sha256(data).hexdigest()
def hx(value): return f"{value:08X}"
def word(data, at=0): return struct.unpack_from("<I", data, at)[0]
def pack(*words): return struct.pack("<" + "I" * len(words), *words)
def put(data, at, value): struct.pack_into("<I", data, at, value)


def require(ok, label):
    if not ok: raise AssertionError(label)


def guard_pins():
    for path, digest in PINS.items():
        require(sha((ROOT / path).read_bytes()) == digest, "Frozen reference: " + path)


def load(path, name):
    require(sha((ROOT / path).read_bytes()) == PINS[path], "Pinned definition import")
    spec = importlib.util.spec_from_file_location(name, ROOT / path)
    module = importlib.util.module_from_spec(spec); spec.loader.exec_module(module)
    return module


def jump(pc, destination, call=False):
    require(pc % 4 == destination % 4 == 0 and 0x80000000 <= destination < 0x80200000
            and (pc + 4) >> 28 == destination >> 28, "Native jump address domain")
    return (0x0C000000 if call else 0x08000000) | ((destination >> 2) & 0x3FFFFFF)


def branch(instruction, pc, destination):
    delta = destination - pc - 4
    require(delta % 4 == 0 and -32768 <= delta // 4 <= 32767, "Native signed branch range")
    return (instruction & 0xFFFF0000) | (delta // 4 & 65535)


def extension(owners, host_bytes, prior):
    require(len(host_bytes) == START and sha(host_bytes) == HOST_SHA, "Complete current Host prefix, not its used end")
    output = bytearray(host_bytes) + bytearray(END - START)
    mapping, segments, transfers = {}, [], []
    for (name, source, length, digest), offset in zip(prior.HANDLERS, (4, 0x34, 0x744)):
        data = prior.extent(owners, "peace", source, length)
        require(len(data) == length and sha(data) == digest, "Exact independent source segment " + name)
        final = PAYLOAD + offset
        segments.append((name, source, final, data))
        for at in range(0, length, 4):
            require(source + at not in mapping, "Disjoint source instruction owners")
            mapping[source + at] = final + at
    targets = {0x8008A20C: LOOP, 0x80088CF4: RESET,
               0x8008A204: PAYLOAD + 0xBA0, 0x800880FC: PAYLOAD + 0xBB0}
    consumed = set()
    for name, source, final, data in segments:
        revised = bytearray(data)
        for at in range(0, len(data), 4):
            before = word(data, at); edge = prior.transfer(source + at, before)
            if edge is None: continue
            kind, target = edge
            require(at + 8 <= len(data) and prior.transfer(source + at + 4, word(data, at + 4)) is None,
                    "Complete noncontrol delay belongs to its source segment")
            if target is None:
                require(name == "state" and source + at in (0x80084304, 0x8008431C, 0x8008436C, 0x80084394, 0x80084454),
                        "Only the five previously inspected computed callback sites are retained")
                transfers.append(dict(segment=name, sourcePc=hx(source + at), finalPc=hx(final + at),
                                      sourceTarget=None, finalTarget=None, kind=kind, owner="retained-unexecuted-computed-call",
                                      before=hx(before), after=hx(before), delay=hx(word(data, at + 4))))
                continue
            if target in mapping: destination, owner = mapping[target], "retained-segment"
            elif target in targets:
                destination, owner = targets[target], "explicit-continuation"; consumed.add(target)
            else:
                require(kind == "call" and EXE <= target <= EXE + len(owners["exe"]) - 4,
                        "Unowned noncall chest transfer")
                destination, owner = target, "retained-unexecuted-EXE-call"
            after = branch(before, final + at, destination) if kind == "branch" else jump(final + at, destination, kind == "call")
            put(revised, at, after)
            transfers.append(dict(segment=name, sourcePc=hx(source + at), finalPc=hx(final + at),
                                  sourceTarget=hx(target), finalTarget=hx(destination), kind=kind, owner=owner,
                                  before=hx(before), after=hx(after), delay=hx(word(data, at + 4))))
        inverse = bytearray(revised)
        for row in transfers:
            if row["segment"] == name: put(inverse, int(row["sourcePc"], 16) - source, int(row["before"], 16))
        require(bytes(inverse) == data, "Complete relocated segment inverse including unchanged words/delays")
        output[final - BASE:final - BASE + len(revised)] = revised
    require(consumed == set(targets), "Exactly four source continuation roles consumed")
    require(sum(t["sourceTarget"] is None for t in transfers) == 5,
            "All five source computed callback sites remain explicitly unexecuted and unbound")
    recipes = {0: pack(0x0260A021), 0x740: pack(0x0260A021)}
    for at, cls, destination in ((0xB78, 0xD, PAYLOAD + 0x740), (0xB8C, 0xAE, PAYLOAD)):
        recipes[at] = pack(0x24020000 | cls, branch(0x10820000, PAYLOAD + at + 4, destination),
                           0, jump(PAYLOAD + at + 12, LOOP), 0)
    recipes[0xBA0] = pack(0x0C01495A, 0x02602021, jump(PAYLOAD + 0xBA8, LOOP), 0)
    recipes[0xBB0] = pack(0x0C01569E, 0x24A70054, jump(PAYLOAD + 0xBB8, LOOP), 0)
    for at, data in recipes.items(): output[START + at:START + at + len(data)] = data
    patches = []
    for pc, target in ((0x8007DB80, PAYLOAD + 0xB78), (0x8007DC4C, PAYLOAD + 0xB8C)):
        at = pc - BASE; before = host_bytes[at:at + 8]
        require(before == pack(0x08021C96, 0), "Exact retained chest/reward default hook preimage")
        after = pack(jump(pc, target), 0); output[at:at + 8] = after
        patches.append(dict(pc=hx(pc), offset=at, beforeHex=before.hex(), afterHex=after.hex()))
    inverse = bytearray(output[:START])
    for row in patches: inverse[row["offset"]:row["offset"] + 8] = bytes.fromhex(row["beforeHex"])
    require(bytes(inverse) == host_bytes and output[USED:] == bytes(END - USED), "Full preserved prefix inverse and exact padding")
    blocks = [("chest", START, 0x740), ("reward", START + 0x740, 0x438),
              ("reward-dispatch", START + 0xB78, 0x14), ("chest-dispatch", START + 0xB8C, 0x14),
              ("delete", START + 0xBA0, 0x10), ("sound", START + 0xBB0, 0x10), ("padding", USED, END - USED)]
    require(all(a + n == b for (_, a, n), (_, b, _) in zip(blocks, blocks[1:]))
            and blocks[-1][1] + blocks[-1][2] == END, "Exact adjacent append owners through complete padded end")
    return bytes(output), mapping, transfers, patches, [dict(id=name, offset=at, byteLength=n, pc=hx(BASE + at),
        sha256=sha(output[at:at + n])) for name, at, n in blocks]


def current_executable(original, host, linked, selector):
    require("source_fresh_disc_body" not in sys.modules, "No ambient sibling module substitution")
    sys.modules["source_fresh_disc_body"] = load(DISC_BODY, "chest_host_disc_definitions")
    try: disc = load(DISC, "chest_host_frozen_disc")
    finally: del sys.modules["source_fresh_disc_body"]
    baseline = disc.compose_exe(original, 19)
    current = bytearray(baseline); before = original[0x4B098:0x4B130]
    initializer = selector.prototype(before, host["copy"])
    require(len(initializer) == 0x98 and sha(initializer) == CURRENT_INIT_SHA
            and current[0x4B098:0x4B130] == before and current[0x5FC7E:0x5FC80] == bytes(2), "Current Family EXE sequential preimages")
    current[0x4B098:0x4B130] = initializer; current[0x5FC7E:0x5FC80] = b"\x0A\0"
    require(sha(current) == CURRENT_EXE_SHA, "Complete independent current Family EXE")
    inverse = bytearray(current); inverse[0x4B098:0x4B130] = before; inverse[0x5FC7E:0x5FC80] = bytes(2)
    require(bytes(inverse) == baseline, "Complete current Family stages inverse to independently inverted LAB/music baseline")
    require(len(INITIALIZER) == 0x98 and selector.prototype(before, BASE + END) == INITIALIZER,
            "Complete new initializer literal matches independent source recipe")
    require(initializer[:0x20] == INITIALIZER[:0x20] and initializer[0x24:] == INITIALIZER[0x24:]
            and word(initializer, 0x20) == 0x2442BA38 and word(INITIALIZER, 0x20) == 0x2442CA38,
            "Only copy-anchor ADDIU changes in the complete initializer")
    result = bytearray(current); result[0x4B098:0x4B130] = INITIALIZER
    require(result[:0x4B098] == current[:0x4B098] and result[0x4B130:] == current[0x4B130:]
            and result[0x5FC7E:0x5FC80] == b"\x0A\0", "Detached Host EXE complement; target10 unchanged")
    inverse = bytearray(result); inverse[0x4B098:0x4B130] = initializer
    require(inverse == current, "Complete detached EXE inverse to actual current Family EXE")
    return bytes(current), bytes(result)


def consumer_windows(original, current, revised, inspected, control436, reward):
    specs = [("retained-call-entry-only", int(row["target"], 16), 0x10, row["entryInspection"]["sha256"])
             for row in inspected["retainedOriginalCallsRequiringCurrentExecutableBinding"]]
    specs += [(row["role"], int(row["pc"], 16), row["byteLength"], row["sha256"])
              for row in control436["narrowOriginalExecutableConsumersRequiringCurrentBinding"]]
    specs += [("copy-leaf", 0x80017700, 0x1C, sha(original[0x7F00:0x7F1C])),
              ("subtract-leaf", reward.SUB_ENTRY, reward.SUB_BYTES, reward.SUB_SHA),
              ("initializer526A8-prefix-only-before-GTE", 0x800526A8, 0xB0,
               "6957574246c153803d760eb6e0001529b8016eaec09da84f94aa893cc5560c21"),
              ("initializer-tail-and-neighbor-owner", 0x8005B6E0, 0xA0,
               "f1d94157410333d6640a99043f9e175f6a41c2150a5bfa47028fdf4f54626be4")]
    result = []
    for name, pc, length, digest in specs:
        at = pc - EXE
        require(0 <= at <= len(original) - length, "Consumer window inside actual complete EXE")
        source = original[at:at + length]
        require(sha(source) == digest and current[at:at + length] == source and revised[at:at + length] == source,
                "Exact source/current/new consumer bytes " + name + " " + hx(pc))
        result.append(dict(id=name, pc=hx(pc), fileOffset=at, byteLength=length, sha256=digest,
                           bytesHex=source.hex(), currentAndNewMatch=True,
                           completeOutgoingClosureCertified=False))
    return result


def retained_overlay_windows(town, current, revised, prior):
    specs = [("Town-loop", 0x80087258, 0x4C,
              "829a0bc3b1a7a93ee100386cb5d7f16eef6fc2737e1044249ef9fba760abfafd"),
             ("Town-reset", 0x800858F8, 0x4C, prior.RESET_PINS["town"]),
             ("Town-key-handler", 0x80083B4C, 0x160, None),
             ("Town-key-compare", 0x8007DBD8, 8, None)]
    result = []
    for name, pc, length, digest in specs:
        at = pc - BASE; source = town[at:at + length]
        require(len(source) == length and (digest is None or sha(source) == digest)
                and current[at:at + length] == source and revised[at:at + length] == source,
                "Retained original/current/new overlay window " + name)
        if name == "Town-key-compare": require(source == pack(0x240200AD, 0x108217DB), "Literal retained AD comparison")
        result.append(dict(id=name, pc=hx(pc), byteLength=length, sha256=sha(source), bytesHex=source.hex(),
                           originalTownMatched=True, currentHostMatched=True, wholeBehaviorCertified=False))
    # These are the CURRENT Wizard-generated owners, not original Town bytes.
    for name, at, length in (("Wizard-class-hook", 0x30FC, 8), ("Wizard-dispatch", 0x10D70, 0x44),
                             ("tick07-table", 0x264, 4), ("tick41-table", 0x34C, 4),
                             ("spawn07-table", 0x3A8, 4), ("spawn41-table", 0x490, 4),
                             ("complete-current-padding", 0x10DB4, 0x24C)):
        before = current[at:at + length]
        require(len(before) == length and revised[at:at + length] == before, "Current Wizard owner retained " + name)
        result.append(dict(id=name, pc=hx(BASE + at), byteLength=length, sha256=sha(before), bytesHex=before.hex(),
                           originalTownMatched=False, currentHostMatched=True, wholeBehaviorCertified=False))
    return result


def scalar_cases(owners, output, mapping, host, reward, inspector, preconditions, prior, selector):
    checks, refusals, traces = [], [], []
    def check(ok, label): require(ok, label); checks.append(label)
    def refuse(action, phrase, label):
        try: action()
        except AssertionError as error:
            require(phrase in str(error), label + ": wrong refusal: " + str(error)); refusals.append(label); return
        raise AssertionError(label + ": unexpectedly accepted")
    code = host.words(output, BASE)
    source_code, _ = reward.source_code(owners, inspector, prior)
    for pc, instruction in source_code.items():
        if pc < BASE: code[pc] = instruction  # Only the two actual EXE vector leaves.
    def town():
        m, old = host.town_main(code); m.writes.clear(); m.reads.clear(); m.calls.clear(); return m, old
    # Begin at the preserved outer dispatch, not just inside a manufactured thunk.
    for cls, stop in ((0xD, PAYLOAD + 0x744), (0xAE, PAYLOAD + 4), (0xAD, 0x80083B4C)):
        m, _ = town(); m.r[4] = cls; before, memory = m.r.copy(), m.memory.copy()
        check(m.run(0x8007DB34, {stop}, maximum=128) == stop and m.r[19] == host.ROW
              and m.r[29] == before[29] and m.r[31] == before[31] and m.memory == memory
              and (cls == 0xAD or m.r[20] == host.ROW), "Actual combined dispatch class " + hex(cls))
    # Final native state stores and both possible Town list/return outcomes.
    m, original = town(); m.r[20], m.r[18], m.r[2] = host.ROW, host.ROW + 0x58, 0x12345678
    before, memory = m.r.copy(), m.memory.copy()
    writes = [(host.ROW + 0x58 + 0x14, 4, 0x12345678), (host.ROW + 0x18, 4, 0)]
    check(m.run(mapping[0x80084464], {LOOP}, maximum=16) == LOOP
          and m.memory == reward.expected_memory(memory, writes) and m.writes == writes
          and m.r[4] == host.ROW and all(m.r[i] == before[i] for i in range(32) if i != 4),
          "Relocated state terminal stores and full register/memory complement")
    cursor = m.r[29] + 0x180; before = m.memory.copy(); m.writes.clear()
    check(m.run(LOOP, {0x8007DAEC}, maximum=32) == 0x8007DAEC and m.r[19] == host.ROW + 0x58
          and m.memory == reward.expected_memory(before, [(cursor, 4, 0x800700FC)]), "Town actual next-row cursor, not donor1E8")
    m.run(0x8007DAEC, {0x8007DB34}); before = m.memory.copy(); m.writes.clear()
    check(m.run(LOOP, {host.RETURN}, maximum=32) == host.RETURN
          and all(m.r[i] == original[i] for i in [*range(16, 24), 29, 30, 31])
          and m.memory == reward.expected_memory(before, [(cursor, 4, 0x80070100)]), "Town terminal path restores1D0 frame without Wizard2B0 leave")
    # Call-entry argument proofs; the EXE bodies do not execute or return here.
    for offset, target, changed in ((0xBA0, 0x80052568, {4: host.ROW}),
                                    (0xBB0, 0x80055A78, {7: host.ROW + 0xAC})):
        m, _ = town(); m.r[5] = host.ROW + 0x58; before, memory = m.r.copy(), m.memory.copy()
        expected = before.copy()
        for reg, value in {**changed, 31: PAYLOAD + offset + 8}.items(): expected[reg] = value
        check(m.run(PAYLOAD + offset, {target}, maximum=8) == target and m.r == expected and m.memory == memory
              and m.calls == [(PAYLOAD + offset, target)], "Appended call and actual delay arguments " + hex(offset))
        tail, _ = town(); before, memory = tail.r.copy(), tail.memory.copy()
        check(tail.run(PAYLOAD + offset + 8, {LOOP}, maximum=4) == LOOP and tail.r == before and tail.memory == memory,
              "Separate post-call cut, no simulated helper return " + hex(offset))
    # Reset branch reaches the RETAINED Town entry directly. The actual model
    # root is still a future row/body gate; this tiny model chain is synthetic.
    m, _ = town(); m.r[19] = m.r[20] = host.ROW; m.r[2] = 0
    m.poke(host.ROW + 0x36, 0xAE, 2); model, animation = 0x80150000, 0x80150100
    m.map(0x80076378 + 0xAE * 4, 4); m.poke(0x80076378 + 0xAE * 4, model)
    m.map(model, 0x3C); m.poke(model + 0x38, animation); m.map(animation, 0xD); m.poke(animation + 0xC, 7, 1)
    before, memory = m.r.copy(), m.memory.copy()
    writes = [(host.ROW + offset, 1, value) for offset, value in ((0x48, 0), (0x40, 0), (0x3C, 0), (0x3D, 0), (0x3E, 0), (0x3F, 1), (0x41, 7))]
    check(m.run(mapping[0x8007E76C], {LOOP}, maximum=40) == LOOP
          and m.memory == reward.expected_memory(memory, writes) and m.writes == writes
          and m.r[19] == m.r[20] == host.ROW and m.r[29] == before[29], "Relocated reward reset exit uses actual retained Town reset")
    # Differential execution of all FIVE actual source private states, not a
    # source-proof claim transferred to the new PCs without executing them.
    family = preconditions.family_rows(owners["peace-data"])
    def fixture(index, program):
        m = host.native.Scalar(reward.TraceCode(program)); rows = family["rows"]
        m.map(reward.ROWS - 0x10, reward.ROW_COUNT * 0x58 + 0x20, 0xA5)
        low = min(int(row["privateSceneOffset"], 16) for row in rows)
        high = max(int(row["privateSceneOffset"], 16) + row["privateByteLength"] for row in rows)
        m.map(reward.PRIVATE + low - 4, high - low + 8, 0xA6)
        for row in rows:
            at, props = reward.ROWS + row["trueIndex"] * 0x58, reward.PRIVATE + int(row["privateSceneOffset"], 16)
            reward.put_bytes(m, at, bytes.fromhex(row["rowHex"])); reward.put_bytes(m, props, bytes.fromhex(row["privateHex"])); m.poke(at, props)
        for at, value in ((reward.BASE_GLOBAL, reward.ROWS), (reward.END_GLOBAL, reward.ROWS + reward.ROW_COUNT * 0x58)):
            m.map(at, 4); m.poke(at, value)
        m.map(0x801EFFE0, 0x80, 0x6B); m.r = [0] + [(0x51000000 + i * 0x10101) & 0xFFFFFFFF for i in range(1, 32)]
        m.r[19] = m.r[20] = reward.ROWS + index * 0x58; m.r[29] = 0x801F0000
        return m
    for index in range(118, 123):
        source, final = fixture(index, source_code), fixture(index, code)
        for tick in range(3):
            for m in (source, final):
                m.reads.clear(); m.writes.clear(); m.calls.clear(); m.code.executed.clear(); m.instructions = 0
            check(source.run(reward.ENTRY, reward.STOPS, maximum=256) == reward.LOOP
                  and final.run(PAYLOAD + 0x740, {LOOP, RESET, *reward.UNEXECUTED_CALLS}, maximum=260) == LOOP,
                  "Retained and relocated waiting path both reach owned loops " + str((index, tick)))
            expected = source.r.copy(); expected[31] = mapping[expected[31]]
            check(final.r == expected and final.memory == source.memory and final.reads == source.reads
                  and final.writes == source.writes and final.calls == [(mapping[pc], target) for pc, target in source.calls]
                  and RESET not in final.code.executed and final.code.executed == [PAYLOAD + 0x740] + [mapping.get(pc, pc) for pc in source.code.executed],
                  "Full differential registers/memory/reads/writes/calls/trace " + str((index, tick)))
            traces.append(dict(row=index, tick=tick, instructions=final.instructions,
                               traceSha256=sha(b"".join(pack(pc, code[pc]) for pc in final.code.executed))))
    bad, _ = town(); del bad.code[PAYLOAD + 0xBA4]
    refuse(lambda: bad.run(PAYLOAD + 0xBA0, {0x80052568}), "Unowned instruction", "Missing thunk call-delay owner")
    bad, _ = town(); bad.code[PAYLOAD + 0xB78 + 4] = branch(0x10820000, PAYLOAD + 0xB7C, PAYLOAD + 0x744)
    bad.r[4] = 0xD
    bad.run(PAYLOAD + 0xB78, {PAYLOAD + 0x744})
    check(bad.r[20] != bad.r[19], "Negative interior entry bypasses required s4=s3 shim")
    # Execute actual incoming store/dispatch/init/tail for ID65 and controls.
    # The historical selector's own interpreter pin predates tiled-main checks.
    # Its pure prototype remains frozen; import the separately pinned current
    # Machine directly (unchanged by7093265), never override the old module pin.
    original = owners["exe"]; scalar = load(MACHINE, "chest_host_initializer_machine")
    scalar.require_helpers(); init_vectors = []
    def init_run(initializer, level):
        program = {}
        for _, pc, n, digest in selector.PINS:
            raw = original[pc - EXE:pc - EXE + n]; require(sha(raw) == digest, "Initializer source control")
            program.update(host.words(raw, pc))
        tail = original[0x4BEE0:0x4BF80]
        require(sha(tail) == "f1d94157410333d6640a99043f9e175f6a41c2150a5bfa47028fdf4f54626be4", "Complete initializer tail control")
        program.update(host.words(tail[:0x18], selector.TAIL)); program.update(host.words(initializer, selector.ENTRY))
        m = scalar.Machine(program)
        for at in selector.OWNERS: m.map(at, 4, 0xA5)
        for at, value in ((0x8007596C, 0xDEAD0065), (0x800758B4, level)):
            m.map(at, 4); m.poke(at, value)
        table = bytearray(original[0x1BA4:0x1D34]); put(table, 65 * 4, selector.ENTRY)
        m.blob(0x800113A4, table); m.r = [0] + [0x13570000 + i * 0x101 for i in range(1, 32)]; m.r[31] = scalar.RETURN
        m.run(0x800155E4, {0x800155FC}, maximum=128); return m
    old_init = selector.prototype(original[0x4B098:0x4B130], BASE + START)
    for level in (13, 65, 0, 255, 0xFFFFFFFF):
        # Incoming dispatcher is bounded to real table entries13/65. The other
        # selector values are covered by frozen selector, not out-of-table calls.
        if level not in (13, 65): continue
        old, new = init_run(old_init, level), init_run(INITIALIZER, level)
        expected, registers = old.memory.copy(), old.r.copy()
        if level == 65:
            expected = reward.expected_memory(expected, [(0x800785D8, 4, BASE + END), (0x800785DC, 4, BASE + END)])
            registers[2] = BASE + END
        check(new.memory == expected and new.r == registers and (new.hi, new.lo) == (old.hi, old.lo)
              and [(at, n) for at, n, _ in new.writes] == [(at, n) for at, n, _ in old.writes]
              and new.calls == old.calls, "Actual incoming initializer complete state complement " + str(level))
        init_vectors.append(dict(level=level, copyAnchor=hx(new.read(0x800785D8)), instructions=new.instructions))
    return dict(checkCount=len(checks), checks=checks, refusalCount=len(refusals), refusals=refusals,
                relocatedWaitingTraces=traces, incomingInitializer=init_vectors,
                resetModelChainSynthetic=True, helperReturnsAssumed=False, wholeChestLifecycleExecuted=False)


def main(path):
    require(__debug__, "Python assertions enabled"); guard_pins()
    reward = load(REWARD, "chest_host_frozen_445"); inspector = reward.frozen_inspector()
    preconditions = inspector.load_preconditions(); prior = preconditions.load_continuation(); prior.require_helpers()
    raw = prior.load(next(iter(prior.HELPERS)), "chest_host_original_reader")
    host = prior.load(list(prior.HELPERS)[1], "chest_host_frozen_abi")
    linked, selector = load(LINKED, "chest_host_frozen_link"), load(SELECTOR, "chest_host_frozen_selector")
    def guards():
        guard_pins(); inspector.guard_frozen(); prior.require_helpers(); host.require_helpers()
        for p, digest in linked.PINS.items(): require(sha((ROOT / p).read_bytes()) == digest, "Frozen linked dependency")
        machine = load(MACHINE, "chest_host_initializer_guard")
        machine.require_helpers()
    guards()
    specs = {**prior.OWNERS, "peace-data": preconditions.EXTRA_OWNERS["peace-data"],
             "wizard": (37 * 2048 + 0x2F5B000, 0x10000, "5d3996111567ffac6eca08d340f2f691824bb7969ba2a0cd95e9346ccfe157e1")}
    with open(path, "rb") as image:
        identity = inspector.file_identity(os.fstat(image.fileno()))
        def same():
            require(inspector.file_identity(os.fstat(image.fileno())) == identity
                    and inspector.file_identity(os.stat(path)) == identity, "Held/named original identity")
        same(); raw.identity(image)
        try:
            directory = raw.logical(image, 37 * 2048, 0x800)
            require(sha(directory) == "15d3e9c45f07e27a0ddd3f3a49b23a077e2c7750021cc89c55b7b16c551186cd", "Complete original WAD directory")
            owners = {name: raw.logical(image, at, n) for name, (at, n, _) in specs.items()}
            for name, (_, n, digest) in specs.items(): require(len(owners[name]) == n and sha(owners[name]) == digest, "Whole original owner " + name)
            base = linked.compose(host, owners["town"], owners["wizard"], owners["exe"])
            require(base["used"] == 0x10DB4 and base["end"] == START and sha(base["bytes"]) == HOST_SHA, "Complete prior Host reference")
            inspected = prior.inspection(owners, host)
            control436 = prior.scalar_witness(owners, host, inspected)
            control445 = reward.witness(owners, inspector, preconditions, prior, host)
            output, mapping, transfers, patches, blocks = extension(owners, base["bytes"], prior)
            current, revised = current_executable(owners["exe"], base, linked, selector)
            windows = consumer_windows(owners["exe"], current, revised, inspected, control436, reward)
            retained = retained_overlay_windows(owners["town"], base["bytes"], output, prior)
            scalar = scalar_cases(owners, output, mapping, host, reward, inspector, preconditions, prior, selector)
            # Reject changed owners, padding and complete-domain lengths, not
            # only a digest metadata substitute or a changed-byte count.
            negatives = []
            def exact(candidate): require(candidate == output, "Complete overlay reference")
            for label, at in (("retained-padding", 0x10DB4), ("Wizard-dispatch", 0x10D70),
                              ("07-table", 0x264), ("chest-hook", 0x3214), ("call-delay", START + 0xBA4),
                              ("source-delay", mapping[0x80084470] - BASE), ("new-padding", USED)):
                wrong = bytearray(output); wrong[at] ^= 1
                try: exact(bytes(wrong))
                except AssertionError: negatives.append(label)
                else: raise AssertionError("Missing complete owner refusal")
            for label, wrong in (("truncated", output[:-1]), ("extended", output + b"\0")):
                try: exact(wrong)
                except AssertionError: negatives.append(label)
                else: raise AssertionError("Missing complete length refusal")
            report = dict(marker="FRESH_CHEST_HOST_EXTENSION_REFERENCE_OK", sourceImageSha256=raw.IMAGE_SHA,
                helperPins=PINS, originalOwners=[dict(id=name, logicalOffset=at, byteLength=n, sha256=digest)
                                                for name, (at, n, digest) in specs.items()],
                currentOverlaySha256=sha(base["bytes"]), currentExecutableSha256=sha(current), currentInitializerSha256=CURRENT_INIT_SHA,
                overlaySha256=sha(output), overlayBytes=len(output), usedOverlayBytes=USED, appendedPayloadSha256=sha(output[START:USED]),
                initializerSha256=sha(INITIALIZER), initializerHex=INITIALIZER.hex(), executableSha256=sha(revised), executableBytes=len(revised),
                copyAnchor=hx(BASE + END), priorUsedEnd=0x10DB4, retainedPrefixBytes=START,
                blocks=blocks, prefixPatches=patches, relocations=transfers,
                executableOwner=dict(offset=0x4B098, byteLength=0x98, beforeSha256=CURRENT_INIT_SHA, afterSha256=sha(INITIALIZER)),
                currentExecutableConsumerWindows=windows, retainedOverlayWindows=retained,
                scalar=scalar, exactOutputNegativeControls=negatives,
                controls436=dict(checks=control436["checkCount"], refusals=control436["refusalCount"]),
                controls445=dict(checks=control445["checkCount"], refusals=control445["refusalCount"]),
                fullPrefixAndExecutableInverse=True, originalAndNewWholeDomainLengthsChecked=True,
                treasureTarget=10, sevenRowsIssued=False, actualModelRootsProven=False, bodyOrDiscIssued=False,
                completeRetainedCallClosureCertified=False, detachedReferenceOnly=True, runtime=False, publication=False)
        finally:
            raw.identity(image); same(); guards()
    with open(path, "rb") as image:
        require(inspector.file_identity(os.fstat(image.fileno())) == identity, "Reopened named original identity")
        raw.identity(image); same()
    guards(); report["sourceUnchanged"] = True
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("original_image"); parser.add_argument("--witness", action="store_true", required=True)
    arguments = parser.parse_args(); main(arguments.original_image)
