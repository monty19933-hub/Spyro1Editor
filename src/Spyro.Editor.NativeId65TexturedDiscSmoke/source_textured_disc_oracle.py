"""Original-only textured raw afterimages. No C# producer, BIN or payload writer.

Pinned independent body/upload helpers reopen and hash the exact original; they
are NOT represented as same-pass reads. Every new donor/metadata/physical read
uses this oracle's same-pass physical index. All held owners are checked finally.
The geometry and identity-transform/GPU execution claims do not grow here.
"""
import contextlib
import hashlib
import importlib.util
import io
import json
import os
from pathlib import Path
import re
import stat
import struct
import sys

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[2]
SIZE, SECTORS = 661547040, 281270
ORIGINAL = "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37"
PINS = {
    "Spyro.Editor.NativeId65FreshDiscSmoke/source_fresh_disc_oracle.py": "f93780c582df180f378a4121a824d01dec6f13e3c5da68a61af571772e732608",
    "Spyro.Editor.NativeId65FreshDiscSmoke/source_fresh_disc_body.py": "e69619f391814c270c9c28bc588f432f79f8a6cd3f61301e3a1c483a8645ad07",
    "Spyro.Editor.NativeId65FreshTerrainTextureSmoke/source_texture_allocation_oracle.py": "c4ee51758031edb3d2b9cab9279985a537d0c06d1251cd24ab090dec67b07d6f",
    "Spyro.Editor.NativeId65FreshTerrainTextureSmoke/source_texture_allocation_golden.json": "c6c16f0bac8fd1816676404f50c99ab422551ddc60da6c2ca2555b0d149e779f",
    "Spyro.Editor.NativeId65FreshTerrainTextureSmoke/source_fresh_upload_fixture.py": "727ea1028d36db37c70e25a112836c4740b967a667aad64c78f6a900786b6cfe",
    "Spyro.Editor.NativeId65NormalResourceSmoke/source_resource_oracle.py": "280087ec3b80e005872b6ec2fe793c9bbbbd69bde70ea6c64de234afa37ad76d",
    "Spyro.Editor.NativeId65AuthoredSurfaceSmoke/source_surface_oracle.py": "bcaa7349277bc5150cd17cf3e8cebbc689b68a58e9dee21a2709841c899aa592",
    "Spyro.Editor.NativeId65NormalResourceSmoke/source_fresh_scene_oracle.py": "d4946487dc45cb353d49d5fc18ab1894ad6b47849b8348b3e14857c7e9f8a8a6",
    "Spyro.Editor.NativeId65NormalBodySmoke/NativeBodySourceWindows.cs": "674ac959cd1b451fc11c3945c1446059a2d09b505e03ddda27e6fbe3851594ec",
    "Spyro.Editor.Core/Music/MusicTrackCatalog.cs": "7854f69c04bf16d3bab5d809baa46e058c294ef6eedc046190f2123c03f02ef3",
}
sha = lambda b: hashlib.sha256(b).hexdigest()
u32 = lambda b, at: struct.unpack_from("<I", b, at)[0]


def identity(f, length, digest):
    before = os.fstat(f.fileno())
    path = os.stat(f.name, follow_symlinks=False)
    assert stat.S_ISREG(before.st_mode) and stat.S_ISREG(path.st_mode)
    assert (path.st_dev, path.st_ino, path.st_size) == (before.st_dev, before.st_ino, length)
    f.seek(0)
    assert hashlib.file_digest(f, "sha256").hexdigest() == digest
    assert f.tell() == length and f.read(1) == b"" and f.tell() == length
    after = os.fstat(f.fileno())
    assert (before.st_dev, before.st_ino, before.st_size, before.st_mtime_ns) == \
           (after.st_dev, after.st_ino, after.st_size, after.st_mtime_ns)


def module(folder, name):
    path = ROOT / "src" / folder / (name + ".py")
    sys.path.insert(0, str(path.parent))
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec)
    sys.modules[name] = result
    previous = sys.argv
    try:
        sys.argv = [str(path)]
        with contextlib.redirect_stdout(io.StringIO()):
            spec.loader.exec_module(result)
    finally:
        sys.argv = previous
    return result


def allocate(texture, fresh, records, domains):
    """Independent generalized source-coordinate allocator; no caller capability."""
    decoded = [texture.decode(record, i) for i, record in enumerate(records)]
    distinct = set(q for entries in decoded for pair in entries for q in pair)
    requests, aliases = [], {}
    for q in sorted(distinct, key=lambda q: (-q[5]*q[6], q)):
        for p in requests:
            if p[:3] == q[:3] and p[3] < q[3]+q[5] and q[3] < p[3]+p[5] and \
                    p[4] < q[4]+q[6] and q[4] < p[4]+p[6]:
                assert texture.contains(p, q) or texture.contains(q, p)
        parent = next((p for p in requests if texture.contains(p, q)), None)
        if parent is None: requests.append(q)
        else: aliases[q] = parent
    output, occupied = bytearray(fresh.upload), bytearray(fresh.gpu_ownership)
    changed, targets = bytearray(0x80000), {}
    for q in sorted(requests, key=lambda q: (q[1] != "palette", -q[5]*q[6], q[0], q[4], q[3])):
        material, kind, bpp, sx, sy, w, h = q
        target = None
        for y in range(0, 513-h, 4 if kind == "palette" and bpp == 4 else 1):
            if kind == "pixel" and y % 256+h > 256: continue
            for x in range(0, 1025-w, 32 if kind == "palette" else 16):
                if all(not any(occupied[(y+j)*1024+x:(y+j)*1024+x+w]) for j in range(h)):
                    target = (x, y); break
            if target is not None: break
        assert target is not None, "Complete texture rectangle does not fit"
        targets[q] = x, y
        payload = texture.rectangle(domains[material], sx, sy, w, h)
        for j in range(h):
            at = (y+j)*1024+x
            assert fresh.gpu_ownership[at:at+w] == bytes(w)
            output[at:at+w] = payload[j*w:(j+1)*w]
            occupied[at:at+w], changed[at:at+w] = bytes([3])*w, bytes([1])*w
    for child, parent in aliases.items():
        px, py = targets[parent]
        targets[child] = px+child[3]-parent[3], py+child[4]-parent[4]
    encoded = []
    for material, record in enumerate(records):
        result = bytearray(record)
        for i, (pixel, palette) in enumerate(decoded[material]):
            x, y = targets[pixel]; px, py = targets[palette]; at = i*8; low = i < 3
            u = x % 128 * (2 if low else 1)
            result[at:at+2] = bytes([u, y % 256])
            result[at+4:at+6] = bytes([u+pixel[6]-1, y % 256])
            result[at+6] = (result[at+6] & 0xE0) | (8 if low else 0x88) | x//128 | (16 if y >= 256 else 0)
            if low: result[at+2:at+4] = bytes([(px+1024)//32, py//4])
            else: struct.pack_into("<H", result, at+2, py*64+(px+1024)//32)
        after = texture.decode(result, material)
        for old_pair, new_pair in zip(decoded[material], after):
            for old, new in zip(old_pair, new_pair):
                assert old[:3] == new[:3] and old[5:] == new[5:]
                assert texture.rectangle(domains[material], *old[3:]) == texture.rectangle(output[:0x80000], *new[3:])
        encoded.append(bytes(result))
    assert all(a == b for i, (a, b) in enumerate(zip(fresh.upload, output)) if i >= 0x80000 or not changed[i])
    return bytes(output), encoded


def run(image, helpers):
    physical = module("Spyro.Editor.NativeId65FreshDiscSmoke", "source_fresh_disc_oracle")
    texture = module("Spyro.Editor.NativeId65FreshTerrainTextureSmoke", "source_texture_allocation_oracle")
    with contextlib.redirect_stdout(io.StringIO()):
        bodies = physical.build_bodies(image.name)
        fresh = texture.capture_fresh(image.name)
        fixed = texture.capture(image.name)
    index, digest = bytearray(SECTORS*32), hashlib.sha256()
    image.seek(0)
    for lba in range(SECTORS):
        raw = image.read(2352); assert len(raw) == 2352
        digest.update(raw); index[lba*32:(lba+1)*32] = hashlib.sha256(raw).digest()
    assert digest.hexdigest() == ORIGINAL and image.read(1) == b"" and image.tell() == SIZE

    def raw_sector(lba):
        assert 0 <= lba < SECTORS
        image.seek(lba*2352); raw = image.read(2352)
        assert len(raw) == 2352 and hashlib.sha256(raw).digest() == index[lba*32:(lba+1)*32]
        return raw

    def read(lba, offset, length):
        assert offset >= 0 and length >= 0
        result = bytearray()
        while length:
            amount = min(length, 2048-offset % 2048)
            result += raw_sector(lba+offset//2048)[24+offset % 2048:24+offset % 2048+amount]
            offset += amount; length -= amount
        return bytes(result)

    pvd, root, header, exe = read(16, 0, 2048), read(22, 0, 2048), read(37, 0, 2048), read(53875, 0, 0x66000)
    assert pvd[:7] == b"\x01CD001\x01" and u32(pvd, 158) == int.from_bytes(pvd[162:166], "big") == 22
    assert u32(pvd, 166) == int.from_bytes(pvd[170:174], "big") == 2048
    rows, cursor = {}, 0
    while cursor < len(root) and root[cursor]:
        size = root[cursor]; assert size >= 34 and cursor+size <= 2048
        row = root[cursor:cursor+size]
        name = row[33:33+row[32]].decode("ascii").removesuffix(";1")
        assert name not in rows and u32(row, 2) == int.from_bytes(row[6:10], "big")
        assert u32(row, 10) == int.from_bytes(row[14:18], "big")
        rows[name] = cursor, u32(row, 2), u32(row, 10); cursor += size
    assert rows["WAD.WAD"][1:] == (37, 0x6927000) and rows["SCUS_942.28"][1:] == (53875, 0x66000)
    assert rows["PETEXA0.STR"][1] == 60000 and header[632:648] == bytes(16)
    assert header[648:656] == bytes.fromhex("0078c90500200000")
    gap = hashlib.sha256()
    for lba in range(54079, 55125):
        raw = raw_sector(lba); bcd = lambda n: n//10*16+n % 10
        assert raw[:12] == bytes.fromhex("00ffffffffffffffffffff00")
        assert raw[12:16] == bytes([bcd((lba+150)//4500), bcd((lba+150)//75 % 60), bcd((lba+150) % 75), 2])
        assert raw[16:24] == bytes.fromhex("0000200000002000") and raw[24:2348] == bytes(2324)
        assert u32(raw, 2348) == physical.edc(raw[16:2348])
        assert all(not start <= lba < start+(length+2047)//2048 for _, start, length in rows.values())
        gap.update(raw)
    assert gap.hexdigest() == "38cb2f93a135ad22d794799a6bbb0fff3d8a1dbabeab00448d94cbe638a3d014"
    for lba in (22, 37, 53874, 53875, 54078):
        raw = raw_sector(lba)
        assert physical.form1(raw, raw[24:2072], raw[18] & 0x81) == raw
    music = helpers["Spyro.Editor.Core/Music/MusicTrackCatalog.cs"]
    music.seek(0); code = music.read().decode()
    names = re.search(r"TrackNames\s*=\s*\[(.*?)\];", code, re.S).group(1)
    excluded = re.search(r"trackId is not (\d+) and not (\d+)", code).groups()
    tracks = [i for i in range(len(re.findall(r'"[^"\r\n]*"', names))) if i not in map(int, excluded)]
    executables = {track: physical.compose_exe(exe, track) for track in tracks}
    donors = {}
    for entry, expected in ((14, "a937aecb3626ffd2311ba136684a34c9f36877054b52557b4524fcacee0e2d97"),
                            (70, "b2a25147c4d81e7fd366a830546b92e6a482fe9a56f8a429a30fcb4857091a02")):
        off, length = struct.unpack_from("<II", header, entry*8); level = read(37, off, length)
        assert sha(level) == expected
        page, metadata = u32(level, 0), u32(level, 8); count = u32(level, metadata+4)
        assert u32(level, metadata) == 8+count*184 and 2 <= count <= 128
        for tid in (0, 1):
            record = level[metadata+8+tid*16:metadata+24+tid*16] + level[metadata+8+count*16+tid*168:metadata+8+count*16+(tid+1)*168]
            donors[entry, tid] = record, level[page:page+0x80000]
    cases = [("fixed-a", 0, ((14, 0), (70, 0))), ("fixed-b", 1, ((14, 0), (70, 0))),
             ("fixed-short", 2, ((14, 0), (70, 0))), ("selected", 0, ((14, 1), (70, 1))),
             ("reversed", 0, ((70, 1), (14, 1))), ("same-source", 0, ((14, 1), (14, 1)))]
    results = []
    for name, body_index, selection in cases:
        records, domains = zip(*(donors[key] for key in selection))
        upload, encoded = allocate(texture, fresh, records, domains)
        old_body, overlay = bodies[body_index]
        old_data = old_body[0xDE800:0xDF0C8]
        data = bytearray(struct.pack("<II", 0x178, 2)+b"".join(r[:16] for r in encoded)+b"".join(r[16:] for r in encoded)+old_data[8:])
        assert len(data) == 0xA38
        for at, value in ((0x208, 0), (0x218, 1)): struct.pack_into("<I", data, at, value)
        if name.startswith("fixed"):
            assert upload == fixed.upload and data == fixed.data and tuple(encoded) == fixed.records
        body = bytearray(old_body); body[0x800:0xDE800] = upload; body[0xDE800:0xDF800] = data+bytes(0x1000-len(data))
        assert body[:0x800] == old_body[:0x800] and body[0xDF800:] == old_body[0xDF800:]
        append = overlay+body; final_exe = 53875+len(append)//2048; end = final_exe+204
        assert len(body) in (0x1FB000, 0x1FB800) and end <= 55125
        new_root, new_header = bytearray(root), bytearray(header)
        for owner, lba, length in (("WAD.WAD", 37, 0x6927000+len(append)), ("SCUS_942.28", final_exe, 0x66000)):
            at = rows[owner][0]
            for off, value in ((2, lba), (10, length)):
                struct.pack_into("<I", new_root, at+off, value); struct.pack_into(">I", new_root, at+off+4, value)
        struct.pack_into("<IIII", new_header, 632, 0x6927000, len(overlay), 0x6936800, len(body))

        def after(lba, raw):
            payload = None
            if lba == 22: payload = new_root
            elif lba == 37: payload = new_header
            elif 53875 <= lba < final_exe: payload = append[(lba-53875)*2048:(lba-53874)*2048]
            elif final_exe <= lba < end: payload = executables[19][(lba-final_exe)*2048:(lba-final_exe+1)*2048]
            elif lba == 53874: payload = raw[24:2072]
            if payload is None: return raw
            flags = raw[18] & 0x81 if lba == 22 else (0x81 if lba in (final_exe-1, end-1) else 0)
            return physical.form1(raw, payload, flags)

        def inverse(lba, actual):
            original = raw_sector(lba)
            assert actual == after(lba, original), "Inverse must authenticate the complete supplied afterimage"
            return original

        forward, restored, changed = hashlib.sha256(), hashlib.sha256(), 0
        for lba in range(SECTORS):
            before = raw_sector(lba); final = after(lba, before)
            forward.update(final); restored.update(inverse(lba, final)); changed += final != before
        assert restored.hexdigest() == ORIGINAL and changed == end-53874+2
        for lba in (0, 22, 37, 53875, final_exe, end-1, end):
            bad = bytearray(after(lba, raw_sector(lba))); bad[24] ^= 1
            try: inverse(lba, bad)
            except AssertionError: pass
            else: raise AssertionError("Changed/unchanged inverse mutation was accepted")
        results.append(dict(name=name, selections=[list(k) for k in selection], upload_sha256=sha(upload), data_sha256=sha(data),
                            record_sha256=list(map(sha, encoded)), body_sha256=sha(body), output_sha256=forward.hexdigest(),
                            restored_original_sha256=restored.hexdigest(), changed_sectors=changed,
                            final_executable_lba=final_exe, final_end=end, root_sha256=sha(new_root), wad_sha256=sha(new_header)))
    return dict(marker="NATIVE_ID65_TEXTURED_DISC_RAW_ORACLE_OK", outputs=results,
                executable_tracks={str(k): sha(v) for k, v in executables.items()},
                source_same_pass_index_for_new_reads=True, payload_helpers_reopen_pinned_original=True,
                inverse_executed=True, physical_output_written=False, hq_runtime_verified=False,
                runtime_verified=False, publication_approved=False)


def main(path):
    owners, failures, result = [], [], None
    try:
        image = open(path, "rb"); owners.append((image, SIZE, ORIGINAL)); identity(*owners[-1])
        helpers = {}
        for relative, digest in PINS.items():
            f = open(ROOT / "src" / relative, "rb"); item = f, os.fstat(f.fileno()).st_size, digest
            owners.append(item); identity(*item); helpers[relative] = f
        golden = open(Path(__file__).with_name("source_textured_disc_golden.json"), "rb")
        owners.append((golden, 10461, "0b45e18a610ce098d02d48ecbf6a7845ad468bcb7da1066fd0bb96fc65a0528a"))
        identity(*owners[-1]); golden.seek(0); expected = json.load(golden)
        result = run(image, helpers)
    except BaseException as error:
        failures.append(error)
    finally:
        for item in owners:
            try: identity(*item)
            except BaseException as error: failures.append(error)
        for f, _, _ in owners:
            try: f.close()
            except BaseException as error: failures.append(error)
    if failures: raise BaseExceptionGroup("Original/helper capture or finalization failed", failures)
    result["all_final_owners_verified"] = True
    assert result == expected, "Independent repeat disagrees with retained preproduction first run"
    print(json.dumps(result, indent=2))


if __name__ == "__main__":
    if len(sys.argv) != 2 or not sys.argv[1].strip() or sys.argv[1].startswith("--"):
        raise SystemExit("Usage: source_textured_disc_oracle.py original.bin; stdout metadata only")
    main(sys.argv[1])
