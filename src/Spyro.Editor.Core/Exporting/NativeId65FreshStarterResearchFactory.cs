using Spyro.Editor.Core.Editing;

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

        var intent = NativeId65FreshDocumentIntent.Create(8448, 8448, 8704, 1024, 1024,
            8960, 8960, 64, new(80, 128, 112), new(128, 96, 64), 19);
        var compiled = await NativeId65FreshDocumentCompilation.CaptureAsync(image, intent, token);
        var candidate = await NativeId65FreshDiscResearchCandidatePlan.CaptureAsync(image, cue, compiled.DiscPlan, token);
        token.ThrowIfCancellationRequested();
        return candidate;
    }
}
