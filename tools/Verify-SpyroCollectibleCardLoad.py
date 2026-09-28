#!/usr/bin/env python3
"""Read-only whole-disc verifier for one fixed USA card-load research recipe.

Physical layout, original-byte preservation, hooks, copy anchors, startup stub,
padding and CD integrity are reconstructed independently from the exact source.
The three appended generated-code blocks are immutable source-compiled literals
reviewed BEFORE publication, not independently reconstructed MIPS semantics.
No DLL, receipt, expected-hash/offset argument or candidate payload is authority.
Only JSON is written to stdout; BIN/CUE/card/settings files are never written.
"""
from __future__ import annotations

import argparse
import base64
import contextlib
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import struct
import sys
import zlib

sys.dont_write_bytecode = True
_spec = importlib.util.spec_from_file_location("_card_physical_cd_oracle", Path(__file__).with_name("Verify-SpyroJapaneseSignReference.py"))
oracle = importlib.util.module_from_spec(_spec)
sys.modules[_spec.name] = oracle
_spec.loader.exec_module(oracle)
require, sha, bounded, u32 = oracle.require, oracle.sha, oracle.bounded, oracle.u32
ReadOnlyFile, logical, rebuild_sector = oracle.ReadOnlyFile, oracle.logical, oracle.rebuild_sector

RAW, USER = 2352, 2048
USA_SIZE = 661547040
USA_SHA = "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37"
WAD_SHA = "7ba8961c3626bcec816fbd6feecc4eff67dc8018ba43974e7838f4c341bf54a1"
EXE_SHA = "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998"
CARD_SHA = "8ee66fe644407bf15f93af1615045fbaca17fe3daa9f3b67f38bee2277a94c7c"
WAD_LBA, WAD_SIZE = 37, 0x6927000
EXE_LBA, EXE_SIZE = 53875, 0x66000
OVERLAY_BASE, OLD_OVERLAY_SIZE, NEW_OVERLAY_SIZE = 0x8007AA38, 0x3800, 0xA800
GROWTH = NEW_OVERLAY_SIZE - OLD_OVERLAY_SIZE
IMAGE_NAME = "collectible-card-load-research-r01.bin"
PROFILE = "native-collectible-card-load-research-v1"
ANCHOR_OFFSETS = (0x3030, 0x1EAFC, 0x23E24)
EXE_EDITS = ((0x10, 4), (0x65DA4, 4), (0x65E40, 0x2C)) + tuple((at, 8) for at in ANCHOR_OFFSETS)
OVERLAY_EDITS = ((0x2060, 4), (0x21C8, 4))
STUB_WORDS = (0x3C02801F, 0x3442F600, 0x3C03801F, 0x3463F800, 0xAC400000,
              0x24420004, 0x0043082B, 0x1420FFFC, 0, 0x08016E38, 0)
BLOCK_SIZES = (("sha", 0x800), ("loader", 0x1000), ("control", 0x2000),
               ("shadow-alignment", 8), ("shadow", 0x200), ("state", 0x80),
               ("digest", 0x20), ("hash-workspace", 0x120), ("read-buffer", 0x2000),
               ("native-prefix-snapshot", 0x1400))
# Frozen from exact-source --describe before any candidate publication.
# Manifest: content 12345678-1234-5678-9012-123456789abc, slot 0,
# active IDs 11111111-1111-1111-1111-111111111111 and
# 22222222-2222-2222-2222-222222222222; source plan dbfb2f5 ancestry.
# Compressed literals contain only generated sha/loader/control capacities.
BEGIN_PC, PUMP_PC, GATE_PC = 0x8007FA38, 0x80080000, 0x80080698
CODE_BLOCKS = {
    'sha': ('200e42c2eb1fe1c3165ba065ec07b13ac6cc054e1691cac80039eee573a05be1',
        'c-rNZ|4$ob9LGP;QRs7}uqzzUZr#Qu{SsD&+3X23RXbv`I_4<2EiiPFOr=A<bc`@EovYn2#6cMR#DXT^AnRD%!5CmNWu+RJi)8VK'
        'nW>9u%woVr7eu2|V}0)Kp*yob!ApAY_viDx?&)*)yzg)bdtfUTkD*Q;<U@zuE?+8YL-{#!sfWnpM-V6;2<rtbx_pt8COV!^dM@93'
        'R8Q+@BI4ACSWn511)|H(qBQWDlzHUd_j2zk^fK})EG9j8;=i2I+p2PVHC){KC>OP=T$y&h4Uckh%5A4yH1UZOU`*1%xP;-dWP}Nc'
        'gDVmblad*xBr8ly`7kK?AYuz|#}Fbv8$=`=GLyN?#cVzH)ct@(Kv=4~iai~du&5)x$%8#DD_F$Dmycl2lfPlnNPK1m_ME81B1e4c'
        '0QPhRu*egCawGO!{2Gg9;^Q^gV_CqWmH2#!^vLV<g268MAO#@ZtplUm6--5Xh^l)58E!qKDn3NK4uF%r5bPx9g<b{OpAPn_MuY&{'
        '6NxVS{X~df>-ta!1@@(cLES@gZ#KA~{HG3BrB?zVMSZE@0ahY=?gV6UewJXn<*`@=3*G}P*!^WF{3Z*b8sgjUA&0*l#j03vkp3T('
        'Eq#TBs61YK8pR0fU1e)~P<TIKNnh~qLQ%>K{^!tLjR!E0wPJfbVNutPICY)Kq$oyNiW*V2q8#EC<x!5JW|XU_73C=^cp*i};$ndq'
        'uI3@s#4FDzc8mvaBk}D&AxBj+Dt(KG=%I(TP<0eJ0#l4F(9Jjk3yeUn0obgrQ}G!|AFkyf)Xb54?nb3=a1d?a)OqB4K2FB!AJ(>j'
        'iE#u?j4imE%zFt5<Qn0lC?kvwGD0N82!pRPLcf<0&g3&fCon>&0m;up-oKm4XM*XyaY)XjE?Jw9oJ*XnvyhxkW3tXhaz43a%_BLZ'
        'e6r3#a!$?3Iv2@VwI=I4q|NJUhRPFcj`kg0T_3X#lk=t_W)B7=Yb6FoRzD^o-!!Dta5~DRQZ9{hdZJjvVal<TGf*y_s8PciDVIUH'
        'Ov<ex%4s-`awf`UQ7)S(ui<#g<xnn{a(P6}8qQ2P3+2{QZXHpphO<&`J>@n~ZX;3ht)lPdxSDymtY#i2)Xc*bHS;j3W*(;0%)>PO'
        'j>z9T#*HDKqQphx`yYF^wD;NK!#DqmPL6y#xl|L|w{6+9Ts<6qrtj7P>&5<2{cTipGHa$|i|y0n#=WgC&9wY8{$1#N+MR*c<$>AR'
        'Lj!l;*)el}?)}*{Ukn*-&4o*wM^ClxymMjf#cw(vv{hC--QPI3C34-FGB$sr`r1l*d~QeY`CAnSZ=}@~6!0B}X=jPBuITT^!soLy'
        'N?zD=#PI4>Lw#G(=drgt3S0Vy7O#C!_S)J=@N`-1Tt&mYWwGx|TiV&LI>gLhgM;;4?(DDyHxzH!b=2pa{CLbgVYF`BIeL3{`HlB#'
        'R+@_fKZko-n-7k(^X=D9VP?MT&&14`dky~K-_Jdm`KA5v9Q@CZe*sz@lZO'),
    'loader': ('a2e80a9fa048977fe3c9b4a78af68ff788398fadbb46a67b1a4e40421063f469',
        'c-rmNZ)jUp7zXfjZ_@V7n#HtcT^6$~g~n#I%ItEq9}XH_KXj#oVTVkZlDX-st+UgqRi|bf%oft6RS+iPzpZOgw!4C$h-HO=Y#?<A'
        'gMl)t2*N}Z#4*QQeD6Ka(c5g_{gMl9-t)U}&OJ%)eGU~!i4Kwz_DD>42dM*s<d{9}jfv0alO#v&Gv2sPOs_j=i)CIbpBv)dw(jY`'
        'K0D=E%AL=%mDXO^c=_|zI`+=1u2Y~^q#C@rT)U)uccb?|tLXbt_w7WVyDo9};y%J>ar++e_2rT`_T>_~ug}*vbd$b(Qul4}^)1|_'
        'Zw`I;`PaAV#=ZsgN%XCD`>rhR^X#(jYeSz9XN%_dalhZRo$)*<&UkK%&>blo*MV6g#Jy`|TP|d+utkG@|EPEF;q|2YHj!NZ?>yQc'
        ';`#gTC)ofec%I$j>(;}=aC5)Zz@uiKVN#7*l4VPKtiu0-$Kh;hypiM|@Fe`5FG=xx<gf6Q@4D`KeulUB+-Cm)o`G|(3b20%Z};`P'
        '-{*PwTKM-LlbW9)`3*b^U-u`e>$^yvg?GYtgh(AcO!72b!B6B!mCuu$h3DY?gQOy(BtL_9!5@8(RPTC{Gw^Qs@r$HRUm*Djychn<'
        'b-tJHBsmG+RPnjqhi|U%!|*K?{uX>|g^$7WVr1NXy(Z|cqNQ_So6p{WJ?69huyf_v)~Gn@#Kfc%7ss5Wm~v9$q|+j1oQyc-G#3h@'
        'd1OvBr}%ajHEO$<%pSU8g}wX6${~-I@Uzw<w+nhxuW!SI`@hEudDqwhx8Pmy$Kf{oUicI60K5~v6CQ+j!297f@OJo<@LKro@TcH)'
        '@DzLiPVfYL5FUcZ;7`Msz$5To@On7GpMi(rHhc)a6n;%;-wltzFT<aO-va*~{v13CzXaa{Z-D;<e;(clzX*Q;ek=R}{6%;S{w;hM'
        'z6|~~{3Uo3{44m&@a6D1_$%-@{0sPA_zL)^@K@mp_zC!H@Y~?i@Ymr<_($*pd?ox{_&)e5_}lRP@YV2fxOqkn_@0q5VV;pV?{D#Y'
        'nuIuF_BtwrcpqFN-6#nPO8;Hf^YlK*dCz$h737#qjd5*qI_0ZdxHdVR;o4+#JJ%*J*K%z#GRw8e#ZInG7Amey{^htfnb*a&$+>Q>'
        'O}6#=*2ed*V<F@Bz}<QnwVUxDaBXse6Ld`HA~`|So89xMw{RUsy_M@IYToZSr*zGk!g-}@e*QVPbUo<JnK`8IBRWE;6N09~sEb%j'
        'zN3eKgL-u?F~F~3wU8+C>r%C?<2bJ|!Wld7u606ZTK|W<PkVI>@7=XR@7?8cDD93dF`vF#=10%Vxt_Eo2KcX-{;j+b_k3i^d|yqx'
        'E`IuX{s*#kLchly2YDRujca(!*<8}k++<0Areh_24^Nf!-g~m7_u83~-dj(V^qx7}N@5{k=CXJ$|Jbe=2NsR1NA;*4)uVd+AICrP'
        '_>(X'),
    'control': ('d333fcc3b8ef49ebd4b653f4cf7b0ec1265309a34ce00d37e32421ffae9b522b',
        'c-rk&O=uHA6n?YW*xgz+sTCBWu3${lDA<~S;HryMiPA$5!Gf1s3PvsdfcOI<5iLkH7D0+oL=k^#A#C*|f+!*?o;-<y2M>at6;ui~'
        'zWK>^x?PhLJb0LQ$jrR=?VH)1_r5O;FuJavz#^blFl^9_Oe#AX+D9zrv#1ZKZz<^&KA0R$7zNW_zcZOh<xDaMoG5?ZL@f1fB)f^@'
        'VV6_?ujyFk`@ykY0^3lSYriNS%mCzzbR7b-eXH2BiSF0Ib2tLea=fFyR<fb8pJm2rp05Km&jFSvP1?j+#uBOAcA|?jq;gBi8u8qC'
        '`ANcp+wMxZ<F>+GcQxE|*TT4)h6nC?c<M$R02@Z#W*i+H4cx<tO`R3Mv<beu0VdxI47(A(8^z@~uJjWiLL-CMNfRAA;g(BrA%PS5'
        'O4J9wXX!h&6ld=DM(ju9Cw*}Z<Ikn|X<s~y@yAm9YC!&nQat91)4Lv%;+F#Ax1{)DDZT*jb_Dp{M!{Tj4J>vi+F65PZ%WuJ5b<(Y'
        'lAl1O-b`{q-04&e9Thc{>^KYl8cJ%X!Cyn|<OEFBA+MkP{9UZJRY!o=P;T;mBIUG^Fg-S116?PzJn$Ym1Fgf;&&qT3NZ%p1XXbb6'
        'mg3z3@tla$n&_13u3R?laygmFf*IxC#E46n5}o%;^IdFyl+F9t{7A|FU1=;IP>VfY9`tQ8GApu{W)vE2*trA!E5&*Bl8$H4AsL@2'
        'H;OYOei_Yy@3JY0SYVjnWq)n-75O@B+hcdqMqv&<_cDzhJj0hXJBwvxBG`X}BfN(yb=m^kTPl4=eEqaU^c{`O_4QRy|IL%|O?BXj'
        'z)$&l*?S;g4DtE~;)wY|oFmV$IdTmfk#E?DoWn-s9X2BOuo3xZh}YVagaMyI{A?PYV@tZSZa^Nt8NlNs0X#lD3m(5X3mz|2;qg-e'
        'Jbo;I#}8NG@q85?-}{ewyn|GDT*vi!ti`aMjq#f7ioHx%IJ}iO5&5~Vq7MJo=O3g#&ieL6uF8Ct;$5+s&wrHSxq$d5iARF;;(ZOh'
        'cw0j+Uf0lzXEpTVQ4PHquc8<CCB3*?MK5mDp%=qd^kOJLF9rkjVxWp%oT;G~$7|@tA?coXN%x$GsORsK{Jr9xCGIW;<?WX8_Qa~|'
        'On+>;CU8yRdA4~tt_6(Sm*M{(PY-mFH!ko4f}i7;>F*Y?)}eh>MK8X2dZF9UaUS_TY@Y+(b3NE|mnV47Z3f5Vdd<hhof3D?^Kth^'
        'DK@_jUT>H13+lkfC48g~ysS&nS?W^rf2>Qoq)WJFcV>bUk?$-FS#KTUYj&rvuJJq8z7vCv&0<uhOqnue%9JTnrc9YKWy+K(Q>IMW'
        '?ClSr(ghI'),
}


def append_layout():
    cursor, result = OLD_OVERLAY_SIZE, []
    for name, size in BLOCK_SIZES:
        result.append((name, cursor, size))
        cursor += size
    padded = (cursor + USER - 1) & ~(USER - 1)
    require(padded == NEW_OVERLAY_SIZE, "Fixed append allocation differs")
    result.append(("sector-padding", cursor, padded - cursor))
    return tuple(result)


def jump(pc, link=False):
    require(type(pc) is int and 0x80000000 <= pc < 0x80200000 and pc % 4 == 0,
            "MIPS jump target outside aligned native RAM")
    return (0x0C000000 if link else 0x08000000) | ((pc >> 2) & 0x3FFFFFF)


def restore_fields(original, after, fields):
    require(len(original) == len(after), "Field inverse extent differs")
    inverse = bytearray(after)
    for at, size in fields:
        inverse[at:at + size] = bounded(original, at, size)
    require(inverse == original, "Complete field complement/inverse differs")
    return bytes(inverse)


def fixed_executable(original):
    require(type(original) is bytes and len(original) == EXE_SIZE and original[:8] == b"PS-X EXE",
            "Requires immutable complete PS-X EXE geometry")
    fields = ((0x10, 0x8005B8E0), (0x14, 0), (0x18, 0x80010000), (0x1C, 0x65800),
              (0x20, 0), (0x24, 0), (0x28, 0), (0x2C, 0), (0x30, 0x801FFFF0),
              (0x34, 0), (0x65DA4, 0x800))
    require(all(u32(original, at) == value for at, value in fields), "Original executable header/reserve differs")
    require(not any(original[0x65E40:]), "Complete original ephemeral tail is not zero")
    result = bytearray(original)
    anchor = OVERLAY_BASE + NEW_OVERLAY_SIZE
    for at in ANCHOR_OFFSETS:
        require(struct.unpack_from("<II", original, at) == (0x3C028008, 0x2442DDE8),
                "An original card copy-anchor instruction pair differs")
        struct.pack_into("<II", result, at, 0x3C020000 | ((anchor + 0x8000) >> 16), 0x24420000 | (anchor & 0xFFFF))
    struct.pack_into("<I", result, 0x10, 0x80075640)
    struct.pack_into("<I", result, 0x65DA4, 0xA00)
    struct.pack_into("<11I", result, 0x65E40, *STUB_WORDS)
    restore_fields(original, result, EXE_EDITS)
    return bytes(result)


def inverse_executable(original, after):
    require(type(after) is bytes and after == fixed_executable(original), "Executable is not the exact fixed afterimage")
    return restore_fields(original, after, EXE_EDITS)


def fixed_overlay(original):
    require(type(original) is bytes and len(original) == OLD_OVERLAY_SIZE, "Requires immutable complete original card allocation")
    for at, pc in ((0x2060, 0x80067628), (0x21C8, 0x8006631C)):
        require(u32(original, at) == jump(pc, True) and u32(original, at + 4) == 0,
                "Original JAL/NOP card seam differs")
    require(set(CODE_BLOCKS) == {"sha", "loader", "control"}, "Reviewed fixed source-compiled code recipe is absent")
    result = bytearray(original)
    for name, at, size in append_layout():
        require(len(result) == at, "Appended block has a gap or overlap")
        payload = bytes(size)
        if name in CODE_BLOCKS:
            digest, encoded = CODE_BLOCKS[name]
            try:
                payload = zlib.decompress(base64.b85decode(encoded))
            except (ValueError, zlib.error) as error:
                raise ValueError("Invalid immutable generated-code encoding") from error
            require(len(payload) == size and sha(payload) == digest, "Reviewed source-compiled block identity differs")
        result.extend(payload)
    control_at = next(at for name, at, _ in append_layout() if name == "control")
    for pc in (BEGIN_PC, PUMP_PC, GATE_PC):
        require(type(pc) is int and OVERLAY_BASE + control_at <= pc < OVERLAY_BASE + control_at + 0x2000 and pc % 4 == 0,
                "Frozen control entry is not inside its complete code block")
    require(BEGIN_PC < PUMP_PC < GATE_PC, "Frozen native entry order differs")
    struct.pack_into("<I", result, 0x2060, jump(BEGIN_PC, True))
    struct.pack_into("<I", result, 0x21C8, jump(GATE_PC))
    restore_fields(original, result[:OLD_OVERLAY_SIZE], OVERLAY_EDITS)
    return bytes(result)


def inverse_overlay(original, after):
    require(type(after) is bytes and after == fixed_overlay(original), "Card overlay is not the exact fixed afterimage")
    return restore_fields(original, after[:OLD_OVERLAY_SIZE], OVERLAY_EDITS)


def wad_entries(data):
    require(type(data) is bytes and len(data) >= USER and len(data) % USER == 0, "Invalid complete packed WAD extent")
    entries = []
    for index in range(256):
        at, size = struct.unpack_from("<II", data, index * 8)
        if at == size == 0:
            continue
        require(at >= USER and size > 0 and at % USER == size % USER == 0 and at + size <= len(data),
                "Invalid/half-empty/unaligned WAD entry")
        entries.append((at, size, index))
    entries.sort()
    cursor = USER
    for at, size, _ in entries:
        require(at == cursor, "Packed WAD has an overlap, gap or alias")
        cursor += size
    require(cursor == len(data) and sum(index == 2 for _, _, index in entries) == 1,
            "Complete packed WAD coverage or unique entry two is absent")
    return entries


def packed_wad(original, overlay_after):
    entries = wad_entries(original)
    card_at, card_size, _ = next(entry for entry in entries if entry[2] == 2)
    require(card_size == OLD_OVERLAY_SIZE and overlay_after == fixed_overlay(bounded(original, card_at, card_size)),
            "Only the fixed entry-two card replacement is admitted")
    result, reports = bytearray(original[:USER]), []
    for at, size, index in entries:
        before = bounded(original, at, size)
        after = overlay_after if index == 2 else before
        new_at = len(result)
        struct.pack_into("<II", result, index * 8, new_at, len(after))
        result.extend(after)
        reports.append({"index": index, "originalOffset": at, "finalOffset": new_at,
                        "originalLength": size, "finalLength": len(after),
                        "originalSha256": sha(before), "finalSha256": sha(after)})
    require(len(result) == len(original) + GROWTH, "Fixed packed WAD growth differs")
    wad_entries(bytes(result))
    return bytes(result), reports


def inverse_wad(original, after):
    old_entries, final_entries = wad_entries(original), wad_entries(after)
    require(len(after) == len(original) + GROWTH and len(old_entries) == len(final_entries), "Inverse WAD owner count/growth differs")
    inverse_header = bytearray(after[:USER])
    inverse = bytearray()
    for (old_at, old_size, index), (new_at, new_size, new_index) in zip(old_entries, final_entries):
        require(index == new_index and new_size == old_size + (GROWTH if index == 2 else 0), "Inverse WAD entry mapping differs")
        before, current = bounded(original, old_at, old_size), bounded(after, new_at, new_size)
        if index == 2:
            current = inverse_overlay(before, current)
        else:
            require(current == before, "Unrelated complete WAD entry payload changed")
        inverse.extend(current)
        struct.pack_into("<II", inverse_header, index * 8, old_at, old_size)
    result = bytes(inverse_header) + bytes(inverse)
    require(result == original, "Complete WAD directory/payload inverse differs")
    return result


def iso_directory(root):
    require(type(root) is bytes and len(root) == USER, "Requires complete single-sector ISO root")
    result, cursor = {}, 0
    while cursor < len(root) and root[cursor]:
        row = bounded(root, cursor, root[cursor])
        require(len(row) >= 34 and row[32] > 0, "Short ISO record or absent name")
        try:
            name = bounded(row, 33, row[32]).decode("ascii").removesuffix(";1")
        except UnicodeDecodeError as error:
            raise ValueError("Non-ASCII ISO name") from error
        require(name not in result and u32(row, 2) == int.from_bytes(row[6:10], "big") and
                u32(row, 10) == int.from_bytes(row[14:18], "big"), "Duplicate or endian-inconsistent ISO owner")
        require(row[26:28] == bytes(2) and row[28:32] == b"\x01\x00\x00\x01" and row[25] & 0x80 == 0,
                "Interleaved, multi-extent or foreign-volume ISO file refused")
        result[name] = (u32(row, 2), u32(row, 10), cursor)
        cursor += len(row)
    require(not any(root[cursor:]), "ISO root has nonzero unparsed tail")
    return result


def fixed_root(original, new_wad_size):
    records = iso_directory(original)
    require(records.get("WAD.WAD", ())[:2] == (WAD_LBA, WAD_SIZE) and
            records.get("SCUS_942.28", ())[:2] == (EXE_LBA, EXE_SIZE), "Original ISO WAD/EXE owners differ")
    require(new_wad_size == WAD_SIZE + GROWTH and WAD_LBA + WAD_SIZE // USER == EXE_LBA,
            "Exact adjacent WAD/EXE relocation geometry differs")
    final_exe_lba = WAD_LBA + new_wad_size // USER
    final_end = final_exe_lba + EXE_SIZE // USER
    require(final_end * RAW <= USA_SIZE, "Relocated executable exceeds raw image")
    for name, (lba, size, _) in records.items():
        if name not in ("WAD.WAD", "SCUS_942.28") and size:
            require(lba + (size + USER - 1) // USER <= WAD_LBA or lba >= final_end,
                    "Another ISO file/directory overlaps the complete relocated WAD/EXE owner")
    result = bytearray(original)
    for name, lba, size in (("WAD.WAD", WAD_LBA, new_wad_size), ("SCUS_942.28", final_exe_lba, EXE_SIZE)):
        at = records[name][2]
        struct.pack_into("<I", result, at + 2, lba)
        struct.pack_into(">I", result, at + 6, lba)
        struct.pack_into("<I", result, at + 10, size)
        struct.pack_into(">I", result, at + 14, size)
    iso_directory(bytes(result))
    return bytes(result)


def inverse_root(original, after):
    require(after == fixed_root(original, WAD_SIZE + GROWTH), "Root is not the exact fixed afterimage")
    records = iso_directory(original)
    return restore_fields(original, after, tuple((records[name][2] + 2, 16) for name in ("WAD.WAD", "SCUS_942.28")))


def prepare_padding(before, lba):
    require(len(before) == RAW and before[16:20] == before[20:24] and before[16:18] == b"\0\0" and
            before[19] == 0 and before[18] in (8, 32), "Consumed padding has an unexpected XA owner")
    result = bytearray(before)
    if before[18] == 32:
        require(not any(before[24:2348]) and u32(before, 2348) == oracle.edc(before[16:2348]),
                "Consumed Form2 padding is nonzero or corrupt")
        result[18] = result[22] = 8
        result[24:] = bytes(RAW - 24)
    else:
        require(not any(before[24:2072]) and rebuild_sector(before, lba) == before,
                "Consumed Form1 padding is nonzero or corrupt")
    # Rebuild also authenticates sync, physical BCD MSF and MODE2 for Form2.
    return rebuild_sector(result, lba)


def expected_sector(before, lba, wad, executable, root, root_lba, wad_flags, exe_flags):
    final_exe_lba = WAD_LBA + len(wad) // USER
    final_end = final_exe_lba + len(executable) // USER
    original_end = EXE_LBA + EXE_SIZE // USER
    owned = WAD_LBA <= lba < final_end or lba == root_lba
    if not owned:
        return before
    result = bytearray(prepare_padding(before, lba) if original_end <= lba < final_end else before)
    if WAD_LBA <= lba < final_exe_lba:
        result[24:2072] = bounded(wad, (lba - WAD_LBA) * USER, USER)
    elif final_exe_lba <= lba < final_end:
        result[24:2072] = bounded(executable, (lba - final_exe_lba) * USER, USER)
    elif lba == root_lba:
        result[24:2072] = root
    if lba in (EXE_LBA - 1, original_end - 1, final_exe_lba - 1, final_end - 1):
        flags = wad_flags if lba == final_exe_lba - 1 else exe_flags if lba == final_end - 1 else 0
        result[18] = result[22] = (result[18] & ~0x81) | flags
    if result == before:
        return before
    if lba < original_end:
        require(rebuild_sector(before, lba) == before, f"Source EDC/ECC failed at LBA {lba}")
    return rebuild_sector(result, lba)


def inverse_sector(after, lba, before, inverse_wad_bytes, inverse_exe_bytes, inverse_root_bytes, root_lba):
    original_end = EXE_LBA + EXE_SIZE // USER
    final_end = original_end + GROWTH // USER
    if original_end <= lba < final_end:
        prepare_padding(before, lba)
        return before  # Exact authenticated zero Form2/Form1 padding owner.
    if not (WAD_LBA <= lba < original_end or lba == root_lba):
        return after
    result = bytearray(after)
    result[:24] = before[:24]  # Original native XA flags and physical header.
    if WAD_LBA <= lba < EXE_LBA:
        result[24:2072] = bounded(inverse_wad_bytes, (lba - WAD_LBA) * USER, USER)
    elif EXE_LBA <= lba < original_end:
        result[24:2072] = bounded(inverse_exe_bytes, (lba - EXE_LBA) * USER, USER)
    else:
        result[24:2072] = inverse_root_bytes
    if result == before:
        return before
    return rebuild_sector(result, lba)


def verify_raw(source, candidate, transform, inverse, source_digest, progress=False):
    length = source.initial.st_size
    require(type(length) is int and length > 0 and length % RAW == 0, "Original is not complete raw sectors")
    if candidate is not None:
        require(candidate.initial.st_size == length, "Candidate whole raw extent differs")
    source_hash, expected_hash, inverse_hash = hashlib.sha256(), hashlib.sha256(), hashlib.sha256()
    changed, changed_bytes = 0, 0
    for offset in range(0, length, RAW * 128):
        before = source.read(offset, min(RAW * 128, length - offset))
        actual = candidate.read(offset, len(before)) if candidate is not None else None
        source_hash.update(before)
        for within in range(0, len(before), RAW):
            lba = (offset + within) // RAW
            old = before[within:within + RAW]
            expected = transform(old, lba)
            require(type(expected) is bytes and len(expected) == RAW, "Expected sector has invalid extent/type")
            current = expected if actual is None else actual[within:within + RAW]
            require(current == expected, f"Candidate differs from fixed physical afterimage at LBA {lba}")
            if expected != old:
                changed += 1
                changed_bytes += sum(a != b for a, b in zip(old, expected))
            restored = inverse(current, lba, old)
            require(restored == old, f"Complete original raw inverse differs at LBA {lba}")
            expected_hash.update(expected)
            inverse_hash.update(restored)
            if progress and lba % 5000 == 0:
                print(f"physical card-load verification: LBA {lba}, {changed} changed sectors", file=sys.stderr, flush=True)
    require(source_hash.hexdigest() == source_digest == inverse_hash.hexdigest(), "Complete original/inverse authentication differs")
    return {"expectedCandidateSha256": expected_hash.hexdigest(), "inverseOriginalSha256": inverse_hash.hexdigest(),
            "changedRawSectors": changed, "changedRawBytes": changed_bytes,
            "candidateVerified": candidate is not None, "completeRawComplementVerified": True,
            "completeOriginalInverseVerified": True}


def require_cue(data):
    require(type(data) is bytes and 0 < len(data) <= 65536, "Invalid bounded CUE")
    try:
        lines = [line.strip() for line in data.decode("utf-8-sig").splitlines() if line.strip()]
    except UnicodeDecodeError as error:
        raise ValueError("CUE is not UTF-8") from error
    match = re.fullmatch(r'FILE\s+"([^"\r\n]+)"\s+BINARY', lines[0], re.IGNORECASE) if lines else None
    require(len(lines) == 3 and match is not None and match.group(1) == IMAGE_NAME and
            lines[1].upper() == "TRACK 01 MODE2/2352" and lines[2].upper() == "INDEX 01 00:00:00",
            "CUE does not bind the exact single-track candidate name/geometry")


def source_alias(path):
    absolute = Path(os.path.abspath(path))
    resolved = absolute.resolve(strict=True)
    signature = lambda p: (lambda s: (s.st_dev, s.st_ino, s.st_mode, s.st_mtime_ns, s.st_ctime_ns))(os.lstat(p))
    chain = [(p, signature(p)) for p in (absolute, *absolute.parents)]
    def stable():
        require(absolute.resolve(strict=True) == resolved and all(signature(p) == prior for p, prior in chain),
                "Source alias chain changed")
    return resolved, stable


def verify(usa, candidate=None, cue=None, quiet=False):
    require(usa is not None, "An explicit original USA BIN is required")
    require(cue is None or candidate is not None, "A candidate CUE requires an explicit candidate BIN")
    if candidate is not None:
        require(Path(candidate).name == IMAGE_NAME, "Candidate filename is outside the fixed research profile")
    if cue is not None:
        require(Path(os.path.abspath(cue)).parent == Path(os.path.abspath(candidate)).parent,
                "Candidate BIN and CUE must share their explicit directory")
    resolved, alias_stable = source_alias(usa)
    with contextlib.ExitStack() as stack:
        source = stack.enter_context(ReadOnlyFile(resolved, USA_SIZE))
        require(source.initial.st_size == USA_SIZE and source.digest() == USA_SHA, "Requires complete exact original USA image")
        pvd = logical(source, 16, USER, 0, USER)
        require(pvd[:7] == b"\x01CD001\x01" and u32(pvd, 158) == int.from_bytes(pvd[162:166], "big") == 22 and
                u32(pvd, 166) == int.from_bytes(pvd[170:174], "big") == USER, "Original ISO PVD/root differs")
        root_lba = u32(pvd, 158)
        root = logical(source, root_lba, USER, 0, USER)
        original_wad = logical(source, WAD_LBA, WAD_SIZE, 0, WAD_SIZE)
        original_exe = logical(source, EXE_LBA, EXE_SIZE, 0, EXE_SIZE)
        require(sha(original_wad) == WAD_SHA and sha(original_exe) == EXE_SHA, "Complete original WAD/EXE identity differs")
        require((u32(original_wad, 16), u32(original_wad, 20)) == (0x5B800, OLD_OVERLAY_SIZE), "Exact source card owner differs")
        original_overlay = bounded(original_wad, u32(original_wad, 16), u32(original_wad, 20))
        require(sha(original_overlay) == CARD_SHA, "Complete original card allocation identity differs")
        overlay_after = fixed_overlay(original_overlay)
        wad_after, entry_reports = packed_wad(original_wad, overlay_after)
        exe_after = fixed_executable(original_exe)
        root_after = fixed_root(root, len(wad_after))
        inverse_wad_bytes = inverse_wad(original_wad, wad_after)
        inverse_exe_bytes = inverse_executable(original_exe, exe_after)
        inverse_root_bytes = inverse_root(root, root_after)
        final_exe_lba = WAD_LBA + len(wad_after) // USER
        final_end = final_exe_lba + len(exe_after) // USER
        wad_flags = source.read((EXE_LBA - 1) * RAW + 18, 1)[0] & 0x81
        exe_flags = source.read((EXE_LBA + EXE_SIZE // USER - 1) * RAW + 18, 1)[0] & 0x81
        output = stack.enter_context(ReadOnlyFile(candidate, USA_SIZE)) if candidate is not None else None
        transform = lambda raw, lba: expected_sector(raw, lba, wad_after, exe_after, root_after, root_lba, wad_flags, exe_flags)
        invert = lambda raw, lba, old: inverse_sector(raw, lba, old, inverse_wad_bytes, inverse_exe_bytes, inverse_root_bytes, root_lba)
        result = verify_raw(source, output, transform, invert, USA_SHA, not quiet)
        if output is not None:
            require(logical(output, 16, USER, 0, USER) == pvd, "Candidate PVD changed")
            require(logical(output, root_lba, USER, 0, USER) == root_after, "Complete candidate ISO root differs")
            # Parse and reverse the actual candidate logical owners too.
            actual_wad = logical(output, WAD_LBA, len(wad_after), 0, len(wad_after))
            require(inverse_wad(original_wad, actual_wad) == original_wad, "Actual candidate WAD inverse differs")
            actual_exe = logical(output, final_exe_lba, EXE_SIZE, 0, EXE_SIZE)
            require(inverse_executable(original_exe, actual_exe) == original_exe, "Actual candidate EXE inverse differs")
        cue_hash = None
        if cue is not None:
            cue_file = stack.enter_context(ReadOnlyFile(cue, 65536))
            cue_bytes = cue_file.read(0, cue_file.initial.st_size)
            require_cue(cue_bytes)
            cue_hash = sha(cue_bytes)
            cue_file.stable(cue_hash)
        if output is not None:
            output.stable(result["expectedCandidateSha256"])
        source.stable(USA_SHA)
        alias_stable()
        return dict(result, schemaVersion=1, profile=PROFILE,
                    mode="fixed-recipe-independent-physical-verification" if output is not None else "original-only-fixed-recipe-prediction",
                    sourceUsaSha256=USA_SHA, candidateImageName=IMAGE_NAME,
                    originalWadSha256=WAD_SHA, expectedWadSha256=sha(wad_after),
                    originalExecutableSha256=EXE_SHA, expectedExecutableSha256=sha(exe_after),
                    originalOverlaySha256=CARD_SHA, expectedOverlaySha256=sha(overlay_after),
                    expectedRootSha256=sha(root_after), finalWadByteLength=len(wad_after),
                    finalExecutableLba=final_exe_lba, finalEndLbaExclusive=final_end,
                    consumedPaddingLbas=list(range(EXE_LBA + EXE_SIZE // USER, final_end)),
                    originalAndNamedInputsStable=True, completeOriginalWadInverseVerified=True,
                    originalOverlayPrefixPreservedExceptTwoHooks=True, allOtherWadEntryPayloadsPreserved=True,
                    generatedCodeAuthority="immutable prepublication source-compiled block literals and SHA256 pins",
                    generatedCodeSemanticsIndependentlyReconstructed=False,
                    appendedBlocks=[{"id": name, "offset": at, "length": size,
                                     "sha256": sha(overlay_after[at:at + size]), "allZero": not any(overlay_after[at:at + size])}
                                    for name, at, size in append_layout()],
                    entries=entry_reports, cueVerified=cue is not None, cueSha256=cue_hash,
                    runtimeVerified=False, biosLoaderVerified=False, nativeResidenceIssued=False,
                    lifetimeProven=False, readyAdmissionAuthorized=False, saveVerified=False,
                    cardIoApproved=False, normalExportApproved=False, publicationApproved=False,
                    imageWrites=0, cardWrites=0, hostileFilesystemRaceSafe=False)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--usa", required=True, help="Explicit exact original USA BIN")
    parser.add_argument("--candidate", help="Explicit fixed-name candidate BIN; omitted predicts only")
    parser.add_argument("--cue", help="Optional explicit same-directory candidate CUE; requires candidate")
    parser.add_argument("--quiet", action="store_true", help="Suppress progress on stderr")
    args = parser.parse_args()
    print(json.dumps(verify(args.usa, args.candidate, args.cue, args.quiet), indent=2))


if __name__ == "__main__":
    main()
