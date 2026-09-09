using System.Buffers.Binary;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;

internal static class MixedResourceCases
{
    // Independent original-only09 and Root10, not newly issued C# output.
    private static readonly (string Name, int A, int B, string Upload, string Data, int Bytes, int Aliases)[] Goldens =
    [
        ("primary",14,70,"a5867f5ecf74a2c54e56553b0eba6dea17c055f478ddb18ff3e2a7c83c3ec415",
            "ddf2dcfa034ec160df4813e6584fb78af2ba713682631d72058a8477288f5e88",27648,16),
        ("reversed",70,14,"6fa0965dae5ecedacdfaae4dcb3d3bd89ee8e197bf0b66a05a7def1db8792220",
            "b7559eed9825c2f70b6d21e24b44185e9d504e8a4fabbf2c24080ac86ed7bacd",27648,16),
        ("same-source",14,14,"ff5e14fb2356f9dd633994505bf73bb79735bad84a9deae653ff5f53d6368040",
            "a63ad93de2b4a8a59965b8c2a5e9c016f6c089e5ee4c98df5a065a7ceae92acd",44032,0)
    ];
    private const string Transfer = "92820254671062b6b20e2cfd2c243faac32ae6223e22d50d06221b4be8539883";
    private static string Hash(ReadOnlySpan<byte> b) => NativeId65FreshWizardResearchResourceSource.Hash(b);

    internal static async Task Free(Action<bool,string> check)
    {
        // Intended missing-API compile RED when only this test/hook is promoted.
        check(typeof(NativeFreshWizardMaterialResource).IsSealed &&
            !typeof(NativeFreshWizardMaterialResource).IsPublic,
            "A single internal closed resource component, not a new public recipe/candidate class.");
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Refuse<OperationCanceledException>(() =>
            NativeFreshWizardMaterialResource.CaptureAsync("must-not-open",null!,null!,null!,cancelled.Token),check);
        await Refuse<ArgumentNullException>(() =>
            NativeFreshWizardMaterialResource.CaptureAsync("must-not-open",null!,null!,null!),check);

        var bytes = new byte[][] { new byte[2], new byte[1], new byte[1] };
        var owners = new[]
        {
            new NativeFreshWizardResourceOwner("u",NativeFreshWizardResourceDomain.Upload,0,2,Hash(bytes[0]),"fixture",null,null),
            new NativeFreshWizardResourceOwner("d",NativeFreshWizardResourceDomain.Data,0,1,Hash(bytes[1]),"fixture",null,null),
            new NativeFreshWizardResourceOwner("m",NativeFreshWizardResourceDomain.Models,0,1,Hash(bytes[2]),"fixture",null,null)
        };
        NativeFreshWizardMaterialResource.RequirePartition(owners,bytes);
        check(true,"Pure positive-extent/hash partition check is not issuance or free-space authority.");
        foreach (var changed in new[] {
            owners[..^1], owners.Append(owners[0]).ToArray(),
            owners.Select((o,i)=>i==0?o with {Offset=1}:o).ToArray(),
            owners.Select((o,i)=>i==0?o with {ByteLength=0}:o).ToArray(),
            owners.Select((o,i)=>i==0?o with {ByteLength=int.MaxValue}:o).ToArray(),
            owners.Select((o,i)=>i==0?o with {Sha256=new string('f',64)}:o).ToArray(),
            owners.Select((o,i)=>i==0?o with {Domain=(NativeFreshWizardResourceDomain)99}:o).ToArray() })
            await Refuse<InvalidDataException>(()=> {NativeFreshWizardMaterialResource.RequirePartition(changed,bytes);return Task.CompletedTask;},check);
        foreach(int n in new[]{0,0x8C7,0x8C9,0x8FB,0x8FD,0x1000})
            await Refuse<InvalidDataException>(()=> {
                NativeStaticTerrainGpuAllocation.ComposeTwoFaceMetadata(new byte[n],Array.Empty<NativeStaticTerrainTextureLayout>());
                return Task.CompletedTask;
            },check);
        byte[] record=new byte[184];
        for(int i=0;i<23;i++)
        {
            record[i*8+2]=32;record[i*8+4]=31;
            record[i*8+6]=i<3?(byte)8:(byte)0x88;record[i*8+7]=i<3?(byte)0x80:(byte)0;
        }
        var layout=NativeStaticTerrainTextureLayout.Decode(record);
        foreach(int n in new[]{0x8C8,0x8FC})
        {
            // Arbitrary neutral tail bytes deliberately carry no source grammar
            // authority; actual adapter input checks remain separate and exact.
            byte[] before=Enumerable.Range(0,n).Select(i=>(byte)(i*17)).ToArray();
            var output=NativeStaticTerrainGpuAllocation.ComposeTwoFaceMetadata(before,[layout,layout]);
            bool all=true;
            for(int i=8;i<n;i++)if(i is not (>=0x98 and <0x9C) and not (>=0xA8 and <0xAC))all &= output[i+0x170]==before[i];
            check(output.Length==n+0x170&&all&&BinaryPrimitives.ReadInt32LittleEndian(output)==0x178&&
                BinaryPrimitives.ReadInt32LittleEndian(output.AsSpan(4))==2&&
                BinaryPrimitives.ReadInt32LittleEndian(output.AsSpan(0x208))==0&&
                BinaryPrimitives.ReadInt32LittleEndian(output.AsSpan(0x218))==1,
                "Both neutral metadata extents retain every translated tail byte and exact new prefix/face fields.");
            for(int slot=0;slot<2;slot++)check(output.AsSpan(8+slot*16,16).SequenceEqual(record.AsSpan(0,16))&&
                output.AsSpan(40+slot*168,168).SequenceEqual(record.AsSpan(16)),"Both complete decoded184-byte metadata records retained.");
        }
        foreach(var records in new[]{Array.Empty<NativeStaticTerrainTextureLayout>(),new[]{layout},new[]{layout,layout,layout},new[]{layout,null!}})
            await Refuse<InvalidDataException>(()=> {NativeStaticTerrainGpuAllocation.ComposeTwoFaceMetadata(new byte[0x8FC],records);return Task.CompletedTask;},check);
        await Refuse<ArgumentNullException>(()=> {NativeStaticTerrainGpuAllocation.ComposeTwoFaceMetadata(new byte[0x8FC],null!);return Task.CompletedTask;},check);
    }

    // Caller supplies the actual existing source-created separatedB Host. The
    // actual material captures are real; no afterimage/DLL/helper output is injected.
    internal static async Task Source(string bin,NativeId65FreshWizardResearchHostPlan host,Action<bool,string> check)
    {
        var document=host.SceneAncestor.AuthoringDocumentAncestor!;
        var a=NativeStaticTerrainTextureSource.Capture(bin,14,1);
        var b=NativeStaticTerrainTextureSource.Capture(bin,70,1);
        var before=host.FallbackAncestor.CopyUploadPayload();
        var beforeData=host.FallbackAncestor.CopyDataComponents();
        var beforeModels=host.FallbackAncestor.CopyModelPayload();
        var beforeScene=host.SceneAncestor.CopyScene();
        foreach(var g in Goldens)
        {
            var m0=g.A==14?a:b;var m1=g.B==14?a:b;
            var result=await NativeFreshWizardMaterialResource.CaptureAsync(bin,host,m0,m1);
            result.RequireAncestry(host,document,m0,m1);
            check(ReferenceEquals(result.HostAncestor,host)&&ReferenceEquals(result.DocumentAncestor,document)&&
                ReferenceEquals(result.FallbackAncestor,host.FallbackAncestor)&&
                ReferenceEquals(result.SceneAncestor,host.SceneAncestor)&&
                ReferenceEquals(result.CompilationAncestor,host.SceneAncestor.CompilationAncestor)&&
                ReferenceEquals(result.MaterialSources[0],m0)&&ReferenceEquals(result.MaterialSources[1],m1),
                "Retains actual Host/Fallback/Scene/Base/aggregate/ordered static-source identities.");
            check(result.UploadSha256==g.Upload&&Hash(result.CopyUploadPayload())==g.Upload&&
                result.DataSha256==g.Data&&Hash(result.CopyDataComponents())==g.Data,
                "Full mixed resource bytes match independent original-only expectations; a mismatch is retained, not retuned.");
            check(result.Allocation.AllocatedByteCount==g.Bytes&&result.Allocation.ContainedAliases.Count==g.Aliases,
                "Actual request bytes and contained aliases retain each material domain.");
            check(result.CopyUploadPayload().Length==0xE5800&&result.CopyDataComponents().Length==0xA6C&&
                result.CopyModelPayload().SequenceEqual(beforeModels)&&result.CopyScene().SequenceEqual(beforeScene),
                "Full upload/data grows metadata only; exact19000 models and3000 scene stay unchanged.");
            check(result.TransferSourceSha256==Transfer&&
                result.CopyTransferSourceImage().SequenceEqual(NativeId65FreshWizardUploadTransferLayout.Compile(before).CopyTransferSourceImage()),
                "Complete paired bank/residue and DMA-only tail retain928202 transfer, not merely the bank hash.");
            byte[] output=result.CopyUploadPayload(), owned=new byte[0x80000];
            foreach(var region in result.Allocation.Allocations)
            {
                var source=result.MaterialSources[region.MaterialId].CopySourceVram();
                for(int row=0;row<region.RowCount;row++)
                {
                    int at=region.TargetOffset+row*1024;
                    check(at>=0&&at+region.RowByteLength<=0x80000&&
                        (at+region.RowByteLength<=0x65800||at>=0x7F000)&&
                        owned.AsSpan(at,region.RowByteLength).ContainsAnyExcept((byte)0)==false,
                        "No floor row overlaps another row or any of the full65800:7F000 residue.");
                    check(output.AsSpan(at,region.RowByteLength).SequenceEqual(
                        source.AsSpan(region.SourceOffset+row*1024,region.RowByteLength)),
                        "Every complete output row retains its actual source bytes.");
                    owned.AsSpan(at,region.RowByteLength).Fill(1);
                }
            }
            bool complement=true;
            for(int i=0;i<before.Length;i++)
                if(i>=0x80000||owned[i]==0)complement &= before[i]==output[i];
            check(complement,"Complete original unallocated/protected/SPU complement: every byte compared.");
            var data=result.CopyDataComponents();
            bool tail=true;
            for(int i=8;i<beforeData.Length;i++)
                if(i is not (>=0x98 and <0x9C) and not (>=0xA8 and <0xAC))
                    tail &= beforeData[i]==data[i+0x170];
            check(tail,"Every old data-tail owner, including zero bytes, moves exactly170.");
            check(result.SourceCertified&&!result.Schema2MaterialBindingProven&&!result.BodyIssued&&
                !result.RuntimeVerified&&!result.PublicationApproved&&!result.HardwareTransfersProven,
                "Source component is not schema2 saved compilation, body, hardware or runtime/candidate approval.");
            foreach(int at in new[]{0,0x657FF,0x65800,0x7EFF0,0x7EFFF,0x7F000,0x80000,0xE57FF})
            {
                var changed=output.ToArray();changed[at]^=1;
                await Refuse<InvalidDataException>(()=> {result.RequireExactOutputs(changed,data,beforeModels,beforeScene);return Task.CompletedTask;},check);
            }
            foreach(int at in new[]{0,8,0x177,0x208,0x218,0x2C0,0x34C,0xA6B})
            {
                var changed=data.ToArray();changed[at]^=1;
                await Refuse<InvalidDataException>(()=> {result.RequireExactOutputs(output,changed,beforeModels,beforeScene);return Task.CompletedTask;},check);
            }
            foreach(int at in new[]{0,0x17800,0x17FEC,0x18AA8,0x18FFF})
            {
                var changed=beforeModels.ToArray();changed[at]^=1;
                await Refuse<InvalidDataException>(()=> {result.RequireExactOutputs(output,data,changed,beforeScene);return Task.CompletedTask;},check);
            }
            foreach(int at in new[]{0,0xD4,0xF88,0x2FE8,0x2FFF})
            {
                var changed=beforeScene.ToArray();changed[at]^=1;
                await Refuse<InvalidDataException>(()=> {result.RequireExactOutputs(output,data,beforeModels,changed);return Task.CompletedTask;},check);
            }
            await Refuse<InvalidDataException>(()=> {result.RequireExactOutputs(output[..^1],data,beforeModels,beforeScene);return Task.CompletedTask;},check);
            await Refuse<InvalidDataException>(()=> {result.RequireExactOutputs(output,data[..^1],beforeModels,beforeScene);return Task.CompletedTask;},check);
            await Refuse<InvalidDataException>(()=> {result.RequireExactOutputs(output,data,beforeModels[..^1],beforeScene);return Task.CompletedTask;},check);
            var returned=result.CopyUploadPayload();returned[0]^=1;
            check(Hash(result.CopyUploadPayload())==g.Upload,"Returned buffers cannot mutate the result.");
            var equalDoc=NativeFreshLevelDocumentIntent.Create(document.Base,document.Placements);
            await Refuse<InvalidDataException>(()=> {result.RequireAncestry(host,equalDoc,m0,m1);return Task.CompletedTask;},check);
            var schema2=document.WithFloorMaterials(NativeFreshFloorMaterialsIntent.Create(
                NativeFreshFloorMaterialIntent.RetailTerrain("spyro1-usa-retail-mode2-2352-v1",g.A,1),
                NativeFreshFloorMaterialIntent.RetailTerrain("spyro1-usa-retail-mode2-2352-v1",g.B,1)));
            await Refuse<InvalidDataException>(()=> {result.RequireAncestry(host,schema2,m0,m1);return Task.CompletedTask;},check);
            Console.WriteLine($"MIXED_RESOURCE_SOURCE case={g.Name} upload={result.UploadSha256} data={result.DataSha256} bodyIssued=false");
        }
        var againA=NativeStaticTerrainTextureSource.Capture(bin,14,1);
        var first=await NativeFreshWizardMaterialResource.CaptureAsync(bin,host,a,b);
        await Refuse<InvalidDataException>(()=> {first.RequireAncestry(host,document,againA,b);return Task.CompletedTask;},check);
        var again=await NativeFreshWizardMaterialResource.CaptureAsync(bin,host,againA,b);
        check(again.PlanSha256==first.PlanSha256&&!ReferenceEquals(again.MaterialSources[0],first.MaterialSources[0]),
            "Actual same-byte reissued source may construct its OWN component; cannot substitute into existing ancestry.");
        var reissuedHost=await NativeId65FreshWizardResearchHostPlan.CaptureAsync(bin,host.SceneAncestor,host.FallbackAncestor);
        await Refuse<InvalidDataException>(()=> {first.RequireAncestry(reissuedHost,document,a,b);return Task.CompletedTask;},check);
        var hostResult=await NativeFreshWizardMaterialResource.CaptureAsync(bin,reissuedHost,a,b);
        check(ReferenceEquals(hostResult.HostAncestor,reissuedHost)&&hostResult.PlanSha256==first.PlanSha256,
            "A genuinely reissued equal Host may issue its own component, never replace the retained Host reference.");
        var route=(NativeFreshWizardRouteIntent)document.Placements[0];
        var changedDocument=NativeFreshLevelDocumentIntent.Create(document.Base,
            [NativeFreshWizardRouteIntent.Create(route.EditorId,route.OriginX,route.OriginY+16,route.OriginZ,route.EndX,route.EndY)]);
        var changedScene=await NativeId65FreshWizardResearchScenePlan.CaptureAuthoredAsync(bin,changedDocument,
            host.SceneAncestor.CompilationAncestor,host.SceneAncestor.FamilyAncestor,host.SceneAncestor.ResourceAncestor);
        var changedHost=await NativeId65FreshWizardResearchHostPlan.CaptureAsync(bin,changedScene,host.FallbackAncestor);
        await Refuse<InvalidDataException>(()=>NativeFreshWizardMaterialResource.CaptureAsync("must-not-open",changedHost,a,b),check,
            "Only the existing exact separatedB Host/floor/route/music");
        var t0=NativeStaticTerrainTextureSource.Capture(bin,14,0);
        await Refuse<InvalidDataException>(()=>NativeFreshWizardMaterialResource.CaptureAsync("must-not-open",host,t0,b),check,
            "Only the three reviewed T1 ordered pairs");
        await Refuse<InvalidDataException>(()=>NativeFreshWizardMaterialResource.CaptureAsync("must-not-open",host,b,b),check,
            "Only the three reviewed T1 ordered pairs");
        await AcquisitionFailure(host,a,b,check);
        check(host.FallbackAncestor.CopyUploadPayload().SequenceEqual(before)&&
            host.FallbackAncestor.CopyDataComponents().SequenceEqual(beforeData)&&
            host.FallbackAncestor.CopyModelPayload().SequenceEqual(beforeModels)&&host.SceneAncestor.CopyScene().SequenceEqual(beforeScene),
            "All actual upstream source-owned objects remain immutable after successful and refused composition.");
    }

    private static async Task AcquisitionFailure(NativeId65FreshWizardResearchHostPlan host,
        NativeStaticTerrainTextureSource a,NativeStaticTerrainTextureSource b,Action<bool,string> check)
    {
        // Explicit source mode only: this is an actual two-byte test file, not
        // an original, output image or fake issued source. No broad cleanup.
        var directory=Directory.CreateTempSubdirectory("spyro-mixed-source-refusal-");
        string path=Path.Combine(directory.FullName,"tiny.bin");byte[] expected=[17,29];
        Console.WriteLine($"MIXED_SOURCE_REFUSAL_FIXTURE_RETAINED path={path}");
        // Retain this tiny fixture unconditionally, including unexpected owners.
        // No deletion, symlink traversal for cleanup, or directory cleanup.
        {
            using(var created=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None))created.Write(expected);
            try {await NativeFreshWizardMaterialResource.CaptureAsync(path,host,a,b);throw new Exception("Expected initial-source refusal.");}
            catch(IOException error) when(error.InnerException is AggregateException aggregate)
            {
                try
                {
                    var leaves=aggregate.InnerExceptions;
                    check(leaves.Count==3&&leaves.All(e=>e is InvalidDataException&&e.Message.StartsWith("Original snapshot length/initial position",StringComparison.Ordinal)),
                        "Actual initial, held-final and current-named-final shape failures remain ordered; both finals attempted after primary.");
                    check(!ReferenceEquals(leaves[0],leaves[1])&&!ReferenceEquals(leaves[1],leaves[2]),
                        "Three actual source checks fail independently before reading bad-length data; no synthetic repeated error is substituted.");
                }
                catch(Exception assertion)
                {throw new AggregateException("Mixed refusal assertion failed; retain the actual source error tree once.",assertion,error);}
            }
            using(var reopened=new FileStream(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None))
            {
                byte[] actual=new byte[2];reopened.ReadExactly(actual);
                check(actual.SequenceEqual(expected)&&reopened.ReadByte()==-1&&reopened.Length==2,
                    "Both acquired source owners really closed; tiny file remained exact and reached true EOF.");
            }
        }
    }

    private static async Task Refuse<T>(Func<Task> action,Action<bool,string> check,string? message=null) where T:Exception
    {
        try {await action();}
        catch(T error){check(message==null||error.Message.StartsWith(message,StringComparison.Ordinal),"Expected specific refusal: "+typeof(T).Name);return;}
        throw new InvalidDataException("Expected "+typeof(T).Name);
    }
}
