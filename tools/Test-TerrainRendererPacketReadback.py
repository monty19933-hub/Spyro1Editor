#!/usr/bin/env python3
"""Source-authenticated native HP/LP drawn triangle, culling and FAR-RGB audit.
This decodes GPU strip/culling inputs, not raw face-slot cyclic order. No emulator.
"""
import argparse,json,struct
from collections import Counter
from pathlib import Path
from terrain_native_readback import sha,wad,logical,word,components
from terrain_sector_readback import sector,decoded
p=argparse.ArgumentParser(description=__doc__);p.add_argument('--plan',type=Path,required=True);p.add_argument('--kind',choices=['copy','sculpt','remove'],required=True);p.add_argument('--sector',type=int,default=90);p.add_argument('--report',type=Path,required=True);a=p.parse_args()
plan=json.loads(a.plan.read_text());source=Path(plan['SourceImagePath']);candidate=Path(plan['OutputImagePath']);assert sha(source)=='fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37'
# Original USA executable instructions: HP byte reversal, LP forward indices,
# HP GPU quad position stores, culling flip/double-sided, LP flags/OT, second
# physical HP colour table load and its distant endpoint selection.
windows=[(0x80026B78,'820d0e0082130e0082190e0080200e00'),(0x80026398,'c24d0d00f801293142540d00f8014a31c25a0d00f8016b3142610d00f8018c31'),(0x80026DD0,'0800c5af1400c6af2000c8af2c00c7af'),(0x80026C60,'0200ac31'),(0x80026C90,'0400aa31'),(0x80026430,'8000a7311f00a631'),(0x80026504,'f800c631'),(0x80026E0C,'2030d000'),(0x80026E14,'0000c68c'),(0x80026FB0,'0000c520')]
exe=logical(source,53875*2048,0x66000)
for addr,h in windows:assert exe[addr-0x8000f800:addr-0x8000f800+len(bytes.fromhex(h))]==bytes.fromhex(h),('original native renderer window',hex(addr))
index=plan['TextureAssetWadIndex'];di=logical(source,37*2048,0x800);dc=logical(candidate,37*2048,0x800)
ob,ol=struct.unpack_from('<II',di,index*8);nb,nl=struct.unpack_from('<II',dc,index*8);before,after=wad(source,ob,ol),wad(candidate,nb,nl)
oldModel,oldSize=struct.unpack_from('<II',before,8);newModel,newSize=struct.unpack_from('<II',after,8)
oe=components(before[oldModel:oldModel+oldSize])[0][1];ne=components(after[newModel:newModel+newSize])[0][1];oldCount,newCount=word(oe,4),word(ne,4)
from terrain_renderer_packets import hp,lp
if a.kind=='copy':
 assert newCount==oldCount+1;hs=sector(ne,oldCount);expected=hp(hs);actual=lp(hs)
 # Each emitted HP packet triangle must be an exact translated source face.
 original=hp(sector(oe,a.sector));translated=Counter()
 for (tri,double,ot),count in original.items():
  t=tuple(((xyz[0]+160,xyz[1],xyz[2]),rgb) for xyz,rgb in tri);translated[min(tuple(t[i:]+t[:i]) for i in range(3)),double,ot]+=count
 assert all(translated[k]>=v for k,v in expected.items()),'copied packet geometry/winding/FAR RGB exists in native source +X160'
else:
 expected=hp(sector(ne,a.sector));actual=lp(sector(ne,a.sector))
 for i in range(oldCount,newCount):
  s=sector(ne,i);assert s[20:23]==b'\0\0\0';actual.update(lp(s))
assert actual==expected,('drawn native HP/LP triangles, winding, physical FAR RGB, culling and OT',list((expected-actual).items())[:3],list((actual-expected).items())[:3])
receipt={'source':sha(source),'candidate':sha(candidate),'kind':a.kind,'sector':a.sector,'originalRendererWindowsAuthenticated':[hex(addr) for addr,h in windows],'hpPhysicalFarTable':2,'hpPacketRawSlots':[3,2,0,1],'lpPacketForwardShifts':[26,20,14,8],'sourceHpFlipAppliedToCullingWinding':True,'drawnTriangleCount':sum(expected.values()),'nativeGpuTriangleCoverageWindingFarRgbExact':True,'doubleSidedAndOrderingDepthExact':True,'generatedTransitionBias':0,'generatedBlend':'opaque','oldSectors':oldCount,'newSectors':newCount,'runtime':False,'scope':'named fixture packet-data/source semantics; no screen, distance or GPU execution claim'}
a.report.parent.mkdir(parents=True,exist_ok=True);a.report.write_text(json.dumps(receipt,indent=2));print('TERRAIN_RENDERER_PACKET_READBACK_OK triangles='+str(sum(expected.values()))+' FARtable2=true nativeRuntime=false')
