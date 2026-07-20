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
