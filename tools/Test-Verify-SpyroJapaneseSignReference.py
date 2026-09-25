#!/usr/bin/env python3
"""Source-free malformed/inverse-verifier cases. Only temporary fixtures are written."""
import importlib.util
import json
from pathlib import Path
import struct
import sys
import tempfile
import types
import unittest

sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location("jp_reference_verifier", Path(__file__).with_name("Verify-SpyroJapaneseSignReference.py"))
v = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = v
spec.loader.exec_module(v)


def word(data, at, value):
    struct.pack_into("<I", data, at, value)


def fixture():
    base = 0x200
    data = bytearray(0x8000)
    word(data, 0x18, base); word(data, 0x1C, 0x6000)
    moby = base + 0x88
    for _ in range(7):
        word(data, moby, 8); moby += 8
    word(data, moby, 8 + 197 * 88); word(data, moby + 4, 197)
    props = moby + 8 + 197 * 88
    cursor, fields = props + 4, []
    for index in v.SIGN_INDICES:
        row = moby + 8 + 88 * index
        start = cursor - base
        word(data, row, start); data[row + 0x36] = 0x47
        word(data, cursor + 12, start + 24); word(data, cursor + 16, start + 20)
        data[cursor + 24:cursor + 38] = bytes([0x80]) * 13 + b'\xff'
        data[cursor + 40:cursor + 44] = bytes([0xCC]) * 4
        fields.extend((row - base, start + 12, start + 16))
        cursor += 44
    word(data, props, cursor - props)
    word(data, cursor, 4); word(data, cursor + 4, 4)
    fixups = cursor + 8
    word(data, fixups, len(fields))
    for i, field in enumerate(fields):
        word(data, fixups + 4 + i * 4, field)
    return data, dict(base=base, moby=moby, props=props, fixups=fixups, first=props + 4)


def raw(lba):
    result = bytearray(2352)
    result[:12] = bytes.fromhex("00ffffffffffffffffffff00")
    minute, r = divmod(lba + 150, 4500); second, frame = divmod(r, 75)
    bcd = lambda n: n // 10 * 16 + n % 10
    result[12:16] = bytes((bcd(minute), bcd(second), bcd(frame), 2))
    result[16:24] = bytes((0, 0, 8, 0)) * 2
    return v.rebuild_sector(result, lba)


class Memory:
    def __init__(self, data):
        self.data = data
        self.initial = types.SimpleNamespace(st_size=len(data))

    def read(self, offset, length):
        return v.bounded(self.data, offset, length)


class Tests(unittest.TestCase):
    def test_native_message_mapping(self):
        self.assertEqual(v.reference_message(173).hex(), "31373320c5bdd9bcff42c5e1e6ff")
        for index in v.SIGN_INDICES:
            self.assertEqual(len(v.reference_message(index)), 14)
        for index in (172, 188, 193, 197, -1):
            with self.assertRaises(ValueError): v.reference_message(index)

    def test_exact_block_parser(self):
        data, positions = fixture()
        patches = v.parse_sign_blocks(data, 0x803800)
        self.assertEqual(len(patches), 18)
        self.assertEqual(patches[0].wad_offset, 0x803800 + positions["first"])
        for patch in patches:
            self.assertEqual(len(patch.before), 40)
            self.assertEqual(patch.after[:24], patch.before[:24])
            self.assertEqual(patch.after[24:38], v.reference_message(patch.index))
            self.assertEqual(patch.after[38:], bytes(2))

    def test_malformed_scene_and_fixups(self):
        data, p = fixture()
        row = p["moby"] + 8 + 173 * 88
        for at, value in ((0x18, 0xFFFFFFFF), (0x1C, 0), (p["moby"] + 4, 4097),
                          (p["fixups"], 0xFFFFFFFF), (p["fixups"], 0),
                          (p["fixups"] + 4, row - p["base"] + 1),
                          (p["fixups"] + 8, row - p["base"]),
                          (p["fixups"] + 4, p["fixups"] - p["base"]),
                          (p["first"] + 12, p["first"] - p["base"] + 28)):
            with self.subTest(at=at, value=value):
                bad = data.copy(); word(bad, at, value)
                with self.assertRaises(ValueError): v.parse_sign_blocks(bad, 0x803800)

    def test_missing_terminator_and_padding(self):
        data, p = fixture()
        for start, count in ((p["first"] + 24, 20), (p["first"] + 39, 1)):
            bad = data.copy(); bad[start:start + count] = bytes([0x80]) * count
            with self.assertRaises(ValueError): v.parse_sign_blocks(bad, 0x803800)

    def test_no_growth_into_gap(self):
        data, p = fixture()
        data[p["first"] + 24:p["first"] + 40] = bytes([0x80, 0xFF]) + bytes(14)
        with self.assertRaisesRegex(ValueError, "exceeds original"):
            v.parse_sign_blocks(data, 0x803800)

    def test_registered_aliases_refused(self):
        data, p = fixture()
        word(data, p["fixups"], 55)
        word(data, p["fixups"] + 4 + 54 * 4, p["first"] + 40 - p["base"])
        word(data, p["first"] + 40, p["first"] + 24 - p["base"])
        with self.assertRaisesRegex(ValueError, "aliases"):
            v.parse_sign_blocks(data, 0x803800)

    def test_complete_dialog_requires_final_line_flush(self):
        message = v.reference_message(181)
        v.validate_native_message(message)
        with self.assertRaisesRegex(ValueError, "final FF"):
            v.validate_native_message(message[:-1])
        data, p = fixture()
        data[p["first"] + 37] = 0x80
        with self.assertRaisesRegex(ValueError, "final FF"):
            v.parse_sign_blocks(data, 0x803800)

    def test_parity_and_raw_headers(self):
        self.assertEqual(v.edc(b"123456789"), 0x6EC2EDC4)
        for lba in (0, 74, 75, 500, 10000):
            original = raw(lba)
            self.assertEqual(v.rebuild_sector(original, lba), original)
            for at in (0, 12, 15, 20):
                bad = bytearray(original); bad[at] ^= 1
                with self.assertRaises(ValueError): v.rebuild_sector(bad, lba)
        with self.assertRaises(ValueError): v.ecc(bytes(1), 86, 24, 2, 86)

    def test_complete_raw_complement(self):
        original = b"".join(raw(i) for i in range(3))
        edited = bytearray(raw(1)); edited[24] = 0x80
        expected = v.rebuild_sector(edited, 1)
        candidate = original[:2352] + expected + original[4704:]
        digest, count = v.verify_complement(Memory(original), Memory(candidate), {1: expected})
        self.assertEqual(digest, v.sha(candidate)); self.assertGreater(count, 1)
        for at in (24, 2352, 2352 + 2072, 2352 + 2248, 4704 + 100):
            with self.subTest(at=at):
                bad = bytearray(candidate); bad[at] ^= 1
                with self.assertRaises(ValueError): v.verify_complement(Memory(original), Memory(bad), {1: expected})
        with self.assertRaises(ValueError): v.verify_complement(Memory(original), Memory(candidate[:-1]), {1: expected})

    def test_cue_snapshot(self):
        cue = b'FILE "Spyro the Dragon (Japan).bin" BINARY\r\n  TRACK 01 MODE2/2352\r\n    INDEX 01 00:00:00\r\n'
        self.assertEqual(v.expected_cue(cue), b'FILE "japanese-sign-reference.bin" BINARY\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\n')
        for bad in (b"", cue.replace(b"MODE2", b"MODE1"), cue.replace(b"00:00:00", b"00:00:01")):
            with self.assertRaises(ValueError): v.expected_cue(bad)

    def test_receipt_is_not_pass_authority(self):
        data, _ = fixture(); patches = v.parse_sign_blocks(data, 0x803800)
        receipt = dict(schemaVersion=1, profile="original-japanese-artisans-sign-message-reference-r02",
                       SourceImageSha256=v.SOURCE_SHA, SourceImageByteLength=v.SIZE,
                       outputImageSha256="candidate-hash", outputCueSha256="cue-hash",
                       nativeUsaImportApproved=False, editorIntegrationApproved=False, runtimeVerified=False,
                       exactPhysicalComplementVerified=False, rawSectorIntegrityVerified=False)
        receipt["signs"] = [dict(TrueIndex=p.index, editableText=f"{p.index} ガパ\n\\c2カメラ\n", nativeBytesHex=v.reference_message(p.index).hex()) for p in patches]
        receipt["writes"] = [dict(WadOffset=p.wad_offset, ByteLength=len(p.before), BeforeSha256=v.sha(p.before), AfterSha256=v.sha(p.after),
                                 DataWadEntry=23, TrueIndex=p.index, TargetRecordSha256=p.row_sha, FixupSha256=p.fixup_sha) for p in patches]
        encoded = json.dumps(receipt).encode()
        self.assertEqual(v.verify_receipt(encoded, patches, "candidate-hash", "cue-hash"), v.sha(encoded))
        for key, value in (("outputImageSha256", "wrong"), ("nativeUsaImportApproved", True), ("schemaVersion", True)):
            bad = dict(receipt); bad[key] = value
            with self.assertRaises(ValueError): v.verify_receipt(json.dumps(bad).encode(), patches, "candidate-hash", "cue-hash")
        receipt["writes"][0]["WadOffset"] += 4
        with self.assertRaises(ValueError): v.verify_receipt(json.dumps(receipt).encode(), patches, "candidate-hash", "cue-hash")
        with self.assertRaises(ValueError): v.verify_receipt(b'{"schemaVersion":1,"schemaVersion":1}', patches, "candidate-hash", "cue-hash")

    def test_held_named_and_symlink_refusals(self):
        with tempfile.TemporaryDirectory(prefix="jp-reference-verifier-fixture-") as directory:
            # macOS's default temporary root traverses /var -> /private/var.
            # The verifier deliberately refuses symlink inputs, so the fixture
            # itself uses its physical temporary directory path.
            physical_directory = Path(directory).resolve()
            path = physical_directory / "fixture.bin"; path.write_bytes(b"original")
            with v.ReadOnlyFile(path) as held:
                held.stable(v.sha(b"original"))
                path.write_bytes(b"changed!")
                with self.assertRaises(ValueError): held.stable(v.sha(b"original"))
            link = physical_directory / "link.bin"; link.symlink_to(path)
            with self.assertRaises(ValueError): v.ReadOnlyFile(link)
            with self.assertRaises(ValueError): v.ReadOnlyFile(path, 1)


if __name__ == "__main__":
    unittest.main()
