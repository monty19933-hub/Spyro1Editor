using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Analysis;

public sealed record NativeTerrainSurfaceSignature(int SurfaceType, int Param1, int Param2)
{
    public static NativeTerrainSurfaceSignature Ordinary { get; } = new(-1, 0, 0);

    public bool IsOrdinary => SurfaceType < 0;
    public bool IsCrossLevelPortable => SurfaceType is < 0 or 0 or 4 or 5;

    public string Label => SurfaceType switch
    {
        -1 => "ordinary solid terrain",
        0 => Param1 == 0
            ? Param2 switch
            {
                0 => "damaging water floor",
                1 => "damaging lava floor",
                2 => "damaging ooze floor",
                _ => $"damaging floor effect {Param2}"
            }
            : Param2 switch
            {
                0 => "lethal water floor",
                1 => "lethal lava floor",
                2 => "lethal ooze floor",
                _ => $"lethal damaging floor effect {Param2}"
            },
        1 => "damaging surface",
        2 => "level-linked proximity surface",
        3 => "High Caves level-linked surface",
        4 => "supercharge surface",
        5 => "invisible-wall surface",
        6 => $"portal surface to level {Param1}",
        7 => $"conditional electric floor {Param1}",
        _ => $"native surface type {SurfaceType} ({Param1}, {Param2})"
    };

    public override string ToString() => Label;
}

public sealed record NativeTerrainFaceSurfaceBinding(
    string RuntimeKey,
    int TextureId,
    int VisualTriangleCount,
    int MatchedVisualTriangleCount,
    bool HasAnyCollisionCandidate,
    int NativeTriangleCount,
    bool HasExactTriangleMapping,
    bool HasUniformSurfaceSignature,
    NativeTerrainSurfaceSignature? Signature,
    IReadOnlyList<NativeCollisionSurfaceTriangle> NativeTriangles,
    string ReadinessNote);

public sealed record NativeTerrainTextureSurfaceVariant(
    int TextureId,
    NativeTerrainSurfaceSignature Signature,
    int FaceCount,
    int CollisionTriangleCount,
    string RepresentativeRuntimeKey,
    IReadOnlyList<string> RuntimeKeys);

public sealed record NativeTerrainSurfaceTransferReadiness(
    bool CanApply,
    int TargetSurfaceIndex,
    int TargetTriangleCount,
    int WritableFlagCount,
    string Note);

public sealed record NativeTerrainSurfaceBatchTransferReadiness(
    bool CanApply,
    int TargetSurfaceIndex,
    int TargetFaceCount,
    int ReadyFaceCount,
    int TargetTriangleCount,
    int WritableFlagCount,
    int PromotionCount,
    IReadOnlyList<int> TargetTriangleIndexes,
    IReadOnlyList<string> BlockedRuntimeKeys,
    string Note)
{
    public bool HasCompleteFaceCoverage =>
        TargetFaceCount > 0 &&
        ReadyFaceCount == TargetFaceCount &&
        BlockedRuntimeKeys.Count == 0;
}

public sealed record NativeTerrainSurfaceLevelCatalog(
    LevelDefinition Level,
    NativeTerrainSurfaceSourceData Source,
    IReadOnlyDictionary<string, NativeTerrainFaceSurfaceBinding> FaceBindings,
    IReadOnlyList<NativeTerrainTextureSurfaceVariant> TextureVariants)
{
    public NativeTerrainFaceSurfaceBinding? FindFace(string runtimeKey)
    {
        return FaceBindings.TryGetValue(runtimeKey ?? "", out NativeTerrainFaceSurfaceBinding? binding)
            ? binding
            : null;
    }

    public NativeTerrainSurfaceTransferReadiness EvaluateTransfer(
        string targetRuntimeKey,
        NativeTerrainSurfaceSignature signature,
        bool crossLevel)
    {
        NativeTerrainFaceSurfaceBinding? target = FindFace(targetRuntimeKey);
        if (target == null || !target.HasExactTriangleMapping || target.NativeTriangles.Count == 0)
        {
            if (signature.IsOrdinary && target is { MatchedVisualTriangleCount: 0, HasAnyCollisionCandidate: false })
            {
                return new NativeTerrainSurfaceTransferReadiness(
                    true,
                    0x3F,
                    0,
                    0,
                    "The selected visual face has no matching playable collision triangles, so ordinary donor art needs no gameplay-property patch.");
            }

            return new NativeTerrainSurfaceTransferReadiness(
                false,
                -1,
                target?.NativeTriangles.Count ?? 0,
                0,
                "The selected face does not have one exact native collision triangle for every visible triangle.");
        }

        if (crossLevel && !signature.IsCrossLevelPortable)
        {
            return new NativeTerrainSurfaceTransferReadiness(
                false,
                -1,
                target.NativeTriangles.Count,
                target.NativeTriangles.Count(triangle => triangle.FlagWadOffset >= 0),
                $"{signature.Label} depends on source-level runtime state and cannot be transplanted safely.");
        }

        int surfaceIndex;
        if (signature.IsOrdinary)
        {
            surfaceIndex = 0x3F;
        }
        else
        {
            PortalSpecialSurfaceRecord? descriptor = Source.SpecialSurfaces.FirstOrDefault(surface =>
                surface.Type == signature.SurfaceType &&
                surface.Param1 == signature.Param1 &&
                surface.Param2 == signature.Param2);
            if (descriptor == null)
            {
                return new NativeTerrainSurfaceTransferReadiness(
                    false,
                    -1,
                    target.NativeTriangles.Count,
                    target.NativeTriangles.Count(triangle => triangle.FlagWadOffset >= 0),
                    $"{Level.DisplayName} has no native {signature.Label} descriptor to reuse.");
            }

            surfaceIndex = descriptor.Index;
            int promotionCount = target.NativeTriangles
                .Where(triangle => triangle.FlagWadOffset < 0)
                .Select(triangle => triangle.TriangleIndex)
                .Distinct()
                .Count();
            if (promotionCount > Source.FlagPromotionCapacity.AdditionalFlagCapacity)
            {
                int writable = target.NativeTriangles.Count(triangle => triangle.FlagWadOffset >= 0);
                return new NativeTerrainSurfaceTransferReadiness(
                    false,
                    surfaceIndex,
                    target.NativeTriangles.Count,
                    writable,
                    $"The selected face needs {promotionCount} promoted collision flag(s), but {Level.DisplayName} has only {Source.FlagPromotionCapacity.AdditionalFlagCapacity} verified bounded promotion slot(s).");
            }
        }

        int writableCount = target.NativeTriangles.Count(triangle => triangle.FlagWadOffset >= 0);
        int promotedCount = target.NativeTriangles
            .Where(triangle => triangle.FlagWadOffset < 0)
            .Select(triangle => triangle.TriangleIndex)
            .Distinct()
            .Count();
        string note = signature.IsOrdinary
            ? writableCount == 0
                ? "The selected collision triangles already use the game's implicit ordinary-terrain behavior."
                : $"Ready to set {target.NativeTriangles.Count} collision triangle(s) to ordinary terrain while preserving native high flag bits."
            : promotedCount > 0
                ? $"Ready to copy {signature.Label} onto {target.NativeTriangles.Count} collision triangle(s) using {Level.DisplayName}'s native descriptor {surfaceIndex}; Create BIN will atomically promote {promotedCount} beyond-count flag(s)."
                : $"Ready to copy {signature.Label} onto {target.NativeTriangles.Count} collision triangle(s) using {Level.DisplayName}'s native descriptor {surfaceIndex}.";
        return new NativeTerrainSurfaceTransferReadiness(
            true,
            surfaceIndex,
            target.NativeTriangles.Count,
            writableCount,
            note);
    }

    public NativeTerrainSurfaceBatchTransferReadiness EvaluateBatchTransfer(
        IEnumerable<string> targetRuntimeKeys,
        NativeTerrainSurfaceSignature signature,
        bool crossLevel)
    {
        ArgumentNullException.ThrowIfNull(targetRuntimeKeys);
        ArgumentNullException.ThrowIfNull(signature);

        string[] runtimeKeys = targetRuntimeKeys
            .Where(runtimeKey => !string.IsNullOrWhiteSpace(runtimeKey))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (runtimeKeys.Length == 0)
        {
            return new NativeTerrainSurfaceBatchTransferReadiness(
                false, -1, 0, 0, 0, 0, 0, [], [],
                "No target faces use the texture record selected for replacement.");
        }

        if (crossLevel && !signature.IsCrossLevelPortable)
        {
            return new NativeTerrainSurfaceBatchTransferReadiness(
                false, -1, runtimeKeys.Length, 0, 0, 0, 0, [], runtimeKeys,
                $"{signature.Label} depends on source-level runtime state and cannot be transplanted safely.");
        }

        int surfaceIndex = 0x3F;
        bool importsPortableDescriptor = false;
        if (!signature.IsOrdinary)
        {
            PortalSpecialSurfaceRecord? descriptor = Source.SpecialSurfaces.FirstOrDefault(surface =>
                surface.Type == signature.SurfaceType &&
                surface.Param1 == signature.Param1 &&
                surface.Param2 == signature.Param2);
            if (descriptor == null)
            {
                if (!crossLevel || !signature.IsCrossLevelPortable)
                {
                    return new NativeTerrainSurfaceBatchTransferReadiness(
                        false, -1, runtimeKeys.Length, 0, 0, 0, 0, [], runtimeKeys,
                        $"{Level.DisplayName} has no native {signature.Label} descriptor to use for the shared replacement.");
                }
                if (Source.SpecialSurfaces.Count >= 63)
                {
                    return new NativeTerrainSurfaceBatchTransferReadiness(
                        false, -1, runtimeKeys.Length, 0, 0, 0, 0, [], runtimeKeys,
                        $"{Level.DisplayName}'s 63 native special-surface slots are already occupied; ordinary terrain reserves index 63.");
                }

                surfaceIndex = Source.SpecialSurfaces.Count;
                importsPortableDescriptor = true;
            }
            else
            {
                // One descriptor index is selected once for the whole batch. Per-face selection
                // could silently mix gameplay properties while replacing one shared art record.
                surfaceIndex = descriptor.Index;
            }
        }

        List<string> blockedRuntimeKeys = [];
        List<string> blockedNotes = [];
        List<NativeTerrainFaceSurfaceBinding> readyBindings = [];
        foreach (string runtimeKey in runtimeKeys)
        {
            NativeTerrainFaceSurfaceBinding? target = FindFace(runtimeKey);
            bool visualOnly = target is
            {
                MatchedVisualTriangleCount: 0,
                HasAnyCollisionCandidate: false
            };
            if (visualOnly && signature.IsOrdinary)
            {
                readyBindings.Add(target!);
                continue;
            }

            if (target == null || !target.HasExactTriangleMapping || target.NativeTriangles.Count == 0)
            {
                blockedRuntimeKeys.Add(runtimeKey);
                blockedNotes.Add(target?.ReadinessNote ?? "No native face binding was decoded.");
                continue;
            }

            readyBindings.Add(target);
        }

        NativeCollisionSurfaceTriangle[] targetTriangles = readyBindings
            .SelectMany(binding => binding.NativeTriangles)
            .GroupBy(triangle => triangle.TriangleIndex)
            .Select(group => group.First())
            .OrderBy(triangle => triangle.TriangleIndex)
            .ToArray();
        int writableCount = targetTriangles.Count(triangle => triangle.FlagWadOffset >= 0);
        int promotionCount = signature.IsOrdinary
            ? 0
            : targetTriangles.Count(triangle => triangle.FlagWadOffset < 0);

        if (blockedRuntimeKeys.Count > 0)
        {
            string examples = string.Join(" | ", blockedNotes.Distinct(StringComparer.OrdinalIgnoreCase).Take(2));
            return new NativeTerrainSurfaceBatchTransferReadiness(
                false,
                surfaceIndex,
                runtimeKeys.Length,
                readyBindings.Count,
                targetTriangles.Length,
                writableCount,
                promotionCount,
                targetTriangles.Select(triangle => triangle.TriangleIndex).ToArray(),
                blockedRuntimeKeys,
                $"{blockedRuntimeKeys.Count}/{runtimeKeys.Length} affected face(s) do not have a complete native property mapping. {examples}");
        }

        if (promotionCount > Source.FlagPromotionCapacity.AdditionalFlagCapacity)
        {
            return new NativeTerrainSurfaceBatchTransferReadiness(
                false,
                surfaceIndex,
                runtimeKeys.Length,
                readyBindings.Count,
                targetTriangles.Length,
                writableCount,
                promotionCount,
                targetTriangles.Select(triangle => triangle.TriangleIndex).ToArray(),
                [],
                $"The shared replacement needs {promotionCount} unique promoted collision flag(s), but {Level.DisplayName} has only {Source.FlagPromotionCapacity.AdditionalFlagCapacity} verified bounded promotion slot(s).");
        }

        int descriptorGrowthBytes = importsPortableDescriptor
            ? 4 + (signature.SurfaceType == 0 ? 12 : 4)
            : 0;
        int oldFlagStorageBytes = Align4(Source.Collision.FlagCount);
        int newFlagStorageBytes = Align4(checked(Source.Collision.FlagCount + promotionCount));
        int flagGrowthBytes = checked(newFlagStorageBytes - oldFlagStorageBytes);
        int totalGrowthBytes = checked(descriptorGrowthBytes + flagGrowthBytes);
        if (totalGrowthBytes > Source.FlagPromotionCapacity.VerifiedZeroTailBytes)
        {
            return new NativeTerrainSurfaceBatchTransferReadiness(
                false,
                surfaceIndex,
                runtimeKeys.Length,
                readyBindings.Count,
                targetTriangles.Length,
                writableCount,
                promotionCount,
                targetTriangles.Select(triangle => triangle.TriangleIndex).ToArray(),
                [],
                $"The shared replacement needs {descriptorGrowthBytes} descriptor byte(s) and {flagGrowthBytes} collision-growth byte(s), but {Level.DisplayName} has only {Source.FlagPromotionCapacity.VerifiedZeroTailBytes} verified zero-tail byte(s).");
        }

        string note = signature.IsOrdinary
            ? $"Ready to keep all {runtimeKeys.Length} affected face(s) on ordinary terrain across {targetTriangles.Length} unique collision triangle(s); visual-only faces need no property bytes."
            : importsPortableDescriptor && promotionCount > 0
                ? $"Ready to import {signature.Label} as native descriptor {surfaceIndex}, apply it to all {runtimeKeys.Length} affected face(s) and {targetTriangles.Length} unique collision triangle(s), and make {promotionCount} bounded flag promotion(s)."
            : importsPortableDescriptor
                ? $"Ready to import {signature.Label} as native descriptor {surfaceIndex} and apply it to all {runtimeKeys.Length} affected face(s) and {targetTriangles.Length} unique collision triangle(s)."
            : promotionCount > 0
                ? $"Ready to copy {signature.Label} through native descriptor {surfaceIndex} onto all {runtimeKeys.Length} affected face(s) and {targetTriangles.Length} unique collision triangle(s), including {promotionCount} bounded promotion(s)."
                : $"Ready to copy {signature.Label} through native descriptor {surfaceIndex} onto all {runtimeKeys.Length} affected face(s) and {targetTriangles.Length} unique collision triangle(s).";
        return new NativeTerrainSurfaceBatchTransferReadiness(
            true,
            surfaceIndex,
            runtimeKeys.Length,
            readyBindings.Count,
            targetTriangles.Length,
            writableCount,
            promotionCount,
            targetTriangles.Select(triangle => triangle.TriangleIndex).ToArray(),
            [],
            note);
    }

    private static int Align4(int value) => checked((value + 3) & ~3);
}

public static class NativeTerrainSurfaceCatalogBuilder
{
    public static NativeTerrainSurfaceLevelCatalog Build(
        string sourceImagePath,
        LevelDefinition level,
        GeometryCandidate geometry)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(geometry);
        NativeTerrainSurfaceSourceData source = PortalSourceDataLocator.LocateTerrainSurfaces(sourceImagePath, level);
        return Build(level, geometry, source);
    }

    public static NativeTerrainSurfaceLevelCatalog Build(
        LevelDefinition level,
        GeometryCandidate geometry,
        NativeTerrainSurfaceSourceData source)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(source);

        Dictionary<string, NativeCollisionSurfaceTriangle[]> nativeByTriangleKey = source.CollisionSurfaceTriangles
            .GroupBy(TriangleKey, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        Dictionary<string, NativeTerrainFaceSurfaceBinding> bindings = new(StringComparer.OrdinalIgnoreCase);
        foreach (TerrainPolygon face in geometry.Polygons.Where(face =>
            !face.IsTerrainRemoved &&
            face.OriginalTextureId >= 0 &&
            face.OriginalPoints.Count >= 3 &&
            face.OriginalZValues.Length >= face.OriginalPoints.Count))
        {
            int visualTriangleCount = face.OriginalPoints.Count - 2;
            int matchedVisualTriangleCount = 0;
            bool hasAnyCollisionCandidate = false;
            List<NativeCollisionSurfaceTriangle> nativeTriangles = [];
            bool exact = true;
            for (int i = 1; i < face.OriginalPoints.Count - 1; i++)
            {
                string key = VisualTriangleKey(face, 0, i, i + 1);
                if (!nativeByTriangleKey.TryGetValue(key, out NativeCollisionSurfaceTriangle[]? candidates) || candidates.Length != 1)
                {
                    hasAnyCollisionCandidate |= candidates is { Length: > 0 };
                    exact = false;
                    continue;
                }

                hasAnyCollisionCandidate = true;
                matchedVisualTriangleCount++;
                nativeTriangles.Add(candidates[0]);
            }

            if (nativeTriangles.Select(triangle => triangle.TriangleIndex).Distinct().Count() != nativeTriangles.Count)
                exact = false;
            exact &= matchedVisualTriangleCount == visualTriangleCount;

            NativeTerrainSurfaceSignature[] signatures = nativeTriangles
                .Select(SignatureFor)
                .Distinct()
                .ToArray();
            bool visualOnly = matchedVisualTriangleCount == 0 && !hasAnyCollisionCandidate;
            bool uniform = (exact && signatures.Length == 1) || visualOnly;
            NativeTerrainSurfaceSignature? signature = visualOnly
                ? NativeTerrainSurfaceSignature.Ordinary
                : uniform ? signatures[0] : null;
            string note = visualOnly
                ? "No visible triangle has a native collision candidate; this is visual-only ordinary donor art."
                : !exact
                ? $"{matchedVisualTriangleCount}/{visualTriangleCount} visible triangle(s) have a unique native collision match."
                : !uniform
                    ? $"The face's {nativeTriangles.Count} collision triangles use mixed native surface behaviors."
                    : $"Exact native mapping: {nativeTriangles.Count} triangle(s), {signature!.Label}.";
            bindings[face.RuntimeKey] = new NativeTerrainFaceSurfaceBinding(
                face.RuntimeKey,
                face.OriginalTextureId,
                visualTriangleCount,
                matchedVisualTriangleCount,
                hasAnyCollisionCandidate,
                nativeTriangles.Count,
                exact,
                uniform,
                signature,
                nativeTriangles.ToArray(),
                note);
        }

        NativeTerrainTextureSurfaceVariant[] variants = bindings.Values
            .Where(binding =>
                (binding.HasExactTriangleMapping ||
                 (binding.MatchedVisualTriangleCount == 0 && !binding.HasAnyCollisionCandidate)) &&
                binding.HasUniformSurfaceSignature &&
                binding.Signature != null)
            .GroupBy(binding => new { binding.TextureId, Signature = binding.Signature! })
            .Select(group =>
            {
                NativeTerrainFaceSurfaceBinding[] faces = group
                    .OrderByDescending(binding => binding.NativeTriangleCount)
                    .ThenBy(binding => binding.RuntimeKey, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                return new NativeTerrainTextureSurfaceVariant(
                    group.Key.TextureId,
                    group.Key.Signature,
                    faces.Length,
                    faces.Sum(binding => binding.NativeTriangleCount),
                    faces[0].RuntimeKey,
                    faces.Select(binding => binding.RuntimeKey).ToArray());
            })
            .OrderBy(variant => variant.TextureId)
            .ThenBy(variant => variant.Signature.SurfaceType)
            .ThenBy(variant => variant.Signature.Param1)
            .ThenBy(variant => variant.Signature.Param2)
            .ToArray();
        return new NativeTerrainSurfaceLevelCatalog(level, source, bindings, variants);
    }

    public static NativeTerrainSurfaceSignature SignatureFor(NativeCollisionSurfaceTriangle triangle)
    {
        return triangle.HasSpecialSurface
            ? new NativeTerrainSurfaceSignature(triangle.SurfaceType, triangle.Param1, triangle.Param2)
            : NativeTerrainSurfaceSignature.Ordinary;
    }

    public static string TriangleKey(NativeCollisionSurfaceTriangle triangle)
    {
        return string.Join("|", new[]
        {
            PointKey(triangle.P1.X, triangle.P1.Y, triangle.P1.Z),
            PointKey(triangle.P2.X, triangle.P2.Y, triangle.P2.Z),
            PointKey(triangle.P3.X, triangle.P3.Y, triangle.P3.Z)
        }.OrderBy(value => value, StringComparer.Ordinal));
    }

    public static string VisualTriangleKey(TerrainPolygon face, int a, int b, int c)
    {
        return string.Join("|", new[]
        {
            PointKey(face.OriginalPoints[a].X, face.OriginalPoints[a].Y, face.OriginalZValues[a]),
            PointKey(face.OriginalPoints[b].X, face.OriginalPoints[b].Y, face.OriginalZValues[b]),
            PointKey(face.OriginalPoints[c].X, face.OriginalPoints[c].Y, face.OriginalZValues[c])
        }.OrderBy(value => value, StringComparer.Ordinal));
    }

    public static string PointKey(float x, float y, float z)
    {
        return $"{MathF.Round(x)},{MathF.Round(y)},{MathF.Round(z)}";
    }
}
