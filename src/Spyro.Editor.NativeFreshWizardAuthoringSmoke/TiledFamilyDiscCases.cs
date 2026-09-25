using System.Reflection;
using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;

// Independent Python physical oracle a86dbc4694f3ef1b1b52cf028f6af59c720ca9566c8ba176b33cbfe97948599a
// pins the whole child-relative image. Tests stream virtual sectors, never a BIN.
internal static class TiledFamilyDiscCases
{
    private const int SectorBytes=2352,SectorCount=281270,End=55169;
    private const long ImageBytes=661547040;
    private const string OriginalHash="fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37";
    private const string ParentHash="a5b00725388f73e7e6f7b9ed356c4a0d0f55b1c869d6a65f38e97ef2641d6c50";
    private const string FinalHash="689fcf296e26d635d9c5ce6fc3fc687c2b47decce10ade2a0903f0c6bd877192";
    private const string BodyHash="c8c85e71b633e29ce573dddccb04a4a0adcd6696d292658304328e435dfaa9ca";

    internal static void Free(Action<bool,string> check)
    {
        var type=typeof(NativeFreshTiledFamilyDiscPlan);
        check(!type.IsPublic&&type.IsSealed&&type.GetConstructors(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance)
            .All(c=>c.IsPrivate)&&type.GetMethods(BindingFlags.Public|BindingFlags.Static|BindingFlags.Instance|BindingFlags.DeclaredOnly).Length==0,
            "Tiled physical composition is internal/private and introduces no public issuer or exporter.");
        var compose=type.GetMethods(BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.DeclaredOnly).Single(m=>m.Name=="Compose");
        check(compose.IsAssembly&&compose.ReturnType==type&&compose.GetParameters().Select(p=>p.ParameterType).SequenceEqual(
            new[]{typeof(NativeFreshTiledFamilyExecutablePlan),typeof(NativeFreshLockedChestResearchDiscPlan)})&&
            !type.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.DeclaredOnly)
                .Any(m=>m.Name.Contains("Capture",StringComparison.Ordinal)),
            "Closed composition accepts only an actual tiled executable and already-captured parent disc, with no new source capture.");
        foreach(var item in new[]{("ExecutableAncestor",typeof(NativeFreshTiledFamilyExecutablePlan)),
            ("ParentDiscAncestor",typeof(NativeFreshLockedChestResearchDiscPlan))})
        {
            var property=type.GetProperty(item.Item1,BindingFlags.NonPublic|BindingFlags.Instance);
            check(property is not null&&property.PropertyType==item.Item2&&property.GetMethod?.IsAssembly==true&&property.GetSetMethod(true) is null,
                "Read-only actual physical ancestor "+item.Item1);
        }
        Reject<ArgumentNullException>(()=>NativeFreshTiledFamilyDiscPlan.Compose(null!,null!),check);
        Reject<ArgumentNullException>(()=>NativeId65FreshWizardDiscGeometry.CompileTiledFamily(null!),check);
        Reject<ArgumentNullException>(()=>NativeFreshResearchPhysicalAfterimages.ForTiledFamily(null!,null!),check);
        using var cancel=new CancellationTokenSource();cancel.Cancel();using var unread=new Unreadable();
        Reject<OperationCanceledException>(()=>NativeId65FreshDiscStreaming.Measure((NativeFreshTiledFamilyDiscPlan)null!,unread,cancel.Token),check);
        Reject<ArgumentNullException>(()=>NativeId65FreshDiscStreaming.Measure((NativeFreshTiledFamilyDiscPlan)null!,unread,CancellationToken.None),check);
        Reject<OperationCanceledException>(()=>NativeId65FreshDiscStreaming.VerifyInverse((NativeFreshTiledFamilyDiscPlan)null!,unread,cancel.Token),check);
        Reject<ArgumentNullException>(()=>NativeId65FreshDiscStreaming.VerifyInverse((NativeFreshTiledFamilyDiscPlan)null!,unread,CancellationToken.None),check);
        check(unread.Accesses==0,"Canceled/null typed streaming refuses without touching caller streams.");
        Console.WriteLine("TILED_FAMILY_DISC_FREE_OK sourceOpened=false normalCreateBin=false runtime=false publication=false");
    }

    internal static async Task Source(string originalBin,NativeFreshTiledFamilyExecutablePlan executable,
        NativeFreshLockedChestResearchDiscPlan parent,Action<bool,string> check)
    {
        ArgumentNullException.ThrowIfNull(executable);ArgumentNullException.ThrowIfNull(parent);ArgumentNullException.ThrowIfNull(check);
        executable.RequireAncestry(executable.BodyAncestor,executable.ParentExecutableAncestor);
        parent.RequireAncestry(executable.ParentExecutableAncestor);
        var body=executable.BodyAncestor;var oldBody=parent.ExecutableAncestor.BodyAncestor;
        byte[] finalBody=executable.CopyLevelData(),oldBodyBytes=parent.CopyBody();
        byte[] overlay=parent.CopyOverlay(),exe=parent.CopyExecutable(),wad=parent.CopyWadDirectory(),root=parent.CopyIsoRootDirectory();
        string parentIdentity=parent.PlanSha256,exeIdentity=executable.PlanSha256;
        check(Hash(finalBody)==BodyHash&&Hash(oldBodyBytes)=="2d8c1bf1af35c8e5522b239c564d36c79c828539d133278c461b6178b53bb504"&&
            executable.CopyExecutable().SequenceEqual(exe)&&ReferenceEquals(parent.ExecutableAncestor,executable.ParentExecutableAncestor),
            "Actual retained parent payloads and tiled Body match independent references before physical composition.");
        var plan=NativeFreshTiledFamilyDiscPlan.Compose(executable,parent);
        plan.RequireAncestry(executable,parent);
        check(ReferenceEquals(plan.ExecutableAncestor,executable)&&ReferenceEquals(plan.ParentDiscAncestor,parent)&&
            plan.ExecutablePlanSha256==executable.PlanSha256&&plan.ParentDiscPlanSha256==parent.PlanSha256&&
            plan.CanonicalDocumentSha256==executable.CanonicalDocumentSha256&&plan.SourceImageSha256==OriginalHash&&
            plan.BodySha256==BodyHash&&plan.ExecutableSha256==executable.ExecutableSha256&&IsHash(plan.PlanSha256),
            "Complete physical fingerprint retains actual tiled document/executable/Body and captured parent ownership.");
        check(plan.BodyLba==53911&&plan.FinalExecutableLba==54965&&plan.FinalExecutableEndExclusive==End&&
            plan.FinalWadByteLength==0x6B48000&&plan.ChangedSectorOwnerCount==1297&&
            plan.BodyLba==parent.BodyLba&&plan.FinalExecutableLba==parent.FinalExecutableLba&&
            plan.FinalWadByteLength==parent.FinalWadByteLength,
            "Unchanged physical allocation shares1297 actual Original owners, independently of the new native-memory accounting.");
        var geometry=NativeId65FreshWizardDiscGeometry.CompileTiledFamily(executable);
        check(!geometry.SourceCertified&&geometry.BodyByteLength==0x20F000&&geometry.OverlayByteLength==0x12000&&
            geometry.GrowthByteLength==0x221000&&body.RuntimeMemory.UsedLevelDataBytes==0xCB8&&
            body.RuntimeMemory.ModelRamStart==0x8008D6F0u&&body.RuntimeMemory.SceneRamStart==0x800A86F0u&&
            body.RuntimeMemory.SceneReadEnd==0x800ABEF0u&&oldBody.RuntimeMemory.UsedLevelDataBytes==0xA6C&&
            oldBody.RuntimeMemory.SceneReadEnd==0x800ABCA4u,
            "New geometry consumes actual CB8 relation; original physical ownership does not substitute the old A6C relation.");
        check(plan.SourceCertified&&plan.ExperimentalKeyZeroAssumption&&plan.ExperimentalTreasureTarget==20&&
            !plan.FullNativeLoaderProven&&!plan.CompleteStaticCensusProven&&!plan.CompleteNativeDependencyClosureProven&&
            !plan.PeakRuntimeMemoryProven&&!plan.RuntimeVerified&&!plan.PublicationApproved&&!plan.NormalCreateBinEnabled&&
            !executable.RuntimeVerified&&!body.FinalExecutableIssued,
            "Exact physical composition grants no full-loader, census, closure, peak, runtime, normal-route or publication approval.");
        byte[][] required=[finalBody,overlay,exe,wad,root];
        check(Copies(plan).Zip(required).All(p=>p.First.SequenceEqual(p.Second))&&
            new[]{plan.BodySha256,plan.OverlaySha256,plan.ExecutableSha256,plan.WadDirectorySha256,plan.IsoRootSha256}
                .SequenceEqual(required.Select(b=>Hash(b))),"Complete payloads retain existing root/WAD/overlay/EXE owners, changing only the issued Body.");
        foreach(var bytes in Copies(plan)){bytes[0]^=1;bytes[^1]^=1;}
        check(Copies(plan).Zip(required).All(p=>p.First.SequenceEqual(p.Second)),"All returned physical payloads are detached.");

        var equalExe=NativeFreshTiledFamilyExecutablePlan.Compose(body,executable.ParentExecutableAncestor);
        check(!ReferenceEquals(equalExe,executable)&&equalExe.PlanSha256==executable.PlanSha256&&equalExe.CopyExecutable().SequenceEqual(exe),
            "Equal tiled executable is a genuine foreign instance before retained-tuple refusal.");
        Reject<InvalidDataException>(()=>plan.RequireAncestry(equalExe,parent),check);
        var equalParent=await NativeFreshLockedChestResearchDiscPlan.CaptureAsync(originalBin,parent.ExecutableAncestor);
        check(!ReferenceEquals(equalParent,parent)&&equalParent.PlanSha256==parent.PlanSha256&&equalParent.CopyBody().SequenceEqual(oldBodyBytes),
            "Equal parent control owns a genuine separate captured Original/index, with equal bytes and fingerprint.");
        Reject<InvalidDataException>(()=>plan.RequireAncestry(executable,equalParent),check);
        var own=NativeFreshTiledFamilyDiscPlan.Compose(equalExe,equalParent);
        check(own.PlanSha256==plan.PlanSha256&&ReferenceEquals(own.ExecutableAncestor,equalExe)&&ReferenceEquals(own.ParentDiscAncestor,equalParent),
            "Equal tuple can issue its own deterministic plan but cannot replace retained instance ownership.");
        Reject<InvalidDataException>(()=>own.RequireAncestry(executable,parent),check);
        var foreignOldExe=NativeFreshLockedChestExperimentalExecutable.Compose(parent.ExecutableAncestor.PlacementAncestor,parent.ExecutableAncestor.HostAncestor);
        var foreignTiled=NativeFreshTiledFamilyExecutablePlan.Compose(body,foreignOldExe);
        check(foreignTiled.CopyExecutable().SequenceEqual(exe),"Foreign parent-executable chain preserves executable bytes before common-owner rejection.");
        Reject<InvalidDataException>(()=>NativeFreshTiledFamilyDiscPlan.Compose(foreignTiled,parent),check);
        Reject<ArgumentNullException>(()=>NativeFreshTiledFamilyDiscPlan.Compose(null!,parent),check);
        Reject<ArgumentNullException>(()=>NativeFreshTiledFamilyDiscPlan.Compose(executable,null!),check);
        Reject<ArgumentNullException>(()=>plan.RequireAncestry(null!,parent),check);
        Reject<ArgumentNullException>(()=>plan.RequireAncestry(executable,null!),check);

        NativeId65FreshDiscVerification measured;
        using(var source=File.OpenRead(originalBin))
        using(var comparison=File.OpenRead(originalBin))
        using(var sink=new InverseSink(comparison,plan,parent,finalBody,check))
        {
            measured=NativeId65FreshDiscStreaming.Measure(plan,source,CancellationToken.None,sink);
            sink.Complete();
            check(measured.OutputSha256==FinalHash&&measured.ChangedSectors==1297&&source.Position==ImageBytes&&source.ReadByte()==-1&&
                source.CanRead&&comparison.CanRead,"Whole virtual BIN matches independent Python reference and all1297 Original changed sectors.");
        }
        using(var source=File.OpenRead(originalBin))
        using(var virtualSource=File.OpenRead(originalBin))
        using(var expected=new ExpectedImage(virtualSource,parent,finalBody))
        {
            source.Position=17;expected.Position=29;
            check(plan.VerifyImage(source,expected)==measured&&source.Position==ImageBytes&&expected.Position==ImageBytes&&expected.EofReads>0&&
                source.CanRead&&virtualSource.CanRead,"Shared verifier consumes every expected sector and actual EOF without taking caller stream ownership.");
            expected.Position=0;
            check(NativeId65FreshDiscStreaming.VerifyInverse(plan,expected,CancellationToken.None)==measured&&expected.EofReads>1,
                "Typed shared inverse authenticates every virtual candidate sector and restores the complete Original hash.");
        }
        using(var virtualSource=File.OpenRead(originalBin))
        using(var extraEof=new ExpectedImage(virtualSource,parent,finalBody,true))
            Reject<InvalidDataException>(()=>NativeId65FreshDiscStreaming.VerifyInverse(plan,extraEof,CancellationToken.None),check);
        using(var source=File.OpenRead(originalBin))
        using(var output=new MemoryStream())
        using(var cancel=new CancellationTokenSource())
        {
            Reject<OperationCanceledException>(()=>NativeId65FreshDiscStreaming.Measure(plan,source,cancel.Token,output,cancel.Cancel),check);
            check(source.Position==SectorBytes&&output.Length==SectorBytes&&source.CanRead&&output.CanWrite,
                "Post-first-sector cancellation emits no success and preserves caller-owned streams.");
        }
        using(var source=File.OpenRead(originalBin))
        {
            source.Position=1;
            Reject<InvalidDataException>(()=>NativeId65FreshDiscStreaming.Measure(plan,source,CancellationToken.None),check);
            Reject<InvalidDataException>(()=>NativeId65FreshDiscStreaming.VerifyInverse(plan,source,CancellationToken.None),check);
            check(source.Position==1,"Nonzero start refuses before reading.");
        }
        using(var source=File.OpenRead(originalBin))
        using(var invalid=new MemoryStream(new byte[1]))
        {
            Reject<InvalidDataException>(()=>NativeId65FreshDiscStreaming.Measure(plan,source,CancellationToken.None,invalid),check);
            Reject<InvalidDataException>(()=>plan.VerifyImage(source,invalid),check);
            Reject<InvalidDataException>(()=>NativeId65FreshDiscStreaming.VerifyInverse(plan,invalid,CancellationToken.None),check);
        }
        byte[] raw=new byte[SectorBytes];using(var source=File.OpenRead(originalBin))source.ReadExactly(raw);
        foreach(int lba in new[]{-1,SectorCount,int.MaxValue})
        {
            Reject<InvalidDataException>(()=>plan.CreateExpectedRawSector(lba,raw),check);
            Reject<InvalidDataException>(()=>plan.CreateOriginalRawSector(lba,raw),check);
        }
        foreach(byte[] invalid in new byte[][]{[],raw[..^1],[..raw,(byte)0]})
        {
            Reject<InvalidDataException>(()=>plan.CreateExpectedRawSector(0,invalid),check);
            Reject<InvalidDataException>(()=>plan.CreateOriginalRawSector(0,invalid),check);
        }
        check(parent.PlanSha256==parentIdentity&&executable.PlanSha256==exeIdentity&&parent.CopyBody().SequenceEqual(oldBodyBytes)&&
            parent.CopyOverlay().SequenceEqual(overlay)&&parent.CopyExecutable().SequenceEqual(exe)&&
            parent.CopyWadDirectory().SequenceEqual(wad)&&parent.CopyIsoRootDirectory().SequenceEqual(root)&&
            Copies(plan).Zip(required).All(p=>p.First.SequenceEqual(p.Second)),"All refusals, streams and copy mutations preserve the whole old parent and new immutable tuple.");
        Console.WriteLine($"TILED_FAMILY_DISC_SOURCE_OK candidate={FinalHash} parent={ParentHash} inverse={OriginalHash} changedOriginal=1297 changedParent=2 rawDelta=1969 payloadDelta=1413 outputFiles=0 runtime=false publication=false");
    }

    // Child-relative comparison uses the already frozen actual parent's sectors,
    // then independently replaces only two data-sector payloads and rebuilds ECC.
    // The whole result is additionally pinned by the separate Python oracle.
    private static byte[] ExpectedSector(NativeFreshLockedChestResearchDiscPlan parent,byte[] body,int lba,ReadOnlySpan<byte> original)
    {
        byte[] expected=parent.CreateExpectedRawSector(lba,original);
        if(lba is 54371 or 54372)
        {
            body.AsSpan((lba-53911)*2048,2048).CopyTo(expected.AsSpan(24,2048));
            RawMode2Form1SectorIntegrity.RebuildLogicalWriteSector(expected,new(2352,24,22,2048),lba);
        }
        return expected;
    }
    private sealed class InverseSink(Stream original,NativeFreshTiledFamilyDiscPlan plan,
        NativeFreshLockedChestResearchDiscPlan parent,byte[] body,Action<bool,string> check):Stream
    {
        private readonly IncrementalHash _candidate=IncrementalHash.CreateHash(HashAlgorithmName.SHA256),
            _original=IncrementalHash.CreateHash(HashAlgorithmName.SHA256),_parent=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        private readonly List<int> _changed=[];private long _position;private int _rawDelta,_payloadDelta;
        public override bool CanRead=>false;public override bool CanSeek=>true;public override bool CanWrite=>true;
        public override long Length=>_position;
        public override long Position{get=>_position;set{if(value!=_position)throw new NotSupportedException();}}
        public override void Write(byte[] bytes,int at,int count)=>Write(bytes.AsSpan(at,count));
        public override void Write(ReadOnlySpan<byte> supplied)
        {
            Require(supplied.Length==SectorBytes&&_position<ImageBytes,"One complete sequential sector");
            int lba=(int)(_position/SectorBytes);byte[] before=new byte[SectorBytes];original.ReadExactly(before);
            byte[] expected=ExpectedSector(parent,body,lba,before),restored=plan.CreateOriginalRawSector(lba,supplied);
            byte[] old=parent.CreateExpectedRawSector(lba,restored);
            Require(supplied.SequenceEqual(expected)&&restored.AsSpan().SequenceEqual(before),"Exact full raw afterimage and Original inverse at "+lba);
            if(!supplied.SequenceEqual(old))_changed.Add(lba);
            for(int i=0;i<SectorBytes;i++)if(supplied[i]!=old[i]){_rawDelta++;if(i is >=24 and <2072)_payloadDelta++;}
            _candidate.AppendData(supplied);_original.AppendData(restored);_parent.AppendData(old);_position+=SectorBytes;
            if(lba is 0 or 22 or 37 or 53874 or 54371 or 54372 or 54965 or 55168 or 55169 or 281269)
            {
                foreach(int at in new[]{0,12,15,16,18,20,22,24,2072,2076,2351})
                {
                    byte[] bad=supplied.ToArray();bad[at]^=1;
                    Reject<InvalidDataException>(()=>plan.CreateOriginalRawSector(lba,bad),check);
                }
                byte[] badSource=before.ToArray();badSource[24]^=1;
                Reject<InvalidDataException>(()=>plan.CreateExpectedRawSector(lba,badSource),check);
                if(lba is 22 or 37 or 54371 or 54372)
                {
                    byte[] bad=supplied.ToArray();bad[24]^=1;
                    RawMode2Form1SectorIntegrity.RebuildLogicalWriteSector(bad,new(2352,24,22,2048),lba);
                    Reject<InvalidDataException>(()=>plan.CreateOriginalRawSector(lba,bad),check);
                }
                check(supplied[..16].SequenceEqual(before.AsSpan(0,16)),"Physical sector address/header complement remains exact.");
            }
        }
        internal void Complete()
        {
            Require(_position==ImageBytes&&original.Position==ImageBytes&&original.ReadByte()==-1,"Complete source and sink actual EOF");
            check(Convert.ToHexStringLower(_candidate.GetHashAndReset())==FinalHash&&
                Convert.ToHexStringLower(_original.GetHashAndReset())==OriginalHash&&
                Convert.ToHexStringLower(_parent.GetHashAndReset())==ParentHash&&
                _changed.SequenceEqual(new[]{54371,54372})&&_rawDelta==1969&&_payloadDelta==1413,
                "Full new image, Original inverse and immediate old-child inverse match Python pins with exact two-sector/raw/payload deltas.");
        }
        protected override void Dispose(bool disposing){if(disposing){_candidate.Dispose();_original.Dispose();_parent.Dispose();}base.Dispose(disposing);}
        public override void Flush(){}
        public override int Read(byte[] bytes,int at,int count)=>throw new NotSupportedException();
        public override long Seek(long count,SeekOrigin origin)=>throw new NotSupportedException();
        public override void SetLength(long length)=>throw new NotSupportedException();
    }
    private sealed class ExpectedImage(Stream original,NativeFreshLockedChestResearchDiscPlan parent,byte[] body,bool extraEof=false):Stream
    {
        private long _position;internal int EofReads;
        public override bool CanRead=>true;public override bool CanSeek=>true;public override bool CanWrite=>false;
        public override long Length=>ImageBytes;
        public override long Position{get=>_position;set{if(value<0||value>Length)throw new ArgumentOutOfRangeException(nameof(value));_position=value;}}
        public override int Read(byte[] bytes,int at,int count)=>Read(bytes.AsSpan(at,count));
        public override int Read(Span<byte> destination)
        {
            int count=(int)Math.Min(destination.Length,Length-_position),copied=0;
            if(count==0&&!destination.IsEmpty){EofReads++;if(extraEof){destination[0]=1;return 1;}}
            while(copied<count)
            {
                int lba=(int)(_position/SectorBytes),offset=(int)(_position%SectorBytes),part=Math.Min(SectorBytes-offset,count-copied);
                byte[] before=new byte[SectorBytes];original.Position=lba*(long)SectorBytes;original.ReadExactly(before);
                ExpectedSector(parent,body,lba,before).AsSpan(offset,part).CopyTo(destination[copied..]);copied+=part;_position+=part;
            }
            return count;
        }
        public override long Seek(long offset,SeekOrigin origin){Position=checked((origin switch{SeekOrigin.Begin=>0,SeekOrigin.Current=>Position,SeekOrigin.End=>Length,_=>throw new ArgumentOutOfRangeException(nameof(origin))})+offset);return Position;}
        public override void Flush()=>throw new NotSupportedException();
        public override void SetLength(long length)=>throw new NotSupportedException();
        public override void Write(byte[] bytes,int at,int count)=>throw new NotSupportedException();
    }
    private sealed class Unreadable:Stream
    {
        internal int Accesses;private T Touch<T>(){Accesses++;throw new InvalidOperationException("Source-free test touched a stream.");}
        public override bool CanRead=>Touch<bool>();public override bool CanSeek=>Touch<bool>();public override bool CanWrite=>Touch<bool>();
        public override long Length=>Touch<long>();public override long Position{get=>Touch<long>();set=>Touch<int>();}
        public override int Read(byte[] bytes,int at,int count)=>Touch<int>();public override long Seek(long offset,SeekOrigin origin)=>Touch<long>();
        public override void Flush()=>Touch<int>();public override void SetLength(long length)=>Touch<int>();public override void Write(byte[] bytes,int at,int count)=>Touch<int>();
    }
    private static byte[][] Copies(NativeFreshTiledFamilyDiscPlan plan)=>
        [plan.CopyBody(),plan.CopyOverlay(),plan.CopyExecutable(),plan.CopyWadDirectory(),plan.CopyIsoRootDirectory()];
    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static bool IsHash(string value)=>value.Length==64&&value.All(c=>c is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static void Require(bool condition,string label){if(!condition)throw new InvalidDataException(label);}
    private static void Reject<T>(Action action,Action<bool,string> check)where T:Exception
    {try{action();}catch(T){check(true,"Tiled physical refusal "+typeof(T).Name);return;}throw new InvalidDataException("Expected "+typeof(T).Name);}
}
