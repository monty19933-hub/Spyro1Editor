using System.Buffers.Binary;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
namespace Spyro.Editor.Core.Universal;

/// <summary>Inert, exact normal-frame transaction replacement for the original
/// Alpine6/52 world. The resident installer is independently replayable, but
/// the complete original ordinary loader reaches it through native Sparx120
/// after scene initialization. Mode12/modal lifetime ownership remains pending.
/// No permission is issued.
/// Aborted frames and unsupported modal rendering are release defects.</summary>
internal sealed class NativeGreenDruidGroup1CompleteFrameGuard
{
 internal const uint ResidentEntry=0x8005B8E0,OriginalFrame=0x8001ED5C;
 internal const int OriginalOverlayBytes=63488,MaximumFinalOverlayBytes=194560;
 private readonly byte[] _exe,_overlay,_data;
 private readonly NativeLevelRuntimeMemoryReport _owner;
 private readonly NativeGreenDruidGroup1ResidentEntrySource _source;
 internal GreenDruidGroup1TerrainQueueSceneStorage.Loaded Queues{get;}
 internal uint CodeAddress{get;} internal uint StateAddress{get;} internal uint InstallerAddress{get;}
 internal uint GuardedFrame{get;} internal int CodeByteLength{get;}
 internal int ReservationCount=>677; internal int GuardSpanCount=>199;
 internal bool ForeignPlacementAdmitted=>false; internal bool GpuCapacityCertified=>false;
 internal bool HardwareValidated=>false; internal bool LifecycleCertified=>false; internal bool ModalRenderingCertified=>false;
 internal IReadOnlyDictionary<uint,uint> PcMap=>new Dictionary<uint,uint>(_map);
 private readonly Dictionary<uint,uint> _map;
 internal byte[] CopyExecutable()=>_exe.ToArray();internal byte[] CopyOverlay()=>_overlay.ToArray();internal byte[] CopyLevelData()=>_data.ToArray();
 private sealed record Recipe(string Name,uint First,uint End);
 private static readonly Recipe[] Recipes=[
  new("current-terrain",0x8008C238u,0x80091048u),
  new("player-models",0x8001F798u,0x800258F0u),
  new("actors-models",0x8004EBA8u,0x80050BD0u),
  new("particles",0x800573C8u,0x800580F4u),
  new("sparx-shared",0x800580F4u,0x8005882Cu),
  new("model-effects",0x80058D64u,0x80059474u),
  new("projected-shadow",0x80059A48u,0x8005A474u),
  new("hud",0x8001844Cu,0x80019300u),
  new("normal-main",0x8001ED5Cu,0x8001F158u),
  new("preterrain-dispatch",0x80019698u,0x80019738u),
  new("actor-dispatch",0x80050BD0u,0x800521C0u),
  new("sparx-dispatch",0x80058BA8u,0x80058BD8u),
  new("hud-queue",0x80019300u,0x80019698u),
  new("terrain-dispatch",0x8002B9CCu,0x8002BA74u),
 ];
 private sealed record Fence(int Recipe,int PcOffset,int PointOffset,int Base,int Offset,int Width,int A,int B,int Words);
 private static Fence[] ReadFences()
 {
  byte[] b=Convert.FromBase64String("AEgMSAweAAAMBwASAEwMSAweAAAMBwASAFwMSAweAAAMBwASAGQMSAweAAAMBwASAJQMlAweAAAkBQYNAJgMlAweAAAkBQYNAJwMlAweAAAkBQYNAKAMlAweAAAkBQYNAKQMlAweAAAkBQYNAKgMlAweAAAkBQYNAKwMlAweAAAkBQYNALAMlAweAAAkBQYNALQMlAweAAAkBQYNAPwN/A0eAAAMBAcNAAAO/A0eAAAMBAcNABAO/A0eAAAMBAcNABgO/A0eAAAMBAcNAEQORA4eAAAcBAUNAEwORA4eAAAcBAUNAFAORA4eAAAcBAUNAFQORA4eAAAcBAUNAFgORA4eAAAcBAUNAFwORA4eAAAcBAUNAGAORA4eAAAcBAUNAPAU8BQeCAAoCQASAPQU8BQeCAAoCQASAPgU8BQeCAAoCQASAPwU8BQeCAAoCQASAIwVjBUeBAAEAQkNAPAV8BUeEAAEAQINAFQWVBYeKAAEAQINAMgWxBYeHAAEAgMNANwW3BYeBAAEAQYNAAgXCBceEAAEAQINADQXNBceKAAEAQINAGAXYBceHAAEAQINAKQXpBceBwABAwQNANQX1BceAAA0BgcNANgX1BceAAA0BgcNANwX1BceAAA0BgcNAOAX1BceAAA0BgcNAOQX1BceAAA0BgcNADAYMBgeAAAkBwgNADQYMBgeAAAkBwgNADgYMBgeAAAkBwgNADwYMBgeAAAkBwgNAEAYMBgeAAAkBwgNAEQYMBgeAAAkBwgNAEwYMBgeAAAkBwgNAFQYMBgeAAAkBwgNAEgaSBoeCAAcBAgNAEwaSBoeCAAcBAgNAFAaSBoeCAAcBAgNAOAa4BoeBAAEAQQNAEQbRBseEAAEAQINALgbtBseHAAEAQINAMwbzBseBAAEAQQNAPgb+BseEAAEAQINACQcJBweHAAEAQINAHAccBweBwABAwQNAKwcrBweDAAQBAYNALAcrBweDAAQBAYNALgctBweJAAEAQINAMgcyBweDAAQBAYNANAcyBweDAAQBAYNANgc1BweJAAEAgMNAOgc6BweDAAQAwQNAPAc6BweDAAQAwQNAPgc9BweJAAEAQMNAAAdAB0eDAAcBAYNAAgdAB0eDAAcBAYNABAdAB0eDAAcBAYNABQdFB0eAAAEAQINAFgdWB0eAAAcBQYNAFwdWB0eAAAcBQYNAGAdWB0eAAAcBQYNAGQdWB0eAAAcBQYNAGwdWB0eAAAcBQYNAHQdWB0eAAAcBQYNAPgj+CMeAAAoDRANAPwj+CMeAAAoDRANAAAk+CMeAAAoDRANABgk+CMeAAAoDRANABwk+CMeAAAoDRANACAk+CMeAAAoDRANAFAk+CMeAAAoDRANAFQk+CMeAAAoDRANAFgk+CMeAAAoDRANAGAk+CMeAAAoDRANAHQlcCUeCAAECw0NAHgleCUeBAAsBAsNAHwleCUeBAAsBAsNAIAleCUeBAAsBAsNAJQleCUeBAAsBAsNAJgleCUeBAAsBAsNAJwleCUeBAAsBAsNAKAleCUeBAAsBAsNAPgl+CUeAAA0AwQNAPwl+CUeAAA0AwQNAAAm+CUeAAA0AwQNAAQm+CUeAAA0AwQNAAwm+CUeAAA0AwQNAGAqYCoeAAAoDRMNAGQqYCoeAAAoDRMNAGgqYCoeAAAoDRMNAHgqYCoeAAAoDRMNAHwqYCoeAAAoDRMNAIAqYCoeAAAoDRMNALAqYCoeAAAoDRMNALQqYCoeAAAoDRMNALgqYCoeAAAoDRMNAMAqYCoeAAAoDRMNAKQrpCseDAAcAQINAKgrpCseDAAcAQINAKwrpCseDAAcAQINACgsJCweCAAEBRMNACwsLCweAAAkAQUNADAsLCweAAAkAQUNAFAsLCweAAAkAQUNAFQsLCweAAAkAQUNAFgsLCweAAAkAQUNAGAsLCweAAAkAQUNAHA2cDYeAAAoEBMNAHQ2cDYeAAAoEBMNAHg2cDYeAAAoEBMNAJA2cDYeAAAoEBMNAJQ2cDYeAAAoEBMNAJg2cDYeAAAoEBMNAMg2cDYeAAAoEBMNAMw2cDYeAAAoEBMNANA2cDYeAAAoEBMNANg2cDYeAAAoEBMNAHQ3dDceAAAoEBMNAHg3dDceAAAoEBMNAHw3dDceAAAoEBMNAJQ3dDceAAAoEBMNAJg3dDceAAAoEBMNAJw3dDceAAAoEBMNAMw3dDceAAAoEBMNANA3dDceAAAoEBMNANQ3dDceAAAoEBMNANw3dDceAAAoEBMNAIA4gDgeAAAoDRANAIQ4gDgeAAAoDRANAIg4gDgeAAAoDRANAKA4gDgeAAAoDRANAKQ4gDgeAAAoDRANAKg4gDgeAAAoDRANANg4gDgeAAAoDRANANw4gDgeAAAoDRAN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");Require(b.Length==677*12,"fixed677 reservation grammar");Fence[] r=new Fence[677];
  for(int n=0;n<r.Length;n++){var p=b.AsSpan(n*12,12);r[n]=new(p[0],BinaryPrimitives.ReadUInt16LittleEndian(p[1..]),BinaryPrimitives.ReadUInt16LittleEndian(p[3..]),p[5],BinaryPrimitives.ReadInt16LittleEndian(p[6..]),p[8],p[9],p[10],p[11]);}
  return r;
 }
 private static void Require(bool v,string why)=>UniversalGreenDruidGroup1TerrainQueues.Require(v,"complete-frame guard: "+why);
 private static uint U(byte[]b,int n)=>BinaryPrimitives.ReadUInt32LittleEndian(b.AsSpan(n));
 private static void Put(byte[]b,int n,uint w)=>BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(n),w);
 private static uint Jump(uint t)=>0x08000000u|((t>>2)&0x3FFFFFF);private static uint Jal(uint t)=>0x0C000000u|((t>>2)&0x3FFFFFF);
 private static uint Lui(int r,uint v)=>0x3C000000u|((uint)r<<16)|(((v+0x8000)>>16)&65535);
 private static uint Add(int d,int s,int n)=>0x24000000u|((uint)s<<21)|((uint)d<<16)|(ushort)n;
 private static uint Lw(int d,int s,int n)=>0x8C000000u|((uint)s<<21)|((uint)d<<16)|(ushort)n;
 private static uint Sw(int d,int s,int n)=>0xAC000000u|((uint)s<<21)|((uint)d<<16)|(ushort)n;
 private static uint Beq(int a,int b,int n)=>0x10000000u|((uint)a<<21)|((uint)b<<16)|(ushort)n;
 private static uint Bne(int a,int b,int n)=>0x14000000u|((uint)a<<21)|((uint)b<<16)|(ushort)n;
 private static byte[] Pack(IEnumerable<uint> w){var a=w.ToArray();byte[]b=new byte[4*a.Length];for(int n=0;n<a.Length;n++)Put(b,4*n,a[n]);return b;}
 private static IEnumerable<uint> Ptr(int r,uint p)=>new[]{Lui(r,p),Add(r,r,(short)p)};
 private static readonly int[] Saved=[16,17,18,19,20,21,22,23,28,29,30,31];
 internal static NativeGreenDruidGroup1CompleteFrameGuard CreateReplacement(NativeGreenDruidGroup1TerrainRenderer previous,
  NativeGreenDruidGroup1ResidentEntrySource source,NativeLevelRuntimeMemoryReport owner)=>new(previous,source,owner);
 private NativeGreenDruidGroup1CompleteFrameGuard(NativeGreenDruidGroup1TerrainRenderer previous,NativeGreenDruidGroup1ResidentEntrySource source,NativeLevelRuntimeMemoryReport owner)
 {
  // Its game code compares the scene address (0x800785E4) with the 2 MB layout's.
  GameRam.RequirePlayStationSceneAddress("The Green Druid frame guard");
  _owner=owner;_source=source;previous.RequireFinal(previous.CopyExecutable(),previous.CopyOverlay(),previous.CopyLevelData());
  byte[] before=previous.CopyOverlay();int prefix=checked((int)(previous.CloneAddress-UniversalOverlay.Base));
  Require(prefix==71680&&before.Length==92160,"exact separate old terrain replacement boundary; all actor/save-map prefix retained");
  _exe=previous.CopyOriginalRendererValidationView(previous.CopyExecutable(),before,previous.CopyLevelData());source.RequireExecutable(_exe);
  _data=previous.CopyLevelData();CodeAddress=previous.CloneAddress;
  // Code length is fixed by the issued677 native store/SDK controls. The old
  // terrain allocation is replaced, never appended again under a second ceiling.
  CodeByteLength=84952;uint cursor=CodeAddress+(uint)CodeByteLength;
  uint pre=cursor,capture=pre+28,abort=capture+156,post=abort+88,gate=post+128;
  InstallerAddress=gate+128;int installerBytes=280;StateAddress=(InstallerAddress+(uint)installerBytes+15)&~15u;
  int used=checked((int)(StateAddress+256-UniversalOverlay.Base));int length=UniversalLevelData.Align(used,2048);
  Require(length<=MaximumFinalOverlayBytes&&length-OriginalOverlayBytes<=NativeOwnedOverlayMaterializer.MaximumAppendByteLength,"cumulative original63488+128KiB code ceiling");
  Require((StateAddress&65535)+160<32768,"native signed field reach in exact compact frame owner");
  _overlay=new byte[length];before.AsSpan(0,prefix).CopyTo(_overlay);
  var bindings=UniversalExeBindings.Read(_exe,20);uint nativeInit=bindings[UniversalExeBindings.InitGlobal].Value;
  UniversalExeBindings.Patch(_exe,bindings[UniversalExeBindings.CopyBufferGlobal],UniversalOverlay.Base+(uint)length);
  UniversalExeBindings.Patch(_exe,UniversalExeBindings.Read(_exe,20)[UniversalExeBindings.InitGlobal],InstallerAddress);
  Queues=previous.Queues.Storage.BindLoaded(_exe,_overlay,owner);
  var built=Compile(previous,source.CopyOriginalExecutable(),StateAddress,abort,Queues.Close,Queues.Final);
  Require(built.Bytes.Length==CodeByteLength,"exact compact complete compiled normal-frame owner length");_map=built.Map;GuardedFrame=_map[OriginalFrame];built.Bytes.CopyTo(_overlay,prefix);
  void Patch(uint at,params uint[] words){for(int n=0;n<words.Length;n++)Put(_overlay,(int)(at-UniversalOverlay.Base)+4*n,words[n]);}
  Patch(_map[0x8001ED80],Jump(pre));Patch(_map[0x8001ED84],0);Patch(_map[0x8001EDD8],Jump(capture),0);
  Patch(_map[0x8001EDF4],Jump(abort),0);Patch(_map[0x8001F11C],Jump(post),0);
  foreach(var part in Transaction(pre,capture,abort,post,StateAddress,_map))part.Bytes.CopyTo(_overlay,(int)(part.Address-UniversalOverlay.Base));
  ResidentGate(GuardedFrame,Queues.CopyBuffer,Queues.Scene).CopyTo(_overlay,(int)(gate-UniversalOverlay.Base));
  var installer=Installer(InstallerAddress,gate,StateAddress,nativeInit);Require(installer.Length==installerBytes,"exact resident installer/flush/cookie owner");installer.CopyTo(_overlay,(int)(InstallerAddress-UniversalOverlay.Base));
  RequireFinal(_exe,_overlay,_data);
 }
 private (byte[] Bytes,Dictionary<uint,uint> Map) Compile(NativeGreenDruidGroup1TerrainRenderer old,byte[] original,uint state,uint abort,uint close,uint final)
 {
  var fences=ReadFences();var originals=new Dictionary<uint,uint>();var guards=new Dictionary<uint,List<uint>>();byte[] oldOverlay=old.CopyOverlay();
  var recipes=Recipes.Select((r,n)=>n==0?new Recipe(r.Name,old.CloneAddress,old.GateAddress):r).ToArray();
  for(int n=0;n<recipes.Length;n++){var r=recipes[n];byte[]b=n==0?oldOverlay:original;uint origin=n==0?UniversalOverlay.Base:UniversalExeBindings.ExeBase;for(uint pc=r.First;pc<r.End;pc+=4)originals.Add(pc,U(b,(int)(pc-origin)));}
  // Rebind only nine explicit queue literal pairs issued by the old complete
  // renderer clone; all original tables/constants/control/source bytes remain.
  foreach(var p in new (uint Pc,int Off,uint Value)[]{(Pc:0x800259B4u,Off:16,Value:final),(0x800261E4u,0,final),(0x80026794u,16,close),(0x80026794u,24,final),(0x80027690u,0,final),(0x8002857Cu,0,close),(0x8002857Cu,8,final),(0x800294F4u,0,close),(0x8002A0A0u,4,final)}){
   uint pc=old.SourceToFinalPc[p.Pc]+(uint)p.Off,w=originals[pc],q=originals[pc+4];int r=(int)(w>>16&31);Require(w>>26==15&&q>>26==13&&(q>>21&31)==r&&(q>>16&31)==r,"owned queue literal form");originals[pc]=(w&0xFFFF0000)|(p.Value>>16);originals[pc+4]=(q&0xFFFF0000)|(p.Value&65535);
  }
  foreach(var f in fences){var r=recipes[f.Recipe];uint pc=r.First+(uint)f.PcOffset,point=r.First+(uint)f.PointOffset,w=originals[pc];
   if(f.Recipe==7&&pc is 0x80018650 or 0x80018D8C or 0x80019110)Require(w==0x0C01819C,"complete native HUD drawmode SDK span");
   else {int width=(w>>26)==40?1:(w>>26)==41?2:4;Require((w>>26)is 40 or 41 or 43 or 58&&(w>>21&31)==f.Base&&(short)w>=f.Offset&&(short)w+width<=f.Offset+f.Width,"exact original member ABI within issued dominating span");}
   Require(f.A>=1&&f.A<=25&&f.B>=0&&f.B<=25&&f.A!=f.Base&&f.B!=f.Base,"issued source dominating span and authenticated scratches");
   var fence=FenceCode(f,state,abort);if(guards.TryGetValue(point,out var prior))Require(prior.SequenceEqual(fence),"identical whole-span leader shared by exact source members");else guards.Add(point,fence);Require(guards[point].Count==f.Words,"issued native fence size");
  }
  Require(guards.Count==GuardSpanCount,"exact199 issued span leaders retainall677 source writer owners");
  Dictionary<uint,uint> map=[];uint at=CodeAddress;foreach(uint pc in originals.Keys){map.Add(pc,at);at+=checked((uint)(4*(guards.TryGetValue(pc,out var g)?g.Count+1:1)));}
  List<uint> words=[];foreach(var p in originals){if(guards.TryGetValue(p.Key,out var g))words.AddRange(g);uint pc=CodeAddress+(uint)words.Count*4,w=p.Value;int op=(int)(w>>26);if(op is 1 or 4 or 5 or 6 or 7){uint t=unchecked(p.Key+4+(uint)(4*(short)w));uint v=map.GetValueOrDefault(t,t);long d=((long)v-pc-4)/4;Require(d>=short.MinValue&&d<=short.MaxValue,"signed native branch reach");w=(w&0xFFFF0000)|(ushort)(short)d;}else if(op is 2 or 3){uint t=((p.Key+4)&0xF0000000)|((w&0x3FFFFFF)<<2);if(t==0x800258F0)t=old.CloneAddress;uint v=map.GetValueOrDefault(t,t);w=(w&0xFC000000)|((v>>2)&0x3FFFFFF);}words.Add(w);}
  byte[] result=Pack(words);
  // Six computed native continuations and three exact8-entry clipping tables.
  foreach(var p in new (uint Hi,uint Lo,uint T)[]{(Hi:0x80022100u,Lo:0x80022104u,T:0x80022110u),(0x80022138u,0x8002213Cu,0x80022148u),(0x80022170u,0x80022174u,0x80022180u),(0x80024044u,0x80024048u,0x80024054u),(0x800240E8u,0x800240ECu,0x800240F8u),(0x80024190u,0x80024194u,0x800241A8u),(0x8004F87Cu,0x8004F880u,0x8004F890u),(0x80050014u,0x80050018u,0x80050028u),(0x800506A0u,0x800506A4u,0x800506B4u)}){
   uint w=originals[p.Hi],q=originals[p.Lo],t=map[p.T];Require(w>>26==15&&q>>26==9&&(((w&65535)<<16)+unchecked((uint)(short)q))==p.T,"exact native computed entry pair");Put(result,(int)(map[p.Hi]-CodeAddress),(w&0xFFFF0000)|(((t+0x8000)>>16)&65535));Put(result,(int)(map[p.Lo]-CodeAddress),(q&0xFFFF0000)|(t&65535));
  }
  return(result,map);
 }
 private static List<uint> FenceCode(Fence f,uint state,uint abort)
 {
  int a=f.A,b=f.B,lo=(short)state;uint hi=Lui(a,state);
  if(b!=0)return[hi,Lw(b,a,lo+156),Lw(a,a,lo+152),0,Add(b,b,-f.Offset-f.Width),((uint)b<<21)|((uint)f.Base<<16)|((uint)b<<11)|0x2Bu,Add(a,a,-f.Offset),((uint)f.Base<<21)|((uint)a<<16)|((uint)a<<11)|0x2Bu,((uint)a<<21)|((uint)b<<16)|((uint)a<<11)|0x25u,Beq(a,0,3),0,Jump(abort),0];
  return[hi,Lw(a,a,lo+156),0,Add(a,a,-f.Offset-f.Width),((uint)a<<21)|((uint)f.Base<<16)|((uint)a<<11)|0x2Bu,Beq(a,0,3),0,Jump(abort),0,hi,Lw(a,a,lo+152),0,Add(a,a,-f.Offset),((uint)f.Base<<21)|((uint)a<<16)|((uint)a<<11)|0x2Bu,Beq(a,0,3),0,Jump(abort),0];
 }
 private static IEnumerable<(uint Address,byte[] Bytes)> Transaction(uint pre,uint capture,uint abort,uint post,uint state,Dictionary<uint,uint> m)
 {
  yield return(pre,Pack(Ptr(5,state).Concat(new[]{Sw(2,5,132),0x3C050001u,0x8CC30074u,Jump(m[0x8001ED88]),0u})));
  List<uint> c=Ptr(2,state).ToList();foreach(int r in Saved)c.Add(Sw(r,2,4*r));c.Add(Sw(0,2,128));foreach(var p in new (int G,int F)[]{(G:0x5888,F:140),(0x581C,144),(0x5820,148),(0x57B0,152),(0x5780,156)})c.AddRange([0x3C038007,Lw(3,3,p.G),0,Sw(3,2,p.F)]);c.AddRange([Jal(0x80033C50),0,Jump(m[0x8001EDE0]),0]);yield return(capture,Pack(c));
  List<uint>a=Ptr(1,state).Concat(new[]{Add(2,0,1),Sw(2,1,128),Lw(2,1,136),0u,Add(2,2,1),Sw(2,1,136)}).ToList();foreach(int r in new[]{16,17,18,19,20,21,22,23,28,30,29,31})a.Add(Lw(r,1,4*r));a.AddRange([Jump(m[0x8001F084]),0]);yield return(abort,Pack(a));
  List<uint>postWords=Ptr(2,state).Concat(new[]{Lw(3,2,128),0u,Bne(3,0,5),0u,Jal(0x80060030),Add(4,4,92),Jump(m[0x8001F124]),0u}).ToList();postWords.AddRange([Lw(4,2,144),0,Add(5,4,0x4008),Sw(0,4,0),Add(4,4,4),Bne(4,5,-3),0,Lw(5,2,132),0,0x3C048007,Sw(5,4,0x5888),Lw(3,2,140),0,Lw(3,3,0x70),0,Sw(3,4,0x57B0),0x3C050001,0x34A5C000,0x00652821,Sw(5,4,0x5780),Jump(m[0x8001F144]),0]);yield return(post,Pack(postWords));
 }
 private static byte[] ResidentGate(uint main,uint copy,uint scene)
 {
  // Resident mode/load checks precede every selected-overlay instruction fetch.
  // All AT/v0/v1 values are defined by the original main before their next use.
  List<uint>w=[0x3C018007,Lw(2,1,0x57D8),Lw(3,1,0x579C),0,0x00431025];List<int>fails=[w.Count];w.AddRange([Bne(2,0,0),0]);w.AddRange([Lw(2,1,0x5964),0,Add(3,0,6)]);fails.Add(w.Count);w.AddRange([Bne(2,3,0),0]);
  foreach(var p in new (uint G,uint V)[]{(0x800785D8u,copy),(0x800785E4u,scene)}){w.AddRange([Lui(1,p.G),Lw(2,1,(short)p.G),0]);w.AddRange(Ptr(1,p.V));fails.Add(w.Count);w.AddRange([Bne(2,1,0),0]);}w.AddRange([Jump(main),0]);int fallback=w.Count;w.AddRange([0x27BDFFE8,0x3C028007,Jump(OriginalFrame+8),0]);foreach(int n in fails)w[n]|=(ushort)(fallback-n-1);Require(w.Count*4==128,"exact resident prefix128");return Pack(w);

 }
 private static byte[] Installer(uint entry,uint gate,uint state,uint originalInit)
 {
  int[]regs=[1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,24,25,31];List<uint>w=[Add(29,29,-80)];foreach(var p in regs.Select((r,n)=>(r,n)))w.Add(Sw(p.r,29,4*p.n));
  w.AddRange(Ptr(2,state));w.AddRange([Lw(3,2,160),0]);int skip=w.Count;w.AddRange([Bne(3,0,0),0]);w.AddRange(Ptr(4,gate));w.AddRange(Ptr(5,ResidentEntry));w.Add(Add(6,0,128));int loop=w.Count;w.AddRange([Lw(2,4,0),Add(4,4,4),Sw(2,5,0),Add(5,5,4),Add(6,6,-4),Bne(6,0,loop-w.Count-6),0]);w.AddRange(Ptr(4,OriginalFrame));w.AddRange(Ptr(2,Jump(ResidentEntry)));w.AddRange([Sw(2,4,0),Sw(0,4,4),Jal(0x800626E8),0]);w.AddRange(Ptr(2,state));w.AddRange([Add(3,0,1),Sw(3,2,160)]);int restore=w.Count;w[skip]|=(ushort)(restore-skip-1);foreach(var p in regs.Select((r,n)=>(r,n)))w.Add(Lw(p.r,29,4*p.n));w.AddRange([Add(29,29,80),Jump(originalInit),0]);return Pack(w);
 }
 internal void RequireFinal(byte[] exe,byte[] overlay,byte[] data)
 {
  Require(exe.AsSpan().SequenceEqual(_exe)&&overlay.AsSpan().SequenceEqual(_overlay)&&data.AsSpan().SequenceEqual(_data),"all emitted detached final bytes");_source.RequireExecutable(exe);var loaded=Queues.Storage.BindLoaded(exe,overlay,_owner);Require(loaded.CopyBuffer==Queues.CopyBuffer&&loaded.Scene==Queues.Scene&&loaded.Close==Queues.Close&&loaded.Final==Queues.Final&&loaded.End==Queues.End,"actual final loader/resource anchors");
  Require(overlay.Length<=MaximumFinalOverlayBytes&&overlay.Length-OriginalOverlayBytes<=NativeOwnedOverlayMaterializer.MaximumAppendByteLength,"unchanged cumulative loaded code limit");Require(U(exe,(int)(OriginalFrame-UniversalExeBindings.ExeBase))==0x27BDFFE8&&U(exe,(int)(OriginalFrame+4-UniversalExeBindings.ExeBase))==0x3C028007,"on-disc frame entry unchanged; installed only after original CRT");
 }
}
