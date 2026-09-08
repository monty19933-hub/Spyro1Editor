#!/usr/bin/env python3
"""Closed separated B/P1/N32 diagnostics for one Root-reviewed successful publication.

No caller profile/hash, automated input, execution packet or debugger write.
The unchanged pinned neutral RAM reader supplies observations, not approval.
"""
from __future__ import annotations
import argparse
from collections import Counter
from dataclasses import asdict,dataclass
from datetime import datetime,timezone
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import re
import socket
import stat
import struct
import sys

sys.dont_write_bytecode=True
LEGACY_PINS={
    'Observe-SpyroFreshWizardP1.py':'5e16c941f3deefce1625b09b89b35af4aaee3b3b008fbd33d56f1aaa0a75f870',
    'Observe-SpyroNativeWizard.py':'7a07ccdbc1288f65c8f8627d9623c3eb8b6cddb97b5b36e06ebe84d9a7d80516',
    'Watch-SpyroId65RuntimeState.py':'eac3953a0ff7ff8acdefbe43cf987f296d44091ad51619efd225690ba1dc0677'}
def _raise(errors):
    if len(errors)==1:raise errors[0]
    if errors:raise BaseExceptionGroup('Separated observer primary/final failures.',errors)
def _metadata(s):return (s.st_dev,s.st_ino,s.st_mode,s.st_size,s.st_mtime_ns,s.st_ctime_ns)
def _source_snapshot():
    result={};errors=[]
    for name in (Path(__file__).name,*LEGACY_PINS):
        fd=None
        try:
            path=Path(__file__).with_name(name);before=path.lstat()
            if not stat.S_ISREG(before.st_mode) or not 0<before.st_size<=65536:raise ValueError('Source is not a bounded regular owner.')
            fd=os.open(path,os.O_RDONLY|os.O_NOFOLLOW|os.O_CLOEXEC|os.O_NONBLOCK)
            if _metadata(os.fstat(fd))!=_metadata(before):raise ValueError('Source changed across open.')
            data=bytearray()
            while len(data)<before.st_size:
                b=os.read(fd,min(65536,before.st_size-len(data)))
                if not b:raise ValueError('Short source read.')
                data.extend(b)
            if os.lseek(fd,0,os.SEEK_CUR)!=before.st_size or os.read(fd,1)!=b'' or os.lseek(fd,0,os.SEEK_CUR)!=before.st_size:
                raise ValueError('Source true EOF changed.')
            if _metadata(os.fstat(fd))!=_metadata(before) or _metadata(path.lstat())!=_metadata(before):raise ValueError('Source metadata changed.')
            digest=hashlib.sha256(data).hexdigest()
            if name in LEGACY_PINS and digest!=LEGACY_PINS[name]:raise ValueError('Unchanged neutral helper pin failed.')
            result[name]=digest
        except BaseException as e:errors.append(e)
        finally:
            if fd is not None:
                try:os.close(fd)
                except BaseException as e:errors.append(e)
    _raise(errors)
    return result
def _load_legacy():
    before=_source_snapshot();errors=[];module=None
    try:
        spec=importlib.util.spec_from_file_location('separated_p1_pinned_neutral_reader',Path(__file__).with_name('Observe-SpyroFreshWizardP1.py'))
        module=importlib.util.module_from_spec(spec);sys.modules[spec.name]=module;spec.loader.exec_module(module)
    except BaseException as e:errors.append(e)
    finally:
        try:
            if _source_snapshot()!=before:raise ValueError('Source changed during neutral import.')
        except BaseException as e:errors.append(e)
    _raise(errors);return module
L=_load_legacy()
ReadOnlyClient=L.ReadOnlyClient
PROFILE='fresh-B-separated-P1-N32-Wizard-track19-diagnostic'
BINDING_PROFILE='native-id65-fresh-wizard-separated-p1-n32-research-r01'
BASENAME='native-id65-fresh-wizard-separated-p1-n32-r01'
EXPECTED_IMAGE_SHA256='e57fedebb59ffc9020de00562baccbc5a6e09bffd5f24f665d34d5f03163cbdb'
BODY_SHA256='503916389b85b655f5e1db5728e01fc2ea95daa7d3b635a3df7f5f4cc6452d29'
SCENE_SHA256='08718e7594702ab21e17f49c459b99cf4e35c8190258abbcaaf720106a880343'
FLOOR_WARNING='Pause in UI: outside authored floor; not a collision-bug diagnosis.'
FALSE_FLAGS=('candidate_authenticated','runtime_pass','ui_result_approved','normal_export_approved')
FILE_ROLES=('original_bin','original_cue','candidate_bin','candidate_cue','receipt','emulator')
SHADER_CACHE_PATH=L.SHADER_CACHE_PATH
HTTP_CACHE_PATH=L.HTTP_CACHE_PATH
BASE_JSON='{"kind":"spyro-native-fresh-intent","schema":1,"floor":{"x":8448,"y":8448,"z":8704,"width":2048,"depth":2048},"spawn":{"x":8960,"y":8960,"heading":64},"display":{"floorRgb":[80,128,112],"skyRgb":[128,96,64]},"musicTrackId":19}'
LIVE_READY=True

@dataclass(frozen=True)
class _Publication:
    directory:str
    receipt_length:int
    receipt_sha256:str
    plan_sha256:str
    clearance_sha256:str
    aggregate_json:str
    aggregate_sha256:str
    outcome:str
@dataclass(frozen=True)
class _CachePin:
    path:str
    device:int
    inode:int

# Root-reviewed actual Publish04 + independent Verify05, PsfP2X.
# Full six-owner independent final checks: separated observer evidence11.
# Cache identities are metadata-only; actual live descriptors still required.
_REVIEWED_PUBLICATION=_Publication(
    directory='/Users/john/Documents/Codex/2026-07-13/i-needed-to-make-a-new/spyro1editor-v3-release/_local/research/dark-wizard-native-closure-20260906/separated-publication-gates-20260908-r01.PsfP2X/04-separated-candidate',
    receipt_length=5875,
    receipt_sha256='7c590c9d1454b9f22b51c643084a6f49220a78c49cc2e657983d9030d1a1932d',
    plan_sha256='c4e638fd34ad6a60165ead6bbe32c94c2bc7ce09c39dca32b82b8083d533e73a',
    clearance_sha256='15cc5b7e936ee5d579ba21d593cc9c97b9ca45220eb69beeedfde095ac01aff0',
    aggregate_json='{"kind":"spyro-native-fresh-level","schema":1,"base":'+BASE_JSON+',"placements":[{"id":"11111111111111111111111111111111","tag":"wizard-route","origin":{"x":9984,"y":9984,"z":8704},"routeEnd":{"x":9472,"y":9984}}]}',
    aggregate_sha256='de14be577c794fa1821552749b2f036aa0bda972d8492a87aba37f7a3e18f953',
    outcome='verified-success')
_REVIEWED_CACHES=(
    _CachePin(SHADER_CACHE_PATH,0x100000e,1242218),
    _CachePin(HTTP_CACHE_PATH,0x100000e,691426))

# Original Town callback/deletion tail and EXE allocator/damage windows, also
# byte-identical in the actual separated image. READ fingerprints, not executed
# branches or proof that a sampled null handle was produced by this callback.
ABSENCE_CODE_WINDOWS=(
    (0x80083178,bytes.fromhex('0880053cbc8ba5240000a28c000000000e00401c000000001000448e00000000060080100000000036008284000000000200571400000000480080a00780013c985820ac941c0208212060021000428e00000000070040100c006426645e000c9cfea524012042280200401400000000000040ae490064926300022418108210')),
    (0x80087248,bytes.fromhex('0c0002a2212060025a49010c000000008001a98f000000000000338d0400292520da60168001a9afcc01bf8fc801be8f')),
    (0x80052568,bytes.fromhex('340085840780013c785721240c00a0040000218c802805002028a100fcffa220040045200000a28c00000000fcff4414000000000400828c000000000000a2ac0780013c905821240000218c000000002208240003002018fdff02200800e003480082a00780013ca45621240000228c00000000ffff4220000022ac5800812048002280ffff03200000812009004314feff0320a8ff212048002280feff0320fdff4310a8ff21205800212058002120ffff0320480023a00780023c3c5742240000438c00000000221861000200600400000000000041ac0000848c00000000ffff8280ffff03201800812009004314feff032018002120ffff2280feff0320fdff431018002120e8ff2120e8ff2120ffff0320ffff23a00780023c305942240000438c00000000221861000200601c')),
    (0x80040F68,bytes.fromhex('0780033cb458638c0780023c6c59428ce0ffbd271000b0af218000001800bfaf030062101400b1af96040108211000000880023cb88b428c00000000080040100efe02240880033c6c8b638c000000000300601824208200fffb0224242082000880073c848ae7240000e28c0000000024208200f10582304f004010000000000880033cbc8b638c000000004b006004000482300780023ca056428c000000000500401410008230ffff62240880013cbc8b22ac1000823008004010200082300880023cbc8b428c000000002e004104190004243c0401081f00042409004010d4ffe5240780023cd461428c040006241f0044909e56010c')),
)

def _capture_absent_companion(client,label):
    """Fresh absent-only diagnostic; no old refused payload or invented handle.

    This is intentionally separate from the pinned present-only capture. Health
    -1 is a supported scalar value, never a player-death or callback verdict.
    """
    result=dict(utc=datetime.now(timezone.utc).isoformat(),label=label[:120],candidate_authenticated=False,
        external_session_binding_checked=False,sequential_reads_not_atomic=True,runtime_pass=False,
        ui_result_approved=False,normal_export_approved=False,payload_valid=False,gate_reread_consistent=False,
        diagnostic_reader='separated-fresh-absent-companion')
    try:
        values=L._fields(client,L.FIELDS);result.update(values)
        for name,(_,expected) in L.GATES.items():
            if values[name]!=expected:raise ValueError('Wrong/uninitialized P1 gate: '+name)
        if values['companion_handle']!=0 or values['player_health_s32'] not in (0,-1):
            raise ValueError('Fresh absence requires null handle and health exactly0/-1; not a death verdict.')
        if not 0<=values['dynamic_count']<=32:raise ValueError('Absent-arm count must be0..32.')
        for name,start,end,stride in (('properties_cursor',L.PRIVATE,L.END,24),('next_free_row',L.BASE,L.RESERVE,88),
                                    ('next_free_particle',L.POOL,L.PETE,32)):
            if not L._grid(values[name],start,end,stride):raise ValueError('Out-of-owner or unaligned cursor: '+name)
        witnesses=[]
        def read(at,n):
            b=client.read(at,n);witnesses.append((at,b));return b
        for at,expected in (*L.CODE_WINDOWS,*ABSENCE_CODE_WINDOWS):
            if read(at,len(expected))!=expected:raise ValueError(f'P1 code witness changed at{at:08X}')
        raw=read(L.PLACED,88);actor=L.decode_moby(raw,0,L.PLACED)
        if actor['class_id']!=0x11B or actor['properties']!=L.WPRIVATE:
            raise ValueError('Placed Wizard is raw/stale or has the wrong private owner.')
        private=read(L.WPRIVATE,80)
        if struct.unpack_from('<I',private)[0]!=L.WPRIVATE+0x28:raise ValueError('Placed nested private link is unrelocated or changed.')
        active=[];terminal=None
        for i in range(32):
            at=L.BASE+i*88;b=read(at,88) # Every scanned row, even inactive, is reread below.
            if b[0x48]==0xFF:terminal=i;break
            if b[0x48]<0x80:
                item=L.decode_moby(b,1+i,at);p=item['properties']
                if item['class_id']==0x78:raise ValueError('Null companion handle conflicts with an active Sparx row.')
                item['private_pointer_classification']='owned-grid' if L._grid(p,L.PRIVATE,L.END-24,24) else 'outside-owned-private-grid'
                item['raw_56_u8']=b[0x56];active.append(item)
        if len(active)!=values['dynamic_count']:raise ValueError('Dynamic count disagrees with scanned active rows.')
        reserve=read(L.RESERVE+0x48,1)[0] if terminal is None else None
        if reserve not in (None,255):raise ValueError('Separate dynamic terminal owner is malformed.')
        private_block=read(L.PRIVATE,0x300)
        pool=b''.join(read(L.POOL+i,1024) for i in range(0,0x2000,1024))
        counts=Counter();samples=[];particle_terminal=None;particle_count=0
        for i in range(256):
            b=pool[i*32:(i+1)*32]
            if b[1]==0xFF:particle_terminal=i;break
            if b[1]>=0x80:continue
            particle_count+=1;counts[f'{b[0]:02X}/kind{b[1]}']+=1
            if b[0] in (7,0x41,0,0xC,0x42,0x46,0x47) and len(samples)<16:
                samples.append(dict(slot=i,selector=b[0],kind=b[1],age=b[2],visibility=b[3],raw_hex=b.hex()))
        pete=read(L.PETE,4)
        if pete!=b'\xff'*4:raise ValueError('Separate particle terminal owner is malformed.')
        # All scalar reports and all newly read payload owners are rechecked,
        # including the complete dynamic rows used for the negative78 claim.
        after=L._fields(client,L.FIELDS)
        if after!=values:raise ValueError('Gate/cursor/renderer/diagnostic changed during sequential reads.')
        for at,b in witnesses:
            if client.read(at,len(b))!=b:raise ValueError(f'Absent-arm witness changed at{at:08X}')
        slack,reason=L.primitive_slack(values['primitive_cursor'],values['primitive_limit'])
        result.update(payload_valid=True,gate_reread_consistent=True,wizard=actor,wizard_private_raw_hex=private.hex(),
            companion=None,companion_observation='absent',absence_reason='native-nonpositive-health-branch-compatible',
            absence_cause_proven=False,dynamic_rows=dict(active=active,terminal_slot=terminal,no_in_range_terminal=terminal is None),
            reserve_terminal_u8_not_actor33=reserve,raw_dynamic_private_block_hex=private_block.hex(),
            particles=dict(active_count=particle_count,terminal_slot=particle_terminal,selector_kind_counts=dict(counts),samples=samples),
            particle_terminal_word_not_record257=pete.hex(),primitive_sampled_slack=slack,primitive_sampled_slack_unavailable_reason=reason)
        attention=[]
        if values['dynamic_count']==32 or terminal is None:attention.append('Pause in UI: nominal actor capacity/terminal needs inspection; no safe spawn guard or peak proof.')
        if any(x['private_pointer_classification']!='owned-grid' for x in active):attention.append('Opaque dynamic property pointer is outside the allocator grid; no dereference or class-specific conclusion.')
        if len({x['properties'] for x in active})!=len(active):attention.append('Duplicate dynamic property pointer observation; ownership/lifetime needs inspection.')
        if any(x['class_id'] not in (0xE,0xF,0x10,0x26,0x56,0x104,0x105) for x in active):attention.append('Unclosed dynamic class observed; no new family acceptance.')
        if particle_terminal is None:attention.append('Pause in UI: full pool or separate terminal owner needs inspection, not a257th/33rd record.')
        if values['renderer_exhaustion_flag'] or slack is None or slack<0:attention.append('Pause in UI: sampled renderer budget requires inspection; not a peak measurement.')
        if attention:result['attention']=attention
    except (ValueError,struct.error) as error:
        result['payload_refused']=str(error);result['attention']=['Pause in UI: no accepted layout/gameplay sample.']
    return result

def _hash(b):return hashlib.sha256(b).hexdigest()
def _publication():
    p=_REVIEWED_PUBLICATION
    if type(p) is not _Publication or p.outcome!='verified-success' or type(p.outcome) is not str:
        raise ValueError('No operator-reviewed successful separated publication; uncertainty is not success.')
    if type(p.directory) is not str or not os.path.isabs(p.directory) or os.path.normpath(p.directory)!=p.directory or any(c in p.directory for c in '\r\n\x00'):
        raise ValueError('Invalid closed publication directory.')
    if type(p.receipt_length) is not int or not 0<p.receipt_length<=65536:raise ValueError('Receipt must be a bounded complete owner.')
    for value in (p.receipt_sha256,p.plan_sha256,p.clearance_sha256,p.aggregate_sha256):
        if type(value) is not str or not re.fullmatch('[0-9a-f]{64}',value):raise ValueError('Incomplete reviewed publication identity.')
    if type(p.aggregate_json) is not str or not 0<len(p.aggregate_json.encode('utf-8'))<=32768 or _hash(p.aggregate_json.encode('utf-8'))!=p.aggregate_sha256:
        raise ValueError('Reviewed aggregate bytes/hash disagree.')
    return p
def _cache_pins():
    pins=_REVIEWED_CACHES
    if type(pins) is not tuple or len(pins)>2:raise ValueError('Fresh cache policy has not been independently reviewed.')
    if any(type(p) is not _CachePin or p.path not in (SHADER_CACHE_PATH,HTTP_CACHE_PATH) or
           any(type(n) is not int or n<=0 for n in (p.device,p.inode)) for p in pins) or len({p.path for p in pins})!=len(pins):
        raise ValueError('Invalid/duplicate exact cache owner.')
    return pins

def capture(client,label):
    """Present capture is unchanged; a null hint permits a separate fresh read."""
    result=dict(L.capture(client,label))
    if result.get('expected_profile_annotation')!=L.PROFILE or result.get('expected_candidate_sha256_annotation')!=L.EXPECTED_IMAGE_SHA256 or \
       any(result.get(k) is not False for k in FALSE_FLAGS) or result.get('external_session_binding_checked') is not False:
        raise ValueError('Unexpected neutral reader profile or approval output.')
    if result.get('payload_valid') is False and result.get('companion_handle')==0 and result.get('player_health_s32') in (0,-1):
        result=_capture_absent_companion(client,label) # Discard every provisional old field.
    result.update(expected_profile_annotation=PROFILE,expected_candidate_sha256_annotation=EXPECTED_IMAGE_SHA256,
        expected_body_sha256_annotation=BODY_SHA256,authored_floor_xy_bounds_annotation=[8448,8448,10496,10496],
        expected_spawn_xyz_annotation=[8960,8960,9216],expected_heading_u8_annotation=64,expected_music_track_id_annotation=19,
        expected_wizard_route_annotation=[9984,9984,8704,9472,9984],
        neutral_reader_source_sha256_annotation=LEGACY_PINS['Observe-SpyroFreshWizardP1.py'])
    if result.get('payload_valid') is True:
        xyz=result.get('player_xyz')
        if type(xyz) is not list or len(xyz)!=3 or any(type(x) is not int for x in xyz):raise ValueError('Missing accepted player coordinates.')
        attention=result.get('attention',[])
        if type(attention) is not list or any(type(s) is not str for s in attention):raise ValueError('Invalid neutral warning shape.')
        attention=[s for s in attention if s!=FLOOR_WARNING]
        inside=8448<=xyz[0]<=10496 and 8448<=xyz[1]<=10496
        result['sampled_player_xy_inside_authored_floor']=inside
        if not inside:attention.append(FLOOR_WARNING)
        if attention:result['attention']=attention
        else:result.pop('attention',None)
    elif result.get('payload_valid') is not False:raise ValueError('Missing neutral acceptance/refusal state.')
    return result

def inspect_session(pid,uid,executable,image,*,runner=L._run):
    """Neutral OS parsing with the genuine new name; no file/live authority."""
    p=_publication();pins=_cache_pins()
    if type(pid) is not int or not 0<pid<=0x7FFFFFFF or type(uid) is not int or uid<0 or \
       type(executable) is not str or not executable.startswith('/') or any(c in executable for c in '\r\n\x00'):
        raise ValueError('Invalid nominated PID/UID/executable.')
    if type(image) is not L.FileIdentity or image.path!=p.directory+'/'+BASENAME+'.bin' or \
       any(type(x) is not int or x<=0 for x in (image.device,image.inode,image.size)) or image.size!=661547040:
        raise ValueError('Wrong separated candidate identity/path.')
    start=L.parse_process(runner(('/bin/ps','-p',str(pid),'-o','lstart=','-o','uid=','-o','comm=')),pid,uid,executable)
    files=L.parse_lsof(runner(('/usr/sbin/lsof','-nP','-a','-p',str(pid),'-d','0-4096','-F0pcftaiDnsPT','-Ts')))
    if any(int(r['p'])!=pid for r in files):raise ValueError('File snapshot belongs to another PID.')
    excluded=[];cache_values={}
    for pin in pins:
        found=[r for r in files if r.get('n')==pin.path]
        if len(found)>1:raise ValueError('Duplicate cache owner.')
        if not found:continue
        r=found[0]
        try:
            if not re.fullmatch('[0-9]+',r['s']):raise ValueError('Invalid descriptive cache size.')
            device,inode,size=int(r['D'],16),int(r['i']),int(r['s'])
        except (KeyError,ValueError) as e:raise ValueError('Incomplete exact cache metadata.') from e
        if (r.get('t'),r.get('a'),device,inode)!=('REG','u',pin.device,pin.inode):raise ValueError('Unreviewed cache owner.')
        excluded.append(r);cache_values[pin.path]=((int(r['f']),device,inode,pin.path),size)
    images=[r for r in files if r.get('n','').lower().endswith(('.bin','.cue','.iso','.chd')) and r not in excluded]
    bins=[r for r in images if r.get('n')==image.path]
    if len(bins)!=1 or any(r.get('n') not in (image.path,image.path[:-4]+'.cue') for r in images):raise ValueError('Missing/extra/ambiguous image owner.')
    b=bins[0]
    try:identity=(int(b['D'],16),int(b['i']),int(b['s']))
    except (KeyError,ValueError) as e:raise ValueError('Incomplete BIN owner.') from e
    if b.get('a')!='r' or b.get('t')!='REG' or identity!=(image.device,image.inode,image.size):raise ValueError('BIN is not exact read-open regular owner.')
    listener=L.parse_lsof(runner(('/usr/sbin/lsof','-nP','-iTCP:2345','-sTCP:LISTEN','-F0pcftaiDnsPT','-Ts')))
    if len(listener)!=1:raise ValueError('Ambiguous listener.')
    r=listener[0]
    if (r['p'],r.get('t'),r.get('P'),r.get('n'),r.get('T'))!=(str(pid),'IPv4','TCP','127.0.0.1:2345','ST=LISTEN'):
        raise ValueError('Listener is not this process and loopback endpoint.')
    shader,shader_size=cache_values.get(SHADER_CACHE_PATH,(None,None));http,http_size=cache_values.get(HTTP_CACHE_PATH,(None,None))
    return L.SessionObservation(pid,uid,start,executable,image,int(b['f']),shader,shader_size,http,http_size)

def _pins():
    p=_publication();cue=f'FILE "{BASENAME}.bin" BINARY\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\n'.encode()
    return (L._FILE_PINS[0],L._FILE_PINS[1],
        ('candidate_bin',L.FilePin(p.directory+'/'+BASENAME+'.bin',661547040,EXPECTED_IMAGE_SHA256)),
        ('candidate_cue',L.FilePin(p.directory+'/'+BASENAME+'.cue',len(cue),_hash(cue))),
        ('receipt',L.FilePin(p.directory+'/'+BASENAME+'.receipt.json',p.receipt_length,p.receipt_sha256)),L._FILE_PINS[5])
def _require_tree():
    p=_publication();L._parents(p.directory+'/'+BASENAME+'.bin')
    expected={BASENAME+suffix for suffix in ('.bin','.cue','.receipt.json')}
    with os.scandir(p.directory) as entries:
        found=[]
        for e in entries:
            if len(found)>=3 or not e.is_file(follow_symlinks=False):raise ValueError('Unexpected candidate artifact owner.')
            found.append(e.name)
    if set(found)!=expected or len(found)!=3:raise ValueError('Candidate must have exactly its three new owners.')
def _read_at(owner,offset,length):
    if type(offset) is not int or type(length) is not int or offset<0 or not 0<length<=65536 or offset>owner.pin.size-length:
        raise ValueError('Read escaped its bounded held owner.')
    return L._read_at(owner,offset,length)
def _check_files(owners):
    _require_tree()
    if set(owners)!=set(FILE_ROLES):raise ValueError('Incomplete exact six-owner set.')
    for owner in owners.values():owner.check(full=owner.pin.size<=65536)
def _logical_hash(owner,lba,length):
    if length<=0 or length%2048:raise ValueError('Logical hash requires complete sectors.')
    digest=hashlib.sha256()
    for i in range(length//2048):digest.update(_read_at(owner,(lba+i)*2352,2352)[24:2072])
    return digest.hexdigest()
def _open_profile_files():
    owners={}
    try:
        p=_publication();_require_tree()
        for role,pin in _pins():owners[role]=L._open_verified_file(pin)
        cue=f'FILE "{BASENAME}.bin" BINARY\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\n'.encode()
        if _read_at(owners['candidate_cue'],0,len(cue))!=cue:raise ValueError('Wrong new CUE bytes.')
        try:r=json.loads(_read_at(owners['receipt'],0,p.receipt_length).decode('utf-8','strict'),object_pairs_hook=L._unique_json,
            parse_constant=lambda _:(_ for _ in ()).throw(ValueError('Nonfinite receipt value.')))
        except UnicodeError as e:raise ValueError('Receipt must be strict UTF8.') from e
        pins=dict(_pins())
        required={'schema':1,'profile':BINDING_PROFILE,'planSha256':p.plan_sha256,
            'sourceImagePath':pins['original_bin'].path,'sourceCuePath':pins['original_cue'].path,
            'sourceImageSha256':pins['original_bin'].sha256,'sourceCueSha256':pins['original_cue'].sha256,
            'imageSha256':EXPECTED_IMAGE_SHA256,'imageByteLength':661547040,'changedRawSectors':1290,
            'cueSha256':_hash(cue),'bodySha256':BODY_SHA256,'executableLba':54958,'executableEndExclusive':55162,
            'canonicalDocumentJson':BASE_JSON,'canonicalDocumentSha256':_hash(BASE_JSON.encode()),
            'clearancePlanSha256':p.clearance_sha256,'aggregateCanonicalDocumentJson':p.aggregate_json,
            'aggregateCanonicalDocumentSha256':p.aggregate_sha256}
        if type(r) is not dict:raise ValueError('Receipt is not a JSON object.')
        for key,value in required.items():
            if type(r.get(key)) is not type(value) or r[key]!=value:raise ValueError('Wrong reviewed receipt field: '+key)
        for key in ('projectSaveBindingClaimed','runtimeVerified','normalCreateBinApproved','persistentOwnerIssued',
            'cardOrHistoryCompatibilityProven','wholePickupOrDeathProven','globalAudioCompatibilityProven','peakRuntimeMemoryProven',
            'hardwareTransfersProven','completeIncomingEdgeClosureProven','completeNativeDependencyClosureProven','checkpointOrForeignStateSupported'):
            if r.get(key) is not False:raise ValueError('Receipt limitation changed: '+key)
        for role,lba,n,expected in (
            ('original_bin',53875,0x66000,'a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998'),
            ('candidate_bin',54958,0x66000,'a0eb46c2735355c05d66cc33492ed70c6c2174e77e8e8eb6c2ca8fae0f3fc8dc'),
            ('candidate_bin',53909,0x20C800,BODY_SHA256),('candidate_bin',54421,0x3000,SCENE_SHA256)):
            if _logical_hash(owners[role],lba,n)!=expected:raise ValueError('Complete profile logical consumer differs.')
        _check_files(owners);return owners
    except BaseException as e:_finish_profile(owners,e,None)
def _finish_profile(owners,primary,before):
    errors=[]
    try:L._finalize_files(owners.values(),primary)
    except BaseException as e:errors.append(e)
    if owners:
        try:_require_tree()
        except BaseException as e:errors.append(e)
    if before is not None:
        try:
            if _source_snapshot()!=before:raise ValueError('Observer/helper source changed.')
        except BaseException as e:errors.append(e)
    _raise(errors)
def verify_actual_files():
    """Closed explicit file gate only; absent reviewed publication refuses."""
    _publication();owners={};before=None;error=None;result=None
    try:
        before=_source_snapshot();owners=_open_profile_files()
        result={'owners':{n:o.report() for n,o in owners.items()},'source_hashes':before,'candidate_authenticated':False,'runtime_pass':False}
    except BaseException as e:error=e
    _finish_profile(owners,error,before);result['final_full_checks_passed']=True;return result
def _external_check(pid,owners,connection=None):
    outputs=[]
    def query(argv):b=L._run(argv);outputs.append(b);return b
    exe=owners['emulator'].identity
    session=inspect_session(pid,os.getuid(),exe.path,owners['candidate_bin'].identity,runner=query)
    listener_fd=int(L.parse_lsof(outputs[2])[0]['f'])
    _,other=L.require_text_owner(L._run(('/usr/sbin/lsof','-nP','-a','-p',str(pid),'-d','txt','-F0pcftaiDnsPT','-Ts')),pid,exe)
    local=remote=server_fd=None
    if connection is not None:
        local,remote=connection.getsockname(),connection.getpeername()
        raw=L._run(('/usr/sbin/lsof','-nP','-a','-p',str(pid),'-iTCP:2345','-sTCP:ESTABLISHED','-F0pcftaiDnsPT','-Ts'))
        L.require_connected_owner(raw,session,local,remote);server_fd=int(L.parse_lsof(raw)[0]['f'])
    return L._BoundSession(session,exe,listener_fd,server_fd,local,remote,other)
def _run_session(pid,cue,input_stream,emit):
    if not LIVE_READY:raise ValueError('Separated live connection is disabled.')
    p=_publication();_cache_pins()
    if type(pid) is not int or not 0<pid<=0x7FFFFFFF or cue!=p.directory+'/'+BASENAME+'.cue':raise ValueError('Wrong fixed session nomination.')
    owners={};before=None;error=None;connection=None
    try:
        before=_source_snapshot();owners=_open_profile_files()
        baseline=_external_check(pid,owners);_check_files(owners);again=_external_check(pid,owners);L._same_bound(baseline,again)
        connection=socket.create_connection(('127.0.0.1',2345),timeout=2);connection.settimeout(2)
        _check_files(owners);bound=_external_check(pid,owners,connection);L._same_bound(again,bound)
        client=ReadOnlyClient(connection);probe=client.read(0x8007596C,4)
        _check_files(owners);after=_external_check(pid,owners,connection);L._same_bound(bound,after);baseline=after
        emit(dict(kind='connected',source_hashes=before,owners={n:o.report() for n,o in owners.items()},external_binding=asdict(after),
            probe_raw=probe.hex(),candidate_authenticated=False,runtime_pass=False,auxiliary_cache_contents_authenticated=False,
            process_identity_limitation=L.PROCESS_IDENTITY_LIMITATION))
        ordinal=0
        while True:
            label=L._command(input_stream)
            if label is None:break
            _check_files(owners);first=_external_check(pid,owners,connection);L._same_bound(baseline,first)
            sample=capture(client,label)
            _check_files(owners);after=_external_check(pid,owners,connection);L._same_bound(first,after);baseline=after
            sample.update(kind='sample',ordinal=ordinal,external_session_binding_checked=True,external_binding=asdict(after),
                auxiliary_cache_contents_authenticated=False,**{k:False for k in FALSE_FLAGS})
            emit(sample);ordinal+=1
        _check_files(owners);L._same_bound(baseline,_external_check(pid,owners,connection))
    except BaseException as e:error=e
    if connection is not None:
        try:connection.close()
        except BaseException as e:error=e if error is None else BaseExceptionGroup('Observation/socket close failures.',[error,e])
    _finish_profile(owners,error,before)
    emit(dict(kind='session_closed',final_full_checks_passed=True,candidate_authenticated=False,runtime_pass=False));return 0
def main(argv=None):
    parser=argparse.ArgumentParser(description=__doc__,allow_abbrev=False)
    parser.add_argument('--pid',type=int);parser.add_argument('--candidate-cue');parser.add_argument('--connect-identified-test-session',action='store_true')
    args=list(sys.argv[1:] if argv is None else argv)
    for flag in ('--pid','--candidate-cue','--connect-identified-test-session'):
        if sum(a==flag or a.startswith(flag+'=') for a in args)>1:parser.error('Repeated option refused.')
    selected=parser.parse_args(args)
    if not LIVE_READY:parser.error('LIVE CONNECTION DISABLED: successful publication and fresh OS review are pending.')
    if not selected.connect_identified_test_session or selected.pid is None or not selected.candidate_cue:parser.error('Explicit fixed session nomination required.')
    print('Connecting can pause DuckStation. Disconnecting can resume it if it was running when the first client connected. Verify pause state in the UI after closing; p [label] samples, q closes. No automatic reconnect.',file=sys.stderr,flush=True)
    try:return _run_session(selected.pid,selected.candidate_cue,sys.stdin.buffer,lambda e:print(json.dumps(e),flush=True))
    except BaseException as e:
        print(json.dumps(dict(kind='session_failed',error=L._failure_record(e),final_full_checks_passed=False,candidate_authenticated=False,runtime_pass=False)),flush=True)
        print('No automatic recovery; disconnecting can resume emulation. Verify pause state in the UI.',file=sys.stderr,flush=True)
        return 130 if isinstance(e,KeyboardInterrupt) else 2
if __name__=='__main__':raise SystemExit(main())
