using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Spyro.Editor.Core.Scene;

[assembly: InternalsVisibleTo("Spyro.Editor.NativeWizardAppendPlacementSmoke")]

namespace Spyro.Editor.Core.Exporting;

internal static class NativeWizardPlacementPairGeometry
{
    // Strict distance between two CLOSED segments. Crossing interiors can be
    // arbitrarily far from all four endpoints, so intersection comes FIRST.
    internal static bool IsStrictlySeparated(int ax, int ay, int bx, int by,
        int cx, int cy, int dx, int dy, int distance)
    {
        if (distance < 0) throw new InvalidDataException("Wizard center distance cannot be negative.");
        BigInteger cSide = Orient(ax, ay, bx, by, cx, cy), dSide = Orient(ax, ay, bx, by, dx, dy);
        BigInteger aSide = Orient(cx, cy, dx, dy, ax, ay), bSide = Orient(cx, cy, dx, dy, bx, by);
        bool intersects =
            (cSide.Sign * dSide.Sign < 0 && aSide.Sign * bSide.Sign < 0) ||
            (cSide.IsZero && Within(ax, ay, bx, by, cx, cy)) ||
            (dSide.IsZero && Within(ax, ay, bx, by, dx, dy)) ||
            (aSide.IsZero && Within(cx, cy, dx, dy, ax, ay)) ||
            (bSide.IsZero && Within(cx, cy, dx, dy, bx, by));
        return !intersects &&
            NativeWizardPlacementGeometry.IsStrictlySeparated(ax, ay, bx, by, cx, cy, distance) &&
            NativeWizardPlacementGeometry.IsStrictlySeparated(ax, ay, bx, by, dx, dy, distance) &&
            NativeWizardPlacementGeometry.IsStrictlySeparated(cx, cy, dx, dy, ax, ay, distance) &&
            NativeWizardPlacementGeometry.IsStrictlySeparated(cx, cy, dx, dy, bx, by, distance);
    }
    private static BigInteger Orient(int ax, int ay, int bx, int by, int px, int py) =>
        ((BigInteger)bx - ax) * ((BigInteger)py - ay) - ((BigInteger)by - ay) * ((BigInteger)px - ax);
    private static bool Within(int ax, int ay, int bx, int by, int px, int py) =>
        px >= Math.Min(ax, bx) && px <= Math.Max(ax, bx) && py >= Math.Min(ay, by) && py <= Math.Max(ay, by);
}

/// <summary>Copied placement receipt only; this public value cannot issue native byte ownership.</summary>
public sealed record NativeWizardFamilyPlacementReceipt(string FamilyId, int NativeIndex,
    NativeWizardPlacementIntent Intent, int GroundZ, string SurfaceCertificateSha256);
public sealed record NativeWizardFamilyPlacementCoordinateMutation(string FamilyId, int NativeIndex,
    string Id, int SceneOffset, int Before, int After);

/// <summary>
/// Nine coordinate-only int32 fields per privately allocated Wizard owner.
/// Full original/other private/fixup/pool bytes stay exact. Static authored dry
/// corridor and center-separation policy is not a body, mover, resource/reward,
/// accepted-history, App, publication or native-runtime certificate.
/// </summary>
public sealed class NativeDarkHollowWizardAppendPlacementPlan
{
    private readonly byte[] _baseScene, _finalScene;
    private NativeDarkHollowWizardAppendPlacementPlan(NativeDarkHollowWizardAppendPlacementSource source,
        IEnumerable<NativeWizardFamilyPlacementIntent> requests)
    {
        // Complete source/schema/bijection/resident/pair/surface validation is
        // atomic: only after every family passes do we copy and modify bytes.
        var evaluated = source.Evaluate(requests);
        var appended = source.Appended;
        Appended = appended;
        _baseScene = appended.CopyScene(); _finalScene = _baseScene.ToArray();
        ProposedOwners = appended.ProposedOwners;
        SourceImageSha256 = source.SourceImageSha256; SourceDataSha256 = source.SourceDataSha256;
        SourceSchemaSha256 = source.SourceSchemaSha256; CollisionSha256 = source.CollisionSha256;
        BaseSceneSha256 = appended.FinalSceneSha256; BaseGraphFingerprint = appended.GraphFingerprint;
        OwnersSha256 = NativeDarkHollowWizardAppendPlacementSource.Hash(ProposedOwners.Serialize());
        PlacedCountAfter = appended.PlacedCountAfter; FixupCountAfter = appended.FixupCountAfter;
        DynamicCapacityAfter = appended.DynamicCapacityAfter;
        Families = Array.AsReadOnly(evaluated.Select(e => new NativeWizardFamilyPlacementReceipt(
            e.Owner.FamilyId, e.Owner.NativeIndex, e.Intent, e.Surface.Policy.GroundZ,
            e.Surface.CertificateSha256)).ToArray());
        List<NativeWizardFamilyPlacementCoordinateMutation> mutations = [];
        HashSet<int> fields = [];
        foreach (var e in evaluated)
        {
            Add(e, "instance.x", e.Owner.Row, 12, e.Intent.OriginX);
            Add(e, "instance.y", e.Owner.Row, 16, e.Intent.OriginY);
            Add(e, "instance.z", e.Owner.Row, 20, e.Intent.OriginZ);
            Add(e, "route0.x", e.Owner.Properties, 0x30, e.Intent.OriginX);
            Add(e, "route0.y", e.Owner.Properties, 0x34, e.Intent.OriginY);
            Add(e, "route0.z", e.Owner.Properties, 0x38, e.Intent.OriginZ + 287);
            Add(e, "route1.x", e.Owner.Properties, 0x40, e.Intent.EndX);
            Add(e, "route1.y", e.Owner.Properties, 0x44, e.Intent.EndY);
            Add(e, "route1.z", e.Owner.Properties, 0x48, e.Intent.OriginZ + 287);
        }
        Mutations = mutations.AsReadOnly();
        FinalSceneSha256 = NativeDarkHollowWizardAppendPlacementSource.Hash(_finalScene);
        PlanSha256 = NativeDarkHollowWizardAppendPlacementSource.Hash(JsonSerializer.SerializeToUtf8Bytes(new
        {
            ProfileId, Scope, SourceImageSha256, SourceDataSha256, SourceSchemaSha256, CollisionSha256,
            BaseSceneSha256, BaseGraphFingerprint, OwnersSha256, PlacedCountAfter, FixupCountAfter, DynamicCapacityAfter,
            Families, Mutations, FinalSceneSha256, AuthoredCorridorHalfWidth, AuthoredClearHeight,
            AuthoredMinimumCenterDistance, OriginalResidentCount
        }));
        RequireOriginalAndFinal(_baseScene, _finalScene);
        return;

        void Add(NativeWizardFamilyPlacementEvaluation e, string id, NativeByteExtent owner, int relative, int after)
        {
            if (relative < 0 || relative % 4 != 0 || relative > owner.ByteLength - 4 ||
                owner.Space.Revision != NativeAddressRevision.Final || owner.Space.Kind != NativeAddressKind.Scene ||
                owner.Space != e.Owner.Row.Space || owner.Space.ByteLength != _baseScene.Length)
                throw new InvalidDataException("Wizard coordinate field crosses its canonical allocated parent owner.");
            int at = checked((int)(owner.Offset + relative));
            if (at % 4 != 0 || !fields.Add(at))
                throw new InvalidDataException("Wizard coordinate field aliases another mutation.");
            mutations.Add(new(e.Owner.FamilyId, e.Owner.NativeIndex, id, at, I32(_baseScene, at), after));
            BinaryPrimitives.WriteInt32LittleEndian(_finalScene.AsSpan(at), after);
        }
    }

    internal static NativeDarkHollowWizardAppendPlacementPlan Build(NativeDarkHollowWizardAppendPlacementSource source,
        IEnumerable<NativeWizardFamilyPlacementIntent> requests) => new(source, requests);
    internal NativeDarkHollowWizardAppendPlan Appended { get; }
    public const string ProfileId = "native-dark-wizard-append-authored-placement-research-v1";
    public const string Scope = "Coordinate-only original-source append scene. Authored dry square corridors and closed-route center separation, not native physical shapes, visible body, mover trajectories, lifetime/resource/reward independence, accepted export/save history or runtime.";
    public NativeMobyPersistentOwnerLedger ProposedOwners { get; }
    public IReadOnlyList<NativeWizardFamilyPlacementReceipt> Families { get; }
    public IReadOnlyList<NativeWizardFamilyPlacementCoordinateMutation> Mutations { get; }
    public string SourceImageSha256 { get; }
    public string SourceDataSha256 { get; }
    public string SourceSchemaSha256 { get; }
    public string CollisionSha256 { get; }
    public string BaseSceneSha256 { get; }
    public string BaseGraphFingerprint { get; }
    public string OwnersSha256 { get; }
    public string FinalSceneSha256 { get; }
    public string PlanSha256 { get; }
    public int PlacedCountAfter { get; }
    public int FixupCountAfter { get; }
    public int DynamicCapacityAfter { get; }
    public int OriginalResidentCount => 128;
    public int FamilyCount => Families.Count;
    public int AuthoredCorridorHalfWidth => 512;
    public int AuthoredClearHeight => 1536;
    public int AuthoredMinimumCenterDistance => 3072;
    public bool AuthoredDryCorridorAccepted => true;
    public bool OriginalCenterSeparationAccepted => true;
    public bool AuthoredPairwiseCenterSeparationAccepted => true;
    public bool InitialHeightProven => false;
    public bool NativeBodyAndTrajectoryProven => false;
    public bool BehaviorClosureProven => false;
    public bool ResourceAndRewardIndependenceProven => false;
    public bool PlacementApproved => false;
    public bool RuntimeFitProven => false;
    public bool RuntimeVerified => false;
    public bool PublicationApproved => false;
    public bool ProjectTransactionIntegrated => false;
    public bool AcceptedExportHistoryProven => false;
    public byte[] CopyScene() => _finalScene.ToArray();
    public byte[] CopyBaseScene() => _baseScene.ToArray();
    public byte[] RevertToAppendedScene(ReadOnlySpan<byte> final)
    {
        RequireOriginalAndFinal(_baseScene, final);
        return _baseScene.ToArray();
    }
    public void RequireOriginalAndFinal(ReadOnlySpan<byte> original, ReadOnlySpan<byte> final)
    {
        if (!original.SequenceEqual(_baseScene))
            throw new InvalidDataException("Count-aware Wizard placement base scene preimage changed.");
        if (!final.SequenceEqual(_finalScene))
            throw new InvalidDataException("Count-aware Wizard placement final scene changed.");
        byte[] inverse = final.ToArray();
        foreach (var field in Mutations)
        {
            if (I32(original, field.SceneOffset) != field.Before || I32(final, field.SceneOffset) != field.After)
                throw new InvalidDataException("Wizard allocated coordinate readback changed.");
            BinaryPrimitives.WriteInt32LittleEndian(inverse.AsSpan(field.SceneOffset), field.Before);
        }
        if (!inverse.AsSpan().SequenceEqual(original))
            throw new InvalidDataException("Wizard placement changed an unowned scene byte.");
    }
    private static int I32(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadInt32LittleEndian(bytes[offset..]);
}
