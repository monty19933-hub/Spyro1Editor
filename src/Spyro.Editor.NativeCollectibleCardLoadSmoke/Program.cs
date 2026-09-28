using System.Buffers.Binary;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

int checks = 0;
void Check(bool value, string why) { checks++; if (!value) throw new Exception(why); }
void Refuse(Action action)
{ try { action(); } catch(Exception e) when(e is InvalidDataException or ArgumentException) { checks++;return; } throw new Exception("Expected bounded refusal."); }
if(args.Length>1) throw new ArgumentException("Only an optional original USA BIN is accepted; no card path or output mode exists.");
Guid Id(int n) => Guid.Parse(n.ToString("x32"));
var manifest = DetachedCollectibleManifest.CreateNew(Id(9999), Enumerable.Range(1,257).Select(Id));
var plan = NativeCollectibleCardLoadPlan.CreateFixture(manifest, 1);
Check(!plan.SourceCertified && !plan.PublicationApproved && !plan.ReadyIssued, "Fixture never issues native admission.");
Check(plan.CopyOverlay().Length > 0x3800 && plan.CopyOverlay().Length % 0x800 == 0, "Complete original allocation retained before appended code/data.");
const uint Stop=0x80010000, Stack=0x801FFCA0, Menu=0x80160000, Record=0x80100000;
const uint Busy=0x80075B50, Latch=0x80075B58;
byte[] poll=Convert.FromHexString("0780023c505b42240000428c0000000006004014000000000780023c585b428c0000000030004010ffff02240780083c505b08250000078d0780033c545b638c0c008014000000000780023c585b428c0000000013004014080003250000628c00000000fdff401000000000b39d0108000000000780023c585b428c0000000008004014000000000200c010000000000000c3ac1400a01021100000c49d01080000a7ac0600c010000000000780023c985b42240000428c000000000000c2ac0600a010000000000780023c945b42240000428c000000000000a2ac0780013c585b20ac010002240800e00300000000");
byte[] Words(params uint[] words)
{ byte[] b=new byte[words.Length*4]; for(int i=0;i<words.Length;i++) BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(i*4),words[i]); return b; }
byte[] Snapshot(RenderProbeCpu c,uint at,int n) => Enumerable.Range(0,n).Select(i=>(byte)c.Get(at+(uint)i,1)).ToArray();
byte[] Prefix()
{
    byte[] b=new byte[0x1400]; new Random(927).NextBytes(b);
    BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(0x7E),0xABCD); return b;
}
RenderProbeCpu Initial(NativeCollectibleCardLoadPlan p)
{
    var c=new RenderProbeCpu { StrictSignedArithmetic=true };
    c.Copy(NativeCollectibleCardLoadPlan.OverlayBase,p.CopyOverlay());
    c.Copy(NativeCollectibleCardLoadPlan.PollPc,poll);
    // Explicit boundary stub only: record the read API arguments, expose an
    // accepted read as pending, but execute no BIOS, IRQ, callback or card I/O.
    c.Copy(NativeCollectibleCardLoadPlan.ReadPc,Words(0x3C088010,0xAD040000,0xAD050004,0xAD060008,0xAD07000C,
        0x8FA90010,0,0xAD090010,0x8D020014,0,0x10400005,0,0x3C088007,0x24090003,0xAD095B50,0xAD005B58,0x03E00008,0));
    c.Copy(NativeCollectibleCardLoadPlan.ShutdownPc,Words(0x3C088010,0x24090001,0xAD090018,0x03E00008,0));
    c.Put(Record+20,1); c.Copy(Menu,Prefix());
    foreach(var (a,v) in new (uint,uint)[] { (0x80078D14,1),(0x80078D18,16),(0x80078D1C,(uint)p.OrdinarySlot),
        (0x80078D20,0xABCD),(0x80078D78,2),(0x80078D7C,5),(0x80078D8C,(uint)p.OrdinarySlot),
        (0x80078D94,1),(0x80078DA0,1),(0x80078DC4,Menu),(0x800785E8,Menu+0x2000) }) c.Put(a,v);
    c.Copy(p.ShadowAddress,Enumerable.Repeat((byte)0xA5,512).ToArray());
    c.Copy(0x801FF600,Enumerable.Repeat((byte)0xEC,512).ToArray());
    for(int r=1;r<32;r++) c.Regs[r]=0xA0000000u+(uint)r;
    c.Regs[4]=0;c.Regs[5]=0x80078DB8;c.Regs[6]=0x80078DBC;c.Regs[29]=Stack;c.Regs[31]=Stop;
    c.Hi=0xAB12CD34;c.Lo=0xEF56AB78;
    return c;
}
void Run(RenderProbeCpu c,uint pc,uint result)
{
    c.Regs[31]=Stop; var before=c.Regs.ToArray(); c.Run(pc,Stop);
    Check(c.Regs[2]==result,$"Expected result {result}, got {c.Regs[2]} at {pc:X8}");
    Check(new[]{4,5,6,7,16,17,18,19,20,21,22,23,26,27,28,29,30,31}.All(r=>c.Regs[r]==before[r]) &&
        c.Hi==0xAB12CD34&&c.Lo==0xEF56AB78,"Caller arguments, callee registers, SP/RA, HI/LO preserved.");
    Check(Snapshot(c,0x801FF600,512).All(b=>b==0xEC),"Live ledger remains untouched.");
}
void Complete(RenderProbeCpu c,NativeCollectibleCardLoadPlan p,byte[]? bytes=null,uint operation=3,uint result=0)
{
    byte[] block=new byte[0x2000];Prefix().CopyTo(block,0);
    (bytes??DetachedCollectibleSaveCodec.Serialize(DetachedCollectibleSave.CreateNew(manifest).WithCollected(p.OrdinarySlot,Id(257)))).CopyTo(block,0x1400);
    c.Copy(p.ReadBufferAddress,block);
    // Model the publisher's final state, not execution of the async service.
    c.Put(Busy,0);c.Put(Latch,1);c.Put(0x80075B94,operation);c.Put(0x80075B98,result);
}
void UnchangedCandidate(RenderProbeCpu c,NativeCollectibleCardLoadPlan p) => Check(Snapshot(c,p.ShadowAddress,512).All(b=>b==0xA5),"Refusal preserves complete existing candidate.");
void Verify(NativeCollectibleCardLoadPlan p)
{
    var c=Initial(p);byte[] initialMenu=Snapshot(c,Menu,0x2000);
    Run(c,p.BeginPc,1);
    Check(c.Get(Record)==16&&c.Get(Record+4)==0x8007AA3C&&c.Get(Record+8)==p.ReadBufferAddress&&c.Get(Record+12)==0&&c.Get(Record+16)==0x2000,"Exact whole-block read ABI.");
    Check(c.Calls.Count(pc=>pc==NativeCollectibleCardLoadPlan.ReadPc)==1,"Exactly one accepted read, not one per frame.");
    Check(Snapshot(c,p.PrefixAddress,0x1400).SequenceEqual(Prefix()),"Complete selected native prefix snapshotted before async read.");
    for(int i=0;i<3;i++) { Run(c,p.PumpPc,1);UnchangedCandidate(c,p); }
    Check(c.Get(p.StateAddress+8)==3&&c.Get(p.StateAddress+12)==0&&c.Get(Latch)==0,"Pending native result0 is not treated as completion.");
    Complete(c,p);Run(c,p.PumpPc,2);
    var state=DetachedCollectibleSave.CreateNew(manifest).WithCollected(p.OrdinarySlot,Id(257));
    byte[] expected=DetachedCollectibleLedgerImage.Hydrate(NativeCollectibleSelectedSlotResidence.Describe(manifest,p.OrdinarySlot),
        manifest,DetachedCollectibleSaveCodec.Serialize(state),1,0xABCD,1).CopyBytes();
    Check(Snapshot(c,p.ShadowAddress,512).SequenceEqual(expected),"Successful callback result + exact prefix + actual native SHA yields all512 expected bytes.");
    Check(c.Get(Latch)==0&&c.Get(p.StateAddress+4)==1,"Completion consumed once; local selection epoch captured.");
    Check(Snapshot(c,Menu,0x2000).SequenceEqual(initialMenu),"Original menu buffer unchanged.");
    bool Inside(uint a,uint start,int length) => (ulong)a>=start && (ulong)a<start+(ulong)length;
    Check(c.Writes.All(a=>Inside(a,p.ReadBufferAddress,0x2000)||Inside(a,p.PrefixAddress,0x1400)||Inside(a,p.ShadowAddress,512)||
        Inside(a,p.StateAddress,0x80)||Inside(a,p.Sha256.DigestAddress,32)||Inside(a,p.Sha256.WorkspaceAddress,288)||
        Inside(a,Stack-NativeCollectibleCardLoadPlan.StackGuardBytes,NativeCollectibleCardLoadPlan.StackGuardBytes)||
        Inside(a,Record,0x1C)||Inside(a,Busy,4)||Inside(a,Latch,4)),"Every successful-path write has a declared owner or explicit test-service boundary.");
    int calls=c.Calls.Count;Run(c,p.PumpPc,2);Check(c.Calls.Count==calls,"Terminal pump is idempotent, never reconsumes stale event.");
    c.Run(p.GatePc,0x8007CC08);Check(c.Get(Record+24)==1,"Resolved gate calls original shutdown and continues after its source JAL.");

    c=Initial(p);Run(c,p.BeginPc,1);c.Run(p.GatePc,0x8007CC48);
    Check(c.Get(Record+24)==0&&c.Get(p.StateAddress)==1,"Pending gate takes native epilogue without shutdown or phase3.");
    c=Initial(p);c.Regs[29]=Menu+0x1000;byte[] protectedPrefix=Snapshot(c,Menu,0x1400);
    Run(c,p.BeginPc,3);Check(c.Writes.Count==0&&c.Calls.Count==0&&Snapshot(c,Menu,0x1400).SequenceEqual(protectedPrefix),"Menu-alias stack refuses before frame or native poll writes.");
    c=Initial(p);Run(c,p.BeginPc,1);c.Regs[29]=Menu+0x1000;int priorWrites=c.Writes.Count,priorCalls=c.Calls.Count;
    Run(c,p.PumpPc,4);Check(c.Writes.Count==priorWrites&&c.Calls.Count==priorCalls,"Pump menu-alias stack cannot mutate frame or consume pending callback.");
    foreach(uint badStack in new uint[]{p.ReadBufferAddress+0x1FF0,Menu+0x1000,0x801FF700})
    {
        c=Initial(p);Run(c,p.BeginPc,1);c.Regs[29]=badStack;priorWrites=c.Writes.Count;
        c.Run(p.GatePc,0x8007CC48);
        Check(c.Get(Record+24)==0&&c.Get(p.StateAddress)==1&&c.Writes.Count==priorWrites,"Gate stack refusal must yield, never call shutdown or write a rejected stack.");
    }
    foreach(uint error in new uint[]{1,2,3,5,0x8008})
    { c=Initial(p);Run(c,p.BeginPc,1);Complete(c,p,result:error);Run(c,p.PumpPc,3);UnchangedCandidate(c,p); }
    c=Initial(p);Run(c,p.BeginPc,1);Complete(c,p,operation:4);Run(c,p.PumpPc,3);UnchangedCandidate(c,p);
    c=Initial(p);Run(c,p.BeginPc,1);c.Put(Busy,0);c.Put(p.StateAddress+8,3);c.Put(p.StateAddress+12,0);
    c.Put(0x80075B94,3);c.Put(0x80075B98,0);Run(c,p.PumpPc,3);UnchangedCandidate(c,p);
    c=Initial(p);Run(c,p.BeginPc,1);Complete(c,p);c.Put(Busy,4);Run(c,p.PumpPc,1);UnchangedCandidate(c,p);
    Complete(c,p);Run(c,p.PumpPc,3);UnchangedCandidate(c,p);
    c=Initial(p);Run(c,p.BeginPc,1);Complete(c,p);c.Copy(Menu+0x4000,Prefix());
    c.Put(0x80078DC4,Menu+0x4000);c.Put(0x800785E8,Menu+0x6000);Run(c,p.PumpPc,3);UnchangedCandidate(c,p);
    c=Initial(p);c.Put(Record+20,0);Run(c,p.BeginPc,3);UnchangedCandidate(c,p);
    c=Initial(p);Complete(c,p);c.Put(Latch,0);Run(c,p.BeginPc,1);
    // A claimed success without a fresh transfer must not reuse a prior valid
    // read buffer. Publication here is a hostile boundary fixture, not BIOS proof.
    c.Copy(p.ReadBufferAddress,Prefix());
    c.Put(Busy,0);c.Put(Latch,1);c.Put(0x80075B94,3);c.Put(0x80075B98,0);
    Run(c,p.PumpPc,3);UnchangedCandidate(c,p);
    c=Initial(p);Run(c,p.BeginPc,1);Run(c,p.BeginPc,1);Check(c.Calls.Count(pc=>pc==NativeCollectibleCardLoadPlan.ReadPc)==1,"Duplicate entry cannot queue another read.");
    Complete(c,p);Run(c,p.PumpPc,3);UnchangedCandidate(c,p);
    foreach(var (address,value) in new (uint,uint)[]{(0x80078D14,0),(0x80078D18,0),(0x80078D1C,3),(0x80078D20,0xABCE),(0x80078D78,3),(0x80078D7C,0),
        (0x80078D94,0),(0x80078D8C,3),(0x80078DA0,0),(0x80078DC4,Menu+4),(0x800785E8,Menu+0x2004)})
    { c=Initial(p);Run(c,p.BeginPc,1);Complete(c,p);c.Put(address,value);Run(c,p.PumpPc,address is 0x80078DC4 or 0x800785E8 ? 4u : 3u);UnchangedCandidate(c,p); }
    foreach(int at in new[]{0,0x7E,0x200,0x78C,0x800,0xD8C,0xE00,0x138C,0x13FF})
    { c=Initial(p);Run(c,p.BeginPc,1);Complete(c,p);c.Put(p.ReadBufferAddress+(uint)at,c.Get(p.ReadBufferAddress+(uint)at,1)^1,1);Run(c,p.PumpPc,3);UnchangedCandidate(c,p); }
    foreach(int at in new[]{0,0x200,0x800,0xE00,0x13FF})
    { c=Initial(p);Run(c,p.BeginPc,1);Complete(c,p);c.Put(Menu+(uint)at,c.Get(Menu+(uint)at,1)^1,1);Run(c,p.PumpPc,3);UnchangedCandidate(c,p); }
    var envelope=DetachedCollectibleSaveCodec.Serialize(state);
    foreach(int at in new[]{0,0x10,95,96,envelope.Length-1})
    { var bad=envelope.ToArray();bad[at]^=1;c=Initial(p);Run(c,p.BeginPc,1);Complete(c,p,bad);Run(c,p.PumpPc,3);UnchangedCandidate(c,p); }
    foreach(var (address,value) in new (uint,uint)[]{(0x80078D14,0),(0x80078D18,17),(0x80078D1C,3),(0x80078D20,0x10000),(0x80078D78,1),(0x80078D7C,4),
        (0x80078D94,0),(0x80078DA0,uint.MaxValue),(0x80078DC4,Menu+1),(0x80078DC4,0x80080000),(0x800785E8,0xFFFFFFFF),(p.StateAddress+4,uint.MaxValue)})
    { c=Initial(p);c.Put(address,value);Run(c,p.BeginPc,3);Check(!c.Calls.Contains(NativeCollectibleCardLoadPlan.ReadPc),"Invalid selection refuses before read.");UnchangedCandidate(c,p); }
    foreach(uint stack in new uint[]{0,0x80200008,p.ShadowAddress+128,p.ReadBufferAddress+0x1FF0,p.PrefixAddress+0x100,0x801FF700})
    { c=Initial(p);c.Regs[29]=stack;Run(c,p.BeginPc,3);Check(c.Writes.Count==0&&c.Reads.Count==0,"Bad entire nested stack rejects before memory access."); }
    Check(p.RevertExact(p.CopyOverlay()).SequenceEqual(p.CopyOriginalOverlay()),"Complete original overlay inverse.");
    Check(p.RevertExecutableExact(p.CopyExecutable()).SequenceEqual(p.CopyOriginalExecutable()),"Complete original executable inverse.");
    byte[] exe=p.CopyExecutable(), original=p.CopyOriginalExecutable();
    int[] offsets=[0x3030,0x1EAFC,0x23E24];
    foreach(int at in offsets)
    {
        uint hi=BinaryPrimitives.ReadUInt32LittleEndian(exe.AsSpan(at)),lo=BinaryPrimitives.ReadUInt32LittleEndian(exe.AsSpan(at+4));
        Check(unchecked((hi<<16)+(uint)(int)(short)lo)==p.Layout.CopyAnchor,"All three native card copy anchors move beyond complete appended allocation.");
    }
    Check(Enumerable.Range(0,exe.Length).Where(i=>!offsets.Any(at=>i>=at&&i<at+8)).All(i=>exe[i]==original[i]),"Every other EXE byte, including exit anchors, retained.");
    byte[] changed=p.CopyOverlay();changed[^1]^=1;Refuse(()=>p.RevertExact(changed));
    changed=p.CopyExecutable();changed[0x3030]^=1;Refuse(()=>p.RevertExecutableExact(changed));
    Check(!p.CardWrites&&!p.NativeResidenceIssued&&!p.RuntimeLayoutProven&&!p.ReadyIssued&&!p.PublicationApproved,"Source/fixture plans do not grant persistent residence or publication.");
}
for(int slot=0;slot<3;slot++) Verify(NativeCollectibleCardLoadPlan.CreateFixture(manifest,slot));
foreach(int count in new[]{1,7,8,9,128,1408})
{
    var m=DetachedCollectibleManifest.CreateNew(Id(9999),Enumerable.Range(1,count).Select(Id));
    var p=NativeCollectibleCardLoadPlan.CreateFixture(m,0);
    Check(p.Loader.EnvelopeByteLength==DetachedCollectibleSaveCodec.Serialize(DetachedCollectibleSave.CreateNew(m)).Length&&p.CopyOverlay().Length==0xA800,"Actual emitters fit complete owned layout across native capacity boundaries.");
}
foreach(int count in new[]{0,1409,4096})
{ var m=DetachedCollectibleManifest.CreateNew(Id(9999),Enumerable.Range(1,count).Select(Id));Refuse(()=>NativeCollectibleCardLoadPlan.CreateFixture(m,0)); }
foreach(int slot in new[]{-1,3}) Refuse(()=>NativeCollectibleCardLoadPlan.CreateFixture(manifest,slot));
if(args.Length==1)
{
    using var source=NativeRegionalSourceSession.Open(args[0],NativeDiscRevision.UsaScus94228);
    var captured=NativeCollectibleCardLoadPlan.Capture(source,manifest,1);
    Check(captured.SourceCertified,"Original source ancestry captured without card/runtime authority.");
    Check(source.ReadExecutableRange(0x80067628-0x8000F800,0xF0).SequenceEqual(poll),"Executed poll fixture exactly matches original complete native function.");
    Verify(captured);
    byte[] exe=captured.CopyExecutable();
    foreach(var (start,end) in new (uint,uint)[]{(0x80012828,0x80012844),(0x8002E2FC,0x8002E314),(0x80033624,0x8003363C)})
    {
        var c=new RenderProbeCpu();c.Copy(0x8000F800,exe);c.Run(start,end);
        Check(c.Get(0x800785D8)==captured.Layout.CopyAnchor&&c.Get(0x800785DC)==captured.Layout.CopyAnchor,"Exact native anchor initialization updates both globals on boot and both reload paths.");
    }
    foreach(bool relocated in new[]{false,true})
    {
        var c=new RenderProbeCpu();c.Copy(0x8000F800,relocated?exe:captured.CopyOriginalExecutable());
        c.Copy(NativeCollectibleCardLoadPlan.OverlayBase,captured.CopyOverlay());
        c.Run(0x80012828,0x80012844);c.Regs[5]=0;c.Regs[29]=Stack;
        // The full native clear is 92,160 iterations (>200k instructions).
        // Keep the shared CPU budget unchanged: stop only at retired-instruction
        // boundaries, resume its real SW/branch/delay words, never replace writes
        // or registers with a host memset/result. No interrupt claim is made.
        c.Run(0x800129F0,0x8001691C);int iterations=0;
        while(true)
        {
            c.Run(0x8001691C,0x80016920);bool last=c.Regs[4]==c.Regs[6];
            c.Run(0x80016920,last?0x80012A0Cu:0x8001691Cu);
            iterations++;if(last)break;if(iterations>0x5A000/4)throw new Exception("Native clear did not terminate in its exact count.");
        }
        Check(iterations==0x5A000/4,"Complete native clear executed every word, without enlarging the interpreter budget or substituting a host clear.");
        Check(Snapshot(c,captured.BeginPc,16).SequenceEqual(captured.CopyOverlay().AsSpan((int)(captured.BeginPc-0x8007AA38),16).ToArray())==relocated,
            "Original boot clear destroys appended code; relocated native clear preserves it.");
        Check(c.Writes.Where(a=>a>=0x8007AA38).All(a=>a>=(relocated?captured.Layout.CopyAnchor:0x8007DDE8)),"Native clear writes start exactly beyond relocated owner, or at original conflicting anchor.");
    }
    // Execute the exact request's register/frame/admission setup, stopping at
    // its first filename-helper call. That helper uses LWL/LWR not supported by
    // this bounded CPU: do not substitute success or change the shared interpreter.
    // No filename formatting, callback enqueue, IRQ, BIOS or card is executed.
    var request=Initial(captured);request.Copy(0x8000F800,exe);
    request.Run(captured.BeginPc,0x80067D74);
    Check(request.Regs[19]==16&&request.Regs[18]==0x8007AA3C&&request.Regs[20]==0&&
        request.Regs[22]==0x2000&&request.Regs[21]==captured.ReadBufferAddress,
        "Exact native request setup retains generated whole-block arguments and passes busy/alignment admission before first helper.");
    Check(request.Regs[4]==16&&request.Regs[5]==0x80075B70,"Exact first filename-helper arguments, not an executed service.");
    Check(request.Get(captured.StateAddress)!=1,"Pending state not published before native API accepts.");
    source.VerifyUnchanged();
}
Console.WriteLine($"NATIVE_COLLECTIBLE_CARD_LOAD_OK checks={checks} bytes={plan.CopyOverlay().Length} source={args.Length==1} cardWrites=0 ready=false");
