using System.Globalization;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using static Cases;

// Tests first. Source mode is read-only for native artifacts; no UI predicate,
// generic family export or runtime certificate is enabled by this contract.
internal static class TiledFamilySavedCandidateCases
{
    internal const string Variant="schema5-four1536-tile-r03-wizard-grass-locked-chest-p9-n32";
    internal const string ExpectedImage=SavedTiledFamilyCompilationCases.ExpectedImage;

    internal static async Task FreeAsync()
    {
        var document=SavedTiledFamilyCompilationCases.Document();
        var (_,saved)=Save("tiled-family-candidate-preflight",document);
        var missing=await Throws<IOException>(()=>NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync(Missing,Missing,saved.Snapshot));
        Check(Leaves(missing)[0].Message.StartsWith("Textured research source path is missing",StringComparison.Ordinal),
            "Exact complete tiled family reaches actual source authentication through the existing public saved factory.");
        await Throws<ArgumentException>(()=>NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync("\0","\0",saved.Snapshot));
        await Throws<OperationCanceledException>(()=>NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync("\0","\0",saved.Snapshot,new(true)));
        foreach(var (name,invalid) in Unsupported())
        {
            var (_,bad)=Save("tiled-family-candidate-invalid-"+name,invalid);
            var error=await Throws<InvalidDataException>(()=>NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync("\0","\0",bad.Snapshot));
            Check(!Leaves(error).Any(e=>e is ArgumentException or FileNotFoundException or DirectoryNotFoundException)&&
                NativeFreshLevelDocumentCodec.SerializeCanonical(bad.Document).SequenceEqual(NativeFreshLevelDocumentCodec.SerializeCanonical(invalid)),
                "Near miss refuses before source normalization and retains the entire saved intent: "+name);
        }
        foreach(string owner in new[]{"raw","manifest"})
        {
            var (project,stale)=Save("tiled-family-candidate-stale-"+owner,document);
            File.AppendAllText(owner=="raw"?stale.Snapshot.Path:project.ManifestPath," ");
            await Throws<InvalidDataException>(()=>NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync("\0","\0",stale.Snapshot));
        }
        Check(!NativeFreshSavedLevelResearchEligibility.IsSupportedLockedChestProfile(document)&&
            !NativeFreshSavedLevelResearchEligibility.IsSupportedTiledProfile(document)&&
            !NativeFreshSavedLevelResearchEligibility.IsSupportedTiledMaterialProfile(document),
            "Closed Candidate source issuance does not change saved UI eligibility or normal Create BIN.");
        Console.WriteLine($"TILED_FAMILY_SAVED_CANDIDATE_FREE_OK checks={Checks} originalOpened=false nativeImageWritten=false oldUiProfilesUnchanged=true");
    }

    private static IReadOnlyList<(string Name,NativeFreshLevelDocumentIntent Document)> Unsupported()
    {
        var d=SavedTiledFamilyCompilationCases.Document();var t=d.TiledContent!;
        var w=(NativeFreshWizardRouteIntent)d.Placements[0];var g=(NativeFreshMobyPlacementIntent)d.Placements[1];
        var c=(NativeFreshLockedChestFamilyPlacementIntent)d.Placements[2];var tiles=t.Tiles.ToArray();
        var old=tiles[3];tiles[3]=NativeFreshFlatTileIntent.Create(old.EditorId,old.X,old.Y,old.Z,old.Width,old.Depth,
            old.FloorColor,NativeFreshFloorMaterialsIntent.Create(Retail(70,1),Retail(14,1)));
        return [
            ("removed-family",d.WithPlacements([w,g])),("actor-order",d.WithPlacements([g,w,c])),
            ("default-rewards",d.WithPlacements([w,g,c.WithRewardDestinations(null)])),
            ("wrong-reward",d.WithPlacements([w,g,c.WithRewardDestinations(NativeFreshChestRewardDestinationOverrides.Create(
                [new(NativeFreshChestRewardRole.Reward4,10454,10503,9216)]))])),
            ("anchor",d.WithPlacements([w,g,c.WithAnchors(9729,9120,8704,10864,9888,8704)])),
            ("grass-source",d.WithPlacements([w,NativeFreshMobyPlacementIntent.Create(g.EditorId,
                NativeFreshRetailMobyReference.Create(Cases.Variant,10,120,501),g.X,g.Y,g.Z,g.NativeYawByte),c])),
            ("wizard-route",d.WithPlacements([NativeFreshWizardRouteIntent.Create(w.EditorId,9984,9984,8704,9456,9984),g,c])),
            ("last-face",d.WithTiledContent(t.WithTiles(tiles))),
            ("tile-order",d.WithTiledContent(t.WithTiles([t.Tiles[1],t.Tiles[0],t.Tiles[2],t.Tiles[3]]))),
            ("spawn",d.WithTiledContent(t.WithSettings(8976,8960,9216,64,t.SkyColor,19))),
            ("heading",d.WithTiledContent(t.WithSettings(8960,8960,9216,65,t.SkyColor,19))),
            ("track",d.WithTiledContent(t.WithSettings(8960,8960,9216,64,t.SkyColor,20)))];
    }

    internal static async Task SourceAsync(string bin,string cue)
    {
        var (project,saved)=Save("tiled-family-saved-candidate",SavedTiledFamilyCompilationCases.Document());
        var input=SavedLevelPublicationCases.SavedFiles.Capture(project);byte[]? expectedRaw=null;
        List<Exception> errors=[];
        try
        {
            var plan=await NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync(bin,cue,saved.Snapshot);
            var family=plan.TiledFamilyCompilation??throw new InvalidDataException("Missing exact tiled family Candidate arm.");
            plan.RequireTiledFamilyAncestry(plan.Saved,family);
            Check(ReferenceEquals(plan.SavedSnapshot,saved.Snapshot)&&ReferenceEquals(family.Document,saved.Document)&&
                plan.LockedChestCompilation is null&&plan.MixedDisc is null&&plan.MixedClearance is null&&
                plan.FamilyCompilation is null&&plan.TiledCompilation is null&&
                plan.ExpectedImageSha256==ExpectedImage&&plan.ExpectedChangedRawSectors==1297&&
                plan.ExpectedImageByteLength==661547040&&!plan.RuntimeVerified,
                "Complete actual saved tiled family selects one physical arm and the independent whole-image reference.");
            var binding=NativeId65FreshResearchPublicationBinding.FromSavedLevel(plan);
            Check(ReferenceEquals(binding.SavedLevelPlan,plan)&&binding.WizardPlan is null,
                "Tiled family uses the existing saved publisher role, source guards, leases and transaction.");
            family.Clearance.RequireStaticLandingClearance(family.Executable,family.ParentClearance);
            var measured=new NativeId65FreshDiscVerification(ExpectedImage,1297);
            byte[] canonical=plan.CopyCanonicalDocument(),receipt=plan.BuildReceipt(measured);
            Receipt(plan,family,receipt);
            await Throws<InvalidDataException>(()=>{plan.RequireTiledFamilyAncestry(saved,family);return Task.CompletedTask;});
            await Throws<InvalidDataException>(()=>{plan.RequireTiledFamilyAncestry(plan.Saved,null!);return Task.CompletedTask;});
            await Throws<InvalidDataException>(()=>{plan.RequireLockedChestAncestry(plan.Saved,null!);return Task.CompletedTask;});
            await Throws<InvalidDataException>(()=>{plan.RequireTiledAncestry(plan.Saved,null!);return Task.CompletedTask;});
            await Throws<InvalidDataException>(()=>{plan.RequireFamilyAncestry(plan.Saved,null!);return Task.CompletedTask;});
            await Throws<InvalidDataException>(()=>{plan.RequireMixedAncestry(plan.Saved,null!,null!);return Task.CompletedTask;});
            await Throws<InvalidDataException>(()=>{_=plan.MaterialCompilation;return Task.CompletedTask;});
            await Throws<InvalidDataException>(()=>{plan.BuildReceipt(new(ExpectedImage,1296));return Task.CompletedTask;});
            await Throws<InvalidDataException>(()=>{plan.BuildReceipt(new(new('0',64),1297));return Task.CompletedTask;});
            await Throws<OperationCanceledException>(()=>{plan.VerifyImage("\0",new(true));return Task.CompletedTask;});
            await Throws<OperationCanceledException>(()=>{plan.WriteNewImage("\0",new(true),()=>throw new InvalidDataException("No callback before cancellation."));return Task.CompletedTask;});
            var repeated=await NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync(bin,cue,NativeFreshLevelDocumentStore.Capture(project));
            Check(repeated.PlanSha256==plan.PlanSha256&&repeated.BuildReceipt(measured).SequenceEqual(receipt)&&
                !ReferenceEquals(repeated.TiledFamilyCompilation,family),
                "Same saved revision reissues identical complete receipt/native bytes, never interchangeable actual owners.");
            await Throws<InvalidDataException>(()=>{plan.RequireTiledFamilyAncestry(plan.Saved,repeated.TiledFamilyCompilation!);return Task.CompletedTask;});
            await Throws<InvalidDataException>(()=>{plan.RequireTiledFamilyAncestry(repeated.Saved,family);return Task.CompletedTask;});
            canonical[0]^=1;receipt[0]^=1;
            Check(canonical[0]!=plan.CopyCanonicalDocument()[0]&&receipt[0]!=plan.BuildReceipt(measured)[0],"Receipt/canonical copies are detached.");

            string authored=Encoding.UTF8.GetString(plan.CopyCanonicalDocument());
            foreach(var (before,after) in new[]{('1','a'),('2','b'),('3','c'),('4','d')})
                authored=authored.Replace(new string(before,32),new string(after,32),StringComparison.Ordinal);
            var renamed=NativeFreshLevelDocumentCodec.Parse(Encoding.UTF8.GetBytes(authored));
            var next=NativeFreshLevelDocumentStore.Save(NativeFreshLevelDocumentStore.Capture(project),renamed);
            expectedRaw=File.ReadAllBytes(next.Path);
            await Throws<InvalidDataException>(()=>plan.RequireFreshAsync());
            var idPlan=await NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync(bin,cue,next);
            Check(idPlan.ExpectedImageSha256==ExpectedImage&&idPlan.PlanSha256!=plan.PlanSha256&&
                !idPlan.CopyCanonicalDocument().SequenceEqual(plan.CopyCanonicalDocument())&&
                !idPlan.BuildReceipt(measured).SequenceEqual(plan.BuildReceipt(measured))&&
                idPlan.TiledFamilyCompilation!.Body.CopyLevelData().SequenceEqual(family.Body.CopyLevelData())&&
                idPlan.TiledFamilyCompilation.Executable.CopyExecutable().SequenceEqual(family.Executable.CopyExecutable()),
                "Tile plus all actor ID edits retain exact native bytes but change the complete saved owner and receipt.");
            Receipt(idPlan,idPlan.TiledFamilyCompilation!,idPlan.BuildReceipt(measured));
            File.AppendAllText(next.Path," \n");expectedRaw=File.ReadAllBytes(next.Path);
            await Throws<InvalidDataException>(()=>idPlan.RequireFreshAsync());
            var rawPlan=await NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync(bin,cue,NativeFreshLevelDocumentStore.Capture(project));
            Check(rawPlan.ExpectedImageSha256==ExpectedImage&&rawPlan.PlanSha256!=idPlan.PlanSha256&&
                rawPlan.CopyCanonicalDocument().SequenceEqual(idPlan.CopyCanonicalDocument())&&
                !rawPlan.BuildReceipt(measured).SequenceEqual(idPlan.BuildReceipt(measured)),
                "Raw-only saved revision remains separately bound despite equal canonical and native bytes.");
            Receipt(rawPlan,rawPlan.TiledFamilyCompilation!,rawPlan.BuildReceipt(measured));
            Console.WriteLine($"TILED_FAMILY_SAVED_CANDIDATE_SOURCE_OK checks={Checks} image={ExpectedImage} receiptBytes={plan.BuildReceipt(measured).Length} nativeImageWritten=false runtime=false");
        }
        catch(Exception error){errors.Add(error);}
        finally{try{input.RequireCurrent(expectedRaw);}catch(Exception error){errors.Add(error);}}
        ThrowFailures(errors);
    }

    // Separate explicit physical mode. Every case gets a fresh saved project;
    // fault evidence is retained and never repaired or reused by the next case.
    internal static async Task PhysicalAsync(string bin,string cue)
    {
        foreach(int operation in new[]{0,2,3,4,5,6,7,8})
        {
            string mode=SavedLevelPublicationCases.Modes[operation];
            var (project,saved)=Save("tiled-family-physical-"+operation,SavedTiledFamilyCompilationCases.Document());
            var input=SavedLevelPublicationCases.SavedFiles.Capture(project);
            string output=Path.Combine(Root,"output-"+operation);
            _=SavedLevelPublicationCases.RequireLocation(output,bin,cue,project.RootPath,false);
            SavedLevelFaultCases.Outcome? outcome=null;List<Exception> errors=[];
            NativeFreshSavedLevelResearchCandidatePlan? plan=null;
            try
            {
                plan=await NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync(bin,cue,saved.Snapshot);
                var family=plan.TiledFamilyCompilation??throw new InvalidDataException("Physical test requires actual tiled family compilation.");
                plan.RequireTiledFamilyAncestry(plan.Saved,family);
                Check(plan.ExpectedImageSha256==ExpectedImage&&plan.ExpectedChangedRawSectors==1297&&
                    plan.CopyCanonicalDocument().SequenceEqual(NativeFreshLevelDocumentCodec.SerializeCanonical(SavedTiledFamilyCompilationCases.Document())),
                    "Every physical test uses the complete exact canonical fixture and independently measured typed Candidate.");
                if(operation==0)
                {
                    var published=await NativeFreshSavedLevelResearchCandidatePublisher.PublishAsync(plan,output);
                    SavedLevelPublicationCases.RequireResult(plan,output,published);
                    var before=SavedLevelPublicationCases.ReadComplete(plan,output);
                    var verified=await NativeFreshSavedLevelResearchCandidatePublisher.VerifyAsync(plan,output);
                    SavedLevelPublicationCases.RequireResult(plan,output,verified);
                    Check(before.SequenceEqual(SavedLevelPublicationCases.ReadComplete(plan,output)),
                        "Clean publish plus independent physical Verify retain complete exact BIN/CUE/receipt and actual Original inverse.");
                }
                else outcome=await SavedLevelFaultCases.Run(plan,output,mode,input);
            }
            catch(Exception error){errors.Add(error);}
            finally
            {
                try{input.RequireCurrent(outcome?.ExpectedRawAfter);}catch(Exception error){errors.Add(error);}
                if(outcome?.ExpectedRawAfter is null)
                    try{NativeFreshDocumentStorage.RequireAggregateCurrent(saved.Snapshot,CancellationToken.None);}
                    catch(Exception error){errors.Add(error);}
            }
            ThrowFailures(errors);
            var artifacts=Directory.Exists(output)?Directory.EnumerateFiles(output,"*",SearchOption.AllDirectories)
                .Order(StringComparer.Ordinal).Select(path=>new{path,bytes=new FileInfo(path).Length}).ToArray():[];
            Console.WriteLine("TILED_FAMILY_SAVED_PHYSICAL_CASE_OK "+JsonSerializer.Serialize(new{
                mode,project=project.RootPath,output,plan!.PlanSha256,image=ExpectedImage,
                outcome=outcome?.Kind??"committed-clean",artifacts,
                evidence=outcome?.Evidence??[],runtimeVerified=false,normalCreateBinApproved=false}));
        }
        Console.WriteLine($"TILED_FAMILY_SAVED_PHYSICAL_OK cases=8 root={Root} image={ExpectedImage} runtime=false evidenceRetained=true");
    }

    private static void Receipt(NativeFreshSavedLevelResearchCandidatePlan plan,NativeFreshSavedTiledFamilyCompilation family,byte[] receipt)
    {
        Check(receipt.Length is >0 and <=65536,"Complete receipt fits the companion bound without truncation.");
        using var json=JsonDocument.Parse(receipt);using var canonical=JsonDocument.Parse(plan.CopyCanonicalDocument());
        var root=json.RootElement;var saved=root.GetProperty("saved");var lineage=root.GetProperty("lineage");
        Check(root.GetProperty("variant").GetString()==Variant&&root.GetProperty("planSha256").GetString()==plan.PlanSha256&&
            root.GetProperty("wholeImageInverseRequired").GetBoolean()&&root.GetProperty("savedRevisionBound").GetBoolean()&&
            saved.GetProperty("projectId").GetString()==plan.SavedSnapshot.ProjectId&&
            saved.GetProperty("root").GetString()==plan.SavedSnapshot.RootPath&&saved.GetProperty("path").GetString()==plan.SavedSnapshot.Path&&
            saved.GetProperty("rawSha256").GetString()==plan.SavedSnapshot.ContentSha256&&
            saved.GetProperty("manifestSha256").GetString()==plan.SavedSnapshot.ManifestSha256&&
            saved.GetProperty("canonicalSha256").GetString()==family.CanonicalDocumentSha256&&
            JsonElement.DeepEquals(saved.GetProperty("canonical"),canonical.RootElement),
            "Receipt binds the complete actual schema5 document and separate saved raw/manifest/path owners.");
        Check(root.GetProperty("SourceImagePath").GetString()==plan.SourceImagePath&&
            root.GetProperty("SourceCuePath").GetString()==plan.SourceCuePath&&root.GetProperty("SourceCueSha256").GetString()==plan.SourceCueSha256&&
            root.GetProperty("sourceImageSha256").GetString()==Original&&root.GetProperty("imageSha256").GetString()==ExpectedImage&&
            root.GetProperty("imageByteLength").GetInt64()==661547040&&root.GetProperty("changedRawSectors").GetInt32()==1297&&
            root.GetProperty("cueSha256").GetString()==Hash(plan.CopyCueBytes()),"Actual source-pair and independently measured image evidence.");
        foreach(var (key,value) in new[]{("compilationPlan",family.PlanSha256),("discPlan",family.Disc.PlanSha256),
            ("executablePlan",family.Executable.PlanSha256),("bodyPlan",family.Body.PlanSha256),("terrainPlan",family.Terrain.PlanSha256),
            ("parentDiscPlan",family.ParentDisc.PlanSha256),("parentExecutablePlan",family.ParentExecutable.PlanSha256),
            ("parentPlacementPlan",family.ParentPlacement.PlanSha256),("parentClearancePlan",family.ParentClearance.PlanSha256),
            ("tiledUnionClearancePlan",family.Clearance.PlanSha256)})
            Check(lineage.GetProperty(key).GetString()==value,"Actual typed lineage: "+key);
        Check(JsonElement.DeepEquals(lineage.GetProperty("tiledContent"),canonical.RootElement.GetProperty("content"))&&
            !lineage.TryGetProperty("floor",out _)&&lineage.GetProperty("tileCount").GetInt32()==4&&
            lineage.GetProperty("orderedFaceMaterials").EnumerateArray().Select(v=>v.GetInt32()).SequenceEqual(new[]{0,0,0,1,0,1,1,1})&&
            lineage.GetProperty("authoredPlacementCount").GetInt32()==3&&lineage.GetProperty("nativePlacedCount").GetInt32()==9&&
            lineage.GetProperty("nativeNominalCapacity").GetInt32()==32,
            "No legacy Base substitution: all tiles/IDs/settings/material faces and authored versus native counts survive.");
        var families=lineage.GetProperty("families");var placements=canonical.RootElement.GetProperty("placements");
        Check(families.GetArrayLength()==3&&Enumerable.Range(0,3).All(i=>
            families[i].GetProperty("EditorId").GetString()==family.Document.Placements[i].EditorId&&
            families[i].GetProperty("FirstRowIndex").GetInt32()==i&&families[i].GetProperty("RowCount").GetInt32()==(i==2?7:1)&&
            JsonElement.DeepEquals(families[i].GetProperty("authoredPlacement"),placements[i])),"Complete ordered authored row expansion.");
        var rows=lineage.GetProperty("nativeRowOwners");
        Check(rows.GetArrayLength()==9&&Enumerable.Range(0,9).All(i=>
            rows[i].GetProperty("nativeIndex").GetInt32()==i&&rows[i].GetProperty("actorClass").GetInt32()==family.ParentExecutable.Rows[i].ActorClassId&&
            rows[i].GetProperty("rowOffset").GetInt64()==family.Body.Allocation.Rows[i].Row.Offset&&
            rows[i].GetProperty("rowLength").GetInt64()==0x58&&
            rows[i].GetProperty("finalRowSha256").GetString()==family.ParentExecutable.Rows[i].SceneRowSha256),"Every complete actual fresh Scene row owner.");
        Check(lineage.GetProperty("sceneSha256").GetString()==family.Body.SceneSha256&&
            lineage.GetProperty("modelSha256").GetString()==family.Terrain.ModelSha256&&
            lineage.GetProperty("bodySha256").GetString()==family.Disc.BodySha256&&
            lineage.GetProperty("executableSha256").GetString()==family.Disc.ExecutableSha256&&
            lineage.GetProperty("dataSha256").GetString()==family.Terrain.DataSha256&&
            lineage.GetProperty("clearanceScope").GetString()=="exact-four-tile-union-initial-and-fixed-xy-landing-only",
            "Actual final complete domains and narrow union clearance scope, not borrowed old runtime approval.");
        foreach(var (name,margins) in new[]{("ChestMargins",family.Clearance.ChestMargins),("PlayerMargins",family.Clearance.PlayerMargins)})
        {
            var values=lineage.GetProperty("clearance").GetProperty(name);
            Check(values.EnumerateObject().Count()==4&&new[]{"Left","Right","Top","Bottom"}.Select(key=>values.GetProperty(key).GetString())
                .SequenceEqual(new[]{margins.Left,margins.Right,margins.Top,margins.Bottom}.Select(value=>value.ToString(CultureInfo.InvariantCulture))),
                "Receipt emits every actual signed invariant "+name+" value, never an empty internal struct.");
        }
        foreach(string flag in new[]{"runtimeVerified","normalCreateBinApproved","publicationApproved","nativePersistentSaveOwnerProven",
            "completeNativeDependencyClosureProven","hqRuntimeProven","audioRuntimeProven","peakRuntimeMemoryProven",
            "keyZeroContributionProven","completeStaticCensusProven","runtimeDropGuarantee","checkpointOrForeignStateSupported"})
            Check(!root.GetProperty(flag).GetBoolean(),"Unpromoted approval boundary: "+flag);
        Check(root.GetProperty("experimentalKeyZeroAssumption").GetBoolean()&&!root.TryGetProperty("publicationGuardPassed",out _),
            "Key-zero remains a premise; the receipt cannot preclaim a future publication guard.");
    }
}
