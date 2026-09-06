using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Exporting;

/// <summary>Stable family and authored coordinates only, never native source authority.</summary>
public sealed record NativeWizardFamilyPlacementIntent(string FamilyId, NativeWizardPlacementIntent Placement);

internal sealed record NativeWizardFamilyPlacementEvaluation(NativeWizardAppendPlacementOwner Owner,
    NativeWizardPlacementIntent Intent, NativeRouteSurfaceReport Surface);

/// <summary>
/// Exact retail Dark terrain paired with a privately issued persistent Wizard
/// scene append. No caller rows, receipts, geometry reports or hashes may issue
/// this source. Existing edited scenes and fixed single-Wizard APIs are separate.
/// </summary>
public sealed class NativeDarkHollowWizardAppendPlacementSource
{
    internal const string DarkDataSha256 = "a937aecb3626ffd2311ba136684a34c9f36877054b52557b4524fcacee0e2d97";
    private const string DonorRowSha256 = "59401eafeab453fd9d26f14eedca58390653dc5f4fcc3d2560f7b3e8fc7afa82";
    private const string DonorPropertiesSha256 = "5b3d762f6117ea30e959a21bf2778f3442b03eba93125c9a0e0a642cc58d345b";
    private readonly NativeDarkHollowWizardAppendPlan _append;
    private readonly NativeRouteSurfaceSnapshot _terrain;
    private readonly IReadOnlyList<NativeWizardAppendPlacementOwner> _owners;
    private readonly (int X, int Y)[] _residents;

    private NativeDarkHollowWizardAppendPlacementSource(NativeDarkHollowWizardAppendPlan appended, byte[] data)
    {
        if (Hash(data) != DarkDataSha256 || I32(data, 24) != 0x194000 || I32(data, 28) != 0x11800 ||
            appended.SourceImageSha256 != NativeMobySourceInventory.OriginalImageSha256)
            throw new InvalidDataException("Count-aware Wizard placement requires the exact original Dark data/scene.");
        _append = appended;
        byte[] original = data.AsSpan(0x194000, 0x11800).ToArray();
        byte[] scene = appended.CopyScene();
        appended.VerifyOriginalAndFinal(original, scene);
        if (!appended.CopyOriginalScene().AsSpan().SequenceEqual(original))
            throw new InvalidDataException("Appended Wizard source scene differs from the actual original data owner.");
        _owners = appended.CopyPlacementOwners();
        var ledger = appended.ProposedOwners;
        ledger.RequireNoUnmaterializedRetirements();
        if (!ledger.SourceCertified || _owners.Count is < 1 or > 256 ||
            ledger.Entries.Count != _owners.Count || appended.PlacedCountBefore != 128 ||
            appended.PlacedCountAfter != 128 + _owners.Count || ledger.HighWater != appended.PlacedCountAfter ||
            appended.FixupCountAfter != 147 + 2 * _owners.Count ||
            appended.DynamicCapacityAfter != (0xE038 - 0x8C38 - appended.PlacedCountAfter * 0x58) / 0x70)
            throw new InvalidDataException("Count-aware placement requires complete source-issued active one-role owners.");
        NativeAddressSpace domain = _owners[0].Row.Space;
        if (domain.Kind != NativeAddressKind.Scene || domain.Revision != NativeAddressRevision.Final ||
            domain.ByteLength != scene.Length ||
            _owners.Select(o => o.FamilyId).Distinct(StringComparer.Ordinal).Count() != _owners.Count)
            throw new InvalidDataException("Wizard placement owners do not share the canonical final scene.");
        var extents = _owners.SelectMany(o => new[] { o.Row, o.Properties }).OrderBy(e => e.Offset).ToArray();
        for (int i = 1; i < extents.Length; i++)
            if (extents[i].Offset < extents[i - 1].EndExclusive)
                throw new InvalidDataException("Wizard placement row/private owners alias.");
        for (int i = 0; i < _owners.Count; i++)
        {
            var owner = _owners[i];
            var entry = ledger.Entries[i];
            if (owner.NativeIndex != 128 + i || entry.NativeIndex != owner.NativeIndex ||
                entry.FamilyId != owner.FamilyId || owner.RoleId != "wizard" || entry.RoleId != owner.RoleId ||
                entry.Role.DonorLevelId != 33 || entry.Role.DonorTrueIndex != 6 || entry.NativeClass != 0x11B ||
                owner.Row.Space != domain || owner.Properties.Space != domain ||
                owner.Row.ByteLength != 0x58 || owner.Properties.ByteLength != 0x50 ||
                owner.Row.Offset != 0x8C38L + owner.NativeIndex * 0x58L ||
                owner.Row.EndExclusive > 0xE038 || owner.Properties.Offset < 0xEED4 ||
                owner.SourcePropertiesPointer != 0xC350 || owner.SourceRowSha256 != DonorRowSha256 ||
                owner.SourcePropertiesSha256 != DonorPropertiesSha256)
                throw new InvalidDataException("Wizard placement role, source schema or allocated owner changed.");
            int row = checked((int)owner.Row.Offset), props = checked((int)owner.Properties.Offset);
            if (I32(scene, row) != props || I32(scene, props) != props + 0x28 ||
                I32(scene, props + 0x28) != 2 || I32(scene, props + 0x2C) != -65536 ||
                I32(scene, props + 0x3C) != 0 || I32(scene, props + 0x4C) != 0 ||
                I32(scene, props + 0x38) - I32(scene, row + 20) != 287 ||
                I32(scene, props + 0x48) != I32(scene, props + 0x38))
                throw new InvalidDataException("Wizard allocated row/private route preimage changed.");
            byte[] nativeRow = scene.AsSpan(row, 0x58).ToArray();
            byte[] nativeProps = scene.AsSpan(props, 0x50).ToArray();
            BinaryPrimitives.WriteInt32LittleEndian(nativeRow, owner.SourcePropertiesPointer);
            BinaryPrimitives.WriteInt32LittleEndian(nativeProps, owner.SourcePropertiesPointer + 0x28);
            if (Hash(nativeRow) != DonorRowSha256 || Hash(nativeProps) != DonorPropertiesSha256)
                throw new InvalidDataException("Wizard allocated owner changed an unowned donor byte.");
        }
        int model = I32(data, 8), length = I32(data, 12);
        _terrain = NativeRouteSurfaceSnapshot.Capture(data.AsSpan(model, length).ToArray(), 0xF01000L + model);
        _residents = Enumerable.Range(0, 128).Select(i =>
            (I32(original, 0x8C38 + i * 0x58 + 12), I32(original, 0x8C38 + i * 0x58 + 16))).ToArray();
    }

    public string SourceImageSha256 => NativeMobySourceInventory.OriginalImageSha256;
    public string SourceDataSha256 => DarkDataSha256;
    public string SourceSchemaSha256 => _append.SourceSchemaSha256;
    public string CollisionSha256 => _terrain.CollisionSha256;
    public int OriginalResidentCount => _residents.Length;
    public int FamilyCount => _owners.Count;

    public static NativeDarkHollowWizardAppendPlacementSource Capture(string originalImagePath,
        NativeDarkHollowWizardAppendPlan appended)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(originalImagePath);
        ArgumentNullException.ThrowIfNull(appended);
        using FileStream image = new(originalImagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        RequireOriginal(image);
        DiscLayout layout = DiscImage.DetectLayout(originalImagePath);
        if (layout.SectorSize != 2352 || layout.UserOffset != 24)
            throw new InvalidDataException("Count-aware placement source physical geometry changed.");
        byte[] header = DiscImage.ReadFileBytes(image, layout, 37, 0, 0x800);
        if (I32(header, 14 * 8) != 0xF01000 || I32(header, 14 * 8 + 4) != 0x28D800)
            throw new InvalidDataException("Count-aware placement source WAD owner changed.");
        byte[] data = DiscImage.ReadFileBytes(image, layout, 37, 0xF01000, 0x28D800);
        var result = new NativeDarkHollowWizardAppendPlacementSource(appended, data);
        RequireOriginal(image);
        return result;
    }

    public NativeDarkHollowWizardAppendPlacementPlan Compile(
        IEnumerable<NativeWizardFamilyPlacementIntent> completeActivePlacements) =>
        NativeDarkHollowWizardAppendPlacementPlan.Build(this, completeActivePlacements);

    internal NativeDarkHollowWizardAppendPlan Appended => _append;

    internal NativeWizardFamilyPlacementEvaluation[] Evaluate(IEnumerable<NativeWizardFamilyPlacementIntent> values)
    {
        NativeWizardFamilyPlacementIntent[] snapshot = Snapshot(values);
        var byFamily = new Dictionary<string, NativeWizardPlacementIntent>(StringComparer.Ordinal);
        foreach (var request in snapshot)
        {
            if (request == null || request.Placement == null)
                throw new InvalidDataException("Missing Wizard family placement intent.");
            NativeMobyPersistentOwnerLayout.RequireId(request.FamilyId);
            if (!byFamily.TryAdd(request.FamilyId, request.Placement))
                throw new InvalidDataException("Duplicate Wizard placement family identity.");
            NativeWizardPlacementGeometry.Validate(request.Placement);
        }
        if (byFamily.Count != _owners.Count || _owners.Any(o => !byFamily.ContainsKey(o.FamilyId)))
            throw new InvalidDataException("Wizard placements are not an exact active FamilyId bijection.");
        NativeWizardPlacementIntent[] intents = _owners.Select(o => byFamily[o.FamilyId]).ToArray();
        int[] grounds = new int[_owners.Count];
        for (int i = 0; i < intents.Length; i++)
        {
            var intent = intents[i];
            int? ground = NativeRouteSurfaceCertificate.FindUniqueFlatGround(_terrain,
                new(intent.OriginX, intent.OriginY, intent.OriginZ));
            if (ground is not { } z)
                throw new InvalidDataException("Wizard placement has no unique flat source ground datum.");
            if (intent.OriginZ < z || intent.OriginZ > z + 512)
                throw new InvalidDataException("Wizard initial height is outside the authored ground..ground+512 interval.");
            grounds[i] = z;
            for (int r = 0; r < _residents.Length; r++)
                if (!NativeWizardPlacementGeometry.IsStrictlySeparated(intent.OriginX, intent.OriginY, intent.EndX,
                    intent.EndY, _residents[r].X, _residents[r].Y, 3072))
                    throw new InvalidDataException($"Wizard family {_owners[i].FamilyId} violates original Moby {r} center separation.");
        }
        for (int i = 0; i < intents.Length; i++)
            for (int j = i + 1; j < intents.Length; j++)
            {
                var a = intents[i]; var b = intents[j];
                if (!NativeWizardPlacementPairGeometry.IsStrictlySeparated(a.OriginX, a.OriginY, a.EndX, a.EndY,
                    b.OriginX, b.OriginY, b.EndX, b.EndY, 3072))
                    throw new InvalidDataException($"Wizard families {_owners[i].FamilyId}/{_owners[j].FamilyId} violate closed-route center separation.");
            }
        var result = new NativeWizardFamilyPlacementEvaluation[intents.Length];
        for (int i = 0; i < intents.Length; i++)
        {
            var intent = intents[i];
            NativeRoutePoint[] route = [new(intent.OriginX, intent.OriginY, intent.OriginZ),
                new(intent.OriginX, intent.OriginY, intent.OriginZ + 287),
                new(intent.EndX, intent.EndY, intent.OriginZ + 287)];
            // Each existing evaluator has a finite work/issue/binding budget.
            // Family count is bounded by the source scene's proved append capacity.
            var surface = NativeRouteSurfaceCertificate.Evaluate(_terrain, route, new(grounds[i], 512, 1536));
            if (!surface.Accepted)
                throw new InvalidDataException("Wizard authored surface corridor refused: " +
                    string.Join(",", surface.Issues.Select(issue => issue.Code).Distinct()));
            result[i] = new(_owners[i], intent, surface);
        }
        return result;
    }

    private NativeWizardFamilyPlacementIntent[] Snapshot(IEnumerable<NativeWizardFamilyPlacementIntent> values)
    {
        if (values == null) throw new InvalidDataException("Missing complete Wizard placement requests.");
        int? declared = values switch
        {
            ICollection<NativeWizardFamilyPlacementIntent> c => c.Count,
            IReadOnlyCollection<NativeWizardFamilyPlacementIntent> c => c.Count,
            _ => null
        };
        if (declared is < 0 || declared > _owners.Count)
            throw new InvalidDataException("Wizard placement declared request count exceeds its source-owned family capacity.");
        List<NativeWizardFamilyPlacementIntent> result = [];
        using var enumerator = values.GetEnumerator();
        while (enumerator.MoveNext())
        {
            if (result.Count == _owners.Count)
                throw new InvalidDataException("Wizard placement exceeded its source-owned family capacity.");
            result.Add(enumerator.Current);
        }
        if (declared.HasValue && declared.Value != result.Count)
            throw new InvalidDataException("Wizard placement enumeration disagrees with its declared count.");
        return result.ToArray();
    }

    private static void RequireOriginal(FileStream image)
    {
        if (image.Length != NativeMobySourceInventory.OriginalImageByteLength)
            throw new InvalidDataException("Count-aware Wizard placement source length changed.");
        image.Position = 0;
        string hash = Convert.ToHexStringLower(SHA256.HashData(image));
        if (hash != NativeMobySourceInventory.OriginalImageSha256 ||
            image.Position != NativeMobySourceInventory.OriginalImageByteLength ||
            image.Length != NativeMobySourceInventory.OriginalImageByteLength)
            throw new InvalidDataException("Count-aware Wizard placement requires the unchanged complete original image.");
    }
    internal static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static int I32(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadInt32LittleEndian(bytes[offset..]);
}
