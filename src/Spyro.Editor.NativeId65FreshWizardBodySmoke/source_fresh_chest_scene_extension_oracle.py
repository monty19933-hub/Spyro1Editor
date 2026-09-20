#!/usr/bin/env python3
"""Original-only A-control/B-separated P2 -> P9 scenes and bounded native slices.

No Core, candidate input, output files, resource installation, Host dispatch,
placed updater, GTE or emulator. Chest source coordinates are deliberately
unchanged: this is storage/reference evidence, not a playable placement recipe.
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
P1 = "src/Spyro.Editor.NativeId65FreshWizardSceneSmoke/source_p1_scene_oracle.py"
P1_SHA = "fd675a846b1fad4097ad7c3acb0cee06262a560e3e20105de492570431b35eca"
MATRIX = "_local/research/dark-wizard-native-closure-20260906/authored-wizard-field-matrix-r01.okFf2x/source_field_matrix.py"
MATRIX_SHA = "c094e48e9de88b5979370080e9f86a12c5543de14c01e19fbf34f0925086d8b1"
MIXED = "_local/research/mixed-wizard-material-composition-r01.ZTFRMb/source_mixed_resource.py"
MIXED_SHA = "d0f8f3c60ade14a0be415d1f4c660e5873cbab31db1b54d8414be5031885c9a3"
EXTRA = {
    "artisans": (37 * 2048 + 0x800800, 0x383000,
                 "86cde4f886d97821a3a4b869100b586842dbd4b9fb696558bead274bb547af4f"),
    "peace": (37 * 2048 + 0x1890800, 0x29E800,
              "c140bd422471c66cd57bd85b29912819f798f5b080779153674714889306c9d3"),
}
ROW, STRIDE, CAPACITY = 0xD4, 0x58, 32
BASE, EXE = 0x80100000, 0x8000F800


def sha(data): return hashlib.sha256(data).hexdigest()
def word(data, at=0): return struct.unpack_from("<I", data, at)[0]
def put(data, at, value): struct.pack_into("<I", data, at, value)
def half(data, at): return struct.unpack_from("<H", data, at)[0]


def require(ok, label):
    if not ok: raise AssertionError(label)


def bounded(data, at, count):
    require(at >= 0 and count > 0 and at <= len(data) - count, "Complete source extent")
    return bytes(data[at:at + count])


def frozen():
    require(sha((ROOT / P1).read_bytes()) == P1_SHA, "Frozen P1 source/loader reference")
    require(sha((ROOT / MATRIX).read_bytes()) == MATRIX_SHA
            and sha((ROOT / MIXED).read_bytes()) == MIXED_SHA, "Frozen original-only B recipe definitions")
    spec = importlib.util.spec_from_file_location("chest_scene_p1_reference", ROOT / P1)
    module = importlib.util.module_from_spec(spec); spec.loader.exec_module(module)
    spec = importlib.util.spec_from_file_location("chest_scene_b_recipe", ROOT / MATRIX)
    matrix = importlib.util.module_from_spec(spec); spec.loader.exec_module(matrix)
    # Only immutable recipe values and pure field/inverse functions are used.
    # Do not invoke the old matrix's source loader or alter its helper pins.
    return module, module.helper(), matrix


def separated_b(old_p1, matrix):
    recipe = dict(matrix.B); matrix.validate_recipe(recipe)
    require((recipe["spawn_x"], recipe["spawn_y"], recipe["floor_z"] + 512, recipe["heading"],
             recipe["origin_x"], recipe["origin_y"], recipe["origin_z"], recipe["end_x"], recipe["end_y"])
            == (8960, 8960, 9216, 64, 9984, 9984, 8704, 9472, 9984), "Exact frozen separated-B recipe")
    # The complete fields are the original-only mixed source recipe at602-608;
    # six words change, while spawn, heading and source-derived Z delta remain.
    values = ((0, recipe["spawn_x"]), (4, recipe["spawn_y"]), (8, recipe["floor_z"] + 512),
              (0xE0, recipe["origin_x"]), (0xE4, recipe["origin_y"]), (0xE8, recipe["origin_z"]),
              (0xFB8, recipe["origin_x"]), (0xFBC, recipe["origin_y"]), (0xFC0, recipe["origin_z"] + 287),
              (0xFC8, recipe["end_x"]), (0xFCC, recipe["end_y"]), (0xFD0, recipe["origin_z"] + 287))
    fields = [(at, struct.pack("<I", value)) for at, value in values] + [(14, bytes([recipe["heading"]]))]
    revised, receipt = matrix.patch_fields(old_p1, fields)
    require(matrix.undo_fields(revised, receipt) == old_p1, "Complete B recipe inverse to source-built A P1")
    changed = tuple((at, word(before), word(after)) for at, before, after in receipt if before != after)
    require(changed == ((0xE0, 8704, 9984), (0xE4, 8960, 9984),
                        (0xFB8, 8704, 9984), (0xFBC, 8960, 9984),
                        (0xFC8, 9216, 9472), (0xFCC, 8960, 9984)), "Exact six changed B preimages; no hidden scene changes")
    require(sha(revised) == "08718e7594702ab21e17f49c459b99cf4e35c8190258abbcaaf720106a880343",
            "Preserved original-only separated-B P1 scene golden")
    return revised, recipe, [dict(offset=at, before=before.hex(), after=after.hex()) for at, before, after in receipt]


def grass_source(data):
    scene = bounded(data, 0x1C9800, 0x14800)
    require((word(data, 0x18), word(data, 0x1C)) == (0x1C9800, len(scene)), "Artisans scene descriptor")
    row, private = bounded(scene, 0xCC44, STRIDE), bounded(scene, 0x11ACC, 8)
    require(sha(row) == "de8450049c0bea92fba8fe4e7f9f9cb749a514d1318dbd83dc9b6b1b18a0b190"
            and sha(private) == "9eeeff662fd5b77dbc35de8ed01e0d1fd149cee49126625b69f65553c4b7c20b",
            "Complete Grass source row/private")
    require(half(row, 0x36) == 0x1F5 and word(row) == 0x11ACC and word(scene, 0x13ED8) == 0xC9,
            "Grass source class/private pointer/fixup count")
    fixups = tuple(word(scene, 0x13EDC + 4 * i) for i in range(0xC9))
    require(tuple(f for f in fixups if 0xCC44 <= f < 0xCC9C) == (0xCC44,)
            and not any(0x11ACC <= f < 0x11AD4 for f in fixups), "Grass has only its row pointer")
    revised = bytearray(row)
    for at, value in ((12, 9152), (16, 9152), (20, 8704)): put(revised, at, value)
    revised[0x20:0x32] = struct.pack("<9h", 0, 0, -4096, 0, 4096, 0, 4096, 0, 0)
    revised[0x46] = 64
    return bytes(revised), private


def chest_source(data):
    scene = bounded(data, 0x1C2800, 0x11800)
    require((word(data, 0x18), word(data, 0x1C)) == (0x1C2800, len(scene)), "Peace scene descriptor")
    count = word(scene, 0x11234)
    require(0 < count <= (len(scene) - 0x11238) // 4, "Bounded Peace fixup list")
    fixups = tuple(word(scene, 0x11238 + i * 4) for i in range(count))
    specs = tuple(zip((78, 79, 118, 119, 120, 121, 122),
                      (0xEA94, 0xEA98, 0xEF9C, 0xEFC4, 0xEFEC, 0xF014, 0xF03C),
                      (4, 0x18, 0x28, 0x28, 0x28, 0x28, 0x28)))
    rows, properties, report = [], [], []
    for ordinal, (index, private_at, length) in enumerate(specs):
        row_at = 0x8260 + index * STRIDE
        row, private = bounded(scene, row_at, STRIDE), bounded(scene, private_at, length)
        require(half(row, 0x36) == (0xAD if ordinal == 0 else 0xAE if ordinal == 1 else 0xD)
                and word(row) == private_at, "Source family row class/private pointer")
        require(tuple(f for f in fixups if row_at <= f < row_at + STRIDE) == (row_at,)
                and not any(private_at <= f < private_at + length for f in fixups),
                "Only row pointers; private integer owners are not pointer fixups")
        if ordinal: require(word(private) == (78 if ordinal == 1 else 79), "Exact source integer owner")
        rows.append(row); properties.append(private)
        report.append(dict(sourceIndex=index, finalIndex=ordinal + 2, rowSha256=sha(row),
                           privateSha256=sha(private), privateByteLength=length,
                           sourcePrivatePointer=private_at, coordinates=list(struct.unpack_from("<3I", row, 12))))
    require(sum(map(len, properties)) == 0xE4, "Exactly seven private source blocks total E4")
    return tuple(rows), tuple(properties), report


def scene_bytes(prefix, supplied_rows, supplied_properties, chest=False):
    """Independent sequential component construction, no production layouts."""
    rows = [bytearray(row) for row in supplied_rows]
    private = [bytearray(block) for block in supplied_properties]
    require(len(prefix) == 0xCC and len(rows) == len(private) == (9 if chest else 2)
            and all(len(row) == STRIDE for row in rows), "Closed complete fixture shape")
    lengths = (0x50, 8, 4, 0x18, 0x28, 0x28, 0x28, 0x28, 0x28) if chest else (0x50, 8)
    require(tuple(map(len, private)) == lengths, "Exact ordered private blocks")
    dynamic = ROW + len(rows) * STRIDE
    next_row = dynamic + CAPACITY * STRIDE
    dynamic_private = next_row + STRIDE
    header = dynamic_private + CAPACITY * 0x18
    offsets, cursor = [], header + 4
    for block in private: offsets.append(cursor); cursor += len(block)
    fixups = []
    for i, row in enumerate(rows):
        put(row, 0, offsets[i]); fixups.append(ROW + i * STRIDE)
        if i == 0:
            put(private[0], 0, offsets[0] + 0x28); fixups.append(offsets[0])
        if chest and i >= 3: put(private[i], 0, 2 if i == 3 else 3)
    output = bytearray(prefix) + struct.pack("<II", header - 0xCC, len(rows))
    output += b"".join(rows) + bytes(CAPACITY * STRIDE + STRIDE + CAPACITY * 0x18)
    require(len(output) == header, "Exact complete dynamic rows/lookahead/private reservation")
    output += struct.pack("<I", 4 + sum(lengths)) + b"".join(private)
    output += struct.pack("<III", 8, 0, 0x2004) + bytes(0x2000)
    fixup_at = len(output)
    output += struct.pack("<I", len(fixups)) + b"".join(struct.pack("<I", f) for f in fixups)
    used = len(output); output += bytes((-used) % 0x800)
    return bytes(output), dict(placed=len(rows), dynamic=dynamic, nextRow=next_row,
        dynamicPrivate=dynamic_private, propertiesHeader=header, privateOffsets=offsets,
        privateBytes=sum(lengths), bucket=cursor + 12, fixupCountOffset=fixup_at,
        fixups=fixups, usedEnd=used, sceneBytes=len(output))


def native_witness(executable, scene, layout, p1, scalar):
    code, pins = {}, []
    for name, pc, length, digest in p1.PINS:
        block = bounded(executable, pc - EXE, length)
        require(sha(block) == digest, "Exact native slice " + name)
        for at in range(0, length, 4):
            require(pc + at not in code, "Disjoint native instruction owners")
            code[pc + at] = word(block, at)
        pins.append(dict(id=name, pc=pc, byteLength=length, sha256=digest))
    machine = scalar.globals_machine(code)
    machine.blob(BASE, scene); machine.poke(0x800785E4, BASE); machine.poke(0x8007596C, 65)
    machine.run(0x80013680, {0x800136C4})
    require(machine.r[16] == machine.r[18] == BASE, "Actual scene global reload")
    # Existing bounded test entry explicitly bypasses all placed actor updating.
    machine.r[16], machine.r[17], machine.r[21], machine.r[2] = BASE + 0x80, 0, 0, word(scene, 0x80)
    machine.run(0x80013C4C, {0x80014084})
    require(machine.read(0x80075828) == BASE + ROW
            and machine.read(0x80075890) == BASE + layout["dynamic"]
            and machine.read(0x800756E8) == BASE + layout["propertiesHeader"]
            and machine.read(0x80075930) == BASE + layout["propertiesHeader"]
            and machine.read(0x800756A8) == CAPACITY and machine.read(0x800756A4) == 0
            and machine.read(0x80075778) == BASE + layout["bucket"], "Native derived P9/N32 owner anchors")
    inverse = bytearray(machine.bytes(BASE, len(scene)))
    for field in layout["fixups"]:
        require(machine.read(BASE + field) == BASE + word(scene, field), "Every actual native pointer fixup")
        put(inverse, field, word(scene, field))
    for at in (layout["dynamic"] + 0x48, layout["propertiesHeader"] - 1):
        require(machine.read(BASE + at, 1) == 255, "Owned native dynamic sentinel")
        inverse[at] = scene[at]
    require(bytes(inverse) == scene, "Complete native loader inverse/complement")
    for i in range(3, 9):
        require(machine.read(BASE + layout["privateOffsets"][i]) == (2 if i == 3 else 3),
                "Native loader leaves all six integer owner fields unrelocated")
    before = machine.bytes(BASE, len(scene)); owners, allocations = [], []
    for index in range(CAPACITY):
        machine.r[31] = scalar.RETURN; start = len(machine.writes)
        machine.run(0x800524C4, {scalar.RETURN})
        row = BASE + layout["dynamic"] + index * STRIDE
        props = BASE + layout["propertiesHeader"] - (index + 1) * 0x18
        require(machine.r[2] == row and machine.read(row) == props, "Actual allocator row/private stride")
        current = ((row, STRIDE), (props, 0x18))
        require(row + STRIDE <= props, "Complete dynamic row/private owners disjoint")
        for at, count in current:
            require(all(not (at < old + length and old < at + count) for old, length in owners),
                    "No dynamic allocation overlaps an earlier complete owner")
        for at, count, _ in machine.writes[start:]:
            if BASE <= at < BASE + len(scene):
                require(BASE + layout["dynamic"] <= at and at + count <= BASE + layout["propertiesHeader"],
                        "Actual allocator writes stay inside complete declared arena")
                require(all(not (at < old + length and old < at + count) for old, length in owners),
                        "Actual allocator does not write an earlier live dynamic owner")
        owners.extend(current); allocations.append(dict(index=index, row=row, private=props))
    after = machine.bytes(BASE, len(scene))
    require(after[:layout["dynamic"]] == before[:layout["dynamic"]]
            and after[layout["propertiesHeader"]:] == before[layout["propertiesHeader"]:],
            "All32 allocations preserve complete placed/private/bucket/fixup complement")
    require(machine.read(0x800756A4) == CAPACITY
            and machine.read(0x8007573C) == BASE + layout["nextRow"]
            and machine.read(0x80075930) == BASE + layout["dynamicPrivate"], "Owned next-row lookahead after allocation32")
    return dict(pins=pins, allocations=allocations, completedPcs=sorted(machine.executed),
                syntheticBase=BASE, syntheticEntry=dict(s0=BASE + 0x80, s1=0, s5=0, v0=word(scene, 0x80)),
                placedUpdaterSkipped=True, completeSceneInitializerExecuted=False,
                allocation33RefusalProven=False, nativeArenaExhaustionSafe=False)


def scene_case(old_p1, grass, chest_rows, chest_private, executable, p1, scalar):
    refusals = []
    def exact(data, expected): require(data == expected, "Exact complete scene bytes/complement")
    wizard = (old_p1[ROW:ROW + STRIDE], old_p1[0xF88:0xFD8])
    rows, private = (wizard[0], grass[0]), (wizard[1], grass[1])
    p2, old_layout = scene_bytes(old_p1[:0xCC], rows, private)
    p9, layout = scene_bytes(old_p1[:0xCC], rows + chest_rows, private + chest_private, True)
    require(old_layout["privateOffsets"] == [0xFE0, 0x1030]
            and old_layout["fixups"] == [0xD4, 0xFE0, 0x12C]
            and old_layout["usedEnd"] == 0x3054 and len(p2) == 0x3800, "Historical P2 exact component control")
    require(layout["privateOffsets"] == [0x1248, 0x1298, 0x12A0, 0x12A4, 0x12BC, 0x12E4, 0x130C, 0x1334, 0x135C]
            and layout["privateBytes"] == 0x13C and layout["dynamic"] == 0x3EC
            and layout["propertiesHeader"] == 0x1244 and layout["fixupCountOffset"] == 0x3390
            and layout["usedEnd"] == 0x33BC and len(layout["fixups"]) == 10 and len(p9) == len(p2),
            "P9 exact derived geometry preserves old3800 physical scene length")
    require(p9[layout["usedEnd"]:] == bytes(len(p9) - layout["usedEnd"]), "Complete sector padding")
    # Restore every transformed source field, then compare entire row/private owners.
    for i, (row, block) in enumerate(zip(rows + chest_rows, private + chest_private)):
        actual_row = bytearray(p9[ROW + i * STRIDE:ROW + (i + 1) * STRIDE])
        actual_private = bytearray(p9[layout["privateOffsets"][i]:layout["privateOffsets"][i] + len(block)])
        put(actual_row, 0, word(row))
        if i == 0 or i >= 3: put(actual_private, 0, word(block))
        require(bytes(actual_row) == row and bytes(actual_private) == block,
                "Complete source row/private inverse including untouched coordinate/state complement")
    recovered_rows = tuple(p9[ROW + i * STRIDE:ROW + (i + 1) * STRIDE] for i in range(2))
    recovered_private = tuple(p9[layout["privateOffsets"][i]:layout["privateOffsets"][i] + len(private[i])] for i in range(2))
    restored, _ = scene_bytes(p9[:0xCC], recovered_rows, recovered_private)
    require(restored == p2, "Complete independent inverse to old authored Wizard+Grass scene")
    for at in (0, 0xCC, 0xD0, ROW, layout["privateOffsets"][0], layout["privateOffsets"][3],
               layout["privateOffsets"][8], layout["dynamic"], layout["bucket"],
               layout["fixupCountOffset"] + 4, len(p9) - 1):
        bad = bytearray(p9); bad[at] ^= 1
        try: exact(bytes(bad), p9)
        except AssertionError: refusals.append(dict(kind="single-byte-owner-or-complement", offset=at))
        else: raise AssertionError("Mutated complete scene accepted")
    for bad in (p9[:-1], p9 + bytes(4), p2):
        try: exact(bad, p9)
        except AssertionError: refusals.append(dict(kind="wrong-whole-scene", length=len(bad)))
        else: raise AssertionError("Wrong complete scene accepted")
    native = native_witness(executable, p9, layout, p1, scalar)
    return dict(p1SceneSha256=sha(old_p1), oldP2SceneSha256=sha(p2), sceneSha256=sha(p9), layout=layout,
        spawn=list(struct.unpack_from("<3I", p9)), heading=p9[14],
        wizardOrigin=list(struct.unpack_from("<3I", p9, ROW + 12)),
        wizardRouteStart=list(struct.unpack_from("<3I", p9, layout["privateOffsets"][0] + 0x30)),
        wizardRouteEnd=list(struct.unpack_from("<3I", p9, layout["privateOffsets"][0] + 0x40)),
        native=native, refusals=refusals, fullOldSceneInverse=True,
        integerOwnerWrites=[dict(row=i, privateOffset=layout["privateOffsets"][i], value=2 if i == 3 else 3)
                            for i in range(3, 9)])


def main(path):
    p1, scalar, matrix = frozen()
    sources_spec = {**p1.SOURCES, **EXTRA}
    def identity(stream):
        scalar.native.identity(stream)
        require(stream.read(1) == b"" and stream.tell() == scalar.native.IMAGE_LENGTH, "Complete original EOF")
    def file_identity(stat): return stat.st_dev, stat.st_ino, stat.st_size, stat.st_mtime_ns, stat.st_ctime_ns
    with open(path, "rb") as image:
        initial = file_identity(os.fstat(image.fileno()))
        require(file_identity(os.stat(path)) == initial, "Held/named original identity")
        identity(image)
        try:
            sources = {name: scalar.native.logical(image, at, length) for name, (at, length, _) in sources_spec.items()}
            for name, (_, length, digest) in sources_spec.items():
                require(type(sources[name]) is bytes and len(sources[name]) == length and sha(sources[name]) == digest,
                        "Complete immutable original source " + name)
            old_p1, _ = p1.build(sources["town"], sources["donor"])
            require(sha(old_p1) == "b7bd222702ff9b5d6ea40c623159f4aa22b3425ef9368059167d1ccab930266b",
                    "Preserved independent P1 control")
            b_p1, b_recipe, b_receipt = separated_b(old_p1, matrix)
            grass = grass_source(sources["artisans"])
            chest_rows, chest_private, source_rows = chest_source(sources["peace"])
            a = scene_case(old_p1, grass, chest_rows, chest_private, sources["exe"], p1, scalar)
            b = scene_case(b_p1, grass, chest_rows, chest_private, sources["exe"], p1, scalar)
            require(a["layout"] == b["layout"] and a["oldP2SceneSha256"] != b["oldP2SceneSha256"]
                    and a["sceneSha256"] != b["sceneSha256"], "Distinct A/B complete scenes share only their exact storage geometry")
            b.update(fixture="separated-B-current-mixed-family", recipe=b_recipe, p1FieldWrites=b_receipt,
                     fullP1InverseToHistoricalA=True)
            result = dict(marker="FRESH_CHEST_P9_SCENE_REFERENCE_OK", sourceOwners=sources_spec,
                helperSha256=P1_SHA, originalSha256=scalar.native.IMAGE_SHA,
                **a, fixture="historical-A-control", separatedB=b, chestSourceRows=source_rows,
                recipeDefinitionPins={MATRIX: MATRIX_SHA, MIXED: MIXED_SHA},
                retainedChestCoordinates=True, authoredChestTransformProven=False,
                resourceOrHostInstallationProven=False, completeBehaviorProven=False,
                runtimeVerified=False, bodyIssued=False, publicationApproved=False, outputFilesWritten=False)
        finally:
            identity(image)
            require(file_identity(os.fstat(image.fileno())) == initial and file_identity(os.stat(path)) == initial,
                    "Complete held/named source unchanged")
            require(sha((ROOT / P1).read_bytes()) == P1_SHA, "P1 definition unchanged during source work")
            require(sha((ROOT / MATRIX).read_bytes()) == MATRIX_SHA
                    and sha((ROOT / MIXED).read_bytes()) == MIXED_SHA, "B recipe definitions unchanged during source work")
            require(sha((ROOT / p1.HELPER).read_bytes()) == p1.HELPER_SHA, "Scalar definition unchanged during source work")
            scalar.require_helpers()
    with open(path, "rb") as reopened:
        require(file_identity(os.fstat(reopened.fileno())) == initial, "Reopened named source identity")
        identity(reopened)
        require(file_identity(os.stat(path)) == initial, "Named source still identifies complete rechecked image")
    result["sourceUnchanged"] = True
    print(json.dumps(result, indent=2, sort_keys=True))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("original_bin")
    main(parser.parse_args().original_bin)
