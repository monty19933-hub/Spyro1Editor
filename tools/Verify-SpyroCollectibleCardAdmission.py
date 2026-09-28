#!/usr/bin/env python3
"""Read-only fixed-profile successor verifier; never opens a card or writes a BIN.

Uses the unchanged R01 source-compiled reader recipe and shared physical walks.
Admission control must be frozen below BEFORE publication; no CLI recipe or
candidate bytes can supply code authority. Hash pins do not prove MIPS semantics.
"""
from __future__ import annotations

import argparse
import base64
import importlib.util
import json
from pathlib import Path
import re
import struct
import sys
import zlib

sys.dont_write_bytecode = True
spec = importlib.util.spec_from_file_location("_admission_reader_physical", Path(__file__).with_name("Verify-SpyroCollectibleCardLoad.py"))
reader = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = reader
spec.loader.exec_module(reader)
require, sha = reader.require, reader.sha

PROFILE = "native-collectible-card-admission-research-v1"
IMAGE_NAME = "collectible-card-admission-research-r01.bin"
NEW_OVERLAY_SIZE, GROWTH = 0xD000, 0x9800
OVERLAY_EDITS = ((0x174, 8), (0x2060, 4), (0x21C8, 4))
EXE_EDITS = reader.EXE_EDITS + ((0x2F00, 0x50),)
RESET_ORIGINAL_SHA = "1e50b4e2d3b702c380c05ae4018cebdf4800b32493b5772e4a2fc1da598f554f"
RESET_WORDS = (0x3C018007, 0xAC205860, 0xAC205750, 0xAC205810, 0xAC20587C,
               0xAC2056C8, 0xAC205830, 0xAC2058E8, 0xAC22580C, 0xAC205838,
               0xAC20583C, 0x8C2155A4, 0, 0x2421F600, 0x14200003, 0,
               0x3C018020, 0xA020F616, 0x3C018007, 0)
# Frozen from the source-tested prepublication description, September27.
# Generated control semantics have source execution/review, not a second compiler.
CONTROL_SHA = "4cc3477d0415c3d881a37231d603b71325c6d9ce17ecd6b779948119bc687c2e"
CONTROL_B85 = (
    "c-rk&O-NKx6h8OOn0IujF{X%`auf$0hv;t}7(8m4T+CRDNeYJCm_(SiawQTW{VWQ{pdScV25Hg{0}-@Iiip~@Xc4Viv}h4Qn<R-M"
    "r*nVCIX<77?SlJ(+_~pF_uO;OJ@<Q74>RY^v4$i-;|K`Mrkffe6&63MksB;jxZkC8(-ugD#P53KHqHtzI_~zHLqASpoF&{Z<Z5&b"
    "j$GS-jPZcuUr>x_OGT6|B<B{!PRq4lzc8+1)?&{6Hb8Gd<_rM`YU1`71U9JLIOfKYhuhUKwdV$av;*2x7Pk|bDc@Cy&imdf3ENh7"
    "McbbLo@(a90jF&!ZYOd9Rt>2-wI26@xZMj=&52wKI|r$G|9ZjwSm-ZpvlagV?Jx3wS*YaigTStSy?0RTdtAjirTt}`_rlh~PG}oM"
    "12GZj^Cc0dPsTY8tva83xL=l4={$PjKF(`dr(XNEG~FD6JH;?e6gR<SF#>msQFvCw8^QB&n`7Lz0h!-JYD;<8+O=(Tjs?|SaEBq+"
    "3YoCFV_X+mcvlQ-*r@MH@HrPRYP^~7?S#jmb!Gb*X+JIPYl*+e-LLax9=S8u8L<{uRqP2lVol_Tb&(_1MvgmSx|zdApUTs6e2`9-"
    "=S%hEizRyUsYg#f@#x7%tL(|iRrciAGCg_SqbEl_dh+rzJ$ZhGo*Y`SC$0IuvhdylkdfMPP-}>=`_R5$+q(ednAoV+?VZ;ezXQ83"
    "qOMdq7hodEhu1jWkypvlpQ>+nXuT@0OOW_pZ^HVEH^K29bDOy>Y8Aqo*ALg=eRSAR3Oc3I2>R3(R4pwhJey8h&?sxslt05TgNJ_L"
    "F+ap(-%G8o9#U@z$J-%&-lg*NEz|$)|Iq)hZ>f4&GfInBvv&Q=^7{SB^@iP=@qzGeO|g)FNBE43_aXm=@P|D#=j95T^i=aA|6JE%"
    "{xRY69{d9m-y!FlB%0PvG;N&tQ>_~#`-BUN>vWCqgN4O)x<dG~F1`x+^Mt?Q5r2sAk;3A04iJ9CgYP5f>h`E}4_Uv7tY3$k)TK44"
    "4g%Zr5S4Ya4shu^UZ~XQ2-)K#sofzGJ4#|Ry|>V~wALuGwwJ7R$Xcb9eOfECyKJ{s=Cf=kbT8D(d@D<6n}~gs#EwB$+Rs7urWhOa"
    "UJ%U?O;x^?ZzI0>ec~-+{B3a@gAR07bOrzC(%;4&6${dGUZdw_eepv_>{}_gq5NHA<hv&3uwZUrU|?WiU|?WiU|?WiU|?WiU|?YI"
    "Kj06&=dAq"
)
ENTRY_PC, BEGIN_PC, GATE_PC = 0x80085238, 0x800852EC, 0x80085580


def append_layout():
    return reader.append_layout() + (("admission-control", 0xA800, 0x2000),
                                    ("admission-state", 0xC800, 0x80),
                                    ("admission-padding", 0xC880, 0x780))


def fixed_executable(original):
    result = bytearray(reader.fixed_executable(original))
    require(sha(original[0x2F00:0x2F50]) == RESET_ORIGINAL_SHA and
            reader.u32(original, 0x2EFC) == 0x2402FFFF, "Reset source window differs")
    for at in reader.ANCHOR_OFFSETS:
        struct.pack_into("<II", result, at, 0x3C028008, 0x24427A38)
    struct.pack_into("<20I", result, 0x2F00, *RESET_WORDS)
    reader.restore_fields(original, result, EXE_EDITS)
    return bytes(result)


def inverse_executable(original, after):
    require(type(after) is bytes and after == fixed_executable(original), "Admission EXE is not the exact afterimage")
    return reader.restore_fields(original, after, EXE_EDITS)


def fixed_overlay(original):
    result = bytearray(reader.fixed_overlay(original))
    require(struct.unpack_from("<II", original, 0x174) == (0x3C028008, 0x8C428D78), "Card entry source seam differs")
    require(len(CONTROL_SHA) == 64 and bool(CONTROL_B85), "Reviewed admission control is not frozen; publication refused")
    try:
        control = zlib.decompress(base64.b85decode(CONTROL_B85))
    except (ValueError, zlib.error) as error:
        raise ValueError("Invalid frozen admission-control encoding") from error
    require(len(control) == 0x2000 and sha(control) == CONTROL_SHA, "Admission control frozen identity differs")
    require(all(type(pc) is int and 0x80085238 <= pc < 0x80087238 and pc % 4 == 0
                for pc in (ENTRY_PC, BEGIN_PC, GATE_PC)) and ENTRY_PC < BEGIN_PC < GATE_PC,
            "Admission entries leave the frozen control owner")
    result.extend(control)
    result.extend(bytes(0x800))
    struct.pack_into("<II", result, 0x174, reader.jump(ENTRY_PC), 0)
    struct.pack_into("<I", result, 0x2060, reader.jump(BEGIN_PC, True))
    struct.pack_into("<I", result, 0x21C8, reader.jump(GATE_PC))
    require(len(result) == NEW_OVERLAY_SIZE, "Admission allocation differs")
    reader.restore_fields(original, result[:0x3800], OVERLAY_EDITS)
    return bytes(result)


def inverse_overlay(original, after):
    require(type(after) is bytes and after == fixed_overlay(original), "Admission overlay is not the exact afterimage")
    return reader.restore_fields(original, after[:0x3800], OVERLAY_EDITS)


def packed_wad(original, overlay):
    return reader._packed_card_wad(original, overlay, sys.modules[__name__])


def inverse_wad(original, after):
    return reader._inverse_card_wad(original, after, sys.modules[__name__])


def fixed_root(original, size):
    return reader._fixed_card_root(original, size, GROWTH)


def inverse_root(original, after):
    require(after == fixed_root(original, reader.WAD_SIZE + GROWTH), "Admission root differs")
    records = reader.iso_directory(original)
    return reader.restore_fields(original, after, tuple((records[name][2] + 2, 16) for name in ("WAD.WAD", "SCUS_942.28")))


def inverse_sector(after, lba, before, wad, exe, root, root_lba):
    original_end = reader.EXE_LBA + reader.EXE_SIZE // reader.USER
    if original_end <= lba < original_end + GROWTH // reader.USER:
        reader.prepare_padding(before, lba)
        return before
    return reader.inverse_sector(after, lba, before, wad, exe, root, root_lba)


def require_cue(data):
    require(type(data) is bytes and 0 < len(data) <= 65536, "Invalid bounded admission CUE")
    lines = [line.strip() for line in data.decode("utf-8-sig").splitlines() if line.strip()]
    match = re.fullmatch(r'FILE\s+"([^"\r\n]+)"\s+BINARY', lines[0], re.IGNORECASE) if lines else None
    require(len(lines) == 3 and match is not None and match.group(1) == IMAGE_NAME and
            lines[1].upper() == "TRACK 01 MODE2/2352" and lines[2].upper() == "INDEX 01 00:00:00",
            "CUE is outside the fixed admission profile")


def verify(usa, candidate=None, cue=None, quiet=False):
    return reader._verify_fixed_recipe(usa, candidate, cue, quiet, sys.modules[__name__])


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--usa", required=True)
    parser.add_argument("--candidate")
    parser.add_argument("--cue")
    parser.add_argument("--quiet", action="store_true")
    args = parser.parse_args()
    print(json.dumps(verify(args.usa, args.candidate, args.cue, args.quiet), indent=2))
