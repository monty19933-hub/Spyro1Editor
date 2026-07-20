using System.Buffers.Binary;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Exporting;

public sealed record NativeMobyPathCoordinatePatch(
    string LevelKey,
    string LevelName,
    int OwnerTrueIndex,
    int NodeIndex,
    int WadLba,
    long WadOffset,
    byte[] Before,
    byte[] After)
{
    public int ByteLength => After.Length;
}

public sealed record NativeMobyPathPatchPlan(
    string Format,
    int Version,
    IReadOnlyList<NativeMobyPathCoordinatePatch> Patches)
{
    public int EditedPathCount => Patches
        .Select(patch => (patch.LevelKey, patch.OwnerTrueIndex))
        .Distinct()
        .Count();
    public int EditedNodeCount => Patches.Count;
}

/// <summary>
/// Builds and applies guarded, in-place native path coordinate patches. Each
/// payload is exactly the three XYZ words; PathData headers and node word +0x0C
/// are never included in the write.
/// </summary>
public static class NativeMobyPathPatchExporter
{
    public const string PlanFormat = "spyro-editor-native-moby-path-patch-plan";
    public const int PlanVersion = 1;
    public const int CoordinateBytes = 12;

    public static NativeMobyPathPatchPlan BuildPlan(IEnumerable<NativeMobyPath> nativePaths)
    {
        List<NativeMobyPathCoordinatePatch> patches = [];
        foreach (NativeMobyPath nativePath in nativePaths)
        {
            foreach (NativePathNode node in nativePath.Nodes.Where(node => node.HasEdit))
            {
                byte[] before = Encode(node.OriginalRawX, node.OriginalRawY, node.OriginalRawZ);
                byte[] after = Encode(node.RawX, node.RawY, node.RawZ);
                patches.Add(new NativeMobyPathCoordinatePatch(
                    nativePath.LevelKey,
                    nativePath.LevelName,
                    nativePath.OwnerTrueIndex,
                    node.Index,
                    nativePath.WadLba,
                    node.CoordinateWadOffset,
                    before,
                    after));
            }
        }

        NativeMobyPathCoordinatePatch[] ordered = patches
            .OrderBy(patch => patch.WadLba)
            .ThenBy(patch => patch.WadOffset)
            .ToArray();
        for (int i = 1; i < ordered.Length; i++)
        {
            NativeMobyPathCoordinatePatch previous = ordered[i - 1];
            NativeMobyPathCoordinatePatch current = ordered[i];
            if (previous.WadLba == current.WadLba && previous.WadOffset + previous.ByteLength > current.WadOffset)
                throw new InvalidOperationException("Native Moby path coordinate patch ranges overlap.");
        }
        return new NativeMobyPathPatchPlan(PlanFormat, PlanVersion, ordered);
    }

    public static void ApplyToImage(string imagePath, NativeMobyPathPatchPlan plan)
    {
        if (!File.Exists(imagePath))
            throw new FileNotFoundException("Missing disc image to patch.", imagePath);
        if (!string.Equals(plan.Format, PlanFormat, StringComparison.Ordinal) || plan.Version != PlanVersion)
            throw new InvalidOperationException("Unsupported native Moby path patch plan.");
        if (plan.Patches.Count == 0)
            return;

        DiscLayout layout = DiscImage.DetectLayout(imagePath);
        using FileStream stream = File.Open(imagePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        DiscFileRecord wad = DiscImage.FindRootFileRecord(stream, layout, IsWadFileName);
        foreach (NativeMobyPathCoordinatePatch patch in plan.Patches)
        {
            if (patch.WadLba != wad.Lba)
                throw new InvalidOperationException($"{patch.LevelName} T{patch.OwnerTrueIndex} path plan targets a different WAD LBA.");
            if (patch.Before.Length != CoordinateBytes || patch.After.Length != CoordinateBytes)
                throw new InvalidOperationException("Native Moby path patches must contain exactly 12 XYZ bytes.");
            byte[] actual = DiscImage.ReadFileBytes(stream, layout, wad.Lba, patch.WadOffset, CoordinateBytes);
            if (!actual.SequenceEqual(patch.Before))
            {
                throw new InvalidOperationException(
                    $"{patch.LevelName} T{patch.OwnerTrueIndex} node {patch.NodeIndex} original XYZ bytes do not match the selected image.");
            }
        }
        foreach (NativeMobyPathCoordinatePatch patch in plan.Patches)
            DiscImage.WriteFileBytes(stream, layout, wad.Lba, patch.WadOffset, patch.After);
    }

    private static byte[] Encode(int rawX, int rawY, int rawZ)
    {
        byte[] bytes = new byte[CoordinateBytes];
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0, 4), rawX);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4, 4), rawY);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8, 4), rawZ);
        return bytes;
    }

    private static bool IsWadFileName(string name) =>
        string.Equals(name, "WAD.WAD", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, "WAD", StringComparison.OrdinalIgnoreCase);
}
