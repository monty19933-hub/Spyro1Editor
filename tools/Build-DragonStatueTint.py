#!/usr/bin/env python3
"""Assemble and verify the per-statue R3000 palette adapter against the shipped payload."""
import struct,json,pathlib,hashlib,re
root=pathlib.Path(__file__).resolve().parent
HOST=0;SITE=0x8002174C;RETURN=0x80021754
code=[];labels={};fix=[]
def i(op,rs,rt,imm):code.append((op<<26)|(rs<<21)|(rt<<16)|(imm&65535))
def r(rs,rt,rd,sh,fn):code.append(rs<<21|rt<<16|rd<<11|sh<<6|fn)
def l(n):labels[n]=HOST+len(code)*4
def br(op,rs,rt,n):fix.append((len(code),n));i(op,rs,rt,0)
def j(a):code.append(2<<26|((a>>2)&0x3ffffff))
def nop():code.append(0)
def move(d,s):r(s,0,d,0,0x21)
Z,AT,V0,V1,A0,A1,A2,A3,T0,T1,T2,T9,GP,RA=0,1,2,3,4,5,6,7,8,9,10,25,28,31
# Site delay slot retains original sll s6,2. Load delay is respected here.
i(35,GP,AT,0x30);nop();r(0,AT,AT,24,2);i(14,AT,AT,0x7E);br(5,AT,Z,'ordinary');nop()
# Exact native unused host tail is private non-reentrant renderer register storage.
i(15,Z,AT,0x8001)
saves=[]
for n,reg in enumerate(range(8,11)):
 saves.append(len(code));i(43,AT,reg,0)
i(35,GP,AT,0x30);i(37,RA,A0,2);i(12,AT,A1,255);r(0,AT,A2,8,2);i(12,A2,A2,255);r(0,AT,A3,16,2);i(12,A3,A3,255)
move(V0,T9);i(15,Z,V1,0x8007);i(9,V1,V1,0x0DF4)

l('loop')
i(36,V0,T0,0);i(36,V0,T1,1);nop();r(T0,T1,T2,0,0x2B);br(4,T2,Z,'green');nop();move(T0,T1)
l('green');i(36,V0,T1,2);nop();r(T0,T1,T2,0,0x2B);br(4,T2,Z,'blue');nop();move(T0,T1)
l('blue')
r(T0,A1,0,0,0x19);r(0,0,T1,0,0x12);r(0,T1,T1,8,2);i(9,V0,V0,4);r(T0,A2,0,0,0x19);r(0,0,T2,0,0x12);i(12,T2,T2,0xFF00);r(T1,T2,T1,0,0x25)
r(T0,A3,0,0,0x19);r(0,0,T2,0,0x12);r(0,T2,T2,8,2);r(0,T2,T2,16,0);r(T1,T2,T1,0,0x25)
i(36,V0,T2,-1);i(9,A0,A0,-1);r(0,T2,T2,24,0);r(T1,T2,T1,0,0x25);i(43,V1,T1,0);br(5,A0,Z,'loop');i(9,V1,V1,4)
l('coloured')
# Match native six-bit specular colour packing, strength32, type0.
r(0,A1,T0,2,2);r(0,T0,T0,12,0);r(0,A2,T1,2,2);r(0,T1,T1,6,0);r(T0,T1,T0,0,0x25);r(0,A3,T1,2,2);r(T0,T1,T0,0,0x25);i(15,Z,AT,0x80);r(T0,AT,T0,0,0x25)
i(15,Z,T9,0x8007);i(9,T9,T9,0x0DF4);i(15,Z,AT,0x8001)
colour_store=len(code);i(43,AT,T0,0)
loads=[]
for reg in range(8,11):loads.append(len(code));i(35,AT,reg,0)
colour_load=len(code);i(35,AT,AT,0)
r(GP,0,0,0,0x13);j(RETURN);nop()
l('ordinary');i(35,GP,AT,0x30);j(RETURN);nop()
# Complete-function hosts were audited against all original native code and WAD owners.
ranges=[(0x80017188,0x800171FC),(0x80017548,0x800175B8),(0x8001771C,0x80017758),(0x80017854,0x80017894)]
DATA=ranges[-1][1]-16
mapping={};segments=[];cursor=0
for idx,(start,end) in enumerate(ranges):
 capacity=(end-start)//4-(4 if idx==len(ranges)-1 else 2)
 take=min(capacity,len(code)-cursor)
 # Keep a branch and its delay instruction together.
 if cursor+take<len(code):
  last=code[cursor+take-1];op=last>>26
  if op in (1,2,3,4,5,6,7) or(op==0 and(last&63)in(8,9)):take-=1
 for n in range(take):mapping[cursor+n]=start+4*n
 segments.append([start,end,cursor,take]);cursor+=take
assert cursor==len(code),(cursor,len(code))
for n,k in enumerate(saves):code[k]|=(DATA+4*n)&65535
for n,k in enumerate(loads):code[k]|=(DATA+4*n)&65535
code[colour_store]|=(DATA+12)&65535;code[colour_load]|=(DATA+12)&65535
for k,n in fix:code[k]|=((mapping[(labels[n]-HOST)//4]-(mapping[k]+4))//4)&65535
out={};blobs=[]
for idx,(start,end,first,count) in enumerate(segments):
 b=bytearray(end-start)
 for k in range(count):struct.pack_into('<I',b,k*4,code[first+k]);out[start+4*k]=code[first+k]
 if first+count<len(code):
  target=mapping[first+count];word=2<<26|((target>>2)&0x3FFFFFF);struct.pack_into('<I',b,count*4,word);out[start+count*4]=word;out[start+count*4+4]=0
 blobs.append(dict(Start=start,End=end,Hex=b.hex()))
hook=dict(Host=ranges[0][0],Site=SITE,Return=RETURN,Data=DATA,WordsByAddress=out,Segments=blobs,Labels={k:mapping[(v-HOST)//4]for k,v in labels.items()})
source=(root.parent/'src/Spyro.Editor.Core/Exporting/NativeDragonStatueColors.cs').read_text()
installed={int(a,16):b for a,_,b in re.findall(r'new\(0x([0-9A-Fa-f]+), "([0-9A-Fa-f]+)", "([0-9A-Fa-f]+)"\)',source)}
for seg in blobs:assert installed[seg['Start']]==seg['Hex'],f"Payload differs at {seg['Start']:08X}"
assert installed[SITE]==struct.pack('<I',2<<26|((ranges[0][0]>>2)&0x3FFFFFF)).hex()
if __name__=='__main__':print('DRAGON_TINT_ASSEMBLY_OK words='+str(len(code))+' bytes='+str(sum(len(bytes.fromhex(s['Hex'])) for s in blobs)))
