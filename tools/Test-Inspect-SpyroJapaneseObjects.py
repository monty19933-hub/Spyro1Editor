#!/usr/bin/env python3
"""Source-free ownership and instruction decoding checks for the JP inspector."""
import importlib.util
import pathlib
import struct
import unittest

spec = importlib.util.spec_from_file_location("jp", pathlib.Path(__file__).with_name("Inspect-SpyroJapaneseObjects.py"))
jp = importlib.util.module_from_spec(spec)
spec.loader.exec_module(jp)


class InspectorTests(unittest.TestCase):
    def test_pinned_hash_shape(self):
        self.assertEqual(len(jp.IMAGE_SHA256), 64)
        self.assertEqual(len(jp.EXE_SHA256), 64)

    def test_range_never_wraps_or_slices_truncation(self):
        for offset, size in [(-1, 1), (0, -1), (4, 1), (2, 3), (2**63, 1)]:
            with self.subTest(offset=offset, size=size), self.assertRaises(ValueError):
                jp.bounded(b"1234", offset, size)

    def test_mips_delay_branch_and_jump_destinations(self):
        self.assertEqual(jp.mips_instruction(0x1440FFFF, 0x80080000), "bne v0, zero, 0x80080000")
        self.assertEqual(jp.mips_instruction(0x080243E9, 0x80080000), "j 0x80090FA4")
        self.assertEqual(jp.mips_instruction(0x96620036, 0x80087F00), "lhu v0, 54(s3)")
        self.assertEqual(jp.mips_instruction(0x8FBD0000, 0), "lw sp, 0(sp)")
        self.assertIn("unimplemented decoder", jp.mips_instruction(0xFFFFFFFF, 0))

    def test_disassembly_alignment_and_bounds(self):
        with self.assertRaises(ValueError):
            jp.disassemble(bytes(8), 2, 4, 0)
        with self.assertRaises(ValueError):
            jp.disassemble(bytes(8), 4, 8, 0)

    def sign_scene(self):
        data = bytearray(96)
        struct.pack_into("<II", data, 16 + 12, 40, 36)
        data[40:45] = b"\x80\xff\x44\x81\x00"
        return data

    def test_message_retains_native_controls(self):
        result = jp.sign_message(self.sign_scene(), 16, 16, 48)
        self.assertEqual(result["bytesHex"], "80ff4481")
        self.assertEqual(result["lineCount"], 2)
        self.assertEqual(result["cursorSceneOffset"], 36)
        self.assertEqual(result["zeroTerminatorSceneOffset"], 44)

    def test_terminator_must_precede_next_observed_boundary(self):
        data = self.sign_scene()
        data[44:48] = b"\x80" * 4
        with self.assertRaises(ValueError):
            jp.sign_message(data, 16, 16, 48)

    def test_message_pointer_and_header_extent_refusals(self):
        for offset, value in [(28, 44), (32, 40)]:
            data = self.sign_scene()
            struct.pack_into("<I", data, offset, value)
            with self.assertRaises(ValueError):
                jp.sign_message(data, 16, 16, 48)
        for start, end in [(20, 48), (16, 36)]:
            with self.assertRaises(ValueError):
                jp.sign_message(self.sign_scene(), 16, start, end)

    def test_model_anim_count_is_not_unbounded(self):
        data = bytearray(256)
        struct.pack_into("<I", data, 0, 0xFFFFFFFF)
        with self.assertRaises(ValueError):
            jp.inspect_model(data, 0, 256)

    def test_scene_zero_component_cannot_hide_rows(self):
        data = bytearray(1024)
        struct.pack_into("<II", data, 0x18, 0x100, 0x300)
        with self.assertRaises(ValueError):
            jp.scene_layout(data)


if __name__ == "__main__":
    unittest.main()
