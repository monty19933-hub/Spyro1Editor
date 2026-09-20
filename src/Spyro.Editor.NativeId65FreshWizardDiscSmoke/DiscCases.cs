// Exact physical bytes are tested against the independent original-source oracle.
using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;

internal static class DiscCases
{
    const string Original="fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37";
    const string Primary="c4a70f826b1a9a3d4ad8e36512be354b6205d27ea2c7a314776a51067f35b5a6";
    internal static void Geometry(Action<bool,string> check,Action<Action,string> reject)
    {
        var g=NativeId65FreshWizardDiscGeometry.Compile(0x11000,0x20C800);
        check(!g.SourceCertified&&g.GrowthByteLength==0x21D800&&g.FinalWadByteLength==0x6B44800&&
            g.BodyWadOffset==0x6938000&&g.FinalExecutableLba==54958&&g.FinalExecutableEndExclusive==55162,
            "bounded geometry derives new owner end without source authority");
        foreach(int bad in new[]{-1,0,0xF800,0x11001,0x11800,int.MaxValue})
            reject(()=>NativeId65FreshWizardDiscGeometry.Compile(bad,0x20C800),"not the actual Host allocation");
        foreach(int bad in new[]{-1,0,0x1FB800,0x20C000,0x20C801,0x20D000,int.MaxValue})
            reject(()=>NativeId65FreshWizardDiscGeometry.Compile(0x11000,bad),"not the actual Body allocation");
        var owners=typeof(NativeFreshResearchPhysicalSourceOwners);
        var constructors=owners.GetConstructors(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        var factories=owners.GetMethods(BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly);
        check(owners.IsSealed&&!owners.IsPublic&&constructors.Length==1&&constructors[0].IsPrivate&&
            owners.GetMethods(BindingFlags.Public|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly).Length==0&&
            owners.GetFields(BindingFlags.NonPublic|BindingFlags.Instance).All(f=>f.IsInitOnly),
            "shared physical owners are internal, sealed, privately captured immutable storage with no public byte authority");
        check(factories.Length==1&&factories[0].Name=="Capture"&&factories[0].IsAssembly&&
            factories[0].ReturnType==owners&&factories[0].GetParameters().Select(p=>p.ParameterType).SequenceEqual(new[]{
                typeof(Stream),typeof(NativeId65FreshDiscSource),typeof(NativeId65FreshWizardDiscGeometry),typeof(CancellationToken)}),
            "only an actual Original index and closed geometry enter shared capture; no arrays, hashes, delegates or interfaces can issue owners");
        using var unread=new NoPhysicalReadStream();
        using(var canceled=new CancellationTokenSource())
        {
            canceled.Cancel();bool refused=false;
            try{_=NativeFreshResearchPhysicalSourceOwners.Capture(unread,null!,g,canceled.Token);}
            catch(OperationCanceledException error){refused=error.CancellationToken==canceled.Token;}
            check(refused&&unread.Accesses==0,"shared capture pre-cancellation refuses before source access or missing-owner inspection");
        }
        foreach(bool absentStream in new[]{true,false})
        {
            bool refused=false;
            try{_=NativeFreshResearchPhysicalSourceOwners.Capture(absentStream?null!:unread,null!,g,CancellationToken.None);}
            catch(ArgumentNullException error){refused=error.ParamName==(absentStream?"image":"original");}
            check(refused&&unread.Accesses==0,"shared capture requires the actual stream and Original owner before any physical read");
        }
    }

    internal static async Task<NativeId65FreshWizardResearchDiscPlan> PrimarySource(string path,
        NativeId65FreshWizardResearchExecutablePlan actual,
        NativeId65FreshWizardResearchExecutablePlan equalForeign,
        Action<bool,string> check,Action<Action,string> reject)
    {
        var plan=await NativeId65FreshWizardResearchDiscPlan.CaptureAsync(path,actual);
        check(ReferenceEquals(plan.ExecutableAncestor,actual),"actual retained EXE identity, not a hash reconstruction");
        plan.RequireAncestry(actual);
        check(actual.CopyExecutable().SequenceEqual(equalForeign.CopyExecutable()),"foreign control really has equal executable bytes");
        reject(()=>plan.RequireAncestry(equalForeign),"equal-byte foreign EXE is not the captured ancestor");
        byte[] root=plan.CopyIsoRootDirectory(),wad=plan.CopyWadDirectory();
        check(Hash(root)=="2769092a104860606518faf9cc8188c27257236b471d11153ac714ebba6ee5ee"&&
            Hash(wad)=="4a862b5fa39739d5a6041b8de75aa8801824aed35afe0283f9ad572810580e82",
            "complete two metadata payloads match the independent pre-production oracle");
        check(BinaryPrimitives.ReadInt32LittleEndian(wad.AsSpan(79*8))==0x6927000&&
            BinaryPrimitives.ReadInt32LittleEndian(wad.AsSpan(79*8+4))==0x11000&&
            BinaryPrimitives.ReadInt32LittleEndian(wad.AsSpan(80*8))==0x6938000&&
            BinaryPrimitives.ReadInt32LittleEndian(wad.AsSpan(80*8+4))==0x20C800&&
            wad.AsSpan(81*8,8).SequenceEqual(Convert.FromHexString("0078C90500200000")),"only exact reserved pair");
        check(plan.CopyExecutable().SequenceEqual(actual.CopyExecutable()),"actual complete compiled EXE unchanged by physical placement");
        root[0]^=1;wad[0]^=1;
        check(!plan.CopyIsoRootDirectory().SequenceEqual(root)&&!plan.CopyWadDirectory().SequenceEqual(wad),"returned metadata copies detached");

        using var source=File.OpenRead(path);
        using var forward=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var inverse=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] raw=new byte[2352];int changed=0,newTail=0;
        for(int lba=0;lba<281270;lba++)
        {
            source.ReadExactly(raw);byte[] after=plan.CreateExpectedRawSector(lba,raw);
            byte[] restored=plan.CreateOriginalRawSector(lba,after);
            check(restored.SequenceEqual(raw),"actual complete physical inverse, including overwritten EXE/Form2 gap");
            bool expectedChanged=lba==22||lba==37||lba>=53874&&lba<55162;
            check(!after.SequenceEqual(raw)==expectedChanged,"exact changed-owner set including all37 newly validated sectors");
            if(expectedChanged){changed++;if(lba>=55125)newTail++;}
            forward.AppendData(after);inverse.AppendData(restored);
            if(lba is 0 or 22 or 37 or 53874 or 53875 or 53908 or 53909 or 54078 or 54079 or
                54957 or 54958 or 55124 or 55125 or 55161 or 55162 or 59999 or 60000 or 281269)
            {
                foreach(int at in new[]{0,12,15,16,18,20,22,24,2072,2076,2351})
                {
                    byte[] bad=after.ToArray();bad[at]^=1;
                    reject(()=>plan.CreateOriginalRawSector(lba,bad),"inverse refuses changed or untouched physical corruption");
                }
                byte[] badSource=raw.ToArray();badSource[24]^=1;
                reject(()=>plan.CreateExpectedRawSector(lba,badSource),"expected afterimage never blesses unauthenticated caller bytes");
                reject(()=>plan.CreateExpectedRawSector(lba==281269?0:lba+1,raw),"wrong original LBA, including similar zero padding");
            }
        }
        check(source.ReadByte()==-1&&source.Position==661547040&&source.Length==661547040,"actual original EOF and final guards");
        check(Hash(forward)==Primary&&Hash(inverse)==Original&&changed==1290&&newTail==37,"full independent physical golden and executed inverse");
        foreach(int lba in new[]{-1,281270,int.MaxValue})
            reject(()=>plan.CreateExpectedRawSector(lba,raw),"invalid LBA rejects before any source authority");
        reject(()=>plan.CreateExpectedRawSector(0,raw[..^1]),"short physical input");
        reject(()=>plan.CreateOriginalRawSector(0,raw.Concat(new byte[1]).ToArray()),"long physical input");
        return plan;
    }
    // Additional separate files implement: actual-readback stream with successful
    // return assertion + all EOF/cancel attacks; same-path authenticated-capture
    // hook overlays; alternate actual scene retained despite equal EXE; API shape.
    // No default or --source invocation writes a physical candidate.
    private sealed class NoPhysicalReadStream:Stream
    {
        internal int Accesses;
        private T Touch<T>(){Accesses++;throw new InvalidOperationException("Source-free physical owner test touched the stream.");}
        public override bool CanRead=>Touch<bool>();
        public override bool CanSeek=>Touch<bool>();
        public override bool CanWrite=>Touch<bool>();
        public override long Length=>Touch<long>();
        public override long Position{get=>Touch<long>();set{Touch<int>();}}
        public override int Read(byte[] b,int at,int n)=>Touch<int>();
        public override long Seek(long offset,SeekOrigin origin)=>Touch<long>();
        public override void Flush()=>Touch<int>();
        public override void SetLength(long n)=>Touch<int>();
        public override void Write(byte[] b,int at,int n)=>Touch<int>();
    }
    static string Hash(ReadOnlySpan<byte> b)=>Convert.ToHexStringLower(SHA256.HashData(b));
    static string Hash(IncrementalHash h)=>Convert.ToHexStringLower(h.GetHashAndReset());
}
