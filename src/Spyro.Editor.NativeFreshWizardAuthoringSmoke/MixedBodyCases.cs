using System.Reflection;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;

// PRIVATE tests first. No build, source execution, output or native claim.
internal static class MixedBodyCases
{
    private const string BodyRequired="Mixed body requires actual schema2 material binding.";
    private const string ExeRefusal="Mixed material Body executable composition is not enabled.";
    private const string Header="fe3757b0f68a8b7003d4a9e4660bc9e2b643fbadc6f1e71123bca29e26ec2ca8";
    private const string Transfer="92820254671062b6b20e2cfd2c243faac32ae6223e22d50d06221b4be8539883";
    private static readonly int[] Lengths=[0xE5800,0x1000,0x19000,0x3000,0x49800,0x42800,0x5D000,0x20800];
    private static readonly string[] BodyHashes=[
        "a74ecbd7772a1a1880af78d15aa0137709f856ff943b4071a46a2f22d83e9aa2",
        "add5896a172226658a9e6537952a84c9062c3684da4924086a85602d505630a8",
        "8ac55a3b6f47caa79f4946c3158d2dc0d3fbe66f3836513b616a4b5b0840c214"];
    private static readonly string[] PaddedDataHashes=[
        "4111cda88bb5c20fcbbbd235d07bfbf9ca6af9bfe0f2ed6e577f9a110b489976",
        "15df61d01db0fcea33d9398abb94a404ae0442b7a68c3986f0eb6ad1281ab02f",
        "895cc66d8bd212618199ede3231ef5f3676be2abc23437f91b4285e36303100a"];
    private static readonly string[] AuxiliaryHashes=[
        "bdf27988f3d2b0fd4c4b905636b6980aff49ab1475d85edb4ce8aa2cc469351f",
        "23968146e46b3263997e16692a192bbdb049966b010673d17611f2ab6903cef0",
        "402bb40e21bf84922cbc35312b0e49b95292a2f96326636aba05685443686991",
        "eabadb4ea663043665bc4ba2d2e00c1c7c104688906b56bac2d7aba7c6cae8da"];

    internal static async Task Free(Action<bool,string> check)
    {
        var method=typeof(NativeId65FreshWizardResearchBodyPlan).GetMethod("CaptureMaterialAuthoredAsync",
            BindingFlags.Static|BindingFlags.NonPublic);
        check(method is {IsAssembly:true}&&method.ReturnType==typeof(Task<NativeId65FreshWizardResearchBodyPlan>)&&
            method.GetParameters().Select(p=>p.ParameterType).SequenceEqual(
                new[]{typeof(string),typeof(NativeFreshWizardMaterialResource),typeof(CancellationToken)}),
            "One internal actual-component Body factory; no new public producer or recipe class.");
        using var cancelled=new CancellationTokenSource();cancelled.Cancel();
        // Assignment outside Refuse requires asynchronous Task delivery, not a synchronous throw.
        Task cancelledTask=NativeId65FreshWizardResearchBodyPlan.CaptureMaterialAuthoredAsync("must-not-open",null!,cancelled.Token);
        await Refuse<OperationCanceledException>(()=>cancelledTask,check);
        Task nullTask=NativeId65FreshWizardResearchBodyPlan.CaptureMaterialAuthoredAsync("must-not-open",null!);
        await Refuse<ArgumentNullException>(()=>nullTask,check);
        await Refuse<ArgumentNullException>(()=>{
            _=NativeId65FreshWizardBodyLayout.CompileMaterialAuthored(null!,new UntouchedAuxiliary());return Task.CompletedTask;
        },check);
    }

    // Hook inside existing schema1 MixedResourceCases loop: no new capture.
    internal static async Task Schema1Refusal(NativeFreshWizardMaterialResource mixed,Action<bool,string> check)
    {
        check(mixed.SourceCertified&&!mixed.Schema2MaterialBindingProven,"This refusal uses a genuine issued schema1 mixed component.");
        await Refuse<InvalidDataException>(()=>NativeId65FreshWizardResearchBodyPlan.CaptureMaterialAuthoredAsync(
            "must-not-open",mixed),check,BodyRequired);
        await Refuse<InvalidDataException>(()=>{
            _=NativeId65FreshWizardBodyLayout.CompileMaterialAuthored(mixed,new UntouchedAuxiliary());return Task.CompletedTask;
        },check,BodyRequired);
    }

    // Reuses actual three pairs and same-byte own component retained by Schema2MaterialAncestryCases.
    internal static async Task Source(string bin,IReadOnlyList<NativeFreshWizardMaterialResource> mixed,
        NativeFreshWizardMaterialResource own,Action<bool,string> check)
    {
        check(mixed.Count==3&&mixed.Select(m=>(m.MaterialSources[0].SourceWadEntry,m.MaterialSources[1].SourceWadEntry))
            .SequenceEqual(new[]{(14,70),(70,14),(14,14)}),"Exact three previously issued schema2 components, in oracle order.");
        var bodies=new List<NativeId65FreshWizardResearchBodyPlan>();
        for(int i=0;i<3;i++)
        {
            var resource=mixed[i];var host=resource.HostAncestor;var scene=resource.SceneAncestor;
            var body=await NativeId65FreshWizardResearchBodyPlan.CaptureMaterialAuthoredAsync(bin,resource);
            bodies.Add(body);body.RequireMixedAncestry(resource);body.RequireAncestry(host);
            var retained=body.MixedResourceAncestor;
            check(body.IsMaterialAuthored&&retained is not null&&ReferenceEquals(retained,resource)&&
                ReferenceEquals(body.HostAncestor,host)&&ReferenceEquals(retained.DocumentAncestor,resource.DocumentAncestor)&&
                ReferenceEquals(retained.MaterialSources[0],resource.MaterialSources[0])&&
                ReferenceEquals(retained.MaterialSources[1],resource.MaterialSources[1])&&
                body.CanonicalDocumentSha256==scene.CanonicalDocumentSha256,
                "Body retains one exact component and its complete document/Host/ordered source chain; BASE canonical meaning is unchanged.");
            byte[] bytes=body.CopyLevelData(),overlay=body.CopyOverlay(),initializer=body.CopyInitializerPatch(),transfer=body.CopyTransferSourceImage();
            body.RequireExactOutputs(bytes,overlay,initializer,transfer);
            check(bytes.Length==0x20C800&&body.LevelDataSha256==BodyHashes[i]&&Hash(bytes)==BodyHashes[i]&&
                body.HeaderSha256==Header&&Hash(bytes.AsSpan(0,0x800))==Header,
                "Complete actual C# Body equals independent original-only oracle; no generated expectation is substituted.");
            check(body.Descriptors.Count==8&&body.Descriptors.Select(d=>d.ByteLength).SequenceEqual(Lengths)&&
                body.Descriptors.Select(d=>d.Index).SequenceEqual(Enumerable.Range(0,8))&&
                body.Descriptors.Where(d=>d.RetainedAuxiliary).Select(d=>d.Index).SequenceEqual(new[]{4,5,6,7}),
                "Eight exact physical descriptors, complete four auxiliary allocations and fixed CD geometry.");
            int next=0x800;
            foreach(var d in body.Descriptors){check(d.Offset==next,"Physical owner offset stays unchanged.");next+=d.ByteLength;}
            check(next==bytes.Length&&NativeId65FreshWizardBodyLayout.DecodeHeader(bytes.AsSpan(0,0x800)).SequenceEqual(body.Descriptors),
                "Fixed header grammar covers the complete physical Body, including reserved complement.");
            check(bytes.AsSpan(0x800,0xE5800).SequenceEqual(resource.CopyUploadPayload())&&
                bytes.AsSpan(0xE6000,0xA6C).SequenceEqual(resource.CopyDataComponents())&&
                Hash(bytes.AsSpan(0xE6000,0x1000))==PaddedDataHashes[i]&&
                !bytes.AsSpan(0xE6A6C,0x594).ContainsAnyExcept((byte)0)&&
                bytes.AsSpan(0xE7000,0x19000).SequenceEqual(resource.CopyModelPayload())&&
                bytes.AsSpan(0x100000,0x3000).SequenceEqual(resource.CopyScene()),
                "Actual mixed data ends A6C with594 zero CD padding; all model/scene bytes retain their issued owner.");
            check(Hash(bytes.AsSpan(0xE7000,0x19000))=="84d00dbbce264bc08c61abfe2579fe3d7a7464ba2df6b189834664da10b82c84"&&
                Hash(bytes.AsSpan(0x100000,0x3000))=="08718e7594702ab21e17f49c459b99cf4e35c8190258abbcaaf720106a880343",
                "Independent complete model/scene hashes remain literal.");
            check(body.ModelRoots.Select(r=>(r.ClassId,r.ModelRelativeOffset,r.PayloadByteLength,r.RegistrationAllocationByteLength))
                .SequenceEqual(host.FallbackAncestor.ModelRoots.Select(r=>((ushort?)r.ClassId,r.Offset,r.ByteLength,r.RegistrationExtentByteLength)))&&
                body.ModelRoots.All(r=>r.EntryRelativeOffset==0xE7000+r.ModelRelativeOffset),
                "All seven model roots retain actual fallback payload/registration extents and unchanged physical offsets.");
            foreach(var d in body.Descriptors.Where(d=>d.RetainedAuxiliary))
                check(Hash(bytes.AsSpan(d.Offset,d.ByteLength))==AuxiliaryHashes[d.Index-4],
                    "Complete original auxiliary hash, including source padding, equals the independent literal.");
            RequireOwners(body,bytes,check);
            check(body.CopyBufferAddress==0x8008BA38&&body.ModelRuntimeAddress==0x8008C4A4&&
                body.SceneRuntimeAddress==0x800A54A4&&body.KnownSceneEndAddress==0x800A84A4&&
                body.ModelRuntimeAddress==checked(body.CopyBufferAddress+0xA6Cu)&&
                body.SceneRuntimeAddress==checked(body.ModelRuntimeAddress+0x19000u)&&
                body.KnownSceneEndAddress==checked(body.SceneRuntimeAddress+0x3000u)&&
                body.ResidentPeteAddress==0x801C5BB8&&body.NormalReservedLowerAddress==0x80187BB0&&
                body.KnownSceneEndAddress<body.NormalReservedLowerAddress&&body.CopyBufferAddress+0x80000<body.NormalReservedLowerAddress,
                "Actual used-length arithmetic matches the original-only native consumer endpoints, without shifting shared PETE.");
            var dataRead=body.ReadPhases.Single(p=>p.Id=="data-read");
            check(dataRead.RuntimeAddress==body.CopyBufferAddress&&dataRead.ByteLength==0x1000&&
                dataRead.NativeOperation.Contains("A6C",StringComparison.OrdinalIgnoreCase)&&
                body.ReadPhases.Single(p=>p.Id=="model-read").RuntimeAddress==body.ModelRuntimeAddress&&
                body.ReadPhases.Single(p=>p.Id=="scene-read").RuntimeAddress==body.SceneRuntimeAddress&&
                body.PhaseOverlaps.Single().RuntimeAddress==body.ModelRuntimeAddress&&body.PhaseOverlaps.Single().ByteLength==0x594&&
                body.ReadPhases.All(p=>p.RequiresExternalCompletion),
                "Read descriptions and discarded-padding overlap match new used data; CD/hardware completion is still external.");
            check(body.ReadPhases.Single(p=>p.Id=="cpu-spu-source").ByteLength==0x7EFF0&&
                body.ReadPhases.Single(p=>p.Id=="dma-spu-source").ByteLength==0x7F000&&
                transfer.Length==0x7F000&&Hash(transfer)==Transfer&&body.TransferSourceSha256==Transfer&&
                transfer.SequenceEqual(resource.CopyTransferSourceImage())&&
                bytes.AsSpan(0x800+0x65800,0x19800).SequenceEqual(host.FallbackAncestor.CopyUploadPayload().AsSpan(0x65800,0x19800))&&
                bytes.AsSpan(0x80800,0x65800).SequenceEqual(host.FallbackAncestor.CopyUploadPayload().AsSpan(0x80000,0x65800)),
                "Complete fallback bank, full65800:7F000 residue and DMA-only16 preserve928202 transfer.");
            check(overlay.SequenceEqual(host.CopyOverlay())&&initializer.SequenceEqual(host.CopyInitializerPatch())&&
                !body.FinalExecutableIssued&&!body.InstalledDispatchProven&&!body.RuntimeVerified&&!body.PublicationApproved&&
                !body.PersistentOwnerIssued&&!body.CompleteNativeDependencyClosureProven&&!body.CompleteLevelResourcesProven&&
                !body.GlobalAudioCompatibilityProven&&!body.PeakRuntimeMemoryProven&&!body.HardwareTransfersProven&&
                !body.CompleteIncomingEdgeClosureProven&&!resource.BodyIssued,
                "Only detached Body issuance advances; Host copies and every downstream boundary remain unchanged.");
            await Refuse<InvalidDataException>(()=>{NativeId65FreshWizardResearchExecutableSource.RequireBody(body);return Task.CompletedTask;},check,ExeRefusal);
            await Refuse<InvalidDataException>(()=>NativeId65FreshWizardResearchExecutablePlan.CaptureAsync("must-not-open",body),check,ExeRefusal);
            await Refuse<InvalidDataException>(()=>NativeId65FreshWizardResearchBodyPlan.CaptureAsync("must-not-open",host),check,
                "Material-authored Scene requires its mixed body");
            await Refuse<InvalidDataException>(()=>{_=NativeId65FreshWizardBodyLayout.CompileAuthored(host,new UntouchedAuxiliary());return Task.CompletedTask;},
                check,"Material-authored Scene requires its mixed body");
            await Refuse<InvalidDataException>(()=>{NativeId65FreshWizardBodyLayout.ValidateStructure(bytes);return Task.CompletedTask;},check);
            byte[][] auxiliary=body.Descriptors.Where(d=>d.RetainedAuxiliary).Select(d=>bytes.AsSpan(d.Offset,d.ByteLength).ToArray()).ToArray();
            var layout=NativeId65FreshWizardBodyLayout.CompileMaterialAuthored(resource,auxiliary);
            check(!layout.SourceCertified&&layout.CopyLevelData().SequenceEqual(bytes),"Same production packing round trip only; full oracle comparison is above.");
            auxiliary[0][0]^=1;
            check(layout.CopyLevelData().SequenceEqual(bytes),"Caller auxiliary arrays are defensively detached.");
            await Refuse<InvalidDataException>(()=>{_=NativeId65FreshWizardBodyLayout.CompileMaterialAuthored(resource,auxiliary);return Task.CompletedTask;},check);
            await Refuse<InvalidDataException>(()=>{_=NativeId65FreshWizardBodyLayout.CompileMaterialAuthored(resource,[]);return Task.CompletedTask;},check);
            var shortAuxiliary=body.Descriptors.Where(d=>d.RetainedAuxiliary).Select(d=>bytes.AsSpan(d.Offset,d.ByteLength).ToArray()).ToArray();
            shortAuxiliary[3]=shortAuxiliary[3][..^1];
            await Refuse<InvalidDataException>(()=>{_=NativeId65FreshWizardBodyLayout.CompileMaterialAuthored(resource,shortAuxiliary);return Task.CompletedTask;},check);
            foreach(int at in new[]{0,0x50,0x150,0x800,0xE6000,0xE6A6B,0xE6A6C,0xE6FFF,0xE7000,0x100000,0x20C7FF})
            {
                byte[] changed=bytes.ToArray();changed[at]^=1;
                await Refuse<InvalidDataException>(()=>{body.RequireExactOutputs(changed,overlay,initializer,transfer);return Task.CompletedTask;},check);
            }
            await Refuse<InvalidDataException>(()=>{body.RequireExactOutputs(bytes[..^1],overlay,initializer,transfer);return Task.CompletedTask;},check);
            var changedTransfer=transfer.ToArray();changedTransfer[^1]^=1;
            await Refuse<InvalidDataException>(()=>{body.RequireExactOutputs(bytes,overlay,initializer,changedTransfer);return Task.CompletedTask;},check);
            bytes[0]^=1;overlay[0]^=1;initializer[0]^=1;transfer[0]^=1;
            check(Hash(body.CopyLevelData())==BodyHashes[i]&&Hash(body.CopyTransferSourceImage())==Transfer&&
                body.CopyOverlay().SequenceEqual(host.CopyOverlay())&&body.CopyInitializerPatch().SequenceEqual(host.CopyInitializerPatch()),
                "All returned complete body/Host/transfer arrays are detached copies.");
            Console.WriteLine($"SCHEMA2_MIXED_BODY_SOURCE pair={resource.MaterialSources[0].SourceWadEntry}/{resource.MaterialSources[1].SourceWadEntry} body={body.LevelDataSha256} finalExecutableIssued=false");
        }
        check(!ReferenceEquals(own,mixed[0])&&own.PlanSha256==mixed[0].PlanSha256&&ReferenceEquals(own.HostAncestor,mixed[0].HostAncestor)&&
            ReferenceEquals(own.DocumentAncestor,mixed[0].DocumentAncestor),"Equal reissue is a genuine component, with the same Host/document.");
        await Refuse<InvalidDataException>(()=>{bodies[0].RequireMixedAncestry(own);return Task.CompletedTask;},check);
        await Refuse<InvalidDataException>(()=>{bodies[0].RequireAncestry(mixed[1].HostAncestor);return Task.CompletedTask;},check);
        var sameInputsAgain=await NativeFreshWizardMaterialResource.CaptureMaterialAuthoredAsync(bin,mixed[0].HostAncestor,
            mixed[0].MaterialSources[0],mixed[0].MaterialSources[1]);
        check(!ReferenceEquals(sameInputsAgain,mixed[0])&&sameInputsAgain.PlanSha256==mixed[0].PlanSha256&&
            ReferenceEquals(sameInputsAgain.HostAncestor,mixed[0].HostAncestor)&&
            ReferenceEquals(sameInputsAgain.DocumentAncestor,mixed[0].DocumentAncestor)&&
            ReferenceEquals(sameInputsAgain.MaterialSources[0],mixed[0].MaterialSources[0])&&
            ReferenceEquals(sameInputsAgain.MaterialSources[1],mixed[0].MaterialSources[1]),
            "Exact mixed-instance control holds every actual nested ancestor reference equal.");
        await Refuse<InvalidDataException>(()=>{bodies[0].RequireMixedAncestry(sameInputsAgain);return Task.CompletedTask;},check);
        var ownBody=await NativeId65FreshWizardResearchBodyPlan.CaptureMaterialAuthoredAsync(bin,sameInputsAgain);
        check(ownBody.PlanSha256==bodies[0].PlanSha256&&ownBody.LevelDataSha256==bodies[0].LevelDataSha256&&
            ReferenceEquals(ownBody.MixedResourceAncestor,sameInputsAgain),"A same-input reissued component may issue its own Body, not replace the first component.");
        await Refuse<InvalidDataException>(()=>{ownBody.RequireMixedAncestry(mixed[0]);return Task.CompletedTask;},check);
        await TinyFailure(path=>NativeId65FreshWizardResearchBodyPlan.CaptureMaterialAuthoredAsync(path,mixed[0]),check);
        await MixedExecutableCases.Source(bin,bodies,ownBody,check);
    }

    private static void RequireOwners(NativeId65FreshWizardResearchBodyPlan body,byte[] bytes,Action<bool,string> check)
    {
        int cursor=0;
        foreach(var owner in body.Owners)
        {
            check(owner.Offset==cursor&&owner.ByteLength>0&&owner.ByteLength<=bytes.Length-cursor&&
                owner.Sha256==Hash(bytes.AsSpan(owner.Offset,owner.ByteLength)),"Every actual Body owner is consecutive, positive, bounded and fully hashed.");
            cursor+=owner.ByteLength;
        }
        var used=body.Owners.Single(o=>o.Offset==0xE6000);
        var padding=body.Owners.Single(o=>o.Offset==0xE6A6C);
        check(cursor==bytes.Length&&used.ByteLength==0xA6C&&padding.ByteLength==0x594&&
            used.Provenance=="actual-issued-mixed-resource"&&body.Owners.Single(o=>o.Offset==0x800).Provenance=="actual-issued-mixed-resource"&&
            body.Evidence.Any(e=>e.Domain=="actual-issued-mixed-resource"&&e.ByteLength==0xA6C&&e.Sha256==used.Sha256),
            "Whole Body owner census and resource evidence report actual mixed used data, not stale fallback attribution.");
        foreach(var d in body.Descriptors.Where(d=>d.RetainedAuxiliary))
        {
            int next=d.Offset;
            foreach(var o in body.AuxiliaryOwners.Where(o=>o.DescriptorIndex==d.Index))
            {
                check(o.BodyRelativeOffset==next&&o.AuxiliaryRelativeOffset==next-d.Offset&&o.ByteLength>0&&
                    o.ByteLength<=d.Offset+d.ByteLength-next&&Hash(bytes.AsSpan(next,o.ByteLength))==o.Sha256,
                    "Complete real auxiliary subowner partition and byte hashes.");next+=o.ByteLength;
            }
            check(next==d.Offset+d.ByteLength,"Every complete auxiliary suffix accounted.");
        }
        var changed=body.Owners.ToArray();changed[2]=changed[2] with{ByteLength=changed[2].ByteLength-1};
        try{body.RequireOwners(changed);throw new Exception("Expected changed owner refusal.");}catch(InvalidDataException){check(true,"Changed owner length cannot replace retained attribution.");}
        body.RequireOwners(body.Owners);
    }

    private static async Task TinyFailure(Func<string,Task> capture,Action<bool,string> check)
    {
        var directory=Directory.CreateTempSubdirectory("spyro-schema2-body-refusal-");string path=Path.Combine(directory.FullName,"tiny.bin");
        Console.WriteLine($"SCHEMA2_BODY_REFUSAL_FIXTURE_RETAINED path={path}");
        using(var created=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None))created.Write([17,29]);
        // Always retain exact fixture or unexpected evidence; never delete.
        Exception? actual=null;List<Exception> failures=[];
        try{await capture(path);}catch(Exception error){actual=error;}
        try
        {
            check(actual is IOException{InnerException:AggregateException},"Actual initial source refusal retains its error tree.");
            var leaves=((AggregateException)actual!.InnerException!).InnerExceptions;
            check(leaves.Count==3&&leaves.All(e=>e is InvalidDataException&&
                e.Message.StartsWith("Original snapshot length/initial position",StringComparison.Ordinal))&&
                !ReferenceEquals(leaves[0],leaves[1])&&!ReferenceEquals(leaves[0],leaves[2])&&!ReferenceEquals(leaves[1],leaves[2]),
                "Actual primary, None held and None named final failures are pairwise distinct.");
        }
        catch(Exception error){failures.Add(error);}
        FileStream? reopened=null;
        try
        {
            reopened=new FileStream(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None);
            byte[] bytes=new byte[2];reopened.ReadExactly(bytes);
            check(bytes.SequenceEqual(new byte[]{17,29})&&reopened.Length==2&&reopened.ReadByte()==-1,
                "Actual source owners released; two bytes and true EOF remain unchanged.");
        }
        catch(Exception error){failures.Add(error);}
        finally{if(reopened!=null)try{reopened.Dispose();}catch(Exception error){failures.Add(error);}}
        if(failures.Count!=0)
            throw new AggregateException("Tiny source controls failed; assertion/action/close first, actual source tree once.",
                failures.Concat(actual is null?Array.Empty<Exception>():new[]{actual}));
    }

    private static async Task Refuse<T>(Func<Task> action,Action<bool,string> check,string? prefix=null) where T:Exception
    {
        try{await action();}
        catch(T error)
        {
            try{check(prefix==null||error.Message.StartsWith(prefix,StringComparison.Ordinal),"Expected exact refusal "+typeof(T).Name);}
            catch(Exception assertion){throw new AggregateException("Refusal assertion failed; actual error retained once.",assertion,error);}
            return;
        }
        throw new Exception("Expected "+typeof(T).Name);
    }
    private sealed class UntouchedAuxiliary:IReadOnlyList<byte[]>
    {
        public int Count=>throw new IOException("Auxiliary count must not be observed before producer refusal.");
        public byte[] this[int index]=>throw new IOException("Auxiliary item must not be observed before producer refusal.");
        public IEnumerator<byte[]> GetEnumerator()=>throw new IOException("Auxiliary enumeration must not precede producer refusal.");
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()=>GetEnumerator();
    }
    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
}
