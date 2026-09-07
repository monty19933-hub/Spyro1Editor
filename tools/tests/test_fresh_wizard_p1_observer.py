"""P1 observer tests first. Fake memory/process/socket only; no live connection."""
import importlib.util
import dataclasses
import hashlib
import io
import os
from pathlib import Path
import socket
import stat
import struct
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.dont_write_bytecode = True
TOOLS = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location("p1_observer", TOOLS / "Observe-SpyroFreshWizardP1.py")
O = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = O
with patch.object(socket, "create_connection", side_effect=AssertionError("No live socket")), \
     patch.object(subprocess, "Popen", side_effect=AssertionError("No live process query")):
    spec.loader.exec_module(O)  # Intentional first RED: missing new observer.

PLACED, BASE, RESERVE, PRIVATE, END = 0x800A5408, 0x800A5460, 0x800A5F60, 0x800A5FB8, 0x800A62B8
WPRIVATE, POOL, PETE = 0x800A62BC, 0x801C3BB8, 0x801C5BB8
LITERALS = {
    0x8007596C:65, 0x800757D8:0, 0x80075690:0, 0x80078CA4:0,
    0x8007595C:5, 0x80075964:35, 0x80078764:0, 0x80078768:0,
    0x800785D8:0x8008BA38, 0x800785DC:0x8008BA38, 0x800785E0:0x8008C334,
    0x800785E4:0x800A5334, 0x80078600:0x100000, 0x80078604:0x3000,
    0x80075828:PLACED, 0x80075890:BASE, 0x800756E8:END, 0x80075778:0x800A6318,
    0x800756A8:32, 0x800785FC:PETE, 0x800785F8:POOL, 0x80075824:POOL,
    0x80076378:PETE, 0x80076558:0x8009CD80, 0x80076410:0x8009F950,
    0x800767E4:0x8009FEB4, 0x800763B0:0xA3B34, 0x800763B4:0x800A4320,
    0x800763B8:0x800A4AAC, 0x800758CC:0x800872A4, 0x80075734:0x8007DA54,
    0x800758E4:0x80088F68, 0x800756BC:0x80087E20, 0x800757A0:0x8007AF2C,
    0x800758A8:0x8007B068, 0x8007574C:0x8007B0AC, 0x800758D8:0x8007D04C,
    0x800114A8:0x8005A898,
}


def row(cls=0x78, status=0, props=0xDEADBEEF):
    b = bytearray(88); struct.pack_into("<I", b, 0, props)
    struct.pack_into("<H", b, 0x36, cls); b[0x48] = status
    return bytes(b)


class Memory:
    def __init__(self): self.data, self.reads, self.flip = {}, [], {}
    def blob(self, at, b): self.data.update((at+i, v) for i, v in enumerate(b))
    def word(self, at, value): self.blob(at, struct.pack("<I", value))
    def read(self, at, n):
        assert type(at) is int and type(n) is int and 0 < n <= 1024
        self.reads.append((at, n))
        if self.reads.count((at, n)) == 2 and at in self.flip:
            self.data[at] ^= self.flip[at]
        try: return bytes(self.data[at+i] for i in range(n))
        except KeyError as e: raise AssertionError(f"Unowned fixture read {at:08X}+{n:X}") from e


def fixture():
    m = Memory()
    for at, value in LITERALS.items(): m.word(at, value)
    for at, fmt in O.REPORT_FIELDS.values(): m.blob(at, bytes(struct.calcsize(fmt)))
    for at, value in {0x800756A4:1, 0x80075930:END-24, 0x8007573C:BASE+88,
                      0x80075898:BASE, 0x80075738:POOL, 0x800757B0:0x80188000,
                      0x80075780:0x80189000, 0x80075820:0x80188000, 0x80078AD0:12}.items(): m.word(at, value)
    m.blob(0x80078A58, struct.pack("<iii", 8960,8960,9061)); m.blob(0x80078A66,b"\xff")
    m.blob(PLACED,row(0x11B,7,WPRIVATE)); m.blob(WPRIVATE,bytes(80)); m.word(WPRIVATE,WPRIVATE+0x28)
    m.blob(BASE,row()+row(status=0xFF)+bytes(30*88)); m.blob(RESERVE,bytes(88))
    m.blob(PRIVATE,bytes(0x300)); m.blob(POOL,bytes(0x2000)); m.blob(POOL+1,b"\xff"); m.word(PETE,0xFFFFFFFF)
    # Neutral positive code fixture. Explicit source tests independently rebuild
    # ALL these emitted bytes, rather than treating this copy as source proof.
    for at, b in O.CODE_WINDOWS: m.blob(at,b)
    return m


class RamTests(unittest.TestCase):
    def test_literal_gate_addresses_and_no_live_authority(self):
        self.assertEqual(LITERALS, {at:value for at,value in O.GATES.values()})
        m=fixture(); r=O.capture(m,"x"*200)
        self.assertTrue(r["payload_valid"]); self.assertFalse(r["candidate_authenticated"])
        self.assertFalse(r["external_session_binding_checked"]); self.assertFalse(r["runtime_pass"])
        self.assertEqual(120,len(r["label"])); self.assertEqual(12,r["player_state"])
        self.assertEqual(255,r["player_heading_u8"]); self.assertEqual(0,r["wizard"]["index"])
        self.assertEqual(1,r["dynamic_rows"]["active"][0]["index"])
        self.assertEqual("outside-owned-private-grid",r["dynamic_rows"]["active"][0]["private_pointer_classification"])
        self.assertNotIn((0xDEADBEEF,24),m.reads)
        self.assertEqual(0x300,len(bytes.fromhex(r["raw_dynamic_private_block_hex"])))

    def test_every_gate_refuses_before_heap(self):
        for at in LITERALS:
            with self.subTest(at=hex(at)):
                m=fixture();m.word(at,LITERALS[at]^1);r=O.capture(m,"bad")
                self.assertFalse(r["payload_valid"])
                self.assertFalse(any(PLACED <= a < END+0x100 or POOL <= a <= PETE for a,n in m.reads))

    def test_each_code_window_mutation_refuses_before_heap(self):
        for at,b in O.CODE_WINDOWS:
            for offset in (0,len(b)-1):
                with self.subTest(at=hex(at),offset=offset):
                    m=fixture();m.data[at+offset]^=1;r=O.capture(m,"code")
                    self.assertFalse(r["payload_valid"]);self.assertNotIn((PLACED,88),m.reads)

    def test_cursors_counts_and_companion_bounds(self):
        bad={0x800756A4:[0,33],0x80075930:[PRIVATE-24,END+24,PRIVATE+1],
             0x8007573C:[BASE-88,RESERVE+88,BASE+1],0x80075898:[PLACED,RESERVE,BASE+1],
             0x80075738:[POOL-32,PETE+32,POOL+1]}
        for at,values in bad.items():
            for value in values:
                with self.subTest(at=hex(at),value=value):
                    m=fixture();m.word(at,value);self.assertFalse(O.capture(m,"bad")["payload_valid"])
                    self.assertNotIn((POOL,1024),m.reads)

    def test_raw_loader_private_and_companion_refuse(self):
        for pointer in (0,0xF88,WPRIVATE+4,0x801410B8):
            m=fixture();m.word(PLACED,pointer);self.assertFalse(O.capture(m,"")["payload_valid"])
            self.assertNotIn((BASE,88),m.reads)
        for at,value in ((WPRIVATE,0xFB0),(WPRIVATE,WPRIVATE+4)):
            m=fixture();m.word(at,value);self.assertFalse(O.capture(m,"")["payload_valid"])
        for b in (row(0x26),row(status=0x80),row(status=0xFF)):
            m=fixture();m.blob(BASE,b);self.assertFalse(O.capture(m,"")["payload_valid"])
            self.assertNotIn((POOL,1024),m.reads)
        m=fixture();m.word(0x80075898,BASE+176);m.blob(BASE+176,row())
        self.assertFalse(O.capture(m,'companion beyond native terminal')['payload_valid'])
        self.assertNotIn((POOL,1024),m.reads)

    def test_full32_reserve_backward_owners_and_all256(self):
        m=fixture();m.word(0x800756A4,32);m.word(0x8007573C,RESERVE);m.word(0x80075930,PRIVATE)
        for i in range(32):m.blob(BASE+i*88,row(0x78 if i==0 else 0x26,props=END-(i+1)*24))
        m.blob(RESERVE+0x48,b"\xff")
        for i in range(256):m.blob(POOL+i*32,bytes([0x47,1])+bytes(30))
        r=O.capture(m,"full")
        self.assertTrue(r["payload_valid"]);self.assertEqual(32,len(r["dynamic_rows"]["active"]))
        self.assertTrue(r["dynamic_rows"]["no_in_range_terminal"])
        self.assertEqual(255,r["reserve_terminal_u8_not_actor33"])
        self.assertEqual(256,r["particles"]["active_count"])
        self.assertNotIn((RESERVE,88),m.reads);self.assertNotIn((PETE,32),m.reads)
        self.assertEqual(2,m.reads.count((RESERVE+0x48,1)))
        self.assertEqual(2,m.reads.count((PETE,4)))
        self.assertTrue(all(r["dynamic_rows"]["active"][i]["private_pointer_classification"]=="owned-grid" for i in range(32)))

    def test_holes_and_reused_properties_are_not_cursor_count_equalities(self):
        m=fixture();m.blob(BASE+88,row(0x26,0x80));m.blob(BASE+176,row(0x26,0,PRIVATE));m.blob(BASE+264,row(status=0xFF))
        m.word(0x800756A4,2);m.word(0x8007573C,BASE+88);m.word(0x80075930,END-24)
        r=O.capture(m,"");self.assertTrue(r["payload_valid"])
        self.assertEqual([1,3],[x["index"] for x in r["dynamic_rows"]["active"]])
        self.assertEqual(3,r["dynamic_rows"]["terminal_slot"])

    def test_postpayload_rereads_withhold_interpretations(self):
        addresses=list(LITERALS)+[0x800756A4,0x80075930,0x8007573C,0x80075898,0x80075738,
                                  PLACED,WPRIVATE,BASE,PETE,0x800757B0,0x80075780,0x80075820,0x800758B0]
        addresses += [at for at,_ in O.CODE_WINDOWS]
        for at in addresses:
            with self.subTest(at=hex(at)):
                m=fixture();m.flip[at]=1;r=O.capture(m,"reread")
                self.assertFalse(r["payload_valid"]);self.assertNotIn("wizard",r);self.assertNotIn("particles",r)

    def test_in_range_dynamic_terminal_is_a_postpayload_witness(self):
        m=fixture();m.flip[BASE+88]=1;r=O.capture(m,'terminal changed')
        self.assertFalse(r['payload_valid']);self.assertNotIn('particles',r)
        self.assertEqual(2,m.reads.count((BASE+88,88)))

    def test_old_observer_still_refuses_new_anchors(self):
        s=importlib.util.spec_from_file_location("old_n16_p1_control",TOOLS/"Observe-SpyroFreshId65.py")
        old=importlib.util.module_from_spec(s);s.loader.exec_module(old)
        m=fixture();self.assertFalse(old.capture(m,"")["payload_valid"])
        self.assertNotIn((POOL,1024),m.reads)


def fields(records):
    return b"".join(b"\0".join(k.encode()+str(v).encode() for k,v in r)+b"\0\n" for r in records)


class ExternalTests(unittest.TestCase):
    def setUp(self):
        self.pid=321;self.uid=501;self.exe="/Applications/DuckStation.app/Contents/MacOS/DuckStation"
        self.process=b"Mon Sep  7 01:02:03 2026 501 "+self.exe.encode()+b"\n"
        self.bin=O.FileIdentity("/private/test/native-id65-fresh-wizard-p1-n32-r01.bin",0x100000A,123,661547040)
        self.file=fields([[('p',321),('c','DuckStation')],[('f','19'),('a','r'),('t','REG'),('D','0x100000a'),('i',123),('s',661547040),('n',self.bin.path)]])
        self.listen=fields([[('p',321),('c','DuckStation')],[('f','7'),('t','IPv4'),('P','TCP'),('n','127.0.0.1:2345'),('T','ST=LISTEN')]])

    def inspect(self,process=None,file=None,listen=None):
        replies=iter([self.process if process is None else process,self.file if file is None else file,self.listen if listen is None else listen])
        commands=[]
        def fake(argv):commands.append(argv);return next(replies)
        result=O.inspect_session(self.pid,self.uid,self.exe,self.bin,runner=fake)
        self.assertEqual(3,len(commands));self.assertTrue(all(isinstance(x,tuple) for x in commands))
        self.assertEqual('/bin/ps',commands[0][0]);self.assertEqual('/usr/sbin/lsof',commands[1][0])
        self.assertNotIn('-p',commands[2]) # Enumerate competing listener owners too.
        return result

    def test_exact_neutral_snapshot_not_live_authority(self):
        r=self.inspect();self.assertEqual((321,501,self.exe), (r.pid,r.uid,r.executable))
        self.assertEqual(19,r.bin_fd);self.assertEqual("Mon Sep  7 01:02:03 2026",r.start_time)
        self.assertFalse(r.candidate_authenticated)
        self.assertIn("second",O.PROCESS_IDENTITY_LIMITATION)
        with self.assertRaises(TypeError):
            O.SessionObservation(r.pid,r.uid,r.start_time,r.executable,r.image,r.bin_fd,candidate_authenticated=True)

    def test_wrong_process_and_file_owners_refuse(self):
        for p in (self.process.replace(b'501',b'502'),self.process.replace(b'DuckStation.app',b'Other.app'),self.process+b'x',b'',b'X'*65537):
            with self.subTest(p=p[:60]),self.assertRaises(ValueError):self.inspect(process=p)
        for before,after in ((b'p321',b'p322'),(b'a r',b'a w'),(b'ar',b'aw'),(b'i123',b'i124'),(b'D0x100000a',b'D0x100000b'),
                             (b's661547040',b's661547039'),(b'tREG',b'tDIR'),(b'f19',b'f19r'),(b'n/private',b'n/other')):
            if before not in self.file:continue
            with self.subTest(before=before),self.assertRaises(ValueError):self.inspect(file=self.file.replace(before,after))
        for extra in (self.file,fields([[('f','20'),('a','r'),('t','REG'),('D','0x100000a'),('i',999),('s',10),('n','/other/another.bin')]])):
            with self.assertRaises(ValueError):self.inspect(file=self.file+extra)

    def test_listener_and_established_tuple_must_belong_to_same_process(self):
        for old,new in ((b'p321',b'p322'),(b'127.0.0.1',b'*'),(b'IPv4',b'IPv6'),(b'LISTEN',b'ESTABLISHED')):
            with self.assertRaises(ValueError):self.inspect(listen=self.listen.replace(old,new))
        with self.assertRaises(ValueError):self.inspect(listen=self.listen+self.listen)
        snap=self.inspect()
        established=fields([[('p',321),('c','DuckStation')],[('f','8'),('t','IPv4'),('P','TCP'),('n','127.0.0.1:2345->127.0.0.1:50000'),('T','ST=ESTABLISHED')]])
        O.require_connected_owner(established,snap,('127.0.0.1',50000),('127.0.0.1',2345))
        for b in (established.replace(b'p321',b'p322'),established.replace(b'50000',b'50001'),established+established):
            with self.assertRaises(ValueError):O.require_connected_owner(b,snap,('127.0.0.1',50000),('127.0.0.1',2345))
        with self.assertRaises(ValueError):O.require_same_session(snap,self.inspect(process=self.process.replace(b'01:02:03',b'01:02:04')))

    def test_strict_bounded_nul_records(self):
        for b in (b'',self.file[:-2],self.file.replace(b'ar\0',b'ar\0ar\0'),b'\xff\0\n',b'a'*65537,
                  self.file.replace(b'f19',b'f-1'),self.file.replace(b'\0\n',b'\n',1)):
            with self.subTest(b=b[:40]),self.assertRaises(ValueError):O.parse_lsof(b)

    def test_disabled_live_cli_refused_without_any_io(self):
        for args in ([],['--pid','321','--candidate-cue','/x.cue','--connect-identified-test-session'],
                     ['--pid','-1'],['--pid','321','--pid','321'],['--candidate-cue','--connect-identified-test-session'],['--host','127.0.0.1']):
            with self.subTest(args=args),patch.object(O,'PHASE2_READY',False),patch.object(socket,'create_connection',side_effect=AssertionError('socket')), \
                 patch.object(subprocess,'Popen',side_effect=AssertionError('process')),patch.object(Path,'open',side_effect=AssertionError('files')), \
                 patch('sys.stderr',new=io.StringIO()),self.assertRaises(SystemExit):O.main(args)

    def test_invalid_neutral_shapes_refuse_before_runner(self):
        for image in (O.FileIdentity(None,1,1,661547040),O.FileIdentity(True,1,1,661547040),
                      O.FileIdentity(self.bin.path,True,1,661547040)):
            with self.subTest(image=image),self.assertRaises(ValueError):
                O.inspect_session(321,501,self.exe,image,runner=lambda _:self.fail('No query on invalid shape'))

    def test_process_runner_only_accepts_exact_closed_queries(self):
        for argv in (('/bin/ps','arbitrary'),('/usr/sbin/lsof','-D','b/private/write-cache'),
                     ('/bin/sh','-c','x'),('/bin/ps','-p','321;anything'),['/bin/ps']):
            with self.subTest(argv=argv),patch.object(subprocess,'Popen',side_effect=AssertionError('No query')),self.assertRaises(ValueError):O._run(argv)

    def test_bounded_child_output_exit_timeout_and_cleanup_without_process(self):
        argv=('/bin/ps','-p','321','-o','lstart=','-o','uid=','-o','comm=')
        class Pipe:
            closed=False
            def fileno(self):return 9
            def close(self):self.closed=True
        class Process:
            def __init__(self,exitcode):self.stdout=Pipe();self.exitcode=exitcode;self.done=False;self.killed=False
            def wait(self,timeout):self.done=True;return self.exitcode
            def poll(self):return self.exitcode if self.done else None
            def kill(self):self.killed=True
        class Selector:
            def __init__(self,ready):self.ready=ready
            def __enter__(self):return self
            def __exit__(self,*args):pass
            def register(self,*args):pass
            def select(self,timeout):return [object()] if self.ready else []
        for chunks,exitcode,ready,valid in (([b'ok',b''],0,True,True),([b'error',b''],1,True,False),
                                          ([b'x'*4096]*16+[b'x'],0,True,False),([],0,False,False)):
            p=Process(exitcode)
            with patch.object(subprocess,'Popen',return_value=p) as popen,patch.object(O.selectors,'DefaultSelector',return_value=Selector(ready)), \
                 patch.object(O.os,'read',side_effect=chunks),patch.object(O.time,'monotonic',return_value=0):
                if valid:self.assertEqual(b'ok',O._run(argv))
                else:
                    with self.assertRaises(ValueError):O._run(argv)
                self.assertFalse(popen.call_args.kwargs['shell']);self.assertEqual('C',popen.call_args.kwargs['env']['LC_ALL'])
                self.assertTrue(p.stdout.closed);self.assertTrue(p.done)
        with patch.object(subprocess,'Popen',side_effect=FileNotFoundError('unavailable')),self.assertRaises(ValueError):O._run(argv)


class TransportTests(unittest.TestCase):
    class Socket:
        def __init__(self,data):self.data=bytearray(data);self.sent=[]
        def recv(self,n):b=bytes(self.data[:n]);del self.data[:n];return b
        def sendall(self,b):self.sent.append(b)

    def test_only_m_ack_and_strict_owner_reply_bounds(self):
        self.assertIs(O.ReadOnlyClient,O.wizard.ReadOnlyClient)
        frame=O.wizard.rsp.frame_payload
        sock=self.Socket(b'+'+frame('AA'*1024))
        self.assertEqual(bytes([170])*1024,O.ReadOnlyClient(sock).read(0x801FFC00,1024))
        self.assertEqual([frame('m801ffc00,400'),b'+'],sock.sent)
        for at,n in ((True,4),(0x80000000,True),(0x7FFFFFFF,4),(0x801FFFFF,2),(0x80000000,0),(0x80000000,1025)):
            s=self.Socket(b'')
            with self.assertRaises(ValueError):O.ReadOnlyClient(s).read(at,n)
            self.assertEqual([],s.sent)
        for reply in ('00','GGGGGGGG','0011223344','E01'):
            with self.assertRaises(ValueError):O.ReadOnlyClient(self.Socket(b'+'+frame(reply))).read(0x8007596C,4)

    def test_actual_wire_failure_bounds_remain_unchanged(self):
        frame=O.wizard.rsp.frame_payload
        for response in (b'',b'-',frame('00000000'),b'+$00000000#zz',b'+$00000000#01',
                         b'+$'+b'0'*4097,b'+'+frame('S05')*32):
            s=self.Socket(response)
            with self.assertRaises(O.wizard.rsp.RspProtocolError):O.ReadOnlyClient(s).read(0x8007596C,4)
            self.assertTrue(all(b in (b'+',b'-') or b.startswith(b'$m') for b in s.sent))


class Phase2TextOwnerTests(unittest.TestCase):
    """Neutral NUL fixtures, not a real process/mapped executable observation."""
    def setUp(self):
        self.pid=321
        self.expected=O.FileIdentity('/private/fixture/Test.app/Contents/MacOS/Test',0x100000A,123,17)
        self.header=[('p',self.pid),('c','Test')]
        self.executable=[('f','txt'),('t','REG'),('D','0x100000a'),('i',123),('s',17),('n',self.expected.path)]
        self.library=[('f','txt'),('t','REG'),('D','0x100000a'),('i',456),('s',23),('n','/private/fixture/libExample.dylib')]

    def test_exact_executable_not_first_mapping_and_other_library_count(self):
        for records,count in (([self.executable],0),([self.library,self.executable],1),([self.executable,self.library],1)):
            with self.subTest(count=count,first=records[0][-1]):
                result=O.require_text_owner(fields([self.header,*records]),self.pid,self.expected)
                self.assertIsInstance(result,tuple)
                self.assertEqual(2,len(result));self.assertIs(result[0],self.expected);self.assertEqual(count,result[1])
        with self.assertRaises(dataclasses.FrozenInstanceError):self.expected.inode=999
        with self.assertRaises(ValueError):O.parse_lsof(fields([self.header,self.executable]))

    def test_missing_duplicate_wrong_and_incomplete_executable_owners_refuse(self):
        raw=fields([self.header,self.executable])
        for before,after in ((b'p321',b'p322'),(b'tREG',b'tDIR'),(b'D0x100000a',b'D0x100000b'),
                             (b'i123',b'i124'),(b's17',b's18'),(b'ftxt',b'f19'),
                             (self.expected.path.encode(),(self.expected.path+' (deleted)').encode())):
            with self.subTest(before=before),self.assertRaises(ValueError):
                O.require_text_owner(raw.replace(before,after),self.pid,self.expected)
        for records in ([self.library],[self.executable,self.executable],
                        [self.executable,[(k,124 if k=='i' else v) for k,v in self.executable]]):
            with self.subTest(records=records),self.assertRaises(ValueError):
                O.require_text_owner(fields([self.header,*records]),self.pid,self.expected)
        for key in ('f','t','D','i','s','n'):
            with self.subTest(missing=key),self.assertRaises(ValueError):
                O.require_text_owner(fields([self.header,[(k,v) for k,v in self.executable if k!=key]]),self.pid,self.expected)
        for incomplete_library in ([('f','txt'),('n','/private/fixture/unknown')],
                                   [(k,'DIR' if k=='t' else v) for k,v in self.library]):
            with self.assertRaises(ValueError):
                O.require_text_owner(fields([self.header,self.executable,incomplete_library]),self.pid,self.expected)

    def test_bounded_complete_txt_grammar_and_neutral_shape_refuse(self):
        raw=fields([self.header,self.executable])
        for bad in (b'',b'x'*65537,raw[:-1],raw[:-2],b'\xff\0\n',fields([self.executable]),
                    raw.replace(b'tREG\0',b'tREG\0tREG\0'),raw+fields([self.header,self.library]),
                    raw.replace(b's17',b's-1'),raw.replace(b'i123',b'iUNKNOWN'),raw.replace(b'D0x100000a',b'D?')):
            with self.subTest(raw=bad[:50]),self.assertRaises(ValueError):O.require_text_owner(bad,self.pid,self.expected)
        for pid in (True,0,-1,'321'):
            with self.subTest(pid=pid),self.assertRaises(ValueError):O.require_text_owner(raw,pid,self.expected)
        for expected in (None,O.FileIdentity('relative',1,1,1),O.FileIdentity(self.expected.path,True,123,17),
                         O.FileIdentity(self.expected.path,0x100000A,123,0)):
            with self.subTest(expected=expected),self.assertRaises(ValueError):O.require_text_owner(raw,self.pid,expected)


class Phase2FileTests(unittest.TestCase):
    """Only tiny owned temporary files. Never originals, candidates or executables."""
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory(prefix='p1-neutral-files-')
        self.addCleanup(self.temp.cleanup)
        self.root=Path(self.temp.name).resolve()  # macOS /var alias is not part of this fixture.
        self.path=self.root/'owner.data';self.data=b'exact tiny neutral owner\x00\x01\xff'
        self.path.write_bytes(self.data)
        self.pin=O.FilePin(str(self.path),len(self.data),hashlib.sha256(self.data).hexdigest())

    def test_immutable_pin_correct_full_identity_and_idempotent_close(self):
        with self.assertRaises(dataclasses.FrozenInstanceError):self.pin.size=1
        real_open=os.open;calls=[]
        def record(path,flags,*args,**kwargs):
            calls.append((path,flags));return real_open(path,flags,*args,**kwargs)
        with patch.object(O.os,'open',side_effect=record):owner=O._open_verified_file(self.pin)
        try:
            current=self.path.stat()
            self.assertEqual(O.FileIdentity(str(self.path),current.st_dev,current.st_ino,len(self.data)),owner.identity)
            leaves=[flags for path,flags in calls if Path(os.fsdecode(path)).name==self.path.name]
            self.assertEqual(1,len(leaves))
            flags=leaves[0]
            self.assertEqual(os.O_RDONLY,flags&os.O_ACCMODE)
            for flag in (os.O_NOFOLLOW,os.O_CLOEXEC,os.O_NONBLOCK):self.assertEqual(flag,flags&flag)
            with patch.object(O.os,'read',side_effect=AssertionError('Metadata-only check must not hash')):owner.check()
            owner.check(full=True)
            self.assertEqual(self.data,self.path.read_bytes())
        finally:owner.close()
        owner.close()
        with self.assertRaises(ValueError):owner.check()

    def test_bad_pin_shape_before_open_and_wrong_whole_hash_or_size(self):
        for path,size,sha in ((None,len(self.data),'0'*64),('relative',len(self.data),'0'*64),
                              (str(self.path),True,'0'*64),(str(self.path),-1,'0'*64),
                              (str(self.path),0,'0'*64),(str(self.path),len(self.data),'x'*64),
                              (str(self.path),len(self.data),'0'*63)):
            with self.subTest(path=path,size=size,sha=sha),patch.object(O.os,'open',side_effect=AssertionError('Invalid shape before open')):
                with self.assertRaises(ValueError):O._open_verified_file(O.FilePin(path,size,sha))
        for size,sha in ((len(self.data)-1,self.pin.sha256),(len(self.data)+1,self.pin.sha256),
                         (1<<60,self.pin.sha256),(len(self.data),'0'*64)):
            with self.subTest(size=size,sha=sha),self.assertRaises(ValueError):
                O._open_verified_file(O.FilePin(str(self.path),size,sha))
        with self.assertRaises(ValueError):
            O._open_verified_file(O.FilePin(str(self.root/'absent'),len(self.data),self.pin.sha256))

    def test_symlink_ancestors_leaf_directory_fifo_and_missing_capability_refuse(self):
        leaf=self.root/'leaf-link';leaf.symlink_to(self.path)
        folder=self.root/'real';folder.mkdir();(folder/'owner').write_bytes(self.data)
        alias=self.root/'linked-folder';alias.symlink_to(folder,target_is_directory=True)
        for path in (leaf,alias/'owner',folder):
            with self.subTest(path=path),patch.object(O.os,'read',side_effect=AssertionError('No nonregular/linked reads')):
                with self.assertRaises(ValueError):O._open_verified_file(O.FilePin(str(path),len(self.data),self.pin.sha256))
        fifo=self.root/'pipe';os.mkfifo(fifo)
        real_open=os.open
        def safe_open(path,flags,*args,**kwargs):
            if Path(os.fsdecode(path)).name==fifo.name:
                self.assertTrue(flags&os.O_NONBLOCK,'A missing guard must not hang this FIFO test.')
            return real_open(path,flags,*args,**kwargs)
        with patch.object(O.os,'open',side_effect=safe_open),self.assertRaises(ValueError):
            O._open_verified_file(O.FilePin(str(fifo),len(self.data),self.pin.sha256))
        for capability in ('O_NOFOLLOW','O_CLOEXEC','O_NONBLOCK'):
            with self.subTest(capability=capability),patch.dict(O.os.__dict__),patch.object(O.os,'open',side_effect=AssertionError('No weaker open fallback')):
                del O.os.__dict__[capability]
                with self.assertRaises(ValueError):O._open_verified_file(self.pin)

    def test_bounded_partial_reads_are_valid_but_short_and_false_eof_refuse_without_fd_leaks(self):
        real_read=os.read
        with patch.object(O.os,'read',side_effect=lambda fd,n:real_read(fd,min(n,3))):
            owner=O._open_verified_file(self.pin)
            owner.close()
        for fault in ('short','extra'):
            opened=[];real_open=os.open
            def tracked_open(*args,**kwargs):
                fd=real_open(*args,**kwargs);opened.append(fd);return fd
            def read(fd,n):
                if fault=='short':return b''
                data=real_read(fd,n)
                return data if data else b'X'
            with self.subTest(fault=fault),patch.object(O.os,'open',side_effect=tracked_open),patch.object(O.os,'read',side_effect=read):
                with self.assertRaises(ValueError):O._open_verified_file(self.pin)
            self.assertTrue(opened)
            for fd in opened:
                with self.assertRaises(OSError):os.fstat(fd)

    def test_final_position_and_metadata_drift_during_initial_hash_refuse(self):
        real_seek=os.lseek;perturbed=[]
        def seek(fd,offset,whence):
            result=real_seek(fd,offset,whence)
            if whence==os.SEEK_CUR:perturbed.append(fd);return result+1
            return result
        with patch.object(O.os,'lseek',side_effect=seek),self.assertRaises(ValueError):O._open_verified_file(self.pin)
        self.assertTrue(perturbed)
        real_read=os.read;changed=[];before=self.path.stat()
        def read(fd,n):
            data=real_read(fd,n)
            if data and not changed:
                changed.append(True);os.utime(self.path,ns=(before.st_atime_ns,before.st_mtime_ns+2_000_000_000))
            return data
        with patch.object(O.os,'read',side_effect=read),self.assertRaises(ValueError):O._open_verified_file(self.pin)
        self.assertEqual([True],changed)

    def test_all_held_descriptor_metadata_fields_and_path_replacement_refuse(self):
        real_stat=os.fstat
        class ChangedStat:
            def __init__(self,original,key,value):self.original,self.key,self.value=original,key,value
            def __getattr__(self,name):return self.value if name==self.key else getattr(self.original,name)
        for field in ('st_dev','st_ino','st_mode','st_size','st_mtime_ns','st_ctime_ns'):
            owner=O._open_verified_file(self.pin)
            try:
                def fstat(fd):
                    result=real_stat(fd);value=getattr(result,field)
                    return ChangedStat(result,field,value^stat.S_IXUSR if field=='st_mode' else value+1)
                with self.subTest(field=field),patch.object(O.os,'fstat',side_effect=fstat),self.assertRaises(ValueError):owner.check()
            finally:owner.close()
        owner=O._open_verified_file(self.pin)
        try:
            self.path.unlink();self.path.write_bytes(self.data)
            with self.assertRaises(ValueError):owner.check()
        finally:owner.close()

    def test_same_length_mutation_and_extension_refuse_recheck(self):
        for replacement in (bytes([self.data[0]^1])+self.data[1:],self.data+b'x'):
            self.path.write_bytes(self.data);owner=O._open_verified_file(self.pin)
            try:
                self.path.write_bytes(replacement)
                with self.assertRaises(ValueError):owner.check(full=True)
            finally:owner.close()


class Phase2FinalizationTests(unittest.TestCase):
    """Fake owners preserve failure identity; these are not real session closes."""
    class Owner:
        def __init__(self,name,events,check_error=None,close_error=None):
            self.name,self.events,self.check_error,self.close_error=name,events,check_error,close_error
        def check(self,full=False):
            if full is not True:raise AssertionError('Finalizer must request complete hashes.')
            self.events.append(('check',self.name))
            if self.check_error is not None:raise self.check_error
        def close(self):
            self.events.append(('close',self.name))
            if self.close_error is not None:raise self.close_error

    def test_no_failure_and_single_original_failure_are_not_wrapped(self):
        self.assertIsNone(O._finalize_files([]))
        for failure_at in (None,'primary','check','close'):
            with self.subTest(failure_at=failure_at):
                events=[];failure=ValueError('exact original failure')
                owners=[self.Owner('a',events,failure if failure_at=='check' else None,failure if failure_at=='close' else None),self.Owner('b',events)]
                if failure_at is None:self.assertIsNone(O._finalize_files(owners))
                else:
                    with self.assertRaises(ValueError) as caught:O._finalize_files(owners,failure if failure_at=='primary' else None)
                    self.assertIs(failure,caught.exception)
                self.assertEqual([('check','a'),('check','b'),('close','a'),('close','b')],events)
        interrupt=KeyboardInterrupt('original interrupt')
        with self.assertRaises(KeyboardInterrupt) as caught:O._finalize_files([],interrupt)
        self.assertIs(interrupt,caught.exception)

    def test_every_full_check_then_every_close_preserves_all_baseexceptions_in_order(self):
        primary=KeyboardInterrupt('primary');a=ValueError('hash a');b=OSError('hash b');c=RuntimeError('close a');d=SystemExit(7)
        events=[];owners=[self.Owner('a',events,a,c),self.Owner('b',events,b,d),self.Owner('c',events)]
        with self.assertRaises(BaseExceptionGroup) as caught:O._finalize_files(owners,primary)
        self.assertEqual(5,len(caught.exception.exceptions))
        self.assertTrue(all(actual is expected for actual,expected in zip(caught.exception.exceptions,(primary,a,b,c,d))))
        self.assertEqual([('check','a'),('check','b'),('check','c'),('close','a'),('close','b'),('close','c')],events)

    def test_existing_primary_group_is_retained_not_flattened_or_discarded(self):
        first=ValueError('one');second=OSError('two');primary=ExceptionGroup('original group',[first,second]);final=RuntimeError('final')
        events=[]
        with self.assertRaises(BaseExceptionGroup) as caught:O._finalize_files([self.Owner('a',events,final)],primary)
        self.assertEqual(2,len(caught.exception.exceptions));self.assertIs(primary,caught.exception.exceptions[0]);self.assertIs(final,caught.exception.exceptions[1])
        self.assertEqual([('check','a'),('close','a')],events)


class Phase2QueryTests(unittest.TestCase):
    TXT=('/usr/sbin/lsof','-nP','-a','-p','321','-d','txt','-F0pcftaiDnsPT','-Ts')
    ESTABLISHED=('/usr/sbin/lsof','-nP','-a','-p','321','-iTCP:2345','-sTCP:ESTABLISHED','-F0pcftaiDnsPT','-Ts')

    def test_only_two_exact_additional_ownership_queries_reach_fake_process(self):
        class Pipe:
            closed=False
            def fileno(self):return 9
            def close(self):self.closed=True
        class Process:
            def __init__(self,code):self.stdout=Pipe();self.code=code;self.done=False
            def wait(self,timeout):self.done=True;return self.code
            def poll(self):return self.code if self.done else None
            def kill(self):self.done=True
        class Selector:
            def __init__(self,ready):self.ready=ready
            def __enter__(self):return self
            def __exit__(self,*args):pass
            def register(self,*args):pass
            def select(self,timeout):return [object()] if self.ready else []
        for argv in (self.TXT,self.ESTABLISHED):
            for chunks,code,ready,valid in (([b'ok',b''],0,True,True),([b'error',b''],1,True,False),
                                           ([b'x'*4096]*16+[b'x'],0,True,False),([],0,False,False)):
                process=Process(code)
                with self.subTest(argv=argv,code=code,ready=ready),patch.object(subprocess,'Popen',return_value=process) as popen, \
                     patch.object(O.selectors,'DefaultSelector',return_value=Selector(ready)),patch.object(O.os,'read',side_effect=chunks), \
                     patch.object(O.time,'monotonic',return_value=0):
                    if valid:self.assertEqual(b'ok',O._run(argv))
                    else:
                        with self.assertRaises(ValueError):O._run(argv)
                    self.assertEqual(argv,popen.call_args.args[0]);self.assertFalse(popen.call_args.kwargs['shell'])
                    self.assertEqual(subprocess.DEVNULL,popen.call_args.kwargs['stdin']);self.assertEqual('C',popen.call_args.kwargs['env']['LC_ALL'])
                    self.assertTrue(process.done);self.assertTrue(process.stdout.closed)

    def test_mutated_query_pid_port_flags_and_extra_arguments_refuse_before_process(self):
        bad=[list(self.TXT),self.TXT+('extra',),self.ESTABLISHED+('extra',),self.TXT[:-1],self.ESTABLISHED[:-1]]
        for pid in ('0','-1','321;anything','321 '):
            bad.extend(tuple(pid if i==4 else value for i,value in enumerate(argv)) for argv in (self.TXT,self.ESTABLISHED))
        bad.extend((tuple('txt-other' if x=='txt' else x for x in self.TXT),
                    tuple('-iTCP:2346' if x=='-iTCP:2345' else x for x in self.ESTABLISHED),
                    tuple('-sTCP:LISTEN' if x=='-sTCP:ESTABLISHED' else x for x in self.ESTABLISHED),
                    tuple('-Fpcn' if x=='-F0pcftaiDnsPT' else x for x in self.TXT)))
        for argv in bad:
            with self.subTest(argv=argv),patch.object(subprocess,'Popen',side_effect=AssertionError('No real/unapproved process query')),self.assertRaises(ValueError):O._run(argv)


if __name__=='__main__':unittest.main()
