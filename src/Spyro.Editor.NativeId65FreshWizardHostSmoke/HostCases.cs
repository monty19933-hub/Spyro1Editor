using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;

internal static class HostCases
{
    private static int checks,refusals;
    private const uint OverlayBase=0x8007AA38;
    private const string OverlayGolden="ad6d952cbf70ab8b1eab53f7637ffd0235271b7b20696956c9406f36f714fa7e";
    private const string InitializerGolden="d7f0e7c9ed86467f0749db689b2bf1f1c4df27587eb357821ef8947d7f70466b";
    private static readonly byte[] SourceInitializer=Convert.FromHexString(
        "0980023ccc9e42240880013cd88522ac0880023ca47242240780013ccc5822ac0880023c54da42240780013c345722ac"+
        "0980023c688f42240780013ce45822ac0880023c207e42240780013cbc5622ac0880023c2caf42240780013ca05722ac"+
        "0880023c68b042240780013ca85822ac0880023cacb042240780013c4c5722ac0880023c4cd042240780013cd85822acb86d010800000000");
    private static readonly (string Id,int At,int Length,string Hash)[] Blocks=[
        ("main",0xF800,0x8B8,"e1de5faa759c53dd30930fcb8c3d0f0aa8f5c297455dabe16677dd4c5030c983"),
        ("spawn26",0x100B8,0x2FC,"a15689742b44c6e5d1e13386cb96e13b2bfc1dcb517b163c2b3d8bc7926c0f70"),
        ("particles",0x103B4,0x3A4,"1479e4257d1189d355a3adc2647788dc2691f7606a8a17d99ce8411f6638726d"),
        ("main-leave",0x10758,0x98,"99abaf134a6d083991456027ba3d26726b9a7b52f53662c8756b5afae1e5f952"),
        ("tick-next-leave",0x107F0,0x98,"8db309546d83384b55c471602abfe13fc12e553320e9fc348e26b9e34f6d02a2"),
        ("tick-delete-leave",0x10888,0x98,"dfcd86473d512e9768a2c7f126225ce8cec234d322aaec266ad6193b1ae72113"),
        ("spawn-leave",0x10920,0x98,"72876f88fb9569e853593890c6b395c101c73c84209220430922a61c27534ebe"),
        ("main26",0x109B8,0x9C,"9c26c8b6818d4df79a9d3eb470aa7ee06402db11cb9b69773b7bce7f93f0e422"),
        ("main11B",0x10A54,0x9C,"fb6ea861474f3b43d41c637666abde7004220318eb4bd61b4b9070223ad6a7e2"),
        ("tick07",0x10AF0,0x9C,"ba132087b0cb1f2306feb5733710e9b323f9c12fa8f61c7aea2559617b724650"),
        ("tick41",0x10B8C,0x9C,"879129c5a11c8ba7d2246c0f0badf4f815ff2de2a48d092109b87223b8e3473e"),
        ("spawn07",0x10C28,0xA0,"70071aa6e7195d5091b23a8fb2720c0e4fb4e417ae952c41b279f784003f8803"),
        ("spawn41",0x10CC8,0xA0,"e31a88180d5e5d2a0ac93a92c03cbec6e32b74013593331b12022238d59fe451"),
        ("effect-call",0x10D68,8,"f48373ac1cccdeb28c8ac2e38dd874c42511abae2ea1ef561b33f6173be54e67"),
        ("dispatch",0x10D70,0x44,"f61a910589ba3dbdf971e588c30ea18c91493b47f7bd76283bd3cf943bb5bc80"),
        ("padding",0x10DB4,0x24C,"c7606d438ecce7eaef403d65064ab7cf28f5eeb137d59e6e4bd5f88f41a126bb")];
    private static readonly (string Id,uint Pc,uint Source)[] Roles=[
        ("main26",0x8008B3F0,0x8007EAD0),("main11B",0x8008B48C,0x80082818),
        ("tick07",0x8008B528,0x80088370),("tick41",0x8008B5C4,0x800888E8),
        ("spawn07",0x8008B660,0x80089934),("spawn41",0x8008B700,0x80089F34)];
    private static NativeId65FreshDocumentIntent A()=>NativeId65FreshDocumentIntent.Create(
        8448,8448,8704,1024,1024,8960,8960,64,new(80,128,112),new(128,96,64),19);
    private static NativeWizardPlacementIntent Placement=>new(8704,8960,8704,9216,8960);

    internal static async Task SourceFree()
    {
        HostedGrassCases.SourceFree(Check);
        var t=typeof(NativeId65FreshWizardResearchHostPlan);
        Check(t.IsSealed&&t.GetConstructors().Length==0&&t.GetProperties().All(p=>p.SetMethod is null),"Closed immutable actual-source result.");
        var api=t.GetMethods(BindingFlags.Public|BindingFlags.Static|BindingFlags.DeclaredOnly);
        Check(api.Length==1&&api[0].Name=="CaptureAsync"&&api[0].GetParameters().Select(p=>p.ParameterType).SequenceEqual(new[]{
            typeof(string),typeof(NativeId65FreshWizardResearchScenePlan),typeof(NativeId65FreshWizardResearchFallbackResourcePlan),typeof(CancellationToken)}),
            "Only original plus actual scene/fallback; no caller code/target/hash/flag certificate.");
        Check(!t.GetMethods(BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly).Any(m=>
            new[]{"Publish","Write","CopyExecutable","CompileBody","CopyStaging"}.Any(n=>m.Name.StartsWith(n,StringComparison.Ordinal))),"No executable/body/staging/publisher expansion.");
        using(var cancel=new CancellationTokenSource())
        {cancel.Cancel();await Reject<OperationCanceledException>(()=>NativeId65FreshWizardResearchHostPlan.CaptureAsync(null!,null!,null!,cancel.Token));}
        await Reject<ArgumentNullException>(()=>NativeId65FreshWizardResearchHostPlan.CaptureAsync("not-opened.bin",null!,null!));
        Check(SourceInitializer.Length==0x98&&Hash(SourceInitializer)=="fe4669229aedbbc8ff698d57a237327af36025aaeda0ff5cc2e1c806fd23e50b","Embedded exact native initializer, not guessed instruction fixture.");
        byte[] original=SourceInitializer.ToArray();
        byte[] result=NativeId65FreshWizardHostLayout.RewriteInitializer(original,0x11000);
        Check(result.Length==0x98&&Hash(result)==InitializerGolden&&original.SequenceEqual(SourceInitializer),"Exact independent initializer golden and source nonmutation.");
        for(int i=0;i<original.Length;i++)
        {byte[] bad=original.ToArray();bad[i]^=1;Reject<InvalidDataException>(()=>NativeId65FreshWizardHostLayout.RewriteInitializer(bad,0x11000));}
        foreach(int length in new[]{0,4,0x94,0x97,0x99,0xA0})
            Reject<InvalidDataException>(()=>NativeId65FreshWizardHostLayout.RewriteInitializer(new byte[length],0x11000));
        foreach(int length in new[]{int.MinValue,-1,0,0xF800,0x10DB4,0x10FFF,0x11001,0x11800,int.MaxValue})
            Reject<InvalidDataException>(()=>NativeId65FreshWizardHostLayout.RewriteInitializer(original,length));
        Reject<InvalidDataException>(()=>NativeId65FreshWizardHostLayout.Compile([],[],[]));
        foreach(var lens in new[]{(0xF7FF,0x10000,0x66000),(0xF800,0xFFFF,0x66000),(0xF800,0x10000,0x65FFF)})
            Reject<InvalidDataException>(()=>NativeId65FreshWizardHostLayout.Compile(new byte[lens.Item1],new byte[lens.Item2],new byte[lens.Item3]));
        Check(Hash(original)=="fe4669229aedbbc8ff698d57a237327af36025aaeda0ff5cc2e1c806fd23e50b","All neutral failures preserve source input.");
        Console.WriteLine($"FRESH_WIZARD_HOST_FREE_OK checks={checks} refusals={refusals} outputsWritten=0");
    }

    internal static async Task Source(string original)
    {
        RequireOriginal(original);
        try
        {
            var c=await NativeId65FreshDocumentCompilation.CaptureAsync(original,A());
            var family=await NativeWizardFamilyAssetPlan.CaptureAsync(original);
            var r=await NativeId65FreshWizardResearchResourcePlan.CaptureAsync(original,c,family);
            var scene=await NativeId65FreshWizardResearchScenePlan.CaptureAsync(original,c,family,r,Placement);
            var fallback=await NativeId65FreshWizardResearchFallbackResourcePlan.CaptureAsync(original,r);
            string oldC=JsonSerializer.Serialize(c),oldScene=JsonSerializer.Serialize(scene),oldR=JsonSerializer.Serialize(r),oldF=JsonSerializer.Serialize(fallback);
            byte[] sceneBytes=scene.CopyScene(),up=r.CopyUploadPayload(),models=r.CopyModelPayload();
            var p=await NativeId65FreshWizardResearchHostPlan.CaptureAsync(original,scene,fallback);
            Check(ReferenceEquals(p.SceneAncestor,scene)&&ReferenceEquals(p.FallbackAncestor,fallback),"Exact two supplied objects retained.");p.RequireAncestry(scene,fallback);
            byte[] overlay=p.CopyOverlay(),initializer=p.CopyInitializerPatch();
            byte[] town=ReadOriginal(original,37,0x118E800,0xF800),wizard=ReadOriginal(original,37,0x2F5B000,0x10000),exe=ReadOriginal(original,53875,0,0x66000);
            Check(Hash(town)=="9bc923cc8d27703537b81b01f51fde351e58aabee87d38a8afc630f1031878a5"&&
                Hash(wizard)=="5d3996111567ffac6eca08d340f2f691824bb7969ba2a0cd95e9346ccfe157e1"&&
                Hash(exe)=="a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998","Independent physical source owners.");
            var neutral=NativeId65FreshWizardHostLayout.Compile(town,wizard,exe);
            Check(!neutral.SourceCertified&&neutral.CopyOverlay().SequenceEqual(overlay)&&
                neutral.SortedParticleSourcePcs.SequenceEqual(new uint[]{0x80088370,0x800888E8,0x800893D0,0x80089934,0x80089F28})&&
                !neutral.SortedParticleSourcePcs.SequenceEqual(neutral.Fragments.Where(x=>x.Package=="particles").Select(x=>x.SourcePc)),
                "Actual existing codec sorting versus explicit closed private reorder; neutral bytes are not authority.");
            Check(overlay.Length==0x11000&&p.OverlayByteLength==overlay.Length&&p.UsedByteLength==0x10DB4&&
                p.CopyAnchor==OverlayBase+overlay.Length&&p.CopyAnchor==0x8008BA38&&Hash(overlay)==OverlayGolden&&p.OverlaySha256==OverlayGolden&&
                initializer.Length==0x98&&Hash(initializer)==InitializerGolden&&p.InitializerSha256==InitializerGolden,"Two complete independent preproduction source-oracle goldens.");
            Check(!neutral.AppendLayout.SourceCertified&&neutral.AppendLayout.CopyRetainedPrefix().SequenceEqual(town)&&
                neutral.AppendLayout.RetainedByteLength==town.Length&&neutral.AppendLayout.UsedByteLength==p.UsedByteLength&&
                neutral.AppendLayout.FinalByteLength==overlay.Length&&neutral.AppendLayout.CopyAnchor==p.CopyAnchor&&
                neutral.AppendLayout.Blocks.Select(b=>(b.Id,b.Owner.Offset,b.Owner.ByteLength)).SequenceEqual(
                    p.AppendedOwners.Select(b=>(b.Id,(long)b.OverlayOffset,(long)b.ByteLength))),
                "Actual Fresh producer consumes shared append ownership while retaining every established source-oracle owner.");
            Check(p.ScenePlanSha256==scene.PlanSha256&&p.FallbackResourcePlanSha256==fallback.PlanSha256&&
                p.CanonicalDocumentSha256==c.CanonicalSha256&&p.PlanSha256.Length==64,"Actual scene/fallback/canonical lineage, no code-hash substitution.");
            Check(p.SourceCertified&&!p.RuntimeVerified&&!p.PublicationApproved&&!p.GlobalAudioCompatibilityProven&&
                !p.CompleteNativeDependencyClosureProven&&!p.CompleteIncomingEdgeClosureProven&&!p.RuntimeStackFitProven&&
                !p.NativeArenaExhaustionSafe&&!p.BodyIssued&&!p.FinalExecutableIssued&&!p.PersistentOwnerIssued&&
                !p.HardwareTransferProven&&!p.InstalledDispatchProven&&p.PendingOwners.Count>=5,"Detached source recipe does not promote unresolved incoming/graph/runtime/body/hardware authority.");
            Check(p.AppendedOwners.Count==Blocks.Length,"Exactly16 appended owners.");int cursor=0xF800;
            for(int i=0;i<Blocks.Length;i++)
            {
                var expected=Blocks[i];var actual=p.AppendedOwners[i];
                Check(actual.Id==expected.Id&&actual.OverlayOffset==expected.At&&actual.OverlayOffset==cursor&&actual.ByteLength==expected.Length&&
                    actual.RuntimePc==OverlayBase+cursor&&actual.Sha256==expected.Hash&&Hash(overlay.AsSpan(cursor,actual.ByteLength))==expected.Hash,"Exact source-oracle whole owner "+expected.Id);
                cursor+=actual.ByteLength;
            }
            Check(cursor==overlay.Length&&!overlay.AsSpan(p.UsedByteLength).ContainsAnyExcept((byte)0),"Complete final zero padding owner, no suffix gap.");
            Check(p.RoleEntries.Select(x=>(x.Id,x.RuntimePc,x.SourcePc)).SequenceEqual(Roles)&&p.RoleEntries.Select(x=>x.RuntimePc).Distinct().Count()==6,"Six distinct final role entries, never shared prototype alias.");
            Check(p.TownPatches.Count==5&&p.TownPatches.Sum(x=>x.ByteLength)==24&&p.TownPatches.Select(x=>x.Offset).Order().SequenceEqual(new[]{0x264,0x34C,0x3A8,0x490,0x30FC}),"Only four native tables plus one two-word class patch.");
            byte[] townInverse=overlay[..0xF800];var changed=new bool[0xF800];
            foreach(var patch in p.TownPatches)
            {
                byte[] before=patch.CopyBeforeBytes(),after=patch.CopyAfterBytes();
                Check(patch.RuntimePc==OverlayBase+patch.Offset&&before.Length==patch.ByteLength&&after.Length==before.Length&&
                    before.AsSpan().SequenceEqual(town.AsSpan(patch.Offset,before.Length))&&after.AsSpan().SequenceEqual(overlay.AsSpan(patch.Offset,after.Length)),"Patch is its actual full source/afterimage field.");
                Check(!changed.AsSpan(patch.Offset,patch.ByteLength).Contains(true),"Original mutation owners disjoint.");Array.Fill(changed,true,patch.Offset,patch.ByteLength);
                before.CopyTo(townInverse,patch.Offset);before[0]^=1;after[0]^=1;
                Check(patch.CopyBeforeBytes()[0]!=before[0]&&patch.CopyAfterBytes()[0]!=after[0],"Patch copies do not alias metadata.");
            }
            Check(townInverse.SequenceEqual(town)&&overlay.AsSpan(0xF494,0x36C).SequenceEqual(town.AsSpan(0xF494)),"Full original Town inverse including originally overwritten tail.");
            for(int i=0;i<town.Length;i++)if(!changed[i])Check(overlay[i]==town[i],"Every untouched Town prefix byte retained.");
            Check(p.Fragments.Count==15&&p.SourceToFinalPc.Count==980&&p.Transfers.Count==168,"Complete captured instruction/transfer budgets.");
            Check(p.Fragments.Where(x=>x.Package=="particles").Select(x=>x.SourcePc).SequenceEqual(new uint[]{0x80088370,0x800888E8,0x80089934,0x80089F28,0x800893D0}),"Explicit closed particle order differs from generic ascending source sort.");
            Check(p.Fragments.Count(x=>x.HasFallthrough)==1&&p.Fragments.Single(x=>x.HasFallthrough).SourcePc==0x800893D0&&
                p.Transfers.Single(x=>x.Kind=="Fallthrough").SourcePc==0x800893EC,"Exactly one mandatory condition fallthrough thunk.");
            foreach(var fragment in p.Fragments)
            {
                int sourceAt=checked((int)(fragment.SourcePc-OverlayBase));byte[] inverse=overlay.AsSpan(fragment.OverlayOffset,fragment.ByteLength).ToArray();
                Check(Hash(wizard.AsSpan(sourceAt,fragment.ByteLength))==fragment.SourceSha256,"Complete original donor fragment pin.");
                foreach(var transfer in p.Transfers.Where(x=>x.SourcePc>=fragment.SourcePc&&x.SourcePc<fragment.SourcePc+fragment.ByteLength))
                {Check(transfer.BeforeWord is not null,"Every actual source instruction has before-word.");Put(inverse,checked((int)(transfer.SourcePc-fragment.SourcePc)),transfer.BeforeWord!.Value);}
                Check(inverse.AsSpan().SequenceEqual(wizard.AsSpan(sourceAt,fragment.ByteLength)),"Full donor inverse, including all literal/particle operands.");
                for(int at=0;at<fragment.ByteLength;at+=4)
                    Check(p.SourceToFinalPc[fragment.SourcePc+(uint)at]==OverlayBase+fragment.OverlayOffset+(uint)at,"Every actual source word mapped exactly once.");
            }
            foreach(var transfer in p.Transfers)
            {
                Check(transfer.FinalPc==OverlayBase+transfer.OverlayOffset&&Word(overlay,transfer.OverlayOffset)==transfer.AfterWord&&
                    Word(overlay,transfer.OverlayOffset+4)==transfer.DelayWord&&Target(transfer.AfterWord,transfer.FinalPc)==transfer.FinalTargetPc,"Re-decoded complete target and original delay/generated NOP.");
                if(transfer.FinalTargetPc>=OverlayBase)
                    Check(p.AppendedOwners.Any(x=>x.Id!="padding"&&transfer.FinalTargetPc>=x.RuntimePc&&transfer.FinalTargetPc<x.RuntimePc+x.ByteLength)||
                        transfer.FinalTargetPc is 0x80087258 or 0x80088F20 or 0x80088F18 or 0x80089E74 or 0x80088F68,"Every linked non-EXE target actually materialized or exact retained Town suffix.");
            }
            var conditional=p.InitializerPatch;var prerequisite=p.RequiredInitializerDispatch;
            Check(conditional.Offset==0x4B098&&conditional.RuntimePc==0x8005A898&&conditional.ByteLength==0x98&&
                conditional.CopyBeforeBytes().SequenceEqual(SourceInitializer)&&conditional.CopyAfterBytes().SequenceEqual(initializer),"Initializer has exact actual resident source owner.");
            Check(prerequisite.ExecutableOffset==0x1CA8&&prerequisite.SourceWord==0x8005B6E0&&prerequisite.RequiredWord==0x8005A898&&
                Word(exe,prerequisite.ExecutableOffset)==prerequisite.SourceWord,"Existing ID65 table substitution is prerequisite metadata, not a second output patch.");
            byte[] prototype=exe.ToArray();initializer.CopyTo(prototype,conditional.Offset);Put(prototype,prerequisite.ExecutableOffset,prerequisite.RequiredWord);
            Check(Hash(prototype)=="f1c8e04eb0c5194ff60f27033ae852a1c49dd8fb9b41790370c1a78d67bf4083","Test-only original-EXE prototype full golden; does not compose LAB/music.");
            conditional.CopyBeforeBytes().CopyTo(prototype,conditional.Offset);Put(prototype,prerequisite.ExecutableOffset,prerequisite.SourceWord);
            Check(prototype.SequenceEqual(exe),"Full original EXE inverse of exact initializer plus existing prerequisite.");
            p.RequireExactOutputs(overlay,initializer);string json=JsonSerializer.Serialize(p);
            var mutations=p.AppendedOwners.SelectMany(x=>new[]{x.OverlayOffset,x.OverlayOffset+x.ByteLength-1})
                .Concat(p.TownPatches.SelectMany(x=>Enumerable.Range(x.Offset,x.ByteLength)))
                .Concat(p.Transfers.SelectMany(x=>new[]{x.OverlayOffset,x.OverlayOffset+4}))
                .Concat(new[]{0,0xF494,0xF7FF,0xF800,0x10DB3,0x10DB4,0x10FFF}).Distinct();
            foreach(int at in mutations)
            {byte[] bad=overlay.ToArray();bad[at]^=1;Reject<InvalidDataException>(()=>p.RequireExactOutputs(bad,initializer));}
            for(int at=0;at<initializer.Length;at++)
            {byte[] bad=initializer.ToArray();bad[at]^=1;Reject<InvalidDataException>(()=>p.RequireExactOutputs(overlay,bad));}
            foreach(var lengths in new[]{(0x10FFF,0x98),(0x11001,0x98),(0x11000,0x97),(0x11000,0x99)})
                Reject<InvalidDataException>(()=>p.RequireExactOutputs(new byte[lengths.Item1],new byte[lengths.Item2]));
            overlay[0]^=1;initializer[0]^=1;
            Check(p.OverlaySha256==Hash(p.CopyOverlay())&&p.InitializerSha256==Hash(p.CopyInitializerPatch())&&JsonSerializer.Serialize(p)==json,"All mutations/failures are atomic and copies are detached.");
            var repeat=await NativeId65FreshWizardResearchHostPlan.CaptureAsync(original,scene,fallback);
            Check(repeat.PlanSha256==p.PlanSha256,"Deterministic actual same input pair.");
            var sameR0Scene=await NativeId65FreshWizardResearchScenePlan.CaptureAsync(original,c,family,r,Placement);
            Check(sameR0Scene.PlanSha256==scene.PlanSha256,"Foreign scene object genuinely byte-identical.");Reject<InvalidDataException>(()=>p.RequireAncestry(sameR0Scene,fallback));
            var altScene=await NativeId65FreshWizardResearchScenePlan.CaptureAsync(original,c,family,r,Placement with{OriginZ=8909});
            var alt=await NativeId65FreshWizardResearchHostPlan.CaptureAsync(original,altScene,fallback);
            Check(alt.OverlaySha256==p.OverlaySha256&&alt.InitializerSha256==p.InitializerSha256&&alt.ScenePlanSha256!=p.ScenePlanSha256&&alt.PlanSha256!=p.PlanSha256,"Valid scene placement is retained; equal code never replaces actual scene identity.");
            var r2=await NativeId65FreshWizardResearchResourcePlan.CaptureAsync(original,c,family);
            var fallback2=await NativeId65FreshWizardResearchFallbackResourcePlan.CaptureAsync(original,r2);
            var scene2=await NativeId65FreshWizardResearchScenePlan.CaptureAsync(original,c,family,r2,Placement);
            Check(r2.PlanSha256==r.PlanSha256&&fallback2.PlanSha256==fallback.PlanSha256&&scene2.PlanSha256==scene.PlanSha256,"Foreign R0 and both descendants genuinely byte-identical.");
            await Reject<InvalidDataException>(()=>NativeId65FreshWizardResearchHostPlan.CaptureAsync("not-opened.bin",scene,fallback2));
            await Reject<InvalidDataException>(()=>NativeId65FreshWizardResearchHostPlan.CaptureAsync("not-opened.bin",scene2,fallback));
            Reject<InvalidDataException>(()=>p.RequireAncestry(scene2,fallback2));
            var own=await NativeId65FreshWizardResearchHostPlan.CaptureAsync(original,scene2,fallback2);own.RequireAncestry(scene2,fallback2);
            Check(own.PlanSha256==p.PlanSha256,"Matching other actual pair can issue its own identical result, without rebinding earlier object.");
            await HostedGrassCases.Source(original,p,repeat,alt,own,town,Check);
            using(var cancel=new CancellationTokenSource())
            {cancel.Cancel();await Reject<OperationCanceledException>(()=>NativeId65FreshWizardResearchHostPlan.CaptureAsync("not-opened.bin",scene,fallback,cancel.Token));}
            await Reject<ArgumentNullException>(()=>NativeId65FreshWizardResearchHostPlan.CaptureAsync("not-opened.bin",scene,null!));
            await Reject<ArgumentException>(()=>NativeId65FreshWizardResearchHostPlan.CaptureAsync("",scene,fallback));
            await Reject<InvalidDataException>(()=>NativeId65FreshWizardResearchHostPlan.CaptureAsync(typeof(HostCases).Assembly.Location,scene,fallback));
            Check(Hash(sceneBytes)=="b7bd222702ff9b5d6ea40c623159f4aa22b3425ef9368059167d1ccab930266b"&&
                Hash(up)=="d74c2571ed857865b2b989af3001117f44e0915f9c6d59927e30a49d90a79402"&&
                Hash(models)=="782c4500b3152f6051b45b8a0a56f6c99c4f703c753948f656318ccfa8618cda"&&
                fallback.UploadSha256=="8c4438fc13eca3b3bd40e9c056094c308fb272aded227577fb812ec9b16d96dc"&&
                fallback.ModelSha256=="84d00dbbce264bc08c61abfe2579fe3d7a7464ba2df6b189834664da10b82c84","All pre-existing primary scene/R0/fallback goldens unchanged.");
            Check(JsonSerializer.Serialize(c)==oldC&&JsonSerializer.Serialize(scene)==oldScene&&JsonSerializer.Serialize(r)==oldR&&
                JsonSerializer.Serialize(fallback)==oldF&&scene.CopyScene().SequenceEqual(sceneBytes),"Every old actual immutable JSON/scene survives successes/refusals unchanged.");
            Console.WriteLine($"overlaySha256={p.OverlaySha256} initializerSha256={p.InitializerSha256} planSha256={p.PlanSha256}");
        }
        finally{RequireOriginal(original);}
        Console.WriteLine($"FRESH_WIZARD_HOST_SOURCE_OK checks={checks} refusals={refusals} outputsWritten=0");
    }
    // Independent bounded original MODE2 payload reader; no editor source issuer
    // or caller digest is manufactured. Full source identity surrounds this run.
    private static byte[] ReadOriginal(string path,int lba,long offset,int length)
    {
        if(lba is not (37 or 53875)||offset<0||length<=0||length>0x66000)throw new InvalidDataException("Fixture read escaped exact owners.");
        using var f=File.OpenRead(path);byte[] b=new byte[length];int cursor=0;
        while(cursor<length)
        {long logical=offset+cursor;int at=(int)(logical%2048),n=Math.Min(length-cursor,2048-at);f.Position=(lba+logical/2048)*2352+24+at;f.ReadExactly(b.AsSpan(cursor,n));cursor+=n;}
        return b;
    }
    private static uint Target(uint w,uint pc)
    {uint op=w>>26;if(op is 2 or 3)return((pc+4)&0xF0000000)|((w&0x3FFFFFF)<<2);if(op is 1 or 4 or 5 or 6 or 7)return unchecked(pc+4+(uint)((short)w*4));throw new InvalidDataException("Expected emitted direct transfer.");}
    private static void RequireOriginal(string path)
    {using var f=File.OpenRead(path);Check(f.Length==661547040&&Convert.ToHexStringLower(SHA256.HashData(f))==
        "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37"&&f.Position==661547040&&f.ReadByte()==-1&&f.Position==661547040&&f.Length==661547040,"Original whole before/finally SHA+actual EOF.");}
    private static uint Word(ReadOnlySpan<byte> b,int at)=>BinaryPrimitives.ReadUInt32LittleEndian(b[at..]);
    private static void Put(Span<byte> b,int at,uint w)=>BinaryPrimitives.WriteUInt32LittleEndian(b[at..],w);
    private static string Hash(ReadOnlySpan<byte> b)=>Convert.ToHexStringLower(SHA256.HashData(b));
    private static void Check(bool ok,string why){if(!ok)throw new InvalidDataException(why);checks++;}
    private static void Reject<T>(Action a)where T:Exception
    {try{a();}catch(T){checks++;refusals++;return;}throw new InvalidDataException("Expected "+typeof(T).Name);}
    private static async Task Reject<T>(Func<Task> a)where T:Exception
    {try{await a();}catch(T){checks++;refusals++;return;}throw new InvalidDataException("Expected "+typeof(T).Name);}
}
