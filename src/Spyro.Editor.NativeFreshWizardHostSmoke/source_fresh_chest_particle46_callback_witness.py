#!/usr/bin/env python3
"""Original-only complete effect46 callback and bounded effect0C inspection.

Executes the actual effect46 allocator/RNG/loop/return from an explicit
post-debris-call chest cut. Empty pool and Town post-visibility outer frame are
synthetic owned inputs. No callback return is fabricated. Effect0C only gets
source context: no fake vector expansion, model/render/body/runtime approval.
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
ENTRY_WITNESS = "src/Spyro.Editor.NativeFreshWizardHostSmoke/source_fresh_chest_callback_entry_witness.py"
PARTICLE_SOURCE = "_local/research/dark-wizard-native-closure-20260906/town-repeat-reward-callback-r01.ban08o/source_reward_callbacks.py"
PINS = {
    ENTRY_WITNESS: "ed7cd81048b4357e5d443aa41eae4edf5c20bf2987f0feeede5506f48901143a",
    PARTICLE_SOURCE: "41ce841c57cd90247407adbf5317af3686dc441b46104a50e7cab048aef18e07",
}
WINDOW_NAMES = ("Town-particle-spawn-entry", "Town-particle46-spawn", "Town-particle46-spawn-tail",
                "Shared-particle-allocator", "Shared-native-RNG")
BASE, EXE, ENTRY, CALL, BACK = 0x8007AA38, 0x8000F800, 0x80084374, 0x80084394, 0x8008439C
POOL, POOL_BYTES, COUNT, SPREAD = 0x801C0000, 0x2000, 32, 0x18
XYZ = (8704, 8960, 8704)
RNG, POOL_BASE_GLOBAL, NEXT_GLOBAL = 0x80075AC0, 0x80075824, 0x80075738


def sha(data): return hashlib.sha256(data).hexdigest()
def hx(value): return f"{value:08X}"
def word(data, at=0): return struct.unpack_from("<I", data, at)[0]


def require(ok, label):
    if not ok: raise AssertionError(label)


def guard_pins():
    for path, digest in PINS.items(): require(sha((ROOT / path).read_bytes()) == digest, "Frozen witness dependency " + path)


def load(path, name):
    require(sha((ROOT / path).read_bytes()) == PINS[path], "Pinned definition import")
    spec = importlib.util.spec_from_file_location(name, ROOT / path)
    module = importlib.util.module_from_spec(spec); spec.loader.exec_module(module); return module


def expected_pool():
    """Independent integer interpretation of recorded effect46 field formulas.

    Exact four RNG calls per particle on the declared empty-pool path; this
    does not model random replacement at a full-pool boundary.
    """
    result = bytearray(POOL_BYTES); seed = 1
    def random15():
        nonlocal seed
        seed = (seed * 0x41C64E6D + 12345) & 0xFFFFFFFF
        return seed >> 16 & 0x7FFF
    for i in range(COUNT):
        at = i * 32; age = random15() & 15
        # Original SRA t0->v0 words00081303 shift12, and coordinate
        # SRA v1->v1 words00031883 shift2 (checked below at all six PCs).
        dx = (((random15() & 0xFFF) - 0x800) * SPREAD) >> 12
        dy = (((random15() & 0xFFF) - 0x800) * SPREAD) >> 12
        dz = ((random15() & 0xFFF) * SPREAD) >> 12
        starts = tuple((v >> 2) + delta * 4 for v, delta in zip(XYZ, (dx, dy, dz)))
        ends = tuple(start + delta * 2 for start, delta in zip(starts, (dx, dy, dz)))
        result[at:at + 4] = bytes((0x46, 1, age, 1))
        for offset, value in zip((4, 6, 8, 10, 12, 14), (*starts, *ends)):
            struct.pack_into("<H", result, at + offset, value & 65535)
        result[at + 16:at + 24] = bytes((0xE0, 0xC0, 0, 0x50, 0xFF, 0xFF, 0x80, 2))
    result[COUNT * 32 + 1] = 255  # Actual allocator moves the FF sentinel.
    return bytes(result), seed


def s3_inspection(owners, prior):
    start, length = 0x80083D68, 0x70C
    state = prior.extent(owners, "peace", start, length)
    require(sha(state) == "83c507315fcf747b0c94c6bd47c7622e79f38c68ff746421fd9b2021314a479f", "Whole source state owner")
    definitions = []
    for at in range(0, length, 4):
        instruction = word(state, at); op = instruction >> 26
        rd, rt = instruction >> 11 & 31, instruction >> 16 & 31
        writes = (op == 0 and rd == 19) or (op in (*range(8, 16), *range(0x20, 0x27)) and rt == 19)
        if not writes: continue
        require(len(definitions) < 16, "Finite s3 source-definition inventory")
        lo, hi = max(0, at - 16), min(length, at + 36)
        definitions.append(dict(pc=hx(start + at), instruction=prior.decoded(start + at, instruction),
                                context=[prior.decoded(start + i, word(state, i)) for i in range(lo, hi, 4)],
                                sourceWindowPc=hx(start + lo), sourceWindowBytes=hi - lo,
                                sha256=sha(state[lo:hi]), dominatingDefinitionProven=False))
    pc, size = 0x8008439C, 0xC0
    data = prior.extent(owners, "peace", pc, size)
    return dict(marker="CHEST_PARTICLE0C_S3_SOURCE_INSPECTION", syntacticS3Definitions=definitions,
                noStateLocalDefinitionObserved=not definitions,
                lateChestWindow=dict(pc=hx(pc), byteLength=size, sha256=sha(data), bytesHex=data.hex(),
                                     instructions=[prior.decoded(pc + at, word(data, at)) for at in range(0, size, 4)]),
                observedTownConsumer=dict(particleEntryS3FromA2Pc="80088F78", classReadPc="800892E0",
                                          classReadWord="86630036", classReadOffset=0x36),
                actualS3OwnerProven=False, particle0CExecuted=False, syntheticVectorExpanded=False)


def witness(owners, entry, inspector, prior, reference, host, linked, particle, Machine, check, refuse):
    base = linked.compose(host, owners["town"], owners["wizard"], owners["exe"])
    overlay, mapping, _, _, _ = reference.extension(owners, base["bytes"], prior)
    check(sha(overlay) == entry.OVERLAY_SHA, "Complete frozen450 new Host")
    selector = reference.load(reference.SELECTOR, "particle46_selector_recipe")
    current, revised = reference.current_executable(owners["exe"], base, linked, selector)
    check(sha(current) == reference.CURRENT_EXE_SHA
          and sha(revised) == "ce341af853579b6b17a18222c08d2c17dbf6a9a572be672cf39edd5a6699ad20", "Whole independent current/new executable controls")
    code, windows = {}, []
    for name, owner, pc, n in particle.WINDOWS:
        if name not in WINDOW_NAMES: continue
        source = prior.extent(owners, owner, pc, n)
        check(sha(source) == particle.SOURCE_PINS[name], "Full previously captured callback/helper owner " + name)
        if name in ("Town-particle46-spawn-tail", "Shared-native-RNG"):
            changed = bytearray(source); changed[-1 if owner == "town" else 0] ^= 1
            refuse(lambda: require(sha(changed) == particle.SOURCE_PINS[name], "Changed native source owner"),
                   "Changed native source owner", "Changed callback delay or RNG multiplier pin " + name)
        if owner == "exe":
            at = pc - EXE
            check(current[at:at + n] == revised[at:at + n] == source, "Actual current/new EXE consumer match " + name)
        else: check(overlay[pc - BASE:pc - BASE + n] == source, "Actual new Host callback bytes " + name)
        for at in range(0, n, 4):
            require(pc + at not in code or code[pc + at] == word(source, at), "Consistent callback code overlap")
            code[pc + at] = word(source, at)
        windows.append(dict(id=name, owner=owner, pc=hx(pc), byteLength=n, sha256=sha(source)))
    require(len(windows) == len(WINDOW_NAMES), "All and only five complete callback/helper windows")
    check(all(code[pc] == 0x00081303 for pc in (0x800897C4, 0x800897E0, 0x800897F8))
          and all(code[pc] == 0x00031883 for pc in (0x80089808, 0x80089820, 0x80089838)),
          "Original literal SRA delta12 and coordinate2 instructions at all six consumers")
    # Confirm the real0C header class read from the same pinned source, without
    # adding this body to executable code or assigning any fake a2 extent.
    particle0c = prior.extent(owners, "town", 0x800892C0, 0x148)
    check(sha(particle0c) == particle.SOURCE_PINS["Town-particle0C-spawn"]
          and word(particle0c, 0x20) == 0x86630036
          and word(prior.extent(owners, "town", 0x80088F78, 4)) == 0x00C09821, "Actual0C context reads a2+36, not a position-only interface")
    literal = dict(entry.SOURCE_SPANS)[ENTRY]
    check(prior.extent(owners, "peace", ENTRY, len(literal) * 4) == entry.pack(literal), "Exact462 post-call chest cut")
    source_code = {**code, **{ENTRY + 4 * i: w for i, w in enumerate(literal)}}
    final_code = {**code, **{mapping[ENTRY + 4 * i]: word(overlay, mapping[ENTRY + 4 * i] - BASE) for i in range(len(literal))}}
    table_pc, table_n = 0x8007AC80, 0x288
    table = overlay[table_pc - BASE:table_pc - BASE + table_n]
    check(word(table, 0x144 + 0x46 * 4) == 0x8008978C, "Actual current/new46 table entry, not Wizard07/41 entries")
    def fixture(program):
        # The native Town outer frame is produced by its actual prologue;
        # host.town_main explicitly separates unexecuted visibility maintenance.
        town, _ = host.town_main(host.words(overlay, BASE))
        m = Machine(entry.TraceCode(program)); m.r, m.memory = town.r.copy(), town.memory.copy()
        m.hi, m.lo = town.hi, town.lo
        for at in (RNG, POOL_BASE_GLOBAL, NEXT_GLOBAL, 0x800785D8, 0x8007596C,
                   0x800758CC, 0x80075734, 0x800758E4, 0x800756BC, 0x800757A0, 0x800758A8, 0x8007574C, 0x800758D8):
            m.map(at, 4, 0xA5)
        m.poke(0x8007596C, 65)
        init = Machine(entry.TraceCode(host.words(reference.INITIALIZER, 0x8005A898)))
        init.memory, init.r = m.memory.copy(), m.r.copy()
        check(init.run(0x8005A898, {0x8005B6E0}, maximum=64) == 0x8005B6E0
              and int.from_bytes(init.bytes(0x800758E4, 4), "little") == 0x80088F68, "Actual new initializer writes particle callback")
        m.memory = init.memory.copy()  # Never override its callback target.
        m.map(table_pc, table_n); m.memory.update((table_pc + i, value) for i, value in enumerate(table))
        m.map(POOL - 32, POOL_BYTES + 64, 0xA7)
        for i in range(POOL_BYTES): m.poke(POOL + i, 0, 1)
        m.poke(POOL + 1, 255, 1); m.poke(RNG, 1); m.poke(POOL_BASE_GLOBAL, POOL); m.poke(NEXT_GLOBAL, POOL)
        m.poke(host.ROW + 0x36, 0xAE, 2)
        for i, value in enumerate(XYZ): m.poke(host.ROW + 0xC + i * 4, value)
        m.r[16], m.r[20] = 10, host.ROW
        return m
    expected, seed = expected_pool(); results, cases = [], []
    for relocated, program in ((False, source_code), (True, final_code)):
        m = fixture(program); memory, registers = m.memory.copy(), m.r.copy()
        sp = m.r[29]; start = mapping[ENTRY] if relocated else ENTRY; back = mapping[BACK] if relocated else BACK
        call = mapping[CALL] if relocated else CALL
        check(m.run(start, {back}, maximum=20000) == back, "Actual complete46 callback and return " + str(relocated))
        if m.bytes(POOL, POOL_BYTES) != expected or int.from_bytes(m.bytes(RNG, 4), "little") != seed:
            print(json.dumps(dict(diagnostic="particle46-reference-mismatch", relocated=relocated,
                actualRng=hx(int.from_bytes(m.bytes(RNG, 4), "little")), expectedRng=hx(seed),
                actualNext=hx(int.from_bytes(m.bytes(NEXT_GLOBAL, 4), "little")),
                expectedNext=hx(POOL + COUNT * 32), actualFirst128=m.bytes(POOL, 128).hex(),
                expectedFirst128=expected[:128].hex(), differingOffsets=[i for i, (a, b) in
                enumerate(zip(m.bytes(POOL, POOL_BYTES), expected)) if a != b][:96])), file=sys.stderr)
        check(m.bytes(POOL, POOL_BYTES) == expected and int.from_bytes(m.bytes(RNG, 4), "little") == seed
              and int.from_bytes(m.bytes(NEXT_GLOBAL, 4), "little") == POOL + COUNT * 32,
              "Independent complete32-particle fields, untouched pool remainder, sentinel and RNG")
        allowed = set(range(POOL, POOL + COUNT * 32 + 2)) | set(range(RNG, RNG + 4)) | set(range(NEXT_GLOBAL, NEXT_GLOBAL + 4)) | set(range(sp - 0xB0, sp))
        check(all(m.memory[at] == value for at, value in memory.items() if at not in allowed)
              and set(m.memory) == set(memory)
              and all(all(at + i in allowed for i in range(n)) for at, n, _ in m.writes), "Full memory complement and every write owner")
        check(all(m.r[r] == registers[r] for r in [*range(16, 24), 29, 30]) and m.r[31] == back,
              "Complete saved-register set and actual Town stack restored by native particle epilogue")
        check(m.calls[0] == (call, 0x80088F68)
              and sum(target == 0x80053570 for _, target in m.calls) == COUNT
              and sum(target == 0x8006272C for _, target in m.calls) == COUNT * 4
              and {target for _, target in m.calls} == {0x80088F68, 0x80053570, 0x8006272C}
              and m.instructions == len(m.code.trace) and 0x80089EC4 in m.code.trace
              and back not in m.code.trace, "Only actual callback/allocator/RNG calls and real JR return, finite owned trace")
        cases.append(dict(relocated=relocated, entry=hx(start), returnPc=hx(back), instructions=m.instructions,
                          fullPoolSha256=sha(m.bytes(POOL, POOL_BYTES)), emittedSha256=sha(m.bytes(POOL, COUNT * 32)),
                          finalRng=hx(seed), callbackFrame=[hx(sp - 0xB0), hx(sp)],
                          initializerProducedParticleCallback=hx(int.from_bytes(bytes(memory[0x800758E4 + i] for i in range(4)), "little")),
                          calls=[dict(pc=hx(pc), target=hx(target)) for pc, target in m.calls]))
        results.append(m)
    source, final = results; normalized_regs = final.r.copy(); normalized_regs[31] = source.r[31]
    normalized_memory = final.memory.copy(); saved_ra = final.r[29] - 4
    for i in range(4): normalized_memory[saved_ra + i] = source.memory[saved_ra + i]
    check(normalized_regs == source.r and normalized_memory == source.memory
          and (source.hi, source.lo) == (final.hi, final.lo)
          and final.code.trace == [mapping.get(pc, pc) for pc in source.code.trace], "Full source/final state and instruction trace differential; only actual caller links rebased")
    missing = fixture(final_code); del missing.memory[host.ROW + 0xC]
    refuse(lambda: missing.run(mapping[ENTRY], {mapping[BACK]}), "Unmapped native read", "Missing actual source XYZ owner")
    missing_rng = fixture(final_code); del missing_rng.code[0x80062758]
    refuse(lambda: missing_rng.run(mapping[ENTRY], {mapping[BACK]}), "Unowned native instruction", "Missing real RNG return delay")
    missing_return = fixture(final_code); del missing_return.code[0x80089EC8]
    refuse(lambda: missing_return.run(mapping[ENTRY], {mapping[BACK]}), "Unowned native instruction", "Missing native callback return delay")
    wrong = fixture(final_code); wrong.poke(0x800758E4, 0x8008BFF0)
    refuse(lambda: wrong.run(mapping[ENTRY], {mapping[BACK]}), "Unowned native instruction", "Wrong Peace callback cannot be substituted")
    short = fixture(final_code); del short.memory[POOL + COUNT * 32 + 1]
    refuse(lambda: short.run(mapping[ENTRY], {mapping[BACK]}), "Unmapped fixture write", "Allocator needs the next sentinel byte beyond emitted records")
    wrong_table = fixture(final_code); wrong_table.poke(table_pc + 0x144 + 0x46 * 4, 0x800892C0)
    refuse(lambda: wrong_table.run(mapping[ENTRY], {mapping[BACK]}), "Unowned native instruction", "Effect0C is not an admitted46 callback body")
    return dict(marker="FRESH_CHEST_PARTICLE46_CALLBACK_WITNESS_OK", sourceWindows=windows,
                expectedFullPoolSha256=sha(expected), expectedEmittedSha256=sha(expected[:COUNT * 32]), cases=cases,
                completeHostSha256=sha(overlay), currentExecutableSha256=sha(current), newExecutableSha256=sha(revised),
                actualParticleTable=dict(pc=hx(table_pc), byteLength=table_n, sha256=sha(table)),
                particle0cInspection=s3_inspection(owners, prior),
                entryConditions=dict(postDebrisCallbackCut=True, previousCallbackReturnNotAssumed=True,
                                     s0=10, count=COUNT, spread=SPREAD, xyz=XYZ, rngSeed=1,
                                     poolBase=hx(POOL), poolBytes=POOL_BYTES, emptyPool=True,
                                     actualTownPrologueThenSeparatePostVisibilityCut=True),
                syntheticOwners=dict(stack=[hx(host.STACK - 0x800), hx(host.STACK + 0x20)],
                                     twoRows=[hx(host.ROW), hx(host.ROW + 0xB0)],
                                     sourceXyz=[hx(host.ROW + 0xC), hx(host.ROW + 0x18)],
                                     guardedPool=[hx(POOL - 32), hx(POOL + POOL_BYTES + 32)],
                                     mutableGlobals=[hx(RNG), hx(NEXT_GLOBAL)],
                                     poolBaseGlobal=hx(POOL_BASE_GLOBAL), callbackTable=[hx(table_pc), hx(table_pc + table_n)]),
                complete46CallbackExecuted=True, nativeAllocatorAndRngExecuted=True, callbackReturnsAssumed=False,
                fullPoolReplacementOrPeakProven=False, particle0CExecuted=False, fullChestHandlerExecuted=False,
                rendererOrModelProof=False, bodyOrSceneIssued=False, runtime=False, publication=False)


def main(path):
    require(__debug__, "Python assertions enabled"); guard_pins()
    entry = load(ENTRY_WITNESS, "particle46_frozen462"); particle = load(PARTICLE_SOURCE, "particle46_frozen_body_records")
    inspector = entry.load(entry.INSPECTOR, "particle46_frozen456")
    reference = inspector.load(inspector.REFERENCE, "particle46_frozen450")
    prior = inspector.load(inspector.CONTINUATION, "particle46_frozen436")
    raw = inspector.load(inspector.READER, "particle46_original_reader")
    host = prior.load(list(prior.HELPERS)[1], "particle46_host")
    linked = reference.load(reference.LINKED, "particle46_link")
    scalar = entry.load(entry.MACHINE, "particle46_current_scalar")
    Machine = entry.load(entry.JALR_ISA, "particle46_jalr").bind(entry.load(entry.REWARD_ISA, "particle46_reward_isa").bind(scalar))
    checks, refusals = [], []
    def check(ok, label): require(ok, label); checks.append(label)
    def refuse(action, phrase, label):
        try: action()
        except AssertionError as error:
            require(phrase in str(error), label + ": wrong refusal: " + str(error)); refusals.append(label); return
        raise AssertionError(label + ": accepted negative control")
    entry.load(entry.ISA_CONTROLS, "particle46_isa_controls").isa(Machine, check, refuse)
    def guards():
        guard_pins(); entry.guard_pins(); inspector.guard_pins(); reference.guard_pins(); prior.require_helpers(); host.require_helpers(); scalar.require_helpers()
        for p, digest in linked.PINS.items(): require(sha((ROOT / p).read_bytes()) == digest, "Frozen link dependency")
    guards()
    specs = {**prior.OWNERS, "wizard": (37 * 2048 + 0x2F5B000, 0x10000, "5d3996111567ffac6eca08d340f2f691824bb7969ba2a0cd95e9346ccfe157e1")}
    with open(path, "rb") as image:
        initial = inspector.identity(os.fstat(image.fileno()))
        def same():
            require(inspector.identity(os.fstat(image.fileno())) == initial and inspector.identity(os.stat(path)) == initial, "Held/named original identity")
        same(); raw.identity(image)
        try:
            directory = raw.logical(image, 37 * 2048, 0x800)
            require(sha(directory) == "15d3e9c45f07e27a0ddd3f3a49b23a077e2c7750021cc89c55b7b16c551186cd", "Complete original WAD directory")
            owners = {name: raw.logical(image, at, n) for name, (at, n, _) in specs.items()}
            for name, (_, n, digest) in specs.items(): require(len(owners[name]) == n and sha(owners[name]) == digest, "Whole source owner " + name)
            report = witness(owners, entry, inspector, prior, reference, host, linked, particle, Machine, check, refuse)
            report.update(sourceImageSha256=raw.IMAGE_SHA, helperPins=PINS,
                          completeSourceOwners={name: dict(logicalOffset=at, byteLength=n, sha256=digest) for name, (at, n, digest) in specs.items()},
                          checkCount=len(checks), checks=checks, refusalCount=len(refusals), refusals=refusals)
        finally:
            raw.identity(image); same(); guards()
    with open(path, "rb") as image:
        require(inspector.identity(os.fstat(image.fileno())) == initial, "Reopened named original identity")
        raw.identity(image); same()
    guards(); report["sourceUnchanged"] = True; print(json.dumps(report, indent=2))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("original_image"); parser.add_argument("--witness", action="store_true", required=True)
    args = parser.parse_args(); main(args.original_image)
