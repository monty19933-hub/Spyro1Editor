import importlib.util
from pathlib import Path
import struct
import sys
import unittest

spec = importlib.util.spec_from_file_location("modal_texture", Path(__file__).with_name("Inspect-SpyroUsaModalTextureLifetime.py"))
model = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = model
spec.loader.exec_module(model)


def words(*values):
    return struct.pack(f"<{len(values)}I", *values)


class ModalTextureLifetimeCases(unittest.TestCase):
    def test_exact_native_owner_does_not_cover_mirror(self):
        self.assertEqual(0x1C200, model.BACKDROP.byte_length)
        self.assertEqual(0xA000, model.MIRROR.byte_length)
        self.assertEqual(0, model.BACKDROP.intersection_bytes(model.MIRROR))
        self.assertEqual((0x76000, 1024), model.MIRROR.right_upload_rows()[0])
        self.assertEqual((0x7FC00, 1024), model.MIRROR.right_upload_rows()[-1])

    def test_actual_byte_restore_counterexample(self):
        result = model.restore_complement(model.BACKDROP, model.MIRROR)
        self.assertEqual((0, 40960), (result["restoredChangedBytes"], result["unrestoredBytes"]))
        self.assertNotEqual(result["originalSha256"], result["afterSha256"])
        self.assertFalse(result["gpuExecuted"])

    def test_native_backdrop_itself_is_restored(self):
        result = model.restore_complement(model.BACKDROP, model.BACKDROP)
        self.assertEqual((115200, 0), (result["restoredChangedBytes"], result["unrestoredBytes"]))
        self.assertEqual(result["originalSha256"], result["afterSha256"])

    def test_partial_overlap_cannot_claim_complete_ownership(self):
        changed = model.Rect(512, 224, 512, 2)
        result = model.restore_complement(model.BACKDROP, changed)
        self.assertEqual((512, 1536), (result["restoredChangedBytes"], result["unrestoredBytes"]))

    def test_geometry_refusals(self):
        for rectangle in ((-1, 0, 1, 1), (0, 0, 0, 1), (1024, 0, 1, 1), (0, 512, 1, 1), (512, 472, 513, 40), (True, 0, 1, 1)):
            with self.subTest(rectangle=rectangle), self.assertRaises(ValueError): model.Rect(*rectangle)
        with self.assertRaises(ValueError): model.Rect(0, 472, 512, 40).right_upload_rows()

    def test_scalar_load_delay_is_not_immediate(self):
        trace = model.SetupTrace({1: model.END_GLOBAL}, {model.END_GLOBAL: 0x801F0000})
        trace.run(words(0x8C250000, 0x00A03021, 0x00A03821), 0x80010000)
        self.assertEqual((0, 0x801F0000), (trace.reg[6], trace.reg[7]))

    def test_scalar_jal_delay_and_state_order(self):
        # Synthetic straight-line fixture, not a source-authenticated native owner.
        code = words(0x27A40010, 0x24020200, 0xA7A20010, 0xA7A00012, 0x24020100, 0xA7A20014,
                     0x240200E1, 0xA7A20016, 0x3C05801F, 0x0C017E8A, 0x24A53E00,
                     0x0C017DD9, 0x00002021, 0x3C018007, 0xAC2057D8)
        events = model.SetupTrace(globals_={model.MODE_GLOBAL: 2}).run(code, 0x80010000)
        self.assertEqual(["load-image", "draw-sync-request", "game-state-store"], [e["kind"] for e in events])
        self.assertEqual([512, 0, 256, 225], events[0]["rect"])
        self.assertEqual(0x801F3E00, events[0]["ramPointer"])
        self.assertEqual(0, events[-1]["value"])

    def test_scalar_refuses_unowned_memory_unknown_calls_and_bad_delay(self):
        for code in (words(0x8C250000, 0), words(0xAC250000), words(0x0C000000, 0), words(0x0C017DD9),
                     words(0x0C017DD9, 0x24040001), words(0x0C017DD9, 0x0C017DD9), b"\x00"):
            with self.subTest(code=code.hex()), self.assertRaises(ValueError): model.SetupTrace().run(code, 0x80010000)
        with self.assertRaises(ValueError): model.SetupTrace().run(bytearray(4), 0x80010000)

    def test_source_identity_refuses_non_disc(self):
        with self.assertRaises(ValueError): model.capture(Path(__file__))


if __name__ == "__main__":
    unittest.main(verbosity=2)
