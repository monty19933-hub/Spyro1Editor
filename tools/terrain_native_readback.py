import argparse, hashlib, json, struct
from pathlib import Path
from fractions import Fraction as F
from itertools import product

def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()

def logical(path, at, n):
    with path.open('rb') as f:
        out = bytearray()
        while n:
            sector, within = divmod(at, 2048)
            take = min(n, 2048 - within)
            f.seek(sector * 2352 + 24 + within)
            b = f.read(take)
            assert len(b) == take
            out.extend(b)
            n -= take
            at += take
        return bytes(out)

def wad(path, at, n):
    return logical(path, 37 * 2048 + at, n)

def intersects(ps, cell):
    poly = [tuple(map(F, q)) for q in ps]
    for axis in range(3):
        for bound, minimum in [(cell[axis] * 256, True), ((cell[axis] + 1) * 256, False)]:
            output = []
            for current, nxt in zip(poly, poly[1:] + poly[:1]):
                inside = lambda q: q[axis] >= bound if minimum else q[axis] <= bound
                ca, na = (inside(current), inside(nxt))
                if ca:
                    output.append(current)
                if ca != na:
                    t = (F(bound) - current[axis]) / (nxt[axis] - current[axis])
                    output.append(tuple((current[j] + t * (nxt[j] - current[j]) for j in range(3))))
            poly = output
            if not poly:
                return False
    return all((any((q[axis] < (cell[axis] + 1) * 256 for q in poly)) for axis in range(3)))

word=lambda b,o:struct.unpack_from("<I",b,o)[0]
def components(data):
    at = 0
    out = []
    for i in range(6):
        n = word(data, at)
        assert n >= 4 and n % 4 == 0 and (at + n <= len(data))
        out.append(data[at:at + n])
        at += n
    return (out, data[at:])
def collision(data):
    n, flags, tree, blocks, tri, assign, flag = struct.unpack_from('<7I', data, 4)
    tree += 4
    blocks += 4
    tri += 4
    assign += 4
    flag += 4
    assert tree == 32 and assign == tri + 12 * n and (flag >= assign + n) and (flag + flags <= len(data))
    t = data[tree:blocks]
    b = data[blocks:tri]
    u = lambda x, o: struct.unpack_from('<H', x, o)[0]
    cells = {}

    def segment(at):
        count = u(t, at)
        assert count <= 256 and at + (count + 1) * 2 <= len(t)
        return [u(t, at + 2 + i * 2) for i in range(count)]
    for z, yp in enumerate(segment(0)):
        if yp == 65535:
            continue
        for y, xp in enumerate(segment(yp)):
            if xp == 65535:
                continue
            for x, gp in enumerate(segment(xp)):
                if gp == 65535:
                    continue
                at = gp * 2
                first = u(b, at)
                assert first & 32768
                seq = [first & 32767]
                at += 2
                while not u(b, at) & 32768:
                    seq.append(u(b, at))
                    at += 2
                assert all((i < n for i in seq))
                cells[x, y, z] = seq
    return {'n': n, 'flags': flags, 'rows': data[tri:assign], 'assignments': data[assign:assign + n], 'prefix': data[flag:flag + flags], 'cells': cells}
def points(bytes):
    x, y, z = struct.unpack('<III', bytes)
    signed = lambda n: n - 512 if n & 256 else n
    q = (x & 16383, y & 16383, z & 16383)
    return [q, (q[0] + signed(x >> 14 & 511), q[1] + signed(y >> 14 & 511), q[2] + (z >> 16 & 255)), (q[0] + signed(x >> 23 & 511), q[1] + signed(y >> 23 & 511), q[2] + (z >> 24 & 255))]
def visibility(b):
    count = word(b, 8)
    end = 4 + word(b, 4)
    groups = []
    for i in range(count):
        at = 4 + word(b, 12 + 4 * i)
        rows = []
        while b[at] != 255:
            rows.append(b[at])
            at += 1
            assert at < end
        groups.append(rows)
    return (groups, b[end:])
