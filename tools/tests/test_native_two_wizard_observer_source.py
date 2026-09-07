"""Explicit, read-only original/candidate bindings for the two-Wizard observer.

This checks raw file owners and address arithmetic, not a loaded emulator image,
native instruction execution, gameplay, or peak capacity. No Core assemblies.
Use --source ORIGINAL --candidate-dir EXACT_TWO_CANDIDATE to enable file reads.
"""
import argparse
import hashlib
import importlib.util
from pathlib import Path
import struct
import sys
import unittest

sys.dont_write_bytecode = True
SOURCE = CANDIDATE = None
ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location("two_observer_source_binding", ROOT / "tools/Observe-SpyroNativeTwoWizards.py")
OBS = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = OBS
SPEC.loader.exec_module(OBS)
ORIGINAL_SHA = "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37"
EXE_SHA = "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998"
LENGTH = 661547040


def sha(data): return hashlib.sha256(data).hexdigest()
def word(data, at): return struct.unpack_from("<I", data, at)[0]


class TwoWizardSourceTests(unittest.TestCase):
    def test_exact_raw_candidate_and_original_owners(self):
        if SOURCE is None:
            self.skipTest("Explicit --source and --candidate-dir required; no file binding inferred.")

        def identity(stream, expected):
            self.assertEqual(LENGTH, stream.seek(0, 2))
            stream.seek(0)
            self.assertEqual(expected, hashlib.file_digest(stream, "sha256").hexdigest())
            self.assertEqual(LENGTH, stream.tell())
            self.assertEqual(b"", stream.read(1))

        def logical(stream, lba, offset, size):
            self.assertTrue(0 <= lba < LENGTH // 2352 and 0 <= offset and 0 < size)
            self.assertLessEqual(lba * 2048 + offset + size, LENGTH // 2352 * 2048)
            output = bytearray()
            while len(output) < size:
                position = offset + len(output)
                count = min(size - len(output), 2048 - position % 2048)
                stream.seek((lba + position // 2048) * 2352 + 24 + position % 2048)
                data = stream.read(count)
                self.assertEqual(count, len(data))
                output.extend(data)
            return bytes(output)

        directory = Path(CANDIDATE)
        for name, expected, length in (
            ("dark-hollow-native-two-wizards-r01.cue", OBS.CUE_SHA, 97),
            ("native-two-wizards.receipt.json", OBS.RECEIPT_SHA, 46201),
        ):
            with (directory / name).open("rb") as stream:
                data = stream.read(length + 1)
                self.assertEqual(length, len(data))
                self.assertEqual(expected, sha(data))
                self.assertEqual(b"", stream.read(1))
        with Path(SOURCE).open("rb") as original, (directory / "dark-hollow-native-two-wizards-r01.bin").open("rb") as candidate:
            identity(original, ORIGINAL_SHA)
            identity(candidate, OBS.IMAGE_SHA)
            try:
                exe = logical(original, 53875, 0, 0x66000)
                self.assertEqual(EXE_SHA, sha(exe))
                pete = logical(original, 37, 0x7B8800, 0x800)
                self.assertEqual("b9a4de193b5cf0bd9592de15c57aa5daa3713c3575026154b45402f3aceef6f1", sha(pete))
                # Actual normal-load reserved bytes and PETE size, not pool257.
                self.assertEqual(0x800, word(exe, 0x65DA4))
                self.assertEqual(0x39C48, word(pete, 0))
                self.assertEqual(OBS.PETE, 0x80200000 - word(exe, 0x65DA4) - word(pete, 0))
                self.assertEqual(OBS.POOL, OBS.PETE - 0x2000)
                header = logical(candidate, 37, 0, 0x800)
                offset, length = struct.unpack_from("<II", header, 14 * 8)
                self.assertEqual((0xF02800, 0x2A0800), (offset, length))
                data = logical(candidate, 37, offset, length)
                self.assertEqual(OBS.DATA_SHA, sha(data))
                patched_exe = logical(candidate, 53916, 0, 0x66000)
                self.assertEqual("ce9c99ba467b089603850989bd141f374a7f86cf8e4deeab3fea1a3fc30164d1", sha(patched_exe))
                high, low = struct.unpack_from("<2I", patched_exe, 0x4B048)
                self.assertEqual((0x3C020000, 0x24420000), (high & 0xFFFF0000, low & 0xFFFF0000))
                copy = ((high & 65535) << 16) + struct.unpack("<h", struct.pack("<H", low & 65535))[0]
                self.assertEqual(OBS.GATES["copy_anchor"][2], copy)

                # Bounded complete native component chain; sound end excludes
                # sector padding. This is arithmetic/readback, not native CPU.
                start, data_size = struct.unpack_from("<II", data, 8)
                cursor, end = start, start + data_size
                def advance(at, minimum=4):
                    n = word(data, at)
                    self.assertTrue(n >= minimum and n % 4 == 0 and at + n <= end)
                    return at + n
                for _ in range(6): cursor = advance(cursor)
                count = word(data, cursor); cursor += 4
                self.assertLessEqual(count, 256)
                for _ in range(count):
                    points = word(data, cursor + 4)
                    self.assertTrue(1 <= points <= (end - cursor - 0x34) // 12)
                    cursor = advance(cursor + 0x34 + 12 * points)
                for _ in range(2): cursor = advance(cursor)
                self.assertEqual(bytes(end - cursor), data[cursor:end])
                self.assertEqual(0x387AC, cursor - start)
                model = copy + cursor - start
                scene_ram = model + word(data, 20)
                self.assertEqual(OBS.GATES["model_anchor"][2], model)
                self.assertEqual(OBS.GATES["scene_anchor"][2], scene_ram)
                scene_at, scene_length = struct.unpack_from("<II", data, 24)
                self.assertEqual((OBS.GATES["scene_disk_offset"][2], OBS.GATES["scene_disk_length"][2]), (scene_at, scene_length))
                scene = data[scene_at:scene_at + scene_length]
                at = 0x88
                for _ in range(7):
                    n = word(scene, at)
                    self.assertTrue(n >= 8 and n % 4 == 0 and at + n <= len(scene))
                    at += n
                self.assertEqual((0x8C30, 0x5408, 130), (at, word(scene, at), word(scene, at + 4)))
                self.assertEqual(OBS.MOBY, scene_ram + at + 8)
                self.assertEqual(OBS.DYNAMIC, OBS.MOBY + 130 * 88)
                self.assertEqual(OBS.END, scene_ram + at + word(scene, at))
                self.assertEqual(OBS.CAPACITY, (OBS.END - OBS.DYNAMIC) // (88 + 24))
                self.assertEqual(96, OBS.END - OBS.DYNAMIC - OBS.CAPACITY * 112)
                self.assertGreater(OBS.DYNAMIC + (OBS.CAPACITY + 1) * 88, OBS.END - (OBS.CAPACITY + 1) * 24)

                donor = logical(original, 37, 0x2F6B000, 0x28A000)
                self.assertEqual("9973493f865b5e86bc17b32c5af9e3d7596e25615618c007ed28e0379161f2d8", sha(donor))
                row = donor[0x1CE800 + 0x6388:0x1CE800 + 0x63E0]
                private = donor[0x1CE800 + 0xC350:0x1CE800 + 0xC3A0]
                self.assertEqual("59401eafeab453fd9d26f14eedca58390653dc5f4fcc3d2560f7b3e8fc7afa82", sha(row))
                self.assertEqual("5b3d762f6117ea30e959a21bf2778f3442b03eba93125c9a0e0a642cc58d345b", sha(private))
                for (index, private_ram), (x, endpoint) in zip(OBS.WIZARDS, ((92832, 91808), (86688, 87712)), strict=True):
                    row_at = at + 8 + index * 88
                    p = word(scene, row_at)
                    self.assertEqual(private_ram, scene_ram + p)
                    self.assertEqual(p + 0x28, word(scene, p))
                    expected_row, expected_private = bytearray(row), bytearray(private)
                    struct.pack_into("<I", expected_row, 0, p)
                    struct.pack_into("<iii", expected_row, 12, x, 25248, 10445)
                    struct.pack_into("<I", expected_private, 0, p + 0x28)
                    struct.pack_into("<iii", expected_private, 0x30, x, 25248, 10732)
                    struct.pack_into("<iii", expected_private, 0x40, endpoint, 25248, 10732)
                    self.assertEqual(expected_row, scene[row_at:row_at + 88])
                    self.assertEqual(expected_private, scene[p:p + 0x50])
                    self.assertEqual(1 << (index & 31), 1 << (index - 128))
                # Numerical owner mapping complements the separate unchanged
                # 1,148-check native source lifecycle oracle; no execution here.
                self.assertEqual((0x800778B8 + (128 >> 5) * 4, "<I"), OBS.OWNERS["owners_live_word"])
                self.assertEqual((0x80077898 + (128 >> 5) * 4, "<I"), OBS.OWNERS["owners_checkpoint_word"])
                self.assertEqual((0x80077908 + 2 * 32 + (128 >> 5) * 4, "<I"), OBS.OWNERS["owners_durable_word"])
            finally:
                identity(original, ORIGINAL_SHA)
                identity(candidate, OBS.IMAGE_SHA)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source")
    parser.add_argument("--candidate-dir")
    args, rest = parser.parse_known_args()
    if bool(args.source) != bool(args.candidate_dir): parser.error("Both explicit source and candidate directory are required.")
    SOURCE, CANDIDATE = args.source, args.candidate_dir
    unittest.main(argv=[sys.argv[0]] + rest)
