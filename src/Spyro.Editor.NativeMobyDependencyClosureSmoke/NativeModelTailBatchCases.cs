using System.Buffers.Binary;
using System.Collections;
using System.Reflection;
using Spyro.Editor.Core.Exporting;

internal static class NativeModelTailBatchCases
{
    // Neutral API contract: immutable package inputs, ordered first-occurrence
    // sharing by actual input reference, and one complete current-body append.
    // No source factory, Original domain, graph authority or runtime admission.
    internal static void Run(Action<bool,string> check)
    {
        byte[] current=Fixture(2),before=current.ToArray();
        byte[] aBytes=Bytes(0x174,0x31),bBytes=Bytes(0x1008,0x72);
        var a=new NativeModelTailPackage(300,"z-first",aBytes);
        var b=new NativeModelTailPackage(200,"a-second",bBytes);
        var batch=NativeModelTailBatch.Compile(current,[a,b,a],[450]);
        byte[] append=[..aBytes,..bBytes];
        byte[] expected=Expected(before,[(300,aBytes),(200,bBytes)]);
        check(batch.ModelGrowthBytes==0x1800&&batch.FinalModelLength==0x2800&&
            batch.CopyAppendBytes().SequenceEqual(append)&&batch.CopyAppendBytes().Length==0x117C,
            "A model batch concatenates exact four-byte-aligned packages and sector-rounds the aggregate once, not each package.");
        check(batch.Packages.Count==2&&ReferenceEquals(batch.Packages[0].Input,a)&&ReferenceEquals(batch.Packages[1].Input,b)&&
            batch.Packages.Select(p=>p.Slot).SequenceEqual(new[]{3,4})&&
            batch.Packages.Select(p=>p.PackageOffset).SequenceEqual(new[]{0x2800,0x2974})&&
            batch.Packages.Select(p=>p.ByteLength).SequenceEqual(new[]{0x174,0x1008})&&
            batch.Packages.Select(p=>p.RootFieldOffset).SequenceEqual(new[]{0x5C,0x60})&&
            batch.Packages.Select(p=>p.ClassFieldOffset).SequenceEqual(new[]{0x156,0x158}),
            "Ordered distinct package owners append after the entire current model allocation and own consecutive new root/class fields.");
        byte[] output=batch.CopyFinalBody();
        check(output.SequenceEqual(expected)&&output.Length==0x5000&&
            output.AsSpan(0x1800,0x1000).SequenceEqual(before.AsSpan(0x1800,0x1000))&&
            !output.AsSpan(0x397C,0x684).ContainsAnyExcept((byte)0),
            "The batch really installs both complete packages, preserves every old model byte including old zero padding, and owns new zero alignment.");
        var table=NativeMobyRegistrationTableLayout.Decode(output.AsSpan(0,0x800),0x1800,batch.FinalModelLength);
        check(table.Registrations.Select(r=>r.ActorId).SequenceEqual(new ushort[]{0,1,2,300,200})&&
            Word(output,0x18)==0x4000&&Word(output,0x20)==0x4800&&
            output.AsSpan(0x4000).SequenceEqual(before.AsSpan(0x2800)),
            "New roots follow request order while complete scene/auxiliary owners shift without losing their bytes.");
        int[] headerChanges=Enumerable.Range(0x14,4).Concat(Enumerable.Range(0x18,4)).Concat(Enumerable.Range(0x20,4))
            .Concat(batch.Packages.SelectMany(p=>Enumerable.Range(p.RootFieldOffset,4).Concat(Enumerable.Range(p.ClassFieldOffset,2)))).ToArray();
        check(Enumerable.Range(0,0x800).Where(i=>!headerChanges.Contains(i)).All(i=>output[i]==before[i]),
            "Only decoded model length, later subfile offsets and all new root/class fields may change the complete header.");
        batch.RequireExactCurrentBody(before);
        check(batch.RevertExact(output).SequenceEqual(before)&&current.SequenceEqual(before)&&
            batch.RevertExact(output.ToArray()).SequenceEqual(before),
            "Exact inverse removes every appended registration/package and recovers the complete current body without mutating inputs.");

        var repeat=NativeModelTailBatch.Compile(before,[a,a,b,a,b],[450]);
        check(repeat.CopyFinalBody().SequenceEqual(expected)&&repeat.CopyAppendBytes().SequenceEqual(append)&&
            repeat.Packages.Count==2&&repeat.Packages.Select(p=>p.Input).SequenceEqual(new[]{a,b}),
            "Retries of the same actual package inputs share once and retain first-occurrence order.");
        var reversed=NativeModelTailBatch.Compile(before,[b,a],[450]);
        check(ReferenceEquals(reversed.Packages[0].Input,b)&&reversed.Packages[0].PackageOffset==0x2800&&
            reversed.Packages[1].PackageOffset==0x3808&&reversed.CopyFinalBody().SequenceEqual(Expected(before,[(200,bBytes),(300,aBytes)])),
            "A different explicit package order is honored, not silently class- or label-sorted.");
        var empty=NativeModelTailBatch.Compile(before,[],[450]);
        check(empty.Packages.Count==0&&empty.ModelGrowthBytes==0&&empty.FinalModelLength==0x1000&&
            empty.CopyAppendBytes().Length==0&&empty.CopyFinalBody().SequenceEqual(before)&&empty.RevertExact(before).SequenceEqual(before),
            "The empty model batch is an exact no-op for the already retained Wizard body, with no invented registration.");

        // Two complete inputs can contain identical payload bytes without
        // sharing a model registration. Class ownership, not payload equality,
        // decides whether these are two independently allocated resources.
        var sameBytesOtherClass=new NativeModelTailPackage(201,"other-class",aBytes);
        var distinct=NativeModelTailBatch.Compile(before,[a,sameBytesOtherClass],[450]);
        check(distinct.Packages.Count==2&&distinct.Packages[1].PackageOffset==0x2974&&
            distinct.CopyAppendBytes().SequenceEqual(aBytes.Concat(aBytes)),
            "Equal package bytes from distinct classes remain two model allocations.");
        var reissued=new NativeModelTailPackage(300,"z-first",aBytes);
        check(!ReferenceEquals(a,reissued)&&a.CopyBytes().SequenceEqual(reissued.CopyBytes()),
            "The contradictory-owner probe uses a genuinely equal reissued package input.");
        Refuse(()=>NativeModelTailBatch.Compile(before,[a,reissued],[450]),check);
        Refuse(()=>NativeModelTailBatch.Compile(before,[a,new NativeModelTailPackage(300,"another-id",aBytes)],[450]),check);
        Refuse(()=>NativeModelTailBatch.Compile(before,[a,new NativeModelTailPackage(201,"z-first",aBytes)],[450]),check);
        Refuse(()=>NativeModelTailBatch.Compile(before,[new NativeModelTailPackage(1,"already-resident",aBytes)],[450]),check);
        Refuse(()=>NativeModelTailBatch.Compile(before,[a],[300]),check);
        Refuse(()=>NativeModelTailBatch.Compile(before,[a],[1]),check);
        Refuse(()=>NativeModelTailBatch.Compile(before,[],[1]),check);

        var full=NativeModelTailBatch.Compile(Fixture(61),[a,b],[450]);
        check(full.Packages.Select(p=>p.Slot).SequenceEqual(new[]{62,63})&&
            NativeMobyRegistrationTableLayout.Decode(full.CopyFinalBody().AsSpan(0,0x800),0x1800,full.FinalModelLength).Registrations.Count==64,
            "All63 generic slots plus the retained special player are available; batch accounting has no smaller arbitrary capacity.");
        Refuse(()=>NativeModelTailBatch.Compile(Fixture(62),[a,b],[450]),check);
        Refuse(()=>NativeModelTailBatch.Compile(Fixture(63),[a],[450]),check);
        check(NativeModelTailBatch.Compile(Fixture(63),[],[450]).Packages.Count==0,
            "A full registration table can be retained without appending or reclaiming an unplaced model.");

        var requests=new BoundedProbe<NativeModelTailPackage>(a,4097);
        Refuse(()=>NativeModelTailBatch.Compile(before,requests,[450]),check);
        check(requests.Yielded==4097&&requests.Enumerations==1&&requests.Disposed,
            "Hostile repeated declarations stop at4097 before deduplication and dispose their enumerator without trusting Count.");
        var shared=new BoundedProbe<ushort>(450,65);
        Refuse(()=>NativeModelTailBatch.Compile(before,[a],shared),check);
        check(shared.Yielded<=65&&shared.Enumerations==1&&shared.Disposed,
            "Hostile shared-class enumeration is bounded and disposed, even when its repeated values would otherwise deduplicate.");
        Refuse<ArgumentNullException>(()=>NativeModelTailBatch.Compile(before,null!,[450]),check);
        Refuse<ArgumentNullException>(()=>NativeModelTailBatch.Compile(before,[a],null!),check);
        Refuse(()=>NativeModelTailBatch.Compile(before,[a,null!],[450]),check);
        Refuse(()=>NativeModelTailBatch.Compile(before,[a],[450,450]),check);
        Refuse(()=>NativeModelTailBatch.Compile(before,[a],[512]),check);

        foreach(ushort cls in new ushort[]{0,512,ushort.MaxValue})Refuse(()=>new NativeModelTailPackage(cls,"bad-class",[0,0,0,0]),check);
        foreach(string id in new[]{""," ","bad\nlabel",new string('x',1025)})Refuse(()=>new NativeModelTailPackage(300,id,[0,0,0,0]),check);
        foreach(int length in new[]{0,1,3,5,0x175})Refuse(()=>new NativeModelTailPackage(300,"bad-length",new byte[length]),check);
        // Bounded allocation/overflow probes, never multi-gigabyte allocations.
        var large=new NativeModelTailPackage(301,"large",new byte[0x800000]);
        var small=new NativeModelTailPackage(302,"small",new byte[4]);
        Refuse(()=>NativeModelTailBatch.Compile(before,[large,small],[450]),check);
        Refuse(()=>new NativeModelTailPackage(303,"oversized",new byte[0x800004]),check);
        foreach(int length in new[]{0,0x7FF,0x27FF,before.Length-1,before.Length+1})
            Refuse(()=>NativeModelTailBatch.Compile(new byte[length],[a],[450]),check);
        foreach(var mutate in new Action<byte[]>[]{
            bytes=>Put(bytes,0x10,0x7FFFF800),bytes=>Put(bytes,0x14,0x7FFFF800),
            bytes=>Put(bytes,0x14,0x80000000),bytes=>Put(bytes,0x18,0x2804),
            bytes=>Put(bytes,0x50,0),bytes=>Put(bytes,0x54,0x80000000),
            bytes=>Put(bytes,0x58,0x1804),bytes=>U16(bytes,0x154,1),
            bytes=>U16(bytes,0x156,0x22),bytes=>bytes[0x7FF]=1})
        {
            byte[] bad=before.ToArray();mutate(bad);byte[] retained=bad.ToArray();
            Refuse(()=>NativeModelTailBatch.Compile(bad,[a,b],[450]),check);
            check(bad.SequenceEqual(retained),"Malformed/overflowing current-body geometry is refused without changing caller bytes.");
        }

        int[] corrupt=headerChanges.Concat(new[]{0,0x50,0x150,0x800,0x1000,0x1800,0x1840,0x27FF,
            0x2800,0x2973,0x2974,0x3800,0x397B,0x397C,0x3FFF,0x4000,0x4800,output.Length-1}).Distinct().ToArray();
        foreach(int at in corrupt)
        {byte[] bad=expected.ToArray();bad[at]^=1;Refuse(()=>batch.RevertExact(bad),check);}
        Refuse(()=>batch.RevertExact(before),check);
        Refuse(()=>batch.RevertExact(expected[..^1]),check);
        Refuse(()=>batch.RevertExact([..expected,(byte)0]),check);
        foreach(int at in new[]{0,0x54,0x800,0x1800,0x27FF,before.Length-1})
        {byte[] bad=before.ToArray();bad[at]^=1;Refuse(()=>batch.RequireExactCurrentBody(bad),check);}
        Refuse(()=>batch.RequireExactCurrentBody(before[..^1]),check);
        Refuse(()=>batch.RequireExactCurrentBody([..before,(byte)0]),check);

        ReadOnly(batch.Packages,check);
        byte[] copiedInput=a.CopyBytes(),copiedAppend=batch.CopyAppendBytes(),copiedInverse=batch.RevertExact(expected);
        aBytes[0]^=1;bBytes[^1]^=1;copiedInput[^1]^=1;copiedAppend[0]^=1;copiedInverse[0]^=1;output[^1]^=1;current[0]^=1;
        check(batch.CopyFinalBody().SequenceEqual(expected)&&batch.CopyAppendBytes().SequenceEqual(append)&&
            batch.RevertExact(expected).SequenceEqual(before)&&a.CopyBytes().SequenceEqual(append.AsSpan(0,0x174).ToArray())&&
            NativeModelTailBatch.Compile(before,[a,b],[450]).CopyFinalBody().SequenceEqual(expected),
            "Input arrays, package copies, append copies, inverse copies and output copies cannot mutate a frozen batch or its deterministic retry.");
        batch.RequireExactCurrentBody(before);

        const BindingFlags instance=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        var type=typeof(NativeModelTailBatch);
        check(type.IsSealed&&!type.IsPublic&&type.GetConstructors(instance).All(c=>c.IsPrivate)&&
            new[]{type,typeof(NativeModelTailPackage)}.All(t=>t.GetProperties(instance).All(p=>
                p.PropertyType!=typeof(bool)&&p.PropertyType!=typeof(NativeAddressSpace)&&p.PropertyType!=typeof(NativeByteExtent)&&
                p.PropertyType!=typeof(NativeMobyModelRegistrationSource))),
            "Neutral append storage has no caller-created Original/Final domain, native source certificate or runtime/export capability flags.");
        Console.WriteLine("NATIVE_MODEL_TAIL_BATCH_ORDER_IDENTITY_AGGREGATE_ALIGNMENT_INSTALL_INVERSE_AND_BOUNDS_OK nativeAdmission=false");
    }

    private static byte[] Fixture(int genericCount)
    {
        byte[] bytes=new byte[0x3800];
        int[] offsets=[0x800,0x1000,0x1800,0x2800,0x3000],lengths=[0x800,0x800,0x1000,0x800,0x800];
        for(int i=0;i<offsets.Length;i++){Put(bytes,i*8,(uint)offsets[i]);Put(bytes,i*8+4,(uint)lengths[i]);}
        for(int i=0;i<=genericCount;i++){Put(bytes,0x50+i*4,(uint)(0x1800+i*4));U16(bytes,0x150+i*2,(ushort)i);}
        for(int i=0x800;i<bytes.Length;i++)bytes[i]=(byte)(i*37+11);
        // Even zero-valued tail bytes belong to the entire current model owner.
        bytes.AsSpan(0x2780,0x80).Clear();return bytes;
    }
    private static byte[] Expected(byte[] current,(ushort Class,byte[] Bytes)[] packages)
    {
        int appendLength=packages.Sum(p=>p.Bytes.Length),growth=(appendLength+0x7FF)&~0x7FF;
        byte[] bytes=new byte[current.Length+growth];current.AsSpan(0,0x2800).CopyTo(bytes);
        current.AsSpan(0x2800).CopyTo(bytes.AsSpan(0x2800+growth));
        Put(bytes,0x14,(uint)(0x1000+growth));Put(bytes,0x18,(uint)(0x2800+growth));Put(bytes,0x20,(uint)(0x3000+growth));
        int cursor=0x2800;
        for(int i=0;i<packages.Length;i++)
        {
            packages[i].Bytes.CopyTo(bytes,cursor);Put(bytes,0x5C+i*4,(uint)cursor);U16(bytes,0x156+i*2,packages[i].Class);
            cursor+=packages[i].Bytes.Length;
        }
        return bytes;
    }
    private sealed class BoundedProbe<T>(T value,int limit):IReadOnlyCollection<T>
    {
        internal int Yielded{get;private set;}
        internal int Enumerations{get;private set;}
        internal bool Disposed{get;private set;}
        public int Count=>throw new InvalidOperationException("Do not trust caller Count for bounded enumeration.");
        public IEnumerator<T> GetEnumerator()
        {
            Enumerations++;
            try{while(Yielded<limit){Yielded++;yield return value;}throw new InvalidOperationException("Read past the finite hostile-enumeration refusal probe.");}
            finally{Disposed=true;}
        }
        IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();
    }
    private static byte[] Bytes(int length,int seed)=>Enumerable.Range(0,length).Select(i=>(byte)(i*13+seed)).ToArray();
    private static uint Word(ReadOnlySpan<byte> bytes,int at)=>BinaryPrimitives.ReadUInt32LittleEndian(bytes[at..]);
    private static void Put(byte[] bytes,int at,uint value)=>BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at),value);
    private static void U16(byte[] bytes,int at,ushort value)=>BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(at),value);
    private static void ReadOnly<T>(IReadOnlyList<T> values,Action<bool,string> check)
    {if(values is IList<T> list)Refuse<NotSupportedException>(()=>list.Clear(),check);else check(true,"No mutable model-batch list interface.");}
    private static void Refuse(Action action,Action<bool,string> check)=>Refuse<InvalidDataException>(action,check);
    private static void Refuse<T>(Action action,Action<bool,string> check)where T:Exception
    {try{action();}catch(T){check(true,"Expected model-tail batch refusal "+typeof(T).Name);return;}throw new InvalidDataException("Expected "+typeof(T).Name);}
}
