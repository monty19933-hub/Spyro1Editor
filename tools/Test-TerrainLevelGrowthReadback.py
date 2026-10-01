#!/usr/bin/env python3
"""Authenticated named native full-entry terrain growth audit; no emulator."""
import argparse,base64,json,struct
from pathlib import Path
from terrain_native_readback import sha,logical,wad,word,components,collision,points,intersects
from itertools import product
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--plan',type=Path,required=True);p.add_argument('--report',type=Path,required=True);a=p.parse_args();plan=json.loads(a.plan.read_text());s=Path(plan['SourceImagePath']);c=Path(plan['OutputImagePath']);assert sha(s)=='fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37'
r=plan['NativeLevelReplacement'];index=r['WadEntry'];assert not plan['Patches'] and index==plan['TextureAssetWadIndex'];ds,dc=[logical(f,37*2048,0x800) for f in (s,c)]
def entry(f,d,i):
 o,n=struct.unpack_from('<II',d,8*i);return wad(f,o,n) if n else b''
before,after=entry(s,ds,index),entry(c,dc,index);assert before==base64.b64decode(r['Before']) and after==base64.b64decode(r['After'])
growth=len(after)-len(before);assert growth==0x800
unchanged=[]
for i in range(256):
 if i!=index:assert entry(s,ds,i)==entry(c,dc,i),('unrelated WAD entry including PETE/overlay',i);unchanged.append(i)
def executable(f):
 pvd=logical(f,16*2048,2048);directory=logical(f,word(pvd,158)*2048,word(pvd,166));at=0
 while at<len(directory):
  size=directory[at]
  if not size:at=(at//2048+1)*2048;continue
  row=directory[at:at+size]
  if row[33:33+row[32]]==b'SCUS_942.28;1':return logical(f,word(row,2)*2048,word(row,10)),word(row,2)
  at+=size
 raise AssertionError('native executable ISO extent')
exe,oldExeLba=executable(s);other,newExeLba=executable(c);assert exe==other
sf=[[struct.unpack_from('<II',b,8*i) for i in range(10)] for b in (before,after)];sf=[rows[:next((i for i,x in enumerate(rows) if x==(0,0)),10)] for rows in sf];old,new=sf;assert len(old)==len(new)
for i,((o,n),(o2,n2)) in enumerate(zip(old,new)):
 assert o2==o+(growth if i>1 else 0) and n2==n+(growth if i==1 else 0),('native subfile relocation',i)
 if i!=1:assert before[o:o+n]==after[o2:o2+n2],('unmodified payload including model packages/scene fixups',i)
header=bytearray(before[:0x800]);struct.pack_into('<I',header,12,old[1][1]+growth)
for i in range(2,len(old)):struct.pack_into('<I',header,8*i,old[i][0]+growth)
roots=[]
for i in range(64):
 o=word(before,0x50+4*i)
 if o not in (0,0xffffffff):
  assert old[2][0]<=o<old[2][0]+old[2][1],('source model root owner',i,hex(o));struct.pack_into('<I',header,0x50+4*i,o+growth);roots.append(i)
assert bytes(header)==after[:0x800], 'header changes exactly subfile1 length/later offsets/all model roots'
oc,os=components(before[old[1][0]:old[1][0]+old[1][1]]);nc,ns=components(after[new[1][0]:new[1][0]+new[1][1]])
for i in (0,3,5):assert oc[i]==nc[i]
def soundEnd(b,sf):
 start,length=sf[1];at=start+sum(map(len,components(b[start:start+length])[0]));n=word(b,at);assert n<=256;at+=4
 for i in range(n):
  pointsCount=word(b,at+4);assert 0<pointsCount<=4096;at+=0x34+12*pointsCount;at+=word(b,at)
 particle=word(b,at);assert particle>=4 and particle%4==0;at+=particle
 sound=word(b,at);assert sound>=4 and sound%4==0 and at+sound<=start+length
 return at+sound
oe,ne=soundEnd(before,old),soundEnd(after,new);assert os[:oe-old[1][0]-sum(map(len,oc))]==ns[:ne-new[1][0]-sum(map(len,nc))], 'complete source portal/particle/sound owner bytes preserved'
assert not any(before[oe:old[1][0]+old[1][1]]) and not any(after[ne:new[1][0]+new[1][1]])
# Constant-propagate the original native per-level initializer to recover its
# concrete copy-buffer store, independent of the compiler's recorded value.
base=0x8000f800;pc=word(exe,0x800113a4-base+4*r['LevelId']);regs=[None]*32;regs[0]=0;copy=None
sign=lambda x:x-65536 if x&32768 else x
for i in range(96):
 w=word(exe,pc-base);op=w>>26;rs=w>>21&31;rt=w>>16&31;imm=w&65535
 if op==15:regs[rt]=imm<<16
 elif op==9:regs[rt]=None if regs[rs] is None else (regs[rs]+sign(imm))&0xffffffff
 elif op==43 and regs[rs] is not None and regs[rt] is not None:
  if (regs[rs]+sign(imm))&0xffffffff==0x800785d8:copy=regs[rt]
 elif op==2:
  target=(pc+4&0xf0000000)|(w&0x3ffffff)<<2
  if target in (0x8005b6d8,0x8005b6e0):break
  pc=target-4
 pc+=4
assert copy is not None
original=copy+oe-old[1][0]+old[2][1]+old[3][1];final=copy+ne-new[1][0]+new[2][1]+new[3][1]
assert original==r['OriginalRuntimeEnd'] and final==r['FinalRuntimeEnd'] and final<=0x80187bb0-1024
fc,lc=collision(oc[4]),collision(nc[4]);assert fc['n']==lc['n'] and fc['flags']==lc['flags'] and fc['assignments']==lc['assignments'] and fc['prefix']==lc['prefix']
changed=[]
for i in range(fc['n']):
 prior=points(fc['rows'][12*i:12*i+12]);newPoints=points(lc['rows'][12*i:12*i+12])
 if prior==newPoints:continue
 changed.append(i)
 def wanted(ps):return {cell for cell in product(*(range(min(q[j] for q in ps)//256,max(q[j] for q in ps)//256+1) for j in range(3))) if intersects(ps,cell)}
 oldWanted,newWanted=wanted(prior),wanted(newPoints);actualOld={cell for cell,rows in fc['cells'].items() if i in rows};actualNew={cell for cell,rows in lc['cells'].items() if i in rows}
 assert actualNew==(actualOld-(oldWanted-newWanted))|(newWanted-oldWanted),('native preserved common memberships/new rational deltas',i)
for cell in fc['cells'].keys()|lc['cells'].keys():
 oldRows,newRows=fc['cells'].get(cell,[]),lc['cells'].get(cell,[]);assert [i for i in oldRows if i in newRows]==[i for i in newRows if i in oldRows];assert all(i in changed for i in set(oldRows)^set(newRows))
out={'source':sha(s),'candidate':sha(c),'levelId':r['LevelId'],'wadEntry':index,'growthBytes':growth,'completeSourceAndOutputBodiesMatchGuardedPlan':True,'allOther255WadEntriesIncludingPeteUnchanged':True,'nativeExeCopyBufferBytesUnchanged':True,'exeIsoLbaBefore':oldExeLba,'exeIsoLbaAfter':newExeLba,'subfileHeadersRelocatedExactly':True,'modelRootsRelocated':roots,'modelPackageSceneAndFixupPayloadsByteExact':True,'portalParticleSoundPayloadByteExact':True,'copyBuffer':hex(copy),'originalRuntimeEnd':hex(original),'finalRuntimeEnd':hex(final),'finalHeadroomBytes':0x80187bb0-final,'requiredMarginBytes':1024,'finalOwnedPaddingZero':True,'changedCollisionRows':changed,'nativeFlagsAssignmentsAndQueryPriorityPreserved':True,'newMembershipDeltaExact':True,'runtime':False,'scope':'named complete-entry growth source/readback; packet audit is a separate receipt'}
a.report.parent.mkdir(parents=True,exist_ok=True);a.report.write_text(json.dumps(out,indent=2));print('TERRAIN_LEVEL_GROWTH_READBACK_OK growth='+str(growth)+' roots='+str(len(roots))+' finalHeadroom='+str(out['finalHeadroomBytes'])+' nativeRuntime=false')
