"""Independent neutral four-tile R03-union reference; no Core/ROM/output reads.

Only the explicitly pinned, source-free three-tile helper is read/imported, for
its preserved control. The new literal document and byte assembly below do not
call production code or derive expectations from a candidate. Material slots
are neutral bindings, not captured records, GPU allocation or source authority.
Run only: python3 source_tiled_family_surface_oracle.py --self-test
"""
import copy
import hashlib
import importlib.util
import json
from pathlib import Path
import struct
import sys

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[2]
HELPERS = {
    "src/Spyro.Editor.NativeId65FreshDocumentSmoke/source_tiled_surface_oracle.py":
        "4aa6bc207bed327c8fb2f9832b8bd1bc098b26b0f50a4817ba55183373be0ec3",
}
VARIANT = "spyro1-usa-retail-mode2-2352-v1"
TILES = ((8448, 8448), (9984, 8448), (8448, 9984), (9984, 9984))
FACES = (0, 0, 0, 1, 0, 1, 1, 1)
SPANS = (("environment", 0, 0x298), ("visibility", 0x298, 0x24),
         ("special", 0x2BC, 8), ("collision", 0x2C4, 0xBC), ("sky", 0x380, 16))
FACE_OFFSETS = (156, 172, 316, 332, 476, 492, 636, 652)

# Whole independently authored canonical schema5 literal, not a codec dump.
# Stable IDs444..777 identify NW/NE/SW/SE;111..333 identify Wizard/Grass/family.
CANONICAL = (
    '{"kind":"spyro-native-fresh-level","schema":5,"content":{"tag":"ordered-flat-tiles-v1","tiles":['
    '{"id":"44444444444444444444444444444444","x":8448,"y":8448,"z":8704,"width":1536,"depth":1536,"floorRgb":[80,128,112],"materials":{"layout":"rectangle-two-triangles-v1","triangle0":{"tag":"retail-terrain-record","sourceVariant":"spyro1-usa-retail-mode2-2352-v1","sourceWadEntry":14,"textureId":1},"triangle1":{"tag":"retail-terrain-record","sourceVariant":"spyro1-usa-retail-mode2-2352-v1","sourceWadEntry":14,"textureId":1}}},'
    '{"id":"55555555555555555555555555555555","x":9984,"y":8448,"z":8704,"width":1536,"depth":1536,"floorRgb":[80,128,112],"materials":{"layout":"rectangle-two-triangles-v1","triangle0":{"tag":"retail-terrain-record","sourceVariant":"spyro1-usa-retail-mode2-2352-v1","sourceWadEntry":14,"textureId":1},"triangle1":{"tag":"retail-terrain-record","sourceVariant":"spyro1-usa-retail-mode2-2352-v1","sourceWadEntry":70,"textureId":1}}},'
    '{"id":"66666666666666666666666666666666","x":8448,"y":9984,"z":8704,"width":1536,"depth":1536,"floorRgb":[80,128,112],"materials":{"layout":"rectangle-two-triangles-v1","triangle0":{"tag":"retail-terrain-record","sourceVariant":"spyro1-usa-retail-mode2-2352-v1","sourceWadEntry":14,"textureId":1},"triangle1":{"tag":"retail-terrain-record","sourceVariant":"spyro1-usa-retail-mode2-2352-v1","sourceWadEntry":70,"textureId":1}}},'
    '{"id":"77777777777777777777777777777777","x":9984,"y":9984,"z":8704,"width":1536,"depth":1536,"floorRgb":[80,128,112],"materials":{"layout":"rectangle-two-triangles-v1","triangle0":{"tag":"retail-terrain-record","sourceVariant":"spyro1-usa-retail-mode2-2352-v1","sourceWadEntry":70,"textureId":1},"triangle1":{"tag":"retail-terrain-record","sourceVariant":"spyro1-usa-retail-mode2-2352-v1","sourceWadEntry":70,"textureId":1}}}'
    '],"spawn":{"x":8960,"y":8960,"z":9216,"heading":64},"skyRgb":[128,96,64],"musicTrackId":19},"placements":['
    '{"id":"11111111111111111111111111111111","tag":"wizard-route","origin":{"x":9984,"y":9984,"z":8704},"routeEnd":{"x":9472,"y":9984}},'
    '{"id":"22222222222222222222222222222222","tag":"native-moby","source":{"sourceVariant":"spyro1-usa-retail-mode2-2352-v1","sourceWadEntry":10,"sourceTrueIndex":121,"expectedNativeClass":501},"position":{"x":9152,"y":9152,"z":8704},"nativeYawByte":64},'
    '{"id":"33333333333333333333333333333333","tag":"locked-chest-family","source":{"sourceVariant":"spyro1-usa-retail-mode2-2352-v1","definitionId":"peace-keepers-locked-chest-v1"},"key":{"x":9728,"y":9120,"z":8704},"chest":{"x":10864,"y":9888,"z":8704},"rewardDestinations":[{"role":"reward.2","x":10454,"y":10503,"z":9216}]}]}'
).encode("ascii")

# Frozen only after this independent source-free assembly is first executed.
# This oracle never writes or updates these expectations itself.
PINS = {
    "canonicalBytes": 2615,
    "canonicalSha256": "2bc9a53da03fdc5d5c8bfe6bc11b1c46fbbdd1db7ae089c1df8a664c20e5b375",
    "surfaceBytes": 912,
    "surfaceSha256": "6af708ea930471156cf3e68406eef231d2ff8b195d6ca1a832a9406c731af23f",
    "boundSurfaceSha256": "a18fbf9c2bc48e8d4b6290e7250236773daaf13fe4d8733fb6526cc28d24c11b",
    "components": [
        {"name": "environment", "offset": 0, "length": 664,
         "sha256": "c2044410114a81f59effc512c70f537d2b6e8ddab50ea88d452331b293f62b60"},
        {"name": "visibility", "offset": 664, "length": 36,
         "sha256": "4c0fc4a651ff0b049cf45e644d5cc255329995436d0b7edb1c461978b92510a0"},
        {"name": "special", "offset": 700, "length": 8,
         "sha256": "6cc16abd70eefb90dc0ba0d14fb088630873b2c6ad943f7442356735984c35a3"},
        {"name": "collision", "offset": 708, "length": 188,
         "sha256": "bfbd5b2344cf1bced9b8a69b9c436e1d3e203a167868817d28e4c6bc18262188"},
        {"name": "sky", "offset": 896, "length": 16,
         "sha256": "7087a293dfe99594734f35672b06831f0da00b001907678d16d63786c023ef53"},
    ],
}


def require(ok, label):
    if not ok:
        raise AssertionError(label)


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def words(*values):
    return struct.pack("<" + "I" * len(values), *values)


def halves(*values):
    return struct.pack("<" + "H" * len(values), *values)


def word(raw, at):
    require(type(at) is int and at >= 0 and at % 4 == 0 and at + 4 <= len(raw), "bounded aligned word")
    return struct.unpack_from("<I", raw, at)[0]


def preserved_control():
    for relative, expected in HELPERS.items():
        path = ROOT / relative
        require(sha(path.read_bytes()) == expected, "pinned source-free helper changed")
        spec = importlib.util.spec_from_file_location("preserved_tiled_surface", path)
        helper = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(helper)
        require(sha(helper.fresh()) == "809c2442de920c36fe2bb97db36abd82b2e5888322c95bee51e661c1408ec3d1",
                "preserved three-tile control")


def surface():
    """Closed literal byte recipe: no document parser or production encoder."""
    sectors, triangles = [], []
    for x, y in ((528, 528), (624, 528), (528, 624), (624, 624)):
        vertices = (0, 96 << 21, 96 << 10, 96 << 21 | 96 << 10)
        header = words((x + 48) << 16 | (y + 48), 544 << 16 | 192, x << 16 | y, 544 << 16)
        header += bytes((4, 4, 2, 0, 4, 4, 2, 12)) + words(0xFFFFFFFF)
        # Literal LP and HP faces; LP uses two copies, HP two index/color words.
        lp = words(0x00108200, 0x00108200, 0x04308200, 0x04308200)
        hp = words(0x02010000, 0x02010000, 0xFF, 0x80,
                   0x02030101, 0x02030101, 0xFF, 0x80)
        sectors.append(header + words(*vertices) + words(*([0x708050] * 4)) + lp +
                       words(*vertices) + words(*([0x708050] * 8)) + hp + words(0))
        # Native land A,C,B / B,C,D, opposite render winding; signed nine-bit deltas.
        triangles.append(words(x | 96 << 23, y | 96 << 14, 544,
                               x + 96 | 416 << 14, y | 96 << 14 | 96 << 23, 544))
    environment = words(664, 4, 20, 180, 340, 500) + b"".join(sectors)
    visibility = words(36, 20, 1, 12) + bytes((0, 1, 2, 3, 255, 255, 255, 255)) + words(1, 8, 0xFFFFFFFF)
    # Exactly Z2 -> Y2 -> X2, then one complete descending group and final marker.
    tree = halves(3, 65535, 65535, 8, 3, 65535, 65535, 16, 3, 65535, 65535, 0)
    blocks = halves(0x8007, 6, 5, 4, 3, 2, 1, 0, 0x8000, 0)
    collision = words(188, 8, 8, 28, 52, 72, 168, 176) + tree + blocks + b"".join(triangles) + bytes(8) + bytes([63] * 8)
    return environment + visibility + words(8, 0) + collision + words(16, 0x406080, 0, 0)


def validate_document(raw):
    require(type(raw) is bytes and 0 < len(raw) <= 65536, "bounded canonical document bytes")
    def unique(pairs):
        result = {}
        for key, value in pairs:
            require(key not in result, "duplicate JSON field")
            result[key] = value
        return result
    doc = json.loads(raw.decode("utf-8"), object_pairs_hook=unique)
    require(list(doc) == ["kind", "schema", "content", "placements"] and
            doc["kind"] == "spyro-native-fresh-level" and type(doc["schema"]) is int and doc["schema"] == 5,
            "whole schema5 root only")
    content = doc["content"]
    require(content["tag"] == "ordered-flat-tiles-v1" and len(content["tiles"]) == 4, "all four tiled owners")
    sources, faces = [], []
    for index, tile in enumerate(content["tiles"]):
        require(tuple(tile[k] for k in ("x", "y")) == TILES[index] and
                all(type(tile[k]) is int for k in ("x", "y", "z", "width", "depth")) and
                (tile["z"], tile["width"], tile["depth"]) == (8704, 1536, 1536) and
                tile["id"] == str(index + 4) * 32 and tile["floorRgb"] == [80, 128, 112],
                "ordered exact rectangle, IDs, elevation and colors")
        require(tile["materials"]["layout"] == "rectangle-two-triangles-v1", "two explicit face owners")
        for name in ("triangle0", "triangle1"):
            material = tile["materials"][name]
            require(material["tag"] == "retail-terrain-record" and material["sourceVariant"] == VARIANT and
                    type(material["sourceWadEntry"]) is int and material["textureId"] == 1, "neutral T1 descriptors")
            key = (material["sourceWadEntry"], material["textureId"])
            if key not in sources:
                sources.append(key)
            faces.append(sources.index(key))
    require(sources == [(14, 1), (70, 1)] and tuple(faces) == FACES, "complete first-occurrence table and eight bindings")
    # The independent literal closes every remaining authored field and key order.
    require(json.dumps(doc, separators=(",", ":"), ensure_ascii=True).encode("ascii") == CANONICAL and raw == CANONICAL,
            "exact canonical full document including all three placements and explicit Reward2")
    return doc


def signed9(value):
    value &= 511
    return value - 512 if value >= 256 else value


def validate_surface(raw, bound=False):
    require(type(raw) is bytes and len(raw) == 912, "complete four-tile surface length")
    for name, at, length in SPANS:
        require(word(raw, at) == length, "complete component: " + name)
    require(word(raw, 4) == 4 and tuple(word(raw, 8 + 4 * i) for i in range(4)) == (20, 180, 340, 500),
            "directory covers all four guarded sectors")
    for index, (world_x, world_y) in enumerate(TILES):
        at = 4 + word(raw, 8 + 4 * index)
        sector = raw[at:at + 160]
        x, y = world_x // 16, world_y // 16
        require(tuple(word(sector, i) for i in (0, 4, 8, 12)) ==
                ((x + 48) << 16 | (y + 48), 544 << 16 | 192, x << 16 | y, 544 << 16), "sector location/culling")
        require(sector[16:28] == bytes((4, 4, 2, 0, 4, 4, 2, 12)) + words(0xFFFFFFFF) and
                sector[156:] == bytes(4), "tables, signed scene sentinels and independent lookahead guard")
        expected_vertices = ((0, 0, 0), (96, 0, 0), (0, 96, 0), (96, 96, 0))
        for start in (28, 76):
            got = tuple(((word(sector, start + 4 * i) >> 21) & 2047,
                         (word(sector, start + 4 * i) >> 10) & 2047,
                         word(sector, start + 4 * i) & 1023) for i in range(4))
            require(got == expected_vertices, "LP/HP independently decoded vertices")
        for start, count in ((44, 4), (92, 8)):
            require(all(word(sector, start + i * 4) == 0x708050 for i in range(count)), "complete color owners")
        for face, expected in enumerate(((0, 1, 2), (1, 3, 2))):
            for offset in (60 + face * 8, 64 + face * 8):
                value = word(sector, offset)
                require(value & 255 == 0 and tuple((value >> shift) & 63 for shift in (26, 20, 14, 8)) ==
                        (*expected, expected[2]), "LP face winding/indices/reserved byte")
            for offset in (124 + face * 16, 128 + face * 16):
                value = word(sector, offset)
                require(tuple((value >> shift) & 255 for shift in (0, 8, 16, 24)) ==
                        (expected[0], *expected), "HP face/color indices")
            require(word(sector, 132 + face * 16) == (FACES[index * 2 + face] if bound else 255) and
                    word(sector, 136 + face * 16) == 128, "exact per-face slot and following primitive flags")
    require(raw[664:700] == words(36, 20, 1, 12) + bytes((0, 1, 2, 3, 255, 255, 255, 255)) + words(1, 8, 0xFFFFFFFF),
            "visibility contains all four sectors with complete terminators and sky")
    require(raw[700:708] == words(8, 0) and raw[896:] == words(16, 0x406080, 0, 0), "special and zero-sky guard")
    collision = raw[708:896]
    require(tuple(word(collision, i * 4) for i in range(8)) == (188, 8, 8, 28, 52, 72, 168, 176), "five collision-relative pointers")
    require(collision[32:56] == halves(3, 65535, 65535, 8, 3, 65535, 65535, 16, 3, 65535, 65535, 0),
            "one actual collision cell with exact byte/word links")
    require(collision[56:76] == halves(0x8007, 6, 5, 4, 3, 2, 1, 0, 0x8000, 0), "all eight descending triangles, final marker and padding")
    for tile, (x, y) in enumerate(TILES):
        corners = ((x, y, 8704), (x + 1536, y, 8704), (x, y + 1536, 8704), (x + 1536, y + 1536, 8704))
        for face, expected in enumerate(((corners[0], corners[2], corners[1]), (corners[1], corners[2], corners[3]))):
            axes = [word(collision, 76 + tile * 24 + face * 12 + axis * 4) for axis in range(3)]
            actual = tuple(tuple(16 * ((value & 16383) + (0 if vertex == 0 else signed9(value >> (14 if vertex == 1 else 23))))
                                 for value in axes) for vertex in range(3))
            require(actual == expected, "independent signed collision decode and upward native winding")
            # Centroid lies on the same material side of the original R03 diagonal.
            total = sum(p[0] + p[1] for p in actual)
            require(FACES[tile * 2 + face] == (0 if total < 3 * 19968 else 1), "old full-floor material region preserved")
    require(collision[172:180] == bytes(8) and collision[180:] == bytes([63] * 8), "all assignments and flags")


def bind_materials(raw, faces):
    before = bytes(raw)
    validate_surface(before)
    require(type(faces) is tuple and len(faces) == 8, "only the complete finite immutable eight-face fixture")
    captured = faces
    require(captured == FACES and all(type(value) is int for value in captured), "exact bounded fixture face sequence")
    result = bytearray(before)
    for offset, slot in zip(FACE_OFFSETS, captured):
        struct.pack_into("<I", result, offset, slot)
    result = bytes(result)
    validate_surface(result, bound=True)
    owned = {offset + i for offset in FACE_OFFSETS for i in range(4)}
    require(all(result[i] == before[i] for i in range(len(before)) if i not in owned), "complete nonbinding complement")
    inverse = bytearray(result)
    for offset in FACE_OFFSETS:
        struct.pack_into("<I", inverse, offset, 255)
    require(bytes(inverse) == before, "complete neutral binding inverse")
    return result


def self_test():
    preserved_control()
    doc = validate_document(CANONICAL)
    raw = surface()
    validate_surface(raw)
    bound = bind_materials(raw, FACES)
    negatives = []
    def reject(label, operation):
        try:
            operation()
        except (AssertionError, ValueError, KeyError, TypeError, UnicodeError):
            negatives.append(label)
            return
        raise AssertionError("negative accepted: " + label)
    def changed_document(path, value):
        changed = copy.deepcopy(doc)
        owner = changed
        for key in path[:-1]:
            owner = owner[key]
        owner[path[-1]] = value
        return json.dumps(changed, separators=(",", ":")).encode("ascii")
    for label, path, value in (
        ("schema4", ("schema",), 4), ("extra-root-authority", ("runtimeVerified",), True),
        ("last-tile-only-coordinate", ("content", "tiles", 3, "x"), 10000),
        ("last-tile-only-ID", ("content", "tiles", 3, "id"), "8" * 32),
        ("tile-gap", ("content", "tiles", 1, "x"), 10000),
        ("tile-overlap", ("content", "tiles", 1, "x"), 9968),
        ("elevation", ("content", "tiles", 2, "z"), 8720),
        ("oversize", ("content", "tiles", 0, "width"), 3072),
        ("wrong-diagonal-partition", ("content", "tiles", 0, "materials", "triangle1", "sourceWadEntry"), 70),
        ("material-T0", ("content", "tiles", 3, "materials", "triangle1", "textureId"), 0),
        ("spawn", ("content", "spawn", "y"), 8832), ("music", ("content", "musicTrackId"), 18),
        ("sky", ("content", "skyRgb"), [128, 96, 65]),
        ("missing-tile", ("content", "tiles"), doc["content"]["tiles"][:-1]),
        ("extra-tile", ("content", "tiles"), doc["content"]["tiles"] + [doc["content"]["tiles"][0]]),
        ("reordered-tiles", ("content", "tiles"), list(reversed(doc["content"]["tiles"]))),
        ("missing-family", ("placements",), doc["placements"][:2]),
        ("reordered-actors", ("placements",), list(reversed(doc["placements"]))),
        ("route", ("placements", 0, "routeEnd", "x"), 9488),
        ("grass-source", ("placements", 1, "source", "sourceTrueIndex"), 120),
        ("chest-anchor", ("placements", 2, "chest", "y"), 9904),
        ("override-absence", ("placements", 2, "rewardDestinations"), None),
        ("override-wrong-role", ("placements", 2, "rewardDestinations", 0, "role"), "reward.5"),
        ("override-coordinate", ("placements", 2, "rewardDestinations", 0, "y"), 10502),
    ):
        reject(label, lambda path=path, value=value: validate_document(changed_document(path, value)))
    reject("duplicate-field", lambda: validate_document(CANONICAL.replace(b'"schema":5', b'"schema":5,"schema":5', 1)))
    reject("document-truncation", lambda: validate_document(CANONICAL[:-1]))
    reject("document-extra-byte", lambda: validate_document(CANONICAL + b" "))
    # Distinct owners/boundaries, not every-byte mutation counts.
    probes = {0, 4, 8, 20, 24, 40, 52, 84, 100, 148, 664, 680, 688, 696,
              700, 704, 708, 712, 720, 724, 728, 732, 736, 740, 762, 764,
              780, 782, 784, 804, 808, 876, 879, 880, 887, 888, 895, 896, 900, 908}
    probes.update(FACE_OFFSETS)
    probes.update(offset + 4 for offset in FACE_OFFSETS)
    probes.update(24 + 160 * tile + 156 for tile in range(4))
    for offset in sorted(probes):
        corrupt = bytearray(raw)
        corrupt[offset] ^= 1
        reject("surface-owner-" + hex(offset), lambda corrupt=corrupt: validate_surface(bytes(corrupt)))
    for offset in FACE_OFFSETS:
        corrupt = bytearray(bound)
        corrupt[offset] = 255
        reject("bound-face-" + hex(offset), lambda corrupt=corrupt: validate_surface(bytes(corrupt), True))
    reject("surface-truncation", lambda: validate_surface(raw[:-1]))
    reject("surface-extra-word", lambda: validate_surface(raw + words(0)))
    for faces in (FACES[:-1], FACES + (0,), (255,) + FACES[1:], (2,) + FACES[1:], (False,) + FACES[1:]):
        reject("bad-binding-" + repr(faces), lambda faces=faces: bind_materials(raw, faces))
    report = {
        "canonicalBytes": len(CANONICAL), "canonicalSha256": sha(CANONICAL),
        "surfaceBytes": len(raw), "surfaceSha256": sha(raw), "boundSurfaceSha256": sha(bound),
        "components": [{"name": name, "offset": at, "length": length, "sha256": sha(raw[at:at + length])}
                       for name, at, length in SPANS],
        "orderedSourceSelections": [[14, 1], [70, 1]], "orderedFaceBindings": list(FACES),
        "faceOffsets": list(FACE_OFFSETS), "negativeControls": len(negatives),
        "sourceOpened": False, "sourceCertified": False, "bodyIssued": False,
        "runtimeVerified": False, "publicationApproved": False,
    }
    print(json.dumps(report, indent=2))
    require(PINS and all(report[name] == value for name, value in PINS.items()), "reference pins not yet frozen or changed")
    print("TILED_FAMILY_SURFACE_ORACLE_OK neutral=true sourceOpened=false bodyIssued=false runtime=false publication=false")
    return report


if __name__ == "__main__":
    require(sys.argv[1:] == ["--self-test"], "Only the source-free --self-test mode is supported; no input/output paths")
    self_test()
