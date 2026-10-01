using System.Buffers.Binary;
using System.Text.Json;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;
using Spyro.Editor.Core.Universal;
using Spyro.Editor.Core.Workspace;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Editing;
int checks = 0;
void Check(bool okay, string message) { checks++; if (!okay) throw new InvalidDataException(message); }
Check(UniversalCapacityPlanner.IsCapacityFailure("Not enough free texture memory in the destination level"), "Texture capacity classification");
Check(UniversalCapacityPlanner.IsCapacityFailure("Universal RAM budget exceeded"), "RAM classification");
Check(!UniversalCapacityPlanner.IsCapacityFailure("Class 0x13B has neither a model nor an extractable handler; its dependency is unresolved."), "Missing dependency must not offer destination removals");
Check(!UniversalCapacityPlanner.IsCapacityFailure("A model frame is outside its source package."),"Malformed animation frame is not a RAM capacity problem");
var allocator = new UniversalVramAllocator([]);
Check(allocator.Rectangle(16,32) != null && allocator.Clut() != null, "Source-free pixel/palette allocation");
Check(allocator.ImportedReservations.Sum(r=>r.Length) == 544, "Reservation includes every allocated zero pixel and palette byte");
Check(allocator.Clone().ImportedReservations.SequenceEqual(allocator.ImportedReservations), "Cloned allocator preserves reservation proof");
allocator.Reserve(2048,32);
Check(allocator.ImportedReservations[^1].Offset == 2048, "In-place equivalent texture storage is also reserved");
if(args.FirstOrDefault()=="--runtime-guards")
{
    await RuntimeInteractionGuardCases.Run(args[1],args[2],Check);
    Console.WriteLine($"UNIVERSAL_RUNTIME_GUARD_OK checks={checks}");return;
}
if(args.FirstOrDefault()=="--prepared-growth")
{
    await PreparedPrivateTerrainGrowthCases.Run(args[1],args[2],args[3],args[4],Check);
    Console.WriteLine($"UNIVERSAL_PREPARED_GROWTH_OK checks={checks}");return;
}
if (args.Length >= 4)
{
    var catalog = LevelCatalog.Load(args[1]);
    var lib = new UniversalSourceLibrary(args[0], catalog);
    var japan = new UniversalJapaneseSource(args[3]);
    var special = UniversalMobyExport.SpecialProvider(japan);
    string snapshot = args[2];
    var placed = UniversalPlacementReader.Read(Path.Combine(snapshot, "artisans-native-edits.json"));
    var removed = UniversalPlacementReader.ReadRemovedNativeRows(Path.Combine(snapshot, "artisans-native-edits.json"));
    var source = lib.Get("artisans");
    var names = MobyLoader.LoadCached(Path.Combine(snapshot, "editor-cache/artisans-mobys.json")).ToList();
    MobyMetadataEnricher.Apply(new EditorWorkspace(snapshot), "artisans", names);
    var labels = names.ToDictionary(m => m.TrueIndex, m => (m.DisplayLabel, m.CandidateKind));
    UniversalMobyPlacement Trial(string key, int row) => new(key,row, 0,0,0, EditorId:"capacity-candidate",PersistentSlot:placed.Count);
    var trial = placed.Append(Trial("wizardpeak",18)).ToList();
    var baseline = UniversalMobyCatalogBuilder.DryRun(lib,source.Level, placed,special,removed);
    Check(baseline.Error == null && baseline.Result!.Ram.FinalHeadroom == 8444, "Saved project baseline must remain composable");
    var magic = UniversalCapacityPlanner.Analyze(lib,source.Level,placed.Append(Trial("magiccrafters",1)).ToList(),removed,
        row => labels.GetValueOrDefault(row,($"Object T{row}","unknown")),special);
    Check(magic.CurrentError?.Contains("0x13B") == true && magic.Checks == 1 && magic.Plans.Count == 0, "Source child dependency is distinct from capacity");
    var advice = UniversalCapacityPlanner.Analyze(lib,source.Level,trial,removed,
        row => labels.GetValueOrDefault(row,($"Object T{row}","unknown")),special);
    Check(advice.Plans.Count > 0, "Actual Artisans snapshot must get a verified source row2 capacity plan");
    var plan = advice.Plans.FirstOrDefault();
    Check(plan != null, "Ordinary enemy/pickup capacity plan must be found");
    Check(advice.Plans.All(p => p.Groups.All(g => g.EffectPriority <= 2)), "Default plans must avoid progression/transport/controller removals");
    var composed = UniversalMobyCatalogBuilder.DryRun(lib,source.Level,trial,special,removed.Concat(plan!.AdditionalRows).ToArray());
    Check(composed.Error == null, "Every emitted suggestion must pass a repeated real compose");
    var r = composed.Result!; var output = new UniversalLevelData(r.LevelData);
    Check(r.Ram.FinalHeadroom >= UniversalMobyComposer.RamSafetyMargin, "Native RAM margin remains enforced");
    Check(output.Scene().RowCount == r.NativeRowsAfter && r.NewRowIndices.Distinct().Count() == trial.Count, "Physical actors/readback are distinct");
    Check(r.DroppedModelClasses.Contains(0x72) && plan.Groups.All(g=>r.DroppedModelClasses.Contains(g.ClassId)), "Every selected exclusively removed model is actually absent");
    Check(!output.Registrations().Any(reg => plan.Groups.Any(g=>g.ClassId==reg.ClassId)), "Registration readback confirms actual model reclamation");
    Check(r.PlacementSaveRows.SequenceEqual(Enumerable.Range(source.Data.Scene().RowCount,trial.Count)), "Existing lifetime save identities are unchanged");
    Check(BinaryPrimitives.ReadUInt16LittleEndian(output.Row(159).AsSpan(0x36)) == BinaryPrimitives.ReadUInt16LittleEndian(source.Data.Row(159).AsSpan(0x36)), "Legacy sign proxy T159 must not replace the native row");
    var imported = r.Classes.Single(c => c.SourceLevelKey == "wizardpeak" && c.ClassId == 0x11C && !c.DependencyOnly);
    Check(BinaryPrimitives.ReadUInt16LittleEndian(output.Row(r.NewRowIndices[^1]).AsSpan(0x36)) == imported.DestinationClassId, "Source row matches explicit mapped registration");
    if (args.Length >= 5)
    {
        var ordinary = JsonSerializer.Deserialize<TerrainPatchPlan>(File.ReadAllText(args[4]))!;
        var saved = NativeTerrainTextureRelocationEditStore.Load(snapshot,"artisans");
        var request = new NativeTerrainTexturePrivateRecordBatchRequest(args[0],Path.ChangeExtension(args[0],".cue"),
            Path.Combine(Path.GetTempPath(),"capacity-proof-unused"),"",source.Level,saved,
            NativeTerrainTexturePrivateRecordBatchCompiler.ConvertOrdinaryTerrainPlan(ordinary));
        var reservations = UniversalTerrainReservations.Capture(lib,source.Level,placed,special,removed);
        Check(reservations.Sum(range=>range.Length) == 2176,"Exact eight-import reservation metadata");
        var privatePlan = NativeTerrainTexturePrivateRecordBatchCompiler.BuildPlan(request with { AdditionalProtectedRanges = reservations });
        Check(privatePlan.FixedTailPlan is { GlobalPackingProofComplete:true, PatchPreimagesVerified:true, CombinedPatchesDisjoint:true },"Joint terrain logical pixel/palette/alias/preimage proofs remain complete");
        var privateHost = UniversalCompositionHost.FromPrivateTerrainPlan(args[0],source.Level,privatePlan);
        var baselineJoint = privateHost.DryRun(lib,placed,special,removed);
        Check(baselineJoint.Error == null && baselineJoint.Result!.Ram.FinalHeadroom == 8260,"Existing eight imports and private68 now compose without extra deletion");
        var privateData = privateHost.DestinationData;
        int component = privateData.Subfiles()[1].Offset;
        Check(BinaryPrimitives.ReadInt32LittleEndian(privateData.Bytes.AsSpan(component+4)) == 69,"Prepared terrain contains actual appended texture68");
        var direct = NativeTexturePageOwnershipScanner.ScanExcludingResidentClasses(args[0],source.Level,new HashSet<int>(),privateData.Bytes);
        Check(direct.AllConsumerClosureComplete,"Prepared native terrain/actor/shared/cutscene ownership remains proven");
        var extra = new[]{32,33,94,109};var gone = removed.Concat(extra).Distinct().ToArray();
        var jointReservations = UniversalTerrainReservations.Capture(lib,source.Level,trial,special,gone);
        var jointPlan = NativeTerrainTexturePrivateRecordBatchCompiler.BuildPlan(request with { AdditionalProtectedRanges=jointReservations });
        var jointHost = UniversalCompositionHost.FromPrivateTerrainPlan(args[0],source.Level,jointPlan);
        var joint = jointHost.DryRun(lib,trial,special,gone);
        Check(joint.Error == null && joint.Result!.Ram.FinalHeadroom == 33852,"Wizard Peak18+all saved imports+private68 fit with four extra removals");
        Check(!joint.Result!.Classes.Any(c=>c.SourceLevelKey=="wizardpeak"&&c.ClassId==0x13B),"Wizard source has no invented Alpine child dependency");
        Check(joint.Result.PlacementSaveRows.SequenceEqual(Enumerable.Range(174,9)),"Joint packing preserves all lifetime IDs");
        var hosts = new Dictionary<string,UniversalCompositionHost>();
        (UniversalComposeResult? Result,string? Error) ExactCheck(IReadOnlyCollection<int> proposed)
        {
            var checkedHost=privateHost.DryRun(lib,trial,special,proposed);
            if(checkedHost.Error==null || UniversalCapacityPlanner.IsCapacityFailure(checkedHost.Error))
            {
                var ranges=UniversalTerrainReservations.Capture(lib,source.Level,trial,special,proposed);
                if(ranges.Count>0)
                {
                    string key=string.Join(';',ranges.Select(r=>$"{r.Offset}:{r.Length}"));
                    if(!hosts.TryGetValue(key,out var prepared))hosts[key]=prepared=UniversalCompositionHost.FromPrivateTerrainPlan(args[0],source.Level,
                        NativeTerrainTexturePrivateRecordBatchCompiler.BuildPlan(request with{AdditionalProtectedRanges=ranges}));
                    checkedHost=prepared.DryRun(lib,trial,special,proposed);
                }
            }
            return checkedHost;
        }
        var exactAdvice=UniversalCapacityPlanner.Analyze(lib,source.Level,trial,removed,
            row=>labels.GetValueOrDefault(row,($"Object T{row}","unknown")),special,host:privateHost,compositionCheck:ExactCheck);
        Console.WriteLine(JsonSerializer.Serialize(new{Scope="actual private68/eight saved terrain paints prepared host",Advice=exactAdvice,Status="composed-static/readback; gameplay not tested"}));
        Check(exactAdvice.Plans.Count>0 && exactAdvice.Plans.First().AdditionalRows.Count<=4,"Prepared-host planner recommends no more removals than the direct four-removal proof");
        var repeated=ExactCheck(removed.Concat(exactAdvice.Plans.First().AdditionalRows).ToArray());
        Check(repeated.Error==null && repeated.Result!.Ram.FinalHeadroom==exactAdvice.Plans.First().FinalRamHeadroom,"Planner reports repeated actual private-terrain compose RAM, not retail estimate");
        var repeatedData=new UniversalLevelData(repeated.Result!.LevelData);
        Check(new[]{0x95,0x1A5}.Where(c=>!exactAdvice.Plans.First().DroppedModelClasses.Contains(c)).All(c=>repeatedData.Registrations().Any(r=>r.ClassId==c)),"Unselected Gnorc/LifeChest model owners survive smallest measured plan");
        Console.WriteLine(JsonSerializer.Serialize(new{Scope="repeated smallest prepared-host plan",AdditionalRemoved=exactAdvice.Plans.First().AdditionalRows,
            repeated.Result.NewRowIndices,repeated.Result.PlacementSaveRows,repeated.Result.Ram,repeated.Result.DroppedModelBytes,repeated.Result.ReclaimedVramBytes,
            NativeRowsAfter=repeatedData.Scene().RowCount,Status="exact composed-static/readback; runtime not tested"}));
        var corrupt = privatePlan with { FixedTailPlan = privatePlan.FixedTailPlan! with { CombinedPatches = privatePlan.FixedTailPlan!.CombinedPatches.Select((p,i)=>i==0?p with { ByteLength=p.ByteLength+1 }:p).ToArray() } };
        bool refused=false;try{UniversalCompositionHost.FromPrivateTerrainPlan(args[0],source.Level,corrupt);}catch(InvalidDataException){refused=true;}
        Check(refused,"Prepared host rejects inconsistent exact patch lengths");
        var globalRequest = new NativeTerrainTextureGlobalRepackSyntheticRequest(args[0],source.Level,[],[new(68,14,69,0)])
            { AdditionalProtectedRanges=[new(UniversalVramAllocator.ImageBytes,1,"invalid reservation")] };
        Check(!NativeTerrainTextureGlobalRepackerResearch.TryBuild(globalRequest,out var _,out var invalid) && invalid.Contains("reservation"),"Reservation cannot exceed owned VRAM boundary");
        if(args.Contains("--growth"))
        {
            string scratch=Path.Combine(Path.GetTempPath(),"spyro-capacity-growth-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(scratch);
            try
            {
                // The captured GUI fixture references another byte-identical USA path. This
                // probe uses the preserved source path and authenticates every patch preimage.
                var probeOrdinary=ordinary with{SourceImagePath=args[0]};
                var painted=UniversalCompositionHost.FromTerrainPlan(args[0],source.Level,probeOrdinary).DestinationData;
                painted.GrowSubfile(1,0x800);
                uint copy=UniversalExeBindings.Read(lib.Disc.Exe,source.Level.LevelId)[UniversalExeBindings.CopyBufferGlobal].Value;
                var replacement=new NativeTerrainLevelReplacement(source.Level.SourceWadEntry,source.Data.Bytes.ToArray(),painted.Bytes.ToArray(),
                    UniversalMobyComposer.RuntimeEnd(source.Data,copy),UniversalMobyComposer.RuntimeEnd(painted,copy)){LevelId=source.Level.LevelId};
                var growing=probeOrdinary with{Patches=[],PatchCount=1,NativeLevelReplacement=replacement};
                bool rejected=false;try{NativeTerrainTexturePrivateRecordBatchCompiler.ConvertOrdinaryTerrainPlan(growing);}catch(InvalidDataException){rejected=true;}
                Check(rejected,"Wholebody growth cannot silently pass equal-length patch conversion");
                var prepRequest=request with{OrdinaryLevelDataPatches=[],TerrainPreparation=growing,AdditionalProtectedRanges=reservations};
                var prepared=await NativeTerrainTexturePreparedSourceCompiler.PrepareAsync(prepRequest,Path.Combine(scratch,"terrain"));
                Check(string.Equals(prepared.AuthorizationBinding?.SourceImageSha256,saved[0].SourceImageSha256,StringComparison.OrdinalIgnoreCase),"Prepared private terrain retains authenticated original promotion owner");
                Check(prepared.Request.TerrainPreparation==null && prepared.Request.SavedEdits[0].SourceImageSha256!=saved[0].SourceImageSha256 &&
                    prepRequest.SavedEdits[0].SourceImageSha256==saved[0].SourceImageSha256,"Prepared contracts rebound in memory without changing saved original intent");
                var growthPlan=NativeTerrainTexturePrivateRecordBatchCompiler.BuildPlan(prepared.Request);
                var growthHost=UniversalCompositionHost.FromPrivateTerrainPlan(args[0],source.Level,growthPlan,prepared.Request.SourceImagePath);
                var grown=growthHost.DryRun(lib,placed,special,removed);
                Console.WriteLine(JsonSerializer.Serialize(new{Scope="prepared growing native terrain source",grown.Error,Ram=grown.Result?.Ram,
                    replacement.OriginalRuntimeEnd,replacement.FinalRuntimeEnd}));
                Check(grown.Error==null && grown.Result!.Ram.FinalHeadroom==8260-(replacement.FinalRuntimeEnd-replacement.OriginalRuntimeEnd),
                    "Native2KB terrain allocation growth/private68/all eight imports compose under actual used native RAM guard");
                File.Delete(prepared.Request.SourceImagePath);
                Check(growthHost.DryRun(lib,placed,special,removed).Error==null,"Prepared-host dropped-owner scans remain valid after disposable image cleanup");
                bool forged=false;try{await NativeTerrainTexturePreparedSourceCompiler.PrepareAsync(prepRequest with{TerrainPreparation=growing with{
                    NativeLevelReplacement=replacement with{OriginalRuntimeEnd=replacement.OriginalRuntimeEnd+16}}},Path.Combine(scratch,"forged"));}catch(InvalidDataException){forged=true;}
                Check(forged&&!File.Exists(Path.Combine(scratch,"forged.bin")),"Forged original runtime bound refused before publication");
                bool stale=false;try{await NativeTerrainTexturePreparedSourceCompiler.PrepareAsync(prepRequest with{SavedEdits=saved.Select(e=>e with{SourceImageSha256=new string('0',64)}).ToArray()},
                    Path.Combine(scratch,"stale"));}catch(InvalidDataException){stale=true;}
                Check(stale&&!File.Exists(Path.Combine(scratch,"stale.bin")),"Stale original private binding cannot be laundered through growth");
                Console.WriteLine(JsonSerializer.Serialize(new{Scope="synthetic native2KB terrain allocation growth/private68/eight imports",RamHeadroom=grown.Result!.Ram.FinalHeadroom,
                    Status="composed-static/readback; actual sculpt/gameplay not tested"}));
            }
            finally{Directory.Delete(scratch,true);}
        }
        Console.WriteLine(JsonSerializer.Serialize(new{JointBaselineHeadroom=baselineJoint.Result!.Ram.FinalHeadroom,
            DruidHeadroom=joint.Result.Ram.FinalHeadroom, AdditionalRemoved=extra, ReservationBytes=reservations.Sum(r=>r.Length),
            joint.Result.NewRowIndices,joint.Result.PlacementSaveRows,joint.Result.DroppedModelBytes,joint.Result.ReclaimedVramBytes,
            TerrainTextureCount=69,Status="exact composed-static/readback; runtime not tested"}));
    }
    Console.WriteLine(JsonSerializer.Serialize(new { Scope="raw retail host; private terrain excluded",SourceSha256=Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.OpenRead(args[0]))), Advice=advice, PhysicalRows=r.NewRowIndices,
        SaveRows=r.PlacementSaveRows, r.DroppedModelClasses,r.DroppedModelBytes,r.ReclaimedVramBytes,r.Ram,
        r.NativeRowsBefore,r.NativeRowsAfter,ImportedClass=imported, Status="composed-static/readback; gameplay not tested" }));
}
Console.WriteLine($"UNIVERSAL_CAPACITY_PLAN_OK checks={checks}");
