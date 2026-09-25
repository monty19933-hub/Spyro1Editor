"""Source-free cases for the native Japanese sign font texel decoder."""
import importlib.util
from pathlib import Path
import struct
import sys
import unittest

spec = importlib.util.spec_from_file_location("jp_font", Path(__file__).with_name("Render-SpyroJapaneseFont.py"))
font = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = font
spec.loader.exec_module(font)


class JapaneseFontCases(unittest.TestCase):
    def test_c5_d5_are_transposed_and_on_distinct_pages(self):
        first, second = font.sign_quad(0x0051), font.sign_quad(0x0151)
        self.assertEqual((16, 17), (first.texture_page, second.texture_page))
        self.assertEqual((23, 16), (first.width, first.height))
        self.assertEqual(((80, 232), (80, 255), (96, 232), (96, 255)), first.uv)
        self.assertEqual(first.uv, second.uv)

    def test_four_packet_formats_and_right_edge_special_case(self):
        self.assertEqual([(24, 16), (23, 16), (23, 16), (23, 15)],
                         [(font.sign_quad(n).width, font.sign_quad(n).height) for n in range(4)])
        edge = font.sign_quad(0xF1)
        self.assertEqual((23, 15), (edge.width, edge.height))
        self.assertEqual((255, 255), edge.uv[3])
        self.assertEqual((0, 236), font.sign_quad(3).uv[0])

    def test_transpose_is_not_a_contiguous_unrotated_crop(self):
        data = bytearray(font.FONT_BYTES)
        # Format1 glyph0051: texture(80,232)=physical(80,488).
        # Independent points at physical x+1 and y+1 become screen y+1 and x+1.
        data[16 * 1024 + 40] = 0x21
        data[17 * 1024 + 40] = 0x03
        quad, indices = font.glyph_indices(bytes(data), 0x51)
        self.assertEqual((1, 3, 2), (indices[0], indices[1], indices[quad.width]))
        self.assertEqual(3, sum(value != 0 for value in indices))

    def test_page_is_64_words_not_64_texels_and_low_nibble_first(self):
        data = bytearray(font.FONT_BYTES)
        data[128] = 0xA3
        self.assertEqual(3, font.nibble(bytes(data), 17, 0, 216))
        self.assertEqual(10, font.nibble(bytes(data), 17, 1, 216))
        self.assertEqual(0, font.nibble(bytes(data), 16, 0, 216))

    def test_palette_selection_and_native_transparency(self):
        data = bytearray(font.FONT_BYTES)
        struct.pack_into("<H", data, 39 * 1024 + 704, 0x7FFF)
        struct.pack_into("<H", data, 38 * 1024 + 704, 0x001F)
        self.assertEqual(0x7FFF, font.palette_words(bytes(data), 0xF6)[0])
        for code in range(0xF7, 0xFB):
            self.assertEqual(0x001F, font.palette_words(bytes(data), code)[0])
        self.assertEqual(0x7FFF, font.palette_words(bytes(data), 0xFB)[0])
        self.assertEqual((255, 255, 255, 255), font.rgba(0x7FFF))
        self.assertEqual((0, 0, 0, 0), font.rgba(0))
        self.assertEqual((0, 0, 0, 255), font.rgba(0x8000))

    def test_control_zero_descriptors_are_not_fake_hiragana(self):
        for value in (0, 0x20, 0x40, 0x46, 0x7F, 0xFD, 0xFF):
            self.assertFalse(font.is_glyph(value))
        for value in (1, 2, 0x21, 0x80, 0xC5, 0xD5, 0xFC):
            self.assertTrue(font.is_glyph(value))

    def test_bounds_are_explicit(self):
        for value in (-1, 4, 0x800, True, 0xF8):
            with self.assertRaises(ValueError):
                font.sign_quad(value)
        with self.assertRaises(ValueError):
            font.glyph_indices(bytes(10), 0)
        for page, u, v in ((15, 0, 216), (24, 0, 216), (16, 256, 216), (16, 0, 215)):
            with self.assertRaises(ValueError):
                font.nibble(bytes(font.FONT_BYTES), page, u, v)


if __name__ == "__main__":
    unittest.main()
