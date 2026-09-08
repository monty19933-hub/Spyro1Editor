using System.Reflection;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
using static Program;

internal static class SelectedBindingCases
{
    // Captured with the old built Core DLL before any production edit.
    private const string LegacyPlan = "d8db59945995b5d78fda18f509572c3c31dc4fd60f4ca9d2df782669662d6e34";
    private const string Original = "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37";
    private const string Unopened = "selected-binding-must-not-open.bin";
    internal static async Task Free()
    {
        var type = typeof(NativeId65FreshTerrainTexturePlan);
        Check(type.GetProperty(nameof(NativeId65FreshTerrainTexturePlan.MaterialSources))!.PropertyType ==
            typeof(IReadOnlyList<NativeStaticTerrainTextureSource>), "Selected material owners are typed and read-only.");
        Check(type.GetProperty(nameof(NativeId65FreshTerrainTexturePlan.BindingProfileId))!.SetMethod is null,
            "A caller cannot change the binding profile.");
        Check(type.GetMethod("RequireAncestry", BindingFlags.Instance | BindingFlags.NonPublic) is not null,
            "Closed composition retains an actual-owner ancestry guard.");
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Task<NativeId65FreshTerrainTexturePlan>? task = null;
        try { task = NativeId65FreshTerrainTexturePlan.CaptureSelectedAsync("unopened-missing-source.bin", null!, null!, null!, cancelled.Token); }
        catch { throw new InvalidDataException("Selected async validation must fault the returned Task, not throw synchronously."); }
        try { await task; throw new InvalidDataException("Expected null-owner refusal."); }
        catch (ArgumentNullException e) { Check(e.ParamName == "fresh", "Actual fresh owner is checked before cancellation and I/O."); }
    }

    internal static async Task Source(string path)
    {
        using var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var failures = new List<Exception>();
        try { RequireOriginal(held); await SourceCore(path); }
        catch (Exception e) { failures.Add(e); }
        finally
        {
            try { RequireOriginal(held); }
            catch (Exception e) { failures.Add(e); }
        }
        if (failures.Count != 0) throw new AggregateException("Selected-source capture/final identity failures.", failures);
        Console.WriteLine("SELECTED_TEXTURE_SOURCE_ORIGINAL_FINAL_OK");
    }

    private static async Task SourceCore(string path)
    {
        var surface = await NativeId65AuthoredSurfaceBundle.CaptureAsync(path,
            new(8448, 8448, 8704, 1024, 1024, 0x708050, 0x406080));
        var companion = await NativeId65CompanionAssetPlan.CaptureAsync(path);
        var resources = await NativeId65NormalResourceBundle.CaptureAsync(path, surface, companion);
        var lists = await NativeId65ZeroSceneAnimationLists.CaptureAsync(path);
        var chain = NativeId65NormalResourceChain.Compile(resources, lists);
        var fresh = await NativeId65FreshSceneBundle.CaptureAsync(path, chain, new(8960, 8960, 9216, 64), 16);
        var dark = NativeStaticTerrainTextureSource.Capture(path, 14, 0);
        var gnasty = NativeStaticTerrainTextureSource.Capture(path, 70, 0);
        var old = await NativeId65FreshTerrainTexturePlan.CaptureAsync(path, fresh, dark, gnasty);
        Check(old.PlanSha256 == LegacyPlan && old.BindingProfileId == NativeId65FreshTerrainTexturePlan.ProfileId &&
            old.UploadSha256 == SourceCases.UploadGolden && old.DataSha256 == SourceCases.DataGolden,
            "Old fixed factory retains the pre-edit serialized PlanSHA and every old byte golden.");
        var selectedOld = await NativeId65FreshTerrainTexturePlan.CaptureSelectedAsync(path, fresh, dark, gnasty);
        Check(selectedOld.BindingProfileId == NativeId65FreshTerrainTexturePlan.SelectedBindingProfileId &&
            selectedOld.BindingProfileId != old.BindingProfileId && selectedOld.PlanSha256 != old.PlanSha256 &&
            selectedOld.CopyUploadPayload().SequenceEqual(old.CopyUploadPayload()) &&
            selectedOld.CopyDataComponents().SequenceEqual(old.CopyDataComponents()) &&
            selectedOld.Evidence.SequenceEqual(old.Evidence),
            "New selected binding domain differs while the actual legacy pair uses identical allocator and consumer bytes.");
        VerifyOwners(old, fresh, dark, gnasty);
        VerifyOwners(selectedOld, fresh, dark, gnasty);

        var first = NativeStaticTerrainTextureSource.Capture(path, 14, 1);
        var second = NativeStaticTerrainTextureSource.Capture(path, 70, 1);
        // Independent raw-sector source capture established these before the new
        // factory; neither donor is one of the old exact T0 pair.
        string[] records = ["7bf72a8271c6cd6f32dad0293c80f825fb2a7d6eccfa6a056622cf406a27d276",
            "2b35495f55799c05ffe003d1265f94fffc70a9f18e33f3b1814f2f70570a1da9"];
        string[] pixels = ["899faabc51e29f5ab4394db639f79ed5f80ed3ad88114985b1c4811342a9b58a",
            "0ba2928627f53497ebc4cf384d9fdf8e829e35801e1071a6be18b64abe98a7a5"];
        string[] palettes = ["1b50c6a0d8d062f6e3aed1cbc8548a219c676f85e3fffa883aa12630886bc126",
            "096831a1943cfda398a8de798df283ca2d71ee53a8f86dfabc89582c24bc5a0e"];
        NativeStaticTerrainTextureSource[] sources = [first, second];
        for (int i = 0; i < sources.Length; i++)
        {
            var source = sources[i]; byte[] vram = source.CopySourceVram();
            Check(source.SourceTextureId == 1 && source.Layout.RecordSha256 == records[i] &&
                Hash(source.Layout.Descriptors[0].CopyPixels(vram)) == pixels[i] &&
                Hash(source.Layout.Descriptors[0].CopyPalette(vram)) == palettes[i] &&
                source.NativeAnimationControlCount == 0 && source.NativeScrollingControlCount == 0,
                "Actual alternative full record, LQ indexed pixels/CLUT and source runtime-control eligibility agree independently.");
        }
        Check(first.Handle != second.Handle && first.SourceTextureId == second.SourceTextureId,
            "Equal native texture IDs in two source levels do not alias source variants.");
        var selected = await NativeId65FreshTerrainTexturePlan.CaptureSelectedAsync(path, fresh, first, second);
        var reversed = await NativeId65FreshTerrainTexturePlan.CaptureSelectedAsync(path, fresh, second, first);
        var same = await NativeId65FreshTerrainTexturePlan.CaptureSelectedAsync(path, fresh, first, first);
        var again = await NativeId65FreshTerrainTexturePlan.CaptureSelectedAsync(path, fresh, first, second);
        Verify(selected, fresh, first, second);
        Verify(reversed, fresh, second, first);
        Verify(same, fresh, first, first);
        Check(selected.UploadSha256 != old.UploadSha256 && selected.DataSha256 != old.DataSha256 &&
            reversed.PlanSha256 != selected.PlanSha256 && reversed.UploadSha256 != selected.UploadSha256 &&
            same.PlanSha256 != selected.PlanSha256 && same.MaterialSources.Count == 2 &&
            ReferenceEquals(same.MaterialSources[0], same.MaterialSources[1]),
            "Alternative, swapped, and same-source selections retain two ordered actual material owners.");
        Check(same.Allocation.Allocations.Count(a => a.MaterialId == 0) == same.Allocation.Allocations.Count(a => a.MaterialId == 1) &&
            same.Allocation.Allocations.Where(a => a.MaterialId == 0).Select(a => a.TargetOffset).Intersect(
                same.Allocation.Allocations.Where(a => a.MaterialId == 1).Select(a => a.TargetOffset)).Count() == 0,
            "Same source twice remains two separate material allocations, without a new cross-material deduplication rule.");
        Check(again.PlanSha256 == selected.PlanSha256 && again.CopyUploadPayload().SequenceEqual(selected.CopyUploadPayload()) &&
            again.CopyDataComponents().SequenceEqual(selected.CopyDataComponents()), "Selected genuine retry is deterministic.");
        var reopened = NativeStaticTerrainTextureSource.Capture(path, 14, 1);
        var freshAgain = await NativeId65FreshSceneBundle.CaptureAsync(path, chain, new(8960, 8960, 9216, 64), 16);
        Check(reopened.Handle == first.Handle && freshAgain.PlanSha256 == fresh.PlanSha256,
            "Equal-byte different actual-source/fresh instances are genuine adversarial ancestry fixtures.");
        Reject(() => Ancestry(selected, fresh, reopened, second), "Equal handle/hash cannot replace retained material0 issuer.");
        Reject(() => Ancestry(selected, fresh, first, first), "Material1 cannot be substituted by another valid issuer.");
        Reject(() => Ancestry(selected, freshAgain, first, second), "Equal fresh PlanSHA cannot replace retained actual ancestor.");
        Reject(() => Ancestry(selected, fresh, second, first), "Reversed owners do not satisfy ordered ancestry.");
        Reject(() => NativeId65FreshTerrainTexturePlan.CaptureAsync(Unopened, fresh, first, second).GetAwaiter().GetResult(),
            "Legacy fixed factory still refuses alternative donors before source I/O.");
        Reject(() => NativeId65FreshTerrainTexturePlan.CaptureAsync(Unopened, fresh, dark, dark).GetAwaiter().GetResult(),
            "Legacy fixed same-source refusal is unchanged.");
        await NullOwner(() => NativeId65FreshTerrainTexturePlan.CaptureSelectedAsync(Unopened, fresh, null!, second), "material0");
        await NullOwner(() => NativeId65FreshTerrainTexturePlan.CaptureSelectedAsync(Unopened, fresh, first, null!), "material1");
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        try
        {
            await NativeId65FreshTerrainTexturePlan.CaptureSelectedAsync(Unopened, fresh, first, second, cancelled.Token);
            throw new InvalidDataException("Expected cancellation before I/O.");
        }
        catch (OperationCanceledException e) { Check(e.CancellationToken == cancelled.Token, "Actual token is forwarded and no source open precedes pre-cancellation."); }
        Reject(() => NativeId65FreshTerrainTexturePlan.CaptureSelectedAsync(typeof(SelectedBindingCases).Assembly.Location,
            fresh, first, second).GetAwaiter().GetResult(), "Valid private inputs cannot authorize a wrong original file.");
        Reject(() => NativeStaticTerrainTextureSource.Capture(path, 16, 1), "Actual Town scrolling source still refuses before selected binding.");
        Reject(() => NativeStaticTerrainTextureSource.Capture(path, 40, 70), "Actual animated source still refuses before selected binding.");
        Reject(() => NativeStaticTerrainTextureSource.Capture(path, 70, 12), "Unsupported actual rotated record remains excluded.");
        var stage = await NativeId65NormalUploadStaging.CaptureTexturedAsync(path, selected);
        var body = await NativeId65NormalBodyPlan.CaptureTexturedAsync(path, selected, stage);
        Check(stage.UpstreamPlanSha256 == selected.PlanSha256 && body.UpstreamPlanSha256 == selected.PlanSha256 &&
            body.ResourceProfile == NativeId65NormalResourceProfile.DirectLqTwoStaticMaterials &&
            body.CopyLevelData().AsSpan(0x800, 0xDE000).SequenceEqual(selected.CopyUploadPayload()) &&
            body.CopyLevelData().AsSpan(0xDE800, 0xA38).SequenceEqual(selected.CopyDataComponents()),
            "Existing actual textured staging/body path consumes the same selected immutable shape, not a duplicate writer.");
        // Existing body/staging pairing compares privately issued profile,
        // PlanSHA and uploadSHA, not the texture object reference. Preserve it.
        var equivalentBody = await NativeId65NormalBodyPlan.CaptureTexturedAsync(path, again, stage);
        Check(equivalentBody.PlanSha256 == body.PlanSha256 && equivalentBody.CopyLevelData().SequenceEqual(body.CopyLevelData()),
            "Existing body accepts a genuinely reissued equivalent private texture plan with identical complete output.");
        Reject(() => NativeId65NormalBodyPlan.CaptureTexturedAsync(Unopened, reversed, stage).GetAwaiter().GetResult(),
            "Different actual selected material order cannot pair with old staging despite equal transfer bytes.");
        Reject(() => NativeId65FreshDiscPlan.CaptureAsync(Unopened, body, 19).GetAwaiter().GetResult(),
            "Actual selected textured body remains refused by existing untextured Disc issuer before I/O.");
        Check((await NativeId65FreshTerrainTexturePlan.CaptureAsync(path, fresh, dark, gnasty)).PlanSha256 == LegacyPlan,
            "All selected successes/refusals leave the old exact factory deterministic.");
        Console.WriteLine($"SELECTED_TEXTURE legacyPlan={old.PlanSha256} selectedPlan={selected.PlanSha256} upload={selected.UploadSha256} data={selected.DataSha256}");
    }

    private static void Verify(NativeId65FreshTerrainTexturePlan plan, NativeId65FreshSceneBundle fresh,
        NativeStaticTerrainTextureSource first, NativeStaticTerrainTextureSource second)
    {
        VerifyOwners(plan, fresh, first, second);
        NativeStaticTerrainTextureInput Input(NativeStaticTerrainTextureSource s, int id) =>
            new(id, s.Handle, "source-right-half-vram:wad-" + s.SourceWadEntry, AllocationCases.Policy,
                s.Layout.CopyRecord(), s.CopySourceVram());
        var inputs = new[] { Input(first, 0), Input(second, 1) };
        AllocationCases.VerifyRecords(inputs, plan.Allocation);
        var neutral = NativeStaticTerrainTextureAllocation.Compile(fresh.CopyUploadPayload(), fresh.UploadOwners, inputs, 0x8000);
        Check(neutral.CopyUploadPayload().SequenceEqual(plan.CopyUploadPayload()) &&
            neutral.ComposeTwoFaceData(fresh.CopyDataComponents()).SequenceEqual(plan.CopyDataComponents()),
            "Selected source binding reuses the existing complete neutral allocator/data composer exactly.");
        byte[] mask = Enumerable.Repeat((byte)255, 0x80000).ToArray();
        foreach (var owner in fresh.UploadOwners.Where(o => o.Offset < mask.Length))
            mask.AsSpan(owner.Offset, owner.ByteLength).Fill(owner.Provenance == AllocationCases.Free ? (byte)0 : (byte)1);
        Check(!mask.Contains((byte)255), "Actual GPU owners are exhaustive before complement verification.");
        AllocationCases.VerifyComplement(fresh.CopyUploadPayload(), plan.CopyUploadPayload(), mask, plan.Allocation.Allocations);
        Check(plan.CopyScene().SequenceEqual(fresh.CopyScene()) && plan.CopyModelPayload().SequenceEqual(fresh.CopyModelPayload()) &&
            plan.CopyUploadPayload().AsSpan(0x80000).SequenceEqual(fresh.CopyUploadPayload().AsSpan(0x80000)) &&
            !plan.RuntimeVerified && !plan.HqRuntimeProven && !plan.CompleteLevelResourcesProven && !plan.PublicationApproved,
            "Scene/model/SPU and every approval boundary remain unchanged by selected art.");
        byte[] copy = plan.CopyUploadPayload(); copy[0] ^= 1;
        Reject(() => plan.RequireExactOutputs(copy, plan.CopyDataComponents()), "Detached mutated copy cannot replace accepted output.");
        plan.RequireExactOutputs(plan.CopyUploadPayload(), plan.CopyDataComponents());
    }

    private static void VerifyOwners(NativeId65FreshTerrainTexturePlan plan, NativeId65FreshSceneBundle fresh,
        NativeStaticTerrainTextureSource first, NativeStaticTerrainTextureSource second)
    {
        Check(plan.MaterialSources.Count == 2 && ReferenceEquals(plan.MaterialSources[0], first) &&
            ReferenceEquals(plan.MaterialSources[1], second) && plan.SourceHandles.SequenceEqual(new[] { first.Handle, second.Handle }),
            "Ordered original object references, not reconstructed receipts, carry source ancestry.");
        Check(plan.MaterialSources is not NativeStaticTerrainTextureSource[] &&
            plan.MaterialSources is IList<NativeStaticTerrainTextureSource> list && list.IsReadOnly,
            "MaterialSources does not expose a mutable backing array.");
        try { ((IList<NativeStaticTerrainTextureSource>)plan.MaterialSources)[0] = second; throw new InvalidDataException("Mutable owner collection."); }
        catch (NotSupportedException) { Check(true, "Defensive ordered owner collection refuses replacement."); }
        Ancestry(plan, fresh, first, second);
    }

    private static void Ancestry(NativeId65FreshTerrainTexturePlan plan, NativeId65FreshSceneBundle fresh,
        NativeStaticTerrainTextureSource first, NativeStaticTerrainTextureSource second)
    {
        try { typeof(NativeId65FreshTerrainTexturePlan).GetMethod("RequireAncestry", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(plan, new object[] { fresh, first, second }); }
        catch (TargetInvocationException e) when (e.InnerException is InvalidDataException) { throw e.InnerException; }
    }
    private static async Task NullOwner(Func<Task<NativeId65FreshTerrainTexturePlan>> action, string parameter)
    {
        try { await action(); throw new InvalidDataException("Expected null-owner refusal."); }
        catch (ArgumentNullException e) { Check(e.ParamName == parameter, "Specific missing issuer refuses before source I/O."); }
    }
    private static void RequireOriginal(FileStream image)
    {
        Check(image.Length == 661547040, "Held original has exact initial length."); image.Position = 0;
        Check(Convert.ToHexStringLower(SHA256.HashData(image)) == Original && image.Position == 661547040 &&
            image.Length == 661547040 && image.ReadByte() == -1, "Held original full SHA, final length, position and true EOF are unchanged.");
    }
}
