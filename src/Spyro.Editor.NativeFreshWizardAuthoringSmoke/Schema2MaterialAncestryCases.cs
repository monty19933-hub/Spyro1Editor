using System.Reflection;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;

// PRIVATE tests first; no compiled/run result claimed.
internal static class Schema2MaterialAncestryCases
{
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static NativeFreshFloorMaterialsIntent Materials(int a,int b,int texture=1) =>
        NativeFreshFloorMaterialsIntent.Create(
            NativeFreshFloorMaterialIntent.RetailTerrain("spyro1-usa-retail-mode2-2352-v1",a,texture),
            NativeFreshFloorMaterialIntent.RetailTerrain("spyro1-usa-retail-mode2-2352-v1",b,texture));

    internal static async Task Free(Action<bool,string> check)
    {
        check(Enum.GetValues<NativeFreshWizardSceneProducer>().Length==3,
            "Three closed scene producer arms, not an export policy or new document class.");
        var sceneFactory=typeof(NativeId65FreshWizardResearchScenePlan).GetMethod("CaptureMaterialAuthoredAsync",
            BindingFlags.Static|BindingFlags.NonPublic);
        var resourceFactory=typeof(NativeFreshWizardMaterialResource).GetMethod("CaptureMaterialAuthoredAsync",
            BindingFlags.Static|BindingFlags.NonPublic);
        check(sceneFactory is {IsAssembly:true}&&resourceFactory is {IsAssembly:true},
            "Both material-authored factories are internal; no new public API.");
        using var cancelled=new CancellationTokenSource();cancelled.Cancel();
        await Refuse<OperationCanceledException>(()=>NativeId65FreshWizardResearchScenePlan.CaptureMaterialAuthoredAsync(
            "must-not-open",null!,null!,null!,null!,cancelled.Token),check);
        await Refuse<ArgumentNullException>(()=>NativeId65FreshWizardResearchScenePlan.CaptureMaterialAuthoredAsync(
            "must-not-open",null!,null!,null!,null!),check);
        await Refuse<OperationCanceledException>(()=>NativeFreshWizardMaterialResource.CaptureMaterialAuthoredAsync(
            "must-not-open",null!,null!,null!,cancelled.Token),check);
        await Refuse<ArgumentNullException>(()=>NativeFreshWizardMaterialResource.CaptureMaterialAuthoredAsync(
            "must-not-open",null!,null!,null!),check);
        await Refuse<InvalidDataException>(()=>{
            _=NativeFreshFloorMaterialIntent.RetailTerrain("unknown-variant",14,1);return Task.CompletedTask;
        },check); // A real neutral constructor refusal, not a fabricated invalid issuer.
    }

    internal static async Task Source(string bin,NativeId65FreshWizardResearchHostPlan oldHost,Action<bool,string> check)
    {
        var oldScene=oldHost.SceneAncestor;var compilation=oldScene.CompilationAncestor;
        var oldDocument=oldScene.AuthoringDocumentAncestor!;var family=oldScene.FamilyAncestor;var resource=oldScene.ResourceAncestor;
        string oldScenePlan=oldScene.PlanSha256,oldHostPlan=oldHost.PlanSha256;
        var a=NativeStaticTerrainTextureSource.Capture(bin,14,1);
        var b=NativeStaticTerrainTextureSource.Capture(bin,70,1);
        var expected=new[]{
            (A:14,B:70,U:"a5867f5ecf74a2c54e56553b0eba6dea17c055f478ddb18ff3e2a7c83c3ec415",D:"ddf2dcfa034ec160df4813e6584fb78af2ba713682631d72058a8477288f5e88"),
            (A:70,B:14,U:"6fa0965dae5ecedacdfaae4dcb3d3bd89ee8e197bf0b66a05a7def1db8792220",D:"b7559eed9825c2f70b6d21e24b44185e9d504e8a4fabbf2c24080ac86ed7bacd"),
            (A:14,B:14,U:"ff5e14fb2356f9dd633994505bf73bb79735bad84a9deae653ff5f53d6368040",D:"a63ad93de2b4a8a59965b8c2a5e9c016f6c089e5ee4c98df5a065a7ceae92acd")};
        var scenePlans=new List<string>();
        var issued=new List<(NativeId65FreshWizardResearchHostPlan Host,NativeFreshWizardMaterialResource Resource)>();
        foreach(var g in expected)
        {
            var document=oldDocument.WithFloorMaterials(Materials(g.A,g.B));
            byte[] canonical=NativeFreshLevelDocumentCodec.SerializeCanonical(document);
            var m0=g.A==14?a:b;var m1=g.B==14?a:b;
            await Refuse<InvalidDataException>(()=>NativeId65FreshWizardResearchScenePlan.CaptureAuthoredAsync(
                "must-not-open",document,compilation,family,resource),check,"This research scene currently requires schema 1");
            var scene=await NativeId65FreshWizardResearchScenePlan.CaptureMaterialAuthoredAsync(bin,document,compilation,family,resource);
            scene.RequireAncestry(compilation,family,resource);
            check(scene.Producer==NativeFreshWizardSceneProducer.MaterialAuthored&&
                ReferenceEquals(scene.AuthoringDocumentAncestor,document)&&ReferenceEquals(document.Base,compilation.Intent)&&
                ReferenceEquals(scene.ResourceAncestor,resource)&&ReferenceEquals(scene.FamilyAncestor,family)&&
                scene.CopyMaterialCanonicalDocument().SequenceEqual(canonical)&&scene.MaterialCanonicalSha256==Hash(canonical),
                "Actual full schema2 aggregate, route/editor ID, Base and complete canonical bytes retained.");
            check(scene.CopyScene().SequenceEqual(oldScene.CopyScene())&&scene.CopyCanonicalDocument().SequenceEqual(compilation.CopyCanonicalDocument())&&
                scene.CanonicalDocumentSha256==compilation.CanonicalSha256&&scene.PlanSha256!=oldScenePlan&&
                scene.BindingProfileId!=oldScene.BindingProfileId,"Equal native scene bytes do not erase material intent or reinterpret BASE canonical fields.");
            scenePlans.Add(scene.PlanSha256);
            var host=await NativeId65FreshWizardResearchHostPlan.CaptureAsync(bin,scene,oldHost.FallbackAncestor);
            check(ReferenceEquals(host.SceneAncestor,scene)&&host.CopyOverlay().SequenceEqual(oldHost.CopyOverlay())&&
                host.CopyInitializerPatch().SequenceEqual(oldHost.CopyInitializerPatch()),
                "Actual new Scene reaches unchanged detached Host bytes, not a completed mixed body.");
            await Refuse<InvalidDataException>(()=>NativeId65FreshWizardResearchBodyPlan.CaptureAsync("must-not-open",host),check,
                "Material-authored Scene requires its mixed body");
            await Refuse<InvalidDataException>(()=>{NativeId65FreshWizardResearchBodySource.RequireHost(host);return Task.CompletedTask;},check,
                "Material-authored Scene requires its mixed body");
            await Refuse<InvalidDataException>(()=>{_=NativeId65FreshWizardBodyLayout.CompileAuthored(host,[]);return Task.CompletedTask;},check,
                "Material-authored Scene requires its mixed body");
            await Refuse<InvalidDataException>(()=>NativeFreshWizardMaterialResource.CaptureAsync("must-not-open",host,m0,m1),check,
                "This research scene currently requires schema 1");
            var mixed=await NativeFreshWizardMaterialResource.CaptureMaterialAuthoredAsync(bin,host,m0,m1);
            mixed.RequireAncestry(host,document,m0,m1);
            check(mixed.Schema2MaterialBindingProven&&ReferenceEquals(mixed.DocumentAncestor,document)&&
                ReferenceEquals(mixed.MaterialSources[0],m0)&&ReferenceEquals(mixed.MaterialSources[1],m1)&&
                !mixed.BodyIssued&&!mixed.RuntimeVerified&&!mixed.PublicationApproved&&!mixed.HardwareTransfersProven,
                "Only the descriptor-bound arm carries actual ordered source instances and schema2 binding; downstream flags stay false.");
            check(mixed.UploadSha256==g.U&&mixed.DataSha256==g.D&&Hash(mixed.CopyUploadPayload())==g.U&&Hash(mixed.CopyDataComponents())==g.D&&
                mixed.CopyModelPayload().SequenceEqual(oldHost.FallbackAncestor.CopyModelPayload())&&mixed.CopyScene().SequenceEqual(oldScene.CopyScene())&&
                mixed.TransferSourceSha256=="92820254671062b6b20e2cfd2c243faac32ae6223e22d50d06221b4be8539883",
                "Same three independent original-only resource expectations, full bank/residue and model/scene bytes; no retuned oracle.");
            var equalDocument=NativeFreshLevelDocumentCodec.Parse(canonical);
            await Refuse<InvalidDataException>(()=>{mixed.RequireAncestry(host,equalDocument,m0,m1);return Task.CompletedTask;},check);
            await Refuse<InvalidDataException>(()=>NativeId65FreshWizardResearchScenePlan.CaptureMaterialAuthoredAsync(
                "must-not-open",equalDocument,compilation,family,resource),check,"Material-authored Scene requires the actual compiled Base");
            var changedReturn=scene.CopyMaterialCanonicalDocument();changedReturn[0]^=1;
            check(scene.CopyMaterialCanonicalDocument().SequenceEqual(canonical),"Returned aggregate bytes are detached.");
            issued.Add((host,mixed));
            Console.WriteLine($"SCHEMA2_MIXED_ANCESTRY_SOURCE pair={g.A}/{g.B} scene={scene.PlanSha256} upload={mixed.UploadSha256} bodyIssued=false");
        }
        check(scenePlans.Distinct().Count()==3,"Changing only material intent changes all three new Scene identities.");
        var primaryHost=issued[0].Host;var primary=issued[0].Resource;
        var reversedHost=issued[1].Host;var sameSource=issued[2];
        var actualDocument=primary.DocumentAncestor;var actualScene=primaryHost.SceneAncestor;
        var resourceAgain=await NativeId65FreshWizardResearchResourcePlan.CaptureAuthoredAsync(bin,compilation,family);
        await Refuse<InvalidDataException>(()=>{actualScene.RequireAncestry(compilation,family,resourceAgain);return Task.CompletedTask;},check);
        var sourceAgain=NativeStaticTerrainTextureSource.Capture(bin,14,1);
        await Refuse<InvalidDataException>(()=>{primary.RequireAncestry(primaryHost,actualDocument,sourceAgain,b);return Task.CompletedTask;},check);
        await Refuse<InvalidDataException>(()=>{sameSource.Resource.RequireAncestry(sameSource.Host,
            sameSource.Resource.DocumentAncestor,a,sourceAgain);return Task.CompletedTask;},check);
        // Both a,a inputs form an otherwise supported14/14 native pair. Each
        // descriptor case therefore isolates exactly one authored slot mismatch.
        await Refuse<InvalidDataException>(()=>NativeFreshWizardMaterialResource.CaptureMaterialAuthoredAsync("must-not-open",primaryHost,a,a),check,
            "Schema2 material descriptors do not match");
        await Refuse<InvalidDataException>(()=>NativeFreshWizardMaterialResource.CaptureMaterialAuthoredAsync("must-not-open",reversedHost,a,a),check,
            "Schema2 material descriptors do not match");
        await Refuse<InvalidDataException>(()=>NativeFreshWizardMaterialResource.CaptureMaterialAuthoredAsync("must-not-open",primaryHost,b,a),check,
            "Schema2 material descriptors do not match");
        await Refuse<InvalidDataException>(()=>NativeFreshWizardMaterialResource.CaptureMaterialAuthoredAsync("must-not-open",oldHost,a,b),check,
            "Schema2 mixed resource requires its material-authored Scene");
        var own=await NativeFreshWizardMaterialResource.CaptureMaterialAuthoredAsync(bin,primaryHost,sourceAgain,b);
        check(own.PlanSha256==primary.PlanSha256&&ReferenceEquals(own.MaterialSources[0],sourceAgain),
            "Equal actual source may issue its OWN descriptor-bound component, not replace a retained instance.");
        var equalActualDocument=NativeFreshLevelDocumentIntent.Create(actualDocument.Base,actualDocument.Placements)
            .WithFloorMaterials(actualDocument.FloorMaterials!);
        var equalScene=await NativeId65FreshWizardResearchScenePlan.CaptureMaterialAuthoredAsync(bin,equalActualDocument,compilation,family,resource);
        check(equalScene.PlanSha256==actualScene.PlanSha256&&!ReferenceEquals(equalScene.AuthoringDocumentAncestor,actualDocument),
            "Equal actual document can issue its own scene chain; the first scene is not rebound.");
        var ownHost=await NativeId65FreshWizardResearchHostPlan.CaptureAsync(bin,equalScene,oldHost.FallbackAncestor);
        await Refuse<InvalidDataException>(()=>{primary.RequireAncestry(ownHost,actualDocument,a,b);return Task.CompletedTask;},check);
        await Refuse<InvalidDataException>(()=>{primary.RequireAncestry(ownHost,equalActualDocument,a,b);return Task.CompletedTask;},check);
        var ownChain=await NativeFreshWizardMaterialResource.CaptureMaterialAuthoredAsync(bin,ownHost,a,b);
        check(ownChain.PlanSha256==primary.PlanSha256&&ReferenceEquals(ownChain.DocumentAncestor,equalActualDocument),
            "A separately issued equal chain owns itself; no fake Host/Scene authority.");
        var clearance=await NativeFreshInitialSpawnClearancePlan.CaptureWizardAsync(bin,actualDocument,actualScene);
        _=clearance.RequireInitialFloorFootprint(actualDocument,actualScene);
        await Refuse<InvalidDataException>(()=>{_=clearance.RequireInitialFloorFootprint(equalActualDocument,actualScene);return Task.CompletedTask;},check);
        await Refuse<InvalidDataException>(()=>{_=oldScene.CopyMaterialCanonicalDocument();return Task.CompletedTask;},check);
        var route=(NativeFreshWizardRouteIntent)oldDocument.Placements[0];
        var moved=oldDocument.WithContent(oldDocument.Base,[NativeFreshWizardRouteIntent.Create(route.EditorId,
            route.OriginX,route.OriginY+16,route.OriginZ,route.EndX,route.EndY)]).WithFloorMaterials(Materials(14,70));
        await Refuse<InvalidDataException>(()=>NativeId65FreshWizardResearchScenePlan.CaptureMaterialAuthoredAsync(
            "must-not-open",moved,compilation,family,resource),check,"Material-authored Scene currently supports only the exact separatedB");
        foreach(var invalid in new[]{oldDocument,
            oldDocument.WithFloorMaterials(Materials(70,70)),oldDocument.WithFloorMaterials(Materials(14,70,0)),
            oldDocument.WithFloorMaterials(NativeFreshFloorMaterialsIntent.Create(NativeFreshFloorMaterialIntent.BaseFloorColor(),
                NativeFreshFloorMaterialIntent.BaseFloorColor())),
            NativeFreshLevelDocumentIntent.Create(oldDocument.Base,[]).WithFloorMaterials(Materials(14,70)),
            NativeFreshLevelDocumentIntent.Create(oldDocument.Base,[oldDocument.Placements[0],
                NativeFreshWizardRouteIntent.Create("22222222222222222222222222222222",9984,9472,8704,9472,9472)]).WithFloorMaterials(Materials(14,70))})
            await Refuse<InvalidDataException>(()=>NativeId65FreshWizardResearchScenePlan.CaptureMaterialAuthoredAsync(
                "must-not-open",invalid,compilation,family,resource),check,"Material-authored Scene");
        await TinyFailure(path=>NativeId65FreshWizardResearchScenePlan.CaptureMaterialAuthoredAsync(
            path,actualDocument,compilation,family,resource),check);
        await TinyFailure(path=>NativeFreshWizardMaterialResource.CaptureMaterialAuthoredAsync(path,primaryHost,a,b),check);
        check(oldScene.PlanSha256==oldScenePlan&&oldHost.PlanSha256==oldHostPlan,
            "Existing schema1 objects and JSON identities remain unchanged; outer source suite still exercises old full goldens.");
    }

    private static async Task TinyFailure(Func<string,Task> capture,Action<bool,string> check)
    {
        var directory=Directory.CreateTempSubdirectory("spyro-schema2-mixed-refusal-");
        string path=Path.Combine(directory.FullName,"tiny.bin");
        Console.WriteLine($"SCHEMA2_MIXED_REFUSAL_FIXTURE_RETAINED path={path}");
        using(var created=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None))created.Write([17,29]);
        // Always retain the actual two-byte fixture/any unexpected entry. No cleanup deletion.
        IOException? sourceFailure=null;
        try{await capture(path);throw new InvalidDataException("Expected actual initial source shape refusal.");}
        catch(IOException error) when(error.InnerException is AggregateException aggregate)
        {
            sourceFailure=error;
            try
            {
                var leaves=aggregate.InnerExceptions;
                check(leaves.Count==3&&leaves.All(e=>e is InvalidDataException&&
                    e.Message.StartsWith("Original snapshot length/initial position",StringComparison.Ordinal))&&
                    !ReferenceEquals(leaves[0],leaves[1])&&!ReferenceEquals(leaves[1],leaves[2])&&!ReferenceEquals(leaves[0],leaves[2]),
                    "Actual initial, held final and named final failures remain independent and ordered.");
            }
            catch(Exception assertion){throw new AggregateException("Source refusal assertion failed; retain actual failure once.",assertion,error);}
        }
        FileStream? reopened=null;List<Exception> controlFailures=[];
        try
        {
            reopened=new FileStream(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None);
            byte[] bytes=new byte[2];reopened.ReadExactly(bytes);
            check(bytes.SequenceEqual(new byte[]{17,29})&&reopened.Length==2&&reopened.ReadByte()==-1,
                "Both actual source owners closed; tiny bytes and true EOF retained.");
        }
        catch(Exception error){controlFailures.Add(error);}
        finally
        {
            if(reopened is not null)
                try{reopened.Dispose();}catch(Exception error){controlFailures.Add(error);}
        }
        if(controlFailures.Count!=0)
            throw new AggregateException("Source refusal follow-up control failed; controls first and actual source failure retained once.",
                controlFailures.Concat(new[]{sourceFailure!}));
    }

    private static async Task Refuse<T>(Func<Task> action,Action<bool,string> check,string? prefix=null) where T:Exception
    {
        try{await action();}
        catch(T error)
        {
            try{check(prefix==null||error.Message.StartsWith(prefix,StringComparison.Ordinal),"Expected exact refusal "+typeof(T).Name);}
            catch(Exception assertion){throw new AggregateException("Refusal assertion failed; actual failure retained once.",assertion,error);}
            return;
        }
        throw new InvalidDataException("Expected "+typeof(T).Name);
    }
}
