using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.Core.Analysis;

/// <summary>
/// Describes the texture evidence attached to a selected, nearby, or candidate terrain face.
/// Signatures are opaque stable strings supplied by the caller; metadata is matched by
/// normalized key and value.
/// </summary>
public sealed record TerrainTextureSuggestionTexture
{
    public required string LevelKey { get; init; }
    public required int TextureId { get; init; }
    public string DisplayName { get; init; } = "";
    public string Surface { get; init; } = "";
    public ColorRgba? PreviewColor { get; init; }
    public string PreviewSignature { get; init; } = "";
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}

public sealed record TerrainTextureSuggestionFace
{
    public required string RuntimeKey { get; init; }
    public required TerrainTextureSuggestionTexture Texture { get; init; }
}

public sealed record TerrainTextureSuggestionNeighbor
{
    public required TerrainTextureSuggestionFace Face { get; init; }
    public bool IsAdjacent { get; init; }

    /// <summary>
    /// Whole-face distance from the selected face. Adjacent faces use the fixed adjacent
    /// weight; non-adjacent faces use this value to reduce their influence.
    /// </summary>
    public int Distance { get; init; } = 1;
}

public sealed record TerrainTextureSuggestionCandidate
{
    public required TerrainTextureSuggestionTexture Texture { get; init; }
    public bool IsAvailable { get; init; } = true;
    public bool IsBlocked { get; init; }
    public string AvailabilityNote { get; init; } = "";
}

public sealed record TerrainTextureSuggestionRequest
{
    public required TerrainTextureSuggestionFace SelectedFace { get; init; }
    public IReadOnlyList<TerrainTextureSuggestionNeighbor> NearbyFaces { get; init; } =
        Array.Empty<TerrainTextureSuggestionNeighbor>();
    public required IReadOnlyList<TerrainTextureSuggestionCandidate> Candidates { get; init; }

    /// <summary>
    /// Suggestions are intentionally a compact palette of four through six entries.
    /// Fewer entries are returned when fewer eligible candidates exist.
    /// </summary>
    public int MaximumSuggestions { get; init; } = 6;
}

public sealed record TerrainTextureSuggestionScore(
    int LocalTextureReuse,
    int SurfaceFit,
    int PreviewSignatureFit,
    int MetadataFit,
    int PreviewColorFit)
{
    public int Total =>
        LocalTextureReuse +
        SurfaceFit +
        PreviewSignatureFit +
        MetadataFit +
        PreviewColorFit;
}

public sealed record TerrainTextureSuggestion(
    TerrainTextureSuggestionCandidate Candidate,
    TerrainTextureSuggestionScore Score,
    IReadOnlyList<string> Reasons);

/// <summary>
/// Produces a deterministic, context-aware shortlist of terrain textures. The engine does
/// not mutate faces or catalog entries, and it never returns the current, blocked, or
/// unavailable texture.
/// </summary>
public static class TerrainTextureSuggestionEngine
{
    private const int AdjacentWeight = 8;
    private const int SelectedColorWeight = 3;
    private const int MaximumColorDistanceSquared = (255 * 255) * 3;
    private const int MaximumColorScore = 4000;

    public static IReadOnlyList<TerrainTextureSuggestion> Rank(TerrainTextureSuggestionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.SelectedFace);
        ArgumentNullException.ThrowIfNull(request.NearbyFaces);
        ArgumentNullException.ThrowIfNull(request.Candidates);

        if (request.MaximumSuggestions is < 4 or > 6)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.MaximumSuggestions),
                request.MaximumSuggestions,
                "Terrain texture suggestions must request between four and six entries.");
        }

        ValidateFace(request.SelectedFace, nameof(request.SelectedFace));
        TextureIdentity selectedIdentity = IdentityFor(request.SelectedFace.Texture);
        IReadOnlyList<WeightedNeighbor> neighbors = PrepareNeighbors(request.SelectedFace, request.NearbyFaces);
        WeightedPreviewColor? targetColor = BuildTargetColor(request.SelectedFace.Texture, neighbors);

        List<ScoredCandidate> scoredCandidates = [];
        foreach (TerrainTextureSuggestionCandidate candidate in request.Candidates)
        {
            ArgumentNullException.ThrowIfNull(candidate);
            ArgumentNullException.ThrowIfNull(candidate.Texture);
            ValidateTexture(candidate.Texture, nameof(request.Candidates));

            if (!candidate.IsAvailable || candidate.IsBlocked)
                continue;

            TextureIdentity identity = IdentityFor(candidate.Texture);
            if (identity == selectedIdentity)
                continue;

            scoredCandidates.Add(ScoreCandidate(candidate, identity, request.SelectedFace.Texture, neighbors, targetColor));
        }

        // Candidate sources can occasionally contain duplicate catalog rows. Score them all,
        // then retain one deterministic best row for each source-level/texture identity.
        IEnumerable<ScoredCandidate> uniqueCandidates = scoredCandidates
            .GroupBy(candidate => candidate.Identity)
            .Select(group => group
                .OrderByDescending(candidate => candidate.Suggestion.Score.Total)
                .ThenBy(candidate => candidate.CanonicalKey, StringComparer.Ordinal)
                .First());

        return uniqueCandidates
            .OrderByDescending(candidate => candidate.Suggestion.Score.Total)
            .ThenBy(candidate => candidate.Identity.LevelKey, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.Identity.TextureId)
            .ThenBy(candidate => candidate.CanonicalKey, StringComparer.Ordinal)
            .Take(request.MaximumSuggestions)
            .Select(candidate => candidate.Suggestion)
            .ToArray();
    }

    private static ScoredCandidate ScoreCandidate(
        TerrainTextureSuggestionCandidate candidate,
        TextureIdentity identity,
        TerrainTextureSuggestionTexture selected,
        IReadOnlyList<WeightedNeighbor> neighbors,
        WeightedPreviewColor? targetColor)
    {
        TerrainTextureSuggestionTexture texture = candidate.Texture;
        int localTextureReuse = 0;
        int surfaceFit = 0;
        int previewSignatureFit = 0;
        int metadataFit = 0;
        int previewColorFit = 0;
        int localTextureMatches = 0;
        int localSurfaceMatches = 0;
        int localPreviewSignatureMatches = 0;

        if (TextMatches(texture.Surface, selected.Surface))
            surfaceFit += 1500;
        if (TextMatches(texture.PreviewSignature, selected.PreviewSignature))
            previewSignatureFit += 2200;
        metadataFit += CountMetadataMatches(texture.Metadata, selected.Metadata) * 250;

        foreach (WeightedNeighbor neighbor in neighbors)
        {
            TerrainTextureSuggestionTexture nearbyTexture = neighbor.Neighbor.Face.Texture;
            if (identity == IdentityFor(nearbyTexture))
            {
                localTextureMatches++;
                localTextureReuse += neighbor.Weight * 850;
            }

            if (TextMatches(texture.Surface, nearbyTexture.Surface))
            {
                localSurfaceMatches++;
                surfaceFit += neighbor.Weight * 200;
            }

            if (TextMatches(texture.PreviewSignature, nearbyTexture.PreviewSignature))
            {
                localPreviewSignatureMatches++;
                previewSignatureFit += neighbor.Weight * 300;
            }

            metadataFit += CountMetadataMatches(texture.Metadata, nearbyTexture.Metadata) * neighbor.Weight * 35;
        }

        if (texture.PreviewColor is ColorRgba candidateColor && targetColor is WeightedPreviewColor color)
        {
            int redDifference = candidateColor.R - color.R;
            int greenDifference = candidateColor.G - color.G;
            int blueDifference = candidateColor.B - color.B;
            int distanceSquared =
                (redDifference * redDifference) +
                (greenDifference * greenDifference) +
                (blueDifference * blueDifference);
            previewColorFit =
                ((MaximumColorDistanceSquared - distanceSquared) * MaximumColorScore) /
                MaximumColorDistanceSquared;
        }

        TerrainTextureSuggestionScore score = new(
            localTextureReuse,
            surfaceFit,
            previewSignatureFit,
            metadataFit,
            previewColorFit);
        List<string> reasons = [];
        if (localTextureMatches > 0)
            reasons.Add($"Used by {localTextureMatches} nearby face(s).");
        if (TextMatches(texture.Surface, selected.Surface))
            reasons.Add("Matches the selected face surface.");
        if (localSurfaceMatches > 0)
            reasons.Add($"Matches the surface of {localSurfaceMatches} nearby face(s).");
        if (TextMatches(texture.PreviewSignature, selected.PreviewSignature))
            reasons.Add("Matches the selected face preview signature.");
        if (localPreviewSignatureMatches > 0)
            reasons.Add($"Matches the preview signature of {localPreviewSignatureMatches} nearby face(s).");
        if (metadataFit > 0)
            reasons.Add("Shares texture metadata with the selected area.");
        if (previewColorFit > 0)
            reasons.Add("Preview color fits the selected area.");
        if (reasons.Count == 0)
            reasons.Add("Available texture candidate.");

        TerrainTextureSuggestion suggestion = new(candidate, score, reasons);
        return new ScoredCandidate(identity, suggestion, CanonicalKeyFor(candidate));
    }

    private static IReadOnlyList<WeightedNeighbor> PrepareNeighbors(
        TerrainTextureSuggestionFace selectedFace,
        IReadOnlyList<TerrainTextureSuggestionNeighbor> nearbyFaces)
    {
        string selectedRuntimeKey = NormalizeText(selectedFace.RuntimeKey);
        List<TerrainTextureSuggestionNeighbor> validated = [];
        foreach (TerrainTextureSuggestionNeighbor neighbor in nearbyFaces)
        {
            ArgumentNullException.ThrowIfNull(neighbor);
            ArgumentNullException.ThrowIfNull(neighbor.Face);
            ValidateFace(neighbor.Face, nameof(nearbyFaces));
            if (neighbor.Distance < 1)
                throw new ArgumentOutOfRangeException(nameof(nearbyFaces), "Nearby face distances must be at least one.");
            if (NormalizeText(neighbor.Face.RuntimeKey) == selectedRuntimeKey)
                continue;
            validated.Add(neighbor);
        }

        // A face contributes at most once. This also makes a caller's input order irrelevant
        // when its adjacency search returns the same runtime face through multiple paths.
        return validated
            .GroupBy(neighbor => NormalizeText(neighbor.Face.RuntimeKey), StringComparer.Ordinal)
            .Select(group => group
                .OrderByDescending(neighbor => neighbor.IsAdjacent)
                .ThenBy(neighbor => neighbor.Distance)
                .ThenBy(neighbor => CanonicalKeyFor(neighbor.Face.Texture), StringComparer.Ordinal)
                .First())
            .Select(neighbor => new WeightedNeighbor(neighbor, WeightFor(neighbor)))
            .OrderBy(neighbor => NormalizeText(neighbor.Neighbor.Face.RuntimeKey), StringComparer.Ordinal)
            .ToArray();
    }

    private static WeightedPreviewColor? BuildTargetColor(
        TerrainTextureSuggestionTexture selected,
        IReadOnlyList<WeightedNeighbor> neighbors)
    {
        long totalWeight = 0;
        long red = 0;
        long green = 0;
        long blue = 0;

        if (selected.PreviewColor is ColorRgba selectedColor)
            AddColor(selectedColor, SelectedColorWeight);

        foreach (WeightedNeighbor neighbor in neighbors)
        {
            if (neighbor.Neighbor.Face.Texture.PreviewColor is ColorRgba nearbyColor)
                AddColor(nearbyColor, neighbor.Weight);
        }

        if (totalWeight == 0)
            return null;

        return new WeightedPreviewColor(
            (int)((red + (totalWeight / 2)) / totalWeight),
            (int)((green + (totalWeight / 2)) / totalWeight),
            (int)((blue + (totalWeight / 2)) / totalWeight));

        void AddColor(ColorRgba color, int weight)
        {
            totalWeight += weight;
            red += color.R * weight;
            green += color.G * weight;
            blue += color.B * weight;
        }
    }

    private static int CountMetadataMatches(
        IReadOnlyDictionary<string, string> candidateMetadata,
        IReadOnlyDictionary<string, string> contextMetadata)
    {
        if (candidateMetadata.Count == 0 || contextMetadata.Count == 0)
            return 0;

        Dictionary<string, string> normalizedContext = contextMetadata
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
            .GroupBy(pair => NormalizeText(pair.Key), StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(pair => NormalizeText(pair.Value)).OrderBy(value => value, StringComparer.Ordinal).First(),
                StringComparer.Ordinal);

        return candidateMetadata
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
            .Select(pair => new KeyValuePair<string, string>(NormalizeText(pair.Key), NormalizeText(pair.Value)))
            .Distinct()
            .Count(pair =>
                normalizedContext.TryGetValue(pair.Key, out string? contextValue) &&
                contextValue == pair.Value);
    }

    private static int WeightFor(TerrainTextureSuggestionNeighbor neighbor)
    {
        if (neighbor.IsAdjacent)
            return AdjacentWeight;

        return Math.Clamp(5 - neighbor.Distance, 1, 4);
    }

    private static bool TextMatches(string? left, string? right)
    {
        string normalizedLeft = NormalizeText(left);
        return normalizedLeft.Length > 0 && normalizedLeft == NormalizeText(right);
    }

    private static void ValidateFace(TerrainTextureSuggestionFace face, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(face.RuntimeKey))
            throw new ArgumentException("Terrain texture suggestion faces need a non-empty runtime key.", parameterName);
        ArgumentNullException.ThrowIfNull(face.Texture);
        ValidateTexture(face.Texture, parameterName);
    }

    private static void ValidateTexture(TerrainTextureSuggestionTexture texture, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(LevelCatalog.NormalizeKey(texture.LevelKey)))
            throw new ArgumentException("Terrain texture suggestion entries need a non-empty level key.", parameterName);
        if (texture.TextureId < 0)
            throw new ArgumentOutOfRangeException(parameterName, "Terrain texture suggestion IDs cannot be negative.");
        ArgumentNullException.ThrowIfNull(texture.Metadata);
    }

    private static TextureIdentity IdentityFor(TerrainTextureSuggestionTexture texture)
    {
        return new TextureIdentity(LevelCatalog.NormalizeKey(texture.LevelKey), texture.TextureId);
    }

    private static string CanonicalKeyFor(TerrainTextureSuggestionCandidate candidate)
    {
        return $"{CanonicalKeyFor(candidate.Texture)}\u001f{NormalizeText(candidate.AvailabilityNote)}";
    }

    private static string CanonicalKeyFor(TerrainTextureSuggestionTexture texture)
    {
        string previewColor = texture.PreviewColor is ColorRgba color
            ? $"{color.R:D3},{color.G:D3},{color.B:D3},{color.A:D3}"
            : "none";
        string metadata = string.Join(
            "\u001e",
            texture.Metadata
                .Select(pair => $"{NormalizeText(pair.Key)}={NormalizeText(pair.Value)}")
                .OrderBy(value => value, StringComparer.Ordinal));
        return string.Join(
            "\u001f",
            LevelCatalog.NormalizeKey(texture.LevelKey),
            texture.TextureId.ToString("D10", System.Globalization.CultureInfo.InvariantCulture),
            NormalizeText(texture.DisplayName),
            NormalizeText(texture.Surface),
            NormalizeText(texture.PreviewSignature),
            previewColor,
            metadata);
    }

    private static string NormalizeText(string? value)
    {
        return (value ?? "").Trim().ToLowerInvariant();
    }

    private readonly record struct TextureIdentity(string LevelKey, int TextureId);
    private sealed record WeightedNeighbor(TerrainTextureSuggestionNeighbor Neighbor, int Weight);
    private sealed record WeightedPreviewColor(int R, int G, int B);
    private sealed record ScoredCandidate(
        TextureIdentity Identity,
        TerrainTextureSuggestion Suggestion,
        string CanonicalKey);
}
