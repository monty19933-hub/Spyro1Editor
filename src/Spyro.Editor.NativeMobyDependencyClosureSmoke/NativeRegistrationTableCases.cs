using System.Buffers.Binary;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;

internal static class NativeRegistrationTableCases
{
    internal static void Free(Action<bool,string> check)
    {
        byte[] header=Header(2);
        var table=NativeMobyRegistrationTableLayout.Decode(header,0x800,0x2000);
        check(table.Registrations.Select(r=>(r.Slot,r.ActorId,r.RootDataOffset)).SequenceEqual(
            new[]{(0,(ushort)0,0x800u),(1,(ushort)0x21,0x820u)}),"Neutral table preserves player slot and ordered generic roots.");
        byte[] before=header.ToArray();
        var appended=table.Append(0x01F5,0x1800);
        byte[] expected=before.ToArray();Put(expected,0x58,0x1800);U16(expected,0x154,0x01F5);
        check(appended.CopyHeader().SequenceEqual(expected)&&table.CopyHeader().SequenceEqual(before)&&header.SequenceEqual(before),
            "One append changes exactly its zero root/class fields and never mutates its parent header.");
        check(appended.Registrations.Count==3&&appended.Registrations[2].Slot==2&&
            appended.Registrations[2].RootDataOffset==0x1800,"Appended package is registered after all complete existing owners.");
        byte[] copy=appended.CopyHeader();copy[0x58]^=1;
        check(appended.CopyHeader().SequenceEqual(expected),"Header copy cannot change the retained registration table.");
        try{((IList<NativeMobySourceRegistration>)appended.Registrations).Clear();throw new Exception("Expected read-only table.");}
        catch(NotSupportedException){check(true,"Registration order is immutable.");}
        Refuse(()=>table.Append(0,0x1800));Refuse(()=>table.Append(512,0x1800));
        Refuse(()=>table.Append(0x21,0x1800));Refuse(()=>appended.Append(0x1F5,0x1900));
        foreach(uint root in new uint[]{0,0x7FC,0x800,0x820,0x1801,0x2800,0x80000000})Refuse(()=>table.Append(0x1F5,root));
        var full=NativeMobyRegistrationTableLayout.Decode(Header(64),0x800,0x2000);
        check(full.Registrations.Count==64,"All63 native generic slots plus player are represented; no smaller arbitrary slot limit.");
        Refuse(()=>full.Append(0x1F5,0x1800));
        foreach(int length in new[]{0,0x1CF,0x7FF,0x801})Refuse(()=>NativeMobyRegistrationTableLayout.Decode(new byte[length],0x800,0x2000));
        foreach(var (offset,length) in new[]{(-1,0x2000),(0,0x2000),(0x800,0),(int.MaxValue,8),(0x801,0x2000)})
            Refuse(()=>NativeMobyRegistrationTableLayout.Decode(header,offset,length));
        foreach(var mutate in new Action<byte[]>[]{b=>Put(b,0x50,0),b=>U16(b,0x150,1),b=>U16(b,0x152,0),
            b=>U16(b,0x152,512),b=>Put(b,0x54,0x800),b=>Put(b,0x54,0x2800),b=>Put(b,0x54,0x821),
            b=>Put(b,0x54,0x80000000),b=>U16(b,0x154,1),b=>{Put(b,0x5C,0x900);U16(b,0x156,0x26);}})
        {byte[] bad=header.ToArray();mutate(bad);byte[] retained=bad.ToArray();Refuse(()=>NativeMobyRegistrationTableLayout.Decode(bad,0x800,0x2000));check(bad.SequenceEqual(retained),"Malformed registration preimage remains unchanged.");}
        byte[] duplicate=Header(3);U16(duplicate,0x154,0x21);Refuse(()=>NativeMobyRegistrationTableLayout.Decode(duplicate,0x800,0x2000));
        check(table.CopyHeader().SequenceEqual(before),"Every failed append leaves the prior table usable and exact.");
        void Refuse(Action action)
        {try{action();}catch(InvalidDataException){check(true,"Expected neutral registration refusal.");return;}throw new Exception("Expected registration refusal.");}
    }

    private static byte[] Header(int count)
    {
        var bytes=new byte[0x800];
        // Non-table header bytes are opaque to this codec and must survive.
        for(int i=0;i<0x50;i++)bytes[i]=(byte)(i*13);
        for(int i=0;i<count;i++){Put(bytes,0x50+4*i,(uint)(0x800+0x20*i));U16(bytes,0x150+2*i,(ushort)(i==0?0:0x20+i));}
        bytes[0x7FF]=0xA5;return bytes;
    }
    private static void Put(byte[] bytes,int at,uint value)=>BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at),value);
    private static void U16(byte[] bytes,int at,ushort value)=>BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(at),value);
}
