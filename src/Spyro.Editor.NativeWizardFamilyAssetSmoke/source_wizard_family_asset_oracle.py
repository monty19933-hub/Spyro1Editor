#!/usr/bin/env python3
"""Exact Wizard33/T6 detached SOURCE-FORM bytes; no target output or Core use.

No existing oracle is imported. Read raw original sectors, native registration /
component / command / ADPCM boundaries, and independently assemble a compact
source-form block. Scene/texture/audio encodings retain their original domains;
this block is not an installed level, VRAM image, SPU bank or native namespace.
"""
import argparse
import hashlib
import json
import struct

IMAGE_LENGTH = 661547040
IMAGE_SHA = "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37"
SOURCE = {
    "exe": (53875 * 2048, 0x66000, "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998"),
    "town": (37 * 2048 + 0x119E000, 0x2E2000, "7ddbf6d9a6ee6c0f64c8564a89e374ca0ca234ef608ec812aae68e8176de1dc0"),
    "donor": (37 * 2048 + 0x2F6B000, 0x28A000, "9973493f865b5e86bc17b32c5af9e3d7596e25615618c007ed28e0379161f2d8"),
    "pete-header": (37 * 2048 + 0x7B8800, 0x800, "b9a4de193b5cf0bd9592de15c57aa5daa3713c3575026154b45402f3aceef6f1"),
    "pete": (37 * 2048 + 0x7B9000, 0x39C48, "1703ac37e59e480909b6f66dc856d4e5f3ce80551f18cbc8c06129c3b54dd35d"),
}


def sha(b): return hashlib.sha256(b).hexdigest()
def word(b, o): return struct.unpack_from("<I", b, o)[0]
def half(b, o): return struct.unpack_from("<H", b, o)[0]


def require(ok, label):
    if not ok: raise ValueError(label)


def extent(b, offset, length, alignment=4):
    require(offset >= 0 and length > 0 and offset % alignment == 0 and
            offset + length <= len(b), "Bounded source extent")
    return b[offset:offset + length]


def identity(image):
    require(image.seek(0, 2) == IMAGE_LENGTH, "Original length")
    image.seek(0)
    require(hashlib.file_digest(image, "sha256").hexdigest() == IMAGE_SHA and
            image.tell() == IMAGE_LENGTH and image.read(1) == b"" and
            image.seek(0, 2) == IMAGE_LENGTH, "Whole original SHA / position / actual EOF")


def logical(image, offset, length):
    require(0 <= offset and 0 < length <= 0x400000, "Bounded read")
    out = bytearray()
    while length:
        n = min(length, 2048 - offset % 2048)
        image.seek(offset // 2048 * 2352 + 24 + offset % 2048)
        b = image.read(n)
        require(len(b) == n, "Exact raw-sector payload read")
        out.extend(b); offset += n; length -= n
    return bytes(out)


def roots(data):
    result = []; terminated = False
    for i in range(64):
        p, cls = word(data, 0x50 + i * 4), half(data, 0x150 + i * 2)
        if not p:
            terminated = True; require(cls == 0, "Root terminator class"); continue
        require(not terminated and cls < 512 and (not result or p > result[-1][1]) and
                cls not in [r[0] for r in result], "Native root order/uniqueness")
        extent(data, p, 4); result.append((cls, p))
    return {cls: (at, (result[i + 1][1] if i + 1 < len(result) else word(data, 0x10) + word(data, 0x14)) - at)
            for i, (cls, at) in enumerate(result)}


def animations(data, root):
    at, length = root; stream = extent(data, at, length); result = {}; p = 0
    while word(stream, p) != 0xFFFFFFFF:
        id_, size = word(stream, p), word(stream, p + 4)
        require(id_ < 46 and id_ not in result and size >= 0x28 and size % 4 == 0, "Animation ID/size")
        b = extent(stream, p, 8 + size)
        require(0 < half(b, 8) < 0x8000 and b[12] == 1 and 0x2C + 4 * half(b, 8) < len(b), "Native type1 frame table")
        result[id_] = (at + p, b); p += len(b)
    require(p + 4 == len(stream), "Complete stream terminator")
    return result


def texture_fields(package):
    tables = set(); model = word(package, 0x34)
    require(1 <= word(package, 0) <= 1024, "Animated package")
    for i in range(word(package, 0)):
        p = word(package, 0x38 + i * 4)
        if p == 0xFFFFFFFF: continue
        extent(package, p, 0x24)
        tables.add(model + word(package, p + 0x14))
        if word(package, p + 0x1C): tables.add(model + word(package, p + 0x1C))
    refs = []
    for at in sorted(tables):
        size = word(package, at); extent(package, at, size + 4)
        p, end = at + 4, at + 4 + size
        while p < end:
            command = word(package, p)
            if command < 0x80000000: n, f = (20, p + 8) if command & 2 else (8, None)
            elif command & 4: n, f = 20, p + 8
            else: n, f = (24, p + 12) if command & 2 else (12, None)
            require(p + n <= end, "Complete native draw command")
            if f is not None: refs.append((f, extent(package, f, 12)))
            p += n
        require(p == end, "Draw table exact end")
    return sorted(tables), refs


def sound(data):
    p = word(data, 8)
    for _ in range(6): p += word(data, p)
    portals = word(data, p); p += 4
    require(portals <= 256, "Portal traversal bound")
    for _ in range(portals):
        p += 0x34 + 12 * word(data, p + 4); p += word(data, p)
    particle = p; p += word(data, p)
    component = extent(data, p, word(data, p))
    n = word(component, 0x104)
    require(0 < n <= 256 and len(component) == 0x108 + 20 * n, "Sound descriptor count")
    bank_at = word(data, 0) + 0x80000
    bank = extent(data, bank_at, word(data, 4) - 0x80000)
    result = []
    for i in range(n):
        d = component[0x108 + i * 20:0x11C + i * 20]; start = word(d, 0); end = start
        require(start % 16 == 0, "Sample alignment")
        while True:
            frame = extent(bank, end, 16, 16)
            require(frame[0] >> 4 <= 4 and frame[0] & 15 <= 12 and frame[1] <= 7, "ADPCM encoding")
            end += 16
            if frame[1] & 1: break
        result.append((p + 0x108 + i * 20, d, bank_at + start, bank[start:end]))
    return particle, p, component[4:260], result


def main(path):
    checks, refusals = [], []
    def check(ok, label): require(ok, label); checks.append(label)
    def reject(call, label):
        try: call()
        except (ValueError, struct.error): refusals.append(label); return
        raise ValueError("Expected refusal: " + label)
    reject(lambda: extent(b"1234", -1, 4), "negative owner")
    reject(lambda: extent(b"1234", 0, 8), "overflow owner")
    reject(lambda: extent(b"1234", 1, 1), "unaligned owner")
    with open(path, "rb") as image:
        identity(image)
        try:
            sources = {name: logical(image, at, n) for name, (at, n, _) in SOURCE.items()}
            for name, (_, _, h) in SOURCE.items(): check(sha(sources[name]) == h, "Full source " + name)
            directory = logical(image, 37 * 2048, 0x800)
            check(sha(directory) == "15d3e9c45f07e27a0ddd3f3a49b23a077e2c7750021cc89c55b7b16c551186cd", "Exact WAD directory")
            check(struct.unpack_from("<II", directory, 40 * 8) == (0x2F6B000, 0x28A000), "Native donor slot40")
            donor, town, pete = sources["donor"], sources["town"], sources["pete"]
            dr, tr = roots(donor), roots(town)
            check(dr[0x11B] == (0x19B518, 0x35B4) and dr[0x26] == (0x1CAC88, 0x564), "Complete registered packages")
            da, ta = animations(donor, dr[0]), animations(town, tr[0])
            check(list(ta) == [18, 19, 29, 40] and 8 not in ta and word(pete, 0x58) == 0xFFFFFFFF, "Town/shared animation8 absence")
            anim_at, anim = da[8]
            check(len(anim) == 0x3E1C and sha(anim) == "cf194a65fc9b3b8790f6cd96eae5ef309156d00ad205d85790b2128e57243eb0", "Complete animation8")
            check(anim[0x0A:0x2C] == ta[18][1][0x0A:0x2C], "Exact Town18 source texture header excluding frame count")
            scene_at = word(donor, 0x18); scene = extent(donor, scene_at, word(donor, 0x1C))
            p = 0x88
            for _ in range(7): p += word(scene, p)
            row_at = p + 8 + 6 * 0x58; row = extent(scene, row_at, 0x58)
            private_at = word(row, 0); private = extent(scene, private_at, 0x50)
            check(row_at == 0x6388 and private_at == 0xC350 and half(row, 0x36) == 0x11B and
                  row[0x53] == 0x56 and row[0x3A] == 0x7F and row[0x48:0x4A] == b"\0\0", "Exact33/T6 role")
            end = p + word(scene, p)
            for _ in range(3): end += word(scene, end)
            fixups = [word(scene, end + 4 + 4 * i) for i in range(word(scene, end))]
            check([f for f in fixups if row_at <= f < row_at + 0x58] == [row_at] and
                  [f for f in fixups if private_at <= f < private_at + 0x50] == [private_at] and
                  word(private, 0) == private_at + 0x28, "Complete row/private pointer-field set")
            particle_at, sound_at, globals_, ds = sound(donor)
            _, _, tg, ts = sound(town)
            particle = extent(donor, particle_at + 20, 12)
            check(half(particle, 0) == 7 and half(particle, 2) == 8, "Whole descriptor7 record")
            owners, block = [], bytearray()
            def add(id_, b):
                check(len(b) > 0 and len(b) % 4 == 0, "Aligned detached owner " + id_)
                owners.append(dict(id=id_, offset=len(block), length=len(b), sha256=sha(b)))
                block.extend(b)
            wizard, bolt = extent(donor, *dr[0x11B]), extent(donor, *dr[0x26])
            for id_, b in [("wizard.model", wizard), ("bolt.model", bolt), ("player.animation8", anim),
                           ("wizard.row", row), ("wizard.private", private), ("particle7", particle)]: add(id_, b)
            tiles, references = [], []
            for owner, package, count, face_set in [("wizard.model", wizard, 51, [0x238, 0x700]), ("bolt.model", bolt, 9, [0x1A8, 0x260])]:
                faces, refs = texture_fields(package)
                check(faces == face_set and len(refs) == count, "Exact source renderer references " + owner)
                groups = {}
                for at, b in refs: groups.setdefault((half(b, 2), half(b, 6)), []).append((at, b))
                for (clut, page), refs in sorted(groups.items()):
                    uv = [(b[o], b[o + 1]) for _, b in refs for o in (0, 4, 8, 10)]
                    tiles.append((owner + f":{clut:04X}:{page:04X}", clut, page, min(u for u, v in uv), min(v for u, v in uv), max(u for u, v in uv), max(v for u, v in uv)))
                    references.extend(dict(owner=owner, offset=at, length=12, tile=tiles[-1][0], sha256=sha(b)) for at, b in refs)
            tiles.append(("particle7", half(particle, 6), half(particle, 10), min(particle[4], particle[8]), min(particle[5], particle[9]), max(particle[4], particle[8]), max(particle[5], particle[9])))
            references.append(dict(owner="particle7", offset=4, length=8, tile="particle7", sha256=sha(particle[4:])))
            texture_info = []
            for id_, clut, page, u0, v0, u1, v1 in tiles:
                width, rows = u1 - u0 + 1, v1 - v0 + 1
                x, y = (page & 15) * 128 + u0 // 2 - 1024, (256 if page & 16 else 0) + v0
                cx, cy = (clut & 63) * 32 - 1024, clut >> 6
                check((page >> 7 & 3) == 0 and u0 % 2 == 0 and width % 2 == 0 and
                      0 <= x < x + width // 2 <= 1024 and 0 <= y < y + rows <= 512 and
                      0 <= cx <= 992 and 0 <= cy < 512, "Complete indexed footprint " + id_)
                pixel_at, clut_at = y * 1024 + x, cy * 1024 + cx
                pixels = b"".join(donor[0x800 + pixel_at + r * 1024:0x800 + pixel_at + r * 1024 + width // 2] for r in range(rows))
                palette = donor[0x800 + clut_at:0x800 + clut_at + 32]
                add(id_ + ".pixels", pixels); add(id_ + ".clut", palette)
                texture_info.append(dict(id=id_, pixelOffset=pixel_at, paletteOffset=clut_at, rowBytes=width // 2, rows=rows, pixelsSha256=sha(pixels), paletteSha256=sha(palette)))
            check(len(tiles) == 5 and sum(t["rowBytes"] * t["rows"] for t in texture_info) == 2056, "Five exact source groups")
            audio = []
            for id_, namespace, selector, index in [("local0", "LocalModelSlot", 0, wizard[4]), ("local1", "LocalModelSlot", 1, wizard[5]),
                    ("local2", "LocalModelSlot", 2, wizard[6]), ("global1F", "GlobalSelector", 0x1F, globals_[0x1F]), ("global26", "GlobalSelector", 0x26, globals_[0x26])]:
                dat, d, sat, sample = ds[index]
                simple_sample = all(sample[o + 1] == (1 if o == len(sample) - 16 else 0) for o in range(0, len(sample), 16))
                supported_import = simple_sample and 0 < half(d, 10) <= 0x3FFF and word(d, 4) == word(d, 12) == word(d, 16) == 0
                check(simple_sample and (supported_import if id_ != "global26" else word(d, 12) == 0x20028), "Exact audio control policy " + id_)
                equivalents = [i for i, (_, td, _, s) in enumerate(ts) if td[4:] == d[4:] and s == sample]
                if id_ == "global26": check(equivalents == [22] and not supported_import, "Death26 resident equivalence, not supported descriptor import")
                add(id_ + ".descriptor", d); add(id_ + ".sample", sample)
                audio.append(dict(id=id_, namespace=namespace, selector=selector, descriptor=index, townEquivalent=equivalents,
                                  sourceDescriptorOffset=dat, sourceSampleOffset=sat, sampleLength=len(sample), sampleSha256=sha(sample), supportedOneShotImport=supported_import))
            check([a["descriptor"] for a in audio] == [49, 50, 51, 40, 22] and
                  [a["townEquivalent"] for a in audio] == [[], [66], [67], [], [22]], "Distinct local/global namespaces and exact Town equivalence")
            check(globals_[0x1F] == 40 and tg[0x1F] == 0 and tg[0x26] == 22, "No implicit global map change")
            check(len(block) == 0x13274 and len(owners) == 26, "Complete compact source-form block")
            check(bytes(block[:0x35B4]) == wizard and block[0x35AC:0x35B4] == donor[0x19EAC4:0x19EACC], "Registered Wizard padding preserved")
            result = dict(checks=len(checks), refusals=len(refusals), sourceSha256=IMAGE_SHA, blockLength=len(block), blockSha256=sha(block),
                          owners=owners, textures=texture_info, textureReferences=references, sounds=audio,
                          animationTextureHeaderSha256=sha(anim[0x0A:0x2C]), sourceForm=True, targetBindingsEmitted=False,
                          nativeCodeExecuted=False, runtimeOrPublicationAuthority=False, filesWritten=0)
        finally:
            identity(image)
    print(json.dumps(result, indent=2))
    print("NATIVE_WIZARD_FAMILY_ASSET_ORACLE_OK sourceUnchanged=true targetOutputsWritten=0")


if __name__ == "__main__":
    p = argparse.ArgumentParser(); p.add_argument("original_bin")
    main(p.parse_args().original_bin)
