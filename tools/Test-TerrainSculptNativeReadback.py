#!/usr/bin/env python3
"""Independent exact Artisans90:0 +8 HP/LP/collision/query readback. No emulator."""
import argparse,json,struct
from collections import Counter
from itertools import product
from pathlib import Path
from terrain_native_readback import sha,wad,logical,word,components,collision,points,visibility,intersects
from terrain_sector_readback import sector,hpFaces,lpFaces,canonical
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--plan',type=Path,required=True);p.add_argument('--report',type=Path,required=True);a=p.parse_args()
plan=json.loads(a.plan.read_text());source=Path(plan['SourceImagePath']);candidate=Path(plan['OutputImagePath']);assert sha(source)=='fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37'
patch=next(q for q in plan['Patches'] if q['Kind']=='terrain-coupled-topology');base=int(patch['WadRelativeOffset'],16);size=patch['ByteLength'];(old,suffixOld),(new,suffixNew)=[components(wad(f,base,size)) for f in (source,candidate)]
for i in (0,3,5):assert old[i]==new[i]
assert suffixOld.rstrip(b'\0')==suffixNew.rstrip(b'\0');assert source.stat().st_size==candidate.stat().st_size
assert logical(source,53875*2048,0x66000)==logical(candidate,53875*2048,0x66000)
directory=logical(source,37*2048,0x800);assert directory==logical(candidate,37*2048,0x800);entryAt,entrySize=struct.unpack_from('<II',directory,80);eb,ea=[wad(f,entryAt,entrySize) for f in (source,candidate)];relative=base-entryAt;assert eb[:relative]==ea[:relative] and eb[relative+size:]==ea[relative+size:]
oe,ne=old[1],new[1];n=word(oe,4);assert n<=word(ne,4)<=255
before,after=map(hpFaces,(sector(oe,90),sector(ne,90)));assert len(before)==len(after)==38
translation={}
for prior,final in zip(before,after):
 for (xyz,c),(xyz2,c2) in zip(prior,final):
  assert c==c2
  if xyz!=xyz2:
   assert xyz[:2]==xyz2[:2] and xyz2[2]-xyz[2]==8
   assert translation.setdefault(xyz,xyz2)==xyz2
assert len(translation)==4 and {q[0] for q in before[0]}==translation.keys()
for i in range(n):
 if i!=90:assert sector(oe,i)==sector(ne,i),('unrelated native sector',i)
expected=Counter(canonical(list(face)) for face in after if len(set(q[0] for q in face))>1)
actual=Counter(canonical(list(face)) for face in lpFaces(sector(ne,90)) if len(set(q[0] for q in face))>1)
for i in range(n,word(ne,4)):
 s=sector(ne,i);assert s[20:23]==b'\0\0\0' and s[23]==s[16]+s[17]+2*s[18]
 actual.update(canonical(list(face)) for face in lpFaces(s) if len(set(q[0] for q in face))>1)
from terrain_renderer_packets import hp as hpPackets,lp as lpPackets
packetExpected=hpPackets(sector(ne,90));packetActual=lpPackets(sector(ne,90))
for i in range(n,word(ne,4)):packetActual.update(lpPackets(sector(ne,i)))
assert packetExpected==packetActual,('drawn GPU HP/LP triangle coverage, native FAR RGB, culling winding and OT',packetExpected-packetActual,packetActual-packetExpected)
first,last=map(collision,(old[4],new[4]));assert first['n']==last['n']==7209 and first['flags']==last['flags']==903
assert first['assignments']==last['assignments'] and first['prefix']==last['prefix']
changed=[];nativeBaselineOmissions=[]
for i in range(first['n']):
 prior=points(first['rows'][i*12:i*12+12]);final=points(last['rows'][i*12:i*12+12]);target=[translation.get(q,q) for q in prior]
 assert final==target,('exact complete physical vertex collision fan',i,prior,final,target)
 if prior!=final:
  changed.append(i);wanted={c for c in product(*(range(min(q[j] for q in final)//256,max(q[j] for q in final)//256+1) for j in range(3))) if intersects(final,c)}
  priorWanted={c for c in product(*(range(min(q[j] for q in prior)//256,max(q[j] for q in prior)//256+1) for j in range(3))) if intersects(prior,c)}
  priorMemberships={c for c,rows in first['cells'].items() if i in rows}
  targetMemberships=(priorMemberships-(priorWanted-wanted))|(wanted-priorWanted)
  assert {c for c,rows in last['cells'].items() if i in rows}==targetMemberships,('exact rational membership changes with native common-cell convention',i)
  if priorWanted!=priorMemberships:nativeBaselineOmissions.append({'row':i,'missingBoundaryCells':sorted(priorWanted-priorMemberships),'extraBoundaryCells':sorted(priorMemberships-priorWanted)})
assert changed
for cell in first['cells'].keys()|last['cells'].keys():
 oldRows,newRows=first['cells'].get(cell,[]),last['cells'].get(cell,[])
 assert [i for i in newRows if i in oldRows]==[i for i in oldRows if i in newRows],('retained native query priority',cell)
 assert all(i in changed for i in set(oldRows)^set(newRows)),('unrelated membership',cell)
(og,os),(ng,ns)=map(visibility,(old[2],new[2]));assert os==ns and len(og)==len(ng)
for prior,final in zip(og,ng):assert [i for i in final if i<n]==prior;assert all((i in final)==(90 in prior) for i in range(n,word(ne,4)))
receipt={'source':sha(source),'candidate':sha(candidate),'modelAllocationBytes':size,'modelAllocationUnchanged':True,'nativeExeCopyBufferUnchanged':True,'WadEntryHeaderAndOutsideModelUnchanged':True,'componentGrowthBytes':len(suffixOld)-len(suffixNew),'trailingZeroBytesMeasured':len(suffixNew)-len(suffixNew.rstrip(b'\0')),'changedVertices':translation.__len__(),'changedCollisionRows':changed,'remainingHpLpPolygonsMatched':sum(expected.values()),'nativeGpuTriangleCoverageWindingFarRgbExact':True,'drawnTrianglesMatched':sum(packetExpected.values()),'allUnrelatedNativeSectorPayloadsPreserved':True,'allCommonNativeQueryOrderPreserved':True,'changedRationalMembershipDeltaExact':True,'sourceNativeBoundaryMembershipOmissionsPreserved':nativeBaselineOmissions,'assignmentsAnd903FlagPrefixPreserved':True,'runtime':False,'scope':'exact Artisans90:0 +8; no generic terrain or allocator acceptance'}
a.report.parent.mkdir(parents=True,exist_ok=True);a.report.write_text(json.dumps(receipt,indent=2));print('TERRAIN_SCULPT_NATIVE_READBACK_OK HP_LP='+str(sum(expected.values()))+' changedCollisionRows='+str(len(changed))+' nativeRuntime=false')
