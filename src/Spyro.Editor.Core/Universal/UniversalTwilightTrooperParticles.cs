using System.Security.Cryptography;
namespace Spyro.Editor.Core.Universal;

/// <summary>Original Twilight trooper effects into the retained Dark Hollow particle manager.
/// Only13 is copied; equivalent native1/70/79 remain in the destination manager.</summary>
public sealed class UniversalTwilightTrooperParticleCertificate
{
 internal UniversalTwilightTrooperParticleCertificate(UniversalOverlay source,bool impact)
 {Source=new(source.Bytes.ToArray(),source.Bindings.ToDictionary());CopiesImpact=impact;}
 internal UniversalOverlay Source {get;}
 public bool CopiesImpact {get;}
 public string SourceLevelKey=>"twilightharbor";
 public string DestinationLevelKey=>"darkhollow";
 internal UniversalOwnedParticleCertificate? OwnedDispatcher=>!CopiesImpact?null:new(new(SourceLevelKey,Source,
  UniversalTwilightTrooperParticles.Spawn13,UniversalTwilightTrooperParticles.Update13),DestinationLevelKey,0x80083274,
  UniversalTwilightTrooperParticles.PatchImpact,d=>UniversalTwilightTrooperParticles.RequireDestination(d,null));
}
public static class UniversalTwilightTrooperParticles
{
 internal static readonly UniversalOwnedParticleFunction Spawn13=new(0x80084EAC,4440,"8f90a0a5e798998a6ecaa168d6937be540e3a61bde05431127dc62f86ae7a324",0x8007ACD4,0x80085FAC,[13]);
 internal static readonly UniversalOwnedParticleFunction Update13=new(0x80083B4C,4960,"de614fce5e088ec3ecbbf7f376155ec23b930a591438f595054b200d9856175b",0x8007AB90,0x80084E5C,[13]);
 public static UniversalTwilightTrooperParticleCertificate Capture(UniversalLevelData data,UniversalOverlay source,UniversalOverlay destination,byte[] exe,bool impact)
 {
  UniversalTwilightTrooperPlacement.RequireSource(data,source);RequireSource(source);RequireDestination(destination,exe);RequireEquivalentLeaves(source,destination);
  return new(source,impact);
 }
 internal static void RequireBundle(UniversalOverlay source,UniversalOverlay destination,IReadOnlyList<int> classes,IReadOnlyList<UniversalOwnedParticleCertificate>? owned,byte[] exe)
 {
  if(!classes.Any(c=>c is 0x93 or 0x94))return;
  RequireSource(source);
  if(destination.Bindings[UniversalExeBindings.ParticleSpawnGlobal].Value!=0x800844A0)return; //Other hosts retain explicit unverified dependency status.
  RequireDestination(destination,exe);RequireEquivalentLeaves(source,destination);
  if(classes.Contains(0xB0))
  {Require(owned?.Any(c=>c.SourceLevelKey=="twilightharbor"&&c.TypeIds.SequenceEqual(new[]{13})&&c.Profile.Source.Bytes.SequenceEqual(source.Bytes))==true,"B0 impact13 requires its issued source dispatcher");Require(!(owned?.Any(c=>c.SourceLevelKey!="twilightharbor"&&c.TypeIds.Contains(13))??false),"Two copied sources cannot independently update the same particle13 lifetime.");}
 }
 internal static void RequireSource(UniversalOverlay o)
 {
  UniversalTwilightTrooperPlacement.RequireOverlay(o);
  Require(o.Bindings[UniversalExeBindings.ParticleSpawnGlobal].Value==Spawn13.Entry&&o.Bindings[UniversalExeBindings.ParticleUpdateGlobal].Value==Update13.Entry,"source callback identity changed");
  RequireOwner(o,Spawn13.Entry,Spawn13.Length,Spawn13.Sha);RequireOwner(o,Update13.Entry,Update13.Length,Update13.Sha);
 }
 internal static void RequireDestination(UniversalOverlay o,byte[]? exe)
 {
  foreach(var b in new UniversalExeBinding[]{new(0x800758E4,0x8005A878,0x8005A87C,0x800844A0),new(0x800756BC,0x8005A888,0x8005A88C,0x80083274)})
   Require(o.Bindings.TryGetValue(b.Global,out var got)&&got==b&&(exe==null||UniversalExeBindings.Read(exe,12)[b.Global]==b),"destination particle callback/executable identity changed");
  foreach(var owner in HostOwners)RequireOwner(o,owner.Pc,owner.Length,owner.Sha);
  Require(o.Word(0x8007ACB8+13*4)==0x8008553C&&o.Word(0x8007AB74+13*4)==0x80084450,"destination13 must remain its exact native no-op loop tail");
  if(exe!=null)foreach(var owner in Native)
  {int at=checked((int)(owner.Pc-UniversalExeBindings.ExeBase));Require(at>=0&&at+owner.Length<=exe.Length&&Hash(exe.AsSpan(at,owner.Length))==owner.Sha,"native allocator/free/vector/RNG/renderer owner changed");}
 }
 internal static UniversalParticleDependency Inspect(UniversalTwilightTrooperParticleCertificate c,int type)
 {
  Require(type is 1 or 13 or 70 or 79,"unissued particle type");Require(type!=13||c.CopiesImpact,"impact13 needs its copied dispatcher");
  return new("twilightharbor",type,type==13?"source-certified-copied":"source-certified-native-equivalent",type==13?
   "Original impact13 constructor/update and first texture tile; destination13 is no-op so it updates once.":
   "Exact native effect packet equivalence; native destination updater runs once. Line70/79 use explicit colors without a texture.");
 }
 internal static void RequireTextures(UniversalTwilightTrooperParticleCertificate c,UniversalLevelData source,UniversalLevelData destination)
 {
  var a=UniversalParticleDependencies.Records(source);var b=UniversalParticleDependencies.Records(destination);
  foreach(int type in c.CopiesImpact?new[]{1,13}:new[]{1})
   Require(a.TryGetValue(type,out var s)&&s.Length==8&&b.TryGetValue(type,out var d)&&d.Length==8&&UniversalParticleDependencies.EquivalentTexture(s,source.Vram(),d,destination.Vram()),"first texture tile/pixels/palette changed");
 }
 internal static void PatchImpact(UniversalAsmPart part,UniversalOverlay source,string label)
 {
  RequireSource(source);
  // The pipeline first requires the complete copied actor input schema. This
  // packet also owns all impact arguments and the vector subtraction delay.
  UniversalNativeLinkedActorOwnership.RequirePacket(part,source,0x800805F8,[0x02202021,0x02202821,0x0C005DD6,0x26A6000C,0x24040001,0x2405000D,0x02203021,0x3C028007,0x8C4258E4,0,0x0040F809,0x02603821]);
  var sites=part.Items.Select((i,n)=>(i,n)).Where(x=>(x.i.Orig??x.i.SourcePc)==0x80080620).Select(x=>x.n).ToArray();Require(sites.Length>0,"copied B0 lost impact request");
  foreach(int at in sites)part.Items[at]=part.Items[at] with{Word=UniversalMips.EncodeJ(3,0),Fix=new(UniversalFixKind.JalLabel,Label:label)};
 }
 private static void RequireEquivalentLeaves(UniversalOverlay source,UniversalOverlay destination)
 {
  // Complete functions bind compiler setup/ABI: Twilight s5=type/s6=80,
  // Dark s6=type/s5=80. Count60, vectors s3/s4, scratch40/44/48 match.
  // Map only those two registers in leaf opcodes. Native JAL callees stay exact;
  // local control targets are explicitly paired, never accepted by a range.
  var targets=new Dictionary<uint,uint>{{0x80083BA0,0x800832C8},{0x80085740,0x80084CD0},{0x80085E74,0x80085404},{0x80085F70,0x80085500},{0x80085FAC,0x8008553C},{0x80084E3C,0x80084430},{0x80084E54,0x80084448},{0x80084E5C,0x80084450}};
  Equal(0x80084F94,0x8008458C,104,true); //1 constructor
  Equal(0x80085D1C,0x800852AC,356,true); //79 line constructor
  Equal(0x800858C4,0x80084E54,240,true); //70 line constructor
  Equal(0x80085F70,0x80085500,60,true); //70 shared RGB/two endpoints tail
  Equal(0x80083C58,0x80083380,136,false); //1 updater
  Equal(0x80084C48,0x8008423C,296,false); //79 updater
  Equal(0x80084518,0x80083B0C,492,false); //70 updater
  Equal(0x80084E3C,0x80084430,60,false); //native alive/free/iteration packet
  // Type1 RGB tail differs by instruction scheduling only. Both write60 RGB,
  // kind4, primitive2E and zero spin, then enter their original iteration tail.
  uint[] a=[0x2408002E,0xA248000F,0x24080004,0xA242000C,0xA242000D,0xA242000E,0xA2480011,0x080217EB,0xA2400010];
  uint[] b=[0x2408002E,0xA242000C,0xA242000D,0xA242000E,0x24020004,0xA248000F,0xA2420011,0x0802154F,0xA2400010];
  for(int n=0;n<a.Length;n++)Require(source.Word(0x80085740+(uint)n*4)==a[n]&&destination.Word(0x80084CD0+(uint)n*4)==b[n],"native1 scheduled RGB/kind/loop tail changed");
  void Equal(uint s,uint d,int bytes,bool swap)
  {
   for(int off=0;off<bytes;off+=4)
   {
    uint w=source.Word(s+(uint)off),got=destination.Word(d+(uint)off);int op=UniversalMips.Op(w);
    if(op==2){uint target=((s+(uint)off+4)&0xF0000000u)|((w&0x03FFFFFFu)<<2);uint paired=target>=s&&target<s+(uint)bytes?d+(target-s):targets.TryGetValue(target,out uint v)?v:throw new InvalidDataException($"Unpaired native particle jump{target:X8} at{s+(uint)off:X8}.");w=UniversalMips.EncodeJ(2,paired);}
    else if(UniversalMips.IsPcRelative(w))
    {
     uint target=UniversalMips.BranchTarget(w,s+(uint)off);uint paired=target>=s&&target<s+(uint)bytes?d+(target-s):targets.TryGetValue(target,out uint v)?v:throw new InvalidDataException("Unpaired native particle branch.");
     w=UniversalMips.WithBranchTarget(w,d+(uint)off,paired);
    }
    if(swap&&op is not(2 or 3))
    {
     int Swap(int r)=>r==21?22:r==22?21:r;
     int rs=Swap(UniversalMips.Rs(w)),rt=Swap(UniversalMips.Rt(w));w=(w&~0x03FF0000u)|((uint)rs<<21)|((uint)rt<<16);
     if(op==0)w=(w&~0x0000F800u)|((uint)Swap((int)(w>>11&31))<<11);
    }
    Require(w==got,$"native effect leaf/argument/lifetime/control semantics differ at{s+(uint)off:X8} expected{w:X8} got{got:X8}");
   }
  }
 }
 private static void Require(bool ok,string why){if(!ok)throw new InvalidDataException("Twilight particles: "+why);}
 private static void RequireOwner(UniversalOverlay o,uint pc,int length,string sha)=>Require(o.Contains(pc)&&(ulong)pc+(uint)length<=o.End&&Hash(o.Bytes.Slice((int)(pc-UniversalOverlay.Base),length))==sha,"complete particle function/table owner changed");
 private static string Hash(ReadOnlySpan<byte> b)=>Convert.ToHexStringLower(SHA256.HashData(b));
 private static readonly (uint Pc,int Length,string Sha)[] HostOwners=[
  (0x800844A0,4340,"3c8bb2b250a0f63124d46b7b4e5238ed325172efb9fcc50a9015968d93d1b908"),(0x80083274,4652,"42568934fe492f56cb58c8d22e803ccfb605b994789d917c86ae8a245b074af8"),
  (0x8007ACB8,324,"3d7229fa46f8f21d4d3dd07f28fb18cbfa0872c445aa24ae571eb1975243c4c6"),(0x8007AB74,324,"138aafd4977dd3327e9283992d2082469c5d064b08b373848f2101d53cce3997")];
 private static readonly (uint Pc,int Length,string Sha)[] Native=[
  (0x800169AC,5288,"cd7816e798e34cc9b4a015bca681d845f526823c50ca29e2db94cf8b1ced0426"),(0x80053570,308,"29bfbf3ce7c1b33a88e8e75a7b23d6c47ff63fa8b70fa405dc04c456c2365d69"),
  (0x800573C8,6160,"87c64709fc57f9f1153f925a758d65464f12fee3a777288af1b06bbe41042d7a"),(0x8006272C,48,"8bcf455ef340cfd7846fd7937a88671fbf165aa56526545a4912547c8d6bcfd4"),(0x8006CBF8,640,"795a93bee7b4d49bef89d4df8c8628a4b4b3a91d694053dbc2b26b4293d4fa55")];
}
