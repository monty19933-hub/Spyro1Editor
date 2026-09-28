#!/usr/bin/env python3
"""Read-only original-only physical oracle for one closed admitted-egg profile.

Both complete retail images are authenticated. Authored models/scene/E800 code
are independently reconstructed by the existing egg oracle; card admission is
the immutable predecessor recipe. New lifecycle MIPS blocks are frozen from a
source-tested description BEFORE publication, not semantically recompiled here.
No candidate, receipt, supplied hash, CLI recipe, card or editor DLL is authority.
No image/card files are written. Physical equality is not runtime certification.
"""
from __future__ import annotations

import argparse
import base64
import contextlib
import importlib.util
import json
import os
from pathlib import Path
import re
import struct
import sys
import zlib

sys.dont_write_bytecode = True


def _load(name, filename):
    spec = importlib.util.spec_from_file_location(name, Path(__file__).with_name(filename))
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module


admission = _load("_admitted_egg_card_recipe", "Verify-SpyroCollectibleCardAdmission.py")
egg = _load("_admitted_egg_authored_recipe", "Verify-SpyroJapaneseEggResearch.py")
reader = admission.reader
require, sha, bounded = reader.require, reader.sha, reader.bounded
PROFILE = "native-artisans-two-japanese-eggs-admitted-lifecycle-research-v1"
IMAGE_NAME = "artisans-native-japanese-eggs-admitted-lifecycle-r01.bin"
CUE_NAME = "artisans-native-japanese-eggs-admitted-lifecycle-r01.cue"
ARTISANS_SIZE = 0x10000
ARTISANS_HOOKS = ((0xE204, 0x80088A38), (0xE0AC, 0x80088B14))
EXE_EDITS = admission.EXE_EDITS + ((0x4AF18, 8),)
# Frozen September 27 from source-only --describe after source573 checks.
# No candidate was published at freeze. This pins emitted bytes, not a second
# semantic implementation. The authored E800 ancestor remains reconstructed.
SOURCE_DESCRIPTION_SHA = "5c41b6b72e993bef314952d685aa6bba33243c9ab058f9d608f0e7a3049fba79"
# Each tuple: id, original-relative offset, full capacity, SHA256, zlib/base85,
# zero-initialized. No production CLI option can replace any of these pins.
BLOCKS = (
    ('pickup-predicate', 0xe800, 0x154, 'a9450ad10d4760f6fc23d07666d5f19c1abc04e5bd701faf29cb1a2d2bc901ec',
     'c-l>my-tKc6ot<Y2vd+a1u+!Hz(9n;f|7!QVCw@|Zrptk{D}pbEUizlmK4^j?l#+6n3!y$+4ULh^$w1Sx4CCdzH>4cxKuW-6fmcK'
     'uVlUi!_t<;CuHuB-r1m$g$v2oa1ageiaQ92T@k~vwL-Bb&qY}5up;))^DsT0CCrPmXX|TJXD6tH&KH>6`nZ_!#xBS0jf#U)#A1p)'
     'RDtGPRKDik62|q8#Wl2lX!5*a<(aCL)(GqTfcSl|gf~|C(oP1S`u!*yGtIuXzpQ6^H>P*{y+{QfdLNH{fTupXYpDCEJa0eJxnV%P'
     '??#XQY07n!>nPU^f6_N&k^TT^BSVe', False),
    ('collect-state', 0xe954, 0x3d0, '73298610c23900127b02ec087c600eb9699a2aebe081e5ff887dec3eef31948d',
     'c-mdkK}b|V7zglgcD44)5?vIOf)w6^rc8upsKXpy6tTpLVPKbt4I%<V_7JgXhYg~hTW%|P$cQp9Ftfl@mx9PkmpTN2hluJH9fF`v'
     ';%nY}e{^s!^Ud#l^F7{wc5gLj=wD9%ps<7NFGD$Xps+ySsi!^Ow^4W}UW>zQP9KwcmAEtBcfbX)5hV2|co&6@bPgZ<fEnG6y@&0='
     'c_LOTINJjosj$f_so%i8E&d9=51v<S8hNRg;eGH2ui1=Lq<#(`fPYzJ^Kn_~r*H*7^q0;0nADHqN8lHj&COY<XW<6^GGG&Zmiit%'
     'fM2@Irgc*4Dfn^txkqe@Hp5%+0{q2SHY=-AUxydrzt-6V{ZdcB%bS_2tFSB1J!I@Xl;IT$XOOuXh23y6zX%H*I}e+KWv!is%{zT('
     'U<-~7!<HNygf(DM#AYp$+6!2mAWELiIQ>V_f8Q|!d+6BVjkz_Cs71BWkZ)6)PXDii$?BD+#5St7r|+%%Ua2kwjrwa*wKL7@r2i)x'
     'O6qZB)k`hO{#~J&dV@(jf9G_X$F5}EPR|Ae?NCjp_6x7PD0b7^WWHCf#dUJ_8r|La*2sQq7yV3n{8BA>zo6f~o%>{*ZqM7^B|)vK'
     'o9c7+rc{o;W#ir^r_MTc#;Ju<H#_xz9Y3J#', False),
    ('query-state', 0xed24, 0x38c, '2e4505b688d69161cb75dd0cb3f9e3437c5095997a1342994f460f546945ab63',
     'c-mdkPe@cj90%~<>}u^VGjwyIAjse?G-)C@10Du?kcTZcl?3Txh;$GtS<u7kr9D(Utz;L3{!kI<L8TNAUPMUBlZP%Uc!==sP0*A0'
     'e1E>^(7ViUK5ynXkMF$erIew=l-?q*fr2MNDb<vlrA=zA59f8{y$#3wLpD8krQRUUg!2ZtAU1-eUWZ>HuN?Q`+dt9cs%0y%YuHc3'
     'Y8iVQVZB8*X(ja{-1*Pv;5XrE#ipE=dK%sg|Fp`cw<z@&cq{zxJexmDQon~Qc-sM+;~}Xl@OJnUX7l=^)NkMhzTmR)x21jscj1H2'
     '*jyTy`USiL{`f1Ke30QNJO}^rkInBLsfXct_}(!aw?*pb@WNT<Y7jPJ-9sX{hXHtzyeVX^`d}}u%pbu#%X(nnV9CHbVKdgcd$3u{'
     'I$+-|y8|o3e4ovcFSX;cXeWx^O<C*Pu>ON(EwIm)$<w|Q4pEJ&qpoa_S|$EpA4H?m|4y8vQhj{xWY+1Q6oQ^+kNi?5&g*`5-|vd*'
     'F=Ta_YNGkCTs7Tr$AkVCD{&sR(RiNbS_NHDRo%CQQ<xXEv=;Su3KL<SiN7zQQnZ^4t17EbT6Myzg;k#b>>!P|', False),
    ('pickup-attempt', 0xf0b0, 0x158, '3e2cd95d22d19b0f2759f5773d3a9630d25008b2e444d2fec953d49d0b430b34',
     'c-oV1;ILu%#$oEkutJ0Z2*iQ>3(*{=91I--Fuq0%hp8Bp&&I%H!ol!>nE?zsdLU{zZ4~Bcn%XdMih}rN3>{NI^h-ug6LYAT2SdjM'
     'h#Z3`L@$SlFav`Cx;Z*PbF|RR`2;ja70nzKs5y*4a~PrKJovv?U4UW#dJ%>t>m?YLu9sn0vHqM26Aws^lfeP3MuK5UKZurLSPG^e'
     'L*<#EW^e-iCjwO?0aXJu6HJ5rt-!EXodqcj*pb*ANNfPp?lA5', False),
    ('egg-lifecycle', 0xf208, 0x398, '134e07f0a5ecf019ab414fac2a3b7a2720c58b128ef30e2db35af41fe9e5f7dc',
     'c-oV1;ILu%#$lSputJ0Z2*iQ>3(*{=91I--Fuq0%hp94@&&I%H!ol!>nE?zsdLU{zZ4~Bcn#M42ih}rN3>{NI^h-uglW?e*2SdjM'
     '5FN(A0Op%O)%^d@Y2pV}!w$4-LIj5?0|Q4d%-n_;O;b0hJW$+3fq_8)-EJMA-L`0Ue*)TVfo8Wk7Q6Kc*sTtgX9U{K2(|mc|Gnx0'
     '4ExuMFf3Uw!LW2a1H%#(1_oBu>#9sVAbCCp2LYHK9tIC^xN-x-O@?6+NKZeAALYpiRtppZ^O>M#a{<-C)Bw!}(?GkxG$`B^81|~O'
     'AjJVY5}N~wEf0;?6e`4P7?yYqL<`#hEMe<GAZ#6oh*xtg@oI!-w;>k0H3`_QNJPBuQiH@N6N85i7XyPJG`?`gA<P_V#2)~xONWI', False),
    ('pre-visual-bridge', 0xf5a0, 0x3c, '0e77ad56e2ac9bb79dad3eb387ddf009a91e1021e84eea71eae0520ab51b909b',
     'c-m7`NMMq0V6|cRw$wyGor#A52)GzHOxPGa1VC&yAe$M=KB~mT0iye$GywLI27~', False),
    ('post-visual-bridge', 0xf5dc, 0x1c, '1359033b4dfb9c34e69fbedfb20e4457d7ff867dddd81af6170f3ee9fcf4e69f',
     'c-m7`NMMq0V6|cRw$!9eor#A52#zW-ae!z5SjPna', False),
    ('live-consumer-guard', 0xf5f8, 0x800, '04ccb42bf627100bec0ff9614dd987dc16f55ae0cf9bbdb0ba647eb543e9d8fd',
     'c-rG=;J3Lk|GlYr1BcCpXbw|hh72JFAOP_-VmM6sq5M82CJqqI0aP!t0HK~8O+7P|e-x^ooneLXY6b>T5L+IoCgBH%skX-o=4A{D'
     'aIt{jATe3Sg-|i}25y@rq1mPk3>m!)V8Cg^z@=$AlYtX1pA(5H&&8l=IuS|!MmU;&pnNBi{Dd$}`9>sp6D0lY4V*S7ZfcrVBAefe'
     'kmmr(TddMFEku%kfFv&nln<%ZG|fbke}W`029-}llGlhp$cqEz85A^4Baz*YCf^XFX&Q(mFO!XCeyXOaCz8AantV*Irl}*6yhtx<'
     '_%H&)#}e86Ud-?_gv!eU<;A{%<tHNR*F=&RN0SHXS45JRK$2%-;MA33P!I$wb>slsF9Ehc6KuaQl0GSjzNs*GDj=!jL{iuD4dH%f'
     'U=)Glk>LR&#63(lEDTO2urxdhM!_f;1;Y^l00u#Vlm', False),
    ('alignment', 0xfdf8, 0x208, '20aa497d9bd4c19e851e3df6e386700faada213db38acf7679f6365832830b3d',
     "", True),
)
BEFORE_PC, AFTER_PC = 0x8008A030, 0x8008A060


def growth():
    require(type(ARTISANS_SIZE) is int and ARTISANS_SIZE == 0x10000,
            "Closed final Artisans allocation differs")
    return admission.GROWTH + ARTISANS_SIZE - 0xE000 + 0x2800


def frozen_blocks():
    require(bool(BLOCKS), "Reviewed lifecycle recipe is not frozen; publication refused")
    cursor, names, result = 0xE800, set(), []
    for name, at, length, digest, encoded, zero in BLOCKS:
        require(type(name) is str and name and name not in names and type(at) is int and
                type(length) is int and at == cursor and length > 0 and length % 4 == 0 and
                at + length <= ARTISANS_SIZE and type(zero) is bool,
                "Lifecycle block owner gap/overlap/type/extent differs")
        require(type(digest) is str and re.fullmatch('[0-9a-f]{64}', digest) is not None,
                "Lifecycle block identity is invalid")
        if zero:
            require(encoded == "", "Zero owner cannot carry code bytes")
            data = bytes(length)
        else:
            try:
                data = zlib.decompress(base64.b85decode(encoded))
            except (TypeError, ValueError, zlib.error) as error:
                raise ValueError("Invalid frozen lifecycle encoding") from error
        require(len(data) == length and sha(data) == digest, "Frozen lifecycle block identity differs")
        result.append((name, at, data, zero)); names.add(name); cursor += length
    require(cursor == ARTISANS_SIZE, "Frozen append does not cover the complete allocation")
    require(type(BEFORE_PC) is int and type(AFTER_PC) is int and BEFORE_PC < AFTER_PC and
            all(pc % 4 == 0 and any(not zero and reader.OVERLAY_BASE + at <= pc < reader.OVERLAY_BASE + at + len(data)
                                  for _, at, data, zero in result) for pc in (BEFORE_PC, AFTER_PC)),
            "Guard hook target is not a frozen code entry")
    growth()
    return result


def append_authored(authored):
    blocks = frozen_blocks()
    require(type(authored) is bytes and len(authored) == 0xE800, "Complete authored E800 ancestor required")
    result = bytearray(authored)
    for (at, original_target), target in zip(ARTISANS_HOOKS, (BEFORE_PC, AFTER_PC)):
        require(struct.unpack_from('<II', authored, at) == (reader.jump(original_target), 0),
                "Authored lifecycle source hook or delay slot differs")
        struct.pack_into('<I', result, at, reader.jump(target))
    for _, at, data, _ in blocks:
        require(at == len(result), "Lifecycle append cursor differs")
        result.extend(data)
    reader.restore_fields(authored, result[:0xE800], tuple((at, 4) for at, _ in ARTISANS_HOOKS))
    return bytes(result)


def inverse_authored(authored, after):
    require(type(after) is bytes and after == append_authored(authored), "Complete guarded Artisans afterimage differs")
    return reader.restore_fields(authored, after[:0xE800], tuple((at, 4) for at, _ in ARTISANS_HOOKS))


def fixed_executable(original):
    growth()
    require(type(original) is bytes and len(original) == reader.EXE_SIZE and
            original[0x4AF18:0x4AF28] == struct.pack('<4I', 0x3C028009, 0x24428620, 0x3C018008, 0xAC2285D8),
            "Original Artisans copy-anchor instruction owner differs")
    result = bytearray(admission.fixed_executable(original))
    require(result[0x4AF18:0x4AF20] == original[0x4AF18:0x4AF20], "EXE owners collide")
    anchor = reader.OVERLAY_BASE + ARTISANS_SIZE
    low = struct.unpack('<h', struct.pack('<H', anchor & 65535))[0]
    struct.pack_into('<II', result, 0x4AF18, 0x3C020000 | ((anchor-low) >> 16), 0x24420000 | (anchor & 65535))
    reader.restore_fields(original, result, EXE_EDITS)
    return bytes(result)


def inverse_executable(original, after):
    require(type(after) is bytes and after == fixed_executable(original), "Complete combined EXE afterimage differs")
    parent = bytearray(after); parent[0x4AF18:0x4AF20] = original[0x4AF18:0x4AF20]
    return admission.inverse_executable(original, bytes(parent))


def fixed_replacements(original, jp_overlay, jp_data):
    entries = {i: bounded(original, at, size) for at, size, i in reader.wad_entries(original)}
    require({2, 9, 10}.issubset(entries), "Missing one of the three fixed original owners")
    require(len(entries[2]) == 0x3800 and len(entries[9]) == 0xE000 and len(entries[10]) == 0x383000,
            "Original card/Artisans allocation lengths differ")
    authored = egg.authored_overlay_afterimage(entries[9], jp_overlay)
    data, _, _ = egg.authored_level_afterimage(entries[10], jp_data)
    require(len(data) == 0x385800, "Complete independently rebuilt authored data differs")
    return {2: admission.fixed_overlay(entries[2]), 9: append_authored(authored), 10: data}


def packed_wad(original, jp_overlay, jp_data):
    replacements = fixed_replacements(original, jp_overlay, jp_data)
    require(set(replacements) == {2, 9, 10}, "Only the three fixed replacement owners are admitted")
    expected_lengths = {2: 0xD000, 9: ARTISANS_SIZE}
    result, reports = bytearray(original[:reader.USER]), []
    for at, size, index in reader.wad_entries(original):
        before = bounded(original, at, size); after = replacements.get(index, before)
        expected = expected_lengths.get(index, size + (0x2800 if index == 10 else 0))
        require(type(after) is bytes and len(after) == expected, "Fixed replacement growth differs")
        new_at = len(result); struct.pack_into('<II', result, index*8, new_at, len(after)); result.extend(after)
        reports.append(dict(index=index, originalOffset=at, finalOffset=new_at, originalLength=size,
                            finalLength=len(after), originalSha256=sha(before), finalSha256=sha(after)))
    require(len(result) == len(original) + growth(), "Three-owner packed WAD growth differs")
    reader.wad_entries(bytes(result))
    return bytes(result), reports


def inverse_wad(original, after, jp_overlay, jp_data):
    require(type(after) is bytes, "Complete immutable WAD afterimage required")
    replacements = fixed_replacements(original, jp_overlay, jp_data)
    old, new = reader.wad_entries(original), reader.wad_entries(after)
    require(len(after) == len(original) + growth() and len(old) == len(new), "Inverse WAD extent/count differs")
    header, payload = bytearray(after[:reader.USER]), bytearray()
    for (at, size, index), (new_at, new_size, new_index) in zip(old, new):
        before = bounded(original, at, size); expected = replacements.get(index, before)
        current = bounded(after, new_at, new_size)
        require(index == new_index and current == expected, "Complete WAD owner mapping or payload differs")
        if index == 2:
            require(admission.inverse_overlay(before, current) == before, "Card ancestor inverse differs")
        elif index == 9:
            authored = egg.authored_overlay_afterimage(before, jp_overlay)
            require(inverse_authored(authored, current) == authored, "Authored prefix inverse differs")
        # The independently reconstructed authored model/scene and E800 recipes
        # each execute their complete original inverse while deriving expected.
        payload.extend(before if index in replacements else current)
        struct.pack_into('<II', header, index*8, at, size)
    result = bytes(header) + bytes(payload)
    require(result == original, "Complete original WAD directory/payload inverse differs")
    return result


def fixed_root(original, size):
    return reader._fixed_card_root(original, size, growth())


def inverse_root(original, after):
    require(after == fixed_root(original, reader.WAD_SIZE + growth()), "Complete ISO root afterimage differs")
    records = reader.iso_directory(original)
    return reader.restore_fields(original, after, tuple((records[n][2]+2,16) for n in ('WAD.WAD','SCUS_942.28')))


def inverse_sector(after, lba, before, wad, exe, root, root_lba):
    end = reader.EXE_LBA + reader.EXE_SIZE // reader.USER
    if end <= lba < end + growth() // reader.USER:
        reader.prepare_padding(before, lba)
        return before
    return reader.inverse_sector(after, lba, before, wad, exe, root, root_lba)


def require_cue(data):
    require(type(data) is bytes and 0 < len(data) <= 65536, "Invalid bounded CUE")
    lines = [line.strip() for line in data.decode('utf-8-sig').splitlines() if line.strip()]
    match = re.fullmatch(r'FILE\s+"([^"\r\n]+)"\s+BINARY', lines[0], re.IGNORECASE) if lines else None
    require(len(lines) == 3 and match is not None and match.group(1) == IMAGE_NAME and
            lines[1].upper() == 'TRACK 01 MODE2/2352' and lines[2].upper() == 'INDEX 01 00:00:00',
            "CUE is outside the fixed admitted lifecycle profile")


def final_geometry(data, rows, bindings):
    previous = egg.runtime_owners(data, rows)
    delta = ARTISANS_SIZE - 0xE800
    result = {k: previous[k] + delta for k in ('copy_anchor','model_anchor','scene_anchor','moby_base','dynamic_base')}
    result['rows'] = [value + delta for value in previous['rows']]
    result['private'] = [value + delta for value in previous['private']]
    result['authored_roots'] = [result['model_anchor'] + b['PackageDataOffset'] - reader.u32(data,0x10) for b in bindings]
    result['scene_end'] = result['scene_anchor'] + reader.u32(data,0x1C)
    require(result['copy_anchor'] == reader.OVERLAY_BASE + ARTISANS_SIZE and
            result['scene_end'] <= 0x801FF600, "Final reconstructed owner geometry overlaps live ledger")
    return result


def verify(usa, japan, candidate=None, cue=None, quiet=False):
    blocks = frozen_blocks()
    require(cue is None or candidate is not None, "Candidate CUE requires explicit BIN")
    if candidate is not None:
        require(Path(candidate).name == IMAGE_NAME, "Candidate filename is outside fixed profile")
    if cue is not None:
        require(Path(cue).name == CUE_NAME and Path(os.path.abspath(cue)).parent == Path(os.path.abspath(candidate)).parent,
                "Fixed candidate BIN/CUE must share their explicit directory")
    aliases = [reader.source_alias(path) for path in (usa, japan)]
    with contextlib.ExitStack() as stack:
        source, donor = [stack.enter_context(reader.ReadOnlyFile(path, limit)) for (path,_),limit in zip(aliases,(reader.USA_SIZE,egg.JP_SIZE))]
        require(source.initial.st_size == reader.USA_SIZE and source.digest() == reader.USA_SHA, "Requires exact complete original USA image")
        require(donor.initial.st_size == egg.JP_SIZE and donor.digest() == egg.JP_SHA, "Requires exact complete original Japanese image")
        root_lba, root, _ = egg.iso_source(source,(reader.WAD_LBA,reader.WAD_SIZE),(reader.EXE_LBA,reader.EXE_SIZE),'SCUS_942.28')
        egg.iso_source(donor,(500,112793600),(24,0x70000),'SCPS_100.83')
        pvd = reader.logical(source,16,reader.USER,0,reader.USER)
        original = reader.logical(source,reader.WAD_LBA,reader.WAD_SIZE,0,reader.WAD_SIZE)
        original_exe = reader.logical(source,reader.EXE_LBA,reader.EXE_SIZE,0,reader.EXE_SIZE)
        require(sha(original) == reader.WAD_SHA and sha(original_exe) == reader.EXE_SHA, "Original complete USA WAD/EXE identity differs")
        require(sha(reader.logical(donor,24,0x70000,0,0x70000)) == egg.JP_EXE_SHA, "Original complete Japanese EXE identity differs")
        jp_header = reader.logical(donor,500,112793600,0,reader.USER)
        def jp_entry(index):
            at, length = struct.unpack_from('<II',jp_header,index*8)
            require(at >= reader.USER and length > 0 and at % reader.USER == length % reader.USER == 0,
                    "Regional donor allocation differs")
            return reader.logical(donor,500,112793600,at,length)
        jp_overlay, jp_data = jp_entry(22), jp_entry(23)
        wad, entries = packed_wad(original,jp_overlay,jp_data)
        exe = fixed_executable(original_exe); root_after = fixed_root(root,len(wad))
        inverse_wad_bytes = inverse_wad(original,wad,jp_overlay,jp_data)
        inverse_exe_bytes = inverse_executable(original_exe,exe); inverse_root_bytes = inverse_root(root,root_after)
        final_exe_lba = reader.WAD_LBA + len(wad)//reader.USER
        final_end = final_exe_lba + len(exe)//reader.USER
        wad_flags = source.read((reader.EXE_LBA-1)*reader.RAW+18,1)[0]&0x81
        exe_flags = source.read((reader.EXE_LBA+reader.EXE_SIZE//reader.USER-1)*reader.RAW+18,1)[0]&0x81
        output = stack.enter_context(reader.ReadOnlyFile(candidate,reader.USA_SIZE)) if candidate is not None else None
        transform = lambda old,lba: reader.expected_sector(old,lba,wad,exe,root_after,root_lba,wad_flags,exe_flags)
        invert = lambda current,lba,old: inverse_sector(current,lba,old,inverse_wad_bytes,inverse_exe_bytes,inverse_root_bytes,root_lba)
        result = reader.verify_raw(source,output,transform,invert,reader.USA_SHA,not quiet)
        if output is not None:
            require(reader.logical(output,16,reader.USER,0,reader.USER) == pvd, "Original PVD changed")
            actual_root = reader.logical(output,root_lba,reader.USER,0,reader.USER)
            require(inverse_root(root,actual_root) == root, "Actual ISO root inverse differs")
            actual_wad = reader.logical(output,reader.WAD_LBA,len(wad),0,len(wad))
            require(inverse_wad(original,actual_wad,jp_overlay,jp_data) == original, "Actual complete WAD inverse differs")
            actual_exe = reader.logical(output,final_exe_lba,reader.EXE_SIZE,0,reader.EXE_SIZE)
            require(inverse_executable(original_exe,actual_exe) == original_exe, "Actual EXE inverse differs")
        cue_hash = None
        if cue is not None:
            cue_file = stack.enter_context(reader.ReadOnlyFile(cue,65536))
            cue_bytes = cue_file.read(0,cue_file.initial.st_size); require_cue(cue_bytes)
            cue_hash = sha(cue_bytes); cue_file.stable(cue_hash)
        if output is not None: output.stable(result['expectedCandidateSha256'])
        source.stable(reader.USA_SHA); donor.stable(egg.JP_SHA)
        for _, stable in aliases: stable()
        old_data = bounded(original,reader.u32(original,80),reader.u32(original,84))
        data, rows, bindings = egg.authored_level_afterimage(old_data,jp_data)
        return dict(result,schemaVersion=1,profile=PROFILE,
                    mode='independent-candidate-verification' if output else 'original-only-fixed-recipe-prediction',
                    sourceUsaSha256=reader.USA_SHA,sourceJapanSha256=egg.JP_SHA,candidateImageName=IMAGE_NAME,
                    expectedWadSha256=sha(wad),expectedExecutableSha256=sha(exe),expectedRootSha256=sha(root_after),
                    finalWadByteLength=len(wad),finalExecutableLba=final_exe_lba,finalEndLbaExclusive=final_end,
                    consumedPaddingLbas=list(range(reader.EXE_LBA+reader.EXE_SIZE//reader.USER,final_end)),
                    entries=entries,changedWadEntryIndices=[2,9,10],allOtherWadEntryPayloadsPreserved=True,
                    originalAndNamedInputsStable=True,completeOriginalWadInverseVerified=True,
                    finalNormalLoadOwners=final_geometry(data,rows,bindings),
                    generatedCodeAuthority='immutable prepublication source-compiled lifecycle blocks and frozen card predecessor',
                    prepublicationSourceDescriptionSha256=SOURCE_DESCRIPTION_SHA,
                    generatedCodeSemanticsIndependentlyReconstructed=False,
                    lifecycleBlocks=[dict(id=n,offset=a,length=len(b),sha256=sha(b),allZero=not any(b)) for n,a,b,_ in blocks],
                    cueVerified=cue is not None,cueSha256=cue_hash,
                    runtimeVerified=False,nativeResidenceIssued=False,lifetimeProven=False,interruptSafetyProven=False,
                    readyAdmissionAuthorized=False,pickupVerified=False,saveVerified=False,cardIoApproved=False,
                    normalExportApproved=False,publicationApproved=False,imageWrites=0,cardWrites=0,hostileFilesystemRaceSafe=False)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--usa',required=True); parser.add_argument('--japan',required=True)
    parser.add_argument('--candidate'); parser.add_argument('--cue'); parser.add_argument('--quiet',action='store_true')
    args = parser.parse_args()
    print(json.dumps(verify(args.usa,args.japan,args.candidate,args.cue,args.quiet),indent=2))
