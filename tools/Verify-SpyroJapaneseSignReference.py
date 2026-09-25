#!/usr/bin/env python3
"""Independent, read-only inverse verifier for the original-JP sign experiment.

No editor DLL, candidate generator, imported inspector, receipt pass flag or
gameplay state supplies the expected bytes. The EDC/ECC arithmetic below was
independently inspected against source_fresh_disc_oracle.py; it must reproduce
the authenticated original touched sectors before constructing an afterimage.
The tool writes only its JSON result to stdout, never a BIN/CUE/receipt.
"""
from __future__ import annotations

import argparse
import contextlib
import dataclasses
import hashlib
import json
import os
from pathlib import Path
import stat
import struct

SIZE = 727179600
SOURCE_SHA = "1bcf4353d6a4d4d0db152d2ebd60879ef85516feaf170f364ff419c54f599ea7"
SOURCE_CUE_SHA = "0faaba827d5a480062dca5eb0a74d31b3079ef9a52071ba3c22acb028fea02ba"
ARTISANS_SHA = "7184fff950efa2bdffa37f97875ef7de2a3219480c61ec3ab37f784778d8cfde"
EXE_SHA = "a67c21f441deb3a4df26099d78ddaa29cc66b927a7387718a678eee71664cee4"
FONT_SHA = "917a2c272859d99b573ee3f79b1a62ca847dff218802b2c65f80bb55cb888ff1"
GLYPH_SHA = "3ebf51c29f337e461de260343b0e11b85eadeec9972b3eefd1afd04b33085913"
SIGN_INDICES = tuple(range(173, 188)) + (194, 195, 196)
WAD_LBA, WAD_SIZE = 500, 112793600
RAW, USER = 2352, 2048
KATAKANA = "アイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロワヲンァィゥェォャュョッ"


def require(value, message):
    if not value:
        raise ValueError(message)


def sha(data):
    return hashlib.sha256(data).hexdigest()


def bounded(data, offset, length):
    require(offset >= 0 and length >= 0 and offset + length <= len(data), "Range exceeds its explicit owner")
    return data[offset:offset + length]


def u32(data, offset=0):
    return struct.unpack("<I", bounded(data, offset, 4))[0]


def component_end(data, offset, minimum):
    length = u32(data, offset)
    require(offset % 4 == 0 and length >= minimum and length % 4 == 0, "Invalid component length/alignment")
    bounded(data, offset, length)
    return offset + length


def reference_message(index):
    require(index in SIGN_INDICES, "Not one of the 18 original Artisans sign indices")
    kana = lambda letter: 0xC0 + KATAKANA.index(letter)
    # Native signs use BD/BC, not the differently positioned alternate 02/01.
    result = str(index).encode("ascii") + bytes((0x20, kana("カ"), 0xBD, kana("ハ"), 0xBC,
                                                0xFF, 0x42, kana("カ"), kana("メ"), kana("ラ"), 0xFF))
    require(len(result) == 14, "Reference message must contain exactly 14 native bytes including final-line flush")
    return result


def validate_native_message(message):
    admitted = set(range(1, 0x19)) | set(range(0x20, 0x2A)) | set(range(0x2B, 0x3A)) | {0x3D, 0x3F} | set(range(0x40, 0x47)) | set(range(0x80, 0xFD)) | {0xFF}
    require(0 < len(message) <= 153 and all(value in admitted for value in message), "Undefined/overlong native message")
    require(message[-1] == 0xFF, "A complete native dialog requires a final FF line flush before NUL")
    lines = message.split(b"\xff")
    require(len(lines) <= 9 and all(len(line) <= 47 for line in lines), "Native message line-buffer bound exceeded")


@dataclasses.dataclass(frozen=True)
class Patch:
    index: int
    wad_offset: int
    data_offset: int
    before: bytes
    after: bytes
    row_sha: str
    fixup_sha: str


def parse_sign_blocks(data, wad_offset):
    """Independent structural parser, also exercised by synthetic malformed fixtures."""
    scene_offset, scene_length = u32(data, 0x18), u32(data, 0x1C)
    require(scene_offset % 4 == 0 and scene_length >= 0x90 and scene_length % 4 == 0, "Invalid scene owner")
    scene = bounded(data, scene_offset, scene_length)
    moby = 0x88
    for _ in range(7):
        moby = component_end(scene, moby, 8)
    props = component_end(scene, moby, 8)
    count = u32(scene, moby + 4)
    require(count <= 4096 and moby + 8 + 88 * count <= props, "Moby rows exceed their component")
    props_end = component_end(scene, props, 4)
    fixups_at = component_end(scene, component_end(scene, props_end, 4), 4)
    fixup_count = u32(scene, fixups_at)
    require(fixup_count <= (len(scene) - fixups_at - 4) // 4, "Relocation count exceeds scene")
    fixups = bounded(scene, fixups_at, 4 + 4 * fixup_count)
    references = {}
    for i in range(fixup_count):
        field = u32(fixups, 4 + i * 4)
        require(field % 4 == 0 and field + 4 <= fixups_at and field not in references, "Invalid/duplicate relocation field")
        target = u32(scene, field)
        require(target < len(scene), "Relocation target exceeds scene")
        references[field] = target
    rows = [(i, moby + 8 + 88 * i, bounded(scene, moby + 8 + 88 * i, 88)) for i in range(count)]
    pointers = [u32(row) for _, _, row in rows]
    selected = [(i, at, row) for i, at, row in rows if row[0x36:0x38] == b"\x47\x00"]
    require(tuple(i for i, _, _ in selected) == SIGN_INDICES, "Original Artisans sign identity census differs")
    patches = []
    for index, row_at, row in selected:
        start = u32(row)
        require(props + 4 <= start < props_end and start % 4 == 0 and pointers.count(start) == 1, "Sign property pointer is invalid or row-aliased")
        boundary = min([p for p in pointers if start < p <= props_end] + [props_end])
        header = bounded(scene, start, 24)
        require(start + 24 < boundary, "Sign header overlaps the next observed property")
        require(header[:12] == bytes(12) and header[20:24] == bytes(4) and
                u32(header, 12) == start + 24 and u32(header, 16) == start + 20, "Native sign pointer/counter header changed")
        require(row[0x3C] == row[0x3D] and row[0x3C] <= 2, "Unsupported sign model variant")
        text_owner = bounded(scene, start + 24, boundary - start - 24)
        require(0 in text_owner, "Missing native message terminator")
        old_message = text_owner[:text_owner.index(0)]
        validate_native_message(old_message)
        length = (24 + len(old_message) + 1 + 3) & ~3
        require(start + length <= boundary, "Original aligned sign block crosses another owner")
        before = bounded(scene, start, length)
        require(before == header + old_message + bytes(length - 24 - len(old_message)), "Original message padding is not exact zero padding")
        required = {row_at: start, start + 12: start + 24, start + 16: start + 20}
        require(all(references.get(field) == target for field, target in required.items()), "A required sign relocation is absent")
        for field, target in references.items():
            overlaps_field = field < start + length and field + 4 > start
            if overlaps_field or start <= target < start + length:
                require(field in required, "Another registered pointer aliases the exact sign block")
        message = reference_message(index)
        require(24 + len(message) + 1 <= length, "Reference text exceeds original terminated/aligned block")
        after = header + message + bytes(length - 24 - len(message))
        patches.append(Patch(index, wad_offset + scene_offset + start, scene_offset + start,
                             before, after, sha(row), sha(fixups)))
    require(all(a.wad_offset + len(a.before) <= b.wad_offset for a, b in zip(patches, patches[1:])), "Sign blocks overlap or reorder")
    return patches


# Reflected CD-ROM EDC polynomial and GF(2^8) parity. No editor implementation is called.
EDC, FORWARD, BACKWARD = [], [0] * 256, [0] * 256
for n in range(256):
    value = n
    for _ in range(8):
        value = (value >> 1) ^ (0xD8018001 if value & 1 else 0)
    EDC.append(value)
    doubled = (n << 1) ^ (0x11D if n & 0x80 else 0)
    FORWARD[n], BACKWARD[n ^ doubled] = doubled, n


def edc(data):
    result = 0
    for byte in data:
        result = (result >> 8) ^ EDC[(result ^ byte) & 255]
    return result


def ecc(data, major_count, minor_count, major_mult, minor_inc):
    size = major_count * minor_count
    require(len(data) == size, "Parity input owner changed")
    result = bytearray(2 * major_count)
    for major in range(major_count):
        cursor, a, b = (major >> 1) * major_mult + (major & 1), 0, 0
        for _ in range(minor_count):
            value = data[cursor]
            cursor = (cursor + minor_inc) % size
            a ^= value
            b ^= value
            a = FORWARD[a]
        a = BACKWARD[FORWARD[a] ^ b]
        result[major], result[major + major_count] = a, a ^ b
    return bytes(result)


def rebuild_sector(raw, lba):
    require(len(raw) == RAW and raw[:12] == bytes.fromhex("00ffffffffffffffffffff00"), "Invalid raw sync/sector size")
    absolute = lba + 150
    minute, remainder = divmod(absolute, 4500)
    second, frame = divmod(remainder, 75)
    require(lba >= 0 and minute < 100, "Invalid native MSF range")
    bcd = lambda n: (n // 10) * 16 + n % 10
    require(raw[12:16] == bytes((bcd(minute), bcd(second), bcd(frame), 2)), "Wrong BCD MSF or MODE2")
    require(raw[16:20] == raw[20:24] and raw[18] & 0x20 == 0, "Not a duplicated MODE2 Form1 subheader")
    result = bytearray(raw)
    struct.pack_into("<I", result, 2072, edc(result[16:2072]))
    parity = bytes(4) + result[16:2076]
    result[2076:2248] = ecc(parity, 86, 24, 2, 86)
    result[2248:] = ecc(parity + result[2076:2248], 52, 43, 86, 88)
    return bytes(result)


class ReadOnlyFile:
    def __init__(self, path, limit=None):
        self.path = Path(os.path.abspath(path))
        for part in (self.path, *self.path.parents):
            require(not stat.S_ISLNK(os.lstat(part).st_mode), "Symbolic-link input refused")
        fd = os.open(self.path, os.O_RDONLY | os.O_NOFOLLOW)
        self.file = os.fdopen(fd, "rb")
        self.initial = os.fstat(fd)
        try:
            require(stat.S_ISREG(self.initial.st_mode) and (limit is None or self.initial.st_size <= limit), "Input must be a bounded regular file")
        except BaseException:
            self.file.close()
            raise

    def __enter__(self):
        return self

    def __exit__(self, *_):
        self.file.close()

    def read(self, offset, length):
        require(0 <= offset and 0 <= length <= 16 * 1024 * 1024 and offset + length <= self.initial.st_size, "Physical read exceeds its owner")
        self.file.seek(offset)
        data = self.file.read(length)
        require(len(data) == length, "Truncated file")
        return data

    def digest(self):
        self.file.seek(0)
        return hashlib.file_digest(self.file, "sha256").hexdigest()

    def stable(self, expected_hash):
        signature = lambda s: (s.st_dev, s.st_ino, s.st_size, s.st_mode, s.st_mtime_ns, s.st_ctime_ns)
        require(signature(self.initial) == signature(os.fstat(self.file.fileno())) == signature(os.lstat(self.path)), "Held/named input identity changed")
        require(self.digest() == expected_hash, "Held input bytes changed")
        with ReadOnlyFile(self.path) as named:
            require(named.digest() == expected_hash, "Named input bytes changed")


def logical(image, lba, owner_length, offset, length):
    require(lba >= 0 and offset >= 0 and length >= 0 and offset + length <= owner_length, "Logical read exceeds file owner")
    output = bytearray()
    while length:
        sector, within = divmod(offset, USER)
        count = min(USER - within, length)
        output.extend(image.read((lba + sector) * RAW + 24 + within, count))
        offset += count
        length -= count
    return bytes(output)


def source_patches(source):
    require(source.initial.st_size == SIZE and source.digest() == SOURCE_SHA, "Requires the exact original Japanese BIN")
    pvd = logical(source, 16, 2048, 0, 2048)
    require(pvd[:7] == b"\x01CD001\x01" and u32(pvd, 158) == 22 and u32(pvd, 166) == 2048, "Japanese ISO root ownership changed")
    root = logical(source, 22, 2048, 0, 2048)
    directory, cursor = {}, 0
    while cursor < len(root) and root[cursor]:
        length = root[cursor]
        require(length >= 34, "Short ISO root record")
        row = bounded(root, cursor, length)
        name = bounded(row, 33, row[32]).decode("ascii").removesuffix(";1")
        require(name not in directory and u32(row, 2) == int.from_bytes(row[6:10], "big") and
                u32(row, 10) == int.from_bytes(row[14:18], "big"), "Ambiguous ISO record ownership")
        directory[name] = (u32(row, 2), u32(row, 10))
        cursor += length
    require(directory.get("WAD.WAD") == (WAD_LBA, WAD_SIZE) and directory.get("SCPS_100.83") == (24, 0x70000), "Regional ISO file owner changed")
    executable = logical(source, 24, 0x70000, 0, 0x70000)
    require(sha(executable) == EXE_SHA and sha(executable[0x69DB8:0x69FB8]) == GLYPH_SHA, "Native executable/glyph descriptor identity changed")
    header = logical(source, WAD_LBA, WAD_SIZE, 0, 2048)
    require((u32(header, 16), u32(header, 20)) == (0x6F800, 0xA000), "Native font WAD owner changed")
    require(sha(logical(source, WAD_LBA, WAD_SIZE, 0x6F800, 0xA000)) == FONT_SHA, "Native font pixels changed")
    offset, length = u32(header, 23 * 8), u32(header, 23 * 8 + 4)
    require((offset, length) == (0x803800, 0x386800), "Artisans regional WAD owner changed")
    data = logical(source, WAD_LBA, WAD_SIZE, offset, length)
    require(sha(data) == ARTISANS_SHA, "Original Artisans data identity changed")
    return parse_sign_blocks(data, offset)


def expected_sectors(source, patches):
    original, after = {}, {}
    for patch in patches:
        for relative, value in enumerate(patch.after):
            sector, within = divmod(patch.wad_offset + relative, USER)
            lba = WAD_LBA + sector
            if lba not in after:
                raw = source.read(lba * RAW, RAW)
                require(rebuild_sector(raw, lba) == raw, "Independent parity oracle failed to reproduce original sector")
                original[lba] = raw
                after[lba] = bytearray(raw)
            require(original[lba][24 + within] == patch.before[relative], "Independent raw/logical patch preimage differs")
            after[lba][24 + within] = value
    return {lba: rebuild_sector(raw, lba) for lba, raw in after.items()}


def verify_complement(source, candidate, sectors):
    """Compare every raw byte against independently reconstructed expected bytes."""
    if candidate is not None:
        require(candidate.initial.st_size == source.initial.st_size, "Candidate size changed")
    expected_hash, actual_hash, changed = hashlib.sha256(), hashlib.sha256(), 0
    block = RAW * 128
    for offset in range(0, source.initial.st_size, block):
        before = source.read(offset, min(block, source.initial.st_size - offset))
        expected = bytearray(before)
        first = offset // RAW
        for lba in range(first, first + len(before) // RAW):
            if lba in sectors:
                within = (lba - first) * RAW
                expected[within:within + RAW] = sectors[lba]
        expected_hash.update(expected)
        if candidate is not None:
            actual = candidate.read(offset, len(before))
            require(actual == expected, f"Candidate has an unplanned raw byte or incorrect parity near 0x{offset:X}")
            actual_hash.update(actual)
        if expected != before:
            changed += sum(a != b for a, b in zip(expected, before))
    if candidate is not None:
        require(actual_hash.digest() == expected_hash.digest(), "Candidate does not match independent full afterimage")
    return expected_hash.hexdigest(), changed


def expected_cue(original_bytes):
    require(sha(original_bytes) == SOURCE_CUE_SHA, "Requires exact supplied original Japanese CUE")
    lines = original_bytes.decode("utf-8").splitlines()
    require(lines == ['FILE "Spyro the Dragon (Japan).bin" BINARY', '  TRACK 01 MODE2/2352', '    INDEX 01 00:00:00'], "Original CUE geometry changed")
    lines[0] = 'FILE "japanese-sign-reference.bin" BINARY'
    return ("\n".join(lines) + "\n").encode("utf-8")


def verify_receipt(data, patches, image_hash, cue_hash):
    def unique_pairs(pairs):
        result = {}
        for key, value in pairs:
            require(key not in result, "Duplicate JSON receipt key")
            result[key] = value
        return result
    receipt = json.loads(data, object_pairs_hook=unique_pairs)
    require(isinstance(receipt, dict) and type(receipt.get("schemaVersion")) is int and receipt.get("schemaVersion") == 1 and
            receipt.get("profile") == "original-japanese-artisans-sign-message-reference-r02", "Unexpected receipt profile")
    require(receipt.get("SourceImageSha256") == SOURCE_SHA and receipt.get("SourceImageByteLength") == SIZE and
            receipt.get("outputImageSha256") == image_hash and receipt.get("outputCueSha256") == cue_hash, "Receipt identity differs from independent readback")
    signs = [{"TrueIndex": p.index, "editableText": f"{p.index} ガパ\n\\c2カメラ\n", "nativeBytesHex": reference_message(p.index).hex()} for p in patches]
    writes = [{"WadOffset": p.wad_offset, "ByteLength": len(p.before), "BeforeSha256": sha(p.before), "AfterSha256": sha(p.after),
               "DataWadEntry": 23, "TrueIndex": p.index, "TargetRecordSha256": p.row_sha, "FixupSha256": p.fixup_sha} for p in patches]
    require(receipt.get("signs") == signs and receipt.get("writes") == writes, "Receipt sign/write descriptions differ from independent reconstruction")
    # These limitations may not be silently promoted; positive verification flags
    # above are intentionally neither consumed nor trusted as evidence.
    require(all(receipt.get(key) is False for key in ("nativeUsaImportApproved", "editorIntegrationApproved", "runtimeVerified")), "Receipt exceeds private-reference authority")
    return sha(data)


def verify(args):
    require(Path(args.original).name == "Spyro the Dragon (Japan).bin", "Original CUE must bind the named original BIN")
    require(Path(args.original_cue).parent.resolve() == Path(args.original).parent.resolve(), "Original CUE/BIN ownership differs")
    with contextlib.ExitStack() as stack:
        original = stack.enter_context(ReadOnlyFile(args.original))
        source_cue = stack.enter_context(ReadOnlyFile(args.original_cue, 65536))
        cue_bytes = expected_cue(source_cue.read(0, source_cue.initial.st_size))
        patches = source_patches(original)
        sectors = expected_sectors(original, patches)
        candidate = None
        if not args.preview_original:
            require(Path(args.candidate).name == "japanese-sign-reference.bin" and
                    Path(args.cue).name == "japanese-sign-reference.cue" and Path(args.receipt).name == "reference-receipt.json" and
                    Path(args.cue).parent.resolve() == Path(args.candidate).parent.resolve() == Path(args.receipt).parent.resolve(),
                    "Candidate CUE/BIN/receipt owner differs")
            candidate = stack.enter_context(ReadOnlyFile(args.candidate))
        image_hash, changed = verify_complement(original, candidate, sectors)
        result = {"schemaVersion": 1, "mode": "original-only-preview" if candidate is None else "independent-candidate-verification",
                  "sourceImageSha256": SOURCE_SHA, "expectedCandidateSha256": image_hash, "signs": len(patches),
                  "touchedRawSectors": sorted(sectors), "changedRawBytes": changed, "expectedCueSha256": sha(cue_bytes),
                  "candidateVerified": candidate is not None, "nativeUsaImportApproved": False, "runtimeVerified": False}
        if candidate is not None:
            cue = stack.enter_context(ReadOnlyFile(args.cue, 65536))
            receipt = stack.enter_context(ReadOnlyFile(args.receipt, 256 * 1024))
            require(cue.read(0, cue.initial.st_size) == cue_bytes, "Output CUE differs from exact preserved track geometry")
            receipt_hash = verify_receipt(receipt.read(0, receipt.initial.st_size), patches, image_hash, sha(cue_bytes))
            candidate.stable(image_hash); cue.stable(sha(cue_bytes)); receipt.stable(receipt_hash)
            result["receiptSha256"] = receipt_hash
        original.stable(SOURCE_SHA); source_cue.stable(SOURCE_CUE_SHA)
        return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--original", required=True)
    parser.add_argument("--original-cue", required=True)
    parser.add_argument("--candidate")
    parser.add_argument("--cue")
    parser.add_argument("--receipt")
    parser.add_argument("--preview-original", action="store_true", help="Derive expected hashes without writing or verifying a candidate")
    args = parser.parse_args()
    if args.preview_original:
        require(not any((args.candidate, args.cue, args.receipt)), "Preview must not pretend to verify candidate artifacts")
    else:
        require(all((args.candidate, args.cue, args.receipt)), "Candidate, CUE and receipt are all required")
    print(json.dumps(verify(args), indent=2, ensure_ascii=False))


if __name__ == "__main__":
    main()
