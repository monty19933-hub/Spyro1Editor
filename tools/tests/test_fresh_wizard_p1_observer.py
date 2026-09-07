"""P1 observer tests first. Fake memory/process/socket only; no live connection."""
import importlib.util
import io
from pathlib import Path
import socket
import struct
import subprocess
import sys
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

    def test_phase1_live_cli_always_refused_without_any_io(self):
        for args in ([],['--pid','321','--candidate-cue','/x.cue','--connect-identified-test-session'],
                     ['--pid','-1'],['--pid','321','--pid','321'],['--candidate-cue','--connect-identified-test-session'],['--host','127.0.0.1']):
            with self.subTest(args=args),patch.object(socket,'create_connection',side_effect=AssertionError('socket')), \
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


if __name__=='__main__':unittest.main()
