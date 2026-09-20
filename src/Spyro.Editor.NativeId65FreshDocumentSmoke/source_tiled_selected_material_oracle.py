"""Independent N1/N3 source capture, packing and six-face data composition.

Original-only raw reads, preserved N2 controls, immutable returned snapshots.
No Core/DLL, output files, scene/body issuer or GPU/gameplay approval. New
reference hashes are printed for review before any production comparison.
"""
import contextlib
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import struct
import sys
from typing import NamedTuple

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[2]
SIZE, SECTORS = 661547040, 281270
ORIGINAL = "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37"
HELPERS = {
    "src/Spyro.Editor.NativeId65FreshTerrainTextureSmoke/source_texture_allocation_oracle.py":
        "c4ee51758031edb3d2b9cab9279985a537d0c06d1251cd24ab090dec67b07d6f",
    "src/Spyro.Editor.NativeId65FreshTerrainTextureSmoke/source_fresh_upload_fixture.py":
        "727ea1028d36db37c70e25a112836c4740b967a667aad64c78f6a900786b6cfe",
    "src/Spyro.Editor.NativeId65FreshDocumentSmoke/source_tiled_material_oracle.py":
        "50fadadfe2f668705d60d203b80c01f4da2b28c012ab017238d4d6d554738237",
    "src/Spyro.Editor.NativeId65FreshDocumentSmoke/source_tiled_surface_oracle.py":
        "4aa6bc207bed327c8fb2f9832b8bd1bc098b26b0f50a4817ba55183373be0ec3",
}
DONOR_DATA = {
    14: "a937aecb3626ffd2311ba136684a34c9f36877054b52557b4524fcacee0e2d97",
    70: "b2a25147c4d81e7fd366a830546b92e6a482fe9a56f8a429a30fcb4857091a02",
}
# Original record evidence predates this oracle and the generalized issuer.
DONOR_RECORDS = {
    (14, 0): "7b074611db43208653a931fb12c98ee64dbc5f8f41ef4edd7b1e5e796a4e0954",
    (70, 0): "2ffdf4bc69645c31a1cc75862288bf3620955567ef65e35b2f8f65862b7dda72",
    (14, 1): "7bf72a8271c6cd6f32dad0293c80f825fb2a7d6eccfa6a056622cf406a27d276",
}
CASE_SPECS = (
    ("one-six-textured", ((14, 0),), (0, 0, 0, 0, 0, 0)),
    ("one-mixed-base", ((14, 0),), (0, 255, 0, 255, 0, 255)),
    ("three-six-textured", ((14, 0), (70, 0), (14, 1)), (0, 1, 2, 0, 1, 2)),
    ("three-mixed-base", ((14, 0), (70, 0), (14, 1)), (0, 1, 255, 2, 0, 255)),
    ("three-reordered", ((14, 1), (14, 0), (70, 0)), (0, 1, 2, 0, 2, 1)),
)


class Source(NamedTuple):
    entry: int
    texture: int
    handle: str
    data_sha256: str
    scene_sha256: str
    record: bytes
    vram: bytes


class Fixture(NamedTuple):
    name: str
    sources: tuple
    faces: tuple
    upload: bytes
    data: bytes
    records: tuple
    regions: tuple
    aliases: tuple
    allocated_bytes: int
    free_bytes: int


class Captured(NamedTuple):
    before_upload: bytes
    ownership: bytes
    before_data: bytes
    tail: bytes
    cases: tuple
    control_json: str


def require(ok, label):
    if not ok:
        raise AssertionError(label)


def sha(data):
    return hashlib.sha256(data).hexdigest()


def word(data, at=0):
    require(at >= 0 and at % 4 == 0 and at + 4 <= len(data), "Complete bounded aligned word")
    return struct.unpack_from("<I", data, at)[0]


def require_helpers():
    for relative, expected in HELPERS.items():
        require(sha((ROOT / relative).read_bytes()) == expected, "Preserved helper changed: " + relative)


def load(relative, name):
    path = ROOT / relative
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec)
    previous = sys.argv
    sys.argv = [str(path)]
    try:
        spec.loader.exec_module(result)
    finally:
        sys.argv = previous
    return result


def require_original(source):
    require(source.seek(0, 2) == SIZE, "Complete original source size")
    source.seek(0)
    require(hashlib.file_digest(source, "sha256").hexdigest() == ORIGINAL, "Complete original source hash")
    require(source.tell() == SIZE and source.read(1) == b"" and source.seek(0, 2) == SIZE, "Complete original EOF")


def source_index(source):
    require(source.seek(0, 2) == SIZE, "Complete indexed source size")
    source.seek(0)
    index, digest = bytearray(SECTORS * 32), hashlib.sha256()
    for lba in range(SECTORS):
        raw = source.read(2352)
        require(len(raw) == 2352, "Complete source sector during authentication")
        digest.update(raw)
        index[lba * 32:(lba + 1) * 32] = hashlib.sha256(raw).digest()
    require(digest.hexdigest() == ORIGINAL and source.tell() == SIZE and source.read(1) == b"" and
            source.seek(0, 2) == SIZE, "Same-pass whole-source index, hash and EOF")
    return bytes(index)


def sector(source, index, lba):
    require(len(index) == SECTORS * 32 and 0 <= lba < SECTORS, "Bounded authenticated source LBA")
    source.seek(lba * 2352)
    raw = source.read(2352)
    require(len(raw) == 2352 and hashlib.sha256(raw).digest() == index[lba * 32:(lba + 1) * 32],
            "Later raw read matches its same-pass complete original preimage")
    return raw


def logical(source, index, lba, offset, length):
    require(lba >= 0 and offset >= 0 and length >= 0 and lba * 2048 + offset + length <= SECTORS * 2048,
            "Complete bounded logical source request")
    output = bytearray()
    while length:
        take = min(length, 2048 - offset % 2048)
        raw = sector(source, index, lba + offset // 2048)
        output.extend(raw[24 + offset % 2048:24 + offset % 2048 + take])
        offset += take
        length -= take
    return bytes(output)


def capture_sources(source, index, allocator):
    table = logical(source, index, 37, 0, 0x800)
    executable = logical(source, index, 53875, 0, 0x66000)
    require(sha(executable) == "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998", "Complete source executable")
    require(executable[0x34B4:0x34C8].hex() == "008002340780013c605720ac0880013ccc8522ac",
            "Actual original clip8000 initializer bytes")
    donors, selections = {}, []
    for entry, texture in DONOR_RECORDS:
        if entry not in donors:
            offset, size = struct.unpack_from("<II", table, entry * 8)
            require(offset >= 0x800 and 32 <= size <= 0x800000, "Bounded complete retail donor entry")
            data = logical(source, index, 37, offset, size)
            require(sha(data) == DONOR_DATA[entry], "Complete independent donor-level hash")
            page, page_length, metadata, data_length, model, model_length, scene, scene_length = struct.unpack_from("<8I", data)
            require(page >= 0x800 and page_length >= 0x80000 and page + page_length <= metadata and
                    metadata + data_length <= model and model + model_length <= scene and
                    scene_length >= 0x98 and scene + scene_length <= len(data), "Complete donor component ownership")
            count = word(data, metadata + 4)
            require(1 <= count <= 128 and word(data, metadata) == 8 + 184 * count and
                    8 + 184 * count <= data_length, "Complete native split metadata table")
            require(data[scene + 0x88:scene + 0x98] == bytes.fromhex("08000000000000000800000000000000"),
                    "Actual donor animation and scrolling control lists are both empty")
            donors[entry] = (data, page, metadata, count, sha(data[scene:scene + scene_length]))
        data, page, metadata, count, scene_sha = donors[entry]
        require(texture < count, "Selected actual record exists")
        lo, hi = metadata + 8 + texture * 16, metadata + 8 + count * 16 + texture * 168
        record = data[lo:lo + 16] + data[hi:hi + 168]
        require(len(record) == 184 and sha(record) == DONOR_RECORDS[(entry, texture)],
                "Actual split record agrees with independent pre-existing source evidence")
        vram = bytes(data[page:page + 0x80000])
        for pair in allocator.decode(record, len(selections)):
            for resource in pair:
                allocator.rectangle(vram, *resource[3:])
        handle = ("native-static-terrain-record-canonical-orientation0-clean-usa-v1:"
                  f"{ORIGINAL}:wad-{entry}:data-{DONOR_DATA[entry]}:t-{texture}:record-{sha(record)}")
        selections.append(Source(entry, texture, handle, DONOR_DATA[entry], scene_sha, bytes(record), vram))
    return tuple(selections)


def pack(before, ownership, sources, allocator):
    require(type(before) is bytes and len(before) == 0xDE000 and type(ownership) is bytes and
            len(ownership) == 0x80000 and 1 <= len(sources) <= 128, "Complete immutable neutral allocation domains")
    decoded = tuple(allocator.decode(source.record, slot) for slot, source in enumerate(sources))
    distinct = set(resource for entries in decoded for pair in entries for resource in pair)
    for left in distinct:
        for right in distinct:
            if left >= right or left[:3] != right[:3]:
                continue
            overlap = left[3] < right[3] + right[5] and right[3] < left[3] + left[5] and \
                left[4] < right[4] + right[6] and right[4] < left[4] + left[6]
            require(not overlap or allocator.contains(left, right) or allocator.contains(right, left),
                    "No unsupported partial overlap inside a source/material interpretation")
    owners, aliases = [], {}
    for resource in sorted(distinct, key=lambda q: (-q[5] * q[6], q)):
        parent = next((owner for owner in owners if allocator.contains(owner, resource)), None)
        if parent is None:
            owners.append(resource)
        else:
            aliases[resource] = parent
    occupied, allocated = bytearray(ownership), bytearray(0x80000)
    require(all(tag in (0, 1, 2) for tag in occupied), "Only explicit free/retained/detached ownership tags")
    require(all(before[i] == 0 for i, tag in enumerate(occupied) if tag == 0), "Explicit free owners contain only zero bytes")
    output, targets, regions = bytearray(before), {}, []
    for resource in sorted(owners, key=lambda q: (q[1] != "palette", -q[5] * q[6], q[0], q[4], q[3])):
        slot, kind, bpp, sx, sy, width, rows = resource
        point = None
        for y in range(0, 513 - rows, 4 if kind == "palette" and bpp == 4 else 1):
            if kind == "pixel" and y % 256 + rows > 256:
                continue
            for x in range(0, 1025 - width, 32 if kind == "palette" else 16):
                if all(not any(occupied[(y + row) * 1024 + x:(y + row) * 1024 + x + width]) for row in range(rows)):
                    point = (x, y)
                    break
            if point is not None:
                break
        require(point is not None, "Complete free rectangle exists; no protected ownership is reclaimed")
        x, y = targets[resource] = point
        payload = allocator.rectangle(sources[slot].vram, sx, sy, width, rows)
        for row in range(rows):
            at = (y + row) * 1024 + x
            require(ownership[at:at + width] == bytes(width) and allocated[at:at + width] == bytes(width),
                    "Every reserved target byte is explicitly free and disjoint")
            output[at:at + width] = payload[row * width:(row + 1) * width]
            occupied[at:at + width], allocated[at:at + width] = bytes([3]) * width, bytes([1]) * width
        regions.append((slot, kind, bpp, sy * 1024 + sx, y * 1024 + x, width, rows, sha(payload)))
    for child, parent in aliases.items():
        px, py = targets[parent]
        targets[child] = px + child[3] - parent[3], py + child[4] - parent[4]
    records = []
    for slot, source in enumerate(sources):
        result = bytearray(source.record)
        for descriptor, pair in enumerate(decoded[slot]):
            pixel, palette = pair
            x, y = targets[pixel]
            px, py = targets[palette]
            at, low = descriptor * 8, descriptor < 3
            u = x % 128 * (2 if low else 1)
            result[at], result[at + 1], result[at + 4], result[at + 5] = u, y % 256, u + pixel[6] - 1, y % 256
            result[at + 6] = (result[at + 6] & 0xE0) | (8 if low else 0x88) | x // 128 | (16 if y >= 256 else 0)
            if low:
                result[at + 2], result[at + 3] = (px + 1024) // 32, py // 4
            else:
                struct.pack_into("<H", result, at + 2, py * 64 + (px + 1024) // 32)
        relocated = allocator.decode(bytes(result), slot)
        for old_pair, new_pair in zip(decoded[slot], relocated):
            for old, new in zip(old_pair, new_pair):
                require(old[:3] == new[:3] and old[5:] == new[5:] and allocator.rectangle(source.vram, *old[3:]) ==
                        allocator.rectangle(bytes(output[:0x80000]), *new[3:]), "Every complete descriptor pixel/palette owner survives re-encoding")
        records.append(bytes(result))
    require(all(output[i] == before[i] for i in range(len(output)) if i >= 0x80000 or not allocated[i]),
            "Complete unallocated/protected-zero/protected-nonzero/SPU complement")
    expected = bytes(output)

    def restore(after):
        require(after == expected, "Complete upload inverse afterimage mismatch")
        restored = bytearray(after)
        for _, _, _, _, target, width, rows, _ in regions:
            for row in range(rows):
                at = target + row * 1024
                restored[at:at + width] = before[at:at + width]
        require(restored == before, "Complete upload inverse preimage mismatch")
        return bytes(restored)

    require(restore(expected) == before, "Complete independently captured GPU allocation inverse")
    probes = (regions[0][4], next(i for i, tag in enumerate(ownership) if tag and before[i] == 0),
              next(i for i, tag in enumerate(ownership) if tag and before[i] != 0), 0x80000, len(output) - 1)
    for at in probes:
        corrupt = bytearray(output)
        corrupt[at] ^= 1
        try:
            restore(bytes(corrupt))
        except AssertionError as error:
            require("inverse afterimage" in str(error), "Exact upload inverse corruption refusal")
        else:
            raise AssertionError("Corrupt allocated/protected/SPU afterimage accepted")
    alias_rows = tuple((child, parent, targets[child], targets[parent]) for child, parent in sorted(aliases.items()))
    return bytes(output), tuple(records), tuple(regions), alias_rows, allocated.count(1), occupied.count(0)


def compose(before, records, faces, geometry, tail):
    geometry.canonical(before, tail)
    count, growth = len(records), 184 * len(records)
    require(count in (1, 2, 3) and all(type(r) is bytes and len(r) == 184 for r in records) and
            len(faces) == 6 and all(face == 255 or 0 <= face < count for face in faces), "Closed N1/N2-control/N3 fixture grammar")
    require(tuple(dict.fromkeys(face for face in faces if face != 255)) == tuple(range(count)),
            "Every distinct source is referenced in deterministic first-occurrence slot order")
    metadata = struct.pack("<II", 8 + growth, count) + b"".join(r[:16] for r in records) + b"".join(r[16:] for r in records)
    output = bytearray(metadata + before[8:])
    require(len(output) == 0xA58 + growth <= 0x1000 and len(metadata) == 8 + growth, "Actual used data fits its fixed allocation")
    offsets = []
    for tile in range(3):
        sector_at = 12 + word(before, 16 + 4 * tile)
        require(sector_at == 28 + 160 * tile and before[sector_at + 16:sector_at + 24] == bytes((4, 4, 2, 0, 4, 4, 2, 12)),
                "Canonical directory/sector owner and complete native table layout")
        face_start = sector_at + 28 + 4 * before[sector_at + 23] + 4 * before[sector_at + 20] + 8 * before[sector_at + 21]
        for triangle in range(2):
            at = face_start + 16 * triangle + 8
            require(word(before, at) == 255 and word(before, at + 4) == 128, "Original full FF/80 material owner")
            struct.pack_into("<I", output, at + growth, faces[2 * tile + triangle])
            offsets.append(at)
    require(offsets == [0xA0, 0xB0, 0x140, 0x150, 0x1E0, 0x1F0], "All six independently derived high-detail face owners")
    changed = {at + byte for at in offsets for byte in range(4)}
    require(all(before[at] == output[at + growth] for at in range(8, len(before)) if at not in changed),
            "Whole translated geometry/visibility/collision/sky/portal/particle/sound complement")
    require(all(word(output, at + growth + 4) == 128 for at in offsets), "Direct-LQ flag80 preserved even on Base faces")
    for slot, record in enumerate(records):
        require(output[8 + 16 * slot:24 + 16 * slot] + output[8 + 16 * count + 168 * slot:8 + 16 * count + 168 * (slot + 1)] == record,
                "Complete ordered184-byte record reconstructed from separated native tables")
    restored = bytearray(struct.pack("<II", 8, 0) + output[8 + growth:])
    for at in offsets:
        struct.pack_into("<I", restored, at, 255)
    require(restored == before, "Complete independent metadata/material inverse")
    return bytes(output)


def capture(path):
    require(__debug__, "Preserved assertion controls must execute")
    require_helpers()
    sys.path.insert(0, str(ROOT / "src/Spyro.Editor.NativeId65FreshTerrainTextureSmoke"))
    allocator = load(list(HELPERS)[0], "selected_raw_allocator")
    fresh_module = load(list(HELPERS)[1], "selected_fresh_upload")
    old = load(list(HELPERS)[2], "selected_n2_source_control")
    geometry = load(list(HELPERS)[3], "selected_tiled_geometry")
    buffer = io.StringIO()
    with contextlib.redirect_stdout(buffer):
        old.main(path)
    control = json.loads(buffer.getvalue())
    require(control["marker"] == "TILED_MATERIAL_EXACT_SOURCE_ORACLE_OK" and control["completeDataInverse"] and
            len(control["cases"]) == 3, "Preserved complete N2 source/data references and inverse actually execute")
    with contextlib.redirect_stdout(io.StringIO()):
        fresh = fresh_module.capture(path)
    with open(path, "rb") as source:
        index = source_index(source)
        sources = capture_sources(source, index, allocator)
        town = logical(source, index, 37, 0x119E000, 0x2E2000)
        require(sha(town) == "7ddbf6d9a6ee6c0f64c8564a89e374ca0ca234ef608ec812aae68e8176de1dc0", "Whole retained Town tail owner")
        tail = bytes(4) + town[0x172590:0x172D08]
        before = struct.pack("<II", 8, 0) + geometry.fresh() + tail
        geometry.canonical(before, tail)
        source_by_id = {(s.entry, s.texture): s for s in sources}
        # The generalized independent packing itself must reproduce N2, not
        # merely run a disconnected old control next to a different algorithm.
        n2 = tuple(source_by_id[key] for key in ((14, 0), (70, 0)))
        upload, records, _, _, allocated, _ = pack(fresh.upload, fresh.gpu_ownership, n2, allocator)
        require(sha(upload) == control["cases"][0]["uploadSha256"] and allocated == 28160 and
                [sha(r) for r in records] == control["encodedRecordSha256"], "New independent generalized packing exactly reproduces old N2 owners")
        for case in control["cases"]:
            require(sha(compose(before, records, tuple(case["orderedFaceIds"]), geometry, tail)) == case["dataSha256"],
                    "Generalized independent split-table writer reproduces every frozen N2 data reference")
        cases = []
        for name, selectors, faces in CASE_SPECS:
            selected = tuple(source_by_id[key] for key in selectors)
            upload, records, regions, aliases, allocated, free = pack(fresh.upload, fresh.gpu_ownership, selected, allocator)
            data = compose(before, records, faces, geometry, tail)
            cases.append(Fixture(name, selected, faces, upload, data, records, regions, aliases, allocated, free))
        require_original(source)
    with open(path, "rb") as final_named_source:
        require_original(final_named_source)
    require_helpers()
    return Captured(bytes(fresh.upload), bytes(fresh.gpu_ownership), bytes(before), bytes(tail), tuple(cases), json.dumps(control))


def summary(captured):
    cases = []
    for case in captured.cases:
        cases.append(dict(name=case.name, selections=[dict(wad=s.entry, texture=s.texture, handle=s.handle,
            sourceDataSha256=s.data_sha256, sourceSceneSha256=s.scene_sha256, sourceRecordSha256=sha(s.record)) for s in case.sources],
            orderedFaceIds=case.faces, relocatedRecordSha256=[sha(r) for r in case.records], uploadSha256=sha(case.upload),
            dataSha256=sha(case.data), usedBytes=len(case.data), nativeClip=0x8000,
            additionalPacketBytesPerPass=12 * sum(face != 255 for face in case.faces),
            allocatedBytes=case.allocated_bytes, freeBytes=case.free_bytes, regions=case.regions, containedAliases=case.aliases))
    return dict(marker="TILED_SELECTED_MATERIAL_EXACT_SOURCE_ORACLE_OK", originalSha256=ORIGINAL, helperSha256=HELPERS,
                beforeUploadSha256=sha(captured.before_upload), beforeDataSha256=sha(captured.before_data), tailSha256=sha(captured.tail),
                cases=cases, protectedZeroBytes=sum(b == 0 and tag != 0 for b, tag in zip(captured.before_upload, captured.ownership)),
                immutableSourceSnapshots=True, authenticatedSamePassPreimages=True, preservedN2ControlsExecuted=True,
                completeUploadComplement=True, completeUploadInverse=True, completeDataComplement=True, completeDataInverse=True,
                referencesAwaitIndependentFreeze=True, coreExecuted=False, filesWritten=False, bodyIssued=False,
                runtimeVerified=False, publicationApproved=False)


def main(path):
    print(json.dumps(summary(capture(path)), indent=2))


if __name__ == "__main__":
    if len(sys.argv) != 2:
        raise SystemExit("Usage: source_tiled_selected_material_oracle.py original.bin")
    main(sys.argv[1])
