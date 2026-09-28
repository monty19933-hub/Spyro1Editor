using System.Buffers.Binary;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;
using Abi = Spyro.Editor.Core.Analysis.NativeCollectibleSelectedSlotResidence;

namespace Spyro.Editor.Core.Analysis;

internal enum NativeCollectibleEnvelopeLoadResult : uint
{ Prepared = 0, InvalidArguments = 1, HeaderMismatch = 2, InvalidBits = 3, ChecksumMismatch = 4, HashRefused = 5 }

/// <summary>
/// Bounded MIPS-I validation of the existing save envelope followed by preparation of
/// a separate 512-byte shadow image. No caller-supplied checksum/count/valid flag
/// is trusted. a0=exact input, a1=exact envelope length, a2=exact context address;
/// context words are ordinary slot, controller index, u16 marker, opaque epoch.
/// Bounds are not native selected-save authentication. All input/context/output,
/// scratch/code and the complete nested stack must be exclusively caller-owned.
/// The live proposed ledger is explicitly excluded, including from stack writes.
/// No card I/O, native Ready, installation, concurrency or lifetime is issued.
/// Preserves a0-a3, s0-s7, k0/k1, gp/fp/sp/ra and HI/LO; other caller GPRs clobber.
/// </summary>
internal sealed class NativeCollectibleEnvelopeLoadKernel
{
    private readonly byte[] bytes;
    private NativeCollectibleEnvelopeLoadKernel(byte[] bytes, NativeByteExtent owner, uint pc,
        DetachedCollectibleManifest manifest, int slot, int envelopeBytes, uint shadow, uint context, NativeSha256Kernel hash)
    {
        this.bytes=bytes.ToArray(); Owner=owner; EntryPc=pc; ManifestSha256=manifest.Sha256;
        OrdinarySlot=slot; EnvelopeByteLength=envelopeBytes; ShadowAddress=shadow; SelectionContextAddress=context; Sha256=hash;
    }
    internal NativeByteExtent Owner { get; }
    internal uint EntryPc { get; }
    internal string ManifestSha256 { get; }
    internal int OrdinarySlot { get; }
    internal int EnvelopeByteLength { get; }
    internal uint ShadowAddress { get; }
    internal uint SelectionContextAddress { get; }
    internal NativeSha256Kernel Sha256 { get; }
    internal int StackByteLength => 0x30;
    internal int MaximumStackByteLength => StackByteLength + Sha256.StackByteLength;
    internal bool NativeResidenceIssued => false;
    internal bool NativeBindingValidated => false;
    internal bool CardIoApproved => false;
    internal bool PublicationApproved => false;
    internal byte[] CopyBytes() => bytes.ToArray();

    internal static NativeCollectibleEnvelopeLoadKernel Compile(DetachedCollectibleManifest manifest, int ordinarySlot,
        NativeByteExtent capacity, uint codeAddress, uint shadowAddress, uint contextAddress, NativeSha256Kernel hash)
    {
        ArgumentNullException.ThrowIfNull(manifest); ArgumentNullException.ThrowIfNull(capacity); ArgumentNullException.ThrowIfNull(hash);
        var residence=Abi.Describe(manifest,ordinarySlot);
        byte[] emptyEnvelope=DetachedCollectibleSaveCodec.Serialize(DetachedCollectibleSave.CreateNew(manifest));
        int length=emptyEnvelope.Length, body=length-32, raw=residence.Sizing.RawBitmapBytes;
        if(capacity.Space.Kind!=NativeAddressKind.LevelData||capacity.Space.Revision!=NativeAddressRevision.Final||
            capacity.Offset%4!=0||capacity.ByteLength<=0||capacity.ByteLength>0x10000||capacity.ByteLength%4!=0||codeAddress%4!=0||
            shadowAddress%16!=0||contextAddress%4!=0||hash.MaximumInputBytes<length||hash.StackByteLength!=64||
            hash.WorkspaceByteLength!=288||hash.Owner.ByteLength!=hash.CopyBytes().Length||
            !ReferenceEquals(capacity.Space,hash.Owner.Space)||
            (long)codeAddress-capacity.Offset!=(long)hash.EntryPc-hash.Owner.Offset)
            throw new InvalidDataException("Envelope loader requires disjoint final code, exact SHA ABI and enough complete input capacity.");

        var ranges=new (uint Start,int Bytes)[] {
            (codeAddress,checked((int)capacity.ByteLength)),(hash.EntryPc,hash.CodeCapacityByteLength),
            (hash.CallerInputAddress,hash.MaximumInputBytes),(shadowAddress,Abi.ReservationBytes),(contextAddress,16),
            (hash.DigestAddress,32),(hash.WorkspaceAddress,hash.WorkspaceByteLength),(residence.ProposedBase,Abi.ReservationBytes) };
        for(int i=0;i<ranges.Length;i++)
        {
            var a=ranges[i];
            if(a.Bytes<=0||a.Start<0x80000000||(ulong)a.Start+(uint)a.Bytes>0x80200000)
                throw new InvalidDataException("Envelope code/data range leaves main RAM.");
            for(int j=0;j<i;j++)
                if((ulong)a.Start<ranges[j].Start+(ulong)ranges[j].Bytes&&(ulong)ranges[j].Start<a.Start+(ulong)a.Bytes)
                    throw new InvalidDataException("Envelope code, input, tuple, shadow, digest, workspace or protected live ledger overlap.");
        }
        // Reuse the canonical codec and established ABI image generator. Only
        // immutable manifest fields/mask become constants; earned state and both
        // checksums always come from executing the native reader below.
        byte[] template=DetachedCollectibleLedgerImage.Hydrate(residence,manifest,emptyEnvelope,0,0,0).CopyBytes();
        template.AsSpan(Abi.EnvelopeDigestOffset,32).Clear(); template[Abi.PhaseOffset]=0;
        var e=new Emitter();
        const int v0=2,a0=4,a1=5,a2=6,t0=8,t1=9,t2=10,t3=11,t4=12,t5=13,t6=14,s0=16,s1=17,t8=24,sp=29,ra=31;
        void Expected(int register,uint value,string failure)
        { e.Constant(t0,value); e.Branch(5,register,t0,failure); }
        Expected(a0,hash.CallerInputAddress,"args"); Expected(a1,(uint)length,"args"); Expected(a2,contextAddress,"args");
        int nested=0x30+hash.StackByteLength;
        e.I(12,sp,t0,7); e.Branch(5,t0,0,"args");
        e.Constant(t0,0x80000000u+(uint)nested); e.R(0x2B,sp,t0,t1); e.Branch(5,t1,0,"args");
        e.Constant(t0,0x80200000); e.R(0x2B,t0,sp,t1); e.Branch(5,t1,0,"args");
        e.I(9,sp,t8,-nested);
        for(int i=0;i<ranges.Length;i++)
        {
            var range=ranges[i]; string next="stack-next-"+i;
            e.Constant(t0,range.Start+(uint)range.Bytes); e.R(0x2B,t8,t0,t1); e.Branch(4,t1,0,next);
            e.Constant(t0,range.Start); e.R(0x2B,t0,sp,t1); e.Branch(5,t1,0,"args"); e.Label(next);
        }
        e.Load(0x23,t1,a2,0); Expected(t1,(uint)ordinarySlot,"args");
        e.Load(0x23,t0,a2,4); e.I(0x0B,t0,t1,2); e.Branch(4,t1,0,"args");
        e.Load(0x23,t0,a2,8); e.Constant(t1,0x10000); e.R(0x2B,t0,t1,t0); e.Branch(4,t0,0,"args");
        for(int at=0;at<DetachedCollectibleSaveCodec.HeaderBytes;at+=4)
        {
            e.Load(0x23,t1,a0,at); Expected(t1,BinaryPrimitives.ReadUInt32LittleEndian(emptyEnvelope.AsSpan(at)),"header");
        }
        int remainder=manifest.LifetimeCount%8;
        if(remainder!=0) for(int slot=0;slot<DetachedCollectibleSave.OrdinarySlotCount;slot++)
        {
            e.Load(0x24,t0,a0,DetachedCollectibleSaveCodec.HeaderBytes+(slot+1)*raw-1);
            e.I(12,t0,t0,0xFF&~((1<<remainder)-1)); e.Branch(5,t0,0,"bits");
        }

        e.I(9,sp,sp,-0x30);
        int[] saved=[a0,a1,a2,7,s0,s1,ra];
        for(int i=0;i<saved.Length;i++) e.I(0x2B,sp,saved[i],0x10+i*4);
        e.R(0x21,a0,0,s0); e.R(0x21,a2,0,s1);
        void Hash(int count)
        {
            e.R(0x21,s0,0,a0); e.Constant(a1,(uint)count);
            e.Word(0x0C000000|((hash.EntryPc>>2)&0x3FFFFFF)); e.Word(0); e.Branch(5,v0,0,"hash-failed");
        }
        Hash(body);
        e.Constant(t2,hash.DigestAddress);
        for(int i=0;i<32;i++)
        {
            e.Load(0x24,t0,s0,body+i); e.Load(0x24,t1,t2,i); e.Branch(5,t0,t1,"checksum");
        }
        Hash(length);
        // Nothing can refuse after this point. Invalidate shadow phase first,
        // clear its entire owner, fill it, then publish its prepared byte last.
        // This is never the live reservation, and is not a native admission hook.
        e.Constant(t8,shadowAddress); e.I(0x28,t8,0,Abi.PhaseOffset);
        e.Constant(t0,shadowAddress); e.Constant(t1,shadowAddress+Abi.ReservationBytes); e.Label("clear-shadow");
        e.I(0x2B,t0,0,0); e.I(9,t0,t0,4); e.Branch(5,t0,t1,"clear-shadow");
        for(int at=0;at<template.Length;at+=4)
        {
            uint value=BinaryPrimitives.ReadUInt32LittleEndian(template.AsSpan(at));
            if(value!=0) { e.Constant(t0,value); e.I(0x2B,t8,t0,at); }
        }
        e.Constant(t2,hash.DigestAddress);
        for(int at=0;at<32;at+=4) { e.Load(0x23,t0,t2,at); e.I(0x2B,t8,t0,Abi.EnvelopeDigestOffset+at); }
        e.Load(0x23,t0,s1,4); e.I(0x28,t8,t0,Abi.ControllerOffset);
        e.Load(0x23,t0,s1,8); e.I(0x29,t8,t0,Abi.NativeFileOffset);
        e.Load(0x23,t0,s1,12); e.I(0x2B,t8,t0,Abi.SessionEpochOffset);
        e.Constant(t2,hash.CallerInputAddress+(uint)(DetachedCollectibleSaveCodec.HeaderBytes+ordinarySlot*raw));
        e.Constant(t3,shadowAddress+Abi.BitmapOffset); e.Constant(t4,shadowAddress+(uint)residence.ActiveMaskOffset);
        e.Constant(t5,hash.CallerInputAddress+(uint)(DetachedCollectibleSaveCodec.HeaderBytes+(ordinarySlot+1)*raw)); e.Constant(t6,0);
        e.Label("copy-bits"); e.Load(0x24,t0,t2,0); e.I(0x28,t3,t0,0); e.Load(0x24,t1,t4,0); e.R(0x24,t0,t1,t0);
        e.Label("count-bits"); e.Branch(4,t0,0,"next-byte"); e.I(9,t0,t1,-1); e.R(0x24,t0,t1,t0); e.I(9,t6,t6,1); e.Branch(4,0,0,"count-bits");
        e.Label("next-byte"); e.I(9,t2,t2,1); e.I(9,t3,t3,1); e.I(9,t4,t4,1); e.Branch(5,t2,t5,"copy-bits");
        e.I(0x29,t8,t6,Abi.EarnedActiveCountOffset); e.Constant(t0,Abi.ReadyPhase); e.I(0x28,t8,t0,Abi.PhaseOffset);
        e.Constant(v0,0); e.Branch(4,0,0,"restore");
        e.Label("checksum"); e.Constant(v0,(uint)NativeCollectibleEnvelopeLoadResult.ChecksumMismatch); e.Branch(4,0,0,"restore");
        e.Label("hash-failed"); e.Constant(v0,(uint)NativeCollectibleEnvelopeLoadResult.HashRefused);
        e.Label("restore");
        for(int i=0;i<saved.Length;i++) e.Load(0x23,saved[i],sp,0x10+i*4);
        e.I(9,sp,sp,0x30); e.R(8,ra,0,0); e.Word(0);
        foreach(var failure in new[]{("args",NativeCollectibleEnvelopeLoadResult.InvalidArguments),("header",NativeCollectibleEnvelopeLoadResult.HeaderMismatch),("bits",NativeCollectibleEnvelopeLoadResult.InvalidBits)})
        { e.Label(failure.Item1); e.Constant(v0,(uint)failure.Item2); e.R(8,ra,0,0); e.Word(0); }
        byte[] code=e.Finish();
        if(code.Length>capacity.ByteLength) throw new InvalidDataException("Envelope loader exceeds its complete code capacity.");
        return new(code,new(capacity.Space,capacity.Offset,code.Length),codeAddress,manifest,ordinarySlot,length,shadowAddress,contextAddress,hash);
    }

    private sealed class Emitter
    {
        private readonly List<uint> words=[];
        private readonly Dictionary<string,int> labels=new(StringComparer.Ordinal);
        private readonly List<(int At,string Label)> branches=[];
        internal void Word(uint word)=>words.Add(word);
        internal void R(int fn,int rs,int rt,int rd,int shift=0)=>Word((uint)(rs<<21|rt<<16|rd<<11|shift<<6|fn));
        internal void I(int op,int rs,int rt,int value)=>Word((uint)(op<<26|rs<<21|rt<<16)|(ushort)value);
        internal void Constant(int r,uint value) { I(15,0,r,(int)(value>>16)); I(13,r,r,(int)(value&0xFFFF)); }
        internal void Load(int op,int r,int pointer,int offset) { I(op,pointer,r,offset); Word(0); }
        internal void Label(string label)=>labels.Add(label,words.Count);
        internal void Branch(int op,int rs,int rt,string label) { branches.Add((words.Count,label)); I(op,rs,rt,0); Word(0); }
        internal byte[] Finish()
        {
            foreach(var b in branches)
            {
                int displacement=labels[b.Label]-b.At-1;
                if(displacement<short.MinValue||displacement>short.MaxValue) throw new InvalidDataException("Envelope branch exceeds MIPS-I displacement.");
                words[b.At]|=(ushort)(short)displacement;
            }
            byte[] result=new byte[words.Count*4];
            for(int i=0;i<words.Count;i++) BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(i*4),words[i]);
            return result;
        }
    }
}
