using System.Security.Cryptography;
using Spyro.Editor.Core.Editing;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Actual immutable authoring intent bound to the existing original-source
/// producers. The canonical digest identifies intent, never native authority.
/// Existing resource/body/disc guards still apply: not every valid neutral
/// rectangle necessarily fits their current bounded compiled profile.
/// This class owns no CUE, filesystem writer, publisher, persistence or UI.
/// </summary>
public sealed class NativeId65FreshDocumentCompilation
{
    private readonly byte[] _canonical;
    private readonly NativeId65FreshSceneBundle _freshResources;

    private NativeId65FreshDocumentCompilation(NativeId65FreshDocumentIntent intent,
        byte[] canonical, NativeId65FreshDiscPlan disc, NativeId65FreshSceneBundle freshResources)
    {
        Intent = intent;
        _canonical = canonical;
        CanonicalSha256 = Convert.ToHexStringLower(SHA256.HashData(canonical));
        DiscPlan = disc;
        _freshResources = freshResources;
    }

    public NativeId65FreshDocumentIntent Intent { get; }
    public string CanonicalSha256 { get; }
    public NativeId65FreshDiscPlan DiscPlan { get; }
    public byte[] CopyCanonicalDocument() => _canonical.ToArray();
    internal NativeId65FreshSceneBundle FreshResourceAncestor => _freshResources;

    public static async Task<NativeId65FreshDocumentCompilation> CaptureAsync(string originalBin,
        NativeId65FreshDocumentIntent intent, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentException.ThrowIfNullOrWhiteSpace(originalBin);
        string image = Path.GetFullPath(originalBin);
        byte[] canonical = NativeId65FreshDocumentCodec.SerializeCanonical(intent);
        var floor = NativeId65AuthoredFloor.FromDisplayColors(intent.FloorX, intent.FloorY, intent.FloorZ,
            intent.FloorWidth, intent.FloorDepth, intent.FloorColor, intent.SkyColor);
        var surface = await NativeId65AuthoredSurfaceBundle.CaptureAsync(image, floor, token);
        var companion = await NativeId65CompanionAssetPlan.CaptureAsync(image, token);
        var resources = await NativeId65NormalResourceBundle.CaptureAsync(image, surface, companion, token);
        var lists = await NativeId65ZeroSceneAnimationLists.CaptureAsync(image, token);
        token.ThrowIfCancellationRequested();
        var chain = NativeId65NormalResourceChain.Compile(resources, lists);
        token.ThrowIfCancellationRequested();
        var scene = await NativeId65FreshSceneBundle.CaptureAsync(image, chain,
            new(intent.SpawnX, intent.SpawnY, intent.SpawnZ, intent.Heading),
            NativeId65FreshDocumentIntent.NominalDynamicCapacity, token);
        var staging = await NativeId65NormalUploadStaging.CaptureAsync(image, scene, token);
        var body = await NativeId65NormalBodyPlan.CaptureAsync(image, scene, staging, token);
        var disc = await NativeId65FreshDiscPlan.CaptureAsync(image, body, intent.MusicTrackId, token);
        token.ThrowIfCancellationRequested();
        return new(intent, canonical, disc, scene);
    }
}
