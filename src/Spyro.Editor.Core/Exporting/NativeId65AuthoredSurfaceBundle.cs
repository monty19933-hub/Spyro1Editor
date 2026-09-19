using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Spyro.Editor.Core.Primitives;
using Spyro.Editor.Core.Scene;

[assembly: InternalsVisibleTo("Spyro.Editor.NativeId65AuthoredSurfaceSmoke")]

namespace Spyro.Editor.Core.Exporting;

/// <summary>Actual native world units, not editor preview or screen units.</summary>
/// <param name="FloorRgb">Raw native color word 0x00BBGGRR, not display hex 0xRRGGBB.</param>
/// <param name="SkyRgb">Raw native color word 0x00BBGGRR, not display hex 0xRRGGBB.</param>
public sealed record NativeId65AuthoredFloor(int X, int Y, int Z, int Width, int Depth,
    uint FloorRgb, uint SkyRgb)
{
    /// <summary>
    /// Converts opaque display-color channels to native R, G, B, 0 byte order.
    /// Geometry validation remains the responsibility of surface compilation.
    /// </summary>
    /// <exception cref="ArgumentException">Either color has unsupported nonopaque alpha.</exception>
    public static NativeId65AuthoredFloor FromDisplayColors(int x, int y, int z, int width, int depth,
        ColorRgba floorColor, ColorRgba skyColor)
    {
        if (floorColor.A != 255)
            throw new ArgumentException("Authored floor colors must be opaque; alpha is unsupported.", nameof(floorColor));
        if (skyColor.A != 255)
            throw new ArgumentException("Authored sky colors must be opaque; alpha is unsupported.", nameof(skyColor));
        return new(x, y, z, width, depth,
            (uint)(floorColor.R | floorColor.G << 8 | floorColor.B << 16),
            (uint)(skyColor.R | skyColor.G << 8 | skyColor.B << 16));
    }
}

public enum NativeId65SurfaceComponentKind
{
    Environment,
    Visibility,
    SpecialSurfaces,
    Collision,
    Cyclorama
}

public enum NativeId65SurfaceReferenceKind
{
    RelativeBytePointer,
    RelativeWordPointer,
    TriangleIndex,
    EnvironmentSectorIndex,
    VisibilityGroupIndex
}

public sealed record NativeId65SurfaceComponent(NativeId65SurfaceComponentKind Kind,
    int BlockOffset, int ByteLength, string Sha256);
public sealed record NativeId65SurfaceOwner(string Id, int BlockOffset, int ByteLength, string Sha256);
public sealed record NativeId65SurfaceReference(string FieldOwnerId, int FieldRelativeOffset,
    int ByteWidth, NativeId65SurfaceReferenceKind Kind, int BaseBlockOffset,
    string TargetOwnerId, int TargetRelativeOffset, uint EncodedValue, uint ValueMask, int UnitByteLength);

/// <summary>
/// Five freshly authored native surface components in one detached namespace.
/// This is not subfile1, a level, a retail inventory, a scene, or a disc patch.
/// There are no source terrain triangles or texture references in the output.
/// </summary>
public sealed class NativeId65AuthoredSurfaceBundle
{
    private readonly byte[] _bytes;
    private readonly byte[]? _sourceExecutable;

    private NativeId65AuthoredSurfaceBundle(byte[] bytes, byte[]? executable)
    {
        Floor = NativeId65AuthoredSurfaceLayout.DecodeAndValidate(bytes);
        _bytes = bytes.ToArray();
        _sourceExecutable = executable?.ToArray();
        ConsumerEvidence = executable == null
            ? Array.AsReadOnly(Array.Empty<NativeId65SurfaceConsumerEvidence>())
            : NativeId65AuthoredSurfaceConsumers.Capture(executable);
        var description = NativeId65AuthoredSurfaceLayout.Describe(_bytes, Floor);
        Components = Array.AsReadOnly(description.Components);
        Owners = Array.AsReadOnly(description.Owners);
        References = Array.AsReadOnly(description.References);
        BundleSha256 = NativeId65AuthoredSurfaceLayout.Hash(_bytes);
    }

    public const string ProfileId = "native-id65-detached-authored-flat-surface-clean-usa-v1";
    public NativeId65AuthoredFloor Floor { get; }
    public bool SourceCertified => _sourceExecutable != null;
    public string? SourceImageSha256 => SourceCertified ? NativeId65AuthoredSurfaceConsumers.ImageSha256 : null;
    public string BundleSha256 { get; }
    public int ByteLength => _bytes.Length;
    public IReadOnlyList<NativeId65SurfaceComponent> Components { get; }
    public IReadOnlyList<NativeId65SurfaceOwner> Owners { get; }
    public IReadOnlyList<NativeId65SurfaceReference> References { get; }
    public IReadOnlyList<NativeId65SurfaceConsumerEvidence> ConsumerEvidence { get; }
    public bool IndependentLevelSupported => false;
    public bool CompleteLevelResourcesProven => false;
    public bool PublicationApproved => false;
    public bool RuntimeVerified => false;
    public byte[] CopyBundle() => _bytes.ToArray();

    public byte[] CopyComponent(NativeId65SurfaceComponentKind kind)
    {
        var component = Components.SingleOrDefault(c => c.Kind == kind)
            ?? throw new ArgumentOutOfRangeException(nameof(kind));
        return _bytes.AsSpan(component.BlockOffset, component.ByteLength).ToArray();
    }

    public byte[] CopySourceExecutable() => _sourceExecutable?.ToArray()
        ?? throw new InvalidOperationException("A neutral surface bundle has no native source executable certificate.");

    public void RequireExactBundle(ReadOnlySpan<byte> bytes)
    {
        if (!bytes.SequenceEqual(_bytes))
            throw new InvalidDataException("Authored surface owner, pointer, guard, or complete byte complement changed.");
    }

    internal static NativeId65AuthoredSurfaceBundle CreateNeutral(byte[] bytes) => new(bytes, null);

    public static async Task<NativeId65AuthoredSurfaceBundle> CaptureAsync(string originalImagePath,
        NativeId65AuthoredFloor floor, CancellationToken cancellationToken = default)
    {
        NativeId65AuthoredSurfaceLayout.ValidateIntent(floor);
        byte[] executable = await NativeId65AuthoredSurfaceConsumers.ReadOriginalExecutableAsync(
            originalImagePath, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        // Only this exact-source adapter reaches the private certified constructor.
        // Consumer bytes are captured, not inserted into the authored surface.
        return new(NativeId65AuthoredSurfaceLayout.Encode(floor), executable);
    }
}

internal sealed record NativeId65SurfaceDescription(NativeId65SurfaceComponent[] Components,
    NativeId65SurfaceOwner[] Owners, NativeId65SurfaceReference[] References);

/// <summary>
/// Intentionally narrow neutral grammar: four vertices and two consistently
/// wound triangles, one Z/Y/X cell, ordinary land, no special/texture/sky parts.
/// It cannot accept caller-supplied native source authority or general meshes.
/// </summary>
internal static class NativeId65AuthoredSurfaceLayout
{
    private const int EnvironmentLength = 172;
    private const int VisibilityLength = 32;
    private const int SpecialLength = 8;
    private const int CycloramaLength = 16;
    private const int CollisionOffset = EnvironmentLength + VisibilityLength + SpecialLength;
    private const int MaximumBundleLength = 4096;

    internal static NativeId65AuthoredSurfaceBundle Compile(NativeId65AuthoredFloor floor)
    {
        ValidateIntent(floor);
        return NativeId65AuthoredSurfaceBundle.CreateNeutral(Encode(floor));
    }

    internal static void ValidateIntent(NativeId65AuthoredFloor floor)
    {
        ArgumentNullException.ThrowIfNull(floor);
        if (floor.X is < 0 or > 262128 || floor.Y is < 0 or > 262128 || floor.Z is < 0 or > 262128 ||
            (floor.X & 15) != 0 || (floor.Y & 15) != 0 || (floor.Z & 15) != 0 ||
            floor.Width is < 16 or > 2048 || floor.Depth is < 16 or > 2048 ||
            (floor.Width & 15) != 0 || (floor.Depth & 15) != 0 ||
            floor.FloorRgb > 0xFFFFFF || floor.SkyRgb > 0xFFFFFF)
            throw new InvalidDataException("Authored floor requires bounded nonnegative14-bit world/16 origins,16..2048 extents and RGB24 colors.");
        if ((floor.X >> 12) != ((floor.X + floor.Width) >> 12) ||
            (floor.Y >> 12) != ((floor.Y + floor.Depth) >> 12))
            throw new InvalidDataException("The complete closed authored rectangle must remain in one native4096-unit collision cell.");
    }

    internal static byte[] Encode(NativeId65AuthoredFloor floor)
    {
        ValidateIntent(floor);
        // The legacy admission and certificates stay unchanged. Shared tiled
        // production owns the actual geometry; one tile preserves every byte.
        return NativeFlatTileSurfaceLayout.Compile(
            new[] { new NativeFlatSurfaceTile(floor.X, floor.Y, floor.Z,
                floor.Width, floor.Depth, floor.FloorRgb) }, floor.SkyRgb).CopyBundle();
    }

    internal static NativeId65AuthoredFloor DecodeAndValidate(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 296 || bytes.Length > MaximumBundleLength || (bytes.Length & 3) != 0 ||
            U(bytes, 0) != EnvironmentLength || U(bytes, EnvironmentLength) != VisibilityLength ||
            U(bytes, EnvironmentLength + VisibilityLength) != SpecialLength)
            throw new InvalidDataException("Authored surface is outside its bounded exact five-component grammar.");
        uint collisionLength = U(bytes, CollisionOffset);
        if (collisionLength < 68 || collisionLength > MaximumBundleLength ||
            CollisionOffset + (long)collisionLength + CycloramaLength != bytes.Length)
            throw new InvalidDataException("Collision owner cannot consume another component or unowned trailing bytes.");
        int sky = CollisionOffset + (int)collisionLength;
        if (U(bytes, sky) != CycloramaLength)
            throw new InvalidDataException("Cyclorama must own its exact zero-part header and speculative-load guard.");
        const int sector = 12;
        var floor = new NativeId65AuthoredFloor(
            (int)(U(bytes, sector + 8) >> 16) * 16,
            (int)(U(bytes, sector + 8) & 65535) * 16,
            (int)(U(bytes, sector + 12) >> 16) * 16,
            (int)(U(bytes, sector + 32) >> 21) * 16,
            (int)((U(bytes, sector + 36) >> 10) & 2047) * 16,
            U(bytes, sector + 44), U(bytes, sky + 4));
        ValidateIntent(floor);
        // This is a strict canonical grammar, not a permissive arbitrary native
        // parser. Re-encoding independently recovered intent binds every flag,
        // count, index, guard and pad byte before the generic index decoder.
        if (!bytes.SequenceEqual(Encode(floor)))
            throw new InvalidDataException("Authored surface has inconsistent geometry, winding, material, visibility, pointer, sentinel or unowned bytes.");
        var sectorReadback = Id65EnvironmentSectorHeaderContract.ReadPointerSpan(
            bytes.Slice(sector, 160), 160, false, "fresh authored surface sector");
        if (sectorReadback.ExactSectorByteLength != 156 || sectorReadback.PointerGapByteLength != 4)
            throw new InvalidDataException("Authored sector tables do not end at their exact owned lookahead guard.");
        ReadOnlySpan<byte> c = bytes.Slice(CollisionOffset, (int)collisionLength);
        int blocks = 4 + (int)U(c, 16);
        int triangles = 4 + (int)U(c, 20);
        NativeCollisionIndexSnapshot index = NativeCollisionIndexCodec.Decode(
            c.Slice(32, blocks - 32), c.Slice(blocks, triangles - blocks), 6, 2);
        var cell = new NativeCollisionCell(floor.X >> 12, floor.Y >> 12, floor.Z >> 12);
        if (index.Cells.Count != 1 || !index.Cells.TryGetValue(cell, out var binding) ||
            !binding.OrderedTriangleIndexes.SequenceEqual(new[] { 1, 0 }) ||
            index.CopyBlocks().AsSpan(6).IndexOfAnyExcept((byte)0) >= 0)
            throw new InvalidDataException("Every emitted triangle must be reachable in the one owned cell; no foreign groups are allowed.");
        return floor;
    }

    internal static NativeId65SurfaceDescription Describe(byte[] bytes, NativeId65AuthoredFloor floor)
    {
        List<NativeId65SurfaceComponent> components = [];
        List<NativeId65SurfaceOwner> owners = [];
        List<NativeId65SurfaceReference> references = [];
        int offset = 0;
        foreach (NativeId65SurfaceComponentKind kind in Enum.GetValues<NativeId65SurfaceComponentKind>())
        {
            int length = (int)U(bytes, offset);
            components.Add(new(kind, offset, length, Hash(bytes.AsSpan(offset, length))));
            offset += length;
        }
        Owner("environment.header", 0, 12);
        Owner("environment.sector-header", 12, 28);
        Owner("environment.lp-vertices", 40, 16);
        Owner("environment.lp-colors", 56, 16);
        Owner("environment.lp-faces", 72, 16);
        Owner("environment.hp-vertices", 88, 16);
        Owner("environment.hp-color-table1", 104, 16);
        Owner("environment.hp-color-table2", 120, 16);
        Owner("environment.hp-faces", 136, 32);
        Owner("environment.face-prefetch-guard", 168, 4);
        int visibility = EnvironmentLength;
        Owner("visibility.header", visibility, 16);
        Owner("visibility.environment-list", visibility + 16, 4);
        Owner("visibility.sky-header", visibility + 20, 8);
        Owner("visibility.sky-list", visibility + 28, 4);
        Owner("special.header", visibility + VisibilityLength, 8);
        int collision = CollisionOffset;
        int blocks = collision + 4 + (int)U(bytes, collision + 16);
        int triangles = collision + 4 + (int)U(bytes, collision + 20);
        int assignments = collision + 4 + (int)U(bytes, collision + 24);
        int flags = collision + 4 + (int)U(bytes, collision + 28);
        Owner("collision.header", collision, 32);
        Owner("collision.tree", collision + 32, blocks - collision - 32);
        Owner("collision.blocks", blocks, 8);
        Owner("collision.triangles", triangles, 24);
        Owner("collision.visibility-assignments", assignments, 4);
        Owner("collision.flags", flags, 4);
        int sky = flags + 4;
        Owner("cyclorama.header", sky, 12);
        Owner("cyclorama.zero-pointer-guard", sky + 12, 4);

        BytePointer("environment.header", 8, 4, "environment.sector-header");
        BytePointer("visibility.header", 4, visibility + 4, "visibility.sky-header");
        BytePointer("visibility.header", 12, visibility + 4, "visibility.environment-list");
        BytePointer("visibility.sky-header", 4, visibility + 20, "visibility.sky-list");
        string[] targets = ["collision.tree", "collision.blocks", "collision.triangles",
            "collision.visibility-assignments", "collision.flags"];
        for (int i = 0; i < targets.Length; i++)
            BytePointer("collision.header", 12 + i * 4, collision + 4, targets[i]);
        int zLength = (floor.Z >> 12) + 1;
        int yLength = (floor.Y >> 12) + 1;
        int xLength = (floor.X >> 12) + 1;
        int yStart = (zLength + 1) * 2;
        int xStart = yStart + (yLength + 1) * 2;
        TreePointer(zLength * 2, yStart);
        TreePointer(yStart + yLength * 2, xStart);
        references.Add(new("collision.tree", xStart + xLength * 2, 2,
            NativeId65SurfaceReferenceKind.RelativeWordPointer, blocks, "collision.blocks", 0, 0, 0xFFFF, 2));
        references.Add(new("collision.blocks", 0, 2, NativeId65SurfaceReferenceKind.TriangleIndex,
            triangles, "collision.triangles", 12, 0x8001, 0x7FFF, 12));
        references.Add(new("collision.blocks", 2, 2, NativeId65SurfaceReferenceKind.TriangleIndex,
            triangles, "collision.triangles", 0, 0, 0x7FFF, 12));
        references.Add(new("visibility.environment-list", 0, 1, NativeId65SurfaceReferenceKind.EnvironmentSectorIndex,
            8, "environment.header", 8, 0, 0xFF, 4));
        for (int i = 0; i < 2; i++)
            references.Add(new("collision.visibility-assignments", i, 1,
                NativeId65SurfaceReferenceKind.VisibilityGroupIndex, visibility + 12,
                "visibility.header", 12, 0, 0xFF, 4));
        if (owners.Sum(o => o.ByteLength) != bytes.Length || offset != bytes.Length)
            throw new InvalidDataException("Surface metadata does not partition the complete detached owner.");
        foreach (var reference in references)
        {
            var field = owners.Single(o => o.Id == reference.FieldOwnerId);
            var target = owners.Single(o => o.Id == reference.TargetOwnerId);
            if (reference.FieldRelativeOffset < 0 || reference.FieldRelativeOffset + reference.ByteWidth > field.ByteLength ||
                reference.TargetRelativeOffset < 0 || reference.TargetRelativeOffset >= target.ByteLength ||
                reference.BaseBlockOffset + (long)(reference.EncodedValue & reference.ValueMask) * reference.UnitByteLength !=
                target.BlockOffset + reference.TargetRelativeOffset)
                throw new InvalidDataException("Surface reference does not resolve to its exact typed owner and base.");
        }
        return new(components.ToArray(), owners.ToArray(), references.ToArray());

        void Owner(string id, int start, int length) =>
            owners.Add(new(id, start, length, Hash(bytes.AsSpan(start, length))));
        void BytePointer(string fieldId, int fieldOffset, int basis, string targetId)
        {
            var field = owners.Single(o => o.Id == fieldId);
            references.Add(new(fieldId, fieldOffset, 4, NativeId65SurfaceReferenceKind.RelativeBytePointer,
                basis, targetId, 0, U(bytes, field.BlockOffset + fieldOffset), uint.MaxValue, 1));
        }
        void TreePointer(int field, int target) => references.Add(new("collision.tree", field, 2,
            NativeId65SurfaceReferenceKind.RelativeBytePointer, collision + 32,
            "collision.tree", target, (uint)target, 0xFFFF, 1));
    }
    internal static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static uint U(ReadOnlySpan<byte> b, int o) => BinaryPrimitives.ReadUInt32LittleEndian(b[o..]);
}
