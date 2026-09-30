"""Bounded integer MIPS test CPU derived from source_owner_lifecycle_oracle.py.
No emulator, BIOS, host memory, or card I/O; strict mapped code/RAM ownership.
Integer HI/LO arithmetic is functional only; no timing or pipeline certification.
"""

def require(condition, message):
    if not condition:
        raise AssertionError(message)

def signed(value, bits=32):
    return value - (1 << bits) if value & (1 << (bits - 1)) else value


class Scalar:
    """Small test interpreter: integer subset only, strict memory/code ownership.

    A branch always executes one delay instruction. A load commits after the
    following instruction has read the old registers; a direct write to that
    same register cancels the pending load. No external helper is synthesized.
    """
    def __init__(self, code):
        self.code = code
        self.r = [0] * 32
        self.memory = {}
        self.writes = []
        self.reads = []
        self.calls = []
        self.instructions = 0
        self.hi = self.lo = 0

    def map(self, address, size, fill=0):
        for i in range(size):
            require(address + i not in self.memory, "Synthetic memory owner overlap")
            self.memory[address + i] = fill

    def poke(self, address, value, size=4):
        for i in range(size):
            require(address + i in self.memory, f"Unmapped fixture write {address+i:08X}")
            self.memory[address + i] = (value >> (i * 8)) & 255

    def read(self, address, size=4):
        require(size == 1 or address % size == 0, "Unaligned native scalar read")
        require(all(address + i in self.memory for i in range(size)), f"Unmapped native read {address:08X}+{size}")
        self.reads.append((address, size))
        return sum(self.memory[address + i] << (8 * i) for i in range(size))

    def write(self, address, value, size):
        require(size == 1 or address % size == 0, "Unaligned native scalar write")
        self.poke(address, value, size)
        self.writes.append((address, size, value & ((1 << (size * 8)) - 1)))

    def bytes(self, address, size):
        return bytes(self.memory[address + i] for i in range(size))

    def run(self, entry, stops, maximum=40000):
        pc, pending_branch, pending_load = entry, None, None
        for _ in range(maximum):
            if pc in stops:
                require(pending_branch is None, "Stopped before a branch delay completed")
                require(pending_load is None, "Stopped with an uncommitted load")
                return pc
            require(pc in self.code and pc % 4 == 0, f"Unowned native instruction {pc:08X}")
            self.pc = pc
            w = self.code[pc]
            op, rs, rt, rd, shift, fn = w >> 26, (w >> 21) & 31, (w >> 16) & 31, (w >> 11) & 31, (w >> 6) & 31, w & 63
            imm = signed(w & 65535, 16)
            a, b = self.r[rs], self.r[rt]
            old_branch, old_load = pending_branch, pending_load
            pending_branch = pending_load = None
            written = set()

            def put(register, value):
                if register:
                    self.r[register] = value & 0xFFFFFFFF
                    written.add(register)

            def branch(target):
                nonlocal pending_branch
                require(old_branch is None, "Control transfer in a branch delay is not in this bounded profile")
                pending_branch = target & 0xFFFFFFFF

            if op == 0:
                if fn == 0: put(rd, b << shift)
                elif fn == 2: put(rd, b >> shift)
                elif fn == 3: put(rd, signed(b) >> shift)
                elif fn == 4: put(rd, b << (a & 31))
                elif fn == 6: put(rd, b >> (a & 31))
                elif fn == 7: put(rd, signed(b) >> (a & 31))
                elif fn == 8: branch(a)
                elif fn == 9:
                    put(rd, pc + 8)
                    self.calls.append((pc, a))
                    branch(a)
                elif fn == 0x18:
                    result = (signed(a) * signed(b)) & 0xFFFFFFFFFFFFFFFF
                    self.lo, self.hi = result & 0xFFFFFFFF, result >> 32
                elif fn == 0x19:
                    result = (a * b) & 0xFFFFFFFFFFFFFFFF
                    self.lo, self.hi = result & 0xFFFFFFFF, result >> 32
                elif fn == 0x1B:
                    require(b != 0, "Fixture division by zero")
                    self.lo, self.hi = a // b, a % b
                elif fn == 0x12: put(rd, self.lo)
                elif fn == 0x10: put(rd, self.hi)
                elif fn in (0x20, 0x21): put(rd, a + b)
                elif fn in (0x22, 0x23): put(rd, a - b)
                elif fn == 0x24: put(rd, a & b)
                elif fn == 0x25: put(rd, a | b)
                elif fn == 0x26: put(rd, a ^ b)
                elif fn == 0x2A: put(rd, int(signed(a) < signed(b)))
                elif fn == 0x2B: put(rd, int(a < b))
                else: raise AssertionError(f"Unsupported SPECIAL {w:08X} at {pc:08X}")
            elif op in (2, 3):
                target = ((pc + 4) & 0xF0000000) | ((w & 0x3FFFFFF) << 2)
                if op == 3:
                    put(31, pc + 8)
                    self.calls.append((pc, target))
                branch(target)
            elif op == 1:
                require(rt in (0, 1), "Only BLTZ/BGEZ REGIMM is supported")
                take = signed(a) < 0 if rt == 0 else signed(a) >= 0
                branch(pc + 4 + imm * 4 if take else pc + 8)
            elif op in (4, 5): branch(pc + 4 + imm * 4 if (a == b) == (op == 4) else pc + 8)
            elif op in (6, 7):
                take = signed(a) <= 0 if op == 6 else signed(a) > 0
                branch(pc + 4 + imm * 4 if take else pc + 8)
            elif op in (8, 9): put(rt, a + imm)
            elif op == 10: put(rt, int(signed(a) < imm))
            elif op == 11: put(rt, int(a < (imm & 0xFFFFFFFF)))
            elif op == 12: put(rt, a & (w & 65535))
            elif op == 13: put(rt, a | (w & 65535))
            elif op == 14: put(rt, a ^ (w & 65535))
            elif op == 15: put(rt, (w & 65535) << 16)
            elif op in (0x20, 0x21, 0x23, 0x24, 0x25):
                width = {0x20: 1, 0x21: 2, 0x23: 4, 0x24: 1, 0x25: 2}[op]
                value = self.read((a + imm) & 0xFFFFFFFF, width)
                if op in (0x20, 0x21): value = signed(value, width * 8) & 0xFFFFFFFF
                pending_load = (rt, value)
            elif op in (0x28, 0x29, 0x2B): self.write((a + imm) & 0xFFFFFFFF, b, {0x28: 1, 0x29: 2, 0x2B: 4}[op])
            else: raise AssertionError(f"Unsupported opcode {w:08X} at {pc:08X}")
            if old_load is not None and old_load[0] not in written:
                put(*old_load)
            self.r[0] = 0
            pc = old_branch if old_branch is not None else (pc + 4) & 0xFFFFFFFF
            self.instructions += 1
        raise AssertionError("Bounded native scalar instruction budget exhausted")
