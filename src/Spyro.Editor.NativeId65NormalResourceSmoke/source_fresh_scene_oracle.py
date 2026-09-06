"""Independent fresh-scene/header-page assembly. Read-only; no DLL or payload writes."""
import hashlib
import importlib.util
import pathlib
import struct
import sys

sys.dont_write_bytecode = True


def sha(data):
    return hashlib.sha256(data).hexdigest()


def main(path):
    helper_path = pathlib.Path(__file__).with_name("source_resource_oracle.py")
    spec = importlib.util.spec_from_file_location("normal_resource_oracle", helper_path)
    helper = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(helper)
    with open(path, "rb") as image:
        def original():
            image.seek(0, 2)
            assert image.tell() == 661547040
            image.seek(0)
            assert hashlib.file_digest(image, "sha256").hexdigest() == "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37"
            assert image.tell() == 661547040
            image.seek(0, 2)
            assert image.tell() == 661547040

        def read(lba, offset, length):
            output = bytearray()
            while length:
                amount = min(length, 2048 - offset % 2048)
                image.seek((lba + offset // 2048) * 2352 + 24 + offset % 2048)
                piece = image.read(amount)
                assert len(piece) == amount
                output += piece
                offset += amount
                length -= amount
            return bytes(output)

        original()
        executable = read(53875, 0, 0x66000)
        data = read(37, 0x119E000, 0x2E2000)
        player = read(37, 0x7B9000, 0x32E1C)
        assert sha(executable) == "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998"
        assert sha(data) == "7ddbf6d9a6ee6c0f64c8564a89e374ca0ca234ef608ec812aae68e8176de1dc0"
        assert sha(player) == "46f570bf0ad2d433f8b42874ae40a3e5d7155d7206dddd1cd08a2d4be16e0e76"
        header = data[0x1D0000:0x1D0088]
        assert sha(header) == "1ba95c48a81510329cacf0b50b2de56fdc134d777efdb8cdddd09984c968da02"
        for offset, length, digest in [
            (0x4368, 0x14, "2ffd895e4284cdb5afd65390cfec5b27f21c3bee059380c9f98aba8724a04c85"),
            (0x7114, 0x1C, "572bd776ba509d54a3848293f5cf78731da439e3b203d100b9cb6fb2ec60decc"),
            (0x49A60, 0x24, "c4743b1846b8c358f06432bf40ce1c0627e30f6dc0b4d96246ddd1237a43405b"),
            (0x11DA8, 0x1A0, "22d2f2acf631d0483fdd3a21a95fdf90520649fbcb95b6426c2a11f2f5e97677"),
            (0x42CC4, 0xA4, "6625282792e81215f1f35e1aa8b4240937206bcc8bd494eef9e000f65a4bbf9e"),
        ]:
            assert sha(executable[offset:offset + length]) == digest
        atlas = data[0x800:0x80800]
        upload = bytearray(0xDE000)
        ownership = bytearray(0x80000)

        def retain(offset, length):
            assert 0 <= offset < offset + length <= 0x80000
            assert not any(value == 2 for value in ownership[offset:offset + length])
            upload[offset:offset + length] = atlas[offset:offset + length]
            ownership[offset:offset + length] = bytes([1]) * length

        # Existing complete player reference union lies within this exact
        # page1F and its palette coordinates; header owners retain the full page.
        for index in range(15):
            raw = header[0x10 + index * 8:0x18 + index * 8]
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
        # Independently confirm the retained player references really are
        # subsumed by those full header pages/CLUTs, including zero pixels.
        tables = set()
        for animation in range(46):
            pointer = helper.word(player, 0x38 + 4 * animation)
            if pointer != 0xFFFFFFFF:
                tables.add(0xAA8 + helper.word(player, pointer + 0x14))
                if helper.word(player, pointer + 0x1C):
                    tables.add(0xAA8 + helper.word(player, pointer + 0x1C))
        assert tables == {0xAA8}
        fields = helper.texture_fields(player, sorted(tables))
        assert len(fields) == 355
        for field in fields:
            raw = player[field:field + 12]
            assert helper.half(raw, 6) == 0x1F
            us, vs = [raw[i] for i in (0, 4, 8, 10)], [raw[i] for i in (1, 5, 9, 11)]
            for row in range(min(vs), max(vs) + 1):
                offset = (256 + row) * 1024 + 896 + min(us) // 2
                length = max(us) // 2 - min(us) // 2 + 1
                assert ownership[offset:offset + length] == bytes([1]) * length
            palette = helper.half(raw, 2)
            offset = (palette >> 6) * 1024 + ((palette & 63) * 16 - 512) * 2
            assert ownership[offset:offset + 32] == bytes([1]) * 32
        # Preserve the prior ten exact particle-frame0 footprints, which are
        # mostly page1D and therefore not subsumed by the header's page1F.
        for index in range(10):
            raw = data[0x172590 + 12 + index * 12:0x172590 + 20 + index * 12]
            u0, v0, clut, u1, v1, page = struct.unpack("<BBHBBH", raw)
            assert page & ~0x7F == 0 and page & 15 >= 8
            x, y = (page & 15) * 64, ((page >> 4) & 1) * 256
            for row in range(min(v0, v1), max(v0, v1) + 1):
                retain((y + row) * 1024 + (x - 512) * 2 + min(u0, u1) // 2,
                       max(u0, u1) // 2 - min(u0, u1) // 2 + 1)
            retain((clut >> 6) * 1024 + ((clut & 63) * 16 - 512) * 2, 32)
        for row in range(32):
            offset = row * 1024
            assert ownership[offset:offset + 16] == bytes(16)
            upload[offset:offset + 16] = atlas[0x582A0 + offset:0x582B0 + offset]
            ownership[offset:offset + 16] = bytes([2]) * 16
        assert ownership[32:64] == bytes(32)
        upload[32:64] = atlas[0x7A2C0:0x7A2E0]
        ownership[32:64] = bytes([2]) * 32
        upload[0x80000:] = data[0x80800:0xDE800]

        lists = b"".join(struct.pack("<II", size, 0) + bytes(size - 8) for size in [12, 12, 8, 8, 12, 8, 8])
        for x, heading in [(8960, 64), (8976, 128)]:
            slots = 16
            arena = 0x70 * slots + 0x58
            scene = bytearray(struct.pack("<iiiBBBB", x, 8960, 9216, 0, 0, heading, 0))
            scene += header[0x10:0x88] + lists
            assert len(scene) == 0xCC
            scene += struct.pack("<II", 8 + arena, 0) + bytes(arena)
            scene += struct.pack("<I", 4)  # empty placed properties
            scene += struct.pack("<II", 8, 0)  # empty numeric/pod list
            scene += struct.pack("<I", 0x2004) + bytes(0x2000)  # spatial buckets
            scene += struct.pack("<I", 0)  # no scene pointer fixups
            assert len(scene) == 0x2840 and arena // 0x70 == slots
            scene += bytes((-len(scene)) % 0x800)
            assert len(scene) == 0x3000
            expected_scene = "4a6e7b5c6c53199b8a7aa6ae0d24ac9bce32634aa259d5f49535282080641433" if x == 8960 else "268b36fdb7136d533c01a93a71fd62a71bc9ec58799558698b14ba1f21b18380"
            assert sha(scene) == expected_scene
            assert sha(upload) == "b769a9a298285aa402956fdbd5721132736388c6f3f1dc1bfcd7e15a9afeb66d"
            assert ownership.count(1) == 104576
            print(f"NATIVE_FRESH_SCENE_RAW_ORACLE x={x} heading={heading} scene={sha(scene)} upload={sha(upload)} source_vram_bytes={ownership.count(1)}")
        original()
        print("NATIVE_FRESH_SCENE_RAW_ORACLE_OK source_partial=true superflame_unavailable=true runtime=false publication=false")


if __name__ == "__main__":
    if len(sys.argv) != 2:
        raise SystemExit("Usage: source_fresh_scene_oracle.py original.bin")
    main(sys.argv[1])
