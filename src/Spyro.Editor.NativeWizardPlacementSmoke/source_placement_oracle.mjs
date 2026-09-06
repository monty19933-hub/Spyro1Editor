// Independent, read-only raw-sector scene/coordinate oracle. No editor assemblies.
// The C# rational certificate separately checks complete corridor/index/clearance.
import fs from 'node:fs';
import crypto from 'node:crypto';
const path=process.argv[2];
if(!path)throw Error('Usage: node source_placement_oracle.mjs original.bin');
const hash=b=>crypto.createHash('sha256').update(b).digest('hex');
const assert=(ok,why)=>{if(!ok)throw Error(why);};
const expected='fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37';
async function requireOriginal(){
  assert(fs.statSync(path).size===661547040,'image size');
  const h=crypto.createHash('sha256');for await(const b of fs.createReadStream(path))h.update(b);
  assert(h.digest('hex')===expected&&fs.statSync(path).size===661547040,'complete source image');
}
await requireOriginal();const fd=fs.openSync(path,'r');
try{
  function logical(at,n){const b=Buffer.alloc(n);for(let p=0;p<n;){const a=at+p,c=Math.min(n-p,2048-a%2048);
    assert(fs.readSync(fd,b,p,c,Math.floor(a/2048)*2352+24+a%2048)===c,'short sector read');p+=c;}return b;}
  const dark=logical(37*2048+0xF01000,0x28D800),wizard=logical(37*2048+0x2F6B000,0x28A000);
  assert(hash(dark)==='a937aecb3626ffd2311ba136684a34c9f36877054b52557b4524fcacee0e2d97','Dark owner');
  assert(hash(wizard)==='9973493f865b5e86bc17b32c5af9e3d7596e25615618c007ed28e0379161f2d8','Wizard owner');
  const donor=wizard.subarray(0x1CE800,0x1E0000),old=dark.subarray(0x194000,0x1A5800),base=Buffer.alloc(0x11800);
  old.copy(base,0,0,0xEED4);old.copy(base,0xEF24,0xEED4,0x10EF0);old.copy(base,0x10F40,0x10EF0,0x11140);
  donor.copy(base,0xB838,0x6388,0x63E0);donor.copy(base,0xEED4,0xC350,0xC3A0);
  for(const[o,v]of[[0x8C34,129],[0xE038,0xEEC],[0xB838,0xEED4],[0xEED4,0xEEFC],[0x10F40,149],[0x11190,0xB838],[0x11194,0xEED4]])base.writeUInt32LE(v,o);
  assert(hash(base)==='9ac99fdd150133a75aac8298da4b738dcbcdd12a1ab649088f53c300bd1a37c4','independent unplaced scene golden');
  const intent={x:92832,y:25248,z:10445,endX:91808,endY:25248};
  const dx=BigInt(intent.endX-intent.x),dy=BigInt(intent.endY-intent.y),length=dx*dx+dy*dy;
  let nearest=null;
  for(let i=0;i<128;i++){
    const at=0x8C38+i*0x58,ex=BigInt(old.readInt32LE(at+12)-intent.x),ey=BigInt(old.readInt32LE(at+16)-intent.y),dot=ex*dx+ey*dy;
    let n,d=1n;
    if(dot<=0n)n=ex*ex+ey*ey;
    else if(dot>=length)n=(ex-dx)**2n+(ey-dy)**2n;
    else{n=(ex*dy-ey*dx)**2n;d=length;}
    assert(n>3072n**2n*d,'original resident center '+i);
    if(nearest===null||n*nearest.d<nearest.n*d)nearest={index:i,n,d};
  }
  assert(donor.readInt32LE(0xC350+0x38)-donor.readInt32LE(0x6388+20)===287,'source route height offset');
  assert(donor.readInt32LE(0xC350+0x48)===donor.readInt32LE(0xC350+0x38),'source route planar Z');
  const fields=[[0xB844,intent.x],[0xB848,intent.y],[0xB84C,intent.z],
    [0xEF04,intent.x],[0xEF08,intent.y],[0xEF0C,intent.z+287],
    [0xEF14,intent.endX],[0xEF18,intent.endY],[0xEF1C,intent.z+287]];
  const after=Buffer.from(base),inverse=Buffer.from(base);
  for(const[at,value]of fields)after.writeInt32LE(value,at);
  after.copy(inverse);
  for(const[at]of fields)base.copy(inverse,at,at,at+4);
  assert(inverse.equals(base),'exact coordinate-only inverse');
  assert(after.subarray(0x8C38,0xB838).equals(old.subarray(0x8C38,0xB838)),'all original native Moby rows');
  // Independent packed source triangle decoding: these are the four ordinary
  // flat supports identified by the separate rational corridor certificate.
  const cb=0x10BCBC+4,tri=cb+dark.readUInt32LE(cb+16),flags=cb+dark.readUInt32LE(cb+24),fc=dark.readUInt32LE(cb+4);
  const signed9=w=>(w&256)?(w&511)-512:w&511;
  const support=[874,875,921,927].map(i=>{
    const at=tri+i*12,x=dark.readUInt32LE(at),y=dark.readUInt32LE(at+4),z=dark.readUInt32LE(at+8);
    const a=[(x&0x3FFF)*16,(y&0x3FFF)*16,(z&0x3FFF)*16];
    const b=[a[0]+signed9(x>>>14)*16,a[1]+signed9(y>>>14)*16,a[2]+((z>>>16)&255)*16];
    const c=[a[0]+signed9(x>>>23)*16,a[1]+signed9(y>>>23)*16,a[2]+(z>>>24)*16];
    const flag=i<fc?dark[flags+i]:255;
    assert((z&0xC000)===0&&[63,255].includes(flag)&&[a,b,c].every(v=>v[2]===10240),'ordinary ground owner');
    assert((b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0])<0,'upward winding');
    return {index:i,wadOffset:0xF01000+at,flag,a,b,c};
  });
  await requireOriginal();
  console.log(JSON.stringify({source:expected,baseScene:hash(base),finalScene:hash(after),intent,
    fields:fields.map(([at,value])=>({sceneOffset:at,before:base.readInt32LE(at),after:value})),
    nearestOriginalCenter:{index:nearest.index,squaredDistanceNumerator:nearest.n.toString(),squaredDistanceDenominator:nearest.d.toString()},
    sourceSupports:support,scope:'Independent scene bytes, center policy and selected source supports; full continuous corridor/index is separately checked in C#; not body, initial-height, native runtime or publication.',
    runtimeVerified:false,publicationApproved:false},null,2));
}finally{fs.closeSync(fd);}
