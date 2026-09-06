"""Read-only independent exact-source native asset splice; no editor DLL or output writes."""
import hashlib
import struct
import sys


def sha(data):
    return hashlib.sha256(data).hexdigest()


def word(data, offset):
    return struct.unpack_from("<I", data, offset)[0]


def require(value, message):
    if not value:
        raise ValueError(message)


def main(path):
    with open(path, "rb") as image:
        def original():
            image.seek(0, 2)
            require(image.tell() == 661547040, "original image length changed")
            image.seek(0)
            digest = hashlib.file_digest(image, "sha256").hexdigest()
            require(image.tell() == 661547040, "original image length changed")
            require(digest == "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37", "original image hash changed")

        def wad(offset, length):
            result = bytearray()
            while length:
                count = min(length, 2048 - offset % 2048)
                image.seek((37 + offset // 2048) * 2352 + 24 + offset % 2048)
                chunk = image.read(count)
                require(len(chunk) == count, "truncated raw sector")
                result.extend(chunk)
                offset += count
                length -= count
            return bytes(result)

        original()
        data = wad(0x119E000, 0x2E2000)
        require(sha(data) == "7ddbf6d9a6ee6c0f64c8564a89e374ca0ca234ef608ec812aae68e8176de1dc0", "Town data owner")
        package = bytearray(data[0x1C9C7C:0x1CC5AC])
        require(sha(package) == "8e0d1cf58e8b164734f4aa3e91a60a1d04e78bb1f5e4cd5c4432b2e3e1e743f1", "complete class78 registered owner")
        fields = []
        for table in (0x3F8, 0x1EA8, 0x23CC, 0x7B0, 0x2260, 0x2784):
            end = table + 4 + word(package, table)
            cursor = table + 4
            while cursor < end:
                command = word(package, cursor)
                if not command & 0x80000000:
                    size, texture = (20, 8) if command & 2 else (8, None)
                elif command & 4:
                    size, texture = 20, 8
                else:
                    size, texture = (24, 12) if command & 2 else (12, None)
                require(cursor + size <= end, "command exceeds native table")
                if texture is not None:
                    fields.append(cursor + texture)
                cursor += size
            require(cursor == end, "face table exact end")
        require(len(fields) == 36, "complete texture reference count")
        for field in fields:
            require(struct.unpack_from("<H", package, field + 2)[0] == 0x7A36 and
                    struct.unpack_from("<H", package, field + 6)[0] == 0x1D, "source page/CLUT")
            for at in (0, 4, 8, 10):
                require(64 <= package[field + at] <= 95 and 96 <= package[field + at + 1] <= 127, "source UV owner")
                package[field + at] -= 64
                package[field + at + 1] -= 96
            struct.pack_into("<H", package, field + 2, 33)
            struct.pack_into("<H", package, field + 6, 8)
        package[4:6] = b"\x00\x01"
        pixels = b"".join(data[0x800 + 0x582A0 + row * 1024:0x800 + 0x582A0 + row * 1024 + 16] for row in range(32))
        palette = data[0x800 + 0x7A2C0:0x800 + 0x7A2C0 + 32]
        descriptors = bytearray(data[0x172610 + 0x108 + 72 * 20:0x172610 + 0x108 + 74 * 20])
        samples = bytearray()
        for index, expected_length in enumerate((0xC60, 0x1990)):
            offset = word(descriptors, index * 20)
            sample = data[0x80800 + offset:0x80800 + offset + expected_length]
            require(sample[-15] == 1 and all(sample[i + 1] == 0 for i in range(0, len(sample) - 16, 16)), "complete native one-shot frames")
            struct.pack_into("<I", descriptors, index * 20, len(samples))
            samples.extend(sample)
        block = package + pixels + palette + descriptors + samples
        require(len(block) == 0x5168, "complete detached block length")
        require(sha(block) == "0a7bcb56eaedded6878ae739aae17d8cb3857eaf74edec52e6f8c01a1667fb2a", "native byte-splice golden")
        original()
        print(f"NATIVE_ID65_COMPANION_ASSET_PYTHON_ORACLE_OK bytes={len(block)} sha={sha(block)} publication=false")


if __name__ == "__main__":
    if len(sys.argv) != 2:
        raise SystemExit("Usage: source_asset_oracle.py original.bin")
    main(sys.argv[1])
