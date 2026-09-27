"""Source-free authored observer refusals; no process, socket, ROM or card I/O."""
import hashlib
import io
import importlib.util
from pathlib import Path
import struct
import sys
from types import SimpleNamespace
import unittest
from unittest.mock import patch

sys.dont_write_bytecode = True
_spec = importlib.util.spec_from_file_location("egg_observer_controls", Path(__file__).with_name("test_japanese_egg_observer.py"))
controls = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(controls)
observer = controls.observer
ROWS = (0x801743D0, 0x80174428)
PRIVATE = (0x80178420, 0x8017842C)
RGB = ((0x801651B0, 0xFF332211), (0x801652C8, 0x00665544),
       (0x801659B8, 0xFFCCBBAA), (0x80165AD0, 0x00FFEEDD))
GUARD = struct.pack("<13I", 0x2463FF0B, 0x10600009, 0x246300F5, 0x2463FE17,
                    0x10600006, 0x246301E9, 0x2463FE0E, 0x10600003, 0x246301F2,
                    0x080215E0, 0, 0x080222EB, 0)
IMAGE = "/private/test/artisans-native-japanese-eggs-authored-colors-r01.bin"
EXE = "/Applications/DuckStation.app/Contents/MacOS/DuckStation"


def fixture():
    memory = controls.fixture()
    for address, value in ((0x800785E4, 0x80166554), (0x80078600, 0x1CC000),
                           (0x80075828, 0x80170800), (0x80075890, 0x80174480),
                           (0x80076B1C, 0x80164D54), (0x80076B40, 0x8016555C)):
        memory.word(address, value)
    for i, (row_at, private, cls) in enumerate(zip(ROWS, PRIVATE, (0x1E9, 0x1F2))):
        row = bytearray(88)
        struct.pack_into("<I", row, 0, private)
        struct.pack_into("<iii", row, 12, 72704, 47104 + 2048 * i, 6656)
        struct.pack_into("<H", row, 0x36, cls)
        row[0x4B], row[0x4F] = 0x94, 1
        memory.map(row_at, row); memory.map(private, bytes(12))
    for address, word in RGB:
        memory.map(address, struct.pack("<I", word) * 70)
    memory.map(0x80088C44, GUARD)
    for address in (0x8007DBD4, 0x8007DCFC):
        memory.map(address, struct.pack("<II", 0x08022311, 0))
    return memory


class AuthoredEggObserverTests(unittest.TestCase):
    def sample(self, memory):
        return observer.capture(memory, "offline authored", authored_colors=True)

    def test_legacy_default_unchanged(self):
        old = observer.capture(controls.fixture(), "legacy")
        self.assertEqual(observer.PROFILE, old["profile_annotation"])
        self.assertNotIn("expected_candidate_sha256_annotation", old)
        self.assertEqual([0x80172BD0, 0x80172C28], [r["address"] for r in old["eggs"]])

    def test_authored_exact_roots_rows_rgb_and_guard(self):
        memory = fixture(); result = self.sample(memory)
        self.assertNotIn("actor_sample_refused", result)
        self.assertEqual([0x1E9, 0x1F2], [r["class_id"] for r in result["eggs"]])
        self.assertEqual(list(ROWS), [r["address"] for r in result["eggs"]])
        self.assertEqual("8d99201784ec1c6be4219c98ac72e148258cb13a7a9b539d5937af83a9b4c1ff", result["expected_candidate_sha256_annotation"])
        self.assertEqual([hashlib.sha256(struct.pack("<I", word) * 70).hexdigest() for _, word in RGB],
                         [c["sha256"] for c in result["authored_model_color_arrays"]])
        self.assertEqual("d2a588147538413f2dc00efce873440ed964e7ec8f8e214ed2c508908c4db276", result["authored_guard_sha256"])
        for flag in ("candidate_authenticated", "runtime_pass", "pickup_verified", "save_verified"):
            self.assertIs(False, result[flag])
        for old in (*observer.ROWS, *observer.PRIVATE, *observer.COLORS):
            self.assertFalse(any(at == old for at, _ in memory.reads))
        self.assertTrue(all(0x80000000 <= at <= 0x80200000 - n and 1 <= n <= 1024 for at, n in memory.reads))
        allowed = {(at, struct.calcsize(fmt)) for at, fmt in observer.FIELDS.values()}
        allowed |= {(0x80076B1C, 4), (0x80076B40, 4), (0x80088C44, 0x34), (0x8007DBD4, 8), (0x8007DCFC, 8)}
        allowed |= {(r, 88) for r in ROWS} | {(p, 12) for p in PRIVATE} | {(a, 0x118) for a, _ in RGB}
        self.assertTrue(set(memory.reads) <= allowed)
        for extent in (*[(r, 88) for r in ROWS], *[(a, 0x118) for a, _ in RGB],
                       (0x80088C44, 0x34), (0x8007DBD4, 8), (0x8007DCFC, 8)):
            self.assertEqual(2, memory.reads.count(extent))

    def test_cross_profile_refused_before_rows(self):
        for memory, authored in ((controls.fixture(), True), (fixture(), False)):
            if authored:
                memory.word(0x80076B1C, 0); memory.word(0x80076B40, 0)
            result = observer.capture(memory, "wrong", authored_colors=authored)
            self.assertIn("actor_sample_refused", result)
            self.assertTrue(all(n < 88 for _, n in memory.reads))

    def test_closed_profile_not_caller_layout(self):
        for value in (1, "authored", {}, None):
            memory = fixture()
            with self.assertRaises(ValueError):
                observer.capture(memory, "invalid profile", authored_colors=value)
            self.assertEqual([], memory.reads)

    def test_each_root_refuses_before_any_actor_read(self):
        for address in (0x8007674C, 0x80076B1C, 0x80076B40):
            for value in (0, 0x80163D54, 0x80164D54, 0x8016555C, 0xFFFFFFFF):
                memory = fixture()
                if struct.unpack("<I", bytes(memory.data[address+i] for i in range(4)))[0] == value:
                    continue
                memory.word(address, value)
                self.assertIn("actor_sample_refused", self.sample(memory))
                self.assertTrue(all(n < 88 for _, n in memory.reads))

    def test_wrong_row_class_or_private_never_chased(self):
        for index, row in enumerate(ROWS):
            for offset, data in ((0x36, struct.pack("<H", 0xF5)), (0x36, struct.pack("<H", 0x1F2 if index == 0 else 0x1E9)),
                                 (0, struct.pack("<I", observer.PRIVATE[index])), (0, struct.pack("<I", 0xFFFFFFFF))):
                memory = fixture(); memory.map(row + offset, data)
                self.assertIn("actor_sample_refused", self.sample(memory))
                self.assertFalse(any(at in PRIVATE or at in dict(RGB) for at, _ in memory.reads))

    def test_all_palette_words_and_code_bytes_are_bound(self):
        for address, _ in RGB:
            for offset in (0, 3, 4 * 35, 0x117):
                memory = fixture(); memory.data[address + offset] ^= 1
                result = self.sample(memory)
                self.assertIn("RGB", result["actor_sample_refused"])
                self.assertNotIn("eggs", result)

    def test_guard_and_both_leaves_are_bound(self):
        for address, length in ((0x80088C44, 0x34), (0x8007DBD4, 8), (0x8007DCFC, 8)):
            for offset in (0, length - 1):
                memory = fixture(); memory.data[address + offset] ^= 1
                self.assertIn("guard", self.sample(memory)["actor_sample_refused"])

    def test_truncation_refused_and_no_results_retained(self):
        for extent in ((*[(r, 88) for r in ROWS], *[(p, 12) for p in PRIVATE],
                        *[(a, 0x118) for a, _ in RGB], (0x80088C44, 0x34))):
            memory = fixture(); memory.short = extent
            result = self.sample(memory)
            self.assertIn("actor_sample_refused", result)
            self.assertNotIn("eggs", result)

    def test_reread_root_row_rgb_and_guard_transition_refused(self):
        for address, value in ((0x80076B40, 0x80164D54), (ROWS[1], 0xFFFFFFFF),
                               (RGB[0][0], 0xFF332210), (0x80088C44, 0)):
            memory = fixture(); memory.flip = (address, value)
            result = self.sample(memory)
            self.assertIn("actor_sample_refused", result)
            self.assertNotIn("eggs", result)

    def test_new_cli_requires_identity_before_socket(self):
        for extra in ([], ["--pid", "123"], ["--candidate-bin", IMAGE]):
            with patch.object(sys, "argv", ["observer", "--connect-identified-test-session", "--authored-colors", *extra]), \
                 patch.object(observer.socket, "create_connection") as connect:
                with self.assertRaises(SystemExit): observer.main()
                connect.assert_not_called()

    def test_process_requires_exact_executable_listener_and_open_inode(self):
        process = "Thu Sep 25 10:00:00 2026 " + EXE + "\n"
        files = "p123\nftxt\ntREG\ni111\nn" + EXE + "\nf4r\ntREG\ni42\nD0x7\nn" + IMAGE + "\n"
        listener = "p123\nf5u\nn127.0.0.1:2345\n"
        cases = ((process, files, listener), (process.replace("DuckStation.app", "Other.app"), files, listener),
                 (process, files.replace("i42", "i43"), listener), (process, files.replace(IMAGE, IMAGE + ".old"), listener),
                 (process, files, listener.replace("p123", "p124")), (process, files, listener.replace("127.0.0.1", "*")),
                 (process, files.replace("D0x7", "D0x8"), listener))
        for i, outputs in enumerate(cases):
            with patch.object(observer, "_command", side_effect=outputs):
                if i == 0: self.assertEqual(process.strip(), observer._require_process(123, Path(IMAGE), 42, 7))
                else:
                    with self.assertRaises(ValueError): observer._require_process(123, Path(IMAGE), 42, 7)

    def test_held_file_hashes_entry_and_close_not_each_sample(self):
        with held_file() as (session, opened, hashed, _, _):
            with session:
                session.require_fresh(); session.require_fresh()
                self.assertEqual(1, hashed.call_count)
            self.assertEqual(2, hashed.call_count)
            opened.assert_called_once_with(Path(IMAGE), observer.os.O_RDONLY | observer.os.O_NOFOLLOW)
            self.assertTrue(session.file.closed)

    def test_file_identity_hash_size_symlink_and_pid_reuse_refuse(self):
        with held_file() as (session, _, hashed, _, _):
            hashed.return_value.hexdigest = lambda: "0" * 64
            with self.assertRaisesRegex(ValueError, "full-image hash"): session.__enter__()
            self.assertTrue(session.file.closed)
        with held_file() as (session, _, _, info, _):
            info.st_size -= 1
            with self.assertRaisesRegex(ValueError, "extent"): session.__enter__()
        with held_file() as (session, opened, _, _, _), patch.object(Path, "is_symlink", return_value=True):
            with self.assertRaisesRegex(ValueError, "symbolic link"): session.__enter__()
            opened.assert_not_called()
        with held_file() as (session, _, _, _, process):
            with session:
                process.return_value = "same path, different start time"
                with self.assertRaisesRegex(ValueError, "start time"): session.require_fresh()
        with held_file() as (session, _, _, _, _):
            session.__enter__()
            try:
                different = SimpleNamespace(st_dev=7, st_ino=99, st_mode=0o100444,
                    st_size=661547040, st_mtime_ns=1, st_ctime_ns=2)
                with patch.object(Path, "lstat", return_value=different):
                    with self.assertRaisesRegex(ValueError, "file identity"): session.require_fresh()
                    with self.assertRaisesRegex(ValueError, "file identity"): session.__exit__(None, None, None)
                self.assertTrue(session.file.closed)
            finally: session.file.close()

    def test_final_rehash_runs_even_after_sample_failure(self):
        with held_file() as (session, _, hashed, _, _):
            with self.assertRaisesRegex(RuntimeError, "sample failed"):
                with session: raise RuntimeError("sample failed")
            self.assertEqual(2, hashed.call_count)
            self.assertTrue(session.file.closed)
        with held_file() as (session, _, hashed, _, _):
            with self.assertRaisesRegex(ValueError, "full-image hash"):
                with session: hashed.return_value.hexdigest = lambda: "f" * 64
            self.assertTrue(session.file.closed)

    def test_no_unowned_process_or_bad_name_can_reach_socket(self):
        with patch.object(observer, "AuthoredSession") as session, \
             patch.object(observer.socket, "create_connection") as connect, \
             patch.object(sys, "argv", ["observer", "--connect-identified-test-session", "--authored-colors", "--pid", "123", "--candidate-bin", IMAGE]):
            session.return_value.__enter__.side_effect = ValueError("unowned process")
            with self.assertRaisesRegex(ValueError, "unowned"): observer.main()
            connect.assert_not_called()
        with patch.object(observer.os, "open") as opened:
            with self.assertRaisesRegex(ValueError, "filename"):
                observer.AuthoredSession("/private/test/other.bin", 123)
            opened.assert_not_called()


class FakeHeld(io.BytesIO):
    def fileno(self): return 27


from contextlib import contextmanager


@contextmanager
def held_file():
    """Mock only OS evidence; no synthetic661MB file or actual process exists."""
    info = SimpleNamespace(st_dev=7, st_ino=42, st_mode=0o100444,
        st_size=661547040, st_mtime_ns=1, st_ctime_ns=2)
    with patch.object(observer.os, "open", return_value=27) as opened, \
         patch.object(observer.os, "fdopen", return_value=FakeHeld(b"fixture")), \
         patch.object(observer.os, "fstat", return_value=info), \
         patch.object(Path, "lstat", return_value=info), \
         patch.object(Path, "is_symlink", return_value=False), \
         patch.object(observer, "_require_process", return_value="exact process start signature") as process, \
         patch.object(observer.hashlib, "file_digest", return_value=SimpleNamespace(hexdigest=lambda: observer.AUTHORED_IMAGE_SHA256)) as hashed:
        yield observer.AuthoredSession(IMAGE, 123), opened, hashed, info, process


if __name__ == "__main__":
    unittest.main()
