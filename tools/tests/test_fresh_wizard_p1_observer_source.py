"""Explicit original-only P1 gates. No default ROM, process, socket or .NET I/O.

Uses the frozen original-source combined-body/Host oracles, not a generated
candidate or Core DLL. Source mode has strict full original pre/finally/EOF.
"""
import argparse
import contextlib
import hashlib
import importlib.util
import io
import json
import os
from pathlib import Path
import struct
import sys
import unittest

sys.dont_write_bytecode=True
ROOT=Path(__file__).resolve().parents[2]
SOURCE=None
IMAGE_SHA='fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37'
BODY='_local/research/dark-wizard-native-closure-20260906/fresh-combined-body-r01.4Uhwza/source_combined_body.py'
BODY_SHA='a7d2c10729b6a88d564297929f59d8cba456a4906d01a101be9a29ffb21d28bd'


def load(path,name):
    spec=importlib.util.spec_from_file_location(name,ROOT/path);m=importlib.util.module_from_spec(spec);sys.modules[name]=m
    with contextlib.redirect_stdout(io.StringIO()):spec.loader.exec_module(m)
    return m


class SourceGates(unittest.TestCase):
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
    parser=argparse.ArgumentParser();parser.add_argument('--source');args,rest=parser.parse_known_args()
    SOURCE=args.source;unittest.main(argv=[sys.argv[0],*rest])
