#!/usr/bin/env python3
"""Phase1: fixed fresh A/P1/N32 diagnostic readers and neutral OS parsers.

LIVE CONNECTION IS DISABLED pending independently reviewed published CUE,
receipt, path and DuckStation executable pins. No opt-in or caller hash enables
it. Memory/OS matches are sequential observations, never gameplay, peak, audio,
save/card or image-authentication certificates. Existing observers are unchanged.
"""
from __future__ import annotations

import argparse
from collections import Counter
from dataclasses import dataclass
from datetime import datetime, timezone
import importlib.util
import os
from pathlib import Path
import re
import selectors
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
REPORT_FIELDS={name:wizard.FIELDS[name] for name in (
    "lives","player_xyz","player_heading_u8","camera_control_angle_s16","player_desired_bearing_u32",
    "player_state","player_substate","player_state_timer","player_animation_id","last_collision_saved_sp_not_peak")}|RENDERER
FIELDS={name:(at,"<I") for name,(at,_) in GATES.items()}|CURSORS|REPORT_FIELDS
REREAD=tuple(GATES)+tuple(CURSORS)+tuple(RENDERER)


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
        if any(after[n]!=values[n] for n in REREAD):raise ValueError("Gate/cursor/renderer changed during sequential reads.")
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
            if len(item)<2 or item[0] not in 'pcftaiDnsPT' or item[0] in fields:raise ValueError('Unknown/empty/duplicate lsof field.')
            fields[item[0]]=item[1:]
        if 'p' in fields:
            if set(fields)-{'p','c'} or not re.fullmatch('[1-9][0-9]{0,9}',fields['p']):raise ValueError('Invalid process record.')
            pid=int(fields['p'])
            if pid in seen_pids:raise ValueError('Repeated process record.')
            seen_pids.add(pid)
        else:
            if pid is None or 'f' not in fields or not re.fullmatch('[0-9]{1,6}',fields['f']):raise ValueError('Unowned/invalid descriptor record.')
            if int(fields['f'])>4096:raise ValueError('Descriptor exceeds bounded snapshot policy.')
            records.append(dict(fields,p=str(pid)))
    if not records:raise ValueError('No complete descriptor records.')
    if len({(r['p'],r['f']) for r in records})!=len(records):raise ValueError('Duplicate descriptor owner.')
    return tuple(records)


def _run(argv):
    """Read-only bounded child query; no shell, input, retry or control action.

    Not called by the phase1 CLI. Tests inject a runner; activation remains a
    separate review. Combined stdout/stderr must be a complete successful record.
    """
    if type(argv) is not tuple or any(type(a) is not str for a in argv):raise ValueError('Unapproved process query.')
    ps=(len(argv)==9 and argv[:2]==('/bin/ps','-p') and re.fullmatch('[1-9][0-9]{0,9}',argv[2]) and
        argv[3:]==('-o','lstart=','-o','uid=','-o','comm='))
    files=(len(argv)==9 and argv[:4]==('/usr/sbin/lsof','-nP','-a','-p') and re.fullmatch('[1-9][0-9]{0,9}',argv[4]) and
           argv[5:]==('-d','0-4096','-F0pcftaiDnsPT','-Ts'))
    listener=argv==('/usr/sbin/lsof','-nP','-iTCP:2345','-sTCP:LISTEN','-F0pcftaiDnsPT','-Ts')
    if not (ps or files or listener):raise ValueError('Only three exact read-only ownership queries are accepted.')
    deadline=time.monotonic()+2
    try:
        p=subprocess.Popen(argv,stdin=subprocess.DEVNULL,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,
                           env=dict(os.environ,LC_ALL='C'),shell=False)
    except OSError as e:raise ValueError('OS ownership query unavailable.') from e
    try:
        output=bytearray()
        with selectors.DefaultSelector() as selector:
            selector.register(p.stdout,selectors.EVENT_READ)
            while True:
                remaining=deadline-time.monotonic()
                if remaining<=0 or not selector.select(remaining):raise ValueError('OS query timed out.')
                b=os.read(p.stdout.fileno(),min(4096,65537-len(output)))
                if not b:break
                output.extend(b)
                if len(output)>65536:raise ValueError('OS query exceeded output bound.')
        remaining=deadline-time.monotonic()
        if remaining<=0 or p.wait(timeout=remaining)!=0:raise ValueError('OS query failed.')
        return bytes(output)
    except (OSError,subprocess.SubprocessError) as e:raise ValueError('OS ownership query unavailable.') from e
    finally:
        if p.poll() is None:p.kill();p.wait(timeout=2)
        p.stdout.close()


def inspect_session(pid,uid,executable,image,*,runner=_run):
    """Neutral independently observed fields; no hash/file or activation authority.

    Actual artifact/executable hashing and before/after connection orchestration
    remain DISABLED pending code-owned reviewed pins. Caller labels cannot enable.
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
    images=[r for r in files if r.get('n','').lower().endswith(('.bin','.cue','.iso','.chd'))]
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
    return SessionObservation(pid,uid,start,executable,image,int(b['f']))


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


def main(argv=None):
    parser=argparse.ArgumentParser(description=__doc__,allow_abbrev=False)
    parser.add_argument('--pid',type=int);parser.add_argument('--candidate-cue')
    parser.add_argument('--connect-identified-test-session',action='store_true')
    args=list(sys.argv[1:] if argv is None else argv)
    for flag in ('--pid','--candidate-cue','--connect-identified-test-session'):
        if sum(a==flag or a.startswith(flag+'=') for a in args)>1:parser.error('Repeated option refused.')
    parser.parse_args(args)
    # Deliberately unconditional. No executable/companion pins have been issued
    # to this observer; do not turn parser fixtures or matching RAM into authority.
    parser.error('LIVE CONNECTION DISABLED: actual published companions/path and DuckStation executable pins await independent review.')


if __name__=='__main__':main()
