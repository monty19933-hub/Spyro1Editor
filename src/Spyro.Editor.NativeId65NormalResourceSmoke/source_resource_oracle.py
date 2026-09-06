"""Independent raw-sector resource assembly; no editor DLL, ROM or payload writes."""
import hashlib
import importlib.util
import pathlib
import struct
import sys


def word(data, at):
    return struct.unpack_from("<I", data, at)[0]


def half(data, at):
    return struct.unpack_from("<H", data, at)[0]


def sha(data):
    return hashlib.sha256(data).hexdigest()


def texture_fields(package, tables):
    result = []
    for table in tables:
        at, end = table + 4, table + 4 + word(package, table)
        while at < end:
            command = word(package, at)
            if not command & 0x80000000:
                size, field = (20, 8) if command & 2 else (8, None)
            elif command & 4:
                size, field = 20, 8
            else:
                size, field = (24, 12) if command & 2 else (12, None)
            assert at + size <= end
            if field is not None:
                result.append(at + field)
            at += size
        assert at == end
    return result


def main(path):
    sys.dont_write_bytecode = True
    surface_path = pathlib.Path(__file__).resolve().parents[1] / "Spyro.Editor.NativeId65AuthoredSurfaceSmoke/source_surface_oracle.py"
    saved = sys.argv
    sys.argv = [str(surface_path)]
    spec = importlib.util.spec_from_file_location("surface_oracle", surface_path)
    surface = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(surface)
    sys.argv = saved
    with open(path, "rb") as image:
        def original():
            image.seek(0, 2)
            assert image.tell() == 661547040
            image.seek(0)
            assert hashlib.file_digest(image, "sha256").hexdigest() == "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37"

        def wad(at, length):
            output = bytearray()
            while length:
                n = min(length, 2048 - at % 2048)
                image.seek((37 + at // 2048) * 2352 + 24 + at % 2048)
                chunk = image.read(n)
                assert len(chunk) == n
                output += chunk
                at += n
                length -= n
            return bytes(output)

        original()
        data = wad(0x119E000, 0x2E2000)
        pete = wad(0x7B9000, 0x32E1C)
        assert sha(data) == "7ddbf6d9a6ee6c0f64c8564a89e374ca0ca234ef608ec812aae68e8176de1dc0"
        assert sha(pete) == "46f570bf0ad2d433f8b42874ae40a3e5d7155d7206dddd1cd08a2d4be16e0e76"
        stream = data[0x173000:0x17FC30]
        assert sha(stream) == "7c9ca6faf03324ef102fe424e3e60ea7d7b6d54dde9c8382c589f960be9db4e0"
        tables = set()
        for index in range(46):
            pointer = word(pete, 0x38 + 4 * index)
            if pointer != 0xFFFFFFFF:
                tables.add(0xAA8 + word(pete, pointer + 0x14))
                if word(pete, pointer + 0x1C):
                    tables.add(0xAA8 + word(pete, pointer + 0x1C))
        cursor, ids = 0, []
        while word(stream, cursor) != 0xFFFFFFFF:
            ids.append(word(stream, cursor))
            assert word(stream, cursor + 8 + 0x14) == 0
            assert word(stream, cursor + 8 + 0x1C) == 0
            cursor += 8 + word(stream, cursor + 4)
        assert cursor == len(stream) - 4 and ids == [18, 19, 29, 40]
        assert tables == {0xAA8}
        fields = texture_fields(pete, sorted(tables))
        assert len(fields) == 355 and {half(pete, p + 6) for p in fields} == {0x1F}
        assert len({half(pete, p + 2) for p in fields}) == 17
        atlas = data[0x800:0x80800]
        upload = bytearray(0xDE000)
        protected = bytearray(0x80000)

        def copy_source(at, n):
            assert 0 <= at < at + n <= 0x80000
            upload[at:at + n] = atlas[at:at + n]
            protected[at:at + n] = bytes([1]) * n

        def footprint(descriptor):
            page, clut = half(descriptor, 6), half(descriptor, 2)
            assert page & ~0x7F == 0 and page & 15 >= 8
            us = [descriptor[i] for i in (0, 4, 8, 10)]
            vs = [descriptor[i] for i in (1, 5, 9, 11)]
            for v in range(min(vs), max(vs) + 1):
                at = (((page >> 4) & 1) * 256 + v) * 1024 + ((page & 15) * 64 - 512) * 2 + min(us) // 2
                copy_source(at, max(us) // 2 - min(us) // 2 + 1)
            copy_source((clut >> 6) * 1024 + ((clut & 63) * 16 - 512) * 2, 32)

        for field in fields:
            footprint(pete[field:field + 12])
        particle = data[0x172590:0x172610]
        assert word(particle, 0) == 0x80 and word(particle, 4) == 10
        for index in range(10):
            at = 8 + 12 * index
            assert half(particle, at + 2) == 8
            frame = particle[at + 4:at + 12]
            footprint(frame + bytes([frame[0], frame[5], frame[4], frame[1]]))
        source_bytes = sum(protected)
        # Fresh detached companion allocator chose page8 U0/V0 and CLUT33.
        package = bytearray(data[0x1C9C7C:0x1CC5AC])
        for field in texture_fields(package, (0x3F8, 0x1EA8, 0x23CC, 0x7B0, 0x2260, 0x2784)):
            assert half(package, field + 2) == 0x7A36 and half(package, field + 6) == 0x1D
            for at in (0, 4, 8, 10):
                package[field + at] -= 64
                package[field + at + 1] -= 96
            struct.pack_into("<H", package, field + 2, 33)
            struct.pack_into("<H", package, field + 6, 8)
        assert package[4:6] == bytes([72, 73])
        for row in range(32):
            assert not any(protected[row * 1024:row * 1024 + 16])
            upload[row * 1024:row * 1024 + 16] = atlas[0x582A0 + row * 1024:0x582B0 + row * 1024]
        assert not any(protected[32:64])
        upload[32:64] = atlas[0x7A2C0:0x7A2E0]
        upload[0x80000:] = data[0x80800:0xDE800]
        model = stream + package + bytes(0x2A0)
        assert len(model) == 0xF800
        for rgb in (0x708050, 0xE03040):
            fresh = surface.fresh(8448, 8448, 8704, 1024, 1024, rgb, 0x406080)
            tail = fresh + bytes(4) + particle + data[0x172610:0x172D08]
            assert len(tail) == 0x8C0
            print(f"NATIVE_ID65_NORMAL_RESOURCE_ORACLE rgb={rgb:06x} upload={sha(upload)} tail={sha(tail)} model={sha(model)} source_vram_bytes={source_bytes}")
        original()
        print("NATIVE_ID65_NORMAL_RESOURCE_RAW_SOURCE_ORACLE_OK complete_chain=false publication=false")


if __name__ == "__main__":
    if len(sys.argv) != 2:
        raise SystemExit("Usage: source_resource_oracle.py original.bin")
    main(sys.argv[1])
