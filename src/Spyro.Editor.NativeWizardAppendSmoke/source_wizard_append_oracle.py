"""Independent raw-sector scene splice; no Core, CPU, placement or publication.

The two equal donor schemas are assigned temporal owners z128 then a129.
Their raw native bytes do not encode the external family strings; JSON project
history remains a separate obligation from this native scene reconstruction.
"""
import hashlib
import json
import struct
import sys


IMAGE_SHA = "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37"


def sha(data):
    return hashlib.sha256(data).hexdigest()


def word(data, offset):
    return struct.unpack_from("<I", data, offset)[0]


def put(data, offset, value):
    struct.pack_into("<I", data, offset, value)


def scene_layout(data, require_zero_tail=True):
    at = 0x88
    for _ in range(7):
        size = word(data, at)
        assert size >= 8 and size % 4 == 0 and at + size <= len(data)
        at += size
    moby = at
    count = word(data, at + 4)
    props = at + word(data, at)
    props_end = props + word(data, props)
    pod_end = props_end + word(data, props_end)
    fixups = pod_end + word(data, pod_end)
    fixup_count = word(data, fixups)
    fixup_end = fixups + 4 + fixup_count * 4
    fields = [word(data, fixups + 4 + i * 4) for i in range(fixup_count)]
    assert len(set(fields)) == len(fields)
    assert count <= 256 and moby + 8 + count * 0x58 <= props
    assert fixup_end <= len(data)
    if require_zero_tail:
        assert not any(data[fixup_end:])
    assert all(field % 4 == 0 and field + 4 <= props_end for field in fields)
    assert all(props + 4 <= word(data, field) < props_end for field in fields)
    return moby, count, props, props_end, fixups, fixup_end, fields


def main(path):
    with open(path, "rb") as image:
        def original():
            image.seek(0, 2)
            assert image.tell() == 661547040
            image.seek(0)
            assert hashlib.file_digest(image, "sha256").hexdigest() == IMAGE_SHA
            assert image.tell() == 661547040
            image.seek(0, 2)
            assert image.tell() == 661547040

        def read(offset, length):
            output = bytearray()
            while length:
                count = min(length, 2048 - offset % 2048)
                image.seek((37 + offset // 2048) * 2352 + 24 + offset % 2048)
                part = image.read(count)
                assert len(part) == count
                output += part
                offset += count
                length -= count
            return bytes(output)

        original()
        header = read(0, 0x800)
        assert (word(header, 14 * 8), word(header, 14 * 8 + 4)) == (0xF01000, 0x28D800)
        assert (word(header, 40 * 8), word(header, 40 * 8 + 4)) == (0x2F6B000, 0x28A000)
        dark = read(0xF01000, 0x28D800)
        donor_data = read(0x2F6B000, 0x28A000)
        assert sha(dark) == "a937aecb3626ffd2311ba136684a34c9f36877054b52557b4524fcacee0e2d97"
        assert sha(donor_data) == "9973493f865b5e86bc17b32c5af9e3d7596e25615618c007ed28e0379161f2d8"
        assert (word(dark, 24), word(dark, 28)) == (0x194000, 0x11800)
        assert (word(donor_data, 24), word(donor_data, 28)) == (0x1CE800, 0x11800)
        source = dark[0x194000:0x1A5800]
        donor = donor_data[0x1CE800:0x1E0000]
        moby, count, props, end, fixups, fixup_end, fields = scene_layout(source)
        # Donor suffix is not allocated/copied by this producer. Do not invent
        # destination-style zero-tail ownership for that unrelated source tail.
        assert sha(donor) == "1e204311534f03f09804d55ca833c62a1a3cc3665dc21d88de743d820103a951"
        donor_layout = scene_layout(donor, require_zero_tail=False)
        row_at = donor_layout[0] + 8 + 6 * 0x58
        assert row_at == 0x6388 and word(donor, row_at) == 0xC350
        row = donor[row_at:row_at + 0x58]
        private = donor[0xC350:0xC3A0]
        assert sha(row) == "59401eafeab453fd9d26f14eedca58390653dc5f4fcc3d2560f7b3e8fc7afa82"
        assert sha(private) == "5b3d762f6117ea30e959a21bf2778f3442b03eba93125c9a0e0a642cc58d345b"
        assert [f for f in donor_layout[-1] if 0xC350 <= f < 0xC3A0] == [0xC350]
        assert [f for f in donor_layout[-1] if row_at <= f < row_at + 0x58] == [row_at]
        assert word(private, 0) == 0xC378 and row[0x43] == 0xFF
        assert (moby, count, props, end, fixups, len(fields)) == (0x8C30, 128, 0xE038, 0xEED4, 0x10EF0, 147)
        outputs = []
        assert (len(source) - fixup_end) // (0x50 + 8) == 19
        assert fixup_end + 20 * (0x50 + 8) > len(source)
        for extra in (1, 2, 19):
            final = bytearray(source)
            delta = extra * len(private)
            final[end + delta:fixups + delta] = source[end:fixups]
            put(final, moby + 4, count + extra)
            put(final, props, word(source, props) + delta)
            new_fields = list(fields)
            owners = []
            for index in range(extra):
                destination_row = moby + 8 + (count + index) * 0x58
                destination_props = end + index * len(private)
                assert not any(source[destination_row:destination_row + 0x58])
                final[destination_row:destination_row + 0x58] = row
                final[destination_props:destination_props + len(private)] = private
                put(final, destination_row, destination_props)
                put(final, destination_props, destination_props + 0x28)
                new_fields += [destination_row, destination_props]
                family_id = ("z", "a")[index] if extra <= 2 else f"boundary-{index:02d}"
                owners.append({"familyId": family_id, "nativeIndex": count + index,
                               "rowOffset": destination_row, "propertiesOffset": destination_props})
            put(final, fixups + delta, len(new_fields))
            for index, field in enumerate(new_fields):
                put(final, fixups + delta + 4 + index * 4, field)
            parsed = scene_layout(final)
            assert parsed[1] == count + extra and parsed[-1] == new_fields
            assert final[moby + 8:moby + 8 + count * 0x58] == source[moby + 8:moby + 8 + count * 0x58]
            assert final[props + 4:end] == source[props + 4:end]
            assert final[end + delta:fixups + delta] == source[end:fixups]
            if extra == 1:
                assert sha(final) == "9ac99fdd150133a75aac8298da4b738dcbcdd12a1ab649088f53c300bd1a37c4"
            outputs.append({"count": count + extra, "sha256": sha(final), "owners": owners,
                            "fixupCount": len(new_fields), "fixupEnd": parsed[-2],
                            "dynamicCapacity": (props - moby - 8 - (count + extra) * 0x58) // 0x70})
        original()
        print(json.dumps({"marker": "NATIVE_WIZARD_APPEND_RAW_ORACLE_OK", "sourceImageSha256": IMAGE_SHA,
                          "sceneByteLength": len(source), "donorLevelId": 33, "donorTrueIndex": 6,
                          "donorRowSha256": sha(row), "donorPrivateSha256": sha(private), "outputs": outputs,
                          "placementApproved": False, "nativeExecution": False, "publicationApproved": False}, indent=2))


if __name__ == "__main__":
    if len(sys.argv) != 2:
        raise SystemExit("Usage: source_wizard_append_oracle.py original.bin")
    main(sys.argv[1])
