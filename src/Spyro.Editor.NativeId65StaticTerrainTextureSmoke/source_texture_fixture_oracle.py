"""Read exact raw sectors and verify C# fixtures independently of editor DLLs.

No source/output writes, allocation approval, renderer emulation or runtime claim.
Usage: python3 source_texture_fixture_oracle.py /path/to/original.bin
"""
import hashlib
import json
from pathlib import Path
import re
import struct
import sys

EXPECTED = "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37"
LENGTH = 661547040
root = Path(__file__).resolve().parent
source = Path(sys.argv[1]).resolve(strict=True)
scalar = (root / "NativeScalarCases.cs").read_text()
records = (root / "StaticSourceCases.cs").read_text()


def fixture(text, name):
    matches = re.findall(r'const string ' + name + r' = "([0-9a-f]+)";', text)
    assert len(matches) == 1, name
    return bytes.fromhex(matches[0])


def logical(image, lba, offset, length):
    result = bytearray()
    while length:
        take = min(length, 2048 - offset % 2048)
        image.seek((lba + offset // 2048) * 2352 + 24 + offset % 2048)
        piece = image.read(take)
        assert len(piece) == take
        result.extend(piece)
        length -= take
        offset += take
    return bytes(result)


def identity(image):
    assert source.stat().st_size == LENGTH
    image.seek(0)
    assert hashlib.file_digest(image, "sha256").hexdigest() == EXPECTED
    assert image.tell() == LENGTH and source.stat().st_size == LENGTH


windows, donors = [], []
with source.open("rb") as image:
    identity(image)
    for name, pc in [
        ("ClipInitializationHex", 0x80012CB4), ("LoaderHex", 0x80012D58),
        ("BaseHex", 0x800267A8), ("FadePreparationHex", 0x80027334),
        ("ChoiceHex", 0x80027244), ("PacketHex", 0x80027508),
    ]:
        expected = fixture(scalar, name)
        actual = logical(image, 53875, pc - 0x8000F800, len(expected))
        assert actual == expected, name
        windows.append({"name": name, "pc": hex(pc), "exeOffset": hex(pc - 0x8000F800),
                        "length": len(actual), "sha256": hashlib.sha256(actual).hexdigest()})
    assert logical(image, 53875, 0x800267B8 - 0x8000F800, 4) == bytes.fromhex("0780033c")
    assert logical(image, 53875, 0x800274A4 - 0x8000F800, 12) == bytes.fromhex("0034013c2528a1000400c5af")
    table = logical(image, 37, 0, 0x800)
    for name, entry, golden in [
        ("DarkRecord", 14, "7b074611db43208653a931fb12c98ee64dbc5f8f41ef4edd7b1e5e796a4e0954"),
        ("GnastyRecord", 70, "2ffdf4bc69645c31a1cc75862288bf3620955567ef65e35b2f8f65862b7dda72"),
    ]:
        offset, size = struct.unpack_from("<II", table, entry * 8)
        data = logical(image, 37, offset, size)
        metadata, scene = struct.unpack_from("<I", data, 8)[0], struct.unpack_from("<I", data, 24)[0]
        count = struct.unpack_from("<I", data, metadata + 4)[0]
        assert 0 < count <= 128
        assert struct.unpack_from("<I", data, metadata)[0] == 8 + count * 184
        assert data[scene + 0x88:scene + 0x98] == bytes.fromhex("08000000000000000800000000000000")
        low, high = metadata + 8, metadata + 8 + count * 16
        record = data[low:low + 16] + data[high:high + 168]
        assert record == fixture(records, name)
        assert hashlib.sha256(record).hexdigest() == golden
        donors.append({"entry": entry, "nativeId": 0, "lqWadOffset": hex(offset + low),
                       "hqWadOffset": hex(offset + high), "recordSha256": golden})
    identity(image)
print("NATIVE_STATIC_TEXTURE_SOURCE_FIXTURE_ORACLE_OK " + json.dumps({
    "sourceSha256BeforeAndAfter": EXPECTED, "windows": windows, "donors": donors,
    "claim": "Exact source fixture readback only; no destination, HQ, clip runtime or publication approval."}))
