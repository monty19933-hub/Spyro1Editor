using System.Security.Cryptography;
namespace Spyro.Editor.Core.Universal;

/// <summary>Original Terrace77 is an egg completion line effect, already owned by original Dark Hollow.
/// This certificate adds no constructor, updater, texture, actor link or reward behavior.</summary>
public sealed record UniversalTerraceLineParticleProof(string SourceLevelKey,string DestinationLevelKey,int TypeId,
 uint SourceSpawn,uint DestinationSpawn,uint SourceUpdate,uint DestinationUpdate,uint RequestingClass,uint RequestPc,
 int RequestCount,int RequestState,string ResourceOwnership,string Status);

public sealed class UniversalTerraceLineParticleCertificate
{
 internal UniversalTerraceLineParticleCertificate() { }
 public UniversalTerraceLineParticleProof Proof {get;}=new("terracevillage","darkhollow",77,0x800866D8,0x800844A0,
  0x800853AC,0x80083274,0x20,0x8007C8B8,16,5,"Native kind4 LINE_G2; two explicit RGB endpoints. No texture/palette/actor link.","source-certified-native-line");
 internal void RequireDestination(UniversalOverlay destination,byte[] exe)=>UniversalTerraceLineParticle77.RequireDestination(destination,exe);
}

public static class UniversalTerraceLineParticle77
{
 public static UniversalTerraceLineParticleCertificate Capture(string sourceKey,string destinationKey,UniversalOverlay source,UniversalOverlay destination,byte[] exe)
 {
  if(sourceKey!="terracevillage" || destinationKey!="darkhollow")throw new InvalidDataException("Terrace line particle certificate has an unrelated source or destination.");
  RequireSource(source);RequireDestination(destination,exe);RequireEquivalentLeaves(source,destination);
  return new();
 }
 internal static UniversalParticleDependency Inspect(UniversalTerraceLineParticleCertificate certificate)=>new("terracevillage",77,certificate.Proof.Status,
  "Uses the destination's equivalent native pink line effect during egg collection completion; no added particle dispatcher.");
 internal static void RequireSource(UniversalOverlay source)
 {
  if(source.Bindings.Count!=SourceBindings.Length || SourceBindings.Any(b=>!source.Bindings.TryGetValue(b.Global,out var found) || found!=b))throw new InvalidDataException("Terrace callback binding identity changed.");
  if(Hash(source.Bytes)!="285fc63da6b536af2b41c19e41d1db03a980007226dc55e5d930a1998f39c50e" ||
   source.Update!=0x8007B5DC || source.Init!=0x8008465C || source.CopyBuffer!=0x80087944 ||
   source.Bindings[UniversalExeBindings.ParticleSpawnGlobal].Value!=0x800866D8 || source.Bindings[UniversalExeBindings.ParticleUpdateGlobal].Value!=0x800853AC)
   throw new InvalidDataException("Terrace line particle lost its exact source callbacks or overlay.");
  RequireOwner(source,0x800866D8,4716,"be2f70563137c96de55fc12bf81b50436c9c2a8940ccf2cebf4837fb40cce0e0");
  RequireOwner(source,0x800853AC,4908,"1889589d267d45d58a451753630598f734e21d7c62c1b545283cef128591eef8");
  RequireOwner(source,0x8007B454,324,"a7a61c2f15cc8ac22acf4bc467cf347a53c925bd2c6a8abc7533dd118242c903");
  RequireOwner(source,0x8007B310,324,"b2b40f117207b98eec550797892d9e9b29f54ba74270af63c4fad8593bdbd287");
 }
 internal static void RequireDestination(UniversalOverlay destination,byte[] exe)
 {
  if(exe.Length<(int)(0x8005A890-UniversalExeBindings.ExeBase))throw new InvalidDataException("Native line particle executable owner is missing or truncated.");
  foreach(var b in DestinationParticleBindings)
   if(!destination.Bindings.TryGetValue(b.Global,out var found) || found!=b || UniversalExeBindings.Read(exe,12)[b.Global]!=b)throw new InvalidDataException("Dark Hollow native particle initializer binding identity changed.");
  // Only particle callbacks are fixed here: the ordinary Moby exporter may append
  // its own update/init/copy wrappers. Both complete native particle functions,
  // primary tables and shared executable callees must remain original.
  if(destination.Bindings[UniversalExeBindings.ParticleSpawnGlobal].Value!=0x800844A0 || destination.Bindings[UniversalExeBindings.ParticleUpdateGlobal].Value!=0x80083274)
   throw new InvalidDataException("Dark Hollow's native line particle callbacks changed.");
  RequireOwner(destination,0x800844A0,4340,"3c8bb2b250a0f63124d46b7b4e5238ed325172efb9fcc50a9015968d93d1b908");
  RequireOwner(destination,0x80083274,4652,"42568934fe492f56cb58c8d22e803ccfb605b994789d917c86ae8a245b074af8");
  RequireOwner(destination,0x8007ACB8,324,"3d7229fa46f8f21d4d3dd07f28fb18cbfa0872c445aa24ae571eb1975243c4c6");
  RequireOwner(destination,0x8007AB74,324,"138aafd4977dd3327e9283992d2082469c5d064b08b373848f2101d53cce3997");
  foreach(var n in Native)
   if(n.Pc<UniversalExeBindings.ExeBase || (ulong)n.Pc-UniversalExeBindings.ExeBase+(uint)n.Length>(uint)exe.Length || Hash(exe.AsSpan((int)(n.Pc-UniversalExeBindings.ExeBase),n.Length))!=n.Sha)
    throw new InvalidDataException("Native line particle allocator, vectors, RNG, trig or LINE_G2 renderer changed.");
 }
 private static void RequireEquivalentLeaves(UniversalOverlay source,UniversalOverlay destination)
 {
  // Complete original callback hashes own compiler-layout differences in stack
  // saves and setup. The77 leaf uses no stack operands or unrelated setup values.
  // Registers s6=class/s3=spawnParam are the same, as are shared native callees.
  // Only the terminal jump's destination-local loop tail differs.
  EqualLeaf(0x80087494,0x800850E4,0xC4,0x800878EC,0x8008553C);
  EqualLeaf(0x80086290,0x80084058,0xE4,0x80086680,0x80084448);
  void EqualLeaf(uint s,uint d,int bytes,uint sTail,uint dTail)
  {
   for(int off=0;off<bytes;off+=4)
   {
    uint a=source.Word(s+(uint)off),b=destination.Word(d+(uint)off);
    if(off==bytes-8)
    {if(a!=UniversalMips.EncodeJ(2,sTail) || b!=UniversalMips.EncodeJ(2,dTail))throw new InvalidDataException("Native line particle loop-tail ownership changed.");}
    else if(a!=b)throw new InvalidDataException("Native line particle bodies are not equivalent.");
   }
  }
 }
 internal static void RequireCopiedCall(UniversalAsmPart part,UniversalOverlay source)
 {
  RequireSource(source);
  var expected=UniversalBehaviorBundleBuilder.BuildPrunedUpdate(source,[0x20],"line77proof").Part;
  // State5's complete vector/camera, arguments, indirect callback and following
  // egg-count/retirement packet must retain exact order, branches and delay slots.
  RequirePacket(0x8007C3CC,0x8007C3F8);
  RequirePacket(0x8007C87C,0x8007C928);
  void RequirePacket(uint first,uint last)
  {
   var wanted=expected.Items.Where(i=>i.Orig>=first && i.Orig<=last).ToArray();
   var starts=part.Items.Select((i,n)=>(i,n)).Where(p=>p.i.Orig==first).Select(p=>p.n).ToArray();
   if(starts.Length==0)throw new InvalidDataException("Copied egg completion lost its state dispatch or line request.");
   foreach(int start in starts)
   {
    var actual=part.Items.Skip(start).Where(i=>i.Word!=null).Take(wanted.Length).ToArray();
    if(actual.Length!=wanted.Length)throw new InvalidDataException("Copied line packet is truncated.");
    for(int n=0;n<wanted.Length;n++)
    {
     var a=actual[n];var e=wanted[n];bool fix=a.Fix==e.Fix;
     if(e.Fix.Kind is UniversalFixKind.TableHi or UniversalFixKind.TableLo)
      fix=a.Fix.Kind==e.Fix.Kind && a.Fix.Label!=null && part.Tables.TryGetValue(a.Fix.Label,out var t) && t.SequenceEqual(expected.Tables[e.Fix.Label!]);
     if(a.Orig!=e.Orig || a.Word!=e.Word || !fix)throw new InvalidDataException("Copied line particle caller changed arguments, state, order or targets.");
    }
   }
   foreach(var w in wanted)if(part.Items.Count(i=>i.Orig==w.Orig)!=starts.Length)throw new InvalidDataException("Copied line particle packet has a partial duplicate.");
   foreach(var i in part.Items.Where(i=>i.Fix.Kind is UniversalFixKind.Branch or UniversalFixKind.Jump && i.Fix.Orig>=first && i.Fix.Orig<=last))
    if(!expected.Items.Any(e=>e.Orig==i.Orig && e.Word==i.Word && e.Fix==i.Fix))throw new InvalidDataException("Copied line particle packet gained an interior entry.");
   foreach(var table in part.Tables.Values)foreach(uint target in table.Where(t=>t>=first && t<=last))
    if(!expected.Tables.Values.Any(t=>t.Contains(target)))throw new InvalidDataException("Copied line particle packet gained a table entry.");
   // Source-PC labels are entry points too: inserted label branches may not
   // enter halfway through an authenticated operand/indirect-call packet.
   var interiorLabels=new HashSet<string>();uint? nextPc=null;
   for(int n=part.Items.Count-1;n>=0;n--)
   {var i=part.Items[n];if(i.Word!=null)nextPc=i.Orig;if(i.Label!=null && nextPc>=first && nextPc<=last)interiorLabels.Add(i.Label);}
   if(part.Items.Any(i=>i.Fix.Kind is UniversalFixKind.BranchLabel or UniversalFixKind.JumpLabel or UniversalFixKind.JalLabel && i.Fix.Label!=null && interiorLabels.Contains(i.Fix.Label)))
    throw new InvalidDataException("Copied line particle packet gained an interior label entry.");
  }
 }
 private static void RequireOwner(UniversalOverlay o,uint pc,int length,string sha)
 {if(!o.Contains(pc) || (ulong)pc+(uint)length>o.End || Hash(o.Bytes.Slice((int)(pc-UniversalOverlay.Base),length))!=sha)throw new InvalidDataException("Line particle function or table owner changed.");}
 private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
 private static readonly UniversalExeBinding[] SourceBindings=[
  new(0x800785D8,0x8005B050,0x8005B054,0x80087944),new(0x800758CC,0x8005B060,0x8005B064,0x8008465C),
  new(0x80075734,0x8005B070,0x8005B074,0x8007B5DC),new(0x800758E4,0x8005B080,0x8005B084,0x800866D8),new(0x800756BC,0x8005B090,0x8005B094,0x800853AC)];
 private static readonly UniversalExeBinding[] DestinationParticleBindings=[new(0x800758E4,0x8005A878,0x8005A87C,0x800844A0),new(0x800756BC,0x8005A888,0x8005A88C,0x80083274)];
 private static readonly (uint Pc,int Length,string Sha)[] Native=[
  (0x800169AC,5288,"cd7816e798e34cc9b4a015bca681d845f526823c50ca29e2db94cf8b1ced0426"),
  (0x80053570,308,"29bfbf3ce7c1b33a88e8e75a7b23d6c47ff63fa8b70fa405dc04c456c2365d69"),
  (0x800573C8,6160,"87c64709fc57f9f1153f925a758d65464f12fee3a777288af1b06bbe41042d7a"),
  (0x8006272C,48,"8bcf455ef340cfd7846fd7937a88671fbf165aa56526545a4912547c8d6bcfd4"),
  (0x8006CBF8,512,"877872b39150488ce0baad5491d04ec492f0a7d4ba45248244c46a0ea548a82b")];
}
