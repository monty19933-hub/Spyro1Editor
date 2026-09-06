"""Independent little-endian surface oracle. Never writes a ROM or payload."""
import hashlib
import pathlib
import struct
import sys


def words(*values):
    return struct.pack("<" + "I" * len(values), *values)


def halfwords(*values):
    return struct.pack("<" + "H" * len(values), *values)


def fresh(x, y, z, width, depth, floor_rgb, sky_rgb):
    x, y, z, width, depth = (v // 16 for v in (x, y, z, width, depth))
    vertices = [0, width << 21, depth << 10, (width << 21) | (depth << 10)]
    lp_faces = [(a << 26) | (b << 20) | (c << 14) | (c << 8)
                for a, b, c in [(0, 1, 2), (1, 3, 2)]]
    hp_faces = [a | (a << 8) | (b << 16) | (c << 24)
                for a, b, c in [(0, 1, 2), (1, 3, 2)]]
    header = (words(((x + width // 2) << 16) | (y + depth // 2),
                    (z << 16) | (width + depth), (x << 16) | y, z << 16)
              + bytes([4, 4, 2, 0, 4, 4, 2, 12]) + words(0xFFFFFFFF))
    sector = (header + words(*vertices) + words(*([floor_rgb] * 4))
              + b"".join(words(v, v) for v in lp_faces)
              + words(*vertices) + words(*([floor_rgb] * 8))
              + b"".join(words(v, v, 0xFF, 0x80) for v in hp_faces))
    assert len(sector) == 156
    environment = words(172, 1, 8) + sector + words(0)
    visibility = words(32, 16, 1, 12) + bytes([0, 255, 255, 255]) + words(1, 8) + bytes([255] * 4)
    special = words(8, 0)
    cx, cy, cz = x >> 8, y >> 8, z >> 8
    z_bytes, y_bytes, x_bytes = (2 * (v + 2) for v in (cz, cy, cx))
    tree = (halfwords(cz + 1, *([65535] * cz), z_bytes)
            + halfwords(cy + 1, *([65535] * cy), z_bytes + y_bytes)
            + halfwords(cx + 1, *([65535] * cx), 0))
    tree += bytes((-len(tree)) % 4)
    blocks = halfwords(0x8001, 0, 0x8000, 0)
    # The native collision plane uses the reverse of the authored render order.
    collision_words = words(x | ((width & 511) << 23), y | (depth << 14), z,
                            (x + width) | ((-width & 511) << 14),
                            y | (depth << 14) | (depth << 23), z)
    triangle_offset = 32 + len(tree) + len(blocks)
    assignments_offset = triangle_offset + 24
    flags_offset = assignments_offset + 4
    collision = (words(flags_offset + 4, 2, 2, 28, 28 + len(tree), triangle_offset - 4,
                       assignments_offset - 4, flags_offset - 4)
                 + tree + blocks + collision_words + words(0) + bytes([63, 63, 0, 0]))
    sky = words(16, sky_rgb, 0, 0)
    return environment + visibility + special + collision + sky


fixture = fresh(8448, 8448, 8704, 1024, 1024, 0x708050, 0x406080)
assert len(fixture) == 324
print("FRESH_SURFACE", len(fixture), hashlib.sha256(fixture).hexdigest())

if len(sys.argv) == 2:
    path = pathlib.Path(sys.argv[1])
    with path.open("rb") as image:
        assert path.stat().st_size == 661547040
        assert hashlib.file_digest(image, "sha256").hexdigest() == "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37"
        executable = bytearray()
        for sector in range(0x66000 // 2048):
            image.seek((53875 + sector) * 2352 + 24)
            executable.extend(image.read(2048))
        assert hashlib.sha256(executable).hexdigest() == "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998"

        def instruction(pc):
            return struct.unpack_from("<I", executable, pc - 0x8000F800)[0]

        # Direct HP triangle primitive, FF branch, then untextured G3 command.
        assert instruction(0x80027260) == 0x31AB0080
        assert instruction(0x80027264) == 0x1D60002A
        assert instruction(0x8002755C) == 0x10400032
        assert instruction(0x80027648) == 0x20010030
        # Count-zero sky still speculatively reads the first pointer in the
        # branch delay slot; the emitted owner includes a zero guard word.
        assert instruction(0x8001304C) == 0x1840000C
        assert instruction(0x8004F09C) == 0x116C0017
        assert instruction(0x8004F0A0) == 0x8D6E0000
        assert instruction(0x80025B08) == 0x8E3C0018
        image.seek(0)
        assert hashlib.file_digest(image, "sha256").hexdigest() == "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37"
    print("EXACT_SOURCE_NATIVE_SURFACE_CONSUMERS_OK")
elif len(sys.argv) != 1:
    raise SystemExit("Usage: source_surface_oracle.py [original.bin]")
