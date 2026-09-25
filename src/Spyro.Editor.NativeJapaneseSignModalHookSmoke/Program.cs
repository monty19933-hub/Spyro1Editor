using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;

int checks=0;
void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
void Refuse(Action action,string message){try{action();}catch(InvalidDataException){checks++;return;}throw new Exception("Expected refusal: "+message);}
static void Put(byte[] b,uint pc,uint value)=>BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(checked((int)(pc-0x8000F800))),value);
static string Hash(byte[] b)=>Convert.ToHexStringLower(SHA256.HashData(b));
static byte[] Fixture()
{
    byte[] b=new byte[0x66000];
    foreach(var (pc,w) in new (uint,uint)[]{
        (0x8001EF40,0x14620080),(0x80033A14,0x14620086),(0x8001EE34,0x0C006903),
        (0x8001A5C0,0x0C017DD9),(0x8001A5C4,0x00002021),(0x8001C598,0x0C0059E1),(0x8001C59C,0x00002021),
        (0x8002C57C,0x0C017DD9),(0x8002C580,0x00002021),(0x8002C7FC,0x0C017DD9),(0x8002C800,0x00002021),
        (0x80074A5C,0x80074A1C),(0x80074A24,0x80061820),(0x80074A38,0x80061470),(0x80074A3C,0x80061234),(0x80074A58,0x80061F48),
        (0x80061F78,0x14400040),(0x80061F7C,0x2402FFFF),(0x80061FAC,0x14400033),(0x80061FB0,0x2402FFFF),
        (0x80061FEC,0x1040FFED),(0x80061FF0,0x00001021),(0x80061ADC,0x3042003F),(0x8006185C,0x144000A0),(0x80061860,0x2402FFFF)})Put(b,pc,w);
    return b;
}
void Gates(NativeJapaneseSignModalHookPlan plan)
{
    Check(!plan.CompleteModalLifetimeProven&&!plan.LiveGpuDriverIdentityProven&&!plan.ControllerRamAllocated&&
        !plan.NativeHooksEmitted&&!plan.InstallationApproved&&!plan.PublicationApproved,"no admission promotion");
    Refuse(plan.RequireInstallation,"native installation still closed");
}
byte[] fixture=Fixture();var decoded=NativeJapaneseSignModalHookPlan.DecodeFixture(fixture);
Check(!decoded.SourceBound&&!decoded.ExactCandidateBeforeimagesProven&&decoded.Resident==null,"fixture cannot gain native authority");
Check(decoded.Transfers.Count==0,"fixture cannot issue transfer pointers into a native resident allocation");
Gates(decoded);Check(decoded.Sites.Count==7,"seven distinct exact paired candidate sites");
foreach(var site in decoded.Sites)
{
    byte[] before=site.CopyBeforeBytes();Check(before.Length==8&&Hash(before)==site.BeforeSha256,"exact instruction plus delay owner");
    before[0]^=1;Check(!before.SequenceEqual(site.CopyBeforeBytes()),"defensive site bytes");
    byte[] bad=fixture.ToArray();bad[(int)(site.Pc-0x8000F800)]^=1;
    Refuse(()=>NativeJapaneseSignModalHookPlan.DecodeFixture(bad),"mutated hook instruction");
    bad=fixture.ToArray();bad[(int)(site.Pc-0x8000F800)+4]^=1;
    Refuse(()=>NativeJapaneseSignModalHookPlan.DecodeFixture(bad),"mutated delay instruction");
}
foreach(uint pc in new uint[]{0x80074A5C,0x80074A24,0x80074A38,0x80074A3C,0x80074A58,0x80061F78,0x80061F7C,
    0x80061FAC,0x80061FB0,0x80061FEC,0x80061FF0,0x80061ADC,0x8006185C,0x80061860})
{byte[] bad=fixture.ToArray();bad[(int)(pc-0x8000F800)]^=1;Refuse(()=>NativeJapaneseSignModalHookPlan.DecodeFixture(bad),"SDK binding/return mutation");}
Refuse(()=>NativeJapaneseSignModalHookPlan.DecodeFixture(fixture[..^1]),"short executable");
Refuse(()=>NativeJapaneseSignModalHookPlan.DecodeFixture([..fixture,0]),"long executable");
byte[] copy=decoded.CopyExecutable();copy[0]^=1;
Refuse(()=>decoded.RequireBeforeimages(copy),"outside-site complete beforeimage corruption");
Refuse(()=>decoded.RequireResidentBody(new byte[0xA000]),"fixture cannot claim resident RAM");
fixture[0]^=1;decoded.RequireBeforeimages(decoded.CopyExecutable());checks++;

Refuse(()=>NativeSignFontProtocol.Begin(0),"no anonymous generation");
var initial=NativeSignFontProtocol.Begin(7);
Refuse(()=>initial.CompleteRequest(8,0),"wrong modal generation");
Refuse(()=>initial.Close(7),"cannot restore before capture/upload completes");
var protocol=initial;List<NativeSignFontProtocol> pending=[];
while(protocol.Phase!=NativeSignFontProtocolPhase.Active)
{
    pending.Add(protocol);Check(!protocol.MayIssueGlyphPackets&&!protocol.MayResumeWorld,"enter dependency barriers remain closed");
    protocol=protocol.CompleteRequest(7,0);
}
Check(protocol.MayIssueGlyphPackets&&!protocol.MayResumeWorld,"only completed atlas permits glyphs, not ordinary world");
Refuse(()=>protocol.CompleteRequest(7,0),"active has no pending GPU request");
var active=protocol;protocol=protocol.Close(7);
while(protocol.Phase!=NativeSignFontProtocolPhase.Restored)
{
    pending.Add(protocol);Check(!protocol.MayIssueGlyphPackets&&!protocol.MayResumeWorld,"exit dependency barriers remain closed");
    protocol=protocol.CompleteRequest(7,0);
}
Check(protocol.MayResumeWorld&&!protocol.MayIssueGlyphPackets,"only complete restoration releases ordinary world");
Check(initial.Phase==NativeSignFontProtocolPhase.EnterDrain&&active.Phase==NativeSignFontProtocolPhase.Active,"immutable phase snapshots");
foreach(var state in pending)
{
    foreach(int failure in new[]{-1,int.MinValue,64,int.MaxValue})
    {
        var fault=state.CompleteRequest(7,failure);
        Check(fault.Phase==NativeSignFontProtocolPhase.Faulted&&!fault.MayResumeWorld&&!fault.MayIssueGlyphPackets,"all request failures stay fail-closed");
        Refuse(()=>fault.CompleteRequest(7,0),"successful unrelated return cannot repair failed ownership");
    }
    bool queue=state.Phase is NativeSignFontProtocolPhase.CaptureEnqueue or NativeSignFontProtocolPhase.AtlasEnqueue or NativeSignFontProtocolPhase.RestoreEnqueue;
    for(int result=1;result<64;result++)
        Check((state.CompleteRequest(7,result).Phase==NativeSignFontProtocolPhase.Faulted)==!queue,"queue admission depth is not a drain success");
}
foreach(var state in pending.Append(active).Append(protocol))
{
    Check(!state.UnexpectedExit(7).MayResumeWorld,"unknown exit never silently permits ordinary draws");
    Refuse(()=>state.InvalidateForReplacement(7),"backup generation nonreuse");
    var invalid=state.InvalidateForReplacement(8);
    Check(invalid.Phase==NativeSignFontProtocolPhase.Invalidated&&!invalid.MayResumeWorld,"level replacement is not proof of new textures");
    Refuse(()=>invalid.CompleteRequest(7,0),"old generation cannot restore into new level");
}
Console.WriteLine($"JAPANESE_SIGN_MODAL_HOOK_SOURCE_FREE_OK checks={checks}");
if(args.Length==0)return;
if(args.Length!=3||args[0]!="--source")throw new Exception("Use --source <exact-JP-BIN> <exact-USA-BIN>");
using var japan=NativeRegionalSourceSession.Open(args[1],NativeDiscRevision.JapanScps10083);
using var usa=NativeRegionalSourceSession.Open(args[2],NativeDiscRevision.UsaScus94228);
var resident=NativeJapaneseSignFontResidentPlan.Capture(japan,usa);
var native=NativeJapaneseSignModalHookPlan.Capture(usa,resident);byte[] exe=native.CopyExecutable();
Check(native.SourceBound&&native.ExactCandidateBeforeimagesProven&&native.InitialSdkReturnSemanticsSourceBound,"source-issued exact boundaries");
Check(ReferenceEquals(native.Resident,resident),"actual resident ancestor retained");Gates(native);
Check(native.Transfers.Count==3,"three exact proposed mirror-transfer roles");
foreach(var transfer in native.Transfers)
{
    Check(transfer.WordX==512&&transfer.Y==472&&transfer.WordWidth==512&&transfer.Height==40&&transfer.ByteLength==0xA000,
        "whole mirrored native atlas extent, never backdrop-only or arbitrary VRAM");
    Check(ReferenceEquals(transfer.CpuOwner,transfer.Phase==NativeSignFontProtocolPhase.AtlasEnqueue?resident.Atlas:resident.Backup)&&
        transfer.CpuOwner.FinalExtent.ByteLength==transfer.ByteLength,"exact typed CPU ancestor for each GPU request");
    Check(!transfer.NativeCallInstalled,"transfer requirements do not become installed calls");
}
native.RequireBeforeimages(exe);native.RequireResidentBody(resident.CopyLevelData());checks+=2;
byte[] badBody=resident.CopyLevelData();badBody[(int)resident.Backup.FinalExtent.Offset]^=1;
Refuse(()=>native.RequireResidentBody(badBody),"mutated resident backup initialization");
foreach(var evidence in NativeJapaneseSignModalHookPlan.Evidence)
    Check(Hash(native.CopyEvidence(evidence))==evidence.Sha256,"source evidence independent readback: "+evidence.Role);
RenderProbeCpu Cpu()
{
    var cpu=new RenderProbeCpu();cpu.Copy(0x8000F800,exe);cpu.Regs[29]=0x801FF000;cpu.Regs[31]=0x801FFF00;return cpu;
}
uint[] drawTargets=[0x8001EF80,0x8001EE88,0x8001EE34,0x8001EE34,0x8001EE1C,0x8001EE1C,0x8001EE34,0x8001EE4C,
    0x8001EE70,0x8001EE88,0x8001EEAC,0x8001EEC4,0x8001EEDC,0x8001EEF4,0x8001EF30,0x8001EF48];
uint[] updateTargets=[0x80033A54,0x800338A0,0x800338B8,0x800338D0,0x800338F0,0x800338F0,0x80033908,0x80033920,
    0x80033944,0x8003395C,0x80033980,0x80033998,0x800339B0,0x800339C8,0x80033A04,0x80033A1C];
foreach(uint mode in Enumerable.Range(0,32).Select(i=>(uint)i).Append(uint.MaxValue))
{
    var cpu=Cpu();cpu.Put(0x800757D8,mode);cpu.Run(0x8001EDDC,mode<16?drawTargets[mode]:0x8001F144);
    Check(cpu.Calls.Count==0,"draw ladder reaches exact destination before any unmodeled callee");
    Check(cpu.Visited.Contains(0x8001EF40)==(mode>=15),"draw unknown-mode candidate reached only after retained legacy states");
    cpu=Cpu();cpu.Put(0x800757D8,mode);cpu.Run(0x80033884,mode<16?updateTargets[mode]:0x80033C30);
    Check(cpu.Calls.Count==0,"update ladder retains existing destination");
    Check(cpu.Visited.Contains(0x80033A14)==(mode>=15),"update unknown-mode candidate preserves legacy states");
}
foreach(uint frame in new uint[]{0,1,2,255,uint.MaxValue})
{
    var cpu=Cpu();cpu.Put(0x800758B8,frame);cpu.Run(0x8001A40C,frame==0?0x8001A444:0x8001A5E0);
    Check(cpu.Calls.Count==0&&cpu.Regs[29]==0x801FF000-0x150,"native first/later frame branch and frame ABI");
}
{
    var cpu=Cpu();cpu.Put(0x800785E8,resident.Memory.NormalReservedStart);cpu.Put(0x800785F0,resident.Memory.NormalReservedStart+0x38000);
    cpu.Run(0x8001A604,0x8001A654);
    Check(cpu.Get(0x800757B0)==resident.Memory.NormalReservedStart&&cpu.Get(0x80075780)==resident.Memory.NormalReservedStart+0x1C000,
        "later native frame publishes actual primitive arena, not atlas or backup RAM");
    Check(cpu.Get(0x800757B0)>resident.Backup.ModeledNormalRamEnd,"explicit resident backup is distinct from normal primitive workspace");
}
// Execute source UI queue seal with fixture-owned pointers. It clears head/tail
// before submission; drawing after the modal function returns misses this list.
{
    var cpu=Cpu();cpu.Put(0x8007581C,0x801E0000);cpu.Put(0x801E0000,0x801E0100);cpu.Put(0x801E0004,0x801E0200);
    cpu.Put(0x801E0100,0x28000000);cpu.Run(0x80016854,0x801FFF00);
    Check(cpu.Get(0x801E0000)==0&&cpu.Get(0x801E0004)==0&&cpu.Regs[2]==0x801E0200,"actual UI queue consumed and returned to caller");
    Check(cpu.Get(0x801E0100)==0x2806BBE0,"native UI terminator preserves packet command byte");
}
// Actual source blocking-drain control flow, with timer/queue-pump callbacks
// intercepted and fake RAM status cells. This is not GPU or callback execution.
foreach(var (head,tail,dma,gpu,expected) in new (uint,uint,uint,uint,uint)[]{
    (0,0,0,0x04000000,0),(2,1,0,0x04000000,uint.MaxValue),
    (0,0,0x01000000,0x04000000,uint.MaxValue),(0,0,0,0,uint.MaxValue)})
{
    var cpu=Cpu();cpu.Put(0x80074B68,head);cpu.Put(0x80074B6C,tail);
    cpu.Put(0x80074B44,0x801E1000);cpu.Put(0x801E1000,dma);cpu.Put(0x80074B38,0x801E1004);cpu.Put(0x801E1004,gpu);
    foreach(uint target in new uint[]{0x80062090,0x80061B00}){cpu.Put(target,0x03E00008);cpu.Put(target+4,0);}
    cpu.Put(0x800620C4,0x03E00008);cpu.Put(0x800620C8,0x2402FFFF);
    cpu.Run(0x80061F48,0x801FFF00);Check(cpu.Regs[2]==expected,"native blocking drain distinguishes idle success from timeout failure");
}
foreach(var (head,tail) in new (uint,uint)[]{(0,0),(0,1),(63,0),(0,63),(37,12)})
{
    var cpu=Cpu();cpu.Put(0x80074B68,head);cpu.Put(0x80074B6C,tail);cpu.Run(0x80061AC4,0x80061AE0);
    Check(cpu.Regs[2]==((head-tail)&63),"native queue admission returns depth, not completion");
}
// Both ordinary source exits ignore nonzero drain result before writing mode0.
foreach(uint start in new uint[]{0x8002C584,0x8002C804})
{
    var cpu=Cpu();cpu.Regs[2]=uint.MaxValue;cpu.Put(0x800757D8,2);cpu.Run(start,start+8);
    Check(cpu.Get(0x800757D8)==0,"native ordinary resume alone is not a checked font restoration barrier");
}
japan.VerifyUnchanged();usa.VerifyUnchanged();checks+=2;
Console.WriteLine($"JAPANESE_SIGN_MODAL_HOOK_SOURCE_OK checks={checks} plan={native.PlanSha256} nativeHooks=false lifetime=false publication=false");
