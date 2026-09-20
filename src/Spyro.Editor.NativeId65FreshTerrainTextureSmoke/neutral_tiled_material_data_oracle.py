"""Closed neutral tiled-material fixtures, independent of Core and source ROMs.

No source is accepted on the command line, no files are written, and no output
here grants allocation, source issuance, body, publication or runtime authority.
The existing three-tile geometry oracle is imported unchanged after a code pin.
"""
import hashlib
import importlib.util
import json
from pathlib import Path
import struct
import sys

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[2]
HELPER = ROOT / "src/Spyro.Editor.NativeId65FreshDocumentSmoke/source_tiled_surface_oracle.py"
HELPER_SHA = "4aa6bc207bed327c8fb2f9832b8bd1bc098b26b0f50a4817ba55183373be0ec3"
# Independent neutral execution329, before the production compositor was built.
EXPECTED = {
    "reused": "d879542a2349aa43090ca12d20962d19d79271d0dcbf17fd99b73d36ce7fee2f",
    "reversed": "6d4bd727ac9fbe9e45a773557d7d101fb1dc066e1045ba6b99cc9421f0bb8c6e",
    "mixed-ff": "758204003ead368f31140991d990c15d716a8232fac32cf01637de04dacb5b16",
    "all-ff-two-records": "c319099d04f864274fc55e2796fcdd274956e261b2e616a3a1830df1b8ce04d5",
    "reversed-records": "ff4ca301eb98af2b8594d6a6fc9c4d3ea88a9e61cfbdba49378e74fa10dc4ce4",
    "duplicate-records": "cc80a320ebbc877d8c958c91e387b48ae285af867ebd8713fa8ca086ff2d6d5e",
    "zero-records": "541d82249eca0cb69c5a31e51fee294e9c5739e1ff9f50d5e1838f34f71e8949",
    "capacity-FFC": "7049763cda5a3083d8de5a7b43bbc71246e463a4db1f537b389e0ed19cc13fcf",
    "capacity-1000": "ec75ec96ca1c76003f36e0a8e12d5f7c1254792893949a8c1c98a020dc1dcf67",
    # Independent neutral execution330; fixture bytes and old pins unchanged.
    "records-1": "dbd8a944da1f9fee91a820c3f21826f238f4d4ad0410d0b5dbf4cfe6fd4cc5e3",
    "records-3": "85c56b9a0d219f98e01e37f747581a46834a468a26157b4551f35ae1d5471a6e",
    "records-7": "858138c128eb0e8f57c8449e9eb72c715631c031ce5701e44d771357cbeb3b04",
}


def sha(data):
    return hashlib.sha256(data).hexdigest()


def require(value, label):
    if not value:
        raise AssertionError(label)


def words(*values):
    return struct.pack("<" + "I" * len(values), *(v & 0xFFFFFFFF for v in values))


def halfwords(*values):
    return struct.pack("<" + "H" * len(values), *values)


def word(data, at):
    require(at >= 0 and at % 4 == 0 and at + 4 <= len(data), "Complete bounded word")
    return struct.unpack_from("<I", data, at)[0]


def put(data, at, value):
    struct.pack_into("<I", data, at, value)


def tiny_surface(z_cell):
    """Eight adjacent16-square tiles; one XY leaf and a known Z segment."""
    require(z_cell in (56, 58, 60), "Closed boundary fixture only")
    sectors, triangles, z = [], [], z_cell * 256
    for x in range(8):
        vertices = (0, 1 << 21, 1 << 10, 1 << 21 | 1 << 10)
        sector = words(x << 16, z << 16 | 2, x << 16, z << 16)
        sector += bytes((4, 4, 2, 0, 4, 4, 2, 12)) + words(0xFFFFFFFF)
        lp, hp = [], []
        for a, b, c in ((0, 1, 2), (1, 3, 2)):
            low = a << 26 | b << 20 | c << 14 | c << 8
            high = a | a << 8 | b << 16 | c << 24
            lp.append(words(low, low))
            hp.append(words(high, high, 255, 128))
        sector += (words(*vertices) + words(*([0x708050] * 4)) + b"".join(lp) + words(*vertices)
                   + words(*([0x708050] * 8)) + b"".join(hp))
        require(len(sector) == 156, "Exact fixture sector")
        sectors.append(sector + words(0))
        triangles.append(words(x | 1 << 23, 1 << 14, z, x + 1 | 511 << 14, 1 << 14 | 1 << 23, z))
    environment = words(1320, 8, *(36 + 160 * i for i in range(8))) + b"".join(sectors)
    visibility = words(40, 24, 1, 12) + bytes(range(8)) + bytes([255] * 4) + words(1, 8) + bytes([255] * 4)
    y_start = (z_cell + 2) * 2
    tree = halfwords(z_cell + 1, *([65535] * z_cell), y_start, 1, y_start + 4, 1, 0)
    tree += bytes((-len(tree)) % 4)
    blocks = halfwords(0x800F, *range(14, -1, -1), 0x8000, 0)
    triangle_start = 32 + len(tree) + len(blocks)
    assignments = triangle_start + 192
    flags = assignments + 16
    collision = (words(flags + 16, 16, 16, 28, 28 + len(tree), triangle_start - 4, assignments - 4, flags - 4)
                 + tree + blocks + b"".join(triangles) + bytes(16) + bytes([63] * 16))
    require(len(environment) == 1320 and len(visibility) == 40 and len(blocks) == 36,
            "Boundary owners have complete independently known spans")
    return environment + visibility + words(8, 0) + collision + words(16, 0x406080, 0, 0)


def patterned_data(surface):
    tail = bytearray((i * 29 + i // 17) & 255 for i in range(0x77C))
    for at, value in ((0, 0), (4, 0x80), (8, 10), (0x84, 0x6F8), (0x188, 76)):
        put(tail, at, value)
    return words(8, 0) + surface + bytes(tail)


def record(index):
    result = bytearray()
    for i in range(23):
        low = i < 3
        raw = bytearray.fromhex("000020201f000880" if low else "004020281f408880")
        raw[0] = index * (2 if low else 8)
        raw[4] = raw[0] + 31
        result += raw
    require(len(result) == 184 and result[:8] == result[8:16] == result[16:24], "Complete synthetic static record")
    return bytes(result)


def face_offsets(data):
    require(data[:8] == words(8, 0), "Only untextured input metadata")
    environment, count = 8, word(data, 12)
    require(count in (3, 8) and word(data, 8) == 8 + count * 164, "Closed fixture environment count/length")
    offsets = []
    for index in range(count):
        start = environment + 4 + word(data, environment + 8 + index * 4)
        require(start == environment + 8 + count * 4 + index * 160, "Actual ordered directory ownership")
        require(data[start + 16:start + 24] == bytes((4, 4, 2, 0, 4, 4, 2, 12)), "Canonical sector table shape")
        # Four LP vertices/colors, two LP faces, four HP vertices/two color
        # banks, then two16-byte HP face records; material is word2 of each.
        high_faces = start + 28 + 4 * 4 + 4 * 4 + 2 * 8 + 4 * 4 + 2 * 4 * 4
        for face in range(2):
            at = high_faces + face * 16 + 8
            require(word(data, at) == 255 and word(data, at + 4) == 128, "Exact FF/80 preimage")
            offsets.append(at)
        require(data[start + 156:start + 160] == bytes(4), "Separate zero prefetch guard")
    return offsets


def compose(data, records, faces, clip=0x8000):
    offsets = face_offsets(data)
    require(clip == 0x8000 and 0 <= len(records) <= 128 and len(faces) == len(offsets), "Explicit clip/table/face count bounds")
    require(all(value == 255 or 0 <= value < len(records) for value in faces), "No ID truncation or masking")
    require(all(r in tuple(record(i) for i in range(8)) for r in records), "Closed canonical neutral record fixtures")
    metadata = words(8 + 184 * len(records), len(records))
    metadata += b"".join(r[:16] for r in records) + b"".join(r[16:] for r in records)
    output = bytearray(metadata + data[8:])
    require(len(output) <= 0x1000, "Exact current data capacity")
    growth = len(metadata) - 8
    for at, material in zip(offsets, faces):
        put(output, at + growth, material)
    changed_owners = {at + byte for at in offsets for byte in range(4)}
    require(all(data[i] == output[i + growth] for i in range(8, len(data)) if i not in changed_owners),
            "Every translated protected geometry/component/tail byte retained")
    require(all(word(output, at + growth + 4) == 128 for at in offsets), "Every direct flag80 retained")
    restored = bytearray(words(8, 0) + output[len(metadata):])
    for at in offsets:
        put(restored, at, 255)
    require(restored == data, "Complete neutral inverse")
    for index, original in enumerate(records):
        rebuilt = output[8 + index * 16:24 + index * 16]
        hq = 8 + len(records) * 16 + index * 168
        rebuilt += output[hq:hq + 168]
        require(rebuilt == original, "Every complete ordered record reconstructed")
    return bytes(output)


def main():
    require(len(sys.argv) == 1, "This oracle accepts no source path or other input arguments")
    require(sha(HELPER.read_bytes()) == HELPER_SHA, "Preserved independent surface oracle pin")
    spec = importlib.util.spec_from_file_location("preserved_neutral_tiled_surface", HELPER)
    geometry = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(geometry)
    data = patterned_data(geometry.fresh())
    require(len(data) == 0xA58 and face_offsets(data) == [0xA0, 0xB0, 0x140, 0x150, 0x1E0, 0x1F0],
            "Frozen independent six-face geometry and full input extent")
    require(sha(data) == EXPECTED["zero-records"], "Frozen complete neutral input")
    rows = (record(0), record(1))
    cases = []
    fixtures = (("reused", rows, [0, 1, 0, 1, 0, 1]), ("reversed", rows, [1, 0, 1, 0, 1, 0]),
                ("mixed-ff", rows, [255, 1, 0, 255, 1, 0]), ("all-ff-two-records", rows, [255] * 6),
                ("reversed-records", rows[::-1], [0, 1, 1, 0, 0, 1]),
                ("duplicate-records", (rows[0], rows[0]), [0, 1, 0, 1, 0, 1]), ("zero-records", (), [255] * 6))
    fixtures += tuple((f"records-{count}", tuple(record(i) for i in range(count)),
                       [count - 1 if i == 5 else i % count for i in range(6)]) for count in (1, 3, 7))
    for name, records, faces in fixtures:
        output = compose(data, records, faces)
        require(output == compose(data, records, faces), "Repeat-call determinism")
        if name in EXPECTED:
            require(sha(output) == EXPECTED[name], "Frozen independent full-data output")
        cases.append(dict(name=name, inputSha256=sha(data), outputSha256=sha(output), usedBytes=len(output),
                          metadataBytes=8 + 184 * len(records), faces=faces))
    try:
        compose(data, tuple(record(i) for i in range(8)), [0, 1, 2, 3, 4, 7])
    except AssertionError as error:
        require(str(error) == "Exact current data capacity", "Eight records refuse1018 at actual capacity")
    else:
        raise AssertionError("Eight-record overflow was accepted")
    for cell, expected in ((56, 0xFFC), (58, 0x1000), (60, 0x1004)):
        before = patterned_data(tiny_surface(cell))
        require(len(before) + 0x170 == expected, "Independent boundary arithmetic")
        if expected > 0x1000:
            try:
                compose(before, rows, [i % 2 for i in range(16)])
            except AssertionError as error:
                require(str(error) == "Exact current data capacity", "Overflow refuses at the intended capacity boundary")
            else:
                raise AssertionError("Overflow was accepted")
            cases.append(dict(name="capacity-1004-refused", inputSha256=sha(before), prospectiveUsedBytes=expected))
        else:
            output = compose(before, rows, [i % 2 for i in range(16)])
            require(sha(output) == EXPECTED[f"capacity-{expected:X}"], "Frozen exact-boundary full-data output")
            cases.append(dict(name=f"capacity-{expected:X}", inputSha256=sha(before), outputSha256=sha(output), usedBytes=len(output)))
    print(json.dumps(dict(marker="NATIVE_TILED_MATERIAL_DATA_NEUTRAL_ORACLE_OK", sourceRead=False, fileWrites=False,
                          sourceAuthority=False, runtimeVerified=False, publicationApproved=False,
                          protectedTranslatedComplement=True, completeNeutralInverse=True, cases=cases), indent=2))


if __name__ == "__main__":
    main()
