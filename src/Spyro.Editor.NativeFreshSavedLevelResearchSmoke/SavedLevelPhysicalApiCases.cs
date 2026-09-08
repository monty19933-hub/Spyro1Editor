using Spyro.Editor.Core.Exporting;
using static Cases;

// PRIVATE tests-first API/fold controls; no physical output in Free or Source.
internal static class SavedLevelPhysicalApiCases
{
    internal static async Task Free()
    {
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        Task<NativeId65FreshDiscResearchPublication> pending;
        try { pending = NativeFreshSavedLevelResearchCandidatePublisher.PublishForSmokeAsync(null!, "\0",
            NativeId65FreshResearchFault.None, null, null, null, canceled.Token); }
        catch { throw new InvalidDataException("New smoke API validation must fault its Task asynchronously."); }
        await Throws<ArgumentNullException>(() => pending);
        await Throws<ArgumentNullException>(() => NativeId65FreshResearchPublicationEngine.PublishSavedLevelForSmokeAsync(
            null!, "\0", (NativeId65FreshResearchFault)999, null, null, null, canceled.Token));
        Check(Enum.GetNames<NativeFreshSavedLevelResearchPoint>().SequenceEqual(
            new[] { "AfterReturnBeforeGuardComplete", "AfterFinalArtifactAttempt" }), "Exact two actual temporal points.");
        var first = new IOException("neutral readback accounting object");
        var second = new InvalidDataException("neutral notification accounting object");
        NativeId65FreshResearchPublicationEngine.ThrowSavedLevelArtifactFailures(null, null);
        var a = await Throws<IOException>(() => { NativeId65FreshResearchPublicationEngine.ThrowSavedLevelArtifactFailures(first, null); return Task.CompletedTask; });
        var b = await Throws<InvalidDataException>(() => { NativeId65FreshResearchPublicationEngine.ThrowSavedLevelArtifactFailures(null, second); return Task.CompletedTask; });
        Check(ReferenceEquals(a, first) && ReferenceEquals(b, second), "Single exact object retained; no physical readback simulated.");
        var both = await Throws<IOException>(() => { NativeId65FreshResearchPublicationEngine.ThrowSavedLevelArtifactFailures(first, second); return Task.CompletedTask; });
        Check(Leaves(both).SequenceEqual(new Exception[] { first, second }), "Complete ordered error fold only, not VerifySet branch coverage.");
    }
    internal static async Task Source(string bin, string cue, NativeFreshSavedLevelResearchCandidatePlan actual)
    {
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        foreach (var fault in new[] { NativeId65FreshResearchFault.None, (NativeId65FreshResearchFault)(-1), (NativeId65FreshResearchFault)999 })
            await Throws<InvalidDataException>(() => NativeFreshSavedLevelResearchCandidatePublisher.PublishForSmokeAsync(
                actual, "\0", fault, null, null, null, canceled.Token));
        await Throws<InvalidDataException>(() => NativeFreshSavedLevelResearchCandidatePublisher.PublishForSmokeAsync(
            actual, "\0", NativeId65FreshResearchFault.None, (_, _) => throw new Exception("must not call"), null, null, canceled.Token));
        var error = await Throws<OperationCanceledException>(() => NativeFreshSavedLevelResearchCandidatePublisher.PublishForSmokeAsync(
            actual, "\0", NativeId65FreshResearchFault.AfterFirstSector, null, null, null, canceled.Token));
        Check(error.CancellationToken == canceled.Token, "Actual binding pre-cancel before any unopened NUL path.");
        // Actual fixed-a role, separately issued with its required old adapter.
        // Selected-profile equality is deliberately NOT substituted as authority.
        var material = actual.MaterialCompilation;
        var texture = await NativeId65FreshTerrainTexturePlan.CaptureAsync(bin,
            material.Texture.FreshAncestor, material.Material0, material.Material1);
        var staging = await NativeId65NormalUploadStaging.CaptureTexturedAsync(bin, texture);
        var body = await NativeId65NormalBodyPlan.CaptureTexturedAsync(bin, texture, staging);
        var disc = await NativeId65FreshDiscPlan.CaptureTexturedAsync(bin, body, 19);
        var old = await NativeId65FreshTexturedResearchCandidatePlan.CaptureAsync(bin, cue, disc);
        await Throws<InvalidDataException>(() => NativeId65FreshResearchPublicationEngine.PublishSavedLevelForSmokeAsync(
            NativeId65FreshResearchPublicationBinding.FromTextured(old), "\0",
            NativeId65FreshResearchFault.AfterFirstSector, null, null, null, canceled.Token));
        Console.WriteLine("SAVED_LEVEL_PHYSICAL_API_SOURCE_OK actualOldRoleRefused=true nativeOutput=false");
    }
}
