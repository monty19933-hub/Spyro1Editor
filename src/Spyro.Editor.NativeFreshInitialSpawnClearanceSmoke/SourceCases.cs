using System.Security.Cryptography;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using static ClearanceCases;

internal static class SourceCases
{
    internal static async Task Run(string original)
    {
        RequireOriginal(original);
        try
        {
            var floor=A();var c=await NativeId65FreshDocumentCompilation.CaptureAsync(original,floor);
            var family=await NativeWizardFamilyAssetPlan.CaptureAsync(original);
            var resources=await NativeId65FreshWizardResearchResourcePlan.CaptureAsync(original,c,family);
            var scene=await NativeId65FreshWizardResearchScenePlan.CaptureAsync(original,c,family,resources,Placement);
            var doc=Document(floor,Placement);byte[] before=scene.CopyScene(),models=resources.CopyModelPayload();
            var result=await NativeFreshInitialSpawnClearancePlan.CaptureWizardAsync(original,doc,scene);
            Check(result.Finding.Status==NativeInitialSpawnClearanceStatus.Overlap&&result.Finding.DistanceSquared==243828&&result.Finding.CombinedRadiusSquared==571536,"Actual issued source produces original negative golden, not synthetic bytes.");
            Reject<InvalidDataException>(()=>result.RequireInitialSeparation(doc,scene));
            result.RequireAncestry(doc,scene,c,family,resources);
            Check(result.SourceCertified&&!result.RuntimeVerified&&!result.FirstQueryProven&&!result.TrajectoryProven&&!result.PublicationApproved,"Exact source facts, no lifecycle/runtime/export grant.");
            Check(result.Finding.IncomingDamageFlags==0&&result.Finding.ResponsePermitted==true,"Actual zero damage cannot hide overlap.");
            Check(result.Evidence.Count>=7&&result.Finding.EditorId==Id,"Concrete source/model/scene consumers and stable document identity.");
            var package=family.CopyOwner("wizard.model");
            NativeFreshInitialSpawnClearanceSource.RequireInstalledOwners(package,models,resources.ModelRoots,resources.Owners);
            foreach(string mode in new[]{"provenance","source-owner","source-offset","length","omit"})
            {
                var owners=resources.Owners.Select(o=>o.Domain==NativeFreshWizardResourceDomain.Models&&o.Offset==0x17134?mode switch{
                    "provenance"=>o with{Provenance="retained-source"},"source-owner"=>o with{SourceOwnerId="zero-but-unowned"},
                    "source-offset"=>o with{SourceOffset=0},"length"=>o with{ByteLength=o.ByteLength-4},_=>o}:o)
                    .Where(o=>mode!="omit"||o.Domain!=NativeFreshWizardResourceDomain.Models||o.Offset!=0x17134).ToArray();
                Reject<InvalidDataException>(()=>NativeFreshInitialSpawnClearanceSource.RequireInstalledOwners(package,models,resources.ModelRoots,owners));
                Check(models.AsSpan(0x17134,4).SequenceEqual(new byte[4]),"Identical lookahead zero bytes do not repair wrong owner classification.");
            }
            foreach(int at in new[]{0x13B94,0x13BB8,0x14B30,0x17124,0x17134})
            {byte[] bad=models.ToArray();bad[at]^=1;Reject<InvalidDataException>(()=>NativeFreshInitialSpawnClearanceSource.RequireInstalledOwners(package,bad,resources.ModelRoots,resources.Owners));}
            Reject<InvalidDataException>(()=>NativeFreshInitialSpawnClearanceSource.RequireInstalledOwners(package,models[..^1],resources.ModelRoots,resources.Owners));
            var changedRoots=resources.ModelRoots.Select(r=>r.ClassId==0x11B?r with{Offset=r.Offset+4}:r).ToArray();
            Reject<InvalidDataException>(()=>NativeFreshInitialSpawnClearanceSource.RequireInstalledOwners(package,models,changedRoots,resources.Owners));
            byte[] copied=result.CopyCanonicalDocument();copied[0]^=1;
            Check(!copied.SequenceEqual(result.CopyCanonicalDocument()),"Returned canonical copy cannot mutate assessment.");
            var repeat=await NativeFreshInitialSpawnClearancePlan.CaptureWizardAsync(original,doc,scene);
            Check(repeat.PlanSha256==result.PlanSha256,"Same immutable actual pair deterministic.");
            var scene2=await NativeId65FreshWizardResearchScenePlan.CaptureAsync(original,c,family,resources,Placement);
            Check(scene2.SceneSha256==scene.SceneSha256,"Wrong scene fixture genuinely equal bytes.");
            Reject<InvalidDataException>(()=>result.RequireAncestry(doc,scene2,c,family,resources));
            Reject<InvalidDataException>(()=>result.RequireInitialSeparation(doc,scene2));
            Reject<InvalidDataException>(()=>result.RequireAncestry(Document(floor,Placement),scene,c,family,resources));
            await Reject<InvalidDataException>(()=>NativeFreshInitialSpawnClearancePlan.CaptureWizardAsync("not-opened",Document(A(),Placement),scene));
            await Reject<InvalidDataException>(()=>NativeFreshInitialSpawnClearancePlan.CaptureWizardAsync("not-opened",Document(floor,Placement with{EndY=8976}),scene));
            await Reject<InvalidDataException>(()=>NativeFreshInitialSpawnClearancePlan.CaptureWizardAsync("not-opened",NativeFreshLevelDocumentIntent.Create(floor,[]),scene));
            var f2=await NativeWizardFamilyAssetPlan.CaptureAsync(original);
            Check(f2.BlockSha256==family.BlockSha256,"Wrong actual family equals original bytes.");
            Reject<InvalidDataException>(()=>result.RequireAncestry(doc,scene,c,f2,resources));
            var r2=await NativeId65FreshWizardResearchResourcePlan.CaptureAsync(original,c,family);
            Check(r2.ModelSha256==resources.ModelSha256,"Wrong actual resource equals original bytes.");
            Reject<InvalidDataException>(()=>result.RequireAncestry(doc,scene,c,family,r2));
            var c2=await NativeId65FreshDocumentCompilation.CaptureAsync(original,floor);
            Check(c2.CanonicalSha256==c.CanonicalSha256,"Wrong actual compilation equals original canonical bytes.");
            Reject<InvalidDataException>(()=>result.RequireAncestry(doc,scene,c2,family,resources));
            var highPlacement=Placement with{OriginZ=8909};var highScene=await NativeId65FreshWizardResearchScenePlan.CaptureAsync(original,c,family,resources,highPlacement);
            var high=await NativeFreshInitialSpawnClearancePlan.CaptureWizardAsync(original,Document(floor,highPlacement),highScene);
            Check(high.Finding.Centre?.Z==9149,"Actual non-grid Z preserved through transform, not defaulted.");
            var cornerPlacement=new NativeWizardPlacementIntent(8464,8464,8704,9456,9456);
            var cornerScene=await NativeId65FreshWizardResearchScenePlan.CaptureAsync(original,c,family,resources,cornerPlacement);var cornerDoc=Document(floor,cornerPlacement);
            var corner=await NativeFreshInitialSpawnClearancePlan.CaptureWizardAsync(original,cornerDoc,cornerScene);
            corner.RequireInitialSeparation(cornerDoc,cornerScene);
            Check(corner.Finding.DistanceSquared==755124&&!corner.TrajectoryProven&&!corner.PublicationApproved,"Actual separated pose is not floor or candidate approval.");
            var equalCornerScene=await NativeId65FreshWizardResearchScenePlan.CaptureAsync(original,c,family,resources,cornerPlacement);
            Check(equalCornerScene.SceneSha256==cornerScene.SceneSha256,"Separated wrong-scene control actually has equal bytes.");
            Reject<InvalidDataException>(()=>corner.RequireInitialSeparation(cornerDoc,equalCornerScene));
            var authored=await NativeId65FreshWizardResearchResourcePlan.CaptureAuthoredAsync(original,c,family);
            var authoredScene=await NativeId65FreshWizardResearchScenePlan.CaptureAuthoredAsync(original,doc,c,family,authored);
            var authoredResult=await NativeFreshInitialSpawnClearancePlan.CaptureWizardAsync(original,doc,authoredScene);
            Check(authoredResult.Finding.Status==NativeInitialSpawnClearanceStatus.Overlap,"New authored path preserves actual unsafe intent and refuses its gate.");
            var equalDocument=Document(floor,Placement);
            Check(NativeFreshLevelDocumentCodec.SerializeCanonical(equalDocument).SequenceEqual(NativeFreshLevelDocumentCodec.SerializeCanonical(doc)),"Document reissue fixture has genuinely equal complete bytes.");
            await Reject<InvalidDataException>(()=>NativeFreshInitialSpawnClearancePlan.CaptureWizardAsync("not-opened",equalDocument,authoredScene));
            var differentId=NativeFreshLevelDocumentIntent.Create(floor,[NativeFreshWizardRouteIntent.Create("22222222222222222222222222222222",8704,8960,8704,9216,8960)]);
            await Reject<InvalidDataException>(()=>NativeFreshInitialSpawnClearancePlan.CaptureWizardAsync("not-opened",differentId,authoredScene));
            Check(scene.SceneSha256=="b7bd222702ff9b5d6ea40c623159f4aa22b3425ef9368059167d1ccab930266b"&&scene.CopyScene().SequenceEqual(before)&&resources.CopyModelPayload().SequenceEqual(models),"Historical unsafe scene and all actual resource bytes untouched.");
            Console.WriteLine($"planSha256={result.PlanSha256} status={result.Finding.Status} distanceSquared={result.Finding.DistanceSquared}");
        }
        finally{RequireOriginal(original);}
        Console.WriteLine($"INITIAL_SPAWN_CLEARANCE_SOURCE_OK checks={Checks} refusals={Refusals} outputsWritten=0");
    }
    private static void RequireOriginal(string path)
    {using var f=File.OpenRead(path);Check(f.Length==661547040&&Convert.ToHexStringLower(SHA256.HashData(f))=="fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37"&&f.Position==661547040&&f.ReadByte()==-1&&f.Position==661547040&&f.Length==661547040,"Full original before/finally SHA/true EOF.");}
}
