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
    internal IReadOnlyList<NativeMobyDependencyPrivateSchema> CapturePrivateStorageRows()
    {
        _source.RequirePrivateStorageSource();
        var rowOwner = Owners.Single(o => o.Id == "wizard.row");
        var privateOwner = Owners.Single(o => o.Id == "wizard.private");
        var row = new NativeMobyDependencyRead("wizard.row", new(OriginalDataDomain, 0x1D4B88, 0x58), rowOwner.Sha256);
        var properties = new NativeMobyDependencyRead("wizard.private", new(OriginalDataDomain, 0x1DAB50, 0x50), privateOwner.Sha256);
        if (!ReadPrivateSource(row.Extent).AsSpan().SequenceEqual(CopyOwner("wizard.row")) ||
            !ReadPrivateSource(properties.Extent).AsSpan().SequenceEqual(CopyOwner("wizard.private")))
            throw new InvalidDataException("Wizard private schema lost its complete existing source-asset owners.");
        return Array.AsReadOnly(new[]{new NativeMobyDependencyPrivateSchema("wizard",6,0x11B,row,properties,0x1CE800,
            Array.AsReadOnly(Array.Empty<NativeMobyDependencyOwnerField>()),false)});
    }

    internal IReadOnlyList<NativeFreshMobyScenePointerReference> CapturePrivateStorageInternalPointers()
    {
        var row=CapturePrivateStorageRows().Single();
        // The original route header points to its own complete route at +0x28.
        // Check the actual retained field, not a caller-supplied pointer label.
        const int targetOffset=0x28;
        byte[] properties=ReadPrivateSource(row.Properties.Extent);
        uint before=System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(properties);
        uint target=checked((uint)(row.Properties.Extent.Offset+targetOffset-row.PropertiesPointerBaseOffset));
        if(properties.Length<targetOffset+4||before!=target)
            throw new InvalidDataException("Wizard original private route pointer lost its actual source field or owned target.");
        return Array.AsReadOnly(new[]{new NativeFreshMobyScenePointerReference(0,NativeFreshMobySceneOwnerKind.Private,0,
            0,NativeFreshMobySceneOwnerKind.Private,targetOffset,before)});
    }

    internal NativeMobyDependencyInstanceTemplate CapturePrivateStorageTemplate(NativeMobyDependencyClosurePlan shared)
    {
        ArgumentNullException.ThrowIfNull(shared);
        return NativeMobyDependencyInstanceTemplate.Capture("source-assets-wizard33-t6-private-copy-v1",
            CapturePrivateStorageRows(),[],shared,ReadPrivateSource);
    }
}
