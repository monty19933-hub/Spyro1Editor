#!/usr/bin/env python3
"""Read-only source evidence and neutral models of observed JP collectible state.

This tool never opens a card or writes a disc. The pure state functions describe
observed Japanese transitions, not a USA extension or native export approval.
"""
from __future__ import annotations

import argparse
from dataclasses import dataclass
from enum import IntEnum
import hashlib
import json
from pathlib import Path
import struct


@dataclass(frozen=True)
class ImageProfile:
    size: int
    digest: str
    exe_lba: int
    exe_size: int
    exe_digest: str


JP = ImageProfile(727179600, "1bcf4353d6a4d4d0db152d2ebd60879ef85516feaf170f364ff419c54f599ea7",
                  24, 0x70000, "a67c21f441deb3a4df26099d78ddaa29cc66b927a7387718a678eee71664cee4")
USA = ImageProfile(661547040, "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37",
                   53875, 0x66000, "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998")
EXE_FILE_BASE = 0x8000F800
USA_CARD_BASE = 0x8007AA38
EGG_TABLE_BYTES, EGG_BANK_BYTES, AUXILIARY_BYTES = 64, 32, 512


class ObservedEggState(IntEnum):
    ABSENT = 0
    NORMALIZED = 1
    PENDING = 0x69


def usa_card_runtime_address(offset: int) -> int:
    """Whole overlay is loaded at AA38; B0E8 is only state-table target zero."""
    if type(offset) is not int or offset < 0 or offset >= 0x3800 or offset % 4:
        raise ValueError("Aligned instruction/word offset must remain inside the card overlay")
    return USA_CARD_BASE + offset


def require_bytes(value: bytes, size: int) -> bytes:
    if not isinstance(value, bytes) or len(value) != size:
        raise ValueError(f"Expected exactly {size} immutable bytes")
    return value


def native_level_slot(level_id: int) -> int:
    """Admits only the thirty source non-flight retail levels, not generic IDs.

    The raw native arithmetic also maps flight15 onto level20 and hypothetical
    ID65 onto spare index30. Neither is an independently proved source owner.
    """
    if type(level_id) is not int or level_id // 10 not in range(1, 7) or level_id % 10 not in range(5):
        raise ValueError("No inspected non-flight Japanese collectible owner for this level")
    return 5 * ((level_id - 10) // 10) + level_id % 10


def native_egg_byte(bank: int, level_id: int) -> int:
    if type(bank) is not int or bank not in (0, 1):
        raise ValueError("Only two bounded native banks fit the observed 64-byte table")
    return 4 + bank * EGG_BANK_BYTES + native_level_slot(level_id)


@dataclass(frozen=True)
class DragonflyAuxiliarySnapshot:
    """Exact opaque 0x200-byte auxiliary owner; no fabricated checksum/schema."""
    data: bytes

    def __post_init__(self):
        require_bytes(self.data, AUXILIARY_BYTES)

    def state(self, bank: int, level_id: int) -> int:
        return self.data[native_egg_byte(bank, level_id)]

    def collection_suppressed(self, bank: int, level_id: int, gate: bool) -> bool:
        return not gate or self.state(bank, level_id) != 0

    def normalized_for_observed_write(self) -> "DragonflyAuxiliarySnapshot":
        result = bytearray(self.data)
        for index in range(4, 4 + EGG_TABLE_BYTES):
            if result[index] == ObservedEggState.PENDING:
                result[index] = ObservedEggState.NORMALIZED
        return DragonflyAuxiliarySnapshot(bytes(result))

    def merge_observed_pending(self, old_table: bytes) -> "DragonflyAuxiliarySnapshot":
        require_bytes(old_table, EGG_TABLE_BYTES)
        result = bytearray(self.data)
        for index, old in enumerate(old_table, 4):
            if result[index] == 0 and old == ObservedEggState.PENDING:
                result[index] = ObservedEggState.PENDING
        return DragonflyAuxiliarySnapshot(bytes(result))


def ordinary_percent(gems: int, dragons: int, eggs: int) -> int:
    """Observed ordinary-counter formula, deliberately no dragonfly argument."""
    if any(type(value) is not int or value < 0 for value in (gems, dragons, eggs)):
        raise ValueError("Only nonnegative ordinary counters are modeled")
    if gems > 12000:
        return 100 + (gems - 12000) // 100
    return (gems * 50 + dragons * 6000 + eggs * 10000) // 12000


def ordinary_save_checksum(slot: bytes) -> int:
    require_bytes(slot, 0x600)
    return sum(slot[:0x58C])


def inspect_ordinary_save(slot: bytes) -> dict:
    require_bytes(slot, 0x600)
    dragons = sum(slot[0x88:0xAC])
    gems = sum(struct.unpack_from("<36h", slot, 0xAC))
    eggs = sum(slot[0xF4:0x106])
    return {"checksumMatches": ordinary_save_checksum(slot) == struct.unpack_from("<I", slot, 0x58C)[0],
            "dragons": dragons, "gems": gems, "ordinaryEggs": eggs,
            "ordinaryPercent": ordinary_percent(gems, dragons, eggs),
            "notChecksummedTailByteLength": 0x600 - 0x590,
            "paddingClaim": "not checksummed by this routine; not established free space"}


class AuthenticatedImage:
    def __init__(self, path: Path, profile: ImageProfile):
        self.path, self.profile = path, profile
        self.file = path.open("rb")
        try:
            self.require(self.file)
        except BaseException:
            self.file.close()
            raise

    def require(self, file):
        if file.seek(0, 2) != self.profile.size:
            raise ValueError("Complete image length changed")
        file.seek(0)
        if hashlib.file_digest(file, "sha256").hexdigest() != self.profile.digest:
            raise ValueError("Complete image identity changed")

    def read(self, lba: int, offset: int, size: int) -> bytes:
        if min(lba, offset, size) < 0 or size > 0x800000:
            raise ValueError("Invalid bounded source read")
        result = bytearray()
        while size:
            count = min(size, 2048 - offset % 2048)
            at = (lba + offset // 2048) * 2352 + 24 + offset % 2048
            if at + count > self.profile.size:
                raise ValueError("Source read leaves complete physical image")
            self.file.seek(at)
            part = self.file.read(count)
            if len(part) != count:
                raise ValueError("Truncated source read")
            result.extend(part)
            offset += count
            size -= count
        return bytes(result)

    def close(self):
        try:
            self.require(self.file)
            with self.path.open("rb") as current:
                self.require(current)
        finally:
            self.file.close()


# Complete semantic windows, independently frozen from direct source disassembly.
JP_SPANS = (
    ("ordinary-percent", 0x1B330, 0xC0, "6fb34fa47ced6bf3e7f5432febecf89372bdb0d3c986127a467e3040683dbc53"),
    ("pending-normalization-and-write", 0x22BBC, 0x64, "ea8865b8abd452b54fc1af8ed5773144b6f2c655c45ecf8c4c85e381cce0b564"),
    ("pending-read-merge", 0x22DF0, 0x78, "bf5e5e824629f3097743369cdae93fd1cf066fc984b74ae95e57b126b56d9b4d"),
    ("ordinary-checksum", 0x4E3D4, 0x28, "d83b069d73f991a0ea4b1a318759f6ea66c5606de361242bab29d02c840824db"),
    ("ordinary-load-save-new", 0x4E458, 0x528, "89de66546296ef2e5e158ad1e9f66af81a0ea2067c4c47b0f435b0ffc346d286"),
    ("auxiliary-clear", 0x4E980, 0x24, "0b09803c8cd05b020554aedc9f4b4a5ae6dc0f5591d83fa11809cd2f81a9c308"),
    ("ordinary-three-slot-menu-percent", 0x4E9A4, 0x158, "ce6bd2c6644526e27c2b585dc45c90f41f0f86c960b18ef5b5191e80c9974a90"),
    ("ordinary-card-slot-write-read", 0x22A70, 0x114, "6c5ed78522f7828298801234a37145e1194dd0b5c369851a4e4f2bdbab0b1252"),
)
USA_SPANS = (
    ("ordinary-percent", 0x1C320, 0xC0, "8ccffbcd96e5afadd87bbe4a6b84f20c9ff4194d30b32aa6223cce2c80bd37e5"),
    ("ordinary-deserialize", 0x49D94, 0x2D0, "ff2bcfe124d934838348cb79807d36cdacc3d82f3cf6773d007c4aa0555e2ae6"),
    ("ordinary-serialize", 0x4A064, 0x1E4, "b8674b556d8f4154886306731ee50c9812f2cac1d9a224756c110be693d19056"),
)


def describe_span(data: bytes, label: str, offset: int, length: int, base: int, expected: str | None = None) -> dict:
    if offset < 0 or length <= 0 or offset % 4 or length % 4 or offset + length > len(data):
        raise ValueError("Instruction span exceeds explicit owner")
    payload = data[offset:offset + length]
    digest = hashlib.sha256(payload).hexdigest()
    if expected is not None and digest != expected:
        raise ValueError(f"Semantic source span changed: {label}")
    return {"id": label, "offset": offset, "pc": f"0x{base + offset:08X}", "length": length,
            "sha256": digest, "words": [f"{word:08X}" for word in struct.unpack(f"<{length // 4}I", payload)]}


def capture(path: Path, profile: ImageProfile, disasm: tuple[int, int] | None = None,
            card_disasm: tuple[int, int] | None = None) -> dict:
    source = AuthenticatedImage(path, profile)
    try:
        exe = source.read(profile.exe_lba, 0, profile.exe_size)
        if exe[:8] != b"PS-X EXE" or hashlib.sha256(exe).hexdigest() != profile.exe_digest:
            raise ValueError("Executable owner identity changed")
        result = {"sourceSha256": profile.digest, "sourceByteLength": profile.size,
                  "exeSha256": profile.exe_digest, "candidateWritten": False, "runtimeAccepted": False,
                  "evidence": [describe_span(exe, *span[:3], EXE_FILE_BASE, span[3])
                               for span in (JP_SPANS if profile == JP else USA_SPANS)]}
        if profile == JP:
            overlay = source.read(500, 0x7F4000, 0xF800)
            if hashlib.sha256(overlay).hexdigest() != "bcfe62569db265135d5627de1902a9d31da82c72f51acdc461d2872e08f7cdf2":
                raise ValueError("Japanese Artisans overlay changed")
            result["eggCode"] = [describe_span(overlay, "egg-gate-and-index", 0x9404, 0x9C, 0x80084A28,
                "489337a3d1655fabed9077510681d0c5970df9e0ed80ccee41cf6477658ff7d3"),
                describe_span(overlay, "egg-pending-store", 0x9630, 0x84, 0x80084A28,
                "3434cb9aa5d58c5282ae0bed93cbe7844ee18bedb820803219a0fb3985faf912")]
            directory = source.read(500, 0, 0x800)
            terrace_at, terrace_size = struct.unpack_from("<II", directory, 60 * 8)
            terrace = source.read(500, terrace_at, terrace_size)
            result["terraceDuplicateOwner"] = {"overlayEntry": 60, "wadOffset": terrace_at,
                "overlaySha256": hashlib.sha256(terrace).hexdigest(), "rowIndices": [176, 177],
                "nativeLevelId": 41, "sharedByteIndexWithinBank": native_level_slot(41),
                "evidence": [describe_span(terrace, "dispatch-table-entry-f5", 0x3D4, 4, 0x80084A28),
                    describe_span(terrace, "dispatch", 0xCBC, 0x40, 0x80084A28),
                    describe_span(terrace, "f5-handler", 0x60B0, 0x2C4, 0x80084A28)]}
            result["cardFileNameAtPC800100C8"] = exe[0x8C8:exe.index(0, 0x8C8)].decode("ascii")
            result["nativeContract"] = {"auxiliaryRuntimeBase": "0x800813E8", "auxiliaryByteLength": 512,
                "cardFileOffset": 0x1400, "eggTableOffset": 4, "eggTableByteLength": 64, "bankStride": 32,
                "bankSelectorRuntimeAddress": "0x8007F390", "gateRuntimeAddress": "0x8007F330",
                "observedStates": {"absent": 0, "normalized": 1, "newlyCollectedPending": 105},
                "retireOnAnyNonzero": True, "ordinaryCompletionIncludesDragonflyEggs": False,
                "ordinarySlotOffsets": [0x200, 0x800, 0xE00], "ordinarySlotBytes": 0x600,
                "ordinaryChecksumLength": 0x58C, "ordinaryChecksumField": 0x58C,
                "bankSemanticName": "unresolved; not the three ordinary card slots",
                "newPerInstanceIdentity": "not supplied by native per-level indexing",
                "auxiliaryChecksum": "not established; preserve all opaque bytes"}
        else:
            card = source.read(37, 0x5B800, 0x3800)
            if hashlib.sha256(card).hexdigest() != "8ee66fe644407bf15f93af1615045fbaca17fe3daa9f3b67f38bee2277a94c7c":
                raise ValueError("USA memory-card overlay identity changed")
            result["cardOverlay"] = {"sha256": hashlib.sha256(card).hexdigest(), "wadOffset": 0x5B800,
                "byteLength": len(card), "headerRuntimeBase": "0x8007AA38", "codeRuntimeBaseAtOffset9C": "0x8007AAD4",
                "stateZeroHandlerRuntimeAddress": "0x8007B0E8", "stateZeroHandlerOffset": 0x6B0,
                "sizeLiteralCandidates": [{"offset": at, "pc": f"0x{usa_card_runtime_address(at):08X}", "word": f"{word:08X}"}
                    for at in range(0x9C, len(card), 4) if (word := struct.unpack_from("<I", card, at)[0]) >> 26 == 9
                    and word & 0xFFFF in (0x200, 0x600, 0x1400, 0x2000)]}
            result["cardOverlay"]["ioCallSites"] = [{"offset": at, "pc": f"0x{usa_card_runtime_address(at):08X}",
                    "target": f"0x{((word & 0x3FFFFFF) << 2) | 0x80000000:08X}"}
                for at in range(0x9C, len(card), 4) if (word := struct.unpack_from("<I", card, at)[0])
                in (0x0C019BD4, 0x0C019B8A, 0x0C019C39, 0x0C019DC6)]
            result["cardHeaderTemplate"] = {"pc": "0x8006F494", "firstFourBytes": exe[0x5FC94:0x5FC98].hex(),
                "sha256": hashlib.sha256(exe[0x5FC94:0x5FE94]).hexdigest()}
            if card_disasm:
                if card_disasm[0] < 0x9C:
                    raise ValueError("The requested card span starts in the non-code header")
                disasm = (usa_card_runtime_address(card_disasm[0]), card_disasm[1])
                exe, disasm_base = card, USA_CARD_BASE
            else:
                disasm_base = EXE_FILE_BASE
        if disasm:
            from capstone import Cs, CS_ARCH_MIPS, CS_MODE_MIPS32, CS_MODE_LITTLE_ENDIAN
            pc, length = disasm
            base = disasm_base if profile == USA else EXE_FILE_BASE
            span = describe_span(exe, "requested-window", pc - base, length, base)
            md = Cs(CS_ARCH_MIPS, CS_MODE_MIPS32 | CS_MODE_LITTLE_ENDIAN)
            span["instructions"] = [f"{i.address:08X} {i.bytes.hex()} {i.mnemonic} {i.op_str}"
                for i in md.disasm(exe[pc - base:pc - base + length], pc)]
            result["disassembly"] = span
        return result
    finally:
        source.close()


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("image", type=Path)
    parser.add_argument("--usa", action="store_true")
    parser.add_argument("--disasm", nargs=2, type=lambda value: int(value, 16), metavar=("PC", "LENGTH"))
    parser.add_argument("--usa-card-disasm", nargs=2, type=lambda value: int(value, 16), metavar=("OFFSET", "LENGTH"))
    options = parser.parse_args()
    if options.usa_card_disasm and (not options.usa or options.disasm):
        parser.error("--usa-card-disasm requires --usa and excludes --disasm")
    report = capture(options.image, USA if options.usa else JP, options.disasm, options.usa_card_disasm)
    report["heldAndNamedOriginalFinal"] = True
    print(json.dumps(report, indent=2))
