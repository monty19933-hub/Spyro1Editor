using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;

int checks=0;
void Check(bool ok,string reason){checks++;if(!ok)throw new Exception(reason);}
void Reject(Action action,string message)
{
    checks++;try{action();}catch(InvalidDataException e)when(e.Message.Contains(message,StringComparison.OrdinalIgnoreCase)){return;}
    throw new Exception("Expected refusal: "+message);
}
// Exact continuous center-separation, including a resident between safe endpoints.
Check(NativeWizardPlacementGeometry.IsStrictlySeparated(0,0,8192,0,4096,3073,3072),"above threshold");
Check(!NativeWizardPlacementGeometry.IsStrictlySeparated(0,0,8192,0,4096,3072,3072),"equal threshold must refuse");
Check(!NativeWizardPlacementGeometry.IsStrictlySeparated(0,0,8192,0,4096,0,3072),"middle collision missed by endpoints");
Check(NativeWizardPlacementGeometry.IsStrictlySeparated(0,0,8192,0,-3073,0,3072),"outside first endpoint");
Check(!NativeWizardPlacementGeometry.IsStrictlySeparated(0,0,8192,0,11264,0,3072),"equal end cap");
Check(NativeWizardPlacementGeometry.IsStrictlySeparated(int.MinValue,int.MinValue,int.MaxValue,int.MaxValue,int.MinValue,int.MaxValue,3072),"full integer domain must not overflow");
Check(!NativeWizardPlacementGeometry.IsStrictlySeparated(1,2,1,2,1,2,3072),"stationary center");
Reject(()=>NativeWizardPlacementGeometry.IsStrictlySeparated(0,0,1,1,4,4,-1),"distance");
var valid=new NativeWizardPlacementIntent(32768,32768,17152,34816,32768);
NativeWizardPlacementGeometry.Validate(valid);
Reject(()=>NativeWizardPlacementGeometry.Validate(valid with{OriginX=-16}),"coordinate");
Reject(()=>NativeWizardPlacementGeometry.Validate(valid with{OriginY=1}),"grid");
Reject(()=>NativeWizardPlacementGeometry.Validate(valid with{EndX=32768}),"distinct");
Reject(()=>NativeWizardPlacementGeometry.Validate(valid with{EndX=65536}),"length");
Reject(()=>NativeWizardPlacementGeometry.Validate(valid with{OriginZ=int.MaxValue}),"coordinate");
Console.WriteLine($"NATIVE_WIZARD_PLACEMENT_SMOKE_OK checks={checks}");
if(args.Length==0)return;
string image=Path.GetFullPath(args[0]);
if(args.Length==2&&args[1]=="--survey")
{
    // Read-only candidate search. Results are authored corridors, never body/runtime certificates.
    using FileStream input=new(image,FileMode.Open,FileAccess.Read,FileShare.Read);
    var layout=DiscImage.DetectLayout(image);
    byte[] data=DiscImage.ReadFileBytes(input,layout,37,0xF01000,0x28D800);
    Check(Convert.ToHexStringLower(SHA256.HashData(data))=="a937aecb3626ffd2311ba136684a34c9f36877054b52557b4524fcacee0e2d97","survey source data");
    int Read(int o)=>BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(o));
    int model=Read(8),length=Read(12);
    var snapshot=NativeRouteSurfaceSnapshot.Capture(data.AsSpan(model,length).ToArray(),0xF01000+model);
    var centers=Enumerable.Range(0,128).Select(i=>(X:Read(0x194000+0x8C38+i*0x58+12),Y:Read(0x194000+0x8C38+i*0x58+16))).ToArray();
    int found=0,attempts=0;
    foreach(var t in snapshot.Triangles.Where(t=>t.A.Z==t.B.Z&&t.A.Z==t.C.Z&&t.Descriptor==null&&t.ZFlags==0))
    {
        int x=((t.A.X+t.B.X+t.C.X)/3)&~15,y=((t.A.Y+t.B.Y+t.C.Y)/3)&~15;
        foreach(var end in new[]{(X:x+1024,Y:y),(X:x,Y:y+1024),(X:x-1024,Y:y),(X:x,Y:y-1024)})
        {
            if(!centers.All(c=>NativeWizardPlacementGeometry.IsStrictlySeparated(x,y,end.X,end.Y,c.X,c.Y,3072)))continue;
            attempts++;
            NativeRoutePoint[] route=[new(x,y,t.A.Z),new(end.X,end.Y,t.A.Z)];
            var result=NativeRouteSurfaceCertificate.Evaluate(snapshot,route,new(t.A.Z,512,1536));
            if(!result.Accepted)continue;
            Console.WriteLine($"CORRIDOR x={x} y={y} ground={t.A.Z} endX={end.X} endY={end.Y} triangle={t.Index} support={string.Join(',',result.SupportBindings.Select(b=>b.Triangle).Distinct())} certificate={result.CertificateSha256}");
            if(++found==12)break;
        }
        if(found==12)break;
    }
    Console.WriteLine($"SURVEY_DONE accepted={found} attempts={attempts}");return;
}
var scene=NativeDarkHollowWizardScenePlan.Capture(image);
var source=NativeDarkHollowWizardPlacementSource.Capture(image,scene);
Check(source.SourceImageSha256==scene.SourceImageSha256&&source.OriginalResidentCount==128,"source identity and original residents");
// Research origin uses the donor's observed205 above its nearest lower flat
// plane. This is an authored choice, not an issued initial-height/mover proof.
var intent=new NativeWizardPlacementIntent(92832,25248,10445,91808,25248);
var plan=source.Compile(intent);
byte[] before=scene.CopyScene(),after=plan.CopyScene();
plan.RequireOriginalAndFinal(before,after);
Check(plan.Mutations.Count==9&&plan.Mutations.Select(m=>m.SceneOffset).Distinct().Count()==9,"exact nine coordinate fields");
Check(plan.RevertToAppendedScene(after).SequenceEqual(before),"exact inverse to original appended-scene stage");
Check(plan.GroundZ==10240&&plan.AuthoredDryCorridorAccepted&&plan.OriginalCenterSeparationAccepted,"actual source corridor and all128centers");
Check(!plan.InitialHeightProven&&!plan.NativeBodyAndTrajectoryProven&&!plan.PlacementApproved&&!plan.RuntimeVerified&&!plan.PublicationApproved,"authored policy must not become native physics/publication");
Check(plan.PlanSha256==source.Compile(intent).PlanSha256,"repeat source issued intent");
Check(plan.BaseSceneSha256==scene.FinalSceneSha256&&plan.BaseGraphFingerprint==scene.GraphFingerprint,"exact base scene stage ownership");
Check(BinaryPrimitives.ReadInt32LittleEndian(after.AsSpan(0xEED4+0x38))==10732&&BinaryPrimitives.ReadInt32LittleEndian(after.AsSpan(0xEED4+0x48))==10732,"preserve source287height difference");
Check(after.AsSpan(0x8C38,128*0x58).SequenceEqual(before.AsSpan(0x8C38,128*0x58)),"all128originalMobyrows intact");
Check(Enumerable.Range(0,after.Length).Where(at=>!plan.Mutations.Any(m=>at>=m.SceneOffset&&at<m.SceneOffset+4)).All(at=>before[at]==after[at]),"complete unowned byte complement");
foreach(int at in plan.Mutations.Select(m=>m.SceneOffset).Concat(new[]{0,0xB838,0xB838+0x36,0xB838+0x43,0xB838+0x53,0xEED4,0xEED4+0x28,0xEED4+0x2C,0xEED4+0x3C,0xEED4+0x4C,0x10EF0,after.Length-1}))
{
    byte[] corrupt=after.ToArray();corrupt[at]^=1;
    Reject(()=>plan.RequireOriginalAndFinal(before,corrupt),"final scene");
    Reject(()=>plan.RevertToAppendedScene(corrupt),"final scene");
}
byte[] wrongBase=before.ToArray();wrongBase[^1]^=1;
Reject(()=>plan.RequireOriginalAndFinal(wrongBase,after),"preimage");
Reject(()=>source.Compile(intent with{OriginZ=10753}),"initial height");
Reject(()=>source.Compile(intent with{OriginZ=10239}),"initial height");
Reject(()=>source.Compile(intent with{OriginX=0,OriginY=0,EndX=1024,EndY=0}),"ground datum");
Reject(()=>source.Compile(intent with{OriginX=92831}),"grid");
Reject(()=>source.Compile(intent with{EndX=89008,EndY=28048}),"center separation");
Check(plan.FinalSceneSha256=="6b34477c459224cbbf4153336d9c4f7e2aada20d9085c975c1eca2ef74c0d16f","independent raw-source coordinate oracle golden");
byte[] detached=plan.CopyScene();detached[0]^=1;
Check(plan.CopyScene().SequenceEqual(after),"final output is detached");
Check(source.Compile(intent with{OriginX=91808,EndX=92832}).FinalSceneSha256!=plan.FinalSceneSha256,"reverse authored intent has independently bound bytes");
Console.WriteLine($"PLACEMENT_GOLDEN scene={plan.FinalSceneSha256} plan={plan.PlanSha256} surface={plan.SurfaceCertificateSha256} collision={plan.CollisionSha256}");
var inventory=NativeMobySourceInventory.Capture(image,LevelCatalog.Load(Environment.CurrentDirectory));
var playerSource=await NativePlayerAnimationSource.CaptureAsync(image);
var player=await playerSource.CompileMergeAsync(image,33,12,[8]);
var soundSource=await NativeGlobalSoundSource.CaptureAsync(image);
var sound=await soundSource.CompileModelSlotMergeAsync(image,inventory,33,12,0x11B,[0,1,2],
    [new(0x1F,NativeGlobalSoundMappingPolicy.ExplicitRebindUnresolvedConsumers),new(0x26)]);
var texture=NativeDarkHollowWizardTexturePlan.Capture(image);
var particle=await NativeDarkHollowWizardParticlePlan.CaptureAsync(image,texture);
var prior=await NativeDarkHollowWizardResourcePlan.CompileWithParticleAsync(image,inventory,player,sound,texture,particle,scene);
var placed=await NativeDarkHollowWizardResourcePlan.CompileWithPlacementAsync(image,inventory,player,sound,texture,particle,scene,plan);
byte[] priorData=prior.CopyFinalLevelData(),placedData=placed.CopyFinalLevelData();
prior.RequireFinal(priorData);placed.RequireFinal(placedData);
Check(prior.PlacementPlanSha256==null&&!prior.IncludesAuthoredPlacement,"existing resource path remains unplaced");
Check(placed.PlacementPlanSha256==plan.PlanSha256&&placed.IncludesAuthoredPlacement,"typed source placement participates in composition");
int sceneAt=BinaryPrimitives.ReadInt32LittleEndian(placedData.AsSpan(0x18));
Check(priorData.Length==placedData.Length&&BinaryPrimitives.ReadInt32LittleEndian(priorData.AsSpan(0x18))==sceneAt,"coordinate-only stage cannot resize/rebase owners");
Check(placedData.AsSpan(sceneAt,after.Length).SequenceEqual(after),"actual composed final scene bytes equal placement stage");
Check(priorData.AsSpan(0,sceneAt).SequenceEqual(placedData.AsSpan(0,sceneAt))&&priorData.AsSpan(sceneAt+after.Length).SequenceEqual(placedData.AsSpan(sceneAt+after.Length)),"every composed resource outside the scene is unchanged");
byte[] restoredData=placedData.ToArray();plan.CopyBaseScene().CopyTo(restoredData,sceneAt);
Check(restoredData.SequenceEqual(priorData),"removing coordinate stage exactly recovers prior full resource output");
Check(!placed.PlacementApproved&&!placed.PublicationApproved&&!placed.RuntimeFitProven,"composing accepted authored intent does not promote native gates");
var behavior=NativeDarkHollowWizardBehaviorSource.Capture(image);
var particleBehavior=NativeDarkHollowWizardParticleBehaviorSource.Capture(image);
var oldOverlay=NativeDarkHollowWizardOverlayPlan.Compile(behavior,prior,particleBehavior);
var placedOverlay=NativeDarkHollowWizardOverlayPlan.Compile(behavior,placed,particleBehavior);
Check(oldOverlay.OverlaySha256=="10f82ee1028a9ad4fa9045d948a88e76545847031b954ac08ad0e74e6e9c7083"&&oldOverlay.ExecutableSha256=="14aa937f198740373b37312eb3054cad46dd1ae05f1920c145d6bc1fde9a8635","existing independent overlay/EXE goldens preserved");
Check(placedOverlay.CopyOverlay().SequenceEqual(oldOverlay.CopyOverlay())&&placedOverlay.CopyExecutable().SequenceEqual(oldOverlay.CopyExecutable()),"coordinate intent must not change native code or copy-buffer instructions");
Check(placedOverlay.CopyFinalLevelData().SequenceEqual(placedData),"overlay composer retains exact authored scene");
Console.WriteLine($"PLACEMENT_COMPOSITION_GOLDEN old={prior.FinalDataSha256} placed={placed.FinalDataSha256} sceneAt={sceneAt:X}");
Console.WriteLine($"NATIVE_WIZARD_PLACEMENT_SOURCE_OK checks={checks}");
