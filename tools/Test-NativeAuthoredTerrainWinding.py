#!/usr/bin/env python3
"""Certify native NCLIP inputs/branches against the actual EXE, then evaluate
integer screen-area culling. This is a packet/branch oracle, not a GTE emulator.
Usage: BIN CAPTURED_RAM REPORT_JSON
"""
import hashlib,json,struct,sys
from pathlib import Path
from SpyroEggScalarMips import require
source,ram_path,report=map(Path,sys.argv[1:4])
def word(b,o):return struct.unpack_from('<I',b,o)[0]
with source.open('rb') as f:
 def sector(lba):f.seek(lba*2352+24);return f.read(2048)
 root=sector(word(sector(16),158));at=0;owner=None
 while root[at]:
  row=root[at:at+root[at]]
  if row[33:33+row[32]]==b'SCUS_942.28;1':owner=(word(row,2),word(row,10))
  at+=len(row)
 require(owner is not None,'Native executable directory owner')
 exe=b''.join(sector(owner[0]+i) for i in range((owner[1]+2047)//2048))[:owner[1]]
# Instructions own both corner selection and acceptance signs, independently
# of the compiler's choice of packed corner order.
cert={0x80026C30:0x48856000,0x80026C34:0x48866800,0x80026C38:0x48887000,
0x80026C44:0x4B400006,0x80026C74:0x0541001A,0x80026C7C:0x48876000,
0x80026C88:0x4B400006,0x80026CB0:0x1D80FFB0,
0x80026404:0x48816000,0x80026408:0x48826800,0x8002640C:0x48837000,
0x80026418:0x4B400006,0x8002643C:0x48846000,0x80026440:0x1CA0000E,
0x80026448:0x4B400006,0x8002646C:0x04E1FFC8,
0x800271D8:0x48856000,0x800271DC:0x48866800,0x800271E0:0x48877000,
0x800271EC:0x4B400006,0x80027220:0x0540FE54,
0x80026604:0x48816000,0x80026608:0x48826800,0x8002660C:0x48837000,
0x80026618:0x4B400006,0x80026640:0x18A0FF53}
for pc,w in cert.items():require(word(exe,pc-0x8000F800)==w,f'Exact native winding instruction {pc:08X}')
def branch(pc,value):
 w=cert[pc];op=w>>26;rt=w>>16&31
 return (value>=0 if rt==1 else value<0) if op==1 else value>0 if op==7 else value<=0 if op==6 else None
def area(p,a,b,c):return (p[b][0]-p[a][0])*(p[c][1]-p[a][1])-(p[b][1]-p[a][1])*(p[c][0]-p[a][0])
def hp(p,idx):
 q=[p[i] for i in idx];a=area(q,0,1,3);b=area(q,2,1,3)
 return branch(0x80026C74,a) or not branch(0x80026CB0,b),(a,b)
def lp(p,idx):
 q=[p[i] for i in idx];a=area(q,0,1,2);b=area(q,3,1,2)
 return branch(0x80026440,a) or not branch(0x8002646C,b),(a,b)
checks=len(cert);cases=[]
for back in (False,True):
 p=[(0,100),(100,100),(100,0),(0,0)]
 if back:p=[(x,-y) for x,y in p]
 for name,h,l in [('old',[0,1,2,3],[0,1,3,2]),('fixed',[3,2,1,0],[3,2,0,1])]:
  accepted,mac=hp(p,h);low,lowmac=lp(p,l)
  require(accepted==low and accepted==(back if name=='old' else not back),'Quad sides agree at both native tiers');checks+=1
  cases.append(dict(packet=name,back=back,HPaccepted=accepted,LPaccepted=low,HPmac0=mac,LPmac0=lowmac))
 a=area(p,2,1,0)
 require(not branch(0x80027220,a)==(not back) and not branch(0x80026640,a)==(not back),'Established HP and corrected LP triangles agree');checks+=1
ram=ram_path.read_bytes();matrix=[struct.unpack_from('<3h',ram,0x76DE4+6*i) for i in range(3)];camera=struct.unpack_from('<3i',ram,0x76DF8)
points=[(31360,31360,8352),(35200,31360,8352),(35200,35200,8352),(31360,35200,8352)]
projected=[];depth=[]
for x,y,z in points:
 v=[(camera[1]-y)/4,(camera[2]-z)/4,(x-camera[0])/4]
 q=[sum(a*b for a,b in zip(row,v))/4096 for row in matrix];depth.append(q[2]);projected.append((round(256*q[0]/q[2]),round(256*q[1]/q[2])))
require(all(z>0 for z in depth),'Captured fixture wholly in front of camera');checks+=1
old,oldmac=hp(projected,[0,1,2,3]);fixed,fixedmac=hp(projected,[3,2,1,0])
require(not old and fixed and not branch(0x80027220,area(projected,2,1,0)),'Captured floor rejects old quad and accepts fixed quad and prior HP triangle');checks+=1
report.parent.mkdir(parents=True,exist_ok=True);report.write_text(json.dumps(dict(checks=checks,sourceSha256=hashlib.sha256(source.read_bytes()).hexdigest(),executableSha256=hashlib.sha256(exe).hexdigest(),ramSha256=hashlib.sha256(ram).hexdigest(),nativeInstructionCount=len(cert),cases=cases,capturedCamera=camera,capturedMatrix=matrix,fixtureRawPoints=points,projectedScreenAreaFixture=projected,fixtureDepth=depth,oldHPmac0=oldmac,fixedHPmac0=fixedmac,scope='Exact EXE input/branch certificate plus integer NCLIP area; captured projection is geometric, not a cycle-accurate GTE simulation',runtimeVerified=False),indent=2)+'\n')
print(f'NATIVE_AUTHORED_TERRAIN_WINDING_OK checks={checks}')
