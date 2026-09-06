using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;

namespace Spyro.Editor.Core.Exporting;

/// <summary>Authored test intent, not a source-issued or native-physics certificate.</summary>
public sealed record NativeWizardPlacementIntent(int OriginX, int OriginY, int OriginZ, int EndX, int EndY);
public sealed record NativeWizardPlacementCoordinateMutation(string Id, int SceneOffset, int Before, int After);

internal static class NativeWizardPlacementGeometry
{
    internal static void Validate(NativeWizardPlacementIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        if (new[] { intent.OriginX, intent.OriginY, intent.OriginZ, intent.EndX, intent.EndY }.Any(v => v is < 0 or > 262128))
            throw new InvalidDataException("Wizard authored coordinate is outside the bounded native world.");
        if (new[] { intent.OriginX, intent.OriginY, intent.EndX, intent.EndY }.Any(v => (v & 15) != 0))
            throw new InvalidDataException("Wizard authored horizontal coordinates require the 16-unit grid.");
        long dx = (long)intent.EndX - intent.OriginX, dy = (long)intent.EndY - intent.OriginY;
        long square = dx * dx + dy * dy;
        if (square == 0) throw new InvalidDataException("Wizard authored route requires two distinct horizontal points.");
        if (square > 8192L * 8192) throw new InvalidDataException("Wizard authored route exceeds the 8192-unit length budget.");
    }

    // Point-to-CLOSED-segment distance, exact even for the full signed-int domain.
    // Equality refuses. This is center separation only, not actor shape/collision.
    internal static bool IsStrictlySeparated(int ax, int ay, int bx, int by, int px, int py, int distance)
    {
        if (distance < 0) throw new InvalidDataException("Wizard center distance cannot be negative.");
        BigInteger dx = (BigInteger)bx - ax, dy = (BigInteger)by - ay;
        BigInteger ex = (BigInteger)px - ax, ey = (BigInteger)py - ay;
        BigInteger length = dx * dx + dy * dy, dot = ex * dx + ey * dy, threshold = (BigInteger)distance * distance;
        if (dot <= 0 || length == 0) return ex * ex + ey * ey > threshold;
        if (dot >= length)
        {
            BigInteger fx = (BigInteger)px - bx, fy = (BigInteger)py - by;
            return fx * fx + fy * fy > threshold;
        }
        BigInteger cross = ex * dy - ey * dx;
        return cross * cross > threshold * length;
    }
}

/// <summary>Frozen exact Dark source; caller bytes cannot issue this authority.</summary>
public sealed class NativeDarkHollowWizardPlacementSource
{
    private readonly NativeDarkHollowWizardScenePlan _scene;
    private readonly NativeRouteSurfaceSnapshot _terrain;
    private readonly (int X, int Y)[] _residents;
    private NativeDarkHollowWizardPlacementSource(NativeDarkHollowWizardScenePlan scene)
    {
        _scene = scene;
        byte[] data = scene.CopyOriginalLevel();
        if (Hash(data) != "a937aecb3626ffd2311ba136684a34c9f36877054b52557b4524fcacee0e2d97" ||
            scene.PlacedCountBefore != 128 || scene.PlacedCountAfter != 129 || scene.DynamicCapacityAfter != 90)
            throw new InvalidDataException("Wizard placement requires the exact single appended Dark scene source.");
        scene.VerifyOriginalAndFinal(data.AsSpan(0x194000, 0x11800), scene.CopyScene());
        int model = I32(data, 8), length = I32(data, 12);
        _terrain = NativeRouteSurfaceSnapshot.Capture(data.AsSpan(model, length).ToArray(), 0xF01000L + model);
        _residents = Enumerable.Range(0, 128).Select(i =>
            (I32(data, 0x194000 + 0x8C38 + i * 0x58 + 12), I32(data, 0x194000 + 0x8C38 + i * 0x58 + 16))).ToArray();
    }
    public string SourceImageSha256 => NativeMobySourceInventory.OriginalImageSha256;
    public int OriginalResidentCount => _residents.Length;
    public string CollisionSha256 => _terrain.CollisionSha256;
    public static NativeDarkHollowWizardPlacementSource Capture(string imagePath, NativeDarkHollowWizardScenePlan scene)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath); ArgumentNullException.ThrowIfNull(scene);
        using FileStream image = new(imagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        RequireOriginal(image);
        if (scene.SourceImageSha256 != NativeMobySourceInventory.OriginalImageSha256)
            throw new InvalidDataException("Wizard placement scene belongs to another source image.");
        var result = new NativeDarkHollowWizardPlacementSource(scene);
        RequireOriginal(image);
        return result;
    }
    public NativeDarkHollowWizardPlacementPlan Compile(NativeWizardPlacementIntent intent) =>
        NativeDarkHollowWizardPlacementPlan.Build(this, intent);
    internal (NativeDarkHollowWizardScenePlan Scene, NativeRouteSurfaceReport Surface) Evaluate(NativeWizardPlacementIntent intent)
    {
        NativeWizardPlacementGeometry.Validate(intent);
        int? ground = NativeRouteSurfaceCertificate.FindUniqueFlatGround(_terrain, new(intent.OriginX, intent.OriginY, intent.OriginZ));
        if (ground is not { } z) throw new InvalidDataException("Wizard placement has no unique flat source ground datum.");
        if (intent.OriginZ < z || intent.OriginZ > z + 512)
            throw new InvalidDataException("Wizard initial height is outside the ground..ground+512 authored research interval.");
        for (int i = 0; i < _residents.Length; i++)
            if (!NativeWizardPlacementGeometry.IsStrictlySeparated(intent.OriginX, intent.OriginY, intent.EndX, intent.EndY,
                _residents[i].X, _residents[i].Y, 3072))
                throw new InvalidDataException($"Wizard authored corridor violates center separation from original Moby {i}.");
        // Preserve the donor's first route-point Z minus instance Z (287). That
        // offset is NOT inferred to be ground height or a fixed body/mover lift.
        NativeRoutePoint[] route = [new(intent.OriginX, intent.OriginY, intent.OriginZ),
            new(intent.OriginX, intent.OriginY, intent.OriginZ + 287), new(intent.EndX, intent.EndY, intent.OriginZ + 287)];
        var surface = NativeRouteSurfaceCertificate.Evaluate(_terrain, route, new(z, 512, 1536));
        if (!surface.Accepted)
            throw new InvalidDataException("Wizard authored surface corridor refused: " + string.Join(",", surface.Issues.Select(i => i.Code).Distinct()));
        return (_scene, surface);
    }
    private static void RequireOriginal(FileStream image)
    {
        image.Position = 0;
        if (image.Length != NativeMobySourceInventory.OriginalImageByteLength ||
            Convert.ToHexStringLower(SHA256.HashData(image)) != NativeMobySourceInventory.OriginalImageSha256 ||
            image.Position != NativeMobySourceInventory.OriginalImageByteLength || image.Length != NativeMobySourceInventory.OriginalImageByteLength)
            throw new InvalidDataException("Wizard placement requires the unchanged complete original image.");
    }
    private static int I32(ReadOnlySpan<byte> bytes, int at) => BinaryPrimitives.ReadInt32LittleEndian(bytes[at..]);
    internal static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}

/// <summary>
/// Typed coordinate-only scene stage. Accepted dry corridor and original-center
/// separation are research policy checks, never body, mover, AI or runtime proof.
/// </summary>
public sealed class NativeDarkHollowWizardPlacementPlan
{
    private readonly byte[] _baseScene, _finalScene;
    private readonly NativeRouteSurfaceReport _surface;
    private NativeDarkHollowWizardPlacementPlan(NativeDarkHollowWizardPlacementSource source, NativeWizardPlacementIntent intent)
    {
        ArgumentNullException.ThrowIfNull(source);
        var (scene, surface) = source.Evaluate(intent);
        _baseScene = scene.CopyScene(); _finalScene = _baseScene.ToArray(); _surface = surface;
        BaseSceneSha256 = scene.FinalSceneSha256; BaseGraphFingerprint = scene.GraphFingerprint;
        SourceDataSha256 = scene.SourceDataSha256; Intent = intent;
        if (I32(_baseScene, 0xB838) != 0xEED4 || I32(_baseScene, 0xEED4) != 0xEEFC ||
            I32(_baseScene, 0xEED4 + 0x28) != 2 || I32(_baseScene, 0xEED4 + 0x2C) != -65536 ||
            I32(_baseScene, 0xEED4 + 0x3C) != 0 || I32(_baseScene, 0xEED4 + 0x4C) != 0 ||
            I32(_baseScene, 0xB838 + 20) != 27853 || I32(_baseScene, 0xEED4 + 0x38) != 28140 ||
            I32(_baseScene, 0xEED4 + 0x48) != 28140)
            throw new InvalidDataException("Wizard placement private row/route preimage changed.");
        var fields = new List<NativeWizardPlacementCoordinateMutation>();
        Add("instance.x", 0xB838 + 12, intent.OriginX); Add("instance.y", 0xB838 + 16, intent.OriginY);
        Add("instance.z", 0xB838 + 20, intent.OriginZ);
        Add("route0.x", 0xEED4 + 0x30, intent.OriginX); Add("route0.y", 0xEED4 + 0x34, intent.OriginY);
        Add("route0.z", 0xEED4 + 0x38, intent.OriginZ + 287);
        Add("route1.x", 0xEED4 + 0x40, intent.EndX); Add("route1.y", 0xEED4 + 0x44, intent.EndY);
        Add("route1.z", 0xEED4 + 0x48, intent.OriginZ + 287);
        Mutations = fields.AsReadOnly(); FinalSceneSha256 = NativeDarkHollowWizardPlacementSource.Hash(_finalScene);
        PlanSha256 = NativeDarkHollowWizardPlacementSource.Hash(JsonSerializer.SerializeToUtf8Bytes(new
        {
            ProfileId, SourceDataSha256, BaseSceneSha256, BaseGraphFingerprint, Intent,
            surface.CertificateSha256, OriginalResidentCount, AuthoredMinimumCenterDistance, Mutations, FinalSceneSha256
        }));
        RequireOriginalAndFinal(_baseScene, _finalScene);
        void Add(string id, int at, int after)
        {
            fields.Add(new(id, at, I32(_baseScene, at), after));
            BinaryPrimitives.WriteInt32LittleEndian(_finalScene.AsSpan(at), after);
        }
    }
    internal static NativeDarkHollowWizardPlacementPlan Build(NativeDarkHollowWizardPlacementSource source,
        NativeWizardPlacementIntent intent) => new(source, intent);
    public const string ProfileId = "native-dark-wizard-authored-dry-corridor-placement-research-v1";
    public string SourceDataSha256 { get; }
    public string SourceImageSha256 => NativeMobySourceInventory.OriginalImageSha256;
    public string BaseSceneSha256 { get; }
    public string BaseGraphFingerprint { get; }
    public string FinalSceneSha256 { get; }
    public string PlanSha256 { get; }
    public NativeWizardPlacementIntent Intent { get; }
    public IReadOnlyList<NativeWizardPlacementCoordinateMutation> Mutations { get; }
    public int GroundZ => _surface.Policy.GroundZ;
    public int AuthoredCorridorHalfWidth => 512;
    public int AuthoredClearHeight => 1536;
    public int AuthoredMinimumCenterDistance => 3072;
    public int OriginalResidentCount => 128;
    public string SurfaceCertificateSha256 => _surface.CertificateSha256;
    public string CollisionSha256 => _surface.CollisionSha256;
    public bool AuthoredDryCorridorAccepted => true;
    public bool OriginalCenterSeparationAccepted => true;
    public bool InitialHeightProven => false;
    public bool NativeBodyAndTrajectoryProven => false;
    public bool PlacementApproved => false;
    public bool RuntimeVerified => false;
    public bool PublicationApproved => false;
    public byte[] CopyScene() => _finalScene.ToArray();
    public byte[] CopyBaseScene() => _baseScene.ToArray();
    public byte[] RevertToAppendedScene(ReadOnlySpan<byte> final)
    {
        RequireOriginalAndFinal(_baseScene, final); return _baseScene.ToArray();
    }
    public void RequireOriginalAndFinal(ReadOnlySpan<byte> original, ReadOnlySpan<byte> final)
    {
        if (!original.SequenceEqual(_baseScene)) throw new InvalidDataException("Wizard placement base scene preimage changed.");
        if (!final.SequenceEqual(_finalScene)) throw new InvalidDataException("Wizard placement final scene changed.");
        byte[] inverse = final.ToArray();
        foreach (var field in Mutations)
        {
            if (I32(final, field.SceneOffset) != field.After || I32(original, field.SceneOffset) != field.Before)
                throw new InvalidDataException("Wizard placement coordinate readback changed.");
            BinaryPrimitives.WriteInt32LittleEndian(inverse.AsSpan(field.SceneOffset), field.Before);
        }
        if (!inverse.AsSpan().SequenceEqual(original)) throw new InvalidDataException("Wizard placement changed an unowned scene byte.");
    }
    internal void RequireSceneStage(NativeDarkHollowWizardScenePlan scene)
    {
        if (scene.SourceDataSha256 != SourceDataSha256 || scene.FinalSceneSha256 != BaseSceneSha256 ||
            scene.GraphFingerprint != BaseGraphFingerprint || !scene.CopyScene().AsSpan().SequenceEqual(_baseScene))
            throw new InvalidDataException("Wizard placement requires its exact source-issued appended scene stage.");
        RequireOriginalAndFinal(scene.CopyScene(), _finalScene);
    }
    private static int I32(ReadOnlySpan<byte> bytes, int at) => BinaryPrimitives.ReadInt32LittleEndian(bytes[at..]);
}
