using System.Reflection;
using Avalonia.Controls;
using Spyro.Editor.App.Views;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using F = NativeFreshMaterialInspectionUiFixture;

// Real headless controls and tiny saved projects. Delayed/failing workers never
// construct a successful native inspection, Candidate or Publication.
internal static class NativeFreshFamilyAsyncReadinessCases
{
    private static readonly string[] TextFields=["FreshFamilyKeyX","FreshFamilyKeyY","FreshFamilyKeyZ",
        "FreshFamilyChestX","FreshFamilyChestY","FreshFamilyChestZ",
        ..Enumerable.Range(1,5).SelectMany(i=>new[]{"X","Y","Z"}.Select(axis=>$"FreshFamilyReward{i}{axis}"))];
    private static readonly string[] ModeFields=[..Enumerable.Range(1,5).Select(i=>$"FreshFamilyReward{i}Mode")];
    private static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static int checks;

    internal static void Run(string parent)
    {
        checks=0;
        Readiness(parent);
        foreach(string stage in new[]{"catalogue","inspection","picker","prepare"})
        foreach(string field in TextFields.Concat(ModeFields))
        foreach(bool aba in new[]{false,true})
            DelayedFields(parent,stage,field,aba);
        InspectionSnapshot(parent);
        PendingFamily(parent);
        PendingNativeInactiveFamily(parent);
        Console.WriteLine($"FRESH_FAMILY_ASYNC_READINESS_OK checks={checks} rawFields=26 structuredTickets=3 sourceOpened=false nativeOutputWritten=false");
    }

    private static void Readiness(string parent)
    {
        var document=NativeFreshFamilyDocumentSessionCases.Document();
        var chest=(NativeFreshLockedChestFamilyPlacementIntent)document.Placements[2];
        Check(document.SchemaVersion==5&&document.Placements.Count==3&&document.Base.FloorWidth==3072&&
            chest.RewardDestinations!.Values.Single() is {Role:NativeFreshChestRewardRole.Reward2,X:10454,Y:10503,Z:9216},
            "Shared fixture is the complete literal R03 document, not a projected Wizard or hidden family.");
        Check(!NativeFreshSavedLevelResearchEligibility.IsSupportedLockedChestProfile(null)&&
            NativeFreshSavedLevelResearchEligibility.IsSupportedLockedChestProfile(document)&&
            NativeFreshSavedLevelResearchEligibility.IsSupportedLockedChestProfile(NativeFreshLevelDocumentCodec.Parse(
                NativeFreshLevelDocumentCodec.SerializeCanonical(document))),"Exact neutral complete recipe is eligible, including a real canonical round trip.");
        var tiled=NativeFreshFamilyDocumentSessionCases.TiledDocument();
        var near=new Dictionary<string,NativeFreshLevelDocumentIntent>
        {
            ["default-rewards"]=Replace(chest.WithRewardDestinations(null)),
            ["reward-role"]=Replace(chest.WithRewardDestinations(NativeFreshChestRewardDestinationOverrides.Create(
                [new(NativeFreshChestRewardRole.Reward1,10454,10503,9216)]))),
            ["reward-x"]=Replace(chest.WithRewardDestinations(NativeFreshChestRewardDestinationOverrides.Create(
                [new(NativeFreshChestRewardRole.Reward2,10455,10503,9216)]))),
            ["anchor"]=Replace(chest.WithAnchors(9744,chest.KeyY,chest.KeyZ,chest.ChestX,chest.ChestY,chest.ChestZ)),
            ["order"]=document.WithPlacements([document.Placements[1],document.Placements[0],chest]),
            ["missing-grass"]=document.WithPlacements([document.Placements[0],chest]),
            ["materials"]=document.WithFloorMaterials(F.Pair(70,1,14,1)),
            ["tiled"]=tiled,
            ["edited-tiled"]=tiled.WithTiledContent(tiled.TiledContent!.WithSettings(8960,8960,9216,65,new(128,96,64),19))
        };
        Check(!NativeFreshSavedLevelResearchEligibility.IsSupportedLockedChestProfile(tiled.WithPlacements([]))&&
            !NativeFreshSavedLevelResearchEligibility.IsSupportedTiledProfile(tiled.WithPlacements([]))&&
            !NativeFreshSavedLevelResearchEligibility.IsSupportedTiledMaterialProfile(tiled.WithPlacements([])),
            "Schema5 does not silently fall into a schema4 P0 tiled research arm after removing its family.");
        Check(!NativeFreshSavedLevelResearchEligibility.IsSupportedMaterialProfile(document)&&
            !NativeFreshSavedLevelResearchEligibility.IsSupportedMixedProfile(document)&&
            !NativeFreshSavedLevelResearchEligibility.IsSupportedFamilyProfile(document)&&
            !NativeFreshSavedLevelResearchEligibility.IsSupportedTiledProfile(document)&&
            !NativeFreshSavedLevelResearchEligibility.IsSupportedTiledMaterialProfile(document),
            "The new exact arm does not broaden any old predicate.");
        foreach(var item in near)
            Check(!NativeFreshSavedLevelResearchEligibility.IsSupportedLockedChestProfile(item.Value),"Neutral near miss remains authored-only: "+item.Key);
        foreach(var item in near.Prepend(new KeyValuePair<string,NativeFreshLevelDocumentIntent>("exact",document)))
        {
            WithFixture(parent,"family-ready-"+item.Key,item.Value,f=>
            {
                int picked=0;var work=new ResearchWork();
                f.Window.SavedResearchWorkForTesting=work;
                f.Window.ResearchOutputPickerForTesting=_=>{picked++;return Task.FromResult<string?>(null);};
                Check(f.Find<Button>("FreshDocumentResearch").IsEnabled==(item.Key=="exact"),"Actual saved readiness matches complete neutral recipe: "+item.Key);
                var applied=f.Window.CurrentAggregateIntent;
                f.Fire("FreshDocumentResearch");
                if(f.Window.ResearchTaskForTesting is { } task)F.Wait(task);
                Check(picked==(item.Key=="exact"?1:0)&&f.AcquiredOwners==picked&&f.ReleaseAttempts==picked&&work.PrepareCalls==0&&work.PublishCalls==0,
                    "Unsupported direct click refuses before ownership/source preparation; supported picker cancellation does not publish.");
                Check((item.Key=="exact"?f.SourceCalls>0:f.SourceCalls==0)&&ReferenceEquals(applied,f.Window.CurrentAggregateIntent)&&
                    !f.Window.HasUnsavedChanges,"Readiness does not apply, save or replace whole authored intent.");
                if(item.Key!="exact")Check((f.Find<TextBlock>("FreshDocumentStatus").Text??"").Contains("family",StringComparison.OrdinalIgnoreCase),
                    "Unsupported schema5 receives the family capability explanation.");
            });
        }
        NativeFreshLevelDocumentIntent Replace(NativeFreshLockedChestFamilyPlacementIntent row)=>
            document.WithPlacements([document.Placements[0],document.Placements[1],row]);
    }

    private static void DelayedFields(string parent,string stage,string field,bool aba)
    {
        string shortField=field.Replace("FreshFamily", "",StringComparison.Ordinal).ToLowerInvariant();
        WithFixture(parent,$"family-async-{stage}-{shortField}-{aba}",NativeFreshFamilyDocumentSessionCases.Document(),f=>
        {
            f.Select(f.Window.CurrentAggregateIntent!.Placements[2].EditorId);
            var applied=f.Window.CurrentAggregateIntent;var snapshot=Session(f).Snapshot;
            object? before=Value(f,field);long generation=Generation(f);
            var catalogue=new CatalogueWork();var inspection=new FailingMaterialInspectionWork();var research=new ResearchWork();
            var picker=new TaskCompletionSource<string?>();Task? task=null;
            string output=Path.Combine(f.Project.RootPath,"never-created-family-output");
            try
            {
                if(stage=="catalogue")
                {
                    f.Window.MobyCatalogueProviderForTesting=catalogue;f.Fire("FreshNativeMobyBrowse");
                    task=f.Window.MobyCatalogueTaskForTesting;F.Wait(catalogue.Entered.Task);
                }
                else if(stage=="inspection")
                {
                    f.Window.MaterialInspectionWorkForTesting=inspection;task=f.Start();F.Wait(inspection.Entered.Task);
                }
                else
                {
                    f.Window.SavedResearchWorkForTesting=research;
                    f.Window.ResearchOutputPickerForTesting=_=>stage=="picker"?picker.Task:Task.FromResult<string?>(output);
                    f.Fire("FreshDocumentResearch");task=f.Window.ResearchTaskForTesting;
                    if(stage=="prepare")F.Wait(research.Entered.Task);
                }
                Check(task is not null&&!task.IsCompleted&&f.AcquiredOwners==1&&f.ReleaseAttempts==0,
                    "Actual editor owner is held across the "+stage+" await.");
                Change(f,field,before);if(aba)SetValue(f,field,before);
                object? expected=Value(f,field);
                Check(Generation(f)>generation&&(!aba||Equals(expected,before)),
                    "Every family coordinate/mode participates in raw invalidation, including A-B-A: "+field);
                Check(!task!.IsCompleted&&f.ReleaseAttempts==0,"Raw invalidation never abandons outstanding work or releases its owner.");
                if(stage=="inspection")Check(inspection.Token.IsCancellationRequested,"Family fields cancel inspection delivery but still await its worker.");
                catalogue.Released.TrySetResult();inspection.Released.TrySetResult();picker.TrySetResult(output);research.Released.TrySetResult();
                F.Wait(task);
                Check(f.ReleaseAttempts==1&&ReferenceEquals(applied,f.Window.CurrentAggregateIntent)&&ReferenceEquals(snapshot,Session(f).Snapshot)&&
                    Equals(Value(f,field),expected)&&!Directory.Exists(output)&&!File.Exists(output),
                    "Stale completion retains actual applied/saved owners and exact raw spelling, with no output.");
                Check(f.Window.ResearchPublicationForTesting is null&&research.PublishCalls==0&&f.NoImages&&
                    !f.Find<Button>("FreshNativeMobyCancelAdd").IsVisible,"No stale family edit becomes a preview, pending native Add or publication.");
                if(stage=="picker")Check(research.PrepareCalls==0,"Family raw drift in the output picker refuses before source preparation.");
                if(stage is "picker" or "prepare")Check(f.Window.SavedResearchFailureForTesting is InvalidDataException,
                    "The research ticket refuses raw family drift, including equal-final-text ABA.");
                if(stage=="catalogue")Check(f.Window.MobyCatalogueFailureForTesting is not null,"Catalogue cannot adopt a late source after family-field drift.");
            }
            finally
            {
                catalogue.Released.TrySetResult();inspection.Released.TrySetResult();picker.TrySetResult(null);research.Released.TrySetResult();
                f.Fire("FreshMobyCatalogueCancel");if(task is not null)F.Drain(task);
            }
        });
    }

    private static void InspectionSnapshot(string parent)
    {
        WithFixture(parent,"family-ticket-structure",NativeFreshFamilyDocumentSessionCases.Document(),f=>
        {
            f.Select(f.Window.CurrentAggregateIntent!.Placements[2].EditorId);
            foreach(string name in new[]{"MobyCatalogueTicket","MaterialInspectionTicket","SavedResearchTicket"})
            {
                var ticketType=typeof(NativeFreshDocumentWindow).GetNestedType(name,BindingFlags.NonPublic)!;
                Check(ticketType.GetProperties().Any(p=>p.PropertyType.Name=="FamilyFields"),
                    "The actual "+name+" carries a complete structured family snapshot.");
            }
            var work=new FailingMaterialInspectionWork();f.Window.MaterialInspectionWorkForTesting=work;Task? task=null;
            try
            {
                task=f.Start();F.Wait(work.Entered.Task);
                var type=typeof(NativeFreshDocumentWindow).GetNestedType("MaterialInspectionTicket",BindingFlags.NonPublic)!;
                object ticket=Activator.CreateInstance(type,[work.Request!,Session(f),Session(f).Snapshot,Session(f).SelectedEditorId,
                    Invoke(f,"MaterialFieldSignature"),Invoke(f,"NativeMobyFieldSignature"),Invoke(f,"FamilyFieldSignature"),
                    Generation(f),f.SelectedSource.Image,f.SelectedSource.Cue])!;
                long generation=Generation(f);
                // A separate direct guard probe isolates structured snapshot
                // equality from the generation fence. Null is never a successful
                // source result: mismatch must refuse before result consumption.
                f.Set("FreshFamilyReward5Z","invalid|raw\ntext");
                SetField(f,"_materialInspectionGeneration",generation);
                try
                {
                    typeof(NativeFreshDocumentWindow).GetMethod("RequireMaterialInspectionCurrent",Private)!
                        .Invoke(f.Window,[ticket,null,CancellationToken.None]);
                    throw new InvalidDataException("Structured family snapshot was not checked.");
                }
                catch(TargetInvocationException error) when(error.InnerException is InvalidDataException)
                {Check(true,"Complete family snapshot rejects inactive raw fields even when the generation is held equal.");}
                finally{SetField(f,"_materialInspectionGeneration",generation+2);}
            }
            finally{work.Released.TrySetResult();if(task is not null)F.Drain(task);}
        });
    }

    private static void PendingFamily(string parent)
    {
        WithFixture(parent,"family-pending-guards",NativeFreshFamilyDocumentSessionCases.Document(),f=>
        {
            var catalogue=new CatalogueWork();var inspection=new FailingMaterialInspectionWork();var research=new ResearchWork();
            f.Window.MobyCatalogueProviderForTesting=catalogue;f.Window.MaterialInspectionWorkForTesting=inspection;
            f.Window.SavedResearchWorkForTesting=research;
            var applied=f.Window.CurrentAggregateIntent;
            f.Fire("FreshFamilyAdd");
            Check(f.Window.HasUnappliedFields,"Pending family is unapplied authorship, not a placement or source owner.");
            foreach(string name in new[]{"FreshNativeMobyBrowse","FreshMaterialsInspect","FreshDocumentResearch","FreshTilesUpgrade"})
            {
                Check(!f.Find<Button>(name).IsEnabled,"Pending family blocks unrelated operation: "+name);f.Fire(name);
            }
            Check(f.AcquiredOwners==0&&f.SourceCalls==0&&catalogue.Calls==0&&inspection.Calls==0&&research.PrepareCalls==0&&
                ReferenceEquals(applied,f.Window.CurrentAggregateIntent),"Direct commands cannot bypass pending family admission or mutate geometry.");
            f.Fire("FreshFamilyCancelAdd");
        });
    }

    private static void PendingNativeInactiveFamily(string parent)
    {
        WithFixture(parent,"family-native-pending",NativeFreshFamilyDocumentSessionCases.Document(),f=>
        {
            var provider=new CatalogueWork();f.Window.MobyCatalogueProviderForTesting=provider;Task? task=null;
            try
            {
                f.Fire("FreshNativeMobyBrowse");task=f.Window.MobyCatalogueTaskForTesting;F.Wait(provider.Entered.Task);
                provider.Released.TrySetResult();
                WaitUntil(()=>f.Find<ListBox>("FreshMobyCatalogueEntries").ItemsSource is not null);
                var list=f.Find<ListBox>("FreshMobyCatalogueEntries");list.SelectedItem=list.ItemsSource!.Cast<NativeFreshMobyCatalogueItem>().Single();F.Pump();
                f.Fire("FreshMobyCatalogueStage");F.Wait(task!);
                var applied=f.Window.CurrentAggregateIntent;
                foreach(var item in new[]{("FreshNativeMobyX","9152"),("FreshNativeMobyY","9152"),("FreshNativeMobyZ","8704"),("FreshNativeMobyYaw","64")})f.Set(item.Item1,item.Item2);
                f.Set("FreshFamilyReward5Z","do not discard this inactive family draft");f.Fire("FreshDocumentApply");
                Check(ReferenceEquals(applied,f.Window.CurrentAggregateIntent)&&f.Text("FreshFamilyReward5Z")=="do not discard this inactive family draft",
                    "Pending native Apply cannot discard changed inactive family fields.");
                f.Fire("FreshNativeMobyCancelAdd");
            }
            finally{provider.Released.TrySetResult();f.Fire("FreshMobyCatalogueCancel");if(task is not null)F.Drain(task);}
        });
    }

    private static void WithFixture(string parent,string id,NativeFreshLevelDocumentIntent document,Action<F> test)
    {
        // The existing shared fixture owns a separate legacy floor. Preserve
        // that owner and load tiled schema5 through the real aggregate Store.
        var f=new F(parent,id.ToLowerInvariant(),document:document.TiledContent == null ? document : NativeFreshFamilyDocumentSessionCases.Document());
        List<Exception> failures=[];
        try
        {
            if(document.TiledContent != null)
            {
                NativeFreshLevelDocumentStore.Save(NativeFreshLevelDocumentStore.Capture(f.Project),document);
                f.ExpectedDocument=NativeFreshLevelDocumentCodec.SerializeCanonical(document);
                f.Fire("FreshDocumentReopen");
                Check(f.Window.CurrentAggregateIntent?.SchemaVersion==5&&f.Window.CurrentAggregateIntent.TiledContent != null,
                    "The actual saved tiled schema5 aggregate reopens without changing its protected legacy owner.");
            }
            test(f);
        }
        catch(Exception error){failures.Add(error);}
        finally{try{f.Close();}catch(Exception error){failures.Add(error);}try{f.RequireProtected();}catch(Exception error){failures.Add(error);}}
        F.Throw(failures);
    }
    private static NativeFreshLevelDocumentSession Session(F f)=>(NativeFreshLevelDocumentSession)GetField(f,"_aggregateSession")!;
    private static long Generation(F f)=>(long)GetField(f,"_materialInspectionGeneration")!;
    private static object? GetField(F f,string name)=>typeof(NativeFreshDocumentWindow).GetField(name,Private)!.GetValue(f.Window);
    private static void SetField(F f,string name,object value)=>typeof(NativeFreshDocumentWindow).GetField(name,Private)!.SetValue(f.Window,value);
    private static object Invoke(F f,string name)=>typeof(NativeFreshDocumentWindow).GetMethod(name,Private)!.Invoke(f.Window,null)!;
    private static object? Value(F f,string field)=>field.EndsWith("Mode",StringComparison.Ordinal)?f.Find<ComboBox>(field).SelectedItem:f.Find<TextBox>(field).Text;
    private static void SetValue(F f,string field,object? value)
    {if(field.EndsWith("Mode",StringComparison.Ordinal))f.Find<ComboBox>(field).SelectedItem=value;else f.Find<TextBox>(field).Text=(string?)value;F.Pump();}
    private static void Change(F f,string field,object? before)
    {
        object changed=field.EndsWith("Mode",StringComparison.Ordinal)?f.Find<ComboBox>(field).ItemsSource!.Cast<object>().First(v=>!Equals(v,before)):
            "raw|"+(before as string)+"\n0";
        SetValue(f,field,changed);
    }
    private static void Check(bool ok,string message){if(!ok)throw new InvalidDataException(message);checks++;}
    private static void WaitUntil(Func<bool> ready)
    {
        var timer=System.Diagnostics.Stopwatch.StartNew();
        while(!ready()&&timer.Elapsed<TimeSpan.FromSeconds(30)){F.Pump();Thread.Sleep(1);}
        Check(ready(),"Actual catalogue chooser appeared after its released worker; cleanup still drains on failure.");
    }
    private sealed class ResearchWork:INativeFreshSavedLevelResearchUiWork
    {
        internal readonly TaskCompletionSource Entered=new(),Released=new();internal int PrepareCalls,PublishCalls;
        public async Task PrepareAsync(NativeFreshLevelDocumentSnapshot snapshot,string image,string cue,CancellationToken token)
        {PrepareCalls++;Entered.TrySetResult();await Released.Task;}
        public Task<NativeId65FreshDiscResearchPublication> PublishAsync(string output,CancellationToken token)
        {PublishCalls++;return Task.FromException<NativeId65FreshDiscResearchPublication>(new IOException("No native publication exists in this neutral test."));}
    }
    private sealed class CatalogueWork:INativeFreshMobyCatalogueProvider
    {
        internal readonly TaskCompletionSource Entered=new(),Released=new();internal int Calls;
        public async Task<NativeFreshMobyCatalogueListing> CaptureAsync(string image)
        {
            Calls++;Entered.TrySetResult();await Released.Task;
            return new(NativeMobySourceInventory.OriginalImageSha256,[new("neutral-family-grass","artisans","Artisans",10,10,121,null,0x1F5,
                NativeMobyCatalogueEntryKind.PlacedRow,"Neutral Grass","Scenery","Synthetic metadata; no native dependency authority.")]);
        }
    }
}
