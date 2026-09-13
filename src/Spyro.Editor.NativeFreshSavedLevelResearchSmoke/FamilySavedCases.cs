using System.Text.Json;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using static Cases;

internal static class FamilySavedCases
{
    internal static NativeFreshLevelDocumentIntent Document(int entry=10,int index=121,int nativeClass=501,
        int x=9152,int y=9152,int z=8704,int yaw=64) =>
        SavedFamilyDocumentFixture.Document(entry,index,nativeClass,x,y,z,yaw);
    internal static async Task Free()
    {
        var valid=Document();
        var old=MixedSavedCases.Document().UpgradeForNativeMobys();
        var historical=old.WithContent(old.Base,[old.Placements[0],NativeFreshMobyPlacementIntent.Create(
            "22222222222222222222222222222222",NativeFreshRetailMobyReference.Create(
                NativeFreshRetailTerrainMaterialIntent.SupportedSourceVariant,10,121,501),9152,9152,8704,64)]);
        Check(NativeFreshLevelDocumentCodec.SerializeCanonical(valid).SequenceEqual(NativeFreshLevelDocumentCodec.SerializeCanonical(historical)),
            "Shared Core/UI fixture retains the former literal schema3 canonical content and stable IDs.");
        Check(NativeFreshSavedLevelResearchEligibility.IsSupportedFamilyProfile(valid)&&
            NativeFreshSavedLevelResearchEligibility.IsSupportedFamilyProfile(NativeFreshLevelDocumentCodec.Parse(
                NativeFreshLevelDocumentCodec.SerializeCanonical(valid)))&&!NativeFreshSavedLevelResearchEligibility.IsSupportedMixedProfile(valid),
            "Exact family shape and canonical reopen are neutral eligibility, never the old mixed arm.");
        Check(!NativeFreshSavedLevelResearchEligibility.IsSupportedFamilyProfile(null),"Missing document grants no family eligibility.");
        foreach(var mixed in new[]{MixedSavedCases.Document(),MixedSavedCases.Document(secondEntry:14),
            MixedSavedCases.Document().WithFloorMaterials(NativeFreshFloorMaterialsIntent.Create(Retail(70,1),Retail(14,1)))})
            Check(NativeFreshSavedLevelResearchEligibility.IsSupportedMixedProfile(mixed)&&
                !NativeFreshSavedLevelResearchEligibility.IsSupportedFamilyProfile(mixed)&&
                !NativeFreshSavedLevelResearchEligibility.IsSupportedFamilyProfile(mixed.UpgradeForNativeMobys()),
                "All three old schema2 mixed arms retain their own eligibility; a schema3 upgrade alone is not family admission.");
        var p0=NativeFreshLevelDocumentIntent.Create(valid.Base,[]).WithFloorMaterials(valid.FloorMaterials!);
        Check(!NativeFreshSavedLevelResearchEligibility.IsSupportedFamilyProfile(p0)&&
            !NativeFreshSavedLevelResearchEligibility.IsSupportedFamilyProfile(NativeFreshLevelDocumentIntent.Create(valid.Base,[valid.Placements[0]])),
            "P0 and schema1 are not silently recast as a family profile.");
        var (_,saved)=Save("family-source-preflight",Document());
        var error=await Throws<IOException>(()=>NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync(Missing,Missing,saved.Snapshot));
        Check(Leaves(error)[0].Message.StartsWith("Textured research source path is missing",StringComparison.Ordinal),
            "Complete supported schema3 reaches source authentication through the one existing saved factory.");
        foreach(var (name,invalid) in SavedFamilyDocumentFixture.Unsupported())
        {
            Check(!NativeFreshSavedLevelResearchEligibility.IsSupportedFamilyProfile(invalid)&&
                !NativeFreshSavedLevelResearchEligibility.IsSupportedMixedProfile(invalid),
                "Near-miss family/native intent refuses both neutral predicates: "+name);
            var (_,bad)=Save("family-unsupported-"+name,invalid);
            var refusal=await Throws<InvalidDataException>(()=>NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync(Missing,Missing,bad.Snapshot));
            Check(!Leaves(refusal).Any(e=>e is FileNotFoundException or DirectoryNotFoundException),
                "Unsupported full schema3 is not silently reduced to the old Wizard/no-placement arm.");
        }
        foreach(string owner in new[]{"raw","manifest"})
        {
            var (project,stale)=Save("family-stale-"+owner,valid);
            File.AppendAllText(owner=="raw"?stale.Snapshot.Path:project.ManifestPath," ");
            await Throws<InvalidDataException>(()=>NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync(Missing,Missing,stale.Snapshot));
        }
    }
    internal static async Task Source(string bin,string cue)
    {
        var (project,saved)=Save("family-saved-publication",Document());
        var plan=await NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync(bin,cue,saved.Snapshot);
        var family=plan.FamilyCompilation!;
        plan.RequireFamilyAncestry(plan.Saved,family);
        Check(ReferenceEquals(family.Saved,plan.Saved)&&ReferenceEquals(family.Document,saved.Document)&&
            ReferenceEquals(family.BaseDocument.Base,saved.Document.Base)&&
            ReferenceEquals(family.BaseDocument.Placements[0],saved.Document.Placements[0])&&
            ReferenceEquals(family.BaseDocument.FloorMaterials,saved.Document.FloorMaterials),
            "Whole actual saved document retained separately from explicitly derived base-body subdocument.");
        Check(family.BaseDocument.SchemaVersion==2&&family.Document.SchemaVersion==3&&
            family.Document.Placements.Count==2&&family.Composition.Placements.Select(p=>p.EditorId)
                .SequenceEqual(saved.Document.Placements.Select(p=>p.EditorId)),"Every saved placement resolves in its actual order; no row is discarded.");
        var native=(NativeFreshMobyPlacementIntent)saved.Document.Placements[1];var placed=family.Composition.Placements[1];
        Check(placed.PassiveAsset!.DonorWadEntry==native.Source.SourceWadEntry&&placed.PassiveAsset.DonorTrueIndex==native.Source.SourceTrueIndex&&
            placed.PassiveAsset.NativeClass==native.Source.ExpectedNativeClass&&placed.X==native.X&&placed.Y==native.Y&&placed.Z==native.Z&&
            placed.NativeYawByte==native.NativeYawByte,"Neutral reference is checked against actual source asset and complete transform.");
        Check(ReferenceEquals(plan.MixedDisc,family.Disc)&&ReferenceEquals(family.Disc.FamilyCompositionAncestor,family.Composition)&&
            family.Composition.LevelDataSha256=="7812d34d4253f38770dd85d49b3e24d72c7a6aa4896e667ef87562191ae86262"&&
            plan.ExpectedChangedRawSectors==1292&&plan.ExpectedImageSha256!=MixedSavedCases.Expected&&!plan.RuntimeVerified,
            "Existing Disc stream owns the composed body and enlarged sector range without borrowing old image or runtime acceptance.");
        var census=family.Disc.ExecutableAncestor.FamilyCensus!;
        Check(census.PlacedCount==2&&census.StaticTreasureTarget==10&&!census.RuntimeDropGuarantee,
            "Family-specific static treasure is ten, not two times ten or a no-runtime-drop promise.");
        var binding=NativeId65FreshResearchPublicationBinding.FromSavedLevel(plan);
        Check(ReferenceEquals(binding.SavedLevelPlan,plan)&&binding.WizardPlan is null,"Same saved publication owner and transaction remain active.");
        var measured=new NativeId65FreshDiscVerification(plan.ExpectedImageSha256,1292);
        byte[] canonical=plan.CopyCanonicalDocument(),receipt=plan.BuildReceipt(measured);
        Check(canonical.SequenceEqual(NativeFreshLevelDocumentCodec.SerializeCanonical(saved.Document)),"Candidate canonical retains source tuple and both rows.");
        using(var json=JsonDocument.Parse(receipt))
        {
            var root=json.RootElement;var revision=root.GetProperty("saved");var lineage=root.GetProperty("lineage");
            Check(root.GetProperty("variant").GetString()=="schema3-two-retail-materials-wizard-grass-p2-n32"&&
                revision.GetProperty("rawSha256").GetString()==saved.Snapshot.ContentSha256&&
                revision.GetProperty("canonicalSha256").GetString()==Hash(canonical)&&
                revision.GetProperty("canonical").GetProperty("placements").GetArrayLength()==2&&
                lineage.GetProperty("familyCompositionPlan").GetString()==family.Composition.PlanSha256&&
                !root.GetProperty("runtimeVerified").GetBoolean()&&!root.GetProperty("completeNativeDependencyClosureProven").GetBoolean(),
                "Receipt contains complete saved authorship and actual family ancestry with honest pending runtime scope.");
            var rows=revision.GetProperty("canonical").GetProperty("placements");var resolved=lineage.GetProperty("families");
            Check(resolved.GetArrayLength()==rows.GetArrayLength()&&Enumerable.Range(0,rows.GetArrayLength()).All(i=>
                JsonElement.DeepEquals(resolved[i].GetProperty("authoredPlacement"),rows[i])&&
                resolved[i].GetProperty("EditorId").GetString()==saved.Document.Placements[i].EditorId&&
                resolved[i].GetProperty("sourceFamilyPlan").GetString()==(family.Composition.Placements[i].WizardAsset?.PlanSha256??
                    family.Composition.Placements[i].PassiveAsset!.PlanSha256)),
                "Every receipt resolved family binds the exact authored row, not Wizard placeholder coordinates or a reordered source plan.");
        }
        var repeated=await NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync(bin,cue,NativeFreshLevelDocumentStore.Capture(project));
        Check(repeated.PlanSha256==plan.PlanSha256&&repeated.BuildReceipt(measured).SequenceEqual(receipt),"Saved family reopen/recompile is deterministic.");
        await Throws<InvalidDataException>(()=>{plan.RequireFamilyAncestry(plan.Saved,repeated.FamilyCompilation!);return Task.CompletedTask;});
        await Throws<InvalidDataException>(()=>{plan.RequireFamilyAncestry(saved,family);return Task.CompletedTask;});
        await Throws<InvalidDataException>(()=>{plan.RequireMixedAncestry(plan.Saved,plan.MixedDisc!,plan.MixedClearance!);return Task.CompletedTask;});
        await Throws<InvalidDataException>(()=>{_=plan.MaterialCompilation;return Task.CompletedTask;});
        await Throws<InvalidDataException>(()=>{plan.BuildReceipt(new(plan.ExpectedImageSha256,1290));return Task.CompletedTask;});
        canonical[0]^=1;receipt[0]^=1;
        Check(plan.CopyCanonicalDocument()[0]!=canonical[0]&&plan.BuildReceipt(measured)[0]!=receipt[0],"Family receipt/canonical copies cannot mutate owners.");
        File.AppendAllText(saved.Snapshot.Path," \n");await Throws<InvalidDataException>(()=>plan.RequireFreshAsync());
        var revised=await NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync(bin,cue,NativeFreshLevelDocumentStore.Capture(project));
        Check(revised.PlanSha256!=plan.PlanSha256&&revised.ExpectedImageSha256==plan.ExpectedImageSha256&&
            revised.CopyCanonicalDocument().SequenceEqual(plan.CopyCanonicalDocument()),"Raw-only saved family change preserves native bytes but requires a new revision owner.");
        Console.WriteLine($"FAMILY_SAVED_SOURCE_OK checks={Checks} image={plan.ExpectedImageSha256} publication=false runtime=false root={project.RootPath}");
    }
}
