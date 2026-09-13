using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Scene;

internal static class FamilyCompositionCases
{
    private const string WizardId="ffffffffffffffffffffffffffffffff",GrassId="11111111111111111111111111111111";
    internal static void Free(Action<bool,string> check)
    {
        FamilyRowSpanCases.Free(check);
        var type=typeof(NativeFreshMobyFamilyCompositionPlan);
        check(type.IsSealed&&!type.IsPublic&&type.GetConstructors().Length==0,"Family composition is internal, closed and has no caller-byte authority.");
        Reject<ArgumentNullException>(()=>NativeFreshMobyFamilyCompositionPlan.Compose(null!,[]),check);
        Reject<ArgumentNullException>(()=>NativeFreshMobyFamilyPlacement.RetainedWizard(WizardId,null!),check);
        Reject<ArgumentNullException>(()=>NativeFreshMobyFamilyPlacement.Passive(GrassId,null!,null!,8960,8960,8704,0),check);
    }

    internal static async Task Source(string original,NativeId65FreshWizardResearchBodyPlan body,Action<bool,string> check,
        Func<string,NativeFreshMobyFamilyCompositionPlan,Action<bool,string>,Task>? onP2=null)
    {
        var wizard=body.HostAncestor.SceneAncestor.FamilyAncestor;
        var retained=NativeFreshMobyFamilyPlacement.RetainedWizard(WizardId,wizard);
        byte[] before=body.CopyLevelData();
        var p1=NativeFreshMobyFamilyCompositionPlan.Compose(body,[retained]);
        check(p1.CopyLevelData().SequenceEqual(before)&&p1.CopyInverseBaseBody().SequenceEqual(before),
            "Shared allocator/composer with retained Wizard alone reproduces complete existing body and inverse.");
        var grass=await NativeMobyFamilyAssetPlan.CaptureArtisansGrassAsync(original);
        var proof=NativeMobyFamilyPassiveHostBinding.Bind(body.HostAncestor,grass);
        var added=NativeFreshMobyFamilyPlacement.Passive(GrassId,grass,proof,9152,9152,8704,64);
        var p2=NativeFreshMobyFamilyCompositionPlan.Compose(body,[retained,added]);
        byte[] bytes=p2.CopyLevelData(),scene=p2.CopyScene();
        check(p2.SourceCertified&&!p2.RuntimeVerified&&!p2.PublicationApproved&&!p2.CompleteNativeBehaviorProven,
            "Actual sources and storage closure are not full behavior, runtime or export approval.");
        check(ReferenceEquals(p2.BaseBodyAncestor,body)&&p2.Placements.Select(p=>p.EditorId).SequenceEqual(new[]{WizardId,GrassId})&&
            p2.Allocation.Rows.Select(r=>r.Index).SequenceEqual(new[]{0,1}),"Author order survives lexical IDs; retained Wizard remains ordinal0.");
        check(bytes.Length==0x20D800&&p2.Layout.PlacedCount==2&&p2.Layout.DynamicCapacity==32&&scene.Length==0x3800&&
            p2.Layout.DynamicRowOffset==0x184&&p2.Layout.NextRowReserveOffset==0xC84&&p2.Layout.DynamicPropertiesOffset==0xCDC&&
            p2.Layout.PrivateOffsets.SequenceEqual(new[]{0xFE0,0x1030})&&p2.Layout.FixupCountOffset==0x3044,
            "P2 growth keeps every dynamic row, lookahead, private block and bucket owner.");
        int[] offsets=[0x800,0xE6000,0xE7000,0x100800,0x104000,0x14D800,0x190000,0x1ED000];
        int[] lengths=[0xE5800,0x1000,0x19800,0x3800,0x49800,0x42800,0x5D000,0x20800];
        for(int i=0;i<8;i++)check(Word(bytes,i*8)==offsets[i]&&Word(bytes,i*8+4)==lengths[i],"Derived complete body owner "+i);
        check(Word(bytes,0x6C)==0x100000&&BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(0x15E))==0x1F5&&
            bytes.AsSpan(0xE7000,0x19000).SequenceEqual(before.AsSpan(0xE7000,0x19000))&&
            bytes.AsSpan(0x100000,0x174).SequenceEqual(grass.CopyPackage())&&
            !bytes.AsSpan(0x100174,0x68C).ContainsAnyExcept((byte)0),"Append complete Grass package after all old model ownership, not into padding.");
        byte[] expectedRow=grass.CopyRow();
        Put(expectedRow,0,0x1030);Put(expectedRow,12,9152);Put(expectedRow,16,9152);Put(expectedRow,20,8704);
        // Independent cardinal yaw64 = 270 degrees, not spawn heading64.
        short[] matrix=[0,0,-4096,0,4096,0,4096,0,0];
        for(int i=0;i<matrix.Length;i++)BinaryPrimitives.WriteInt16LittleEndian(expectedRow.AsSpan(0x20+i*2),matrix[i]);
        expectedRow[0x46]=64;
        check(scene.AsSpan(0x12C,0x58).SequenceEqual(expectedRow)&&scene.AsSpan(0x1030,8).SequenceEqual(grass.CopyProperties()),
            "Native Grass authored transform and private source complement are exact.");
        check(Word(scene,0xD4)==0xFE0&&Word(scene,0xFE0)==0x1008&&Word(scene,0x3044)==3&&
            new[]{Word(scene,0x3048),Word(scene,0x304C),Word(scene,0x3050)}.SequenceEqual(new[]{0xD4,0xFE0,0x12C}),
            "Both families use explicit ordered scene fixups; Wizard route pointer moves with private storage.");
        check(p2.CopyInverseBaseBody().SequenceEqual(before)&&body.CopyLevelData().SequenceEqual(before),
            "Full composed image inverses to the actual old Body and leaves it immutable.");
        p2.RequireAncestry(body);p2.RequireExactLevelData(bytes);
        var repeat=NativeFreshMobyFamilyCompositionPlan.Compose(body,[retained,added]);
        check(repeat.PlanSha256==p2.PlanSha256&&repeat.CopyLevelData().SequenceEqual(bytes),"Repeat actual family request is deterministic.");
        FamilyRowSpanCases.Source(wizard,grass,p1,p2,check);
        if(onP2 is not null)await onP2(original,p2,check);
        var p3=NativeFreshMobyFamilyCompositionPlan.Compose(body,[retained,added,
            NativeFreshMobyFamilyPlacement.Passive("22222222222222222222222222222222",grass,proof,8832,9152,8704,0)]);
        check(p3.Layout.PlacedCount==3&&p3.CopyLevelData().AsSpan(0x100000,0x174).SequenceEqual(grass.CopyPackage())&&
            p3.Allocation.Rows[1].Properties.Offset!=p3.Allocation.Rows[2].Properties.Offset&&
            Word(p3.CopyLevelData(),0x14)==0x19800&&Word(p3.CopyLevelData(),0x70)==0,
            "Repeated passive instances share one registered immutable package and have distinct private state.");
        var reissuedGrass=await NativeMobyFamilyAssetPlan.CaptureArtisansGrassAsync(original);
        var reissuedProof=NativeMobyFamilyPassiveHostBinding.Bind(body.HostAncestor,reissuedGrass);
        check(!ReferenceEquals(reissuedGrass,grass)&&reissuedGrass.PlanSha256==grass.PlanSha256,
            "Equal reissued Grass evidence still has another actual retained source-asset identity.");
        Reject<InvalidDataException>(()=>NativeFreshMobyFamilyCompositionPlan.Compose(body,[retained,added,
            NativeFreshMobyFamilyPlacement.Passive("33333333333333333333333333333333",reissuedGrass,reissuedProof,8832,9152,8704,0)]),check);
        foreach(var bad in new[]{Array.Empty<NativeFreshMobyFamilyPlacement>(),new[]{added,retained},new[]{retained,retained},new[]{retained,added,added}})
            Reject<InvalidDataException>(()=>NativeFreshMobyFamilyCompositionPlan.Compose(body,bad),check);
        var floor=body.HostAncestor.SceneAncestor.CompilationAncestor.Intent;
        foreach(var bad in new[]{(floor.FloorX,8960,8704,0),(8960,floor.FloorY+floor.FloorDepth,8704,0),(8960,8960,-1,0),(8960,8960,8704,256)})
            Reject<InvalidDataException>(()=>NativeFreshMobyFamilyCompositionPlan.Compose(body,[retained,
                NativeFreshMobyFamilyPlacement.Passive(GrassId,grass,proof,bad.Item1,bad.Item2,bad.Item3,bad.Item4)]),check);
        var equalBody=body.MixedResourceAncestor is { } mixed
            ?await NativeId65FreshWizardResearchBodyPlan.CaptureMaterialAuthoredAsync(original,mixed)
            :await NativeId65FreshWizardResearchBodyPlan.CaptureAsync(original,body.HostAncestor);
        Reject<InvalidDataException>(()=>p2.RequireAncestry(equalBody),check);
        byte[] equal=bytes.ToArray();equal[0x6C]^=1;Reject<InvalidDataException>(()=>p2.RequireExactLevelData(equal),check);
        foreach(int at in new[]{0,0xE6000,0x100000,0x100174,0x100800+0x184,0x100800+0x3048,0x104000,bytes.Length-1})
        {byte[] bad=bytes.ToArray();bad[at]^=1;Reject<InvalidDataException>(()=>p2.RequireExactLevelData(bad),check);}
        bytes[0]^=1;scene[0]^=1;
        check(p2.CopyInverseBaseBody().SequenceEqual(before)&&Hash(p2.CopyLevelData())==p2.LevelDataSha256,
            "Copy mutation and refusal cannot alter retained composition.");
        Console.WriteLine($"NATIVE_FRESH_FAMILY_COMPOSITION_SOURCE_OK body={p2.LevelDataSha256} scene={p2.SceneSha256} runtime=0 publication=0");
    }
    private static int Word(ReadOnlySpan<byte> b,int at)=>BinaryPrimitives.ReadInt32LittleEndian(b[at..]);
    private static void Put(Span<byte> b,int at,int value)=>BinaryPrimitives.WriteInt32LittleEndian(b[at..],value);
    private static string Hash(ReadOnlySpan<byte> b)=>Convert.ToHexStringLower(SHA256.HashData(b));
    private static void Reject<T>(Action action,Action<bool,string> check)where T:Exception
    {try{action();}catch(T){check(true,"Family composition refused "+typeof(T).Name);return;}throw new InvalidDataException("Expected "+typeof(T).Name);}
}
