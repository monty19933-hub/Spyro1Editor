"""Independent raw-sector paired data/list oracle; no editor DLL or ROM writes."""
import hashlib
import importlib.util
import pathlib
import struct
import sys


def sha(data):
    return hashlib.sha256(data).hexdigest()


def main(path):
    sys.dont_write_bytecode = True
    surface_path = pathlib.Path(__file__).resolve().parents[1] / "Spyro.Editor.NativeId65AuthoredSurfaceSmoke/source_surface_oracle.py"
    spec = importlib.util.spec_from_file_location("surface_oracle", surface_path)
    surface = importlib.util.module_from_spec(spec)
    saved_arguments = sys.argv
    sys.argv = [str(surface_path)]
    spec.loader.exec_module(surface)
    sys.argv = saved_arguments
    with open(path, "rb") as image:
        def original():
            image.seek(0, 2)
            assert image.tell() == 661547040
            image.seek(0)
            assert hashlib.file_digest(image, "sha256").hexdigest() == "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37"

        def read(lba, offset, length):
            output = bytearray()
            while length:
                amount = min(length, 2048 - offset % 2048)
                image.seek((lba + offset // 2048) * 2352 + 24 + offset % 2048)
                chunk = image.read(amount)
                assert len(chunk) == amount
                output += chunk
                offset += amount
                length -= amount
            return bytes(output)

        original()
        executable = read(53875, 0, 0x66000)
        assert sha(executable) == "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998"
        spans = [
            (0x4440, 0x2E4, "3d5c033d15f4e510c4326b9699c0b4b6d5def3cd3c288f528e0c7f823274b80e"),
            (0x1AEFC, 0xC94, "040a3b6ab61a418501453135f4b7cf41b94e57081377f301730f93d68970166b"),
            (0x1BCAC, 0x520, "b71153d2ffb77871623e5f3f85e9adb9e4793d66368eee9d30b7fd96bef74354"),
            (0x24250, 0x60, "12f23f264ca3b413c8a05ecba7a428f56bff1f127b6ad24f734f60bd002d7e97"),
            (0x3558, 0x54, "25aede725485af9da35013da9c1adbcb8d4f336c588c272e29edb6c7115aca0c"),
        ]
        for offset, length, expected in spans:
            assert sha(executable[offset:offset + length]) == expected
        for branch in [0x8002A718, 0x8002A850, 0x8002AD68, 0x8002B4C4, 0x8002B57C, 0x8002B7D0]:
            words = struct.unpack_from("<II", executable, branch - 0x8000F800)
            assert words[0] >> 26 == 4 and words[1] == 0x8DCD0000
        data = read(37, 0x119E000, 0x2E2000)
        assert sha(data) == "7ddbf6d9a6ee6c0f64c8564a89e374ca0ca234ef608ec812aae68e8176de1dc0"
        lengths = [12, 12, 8, 8, 12, 8, 8]
        lists = b"".join(struct.pack("<II", size, 0) + bytes(size - 8) for size in lengths)
        assert len(lists) == 0x44
        cursor = 0
        for index, size in enumerate(lengths):
            assert struct.unpack_from("<II", lists, cursor) == (size, 0)
            assert (size == 12) == (index in [0, 1, 4])
            cursor += size
        assert 0x88 + cursor == 0xCC
        for rgb in [0x708050, 0xE03040]:
            fresh = surface.fresh(8448, 8448, 8704, 1024, 1024, rgb, 0x406080)
            tail = fresh + bytes(4) + data[0x172590:0x172D08]
            paired = struct.pack("<II", 8, 0) + tail
            assert len(paired) == 0x8C8 and paired[8:] == tail
            print(f"NATIVE_ID65_SCENE_LIST_RAW_ORACLE rgb={rgb:06x} data={sha(paired)} lists={sha(lists)} bytes={len(paired)}/{len(lists)}")
        original()
        print("NATIVE_ID65_SCENE_LIST_RAW_SOURCE_ORACLE_OK scene_initialized=false level=false publication=false")


if __name__ == "__main__":
    if len(sys.argv) != 2:
        raise SystemExit("Usage: source_scene_list_oracle.py original.bin")
    main(sys.argv[1])
