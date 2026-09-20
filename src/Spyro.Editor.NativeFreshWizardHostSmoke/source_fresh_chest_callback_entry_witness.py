#!/usr/bin/env python3
"""Original-only six-cut chest callback-entry differential witness.

Actual initializer instructions supply the callback globals. Source chest
instructions run under TARGET TOWN globals, not original Peace runtime. Stop
before the callback body; never synthesize its return. Two explicit post-call
cuts and incoming a0=1 cuts are not full-handler, model or lifecycle proofs.
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
INSPECTOR = "src/Spyro.Editor.NativeFreshWizardHostSmoke/source_fresh_chest_computed_callbacks_inspector.py"
MACHINE = "src/Spyro.Editor.NativeId65FreshDocumentSmoke/source_variable_native_loader_oracle.py"
REWARD_ISA = "_local/research/dark-wizard-native-closure-20260906/town-repeat-reward-callback-r01.ban08o/reward_scalar.py"
JALR_ISA = "_local/research/dark-wizard-native-closure-20260906/town-number-reward-r01.SYHobQ/number_scalar.py"
ISA_CONTROLS = "_local/research/dark-wizard-native-closure-20260906/town-number-reward-r01.SYHobQ/source_number_oracle.py"
PINS = {
    INSPECTOR: "31158f24efafd7e597ef491021963612cf77d7f0f365a83385f0a49afbebbe3d",
    MACHINE: "75f6762822c79520176bb3127e9b426822fb7b007b2ba6da931e4822cb43f283",
    REWARD_ISA: "d31d78c011e57632bc727131850f66404fa9d05585aef3aef6ecd794a54c386a",
    JALR_ISA: "3342f868df504fcfc3c72093efe9b03fc36ce7d36a5edde566bd3d20d55b94d8",
    ISA_CONTROLS: "ec0d022215667cef8cd274e7557d8b16364036e11e16970f466a50c02e587431",
}
OVERLAY_SHA = "9d828474ed59e411b570e011e4b4156037e628c4cc9596cb476e7e2abdb995b2"
INITIALIZER_SHA = "4ba757dcac9e8a476c97a6767f82e5fe8bcdcd6511f5464ba1d87e981df16cf0"
BASE, EXE, TAIL = 0x8007AA38, 0x8000F800, 0x8005B6E0
ROW, VECTOR, STACK = 0x80140000, 0x80150000, 0x801FF000
SPAWN_GLOBAL, PARTICLE_GLOBAL = 0x800758CC, 0x800758E4
SPAWN, PARTICLE = 0x800872A4, 0x80088F68
# Literal words from original-only456, not copied producer bytes. The last
# owner includes BOTH alternatives and their common JALR/delay.
SOURCE_SPANS = (
    (0x800842EC, (0x14400010, 0x24040136, 0x02802821, 0x3C028007, 0x8C4258CC, 0, 0x0040F809, 0x26100001)),
    (0x8008430C, (0x24040137, 0x3C028007, 0x8C4258CC, 0, 0x0040F809, 0x02802821)),
    (0x80084350, (0x1440000B, 0x24040020, 0x24040135, 0x02802821, 0x3C028007, 0x8C4258CC, 0, 0x0040F809, 0x26100001)),
    (0x80084374, (0x2A02000A, 0x1440FFEE, 0x24040020, 0x24050046, 0x2686000C, 0x3C028007, 0x8C4258E4, 0, 0x0040F809, 0x24070018)),
    (0x80084424, (0x2405000C, 0x02603021, 0x3C028007, 0x8C4258E4, 0x08021115, 0x34078080,
                  0x2405000C, 0x02603021, 0x3C070500, 0x3C028007, 0x8C4258E4, 0x34E78080, 0x0040F809, 0)),
)
CUTS = (
    ("debris136-capacity-branch", 0x800842EC, 0x80084304, SPAWN_GLOBAL,
     {2: 0, 16: 2}, {2: SPAWN, 4: 0x136, 5: ROW, 16: 3},
     "Explicit capacity-comparison result v0=0; earlier capacity loads/comparison are not executed"),
    ("debris137-post-call", 0x8008430C, 0x8008431C, SPAWN_GLOBAL,
     {}, {2: SPAWN, 4: 0x137, 5: ROW},
     "Separate post-spawn-call cut; no earlier callback return or preserved registers are inferred"),
    ("debris135-capacity-branch", 0x80084350, 0x8008436C, SPAWN_GLOBAL,
     {2: 0, 16: 9}, {2: SPAWN, 4: 0x135, 5: ROW, 16: 10},
     "Explicit capacity-comparison result v0=0; earlier comparison is not executed"),
    ("line46-post-call-loop-exit", 0x80084374, 0x80084394, PARTICLE_GLOBAL,
     {16: 10}, {2: PARTICLE, 4: 0x20, 5: 0x46, 6: ROW + 0xC, 7: 0x18},
     "Separate post-spawn-call cut with s0=10; actual SLTI/BNE and count32 delay execute"),
    ("particle0C-low-flags", 0x80084424, 0x80084454, PARTICLE_GLOBAL,
     {4: 1, 19: VECTOR}, {2: PARTICLE, 5: 0xC, 6: VECTOR, 7: 0x8080},
     "Explicit incoming a0=1 and s3=owned synthetic vector; preceding condition path is not executed"),
    ("particle0C-high-flags", 0x8008443C, 0x80084454, PARTICLE_GLOBAL,
     {4: 1, 19: VECTOR}, {2: PARTICLE, 5: 0xC, 6: VECTOR, 7: 0x05008080},
     "Explicit incoming a0=1 and s3=owned synthetic vector; alternate preceding condition is not executed"),
)


def sha(data): return hashlib.sha256(data).hexdigest()
def hx(value): return f"{value:08X}"
def pack(words): return struct.pack("<" + "I" * len(words), *words)


def require(ok, label):
    if not ok: raise AssertionError(label)


def guard_pins():
    for path, digest in PINS.items(): require(sha((ROOT / path).read_bytes()) == digest, "Frozen source dependency " + path)


def load(path, name):
    require(sha((ROOT / path).read_bytes()) == PINS[path], "Pinned definition import")
    spec = importlib.util.spec_from_file_location(name, ROOT / path)
    module = importlib.util.module_from_spec(spec); spec.loader.exec_module(module); return module


class TraceCode(dict):
    def __init__(self, code): super().__init__(code); self.trace = []
    def __getitem__(self, pc): self.trace.append(pc); return super().__getitem__(pc)


def witness(owners, inspector, prior, reference, host, linked, Machine, check, refuse):
    inspected = inspector.inspect(owners, prior, reference)
    source_code, span_reports = {}, []
    for pc, words in SOURCE_SPANS:
        data = prior.extent(owners, "peace", pc, len(words) * 4)
        check(data == pack(words), "Complete456 source words " + hx(pc))
        for i, instruction in enumerate(words):
            require(pc + i * 4 not in source_code, "Disjoint executable cut owners")
            source_code[pc + i * 4] = instruction
        span_reports.append(dict(pc=hx(pc), byteLength=len(data), sha256=sha(data), bytesHex=data.hex()))
    # Pure original-only450 composition, never Core or candidate output.
    base = linked.compose(host, owners["town"], owners["wizard"], owners["exe"])
    output, mapping, _, _, _ = reference.extension(owners, base["bytes"], prior)
    check(sha(output) == OVERLAY_SHA and sha(reference.INITIALIZER) == INITIALIZER_SHA, "Exact pre-producer450 overlay/initializer controls")
    final_code = {mapping[pc]: struct.unpack_from("<I", output, mapping[pc] - BASE)[0] for pc in source_code}
    init_results, initializer_reports = {}, []
    for row in inspected["initializers"]:
        entry = int(row["entryPc"], 16); data = bytes.fromhex(row["bytesHex"])
        # This executes to the real outgoing tail ENTRY, never its return/body.
        m = Machine(TraceCode({entry + at: struct.unpack_from("<I", data, at)[0] for at in range(0, len(data), 4)}))
        stores = {int(x["globalAddress"], 16): int(x["callbackValue"], 16) for x in row["callbackStores"]}
        for at in [0x800785D8, 0x8007596C, *stores]: m.map(at, 4, 0xA5)
        m.poke(0x8007596C, 65 if "Fresh65" in row["id"] else 13 if "Town13" in row["id"] else 20)
        before = m.memory.copy(); m.r = [0] + [0x51000000 + i * 0x10101 for i in range(1, 32)]
        m.hi, m.lo = 0x12345678, 0x89ABCDEF
        # Peace ends at its own native tail, decoded from the exact final J.
        jump_word = struct.unpack_from("<I", data, 0x90)[0]
        require(jump_word >> 26 == 2 and struct.unpack_from("<I", data, 0x94)[0] == 0, "Initializer terminal J/NOP")
        stop = ((entry + 0x94) & 0xF0000000) | ((jump_word & 0x3FFFFFF) << 2)
        check(m.run(entry, {stop}, maximum=64) == stop and stop not in m.code.trace, "Actual initializer reaches unexecuted native tail " + row["id"])
        expected = before.copy()
        for at, n, value in m.writes:
            require(n == 4 and at in [0x800785D8, *stores], "Initializer writes only nine declared singleton owners")
            for i in range(n): expected[at + i] = value >> (i * 8) & 255
        check(m.memory == expected and len(m.writes) == 9 and {at for at, _, _ in m.writes} == {0x800785D8, *stores}
              and all(int.from_bytes(m.bytes(at, 4), "little") == value for at, value in stores.items())
              and (m.hi, m.lo) == (0x12345678, 0x89ABCDEF), "Nine real initializer stores including all eight callbacks " + row["id"])
        init_results[row["id"]] = m
        initializer_reports.append(dict(id=row["id"], sha256=sha(data), stop=hx(stop), writes=[dict(address=hx(at), value=hx(value)) for at, _, value in m.writes],
                                        instructions=m.instructions, callbackBodiesExecuted=False))
    contexts = ("original-Town13", "current-Fresh65-reference", "new-detached-Fresh65-reference")
    cases = []
    def fixture(code, initializer, inputs):
        m = Machine(TraceCode(code)); m.memory = initializer.memory.copy()
        m.map(ROW, 0x58, 0xA6); m.map(VECTOR, 12, 0x6B); m.map(STACK - 0x20, 0x40, 0xA7)
        m.r = [0] + [0x52000000 + i * 0x10101 for i in range(1, 32)]
        m.r[19] = m.r[20] = ROW; m.r[29] = STACK
        for reg, value in inputs.items(): m.r[reg] = value
        m.hi, m.lo = 0x12345678, 0x89ABCDEF; return m
    for label, entry, call, global_at, inputs, changed, condition in CUTS:
        results = []
        for context in contexts:
            initializer = init_results[context]
            # Globals are copied from actual executed initializer stores only.
            target = int.from_bytes(initializer.bytes(global_at, 4), "little")
            require(target == (SPAWN if global_at == SPAWN_GLOBAL else PARTICLE), "Actual target-host callback owner")
            for relocated, code in ((False, source_code), (True, final_code)):
                m = fixture(code, initializer, inputs); before, memory = m.r.copy(), m.memory.copy()
                expected = before.copy()
                for reg, value in {**changed, 31: (mapping[call] if relocated else call) + 8}.items(): expected[reg] = value
                start = mapping[entry] if relocated else entry; call_pc = mapping[call] if relocated else call
                check(m.run(start, {target}, maximum=32) == target and m.r == expected and m.memory == memory
                      and m.writes == [] and m.reads == [(global_at, 4)] and m.calls == [(call_pc, target)]
                      and m.instructions == len(m.code.trace) and target not in m.code.trace
                      and (m.hi, m.lo) == (0x12345678, 0x89ABCDEF), "Complete entry registers/arguments/memory/load/link/delay " + str((label, context, relocated)))
                results.append(m)
                cases.append(dict(id=label, initializer=context, relocated=relocated, entry=hx(start), callPc=hx(call_pc),
                                  target=hx(target), sourceEntryCondition=condition, args=[hx(x) for x in m.r[4:8]],
                                  link=hx(m.r[31]), instructions=m.instructions,
                                  trace=[hx(pc) for pc in m.code.trace], callbackBodyExecuted=False))
            original, final = results[-2:]; normalized = final.r.copy(); normalized[31] = original.r[31]
            check(normalized == original.r and final.memory == original.memory
                  and final.code.trace == [mapping[pc] for pc in original.code.trace], "Exact relocated trace and complete-state differential " + str((label, context)))
    initializer = init_results[contexts[-1]]
    missing = fixture(source_code, initializer, {2: 0}); del missing.memory[SPAWN_GLOBAL]
    refuse(lambda: missing.run(0x800842EC, {SPAWN}), "Unmapped native read", "Callback global cannot be synthesized")
    wrong = fixture(source_code, initializer, {2: 0}); wrong.poke(SPAWN_GLOBAL, 0x8008A258)
    refuse(lambda: wrong.run(0x800842EC, {SPAWN}), "Unowned native instruction", "Original Peace callback is not a target Town entry")
    missing_delay = fixture(final_code, initializer, {2: 0}); del missing_delay.code[mapping[0x80084308]]
    refuse(lambda: missing_delay.run(mapping[0x800842EC], {SPAWN}), "Unowned native instruction", "Missing JALR increment delay")
    killed_load = fixture(source_code, initializer, {2: 0}); killed_load.code[0x80084300] = 0x3C020000
    refuse(lambda: killed_load.run(0x800842EC, {SPAWN}), "Unowned native instruction", "Direct v0 write cancels pending callback load")
    return dict(marker="FRESH_CHEST_CALLBACK_ENTRY_WITNESS_OK", sourceWindows=span_reports, initializerExecutions=initializer_reports,
                entryCases=cases, overlaySha256=sha(output), initializerSha256=INITIALIZER_SHA,
                sourceCodeUsesTargetTownGlobals=True, originalPeaceRuntimeExecuted=False,
                callbackReturnsAssumed=False, callbackBodiesExecuted=False, fullHandlerExecuted=False,
                modelRootsOrRendererCertified=False, bodyOrSceneIssued=False, runtime=False, publication=False)


def main(path):
    require(__debug__, "Python assertions enabled"); guard_pins()
    inspector = load(INSPECTOR, "callback_entry_frozen456")
    reference = inspector.load(inspector.REFERENCE, "callback_entry_frozen450")
    prior = inspector.load(inspector.CONTINUATION, "callback_entry_frozen436")
    raw = inspector.load(inspector.READER, "callback_entry_original_reader")
    host = prior.load(list(prior.HELPERS)[1], "callback_entry_host_definitions")
    linked = reference.load(reference.LINKED, "callback_entry_frozen_link")
    scalar = load(MACHINE, "callback_entry_current_pinned_machine")
    Machine = load(JALR_ISA, "callback_entry_jalr").bind(load(REWARD_ISA, "callback_entry_reward_isa").bind(scalar))
    checks, refusals = [], []
    def check(ok, label): require(ok, label); checks.append(label)
    def refuse(action, phrase, label):
        try: action()
        except AssertionError as error:
            require(phrase in str(error), label + ": wrong refusal: " + str(error)); refusals.append(label); return
        raise AssertionError(label + ": accepted negative control")
    load(ISA_CONTROLS, "callback_entry_frozen_isa_controls").isa(Machine, check, refuse)
    def guards():
        guard_pins(); inspector.guard_pins(); reference.guard_pins(); prior.require_helpers(); scalar.require_helpers(); host.require_helpers()
        # The linker's Host interpreter transitively pins its own source inputs.
        for p, digest in reference.PINS.items(): require(sha((ROOT / p).read_bytes()) == digest, "Frozen450 dependency after source")
        for p, digest in linked.PINS.items(): require(sha((ROOT / p).read_bytes()) == digest, "Frozen linked dependency after source")
    guards()
    specs = {**prior.OWNERS, "wizard": (37 * 2048 + 0x2F5B000, 0x10000,
             "5d3996111567ffac6eca08d340f2f691824bb7969ba2a0cd95e9346ccfe157e1")}
    with open(path, "rb") as image:
        initial = inspector.identity(os.fstat(image.fileno()))
        def same():
            require(inspector.identity(os.fstat(image.fileno())) == initial and inspector.identity(os.stat(path)) == initial,
                    "Held/named original identity")
        same(); raw.identity(image)
        try:
            directory = raw.logical(image, 37 * 2048, 0x800)
            require(sha(directory) == "15d3e9c45f07e27a0ddd3f3a49b23a077e2c7750021cc89c55b7b16c551186cd", "Whole original WAD directory")
            owners = {name: raw.logical(image, at, n) for name, (at, n, _) in specs.items()}
            for name, (_, n, digest) in specs.items(): require(len(owners[name]) == n and sha(owners[name]) == digest, "Whole source owner " + name)
            report = witness(owners, inspector, prior, reference, host, linked, Machine, check, refuse)
            report.update(sourceImageSha256=raw.IMAGE_SHA, helperPins=PINS, checkCount=len(checks), checks=checks,
                          refusalCount=len(refusals), refusals=refusals,
                          originalOwners=[dict(id=name, logicalOffset=at, byteLength=n, sha256=digest) for name, (at, n, digest) in specs.items()])
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
