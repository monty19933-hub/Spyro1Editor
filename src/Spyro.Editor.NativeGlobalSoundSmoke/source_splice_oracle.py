#!/usr/bin/env python3
"""Read-only independent raw-sector oracle; never emits a ROM or other payload file."""
import hashlib
import struct
import sys

EXPECTED_SOURCE = "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37"


def sha(data):
    return hashlib.sha256(data).hexdigest()


def word(data, offset):
    return struct.unpack_from("<I", data, offset)[0]


def put(data, offset, value):
    struct.pack_into("<I", data, offset, value)


with open(sys.argv[1], "rb") as image:
    assert image.seek(0, 2) == 661547040
    image.seek(0)
    assert hashlib.file_digest(image, "sha256").hexdigest() == EXPECTED_SOURCE

    def read(offset, length):
        result = bytearray()
        while length:
            chunk = min(length, 2048 - offset % 2048)
            image.seek((37 + offset // 2048) * 2352 + 24 + offset % 2048)
            result.extend(image.read(chunk))
            offset += chunk
            length -= chunk
        return result

    directory = read(0, 0x800)

    def level(level_id):
        entry = 10 + ((level_id // 10 - 1) * 6 + level_id % 10) * 2
        offset, length = struct.unpack_from("<II", directory, entry * 8)
        return read(offset, length)

    def sound_offset(data):
        cursor = word(data, 8)
        for _ in range(6):
            cursor += word(data, cursor)
        portals = word(data, cursor)
        cursor += 4
        for _ in range(portals):
            cursor += 0x34 + 12 * word(data, cursor + 4)
            cursor += word(data, cursor)
        return cursor + word(data, cursor)

    donor = level(33)
    donor_sound = sound_offset(donor)
    descriptor = donor[donor_sound + 0x108 + donor[donor_sound + 4 + 0x1F] * 20:][:20]
    sample_offset = word(donor, 0) + 0x80000 + word(descriptor, 0)
    sample_end = sample_offset
    while True:
        sample_end += 16
        if donor[sample_end - 15] & 1:
            break
    sample = donor[sample_offset:sample_end]
    assert len(sample) == 0x1D40
    assert sha(sample) == "b03fdc973a8966a21326691f1546c19bf9e8d40d2eb15b00bec6cc9789599322"
    animation_offset = word(donor, 0x50)
    while word(donor, animation_offset) != 8:
        assert word(donor, animation_offset) != 0xFFFFFFFF
        animation_offset += 8 + word(donor, animation_offset + 4)
    animation = donor[animation_offset:animation_offset + 8 + word(donor, animation_offset + 4)]
    assert len(animation) == 0x3E1C
    assert sha(animation) == "cf194a65fc9b3b8790f6cd96eae5ef309156d00ad205d85790b2128e57243eb0"
    for target_id in (12, 13, 14):
        original = level(target_id)
        header = original[:0x800]
        sound = sound_offset(original)
        sound_length = word(original, sound)
        descriptor_count = word(original, sound + 0x104)
        appended_descriptor = descriptor[:]
        put(appended_descriptor, 0, word(original, 4) - 0x80000)
        blocks = []
        shift = 0
        for index in range(10):
            offset, length = struct.unpack_from("<II", original, index * 8)
            if not length:
                assert offset == 0
                continue
            block = original[offset:offset + length]
            put(header, index * 8, offset + shift)
            if index == 0:
                block += sample + bytes(0x2000 - len(sample))
                put(header, index * 8 + 4, length + 0x2000)
                shift += 0x2000
            elif index == 1:
                local_sound = sound - offset
                insert = local_sound + sound_length
                block = block[:insert] + appended_descriptor + block[insert:] + bytes(0x800 - 20)
                put(block, local_sound, sound_length + 20)
                put(block, local_sound + 0x104, descriptor_count + 1)
                block[local_sound + 4 + 0x1F] = descriptor_count
                put(header, index * 8 + 4, length + 0x800)
                shift += 0x800
            blocks.append(block)
        for index in range(64):
            root = word(original, 0x50 + index * 4)
            if root:
                put(header, 0x50 + index * 4, root + 0x2800)
        final = header + b"".join(blocks)
        assert len(final) == len(original) + 0x2800
        print(f"SOUND_SPLICE_ORACLE level={target_id} finalSha256={sha(final)}")
        insertion = word(final, 0x54) - 4
        assert word(final, insertion) == 0xFFFFFFFF
        model_end = word(final, 0x10) + word(final, 0x14)
        combined = final[:insertion] + animation + final[insertion:model_end] + bytes(0x4000 - len(animation)) + final[model_end:]
        for index in range(10):
            if not word(final, index * 8):
                continue
            if index == 2:
                put(combined, index * 8 + 4, word(final, index * 8 + 4) + 0x4000)
            elif index > 2:
                put(combined, index * 8, word(final, index * 8) + 0x4000)
        for index in range(1, 64):
            root = word(final, 0x50 + index * 4)
            if root:
                put(combined, 0x50 + index * 4, root + len(animation))
        assert len(combined) == len(original) + 0x6800
        print(f"SOUND_PLAYER_COMPOSITION_ORACLE level={target_id} finalSha256={sha(combined)}")
    image.seek(0)
    assert hashlib.file_digest(image, "sha256").hexdigest() == EXPECTED_SOURCE
    print("NATIVE_GLOBAL_SOUND_INDEPENDENT_ORACLE_OK targets=3 sourceUnchanged=true outputsWritten=0")
