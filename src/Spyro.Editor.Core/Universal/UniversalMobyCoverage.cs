using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Universal;

public sealed record UniversalCoverageVariant(string VariantId, int ClassId, string SourceLevelKey, int SourceRow,
    IReadOnlyList<int> EquivalentSourceRows, string? CatalogRestriction, int ExternalObjectLinks,
    bool LocalModel, bool SharedModel, bool HasBehavior)
{
    public string? SpecialId { get; init; }
    public string AssetOrigin { get; init; } = "usa-source-native";
}

public sealed record UniversalCoverageCell(string Destination, string VariantId, string CatalogStatus,
    string DependencyExportStatus, string RuntimeStatus, string? FailureKind, string? Detail,
    IReadOnlyList<UniversalClassImport> Dependencies, IReadOnlyList<string> Warnings, UniversalRamBudget? Ram)
{
    public IReadOnlyList<UniversalParticleDependency> ParticleDependencies { get; init; } = [];
    public IReadOnlyList<UniversalPlayerAnimationImport> PlayerAnimationImports { get; init; } = [];
    public IReadOnlyList<UniversalBehaviorSourceReport> Behavior { get; init; } = [];
    public int RestoredObjectLinks { get; init; }
    public int UnresolvedObjectLinks { get; init; }
    public int? StaticPlanRepresentativeRow { get; init; }
    public string StaticAnalysisMethod { get; init; } = "exact-compose";
}

public sealed record UniversalCoverageReport(int SchemaVersion, string SourceImageSha256, string VariantDefinition,
    IReadOnlyList<UniversalCoverageVariant> Variants, IReadOnlyList<string> Destinations,
    IReadOnlyList<UniversalCoverageCell> Cells, string RuntimeEvidenceScope)
{
    public string ImplementationModuleVersionId { get; init; } = typeof(UniversalMobyComposer).Module.ModuleVersionId.ToString();
    public string? SourceCodeFingerprint { get; init; }
    public string SourceCodeFingerprintScope { get; init; } = "Core .cs source snapshot at matrix start; compiled module identity records the actually loaded implementation.";
}

/// <summary>Separate catalog, exact dependency composition and runtime evidence. Every inventory row is
/// represented; placement-only duplicates share a semantic variant within the same source/class.
/// No successful dry run is reported as a written-disc readback or emulator observation.</summary>
public static class UniversalMobyCoverage
{
    public static IReadOnlyList<UniversalCoverageVariant> MeaningfulVariants(UniversalSourceLibrary lib, LevelCatalog catalog)
    {
        return UniversalMobyCatalogBuilder.AllVariants(lib, catalog).GroupBy(v => (v.SourceLevelKey, v.ClassId, Signature(lib, v)))
            .Select(g =>
            {
                var v = g.OrderBy(x => x.SourceRow).First();
                return new UniversalCoverageVariant($"{v.SourceLevelKey}:0x{v.ClassId:X3}:{g.Key.Item3}", v.ClassId, v.SourceLevelKey,
                    v.SourceRow, g.Select(x => x.SourceRow).Order().ToList(), v.RestrictionReason, v.DroppedExternalPointers,
                    v.HasLocalModel, v.SharedModel, v.HasBehavior);
            }).OrderBy(v => v.ClassId).ThenBy(v => v.SourceLevelKey, StringComparer.Ordinal).ThenBy(v => v.SourceRow).ToList();
    }

    private static string Signature(UniversalSourceLibrary lib, UniversalMobyVariant v)
    {
        var t = lib.Get(v.SourceLevelKey).Data.CaptureRow(v.SourceLevelKey, v.SourceRow);
        byte[] row = t.Row.ToArray(), props = t.Properties.ToArray();
        Array.Clear(row, 0, 4); // properties address
        Array.Clear(row, 0x0C, 12); // placement coordinates
        row[0x43] = 0; row[0x46] = 0; // source activation pod and authored heading
        foreach (var (offset, target) in t.InternalPointers)
            BinaryPrimitives.WriteInt32LittleEndian(props.AsSpan(offset), target);
        byte[] identity = [.. row, .. props, .. BitConverter.GetBytes(t.DroppedExternalPointers)];
        return Convert.ToHexStringLower(SHA256.HashData(identity));
    }

    public static UniversalCoverageReport Run(UniversalSourceLibrary lib, LevelCatalog catalog, bool compose = true,
        Action<string>? progress = null, IReadOnlySet<string>? destinationKeys = null,
        Func<string, UniversalSpecialAsset?>? special = null)
    {
        var variants = MeaningfulVariants(lib, catalog).Concat(UniversalSpecialCatalog.Entries(special != null)
            .SelectMany(e => e.Variants.Select(v => new UniversalCoverageVariant("special:" + UniversalSpecialCatalog.SpecialIdForClass(v.ClassId),
                v.ClassId, v.SourceLevelKey, v.SourceRow, [v.SourceRow], e.RestrictionReason, 0, true, false, true)
                { SpecialId = UniversalSpecialCatalog.SpecialIdForClass(v.ClassId),
                    AssetOrigin = v.ClassId == UniversalDragonflyEggs.SignClass || v.ClassId == UniversalDragonflyEggs.Variants[0].Class
                        ? "japan-source-native" : "derived-colour-variant-of-japan-model" }))).ToList();
        var destinations = catalog.Levels.Where(l => l.SourceWadEntry > 0 && (destinationKeys == null || destinationKeys.Contains(l.Key))).ToList();
        List<UniversalCoverageCell> cells = [];
        foreach (var dest in destinations)
        {
            progress?.Invoke($"{dest.Key}: {variants.Count} meaningful variants");
            var d = lib.Get(dest.Key);
            var vram = compose ? UniversalMobyExport.DefaultProtectedVram(lib.ImagePath, dest).ToList() : [];
            Dictionary<(string Source, int Class, int PropertiesBytes, int Fixups, int Links, int SelfLinks, int Treasure, string? Special), UniversalCoverageCell> plans = [];
            foreach (var v in variants)
            {
                if (v.CatalogRestriction != null)
                {
                    cells.Add(new(dest.Key, v.VariantId, "restricted", "not-tested", "not-tested", "catalog-restriction", v.CatalogRestriction, [], [], null));
                    continue;
                }
                if (!compose)
                {
                    cells.Add(new(dest.Key, v.VariantId, "listed", "not-tested", "not-tested", null, null, [], [], null));
                    continue;
                }
                byte[] row = v.SpecialId == null ? lib.Get(v.SourceLevelKey).Data.Row(v.SourceRow) : new byte[UniversalLevelData.RowStride];
                var p = new UniversalMobyPlacement(v.SourceLevelKey, v.SourceRow, BitConverter.ToInt32(row, 0x0C),
                    BitConverter.ToInt32(row, 0x10), BitConverter.ToInt32(row, 0x14), SpecialId: v.SpecialId,
                    Text: v.SpecialId == UniversalDragonflyEggs.SignId ? ["COVERAGE"] : null);
                var template = v.SpecialId == null ? lib.Get(v.SourceLevelKey).Data.CaptureRow(v.SourceLevelKey, v.SourceRow) : null;
                var planKey = (v.SourceLevelKey, v.ClassId, template?.Properties.Length ?? 0, template?.InternalPointers.Count ?? 0,
                    template?.DroppedExternalPointers ?? 0, template?.ExternalRowPointers.Count(link => link.SourceRow == v.SourceRow) ?? 0, v.SpecialId == null ? UniversalMobyComposer.TreasureValue(row) : 0, v.SpecialId);
                if (plans.TryGetValue(planKey, out var prior))
                {
                    cells.Add(prior with { VariantId = v.VariantId, StaticAnalysisMethod = "reused-identical-resource-plan",
                        Warnings = prior.Warnings.Select(w => w.Replace($" row {prior.StaticPlanRepresentativeRow}:", $" row {v.SourceRow}:", StringComparison.Ordinal)).ToArray() });
                    continue;
                }
                int cellStart = cells.Count;
                try
                {
                    var r = UniversalMobyComposer.Compose(dest, d.Data.Bytes, d.Overlay.Bytes.ToArray(), lib.Disc.Exe,
                        lib.Get, lib.SharedClasses, vram, [p], special, sharedModelPackage: lib.SharedModelPackage, playerAnimationSource: lib.PlayerAnimations);
                    cells.Add(new(dest.Key, v.VariantId, "listed", r.ParticleDependencies.Any(p => p.Status == "dependency-unverified") || r.UnresolvedObjectLinks > 0 ? "composed-dependency-unverified" : "composed-static-only",
                        "not-tested", null, null, r.Classes, r.Warnings, r.Ram) { ParticleDependencies = r.ParticleDependencies, PlayerAnimationImports = r.PlayerAnimationImports, Behavior = r.Behavior, RestoredObjectLinks = r.RestoredObjectLinks, UnresolvedObjectLinks = r.UnresolvedObjectLinks });
                }
                catch (Exception e) when (e is InvalidDataException or IOException or InvalidOperationException or ArgumentException)
                {
                    cells.Add(new(dest.Key, v.VariantId, "listed", "failed", "not-tested", FailureKind(e), e.Message, [], [],
                        e is UniversalBudgetException budget ? budget.Budget : null));
                }
                cells[cellStart] = cells[cellStart] with { StaticPlanRepresentativeRow = v.SourceRow };
                plans[planKey] = cells[cellStart];
            }
        }
        string hash;
        using (var fs = File.OpenRead(lib.ImagePath)) hash = Convert.ToHexStringLower(SHA256.HashData(fs));
        return new(2, hash, "Same source/class; canonical row and reachable properties, excluding coordinates, heading and activation pod. Source rows remain enumerated; no cross-source semantic equivalence is assumed.",
            variants, destinations.Select(d => d.Key).ToList(), cells, "No emulator tests are inferred or imported. Written-disc readback and runtime observations require separate receipts. Static resource plans reuse a composed representative only for identical source/class, reachable properties size, fixup count, dropped-link count, restorable self-link count and treasure delta; each semantic variant remains a distinct cell.");
    }

    public static string FailureKind(Exception e) => e switch
    {
        _ when e.Message.Contains("class remapping", StringComparison.OrdinalIgnoreCase) || e.Message.Contains("initializer class stores") || e.Message.Contains("Moby ownership chain") || e.Message.Contains("class-store scratch") => "class-remap-ownership",
        _ when e.Message.Contains("Shared class") && e.Message.Contains("alias package") => "shared-model-package",
        UniversalSourceCollisionException => "source-class-collision",
        UniversalBudgetException => "ram-capacity",
        _ when e.Message.Contains("Particle dependency") && e.Message.Contains("conflict") => "particle-identity-conflict",
        _ when e.Message.Contains("Particle dependency") => "missing-particle-dependency",
        _ when e.Message.Contains("non-4bpp") || e.Message.Contains("reserved texture depth") => "unsupported-texture-depth",
        _ when e.Message.Contains("sound", StringComparison.OrdinalIgnoreCase) => "sound-dependency",
        _ when e.Message.Contains("texture", StringComparison.OrdinalIgnoreCase) || e.Message.Contains("CLUT") => "vram-capacity",
        _ when e.Message.Contains("registration slot") => "model-slot-capacity",
        _ when e.Message.Contains("persistent placed") => "persistent-row-capacity",
        _ when e.Message.Contains("arena") || e.Message.Contains("dynamic object") => "dynamic-slot-capacity",
        _ => "dependency-extraction"
    };
}
