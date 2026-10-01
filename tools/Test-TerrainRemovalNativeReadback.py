#!/usr/bin/env python3
"""Exact Artisans90:0 complete HP/LP removal readback. No emulator or generic claim."""
import argparse,json,struct
from pathlib import Path
from collections import Counter
from terrain_native_readback import sha,wad,logical,word,components,collision,points,visibility
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--plan',type=Path,required=True);p.add_argument('--report',type=Path,required=True);a=p.parse_args();plan=json.loads(a.plan.read_text());source=Path(plan['SourceImagePath']);candidate=Path(plan['OutputImagePath']);assert sha(source)=='fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37';patch=next(q for q in plan['Patches'] if q['Kind']=='terrain-coupled-topology');base=int(patch['WadRelativeOffset'],16);size=patch['ByteLength'];(old,suffixOld),(new,suffixNew)=[components(wad(f,base,size)) for f in (source,candidate)]
for i in [0,3,5]:assert old[i]==new[i]
assert suffixOld.rstrip(b'\0')==suffixNew.rstrip(b'\0');assert source.stat().st_size==candidate.stat().st_size;assert logical(source,53875*2048,0x66000)==logical(candidate,53875*2048,0x66000);directory=logical(source,37*2048,0x800);assert directory==logical(candidate,37*2048,0x800);entryAt,entrySize=struct.unpack_from('<II',directory,80);eb,ea=[wad(f,entryAt,entrySize) for f in (source,candidate)];relative=base-entryAt;assert eb[:relative]==ea[:relative] and eb[relative+size:]==ea[relative+size:]
first,last=map(collision,[old[4],new[4]]);n=first['n'];assert n==last['n']==7209;assert first['flags']==last['flags']==903;assert first['assignments']==last['assignments'];assert first['prefix']==last['prefix'];removed={i for i in range(n) if first['rows'][i*12:i*12+12]!=last['rows'][i*12:i*12+12]};assert len(removed)==2
for i in removed:assert len(set(points(last['rows'][i*12:i*12+12])))==1
for cell in first['cells'].keys()|last['cells'].keys():assert last['cells'].get(cell,[])==[i for i in first['cells'].get(cell,[]) if i not in removed],('complete native removal/query order',cell)
assert set(i for rows in last['cells'].values() for i in rows)==set(i for rows in first['cells'].values() for i in rows)-removed
oldEnv,newEnv=old[1],new[1];sourceCount=word(oldEnv,4);newCount=word(newEnv,4);assert sourceCount<newCount<=255

def sector(env,i):
 at=4+word(env,8+4*i);lpv,lpc,lpf,_,hpv,hpc,hpf,offset=env[at+16:at+24];end=at+28+4*(lpv+lpc+2*lpf+hpv+2*hpc+4*hpf);return env[at:end]
def decoded(s,at):
 packed=word(s,at);xy,zpos=word(s,8),word(s,12);x=(xy>>16)+((packed>>21)&2047);y=(xy&65535)+((packed>>10)&2047);z=((zpos>>14)&65535)//4+(packed&1023)
 if word(s,4)&4096:z//=8
 return x,y,z

def hpFaces(s):
 lpv,lpc,lpf,_,hpv,hpc,hpf,_=s[16:24];verts=28+4*(lpv+lpc+2*lpf);colours=verts+4*hpv;faces=colours+8*hpc;out=[]
 for f in range(hpf):
  at=faces+16*f;vs=s[at:at+4];cs=s[at+4:at+8];assert all(i<hpv for i in vs) and all(i<hpc for i in cs)
  out.append(tuple((decoded(s,verts+4*i),word(s,colours+4*c)) for i,c in zip(vs,cs)))
 return out

def lpFaces(s):
 lpv,lpc,lpf,_,hpv,hpc,hpf,offset=s[16:24];assert lpv<=63 and lpc<=63;verts=28;colours=28+4*lpv;faces=colours+4*lpc;out=[]
 for f in range(lpf):
  at=faces+8*f;v,c=word(s,at),word(s,at+4);ids=[v>>shift&63 for shift in [26,20,14,8]];shades=[c>>shift&63 for shift in [26,20,14,8]];assert all(i<lpv for i in ids) and all(i<lpc for i in shades)
  out.append(tuple((decoded(s,verts+4*i),word(s,colours+4*j)) for i,j in zip(ids,shades)))
 return out
oldOwner,newOwner=sector(oldEnv,90),sector(newEnv,90);oldHp,newHp=hpFaces(oldOwner),hpFaces(newOwner);assert len(oldHp)==len(newHp);assert len(set(q[0] for q in newHp[0]))==1;assert newHp[1:]==oldHp[1:]
for i in range(sourceCount):
 if i!=90:assert sector(oldEnv,i)==sector(newEnv,i),('unrelated native sector',i)
assert all(len(set(q[0] for q in face))==1 for face in lpFaces(newOwner)), 'no stale original low-detail floor'
# Canonical cyclic order preserves winding and exact native FAR colour per vertex.
def canonical(face):return min(tuple(face[i:]+face[:i]) for i in range(4))
expected=Counter(canonical(list(face)) for face in oldHp[1:] if len(set(q[0] for q in face))>1);actual=Counter();appended=[]
for i in range(sourceCount,newCount):
 s=sector(newEnv,i);assert s[20:23]==b'\0\0\0';assert s[23]==s[16]+s[17]+2*s[18];polys=lpFaces(s);actual.update(canonical(list(face)) for face in polys);appended.append({'sector':i,'vertices':s[16],'colours':s[17],'faces':s[18]})
assert actual==expected,('remaining HP/new LP exact geometry/FARcolour/winding multiset',expected-actual,actual-expected)
# Each deleted collision row must be a native triangulation of exactly the removed HP polygon.
shape={q[0] for q in oldHp[0]};assert all(set(points(first['rows'][i*12:i*12+12]))<=shape for i in removed)
(og,os),(ng,ns)=map(visibility,[old[2],new[2]]);assert os==ns and len(og)==len(ng)
for prior,final in zip(og,ng):assert [i for i in final if i<sourceCount]==prior;assert all((i in final)==(90 in prior) for i in range(sourceCount,newCount))
receipt={'source':sha(source),'candidate':sha(candidate),'modelAllocationBytes':size,'modelAllocationUnchanged':True,'nativeExeCopyBufferUnchanged':True,'WadEntryHeaderAndOutsideModelUnchanged':True,'componentGrowthBytes':len(suffixOld)-len(suffixNew),'trailingZeroBytesMeasured':len(suffixNew)-len(suffixNew.rstrip(b'\0')),'triangleRows':n,'flagPrefix':first['flags'],'removedRows':sorted(removed),'oldSectors':sourceCount,'newSectors':newCount,'lpOnlySectors':appended,'remainingHpPolygonsMatched':sum(expected.values()),'remainingHPtoNewLPGeometryFarColoursWindingExact':True,'originalLPOwnerFacesAllDegenerate':True,'allOtherHPSectorsPreserved':True,'allRemovedNativeMembershipsGone':True,'allSurvivorNativeQueryOrderPreserved':True,'nativeAssignmentFlagPrefixPreserved':True,'immutableComponentsAndOpaqueSuffixContentPreserved':True,'runtime':False,'scope':'exact Artisans90:0 removal; fixed original allocation/source ownership only'};a.report.parent.mkdir(parents=True,exist_ok=True);a.report.write_text(json.dumps(receipt,indent=2));print('TERRAIN_REMOVAL_NATIVE_READBACK_OK removedRows='+str(sorted(removed))+' matchedRemainingHP='+str(sum(expected.values()))+' lpOnlySectors='+str(newCount-sourceCount)+' runtime=false')
