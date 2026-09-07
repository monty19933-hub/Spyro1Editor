"""Explicit source/file P1 gates. No default ROM, process, socket or .NET I/O.

Native source mode uses frozen original-source combined-body/Host oracles, not a
candidate or Core DLL, with full original pre/finally/EOF checks. Separate
explicit modes read the reviewed actual files or recorded OS fixtures only.
"""
import argparse
import contextlib
import hashlib
import importlib.util
import io
import inspect
import json
import os
from pathlib import Path
import struct
import sys
import unittest
from unittest.mock import patch

sys.dont_write_bytecode=True
ROOT=Path(__file__).resolve().parents[2]
SOURCE=None
ACTUAL_FILES=False
OS_FIXTURES=False
IMAGE_SHA='fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37'
BODY='_local/research/dark-wizard-native-closure-20260906/fresh-combined-body-r01.4Uhwza/source_combined_body.py'
BODY_SHA='a7d2c10729b6a88d564297929f59d8cba456a4906d01a101be9a29ffb21d28bd'


def load(path,name):
    spec=importlib.util.spec_from_file_location(name,ROOT/path);m=importlib.util.module_from_spec(spec);sys.modules[name]=m
    with contextlib.redirect_stdout(io.StringIO()):spec.loader.exec_module(m)
    return m


class HealthMotionTests(unittest.TestCase):
    EXPECTED={
        'player_health_s32':(0x80078BBC,'<i'),
        'player_damage_accumulator_u32':(0x80078A84,'<I'),
        'player_motion_78b4c_s32x3':(0x80078B4C,'<iii'),
        'player_motion_78b64_s32x3':(0x80078B64,'<iii'),
        'player_motion_78ba4_s32':(0x80078BA4,'<i'),
        'player_state_input_78af4_s32':(0x80078AF4,'<i'),
        'player_state_input_78b08_s32':(0x80078B08,'<i'),
    }

    def setUp(self):
        self.neutral=load('tools/tests/test_fresh_wizard_p1_observer.py','p1_health_neutral_fixture')
        self.O=self.neutral.O

    def test_exact_seven_report_owners_and_rereads_not_baseline_thresholds(self):
        O=self.O;self.assertEqual(self.EXPECTED,O.HEALTH_MOTION_FIELDS)
        self.assertEqual(44,sum(struct.calcsize(fmt) for _,fmt in O.HEALTH_MOTION_FIELDS.values()))
        for name,descriptor in self.EXPECTED.items():
            self.assertEqual(descriptor,O.REPORT_FIELDS[name]);self.assertEqual(descriptor,O.FIELDS[name])
            self.assertEqual(1,O.REREAD.count(name));self.assertNotIn(name,O.GATES)

    def test_signed_extremes_high_flags_and_vectors_report_without_diagnosis(self):
        O=self.O;baseline=O.capture(self.neutral.fixture(),'same label');m=self.neutral.fixture()
        values={'player_health_s32':-1,'player_damage_accumulator_u32':0x80020020,
                'player_motion_78b4c_s32x3':[-2147483648,-1,2147483647],
                'player_motion_78b64_s32x3':[2147483647,0,-2147483648],
                'player_motion_78ba4_s32':-192,'player_state_input_78af4_s32':-2147483648,
                'player_state_input_78b08_s32':2147483647}
        for name,value in values.items():
            at,fmt=self.EXPECTED[name];m.blob(at,struct.pack(fmt,*(value if isinstance(value,list) else [value])))
        result=O.capture(m,'same label');self.assertTrue(result['payload_valid'])
        for name,value in values.items():
            self.assertEqual(value,result[name]);at,fmt=self.EXPECTED[name]
            self.assertEqual(2,m.reads.count((at,struct.calcsize(fmt))))
        for key,value in baseline.items():
            if key not in self.EXPECTED and key!='utc':self.assertEqual(value,result[key],key)
        for key in ('candidate_authenticated','external_session_binding_checked','runtime_pass','ui_result_approved','normal_export_approved'):
            self.assertFalse(result[key])

    def test_each_report_owner_drift_or_partial_read_refuses_payload(self):
        for name,(at,fmt) in self.EXPECTED.items():
            with self.subTest(name=name,kind='reread drift'):
                m=self.neutral.fixture();m.flip[at]=1;result=self.O.capture(m,'drift')
                self.assertFalse(result['payload_valid']);self.assertFalse(result['gate_reread_consistent'])
                self.assertNotIn('dynamic_rows',result);self.assertNotIn('particles',result)
            with self.subTest(name=name,kind='partial owner'):
                m=self.neutral.fixture();read=m.read
                m.read=lambda address,n:read(address,n)[:-1] if address==at else read(address,n)
                result=self.O.capture(m,'short')
                self.assertFalse(result['payload_valid']);self.assertNotIn('dynamic_rows',result)


class ConnectionWarningTests(unittest.TestCase):
    def run_main_fake(self,fail=False):
        O=load('tools/Observe-SpyroFreshWizardP1.py','p1_connection_warning_fakes')
        err=io.StringIO();out=io.StringIO();seen=[]
        class Input:buffer=io.BytesIO(b'q\n')
        def run(pid,cue,source,emit):
            seen.append(err.getvalue())
            self.assertEqual(321,pid);self.assertEqual(O.CANDIDATE_DIRECTORY+'/'+O.CANDIDATE_BASENAME+'.cue',cue)
            self.assertIs(Input.buffer,source)
            if fail:raise ValueError('synthetic closed-session failure')
            return 0 # CLI plumbing only; no fabricated connected or verified event.
        with patch.object(O,'PHASE2_READY',True),patch.object(O,'_run_session',side_effect=run) as session, \
             patch.object(O.sys,'stdin',Input()),contextlib.redirect_stderr(err),contextlib.redirect_stdout(out):
            result=O.main(['--pid','321','--candidate-cue',O.CANDIDATE_DIRECTORY+'/'+O.CANDIDATE_BASENAME+'.cue',
                           '--connect-identified-test-session'])
        self.assertEqual(1,session.call_count)
        return result,seen,err.getvalue(),out.getvalue()

    def test_pause_and_conditional_resume_warning_precedes_any_session_work(self):
        result,seen,err,out=self.run_main_fake()
        self.assertEqual(0,result);self.assertEqual('',out);self.assertEqual([err],seen)
        self.assertIn('Connecting can pause DuckStation.',seen[0])
        self.assertIn('Disconnecting can resume DuckStation if it was running when the first client was accepted.',seen[0])
        self.assertIn('Verify pause state in the emulator UI after closing',seen[0])
        self.assertIn('p <label> samples, q closes.',seen[0]);self.assertIn('No automatic reconnect.',seen[0])

    def test_failure_warns_of_conditional_resume_without_claiming_verified_closure(self):
        result,seen,err,out=self.run_main_fake(fail=True)
        self.assertEqual(2,result);self.assertEqual(1,len(seen))
        event=json.loads(out);self.assertEqual('session_failed',event['kind'])
        for key in ('final_full_checks_passed','candidate_authenticated','runtime_pass'):self.assertFalse(event[key])
        failure_warning=err[len(seen[0]):]
        self.assertIn('No automatic recovery.',failure_warning)
        self.assertIn('Disconnecting can resume DuckStation if it was running when the first client was accepted',failure_warning)
        self.assertIn('verify pause state in the emulator UI.',failure_warning)
        self.assertNotIn('disconnect does not resume',err)


class QueryDiagnosticTests(unittest.TestCase):
    QUERIES=(('process',('/bin/ps','-p','321','-o','lstart=','-o','uid=','-o','comm=')),
        ('numeric-fds',('/usr/sbin/lsof','-nP','-a','-p','321','-d','0-4096','-F0pcftaiDnsPT','-Ts')),
        ('listener',('/usr/sbin/lsof','-nP','-iTCP:2345','-sTCP:LISTEN','-F0pcftaiDnsPT','-Ts')),
        ('text-owner',('/usr/sbin/lsof','-nP','-a','-p','321','-d','txt','-F0pcftaiDnsPT','-Ts')),
        ('established-socket',('/usr/sbin/lsof','-nP','-a','-p','321','-iTCP:2345','-sTCP:ESTABLISHED','-F0pcftaiDnsPT','-Ts')))

    def run_failure(self,argv,*,code=7,ready=True,chunks=None,spawn_error=False):
        O=load('tools/Observe-SpyroFreshWizardP1.py','p1_query_diagnostic_fakes')
        class Pipe:
            closed=False
            def fileno(self):return 9
            def close(self):self.closed=True
        class Process:
            def __init__(self):self.stdout=Pipe();self.done=False;self.kills=0;self.timeouts=[]
            def wait(self,timeout):self.timeouts.append(timeout);self.done=True;return code
            def poll(self):return code if self.done else None
            def kill(self):self.kills+=1;self.done=True
        class Selector:
            def __enter__(self):return self
            def __exit__(self,*args):pass
            def register(self,*args):pass
            def select(self,timeout):return [object()] if ready else []
        process=Process()
        with patch.object(O.subprocess,'Popen',side_effect=OSError('PRIVATE_OS_OUTPUT') if spawn_error else None,return_value=process) as popen, \
             patch.object(O.selectors,'DefaultSelector',return_value=Selector()), \
             patch.object(O.os,'read',side_effect=chunks if chunks is not None else [b'PRIVATE_OS_OUTPUT',b'']) as read, \
             patch.object(O.time,'monotonic',return_value=0),self.assertRaises(ValueError) as caught:
            O._run(argv)
        self.assertEqual(1,popen.call_count);self.assertEqual(argv,popen.call_args.args[0])
        self.assertFalse(popen.call_args.kwargs['shell']);self.assertEqual(O.subprocess.DEVNULL,popen.call_args.kwargs['stdin'])
        self.assertNotIn('PRIVATE_OS_OUTPUT',str(caught.exception))
        if not spawn_error:
            self.assertTrue(process.stdout.closed);self.assertTrue(process.done)
            self.assertTrue(all(0<t<=2 for t in process.timeouts))
            self.assertTrue(all(0<c.args[1]<=4096 for c in read.call_args_list))
        return str(caught.exception)

    def test_all_five_failed_query_roles_report_exact_exit_without_os_output(self):
        for role,argv in self.QUERIES:
            for code in (1,7,-9):
                with self.subTest(role=role,code=code):
                    self.assertEqual(f'OS query [{role}] failed (exit code {code}).',self.run_failure(argv,code=code))

    def test_query_unavailable_timeout_and_output_bound_retain_role(self):
        for role,argv in self.QUERIES:
            for options,reason in (({'spawn_error':True},'unavailable'),({'ready':False},'timed out'),
                                   ({'chunks':[b'x'*4096]*16+[b'x']},'exceeded output bound')):
                with self.subTest(role=role,reason=reason):
                    self.assertEqual(f'OS query [{role}] {reason}.',self.run_failure(argv,**options))


class SessionTests(unittest.TestCase):
    def setUp(self):self.O=load('tools/Observe-SpyroFreshWizardP1.py','p1_session_fakes')

    def run_fake(self,*,changed=None,probe_error=None,final_failures=False,tree_error=None,http_changed=False,commands=b'p test\nq\n'):
        O=self.O;trace=[];events=[];files={}
        class Owner:
            def __init__(self,name):self.name=name
            def check(self,full=False):
                trace.append(('check',self.name,full))
                if full and final_failures and self.name in ('original_bin','receipt'):raise OSError(self.name+' final failure')
            def close(self):
                trace.append(('close',self.name))
                if final_failures and self.name=='emulator':raise OSError('emulator close failure')
            def report(self):return dict(sha256='synthetic-metadata-not-an-artifact',length=1)
        for name in ('original_bin','original_cue','candidate_bin','candidate_cue','receipt','emulator'):files[name]=Owner(name)
        class Connection:
            def settimeout(self,n):trace.append(('timeout',n))
            def close(self):trace.append(('socket-close',))
        connection=Connection()
        class Client:
            def __init__(self,c):self.connection=c
            def read(self,at,n):
                trace.append(('m',at,n))
                if probe_error:raise probe_error
                return bytes(4) # Initial transport probe need not report level65.
        image=O.FileIdentity(O.CANDIDATE_DIRECTORY+'/'+O.CANDIDATE_BASENAME+'.bin',1,2,661547040)
        exe=O.FileIdentity('/fixture/DuckStation',1,3,123)
        http={} if not http_changed else dict(auxiliary_http_cache=(5,0x1000011,691426,'/Users/john/Library/Application Support/DuckStation/cache/http_cache.bin'),auxiliary_http_cache_size=0)
        process=O.SessionObservation(321,501,'Mon Sep  7 01:02:03 2026',exe.path,image,19,**http)
        counter=0
        def external(pid,owners,connection=None):
            nonlocal counter
            counter+=1;trace.append(('external',counter,connection is not None))
            current=process if counter!=changed else O.SessionObservation(321,501,'Mon Sep  7 01:02:04 2026',exe.path,image,19)
            if http_changed and counter==6:
                current=O.SessionObservation(321,501,process.start_time,exe.path,image,19,
                    auxiliary_http_cache=(6,*process.auxiliary_http_cache[1:]),auxiliary_http_cache_size=0)
            return O._BoundSession(current,exe,7,8 if connection else None,
                                   ('127.0.0.1',50000) if connection else None,
                                   ('127.0.0.1',2345) if connection else None,0)
        def emit(event):trace.append(('emit',event['kind']));events.append(event)
        def capture(client,label):
            trace.append(('payload',));return dict(payload_valid=True,secret='buffered',candidate_authenticated=False,
                external_session_binding_checked=False,runtime_pass=False,ui_result_approved=False,normal_export_approved=False)
        error=None
        with patch.object(O,'PHASE2_READY',True),patch.object(O,'_open_profile_files',side_effect=lambda:files), \
             patch.object(O,'_check_files',side_effect=lambda owners:trace.append(('file-barrier',))), \
             patch.object(O,'_require_tree',side_effect=(tree_error if tree_error else lambda:trace.append(('final-tree',)))), \
             patch.object(O,'_external_check',side_effect=external),patch.object(O.socket,'create_connection',side_effect=lambda *a,**k:(trace.append(('connect',)) or connection)), \
             patch.object(O,'ReadOnlyClient',Client),patch.object(O,'capture',side_effect=capture):
            try:O._run_session(321,O.CANDIDATE_DIRECTORY+'/'+O.CANDIDATE_BASENAME+'.cue',io.BytesIO(commands),emit)
            except BaseException as e:error=e
        self.assertEqual(6,len([t for t in trace if t[0]=='check' and t[2]]))
        self.assertEqual(6,len([t for t in trace if t[0]=='close']))
        return trace,events,error

    def test_readiness_gate_is_inside_session_before_any_io(self):
        with patch.object(self.O,'PHASE2_READY',False), \
             patch.object(self.O,'_open_profile_files',side_effect=AssertionError('No file IO')),self.assertRaises(ValueError):
            self.O._run_session(321,'/not-approved.cue',io.BytesIO(),lambda e:self.fail('No emitted success'))

    def test_fake_production_orchestration_buffers_until_external_postcheck(self):
        trace,events,error=self.run_fake();self.assertIsNone(error)
        self.assertEqual([( 'm',0x8007596C,4)],[t for t in trace if t[0]=='m'])
        self.assertLess(trace.index(('external',3,True)),trace.index(('m',0x8007596C,4)))
        self.assertLess(trace.index(('external',4,True)),trace.index(('emit','connected')))
        self.assertLess(trace.index(('external',6,True)),trace.index(('emit','sample')))
        sample=next(e for e in events if e['kind']=='sample')
        self.assertTrue(sample['external_session_binding_checked']);self.assertFalse(sample['candidate_authenticated'])
        self.assertFalse(sample['runtime_pass']);self.assertFalse(sample['ui_result_approved']);self.assertFalse(sample['normal_export_approved'])
        self.assertEqual(('emit','session_closed'),trace[-1]);self.assertIn(('socket-close',),trace)

    def test_identity_changes_refuse_at_each_boundary_without_sample_or_retry(self):
        for changed,connects,reads,payloads in ((2,0,0,0),(3,1,0,0),(4,1,1,0),(6,1,1,1)):
            with self.subTest(changed=changed):
                trace,events,error=self.run_fake(changed=changed)
                self.assertIsInstance(error,ValueError)
                self.assertEqual(connects,len([t for t in trace if t[0]=='connect']))
                self.assertEqual(reads,len([t for t in trace if t[0]=='m']))
                self.assertEqual(payloads,len([t for t in trace if t[0]=='payload']))
                self.assertFalse(any(e['kind'] in ('sample','session_closed') for e in events))
                self.assertFalse(any('secret' in e for e in events))

    def test_primary_and_all_final_file_failures_survive(self):
        original=OSError('original transport failure')
        trace,events,error=self.run_fake(probe_error=original,final_failures=True)
        def flatten(e):return sum((flatten(x) for x in e.exceptions),[]) if isinstance(e,BaseExceptionGroup) else [e]
        failures=flatten(error);self.assertIs(original,failures[0]);self.assertEqual(4,len(failures))
        self.assertFalse(any(e['kind']=='session_closed' for e in events));self.assertIn(('socket-close',),trace)

    def test_http_owner_postcheck_change_discards_payload_and_finishes_all_owners(self):
        trace,events,error=self.run_fake(http_changed=True)
        self.assertIsInstance(error,ValueError);self.assertEqual(1,trace.count(('payload',)))
        self.assertEqual(1,trace.count(('connect',)));self.assertIn(('socket-close',),trace)
        self.assertFalse(any(e['kind'] in ('sample','session_closed') or 'secret' in e for e in events))
        connected=next(e for e in events if e['kind']=='connected')
        self.assertEqual(5,connected['external_binding']['process']['auxiliary_http_cache'][0])
        self.assertFalse(connected['auxiliary_cache_contents_authenticated'])
        _,failed_events,combined=self.run_fake(http_changed=True,final_failures=True)
        def flatten(e):return sum((flatten(x) for x in e.exceptions),[]) if isinstance(e,BaseExceptionGroup) else [e]
        failures=flatten(combined);self.assertIsInstance(failures[0],ValueError);self.assertEqual(4,len(failures))
        self.assertFalse(any(e['kind'] in ('sample','session_closed') for e in failed_events))

    def test_final_unknown_candidate_owner_refuses_after_all_six_checks_and_closes(self):
        failure=ValueError('Unknown final artifact owner')
        trace,events,error=self.run_fake(tree_error=failure)
        self.assertIs(error,failure);self.assertFalse(any(e['kind']=='session_closed' for e in events))

    def test_bounded_manual_input_no_interpolation_or_auto_sample(self):
        for command in (b'p '+b'x'*513+b'\n',b'c\n',b'p \x00\n',b'p \xff\n',b'p x\r\n'):
            with self.subTest(command=command[:20]):
                trace,events,error=self.run_fake(commands=command);self.assertIsInstance(error,ValueError)
                self.assertNotIn(('payload',),trace);self.assertFalse(any(e['kind']=='sample' for e in events))


class RecordedGrammarTests(unittest.TestCase):
    def setUp(self):self.O=load('tools/Observe-SpyroFreshWizardP1.py','p1_recorded_grammar')
    @staticmethod
    def fields(records):return b''.join(b'\0'.join(k.encode()+str(v).encode() for k,v in row)+b'\0\n' for row in records)

    def test_exact_npolicy_empty_name_does_not_permit_other_empty_records(self):
        header=[('p',321),('c','DuckStation')];row=[('f',17),('a',' '),('t','NPOLICY'),('n','')]
        self.assertEqual('',self.O.parse_lsof(self.fields([header,row]))[0]['n'])
        for bad in ([('f',17),('a','u'),('t','NPOLICY'),('n','')],
                    [('f',17),('a',' '),('t','REG'),('n','')],row+[('D','0x1000011')],
                    [('f',17),('a',' '),('t','NPOLICY')],row+[('n','')]):
            with self.subTest(bad=bad),self.assertRaises(ValueError):self.O.parse_lsof(self.fields([header,bad]))

    def test_exact_shader_auxiliary_owner_not_general_extra_bin_permission(self):
        O=self.O;exe='/Applications/DuckStation.app/Contents/MacOS/DuckStation'
        image=O.FileIdentity('/private/test/'+O.CANDIDATE_BASENAME+'.bin',0x1000011,123,661547040)
        head=[('p',321),('c','DuckStation')]
        image_row=[('f',18),('a','r'),('t','REG'),('D','0x1000011'),('i',123),('s',image.size),('n',image.path)]
        cache=[('f',33),('a','u'),('t','REG'),('D','0x1000011'),('i',1242218),('s',101882),
               ('n','/Users/john/Library/Application Support/DuckStation/cache/vulkan_shaders.bin')]
        def inspect(row):
            replies=iter([b'Mon Sep  7 01:07:43 2026 501 '+exe.encode()+b'\n',self.fields([head,image_row,row]),
                self.fields([head,[('f',34),('a','u'),('t','IPv4'),('P','TCP'),('n','127.0.0.1:2345'),('T','ST=LISTEN')]])])
            return O.inspect_session(321,501,exe,image,runner=lambda _:next(replies))
        first=inspect(cache);self.assertIsNotNone(first.auxiliary_cache)
        with self.assertRaises(ValueError):O.require_same_session(first,inspect([(k,35 if k=='f' else v) for k,v in cache]))
        for key,value in (('a','r'),('a','w'),('t','DIR'),('D','0x1000012'),('i',1242219),('s',-1),
                          ('n','/other/vulkan_shaders.bin'),('n',cache[-1][1]+'.other.bin')):
            with self.subTest(key=key,value=value),self.assertRaises(ValueError):inspect([(k,value if k==key else v) for k,v in cache])
        for key in ('D','i','s'):
            with self.subTest(missing=key),self.assertRaises(ValueError):inspect([(k,v) for k,v in cache if k!=key])
        for size in (0,101883):
            self.assertIsNotNone(inspect([(k,size if k=='s' else v) for k,v in cache]).auxiliary_cache)


class HttpCacheTests(unittest.TestCase):
    PATH='/Users/john/Library/Application Support/DuckStation/cache/http_cache.bin'

    def setUp(self):
        self.O=load('tools/Observe-SpyroFreshWizardP1.py','p1_http_cache_fakes')
        self.exe='/Applications/DuckStation.app/Contents/MacOS/DuckStation'
        self.image=self.O.FileIdentity('/private/test/'+self.O.CANDIDATE_BASENAME+'.bin',0x1000011,123,661547040)
        self.head=[('p',321),('c','DuckStation')]
        self.image_row=[('f',18),('a','r'),('t','REG'),('D','0x1000011'),('i',123),('s',661547040),('n',self.image.path)]
        self.shader=[('f',33),('a','u'),('t','REG'),('D','0x1000011'),('i',1242218),('s',101882),('n',self.O.SHADER_CACHE_PATH)]
        self.http=[('f',5),('a','u'),('t','REG'),('D','0x1000011'),('i',691426),('s',0),('n',self.PATH)]

    def inspect(self,rows):
        f=RecordedGrammarTests.fields
        replies=iter([b'Mon Sep  7 01:02:03 2026 501 '+self.exe.encode()+b'\n',f([self.head,self.image_row,*rows]),
            f([self.head,[('f',34),('t','IPv4'),('P','TCP'),('n','127.0.0.1:2345'),('T','ST=LISTEN')]])])
        return self.O.inspect_session(321,501,self.exe,self.image,runner=lambda _:next(replies))

    @staticmethod
    def changed(row,key,value):return [(k,value if k==key else v) for k,v in row]

    def test_both_exact_caches_keep_independent_identities_and_existing_shader_fields(self):
        O=self.O;before=self.inspect([self.shader]);both=self.inspect([self.http,self.shader])
        self.assertEqual((33,0x1000011,1242218,O.SHADER_CACHE_PATH),both.auxiliary_cache)
        self.assertEqual(before.auxiliary_cache,both.auxiliary_cache);self.assertEqual(101882,both.auxiliary_cache_size)
        self.assertEqual((5,0x1000011,691426,self.PATH),both.auxiliary_http_cache)
        self.assertEqual(0,both.auxiliary_http_cache_size);self.assertFalse(both.candidate_authenticated)
        for key,value in vars(before).items():
            if key not in ('auxiliary_http_cache','auxiliary_http_cache_size'):self.assertEqual(value,vars(both)[key],key)
        self.assertIsNone(before.auxiliary_http_cache);self.assertIsNone(before.auxiliary_http_cache_size)
        neither=self.inspect([]);self.assertIsNone(neither.auxiliary_cache);self.assertIsNone(neither.auxiliary_http_cache)
        O.require_same_session(both,self.inspect([self.shader,self.http]))

    def test_only_exact_http_owner_is_excluded_from_additional_images(self):
        for key,value in (('f',18),('a','r'),('a','w'),('t','DIR'),('D','0x1000012'),('i',691427),
                          ('s',-1),('s','x'),('s','+0'),('s','0.0'),('n','/other/http_cache.bin'),
                          ('n',self.PATH.replace('http_cache','HTTP_CACHE')),('n',self.PATH+'.other.bin'),
                          ('n',self.PATH[:-4]+'.cue'),('n',self.PATH[:-4]+'.iso'),('n',self.PATH[:-4]+'.chd')):
            with self.subTest(key=key,value=value),self.assertRaises(ValueError):
                self.inspect([self.shader,self.changed(self.http,key,value)])
        for key in ('a','t','D','i','s'):
            with self.subTest(missing=key),self.assertRaises(ValueError):self.inspect([self.shader,[(k,v) for k,v in self.http if k!=key]])
        for extra in (self.http,self.changed(self.http,'f',6),self.changed(self.changed(self.http,'f',6),'n','/other/another.bin')):
            with self.subTest(extra=extra),self.assertRaises(ValueError):self.inspect([self.shader,self.http,extra])
        for old,new in (('a','u'),('i',124),('s',661547039),('n','/other/candidate.bin')):
            with self.subTest(candidate=old),self.assertRaises(ValueError):
                saved=self.image_row;self.image_row=self.changed(saved,old,new)
                try:self.inspect([self.shader,self.http])
                finally:self.image_row=saved

    def test_each_cache_presence_fd_and_identity_are_stable_but_sizes_are_descriptive(self):
        O=self.O;both=self.inspect([self.shader,self.http])
        for rows in ([],[self.shader],[self.http],[self.changed(self.shader,'f',32),self.http],
                     [self.shader,self.changed(self.http,'f',6)]):
            with self.subTest(rows=rows),self.assertRaises(ValueError):O.require_same_session(both,self.inspect(rows))
        for row in (self.shader,self.http):
            for key,value in (('D','0x1000012'),('i',999),('n','/other/extra.bin')):
                rows=[self.changed(x,key,value) if x is row else x for x in (self.shader,self.http)]
                with self.subTest(row=row[-1],key=key),self.assertRaises(ValueError):self.inspect(rows)
        for size in (0,1,999999):
            changed=self.inspect([self.changed(self.shader,'s',size),self.changed(self.http,'s',size)])
            O.require_same_session(both,changed);self.assertEqual(size,changed.auxiliary_http_cache_size)


class SourceGates(unittest.TestCase):
    def test_recorded_http_numeric_fixture_with_synthetic_process_listener_only(self):
        if not OS_FIXTURES:self.skipTest('Explicit --os-fixtures required; no default recorded/cache/process reads.')
        path=ROOT/'_local/native-fresh-wizard-p1-runtime-20260907-r02.luV04T/after-settings-numeric-fds.bin'
        raw=path.read_bytes();self.assertEqual(2088,len(raw))
        self.assertEqual('f9f5a48bcfb8581023c64b0c9d0ad7820e78c45fbf3cd9f9f2a283208e319bf0',hashlib.sha256(raw).hexdigest())
        O=load('tools/Observe-SpyroFreshWizardP1.py','p1_recorded_http_only')
        image=O.FileIdentity(O.CANDIDATE_DIRECTORY+'/'+O.CANDIDATE_BASENAME+'.bin',0x1000011,44198621,661547040)
        exe='/Applications/DuckStation.app/Contents/MacOS/DuckStation'
        # Only the numeric FD bytes above are actual recorded evidence. These
        # ps/listener replies are synthetic parser inputs, not a live identity.
        replies=iter([b'Mon Sep  7 01:02:03 2026 501 '+exe.encode()+b'\n',raw,
            RecordedGrammarTests.fields([[('p',42862),('c','DuckStation')],[('f',34),('t','IPv4'),('P','TCP'),('n','127.0.0.1:2345'),('T','ST=LISTEN')]])])
        observed=O.inspect_session(42862,501,exe,image,runner=lambda _:next(replies))
        self.assertEqual(18,observed.bin_fd);self.assertFalse(observed.candidate_authenticated)
        self.assertEqual((5,0x1000011,691426,HttpCacheTests.PATH),observed.auxiliary_http_cache)
        self.assertEqual(0,observed.auxiliary_http_cache_size)
        self.assertEqual((33,0x1000011,1242218,O.SHADER_CACHE_PATH),observed.auxiliary_cache)
        self.assertEqual(101882,observed.auxiliary_cache_size)

    def test_original_health_motion_operands_and_complete_vector_copy(self):
        if SOURCE is None:self.skipTest('Explicit --source required; no default original reads.')
        path=ROOT/'src/Spyro.Editor.NativeWizardFamilyAssetSmoke/source_fresh_wizard_audio_audit.py'
        digest='7fe4e4e65d1552420bb48778a274e25afdf65005d55e8590125d2f84db23f532'
        self.assertEqual(digest,hashlib.sha256(path.read_bytes()).hexdigest())
        raw=load(str(path),'p1_health_source_reader');O=load('tools/Observe-SpyroFreshWizardP1.py','p1_health_source_fields')
        with open(SOURCE,'rb') as image:
            initial=os.fstat(image.fileno())
            def identity():
                raw.identity(image)
                for state in (os.fstat(image.fileno()),os.stat(SOURCE)):
                    self.assertEqual((initial.st_dev,initial.st_ino,661547040,initial.st_mtime_ns,initial.st_ctime_ns),
                                     (state.st_dev,state.st_ino,state.st_size,state.st_mtime_ns,state.st_ctime_ns))
            identity()
            try:
                offset,length,base,exe_sha=raw.SOURCES['exe'];exe=raw.logical(image,offset,length)
                self.assertEqual(exe_sha,hashlib.sha256(exe).hexdigest())
                pins=((0x800427BC,0xB4,'33edd323404e3bdb4e79ed42222ce965b4eb162366aa2c3ecc53a7ab1d7c6ffd'),
                      (0x800416AC,0x70,'c8378f043a7c3e803d4fe09b3fbf6fee83f511c65b92cb2b34f252862d46c6a2'),
                      (0x8003EE9C,0x50,'0c24e672f1bb410708d70088bda3f7c1b2dc14585b88e6a370788728617af821'),
                      (0x8003F8D4,0x18,'17853e6852a3ec63f795961bc1715ea3f84585bcd46ba9c2a6c7a1552f745933'),
                      (0x80017700,0x28,'7c1af3c5556925409e78330cd5a9bfa66cdf2a0ef040af9aa3f7d8a062aca56e'))
                for at,n,sha in pins:self.assertEqual(sha,hashlib.sha256(exe[at-base:at-base+n]).hexdigest())
                def words(at,expected):
                    actual=struct.unpack_from('<'+'I'*len(expected),exe,at-base)
                    self.assertEqual(tuple(expected),actual,hex(at));return actual
                def absolute(hi,low):
                    self.assertEqual(15,hi>>26)
                    return (((hi&65535)<<16)+((low&32767)-(low&32768)))&0xFFFFFFFF
                # Actual LW plus signed BGEZ/SLTI consumers; delay words retained.
                health=words(0x800427EC,[0x3C028008,0x8C428BBC,0,0x04410357,0])
                damage=words(0x800427BC,[0x3C028008,0x8C428A84,0,0x30420020])
                state_a=words(0x800416D8,[0x3C028008,0x8C428AF4,0,0x28420004])
                state_b=words(0x800416F0,[0x3C028008,0x8C428B08,0,0x28420017])
                for seq in (health,damage,state_a,state_b):self.assertEqual((35,2,2),(seq[1]>>26,seq[1]>>21&31,seq[1]>>16&31))
                # Actual call arguments are based on substate78AD4, not a seeded
                # guessed motion pointer. The JAL delay clears substate.
                args=words(0x8003EE9C,[0x3C028008,0x24428AD4,0x24440078,0x24450090,0x0C005DC0,0xAC400000])
                vector_base=absolute(args[0],args[1]);dest=vector_base+(args[2]&65535);source=vector_base+(args[3]&65535)
                self.assertEqual(0x80017700,((0x8003EEB4)&0xF0000000)|((args[4]&0x3FFFFFF)<<2))
                copy=words(0x80017700,[0x8CA10000,0x8CA20004,0x8CA30008,0xAC810000,0xAC820004,0x03E00008,0xAC830008])
                self.assertEqual([(35,5,i+1,i*4) for i in range(3)],[(w>>26,w>>21&31,w>>16&31,w&65535) for w in copy[:3]])
                self.assertEqual([(43,4,i+1,i*4) for i in range(3)],[(w>>26,w>>21&31,w>>16&31,w&65535) for w in (copy[3],copy[4],copy[6])])
                # State6 selected negative initialization reaches the actual SW.
                branch=words(0x8003EEC0,[0x14400284,0x2402FF40])
                self.assertEqual(0x8003F8D4,0x8003EEC4+((branch[0]&65535)<<2))
                self.assertEqual(-192,(branch[1]&32767)-(branch[1]&32768))
                scalar=words(0x8003F8D4,[0x3C018008,0xAC228BA4])
                expected={'player_health_s32':(absolute(*health[:2]),'<i'),
                    'player_damage_accumulator_u32':(absolute(*damage[:2]),'<I'),
                    'player_motion_78b4c_s32x3':(dest,'<iii'),'player_motion_78b64_s32x3':(source,'<iii'),
                    'player_motion_78ba4_s32':(absolute(*scalar),'<i'),
                    'player_state_input_78af4_s32':(absolute(*state_a[:2]),'<i'),
                    'player_state_input_78b08_s32':(absolute(*state_b[:2]),'<i')}
                self.assertEqual(expected,O.HEALTH_MOTION_FIELDS)
                for name,(at,fmt) in expected.items():
                    self.assertEqual((at,fmt),O.FIELDS[name]);self.assertIn(name,O.REREAD)
                # Instruction/operand proof only: no GTE/audio/state helper is
                # executed or assigned a fabricated outcome in this test.
            finally:
                identity();self.assertEqual(digest,hashlib.sha256(path.read_bytes()).hexdigest())

    def test_root_reviewed_recorded_os_fixtures_not_live_process(self):
        if not OS_FIXTURES:self.skipTest('Explicit --os-fixtures required; recorded files only, never live queries.')
        O=load('tools/Observe-SpyroFreshWizardP1.py','p1_recorded_os_only')
        folder=ROOT/'_local/native-fresh-wizard-p1-runtime-20260907-r01.aaM1NY'
        pins={'process-before.txt':'fbe7be12748b4beb8cf0b015135fb9a5374d0ca1625a19e638c6c7fed86d4128',
              'files-before.raw':'c1ea1857570361644277a68066c83b6d802945c7fe98ba3251382d01e4fc149c',
              'listener-before.raw':'50e1ac29691de90a3e3bf4845a57b25a4413c1bdc8afc9e3570c358c5873931a',
              'text-before.raw':'6e2ccda34523781914867ba72085e0e16f3889b85fc1f1e10e428b7b474f3f6c'}
        raw={name:(folder/('os-'+name)).read_bytes() for name in pins}
        for name,digest in pins.items():self.assertEqual(digest,hashlib.sha256(raw[name]).hexdigest())
        image=O.FileIdentity(str(ROOT/'_local/research/dark-wizard-native-closure-20260906/fresh-wizard-publication-validation-r01.0eF4Tz/candidate-r01/native-id65-fresh-wizard-p1-n32-r01.bin'),0x1000011,44198621,661547040)
        executable=O.FileIdentity('/Applications/DuckStation.app/Contents/MacOS/DuckStation',0x1000011,41889985,22731840)
        replies=iter(raw[name] for name in ('process-before.txt','files-before.raw','listener-before.raw'))
        observed=O.inspect_session(38592,501,executable.path,image,runner=lambda _:next(replies))
        self.assertEqual(18,observed.bin_fd);self.assertFalse(observed.candidate_authenticated)
        self.assertEqual((executable,87),O.require_text_owner(raw['text-before.raw'],38592,executable))

    def test_actual_file_gate_has_no_caller_profile_or_hash_authority(self):
        O=load('tools/Observe-SpyroFreshWizardP1.py','p1_closed_file_gate_shape')
        self.assertEqual((),tuple(inspect.signature(O.verify_actual_files).parameters))
        self.assertIs(type(O.PHASE2_READY),bool)

    def test_reviewed_actual_six_file_gate(self):
        if not ACTUAL_FILES:self.skipTest('Explicit --actual-files required; no default candidate/executable reads.')
        O=load('tools/Observe-SpyroFreshWizardP1.py','p1_actual_file_gate')
        result=O.verify_actual_files()
        self.assertEqual(6,len(result['owners']));self.assertTrue(result['final_full_checks_passed'])
        self.assertFalse(result['runtime_pass']);self.assertFalse(result['candidate_authenticated'])
        self.assertEqual('af41aa8b0af10504c626f76cbaa2446e2207486551ce6ba8766794261a63b63e',
                         result['owners']['receipt']['sha256'])
        print('FRESH_WIZARD_P1_ACTUAL_FILES_OK '+json.dumps(result,sort_keys=True),flush=True)

    def test_owner_arithmetic_does_not_certify_runtime(self):
        self.assertEqual(0x800A62B8-0x800A5460,32*0x70+0x58)
        self.assertEqual(0x800A5F60,0x800A5460+32*88)
        self.assertEqual(0x800A5FB8,0x800A62B8-32*24)
        self.assertEqual(0x801C5BB8,0x801C3BB8+256*32)

    def test_complete_original_native_gates(self):
        if SOURCE is None:self.skipTest('Explicit --source required; no default original source reads.')
        self.assertEqual(BODY_SHA,hashlib.sha256((ROOT/BODY).read_bytes()).hexdigest())
        raw=load(BODY,'p1_observer_body_raw');raw.pins()
        O=load('tools/Observe-SpyroFreshWizardP1.py','p1_observer_source_production')
        with open(SOURCE,'rb') as image:
            before=os.fstat(image.fileno())
            self.assertEqual(661547040,before.st_size)
            def identity():
                self.assertEqual((before.st_dev,before.st_ino,before.st_size),
                    (os.fstat(image.fileno()).st_dev,os.fstat(image.fileno()).st_ino,os.fstat(image.fileno()).st_size))
                image.seek(0);self.assertEqual(IMAGE_SHA,hashlib.file_digest(image,'sha256').hexdigest())
                self.assertEqual(661547040,image.tell());self.assertEqual(b'',image.read(1))
                self.assertEqual(661547040,image.tell());self.assertEqual(661547040,os.fstat(image.fileno()).st_size)
                pathstat=os.stat(SOURCE)
                self.assertEqual((before.st_dev,before.st_ino,before.st_size),(pathstat.st_dev,pathstat.st_ino,pathstat.st_size))
            identity()
            try:
                # Capture the genuine native machine returned by the unchanged
                # raw helper factory. This wrapper adds no expected globals or
                # altered instructions/returns/payloads; the historical shared
                # root0 seed is explicitly excluded below. Restore in finally.
                modules={p:raw.load(p,'observer_actual_'+str(i)) for i,p in enumerate(raw.HELPERS)}
                resource=modules[next(iter(raw.HELPERS))]
                native_path=list(resource.HELPERS)[3]
                native=resource.load(native_path,'observer_actual_native')
                old_load,old_resource_load,old_machine=raw.load,resource.load,native.globals_machine
                machines=[]
                def retain_machine(code):
                    m=old_machine(code);machines.append(m);return m
                raw.load=lambda p,n:modules[p] if p in modules else old_load(p,n)
                resource.load=lambda p,n:native if p==native_path else old_resource_load(p,n)
                native.globals_machine=retain_machine
                out=io.StringIO()
                try:
                    with contextlib.redirect_stdout(out):raw.main(SOURCE,False)
                finally:raw.load,resource.load,native.globals_machine=old_load,old_resource_load,old_machine
                report=json.loads(out.getvalue())
                self.assertEqual(567,report['checks']);self.assertFalse(report['placedInitializerExecuted'])
                byaddress={at:value for at,value in O.GATES.values()}
                for key,at in [('copyAddress',0x800785D8),('modelAddress',0x800785E0),('sceneAddress',0x800785E4)]:
                    self.assertEqual(int(report[key],16),byaddress[at])
                scene=int(report['sceneAddress'],16)
                self.assertEqual(scene+0xD4,byaddress[0x80075828]);self.assertEqual(scene+0x12C,byaddress[0x80075890])
                self.assertEqual(scene+0xF84,byaddress[0x800756E8])
                for i,((row,n),(private,k)) in enumerate(report['nativeAllocationOwners']):
                    self.assertEqual((scene+0x12C+i*88,88,scene+0xF84-(i+1)*24,24),(row,n,private,k))
                actual=[m for m in machines if m.read(0x800785E4)==scene and m.read(0x800756A4)==32]
                self.assertEqual(1,len(actual));actual=actual[0]
                # The old combined oracle seeds root0 at its historical synthetic
                # PETE witness+800. It cannot certify the real shared root leaf.
                # Six LOCAL roots are genuinely produced by this native machine;
                # shared root0 is independently derived below through its helper.
                leaves=[0x80076378+cls*4 for cls,_ in raw.ROOTS if cls!=0]+[
                    0x800785E0,0x800785E4,0x80078600,0x80078604,0x80075828,0x80075890,0x800756E8,0x800756A8,0x80075778]
                def require_actual_leaves(gates):
                    for at in leaves:self.assertEqual(actual.read(at),gates[at],hex(at))
                require_actual_leaves(byaddress)
                self.assertEqual(0x000A3B34,actual.read(0x800763B0)) # Real simple-model native low-pointer result.
                for at,bad in ((0x800763B0,0x800A3B34),(0x80075778,0x8009A7D0),
                               (0x80076558,0x10A4C),(0x80075828,0xD4)):
                    wrong=dict(byaddress);wrong[at]=bad
                    with self.assertRaises(AssertionError):require_actual_leaves(wrong)
                linked=raw.load(list(raw.HELPERS)[4],'p1_observer_linked')
                h=linked.load(next(iter(linked.PINS)),'p1_observer_host_raw')
                selector=linked.load(list(linked.PINS)[1],'p1_observer_selector')
                domains,_,_=h.capture(SOURCE)
                host=linked.compose(h,domains['town'],domains['wizard'],domains['exe'])
                init=selector.prototype(domains['exe'][0x4B098:0x4B130],host['copy'])
                for at,b in O.CODE_WINDOWS:
                    source_bytes=init if at==0x8005A898 else host['bytes'][at-linked.BASE:at-linked.BASE+len(b)]
                    self.assertEqual(b,source_bytes,hex(at))
                self.assertEqual('ad6d952cbf70ab8b1eab53f7637ffd0235271b7b20696956c9406f36f714fa7e',hashlib.sha256(host['bytes']).hexdigest())
                exe=domains['exe'];p1=raw.load(list(raw.HELPERS)[1],'p1_observer_scene_raw');scalar=p1.helper()
                header=scalar.native.logical(image,37*2048+0x7B8800,0x800)
                shared_payload=scalar.native.logical(image,37*2048+0x7B9000,0x39C48)
                self.assertEqual('b9a4de193b5cf0bd9592de15c57aa5daa3713c3575026154b45402f3aceef6f1',hashlib.sha256(header).hexdigest())
                self.assertEqual('1703ac37e59e480909b6f66dc856d4e5f3ce80551f18cbc8c06129c3b54dd35d',hashlib.sha256(shared_payload).hexdigest())
                self.assertEqual((0x39C48,0x800,0),struct.unpack_from('<III',header))
                shared_at,shared_n=0x8005B7D8,0xD4
                shared_code=exe[shared_at-0x8000F800:shared_at-0x8000F800+shared_n]
                self.assertEqual('f5f4ea14f28a2cfbab256546d7c4a41f9e796b62b152d84252d254b3d6cf1b67',hashlib.sha256(shared_code).hexdigest())
                code=dict(actual.code);code.update({shared_at+i:struct.unpack_from('<I',shared_code,i)[0] for i in range(0,shared_n,4)})
                shared=scalar.Machine(code);shared.map(0x80075000,0x4000);shared.map(0x801FF800,0x800);shared.r[29]=0x801FFF00
                shared.blob(0x80100000,header)
                shared.poke(0x800755A4,struct.unpack_from('<I',exe,0x65DA4)[0])
                shared.run(0x8005B7D8,{0x8005B7F0}) # Actual s2=0/s3=class-table prologue, not a seeded table address.
                self.assertEqual((0,0x80076378),(shared.r[18],shared.r[19]))
                # Explicit completed-header/CD-phase entry witness. No CD helper
                # return is fabricated; execute actual allocation subtraction,
                # then deliver the complete pinned source payload at that result.
                shared.r[16]=0x80100000
                shared.run(0x8005B814,{0x8005B818})
                shared.run(0x8005B848,{0x8005B864})
                pete_base=shared.read(0x800785FC)
                self.assertEqual(0x801C5BB8,pete_base)
                shared.blob(pete_base,shared_payload)
                self.assertEqual(0,shared.read(0x80076378)) # No expected root0 seeded.
                shared.run(0x8005B870,{0x8005B8AC}) # Real 133E0 relocation, return, class0 SW and all delays.
                self.assertEqual([(0x8005B890,0x800133E0)],shared.calls)
                self.assertEqual(pete_base,shared.read(0x80076378))
                self.assertEqual(byaddress[0x80076378],shared.read(0x80076378))
                self.assertEqual(byaddress[0x800785FC],pete_base)
                self.assertNotEqual(actual.read(0x80076378),shared.read(0x80076378),
                                    'Historical local-loader seed is not actual shared-registration evidence')
                for bad in (0x801C63B8,0x8008C334,0x800):
                    with self.assertRaises(AssertionError):self.assertEqual(shared.read(0x80076378),bad)
                self.assertTrue(all(pc in actual.code or shared_at<=pc<shared_at+shared_n for pc in shared.executed))
                initcode={0x8005A898+i:struct.unpack_from('<I',init,i)[0] for i in range(0,len(init),4)}
                initcode.update({pc:struct.unpack_from('<I',exe,pc-0x8000F800)[0] for pc in range(0x8005B6E0,0x8005B6F8,4)})
                initialized=scalar.Machine(initcode);initialized.map(0x80075000,0x4000)
                initialized.poke(0x8007596C,65);initialized.r[31]=0xDEADBEE0
                initialized.run(0x8005A898,{0xDEADBEE0}) # Real tail/JR/delay; declared synthetic return boundary.
                for name,(at,value) in O.GATES.items():
                    if name.startswith('callback') or name in ('copy_anchor','copy_cursor'):
                        self.assertEqual(initialized.read(at),value,name)
                self.assertTrue(all(pc in initcode for pc in initialized.executed))
                self.assertEqual('a0b8c706c22202f8f5e7b127b53da4ab17be1e16f104b773fc137e9ff2bade42',hashlib.sha256(exe[0x5B6D0-0xF800:0x5B734-0xF800]).hexdigest())
                spans=((0x8005B6F8,0x1C),(0x8001441C,0x74))
                code={at+i:struct.unpack_from('<I',exe,at-0x8000F800+i)[0] for at,n in spans for i in range(0,n,4)}
                def machine(mapped_pete=True):
                    m=scalar.Machine(code);m.map(0x80075000,0x4000);m.map(0x801F7F00,0x100);m.r[29]=0x801F8000
                    pete=0x80200000-struct.unpack_from('<I',exe,0x65DA4)[0]-0x39C48
                    m.map(pete-0x2000,0x2004,0xA5)
                    if mapped_pete:m.poke(0x800785FC,pete)
                    else:
                        for i in range(4):del m.memory[0x800785FC+i]
                    return m,pete
                m,pete=machine();m.r[2]=0xDEADBEEF;m.r[3]=0xCAFEBABE
                m.run(0x8005B6F8,{0x8005B714});self.assertEqual(pete-0x2000,m.read(0x800785F8))
                m.run(0x8001441C,{0x80058B68})
                for at in (0x800785F8,0x80075824,0x80075738):self.assertEqual(0x801C3BB8,m.read(at))
                self.assertEqual(0xFFFFFFFF,m.read(pete));self.assertEqual(255,m.read(pete-0x1FFF,1))
                self.assertTrue(all(any(at<=pc<at+n for at,n in spans) for pc in m.executed))
                missing,_=machine(False)
                with self.assertRaises(AssertionError):missing.run(0x8005B6F8,{0x8005B714})
                h.require_helpers();scalar.require_helpers()
                for path,digest in linked.PINS.items():self.assertEqual(digest,hashlib.sha256((ROOT/path).read_bytes()).hexdigest())
            finally:
                identity();raw.pins();self.assertEqual(BODY_SHA,hashlib.sha256((ROOT/BODY).read_bytes()).hexdigest())


if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--source');parser.add_argument('--actual-files',action='store_true');parser.add_argument('--os-fixtures',action='store_true')
    args,rest=parser.parse_known_args();SOURCE=args.source;ACTUAL_FILES=args.actual_files;OS_FIXTURES=args.os_fixtures
    unittest.main(argv=[sys.argv[0],*rest])
