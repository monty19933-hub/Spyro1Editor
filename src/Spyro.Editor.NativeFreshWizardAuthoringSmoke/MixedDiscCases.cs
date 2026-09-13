using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;

internal static class MixedDiscCases
{
    private const string MaterialRequired="Material-authored disc requires its actual mixed executable.";
    private const string OldDiscRefusal="Material-authored Wizard executable disc composition is not enabled.";
    private const string OldCandidateRefusal="Material-authored Wizard disc requires its separate saved candidate binding.";
    private const string OracleName="_local/research/mixed-wizard-material-composition-r01.ZTFRMb/09-original-first.json";
    private const string OracleHash="e04c7a6c17c3800bd1e53225bcca8f1d6c3cc32a890428ba0b8dc2021510df31";
    private static readonly string[] Names=["primary","reversed","same-source"];
    private static readonly string[] Images=[
        "98b4c9d2ee5cef75f733367b3910f01a29f44fceb4b57d6b1056de23d6ea4e46",
        "65b13d5382e2031c6d6bee4bd379b8bfb217c4537c144f9663aa9e3889c0f557",
        "02655e30f9ee5d74166ff8b8b5f5bf0aadc6a0d86c47cc692b4e4f35df97277a"];
    internal static async Task Free(Action<bool,string> check)
    {
        Type type=typeof(NativeId65FreshWizardResearchDiscPlan);
        var method=type.GetMethod("CaptureMaterialAuthoredAsync",BindingFlags.Static|BindingFlags.NonPublic);
        check(method is {IsAssembly:true}&&method.ReturnType==typeof(Task<NativeId65FreshWizardResearchDiscPlan>)&&
            method.GetParameters().Select(p=>p.ParameterType).SequenceEqual(
                new[]{typeof(string),typeof(NativeId65FreshWizardResearchExecutablePlan),typeof(CancellationToken)})&&
            method.GetParameters()[2].HasDefaultValue,"One internal exact-EXE mixed Disc factory with default token.");
        check(type.GetMethods(BindingFlags.Public|BindingFlags.Static|BindingFlags.DeclaredOnly).Select(m=>m.Name)
            .SequenceEqual(new[]{"CaptureAsync"}),"The old public Disc factory remains the sole public issuer.");
        check(type.GetConstructors(BindingFlags.Public|BindingFlags.Instance).Length==0&&
            type.GetConstructors(BindingFlags.NonPublic|BindingFlags.Instance) is [{IsPrivate:true}],
            "Both Disc arms require the one private source constructor.");
        using var cancelled=new CancellationTokenSource();cancelled.Cancel();
        Task cancelledTask=NativeId65FreshWizardResearchDiscPlan.CaptureMaterialAuthoredAsync("must-not-open",null!,cancelled.Token);
        await Refuse<OperationCanceledException>(()=>cancelledTask,check);
        Task nullTask=NativeId65FreshWizardResearchDiscPlan.CaptureMaterialAuthoredAsync("must-not-open",null!);
        await Refuse<ArgumentNullException>(()=>nullTask,check);
        await Refuse<ArgumentNullException>(()=>{
            NativeId65FreshWizardResearchDiscSource.RequireMaterialExecutable(null!);return Task.CompletedTask;
        },check);
        // Component accounting control only; this does not inject filesystem
        // close failures into the actual path-based issuer.
        Exception[] ordered=[new InvalidDataException("primary"),new IOException("held-final"),
            new IOException("named-final"),new IOException("named-close"),new IOException("retained-index"),new IOException("held-close")];
        try{NativeFreshResearchSourceOwners.ThrowFailures(ordered);throw new Exception("Expected accounting refusal.");}
        catch(IOException error)when(error.InnerException is AggregateException aggregate)
        {check(aggregate.InnerExceptions.SequenceEqual(ordered),"Component-only accounting retains primary once and ordered final/close references.");}
    }

    internal static async Task LegacyExecutableRefusal(NativeId65FreshWizardResearchExecutablePlan executable,Action<bool,string> check)
    {
        check(!executable.IsMaterialAuthored,"Wrong-arm control retains the genuinely issued untextured EXE.");
        await Refuse<InvalidDataException>(()=>NativeId65FreshWizardResearchDiscPlan.CaptureMaterialAuthoredAsync(
            "must-not-open",executable),check,MaterialRequired);
        await Refuse<InvalidDataException>(()=>{
            NativeId65FreshWizardResearchDiscSource.RequireMaterialExecutable(executable);return Task.CompletedTask;
        },check,MaterialRequired);
        using var cancelled=new CancellationTokenSource();cancelled.Cancel();
        await Refuse<OperationCanceledException>(()=>NativeId65FreshWizardResearchDiscPlan.CaptureMaterialAuthoredAsync(
            null!,executable,cancelled.Token),check);
    }

    internal static async Task Source(string bin,IReadOnlyList<NativeId65FreshWizardResearchExecutablePlan> executables,
        Action<bool,string> check)
    {
        check(executables.Count==3&&executables.Select(e=>(e.MixedResourceAncestor!.MaterialSources[0].SourceWadEntry,
            e.MixedResourceAncestor!.MaterialSources[1].SourceWadEntry)).SequenceEqual(new[]{(14,70),(70,14),(14,14)}),
            "Only the three actual admitted mixed EXE chains enter Disc source mode, in donor order.");
        using var oracle=ReadOracle(check);
        var physical=oracle.RootElement.GetProperty("physical");
        var plans=new List<NativeId65FreshWizardResearchDiscPlan>();
        string[] identities=executables.Select(e=>e.PlanSha256).ToArray();
        for(int i=0;i<executables.Count;i++)
        {
            var executable=executables[i];var body=executable.BodyAncestor;var mixed=executable.MixedResourceAncestor!;
            var scene=mixed.SceneAncestor;
            var expected=oracle.RootElement.GetProperty("cases").EnumerateArray().Single(c=>c.GetProperty("name").GetString()==Names[i]);
            await Refuse<InvalidDataException>(()=>NativeId65FreshWizardResearchDiscPlan.CaptureAsync("must-not-open",executable),check,OldDiscRefusal);
            await Refuse<ArgumentException>(()=>NativeId65FreshWizardResearchDiscPlan.CaptureMaterialAuthoredAsync("",executable),check);
            using(var cancelled=new CancellationTokenSource())
            {
                cancelled.Cancel();
                await Refuse<OperationCanceledException>(()=>NativeId65FreshWizardResearchDiscPlan.CaptureMaterialAuthoredAsync(
                    "must-not-open",executable,cancelled.Token),check);
            }
            var disc=await NativeId65FreshWizardResearchDiscPlan.CaptureMaterialAuthoredAsync(bin,executable);
            plans.Add(disc);
            disc.RequireAncestry(executable);
            check(disc.IsMaterialAuthored&&ReferenceEquals(disc.ExecutableAncestor,executable)&&
                ReferenceEquals(disc.ExecutableAncestor.BodyAncestor,body)&&ReferenceEquals(body.MixedResourceAncestor,mixed)&&
                ReferenceEquals(mixed.DocumentAncestor,scene.AuthoringDocumentAncestor)&&
                disc.ExecutablePlanSha256==executable.PlanSha256&&disc.BodyPlanSha256==body.PlanSha256&&
                disc.CanonicalDocumentSha256==body.CanonicalDocumentSha256,
                "Actual Disc-to-EXE-to-Body-to-Mixed/full document/ordered donor chain is retained; canonical field remains Base.");
            CheckComponents(disc,expected,physical,check);
            await Refuse<InvalidDataException>(()=>NativeId65FreshWizardResearchCandidatePlan.CaptureAsync(
                "must-not-open","must-not-open",disc),check,OldCandidateRefusal);
            var clearance=await NativeFreshInitialSpawnClearancePlan.CaptureWizardAsync(bin,mixed.DocumentAncestor,scene);
            clearance.RequireInitialSeparation(mixed.DocumentAncestor,scene);
            await Refuse<InvalidDataException>(()=>NativeId65FreshWizardResearchCandidatePlan.CaptureSeparatedAsync(
                "must-not-open","must-not-open",disc,clearance),check,OldCandidateRefusal);
            await Refuse<ArgumentNullException>(()=>NativeId65FreshWizardResearchCandidatePlan.CaptureSeparatedAsync(
                "must-not-open","must-not-open",disc,null!),check);
            var sectorHashes=expected.GetProperty("changedRawSectorSha256").EnumerateObject()
                .ToDictionary(p=>int.Parse(p.Name,System.Globalization.CultureInfo.InvariantCulture),p=>p.Value.GetString()!);
            check(sectorHashes.Keys.Order().SequenceEqual(new[]{22,37}.Concat(Enumerable.Range(53874,1288)))&&
                expected.GetProperty("originalChangedRawSectors").GetInt32()==1290&&
                expected.GetProperty("expectedImageSha256").GetString()==Images[i],
                "Authenticated original-only oracle has exactly all1290 owners and the frozen complete image literal.");
            using(var original=File.OpenRead(bin))
            using(var auditOriginal=File.OpenRead(bin))
            using(var sink=new OwnerSink(auditOriginal,disc,sectorHashes))
            {
                int first=0;
                var result=NativeId65FreshDiscStreaming.Measure(disc,original,CancellationToken.None,sink,()=>first++);
                check(result==new NativeId65FreshDiscVerification(Images[i],1290)&&sink.Digest()==Images[i]&&
                    sink.Length==661547040&&original.Position==661547040&&first==1,
                    "Actual shared stream and independent sink agree on every byte, exact full image hash/count/extent.");
                sink.RequireComplete(check);
            }
            using(var original=File.OpenRead(bin))
            {
                byte[] before=new byte[2352];original.Position=55161*2352L;original.ReadExactly(before);
                byte[] after=disc.CreateExpectedRawSector(55161,before);
                foreach(int at in new[]{0,18,22,24,2072,2351})
                {
                    byte[] changed=after.ToArray();changed[at]^=1;
                    await Refuse<InvalidDataException>(()=>{_=disc.CreateOriginalRawSector(55161,changed);return Task.CompletedTask;},check);
                }
                before[24]^=1;
                await Refuse<InvalidDataException>(()=>{_=disc.CreateExpectedRawSector(55161,before);return Task.CompletedTask;},check);
            }
            check(disc.SourceCertified&&!disc.InstalledDispatchProven&&!disc.GlobalTransitionState12Supported&&!disc.PersistentOwnerIssued&&
                !disc.CardOrHistoryCompatibilityProven&&!disc.WholePickupOrDeathProven&&!disc.GlobalAudioCompatibilityProven&&
                !disc.PeakRuntimeMemoryProven&&!disc.HardwareTransfersProven&&!disc.CompleteIncomingEdgeClosureProven&&
                !disc.CompleteNativeDependencyClosureProven&&!disc.RuntimeVerified&&!disc.PublicationApproved&&!disc.NormalCreateBinEnabled&&
                !body.FinalExecutableIssued&&!mixed.BodyIssued,"Disc source bytes issue no runtime/publication authority or ancestor mutation.");
            Console.WriteLine($"SCHEMA2_MIXED_DISC_SOURCE pair={mixed.MaterialSources[0].SourceWadEntry}/{mixed.MaterialSources[1].SourceWadEntry} plan={disc.PlanSha256} fullImage={Images[i]} rawOwners=1290 allOriginalSectors=281270 outputWritten=false");
        }
        check(plans.Select(p=>p.PlanSha256).Distinct().Count()==3,"Ordered material choices retain three distinct actual Disc identities.");
        await Refuse<InvalidDataException>(()=>{plans[0].RequireAncestry(executables[1]);return Task.CompletedTask;},check);
        var equal=await NativeId65FreshWizardResearchExecutablePlan.CaptureMaterialAuthoredAsync(bin,executables[0].BodyAncestor);
        check(!ReferenceEquals(equal,executables[0])&&equal.PlanSha256==executables[0].PlanSha256&&
            ReferenceEquals(equal.BodyAncestor,executables[0].BodyAncestor)&&ReferenceEquals(equal.MixedResourceAncestor,executables[0].MixedResourceAncestor),
            "Actual equal EXE reissue holds the same exact Body/mixed/document/source owners fixed.");
        await Refuse<InvalidDataException>(()=>{plans[0].RequireAncestry(equal);return Task.CompletedTask;},check);
        var own=await NativeId65FreshWizardResearchDiscPlan.CaptureMaterialAuthoredAsync(bin,equal);
        own.RequireAncestry(equal);
        check(own.PlanSha256==plans[0].PlanSha256&&ReferenceEquals(own.ExecutableAncestor,equal),
            "Equal reissued EXE may issue its own Disc but cannot replace another Disc's EXE.");
        await Refuse<InvalidDataException>(()=>{own.RequireAncestry(executables[0]);return Task.CompletedTask;},check);
        await MixedExecutableCases.TinyFailure(path=>NativeId65FreshWizardResearchDiscPlan.CaptureMaterialAuthoredAsync(path,executables[0]),
            check,"Fresh-disc capture requires an exact-length seekable original source.","disc");
        check(executables.Select(e=>e.PlanSha256).SequenceEqual(identities),"Every incoming executable identity survives Disc capture/refusal unchanged.");
    }

    private static JsonDocument ReadOracle(Action<bool,string> check)
    {
        // Called only in explicit source mode. Default/free checks never read
        // an original image or this private original-only evidence packet.
        string? path=null;
        foreach(string start in new[]{Directory.GetCurrentDirectory(),AppContext.BaseDirectory})
            for(DirectoryInfo? folder=new(start);folder is not null;folder=folder.Parent)
            {
                string candidate=Path.Combine(folder.FullName,OracleName);
                if(File.Exists(candidate)){path=candidate;break;}
            }
        if(path is null)throw new FileNotFoundException("Pinned original-only mixed Disc oracle is required in source mode.",OracleName);
        byte[] bytes=File.ReadAllBytes(path);
        check(bytes.Length==1126203&&Hash(bytes)==OracleHash,"Complete independent original-only oracle bytes remain pinned before parse.");
        return JsonDocument.Parse(bytes);
    }

    private static void CheckComponents(NativeId65FreshWizardResearchDiscPlan disc,JsonElement expected,JsonElement physical,
        Action<bool,string> check)
    {
        var ancestor=disc.ExecutableAncestor.BodyAncestor;byte[] body=disc.CopyBody();
        check(body.Length==0x20C800&&disc.FinalExecutableLba==54958&&disc.FinalExecutableEndExclusive==55162&&
            disc.FinalWadByteLength==0x6B44800&&disc.CopyOverlay().Length==0x11000&&disc.CopyExecutable().Length==0x66000,
            "Fixed Host/Body/WAD/EXE physical allocations and relocation retain exact geometry.");
        check(Hash(body)==expected.GetProperty("bodySha256").GetString()&&Hash(body.AsSpan(0,0x800))==expected.GetProperty("headerSha256").GetString()&&
            Hash(disc.CopyExecutable())==expected.GetProperty("executableSha256").GetString()&&
            Hash(disc.CopyIsoRootDirectory())==physical.GetProperty("rootSha256").GetString()&&
            Hash(disc.CopyWadDirectory())==physical.GetProperty("wadIndexSha256").GetString(),
            "Actual complete Body/header/EXE/root/WAD hashes equal frozen original-only component expectations.");
        var descriptors=expected.GetProperty("descriptors").EnumerateArray().ToArray();
        check(descriptors.Length==8&&ancestor.Descriptors.Count==8,"Complete eight physical descriptors retained.");
        foreach(var descriptor in descriptors)
        {
            int i=descriptor.GetProperty("index").GetInt32(),offset=descriptor.GetProperty("offset").GetInt32(),length=descriptor.GetProperty("length").GetInt32();
            check(ancestor.Descriptors[i].Offset==offset&&ancestor.Descriptors[i].ByteLength==length&&
                Hash(body.AsSpan(offset,length))==descriptor.GetProperty("sha256").GetString(),
                "Complete actual physical allocation matches original-only oracle, including auxiliary padding.");
        }
        byte[] wad=disc.CopyWadDirectory();
        check(BinaryPrimitives.ReadInt32LittleEndian(wad.AsSpan(79*8))==0x6927000&&
            BinaryPrimitives.ReadInt32LittleEndian(wad.AsSpan(79*8+4))==0x11000&&
            BinaryPrimitives.ReadInt32LittleEndian(wad.AsSpan(80*8))==0x6938000&&
            BinaryPrimitives.ReadInt32LittleEndian(wad.AsSpan(80*8+4))==0x20C800,
            "Actual reserved WAD entries79/80 preserve Host/Body boundaries.");
        var copies=new[]{body,disc.CopyOverlay(),disc.CopyExecutable(),wad,disc.CopyIsoRootDirectory()};
        string[] hashes=copies.Select(b=>Hash(b)).ToArray();foreach(byte[] copy in copies){copy[0]^=1;copy[^1]^=1;}
        check(new[]{disc.CopyBody(),disc.CopyOverlay(),disc.CopyExecutable(),disc.CopyWadDirectory(),disc.CopyIsoRootDirectory()}
            .Select(b=>Hash(b)).SequenceEqual(hashes),"Every complete Disc payload copy is detached.");
    }

    // Actual shared writer bytes enter the existing hash sink. This adapter
    // independently checks every sector against its own original reader and
    // the pinned oracle, without generating or retaining an expected image.
    private sealed class OwnerSink(Stream original,NativeId65FreshWizardResearchDiscPlan disc,
        IReadOnlyDictionary<int,string> changed):Stream
    {
        private readonly SeparatedCandidateCases.HashSink sink=new();
        private readonly IncrementalHash restoredHash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        private int sectors,changedCount,untouchedCount;
        public override bool CanRead=>false;public override bool CanSeek=>true;public override bool CanWrite=>true;
        public override long Length=>sink.Length;public override long Position{get=>sink.Position;set=>throw new NotSupportedException();}
        internal string Digest()=>sink.Digest();
        public override void Write(byte[] bytes,int offset,int count)=>Write(bytes.AsSpan(offset,count));
        public override void Write(ReadOnlySpan<byte> after)
        {
            int lba=sectors;byte[] before=new byte[2352];original.ReadExactly(before);
            if(after.Length!=2352||original.Position!=(lba+1)*2352L||original.Length!=661547040)
                throw new InvalidDataException("Mixed sink original pair boundary changed.");
            if(changed.TryGetValue(lba,out string? hash))
            {
                if(before.AsSpan().SequenceEqual(after)||Hash(after)!=hash)throw new InvalidDataException("Mixed raw oracle mismatch at LBA "+lba);
                RawMode2Form1SectorIntegrity.RequireRawSectorIntegrity(after,lba);changedCount++;
            }
            else
            {
                if(!before.AsSpan().SequenceEqual(after))throw new InvalidDataException("Mixed untouched complement changed at LBA "+lba);
                untouchedCount++;
            }
            byte[] restored=disc.CreateOriginalRawSector(lba,after);
            if(!before.AsSpan().SequenceEqual(restored))throw new InvalidDataException("Mixed inverse failed at LBA "+lba);
            restoredHash.AppendData(restored);sink.Write(after);sectors++;
        }
        internal void RequireComplete(Action<bool,string> check)
        {
            check(sectors==281270&&changedCount==1290&&untouchedCount==279980,
                "Every original sector audited:1290 exact raw oracle/parity owners and279980 byte-identical untouched owners.");
            check(original.ReadByte()==-1&&original.Position==661547040&&original.Length==661547040&&
                Convert.ToHexStringLower(restoredHash.GetHashAndReset())==NativeId65FreshDiscSource.OriginalSha256,
                "Every sector inverse restores authenticated original bytes with exact complete SHA and real EOF.");
        }
        protected override void Dispose(bool disposing){if(disposing){sink.Dispose();restoredHash.Dispose();}base.Dispose(disposing);}
        public override void Flush(){}public override void SetLength(long length)=>throw new NotSupportedException();
        public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException();
        public override int Read(byte[] bytes,int offset,int count)=>throw new NotSupportedException();
    }

    private static async Task Refuse<T>(Func<Task> action,Action<bool,string> check,string? prefix=null)where T:Exception
    {
        try{await action();}
        catch(T error)
        {
            try{check(prefix is null||error.Message.StartsWith(prefix,StringComparison.Ordinal),"Expected mixed Disc refusal "+typeof(T).Name);}
            catch(Exception assertion){throw new AggregateException("Disc refusal assertion failed; actual retained once.",assertion,error);}
            return;
        }
        throw new Exception("Expected "+typeof(T).Name);
    }
    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
}
