using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.Core.Scene;

public sealed class GeometryCandidate
{
    public string Name { get; init; } = "geometry";
    public Rect2f Bounds { get; set; } = Rect2f.Empty;
    public float MinZ { get; init; }
    public float MaxZ { get; init; }
    public List<TerrainPolygon> Polygons { get; } = new();
    public List<TerrainEdge> Edges { get; } = new();
}

public enum TerrainStructureEditKind
{
    None,
    RemoveFace,
    AddCloneFace
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
        IReadOnlyList<int>? colourIndexes = null)
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
        Word3 = word3;
        Word4 = word4;
        FaceFlip = faceFlip;
        FaceDepth = faceDepth;
        ColourIndexes = colourIndexes?.ToArray() ?? Array.Empty<int>();
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
    public bool FaceFlip { get; }
    public int FaceDepth { get; }
    public IReadOnlyList<int> ColourIndexes { get; }
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

    public bool HasTextureEdit => OriginalTextureId >= 0 && TextureId != OriginalTextureId;
    public bool HasHeightEdit => HasEditedZValues();
    public bool HasPositionEdit => HasEditedPoints();
    public TerrainStructureEditKind StructureEdit { get; private set; } = TerrainStructureEditKind.None;
    public bool HasStructureEdit => StructureEdit != TerrainStructureEditKind.None;
    public bool IsTerrainRemoved => StructureEdit == TerrainStructureEditKind.RemoveFace;
    public bool IsTerrainAddClone => StructureEdit == TerrainStructureEditKind.AddCloneFace;
    public bool IsTerrainEdited => HasHeightEdit || HasPositionEdit || HasTextureEdit || HasStructureEdit;
    public string RuntimeKey => $"{SectorIndex}:{FaceIndex}:{Detail}";

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
        if (textureId >= 0)
            TextureId = textureId;
    }

    public void ResetTerrainEdit()
    {
        ResetTerrainHeightEdit();
        ResetTerrainPositionEdit();
        TextureId = OriginalTextureId;
        StructureEdit = TerrainStructureEditKind.None;
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
