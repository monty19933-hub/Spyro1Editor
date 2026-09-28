using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Persistence;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Source-bound, detached two-seam card-menu reader. Begin preserves the native
/// selected-tail poll, queues a separate full-block read, and snapshots the
/// native prefix. Pump nonblockingly consumes only a successful read completion,
/// rechecks selection and the entire prefix, then prepares the existing shadow
/// ABI. Gate postpones shutdown while the request remains pending. Never writes
/// a card or the live ledger. Appended RAM/copy-anchor lifetime, disc relocation,
/// legacy initialization, durable save transactions and Ready are NOT issued.
/// </summary>
internal sealed class NativeCollectibleCardLoadPlan
{
    internal const uint OverlayBase = 0x8007AA38, ReadPc = 0x80066E28,
        PollPc = 0x80067628, ShutdownPc = 0x8006631C;
    internal const uint Idle = 0, Waiting = 1, Prepared = 2, Rejected = 3, PreflightRefused = 4;
    internal const int StateOffset = 0, EpochOffset = 4, OperationOffset = 8,
        ResultOffset = 12, FailureOffset = 16, MenuPointerOffset = 20, ContextOffset = 32;
    internal const int FrameBytes = 0x50, StackGuardBytes = 0x200;
    private const uint Bound = 0x80078D14, Controller = 0x80078D18, Slot = 0x80078D1C,
        Marker = 0x80078D20, Outer = 0x80078D78, Substate = 0x80078D7C,
        CursorSlot = 0x80078D8C, Intent = 0x80078D94, CursorController = 0x80078DA0,
        MenuPointer = 0x80078DC4, ArenaEnd = 0x800785E8, Busy = 0x80075B50;
    private const string CardHash = "8ee66fe644407bf15f93af1615045fbaca17fe3daa9f3b67f38bee2277a94c7c";
    private static readonly int[] AnchorOffsets = [0x3030,0x1EAFC,0x23E24];
    private readonly byte[] original, overlay, originalExecutable, executable;

    private NativeCollectibleCardLoadPlan(byte[] original, byte[] originalExe, DetachedCollectibleManifest manifest, int slot, bool source)
    {
        this.original = original.ToArray(); SourceCertified = source; OrdinarySlot = slot;
        if (original.Length != 0x3800 || Word(original, 0x2060) != Call(PollPc) ||
            Word(original, 0x2064) != 0 || Word(original, 0x21C8) != Call(ShutdownPc) || Word(original, 0x21CC) != 0)
            throw new InvalidDataException("Card reader requires both complete original JAL/NOP seams.");
        var sizes = new NativeOwnedOverlayBlockSize[] {
            new("sha",0x800), new("loader",0x1000), new("control",0x2000), new("shadow-alignment",8),
            new("shadow",0x200), new("state",0x80), new("digest",0x20), new("hash-workspace",0x120),
            new("read-buffer",0x2000), new("native-prefix-snapshot",0x1400) };
        Layout = NativeOwnedOverlayAppendLayout.Create("card-load:" + Hash(original) + ":" + manifest.Sha256 + ":" + slot,
            original, OverlayBase, 0x10000, sizes, "sector-padding");
        if(originalExe.Length!=0x66000) throw new InvalidDataException("Card copy anchors require the complete executable owner.");
        originalExecutable=originalExe.ToArray(); executable=originalExe.ToArray();
        foreach(int at in AnchorOffsets)
        {
            if(Word(originalExe,at)!=0x3C028008 || Word(originalExe,at+4)!=0x2442DDE8)
                throw new InvalidDataException("One of the three original card copy anchors changed.");
            Put(executable,at,0x3C020000 | ((Layout.CopyAnchor+0x8000)>>16));
            Put(executable,at+4,0x24420000 | (Layout.CopyAnchor&0xFFFF));
        }
        NativeByteExtent Owner(string id) => Layout.Blocks.Single(b => b.Id == id).Owner;
        uint Pc(string id) => checked(OverlayBase + (uint)Owner(id).Offset);
        StateAddress = Pc("state"); ReadBufferAddress = Pc("read-buffer"); PrefixAddress = Pc("native-prefix-snapshot");
        ShadowAddress = Pc("shadow"); InputAddress = ReadBufferAddress + 0x1400;
        Sha256 = NativeSha256Kernel.Compile(Owner("sha"), Pc("sha"), InputAddress, 1024, Pc("digest"), Pc("hash-workspace"));
        Loader = NativeCollectibleEnvelopeLoadKernel.Compile(manifest, slot, Owner("loader"), Pc("loader"),
            ShadowAddress, StateAddress + ContextOffset, Sha256);
        if (FrameBytes + Loader.MaximumStackByteLength > StackGuardBytes)
            throw new InvalidDataException("Card reader exceeds its checked nested generated-code frame.");
        var control = EmitControl(Pc("control"));
        if (control.Length > Owner("control").ByteLength)
            throw new InvalidDataException("Card reader exceeds its complete control-code capacity.");
        var blocks = new List<NativeOwnedOverlayBlock>();
        foreach (var size in sizes)
        {
            byte[] bytes = new byte[size.ByteLength];
            byte[] payload = size.Id switch { "sha" => Sha256.CopyBytes(), "loader" => Loader.CopyBytes(), "control" => control, _ => [] };
            payload.CopyTo(bytes,0); blocks.Add(new(size.Id,Owner(size.Id),bytes));
        }
        var entries = new[] { ("begin","control",BeginPc), ("pump","control",PumpPc), ("gate","control",GatePc),
            ("sha","sha",Sha256.EntryPc), ("loader","loader",Loader.EntryPc) }
            .Select(e => new NativeOwnedOverlayEntry(e.Item1,e.Item2,new(Layout.Domain,e.Item3-OverlayBase,4))).ToArray();
        overlay = Layout.Materialize(blocks,entries,entries.Select(e => e.Owner));
        Put(overlay,0x2060,Call(BeginPc)); Put(overlay,0x21C8,Jump(GatePc));
        for (int i=0;i<original.Length;i++)
            if (!(i>=0x2060 && i<0x2064 || i>=0x21C8 && i<0x21CC) && overlay[i]!=original[i])
                throw new InvalidDataException("Card reader changed another original overlay owner.");
    }

    internal NativeOwnedOverlayAppendLayout Layout { get; }
    internal NativeSha256Kernel Sha256 { get; }
    internal NativeCollectibleEnvelopeLoadKernel Loader { get; }
    internal int OrdinarySlot { get; }
    internal uint BeginPc { get; private set; }
    internal uint PumpPc { get; private set; }
    internal uint GatePc { get; private set; }
    internal uint StateAddress { get; }
    internal uint ReadBufferAddress { get; }
    internal uint PrefixAddress { get; }
    internal uint ShadowAddress { get; }
    internal uint InputAddress { get; }
    internal bool SourceCertified { get; }
    internal bool NativeResidenceIssued => false;
    internal bool RuntimeLayoutProven => false;
    internal bool ReadyIssued => false;
    internal bool CardWrites => false;
    internal bool PublicationApproved => false;
    internal byte[] CopyOverlay() => overlay.ToArray();
    internal byte[] CopyOriginalOverlay() => original.ToArray();
    internal byte[] CopyExecutable() => executable.ToArray();
    internal byte[] CopyOriginalExecutable() => originalExecutable.ToArray();
    internal byte[] RevertExecutableExact(ReadOnlySpan<byte> bytes) => bytes.SequenceEqual(executable) ? originalExecutable.ToArray() :
        throw new InvalidDataException("Card-reader executable inverse requires its complete exact afterimage.");
    internal byte[] RevertExact(ReadOnlySpan<byte> bytes) => bytes.SequenceEqual(overlay) ? original.ToArray() :
        throw new InvalidDataException("Card-reader inverse requires its complete exact detached afterimage.");

    internal static NativeCollectibleCardLoadPlan Capture(NativeRegionalSourceSession source,
        DetachedCollectibleManifest manifest, int slot)
    {
        ArgumentNullException.ThrowIfNull(source);
        try
        {
            if (source.Revision != NativeDiscRevision.UsaScus94228 || source.Entries[2].Offset != 0x5B800 || source.Entries[2].ByteLength != 0x3800)
                throw new InvalidDataException("Card reader requires the original USA entry 2.");
            byte[] card = source.ReadWadEntry(2), exe = source.ReadExecutableRange(0,source.ExecutableByteLength);
            NativeJapaneseEggMemoryReport.RequireSourceExecutable(exe);
            if (Hash(card)!=CardHash || Hash(exe.AsSpan((int)(PollPc-0x8000F800),0xF0))!=
                "19553c82e1ac2ff3a3089e3c8b101ade63a08c9620216785f79df4a2d2da3e21")
                throw new InvalidDataException("Exact card overlay or poll service changed.");
            return new(card,exe,manifest,slot,true);
        }
        finally { source.VerifyUnchanged(); }
    }

    // Explicit fixture ancestry can never be promoted by source-looking words.
    internal static NativeCollectibleCardLoadPlan CreateFixture(DetachedCollectibleManifest manifest, int slot)
    {
        byte[] card = new byte[0x3800];
        "BASCUS-94228SPYRO\0"u8.CopyTo(card.AsSpan(4));
        Put(card,0x2060,Call(PollPc)); Put(card,0x21C8,Call(ShutdownPc));
        byte[] exe=new byte[0x66000];
        foreach(int at in AnchorOffsets) { Put(exe,at,0x3C028008); Put(exe,at+4,0x2442DDE8); }
        return new(card,exe,manifest,slot,false);
    }

    private byte[] EmitControl(uint code)
    {
        var e = new Emitter(code);
        const int v0=2,a0=4,a1=5,a2=6,a3=7,t0=8,t1=9,t2=10,t3=11,s0=16,s1=17,sp=29,ra=31;
        void Read(int r,uint address) { e.Const(t3,address); e.Load(r,t3,0); }
        void Write(int r,uint address) { e.Const(t3,address); e.I(0x2B,t3,r,0); }
        void Set(uint address,uint value) { e.Const(t0,value); Write(t0,address); }
        void Equal(int r,uint value,string fail) { e.Const(t1,value); e.B(5,r,t1,fail); }
        void Expected(uint address,uint value,string fail) { Read(t0,address); Equal(t0,value,fail); }
        void CheckStack(string fail)
        {
            e.I(12,sp,t0,7); e.B(5,t0,0,fail);
            e.Const(t0,0x80000000+StackGuardBytes); e.R(0x2B,sp,t0,t1); e.B(5,t1,0,fail);
            e.Const(t0,0x80200000); e.R(0x2B,t0,sp,t1); e.B(5,t1,0,fail);
            // Exclude the complete original+appended overlay, native service/global
            // area, and the protected live reservation from the whole checked frame.
            foreach (var (start,end) in new[] { (0x80000000u,Layout.CopyAnchor), (0x801FF600u,0x801FF800u) })
            {
                string next=e.Unique(); e.I(9,sp,t0,-StackGuardBytes); e.Const(t1,end);
                e.R(0x2B,t0,t1,t2); e.B(4,t2,0,next); e.Const(t1,start); e.R(0x2B,t1,sp,t2); e.B(5,t2,0,fail); e.Label(next);
            }
            // Validate the dynamic native buffer using only global reads, before
            // Save or any native service can write to a potentially aliased SP.
            Read(t0,MenuPointer); e.I(12,t0,t1,3); e.B(5,t1,0,fail);
            e.Const(t1,Layout.CopyAnchor+0x3E000); e.R(0x2B,t0,t1,t2); e.B(5,t2,0,fail);
            e.Const(t1,0x801FD600); e.R(0x2B,t1,t0,t2); e.B(5,t2,0,fail);
            e.I(9,t0,t1,0x2000); Read(t2,ArenaEnd); e.B(5,t1,t2,fail);
            string separate=e.Unique(); e.I(9,sp,t2,-StackGuardBytes); e.R(0x2B,t2,t1,t2); e.B(4,t2,0,separate);
            e.R(0x2B,t0,sp,t2); e.B(5,t2,0,fail); e.Label(separate);
        }
        int[] saved = [a0,a1,a2,a3,s0,s1,ra];
        void Save()
        {
            e.I(9,sp,sp,-FrameBytes);
            for(int i=0;i<saved.Length;i++) e.I(0x2B,sp,saved[i],0x20+i*4);
            e.R(16,0,0,t0); e.I(0x2B,sp,t0,0x3C); e.R(18,0,0,t0); e.I(0x2B,sp,t0,0x40);
        }
        void Restore()
        {
            e.Load(t0,sp,0x3C); e.R(17,t0,0,0); e.Load(t0,sp,0x40); e.R(19,t0,0,0);
            for(int i=0;i<saved.Length;i++) e.Load(saved[i],sp,0x20+i*4);
            e.I(9,sp,sp,FrameBytes); e.R(8,ra,0,0); e.Word(0);
        }
        void Selection(string fail,bool captured)
        {
            Expected(Bound,1,fail); Expected(Outer,2,fail); Expected(Substate,5,fail);
            Expected(Intent,1,fail); Expected(Slot,(uint)OrdinarySlot,fail); Expected(CursorSlot,(uint)OrdinarySlot,fail);
            Read(t0,Controller); e.I(12,t0,t1,15); e.B(5,t1,0,fail); e.I(0x0B,t0,t1,17); e.B(4,t1,0,fail);
            e.R(2,0,t0,s0,4); Read(t0,CursorController); e.B(5,t0,s0,fail);
            Read(t0,Marker); e.Const(t1,0x10000); e.R(0x2B,t0,t1,t2); e.B(4,t2,0,fail);
            Read(s1,MenuPointer); e.I(12,s1,t1,3); e.B(5,t1,0,fail);
            e.Const(t1,Layout.CopyAnchor+0x3E000); e.R(0x2B,s1,t1,t2); e.B(5,t2,0,fail);
            e.Const(t1,0x801FD600); e.R(0x2B,t1,s1,t2); e.B(5,t2,0,fail);
            Read(t1,ArenaEnd); e.I(9,s1,t2,0x2000); e.B(5,t1,t2,fail);
            // Complete current stack and native menu buffer must not alias either.
            string separate=e.Unique(); e.I(9,sp,t1,FrameBytes-StackGuardBytes); e.R(0x2B,t1,t2,t0); e.B(4,t0,0,separate);
            e.I(9,sp,t1,FrameBytes); e.R(0x2B,s1,t1,t0); e.B(5,t0,0,fail); e.Label(separate);
            // Header marker is read only after the complete menu extent was checked.
            e.Load(t0,s1,0x7E,0x25); Read(t1,Marker); e.B(5,t0,t1,fail);
            if(captured)
            {
                Read(t0,StateAddress+MenuPointerOffset); e.B(5,t0,s1,fail);
                Read(t0,StateAddress+ContextOffset+4); e.B(5,t0,s0,fail);
                Read(t0,StateAddress+ContextOffset+8); Read(t1,Marker); e.B(5,t0,t1,fail);
                Expected(StateAddress+ContextOffset,(uint)OrdinarySlot,fail);
                Read(t0,StateAddress+ContextOffset+12); Read(t1,StateAddress+EpochOffset); e.B(5,t0,t1,fail);
            }
        }
        void Reject(uint reason,string end) { Set(StateAddress+FailureOffset,reason); Set(StateAddress,Rejected); e.B(4,0,0,end); }

        BeginPc=e.Pc; CheckStack("begin-stack"); Save();
        Expected(StateAddress,Waiting,"begin-normal");
        // A duplicate entry cannot consume/replace this reader's outstanding event.
        Set(StateAddress+FailureOffset,10); e.B(4,0,0,"begin-finish");
        e.Label("begin-normal"); e.Call(PollPc); e.I(0x2B,sp,v0,0x44);
        Set(StateAddress,Rejected); Set(StateAddress+FailureOffset,0);
        Selection("begin-invalid",false); Expected(Busy,0,"begin-invalid"); Expected(0x80075B58,0,"begin-invalid");
        Read(t0,StateAddress+EpochOffset); e.Const(t1,uint.MaxValue); e.B(4,t0,t1,"begin-invalid"); e.I(9,t0,t0,1);
        Write(t0,StateAddress+EpochOffset); Write(t0,StateAddress+ContextOffset+12);
        Write(s1,StateAddress+MenuPointerOffset); Write(s0,StateAddress+ContextOffset+4);
        Read(t0,Marker); Write(t0,StateAddress+ContextOffset+8); Set(StateAddress+ContextOffset,(uint)OrdinarySlot);
        e.Const(t0,PrefixAddress); e.R(0x21,s1,0,t1); e.Const(t2,PrefixAddress+0x1400);
        e.Label("snapshot"); e.Load(a0,t1,0); e.I(0x2B,t0,a0,0); e.I(9,t0,t0,4); e.I(9,t1,t1,4); e.B(5,t0,t2,"snapshot");
        // Do not let a failed/short transfer reuse a preceding valid envelope.
        // Native result0 is still required; no byte-count service proof is implied.
        e.Const(t0,ReadBufferAddress); e.Const(t1,ReadBufferAddress+0x2000);
        e.Label("clear-read"); e.I(0x2B,t0,0,0); e.I(9,t0,t0,4); e.B(5,t0,t1,"clear-read");
        Read(a0,Controller); e.Const(a1,OverlayBase+4); e.Const(a2,ReadBufferAddress); e.Const(a3,0);
        e.Const(t0,0x2000); e.I(0x2B,sp,t0,0x10); e.Call(ReadPc); Equal(v0,1,"begin-request-refused");
        Set(StateAddress,Waiting); e.B(4,0,0,"begin-finish");
        e.Label("begin-invalid"); Reject(11,"begin-finish"); e.Label("begin-request-refused"); Reject(12,"begin-finish");
        e.Label("begin-finish"); Read(v0,StateAddress); Restore();
        e.Label("begin-stack"); e.Const(v0,Rejected); e.R(8,ra,0,0); e.Word(0);

        PumpPc=e.Pc; CheckStack("pump-stack"); Save();
        Expected(StateAddress,Waiting,"pump-finish");
        e.Const(a0,1); e.Const(a1,StateAddress+OperationOffset); e.Const(a2,StateAddress+ResultOffset); e.Call(PollPc);
        e.B(4,v0,0,"pump-finish"); Equal(v0,1,"pump-no-event");
        Expected(Busy,0,"pump-unexpected-busy"); Expected(StateAddress+OperationOffset,3,"pump-io-error");
        Expected(StateAddress+ResultOffset,0,"pump-io-error"); Expected(StateAddress+FailureOffset,0,"pump-stale");
        Selection("pump-stale",true);
        e.Const(t0,PrefixAddress); e.Const(t1,ReadBufferAddress); e.Const(t2,PrefixAddress+0x1400);
        e.Label("prefix-compare"); e.Load(a0,t0,0); e.Load(a1,t1,0); e.B(5,a0,a1,"pump-prefix");
        e.Load(a1,s1,0); e.B(5,a0,a1,"pump-prefix");
        e.I(9,s1,s1,4); e.I(9,t0,t0,4); e.I(9,t1,t1,4); e.B(5,t0,t2,"prefix-compare");
        e.Const(a0,InputAddress); e.Const(a1,(uint)Loader.EnvelopeByteLength); e.Const(a2,StateAddress+ContextOffset); e.Call(Loader.EntryPc);
        e.B(5,v0,0,"pump-envelope"); Set(StateAddress,Prepared); e.B(4,0,0,"pump-finish");
        e.Label("pump-unexpected-busy"); Set(StateAddress+FailureOffset,13); e.B(4,0,0,"pump-finish");
        e.Label("pump-no-event"); Reject(14,"pump-finish"); e.Label("pump-io-error"); Reject(15,"pump-finish");
        e.Label("pump-stale"); Reject(16,"pump-finish"); e.Label("pump-prefix"); Reject(17,"pump-finish");
        e.Label("pump-envelope"); Reject(18,"pump-finish");
        e.Label("pump-finish"); Read(v0,StateAddress); Restore();
        e.Label("pump-stack"); e.Const(v0,PreflightRefused); e.R(8,ra,0,0); e.Word(0);

        // This bridge is entered by J at the original shutdown JAL. The native
        // update already owns/saves RA in its 0x68-byte frame, and its subsequent
        // calls/epilogue restore it. Never return to the replaced instruction.
        GatePc=e.Pc; e.Call(PumpPc); e.B(4,v0,0,"gate-resolved");
        e.Const(t0,Prepared); e.B(4,v0,t0,"gate-resolved"); e.Const(t0,Rejected); e.B(5,v0,t0,"gate-pending");
        e.Label("gate-resolved");
        e.Call(ShutdownPc); e.Word(Jump(0x8007CC08)); e.Word(0);
        e.Label("gate-pending"); e.Word(Jump(0x8007CC48)); e.Word(0);
        return e.Finish();
    }

    private static uint Word(ReadOnlySpan<byte> b,int at) => BinaryPrimitives.ReadUInt32LittleEndian(b[at..]);
    private static void Put(Span<byte> b,int at,uint value) => BinaryPrimitives.WriteUInt32LittleEndian(b[at..],value);
    private static uint Call(uint pc) => 0x0C000000 | ((pc>>2)&0x3FFFFFF);
    private static uint Jump(uint pc) => 0x08000000 | ((pc>>2)&0x3FFFFFF);
    private static string Hash(ReadOnlySpan<byte> b) => Convert.ToHexStringLower(SHA256.HashData(b));
    private sealed class Emitter(uint start)
    {
        private readonly List<uint> words=[];
        private readonly Dictionary<string,int> labels=[];
        private readonly List<(int At,string Name)> branches=[];
        private int unique;
        internal uint Pc => checked(start+(uint)words.Count*4);
        internal string Unique() => "local-"+unique++;
        internal void Word(uint value) => words.Add(value);
        internal void R(int fn,int rs,int rt,int rd,int shift=0) => Word((uint)(rs<<21|rt<<16|rd<<11|shift<<6|fn));
        internal void I(int op,int rs,int rt,int value) => Word((uint)(op<<26|rs<<21|rt<<16)|(ushort)value);
        internal void Const(int r,uint value) { I(15,0,r,(int)(value>>16)); I(13,r,r,(int)value); }
        internal void Load(int r,int pointer,int offset,int op=0x23) { I(op,pointer,r,offset); Word(0); }
        internal void Call(uint pc) { Word(NativeCollectibleCardLoadPlan.Call(pc)); Word(0); }
        internal void Label(string name) => labels.Add(name,words.Count);
        internal void B(int op,int rs,int rt,string name) { branches.Add((words.Count,name)); I(op,rs,rt,0); Word(0); }
        internal byte[] Finish()
        {
            foreach(var b in branches)
            { int n=labels[b.Name]-b.At-1; if(n<short.MinValue||n>short.MaxValue) throw new InvalidDataException("Card control branch exceeds MIPS range."); words[b.At]|=(ushort)(short)n; }
            byte[] bytes=new byte[words.Count*4]; for(int i=0;i<words.Count;i++) Put(bytes,i*4,words[i]); return bytes;
        }
    }
}
