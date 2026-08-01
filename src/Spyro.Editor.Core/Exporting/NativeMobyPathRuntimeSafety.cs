using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Spyro.Editor.Core.Levels;
using Spyro.Editor.Core.Rendering;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Exporting;

public readonly record struct NativeMobyPathRawPosition(
    int RawX,
    int RawY,
    int RawZ);

public sealed record NativeMobyPathRuntimeSafetyFinding(
    string Code,
    MobyBuildSafetyStatus Status,
    string Message);

public sealed record NativeMobyPathRuntimeEvidenceProfile(
    string Id,
    string LevelKey,
    int OwnerTrueIndex,
    string OriginalPathSha256,
    string EditedCoordinatesSha256,
    string RuntimeEvidence);

public enum NativeMobyPathClosingTraversal
{
    HandlerSpecificUnverified,
    CyclicForwardOrReverse,
    GenericWrapToFirst = CyclicForwardOrReverse
}

public sealed record NativeMobyPathTraversalProfile(
    string Id,
    string LevelKey,
    int OwnerTrueIndex,
    NativeMobyPathClosingTraversal ClosingTraversal,
    string Evidence);

public sealed record NativeMobyPathRuntimeSafetyResult(
    string EditedCoordinatesSha256,
    NativeMobyPathRuntimeEvidenceProfile? RuntimeEvidence,
    IReadOnlyList<NativeMobyPathRuntimeSafetyFinding> Findings)
{
    public bool IsGeometricallySafe =>
        Findings.All(finding => finding.Status != MobyBuildSafetyStatus.Blocked);

    public bool IsRuntimeProven => RuntimeEvidence != null;
}

/// <summary>
/// Exact edited-route runtime evidence. A profile is deliberately tied to the
/// retail path preimage and all edited XYZ words; static validation alone does
/// not promote arbitrary user-authored routes to normal Create BIN.
/// </summary>
public static class NativeMobyPathRuntimeEvidenceRegistry
{
    private static readonly NativeMobyPathRuntimeEvidenceProfile[] RegisteredProfiles = [];

    public static IReadOnlyList<NativeMobyPathRuntimeEvidenceProfile> Profiles => RegisteredProfiles;

    public static NativeMobyPathRuntimeEvidenceProfile? Resolve(
        NativeMobyPath path,
        string editedCoordinatesSha256)
    {
        string levelKey = LevelCatalog.NormalizeKey(path.LevelKey);
        return RegisteredProfiles.SingleOrDefault(profile =>
            string.Equals(LevelCatalog.NormalizeKey(profile.LevelKey), levelKey, StringComparison.Ordinal) &&
            profile.OwnerTrueIndex == path.OwnerTrueIndex &&
            string.Equals(profile.OriginalPathSha256, path.OriginalPathSha256, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(profile.EditedCoordinatesSha256, editedCoordinatesSha256, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(profile.RuntimeEvidence));
    }

    public static string ComputeEditedCoordinatesSha256(NativeMobyPath path)
    {
        byte[] originalFingerprint = Encoding.ASCII.GetBytes(path.OriginalPathSha256);
        byte[] payload = new byte[
            originalFingerprint.Length +
            sizeof(int) +
            sizeof(int) +
            (path.Nodes.Count * 3 * sizeof(int))];
        int offset = 0;
        originalFingerprint.CopyTo(payload, offset);
        offset += originalFingerprint.Length;
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(offset, sizeof(int)), path.OwnerTrueIndex);
        offset += sizeof(int);
        BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(offset, sizeof(int)), path.Nodes.Count);
        offset += sizeof(int);
        foreach (NativePathNode node in path.Nodes)
        {
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(offset, sizeof(int)), node.RawX);
            offset += sizeof(int);
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(offset, sizeof(int)), node.RawY);
            offset += sizeof(int);
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(offset, sizeof(int)), node.RawZ);
            offset += sizeof(int);
        }

        return Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
    }
}

/// <summary>
/// Handler-specific traversal evidence. Exact disassembly confirms that all 12
/// retail class-0x21 routes can traverse the closing edge. Some handlers wrap
/// forward immediately; Stone Hill reaches the same edge later in reverse.
/// Unknown future handlers remain Review-only until their traversal is decoded.
/// </summary>
public static class NativeMobyPathTraversalProfileRegistry
{
    private static readonly NativeMobyPathTraversalProfile[] RegisteredProfiles =
    [
        Cyclic("stonehill", 166),
        Cyclic("townsquare", 88),
        Cyclic("peacekeepers", 44),
        Cyclic("drycanyon", 87),
        Cyclic("clifftown", 146),
        Cyclic("magiccrafters", 0),
        Cyclic("magiccrafters", 14),
        Cyclic("alpineridge", 43),
        Cyclic("highcaves", 17),
        Cyclic("highcaves", 134),
        Cyclic("wizardpeak", 26),
        Cyclic("wizardpeak", 139)
    ];

    public static IReadOnlyList<NativeMobyPathTraversalProfile> Profiles => RegisteredProfiles;

    public static NativeMobyPathTraversalProfile? Resolve(NativeMobyPath path)
    {
        string levelKey = LevelCatalog.NormalizeKey(path.LevelKey);
        return RegisteredProfiles.SingleOrDefault(profile =>
            string.Equals(LevelCatalog.NormalizeKey(profile.LevelKey), levelKey, StringComparison.Ordinal) &&
            profile.OwnerTrueIndex == path.OwnerTrueIndex);
    }

    private static NativeMobyPathTraversalProfile Cyclic(string levelKey, int ownerTrueIndex) =>
        new(
            $"{levelKey}-T{ownerTrueIndex}-class21-cyclic-v1",
            levelKey,
            ownerTrueIndex,
            NativeMobyPathClosingTraversal.CyclicForwardOrReverse,
            "Exact class-0x21 overlay and func_80039E94 disassembly verifies cyclic advancement; " +
            "the closing edge may be traversed forward or in reverse.");
}

/// <summary>
/// Fail-closed checks for the integer-vector and floor-probe assumptions used
/// by Spyro 1's native path followers. Sequential segments are always checked;
/// the closing edge is blocking where disassembly proves the destination
/// handler can traverse it, whether forward or in reverse.
/// </summary>
public static class NativeMobyPathRuntimeSafety
{
    public const int GteComponentMinimum = short.MinValue;
    public const int GteComponentMaximum = short.MaxValue;
    public const long MaximumSignedSquaredMagnitude = int.MaxValue;
    public const int MaximumClearanceDeltaRaw = 0x200;

    public static NativeMobyPathRuntimeSafetyResult Inspect(
        NativeMobyPath path,
        NativeMobyPathRawPosition ownerPosition,
        GeometryCandidate? geometry)
    {
        ArgumentNullException.ThrowIfNull(path);
        List<NativeMobyPathRuntimeSafetyFinding> findings = [];

        if (path.Nodes.Count == 0)
        {
            findings.Add(Blocked(
                "native-path-empty",
                "The native egg-thief path has no nodes."));
            return BuildResult(path, findings);
        }

        for (int index = 0; index < path.Nodes.Count - 1; index++)
        {
            NativePathNode from = path.Nodes[index];
            NativePathNode to = path.Nodes[index + 1];
            InspectVector(
                checked((long)to.RawX - from.RawX),
                checked((long)to.RawY - from.RawY),
                checked((long)to.RawZ - from.RawZ),
                $"sequential segment node {from.Index + 1} -> node {to.Index + 1}",
                "native-path-sequential",
                MobyBuildSafetyStatus.Blocked,
                findings);
        }

        NativePathNode last = path.Nodes[^1];
        NativePathNode first = path.Nodes[0];
        NativeMobyPathTraversalProfile? traversalProfile =
            NativeMobyPathTraversalProfileRegistry.Resolve(path);
        bool verifiedCyclic =
            traversalProfile?.ClosingTraversal == NativeMobyPathClosingTraversal.CyclicForwardOrReverse;
        InspectVector(
            checked((long)first.RawX - last.RawX),
            checked((long)first.RawY - last.RawY),
            checked((long)first.RawZ - last.RawZ),
            verifiedCyclic
                ? $"verified cyclic closing edge node {last.Index + 1} <-> node {first.Index + 1} " +
                  "(the handler may traverse it forward or in reverse)"
                : $"unverified handler closing edge between node {last.Index + 1} and node {first.Index + 1}",
            verifiedCyclic ? "native-path-cyclic" : "native-path-unverified-closing",
            verifiedCyclic ? MobyBuildSafetyStatus.Blocked : MobyBuildSafetyStatus.Review,
            findings);

        if (path.CurrentNode < 0 || path.CurrentNode >= path.Nodes.Count)
        {
            findings.Add(Blocked(
                "native-path-current-node-invalid",
                $"The native path current-node value {path.CurrentNode} is outside its {path.Nodes.Count}-node route."));
        }
        else
        {
            NativePathNode current = path.Nodes[path.CurrentNode];
            InspectVector(
                checked((long)current.RawX - ownerPosition.RawX),
                checked((long)current.RawY - ownerPosition.RawY),
                checked((long)current.RawZ - ownerPosition.RawZ),
                $"owner T{path.OwnerTrueIndex} -> current node {current.Index + 1}",
                "native-path-owner-current",
                MobyBuildSafetyStatus.Blocked,
                findings);
        }

        InspectEditedNodeTerrain(path, geometry, findings);
        return BuildResult(path, findings);
    }

    private static void InspectVector(
        long deltaX,
        long deltaY,
        long deltaZ,
        string label,
        string codePrefix,
        MobyBuildSafetyStatus overflowStatus,
        List<NativeMobyPathRuntimeSafetyFinding> findings)
    {
        if (!IsSigned16(deltaX) || !IsSigned16(deltaY) || !IsSigned16(deltaZ))
        {
            findings.Add(new NativeMobyPathRuntimeSafetyFinding(
                $"{codePrefix}-component-overflow",
                overflowStatus,
                $"{label} has raw delta ({deltaX}, {deltaY}, {deltaZ}); every component must remain within " +
                $"{GteComponentMinimum}..{GteComponentMaximum} for the native signed-16 GTE/vector path."));
        }

        long squaredMagnitude;
        try
        {
            squaredMagnitude = checked(
                checked(deltaX * deltaX) +
                checked(deltaY * deltaY) +
                checked(deltaZ * deltaZ));
        }
        catch (OverflowException)
        {
            squaredMagnitude = long.MaxValue;
        }

        if (squaredMagnitude > MaximumSignedSquaredMagnitude)
        {
            findings.Add(new NativeMobyPathRuntimeSafetyFinding(
                $"{codePrefix}-magnitude-overflow",
                overflowStatus,
                $"{label} has squared raw magnitude {squaredMagnitude}; it exceeds signed-int range " +
                $"{MaximumSignedSquaredMagnitude} used by the native magnitude path."));
        }
    }

    private static void InspectEditedNodeTerrain(
        NativeMobyPath path,
        GeometryCandidate? geometry,
        List<NativeMobyPathRuntimeSafetyFinding> findings)
    {
        NativePathNode[] editedNodes = path.Nodes.Where(node => node.HasEdit).ToArray();
        if (editedNodes.Length == 0)
            return;
        if (geometry is not { Polygons.Count: > 0 })
        {
            findings.Add(new NativeMobyPathRuntimeSafetyFinding(
                "native-path-terrain-unavailable",
                MobyBuildSafetyStatus.Review,
                "No source-derived terrain geometry is available to compare edited path-node floor clearance."));
            return;
        }

        foreach (NativePathNode node in editedNodes)
        {
            bool originalHit = TryFindTerrainRawZ(
                geometry,
                node.OriginalRawX,
                node.OriginalRawY,
                node.OriginalRawZ,
                out int originalTerrainRawZ);
            bool editedHit = TryFindTerrainRawZ(
                geometry,
                node.RawX,
                node.RawY,
                node.RawZ,
                out int editedTerrainRawZ);

            if (!editedHit)
            {
                findings.Add(Blocked(
                    "native-path-node-no-terrain-hit",
                    $"Edited node {node.Index + 1} has no source-derived terrain hit at its new XY position."));
                continue;
            }
            if (!originalHit)
            {
                findings.Add(new NativeMobyPathRuntimeSafetyFinding(
                    "native-path-source-clearance-unavailable",
                    MobyBuildSafetyStatus.Review,
                    $"Source node {node.Index + 1} has no terrain hit, so its native floor clearance cannot be compared."));
                continue;
            }

            long originalClearance = (long)node.OriginalRawZ - originalTerrainRawZ;
            long editedClearance = (long)node.RawZ - editedTerrainRawZ;
            long clearanceDelta = Math.Abs(editedClearance - originalClearance);
            if (clearanceDelta > MaximumClearanceDeltaRaw)
            {
                findings.Add(Blocked(
                    "native-path-node-clearance-mismatch",
                    $"Edited node {node.Index + 1} changes terrain clearance from {originalClearance} to " +
                    $"{editedClearance} raw units (delta {clearanceDelta}); the checked maximum is " +
                    $"{MaximumClearanceDeltaRaw} raw units."));
            }
        }
    }

    private static bool TryFindTerrainRawZ(
        GeometryCandidate geometry,
        int rawX,
        int rawY,
        int referenceRawZ,
        out int terrainRawZ)
    {
        terrainRawZ = 0;
        bool hit = TerrainSnapper.TryFindZAt(
            geometry.Polygons,
            rawX / (float)SpyroNativeTerrainCamera.CameraPositionScale,
            rawY / (float)SpyroNativeTerrainCamera.CameraPositionScale,
            referenceRawZ / (float)SpyroNativeTerrainCamera.CameraPositionScale,
            out float terrainZ,
            preferTopSurface: true);
        if (!hit || !float.IsFinite(terrainZ))
            return false;

        double scaled = terrainZ * SpyroNativeTerrainCamera.CameraPositionScale;
        if (scaled < int.MinValue || scaled > int.MaxValue)
            return false;
        terrainRawZ = (int)Math.Round(scaled);
        return true;
    }

    private static NativeMobyPathRuntimeSafetyResult BuildResult(
        NativeMobyPath path,
        IReadOnlyList<NativeMobyPathRuntimeSafetyFinding> findings)
    {
        string editedCoordinatesSha256 =
            NativeMobyPathRuntimeEvidenceRegistry.ComputeEditedCoordinatesSha256(path);
        NativeMobyPathRuntimeEvidenceProfile? evidence =
            NativeMobyPathRuntimeEvidenceRegistry.Resolve(path, editedCoordinatesSha256);
        return new NativeMobyPathRuntimeSafetyResult(
            editedCoordinatesSha256,
            evidence,
            findings);
    }

    private static bool IsSigned16(long value) =>
        value is >= GteComponentMinimum and <= GteComponentMaximum;

    private static NativeMobyPathRuntimeSafetyFinding Blocked(string code, string message) =>
        new(code, MobyBuildSafetyStatus.Blocked, message);
}
