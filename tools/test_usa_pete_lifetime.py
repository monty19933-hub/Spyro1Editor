"""Source-free refusal and boundary counterexamples; no ROM or card output."""
import importlib.util
from pathlib import Path
import struct
import sys
import unittest

spec = importlib.util.spec_from_file_location("pete_lifetime", Path(__file__).with_name("Inspect-SpyroUsaPeteLifetime.py"))
model = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = model
spec.loader.exec_module(model)


class PeteLifetimeCases(unittest.TestCase):
    def test_shared_growth_lowers_base_not_tail_ceiling(self):
        a, b = model.geometry(0x80), model.geometry(0x680)
        self.assertEqual(0x600, a["appendedPeteResidentBase"] - b["appendedPeteResidentBase"])
        self.assertEqual(a["appendedPeteTail"][1], b["appendedPeteTail"][1])

    def test_particle_last_record_overwrites_proposed_shared_tail(self):
        for size in (8, 0x20, 0x80, 0x680):
            g = model.geometry(size)
            # For size<0x20 the final record still owns that range; +1 proves
            # an explicit store intersection only for the larger examples.
            if size >= 0x20:
                self.assertTrue(g["appendedPeteTail"][0] <= g["lastParticleKindStoreWithoutReserveChange"] < g["appendedPeteTail"][1])
            self.assertLessEqual(g["alternateParticlePoolWithoutReserveChange"][0], g["appendedPeteTail"][0])

    def test_increasing_reserve_requires_native_sentinel_guard(self):
        g = model.geometry()
        self.assertEqual([0x801FF000, 0x801FF004], g["alternateSentinelWrite"])
        self.assertEqual([0x801FF010, 0x801FF690], g["proposedLedger"])
        self.assertLessEqual(g["alternateSentinelWrite"][1], g["proposedLedger"][0])
        self.assertLessEqual(g["proposedLedger"][1], g["unchangedOriginalStackReservation"][0])

    def test_normal_floor_is_charged_without_stack_certificate(self):
        g = model.geometry()
        self.assertEqual(0x801873B0, g["normalFloorIfReserveIncreased"])
        self.assertFalse(g["persistentRamOwnerIssued"])
        self.assertFalse(g["allLevelsFitProven"])
        self.assertFalse(g["interruptStackPeakProven"])

    def test_oversize_unaligned_negative_boolean_refused(self):
        for growth, reserve in ((True, 0x800), (0, 0x800), (-8, 0x800), (7, 0x800),
                                (0x800, 0x800), (0x680, 0x801), (0x680, 0), (0x680, True)):
            with self.assertRaises(ValueError): model.geometry(growth, reserve)

    def test_census_distinguishes_direct_calls_from_literal_words(self):
        b = struct.pack("<5I", 0x0C016DF6, 0x08016DF6, 0x8005B7D8, 0x0C016DBE, 0)
        c = model.census(b, 0x80010000)
        self.assertEqual([0x80010000, 0x80010004], c["peteCalls"])
        self.assertEqual([0x8001000C], c["frameCalls"])

    def test_reserve_writer_is_candidate_not_silently_ignored(self):
        c = model.census(struct.pack("<2I", 0x3C018007, 0xAC2055A4), 0x80010000)
        self.assertEqual([0x80010004], c["reserveLowHalfCandidates"])

    def test_partial_word_loads_and_stores_are_candidates(self):
        b = struct.pack("<4I", *(op << 26 | 1 << 21 | 2 << 16 | 0x55A4 for op in (0x22, 0x26, 0x2A, 0x2E)))
        self.assertEqual([0x80010000, 0x80010004, 0x80010008, 0x8001000C],
                         model.census(b, 0x80010000)["reserveLowHalfCandidates"])

    def test_fc_store_requires_matching_high_register(self):
        c = model.census(struct.pack("<4I", 0x3C018008, 0xAC2285FC, 0x3C028008, 0xAC2285FC), 0x80010000)
        self.assertEqual([0x80010004], c["directFcStores"])

    def test_census_refuses_partial_mutable_owner(self):
        for b in (b"x", bytearray(4)):
            with self.assertRaises(ValueError): model.census(b, 0x80010000)

    def test_wrong_image_identity_fails_before_native_inspection(self):
        with self.assertRaises(ValueError): model.capture(Path(__file__))


if __name__ == "__main__":
    unittest.main(verbosity=2)
