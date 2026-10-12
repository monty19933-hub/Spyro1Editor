using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Exporting;

/// <summary>An original level's surface record that another original level can copy in: the level, its WAD entry, the
/// record's index in that level's special-surface table and its bytes.</summary>
public sealed record NativeTerrainSurfaceDescriptorDonor(
    NativeTerrainSurfaceSignature Signature,
    string LevelKey,
    string LevelName,
    int WadEntry,
    int Index,
    byte[] RawRecord)
{
    public NativeTerrainSurfaceDescriptorImport ToImport() =>
        new(Signature, RawRecord.ToArray(), LevelKey, Index, Convert.ToHexString(SHA256.HashData(RawRecord)));
}

/// <summary>
/// Ground kinds an original level can be given even when its own special-surface table does not have them (lava in
/// Artisans, water in Tree Tops…). These are the kinds the game's executable handles the same way in every level
/// (<see cref="AuthoredSurfaceKind.All"/>: water, lava and ooze floors that hurt or kill, supercharge, invisible wall);
/// every one of them is stored by some original level as exactly the record <see cref="CanonicalRecord"/> writes (a
/// type word, then for floors the two parameter words). Create BIN copies the record from that level and appends it to
/// the target's table (<see cref="NativeTerrainSurfaceLayoutComposer"/>); portals and level-linked kinds never travel.
/// </summary>
public static class NativeTerrainSurfaceKindImport
{
    public static IReadOnlyList<NativeTerrainSurfaceSignature> ImportableKinds { get; } =
        AuthoredSurfaceKind.All.Select(kind => new NativeTerrainSurfaceSignature(kind.Type, kind.Param1, kind.Param2)).ToArray();

    public static bool IsImportable(NativeTerrainSurfaceSignature signature) => ImportableKinds.Contains(signature);

    /// <summary>The record original levels store for this kind.</summary>
    public static byte[] CanonicalRecord(NativeTerrainSurfaceSignature signature) =>
        new AuthoredSurfaceKind(signature.SurfaceType, signature.Param1, signature.Param2).NativeRecord();

    /// <summary>True when <paramref name="record"/> is this kind exactly as original levels store it.</summary>
    public static bool IsCanonical(PortalSpecialSurfaceRecord record, NativeTerrainSurfaceSignature signature) =>
        record.Type == signature.SurfaceType && record.Param1 == signature.Param1 && record.Param2 == signature.Param2 &&
        record.RawBytes.AsSpan().SequenceEqual(CanonicalRecord(signature));

    /// <summary>The first level of <paramref name="levels"/> (in order) whose table holds the kind exactly as
    /// <see cref="CanonicalRecord"/>; null when none does or the kind cannot travel. <paramref name="locate"/> returns a
    /// level's decoded tables (null when it cannot be read; that level is skipped).</summary>
    public static NativeTerrainSurfaceDescriptorDonor? FindDonor(
        NativeTerrainSurfaceSignature signature,
        IEnumerable<LevelDefinition> levels,
        Func<LevelDefinition, NativeTerrainSurfaceSourceData?> locate)
    {
        if (!IsImportable(signature))
            return null;
        foreach (LevelDefinition level in levels)
        {
            if (level.SourceWadEntry < 0)
                continue;
            NativeTerrainSurfaceSourceData? source = locate(level);
            PortalSpecialSurfaceRecord? record = source?.SpecialSurfaces.FirstOrDefault(r => IsCanonical(r, signature));
            if (record != null)
                return new NativeTerrainSurfaceDescriptorDonor(signature, level.Key, level.DisplayName, level.SourceWadEntry, record.Index, record.RawBytes.ToArray());
        }
        return null;
    }

    /// <summary>The donor record a saved edit names, re-read from <paramref name="donorSource"/> (the donor level in the
    /// source image); null with a reason when it is gone or is not this kind exactly as original levels store it.</summary>
    public static NativeTerrainSurfaceDescriptorImport? ResolveSavedDonor(
        NativeTerrainSurfaceSignature signature,
        NativeTerrainSurfaceSourceData donorSource,
        int donorIndex,
        out string reason)
    {
        reason = "";
        if (!IsImportable(signature))
        {
            reason = $"{signature.Label} is not one of the kinds every level can use.";
            return null;
        }
        PortalSpecialSurfaceRecord? record = donorSource.SpecialSurfaces.FirstOrDefault(r => r.Index == donorIndex);
        if (record == null || !IsCanonical(record, signature))
        {
            reason = $"{donorSource.LevelName} no longer has {signature.Label} at surface {donorIndex}.";
            return null;
        }
        return new NativeTerrainSurfaceDescriptorDonor(signature, donorSource.LevelKey, donorSource.LevelName, -1, record.Index, record.RawBytes.ToArray()).ToImport();
    }
}
