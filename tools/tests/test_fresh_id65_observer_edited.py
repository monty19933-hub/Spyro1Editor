"""Exact edited-profile observer tests; fake memory only, no socket/source writes."""
import hashlib
import importlib.util
import json
from pathlib import Path
import socket
import sys
import unittest
from unittest.mock import patch

sys.dont_write_bytecode = True
_path = Path(__file__).with_name("test_fresh_id65_observer.py")
_spec = importlib.util.spec_from_file_location("edited_observer_starter_control", _path)
base = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(base)
observer = base.OBSERVER
EDITED = "edited-landing-track02-r01"
EXPECTED = {
    "expected_profile_annotation": "fresh-id65-edited-landing-track02-r01-idle-center-walk-research-v1",
    "expected_candidate_sha256_annotation": "ecc8f61cc16bf86a2f5117b327c49aede18f5c1e6a602c0e67f2ba58565aed16",
    "expected_body_sha256_annotation": "bae43882a39dfc1604f4e6d578a354f34f99107f97faa9330600b3e01f34b15b",
    "expected_scene_sha256_annotation": "13e3674859b79ff45fa950cb1c2f788a368f89d70bb24599dfbccf061a75c0de",
    "expected_exe_sha256_annotation": "2c1fd87e9fa8ce5c5f16be53e738405cec90c0bfc10ac9cb8cf1ce979b2e5c57",
    "expected_cue_sha256_annotation": "1fcb9bade2f3c9c59b6e6d7b57bd8a2bd0773534d151374d62fcb2ce442e663c",
    "expected_receipt_sha256_annotation": "3b545c4dd0e2102586e9f520dec96fda3ea8e84a088003d5b7a7a5ef701373d7",
    "expected_canonical_document_sha256_annotation": "b3f10d40db88cbfa3d3393e0d9c9e5e49e9a829f3fd311bcfa005082016e4cf1",
    "expected_plan_sha256_annotation": "fba3e61a3f93b9dff27d9a5304eb746b7c2ef9aa95a71cfeda6adaee62b571c2",
}


def captured(x=9216, y=9216, z=9216):
    reader = base.fixture()
    reader.set("player_xyz", (x, y, z))
    return reader, observer.capture(reader, "edited", profile=EDITED)


class EditedObserverTests(unittest.TestCase):
    def test_default_full_sample_and_read_trace_are_legacy_exact(self):
        reader = base.fixture()
        sample = observer.capture(reader, "starter-preservation")
        sample["utc"] = "fixed"
        self.assertEqual("0fcc9a65d924086ad77b972cb20487f89cd8b6bb4b8099fdfe7486ab80d02d57",
                         hashlib.sha256(json.dumps(sample, sort_keys=True, separators=(",", ":")).encode()).hexdigest())
        self.assertEqual("7d32b2ad67f5de0234595a1f6492a6752489f35937b2e706f399f61f53992fc7",
                         hashlib.sha256(json.dumps(reader.reads, separators=(",", ":")).encode()).hexdigest())
        self.assertEqual(95, len(reader.reads))

    def test_edited_profile_is_exact_annotation_not_loaded_disc_or_ui_approval(self):
        reader, sample = captured()
        self.assertTrue(sample["payload_valid"])
        self.assertTrue(sample["gate_reread_consistent"])
        for key, value in EXPECTED.items(): self.assertEqual(value, sample[key])
        self.assertEqual([8192, 8192, 10240, 10240], sample["authored_floor_xy_bounds_annotation"])
        self.assertEqual([9216, 9216, 9216], sample["expected_spawn_xyz_annotation"])
        self.assertEqual(96, sample["expected_spawn_heading_u8_annotation"])
        self.assertEqual(8704, sample["expected_floor_z_annotation"])
        self.assertEqual(2, sample["expected_music_track_id_annotation"])
        self.assertEqual(255, sample["player_heading_u8"], "actual heading remains independently sampled")
        for name in ("candidate_authenticated", "runtime_pass", "ui_result_approved", "normal_export_approved"):
            self.assertIs(False, sample[name])
        self.assertNotIn("attention", sample)
        self.assertNotIn((0xDEADBEEF, 4), reader.reads)
        self.assertNotIn((base.BASE + 16 * 88, 88), reader.reads)

    def test_distinct_floor_edges_and_outside_without_broadening_starter(self):
        for x in (8192, 9216, 10240):
            for y in (8192, 9216, 10240):
                for z in (-2147483648, 2147483647):
                    _, sample = captured(x, y, z)
                    self.assertTrue(sample["sampled_player_xy_inside_authored_floor"])
                    self.assertNotIn("attention", sample)
        for x, y in ((8191, 9216), (10241, 9216), (9216, 8191), (9216, 10241)):
            _, sample = captured(x, y)
            self.assertFalse(sample["sampled_player_xy_inside_authored_floor"])
            self.assertTrue(any("[8192,10240] x [8192,10240]" in text for text in sample["attention"]))
            self.assertTrue(sample["payload_valid"])
            self.assertFalse(sample["runtime_pass"])
        reader = base.fixture(); reader.set("player_xyz", (10000, 10000, 9216))
        old = observer.capture(reader, "still-starter")
        self.assertFalse(old["sampled_player_xy_inside_authored_floor"])
        self.assertEqual(base.BIN_SHA, old["expected_candidate_sha256_annotation"])
        self.assertTrue(captured(10000, 10000)[1]["sampled_player_xy_inside_authored_floor"])
        # Equal RAM anchors cannot distinguish these source bodies by themselves.
        self.assertFalse(old["candidate_authenticated"])
        self.assertFalse(captured(10000, 10000)[1]["candidate_authenticated"])

    def test_unknown_or_caller_metadata_profile_refuses_before_any_read(self):
        for profile in (None, True, {}, {"candidate_authenticated": True}, "edited", "starter", "Edited-landing-track02-r01"):
            reader = base.fixture()
            with self.subTest(profile=profile), self.assertRaises(ValueError):
                observer.capture(reader, "bad-profile", profile=profile)
            self.assertEqual([], reader.reads)

    def test_edited_does_not_relax_any_baseline_or_reread_gate(self):
        for name, (_, _, value) in base.GATES.items():
            reader = base.fixture(); reader.set(name, value ^ 1)
            sample = observer.capture(reader, "bad-gate", profile=EDITED)
            self.assertFalse(sample["payload_valid"])
            self.assertEqual([], base.heap_reads(reader))
        for name in tuple(base.GATES) + tuple(base.CURSORS):
            at, _, value = base.ALL_FIELDS[name]
            reader = base.fixture(); reader.flip_on_second_read[at] = value ^ 1
            sample = observer.capture(reader, "changed-gate", profile=EDITED)
            self.assertFalse(sample["payload_valid"])
            self.assertNotIn("dynamic_rows", sample)
            self.assertFalse(sample["ui_result_approved"])

    def test_explicit_profile_cli_and_connect_opt_in_are_both_required(self):
        for args in (["--profile", EDITED], ["--profile", "unknown", "--connect-identified-test-session"]):
            with patch.object(sys, "argv", ["observer", *args]), patch.object(socket, "create_connection") as connect:
                with self.assertRaises(SystemExit): observer.main()
                connect.assert_not_called()
        # Mock only: proves parsing reaches the same fixed endpoint, never a real connection.
        with patch.object(sys, "argv", ["observer", "--profile", EDITED, "--connect-identified-test-session"]), patch.object(
                socket, "create_connection", side_effect=RuntimeError("mock connection boundary")) as connect:
            with self.assertRaisesRegex(RuntimeError, "mock connection boundary"): observer.main()
            connect.assert_called_once_with(("127.0.0.1", 2345), timeout=2)


if __name__ == "__main__":
    unittest.main()
