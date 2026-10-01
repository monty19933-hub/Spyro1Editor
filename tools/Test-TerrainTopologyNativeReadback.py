#!/usr/bin/env python3
"""Independent native appended floor/lookup/prefix/visibility readback. No emulator."""
import argparse,json,struct,hashlib
from pathlib import Path
from itertools import product
from terrain_native_readback import sha,wad,intersects
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--plan',type=Path,required=True);p.add_argument('--report',type=Path,required=True);p.add_argument('--visibility',choices=['source','all'],default='source');a=p.parse_args();plan=json.loads(a.plan.read_text());source=Path(plan['SourceImagePath']);candidate=Path(plan['OutputImagePath']);assert sha(source)=='fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37';patch=next(q for q in plan['Patches'] if q['Kind']=='terrain-coupled-topology');base=int(patch['WadRelativeOffset'],16);size=patch['ByteLength'];models=[wad(f,base,size) for f in (source,candidate)]
word=lambda b,o:struct.unpack_from('<I',b,o)[0]
def components(data):
 at=0;out=[]
 for i in range(6):
  n=word(data,at);assert n>=4 and n%4==0 and at+n<=len(data);out.append(data[at:at+n]);at+=n
 return out,data[at:]
(old,suffixOld),(new,suffixNew)=map(components,models)
for i in [0,3,5]:assert old[i]==new[i],('immutable native component',i)
assert suffixOld.rstrip(b'\0')==suffixNew.rstrip(b'\0'),'portal/particle/sound suffix content preserved'
assert len(suffixOld)>len(suffixNew),'growth only consumes original allocation padding'
for i in range(len(old)):
 if i not in [1,2,4]:assert old[i]==new[i]
def collision(data):
 n,flags,tree,blocks,tri,assign,flag=struct.unpack_from('<7I',data,4);tree+=4;blocks+=4;tri+=4;assign+=4;flag+=4
 assert tree==32 and assign==tri+12*n and flag>=assign+n and flag+flags<=len(data)
 t=data[tree:blocks];b=data[blocks:tri];u=lambda x,o:struct.unpack_from('<H',x,o)[0];cells={}
 def segment(at):
  count=u(t,at);assert count<=256 and at+(count+1)*2<=len(t);return [u(t,at+2+i*2) for i in range(count)]
 for z,yp in enumerate(segment(0)):
  if yp==65535:continue
  for y,xp in enumerate(segment(yp)):
   if xp==65535:continue
   for x,gp in enumerate(segment(xp)):
    if gp==65535:continue
    at=gp*2;first=u(b,at);assert first&32768;seq=[first&32767];at+=2
    while not u(b,at)&32768:seq.append(u(b,at));at+=2
    assert all(i<n for i in seq);cells[(x,y,z)]=seq
 return {'n':n,'flags':flags,'rows':data[tri:assign],'assignments':data[assign:assign+n],'prefix':data[flag:flag+flags],'cells':cells}
assert source.stat().st_size==candidate.stat().st_size
from terrain_native_readback import logical
assert logical(source,53875*2048,0x66000)==logical(candidate,53875*2048,0x66000), 'native EXE/copy-buffer owners unchanged'
directory=logical(source,37*2048,0x800);assert directory==logical(candidate,37*2048,0x800)
entryAt,entrySize=struct.unpack_from('<II',directory,10*8);entryBefore=wad(source,entryAt,entrySize);entryAfter=wad(candidate,entryAt,entrySize);relative=base-entryAt;assert entryBefore[:relative]==entryAfter[:relative] and entryBefore[relative+size:]==entryAfter[relative+size:]
first,last=map(collision,[old[4],new[4]]);n=first['n'];assert n==7209 and last['n']==7211;assert first['flags']==last['flags']==903;assert last['rows'][:12*n]==first['rows'];assert last['assignments'][:n]==first['assignments'];assert first['prefix']==last['prefix'];assert set(i for rows in last['cells'].values() for i in rows)==set(i for rows in first['cells'].values() for i in rows)|set(range(n,last['n']))
for cell in first['cells'].keys()|last['cells'].keys():
 assert [i for i in last['cells'].get(cell,[]) if i<n]==first['cells'].get(cell,[]),('native query order',cell)
 assert all(i>=n for i in last['cells'].get(cell,[])[len(first['cells'].get(cell,[])):])
def points(bytes):
 x,y,z=struct.unpack('<III',bytes);signed=lambda n:n-512 if n&256 else n;q=(x&16383,y&16383,z&16383)
 return [q,(q[0]+signed(x>>14&511),q[1]+signed(y>>14&511),q[2]+(z>>16&255)),(q[0]+signed(x>>23&511),q[1]+signed(y>>23&511),q[2]+(z>>24&255))]
appended=[]
for row in range(n,last['n']):
 ps=points(last['rows'][row*12:row*12+12]);expected={c for c in product(*(range(min(q[j] for q in ps)//256,max(q[j] for q in ps)//256+1) for j in range(3))) if intersects(ps,c)};actual={c for c,rows in last['cells'].items() if row in rows};assert actual==expected,('exact rational membership',row,expected-actual,actual-expected)
 matches=[]
 for oldRow in range(n):
  original=points(first['rows'][oldRow*12:oldRow*12+12]);translated=[(x+160,y,z) for x,y,z in original]
  if set(translated)==set(ps):matches.append(oldRow)
 assert len(matches)==1 and matches[0]>=first['flags'];assert last['assignments'][row]==first['assignments'][matches[0]]
 appended.append({'row':row,'copiedFrom':matches[0],'points':ps,'cells':sorted(actual),'flag':255})
oldEnv,newEnv=old[1],new[1];sectorCount=word(oldEnv,4);assert word(newEnv,4)==sectorCount+1
for i in range(sectorCount):
 oldAt=4+word(oldEnv,8+4*i);newAt=4+word(newEnv,8+4*i);oldEnd=4+word(oldEnv,12+4*i) if i+1<sectorCount else len(oldEnv);assert oldEnv[oldAt:oldEnd]==newEnv[newAt:newAt+oldEnd-oldAt],('retained native HP/LP sector',i)
sector=4+word(newEnv,8+4*sectorCount);hpLp=tuple(newEnv[sector+16:sector+24]);assert hpLp[:7]==(4,4,1,0,4,4,1) and hpLp[7]==hpLp[0]+hpLp[1]+2*hpLp[2]
def visibility(b):
 count=word(b,8);end=4+word(b,4);groups=[]
 for i in range(count):
  at=4+word(b,12+4*i);rows=[]
  while b[at]!=255:rows.append(b[at]);at+=1;assert at<end
  groups.append(rows)
 return groups,b[end:]
(og,os),(ng,ns)=map(visibility,[old[2],new[2]]);assert len(og)==len(ng) and os==ns
for prior,final in zip(og,ng):assert [i for i in final if i<sectorCount]==prior;assert (sectorCount in final)==(a.visibility=="all" or 90 in prior)
receipt={'source':sha(source),'candidate':sha(candidate),'modelAllocationBytes':size,'modelAllocationUnchanged':True,'nativeExeCopyBufferUnchanged':True,'WadEntryHeaderAndOutsideModelUnchanged':True,'componentGrowthBytes':len(suffixOld)-len(suffixNew),'trailingZeroBytesMeasured':len(suffixNew)-len(suffixNew.rstrip(b'\0')),'oldRows':n,'newRows':last['n'],'flagPrefix':last['flags'],'newSector':sectorCount,'hpLpCounts':hpLp,'appended':appended,'originalSectorPayloadsPreserved':True,'originalCollisionRowsAssignmentsFlagsPreserved':True,'allOriginalMembershipsAndQueryOrderPreserved':True,'newRationalMembershipExact':True,'nativeVisibilityMode':a.visibility,'immutableComponentAndOpaqueSuffixContentPreserved':True,'runtime':False,'scope':'exact Artisans floor90:0 copy +X160; no general structural acceptance'};a.report.parent.mkdir(parents=True,exist_ok=True);a.report.write_text(json.dumps(receipt,indent=2));print('TERRAIN_TOPOLOGY_NATIVE_READBACK_OK rows7209to7211 flags903 HP4 LP4 copiedSector='+str(sectorCount)+' nativeRuntime=false')
