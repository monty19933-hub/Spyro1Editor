"""Read-only independent raw-sector byte oracle; no .NET/helpers/candidate writes."""

import hashlib
import os
import struct
import sys

SOURCE_SHA256 = "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37"
SOURCE_LENGTH = 661547040
CASES = (
    (40, (8,), "25e1a8d8c74a4c9e22596a72847ddd3b8dde155022d38221317d5204a9cc5f5e"),
    (78, (33, 34, 35), "251770f0eade8981fd6fca94d3a0bf434baeaff23ee83520dda0bf9d78079c5b"),
)


def word(data, offset):
    return struct.unpack_from("<I", data, offset)[0]


def require_source(image):
    assert os.fstat(image.fileno()).st_size == SOURCE_LENGTH, "source length"
    image.seek(0)
    digest = hashlib.file_digest(image, "sha256").hexdigest()
    assert digest == SOURCE_SHA256, "source SHA-256"
    assert image.tell() == SOURCE_LENGTH, "complete source hash"
    assert os.fstat(image.fileno()).st_size == SOURCE_LENGTH, "source length changed"


def read_wad(image, offset, length):
    assert 0 <= offset <= 0x6927000 and 0 <= length <= 0x6927000 - offset
    output = bytearray()
    while length:
        sector, within = divmod(offset, 2048)
        size = min(length, 2048 - within)
        image.seek((37 + sector) * 2352 + 24 + within)
        block = image.read(size)
        assert len(block) == size, "truncated raw sector"
        output.extend(block)
        offset += size
        length -= size
    return output


def main(path):
    with open(path, "rb") as image:
        require_source(image)
        directory = read_wad(image, 0, 2048)

        def level(entry):
            return read_wad(image, word(directory, entry * 8), word(directory, entry * 8 + 4))

        destination = level(18)
        for donor_entry, ids, expected in CASES:
            donor = level(donor_entry)
            cursor = word(donor, 0x50)
            terminal = word(donor, 0x54) - 4
            records = {}
            while cursor < terminal:
                animation = word(donor, cursor)
                length = word(donor, cursor + 4) + 8
                assert animation < 46 and animation not in records
                assert length >= 8 and cursor + length <= terminal
                records[animation] = donor[cursor:cursor + length]
                cursor += length
            assert cursor == terminal and word(donor, terminal) == 0xFFFFFFFF
            added = b"".join(records[animation] for animation in ids)
            exact = len(added)
            rounded = (exact + 2047) // 2048 * 2048
            insertion = word(destination, 0x54) - 4
            model_end = word(destination, 0x10) + word(destination, 0x14)
            # Deliberately direct source-range splice, independent of the Core map.
            final = (destination[:insertion] + added + destination[insertion:model_end]
                     + bytes(rounded - exact) + destination[model_end:])
            for slot in range(10):
                if word(destination, slot * 8) == 0:
                    continue
                if slot == 2:
                    struct.pack_into("<I", final, slot * 8 + 4, word(destination, slot * 8 + 4) + rounded)
                elif slot > 2:
                    struct.pack_into("<I", final, slot * 8, word(destination, slot * 8) + rounded)
            for slot in range(1, 64):
                root = word(destination, 0x50 + slot * 4)
                if root:
                    struct.pack_into("<I", final, 0x50 + slot * 4, root + exact)
            digest = hashlib.sha256(final).hexdigest()
            assert digest == expected, "whole-level byte golden"
            print("NATIVE_PLAYER_ANIMATION_INDEPENDENT_SPLICE_OK",
                  f"ids={','.join(map(str, ids))} exact=0x{exact:X} sector=0x{rounded:X} sha256={digest}")
        require_source(image)


if __name__ == "__main__":
    if len(sys.argv) != 2:
        raise SystemExit("Usage: source_splice_oracle.py <original.bin>")
    main(sys.argv[1])
