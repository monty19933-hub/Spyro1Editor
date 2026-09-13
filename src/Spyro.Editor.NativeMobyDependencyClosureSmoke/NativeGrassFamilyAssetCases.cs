using System.Reflection;
using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Scene;

internal static class NativeGrassFamilyAssetCases
{
    internal static async Task Free(Action<bool,string> check)
    {
        var type=typeof(NativeMobyFamilyAssetPlan);
        check(type.IsSealed&&type.GetConstructors(BindingFlags.Public|BindingFlags.Instance).Length==0,
            "Native family asset has no public construction from bytes or claims.");
        var methods=type.GetMethods(BindingFlags.Public|BindingFlags.Static|BindingFlags.DeclaredOnly);
        check(methods.Length==1&&methods[0].Name=="CaptureArtisansGrassAsync"&&
            methods[0].GetParameters().Select(p=>p.ParameterType).SequenceEqual(new[]{typeof(string),typeof(CancellationToken)}),
            "Only the exact original-source Artisans T121 family issuer is public.");
        using var cancelled=new CancellationTokenSource();cancelled.Cancel();
        Task cancellation=NativeMobyFamilyAssetPlan.CaptureArtisansGrassAsync(null!,cancelled.Token);
        await Refuse<OperationCanceledException>(()=>cancellation,check);
        Task invalid=NativeMobyFamilyAssetPlan.CaptureArtisansGrassAsync("");
        await Refuse<ArgumentException>(()=>invalid,check);
        await Refuse<ArgumentNullException>(()=>{NativeGrassFamilySourceDecoder.DecodeDonor(null!);return Task.CompletedTask;},check);
        foreach(int length in new[]{0,0x383000-1,0x383000,0x383000+1})
            await Refuse<InvalidDataException>(()=>{NativeGrassFamilySourceDecoder.DecodeDonor(new byte[length]);return Task.CompletedTask;},check);
        foreach(int length in new[]{0,0xDFFF,0xE000,0xE001})
            await Refuse<InvalidDataException>(()=>{NativeGrassFamilySourceDecoder.DecodeDonorDispatch(new byte[length]);return Task.CompletedTask;},check);
        await TinySource(check);
    }

    internal static async Task Source(string image,Action<bool,string> check)
    {
        var asset=await NativeMobyFamilyAssetPlan.CaptureArtisansGrassAsync(image);
        check(asset.SourceCertified&&asset.DonorTrueIndex==121&&asset.NativeClass==0x01F5&&
            asset.CopyRow().Length==0x58&&asset.CopyProperties().Length==8&&asset.CopyPackage().Length==0x174,
            "Actual original-source asset contains the exact Grass donor row/properties/package.");
        check(asset.RowSha256=="de8450049c0bea92fba8fe4e7f9f9cb749a514d1318dbd83dc9b6b1b18a0b190"&&
            asset.PropertiesSha256=="9eeeff662fd5b77dbc35de8ed01e0d1fd149cee49126625b69f65553c4b7c20b"&&
            asset.PackageSha256=="90ca71a190c4567817d728753f25df667d56515e1721d24e19e6da9dc07edcc3"&&
            asset.DonorDispatchSha256=="ffdacdf97f86f1aed4505e0869ee2a9bfbdaad92025704fd33d1d4bfb9ba6d88"&&
            asset.DonorPropertiesSharingIndices.SequenceEqual(Enumerable.Range(118,16)),
            "Whole donor asset, shared-properties cluster and executed donor trace match pre-extraction literals.");
        check(!asset.DestinationBound&&!asset.NativeImportApproved&&!asset.RuntimeVerified&&!asset.PublicationApproved,
            "Source asset explicitly does not issue destination, behavior, registration, placement or export approval.");
        var graph=asset.AssetGraph;var template=asset.InstanceTemplate;var schema=template.Rows.Single();
        check(graph.Nodes.Count==2&&graph.Installs.Count==1&&graph.References.Count==0&&
            graph.CopyPayload("grass.package").SequenceEqual(asset.CopyPackage())&&
            graph.Nodes.All(n=>n.Sharing==NativeMobyDependencySharing.Immutable)&&
            graph.Nodes.All(n=>n.Resolution is NativeMobyDependencyResolution.Aggregate or NativeMobyDependencyResolution.ImportFromSource)&&
            ReferenceEquals(template.Shared,graph),
            "Existing dependency compiler materializes one detached shared package with donor evidence, not destination residency.");
        check(schema.SourceInstanceIndex==121&&schema.NativeClass==0x01F5&&schema.Row.Extent.Offset==0xCC44&&
            schema.Properties.Extent.Offset==0x11ACC&&schema.Properties.Extent.ByteLength==8&&
            schema.OwnerFields.Count==0&&!schema.RequiresBytePersistentOwner&&template.Globals.Count==0&&
            asset.ReadSource(schema.Row.Extent).SequenceEqual(asset.CopyRow())&&
            asset.ReadSource(schema.Properties.Extent).SequenceEqual(Convert.FromHexString("040000008A000000")),
            "Actual instance template retains its complete source row and pointer-free properties schema.");
        var modelSource=graph.Nodes.Single(n=>n.Id=="grass.package").Reads.Single().Extent.Space;
        byte[] completeData=asset.ReadSource(new(modelSource,0,modelSource.ByteLength));
        var decoded=NativeGrassFamilySourceDecoder.DecodeDonor(completeData);
        check(decoded.NormalFaces.RecordCount==9&&decoded.FarLodFaces.RecordCount==9&&
            decoded.NormalFaces.TexturedRecordCount==0&&decoded.FarLodFaces.TexturedRecordCount==0&&
            decoded.NormalFaces.Sha256=="f06f9c3ff1ba9bebb7379507144b595b1c326681c0713063892ae15e16162b3b"&&
            decoded.FarLodFaces.Sha256=="b30459d48479636f8f1c385e3a5e86df21a24637806c5e5699553ec789e75c94"&&
            decoded.PackageEntryRelativeReferenceCount==0&&decoded.InternalPropertiesFixups.Count==0&&decoded.PointerFieldOccurrenceCount==1,
            "Shared decoder retains both exact untextured face grammars, zero package rebase sites and one scene-pointer fixup.");
        foreach(int at in new[]{0,0x50+22*4,0x150+22*2,0x1C1520,0x1C1693,0x1C9800+0xCC44,0x1C9800+0x11ACC,0x1C9800+0x13ED8})
        {
            completeData[at]^=1;
            await Refuse<InvalidDataException>(()=>{NativeGrassFamilySourceDecoder.DecodeDonor(completeData);return Task.CompletedTask;},check);
            completeData[at]^=1;
        }
        byte[] row=asset.CopyRow(),properties=asset.CopyProperties(),package=asset.CopyPackage();
        row[0]^=1;properties[0]^=1;package[0]^=1;decoded.Row[0]^=1;decoded.Properties[0]^=1;decoded.Package[0]^=1;
        asset.RequireRetainedOwners();
        check(Hash(asset.CopyRow())==asset.RowSha256&&Hash(asset.CopyProperties())==asset.PropertiesSha256&&
            Hash(asset.CopyPackage())==asset.PackageSha256,"Caller/decoder copies cannot mutate captured asset, graph or template owners.");

        // Component allocation control only: these artificial arenas are not
        // a source-issued retail/fresh scene or destination behavior proof.
        var arena=new NativeAddressSpace(NativeAddressKind.Scene,"component-only-grass-allocation",NativeAddressRevision.Final,0x2000);
        var runtime=new NativeAddressSpace(NativeAddressKind.ExecutableFile,"component-only-runtime-domain",NativeAddressRevision.Original,0x66000);
        var allocation=NativeMobyDependencyInstantiationPlan.Create(new(arena,0xD4,0xB0),new(arena,0x1000,16),0xD4,0,
            [arena,asset.DetachedModelDomain],runtime);
        var allocated=allocation.Allocate([new("grass-b",template),new("grass-a",template)]);
        check(allocation.Rows.Count==0&&allocated.Rows.Select(r=>r.Index).SequenceEqual(new[]{0,1})&&
            allocated.PropertyBytesAllocated==16&&allocated.SharedNodeCount==2&&
            allocated.Rows[0].Properties.Offset==0x1000&&allocated.Rows[1].Properties.Offset==0x1008&&
            allocated.Rows.All(r=>r.RemapPrivateOwnerIndices(asset.CopyProperties()).SequenceEqual(asset.CopyProperties())),
            "Actual source template supports independent native instances sharing one detached package; component-only arena control.");
        check(allocated.Allocate([new("grass-a",template)]).Rows.Count==2,"Exact family request retry reuses its existing allocation.");
        await Refuse<InvalidDataException>(()=>{allocated.Allocate([new("grass-c",template)]);return Task.CompletedTask;},check);
        await Refuse<InvalidDataException>(()=>{allocation.Allocate([new("grass-a",template,NativeMobyDependencyActivation.SequentialUnproved)]);return Task.CompletedTask;},check);
        var equal=await NativeMobyFamilyAssetPlan.CaptureArtisansGrassAsync(image);
        check(equal.PlanSha256==asset.PlanSha256&&!template.SameBinding(equal.InstanceTemplate),
            "Equal source reissue has deterministic metadata but cannot replace actual graph/source address identities.");
        await Refuse<InvalidDataException>(()=>{allocated.Allocate([new("grass-a",equal.InstanceTemplate)]);return Task.CompletedTask;},check);
        await Refuse<InvalidDataException>(()=>{
            asset.ReadSource(new(new NativeAddressSpace(modelSource.Kind,modelSource.OwnerIdentity,modelSource.Revision,modelSource.ByteLength),0,4));
            return Task.CompletedTask;
        },check);
        Console.WriteLine($"NATIVE_GRASS_FAMILY_ASSET_SOURCE_OK plan={asset.PlanSha256} donor=artisans:T121:01F5 package={asset.PackageSha256} graph={graph.Fingerprint} destinationBound=false outputWritten=false");
    }

    private static async Task TinySource(Action<bool,string> check)
    {
        string path=Path.Combine(Directory.CreateTempSubdirectory("spyro-grass-family-refusal-").FullName,"tiny.bin");
        using(var file=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None))file.Write([17,29]);
        Console.WriteLine($"NATIVE_GRASS_FAMILY_REFUSAL_FIXTURE_RETAINED path={path}");
        Exception? actual=null;List<Exception> failures=[];
        try{await NativeMobyFamilyAssetPlan.CaptureArtisansGrassAsync(path);}catch(Exception error){actual=error;}
        Attempt(()=>{
            check(actual is IOException{InnerException:AggregateException},"Actual tiny-source primary and final errors remain aggregated.");
            var leaves=((AggregateException)actual!.InnerException!).InnerExceptions;
            check(leaves.Count==3&&leaves.All(e=>e is InvalidDataException&&e.Message=="Fresh-disc capture requires an exact-length seekable original source.")&&
                !ReferenceEquals(leaves[0],leaves[1])&&!ReferenceEquals(leaves[0],leaves[2])&&!ReferenceEquals(leaves[1],leaves[2]),
                "Three actual distinct initial/held-final/current-final failures retain their order and identity.");
        });
        FileStream? reopened=null;
        try
        {
            reopened=new FileStream(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None);
            Attempt(()=>check(reopened.Length==2,"Every failed-capture owner closed before exclusive reopen."));
            byte[] bytes=new byte[2];bool read=false;Attempt(()=>{reopened.ReadExactly(bytes);read=true;});
            if(read)Attempt(()=>check(bytes.SequenceEqual(new byte[]{17,29}),"Actual source bytes remain unchanged."));
            Attempt(()=>check(reopened.Position==2,"Actual reopened read consumed both bytes."));
            Attempt(()=>check(reopened.ReadByte()==-1,"Actual reopened source has true EOF."));
            Attempt(()=>check(reopened.Length==2&&reopened.Position==2,"Final source length/position remain unchanged."));
        }
        catch(Exception error){failures.Add(error);}
        finally{if(reopened is not null)Attempt(reopened.Dispose);}
        if(failures.Count!=0)throw new AggregateException("Grass source controls failed; actual capture error retained once.",
            failures.Concat(actual is null?Array.Empty<Exception>():new[]{actual}));
        void Attempt(Action action){try{action();}catch(Exception error){failures.Add(error);}}
    }

    private static async Task Refuse<T>(Func<Task> action,Action<bool,string> check)where T:Exception
    {try{await action();}catch(T){check(true,"Expected family refusal "+typeof(T).Name);return;}throw new Exception("Expected "+typeof(T).Name);}
    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
}
