using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;

internal static class SceneCases
{
    private static int checks, refusals;
    // Complete original33/T6 owners; neutral fixtures are not an issuer.
    private static readonly byte[] Row = Convert.FromHexString("50c300000000000000000000f68f0000f6040100cd6c00000000000000000080000000000000000000000000000000000000000000001b0100007f0000000000002000ff000000040000ff0900000000200010567f100000");
    private static readonly byte[] Private = Convert.FromHexString("78c300000000000000000000200000000000000001000000ffffffff000000000000000000000000020000000000fffff68f0000f6040100ec6d0000000000007b760000aef80000ec6d000000000000");
    private static NativeWizardPlacementIntent Placement => new(8704, 8960, 8704, 9216, 8960);
    private static NativeId65FreshDocumentIntent A() => NativeId65FreshDocumentIntent.Create(
        8448,8448,8704,1024,1024,8960,8960,64,new(80,128,112),new(128,96,64),19);

    internal static async Task SourceFree()
    {
        Type t = typeof(NativeId65FreshWizardResearchScenePlan);
        Check(t.IsSealed && t.GetConstructors().Length == 0 && t.GetProperties().All(p=>p.SetMethod == null), "Closed immutable result.");
        var factories = t.GetMethods(BindingFlags.Public|BindingFlags.Static|BindingFlags.DeclaredOnly);
        Check(factories.Length==2 && factories.Select(m=>m.Name).Order().SequenceEqual(new[]{"CaptureAsync","CaptureAuthoredAsync"}) &&
            factories.Single(m=>m.Name=="CaptureAsync").GetParameters().Select(p=>p.ParameterType).SequenceEqual(new[]{
            typeof(string),typeof(NativeId65FreshDocumentCompilation),typeof(NativeWizardFamilyAssetPlan),
            typeof(NativeId65FreshWizardResearchResourcePlan),typeof(NativeWizardPlacementIntent),typeof(CancellationToken)}) &&
            factories.Single(m=>m.Name=="CaptureAuthoredAsync").GetParameters().Select(p=>p.ParameterType).SequenceEqual(new[]{
            typeof(string),typeof(NativeFreshLevelDocumentIntent),typeof(NativeId65FreshDocumentCompilation),typeof(NativeWizardFamilyAssetPlan),
            typeof(NativeId65FreshWizardResearchResourcePlan),typeof(CancellationToken)}),
            "Named legacy and actual-aggregate producers require issued ancestry; no hash/bytes/approval factory.");
        Check(!t.GetMethods(BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly).Any(m=>
            new[]{"Publish","Write","CompileBody","CompileOverlay"}.Any(s=>m.Name.StartsWith(s,StringComparison.Ordinal))), "No body/overlay/output writer.");
        using(var canceled=new CancellationTokenSource())
        {canceled.Cancel();await Reject<OperationCanceledException>(()=>NativeId65FreshWizardResearchScenePlan.CaptureAsync(null!,null!,null!,null!,null!,canceled.Token));}
        await Reject<ArgumentNullException>(()=>NativeId65FreshWizardResearchScenePlan.CaptureAsync("not-opened.bin",null!,null!,null!,Placement));
        Check(Hash(Row)=="59401eafeab453fd9d26f14eedca58390653dc5f4fcc3d2560f7b3e8fc7afa82" &&
            Hash(Private)=="5b3d762f6117ea30e959a21bf2778f3442b03eba93125c9a0e0a642cc58d345b", "Embedded source-form complete pins.");
        byte[] p0=NativeId65FreshSceneLayout.Compile(new(8960,8960,9216,64),16,new byte[120],
            NativeId65ZeroSceneAnimationLists.CreateNeutral().CopyComponents()).CopyScene();
        var layout=NativeId65FreshWizardSceneLayout.Compile(p0,Row,Private,Placement);
        RequireLayout(layout,p0,Row,Private);
        StorageCases.Run(p0,Row,Private);
        Check(!layout.SourceCertified, "Neutral layout never issues source capability.");
        byte[] b=layout.CopyScene();
        for(int at=0;at<b.Length;at++)
        {byte[] corrupt=b.ToArray();corrupt[at]^=1;Reject<InvalidDataException>(()=>layout.RequireExactScene(corrupt));}
        Check(layout.CopyScene().SequenceEqual(b), "All byte mutation refusals are atomic.");
        b[0]^=1;Check(layout.CopyScene()[0]!=b[0],"No result buffer alias.");
        foreach(var intent in new[]{ Placement with{OriginX=8448},Placement with{EndX=9472},Placement with{OriginY=8448},
            Placement with{EndY=9472},Placement with{OriginZ=8703},Placement with{OriginZ=9217},Placement with{OriginX=8705},
            Placement with{EndX=8704},Placement with{OriginZ=int.MaxValue},Placement with{EndX=-16} })
            Reject<InvalidDataException>(()=>NativeId65FreshWizardSceneLayout.Compile(p0,Row,Private,intent));
        foreach(var intent in new[]{new NativeWizardPlacementIntent(8464,8464,8704,9456,9456),new(9456,9456,9216,8464,8464)})
        {var edge=NativeId65FreshWizardSceneLayout.Compile(p0,Row,Private,intent);Check(I32(edge.CopyScene(),0xF88+0x48)==intent.OriginZ+287,"Strict-inside first/last grid positions and both height endpoints.");}
        foreach(var inputs in new[]{(p0[..^1],Row,Private),(p0,Row[..^1],Private),(p0,Row,Private[..^1]),(new byte[0x3800],Row,Private)})
            Reject<InvalidDataException>(()=>NativeId65FreshWizardSceneLayout.Compile(inputs.Item1,inputs.Item2,inputs.Item3,Placement));
        foreach(var bad in new[]{(true,0),(true,0x30),(false,0),(false,0x28),(false,0x38)})
        {byte[] r=Row.ToArray(),p=Private.ToArray();(bad.Item1?r:p)[bad.Item2]^=1;Reject<InvalidDataException>(()=>NativeId65FreshWizardSceneLayout.Compile(p0,r,p,Placement));}
        byte[] changed=p0.ToArray();changed[^1]=1;Reject<InvalidDataException>(()=>NativeId65FreshWizardSceneLayout.Compile(changed,Row,Private,Placement));
        Check((0xE58-0x58)/0x70==32 && (0xE58-0x58)-32*0x70==0,
            "Appending into oldP0/N32 reports32 but has zero lookahead reserve; nominal count alone is insufficient.");
        Console.WriteLine($"FRESH_WIZARD_SCENE_FREE_OK checks={checks} refusals={refusals} outputsWritten=0");
    }

    internal static async Task Source(string original)
    {
        RequireOriginal(original);
        try
        {
            var c=await NativeId65FreshDocumentCompilation.CaptureAsync(original,A());
            var f=await NativeWizardFamilyAssetPlan.CaptureAsync(original);
            var r=await NativeId65FreshWizardResearchResourcePlan.CaptureAsync(original,c,f);
            byte[] p0=c.FreshResourceAncestor.CopyScene(),body=c.DiscPlan.CopyBody(),upload=r.CopyUploadPayload(),data=r.CopyDataComponents(),models=r.CopyModelPayload();
            string oldJson=JsonSerializer.Serialize(c);
            var p=await NativeId65FreshWizardResearchScenePlan.CaptureAsync(original,c,f,r,Placement);
            p.RequireAncestry(c,f,r);
            Check(ReferenceEquals(p.CompilationAncestor,c)&&ReferenceEquals(p.FamilyAncestor,f)&&ReferenceEquals(p.ResourceAncestor,r),"Exact actual private ancestry, not hashes.");
            RequireLayout(p.Layout,p0,f.CopyOwner("wizard.row"),f.CopyOwner("wizard.private"));
            Check(p.SceneSha256=="b7bd222702ff9b5d6ea40c623159f4aa22b3425ef9368059167d1ccab930266b",
                "Independent raw original-sector P1/N32 full scene golden, established before Core result.");
            Check(p.SceneSha256==Hash(p.CopyScene()) && p.PlanSha256.Length==64 && p.CanonicalDocumentSha256==c.CanonicalSha256 &&
                p.CopyCanonicalDocument().SequenceEqual(c.CopyCanonicalDocument()),"Actual immutable canonical lineage and scene digest.");
            Check(p.PlacedCount==1 && p.SceneOrdinal==0 && p.NativeNominalCapacity==32 && p.DynamicArenaByteLength==0xE58 &&
                !p.RuntimeVerified&&!p.PublicationApproved&&!p.GlobalAudioCompatibilityProven&&!p.CompleteNativeDependencyClosureProven&&
                !p.NativeArenaExhaustionSafe&&!p.BodyIssued&&!p.PersistentOwnerIssued&&!p.AuthoredDryCorridorAccepted&&!p.NativeBodyAndTrajectoryProven,
                "Detached scene-only ordinal and honest false gates.");
            Check(p.PendingOwners.Count>=4,"Remaining callback/reward/audio/body/runtime ownership remains explicit.");
            p.RequireExactScene(p.CopyScene());
            foreach(var owner in p.Owners)
            {byte[] bad=p.CopyScene();bad[owner.Offset]^=1;Reject<InvalidDataException>(()=>p.RequireExactScene(bad));}
            var repeat=await NativeId65FreshWizardResearchScenePlan.CaptureAsync(original,c,f,r,Placement);
            Check(repeat.PlanSha256==p.PlanSha256&&repeat.SceneSha256==p.SceneSha256,"Same actual ancestry deterministic.");
            var alternate=await NativeId65FreshWizardResearchScenePlan.CaptureAsync(original,c,f,r,Placement with{OriginZ=8909});
            Check(alternate.SceneSha256!=p.SceneSha256 && alternate.PlanSha256!=p.PlanSha256,"Actual geometric intent drives output, not hidden default.");
            using(var token=new CancellationTokenSource())
            {token.Cancel();await Reject<OperationCanceledException>(()=>NativeId65FreshWizardResearchScenePlan.CaptureAsync("not-opened.bin",c,f,r,Placement,token.Token));}
            await Reject<InvalidDataException>(()=>NativeId65FreshWizardResearchScenePlan.CaptureAsync("not-opened.bin",c,f,r,Placement with{EndX=9472}));
            await Reject<ArgumentNullException>(()=>NativeId65FreshWizardResearchScenePlan.CaptureAsync("not-opened.bin",c,f,null!,Placement));
            var f2=await NativeWizardFamilyAssetPlan.CaptureAsync(original);
            Check(f2.BlockSha256==f.BlockSha256,"Mismatched actual family is genuinely byte-identical.");
            await Reject<InvalidDataException>(()=>NativeId65FreshWizardResearchScenePlan.CaptureAsync("not-opened.bin",c,f2,r,Placement));
            var c2=await NativeId65FreshDocumentCompilation.CaptureAsync(original,A());
            Check(c2.CanonicalSha256==c.CanonicalSha256,"Mismatched actual compilation is genuinely byte-identical.");
            await Reject<InvalidDataException>(()=>NativeId65FreshWizardResearchScenePlan.CaptureAsync("not-opened.bin",c2,f,r,Placement));
            var r2=await NativeId65FreshWizardResearchResourcePlan.CaptureAsync(original,c,f);
            Reject<InvalidDataException>(()=>p.RequireAncestry(c,f,r2));
            var own=await NativeId65FreshWizardResearchScenePlan.CaptureAsync(original,c,f,r2,Placement);
            Check(own.SceneSha256==p.SceneSha256,"Another real resource result makes its own plan, not rebinding the first.");
            Check(Hash(p0)=="4a6e7b5c6c53199b8a7aa6ae0d24ac9bce32634aa259d5f49535282080641433" &&
                Hash(body)=="b4abbdd09f794510d9b9341d7c7111ddd0b9068411bab30e70e7477b4f8c096c" &&
                Hash(upload)=="d74c2571ed857865b2b989af3001117f44e0915f9c6d59927e30a49d90a79402" &&
                Hash(data)=="bea93c58d6d568c7c6162816ddad1afff32a51886f9412a0809b22887e0b83d4" &&
                Hash(models)=="782c4500b3152f6051b45b8a0a56f6c99c4f703c753948f656318ccfa8618cda", "All old P0/body/resource goldens unchanged.");
            Check(c.FreshResourceAncestor.CopyScene().SequenceEqual(p0)&&c.DiscPlan.CopyBody().SequenceEqual(body)&&JsonSerializer.Serialize(c)==oldJson&&
                r.CopyUploadPayload().SequenceEqual(upload)&&r.CopyDataComponents().SequenceEqual(data)&&r.CopyModelPayload().SequenceEqual(models),"All upstream values preserved after successes/refusals.");
            Console.WriteLine($"sceneSha256={p.SceneSha256} planSha256={p.PlanSha256}");
        }
        finally {RequireOriginal(original);}
        Console.WriteLine($"FRESH_WIZARD_SCENE_SOURCE_OK checks={checks} refusals={refusals} outputsWritten=0");
    }

    private static void RequireLayout(NativeId65FreshWizardSceneLayout l,byte[] p0,byte[] row,byte[] props)
    {
        byte[] b=l.CopyScene();
        StorageCases.RequireLegacy(p0,row,props,b);
        Reject<InvalidDataException>(()=>NativeId65FreshSceneLayout.DecodeCanonical(b));
        Check(b.Length==0x3000 && I32(b,0xCC)==0xEB8 && I32(b,0xD0)==1 && l.DynamicArenaOffset==0x12C &&
            l.DynamicArenaByteLength==0xE58 && l.NativeNominalCapacity==32,"P1 row is additional to N32+58 arena.");
        Check(I32(b,0xF84)==0x54&&I32(b,0xFD8)==8&&I32(b,0xFDC)==0&&I32(b,0xFE0)==0x2004&&
            I32(b,0x2FE4)==2&&I32(b,0x2FE8)==0xD4&&I32(b,0x2FEC)==0xF88&&
            I32(b,0xD4)==0xF88&&I32(b,0xF88)==0xFB0,"Exact component chain, two scene-relative fixups and source-private targets.");
        Check(new[]{I32(b,0xE0),I32(b,0xE4),I32(b,0xE8),I32(b,0xFB8),I32(b,0xFBC),I32(b,0xFC0),I32(b,0xFC8),I32(b,0xFCC),I32(b,0xFD0)}
            .SequenceEqual(new[]{8704,8960,8704,8704,8960,8991,9216,8960,8991}),"Independent nine-field geometric mapping including source+287 route Z.");
        Check(b.AsSpan(0,0xCC).SequenceEqual(p0.AsSpan(0,0xCC)) && b.AsSpan(0x12C,0xE58).ContainsAnyExcept((byte)0)==false &&
            b.AsSpan(0xFE4,0x2000).ContainsAnyExcept((byte)0)==false&&b.AsSpan(0x2FF0).ContainsAnyExcept((byte)0)==false,"Retained prefix and separately generated complete complements.");
        int at=0;foreach(var o in l.Owners)
        {
            Check(o.Offset==at&&o.ByteLength>0&&o.ByteLength<=b.Length-at&&Hash(b.AsSpan(at,o.ByteLength))==o.Sha256,"Contiguous exhaustive bound byte owner.");
            if(o.Provenance=="retained-source")
            {byte[] src=o.SourceOwnerId switch{"fresh.scene"=>p0,"wizard.row"=>row,"wizard.private"=>props,_=>throw new InvalidDataException()};
                Check(b.AsSpan(at,o.ByteLength).SequenceEqual(src.AsSpan(o.SourceOffset!.Value,o.ByteLength)),"Complete untouched source complement.");}
            at+=o.ByteLength;
        }
        Check(at==b.Length&&l.Mutations.Count==9&&l.PointerFixups.Count==2,"Exhaustive scene and exactly nine coordinate/two pointer edits.");
        byte[] inverseRow=b.AsSpan(0xD4,0x58).ToArray(),inversePrivate=b.AsSpan(0xF88,0x50).ToArray();
        foreach(var m in l.Mutations)
        {Check(I32(b,m.SceneOffset)==m.After,"Every geometric field projects to bytes.");Put(m.SceneOffset<0x12C?inverseRow:inversePrivate,m.SceneOffset-(m.SceneOffset<0x12C?0xD4:0xF88),m.Before);}
        foreach(var f in l.PointerFixups)
        {Check(I32(b,f.SceneOffset)==f.After,"Every pointer field projects to bytes.");Put(f.SceneOffset<0x12C?inverseRow:inversePrivate,f.SceneOffset-(f.SceneOffset<0x12C?0xD4:0xF88),f.Before);}
        Check(inverseRow.SequenceEqual(row)&&inversePrivate.SequenceEqual(props),"Only declared11 fields differ from entire donor row/private.");
        Check(l.NextRowReserveOffset==0xC2C && l.NextRowReserveByteLength==0x58 && l.DynamicPropertiesOffset==0xC84 &&
            l.DynamicPropertiesByteLength==0x300&&l.DynamicPropertiesOffset+l.DynamicPropertiesByteLength==0xF84,"32 complete rows, exact lookahead gap,32 descending properties.");
        Check(l.RuntimeAnchors.Single(a=>a.Id=="placed-row-base").SceneRelativeOffset==0xD4&&
            l.RuntimeAnchors.Single(a=>a.Id=="dynamic-row-base").SceneRelativeOffset==0x12C&&
            l.RuntimeAnchors.Single(a=>a.Id=="dynamic-properties-end").SceneRelativeOffset==0xF84&&
            l.RuntimeAnchors.Single(a=>a.Id=="two-spatial-bucket-banks").SceneRelativeOffset==0xFE4,"Scene-relative loader ledger; no invented absolute RAM base.");
    }
    private static void RequireOriginal(string path)
    {using var s=File.OpenRead(path);Check(s.Length==661547040&&Convert.ToHexStringLower(SHA256.HashData(s))=="fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37"&&s.Position==661547040&&s.ReadByte()==-1&&s.Position==661547040&&s.Length==661547040,"Actual full original before/finally SHA/EOF/length/position.");}
    private static int I32(ReadOnlySpan<byte> b,int at)=>BinaryPrimitives.ReadInt32LittleEndian(b[at..]);
    private static void Put(Span<byte> b,int at,int n)=>BinaryPrimitives.WriteInt32LittleEndian(b[at..],n);
    private static string Hash(ReadOnlySpan<byte> b)=>Convert.ToHexStringLower(SHA256.HashData(b));
    private static void Check(bool yes,string why){if(!yes)throw new InvalidDataException(why);checks++;}
    private static void Reject<T>(Action a)where T:Exception{try{a();}catch(T){checks++;refusals++;return;}throw new InvalidDataException("Expected "+typeof(T).Name);}
    private static async Task Reject<T>(Func<Task> a)where T:Exception{try{await a();}catch(T){checks++;refusals++;return;}throw new InvalidDataException("Expected "+typeof(T).Name);}
}
