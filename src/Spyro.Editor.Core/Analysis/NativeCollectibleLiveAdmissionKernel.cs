using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.Core.Analysis;

/// <summary>
/// Fixed slot-zero successor to the unchanged R01 reader. Only a Begin-armed,
/// consumed native read and revalidated prefix/envelope can reach the live copy.
/// All live accesses require the A00 reserve; guard[0,10) is never written and
/// phase is published last. Owned preflight refusal clears only the phase byte
/// before checking the stack, even when incoming SP aliases that byte; it makes
/// no frame or native call. A refused Begin is not cancellation: an already
/// accepted pending read retains its arm and must pass all ancestry checks.
/// This is emitted detached code, not an IRQ/DMA,
/// all-transition residence, collector, card-write or publication certificate.
/// </summary>
internal sealed class NativeCollectibleLiveAdmissionKernel
{
    internal const uint LiveAddress = 0x801FF600, PhaseAddress = LiveAddress+0x16;
    internal const int FrameBytes = 0x30, StackGuardBytes = 0x240, NativeCallerFrameBytes = 0x68;
    internal const int ArmedOffset = 0, EpochOffset = 4, OutcomeOffset = 8, FailureOffset = 12;
    private const uint Reserve = 0x800755A4, Menu = 0x80078DC4, ArenaEnd = 0x800785E8;
    private readonly byte[] overlay;
    private readonly NativeCollectibleCardLoadPlan reader;
    private NativeCollectibleLiveAdmissionKernel(NativeCollectibleCardLoadPlan reader)
    {
        this.reader = reader;
        if (reader.OrdinarySlot != 0 || reader.Loader.ManifestSha256 != NativeCollectibleCardLoadResearchComposition.FixedManifest().Sha256 ||
            reader.Layout.FinalByteLength != 0xA800 || reader.Layout.CopyAnchor != 0x80085238)
            throw new InvalidDataException("Live admission requires the unchanged fixed-manifest slot-zero R01 reader.");
        Layout = NativeOwnedOverlayAppendLayout.Create("card-live-admission-v1:" + Hash(reader.CopyOverlay()),reader.CopyOverlay(),
            NativeCollectibleCardLoadPlan.OverlayBase,0x10000,[new("admission-control",0x2000),new("admission-state",0x80)],"admission-padding");
        var control = Layout.Blocks.Single(b => b.Id == "admission-control").Owner;
        var state = Layout.Blocks.Single(b => b.Id == "admission-state").Owner;
        StateAddress = checked(NativeCollectibleCardLoadPlan.OverlayBase+(uint)state.Offset);
        byte[] code = Emit(checked(NativeCollectibleCardLoadPlan.OverlayBase+(uint)control.Offset));
        if (code.Length > control.ByteLength) throw new InvalidDataException("Admission code exceeds its entire owned capacity.");
        CodeByteLength = code.Length;
        byte[] capacity = new byte[checked((int)control.ByteLength)]; code.CopyTo(capacity,0);
        var entries = new[] { ("card-entry",EntryPc),("begin",BeginPc),("gate",GatePc),("validate",ValidatePc),("publish-ready",PublishPc) }
            .Select(e => new NativeOwnedOverlayEntry(e.Item1,"admission-control",new(Layout.Domain,e.Item2-NativeCollectibleCardLoadPlan.OverlayBase,4))).ToArray();
        overlay = Layout.Materialize([new("admission-control",control,capacity),new("admission-state",state,new byte[0x80])],
            entries,entries.Select(e => e.Owner));
        Put(overlay,0x174,Jump(EntryPc));Put(overlay,0x178,0);
        Put(overlay,0x2060,Call(BeginPc));Put(overlay,0x21C8,Jump(GatePc));
    }
    internal NativeOwnedOverlayAppendLayout Layout { get; }
    internal uint EntryPc { get; private set; }
    internal uint BeginPc { get; private set; }
    internal uint GatePc { get; private set; }
    internal uint ValidatePc { get; private set; }
    internal uint PublishPc { get; private set; }
    internal uint AfterLoaderPc { get; private set; }
    internal uint StateAddress { get; }
    internal int CodeByteLength { get; }
    internal byte[] CopyOverlay() => overlay.ToArray();
    internal static NativeCollectibleLiveAdmissionKernel Compile(NativeCollectibleCardLoadPlan reader)
    { ArgumentNullException.ThrowIfNull(reader); return new(reader); }

    private byte[] Emit(uint pc)
    {
        var e = new Emitter(pc);
        const int at=1,v0=2,a0=4,a1=5,a2=6,t0=8,t1=9,t2=10,t3=11,t4=12,sp=29,ra=31;
        void Read(int r,uint address) { e.Const(t3,address);e.Load(r,t3,0); }
        void Set(uint address,uint value) { e.Const(t0,value);e.Const(t3,address);e.I(0x2B,t3,t0,0); }
        void Expect(uint address,uint value,string fail)
        { Read(t0,address);e.Const(t1,value);e.B(5,t0,t1,fail); }
        void LiveOwner(string fail)
        { Expect(Reserve,0xA00,fail);e.Const(t0,PhaseAddress);e.I(0x28,t0,0,0); }
        void Stack(string fail)
        {
            e.I(12,sp,t0,7);e.B(5,t0,0,fail);
            e.Const(t0,0x80000000+StackGuardBytes);e.R(0x2B,sp,t0,t1);e.B(5,t1,0,fail);
            // The original ABAC prologue owns [SP,SP+68), including saved RA
            // and S0..S4. Exclude it as well as our nested lower stack usage.
            e.Const(t0,0x80200000-NativeCallerFrameBytes);e.R(0x2B,t0,sp,t1);e.B(5,t1,0,fail);
            foreach (var (start,end) in new[] { (0x80000000u,Layout.CopyAnchor),(LiveAddress,LiveAddress+0x200) })
            {
                string next=e.Unique();e.I(9,sp,t0,-StackGuardBytes);e.Const(t1,end);e.R(0x2B,t0,t1,t2);e.B(4,t2,0,next);
                e.Const(t1,start);e.I(9,sp,t0,NativeCallerFrameBytes);e.R(0x2B,t1,t0,t2);e.B(5,t2,0,fail);e.Label(next);
            }
            Read(t0,Menu);e.I(12,t0,t1,3);e.B(5,t1,0,fail);
            e.Const(t1,Layout.CopyAnchor+0x3E000);e.R(0x2B,t0,t1,t2);e.B(5,t2,0,fail);
            e.Const(t1,0x801FD600);e.R(0x2B,t1,t0,t2);e.B(5,t2,0,fail);
            e.I(9,t0,t1,0x2000);Read(t2,ArenaEnd);e.B(5,t1,t2,fail);
            string apart=e.Unique();e.I(9,sp,t2,-StackGuardBytes);e.R(0x2B,t2,t1,t2);e.B(4,t2,0,apart);
            e.I(9,sp,t3,NativeCallerFrameBytes);e.R(0x2B,t0,t3,t2);e.B(5,t2,0,fail);e.Label(apart);
        }
        int[] saved = [a0,a1,a2,7,ra];
        void Save()
        { e.I(9,sp,sp,-FrameBytes);for(int i=0;i<saved.Length;i++)e.I(0x2B,sp,saved[i],0x10+i*4); }
        void Restore()
        { for(int i=0;i<saved.Length;i++)e.Load(saved[i],sp,0x10+i*4);e.I(9,sp,sp,FrameBytes); }

        // ABAC is called only while outer!=3 by the pinned resident dispatch.
        // This bridge has no stack/call and replays the replaced LUI/LW before
        // the original prologue. Outer0/1 and non-selection state disarm stale
        // control, but outer2/sub5 pending polls must survive multiple updates.
        // Forcing a different tuple while Busy is artificial out-of-path input:
        // this refusal does not cancel/recover the native service operation.
        EntryPc=e.Pc;
        e.Const(at,Reserve);e.Load(v0,at,0);e.I(9,v0,v0,-0xA00);e.B(5,v0,0,"entry-replay");
        e.Const(at,0x80078D78);e.Load(v0,at,0);e.I(9,v0,v0,-3);e.B(4,v0,0,"entry-replay");
        e.Const(at,PhaseAddress);e.I(0x28,at,0,0);
        e.Const(at,0x80078D78);e.Load(v0,at,0);e.I(9,v0,v0,-2);e.B(5,v0,0,"entry-disarm");
        e.Const(at,0x80078D7C);e.Load(v0,at,0);e.I(9,v0,v0,-5);e.B(4,v0,0,"entry-replay");
        e.Label("entry-disarm");e.Const(at,StateAddress);e.I(0x2B,at,0,ArmedOffset);e.I(0x2B,at,0,OutcomeOffset);e.I(0x2B,at,0,FailureOffset);
        e.Const(at,reader.StateAddress);e.I(0x2B,at,0,NativeCollectibleCardLoadPlan.StateOffset);
        e.I(0x2B,at,0,NativeCollectibleCardLoadPlan.FailureOffset);
        e.Label("entry-replay");e.I(15,0,v0,0x8008);e.Load(v0,v0,unchecked((short)0x8D78));e.Word(Jump(0x8007ABB4));e.Word(0);

        BeginPc=e.Pc;LiveOwner("begin-refused");Stack("begin-refused");Save();
        Set(StateAddress+ArmedOffset,0);Set(StateAddress+OutcomeOffset,0);Set(StateAddress+FailureOffset,0);
        e.Call(reader.BeginPc);
        e.Const(t0,NativeCollectibleCardLoadPlan.Waiting);e.B(5,v0,t0,"begin-return");
        Expect(reader.StateAddress,NativeCollectibleCardLoadPlan.Waiting,"begin-return");
        Expect(reader.StateAddress+NativeCollectibleCardLoadPlan.FailureOffset,0,"begin-return");
        Read(t0,reader.StateAddress+NativeCollectibleCardLoadPlan.EpochOffset);e.B(4,t0,0,"begin-return");
        Read(t1,reader.StateAddress+NativeCollectibleCardLoadPlan.ContextOffset+12);e.B(5,t0,t1,"begin-return");
        e.Const(t3,StateAddress);e.I(0x2B,t3,t0,EpochOffset);Set(StateAddress+OutcomeOffset,1);Set(StateAddress+ArmedOffset,1);
        e.Label("begin-return");Restore();e.R(8,ra,0,0);e.Word(0);
        e.Label("begin-refused");e.Const(v0,NativeCollectibleCardLoadPlan.Rejected);e.R(8,ra,0,0);e.Word(0);

        GatePc=e.Pc;LiveOwner("gate-yield-unframed");Stack("gate-yield-unframed");Save();e.Call(reader.PumpPc);
        e.Const(t0,NativeCollectibleCardLoadPlan.Prepared);e.B(4,v0,t0,"gate-validate");
        e.B(4,v0,0,"gate-refuse");e.Const(t0,NativeCollectibleCardLoadPlan.Rejected);e.B(4,v0,t0,"gate-refuse");
        e.B(4,0,0,"gate-yield");
        e.Label("gate-validate");e.CallLabel("validate");e.B(4,v0,0,"gate-refuse");
        e.Const(a0,reader.InputAddress);e.Const(a1,(uint)reader.Loader.EnvelopeByteLength);
        e.Const(a2,reader.StateAddress+NativeCollectibleCardLoadPlan.ContextOffset);e.Call(reader.Loader.EntryPc);
        AfterLoaderPc=e.Pc;e.B(5,v0,0,"gate-refuse");
        // Recheck current tuple, drained service, epoch and full prefixes after
        // the nested hash/loader. This is bounded synchronous evidence, not an
        // interrupt exclusion or a physical-card replacement identity proof.
        e.CallLabel("validate");e.B(4,v0,0,"gate-refuse");
        Set(StateAddress+ArmedOffset,0);Set(StateAddress+OutcomeOffset,2);Set(StateAddress+FailureOffset,0);
        e.Const(t0,reader.ShadowAddress+0x10);e.Const(t1,LiveAddress+0x10);e.Const(t2,LiveAddress+0x200);
        e.Const(t4,0xFF00FFFF);e.Label("copy-live");e.Load(t3,t0,0);
        e.Const(v0,LiveAddress+0x14);e.B(5,t1,v0,"copy-word");e.R(0x24,t3,t4,t3);
        e.Label("copy-word");e.I(0x2B,t1,t3,0);e.I(9,t0,t0,4);e.I(9,t1,t1,4);e.B(5,t1,t2,"copy-live");
        e.Const(t0,PhaseAddress);e.Const(t1,1);PublishPc=e.Pc;e.I(0x28,t0,t1,0);
        e.B(4,0,0,"gate-shutdown");
        e.Label("gate-refuse");Set(StateAddress+ArmedOffset,0);Set(StateAddress+OutcomeOffset,3);Set(StateAddress+FailureOffset,1);
        e.Label("gate-shutdown");Restore();e.Call(NativeCollectibleCardLoadPlan.ShutdownPc);e.Word(Jump(0x8007CC08));e.Word(0);
        e.Label("gate-yield");Restore();
        e.Label("gate-yield-unframed");e.Word(Jump(0x8007CC48));e.Word(0);

        ValidatePc=e.Pc;e.Label("validate");
        Expect(Reserve,0xA00,"invalid");Expect(StateAddress+ArmedOffset,1,"invalid");
        Expect(reader.StateAddress,NativeCollectibleCardLoadPlan.Prepared,"invalid");
        Expect(reader.StateAddress+NativeCollectibleCardLoadPlan.OperationOffset,3,"invalid");
        Expect(reader.StateAddress+NativeCollectibleCardLoadPlan.ResultOffset,0,"invalid");
        Expect(reader.StateAddress+NativeCollectibleCardLoadPlan.FailureOffset,0,"invalid");
        Expect(0x80075B50,0,"invalid");Expect(0x80075B58,0,"invalid");
        Read(t0,StateAddress+EpochOffset);e.B(4,t0,0,"invalid");
        Read(t1,reader.StateAddress+NativeCollectibleCardLoadPlan.EpochOffset);e.B(5,t0,t1,"invalid");
        Read(t1,reader.StateAddress+NativeCollectibleCardLoadPlan.ContextOffset+12);e.B(5,t0,t1,"invalid");
        foreach(var (address,value) in new (uint,uint)[] { (0x80078D14,1),(0x80078D78,2),(0x80078D7C,5),
            (0x80078D94,1),(0x80078D1C,0),(0x80078D8C,0),(reader.StateAddress+NativeCollectibleCardLoadPlan.ContextOffset,0) }) Expect(address,value,"invalid");
        Read(t0,0x80078D18);e.I(12,t0,t1,15);e.B(5,t1,0,"invalid");e.I(11,t0,t1,17);e.B(4,t1,0,"invalid");
        e.R(2,0,t0,t0,4);Read(t1,0x80078DA0);e.B(5,t0,t1,"invalid");
        Read(t1,reader.StateAddress+NativeCollectibleCardLoadPlan.ContextOffset+4);e.B(5,t0,t1,"invalid");
        Read(t0,0x80078D20);e.Const(t1,0x10000);e.R(0x2B,t0,t1,t2);e.B(4,t2,0,"invalid");
        Read(t1,reader.StateAddress+NativeCollectibleCardLoadPlan.ContextOffset+8);e.B(5,t0,t1,"invalid");
        Read(t2,Menu);e.I(12,t2,t1,3);e.B(5,t1,0,"invalid");
        e.Const(t1,Layout.CopyAnchor+0x3E000);e.R(0x2B,t2,t1,t3);e.B(5,t3,0,"invalid");
        e.Const(t1,0x801FD600);e.R(0x2B,t1,t2,t3);e.B(5,t3,0,"invalid");
        Read(t1,reader.StateAddress+NativeCollectibleCardLoadPlan.MenuPointerOffset);e.B(5,t2,t1,"invalid");
        e.Load(t1,t2,0x7E,0x25);e.B(5,t0,t1,"invalid");
        e.I(9,t2,t0,0x2000);Read(t1,ArenaEnd);e.B(5,t0,t1,"invalid");
        e.Const(t0,reader.PrefixAddress);e.Const(t1,reader.ReadBufferAddress);e.Const(t4,reader.PrefixAddress+0x1400);
        e.Label("prefixes");e.Load(t3,t0,0);e.Load(v0,t1,0);e.B(5,t3,v0,"invalid");
        e.Load(v0,t2,0);e.B(5,t3,v0,"invalid");e.I(9,t0,t0,4);e.I(9,t1,t1,4);e.I(9,t2,t2,4);e.B(5,t0,t4,"prefixes");
        e.Const(v0,1);e.R(8,ra,0,0);e.Word(0);
        e.Label("invalid");e.Const(v0,0);e.R(8,ra,0,0);e.Word(0);
        return e.Finish();
    }
    private static uint Call(uint pc) => 0x0C000000|((pc>>2)&0x3FFFFFF);
    private static uint Jump(uint pc) => 0x08000000|((pc>>2)&0x3FFFFFF);
    private static void Put(Span<byte> bytes,int offset,uint word) => BinaryPrimitives.WriteUInt32LittleEndian(bytes[offset..],word);
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private sealed class Emitter(uint start)
    {
        private readonly List<uint> words=[];
        private readonly Dictionary<string,int> labels=[];
        private readonly List<(int At,string Label)> branches=[],calls=[];
        private int unique;
        internal uint Pc => checked(start+(uint)words.Count*4);
        internal string Unique() => "unique-"+unique++;
        internal void Word(uint word) => words.Add(word);
        internal void I(int op,int rs,int rt,int immediate) => Word((uint)(op<<26|rs<<21|rt<<16)|(ushort)immediate);
        internal void R(int function,int rs,int rt,int rd,int shift=0) => Word((uint)(rs<<21|rt<<16|rd<<11|shift<<6|function));
        internal void Const(int r,uint value) { I(15,0,r,(int)(value>>16));I(13,r,r,(int)value); }
        internal void Load(int r,int address,int offset,int op=0x23) { I(op,address,r,offset);Word(0); }
        internal void B(int op,int rs,int rt,string label) { branches.Add((words.Count,label));I(op,rs,rt,0);Word(0); }
        internal void Label(string label) => labels.Add(label,words.Count);
        internal void Call(uint target) { Word(NativeCollectibleLiveAdmissionKernel.Call(target));Word(0); }
        internal void CallLabel(string label) { calls.Add((words.Count,label));Word(0);Word(0); }
        internal byte[] Finish()
        {
            foreach(var (at,label) in branches)
            { int distance=labels[label]-at-1;if(distance<short.MinValue||distance>short.MaxValue)throw new InvalidDataException("Admission branch exceeds MIPS-I range.");words[at]|=(ushort)(short)distance; }
            foreach(var (at,label) in calls)words[at]=NativeCollectibleLiveAdmissionKernel.Call(checked(start+(uint)labels[label]*4));
            byte[] bytes=new byte[words.Count*4];for(int i=0;i<words.Count;i++)Put(bytes,i*4,words[i]);return bytes;
        }
    }
}
