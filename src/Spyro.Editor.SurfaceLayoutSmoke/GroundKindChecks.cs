using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.SurfaceLayoutSmoke;

/// <summary>Ground kinds an original level never had (Tile Type… and the Ground card): the records copied in, the saved
/// edit that names them, and the check of an exported BIN.</summary>
internal static class GroundKindChecks
{
    private static int _checks;

    private static void Check(bool condition, string message)
    {
        _checks++;
        if (!condition)
            throw new InvalidOperationException(message);
    }

    /// <summary>Source-free: every kind every level can use is appended from its canonical record to a table that lacks
    /// it and reparses on the assigned triangle; a saved donor resolves only when the donor still holds that exact
    /// record; portals and level-linked kinds never travel; the saved edit keeps its donor; own-tile kinds are told from
    /// copied ones.</summary>
    public static int RunSourceFree()
    {
        _checks = 0;
        Check(NativeTerrainSurfaceKindImport.ImportableKinds.Count == 8, "Eight kinds can be copied into any level.");
        foreach (NativeTerrainSurfaceSignature kind in NativeTerrainSurfaceKindImport.ImportableKinds)
        {
            byte[] canonical = NativeTerrainSurfaceKindImport.CanonicalRecord(kind);
            Check(canonical.Length == (kind.SurfaceType == 0 ? 12 : 4) &&
                  BinaryPrimitives.ReadInt32LittleEndian(canonical) == kind.SurfaceType &&
                  (kind.SurfaceType != 0 || (BinaryPrimitives.ReadInt32LittleEndian(canonical.AsSpan(4)) == kind.Param1 &&
                                             BinaryPrimitives.ReadInt32LittleEndian(canonical.AsSpan(8)) == kind.Param2)),
                $"{kind.Label} has the wrong canonical record.");

            // A level whose table has other kinds only (and a portal) gets the record appended.
            byte[][] others = [Record(6, 14, 0), .. NativeTerrainSurfaceKindImport.ImportableKinds.Where(k => k != kind).Take(2).Select(NativeTerrainSurfaceKindImport.CanonicalRecord)];
            SyntheticSurfaceLevelDataFixture target = SyntheticSurfaceLevelDataFixture.Build(others, zeroTailBytes: 96);
            NativeTerrainSurfaceSourceData targetSource = Parse(target, "target");

            // The donor: an original level holding the record (here a synthetic one at index 1).
            SyntheticSurfaceLevelDataFixture donorLevel = SyntheticSurfaceLevelDataFixture.Build([Record(6, 11, 2), canonical]);
            NativeTerrainSurfaceSourceData donorSource = Parse(donorLevel, "donor");
            NativeTerrainSurfaceDescriptorImport? donor = NativeTerrainSurfaceKindImport.ResolveSavedDonor(kind, donorSource, 1, out string donorReason);
            Check(donor != null && donor.RawRecord.AsSpan().SequenceEqual(canonical) && donor.SourceSurfaceIndex == 1,
                $"{kind.Label}: the saved donor did not resolve ({donorReason}).");
            Check(NativeTerrainSurfaceKindImport.ResolveSavedDonor(kind, donorSource, 0, out _) == null,
                $"{kind.Label}: a donor index holding another kind resolved.");

            bool ok = NativeTerrainSurfaceLayoutComposer.TryBuild(
                target.Bytes, targetSource,
                [new NativeTerrainSurfaceBehaviorAssignment(6, kind, donor, "tile:6", kind.Label),
                 new NativeTerrainSurfaceBehaviorAssignment(1, kind, donor, "tile:1", kind.Label)],
                [], out NativeTerrainSurfaceLayoutPlan? plan, out string reason);
            Check(ok && plan != null, $"{kind.Label} was not appended: {reason}");
            Check(plan!.NewSurfaceCount == others.Length + 1 && plan.DescriptorGrowthBytes == 4 + canonical.Length,
                $"{kind.Label} appended the wrong number of bytes.");
            byte[] final = target.Bytes.ToArray();
            plan.After.CopyTo(final, (int)(plan.WadOffset - target.LevelDataWadOffset));
            NativeTerrainSurfaceSourceData reparsed = Parse(final, target.LevelDataWadOffset, "final");
            Check(reparsed.SpecialSurfaces[^1].RawBytes.AsSpan().SequenceEqual(canonical), $"{kind.Label} record did not read back.");
            for (int i = 0; i < others.Length; i++)
                Check(reparsed.SpecialSurfaces[i].RawBytes.AsSpan().SequenceEqual(others[i]), $"{kind.Label}: existing record {i} changed.");
            foreach (NativeCollisionSurfaceTriangle triangle in reparsed.CollisionSurfaceTriangles.Where(t => t.SurfaceIndex == others.Length))
                Check(NativeTerrainSurfaceCatalogBuilder.SignatureFor(triangle) == kind, $"{kind.Label}: a triangle reads as another kind.");
            Check(reparsed.CollisionSurfaceTriangles.Count(t => t.SurfaceIndex == others.Length) == 2,
                $"{kind.Label}: not exactly the two assigned triangles carry the new kind.");

            // The first original level holding it, in order, is the donor.
            NativeTerrainSurfaceDescriptorDonor? found = NativeTerrainSurfaceKindImport.FindDonor(
                kind,
                [new LevelDefinition { Key = "first", DisplayName = "First", SourceWadEntry = 1 },
                 new LevelDefinition { Key = "second", DisplayName = "Second", SourceWadEntry = 2 }],
                level => level.Key == "first" ? targetSource : donorSource);
            Check(found is { LevelKey: "second", Index: 1, WadEntry: 2 }, $"{kind.Label}: the donor search picked the wrong level.");
        }

        // A record that only looks like the kind (an extra word) is not the canonical one and is never used.
        SyntheticSurfaceLevelDataFixture odd = SyntheticSurfaceLevelDataFixture.Build([Record(0, 0, 1, 16)]);
        Check(NativeTerrainSurfaceKindImport.ResolveSavedDonor(new(0, 0, 1), Parse(odd, "odd"), 0, out _) == null,
            "A non-canonical lava record was accepted as a donor.");
        foreach (NativeTerrainSurfaceSignature never in new NativeTerrainSurfaceSignature[] { new(6, 14, 0), new(2, 0, 0), new(3, 0, 0), new(7, 1, 0), new(1, 0, 0) })
            Check(!NativeTerrainSurfaceKindImport.IsImportable(never) && NativeTerrainSurfaceKindImport.FindDonor(never, [], _ => null) == null,
                $"{never.Label} must never be copied into another level.");

        // The saved edit keeps the donor; Tile Type and Ground kinds are told from kinds copied with another face.
        TerrainPolygon face = new([new Vector2f(0, 0), new Vector2f(64, 0), new Vector2f(64, 64), new Vector2f(0, 64)], [10, 10, 10, 10], 3, 7, 2, "hp", new ColorRgba(1, 2, 3));
        face.ApplySurfaceBehaviorEdit(new TerrainSurfaceBehaviorEdit(0, 0, 1, "artisans", face.RuntimeKey, "damaging lava floor")
        {
            DescriptorDonorLevelKey = "peacekeepers", DescriptorDonorWadEntry = 20, DescriptorDonorIndex = 0
        });
        string path = Path.Combine(Path.GetTempPath(), $"ground-kind-{Guid.NewGuid():N}.json");
        try
        {
            TerrainEditStore.SaveAsync(path, [face], "Artisans").GetAwaiter().GetResult();
            TerrainPolygon reloaded = new([new Vector2f(0, 0), new Vector2f(64, 0), new Vector2f(64, 64), new Vector2f(0, 64)], [10, 10, 10, 10], 3, 7, 2, "hp", new ColorRgba(1, 2, 3));
            TerrainEditStore.Load(path, [reloaded]);
            Check(reloaded.SurfaceBehaviorEdit is { SurfaceType: 0, Param2: 1, DescriptorDonorLevelKey: "peacekeepers", DescriptorDonorWadEntry: 20, DescriptorDonorIndex: 0, ImportsDescriptor: true },
                "The saved lava tile lost the record it copies in.");
            Check(reloaded.SurfaceBehaviorEdit!.IsOwnTileType(reloaded.RuntimeKey, "artisans") &&
                  !reloaded.SurfaceBehaviorEdit.IsOwnTileType(reloaded.RuntimeKey, "stonehill") &&
                  !new TerrainSurfaceBehaviorEdit(0, 0, 1, "gnastygnorc", "4:1:hp", "lava").IsOwnTileType(reloaded.RuntimeKey, "artisans"),
                "A tile's own kind is not told apart from a kind copied with another face.");
            using JsonDocument saved = JsonDocument.Parse(File.ReadAllText(path));
            JsonElement row = saved.RootElement.GetProperty("edits")[0];
            Check(row.GetProperty("nativeSurfaceDescriptorDonorLevelKey").GetString() == "peacekeepers" &&
                  row.GetProperty("nativeSurfaceDescriptorDonorIndex").GetInt32() == 0,
                "The terrain-edit file does not name the donor record.");
        }
        finally
        {
            File.Delete(path);
        }
        return _checks;
    }

    /// <summary>With the disc: every kind into every original level (one tile), the record copied from the first level
    /// that has it, the result reparsed. Returns (levels, imports, reuses).</summary>
    public static (int Levels, int Imports, int Reuses) RunRetailKindMatrix(string workspaceRoot, string sourceImagePath)
    {
        LevelCatalog catalog = LevelCatalog.Load(workspaceRoot);
        LevelDefinition[] levels = LevelRealmCatalog.OrderLevels(catalog.Levels).Where(level => level.SourceWadEntry >= 0).ToArray();
        Dictionary<string, NativeTerrainSurfaceSourceData> sources = levels.ToDictionary(
            level => level.Key, level => PortalSourceDataLocator.LocateTerrainSurfaces(sourceImagePath, level));
        int imports = 0, reuses = 0;
        foreach (NativeTerrainSurfaceSignature kind in NativeTerrainSurfaceKindImport.ImportableKinds)
        {
            NativeTerrainSurfaceDescriptorDonor donor = NativeTerrainSurfaceKindImport.FindDonor(kind, levels, level => sources[level.Key])
                ?? throw new InvalidOperationException($"No original level holds {kind.Label}.");
            foreach (LevelDefinition level in levels)
            {
                NativeTerrainSurfaceSourceData source = sources[level.Key];
                byte[] levelData = PortalSourceDataLocator.ReadTerrainLevelData(sourceImagePath, source);
                bool has = source.SpecialSurfaces.Any(r => r.Type == kind.SurfaceType && r.Param1 == kind.Param1 && r.Param2 == kind.Param2);
                int triangle = source.Collision.FlagCount;
                bool ok = NativeTerrainSurfaceLayoutComposer.TryBuild(levelData, source,
                    [new NativeTerrainSurfaceBehaviorAssignment(triangle, kind, has ? null : donor.ToImport(), $"{level.Key}:{triangle}", kind.Label)],
                    [], out NativeTerrainSurfaceLayoutPlan? plan, out string reason);
                Check(ok && plan != null, $"{level.DisplayName} could not take {kind.Label}: {reason}");
                byte[] final = levelData.ToArray();
                plan!.After.CopyTo(final, (int)(plan.WadOffset - source.LevelDataWadOffset));
                NativeTerrainSurfaceSourceData reparsed = PortalSourceDataLocator.ParseTerrainSurfaces(final, source.LevelDataWadOffset, level.Key, level.DisplayName);
                Check(reparsed.CollisionSurfaceTriangles.Count(t => NativeTerrainSurfaceCatalogBuilder.SignatureFor(t) == kind) ==
                      source.CollisionSurfaceTriangles.Count(t => NativeTerrainSurfaceCatalogBuilder.SignatureFor(t) == kind) + 1,
                    $"{level.DisplayName}: {kind.Label} did not land on exactly one more triangle.");
                if (has) reuses++; else imports++;
            }
        }
        return (levels.Length, imports, reuses);
    }

    /// <summary>The check of an exported BIN against the ground-tiles.json the UiSmoke step wrote: the level's table holds
    /// each kind the tiles need (the copied ones exactly as original levels store them, its own records unchanged), and
    /// every collision triangle under each painted tile carries the tile's kind.</summary>
    public static int VerifyGroundExport(string exportImagePath, string sourceImagePath, string workspaceRoot, string specPath)
    {
        _checks = 0;
        using JsonDocument spec = JsonDocument.Parse(File.ReadAllText(specPath));
        string levelKey = spec.RootElement.GetProperty("level").GetString()!;
        LevelDefinition level = LevelCatalog.Load(workspaceRoot).FindByKey(levelKey) ?? throw new InvalidOperationException("Unknown level " + levelKey);
        NativeTerrainSurfaceSourceData before = PortalSourceDataLocator.LocateTerrainSurfaces(sourceImagePath, level);
        NativeTerrainSurfaceSourceData after = PortalSourceDataLocator.LocateTerrainSurfaces(exportImagePath, level);
        Console.WriteLine($"{level.DisplayName}: special surfaces {before.SpecialSurfaces.Count} -> {after.SpecialSurfaces.Count}, " +
            $"collision flags {before.Collision.FlagCount} -> {after.Collision.FlagCount}, spare bytes {before.FlagPromotionCapacity.VerifiedZeroTailBytes} -> {after.FlagPromotionCapacity.VerifiedZeroTailBytes}");
        for (int i = 0; i < before.SpecialSurfaces.Count; i++)
            Check(after.SpecialSurfaces[i].RawBytes.AsSpan().SequenceEqual(before.SpecialSurfaces[i].RawBytes), $"Existing surface record {i} changed.");
        for (int i = before.SpecialSurfaces.Count; i < after.SpecialSurfaces.Count; i++)
        {
            PortalSpecialSurfaceRecord added = after.SpecialSurfaces[i];
            NativeTerrainSurfaceSignature kind = new(added.Type, added.Param1, added.Param2);
            Check(NativeTerrainSurfaceKindImport.IsImportable(kind) && NativeTerrainSurfaceKindImport.IsCanonical(added, kind),
                $"Added surface record {i} is not a kind every level can use, stored as original levels store it.");
            Console.WriteLine($"  added record {i}: {kind.Label} = {Convert.ToHexString(added.RawBytes)}");
        }
        Check(after.Collision.TriangleCount == before.Collision.TriangleCount, "The collision triangle count changed.");
        (int, int, int) KindOf(JsonElement t) { JsonElement k = t.GetProperty("Kind"); return (k[0].GetInt32(), k[1].GetInt32(), k[2].GetInt32()); }
        HashSet<(int, int, int)> needed = spec.RootElement.GetProperty("tiles").EnumerateArray()
            .Where(t => !t.GetProperty("Ground").GetString()!.StartsWith("undone", StringComparison.Ordinal))
            .Select(KindOf)
            .Where(k => k.Item1 >= 0 && !before.SpecialSurfaces.Any(r => (r.Type, r.Param1, r.Param2) == k))
            .ToHashSet();
        Check(after.SpecialSurfaces.Count == before.SpecialSurfaces.Count + needed.Count,
            $"The table grew by {after.SpecialSurfaces.Count - before.SpecialSurfaces.Count} records, not by the {needed.Count} kind(s) the painted tiles needed.");

        ILookup<string, NativeCollisionSurfaceTriangle> byKey = after.CollisionSurfaceTriangles.ToLookup(NativeTerrainSurfaceCatalogBuilder.TriangleKey, StringComparer.Ordinal);
        foreach (JsonElement tile in spec.RootElement.GetProperty("tiles").EnumerateArray())
        {
            string ground = tile.GetProperty("Ground").GetString()!;
            JsonElement k = tile.GetProperty("Kind");
            NativeTerrainSurfaceSignature expected = ground.StartsWith("undone", StringComparison.Ordinal) || k[0].GetInt32() < 0
                ? NativeTerrainSurfaceSignature.Ordinary
                : new(k[0].GetInt32(), k[1].GetInt32(), k[2].GetInt32());
            NativeCollisionSurfaceTriangle[]? matched = null;
            foreach (JsonElement triangulation in tile.GetProperty("TriangleKeys").EnumerateArray())
            {
                NativeCollisionSurfaceTriangle[] hits = triangulation.EnumerateArray().Select(key => byKey[key.GetString()!].ToArray()).Where(h => h.Length == 1).Select(h => h[0]).ToArray();
                if (hits.Length == triangulation.GetArrayLength()) { matched = hits; break; }
            }
            Check(matched != null, $"{ground} tile {tile.GetProperty("RuntimeKey").GetString()}: its collision triangles were not found in the export.");
            foreach (NativeCollisionSurfaceTriangle triangle in matched!)
                Check(NativeTerrainSurfaceCatalogBuilder.SignatureFor(triangle) == expected,
                    $"{ground} tile {tile.GetProperty("RuntimeKey").GetString()}: triangle {triangle.TriangleIndex} is {NativeTerrainSurfaceCatalogBuilder.SignatureFor(triangle).Label}, not {expected.Label}.");
            Console.WriteLine($"  {ground,-10} {tile.GetProperty("RuntimeKey").GetString(),-10} triangles {string.Join(",", matched.Select(t => t.TriangleIndex))}: " +
                $"{string.Join(", ", matched.Select(t => $"flag 0x{t.FlagByte:X2} -> surface {t.SurfaceIndex}"))} = {expected.Label}");
        }
        return _checks;
    }

    private static byte[] Record(int type, int param1, int param2, int length = 0)
    {
        length = length > 0 ? length : type is 0 or 6 ? 12 : 4;
        byte[] raw = new byte[length];
        BinaryPrimitives.WriteInt32LittleEndian(raw, type);
        if (length >= 12)
        {
            BinaryPrimitives.WriteInt32LittleEndian(raw.AsSpan(4), param1);
            BinaryPrimitives.WriteInt32LittleEndian(raw.AsSpan(8), param2);
        }
        return raw;
    }

    private static NativeTerrainSurfaceSourceData Parse(SyntheticSurfaceLevelDataFixture fixture, string key) =>
        Parse(fixture.Bytes, fixture.LevelDataWadOffset, key);

    private static NativeTerrainSurfaceSourceData Parse(byte[] bytes, long wadOffset, string key) =>
        PortalSourceDataLocator.ParseTerrainSurfaces(bytes, wadOffset, key, key);
}
