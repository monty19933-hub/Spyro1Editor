#!/usr/bin/env python3
"""Actual tiled-family Scene storage at the frozen native model endpoint.

Source-only, read-only four-file witness. Explicit post-header s5=0 skips actor
updating. No full initializer, allocator, CD/hardware, gameplay or disc output.
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
MODEL = "src/Spyro.Editor.NativeFreshWizardAuthoringSmoke/source_tiled_family_native_model_oracle.py"
HELPERS = {MODEL: "20bf2c8f6ba2716752982b98c25869ccc9cd6cc8b3439b8c8d0894212e1521d7"}
SCENE_SHA = "4f560dcbfa2d997a3f02554cf297d95c1778f67fc5d8fcf8ff47f64d609a89e8"
SCENE_SPANS = ("scene-global-reload", "scene-lists-arena-route-buffer-fixups")
LISTS = ((0x80078560, 0x80078564, 12), (0x80078568, 0x8007856C, 12),
         (0x80078580, 0x80078584, 8), (0x80078570, 0x80078574, 8),
         (0x800785A0, 0x800785A4, 12), (0x80078588, 0x8007858C, 8),
         (0x80078578, 0x8007857C, 8))
# Exactly the globals touched by these two cuts with external level ID65 and
# s5=0. 785E4 is retained from native model execution, never mapped or poked.
# Neither Scene cut accesses stack memory, so there is NO Scene frame mapping.
NEW_GLOBALS = (0x800758C8, 0x8007572C, 0x80075814, 0x80075684,
               0x800756F0, 0x800756F4, 0x80075828, 0x80075890, 0x8007573C,
               0x800756A4, 0x800756E8, 0x80075930, 0x800756A8,
               0x800757F8, 0x800757B4, 0x80075778, 0x8007596C) + tuple(
                   address for count, pointer, _ in LISTS for address in (count, pointer))
# Frozen only after the unchanged interpreter executed the actual complete
# child Scene at the actual model result. --derive cannot bypass these pins.
PINS = {
    "sceneBase": 0x800A86F0, "sceneEnd": 0x800ABEF0, "sceneByteLength": 0x3800,
    "sceneSha256": SCENE_SHA,
    "relocatedSceneSha256": "eee95aff98549e43738fa76d419e15ca240959fad7d10dc7b7ed4d60ee28fcb9",
    "instructions": 309, "reloadInstructions": 15, "nativeReadCount": 57,
    "writes": 47, "sceneWrites": 12, "globalWords": 32,
    "registers": [0, 2147942400, 0, 2148178508, 613566758, 2148186752,
                  1056768, 2148172532, 32, 2148172528, 15892, 1, 2097151,
                  4292870144, 0, 0, 2148186796, 10, 2148173552, 2147970128,
                  2147967864, 0, 0, 0, 0, 0, 0, 0, 0, 2149580544, 0, 2147572500],
    "hiLo": [32, 3067834110],
    "executedPcSha256": "301c357f693d4ea8dad82afb245619a90cf85392c5b19d67d82169902964550f",
    "expectedResultGlobalsSeeded": False, "modelSceneEndpointReseeded": False,
    "exactCompleteMemoryAndWriteComplement": True, "completeImmediateInverse": True,
    "allSixIntegerOwnersUnrelocated": True, "explicitReward2Unchanged": True,
    "completeLookaheadReservePaddingUnchanged": True, "stackBytesMappedForScene": 0,
}


def require(ok, label):
    if not ok:
        raise AssertionError(label)


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def word(raw, at=0):
    require(type(at) is int and at >= 0 and at % 4 == 0 and at + 4 <= len(raw), "Complete aligned word")
    return struct.unpack_from("<I", raw, at)[0]


def load(relative, expected, name):
    path = ROOT / relative
    require(sha(path.read_bytes()) == expected, "Pinned definition immediately before import: " + relative)
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


def add_manifest(manifest, entries):
    for path, expected in entries.items():
        require(path not in manifest or manifest[path] == expected, "Consistent transitive definition pin")
        manifest[path] = expected


def definition_checks(before, manifest):
    checks = [(Path(__file__), sha(before))]
    checks += [(ROOT / path, digest) for path, digest in manifest.items()]
    errors = []
    for path, digest in checks:
        try:
            require(sha(path.read_bytes()) == digest, "Complete retained definition: " + str(path))
        except BaseException as error:
            errors.append(error)
    if errors:
        raise BaseExceptionGroup("All helper and own definition checks", errors)


def source_code(original, current, windows):
    code = {}
    for span in windows:
        pc, size = span["pc"], span["byteLength"]
        at = pc - 0x8000F800
        raw = original[at:at + size]
        require(len(raw) == size and sha(raw) == span["sha256"] and current[at:at + size] == raw,
                "Exact Original/current instruction window: " + span["name"])
        for offset in range(0, size, 4):
            address = pc + offset
            if span["name"].startswith("shared-") and not (
                    0x8005B7D8 <= address < 0x8005B7F0 or address == 0x8005B814 or
                    0x8005B848 <= address < 0x8005B864 or 0x8005B870 <= address < 0x8005B8B8):
                continue
            require(address not in code, "Disjoint executed instruction owners")
            code[address] = word(raw, offset)
    return code


def layout(scene):
    require(type(scene) is bytes and len(scene) == 0x3800 and sha(scene) == SCENE_SHA,
            "Complete immutable exact Scene including padding")
    cursor = 0x88
    for _, _, length in LISTS:
        require(word(scene, cursor) == length and word(scene, cursor + 4) == 0,
                "Exact seven empty list lengths and counts")
        cursor += length
    row, placed, stride, capacity = cursor + 8, word(scene, cursor + 4), 0x58, 32
    require(row == 0xD4 and placed == 9, "Actual complete P9 row table")
    dynamic = row + placed * stride
    lookahead = dynamic + capacity * stride
    dynamic_private = lookahead + stride
    properties_header = dynamic_private + capacity * 0x18
    require(word(scene, cursor) == properties_header - cursor and
            (dynamic, lookahead, dynamic_private, properties_header) == (0x3EC, 0xEEC, 0xF44, 0x1244),
            "Independent exact P9/N32 rows/lookahead/private arena algebra")
    private_lengths = (0x50, 8, 4, 0x18, 0x28, 0x28, 0x28, 0x28, 0x28)
    private, cursor = [], properties_header + 4
    for index, length in enumerate(private_lengths):
        require(word(scene, row + index * stride) == cursor, "Every complete row has its ordered private block")
        private.append(cursor)
        cursor += length
    require(word(scene, properties_header) == 4 + sum(private_lengths) and
            word(scene, private[0]) == private[0] + 0x28,
            "Exact private component length and Wizard route pointer")
    route = cursor
    require((word(scene, route), word(scene, route + 4), word(scene, route + 8)) == (8, 0, 0x2004),
            "Empty route list followed by complete bucket buffer")
    bucket = route + 12
    fixes_at = bucket + 0x2000
    fixes = (row, private[0]) + tuple(row + index * stride for index in range(1, placed))
    require(word(scene, fixes_at) == len(fixes) == 10 and
            tuple(word(scene, fixes_at + 4 + 4 * index) for index in range(10)) == fixes,
            "Exact ten fixup owner fields derived independently from row/private structure")
    used_end = fixes_at + 4 + len(fixes) * 4
    require((bucket, fixes_at, used_end) == (0x1390, 0x3390, 0x33BC) and
            scene[dynamic:properties_header] == bytes(properties_header - dynamic) and
            scene[bucket:fixes_at] == bytes(0x2000) and scene[used_end:] == bytes(len(scene) - used_end),
            "Complete dynamic rows/lookahead/private reserve, bucket and padding preimage")
    integer_owners = tuple((private[index], 2 if index == 3 else 3) for index in range(3, 9))
    require(all(word(scene, offset) == owner for offset, owner in integer_owners) and
            all(offset not in fixes for offset, _ in integer_owners), "Six integer family owners never enter pointer fixups")
    reward2 = (row + 5 * stride + 12, private[5] + 4)
    require(reward2 == (0x298, 0x12E8) and all(
            struct.unpack_from("<3I", scene, offset) == (10454, 10503, 9216) for offset in reward2),
            "Actual explicit Reward2 row and private WORLD destination")
    return dict(row=row, placed=placed, stride=stride, capacity=capacity, dynamic=dynamic,
                lookahead=lookahead, dynamicPrivate=dynamic_private, propertiesHeader=properties_header,
                privateOffsets=private, route=route, bucket=bucket, fixupCountOffset=fixes_at,
                fixes=fixes, usedEnd=used_end, integerOwners=integer_owners, reward2Offsets=reward2)


def prepare_scene(scene, predecessor, program, model, loader):
    m = loader.Machine(program)
    # Import the entire actual model result unchanged: data, complete model and
    # PETE, registration/header/global owners and registers, but no parser frame.
    m.memory, m.r, m.hi, m.lo = dict(predecessor.memory), list(predecessor.r), predecessor.hi, predecessor.lo
    base = m.read(0x800785E4)
    require(base == 0x800A86F0 and base + len(scene) == 0x800ABEF0 and
            m.r[29] == model.MODEL_STACK and all(model.MODEL_STACK + i not in m.memory for i in range(-0x100, 0x100)),
            "Unchanged model-produced Scene endpoint and no inherited/retired frame")
    model.owned_blob(m, base, scene)  # Explicit byte-mapped CD delivery, not an executed CD helper.
    for at in NEW_GLOBALS:
        model.owned_map(m, at, 4, 0xA5)
    m.poke(0x8007596C, 65)  # Explicit external caller scalar; no initializer chronology claim.
    m.reads.clear()
    require(m.memory[base - 1] == predecessor.memory[base - 1] and base + len(scene) not in m.memory,
            "No extra prefix/suffix allocation: preceding byte belongs to complete actual model")
    return m, base


def run_scene(m, scene, maximum=10000):
    before = m.instructions
    m.run(0x80013680, {0x800136C4}, maximum=maximum)
    reload_count = m.instructions - before
    require(m.r[16] == m.r[18] == int.from_bytes(m.bytes(0x800785E4, 4), "little"),
            "Native reload consumes actual model-produced Scene global")
    # Explicit source cut after unexecuted header/reset helpers. This skips all
    # placed updating and does not claim a complete Scene initializer or return.
    m.r[16], m.r[17], m.r[21], m.r[2] = m.r[18] + 0x80, 0, 0, word(scene, 0x80)
    m.run(0x80013C4C, {0x80014084}, maximum=maximum)
    return reload_count


def expected_writes(scene, details, base):
    # Structural formulas and literal native zero/sentinel stores, not another
    # interpreter and not harvested writes from the machine under test.
    writes = [(at, 4, 0) for at in (0x800758C8, 0x8007572C, 0x80075814, 0x80075684)]
    writes += [(0x800756F0, 4, word(scene, 0x80)), (0x800756F4, 4, word(scene, 0x84))]
    cursor = 0x88
    for count, pointer, length in LISTS:
        writes += [(pointer, 4, base + cursor + 8), (count, 4, 0)]
        cursor += length
    writes += [(at, 4, 0) for at in (0x80078560, 0x80078568, 0x800785A0, 0x80078588, 0x80078578)]
    writes += [(0x80075828, 4, base + details["row"]),
               (0x80075890, 4, base + details["dynamic"]),
               (0x8007573C, 4, base + details["dynamic"]),
               (base + details["dynamic"] + 0x48, 1, 255), (0x800756A4, 4, 0),
               (0x800756E8, 4, base + details["propertiesHeader"]),
               (0x80075930, 4, base + details["propertiesHeader"]),
               (base + details["propertiesHeader"] - 1, 1, 255),
               (0x800756A8, 4, details["capacity"]),
               (0x800757F8, 4, base + details["route"] + 8), (0x800757B4, 4, 0),
               (0x80075778, 4, base + details["bucket"])]
    writes += [(base + offset, 4, base + word(scene, offset)) for offset in details["fixes"]]
    return writes


def verify_state(m, before, writes, scene, details, base):
    expected = dict(before)
    for address, size, value in writes:
        require(all(address + i in expected for i in range(size)), "Expected writes stay within original explicit owners")
        for i in range(size):
            expected[address + i] = value >> (8 * i) & 255
    require(m.memory == expected and sorted(m.writes) == sorted(writes),
            "Exact complete independent memory/write complement")
    inverse = dict(m.memory)
    for address, size, _ in writes:
        for i in range(size):
            inverse[address + i] = before[address + i]
    require(inverse == before, "Complete immediate inverse includes all unchanged data/model/PETE/Scene/globals")
    for offset, value in details["integerOwners"]:
        require(m.read(base + offset) == value, "Every integer family owner stays unchanged")
    for offset in details["reward2Offsets"]:
        require(m.bytes(base + offset, 12) == scene[offset:offset + 12], "Complete explicit Reward2 destination remains unchanged")
    return expected


def negative_controls(scene, details, prepared, program, writes, base, loader, model, original, current, windows):
    refused, demonstrations = [], []

    def reject(label, action, phrase):
        try:
            action()
        except AssertionError as error:
            require(phrase in str(error), label + ": unexpected refusal: " + str(error))
            refused.append(label)
            return
        raise AssertionError("Accepted negative control: " + label)

    def machine(code=None):
        m = loader.Machine(program if code is None else code)
        m.memory, m.r, m.hi, m.lo = dict(prepared[0]), list(prepared[1]), prepared[2], prepared[3]
        return m

    for span in windows:
        at, size = span["pc"] - 0x8000F800, span["byteLength"]
        bad = bytearray(current[at:at + size]); bad[-1] ^= 1
        reject("changed-source-" + span["name"], lambda bad=bad, at=at, size=size:
               require(bytes(bad) == original[at:at + size], "Exact instruction mismatch"), "instruction mismatch")
    for pc in (0x80013684, 0x800136B8, 0x80013EE4, 0x80014060, 0x80014080):
        altered = dict(program); del altered[pc]
        reject("missing-executed-code-" + hex(pc), lambda altered=altered: run_scene(machine(altered), scene),
               "Unowned native instruction")
    for at, label in ((0x800785E4, "model-produced-global"), (0x8007596C, "explicit-caller-id"),
                       (base + 0xCC, "arena-header"), (base + 0x33B8, "last-fixup-entry"),
                       (base + 0x12A0, "key-private-pointer-target")):
        m = machine(); del m.memory[at]
        if label == "key-private-pointer-target":
            # Native pointer addition does not dereference the private payload.
            # Full physical-owner/complement verification must detect this hole.
            run_scene(m, scene)
            reject("missing-complete-" + label, lambda m=m: verify_state(m, prepared[0], writes, scene, details, base),
                   "complete independent")
        else:
            reject("missing-owner-" + label, lambda m=m: run_scene(m, scene), "Unmapped native read")
    reject("finite-scene-instruction-budget", lambda: run_scene(machine(), scene, 8), "instruction budget")
    reject("overlapping-scene-owner", lambda: model.owned_blob(machine(), base, scene), "Disjoint bounded")
    reject("old-parser-frame-overlaps-actual-PETE", lambda: model.owned_map(machine(), loader.STACK - 8, 8), "Disjoint bounded")
    for label, bad in (("truncated", scene[:-1]), ("appended", scene + bytes(1)),
                       ("short-sector", scene[:-0x800]), ("added-sector", scene + bytes(0x800))):
        reject("wrong-scene-extent-" + label, lambda bad=bad: layout(bad), "Complete immutable exact Scene")
    # The native loop authenticates neither private integers nor destinations,
    # reserves, or padding. The independent whole-owner check refuses each.
    probes = [offset for offset, _ in details["integerOwners"]]
    probes += [offset + axis * 4 for offset in details["reward2Offsets"] for axis in range(3)]
    probes += [details["lookahead"], details["lookahead"] + 0x57, details["dynamicPrivate"],
               details["propertiesHeader"] - 2, details["bucket"], details["fixupCountOffset"] - 1,
               details["usedEnd"], len(scene) - 1]
    for offset in probes:
        m = machine(); m.memory[base + offset] ^= 1
        run_scene(m, scene)
        reject("changed-unchanged-scene-complement-" + hex(offset),
               lambda m=m: verify_state(m, prepared[0], writes, scene, details, base), "complete independent")
    for pc, at, size, label in ((0x80013F38, 0x80075828, 4, "placed-row-global"),
                                 (0x80013FC0, 0x800756A8, 4, "capacity-global"),
                                 (0x80013F78, base + 0x434, 1, "first-sentinel"),
                                 (0x80013F94, base + 0x1243, 1, "second-sentinel"),
                                 (0x8001406C, base + 0xD4, 4, "actual-pointer-fixups")):
        altered = dict(program); altered[pc] = 0
        m = machine(altered)
        if label == "placed-row-global":
            # Its native consumer turns the untouched poison into an unowned
            # pointer before the end; no expected result is available to rescue it.
            reject("unseeded-" + label, lambda: run_scene(m, scene), "Unmapped fixture write")
            continue
        run_scene(m, scene)
        prior = sum(prepared[0][at + i] << (8 * i) for i in range(size))
        require(m.read(at, size) == prior, "Skipped result store retains its actual poison/preimage")
        reject("unseeded-" + label, lambda m=m: verify_state(m, prepared[0], writes, scene, details, base),
               "complete independent")
        demonstrations.append(label + "-not-seeded")
    positive = machine(); run_scene(positive, scene)
    expected = verify_state(positive, prepared[0], writes, scene, details, base)
    altered = dict(program); altered[0x80013EFC] = 0
    m = machine(altered); run_scene(m, scene)
    require(m.memory == expected, "Skipped duplicate zero store preserves final bytes")
    reject("skipped-idempotent-zero-store", lambda: verify_state(m, prepared[0], writes, scene, details, base),
           "complete independent")
    m = machine(); run_scene(m, scene)
    m.write(0x800785E4, base, 4)
    require(m.memory == expected, "Unexpected same-value write preserves final bytes")
    reject("unexpected-idempotent-endpoint-write", lambda: verify_state(m, prepared[0], writes, scene, details, base),
           "complete independent")
    return dict(refusals=refused, unseededResultControls=demonstrations,
                nativeStorageIsNotInputAuthentication=True, exactWriteMultisetCheckedBeyondFinalBytes=True)


def execute_scene(scene, predecessor, code, model, loader, original, current, windows):
    details = layout(scene)
    program = {pc: value for pc, value in code.items() if
               0x80013680 <= pc < 0x800136C4 or 0x80013C4C <= pc < 0x80014084}
    require(len(program) == (0x44 + 0x438) // 4, "Only exact two Scene instruction windows executable")
    m, base = prepare_scene(scene, predecessor, program, model, loader)
    prepared = (dict(m.memory), list(m.r), m.hi, m.lo)
    writes = expected_writes(scene, details, base)
    reload_count = run_scene(m, scene)
    native_reads = tuple(m.reads)
    require(m.calls == [] and m.r[29] == prepared[1][29] and
            set(m.memory) == set(prepared[0]), "No actor/helper call, stack access or implicit mapping")
    globals_accessed = set()
    for at, size in tuple(m.reads) + tuple((at, size) for at, size, _ in m.writes):
        if not base <= at < base + len(scene):
            globals_accessed.add(at)
            require(at in NEW_GLOBALS or at == 0x800785E4, "Only exact explicit Scene globals accessed")
    require(globals_accessed == set(NEW_GLOBALS) | {0x800785E4}, "Every and only declared global is accessed")
    verify_state(m, prepared[0], writes, scene, details, base)
    controls = negative_controls(scene, details, prepared, program, writes, base, loader, model, original, current, windows)
    case = dict(sceneBase=base, sceneEnd=base + len(scene), sceneByteLength=len(scene), sceneSha256=sha(scene),
                relocatedSceneSha256=sha(m.bytes(base, len(scene))), instructions=m.instructions,
                reloadInstructions=reload_count, nativeReadCount=len(native_reads), writes=len(m.writes),
                sceneWrites=len([at for at, _, _ in m.writes if base <= at < base + len(scene)]),
                globalWords=len(globals_accessed), globals=[dict(address=at, value=m.read(at)) for at in sorted(globals_accessed)],
                layout=details, sentinelOffsets=[0x434, 0x1243], registers=m.r, hiLo=[m.hi, m.lo],
                executedPcSha256=sha(b"".join(struct.pack("<I", pc) for pc in sorted(m.executed))),
                expectedResultGlobalsSeeded=False, modelSceneEndpointReseeded=False,
                exactCompleteMemoryAndWriteComplement=True, completeImmediateInverse=True,
                allSixIntegerOwnersUnrelocated=True, explicitReward2Unchanged=True,
                completeLookaheadReservePaddingUnchanged=True, stackBytesMappedForScene=0,
                sceneCutRegisters=dict(s0=base + 0x80, s1=0, s5=0, v0=word(scene, 0x80)))
    return case, controls


def main(argv=None):
    args = list(sys.argv[1:] if argv is None else argv)
    require(__debug__ and len(args) == 5 and args[0] in ("--derive", "--witness"),
            "Usage: --derive/--witness absolute-Original.bin Original.cue exact-child.bin child.cue")
    mode, *names = args
    paths = [Path(name) for name in names]
    require(all(path.is_absolute() for path in paths) and paths[0].parent == paths[1].parent and
            paths[2].parent == paths[3].parent and [path.name for path in paths] ==
            ["Spyro the Dragon (USA).bin", "Spyro the Dragon (USA).cue",
             "native-fresh-saved-level-r01.bin", "native-fresh-saved-level-r01.cue"],
            "Only explicit absolute exact paired input names; no output path")
    before, manifest = Path(__file__).read_bytes(), dict(HELPERS)
    held, failures, report = [], [], None
    with contextlib.ExitStack() as stack:
        try:
            model = load(MODEL, manifest[MODEL], "family_scene_model")
            add_manifest(manifest, model.HELPERS)
            parser = load(model.PARSER, manifest[model.PARSER], "family_scene_parser")
            body_reference = load(model.BODY, manifest[model.BODY], "family_scene_body")
            add_manifest(manifest, parser.HELPERS)
            add_manifest(manifest, body_reference.HELPERS)
            child = load(parser.CHILD, manifest[parser.CHILD], "family_scene_child")
            loader = load(parser.LOADER, manifest[parser.LOADER], "family_scene_loader")
            add_manifest(manifest, child.HELPERS)
            add_manifest(manifest, loader.HELPERS)
            reader_path, neutral_path = list(child.HELPERS)[:2]
            reader = load(reader_path, manifest[reader_path], "family_scene_reader")
            neutral = load(neutral_path, manifest[neutral_path], "family_scene_neutral")
            definition_checks(before, manifest)
            specs = ((child.IMAGE_LENGTH, parser.ORIGINAL_SHA), (88, parser.ORIGINAL_CUE_SHA),
                     (child.IMAGE_LENGTH, child.IMAGE_SHA), (len(child.CUE_BYTES), sha(child.CUE_BYTES)))
            for path, (length, digest) in zip(paths, specs):
                stream = stack.enter_context(path.open("rb"))
                initial = child.identity(os.fstat(stream.fileno()))
                held.append((stream, path, initial, length, digest))
                child.authenticate(stream, path, initial, length, digest)
            # Unmodified complete control, including all parser/model negatives,
            # its separately labelled historical seeded-player fixture and its
            # own four-file finalization. This report is never a state fixture.
            output = io.StringIO()
            with contextlib.redirect_stdout(output):
                model.main(["--witness", *names])
            frozen = json.loads(output.getvalue())
            require(frozen["marker"] == "TILED_FAMILY_NATIVE_MODEL_ORACLE_OK" and
                    frozen["oracleSha256"] == HELPERS[MODEL] and
                    frozen["completeBeforeAfterReopenedFourFileGuards"] and
                    all(manifest[path] == digest for path, digest in frozen["helperPins"].items()),
                    "Unchanged complete model predecessor passes with same definition closure")
            original = reader.logical(held[0][0], 53875 * 2048, 0x66000)
            allocated, extraction = child.extract(held[2][0], reader)
            current = reader.logical(held[2][0], (37 + extraction["wadByteLength"] // 2048) * 2048, 0x66000)
            require(sha(original) == loader.native.EXE_SHA and sha(current) == parser.CURRENT_EXE_SHA,
                    "Complete exact Original/current executables")
            prior = reader.logical(held[2][0], extraction["wadLba"] * 2048 + extraction["bodyWadOffset"],
                                   extraction["bodyByteLength"])
            data, _, _ = child.compose(allocated, neutral)
            body = body_reference.assemble(prior, data)
            require(sha(body) == body_reference.PINS["newBodySha256"], "Complete independently assembled actual tiled Body")
            pete = reader.logical(held[0][0], 37 * 2048 + 0x7B9000, 0x39C48)
            shared_header = reader.logical(held[0][0], 37 * 2048 + 0x7B8800, 0x800)
            require(sha(pete) == model.PETE_SHA and sha(shared_header) == model.PETE_HEADER_SHA and
                    reader.logical(held[2][0], 37 * 2048 + 0x7B9000, len(pete)) == pete and
                    reader.logical(held[2][0], 37 * 2048 + 0x7B8800, len(shared_header)) == shared_header,
                    "Complete retained shared inputs match both exact images")
            roots = [dict(slot=index,
                          classId=int.from_bytes(body[0x150 + index * 2:0x152 + index * 2], "little"),
                          modelOffset=word(body, 0x50 + index * 4) - 0xE7000)
                     for index in range(64) if word(body, 0x50 + index * 4)]
            require(roots == frozen["localRoots"], "Actual complete current Body model-root table")
            windows = list(frozen["sourceWindows"])
            for name, pc, size, digest in loader.SPANS:
                if name in SCENE_SPANS:
                    windows.append(dict(name=name, pc=pc, byteLength=size, sha256=digest))
            code = source_code(original, current, windows)
            case, predecessor, _ = model.execute_models(parser, loader, code, data, body, pete,
                                                         shared_header, original, roots)
            require(all(case[key] == value for key, value in model.PINS.items()),
                    "Unchanged actual model execution supplies Scene state")
            scene_at, scene_length = body_reference.descriptors(body)[3]
            scene = body[scene_at:scene_at + scene_length]
            require(scene_at == case["sceneCdOffset"] == 0x102000 and scene_length == case["sceneCdByteLength"] == 0x3800
                    and sha(scene) == SCENE_SHA, "Exact complete actual placed Scene owner")
            scene_windows = [span for span in windows if span["name"] in SCENE_SPANS]
            scene_case, controls = execute_scene(scene, predecessor, code, model, loader, original, current, scene_windows)
            require(PINS and all(scene_case[key] == value for key, value in PINS.items()),
                    "Frozen actual source-executed Scene references")
            require(len(controls["refusals"]) == 46 and len(controls["unseededResultControls"]) == 4,
                    "Complete frozen Scene refusal and unseeded-result control set")
            report = dict(marker="TILED_FAMILY_NATIVE_SCENE_ORACLE_OK" if mode == "--witness" else
                          "TILED_FAMILY_NATIVE_SCENE_MEASURED_UNFROZEN", oracleSha256=sha(before),
                          helperPins=manifest, sourceWindows=scene_windows, negativeControls=controls,
                          modelPredecessor=dict(marker=frozen["marker"], oracleSha256=frozen["oracleSha256"],
                                                casePinsVerified=True, sceneBase=case["sceneBase"],
                                                parserInstructions=case["parser"]["instructions"],
                                                sharedInstructions=case["sharedInstructions"],
                                                localInstructions=case["localChainInstructions"]), case=scene_case,
                          originalSha256=parser.ORIGINAL_SHA, originalCueSha256=parser.ORIGINAL_CUE_SHA,
                          childSha256=child.IMAGE_SHA, childCueSha256=sha(child.CUE_BYTES),
                          originalExecutableSha256=sha(original), currentExecutableSha256=sha(current),
                          completeBodySha256=sha(body), nativeSceneStorageCutsExecuted=True,
                          retainedCompleteModelDataPeteComplementVerified=True,
                          externalCallerLevelIdFixture=65, cdDeliveryIsCompleteByteMapping=True,
                          fullSceneInitializerExecuted=False, placedActorUpdaterExecuted=False,
                          allocatorExecuted=False, cdCopyOrHardwareExecuted=False, hostInitializerExecuted=False,
                          runtimeVerified=False, sourceCertified=False, bodyIssued=False, discIssued=False,
                          publicationApproved=False, completeNativeDependencyClosureProven=False)
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
                definition_checks(before, manifest)
            except BaseException as error:
                failures.append(error)
            if failures:
                raise BaseExceptionGroup("Final exact Original/child BIN/CUE and all definitions", failures)
    report["completeBeforeAfterReopenedFourFileGuards"] = True
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
