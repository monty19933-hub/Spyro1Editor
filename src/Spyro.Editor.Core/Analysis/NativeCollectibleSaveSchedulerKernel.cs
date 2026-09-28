using System.Buffers.Binary;
using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.Core.Analysis;

/// <summary>
/// DETACHED fixed-profile, terminal-held same-overlay scheduler. It issues no placement,
/// cache, IRQ exclusion, native session, retention, rearm or publication right.
/// The caller must exclude mutation/IRQ during each entry. The native setup
/// service is deliberately not represented as an IRQ-safe leaf.
/// Flow5 permanently retains any precheck/header receipt and its buffer; it has
/// no header-drain or owner-release entry. Flow4 uses the controller's separate
/// terminal Drain but likewise grants no release/rearm. A zero-flow clean save
/// or nonapplicable callback follows retail; an armed flow cannot do so.
/// </summary>
internal sealed class NativeCollectibleSaveSchedulerKernel
{
    internal const int CombinedOwnerBytes=0x7C8,MainCapacityBytes=0x5C0,FinalCapacityBytes=0x208,StateBytes=0x40,FrameBytes=0x30;
    internal const uint EntryPc=0x80088C78,FinalPc=0x8008A830,StateAddress=0x8008A9F8,Live=0x801FF600;
    private const uint Busy=0x80075B50,Magic=0x53534331;
    private uint ControllerState=>Controller.StateAddress;
    private uint OrdinaryInput=>Controller.OrdinaryInputAddress;
    private uint Begin=>Controller.BeginPc;
    private uint Pump=>Controller.PumpPc;
    private uint Drain=>Controller.DrainPc;
    private readonly byte[] bytes;
    private NativeCollectibleSaveSchedulerKernel(NativeCollectibleSaveControllerKernel controller,IReadOnlyList<(uint Address,uint Value)> globals)
    {
        Controller=controller;ExpectedGlobals=Array.AsReadOnly(globals.ToArray());
        if(globals.Count!=15)throw new InvalidDataException("Closed scheduler globals table changed its exact owner.");
        bytes=Emit();
        if(bytes.Length>CombinedOwnerBytes)
            throw new InvalidDataException($"Measured scheduler exceeds two approved owners: {bytes.Length:X}.");
    }
    internal static NativeCollectibleSaveSchedulerKernel CreateFixture()=>new(NativeCollectibleSaveControllerKernel.CreateFixture(0x80167D60),
        new(uint,uint)[]{(0x8007596C,10),(0x80078D00,1),(0x80075690,0),(0x80075734,0x8007D9C8),
            (0x800785D8,0x8008AA38),(0x800785E0,0x80117D54),(0x800785E4,0x8016F554),(0x80075828,0x80178600),
            (0x80075890,0x8017D480),(0x80076C78,0x8015A000),(0x80076C7C,0x80160000),
            (0x800755A4,0xA00),(0x80078D14,1),(0x80078D1C,0),(0x800756BC,0x80086134)});
    internal static NativeCollectibleSaveSchedulerKernel Compile(NativeCollectibleSaveWorkspacePlan workspace,NativeJapaneseEggLifecycleOverlayPlan lifecycle)
    {
        lifecycle.RequireWorkspaceCurrent(workspace);workspace.RequireCurrent(workspace.Ancestry);
        if(workspace.Profile!=NativeCollectibleSaveWorkspacePlan.WorkspaceProfile.Controller||workspace.Controller is not{SourceCertified:true} controller||
            lifecycle.LiveGuard is not{SourceBound:true} guard)
            throw new InvalidDataException("Closed source controller workspace and recompiled lifecycle required.");
        return new(controller,guard.ExpectedSceneGlobals.Where(g=>g.Address!=0x800757D8).Concat(new(uint,uint)[]{
            (0x80078D00,1),(0x800755A4,0xA00),(0x80078D14,1),(0x80078D1C,0),(0x800756BC,0x80086134)}).ToArray());
    }
    internal NativeCollectibleSaveControllerKernel Controller{get;}
    internal IReadOnlyList<(uint Address,uint Value)> ExpectedGlobals{get;}
    internal int MainCodeByteLength{get;private set;}
    internal int FinalCodeByteLength{get;private set;}
    internal uint FilenameAddress{get;private set;}
    internal bool SourceCertified=>Controller.SourceCertified;
    internal byte[] CopyMainBytes()=>bytes.AsSpan(0,MainCapacityBytes).ToArray();
    internal byte[] CopyFinalBytes()=>bytes.AsSpan(MainCapacityBytes,FinalCapacityBytes).ToArray();
    internal int CodeByteLength=>MainCodeByteLength+FinalCodeByteLength;
    internal bool HeaderDrainEmitted=>false;
    internal bool NativeIoExecuted=>false;
    internal bool RuntimeVerified=>false;
    internal bool PublicationApproved=>false;
    internal bool LiveCommitEmitted=>false;
    internal bool RearmEmitted=>false;
    internal bool ExternalExclusionRequired=>true;
    internal bool CacheCoherenceProven=>false;
    internal byte[] CopyBytes()=>bytes.ToArray();

    // Flow: 0 fresh,1 precheck,2 header,3 transaction,4 terminal held,
    // 5 permanently blocked. Receipt values match the detached controller.
    // +00 flow,+04 receipt,+08 request-return,+0C poll-return,+10 poll-op,
    // +14 poll-result,+18 port,+1C marker,+20 epoch,+24 generation,+28 fault,
    // +2C magic,+30 expected-op,+34 last-drain,+38..3F reserved.
    private byte[] Emit()
    {
        var e=new Emitter(EntryPc);
        const int v0=2,a0=4,a1=5,a2=6,a3=7,t0=8,t1=9,t2=10,t3=11,t4=12,
            s0=16,s1=17,s2=18,sp=29,ra=31;
        void Eq(int r,uint value,string fail){e.C(t1,value);e.B(5,r,t1,fail);}
        void Read(int r,uint at,int op=0x23){e.C(t4,at);e.L(op,r,t4,0);}
        void Set(int at,uint value){e.C(t0,value);e.I(0x2B,s0,t0,at);}
        void Expect(uint at,uint value,string fail,int op=0x23){Read(t0,at,op);Eq(t0,value,fail);}
        void Ret(){e.R(8,ra,0,0);e.W(0);}
        void StateArgs(){e.C(a0,ControllerState);e.C(a1,0);e.C(a2,0);e.C(a3,0);}

        // Non-applicable callbacks retain the exact original JR RA behavior.
        Eq(ra,0x80031E08,"native-return");
        Expect(0x800757D8,11,"skip-unframed");Expect(0x80078D0C,2,"skip-unframed");
        e.I(12,sp,t0,7);e.B(5,t0,0,"skip-unframed");
        e.C(t0,0x801FF910);e.R(0x2B,sp,t0,t1);e.B(5,t1,0,"skip-unframed");
        e.C(t0,0x801FFF78);e.R(0x2B,t0,sp,t1);e.B(5,t1,0,"skip-unframed");
        // A fixed table minimizes the full source-owner gate. No tail read or
        // stack write occurs before every table entry has matched.
        e.Address(t2,"globals");e.C(t3,15);e.Label("global-loop");
        e.I(0x23,t2,t0,0);e.I(0x23,t2,t1,4);e.L(0x23,t0,t0,0);
        e.B(5,t0,t1,"skip-unframed");e.I(9,t2,t2,8);e.I(9,t3,t3,-1);e.B(5,t3,0,"global-loop");
        e.I(9,sp,sp,-FrameBytes);e.I(0x2B,sp,s0,0x18);e.I(0x2B,sp,s1,0x1C);e.I(0x2B,sp,s2,0x20);e.I(0x2B,sp,ra,0x24);
        e.R(16,0,0,t0);e.I(0x2B,sp,t0,0x28);e.R(18,0,0,t0);e.I(0x2B,sp,t0,0x2C);
        e.C(s0,StateAddress);e.C(s1,Busy);e.C(s2,Live);
        e.L(0x23,t0,s0,0);Eq(t0,4,"not-held");StateArgs();e.Call(Drain);e.I(0x2B,s0,v0,0x34);e.B(4,0,0,"finish");
        e.Label("not-held");e.C(t1,5);e.B(4,t0,t1,"finish");e.I(11,t0,t1,4);e.B(4,t1,0,"blocked");
        e.CallLabel("context");e.L(0x23,t0,s0,0);e.B(4,t0,0,"fresh");
        e.L(0x23,t0,s0,0x2C);Eq(t0,Magic,"blocked");
        e.L(0x23,t0,s0,0);e.C(t1,3);e.B(4,t0,t1,"transaction");

        // Shared, nonblocking precheck/header receipt path. A mismatched busy
        // operation or foreign completion is left unconsumed and sticky.
        e.L(0x23,t0,s0,4);Eq(t0,2,"blocked");e.L(0x23,t0,s0,8);Eq(t0,1,"blocked");
        e.L(0x23,t2,s0,0x30);e.L(0x23,t0,s1,0);e.B(4,t0,0,"completed-latch");e.B(5,t0,t2,"blocked");
        e.L(0x23,t0,s1,8);e.B(5,t0,0,"blocked");e.B(4,0,0,"finish");
        e.Label("completed-latch");e.L(0x23,t0,s1,8);Eq(t0,1,"blocked");
        e.L(0x23,t0,s1,0x44);e.B(5,t0,t2,"blocked");
        e.C(a0,1);e.I(9,s0,a1,0x10);e.I(9,s0,a2,0x14);e.Call(0x80067628);e.I(0x2B,s0,v0,0xC);
        Eq(v0,1,"blocked");e.I(0x23,s0,t0,0x10);e.L(0x23,t1,s0,0x30);e.B(5,t0,t1,"blocked");
        Set(4,4);e.CallLabel("idle");e.CallLabel("context");
        e.L(0x23,t0,s0,0);e.C(t1,2);e.B(4,t0,t1,"header-complete");
        e.L(0x23,t0,s0,0x14);e.B(4,t0,0,"request-header");Eq(t0,3,"blocked");
        e.Label("request-header");e.CallLabel("queue");Set(0,2);Set(0x30,3);e.CallLabel("intent");
        e.L(0x23,a0,s0,0x18);e.Address(a1,"filename");e.C(a2,OrdinaryInput);e.C(a3,0);e.C(t0,0x80);e.I(0x2B,sp,t0,0x10);
        e.Call(0x80066E28);e.B(4,0,0,"request-return");

        e.Label("fresh");
        // No state1/state2 adoption, and no zeroing a possibly owned queue.
        Expect(0x80078D08,0,"blocked");Expect(ControllerState,0,"blocked");
        e.C(t2,StateAddress);e.C(t3,StateAddress+StateBytes);e.Label("zero-state");e.I(0x23,t2,t0,0);e.I(9,t2,t2,4);e.B(5,t0,0,"blocked");
        e.B(5,t2,t3,"zero-state");e.CallLabel("idle");Expect(0x800751B0,uint.MaxValue,"blocked");
        Read(t0,0x80078D18);e.I(0x2B,s0,t0,0x18);Read(t0,0x80078D20);e.I(0x2B,s0,t0,0x1C);
        e.L(0x23,t0,s2,0x8C);e.I(0x2B,s0,t0,0x20);e.L(0x23,t0,s2,0x7C);e.I(0x2B,s0,t0,0x24);Set(0x2C,Magic);
        // Service initialization is source-pinned, but BIOS/event/IRQ stack
        // closure remains an external installation gate, not a proven bound.
        Set(0,1);e.Call(0x800662BC);e.CallLabel("context");e.CallLabel("queue");
        Set(0x30,2);e.CallLabel("intent");e.L(0x23,a0,s0,0x18);e.Call(0x800665B8);
        e.Label("request-return");e.I(0x2B,s0,v0,8);Set(4,5);e.B(4,v0,0,"rejected");Eq(v0,1,"blocked");
        Set(4,2);e.L(0x23,t0,s1,0);e.L(0x23,t1,s0,0x30);e.B(5,t0,t1,"blocked");
        e.L(0x23,t0,s1,8);e.B(5,t0,0,"blocked");e.CallLabel("context");e.B(4,0,0,"finish");

        e.Label("header-complete");e.L(0x23,t0,s0,0x14);e.B(5,t0,0,"blocked");
        Read(t0,OrdinaryInput+0x7E,0x25);e.L(0x23,t1,s0,0x1C);e.B(5,t0,t1,"blocked");
        e.C(a0,OrdinaryInput);e.Call(0x80059864);e.CallLabel("context");
        e.C(a0,OrdinaryInput);e.C(a1,0x600);e.C(a2,Live);e.C(a3,0);e.Call(Begin);Set(0,3);e.B(4,0,0,"controller-return");
        e.Label("transaction");StateArgs();e.Call(Pump);
        e.Label("controller-return");e.I(9,v0,t0,-1);e.I(11,t0,t1,5);e.B(5,t1,0,"finish");
        e.I(9,v0,t0,-6);e.I(11,t0,t1,3);e.B(4,t1,0,"blocked");Set(0,4);e.B(4,0,0,"finish");

        // Context is scalar selection/epoch continuity, not a reusable native
        // session proof. Controller subsequently validates complete ledger and
        // mutation highwater. Before Begin, any pickup makes this arm refuse.
        e.Section(FinalPc);e.Label("context");e.L(0x25,t0,s2,0x16);Eq(t0,1,"blocked");
        e.L(0x25,t0,s2,0x18);e.I(9,t0,t1,-0x100);e.I(11,t1,t2,2);e.B(4,t2,0,"blocked");e.R(0,0,t1,t1,4);
        Read(t0,0x80078D18);e.B(5,t0,t1,"blocked");e.L(0x25,t1,s2,0x1A);Read(t0,0x80078D20);e.B(5,t0,t1,"blocked");
        e.L(0x23,t0,s2,0x8C);e.B(4,t0,0,"blocked");e.L(0x23,t3,s2,0x7C);e.B(4,t3,0,"blocked");
        e.L(0x23,t2,s0,0);e.B(4,t2,0,"context-return");
        e.L(0x23,t1,s0,0x20);e.B(5,t0,t1,"blocked");Read(t0,0x80078D18);e.L(0x23,t1,s0,0x18);e.B(5,t0,t1,"blocked");
        Read(t0,0x80078D20);e.L(0x23,t1,s0,0x1C);e.B(5,t0,t1,"blocked");
        e.C(t0,3);e.B(4,t2,t0,"context-return");e.L(0x23,t1,s0,0x24);e.B(5,t3,t1,"blocked");
        e.Label("context-return");Ret();
        e.Label("intent");Set(4,1);Set(8,uint.MaxValue);Set(0xC,uint.MaxValue);Ret();
        e.Label("rejected");Set(4,3);e.B(4,0,0,"blocked");
        e.Section(EntryPc);
        e.Label("queue");Read(t0,0x800751B0);e.I(9,t0,t0,1);e.I(11,t0,t1,4);e.B(4,t1,0,"blocked");
        e.Label("idle");e.L(0x23,t0,s1,0);e.L(0x23,t1,s1,8);e.R(0x25,t0,t1,t0);e.B(5,t0,0,"blocked");Ret();
        e.Label("blocked");e.L(0x23,t0,s0,0);e.B(4,t0,0,"finish");Set(0,5);Set(0x28,1);
        e.Label("finish");e.I(0x23,sp,t0,0x28);e.I(0x23,sp,t1,0x2C);e.R(17,t0,0,0);e.R(19,t1,0,0);
        e.I(0x23,sp,s0,0x18);e.I(0x23,sp,s1,0x1C);e.I(0x23,sp,s2,0x20);e.I(0x23,sp,ra,0x24);e.I(9,sp,sp,FrameBytes);
        e.Label("skip-unframed");Read(t0,StateAddress);e.B(4,t0,0,"native-return");e.Jump(0x800324B8);e.Label("native-return");Ret();

        // Fixture values are not an alternate source authority. The closed
        // plan will supply exactly its projected source owners if this fits.
        e.Section(FinalPc);e.Label("globals");
        foreach(var(at,value)in ExpectedGlobals){e.W(at);e.W(value);}
        e.Label("filename");FilenameAddress=e.Pc;ReadOnlySpan<byte> filename="BASCUS-94228SPYRO\0\0\0"u8;
        for(int i=0;i<filename.Length;i+=4)e.W(BinaryPrimitives.ReadUInt32LittleEndian(filename.Slice(i,4)));
        byte[] result=e.Finish(out int main,out int final);MainCodeByteLength=main;FinalCodeByteLength=final;return result;
    }
    private sealed class Emitter
    {
        private readonly uint start;
        internal Emitter(uint at){start=at;current=at;sections=new(){{at,[]},{FinalPc,[]}};}
        private readonly Dictionary<uint,List<uint>> sections;
        private readonly Dictionary<string,uint> labels=[];
        private readonly List<(uint At,string Label)> branches=[],calls=[],addresses=[];
        private uint current;
        internal uint Pc=>checked(current+(uint)sections[current].Count*4);
        internal void Section(uint at)=>current=at;
        internal void W(uint value)=>sections[current].Add(value);
        internal void I(int op,int rs,int rt,int immediate)=>W((uint)(op<<26|rs<<21|rt<<16)|(ushort)immediate);
        internal void R(int fn,int rs,int rt,int rd,int shift=0)=>W((uint)(rs<<21|rt<<16|rd<<11|shift<<6|fn));
        internal void C(int r,uint value){if(value<=0x7FFF)I(9,0,r,(int)value);else if(value>=0xFFFF8000)I(9,0,r,unchecked((int)value));else{I(15,0,r,(int)(value>>16));I(13,r,r,(int)value);}}
        internal void L(int op,int r,int p,int at){I(op,p,r,at);W(0);}
        internal void B(int op,int rs,int rt,string label){branches.Add((Pc,label));I(op,rs,rt,0);W(0);}
        internal void Call(uint pc){W(0x0C000000|((pc>>2)&0x3FFFFFF));W(0);}
        internal void CallLabel(string label){calls.Add((Pc,label));W(0);W(0);}
        internal void Address(int r,string label){addresses.Add((Pc,label));C(r,0x80000000);}
        internal void Jump(uint pc){W(0x08000000|((pc>>2)&0x3FFFFFF));W(0);}
        internal void Label(string label)=>labels.Add(label,Pc);
        internal byte[] Finish(out int main,out int final)
        {
            main=sections[start].Count*4;final=sections[FinalPc].Count*4;
            if(main>MainCapacityBytes||final+StateBytes>FinalCapacityBytes)
                throw new InvalidDataException($"Partition measurement main={main:X}/5C0 excess={Math.Max(0,main-MainCapacityBytes):X}, final-code-table={final:X}+state40/208 excess={Math.Max(0,final+StateBytes-FinalCapacityBytes):X}; no growth authorized.");
            void Patch(uint at,Func<uint,uint> change){uint section=at>=FinalPc?FinalPc:start;int index=checked((int)((at-section)/4));sections[section][index]=change(sections[section][index]);}
            foreach(var(at,label)in branches){long delta=((long)labels[label]-at)/4-1;if(delta<short.MinValue||delta>short.MaxValue)throw new InvalidDataException("Scheduler branch overflow.");Patch(at,w=>w|(ushort)(short)delta);}
            foreach(var(at,label)in calls)Patch(at,_=>0x0C000000|((labels[label]>>2)&0x3FFFFFF));
            foreach(var(at,label)in addresses){uint pc=labels[label];Patch(at,w=>w|(pc>>16));Patch(at+4,w=>w|(pc&0xFFFF));}
            byte[] result=new byte[CombinedOwnerBytes];int cursor=0;
            foreach(uint section in new[]{start,FinalPc}){for(int i=0;i<sections[section].Count;i++)BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(cursor+i*4),sections[section][i]);cursor+=section==start?MainCapacityBytes:FinalCapacityBytes;}
            return result;
        }
    }
}
