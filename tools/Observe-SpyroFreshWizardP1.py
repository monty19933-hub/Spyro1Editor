#!/usr/bin/env python3
"""Fixed fresh A/P1/N32 diagnostic readers and closed external-session gates.

Root-reviewed phase2 permits only the fixed published candidate and independently
identified local DuckStation session. Published artifact and executable pins are
fixed here; no caller hash changes the connection profile. Memory/OS matches are
sequential observations, never gameplay, peak, audio, save/card or image-
authentication certificates. Existing observers are unchanged.
"""
from __future__ import annotations

import argparse
from collections import Counter
from dataclasses import asdict, dataclass, field
from datetime import datetime, timezone
import importlib.util
import hashlib
import json
import os
from pathlib import Path
import re
import selectors
import socket
import stat
import struct
import subprocess
import sys
import time

_spec=importlib.util.spec_from_file_location("p1_m_only_primitives",Path(__file__).with_name("Observe-SpyroNativeWizard.py"))
wizard=importlib.util.module_from_spec(_spec);sys.modules[_spec.name]=wizard;_spec.loader.exec_module(wizard)
ReadOnlyClient=wizard.ReadOnlyClient
decode_moby=wizard.decode_moby
primitive_slack=wizard.primitive_slack

PROFILE="fresh-A-P1-N32-Wizard-R1-track19-diagnostic-phase1"
EXPECTED_IMAGE_SHA256="c4a70f826b1a9a3d4ad8e36512be354b6205d27ea2c7a314776a51067f35b5a6"
CANDIDATE_BASENAME="native-id65-fresh-wizard-p1-n32-r01"
PROCESS_IDENTITY_LIMITATION="ps lstart has second-level precision; PID reuse within that second and hostile races are not excluded."
PLACED,BASE,RESERVE,PRIVATE,END=0x800A5408,0x800A5460,0x800A5F60,0x800A5FB8,0x800A62B8
WPRIVATE,POOL,PETE=0x800A62BC,0x801C3BB8,0x801C5BB8
GATES={
    "level":(0x8007596C,65),"game_state":(0x800757D8,0),"normal_classifier":(0x80075690,0),
    "flight_permission":(0x80078CA4,0),"world_index":(0x8007595C,5),"continuous_level_index":(0x80075964,35),
    "superflame_active":(0x80078764,0),"superflame_duration":(0x80078768,0),
    "copy_anchor":(0x800785D8,0x8008BA38),"copy_cursor":(0x800785DC,0x8008BA38),
    "model_anchor":(0x800785E0,0x8008C334),"scene_anchor":(0x800785E4,0x800A5334),
    "scene_disk_offset":(0x80078600,0x100000),"scene_disk_length":(0x80078604,0x3000),
    "moby_base":(0x80075828,PLACED),"dynamic_base":(0x80075890,BASE),"arena_end":(0x800756E8,END),
    "bucket_base":(0x80075778,0x800A6318),"dynamic_capacity":(0x800756A8,32),
    "resident_pete":(0x800785FC,PETE),"particle_allocation_base":(0x800785F8,POOL),"particle_base":(0x80075824,POOL),
    "player_root":(0x80076378,PETE),"companion_model_root":(0x80076558,0x8009CD80),
    "bolt_model_root":(0x80076410,0x8009F950),"wizard_model_root":(0x800767E4,0x8009FEB4),
    "fallback0e_model_root_low":(0x800763B0,0x000A3B34),"fallback0f_model_root":(0x800763B4,0x800A4320),
    "fallback10_model_root":(0x800763B8,0x800A4AAC),
    "callback_spawn":(0x800758CC,0x800872A4),"callback_tick":(0x80075734,0x8007DA54),
    "callback_effect_spawn":(0x800758E4,0x80088F68),"callback_effect_tick":(0x800756BC,0x80087E20),
    "callback4":(0x800757A0,0x8007AF2C),"callback5":(0x800758A8,0x8007B068),
    "callback6":(0x8007574C,0x8007B0AC),"callback7":(0x800758D8,0x8007D04C),
    "initializer_table":(0x800114A8,0x8005A898),
}
CURSORS={"dynamic_count":(0x800756A4,"<I"),"properties_cursor":(0x80075930,"<I"),
         "next_free_row":(0x8007573C,"<I"),"companion_handle":(0x80075898,"<I"),
         "next_free_particle":(0x80075738,"<I")}
RENDERER={"primitive_cursor":(0x800757B0,"<I"),"primitive_limit":(0x80075780,"<I"),
          "ordering_table":(0x80075820,"<I"),"renderer_exhaustion_flag":(0x800758B0,"<I")}
# Fixed raw diagnostic words only: no range thresholds, motion units or damage
# attribution. State6 alone and one cleared accumulator do not identify cause.
HEALTH_MOTION_FIELDS={
    "player_health_s32":(0x80078BBC,"<i"),"player_damage_accumulator_u32":(0x80078A84,"<I"),
    "player_motion_78b4c_s32x3":(0x80078B4C,"<iii"),"player_motion_78b64_s32x3":(0x80078B64,"<iii"),
    "player_motion_78ba4_s32":(0x80078BA4,"<i"),"player_state_input_78af4_s32":(0x80078AF4,"<i"),
    "player_state_input_78b08_s32":(0x80078B08,"<i"),
}
REPORT_FIELDS={name:wizard.FIELDS[name] for name in (
    "lives","player_xyz","player_heading_u8","camera_control_angle_s16","player_desired_bearing_u32",
    "player_state","player_substate","player_state_timer","player_animation_id","last_collision_saved_sp_not_peak")}|RENDERER|HEALTH_MOTION_FIELDS
FIELDS={name:(at,"<I") for name,(at,_) in GATES.items()}|CURSORS|REPORT_FIELDS
REREAD=tuple(GATES)+tuple(CURSORS)+tuple(RENDERER)+tuple(HEALTH_MOTION_FIELDS)


def _words(values):return struct.pack("<"+"I"*len(values),*values)
def _ins(op,rs,rt,imm):return op<<26|rs<<21|rt<<16|(imm&65535)
def _jump(at):return 0x08000000|((at>>2)&0x3FFFFFF)
def _move(to,source):return source<<21|to<<11|0x21


def _adapter(role,target):
    # Immutable expected READ fingerprints only; never installed/executed here.
    registers=tuple(r for r in range(1,32) if r!=29)
    w=[_ins(9,29,29,-0x2B0)]+[_ins(0x2B,29,r,0x230+i*4) for i,r in enumerate(registers)]
    w += [0x4010,_ins(0x2B,29,8,0x2A8),0x4012,_ins(0x2B,29,8,0x2AC)]
    if role=="main":w += [_move(23,30),_ins(9,0,30,1)]
    elif role=="tick":w += [_move(21,18),_move(18,16)]
    else:w += [_ins(0x23,29,to,0x230+registers.index(source)*4) for to,source in ((22,21),(21,23),(23,22))]
    return _words(w+[_jump(target),0])


CODE_WINDOWS=(
    (0x8007DB34,bytes.fromhex("ea2d020800000000")),
    (0x8007AC9C,_words([0x8008B528])),(0x8007AD84,_words([0x8008B5C4])),
    (0x8007ADE0,_words([0x8008B660])),(0x8007AEC8,_words([0x8008B700])),
    (0x8008B3F0,_adapter("main",0x8008A254)),(0x8008B48C,_adapter("main",0x8008A3F4)),
    (0x8008B528,_adapter("tick",0x8008ADEC)),(0x8008B5C4,_adapter("tick",0x8008AE84)),
    (0x8008B660,_adapter("spawn",0x8008AF80)),(0x8008B700,_adapter("spawn",0x8008B0A8)),
    (0x8008B7A8,_words([_ins(9,0,2,0x26),_ins(5,4,2,3),0,_jump(0x8008B3F0),0,
                       _ins(9,0,2,0x11B),_ins(5,4,2,3),0,_jump(0x8008B48C),0,
                       0x28820101,_ins(5,2,0,3),0x288200FF,_jump(0x8007DC70),0,_jump(0x8007DB40),0])),
    (0x8005A898,bytes.fromhex("0780013c6c59228c0880013cbfff4224040040140980023c0980023c0200001038ba4224cc9e4224d88522ac0780013c0880023ca4724224cc5822ac0880023c54da4224345722ac0980023c688f4224e45822ac0880023c207e4224bc5622ac0880023c2caf4224a05722ac0880023c68b04224a85822ac0880023cacb042244c5722ac0880023c4cd04224d85822acb86d010800000000")),
)


def _fields(client,names):
    result={}
    for name in names:
        at,fmt=FIELDS[name];value=struct.unpack(fmt,client.read(at,struct.calcsize(fmt)))
        result[name]=value[0] if len(value)==1 else list(value)
    return result


def _grid(value,start,end,stride):return start<=value<=end and (value-start)%stride==0


def _require_baseline(v):
    for name,(_,expected) in GATES.items():
        if v[name]!=expected:raise ValueError("Wrong/uninitialized P1 gate: "+name)
    if not 1<=v["dynamic_count"]<=32:raise ValueError("Initialized P1 count must be1..32, not a peak proof.")
    for name,start,end,stride in (("properties_cursor",PRIVATE,END,24),("next_free_row",BASE,RESERVE,88),
                                  ("companion_handle",BASE,RESERVE-88,88),("next_free_particle",POOL,PETE,32)):
        if not _grid(v[name],start,end,stride):raise ValueError("Out-of-owner or unaligned cursor: "+name)


def capture(client,label):
    """Neutral fixed-RAM reader. Cannot issue external session authentication."""
    if type(label) is not str:raise ValueError("Sample label must be text.")
    result=dict(utc=datetime.now(timezone.utc).isoformat(),label=label[:120],expected_profile_annotation=PROFILE,
                expected_candidate_sha256_annotation=EXPECTED_IMAGE_SHA256,candidate_authenticated=False,
                external_session_binding_checked=False,sequential_reads_not_atomic=True,runtime_pass=False,
                ui_result_approved=False,normal_export_approved=False,payload_valid=False,gate_reread_consistent=False)
    try:
        values=_fields(client,FIELDS);result.update(values);_require_baseline(values)
        witnesses=[]
        for at,expected in CODE_WINDOWS:
            actual=client.read(at,len(expected))
            if actual!=expected:raise ValueError(f"P1 code witness changed at{at:08X}")
            witnesses.append((at,actual))
        raw=client.read(PLACED,88);actor=decode_moby(raw,0,PLACED);witnesses.append((PLACED,raw))
        if actor["class_id"]!=0x11B or actor["properties"]!=WPRIVATE:raise ValueError("Placed Wizard is raw/stale or has the wrong private owner.")
        private=client.read(WPRIVATE,80);witnesses.append((WPRIVATE,private))
        if struct.unpack_from('<I',private)[0]!=WPRIVATE+0x28:raise ValueError("Placed nested private link is unrelocated or changed.")
        handle=values["companion_handle"];companion_raw=client.read(handle,88)
        companion=decode_moby(companion_raw,1+(handle-BASE)//88,handle);witnesses.append((handle,companion_raw))
        if companion["class_id"]!=0x78 or companion_raw[0x48]>=0x80:raise ValueError("Sparx handle is absent/inactive or stale.")
        active=[];terminal=None
        for i in range(32):
            at=BASE+i*88
            # Reuse the prechecked exact companion snapshot, then reread it at
            # the final barrier; no early duplicate read mistaken for that barrier.
            b=companion_raw if at==handle else client.read(at,88)
            if b[0x48]==0xFF:
                terminal=i;witnesses.append((at,b));break
            if b[0x48]<0x80:
                item=decode_moby(b,1+i,at);p=item["properties"]
                item["private_pointer_classification"]="owned-grid" if _grid(p,PRIVATE,END-24,24) else "outside-owned-private-grid"
                item["raw_56_u8"]=b[0x56];active.append(item)
        if not any(item['address']==handle for item in active):raise ValueError('Companion is beyond the native dynamic terminal.')
        reserve=None
        if terminal is None:
            b=client.read(RESERVE+0x48,1);reserve=b[0];witnesses.append((RESERVE+0x48,b))
        private_block=client.read(PRIVATE,0x300) # Fixed owner, never a row-supplied address.
        pool=b''.join(client.read(POOL+i,1024) for i in range(0,0x2000,1024))
        counts=Counter();samples=[];particle_terminal=None;particle_count=0
        for i in range(256):
            b=pool[i*32:(i+1)*32]
            if b[1]==0xFF:particle_terminal=i;break
            if b[1]>=0x80:continue
            particle_count+=1;counts[f"{b[0]:02X}/kind{b[1]}"]+=1
            if b[0] in (7,0x41,0,0xC,0x42,0x46,0x47) and len(samples)<16:
                samples.append(dict(slot=i,selector=b[0],kind=b[1],age=b[2],visibility=b[3],raw_hex=b.hex()))
        pete=client.read(PETE,4);witnesses.append((PETE,pete))
        after=_fields(client,REREAD)
        if any(after[n]!=values[n] for n in REREAD):raise ValueError("Gate/cursor/renderer/diagnostic changed during sequential reads.")
        for at,b in witnesses:
            if client.read(at,len(b))!=b:raise ValueError(f"Code/placed/private/companion/terminal witness changed at{at:08X}")
        slack,reason=primitive_slack(values['primitive_cursor'],values['primitive_limit'])
        result.update(payload_valid=True,gate_reread_consistent=True,wizard=actor,wizard_private_raw_hex=private.hex(),
            companion=companion,dynamic_rows=dict(active=active,terminal_slot=terminal,no_in_range_terminal=terminal is None),
            reserve_terminal_u8_not_actor33=reserve,raw_dynamic_private_block_hex=private_block.hex(),
            particles=dict(active_count=particle_count,terminal_slot=particle_terminal,selector_kind_counts=dict(counts),samples=samples),
            particle_terminal_word_not_record257=pete.hex(),primitive_sampled_slack=slack,
            primitive_sampled_slack_unavailable_reason=reason,authored_floor_xy_bounds_annotation=[8448,8448,9472,9472],
            expected_spawn_xyz_annotation=[8960,8960,9216],expected_heading_u8_annotation=64,expected_music_track_id_annotation=19)
        attention=[]
        if values['dynamic_count']==32 or terminal is None:attention.append('Pause in UI: nominal actor capacity/terminal needs inspection; no safe spawn guard or peak proof.')
        if any(x['private_pointer_classification']!='owned-grid' for x in active):attention.append('Opaque dynamic property pointer is outside the allocator grid; no dereference or class-specific conclusion.')
        if len({x['properties'] for x in active})!=len(active):attention.append('Duplicate dynamic property pointer observation; ownership/lifetime needs inspection.')
        if any(x['class_id'] not in (0xE,0xF,0x10,0x26,0x56,0x78,0x104,0x105) for x in active):attention.append('Unclosed dynamic class observed; no new family acceptance.')
        if particle_terminal is None or pete!=b'\xff'*4 or reserve not in (None,255):attention.append('Pause in UI: full pool or separate terminal owner needs inspection, not a257th/33rd record.')
        x,y,_=values['player_xyz'];inside=8448<=x<=9472 and 8448<=y<=9472
        result['sampled_player_xy_inside_authored_floor']=inside
        if not inside:attention.append('Pause in UI: outside authored floor; not a collision-bug diagnosis.')
        if values['renderer_exhaustion_flag'] or slack is None or slack<0:attention.append('Pause in UI: sampled renderer budget requires inspection; not a peak measurement.')
        if attention:result['attention']=attention
    except (ValueError,struct.error) as error:
        result['payload_refused']=str(error);result['attention']=['Pause in UI: no accepted layout/gameplay sample.']
    return result


@dataclass(frozen=True)
class FileIdentity:
    """Neutral expected OS identity, not an issued candidate/verified hash."""
    path:str
    device:int
    inode:int
    size:int


@dataclass(frozen=True)
class SessionObservation:
    pid:int
    uid:int
    start_time:str
    executable:str
    image:FileIdentity
    bin_fd:int
    auxiliary_cache:tuple|None=None
    auxiliary_cache_size:int|None=field(default=None,compare=False)
    @property
    def candidate_authenticated(self):return False


def _text(raw):
    if type(raw) is not bytes or not 0<len(raw)<=65536:raise ValueError('Missing/overlong OS output.')
    try:return raw.decode('utf-8','strict')
    except UnicodeError as e:raise ValueError('Invalid OS text.') from e


def parse_process(raw,pid,uid,executable):
    if type(pid) is not int or not 0<pid<=0x7FFFFFFF or type(uid) is not int or uid<0:raise ValueError('Invalid nominated PID/UID.')
    text=_text(raw)
    match=re.fullmatch(r' *(\w{3} \w{3} [ 0-3][0-9] [0-2][0-9]:[0-5][0-9]:[0-5][0-9] [0-9]{4}) +([0-9]{1,10}) +(/[^\r\n\x00]+)\n',text)
    if not match:raise ValueError('Incomplete/ambiguous ps lstart/uid/comm.')
    start,owner,path=match.groups()
    try:parsed=datetime.strptime(start,'%a %b %d %H:%M:%S %Y')
    except ValueError as e:raise ValueError('Invalid process start timestamp.') from e
    canonical=parsed.strftime('%a %b ')+f'{parsed.day:2d}'+parsed.strftime(' %H:%M:%S %Y')
    if canonical!=start or int(owner)!=uid or path!=executable:raise ValueError('Wrong process start/UID/executable owner.')
    return start


def parse_lsof(raw):
    text=_text(raw)
    if not text.endswith('\x00\n'):raise ValueError('Truncated NUL lsof output.')
    records=[];pid=None;seen_pids=set()
    for line in text.splitlines():
        if not line.endswith('\x00'):raise ValueError('Missing field terminator.')
        fields={}
        for item in line[:-1].split('\x00'):
            if (len(item)<2 and item!='n') or item[0] not in 'pcftaiDnsPT' or item[0] in fields:raise ValueError('Unknown/empty/duplicate lsof field.')
            fields[item[0]]=item[1:]
        if 'p' in fields:
            if set(fields)-{'p','c'} or not re.fullmatch('[1-9][0-9]{0,9}',fields['p']):raise ValueError('Invalid process record.')
            pid=int(fields['p'])
            if pid in seen_pids:raise ValueError('Repeated process record.')
            seen_pids.add(pid)
        else:
            if pid is None or 'f' not in fields or not re.fullmatch('[0-9]{1,6}',fields['f']):raise ValueError('Unowned/invalid descriptor record.')
            if int(fields['f'])>4096:raise ValueError('Descriptor exceeds bounded snapshot policy.')
            if fields.get('t')=='NPOLICY':
                if set(fields)!={'f','a','t','n'} or fields['a']!=' ' or fields['n']!='':
                    raise ValueError('Only the exact recorded empty-name NPOLICY grammar is supported.')
            elif fields.get('n')=='':raise ValueError('Empty names are not general descriptor permission.')
            records.append(dict(fields,p=str(pid)))
    if not records:raise ValueError('No complete descriptor records.')
    if len({(r['p'],r['f']) for r in records})!=len(records):raise ValueError('Duplicate descriptor owner.')
    return tuple(records)


def _run(argv):
    """Read-only bounded child query; no shell, input, retry or control action.

    Tests inject a runner; live CLI activation remains a separate review.
    Combined stdout/stderr must be a complete successful record.
    """
    if type(argv) is not tuple or any(type(a) is not str for a in argv):raise ValueError('Unapproved process query.')
    ps=(len(argv)==9 and argv[:2]==('/bin/ps','-p') and re.fullmatch('[1-9][0-9]{0,9}',argv[2]) and
        argv[3:]==('-o','lstart=','-o','uid=','-o','comm='))
    files=(len(argv)==9 and argv[:4]==('/usr/sbin/lsof','-nP','-a','-p') and re.fullmatch('[1-9][0-9]{0,9}',argv[4]) and
           argv[5:]==('-d','0-4096','-F0pcftaiDnsPT','-Ts'))
    listener=argv==('/usr/sbin/lsof','-nP','-iTCP:2345','-sTCP:LISTEN','-F0pcftaiDnsPT','-Ts')
    additional=(len(argv)==9 and argv[:4]==('/usr/sbin/lsof','-nP','-a','-p') and re.fullmatch('[1-9][0-9]{0,9}',argv[4]) and
        argv[5:] in (('-d','txt','-F0pcftaiDnsPT','-Ts'),('-iTCP:2345','-sTCP:ESTABLISHED','-F0pcftaiDnsPT','-Ts')))
    if not (ps or files or listener or additional):raise ValueError('Only five exact read-only ownership queries are accepted.')
    role='process' if ps else 'numeric-fds' if files else 'listener' if listener else 'text-owner' if argv[5]=='-d' else 'established-socket'
    prefix=f'OS query [{role}]'
    deadline=time.monotonic()+2
    try:
        p=subprocess.Popen(argv,stdin=subprocess.DEVNULL,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,
                           env=dict(os.environ,LC_ALL='C'),shell=False)
    except OSError as e:raise ValueError(f'{prefix} unavailable.') from e
    try:
        output=bytearray()
        with selectors.DefaultSelector() as selector:
            selector.register(p.stdout,selectors.EVENT_READ)
            while True:
                remaining=deadline-time.monotonic()
                if remaining<=0 or not selector.select(remaining):raise ValueError(f'{prefix} timed out.')
                b=os.read(p.stdout.fileno(),min(4096,65537-len(output)))
                if not b:break
                output.extend(b)
                if len(output)>65536:raise ValueError(f'{prefix} exceeded output bound.')
        remaining=deadline-time.monotonic()
        if remaining<=0:raise ValueError(f'{prefix} timed out.')
        code=p.wait(timeout=remaining)
        if code!=0:raise ValueError(f'{prefix} failed (exit code {code}).')
        return bytes(output)
    except (OSError,subprocess.SubprocessError) as e:raise ValueError(f'{prefix} unavailable.') from e
    finally:
        if p.poll() is None:p.kill();p.wait(timeout=2)
        p.stdout.close()


def inspect_session(pid,uid,executable,image,*,runner=_run):
    """Neutral independently observed fields; no hash/file or activation authority.

    The closed file/session layer supplies actual file identity independently;
    this neutral parser alone grants no connection or artifact authority.
    """
    if type(image) is not FileIdentity or type(image.path) is not str or not image.path.startswith('/') or '\x00' in image.path or \
       any(type(x) is not int or x<=0 for x in (image.device,image.inode,image.size)) or \
       image.size!=661547040 or not image.path.endswith('/'+CANDIDATE_BASENAME+'.bin'):
        raise ValueError('Wrong fixed candidate OS identity shape.')
    if type(pid) is not int or not 0<pid<=0x7FFFFFFF or type(uid) is not int or uid<0 or \
       type(executable) is not str or not executable.startswith('/') or any(c in executable for c in '\r\n\x00'):
        raise ValueError('Wrong nominated process shape.')
    start=parse_process(runner(('/bin/ps','-p',str(pid),'-o','lstart=','-o','uid=','-o','comm=')),pid,uid,executable)
    files=parse_lsof(runner(('/usr/sbin/lsof','-nP','-a','-p',str(pid),'-d','0-4096','-F0pcftaiDnsPT','-Ts')))
    if any(int(r['p'])!=pid for r in files):raise ValueError('File snapshot belongs to another PID.')
    caches=[r for r in files if r.get('n')==SHADER_CACHE_PATH]
    if len(caches)>1:raise ValueError('Ambiguous fixed shader-cache auxiliary owner.')
    cache_identity=None;cache_size=None
    if caches:
        r=caches[0]
        try:
            if not re.fullmatch('[0-9]+',r['s']):raise ValueError('Invalid mutable cache size.')
            device,inode,cache_size=int(r['D'],16),int(r['i']),int(r['s'])
        except (KeyError,ValueError) as e:raise ValueError('Incomplete fixed cache metadata.') from e
        if (r.get('t'),r.get('a'),device,inode)!=('REG','u',0x1000011,1242218):
            raise ValueError('The extra BIN is not the one reviewed auxiliary shader-cache owner.')
        cache_identity=(int(r['f']),device,inode,SHADER_CACHE_PATH)
    images=[r for r in files if r.get('n','').lower().endswith(('.bin','.cue','.iso','.chd')) and r not in caches]
    bins=[r for r in images if r.get('n')==image.path]
    if len(bins)!=1 or any(r.get('n') not in (image.path,image.path[:-4]+'.cue') for r in images):raise ValueError('Missing/ambiguous or additional open image.')
    b=bins[0]
    try:identity=(int(b['D'],16),int(b['i']),int(b['s']))
    except (KeyError,ValueError) as e:raise ValueError('Incomplete BIN FD identity.') from e
    if b.get('a')!='r' or b.get('t')!='REG' or identity!=(image.device,image.inode,image.size):raise ValueError('BIN is not the exact read-open regular owner.')
    listeners=parse_lsof(runner(('/usr/sbin/lsof','-nP','-iTCP:2345','-sTCP:LISTEN','-F0pcftaiDnsPT','-Ts')))
    if len(listeners)!=1:raise ValueError('Ambiguous TCP2345 listener.')
    r=listeners[0]
    if (r['p'],r.get('t'),r.get('P'),r.get('n'),r.get('T'))!=(str(pid),'IPv4','TCP','127.0.0.1:2345','ST=LISTEN'):
        raise ValueError('Listener is not this process and exact loopback endpoint.')
    return SessionObservation(pid,uid,start,executable,image,int(b['f']),cache_identity,cache_size)


def require_same_session(before,after):
    if type(before) is not SessionObservation or type(after) is not SessionObservation or before!=after:
        raise ValueError('Process/start/open BIN identity changed across sequential checks.')


def require_connected_owner(raw,session,local,remote):
    if type(session) is not SessionObservation or type(local) is not tuple or len(local)!=2 or \
       local[0]!='127.0.0.1' or type(local[1]) is not int or not 0<local[1]<65536 or remote!=('127.0.0.1',2345):
        raise ValueError('Unexpected local/remote socket tuple.')
    records=parse_lsof(raw);expected=f'127.0.0.1:2345->127.0.0.1:{local[1]}'
    if len(records)!=1 or (records[0]['p'],records[0].get('t'),records[0].get('P'),records[0].get('n'),records[0].get('T'))!=(str(session.pid),'IPv4','TCP',expected,'ST=ESTABLISHED'):
        raise ValueError('Established server socket does not belong to the identified process/tuple.')


# Root activation after peer review, actual six-file gate and real OS prechecks.
# Evidence: fresh-p1-observer-phase2-r01.mBNeUE/08-root-* and09-root-*.
PHASE2_READY=True
CANDIDATE_DIRECTORY='/Users/john/Documents/Codex/2026-07-13/i-needed-to-make-a-new/spyro1editor-v3-release/_local/research/dark-wizard-native-closure-20260906/fresh-wizard-publication-validation-r01.0eF4Tz/candidate-r01'
ORIGINAL_STEM='/Users/john/Documents/Codex/2026-04-24/could-you-create-a-level-editor/Spyro the Dragon (USA)'
EMULATOR_PATH='/Applications/DuckStation.app/Contents/MacOS/DuckStation'
SHADER_CACHE_PATH='/Users/john/Library/Application Support/DuckStation/cache/vulkan_shaders.bin'


@dataclass(frozen=True)
class FilePin:
    """Neutral byte expectation; only the closed live factory selects pins."""
    path:str
    size:int
    sha256:str


_FILE_PINS=(
    ('original_bin',FilePin(ORIGINAL_STEM+'.bin',661547040,'fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37')),
    ('original_cue',FilePin(ORIGINAL_STEM+'.cue',88,'05142502c75d3a0b6374003a8fe41893d6faf0ad55863ff0b62a265a1b152766')),
    ('candidate_bin',FilePin(CANDIDATE_DIRECTORY+'/'+CANDIDATE_BASENAME+'.bin',661547040,EXPECTED_IMAGE_SHA256)),
    ('candidate_cue',FilePin(CANDIDATE_DIRECTORY+'/'+CANDIDATE_BASENAME+'.cue',98,'78c40516cbfe54286b839453d8ccf5786ac685d23a69623e6e51d82106238a6e')),
    ('receipt',FilePin(CANDIDATE_DIRECTORY+'/'+CANDIDATE_BASENAME+'.receipt.json',4877,'af41aa8b0af10504c626f76cbaa2446e2207486551ce6ba8766794261a63b63e')),
    ('emulator',FilePin(EMULATOR_PATH,22731840,'34c62fd34b2a18670007c9383cea0b0222b2ab241f643bdbc5d1620a298ac870')),
)
_STAT_FIELDS=('st_dev','st_ino','st_mode','st_size','st_mtime_ns','st_ctime_ns')
_HELPER_PINS={'Observe-SpyroNativeWizard.py':'7a07ccdbc1288f65c8f8627d9623c3eb8b6cddb97b5b36e06ebe84d9a7d80516',
              'Watch-SpyroId65RuntimeState.py':'eac3953a0ff7ff8acdefbe43cf987f296d44091ad51619efd225690ba1dc0677'}


def _stat_tuple(value):return tuple(getattr(value,k) for k in _STAT_FIELDS)


def _parents(path):
    result=[]
    for p in Path(path).parents:
        if len(result)>=64:raise ValueError('Fixed file ancestor depth exceeds bound.')
        s=os.lstat(p)
        if not stat.S_ISDIR(s.st_mode):raise ValueError('Linked or non-directory file ancestor.')
        result.append((str(p),s.st_dev,s.st_ino,s.st_mode))
    return tuple(result)


class _VerifiedFile:
    def __init__(self,pin,fd,before,parents):
        self.pin,self.fd,self.before,self.parents=pin,fd,_stat_tuple(before),parents
        self.identity=FileIdentity(pin.path,before.st_dev,before.st_ino,before.st_size)

    def check(self,full=False):
        if self.fd is None:raise ValueError('Verified owner is closed.')
        try:
            if _stat_tuple(os.fstat(self.fd))!=self.before or _stat_tuple(os.lstat(self.pin.path))!=self.before or _parents(self.pin.path)!=self.parents:
                raise ValueError('Held descriptor/path/ancestor identity changed.')
            if full:
                if os.lseek(self.fd,0,os.SEEK_SET)!=0:raise ValueError('Owner failed seek-to-zero.')
                remaining=self.pin.size;digest=hashlib.sha256()
                while remaining:
                    n=min(1048576,remaining);b=os.read(self.fd,n)
                    if type(b) is not bytes or not 0<len(b)<=n:raise ValueError('Owner returned partial EOF or overreported bytes.')
                    digest.update(b);remaining-=len(b)
                if os.lseek(self.fd,0,os.SEEK_CUR)!=self.pin.size or os.read(self.fd,1)!=b'' or os.lseek(self.fd,0,os.SEEK_CUR)!=self.pin.size:
                    raise ValueError('Owner true EOF/position changed.')
                if digest.hexdigest()!=self.pin.sha256:raise ValueError('Complete pinned file hash changed.')
                if _stat_tuple(os.fstat(self.fd))!=self.before or _stat_tuple(os.lstat(self.pin.path))!=self.before or _parents(self.pin.path)!=self.parents:
                    raise ValueError('File metadata changed during complete hash.')
        except OSError as e:raise ValueError('Read-only owner verification failed.') from e

    def close(self):
        if self.fd is not None:
            fd,self.fd=self.fd,None
            os.close(fd)

    def report(self):
        return dict(path=self.pin.path,length=self.pin.size,sha256=self.pin.sha256,
                    device=self.identity.device,inode=self.identity.inode,
                    initial_metadata=dict(zip(_STAT_FIELDS,self.before)),hash_is_complete=True)


def _raise_failures(errors):
    if len(errors)==1:raise errors[0]
    if errors:raise BaseExceptionGroup('Observation and final owner checks failed; all causes retained.',errors)


def _finalize_files(owners,primary=None):
    """Every full final check, then every close; never mask a prior failure."""
    errors=[] if primary is None else [primary]
    owners=tuple(owners)
    for owner in owners:
        try:owner.check(full=True)
        except BaseException as e:errors.append(e)
    for owner in owners:
        try:owner.close()
        except BaseException as e:errors.append(e)
    _raise_failures(errors)


def _open_verified_file(pin):
    if type(pin) is not FilePin or type(pin.path) is not str or not os.path.isabs(pin.path) or os.path.normpath(pin.path)!=pin.path or \
       any(c in pin.path for c in '\r\n\x00') or type(pin.size) is not int or not 0<pin.size<=661547040 or \
       type(pin.sha256) is not str or not re.fullmatch('[0-9a-f]{64}',pin.sha256):
        raise ValueError('Invalid neutral fixed-file pin shape.')
    if any(not hasattr(os,key) for key in ('O_NOFOLLOW','O_CLOEXEC','O_NONBLOCK')):raise ValueError('Required no-follow/nonblocking capabilities unavailable.')
    fd=None
    try:
        parents=_parents(pin.path);before=os.lstat(pin.path)
        if not stat.S_ISREG(before.st_mode) or before.st_size!=pin.size:raise ValueError('Pinned owner is not an exact regular file.')
        fd=os.open(pin.path,os.O_RDONLY|os.O_NOFOLLOW|os.O_CLOEXEC|os.O_NONBLOCK)
        if _stat_tuple(os.fstat(fd))!=_stat_tuple(before):raise ValueError('Owner changed across read-only open.')
        owner=_VerifiedFile(pin,fd,before,parents);owner.check(full=True)
        return owner
    except BaseException as e:
        primary=ValueError('Read-only fixed owner unavailable.') if isinstance(e,OSError) else e
        if primary is not e:primary.__cause__=e
        errors=[primary]
        if fd is not None:
            try:os.close(fd)
            except BaseException as close_error:errors.append(close_error)
        _raise_failures(errors)


def _read_at(owner,offset,length):
    owner.check()
    if os.lseek(owner.fd,offset,os.SEEK_SET)!=offset:raise ValueError('Bounded file seek failed.')
    result=bytearray()
    while len(result)<length:
        n=length-len(result);b=os.read(owner.fd,n)
        if type(b) is not bytes or not 0<len(b)<=n:raise ValueError('Bounded file read escaped exact owner.')
        result.extend(b)
    if os.lseek(owner.fd,0,os.SEEK_CUR)!=offset+length:raise ValueError('Bounded owner position changed.')
    owner.check()
    return bytes(result)


def _require_tree():
    _parents(CANDIDATE_DIRECTORY+'/'+CANDIDATE_BASENAME+'.bin')
    expected={Path(pin.path).name for role,pin in _FILE_PINS if role in ('candidate_bin','candidate_cue','receipt')}
    with os.scandir(CANDIDATE_DIRECTORY) as entries:
        found=[]
        for entry in entries:
            if len(found)>=3 or not entry.is_file(follow_symlinks=False):raise ValueError('Unexpected candidate artifact owner.')
            found.append(entry.name)
    if set(found)!=expected or len(found)!=3:raise ValueError('Candidate is not the exact three-owner set.')


def _check_files(owners):
    _require_tree()
    for owner in owners.values():owner.check(full=owner.pin.size<=65536)


def _unique_json(pairs):
    result={}
    for key,value in pairs:
        if key in result:raise ValueError('Duplicate receipt key.')
        result[key]=value
    return result


def _open_profile_files():
    owners={}
    try:
        _require_tree()
        for role,pin in _FILE_PINS:owners[role]=_open_verified_file(pin)
        cue=f'FILE "{CANDIDATE_BASENAME}.bin" BINARY\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\n'.encode()
        if _read_at(owners['candidate_cue'],0,98)!=cue:raise ValueError('Code-owned CUE bytes changed.')
        receipt=json.loads(_read_at(owners['receipt'],0,4877).decode('utf-8','strict'),object_pairs_hook=_unique_json,
                           parse_constant=lambda _:(_ for _ in ()).throw(ValueError('Nonfinite receipt value.')))
        required={'schema':1,'profile':'native-id65-fresh-wizard-primary-p1-n32-research-r01',
            'planSha256':'3278bedaf4d24a564bfc6b3fc4554a959bf520bc51608bd53b73c8a112a1ac8b',
            'sourceImagePath':ORIGINAL_STEM+'.bin','sourceCuePath':ORIGINAL_STEM+'.cue',
            'sourceImageSha256':_FILE_PINS[0][1].sha256,'sourceCueSha256':_FILE_PINS[1][1].sha256,
            'imageSha256':EXPECTED_IMAGE_SHA256,'imageByteLength':661547040,'changedRawSectors':1290,
            'cueSha256':_FILE_PINS[3][1].sha256,'executableLba':54958,'executableEndExclusive':55162,
            'canonicalDocumentSha256':'59ab0b1bfb8bb62a51d34d7d7b3ab0387c51ad9e5abf24769558ed88b8e73614'}
        for key,value in required.items():
            if type(receipt.get(key)) is not type(value) or receipt[key]!=value:raise ValueError('Wrong pinned receipt description: '+key)
        for key in ('projectSaveBindingClaimed','runtimeVerified','normalCreateBinApproved','persistentOwnerIssued',
                    'cardOrHistoryCompatibilityProven','wholePickupOrDeathProven','globalAudioCompatibilityProven',
                    'peakRuntimeMemoryProven','hardwareTransfersProven','completeIncomingEdgeClosureProven',
                    'completeNativeDependencyClosureProven','checkpointOrForeignStateSupported'):
            if receipt.get(key) is not False:raise ValueError('Receipt limitations changed.')
        for role,lba,expected in (('original_bin',53875,'a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998'),
                                  ('candidate_bin',54958,'a0eb46c2735355c05d66cc33492ed70c6c2174e77e8e8eb6c2ca8fae0f3fc8dc')):
            digest=hashlib.sha256()
            for index in range(204):digest.update(_read_at(owners[role],(lba+index)*2352,2352)[24:2072])
            if digest.hexdigest()!=expected:raise ValueError('Complete logical game EXE readback differs.')
        _check_files(owners)
        return owners
    except BaseException as e:_finalize_files(owners.values(),e)


def _source_snapshot():
    result={}
    for name in (Path(__file__).name,*_HELPER_PINS):
        path=Path(__file__).with_name(name);before=path.stat()
        with path.open('rb') as source:
            b=source.read(65537)
            if len(b)>65536 or len(b)!=before.st_size or source.read(1)!=b'' or source.tell()!=len(b):
                raise ValueError('Observer/helper source exceeds bounded complete owner.')
            if _stat_tuple(os.fstat(source.fileno()))!=_stat_tuple(before) or _stat_tuple(path.stat())!=_stat_tuple(before):
                raise ValueError('Observer/helper source changed during read.')
        result[name]=hashlib.sha256(b).hexdigest()
        if name in _HELPER_PINS and result[name]!=_HELPER_PINS[name]:raise ValueError('Reviewed m-only helper source changed.')
    return result


def _finish_profile(owners,primary,source_before):
    errors=[]
    try:_finalize_files(owners.values(),primary)
    except BaseException as e:errors.append(e)
    if owners:
        try:_require_tree()
        except BaseException as e:errors.append(e)
    if source_before is not None:
        try:
            if _source_snapshot()!=source_before:raise ValueError('Observer source changed during observation.')
        except BaseException as e:errors.append(e)
    _raise_failures(errors)


def verify_actual_files():
    """Explicit read-only gate; no PID query/socket and no caller pins."""
    owners={};before=None;primary=None;result=None
    try:
        before=_source_snapshot();owners=_open_profile_files()
        result=dict(owners={name:owner.report() for name,owner in owners.items()},source_hashes=before,
                    final_full_checks_passed=True,candidate_authenticated=False,runtime_pass=False)
    except BaseException as e:primary=e
    _finish_profile(owners,primary,before)
    return result


def require_text_owner(raw,pid,expected):
    """Separate mapped-text grammar. Other REG mappings are not authenticated."""
    if type(pid) is not int or not 0<pid<=0x7FFFFFFF or type(expected) is not FileIdentity or \
       type(expected.path) is not str or not expected.path.startswith('/') or any(c in expected.path for c in '\r\n\x00') or \
       any(type(v) is not int or v<=0 for v in (expected.device,expected.inode,expected.size)):raise ValueError('Invalid neutral text-owner shape.')
    text=_text(raw)
    if not text.endswith('\x00\n'):raise ValueError('Incomplete mapped-text output.')
    records=[];seen_header=False;seen=set()
    for line in text.splitlines():
        if not line.endswith('\x00'):raise ValueError('Incomplete mapped-text fields.')
        values={}
        for item in line[:-1].split('\x00'):
            if len(item)<2 or item[0] not in 'pcfatDisn' or item[0] in values:raise ValueError('Unknown/empty/duplicate text field.')
            values[item[0]]=item[1:]
        if 'p' in values:
            if seen_header or set(values)-{'p','c'} or values['p']!=str(pid):raise ValueError('Wrong/repeated text process header.')
            seen_header=True;continue
        if not seen_header or set(values)-{'f','a','t','D','i','s','n'} or not {'f','t','D','i','s','n'}<=set(values) or \
           values['f']!='txt' or values['t']!='REG' or not values['n'].startswith('/') or \
           not re.fullmatch('[0-9]+',values['i']) or not re.fullmatch('[0-9]+',values['s']):
            raise ValueError('Incomplete/nonregular mapped-text record.')
        try:identity=(int(values['D'],16),int(values['i']),int(values['s']),values['n'])
        except ValueError as e:raise ValueError('Invalid mapped-text device.') from e
        if identity[0]<=0 or identity[1]<=0 or identity[2]<0 or identity in seen:raise ValueError('Duplicate/invalid mapped-text owner.')
        seen.add(identity);records.append(identity)
    wanted=(expected.device,expected.inode,expected.size,expected.path)
    if len([r for r in records if r[3]==expected.path])!=1 or wanted not in records:raise ValueError('Actual mapped executable owner differs.')
    return expected,len(records)-1


@dataclass(frozen=True)
class _BoundSession:
    process:SessionObservation
    executable:FileIdentity
    listener_fd:int
    server_fd:int|None
    local:tuple|None
    remote:tuple|None
    other_text_count:int=field(compare=False)


def _external_check(pid,owners,connection=None):
    outputs=[]
    def query(argv):
        b=_run(argv);outputs.append(b);return b
    exe=owners['emulator'].identity
    observed=inspect_session(pid,os.getuid(),exe.path,owners['candidate_bin'].identity,runner=query)
    listener_fd=int(parse_lsof(outputs[2])[0]['f'])
    _,other=require_text_owner(_run(('/usr/sbin/lsof','-nP','-a','-p',str(pid),'-d','txt','-F0pcftaiDnsPT','-Ts')),pid,exe)
    local=remote=server_fd=None
    if connection is not None:
        local,remote=connection.getsockname(),connection.getpeername()
        raw=_run(('/usr/sbin/lsof','-nP','-a','-p',str(pid),'-iTCP:2345','-sTCP:ESTABLISHED','-F0pcftaiDnsPT','-Ts'))
        require_connected_owner(raw,observed,local,remote);server_fd=int(parse_lsof(raw)[0]['f'])
    return _BoundSession(observed,exe,listener_fd,server_fd,local,remote,other)


def _same_bound(before,after):
    if type(before) is not _BoundSession or type(after) is not _BoundSession:raise ValueError('Missing closed external observation.')
    require_same_session(before.process,after.process)
    if (before.executable,before.listener_fd)!=(after.executable,after.listener_fd) or \
       (before.server_fd is not None and (before.server_fd,before.local,before.remote)!=(after.server_fd,after.local,after.remote)):
        raise ValueError('Mapped executable/listener/established identity changed.')


def _command(source):
    b=source.readline(513)
    if b==b'' or b==b'q\n':return None
    if type(b) is not bytes or len(b)>512 or not b.endswith(b'\n') or any(c<32 and c!=10 for c in b) or b.count(b'\n')!=1:
        raise ValueError('Manual input must be one bounded control-free line.')
    try:s=b[:-1].decode('utf-8','strict')
    except UnicodeError as e:raise ValueError('Manual input must be strict UTF8.') from e
    if s=='p':return ''
    if not s.startswith('p ') or len(s[2:])>120:raise ValueError('Only p [label] or q is accepted.')
    return s[2:]


def _run_session(pid,cue,input_stream,emit):
    if not PHASE2_READY:raise ValueError('Phase2 live activation is still disabled.')
    if type(pid) is not int or not 0<pid<=0x7FFFFFFF or cue!=CANDIDATE_DIRECTORY+'/'+CANDIDATE_BASENAME+'.cue':
        raise ValueError('Only the exact reviewed PID nomination and fixed CUE path are accepted.')
    owners={};connection=None;primary=None;source_before=None
    try:
        source_before=_source_snapshot();owners=_open_profile_files()
        baseline=_external_check(pid,owners)
        _check_files(owners);again=_external_check(pid,owners);_same_bound(baseline,again)
        connection=socket.create_connection(('127.0.0.1',2345),timeout=2);connection.settimeout(2)
        _check_files(owners);bound=_external_check(pid,owners,connection);_same_bound(again,bound)
        client=ReadOnlyClient(connection);probe=client.read(0x8007596C,4)
        _check_files(owners);after=_external_check(pid,owners,connection);_same_bound(bound,after);baseline=after
        emit(dict(kind='connected',source_hashes=source_before,owners={n:o.report() for n,o in owners.items()},
                  external_binding=asdict(baseline),probe_raw=probe.hex(),candidate_authenticated=False,runtime_pass=False,
                  auxiliary_cache_contents_authenticated=False,process_identity_limitation=PROCESS_IDENTITY_LIMITATION))
        ordinal=0
        while True:
            label=_command(input_stream)
            if label is None:break
            _check_files(owners);before=_external_check(pid,owners,connection);_same_bound(baseline,before)
            sample=capture(client,label)
            _check_files(owners);after=_external_check(pid,owners,connection);_same_bound(before,after);baseline=after
            sample.update(kind='sample',ordinal=ordinal,external_session_binding_checked=True,external_binding=asdict(after),
                          candidate_authenticated=False,runtime_pass=False,ui_result_approved=False,normal_export_approved=False,
                          auxiliary_cache_contents_authenticated=False)
            emit(sample);ordinal+=1
        _check_files(owners);_same_bound(baseline,_external_check(pid,owners,connection))
    except BaseException as e:primary=e
    if connection is not None:
        try:connection.close()
        except BaseException as e:
            primary=e if primary is None else BaseExceptionGroup('Observation and socket close failed.',[primary,e])
    _finish_profile(owners,primary,source_before)
    emit(dict(kind='session_closed',final_full_checks_passed=True,candidate_authenticated=False,runtime_pass=False))
    return 0


def _failure_record(error):
    result=dict(type=type(error).__name__,message=str(error))
    if isinstance(error,BaseExceptionGroup):result['causes']=[_failure_record(e) for e in error.exceptions]
    return result


def main(argv=None):
    parser=argparse.ArgumentParser(description=__doc__,allow_abbrev=False)
    parser.add_argument('--pid',type=int);parser.add_argument('--candidate-cue')
    parser.add_argument('--connect-identified-test-session',action='store_true')
    args=list(sys.argv[1:] if argv is None else argv)
    for flag in ('--pid','--candidate-cue','--connect-identified-test-session'):
        if sum(a==flag or a.startswith(flag+'=') for a in args)>1:parser.error('Repeated option refused.')
    selected=parser.parse_args(args)
    if not PHASE2_READY:parser.error('LIVE CONNECTION DISABLED: phase2 final source/file/actual OS fixture review is pending.')
    if not selected.connect_identified_test_session or selected.pid is None or selected.candidate_cue is None:
        parser.error('Explicit exact session nomination and connection consent are required.')
    print('Connecting can pause DuckStation. Disconnecting can resume DuckStation if it was running when the first client was accepted. Verify pause state in the emulator UI after closing; p <label> samples, q closes. No automatic reconnect.',file=sys.stderr,flush=True)
    try:return _run_session(selected.pid,selected.candidate_cue,sys.stdin.buffer,lambda event:print(json.dumps(event),flush=True))
    except BaseException as error:
        print(json.dumps(dict(kind='session_failed',error=_failure_record(error),final_full_checks_passed=False,
                              candidate_authenticated=False,runtime_pass=False)),flush=True)
        print('No automatic recovery. Disconnecting can resume DuckStation if it was running when the first client was accepted; verify pause state in the emulator UI.',file=sys.stderr,flush=True)
        return 130 if isinstance(error,KeyboardInterrupt) else 2


if __name__=='__main__':raise SystemExit(main())
