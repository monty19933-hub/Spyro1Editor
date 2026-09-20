#!/usr/bin/env python3
"""Actual integer model parsers on a child-relative complete Body reference.

Shared PETE and local registration entry cuts are explicit; CD/header delivery
is byte mapping, not a fabricated helper return. No Scene, render, gameplay or
hardware executes. All exact inputs and loaded definitions are finalized.
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
PARSER = "src/Spyro.Editor.NativeFreshWizardAuthoringSmoke/source_tiled_family_native_parser_oracle.py"
BODY = "src/Spyro.Editor.NativeFreshWizardAuthoringSmoke/source_tiled_family_body_child_oracle.py"
HELPERS = {PARSER: "34c598482dcb1e2798f92ab17a8105cec0b9f4058759c10c17408c7a37840a5e",
           BODY: "223a05de692ab0e4f9a660d62842f1805ac4d9bc13329653b147d6888a62b06a"}
MODEL_SPANS = ("special-player-stream-loader", "complete-generic-model-loader", "registration-and-scene-base")
PETE_SHA = "1703ac37e59e480909b6f66dc856d4e5f3ce80551f18cbc8c06129c3b54dd35d"
PETE_HEADER_SHA = "b9a4de193b5cf0bd9592de15c57aa5daa3713c3575026154b45402f3aceef6f1"
HEADER_FIXTURE = 0x80100000
MODEL_STACK = 0x801FFF00  # Disjoint from actual resident payload ending801FF800.
# Frozen after successful source execution, independent of Core outputs.
PINS = {
    "sharedInstructions": 11127,
    "residentBase": 2149342136,
    "sharedRoot0Seeded": False,
    "localPlayerAnimationIds": [
        18,
        19,
        29,
        40,
        8
    ],
    "modelBase": 2148062960,
    "sceneBase": 2148173552,
    "localChainInstructions": 15612,
    "modelSha256": "b3fdc6639588149657de506300930e5f96cb69dc3b02a4362fac52a7c3f764bf",
    "sharedRelocatedSha256": "f464a11a8910261b5c2e79e2b161a6cb0503392b825a436c2ef780c25c7f563b",
    "relocatedModelSha256": "257dc6fb534ff0cc529206238626d941799b5acb08bcc02bb09fc39a797bff52",
    "finalResidentSha256": "57eea4581ba06a6215d518ecbfb3e9004ddfbcb481a25a824d056d4eed261b47",
    "classTableSha256": "2061cc3d41dba04776fbcf342439fa58c2cc128f89cb849d48a30111a92835bc",
    "sharedWrites": 1153,
    "localModelWrites": 971,
    "sharedRegisters": [
        0,
        2148007936,
        0,
        2147969888,
        2149575840,
        2149344864,
        26,
        2149342320,
        2149575100,
        2149575100,
        2149342260,
        46,
        2097151,
        4292870144,
        0,
        0,
        2148532656,
        2149342136,
        54,
        2147967864,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        2149580496,
        0,
        2147858584
    ],
    "localRegisters": [
        0,
        2147942400,
        2148173552,
        1,
        110592,
        14336,
        1056768,
        2148172532,
        2148172528,
        2148172528,
        15892,
        1,
        2097151,
        4292870144,
        0,
        0,
        12,
        2147970180,
        2147970408,
        2147970128,
        2147967864,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        2149580544,
        0,
        2147572500
    ],
    "sharedHiLo": [
        0,
        0
    ],
    "localHiLo": [
        0,
        0
    ],
    "exactMemoryAndWriteComplement": True,
    "sceneMapped": False,
    "modelPhysicalPaddingMapped": True,
    "dataAllocationPaddingMapped": False,
    "retiredParserFrameImported": False,
    "disjointPhysicalOwnerMappings": True,
    "completeImmediateRelocationInverse": True,
    "expectedSceneEndpointSeeded": False
}


def require(ok, label):
    if not ok:
        raise AssertionError(label)


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def load(relative, expected, name):
    path = ROOT / relative
    require(sha(path.read_bytes()) == expected, "Pinned definition before import: " + relative)
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec)
    previous = sys.argv
    try:
        sys.argv = [str(path)]
        with contextlib.redirect_stdout(io.StringIO()):
            spec.loader.exec_module(result)
    finally:
        sys.argv = previous
    return result


def definitions(before, manifest):
    require(Path(__file__).read_bytes() == before, "New model witness unchanged")
    for path, expected in manifest.items():
        require(sha((ROOT / path).read_bytes()) == expected, "Complete loaded definition: " + path)


def u32(value):
    return value & 0xFFFFFFFF


def signed(value, bits=32):
    value &= (1 << bits) - 1
    return value - (1 << bits) if value & (1 << (bits - 1)) else value


def owned_map(m, at, length, fill=0):
    require(type(at) is int and type(length) is int and 0x80000000 <= at < 0x80200000 and
            0 < length <= 0x40000 and at + length <= 0x80200000 and
            all(at + i not in m.memory for i in range(length)), "Disjoint bounded explicit native memory owner")
    m.map(at, length, fill)


def owned_blob(m, at, raw):
    require(type(raw) is bytes, "Immutable physical owner snapshot")
    owned_map(m, at, len(raw))
    m.memory.update((at + i, value) for i, value in enumerate(raw))


class Expected:
    """Independent structural formulas, not another instruction interpreter."""
    def __init__(self, memory):
        self.memory, self.writes = dict(memory), []

    def read(self, at, size=4):
        require(type(at) is int and at % size == 0 and all(at + i in self.memory for i in range(size)),
                "Expected relocation read must have an aligned complete owner")
        return sum(self.memory[at + i] << (8 * i) for i in range(size))

    def write(self, at, value, size=4):
        self.read(at, size)
        value &= (1 << (size * 8)) - 1
        self.writes.append((at, size, value))
        for i in range(size):
            self.memory[at + i] = value >> (8 * i) & 255

    def generic(self, root):
        count = signed(self.read(root))
        if count < 0:
            for offset in (4, 12, 8):
                self.write(root + offset, root + self.read(root + offset))
            return root & 0x7FFFFFFF
        require(count <= 46, "Bounded actual animated model count")
        for offset in range(0x14, 0x34, 4):
            if self.read(root + offset):
                self.write(root + offset, (root & 0x7FFFFFFF) + self.read(root + offset))
        geometry = u32(root + self.read(root + 0x34))
        self.write(root + 0x34, geometry)
        for animation in range(count):
            field = root + 0x38 + 4 * animation
            relative = self.read(field)
            if relative == 0xFFFFFFFF:
                continue
            record = u32(root + relative)
            self.write(field, record)
            for offset in (0x14, 0x18):
                self.write(record + offset, geometry + self.read(record + offset))
            if self.read(record + 0x1C):
                for offset in (0x1C, 0x20):
                    self.write(record + offset, geometry + self.read(record + offset))
            packed = self.read(record + 4, 1) != 0
            if packed:
                self.write(record + 0x10, geometry + self.read(record + 0x10))
            count_words = signed(self.read(record, 2), 16)
            require(0 <= count_words <= 4096, "Finite actual animation relocation rows")
            for row in range(count_words):
                at = record + 0x24 + row * (4 if packed else 8)
                value = self.read(at)
                if packed:
                    self.write(at, value & 0xFFE00000)
                    self.write(at, (value & 0xFFE00000) +
                               ((signed(u32((value & 0x1FFFFF) + geometry)) >> 1) & 0x1FFFFF))
                else:
                    self.write(at, value + (geometry & 0x1FFFFF))
                    following = self.read(at + 4)
                    if following & 0xFFFF:
                        self.write(at + 4, following + ((signed(u32(geometry - at)) >> 2) & 0xFFFF))
        return root

    def player_stream(self, model, resident, threshold):
        for animation in range(46):
            field = resident + 0x38 + animation * 4
            prior = self.read(field)
            if prior >= threshold and prior == 0xFFFFFFFF:
                self.write(field, 0)
        cursor, ids = model, []
        geometry = self.read(resident + 0x34)
        while signed(self.read(cursor)) >= 0:
            animation, length = self.read(cursor), self.read(cursor + 4)
            require(animation < 46 and animation not in ids and 0x24 <= length <= 0x20000,
                    "Finite unique actual local player stream")
            record = cursor + 8
            self.write(resident + 0x38 + 4 * animation, record)
            for offset in (0x14, 0x18):
                self.write(record + offset, geometry + self.read(record + offset))
            count = signed(self.read(record, 2), 16)
            require(0 <= count <= 4096, "Finite player packed rows")
            packed_end = record + 0x24 + count * 4
            self.write(record + 0x10, packed_end + self.read(record + 0x10))
            for row in range(count):
                at = record + 0x24 + row * 4
                value = self.read(at)
                self.write(at, (value & 0xFFE00000) +
                           ((signed(u32((value & 0x1FFFFF) + packed_end)) >> 1) & 0x1FFFFF))
            cursor = record + length
            ids.append(animation)
            require(len(ids) <= 46, "Finite player stream terminator")
        require(self.read(cursor) == 0xFFFFFFFF, "Exact player sentinel")
        return ids


def exact_state(m, expected, start=0):
    require(m.memory == expected.memory and sorted(m.writes[start:]) == sorted(expected.writes),
            "Complete independent relocated memory and exact write multiset including unchanged complement")


def shared_fixture(code, loader, pete, header, original):
    m = loader.Machine(code)
    owned_map(m, 0x80076378, 0x800)  # Explicit initially-empty512-slot registration fixture.
    owned_map(m, 0x800755A4, 4)
    owned_map(m, 0x800785FC, 4, 0xA5)
    owned_map(m, MODEL_STACK - 0x10, 8, 0xA5)  # Only the prologue's two actual stores.
    owned_blob(m, HEADER_FIXTURE, header)
    m.poke(0x800755A4, struct.unpack_from("<I", original, 0x65DA4)[0])
    require(m.read(0x800755A4) == 0x800, "Actual Original resident top-reserve scalar")
    m.r[29], m.r[31] = MODEL_STACK, loader.RETURN
    expected = Expected(m.memory)
    expected.write(MODEL_STACK - 0x10, 0)
    expected.write(MODEL_STACK - 0xC, 0)
    m.run(0x8005B7D8, {0x8005B7F0})
    require((m.r[18], m.r[19], m.r[29]) == (0, 0x80076378, MODEL_STACK - 0x30),
            "Executed shared prologue produces counter/table/frame")
    # Three separately named source cuts: completed header delivery; LUI top;
    # actual allocation subtraction/store. No skipped CD helper returns.
    m.r[16] = HEADER_FIXTURE
    m.run(0x8005B814, {0x8005B818})
    m.run(0x8005B848, {0x8005B864})
    base = m.read(0x800785FC)
    require(base == 0x80200000 - 0x800 - len(pete) == 0x801C5BB8 and
            m.read(0x80076378) == 0, "Native derived shared base; no class0 or expected threshold seed")
    expected.write(0x800785FC, base)
    exact_state(m, expected)
    owned_blob(m, base, pete)  # Exact complete physical-copy result, not executed memcpy/CD.
    before_payload = dict(m.memory)
    expected = Expected(m.memory)
    slots = []
    for index in range(64):
        offset, cls = struct.unpack_from("<II", header, 4 + 8 * index)
        if offset == 0:
            require(cls == 0 and header[4 + 8 * index:4 + 8 * 64] == bytes(8 * (64 - index)),
                    "Complete shared registration terminator complement")
            break
        require(0x800 <= offset < len(pete) + 0x800 and cls < 512 and all(cls != old[0] for old in slots),
                "Actual bounded unique shared roots")
        root = base + offset - 0x800
        result = expected.generic(root)
        expected.write(0x80076378 + cls * 4, result)
        slots.append((cls, root, result))
    require(len(slots) == 54 and slots[0] == (0, base, base), "Exact full54 shared registrations")
    start = len(m.writes)
    m.run(0x8005B870, {0x8005B8B8}, maximum=250000)
    exact_state(m, expected, start)
    restored = dict(m.memory)
    for at, size, _ in expected.writes:
        for i in range(size):
            restored[at + i] = before_payload[at + i]
    require(restored == before_payload, "Complete shared relocation/registration immediate inverse")
    require(m.r[18] == 54 and m.r[16] == HEADER_FIXTURE + 54 * 8 and
            m.r[19] == 0x80076378 and m.r[29] == MODEL_STACK - 0x30 and
            m.calls == [(0x8005B890, 0x800133E0)] * 54,
            "All actual shared helper calls and returns; stop before B6F8 tail")
    return m, slots, base


def execute_models(parser, loader, code, data, body, pete, shared_header, original, roots):
    parser_case, parsed = parser.execute(data, code, loader)
    require(all(parser_case[key] == value for key, value in parser.PINS.items()), "Frozen parser control on actual model predecessor")
    shared, shared_roots, resident = shared_fixture(code, loader, pete, shared_header, original)
    # Separate explicit model-entry stack fixture. The frozen parser control's
    # stack ends at801FF800, overlapping eight bytes of the complete PETE copy;
    # that old temporary frame is NOT imported into this distinct model phase.
    m = loader.Machine(code)
    for at in parser.GLOBAL_WORDS:
        owned_blob(m, at, parsed.bytes(at, 4))
    owned_blob(m, 0x80076278, parsed.bytes(0x80076278, 0x100))
    owned_blob(m, parser.COPY, parsed.bytes(parser.COPY, len(data)))
    m.r, m.hi, m.lo = list(parsed.r), parsed.hi, parsed.lo
    m.r[29] = MODEL_STACK
    model_at = m.read(0x800785E0)
    header, model = body[:0x800], body[0xE7000:0x102000]
    require(model_at == parser_case["modelEndpoint"] and model_at not in m.memory,
            "Actual parser-derived model anchor immediately after used data, never seeded")
    owned_blob(m, model_at, model)
    owned_blob(m, resident, shared.bytes(resident, len(pete)))
    owned_blob(m, 0x80076378, shared.bytes(0x80076378, 0x800))
    owned_blob(m, 0x80076C00, header[:0x1D0])  # Actual descriptor/root/class header, not generated fixture.
    for at in (0x800785FC, 0x800785E4, 0x80078600, 0x80078604, 0x80075864):
        owned_map(m, at, 4, 0xA5)
    m.poke(0x800785FC, resident)  # Output of the actual independent shared allocation/loop above.
    m.poke(0x80075864, 0)  # Explicit external caller phase fixture, not inferred runtime chronology.
    m.r[16] = 1  # Source-cut root0-present branch, authenticated by actual header root0.
    prepared = (dict(m.memory), list(m.r), m.hi, m.lo)
    expected, start = Expected(m.memory), len(m.writes)
    player_ids = expected.player_stream(model_at, resident, resident)
    for cls in range(512):
        at = 0x80076378 + cls * 4
        if (expected.read(at) & 0xFFFFFF) < (resident & 0xFFFFFF):
            expected.write(at, 0)
    for row in roots[1:]:
        root = model_at + row["modelOffset"]
        result = expected.generic(root)
        expected.write(0x80076378 + row["classId"] * 4, result)
    expected.write(0x800785E4, model_at + len(model))
    expected.write(0x80078604, 0x3800)
    expected.write(0x80078600, 0x102000)
    expected.write(0x80075864, 1)
    require(model_at + len(model) not in m.memory, "No Scene byte mapped before model execution")
    m.run(0x80015A54, {0x800163BC}, maximum=250000)
    exact_state(m, expected, start)
    restored = dict(m.memory)
    for at, size, _ in expected.writes:
        for i in range(size):
            restored[at + i] = prepared[0][at + i]
    require(restored == prepared[0], "Complete immediate relocation inverse including every retained owner")
    require(m.read(0x800785E4) == model_at + len(model) == 0x800A86F0 and
            m.read(0x80078600) == 0x102000 and m.read(0x80078604) == 0x3800,
            "Actual executed Scene base and CD descriptor algebra, no expected endpoint seed")
    require(player_ids == [18, 19, 29, 40, 8] and m.bytes(parser.COPY, len(data)) ==
            parsed.bytes(parser.COPY, len(data)),
            "Five exact local player animations and whole already-parsed data unchanged")
    require(m.r[29] == MODEL_STACK and m.calls == [(0x80015A78, 0x80013230)] +
            [(0x80015B0C, 0x800133E0)] * 11, "Real local player and eleven generic helper returns")
    registered = [(cls, m.read(0x80076378 + cls * 4)) for cls in range(512) if m.read(0x80076378 + cls * 4)]
    require(m.read(0x80076378 + 0xAD * 4) == (resident + 0x34A8C - 0x800) & 0x7FFFFFFF and
            m.read(0x80076378 + 0xD * 4) == 0,
            "Key AD simple-model low alias comes from actual shared loader; controller D has no model root")
    return dict(parser=parser_case, sharedInstructions=shared.instructions,
                sharedRegisteredRoots=shared_roots, sharedRelocatedSha256=sha(shared.bytes(resident, len(pete))),
                residentBase=resident, sharedRoot0Seeded=False, localPlayerAnimationIds=player_ids,
                modelBase=model_at, sceneBase=m.read(0x800785E4), localChainInstructions=m.instructions,
                modelSha256=sha(model), relocatedModelSha256=sha(m.bytes(model_at, len(model))),
                finalResidentSha256=sha(m.bytes(resident, len(pete))), registeredRoots=registered,
                classTableSha256=sha(m.bytes(0x80076378, 0x800)),
                sharedWrites=len(shared.writes), localModelWrites=len(m.writes) - start,
                sharedRegisters=shared.r, localRegisters=m.r, sharedHiLo=[shared.hi, shared.lo], localHiLo=[m.hi, m.lo],
                exactMemoryAndWriteComplement=True, modelCdOffset=0xE7000, modelCdByteLength=len(model),
                sceneCdOffset=0x102000, sceneCdByteLength=0x3800,
                sceneMapped=False, modelPhysicalPaddingMapped=True, dataAllocationPaddingMapped=False,
                retiredParserFrameImported=False, disjointPhysicalOwnerMappings=True,
                completeImmediateRelocationInverse=True, expectedSceneEndpointSeeded=False), m, prepared


def negatives(loader, code, prepared, model_at, resident, original, current, windows):
    refused, demonstrations = [], []

    def reject(label, action, phrase):
        try:
            action()
        except AssertionError as error:
            require(phrase in str(error), label + ": expected refusal, got " + str(error))
            refused.append(label)
            return
        raise AssertionError("Accepted negative control: " + label)

    def machine(program=None):
        m = loader.Machine(code if program is None else program)
        m.memory, m.r, m.hi, m.lo = dict(prepared[0]), list(prepared[1]), prepared[2], prepared[3]
        return m

    def run(m, budget=250000):
        m.run(0x80015A54, {0x800163BC}, maximum=budget)
        return m

    for window in windows:
        if window["name"] not in MODEL_SPANS and not window["name"].startswith("shared-"):
            continue
        at, length = window["pc"] - 0x8000F800, window["byteLength"]
        bad = bytearray(current[at:at + length]); bad[-1] ^= 1
        reject("mutated-source-" + window["name"], lambda bad=bad, at=at, length=length:
               require(bytes(bad) == original[at:at + length], "Exact source instruction mismatch"), "instruction mismatch")
    for pc in (0x80013230, 0x800133E0, 0x80013428, 0x80015B24, 0x80015B7C):
        program = dict(code); del program[pc]
        reject("unmapped-call-delay-or-registration-" + hex(pc), lambda program=program: run(machine(program)), "Unowned native instruction")
    for address, label in ((model_at + 0x1AC00, "truncated-final-debris-header"),
                           (0x80076378, "missing-actual-resident-root-slot"),
                           (0x80076C10, "missing-actual-model-descriptor")):
        m = machine(); del m.memory[address]
        reject(label, lambda m=m: run(m), "Unmapped native read")
    m = machine(); m.poke(0x80076D52, 0x200, 2)
    reject("class512-outside-complete-registration-table", lambda: run(m), "Unmapped fixture write 80076B78")
    m = machine(); m.poke(0x80076C54, 0x7FFFFFFC)
    reject("root-offset-outside-actual-model-owner", lambda: run(m), "Unmapped native read")
    reject("finite-native-budget", lambda: run(machine(), 8), "instruction budget")
    m = loader.Machine(code); owned_map(m, loader.STACK - 8, 8, 0xA5)
    reject("old-parser-frame-collides-with-complete-resident", lambda: owned_map(m, resident, 0x39C48), "Disjoint bounded")
    reject("physical-owner-end-overflow", lambda: owned_map(machine(), 0x801FFFFC, 8), "Disjoint bounded")
    program = dict(code); program[0x80015B5C] = 0
    m = run(machine(program))
    require(m.r[2] == model_at + 0x1B000 and m.read(0x800785E4) == 0xA5A5A5A5,
            "Skipped actual Scene store leaves poison despite correct computed endpoint")
    demonstrations.append("scene-endpoint-store-not-seeded")
    m = machine(); m.poke(0x80076D52, 0xAD, 2)
    run(m)
    require(m.read(0x80076378 + 0xAD * 4) == model_at + 0x10A4C,
            "Native registration accepts altered class identity; exact actual header ownership is necessary")
    demonstrations.append("native-registration-is-not-header-authentication")
    return dict(refusals=refused, nativeIsNotValidationControls=demonstrations)


def main(argv=None):
    args = list(sys.argv[1:] if argv is None else argv)
    require(__debug__ and len(args) == 5 and args[0] in ("--derive", "--witness"),
            "Usage: --derive/--witness absolute-Original.bin Original.cue exact-child.bin child.cue")
    mode, *names = args
    paths = [Path(name) for name in names]
    require(all(p.is_absolute() for p in paths) and paths[0].parent == paths[1].parent and
            paths[2].parent == paths[3].parent and [p.name for p in paths] ==
            ["Spyro the Dragon (USA).bin", "Spyro the Dragon (USA).cue",
             "native-fresh-saved-level-r01.bin", "native-fresh-saved-level-r01.cue"],
            "Only explicit exact paired input names; no output path")
    before = Path(__file__).read_bytes()
    parser = load(PARSER, HELPERS[PARSER], "family_model_parser")
    body_reference = load(BODY, HELPERS[BODY], "family_model_body_reference")
    child = load(parser.CHILD, parser.HELPERS[parser.CHILD], "family_model_child")
    loader = load(parser.LOADER, parser.HELPERS[parser.LOADER], "family_model_machine")
    manifest = dict(HELPERS)
    for entries in (parser.HELPERS, child.HELPERS, loader.HELPERS, body_reference.HELPERS):
        for path, digest in entries.items():
            require(path not in manifest or manifest[path] == digest, "Consistent transitive definition")
            manifest[path] = digest
    definitions(before, manifest)
    reader_path, neutral_path = list(child.HELPERS)[:2]
    reader = load(reader_path, manifest[reader_path], "family_model_reader")
    neutral = load(neutral_path, manifest[neutral_path], "family_model_neutral")
    held, failures, report = [], [], None
    with contextlib.ExitStack() as stack:
        try:
            specs = ((child.IMAGE_LENGTH, parser.ORIGINAL_SHA), (88, parser.ORIGINAL_CUE_SHA),
                     (child.IMAGE_LENGTH, child.IMAGE_SHA), (len(child.CUE_BYTES), sha(child.CUE_BYTES)))
            for path, (length, digest) in zip(paths, specs):
                stream = stack.enter_context(path.open("rb"))
                initial = child.identity(os.fstat(stream.fileno()))
                held.append((stream, path, initial, length, digest))
                child.authenticate(stream, path, initial, length, digest)
            output = io.StringIO()
            with contextlib.redirect_stdout(output):
                loader.main(str(paths[0]))
            control = json.loads(output.getvalue())
            require(control["marker"] == "VARIABLE_NATIVE_LOADER_ORACLE_OK" and control["checks"] == 5987 and
                    control["expected_refusals"] == 29, "Unchanged old control first; historical seeded player remains separately labelled")
            neutral.preserved_control()
            neutral.validate_document(neutral.CANONICAL)
            original = reader.logical(held[0][0], 53875 * 2048, 0x66000)
            allocated, extraction = child.extract(held[2][0], reader)
            current = reader.logical(held[2][0], (37 + extraction["wadByteLength"] // 2048) * 2048, 0x66000)
            code, windows = parser.code_owners(original, current, loader)
            for name, pc, length, digest in loader.SPANS:
                if name not in MODEL_SPANS:
                    continue
                at = pc - 0x8000F800
                raw = original[at:at + length]
                require(sha(raw) == digest and current[at:at + length] == raw, "Exact retained model code " + name)
                windows.append(dict(name=name, pc=pc, byteLength=length, sha256=digest))
                code.update((pc + i, parser.word(raw, i)) for i in range(0, length, 4))
            for name, pc, length, digest in (
                    ("shared-prologue-allocation-first-root", 0x8005B7D8, 0xD4,
                     "f5f4ea14f28a2cfbab256546d7c4a41f9e796b62b152d84252d254b3d6cf1b67"),
                    ("shared-loop-next-and-delay", 0x8005B8AC, 12,
                     "12aa7636b563700087d046eb8dd2e7f33f86bfff5462bc6b0744dbed83843bc9")):
                at = pc - 0x8000F800
                raw = original[at:at + length]
                require(sha(raw) == digest and current[at:at + length] == raw, "Exact retained shared code " + name)
                windows.append(dict(name=name, pc=pc, byteLength=length, sha256=digest))
                # Do not even map the skipped CD/copy calls or other prologue
                # owners: only explicitly executed cuts and full root loop.
                for i in range(0, length, 4):
                    current_pc = pc + i
                    if (0x8005B7D8 <= current_pc < 0x8005B7F0 or current_pc == 0x8005B814 or
                            0x8005B848 <= current_pc < 0x8005B864 or 0x8005B870 <= current_pc < 0x8005B8B8):
                        require(current_pc not in code, "Disjoint shared instruction owner")
                        code[current_pc] = parser.word(raw, i)
            require(original[0x65DA4:0x65DA8] == current[0x65DA4:0x65DA8] == bytes.fromhex("00080000"),
                    "Actual retained native resident-reserve scalar")
            prior = reader.logical(held[2][0], extraction["wadLba"] * 2048 + extraction["bodyWadOffset"], extraction["bodyByteLength"])
            data_report = child.reference(allocated, neutral)
            require(all(data_report[k] == v for k, v in child.PINS.items()), "Every frozen child-relative used-data reference")
            data, _, _ = child.compose(allocated, neutral)
            body = body_reference.assemble(prior, data)
            body_report = body_reference.reference(prior, data, child)
            require(all(body_report[k] == v for k, v in body_reference.PINS.items()), "Exact complete independently assembled Body and all47 controls")
            pete = reader.logical(held[0][0], 37 * 2048 + 0x7B9000, 0x39C48)
            require(sha(pete) == PETE_SHA, "Complete Original resident PETE package")
            shared_header = reader.logical(held[0][0], 37 * 2048 + 0x7B8800, 0x800)
            require(sha(shared_header) == PETE_HEADER_SHA and parser.word(shared_header) == len(pete) and
                    reader.logical(held[2][0], 37 * 2048 + 0x7B8800, 0x800) == shared_header and
                    reader.logical(held[2][0], 37 * 2048 + 0x7B9000, len(pete)) == pete,
                    "Actual complete shared header/payload preserved in exact child")
            require(0x8007AA38 + extraction["hostByteLength"] == parser.COPY and
                    current[0x4B0B8:0x4B0BC] == bytes.fromhex("38ca4224"),
                    "Retained12000 Host and actual copy-anchor instruction; initializer not executed")
            host = reader.logical(held[2][0], extraction["wadLba"] * 2048 + extraction["hostWadOffset"], extraction["hostByteLength"])
            require(len(host) == 0x12000 and sha(host) == "9d828474ed59e411b570e011e4b4156037e628c4cc9596cb476e7e2abdb995b2",
                    "Complete unchanged current chest Host child owner; no overlay instructions executed here")
            header, model = body[:0x800], body[0xE7000:0x102000]
            roots = []
            for index in range(64):
                offset = parser.word(header, 0x50 + 4 * index)
                if offset:
                    roots.append(dict(slot=index, classId=int.from_bytes(header[0x150 + 2 * index:0x152 + 2 * index], "little"),
                                      modelOffset=offset - 0xE7000))
            require([(r["classId"], r["modelOffset"]) for r in roots] == [
                (0, 0), (0x78, 0x10A4C), (0x26, 0x1361C), (0x11B, 0x13B80), (0xE, 0x17800),
                (0xF, 0x17FEC), (0x10, 0x18778), (0x1F5, 0x19000), (0xAE, 0x19800),
                (0x135, 0x1A808), (0x136, 0x1AA2C), (0x137, 0x1AC00)], "All twelve actual local roots; AD is shared, not local")
            case, machine, prepared = execute_models(parser, loader, code, data, body, pete, shared_header, original, roots)
            controls = negatives(loader, code, prepared, case["modelBase"], case["residentBase"], original, current, windows)
            require(PINS and all(case[k] == v for k, v in PINS.items()), "Frozen source-executed model references")
            report = dict(marker="TILED_FAMILY_NATIVE_MODEL_ORACLE_OK" if mode == "--witness" else "TILED_FAMILY_NATIVE_MODEL_MEASURED_UNFROZEN",
                          oracleSha256=sha(before), helperPins=manifest, sourceWindows=windows,
                          originalSha256=parser.ORIGINAL_SHA, originalCueSha256=parser.ORIGINAL_CUE_SHA,
                          childSha256=child.IMAGE_SHA, childCueSha256=sha(child.CUE_BYTES),
                          originalExecutableSha256=sha(original), currentExecutableSha256=sha(current),
                          hostSha256=sha(host), originalPeteSha256=sha(pete), originalPeteHeaderSha256=sha(shared_header),
                          preservedControl=dict(checks=control["checks"], refusals=control["expected_refusals"],
                                                historicalPlayerBaseFixture=loader.PETE, notActualSharedRootEvidence=True),
                          bodyReference=body_report, localRoots=roots, case=case,
                          negativeControls=controls,
                          nativeDataAndSoundParserExecuted=True, nativeSharedAndLocalModelLoaderCutsExecuted=True,
                          fullNativeLevelLoaderExecuted=False, hostInitializerExecuted=False,
                          headerDeliveryFixtureAddress=HEADER_FIXTURE, modelCutStackFixture=MODEL_STACK,
                          modelPhysicalReadResultMapped=True, cdCopyOrHardwareExecuted=False,
                          sceneInitializationExecuted=False, renderOrCollisionQueryExecuted=False,
                          sourceCertified=False, bodyIssued=False, discIssued=False,
                          runtimeVerified=False, publicationApproved=False, completeNativeDependencyClosureProven=False)
        except BaseException as error:
            failures.append(error)
        finally:
            for stream, path, initial, length, digest in held:
                for reopen in (False, True):
                    try:
                        if reopen:
                            with path.open("rb") as reopened:
                                child.authenticate(reopened, path, initial, length, digest)
                        else:
                            child.authenticate(stream, path, initial, length, digest)
                    except BaseException as error:
                        failures.append(error)
            try:
                definitions(before, manifest)
            except BaseException as error:
                failures.append(error)
            if failures:
                raise BaseExceptionGroup("Final exact Original/child BIN/CUE and definitions", failures)
    report["completeBeforeAfterReopenedFourFileGuards"] = True
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
