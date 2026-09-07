using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;

internal static class ExecutableCases
{
    private static int checks,refusals;
    private const int InitAt=0x4B098,InitLength=0x98,TargetAt=0x5FC7E;
    internal static void SourceFree()
    {
        byte[] original=[1,2,3,4,5,6,7,8],current=original.ToArray();
        NativeId65FreshWizardExecutableLayout.ApplySequential(current,2,[3,4],[9,10]);
        Check(current.SequenceEqual(new byte[]{1,2,9,10,5,6,7,8}),"Neutral exact equal-size stage only changes its owner.");
        byte[] after=current.ToArray();
        Reject<InvalidDataException>(()=>NativeId65FreshWizardExecutableLayout.ApplySequential(current,2,[3,4],[9,10]));
        Check(current.SequenceEqual(after),"Duplicate preimage failure is atomic.");
        NativeId65FreshWizardExecutableLayout.ApplySequential(current,2,[9,10],[3,4]);
        Check(current.SequenceEqual(original),"Neutral full inverse.");
        foreach(int at in new[]{-1,int.MinValue,7,8,int.MaxValue})
        {Reject<InvalidDataException>(()=>NativeId65FreshWizardExecutableLayout.ApplySequential(current,at,[3,4],[9,10]));Check(current.SequenceEqual(original),"Invalid bounds preserve complete input.");}
        (byte[] Before,byte[] After)[] badStages=[([],[]),([3],[9,10]),([99,4],[9,10])];
        foreach(var pair in badStages)
        {Reject<InvalidDataException>(()=>NativeId65FreshWizardExecutableLayout.ApplySequential(current,2,pair.Before,pair.After));Check(current.SequenceEqual(original),"Invalid preimage/width preserves complete input.");}
        Check(ExecutableGoldens.All.Length==46&&ExecutableGoldens.All.Select(x=>x.Track).Distinct().Count()==46&&
            !ExecutableGoldens.All.Any(x=>x.Track is 30 or 35),"Exact all46 prior/new independent literals, no reserved tracks.");
        Console.WriteLine($"FRESH_WIZARD_EXECUTABLE_FREE_OK checks={checks} refusals={refusals} outputsWritten=0");
    }
    internal static async Task Source(string path)
    {
        Original(path);
        try
        {
            var intent=NativeId65FreshDocumentIntent.Create(8448,8448,8704,1024,1024,8960,8960,64,new(80,128,112),new(128,96,64),19);
            var compilation=await NativeId65FreshDocumentCompilation.CaptureAsync(path,intent);
            var family=await NativeWizardFamilyAssetPlan.CaptureAsync(path);
            var resource=await NativeId65FreshWizardResearchResourcePlan.CaptureAsync(path,compilation,family);
            var scene=await NativeId65FreshWizardResearchScenePlan.CaptureAsync(path,compilation,family,resource,new(8704,8960,8704,9216,8960));
            var fallback=await NativeId65FreshWizardResearchFallbackResourcePlan.CaptureAsync(path,resource);
            var host=await NativeId65FreshWizardResearchHostPlan.CaptureAsync(path,scene,fallback);
            var body=await NativeId65FreshWizardResearchBodyPlan.CaptureAsync(path,host);
            string ancestors=JsonSerializer.Serialize(new{compilation,family,resource,scene,fallback,host,body});
            byte[] source=compilation.FreshResourceAncestor.ResourceChainAncestor.Resources.Surface.CopySourceExecutable();
            byte[] placedScene=scene.CopyScene(),before=host.InitializerPatch.CopyBeforeBytes(),initializer=host.CopyInitializerPatch();
            var p=await NativeId65FreshWizardResearchExecutablePlan.CaptureAsync(path,body);p.RequireAncestry(body);
            byte[] final=p.CopyExecutable();var golden=ExecutableGoldens.All.Single(x=>x.Track==19);
            Check(ReferenceEquals(p.BodyAncestor,body)&&p.BodyPlanSha256==body.PlanSha256&&p.HostPlanSha256==host.PlanSha256&&
                p.CanonicalDocumentSha256==compilation.CanonicalSha256&&p.SelectedBuiltInTrack==intent.MusicTrackId,"Actual Body/Host/intent source lineage, no selected-track default.");
            Check(final.Length==0x66000&&Hash(final)==golden.Final&&p.ExecutableSha256==golden.Final&&
                p.HostLabMusicExecutableSha256==golden.HostLabMusic&&p.PriorFreshExecutableSha256==golden.Prior,"Independent complete three-stage executable hashes.");
            Check(p.SourceCertified&&!p.InstalledDispatchProven&&!p.GlobalTransitionState12Supported&&!p.PersistentOwnerIssued&&
                !p.CardOrHistoryCompatibilityProven&&!p.WholePickupOrDeathProven&&!p.GlobalAudioCompatibilityProven&&!p.PeakRuntimeMemoryProven&&
                !p.CompleteIncomingEdgeClosureProven&&!p.CompleteNativeDependencyClosureProven&&!p.RuntimeVerified&&!p.PublicationApproved,"Detached executable bytes never issue runtime/card/history/publication authority.");
            Check(p.Census.PlacedCount==1&&p.Census.ActorClassId==0x11B&&p.Census.ActorKind==0x7F&&p.Census.RewardClassId==0x56&&
                p.Census.ValuePerActor==10&&p.Census.StaticTreasureTarget==10&&p.Census.TargetSlot==35&&p.Census.TargetFileOffset==TargetAt&&
                p.Census.SceneRowOffset==0xD4&&p.Census.SceneRowSha256==Hash(placedScene.AsSpan(0xD4,0x58)),"Static census comes from actual issued row; it is not a durable owner.");
            Check(final.AsSpan(InitAt,InitLength).SequenceEqual(initializer)&&Half(final,TargetAt)==10&&
                Word(final,0x1CA8)==0x8005A898&&p.SequentialStages.Count(s=>s.FileOffset==0x1CA8)==1,"Existing table requirement consumed once, exact Host initializer and two-byte target.");
            var tail=p.SequentialStages.TakeLast(2).ToArray();
            Check(tail[0].FileOffset==InitAt&&Convert.FromHexString(tail[0].BeforeHex).SequenceEqual(before)&&
                Convert.FromHexString(tail[0].AfterHex).SequenceEqual(initializer)&&tail[1].FileOffset==TargetAt&&
                tail[1].BeforeHex=="0000"&&tail[1].AfterHex=="0A00","Exact sequential final owners/preimages/order; target width remains2.");
            p.RequireExactExecutable(final);Check(p.RevertExact(final).SequenceEqual(source),"Actual Plan complete reverse to original.");
            byte[] replay=source.ToArray();
            foreach(var patch in p.FoldedWindows)NativeId65FreshWizardExecutableLayout.ApplySequential(replay,patch.FileOffset,Convert.FromHexString(patch.BeforeHex),Convert.FromHexString(patch.AfterHex));
            Check(replay.SequenceEqual(final),"Folded original diff replay preserves complete complement.");
            foreach(var g in ExecutableGoldens.All)
            {
                var prior=NativeId65FreshExecutablePlan.Compile(source,g.Track);byte[] old=prior.CopyExecutable();
                var layout=NativeId65FreshWizardExecutableLayout.Compile(source,before,initializer,placedScene,g.Track);byte[] output=layout.CopyExecutable();
                Check(!layout.SourceCertified&&layout.SelectedBuiltInTrack==g.Track&&Hash(old)==g.Prior&&layout.PriorFreshExecutableSha256==g.Prior&&
                    layout.HostLabMusicExecutableSha256==g.HostLabMusic&&Hash(output)==g.Final&&layout.ExecutableSha256==g.Final,"All46 actual source tracks preserve old and both new full goldens.");
                Check(layout.RevertExact(output).SequenceEqual(source)&&prior.RevertExact(old).SequenceEqual(source),"All46 full old/new reverses.");
                Check(output.AsSpan(0,InitAt).SequenceEqual(old.AsSpan(0,InitAt))&&
                    output.AsSpan(InitAt+InitLength,TargetAt-InitAt-InitLength).SequenceEqual(old.AsSpan(InitAt+InitLength,TargetAt-InitAt-InitLength))&&
                    output.AsSpan(TargetAt+2).SequenceEqual(old.AsSpan(TargetAt+2)),"Every old compiler byte outside two new owners preserved.");
                Check(output.AsSpan(0x5FC38,70).SequenceEqual(source.AsSpan(0x5FC38,70))&&output.AsSpan(0x5F85C,0x1BC).SequenceEqual(source.AsSpan(0x5F85C,0x1BC))&&
                    output.AsSpan(0x65D64,4).SequenceEqual("LAB\0"u8)&&output.AsSpan(0x65D68,8).SequenceEqual("JACQUES\0"u8)&&Word(output,0x5F828)==g.Track,"Other35 targets/music-neighbors and owned title are unchanged.");
                byte[] attempted=old.ToArray();Reject<InvalidDataException>(()=>NativeId65FreshWizardExecutableLayout.ApplySequential(attempted,0x1CA8,source.AsSpan(0x1CA8,4),old.AsSpan(0x1CA8,4)));
                Check(attempted.SequenceEqual(old),"Actual source duplicate-table refusal is atomic.");
            }
            foreach(int track in new[]{int.MinValue,-1,30,35,48,int.MaxValue})
                Reject<ArgumentOutOfRangeException>(()=>NativeId65FreshWizardExecutableLayout.Compile(source,before,initializer,placedScene,track));
            foreach(int length in new[]{0,0x65FFF,0x66001})
                Reject<InvalidDataException>(()=>NativeId65FreshWizardExecutableLayout.Compile(new byte[length],before,initializer,placedScene,19));
            foreach(int at in new[]{0,0x1CA8,InitAt,TargetAt,0x65FFF})
            {byte[] changed=source.ToArray();changed[at]^=1;Reject<InvalidDataException>(()=>NativeId65FreshWizardExecutableLayout.Compile(changed,before,initializer,placedScene,19));}
            foreach(int i in Enumerable.Range(0,InitLength))
            {
                byte[] badBefore=before.ToArray();badBefore[i]^=1;Reject<InvalidDataException>(()=>NativeId65FreshWizardExecutableLayout.Compile(source,badBefore,initializer,placedScene,19));
                byte[] badAfter=initializer.ToArray();badAfter[i]^=1;Reject<InvalidDataException>(()=>NativeId65FreshWizardExecutableLayout.Compile(source,before,badAfter,placedScene,19));
            }
            (int Before,int After)[] badLengths=[(0,InitLength),(InitLength-1,InitLength),(InitLength,InitLength+1)];
            foreach(var lengths in badLengths)
                Reject<InvalidDataException>(()=>NativeId65FreshWizardExecutableLayout.Compile(source,new byte[lengths.Before],new byte[lengths.After],placedScene,19));
            foreach(int at in new[]{0xD0,0xD4+0x36,0xD4+0x3A,0xD4+0x53,0x12C,0xFD8,0xFDC})
            {byte[] bad=placedScene.ToArray();bad[at]^=1;Reject<InvalidDataException>(()=>NativeId65FreshWizardExecutableLayout.Compile(source,before,initializer,bad,19));}
            foreach(int length in new[]{0,0x2FFF,0x3001})
                Reject<InvalidDataException>(()=>NativeId65FreshWizardExecutableLayout.Compile(source,before,initializer,new byte[length],19));
            foreach(int at in Enumerable.Range(InitAt,InitLength).Concat(new[]{0,0x1CA8,InitAt-1,InitAt+InitLength,TargetAt-1,TargetAt,TargetAt+1,TargetAt+2,0x65FFF}))
            {byte[] bad=final.ToArray();bad[at]^=1;Reject<InvalidDataException>(()=>p.RequireExactExecutable(bad));Reject<InvalidDataException>(()=>p.RevertExact(bad));}
            foreach(int length in new[]{0,0x65FFF,0x66001})Reject<InvalidDataException>(()=>p.RequireExactExecutable(new byte[length]));
            string saved=JsonSerializer.Serialize(p);final[InitAt]^=1;
            Check(Hash(p.CopyExecutable())==golden.Final&&JsonSerializer.Serialize(p)==saved,"Returned buffer edits and every refusal leave accepted Plan unchanged.");
            var repeat=await NativeId65FreshWizardResearchExecutablePlan.CaptureAsync(path,body);Check(repeat.PlanSha256==p.PlanSha256,"Same Body repeated capture is deterministic.");
            var equalBody=await NativeId65FreshWizardResearchBodyPlan.CaptureAsync(path,host);
            Check(equalBody.PlanSha256==body.PlanSha256&&!ReferenceEquals(equalBody,body),"Foreign Body is genuinely equal content.");
            Reject<InvalidDataException>(()=>p.RequireAncestry(equalBody));
            var alternateScene=await NativeId65FreshWizardResearchScenePlan.CaptureAsync(path,compilation,family,resource,scene.Placement with{OriginZ=8909});
            var alternateHost=await NativeId65FreshWizardResearchHostPlan.CaptureAsync(path,alternateScene,fallback);
            var alternateBody=await NativeId65FreshWizardResearchBodyPlan.CaptureAsync(path,alternateHost);
            var alternate=await NativeId65FreshWizardResearchExecutablePlan.CaptureAsync(path,alternateBody);
            Check(ReferenceEquals(alternate.BodyAncestor,alternateBody)&&alternate.CopyExecutable().SequenceEqual(p.CopyExecutable())&&
                alternate.PlanSha256!=p.PlanSha256&&alternate.Census.SceneRowSha256!=p.Census.SceneRowSha256&&
                alternate.Census.SceneRowSha256==Hash(alternateScene.CopyScene().AsSpan(0xD4,0x58)),"Equal executable bytes never collapse alternate actual scene/body ancestry.");
            Reject<InvalidDataException>(()=>p.RequireAncestry(alternateBody));
            using(var cancel=new CancellationTokenSource())
            {cancel.Cancel();await Reject<OperationCanceledException>(()=>NativeId65FreshWizardResearchExecutablePlan.CaptureAsync("must-not-open.bin",body,cancel.Token));}
            await Reject<ArgumentException>(()=>NativeId65FreshWizardResearchExecutablePlan.CaptureAsync("",body));
            await Reject<InvalidDataException>(()=>NativeId65FreshWizardResearchExecutablePlan.CaptureAsync(typeof(ExecutableCases).Assembly.Location,body));
            Check(JsonSerializer.Serialize(new{compilation,family,resource,scene,fallback,host,body})==ancestors,"Every actual ancestor and old compiler output stays unchanged.");
            Console.WriteLine($"executableSha256={p.ExecutableSha256} preTargetSha256={p.HostLabMusicExecutableSha256} planSha256={p.PlanSha256}");
        }
        finally{Original(path);}
        Console.WriteLine($"FRESH_WIZARD_EXECUTABLE_SOURCE_OK checks={checks} refusals={refusals} outputsWritten=0");
    }
    private static void Original(string path)
    {using var f=File.OpenRead(path);Check(f.Length==661547040&&Convert.ToHexStringLower(SHA256.HashData(f))=="fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37"&&f.Position==661547040&&f.ReadByte()==-1&&f.Position==661547040&&f.Length==661547040,"Original complete SHA/actual EOF before/finally.");}
    private static uint Word(ReadOnlySpan<byte> b,int at)=>BinaryPrimitives.ReadUInt32LittleEndian(b[at..]);
    private static ushort Half(ReadOnlySpan<byte> b,int at)=>BinaryPrimitives.ReadUInt16LittleEndian(b[at..]);
    private static string Hash(ReadOnlySpan<byte> b)=>Convert.ToHexStringLower(SHA256.HashData(b));
    private static void Check(bool ok,string why){if(!ok)throw new InvalidDataException(why);checks++;}
    private static void Reject<T>(Action action)where T:Exception
    {try{action();}catch(T){checks++;refusals++;return;}throw new InvalidDataException("Expected "+typeof(T).Name);}
    private static async Task Reject<T>(Func<Task> action)where T:Exception
    {try{await action();}catch(T){checks++;refusals++;return;}throw new InvalidDataException("Expected "+typeof(T).Name);}
}
