using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;

internal static class FamilyExecutableDiscCases
{
    private const string ExeHash="a0eb46c2735355c05d66cc33492ed70c6c2174e77e8e8eb6c2ca8fae0f3fc8dc";
    private const string BodyHash="7812d34d4253f38770dd85d49b3e24d72c7a6aa4896e667ef87562191ae86262";
    internal static async Task Free(Action<bool,string> check)
    {
        foreach(var type in new[]{typeof(NativeId65FreshWizardResearchExecutablePlan),typeof(NativeId65FreshWizardResearchDiscPlan)})
        {
            var method=type.GetMethod("CaptureFamilyComposedAsync",BindingFlags.Static|BindingFlags.NonPublic);
            check(method is {IsAssembly:true}&&method.GetParameters()[2].HasDefaultValue&&
                type.GetConstructors().Length==0,"New family arm stays internal with closed result construction and cancellation.");
        }
        using var cancelled=new CancellationTokenSource();cancelled.Cancel();
        Task exe=NativeId65FreshWizardResearchExecutablePlan.CaptureFamilyComposedAsync("must-not-open",null!,cancelled.Token);
        Task disc=NativeId65FreshWizardResearchDiscPlan.CaptureFamilyComposedAsync("must-not-open",null!,cancelled.Token);
        await Refuse<OperationCanceledException>(()=>exe,check);await Refuse<OperationCanceledException>(()=>disc,check);
        await Refuse<ArgumentNullException>(()=>NativeId65FreshWizardResearchExecutablePlan.CaptureFamilyComposedAsync("must-not-open",null!),check);
        await Refuse<ArgumentNullException>(()=>NativeId65FreshWizardResearchDiscPlan.CaptureFamilyComposedAsync("must-not-open",null!),check);
        Reject<ArgumentNullException>(()=>NativeId65FreshWizardResearchExecutableSource.RequireFamilyComposition(null!),check);
        Reject<ArgumentNullException>(()=>NativeId65FreshWizardResearchDiscSource.RequireFamilyExecutable(null!),check);
        var floor=NativeId65FreshDocumentIntent.Create(8448,8448,8704,2048,2048,8960,8960,64,new(80,128,112),new(128,96,64),19);
        var document=NativeFreshLevelDocumentIntent.Create(floor,[NativeFreshWizardRouteIntent.Create(
            "11111111111111111111111111111111",9984,9984,8704,9472,9984)]);
        foreach(var pair in new[]{(14,70),(70,14),(14,14)})
        {
            var shaped=document.WithFloorMaterials(NativeFreshFloorMaterialsIntent.Create(
                NativeFreshFloorMaterialIntent.RetailTerrain("spyro1-usa-retail-mode2-2352-v1",pair.Item1,1),
                NativeFreshFloorMaterialIntent.RetailTerrain("spyro1-usa-retail-mode2-2352-v1",pair.Item2,1)));
            _=NativeFreshWizardAuthoringShape.RequireMaterialDocumentShape(shaped);
            if(pair==(14,70))NativeId65FreshWizardResearchExecutableSource.RequireFamilyMaterialDocumentShape(shaped);
            else Reject<InvalidDataException>(()=>NativeId65FreshWizardResearchExecutableSource.RequireFamilyMaterialDocumentShape(shaped),check);
            check(true,"Old shape retains all three pairs; family preflight admits ordered14/70 only, without source I/O.");
        }
    }

    internal static async Task Source(string bin,NativeFreshMobyFamilyCompositionPlan composition,Action<bool,string> check)
    {
        var body=composition.BaseBodyAncestor;var host=body.HostAncestor;
        var grass=composition.Placements[1].PassiveAsset!;
        string originalState=JsonSerializer.Serialize(new{body,host,grass});
        byte[] scene=composition.CopyScene(),original=host.SceneAncestor.CompilationAncestor.FreshResourceAncestor.ResourceChainAncestor.Resources.Surface.CopySourceExecutable();
        check(composition.LevelDataSha256==BodyHash&&body.IsMaterialAuthored,"Exact source-derived mixed14/70 P2 Body enters this integration test.");
        NativeId65FreshWizardResearchExecutableSource.RequireFamilyComposition(composition);
        foreach(var refused in new[]{NativeFreshMobyFamilyCompositionPlan.Compose(body,[composition.Placements[0]]),
            NativeFreshMobyFamilyCompositionPlan.Compose(body,[composition.Placements[0],composition.Placements[1],
                NativeFreshMobyFamilyPlacement.Passive("22222222222222222222222222222222",grass,composition.Placements[1].Dispatch!,9152,9408,8704,64)]),
            NativeFreshMobyFamilyCompositionPlan.Compose(body,[composition.Placements[0],
                NativeFreshMobyFamilyPlacement.Passive(composition.Placements[1].EditorId,grass,composition.Placements[1].Dispatch!,9153,9152,8704,64)]),
            NativeFreshMobyFamilyCompositionPlan.Compose(body,[composition.Placements[0],
                NativeFreshMobyFamilyPlacement.Passive(composition.Placements[1].EditorId,grass,composition.Placements[1].Dispatch!,9152,9152,8704,65)])})
            await Refuse<InvalidDataException>(()=>NativeId65FreshWizardResearchExecutablePlan.CaptureFamilyComposedAsync("must-not-open",refused),check);
        var exe=await NativeId65FreshWizardResearchExecutablePlan.CaptureFamilyComposedAsync(bin,composition);
        exe.RequireFamilyAncestry(composition);
        check(ReferenceEquals(exe.FamilyCompositionAncestor,composition)&&ReferenceEquals(exe.BodyAncestor,body)&&
            exe.BodyPlanSha256==composition.PlanSha256&&exe.ExecutableSha256==ExeHash&&exe.CopyExecutable().AsSpan().SequenceEqual(
                NativeId65FreshWizardExecutableLayout.Compile(original,host.InitializerPatch.CopyBeforeBytes(),host.CopyInitializerPatch(),host.SceneAncestor.CopyScene(),19).CopyExecutable()),
            "Final family Body identity is separate from base Body; existing initializer/LAB/music/full EXE bytes stay exact.");
        var census=exe.FamilyCensus!;
        check(census.PlacedCount==2&&census.Rows.Count==2&&census.StaticTreasureTarget==10&&census.TargetSlot==35&&
            census.TargetFileOffset==0x5FC7E&&!census.RuntimeDropGuarantee&&census.Rows.Select(r=>r.StaticValue).SequenceEqual(new[]{10,0}),
            "Static family sum is10, not placedCount*10; no runtime/drop guarantee is issued.");
        check(census.Rows.Select(r=>r.PlacedOrdinal).SequenceEqual(new[]{0,1})&&
            census.Rows.Select(r=>r.ActorClassId).SequenceEqual(new ushort[]{0x11B,0x1F5})&&
            census.Rows.Select(r=>r.RewardClassId).SequenceEqual(new byte[]{0x56,0xFF})&&
            census.Rows[1].ActorKind==grass.CopyRow()[0x3A]&&census.Rows[1].FamilyAssetPlanSha256==grass.PlanSha256&&
            census.Rows.All(r=>r.SceneRowSha256==Hash(scene.AsSpan(r.SceneRowOffset,0x58))),
            "Exact ordered source family, retained kind, selector and full final row hashes own each ledger entry.");
        check(exe.Census.PlacedCount==1&&exe.Census.StaticTreasureTarget==10&&exe.RevertExact(exe.CopyExecutable()).SequenceEqual(original),
            "Legacy one-Wizard census is not relabeled total placement count; complete EXE inverse restores actual original.");
        foreach(int at in new[]{0xD0,0xD4+0x36,0xD4+0x3A,0xD4+0x53,0x12C+0x36,0x12C+0x3A,0x12C+0x53,0x3044,0x37FF})
        {
            byte[] changed=scene.ToArray();changed[at]^=1;
            Reject<InvalidDataException>(()=>NativeId65FreshWizardExecutableLayout.CompileFamilyComposed(original,
                host.InitializerPatch.CopyBeforeBytes(),host.CopyInitializerPatch(),composition,changed,19),check);
        }
        foreach(int at in new[]{0x2B3F4,0x2B3FC,0x2B400,0x2B404,0x2B408,0x2C2A8})
        {
            byte[] changed=original.ToArray();changed[at]^=1;
            Reject<InvalidDataException>(()=>NativeId65FreshWizardExecutableLayout.CompileFamilyComposed(changed,
                host.InitializerPatch.CopyBeforeBytes(),host.CopyInitializerPatch(),composition,scene,19),check);
        }
        var equal=NativeFreshMobyFamilyCompositionPlan.Compose(body,composition.Placements);
        check(equal.PlanSha256==composition.PlanSha256&&!ReferenceEquals(equal,composition),"Equal actual composition control is independently issued.");
        Reject<InvalidDataException>(()=>exe.RequireFamilyAncestry(equal),check);
        Reject<InvalidDataException>(()=>exe.RequireAncestry(body),check);
        var oldExe=await NativeId65FreshWizardResearchExecutablePlan.CaptureMaterialAuthoredAsync(bin,body);
        check(oldExe.ExecutableSha256==exe.ExecutableSha256&&oldExe.PlanSha256!=exe.PlanSha256&&oldExe.FamilyCompositionAncestor is null,
            "Equal executable bytes never collapse historical and new family capabilities.");
        await Refuse<InvalidDataException>(()=>NativeId65FreshWizardResearchDiscPlan.CaptureFamilyComposedAsync("must-not-open",oldExe),check);
        await Refuse<InvalidDataException>(()=>NativeId65FreshWizardResearchDiscPlan.CaptureAsync("must-not-open",exe),check);
        await Refuse<InvalidDataException>(()=>NativeId65FreshWizardResearchDiscPlan.CaptureMaterialAuthoredAsync("must-not-open",exe),check);
        var disc=await NativeId65FreshWizardResearchDiscPlan.CaptureFamilyComposedAsync(bin,exe);disc.RequireAncestry(exe);
        check(ReferenceEquals(disc.FamilyCompositionAncestor,composition)&&disc.BodyPlanSha256==composition.PlanSha256&&
            disc.CopyBody().SequenceEqual(composition.CopyLevelData())&&disc.CopyOverlay().SequenceEqual(body.CopyOverlay())&&
            disc.FinalExecutableLba==54960&&disc.FinalExecutableEndExclusive==55164&&disc.FinalWadByteLength==0x6B45800&&
            disc.ChangedSectorOwnerCount==1292,"Actual family body drives two additional physical sectors and complete EXE relocation.");
        check(BinaryPrimitives.ReadInt32LittleEndian(disc.CopyWadDirectory().AsSpan(80*8+4))==0x20D800&&
            !disc.RuntimeVerified&&!disc.PublicationApproved&&!disc.NormalCreateBinEnabled,"WAD body length changes, not runtime/publication scope.");
        using(var input=File.OpenRead(bin))using(var audit=File.OpenRead(bin))using(var sink=new InverseSink(audit,disc))
        {
            var result=NativeId65FreshDiscStreaming.Measure(disc,input,CancellationToken.None,sink);
            check(result.ChangedSectors==1292&&result.OutputSha256==sink.Digest()&&sink.Owners==1292&&
                sink.Length==661547040&&sink.RestoredDigest()==NativeId65FreshDiscSource.OriginalSha256,
                "Shared full-image writer and independent source reader preserve all unchanged sectors and complete per-sector inverse.");
            Console.WriteLine($"FAMILY_DISC_FULL_IMAGE hash={result.OutputSha256} owners={sink.Owners} oracleIndependent=false outputWritten=false");
        }
        using(var input=File.OpenRead(bin))
        foreach(int lba in new[]{55162,55163,55164})
        {
            byte[] before=new byte[2352];input.Position=lba*2352L;input.ReadExactly(before);
            byte[] after=disc.CreateExpectedRawSector(lba,before);
            check(before.AsSpan().SequenceEqual(after)==(lba==55164)&&disc.CreateOriginalRawSector(lba,after).SequenceEqual(before),
                "Both newly owned sectors and first unchanged sector obey exact dynamic range/inverse.");
            after[24]^=1;Reject<InvalidDataException>(()=>disc.CreateOriginalRawSector(lba,after),check);
        }
        await MixedExecutableCases.TinyFailure(path=>NativeId65FreshWizardResearchExecutablePlan.CaptureFamilyComposedAsync(path,composition),check,fixtureKind:"family-exe");
        await MixedExecutableCases.TinyFailure(path=>NativeId65FreshWizardResearchDiscPlan.CaptureFamilyComposedAsync(path,exe),check,
            "Fresh-disc capture requires an exact-length seekable original source.","family-disc");
        check(JsonSerializer.Serialize(new{body,host,grass})==originalState&&composition.LevelDataSha256==BodyHash,
            "New arm issuance and refusals leave all source ancestors unchanged.");
    }
    private sealed class InverseSink(Stream original,NativeId65FreshWizardResearchDiscPlan disc):Stream
    {
        private readonly IncrementalHash hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256),inverse=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        private long length;internal int Owners{get;private set;}
        internal string Digest()=>Convert.ToHexStringLower(hash.GetCurrentHash());
        internal string RestoredDigest()=>Convert.ToHexStringLower(inverse.GetCurrentHash());
        public override bool CanRead=>false;public override bool CanSeek=>true;public override bool CanWrite=>true;
        public override long Length=>length;public override long Position{get=>length;set=>throw new NotSupportedException();}
        public override void Write(byte[] b,int at,int n)=>Write(b.AsSpan(at,n));
        public override void Write(ReadOnlySpan<byte> after)
        {
            if(after.Length!=2352)throw new InvalidDataException("Expected one raw sector.");
            int lba=checked((int)(length/2352));byte[] before=new byte[2352];original.ReadExactly(before);
            bool owner=lba is 22 or 37||lba>=53874&&lba<55164;
            if(owner){RawMode2Form1SectorIntegrity.RequireRawSectorIntegrity(after,lba);Owners++;}
            else if(!before.AsSpan().SequenceEqual(after))throw new InvalidDataException("Unchanged sector differs.");
            byte[] restored=disc.CreateOriginalRawSector(lba,after);
            if(!restored.SequenceEqual(before))throw new InvalidDataException("Full source inverse differs.");
            hash.AppendData(after);inverse.AppendData(restored);length+=2352;
        }
        protected override void Dispose(bool disposing){if(disposing){hash.Dispose();inverse.Dispose();}base.Dispose(disposing);}
        public override void Flush(){} public override long Seek(long o,SeekOrigin s)=>throw new NotSupportedException();
        public override void SetLength(long n){if(n!=length)throw new InvalidDataException("Unexpected hash-sink extent.");}
        public override int Read(byte[] b,int a,int n)=>throw new NotSupportedException();
    }
    private static void Reject<T>(Action action,Action<bool,string> check)where T:Exception
    {try{action();}catch(T){check(true,"Expected "+typeof(T).Name);return;}throw new InvalidDataException("Expected "+typeof(T).Name);}
    private static async Task Refuse<T>(Func<Task> action,Action<bool,string> check)where T:Exception
    {try{await action();}catch(T){check(true,"Expected "+typeof(T).Name);return;}throw new InvalidDataException("Expected "+typeof(T).Name);}
    private static string Hash(ReadOnlySpan<byte> b)=>Convert.ToHexStringLower(SHA256.HashData(b));
}
