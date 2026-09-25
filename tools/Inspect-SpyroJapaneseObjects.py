#!/usr/bin/env python3
"""Read-only evidence for the supplied SCPS-10083 revision's objects.

This is an inspection tool, not an exporter or an import certificate. It opens
the original image only with rb, requires its exact SHA256, and emits metadata
and selected source bytes to stdout. No emulator or editor state is touched.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import struct
from pathlib import Path

IMAGE_BYTES = 727179600
IMAGE_SHA256 = "1bcf4353d6a4d4d0db152d2ebd60879ef85516feaf170f364ff419c54f599ea7"
WAD_LBA = 500
WAD_BYTES = 112793600
EXE_LBA = 24
EXE_BYTES = 0x70000
EXE_SHA256 = "a67c21f441deb3a4df26099d78ddaa29cc66b927a7387718a678eee71664cee4"
REGISTERS = "zero at v0 v1 a0 a1 a2 a3 t0 t1 t2 t3 t4 t5 t6 t7 s0 s1 s2 s3 s4 s5 s6 s7 t8 t9 k0 k1 gp sp fp ra".split()


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def bounded(data: bytes, offset: int, size: int) -> bytes:
    if offset < 0 or size < 0 or offset + size > len(data):
        raise ValueError("Read exceeds its explicit byte owner")
    return data[offset:offset + size]


def u32(data: bytes, offset: int = 0) -> int:
    return struct.unpack("<I", bounded(data, offset, 4))[0]


def u16(data: bytes, offset: int = 0) -> int:
    return struct.unpack("<H", bounded(data, offset, 2))[0]


class Source:
    def __init__(self, path: Path):
        self.path = path
        self.file = path.open("rb")
        try:
            if self.file.seek(0, 2) != IMAGE_BYTES:
                raise ValueError("Image length differs from authenticated Japanese revision")
            self.require_hash()
        except BaseException:
            self.file.close()
            raise

    def require_hash(self) -> None:
        self.file.seek(0)
        digest = hashlib.file_digest(self.file, "sha256").hexdigest()
        if digest != IMAGE_SHA256:
            raise ValueError("Image hash differs from authenticated Japanese revision")
        with self.path.open("rb") as named:
            if named.seek(0, 2) != IMAGE_BYTES:
                raise ValueError("Named original image length changed during inspection")
            named.seek(0)
            if hashlib.file_digest(named, "sha256").hexdigest() != IMAGE_SHA256:
                raise ValueError("Named original image changed during inspection")

    def read_file(self, lba: int, owner_size: int, offset: int, size: int) -> bytes:
        if lba < 0 or offset < 0 or size < 0 or offset + size > owner_size:
            raise ValueError("Disc read exceeds explicitly identified ISO file")
        out = bytearray()
        while size:
            sector, within = divmod(offset, 2048)
            count = min(size, 2048 - within)
            self.file.seek((lba + sector) * 2352 + 24 + within)
            part = self.file.read(count)
            if len(part) != count:
                raise ValueError("Truncated MODE2 source sector")
            out.extend(part)
            offset += count
            size -= count
        return bytes(out)

    def wad(self, offset: int, size: int) -> bytes:
        return self.read_file(WAD_LBA, WAD_BYTES, offset, size)

    def executable(self) -> bytes:
        data = self.read_file(EXE_LBA, EXE_BYTES, 0, EXE_BYTES)
        if data[:8] != b"PS-X EXE" or sha256(data) != EXE_SHA256:
            raise ValueError("Japanese executable identity changed")
        return data


def mips_instruction(word: int, pc: int) -> str:
    """Small audit disassembler; unknown words stay explicit, never interpreted."""
    op, rs, rt, rd, sa, fn = word >> 26, word >> 21 & 31, word >> 16 & 31, word >> 11 & 31, word >> 6 & 31, word & 63
    immediate = word & 0xffff
    signed = immediate if immediate < 0x8000 else immediate - 0x10000
    r = REGISTERS
    if word == 0:
        return "nop"
    if op == 0:
        if fn in (0, 2, 3):
            return f"{ {0:'sll',2:'srl',3:'sra'}[fn]} {r[rd]}, {r[rt]}, {sa}"
        if fn in (4, 6, 7):
            return f"{ {4:'sllv',6:'srlv',7:'srav'}[fn]} {r[rd]}, {r[rt]}, {r[rs]}"
        if fn in (8, 9):
            return f"{'jr' if fn == 8 else 'jalr'} {r[rs]}"
        if fn in (16, 18):
            return f"{'mfhi' if fn == 16 else 'mflo'} {r[rd]}"
        if fn in (24, 25, 26, 27):
            return f"{ {24:'mult',25:'multu',26:'div',27:'divu'}[fn]} {r[rs]}, {r[rt]}"
        names = {32:'add',33:'addu',34:'sub',35:'subu',36:'and',37:'or',38:'xor',39:'nor',42:'slt',43:'sltu'}
        if fn in names:
            return f"{names[fn]} {r[rd]}, {r[rs]}, {r[rt]}"
    if op in (2, 3):
        return f"{'j' if op == 2 else 'jal'} 0x{((pc + 4) & 0xf0000000) | ((word & 0x3ffffff) << 2):08X}"
    if op in (4, 5, 6, 7) or op == 1 and rt in (0, 1, 16, 17):
        name = {4:'beq',5:'bne',6:'blez',7:'bgtz'}.get(op) or {0:'bltz',1:'bgez',16:'bltzal',17:'bgezal'}[rt]
        arguments = f"{r[rs]}, {r[rt]}, " if op in (4, 5) else f"{r[rs]}, "
        return f"{name} {arguments}0x{(pc + 4 + signed * 4) & 0xffffffff:08X}"
    if op == 15:
        return f"lui {r[rt]}, 0x{immediate:04X}"
    if op in (8, 9, 10, 11, 12, 13, 14):
        name = {8:'addi',9:'addiu',10:'slti',11:'sltiu',12:'andi',13:'ori',14:'xori'}[op]
        operand = str(signed) if op < 12 else f"0x{immediate:X}"
        return f"{name} {r[rt]}, {r[rs]}, {operand}"
    if op in (32, 33, 34, 35, 36, 37, 38, 40, 41, 42, 43, 46):
        name = {32:'lb',33:'lh',34:'lwl',35:'lw',36:'lbu',37:'lhu',38:'lwr',40:'sb',41:'sh',42:'swl',43:'sw',46:'swr'}[op]
        return f"{name} {r[rt]}, {signed}({r[rs]})"
    return f".word 0x{word:08X} ; unimplemented decoder"


def disassemble(data: bytes, start: int, size: int, load_base: int) -> list[dict]:
    bounded(data, start, size)
    if start % 4 or size % 4:
        raise ValueError("MIPS range must be word aligned")
    return [{"offset": f"0x{i:X}", "pc": f"0x{load_base + i:08X}",
             "word": f"0x{u32(data, i):08X}", "instruction": mips_instruction(u32(data, i), load_base + i)}
            for i in range(start, start + size, 4)]


def scene_layout(data: bytes) -> tuple[bytes, int, int, int, int]:
    scene_offset, scene_length = u32(data, 0x18), u32(data, 0x1c)
    scene = bounded(data, scene_offset, scene_length)
    cursor = 0x88
    for _ in range(7):
        size = u32(scene, cursor)
        if size < 8 or size % 4:
            raise ValueError("Unsupported scene component chain")
        bounded(scene, cursor, size)
        cursor += size
    size, count = u32(scene, cursor), u32(scene, cursor + 4)
    if size < 8 or size % 4 or count > 4096 or 8 + count * 0x58 > size:
        raise ValueError("Invalid placed-row component extent")
    bounded(scene, cursor, size)
    props = cursor + size
    props_size = u32(scene, props)
    if props_size < 4 or props_size % 4:
        raise ValueError("Invalid private-data component extent")
    bounded(scene, props, props_size)
    return scene, scene_offset, cursor, props, props_size


def sign_message(scene: bytes, prop: int, properties_start: int, properties_end: int) -> dict:
    if prop < properties_start or prop % 4 or prop + 0x18 > properties_end:
        raise ValueError("Sign header escapes the private-data owner")
    string_pointer, cursor_pointer = u32(scene, prop + 12), u32(scene, prop + 16)
    if string_pointer != prop + 0x18 or cursor_pointer != prop + 0x14:
        raise ValueError("Unrecognized sign pointer ownership")
    end = scene.find(b"\0", string_pointer, properties_end)
    if end < 0:
        raise ValueError("Sign string has no terminator in private-data owner")
    raw = scene[string_pointer:end]
    return {"textSceneOffset": string_pointer, "cursorSceneOffset": cursor_pointer,
            "byteLength": len(raw), "bytesHex": raw.hex(), "sha256": sha256(raw),
            "lineCount": raw.count(b"\xff") + 1,
            "codecStatus": "native single-byte glyph/control stream; 00 terminates, FF line break, 40..46 text color",
            "zeroTerminatorSceneOffset": end}


def inspect_model(data: bytes, root: int, owner_end: int) -> dict:
    """Report animation-owned face streams, never infer a complete package."""
    def read(at: int, n: int) -> bytes:
        if at < root or at + n > owner_end:
            raise ValueError("Model reference escapes declared model subfile")
        return bounded(data, at, n)

    count = u32(read(root, 4))
    if count not in (1, 3):
        raise ValueError("Selected Japanese object has an unrecognized animation count")
    read(root, 0x38 + count * 4)
    model_data = root + u32(data, root + 0x34)
    animations = []
    for index in range(count):
        animation = root + u32(data, root + 0x38 + index * 4)
        header = read(animation, 0x24)
        frames = u16(header)
        if frames > 1024:
            raise ValueError("Unbounded animation frame count")
        stride = 8 if header[4] == 0 else 4
        frame_data = read(animation + 0x24, frames * stride)
        faces = []
        for field, name in [(0x14, "near"), (0x1C, "far")]:
            relative = u32(header, field)
            if field == 0x1C and relative == 0:
                continue
            start = model_data + relative
            length = u32(read(start, 4))
            if length > 0x20000 or length % 4:
                raise ValueError("Unbounded native face command stream")
            face_data = read(start + 4, length)
            cursor, commands, materials = 0, 0, []
            while cursor < len(face_data):
                word = u32(face_data, cursor)
                if word & 0x80000000 == 0:
                    size, texture = (20, 8) if word & 2 else (8, -1)
                elif word & 4:
                    size, texture = 20, 8
                else:
                    size, texture = (24, 12) if word & 2 else (12, -1)
                command = bounded(face_data, cursor, size)
                if texture >= 0:
                    material = {"clut": u16(command, texture + 2), "tpage": u16(command, texture + 6),
                                "uvBytesHex": bounded(command, texture, 12).hex()}
                    materials.append(material)
                cursor += size
                commands += 1
            faces.append({"distance": name, "dataOffset": start, "byteLength": 4 + length,
                          "sha256": sha256(read(start, length + 4)), "commands": commands,
                          "texturedCommands": len(materials), "materials": materials})
        animations.append({"index": index, "headerDataOffset": animation, "headerHex": header.hex(),
                           "frameCount": frames, "framesHex": frame_data.hex(), "faces": faces})
    return {"completeModelClosureProven": False, "animationCount": count,
            "modelDataOffset": model_data, "animations": animations}


def inspect_entry(data: bytes, entry: int, wad_offset: int, actors=(0x47, 0xf5)) -> dict:
    scene, scene_offset, mobys, props, props_size = scene_layout(data)
    count = u32(scene, mobys + 4)
    registrations = {}
    for index in range(64):
        root, actor = u32(data, 0x50 + index * 4), u16(data, 0x150 + index * 2)
        if root and actor in actors:
            model_offset, model_length = u32(data, 0x10), u32(data, 0x14)
            if root < model_offset or root + 0x40 > model_offset + model_length:
                raise ValueError("Selected model root outside its model subfile")
            registrations[actor] = {"slot": index, "dataOffset": root,
                                    "wadOffset": wad_offset + root,
                                    "header64Hex": bounded(data, root, 64).hex(),
                                    "header64Sha256": sha256(bounded(data, root, 64)),
                                    "modelEvidence": inspect_model(data, root, model_offset + model_length)}
    property_references = [u32(scene, mobys + 8 + index * 0x58) for index in range(count)]
    rows = []
    for index in range(count):
        start = mobys + 8 + index * 0x58
        row = bounded(scene, start, 0x58)
        actor = u16(row, 0x36)
        if actor not in actors:
            continue
        prop = u32(row)
        result = {"index": index, "actorId": actor, "sceneOffset": start,
                  "wadOffset": wad_offset + scene_offset + start,
                  "recordHex": row.hex(), "recordSha256": sha256(row),
                  "xyz": list(struct.unpack_from("<iii", row, 12)),
                  "propertiesSceneOffset": prop, "animationBytes3C3D": list(row[0x3c:0x3e]),
                  "renderRadius50": row[0x50]}
        if actor == 0x47:
            if property_references.count(prop) != 1:
                raise ValueError("Sign private owner is aliased by another native row")
            private_end = min([p for p in property_references if prop < p <= props + props_size] or [props + props_size])
            result["message"] = sign_message(scene, prop, props + 4, private_end)
        elif actor == 0xf5:
            if prop < props + 4 or prop + 12 > props + props_size:
                raise ValueError("Egg private-data header escapes its owner")
            result["privateHeader12Hex"] = bounded(scene, prop, 12).hex()
        rows.append(result)
    return {"entry": entry, "wadOffset": wad_offset, "byteLength": len(data), "sha256": sha256(data),
            "sceneDataOffset": scene_offset, "mobyComponentSceneOffset": mobys, "totalRows": count,
            "propertiesComponentSceneOffset": props, "propertiesComponentBytes": props_size,
            "selectedModelRoots": registrations, "selectedRows": rows}


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("image", type=Path)
    parser.add_argument("--entries", type=lambda s: [int(x, 0) for x in s.split(",")], default=[23, 25, 27])
    parser.add_argument("--exe-disasm", nargs=2, metavar=("OFFSET", "LENGTH"), type=lambda s: int(s, 0))
    parser.add_argument("--overlay-disasm", nargs=4, metavar=("ENTRY", "OFFSET", "LENGTH", "LOAD_BASE"), type=lambda s: int(s, 0))
    parser.add_argument("--output", type=Path, help="Create a new report file exclusively; existing files are never replaced")
    args = parser.parse_args()
    source = Source(args.image)
    try:
        header = source.wad(0, 0x800)
        report = {"schemaVersion": 1, "sourceSha256": IMAGE_SHA256, "sourceBytes": IMAGE_BYTES,
                  "nativeImportApproved": False, "scope": "read-only source evidence; no rendered/runtime validation"}
        if args.exe_disasm:
            exe = source.executable()
            start, size = args.exe_disasm
            report["instructions"] = disassemble(exe, start, size, u32(exe, 0x18) - 0x800)
        elif args.overlay_disasm:
            entry, start, size, base = args.overlay_disasm
            if entry < 0 or entry >= 256:
                raise ValueError("Invalid WAD directory index")
            offset, length = u32(header, entry * 8), u32(header, entry * 8 + 4)
            report["loadBaseStatus"] = "caller-supplied; must be independently bound to loader evidence"
            report["instructions"] = disassemble(source.wad(offset, length), start, size, base)
        else:
            report["levels"] = []
            for entry in args.entries:
                if entry < 23 or entry > 91 or entry % 2 == 0:
                    raise ValueError("Only the observed Japanese level-data directory entries are admitted")
                offset, size = u32(header, entry * 8), u32(header, entry * 8 + 4)
                report["levels"].append(inspect_entry(source.wad(offset, size), entry, offset))
        source.require_hash()
        serialized = json.dumps(report, indent=2)
        if args.output:
            with args.output.open("x", encoding="utf-8") as output:
                output.write(serialized + "\n")
            print(json.dumps({"report": str(args.output.resolve()), "sourceSha256": IMAGE_SHA256,
                              "originalFinal": True, "candidateWrites": 0}))
        else:
            print(serialized)
    finally:
        source.file.close()


if __name__ == "__main__":
    main()
