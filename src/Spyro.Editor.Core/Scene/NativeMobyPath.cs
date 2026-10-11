using Spyro.Editor.Core.Primitives;

namespace Spyro.Editor.Core.Scene;

/// <summary>
/// One in-place editable node from Spyro 1's native PathData structure.
/// The fourth word is intentionally read-only until its runtime meaning is
/// decoded; exporters patch only the three coordinate words.
/// </summary>
public sealed class NativePathNode
{
    internal NativePathNode(
        int index,
        int rawX,
        int rawY,
        int rawZ,
        int unknownWord,
        long coordinateWadOffset)
    {
        Index = index;
        OriginalRawX = RawX = rawX;
        OriginalRawY = RawY = rawY;
        OriginalRawZ = RawZ = rawZ;
        UnknownWord = unknownWord;
        CoordinateWadOffset = coordinateWadOffset;
    }

    public int Index { get; }
    public int OriginalRawX { get; }
    public int OriginalRawY { get; }
    public int OriginalRawZ { get; }
    public int RawX { get; private set; }
    public int RawY { get; private set; }
    public int RawZ { get; private set; }
    public int UnknownWord { get; }
    public long CoordinateWadOffset { get; }
    public Vector3f OriginalPosition => new(OriginalRawX / 16f, OriginalRawY / 16f, OriginalRawZ / 16f);
    public Vector3f Position => new(RawX / 16f, RawY / 16f, RawZ / 16f);
    public bool HasEdit => RawX != OriginalRawX || RawY != OriginalRawY || RawZ != OriginalRawZ;

    public void SetRawPosition(int rawX, int rawY, int rawZ)
    {
        RawX = rawX;
        RawY = rawY;
        RawZ = rawZ;
    }

    public void SetPosition(Vector3f position)
    {
        SetRawPosition(
            checked((int)Math.Round(position.X * 16f)),
            checked((int)Math.Round(position.Y * 16f)),
            checked((int)Math.Round(position.Z * 16f)));
    }

    public void TranslateRaw(int deltaX, int deltaY, int deltaZ) =>
        SetRawPosition(
            checked(RawX + deltaX),
            checked(RawY + deltaY),
            checked(RawZ + deltaZ));

    public void Reset() => SetRawPosition(OriginalRawX, OriginalRawY, OriginalRawZ);
}

/// <summary>
/// A decoded native PathData block owned by one source Moby.
/// </summary>
public sealed class NativeMobyPath
{
    internal NativeMobyPath(
        string levelKey,
        string levelName,
        int ownerTrueIndex,
        ushort ownerNativeClass,
        long ownerRecordWadOffset,
        int wadLba,
        long sceneWadOffset,
        int sceneByteLength,
        uint propertiesPointer,
        long propertiesWadOffset,
        uint pathPointer,
        long pathWadOffset,
        byte[] originalHeaderBytes,
        string originalPathSha256,
        IReadOnlyList<NativePathNode> nodes)
    {
        LevelKey = levelKey;
        LevelName = levelName;
        OwnerTrueIndex = ownerTrueIndex;
        OwnerNativeClass = ownerNativeClass;
        OwnerRecordWadOffset = ownerRecordWadOffset;
        WadLba = wadLba;
        SceneWadOffset = sceneWadOffset;
        SceneByteLength = sceneByteLength;
        PropertiesPointer = propertiesPointer;
        PropertiesWadOffset = propertiesWadOffset;
        PathPointer = pathPointer;
        PathWadOffset = pathWadOffset;
        OriginalHeaderBytes = (byte[])originalHeaderBytes.Clone();
        OriginalPathSha256 = originalPathSha256;
        Nodes = nodes;
    }

    public string LevelKey { get; }
    public string LevelName { get; }
    public int OwnerTrueIndex { get; }
    public ushort OwnerNativeClass { get; }
    public long OwnerRecordWadOffset { get; }
    public int WadLba { get; }
    public long SceneWadOffset { get; }
    public int SceneByteLength { get; }
    public uint PropertiesPointer { get; }
    public long PropertiesWadOffset { get; }
    public uint PathPointer { get; }
    public long PathWadOffset { get; }
    public byte[] OriginalHeaderBytes { get; }
    public string OriginalPathSha256 { get; }
    public IReadOnlyList<NativePathNode> Nodes { get; }
    /// <summary>The route of an object added to a level (not on the disc): its nodes are saved with the object
    /// (<see cref="Moby.UniversalPathNodes"/>), not as in-place disc edits.</summary>
    public bool IsPlacedObjectRoute { get; private init; }

    /// <summary>An added object's route: <paramref name="original"/> is where it came with (moved with the object),
    /// <paramref name="current"/> as edited; raw x, y, z per node.</summary>
    public static NativeMobyPath ForPlacedObject(string levelKey, string levelName, int ownerTrueIndex, ushort ownerClass,
        IReadOnlyList<int[]> original, IReadOnlyList<int[]> current)
    {
        if (original.Count != current.Count || original.Count is 0 or > 64) throw new ArgumentException("A route needs 1 to 64 nodes.");
        var nodes = original.Select((n, i) => new NativePathNode(i, n[0], n[1], n[2], 0, -1)).ToList();
        for (int i = 0; i < nodes.Count; i++) nodes[i].SetRawPosition(current[i][0], current[i][1], current[i][2]);
        byte[] header = new byte[8]; header[0] = (byte)nodes.Count;
        return new NativeMobyPath(levelKey, levelName, ownerTrueIndex, ownerClass, -1, 0, -1, 0, 0, -1, 0, -1, header, "", nodes)
            { IsPlacedObjectRoute = true };
    }

    /// <summary>The nodes' raw positions (x, y, z), in order.</summary>
    public List<int[]> RawNodes() => [.. Nodes.Select(n => new[] { n.RawX, n.RawY, n.RawZ })];
    public int NodeCount => OriginalHeaderBytes[0];
    public int CurrentNode => OriginalHeaderBytes[1];
    public short Reversed => BitConverter.ToInt16(OriginalHeaderBytes, 6);
    public bool HasEdits => Nodes.Any(node => node.HasEdit);

    public void TranslateRaw(int deltaX, int deltaY, int deltaZ)
    {
        foreach (NativePathNode node in Nodes)
            node.TranslateRaw(deltaX, deltaY, deltaZ);
    }

    public void ResetEdits()
    {
        foreach (NativePathNode node in Nodes)
            node.Reset();
    }
}
