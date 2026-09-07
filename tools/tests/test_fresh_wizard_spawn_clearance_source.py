"""Exact unsafe P1 spawn witness, not a compiler guard or native execution.

Default discovery is source/fixture-free. Explicit --source BIN --issued-bin BIN
reads the two pinned complete images; --recorded JSONL reads only the pinned
closed R03 observation. The two modes cannot be combined.
"""
import argparse
import contextlib
import copy
import hashlib
import importlib.util
import io
import json
import os
from pathlib import Path
import stat
import struct
import sys
import unittest
from unittest.mock import Mock, patch

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ISSUED = RECORDED = None
IMAGE_LENGTH = 661547040
ORIGINAL_SHA = 'fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37'
ISSUED_SHA = 'c4a70f826b1a9a3d4ad8e36512be354b6205d27ea2c7a314776a51067f35b5a6'
LOG_SHA = '9a721ac35712da926c67e8b7e12b1f9a2f4392ba52e9ca58ca70a0bedc7bd18e'
HELPER = ROOT / 'src/Spyro.Editor.NativeWizardFamilyAssetSmoke/source_fresh_wizard_audio_audit.py'
HELPER_SHA = '7fe4e4e65d1552420bb48778a274e25afdf65005d55e8590125d2f84db23f532'
IDENTITY = (4096, 0, 0, 0, 4096, 0, 0, 0, 4096)
NATIVE_WORDS = {
    0x80040764:0x24140001, 0x80040904:0x24050164, 0x8004090c:0x24c68a84,
    0x80040914:0xafa00010, 0x8004091c:0xafb40014,
    0x8004e8c4:0x000d5d43, 0x8004e8c8:0x000d6280, 0x8004e8cc:0x000c6543,
    0x8004e8d0:0x000d6d00, 0x8004e8d4:0x000d6cc3,
    0x8004e8d8:0x01cb5804, 0x8004e8dc:0x01cc6004, 0x8004e8e0:0x01cd6804,
    0x8004e8e4:0x000d7400, 0x8004e8e8:0x01cc7020,
    0x8004e8ec:0x488b0800, 0x8004e8f0:0x488e0000,
    0x8004e900:0x480c5800, 0x8004e904:0x480d4800, 0x8004e908:0x480e5000,
    0x8004e90c:0x01836020, 0x8004e910:0x008d6822, 0x8004e914:0x00ae7022,
    0x8004e924:0x00025402, 0x8004e928:0x01565020,
    0x8004e984:0x8c30fffc, 0x8004e98c:0x01f07825, 0x8004e990:0xae8f0000,
    0x8004e994:0x304f0002, 0x8004e998:0x15e0fefd,
    0x8004e9a0:0x326f0001, 0x8004e9a4:0x11e00007, 0x8004e9bc:0x08013a76,
    0x8004eb4c:0xaf970000, 0x8004eb50:0xaf980004, 0x8004eb54:0xaf990008,
    0x80052770:0x200a1000, 0x80052774:0x200b0000, 0x80052778:0x200c1000,
    0x8005277c:0x200d0000, 0x80052780:0x200e1000,
    0x800527ac:0x1080002a, 0x8005285c:0x1080002b, 0x80052910:0x10800027,
}


def require(condition, label):
    if not condition: raise ValueError(label)


def sha(data): return hashlib.sha256(data).hexdigest()


def options(argv):
    parser=argparse.ArgumentParser(description=__doc__, allow_abbrev=False)
    for option in ('--source','--issued-bin','--recorded'): parser.add_argument(option)
    if any(sum(a.split('=',1)[0]==o for a in argv)>1 for o in ('--source','--issued-bin','--recorded')):
        parser.error('Repeated mode option')
    args=parser.parse_args(argv)
    if any(v is not None and not v.strip() for v in (args.source,args.issued_bin,args.recorded)):
        parser.error('Empty path')
    if bool(args.source)!=bool(args.issued_bin) or (args.source and args.recorded):
        parser.error('Use paired source/issued-bin or separate recorded mode')
    return args.source,args.issued_bin,args.recorded


def metadata(s): return (s.st_dev,s.st_ino,s.st_mode,s.st_size,s.st_mtime_ns,s.st_ctime_ns)


def identity(owner):
    f,path,n,digest,initial=owner
    require(stat.S_ISREG(initial.st_mode) and initial.st_size==n,'Exact regular owner')
    require(metadata(os.fstat(f.fileno()))==metadata(initial)==metadata(path.lstat()),'Held/path identity')
    require(f.seek(0,2)==n,'Initial length');f.seek(0);h=hashlib.sha256();remaining=n
    while remaining:
        part=f.read(min(1024*1024,remaining))
        require(0<len(part)<=remaining,'Bounded complete read');h.update(part);remaining-=len(part)
    require(f.tell()==n and f.read(1)==b'' and f.seek(0,2)==n and h.hexdigest()==digest,'Full SHA / true EOF')
    require(metadata(os.fstat(f.fileno()))==metadata(initial)==metadata(path.lstat()),'Final held/path identity')


@contextlib.contextmanager
def held_files(specs):
    owners=[];failures=[]
    try:
        for path,n,digest in specs:
            path=Path(path)
            fd=os.open(path,os.O_RDONLY|os.O_NOFOLLOW|os.O_NONBLOCK)
            try: f=os.fdopen(fd,'rb')
            except BaseException: os.close(fd);raise
            try: initial=os.fstat(f.fileno())
            except BaseException: f.close();raise
            owners.append((f,path,n,digest,initial));identity(owners[-1])
        yield owners
    except BaseException as error: failures.append(error)
    finally:
        # Attempt every final identity and every close, retaining the primary too.
        for owner in owners:
            try: identity(owner)
            except BaseException as error: failures.append(error)
        for owner in owners:
            try: owner[0].close()
            except BaseException as error: failures.append(error)
    if len(failures)==1: raise failures[0]
    if failures: raise BaseExceptionGroup('Witness primary/final owner failures',failures)


@contextlib.contextmanager
def captured_sources(source,issued):
    with held_files([(HELPER,13506,HELPER_SHA),
                     (Path(source),IMAGE_LENGTH,ORIGINAL_SHA),
                     (Path(issued),IMAGE_LENGTH,ISSUED_SHA)]) as owners:
        helper,original,p1=(o[0] for o in owners)
        helper.seek(0);code=helper.read(13507)
        require(len(code)==13506 and sha(code)==HELPER_SHA,'Executed helper pin')
        spec=importlib.util.spec_from_file_location('_spawn_raw',HELPER)
        raw=importlib.util.module_from_spec(spec)
        exec(compile(code,str(HELPER),'exec'),raw.__dict__) # pinned leaf; no transitive imports
        values={}
        for key in ('exe','wizard-data'):
            at,n,_,digest=raw.SOURCES[key];values[key]=raw.logical(original,at,n)
            require(sha(values[key])==digest,'Complete original '+key)
        directory=raw.logical(p1,37*2048,2048)
        require(struct.unpack_from('<4I',directory,79*8)==(0x6927000,0x11000,0x6938000,0x20c800),'P1 physical domains')
        body=raw.logical(p1,37*2048+0x6938000,0x20c800)
        require(sha(body)=='33e54ff9e15608ada3f6d430c54fbabc21a8263e059c050b391db22cc541bc00','Complete body')
        fields=struct.unpack_from('<16I',body)
        require(fields[4:8]==(0xe7000,0x19000,0x100000,0x3000),'Exact models/scene owners')
        models=body[0xe7000:0x100000];scene=body[0x100000:0x103000]
        require(sha(models)=='84d00dbbce264bc08c61abfe2579fe3d7a7464ba2df6b189834664da10b82c84','Complete models')
        require(sha(scene)=='b7bd222702ff9b5d6ea40c623159f4aa22b3425ef9368059167d1ccab930266b','Complete scene')
        candidate_exe=raw.logical(p1,54958*2048,0x66000)
        require(sha(candidate_exe)=='a0eb46c2735355c05d66cc33492ed70c6c2174e77e8e8eb6c2ca8fae0f3fc8dc','Complete P1 EXE')
        for pc in NATIVE_WORDS:
            off=pc-0x8000f800
            require(candidate_exe[off:off+4]==values['exe'][off:off+4],'Actual native complement')
        package=values['wizard-data'][0x19b518:0x19eacc]
        require(sha(package)=='070a57069c8747ada4caacc0394b93e9a970e5ba7ff21159584abdc7a0fd4faf','Whole source Wizard')
        require(models[0x17134:0x17138]==bytes(4),'Separate actual generated lookahead owner')
        yield values['exe'],package,models[0x13b80:0x17134],scene


def geometry(package,row,spawn,matrix,anim,frame):
    require(len(package)==0x35b4 and len(row)==0x58 and len(spawn)==12,'Bounded fixed domains')
    require(tuple(matrix)==IDENTITY and anim==frame==0,'Only confirmed identity/current pose0')
    require(row[0x44:0x48]==b'\0\0\0\4','Initial zero-angle source row only')
    roots=struct.unpack_from('<8I',package,0x14)
    require(roots==(0x800035a4,0,0,0,0,0,0,0),'Exact collider roots')
    broad,command,vertex_index,flags=struct.unpack_from('<4I',package,0x35a4)
    require((broad,command,vertex_index,flags)==(2011,0x01900301,0,0),'Exact sphere descriptor')
    ap=struct.unpack_from('<I',package,0x38)[0]
    geom=struct.unpack_from('<I',package,0x34)[0]
    require(ap==0x44 and geom==0x238 and package[ap+4:ap+6]==b'\0\1','Exact frame grammar/scale1')
    frameword=struct.unpack_from('<I',package,ap+0x24)[0];vp=geom+(frameword&0x1fffff)
    require(vp==0xfb0,'Actual bounded frame0 vertex owner')
    word=struct.unpack_from('<I',package,vp)[0]
    def signed(v): return (v&0x7fffffff)-(v&0x80000000)
    vertex=((signed(word)>>21)<<1,(signed(word<<10)>>21)<<1,(signed(word<<20)>>19)<<1)
    require(word==0xf6402fc4 and vertex==(-156,10,-240),'Signed packed vertex0')
    xyz=struct.unpack_from('<3i',row,12);player=struct.unpack('<3i',spawn)
    require(xyz==(8704,8960,8704) and player==(8960,8960,9216),'Exact initial placement')
    centre=(xyz[0]+vertex[0],xyz[1]-vertex[1],xyz[2]-vertex[2])
    distance2=sum((a-b)**2 for a,b in zip(centre,player));radius2=((command>>16)+356)**2
    return dict(vertex=vertex,centre=centre,distance2=distance2,radius2=radius2,
                overlap=distance2<radius2,incoming_flags=flags,
                response_permitted=not bool(command&2),native_gte_executed=False)


def recorded_witness(rows):
    require(rows[-1]==dict(kind='session_closed',final_full_checks_passed=True,
                          candidate_authenticated=False,runtime_pass=False),'Honest closed session')
    samples=[r for r in rows if r.get('kind')=='sample']
    require([s['ordinal'] for s in samples]==list(range(36)),'All 36 ordered samples')
    for s in samples:
        for key in ('candidate_authenticated','runtime_pass','ui_result_approved','normal_export_approved'):
            require(s[key] is False,'False approval retained')
    for s in samples[33:36]:
        require(s['payload_valid'] and s['gate_reread_consistent'],'Accepted sequential witness')
        raw=bytes.fromhex(s['wizard']['raw_hex'])
        require(len(raw)==0x58 and struct.unpack_from('<9h',raw,0x20)==IDENTITY,'Actual sampled identity')
        require((raw[0x3c],raw[0x3e])==(0,0),'Actual current animation/frame0')
        require(struct.unpack_from('<3i',raw,12)==(8704,8960,8704),'Actual sampled row centre')
    return samples


class ModeTests(unittest.TestCase):
    def test_exact_modes_before_any_capture(self):
        self.assertEqual((None, None, None), options([]))
        self.assertEqual(('a', 'b', None), options(['--source', 'a', '--issued-bin', 'b']))
        self.assertEqual((None, None, 'c'), options(['--recorded', 'c']))
        for args in (['--source', 'a'], ['--issued-bin', 'b'],
                     ['--source', 'a', '--issued-bin', 'b', '--recorded', 'c'],
                     ['--recorded', 'a', '--recorded', 'b'], ['--unknown'],
                     ['--recorded=a','--recorded=b'], ['--recorded',''],
                     ['--recorded=a','--recorded','b'], ['--recorded','a','--recorded=b'],
                     ['--recorded',' \t '], ['--source',' ','--issued-bin','b'],
                     ['--source','a','--issued-bin',' '],
                     ['--source', '--recorded', '--issued-bin', 'b']):
            with self.subTest(args=args), contextlib.redirect_stderr(io.StringIO()):
                with self.assertRaises(SystemExit): options(args)

    def test_primary_and_both_final_failures_preserved_and_both_closed(self):
        files=[Mock(),Mock()];seen=[];primary=ValueError('primary')
        def verify(owner):
            seen.append(owner[1].name)
            if len(seen)>2: raise OSError('final '+owner[1].name)
        with patch.object(os,'open',side_effect=[90,91]),patch.object(os,'fdopen',side_effect=files), \
             patch.object(os,'fstat',return_value=object()),patch(__name__+'.identity',side_effect=verify):
            with self.assertRaises(BaseExceptionGroup) as failure:
                with held_files([(Path('a'),1,'x'),(Path('b'),1,'y')]): raise primary
        self.assertEqual(['a','b','a','b'],seen)
        self.assertIs(primary,failure.exception.exceptions[0])
        self.assertEqual(3,len(failure.exception.exceptions))
        for f in files: f.close.assert_called_once_with()


class SourceTests(unittest.TestCase):
    def test_exact_issued_overlap_and_source_response_not_damage(self):
        if SOURCE is None: self.skipTest('explicit --source/--issued-bin only')
        with captured_sources(SOURCE, ISSUED) as (exe, package, installed, scene):
            self.assertEqual(package[0x14:0x238], installed[0x14:0x238])
            self.assertEqual(package[0x35a4:], installed[0x35a4:])
            self.assertEqual(package[0xfb0:0xfb4], installed[0xfb0:0xfb4])
            row = scene[0xd4:0x12c]
            self.assertEqual(b'\0\0\0\4', row[0x44:0x48])
            self.assertEqual(bytes(4), row[0x3c:0x40])
            result = geometry(package, row, scene[:12], IDENTITY, 0, 0)
            self.assertEqual((-156, 10, -240), result['vertex'])
            self.assertEqual((8548, 8950, 8944), result['centre'])
            self.assertEqual((243828, 571536, True),
                             (result['distance2'], result['radius2'], result['overlap']))
            self.assertEqual((0, True), (result['incoming_flags'], result['response_permitted']))
            self.assertFalse(result['native_gte_executed'])
            # These plausible wrong arithmetic recipes must not equal the witness.
            spawn = struct.unpack('<3i', scene[:12]); xyz = struct.unpack_from('<3i', row, 12)
            for wrong in ((8548, 8970, 8464), (8626, 8955, 8824), xyz):
                self.assertNotEqual(result['distance2'], sum((a-b)**2 for a,b in zip(wrong,spawn)))
            for matrix, anim, frame in ((IDENTITY,1,0),(IDENTITY,0,1),((1,)*9,0,0)):
                with self.assertRaises(ValueError): geometry(package,row,scene[:12],matrix,anim,frame)
            for offset in (0x44+5, 0x35a8, 0x35ac):
                changed = bytearray(package); changed[offset] ^= 1
                with self.assertRaises(ValueError): geometry(changed,row,scene[:12],IDENTITY,0,0)
            # Zero incoming flags do not bypass the separately controlled push-out.
            for pc, word in NATIVE_WORDS.items():
                self.assertEqual(word, struct.unpack_from('<I', exe, pc-0x8000f800)[0], hex(pc))


class RecordedTests(unittest.TestCase):
    def test_closed_actual_pose_displacement_and_no_approval(self):
        if RECORDED is None: self.skipTest('explicit --recorded only')
        with held_files([(Path(RECORDED),155872,LOG_SHA)]) as owners:
            owners[0][0].seek(0); data=owners[0][0].read(155873)
            rows = [json.loads(line) for line in data.splitlines()]
            samples = recorded_witness(rows)
            self.assertEqual(list(range(36)), [s['ordinal'] for s in samples])
            for n in (33,34,35):
                s=samples[n]; raw=bytes.fromhex(s['wizard']['raw_hex'])
                self.assertEqual(IDENTITY, struct.unpack_from('<9h',raw,0x20))
                self.assertEqual((0,0), (raw[0x3c],raw[0x3e]))
                self.assertEqual((3,0), (s['player_health_s32'],s['player_damage_accumulator_u32']))
            self.assertEqual([7008,192,4640], samples[33]['player_motion_78b64_s32x3'])
            self.assertEqual(572306,sum((a-b)**2 for a,b in zip(samples[33]['player_xyz'],(8548,8950,8944))))
            self.assertEqual(1023659,sum((a-b)**2 for a,b in zip(samples[35]['player_xyz'],(8548,8950,8944))))
            for n, previous in ((33,[8960,8960,9216]),(35,samples[34]['player_xyz'])):
                expected=[32*(a-b) for a,b in zip(samples[n]['player_xyz'],previous)]
                self.assertEqual(expected,samples[n]['player_motion_78b64_s32x3'])
                self.assertEqual(expected,samples[n]['player_motion_78b4c_s32x3'])
            self.assertEqual([0x78], [s['class_id'] for s in samples[33]['dynamic_rows']['active']])
            self.assertEqual([0x78,0x26], [s['class_id'] for s in samples[35]['dynamic_rows']['active']])
            for kind in ('pose','matrix','closure','approval','missing sample'):
                changed=copy.deepcopy(rows)
                if kind=='closure': changed[-1]['final_full_checks_passed']=False
                elif kind=='missing sample': changed.pop(next(i for i,r in enumerate(changed) if r.get('ordinal')==34))
                else:
                    s=next(r for r in changed if r.get('kind')=='sample' and r['ordinal']==33)
                    if kind=='approval': s['runtime_pass']=True
                    else:
                        raw=bytearray.fromhex(s['wizard']['raw_hex']);raw[0x3e if kind=='pose' else 0x20]^=1
                        s['wizard']['raw_hex']=raw.hex()
                with self.assertRaises(ValueError): recorded_witness(changed)


if __name__ == '__main__':
    SOURCE, ISSUED, RECORDED = options(sys.argv[1:])
    unittest.main(argv=[sys.argv[0]], verbosity=2)
