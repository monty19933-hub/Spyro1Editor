#!/usr/bin/env python3
"""Original-only separated-B P9 two-anchor chest placement reference.

Exactly36 coordinate words change. This script accepts only the original BIN,
never Core/candidate bytes, and writes JSON only. Decoded floor triangles prove
point containment, not actor footprint, clearance, native collision or gameplay.
"""
import argparse
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
SCENE = "src/Spyro.Editor.NativeId65FreshWizardBodySmoke/source_fresh_chest_scene_extension_oracle.py"
SCENE_SHA = "7452a116ad1ef1d7c20aa2846d954c9fe7ea1e86fe9dd9d3dd9c4e3add7cf610"
SURFACE = "src/Spyro.Editor.NativeId65AuthoredSurfaceSmoke/source_surface_oracle.py"
SURFACE_SHA = "bcaa7349277bc5150cd17cf3e8cebbc689b68a58e9dee21a2709841c899aa592"
P2_SHA = "70312f09b2441c602ac5e8a5edd10dd0be7edb3246928ad0bf9aba8450653cf0"
P9_SHA = "cb881f39cb6ea5bf6bd47cd23564474488dac15d565f403e3591e44ea720a83c"
KEY = (8960, 9728, 8704)
CHEST = (9472, 8960, 8704)
REWARD_DELTAS = ((-410, 1229, 512), (0, 1229, 512), (-615, 205, 512),
                 (-410, 615, 512), (204, 819, 512))


def sha(data): return hashlib.sha256(data).hexdigest()
def word(data, at=0): return struct.unpack_from("<I", data, at)[0]
def xyz(data, at=12): return struct.unpack_from("<3i", data, at)


def require(ok, label):
    if not ok: raise AssertionError(label)


def frozen_module(relative, digest, name):
    require(sha((ROOT / relative).read_bytes()) == digest, "Exact helper definition " + relative)
    spec = importlib.util.spec_from_file_location(name, ROOT / relative)
    module = importlib.util.module_from_spec(spec)
    previous = sys.argv
    try:
        # The old surface helper has an optional source mode at module scope.
        # Isolate argv and suppress only its source-free diagnostic print.
        sys.argv = [str(ROOT / relative)]
        with contextlib.redirect_stdout(io.StringIO()): spec.loader.exec_module(module)
    finally:
        sys.argv = previous
    return module


def placement(p9, layout, source_rows, source_private):
    require(type(p9) is bytes and sha(p9) == P9_SHA and len(p9) == 0x3800,
            "Complete original-built separated-B P9 preimage")
    require(len(source_rows) == len(source_private) == 7, "Actual seven source row/private pairs")
    source_chest = xyz(source_rows[1])
    deltas = tuple(tuple(v - c for v, c in zip(xyz(row), source_chest)) for row in source_rows[2:])
    require(deltas == REWARD_DELTAS, "Original reward cluster, without Town placement policy")
    for row, private in zip(source_rows[2:], source_private[2:]):
        require(xyz(row) == xyz(private, 4), "Actual reward row/private XYZ premise")
    points = (KEY, CHEST) + tuple(tuple(c + d for c, d in zip(CHEST, delta)) for delta in deltas)
    require(points == ((8960, 9728, 8704), (9472, 8960, 8704), (9062, 10189, 9216),
                       (9472, 10189, 9216), (8857, 9165, 9216), (9062, 9575, 9216),
                       (9676, 9779, 9216)), "Exact bounded two-anchor fixture")
    output, fields, owned = bytearray(p9), [], set()

    def field(at, value, row, domain, axis):
        require(type(value) is int and -(1 << 31) <= value < (1 << 31), "Signed native coordinate word")
        require(at % 4 == 0 and 0 <= at <= len(p9) - 4, "Complete aligned coordinate field")
        span = set(range(at, at + 4))
        require(not owned.intersection(span), "Disjoint coordinate words")
        owned.update(span)
        before, after = p9[at:at + 4], struct.pack("<i", value)
        output[at:at + 4] = after
        fields.append((at, before, after, row, domain, axis))

    for ordinal, point in enumerate(points):
        index = ordinal + 2
        row_at = 0xD4 + index * 0x58
        require(xyz(p9, row_at + 12) == xyz(source_rows[ordinal]), "Actual source row coordinate preimage")
        for axis, value in enumerate(point): field(row_at + 12 + axis * 4, value, index, "row", axis)
        if ordinal >= 2:
            private_at = layout["privateOffsets"][index]
            require(xyz(p9, private_at + 4) == xyz(source_private[ordinal], 4), "Actual reward private preimage")
            for axis, value in enumerate(point): field(private_at + 4 + axis * 4, value, index, "private", axis)
    after = bytes(output)
    require(len(fields) == 36 and len(owned) == 144 and all(before != revised for _, before, revised, *_ in fields),
            "Exactly36 changed coordinate dwords; no masking, snapping or extra writes")
    require(all(after[i] == value for i, value in enumerate(p9) if i not in owned),
            "Complete scene complement including class/selector/matrix/owners/fixups and retained Wizard/Grass")
    for ordinal, point in enumerate(points):
        index = ordinal + 2
        require(xyz(after, 0xD4 + index * 0x58 + 12) == point, "All final row XYZ decoded exactly")
        if ordinal >= 2:
            require(xyz(after, layout["privateOffsets"][index] + 4) == point, "All final reward private XYZ decoded exactly")
    for index in range(3, 9):
        require(word(after, layout["privateOffsets"][index]) == (2 if index == 3 else 3),
                "Six integer owners unchanged by placement")
    require(tuple(word(after, at) for at in layout["fixups"]) == tuple(word(p9, at) for at in layout["fixups"]),
            "All ten pointer-fixup fields unchanged by placement")
    return after, tuple(fields), points, deltas


def inverse(supplied, expected, before, fields):
    require(type(supplied) is bytes and supplied == expected, "Complete exact placed-scene afterimage")
    restored = bytearray(supplied)
    for at, preimage, after, *_ in reversed(fields):
        require(restored[at:at + 4] == after, "Exact coordinate inverse preimage")
        restored[at:at + 4] = preimage
    require(bytes(restored) == before, "Complete inverse to source-coordinate P9")
    return bytes(restored)


def floor_centers(surface, recipe, points):
    # Compile solely from the frozen original-only B recipe and independent
    # packing helper, then decode its actual collision words rather than test
    # support against an invented infinite plane or a display rectangle alone.
    blob = bytes(surface.fresh(*(recipe[k] for k in
                 ("floor_x", "floor_y", "floor_z", "width", "depth", "floor_rgb", "sky_rgb"))))
    components, cursor = [], 0
    for _ in range(5):
        length = word(blob, cursor)
        require(length >= 4 and length % 4 == 0 and cursor <= len(blob) - length, "Complete surface component")
        components.append((cursor, length)); cursor += length
    require(cursor == len(blob) == 0x144 and tuple(n for _, n in components) == (172, 32, 8, 96, 16),
            "Unchanged exact2048 B surface grammar")
    start, length = components[3]
    collision = blob[start:start + length]
    triangles_at = 4 + word(collision, 20)
    assignments_at, flags_at = 4 + word(collision, 24), 4 + word(collision, 28)
    require(word(collision, 4) == word(collision, 8) == 2 and triangles_at + 24 == assignments_at
            and assignments_at + 4 == flags_at and flags_at + 4 == len(collision)
            and collision[assignments_at:flags_at] == bytes(4)
            and collision[flags_at:] == bytes((63, 63, 0, 0)), "Exact two collision triangles and retained assignments/flags")
    signed9 = lambda value: (value & 511) - (512 if value & 256 else 0)
    triangles = []
    for index in range(2):
        axes = tuple(word(collision, triangles_at + index * 12 + axis * 4) for axis in range(3))
        a = tuple((value & 0x3FFF) * 16 for value in axes)
        b = tuple(v + signed9(packed >> 14) * 16 for v, packed in zip(a, axes))
        c = tuple(v + signed9(packed >> 23) * 16 for v, packed in zip(a, axes))
        triangles.append((a, b, c))
    triangles = tuple(triangles)
    require(triangles == (((8448, 8448, 8704), (8448, 10496, 8704), (10496, 8448, 8704)),
                          ((10496, 8448, 8704), (8448, 10496, 8704), (10496, 10496, 8704))),
            "Actual packed finite B floor triangles; negative-XY native land winding")
    def cross(a, b, p): return (b[0] - a[0]) * (p[1] - a[1]) - (b[1] - a[1]) * (p[0] - a[0])
    report = []
    for ordinal, point in enumerate(points):
        margins = (point[0] - 8448, 10496 - point[0], point[1] - 8448, 10496 - point[1])
        supporting = tuple(i for i, tri in enumerate(triangles)
                           if all(cross(tri[j], tri[(j + 1) % 3], point) <= 0 for j in range(3)))
        require(min(margins) > 0 and supporting and point[2] - 8704 == (0 if ordinal < 2 else 512),
                "Strict XY point interior and exact source-derived Z offset above finite floor")
        report.append(dict(row=ordinal + 2, xyz=point, floorPlaneZ=8704, offsetAbovePlane=point[2] - 8704,
                           xyMargins=margins, containingCollisionTriangles=supporting))
    require(min(min(item["xyMargins"]) for item in report) == 307, "Exact307 minimum center margin")
    return dict(surfaceSha256=sha(blob), surfaceLength=len(blob), collisionSha256=sha(collision),
                triangles=triangles, centers=report, minimumCenterMargin=307,
                method="finite decoded triangle XY point projection only", footprintProven=False,
                nativeCollisionQueryExecuted=False, clearanceProven=False, routeSafetyProven=False)


def main(path):
    self_path = Path(__file__).resolve()
    self_sha = sha(self_path.read_bytes())
    scene = frozen_module(SCENE, SCENE_SHA, "placement_scene_reference")
    surface = frozen_module(SURFACE, SURFACE_SHA, "placement_surface_reference")
    p1, scalar, matrix = scene.frozen()
    source_specs = {**p1.SOURCES, **scene.EXTRA}
    def identity(stream):
        scalar.native.identity(stream)
        require(stream.read(1) == b"" and stream.tell() == scalar.native.IMAGE_LENGTH, "Complete original EOF")
    def file_identity(stat): return stat.st_dev, stat.st_ino, stat.st_size, stat.st_mtime_ns, stat.st_ctime_ns
    def definitions():
        for relative, digest in ((SCENE, SCENE_SHA), (SURFACE, SURFACE_SHA), (scene.P1, scene.P1_SHA),
                                 (scene.MATRIX, scene.MATRIX_SHA), (scene.MIXED, scene.MIXED_SHA),
                                 (p1.HELPER, p1.HELPER_SHA)):
            require(sha((ROOT / relative).read_bytes()) == digest, "Helper unchanged " + relative)
        require(sha(self_path.read_bytes()) == self_sha, "Placement oracle unchanged")
        scalar.require_helpers()
    with open(path, "rb") as image:
        initial = file_identity(os.fstat(image.fileno()))
        require(file_identity(os.stat(path)) == initial, "Held/named original identity")
        identity(image)
        try:
            sources = {name: scalar.native.logical(image, at, length)
                       for name, (at, length, _) in source_specs.items()}
            for name, (_, length, digest) in source_specs.items():
                require(type(sources[name]) is bytes and len(sources[name]) == length and sha(sources[name]) == digest,
                        "Complete immutable original owner " + name)
            a_p1, _ = p1.build(sources["town"], sources["donor"])
            require(sha(a_p1) == "b7bd222702ff9b5d6ea40c623159f4aa22b3425ef9368059167d1ccab930266b", "Historical A P1 control")
            b_p1, recipe, _ = scene.separated_b(a_p1, matrix)
            grass = scene.grass_source(sources["artisans"])
            rows, private, original_rows = scene.chest_source(sources["peace"])
            retained_rows = (b_p1[0xD4:0x12C], grass[0])
            retained_private = (b_p1[0xF88:0xFD8], grass[1])
            p2, _ = scene.scene_bytes(b_p1[:0xCC], retained_rows, retained_private)
            p9, layout = scene.scene_bytes(b_p1[:0xCC], retained_rows + rows, retained_private + private, True)
            require(sha(p2) == P2_SHA and sha(p9) == P9_SHA, "Frozen full B P2/P9 independent controls")
            after, fields, points, deltas = placement(p9, layout, rows, private)
            restored = inverse(after, after, p9, fields)
            restored_p2, _ = scene.scene_bytes(restored[:0xCC],
                tuple(restored[0xD4 + i * 0x58:0xD4 + (i + 1) * 0x58] for i in range(2)),
                tuple(restored[layout["privateOffsets"][i]:layout["privateOffsets"][i] + n]
                      for i, n in enumerate((0x50, 8))))
            require(restored_p2 == p2, "Complete authoredP9 to originalP9 to retainedP2 inverse")
            refusals = []
            # Every coordinate plus protected row/private/header/arena/bucket/
            # fixup/padding representative must fail the SAME exact inverse.
            bad_offsets = sorted({at for at, *_ in fields} | {0, 0xCC, 0xD4, 0x12C, 0x1BA, 0x229,
                layout["dynamic"], layout["privateOffsets"][0], layout["privateOffsets"][3],
                layout["bucket"], layout["fixupCountOffset"] + 4, len(after) - 1})
            for at in bad_offsets:
                bad = bytearray(after); bad[at] ^= 1
                try: inverse(bytes(bad), after, p9, fields)
                except AssertionError as error:
                    require(str(error) == "Complete exact placed-scene afterimage", "Exact corruption refusal")
                    refusals.append(dict(kind="coordinate-or-protected-complement", offset=at))
                else: raise AssertionError("Corrupt placed scene accepted")
            for bad in (after[:-1], after + bytes(4), p9, p2):
                try: inverse(bad, after, p9, fields)
                except AssertionError as error:
                    require(str(error) == "Complete exact placed-scene afterimage", "Exact whole-scene refusal")
                    refusals.append(dict(kind="wrong-whole-scene", length=len(bad)))
                else: raise AssertionError("Wrong whole placed scene accepted")
            geometry = floor_centers(surface, recipe, points)
            native = scene.native_witness(sources["exe"], after, layout, p1, scalar)
            result = dict(marker="FRESH_CHEST_PLACEMENT_REFERENCE_OK", originalSha256=scalar.native.IMAGE_SHA,
                sourceOwners=source_specs, helpers={SCENE: SCENE_SHA, SURFACE: SURFACE_SHA}, selfSha256=self_sha,
                sourceRows=original_rows, oldP2SceneSha256=sha(p2), sourceCoordinateP9Sha256=sha(p9),
                placedSceneSha256=sha(after), sceneLength=len(after), layout=layout,
                anchors=dict(key=KEY, chest=CHEST), rewardOffsetsFromActualSource=deltas, floor=geometry,
                coordinateWords=len(fields), changedBytes=sum(a != b for a, b in zip(p9, after)),
                fieldWrites=[dict(offset=at, before=before.hex(), after=revised.hex(), row=row, domain=domain, axis=axis)
                             for at, before, revised, row, domain, axis in fields],
                fullProtectedComplement=True, fullInverseToSourceP9=True, fullInverseToRetainedP2=True,
                native=native, refusals=refusals, coordinateLinkageProven=True,
                retainedWizard=dict(origin=(9984, 9984, 8704), routeEnd=(9472, 9984),
                                    nearbyRewardCenterXYDelta=(0, 205), interferenceExcluded=False),
                footprintProven=False, clearanceProven=False, runtimeVerified=False,
                completeNativeDependencyClosureProven=False, fullBehaviorProven=False,
                sourceIssuerOrCandidateCompared=False, bodyIssued=False, publicationApproved=False,
                outputFilesWritten=False)
        finally:
            identity(image)
            require(file_identity(os.fstat(image.fileno())) == initial and file_identity(os.stat(path)) == initial,
                    "Complete held/named original unchanged")
            definitions()
    with open(path, "rb") as reopened:
        require(file_identity(os.fstat(reopened.fileno())) == initial, "Reopened named original identity")
        identity(reopened)
        require(file_identity(os.stat(path)) == initial, "Named original identifies complete rechecked image")
    definitions()
    result["sourceUnchanged"] = True
    print(json.dumps(result, indent=2, sort_keys=True))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("original_bin")
    main(parser.parse_args().original_bin)
