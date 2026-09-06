"""Independent complete raw afterimage; original-only, no DLL or candidate writes.

Every later metadata/physical read authenticates its same-pass source-LBA digest.
Parity is derived here and first required to reproduce actual original sectors.
"""
import hashlib
import json
import struct
import sys

sys.dont_write_bytecode = True
from source_fresh_disc_body import build_bodies

SIZE, SECTORS = 661547040, 281270
SOURCE_SHA = "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37"
sha = lambda b: hashlib.sha256(b).hexdigest()
u32 = lambda b, o: struct.unpack_from("<I", b, o)[0]
EDC, FORWARD, BACKWARD = [], [0] * 256, [0] * 256
for n in range(256):
    value = n
    for _ in range(8):
        value = (value >> 1) ^ (0xD8018001 if value & 1 else 0)
    EDC.append(value)
    doubled = (n << 1) ^ (0x11D if n & 0x80 else 0)
    FORWARD[n], BACKWARD[n ^ doubled] = doubled, n


def edc(data):
    value = 0
    for byte in data:
        value = (value >> 8) ^ EDC[(value ^ byte) & 255]
    return value


def ecc(data, major_count, minor_count, major_mult, minor_inc):
    size = major_count * minor_count
    assert len(data) == size
    result = bytearray(2 * major_count)
    for major in range(major_count):
        index, a, b = (major >> 1) * major_mult + (major & 1), 0, 0
        for _ in range(minor_count):
            value = data[index]
            index = (index + minor_inc) % size
            a ^= value
            b ^= value
            a = FORWARD[a]
        a = BACKWARD[FORWARD[a] ^ b]
        result[major], result[major + major_count] = a, a ^ b
    return result


def form1(before, payload, flags):
    assert len(before) == 2352 and len(payload) == 2048 and flags & ~0x81 == 0
    assert before[16:20] == before[20:24] and before[16] == before[17] == before[19] == 0
    assert before[18] in (8, 0x89, 0x20) # exact original XA identity, not arbitrary reset
    out = bytearray(before)
    out[16:24] = bytes([0, 0, 8 | flags, 0]) * 2
    out[24:2072] = payload
    struct.pack_into("<I", out, 2072, edc(out[16:2072]))
    parity_source = bytes(4) + out[16:2076]
    out[2076:2248] = ecc(parity_source, 86, 24, 2, 86)
    out[2248:2352] = ecc(parity_source + out[2076:2248], 52, 43, 86, 88)
    assert out[:16] == before[:16] # original DESTINATION MSF, not relocated EXE MSF
    return bytes(out)


def compose_exe(original, track):
    assert 0 <= track < 48 and track not in (30, 35)
    out = bytearray(original)
    stages = [
        (0x1CA8, "e0b60580", "98a80580"),
        (0x1DF44, "3700422c", "3800422c"),
        (0x1E034, "37b6000800000000", "1c0684af880680af"),
        (0x40C0, "6666023c", "005e023c"),
        (0x6007C, "64550780", "e4010180"),
        (0x1C57C, "00000000", "0780083c"),
        (0x1C584, "00000000", "6459098d"),
        (0x1C594, "0780033c6459638c0780043c5cf08424402803002128a300802805002128a400",
         "2300262d2118260150f00425402803002128a300802805002128a4000100c638"),
        (0x1C5D8, "0780033cc858638c", "21104600c858038d"),
        (0x5F828, "13000000", struct.pack("<I", track).hex()),
        (0x6007C, "e4010180", "64550780"),
        (0x65D64, "41000000", "4c414200"),
    ]
    for index, (offset, old, new) in enumerate(stages):
        before, after = bytes.fromhex(old), bytes.fromhex(new)
        assert len(before) == len(after) and out[offset:offset + len(before)] == before
        out[offset:offset + len(after)] = after
        if index == 3:
            assert sha(out) == "0f6996babd64e815529df648fd7136f478c42f8187567d71439462df12b931ec"
        if index == 4:
            assert sha(out) == "fa5fc7981188b78fa7d7b78facca64c1f79dadb107515e9146ad178ade39d442"
    inverse = out.copy()
    for offset, old, new in reversed(stages):
        before, after = bytes.fromhex(new), bytes.fromhex(old)
        assert inverse[offset:offset + len(before)] == before
        inverse[offset:offset + len(after)] = after
    assert inverse == original
    assert out[0x65D5C:0x65D64] == original[0x65D5C:0x65D64] and out[0x65D68:0x65D70] == b"JACQUES\0"
    assert out[0x5F85C:0x5FA18] == original[0x5F85C:0x5FA18]
    mapping = [u32(out, 0x5F79C + 4 * i) for i in range(48)]
    source_mapping = [u32(original, 0x5F79C + 4 * i) for i in range(48)]
    assert mapping[35] == track and all(mapping[i] == source_mapping[i] for i in range(48) if i != 35)
    for index in range(35):
        for choice in range(3):
            slot = u32(original, 0x5F85C + 12 * index + 4 * choice)
            assert slot != 35 and mapping[slot] == source_mapping[slot]
    for choice in range(3):
        assert u32(original, 0x5F85C + 12 * 34 + 4 * choice) + 1 == 35
    return bytes(out)


def main(path):
    bodies = build_bodies(path)
    assert [sha(body) for body, _ in bodies[:2]] == [
        "b4abbdd09f794510d9b9341d7c7111ddd0b9068411bab30e70e7477b4f8c096c",
        "1314b299759ad0f66eed054db853c18e8db6dabda7c56a28cabd3fd74ea39643"]
    with open(path, "rb") as image:
        index, source_hash = bytearray(SECTORS * 32), hashlib.sha256()
        assert image.seek(0, 2) == SIZE
        image.seek(0)
        for lba in range(SECTORS):
            raw = image.read(2352)
            assert len(raw) == 2352
            source_hash.update(raw)
            index[32 * lba:32 * lba + 32] = hashlib.sha256(raw).digest()
        assert source_hash.hexdigest() == SOURCE_SHA and image.read(1) == b"" and image.tell() == SIZE
        assert image.seek(0, 2) == SIZE

        def raw_sector(lba):
            assert 0 <= lba < SECTORS
            image.seek(lba * 2352)
            raw = image.read(2352)
            assert len(raw) == 2352 and hashlib.sha256(raw).digest() == index[32 * lba:32 * lba + 32]
            return raw

        def logical(lba, length):
            assert length >= 0 and length % 2048 == 0
            return b"".join(raw_sector(lba + i)[24:2072] for i in range(length // 2048))

        pvd = logical(16, 2048)
        assert pvd[:7] == b"\x01CD001\x01"
        assert u32(pvd, 158) == int.from_bytes(pvd[162:166], "big") == 22
        assert u32(pvd, 166) == int.from_bytes(pvd[170:174], "big") == 2048
        root = logical(22, 2048)
        rows, cursor = {}, 0
        while cursor < len(root) and root[cursor]:
            length = root[cursor]
            assert length >= 34 and cursor + length <= 2048
            row = root[cursor:cursor + length]
            name = row[33:33 + row[32]].decode("ascii").removesuffix(";1")
            extent, size = u32(row, 2), u32(row, 10)
            assert extent == int.from_bytes(row[6:10], "big") and size == int.from_bytes(row[14:18], "big")
            assert name not in rows
            rows[name] = (cursor, extent, size)
            cursor += length
        assert rows["WAD.WAD"][1:] == (37, 0x6927000) and rows["SCUS_942.28"][1:] == (53875, 0x66000)
        assert rows["PETEXA0.STR"][1] == 60000
        header, original_exe = logical(37, 2048), logical(53875, 0x66000)
        assert header[79 * 8:81 * 8] == bytes(16) and header[81 * 8:82 * 8] == bytes.fromhex("0078c90500200000")
        assert sha(original_exe) == "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998"
        tracks = {track: compose_exe(original_exe, track) for track in range(48) if track not in (30, 35)}
        for lba in (22, 37, 53874, 53875, 54078):
            raw = raw_sector(lba)
            assert raw[16:20] == raw[20:24] and raw[18] in (8, 0x89)
            assert form1(raw, raw[24:2072], raw[18] & 0x81) == raw
        gap_hash = hashlib.sha256()
        for lba in range(54079, 55125):
            raw = raw_sector(lba)
            assert raw[:12] == bytes.fromhex("00ffffffffffffffffffff00")
            bcd = lambda n: (n // 10) * 16 + n % 10
            assert raw[12:16] == bytes([bcd((lba + 150) // 4500), bcd(((lba + 150) // 75) % 60), bcd((lba + 150) % 75), 2])
            assert raw[16:24] == bytes.fromhex("0000200000002000") and raw[24:2348] == bytes(2324)
            assert u32(raw, 2348) == edc(raw[16:2348])
            assert all(not (start <= lba < start + (size + 2047) // 2048) for _, start, size in rows.values())
            gap_hash.update(raw)
        assert gap_hash.hexdigest() == "38cb2f93a135ad22d794799a6bbb0fff3d8a1dbabeab00448d94cbe638a3d014"

        results = []
        for body, overlay in bodies:
            append = overlay + body
            assert len(append) in (0x20A800, 0x20B000)
            final_wad_size, final_exe_lba = 0x6927000 + len(append), 53875 + len(append) // 2048
            final_end = final_exe_lba + 204
            changed_root, changed_header = bytearray(root), bytearray(header)
            for name, lba, size in [("WAD.WAD", 37, final_wad_size), ("SCUS_942.28", final_exe_lba, 0x66000)]:
                at = rows[name][0]
                for offset, value in [(2, lba), (10, size)]:
                    struct.pack_into("<I", changed_root, at + offset, value)
                    struct.pack_into(">I", changed_root, at + offset + 4, value)
            struct.pack_into("<IIII", changed_header, 79 * 8, 0x6927000, len(overlay), 0x6936800, len(body))
            forward_hash, inverse_hash = hashlib.sha256(), hashlib.sha256()
            changed, first_changed, last_changed = 0, None, None
            for lba in range(SECTORS):
                raw = raw_sector(lba)
                after, payload = raw, None
                if lba == 22:
                    payload = changed_root
                elif lba == 37:
                    payload = changed_header
                elif 53875 <= lba < final_exe_lba:
                    start = (lba - 53875) * 2048
                    payload = append[start:start + 2048]
                elif final_exe_lba <= lba < final_end:
                    start = (lba - final_exe_lba) * 2048
                    payload = tracks[19][start:start + 2048]
                elif lba == 53874:
                    payload = raw[24:2072]
                if payload is not None:
                    flags = 0x81 if lba in (22, final_exe_lba - 1, final_end - 1) else 0
                    after = form1(raw, payload, flags)
                forward_hash.update(after)
                # Full original raw preimages define the inverse expectation,
                # not reconstructed Form1 logical payloads for former Form2 gap.
                inverse_hash.update(raw)
                if after != raw:
                    changed += 1
                    first_changed = lba if first_changed is None else first_changed
                    last_changed = lba
            assert changed == final_end - 53874 + 2 and first_changed == 22 and last_changed == final_end - 1
            assert inverse_hash.hexdigest() == SOURCE_SHA
            results.append(dict(body_sha256=sha(body), output_sha256=forward_hash.hexdigest(),
                                root_sha256=sha(changed_root), wad_header_sha256=sha(changed_header),
                                executable_sha256=sha(tracks[19]), changed_sectors=changed,
                                final_executable_lba=final_exe_lba, final_wad_size=final_wad_size,
                                inverse_expected_original_sha256=inverse_hash.hexdigest()))
        image.seek(0)
        assert hashlib.file_digest(image, "sha256").hexdigest() == SOURCE_SHA
        assert image.read(1) == b"" and image.tell() == SIZE and image.seek(0, 2) == SIZE
        assert [result["output_sha256"] for result in results] == [
            "1818fe50a132901b9ad670769d4967ccc0a5120897637efa2ad709b8f1d53188",
            "40047bd8e40f23b522bfaa04dd72353013fbcfddbbd9ba6b1e357b87e8664343",
            "e6d5f2abd512814bac54f48b2d3bb3255d09dc4d5abfb6aef87ddf82ea43fc08"]
        print(json.dumps(dict(marker="FRESH_ID65_DISC_RAW_ORACLE_OK", outputs=results,
                              executable_track_sha256={track: sha(data) for track, data in tracks.items()},
                              private_same_pass_sector_index=True, all_following_disc_metadata_authenticated=True,
                              source_before_after_verified=True, original_form1_parity_reproduced=True,
                              physical_candidate_written=False, native_runtime_verified=False,
                              glyph_or_music_cpu_executed=False, publication_approved=False), indent=2))


if __name__ == "__main__":
    if len(sys.argv) != 2:
        raise SystemExit("Usage: source_fresh_disc_oracle.py original.bin")
    main(sys.argv[1])
