using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Exporting;

public sealed record NativeLevelReplacementSafetyReport(
    string TargetLevelKey,
    int DestinationLevelId,
    HomeworldPortalControlDefinition Portal,
    int PlannedPatchCount,
    IReadOnlyList<string> ReadOnlyScopes,
    IReadOnlyList<string> Findings,
    bool SafeForByteIdenticalBaseline);

public sealed record NativeLevelReplacementBaselinePlan(
    NativeLevelReplacementManifest Manifest,
    NativeLevelReplacementSafetyReport Safety,
    IReadOnlyList<string> PlannedWrites);

public static class NativeLevelReplacementSafetyInspector
{
    private static readonly int[] ExpectedPortalTrueIndexes = [38, 144, 157];
    private static readonly int[] ExpectedReturnHomeTrueIndexes = [177, 179];

    public static NativeLevelReplacementSafetyReport Inspect(
        NativeLevelReplacementManifest manifest,
        LevelCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(catalog);
        if (manifest.Slot is null || manifest.Source is null)
            throw new InvalidDataException("The native level-replacement manifest is incomplete.");
        NativeLevelSlotContract expected = NativeLevelReplacementSupportCatalog.RequireSupported(
            catalog,
            manifest.Slot.TargetLevelKey);
        if (manifest.Slot != expected)
            throw new InvalidDataException("The native level-replacement slot identity changed.");

        HomeworldPortalControlDefinition portal =
            NativeLevelReplacementSupportCatalog.RequireStoneHillPortal();
        int[] storedPortalRows = manifest.Source.ArtisansPortalControlRows
            .Select(row => row.TrueIndex)
            .ToArray();
        int[] storedReturnHomeRows = manifest.Source.StoneHillReturnHomeRows
            .Select(row => row.TrueIndex)
            .ToArray();
        if (!storedPortalRows.SequenceEqual(ExpectedPortalTrueIndexes) ||
            manifest.Source.ArtisansPortalControlRows.Any(row =>
                !string.Equals(row.OwnerLevelKey, "artisans", StringComparison.Ordinal)))
        {
            throw new InvalidDataException(
                "The manifest does not preserve the exact Artisans Stone Hill portal rows T38/T144/T157.");
        }
        if (!storedReturnHomeRows.SequenceEqual(ExpectedReturnHomeTrueIndexes) ||
            manifest.Source.StoneHillReturnHomeRows.Any(row =>
                !string.Equals(row.OwnerLevelKey, "stonehill", StringComparison.Ordinal)))
        {
            throw new InvalidDataException(
                "The manifest does not preserve the exact Stone Hill Return Home group T177/T179.");
        }

        IReadOnlyList<string> readOnlyScopes =
        [
            $"{manifest.Source.Executable.Name} complete executable (level dispatch, save, and progression tables)",
            $"WAD entry {expected.MetadataWadDirectoryIndex} Stone Hill level-ID metadata/overlay package",
            $"WAD entry {expected.MetadataAdjacentWadDirectoryIndex} metadata-adjacent archive (relationship unresolved)",
            $"WAD entry {expected.LoadedDataPredecessorWadDirectoryIndex} loaded-entry predecessor package",
            "Artisans Stone Hill portal controls T38/T144/T157",
            "Stone Hill Return Home platform/helper T177/T179",
            "Normal Create BIN and every V4 retail-offset writer"
        ];
        IReadOnlyList<string> findings =
        [
            "The first V5 plan contains zero byte patches and is authorized only to prove a byte-identical BIN/CUE baseline.",
            "Stone Hill keeps retail level ID 11 and loaded data WAD entry 12; no new 36th level-table slot is created.",
            "Runtime promotion remains blocked until an edited replacement candidate passes DuckStation portal, gameplay, reload, Return Home, and save-progression checks."
        ];
        return new NativeLevelReplacementSafetyReport(
            expected.TargetLevelKey,
            expected.LevelId,
            portal,
            PlannedPatchCount: 0,
            readOnlyScopes,
            findings,
            SafeForByteIdenticalBaseline: true);
    }

    public static NativeLevelReplacementBaselinePlan BuildBaselinePlan(
        NativeLevelReplacementManifest manifest,
        LevelCatalog catalog)
    {
        NativeLevelReplacementSafetyReport safety = Inspect(manifest, catalog);
        if (!safety.SafeForByteIdenticalBaseline || safety.PlannedPatchCount != 0)
            throw new InvalidOperationException("The Stone Hill baseline must contain exactly zero planned writes.");
        return new NativeLevelReplacementBaselinePlan(
            manifest,
            safety,
            PlannedWrites: Array.Empty<string>());
    }
}
