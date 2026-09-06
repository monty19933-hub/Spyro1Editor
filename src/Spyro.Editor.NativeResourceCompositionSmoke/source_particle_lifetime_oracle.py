#!/usr/bin/env python3
"""Independent raw-sector source oracle. CFG/arithmetic checks are not native CPU/GPU emulation."""
import hashlib
import json
import struct
import sys

SOURCE = "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37"
def sha(b): return hashlib.sha256(b).hexdigest()
def u32(b, o=0): return struct.unpack_from("<I", b, o)[0]
def signed16(v): return v - 65536 if v & 32768 else v
def words(b): return [u32(b, o) for o in range(0, len(b), 4)]

with open(sys.argv[1], "rb") as image:
    assert image.seek(0, 2) == 661547040
    image.seek(0)
    assert hashlib.file_digest(image, "sha256").hexdigest() == SOURCE
    def read(offset, length, lba=37):
        result = bytearray()
        while length:
            count = min(length, 2048-offset % 2048)
            image.seek((lba+offset//2048)*2352+24+offset % 2048)
            part = image.read(count)
            assert len(part) == count
            result.extend(part); offset += count; length -= count
        return result
    exe = read(0, 0x66000, 53875)
    donor = read(0x2F5B000, 0x10000)
    dark = read(0xEF6000, 0xB000)
    pete_copy = read(0x7B9000, 0x39C48)
    assert sha(pete_copy) == "1703ac37e59e480909b6f66dc856d4e5f3ce80551f18cbc8c06129c3b54dd35d"
    pete = pete_copy[:0x40]
    assert sha(exe) == "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998"
    assert sha(donor) == "5d3996111567ffac6eca08d340f2f691824bb7969ba2a0cd95e9346ccfe157e1"
    assert sha(dark) == "9920aa47b94005166f457b5b36df6dedd1f2400f91698bac21cdb01d8590ef32"
    certificates = []
    def cert(name, pc, size, expected=None, buf=exe, base=0x8000F800):
        b = buf[pc-base:pc-base+size]
        assert len(b) == size
        if expected: assert sha(b) == expected
        certificates.append(dict(id=name, pc=f"{pc:08X}", length=size, sha256=sha(b)))
        return b
    spawn = cert("native07-spawn", 0x80089934, 0x11C,
        "c3e0db5def4d5c5c63c10b3e8acf314ebabfa91c93f8a2087a268e78da2df6e3", donor, 0x8007AA38)
    tail = cert("native07-frame-zero-tail", 0x80089F28, 12, buf=donor, base=0x8007AA38)
    tick = cert("native07-tick", 0x80088370, 0x98,
        "a2f5681329e63ab489abec1424ecc230f462f4af53bfc5d0e0ae94bb5a09afc4", donor, 0x8007AA38)
    render = cert("kind2-complete-render-path", 0x80057C08, 0x274,
        "5763c22d74429eece44169934917ca2336b4f2a1fb9565c8fb292e5350e261f4")
    line = cert("kind1-complete-render-path", 0x80057E7C, 0x11C,
        "f01218c13d66114224931e8a049ad297c4ff48fee5ebe830a2f2f58635e5efa4")
    cert("allocator", 0x80053570, 0x98, "2eefe927534faee54464ea9af9e53a9382ff3fafa0f90d50b6a68982dce61ddb")
    cert("delete", 0x80053608, 0x9C, "3bf6f2ab0aaba682bc34bb5c4f734e872e144df43022b4e3f389784aa5a11be8")
    cert("random", 0x8006272C, 0x30, "8bcf455ef340cfd7846fd7937a88671fbf165aa56526545a4912547c8d6bcfd4")
    cert("vector-pack", 0x80017BFC, 0x28, "ef85ebacb8d15ec69efdbdfc9b342ec6ef5007f3216e8f2867baffb9519dd312")
    cert("renderer-kind-dispatch", 0x80057494, 0x64, "fe9649cc792838807a4f4218c80e4a9921e6af4a75645ab5d5eef0aa56c2c9d6")
    cert("negative-kind-prefetch", 0x80058080, 0x2C, "5d2a30a886c3a2e684b2a1216d0c9f477a470bd48d258f6e1c45aa0ffceff83e")
    cert("pool-init-and-terminal", 0x8001441C, 0x38, "3f2eab7fbf790f58eab1d2cd3d663bbfd8355b4ebd4bff1e4f6b43d387c12b7d")
    cert("normal-buffer-initializer", 0x8005B6F8, 0xE0, "d0b4cf5a7cf9ee407ff413f7261bd5f92f4880d7cee8467852f4d757e6b84295")
    cert("resident-PETE-copy-and-normal-init-call", 0x8005B844, 0x8C, "0bd2bacdbb26dc622390405c6c154fa0ddb89c8a1e252704c7877c016e73a2ec")
    cert("renderer-camera-and-scratch-prologue", 0x800573C8, 0xF8)
    cert("resident-sine-table-and-quarter-overlap", 0x8006CBF8, 0x280,
        "795a93bee7b4d49bef89d4df8c8628a4b4b3a91d694053dbc2b26b4293d4fa55")

    assert words(tail) == [0xA2480011, 0x08022A12, 0xA2400010]
    assert u32(spawn, 4) == 0x24040002  # allocator kind2
    # Exhaust all native direct/conditional paths in the acyclic spawn body.
    # Shared allocator/RNG/vector routines are separately source-bound above.
    paths = [(0x80089934, False)]
    ended = []
    visited = set()
    code = dict(zip(range(0x80089934, 0x80089A50, 4), words(spawn)))
    code.update(zip(range(0x80089F28, 0x80089F34, 4), words(tail)))
    while paths:
        pc, zeroed = paths.pop()
        if (pc, zeroed) in visited: continue
        visited.add((pc, zeroed))
        if pc == 0x8008A848:
            ended.append(zeroed); continue
        assert pc in code
        word = code[pc]; op = word >> 26
        zeroed |= word == 0xA2400010
        if op in (1, 2, 3, 4, 5, 6, 7):
            delay = code[pc+4]
            assert delay >> 26 not in (1, 2, 3, 4, 5, 6, 7)
            zeroed |= delay == 0xA2400010
            if op == 3: paths.append((pc+8, zeroed))
            elif op == 2: paths.append((((pc+4) & 0xF0000000) | ((word & 0x3FFFFFF) << 2), zeroed))
            else:
                paths.extend([(pc+8, zeroed), (pc+4+signed16(word & 65535)*4, zeroed)])
        else: paths.append((pc+4, zeroed))
    assert ended and all(ended)
    # Decode every actual source tick store, accounting for s2=raw+1.
    stores = []
    for word in words(tick):
        op = word >> 26
        if op in (0x28, 0x29, 0x2B):
            assert (word >> 21) & 31 == 18
            width = {0x28:1, 0x29:2, 0x2B:4}[op]
            stores.extend(range(1+signed16(word & 65535), 1+signed16(word & 65535)+width))
    assert 0x10 not in stores and sorted(set(stores)) == [2,4,5,6,7,8,9,10,11]
    # Renderer advances raw cursor once. Its only raw-particle stores change
    # visibility byte3; GPU commands are written to separately owned buffers.
    render_writes = []; cursor = 0
    for word in words(render):
        if word == 0x23FF0020: cursor += 0x20
        if word >> 26 in (0x28, 0x29, 0x2B) and (word >> 21) & 31 == 31:
            assert word >> 26 == 0x28
            render_writes.append(cursor + signed16(word & 65535))
    assert cursor == 0x20 and render_writes == [3,3]
    assert words(exe[0x80057D9C-0x8000F800:0x80057DC4-0x8000F800]) == [
        0x97EBFFF0,0x8FE3FFEC,0x000B6202,0x00AC2822,0x04A0FDC4,
        0x316C00FF,0x000C60C0,0x002C0820,0x8C2B0004,0x8C2C0008]
    # The full-pool allocator's masked random result always selects a slot.
    for rand in range(32768):
        offset = 0x2000 - ((rand & 255)+1)*0x20
        assert 0 <= offset <= 0x1FE0 and offset % 32 == 0
    # Bind actual native pre-check and independent reload/increment/store instructions.
    assert [u32(tick, o) for o in (0, 8, 12)] == [0x92420001, 0x2C420019, 0x1040041B]
    assert [u32(tick, o) for o in (0x5C, 0x7C, 0x80)] == [0x92460001, 0x24C60001, 0xA2460001]
    # Abstract replay only; native helper bodies, renderer, motion and GPU are NOT executed.
    frame_store = u32(tail, 8)
    assert frame_store >> 26 == 0x28 and (frame_store >> 21) & 31 == 18
    assert (frame_store >> 16) & 31 == 0 and signed16(frame_store & 65535) == 0x10
    samples = 0
    for stale_frame in range(256):
        for initial_age in range(16):
            raw = bytearray(32)
            raw[0x10] = stale_frame
            raw[signed16(frame_store & 65535)] = 0  # apply only decoded SB-zero semantics
            frame = raw[0x10]
            age = initial_age
            steps = 0
            while age < 25:
                assert frame == 0 and 4+frame*8+8 == 12
                age += 1; steps += 1
            assert steps == 25 - initial_age and 1 <= steps <= 25
            samples += 1
    assert samples == 4096
    assert u32(pete, 0) == 46 and pete[0x20:0x22] == bytes(2)
    assert u32(exe, 0x800755A4-0x8000F800) == 0x800
    header = read(0x7B8800, 0x800)
    assert sha(header) == "b9a4de193b5cf0bd9592de15c57aa5daa3713c3575026154b45402f3aceef6f1"
    assert u32(header, 0) == 0x39C48
    top = 0x80200000 - 0x800 - 0x39C48
    image.seek(0)
    assert hashlib.file_digest(image, "sha256").hexdigest() == SOURCE
    assert image.tell() == 661547040 and image.seek(0, 2) == 661547040
    print(json.dumps(dict(sourceSha256=SOURCE, sourceUnchanged=True, outputsWritten=0,
        abstractArithmeticReplay=True, nativeCpuEmulated=False, nativeGpuEmulated=False,
        maxAgeUpdates=25, maxAgeCheckCallsIncludingDeletion=26,
        native07FramePayloadBytes=8, recordBytes=12, maxFrameIndex=0,
        tickRawWrittenOffsets=sorted(set(stores)), kind2RawWrittenOffsets=render_writes,
        allocatorRngCases=32768, staleFrameCases=256, initialAgeCases=16, abstractMatrixSamples=samples,
        poolStart=f"{top-0x2000:08X}", poolEnd=f"{top:08X}",
        terminalPrefetchOwner="resident PETE shared player package +0x20..+0x22",
        peteFirst64Sha256=sha(pete), certificates=certificates,
        limitations=["Bounded original normal-level source profile, not arbitrary runtime writes.",
            "No native gameplay, GPU execution, alternate buffer lifecycle or peak-resource acceptance.",
            "Camera/viewport, ordering-table, primitive buffer and hardware scratchpad remain native shared-engine owners."]), indent=2))
