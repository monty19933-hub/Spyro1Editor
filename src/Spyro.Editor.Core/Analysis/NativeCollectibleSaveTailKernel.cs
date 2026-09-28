using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.Core.Analysis;

/// <summary>
/// Detached fixed-two-ID/slot-zero MIPS preparation, not a card writer or
/// resident allocation. a0=OldTailAddress, a1=100(hex), a2=801FF600, a3=0.
/// Returns v0=0 only after publishing a complete immutable proposal; failure
/// preserves the entire previous output and both inputs. Temporary scratch
/// and declared nested stack may change. A source certificate pins observed
/// native seams, never placement, asynchronous ownership or real I/O.
/// Input/code/live mutation must be excluded during each execution; final
/// comparisons detect changes, but do not establish IRQ/DMA atomicity.
/// </summary>
internal sealed class NativeCollectibleSaveTailKernel
{
    internal const uint LiveAddress = 0x801FF600, PreparedMagic = 0x53565031;
    internal const int TransferBytes = 0x100, EnvelopeBytes = 0x83, PreparedBytes = 0x340,
        PreparedTailOffset = 0x200, PreparedDigestOffset = 0x300, PreparedFlagOffset = 0x320,
        ArenaBytes = 0x3BA0, CodeCapacityBytes = 0x3000, FrameBytes = 0x30,
        MaximumStackBytes = 0xA0, NativeCallerBytes = 0x48;
    private readonly byte[] bytes;
    private readonly byte[]? originalExecutable;
    internal sealed record SourceSpan(uint Pc,int ByteLength,string Sha256);
    internal static IReadOnlyList<SourceSpan> SourceSpans { get; } = Array.AsReadOnly(new[] {
        new SourceSpan(0x800320C0,0x278,"5fe4e29fa389cfcde8f45d21b8235a38a5faf7e5efbdfde295285861827efa6c"),
        new SourceSpan(0x800321FC,0x3C,"5b10b96c51df659639cfa1f76e2842c18acbf8b6202127a97aaec83b0dc95af6"),
        new SourceSpan(0x80032314,0x18,"1742cd0f5996ac7606b2eecbba2fd2d7d74c0bf2f36238b923e3eeee87f4fdcc"),
        new SourceSpan(0x800324B8,0x28,"66761f9b5081320553a85d9dd65d250162a25590f3745c8b7f8842e93a7d8072"),
        new SourceSpan(0x80059864,0x1E4,"b8674b556d8f4154886306731ee50c9812f2cac1d9a224756c110be693d19056"),
        new SourceSpan(0x80066E28,0x10C,"87ba1be6dead52b5b297e0587aad7c37f99b5676e497988c0846859e802f9b6d"),
        new SourceSpan(0x800670E4,0x10C,"791708cc4c2bb40cdef08c7fdd0a4c8981abd9ede32105b305f25074c1bc868a"),
        new SourceSpan(0x80067628,0xF0,"19553c82e1ac2ff3a3089e3c8b101ade63a08c9620216785f79df4a2d2da3e21") });
    private NativeCollectibleSaveTailKernel(uint arena,byte[]? executable)
    {
        // These are detached arithmetic ranges, not discovered free RAM.
        if (arena % 16 != 0 || arena < 0x80090000 || (ulong)arena+ArenaBytes > 0x801F0000)
            throw new InvalidDataException("Detached preparation arena must be aligned and disjoint from native globals/live/stack.");
        ArenaAddress = arena; originalExecutable = executable?.ToArray();
        var domain = new NativeAddressSpace(NativeAddressKind.LevelData,"detached-fixed-collectible-save-tail",NativeAddressRevision.Final,ArenaBytes);
        var manifest = NativeCollectibleCardLoadResearchComposition.FixedManifest();
        Sha256 = NativeSha256Kernel.Compile(new(domain,0x1000,0x800),arena+0x1000,StageAddress,TransferBytes,DigestAddress,WorkspaceAddress);
        Loader = NativeCollectibleEnvelopeLoadKernel.Compile(manifest,0,new(domain,0x1800,0x1000),arena+0x1800,ShadowAddress,ContextAddress,Sha256);
        Query = NativeCollectibleStateQueryKernel.Compile(manifest,0,new(domain,0x2800,0x800),arena+0x2800,LiveAddress);
        bytes = Emit();
        if (bytes.Length > 0x1000 || FrameBytes+Loader.MaximumStackByteLength != MaximumStackBytes)
            throw new InvalidDataException("Preparation code or nested-stack capacity exhausted.");
    }
    internal uint ArenaAddress { get; }
    internal uint EntryPc => ArenaAddress;
    internal uint OldTailAddress => ArenaAddress+0x3000;
    internal uint StageAddress => ArenaAddress+0x3100;
    internal uint OutputAddress => ArenaAddress+0x3200;
    internal uint ShadowAddress => ArenaAddress+0x3540;
    internal uint SnapshotAddress => ArenaAddress+0x3740;
    internal uint OldSnapshotAddress => ArenaAddress+0x3940;
    internal uint ContextAddress => ArenaAddress+0x3A40;
    internal uint DigestAddress => ArenaAddress+0x3A50;
    internal uint WorkspaceAddress => ArenaAddress+0x3A70;
    internal NativeSha256Kernel Sha256 { get; }
    internal NativeCollectibleEnvelopeLoadKernel Loader { get; }
    internal NativeCollectibleStateQueryKernel Query { get; }
    internal int CodeByteLength => bytes.Length;
    internal bool SourceCertified => originalExecutable is not null;
    internal bool CardWrites => false;
    internal bool RuntimeVerified => false;
    internal bool NativeResidenceIssued => false;
    internal bool PublicationApproved => false;
    internal bool NormalCreateBinApproved => false;
    internal byte[] CopyBytes() => bytes.ToArray();
    internal static NativeCollectibleSaveTailKernel CreateFixture(uint arena) => new(arena,null);
    internal static NativeCollectibleSaveTailKernel Capture(NativeRegionalSourceSession source,uint arena)
    {
        ArgumentNullException.ThrowIfNull(source);
        try
        {
            if (source.Revision != NativeDiscRevision.UsaScus94228) throw new InvalidDataException("Exact original USA required.");
            byte[] exe = source.ReadExecutableRange(0,0x66000);
            if (Hash(exe) != "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998")
                throw new InvalidDataException("Complete original save executable changed.");
            foreach (var span in SourceSpans)
                if (Hash(exe.AsSpan((int)(span.Pc-0x8000F800),span.ByteLength)) != span.Sha256)
                    throw new InvalidDataException("Original save seam changed at "+span.Pc.ToString("X8"));
            return new(arena,exe);
        }
        finally { source.VerifyUnchanged(); }
    }
    internal void RequireSourceEvidence()
    { if (originalExecutable is null) throw new InvalidDataException("Detached fixtures cannot issue source save-seam evidence."); }
    private static string Hash(ReadOnlySpan<byte> value) => Convert.ToHexStringLower(SHA256.HashData(value));
    private byte[] Emit()
    {
        var e = new Emitter(); const int v0=2,a0=4,a1=5,a2=6,a3=7,t0=8,t1=9,t2=10,t3=11,t4=12,sp=29,ra=31;
        void Expected(int r,uint value,string fail) { e.C(t1,value); e.B(5,r,t1,fail); }
        void Read(int r,uint address,int width=0x23) { e.C(t4,address); e.L(width,r,t4,0); }
        void Expect(uint address,uint value,string fail,int width=0x23) { Read(t0,address,width); Expected(t0,value,fail); }
        void Selection(string fail)
        {
            Expect(0x800755A4,0xA00,fail); Expect(0x80078D14,1,fail); Expect(0x80078D1C,0,fail);
            Expect(0x80075B50,0,fail); Expect(0x80075B58,0,fail);
            Read(t0,LiveAddress+0x18,0x24); e.I(11,t0,t1,2); e.B(4,t1,0,fail);
            e.R(0,0,t0,t0,4); Read(t2,0x80078D18); e.B(5,t0,t2,fail);
            Read(t0,LiveAddress+0x1A,0x25); Read(t2,0x80078D20); e.B(5,t0,t2,fail);
            Read(t0,LiveAddress+0x8C); e.B(4,t0,0,fail);
        }
        void Copy(uint from,uint to,int length)
        {
            string loop=e.Unique(); e.C(t0,from); e.C(t1,to); e.C(t2,from+(uint)length); e.Label(loop);
            e.L(0x24,t3,t0,0); e.I(0x28,t1,t3,0); e.I(9,t0,t0,1); e.I(9,t1,t1,1); e.B(5,t0,t2,loop);
        }
        void Compare(uint first,uint second,int length,string fail)
        {
            string loop=e.Unique(); e.C(t0,first); e.C(t1,second); e.C(t2,first+(uint)length); e.Label(loop);
            e.L(0x24,t3,t0,0); e.L(0x24,t4,t1,0); e.B(5,t3,t4,fail);
            e.I(9,t0,t0,1); e.I(9,t1,t1,1); e.B(5,t0,t2,loop);
        }
        void Hash(int length)
        { e.C(a0,StageAddress); e.C(a1,(uint)length); e.Call(Sha256.EntryPc); e.B(5,v0,0,"failure"); }
        Expected(a0,OldTailAddress,"arguments"); Expected(a1,TransferBytes,"arguments"); Expected(a2,LiveAddress,"arguments"); Expected(a3,0,"arguments");
        e.I(12,sp,t0,7); e.B(5,t0,0,"arguments");
        e.C(t0,0x801FF800+MaximumStackBytes); e.R(0x2B,sp,t0,t1); e.B(5,t1,0,"arguments");
        e.C(t0,0x80200000-NativeCallerBytes); e.R(0x2B,t0,sp,t1); e.B(5,t1,0,"arguments");
        Selection("arguments"); Expect(LiveAddress+0x19,1,"arguments",0x24);
        Read(t0,LiveAddress+0x7C); e.B(4,t0,0,"arguments"); Read(t2,LiveAddress+0x80); e.R(0x2B,t0,t2,t1); e.B(5,t1,0,"arguments");
        // Complete fixed-profile reserved tail, not an assumed scratch owner.
        e.C(t0,LiveAddress+0xB0); e.C(t1,LiveAddress+0x200); e.Label("reserved");
        e.L(0x23,t2,t0,0); e.B(5,t2,0,"arguments"); e.I(9,t0,t0,4); e.B(5,t0,t1,"reserved");
        e.I(9,sp,sp,-FrameBytes); int[] saved=[a0,a1,a2,a3,ra];
        for(int i=0;i<saved.Length;i++) e.I(0x2B,sp,saved[i],0x10+i*4);
        e.C(a0,LiveAddress); e.C(a1,0); e.Call(Query.EntryPc); e.I(11,v0,t0,2); e.B(4,t0,0,"failure");
        Copy(LiveAddress,SnapshotAddress,512); Copy(OldTailAddress,OldSnapshotAddress,TransferBytes); Copy(OldSnapshotAddress,StageAddress,TransferBytes);
        e.C(t0,ContextAddress); e.I(0x2B,t0,0,0);
        Read(t1,SnapshotAddress+0x18,0x24); e.I(0x2B,t0,t1,4);
        Read(t1,SnapshotAddress+0x1A,0x25); e.I(0x2B,t0,t1,8);
        Read(t1,SnapshotAddress+0x8C); e.I(0x2B,t0,t1,12);
        e.C(a0,StageAddress); e.C(a1,EnvelopeBytes); e.C(a2,ContextAddress); e.Call(Loader.EntryPc); e.B(5,v0,0,"failure");
        Compare(ShadowAddress+0x50,SnapshotAddress+0x50,32,"failure");
        Read(t0,ShadowAddress+0x90,0x24); Read(t1,SnapshotAddress+0x90,0x24); e.R(0x24,t0,t1,t2); e.B(5,t0,t2,"failure");
        e.C(t0,StageAddress); e.I(0x28,t0,t1,0x60); Hash(0x63); Copy(DigestAddress,StageAddress+0x63,32); Hash(EnvelopeBytes);
        Compare(OldTailAddress,OldSnapshotAddress,TransferBytes,"failure");
        Compare(LiveAddress+0x10,SnapshotAddress+0x10,0x1F0,"failure"); Selection("failure");
        // Every refusal precedes output writes. Publish marker is last; neither
        // this record nor its marker alone is a native I/O ownership token.
        e.C(t0,OutputAddress+PreparedFlagOffset); e.I(0x2B,t0,0,0);
        e.C(t0,OutputAddress); e.C(t1,OutputAddress+PreparedBytes); e.Label("clear-output");
        e.I(0x2B,t0,0,0); e.I(9,t0,t0,4); e.B(5,t0,t1,"clear-output");
        Copy(SnapshotAddress,OutputAddress,512); Copy(StageAddress,OutputAddress+PreparedTailOffset,TransferBytes);
        Copy(DigestAddress,OutputAddress+PreparedDigestOffset,32); e.C(t0,OutputAddress+PreparedFlagOffset); e.C(t1,PreparedMagic); e.I(0x2B,t0,t1,0);
        e.C(v0,0); e.B(4,0,0,"restore"); e.Label("failure"); e.C(v0,2); e.Label("restore");
        for(int i=0;i<saved.Length;i++) e.L(0x23,saved[i],sp,0x10+i*4);
        e.I(9,sp,sp,FrameBytes); e.R(8,ra,0,0); e.W(0);
        e.Label("arguments"); e.C(v0,1); e.R(8,ra,0,0); e.W(0); return e.Finish();
    }
    private sealed class Emitter
    {
        private readonly List<uint> words=[]; private readonly Dictionary<string,int> labels=[]; private readonly List<(int,string)> branches=[];
        internal string Unique()=>"loop"+words.Count;
        internal void W(uint value)=>words.Add(value);
        internal void I(int op,int rs,int rt,int imm)=>W((uint)(op<<26|rs<<21|rt<<16)|(ushort)imm);
        internal void R(int fn,int rs,int rt,int rd,int sh=0)=>W((uint)(rs<<21|rt<<16|rd<<11|sh<<6|fn));
        internal void C(int r,uint v){I(15,0,r,(int)(v>>16));I(13,r,r,(int)(v&65535));}
        internal void L(int op,int r,int p,int at){I(op,p,r,at);W(0);}
        internal void Label(string id)=>labels.Add(id,words.Count);
        internal void B(int op,int a,int b,string id){branches.Add((words.Count,id));I(op,a,b,0);W(0);}
        internal void Call(uint pc){W(0x0C000000|((pc>>2)&0x3FFFFFF));W(0);}
        internal byte[] Finish()
        {
            foreach(var (at,id) in branches){int delta=labels[id]-at-1;if(delta<short.MinValue||delta>short.MaxValue)throw new InvalidDataException("Preparation branch overflow.");words[at]|=(ushort)(short)delta;}
            byte[] result=new byte[words.Count*4];for(int i=0;i<words.Count;i++)BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(i*4),words[i]);return result;
        }
    }
}
