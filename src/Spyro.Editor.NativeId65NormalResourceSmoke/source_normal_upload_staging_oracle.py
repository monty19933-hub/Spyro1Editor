"""Read-only raw-source evidence for the normal upload staging stage.

This is not a hardware emulator, padded native subfile, or publication producer.
It emits evidence only; all source bytes are read from the exact original image.
"""
import hashlib
import json
import struct
import sys


IMAGE_SHA = "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37"
EXE_SHA = "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998"


def sha(value):
    return hashlib.sha256(value).hexdigest()


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

        def read(lba, offset, length):
            output = bytearray()
            while length:
                count = min(length, 2048 - offset % 2048)
                image.seek((lba + offset // 2048) * 2352 + 24 + offset % 2048)
                block = image.read(count)
                assert len(block) == count
                output += block
                offset += count
                length -= count
            return bytes(output)

        original()
        executable = read(53875, 0, 0x66000)
        assert sha(executable) == EXE_SHA
        data = read(37, 0x119E000, 0x2E2000)
        assert sha(data) == "7ddbf6d9a6ee6c0f64c8564a89e374ca0ca234ef608ec812aae68e8176de1dc0"
        atlas = data[0x800:0x80800]
        header = data[0x1D0000:0x1D0088]
        upload = bytearray(0xDE000)

        # Independent reconstruction of the already checkpointed paired upload.
        # Full source pages/CLUT rows, particle frame-0 extents, detached companion.
        def retain(offset, length):
            assert 0 <= offset < offset + length <= 0x80000
            upload[offset:offset + length] = atlas[offset:offset + length]

        for index in range(15):
            raw = header[0x10 + 8 * index:0x18 + 8 * index]
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
            colors = 16 << (4 * mode)
            assert cx >= 512 and cx + colors <= 1024 and cy + 8 <= 512
            for row in range(8):
                retain((cy + row) * 1024 + (cx - 512) * 2, colors * 2)
        for index in range(10):
            raw = data[0x17259C + 12 * index:0x1725A4 + 12 * index]
            u0, v0, clut, u1, v1, page = struct.unpack("<BBHBBH", raw)
            assert page & ~0x7F == 0 and page & 15 >= 8
            x, y = (page & 15) * 64, ((page >> 4) & 1) * 256
            for row in range(min(v0, v1), max(v0, v1) + 1):
                retain((y + row) * 1024 + (x - 512) * 2 + min(u0, u1) // 2,
                       max(u0, u1) // 2 - min(u0, u1) // 2 + 1)
            retain((clut >> 6) * 1024 + ((clut & 63) * 16 - 512) * 2, 32)
        for row in range(32):
            upload[row * 1024:row * 1024 + 16] = atlas[0x582A0 + row * 1024:0x582B0 + row * 1024]
        upload[32:64] = atlas[0x7A2C0:0x7A2E0]
        upload[0x80000:] = data[0x80800:0xDE800]
        assert sha(upload) == "b769a9a298285aa402956fdbd5721132736388c6f3f1dc1bfcd7e15a9afeb66d"

        # Native request is not the DMA rounded read extent or the sound bank.
        cpu_request = 0x7EFF0
        dma_blocks = (cpu_request >> 6) + int((cpu_request & 63) != 0)
        dma_source_length = dma_blocks * 16 * 4
        assert dma_source_length == 0x7F000
        staging = upload[0x80000:] + upload[0x5E000:0x7F000]
        assert sha(staging) == "626ae349b98cb4bea955ac53020586f645f872225085a67bc809f46f4ab0fb07"
        assert len(staging) == dma_source_length
        assert staging[:0x5E000] == data[0x80800:0xDE800]
        assert staging[0x5E000:] == upload[0x5E000:0x7F000]
        assert staging[0x7EFF0:].hex() == "3232b131122e903191311232f231322e"

        spans = []
        for label, address, length in [
            ("normal-loader-VRAM-then-bank", 0x800156B0, 0x230),
            ("CD-request-sector-rounding", 0x80016698, 0xEC),
            ("normal-fixed-SPU-request", 0x8001583C, 0x20),
            ("capped-write-wrapper", 0x8005CAC4, 0x60),
            ("CPU-or-DMA-backend", 0x8005C450, 0x88),
            ("full-DMA-register-setup", 0x8005C1C0, 0x290),
            ("full-CPU-FIFO-transfer", 0x8005BE88, 0x1CC),
            ("public-address-and-mode-setters", 0x8005CB24, 0x8C),
            ("address-rounding-helper", 0x8005C588, 0xA4),
            ("boot-units-and-default-block", 0x8005BCD0, 0x104),
            ("VRAM-upload-wrapper", 0x8005FA28, 0x64),
            ("VRAM-queue", 0x80061820, 0x2E0),
            ("VRAM-transfer", 0x80061234, 0x23C),
        ]:
            offset = address - 0x8000F800
            block = executable[offset:offset + length]
            assert len(block) == length
            spans.append({"id": label, "runtime": f"{address:08X}",
                          "exeOffset": f"{offset:X}", "length": f"{length:X}",
                          "sha256": sha(block)})
        pointers = executable[0x63D54:0x63D6C]
        assert struct.unpack("<6I", pointers) == (0x1F801C00, 0x1F8010C0,
                                                  0x1F8010C4, 0x1F8010C8,
                                                  0x1F8010F0, 0x1F801014)
        boot = executable[0x63D94:0x63DA4]
        assert boot == bytes([7]) * 16
        original()
        print(json.dumps({"marker": "NATIVE_ID65_NORMAL_UPLOAD_STAGING_RAW_ORACLE_OK",
                          "nativeScalarInstructionsExecuted": False,
                          "sourceImageSha256": IMAGE_SHA, "executableSha256": EXE_SHA,
                          "pairedUploadSha256": sha(upload), "stagingSha256": sha(staging),
                          "stagingLength": len(staging), "cpuRequest": cpu_request,
                          "dmaBlockCount": dma_blocks, "dmaBlockWords": 16,
                          "dmaSourceLength": dma_source_length,
                          "linearSpuDestination": 0x1010, "linearSpuEnd": 0x80010,
                          "dmaOnlyTailHex": staging[0x7EFF0:].hex(),
                          "dmaOnlyTailSha256": sha(staging[0x7EFF0:]),
                          "hardwarePointerTableSha256": sha(pointers),
                          "bootBlockSha256": sha(boot), "bootBlockDestination": 0x1000,
                          "spuWrapSemanticsProven": False, "reservedLow16PreservationProven": False,
                          "hardwareEmulated": False, "publicationApproved": False,
                          "spans": spans}, indent=2))


if __name__ == "__main__":
    if len(sys.argv) != 2:
        raise SystemExit("Usage: source_normal_upload_staging_oracle.py original.bin")
    main(sys.argv[1])
