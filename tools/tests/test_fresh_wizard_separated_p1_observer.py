"""Separated observer: source-free/fake-only tests; no live publication/session."""
import dataclasses
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import socket
import struct
import subprocess
import sys
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch

sys.dont_write_bytecode=True
REVIEWED_ACTIVE_SPEC=False
ABSENCE_SOURCE=False
TOOLS=Path(__file__).resolve().parents[1]
def load(path,name):
    spec=importlib.util.spec_from_file_location(name,path)
    value=importlib.util.module_from_spec(spec);sys.modules[name]=value
    with patch.object(socket,'create_connection',side_effect=AssertionError('No live socket')), \
         patch.object(subprocess,'Popen',side_effect=AssertionError('No process query')):
        spec.loader.exec_module(value)
    return value
N=load(TOOLS/'Observe-SpyroFreshWizardSeparatedP1.py','separated_observer_tests')
F=load(TOOLS/'tests/test_fresh_wizard_p1_observer.py','separated_unchanged_memory_fixtures')

def profile(**changes):
    values=dict(directory='/synthetic/candidate',receipt_length=100,receipt_sha256='a'*64,
                plan_sha256='b'*64,clearance_sha256='c'*64,aggregate_json='{"synthetic":true}',
                aggregate_sha256=hashlib.sha256(b'{"synthetic":true}').hexdigest(),outcome='verified-success')
    return N._Publication(**(values|changes))

def process_records(image,*,pid=321,cache=None):
    row=f'f19\0ar\0tREG\0D{image.device:x}\0i{image.inode}\0s{image.size}\0n{image.path}\0\n'
    return (f'p{pid}\0cDuckStation\0\n'+row+(cache or '')).encode()

def observed(*,image=None,start='Mon Sep  7 01:02:03 2026',cache=None):
    image=image or N.L.FileIdentity('/synthetic/candidate/'+N.BASENAME+'.bin',1,2,661547040)
    return N.L.SessionObservation(321,501,start,'/synthetic/DuckStation',image,19,
        auxiliary_cache=cache)

def leaves(error):
    return [x for e in error.exceptions for x in leaves(e)] if isinstance(error,BaseExceptionGroup) else [error]

def absent_fixture(health=0,count=1):
    m=F.fixture();m.word(0x80075898,0);m.blob(0x80078BBC,struct.pack('<i',health))
    m.word(0x800756A4,count);m.word(0x80075930,N.L.END-count*24)
    m.word(0x8007573C,N.L.BASE+count*88)
    for i in range(32):
        m.blob(N.L.BASE+i*88,F.row(0x26,0 if i<count else 0xFF,N.L.END-(i+1)*24))
    m.blob(N.L.RESERVE+0x48,b'\xff')
    # Added independently source-bound fingerprints will be installed here once
    # the new arm exists. This synthetic memory is never source/runtime proof.
    for at,b in getattr(N,'ABSENCE_CODE_WINDOWS',()):m.blob(at,b)
    return m

class AbsentCompanionTests(unittest.TestCase):
    def test_complete_frozen_absence_fingerprints_not_just_matching_prefixes(self):
        self.assertEqual({
            0x80083178:(0x80,'d0d8e98656ea6c3b4dd760fc38c8f4b9976647d41074815704c1becb8556b8f2'),
            0x80087248:(0x30,'bb460ef160eeb33d5c7f722ed97a4055ea58fb4aecf05f960fe3d28f2cfd85d8'),
            0x80052568:(0x130,'5f7a95466d261fadeee8641fe17e0b94c81692cfafb8be376eb613016a3a16af'),
            0x80040F68:(0xF8,'20b8910053abca1f5a3bfb052c241d4b242a851add580b50ce44894fc50456f6')},
            {at:(len(b),N._hash(b)) for at,b in N.ABSENCE_CODE_WINDOWS})
        self.assertEqual(4,len(N.ABSENCE_CODE_WINDOWS))

    def test_fresh_closed_health_set_without_zero_page_or_old_refusal_upgrade(self):
        for health in (0,-1):
            for count in (0,1,32):
                with self.subTest(health=health,count=count):
                    old=N.L.capture(absent_fixture(health,count),'historical-contract')
                    self.assertFalse(old['payload_valid']);self.assertFalse(old['gate_reread_consistent'])
                    m=absent_fixture(health,count);r=N.capture(m,'fresh-absence')
                    self.assertTrue(r['payload_valid'],r.get('payload_refused'))
                    self.assertTrue(r['gate_reread_consistent']);self.assertIsNone(r['companion'])
                    self.assertEqual('absent',r['companion_observation'])
                    self.assertEqual('native-nonpositive-health-branch-compatible',r['absence_reason'])
                    self.assertIs(r['absence_cause_proven'],False)
                    self.assertEqual(count,len(r['dynamic_rows']['active']))
                    self.assertTrue(all(at>=0x80000000 for at,n in m.reads))
                    for key in N.FALSE_FLAGS:self.assertIs(r[key],False)
                    self.assertIs(r['external_session_binding_checked'],False)
                    self.assertNotIn('player_dead',r)

    def test_unsupported_health_or_nonzero_handle_never_routes_or_reads_zero(self):
        for health,handle in ((1,0),(3,0),(-2,0),(-2147483648,0),(0,N.L.BASE+1),
                              (0,N.L.RESERVE),(0,N.L.END),(0,0xDEADBEEF)):
            m=absent_fixture();m.blob(0x80078BBC,struct.pack('<i',health));m.word(0x80075898,handle)
            with self.subTest(health=health,handle=handle),patch.object(N,'_capture_absent_companion',side_effect=AssertionError('No null arm')):
                r=N.capture(m,'unsupported');self.assertFalse(r['payload_valid'])
                self.assertNotIn((N.L.PLACED,88),m.reads);self.assertTrue(all(at>=0x80000000 for at,n in m.reads))

    def test_every_gate_and_cursor_still_refuses_before_heap(self):
        changes=[(at,value^1) for at,value in F.LITERALS.items()]
        changes += [(0x800756A4,n) for n in (33,0xFFFFFFFF)]
        for at,start,end,stride in ((0x80075930,N.L.PRIVATE,N.L.END,24),(0x8007573C,N.L.BASE,N.L.RESERVE,88),
                                    (0x80075738,N.L.POOL,N.L.PETE,32)):
            changes.extend((at,n) for n in (0,start-1,start+1,end+stride))
        for at,value in changes:
            with self.subTest(at=hex(at),value=value):
                m=absent_fixture();m.word(at,value);r=N.capture(m,'wrong-owner')
                self.assertFalse(r['payload_valid']);self.assertFalse(r['gate_reread_consistent'])
                self.assertNotIn((N.L.PLACED,88),m.reads)

    def test_all_code_first_last_byte_and_reread_mutations(self):
        for at,b in (*N.L.CODE_WINDOWS,*N.ABSENCE_CODE_WINDOWS):
            for offset in (0,len(b)-1):
                m=absent_fixture();m.data[at+offset]^=1
                with self.subTest(at=hex(at),offset=offset):
                    r=N.capture(m,'code');self.assertFalse(r['payload_valid'])
                    self.assertIn('code witness',r['payload_refused']);self.assertNotIn((N.L.PLACED,88),m.reads)
            m=absent_fixture();m.flip[at]=1;r=N.capture(m,'code-drift')
            self.assertFalse(r['payload_valid']);self.assertIn('witness changed',r['payload_refused'])

    def test_inconsistent_rows_private_and_sentinels_refuse_without_opaque_dereference(self):
        controls=(('count',lambda m:m.word(0x800756A4,0)),
            ('Sparx',lambda m:m.blob(N.L.BASE,F.row(0x78,0,N.L.END-24))),
            ('terminal',lambda m:m.blob(N.L.BASE+0x48,b'\xff')),
            ('Wizard-class',lambda m:m.blob(N.L.PLACED,F.row(0x78,0,N.L.WPRIVATE))),
            ('Wizard-owner',lambda m:m.word(N.L.PLACED,0xDEADBEEF)),
            ('nested-link',lambda m:m.word(N.L.WPRIVATE,0)),
            ('particle-terminal',lambda m:m.word(N.L.PETE,0)))
        for name,mutate in controls:
            with self.subTest(name=name):
                m=absent_fixture();mutate(m);r=N.capture(m,name)
                self.assertFalse(r['payload_valid']);self.assertFalse(r['gate_reread_consistent'])
                self.assertNotIn('companion_observation',r)
        m=absent_fixture(count=32);m.blob(N.L.RESERVE+0x48,b'\x00')
        self.assertFalse(N.capture(m,'reserve')['payload_valid'])
        m=absent_fixture();m.word(N.L.BASE,0xDEADBEEF);r=N.capture(m,'opaque')
        self.assertTrue(r['payload_valid']);self.assertTrue(any('Opaque' in a for a in r['attention']))
        self.assertNotIn((0xDEADBEEF,24),m.reads)

    def test_holes_are_not_repaired_into_dense_cursor_equations(self):
        m=absent_fixture();m.blob(N.L.BASE,F.row(0x78,0xFE,0))
        m.blob(N.L.BASE+88,F.row(0x26,0,N.L.END-24));m.blob(N.L.BASE+176,F.row(status=0xFF))
        m.word(0x8007573C,N.L.BASE);m.word(0x80075930,N.L.END)
        r=N.capture(m,'hole');self.assertTrue(r['payload_valid'])
        self.assertEqual(2,r['dynamic_rows']['terminal_slot']);self.assertEqual(N.L.BASE+88,r['dynamic_rows']['active'][0]['address'])
        for at in (N.L.BASE,N.L.BASE+88,N.L.BASE+176):self.assertEqual(2,m.reads.count((at,88)))

    def test_fresh_acquisition_and_final_scalar_barriers(self):
        # The old refusal's fields are only a hint: every field is acquired again.
        m=absent_fixture();N.capture(m,'reads')
        for at,fmt in N.L.FIELDS.values():self.assertEqual(3,m.reads.count((at,struct.calcsize(fmt))))
        for stage in (2,3):
            changes=[(0x80075898,struct.pack('<I',N.L.BASE)),(0x80078BBC,struct.pack('<i',1))]
            if stage==3:changes += [(at,bytes([m.data[at]^1])+bytes(m.data[at+i] for i in range(1,struct.calcsize(fmt)))) for at,fmt in N.L.FIELDS.values()]
            for at,replacement in changes:
                with self.subTest(stage=stage,at=hex(at)):
                    x=absent_fixture();read=x.read
                    def changing(address,n):
                        if address==at and x.reads.count((address,n))==stage-1:x.blob(address,replacement)
                        return read(address,n)
                    x.read=changing;r=N.capture(x,'scalar-drift')
                    self.assertFalse(r['payload_valid']);self.assertFalse(r['gate_reread_consistent'])
        # A legal health0 -> -1 transition at the final barrier is still refused.
        m=absent_fixture();read=m.read
        def negative(at,n):
            if at==0x80078BBC and m.reads.count((at,n))==2:m.blob(at,struct.pack('<i',-1))
            return read(at,n)
        m.read=negative;self.assertFalse(N.capture(m,'health-transition')['payload_valid'])

    def test_every_full_dynamic_row_and_all_fixed_payload_owners_are_reread(self):
        m=absent_fixture(count=32);r=N.capture(m,'all-rows');self.assertTrue(r['payload_valid'])
        for i in range(32):self.assertEqual(2,m.reads.count((N.L.BASE+i*88,88)))
        owners=[(N.L.PLACED,88),(N.L.WPRIVATE,80),(N.L.PRIVATE,0x300),(N.L.RESERVE+0x48,1),(N.L.PETE,4)]
        owners += [(N.L.POOL+i,1024) for i in range(0,0x2000,1024)]
        for at,n in owners:self.assertEqual(2,m.reads.count((at,n)))
        for at,n in owners+[(N.L.BASE+i*88,88) for i in range(32)]:
            with self.subTest(at=hex(at)):
                m=absent_fixture(count=32);m.flip[at]=1;r=N.capture(m,'payload-drift')
                self.assertFalse(r['payload_valid']);self.assertIn('witness changed',r['payload_refused'])
        m=absent_fixture();m.blob(N.L.BASE,F.row(0x78,0xFE));m.blob(N.L.BASE+88,F.row(0x26,0,N.L.END-24))
        m.blob(N.L.BASE+176,F.row(status=0xFF));read=m.read
        def inactive_to_sparx(at,n):
            if at==N.L.BASE and m.reads.count((at,n))==1:m.blob(at,F.row(0x78,0,N.L.END-48))
            return read(at,n)
        m.read=inactive_to_sparx;self.assertFalse(N.capture(m,'inactive-to-Sparx')['payload_valid'])

    def test_new_absence_retains_renderer_floor_and_full_pool_warnings(self):
        for mutate,warning in ((lambda m:m.word(0x800758B0,1),'renderer'),
            (lambda m:m.word(0x800757B0,0),'renderer'),
            (lambda m:m.blob(0x80078A58,struct.pack('<iii',10497,8960,9064)),'outside authored floor'),
            (lambda m:m.blob(N.L.POOL,bytes(0x2000)),'full pool')):
            m=absent_fixture();mutate(m);r=N.capture(m,'warnings')
            self.assertTrue(r['payload_valid']);self.assertTrue(any(warning in a for a in r['attention']))
            for key in N.FALSE_FLAGS:self.assertIs(r[key],False)

    def test_explicit_original_and_published_source_windows_and_historical_refusal(self):
        if not ABSENCE_SOURCE:self.skipTest('Explicit source-absence mode only; no BIN/default live reads.')
        # Full actual original/candidate owners, not fixture bytes or a private
        # disassembly helper. Close/check every acquired owner after any failure.
        before=N._source_snapshot();owners=[];errors=[]
        def logical(owner,offset,length):
            out=bytearray()
            while length:
                n=min(length,2048-offset%2048)
                out.extend(N._read_at(owner,offset//2048*2352+24+offset%2048,n));offset+=n;length-=n
            return bytes(out)
        try:
            pins=dict(N._pins())
            for role in ('original_bin','candidate_bin'):owners.append(N.L._open_verified_file(pins[role]))
            original,candidate=owners
            town=logical(original,37*2048+0x118E800,0xF800);exe=logical(original,53875*2048,0x66000)
            self.assertEqual('9bc923cc8d27703537b81b01f51fde351e58aabee87d38a8afc630f1031878a5',N._hash(town))
            self.assertEqual('a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998',N._hash(exe))
            overlay=logical(candidate,53875*2048,0x11000);issuedexe=logical(candidate,54958*2048,0x66000)
            self.assertEqual('ad6d952cbf70ab8b1eab53f7637ffd0235271b7b20696956c9406f36f714fa7e',N._hash(overlay))
            self.assertEqual('a0eb46c2735355c05d66cc33492ed70c6c2174e77e8e8eb6c2ca8fae0f3fc8dc',N._hash(issuedexe))
            self.assertEqual([0x80083178,0x80087248,0x80052568,0x80040F68],[at for at,b in N.ABSENCE_CODE_WINDOWS])
            for at,b in N.ABSENCE_CODE_WINDOWS:
                base,source,issued=(0x8007AA38,town,overlay) if at>=0x8007AA38 else (0x8000F800,exe,issuedexe)
                self.assertEqual(b,source[at-base:at-base+len(b)]);self.assertEqual(b,issued[at-base:at-base+len(b)])
            def word(data,base,at):return struct.unpack_from('<I',data,at-base)[0]
            literal={0x80083178:0x3C058008,0x8008317C:0x24A58BBC,0x80083180:0x8CA20000,
                0x80083184:0,0x80083188:0x1C40000E,0x8008318C:0,0x800831B4:0x3C018007,
                0x800831B8:0xAC205898,0x800831BC:0x08021C94,0x800831C0:0x02602021,
                0x80087250:0x0C01495A,0x80087254:0}
            for at,w in literal.items():self.assertEqual(w,word(town,0x8007AA38,at))
            for at,w in {0x80040FE8:0x3C038008,0x80040FEC:0x8C638BBC,0x80040FF0:0,
                0x80040FF4:0x0460004B,0x80041010:0x2462FFFF,0x80041014:0x3C018008,
                0x80041018:0xAC228BBC,0x800525CC:0x3C018007,0x800525D0:0x242156A4,
                0x800525D4:0x8C220000,0x800525D8:0,0x800525DC:0x2042FFFF,0x800525E0:0xAC220000}.items():
                self.assertEqual(w,word(exe,0x8000F800,at))
            # No live RAM reconstruction or upgrade of this historical refusal.
            log=TOOLS.parent/'_local/native-fresh-wizard-separated-runtime-20260908-r01.V9FBOP/observer01.jsonl'
            self.assertLessEqual(log.stat().st_size,1024*1024);b=log.read_bytes()
            self.assertEqual('00fe5641fb891d8f6c76c296d692720396cee139c51336b730442c2ef9525372',N._hash(b))
            records=[json.loads(line) for line in b.splitlines()]
            sample=next(r for r in records if r.get('kind')=='sample' and r.get('ordinal')==11)
            self.assertFalse(sample['payload_valid']);self.assertFalse(sample['gate_reread_consistent'])
            self.assertEqual('Out-of-owner or unaligned cursor: companion_handle',sample['payload_refused'])
            self.assertNotIn('wizard',sample);self.assertEqual(0,sample['companion_handle'])
        except BaseException as e:errors.append(e)
        finally:
            for owner in owners:
                try:owner.check(full=True)
                except BaseException as e:errors.append(e)
                finally:
                    try:owner.close()
                    except BaseException as e:errors.append(e)
            try:self.assertEqual(before,N._source_snapshot())
            except BaseException as e:errors.append(e)
        N._raise(errors)

class ProfileTests(unittest.TestCase):
    def test_old_sources_are_unchanged(self):
        snapshot=N._source_snapshot()
        for name,digest in N.LEGACY_PINS.items():self.assertEqual(digest,snapshot[name])
        self.assertEqual('native-id65-fresh-wizard-p1-n32-r01',N.L.CANDIDATE_BASENAME)
        self.assertEqual('c4a70f826b1a9a3d4ad8e36512be354b6205d27ea2c7a314776a51067f35b5a6',N.L.EXPECTED_IMAGE_SHA256)
        self.assertTrue(N.L.PHASE2_READY)

    def test_annotations_and_only_old_floor_warning_are_recomputed(self):
        before=(N.L.PROFILE,N.L.EXPECTED_IMAGE_SHA256,N.L.CANDIDATE_BASENAME,N.L._FILE_PINS)
        for x,y,inside in ((10000,10000,True),(8448,8448,True),(10496,10496,True),(10497,9000,False),(8447,9000,False)):
            with self.subTest(x=x,y=y):
                m=F.fixture();m.blob(0x80078A58,struct.pack('<iii',x,y,9216))
                r=N.capture(m,'neutral')
                self.assertTrue(r['payload_valid']);self.assertEqual(inside,r['sampled_player_xy_inside_authored_floor'])
                self.assertEqual([8448,8448,10496,10496],r['authored_floor_xy_bounds_annotation'])
                self.assertEqual([9984,9984,8704,9472,9984],r['expected_wizard_route_annotation'])
                self.assertEqual(N.EXPECTED_IMAGE_SHA256,r['expected_candidate_sha256_annotation'])
                self.assertEqual(N.PROFILE,r['expected_profile_annotation'])
                self.assertEqual(not inside,N.FLOOR_WARNING in r.get('attention',[]))
                self.assertTrue(any('Opaque dynamic' in s for s in r['attention']))
                for key in N.FALSE_FLAGS:self.assertIs(r[key],False)
        self.assertEqual(before,(N.L.PROFILE,N.L.EXPECTED_IMAGE_SHA256,N.L.CANDIDATE_BASENAME,N.L._FILE_PINS))

    def test_old_refusal_and_all_literal_layout_gates_remain(self):
        self.assertEqual(F.LITERALS,{at:value for at,value in N.L.GATES.values()})
        for at,value in F.LITERALS.items():
            m=F.fixture();m.word(at,value^1);r=N.capture(m,'wrong')
            self.assertFalse(r['payload_valid']);self.assertIn('payload_refused',r)
            self.assertNotIn((N.L.PLACED,88),m.reads)
        m=F.fixture();m.flip[N.L.WPRIVATE]=1;r=N.capture(m,'drift')
        self.assertFalse(r['payload_valid']);self.assertFalse(r['gate_reread_consistent'])
        m=F.fixture();m.word(0x80075898,N.L.RESERVE);self.assertFalse(N.capture(m,'companion')['payload_valid'])
        m=F.fixture();m.blob(N.L.BASE+0x48,b'\xff');self.assertFalse(N.capture(m,'terminal')['payload_valid'])

    def test_uncertain_publication_and_incomplete_cache_policy_are_not_authority(self):
        for outcome in ('publication-unconfirmed','known-committed-failure','aborted','',True):
            with patch.object(N,'_REVIEWED_PUBLICATION',profile(outcome=outcome)),self.assertRaises(ValueError):N._publication()
        for p in (None,profile(directory='/synthetic/../candidate'),profile(receipt_length=65537),profile(plan_sha256='B'*64)):
            with patch.object(N,'_REVIEWED_PUBLICATION',p),self.assertRaises(ValueError):N._publication()
        with patch.object(N,'_REVIEWED_PUBLICATION',profile()):
            self.assertEqual('/synthetic/candidate',N._publication().directory)
            with patch.object(N,'_REVIEWED_CACHES',None),self.assertRaises(ValueError):N._cache_pins()

    def test_exact_cli_and_internal_disable_precede_external_work(self):
        choices=([],['--pid','321'],['--connect-identified-test-session','--pid','321','--candidate-cue','/fake'],
                 ['--pid=321','--pid','322'],['--candidate-cue=x','--candidate-cue=y'],['--unknown'],
                 ['--pid','1','extra'],['--connect-identified-test-session=true'])
        with patch.object(N,'LIVE_READY',False),patch.object(N,'_run_session',side_effect=AssertionError('No session')):
            for args in choices:
                with self.subTest(args=args),self.assertRaises(SystemExit),patch('sys.stderr',io.StringIO()):N.main(args)
        with patch.object(N,'LIVE_READY',False),patch.object(N,'_source_snapshot',side_effect=AssertionError('No source work')),self.assertRaisesRegex(ValueError,'disabled'):
            N._run_session(321,'/fake',io.BytesIO(),lambda _:self.fail('No event'))

    def test_enabled_missing_spec_or_cache_refuses_before_any_artifact_or_external_work(self):
        for publication,caches,reason in ((None,(),'successful separated publication'),(profile(),None,'cache policy')):
            with self.subTest(reason=reason),patch.object(N,'LIVE_READY',True),patch.object(N,'_REVIEWED_PUBLICATION',publication), \
                 patch.object(N,'_REVIEWED_CACHES',caches),patch.object(N,'_source_snapshot') as source, \
                 patch.object(N,'_open_profile_files') as artifacts,patch.object(N,'_external_check') as external, \
                 patch.object(N.socket,'create_connection') as connection:
                with self.assertRaisesRegex(ValueError,reason):
                    N._run_session(321,'/synthetic/candidate/'+N.BASENAME+'.cue',io.BytesIO(),lambda _:self.fail('No event'))
                source.assert_not_called();artifacts.assert_not_called();external.assert_not_called();connection.assert_not_called()
        with patch.object(N,'_REVIEWED_PUBLICATION',None),patch.object(N,'_source_snapshot') as source:
            with self.assertRaisesRegex(ValueError,'successful separated publication'):N.verify_actual_files()
            source.assert_not_called()

    def test_enabled_cli_is_still_explicit_exact_and_wrong_nomination_has_no_io(self):
        cue='/synthetic/candidate/'+N.BASENAME+'.cue'
        parser_refusals=([],['--pid=321'],['--connect-identified-test-session'],['--pid=321','--pid','322'],
            ['--candidate-cue=x','--candidate-cue','y'],['--unknown'],['--connect-identified-test-session=true'])
        with patch.object(N,'LIVE_READY',True),patch.object(N,'_REVIEWED_PUBLICATION',profile()),patch.object(N,'_REVIEWED_CACHES',()), \
             patch.object(N,'_source_snapshot') as source,patch.object(N,'_open_profile_files') as artifacts, \
             patch.object(N,'_external_check') as external,patch.object(N.socket,'create_connection') as connection:
            for args in parser_refusals:
                with self.subTest(args=args),self.assertRaises(SystemExit),patch('sys.stderr',io.StringIO()):N.main(args)
            for pid,path in (('0',cue),('-1',cue),('2147483648',cue),('321',cue+' '),('321','/elsewhere/'+N.BASENAME+'.cue'),
                             ('321','/synthetic/candidate/'+N.L.CANDIDATE_BASENAME+'.cue')):
                out=io.StringIO()
                with patch('sys.stderr',io.StringIO()),patch('sys.stdout',out):
                    self.assertEqual(2,N.main(['--connect-identified-test-session','--pid',pid,'--candidate-cue',path]))
                event=json.loads(out.getvalue());self.assertEqual('session_failed',event['kind'])
                self.assertFalse(event['final_full_checks_passed']);self.assertFalse(event['candidate_authenticated'])
            source.assert_not_called();artifacts.assert_not_called();external.assert_not_called();connection.assert_not_called()

    def test_explicit_reviewed_active_spec_without_files_or_live_access(self):
        if not REVIEWED_ACTIVE_SPEC:self.skipTest('Explicit reviewed-active-spec metadata mode only; not an actual file/live gate.')
        # This checks the real, unpatched, code-owned selection only. It does not
        # establish publication success, file identity, FD ownership or runtime.
        with patch.object(N,'_source_snapshot',side_effect=AssertionError('No source re-read')), \
             patch.object(N.L,'_open_verified_file',side_effect=AssertionError('No artifact read')), \
             patch.object(N,'_external_check',side_effect=AssertionError('No process query')), \
             patch.object(N.socket,'create_connection',side_effect=AssertionError('No socket')):
            self.assertIs(N.LIVE_READY,True)
            p=N._publication();caches=N._cache_pins();pins=dict(N._pins())
            self.assertEqual(set(N.FILE_ROLES),set(pins))
            self.assertEqual('verified-success',p.outcome)
            self.assertEqual(str(TOOLS.parent/'_local/research/dark-wizard-native-closure-20260906/separated-publication-gates-20260908-r01.PsfP2X/04-separated-candidate'),p.directory)
            self.assertEqual((5875,'7c590c9d1454b9f22b51c643084a6f49220a78c49cc2e657983d9030d1a1932d'),(p.receipt_length,p.receipt_sha256))
            self.assertEqual('c4e638fd34ad6a60165ead6bbe32c94c2bc7ce09c39dca32b82b8083d533e73a',p.plan_sha256)
            self.assertEqual('15cc5b7e936ee5d579ba21d593cc9c97b9ca45220eb69beeedfde095ac01aff0',p.clearance_sha256)
            self.assertEqual('native-id65-fresh-wizard-separated-p1-n32-r01',N.BASENAME)
            self.assertEqual('native-id65-fresh-wizard-separated-p1-n32-research-r01',N.BINDING_PROFILE)
            for role,suffix in (('candidate_bin','.bin'),('candidate_cue','.cue'),('receipt','.receipt.json')):
                self.assertEqual(p.directory+'/'+N.BASENAME+suffix,pins[role].path)
            self.assertEqual((661547040,'e57fedebb59ffc9020de00562baccbc5a6e09bffd5f24f665d34d5f03163cbdb'),
                (pins['candidate_bin'].size,pins['candidate_bin'].sha256))
            self.assertEqual('572af87a5fef9497fa32cab6588cdc87f61304c430dfc5e58c8dd6aae070c556',pins['candidate_cue'].sha256)
            self.assertEqual((p.receipt_length,p.receipt_sha256),(pins['receipt'].size,pins['receipt'].sha256))
            for index,role in ((0,'original_bin'),(1,'original_cue'),(5,'emulator')):
                self.assertEqual(N.L._FILE_PINS[index][1],pins[role])
            aggregate=json.loads(p.aggregate_json,object_pairs_hook=N.L._unique_json)
            self.assertEqual(dict(kind='spyro-native-fresh-level',schema=1,base=json.loads(N.BASE_JSON),placements=[
                dict(id='1'*32,tag='wizard-route',origin=dict(x=9984,y=9984,z=8704),routeEnd=dict(x=9472,y=9984))]),aggregate)
            self.assertEqual(p.aggregate_sha256,hashlib.sha256(p.aggregate_json.encode()).hexdigest())
            self.assertEqual('de14be577c794fa1821552749b2f036aa0bda972d8492a87aba37f7a3e18f953',p.aggregate_sha256)
            self.assertEqual((N._CachePin(N.SHADER_CACHE_PATH,0x100000e,1242218),N._CachePin(N.HTTP_CACHE_PATH,0x100000e,691426)),caches)

class SessionIdentityTests(unittest.TestCase):
    def test_malformed_nomination_refuses_before_query(self):
        for pid,uid,exe in ((0,501,'/x'),(True,501,'/x'),(321,-1,'/x'),(321,501,'relative'),(321,501,'/x\n')):
            with self.subTest(pid=pid,uid=uid,exe=exe),patch.object(N,'_REVIEWED_PUBLICATION',profile()), \
                 patch.object(N,'_REVIEWED_CACHES',()),self.assertRaises(ValueError):
                N.inspect_session(pid,uid,exe,observed().image,runner=lambda _:self.fail('Query preceded shape refusal'))

    def run_inspection(self,*,image=None,records=None,ps=None,listener=None,caches=()):
        image=image or observed().image
        answers=[ps or b'Mon Sep  7 01:02:03 2026 501 /synthetic/DuckStation\n',
                 records or process_records(image),listener or b'p321\0cDuckStation\0\nf7\0tIPv4\0PTCP\0n127.0.0.1:2345\0TST=LISTEN\0\n']
        calls=[]
        def query(argv):calls.append(argv);return answers[len(calls)-1]
        with patch.object(N,'_REVIEWED_PUBLICATION',profile()),patch.object(N,'_REVIEWED_CACHES',caches):
            return N.inspect_session(321,501,'/synthetic/DuckStation',image,runner=query),calls

    def test_actual_new_name_is_compared_without_old_alias(self):
        value,calls=self.run_inspection();self.assertEqual(observed(),value);self.assertEqual(3,len(calls));self.assertFalse(value.candidate_authenticated)
        for path in ('/synthetic/candidate/'+N.L.CANDIDATE_BASENAME+'.bin','/elsewhere/'+N.BASENAME+'.bin'):
            with self.assertRaises(ValueError):self.run_inspection(image=dataclasses.replace(observed().image,path=path))
        for replace in ({'device':2},{'inode':3},{'size':661547039}):
            image=dataclasses.replace(observed().image,**replace)
            with self.assertRaises(ValueError):self.run_inspection(records=process_records(image))
        with self.assertRaises(ValueError):self.run_inspection(ps=b'Mon Sep  7 01:02:03 2026 502 /synthetic/DuckStation\n')
        with self.assertRaises(ValueError):self.run_inspection(listener=b'p322\0\nf7\0tIPv4\0PTCP\0n127.0.0.1:2345\0TST=LISTEN\0\n')

    def test_new_cache_device_is_neutral_and_other_image_neighbors_refuse(self):
        pin=N._CachePin(N.SHADER_CACHE_PATH,0x100000e,1242218)
        row=f'f5\0au\0tREG\0D100000e\0i1242218\0s101882\0n{pin.path}\0\n'
        value,_=self.run_inspection(records=process_records(observed().image,cache=row),caches=(pin,))
        self.assertEqual((5,0x100000e,1242218,pin.path),value.auxiliary_cache)
        for wrong in (row.replace('100000e','1000011'),row.replace('1242218','1242219'),row.replace('au','ar'),row.replace(pin.path,pin.path+'.other.bin')):
            with self.assertRaises(ValueError):self.run_inspection(records=process_records(observed().image,cache=wrong),caches=(pin,))
        with self.assertRaises(ValueError):self.run_inspection(records=process_records(observed().image,cache=row),caches=())
        changed=dataclasses.replace(value,auxiliary_cache_size=999);N.L.require_same_session(value,changed)
        for change in ({'auxiliary_cache':None},{'auxiliary_cache':(6,0x100000e,1242218,pin.path)},{'start_time':'Mon Sep  7 01:02:04 2026'}):
            with self.assertRaises(ValueError):N.L.require_same_session(value,dataclasses.replace(value,**change))

    def test_both_cache_owners_are_independent_and_sizes_only_descriptive(self):
        pins=(N._CachePin(N.SHADER_CACHE_PATH,0x100000e,1242218),N._CachePin(N.HTTP_CACHE_PATH,0x100000e,691426))
        rows=[f'f{fd}\0au\0tREG\0D100000e\0i{pin.inode}\0s{size}\0n{pin.path}\0\n'
              for fd,size,pin in zip((5,6),(101882,0),pins)]
        first,_=self.run_inspection(records=process_records(observed().image,cache=''.join(rows)),caches=pins)
        self.assertEqual((6,0x100000e,691426,N.HTTP_CACHE_PATH),first.auxiliary_http_cache)
        changed,_=self.run_inspection(records=process_records(observed().image,cache=''.join(rows).replace('s0\0','s123\0')),caches=pins)
        N.L.require_same_session(first,changed)
        for index in range(2):
            without,_=self.run_inspection(records=process_records(observed().image,cache=rows[1-index]),caches=pins)
            with self.assertRaises(ValueError):N.L.require_same_session(first,without)
            changed_rows=rows.copy();changed_rows[index]=rows[index].replace(f'f{5+index}\0',f'f{7+index}\0')
            other,_=self.run_inspection(records=process_records(observed().image,cache=''.join(changed_rows)),caches=pins)
            with self.assertRaises(ValueError):N.L.require_same_session(first,other)
            for bad in (rows[index].replace('100000e','1000011'),rows[index].replace(f'i{pins[index].inode}',f'i{pins[index].inode+1}'),
                        rows[index].replace('tREG','tDIR'),rows[index].replace('au','ar'),rows[index].replace(pins[index].path,pins[index].path+'.bin')):
                with self.assertRaises(ValueError):self.run_inspection(records=process_records(observed().image,cache=rows[1-index]+bad),caches=pins)

    def test_real_external_composition_binds_text_and_established_server_before_read(self):
        exe=N.L.FileIdentity('/synthetic/DuckStation',1,3,10)
        owners={'candidate_bin':SimpleNamespace(identity=observed().image),'emulator':SimpleNamespace(identity=exe)}
        text=b'p321\0\nftxt\0tREG\0D1\0i3\0s10\0n/synthetic/DuckStation\0\n'
        server=b'p321\0\nf8\0tIPv4\0PTCP\0n127.0.0.1:2345->127.0.0.1:50000\0TST=ESTABLISHED\0\n'
        class Connection:
            def getsockname(self):return ('127.0.0.1',50000)
            def getpeername(self):return ('127.0.0.1',2345)
        for mapped,connected,valid in ((text,server,True),(text.replace(b'i3',b'i4'),server,False),
                (text,server.replace(b'50000',b'50001'),False),(text,server.replace(b'p321',b'p322'),False)):
            replies=[b'Mon Sep  7 01:02:03 2026 501 /synthetic/DuckStation\n',process_records(observed().image),
                b'p321\0\nf7\0tIPv4\0PTCP\0n127.0.0.1:2345\0TST=LISTEN\0\n',mapped,connected]
            calls=[]
            def run(argv):calls.append(argv);return replies[len(calls)-1]
            with patch.object(N,'_REVIEWED_PUBLICATION',profile()),patch.object(N,'_REVIEWED_CACHES',()), \
                 patch.object(N.os,'getuid',return_value=501),patch.object(N.L,'_run',side_effect=run):
                if valid:
                    bound=N._external_check(321,owners,Connection())
                    self.assertEqual((exe,7,8),(bound.executable,bound.listener_fd,bound.server_fd))
                    self.assertEqual(5,len(calls));self.assertFalse(bound.process.candidate_authenticated)
                else:
                    with self.assertRaises(ValueError):N._external_check(321,owners,Connection())

class FileProfileTests(unittest.TestCase):
    def fixture(self):
        p=profile();cue=f'FILE "{N.BASENAME}.bin" BINARY\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\n'.encode()
        source,cue_source=N.L._FILE_PINS[0][1],N.L._FILE_PINS[1][1]
        receipt=dict(schema=1,profile=N.BINDING_PROFILE,planSha256=p.plan_sha256,
            sourceImagePath=source.path,sourceCuePath=cue_source.path,sourceImageSha256=source.sha256,sourceCueSha256=cue_source.sha256,
            imageSha256=N.EXPECTED_IMAGE_SHA256,imageByteLength=661547040,changedRawSectors=1290,
            cueSha256=hashlib.sha256(cue).hexdigest(),bodySha256=N.BODY_SHA256,executableLba=54958,executableEndExclusive=55162,
            canonicalDocumentJson=N.BASE_JSON,canonicalDocumentSha256=hashlib.sha256(N.BASE_JSON.encode()).hexdigest(),
            clearancePlanSha256=p.clearance_sha256,aggregateCanonicalDocumentJson=p.aggregate_json,aggregateCanonicalDocumentSha256=p.aggregate_sha256)
        for key in ('projectSaveBindingClaimed','runtimeVerified','normalCreateBinApproved','persistentOwnerIssued',
            'cardOrHistoryCompatibilityProven','wholePickupOrDeathProven','globalAudioCompatibilityProven','peakRuntimeMemoryProven',
            'hardwareTransfersProven','completeIncomingEdgeClosureProven','completeNativeDependencyClosureProven','checkpointOrForeignStateSupported'):
            receipt[key]=False
        return p,cue,receipt

    def run_fake_open(self,raw=None,*,cue_override=None,fail_open=None,logical_bad=None):
        p,cue,receipt=self.fixture();data=json.dumps(receipt).encode() if raw is None else raw
        p=dataclasses.replace(p,receipt_length=len(data),receipt_sha256=hashlib.sha256(data).hexdigest())
        trace=[];count=0
        class Owner:
            def __init__(self,pin,role):self.pin=pin;self.role=role
            def check(self,full=False):trace.append(('check',self.role,full))
            def close(self):trace.append(('close',self.role))
        def open_file(pin):
            nonlocal count
            role=N.FILE_ROLES[count];count+=1
            if count==fail_open:raise OSError('open-primary')
            return Owner(pin,role)
        def read(owner,offset,length):
            self.assertEqual(0,offset)
            return data if owner.role=='receipt' else cue if cue_override is None else cue_override
        logical=['a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998',
            'a0eb46c2735355c05d66cc33492ed70c6c2174e77e8e8eb6c2ca8fae0f3fc8dc',N.BODY_SHA256,N.SCENE_SHA256]
        if logical_bad is not None:logical[logical_bad]='0'*64
        error=None;owners=None
        with patch.object(N,'_REVIEWED_PUBLICATION',p),patch.object(N,'_require_tree',side_effect=lambda:trace.append(('tree',))), \
             patch.object(N.L,'_open_verified_file',side_effect=open_file),patch.object(N,'_read_at',side_effect=read), \
             patch.object(N,'_logical_hash',side_effect=logical):
            try:owners=N._open_profile_files()
            except BaseException as e:error=e
            if owners:N.L._finalize_files(owners.values())
        return owners,error,trace

    def test_new_cue_receipt_and_complete_consumer_bindings(self):
        p,cue,r=self.fixture()
        self.assertEqual('572af87a5fef9497fa32cab6588cdc87f61304c430dfc5e58c8dd6aae070c556',hashlib.sha256(cue).hexdigest())
        owners,error,trace=self.run_fake_open();self.assertIsNone(error);self.assertEqual(set(N.FILE_ROLES),set(owners))
        mutations=[b'\xff',b'{"schema":1,"schema":1}',b'{"x":NaN}',b'[]']
        for key,value in (('schema',True),('imageByteLength',True),('profile',N.L.PROFILE),('runtimeVerified',True),
                          ('canonicalDocumentJson',N.BASE_JSON+' '),('aggregateCanonicalDocumentJson','{}'),
                          ('clearancePlanSha256','d'*64),('sourceCuePath','/different.cue'),('cueSha256','0'*64)):
            mutations.append(json.dumps(r|{key:value}).encode())
        for raw in mutations:
            with self.subTest(raw=raw[:80]):
                _,error,trace=self.run_fake_open(raw);self.assertIsInstance(error,ValueError)
                self.assertEqual(6,len([x for x in trace if x[0]=='check' and x[2]]));self.assertEqual(6,len([x for x in trace if x[0]=='close']))
        for index in range(4):
            _,error,_=self.run_fake_open(logical_bad=index);self.assertIsInstance(error,ValueError)
        _,error,_=self.run_fake_open(cue_override=cue.replace(N.BASENAME.encode(),N.L.CANDIDATE_BASENAME.encode()))
        self.assertIsInstance(error,ValueError)

    def test_partial_acquisition_always_checks_and_closes_only_acquired_owners(self):
        for index in range(1,7):
            _,error,trace=self.run_fake_open(fail_open=index);self.assertEqual('open-primary',str(error))
            self.assertEqual(index-1,len([x for x in trace if x[0]=='check' and x[2]]))
            self.assertEqual(index-1,len([x for x in trace if x[0]=='close']))
            self.assertEqual(1 if index==1 else 2,len([x for x in trace if x[0]=='tree']))

class FinalizationTests(unittest.TestCase):
    def test_all_final_failures_keep_primary_and_attempt_six_checks_closes_tree_sources(self):
        trace=[]
        class Owner:
            def __init__(self,name):self.name=name
            def check(self,full=False):trace.append('check-'+self.name);raise ValueError('check-'+self.name)
            def close(self):trace.append('close-'+self.name);raise OSError('close-'+self.name)
        owners={name:Owner(name) for name in N.FILE_ROLES}
        def tree():trace.append('tree');raise ValueError('tree')
        def sources():trace.append('sources');raise ValueError('sources')
        with patch.object(N,'_require_tree',side_effect=tree),patch.object(N,'_source_snapshot',side_effect=sources):
            with self.assertRaises(BaseExceptionGroup) as raised:N._finish_profile(owners,KeyboardInterrupt('primary'),{'before':1})
        self.assertEqual(['primary']+trace,[str(e) for e in leaves(raised.exception)])
        self.assertEqual(['check-'+n for n in N.FILE_ROLES]+['close-'+n for n in N.FILE_ROLES]+['tree','sources'],trace)

    def test_source_snapshot_attempts_all_owners_and_preserves_primary_before_close_error(self):
        s=SimpleNamespace(st_dev=1,st_ino=2,st_mode=0o100600,st_size=1,st_mtime_ns=3,st_ctime_ns=4)
        opened=[];closed=[];reads={}
        def open_file(*_):fd=10+len(opened);opened.append(fd);return fd
        def read_file(fd,n):
            if fd==10:raise ValueError('source-primary')
            reads[fd]=reads.get(fd,0)+1
            return b'A' if reads[fd]==1 else b''
        def close_file(fd):
            closed.append(fd)
            if fd==10:raise OSError('source-close')
        pins={name:hashlib.sha256(b'A').hexdigest() for name in N.LEGACY_PINS}
        with patch.object(N,'LEGACY_PINS',pins),patch.object(N.Path,'lstat',return_value=s), \
             patch.object(N.os,'open',side_effect=open_file),patch.object(N.os,'fstat',return_value=s), \
             patch.object(N.os,'read',side_effect=read_file),patch.object(N.os,'lseek',return_value=1), \
             patch.object(N.os,'close',side_effect=close_file):
            try:N._source_snapshot()
            except BaseException as error:failures=leaves(error)
            else:self.fail('Expected source failure')
        self.assertEqual([10,11,12,13],opened);self.assertEqual(opened,closed)
        self.assertEqual(['source-primary','source-close'],[str(x) for x in failures])

    def test_actual_tiny_held_owner_checks_full_eof_and_drift(self):
        with tempfile.TemporaryDirectory(prefix='separated-neutral-') as folder:
            path=Path(folder)/'tiny';path.write_bytes(b'bounded fixture')
            pin=N.L.FilePin(str(path.resolve()),15,hashlib.sha256(b'bounded fixture').hexdigest())
            owner=N.L._open_verified_file(pin)
            try:
                self.assertEqual(b'bounded',N._read_at(owner,0,7))
                with self.assertRaises(ValueError):N._read_at(owner,14,2)
                with self.assertRaises(ValueError):N._read_at(owner,-1,1)
                path.write_bytes(b'changed fixture')
                with self.assertRaises(ValueError):owner.check(full=True)
            finally:owner.close()
            owner.close()

    def test_fake_orchestration_discards_failed_postcheck_and_checks_every_owner(self):
        cases=[(changed,close_error,False) for changed,close_error in ((None,False),(2,False),(3,False),(4,False),(6,False),(None,True),(6,True))]
        cases += [(None,False,True),(6,False,True),(None,True,True),(6,True,True)]
        actual_capture=N.capture
        for changed,close_error,actual_absent in cases:
            trace=[];events=[];owners={};counter=0
            memory=absent_fixture()
            class Owner:
                def check(self,full=False):trace.append(('file-final',self.name,full))
                def close(self):trace.append(('file-close',self.name))
                def report(self):return {'fixture_only':True}
            for name in N.FILE_ROLES:
                o=Owner();o.name=name;owners[name]=o
            class Connection:
                def settimeout(self,_):pass
                def close(self):
                    trace.append(('socket-close',))
                    if close_error:raise OSError('socket-close')
            class Client:
                def __init__(self,_):pass
                def read(self,at,n):
                    trace.append(('m',at,n));return memory.read(at,n) if actual_absent else bytes(n)
            def external(*args):
                nonlocal counter
                counter+=1;trace.append(('external',counter))
                process=observed(start='Mon Sep  7 01:02:04 2026' if counter==changed else 'Mon Sep  7 01:02:03 2026')
                connected=len(args)==3
                return N.L._BoundSession(process,N.L.FileIdentity('/synthetic/DuckStation',1,3,10),7,
                    8 if connected else None,('127.0.0.1',50000) if connected else None,('127.0.0.1',2345) if connected else None,0)
            def sample(client,label):
                trace.append(('payload',))
                return actual_capture(client,label) if actual_absent else {'payload_valid':True,'buffered_secret':'not emitted until postcheck'}
            error=None
            with patch.object(N,'LIVE_READY',True),patch.object(N,'_REVIEWED_PUBLICATION',profile()),patch.object(N,'_REVIEWED_CACHES',()), \
                 patch.object(N,'_source_snapshot',return_value={'fixture':'not source proof'}),patch.object(N,'_open_profile_files',return_value=owners), \
                 patch.object(N,'_check_files',side_effect=lambda _:None),patch.object(N,'_require_tree',side_effect=lambda:None), \
                 patch.object(N,'_external_check',side_effect=external),patch.object(N.socket,'create_connection',return_value=Connection()), \
                 patch.object(N,'ReadOnlyClient',Client),patch.object(N,'capture',side_effect=sample):
                try:N._run_session(321,'/synthetic/candidate/'+N.BASENAME+'.cue',io.BytesIO(b'p test\nq\n'),events.append)
                except BaseException as e:error=e
            self.assertEqual(6,len([x for x in trace if x[0]=='file-final' and x[2]]))
            self.assertEqual(6,len([x for x in trace if x[0]=='file-close']))
            if changed!=2:self.assertEqual(1,trace.count(('socket-close',)))
            if close_error:
                self.assertEqual('socket-close',str(leaves(error)[-1]))
                self.assertFalse(any(e['kind']=='session_closed' for e in events))
                if changed:
                    self.assertIsInstance(leaves(error)[0],ValueError)
                    self.assertFalse(any(e['kind']=='sample' for e in events))
                continue
            if changed:
                self.assertIsInstance(error,ValueError);self.assertFalse(any(e['kind'] in ('sample','session_closed') for e in events))
            else:
                self.assertIsNone(error);self.assertEqual('session_closed',events[-1]['kind'])
                s=next(e for e in events if e['kind']=='sample');self.assertTrue(s['external_session_binding_checked'])
                if actual_absent:
                    self.assertTrue(s['payload_valid']);self.assertEqual('absent',s['companion_observation'])
                    self.assertFalse(s['absence_cause_proven']);self.assertIsNone(s['companion'])
                for key in N.FALSE_FLAGS:self.assertIs(s[key],False)
                self.assertLess(trace.index(('external',3)),trace.index(('m',0x8007596C,4)))
                self.assertLess(trace.index(('payload',)),trace.index(('external',6)))

if __name__=='__main__':
    if '--source-absence' in sys.argv:
        if sys.argv.count('--source-absence')!=1:raise SystemExit('Repeated source-absence flag.')
        ABSENCE_SOURCE=True;sys.argv.remove('--source-absence')
    if '--reviewed-active-spec' in sys.argv:
        if sys.argv.count('--reviewed-active-spec')!=1:raise SystemExit('Repeated reviewed-active-spec flag.')
        REVIEWED_ACTIVE_SPEC=True;sys.argv.remove('--reviewed-active-spec')
    unittest.main()
