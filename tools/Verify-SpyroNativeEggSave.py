#!/usr/bin/env python3
"""Execute exact retail save words in bounded scalar MIPS. No card/emulator I/O."""
import importlib.util, json, struct, sys
from pathlib import Path
root=Path(__file__).resolve().parent.parent; image=Path(sys.argv[1])
s=importlib.util.spec_from_file_location('oracle',root/'src/Spyro.Editor.NativeMobyPersistentOwnerSmoke/source_owner_lifecycle_oracle.py')
o=importlib.util.module_from_spec(s);s.loader.exec_module(o)
checks=[]
def check(ok,why):
 o.require(ok,why); checks.append(why)
spans=[(0x80059594,0x2D0,'ff2bcfe124d934838348cb79807d36cdacc3d82f3cf6773d007c4aa0555e2ae6'),(0x80059864,0x1E4,'b8674b556d8f4154886306731ee50c9812f2cac1d9a224756c110be693d19056'),(0x80016914,0x98,'1c16f5a238ffa3b6160e60fd849b1ff89f7a409cb22a39bf5ae31fb0d5f137ca'),(0x8005956C,0x28,'d83b069d73f991a0ea4b1a318759f6ea66c5606de361242bab29d02c840824db')]
with image.open('rb') as f:
 o.identity(f); exe=o.logical(f,53875*2048,0x66000);check(o.sha(exe)==o.EXE_SHA,'original EXE identity')
 code={}
 for pc,n,h in spans:
  b=exe[pc-0x8000F800:pc-0x8000F800+n];check(o.sha(b)==h,f'exact native range {pc:08X}')
  for i in range(0,n,4):code[pc+i]=struct.unpack_from('<I',b,i)[0]
 # Entire authenticated EXE is available; all instruction/RAM reads stay strict.
 for i in range(0x800,len(exe),4):code[0x8000F800+i]=struct.unpack_from('<I',exe,i)[0]
 from SpyroEggScalarMips import Scalar
 m=Scalar(code);m.map(0x80070000,0x10000);m.map(0x801D0000,0x620,0xA5);m.map(0x801EF000,0x1000)
 scratch=0x801D0010; stop=0xDEADBEE0
 m.poke(0x8007596C,10);m.poke(0x8007582C,3);m.poke(0x80073554,0x80070000)
 pattern=bytes(((i*73+19)&255) for i in range(0x480))
 for i,b in enumerate(pattern):m.poke(0x80077908+i,b,1)
 def invoke(pc):
  m.r[4]=scratch;m.r[29]=0x801EFFF0;m.r[31]=stop;m.run(pc,{stop},maximum=100000)
 invoke(0x80059864)
 saved=m.bytes(scratch,0x600)
 check(saved[0x10C:0x58C]==pattern,'full original serializer preserves ALL 36 x 256 durable object bits')
 check(struct.unpack_from('<I',saved,0x58C)[0]==sum(saved[:0x58C]),'full original serializer checksum covers the bitmap')
 check(m.bytes(scratch-16,16)==bytes([0xA5])*16 and m.bytes(scratch+0x600,16)==bytes([0xA5])*16,'native serializer respects 0x600 slot bounds')
 for lvl in (0,1,2,5,6,29,30,35):
  for row in (0,31,32,127,128,195,254,255):
   wordoff=0x10C+lvl*32+(row>>5)*4
   check(bool((struct.unpack_from('<I',saved,wordoff)[0]>>(row&31))&1)==bool((pattern[lvl*32+row//8]>>(row&7))&1),f'egg bit level={lvl} row={row} is within checksum-covered slot')
 for i in range(0x480):m.poke(0x80077908+i,0,1)
 # The retail function calls native configuration conversion 5CC58; execute
 # that exact helper too, with the ordinary saved option in the input.
 try: invoke(0x80059594)
 except Exception:
  print(f'CPU pc={m.pc:08X} reads={m.reads[-15:]} regs={m.r}',file=sys.stderr); raise
 check(m.bytes(0x80077908,0x480)==pattern,'full original deserializer restores ALL 36 x 256 durable object bits')
 o.identity(f)
 print(json.dumps(dict(marker='NATIVE_EGG_SAVE_SCALAR_OK',checks=len(checks),labels=checks,sourceImageSha256=o.IMAGE_SHA,sourceExeSha256=o.EXE_SHA,serializer='80059864',deserializer='80059594',durableRam=['80077908','80077D88'],saveBitmapOffsets=['10C','58C'],runtime=False,memoryCardIO=False,sourceUnchanged=True,calls=[dict(pc=f'{pc:08X}',callee=f'{target:08X}') for pc,target in m.calls],instructions=m.instructions),indent=2))
