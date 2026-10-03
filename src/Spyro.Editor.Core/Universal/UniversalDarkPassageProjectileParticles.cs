using System.Security.Cryptography;
namespace Spyro.Editor.Core.Universal;

/// <summary>Issued ownership of original Dark9B flight trail1 and impact13. No gameplay acceptance is implied.</summary>
public sealed class UniversalDarkPassageProjectileParticleCertificate
{
 internal UniversalDarkPassageProjectileParticleCertificate(string destinationKey,UniversalOverlay source,UniversalOverlay destination)
 {DestinationLevelKey=destinationKey;Source=new(source.Bytes.ToArray(),source.Bindings.ToDictionary());Destination=new(destination.Bytes.ToArray(),destination.Bindings.ToDictionary());}
 public string SourceLevelKey=>"darkpassage";
 public string DestinationLevelKey {get;}
 public bool CopiesImpact=>Destination.Bindings[UniversalExeBindings.ParticleUpdateGlobal].Value!=0x8008AE28;
 public string DestinationParticleProfile=>Destination.Bindings[UniversalExeBindings.ParticleUpdateGlobal].Value==0x80087E20?"native-town-square-cleared":DestinationLevelKey;
 internal UniversalOverlay Source {get;}
 internal UniversalOverlay Destination {get;}
 internal void RequireDestination(UniversalOverlay destination,byte[] exe)=>UniversalDarkPassageProjectileParticles.RequireDestination(DestinationLevelKey,destination,exe);
 internal UniversalOwnedParticleCertificate? OwnedDispatcher=>CopiesImpact?new(new("darkpassage",Source,
  UniversalDarkPassageProjectileParticles.Spawn13,UniversalDarkPassageProjectileParticles.Update13),DestinationLevelKey,
  Destination.Bindings[UniversalExeBindings.ParticleUpdateGlobal].Value,
  (part,source,label)=>UniversalDarkPassageProjectileParticles.PatchImpact(part,source,label),
  destination=>UniversalDarkPassageProjectileParticles.RequireDestination(DestinationLevelKey,destination,null)):null;
}

public static class UniversalDarkPassageProjectileParticles
{
 internal static readonly UniversalOwnedParticleFunction Spawn13=new(0x80086D38,5020,"37358ca0fb4dba01e829c46d5e94aae7146fb550b20680c95327f16361eaa96e",0x8007B370,0x8008807C,[13]);
 internal static readonly UniversalOwnedParticleFunction Update13=new(0x800857FC,5436,"0de2b6053564c8aa12aaa5c447964561dbe2088f63719d595f936d74ce1eb9cf",0x8007B22C,0x80086CE8,[13]);
 public static UniversalDarkPassageProjectileParticleCertificate Capture(string sourceKey,string destinationKey,UniversalLevelData data,UniversalOverlay source,UniversalOverlay destination,byte[] exe)
 {
  if(!string.Equals(sourceKey,"darkpassage",StringComparison.OrdinalIgnoreCase) || destinationKey is not ("darkhollow" or "peacekeepers"))throw new InvalidDataException("The projectile particle certificate has an unrelated source or destination.");
  if(Hash(data.Bytes)!="217d4e525f10b63f4ae4a413d807989241b7f2f91963b543b421cbd3954c364a")throw new InvalidDataException("Dark projectile particle source data changed.");
  RequireSource(source);RequireDestination(destinationKey,destination,exe);return new(destinationKey,source,destination);
 }
 internal static bool IsSupportedDestination(UniversalOverlay o)=>o.Bindings.TryGetValue(UniversalExeBindings.ParticleSpawnGlobal,out var b)&&b.Value is 0x800844A0 or 0x8008BFF0 or 0x80088F68;
 internal static bool HasNativeSourceOwner(UniversalOverlay o)
 {
  try{RequireSourceParticleFunctions(o);return true;}catch(InvalidDataException){return false;}
 }
 internal static void RequireBundle(string key,UniversalOverlay source,IReadOnlyList<int> classes,UniversalOverlay destination,byte[] exe,
  IReadOnlyDictionary<string,UniversalDarkPassageProjectileParticleCertificate>? certificates,IReadOnlyList<UniversalOwnedParticleCertificate>? owned)
 {
  if(!classes.Contains(0x9B))return;
  bool originalSource=Hash(source.Bytes)=="a8b99270c37a37363824cf1fd2081c092968abef62df9e74d70ae0017b984727" ||
   source.Bindings.TryGetValue(UniversalExeBindings.ParticleSpawnGlobal,out var spawnBinding)&&source.Bindings.TryGetValue(UniversalExeBindings.ParticleUpdateGlobal,out var updateBinding)&&spawnBinding.Value==Spawn13.Entry&&updateBinding.Value==Update13.Entry;
  if(!string.Equals(key,"darkpassage",StringComparison.OrdinalIgnoreCase)) {if(originalSource)throw new InvalidDataException("Dark projectile particle source was mislabeled.");return;}
  RequireSource(source);
  if(HasNativeSourceOwner(destination))return;
  if(!IsSupportedDestination(destination))return; // Other destinations remain explicitly dependency-unverified.
  if(certificates==null||!certificates.TryGetValue(key,out var certificate))throw new InvalidDataException("This copied Dark projectile needs its issued particle effect ownership.");
  certificate.RequireDestination(destination,exe);
  if(!source.Bytes.SequenceEqual(certificate.Source.Bytes)||source.Bindings.Count!=certificate.Source.Bindings.Count||source.Bindings.Any(b=>!certificate.Source.Bindings.TryGetValue(b.Key,out var c)||c!=b.Value))throw new InvalidDataException("The projectile effect certificate has a different copied source.");
  if(certificate.CopiesImpact && (owned?.Any(c=>c.SourceLevelKey!="darkpassage"&&c.TypeIds.Contains(13))??false))throw new InvalidDataException("Two copied sources cannot independently update the same particle13 lifetime.");
  if(certificate.CopiesImpact && !(owned?.Any(c=>c.SourceLevelKey=="darkpassage"&&c.TypeIds.SequenceEqual(new[]{13}))??false))throw new InvalidDataException("The projectile impact needs its issued source13 dispatcher.");
 }
 internal static UniversalParticleDependency Inspect(UniversalDarkPassageProjectileParticleCertificate c,int type)=>new("darkpassage",type,
  type==13&&c.CopiesImpact?"source-certified-copied":"source-certified-native-equivalent",
  type==13&&c.CopiesImpact?"Original impact13 constructor/update, imported first texture tile and native renderer; destination manager runs once.":"Exact original particle constructor/update semantics and sampled pixels/palette match the host's native effect.");
 internal static void RequireTextures(UniversalDarkPassageProjectileParticleCertificate c,UniversalLevelData source,UniversalLevelData destination)
 {
  var a=UniversalParticleDependencies.Records(source);var b=UniversalParticleDependencies.Records(destination);
  foreach(int type in new[]{1,13})if(!a.TryGetValue(type,out var s)||s.Length!=8||!b.TryGetValue(type,out var d)||d.Length!=8||!UniversalParticleDependencies.EquivalentTexture(s,source.Vram(),d,destination.Vram()))throw new InvalidDataException("Dark projectile particle lost its first texture tile, sampled pixels or palette.");
 }
 internal static void RequireSource(UniversalOverlay o)
 {
  UniversalDarkPassageShapeshifterOwnership.RequireSource(o);
  if(o.Bindings.Count!=SourceBindings.Length || SourceBindings.Any(b=>!o.Bindings.TryGetValue(b.Global,out var f)||f!=b))throw new InvalidDataException("Dark projectile callback binding identity changed.");
  RequireSourceParticleFunctions(o);
 }
 private static void RequireSourceParticleFunctions(UniversalOverlay o)
 {
  RequireOwner(o,Spawn13.Entry,Spawn13.Length,Spawn13.Sha);RequireOwner(o,Update13.Entry,Update13.Length,Update13.Sha);
  RequireOwner(o,Spawn13.Table,324,"5fb0e2d534be293fc1b6eb0c6c37fbd3073c459954548b4dc3fc7cc762ce7390");RequireOwner(o,Update13.Table,324,"df43b46222d4772bfb8281acb61ed015ccd3a7dc92d753cd6b94dc1d5fc05400");
  if(o.Bindings[UniversalExeBindings.ParticleSpawnGlobal].Value!=Spawn13.Entry||o.Bindings[UniversalExeBindings.ParticleUpdateGlobal].Value!=Update13.Entry)throw new InvalidDataException("Dark projectile particle callbacks changed.");
 }
 internal static void RequireDestination(string key,UniversalOverlay destination,byte[]? exe)
 {
  if(key is not ("darkhollow" or "peacekeepers"))throw new InvalidDataException("Unowned projectile particle destination.");
  bool town=key=="peacekeepers"&&destination.Bindings.TryGetValue(UniversalExeBindings.ParticleUpdateGlobal,out var update)&&update.Value==0x80087E20;
  var bindings=town?TownClearedPeaceBindings:key=="darkhollow"?DarkBindings:PeaceBindings;

  foreach(var b in bindings)if(!destination.Bindings.TryGetValue(b.Global,out var f)||f!=b)throw new InvalidDataException("Projectile destination particle callback identity changed.");
  if(exe!=null){foreach(var b in bindings)if(UniversalExeBindings.Read(exe,key=="darkhollow"?12:20)[b.Global]!=b)throw new InvalidDataException("Projectile destination executable callback identity changed.");RequireNative(exe);}
  foreach(var owner in town?TownOwners:key=="darkhollow"?DarkOwners:PeaceOwners)RequireOwner(destination,owner.Pc,owner.Length,owner.Sha);
  if(town)
  {
   if(destination.Word(0x8007AC80+13*4)!=0x80088F20||destination.Word(0x80088F20)!=0x26100020||destination.Word(0x8007ADC4+13*4)!=0x80089E74)throw new InvalidDataException("Cleared Town particle13 no-op owner changed.");
   // The exact nine-setter Town initializer is the Start Blank owner. Its full
   // bytes, copied into Peace's isolated native initializer, must remain intact.
   if(exe!=null && Hash(exe.AsSpan((int)(0x8005AA28-UniversalExeBindings.ExeBase),152))!="fe4669229aedbbc8ff698d57a237327af36025aaeda0ff5cc2e1c806fd23e50b")throw new InvalidDataException("Cleared Peace lost its exact Town nine-setter initializer owner.");
  }
  // Dark13 is exactly the original no-op tail, so only its issued source updater runs.
  if(key=="darkhollow"&&(destination.Word(0x8007AB74+13*4)!=0x80084450||destination.Word(0x80084450)!=0x26310020||destination.Word(0x8007ACB8+13*4)!=0x8008553C))throw new InvalidDataException("Dark Hollow particle13 no-op owner changed.");
 }
 internal static void RequireNative(byte[] exe)
 {
  foreach(var n in Native)if(n.Pc<UniversalExeBindings.ExeBase||(ulong)n.Pc-UniversalExeBindings.ExeBase+(uint)n.Length>(uint)exe.Length||Hash(exe.AsSpan((int)(n.Pc-UniversalExeBindings.ExeBase),n.Length))!=n.Sha)throw new InvalidDataException("Projectile particles lost native allocator/free, vector, RNG, trig or rotated-quad renderer ownership.");
 }
 internal static void RequireCopiedCalls(UniversalAsmPart part,UniversalOverlay source)
 {
  RequireSource(source);var expected=UniversalBehaviorBundleBuilder.BuildPrunedUpdate(source,[0x9B],"projectileproof").Part;
  RequirePacket(0x80080F34,0x80081150);
  void RequirePacket(uint first,uint last)
  {
   var wanted=expected.Items.Where(i=>i.Orig>=first&&i.Orig<=last).ToArray();
   var starts=part.Items.Select((i,n)=>(i,n)).Where(x=>x.i.Orig==first).Select(x=>x.n).ToArray();
   if(starts.Length==0)throw new InvalidDataException("Copied projectile lost its state/update effect owner.");
   foreach(int start in starts){var actual=part.Items.Skip(start).Where(i=>i.Word!=null).Take(wanted.Length).ToArray();if(!actual.SequenceEqual(wanted))throw new InvalidDataException("Copied projectile effect packet changed arguments, flow, order or targets.");}
   foreach(var w in wanted)if(part.Items.Count(i=>i.Orig==w.Orig)!=starts.Length)throw new InvalidDataException("Copied projectile effect has a partial duplicate.");
   foreach(var i in part.Items.Where(i=>(i.Fix.Kind is UniversalFixKind.Branch or UniversalFixKind.Jump)&&i.Fix.Orig>=first&&i.Fix.Orig<=last))if(!expected.Items.Any(e=>e.Orig==i.Orig&&e.Word==i.Word&&e.Fix==i.Fix))throw new InvalidDataException("Copied projectile effect gained an interior entry.");
   foreach(var t in part.Tables.Values)foreach(uint target in t.Where(t=>t>=first&&t<=last))if(!expected.Tables.Values.Any(t=>t.Contains(target)))throw new InvalidDataException("Copied projectile effect gained a table entry.");
   HashSet<string> labels=[];uint? next=null;for(int n=part.Items.Count-1;n>=0;n--){var i=part.Items[n];if(i.Word!=null)next=i.Orig;if(i.Label!=null&&next>=first&&next<=last)labels.Add(i.Label);}
   if(part.Items.Any(i=>(i.Fix.Kind is UniversalFixKind.BranchLabel or UniversalFixKind.JumpLabel or UniversalFixKind.JalLabel)&&i.Fix.Label!=null&&labels.Contains(i.Fix.Label)))throw new InvalidDataException("Copied projectile effect gained an interior label entry.");
  }
 }
 internal static void PatchImpact(UniversalAsmPart part,UniversalOverlay source,string label)
 {
  RequireCopiedCalls(part,source);int[] sites=part.Items.Select((i,n)=>(i,n)).Where(x=>x.i.Orig==0x80081138).Select(x=>x.n).ToArray();
  foreach(int at in sites)part.Items[at]=part.Items[at] with{Word=UniversalMips.EncodeJ(3,0),Fix=new(UniversalFixKind.JalLabel,Label:label)};
 }
 private static void RequireOwner(UniversalOverlay o,uint pc,int length,string sha){if(!o.Contains(pc)||(ulong)pc+(uint)length>o.End||Hash(o.Bytes.Slice((int)(pc-UniversalOverlay.Base),length))!=sha)throw new InvalidDataException("Projectile particle function/table owner changed.");}
 private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
 private static readonly UniversalExeBinding[] TownClearedPeaceBindings=[new(0x800758E4,0x8005AA58,0x8005AA5C,0x80088F68),new(0x800756BC,0x8005AA68,0x8005AA6C,0x80087E20)];
 private static readonly (uint Pc,int Length,string Sha)[] TownOwners=[
  (0x80088F68,3940,"0f41d76490950c9ee0b2c3be847ff9b3f360b4852ce674e7acc04120c975c931"),
  (0x80087E20,4424,"1279e327bdbf92e03c991d619cf3190f3015bb63493f13855355588c0ba78cdc"),
  (0x8007ADC4,324,"cfbfca35aad07e26113533f2444ec9d148510523e93e37ddf506aa5203132532"),
  (0x8007AC80,324,"2e8c2cfb1539eab094c0c0d8cb771df1aaac08c092947b20869c715e4fc93bc8")];
 private static readonly UniversalExeBinding[] SourceBindings=[
  new(0x800785D8,0x8005B2D0,0x8005B2D4,0x800880D4),
  new(0x800758CC,0x8005B2E0,0x8005B2E4,0x80084B94),
  new(0x80075734,0x8005B2F0,0x8005B2F4,0x8007B4F8),
  new(0x800758E4,0x8005B300,0x8005B304,0x80086D38),
  new(0x800756BC,0x8005B310,0x8005B314,0x800857FC)];
 private static readonly UniversalExeBinding[] DarkBindings=[
  new(0x800758E4,0x8005A878,0x8005A87C,0x800844A0),
  new(0x800756BC,0x8005A888,0x8005A88C,0x80083274)];
 private static readonly (uint Pc,int Length,string Sha)[] DarkOwners=[
  (0x800844A0,4340,"3c8bb2b250a0f63124d46b7b4e5238ed325172efb9fcc50a9015968d93d1b908"),
  (0x8007ACB8,324,"3d7229fa46f8f21d4d3dd07f28fb18cbfa0872c445aa24ae571eb1975243c4c6"),
  (0x80083274,4652,"42568934fe492f56cb58c8d22e803ccfb605b994789d917c86ae8a245b074af8"),
  (0x8007AB74,324,"138aafd4977dd3327e9283992d2082469c5d064b08b373848f2101d53cce3997")];
 private static readonly UniversalExeBinding[] PeaceBindings=[
  new(0x800758E4,0x8005AA58,0x8005AA5C,0x8008BFF0),
  new(0x800756BC,0x8005AA68,0x8005AA6C,0x8008AE28)];
 private static readonly (uint Pc,int Length,string Sha)[] PeaceOwners=[
  (0x8008BFF0,4020,"73c3300f23403ea80dd10c4e2d8fb2134b84a726af9078e0e4b38b43e110a022"),
  (0x8007B710,324,"84c0178e89a2789a34e7e04487eaa2483d130caaafb20ceebbdd0a11af07579f"),
  (0x8008AE28,4552,"4222aa7ae72bafa344b89844ba31b817b9e1917ed1636d957ef17fd76b015b9b"),
  (0x8007B5CC,324,"8d43637b3dcf652cb8ce111ee7a360792f803122ddca530618a033e076a6320f")];
 private static readonly (uint Pc,int Length,string Sha)[] Native=[
  (0x800131BC,48,"26d3b05d50cd1f302f5a3690c1d1e38719e35c8a46e04b4f1327e78953f077b0"),
  (0x800169AC,5288,"cd7816e798e34cc9b4a015bca681d845f526823c50ca29e2db94cf8b1ced0426"),
  (0x80053570,308,"29bfbf3ce7c1b33a88e8e75a7b23d6c47ff63fa8b70fa405dc04c456c2365d69"),
  (0x800573C8,6160,"87c64709fc57f9f1153f925a758d65464f12fee3a777288af1b06bbe41042d7a"),
  (0x8006272C,48,"8bcf455ef340cfd7846fd7937a88671fbf165aa56526545a4912547c8d6bcfd4"),
  (0x8006CBF8,512,"877872b39150488ce0baad5491d04ec492f0a7d4ba45248244c46a0ea548a82b")];
}
