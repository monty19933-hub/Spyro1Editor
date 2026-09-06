"""Independent exact-source body/header fixture. No DLL, native runtime or writes.

Uses the prior independent floor encoder and package-field reader, not any
production C# producer. Reassembles every normal block and retains four exact
auxiliary allocations, then emits only digests/metadata to stdout.
"""
import hashlib
import importlib.util
import json
import pathlib
import re
import struct
import sys

sys.dont_write_bytecode = True
ROOT = pathlib.Path(__file__).resolve().parents[1]


def module(relative, name):
    path = ROOT / relative
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec)
    previous = sys.argv
    sys.argv = [str(path)]
    try:
        spec.loader.exec_module(result)
    finally:
        sys.argv = previous
    return result


def sha(value):
    return hashlib.sha256(value).hexdigest()


def main(path):
    surface = module("Spyro.Editor.NativeId65AuthoredSurfaceSmoke/source_surface_oracle.py", "body_surface")
    model_reader = module("Spyro.Editor.NativeId65NormalResourceSmoke/source_resource_oracle.py", "body_model")
    with open(path, "rb") as image:
        def original():
            image.seek(0, 2)
            assert image.tell() == 661547040
            image.seek(0)
            assert hashlib.file_digest(image, "sha256").hexdigest() == (
                "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37")
            assert image.tell() == 661547040
            image.seek(0, 2)
            assert image.tell() == 661547040

        def read(lba, offset, length):
            result = bytearray()
            while length:
                amount = min(length, 2048 - offset % 2048)
                image.seek((lba + offset // 2048) * 2352 + 24 + offset % 2048)
                part = image.read(amount)
                assert len(part) == amount
                result += part
                offset += amount
                length -= amount
            return bytes(result)

        original()
        exe = read(53875, 0, 0x66000)
        town = read(37, 0x119E000, 0x2E2000)
        overlay = read(37, 0x118E800, 0xF800)
        assert sha(exe) == "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998"
        assert sha(town) == "7ddbf6d9a6ee6c0f64c8564a89e374ca0ca234ef608ec812aae68e8176de1dc0"
        assert sha(overlay) == "9bc923cc8d27703537b81b01f51fde351e58aabee87d38a8afc630f1031878a5"
        pin_source = pathlib.Path(__file__).with_name("NativeBodySourceWindows.cs").read_text()
        pins = re.findall(r'new\("([^"]+)", (0x[0-9A-F]+), "([0-9a-f]+)", "([0-9a-f]{64})"\)', pin_source)
        assert len(pins) == 16
        for name, address, encoded, digest in pins:
            raw = bytes.fromhex(encoded)
            offset = int(address, 16) - 0x8000F800
            assert sha(raw) == digest and exe[offset:offset + len(raw)] == raw, name

        atlas = town[0x800:0x80800]
        tiledefs = town[0x1D0010:0x1D0088]
        upload = bytearray(0xDE000)
        owners = bytearray(0x80000)

        def retain(offset, length):
            assert 0 <= offset < offset + length <= 0x80000
            assert 2 not in owners[offset:offset + length]
            upload[offset:offset + length] = atlas[offset:offset + length]
            owners[offset:offset + length] = bytes([1]) * length

        for index in range(15):
            raw = tiledefs[index * 8:index * 8 + 8]
            if index == 13:
                assert raw == bytes(8)
                continue
            u0, v0, clut, u1, v1, page = struct.unpack("<BBHBBH", raw)
            assert page & 0x8000 and page & 0x7F00 == 0
            mode = (page >> 7) & 1
            x, y = (page & 15) * 64, ((page >> 4) & 1) * 256
            words = 64 << mode
            assert x >= 512 and x + words <= 1024
            for row in range(256):
                retain((y + row) * 1024 + (x - 512) * 2, words * 2)
            cx, cy = (clut & 63) * 16, clut >> 6
            colors = 16 if mode == 0 else 256
            assert cx >= 512 and cx + colors <= 1024 and cy + 8 <= 512
            for row in range(8):
                retain((cy + row) * 1024 + (cx - 512) * 2, colors * 2)
        for index in range(10):
            raw = town[0x17259C + 12 * index:0x1725A4 + 12 * index]
            u0, v0, clut, u1, v1, page = struct.unpack("<BBHBBH", raw)
            assert page & ~0x7F == 0 and page & 15 >= 8
            x, y = (page & 15) * 64, ((page >> 4) & 1) * 256
            for row in range(min(v0, v1), max(v0, v1) + 1):
                retain((y + row) * 1024 + (x - 512) * 2 + min(u0, u1) // 2,
                       max(u0, u1) // 2 - min(u0, u1) // 2 + 1)
            retain((clut >> 6) * 1024 + ((clut & 63) * 16 - 512) * 2, 32)
        for row in range(32):
            offset = row * 1024
            assert owners[offset:offset + 16] == bytes(16)
            upload[offset:offset + 16] = atlas[0x582A0 + offset:0x582B0 + offset]
            owners[offset:offset + 16] = bytes([2]) * 16
        assert owners[32:64] == bytes(32)
        upload[32:64] = atlas[0x7A2C0:0x7A2E0]
        upload[0x80000:] = town[0x80800:0xDE800]
        assert sha(upload) == "b769a9a298285aa402956fdbd5721132736388c6f3f1dc1bfcd7e15a9afeb66d"

        package = bytearray(town[0x1C9C7C:0x1CC5AC])
        fields = model_reader.texture_fields(package, (0x3F8, 0x1EA8, 0x23CC, 0x7B0, 0x2260, 0x2784))
        assert len(fields) == 36
        for field in fields:
            assert struct.unpack_from("<H", package, field + 2)[0] == 0x7A36
            assert struct.unpack_from("<H", package, field + 6)[0] == 0x1D
            for at in (0, 4, 8, 10):
                package[field + at] -= 64
                package[field + at + 1] -= 96
            struct.pack_into("<H", package, field + 2, 33)
            struct.pack_into("<H", package, field + 6, 8)
        assert package[4:6] == bytes([72, 73])
        model = town[0x173000:0x17FC30] + package + bytes(0x2A0)
        assert sha(model) == "88c78343c283256de6ce8d3151b43fa72ba4d96b046d869bd0a7209cbff4c23d"
        data = struct.pack("<II", 8, 0) + surface.fresh(8448, 8448, 8704, 1024, 1024, 0x708050, 0x406080)
        data += bytes(4) + town[0x172590:0x172D08]
        assert len(data) == 0x8C8 and sha(data) == "821d25d4c012aa180d369f755d82a780dbcf244b4e53c6c4bcf349da533dce65"
        padded_data = data + bytes((-len(data)) % 0x800)
        auxiliary = []
        for index in range(4, 8):
            offset, length = struct.unpack_from("<II", town, 8 * index)
            auxiliary.append(town[offset:offset + length])
        assert sum(map(len, auxiliary)) == 0x109800
        lists = b"".join(struct.pack("<II", size, 0) + bytes(size - 8) for size in [12, 12, 8, 8, 12, 8, 8])
        results = []
        for x, heading in [(8960, 64), (8976, 128)]:
            arena = 0x70 * 16 + 0x58
            scene = struct.pack("<iiiBBBB", x, 8960, 9216, 0, 0, heading, 0) + tiledefs + lists
            scene += struct.pack("<II", 8 + arena, 0) + bytes(arena)
            scene += struct.pack("<IIII", 4, 8, 0, 0x2004) + bytes(0x2000) + bytes(4)
            scene += bytes((-len(scene)) % 0x800)
            assert len(scene) == 0x3000
            header = bytearray(0x800)
            parts = [upload, padded_data, model, scene, *auxiliary]
            cursor = 0x800
            for index, part in enumerate(parts):
                struct.pack_into("<II", header, 8 * index, cursor, len(part))
                cursor += len(part)
            struct.pack_into("<II", header, 0x50, 0xDF800, 0xEC430)
            struct.pack_into("<H", header, 0x152, 0x78)
            body = header + b"".join(parts)
            assert len(body) == cursor == 0x1FB800
            results.append(dict(x=x, heading=heading, bytes=len(body), body_sha256=sha(body),
                                header_sha256=sha(header), scene_sha256=sha(scene)))
        original()
        print(json.dumps(dict(source_before_after_verified=True, scalar_source_pins=len(pins),
                              outputs=results, overlay_sha256=sha(overlay),
                              native_executed=False, runtime_verified=False, publication_approved=False), indent=2))


if __name__ == "__main__":
    if len(sys.argv) != 2:
        raise SystemExit("Usage: source_normal_body_oracle.py original.bin")
    main(sys.argv[1])
