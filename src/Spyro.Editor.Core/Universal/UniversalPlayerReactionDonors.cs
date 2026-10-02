using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.Core.Universal;

/// <summary>Exact native Spyro reaction IDs absent from the original Terrace stream.
/// The source-issued player table owns these IDs independently of an enemy model.
/// This narrow certificate supplies original byte-identical reaction records; it
/// does not authorize another animation ID, reinterpret a mask or prove gameplay.</summary>
public static class UniversalPlayerReactionDonors
{
    public static int? ResolveMissingDonor(string sourceKey, int sourceLevelId, int animationId,
        NativePlayerAnimationSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!string.Equals(sourceKey, "terracevillage", StringComparison.OrdinalIgnoreCase) ||
            sourceLevelId != 41 || animationId is not (26 or 32)) return null;
        var owner = source.Levels.Single(l => l.LevelId == 41);
        if (source.SourceImageSha256 != NativePlayerAnimationSource.OriginalImageSha256 ||
            owner.DataSha256 != "26bd02231f93d780ee75b528aac8e5797fbb0b578acfb2ba21e692872e6b5c5e" ||
            owner.Stream.Sha256 != "28d38e25cd338910063ea90160eb71d06dafaf77c6541d34dc07c0d89dbc2aa7" ||
            owner.Stream.Records.Any(r => r.AnimationId == animationId) || source.SharedAnimationIds.Contains(animationId))
            throw new InvalidDataException("Terrace reaction fallback differs from its original missing-stream owner.");
        var donor = source.Levels.Single(l => l.LevelId == 12);
        if (donor.DataSha256 != "a937aecb3626ffd2311ba136684a34c9f36877054b52557b4524fcacee0e2d97" ||
            donor.Stream.Sha256 != "90e32aec6d24572225363810b76965a857173e4ccbfc97c4abf8685b57f3607e")
            throw new InvalidDataException("Spyro reaction donor differs from original Dark Hollow ownership.");
        string hash = animationId == 26 ? "da884b15ce397011fbea4cb13063da210a7babb568e54fd7d4f2d044a24a98cd" :
            "bef36e69a8e1f37e22d5ad55049637894136ab9cfcf3be58141676f57e09927c";
        int length = animationId == 26 ? 5784 : 19340, frames = animationId == 26 ? 12 : 36;
        var texture = source.Levels.Single(l => l.LevelId == 10).Stream.Records.Single(r => r.AnimationId == 18).CopyRecord();
        int[] nativeDonors = [12, 14, 21, 23, 24, 31, 33, 50, 51, 53, 61, 63];
        var supplied = source.Levels.Where(l => l.Stream.Records.Any(r => r.AnimationId == animationId)).Select(l => l.LevelId).Order().ToArray();
        if (!supplied.SequenceEqual(nativeDonors))
            throw new InvalidDataException("The complete native Spyro reaction donor population differs from its certificate.");
        foreach (int level in nativeDonors)
        {
            var record = source.Levels.Single(l => l.LevelId == level).Stream.Records.Single(r => r.AnimationId == animationId);
            byte[] bytes = record.CopyRecord();
            if (record.Sha256 != hash || Convert.ToHexStringLower(SHA256.HashData(bytes)) != hash ||
                record.ByteLength != length || record.FrameCount != frames ||
                record.Frames.Any(f => f.UpperByte != 0xFF) ||
                !bytes.AsSpan(0x0A, 0x22).SequenceEqual(texture.AsSpan(0x0A, 0x22)))
                throw new InvalidDataException("Native Spyro reaction ID has a changed record, selector, geometry or texture header.");
        }
        return 12;
    }
}
