#!/usr/bin/env python3
"""Read-only source/recipe ABI oracle; no Core build, ROM or native execution.

Town's visibility helpers and donor whole handlers are not synthesized. The
prologue runs to its real first JAL target; dispatch has an explicit synthetic
post-visibility-list entry witness. Recipe addresses are test-only code owners,
not approved native arena placements. Stdout is evidence, never a candidate.
"""
import argparse
import copy
import hashlib
import importlib.util
import json
import shutil
import struct
import subprocess
import sys
from pathlib import Path

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[2]
BASE, STACK, ROW, POOL = 0x8007AA38, 0x801FF000, 0x80140000, 0x801C0000
RETURN, SPYRO = 0xDEADBEE0, 0x80078A58
HELPERS = {
    "src/Spyro.Editor.NativeMobyPersistentOwnerSmoke/source_owner_lifecycle_oracle.py": "4374150efed3d518ae5a6ae0aeae6b8fe05e03b940961049a480759c9f958429",
    "src/Spyro.Editor.NativeMobyDependencyClosureSmoke/source_overlay_oracle.mjs": "394d5971720dc808c35705dcb79201d24f7ada49d877dd10b826c43fbb6d84df",
    "src/Spyro.Editor.Core/Analysis/NativeMipsBehaviorFragmentCodec.cs": "b239a992c6d8d2c029a80c774bd877123949690120a343d65792d5411061043e",
}
SPANS = (
    ("Town-complete-particle-dispatch-tables", "town-data", 0x8007AC80, 0x288, "72901b134a94cfd71541e23cda95902a16814bb887bfe6b16e215fe6280b202e"),
    ("actual-RNG-helper", "exe", 0x8006272C, 0x30, "8bcf455ef340cfd7846fd7937a88671fbf165aa56526545a4912547c8d6bcfd4"),
    ("donor-Spyro-and-actor-loads", "wizard", 0x80082868, 8, "540f8a196a974edfda4d501c4e2e650ff8b02fe77353a7ed296234fa4bee3dd9"),
    ("Town-main-prologue-and-list-setup", "town", 0x8007DA54, 0x178, "3390151d01a3c026f6115a51b16b92414bf7dc3af8dbbf8e38717eaac3942f7c"),
    ("Town-complete-class-compare-chain", "town", 0x8007DB34, 0x264, "0ffb33d9b0b797e469fb2a1003c1736e28ee380b42738f40f127d8e1a5f00bf4"),
    ("Town-next-owner-and-return", "town", 0x80087258, 0x4C, "829a0bc3b1a7a93ee100386cb5d7f16eef6fc2737e1044249ef9fba760abfafd"),
    ("Town-tick-prologue", "town", 0x80087E20, 0x80, "7d6997decec01158391136cabbd0b90dea27ea57864fc1b4fd165d353b0fffca"),
    ("Town-tick-tail", "town", 0x80088EF0, 0x78, "129bcac3567804a28a71ccdddbc591ca348d96f56f7c24a1c4f9d250970f8d69"),
    ("Town-spawn-prologue", "town", 0x80088F68, 0x8C, "0f9c96d1d1bd04cd777e56e00b769d81b8e378b0d34f9d6d5bbf63ef21469831"),
    ("Town-spawn-tail", "town", 0x80089E74, 0x58, "7ffdfc304c6719b57a2751a9c83b022e827093f66387e30d60865292a19b8ebd"),
    ("donor-tick-prologue", "wizard", 0x80087B40, 0x80, "54b8d1235fdcd25f7c07dfeb4a517395fad0849d42197319b8c0713d0bd68827"),
    ("donor-tick-tail", "wizard", 0x800893D0, 0x80, "5807c25f222f3b7077deed1df8f669116f7dc79ba9b11802a5e1c7f05e6d2648"),
    ("donor-spawn-prologue", "wizard", 0x80089450, 0x90, "531d28fed98c00022f91d0b4ad53ff4455105208d7781389dd9bd8241f910203"),
    ("donor-spawn-tail", "wizard", 0x8008A848, 0x58, "868ac5cab4a058ab87978e586afaa496e6e9e5ef14b410a198cb64bee3cf793f"),
    ("donor07-tick", "wizard", 0x80088370, 0x98, "a2f5681329e63ab489abec1424ecc230f462f4af53bfc5d0e0ae94bb5a09afc4"),
    ("donor41-tick", "wizard", 0x800888E8, 0xFC, "bdfa5cf0752d3ca410a216f83a31d196492bd22de836ccfba9255582a5c01a19"),
    ("donor07-spawn", "wizard", 0x80089934, 0x11C, "c3e0db5def4d5c5c63c10b3e8acf314ebabfa91c93f8a2087a268e78da2df6e3"),
    ("donor07-tail-and41-spawn", "wizard", 0x80089F28, 0xD0, "fbff816f4c054e52e663d05b0c170db721b3931ce7baac3bd93340837db075d8"),
)


def sha(data):
    return hashlib.sha256(data).hexdigest()


def require(ok, message):
    if not ok:
        raise AssertionError(message)


def require_helpers():
    for path, expected in HELPERS.items():
        require(sha((ROOT / path).read_bytes()) == expected, "Pinned input changed: " + path)


require_helpers()
spec = importlib.util.spec_from_file_location("fresh_host_memory", ROOT / next(iter(HELPERS)))
native = importlib.util.module_from_spec(spec)
spec.loader.exec_module(native)


class Machine(native.Scalar):
    """Strict integer subset with R3000 load/control delays and HI/LO only.

    No helper stubs, GTE, cache, interrupt, timing or hardware implementation.
    Consecutive same-target delayed loads cancel the older pending value.
    """
    def __init__(self, code):
        super().__init__(code)
        self.hi, self.lo = 0x12345678, 0x89ABCDEF

    def run(self, entry, stops, maximum=10000):
        pc, pending_branch, pending_load = entry, None, None
        for _ in range(maximum):
            if pc in stops:
                require(pending_branch is None and pending_load is None, "Unretired delay at stop")
                return pc
            require(pc in self.code and pc % 4 == 0, f"Unowned instruction {pc:08X}")
            w = self.code[pc]
            op, rs, rt, rd, shift, fn = w >> 26, w >> 21 & 31, w >> 16 & 31, w >> 11 & 31, w >> 6 & 31, w & 63
            a, b, imm = self.r[rs], self.r[rt], native.signed(w & 65535, 16)
            old_branch, old_load = pending_branch, pending_load
            pending_branch = pending_load = None
            written = set()

            def put(r, value):
                if r:
                    self.r[r] = value & 0xFFFFFFFF
                    written.add(r)

            def branch(target):
                nonlocal pending_branch
                require(old_branch is None, "Control in delay slot")
                pending_branch = target & 0xFFFFFFFF

            if op == 0:
                if fn == 0: put(rd, b << shift)
                elif fn == 2: put(rd, b >> shift)
                elif fn == 3: put(rd, native.signed(b) >> shift)
                elif fn == 8: branch(a)
                elif fn == 0x10: put(rd, self.hi)
                elif fn == 0x11: self.hi = a
                elif fn == 0x12: put(rd, self.lo)
                elif fn == 0x13: self.lo = a
                elif fn == 0x18:
                    product = (native.signed(a) * native.signed(b)) & 0xFFFFFFFFFFFFFFFF
                    self.hi, self.lo = product >> 32, product & 0xFFFFFFFF
                elif fn == 0x21: put(rd, a + b)
                elif fn == 0x23: put(rd, a - b)
                elif fn == 0x24: put(rd, a & b)
                elif fn == 0x25: put(rd, a | b)
                elif fn == 0x2A: put(rd, int(native.signed(a) < native.signed(b)))
                else: raise AssertionError(f"Unsupported SPECIAL {w:08X}")
            elif op in (2, 3):
                target = ((pc + 4) & 0xF0000000) | ((w & 0x3FFFFFF) << 2)
                if op == 3:
                    put(31, pc + 8)
                    self.calls.append((pc, target))
                branch(target)
            elif op in (4, 5): branch(pc + 4 + imm * 4 if (a == b) == (op == 4) else pc + 8)
            elif op in (6, 7):
                require(rt == 0, "Reserved branch field")
                branch(pc + 4 + imm * 4 if (native.signed(a) <= 0) == (op == 6) else pc + 8)
            elif op == 9: put(rt, a + imm)
            elif op == 10: put(rt, int(native.signed(a) < imm))
            elif op == 11: put(rt, int(a < (imm & 0xFFFFFFFF)))
            elif op == 12: put(rt, a & (w & 65535))
            elif op == 13: put(rt, a | (w & 65535))
            elif op == 15: put(rt, (w & 65535) << 16)
            elif op in (0x21, 0x23, 0x24, 0x25):
                width = {0x21: 2, 0x23: 4, 0x24: 1, 0x25: 2}[op]
                value = self.read((a + imm) & 0xFFFFFFFF, width)
                if op == 0x21: value = native.signed(value, 16)
                if old_load is not None and old_load[0] == rt: old_load = None
                pending_load = (rt, value & 0xFFFFFFFF)
            elif op in (0x28, 0x29, 0x2B):
                self.write((a + imm) & 0xFFFFFFFF, b, {0x28: 1, 0x29: 2, 0x2B: 4}[op])
            else: raise AssertionError(f"Unsupported opcode {w:08X}")
            if old_load is not None and old_load[0] not in written: put(*old_load)
            self.r[0] = 0
            self.instructions += 1
            pc = old_branch if old_branch is not None else (pc + 4) & 0xFFFFFFFF
        raise AssertionError("Finite instruction budget exhausted")


def words(data, start):
    require(len(data) % 4 == 0 and start % 4 == 0, "Code alignment")
    return {start + i: struct.unpack_from("<I", data, i)[0] for i in range(0, len(data), 4)}


def capture(path):
    with open(path, "rb") as image:
        native.identity(image)
        require(image.read(1) == b"", "Original EOF")
        town = native.logical(image, 37 * 2048 + 0x118E800, 0xF800)
        wizard = native.logical(image, 37 * 2048 + 0x2F5B000, 0x10000)
        exe = native.logical(image, 53875 * 2048, 0x66000)
    require(sha(town) == "9bc923cc8d27703537b81b01f51fde351e58aabee87d38a8afc630f1031878a5", "Whole Town overlay")
    require(sha(wizard) == "5d3996111567ffac6eca08d340f2f691824bb7969ba2a0cd95e9346ccfe157e1", "Whole donor overlay")
    require(sha(exe) == native.EXE_SHA, "Whole executable")
    domains, code = {"town": town, "town-data": town, "wizard": wizard, "exe": exe}, {}
    for label, owner, pc, length, expected in SPANS:
        basis = 0x8000F800 if owner == "exe" else BASE
        data = domains[owner][pc - basis:pc - basis + length]
        require(sha(data) == expected, "Source window: " + label)
        if owner in ("town", "exe"): code.update(words(data, pc))
    # Read the exact current golden afterimage from the unchanged raw oracle.
    # It authenticates original source and reproduces the known overlay/EXE;
    # this does not invoke an editor assembly or write any image.
    node = shutil.which("node")
    require(node is not None, "Node required for preserved current-word oracle")
    js = """process.argv[2]=process.argv[1];let report;
console.log=s=>{report=JSON.parse(s)};
const m=await import(process.argv[3]);
const a=report.blocks.find(b=>b.id==='wizard-adapter');
const x=report.blocks.find(b=>b.id==='main-exit');
process.stdout.write(JSON.stringify({overlaySha:report.overlaySha,entryPc:parseInt(a.pc,16),
entry:Buffer.from(m.overlayBytes.subarray(a.offset,a.offset+a.length)).toString('hex'),
exitPc:parseInt(x.pc,16),exit:Buffer.from(m.overlayBytes.subarray(x.offset,x.offset+x.length)).toString('hex')}));"""
    result = subprocess.run([node, "--input-type=module", "-e", js, str(Path(path).resolve()), "unused",
        (ROOT / "src/Spyro.Editor.NativeMobyDependencyClosureSmoke/source_overlay_oracle.mjs").as_uri()],
        check=True, text=True, capture_output=True)
    current = json.loads(result.stdout)
    require(current["overlaySha"] == "10f82ee1028a9ad4fa9045d948a88e76545847031b954ac08ad0e74e6e9c7083", "Current certified Dark overlay golden")
    return domains, code, current


def seed(code):
    m = Machine(dict(code))
    m.r = [0] + [0x13570000 + i * 0x101 for i in range(1, 32)]
    m.r[29], m.r[31] = STACK, RETURN
    m.map(STACK - 0x800, 0x820, 0xA5)
    m.map(ROW, 0xB0)
    m.poke(ROW + 0x36, 0x11B, 2)
    m.poke(ROW + 0x58 + 0x36, 0x78, 2)
    m.map(0x800700F4, 12)
    m.poke(0x800700F4, ROW)
    m.poke(0x800700F8, ROW + 0x58)
    for at in (0x800756CC, 0x80075794, 0x800756C4, 0x800757F4): m.map(at, 4)
    m.map(SPYRO, 12, 0x5A)
    return m


def town_main(code):
    m = seed(code)
    original = m.r.copy()
    require(m.run(0x8007DA54, {0x80051FEC}) == 0x80051FEC, "Real prologue stops before visibility helper")
    require(m.r[29] == STACK - 0x1D0 and m.read(STACK - 0x28) == original[16], "Prologue JAL delay saved s0")
    # Deliberately not a simulated helper return: separate native entry after
    # unexecuted list maintenance. The synthetic list is a declared witness.
    m.run(0x8007DAC4, {0x8007DB34})
    require((m.r[19], m.r[23], m.r[30]) == (ROW, 0x10, SPYRO), "Actual Town actor/s7/fp setup")
    return m, original


def i(op, rs, rt, value):
    return op << 26 | rs << 21 | rt << 16 | value & 65535


def move(rd, rs):
    return rs << 21 | rd << 11 | 0x21


def jump(pc):
    require(pc % 4 == 0 and 0x80000000 <= pc < 0x80200000, "Recipe target alignment/domain")
    return 0x08000000 | (pc >> 2 & 0x3FFFFFF)


def pack(values):
    return struct.pack("<" + "I" * len(values), *values)


REGS = [r for r in range(1, 32) if r != 29]
ENTRY, LEAVE, LEAVE_DELETE, DISPATCH = 0x80190000, 0x80190200, 0x80190300, 0x80190400
DONOR, MARKER, LIGHTNING = 0x80191000, 0x80193000, 0x80193100


def adapter(kind, target):
    prefix = [i(9, 29, 29, -0x2B0)] + [i(0x2B, 29, r, 0x230 + k * 4) for k, r in enumerate(REGS)]
    prefix += [0x4010, i(0x2B, 29, 8, 0x2A8), 0x4012, i(0x2B, 29, 8, 0x2AC)]
    if kind == "old": mapping = [i(9, 0, 30, 1)]
    elif kind == "main": mapping = [move(23, 30), i(9, 0, 30, 1)]
    elif kind == "tick": mapping = [move(21, 18), move(18, 16)]
    elif kind == "spawn":
        # Load saved values, not sequential moves that overwrite a later input.
        mapping = [i(0x23, 29, dest, 0x230 + REGS.index(src) * 4) for dest, src in ((22, 21), (21, 23), (23, 22))]
    else: raise AssertionError("Uncertified adapter role")
    return pack(prefix + mapping + [jump(target), 0])


def leave(target):
    return pack([i(0x23, 29, 8, 0x2A8), 0, 0x01000011, i(0x23, 29, 8, 0x2AC), 0, 0x01000013] +
        [i(0x23, 29, r, 0x230 + k * 4) for k, r in enumerate(REGS)] + [jump(target), i(9, 29, 29, 0x2B0)])


def dispatch():
    # The displaced SLTI/BEQ is reproduced with two absolute exits; its original
    # delay SLTI remains executed exactly once. Every other class stays native.
    return pack([i(9, 0, 2, 0x26), i(5, 4, 2, 3), 0, jump(LIGHTNING), 0,
        i(9, 0, 2, 0x11B), i(5, 4, 2, 3), 0, jump(ENTRY), 0,
        0x28820101, i(5, 2, 0, 3), 0x288200FF, jump(0x8007DC70), 0, jump(0x8007DB40), 0])


def table(town, at, selector):
    return struct.unpack_from("<I", town, at - BASE + selector * 4)[0]


def particle_machine(code, town, selector, spawn=False, count=1):
    m = seed(code)
    m.map(POOL, 0x60)
    m.poke(POOL, selector, 1)
    m.poke(POOL + 1, 2 if selector == 7 else 1, 1)
    m.poke(POOL + 3, 1, 1)
    m.poke(POOL + 0x21, 0xFF, 1)
    m.map(0x80075824, 4); m.poke(0x80075824, POOL)
    m.map(0x80075AC0, 4); m.poke(0x80075AC0, 12345)
    m.map(0x8007AC80, 0x288)
    for at in range(0x288): m.poke(0x8007AC80 + at, town[0x248 + at], 1)
    before = m.r.copy()
    if spawn:
        m.r[4:8] = [count, selector, ROW, 0x1234]
        before = m.r.copy()
        destination = table(town, 0x8007ADC4, selector) if selector < 81 and count > 0 else (0x80089E74 if count > 0 else RETURN)
        m.run(0x80088F68, {destination})
    else:
        m.r[4] = 3
        before = m.r.copy()
        destination = table(town, 0x8007AC80, selector) if selector < 81 else 0x80088F20
        m.run(0x80087E20, {destination})
    return m, before, destination


def relocate(wizard, spans, exits):
    mapping, offset = {}, 0
    for pc, length in spans:
        for k in range(0, length, 4): mapping[pc + k] = DONOR + offset + k
        offset += length
    output = {}
    for pc, length in spans:
        for k in range(0, length, 4):
            source = pc + k; target_pc = mapping[source]
            w = struct.unpack_from("<I", wizard, source - BASE)[0]
            op = w >> 26
            if op in (2, 3, 4, 5, 6, 7):
                old = ((source + 4) & 0xF0000000) | ((w & 0x3FFFFFF) << 2) if op in (2, 3) else source + 4 + native.signed(w & 65535, 16) * 4
                if old in mapping: target = mapping[old]
                elif old in exits: target = exits[old]
                elif op == 3 and old in (0x8006272C, 0x80053570, 0x80017BFC): target = old
                else: raise AssertionError(f"Uncertified source transfer {source:08X}->{old:08X}")
                if op in (2, 3): w = jump(target) | (0x04000000 if op == 3 else 0)
                else:
                    delta = target - target_pc - 4
                    require(delta % 4 == 0 and -32768 <= delta // 4 <= 32767, "Relocated branch range")
                    w = w & 0xFFFF0000 | delta // 4 & 65535
            output[target_pc] = w
    return output, mapping


def main(path, red=False):
    require(__debug__, "Assertions required")
    domains, code, current = capture(path)
    checks, refusals = [], []
    def check(ok, label):
        require(ok, label); checks.append(label)
    def reject(action, expected):
        try: action()
        except AssertionError as error:
            check(expected in str(error), "Exact refusal: " + expected); refusals.append(expected); return
        raise AssertionError("Missing refusal: " + expected)

    m, _ = town_main(code)
    entry = bytes.fromhex(current["entry"])
    m.code.update(words(entry, current["entryPc"]))
    donor_entry = 0x80000000 | ((struct.unpack_from("<I", entry, len(entry) - 8)[0] & 0x3FFFFFF) << 2)
    m.run(current["entryPc"], {donor_entry})
    mismatch = {"case": "unsupported-current-Dark-adapter-on-Town", "actualS7": hex(m.r[23]),
        "requiredDonorSpyro": hex(SPYRO), "actualFp": m.r[30], "currentEntrySha256": sha(entry),
        "certifiedDarkUseRegressed": False, "nativeRuntime": False}
    if red:
        print(json.dumps(mismatch), flush=True)
        require(m.r[23] == SPYRO, "UNSUPPORTED HOST RED: current Dark adapter retains Town s7=0x10, not donor Spyro")
    check(entry == adapter("old", donor_entry), "Real current golden entry equals pinned production emission recipe")
    check(bytes.fromhex(current["exit"]) == leave(0x80082914), "Real current golden exit remains unchanged")
    check(m.r[23] == 0x10 and m.r[30] == 1, "Unsupported Town mismatch is reproduced, not certified Dark regression")
    reject(lambda: m.read(m.r[23]), "Unmapped native read")
    # Exact two native loads with one explicit test-only NOP to retire the
    # second load. This is a consumer slice, not whole Wizard execution.
    consumer = domains["wizard"][0x80082868 - BASE:0x80082870 - BASE] + bytes(4)
    m.code.update(words(consumer, MARKER))
    reject(lambda: m.run(MARKER, {MARKER + 12}), "Unmapped native read 00000010")

    # A malicious clobber witness tests the wrapper contract, not donor code.
    # The payload may use its owned lower230 bytes; no helper peak is claimed.
    def clobber_restore(machine, role, target, continuation):
        state, hi, lo, sp = machine.r.copy(), machine.hi, machine.lo, machine.r[29]
        outer = machine.bytes(sp, STACK + 0x20 - sp)
        lower = machine.bytes(STACK - 0x800, sp - 0x2B0 - (STACK - 0x800))
        machine.code.update(words(adapter(role, target), ENTRY))
        machine.code.update(words(leave(continuation), LEAVE))
        machine.run(ENTRY, {target})
        expected = {"main": (state[30], 1), "tick": (state[16], state[18]), "spawn": (state[23], state[21], state[22])}[role]
        actual = {"main": (machine.r[23], machine.r[30]), "tick": (machine.r[18], machine.r[21]), "spawn": (machine.r[21], machine.r[22], machine.r[23])}[role]
        check(actual == expected, role + " semantic register inputs")
        check(machine.r[29] == sp - 0x2B0, role + " private owned frame")
        for r in REGS: machine.r[r] = (0xE1000000 + r * 0x10101) & 0xFFFFFFFF
        machine.hi, machine.lo = 0xF1234567, 0xFEDCBA98
        for at in range(sp - 0x2B0, sp - 0x80): machine.poke(at, 0xCC, 1)
        machine.run(LEAVE, {continuation})
        check(machine.r == state and (machine.hi, machine.lo) == (hi, lo), role + " all GPR/SP/HI/LO restored")
        check(machine.bytes(sp, STACK + 0x20 - sp) == outer, role + " complete host frame and adjacent bytes preserved")
        check(machine.bytes(STACK - 0x800, len(lower)) == lower, role + " lower adjacent stack owner preserved")

    main_m, original = town_main(code)
    clobber_restore(main_m, "main", MARKER, 0x80087258)
    main_m.run(0x80087258, {0x8007DB34})
    check(main_m.r[19] == ROW + 0x58 and main_m.r[4] == 0x78 and main_m.read(STACK - 0x50) == 0x800700FC,
        "Actual next-owner continuation reaches companion78 and advances list cursor once")
    main_m.run(0x80087258, {RETURN})
    check(all(main_m.r[r] == original[r] for r in list(range(16, 24)) + [29, 30, 31]), "Actual outer Town epilogue restores native callee-saved state")

    # Compare complete original class dispatch outcomes, including all512 class
    # keys, without entering any selected handler/helper body.
    dispatch_code = words(domains["town"][0x8007DB34 - BASE:0x8007DD98 - BASE], 0x8007DB34)
    native_stops = {0x80087258}
    for pc, w in dispatch_code.items():
        op = w >> 26
        if op in (2, 4, 5):
            at = ((pc + 4) & 0xF0000000) | ((w & 0x3FFFFFF) << 2) if op == 2 else pc + 4 + native.signed(w & 65535, 16) * 4
            if at not in dispatch_code: native_stops.add(at)
    routes = []
    for actor_class in range(512):
        a = Machine(dispatch_code); a.r[4], a.r[23] = actor_class, 0x10
        b = copy.deepcopy(a); b.code = dict(dispatch_code) | words(dispatch(), DISPATCH)
        old = a.run(0x8007DB34, native_stops)
        new = b.run(DISPATCH, native_stops | {ENTRY, LIGHTNING})
        if actor_class in (0x11B, 0x26): check(new == (ENTRY if actor_class == 0x11B else LIGHTNING), "New exact class hook " + str(actor_class))
        else: check(new == old and b.r == a.r, "Resident/default class complement " + str(actor_class))
        routes.append((actor_class, old, new))
    check(routes[0x78][1:] == (0x80082FB8, 0x80082FB8), "Original companion78 native handler unchanged")
    active, _ = town_main(code)
    active.code.update(words(pack([jump(DISPATCH), 0]), 0x8007DB34))
    active.code.update(words(dispatch(), DISPATCH))
    active.code.update(words(adapter("main", MARKER), ENTRY))
    active.run(0x8007DB34, {MARKER})
    check((active.r[19], active.r[23], active.r[30]) == (ROW, SPYRO, 1), "Actual patched native class entry reaches adapted donor inputs")
    active.code.update(words(consumer, MARKER)); active.run(MARKER, {MARKER + 12})
    check(active.r[4] == 0x5A5A5A5A and active.reads[-2:] == [(SPYRO, 4), (ROW + 0x10, 4)], "Exact donor Spyro/actor loads use mapped owners after Town adaptation")

    town, wizard = domains["town"], domains["wizard"]
    hook_pcs = (0x8007AC9C, 0x8007AD84, 0x8007ADE0, 0x8007AEC8)
    proposed = bytearray(town)
    proposed[0x8007DB34 - BASE:0x8007DB34 - BASE + 8] = pack([jump(DISPATCH), 0])
    for at in hook_pcs: struct.pack_into("<I", proposed, at - BASE, ENTRY)
    inverse = bytearray(proposed)
    inverse[0x8007DB34 - BASE:0x8007DB34 - BASE + 8] = town[0x8007DB34 - BASE:0x8007DB34 - BASE + 8]
    for at in hook_pcs: inverse[at - BASE:at - BASE + 4] = town[at - BASE:at - BASE + 4]
    check(bytes(inverse) == town, "Prototype hook inverse preserves every other byte of full Town overlay")
    for selector in range(81):
        for spawn in (False, True):
            pm, _, destination = particle_machine(code, town, selector, spawn)
            wanted = table(town, 0x8007ADC4 if spawn else 0x8007AC80, selector)
            check(destination == wanted, "Actual particle dispatch " + str((selector, spawn)))
            if selector not in (7, 0x41):
                check(table(proposed, 0x8007ADC4 if spawn else 0x8007AC80, selector) == wanted,
                    "Original companion/default particle table complement " + str((selector, spawn)))
    for selector in (7, 0x41):
        tick, before, target = particle_machine(code, proposed, selector)
        check(target == ENTRY and (tick.r[16], tick.r[18], tick.r[19], tick.r[22]) == (POOL + 1, POOL, POOL + 4, POOL + 0x18), "Exact patched Town table reaches particle raw/plus-one ABI")
        clobber_restore(tick, "tick", MARKER, 0x80088F20)
        tick.run(0x80088F20, {RETURN})
        check(all(tick.r[r] == before[r] for r in list(range(16, 23)) + [29, 31]), "Native tick terminal restores outer frame")
        spawn, before, target = particle_machine(code, proposed, selector, True)
        check(target == ENTRY and (spawn.r[21], spawn.r[22], spawn.r[23], spawn.r[30]) == (selector, 255, 128, 0), "Exact patched Town spawn selector/color/counter ABI")
        clobber_restore(spawn, "spawn", MARKER, 0x80089E74)
        spawn.run(0x80089E74, {RETURN})
        check(all(spawn.r[r] == before[r] for r in list(range(16, 24)) + [29, 30, 31]), "Native spawn terminal restores outer frame")
    for selector in (81, 255):
        pm, _, target = particle_machine(code, town, selector)
        check(target == 0x80088F20 and not any(0x8007AC80 <= at < 0x8007ADC4 for at, _ in pm.reads), "Out-of-table selector avoids native table read")
    empty, before, _ = particle_machine(code, town, 7, True, 0)
    check(all(empty.r[r] == before[r] for r in list(range(16, 24)) + [29, 30, 31]), "Nonpositive spawn count follows real empty epilogue")

    # Execute source07 entirely; source41 includes its real RNG helper and
    # condition tail. The delete helper is an explicit stop, never a success.
    for selector, pc, size in ((7, 0x80088370, 0x98), (0x41, 0x800888E8, 0xFC)):
        spans = [(pc, size)] + ([(0x800893D0, 0x1C)] if selector == 0x41 else [])
        linked, mapping = relocate(wizard, spans, {0x800893EC: LEAVE_DELETE, 0x800893F4: LEAVE})
        for age in (0, 1, 7, 8, 12, 13, 24, 25, 255):
            pm, _, _ = particle_machine(code, town, selector)
            state, outer, hilo = pm.r.copy(), pm.bytes(pm.r[29], 0x80), (pm.hi, pm.lo)
            pm.poke(POOL + 2, age, 1)
            for at, value in ((4, 100), (6, 200), (8, 300), (10, 140), (12, 260), (14, 380), (24, 2), (26, 4), (28, 6)):
                pm.poke(POOL + at, value, 2)
            expected_payload = bytearray(pm.bytes(POOL, 32))
            if selector == 7 and age < 25:
                expected_payload[2] = age + 1
                expected_payload[10] = (age if age < 13 else 24 - age) * 4
                expected_payload[11] = (expected_payload[11] + expected_payload[30]) & 255
                for at, value in ((4, 102), (6, 204), (8, 306)): struct.pack_into("<H", expected_payload, at, value)
            if selector == 0x41:
                expected_payload[2] = (age + 1) & 255
                rng1 = (12345 * 0x41C64E6D + 0x3039) & 0xFFFFFFFF
                rng2 = (rng1 * 0x41C64E6D + 0x3039) & 0xFFFFFFFF
                for at, value in ((4, 120), (6, 230), (8, 340), (10, 159 + ((rng1 >> 16) & 2)), (12, 289 + ((rng2 >> 16) & 2)), (14, 420)):
                    struct.pack_into("<H", expected_payload, at, value)
            pm.code.update(linked)
            pm.code.update(words(adapter("tick", mapping[pc]), ENTRY))
            pm.code.update(words(leave(0x80088F20), LEAVE))
            pm.code.update(words(leave(0x80088F18), LEAVE_DELETE))
            continuation = pm.run(ENTRY, {0x80088F20, 0x80088F18})
            deleting = age >= 25 if selector == 7 else (age + 1) % 256 >= 8
            check(continuation == (0x80088F18 if deleting else 0x80088F20), "Exact source condition chooses correct Town continuation")
            check(pm.r == state and (pm.hi, pm.lo) == hilo and pm.bytes(state[29], 0x80) == outer, "Actual tick restores every host register/frame/HI/LO " + str((selector, age)))
            check(pm.bytes(POOL, 32) == bytes(expected_payload), "Complete32-byte source particle afterimage/complement")
            if selector == 7:
                check(pm.read(POOL + 2, 1) == (age + 1 if age < 25 else age), "Actual07 age bound " + str(age))
                check(pm.read(POOL + 4, 2) == (102 if age < 25 else 100), "Actual07 uses raw+1 and preserves halfword layout " + str(age))
                check(not pm.calls, "07 tick uses no synthesized helper")
            else:
                check(pm.read(POOL + 2, 1) == (age + 1) % 256 and pm.read(POOL + 4, 2) == 120 and pm.read(POOL + 14, 2) == 420, "Actual41 private scratch and condition-tail store " + str(age))
                rng = 12345
                for _ in range(2): rng = (rng * 0x41C64E6D + 0x3039) & 0xFFFFFFFF
                check(pm.read(0x80075AC0) == rng and [c[1] for c in pm.calls] == [0x8006272C] * 2, "Actual two RNG helper calls only")
            if deleting:
                pm.run(0x80088F18, {0x80053608})
                check(pm.r[4] == POOL and pm.calls[-1] == (0x80088F18, 0x80053608), "Native Town deletion JAL delay passes original raw pool row")
                reject(lambda: pm.run(0x80053608, {RETURN}), "Unowned instruction")

    # Spawn code is executed only to its allocator JAL, plus a separately
    # witnessed final suffix: no allocator/vector helper return is fabricated.
    for selector, pc in ((7, 0x80089934), (0x41, 0x80089F34)):
        sm, _, _ = particle_machine(code, town, selector, True, 3)
        sm.code.update(words(wizard[pc - BASE:pc - BASE + 8], DONOR))
        sm.code.update(words(adapter("spawn", DONOR), ENTRY))
        sm.run(ENTRY, {0x80053570})
        check(sm.r[4] == (2 if selector == 7 else 1) and sm.r[22] == selector and sm.r[21] == 128,
            "Actual spawn allocator call and delayed kind argument " + str(selector))
        reject(lambda: sm.run(0x80053570, {RETURN}), "Unowned instruction")

    # The source sprite tail has its own precise entry preconditions. It is
    # tested separately, never passed off as an allocator/helper completion.
    suffix, suffix_map = relocate(wizard, [(0x80089A28, 0x28), (0x80089F28, 0x0C)], {0x8008A848: LEAVE})
    suffix_m, _, _ = particle_machine(code, proposed, 7, True, 3)
    suffix_state = suffix_m.r.copy()
    suffix_m.code.update(suffix)
    suffix_m.code.update(words(adapter("spawn", suffix_map[0x80089A28]), ENTRY))
    suffix_m.code.update(words(leave(0x80089E74), LEAVE))
    suffix_m.run(ENTRY, {suffix_map[0x80089A28]})
    suffix_m.r[18], suffix_m.r[2] = POOL, 0xABCDE005
    suffix_m.run(suffix_map[0x80089A28], {0x80089E74})
    check(suffix_m.r == suffix_state and suffix_m.bytes(POOL + 12, 3) == bytes([128] * 3) and suffix_m.read(POOL + 16, 1) == 0 and suffix_m.read(POOL + 17, 1) == 4,
        "Separate exact spawn sprite suffix preserves donor RGB/frame/depth and restores Town counter inputs")
    suffix_m.run(0x80089E74, {ENTRY})
    check(suffix_m.r[30] == 1 and suffix_m.read(STACK - 0x30) == 4 and suffix_m.read(STACK - 0x50) == 3,
        "Actual Town spawn continuation increments its own cursor80/count60 once")

    # A copied Dark operand rebase targets s1, which Town does not establish as
    # a particle cursor. The native first load must fail, not accept an alias.
    wrong, _, _ = particle_machine(code, town, 7)
    first = struct.unpack_from("<I", wizard, 0x80088370 - BASE)[0]
    wrong.code[DONOR] = first & ~(31 << 21) | 17 << 21
    reject(lambda: wrong.run(DONOR, {MARKER}), "Unmapped native read")
    bad_restore, _ = town_main(code)
    snapshot = bad_restore.r.copy()
    bad_restore.code.update(words(adapter("main", MARKER), ENTRY))
    broken = bytearray(leave(0x80087258)); struct.pack_into("<I", broken, 4, i(9, 0, 8, 0))
    bad_restore.code.update(words(broken, LEAVE)); bad_restore.run(ENTRY, {MARKER}); bad_restore.run(LEAVE, {0x80087258})
    reject(lambda: require(bad_restore.hi == 0x12345678 and bad_restore.r == snapshot, "Broken delayed HI restore"), "Broken delayed HI restore")
    reject(lambda: adapter("foreign-host", MARKER), "Uncertified adapter role")

    # Mutation/strictness controls keep acceptance tied to the actual source.
    for label, owner, pc, length, expected in SPANS:
        basis = 0x8000F800 if owner == "exe" else BASE
        mutant = bytearray(domains[owner][pc - basis:pc - basis + length]); mutant[0] ^= 1
        reject(lambda mutant=mutant, expected=expected: require(sha(mutant) == expected, "Mutated pinned source window"), "Mutated pinned source window")
    reject(lambda: Machine({ENTRY: 0x48000000}).run(ENTRY, {MARKER}), "Unsupported opcode")
    reject(lambda: Machine({ENTRY: jump(ENTRY), ENTRY + 4: 0}).run(ENTRY, {MARKER}, maximum=8), "Finite instruction budget")
    reject(lambda: Machine({ENTRY: jump(MARKER), ENTRY + 4: jump(MARKER)}).run(ENTRY, {MARKER}), "Control in delay slot")
    reject(lambda: seed({ENTRY: i(0x23, 29, 2, 1)}).run(ENTRY, {MARKER}), "Unaligned native scalar read")
    micro = seed(words(pack([i(0x23, 4, 2, 0), i(0x23, 4, 2, 4), move(3, 2), move(5, 2)]), ENTRY))
    micro.r[4], micro.r[2] = ROW, 9; micro.poke(ROW, 42); micro.poke(ROW + 4, 77)
    micro.run(ENTRY, {ENTRY + 16})
    check((micro.r[3], micro.r[5]) == (9, 77), "Consecutive same-target loads cancel old pending value")
    check(sha(domains["exe"]) == native.EXE_SHA, "No EXE callback/global changes")
    print(json.dumps({"marker": "FRESH_WIZARD_HOST_ABI_SOURCE_OK", "checks": len(checks), "refusals": len(refusals),
        "sourceImageSha256": native.IMAGE_SHA, "townSha256": sha(town), "wizardSha256": sha(wizard), "exeSha256": native.EXE_SHA,
        "unsupportedOldHostControl": mismatch, "sourcePins": SPANS, "helperPins": HELPERS,
        "townMainEntry": "8007DA54", "townNextOwner": "80087258", "mainFrame": 0x1D0,
        "privateAdapterFrame": 0x2B0, "classDispatchKeys": 512, "particleDispatchKeys": 81,
        "recipeHashes": {role: sha(adapter(role, MARKER)) for role in ("main", "tick", "spawn")},
        "boundaries": ["Visibility helpers51FEC/522C0 not executed; separate synthetic list entry at7DAC4",
            "Whole Wizard/lightning bodies and spawn allocator/vector helpers not executed",
            "Actual source07/41 integer ticks and RNG helper executed in synthetic owned memory",
            "Register clobber witness tests wrapper restoration, not donor behavior",
            "Recipe addresses do not certify final overlay placement, transient stack peak or native runtime"],
        "runtime": False, "behaviorInstalled": False, "publication": False}, indent=2))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("original_image")
    parser.add_argument("--unsupported-current-adapter-red", action="store_true")
    args = parser.parse_args()
    try:
        main(args.original_image, args.unsupported_current_adapter_red)
    finally:
        require_helpers()
        with open(args.original_image, "rb") as original:
            native.identity(original)
            require(original.read(1) == b"", "Original final EOF")
