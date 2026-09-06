using System.Buffers.Binary;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
using static Program;

internal static class SourceCases
{
    // Independent Python raw-sector oracle values, established before any new
    // C# production type exists. Existing untextured values are not changed.
    internal const string UploadGolden = "2ab548f540fbfadddc38d96e70a9633a1c6f37c37ee2d1a896919ac30f929333";
    internal const string DataGolden = "c13c21171e5ed4755f1598914eba89b0b31795b91aaeb2605efc6fed9aa17160";
    private static readonly string[] RecordGoldens =
    [
        "06138c4791b32b848dd81cd4dbab46419d7f3bd1c6b46a6f38194c8645ba7251",
        "b57966bf69be49d581dfd73d764155de838906d5d7550ff4d1c85de7c272d100"
    ];

    internal static async Task Run(string original)
    {
        var floor = new NativeId65AuthoredFloor(8448, 8448, 8704, 1024, 1024, 0x708050, 0x406080);
        var surface = await NativeId65AuthoredSurfaceBundle.CaptureAsync(original, floor);
        var companion = await NativeId65CompanionAssetPlan.CaptureAsync(original);
        var resources = await NativeId65NormalResourceBundle.CaptureAsync(original, surface, companion);
        var lists = await NativeId65ZeroSceneAnimationLists.CaptureAsync(original);
        var chain = NativeId65NormalResourceChain.Compile(resources, lists);
        var fresh = await NativeId65FreshSceneBundle.CaptureAsync(original, chain, new(8960, 8960, 9216, 64), 16);
        var dark = NativeStaticTerrainTextureSource.Capture(original, 14, 0);
        var gnasty = NativeStaticTerrainTextureSource.Capture(original, 70, 0);
        byte[] beforeUpload = fresh.CopyUploadPayload(), beforeData = fresh.CopyDataComponents();
        byte[] beforeScene = fresh.CopyScene(), beforeModel = fresh.CopyModelPayload();
        Check(fresh.UploadSha256 == "b769a9a298285aa402956fdbd5721132736388c6f3f1dc1bfcd7e15a9afeb66d" &&
            fresh.DataSha256 == "821d25d4c012aa180d369f755d82a780dbcf244b4e53c6c4bcf349da533dce65" &&
            fresh.SceneSha256 == "4a6e7b5c6c53199b8a7aa6ae0d24ac9bce32634aa259d5f49535282080641433" &&
            fresh.ModelSha256 == "88c78343c283256de6ce8d3151b43fa72ba4d96b046d869bd0a7209cbff4c23d",
            "All prior untextured upload/data/scene/model byte goldens remain exact.");
        Check(dark.SourceTextureId == gnasty.SourceTextureId && dark.Handle != gnasty.Handle,
            "Same localT0 at Dark and Gnasty's World is two different complete source variants.");
        var plan = await NativeId65FreshTerrainTexturePlan.CaptureAsync(original, fresh, dark, gnasty);
        Check(plan.SourceCertified && plan.DestinationAllocationProven && !plan.RuntimeVerified &&
            !plan.PublicationApproved && !plan.HqRuntimeProven && !plan.CompleteLevelResourcesProven,
            "Source-issued detached allocation is not HQ, runtime, complete native resource or publication approval.");
        Check(plan.UpstreamPlanSha256 == fresh.PlanSha256 && plan.SourceHandles.SequenceEqual(new[] { dark.Handle, gnasty.Handle }),
            "Actual private upstream revision and ordered exact source handles bind both material owners.");
        Check(plan.NativeClip == 0x8000 && plan.AdditionalPacketBytesPerPass == 24,
            "Conditional native default clip and twoGT3 packet delta per pass, not whole-frame peak budget.");
        var allocation = plan.Allocation;
        Check(allocation.Allocations.Count == 31 && allocation.ContainedAliases.Count == 16 &&
            allocation.AllocatedByteCount == 28160 && allocation.FreeByteCount == 391008,
            "All real requests and sixteen source-contained Gnasty close subtiles fit issued ownership.");
        Check(allocation.Records.Select(r => r.RecordSha256).SequenceEqual(RecordGoldens),
            "Both complete relocated records agree with independently reencoded raw-source goldens.");
        Check(plan.UploadSha256 == UploadGolden && Hash(plan.CopyUploadPayload()) == UploadGolden &&
            plan.DataSha256 == DataGolden && Hash(plan.CopyDataComponents()) == DataGolden,
            "Whole actual upload/data outputs agree with raw-sector oracle, not a C#-generated receipt.");
        var neutralInputs = new[] { Input(dark, 0), Input(gnasty, 1) };
        var neutral = NativeStaticTerrainTextureAllocation.Compile(beforeUpload, fresh.UploadOwners, neutralInputs, 0x8000);
        Check(!neutral.SourceCertified && neutral.CopyUploadPayload().SequenceEqual(plan.CopyUploadPayload()) &&
            neutral.ComposeTwoFaceData(beforeData).SequenceEqual(plan.CopyDataComponents()),
            "The private issuer uses the exact same neutral byte/allocation implementation exercised by source-free tests.");
        AllocationCases.VerifyRecords(neutralInputs, allocation);
        byte[] mask = Mask(fresh.UploadOwners);
        Check(mask.Count(x => x != 0) == 105120 && beforeUpload.Take(0x80000).Where((b, i) => mask[i] != 0).Count(b => b == 0) == 17369,
            "All actual header/PETE/player/particle/companion owners remain protected, including17369 zero bytes.");
        AllocationCases.VerifyComplement(beforeUpload, plan.CopyUploadPayload(), mask, allocation.Allocations);
        foreach (var alias in allocation.ContainedAliases)
        {
            Check(alias.MaterialId == 1 && alias.RowByteLength == 16 && alias.RowCount == 16 &&
                alias.TargetOffset - alias.ParentTargetOffset == alias.SourceOffset - alias.ParentSourceOffset,
                "Every real contained close subtile retains its exact two-dimensional parent-relative byte offset.");
        }
        byte[] outputData = plan.CopyDataComponents();
        Check(outputData.Length == 0xA38 && beforeData.Length == 0x8C8 &&
            BinaryPrimitives.ReadUInt32LittleEndian(outputData) == 0x178 &&
            BinaryPrimitives.ReadUInt32LittleEndian(outputData.AsSpan(4)) == 2,
            "Actual component length/count and sector-rounded unchanged1000 data allocation.");
        for (int material = 0; material < 2; material++)
        {
            byte[] record = allocation.Records[material].CopyRecord();
            Check(outputData.AsSpan(8 + material * 16, 16).SequenceEqual(record.AsSpan(0, 16)) &&
                outputData.AsSpan(40 + material * 168, 168).SequenceEqual(record.AsSpan(16)), "Native split LQ/HQ metadata contains all184 bytes.");
        }
        Check(AllocationCases.Word(outputData, 0x208) == 0 && AllocationCases.Word(outputData, 0x218) == 1,
            "The two actual fresh HP triangles reference real material IDs0 and1.");
        for (int i = 8; i < beforeData.Length; i++)
            if (i is not (>= 0x98 and < 0x9C) and not (>= 0xA8 and < 0xAC))
                Check(beforeData[i] == outputData[i + 0x170], "Complete translated surface/collision/visibility/particle/sound tail complement.");
        Check(plan.CopyScene().SequenceEqual(beforeScene) && plan.CopyModelPayload().SequenceEqual(beforeModel) &&
            plan.CopyUploadPayload().AsSpan(0x80000).SequenceEqual(beforeUpload.AsSpan(0x80000)),
            "No placed scene, model/PETE/companion, native seven-list, or SPU mutation is hidden in texture composition.");
        plan.RequireExactOutputs(plan.CopyUploadPayload(), plan.CopyDataComponents());
        foreach (int at in new[] { 0, 32, 0x1000, 0x7FFFF, 0x80000, beforeUpload.Length - 1 })
        {
            byte[] bad = plan.CopyUploadPayload(); bad[at] ^= 1;
            Reject(() => plan.RequireExactOutputs(bad, plan.CopyDataComponents()), "Actual full output upload readback includes all protected/free/SPU bytes.");
        }
        foreach (int at in new[] { 0, 8, 39, 40, 0x177, 0x208, 0x218, outputData.Length - 1 })
        {
            byte[] bad = outputData.ToArray(); bad[at] ^= 1;
            Reject(() => plan.RequireExactOutputs(plan.CopyUploadPayload(), bad), "Actual full data readback includes descriptor/shifted tail complement.");
        }
        Reject(() => NativeId65FreshTerrainTexturePlan.CaptureAsync(original, fresh, gnasty, dark).GetAwaiter().GetResult(), "Fixed two-source recipe cannot be reversed by caller selection.");
        Reject(() => NativeId65FreshTerrainTexturePlan.CaptureAsync(original, fresh, dark, dark).GetAwaiter().GetResult(), "Caller cannot reuse one issuer as the other material source.");
        Reject(() => NativeId65FreshTerrainTexturePlan.CaptureAsync("unopened-missing-source.bin", null!, dark, gnasty).GetAwaiter().GetResult(), "Missing private fresh owner refuses before touching source path.");
        Reject(() => NativeId65FreshTerrainTexturePlan.CaptureAsync("unopened-missing-source.bin", fresh, null!, gnasty).GetAwaiter().GetResult(), "Missing private material source refuses before source path.");
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        bool wasCancelled = false;
        try { await NativeId65FreshTerrainTexturePlan.CaptureAsync(original, fresh, dark, gnasty, cancelled.Token); }
        catch (OperationCanceledException) { wasCancelled = true; }
        Check(wasCancelled, "Cancelled source capture does not leave a partially accepted plan.");
        var again = await NativeId65FreshTerrainTexturePlan.CaptureAsync(original, fresh, dark, gnasty);
        Check(again.PlanSha256 == plan.PlanSha256 && again.CopyUploadPayload().SequenceEqual(plan.CopyUploadPayload()), "Genuine recapture/retry is deterministic.");
        Check(fresh.CopyDataComponents().SequenceEqual(beforeData) && fresh.CopyUploadPayload().SequenceEqual(beforeUpload) &&
            fresh.CopyScene().SequenceEqual(beforeScene) && fresh.CopyModelPayload().SequenceEqual(beforeModel),
            "Original issued untextured object and every old output remain unchanged after all acceptance/refusal paths.");
    }

    private static NativeStaticTerrainTextureInput Input(NativeStaticTerrainTextureSource source, int material) =>
        new(material, source.Handle, "source-right-half-vram:wad-" + source.SourceWadEntry, AllocationCases.Policy,
            source.Layout.CopyRecord(), source.CopySourceVram());

    private static byte[] Mask(IReadOnlyList<NativeId65NormalResourceOwner> owners)
    {
        byte[] mask = Enumerable.Repeat((byte)255, 0x80000).ToArray();
        foreach (var owner in owners.Where(o => o.Offset < mask.Length))
        {
            Check(owner.Domain == NativeId65ResourceDomain.UploadPayload && owner.Offset + owner.ByteLength <= mask.Length,
                "Actual issued owner has a complete GPU domain, not a crossing SPU owner.");
            byte tag = owner.Provenance switch { AllocationCases.Free => 0, AllocationCases.Protected => 1,
                AllocationCases.Detached => 2, _ => throw new InvalidDataException("Unknown actual owner") };
            Check(mask.AsSpan(owner.Offset, owner.ByteLength).ToArray().All(x => x == 255), "Actual source owner partition is disjoint.");
            mask.AsSpan(owner.Offset, owner.ByteLength).Fill(tag);
        }
        Check(!mask.Contains((byte)255), "Actual source owner partition is exhaustive.");
        return mask;
    }
}
