#!/usr/bin/env python3
"""Independent fixed-capacity native collision-index/readback audit; no game execution."""
import argparse,hashlib,json,struct
from pathlib import Path
from fractions import Fraction as F
from itertools import product
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--plan',type=Path,required=True);p.add_argument('--ram',type=Path);p.add_argument('--report',type=Path,required=True);a=p.parse_args();plan=json.loads(a.plan.read_text());source=Path(plan['SourceImagePath']);candidate=Path(plan['OutputImagePath'])
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
assert sha(source)=='fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37'
def logical(path,at,n):
 with path.open('rb') as f:
  out=bytearray()
  while n:
   sector,within=divmod(at,2048);take=min(n,2048-within);f.seek(sector*2352+24+within);b=f.read(take);assert len(b)==take;out.extend(b);n-=take;at+=take
  return bytes(out)
def wad(path,at,n):return logical(path,37*2048+at,n)
patches=plan['Patches'];treePatch=next(q for q in patches if q['Kind']=='collision-index-block-tree');blockPatch=next(q for q in patches if q['Kind']=='collision-index-blocks');tw=int(treePatch['WadRelativeOffset'],16);bw=int(blockPatch['WadRelativeOffset'],16);headerWad=tw-28
headers=[struct.unpack('<7I',wad(f,headerWad,28)) for f in (source,candidate)];assert headers[0]==headers[1];count,flags,treeRel,blockRel,triRel,assignRel,flagRel=headers[0];assert headerWad+treeRel==tw and headerWad+blockRel==bw
triWad=headerWad+triRel
edited={ (int(q['WadRelativeOffset'],16)-triWad)//12 for q in patches if q['Kind']=='collision-triangle-fan'};assert edited and all(0<=i<count for i in edited)
def decode(f):
 tree=wad(f,tw,treePatch['ByteLength']);blocks=wad(f,bw,blockPatch['ByteLength']);word=lambda b,o:struct.unpack_from('<H',b,o)[0]
 cells={}
 def segment(at):
  n=word(tree,at);assert n<=256 and at+(n+1)*2<=len(tree);return [word(tree,at+2+i*2) for i in range(n)]
 for z,yAt in enumerate(segment(0)):
  if yAt==65535:continue
  for y,xAt in enumerate(segment(yAt)):
   if xAt==65535:continue
   for x,group in enumerate(segment(xAt)):
    if group==65535:continue
    at=group*2;first=word(blocks,at);assert first&32768;seq=[first&32767];at+=2
    while not word(blocks,at)&32768:seq.append(word(blocks,at));at+=2
    assert all(i<count for i in seq);cells[(x,y,z)]=seq
 return cells
before,after=decode(source),decode(candidate);assert set(i for rows in before.values() for i in rows)==set(i for rows in after.values() for i in rows)
changed=[]
for cell in before.keys()|after.keys():
 old,new=before.get(cell,[]),after.get(cell,[])
 assert [i for i in old if i not in edited]==[i for i in new if i not in edited],('unaffected native query order',cell)
 retained=[i for i in old if i in new];assert new[:len(retained)]==retained,('common native query order',cell)
 assert all(i in edited for i in new[len(retained):]),('only moved rows may append',cell)
 if old!=new:changed.append({'cell':cell,'before':old,'after':new})
def points(i):
 x,y,z=struct.unpack('<III',wad(candidate,triWad+i*12,12));signed=lambda n:n-512 if n&256 else n
 q=(x&16383,y&16383,z&16383)
 return [q,(q[0]+signed(x>>14&511),q[1]+signed(y>>14&511),q[2]+(z>>16&255)),(q[0]+signed(x>>23&511),q[1]+signed(y>>23&511),q[2]+(z>>24&255))]
def intersects(ps,cell):
 poly=[tuple(map(F,q)) for q in ps]
 for axis in range(3):
  for bound,minimum in [(cell[axis]*256,True),((cell[axis]+1)*256,False)]:
   output=[]
   for current,nxt in zip(poly,poly[1:]+poly[:1]):
    inside=lambda q:q[axis]>=bound if minimum else q[axis]<=bound
    ca,na=inside(current),inside(nxt)
    if ca:output.append(current)
    if ca!=na:
     t=(F(bound)-current[axis])/(nxt[axis]-current[axis]);output.append(tuple(current[j]+t*(nxt[j]-current[j]) for j in range(3)))
   poly=output
   if not poly:return False
 return all(any(q[axis]<(cell[axis]+1)*256 for q in poly) for axis in range(3))
fan=[]
for i in sorted(edited):
 ps=points(i);expected={c for c in product(*(range(min(q[j] for q in ps)//256,max(q[j] for q in ps)//256+1) for j in range(3))) if intersects(ps,c)};actual={c for c,rows in after.items() if i in rows};assert actual==expected,('exact rational triangle/cell intersection',i,sorted(expected-actual),sorted(actual-expected));fan.append({'row':i,'points':ps,'cells':sorted(actual)})
assert wad(source,headerWad+assignRel,count)==wad(candidate,headerWad+assignRel,count);assert wad(source,headerWad+flagRel,flags)==wad(candidate,headerWad+flagRel,flags)
ramProof=None
if a.ram:
 ram=a.ram.read_bytes();assert len(ram)==2*1024*1024
 for q in patches:
  actual=wad(candidate,int(q['WadRelativeOffset'],16),q['ByteLength']);found=[];start=0
  while True:
   at=ram.find(actual,start)
   if at<0:break
   found.append(at);start=at+1
  assert len(found)==1,('unique loaded native patch',q['Kind'],len(found))
 ramProof={'sha256':sha(a.ram),'loadedPatches':len(patches)}
receipt={'source':sha(source),'candidate':sha(candidate),'plan':sha(a.plan),'triangles':count,'flagsPrefixCount':flags,'editedRows':sorted(edited),'beforeCells':len(before),'afterCells':len(after),'changedCells':changed,'fan':fan,'unaffectedQueryOrderPreserved':True,'commonQueryOrderPreserved':True,'onlyNewMembershipsAppended':True,'fullActiveTriangleSetPreserved':True,'rationalTriangleBoxMembershipExact':True,'assignmentAndFlagsBytesPreserved':True,'ram':ramProof,'nativeControlExecution':False,'queryTiePolicy':'Native vertical query8004DA30 rejects equal-height replacement; retained rows retain first-hit priority. Newly entered memberships append.'};a.report.parent.mkdir(parents=True,exist_ok=True);a.report.write_text(json.dumps(receipt,indent=2));print('TERRAIN_NATIVE_INDEX_READBACK_OK edited='+str(len(edited))+' changedCells='+str(len(changed))+' nativeRuntime=false')
