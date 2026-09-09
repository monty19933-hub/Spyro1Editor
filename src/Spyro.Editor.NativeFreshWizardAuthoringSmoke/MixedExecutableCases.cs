using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;

// PRIVATE tests first; no compilation, source execution or output is claimed.
internal static class MixedExecutableCases
{
    private const string MaterialRequired="Material-authored executable requires its actual mixed Body.";
    private const string DiscRefusal="Material-authored Wizard executable disc composition is not enabled.";
    private const string Original="a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998";
    private const string Prior="3473dc92e087c87858434125d5fb1ec879450682e3be64c508de6805029ce7a2";
    private const string HostLab="eaf75497957443252b0403a5335c048e5104c60ab75baa3d8a6409de79a69eff";
    private const string Final="a0eb46c2735355c05d66cc33492ed70c6c2174e77e8e8eb6c2ca8fae0f3fc8dc";
    private const string Initializer="d7f0e7c9ed86467f0749db689b2bf1f1c4df27587eb357821ef8947d7f70466b";
    private const int InitAt=0x4B098,InitLength=0x98,TargetAt=0x5FC7E;

    internal static async Task Free(Action<bool,string> check)
    {
        Type type=typeof(NativeId65FreshWizardResearchExecutablePlan);
        var method=type.GetMethod("CaptureMaterialAuthoredAsync",BindingFlags.Static|BindingFlags.NonPublic);
        check(method is {IsAssembly:true}&&method.ReturnType==typeof(Task<NativeId65FreshWizardResearchExecutablePlan>)&&
            method.GetParameters().Select(p=>p.ParameterType).SequenceEqual(
                new[]{typeof(string),typeof(NativeId65FreshWizardResearchBodyPlan),typeof(CancellationToken)})&&
            method.GetParameters()[2].HasDefaultValue,"One internal exact-Body factory with default cancellation token.");
        var constructors=type.GetConstructors(BindingFlags.NonPublic|BindingFlags.Instance);
        check(type.IsSealed&&type.GetConstructors(BindingFlags.Public|BindingFlags.Instance).Length==0&&
            constructors.Length==1&&constructors[0].IsPrivate,"Public sealed result still has only private construction.");
        using var cancelled=new CancellationTokenSource();cancelled.Cancel();
        // Outside Refuse: both errors must be delivered through the returned Task.
        Task cancelledTask=NativeId65FreshWizardResearchExecutablePlan.CaptureMaterialAuthoredAsync(
            "must-not-open",null!,cancelled.Token);
        await Refuse<OperationCanceledException>(()=>cancelledTask,check);
        Task nullTask=NativeId65FreshWizardResearchExecutablePlan.CaptureMaterialAuthoredAsync("must-not-open",null!);
        await Refuse<ArgumentNullException>(()=>nullTask,check);
        await Refuse<ArgumentNullException>(()=>{
            NativeId65FreshWizardResearchExecutableSource.RequireMaterialBody(null!);return Task.CompletedTask;
        },check);
    }

    // Third approved hook reuses the normal actual Body before its existing old EXE capture.
    internal static async Task LegacyBodyRefusal(NativeId65FreshWizardResearchBodyPlan body,Action<bool,string> check)
    {
        check(body.SourceCertified&&!body.IsMaterialAuthored&&body.MixedResourceAncestor is null,
            "This wrong-arm control uses the genuine existing untextured Body.");
        await Refuse<InvalidDataException>(()=>NativeId65FreshWizardResearchExecutablePlan.CaptureMaterialAuthoredAsync(
            "must-not-open",body),check,MaterialRequired);
        await Refuse<InvalidDataException>(()=>{
            NativeId65FreshWizardResearchExecutableSource.RequireMaterialBody(body);return Task.CompletedTask;
        },check,MaterialRequired);
    }

    internal static async Task Source(string bin,IReadOnlyList<NativeId65FreshWizardResearchBodyPlan> bodies,
        NativeId65FreshWizardResearchBodyPlan differentMixedBody,Action<bool,string> check)
    {
        check(bodies.Count==3&&bodies.All(b=>b.IsMaterialAuthored&&b.MixedResourceAncestor is not null)&&
            bodies.Select(b=>(b.MixedResourceAncestor!.MaterialSources[0].SourceWadEntry,
                b.MixedResourceAncestor!.MaterialSources[1].SourceWadEntry)).SequenceEqual(new[]{(14,70),(70,14),(14,14)}),
            "Only the three already-issued mixed Bodies enter this EXE-only stage.");
        var plans=new List<NativeId65FreshWizardResearchExecutablePlan>();
        string[] bodyIdentities=bodies.Select(b=>b.PlanSha256).ToArray();
        foreach(var body in bodies)
        {
            var mixed=body.MixedResourceAncestor!;var host=body.HostAncestor;var scene=host.SceneAncestor;
            byte[] original=scene.CompilationAncestor.FreshResourceAncestor.ResourceChainAncestor.Resources.Surface.CopySourceExecutable();
            byte[] placed=scene.CopyScene(),before=host.InitializerPatch.CopyBeforeBytes(),initializer=host.CopyInitializerPatch();
            check(original.Length==0x66000&&Hash(original)==Original&&Hash(initializer)==Initializer,
                "Actual retained source and complete Host initializer agree with fixed pre-existing literals.");
            var plan=await NativeId65FreshWizardResearchExecutablePlan.CaptureMaterialAuthoredAsync(bin,body);
            plans.Add(plan);plan.RequireAncestry(body);
            check(plan.IsMaterialAuthored&&ReferenceEquals(plan.BodyAncestor,body)&&ReferenceEquals(plan.MixedResourceAncestor,mixed)&&
                ReferenceEquals(plan.MixedResourceAncestor!.DocumentAncestor,mixed.DocumentAncestor)&&
                plan.BodyPlanSha256==body.PlanSha256&&plan.HostPlanSha256==host.PlanSha256&&
                plan.CanonicalDocumentSha256==body.CanonicalDocumentSha256&&plan.SelectedBuiltInTrack==19,
                "Actual mixed Body and full aggregate chain are retained; existing canonical field remains BASE.");
            byte[] final=plan.CopyExecutable();
            check(final.Length==0x66000&&Hash(final)==Final&&plan.ExecutableSha256==Final&&
                plan.PriorFreshExecutableSha256==Prior&&plan.HostLabMusicExecutableSha256==HostLab,
                "Three complete original-only track19 hashes remain unchanged, not generated expectations.");
            var census=plan.Census;
            check(census.PlacedCount==1&&census.ActorClassId==0x11B&&census.ActorKind==0x7F&&census.RewardClassId==0x56&&
                census.ValuePerActor==10&&census.StaticTreasureTarget==10&&census.TargetSlot==35&&
                census.TargetFileOffset==TargetAt&&census.SceneRowOffset==0xD4&&
                census.SceneRowSha256==Hash(placed.AsSpan(0xD4,0x58)),
                "Census scalar literals bind the actual retained row, not dynamic capacity or durable identity.");
            check(final.AsSpan(InitAt,InitLength).SequenceEqual(initializer)&&
                Half(final,TargetAt)==10&&Word(final,0x1CA8)==0x8005A898&&Word(final,0x5F828)==19&&
                final.AsSpan(0x65D64,4).SequenceEqual("LAB\0"u8)&&final.AsSpan(0x65D68,8).SequenceEqual("JACQUES\0"u8),
                "Exact initializer, table, track19, target and title owners remain the existing executable recipe.");
            var prior=NativeId65FreshExecutablePlan.Compile(original,19);byte[] priorBytes=prior.CopyExecutable();
            check(Hash(priorBytes)==Prior&&plan.SequentialStages.Count==prior.SequentialStages.Count+2&&
                plan.SequentialStages.Take(prior.SequentialStages.Count).SequenceEqual(prior.SequentialStages)&&
                plan.SequentialStages.Count(s=>s.FileOffset==0x1CA8)==1,
                "Existing bootstrap/music/LAB stages are retained exactly; table ownership is not applied twice.");
            var tail=plan.SequentialStages.TakeLast(2).ToArray();
            check(tail[0].Name=="actual-Host-conditional-resident-initializer"&&tail[0].FileOffset==InitAt&&
                Convert.FromHexString(tail[0].BeforeHex).SequenceEqual(before)&&
                Convert.FromHexString(tail[0].AfterHex).SequenceEqual(initializer)&&
                tail[1].Name=="static-one-Wizard-slot35-treasure-target"&&tail[1].FileOffset==TargetAt&&
                tail[1].BeforeHex=="0000"&&tail[1].AfterHex=="0A00",
                "Two final owned stages preserve exact source preimages, width and ordering.");
            check(final.AsSpan(0,InitAt).SequenceEqual(priorBytes.AsSpan(0,InitAt))&&
                final.AsSpan(InitAt+InitLength,TargetAt-InitAt-InitLength).SequenceEqual(priorBytes.AsSpan(InitAt+InitLength,TargetAt-InitAt-InitLength))&&
                final.AsSpan(TargetAt+2).SequenceEqual(priorBytes.AsSpan(TargetAt+2))&&
                final.AsSpan(0x5FC38,70).SequenceEqual(original.AsSpan(0x5FC38,70)),
                "Complete prior complement and other35 target owners remain unchanged.");
            plan.RequireExactExecutable(final);
            check(plan.RevertExact(final).SequenceEqual(original),"Complete issued EXE reverse restores its actual original.");
            var neutral=NativeId65FreshWizardExecutableLayout.Compile(original,before,initializer,placed,19);
            check(!neutral.SourceCertified&&neutral.CopyExecutable().SequenceEqual(final)&&
                neutral.SequentialStages.SequenceEqual(plan.SequentialStages)&&
                neutral.FoldedWindows.SequenceEqual(plan.FoldedWindows)&&neutral.RevertExact(final).SequenceEqual(original),
                "Unchanged neutral layout round trip agrees with issued bytes; this is not a second independent oracle.");
            byte[] replay=original.ToArray();
            foreach(var window in plan.FoldedWindows)
                NativeId65FreshWizardExecutableLayout.ApplySequential(replay,window.FileOffset,
                    Convert.FromHexString(window.BeforeHex),Convert.FromHexString(window.AfterHex));
            check(replay.SequenceEqual(final),"Complete folded-stage replay retains the original complement.");
            check(plan.SourceCertified&&!plan.InstalledDispatchProven&&!plan.GlobalTransitionState12Supported&&
                !plan.PersistentOwnerIssued&&!plan.CardOrHistoryCompatibilityProven&&!plan.WholePickupOrDeathProven&&
                !plan.GlobalAudioCompatibilityProven&&!plan.PeakRuntimeMemoryProven&&!plan.CompleteIncomingEdgeClosureProven&&
                !plan.CompleteNativeDependencyClosureProven&&!plan.RuntimeVerified&&!plan.PublicationApproved&&
                !body.FinalExecutableIssued&&!mixed.BodyIssued,
                "A detached descendant does not mutate ancestor flags or claim Disc/publication/runtime authority.");
            await Refuse<InvalidDataException>(()=>{
                NativeId65FreshWizardResearchDiscSource.RequireExecutable(plan);return Task.CompletedTask;
            },check,DiscRefusal);
            await Refuse<InvalidDataException>(()=>NativeId65FreshWizardResearchDiscPlan.CaptureAsync("must-not-open",plan),check,DiscRefusal);
            foreach(int at in new[]{0,0x1CA8,InitAt,InitAt+InitLength-1,TargetAt,TargetAt+1,0x65FFF})
            {
                byte[] changed=final.ToArray();changed[at]^=1;
                await Refuse<InvalidDataException>(()=>{plan.RequireExactExecutable(changed);return Task.CompletedTask;},check);
                await Refuse<InvalidDataException>(()=>{_=plan.RevertExact(changed);return Task.CompletedTask;},check);
            }
            foreach(int length in new[]{0,0x65FFF,0x66001})
                await Refuse<InvalidDataException>(()=>{plan.RequireExactExecutable(new byte[length]);return Task.CompletedTask;},check);
            final[0]^=1;
            check(Hash(plan.CopyExecutable())==Final,"Returned complete executable bytes are detached.");
            Console.WriteLine($"SCHEMA2_MIXED_EXECUTABLE_SOURCE pair={mixed.MaterialSources[0].SourceWadEntry}/{mixed.MaterialSources[1].SourceWadEntry} exe={plan.ExecutableSha256} oldDiscRefused=true outputWritten=false");
        }
        check(plans.Select(p=>p.PlanSha256).Distinct().Count()==3&&
            plans.All(p=>p.ExecutableSha256==Final),"Equal EXE bytes do not collapse three different actual material/Body chains.");
        await Refuse<InvalidDataException>(()=>{plans[0].RequireAncestry(bodies[1]);return Task.CompletedTask;},check);
        check(!ReferenceEquals(differentMixedBody,bodies[0])&&differentMixedBody.PlanSha256==bodies[0].PlanSha256&&
            !ReferenceEquals(differentMixedBody.MixedResourceAncestor,bodies[0].MixedResourceAncestor),
            "Existing fourth Body is a genuine equal reissue with a different mixed instance.");
        await Refuse<InvalidDataException>(()=>{plans[0].RequireAncestry(differentMixedBody);return Task.CompletedTask;},check);
        // Root-authorized one additional Body capture: no new Mixed/source/Scene/Host.
        var equalBody=await NativeId65FreshWizardResearchBodyPlan.CaptureMaterialAuthoredAsync(bin,bodies[0].MixedResourceAncestor!);
        check(!ReferenceEquals(equalBody,bodies[0])&&equalBody.PlanSha256==bodies[0].PlanSha256&&
            equalBody.LevelDataSha256==bodies[0].LevelDataSha256&&ReferenceEquals(equalBody.MixedResourceAncestor,bodies[0].MixedResourceAncestor)&&
            ReferenceEquals(equalBody.HostAncestor,bodies[0].HostAncestor),
            "Exact Body-instance control holds the same actual Mixed/Host/document/both source references.");
        await Refuse<InvalidDataException>(()=>{plans[0].RequireAncestry(equalBody);return Task.CompletedTask;},check);
        var own=await NativeId65FreshWizardResearchExecutablePlan.CaptureMaterialAuthoredAsync(bin,equalBody);
        own.RequireAncestry(equalBody);
        check(own.PlanSha256==plans[0].PlanSha256&&own.ExecutableSha256==Final&&ReferenceEquals(own.BodyAncestor,equalBody),
            "Equal actual Body can issue its own deterministic EXE, not replace the first retained Body.");
        await Refuse<InvalidDataException>(()=>{own.RequireAncestry(bodies[0]);return Task.CompletedTask;},check);
        await TinyFailure(path=>NativeId65FreshWizardResearchExecutablePlan.CaptureMaterialAuthoredAsync(path,bodies[0]),check);
        check(bodies.Select(b=>b.PlanSha256).SequenceEqual(bodyIdentities),
            "New captures, copy mutations and refusals leave the actual incoming Bodies unchanged.");
    }

    private static async Task TinyFailure(Func<string,Task> capture,Action<bool,string> check)
    {
        var directory=Directory.CreateTempSubdirectory("spyro-schema2-exe-refusal-");
        string path=Path.Combine(directory.FullName,"tiny.bin");
        Console.WriteLine($"SCHEMA2_EXE_REFUSAL_FIXTURE_RETAINED path={path}");
        using(var created=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None))created.Write([17,29]);
        Exception? actual=null;List<Exception> failures=[];
        try{await capture(path);}catch(Exception error){actual=error;}
        try
        {
            check(actual is IOException{InnerException:AggregateException},"Actual initial source refusal retains its error tree.");
            var leaves=((AggregateException)actual!.InnerException!).InnerExceptions;
            check(leaves.Count==3&&leaves.All(e=>e is InvalidDataException&&
                e.Message.StartsWith("Original snapshot length/initial position",StringComparison.Ordinal))&&
                !ReferenceEquals(leaves[0],leaves[1])&&!ReferenceEquals(leaves[0],leaves[2])&&!ReferenceEquals(leaves[1],leaves[2]),
                "Actual primary, None held and None named final failures are pairwise distinct.");
        }
        catch(Exception error){failures.Add(error);}
        FileStream? reopened=null;
        void Attempt(Action action){try{action();}catch(Exception error){failures.Add(error);}}
        try
        {
            reopened=new FileStream(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None);
            Attempt(()=>check(reopened.Length==2,"Exclusive reopened source retains its initial length."));
            byte[] bytes=new byte[2];bool read=false;
            Attempt(()=>{reopened.ReadExactly(bytes);read=true;});
            if(read)Attempt(()=>check(bytes.SequenceEqual(new byte[]{17,29}),"Retained two source bytes unchanged."));
            Attempt(()=>check(reopened.Position==2,"Actual two-byte read reaches its exact end."));
            Attempt(()=>check(reopened.ReadByte()==-1,"Actual exclusive reopened source has true EOF."));
            Attempt(()=>check(reopened.Position==2,"Actual EOF does not advance source position."));
            Attempt(()=>check(reopened.Length==2,"Exclusive reopened source retains its final length."));
        }
        catch(Exception error){failures.Add(error);}
        finally{if(reopened is not null)try{reopened.Dispose();}catch(Exception error){failures.Add(error);}}
        if(failures.Count!=0)
            throw new AggregateException("Tiny source controls failed; assertion/action/close first, actual source tree once.",
                failures.Concat(actual is null?Array.Empty<Exception>():new[]{actual}));
        // No fixture/evidence deletion, even on a passing expected refusal.
    }

    private static async Task Refuse<T>(Func<Task> action,Action<bool,string> check,string? prefix=null) where T:Exception
    {
        try{await action();}
        catch(T error)
        {
            try{check(prefix==null||error.Message.StartsWith(prefix,StringComparison.Ordinal),"Expected exact refusal "+typeof(T).Name);}
            catch(Exception assertion){throw new AggregateException("Refusal assertion failed; actual error retained once.",assertion,error);}
            return;
        }
        throw new Exception("Expected "+typeof(T).Name);
    }
    private static uint Word(ReadOnlySpan<byte> bytes,int at)=>BinaryPrimitives.ReadUInt32LittleEndian(bytes[at..]);
    private static ushort Half(ReadOnlySpan<byte> bytes,int at)=>BinaryPrimitives.ReadUInt16LittleEndian(bytes[at..]);
    private static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
}
