using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Universal;
using Spyro.Editor.Core.Levels;

int checks=0;
void Check(bool b,string message){if(!b)throw new InvalidOperationException(message);checks++;}
bool Refuses(Action action){try{action();return false;}catch(InvalidDataException){return true;}}
var map=Enumerable.Range(0x53,5).ToDictionary(c=>c,c=>0x1FD-(c-0x53));
const uint codeBase=0x80090000, actor=0x80110000, stop=0x80010004;
var (code,labels)=UniversalBehaviorBundleBuilder.Assemble([UniversalGemCollectionAdapter.Build(map,"gem")],codeBase);
for(int actual=0;actual<512;actual++)
{
    var vm=new StrictMachine(code,codeBase);vm.Set16(actor+0x36,(ushort)actual);
    for(int r=1;r<32;r++)vm.R[r]=(uint)(0xB0000000+r*0x10201);
    vm.R[4]=actor;vm.R[29]=0x801F0000;vm.R[31]=stop;uint[] initial=vm.R.ToArray(),expected=initial.ToArray();
    int canonical=map.FirstOrDefault(p=>p.Value==actual).Key;if(canonical==0)canonical=actual;
    int calls=0;
    vm.External=(target,m)=>
    {
        Check(target==UniversalGemCollectionAdapter.CollectItem,"exact native helper target");calls++;
        Check(m.R[4]==actor&&m.Get16(actor+0x36)==canonical,"physical actor and native semantic class passed");
        Check(m.R[8]==initial[8]&&m.R[9]==initial[9]&&m.R[5]==initial[5]&&m.R[6]==initial[6]&&m.R[7]==initial[7],"callee inputs and scratch inputs preserved");
        Check(m.R[29]==initial[29]-48,"16-byte argument home area reserved");
        for(int n=0;n<16;n+=4)m.Set32(m.R[29]+(uint)n,0xCCCC0000+(uint)n);
        for(int r=1;r<=15;r++)expected[r]=m.R[r]=(uint)(0xDD000000+r*0x10001);
        foreach(int r in new[]{24,25})expected[r]=m.R[r]=(uint)(0xEE000000+r);
    };
    vm.Run(labels["gem"],stop);
    Check(calls==1&&vm.Get16(actor+0x36)==actual,"one helper call and exact mapped class restoration");
    for(int r=1;r<32;r++)Check(vm.R[r]==expected[r],"helper outputs, callee-saved registers, stack and return address preserved r"+r);
}
Check(Refuses(()=>UniversalGemCollectionAdapter.Build(new Dictionary<int,int>{{0x53,512}},"bad")),"out of range class refused");
Check(Refuses(()=>UniversalGemCollectionAdapter.Build(new Dictionary<int,int>{{0x53,511},{0x54,511}},"bad")),"ambiguous class mapping refused");
Check(Refuses(()=>UniversalGemCollectionAdapter.Build(new Dictionary<int,int>{{0x53,0x53}},"identity")),"unused identity adapter refused");
UniversalOverlay Overlay(uint call)
{
    byte[] b=new byte[0x400];BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(0x100),call);
    return new(b,new[]{UniversalExeBindings.InitGlobal,UniversalExeBindings.UpdateGlobal,UniversalExeBindings.CopyBufferGlobal}.ToDictionary(g=>g,g=>new UniversalExeBinding(g,0,0,UniversalOverlay.Base+0x100)));
}
var overlay=Overlay(UniversalMips.EncodeJ(3,UniversalGemCollectionAdapter.CollectItem));
UniversalAsmPart Caller(uint? pc){var p=new UniversalAsmPart();p.Emit(overlay.Word(UniversalOverlay.Base+0x100),orig:pc);p.Emit(0);return p;}
var identityCaller=Caller(UniversalOverlay.Base+0x100);var identityWords=identityCaller.Items.ToArray();
Check(UniversalGemCollectionAdapter.PatchCalls(identityCaller,overlay,new Dictionary<int,int>{{0x53,0x53}},"identity",null).Count==0&&identityCaller.Items.SequenceEqual(identityWords),"identity gem needs no executable or adapter");
Check(UniversalGemCollectionAdapter.PatchCalls(Caller(null),overlay,new Dictionary<int,int>{{0x60,511}},"other",null).Count==0,"non-gem source untouched without executable");
var absent=new UniversalAsmPart();absent.Emit(0);
Check(UniversalGemCollectionAdapter.PatchCalls(absent,overlay,map,"none",null).Count==0,"no collection call needs no executable");
var noExe=Caller(UniversalOverlay.Base+0x100);var before=noExe.Items.ToArray();
Check(Refuses(()=>UniversalGemCollectionAdapter.PatchCalls(noExe,overlay,map,"missing",null))&&noExe.Items.SequenceEqual(before),"needed missing executable refuses before any caller mutation");
DestinationGemCollectionCases.Free(Check);
DestinationGemColourCases.Free(Check);
Console.WriteLine($"UNIVERSAL_GEM_ADAPTER_FREE_OK checks={checks}");
if(args.Length==0)return;
if(args.Length==2&&args[0]=="--emit")
{File.WriteAllText(args[1],JsonSerializer.Serialize(new{Base=codeBase,Code=Convert.ToHexString(code),Map=map}));return;}
if(args.Length==4 && args[0]=="--destination-census")
{
    var censusCatalog=LevelCatalog.Load(args[2]);var censusLibrary=new UniversalSourceLibrary(args[1],censusCatalog);
    DestinationGemCollectionCases.Source(censusLibrary,censusCatalog,args[3],Check);
    DestinationGemColourCases.Source(censusLibrary,censusCatalog,args[3]+".colours.json",Check);
    Console.WriteLine($"DESTINATION_GEM_CALL_OWNERS_OK checks={checks}");return;
}
if(args.Length<3)throw new ArgumentException("USA.bin sourceWorkspace output.bin");
var catalog=LevelCatalog.Load(args[1]);var library=new UniversalSourceLibrary(args[0],catalog);var exe=library.Disc.Exe;
UniversalGemCollectionAdapter.RequireNativeOwner(exe);Check(true,"exact original native code and colour owners authenticated");
foreach(var owner in new[]{(UniversalGemCollectionAdapter.CollectItem,UniversalGemCollectionAdapter.OwnerLength),(UniversalGemCollectionAdapter.Colours,UniversalGemCollectionAdapter.ColourLength)})
for(int i=0;i<owner.Item2;i++)
{var bad=exe.ToArray();bad[(int)(owner.Item1-UniversalExeBindings.ExeBase)+i]^=1;Check(Refuses(()=>UniversalGemCollectionAdapter.RequireNativeOwner(bad)),"every owner byte mutation refused");}
Check(Refuses(()=>UniversalGemCollectionAdapter.PatchCalls(Caller(null),overlay,map,"badowner",exe)),"missing source provenance refused");
var altered=Caller(UniversalOverlay.Base+0x104);
Check(Refuses(()=>UniversalGemCollectionAdapter.PatchCalls(altered,overlay,map,"wrongpc",exe)),"wrong source instruction refused");
var pc=UniversalOverlay.Base+0x100;var patched=Caller(pc);var sites=UniversalGemCollectionAdapter.PatchCalls(patched,overlay,map,"gem",exe);
Check(sites.SequenceEqual(new[]{pc})&&patched.Items[0].Fix.Kind==UniversalFixKind.JalLabel,"exact owned call redirected to source-qualified wrapper");
var donor=library.Get("icecavern");var sourceUpdate=UniversalBehaviorBundleBuilder.BuildPrunedUpdate(donor.Overlay,[0x53],"test",new Dictionary<int,int>{{0x53,0x1FD}}).Part;
var realSites=UniversalGemCollectionAdapter.PatchCalls(sourceUpdate,donor.Overlay,new Dictionary<int,int>{{0x53,0x1FD}},"gem",exe);
Check(realSites.Contains(0x8007D0D0),"exact Ice native source collection call adapted");
string output=Path.GetFullPath(args[2]);if(File.Exists(output))throw new InvalidOperationException("Existing fixture preserved");
var placement=new UniversalMobyPlacement("icecavern",2,84992,47850,8960,EditorId:"gem-owner-ice2-spawn",PersistentSlot:0);
var result=UniversalMobyExport.Apply(library,library.ImagePath,output,catalog,new Dictionary<string,IReadOnlyList<UniversalMobyPlacement>>{{"artisans",new[]{placement}}});
var composed=result.Levels.Single().Compose;var destination=library.Get("artisans");var written=UniversalDiscImage.Read(output);
var data=new UniversalLevelData(written.EntryBytes(destination.Level.SourceWadEntry));var imported=composed.Classes.Single(c=>c.ClassId==0x53&&!c.DependencyOnly);
Check(BinaryPrimitives.ReadUInt16LittleEndian(data.Row(composed.NewRowIndices[0]).AsSpan(0x36))==imported.DestinationClassId&&imported.DestinationClassId!=0x53,"written native row keeps distinct source-scoped model class");
UniversalGemCollectionAdapter.RequireNativeOwner(written.Exe);Check(true,"written EXE helper and colour owners unchanged");
Check(written.EntryBytes(destination.Level.SourceWadEntry-1).SequenceEqual(composed.Overlay),"written overlay equals actual assembled collection adapter");
var report=composed.Behavior.Single(s=>s.SourceLevelKey=="icecavern");
Check(report.GemCollectionCallSites.Contains(0x8007D0D0)&&report.GemCollectionAdapterBytes>0,"written composition reports exact source collection adapter sites and allocation");
Check(composed.DestinationGemCollectionCalls.Count>0&&composed.DestinationGemCollectionExtraBytes==0,"destination native collector reuses existing source adapter without extra allocation");
Check(composed.DestinationGemColourLoads.Count==1&&composed.DestinationGemColourExtraBytes>0,"native linked sparkle color adapter allocated and accounted");
foreach(var colour in composed.DestinationGemColourLoads)
{
    int offset=(int)(colour.PatchPc-UniversalOverlay.Base);
    Check(BinaryPrimitives.ReadUInt32LittleEndian(composed.Overlay.AsSpan(offset))==UniversalMips.EncodeJ(2,colour.Adapter)&&
        BinaryPrimitives.ReadUInt32LittleEndian(composed.Overlay.AsSpan(offset+4))==colour.DelayWord&&
        BinaryPrimitives.ReadUInt32LittleEndian(composed.Overlay.AsSpan(offset+8))==colour.AddWord&&
        BinaryPrimitives.ReadUInt32LittleEndian(composed.Overlay.AsSpan(offset+12))==colour.LoadWord,"written non-linking sparkle patch/source producer exact");
}
foreach(var call in composed.DestinationGemCollectionCalls)
{
    int offset=(int)(call.CallPc-UniversalOverlay.Base);
    Check(BinaryPrimitives.ReadUInt32LittleEndian(composed.Overlay.AsSpan(offset))==UniversalMips.EncodeJ(3,call.Adapter)&&
        BinaryPrimitives.ReadUInt32LittleEndian(composed.Overlay.AsSpan(offset+4))==call.DelayWord,"written destination call and original delay readback");
}
for(int offset=0;offset<(int)(destination.Overlay.CopyBuffer-UniversalOverlay.Base);offset++)
    if(!composed.DestinationGemCollectionCalls.Any(c=>offset>=(int)(c.CallPc-UniversalOverlay.Base)&&offset<(int)(c.CallPc-UniversalOverlay.Base)+4) && !composed.DestinationGemColourLoads.Any(c=>offset>=(int)(c.PatchPc-UniversalOverlay.Base)&&offset<(int)(c.PatchPc-UniversalOverlay.Base)+4))
        Check(composed.Overlay[offset]==destination.Overlay.Bytes[offset],"every unrelated retained native overlay byte exact");
Check(composed.Ram.FinalHeadroom>=UniversalMobyComposer.RamSafetyMargin,"native RAM guard retained");
Check(written.EntryBytes(8).SequenceEqual(library.Disc.EntryBytes(8)),"shared PETE model owner unchanged");
using(var fs=File.OpenRead(library.ImagePath))Check(Convert.ToHexStringLower(SHA256.HashData(fs))==UniversalSourceLibrary.OriginalUsaSha256,"original USA disc unchanged");
File.WriteAllText(Path.ChangeExtension(output,".cue"),$"FILE \"{Path.GetFileName(output)}\" BINARY\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\n");
var receipt=new{Checks=checks,Source="icecavern",SourceRow=2,SourceClass=0x53,imported.DestinationClassId,Position=new{X=84992,Y=47850,Z=8960},composed.NewRowIndices,composed.PlacementSaveRows,composed.Ram,report.GemCollectionCallSites,report.GemCollectionAdapterBytes,composed.DestinationGemCollectionCalls,composed.DestinationGemCollectionExtraBytes,composed.DestinationGemColourLoads,composed.DestinationGemColourExtraBytes,composed.ParticleDependencies,result.OutputImageSha256,CoreModuleVersionId=typeof(UniversalMobyComposer).Module.ModuleVersionId,CoreSha256=Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(typeof(UniversalMobyComposer).Assembly.Location))),RuntimeStatus="not-tested"};
File.WriteAllText(Path.ChangeExtension(output,".readback.json"),JsonSerializer.Serialize(receipt,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine(JsonSerializer.Serialize(receipt));

sealed class StrictMachine(byte[] code,uint codeBase)
{
    public uint[] R=new uint[32];readonly Dictionary<uint,byte> mem=[];
    public Action<uint,StrictMachine>? External;
    public void Set16(uint a,ushort v){mem[a]=(byte)v;mem[a+1]=(byte)(v>>8);}
    public ushort Get16(uint a)=>(ushort)(mem.GetValueOrDefault(a)|(mem.GetValueOrDefault(a+1)<<8));
    public void Set32(uint a,uint v){for(int n=0;n<4;n++)mem[a+(uint)n]=(byte)(v>>(n*8));}
    uint Get32(uint a)=>(uint)(Get16(a)|(Get16(a+2)<<16));
    public void Run(uint pc,uint stop)
    {
        uint? branch=null,loadValue=null;int loadReg=0;
        for(int step=0;step<1000&&pc!=stop;step++)
        {
            if(pc==UniversalGemCollectionAdapter.CollectItem)
            {if(branch!=null||loadValue!=null)throw new Exception("unresolved architectural delay at callee");uint ra=R[31];External!(pc,this);pc=ra;continue;}
            if(pc<codeBase||pc>=codeBase+code.Length)throw new Exception("unowned instruction "+pc.ToString("X"));
            uint w=BinaryPrimitives.ReadUInt32LittleEndian(code.AsSpan((int)(pc-codeBase)));int op=UniversalMips.Op(w),rs=UniversalMips.Rs(w),rt=UniversalMips.Rt(w);uint value=0;bool writes=false,loads=false;uint? nextBranch=null;
            uint address=unchecked(R[rs]+(uint)UniversalMips.SImm(w));
            switch(op)
            {
                case 0:if(w==0)break;if(UniversalMips.Fn(w)==8){nextBranch=R[rs];break;}
                    if(UniversalMips.Fn(w)==0){value=R[rt]<<(int)((w>>6)&31);rt=UniversalMips.Rd(w);writes=true;break;}
                    if(UniversalMips.Fn(w)==0x21){value=unchecked(R[rs]+R[rt]);rt=UniversalMips.Rd(w);writes=true;break;}
                    throw new Exception("unsupported R");
                case 15:value=(uint)UniversalMips.Imm(w)<<16;writes=true;break;
                case 9:value=address;writes=true;break;
                case 35:value=Get32(address);loads=true;break;
                case 37:value=Get16(address);loads=true;break;
                case 41:Set16(address,(ushort)R[rt]);break;
                case 43:Set32(address,R[rt]);break;
                case 5:if(R[rs]!=R[rt])nextBranch=UniversalMips.BranchTarget(w,pc);break;
                case 2:nextBranch=UniversalMips.BranchTarget(w,pc);break;
                case 3:nextBranch=UniversalMips.BranchTarget(w,pc);value=pc+8;rt=31;writes=true;break;
                default:throw new Exception("unsupported opcode "+op);
            }
            if(loadValue is uint old&&loadReg!=0)R[loadReg]=old;
            loadValue=null;if(loads){loadValue=value;loadReg=rt;}else if(writes&&rt!=0)R[rt]=value;
            if(branch!=null&&nextBranch!=null)throw new Exception("control transfer in delay slot");
            uint next=branch??pc+4;branch=nextBranch;pc=next;R[0]=0;
        }
        if(pc!=stop||branch!=null||loadValue!=null)throw new Exception("wrapper did not return cleanly");
    }
}
