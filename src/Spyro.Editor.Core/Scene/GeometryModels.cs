using System.Globalization;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Rendering;

namespace Spyro.Editor.Core.Scene;

public sealed class GeometryCandidate
{
    public string Name { get; init; } = "geometry";
    public Rect2f Bounds { get; set; } = Rect2f.Empty;
    public float MinZ { get; init; }
    public float MaxZ { get; init; }
    public List<TerrainPolygon> Polygons { get; } = new();
    public List<TerrainEdge> Edges { get; } = new();
    public List<LowDetailTerrainPolygon> LowDetailPolygons { get; } = new();
    public List<SceneSectorRenderMetadata> SourceSectors { get; } = new();
    public bool HasStaticLowDetailPreview { get; set; }
    public string TerrainLodPreviewContract { get; set; } = "";
    public bool HasExactHighPolyMaterialState { get; set; }
    public string HighPolyMaterialContract { get; set; } = "";
    public bool HasExactHighPolyCoordinateState { get; set; }
    public string HighPolyCoordinateContract { get; set; } = "";
    public NativeTerrainOcclusionData? NativeTerrainOcclusion { get; set; }
}

public sealed record SceneSectorRenderMetadata(
    int SectorIndex,
    int SectorOffset,
    Vector3f Center,
    int Radius,
    bool DisableLowDetail,
    bool DisableHighDetail,
    bool ForceLowDetail,
    int LowDetailVertexCount,
    int LowDetailColorCount,
    int LowDetailFaceCount,
    int HighDetailVertexCount,
    int HighDetailColorCount,
    int HighDetailFaceCount,
    NativeTerrainHpSectorCoordinatePayload? HighPolyCoordinates = null);

public sealed class LowDetailTerrainPolygon
{
    public LowDetailTerrainPolygon(
        IReadOnlyList<Vector2f> points,
        IReadOnlyList<float> zValues,
        int sectorIndex,
        int faceIndex,
        int sectorOffset,
        int faceOffset,
        IReadOnlyList<int> vertexIndexes,
        IReadOnlyList<int> cornerPointIndexes,
        IReadOnlyList<int> colorIndexes,
        IReadOnlyList<ColorRgba> cornerColors,
        uint rawWord0,
        uint rawWord1,
        int transitionBias,
        bool doubleSided,
        bool semiTransparent,
        int blendMode,
        int orderingTableBias)
    {
        Points = points.ToArray();
        ZValues = zValues.ToArray();
        SectorIndex = sectorIndex;
        FaceIndex = faceIndex;
        SectorOffset = sectorOffset;
        FaceOffset = faceOffset;
        VertexIndexes = vertexIndexes.ToArray();
        CornerPointIndexes = cornerPointIndexes.ToArray();
        ColorIndexes = colorIndexes.ToArray();
        CornerColors = cornerColors.ToArray();
        RawWord0 = rawWord0;
        RawWord1 = rawWord1;
        TransitionBias = transitionBias & 0x1F;
        DoubleSided = doubleSided;
        SemiTransparent = semiTransparent;
        BlendMode = blendMode & 0x03;
        OrderingTableBias = orderingTableBias & 0x1F;
        Bounds = CalculateBounds(Points);
        AvgZ = ZValues.Count == 0 ? 0 : ZValues.Average();
    }

    public IReadOnlyList<Vector2f> Points { get; }
    public IReadOnlyList<float> ZValues { get; }
    public int SectorIndex { get; }
    public int FaceIndex { get; }
    public int SectorOffset { get; }
    public int FaceOffset { get; }
    public IReadOnlyList<int> VertexIndexes { get; }
    public IReadOnlyList<int> CornerPointIndexes { get; }
    public IReadOnlyList<int> ColorIndexes { get; }
    public IReadOnlyList<ColorRgba> CornerColors { get; }
    public uint RawWord0 { get; }
    public uint RawWord1 { get; }
    public int TransitionBias { get; }
    public bool DoubleSided { get; }
    public bool SemiTransparent { get; }
    public int BlendMode { get; }
    public int OrderingTableBias { get; }
    public Rect2f Bounds { get; }
    public float AvgZ { get; }
    public string RuntimeKey => $"{SectorIndex}:{FaceIndex}:lp";

    public bool HasCompleteNativePayload =>
        Points.Count is >= 3 and <= SourceSceneOverlayContract.CornerSlotCount &&
        ZValues.Count == Points.Count &&
        VertexIndexes.Count == SourceSceneOverlayContract.CornerSlotCount &&
        CornerPointIndexes.Count == SourceSceneOverlayContract.CornerSlotCount &&
        ColorIndexes.Count == SourceSceneOverlayContract.CornerSlotCount &&
        CornerColors.Count == SourceSceneOverlayContract.CornerSlotCount &&
        CornerPointIndexes.All(index => index >= 0 && index < Points.Count);

    private static Rect2f CalculateBounds(IReadOnlyList<Vector2f> points)
    {
        if (points.Count == 0)
            return Rect2f.Empty;
        return Rect2f.FromBounds(
            points.Min(point => point.X),
            points.Min(point => point.Y),
            points.Max(point => point.X),
            points.Max(point => point.Y));
    }
}

public enum TerrainStructureEditKind
{
    None,
    RemoveFace,
    AddCloneFace
}

public sealed record TerrainSurfaceBehaviorEdit(
    int SurfaceType,
    int Param1,
    int Param2,
    string SourceLevelKey,
    string SourceRuntimeKey,
    string Label);

public sealed record TerrainTextureVisualCorner(
    ColorRgba NearColor,
    ColorRgba FarColor);

public sealed record TerrainTextureVisualEdit(
    int SourceTextureId,
    string SourceLevelKey,
    string SourceRuntimeKey,
    int SourceSectorOffset,
    int SourceFaceOffset,
    TerrainTextureVisualCorner Corner0,
    TerrainTextureVisualCorner Corner1,
    TerrainTextureVisualCorner Corner2,
    TerrainTextureVisualCorner Corner3,
    string Label)
{
    public IReadOnlyList<TerrainTextureVisualCorner> Corners =>
        [Corner0, Corner1, Corner2, Corner3];

    public int UniqueCornerPairCount => Corners.Distinct().Count();
    public ColorRgba AverageNearColor => Average(Corners.Select(corner => corner.NearColor));
    public ColorRgba AverageFarColor => Average(Corners.Select(corner => corner.FarColor));

    private static ColorRgba Average(IEnumerable<ColorRgba> colors)
    {
        ColorRgba[] values = colors.ToArray();
        if (values.Length == 0)
            return ColorRgba.FromRgb(96, 128, 96);

        return ColorRgba.FromRgb(
            values.Sum(color => color.R) / values.Length,
            values.Sum(color => color.G) / values.Length,
            values.Sum(color => color.B) / values.Length);
    }
}

public sealed class TerrainPolygon
{
    public TerrainPolygon(
        IReadOnlyList<Vector2f> points,
        IReadOnlyList<float> zValues,
        int textureId,
        int sectorIndex,
        int faceIndex,
        string detail,
        ColorRgba faceColor,
        int sectorOffset = -1,
        int faceOffset = -1,
        IReadOnlyList<int>? vertexIndexes = null,
        string word3 = "",
        string word4 = "",
        bool faceFlip = false,
        int faceDepth = -1,
        IReadOnlyList<int>? colourIndexes = null,
        IReadOnlyList<ColorRgba>? nearColors = null,
        IReadOnlyList<ColorRgba>? farColors = null,
        IReadOnlyList<int>? cornerPointIndexes = null,
        uint? nativeFaceWord2 = null,
        int nativeMaterialByte = -1,
        bool? nativeUntexturedSentinel = null,
        bool? nativePrimitiveSemiTransparent = null,
        int nativeTextureId = -1,
        uint? nativeFaceWord0 = null,
        uint? nativeFaceWord1 = null,
        uint? nativeFaceWord3 = null)
    {
        _points = points.ToArray();
        OriginalPoints = points.ToArray();
        ZValues = zValues.ToArray();
        OriginalZValues = zValues.ToArray();
        TextureId = textureId;
        OriginalTextureId = textureId;
        SectorIndex = sectorIndex;
        FaceIndex = faceIndex;
        Detail = detail;
        SectorOffset = sectorOffset;
        FaceOffset = faceOffset;
        VertexIndexes = vertexIndexes?.ToArray() ?? Array.Empty<int>();
        ColourIndexes = colourIndexes?.ToArray() ?? Array.Empty<int>();
        Word3 = word3;
        Word4 = word4;
        bool hasCompleteNativeFaceWordPayload =
            nativeFaceWord0.HasValue &&
            nativeFaceWord1.HasValue &&
            nativeFaceWord2.HasValue &&
            nativeFaceWord3.HasValue;
        bool hasNewNativeFaceWordPayload =
            nativeFaceWord0.HasValue || nativeFaceWord1.HasValue || nativeFaceWord3.HasValue;
        if (hasNewNativeFaceWordPayload && !hasCompleteNativeFaceWordPayload)
            throw new InvalidDataException("HP face has an incomplete typed raw-word 0-3 payload.");

        NativeFaceWord0 = nativeFaceWord0 ?? PackNativeByteSlotsOrZero(VertexIndexes);
        NativeFaceWord1 = nativeFaceWord1 ?? PackNativeByteSlotsOrZero(ColourIndexes);
        NativeFaceWord2 = nativeFaceWord2 ?? ParseNativeWord(word3);
        NativeFaceWord3 = nativeFaceWord3 ?? ParseNativeWord(word4);
        HasCompleteNativeHighPolyFacePayload = hasCompleteNativeFaceWordPayload;
        if (HasCompleteNativeHighPolyFacePayload)
        {
            if (!TryParseNativeWord(word3, out uint legacyWord2) || legacyWord2 != NativeFaceWord2 ||
                !TryParseNativeWord(word4, out uint legacyWord3) || legacyWord3 != NativeFaceWord3 ||
                !TryPackNativeByteSlots(VertexIndexes, out uint packedWord0) || packedWord0 != NativeFaceWord0 ||
                !TryPackNativeByteSlots(ColourIndexes, out uint packedWord1) || packedWord1 != NativeFaceWord1 ||
                faceFlip != ((NativeFaceWord3 & 0x02) != 0) ||
                faceDepth != (int)((NativeFaceWord3 >> 3) & 0x1F))
            {
                throw new InvalidDataException("HP face raw-word 0-3 payload does not match its serialized slots, legacy words, flip, or depth fields.");
            }
        }

        PsxTerrainPrimitiveClassification nativeMaterial =
            PsxTerrainBlendKernel.ClassifyHighPolyMaterialByte((byte)(NativeFaceWord2 & 0xFF));
        NativeMaterialByte = nativeMaterial.RawMaterialByte;
        IsNativeUntexturedSentinel = nativeMaterial.IsOpaqueUntexturedSentinel;
        NativePrimitiveSemiTransparent = nativeMaterial.PrimitiveSemiTransparent;
        NativeTextureId = nativeMaterial.TextureId;
        HasNativeHighPolyMaterialPayload = nativeFaceWord2.HasValue;
        if (HasNativeHighPolyMaterialPayload &&
            (nativeMaterialByte != NativeMaterialByte ||
             nativeUntexturedSentinel != IsNativeUntexturedSentinel ||
             nativePrimitiveSemiTransparent != NativePrimitiveSemiTransparent ||
             nativeTextureId != NativeTextureId ||
             textureId != NativeTextureId))
        {
            throw new InvalidDataException(
                $"HP face material payload does not match native word +0x08 0x{NativeFaceWord2:X8}.");
        }
        FaceFlip = faceFlip;
        FaceDepth = faceDepth;
        NearColors = nearColors?.ToArray() ?? Array.Empty<ColorRgba>();
        FarColors = farColors?.ToArray() ?? Array.Empty<ColorRgba>();
        CornerPointIndexes = cornerPointIndexes?.ToArray() ?? BuildCornerPointIndexes(VertexIndexes);
        FaceColor = faceColor;
        SurfaceColor = faceColor;
        Bounds = CalculateBounds(Points);
        Center = CalculateCenter(Points);
        RecalculateZStats();
    }

    private readonly Vector2f[] _points;

    public IReadOnlyList<Vector2f> Points => _points;
    public IReadOnlyList<Vector2f> OriginalPoints { get; }
    public float[] ZValues { get; }
    public float[] OriginalZValues { get; }
    public int TextureId { get; private set; }
    public int OriginalTextureId { get; }
    public int SectorIndex { get; }
    public int FaceIndex { get; }
    public string Detail { get; }
    public int SectorOffset { get; }
    public int FaceOffset { get; }
    public IReadOnlyList<int> VertexIndexes { get; }
    public string Word3 { get; }
    public string Word4 { get; }
    /// <summary>The zero-based first native HP face word at +0x00 (four little-endian vertex slots).</summary>
    public uint NativeFaceWord0 { get; }
    /// <summary>The zero-based second native HP face word at +0x04 (four little-endian color slots).</summary>
    public uint NativeFaceWord1 { get; }
    /// <summary>The zero-based third native HP face word at +0x08 (legacy serialized name: Word3).</summary>
    public uint NativeFaceWord2 { get; }
    /// <summary>The native face material byte at +0x08.</summary>
    public byte NativeMaterialByte { get; }
    public bool IsNativeUntexturedSentinel { get; }
    public bool NativePrimitiveSemiTransparent { get; }
    public int NativeTextureId { get; }
    public bool HasNativeHighPolyMaterialPayload { get; }
    public bool HasCompleteNativeHighPolyFacePayload { get; }
    /// <summary>The zero-based fourth native HP face word at +0x0C (legacy serialized name: Word4).</summary>
    public uint NativeFaceWord3 { get; }
    public bool LqFadeBypass => (NativeFaceWord3 & 0x01) != 0;
    public bool HqOverlayBypass => (NativeFaceWord3 & 0xC0) != 0;
    public bool FaceFlip { get; }
    public int FaceDepth { get; }
    public IReadOnlyList<int> ColourIndexes { get; }
    /// <summary>
    /// Maps each raw face slot (0..3) to the corresponding entry in <see cref="Points"/>.
    /// A native triangle repeats slots 0 and 1, so both map to the same point.
    /// </summary>
    public IReadOnlyList<int> CornerPointIndexes { get; }
    /// <summary>
    /// Legacy serialized NearColors field: physical HP color table 2, used by
    /// the retail DPCS path as the runtime-far endpoint, in raw slot order.
    /// </summary>
    public IReadOnlyList<ColorRgba> NearColors { get; }
    /// <summary>
    /// Legacy serialized FarColors field: physical HP color table 1, used by
    /// the retail DPCS path as the runtime-near endpoint, in raw slot order.
    /// </summary>
    public IReadOnlyList<ColorRgba> FarColors { get; }
    public ColorRgba FaceColor { get; }
    public string Surface { get; private set; } = "unknown";
    public string SurfaceSource { get; private set; } = "unclassified";
    public string Behavior { get; private set; } = "unknown";
    public string BehaviorSource { get; private set; } = "unclassified";
    public string BehaviorConfidence { get; private set; } = "unknown";
    public string BehaviorNote { get; private set; } = "";
    public ColorRgba SurfaceColor { get; private set; }
    public Rect2f Bounds { get; private set; }
    public Vector2f Center { get; private set; }
    public float AvgZ { get; private set; }
    public float MinZ { get; private set; }
    public float MaxZ { get; private set; }
    public float TerrainEditDeltaZ { get; private set; }
    public TerrainSurfaceBehaviorEdit? SurfaceBehaviorEdit { get; private set; }
    public TerrainTextureVisualEdit? TextureVisualEdit { get; private set; }

    public bool HasTextureEdit => OriginalTextureId >= 0 && TextureId != OriginalTextureId;
    public bool HasNativeCornerPayload =>
        VertexIndexes.Count == SourceSceneOverlayContract.CornerSlotCount &&
        CornerPointIndexes.Count == SourceSceneOverlayContract.CornerSlotCount &&
        ColourIndexes.Count == SourceSceneOverlayContract.CornerSlotCount &&
        NearColors.Count == SourceSceneOverlayContract.CornerSlotCount &&
        FarColors.Count == SourceSceneOverlayContract.CornerSlotCount &&
        HasConsistentCornerTopology();
    public bool HasTextureVisualEdit => TextureVisualEdit != null;
    public bool HasHeightEdit => HasEditedZValues();
    public bool HasPositionEdit => HasEditedPoints();
    public TerrainStructureEditKind StructureEdit { get; private set; } = TerrainStructureEditKind.None;
    public bool HasStructureEdit => StructureEdit != TerrainStructureEditKind.None;
    public bool IsTerrainRemoved => StructureEdit == TerrainStructureEditKind.RemoveFace;
    public bool IsTerrainAddClone => StructureEdit == TerrainStructureEditKind.AddCloneFace;
    public bool HasSurfaceBehaviorEdit => SurfaceBehaviorEdit != null;
    public bool IsTerrainEdited => HasHeightEdit || HasPositionEdit || HasTextureEdit || HasTextureVisualEdit || HasStructureEdit || HasSurfaceBehaviorEdit;
    public string RuntimeKey => $"{SectorIndex}:{FaceIndex}:{Detail}";

    private static uint ParseNativeWord(string text) =>
        TryParseNativeWord(text, out uint word) ? word : 0;

    private static bool TryParseNativeWord(string? text, out uint word)
    {
        string value = (text ?? "").Trim();
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            value = value[2..];
        return uint.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out word);
    }

    private static uint PackNativeByteSlotsOrZero(IReadOnlyList<int> slots) =>
        TryPackNativeByteSlots(slots, out uint word) ? word : 0;

    private static bool TryPackNativeByteSlots(IReadOnlyList<int> slots, out uint word)
    {
        word = 0;
        if (slots.Count != SourceSceneOverlayContract.CornerSlotCount ||
            slots.Any(slot => slot is < byte.MinValue or > byte.MaxValue))
        {
            return false;
        }

        word = (uint)slots[0] |
            ((uint)slots[1] << 8) |
            ((uint)slots[2] << 16) |
            ((uint)slots[3] << 24);
        return true;
    }

    private static IReadOnlyList<int> BuildCornerPointIndexes(IReadOnlyList<int> vertexIndexes)
    {
        if (vertexIndexes.Count != SourceSceneOverlayContract.CornerSlotCount)
            return Array.Empty<int>();

        Dictionary<int, int> pointIndexByVertex = new();
        int[] result = new int[SourceSceneOverlayContract.CornerSlotCount];
        for (int slot = 0; slot < result.Length; slot++)
        {
            int vertexIndex = vertexIndexes[slot];
            if (!pointIndexByVertex.TryGetValue(vertexIndex, out int pointIndex))
            {
                pointIndex = pointIndexByVertex.Count;
                pointIndexByVertex.Add(vertexIndex, pointIndex);
            }

            result[slot] = pointIndex;
        }

        return result;
    }

    private bool HasConsistentCornerTopology()
    {
        if (Points.Count is < 3 or > SourceSceneOverlayContract.CornerSlotCount)
            return false;

        Dictionary<int, int> expectedPointIndexByVertex = new();
        for (int slot = 0; slot < SourceSceneOverlayContract.CornerSlotCount; slot++)
        {
            int pointIndex = CornerPointIndexes[slot];
            if (pointIndex < 0 || pointIndex >= Points.Count)
                return false;

            int vertexIndex = VertexIndexes[slot];
            if (!expectedPointIndexByVertex.TryGetValue(vertexIndex, out int expectedPointIndex))
            {
                expectedPointIndex = expectedPointIndexByVertex.Count;
                expectedPointIndexByVertex.Add(vertexIndex, expectedPointIndex);
            }

            if (pointIndex != expectedPointIndex)
                return false;
        }

        return expectedPointIndexByVertex.Count == Points.Count;
    }

    public void ApplyTerrainDeltaZ(float deltaZ)
    {
        int count = Math.Min(ZValues.Length, OriginalZValues.Length);
        for (int i = 0; i < count; i++)
            ZValues[i] = OriginalZValues[i] + deltaZ;

        RecalculateZStats();
    }

    public void ApplyTerrainVertexDeltas(IReadOnlyList<float> deltaZValues)
    {
        int count = Math.Min(Math.Min(ZValues.Length, OriginalZValues.Length), deltaZValues.Count);
        for (int i = 0; i < count; i++)
            ZValues[i] = OriginalZValues[i] + deltaZValues[i];

        RecalculateZStats();
    }

    public void ApplyTerrainZValues(IReadOnlyList<float> zValues)
    {
        int count = Math.Min(ZValues.Length, zValues.Count);
        for (int i = 0; i < count; i++)
            ZValues[i] = zValues[i];

        RecalculateZStats();
    }

    public void ApplyTerrainTranslation(float deltaX, float deltaY)
    {
        int count = Math.Min(_points.Length, OriginalPoints.Count);
        for (int i = 0; i < count; i++)
            _points[i] = new Vector2f(OriginalPoints[i].X + deltaX, OriginalPoints[i].Y + deltaY);

        RecalculatePointStats();
    }

    public void ApplyTerrainPointPositions(IReadOnlyList<Vector2f> points)
    {
        int count = Math.Min(_points.Length, points.Count);
        for (int i = 0; i < count; i++)
            _points[i] = points[i];

        RecalculatePointStats();
    }

    public void ApplyTerrainVertexXYDeltas(IReadOnlyList<Vector2f> deltaValues)
    {
        int count = Math.Min(Math.Min(_points.Length, OriginalPoints.Count), deltaValues.Count);
        for (int i = 0; i < count; i++)
            _points[i] = new Vector2f(OriginalPoints[i].X + deltaValues[i].X, OriginalPoints[i].Y + deltaValues[i].Y);

        RecalculatePointStats();
    }

    public IReadOnlyList<float> TerrainVertexDeltas()
    {
        int count = Math.Min(ZValues.Length, OriginalZValues.Length);
        float[] deltas = new float[count];
        for (int i = 0; i < count; i++)
            deltas[i] = ZValues[i] - OriginalZValues[i];
        return deltas;
    }

    public IReadOnlyList<Vector2f> TerrainVertexXYDeltas()
    {
        int count = Math.Min(_points.Length, OriginalPoints.Count);
        Vector2f[] deltas = new Vector2f[count];
        for (int i = 0; i < count; i++)
            deltas[i] = new Vector2f(_points[i].X - OriginalPoints[i].X, _points[i].Y - OriginalPoints[i].Y);
        return deltas;
    }

    public void ApplyTextureOverride(int textureId)
    {
        if (textureId < 0)
            return;

        if (textureId != TextureId)
        {
            TextureVisualEdit = null;
            SurfaceBehaviorEdit = null;
        }
        TextureId = textureId;
    }

    public void ApplyTextureVisualEdit(TerrainTextureVisualEdit edit)
    {
        ArgumentNullException.ThrowIfNull(edit);
        TextureVisualEdit = edit;
    }

    public void ClearTextureVisualEdit()
    {
        TextureVisualEdit = null;
    }

    public void ResetTerrainEdit()
    {
        ResetTerrainHeightEdit();
        ResetTerrainPositionEdit();
        TextureId = OriginalTextureId;
        TextureVisualEdit = null;
        StructureEdit = TerrainStructureEditKind.None;
        SurfaceBehaviorEdit = null;
    }

    public void ResetTerrainHeightEdit()
    {
        ApplyTerrainDeltaZ(0);
    }

    public void ResetTerrainPositionEdit()
    {
        ApplyTerrainPointPositions(OriginalPoints);
    }

    public void StageTerrainRemoval()
    {
        StructureEdit = TerrainStructureEditKind.RemoveFace;
    }

    public void StageTerrainAddClone()
    {
        StructureEdit = TerrainStructureEditKind.AddCloneFace;
    }

    public void SetSurface(string surface, ColorRgba color, string source)
    {
        Surface = string.IsNullOrWhiteSpace(surface) ? "unknown" : surface;
        SurfaceColor = color;
        SurfaceSource = string.IsNullOrWhiteSpace(source) ? "unclassified" : source;
    }

    public void SetSurfacePreviewColor(ColorRgba color, string source)
    {
        SurfaceColor = color;
        if (!string.IsNullOrWhiteSpace(source) && !SurfaceSource.Contains(source, StringComparison.OrdinalIgnoreCase))
            SurfaceSource = $"{SurfaceSource} + {source}";
    }

    public void SetBehavior(string behavior, string source, string confidence, string note)
    {
        Behavior = string.IsNullOrWhiteSpace(behavior) ? "unknown" : behavior;
        BehaviorSource = string.IsNullOrWhiteSpace(source) ? "unclassified" : source;
        BehaviorConfidence = string.IsNullOrWhiteSpace(confidence) ? "unknown" : confidence;
        BehaviorNote = note ?? "";
    }

    public void ApplySurfaceBehaviorEdit(TerrainSurfaceBehaviorEdit edit)
    {
        ArgumentNullException.ThrowIfNull(edit);
        SurfaceBehaviorEdit = edit;
    }

    public void ClearSurfaceBehaviorEdit()
    {
        SurfaceBehaviorEdit = null;
    }

    public bool TryGetZ(float x, float y, out float z)
    {
        z = AvgZ;
        if (Points.Count < 3 || ZValues.Length < Points.Count)
            return false;

        for (int i = 1; i < Points.Count - 1; i++)
        {
            if (TryInterpolateTriangle(Points[0], ZValues[0], Points[i], ZValues[i], Points[i + 1], ZValues[i + 1], x, y, out z))
                return true;
        }

        return ContainsXY(x, y);
    }

    public bool ContainsXY(float x, float y)
    {
        bool inside = false;
        int j = Points.Count - 1;
        for (int i = 0; i < Points.Count; i++)
        {
            float yi = Points[i].Y;
            float yj = Points[j].Y;
            if (((yi > y) != (yj > y)) &&
                (x < ((Points[j].X - Points[i].X) * (y - yi) / ((yj - yi) == 0f ? 0.0001f : yj - yi)) + Points[i].X))
                inside = !inside;

            j = i;
        }

        return inside;
    }

    private void RecalculateZStats()
    {
        if (ZValues.Length == 0)
        {
            AvgZ = 0;
            MinZ = 0;
            MaxZ = 0;
            TerrainEditDeltaZ = 0;
            return;
        }

        float sum = 0;
        float deltaSum = 0;
        MinZ = ZValues[0];
        MaxZ = ZValues[0];
        for (int i = 0; i < ZValues.Length; i++)
        {
            float z = ZValues[i];
            sum += z;
            MinZ = Math.Min(MinZ, z);
            MaxZ = Math.Max(MaxZ, z);
            if (i < OriginalZValues.Length)
                deltaSum += z - OriginalZValues[i];
        }

        AvgZ = sum / ZValues.Length;
        TerrainEditDeltaZ = OriginalZValues.Length == 0 ? 0 : deltaSum / Math.Min(ZValues.Length, OriginalZValues.Length);
    }

    private void RecalculatePointStats()
    {
        Bounds = CalculateBounds(Points);
        Center = CalculateCenter(Points);
    }

    private bool HasEditedZValues()
    {
        int count = Math.Min(ZValues.Length, OriginalZValues.Length);
        for (int i = 0; i < count; i++)
        {
            if (Math.Abs(ZValues[i] - OriginalZValues[i]) > 0.001f)
                return true;
        }

        return false;
    }

    private bool HasEditedPoints()
    {
        int count = Math.Min(_points.Length, OriginalPoints.Count);
        for (int i = 0; i < count; i++)
        {
            if (Math.Abs(_points[i].X - OriginalPoints[i].X) > 0.001f ||
                Math.Abs(_points[i].Y - OriginalPoints[i].Y) > 0.001f)
            {
                return true;
            }
        }

        return _points.Length != OriginalPoints.Count;
    }

    private static Rect2f CalculateBounds(IReadOnlyList<Vector2f> points)
    {
        if (points.Count == 0)
            return Rect2f.Empty;

        float minX = float.MaxValue;
        float minY = float.MaxValue;
        float maxX = float.MinValue;
        float maxY = float.MinValue;
        foreach (Vector2f point in points)
        {
            minX = Math.Min(minX, point.X);
            minY = Math.Min(minY, point.Y);
            maxX = Math.Max(maxX, point.X);
            maxY = Math.Max(maxY, point.Y);
        }

        return Rect2f.FromBounds(minX, minY, maxX, maxY);
    }

    private static Vector2f CalculateCenter(IReadOnlyList<Vector2f> points)
    {
        if (points.Count == 0)
            return new Vector2f();

        float sumX = 0;
        float sumY = 0;
        foreach (Vector2f point in points)
        {
            sumX += point.X;
            sumY += point.Y;
        }

        return new Vector2f(sumX / points.Count, sumY / points.Count);
    }

    private static bool TryInterpolateTriangle(Vector2f a, float az, Vector2f b, float bz, Vector2f c, float cz, float x, float y, out float z)
    {
        z = 0;
        float denom = ((b.Y - c.Y) * (a.X - c.X)) + ((c.X - b.X) * (a.Y - c.Y));
        if (Math.Abs(denom) < 0.0001f)
            return false;

        float u = (((b.Y - c.Y) * (x - c.X)) + ((c.X - b.X) * (y - c.Y))) / denom;
        float v = (((c.Y - a.Y) * (x - c.X)) + ((a.X - c.X) * (y - c.Y))) / denom;
        float w = 1f - u - v;
        const float eps = -0.001f;
        if (u < eps || v < eps || w < eps)
            return false;

        z = (u * az) + (v * bz) + (w * cz);
        return true;
    }
}

public readonly record struct TerrainEdge(float X1, float Y1, float X2, float Y2)
{
    public Rect2f Bounds => Rect2f.FromBounds(Math.Min(X1, X2), Math.Min(Y1, Y2), Math.Max(X1, X2), Math.Max(Y1, Y2));
}
