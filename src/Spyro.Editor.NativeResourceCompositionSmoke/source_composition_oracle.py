#!/usr/bin/env python3
"""Independent exact-profile raw-sector oracle. Never writes a ROM or payload."""
import hashlib
import struct
import sys

SOURCE = "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37"
def sha(b): return hashlib.sha256(b).hexdigest()
def u32(b, o): return struct.unpack_from("<I", b, o)[0]
def u16(b, o): return struct.unpack_from("<H", b, o)[0]
def p32(b, o, v): struct.pack_into("<I", b, o, v)
def p16(b, o, v): struct.pack_into("<H", b, o, v)

with open(sys.argv[1], "rb") as image:
    assert image.seek(0, 2) == 661547040
    image.seek(0)
    assert hashlib.file_digest(image, "sha256").hexdigest() == SOURCE
    def read(o, n):
        b = bytearray()
        while n:
            count = min(n, 2048-o % 2048)
            image.seek((37+o//2048)*2352+24+o % 2048)
            b.extend(image.read(count)); o += count; n -= count
        return b
    original = read(0xF01000, 0x28D800)
    donor = read(0x2F6B000, 0x28A000)
    assert sha(original) == "a937aecb3626ffd2311ba136684a34c9f36877054b52557b4524fcacee0e2d97"
    assert sha(donor) == "9973493f865b5e86bc17b32c5af9e3d7596e25615618c007ed28e0379161f2d8"
    base = original[:]
    # Prior independently reviewed exact texture allocation certificate. The
    # oracle copies native pixels directly and reparses/rebases native commands.
    tiles = [(0x7025, 0x7A, 0x58110, 0x700A0, 0xE400, 0xE420, 16, 32),
             (0x722C, 0x1A, 0x58160, 0x72180, 0xE410, 0xE440, 16, 32),
             (0x7A27, 0x1A, 0x50100, 0x7A0E0, 0xE460, 0xE480, 16, 32),
             (0x7822, 0x38, 0x70050, 0x78040, 0x4400, 0xE4A0, 2, 4),
             (0x752D, 0x38, 0x78010, 0x751A0, 0xE470, 0xE4C0, 16, 32)]
    for _, _, sp, sc, tp, tc, width, rows in tiles:
        for row in range(rows):
            a, b = 0x800+sp+row*1024, 0x800+tp+row*1024
            assert not any(base[b:b+width])
            base[b:b+width] = donor[a:a+width]
        assert not any(base[0x800+tc:0x800+tc+32])
        base[0x800+tc:0x800+tc+32] = donor[0x800+sc:0x800+sc+32]
    assert sha(base) == "9c62ac9289cfdbac95c3bcc89a0cb8fdcc0f3ca8e53c7dbdbc601fd152d9f0e1"
    def model(offset, length):
        b = donor[offset:offset+length]
        faces = set()
        for i in range(u32(b, 0)):
            root = u32(b, 0x38+4*i)
            faces.add(u32(b, 0x34)+u32(b, root+0x14))
            if u32(b, root+0x1C): faces.add(u32(b, 0x34)+u32(b, root+0x1C))
        groups = {}
        for table in sorted(faces):
            cursor, end = table+4, table+4+u32(b, table)
            while cursor < end:
                command = u32(b, cursor)
                if not command & 0x80000000:
                    size, field = (20, cursor+8) if command & 2 else (8, None)
                elif command & 4: size, field = 20, cursor+8
                else: size, field = (24, cursor+12) if command & 2 else (12, None)
                assert cursor+size <= end
                if field is not None: groups.setdefault((u16(b, field+2), u16(b, field+6)), []).append(field)
                cursor += size
        for key, fields in groups.items():
            tile = next(t for t in tiles if t[:2] == key)
            tp, tc = tile[4:6]
            min_u = min(b[f+i] for f in fields for i in (0, 4, 8, 10))
            min_v = min(b[f+i+1] for f in fields for i in (0, 4, 8, 10))
            x, y = tp % 1024+1024, tp//1024
            new_u, new_v = x % 128*2, y % 256
            page = (key[1] & ~31) | x//128 | (16 if y >= 256 else 0)
            clut = (tc % 1024+1024)//32 | (tc//1024 << 6)
            for f in fields:
                for i in (0, 4, 8, 10):
                    b[f+i] += new_u-min_u; b[f+i+1] += new_v-min_v
                p16(b, f+2, clut); p16(b, f+6, page)
        return b
    wizard, bolt = model(0x19B518, 0x35B4), model(0x1CAC88, 0x564)
    assert sha(wizard[:0x35AC]) == "776a28cdb88cd542a10898b372c3dbd5fe95357046f7f72641d14e309b7759f3"
    assert sha(bolt) == "51748c9b1eea600542e5f4988078ddcb8149e77156674faa2fe0539f1906fc88"
    wizard[4:7] = bytes((81, 82, 83))
    assert sha(wizard) == "e3cd4d67f39eb9375a8d17b4c2830050f33b5523c3421e7eba828af9f6eabe6e"
    packages = bolt+wizard
    def sound_offset(b):
        cursor = u32(b, 8)
        for _ in range(6): cursor += u32(b, cursor)
        portals = u32(b, cursor); cursor += 4
        for _ in range(portals):
            cursor += 0x34+12*u32(b, cursor+4); cursor += u32(b, cursor)
        return cursor+u32(b, cursor)
    sound, ds = sound_offset(base), sound_offset(donor)
    samples, descriptors = bytearray(), bytearray()
    for index in (40, 49, 50, 51):
        d = donor[ds+0x108+index*20:][:20]
        start = u32(donor, 0)+0x80000+u32(d, 0); end = start
        while True:
            end += 16
            if donor[end-15] & 1: break
        p32(d, 0, 0x70800+len(samples))
        descriptors += d; samples += donor[start:end]
    assert len(samples) == 0xA2C0 and len(packages) == 0x3B18
    a = u32(donor, 0x50)
    while u32(donor, a) != 8: a += 8+u32(donor, a+4)
    animation = donor[a:a+8+u32(donor, a+4)]
    assert len(animation) == 0x3E1C
    for with_scene in (False, True):
        header, blocks, shift = base[:0x800], [], 0
        for index in range(10):
            offset, length = struct.unpack_from("<II", base, index*8)
            if not length: continue
            block = base[offset:offset+length]
            p32(header, index*8, offset+shift)
            if index == 0:
                block += samples+bytes(0xA800-len(samples)); growth = 0xA800
            elif index == 1:
                local = sound-offset; cut = local+u32(base, sound)
                block = block[:cut]+descriptors+block[cut:]+bytes(0x800-len(descriptors))
                p32(block, local, u32(base, sound)+80); p32(block, local+0x104, 84); block[local+4+0x1F] = 80; growth = 0x800
            elif index == 2:
                cut = u32(base, 0x54)-offset-4
                assert u32(block, cut) == 0xFFFFFFFF
                block = block[:cut]+animation+block[cut:]+packages+bytes(0x8000-len(animation)-len(packages)); growth = 0x8000
            else:
                growth = 0
                if index == 3 and with_scene:
                    old = block[:]; block = bytearray(len(old)); block[:0xEED4] = old[:0xEED4]
                    row = donor[0x1CE800+0x6388:][:0x58]; props = donor[0x1CE800+0xC350:][:0x50]
                    p32(row, 0, 0xEED4); p32(props, 0, 0xEEFC)
                    block[0xB838:0xB890] = row; block[0xEED4:0xEF24] = props
                    block[0xEF24:0x10F40] = old[0xEED4:0x10EF0]
                    block[0x10F40:0x11190] = old[0x10EF0:0x11140]
                    p32(block, 0x8C34, 129); p32(block, 0xE038, u32(old, 0xE038)+0x50)
                    p32(block, 0x10F40, 149); p32(block, 0x11190, 0xB838); p32(block, 0x11194, 0xEED4)
            p32(header, index*8+4, length+growth); shift += growth; blocks.append(block)
        roots = 0
        for i in range(64):
            root = u32(base, 0x50+4*i)
            if not root: break
            p32(header, 0x50+4*i, root+0xB000+(len(animation) if i else 0)); roots += 1
        start = u32(base, 0x18)+0xB000+len(animation)
        for i, cls, delta in ((roots, 0x26, 0), (roots+1, 0x11B, len(bolt))):
            assert u32(base, 0x50+4*i) == 0 and u16(base, 0x150+2*i) == 0
            p32(header, 0x50+4*i, start+delta); p16(header, 0x150+2*i, cls)
        final = header+b"".join(blocks)
        assert len(final)-len(base) == 0x13000
        print(f"NATIVE_RESOURCE_COMPOSITION_ORACLE scene={with_scene} sha256={sha(final)} growth=13000 modelGrowth=8000")
        # Optional descriptor7 stage: replay from its independent native record
        # and prior five-tile allocation, not from a Core-produced afterimage.
        record = donor[0x173518:0x173524]
        assert record.hex() == "0700080020e02d753fff3800"
        pixel, clut = tiles[-1][4:6]
        x, y = pixel % 1024+1024, pixel//1024
        for at in (4, 8):
            record[at] += x % 128*2-32; record[at+1] += y % 256-224
        p16(record, 6, (clut % 1024+1024)//32 | (clut//1024 << 6))
        p16(record, 10, (u16(record, 10) & ~31) | x//128 | (16 if y >= 256 else 0))
        first = u32(final, 8); limit = first+u32(final, 12)
        at = 0x128F7C+first-u32(base, 8); insert = at+0x8C
        assert u32(final, at) == 0x8C and u32(final, at+4) == 11 and not any(final[limit-12:limit])
        with_particle = final[:insert]+record+final[insert:limit-12]+final[limit:]
        p32(with_particle, at, 0x98); p32(with_particle, at+4, 12)
        assert len(with_particle) == len(final) and with_particle[u32(final, 0x10):] == final[u32(final, 0x10):]
        print(f"NATIVE_PARTICLE_COMPOSITION_ORACLE scene={with_scene} sha256={sha(with_particle)} record={record.hex()} outputsWritten=0")
    image.seek(0)
    assert hashlib.file_digest(image, "sha256").hexdigest() == SOURCE
    print("NATIVE_RESOURCE_COMPOSITION_ORACLE_OK sourceUnchanged=true outputsWritten=0")
