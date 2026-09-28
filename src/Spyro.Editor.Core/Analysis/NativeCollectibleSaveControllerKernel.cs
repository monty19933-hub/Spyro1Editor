using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.Core.Analysis;

internal enum NativeCollectibleSaveControllerStage : uint
{ Idle,ReadingOldTail,WritingOrdinary,ReadingOrdinary,WritingTail,ReadingTail,Committed,Failed,Revoked,PreflightRefused }
internal enum NativeCollectibleSavePendingReceipt : uint
{ None,Intent,Accepted,Rejected,Consumed,Indeterminate }
internal enum NativeCollectibleSaveDrainResult : uint
{ NoOwnedTransfer,Pending,Consumed,Indeterminate,PreflightRefused=9 }

/// <summary>
/// DETACHED fixed-two-ID/slot0 MIPS transaction experiment. Begin takes
/// a0=OrdinaryInputAddress,a1=600(hex),a2=801FF600,a3=0. Pump/Revoke take
/// a0=StateAddress,a1=a2=a3=0. v0 is a stage; PreflightRefused changes nothing.
/// One zero-initialized state has ONE lifetime; no reset/reuse entry exists.
/// Pump makes at most one nonblocking poll and one subsequent native request.
/// Success publishes only a detached proposal, never live RAM/native success7.
/// Revoke is an unframed, fixed three-word state write, even if SP aliases state.
/// It neither cancels/drains I/O nor permits buffers/code to be reclaimed.
/// Drain takes the same state ABI but returns a separate drain result. Only
/// Idle/terminal stages may drain, with at most one nonblocking poll and no
/// request. It ignores invalidated live/session bytes and preserves terminal
/// stage/proposal. Receipt words survive failure/revocation; they are not an
/// authenticated request/session identity. Caller must serialize entries and
/// return into its retained overlay before considering any owner release.
///
/// Code, state, input/expected buffers and filename require external exclusive
/// owners. The legacy profile requires external whole-entry IRQ exclusion;
/// every profile still requires exclusion of unrelated mutation/DMA except a
/// completed service's declared writes. Caller MUST signal Revoke before session changes,
/// even when opaque epoch/tuple/ledger bytes repeat. No native hook authenticates
/// that lifetime. Retain every pending I/O buffer after failure/revocation.
/// Successful service semantics must supply the full old100 transfer: native
/// poll exposes no byte count; unknown suffix bytes cannot detect lying short
/// success. Known readbacks are complement-prefilled and compared byte-for-byte.
/// The native request50 stack bound covers accepted non-diagnostic paths only;
/// error diagnostics, BIOS, IRQ and asynchronous callbacks remain unproved.
/// Source queue index -1 is empty. The pre-request unsigned(index+1)&lt;4 guard
/// admits only -1/0/1/2, preventing the known full-queue false-accepted route.
/// This non-atomic check still needs external exclusive/IRQ ownership; it does
/// not prove enqueue/completion, cancellation or release of an outstanding owner.
/// Every request also rechecks all18 source-fixed filename bytes including NUL;
/// the separate filename owner does not grant caller-selected file authority.
/// The opt-in IRQ-windowed successor masks only finite native snapshot/request/
/// receipt sections. Bulk validation/preparation/copies are outside its owned
/// windows (an already-disabled caller stays disabled). Foreground entry/session
/// serialization and exclusive/DMA ownership remain external requirements;
/// emitting the source Enter/Exit protocol does not prove BIOS or IRQ runtime.
/// </summary>
internal sealed class NativeCollectibleSaveControllerKernel
{
    internal const uint ReadPc=0x80066E28,WritePc=0x800670E4,PollPc=0x80067628,ProposalMagic=0x53564331;
    internal const uint EnterCriticalPc=0x8005DBA4,ExitCriticalPc=0x8005DBB4;
    internal const int CodeCapacityBytes=0x2000,ArenaBytes=0x7300,StateBytes=0x30,
        FrameBytes=0x40,MaximumStackBytes=0xE0,NativeCallerBytes=0x48;
    private const uint Live=0x801FF600,Busy=0x80075B50,Latch=0x80075B58;
    internal static IReadOnlyList<NativeCollectibleSaveTailKernel.SourceSpan> QueueSourceSpans { get; }=Array.AsReadOnly(new[]{
        new NativeCollectibleSaveTailKernel.SourceSpan(0x80068F30,0x14,"b81ac71035cb2a71c2c7a316c0c11bc2c00233f882df5e6301aa4e315ffdd1e0"),
        new NativeCollectibleSaveTailKernel.SourceSpan(0x80068F44,0x80,"7b25bd5394d6eb44fbe836882eb7bb866ec7c917813ebdd6e17b7037cc9e06ce")});
    internal static IReadOnlyList<NativeCollectibleSaveTailKernel.SourceSpan> CriticalSourceSpans { get; }=Array.AsReadOnly(new[]{
        new NativeCollectibleSaveTailKernel.SourceSpan(EnterCriticalPc,0x10,"505f018057cab576fce3e80a248467f5c8ad5e14c7d2289869e453ad092ef931"),
        new NativeCollectibleSaveTailKernel.SourceSpan(ExitCriticalPc,0x10,"6e003f701ca622421a42bea9b13f252e5325ecafcb0e85216766a154e060e578")});
    // State: stage0, poll operation4/result8, observed generationC/bitmap10,
    // external revocation14, failure18, proposal marker1C; receipt20, fixed
    // request kind24 (1..5, same order as active stages), raw request return28,
    // raw poll return2C. Intent is published before JAL, results before checks.
    private readonly byte[] bytes;
    internal sealed record Region(string Role,uint Address,uint ByteLength);
    private NativeCollectibleSaveControllerKernel(uint arena,NativeRegionalSourceSession? source,bool irqWindows=false)
    {
        if(arena%16!=0||arena<0x80090000||(ulong)arena+ArenaBytes>0x801F0000)
            throw new InvalidDataException("Complete detached controller owners require aligned bounded cached RAM.");
        ArenaAddress=arena;IrqWindowsEmitted=irqWindows;
        Preparation=source is null?NativeCollectibleSaveTailKernel.CreateFixture(checked(arena+CodeCapacityBytes)):
            NativeCollectibleSaveTailKernel.Capture(source,checked(arena+CodeCapacityBytes));
        if(FrameByteLength+NativeCollectibleSaveTailKernel.MaximumStackBytes!=MaximumStackByteLength)
            throw new InvalidDataException("Generated nested stack contract changed.");
        uint cursor=arena;List<Region> regions=[];
        void Own(string role,int n){regions.Add(new(role,cursor,(uint)n));cursor=checked(cursor+(uint)n);}
        Own("controller-code",CodeCapacityBytes);Own("preparation-arena",NativeCollectibleSaveTailKernel.ArenaBytes);
        Own("ordinary-input",0x600);Own("expected-ordinary",0x600);Own("ordinary-readback",0x600);Own("tail-readback",0x100);
        Own("begin-ledger",0x200);Own("commit-proposal",0x200);Own("controller-state",StateBytes);Own("fixed-filename",0x20);Own("alignment",0x10);
        if(cursor!=arena+ArenaBytes)throw new InvalidDataException("Complete measured controller partition changed.");
        Regions=regions.AsReadOnly();bytes=Emit();
        if(bytes.Length>CodeCapacityBytes)throw new InvalidDataException($"Measured controller {bytes.Length:X} exceeds explicit2000 code owner by{bytes.Length-CodeCapacityBytes:X}; no growth authorized.");
    }
    internal uint ArenaAddress { get; }
    internal uint BeginPc { get; private set; }
    internal uint PumpPc { get; private set; }
    internal uint RevokePc { get; private set; }
    internal uint DrainPc { get; private set; }
    internal bool IrqWindowsEmitted { get; }
    internal int FrameByteLength=>IrqWindowsEmitted?0x50:FrameBytes;
    internal int MaximumStackByteLength=>IrqWindowsEmitted?0xF0:MaximumStackBytes;
    internal uint IrqEnterHelperPc { get; private set; }
    internal uint IrqLeaveHelperPc { get; private set; }
    internal uint IrqLeaveReturnPc { get; private set; }
    internal uint OrdinaryInputAddress=>ArenaAddress+0x5BA0;
    internal uint ExpectedOrdinaryAddress=>ArenaAddress+0x61A0;
    internal uint OrdinaryReadbackAddress=>ArenaAddress+0x67A0;
    internal uint TailReadbackAddress=>ArenaAddress+0x6DA0;
    internal uint BeginLedgerAddress=>ArenaAddress+0x6EA0;
    internal uint ProposalAddress=>ArenaAddress+0x70A0;
    internal uint StateAddress=>ArenaAddress+0x72A0;
    internal uint FilenameAddress=>ArenaAddress+0x72D0;
    internal NativeCollectibleSaveTailKernel Preparation { get; }
    internal IReadOnlyList<Region> Regions { get; }
    internal int CodeByteLength=>bytes.Length;
    internal bool SourceCertified=>Preparation.SourceCertified;
    internal bool NativeIoCallsEmitted=>true;
    internal bool NativeIoExecuted=>false;
    internal bool RuntimeVerified=>false;
    internal bool HooksInstalled=>false;
    internal bool NativeResidenceIssued=>false;
    internal bool NativeSessionIdentityAuthenticated=>false;
    internal bool CancellationApproved=>false;
    internal bool NativeFullStackProven=>false;
    internal bool PublicationApproved=>false;
    internal bool CardWrites=>false;
    internal bool NormalCreateBinApproved=>false;
    internal byte[] CopyBytes()=>bytes.ToArray();
    internal byte[] CopyFilename()=>"BASCUS-94228SPYRO\0"u8.ToArray();
    internal void RequireSourceEvidence()=>Preparation.RequireSourceEvidence();
    internal void RequireNativeInstallation()=>throw new InvalidDataException(
        "Detached controller: native hooks, session revocation, lease/drain, IRQ/cache/full-stack safety and real I/O remain unproved.");
    internal static NativeCollectibleSaveControllerKernel CreateFixture(uint arena)=>new(arena,null);
    internal static NativeCollectibleSaveControllerKernel CreateIrqWindowedFixture(uint arena)=>new(arena,null,true);
    internal static NativeCollectibleSaveControllerKernel Capture(NativeRegionalSourceSession source,uint arena)=>CaptureProfile(source,arena,false);
    internal static NativeCollectibleSaveControllerKernel CaptureIrqWindowed(NativeRegionalSourceSession source,uint arena)=>CaptureProfile(source,arena,true);
    private static NativeCollectibleSaveControllerKernel CaptureProfile(NativeRegionalSourceSession source,uint arena,bool irqWindows)
    {
        ArgumentNullException.ThrowIfNull(source);
        try
        {
            if(source.Revision!=NativeDiscRevision.UsaScus94228||
                !source.ReadExecutableRange(0x15B8,18).AsSpan().SequenceEqual("BASCUS-94228SPYRO\0"u8))
                throw new InvalidDataException("Exact source native filename at80010DB8 required.");
            foreach(var span in QueueSourceSpans.Concat(irqWindows?CriticalSourceSpans:[]))
                if(Convert.ToHexStringLower(SHA256.HashData(source.ReadExecutableRange((int)(span.Pc-0x8000F800),span.ByteLength)))!=span.Sha256)
                    throw new InvalidDataException("Source queue initializer/enqueue body changed.");
            return new(arena,source,irqWindows);
        }
        finally{source.VerifyUnchanged();}
    }
    private byte[] Emit()
    {
        var e=new Emitter(ArenaAddress,IrqWindowsEmitted);const int v0=2,a0=4,a1=5,a2=6,a3=7,t0=8,t1=9,t2=10,t3=11,t4=12,sp=29,ra=31;
        void Enter(){if(IrqWindowsEmitted)e.CallLabel("irq-enter");}
        void Leave(){if(IrqWindowsEmitted)e.CallLabel("irq-leave");}
        void Read(int r,uint at,int op=0x23){e.C(t4,at);e.L(op,r,t4,0);}
        void Write(int r,uint at,int op=0x2B){e.C(t4,at);e.I(op,t4,r,0);}
        void Set(uint at,uint value){e.C(t0,value);Write(t0,at);}
        void Equal(int r,uint value,string fail){e.C(t1,value);e.B(5,r,t1,fail);}
        void Expect(uint at,uint value,string fail){Read(t0,at);Equal(t0,value,fail);}
        void Unrevoked(){Expect(StateAddress+0x14,0,"revoked");}
        void Stack()
        {
            e.I(12,sp,t0,7);e.B(5,t0,0,"preflight");
            e.C(t0,0x801FF800+(uint)MaximumStackByteLength);e.R(0x2B,sp,t0,t1);e.B(5,t1,0,"preflight");
            e.C(t0,0x80200000-NativeCallerBytes);e.R(0x2B,t0,sp,t1);e.B(5,t1,0,"preflight");
        }
        int[] saved=[a0,a1,a2,a3,ra];
        void Save()
        {
            e.I(9,sp,sp,-FrameByteLength);for(int i=0;i<saved.Length;i++)e.I(0x2B,sp,saved[i],0x20+i*4);
            e.R(16,0,0,t0);e.I(0x2B,sp,t0,0x34);e.R(18,0,0,t0);e.I(0x2B,sp,t0,0x38);
            if(IrqWindowsEmitted)e.I(0x2B,sp,0,0x44);
        }
        void Copy(uint from,uint to,int length,bool complement=false)
        {
            string loop=e.Unique();e.C(t0,from);e.C(t1,to);e.C(t2,from+(uint)length);e.Label(loop);
            e.L(0x24,t3,t0,0);if(complement){e.C(t4,0xFF);e.R(0x26,t3,t4,t3);}
            e.I(0x28,t1,t3,0);e.I(9,t0,t0,1);e.I(9,t1,t1,1);e.B(5,t0,t2,loop);
        }
        void Compare(uint from,uint to,int length)
        {
            string loop=e.Unique();e.C(t0,from);e.C(t1,to);e.C(t2,from+(uint)length);e.Label(loop);
            e.L(0x24,t3,t0,0);e.L(0x24,t4,t1,0);e.B(5,t3,t4,"failure");
            e.I(9,t0,t0,1);e.I(9,t1,t1,1);e.B(5,t0,t2,loop);
        }
        void Zero(uint from,int length)
        {string loop=e.Unique();e.C(t0,from);e.C(t1,from+(uint)length);e.Label(loop);e.I(0x2B,t0,0,0);e.I(9,t0,t0,4);e.B(5,t0,t1,loop);}
        void AllZero(uint from,int length)
        {string loop=e.Unique();e.C(t0,from);e.C(t1,from+(uint)length);e.Label(loop);e.L(0x23,t2,t0,0);e.B(5,t2,0,"failure");e.I(9,t0,t0,4);e.B(5,t0,t1,loop);}
        void Validate(bool captured)=>e.CallLabel(captured?"validate-current":"validate-initial");
        void Receipt(NativeCollectibleSavePendingReceipt value)=>Set(StateAddress+0x20,(uint)value);
        void ExpectedOperation(string fail)
        {
            Read(t0,StateAddress+0x24);e.I(9,t0,t1,-1);e.I(11,t1,t2,5);e.B(4,t2,0,fail);
            e.I(12,t0,t2,1);e.C(t3,4);e.R(0x23,t3,t2,t3); // odd read3, even write4
        }
        void PendingReceipt(string fail)
        {
            Expect(StateAddress+0x20,(uint)NativeCollectibleSavePendingReceipt.Accepted,fail);
            Expect(StateAddress+0x28,1,fail);Read(t0,StateAddress+0x2C);e.I(9,t0,t1,1);
            e.I(11,t1,t2,2);e.B(4,t2,0,fail); // only sentinel FFFFFFFF or pending0
        }
        void ValidateBody(bool captured)
        {
            Unrevoked();Expect(0x800755A4,0xA00,"failure");Expect(0x80078D14,1,"failure");Expect(0x80078D1C,0,"failure");
            Read(t0,Live+0x18,0x24);e.I(11,t0,t1,2);e.B(4,t1,0,"failure");e.R(0,0,t0,t0,4);
            Read(t1,0x80078D18);e.B(5,t0,t1,"failure");
            Read(t0,Live+0x1A,0x25);Read(t1,0x80078D20);e.B(5,t0,t1,"failure");
            Read(t0,Live+0x19,0x24);Equal(t0,1,"failure");Read(t0,Live+0x8C);e.B(4,t0,0,"failure");
            Read(t0,Live+0x7C);e.B(4,t0,0,"failure");Read(t1,Live+0x80);e.R(0x2B,t0,t1,t2);e.B(5,t2,0,"failure");
            AllZero(Live+0xB0,0x150);
            e.C(a0,Live);e.C(a1,0);e.Call(Preparation.Query.EntryPc);e.I(11,v0,t0,2);e.B(4,t0,0,"failure");
            if(captured)
            {
                Compare(Live+0x18,BeginLedgerAddress+0x18,4);Compare(Live+0x50,BeginLedgerAddress+0x50,32);
                Compare(Live+0x80,BeginLedgerAddress+0x80,4);Compare(Live+0x8C,BeginLedgerAddress+0x8C,4);
                Read(t0,Live+0x7C);Read(t1,StateAddress+0xC);e.R(0x2B,t0,t1,t2);e.B(5,t2,0,"failure");
                Read(t2,Live+0x90,0x24);Read(t3,StateAddress+0x10);e.R(0x24,t2,t3,t4);e.B(5,t4,t3,"failure");
                string newer=e.Unique();e.B(5,t0,t1,newer);e.B(5,t2,t3,"failure");e.Label(newer);
                Write(t0,StateAddress+0xC);Write(t2,StateAddress+0x10);
            }
        }
        void Request(uint op,uint buffer,int offset,int length,NativeCollectibleSaveControllerStage next)
        {
            Enter();
            Unrevoked();Expect(Busy,0,"failure");Expect(Latch,0,"failure");
            // Source signed last-enqueued index has the explicit empty value -1.
            // Normalize with nontrapping ADDIU and compare unsigned: exactly
            // FFFFFFFF/0/1/2 pass; all underflow/overflow/full values refuse.
            Read(t0,0x800751B0);e.I(9,t0,t0,1);e.I(11,t0,t1,4);e.B(4,t1,0,"failure");
            e.CallLabel("validate-filename");
            Expect(StateAddress+0x20,(uint)(next==NativeCollectibleSaveControllerStage.ReadingOldTail?
                NativeCollectibleSavePendingReceipt.None:NativeCollectibleSavePendingReceipt.Consumed),"failure");
            Set(StateAddress+0x24,(uint)next);Set(StateAddress+0x28,uint.MaxValue);Set(StateAddress+0x2C,uint.MaxValue);
            Receipt(NativeCollectibleSavePendingReceipt.Intent);
            Read(a0,0x80078D18);e.C(a1,FilenameAddress);e.C(a2,buffer);e.C(a3,(uint)offset);
            e.C(t0,(uint)length);e.I(0x2B,sp,t0,0x10);e.Call(op==3?ReadPc:WritePc);
            e.CallLabel("record-request");
            // A synchronous external revocation cannot be overwritten on return.
            Unrevoked();Equal(v0,1,"failure");Expect(Busy,op,"failure");Expect(Latch,0,"failure");
            if(IrqWindowsEmitted){Set(StateAddress,(uint)next);Leave();Validate(true);}
            else{Validate(true);Set(StateAddress,(uint)next);}
            e.B(4,0,0,"finish");
        }

        BeginPc=e.Pc;Equal(a0,OrdinaryInputAddress,"preflight");Equal(a1,0x600,"preflight");Equal(a2,Live,"preflight");Equal(a3,0,"preflight");Stack();
        Read(t0,StateAddress+0x14);e.B(5,t0,0,"unframed-revoked");Read(v0,StateAddress);e.B(5,v0,0,"return");
        Save();AllZero(StateAddress,StateBytes);Enter();Expect(Busy,0,"failure");Expect(Latch,0,"failure");Leave();Validate(false);
        Copy(Live,BeginLedgerAddress,512);Copy(OrdinaryInputAddress,ExpectedOrdinaryAddress,0x600);
        Read(t0,Live+0x7C);Write(t0,StateAddress+0xC);Read(t0,Live+0x90,0x24);Write(t0,StateAddress+0x10);
        Zero(Preparation.OldTailAddress,0x100);Request(3,Preparation.OldTailAddress,0x1400,0x100,NativeCollectibleSaveControllerStage.ReadingOldTail);

        PumpPc=e.Pc;Equal(a0,StateAddress,"preflight");Equal(a1,0,"preflight");Equal(a2,0,"preflight");Equal(a3,0,"preflight");Stack();
        Read(t0,StateAddress+0x14);e.B(5,t0,0,"unframed-revoked");Read(v0,StateAddress);e.B(4,v0,0,"return");
        e.I(11,v0,t0,6);e.B(4,t0,0,"return");Save();Validate(true);PendingReceipt("failure");
        Read(t0,StateAddress);Read(t1,StateAddress+0x24);e.B(5,t0,t1,"failure");
        Enter();
        Set(StateAddress+4,uint.MaxValue);Set(StateAddress+8,uint.MaxValue);
        e.C(a0,1);e.C(a1,StateAddress+4);e.C(a2,StateAddress+8);e.Call(PollPc);e.CallLabel("record-poll");Unrevoked();
        e.B(4,v0,0,"pending");Equal(v0,1,"failure");Expect(StateAddress+0x20,(uint)NativeCollectibleSavePendingReceipt.Consumed,"failure");
        Expect(Busy,0,"failure");Expect(Latch,0,"failure");Expect(StateAddress+8,0,"failure");
        Read(t0,StateAddress);e.C(t1,2);e.B(4,t0,t1,"write-complete");e.C(t1,4);e.B(4,t0,t1,"write-complete");
        Expect(StateAddress+4,3,"failure");e.B(4,0,0,"completed-operation");
        e.Label("write-complete");Expect(StateAddress+4,4,"failure");e.Label("completed-operation");Leave();Validate(true);
        for(uint stage=1;stage<=5;stage++){Read(t0,StateAddress);e.C(t1,stage);e.B(4,t0,t1,"complete-"+stage);}
        e.B(4,0,0,"failure");

        e.Label("complete-1");
        // This unchanged preparer reads actual live, not the earlier Begin copy.
        // Refuse early mutations before any write rather than transiently replacing live.
        Compare(Live+0x10,BeginLedgerAddress+0x10,0x1F0);
        e.C(a0,Preparation.OldTailAddress);e.C(a1,0x100);e.C(a2,Live);e.C(a3,0);e.Call(Preparation.EntryPc);
        Unrevoked();e.B(5,v0,0,"failure");Validate(true);
        Expect(Preparation.OutputAddress+0x320,NativeCollectibleSaveTailKernel.PreparedMagic,"failure");
        Compare(Preparation.OutputAddress+0x10,BeginLedgerAddress+0x10,0x1F0);
        Request(4,ExpectedOrdinaryAddress,0x200,0x600,NativeCollectibleSaveControllerStage.WritingOrdinary);
        e.Label("complete-2");Copy(ExpectedOrdinaryAddress,OrdinaryReadbackAddress,0x600,true);
        Request(3,OrdinaryReadbackAddress,0x200,0x600,NativeCollectibleSaveControllerStage.ReadingOrdinary);
        e.Label("complete-3");Compare(ExpectedOrdinaryAddress,OrdinaryReadbackAddress,0x600);
        Request(4,Preparation.OutputAddress+0x200,0x1400,0x100,NativeCollectibleSaveControllerStage.WritingTail);
        e.Label("complete-4");Copy(Preparation.OutputAddress+0x200,TailReadbackAddress,0x100,true);
        Request(3,TailReadbackAddress,0x1400,0x100,NativeCollectibleSaveControllerStage.ReadingTail);
        e.Label("complete-5");Compare(Preparation.OutputAddress+0x200,TailReadbackAddress,0x100);Validate(true);
        Copy(Live,ProposalAddress,512);Copy(Preparation.OutputAddress+0x300,ProposalAddress+0x50,32);
        Read(t0,BeginLedgerAddress+0x7C);Write(t0,ProposalAddress+0x80);Read(t1,Live+0x7C);e.B(5,t0,t1,"retain-dirty");
        Read(t0,BeginLedgerAddress+0x90,0x24);Read(t1,Live+0x90,0x24);e.B(5,t0,t1,"retain-dirty");
        Write(0,ProposalAddress+0x19,0x28);e.Label("retain-dirty");Unrevoked();
        Set(StateAddress+0x1C,ProposalMagic);Set(StateAddress,(uint)NativeCollectibleSaveControllerStage.Committed);e.B(4,0,0,"finish");

        e.Label("pending");Expect(StateAddress+0x20,(uint)NativeCollectibleSavePendingReceipt.Accepted,"failure");
        Expect(Latch,0,"failure");Read(t0,StateAddress);e.C(t1,2);e.B(4,t0,t1,"pending-write");
        e.C(t1,4);e.B(4,t0,t1,"pending-write");Expect(Busy,3,"failure");e.B(4,0,0,"pending-valid");
        e.Label("pending-write");Expect(Busy,4,"failure");e.Label("pending-valid");Leave();Validate(true);e.B(4,0,0,"finish");
        e.Label("failure");Read(t0,StateAddress+0x14);e.B(5,t0,0,"revoked");Set(StateAddress+0x18,1);Set(StateAddress+0x1C,0);
        Set(StateAddress,(uint)NativeCollectibleSaveControllerStage.Failed);e.B(4,0,0,"finish");
        e.Label("revoked");Set(StateAddress+0x1C,0);Set(StateAddress,(uint)NativeCollectibleSaveControllerStage.Revoked);
        e.Label("finish");Read(v0,StateAddress);
        e.Label("restore");
        Leave();
        e.L(0x23,t0,sp,0x34);e.R(17,t0,0,0);e.L(0x23,t0,sp,0x38);e.R(19,t0,0,0);
        for(int i=0;i<saved.Length;i++)e.L(0x23,saved[i],sp,0x20+i*4);
        e.I(9,sp,sp,FrameByteLength);e.B(4,0,0,"return");

        RevokePc=e.Pc;Equal(a0,StateAddress,"preflight");Equal(a1,0,"preflight");Equal(a2,0,"preflight");Equal(a3,0,"preflight");
        Set(StateAddress+0x14,1);Set(StateAddress+0x1C,0);Set(StateAddress,(uint)NativeCollectibleSaveControllerStage.Revoked);
        e.Label("unframed-revoked");e.C(v0,(uint)NativeCollectibleSaveControllerStage.Revoked);e.B(4,0,0,"return");
        e.Label("preflight");e.C(v0,(uint)NativeCollectibleSaveControllerStage.PreflightRefused);
        e.Label("return");e.R(8,ra,0,0);e.W(0);

        DrainPc=e.Pc;Equal(a0,StateAddress,"preflight");Equal(a1,0,"preflight");Equal(a2,0,"preflight");Equal(a3,0,"preflight");Stack();
        Read(t0,StateAddress);e.B(4,t0,0,"drain-entry");e.I(9,t0,t1,-6);e.I(11,t1,t2,3);e.B(4,t2,0,"preflight");
        e.Label("drain-entry");Save();Enter();Read(t0,StateAddress+0x20);e.B(4,t0,0,"drain-none");
        ExpectedOperation("drain-uncertain");Read(t0,StateAddress+0x20);
        e.C(t1,(uint)NativeCollectibleSavePendingReceipt.Rejected);e.B(4,t0,t1,"drain-rejected");
        e.C(t1,(uint)NativeCollectibleSavePendingReceipt.Consumed);e.B(4,t0,t1,"drain-consumed");
        PendingReceipt("drain-uncertain");
        // Never consume a foreign/stale completion. Pending and already
        // completed ownership are two distinct source-backed entry shapes.
        Read(t0,Busy);e.B(4,t0,0,"drain-latched");e.B(5,t0,t3,"drain-uncertain");Expect(Latch,0,"drain-uncertain");e.B(4,0,0,"drain-poll");
        e.Label("drain-latched");Expect(Latch,1,"drain-uncertain");Read(t0,0x80075B94);e.B(5,t0,t3,"drain-uncertain");
        e.Label("drain-poll");Set(StateAddress+4,uint.MaxValue);Set(StateAddress+8,uint.MaxValue);
        e.C(a0,1);e.C(a1,StateAddress+4);e.C(a2,StateAddress+8);e.Call(PollPc);e.CallLabel("record-poll");
        Read(t0,StateAddress+0x20);e.C(t1,(uint)NativeCollectibleSavePendingReceipt.Consumed);e.B(4,t0,t1,"drain-consumed");
        e.C(t1,(uint)NativeCollectibleSavePendingReceipt.Accepted);e.B(5,t0,t1,"drain-uncertain");
        e.C(v0,(uint)NativeCollectibleSaveDrainResult.Pending);e.B(4,0,0,"restore");
        e.Label("drain-none");Expect(StateAddress+0x24,0,"drain-uncertain");Expect(StateAddress+0x28,0,"drain-uncertain");Expect(StateAddress+0x2C,0,"drain-uncertain");
        e.B(4,0,0,"drain-no-owned");
        e.Label("drain-rejected");Expect(StateAddress+0x28,0,"drain-uncertain");Expect(StateAddress+0x2C,uint.MaxValue,"drain-uncertain");
        e.Label("drain-no-owned");e.C(v0,(uint)NativeCollectibleSaveDrainResult.NoOwnedTransfer);e.B(4,0,0,"drain-clean");
        e.Label("drain-consumed");ExpectedOperation("drain-uncertain");Expect(StateAddress+0x28,1,"drain-uncertain");Expect(StateAddress+0x2C,1,"drain-uncertain");
        Read(t0,StateAddress+4);e.B(5,t0,t3,"drain-uncertain");e.C(v0,(uint)NativeCollectibleSaveDrainResult.Consumed);
        e.Label("drain-clean");Expect(Busy,0,"drain-uncertain");Expect(Latch,0,"drain-uncertain");e.B(4,0,0,"restore");
        e.Label("drain-uncertain");
        // A contradictory observation permanently loses an accepted owner's
        // identity. A later same-operation foreign latch cannot heal it. Clean
        // None/Rejected/Consumed receipts are retained when globals are foreign.
        Read(t0,StateAddress+0x20);e.C(t1,(uint)NativeCollectibleSavePendingReceipt.Accepted);e.B(5,t0,t1,"drain-uncertain-result");
        Receipt(NativeCollectibleSavePendingReceipt.Indeterminate);
        e.Label("drain-uncertain-result");e.C(v0,(uint)NativeCollectibleSaveDrainResult.Indeterminate);e.B(4,0,0,"restore");

        e.Label("record-request");Write(v0,StateAddress+0x28);e.B(4,v0,0,"receipt-rejected");e.C(t0,1);e.B(5,v0,t0,"receipt-uncertain");
        Receipt(NativeCollectibleSavePendingReceipt.Accepted);e.B(4,0,0,"receipt-return");
        e.Label("receipt-rejected");Receipt(NativeCollectibleSavePendingReceipt.Rejected);e.B(4,0,0,"receipt-return");
        e.Label("record-poll");Write(v0,StateAddress+0x2C);ExpectedOperation("receipt-uncertain");
        Expect(StateAddress+0x20,(uint)NativeCollectibleSavePendingReceipt.Accepted,"receipt-uncertain");Expect(StateAddress+0x28,1,"receipt-uncertain");
        Read(t0,StateAddress+4);e.B(5,t0,t3,"receipt-uncertain");Expect(Latch,0,"receipt-uncertain");
        e.B(4,v0,0,"receipt-pending");e.C(t0,1);e.B(5,v0,t0,"receipt-uncertain");Expect(Busy,0,"receipt-uncertain");
        Receipt(NativeCollectibleSavePendingReceipt.Consumed);e.B(4,0,0,"receipt-return");
        e.Label("receipt-pending");Read(t0,Busy);e.B(5,t0,t3,"receipt-uncertain");e.B(4,0,0,"receipt-return");
        e.Label("receipt-uncertain");Receipt(NativeCollectibleSavePendingReceipt.Indeterminate);
        e.Label("receipt-return");e.R(8,ra,0,0);e.W(0);
        // Shared validation keeps its return in the controller's final owned
        // frame word. Its only child is the stack-free query; no hidden push.
        foreach(bool captured in new[]{false,true})
        {
            e.Label(captured?"validate-current":"validate-initial");e.I(0x2B,sp,ra,0x3C);ValidateBody(captured);
            e.L(0x23,ra,sp,0x3C);e.R(8,ra,0,0);e.W(0);
        }
        e.Label("validate-filename");byte[] filename=CopyFilename();
        for(int at=0;at<16;at+=4)Expect(FilenameAddress+(uint)at,BinaryPrimitives.ReadUInt32LittleEndian(filename.AsSpan(at)),"failure");
        Read(t0,FilenameAddress+16,0x25);Equal(t0,BinaryPrimitives.ReadUInt16LittleEndian(filename.AsSpan(16)),"failure");
        e.R(8,ra,0,0);e.W(0);
        if(IrqWindowsEmitted)
        {
            // Private successor frame words: token40, held44, helperRA48,
            // v0save4C. No native argument home/fifth-argument word is borrowed.
            // The source protocol exits only for Enter's exact token1; an
            // already-disabled caller stays disabled. No BIOS model is claimed.
            e.Label("irq-enter");IrqEnterHelperPc=e.Pc;
            e.I(0x2B,sp,ra,0x48);e.I(0x2B,sp,v0,0x4C);e.Call(EnterCriticalPc);
            e.I(0x2B,sp,v0,0x40);e.C(t0,1);e.I(0x2B,sp,t0,0x44);
            e.I(0x23,sp,v0,0x4C);e.I(0x23,sp,ra,0x48);e.W(0);e.R(8,ra,0,0);e.W(0);
            e.Label("irq-leave");IrqLeaveHelperPc=e.Pc;
            e.L(0x23,t0,sp,0x44);e.B(4,t0,0,"irq-return");
            e.I(0x2B,sp,ra,0x48);e.I(0x2B,sp,v0,0x4C);e.I(0x2B,sp,0,0x44);
            e.L(0x23,t0,sp,0x40);e.C(t1,1);e.B(5,t0,t1,"irq-restored");e.Call(ExitCriticalPc);
            e.Label("irq-restored");e.I(0x23,sp,v0,0x4C);e.I(0x23,sp,ra,0x48);e.W(0);
            e.Label("irq-return");IrqLeaveReturnPc=e.Pc;e.R(8,ra,0,0);e.W(0);
        }
        return e.Finish();
    }
    private sealed class Emitter(uint start,bool compact)
    {
        private readonly List<uint> words=[];private readonly Dictionary<string,int> labels=[];private readonly List<(int,string)> branches=[],calls=[];
        internal uint Pc=>checked(start+(uint)words.Count*4);
        internal string Unique()=>"local-"+words.Count;
        internal void W(uint v)=>words.Add(v);
        internal void I(int op,int rs,int rt,int value)=>W((uint)(op<<26|rs<<21|rt<<16)|(ushort)value);
        internal void R(int fn,int rs,int rt,int rd,int shift=0)=>W((uint)(rs<<21|rt<<16|rd<<11|shift<<6|fn));
        internal void C(int r,uint v){if(compact&&(v<=0x7FFF||v>=0xFFFF8000))I(9,0,r,unchecked((int)v));else{I(15,0,r,(int)(v>>16));I(13,r,r,(int)v);}}
        internal void L(int op,int r,int p,int at){I(op,p,r,at);W(0);}
        internal void B(int op,int rs,int rt,string target){branches.Add((words.Count,target));I(op,rs,rt,0);W(0);}
        internal void Call(uint pc){W(0x0C000000|((pc>>2)&0x3FFFFFF));W(0);}
        internal void CallLabel(string name){calls.Add((words.Count,name));W(0);W(0);}
        internal void Label(string name)=>labels.Add(name,words.Count);
        internal byte[] Finish()
        {
            foreach(var (at,name)in branches){int n=labels[name]-at-1;if(n<short.MinValue||n>short.MaxValue)throw new InvalidDataException("Controller branch overflow.");words[at]|=(ushort)(short)n;}
            foreach(var (at,name)in calls)words[at]=0x0C000000|(((start+(uint)labels[name]*4)>>2)&0x3FFFFFF);
            byte[] bytes=new byte[words.Count*4];for(int i=0;i<words.Count;i++)BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(i*4),words[i]);return bytes;
        }
    }
}
