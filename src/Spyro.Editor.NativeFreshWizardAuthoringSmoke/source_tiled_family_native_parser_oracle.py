#!/usr/bin/env python3
"""Original native parser on independent child-relative four-tile family data.

The preserved variable/P0 control executes unchanged first. The NEW family
witness executes only data/sound parsing and the real caller return/store.
No family model, Scene, data padding, GPU/SPU/CD hardware or runtime is mapped
or executed. The child reference is not independent Original asset provenance.
Four exact input files and all loaded definitions are guarded before/after.
"""
import contextlib
import hashlib
import importlib.util
import io
import json
import os
from pathlib import Path
import struct
import sys

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[2]
ORIGINAL_SHA = "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37"
ORIGINAL_CUE_SHA = "05142502c75d3a0b6374003a8fe41893d6faf0ad55863ff0b62a265a1b152766"
CURRENT_EXE_SHA = "81501f07ec9ce336089ec5a92f81bf51569aed723ac4f8ba1cf85ba0ef69a431"
COPY, STOP = 0x8008CA38, 0x80015904
GLOBAL_WORDS = (0x800758BC, 0x800761D0, 0x800761D4, 0x80076234,
                0x80077780, 0x80077784, 0x80077788, 0x8007778C, 0x80077790,
                0x800785A8, 0x800785AC, 0x800785B0, 0x800785B4, 0x800785B8,
                0x800785BC, 0x800785C0, 0x800785C4, 0x800785C8, 0x800785D4,
                0x800785DC, 0x800785E0, 0x80078A40)
CHILD = "src/Spyro.Editor.NativeFreshWizardAuthoringSmoke/source_tiled_family_data_child_oracle.py"
LOADER = "src/Spyro.Editor.NativeId65FreshDocumentSmoke/source_variable_native_loader_oracle.py"
HELPERS = {
    CHILD: "4f0ddbd810ae40ee10bd96c58d92fd4c46e8af1e094a6c309bc43c6e5e6d7ede",
    LOADER: "75f6762822c79520176bb3127e9b426822fb7b007b2ba6da931e4822cb43f283",
}
SPAN_NAMES = ("sound-pointer-initializer", "complete-data-parser", "actual-data-call-and-return-store")
# Frozen after actual Original instructions executed the independent new input;
# never derived from Core. --derive does not bypass these frozen measurements.
PINS = {
    "usedByteLength": 0xCB8, "modelEndpoint": 0x8008D6F0, "instructions": 1077,
    "soundEntryArguments": [0x8008CFD4, 1, 0x8008CFD0, 0],
    "loadedDataSha256": "5a1547251ca6326d2cc8c9f414e8c9090b6385071dc55832cdbe009c811d7109",
    "writtenOwnerCount": 124, "nativeReadCount": 228, "soundDescriptorCount": 78,
    "environmentPointers": [0x8008CBC8, 0x8008CC68, 0x8008CD08, 0x8008CDA8],
    "structuralFaceValues": [0, 0, 0, 1, 0, 1, 1, 1],
    "effectiveSoundOffsetAdjustment": 0x1010, "nativeFaceMaterialReads": [],
    "executedPcSha256": "819f0cf9075906ee4726951eeb0ef90aeb90200f210754305edf5391c3db9209",
    "completeMemoryAndRegisterComplement": True, "memoryKeysUnchanged": True,
    "followingDataPaddingMapped": False, "expectedModelEndpointSeeded": False,
    "renderOrCollisionQueryExecuted": False,
}


def require(ok, label):
    if not ok:
        raise AssertionError(label)


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def word(raw, at=0):
    require(type(at) is int and at >= 0 and at % 4 == 0 and at + 4 <= len(raw), "Bounded aligned immutable word")
    return struct.unpack_from("<I", raw, at)[0]


def load(relative, expected, name):
    path = ROOT / relative
    require(sha(path.read_bytes()) == expected, "Exact helper before import: " + relative)
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    previous = sys.argv
    try:
        sys.argv = [str(path)]
        with contextlib.redirect_stdout(io.StringIO()):
            spec.loader.exec_module(module)
    finally:
        sys.argv = previous
    return module


def definitions(before, manifest):
    require(Path(__file__).read_bytes() == before, "New parser witness definition unchanged")
    for relative, expected in manifest.items():
        require(sha((ROOT / relative).read_bytes()) == expected, "Complete loaded helper definition: " + relative)


def code_owners(original, current, loader):
    require(len(original) == len(current) == 0x66000 and sha(original) == loader.native.EXE_SHA and
            sha(current) == CURRENT_EXE_SHA, "Exact complete Original and retained child executables")
    code, windows = {}, []
    for name, pc, length, expected in loader.SPANS:
        if name not in SPAN_NAMES:
            continue
        at = pc - 0x8000F800
        raw = original[at:at + length]
        require(len(raw) == length and sha(raw) == expected and current[at:at + length] == raw,
                "Actual Original parser bytes are preserved in the exact child executable: " + name)
        windows.append(dict(name=name, pc=pc, byteLength=length, sha256=expected))
        for offset in range(0, length, 4):
            require(pc + offset not in code, "Disjoint complete native instruction owners")
            code[pc + offset] = word(raw, offset)
    require(len(windows) == 3 and len(code) == (0x68 + 0x4D8 + 0x24) // 4,
            "Only parser, sound helper and actual call/return/store are executable")
    return code, windows


def machine(data, code, loader):
    # Use the unchanged Machine, but ONLY the source-observed parser globals,
    # particle table and exact8-byte frame, not the old model/header/Scene map.
    # Poison unused values; 78A40=0 is an explicit synthetic external caller
    # scalar, not an observed runtime or SPU base. No padding/model/Scene bytes
    # follow used data. Sound's1010 adjustment is a native literal, not this slot.
    m = loader.Machine(code)
    for at in GLOBAL_WORDS:
        m.map(at, 4, 0xA5)
    m.map(0x80076278, 0x100, 0xA5)
    m.map(loader.STACK - 8, 8, 0xA5)
    m.r[29], m.r[31] = loader.STACK, loader.RETURN
    m.blob(COPY, data)
    m.poke(0x80078A40, 0)
    m.poke(0x800785DC, COPY)
    m.poke(0x800785E0, 0xA5A5A5A5)
    m.r[16] = 0x12345678
    require(COPY + len(data) not in m.memory and COPY - 1 not in m.memory,
            "Used data has no mapped prefix/suffix guard, sector padding or following model")
    return m


def run_parser(m):
    m.run(0x800158E0, {0x80012CF0}, maximum=10000)
    args = tuple(m.r[4:8])
    m.run(0x80012CF0, {STOP}, maximum=10000)
    return args


def execute(data, code, loader):
    require(type(data) is bytes and len(data) == 0xCB8 and sha(data) ==
            "fe754f8e5e4686fdfe03d7e516dab8f1b52ad605b1541615e98d3f0511e4835e",
            "Complete independent child-relative data before native execution")
    m = machine(data, code, loader)
    before = dict(m.memory)
    # Stop at the ACTUAL called sound-helper entry, after the caller delay, so
    # arguments are observed from native code; no return value is manufactured.
    sound_args = run_parser(m)
    native_reads = tuple(m.reads)
    require(m.r[29] == loader.STACK and m.r[16] == 0x12345678 and
            m.calls == [(0x800158E8, 0x80012D58), (0x80013208, 0x80012CF0)],
            "Actual caller/parser/helper calls, both control delays, real return and stack/s0 preservation")
    require(m.r[2] == m.read(0x800785E0) == COPY + len(data) == 0x8008D6F0,
            "Native returned and stored model endpoint is executed, never seeded")
    require(m.read(0x800785C0) == COPY + 8 and m.read(0x800785C4) == COPY + 0x28 and
            m.read(0x800785C8) == 2, "Both native split LQ/HQ pointers and actual two-record count")
    collision = COPY + 0x43C + 4
    require(m.read(0x800785D4) == collision and all(m.read(collision + at) == collision + word(data, 0x440 + at)
            for at in (8, 12, 16, 20, 24)), "All five native collision-pointer relocations")
    sound_at = COPY + 0x598
    require(m.read(0x800761D4) == sound_at + 4 and m.read(0x800761D0) == sound_at + 0x108 and
            word(data, 0x69C) == 78 and all(m.read(sound_at + 0x108 + 20 * i) ==
                word(data, 0x6A0 + 20 * i) + 0x1010 for i in range(78)),
            "Actual sound helper rebases all78 descriptors and both pointer globals; not old P0 count76")
    require(sound_args == (sound_at + 4, 1, sound_at, 0),
            "Native parser produces actual helper arguments; synthetic78A40=0 input remains explicit")
    require(code[0x80012D44] == 0x24421010 and code[0x80012D0C] == 0x10A00010 and
            code[0x80012D10] == 0,
            "Sound1010 adjustment is actual ADDIU literal under nonzero a1, with actual branch delay; not a simulated SPU base")
    require(COPY + len(data) not in m.memory and set(m.memory) == set(before),
            "Execution never obtains any new or following data/model/Scene owner")
    expected_writes = [(loader.STACK - 8, 4, 0x12345678), (loader.STACK - 4, 4, 0x800158F0)]
    expected_globals = {
        0x800785C0: COPY + 8, 0x800785C8: 2, 0x800785C4: COPY + 0x28,
        0x800785A8: COPY + 0x180, 0x800785AC: 4,
        0x800785B0: COPY + 0x41C, 0x800785B4: 1,
        0x8007778C: COPY + 0x42C, 0x80077788: 1,
        0x800785B8: COPY + 0x43C, 0x800785BC: 0, 0x800785D4: collision,
        0x80077784: COPY + 0x504, 0x80077780: 0, 0x800758BC: 0,
        0x80076234: 0x720, 0x800761D4: sound_at + 4, 0x800761D0: sound_at + 0x108,
        0x800785E0: COPY + len(data),
    }
    expected_writes += [(at, 4, value) for at, value in expected_globals.items()]
    expected_writes += [(0x80077790 + i, 1, value) for i, value in enumerate((128, 96, 64))]
    # The descriptor table points at each complete12-byte record HEADER, not
    # at its8-byte payload. All other64-slot table bytes remain poisoned.
    ids = (0x15, 0x18, 2, 0x21, 0xC, 0xB, 0xA, 9, 1, 0, 7)
    particle_rows = [(particle, COPY + 0x514 + 12 * i) for i, particle in enumerate(ids)]
    expected_writes += [(0x80076278 + 4 * particle, 4, at) for particle, at in particle_rows]
    relocations = [(0x180 + 4 * i, COPY + 0x17C + word(data, 0x180 + 4 * i)) for i in range(4)]
    relocations += [(0x41C, COPY + 0x414 + word(data, 0x41C)),
                    (0x42C, COPY + 0x428 + word(data, 0x42C))]
    relocations += [(0x440 + at, collision + word(data, 0x440 + at)) for at in (8, 12, 16, 20, 24)]
    relocations += [(0x6A0 + 20 * i, word(data, 0x6A0 + 20 * i) + 0x1010) for i in range(78)]
    expected_writes += [(COPY + at, 4, value) for at, value in relocations]
    require(len(m.writes) == len(expected_writes) and sorted(m.writes) == sorted(expected_writes),
            "Every native write matches independent global/particle/pointer/sound formulas; no extra owner is changed")
    expected_memory = dict(before)
    for at, size, value in expected_writes:
        for i in range(size):
            require(at + i in expected_memory, "Expected write stays in its predeclared owned fixture")
            expected_memory[at + i] = value >> (8 * i) & 255
    require(m.memory == expected_memory, "Complete mapped memory complement including all unused particle/global/stack bytes")
    expected_registers = [0] * 32
    for index, value in {1: 0x80080000, 2: COPY + len(data), 4: 0xFFFFFFEC, 5: 1,
                         6: 0x80078A40, 16: 0x12345678, 29: loader.STACK, 31: 0x800158F0}.items():
        expected_registers[index] = value
    require(m.r == expected_registers and m.hi == m.lo == 0, "Complete returned scalar register/HI/LO state")
    sectors = [m.read(COPY + 0x180 + 4 * i) for i in range(4)]
    require(sectors == [COPY + 0x190 + 160 * i for i in range(4)], "All four native environment directory pointers")
    # The parser does not render faces or query collision triangles. The eight
    # words and sector guards below are structural retained-byte readback only.
    face_offsets = [0x178 + at for at in (156, 172, 316, 332, 476, 492, 636, 652)]
    face_values = [word(m.bytes(COPY, len(data)), at) for at in face_offsets]
    require(face_values == [0, 0, 0, 1, 0, 1, 1, 1] and
            all(word(m.bytes(COPY, len(data)), at + 4) == 0x80 for at in face_offsets) and
            all(m.bytes(at + 156, 4) == bytes(4) for at in sectors),
            "Structural readback of all eight retained materials/flags and four sector guards, not rendering")
    face_native_reads = [at for at in face_offsets if any(
        read < COPY + at + 4 and COPY + at < read + size for read, size in native_reads)]
    require(not face_native_reads, "No face-material field was read by this native parser path")
    return dict(usedByteLength=len(data), modelEndpoint=m.r[2], instructions=m.instructions,
                soundEntryArguments=list(sound_args), calls=m.calls,
                loadedDataSha256=sha(m.bytes(COPY, len(data))),
                registers=m.r, writtenOwnerCount=len(expected_writes),
                particlePointers=particle_rows, soundDescriptorCount=78,
                environmentPointers=sectors, structuralFaceValues=face_values,
                externalCallerScalarFixture=dict(address=0x80078A40, value=0, observedRuntimeState=False),
                effectiveSoundOffsetAdjustment=0x1010,
                soundOffsetAdjustmentInstruction=dict(pc=0x80012D44, word=0x24421010),
                nativeFaceMaterialReads=face_native_reads, renderOrCollisionQueryExecuted=False,
                completeMemoryAndRegisterComplement=True, nativeReadCount=len(native_reads),
                executedPcSha256=sha(b"".join(struct.pack("<I", pc) for pc in sorted(m.executed))),
                memoryKeysUnchanged=True, followingDataPaddingMapped=False,
                expectedModelEndpointSeeded=False), m


def negatives(data, code, original, current, loader, neutral):
    refused, demonstrated = [], []
    def reject(label, action, phrase):
        try:
            action()
        except AssertionError as error:
            require(phrase in str(error), "Exact negative refusal: " + label + ": " + str(error))
            refused.append(label)
            return
        raise AssertionError("Negative accepted: " + label)
    def raw_run(value, program=code):
        m = machine(value, program, loader)
        run_parser(m)
        return m
    for name, pc, length, _ in loader.SPANS:
        if name in SPAN_NAMES:
            changed = bytearray(original)
            changed[pc - 0x8000F800 + length - 1] ^= 1
            reject("source-window-" + name, lambda changed=changed: code_owners(bytes(changed), current, loader), "Exact complete")
    for at, value, phrase, label in ((0x178, 665, "Unaligned", "unaligned-environment"),
                                   (0x178, 0x10000, "Unmapped", "out-of-owner-component"),
                                   (0x69C, 79, "Unmapped", "79th-unowned-sound-descriptor")):
        bad = bytearray(data)
        struct.pack_into("<I", bad, at, value)
        reject(label, lambda bad=bad: raw_run(bytes(bad)), phrase)
    bad_particle = bytearray(data)
    struct.pack_into("<h", bad_particle, 0x514, 64)
    reject("particle64-no-model-table-owner", lambda: raw_run(bytes(bad_particle)), "Unmapped")
    reject("truncated-sound-descriptor-start", lambda: raw_run(data[:-17]), "Unmapped")
    for pc in (0x80012CF0, 0x8001322C, 0x80015900):
        program = dict(code)
        del program[pc]
        reject("missing-callee-delay-store-" + hex(pc), lambda program=program: raw_run(data, program), "Unowned native instruction")
    missing_global = machine(data, code, loader)
    del missing_global.memory[0x80078A40]
    reject("unmapped-explicit-caller-scalar", lambda: run_parser(missing_global), "Unmapped native read")
    budget = machine(data, code, loader)
    reject("finite-instruction-budget", lambda: budget.run(0x800158E0, {STOP}, maximum=8), "budget")
    for at, label in ((0x45C, "tree-not-queried"), (0x214, "face-not-rendered")):
        bad = bytearray(data)
        bad[at] ^= 1
        reject(label + "-structural-refusal", lambda bad=bad: neutral.validate_surface(bytes(bad[0x178:0x508]), True), "")
        require(raw_run(bytes(bad)).r[2] == COPY + len(data), "Pointer parser still returns on unexamined " + label)
        demonstrated.append(label)
    require(raw_run(data[:-1]).r[2] == COPY + len(data),
            "Parser can return beyond an unread truncated descriptor tail; complete input ownership remains essential")
    demonstrated.append("unread-tail-truncation-not-native-validation")
    program = dict(code)
    program[0x80015900] = 0
    skipped = raw_run(data, program)
    require(skipped.r[2] == COPY + len(data) and skipped.read(0x800785E0) == 0xA5A5A5A5,
            "Missing native endpoint store cannot be hidden by seeding its expected value")
    demonstrated.append("missing-endpoint-store-poison-survives")
    return dict(refusals=refused, parserIsNotValidationControls=demonstrated)


def main(argv=None):
    args = list(sys.argv[1:] if argv is None else argv)
    require(len(args) == 5 and args[0] in ("--derive", "--witness"),
            "Usage: --derive/--witness absolute-Original.bin Original.cue exact-child.bin child.cue")
    mode, *names = args
    paths = [Path(value) for value in names]
    require(all(path.is_absolute() for path in paths) and paths[0].parent == paths[1].parent and
            paths[2].parent == paths[3].parent and paths[0].name == "Spyro the Dragon (USA).bin" and
            paths[1].name == "Spyro the Dragon (USA).cue" and paths[2].name == "native-fresh-saved-level-r01.bin" and
            paths[3].name == "native-fresh-saved-level-r01.cue", "Only explicit exact paired input names; no output path")
    require(__debug__, "Unchanged preserved Python assertions must execute")
    before = Path(__file__).read_bytes()
    child = load(CHILD, HELPERS[CHILD], "family_parser_child_reference")
    loader = load(LOADER, HELPERS[LOADER], "family_parser_existing_machine")
    manifest = dict(HELPERS)
    for dependencies in (child.HELPERS, loader.HELPERS):
        for relative, expected in dependencies.items():
            require(relative not in manifest or manifest[relative] == expected, "Consistent transitive helper pins")
            manifest[relative] = expected
    definitions(before, manifest)
    reader_path, neutral_path = list(child.HELPERS)[:2]
    reader = load(reader_path, manifest[reader_path], "family_parser_raw_reader")
    neutral = load(neutral_path, manifest[neutral_path], "family_parser_neutral_surface")
    held, report = [], None
    with contextlib.ExitStack() as stack:
        try:
            specs = ((child.IMAGE_LENGTH, ORIGINAL_SHA), (88, ORIGINAL_CUE_SHA),
                     (child.IMAGE_LENGTH, child.IMAGE_SHA), (len(child.CUE_BYTES), sha(child.CUE_BYTES)))
            for path, (length, digest) in zip(paths, specs):
                stream = stack.enter_context(path.open("rb"))
                initial = child.identity(os.fstat(stream.fileno()))
                held.append((stream, path, initial, length, digest))
                child.authenticate(stream, path, initial, length, digest)
            # The unchanged P0/legacy/model/Scene helper control executes first,
            # separately from the new parser-only family case below.
            output = io.StringIO()
            with contextlib.redirect_stdout(output):
                loader.main(str(paths[0]))
            control = json.loads(output.getvalue())
            require(control["marker"] == "VARIABLE_NATIVE_LOADER_ORACLE_OK" and
                    control["coordinate_sums"] == 190 and control["distinct_used_lengths"] == 96 and
                    control["native_parser_and_sound_helper_executed"] and
                    control["tiled_cases"][0]["model"] == "0x8008a924", "Preserved P0/legacy controls execute unchanged first")
            neutral.preserved_control()
            neutral.validate_document(neutral.CANONICAL)
            allocated, extraction = child.extract(held[2][0], reader)
            child_report = child.reference(allocated, neutral)
            require(all(child_report[key] == value for key, value in child.PINS.items()),
                    "Every frozen independent child-relative data pin and inverse passes")
            data, _, _ = child.compose(allocated, neutral)
            original = reader.logical(held[0][0], 53875 * 2048, 0x66000)
            current = reader.logical(held[2][0], (37 + extraction["wadByteLength"] // 2048) * 2048, 0x66000)
            code, windows = code_owners(original, current, loader)
            require(0x8007AA38 + extraction["hostByteLength"] == COPY and
                    current[0x4B0B8:0x4B0BC] == bytes.fromhex("38ca4224"),
                    "Retained12000 Host end and actual current initializer copy-anchor instruction agree")
            case, m = execute(data, code, loader)
            controls = negatives(data, code, original, current, loader, neutral)
            require(PINS and all(case[key] == value for key, value in PINS.items()), "Native parser references must be frozen and match")
            report = dict(marker="TILED_FAMILY_NATIVE_PARSER_ORACLE_OK" if mode == "--witness" else
                          "TILED_FAMILY_NATIVE_PARSER_MEASURED_UNFROZEN", oracleSha256=sha(before), helperPins=manifest,
                          preservedControl=dict(marker=control["marker"], checks=control["checks"],
                                                refusals=control["expected_refusals"], coordinateSums=190, distinctUsedLengths=96),
                          originalSha256=ORIGINAL_SHA, originalCueSha256=ORIGINAL_CUE_SHA,
                          childSha256=child.IMAGE_SHA, childCueSha256=sha(child.CUE_BYTES),
                          childReferenceSha256=child_report["newUsedSha256"], sourceWindows=windows, case=case,
                          familyNegativeControls=controls,
                          soundHelperWords=[dict(pc=pc, word=code[pc]) for pc in range(0x80012CF0, 0x80012D58, 4)],
                          childRelativeAssetProvenance=True, independentOriginalAssetClosureProven=False,
                          sourceCertified=False, bodyIssued=False, discIssued=False,
                          nativeDataAndSoundParserExecuted=True, fullNativeLevelLoaderExecuted=False,
                          familyModelOrSceneExecuted=False, familyModelOrSceneOrPaddingMapped=False,
                          hostInitializerExecuted=False, copyPointerFixtureInput=COPY,
                          cdOrGpuOrSpuHardwareExecuted=False, runtimeVerified=False, publicationApproved=False,
                          completeNativeDependencyClosureProven=False)
        finally:
            failures = []
            for stream, path, initial, length, digest in held:
                try:
                    child.authenticate(stream, path, initial, length, digest)
                    with path.open("rb") as reopened:
                        child.authenticate(reopened, path, initial, length, digest)
                except BaseException as error:
                    failures.append(error)
            try:
                definitions(before, manifest)
            except BaseException as error:
                failures.append(error)
            if failures:
                raise BaseExceptionGroup("Final whole Original/child BIN/CUE and all helper/self guards", failures)
    report["completeBeforeAfterReopenedFourFileGuards"] = True
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
