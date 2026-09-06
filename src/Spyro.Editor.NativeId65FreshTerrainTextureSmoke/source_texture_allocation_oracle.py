"""Raw-sector two-material reencoding oracle. No editor DLL or payload/ROM writes.

Keeps complete184-byte records/all46 descriptors and immutable source upload
owners. Resource byte equality and the fixed LQ clip proof are NOT runtime,
HQ, peak-GPU-budget, body/staging or publication acceptance.
"""
import contextlib
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import struct
import sys

sys.dont_write_bytecode = True
from source_fresh_upload_fixture import capture, sha

ORIGINAL = "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37"
ROOT = Path(__file__).resolve().parents[2]


def rectangle(data, x, y, width, rows):
    assert len(data) == 0x80000 and 0 <= x <= 1024-width and 0 <= y <= 512-rows
    return b"".join(data[(y+i)*1024+x:(y+i)*1024+x+width] for i in range(rows))


def decode(record, material):
    assert len(record) == 184 and record[:8] == record[8:16] == record[16:24]
    result = []
    for i in range(23):
        b = record[i*8:i*8+8]; low = i < 3
        side = 32 if low else b[4]-b[0]+1
        assert b[5] == b[1] and b[4] >= b[0] and b[1]+side <= 256
        if low:
            assert b[0] % 2 == 0 and b[4]-b[0] == 31 and b[7] == 128 and b[6] & 0x88 == 8
            x, px, py, pw, ph = (b[6]&7)*128+b[0]//2, b[2]*32-1024, b[3]*4, 32, 16
        else:
            assert b[6]&0x88 == 0x88 and b[7]&127 == 0 and side in ((32,) if i < 7 else (16, 32))
            clut = int.from_bytes(b[2:4], "little"); assert clut < 0x8000
            x, px, py, pw, ph = (b[6]&15)*128+b[0]-1024, ((clut&63)*16-512)*2, clut>>6, 512, 1
        y = (256 if b[6]&16 else 0)+b[1]
        pair = []
        for kind, rx, ry, w, h in (("pixel", x, y, side//2 if low else side, side), ("palette", px, py, pw, ph)):
            assert 0 <= rx <= 1024-w and 0 <= ry <= 512-h
            pair.append((material, kind, 4 if low else 8, rx, ry, w, h))
        result.append(tuple(pair))
    return result


def contains(parent, child):
    return parent[:3] == child[:3] and parent[3] <= child[3] and parent[4] <= child[4] and \
        parent[3]+parent[5] >= child[3]+child[5] and parent[4]+parent[6] >= child[4]+child[6]


def main(path):
    fresh = capture(path)
    surface_path = ROOT / "src/Spyro.Editor.NativeId65AuthoredSurfaceSmoke/source_surface_oracle.py"
    oldargv = sys.argv; sys.argv = [str(surface_path)]
    try:
        with contextlib.redirect_stdout(io.StringIO()):
            spec = importlib.util.spec_from_file_location("surface_raw", surface_path)
            surface = importlib.util.module_from_spec(spec); spec.loader.exec_module(surface)
    finally:
        sys.argv = oldargv
    records, domains, handles = [], [], []
    with open(path, "rb") as image:
        def original():
            assert image.seek(0, 2) == 661547040; image.seek(0)
            assert hashlib.file_digest(image, "sha256").hexdigest() == ORIGINAL
            assert image.tell() == 661547040 and image.seek(0, 2) == 661547040

        def read(lba, offset, length):
            out = bytearray()
            while length:
                amount = min(length, 2048-offset%2048)
                image.seek((lba+offset//2048)*2352+24+offset%2048)
                piece = image.read(amount); assert len(piece) == amount
                out += piece; offset += amount; length -= amount
            return bytes(out)

        original(); table = read(37, 0, 0x800)
        exe = read(53875, 0, 0x66000)
        assert sha(exe) == "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998"
        assert exe[0x34B4:0x34C8].hex() == "008002340780013c605720ac0880013ccc8522ac"
        # Actual native clip initialization, rather than a caller's claimed value.
        assert sha(exe[0x34B4:0x34C8]) == "126c3d68a117fc390d8ff0fb09e492f75c07409a49c0f1a409eca2f2346063a4"
        for entry, datahash, recordhash in (
            (14, "a937aecb3626ffd2311ba136684a34c9f36877054b52557b4524fcacee0e2d97", "7b074611db43208653a931fb12c98ee64dbc5f8f41ef4edd7b1e5e796a4e0954"),
            (70, "b2a25147c4d81e7fd366a830546b92e6a482fe9a56f8a429a30fcb4857091a02", "2ffdf4bc69645c31a1cc75862288bf3620955567ef65e35b2f8f65862b7dda72")):
            off, length = struct.unpack_from("<II", table, entry*8); data = read(37, off, length)
            assert sha(data) == datahash
            page, page_length, metadata = struct.unpack_from("<III", data)
            length, count = struct.unpack_from("<II", data, metadata)
            assert page_length >= 0x80000 and length == 8+count*184
            record = data[metadata+8:metadata+24]+data[metadata+8+count*16:metadata+8+count*16+168]
            assert sha(record) == recordhash
            records.append(record); domains.append(data[page:page+0x80000])
            handles.append(f"native-static-terrain-record-canonical-orientation0-clean-usa-v1:{ORIGINAL}:wad-{entry}:data-{datahash}:t-0:record-{recordhash}")
        town = read(37, 0x119E000, 0x2E2000)
        assert sha(town) == "7ddbf6d9a6ee6c0f64c8564a89e374ca0ca234ef608ec812aae68e8176de1dc0"
        data_before = struct.pack("<II", 8, 0)+surface.fresh(8448, 8448, 8704, 1024, 1024, 0x708050, 0x406080)+bytes(4)+town[0x172590:0x172D08]
        assert len(data_before) == 0x8C8
        original()

    decoded = [decode(record, i) for i, record in enumerate(records)]
    distinct = set(q for entries in decoded for pair in entries for q in pair)
    requests, aliases = [], {}
    for q in sorted(distinct, key=lambda q: (-q[5]*q[6], q)):
        parent = next((p for p in requests if contains(p, q)), None)
        if parent is None: requests.append(q)
        else: aliases[q] = parent
    assert len(requests) == 31 and len(aliases) == 16
    output = bytearray(fresh.upload); occupied = bytearray(fresh.gpu_ownership); allocated = bytearray(0x80000)
    placements = {}
    for q in sorted(requests, key=lambda q: (q[1] != "palette", -q[5]*q[6], q[0], q[4], q[3])):
        material, kind, bpp, sx, sy, w, h = q
        point = None
        for y in range(0, 513-h, 4 if kind == "palette" and bpp == 4 else 1):
            if kind == "pixel" and y%256+h > 256: continue
            for x in range(0, 1025-w, 32 if kind == "palette" else 16):
                if all(not any(occupied[(y+j)*1024+x:(y+j)*1024+x+w]) for j in range(h)):
                    point = (x, y); break
            if point is not None: break
        assert point is not None
        x, y = placements[q] = point
        payload = rectangle(domains[material], sx, sy, w, h)
        for j in range(h):
            at = (y+j)*1024+x
            assert not any(allocated[at:at+w]) and fresh.gpu_ownership[at:at+w] == bytes(w)
            output[at:at+w] = payload[j*w:(j+1)*w]; occupied[at:at+w] = bytes([3])*w; allocated[at:at+w] = bytes([1])*w
    for child, parent in aliases.items():
        px, py = placements[parent]
        placements[child] = px+child[3]-parent[3], py+child[4]-parent[4]

    rebased = []
    for material, original_record in enumerate(records):
        result = bytearray(original_record)
        for i, pair in enumerate(decoded[material]):
            pixel, palette = pair; x, y = placements[pixel]; px, py = placements[palette]
            at = i*8; low = i < 3
            u = x%128*(2 if low else 1)
            result[at] = u; result[at+1] = y%256; result[at+4] = u+pixel[6]-1; result[at+5] = y%256
            result[at+6] = (result[at+6]&0xE0) | (8 if low else 0x88) | (x//128) | (16 if y >= 256 else 0)
            if low: result[at+2] = (px+1024)//32; result[at+3] = py//4
            else: struct.pack_into("<H", result, at+2, py*64+(px+1024)//32)
        after = decode(bytes(result), material)
        for old_pair, new_pair in zip(decoded[material], after):
            for old, new in zip(old_pair, new_pair):
                assert old[:3] == new[:3] and old[5:] == new[5:]
                assert rectangle(domains[material], *old[3:]) == rectangle(bytes(output[:0x80000]), *new[3:])
        rebased.append(bytes(result))

    data_after = bytearray(struct.pack("<II", 0x178, 2)+b"".join(r[:16] for r in rebased)+b"".join(r[16:] for r in rebased)+data_before[8:])
    for before, after, value in ((0x98, 0x208, 0), (0xA8, 0x218, 1)):
        assert struct.unpack_from("<II", data_before, before) == (255, 128)
        struct.pack_into("<I", data_after, after, value)
    assert len(data_after) == 0xA38 and allocated.count(1) == 28160
    assert sha(output) == "2ab548f540fbfadddc38d96e70a9633a1c6f37c37ee2d1a896919ac30f929333"
    assert all(a == b for i, (a, b) in enumerate(zip(fresh.upload, output)) if i >= 0x80000 or not allocated[i])
    excluded = set(range(0x98, 0x9C)) | set(range(0xA8, 0xAC))
    assert all(data_before[i] == data_after[i+0x170] for i in range(8, len(data_before)) if i not in excluded)
    result = dict(marker="NATIVE_FRESH_TERRAIN_TEXTURE_RAW_ORACLE_OK", originalSha256=ORIGINAL,
        sourceHandles=handles, oldUploadSha256=sha(fresh.upload), uploadSha256=sha(output),
        oldDataSha256=sha(data_before), dataSha256=sha(data_after), encodedRecordHex=[r.hex() for r in rebased],
        encodedRecordSha256=[sha(r) for r in rebased], descriptorCount=46, allocations=len(requests),
        containedAliases=len(aliases), allocatedBytes=allocated.count(1), freeAfter=occupied.count(0),
        protectedZeroBytes=sum(b == 0 and own != 0 for b, own in zip(fresh.upload, fresh.gpu_ownership)),
        dataLength=len(data_after), usedDataGrowth=0x170, nativeClip=0x8000, completeUploadComplement=True,
        completeTranslatedDataComplement=True, sceneModelAndSpuChanged=False, hqRuntime=False,
        runtimeResourceAcceptance=False, publicationApproved=False)
    with open(path, "rb") as final_source:
        assert final_source.seek(0, 2) == 661547040; final_source.seek(0)
        assert hashlib.file_digest(final_source, "sha256").hexdigest() == ORIGINAL
        assert final_source.tell() == 661547040 and final_source.seek(0, 2) == 661547040
    assert result == json.loads(Path(__file__).with_name("source_texture_allocation_golden.json").read_text())
    print(json.dumps(result, indent=2))
    return result


if __name__ == "__main__":
    if len(sys.argv) != 2: raise SystemExit("Usage: source_texture_allocation_oracle.py original.bin")
    main(sys.argv[1])
