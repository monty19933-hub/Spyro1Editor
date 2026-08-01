using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Primitives;

List<string> failures = [];

RunContextRankingChecks(failures);
RunDeterminismChecks(failures);
RunColorAndDuplicateChecks(failures);
RunValidationChecks(failures);

if (failures.Count > 0)
{
    Console.Error.WriteLine("Terrain texture suggestion smoke failed:");
    foreach (string failure in failures)
        Console.Error.WriteLine($"- {failure}");
    return 1;
}

Console.WriteLine("Terrain texture suggestion smoke passed.");
Console.WriteLine("- current, blocked, and unavailable candidates excluded");
Console.WriteLine("- adjacent texture, surface, preview signature, metadata, and color evidence ranked");
Console.WriteLine("- cross-level same-ID candidate retained");
Console.WriteLine("- four-to-six output cap and deterministic tie-breaking verified");
Console.WriteLine("- duplicate identities collapsed deterministically");
return 0;

static void RunContextRankingChecks(List<string> failures)
{
    TerrainTextureSuggestionFace selected = Face(
        "95:3:hp",
        Texture("artisans", 5, "Current grass", "grass", "#306430", "moss", ("realm", "temperate")));
    TerrainTextureSuggestionNeighbor[] neighbors =
    [
        Neighbor(
            Face(
                "95:4:hp",
                Texture("artisans", 6, "Nearby grass", "grass", "#326832", "moss", ("realm", "temperate"))),
            adjacent: true),
        Neighbor(
            Face(
                "96:1:hp",
                Texture("artisans", 7, "Nearby stone", "stone", "#77706C", "rock", ("realm", "temperate"))),
            adjacent: false,
            distance: 2)
    ];
    TerrainTextureSuggestionCandidate current = Candidate(selected.Texture);
    TerrainTextureSuggestionCandidate blocked = Candidate(
        Texture("gnastysworld", 10, "Blocked perfect match", "grass", "#316531", "moss", ("realm", "temperate")),
        blocked: true);
    TerrainTextureSuggestionCandidate unavailable = Candidate(
        Texture("darkhollow", 11, "Unavailable perfect match", "grass", "#316531", "moss", ("realm", "temperate")),
        available: false);
    TerrainTextureSuggestionCandidate nearbyReuse = Candidate(neighbors[0].Face.Texture);
    TerrainTextureSuggestionCandidate crossLevelSameId = Candidate(
        Texture("gnastysworld", 5, "Cross-level same ID", "grass", "#306430", "moss", ("realm", "temperate")));
    TerrainTextureSuggestionCandidate[] candidates =
    [
        current,
        blocked,
        unavailable,
        Candidate(Texture("clifftown", 12, "Sand", "sand", "#B99252", "sand")),
        Candidate(Texture("darkhollow", 4, "Dark grass", "grass", "#295729", "moss")),
        nearbyReuse,
        Candidate(Texture("toasty", 2, "Brick", "stone", "#80685F", "brick")),
        crossLevelSameId,
        Candidate(Texture("stonehill", 8, "Grass", "grass", "#3B733A", "moss")),
        Candidate(Texture("wizardpeak", 9, "Snow", "snow", "#D6E2E7", "snow"))
    ];

    IReadOnlyList<TerrainTextureSuggestion> ranked = TerrainTextureSuggestionEngine.Rank(
        new TerrainTextureSuggestionRequest
        {
            SelectedFace = selected,
            NearbyFaces = neighbors,
            Candidates = candidates
        });

    if (ranked.Count != 6)
        failures.Add($"Context ranking expected six suggestions, found {ranked.Count}.");
    if (ranked.Any(result => ReferenceEquals(result.Candidate, current)))
        failures.Add("Context ranking returned the current texture.");
    if (ranked.Any(result => ReferenceEquals(result.Candidate, blocked)))
        failures.Add("Context ranking returned a blocked texture.");
    if (ranked.Any(result => ReferenceEquals(result.Candidate, unavailable)))
        failures.Add("Context ranking returned an unavailable texture.");
    if (!ranked.Any(result => ReferenceEquals(result.Candidate, crossLevelSameId)))
        failures.Add("Context ranking incorrectly excluded a same-ID texture from another level.");
    if (ranked.Count > 0 && !ReferenceEquals(ranked[0].Candidate, nearbyReuse))
        failures.Add($"Expected the adjacent texture reuse first, found {Label(ranked[0])}.");
    if (ranked.Count > 0 && ranked[0].Score.LocalTextureReuse <= 0)
        failures.Add("Top adjacent texture has no local-reuse score.");
    if (ranked.Any(result => result.Score.Total !=
        result.Score.LocalTextureReuse +
        result.Score.SurfaceFit +
        result.Score.PreviewSignatureFit +
        result.Score.MetadataFit +
        result.Score.PreviewColorFit))
    {
        failures.Add("A suggestion total does not equal its score breakdown.");
    }

    IReadOnlyList<TerrainTextureSuggestion> four = TerrainTextureSuggestionEngine.Rank(
        new TerrainTextureSuggestionRequest
        {
            SelectedFace = selected,
            NearbyFaces = neighbors,
            Candidates = candidates,
            MaximumSuggestions = 4
        });
    if (four.Count != 4)
        failures.Add($"Four-entry request returned {four.Count} suggestions.");

    IReadOnlyList<TerrainTextureSuggestion> sparse = TerrainTextureSuggestionEngine.Rank(
        new TerrainTextureSuggestionRequest
        {
            SelectedFace = selected,
            Candidates =
            [
                current,
                Candidate(Texture("darkhollow", 1, "One")),
                Candidate(Texture("darkhollow", 2, "Two"))
            ]
        });
    if (sparse.Count != 2)
        failures.Add($"Sparse ranking should return its two eligible candidates without padding, found {sparse.Count}.");
}

static void RunDeterminismChecks(List<string> failures)
{
    TerrainTextureSuggestionFace selected = Face("1:1:hp", Texture("current", 0, "Current"));
    TerrainTextureSuggestionCandidate[] candidates =
    [
        Candidate(Texture("zeta", 1, "Zeta")),
        Candidate(Texture("alpha", 4, "Alpha four")),
        Candidate(Texture("alpha", 2, "Alpha two")),
        Candidate(Texture("gamma", 9, "Gamma")),
        Candidate(Texture("beta", 3, "Beta")),
        Candidate(Texture("delta", 8, "Delta")),
        Candidate(Texture("epsilon", 7, "Epsilon"))
    ];

    string[] forward = TerrainTextureSuggestionEngine.Rank(
            new TerrainTextureSuggestionRequest
            {
                SelectedFace = selected,
                Candidates = candidates
            })
        .Select(Identity)
        .ToArray();
    string[] reverse = TerrainTextureSuggestionEngine.Rank(
            new TerrainTextureSuggestionRequest
            {
                SelectedFace = selected,
                Candidates = candidates.Reverse().ToArray()
            })
        .Select(Identity)
        .ToArray();

    if (!forward.SequenceEqual(reverse, StringComparer.Ordinal))
        failures.Add("Candidate input order changed deterministic ranking.");
    string[] expected = ["alpha:2", "alpha:4", "beta:3", "delta:8", "epsilon:7", "gamma:9"];
    if (!forward.SequenceEqual(expected, StringComparer.Ordinal))
        failures.Add($"Tie-break order was '{string.Join(", ", forward)}', expected '{string.Join(", ", expected)}'.");
}

static void RunColorAndDuplicateChecks(List<string> failures)
{
    TerrainTextureSuggestionFace selected = Face(
        "2:2:hp",
        Texture("artisans", 1, "Selected", previewColor: "#646464"));
    TerrainTextureSuggestionNeighbor nearby = Neighbor(
        Face("2:3:hp", Texture("artisans", 2, "Neighbor", previewColor: "#686868")),
        adjacent: true);
    TerrainTextureSuggestionCandidate goodDuplicate = Candidate(
        Texture("darkhollow", 3, "Good duplicate", previewColor: "#666666"));
    TerrainTextureSuggestionCandidate badDuplicate = Candidate(
        Texture("darkhollow", 3, "Bad duplicate", previewColor: "#FFFFFF"));
    TerrainTextureSuggestionCandidate far = Candidate(
        Texture("stonehill", 4, "Far color", previewColor: "#000000"));
    TerrainTextureSuggestionCandidate noPreview = Candidate(Texture("toasty", 5, "No preview"));
    TerrainTextureSuggestionCandidate[] candidates =
    [
        badDuplicate,
        far,
        noPreview,
        goodDuplicate,
        Candidate(Texture("clifftown", 6, "Other")),
        Candidate(Texture("wizardpeak", 7, "Other two")),
        Candidate(Texture("blowhard", 8, "Other three"))
    ];

    IReadOnlyList<TerrainTextureSuggestion> ranked = TerrainTextureSuggestionEngine.Rank(
        new TerrainTextureSuggestionRequest
        {
            SelectedFace = selected,
            NearbyFaces = [nearby],
            Candidates = candidates
        });

    TerrainTextureSuggestion[] duplicateRows = ranked
        .Where(result => Identity(result) == "darkhollow:3")
        .ToArray();
    if (duplicateRows.Length != 1)
        failures.Add($"Duplicate identity was returned {duplicateRows.Length} times.");
    else if (!ReferenceEquals(duplicateRows[0].Candidate, goodDuplicate))
        failures.Add("Duplicate identity did not retain its deterministic highest-scoring row.");
    if (ranked.Count > 0 && !ReferenceEquals(ranked[0].Candidate, goodDuplicate))
        failures.Add($"Closest preview color should rank first, found {Label(ranked[0])}.");

    IReadOnlyList<TerrainTextureSuggestion> reversed = TerrainTextureSuggestionEngine.Rank(
        new TerrainTextureSuggestionRequest
        {
            SelectedFace = selected,
            NearbyFaces = [nearby],
            Candidates = candidates.Reverse().ToArray()
        });
    if (!ranked.Select(Identity).SequenceEqual(reversed.Select(Identity), StringComparer.Ordinal))
        failures.Add("Duplicate candidate input order changed ranking.");
}

static void RunValidationChecks(List<string> failures)
{
    TerrainTextureSuggestionFace selected = Face("3:1:hp", Texture("artisans", 1, "Selected"));
    TerrainTextureSuggestionCandidate[] candidates =
    [
        Candidate(Texture("stonehill", 1, "One")),
        Candidate(Texture("stonehill", 2, "Two")),
        Candidate(Texture("stonehill", 3, "Three")),
        Candidate(Texture("stonehill", 4, "Four"))
    ];

    try
    {
        TerrainTextureSuggestionEngine.Rank(
            new TerrainTextureSuggestionRequest
            {
                SelectedFace = selected,
                Candidates = candidates,
                MaximumSuggestions = 3
            });
        failures.Add("A three-entry suggestion request was not rejected.");
    }
    catch (ArgumentOutOfRangeException)
    {
    }

    try
    {
        TerrainTextureSuggestionEngine.Rank(
            new TerrainTextureSuggestionRequest
            {
                SelectedFace = selected,
                NearbyFaces =
                [
                    Neighbor(Face("3:2:hp", Texture("artisans", 2, "Neighbor")), adjacent: false, distance: 0)
                ],
                Candidates = candidates
            });
        failures.Add("A zero-distance nearby face was not rejected.");
    }
    catch (ArgumentOutOfRangeException)
    {
    }
}

static TerrainTextureSuggestionTexture Texture(
    string levelKey,
    int textureId,
    string displayName,
    string surface = "",
    string? previewColor = null,
    string previewSignature = "",
    params (string Key, string Value)[] metadata)
{
    ColorRgba? color = null;
    if (previewColor != null)
    {
        if (!ColorRgba.TryParseHex(previewColor, out ColorRgba parsed))
            throw new InvalidOperationException($"Invalid smoke preview color '{previewColor}'.");
        color = parsed;
    }

    return new TerrainTextureSuggestionTexture
    {
        LevelKey = levelKey,
        TextureId = textureId,
        DisplayName = displayName,
        Surface = surface,
        PreviewColor = color,
        PreviewSignature = previewSignature,
        Metadata = metadata.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase)
    };
}

static TerrainTextureSuggestionFace Face(string runtimeKey, TerrainTextureSuggestionTexture texture)
{
    return new TerrainTextureSuggestionFace
    {
        RuntimeKey = runtimeKey,
        Texture = texture
    };
}

static TerrainTextureSuggestionNeighbor Neighbor(
    TerrainTextureSuggestionFace face,
    bool adjacent,
    int distance = 1)
{
    return new TerrainTextureSuggestionNeighbor
    {
        Face = face,
        IsAdjacent = adjacent,
        Distance = distance
    };
}

static TerrainTextureSuggestionCandidate Candidate(
    TerrainTextureSuggestionTexture texture,
    bool available = true,
    bool blocked = false)
{
    return new TerrainTextureSuggestionCandidate
    {
        Texture = texture,
        IsAvailable = available,
        IsBlocked = blocked
    };
}

static string Identity(TerrainTextureSuggestion suggestion)
{
    return $"{NormalizeLevelKey(suggestion.Candidate.Texture.LevelKey)}:{suggestion.Candidate.Texture.TextureId}";
}

static string Label(TerrainTextureSuggestion suggestion)
{
    return $"{suggestion.Candidate.Texture.LevelKey} texture {suggestion.Candidate.Texture.TextureId}";
}

static string NormalizeLevelKey(string value)
{
    return value.Trim().Replace(" ", "", StringComparison.Ordinal).Replace("_", "", StringComparison.Ordinal).ToLowerInvariant();
}
