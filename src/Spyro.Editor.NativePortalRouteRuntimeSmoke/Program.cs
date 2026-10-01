using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using System.Runtime.InteropServices;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Universal;

int checks=0;
void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);checks++;}
void Refuse(Action action,string message){bool refused=false;try{action();}catch(InvalidDataException){refused=true;}Check(refused,message);}
uint Word(byte[] bytes,uint address)=>BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(checked((int)(address-UniversalOverlay.Base)),4));
void Put(byte[] bytes,uint address,uint value)=>BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(checked((int)(address-UniversalOverlay.Base)),4),value);
const uint oldUpdate=UniversalOverlay.Base+0x100,oldCopy=UniversalOverlay.Base+0x300;
byte[] OriginalOverlay(bool signs=false)
{
    byte[] bytes=new byte[0x800];Put(bytes,oldUpdate,UniversalMips.JrRa);
    if(signs){Put(bytes,oldUpdate-12,UniversalSpecialBehaviors.SignHookMarker);Put(bytes,oldUpdate-8,UniversalMips.EncodeJ(2,UniversalOverlay.Base+0x140));}
    return bytes;
}
var ordinary=OriginalOverlay();var wrapper=NativePortalRouteRuntime.AppendReturnWrapper(ordinary,oldUpdate,oldCopy,10,11);
Check(NativePortalRouteRuntime.BuildPauseExit().Length==176&&NativePortalRouteRuntime.BuildRouteHelper().Length==84,"exact resident source owner budget");
Check(NativePortalRouteRuntime.RouteHelperAddress==NativePortalRouteRuntime.PauseExitAddress+92,"main and helper partition original owner exactly");
Check(NativePortalRouteRuntime.BuildFlightReturn().Length==96,"flight return fits its original owner");
Check(ordinary.Take((int)(oldCopy-UniversalOverlay.Base)).SequenceEqual(wrapper.Overlay.Take((int)(oldCopy-UniversalOverlay.Base))),"all old resident bytes preserved");
Check(UniversalMips.BranchTarget(Word(wrapper.Overlay,wrapper.UpdatePointer),wrapper.UpdatePointer)==oldUpdate,"bound update preserves original tail");
Check(Word(wrapper.Overlay,wrapper.UpdatePointer+16)==NativePortalRouteRuntime.Tag&&Word(wrapper.Overlay,wrapper.UpdatePointer+20)==10&&Word(wrapper.Overlay,wrapper.UpdatePointer+24)==11,"complete PORT metadata placed after SMAP fields");
Check(wrapper.CopyBuffer>=wrapper.ReturnStubPointer+32&&wrapper.Overlay.Length%0x800==0,"appended stub resident allocation and sector alignment");
foreach(var (home,token) in new(int,int)[]{(9,11),(70,71),(15,11),(10,10),(10,21),(60,65),(10,-1)})
    Refuse(()=>NativePortalRouteRuntime.AppendReturnWrapper(ordinary,oldUpdate,oldCopy,home,token),"invalid portal pair refused");
foreach(var (update,copy) in new(uint,uint)[]{(UniversalOverlay.Base-4,oldCopy),(oldUpdate+1,oldCopy),(oldUpdate,oldUpdate+12),(oldUpdate,oldCopy+1),(oldUpdate,UniversalOverlay.Base+0x900)})
    Refuse(()=>NativePortalRouteRuntime.AppendReturnWrapper(ordinary,update,copy,10,11),"invalid resident bounds refused");
var brokenSign=OriginalOverlay(true);Put(brokenSign,oldUpdate-4,1);
Refuse(()=>NativePortalRouteRuntime.AppendReturnWrapper(brokenSign,oldUpdate,oldCopy,10,11),"modified sign prefix refused");
var brokenMap=OriginalOverlay();Put(brokenMap,oldUpdate+8,NativeMobySaveMap.Tag);Put(brokenMap,oldUpdate+12,oldCopy);
Refuse(()=>NativePortalRouteRuntime.AppendReturnWrapper(brokenMap,oldUpdate,oldCopy,10,11),"out-of-resident map leaf refused");
int[] levelIds=Enumerable.Range(10,56).Where(id=>id%10<=5).ToArray();
var endpoints=HomeworldPortalControlCatalog.All.Select(p=>(Home:p.DestinationLevelId/10*10,Token:p.DestinationLevelId)).ToArray();
ScalarMips Machine(byte[] overlay,uint update,int level,byte[]? executable=null)
{
    var m=new ScalarMips();m.Segment(UniversalOverlay.Base,overlay);
    if(executable!=null)m.Segment(UniversalExeBindings.ExeBase,executable);
    else m.Segment(NativePortalRouteRuntime.PauseExitAddress,NativePortalRouteRuntime.BuildPauseExit());
    m.Memory[0x8007596C]=(uint)level;m.Memory[UniversalExeBindings.UpdateGlobal]=update;
    m.Memory[0x800758AC]=0;m.Memory[0x800758B4]=0x77;m.Memory[0x80075864]=0x66;m.Memory[0x8007576C]=0x55;
    return m;
}
foreach(int level in levelIds)
{
    var m=Machine(ordinary,oldUpdate,level);uint[] registers=m.R.ToArray();m.Run(NativePortalRouteRuntime.RouteHelperAddress,registers[31]);
    Check(m.Memory[0x800758AC]==level&&m.Memory[0x800758B4]==level/10*10,"scalar complete default route "+level);
    Check(m.R[4]==registers[4]&&m.R[29]==registers[29]&&m.R[31]==registers[31]&&Enumerable.Range(16,8).All(r=>m.R[r]==registers[r]),"scalar helper preserves owned registers "+level);
    Check(m.Writes.Select(w=>w.Address).SequenceEqual(new uint[]{0x800758AC,0x800758B4}),"helper owns only two route stores "+level);
}
foreach(int level in levelIds)foreach(var endpoint in endpoints)
{
    var route=NativePortalRouteRuntime.AppendReturnWrapper(ordinary,oldUpdate,oldCopy,endpoint.Home,endpoint.Token);
    var m=Machine(route.Overlay,route.UpdatePointer,level);uint a0=m.R[4],ra=m.R[31],sp=m.R[29];
    m.Run(route.ReturnStubPointer,NativePortalRouteRuntime.TransitionAddress);
    Check(m.Memory[0x800758AC]==endpoint.Token&&m.Memory[0x800758B4]==endpoint.Home,"scalar configured endpoint "+level+":"+endpoint.Token);
    Check(m.R[4]==a0&&m.R[31]==ra&&m.R[29]==sp,"transition stub preserves a0/ra/frame "+level+":"+endpoint.Token);
    Check(m.Calls.SequenceEqual(new[]{NativePortalRouteRuntime.RouteHelperAddress})&&m.PendingLoad==null,"stub follows resident helper then native transition "+level+":"+endpoint.Token);
}
foreach(bool signs in new[]{false,true})
{
    var original=OriginalOverlay(signs);var bindings=new Dictionary<uint,UniversalExeBinding>();
    foreach(var (global,value) in new(uint,uint)[]{(UniversalExeBindings.InitGlobal,oldUpdate),(UniversalExeBindings.UpdateGlobal,oldUpdate),(UniversalExeBindings.CopyBufferGlobal,oldCopy)})bindings[global]=new(global,0,0,value);
    var mapped=NativeMobySaveMap.Append(new(original,bindings),original,oldUpdate,oldCopy,[2,0,1]);
    var routed=NativePortalRouteRuntime.AppendReturnWrapper(mapped.Overlay,mapped.UpdatePointer,mapped.CopyBuffer,60,64);
    Check(Word(routed.Overlay,routed.UpdatePointer+8)==NativeMobySaveMap.Tag&&Word(routed.Overlay,routed.UpdatePointer+12)==Word(mapped.Overlay,mapped.UpdatePointer+12),"SMAP tag and exact leaf preserved "+signs);
    if(signs)Check(routed.Overlay.AsSpan((int)(routed.UpdatePointer-12-UniversalOverlay.Base),12).SequenceEqual(original.AsSpan((int)(oldUpdate-12-UniversalOverlay.Base),12)),"SIGN marker and jump remain identical");
    for(uint physical=0;physical<5;physical++)
    {
        var m=Machine(routed.Overlay,routed.UpdatePointer,65);m.Segment(NativeMobySaveMap.MapRowAddress,NativeMobySaveMap.GatewayRoutine());m.R[6]=physical;
        m.Run(NativeMobySaveMap.MapRowAddress,m.R[31]);
        Check(m.R[6]==(physical<3?new uint[]{2,0,1}[physical]:uint.MaxValue),"native map gateway still routes actual logical row "+physical+":"+signs);
    }
    var update=Machine(routed.Overlay,routed.UpdatePointer,65);update.Run(routed.UpdatePointer,update.R[31]);
    Check(update.Writes.Count==0,"normal update chains without running metadata or return stub");
}
Console.WriteLine($"NATIVE_PORTAL_ROUTE_FREE_OK checks={checks} configuredPairs={levelIds.Length*endpoints.Length}");
if(args.Length==0)return;
if(args.Length!=3||args[0]!="--source")throw new ArgumentException("--source <USA.bin> <workspace>");
var disc=UniversalDiscImage.Read(args[1]);byte[] originalExe=disc.Exe,staged=originalExe.ToArray();
NativePortalRouteRuntime.PatchPauseExit(originalExe,staged);NativePortalRouteRuntime.PatchFlightReturn(originalExe,staged);
var patched=staged.ToArray();NativePortalRouteRuntime.PatchPauseExit(originalExe,staged);NativePortalRouteRuntime.PatchFlightReturn(originalExe,staged);
Check(staged.SequenceEqual(patched),"canonical executable patches are idempotent");
var changedOffsets=Enumerable.Range(0,staged.Length).Where(i=>originalExe[i]!=staged[i]).ToArray();
Check(changedOffsets.All(i=>i>=NativePortalRouteRuntime.PauseExitAddress-UniversalExeBindings.ExeBase&&i<NativePortalRouteRuntime.PauseExitAddress-UniversalExeBindings.ExeBase+176||i>=NativePortalRouteRuntime.FlightReturnAddress-UniversalExeBindings.ExeBase&&i<NativePortalRouteRuntime.FlightReturnAddress-UniversalExeBindings.ExeBase+96),"only both authenticated owner windows changed");
foreach(uint address in new[]{NativePortalRouteRuntime.PauseExitAddress,NativePortalRouteRuntime.PauseExitAddress+172,NativePortalRouteRuntime.ResetAddress,NativePortalRouteRuntime.PauseCallerAddress})
{
    var bad=originalExe.ToArray();bad[(int)(address-UniversalExeBindings.ExeBase)]^=1;byte[] before=bad.ToArray();
    Refuse(()=>NativePortalRouteRuntime.PatchPauseExit(originalExe,bad),"changed staged native pause owner refused "+address.ToString("X8"));
    Check(bad.SequenceEqual(before),"rejected pause leaves all bytes intact");
    Refuse(()=>NativePortalRouteRuntime.PatchPauseExit(bad,originalExe.ToArray()),"changed source native pause owner refused");
}
foreach(uint address in new[]{NativePortalRouteRuntime.FlightReturnAddress,0x8002F0B0u,0x8002F350u,0x8002F3A4u})
{
    var bad=staged.ToArray();bad[(int)(address-UniversalExeBindings.ExeBase)]^=1;byte[] before=bad.ToArray();
    Refuse(()=>NativePortalRouteRuntime.PatchFlightReturn(originalExe,bad),"changed staged flight owner refused "+address.ToString("X8"));
    Check(bad.SequenceEqual(before),"rejected flight leaves all bytes intact");
}
Refuse(()=>NativePortalRouteRuntime.PatchFlightReturn(originalExe,originalExe.ToArray()),"flight refuses absent resident helper");
uint[] originalPauseGlobals=[0x800770C8,0x800770CC,0x800770D0,0x800770D4,0x800770D8,0x800770DC,0x80075864,0x800756AC,0x80076E90,0x8007576C,0x800758AC,0x800757D8,0x800756D0,0x800756B0,0x8007579C,0x800758B4];
foreach(int level in levelIds)
{
    var baseline=Machine(ordinary,oldUpdate,level,originalExe);var actual=Machine(ordinary,oldUpdate,level,staged);
    baseline.Run(NativePortalRouteRuntime.PauseExitAddress,baseline.R[31]);actual.Run(NativePortalRouteRuntime.PauseExitAddress,actual.R[31]);
    Check(originalPauseGlobals.All(a=>baseline.Memory[a]==actual.Memory[a]),"actual original and emitted pause effects match "+level);
    Check(actual.Calls.SequenceEqual(new[]{NativePortalRouteRuntime.ResetAddress,NativePortalRouteRuntime.RouteHelperAddress})&&baseline.Calls.SequenceEqual(new[]{NativePortalRouteRuntime.ResetAddress}),"actual native reset retained exactly once "+level);
    Check(actual.R[29]==baseline.R[29]&&actual.R[31]==baseline.R[31],"actual pause prolog/return frame matches "+level);
}
void FlightFrame(ScalarMips m)
{
    m.Memory[m.R[29]+0x48]=m.R[31];m.Memory[m.R[29]+0x44]=m.R[19];m.Memory[m.R[29]+0x40]=m.R[18];m.Memory[m.R[29]+0x3C]=m.R[17];m.Memory[m.R[29]+0x38]=m.R[16];
}
foreach(int level in levelIds)
{
    var baseline=Machine(ordinary,oldUpdate,level,originalExe);var actual=Machine(ordinary,oldUpdate,level,staged);FlightFrame(baseline);FlightFrame(actual);
    uint stop=baseline.R[31];baseline.Run(NativePortalRouteRuntime.FlightReturnAddress,stop);actual.Run(NativePortalRouteRuntime.FlightReturnAddress,stop);
    Check(new uint[]{0x800758AC,0x800758B4,0x80075864,0x8007576C,0x80075940,0x8007593C}.All(a=>baseline.Memory[a]==actual.Memory[a]),"actual original and emitted flight defaults match "+level);
    Check(actual.R[29]==baseline.R[29]&&Enumerable.Range(16,8).All(r=>actual.R[r]==baseline.R[r])&&actual.R[31]==baseline.R[31],"actual flight continuation frame matches "+level);
}
foreach(int level in levelIds)foreach(var endpoint in endpoints)
{
    var route=NativePortalRouteRuntime.AppendReturnWrapper(ordinary,oldUpdate,oldCopy,endpoint.Home,endpoint.Token);
    foreach(bool flight in new[]{false,true})
    {
        var m=Machine(route.Overlay,route.UpdatePointer,level,staged);uint stop=m.R[31];if(flight)FlightFrame(m);
        m.Run(flight?NativePortalRouteRuntime.FlightReturnAddress:NativePortalRouteRuntime.PauseExitAddress,stop);
        Check(m.Memory[0x800758AC]==endpoint.Token&&m.Memory[0x800758B4]==endpoint.Home,"actual custom pause/flight route "+level+":"+endpoint.Token+":"+flight);
        Check(m.Memory[0x80075864]==(flight?2:0)&&m.Memory[0x8007576C]==uint.MaxValue,"native custom transition flags retained "+flight);
    }
}
var catalog=LevelCatalog.Load(args[2]);var library=new UniversalSourceLibrary(args[1],catalog);int nativeSites=0;
foreach(var level in catalog.Levels.Where(l=>l.SourceWadEntry>0))
{
    var source=library.Get(level.Key);var dispatch=UniversalDispatchAnalysis.FindUpdateSwitch(source.Overlay);
    if(!UniversalDispatchAnalysis.Handles(source.Overlay,dispatch,9))continue;
    var trace=UniversalDispatchAnalysis.Trace(source.Overlay,dispatch.TreeStart,9,dispatch.Seeds,dispatch.ClassRegister);
    var closure=UniversalDispatchAnalysis.Closure(source.Overlay,dispatch.Function,[trace.Stop],new HashSet<uint>{dispatch.TreeStart}).Instructions;
    var callPcs=closure.Where(pc=>UniversalMips.IsJal(source.Overlay.Word(pc))&&UniversalMips.BranchTarget(source.Overlay.Word(pc),pc)==NativePortalRouteRuntime.TransitionAddress).ToArray();
    if(callPcs.Length==0)continue;
    var route=NativePortalRouteRuntime.AppendReturnWrapper(source.Overlay.Bytes.ToArray(),source.Overlay.Update,source.Overlay.CopyBuffer,30,33);
    byte[] before=route.Overlay.ToArray();int count=NativePortalRouteRuntime.PatchReturnHomeCalls(library,route,source.Overlay.Bindings,source.Data,level.Key);
    Check(count==callPcs.Length,"all exact native class009 calls patched "+level.Key);nativeSites+=count;
    var after=route.Overlay.ToArray();Check(NativePortalRouteRuntime.PatchReturnHomeCalls(library,route.Overlay,route.ReturnStubPointer)==count&&route.Overlay.SequenceEqual(after),"native call patch idempotent "+level.Key);
    Check(Enumerable.Range(0,before.Length).Where(i=>before[i]!=after[i]).All(i=>callPcs.Any(pc=>i>=(int)(pc-UniversalOverlay.Base)&&i<(int)(pc-UniversalOverlay.Base)+4)),"only exact source call words change "+level.Key);
    foreach(uint call in callPcs)
    {
        var m=Machine(route.Overlay,route.UpdatePointer,level.LevelId,staged);m.R[2]=(uint)level.LevelId;m.R[5]=(uint)level.LevelId;m.Product(level.LevelId,0x66666667);
        m.Run(call-44,NativePortalRouteRuntime.TransitionAddress);
        Check(m.Memory[0x800758AC]==33&&m.Memory[0x800758B4]==30&&m.R[4]==0&&m.R[31]==call+8,"actual native packet uses configured route and correct return argument "+level.Key);
    }
}
Check(nativeSites==23,"all23 authenticated source ReturnHome sites covered");
// Exercise actual relayout output, so original packet PC metadata need not survive in the final overlay.
{
    var source=library.Get("stonehill");var (part,_,_)=UniversalBehaviorBundleBuilder.BuildPrunedUpdate(source.Overlay,[9],"imported_return",new Dictionary<int,int>{{9,0x1FD}});
    uint append=(oldCopy+3)&~3u;byte[] behavior=UniversalBehaviorBundleBuilder.Assemble([part],append).Code;
    uint resident=append+(uint)behavior.Length;byte[] bytes=new byte[UniversalLevelData.Align((int)(resident-UniversalOverlay.Base),0x800)];ordinary.AsSpan(0,(int)(oldCopy-UniversalOverlay.Base)).CopyTo(bytes);behavior.CopyTo(bytes,(int)(append-UniversalOverlay.Base));
    var route=NativePortalRouteRuntime.AppendReturnWrapper(bytes,oldUpdate,resident,50,52);
    Check(NativePortalRouteRuntime.PatchReturnHomeCalls(library,route.Overlay,route.ReturnStubPointer)==1,"actual remapped/pruned imported handler packet is recognized");
    uint call=0;for(uint pc=append;pc+4<=resident;pc+=4)if(Word(route.Overlay,pc)==UniversalMips.EncodeJ(3,route.ReturnStubPointer))call=pc;
    Check(call!=0,"imported source call relocates to actual resident route stub");
    var m=Machine(route.Overlay,route.UpdatePointer,65,staged);m.R[2]=65;m.R[5]=65;m.Product(65,0x66666667);m.Run(call-44,NativePortalRouteRuntime.TransitionAddress);
    Check(m.Memory[0x800758AC]==52&&m.Memory[0x800758B4]==50&&m.R[31]==call+8,"actual imported packet routes authoredID65 to selected native endpoint");
    var wrong=route.Overlay.ToArray();Put(wrong,call-40,Word(wrong,call-40)^1);
    Check(NativePortalRouteRuntime.PatchReturnHomeCalls(library,wrong,route.ReturnStubPointer)==0,"changed packet is never promoted to an owned common transition");
    var unmodifiedCalls=route.Overlay.ToArray();ref byte ownerByte=ref MemoryMarshal.GetReference(source.Overlay.Bytes.Slice(checked((int)(0x8007E064-UniversalOverlay.Base))));
    byte savedOwner=ownerByte;ownerByte^=1;
    try{Refuse(()=>NativePortalRouteRuntime.PatchReturnHomeCalls(library,unmodifiedCalls,route.ReturnStubPointer),"changed original source packet owner refused");Check(unmodifiedCalls.SequenceEqual(route.Overlay),"source certificate rejection occurs before mutation");}
    finally{ownerByte=savedOwner;}
    var brokenStub=route.Overlay.ToArray();Put(brokenStub,route.ReturnStubPointer+8,0);
    Refuse(()=>NativePortalRouteRuntime.PatchReturnHomeCalls(library,brokenStub,route.ReturnStubPointer),"changed resident return stub refused before any call mutation");
    var branch=route.Overlay.ToArray();Put(branch,call-48,UniversalMips.EncodeJ(2,call-44));
    Refuse(()=>NativePortalRouteRuntime.PatchReturnHomeCalls(library,branch,route.ReturnStubPointer),"copied source packet in a new delay slot refused");
}
await FreshBindingCases.Run(args[1],originalExe,args[2],library,Check,Refuse);
string sourceHash;using(var source=File.OpenRead(args[1]))sourceHash=Convert.ToHexStringLower(SHA256.HashData(source));
Check(sourceHash==UniversalSourceLibrary.OriginalUsaSha256,"original source image remains intact");
Console.WriteLine($"NATIVE_PORTAL_ROUTE_SOURCE_OK checks={checks} sourceSha256={sourceHash} runtime=not-tested");
