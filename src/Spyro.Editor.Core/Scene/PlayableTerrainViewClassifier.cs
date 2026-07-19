using System.Runtime.CompilerServices;
using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.Core.Scene;

/// <summary>
/// Builds the editor-only terrain subset used by the default Playable view.
/// The source geometry is never changed: Complete Scene and every exporter
/// continue to use every face in <see cref="GeometryCandidate.Polygons"/>.
/// </summary>
public static class PlayableTerrainViewClassifier
{
    public const int MinimumGroupFaceCount = 256;
    public const double MinimumProjectedFaceAreaShare = 0.25;
    public const int MinimumTouchedSceneBoundsSides = 2;
    public const float CoplanarEpsilon = 0.001f;
    private const int MinimumFacesLeftVisible = 64;
    private const double MinimumFaceShareLeftVisible = 0.10;

    // These are presentation signatures from the all-35 retail audit. A
    // rebuilt cache from another revision or an already-edited BIN must fail
    // open: a newly qualifying plane remains visible until separately audited.
    private static readonly IReadOnlyDictionary<string, int> AuditedSuppressedFaceCounts =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["artisans:texture-27:z-192"] = 780,
            ["toasty:texture-52:z-378"] = 364,
            ["sunnyflight:texture-46:z-129"] = 1841,
            ["mistybog:texture-38:z-605"] = 1488,
            ["mistybog:texture-38:z-157"] = 664,
            ["metalhead:texture-67:z-128"] = 439,
            ["gnastysworld:texture-0:z-387"] = 1954,
            ["gnorccove:texture-10:z-1088"] = 1478
        };

    private static readonly IReadOnlyDictionary<string, (int FaceCount, string Evidence)> AuditedRetainedFaceCounts =
        new Dictionary<string, (int FaceCount, string Evidence)>(StringComparer.Ordinal)
        {
            ["stonehill:texture-32:z-1024"] =
                (327, "Stone Hill's source-proven ocean sheet"),
            ["beastmakers:texture-41:z-951"] =
                (604, "Beast Makers' paired-capture-confirmed swamp/ooze gameplay context")
        };

    private static readonly ConditionalWeakTable<GeometryCandidate, PlanCache> CachedPlans = new();

    public static PlayableTerrainViewPlan GetOrBuild(string levelKey, GeometryCandidate geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        string normalizedLevelKey = NormalizeLevelKey(levelKey);
        PlanCache cache = CachedPlans.GetValue(geometry, _ => new PlanCache());
        lock (cache.Gate)
        {
            if (!cache.Plans.TryGetValue(normalizedLevelKey, out PlayableTerrainViewPlan? plan))
            {
                plan = Build(normalizedLevelKey, geometry);
                cache.Plans.Add(normalizedLevelKey, plan);
            }
            return plan;
        }
    }

    public static bool ShouldShow(string levelKey, GeometryCandidate geometry, TerrainPolygon polygon) =>
        GetOrBuild(levelKey, geometry).ShouldShow(polygon);

    public static PlayableTerrainViewPlan Build(string levelKey, GeometryCandidate geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        string normalizedLevelKey = NormalizeLevelKey(levelKey);
        if (string.IsNullOrWhiteSpace(normalizedLevelKey))
        {
            return PlayableTerrainViewPlan.FailOpen(
                geometry.Polygons.Count,
                "A level key is required before broad terrain sheets can be hidden safely.");
        }

        TerrainPolygon[] sourceFaces = geometry.Polygons
            .Where(IsSourceHighPolyFace)
            .ToArray();
        if (sourceFaces.Length < MinimumGroupFaceCount ||
            !TryGetProjectedFaceArea(sourceFaces, out double totalProjectedFaceArea) ||
            !TryGetSceneBounds(sourceFaces, out SceneBounds sceneBounds))
        {
            return PlayableTerrainViewPlan.FailOpen(
                geometry.Polygons.Count,
                "The loaded geometry does not have enough exact source HP data for a safe playable-view classification.");
        }

        List<PlayableTerrainViewGroup> candidates = [];
        foreach (IGrouping<CoplanarTextureKey, TerrainPolygon> group in sourceFaces
                     .Select(face => (Face: face, Key: TryGetCoplanarTextureKey(face)))
                     .Where(item => item.Key.HasValue)
                     .GroupBy(item => item.Key!.Value, item => item.Face))
        {
            TerrainPolygon[] faces = group.ToArray();
            if (faces.Length < MinimumGroupFaceCount)
                continue;

            double projectedFaceArea = faces.Sum(ProjectedArea);
            double projectedFaceAreaShare = projectedFaceArea / totalProjectedFaceArea;
            if (projectedFaceAreaShare < MinimumProjectedFaceAreaShare ||
                !TryGetSceneBounds(faces, out SceneBounds groupBounds))
            {
                continue;
            }

            IReadOnlyList<string> touchedSides = TouchedSceneBoundsSides(groupBounds, sceneBounds);
            if (touchedSides.Count < MinimumTouchedSceneBoundsSides)
                continue;

            int nativeDepthTwoOrFartherFaces = faces.Count(face => face.FaceDepth >= 2);
            string signature = BuildSignature(normalizedLevelKey, group.Key.TextureId, group.Key.PlaneZ);
            bool retainedLevelException =
                AuditedRetainedFaceCounts.TryGetValue(signature, out (int FaceCount, string Evidence) retainedAudit) &&
                faces.Length == retainedAudit.FaceCount;
            bool auditedSuppression =
                AuditedSuppressedFaceCounts.TryGetValue(signature, out int auditedFaceCount) &&
                faces.Length == auditedFaceCount;
            PlayableTerrainViewDisposition disposition = retainedLevelException
                ? PlayableTerrainViewDisposition.RetainedLevelException
                : auditedSuppression
                    ? PlayableTerrainViewDisposition.SuppressedByDefault
                    : PlayableTerrainViewDisposition.RetainedUnreviewedCandidate;
            string measurement =
                $"{faces.Length:N0} exact coplanar faces, {projectedFaceAreaShare:P1} of projected face area, " +
                $"touching {touchedSides.Count} scene bounds sides ({string.Join(", ", touchedSides)})";
            string reason = retainedLevelException
                ? $"{signature} matches {retainedAudit.Evidence}. It remains visible in Playable view; Complete Scene still uses the same full source geometry."
                : auditedSuppression
                    ? $"{signature} is an audited oversized flat overview sheet ({measurement}). Playable view omits it from Map/Fit; this is not a claim that it is non-playable. Complete Scene, local Game View, and export retain it."
                    : $"{signature} passed the broad-sheet measurements ({measurement}) but its exact signature/count was not in the retail audit. Playable view retains it until reviewed.";

            candidates.Add(new PlayableTerrainViewGroup(
                signature,
                normalizedLevelKey,
                group.Key.TextureId,
                group.Key.PlaneZ,
                DominantSurface(faces),
                disposition,
                faces.Length,
                faces.Length / (double)Math.Max(1, geometry.Polygons.Count),
                projectedFaceArea,
                projectedFaceAreaShare,
                touchedSides,
                nativeDepthTwoOrFartherFaces,
                nativeDepthTwoOrFartherFaces / (double)faces.Length,
                faces.Select(face => face.RuntimeKey).Order(StringComparer.Ordinal).ToArray(),
                reason));
        }

        if (candidates.Count == 0)
        {
            return PlayableTerrainViewPlan.FailOpen(
                geometry.Polygons.Count,
                "No texture-scoped exact coplanar sheet passed the conservative broad-background thresholds.");
        }

        HashSet<string> suppressedKeys = candidates
            .Where(group => group.Disposition == PlayableTerrainViewDisposition.SuppressedByDefault)
            .SelectMany(group => group.RuntimeKeys)
            .ToHashSet(StringComparer.Ordinal);
        int remainingFaces = geometry.Polygons.Count(face => !suppressedKeys.Contains(face.RuntimeKey));
        int requiredRemainingFaces = Math.Max(
            MinimumFacesLeftVisible,
            (int)Math.Ceiling(geometry.Polygons.Count * MinimumFaceShareLeftVisible));
        bool failedOpen = remainingFaces < requiredRemainingFaces;
        if (failedOpen)
        {
            candidates = candidates
                .Select(group => group.Disposition == PlayableTerrainViewDisposition.SuppressedByDefault
                    ? group with
                    {
                        Disposition = PlayableTerrainViewDisposition.RetainedFailOpen,
                        Reason = $"{group.Signature} passed the broad-sheet measurements, but hiding it would leave too little scene context, so Playable view retained it."
                    }
                    : group)
                .ToList();
        }

        return new PlayableTerrainViewPlan(
            geometry.Polygons.Count,
            geometry.Polygons,
            candidates
                .OrderBy(group => group.Disposition)
                .ThenByDescending(group => group.FaceCount)
                .ThenBy(group => group.TextureId)
                .ThenBy(group => group.PlaneZ)
                .ToArray(),
            failedOpen,
            failedOpen
                ? "Broad-sheet candidates were retained because the minimum foreground/context guard failed."
                : "Conservative texture-scoped broad-sheet classification completed.");
    }

    private static bool IsSourceHighPolyFace(TerrainPolygon face) =>
        face.OriginalTextureId >= 0 &&
        face.Detail.Equals("hp", StringComparison.OrdinalIgnoreCase) &&
        face.HasCompleteNativeHighPolyFacePayload &&
        face.OriginalPoints.Count is >= 3 and <= SourceSceneOverlayContract.CornerSlotCount &&
        face.OriginalZValues.Length >= face.OriginalPoints.Count;

    private static CoplanarTextureKey? TryGetCoplanarTextureKey(TerrainPolygon face)
    {
        int count = face.OriginalPoints.Count;
        if (count < 3 || face.OriginalZValues.Length < count)
            return null;

        float averageZ = 0;
        for (int index = 0; index < count; index++)
        {
            float z = face.OriginalZValues[index];
            if (!float.IsFinite(z))
                return null;
            averageZ += z;
        }
        averageZ /= count;
        for (int index = 0; index < count; index++)
        {
            if (MathF.Abs(face.OriginalZValues[index] - averageZ) > CoplanarEpsilon)
                return null;
        }

        float normalizedPlaneZ = MathF.Round(averageZ / CoplanarEpsilon) * CoplanarEpsilon;
        return new CoplanarTextureKey(face.OriginalTextureId, normalizedPlaneZ);
    }

    private static bool TryGetProjectedFaceArea(IEnumerable<TerrainPolygon> faces, out double area)
    {
        area = faces.Sum(ProjectedArea);
        return double.IsFinite(area) && area > 0;
    }

    private static double ProjectedArea(TerrainPolygon face)
    {
        IReadOnlyList<Vector2f> points = face.OriginalPoints;
        if (points.Count < 3)
            return 0;

        double twiceSignedArea = 0;
        for (int index = 0; index < points.Count; index++)
        {
            Vector2f current = points[index];
            Vector2f next = points[(index + 1) % points.Count];
            twiceSignedArea += (current.X * (double)next.Y) - (next.X * (double)current.Y);
        }

        double area = Math.Abs(twiceSignedArea) * 0.5;
        return double.IsFinite(area) ? area : 0;
    }

    private static bool TryGetSceneBounds(IEnumerable<TerrainPolygon> faces, out SceneBounds bounds)
    {
        float minX = float.MaxValue;
        float minY = float.MaxValue;
        float maxX = float.MinValue;
        float maxY = float.MinValue;
        bool found = false;
        foreach (TerrainPolygon face in faces)
        {
            foreach (Vector2f point in face.OriginalPoints)
            {
                if (!float.IsFinite(point.X) || !float.IsFinite(point.Y))
                    continue;
                minX = Math.Min(minX, point.X);
                minY = Math.Min(minY, point.Y);
                maxX = Math.Max(maxX, point.X);
                maxY = Math.Max(maxY, point.Y);
                found = true;
            }
        }

        bounds = new SceneBounds(minX, minY, maxX, maxY);
        return found && maxX > minX && maxY > minY;
    }

    private static IReadOnlyList<string> TouchedSceneBoundsSides(SceneBounds group, SceneBounds scene)
    {
        List<string> result = [];
        if (MathF.Abs(group.MinX - scene.MinX) <= CoplanarEpsilon)
            result.Add("left");
        if (MathF.Abs(group.MaxX - scene.MaxX) <= CoplanarEpsilon)
            result.Add("right");
        if (MathF.Abs(group.MinY - scene.MinY) <= CoplanarEpsilon)
            result.Add("top");
        if (MathF.Abs(group.MaxY - scene.MaxY) <= CoplanarEpsilon)
            result.Add("bottom");
        return result;
    }

    private static string DominantSurface(IEnumerable<TerrainPolygon> faces) =>
        faces
            .GroupBy(face => TerrainMaterialClassifier.NormalizeSurfaceName(face.Surface), StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Key)
            .FirstOrDefault() ?? "unknown";

    private static string BuildSignature(string levelKey, int textureId, float planeZ) =>
        $"{levelKey}:texture-{textureId}:z-{planeZ:0.###}";

    private static string NormalizeLevelKey(string? levelKey) =>
        (levelKey ?? "").Trim().ToLowerInvariant();

    private readonly record struct CoplanarTextureKey(int TextureId, float PlaneZ);
    private readonly record struct SceneBounds(float MinX, float MinY, float MaxX, float MaxY);

    private sealed class PlanCache
    {
        public object Gate { get; } = new();
        public Dictionary<string, PlayableTerrainViewPlan> Plans { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}

public enum PlayableTerrainViewDisposition
{
    SuppressedByDefault,
    RetainedLevelException,
    RetainedUnreviewedCandidate,
    RetainedFailOpen
}

public enum PlayableTerrainViewRole
{
    ForegroundOrContext,
    BroadCoplanarUnderlay
}

public sealed record PlayableTerrainViewGroup(
    string Signature,
    string LevelKey,
    int TextureId,
    float PlaneZ,
    string Surface,
    PlayableTerrainViewDisposition Disposition,
    int FaceCount,
    double GeometryFaceShare,
    double ProjectedFaceArea,
    double ProjectedFaceAreaShare,
    IReadOnlyList<string> TouchedSceneBoundsSides,
    int NativeDepthTwoOrFartherFaceCount,
    double NativeDepthTwoOrFartherFaceShare,
    IReadOnlyList<string> RuntimeKeys,
    string Reason);

public sealed record PlayableTerrainViewFaceClassification(
    PlayableTerrainViewRole Role,
    bool VisibleByDefault,
    PlayableTerrainViewGroup? Group,
    string Reason);

public sealed class PlayableTerrainViewPlan
{
    private readonly IReadOnlyDictionary<TerrainPolygon, PlayableTerrainViewGroup> _candidateGroupByFace;
    private readonly IReadOnlySet<TerrainPolygon> _suppressedFaces;

    internal PlayableTerrainViewPlan(
        int storedFaceCount,
        IReadOnlyList<TerrainPolygon> geometryFaces,
        IReadOnlyList<PlayableTerrainViewGroup> candidateGroups,
        bool failedOpen,
        string summary)
    {
        StoredFaceCount = Math.Max(0, storedFaceCount);
        CandidateGroups = candidateGroups;
        FailedOpen = failedOpen;
        Summary = summary;
        SuppressedGroups = candidateGroups
            .Where(group => group.Disposition == PlayableTerrainViewDisposition.SuppressedByDefault)
            .ToArray();
        RetainedGroups = candidateGroups
            .Where(group => group.Disposition != PlayableTerrainViewDisposition.SuppressedByDefault)
            .ToArray();

        Dictionary<string, PlayableTerrainViewGroup> candidateGroupByRuntimeKey = new(StringComparer.Ordinal);
        foreach (PlayableTerrainViewGroup group in CandidateGroups)
        {
            foreach (string runtimeKey in group.RuntimeKeys)
                candidateGroupByRuntimeKey.TryAdd(runtimeKey, group);
        }

        Dictionary<TerrainPolygon, PlayableTerrainViewGroup> candidateGroupByFace =
            new(ReferenceEqualityComparer.Instance);
        HashSet<TerrainPolygon> suppressedFaces = new(ReferenceEqualityComparer.Instance);
        foreach (TerrainPolygon face in geometryFaces)
        {
            // Runtime identity is resolved exactly once while constructing the
            // plan. Per-frame visibility/classification is reference-only and
            // therefore does not allocate an interpolated RuntimeKey string.
            if (!candidateGroupByRuntimeKey.TryGetValue(face.RuntimeKey, out PlayableTerrainViewGroup? group))
                continue;

            candidateGroupByFace.TryAdd(face, group);
            if (group.Disposition == PlayableTerrainViewDisposition.SuppressedByDefault)
                suppressedFaces.Add(face);
        }
        _candidateGroupByFace = candidateGroupByFace;
        _suppressedFaces = suppressedFaces;
        SuppressedFaceCount = suppressedFaces.Count;
        VisibleFaceCount = Math.Max(0, StoredFaceCount - SuppressedFaceCount);
    }

    public int StoredFaceCount { get; }
    public int VisibleFaceCount { get; }
    public int SuppressedFaceCount { get; }
    public int SuppressedSheetCount => SuppressedGroups.Count;
    public bool FailedOpen { get; }
    public string Summary { get; }
    public IReadOnlyList<PlayableTerrainViewGroup> CandidateGroups { get; }
    public IReadOnlyList<PlayableTerrainViewGroup> SuppressedGroups { get; }
    public IReadOnlyList<PlayableTerrainViewGroup> RetainedGroups { get; }

    public bool ShouldShow(TerrainPolygon polygon)
    {
        ArgumentNullException.ThrowIfNull(polygon);
        return polygon.IsTerrainEdited || !_suppressedFaces.Contains(polygon);
    }

    public PlayableTerrainViewFaceClassification Classify(TerrainPolygon polygon)
    {
        ArgumentNullException.ThrowIfNull(polygon);
        if (!_candidateGroupByFace.TryGetValue(polygon, out PlayableTerrainViewGroup? group))
        {
            return new PlayableTerrainViewFaceClassification(
                PlayableTerrainViewRole.ForegroundOrContext,
                true,
                null,
                "Face is not part of an audited oversized flat overview sheet.");
        }

        if (group.Disposition != PlayableTerrainViewDisposition.SuppressedByDefault)
        {
            return new PlayableTerrainViewFaceClassification(
                PlayableTerrainViewRole.BroadCoplanarUnderlay,
                true,
                group,
                group.Reason);
        }

        bool visible = polygon.IsTerrainEdited;
        return new PlayableTerrainViewFaceClassification(
            PlayableTerrainViewRole.BroadCoplanarUnderlay,
            visible,
            group,
            visible
                ? "Audited oversized overview face is retained because it has a staged terrain edit."
                : group.Reason);
    }

    internal static PlayableTerrainViewPlan FailOpen(int storedFaceCount, string reason) =>
        new(
            storedFaceCount,
            Array.Empty<TerrainPolygon>(),
            Array.Empty<PlayableTerrainViewGroup>(),
            failedOpen: true,
            reason);
}
