using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;

internal static class SameLayoutMaterialCases
{
    private const string InvalidPath="\0";
    private const string SceneHash="08718e7594702ab21e17f49c459b99cf4e35c8190258abbcaaf720106a880343";
    private const string HostHash="ad6d952cbf70ab8b1eab53f7637ffd0235271b7b20696956c9406f36f714fa7e";
    private const string UploadHash="a5867f5ecf74a2c54e56553b0eba6dea17c055f478ddb18ff3e2a7c83c3ec415";
    private const string TransferHash="92820254671062b6b20e2cfd2c243faac32ae6223e22d50d06221b4be8539883";

    internal static async Task Free(Action<bool,string> check)
    {
        var sceneMethod=typeof(NativeId65FreshWizardResearchScenePlan).GetMethod("CaptureSameLayoutMaterialAuthoredAsync",BindingFlags.Static|BindingFlags.NonPublic);
        var mixedMethod=typeof(NativeFreshWizardMaterialResource).GetMethod("CaptureSameLayoutMaterialAuthoredAsync",BindingFlags.Static|BindingFlags.NonPublic);
        check(sceneMethod is{IsAssembly:true}&&sceneMethod.ReturnType==typeof(Task<NativeId65FreshWizardResearchScenePlan>)&&
            sceneMethod.GetParameters().Select(p=>p.ParameterType).SequenceEqual(new[]{typeof(string),typeof(NativeFreshLevelDocumentIntent),
                typeof(NativeId65FreshDocumentCompilation),typeof(NativeWizardFamilyAssetPlan),typeof(NativeId65FreshWizardResearchResourcePlan),typeof(CancellationToken)})&&
            mixedMethod is{IsAssembly:true}&&mixedMethod.ReturnType==typeof(Task<NativeFreshWizardMaterialResource>)&&
            mixedMethod.GetParameters().Select(p=>p.ParameterType).SequenceEqual(new[]{typeof(string),typeof(NativeId65FreshWizardResearchHostPlan),
                typeof(NativeStaticTerrainTextureSource),typeof(NativeStaticTerrainTextureSource),typeof(CancellationToken)}),
            "Same-layout materials use two named internal source factories, not a public export policy or caller-byte issuer.");
        check((int)NativeFreshWizardSceneProducer.FixedA==0&&(int)NativeFreshWizardSceneProducer.Authored==1&&
            (int)NativeFreshWizardSceneProducer.MaterialAuthored==2,
            "All three original scene producer identities keep their serialized ordinal meaning.");
        await Refuse<OperationCanceledException>(()=>NativeId65FreshWizardResearchScenePlan.CaptureSameLayoutMaterialAuthoredAsync(
            InvalidPath,null!,null!,null!,null!,new(true)),check);
        await Refuse<ArgumentNullException>(()=>NativeId65FreshWizardResearchScenePlan.CaptureSameLayoutMaterialAuthoredAsync(
            InvalidPath,null!,null!,null!,null!),check);
        await Refuse<OperationCanceledException>(()=>NativeFreshWizardMaterialResource.CaptureSameLayoutMaterialAuthoredAsync(
            InvalidPath,null!,null!,null!,new(true)),check);
        await Refuse<ArgumentNullException>(()=>NativeFreshWizardMaterialResource.CaptureSameLayoutMaterialAuthoredAsync(
            InvalidPath,null!,null!,null!),check);
        Reject<ArgumentNullException>(()=>NativeFreshWizardAuthoringShape.RequireSameLayoutMaterialDocumentShape(null!),check);
        var wide=Document(3072);
        foreach(var pair in new[]{(14,70),(70,14),(14,14)})
        {
            var document=wide.WithFloorMaterials(Materials(pair.Item1,pair.Item2));
            check(NativeFreshWizardAuthoringShape.RequireSameLayoutMaterialDocumentShape(document)==new NativeWizardPlacementIntent(9984,9984,8704,9472,9984),
                "Neutral same-layout preflight retains each reviewed ordered T1 pair and actual wider-floor route without issuing source authority.");
            Reject<InvalidDataException>(()=>NativeFreshWizardAuthoringShape.RequireMaterialDocumentShape(document),check);
        }
        var authored=wide.WithContent(NativeId65FreshDocumentIntent.Create(8448,8448,8704,3072,3072,
            8960,8832,80,new(96,112,128),new(64,80,96),2),[NativeFreshWizardRouteIntent.Create(
                "22222222222222222222222222222222",10240,9984,8720,9728,9984)]);
        check(NativeFreshWizardAuthoringShape.RequireSameLayoutMaterialDocumentShape(authored)==new NativeWizardPlacementIntent(10240,9984,8720,9728,9984),
            "The new neutral shape retains authored spawn, colors, heading, track and route rather than substituting separatedB intent.");
        var badDocuments=new[]{NativeFreshLevelDocumentIntent.Create(wide.Base,wide.Placements),wide.WithPlacements([]),
            wide.WithPlacements([wide.Placements[0],NativeFreshWizardRouteIntent.Create("22222222222222222222222222222222",9984,9472,8704,9472,9472)]),
            wide.WithPlacements([NativeFreshWizardRouteIntent.Create("11111111111111111111111111111111",9984,9984,9217,9472,9984)]),
            wide.UpgradeForNativeMobys(),wide.WithFloorMaterials(Materials(70,70)),wide.WithFloorMaterials(Materials(14,70,0)),
            wide.WithFloorMaterials(NativeFreshFloorMaterialsIntent.Create(NativeFreshFloorMaterialIntent.BaseFloorColor(),
                NativeFreshFloorMaterialIntent.RetailTerrain(NativeFreshRetailTerrainMaterialIntent.SupportedSourceVariant,70,1)))};
        foreach(var invalid in badDocuments)
            Reject<InvalidDataException>(()=>NativeFreshWizardAuthoringShape.RequireSameLayoutMaterialDocumentShape(invalid),check);
        check(NativeFreshWizardAuthoringShape.RequireMaterialDocumentShape(Document(2048))==new NativeWizardPlacementIntent(9984,9984,8704,9472,9984),
            "The preserved separatedB shape still admits only its original exact recipe.");
    }

    internal static async Task Source(string path,Action<bool,string> check)
    {
        var wizard=await NativeWizardFamilyAssetPlan.CaptureAsync(path);
        var material0=NativeStaticTerrainTextureSource.Capture(path,14,1);
        var material1=NativeStaticTerrainTextureSource.Capture(path,70,1);
        var baseline=await Capture(Document(2048),false);
        var wide=await Capture(Document(3072),true);
        var document=wide.Mixed.DocumentAncestor;var compilation=wide.Scene.CompilationAncestor;
        var resource=wide.Scene.ResourceAncestor;var fallback=wide.Host.FallbackAncestor;
        byte[] canonical=NativeFreshLevelDocumentCodec.SerializeCanonical(document);
        wide.Scene.RequireAncestry(compilation,wizard,resource);wide.Host.RequireAncestry(wide.Scene,fallback);
        wide.Mixed.RequireAncestry(wide.Host,document,material0,material1);wide.Body.RequireMixedAncestry(wide.Mixed);
        check(ReferenceEquals(document.Base,compilation.Intent)&&ReferenceEquals(wide.Scene.AuthoringDocumentAncestor,document)&&
            ReferenceEquals(wide.Mixed.CompilationAncestor,compilation)&&ReferenceEquals(wide.Mixed.MaterialSources[0],material0)&&
            ReferenceEquals(wide.Mixed.MaterialSources[1],material1)&&ReferenceEquals(wide.Body.HostAncestor,wide.Host)&&
            ReferenceEquals(wide.Body.MixedResourceAncestor,wide.Mixed)&&wide.Scene.CopyMaterialCanonicalDocument().SequenceEqual(canonical)&&
            wide.Scene.MaterialCanonicalSha256==Hash(canonical)&&wide.Scene.CopyCanonicalDocument().SequenceEqual(compilation.CopyCanonicalDocument()),
            "Complete actual schema2 canonical document, compiled Base, Wizard/resource/Host and ordered captured materials survive into the Body.");
        check(wide.Scene.Producer!=baseline.Scene.Producer&&wide.Scene.BindingProfileId!=baseline.Scene.BindingProfileId&&
            wide.Scene.PlanSha256!=baseline.Scene.PlanSha256&&wide.Host.PlanSha256!=baseline.Host.PlanSha256&&
            wide.Mixed.PlanSha256!=baseline.Mixed.PlanSha256&&wide.Body.PlanSha256!=baseline.Body.PlanSha256,
            "A separate named material producer and actual wider intent cannot alias the original separatedB serialized identity.");
        check(Hash(baseline.Scene.CopyScene())==SceneHash&&Hash(wide.Scene.CopyScene())==SceneHash&&
            baseline.Host.OverlaySha256==HostHash&&wide.Host.OverlaySha256==HostHash&&
            wide.Host.CopyOverlay().SequenceEqual(baseline.Host.CopyOverlay())&&
            wide.Host.CopyInitializerPatch().SequenceEqual(baseline.Host.CopyInitializerPatch()),
            "Unchanged actual spawn/route yields the independently frozen complete scene and Host bytes despite wider authored geometry.");
        check(Hash(baseline.Mixed.CopyDataComponents())=="ddf2dcfa034ec160df4813e6584fb78af2ba713682631d72058a8477288f5e88"&&
            Hash(baseline.Body.CopyLevelData())=="a74ecbd7772a1a1880af78d15aa0137709f856ff943b4071a46a2f22d83e9aa2"&&
            Hash(baseline.Host.FallbackAncestor.CopyDataComponents())=="b650a22bd06fe8d983a3422321981682e45c35c477aed176a88331034492983b",
            "The actual old2048 baseline still matches its unchanged independent complete data and Body goldens.");
        byte[] expectedBase=PatchSurface(baseline.Scene.CompilationAncestor.FreshResourceAncestor.CopyDataComponents(),8);
        byte[] expectedResource=PatchSurface(baseline.Scene.ResourceAncestor.CopyDataComponents(),8);
        byte[] expectedFallback=PatchSurface(baseline.Host.FallbackAncestor.CopyDataComponents(),8);
        byte[] expectedMixed=PatchSurface(baseline.Mixed.CopyDataComponents(),0x178);
        check(expectedBase.Length==0x8C8&&expectedResource.Length==0x8FC&&expectedFallback.Length==0x8FC&&expectedMixed.Length==0xA6C&&
            compilation.FreshResourceAncestor.CopyDataComponents().SequenceEqual(expectedBase)&&resource.CopyDataComponents().SequenceEqual(expectedResource)&&
            fallback.CopyDataComponents().SequenceEqual(expectedFallback)&&wide.Mixed.CopyDataComponents().SequenceEqual(expectedMixed),
            "Twelve independent literal geometry-word patches predict every byte through8C8/8FC/A6C; lengths, metadata, collision-index and component complements stay intact.");
        byte[] expectedBody=baseline.Body.CopyLevelData();expectedMixed.CopyTo(expectedBody,0xE6000);
        byte[] actualBody=wide.Body.CopyLevelData();
        check(actualBody.SequenceEqual(expectedBody)&&wide.Body.LevelDataSha256==Hash(expectedBody)&&
            wide.Body.Descriptors.SequenceEqual(baseline.Body.Descriptors)&&wide.Body.CopyBufferAddress==0x8008BA38&&
            wide.Body.ModelRuntimeAddress==0x8008C4A4&&wide.Body.SceneRuntimeAddress==0x800A54A4&&wide.Body.KnownSceneEndAddress==0x800A84A4,
            "Independent child-relative complete Body expectation changes only actual geometry words, not header, copy endpoints, padding or other subfiles.");
        check(wide.Mixed.UploadSha256==UploadHash&&baseline.Mixed.UploadSha256==UploadHash&&
            wide.Mixed.CopyUploadPayload().SequenceEqual(baseline.Mixed.CopyUploadPayload())&&
            wide.Mixed.CopyModelPayload().SequenceEqual(baseline.Mixed.CopyModelPayload())&&
            wide.Mixed.CopyScene().SequenceEqual(baseline.Mixed.CopyScene())&&
            wide.Mixed.CopyTransferSourceImage().SequenceEqual(baseline.Mixed.CopyTransferSourceImage())&&wide.Mixed.TransferSourceSha256==TransferHash&&
            wide.Mixed.Allocation.Allocations.SequenceEqual(baseline.Mixed.Allocation.Allocations)&&
            wide.Mixed.Allocation.ContainedAliases.SequenceEqual(baseline.Mixed.Allocation.ContainedAliases),
            "All GPU allocations/aliases, complete audio/residue, models and scene remain identical to the actual old baseline.");
        NativeId65FreshWizardResearchExecutableSource.RequireWideFamilyMaterialBody(wide.Body);
        Reject<InvalidDataException>(()=>NativeId65FreshWizardResearchExecutableSource.RequireWideFamilyMaterialBody(baseline.Body),check);
        Reject<InvalidDataException>(()=>NativeId65FreshWizardResearchExecutableSource.RequireFamilyMaterialDocumentShape(document),check);
        check(wide.Mixed.SourceCertified&&wide.Mixed.Schema2MaterialBindingProven&&!wide.Mixed.BodyIssued&&!wide.Mixed.RuntimeVerified&&
            !wide.Mixed.PublicationApproved&&!wide.Mixed.HardwareTransfersProven&&!wide.Body.FinalExecutableIssued&&
            !wide.Body.RuntimeVerified&&!wide.Body.PublicationApproved&&!wide.Body.CompleteNativeDependencyClosureProven&&
            !wide.Body.PeakRuntimeMemoryProven&&!wide.Host.PublicationApproved&&!wide.Scene.PublicationApproved,
            "The separate source-owned component/Body arm does not widen runtime, final-executable, full-closure or editor-publication authority.");
        await Refuse<InvalidDataException>(()=>NativeId65FreshWizardResearchScenePlan.CaptureMaterialAuthoredAsync(
            InvalidPath,document,compilation,wizard,resource),check);
        await Refuse<InvalidDataException>(()=>NativeId65FreshWizardResearchScenePlan.CaptureAuthoredAsync(
            InvalidPath,document,compilation,wizard,resource),check);
        await Refuse<InvalidDataException>(()=>NativeFreshWizardMaterialResource.CaptureMaterialAuthoredAsync(InvalidPath,wide.Host,material0,material1),check);
        await Refuse<InvalidDataException>(()=>NativeFreshWizardMaterialResource.CaptureSameLayoutMaterialAuthoredAsync(InvalidPath,baseline.Host,material0,material1),check);
        await Refuse<InvalidDataException>(()=>NativeFreshWizardMaterialResource.CaptureAsync(InvalidPath,wide.Host,material0,material1),check);
        await Refuse<InvalidDataException>(()=>NativeId65FreshWizardResearchBodyPlan.CaptureAsync(InvalidPath,wide.Host),check);
        await Refuse<InvalidDataException>(()=>NativeId65FreshWizardResearchExecutablePlan.CaptureAsync(InvalidPath,wide.Body),check);
        await Refuse<OperationCanceledException>(()=>NativeId65FreshWizardResearchScenePlan.CaptureSameLayoutMaterialAuthoredAsync(
            InvalidPath,document,compilation,wizard,resource,new(true)),check);
        await Refuse<OperationCanceledException>(()=>NativeFreshWizardMaterialResource.CaptureSameLayoutMaterialAuthoredAsync(
            InvalidPath,wide.Host,material0,material1,new(true)),check);
        await Refuse<ArgumentNullException>(()=>NativeFreshWizardMaterialResource.CaptureSameLayoutMaterialAuthoredAsync(InvalidPath,wide.Host,null!,material1),check);
        await Refuse<ArgumentNullException>(()=>NativeFreshWizardMaterialResource.CaptureSameLayoutMaterialAuthoredAsync(InvalidPath,wide.Host,material0,null!),check);
        foreach(var pair in new[]{(material1,material0),(material0,material0)})
            await Refuse<InvalidDataException>(()=>NativeFreshWizardMaterialResource.CaptureSameLayoutMaterialAuthoredAsync(InvalidPath,wide.Host,pair.Item1,pair.Item2),check);
        var equalDocument=NativeFreshLevelDocumentCodec.Parse(canonical);
        Reject<InvalidDataException>(()=>wide.Mixed.RequireAncestry(wide.Host,equalDocument,material0,material1),check);
        await Refuse<InvalidDataException>(()=>NativeId65FreshWizardResearchScenePlan.CaptureSameLayoutMaterialAuthoredAsync(
            InvalidPath,equalDocument,compilation,wizard,resource),check);
        var equalResource=await NativeId65FreshWizardResearchResourcePlan.CaptureAuthoredAsync(path,compilation,wizard);
        check(equalResource.PlanSha256==resource.PlanSha256,"Foreign resource control retains equal complete identity, not a different recipe.");
        Reject<InvalidDataException>(()=>wide.Scene.RequireAncestry(compilation,wizard,equalResource),check);
        var equalHost=await NativeId65FreshWizardResearchHostPlan.CaptureAsync(path,wide.Scene,fallback);
        check(equalHost.PlanSha256==wide.Host.PlanSha256,"Foreign Host control retains equal bytes and exact nested ancestry.");
        Reject<InvalidDataException>(()=>wide.Mixed.RequireAncestry(equalHost,document,material0,material1),check);
        var equal0=NativeStaticTerrainTextureSource.Capture(path,14,1);var equal1=NativeStaticTerrainTextureSource.Capture(path,70,1);
        check(equal0.Handle==material0.Handle&&equal1.Handle==material1.Handle,"Foreign material controls are separately captured equal source handles.");
        Reject<InvalidDataException>(()=>wide.Mixed.RequireAncestry(wide.Host,document,equal0,material1),check);
        Reject<InvalidDataException>(()=>wide.Mixed.RequireAncestry(wide.Host,document,material0,equal1),check);
        var repeat=await NativeFreshWizardMaterialResource.CaptureSameLayoutMaterialAuthoredAsync(path,wide.Host,material0,material1);
        check(repeat.PlanSha256==wide.Mixed.PlanSha256&&repeat.CopyDataComponents().SequenceEqual(expectedMixed),
            "Repeated actual same-layout inputs are deterministic without treating equal issuance as the retained instance.");
        Reject<InvalidDataException>(()=>wide.Body.RequireMixedAncestry(repeat),check);
        byte[] returnedCanonical=wide.Scene.CopyMaterialCanonicalDocument();returnedCanonical[0]^=1;
        byte[] upload=wide.Mixed.CopyUploadPayload(),data=wide.Mixed.CopyDataComponents(),models=wide.Mixed.CopyModelPayload(),scene=wide.Mixed.CopyScene();
        wide.Mixed.RequireExactOutputs(upload,data,models,scene);
        foreach(int at in new[]{0,0x177,0x178+0x2C,0x178+0x114,0x2BB,0x2BC,data.Length-1})
        {byte[] changed=data.ToArray();changed[at]^=1;Reject<InvalidDataException>(()=>wide.Mixed.RequireExactOutputs(upload,changed,models,scene),check);}
        upload[0]^=1;data[0]^=1;models[0]^=1;scene[0]^=1;actualBody[0]^=1;
        check(wide.Mixed.CopyUploadPayload().SequenceEqual(baseline.Mixed.CopyUploadPayload())&&wide.Mixed.CopyDataComponents().SequenceEqual(expectedMixed)&&
            wide.Mixed.CopyModelPayload().SequenceEqual(baseline.Mixed.CopyModelPayload())&&wide.Mixed.CopyScene().SequenceEqual(baseline.Mixed.CopyScene())&&
            wide.Body.CopyLevelData().SequenceEqual(expectedBody)&&wide.Scene.CopyMaterialCanonicalDocument().SequenceEqual(canonical),
            "Complete source-owned canonical and payload snapshots remain detached after corruption and all pre-I/O refusals.");
        Console.WriteLine($"SAME_LAYOUT_MATERIAL_SOURCE_OK floor=3072 data={wide.Mixed.DataSha256} body={wide.Body.LevelDataSha256} childRelativeGeometry=true runtime=false publication=false");

        async Task<(NativeId65FreshWizardResearchScenePlan Scene,NativeId65FreshWizardResearchHostPlan Host,
            NativeFreshWizardMaterialResource Mixed,NativeId65FreshWizardResearchBodyPlan Body)> Capture(NativeFreshLevelDocumentIntent intent,bool sameLayout)
        {
            var compiled=await NativeId65FreshDocumentCompilation.CaptureAsync(path,intent.Base);
            var resources=await NativeId65FreshWizardResearchResourcePlan.CaptureAuthoredAsync(path,compiled,wizard);
            var scene=sameLayout?await NativeId65FreshWizardResearchScenePlan.CaptureSameLayoutMaterialAuthoredAsync(path,intent,compiled,wizard,resources):
                await NativeId65FreshWizardResearchScenePlan.CaptureMaterialAuthoredAsync(path,intent,compiled,wizard,resources);
            var retained=await NativeId65FreshWizardResearchFallbackResourcePlan.CaptureAsync(path,resources);
            var host=await NativeId65FreshWizardResearchHostPlan.CaptureAsync(path,scene,retained);
            var mixed=sameLayout?await NativeFreshWizardMaterialResource.CaptureSameLayoutMaterialAuthoredAsync(path,host,material0,material1):
                await NativeFreshWizardMaterialResource.CaptureMaterialAuthoredAsync(path,host,material0,material1);
            return(scene,host,mixed,await NativeId65FreshWizardResearchBodyPlan.CaptureMaterialAuthoredAsync(path,mixed));
        }
    }

    // Literal native packed-word preimages/afterimages, not the production
    // encoder or allocator. Both rectangles occupy the same closed4096 cell;
    // every byte outside these12 geometry words is retained verbatim.
    private static byte[] PatchSurface(byte[] before,int surface)
    {
        (int At,uint Before,uint After)[] fields=[
            (0x0C,0x02500250,0x02700270),(0x10,0x02200100,0x02200180),
            (0x2C,0x10000000,0x18000000),(0x30,0x00020000,0x00030000),(0x34,0x10020000,0x18030000),
            (0x5C,0x10000000,0x18000000),(0x60,0x00020000,0x00030000),(0x64,0x10020000,0x18030000),
            (0x114,0x40000210,0x60000210),(0x118,0x00200210,0x00300210),
            (0x120,0x00600290,0x005002D0),(0x124,0x40200210,0x60300210)];
        byte[] expected=before.ToArray();
        foreach(var field in fields)
        {
            int at=surface+field.At;
            if(BinaryPrimitives.ReadUInt32LittleEndian(before.AsSpan(at))!=field.Before)
                throw new InvalidDataException("Independent2048 surface preimage changed at "+field.At.ToString("X"));
            BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(at),field.After);
        }
        return expected;
    }
    private static NativeFreshLevelDocumentIntent Document(int size)=>NativeFreshLevelDocumentIntent.Create(
        NativeId65FreshDocumentIntent.Create(8448,8448,8704,size,size,8960,8960,64,new(80,128,112),new(128,96,64),19),
        [NativeFreshWizardRouteIntent.Create("11111111111111111111111111111111",9984,9984,8704,9472,9984)]).WithFloorMaterials(Materials(14,70));
    private static NativeFreshFloorMaterialsIntent Materials(int first,int second,int texture=1)=>NativeFreshFloorMaterialsIntent.Create(
        NativeFreshFloorMaterialIntent.RetailTerrain(NativeFreshRetailTerrainMaterialIntent.SupportedSourceVariant,first,texture),
        NativeFreshFloorMaterialIntent.RetailTerrain(NativeFreshRetailTerrainMaterialIntent.SupportedSourceVariant,second,texture));
    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Reject<T>(Action action,Action<bool,string> check)where T:Exception
    {try{action();}catch(T){check(true,"Same-layout material refused "+typeof(T).Name);return;}throw new InvalidDataException("Expected "+typeof(T).Name);}
    private static async Task Refuse<T>(Func<Task> action,Action<bool,string> check)where T:Exception
    {try{await action();}catch(T){check(true,"Same-layout material refused "+typeof(T).Name);return;}throw new InvalidDataException("Expected "+typeof(T).Name);}
}
