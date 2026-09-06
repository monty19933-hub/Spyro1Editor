"""Bounded native scalar fixtures for the proposed fresh-ID65 observer.

No production observer import, socket, CPU/GTE/hardware or gameplay acceptance.
Optional --source binds the complete original BIN/EXE and runs the actual generic
relocator over the complete source companion package. Default synthetic package
checks only the zero-animation path; it is not the source-package result.
"""
import argparse
import hashlib
import importlib.util
import json
from pathlib import Path
import struct
import sys
import unittest

sys.dont_write_bytecode = True
SOURCE = None
EDITED_CANDIDATE = None
BIN_SHA = "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37"
EXE_SHA = "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998"
PACKAGE_SHA = "8e0d1cf58e8b164734f4aa3e91a60a1d04e78bb1f5e4cd5c4432b2e3e1e743f1"
PINS = {
    "authored_spawn": (0x8001377C, "6b7d387efa46a06550a5784b4e9329337b763718e08e5440e05b87012ba01e66",
        "0000028e0780033cd056638c0780013cd87822ac0400028e0780013cdc7822ac0800028e0780013ce07822ac0e0002920780013ce47822ac35006014"),
    "registration": (0x80015ACC, "1c53991c914bb7205a9cbd9041be413c3b1248e2c035551d7a2ec6c10408008d",
        "010010240780143c786394260780133c506c732602017226040071260000238e00000000100060180400312601001026c0ff648e0880023ce085428c23206400f84c000c2120440000004386000000008018030021187400000062ac4000022aeeff4014020052260880023ce085428cc4ff648eccff658ec8ff668e0780033c6458638c21104400010063240880013ce48522ac0880013c048625ac0880013c008626ac0780013c645823acef58000800000000"),
    "generic_helper": (0x800133E0, "fede3c414aea2dce0372fce574549d961123cf6456a7a291acef48b9c4af8598",
        "e8ffbd27214880000000228d000000000e00410421402001ff7f023cffff4234241022010400238d0c00248d21186900040023ad0800238d212089000c0024ad21186900904d0008080023ad21580000ff7f023cffff423424202201211820011400628c000000000200401021108200140062ac01006b2508006229f8ff4014040063243400028d215800000000038d2110220171006018340002ad1f000c3cffff8c35e0ff0d3c213800013800e38cffff02246400621000000000212023013800e4ac3400028d1400838c0000000021104300140082ac3800e48c3400028d1800838c0000000021104300180082ac3800e48c000000001c00838c000000000c006010000000003400028d00000000211043001c0082ac3800e48c3400028d2000838c0000000021104300200082ac3800e48c00000000040082900000000021004010000000003400028d1000838c0000000021104300100082ac3800e28c21300000000043840000000036006018240045242150e0000000a38c0000000024206d000000a4ac3400028d24186c00211062004310020024184c00212083000000a4ac3800428d0100c62400004284000000002a10c200efff40140400a5248a4d0008000000002400852400008284000000001c004018213000002120e0003400028d0000a38c24104c00211862000000a3ac0400a5240000a38c00000000ffff623009004010000000003400028d00000000231045000400422483100200ffff4230211062000000a2ac3800828c0100c62400004284000000002a10c200e7ff40140400a5240000028d01006b252a10620195ff40140400e724211020011800bd270800e00300000000"),
    "pool_init": (0x8001441C, "0b54dc9bc7189e4b54bb874da4b2def42e3a3a4f26090f573b782dbe81c98881",
        "0880023cf885428cff0003240780013c245822ac0780013c385722ac010043a00780033c2458638cffff0224da62010c002062ac0f00a012000000000780023c9056428c000000000a004014780004240780023ccc58428c0000000009f84000212800000780013c985822ac8051010c01000424"),
    "callbacks": (0x8005A898, "fe4669229aedbbc8ff698d57a237327af36025aaeda0ff5cc2e1c806fd23e50b",
        "0980023ccc9e42240880013cd88522ac0880023ca47242240780013ccc5822ac0880023c54da42240780013c345722ac0980023c688f42240780013ce45822ac0880023c207e42240780013cbc5622ac0880023c2caf42240780013ca05722ac0880023c68b042240780013ca85822ac0880023cacb042240780013c4c5722ac0880023c4cd042240780013cd85822acb86d010800000000"),
}
CALLBACKS = {
    0x800758CC: 0x800872A4, 0x80075734: 0x8007DA54,
    0x800758E4: 0x80088F68, 0x800756BC: 0x80087E20,
    0x800757A0: 0x8007AF2C, 0x800758A8: 0x8007B068,
    0x8007574C: 0x8007B0AC, 0x800758D8: 0x8007D04C,
}


def signed(value):
    return value - 0x100000000 if value & 0x80000000 else value


class NativeScalar:
    """Only source-pinned integer instructions; strict separate code/data owners.

    Load results retire after the following instruction. Control transfers
    execute exactly one delay instruction. No callback or omitted helper is
    synthesized. ADDU/ADDIU wrap; signed arithmetic trapping is not exercised.
    """
    def __init__(self, names):
        self.r = [0] * 32
        self.code = {}
        self.memory = {}
        self.trace = []
        self.writes = []
        for name in names:
            pc, sha, literal = PINS[name]
            code = bytes.fromhex(literal)
            if hashlib.sha256(code).hexdigest() != sha:
                raise ValueError("Source instruction fixture changed.")
            for i in range(0, len(code), 4):
                self.code[pc + i] = int.from_bytes(code[i:i + 4], "little")

    def map(self, at, data):
        if any(at + i in self.memory for i in range(len(data))):
            raise ValueError("Overlapping scalar data owner.")
        self.memory.update((at + i, b) for i, b in enumerate(data))

    def read(self, at, size, signed_value=False):
        if size in (2, 4) and at % size:
            raise ValueError("Unaligned native scalar access.")
        return int.from_bytes(bytes(self.memory[at + i] for i in range(size)),
                              "little", signed=signed_value) & 0xFFFFFFFF

    def write(self, at, value, size=4):
        if size in (2, 4) and at % size:
            raise ValueError("Unaligned native scalar store.")
        if not all(at + i in self.memory for i in range(size)):
            raise ValueError("Native store escaped scalar owner.")
        data = (value & ((1 << (8 * size)) - 1)).to_bytes(size, "little")
        self.memory.update((at + i, b) for i, b in enumerate(data))
        self.writes.append((at, size))

    def run(self, pc, stop, maximum=12000):
        branch = load = None
        for _ in range(maximum):
            if pc == stop:
                if branch is not None or load is not None:
                    raise ValueError("Stopped with unretired delay.")
                return
            if pc not in self.code:
                raise ValueError("Instruction fetch outside source pin.")
            word = self.code[pc]
            self.trace.append(pc)
            op, rs, rt, rd = word >> 26, word >> 21 & 31, word >> 16 & 31, word >> 11 & 31
            fn, shift = word & 63, word >> 6 & 31
            imm = (word & 65535) - (65536 if word & 32768 else 0)
            a, b = self.r[rs], self.r[rt]
            old_branch, old_load = branch, load
            branch = load = write = None
            if op == 0:
                if fn == 0: write = rd, b << shift
                elif fn == 3: write = rd, signed(b) >> shift
                elif fn == 8: branch = a
                elif fn == 0x21: write = rd, a + b
                elif fn == 0x23: write = rd, a - b
                elif fn == 0x24: write = rd, a & b
                elif fn == 0x2A: write = rd, int(signed(a) < signed(b))
                else: raise ValueError("Unsupported native scalar R instruction.")
            elif op == 1 and rt == 1:
                branch = pc + 4 + imm * 4 if signed(a) >= 0 else pc + 8
            elif op in (2, 3):
                branch = ((pc + 4) & 0xF0000000) | ((word & 0x3FFFFFF) << 2)
                if op == 3: write = 31, pc + 8
            elif op in (4, 5):
                branch = pc + 4 + imm * 4 if ((a == b) == (op == 4)) else pc + 8
            elif op == 6:
                branch = pc + 4 + imm * 4 if signed(a) <= 0 else pc + 8
            elif op == 9: write = rt, a + imm
            elif op == 10: write = rt, int(signed(a) < imm)
            elif op == 12: write = rt, a & (word & 65535)
            elif op == 13: write = rt, a | (word & 65535)
            elif op == 15: write = rt, (word & 65535) << 16
            elif op in (0x21, 0x23, 0x24):
                load = rt, self.read((a + imm) & 0xFFFFFFFF,
                                     2 if op == 0x21 else 4 if op == 0x23 else 1, op == 0x21)
            elif op in (0x28, 0x2B):
                self.write((a + imm) & 0xFFFFFFFF, b, 1 if op == 0x28 else 4)
            else: raise ValueError("Unsupported native scalar instruction.")
            if old_branch is not None and branch is not None:
                raise ValueError("Branch in a native delay slot.")
            if old_load is not None and old_load[0]:
                self.r[old_load[0]] = old_load[1]
            if write is not None and write[0]:
                self.r[write[0]] = write[1] & 0xFFFFFFFF
            self.r[0] = 0
            pc = old_branch if old_branch is not None else pc + 4
        raise ValueError("Native scalar finite instruction budget exhausted.")


def register_package(package):
    machine = NativeScalar(("registration", "generic_helper"))
    machine.map(0x80075000, bytes(0x4000))
    machine.map(0x800973C4, package)
    machine.r[29] = 0x801F0000
    # Exact emitted header roots and descriptor fields; root2 is zero.
    for at, value in {
        0x80076C10: 0xDF800, 0x80076C14: 0xF800,
        0x80076C18: 0xEF000, 0x80076C1C: 0x3000,
        0x80076C54: 0xEC430, 0x800785E0: 0x8008A794,
    }.items(): machine.write(at, value)
    machine.write(0x80076D52, 0x78, 2)
    machine.writes.clear()
    machine.run(0x80015ACC, 0x800163BC)
    return machine


class SourceFixtureTests(unittest.TestCase):
    def test_native_authored_spawn_xyz_and_unsigned_heading_widths(self):
        # Stops before the following branch. This is the real authored scene
        # header -> initial/checkpoint position consumer, not a moving-player gate.
        for heading in (0, 96, 255):
            machine = NativeScalar(("authored_spawn",))
            machine.map(0x80075000, bytes(0x4000))
            machine.map(0x80099F94, struct.pack("<iiiBBBB", 9216, 9216, 9216, 0xA5, 0x5A, heading, 0xEE))
            machine.r[16] = 0x80099F94
            machine.run(0x8001377C, 0x800137B4)
            self.assertEqual((9216, 9216, 9216, heading),
                             tuple(machine.read(at, 4) for at in (0x800778D8, 0x800778DC, 0x800778E0, 0x800778E4)))
            self.assertEqual([(at, 4) for at in (0x800778D8, 0x800778DC, 0x800778E0, 0x800778E4)], machine.writes)

    def test_all_pins_have_complete_exact_sha_and_instruction_owners(self):
        for pc, sha, literal in PINS.values():
            code = bytes.fromhex(literal)
            self.assertEqual(sha, hashlib.sha256(code).hexdigest())
            self.assertEqual(0, len(code) % 4)
            self.assertGreaterEqual(pc, 0x80010000)

    def test_native_registration_zero_count_fixture_is_not_actual_companion(self):
        machine = register_package(bytes(0x50))
        self.assertEqual(0x800973C4, machine.read(0x80076558, 4))
        self.assertEqual(0x80099F94, machine.read(0x800785E4, 4))
        self.assertEqual(0xEF000, machine.read(0x80078600, 4))
        self.assertEqual(0x3000, machine.read(0x80078604, 4))
        self.assertEqual(0x801F0000, machine.r[29])
        self.assertIn(0x800133E0, machine.trace)
        self.assertIn(0x80013648, machine.trace)  # Real JR delay executed.
        self.assertIn((0x80076558, 4), machine.writes)
        self.assertEqual(1, machine.trace.count(0x800133E0))

    def test_native_callback_initializer_without_following_helper(self):
        machine = NativeScalar(("callbacks",))
        machine.map(0x80075000, bytes(0x4000))
        machine.run(0x8005A898, 0x8005B6E0)
        self.assertEqual(0x80089ECC, machine.read(0x800785D8, 4))
        for at, expected in CALLBACKS.items():
            self.assertEqual(expected, machine.read(at, 4))
        self.assertEqual(0x8005A92C, machine.trace[-1])  # Jump delay, no helper body.

    def test_source_pool_terminator_is_separate_not_particle257(self):
        machine = NativeScalar(("pool_init",))
        machine.map(0x80075000, bytes(0x4000))
        machine.map(0x801C3BB8, bytes([0xA5]) * 0x2004)
        machine.write(0x800785F8, 0x801C3BB8)
        machine.run(0x8001441C, 0x80058B68)
        self.assertEqual(0x801C3BB8, machine.read(0x80075824, 4))
        self.assertEqual(0x801C3BB8, machine.read(0x80075738, 4))
        self.assertEqual(0xFF, machine.read(0x801C3BB9, 1))
        self.assertEqual(0xFFFFFFFF, machine.read(0x801C5BB8, 4))
        self.assertEqual([(0x800785F8, 4), (0x80075824, 4),
                          (0x80075738, 4), (0x801C3BB9, 1),
                          (0x801C5BB8, 4)], machine.writes)

    def test_actual_original_full_package_registration(self):
        if SOURCE is None: self.skipTest("Explicit --source required; no source fixture authority inferred.")
        with Path(SOURCE).open("rb") as stream:
            self.assertEqual(661547040, stream.seek(0, 2))
            stream.seek(0)
            self.assertEqual(BIN_SHA, hashlib.file_digest(stream, "sha256").hexdigest())
            def read(lba, offset, size):
                result = bytearray()
                while size:
                    count = min(size, 2048 - offset % 2048)
                    stream.seek((lba + offset // 2048) * 2352 + 24 + offset % 2048)
                    part = stream.read(count)
                    self.assertEqual(count, len(part))
                    result.extend(part); size -= count; offset += count
                return bytes(result)
            exe = read(53875, 0, 0x66000)
            self.assertEqual(EXE_SHA, hashlib.sha256(exe).hexdigest())
            for pc, sha, literal in PINS.values():
                expected = bytes.fromhex(literal)
                self.assertEqual(expected, exe[pc - 0x8000F800:pc - 0x8000F800 + len(expected)])
            package = read(37, 0x1367C7C, 0x2930)
            self.assertEqual(PACKAGE_SHA, hashlib.sha256(package).hexdigest())
            self.assertEqual(6, int.from_bytes(package[:4], "little"))
            for i in range(6):
                at = struct.unpack_from("<I", package, 0x38 + i * 4)[0]
                self.assertEqual(15, struct.unpack_from("<h", package, at)[0])
            machine = register_package(package)
            self.assertEqual(0x800973C4, machine.read(0x80076558, 4))
            self.assertEqual(0x80099F94, machine.read(0x800785E4, 4))
            self.assertEqual(6, machine.trace.count(0x8001348C))
            self.assertEqual(90, machine.trace.count(0x800135C0))
            self.assertEqual(0x801F0000, machine.r[29])
            self.assertTrue(all(0x800973C4 <= at < 0x800973C4 + 0x2930
                                or 0x80075000 <= at < 0x80079000 for at, _ in machine.writes))
            # Reconstruct the actual emitted body with the previously reviewed
            # independent raw oracle. This binds our literal header/runtime
            # fixture to the A/N16 body, not only to caller-supplied metadata.
            oracle_path = Path(__file__).resolve().parents[2] / "src/Spyro.Editor.NativeId65FreshDiscSmoke/source_fresh_disc_body.py"
            spec = importlib.util.spec_from_file_location("observer_raw_body_oracle", oracle_path)
            oracle = importlib.util.module_from_spec(spec)
            spec.loader.exec_module(oracle)
            body, _ = oracle.build_bodies(SOURCE)[0]
            self.assertEqual("b4abbdd09f794510d9b9341d7c7111ddd0b9068411bab30e70e7477b4f8c096c",
                             hashlib.sha256(body).hexdigest())
            self.assertEqual((0xDF800, 0xF800, 0xEF000, 0x3000), struct.unpack_from("<4I", body, 16))
            self.assertEqual((0xDF800, 0xEC430), struct.unpack_from("<2I", body, 0x50))
            self.assertEqual(0x78, struct.unpack_from("<H", body, 0x152)[0])
            # Bind the observer's XY-only walk-scope annotation to actual
            # emitted HP floor vertices/faces, not a caller-declared radius.
            data_offset, data_length = struct.unpack_from("<II", body, 8)
            self.assertEqual(0x1000, data_length)
            environment = data_offset + struct.unpack_from("<I", body, data_offset)[0]
            self.assertEqual((172, 1, 8), struct.unpack_from("<3I", body, environment))
            sector = environment + 4 + struct.unpack_from("<I", body, environment + 8)[0]
            xy, z = struct.unpack_from("<II", body, sector + 8)
            origin = ((xy >> 16) * 16, (xy & 65535) * 16, (z >> 16) * 16)
            self.assertEqual((4, 4, 2, 12), tuple(body[sector + 20:sector + 24]))
            vertices = [(origin[0] + ((word >> 21) & 2047) * 16,
                         origin[1] + ((word >> 10) & 2047) * 16,
                         origin[2] + (word & 1023) * 16)
                        for word in struct.unpack_from("<4I", body, sector + 76)]
            self.assertEqual([(8448, 8448, 8704), (9472, 8448, 8704),
                              (8448, 9472, 8704), (9472, 9472, 8704)], vertices)
            faces = []
            for index in range(2):
                word = struct.unpack_from("<I", body, sector + 124 + index * 16)[0]
                self.assertEqual(word & 255, (word >> 8) & 255)
                faces.append((word & 255, (word >> 16) & 255, (word >> 24) & 255))
            self.assertEqual([(0, 1, 2), (1, 3, 2)], faces)
            scene_offset, scene_length = struct.unpack_from("<II", body, 24)
            self.assertEqual("4a6e7b5c6c53199b8a7aa6ae0d24ac9bce32634aa259d5f49535282080641433",
                             hashlib.sha256(body[scene_offset:scene_offset + scene_length]).hexdigest())
            installed = register_package(body[0xEC430:0xEC430 + 0x2930])
            self.assertEqual(0x800973C4, installed.read(0x80076558, 4))
            self.assertEqual(90, installed.trace.count(0x800135C0))
            stream.seek(0)
            self.assertEqual(BIN_SHA, hashlib.file_digest(stream, "sha256").hexdigest())
        print("FRESH_ID65_OBSERVER_SOURCE_OK full_native_model_helper=True runtime_pass=False")

    def test_edited_body_reconstructed_from_original_preserves_layout_and_decodes_exact_floor(self):
        if SOURCE is None: self.skipTest("Explicit --source required for the edited source body.")
        body = self.edited_body_from_original()
        self.require_edited_body(body)
        scene = struct.unpack_from("<I", body, 24)[0]
        machine = NativeScalar(("authored_spawn",))
        machine.map(0x80075000, bytes(0x4000))
        machine.map(0x80099F94, body[scene:scene + 16])
        machine.r[16] = 0x80099F94
        machine.run(0x8001377C, 0x800137B4)
        self.assertEqual((9216, 9216, 9216, 96), tuple(machine.read(at, 4) for at in (0x800778D8, 0x800778DC, 0x800778E0, 0x800778E4)))
        for offset in (scene, scene + 14, 0xDE808 + 92):
            changed = bytearray(body); changed[offset] ^= 1
            with self.assertRaises(AssertionError): self.require_edited_body(changed)
        print("FRESH_ID65_EDITED_OBSERVER_SOURCE_BODY_OK source_native_spawn=True loaded_disc_authenticated=False runtime_pass=False")

    def test_exact_edited_published_artifact_and_companions_are_not_ram_authentication(self):
        if SOURCE is None or EDITED_CANDIDATE is None:
            self.skipTest("Explicit --source and --edited-candidate required for artifact readback.")
        candidate = Path(EDITED_CANDIDATE)
        self.assertEqual("native-id65-fresh-edited-landing-track02-r01.bin", candidate.name)
        cue = candidate.with_suffix(".cue")
        receipt = candidate.with_suffix(".receipt.json")
        for path in (candidate, cue, receipt):
            self.assertTrue(path.is_file()); self.assertFalse(path.is_symlink())
        with candidate.open("rb") as stream:
            self.assertEqual(661547040, stream.seek(0, 2)); stream.seek(0)
            self.assertEqual("ecc8f61cc16bf86a2f5117b327c49aede18f5c1e6a602c0e67f2ba58565aed16", hashlib.file_digest(stream, "sha256").hexdigest())
            def read(offset, size):
                result = bytearray()
                while len(result) < size:
                    at = offset + len(result); count = min(size - len(result), 2048 - at % 2048)
                    stream.seek(at // 2048 * 2352 + 24 + at % 2048)
                    data = stream.read(count); self.assertEqual(count, len(data)); result.extend(data)
                return bytes(result)
            header = read(37 * 2048, 2048)
            self.assertEqual((0x6936800, 0x1FB800), struct.unpack_from("<II", header, 80 * 8))
            actual = read(37 * 2048 + 0x6936800, 0x1FB800)
            self.assertEqual(self.edited_body_from_original(), actual)
            self.require_edited_body(actual)
            executable = read(54921 * 2048, 0x66000)
            self.assertEqual("2c1fd87e9fa8ce5c5f16be53e738405cec90c0bfc10ac9cb8cf1ce979b2e5c57", hashlib.sha256(executable).hexdigest())
            for pc, _, literal in PINS.values():
                data = bytes.fromhex(literal)
                self.assertEqual(data, executable[pc - 0x8000F800:pc - 0x8000F800 + len(data)])
            stream.seek(0)
            self.assertEqual("ecc8f61cc16bf86a2f5117b327c49aede18f5c1e6a602c0e67f2ba58565aed16", hashlib.file_digest(stream, "sha256").hexdigest())
            self.assertEqual(b"", stream.read(1)); self.assertEqual(661547040, stream.tell())
        self.assertEqual(107, cue.stat().st_size); self.assertEqual(4027, receipt.stat().st_size)
        cue_bytes, receipt_bytes = cue.read_bytes(), receipt.read_bytes()
        self.require_edited_companions(cue_bytes, receipt_bytes)
        for which in (0, 1):
            values = [cue_bytes, receipt_bytes]; changed = bytearray(values[which]); changed[-1] ^= 1; values[which] = bytes(changed)
            with self.assertRaises(AssertionError): self.require_edited_companions(*values)
        self.assertEqual(cue_bytes, cue.read_bytes()); self.assertEqual(receipt_bytes, receipt.read_bytes())
        print("FRESH_ID65_EDITED_OBSERVER_ARTIFACT_OK exact_document_image_receipt=True candidate_files_written=0 loaded_disc_authenticated=False runtime_pass=False")

    def edited_body_from_original(self):
        root = Path(__file__).resolve().parents[2]
        helpers = {
            "src/Spyro.Editor.NativeId65FreshDiscSmoke/source_fresh_disc_body.py": "e69619f391814c270c9c28bc588f432f79f8a6cd3f61301e3a1c483a8645ad07",
            "src/Spyro.Editor.NativeId65AuthoredSurfaceSmoke/source_surface_oracle.py": "bcaa7349277bc5150cd17cf3e8cebbc689b68a58e9dee21a2709841c899aa592",
            "src/Spyro.Editor.NativeId65NormalResourceSmoke/source_resource_oracle.py": "280087ec3b80e005872b6ec2fe793c9bbbbd69bde70ea6c64de234afa37ad76d",
            "src/Spyro.Editor.NativeId65NormalBodySmoke/NativeBodySourceWindows.cs": "674ac959cd1b451fc11c3945c1446059a2d09b505e03ddda27e6fbe3851594ec",
        }
        for relative, digest in helpers.items(): self.assertEqual(digest, hashlib.sha256((root / relative).read_bytes()).hexdigest())
        def load(relative, name):
            spec = importlib.util.spec_from_file_location(name, root / relative)
            module = importlib.util.module_from_spec(spec)
            previous = sys.argv
            sys.argv = [str(root / relative)]
            try:
                spec.loader.exec_module(module)
            finally:
                sys.argv = previous
            return module
        raw = load(next(iter(helpers)), "edited_observer_raw_body")
        surface = load("src/Spyro.Editor.NativeId65AuthoredSurfaceSmoke/source_surface_oracle.py", "edited_observer_raw_surface")
        baseline, _ = raw.build_bodies(SOURCE)[0]
        self.assertEqual("b4abbdd09f794510d9b9341d7c7111ddd0b9068411bab30e70e7477b4f8c096c", hashlib.sha256(baseline).hexdigest())
        body = bytearray(baseline)
        data, _, _, _, scene = struct.unpack_from("<5I", body, 8)
        old = surface.fresh(8448, 8448, 8704, 1024, 1024, 0x708050, 0x406080)
        edited = surface.fresh(8192, 8192, 8704, 2048, 2048, 0x788448, 0x64422C)
        self.assertEqual(len(old), len(edited)); self.assertEqual(old, body[data + 8:data + 8 + len(old)])
        body[data + 8:data + 8 + len(old)] = edited
        body[scene:scene + 16] = struct.pack("<iiiBBBB", 9216, 9216, 9216, 0, 0, 96, 0)
        reverted = body.copy(); reverted[data + 8:data + 8 + len(old)] = old; reverted[scene:scene + 16] = baseline[scene:scene + 16]
        self.assertEqual(baseline, reverted, "every unowned complete body byte is preserved")
        for relative, digest in helpers.items(): self.assertEqual(digest, hashlib.sha256((root / relative).read_bytes()).hexdigest())
        return bytes(body)

    def require_edited_body(self, body):
        self.assertEqual("bae43882a39dfc1604f4e6d578a354f34f99107f97faa9330600b3e01f34b15b", hashlib.sha256(body).hexdigest())
        self.assertEqual((0xDF800, 0xF800, 0xEF000, 0x3000), struct.unpack_from("<4I", body, 16))
        data, data_length = struct.unpack_from("<II", body, 8); self.assertEqual(0x1000, data_length)
        environment = data + struct.unpack_from("<I", body, data)[0]
        self.assertEqual((172, 1, 8), struct.unpack_from("<3I", body, environment))
        sector = environment + 4 + struct.unpack_from("<I", body, environment + 8)[0]
        xy, z = struct.unpack_from("<II", body, sector + 8)
        origin = ((xy >> 16) * 16, (xy & 65535) * 16, (z >> 16) * 16)
        self.assertEqual((4, 4, 2, 12), tuple(body[sector + 20:sector + 24]))
        vertices = [(origin[0] + ((word >> 21) & 2047) * 16, origin[1] + ((word >> 10) & 2047) * 16,
                     origin[2] + (word & 1023) * 16) for word in struct.unpack_from("<4I", body, sector + 76)]
        self.assertEqual([(8192, 8192, 8704), (10240, 8192, 8704), (8192, 10240, 8704), (10240, 10240, 8704)], vertices)
        faces = []
        for index in range(2):
            word = struct.unpack_from("<I", body, sector + 124 + index * 16)[0]
            self.assertEqual(word & 255, (word >> 8) & 255)
            faces.append((word & 255, (word >> 16) & 255, (word >> 24) & 255))
        self.assertEqual([(0, 1, 2), (1, 3, 2)], faces)
        scene, length = struct.unpack_from("<II", body, 24)
        self.assertEqual("13e3674859b79ff45fa950cb1c2f788a368f89d70bb24599dfbccf061a75c0de", hashlib.sha256(body[scene:scene + length]).hexdigest())
        self.assertEqual(struct.pack("<iiiBBBB", 9216, 9216, 9216, 0, 0, 96, 0), body[scene:scene + 16])
        installed = register_package(body[0xEC430:0xEC430 + 0x2930])
        self.assertEqual(0x800973C4, installed.read(0x80076558, 4)); self.assertEqual(90, installed.trace.count(0x800135C0))

    def require_edited_companions(self, cue, receipt):
        self.assertEqual("1fcb9bade2f3c9c59b6e6d7b57bd8a2bd0773534d151374d62fcb2ce442e663c", hashlib.sha256(cue).hexdigest())
        self.assertEqual("3b545c4dd0e2102586e9f520dec96fda3ea8e84a088003d5b7a7a5ef701373d7", hashlib.sha256(receipt).hexdigest())
        parsed = json.loads(receipt)
        expected = {"kind": "spyro-native-fresh-intent", "schema": 1,
                    "floor": {"x": 8192, "y": 8192, "z": 8704, "width": 2048, "depth": 2048},
                    "spawn": {"x": 9216, "y": 9216, "heading": 96},
                    "display": {"floorRgb": [72, 132, 120], "skyRgb": [44, 66, 100]}, "musicTrackId": 2}
        canonical = json.dumps(expected, separators=(",", ":")).encode()
        self.assertEqual(canonical.decode(), parsed["canonicalDocumentJson"])
        self.assertEqual("b3f10d40db88cbfa3d3393e0d9c9e5e49e9a829f3fd311bcfa005082016e4cf1", hashlib.sha256(canonical).hexdigest())
        self.assertEqual(hashlib.sha256(canonical).hexdigest(), parsed["canonicalDocumentSha256"])
        self.assertEqual("fba3e61a3f93b9dff27d9a5304eb746b7c2ef9aa95a71cfeda6adaee62b571c2", parsed["planSha256"])
        for name in ("runtimeVerified", "normalCreateBinApproved", "projectSaveBindingClaimed"):
            self.assertIs(False, parsed[name])


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source")
    parser.add_argument("--edited-candidate", help="Read-only exact edited artifact check, only together with --source.")
    args = parser.parse_args()
    if args.edited_candidate and not args.source: parser.error("--edited-candidate requires --source.")
    SOURCE = args.source
    EDITED_CANDIDATE = args.edited_candidate
    unittest.main(argv=[sys.argv[0]])
