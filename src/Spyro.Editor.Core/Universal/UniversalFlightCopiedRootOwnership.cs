using System.Security.Cryptography;
namespace Spyro.Editor.Core.Universal;

/// <summary>Authoring refusal with technical source/root information retained for reports.</summary>
public sealed class UniversalFlightDependencyException : IOException
{
    public string SourceLevelKey { get; }
    public IReadOnlyList<int> RootClasses { get; }
    public UniversalFlightDependencyException(string sourceLevelKey, IEnumerable<int> rootClasses)
        : base("This Flight object needs its course timer and objectives. Cross-level placement is not ready for this object yet.")
    {
        SourceLevelKey = sourceLevelKey;
        RootClasses = Array.AsReadOnly(rootClasses.Distinct().Order().ToArray());
    }
}

/// <summary>Refuses copied Flight roots until source-owned spawn, self-transition and
/// destination course-context dependencies are issued. Native destination handlers
/// do not pass through this gate. Identity namespaces do not establish dependency ownership.</summary>
public static class UniversalFlightCopiedRootOwnership
{
    private static readonly HashSet<int> RootClasses = [0xA2, 0xA3, 0x134, 0x161, 0x154, 0x4E, 0x18D, 0x12B];
    private static readonly Dictionary<string,string> OriginalOwners = new(StringComparer.OrdinalIgnoreCase)
    {
        ["sunnyflight"] = "42b4e8a637ff91f87b884a9beea3266299d01ae36d36b437436d247f78812f59",
        ["nightflight"] = "3891e7af5f83825dbebc80952e173fbbea8125653d4b00fc7b1980a1128b873a",
        ["crystalflight"] = "e8d91a7c5f9f5d06b95ac01ade6fa4423485fd3d5dd75dd0dc15cc8319d46a59",
        ["wildflight"] = "5069a570d9cce769fed272d8c8fe24a56ade3317370fe5c688ebb43126570e97",
        ["icyflight"] = "67de47f547e65d4345401dedbb988b38c0de61648c903ab98d1a64f64fce9f0e"
    };

    public static void RequireCopiedSourceDependencyOwnership(string sourceKey, UniversalOverlay source, IReadOnlyList<int> classes)
    {
        int[] roots = classes.Where(RootClasses.Contains).Distinct().Order().ToArray();
        if (roots.Length == 0) return;
        bool namedOwner = OriginalOwners.ContainsKey(sourceKey);
        string hash = Convert.ToHexStringLower(SHA256.HashData(source.Bytes));
        var originalOwner = OriginalOwners.FirstOrDefault(p => p.Value == hash);
        if (!namedOwner && originalOwner.Key == null) return;
        string owner = namedOwner ? sourceKey.ToLowerInvariant() : originalOwner.Key;
        throw new UniversalFlightDependencyException(owner, roots);
    }
}
