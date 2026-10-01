using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Universal;

/// <summary>Reserves the actual source-proved import layout before a private terrain repack.
/// No actor is removed here. The later composer still verifies the prepared destination in full.</summary>
public static class UniversalTerrainReservations
{
    public static IReadOnlyList<NativeTexturePageOwnedRange> Capture(UniversalSourceLibrary library, LevelDefinition destination,
        IReadOnlyList<UniversalMobyPlacement> placements, Func<string,UniversalSpecialAsset?>? special = null,
        IReadOnlyCollection<int>? removedRows = null)
    {
        if (!placements.Any(p => !p.Retired)) return [];
        var import = UniversalMobyCatalogBuilder.DryRun(library,destination,placements,special,removedRows);
        if(import.Error!=null&&UniversalCapacityPlanner.IsCapacityFailure(import.Error))
        {
            // A retail-first layout may fail although the jointly repacked destination fits.
            // Preserve a proved earlier-import layout, with original native model ownership
            // retained during this reservation-only probe. Every actor remains in the final
            // composition; prefixes never authorize an omitted import or dependency.
            var native=library.Get(destination.Key);
            var gone=(removedRows??[]).ToHashSet();
            var dropped=UniversalMobyComposer.DroppableClasses(native.Data,native.Overlay,library.Disc.Exe,
                library.SharedClasses,gone,new HashSet<int>()).Classes;
            foreach(int cls in dropped)
            {
                int representative=Enumerable.Range(0,native.Data.Scene().RowCount)
                    .Last(row=>System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(native.Data.Row(row).AsSpan(0x36))==cls);
                gone.Remove(representative);
            }
            var ordered=placements.Any(p=>p.PersistentSlot!=null)?placements.OrderBy(p=>p.PersistentSlot).ToArray():placements.ToArray();
            for(int count=ordered.Length-1;count>0;count--)
            {
                import=UniversalMobyCatalogBuilder.DryRun(library,destination,ordered.Take(count).ToArray(),special,gone);
                if(import.Error==null)break;
                if(!UniversalCapacityPlanner.IsCapacityFailure(import.Error))return [];
            }
        }
        if (import.Error != null) return []; // The final exact composer reports this bounded refusal.
        var before = NativeTexturePageOwnershipScanner.Scan(library.ImagePath,destination);
        if (!before.AllConsumerClosureComplete)
            throw new InvalidDataException("Universal/private terrain layout cannot be reserved because a native texture ownership consumer is unproven.");
        var original = new bool[UniversalVramAllocator.ImageBytes];
        var imported = new bool[original.Length];
        foreach (var range in before.ProtectedRanges) Array.Fill(original,true,checked((int)range.Offset),range.Length);
        // Capture every source-validated pixel/CLUT reservation, including zero pixels and
        // textures reused in place. No second mesh parser can silently omit imported storage.
        foreach (var range in import.Result!.ImportedVramRanges) Array.Fill(imported,true,checked((int)range.Offset),range.Length);
        List<NativeTexturePageOwnedRange> ranges = [];
        for (int i = 0; i < imported.Length;)
        {
            if (!imported[i] || original[i]) { i++; continue; }
            int start = i++;
            while (i < imported.Length && imported[i] && !original[i]) i++;
            ranges.Add(new(start,i-start,"pending universal source-proved pixel/palette layout"));
        }
        return ranges;
    }
}
