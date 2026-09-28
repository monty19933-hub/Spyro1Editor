using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;

internal static class ControllerWorkspaceCases
{
    private static uint W(byte[] bytes,int at)=>BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at));
    private static string Hash(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
    internal static void Geometry(Action<bool,string> check,Action<Action,string> refuse)
    {
        foreach(uint start in new uint[]{0x800C0000,0x800C0004,0x800C0008,0x800C000C,0x80167D54})
        {
            var g=NativeCollectibleSaveWorkspacePlan.MeasureController(start,0x801879B0);
            var k=NativeCollectibleSaveControllerKernel.CreateFixture(g.ArenaAddress);
            check(g.TailAddress==start&&g.ArenaAddress%16==0&&g.ArenaAddress-start==g.LeadingBytes,"Controller alignment is explicit");
            check(g.SemanticBytes==k.Regions.Sum(r=>(long)r.ByteLength)&&g.SemanticBytes==0x7300&&g.GrowthBytes==0x7800,"Actual complete controller regions round to7800, not previous5000");
            check(g.ExpectedOrdinaryAddress==k.ExpectedOrdinaryAddress&&g.OrdinaryReadbackAddress==k.OrdinaryReadbackAddress&&
                g.TailReadbackAddress==k.TailReadbackAddress&&g.TransactionStateAddress==k.StateAddress,"Controller sizing fields refer to its actual closed buffers");
            check(g.ArenaAddress+g.SemanticBytes+g.TrailingBytes==g.EndAddress&&g.EndAddress==start+0x7800&&
                g.TailGapToFloor==0x801879B0L-g.EndAddress,"Every controller byte and outer padding charged once");
            check(NativeCollectibleSaveWorkspacePlan.Measure(start,0x801879B0).GrowthBytes==0x5000,"Existing preparation measure remains unchanged");
        }
        foreach(var(start,floor)in new(uint,uint)[]{(0,0x801879B0),(0x800C0001,0x801879B0),(0x801879B0,0x801879B0),
            (0x801F0000,0x80200000),(0xFFFFFFFC,0xFFFFFFFF),(0x800C0000,0x800C77FF),(0x800C0000,0xFFFFFFFF)})
            refuse(()=>NativeCollectibleSaveWorkspacePlan.MeasureController(start,floor),"Controller unaligned/overflow/short floor");
        var exact=NativeCollectibleSaveWorkspacePlan.MeasureController(0x80167D54,0x8016F554);
        check(exact.LeadingBytes==0xC&&exact.TrailingBytes==0x4F4&&exact.TailGapToFloor==0,"Controller exact half-open floor");
        refuse(()=>NativeCollectibleSaveWorkspacePlan.MeasureController(0x80167D54,0x8016F553),"Controller one-byte short floor");
        refuse(()=>NativeCollectibleSaveWorkspacePlan.CaptureController(null!,null!),"Controller has no caller payload/source bypass");
    }

    internal static void Source(NativeCollectibleSaveWorkspacePlan preparation,NativeCollectibleSaveWorkspacePlan plan,
        NativeRegionalSourceSession usa,NativeRegionalSourceSession japan,Action<bool,string> check,Action<Action,string> refuse)
    {
        var k=plan.Controller??throw new Exception("Controller profile missing actual kernel");
        byte[] before=plan.Ancestry.CopyLevelData(),after=plan.CopyLevelData();
        check(preparation.Profile==NativeCollectibleSaveWorkspacePlan.WorkspaceProfile.Preparation&&preparation.Controller is null&&
            preparation.FinalDataSha256=="9e50753a043722117f7843acf1a3d249191c847351fa518c4fada6cc870c7bc4","Preparation profile and exact old data golden preserved");
        check(plan.Profile==NativeCollectibleSaveWorkspacePlan.WorkspaceProfile.Controller&&ReferenceEquals(plan.Preparation,k.Preparation)&&
            plan.SourceCertified&&k.SourceCertified&&plan.MeasuredAllocationFits,"Source-certified controller shares exact nested preparer and reports geometry only");
        check(plan.GrowthBytes==0x7800&&after.Length==0x38D000&&W(after,0x14)==0x57800&&W(after,0x18)==0x1D3800&&
            plan.FinalDataSha256=="a27e8e5848f6cb99cdb0ea5c60aa93e4571b4721db665d48a8877f898a525b7e",
            "Minimum growth starts from admitted parent, not previous workspace; exact emitted-data golden");
        check(plan.Memory.CopyAnchor==0x8008AA38&&plan.Memory.ModelStart==0x80117D54&&plan.Memory.FinalSceneStart==0x8016F554&&
            plan.Memory.FinalSceneEnd==0x80183D54&&plan.Memory.TransferPeakEnd==0x80183D54&&plan.Memory.FinalNormalGap==0x3C5C&&
            plan.Memory.NormalReservedStart==0x801879B0,"Complete final A00 geometry golden");
        check(k.ArenaAddress==0x80167D60&&k.OrdinaryInputAddress==0x8016D900&&k.ExpectedOrdinaryAddress==0x8016DF00&&
            k.OrdinaryReadbackAddress==0x8016E500&&k.TailReadbackAddress==0x8016EB00&&k.StateAddress==0x8016F000&&
            k.FilenameAddress==0x8016F030,"Independent buffer address golden");
        var changes=Enumerable.Range(0x14,4).ToHashSet();
        for(int i=0;i<10;i++)
        {
            int n=(int)W(before,i*8+4);if(n==0)continue;
            int old=(int)W(before,i*8),now=(int)W(after,i*8);
            check(after.AsSpan(now,n).SequenceEqual(before.AsSpan(old,n))&&plan.Layout.Map(new(plan.OriginalDomain,old,n)).Final.Offset==now,
                "Controller common exact retained-subfile projection "+i);
            if(i>2)changes.UnionWith(Enumerable.Range(i*8,4));
        }
        check(Enumerable.Range(0,0x800).Where(i=>!changes.Contains(i)).All(i=>before[i]==after[i]),"Controller full header/class/root complement unchanged");
        long file=plan.TailExtent.Offset;uint ram=plan.Geometry.TailAddress;
        foreach(var owner in plan.Owners)
        {
            byte[] bytes=plan.CopyOwnerBytes(owner.Role);
            check(owner.Extent.Offset==file&&owner.Address==ram&&owner.InitialSha256==Hash(bytes),"Controller complete owner partition "+owner.Role);
            if(owner.Role is not("controller-code" or "preparation-code" or "fixed-filename"))
                check(!bytes.AsSpan().ContainsAnyExcept((byte)0),"Controller explicit zero owner "+owner.Role);
            file=owner.Extent.EndExclusive;ram+=(uint)owner.Extent.ByteLength;
        }
        check(file==W(after,0x18)&&ram==plan.Memory.FinalSceneStart,"Controller owner partition reaches exact scene boundary");
        foreach(var region in k.Regions)
        {
            var contained=plan.Owners.Where(o=>o.Address>=region.Address&&o.Address+(uint)o.Extent.ByteLength<=region.Address+region.ByteLength).ToArray();
            check(contained.Sum(o=>o.Extent.ByteLength)==region.ByteLength&&contained[0].Address==region.Address,"Materialized owners exactly cover kernel region "+region.Role);
        }
        byte[] filename=plan.CopyOwnerBytes("fixed-filename");
        check(filename.Length==0x20&&filename.AsSpan(0,18).SequenceEqual(k.CopyFilename())&&
            filename.AsSpan(0,18).SequenceEqual(usa.ReadExecutableRange(0x15B8,18))&&!filename.AsSpan(18).ContainsAnyExcept((byte)0),"Closed pinned filename plus fourteen zero bytes");
        check(plan.CodeBlocks.Count==5&&plan.CodeBlocks[0].Role=="controller"&&plan.CodeBlocks[0].Bytes.SequenceEqual(k.CopyBytes()),"Actual controller and four nested emitted blocks materialized");
        foreach(var block in plan.CodeBlocks)
        {
            check(block.Address==plan.Geometry.TailAddress+block.Extent.Offset-plan.TailExtent.Offset&&
                after.AsSpan((int)block.Extent.Offset,(int)block.Extent.ByteLength).SequenceEqual(block.Bytes)&&Hash(block.Bytes)==block.Sha256,
                "Controller exact code addresses/bytes "+block.Role);
            byte[] copy=block.Bytes;copy[0]^=1;check(Hash(block.Bytes)==block.Sha256,"Controller code copies are defensive "+block.Role);
        }
        byte[] expectedTail=new byte[plan.GrowthBytes];
        foreach(var block in plan.CodeBlocks)block.Bytes.CopyTo(expectedTail,checked((int)(block.Extent.Offset-plan.TailExtent.Offset)));
        k.CopyFilename().CopyTo(expectedTail,checked((int)(k.FilenameAddress-plan.Geometry.TailAddress)));
        check(after.AsSpan((int)plan.TailExtent.Offset,plan.GrowthBytes).SequenceEqual(expectedTail),
            "Complete insertion is only five exact blocks and pinned filename; every code-capacity gap and remaining byte is zero");
        check(plan.RevertExact(after).SequenceEqual(before)&&plan.RevertToOriginalSourceExact(after).SequenceEqual(usa.ReadWadEntry(10)),"Controller exact parent/source inverses");
        foreach(int at in new[]{0,0x14,0x18,0x50,0x150,0x7FF,0x800,after.Length-1}.Concat(plan.Owners.SelectMany(o=>new[]{(int)o.Extent.Offset,(int)o.Extent.EndExclusive-1})).Distinct())
        {var bad=after.ToArray();bad[at]^=1;refuse(()=>plan.RevertExact(bad),"Controller complete afterimage corruption "+at);}
        refuse(()=>plan.RevertExact(after[..^1]),"Controller truncation");refuse(()=>plan.RevertExact([..after,0]),"Controller extension");
        refuse(()=>plan.RevertExact(preparation.CopyLevelData()),"Preparation output is not controller afterimage");
        refuse(()=>preparation.RevertExact(after),"Controller output is not preparation afterimage");
        var other=NativeJapaneseEggAdmittedLifecyclePlan.Compose(plan.Ancestry.Admission,plan.Ancestry.Authored,plan.Ancestry.Lifecycle);
        refuse(()=>plan.RequireCurrent(other),"Controller rejects byte-identical replacement owner");
        refuse(()=>NativeCollectibleSaveWorkspacePlan.CaptureController(japan,plan.Ancestry),"Controller wrong regional source");
        check(plan.RequiredRowRebindings.Select(r=>r.NewRowAddress).SequenceEqual(new uint[]{0x8017D3D0,0x8017D428})&&
            plan.RequiredRowRebindings.Select(r=>r.NewPrivateAddress).SequenceEqual(new uint[]{0x80181420,0x8018142C})&&
            plan.RequiredGlobalRebindings.All(g=>g.NewValue==g.OldValue+0x7800),"Controller final row/private/global rebinding evidence");
        check(!plan.CurrentGuardMatchesProjection&&!plan.NativeResidenceIssued&&!plan.HooksInstalled&&!plan.InstructionCacheProven&&
            !plan.InterruptSafetyProven&&!plan.LifetimeProven&&!plan.OriginalModelReferenceClosureProven&&!plan.FullRuntimePeakProven&&
            !plan.RuntimeVerified&&!plan.AsynchronousCardLeaseProven&&!plan.FutureControllerFitProven&&!plan.CardWrites&&!plan.PublicationApproved,
            "Controller measured allocation does not grant hooks, current guard, lifetime or publication");
        refuse(plan.RequireNativeInstallation,"Controller allocation is not an installation");
    }
}
