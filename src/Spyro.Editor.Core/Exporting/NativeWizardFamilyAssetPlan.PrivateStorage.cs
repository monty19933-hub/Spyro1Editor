using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Exporting;

public sealed partial class NativeWizardFamilyAssetPlan
{
    internal NativeAddressSpace OriginalDataDomain => _source.Models.Original;
    internal NativeAddressSpace OriginalExecutableDomain => _source.OriginalExecutableDomain;
    internal byte[] ReadPrivateSource(NativeByteExtent extent) => _source.ReadPrivateSource(extent);
    internal void RequireNonSharedModelClass(ushort nativeClass) => _source.RequireNonSharedModelClass(nativeClass);

    /// <summary>
    /// Private storage schema over the existing actual original donor domain.
    /// The composer owns the supplied shared graph and its final-resource
    /// bindings. This is not a new family/behavior admission or a claim that
    /// all mutable globals, callbacks, persistence or runtime uses are closed.
    /// </summary>
    internal NativeMobyDependencyInstanceTemplate CapturePrivateStorageTemplate(NativeMobyDependencyClosurePlan shared)
    {
        ArgumentNullException.ThrowIfNull(shared);
        _source.RequirePrivateStorageSource();
        var rowOwner = Owners.Single(o => o.Id == "wizard.row");
        var privateOwner = Owners.Single(o => o.Id == "wizard.private");
        var row = new NativeMobyDependencyRead("wizard.row", new(OriginalDataDomain, 0x1D4B88, 0x58), rowOwner.Sha256);
        var properties = new NativeMobyDependencyRead("wizard.private", new(OriginalDataDomain, 0x1DAB50, 0x50), privateOwner.Sha256);
        if (!ReadPrivateSource(row.Extent).AsSpan().SequenceEqual(CopyOwner("wizard.row")) ||
            !ReadPrivateSource(properties.Extent).AsSpan().SequenceEqual(CopyOwner("wizard.private")))
            throw new InvalidDataException("Wizard private schema lost its complete existing source-asset owners.");
        return NativeMobyDependencyInstanceTemplate.Capture("source-assets-wizard33-t6-private-copy-v1",
            [new("wizard", 6, 0x11B, row, properties, 0x1CE800, [], false)], [], shared, ReadPrivateSource);
    }
}
