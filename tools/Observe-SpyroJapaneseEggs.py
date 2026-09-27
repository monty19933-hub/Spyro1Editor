#!/usr/bin/env python3
"""Manual read-only samples for the two closed USA Artisans egg experiments.

Identify the actual DuckStation PID and open candidate BIN externally first.
Connect only to that isolated session. Initial connection can pause execution;
pause/resume and all gameplay input stay in the UI. These sequential RAM reads
are not atomic snapshots or a runtime pass. No sampled pointer becomes a read
address; no card, settings, file, execution or memory writes are performed.
"""
from __future__ import annotations

import argparse
from contextlib import nullcontext
from datetime import datetime, timezone
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import socket
import stat
import struct
import subprocess
import sys
from types import MappingProxyType

sys.dont_write_bytecode = True
_spec = importlib.util.spec_from_file_location(
    "japanese_egg_read_primitives", Path(__file__).with_name("Observe-SpyroNativeWizard.py"))
wizard = importlib.util.module_from_spec(_spec)
sys.modules[_spec.name] = wizard
_spec.loader.exec_module(wizard)
ReadOnlyClient = wizard.ReadOnlyClient

PROFILE = "native-artisans-two-japanese-eggs-visual-research-v1"
# Independently derived from the actual source-issued model/scene append.
# Static bounds, NOT offsets supplied by a receipt or observed RAM pointers.
SCENE, ROOT = 0x80164D54, 0x80163D54
ROWS = (0x80172BD0, 0x80172C28)
PRIVATE = (0x80176C20, 0x80176C2C)
COLORS = (0x801641B0, 0x801642C8)
GATES = MappingProxyType({
    "level": (0x8007596C, "<I", 10),
    "game_state": (0x800757D8, "<I", 0),
    "normal_classifier": (0x80075690, "<I", 0),
    "copy_anchor": (0x800785D8, "<I", 0x80089238),
    "model_anchor": (0x800785E0, "<I", 0x80116554),
    "scene_anchor": (0x800785E4, "<I", SCENE),
    "scene_disk_offset": (0x80078600, "<I", 0x1CA800),
    "scene_disk_length": (0x80078604, "<I", 0x14800),
    "moby_base": (0x80075828, "<I", 0x8016F000),
    "dynamic_base": (0x80075890, "<I", 0x80172C80),
    "dynamic_capacity": (0x800756A8, "<I", 89),
    "egg_model_root": (0x8007674C, "<I", ROOT),
})
FIELDS = MappingProxyType({name: (address, fmt) for name, (address, fmt, _) in GATES.items()} | {
    name: wizard.FIELDS[name] for name in (
        "lives", "player_xyz", "player_heading_u8", "camera_control_angle_s16",
        "player_desired_bearing_u32", "player_state", "player_substate",
        "primitive_cursor", "primitive_limit", "renderer_exhaustion_flag", "dynamic_count")
})

AUTHORED_PROFILE = "native-artisans-two-japanese-eggs-authored-colors-research-v1"
AUTHORED_IMAGE_NAME = "artisans-native-japanese-eggs-authored-colors-r01.bin"
AUTHORED_IMAGE_SHA256 = "8d99201784ec1c6be4219c98ac72e148258cb13a7a9b539d5937af83a9b4c1ff"
AUTHORED_IMAGE_SIZE = 661547040
DUCKSTATION_EXE = "/Applications/DuckStation.app/Contents/MacOS/DuckStation"
# Actual final model length50000, Scene[80166554,8017AD54). These are a
# separate closed layout, not arithmetic authorized by annotations or RAM.
AUTHORED_ROWS = (0x801743D0, 0x80174428)
AUTHORED_PRIVATE = (0x80178420, 0x8017842C)
AUTHORED_GATES = MappingProxyType(dict(GATES) | {
    "scene_anchor": (0x800785E4, "<I", 0x80166554),
    "scene_disk_offset": (0x80078600, "<I", 0x1CC000),
    "moby_base": (0x80075828, "<I", 0x80170800),
    "dynamic_base": (0x80075890, "<I", 0x80174480),
    "authored_a_model_root": (0x80076B1C, "<I", 0x80164D54),
    "authored_b_model_root": (0x80076B40, "<I", 0x8016555C),
})
AUTHORED_FIELDS = MappingProxyType(dict(FIELDS) | {
    name: (address, fmt) for name, (address, fmt, _) in AUTHORED_GATES.items()})
AUTHORED_COLORS = (
    (0x801651B0, 0xFF332211, "a65b6a4aed6a00287a0ed987b3519b272a42c5764f3ab46e364637e9bbb828e6"),
    (0x801652C8, 0x00665544, "466009e893bb9219bd6bda5d161bf859afe08175e69b317ed0c8ff407557771e"),
    (0x801659B8, 0xFFCCBBAA, "2a470808d975fafdfb7cf892aa13e5db5b969b1db0c098bd8b50f5f8e39be4ad"),
    (0x80165AD0, 0x00FFEEDD, "99f97b63a9e3c93c073ae7a7ccd2e2f57e29522383e2ae865a7db20a40071829"),
)
AUTHORED_CODE = (
    (0x80088C44, struct.pack("<13I", 0x2463FF0B, 0x10600009, 0x246300F5,
        0x2463FE17, 0x10600006, 0x246301E9, 0x2463FE0E, 0x10600003,
        0x246301F2, 0x080215E0, 0, 0x080222EB, 0)),
    (0x8007DBD4, struct.pack("<II", 0x08022311, 0)),
    (0x8007DCFC, struct.pack("<II", 0x08022311, 0)),
)


def _profile(authored_colors):
    if type(authored_colors) is not bool:
        raise ValueError("Only the two closed observer profiles are supported.")
    return (AUTHORED_GATES, AUTHORED_FIELDS, AUTHORED_ROWS, AUTHORED_PRIVATE, (0x1E9, 0x1F2)) if authored_colors else (
        GATES, FIELDS, ROWS, PRIVATE, (0xF5, 0xF5))


def read_fields(client, names, fields=FIELDS):
    result = {}
    for name in names:
        address, fmt = fields[name]
        value = struct.unpack(fmt, client.read(address, struct.calcsize(fmt)))
        result[name] = value[0] if len(value) == 1 else list(value)
    return result


def require_layout(values, gates=GATES):
    for name, (_, _, expected) in gates.items():
        if values[name] != expected:
            raise ValueError("Wrong/uninitialized fixed egg layout: " + name)
    if not 0 <= values["dynamic_count"] <= 89:
        raise ValueError("Dynamic count exceeds the exact native arena capacity.")


def require_row(row, index, private=PRIVATE, classes=(0xF5, 0xF5)):
    if len(row) != 0x58:
        raise ValueError("Partial egg row.")
    if struct.unpack_from("<H", row, 0x36)[0] != classes[index]:
        raise ValueError("Appended actor is not F5." if classes == (0xF5, 0xF5) else
                         "Appended actor has a different fixed profile class.")
    if struct.unpack_from("<I", row)[0] != private[index]:
        raise ValueError("Unrelocated or different private owner; no pointer chase.")
    if row[0x4B] != 0x94 or row[0x4F] != 1 or row[0x48] != 0:
        raise ValueError("Different format/mode/live state; visual profile not initialized.")
    if struct.unpack_from("<ii", row, 12) != (72704, 47104 + index * 2048):
        raise ValueError("Actor is not at its fixed source-inspected XY site.")


def _authored_bytes(client):
    colors = []
    for address, word, digest in AUTHORED_COLORS:
        data = client.read(address, 0x118)
        if data != struct.pack("<I", word) * 70 or hashlib.sha256(data).hexdigest() != digest:
            raise ValueError("Authored RGB/CODE owner differs from the exact diagnostic array.")
        colors.append(data)
    for address, expected in AUTHORED_CODE:
        if client.read(address, len(expected)) != expected:
            raise ValueError("Authored guard/default-leaf owner differs.")
    return colors


def capture(client, label, *, authored_colors=False):
    gates, fields, row_addresses, private, classes = _profile(authored_colors)
    result = {"utc": datetime.now(timezone.utc).isoformat(), "label": label[:120],
              "profile_annotation": PROFILE, "sequential_reads_not_atomic": True,
              "candidate_authenticated": False, "runtime_pass": False,
              "pickup_verified": False, "save_verified": False}
    if authored_colors:
        result.update(profile_annotation=AUTHORED_PROFILE,
                      expected_candidate_sha256_annotation=AUTHORED_IMAGE_SHA256,
                      expected_candidate_filename_annotation=AUTHORED_IMAGE_NAME)
    try:
        result.update(read_fields(client, fields, fields))
        require_layout(result, gates)
        rows = [client.read(address, 0x58) for address in row_addresses]
        for index, row in enumerate(rows):
            require_row(row, index, private, classes)
        eggs = []
        for index, row in enumerate(rows):
            observed = wizard.decode_moby(row, 174 + index, row_addresses[index])
            observed["yaw_u8"] = row[0x46]
            observed["visible_raw_u8"] = row[0x51]
            observed["private_words_raw"] = list(struct.unpack("<III", client.read(private[index], 12)))
            eggs.append(observed)
        colors = _authored_bytes(client) if authored_colors else [client.read(address, 0x118) for address in COLORS]
        # Re-read all layout gates and both rows' identity, not mutable animation
        # or bob fields. This detects known transitions, not every possible race.
        after = read_fields(client, tuple(gates) + ("dynamic_count",), fields)
        require_layout(after, gates)
        if any(result[name] != after[name] for name in after):
            raise ValueError("Layout/capacity state changed during sequential sample.")
        for index, address in enumerate(row_addresses):
            require_row(client.read(address, 0x58), index, private, classes)
        if authored_colors:
            _authored_bytes(client)
        result["eggs"] = eggs
        result["authored_model_color_arrays" if authored_colors else "native_model_color_arrays"] = [
            {"role": role, "address": address, "sha256": hashlib.sha256(data).hexdigest(),
             "first_packed_word": struct.unpack_from("<I", data)[0], "words": 70}
            for role, address, data in zip(("a-near", "a-far", "b-near", "b-far") if authored_colors else ("near", "far"),
                tuple(c[0] for c in AUTHORED_COLORS) if authored_colors else COLORS, colors)]
        if authored_colors:
            result["authored_guard_sha256"] = hashlib.sha256(AUTHORED_CODE[0][1]).hexdigest()
        result["primitive_sampled_slack"], result["primitive_slack_reason"] = wizard.primitive_slack(
            result["primitive_cursor"], result["primitive_limit"])
        if result["renderer_exhaustion_flag"]:
            result["attention"] = "Pause in UI: renderer exhaustion flag observed."
    except (ValueError, struct.error) as error:
        result["actor_sample_refused"] = str(error)
        result["attention"] = "Pause in UI; sample is not a layout/gameplay pass."
    return result


def _command(args):
    try:
        return subprocess.run(args, check=True, capture_output=True, text=True, timeout=3).stdout
    except (OSError, subprocess.SubprocessError) as error:
        raise ValueError("Cannot establish exact read-only process ownership.") from error


def _lsof_records(output, pid):
    records, row = [], None
    for line in output.splitlines():
        if line.startswith("p"):
            if line != "p" + str(pid): raise ValueError("Different process owns candidate/listener.")
        elif line.startswith("f"):
            row = {"f": line[1:]}; records.append(row)
        elif row is not None and line:
            row[line[0]] = line[1:]
    if output.splitlines()[:1] != ["p" + str(pid)]:
        raise ValueError("Missing exact process owner.")
    return records


def _require_process(pid, path, inode, device):
    if type(pid) is not int or pid <= 0: raise ValueError("An exact positive DuckStation PID is required.")
    # On macOS comm is width-truncated unless it is the final ps column.
    # lstart plus the full executable rejects PID reuse without relying on the
    # user's relative CUE argv or accepting an executable-path override.
    signature = _command(["/bin/ps", "-ww", "-p", str(pid), "-o", "lstart=", "-o", "comm="]).strip()
    if "\n" in signature or not signature.endswith(" " + DUCKSTATION_EXE):
        raise ValueError("Process executable is not the fixed DuckStation.app binary.")
    files = _lsof_records(_command(["/usr/sbin/lsof", "-a", "-p", str(pid), "-FpnfitD"]), pid)
    if not any(r.get("f") == "txt" and r.get("n") == DUCKSTATION_EXE for r in files) or not any(
            r.get("t") == "REG" and r.get("n") == str(path) and r.get("i") == str(inode) and
            int(r.get("D", "-1"), 16) == device for r in files):
        raise ValueError("PID does not hold the exact named candidate inode and executable.")
    listeners = _lsof_records(_command(["/usr/sbin/lsof", "-nP", "-a", "-p", str(pid),
        "-iTCP:2345", "-sTCP:LISTEN", "-Fpn"]), pid)
    if not listeners or any(r.get("n") != "127.0.0.1:2345" for r in listeners):
        raise ValueError("PID does not own only the exact loopback2345 listener.")
    return signature


def _identity(value):
    return (value.st_dev, value.st_ino, value.st_mode, value.st_size, value.st_mtime_ns, value.st_ctime_ns)


class FixedCandidateSession:
    """Held-file and named-process evidence, not atomic loaded-disc/GPU proof.

    No caller hash, receipt or override can authorize a different candidate.
    The fixed file is hashed on entry AND close; stable identity plus fresh
    ps/lsof evidence is checked around each sequential sample. No CPU control.
    """
    # Closed subclasses supply these bindings; no CLI hash/profile override.
    image_name = None
    image_sha256 = None
    image_size = None

    def __init__(self, candidate, pid):
        self.path, self.pid, self.file = Path(os.path.abspath(candidate)), pid, None
        if self.path.name != self.image_name or "\n" in str(self.path):
            raise ValueError("Only the exact closed candidate filename is admitted.")
        if (type(self.image_sha256) is not str or len(self.image_sha256) != 64 or
                any(c not in "0123456789abcdef" for c in self.image_sha256) or
                type(self.image_size) is not int or self.image_size <= 0):
            raise ValueError("Closed candidate identity has not been issued.")

    def _require_file(self):
        if any(p.is_symlink() for p in (self.path, *self.path.parents)):
            raise ValueError("Candidate path may not contain a substituted symbolic link.")
        if _identity(self.path.lstat()) != self.identity or _identity(os.fstat(self.file.fileno())) != self.identity:
            raise ValueError("Held/named candidate file identity changed.")

    def _hash(self):
        self.file.seek(0)
        digest = hashlib.file_digest(self.file, "sha256").hexdigest()
        self._require_file()
        if digest != self.image_sha256:
            raise ValueError("Candidate is not the exact closed full-image hash.")

    def __enter__(self):
        if any(p.is_symlink() for p in (self.path, *self.path.parents)):
            raise ValueError("Candidate path may not contain a symbolic link.")
        fd = os.open(self.path, os.O_RDONLY | os.O_NOFOLLOW)
        self.file = os.fdopen(fd, "rb")
        try:
            info = os.fstat(self.file.fileno()); self.identity = _identity(info)
            if not stat.S_ISREG(info.st_mode) or info.st_size != self.image_size:
                raise ValueError("Candidate has a different complete image extent.")
            self._hash()
            self.signature = _require_process(self.pid, self.path, info.st_ino, info.st_dev)
            self.require_fresh()
            return self
        except BaseException:
            self.file.close(); raise

    def require_fresh(self):
        self._require_file()
        if _require_process(self.pid, self.path, self.identity[1], self.identity[0]) != self.signature:
            raise ValueError("DuckStation process identity/start time changed.")
        self._require_file()

    def __exit__(self, *_):
        try:
            self._hash()
        finally:
            self.file.close()


class AuthoredSession(FixedCandidateSession):
    image_name = AUTHORED_IMAGE_NAME
    image_sha256 = AUTHORED_IMAGE_SHA256
    image_size = AUTHORED_IMAGE_SIZE


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--connect-identified-test-session", action="store_true")
    parser.add_argument("--authored-colors", action="store_true", help="Separate fixed diagnostic-color profile; legacy remains default.")
    parser.add_argument("--pid", type=int, help="Exact isolated DuckStation PID, required only for authored colors.")
    parser.add_argument("--candidate-bin", help="Exact authored BIN held by that PID; no receipt/hash override.")
    args = parser.parse_args()
    if not args.connect_identified_test_session:
        parser.error("Identify the exact isolated PID/open BIN before explicitly opting in.")
    if args.authored_colors and (args.pid is None or args.pid <= 0 or not args.candidate_bin):
        parser.error("Authored colors require the exact --pid and --candidate-bin before any connection.")
    if not args.authored_colors and (args.pid is not None or args.candidate_bin is not None):
        parser.error("PID/BIN ownership parameters belong only to the explicit authored-color profile.")
    with AuthoredSession(args.candidate_bin, args.pid) if args.authored_colors else nullcontext() as owned:
        with socket.create_connection(("127.0.0.1", 2345), timeout=2) as connection:
            if owned: owned.require_fresh()
            client = ReadOnlyClient(connection)
            print("READ_ONLY_JP_EGGS_CONNECTED; p <label> samples, q closes. UI-only input/resume.", flush=True)
            for line in sys.stdin:
                command = line.strip()
                if command == "q":
                    break
                if command == "p" or command.startswith("p "):
                    if owned: owned.require_fresh()
                    result = capture(client, command[1:].strip(), authored_colors=args.authored_colors)
                    if owned:
                        owned.require_fresh()
                        result.update(process_candidate_ownership_checked=True, process_id=owned.pid,
                                      candidate_path=str(owned.path), loaded_disc_atomic_proof=False)
                    print(json.dumps(result), flush=True)


if __name__ == "__main__":
    main()
