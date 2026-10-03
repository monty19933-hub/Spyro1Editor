#!/usr/bin/env python3
"""Exact native SPU consumer spans and strict scalar DMA block counter. Usage: BIN REPORT_JSON. No image writes/emulator/audio claim."""
from pathlib import Path
import sys,struct,re,json,hashlib
source,report=map(Path,sys.argv[1:3])
from SpyroEggScalarMips import Scalar
f=source.open('rb')
def u32(b,o):return struct.unpack_from('<I',b,o)[0]
def sector(lba):f.seek(lba*2352+24);return f.read(2048)
root=sector(u32(sector(16),158));at=0;owner=None
while root[at]:
 row=root[at:at+root[at]]
 if row[33:33+row[32]]==b'SCUS_942.28;1':owner=(u32(row,2),u32(row,10))
 at+=len(row)
assert owner is not None and owner[1]==0x66000
exe=b''.join(sector(owner[0]+i) for i in range(owner[1]//2048))
u=lambda b,o:struct.unpack_from('<I',b,o)[0]
spans=[{'name': 'SPU-init-disabled-reverb', 'pc': '0x8005bbf4', 'exeOffset': 312308, 'bytes': 660, 'sha256': 'a21dba15beee45866129f9f3adeffa8fe95e3eac47fcc0781f1f5d0007b261f9'}, {'name': 'SPU-DMA-BCR', 'pc': '0x8005c3c0', 'exeOffset': 314304, 'bytes': 76, 'sha256': 'ef132cef57f73b1110a2b080b8f56b633baa023a939816beb1d6d62fed848058'}, {'name': 'SPU-write-cap', 'pc': '0x8005cac4', 'exeOffset': 316100, 'bytes': 96, 'sha256': '434a098a521f8a9fc6d1cf1841b4c9ae1ebd1ff946baf5ca019dba734c25cce3'}, {'name': 'SPU-transfer-start', 'pc': '0x8005cb24', 'exeOffset': 316196, 'bytes': 88, 'sha256': '356db920d194b5b0823ce052e38699d4b15b6aa9a3223896dbab4c87a8ef6065'}, {'name': 'SPU-voice-start-repeat-addresses', 'pc': '0x8005d334', 'exeOffset': 318260, 'bytes': 60, 'sha256': 'f4f9af30ac702db2ebc8bc8b0b22445bb2f1a56040ebe321f1c8422343c56202'}, {'name': 'SPU-address-units', 'pc': '0x8005c588', 'exeOffset': 314760, 'bytes': 196, 'sha256': '68b10fb374a70300499c0d27c1d50bdd2ec55f39585fb8e2c0444bdf9112a33a'}, {'name': 'native-play-descriptor-reader', 'pc': '0x80055c60', 'exeOffset': 287840, 'bytes': 576, 'sha256': '24c705a0c245d71bee3f1f152cbe0e7da6de18d827621c510819ddfb1e34d795'}]
code={}
for span in spans:
 off=span['exeOffset'];n=span['bytes'];assert hashlib.sha256(exe[off:off+n]).hexdigest()==span['sha256'],span['name']
 pc=int(span['pc'],16)
 for i in range(0,n,4):code[pc+i]=u(exe,off+i)
checked=sum(span['bytes']//4 for span in spans)
cases=[]
for n in [16,64,0x7e800,0x7eff0]:
 c=Scalar(code);c.map(0x80090000,8);c.r[16]=0x80090004;c.poke(0x80090004,n)
 c.map(0x80073558,8);c.map(0x800735A8,8);c.poke(0x80073558,0x1f8010c0);c.poke(0x8007355c,0x1f8010c4);c.poke(0x800735a8,0x80090010);c.map(0x1f8010c0,8)
 c.run(0x8005C3C0,{0x8005C40C},maximum=30);bcr=c.read(0x1f8010c4);actual=(bcr>>16)*(bcr&65535)*4;assert actual==(n+63)//64*64
 cases.append(dict(requested=n,bcr=hex(bcr),actualDmaBytes=actual,wrapBytes=max(0,0x1010+actual-0x80000)))
# Exact init disables voice reverb masks and volume; final CNT C000 lacks bit7.
for pc,w in [(0x8005BC48,0xa44001aa),(0x8005BD1C,0xa4400184),(0x8005BD20,0xa4400186),(0x8005BD2C,0xa4400198),(0x8005BD30,0xa440019a),(0x8005BE58,0x3403c000),(0x8005BE5C,0xa48301aa)]:assert code[pc]==w
result=dict(scope='Actual original executable/PsyQ source word matching and strict scalar DMA block counter only; no emulator/audio/physical-device proof',psyqInstructionsMatched=checked,nativeSpans=spans,dmaCases=cases,spuRegions=dict(capture=[0,0x1000],nativeDummyBlock=[0x1000,0x1010],descriptorSamples=[0x1010,0x80000],reverbEnabledByNativeInit=False),physicalStorageMaximum=0x7f000,descriptorExtentMaximum=0x7eff0,finalStoragePaddingBytes=16,hardwareSources=['https://psx-spx.consoledev.net/ps1/spu/soundprocessingunitspu/','https://raw.githubusercontent.com/stenzek/duckstation/master/src/core/spu.cpp'])
report.parent.mkdir(parents=True,exist_ok=True);report.write_text(json.dumps(result,indent=2));print('NATIVE_SOUND_DMA_CONTRACT_OK instructions='+str(checked)+' cases='+str(len(cases))+' BCR='+cases[-1]['bcr']+' DMA='+str(cases[-1]['actualDmaBytes']))
