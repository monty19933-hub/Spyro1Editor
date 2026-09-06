namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Captures the existing fixed untextured A/N16/track19 research starter through
/// its production source issuers. This is recipe composition only: it neither
/// exports an authoring document nor publishes files or grants runtime approval.
/// </summary>
public static class NativeId65FreshStarterResearchFactory
{
    public static async Task<NativeId65FreshDiscResearchCandidatePlan> CaptureAsync(
        string originalBin, string sourceCue, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(originalBin);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceCue);
        string image = Path.GetFullPath(originalBin), cue = Path.GetFullPath(sourceCue);

        var surface = await NativeId65AuthoredSurfaceBundle.CaptureAsync(image,
            new(8448, 8448, 8704, 1024, 1024, 0x708050, 0x406080), token);
        var companion = await NativeId65CompanionAssetPlan.CaptureAsync(image, token);
        var resources = await NativeId65NormalResourceBundle.CaptureAsync(image, surface, companion, token);
        var lists = await NativeId65ZeroSceneAnimationLists.CaptureAsync(image, token);
        token.ThrowIfCancellationRequested();
        var chain = NativeId65NormalResourceChain.Compile(resources, lists);
        token.ThrowIfCancellationRequested();
        var scene = await NativeId65FreshSceneBundle.CaptureAsync(image, chain,
            new(8960, 8960, 9216, 64), 16, token);
        var staging = await NativeId65NormalUploadStaging.CaptureAsync(image, scene, token);
        var body = await NativeId65NormalBodyPlan.CaptureAsync(image, scene, staging, token);
        var disc = await NativeId65FreshDiscPlan.CaptureAsync(image, body, 19, token);
        var candidate = await NativeId65FreshDiscResearchCandidatePlan.CaptureAsync(image, cue, disc, token);
        token.ThrowIfCancellationRequested();
        return candidate;
    }
}
