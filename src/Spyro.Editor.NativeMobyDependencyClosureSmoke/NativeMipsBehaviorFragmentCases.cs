using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Scene;

internal static class NativeMipsBehaviorFragmentCases
{
    internal static void Run(Action<bool,string> check,Action<Action,string> reject)
    {
        byte[] bytes=Words(0x10000003,0x24020007,0,0,0x08004008,0x24420001);
        NativeAddressSpace original=new(NativeAddressKind.ExecutableFile,"mips-source",NativeAddressRevision.Original,bytes.Length);
        NativeMobyDependencyRead read=new("code",new(original,0,bytes.Length),Hash(bytes));
        byte[] Reader(NativeByteExtent extent)=>extent.Space==original?bytes.AsSpan((int)extent.Offset,(int)extent.ByteLength).ToArray():throw new InvalidDataException("unbound source");
        var fragment=new NativeMipsBehaviorFragmentSource("main",0x80010000,read);
        var package=NativeMipsBehaviorFragmentCodec.Capture([fragment],[0x80010000],[0x80010020],Reader);
        check(package.InstructionCount==6&&package.Transfers.Count==2&&package.Transfers.All(t=>t.HasDelaySlot),"MIPS direct branch/jump delay inventory changed.");
        NativeAddressSpace final=new(NativeAddressKind.ExecutableFile,"mips-final",NativeAddressRevision.Final,0x200);
        var linked=package.Link(new(final,0,0x200),0x80020000,[new(0x80010010,0x80010020,0x80020080,new(final,0x80,4))]);
        byte[] output=linked.CopyBytes();check(Word(output,0)==0x10000003&&Word(output,0x10)==0x08008020,"Typed branch/jump target did not relocate.");
        check(!linked.CompleteInstalledCodeOwnerProven&&linked.UnmaterializedExternalTargets.Single().Offset==0x80,"An external code reservation became proof of materialized executable bytes.");
        check(Word(output,4)==0x24020007&&Word(output,0x14)==0x24420001,"Native delay slot byte was rewritten.");
        output[4]^=1;check(linked.CopyBytes()[4]!=output[4],"Linked code aliases caller memory.");
        reject(()=>package.Link(new(final,0,0x200),0x80020000,[]),"unresolved");
        reject(()=>package.Link(new(final,0,0x200),0x80020000,[new(0x80010010,0x80010020,0x80020080,new(new(NativeAddressKind.ExecutableFile,"mips-final",NativeAddressRevision.Final,0x200),0x80,4))]),"domain");
        reject(()=>package.Link(new(final,0,0x200),0x80020000,[new(0x80010010,0x80010020,0x80020100,new(final,0x80,4))]),"mapping");
        reject(()=>package.Link(new(final,0,0x200),0x80020000,[new(0x80010010,0x80010024,0x80020080,new(final,0x80,4))]),"preimage");
        reject(()=>package.Link(new(final,0,0x200),0x80020000,[new(0x80010010,0x80010020,0x80020080,new(final,0x80,4)),new(0x80010010,0x80010020,0x80020080,new(final,0x80,4))]),"duplicate");
        reject(()=>package.Link(new(final,0,0x200),0x80020000,[new(0x80010010,0x80010020,0x80020000,new(final,0,4))]),"generated code overlap");
        reject(()=>package.Link(new(final,0,0x200),0x80020000,[new(0x80010010,0x80010020,0x80020014,new(final,0x14,8))]),"generated code overlap");
        check(package.Link(new(final,0x20,0x1E0),0x80020020,[new(0x80010010,0x80010020,0x80020038,new(final,0x38,4))])
            .UnmaterializedExternalTargets.Single().Offset==0x38,"External code reservation immediately after the generated prefix was rejected or remapped.");
        ExpectMessage(()=>NativeMipsBehaviorFragmentCodec.Capture([fragment],[0x80010000],[],e=>bytes.Concat(new byte[4]).ToArray()),"length","oversized source read checked before cloning");
        ExpectMessage(()=>NativeMipsBehaviorFragmentCodec.Capture([fragment],new BoundedProbeList<uint>(0x80010000,65),[],Reader),"budget","entry snapshot bounded independently of claimed Count");
        ExpectMessage(()=>NativeMipsBehaviorFragmentCodec.Capture([fragment],[0x80010000],new BoundedProbeList<uint>(0x80010020,65),Reader),"budget","exit snapshot bounded independently of claimed Count");
        ExpectMessage(()=>package.Link(new(final,0,0x200),0x80020000,new BoundedProbeList<NativeMipsBehaviorExternalBinding>(new(0x80010010,0x80010020,0x80020080,new(final,0x80,4)),8193)),"budget","binding snapshot bounded independently of claimed Count");
        var changed=bytes.ToArray();bytes[0]^=1;reject(()=>NativeMipsBehaviorFragmentCodec.Capture([fragment],[0x80010000],[0x80010020],Reader),"preimage");changed.CopyTo(bytes,0);
        reject(()=>Capture(Words(0x08004000)),"delay");
        reject(()=>Capture(Words(0x08004000,0x08004000)),"delay");
        reject(()=>Capture(Words(0x00200008,0)),"computed");
        reject(()=>Capture(Words(0x42000010,0)),"opcode");
        reject(()=>NativeMipsBehaviorFragmentCodec.Capture([fragment,fragment],[0x80010000],[],Reader),"duplicate");
        IEnumerable<NativeMipsBehaviorFragmentSource> Infinite(){while(true)yield return fragment;}
        reject(()=>NativeMipsBehaviorFragmentCodec.Capture(Infinite(),[0x80010000],[],Reader),"budget");
        var delayEntry=Capture(Words(0x08004008,0),0x80010000,0x80010004);
        check(delayEntry.Transfers.Any(t=>t.Kind==NativeMipsBehaviorTransferKind.Fallthrough),"Independent entry into a final delay word lost its ordinary fallthrough obligation.");
        reject(()=>delayEntry.Link(new(final,0,0x200),0x80020000,[new(0x80010000,0x80010020,0x80020080,new(final,0x80,4))]),"unresolved");
        var branch=Capture(Words(0x10000007,0));var far=new NativeAddressSpace(NativeAddressKind.ExecutableFile,"far",NativeAddressRevision.Final,0x30000);
        reject(()=>branch.Link(new(far,0,0x30000),0x80020000,[new(0x80010000,0x80010020,0x80040004,new(far,0x20004,4))]),"displacement");
        byte[] calling=Words(0x0C004000,0,0x0800C010,0);
        var callDomain=new NativeAddressSpace(NativeAddressKind.LevelData,"caller",NativeAddressRevision.Original,calling.Length);
        var callPackage=NativeMipsBehaviorFragmentCodec.Capture([new("caller",0x80030000,new("code",new(callDomain,0,calling.Length),Hash(calling)))],
            [0x80030000],[0x80030040],e=>e.Space==callDomain?calling.ToArray():Reader(e));
        NativeMipsBehaviorExternalBinding globalBinding=new(0x80030000,0x80010000,0x80010000,new(original,0,4),read,0x80010000);
        NativeMipsBehaviorExternalBinding returnBinding=new(0x80030008,0x80030040,0x80040080,new(final,0x80,4));
        check(Word(callPackage.Link(new(final,0,0x200),0x80040000,[globalBinding,returnBinding]).CopyBytes(),0)==0x0C004000,
            "Exact source-certified shared EXE call changed its native target.");
        reject(()=>callPackage.Link(new(final,0,0x200),0x80040000,[globalBinding with { Evidence=read with { ExpectedSha256=Hash([0]) } },returnBinding]),"preimage");
        reject(()=>callPackage.Link(new(final,0,0x200),0x80040000,[globalBinding with { TargetPc=0x80010004,Target=new(original,4,4) },returnBinding]),"certificate");
        var adapter=NativeMipsOwnedFrameAdapter.Create(0x80024000,0x80082914);
        check(adapter.FrameByteLength==0x2B0&&!adapter.RuntimeStackFitProven,"Owned adapter nominal stack size became runtime approval.");
        TestAdapter(adapter,check);
        Console.WriteLine("NATIVE_MIPS_FRAGMENT_TYPED_BRANCH_DELAY_AND_OWNED_FRAME_OK");
        return;
        NativeMipsBehaviorFragmentPackage Capture(byte[] fixture,params uint[] entries)
        {var d=new NativeAddressSpace(NativeAddressKind.ExecutableFile,"hostile",NativeAddressRevision.Original,fixture.Length);return NativeMipsBehaviorFragmentCodec.Capture([new("hostile",0x80010000,new("code",new(d,0,fixture.Length),Hash(fixture)))],entries.Length==0?[0x80010000]:entries,[],e=>fixture.ToArray());}
        void ExpectMessage(Action action,string fragment,string label)
        {try{action();}catch(InvalidDataException ex){check(ex.Message.Contains(fragment,StringComparison.OrdinalIgnoreCase),label);return;}throw new InvalidOperationException("Expected bounded refusal: "+label);}
    }
    internal static void Source(string image,Action<bool,string> check)
    {
        var native=NativeDarkHollowWizardBehaviorSource.Capture(image);
        check(native.MainInstructionCount==558&&native.MainFragmentCount==7&&native.SpawnFragmentCount==3,"Source-owned native main/spawn frontier changed.");
        check(native.PackageSha256=="a9a8227c74a9c479f7f94c82314813809bd5caa0a5a137b0c18d614b80b3e3df"&&native.DirectExeCalls.Count==26,
            "Source package differs from the independent raw-sector manifest/call oracle.");
        check(native.MainCallbackCount==6&&native.SpawnCallbackCount==1&&native.NativeImpactChildCount==4&&native.NativeDynamicGuard==21,"Native children/callback/resource guard was suppressed or substituted.");
        check(native.InterceptedExitPc==0x80086D8C&&native.DestinationContinuationPc==0x80082914,"Native donor cursor load was not intercepted exactly.");
        check(native.MainStackLoads==0&&native.MainMaximumStackStoreEnd==0x34,"New source-native stack input/scratch obligation was silently accepted.");
        check(native.MainStackAddressWords.Single().Word==0x27B20068,"Native helper-derived sp+68 address was omitted from the frame obligations.");
        check(!native.CompleteNativeClosureProven&&!native.RuntimeFitProven&&!native.PublicationApproved&&native.UnresolvedObligations.Count>0,"Unresolved callback/global/placement edges became native approval.");
        check(native.Fragments.All(f=>!(f.SourcePc<=0x80086D8C&&f.SourcePc+f.ByteLength>0x80086D8C)),"Donor cursor-load code entered the source package.");
        Console.WriteLine($"NATIVE_DARK_WIZARD_BEHAVIOR_SOURCE_PACKAGE_OK: {native.PackageSha256}; unresolved={native.UnresolvedObligations.Count}; runtime/publication=false");
    }
    private static void TestAdapter(NativeMipsOwnedFrameAdapter adapter,Action<bool,string> check)
    {
        // A small independent R3000 interpreter models branch delay AND one-
        // instruction load delay. It executes generated adapter instructions only.
        uint[] regs=Enumerable.Range(0,32).Select(i=>0x12340000u+(uint)i).ToArray();regs[0]=0;regs[29]=0x801F0000;
        uint[] before=regs.ToArray();Dictionary<uint,uint> memory=[];uint hi=0xABCD,lo=0xFEDC;uint originalHi=hi,originalLo=lo;
        uint darkCursor=0x80123400;memory[regs[29]+0x188]=darkCursor;
        Execute(adapter.CopyEntry(),0x80020000,0x80024000);check(regs[29]==before[29]-0x2B0&&regs[30]==1&&regs[19]==before[19]&&regs[23]==before[23],"Entry changed actor/Spyro roles or lacked private frame/fp=1.");
        for(int i=1;i<32;i++)if(i!=29)regs[i]=0xCAFE0000u+(uint)i;hi=7;lo=8;
        for(uint offset=0;offset<0x230;offset+=4)memory[regs[29]+offset]=0xDEADBEEF;
        Execute(adapter.CopyExit(),0x80021000,0x80082914);
        check(regs.SequenceEqual(before)&&hi==originalHi&&lo==originalLo,"Owned adapter failed exact GPR/HI/LO/SP restoration under load-delay semantics.");
        check(memory[regs[29]+0x188]==darkCursor,"Donor scratch overwrote the Dark caller cursor.");
        Execute(Words(0x8FA90188,0,0),0x80082914);check(regs[9]==darkCursor,"Restored native continuation reads another frame's cursor.");
        return;
        void Execute(byte[] code,uint start,uint? expectedJump=null)
        {
            (int Register,uint Value)? pending=null;uint? jump=null;
            for(int offset=0;offset<code.Length;offset+=4)
            {
                uint w=Word(code,offset),op=w>>26;int rs=(int)(w>>21)&31,rt=(int)(w>>16)&31,rd=(int)(w>>11)&31;short imm=(short)w;
                var previous=pending;pending=null;bool wasDelay=jump.HasValue;
                switch(op)
                {
                    case 0: switch(w&63){case 0:break;case 16:regs[rd]=hi;break;case 18:regs[rd]=lo;break;case 17:hi=regs[rs];break;case 19:lo=regs[rs];break;default:throw new Exception("Unknown adapter SPECIAL.");}break;
                    case 9:regs[rt]=unchecked(regs[rs]+(uint)imm);break;
                    case 0x2B:memory[unchecked(regs[rs]+(uint)imm)]=regs[rt];break;
                    case 0x23:pending=(rt,memory[unchecked(regs[rs]+(uint)imm)]);break;
                    case 2:jump=((start+(uint)offset+4)&0xF0000000)|(w&0x3FFFFFF)*4;break;
                    default:throw new Exception("Unknown adapter opcode.");
                }
                if(previous is{ }p)regs[p.Register]=p.Value;regs[0]=0;
                if(wasDelay){check(offset==code.Length-4&&jump==expectedJump,"Adapter jumped before complete restore/save or to the wrong exact native continuation.");return;}
            }
        }
    }
    private static byte[] Words(params uint[] words){byte[] b=new byte[words.Length*4];for(int i=0;i<words.Length;i++)BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(i*4),words[i]);return b;}
    private static uint Word(ReadOnlySpan<byte>b,int o)=>BinaryPrimitives.ReadUInt32LittleEndian(b[o..]);
    private static string Hash(ReadOnlySpan<byte>b)=>Convert.ToHexStringLower(SHA256.HashData(b));
    private sealed class BoundedProbeList<T>(T item,int finiteBudget):IReadOnlyList<T>
    {
        public int Count=>1;
        public T this[int index]=>throw new InvalidOperationException("Untrusted list indexer was used.");
        public IEnumerator<T> GetEnumerator(){for(int i=0;i<finiteBudget;i++)yield return item;throw new InvalidOperationException("Enumeration exceeded the finite refusal probe.");}
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()=>GetEnumerator();
    }
}
