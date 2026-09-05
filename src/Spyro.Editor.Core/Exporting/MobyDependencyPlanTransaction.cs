using System.Globalization;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Keeps one object's dependency writes and allocation reservations provisional
/// until its source row/instance has also been accepted. A rejected object must
/// not install a model or activate a helper merely because its package was valid.
/// Earlier successful objects can continue sharing their committed allocations.
/// </summary>
internal sealed class MobyDependencyPlanTransaction
{
    private readonly List<MobySourcePatch> _patches;
    private readonly List<MobyActorPackageImportPreview> _previews;
    private readonly HashSet<long> _writtenOffsets;
    private readonly HashSet<string> _writtenRecipes;
    private readonly Dictionary<string, CrossLevelSharedSpecialCluster> _sharedClusters;
    private readonly int _patchStart;
    private readonly int _previewStart;
    private readonly HashSet<string> _priorRecipes;
    private readonly HashSet<string> _priorClusterKeys;
    private bool _committed;

    internal MobyDependencyPlanTransaction(
        List<MobySourcePatch> patches,
        List<MobyActorPackageImportPreview> previews,
        HashSet<long> writtenOffsets,
        HashSet<string> writtenRecipes,
        Dictionary<string, CrossLevelSharedSpecialCluster> sharedClusters)
    {
        _patches = patches;
        _previews = previews;
        _writtenOffsets = writtenOffsets;
        _writtenRecipes = writtenRecipes;
        _sharedClusters = sharedClusters;
        _patchStart = patches.Count;
        _previewStart = previews.Count;
        _priorRecipes = new HashSet<string>(writtenRecipes, writtenRecipes.Comparer);
        _priorClusterKeys = new HashSet<string>(sharedClusters.Keys, sharedClusters.Comparer);
    }

    internal void Commit() => _committed = true;

    internal void RollbackUnlessCommitted()
    {
        if (_committed) return;

        foreach (MobySourcePatch patch in _patches.Skip(_patchStart))
        {
            if (!patch.WadRelativeOffset.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ||
                !long.TryParse(patch.WadRelativeOffset.AsSpan(2), NumberStyles.AllowHexSpecifier,
                    CultureInfo.InvariantCulture, out long offset))
            {
                continue;
            }
            for (long end = checked(offset + patch.ByteLength); offset < end; offset++)
                _writtenOffsets.Remove(offset);
        }
        _patches.RemoveRange(_patchStart, _patches.Count - _patchStart);
        _writtenRecipes.RemoveWhere(recipe => !_priorRecipes.Contains(recipe));
        foreach (string key in _sharedClusters.Keys.Where(key => !_priorClusterKeys.Contains(key)).ToArray())
            _sharedClusters.Remove(key);

        // Retain diagnostics, but rejected owner previews must not trigger the
        // later package-dependent helper pass or become runtime-test evidence.
        for (int index = _previewStart; index < _previews.Count; index++)
        {
            if (_previews[index].CanWriteImage)
                _previews[index] = _previews[index] with
                {
                    CanWriteImage = false,
                    GuardReason = "Package installation was rolled back because the owning Moby edit was not accepted. " + _previews[index].GuardReason
                };
        }
    }
}
