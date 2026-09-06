using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;

internal static class PackedEntryCases
{
    internal static int Run()
    {
        int checks=0;
        byte[] header=new byte[0x800],a=Enumerable.Repeat((byte)1,0x800).ToArray(),b=Enumerable.Repeat((byte)2,0x800).ToArray(),c=Enumerable.Repeat((byte)3,0x800).ToArray();
        Put(header,0,0x800);Put(header,4,0x800);Put(header,13*8,0x1000);Put(header,13*8+4,0x800);Put(header,14*8,0x1800);Put(header,14*8+4,0x800);
        byte[] Read(int offset,int length)=>offset switch{0x800=>a.ToArray(),0x1000=>b.ToArray(),0x1800=>c.ToArray(),_=>throw new InvalidDataException("unknown source owner")};
        var overlay=new NativeWizardResearchReplacement(13,0x1000,0x800,Hash(b),Enumerable.Repeat((byte)4,0x1000).ToArray());
        var data=new NativeWizardResearchReplacement(14,0x1800,0x800,Hash(c),Enumerable.Repeat((byte)5,0x1800).ToArray());
        var packed=NativeWizardResearchPackedEntryComposer.Compose(header,0x2000,[overlay,data],Read);
        Check(packed.Bytes.Length==0x3800&&packed.Bytes.AsSpan(0x800,0x800).SequenceEqual(a),"complete unrelated owner complement");
        Check(packed.Bytes.AsSpan(0x1000,0x1000).SequenceEqual(overlay.Bytes)&&packed.Bytes.AsSpan(0x2000,0x1800).SequenceEqual(data.Bytes),"two complete installed owners");
        Check(packed.Entries.Count==3&&packed.Entries.Single(e=>e.Index==14).FinalOffset==0x2000,"derived packed entry remap");
        Check(packed.Bytes.SequenceEqual(NativeWizardResearchPackedEntryComposer.Compose(header,0x2000,[data,overlay],Read).Bytes),"request order independence");
        Check(BitConverter.ToInt32(header,14*8)==0x1800,"source header unchanged");
        Reject(()=>NativeWizardResearchPackedEntryComposer.Compose(header,0x2000,[overlay],Read),"exactly");
        Reject(()=>NativeWizardResearchPackedEntryComposer.Compose(header,0x2000,[overlay,overlay],Read),"duplicate");
        Reject(()=>NativeWizardResearchPackedEntryComposer.Compose(header,0x2000,[overlay with{Index=12},data],Read),"owner");
        Reject(()=>NativeWizardResearchPackedEntryComposer.Compose(header,0x2000,[overlay with{OriginalOffset=0x800},data],Read),"preimage");
        Reject(()=>NativeWizardResearchPackedEntryComposer.Compose(header,0x2000,[overlay with{OriginalSha256=Hash(a)},data],Read),"preimage");
        Reject(()=>NativeWizardResearchPackedEntryComposer.Compose(header,0x2000,[overlay with{Bytes=new byte[0x801]},data],Read),"alignment");
        Reject(()=>NativeWizardResearchPackedEntryComposer.Compose(header,0x2000,[overlay,data],(o,n)=>new byte[n+1]),"length");
        Reject(()=>NativeWizardResearchPackedEntryComposer.Compose(header,0x2000,new[]{overlay,data,overlay},Read),"budget");
        byte[] bad=header.ToArray();Put(bad,13*8,0x800);
        Reject(()=>NativeWizardResearchPackedEntryComposer.Compose(bad,0x2000,[overlay,data],Read),"packed");
        bad=header.ToArray();Put(bad,14*8+4,0x7FFFFFFF);
        Reject(()=>NativeWizardResearchPackedEntryComposer.Compose(bad,0x2000,[overlay,data],Read),"owner");
        Reject(()=>NativeWizardResearchPackedEntryComposer.Compose(header[..^1],0x2000,[overlay,data],Read),"header");
        Reject(()=>NativeWizardResearchPackedEntryComposer.Compose(header,int.MaxValue,[overlay,data],Read),"budget");
        int calls=0;
        Reject(()=>NativeWizardResearchPackedEntryComposer.Compose(header,0x2000,[overlay with{OriginalLength=0x1000},data],(o,n)=>{calls++;return Read(o,n);}),"preimage");
        Check(calls==0,"invalid actual ownership is refused before source extraction or payload freezing");
        byte[] companion=[1,2,3,4];
        NativeDarkHollowWizardResearchCandidatePublisher.RequireCompanion(new MemoryStream(companion),companion);Check(true,"exact companion accepts");
        Reject(()=>NativeDarkHollowWizardResearchCandidatePublisher.RequireCompanion(new MemoryStream(companion[..^1]),companion),"length");
        Reject(()=>NativeDarkHollowWizardResearchCandidatePublisher.RequireCompanion(new MemoryStream(new byte[5]),companion),"length");
        Reject(()=>NativeDarkHollowWizardResearchCandidatePublisher.RequireCompanion(new MemoryStream(new byte[4]),companion),"companion");
        using(var late=new LateLengthStream(companion))Reject(()=>NativeDarkHollowWizardResearchCandidatePublisher.RequireCompanion(late,companion),"during");
        using(var oversized=new OversizedLengthStream())
        {Reject(()=>NativeDarkHollowWizardResearchCandidatePublisher.RequireCompanion(oversized,companion),"before");Check(oversized.Reads==0,"oversized companion refused before any read/allocation");}
        Console.WriteLine("NATIVE_WIZARD_RESEARCH_PACKED_TWO_OWNER_COMPLEMENT_AND_REFUSALS_OK");return checks;
        void Check(bool value,string label){if(!value)throw new InvalidDataException(label);checks++;}
        void Reject(Action action,string fragment){try{action();}catch(InvalidDataException e)when(e.Message.Contains(fragment,StringComparison.OrdinalIgnoreCase)){checks++;return;}throw new InvalidDataException("Expected refusal: "+fragment);}
    }
    private static void Put(byte[] bytes,int offset,int value)=>BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset),value);
    private static string Hash(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
    private sealed class LateLengthStream(byte[] bytes):MemoryStream(bytes)
    {
        private bool _read;
        public override long Length=>base.Length+(_read?1:0);
        public override int Read(Span<byte> buffer){int count=base.Read(buffer);_read=true;return count;}
    }
    private sealed class OversizedLengthStream:MemoryStream
    {
        internal int Reads;
        public override long Length=>long.MaxValue;
        public override int Read(Span<byte> buffer){Reads++;throw new InvalidOperationException("Must not read oversized companion");}
    }
}
