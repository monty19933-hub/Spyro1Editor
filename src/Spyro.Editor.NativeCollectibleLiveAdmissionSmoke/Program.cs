using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

int checks = 0;
void Check(bool ok,string why) { checks++; if (!ok) throw new Exception(why); }
void Refuse(Action action,string why)
{ try { action(); } catch (Exception e) when (e is InvalidDataException or ArgumentException) { checks++; return; } throw new Exception("Expected refusal: " + why); }
if (args.Length > 1) throw new ArgumentException("Only an optional original USA BIN is accepted; no publication/card/output mode.");
const uint Stop = 0x80010000, Stack = 0x801FFCA0, Menu = 0x80160000, Record = 0x80100000,
    Live = 0x801FF600, Phase = Live+0x16, Busy = 0x80075B50, Latch = 0x80075B58;
var manifest = DetachedCollectibleManifest.CreateNew(Guid.Parse("12345678-1234-5678-9012-123456789abc"),
    [Guid.Parse("11111111-1111-1111-1111-111111111111"),Guid.Parse("22222222-2222-2222-2222-222222222222")]);
byte[] envelope = DetachedCollectibleSaveCodec.Serialize(DetachedCollectibleSave.CreateNew(manifest)
    .WithCollected(0,Guid.Parse("11111111-1111-1111-1111-111111111111")));
byte[] poll = Convert.FromHexString("0780023c505b42240000428c0000000006004014000000000780023c585b428c0000000030004010ffff02240780083c505b08250000078d0780033c545b638c0c008014000000000780023c585b428c0000000013004014080003250000628c00000000fdff401000000000b39d0108000000000780023c585b428c0000000008004014000000000200c010000000000000c3ac1400a01021100000c49d01080000a7ac0600c010000000000780023c985b42240000428c000000000000c2ac0600a010000000000780023c945b42240000428c000000000000a2ac0780013c585b20ac010002240800e00300000000");
byte[] Words(params uint[] words)
{ byte[] b = new byte[words.Length*4]; for (int i=0;i<words.Length;i++) BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(i*4),words[i]); return b; }
byte[] Snapshot(RenderProbeCpu c,uint at,int n) => Enumerable.Range(0,n).Select(i => (byte)c.Get(at+(uint)i,1)).ToArray();
byte[] Prefix()
{ byte[] b = new byte[0x1400]; new Random(928).NextBytes(b); BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(0x7E),0xABCD); return b; }
byte[] Poison()
{ byte[] b = Enumerable.Repeat((byte)0xEC,0x200).ToArray(); b[0x16] = 1; return b; }
RenderProbeCpu Initial(NativeCollectibleCardAdmissionPlan p)
{
    var c = new RenderProbeCpu { StrictSignedArithmetic = true };
    c.Copy(NativeCollectibleCardLoadPlan.OverlayBase,p.CopyOverlay()); c.Copy(NativeCollectibleCardLoadPlan.PollPc,poll);
    // Explicit acceptance/shutdown boundaries; actual poll, generated SHA and
    // loader/controller/admission code execute. No BIOS/card/IRQ is simulated.
    c.Copy(NativeCollectibleCardLoadPlan.ReadPc,Words(0x3C088010,0xAD040000,0xAD050004,0xAD060008,0xAD07000C,
        0x8FA90010,0,0xAD090010,0x8D020014,0,0x10400005,0,0x3C088007,0x24090003,0xAD095B50,0xAD005B58,0x03E00008,0));
    c.Copy(NativeCollectibleCardLoadPlan.ShutdownPc,Words(0x3C088010,0x24090001,0xAD090018,0x03E00008,0));
    c.Put(Record+20,1); c.Copy(Menu,Prefix()); c.Copy(Live,Poison()); c.Put(0x800755A4,0xA00);
    foreach (var (a,v) in new (uint,uint)[] { (0x80078D14,1),(0x80078D18,0),(0x80078D1C,0),(0x80078D20,0xABCD),
        (0x80078D78,2),(0x80078D7C,5),(0x80078D8C,0),(0x80078D94,1),(0x80078DA0,0),(0x80078DC4,Menu),(0x800785E8,Menu+0x2000) }) c.Put(a,v);
    for (int r=1;r<32;r++) c.Regs[r]=0xA0000000u+(uint)r;
    c.Regs[4]=0; c.Regs[5]=0x80078DB8; c.Regs[6]=0x80078DBC; c.Regs[29]=Stack; c.Regs[31]=Stop;
    c.Hi=0xAB12CD34;c.Lo=0xEF56AB78; return c;
}
void Complete(RenderProbeCpu c,NativeCollectibleCardAdmissionPlan p,byte[]? bytes = null,uint operation = 3,uint result = 0)
{
    byte[] block = new byte[0x2000]; Prefix().CopyTo(block,0); (bytes??envelope).CopyTo(block,0x1400);
    c.Copy(p.Reader.ReadBufferAddress,block); c.Put(Busy,0);c.Put(Latch,1);c.Put(0x80075B94,operation);c.Put(0x80075B98,result);
}
void Begin(RenderProbeCpu c,NativeCollectibleCardAdmissionPlan p,uint result = 1)
{
    c.Regs[31]=Stop; var regs=c.Regs.ToArray(); c.Run(p.Kernel.BeginPc,Stop);
    Check(c.Regs[2]==result,"Begin preserves R01 result semantics.");
    Check(new[]{4,5,6,7,16,17,18,19,20,21,22,23,26,27,28,29,30,31}.All(r=>regs[r]==c.Regs[r]) &&
        c.Hi==0xAB12CD34&&c.Lo==0xEF56AB78,"Wrapper preserves arguments/callee registers/SP/RA/HI/LO.");
    Check(c.Get(Phase,1)==0,"A00 selection begins by invalidating live Ready.");
}
void Unadmitted(RenderProbeCpu c)
{ byte[] expected=Poison();expected[0x16]=0;Check(Snapshot(c,Live,0x200).SequenceEqual(expected),"Refusal preserves guard/progress; only phase invalidated."); }
void Verify(NativeCollectibleCardAdmissionPlan p)
{
    var k=p.Kernel;var r=p.Reader;var c=Initial(p);Begin(c,p);
    Check(c.Get(k.StateAddress)==1&&c.Get(k.StateAddress+4)==1,"Only accepted Begin arms captured epoch.");
    for(int i=0;i<3;i++){c.Run(k.EntryPc,0x8007ABB4);c.Run(k.GatePc,0x8007CC48);Check(c.Get(k.StateAddress)==1&&c.Get(Record+24)==0,"Repeated real card updates retain pending ancestry without shutdown.");}
    Complete(c,p);c.Run(k.GatePc,k.PublishPc);
    Check(c.Get(Phase,1)==0,"Ready remains zero through every payload store.");
    byte[] expected=DetachedCollectibleLedgerImage.Hydrate(NativeCollectibleSelectedSlotResidence.Describe(manifest,0),manifest,envelope,0,0xABCD,1).CopyBytes();
    Poison().AsSpan(0,0x10).CopyTo(expected);expected[0x16]=0;
    Check(Snapshot(c,Live,0x200).SequenceEqual(expected),"Complete admission image except Ready matches exact validated envelope and preserved sentinel guard.");
    c.Run(k.PublishPc,NativeCollectibleCardLoadPlan.ShutdownPc);expected[0x16]=1;
    Check(Snapshot(c,Live,0x200).SequenceEqual(expected)&&c.Get(k.StateAddress)==0,"Ready published last and one-shot ancestry consumed before original shutdown.");
    Check(c.Writes.Where(a=>a>=Live&&a<Live+0x200).Last()==Phase,"Last write to live reservation is Ready byte.");
    c.Run(NativeCollectibleCardLoadPlan.ShutdownPc,0x8007CC08);Check(c.Get(Record+24)==1,"Ordinary source shutdown and continuation preserved.");
    Check(!c.Writes.Any(a=>a>=Live&&a<Live+0x10),"No guard byte ever written.");
    c.Run(k.GatePc,0x8007CC08);Check(c.Get(Phase,1)==0,"Repeated Prepared with consumed arm cannot restore Ready.");

    foreach(var (address,value) in new (uint,uint)[]{(k.StateAddress,0),(k.StateAddress+4,2),(r.StateAddress+4,2),
        (r.StateAddress+32+12,2),(0x80078D14,0),(0x80078D18,16),(0x80078D1C,1),(0x80078D20,0xABCE),
        (0x80078D94,0),(0x80078D8C,1),(0x80078DA0,1)})
    {c=Initial(p);Begin(c,p);Complete(c,p);c.Put(address,value);c.Run(k.GatePc,0x8007CC08);Unadmitted(c);}
    foreach(uint result in new uint[]{1,2,3,5,0x8008})
    {c=Initial(p);Begin(c,p);Complete(c,p,result:result);c.Run(k.GatePc,0x8007CC08);Unadmitted(c);}
    c=Initial(p);Begin(c,p);Complete(c,p,operation:4);c.Run(k.GatePc,0x8007CC08);Unadmitted(c);
    foreach(int at in new[]{0,0x7E,0x200,0x800,0xE00,0x13FF})
    {c=Initial(p);Begin(c,p);Complete(c,p);c.Put(r.PrefixAddress+(uint)at,c.Get(r.PrefixAddress+(uint)at,1)^1,1);c.Run(k.GatePc,0x8007CC08);Unadmitted(c);}
    foreach(int at in new[]{0,0x10,0x60,envelope.Length-1})
    {byte[] bad=envelope.ToArray();bad[at]^=1;c=Initial(p);Begin(c,p);Complete(c,p,bad);c.Run(k.GatePc,0x8007CC08);Unadmitted(c);}
    foreach(byte fill in new byte[]{0,0xFF})
    {c=Initial(p);Begin(c,p);Complete(c,p,Enumerable.Repeat(fill,envelope.Length).ToArray());c.Run(k.GatePc,0x8007CC08);Unadmitted(c);}
    c=Initial(p);c.Put(0x80078D94,0);Begin(c,p,3);Check(c.Get(k.StateAddress)==0,"New-game cannot arm or manufacture empty Ready.");Unadmitted(c);
    c=Initial(p);Begin(c,p);Begin(c,p);Complete(c,p);c.Run(k.GatePc,0x8007CC08);Unadmitted(c);
    foreach(uint badStack in new uint[]{0,0x80200000,0x80200008,k.StateAddress+0x40,r.ShadowAddress+0x100,
        NativeCollectibleCardLoadPlan.OverlayBase+0xA900,Menu+0x1000,Live+0x100,Phase,Phase&~7u,Live-0x20})
    {
        c=Initial(p);c.Regs[29]=badStack;Begin(c,p,3);Unadmitted(c);
        Check(c.Writes.SequenceEqual(new[]{Phase})&&c.Calls.Count==0,"Unsafe Begin writes exactly owned phase byte, including SP aliases; no frame or native calls.");
        c=Initial(p);Begin(c,p);Complete(c,p);c.Regs[29]=badStack;int writes=c.Writes.Count,calls=c.Calls.Count;
        c.Run(k.GatePc,0x8007CC48);Unadmitted(c);
        Check(c.Writes.Skip(writes).SequenceEqual(new[]{Phase})&&c.Calls.Count==calls,"Unsafe Gate writes exactly owned phase byte, including SP aliases; no frame or native calls.");
    }
    // Preflight refusal is not cancellation. The accepted pending read remains
    // armed and can complete only with its unchanged tuple/epoch/full prefix.
    foreach(bool badReserve in new[]{false,true})
    {
        c=Initial(p);Begin(c,p);uint epoch=c.Get(k.StateAddress+4);int writes=c.Writes.Count,calls=c.Calls.Count;
        if(badReserve)c.Put(0x800755A4,0x800);else c.Regs[29]=Live-0x20;
        c.Regs[31]=Stop;c.Run(k.BeginPc,Stop);
        Check(c.Regs[2]==3&&c.Get(k.StateAddress)==1&&c.Get(k.StateAddress+4)==epoch&&c.Get(Busy)==3,
            "Refused second Begin retains the real pending read ancestry; it is not cancellation.");
        Check(c.Calls.Count==calls&&(badReserve?c.Writes.Count==writes:c.Writes.Skip(writes).SequenceEqual(new[]{Phase})),
            "Refused second Begin performs no frame/native call or unauthorized control mutation.");
        c.Put(0x800755A4,0xA00);c.Regs[29]=Stack;Complete(c,p);c.Run(k.GatePc,0x8007CC08);
        Check(c.Get(Phase,1)==1&&c.Get(k.StateAddress)==0,"Unchanged pending ancestry completes once after temporary preflight refusal.");
    }
    foreach(uint reserve in new uint[]{0,0x800,0xA04})
    {
        c=Initial(p);c.Put(0x800755A4,reserve);c.Run(k.GatePc,0x8007CC48);
        Check(c.Writes.Count==0&&!c.Reads.Any(a=>a>=Live&&a<Live+0x200),"Wrong reserve never accesses unowned live RAM.");
    }
    foreach(var (outer,sub) in new (uint,uint)[]{(0,0),(1,0),(2,0),(2,4)})
    {c=Initial(p);Begin(c,p);c.Put(0x80078D78,outer);c.Put(0x80078D7C,sub);c.Run(k.EntryPc,0x8007ABB4);
     Check(c.Get(k.StateAddress)==0&&c.Get(r.StateAddress)==0&&c.Get(Busy)==3,
         "Artificial nonselection disarms stale state but does not cancel native Busy; not a safe-cancellation claim.");Unadmitted(c);}
    Check(p.RevertOverlayExact(p.CopyOverlay()).SequenceEqual(r.CopyOriginalOverlay()),"Complete original overlay inverse.");
    Check(p.RevertExecutableExact(p.CopyExecutable()).SequenceEqual(p.CopyOriginalExecutable()),"Complete original executable inverse.");
    foreach(int at in new[]{0,0x174,0x2060,0x21C8,0x3800,0xA800,p.CopyOverlay().Length-1})
    {byte[] bad=p.CopyOverlay();bad[at]^=1;Refuse(()=>p.RevertOverlayExact(bad),"mutated exact overlay afterimage");}
    Check(!p.PublicationApproved&&!p.RuntimeVerified&&!p.LifetimeProven&&!p.CardWrites&&!p.CollectorEnabled,
        "Successor emits admission code, not broad runtime/publication/collector/card authority.");
}
var fixture=NativeCollectibleCardAdmissionPlan.CreateFixture();
Check(!fixture.SourceCertified,"Fixture cannot become retail ancestry.");
Refuse(fixture.RequireSourceComposition,"fixture cannot grant source authority");
Verify(fixture);
if(args.Length==1)
{
    using var source=NativeRegionalSourceSession.Open(args[0],NativeDiscRevision.UsaScus94228);
    var captured=NativeCollectibleCardAdmissionPlan.Capture(source);Verify(captured);captured.RequireSourceComposition();
    SourceChecks.Run(captured,source,Check);source.VerifyUnchanged();
}
Console.WriteLine($"NATIVE_COLLECTIBLE_LIVE_ADMISSION_OK checks={checks} source={args.Length==1} imageWrites=0 cardWrites=0 runtimeVerified=false");
