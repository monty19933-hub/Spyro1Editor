"""Independent fixed three-tile byte oracle; no Core, source reads or writes.

This is a closed test fixture, not a general tile compiler or source issuer.
The explicit cell memberships use conservative closed native tile bounds.
"""
import hashlib
import struct

SURFACE_SHA = "809c2442de920c36fe2bb97db36abd82b2e5888322c95bee51e661c1408ec3d1"
DATA_SHA = "fef347e230881f796927562fd4d9482a94c15a43ceae050cbfe5821b6edb59cb"
TILES = ((8448, 8448, 8704), (10496, 8448, 8704), (12544, 8448, 8704))


def require(ok, message):
    if not ok:
        raise AssertionError(message)


def sha(data):
    return hashlib.sha256(data).hexdigest()


def words(*values):
    return struct.pack("<" + "I" * len(values), *values)


def halfwords(*values):
    return struct.pack("<" + "H" * len(values), *values)


def word(data, at):
    require(at >= 0 and at % 4 == 0 and at + 4 <= len(data), "Tiled bounded word owner")
    return struct.unpack_from("<I", data, at)[0]


def fresh():
    sectors, triangles = [], []
    for world_x, world_y, world_z in TILES:
        x, y, z = world_x // 16, world_y // 16, world_z // 16
        width = depth = 128
        vertices = (0, width << 21, depth << 10, width << 21 | depth << 10)
        header = words((x + 64) << 16 | (y + 64), z << 16 | 256, x << 16 | y, z << 16)
        header += bytes((4, 4, 2, 0, 4, 4, 2, 12)) + words(0xFFFFFFFF)
        lp, hp = [], []
        for a, b, c in ((0, 1, 2), (1, 3, 2)):
            face = a << 26 | b << 20 | c << 14 | c << 8
            lp.append(words(face, face))
            face = a | a << 8 | b << 16 | c << 24
            hp.append(words(face, face, 0xFF, 0x80))
        sector = (header + words(*vertices) + words(*([0x708050] * 4)) + b"".join(lp)
                  + words(*vertices) + words(*([0x708050] * 8)) + b"".join(hp))
        require(len(sector) == 156, "Each independent packed tile sector is complete")
        sectors.append(sector + words(0))  # separate speculative-read guard
        # Native land uses A,C,B then B,C,D, the opposite render winding.
        triangles.append(words(x | width << 23, y | depth << 14, z,
                               x + width | (-width & 511) << 14,
                               y | depth << 14 | depth << 23, z))
    environment = words(500, 3, 16, 176, 336) + b"".join(sectors)
    visibility = words(32, 16, 1, 12) + bytes((0, 1, 2, 255)) + words(1, 8) + bytes([255] * 4)
    special = words(8, 0)
    # Z2 -> Y2 -> X2/X3. Axis links are tree-relative BYTE offsets;
    # leaf links 0/4 are WORD offsets into the two complete block groups.
    tree = halfwords(3, 65535, 65535, 8, 3, 65535, 65535, 16,
                     4, 65535, 65535, 0, 4, 0)
    blocks = halfwords(0x8003, 2, 1, 0, 0x8005, 4, 3, 2, 0x8000, 0)
    require(len(tree) == 28 and len(blocks) == 20, "Exact tree/block alignment owners")
    require(struct.unpack_from("<4H", blocks, 0) == (0x8003, 2, 1, 0) and
            struct.unpack_from("<4H", blocks, 8) == (0x8005, 4, 3, 2) and
            blocks[16:] == halfwords(0x8000, 0), "Two descending groups and exact final sentinel/zero pad")
    collision = (words(168, 6, 6, 28, 56, 76, 148, 156) + tree + blocks
                 + b"".join(triangles) + bytes(8) + bytes([63] * 6) + bytes(2))
    sky = words(16, 0x406080, 0, 0)
    result = environment + visibility + special + collision + sky
    require(tuple(map(len, (environment, visibility, special, collision, sky))) == (500, 32, 8, 168, 16),
            "Independent five-component spans")
    require(len(result) == 0x2D4 and sha(result) == SURFACE_SHA, "Independent three-tile surface golden")
    return result


def canonical(data, tail):
    """Separate closed tiled grammar; never widens the legacy96-length parser."""
    require(len(data) == 0xA58 and len(data) % 4 == 0, "Tiled canonical alignment/length")
    require(data[:8] == words(8, 0), "Tiled untextured metadata profile")
    positions, cursor = [], 8
    for expected in (500, 32, 8, 168, 16):
        positions.append(cursor)
        require(word(data, cursor) == expected and cursor + expected <= len(data), "Tiled exact component owner")
        cursor += expected
    require(positions == [8, 508, 540, 548, 716] and cursor == 732, "Tiled actual component offsets")
    require(len(tail) == 0x77C and tail[:4] == words(0) and word(tail, 4) == 0x80 and
            word(tail, 8) == 10 and word(tail, 0x84) == 0x6F8 and word(tail, 0x188) == 76,
            "Tiled complete raw-portal/particle/sound source tail")
    require(data[8:cursor] == fresh(), "Tiled exact canonical surface/tree bytes")
    require(data[cursor:] == tail, "Tiled complete retained tail")
    require(sha(data) == DATA_SHA, "Independent complete tiled data golden")
    return positions, cursor


def body(baseline, data, tail):
    canonical(data, tail)
    require(len(baseline) == 0x1FB800 and
            sha(baseline) == "b4abbdd09f794510d9b9341d7c7111ddd0b9068411bab30e70e7477b4f8c096c",
            "Independent baseline body owner")
    require(baseline[:32] == words(0x800, 0xDE000, 0xDE800, 0x1000, 0xDF800, 0xF800, 0xEF000, 0x3000),
            "Tiled fixture retains exact physical header allocations")
    padding = bytes(0x1000 - len(data))
    result = baseline[:0xDE800] + data + padding + baseline[0xDF800:]
    require(len(result) == len(baseline) and result[:0xDE800] == baseline[:0xDE800] and
            result[0xDF800:] == baseline[0xDF800:] and result[0xDE800 + len(data):0xDF800] == padding,
            "Only data allocation changes; complete header/upload/model/scene/auxiliary owners remain exact")
    return result
